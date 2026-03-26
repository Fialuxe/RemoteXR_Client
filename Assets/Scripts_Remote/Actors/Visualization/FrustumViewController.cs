using UnityEngine;

/// <summary>
/// 【役者 / Actor - Frustum Visualization】
/// Remote Expert のカメラFOVを錐台（ピラミッド）メッシュとして表示する。
/// GazeEventHubSO を購読し、视线データが届くたびに錐台の向きを更新する。
///
/// アルゴリズム由来: 旧 NetworkedDataReceiver.CreateGazeFrustum() を完全に分離・拡張。
/// アタッチ場所: Hierarchy内の [GazeVisualization] 空のGameObject
/// </summary>
public class FrustumViewController : MonoBehaviour
{
    [Header("Event Channel (必須)")]
    [SerializeField] private GazeEventHubSO eventHub;

    [Header("Frustum 形状")]
    [Tooltip("錐台の長さ（奥行き）[m]")]
    [SerializeField] private float frustumLength = 2f;

    [Tooltip("水平FOV [度]")]
    [SerializeField] private float horizontalFOV = 60f;

    [Tooltip("垂直FOV [度]")]
    [SerializeField] private float verticalFOV = 45f;

    [Tooltip("錐台の起点からの近平面距離 [m]")]
    [SerializeField] private float nearDistance = 0.05f;

    [Header("見た目")]
    [Tooltip("半透明の水色（Alphaで透明度調整）")]
    [SerializeField] private Color frustumColor = new Color(0f, 0.8f, 1f, 0.2f);

    [Tooltip("外縁のワイヤーフレーム色")]
    [SerializeField] private Color edgeColor = new Color(0f, 0.8f, 1f, 0.7f);

    [Tooltip("外部Materialを指定する場合（未指定なら自動生成）")]
    [SerializeField] private Material overrideMaterial;

    // ─── 内部 ─────────────────────────────────────────
    private GameObject _frustumFace;
    private GameObject _frustumEdge;
    private MeshFilter _faceMeshFilter;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[FrustumViewController] {gameObject.name}: GazeEventHubSO がアサインされていません！");
            this.enabled = false; return;
        }
        BuildFrustumObjects();
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

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleGazeUpdated(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal, bool hasHit)
    {
        SetVisible(true);
        _frustumFace.transform.position = origin;
        _frustumFace.transform.rotation = Quaternion.LookRotation(direction);
        _frustumEdge.transform.position = origin;
        _frustumEdge.transform.rotation = Quaternion.LookRotation(direction);
        // FOVが変わっていればメッシュを再生成
        RebuildMeshIfNeeded();
    }

    private void HandleGazeLost()
    {
        SetVisible(false);
    }

    // ─── メッシュ構築 ─────────────────────────────────

    private float _lastHFOV, _lastVFOV, _lastLength, _lastNear;

    private void BuildFrustumObjects()
    {
        // 面（半透明塗り）
        _frustumFace = new GameObject("FrustumFace");
        _frustumFace.transform.parent = transform;
        _faceMeshFilter = _frustumFace.AddComponent<MeshFilter>();
        MeshRenderer faceRenderer = _frustumFace.AddComponent<MeshRenderer>();
        faceRenderer.material = BuildTransparentMaterial(frustumColor, overrideMaterial);
        faceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        faceRenderer.receiveShadows = false;

        // 縁（ワイヤーフレーム風ラインレンダラー）
        _frustumEdge = new GameObject("FrustumEdge");
        _frustumEdge.transform.parent = transform;
        BuildEdgeLines(_frustumEdge);

        SetVisible(false);
        RebuildMesh();
    }

    private void RebuildMeshIfNeeded()
    {
        if (Mathf.Approximately(_lastHFOV, horizontalFOV) &&
            Mathf.Approximately(_lastVFOV, verticalFOV) &&
            Mathf.Approximately(_lastLength, frustumLength) &&
            Mathf.Approximately(_lastNear, nearDistance)) return;
        RebuildMesh();
    }

    private void RebuildMesh()
    {
        _lastHFOV = horizontalFOV; _lastVFOV = verticalFOV;
        _lastLength = frustumLength; _lastNear = nearDistance;

        float halfH_far  = Mathf.Tan(horizontalFOV * 0.5f * Mathf.Deg2Rad) * frustumLength;
        float halfV_far  = Mathf.Tan(verticalFOV   * 0.5f * Mathf.Deg2Rad) * frustumLength;
        float halfH_near = Mathf.Tan(horizontalFOV * 0.5f * Mathf.Deg2Rad) * nearDistance;
        float halfV_near = Mathf.Tan(verticalFOV   * 0.5f * Mathf.Deg2Rad) * nearDistance;

        // 8頂点(近平面4 + 遠平面4)の錐台
        Vector3[] verts = {
            new Vector3(-halfH_near, -halfV_near, nearDistance),  // 0 near BL
            new Vector3( halfH_near, -halfV_near, nearDistance),  // 1 near BR
            new Vector3( halfH_near,  halfV_near, nearDistance),  // 2 near TR
            new Vector3(-halfH_near,  halfV_near, nearDistance),  // 3 near TL
            new Vector3(-halfH_far,  -halfV_far,  frustumLength), // 4 far BL
            new Vector3( halfH_far,  -halfV_far,  frustumLength), // 5 far BR
            new Vector3( halfH_far,   halfV_far,  frustumLength), // 6 far TR
            new Vector3(-halfH_far,   halfV_far,  frustumLength), // 7 far TL
        };

        // 6面 × 2三角形
        int[] tris = {
            0,3,1, 1,3,2,   // 近面
            4,5,7, 5,6,7,   // 遠面
            0,1,4, 1,5,4,   // 下面
            2,3,6, 3,7,6,   // 上面
            0,4,3, 3,4,7,   // 左面
            1,2,5, 2,6,5    // 右面
        };

        Mesh m = new Mesh { name = "FrustumMesh" };
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        _faceMeshFilter.mesh = m;

        UpdateEdgeLines(verts);
        Debug.Log($"[FrustumViewController] メッシュ再生成: HFOV={horizontalFOV}° VFOV={verticalFOV}° Length={frustumLength}m");
    }

    private void BuildEdgeLines(GameObject parent)
    {
        // 12本のエッジをLineRendererで描く
        for (int i = 0; i < 12; i++)
        {
            GameObject edgeLine = new GameObject($"Edge_{i}");
            edgeLine.transform.parent = parent.transform;
            LineRenderer lr = edgeLine.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = 0.003f;
            lr.endWidth = 0.003f;
            lr.material = BuildTransparentMaterial(edgeColor, null);
            lr.useWorldSpace = false;
        }
    }

    private void UpdateEdgeLines(Vector3[] v)
    {
        // v[0..3]=near, v[4..7]=far の12エッジ
        (int a, int b)[] edges = {
            (0,1),(1,2),(2,3),(3,0),     // 近面
            (4,5),(5,6),(6,7),(7,4),     // 遠面
            (0,4),(1,5),(2,6),(3,7)      // 側面
        };
        LineRenderer[] lrs = _frustumEdge.GetComponentsInChildren<LineRenderer>();
        for (int i = 0; i < Mathf.Min(lrs.Length, edges.Length); i++)
        {
            lrs[i].SetPosition(0, v[edges[i].a]);
            lrs[i].SetPosition(1, v[edges[i].b]);
        }
    }

    private void SetVisible(bool visible)
    {
        if (_frustumFace != null) _frustumFace.SetActive(visible);
        if (_frustumEdge != null) _frustumEdge.SetActive(visible);
    }

    // ─── マテリアル生成 ───────────────────────────────

    private static Material BuildTransparentMaterial(Color color, Material overrideMat)
    {
        if (overrideMat != null) return overrideMat;
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
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
}
