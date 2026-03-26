using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// [Actor - Worker Avatar / Visualization]
/// Subscribes to the event hub (WorkerPoseEventHubSO) and places
/// spheres (or custom prefabs) at each received tracking point position.
///
/// -- Extension Policy --
/// - Uses a Dictionary keyed by PointId (string) to manage visual objects.
/// - Unknown PointIds are automatically handled by creating new spheres.
///   Phase 2: When "hand_l", "hand_r" are sent, spheres are auto-generated.
///   Phase 3: Finger joints work the same way.
/// - To use a custom Prefab for a specific PointId, configure pointPrefabOverrides.
///
/// Attach to: [WorkerRepresentation] create a new GameObject
/// </summary>
public class WorkerAvatarController : MonoBehaviour
{
    [Header("Event Channel (Required)")]
    [Tooltip("Event hub to subscribe to for pose data. Drag WorkerPoseEventHub.asset here.")]
    [SerializeField] private WorkerPoseEventHubSO eventHub;

    [Header("Default Display Settings")]
    [Tooltip("Default avatar point color")]
    [SerializeField] private Color defaultColor = new Color(0f, 1f, 0.5f, 0.7f);

    [Tooltip("Default point radius")]
    [SerializeField] private float defaultRadius = 0.08f;

    [Header("Custom Prefab (Optional)")]
    [Tooltip("Configure custom prefabs for specific PointIds")]
    [SerializeField] private PointPrefabOverride[] pointPrefabOverrides;

    [System.Serializable]
    public struct PointPrefabOverride
    {
        public string pointId;
        public GameObject prefab;
    }

    // PointId -> visual GameObject mapping
    private readonly Dictionary<string, GameObject> _pointObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _prefabMap = new Dictionary<string, GameObject>();

    // --- Lifecycle ---

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[WorkerAvatarController] {gameObject.name}: WorkerPoseEventHubSO is not assigned!");
            this.enabled = false; return;
        }

        // Build prefab override dictionary
        if (pointPrefabOverrides != null)
        {
            foreach (var ovr in pointPrefabOverrides)
            {
                if (!string.IsNullOrEmpty(ovr.pointId) && ovr.prefab != null)
                    _prefabMap[ovr.pointId] = ovr.prefab;
            }
        }
    }

    private void OnEnable()
    {
        if (eventHub != null)
        {
            eventHub.OnPoseUpdated += HandlePoseUpdated;
            eventHub.OnPoseLost += HandlePoseLost;
        }
        Debug.Log("[WorkerAvatarController] Started subscribing to event hub.");
    }

    private void OnDisable()
    {
        if (eventHub != null)
        {
            eventHub.OnPoseUpdated -= HandlePoseUpdated;
            eventHub.OnPoseLost -= HandlePoseLost;
        }
    }

    // --- Handlers ---

    private void HandlePoseUpdated(WorkerPoseEventHubSO.TrackingPointData[] points)
    {
        foreach (var point in points)
        {
            if (!_pointObjects.TryGetValue(point.PointId, out GameObject go))
            {
                // New PointId received -> auto-generate a sphere
                go = CreatePointObject(point.PointId);
                _pointObjects[point.PointId] = go;
                Debug.Log($"[WorkerAvatarController] Created new point '{point.PointId}'.");
            }

            go.transform.position = point.Position;
            go.transform.rotation = point.Rotation;
        }
    }

    private void HandlePoseLost()
    {
        // Worker left -> hide all points
        foreach (var kvp in _pointObjects)
        {
            if (kvp.Value != null) kvp.Value.SetActive(false);
        }
        Debug.Log("[WorkerAvatarController] Worker pose lost. All points hidden.");
    }

    // --- Point Creation ---

    private GameObject CreatePointObject(string pointId)
    {
        GameObject go;

        if (_prefabMap.TryGetValue(pointId, out GameObject prefab))
        {
            // Custom prefab available
            go = Instantiate(prefab, transform);
            go.name = $"WorkerPoint_{pointId}";
        }
        else
        {
            // Default: generate a Sphere
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"WorkerPoint_{pointId}";
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * defaultRadius * 2f;

            // Collider not needed
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Set color
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = defaultColor;
            }
        }

        return go;
    }
}
