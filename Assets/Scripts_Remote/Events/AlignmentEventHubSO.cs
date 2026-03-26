using System;
using UnityEngine;

/// <summary>
/// [Event Channel - Alignment]
/// The sole internal communication hub for the alignment system.
/// By routing all communication through this ScriptableObject,
/// senders (translators) and receivers (actors) remain completely
/// decoupled from each other.
///
/// - Does NOT contain any Photon/network communication (internal app events only).
/// - Holds no state; only relays events (Single Responsibility Principle).
/// </summary>
[CreateAssetMenu(fileName = "AlignmentEventHub", menuName = "Events/Alignment Event Hub")]
public class AlignmentEventHubSO : ScriptableObject
{
    // --- Internal Communication Events ---

    /// <summary>Request to move the mesh by the specified vector.</summary>
    public event Action<Vector3> OnMoveRequested;

    /// <summary>Request to rotate the mesh around a specified axis by a given angle.</summary>
    public event Action<Vector3, float> OnRotateRequested;

    /// <summary>Request to uniformly scale the mesh along the Y-axis (height).</summary>
    public event Action<float> OnScaleRequested;

    /// <summary>Request to save the current alignment.</summary>
    public event Action OnSaveRequested;

    /// <summary>Request to reset (delete) the saved alignment.</summary>
    public event Action OnResetRequested;

    /// <summary>
    /// Fired when the target mesh is spawned at runtime.
    /// Emitted by ScannedRoomSpawner; consumed by Systems (CalibrationPersistenceSystem, etc.)
    /// to dynamically set the targetMesh reference.
    /// </summary>
    public event Action<Transform> OnTargetMeshSpawned;

    /// <summary>
    /// Fired when initial alignment data is needed (e.g., on room join).
    /// Written by RoomConnectionHandler; subscribed to by PhotonSyncRequestSender.
    /// </summary>
    public event Action OnInitialDataRequested;

    /// <summary>
    /// Fired when another client requests the current mesh position to be sent.
    /// Written by PhotonAlignmentReceiver; subscribed to by PhotonAlignmentSender.
    /// </summary>
    public event Action OnSyncBroadcastRequested;

    // --- Invocation Methods ---

    /// <param name="delta">Per-frame movement delta in world coordinates.</param>
    public void RequestMove(Vector3 delta)
    {
        Debug.Log($"[AlignmentEventHub] RequestMove fired: delta={delta}");
        OnMoveRequested?.Invoke(delta);
    }

    /// <param name="axis">Rotation axis (world space).</param>
    /// <param name="angle">Rotation angle (degrees).</param>
    public void RequestRotate(Vector3 axis, float angle)
    {
        Debug.Log($"[AlignmentEventHub] RequestRotate fired: axis={axis}, angle={angle}");
        OnRotateRequested?.Invoke(axis, angle);
    }

    /// <param name="delta">Scale delta (positive = enlarge, negative = shrink).</param>
    public void RequestScale(float delta)
    {
        Debug.Log($"[AlignmentEventHub] RequestScale fired: delta={delta}");
        OnScaleRequested?.Invoke(delta);
    }

    public void RequestSave()
    {
        Debug.Log("[AlignmentEventHub] RequestSave fired");
        OnSaveRequested?.Invoke();
    }

    public void RequestReset()
    {
        Debug.Log("[AlignmentEventHub] RequestReset fired");
        OnResetRequested?.Invoke();
    }

    /// <summary>
    /// Called immediately after ScannedRoomSpawner instantiates a mesh.
    /// CalibrationPersistenceSystem etc. receive this to dynamically assign targetMesh.
    /// </summary>
    public void NotifyMeshSpawned(Transform spawnedMesh)
    {
        Debug.Log($"[AlignmentEventHub] NotifyMeshSpawned fired: mesh={spawnedMesh.name}");
        OnTargetMeshSpawned?.Invoke(spawnedMesh);
    }

    /// <summary>
    /// Writes a signal to the event hub requesting initial data upon room join.
    /// </summary>
    public void RequestInitialData()
    {
        Debug.Log("[AlignmentEventHub] RequestInitialData fired");
        OnInitialDataRequested?.Invoke();
    }

    /// <summary>
    /// Signal to broadcast current position in response to another client's request.
    /// </summary>
    public void RequestSyncBroadcast()
    {
        Debug.Log("[AlignmentEventHub] RequestSyncBroadcast fired");
        OnSyncBroadcastRequested?.Invoke();
    }
}
