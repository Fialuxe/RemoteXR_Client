using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// [System - Networking / SyncRequest-only Sender]
/// Monitors the event hub (AlignmentEventHubSO) for initial data requests
/// and sends SyncRequest to other clients (LocalXR) via Photon network.
///
/// Photon Event Code: 104
/// Payload: none (request only)
///
/// RemoteXR does NOT have alignment sending authority,
/// so PhotonAlignmentSender does not exist here.
/// This class serves the single purpose of sending SyncRequest (Event 104).
///
/// Attach to: [Systems] empty GameObject
/// </summary>
public class PhotonSyncRequestSender : MonoBehaviour
{
    public const byte SyncRequestEventCode = 104;

    [Header("Event Channel (Required)")]
    [Tooltip("Event hub to monitor for initial data requests. Drag AlignmentEventHub.asset here.")]
    [SerializeField] private AlignmentEventHubSO eventHub;

    // --- Lifecycle ---

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[PhotonSyncRequestSender] {gameObject.name}: AlignmentEventHubSO is not assigned! Disabling component.");
            this.enabled = false; return;
        }
    }

    private void OnEnable()
    {
        if (eventHub != null) eventHub.OnInitialDataRequested += HandleInitialDataRequest;
        Debug.Log("[PhotonSyncRequestSender] Started monitoring event hub (initial data request -> SyncRequest send).");
    }

    private void OnDisable()
    {
        if (eventHub != null) eventHub.OnInitialDataRequested -= HandleInitialDataRequest;
        Debug.Log("[PhotonSyncRequestSender] Stopped monitoring event hub.");
    }

    // --- Handler ---

    private void HandleInitialDataRequest()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonSyncRequestSender] Not connected to network. Skipping SyncRequest send.");
            return;
        }

        try
        {
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(SyncRequestEventCode, null, opts, SendOptions.SendReliable);

            Debug.Log("[PhotonSyncRequestSender] SyncRequest (Event 104) sent. Awaiting response from LocalXR.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PhotonSyncRequestSender] Error during Photon send: {e.Message}");
        }
    }
}
