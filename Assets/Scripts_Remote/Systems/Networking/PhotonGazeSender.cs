using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// 【裏方 / System - Gaze送信】
/// GazeEventHubSO を購読し、3D視線データをPhoton Event 102 でLocal Workerに送信する。
///
/// データ経路（上流）:
///   OSCDataReceiver → BiometricEventHubSO → GazeRayCalculator → GazeEventHubSO → 本クラス
///
/// 旧実装との差分:
///   - カメラのforward方向をポーリングする方式から、GazeEventHubSOを購読する方式に変更。
///   - カメラへの直接依存を排除。GazeEventHub からイベントが来た時だけ送信する。
///
/// Photon Event Code: 102
/// Payload: [originX, originY, originZ, dirX, dirY, dirZ, hitX, hitY, hitZ, hasHit(bool)]
///
/// アタッチ場所: [Systems] 空のGameObject
/// </summary>
public class PhotonGazeSender : MonoBehaviour
{
    public const byte GazeEventCode = 102;

    [Header("Event Channel (必須)")]
    [Tooltip("GazeEventHub.assetをここへドラッグ")]
    [SerializeField] private GazeEventHubSO gazeHub;

    [Header("送信レート制限")]
    [Tooltip("最小送信間隔 [s]。頻繁すぎる送信を防ぎ帯域を節約する。")]
    [SerializeField] private float minSendInterval = 0.05f; // 最大20fps

    [Header("オフラインデバッグ")]
    [Tooltip("Photon未接続時に別のGazeEventHubSOへローカル転送する（ループバックテスト用）")]
    [SerializeField] private GazeEventHubSO localDebugHub;

    private float _lastSendTime;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (gazeHub == null)
        {
            Debug.LogError("[PhotonGazeSender] GazeEventHubSO がアサインされていません！");
            this.enabled = false; return;
        }
    }

    private void OnEnable()
    {
        if (gazeHub == null) return;
        gazeHub.OnGazeUpdated += HandleGazeUpdated;
        gazeHub.OnGazeLost    += HandleGazeLost;
        Debug.Log("[PhotonGazeSender] GazeEventHubの購読を開始しました。");
    }

    private void OnDisable()
    {
        if (gazeHub == null) return;
        gazeHub.OnGazeUpdated -= HandleGazeUpdated;
        gazeHub.OnGazeLost    -= HandleGazeLost;
        Debug.Log("[PhotonGazeSender] GazeEventHubの購読を解除しました。");
    }

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleGazeUpdated(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal, bool hasHit)
    {
        // レート制限
        if (Time.time - _lastSendTime < minSendInterval) return;
        _lastSendTime = Time.time;

        Send(origin, direction, hitPoint, hasHit);
    }

    private void HandleGazeLost()
    {
        // 視線消失を通知（hasHit=falseのゼロベクトルで「消えた」を表現）
        Send(Vector3.zero, Vector3.forward, Vector3.zero, false);
        Debug.Log("[PhotonGazeSender] Gaze消失を送信しました。");
    }

    // ─── Photon送信 ─────────────────────────────────

    private void Send(Vector3 origin, Vector3 direction, Vector3 hitPoint, bool hasHit)
    {
        object[] data = {
            origin.x,    origin.y,    origin.z,
            direction.x, direction.y, direction.z,
            hitPoint.x,  hitPoint.y,  hitPoint.z,
            hasHit
        };

        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
        {
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(GazeEventCode, data, opts, SendOptions.SendUnreliable);
            Debug.Log($"[PhotonGazeSender] Event102送信: origin={origin}, hasHit={hasHit}");
        }
        else if (localDebugHub != null)
        {
            // オフラインデバッグ: ループバック
            localDebugHub.UpdateGaze(origin, direction, hitPoint, -direction, hasHit);
            Debug.Log("[PhotonGazeSender] ローカルデバッグ: GazeをlocalDebugHubに転送しました。");
        }
        else
        {
            Debug.LogWarning("[PhotonGazeSender] Photon未接続かつlocalDebugHubも未設定。送信をスキップしました。");
        }
    }
}
