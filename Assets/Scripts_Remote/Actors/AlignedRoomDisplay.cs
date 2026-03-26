using UnityEngine;

/// <summary>
/// 【役者 / Actor - Aligned Room Display】
/// Local WorkerがアラインしたメッシュのTransformを受け取り、同じ位置・回転・スケールを
/// Remote Expert側のメッシュに適用する。
///
/// Localの AlignableTarget が「移動させる役者」なら、
/// こちらは「受け取って同期させる役者」。
///
/// データ経路:
///   Photon Event 101 → PhotonAlignmentReceiver → AlignmentEventHubSO
///   → AlignedRoomDisplay が受信 → targetMesh.position/rotation/scale を直接適用
///
/// アタッチ場所: [RemoteRoom] 空のGameObject
/// </summary>
public class AlignedRoomDisplay : MonoBehaviour
{
    [Header("Event Channel (必須)")]
    [Tooltip("AlignmentEventHub.assetをここへドラッグ")]
    [SerializeField] private AlignmentEventHubSO eventHub;

    [Header("表示対象")]
    [Tooltip("Transformを適用する対象メッシュ。RoomMeshSpawnerが動的に設定するため空欄でもOK。")]
    [SerializeField] private Transform targetMesh;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[AlignedRoomDisplay] AlignmentEventHubSO がアサインされていません！");
            this.enabled = false; return;
        }
        if (targetMesh == null)
            Debug.LogWarning("[AlignedRoomDisplay] targetMesh が未アサインです。" +
                             "OnTargetMeshSpawned で動的に設定されることを期待します。");
    }

    private void OnEnable()
    {
        if (eventHub == null) return;
        eventHub.OnMoveRequested    += HandleMove;
        eventHub.OnRotateRequested  += HandleRotate;
        eventHub.OnScaleRequested   += HandleScale;
        eventHub.OnTargetMeshSpawned += HandleMeshSpawned;
        Debug.Log("[AlignedRoomDisplay] 掲示板の購読を開始しました。");
    }

    private void OnDisable()
    {
        if (eventHub == null) return;
        eventHub.OnMoveRequested    -= HandleMove;
        eventHub.OnRotateRequested  -= HandleRotate;
        eventHub.OnScaleRequested   -= HandleScale;
        eventHub.OnTargetMeshSpawned -= HandleMeshSpawned;
        Debug.Log("[AlignedRoomDisplay] 掲示板の購読を解除しました。");
    }

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleMeshSpawned(Transform spawnedMesh)
    {
        targetMesh = spawnedMesh;
        Debug.Log($"[AlignedRoomDisplay] targetMesh を動的に設定しました: {targetMesh.name}");
    }

    /// <summary>deltaをWorld座標でそのまま適用（Photon受信時は絶対位置が来るため、
    /// PhotonAlignmentReceiverは直接 targetMesh.position を書き換える設計。
    /// このハンドラはLocalのAlignableTargetと対称性を保つために存在する。）</summary>
    private void HandleMove(Vector3 delta)
    {
        if (targetMesh == null) return;
        targetMesh.position += delta;
    }

    private void HandleRotate(Vector3 axis, float angle)
    {
        if (targetMesh == null) return;
        targetMesh.Rotate(axis, angle, Space.World);
    }

    private void HandleScale(float delta)
    {
        if (targetMesh == null) return;
        targetMesh.localScale += Vector3.one * delta;
    }
}
