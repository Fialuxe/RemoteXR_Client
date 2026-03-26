using UnityEngine;

/// <summary>
/// 【役者 / Actor - Ray Gaze Visualization】
/// Remote Expert の視線方向を半透明ラインで表示する。
/// GazeEventHubSO を購読し、視線データが届くたびにラインを更新する。
///
/// アタッチ場所: Hierarchy内の [GazeVisualization] 空のGameObject
/// </summary>
public class RayGazeViewController : MonoBehaviour
{
    [Header("Event Channel (必須)")]
    [SerializeField] private GazeEventHubSO eventHub;

    [Header("Ray 設定")]
    [Tooltip("ヒットしない場合のデフォルト長さ [m]")]
    [SerializeField] private float defaultRayLength = 5f;

    [Tooltip("ラインの太さ [m]")]
    [SerializeField] private float lineWidth = 0.005f;

    [Header("見た目")]
    [Tooltip("半透明の水色（Alphaで透明度調整）")]
    [SerializeField] private Color rayColor = new Color(0f, 0.8f, 1f, 0.6f);

    [Tooltip("外部Materialを指定する場合（未指定なら自動生成）")]
    [SerializeField] private Material overrideMaterial;

    // ─── 内部 ─────────────────────────────────────────
    private LineRenderer _lineRenderer;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[RayGazeViewController] {gameObject.name}: GazeEventHubSO がアサインされていません！");
            this.enabled = false; return;
        }
        BuildLineRenderer();
    }

    private void OnEnable()
    {
        if (eventHub == null) return;
        eventHub.OnGazeUpdated += HandleGazeUpdated;
        eventHub.OnGazeLost    += HandleGazeLost;
    }

    private void OnDisable()
    {
        if (eventHub == null) return;
        eventHub.OnGazeUpdated -= HandleGazeUpdated;
        eventHub.OnGazeLost    -= HandleGazeLost;
        if (_lineRenderer != null) _lineRenderer.enabled = false;
    }

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleGazeUpdated(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal, bool hasHit)
    {
        Vector3 endPoint = hasHit ? hitPoint : origin + direction.normalized * defaultRayLength;
        _lineRenderer.SetPosition(0, origin);
        _lineRenderer.SetPosition(1, endPoint);
        _lineRenderer.enabled = true;
        Debug.Log($"[RayGazeViewController] Ray更新: {origin} → {endPoint} (hasHit={hasHit})");
    }

    private void HandleGazeLost()
    {
        if (_lineRenderer != null) _lineRenderer.enabled = false;
        Debug.Log("[RayGazeViewController] 視線消失: Ray を非表示にしました。");
    }

    // ─── LineRenderer 構築 ─────────────────────────────

    private void BuildLineRenderer()
    {
        GameObject lineObj = new GameObject("GazeRayLine");
        lineObj.transform.parent = transform;

        _lineRenderer = lineObj.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth * 0.3f; // 先端を細くして視線らしくする
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.material = BuildLineMaterial();
        _lineRenderer.startColor = rayColor;
        _lineRenderer.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0f); // 先端フェード
        _lineRenderer.enabled = false;

        Debug.Log("[RayGazeViewController] LineRenderer を構築しました。");
    }

    private Material BuildLineMaterial()
    {
        if (overrideMaterial != null) return overrideMaterial;
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = rayColor;
        return mat;
    }
}
