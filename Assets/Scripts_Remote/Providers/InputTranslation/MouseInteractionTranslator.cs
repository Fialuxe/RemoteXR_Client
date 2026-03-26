using UnityEngine;

/// <summary>
/// 【翻訳者 / Provider - Mouse Interaction (視点回転)】
/// Remote Expert がマウスで仮想世界の視点を自由に変えるための翻訳者。
/// KeyboardTranslator でカメラ位置を動かし、このスクリプトで向きを変えることで
/// FPS視点の仮想空間ナビゲーションが完成する。
///
/// 操作:
///   右クリック中 のマウス移動 = 視点を上下左右に回転
///   （右クリック中のみ有効にすることで、UIクリック等との干渉を避ける）
///
/// EventHub には一切書き込まない。カメラの Transform を直接変更する。
/// カメラの向きが変われば PhotonGazeSender が次の送信タイミングで自動的に使用する。
///
/// アタッチ場所: [InputProviders] 空のGameObject
/// </summary>
public class MouseInteractionTranslator : MonoBehaviour
{
    [Header("移動対象カメラ")]
    [Tooltip("視点回転させるカメラ。[PlayerCamera]をドラッグ。空欄なら Camera.main を使用。")]
    [SerializeField] private Transform cameraTransform;

    [Header("回転設定")]
    [Tooltip("マウス感度（高いほど素早く回転）")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("TRUEなら右クリック中のみ視点回転。FALSEなら常時回転。")]
    [SerializeField] private bool requireRightClick = true;

    [Tooltip("上下の視点角度制限 [度]（-90〜90の範囲で指定）")]
    [SerializeField] private float pitchLimit = 80f;

    private float _pitch; // 現在の上下角度（累積）

    // ─── ライフサイクル ─────────────────────────────────

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            Debug.Log($"[MouseInteractionTranslator] カメラを自動解決しました: {cameraTransform.name}");
        }
        if (cameraTransform == null)
        {
            Debug.LogError("[MouseInteractionTranslator] カメラが見つかりません！ Inspector で指定してください。");
            this.enabled = false; return;
        }

        // 初期ピッチを現在のカメラ角度から取得
        _pitch = cameraTransform.eulerAngles.x;
        if (_pitch > 180f) _pitch -= 360f; // 0〜360 → -180〜180 に正規化

        Debug.Log($"[MouseInteractionTranslator] 起動しました。" +
                  $"右クリック{(requireRightClick ? "中に" : "不要で")}マウスムーブで視点回転。");
    }

    private void Update()
    {
        if (cameraTransform == null) return;
        if (requireRightClick && !Input.GetMouseButton(1)) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 水平回転（Y軸：World座標）
        cameraTransform.Rotate(Vector3.up, mouseX, Space.World);

        // 垂直回転（X軸：ローカル座標、上下限あり）
        _pitch -= mouseY;
        _pitch  = Mathf.Clamp(_pitch, -pitchLimit, pitchLimit);

        Vector3 euler = cameraTransform.eulerAngles;
        euler.x = _pitch;
        cameraTransform.eulerAngles = euler;
    }
}
