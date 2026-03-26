using System;
using UnityEngine;

/// <summary>
/// 【掲示板 / Event Channel】
/// Remote Expert の視線・カーソルデータのアプリ内通信ハブ。
///
/// - PhotonGazeReceiver（姉）がPhotonから受け取り、ここに書き込む。
/// - FrustumViewController / RayGazeViewController / SurfaceCircleViewController が読む。
/// - Local Worker 自身のカメラ情報を発信する際も同じハブを使う。
/// </summary>
[CreateAssetMenu(fileName = "GazeEventHub", menuName = "Events/Gaze Event Hub")]
public class GazeEventHubSO : ScriptableObject
{
    // ─── イベント ─────────────────────────────────────

    /// <summary>
    /// 視線データが更新された時に発火する。
    /// </summary>
    /// <param name="origin">視線の発生源（カメラ位置）</param>
    /// <param name="direction">視線の向き（正規化済み）</param>
    /// <param name="hitPoint">サーフェスへのヒット点（hasHit=falseの場合は無効）</param>
    /// <param name="hitNormal">ヒット面の法線（hasHit=falseの場合は無効）</param>
    /// <param name="hasHit">視線がサーフェスにヒットしたか</param>
    public event Action<Vector3, Vector3, Vector3, Vector3, bool> OnGazeUpdated;

    /// <summary>視線データが消えた（送信側が非アクティブ）時に発火する。</summary>
    public event Action OnGazeLost;

    /// <summary>
    /// 視線の表示モード変更要求があった時に発火する。
    /// （int 0=Frustum, 1=Ray, 2=Circle, 3=None etc... enumと連動）
    /// </summary>
    public event Action<int> OnModeChangeRequested;

    // ─── 発火メソッド ─────────────────────────────────

    public void UpdateGaze(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal, bool hasHit)
    {
        Debug.Log($"[GazeEventHub] UpdateGaze 発火: origin={origin}, hasHit={hasHit}" +
                  (hasHit ? $", hitPoint={hitPoint}" : ""));
        OnGazeUpdated?.Invoke(origin, direction, hitPoint, hitNormal, hasHit);
    }

    public void LostGaze()
    {
        Debug.Log("[GazeEventHub] LostGaze 発火");
        OnGazeLost?.Invoke();
    }

    /// <summary>
    /// 表示モードの変更を掲示板に要求する。
    /// </summary>
    public void RequestModeChange(int mode)
    {
        Debug.Log($"[GazeEventHub] RequestModeChange 発火: mode={mode}");
        OnModeChangeRequested?.Invoke(mode);
    }
}
