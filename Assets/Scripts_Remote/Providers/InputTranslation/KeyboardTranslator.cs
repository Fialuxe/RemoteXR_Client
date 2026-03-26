using UnityEngine;

/// <summary>
/// 【翻訳者 / Provider - Keyboard (FPS移動)】
/// Remote Expert がキーボードで仮想世界の中を自由に歩き回るための翻訳者。
/// WASD/QE でカメラを移動させる（FPSゲームのプレイヤー操作に相当）。
///
/// 操作一覧:
///   W/S   = 前進・後退（カメラの向いている方向）
///   A/D   = 左右ストレーフ
///   E/Q   = 上下移動（垂直浮上・降下）
///   Shift = 移動速度の一時加速
///
/// EventHub には一切書き込まない。カメラの Transform を直接変更する。
/// カメラの向きが変われば PhotonGazeSender が次の送信タイミングで自動的に使用する。
///
/// アタッチ場所: [InputProviders] 空のGameObject
/// </summary>
public class KeyboardTranslator : MonoBehaviour
{
    [Header("移動対象カメラ")]
    [Tooltip("FPS移動させるカメラ。[PlayerCamera]をドラッグ。空欄なら Camera.main を使用。")]
    [SerializeField] private Transform cameraTransform;

    [Header("移動設定")]
    [Tooltip("通常移動速度 [m/s]")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Shift押下時の加速倍率")]
    [SerializeField] private float sprintMultiplier = 2.5f;

    [Tooltip("上下移動速度 [m/s]")]
    [SerializeField] private float verticalSpeed = 2f;

    // ─── ライフサイクル ─────────────────────────────────

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            Debug.Log($"[KeyboardTranslator] カメラを自動解決しました: {cameraTransform.name}");
        }
        if (cameraTransform == null)
        {
            Debug.LogError("[KeyboardTranslator] カメラが見つかりません！ Inspector で指定してください。");
            this.enabled = false; return;
        }
        Debug.Log("[KeyboardTranslator] 起動しました。WASD=移動 E/Q=上下 Shift=加速");
    }

    private void Update()
    {
        if (cameraTransform == null) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // 水平移動（カメラの向きに沿って）
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right   = cameraTransform.right;

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += forward;
        if (Input.GetKey(KeyCode.S)) move -= forward;
        if (Input.GetKey(KeyCode.D)) move += right;
        if (Input.GetKey(KeyCode.A)) move -= right;

        // 垂直移動（世界座標のY軸）
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        if (move.sqrMagnitude > 0)
        {
            cameraTransform.position += move.normalized * speed * Time.deltaTime;
        }
    }
}
