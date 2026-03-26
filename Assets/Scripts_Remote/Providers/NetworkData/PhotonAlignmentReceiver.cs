using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// 【翻訳者 / Provider - NetworkData (Photon受信・姉)】
/// Photonネットワークから届いたアライメントデータを受信し、
/// 掲示板（AlignmentEventHubSO）に標準語で代筆する。
///
/// 役割の分離:
/// - 本クラスは「受信→掲示板への書き込み」のみに責任を持つ（姉）。
/// - 「掲示板→Photonへの送信」は PhotonAlignmentSender（妹）が担当。
/// - メッシュのTransformを直接操作することは一切しない。
///
/// アタッチ場所: Hierarchy内の [InputProviders] または [NetworkData] という空のGameObject
/// </summary>
public class PhotonAlignmentReceiver : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte AlignmentEventCode = 101;

    [Header("Event Channel (必須)")]
    [Tooltip("受信データを代筆する掲示板。AlignmentEventHub.assetをここへドラッグ。")]
    [SerializeField] private AlignmentEventHubSO eventHub;

    [Header("適用対象 (必須)")]
    [Tooltip("受信したTransformを直接セットする対象メッシュ。（位置上書き方式のため）")]
    [SerializeField] private Transform targetMesh;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        bool hasError = false;
        if (eventHub == null)
        {
            Debug.LogError("[PhotonAlignmentReceiver] AlignmentEventHubSO がアサインされていません！コンポーネントを停止します。");
            this.enabled = false; return;
        }
        if (targetMesh == null)
        {
            Debug.LogWarning("[PhotonAlignmentReceiver] targetMesh が未アサインです。OnTargetMeshSpawned で動的に設定されることを期待します。");
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
        if (eventHub != null) eventHub.OnTargetMeshSpawned += HandleMeshSpawned;
        Debug.Log("[PhotonAlignmentReceiver] Photonの監視（Event受信）を開始しました（姉・受信担当）。");
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
        if (eventHub != null) eventHub.OnTargetMeshSpawned -= HandleMeshSpawned;
        Debug.Log("[PhotonAlignmentReceiver] Photonの監視を解除しました。");
    }

    // ─── Photonイベント受信 ─────────────────────────────

    private void HandleMeshSpawned(Transform spawnedMesh)
    {
        targetMesh = spawnedMesh;
        Debug.Log($"[PhotonAlignmentReceiver] targetMesh を動的に設定しました: {targetMesh.name}");
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != AlignmentEventCode) return;

        try
        {
            object[] data = (object[]) photonEvent.CustomData;
            if (data == null || data.Length < 10)
            {
                Debug.LogWarning($"[PhotonAlignmentReceiver] 不正なデータ形式を受信しました。 " +
                                 $"dataLength={(data?.Length ?? 0)}");
                return;
            }

            // 旧 NetworkedDataReceiver.OnPhotonEvent() Code101受信アルゴリズムと互換
            Vector3 pos = new Vector3(
                System.Convert.ToSingle(data[0]),
                System.Convert.ToSingle(data[1]),
                System.Convert.ToSingle(data[2])
            );
            Quaternion rot = new Quaternion(
                System.Convert.ToSingle(data[3]),
                System.Convert.ToSingle(data[4]),
                System.Convert.ToSingle(data[5]),
                System.Convert.ToSingle(data[6])
            );
            Vector3 scale = new Vector3(
                System.Convert.ToSingle(data[7]),
                System.Convert.ToSingle(data[8]),
                System.Convert.ToSingle(data[9])
            );

            Debug.Log($"[PhotonAlignmentReceiver] Photonからアライメントデータを受信しました。" +
                      $" 受信＝position:{pos}, rotation:{rot.eulerAngles}, scale:{scale}");

            // NOTE: 受信データは絶対座標のためEventChannel経由ではなく直接適用。
            // deltaでなく絶対値で届くため、分割送信方式(delta)とは設計が異なる。
            // 将来的にはEventHubに「絶対位置セット要求」を追加して完全に疎結合化することも可能。
            targetMesh.position   = pos;
            targetMesh.rotation   = rot;
            targetMesh.localScale = scale;

            Debug.Log($"[PhotonAlignmentReceiver] 受信データをメッシュに適用しました（空間同期完了）。");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PhotonAlignmentReceiver] データ解析中にエラー: {e.Message}");
        }
    }
}
