using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// 【Application / Avatar Spawner】
/// Photonルームに参加した時点で、このクライアントの「位置」を表す仮アバター（球）を
/// ネットワーク越しにInstantiateする。
///
/// 目的:
///   - Unityの Scene ビューで Remote Expert と Local Worker がどこにいるかをデバッグできる。
///   - カメラ（PlayerCamera または HMDカメラ）にアタッチした Transform を毎フレーム球に追従させる。
///   - Photon の PhotonTransformView により、他のクライアントからも球の位置が見える。
///
/// 前提:
///   - Resourcesフォルダに "AvatarSphere" という名前のPrefabが必要（自動作成の場合は後述）。
///   - AvatarSphereには PhotonView + PhotonTransformView + SphereRenderer が必要。
///   - このスクリプトは LocalWorkerAppManager または RemoteExpertAppManager と同一のGameObjectか
///     [AppManager]にアタッチする。
///
/// アタッチ場所: [AppManager] 空のGameObject
/// </summary>
public class NetworkAvatarSpawner : MonoBehaviourPunCallbacks
{
    [Header("追従させるカメラ")]
    [Tooltip("アバター球が追従するカメラTransform。空欄なら Camera.main を使用。")]
    [SerializeField] private Transform trackingTarget;

    [Header("アバター設定")]
    [Tooltip("Resourcesフォルダ内のPrefab名。存在しない場合はランタイムで球を生成する。")]
    [SerializeField] private string avatarPrefabName = "AvatarSphere";

    [Tooltip("アバター球の大きさ")]
    [SerializeField] private float avatarRadius = 0.1f;

    [Tooltip("Local（自分自身）のアバター色")]
    [SerializeField] private Color localColor = new Color(0f, 0.8f, 1f, 0.6f); // 水色

    [Tooltip("Remote（相手）のアバター色（Photonからのインスタンスにはこちらは自動反映されません）")]
    [SerializeField] private Color remoteColor = new Color(1f, 0.4f, 0f, 0.6f); // オレンジ

    private GameObject _myAvatar;

    // ─── ライフサイクル ─────────────────────────────────

    private void Start()
    {
        if (trackingTarget == null && Camera.main != null)
        {
            trackingTarget = Camera.main.transform;
            Debug.Log($"[NetworkAvatarSpawner] TrackingTargetを自動解決: {trackingTarget.name}");
        }
        if (trackingTarget == null)
            Debug.LogWarning("[NetworkAvatarSpawner] TrackingTargetが見つかりません。Inspector で指定してください。");

        // すでにルームにいるなら即スポーン（シーン再ロード後など）
        if (PhotonNetwork.InRoom)
            SpawnAvatar();
    }

    private void LateUpdate()
    {
        // 自分のアバターをカメラに追従させる
        if (_myAvatar != null && trackingTarget != null)
            _myAvatar.transform.position = trackingTarget.position;
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[NetworkAvatarSpawner] ルーム参加。アバターをスポーンします。");
        SpawnAvatar();
    }

    public override void OnLeftRoom()
    {
        if (_myAvatar != null)
        {
            PhotonNetwork.Destroy(_myAvatar);
            _myAvatar = null;
        }
    }

    // ─── スポーン ─────────────────────────────────────

    private void SpawnAvatar()
    {
        if (_myAvatar != null) return; // 二重スポーン防止

        Vector3 spawnPos = trackingTarget != null ? trackingTarget.position : Vector3.zero;

        // Resourcesにプレハブがあればそれを使う、なければランタイムで球を生成
        if (Resources.Load(avatarPrefabName) != null)
        {
            _myAvatar = PhotonNetwork.Instantiate(avatarPrefabName, spawnPos, Quaternion.identity);
            Debug.Log($"[NetworkAvatarSpawner] '{avatarPrefabName}' をPhotonでInstantiateしました。");
        }
        else
        {
            // Fallback: PhotonなしのローカルSphere（Scene ビューのみ見える）
            _myAvatar = CreateLocalSphere(spawnPos);
            Debug.LogWarning("[NetworkAvatarSpawner] Resourcesに 'AvatarSphere' が見つかりません。" +
                             "ローカル球をフォールバック生成しました（Photon同期なし）。");
        }

        // ローカルアバターの色を設定
        Renderer r = _myAvatar.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            r.material.color = localColor;
            r.material.SetFloat("_Mode", 3); // Standard Shader の Transparent
            r.material.renderQueue = 3000;
        }
    }

    private GameObject CreateLocalSphere(Vector3 pos)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"LocalAvatar_{PhotonNetwork.LocalPlayer?.NickName ?? "Unknown"}";
        sphere.transform.position = pos;
        sphere.transform.localScale = Vector3.one * avatarRadius * 2f;

        // コライダー不要
        Collider col = sphere.GetComponent<Collider>();
        if (col != null) Destroy(col);

        return sphere;
    }
}
