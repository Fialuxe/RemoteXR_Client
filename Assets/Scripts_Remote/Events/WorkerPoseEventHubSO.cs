using System;
using UnityEngine;

/// <summary>
/// [Event Channel - Worker Pose]
/// Internal communication hub for Local Worker body pose data.
///
/// -- Design Policy --
/// - Tracking points are identified by string IDs (not enums).
///   Phase 1: "head" only
///   Phase 2: "head", "hand_l", "hand_r"
///   Phase 3: "head", "hand_l", ..., "hand_l_index_tip", etc.
///   Adding more points requires NO changes to this class, Sender, or Receiver.
///   Only the Provider (data collection) and Actor (visualization) need updating.
///
/// - Does NOT contain any Photon/network communication (internal app events only).
/// - Holds no state; only relays events (Single Responsibility Principle).
/// </summary>
[CreateAssetMenu(fileName = "WorkerPoseEventHub", menuName = "Events/Worker Pose Event Hub")]
public class WorkerPoseEventHubSO : ScriptableObject
{
    // --- Data Structures ---

    /// <summary>
    /// Data for a single tracking point.
    /// PointId is a free-form string, extensible without limit.
    /// </summary>
    [Serializable]
    public struct TrackingPointData
    {
        public string PointId;        // "head", "hand_l", "hand_r", "hand_l_thumb_tip", etc.
        public Vector3 Position;
        public Quaternion Rotation;
    }

    // --- Events ---

    /// <summary>
    /// Fired when tracking data is updated.
    /// Array length may vary per frame (only active points are included).
    /// </summary>
    public event Action<TrackingPointData[]> OnPoseUpdated;

    /// <summary>
    /// Fired when tracking is lost (Local Worker left the room or became inactive).
    /// </summary>
    public event Action OnPoseLost;

    // --- Invocation Methods ---

    public void UpdatePose(TrackingPointData[] points)
    {
        OnPoseUpdated?.Invoke(points);
    }

    public void LostPose()
    {
        Debug.Log("[WorkerPoseEventHub] LostPose fired");
        OnPoseLost?.Invoke();
    }
}
