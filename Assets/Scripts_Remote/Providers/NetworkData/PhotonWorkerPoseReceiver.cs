using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// [Provider - NetworkData / Receiver]
/// Receives Worker pose data from the Photon network
/// and writes it to the event hub (WorkerPoseEventHubSO).
///
/// -- Payload Parsing --
/// Performs symmetric deserialization with PhotonWorkerPoseSender.
/// Point count and PointIds are variable, so no changes needed for Phase 2+.
///
/// Attach to: [BackgroundSystem] existing GameObject
/// </summary>
public class PhotonWorkerPoseReceiver : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte WorkerPoseEventCode = 105;
    private const int ELEMENTS_PER_POINT = 8;

    [Header("Event Channel (Required)")]
    [Tooltip("Event hub to write received data to. Drag WorkerPoseEventHub.asset here.")]
    [SerializeField] private WorkerPoseEventHubSO eventHub;

    // --- Lifecycle ---

    private void Awake()
    {
        if (eventHub == null)
        {
            Debug.LogError($"[PhotonWorkerPoseReceiver] {gameObject.name}: WorkerPoseEventHubSO is not assigned!");
            this.enabled = false; return;
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
        Debug.Log("[PhotonWorkerPoseReceiver] Started monitoring for Worker pose data.");
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // --- Photon Event Reception ---

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != WorkerPoseEventCode) return;

        try
        {
            object[] data = (object[])photonEvent.CustomData;
            if (data == null || data.Length < 1) return;

            int count = System.Convert.ToInt32(data[0]);
            if (data.Length < 1 + count * ELEMENTS_PER_POINT) return;

            var points = new WorkerPoseEventHubSO.TrackingPointData[count];

            for (int i = 0; i < count; i++)
            {
                int offset = 1 + i * ELEMENTS_PER_POINT;
                points[i] = new WorkerPoseEventHubSO.TrackingPointData
                {
                    PointId = (string)data[offset + 0],
                    Position = new Vector3(
                        System.Convert.ToSingle(data[offset + 1]),
                        System.Convert.ToSingle(data[offset + 2]),
                        System.Convert.ToSingle(data[offset + 3])
                    ),
                    Rotation = new Quaternion(
                        System.Convert.ToSingle(data[offset + 4]),
                        System.Convert.ToSingle(data[offset + 5]),
                        System.Convert.ToSingle(data[offset + 6]),
                        System.Convert.ToSingle(data[offset + 7])
                    ),
                };
            }

            // Write to event hub
            eventHub.UpdatePose(points);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PhotonWorkerPoseReceiver] Data parsing error: {e.Message}");
        }
    }
}
