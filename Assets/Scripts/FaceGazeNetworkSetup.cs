using UnityEngine;
using Photon.Pun;

/// <summary>
/// Helper script to set up face mesh and gaze transmission for a networked player.
/// Attach this to your player prefab (e.g., LocalClientCube) to automatically configure
/// all required components for face and gaze data transmission/reception.
/// </summary>
public class FaceGazeNetworkSetup : MonoBehaviour
{
    [Header("Setup Configuration")]
    [Tooltip("Automatically add required components on Start")]
    public bool autoSetup = true;
    
    [Tooltip("Is this the remote client (transmitter) or local client (receiver)?")]
    public bool isRemoteClient = true;

    [Header("LSL Stream Names (Remote Client Only)")]
    public string faceMeshStreamName = "FaceMesh";
    public string gazeStreamName = "EyeGaze";

    [Header("Transmission Settings (Remote Client Only)")]
    [Range(1, 10)]
    public int transmissionInterval = 1;
    public bool transmitFaceMesh = true;
    public bool transmitGaze = true;

    [Header("Visualization Settings (Local Client Only)")]
    public GameObject landmarkPrefab;
    public GameObject gazeIndicatorPrefab;

    private void Start()
    {
        if (autoSetup)
        {
            SetupComponents();
        }
    }

    [ContextMenu("Setup Components")]
    public void SetupComponents()
    {
        PhotonView photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            Debug.LogError("FaceGazeNetworkSetup: PhotonView component is required!");
            return;
        }

        if (photonView.IsMine && isRemoteClient)
        {
            SetupRemoteClient();
        }
        else if (!photonView.IsMine)
        {
            SetupLocalClient();
        }
    }

    private void SetupRemoteClient()
    {
        Debug.Log("Setting up Remote Client (Transmitter)...");

        PhotonView photonView = GetComponent<PhotonView>();

        // Add LSL receivers if not present
        LslFaceMeshReceiver faceMeshReceiver = GetComponent<LslFaceMeshReceiver>();
        if (faceMeshReceiver == null)
        {
            faceMeshReceiver = gameObject.AddComponent<LslFaceMeshReceiver>();
            faceMeshReceiver.streamName = faceMeshStreamName;
            Debug.Log("Added LslFaceMeshReceiver");
        }

        LslGazeReceiver gazeReceiver = GetComponent<LslGazeReceiver>();
        if (gazeReceiver == null)
        {
            gazeReceiver = gameObject.AddComponent<LslGazeReceiver>();
            gazeReceiver.streamName = gazeStreamName;
            Debug.Log("Added LslGazeReceiver");
        }

        // Add transmitter if not present
        PhotonFaceGazeTransmitter transmitter = GetComponent<PhotonFaceGazeTransmitter>();
        if (transmitter == null)
        {
            transmitter = gameObject.AddComponent<PhotonFaceGazeTransmitter>();
            transmitter.faceMeshReceiver = faceMeshReceiver;
            transmitter.gazeReceiver = gazeReceiver;
            transmitter.transmissionInterval = transmissionInterval;
            transmitter.transmitFaceMesh = transmitFaceMesh;
            transmitter.transmitGaze = transmitGaze;
            Debug.Log("Added PhotonFaceGazeTransmitter");
        }

        // CRITICAL: Register transmitter in PhotonView.ObservedComponents
        if (photonView != null && !photonView.ObservedComponents.Contains(transmitter))
        {
            photonView.ObservedComponents.Add(transmitter);
            Debug.Log("✓ Registered PhotonFaceGazeTransmitter in PhotonView.ObservedComponents");
        }

        Debug.Log("Remote Client setup complete!");
    }

    private void SetupLocalClient()
    {
        Debug.Log("Setting up Local Client (Receiver)...");

        PhotonView photonView = GetComponent<PhotonView>();

        // Add transmitter if not present (for receiving data)
        PhotonFaceGazeTransmitter transmitter = GetComponent<PhotonFaceGazeTransmitter>();
        if (transmitter == null)
        {
            transmitter = gameObject.AddComponent<PhotonFaceGazeTransmitter>();
            Debug.Log("Added PhotonFaceGazeTransmitter for receiving");
        }

        // CRITICAL: Register transmitter in PhotonView.ObservedComponents
        if (photonView != null && !photonView.ObservedComponents.Contains(transmitter))
        {
            photonView.ObservedComponents.Add(transmitter);
            Debug.Log("✓ Registered PhotonFaceGazeTransmitter in PhotonView.ObservedComponents");
        }

        // Add receiver/visualizer if not present
        PhotonFaceGazeReceiver receiver = GetComponent<PhotonFaceGazeReceiver>();
        if (receiver == null)
        {
            receiver = gameObject.AddComponent<PhotonFaceGazeReceiver>();
            receiver.transmitter = transmitter;
            
            if (landmarkPrefab != null)
                receiver.landmarkPrefab = landmarkPrefab;
            
            if (gazeIndicatorPrefab != null)
                receiver.gazeIndicator = gazeIndicatorPrefab;
            
            Debug.Log("Added PhotonFaceGazeReceiver for visualization");
        }

        Debug.Log("Local Client setup complete!");
    }

    private void OnGUI()
    {
        // Display setup status
        GUILayout.BeginArea(new Rect(10, Screen.height - 120, 300, 110));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("<b>Face/Gaze Network Setup</b>");
        
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null)
        {
            GUILayout.Label($"Role: {(pv.IsMine ? "Transmitter" : "Receiver")}");
            GUILayout.Label($"Owner: {pv.Owner?.NickName ?? "None"}");
            
            if (pv.IsMine)
            {
                var faceMesh = GetComponent<LslFaceMeshReceiver>();
                var gaze = GetComponent<LslGazeReceiver>();
                GUILayout.Label($"Face LSL: {(faceMesh?.IsConnected ?? false ? "✓" : "✗")}");
                GUILayout.Label($"Gaze LSL: {(gaze?.IsConnected ?? false ? "✓" : "✗")}");
            }
        }
        else
        {
            GUILayout.Label("PhotonView not found!");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
