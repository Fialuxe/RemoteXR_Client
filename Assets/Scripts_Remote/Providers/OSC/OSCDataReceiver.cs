using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// 【翻訳者 / Provider - OSC (生体データ受信)】
/// Python（ウェブカメラ・視線推定スクリプト）からOSCプロトコル（UDP）で届く
/// 視線・顔ランドマークデータを受信し、BiometricEventHubSO に書き込む。
///
/// 旧 OscDataReceiver.cs のリファクタリング版。
/// 変更点:
///   - 公開ゲッター（GetGazePosition等）を廃止。掲示板への書き込みに一本化。
///   - Face/Gaze タイムアウト検知でそれぞれ LostXxx() を発火。
///   - モックモード（useFakeData）はそのまま維持（デバッグ用）。
///
/// OSCメッセージ形式:
///   /gaze     x y pupil       (正規化画面座標 0〜1 + 瞳孔サイズ)
///   /facemesh index x y z     (ランドマーク番号 0〜67 + 座標)
///
/// アタッチ場所: [InputProviders] または [Systems] 空のGameObject
/// </summary>
public class OSCDataReceiver : MonoBehaviour
{
    [Header("Event Channels (必須)")]
    [Tooltip("BiometricEventHub.assetをここへドラッグ")]
    [SerializeField] private BiometricEventHubSO biometricHub;

    [Header("OSC ネットワーク設定")]
    [Tooltip("Pythonからのデータを受け取るポート番号")]
    [SerializeField] private int receivePort = 8000;

    [Header("タイムアウト設定")]
    [Tooltip("この秒数データが来なければ LostGaze/LostFaceData を発火 [s]")]
    [SerializeField] private float dataTimeoutSeconds = 2f;

    [Header("モックモード（オフラインデバッグ用）")]
    [Tooltip("ONにするとPythonなしでダミーデータを生成（円形視線パターン）")]
    [SerializeField] private bool useFakeData = false;
    [Tooltip("ダミーデータの送信レート [Hz]")]
    [SerializeField] private float fakeDataRate = 30f;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugGUI = false;

    // ─── 内部状態 ─────────────────────────────────────

    private UdpClient _udpClient;
    private bool _isRunning;
    private float _lastGazeTime;
    private float _lastFaceTime;
    private bool _gazeActive;
    private bool _faceActive;

    // ダミーデータ生成用
    private float _fakeTimer;
    private float _fakeGazeAngle;

    // デバッグ表示用
    private Vector2 _lastGazePos;
    private float   _lastPupil;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (biometricHub == null)
        {
            Debug.LogError("[OSCDataReceiver] BiometricEventHubSO がアサインされていません！");
            this.enabled = false; return;
        }
    }

    private void Start()
    {
        if (!useFakeData)
            StartUDP();
    }

    private void Update()
    {
        if (useFakeData)
        {
            UpdateFakeData();
            return;
        }
        if (!_isRunning || _udpClient == null) return;

        // 届いているUDPパケットを全て処理
        while (_udpClient.Available > 0)
        {
            try
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udpClient.Receive(ref ep);
                ParseOSCMessage(data);
            }
            catch (SocketException) { break; }
            catch (Exception e)
            {
                Debug.LogWarning($"[OSCDataReceiver] 受信エラー: {e.Message}");
                break;
            }
        }

        // タイムアウト検知
        if (_gazeActive && Time.time - _lastGazeTime > dataTimeoutSeconds)
        {
            _gazeActive = false;
            biometricHub.LostGaze();
            Debug.Log("[OSCDataReceiver] Gazeデータがタイムアウトしました。");
        }
        if (_faceActive && Time.time - _lastFaceTime > dataTimeoutSeconds)
        {
            _faceActive = false;
            biometricHub.LostFaceData();
            Debug.Log("[OSCDataReceiver] Faceデータがタイムアウトしました。");
        }
    }

    private void OnDestroy() => CloseUDP();
    private void OnApplicationQuit() => CloseUDP();

    // ─── UDP ─────────────────────────────────────────

    private void StartUDP()
    {
        try
        {
            _udpClient = new UdpClient(receivePort);
            _udpClient.Client.ReceiveTimeout = 100;
            _isRunning = true;
            Debug.Log($"<color=green>[OSCDataReceiver] UDP受信開始。ポート={receivePort}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"[OSCDataReceiver] UDP起動失敗（port={receivePort}）: {e.Message}");
            _isRunning = false;
        }
    }

    private void CloseUDP()
    {
        _isRunning = false;
        _udpClient?.Close();
        _udpClient = null;
    }

    // ─── OSC パース ──────────────────────────────────

    private void ParseOSCMessage(byte[] data)
    {
        if (data == null || data.Length < 8) return;
        try
        {
            int idx = 0;
            string address  = ReadString(data, ref idx);
            string typeTags = ReadString(data, ref idx); // e.g. ",fff"

            if (address == "/gaze")
            {
                float x      = ReadFloat(data, ref idx);
                float y      = ReadFloat(data, ref idx);
                float pupil  = ReadFloat(data, ref idx);

                if (IsValid(x) && IsValid(y) && IsValid(pupil))
                {
                    Vector2 pos = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
                    pupil = Mathf.Clamp(pupil, 0f, 10f);
                    _lastGazePos = pos;
                    _lastPupil   = pupil;
                    _lastGazeTime = Time.time;
                    _gazeActive   = true;
                    biometricHub.PublishGaze(pos, pupil);
                }
            }
            else if (address == "/facemesh")
            {
                int   index = (int)ReadFloat(data, ref idx);
                float x     = ReadFloat(data, ref idx);
                float y     = ReadFloat(data, ref idx);
                float z     = ReadFloat(data, ref idx);

                if (index >= 0 && index < 68 && IsValid(x) && IsValid(y) && IsValid(z))
                {
                    Vector3 pos = new Vector3(
                        Mathf.Clamp(x, -10f, 10f),
                        Mathf.Clamp(y, -10f, 10f),
                        Mathf.Clamp(z, -10f, 10f));
                    _lastFaceTime = Time.time;
                    _faceActive   = true;
                    biometricHub.PublishFaceLandmark(index, pos);
                }
            }
        }
        catch (Exception e)
        {
            if (showDebugGUI)
                Debug.LogWarning($"[OSCDataReceiver] OSCパースエラー: {e.Message}");
        }
    }

    // ─── OSC バイト解析ヘルパー（旧実装から継承・整理）──────

    private static string ReadString(byte[] data, ref int idx)
    {
        int start = idx;
        while (idx < data.Length && data[idx] != 0) idx++;
        string s = Encoding.ASCII.GetString(data, start, idx - start);
        idx++;
        while (idx % 4 != 0) idx++;
        return s;
    }

    private static float ReadFloat(byte[] data, ref int idx)
    {
        if (idx + 4 > data.Length) return 0f;
        // OSCはビッグエンディアン
        byte[] b = { data[idx+3], data[idx+2], data[idx+1], data[idx] };
        idx += 4;
        return BitConverter.ToSingle(b, 0);
    }

    private static bool IsValid(float v) =>
        !float.IsNaN(v) && !float.IsInfinity(v) && Mathf.Abs(v) < 1e6f;

    // ─── モックデータ生成 ─────────────────────────────

    private void UpdateFakeData()
    {
        _fakeTimer += Time.deltaTime;
        if (_fakeTimer < 1f / fakeDataRate) return;
        _fakeTimer = 0f;

        // 円形視線パターン
        _fakeGazeAngle += Time.deltaTime * 2f;
        Vector2 gazePos = new Vector2(
            0.5f + Mathf.Cos(_fakeGazeAngle) * 0.3f,
            0.5f + Mathf.Sin(_fakeGazeAngle) * 0.3f);
        float pupil = 3f + Mathf.Sin(Time.time * 2f) * 0.5f;

        _lastGazePos  = gazePos;
        _lastPupil    = pupil;
        _lastGazeTime = Time.time;
        _gazeActive   = true;
        biometricHub.PublishGaze(gazePos, pupil);

        // 顔ランドマーク（ブリージングアニメーション）
        for (int i = 0; i < 68; i++)
        {
            float angle   = (i / 68f) * Mathf.PI * 2f;
            float breathe = Mathf.Sin(Time.time * 1.5f) * 0.02f;
            Vector3 pos   = new Vector3(
                0.5f + Mathf.Cos(angle) * (0.15f + breathe),
                0.5f + Mathf.Sin(angle) * (0.20f + breathe),
                Mathf.Sin(angle * 4f) * 0.05f + breathe);
            biometricHub.PublishFaceLandmark(i, pos);
        }
        _lastFaceTime = Time.time;
        _faceActive   = true;
    }

    // ─── デバッグGUI ─────────────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;
        GUILayout.BeginArea(new Rect(10, 10, 280, 160));
        GUILayout.BeginVertical("box");
        GUILayout.Label("=== OSC DATA RECEIVER ===");
        GUILayout.Label($"Mode: {(useFakeData ? "MOCK (fake data)" : $"OSC port={receivePort}")}");
        GUILayout.Label($"Running: {_isRunning}");
        GUILayout.Label($"Gaze: {(_gazeActive ? $"({_lastGazePos.x:F2}, {_lastGazePos.y:F2}) p={_lastPupil:F2}" : "No data")}");
        GUILayout.Label($"Face: {(_faceActive ? "Receiving" : "No data")}");
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
