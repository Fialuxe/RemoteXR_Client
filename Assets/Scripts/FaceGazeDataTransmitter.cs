using UnityEngine;

/// <summary>
/// RemoteXR_Client専用: LSLから受信した視線・表情データをAlignmentNetworkHub経由でLocalXR_Clientに送信
/// PhotonViewに依存せず、AlignmentNetworkHubのRPCシステムを使用
/// 
/// 使用方法:
/// 1. RemoteXR_ClientのGameObjectにアタッチ
/// 2. LslGazeReceiverとLslFaceMeshReceiverへの参照を設定(自動検出可能)
/// 3. 自動的にデータ送信を開始
/// </summary>
public class FaceGazeDataTransmitter : MonoBehaviour
{
    [Header("LSL Receiver References")]
    [Tooltip("視線データを受信するLslGazeReceiver")]
    public LslGazeReceiver gazeReceiver;
    
    [Tooltip("表情データを受信するLslFaceMeshReceiver")]
    public LslFaceMeshReceiver faceMeshReceiver;

    [Header("Transmission Settings")]
    [Tooltip("視線データの送信間隔(秒)")]
    [Range(0.01f, 1f)]
    public float gazeTransmissionInterval = 0.033f; // 約30Hz
    
    [Tooltip("表情データの送信間隔(秒)")]
    [Range(0.01f, 1f)]
    public float faceTransmissionInterval = 0.033f; // 約30Hz
    
    [Tooltip("送信する表情ランドマークの数(最大68)")]
    [Range(10, 68)]
    public int landmarkCount = 20;

    [Header("Debug")]
    [Tooltip("デバッグログを表示")]
    public bool showDebugLogs = false;
    
    [Tooltip("画面上にステータスを表示")]
    public bool showOnScreenStatus = true;

    // 送信タイマー
    private float gazeTimer;
    private float faceTimer;
    
    // 統計
    private int gazeSentCount;
    private int faceSentCount;
    private float lastGazeSendTime;
    private float lastFaceSendTime;
    
    // キーランドマークのインデックス(68点モデルから重要な20点を選択)
    private readonly int[] keyLandmarkIndices = new int[]
    {
        // 顔の輪郭・顎
        0, 8, 16,  // 左顎、顎中央、右顎
        
        // 眉毛
        17, 21, 22, 26,  // 右眉外側、右眉内側、左眉内側、左眉外側
        
        // 鼻
        27, 30, 33,  // 鼻筋上部、鼻先端、鼻下部
        
        // 目
        36, 39, 42, 45,  // 右目外側、右目内側、左目内側、左目外側
        37, 40, 43, 46,  // 右目上部、右目下部、左目上部、左目下部
        
        // 口
        48, 54, 51, 57, 62, 66  // 口右、口左、上唇上部、下唇下部、上唇内側、下唇内側
    };

    private void Start()
    {
        // 自動検出
        if (gazeReceiver == null)
        {
            gazeReceiver = FindObjectOfType<LslGazeReceiver>();
            if (gazeReceiver != null)
                Debug.Log("[FaceGazeDataTx] LslGazeReceiverを自動検出");
        }
        
        if (faceMeshReceiver == null)
        {
            faceMeshReceiver = FindObjectOfType<LslFaceMeshReceiver>();
            if (faceMeshReceiver != null)
                Debug.Log("[FaceGazeDataTx] LslFaceMeshReceiverを自動検出");
        }
        
        // AlignmentNetworkHubの存在確認
        if (!AlignmentNetworkHub.IsReady)
        {
            Debug.LogWarning("[FaceGazeDataTx] AlignmentNetworkHubが準備できていません。Photon接続を確認してください。");
        }
        
        Debug.Log("========================================");
        Debug.Log("[FaceGazeDataTx] RemoteXR_Client データ送信開始");
        Debug.Log($"[FaceGazeDataTx] 視線レシーバー: {(gazeReceiver != null ? "検出" : "未検出")}");
        Debug.Log($"[FaceGazeDataTx] 表情レシーバー: {(faceMeshReceiver != null ? "検出" : "未検出")}");
        Debug.Log($"[FaceGazeDataTx] AlignmentNetworkHub: {(AlignmentNetworkHub.IsReady ? "準備完了" : "未準備")}");
        Debug.Log("========================================");
    }

    private void Update()
    {
        // AlignmentNetworkHubが準備できていない場合はスキップ
        if (!AlignmentNetworkHub.IsReady)
        {
            if (showDebugLogs && Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("[FaceGazeDataTx] AlignmentNetworkHubが未準備。Photon接続を待機中...");
            }
            return;
        }
        
        // 視線データ送信
        gazeTimer += Time.deltaTime;
        if (gazeTimer >= gazeTransmissionInterval)
        {
            TransmitGazeData();
            gazeTimer = 0f;
        }
        
        // 表情データ送信
        faceTimer += Time.deltaTime;
        if (faceTimer >= faceTransmissionInterval)
        {
            TransmitFaceData();
            faceTimer = 0f;
        }
    }

    private void TransmitGazeData()
    {
        if (gazeReceiver == null || !gazeReceiver.IsConnected)
        {
            if (showDebugLogs && gazeSentCount == 0)
            {
                Debug.LogWarning("[FaceGazeDataTx] 視線データレシーバーが接続されていません");
            }
            return;
        }
        
        Vector2 gazePosition = gazeReceiver.GetGazePosition();
        float pupilSize = gazeReceiver.GetPupilSize();
        
        // データの有効性チェック
        if (float.IsNaN(gazePosition.x) || float.IsNaN(gazePosition.y) || 
            float.IsInfinity(gazePosition.x) || float.IsInfinity(gazePosition.y))
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[FaceGazeDataTx] 無効な視線データ: {gazePosition}");
            }
            return;
        }
        
        // AlignmentNetworkHub経由で送信
        AlignmentNetworkHub.BroadcastGazeData(gazePosition, pupilSize);
        
        gazeSentCount++;
        lastGazeSendTime = Time.time;
        
        if (showDebugLogs && gazeSentCount % 30 == 1)
        {
            Debug.Log($"[FaceGazeDataTx] 視線データ送信 #{gazeSentCount}: {gazePosition}, pupil={pupilSize:F3}");
        }
    }

    private void TransmitFaceData()
    {
        if (faceMeshReceiver == null || !faceMeshReceiver.IsConnected)
        {
            if (showDebugLogs && faceSentCount == 0)
            {
                Debug.LogWarning("[FaceGazeDataTx] 表情データレシーバーが接続されていません");
            }
            return;
        }
        
        // キーランドマークを取得
        Vector3[] landmarks = new Vector3[Mathf.Min(landmarkCount, keyLandmarkIndices.Length)];
        bool hasValidData = false;
        
        for (int i = 0; i < landmarks.Length; i++)
        {
            int landmarkIndex = keyLandmarkIndices[i];
            Vector3 landmark = faceMeshReceiver.GetLandmark(landmarkIndex);
            landmarks[i] = landmark;
            
            // 少なくとも1つ有効なデータがあるか確認
            if (faceMeshReceiver.IsLandmarkValid(landmarkIndex))
            {
                hasValidData = true;
            }
        }
        
        if (!hasValidData)
        {
            if (showDebugLogs && faceSentCount % 100 == 0)
            {
                Debug.LogWarning("[FaceGazeDataTx] 有効な表情データがありません");
            }
            return;
        }
        
        // AlignmentNetworkHub経由で送信
        AlignmentNetworkHub.BroadcastFaceLandmarks(landmarks);
        
        faceSentCount++;
        lastFaceSendTime = Time.time;
        
        if (showDebugLogs && faceSentCount % 30 == 1)
        {
            Debug.Log($"[FaceGazeDataTx] 表情データ送信 #{faceSentCount}: {landmarks.Length}点");
        }
    }

    private void OnGUI()
    {
        if (!showOnScreenStatus)
            return;
        
        float panelWidth = 400f;
        float panelHeight = 300f;
        float margin = 10f;
        
        GUILayout.BeginArea(new Rect(Screen.width - panelWidth - margin, margin, panelWidth, panelHeight));
        GUILayout.BeginVertical("box");
        
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.cyan }
        };
        
        GUIStyle normalStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.white }
        };
        
        GUIStyle successStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.green }
        };
        
        GUIStyle errorStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.red }
        };
        
        GUILayout.Label("RemoteXR データ送信状況", headerStyle);
        GUILayout.Space(5);
        
        // NetworkHub状態
        bool hubReady = AlignmentNetworkHub.IsReady;
        GUILayout.Label($"NetworkHub: {(hubReady ? "✓ 準備完了" : "✗ 未準備")}", 
            hubReady ? successStyle : errorStyle);
        
        GUILayout.Space(5);
        
        // 視線データ
        GUILayout.Label("【視線データ】", headerStyle);
        if (gazeReceiver != null)
        {
            bool connected = gazeReceiver.IsConnected;
            GUILayout.Label($"LSL接続: {(connected ? "✓" : "✗")}", connected ? successStyle : errorStyle);
            GUILayout.Label($"送信回数: {gazeSentCount}", normalStyle);
            
            if (gazeSentCount > 0)
            {
                float timeSinceLastSend = Time.time - lastGazeSendTime;
                GUILayout.Label($"最終送信: {timeSinceLastSend:F2}秒前", normalStyle);
                
                if (connected)
                {
                    Vector2 gaze = gazeReceiver.GetGazePosition();
                    GUILayout.Label($"位置: ({gaze.x:F3}, {gaze.y:F3})", normalStyle);
                }
            }
        }
        else
        {
            GUILayout.Label("LSL接続: ✗ 未設定", errorStyle);
        }
        
        GUILayout.Space(5);
        
        // 表情データ
        GUILayout.Label("【表情データ】", headerStyle);
        if (faceMeshReceiver != null)
        {
            bool connected = faceMeshReceiver.IsConnected;
            GUILayout.Label($"LSL接続: {(connected ? "✓" : "✗")}", connected ? successStyle : errorStyle);
            GUILayout.Label($"送信回数: {faceSentCount}", normalStyle);
            
            if (faceSentCount > 0)
            {
                float timeSinceLastSend = Time.time - lastFaceSendTime;
                GUILayout.Label($"最終送信: {timeSinceLastSend:F2}秒前", normalStyle);
                GUILayout.Label($"ランドマーク数: {landmarkCount}", normalStyle);
            }
        }
        else
        {
            GUILayout.Label("LSL接続: ✗ 未設定", errorStyle);
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        Debug.Log("========================================");
        Debug.Log("[FaceGazeDataTx] 送信統計");
        Debug.Log($"  視線データ: {gazeSentCount}回送信");
        Debug.Log($"  表情データ: {faceSentCount}回送信");
        Debug.Log("========================================");
    }
}
