using System;
using UnityEngine;

/// <summary>
/// 【掲示板 / Event Channel - Biometric Data】
/// OSCDataReceiver が受け取った生の生体データを、アプリ内に届ける掲示板。
///
/// - OSCDataReceiver (翻訳者) がここに書き込む
/// - GazeRayCalculator (裏方) が OnGaze2DUpdated を読んで3DRayに変換する
/// - FaceMeshViewer (役者) が OnFaceLandmarkUpdated を読んで表示する
///
/// Gaze や Face のデータは「生の2D座標」で届く。3D変換は別クラスが担う。
/// </summary>
[CreateAssetMenu(fileName = "BiometricEventHub", menuName = "Events/Biometric Event Hub")]
public class BiometricEventHubSO : ScriptableObject
{
    // ─── 視線データ ─────────────────────────────────────

    /// <summary>
    /// 視線の2D画面座標が更新された。
    /// x,y は 0〜1 に正規化された画面座標（左下=0,0、右上=1,1）。
    /// pupilSize はmm単位相当の概算値（OSCから届く生値）。
    /// </summary>
    public event Action<Vector2, float> OnGaze2DUpdated;

    /// <summary>視線データが消えた（タイムアウト or モード切替）。</summary>
    public event Action OnGaze2DLost;

    // ─── 顔ランドマーク ─────────────────────────────────

    /// <summary>
    /// 顔ランドマーク1点が更新された。
    /// index: 0〜67（68点モデル）、pos: 正規化された顔内座標。
    /// </summary>
    public event Action<int, Vector3> OnFaceLandmarkUpdated;

    /// <summary>顔データがタイムアウトした。</summary>
    public event Action OnFaceDataLost;

    // ─── 発火メソッド ─────────────────────────────────

    public void PublishGaze(Vector2 screenPos, float pupilSize)
    {
        OnGaze2DUpdated?.Invoke(screenPos, pupilSize);
    }

    public void LostGaze()
    {
        OnGaze2DLost?.Invoke();
    }

    public void PublishFaceLandmark(int index, Vector3 pos)
    {
        OnFaceLandmarkUpdated?.Invoke(index, pos);
    }

    public void LostFaceData()
    {
        OnFaceDataLost?.Invoke();
    }
}
