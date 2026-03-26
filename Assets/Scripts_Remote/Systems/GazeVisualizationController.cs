using UnityEngine;

/// <summary>
/// 【裏方 / System - Gaze Visualization Controller】
/// Frustum / Ray / SurfaceCircle の表示モードを一元管理する。
/// Remote Expert がどの視線表現をLocal Workerに提示するかをキーボードまたは
/// Inspector から切り替える。
///
/// 操作方法（デフォルト）:
///   V         = 次のモードに切り替え（Frustum → Ray → Circle → All → None → Frustum）
///   Shift+V   = 前のモードに切り替え
///
/// アタッチ場所: [GazeVisualization] 空のGameObject（3つのViewControllerの親）
/// </summary>
public class GazeVisualizationController : MonoBehaviour
{
    public enum GazeVisualizationMode
    {
        Frustum,
        Ray,
        SurfaceCircle,
        All,
        None
    }

    [Header("Event Channel (必須)")]
    [Tooltip("GazeEventHub.assetをここへドラッグ")]
    [SerializeField] private GazeEventHubSO gazeHub;

    [Header("初期モード")]
    [SerializeField] private GazeVisualizationMode currentMode = GazeVisualizationMode.Frustum;

    [Header("各ViewControllerの親GameObject")]
    [Tooltip("FrustumViewController がアタッチされた GameObject")]
    [SerializeField] private GameObject frustumObject;

    [Tooltip("RayGazeViewController がアタッチされた GameObject")]
    [SerializeField] private GameObject rayObject;

    [Tooltip("SurfaceCircleViewController がアタッチされた GameObject")]
    [SerializeField] private GameObject circleObject;

    [Header("キーバインド")]
    [Tooltip("モード切り替えキー")]
    [SerializeField] private KeyCode cycleKey = KeyCode.V;

    // ─── ライフサイクル ─────────────────────────────────

    private void Start()
    {
        ApplyMode(currentMode);
        Debug.Log($"[GazeVisualizationController] 起動。初期モード: {currentMode}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(cycleKey))
        {
            bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            CycleMode(reverse);
        }
    }

    // ─── モード切り替え ─────────────────────────────────

    private static readonly GazeVisualizationMode[] CycleModes = {
        GazeVisualizationMode.Frustum,
        GazeVisualizationMode.Ray,
        GazeVisualizationMode.SurfaceCircle,
        GazeVisualizationMode.None
    };

    private void CycleMode(bool reverse)
    {
        int currentIndex = System.Array.IndexOf(CycleModes, currentMode);
        if (currentIndex == -1) currentIndex = 0; // Allモード等にいた場合のフォールバック

        int count = CycleModes.Length;
        int nextIndex = (currentIndex + (reverse ? count - 1 : 1)) % count;
        SetMode(CycleModes[nextIndex]);
    }

    /// <summary>外部から直接モードを設定する（UIボタン等から呼び出す用）</summary>
    public void SetMode(GazeVisualizationMode mode)
    {
        currentMode = mode;
        ApplyMode(mode);

        if (gazeHub != null)
        {
            gazeHub.RequestModeChange((int)mode);
        }

        Debug.Log($"[GazeVisualizationController] モード変更: {mode}");
    }

    // Inspector のドロップダウンボタン向けに文字列版も用意
    public void SetModeFrustum()     => SetMode(GazeVisualizationMode.Frustum);
    public void SetModeRay()         => SetMode(GazeVisualizationMode.Ray);
    public void SetModeCircle()      => SetMode(GazeVisualizationMode.SurfaceCircle);
    public void SetModeAll()         => SetMode(GazeVisualizationMode.All);
    public void SetModeNone()        => SetMode(GazeVisualizationMode.None);

    // ─── 適用 ─────────────────────────────────────────

    private void ApplyMode(GazeVisualizationMode mode)
    {
        SetActive(frustumObject, mode == GazeVisualizationMode.Frustum || mode == GazeVisualizationMode.All);
        SetActive(rayObject,     mode == GazeVisualizationMode.Ray     || mode == GazeVisualizationMode.All);
        SetActive(circleObject,  mode == GazeVisualizationMode.SurfaceCircle || mode == GazeVisualizationMode.All);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    // ─── Inspector 表示確認 ──────────────────────────────

    private void OnGUI()
    {
        // 右上に現在のモードを常時表示
        GUI.color = new Color(0f, 0.8f, 1f, 0.8f);
        GUI.Label(new Rect(Screen.width - 220, 10, 210, 30),
                  $"[Gaze Mode] {currentMode}  (V で切替)");
        GUI.color = Color.white;
    }
}
