using System;
using UnityEngine;

/// <summary>
/// 【Application / Factory】
/// スキャンされた部屋のメッシュをランタイムでスポーンし、シーンに配置する。
/// スポーン完了後、AlignmentEventHubSO に通知することで、
/// CalibrationPersistenceSystem や PhotonAlignmentSender が動的に対象メッシュを設定できる。
///
/// アタッチ場所: Hierarchy内の [AppManager] または [Factories] という空のGameObject
/// </summary>
public class ScannedRoomSpawner : MonoBehaviour
{
    [Header("Event Channel (必須)")]
    [Tooltip("スポーン完了を通知する掲示板。AlignmentEventHub.assetをここへドラッグ。")]
    [SerializeField] private AlignmentEventHubSO eventHub;

    [Header("Base Template")]
    [SerializeField] private GameObject roomPrefab;

    [Header("Custom Data")]
    [SerializeField] private Mesh scannedMesh;

    [Tooltip("スポーン後に動的にアタッチするスクリプト名（例: 'AlignableTarget'）。空欄可。")]
    [SerializeField] private string scriptNameToAttach;

    private void Start()
    {
        SpawnAndSetupRoom();
    }

    [ContextMenu("Spawn And Setup Room Now")]
    private void SpawnAndSetupRoom()
    {
        if (roomPrefab == null)
        {
            Debug.LogError("[ScannedRoomSpawner] Room Prefab がアサインされていません！スポーンを中断します。");
            return;
        }

        GameObject roomInstance = InstantiatePrefab();
        ApplyCustomMesh(roomInstance);
        AttachDynamicScript(roomInstance);

        // スポーン完了を掲示板に通知 → CalibrationPersistenceSystem などが受け取り targetMesh を設定する
        if (eventHub != null)
        {
            eventHub.NotifyMeshSpawned(roomInstance.transform);
            Debug.Log($"[ScannedRoomSpawner] {roomInstance.name} のスポーンを掲示板に通知しました。");
        }
        else
        {
            Debug.LogWarning("[ScannedRoomSpawner] EventHub が未アサインのため、Systemsへの通知をスキップしました。" +
                             " CalibrationPersistenceSystem等のtargetMeshは手動でセットしてください。");
        }
    }

    private GameObject InstantiatePrefab()
    {
        return Instantiate(roomPrefab, Vector3.zero, roomPrefab.transform.rotation);
    }

    private void ApplyCustomMesh(GameObject instance)
    {
        if (scannedMesh == null) return;

        MeshFilter targetFilter = instance.GetComponentInChildren<MeshFilter>();
        if (targetFilter != null)
        {
            targetFilter.mesh = scannedMesh;
            Debug.Log($"[ScannedRoomSpawner] カスタムメッシュを適用しました: {scannedMesh.name}");
        }
        else
        {
            Debug.LogWarning("[ScannedRoomSpawner] Prefab に MeshFilter がありません。メッシュ差替をスキップしました。");
        }
    }

    private void AttachDynamicScript(GameObject instance)
    {
        if (string.IsNullOrEmpty(scriptNameToAttach)) return;

        Type scriptType = Type.GetType(scriptNameToAttach);
        if (scriptType == null)
        {
            Debug.LogError($"[ScannedRoomSpawner] スクリプト '{scriptNameToAttach}' が見つかりません。" +
                           " 正確なクラス名（大文字小文字含む）を確認してください。");
            return;
        }

        instance.AddComponent(scriptType);
        Debug.Log($"[ScannedRoomSpawner] {scriptNameToAttach} を動的にアタッチしました。");
    }
}