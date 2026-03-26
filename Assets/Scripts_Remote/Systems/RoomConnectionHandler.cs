using Photon.Pun;
using UnityEngine;

/// <summary>
/// [System - State Monitor]
/// Monitors Photon network connection state (room join, etc.)
/// and writes signals to the event hub (AlignmentEventHubSO).
///
/// This class does NOT have Photon sending authority.
/// It only writes "joined room" facts to the event hub as a monitor.
/// Actual Photon sends are handled by Senders (e.g. PhotonSyncRequestSender)
/// that subscribe to the event hub.
///
/// Attach to: [Systems] empty GameObject
/// </summary>
public class RoomConnectionHandler : MonoBehaviourPunCallbacks
{
    [Header("Event Channel (Required)")]
    [Tooltip("Event hub to write join notifications to. Drag AlignmentEventHub.asset here.")]
    [SerializeField] private AlignmentEventHubSO eventHub;

    // --- Lifecycle ---

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[RoomConnectionHandler] {gameObject.name}: AlignmentEventHubSO is not assigned! Disabling component.");
            this.enabled = false; return;
        }
    }

    // --- Photon Callbacks ---

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        Debug.Log("[RoomConnectionHandler] Room join detected. Writing initial data request to event hub.");
        eventHub.RequestInitialData();
    }
}
