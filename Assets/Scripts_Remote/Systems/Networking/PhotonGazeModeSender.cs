using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// 【裏方 / System - Gaze Mode 送信】
/// 掲示板（GazeEventHubSO）のモード変更要求を監視し、
/// Photonネットワーク経由でRemote Expert（他拠点のクライアント）へデータを発信する。
///
/// Photon Event Code: 103
/// Payload: object[] { (int)mode }
///
/// アタッチ場所: Hierarchy内の [Systems] という空のGameObject
/// </summary>
public class PhotonGazeModeSender : MonoBehaviour
{
    public const byte GazeModeEventCode = 103;

    [Header("Event Channel (必須)")]
    [Tooltip("状態変更を監視する掲示板。GazeEventHub.assetをここへドラッグ。")]
    [SerializeField] private GazeEventHubSO eventHub;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[PhotonGazeModeSender] {gameObject.name}: GazeEventHubSO がアサインされていません！コンポーネントを停止します。");
            this.enabled = false; return;
        }
    }

    private void OnEnable()
    {
        if (eventHub == null) return;
        eventHub.OnModeChangeRequested += HandleModeChange;
        Debug.Log("[PhotonGazeModeSender] 掲示板の監視を開始しました（妹・発信担当）。");
    }

    private void OnDisable()
    {
        if (eventHub == null) return;
        eventHub.OnModeChangeRequested -= HandleModeChange;
        Debug.Log("[PhotonGazeModeSender] 掲示板の監視を解除しました。");
    }

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleModeChange(int mode)
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonGazeModeSender] ネットワーク未接続。Photonへの横流し（送信）をスキップしました。");
            return;
        }

        try
        {
            object[] data = new object[] { mode };
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(GazeModeEventCode, data, opts, SendOptions.SendReliable);

            Debug.Log($"[PhotonGazeModeSender] 掲示板のモード変更情報をPhotonへ送信しました。 mode={mode}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PhotonGazeModeSender] Photon送信中にエラーが発生しました: {e.Message}");
        }
    }
}
