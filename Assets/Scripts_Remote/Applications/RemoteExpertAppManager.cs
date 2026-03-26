using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// 【Application / AppManager (Remote Expert)】
/// Remote Expert アプリ全体の初期化を担う「支配人」。
///
/// 責務:
///   1. Photonへの接続とルーム参加管理
///   2. 仮想世界の開始位置・視点を設定（移動可能なPlayerCameraの初期化）
///   3. 接続状態のログ出力
///
/// Localの LocalWorkerAppManager と対称な設計。
/// VRカメラ解決は不要（PC向けなので Camera.main または [PlayerCamera] を使う）。
///
/// アタッチ場所: [AppManager] 空のGameObject
/// </summary>
public class RemoteExpertAppManager : MonoBehaviourPunCallbacks
{
    [Header("Photon 設定")]
    [SerializeField] private string roomName = "MeshVRRoom";
    [SerializeField] private int maxPlayers = 4;

    [Header("PlayerCamera (仮想世界の視点)")]
    [Tooltip("仮想空間内を動き回るカメラ。空欄なら Camera.main を使用。")]
    [SerializeField] private Transform playerCamera;

    [Tooltip("仮想空間に入った際の初期位置（メッシュの外から見下ろす位置など）")]
    [SerializeField] private Vector3 initialCameraPosition = new Vector3(0f, 1.6f, -3f);

    [Tooltip("初期カメラ向き（オイラー角）")]
    [SerializeField] private Vector3 initialCameraRotation = new Vector3(10f, 0f, 0f);

    // ─── ライフサイクル ─────────────────────────────────

    private void Start()
    {
        InitializePlayerCamera();
        ConnectToPhoton();
    }

    // ─── カメラ初期化 ─────────────────────────────────

    /// <summary>
    /// 仮想世界の視点（PlayerCamera）を初期化する。
    /// 移動可能なカメラとして、KeyboardTranslator・MouseInteractionTranslator・PhotonGazeSender
    /// が共通で参照する。
    /// </summary>
    private void InitializePlayerCamera()
    {
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
            Debug.Log($"[RemoteExpertAppManager] PlayerCameraを自動解決: {playerCamera.name}");
        }

        if (playerCamera == null)
        {
            Debug.LogError("[RemoteExpertAppManager] PlayerCameraが見つかりません！" +
                           " Inspector で [PlayerCamera] を指定してください。");
            return;
        }

        // 仮想世界の開始位置・向きを設定
        playerCamera.position = initialCameraPosition;
        playerCamera.rotation = Quaternion.Euler(initialCameraRotation);
        Debug.Log($"[RemoteExpertAppManager] PlayerCameraを初期位置に配置しました。" +
                  $" pos={initialCameraPosition}, rot={initialCameraRotation}");
    }

    // ─── Photon接続 ─────────────────────────────────

    private void ConnectToPhoton()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[RemoteExpertAppManager] 既にPhotonへ接続済みです。ルーム参加を試みます。");
            TryJoinRoom();
            return;
        }

        PhotonNetwork.NickName = "RemoteExpert_" + Random.Range(1000, 9999);
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log($"[RemoteExpertAppManager] Photonへ接続開始。NickName={PhotonNetwork.NickName}");
    }

    private void TryJoinRoom()
    {
        RoomOptions options = new RoomOptions { MaxPlayers = (byte)maxPlayers };
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
        Debug.Log($"[RemoteExpertAppManager] ルーム '{roomName}' への参加を試みます。");
    }

    // ─── Photonコールバック ─────────────────────────────

    public override void OnConnectedToMaster()
    {
        Debug.Log("<color=green>[RemoteExpertAppManager] Photon Masterへ接続完了。</color>");
        TryJoinRoom();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"<color=green>[RemoteExpertAppManager] ルーム '{PhotonNetwork.CurrentRoom.Name}' に参加しました。" +
                  $" 現在人数: {PhotonNetwork.CurrentRoom.PlayerCount}</color>");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[RemoteExpertAppManager] ルーム参加失敗。 code={returnCode}, message={message}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[RemoteExpertAppManager] Photon切断。 cause={cause}");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[RemoteExpertAppManager] Local Worker が参加しました: {newPlayer.NickName}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[RemoteExpertAppManager] Local Worker が退出しました: {otherPlayer.NickName}");
    }

    // ─── 公開プロパティ ─────────────────────────────────

    /// <summary>仮想世界を移動するPlayerCameraのTransform（他Systemが参照したい場合用）</summary>
    public Transform PlayerCamera => playerCamera;
}
