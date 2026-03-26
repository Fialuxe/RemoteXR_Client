using UnityEngine;

/// <summary>
/// 【役者 / Actor - Surface Circle Visualization】
/// Remote Expert の視線がサーフェスに当たった点に、
/// 半透明の円形マーカーを表示する。
///
/// アルゴリズム由来: 旧 NetworkedDataReceiver の SurfaceCircle 部分を分離。
/// ヒットしない場合は非表示になる（旧実装の挙動を維持）。
///
/// アタッチ場所: Hierarchy内の [GazeVisualization] 空のGameObject
/// </summary>
public class SurfaceCircleViewController : MonoBehaviour
{
    [Header("Event Channel (必須)")]
    [SerializeField] private GazeEventHubSO eventHub;

    [Header("Circle 設定")]
    [Tooltip("サーフェス上の円の半径 [m]")]
    [SerializeField] private float circleRadius = 0.08f;

    [Tooltip("サーフェスからの浮き量（Z-fighting防止）[m]")]
    [SerializeField] private float surfaceOffset = 0.001f;

    [Tooltip("円のスムーズ追従速度（0=瞬間, 大=遅延）")]
    [SerializeField] private float followSmoothSpeed = 20f;

    [Header("見た目")]
    [Tooltip("半透明の水色（Alphaで透明度調整）")]
    [SerializeField] private Color circleColor = new Color(0f, 0.8f, 1f, 0.4f);

    [Tooltip("外部Materialを指定する場合（未指定なら自動生成）")]
    [SerializeField] private Material overrideMaterial;

    // ─── 内部 ─────────────────────────────────────────
    private GameObject _circleObj;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _isVisible;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[SurfaceCircleViewController] {gameObject.name}: GazeEventHubSO がアサインされていません！");
            this.enabled = false; return;
        }
        BuildCircle();
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
        SetVisible(false);
    }

    private void Update()
    {
        // スムーズ追従（カクつきを防ぐ）
        if (!_isVisible || _circleObj == null) return;
        _circleObj.transform.position = Vector3.Lerp(
            _circleObj.transform.position, _targetPosition, followSmoothSpeed * Time.deltaTime);
        _circleObj.transform.rotation = Quaternion.Slerp(
            _circleObj.transform.rotation, _targetRotation, followSmoothSpeed * Time.deltaTime);
    }

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleGazeUpdated(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal, bool hasHit)
    {
        if (!hasHit)
        {
            SetVisible(false);
            return;
        }

        // 目標位置を設定（Updateでスムーズに追従）
        _targetPosition = hitPoint + hitNormal * surfaceOffset;
        // 旧実装同様、法線方向を向かせた後90°回転してサーフェスに平行に
        _targetRotation = Quaternion.LookRotation(hitNormal) * Quaternion.Euler(90f, 0f, 0f);
        SetVisible(true);
    }

    private void HandleGazeLost()
    {
        SetVisible(false);
        Debug.Log("[SurfaceCircleViewController] 視線消失: Circleを非表示にしました。");
    }

    // ─── Circle 構築 ──────────────────────────────────

    private void BuildCircle()
    {
        _circleObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _circleObj.name = "GazeSurfaceCircle";
        _circleObj.transform.parent = transform;
        // 超薄い円盤として使用（高さは最小限）
        _circleObj.transform.localScale = new Vector3(circleRadius * 2f, 0.0005f, circleRadius * 2f);

        // コライダーは不要（視覚表現のみ）
        Collider col = _circleObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer r = _circleObj.GetComponent<Renderer>();
        r.material = BuildMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        SetVisible(false);
        Debug.Log($"[SurfaceCircleViewController] SurfaceCircle を構築しました。radius={circleRadius}m");
    }

    private Material BuildMaterial()
    {
        if (overrideMaterial != null) return overrideMaterial;
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = circleColor;
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_circleObj != null) _circleObj.SetActive(visible);
    }
}
