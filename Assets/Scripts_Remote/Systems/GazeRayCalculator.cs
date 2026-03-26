using UnityEngine;

/// <summary>
/// 【裏方 / System - Gaze Ray Calculator】
/// BiometricEventHubSO から届いた「2D画面座標上のGaze」を
/// 3D世界座標のRayに変換し、GazeEventHubSO に書き込む。
///
/// 変換アルゴリズム:
///   1. 2D画面座標（0〜1正規化）を Screen座標に変換
///   2. Camera.ScreenPointToRay() で3DRayを生成
///   3. Raycast でサーフェスとの交点・法線を求める
///   4. GazeEventHubSO.UpdateGaze(origin, direction, hitPoint, hitNormal, hasHit) を発火
///
/// 責務の分離:
///   - OSCDataReceiver  = OSCデータをBiometricHubに書く（生データ）
///   - GazeRayCalculator = BiometricHubを読んでGazeHubに変換（座標変換）
///   - PhotonGazeSender  = GazeHubを読んでPhotonに送る（ネットワーク）
///
/// アタッチ場所: [Systems] 空のGameObject
/// </summary>
public class GazeRayCalculator : MonoBehaviour
{
    [Header("Event Channels (必須)")]
    [Tooltip("BiometricEventHub.assetをここへドラッグ（2D生データの入力）")]
    [SerializeField] private BiometricEventHubSO biometricHub;

    [Tooltip("GazeEventHub.assetをここへドラッグ（3D変換後データの出力）")]
    [SerializeField] private GazeEventHubSO gazeHub;

    [Header("PlayerCamera (視線の発射源)")]
    [Tooltip("Remote ExpertのPlayerCamera。空欄なら Camera.main を使用。")]
    [SerializeField] private Camera gazeCamera;

    [Header("Raycast 設定")]
    [Tooltip("Raycastを判定するレイヤーマスク（0=全レイヤー）")]
    [SerializeField] private LayerMask hitLayerMask;

    [Tooltip("Raycastの最大距離 [m]")]
    [SerializeField] private float maxRayDistance = 20f;

    // ─── ライフサイクル ─────────────────────────────────

    private void Awake()
    {
        bool ok = true;
        if (biometricHub == null)
        {
            Debug.LogError("[GazeRayCalculator] BiometricEventHubSO がアサインされていません！");
            ok = false;
        }
        if (gazeHub == null)
        {
            Debug.LogError("[GazeRayCalculator] GazeEventHubSO がアサインされていません！");
            ok = false;
        }
        if (!ok) { this.enabled = false; return; }
    }

    private void Start()
    {
        if (gazeCamera == null) gazeCamera = Camera.main;
        if (gazeCamera == null)
        {
            Debug.LogError("[GazeRayCalculator] PlayerCameraが見つかりません！ Inspector で指定してください。");
            this.enabled = false; return;
        }
        Debug.Log($"[GazeRayCalculator] 起動しました。Camera={gazeCamera.name}");
    }

    private void OnEnable()
    {
        if (biometricHub == null) return;
        biometricHub.OnGaze2DUpdated += HandleGaze2D;
        biometricHub.OnGaze2DLost    += HandleGazeLost;
    }

    private void OnDisable()
    {
        if (biometricHub == null) return;
        biometricHub.OnGaze2DUpdated -= HandleGaze2D;
        biometricHub.OnGaze2DLost    -= HandleGazeLost;
    }

    // ─── ハンドラ ─────────────────────────────────────

    private void HandleGaze2D(Vector2 normalizedScreenPos, float pupilSize)
    {
        if (gazeCamera == null) return;

        // 正規化座標 → スクリーンピクセル座標へ変換
        // OSCのY軸は上が1.0（画面上部）のため、Unityの下原点と逆。反転する。
        Vector3 screenPos = new Vector3(
            normalizedScreenPos.x * Screen.width,
            (1f - normalizedScreenPos.y) * Screen.height,
            0f
        );

        // スクリーン座標 → 3DRay
        Ray ray = gazeCamera.ScreenPointToRay(screenPos);
        Vector3 origin    = ray.origin;
        Vector3 direction = ray.direction;

        // Raycast でサーフェスとの交点を求める
        int mask = hitLayerMask == 0 ? ~0 : (int)hitLayerMask;
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, mask);

        Vector3 hitPoint  = hasHit ? hit.point  : Vector3.zero;
        Vector3 hitNormal = hasHit ? hit.normal  : Vector3.up;

        gazeHub.UpdateGaze(origin, direction, hitPoint, hitNormal, hasHit);

        Debug.Log($"[GazeRayCalculator] 2D({normalizedScreenPos.x:F2},{normalizedScreenPos.y:F2})" +
                  $" → 3D dir={direction}, hasHit={hasHit}");
    }

    private void HandleGazeLost()
    {
        gazeHub.LostGaze();
        Debug.Log("[GazeRayCalculator] Gaze消失。GazeHubにLostGazeを発火しました。");
    }
}
