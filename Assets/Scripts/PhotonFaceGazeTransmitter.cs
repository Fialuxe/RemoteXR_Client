using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Transmits face mesh and eye gaze data from remote client to local client using Photon.
/// On REMOTE CLIENT: Attach LSL receivers to read and transmit data.
/// On LOCAL CLIENT: Leave LSL receivers empty - this script will only receive data.
/// The script automatically detects its role based on PhotonView.IsMine.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PhotonFaceGazeTransmitter : MonoBehaviourPun, IPunObservable
{
    [Header("LSL Component References (Remote Client Only)")]
    [Tooltip("Reference to the LslFaceMeshReceiver component - ONLY needed on remote client")]
    public LslFaceMeshReceiver faceMeshReceiver;
    
    [Tooltip("Reference to the LslGazeReceiver component - ONLY needed on remote client")]
    public LslGazeReceiver gazeReceiver;

    [Header("Transmission Settings")]
    [Tooltip("Send data every N frames to reduce network traffic (1 = every frame)")]
    [Range(1, 10)]
    public int transmissionInterval = 1;
    
    [Tooltip("Enable face mesh data transmission")]
    public bool transmitFaceMesh = true;
    
    [Tooltip("Enable eye gaze data transmission")]
    public bool transmitGaze = true;

    [Header("Compression Settings")]
    [Tooltip("Number of key facial landmarks to send (instead of all 68)")]
    [Range(10, 68)]
    public int keyLandmarksCount = 20;
    
    [Tooltip("Compress landmark data by reducing precision (fewer decimal places)")]
    public bool compressLandmarks = true;
    
    [Header("Debug")]
    public bool showDebugLogs = false;

    // Network state for received data (for remote clients)
    private Vector3[] receivedFaceLandmarks;
    private Vector2 receivedGazePosition;
    private float receivedPupilSize;
    private bool hasReceivedFaceData = false;
    private bool hasReceivedGazeData = false;

    // Transmission counter
    private int frameCounter = 0;

    // Key landmark indices to transmit (optimized subset of 68-point model)
    private readonly int[] keyLandmarkIndices = new int[]
    {
        // Face outline & chin
        0, 8, 16,  // Left jaw, chin, right jaw
        
        // Eyebrows
        17, 21, 22, 26,  // Outer right brow, inner right brow, inner left brow, outer left brow
        
        // Nose
        27, 30, 33,  // Nose bridge top, nose tip, nose bottom
        
        // Eyes
        36, 39, 42, 45,  // Right eye outer, right eye inner, left eye inner, left eye outer
        37, 40, 43, 46,  // Right eye top, right eye bottom, left eye top, left eye bottom
        
        // Mouth
        48, 54, 51, 57, 62, 66  // Mouth right, mouth left, upper lip top, lower lip bottom, upper lip inner, lower lip inner
    };

    private void Awake()
    {
        // Initialize received data arrays (always needed)
        receivedFaceLandmarks = new Vector3[keyLandmarksCount];
        receivedGazePosition = Vector2.zero;
        receivedPupilSize = 0f;
    }

    private void Start()
    {
        // Validate PhotonView
        if (photonView == null)
        {
            Debug.LogError("PhotonFaceGazeTransmitter requires a PhotonView component!");
            enabled = false;
            return;
        }

        // Auto-find LSL receivers if this is the owner (remote client)
        if (photonView.IsMine)
        {
            if (faceMeshReceiver == null)
                faceMeshReceiver = GetComponent<LslFaceMeshReceiver>();
            
            if (gazeReceiver == null)
                gazeReceiver = GetComponent<LslGazeReceiver>();
            
            if (showDebugLogs)
            {
                Debug.Log($"PhotonFaceGazeTransmitter (REMOTE/TRANSMITTER mode). Face: {faceMeshReceiver != null}, Gaze: {gazeReceiver != null}");
            }
        }
        else
        {
            // This is a remote player instance on the local client - we only receive data
            if (showDebugLogs)
            {
                Debug.Log($"PhotonFaceGazeTransmitter (LOCAL/RECEIVER mode) for player: {photonView.Owner?.NickName}");
            }
        }
    }

    private void Update()
    {
        // Only transmit if this is our PhotonView
        if (!photonView.IsMine)
            return;

        frameCounter++;
        
        // Throttle transmission based on interval
        if (frameCounter % transmissionInterval != 0)
            return;
    }

    /// <summary>
    /// Called by Photon to serialize/deserialize data over the network
    /// </summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We are sending data (this is the local player)
            SendFaceGazeData(stream);
        }
        else
        {
            // We are receiving data (this is a remote player)
            ReceiveFaceGazeData(stream);
        }
    }

    /// <summary>
    /// Sends face mesh and gaze data to the network
    /// </summary>
    private void SendFaceGazeData(PhotonStream stream)
    {
        // Send face mesh data
        if (transmitFaceMesh && faceMeshReceiver != null && faceMeshReceiver.IsConnected)
        {
            stream.SendNext(true); // Face data available flag
            
            // Send key landmarks
            for (int i = 0; i < keyLandmarksCount && i < keyLandmarkIndices.Length; i++)
            {
                int landmarkIndex = keyLandmarkIndices[i];
                Vector3 landmark = faceMeshReceiver.GetLandmark(landmarkIndex);
                
                if (compressLandmarks)
                {
                    // Compress by reducing precision
                    stream.SendNext(Mathf.Round(landmark.x * 1000f) / 1000f);
                    stream.SendNext(Mathf.Round(landmark.y * 1000f) / 1000f);
                    stream.SendNext(Mathf.Round(landmark.z * 1000f) / 1000f);
                }
                else
                {
                    stream.SendNext(landmark.x);
                    stream.SendNext(landmark.y);
                    stream.SendNext(landmark.z);
                }
            }

            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Sending face mesh data: {keyLandmarksCount} landmarks");
            }
        }
        else
        {
            stream.SendNext(false); // No face data available
        }

        // Send gaze data
        if (transmitGaze && gazeReceiver != null && gazeReceiver.IsConnected)
        {
            stream.SendNext(true); // Gaze data available flag
            
            // Get actual gaze data from receiver
            Vector2 gazePos = gazeReceiver.GetGazePosition();
            float pupilSize = gazeReceiver.GetPupilSize();
            
            stream.SendNext(gazePos.x);
            stream.SendNext(gazePos.y);
            stream.SendNext(pupilSize);

            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Sending gaze data: pos={gazePos}, pupil={pupilSize}");
            }
        }
        else
        {
            stream.SendNext(false); // No gaze data available
        }
    }

    /// <summary>
    /// Receives face mesh and gaze data from the network
    /// </summary>
    private void ReceiveFaceGazeData(PhotonStream stream)
    {
        // Receive face mesh data
        hasReceivedFaceData = (bool)stream.ReceiveNext();
        
        if (hasReceivedFaceData)
        {
            for (int i = 0; i < keyLandmarksCount && i < keyLandmarkIndices.Length; i++)
            {
                float x = (float)stream.ReceiveNext();
                float y = (float)stream.ReceiveNext();
                float z = (float)stream.ReceiveNext();
                receivedFaceLandmarks[i] = new Vector3(x, y, z);
            }

            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Received face mesh data: {keyLandmarksCount} landmarks from {photonView.Owner.NickName}");
            }
        }

        // Receive gaze data
        hasReceivedGazeData = (bool)stream.ReceiveNext();
        
        if (hasReceivedGazeData)
        {
            receivedGazePosition.x = (float)stream.ReceiveNext();
            receivedGazePosition.y = (float)stream.ReceiveNext();
            receivedPupilSize = (float)stream.ReceiveNext();

            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Received gaze data: {receivedGazePosition} from {photonView.Owner.NickName}");
            }
        }
    }

    // Public accessors for received data (for other scripts to use)
    
    /// <summary>
    /// Check if this PhotonView has received face mesh data
    /// </summary>
    public bool HasFaceData => hasReceivedFaceData;

    /// <summary>
    /// Check if this PhotonView has received gaze data
    /// </summary>
    public bool HasGazeData => hasReceivedGazeData;

    /// <summary>
    /// Get a specific landmark from the received face mesh data
    /// </summary>
    public Vector3 GetReceivedLandmark(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < receivedFaceLandmarks.Length)
            return receivedFaceLandmarks[keyIndex];
        return Vector3.zero;
    }

    /// <summary>
    /// Get the full array of received landmarks
    /// </summary>
    public Vector3[] GetReceivedLandmarks()
    {
        return receivedFaceLandmarks;
    }

    /// <summary>
    /// Get the received gaze position (normalized screen coordinates)
    /// </summary>
    public Vector2 GetReceivedGazePosition()
    {
        return receivedGazePosition;
    }

    /// <summary>
    /// Get the received pupil size
    /// </summary>
    public float GetReceivedPupilSize()
    {
        return receivedPupilSize;
    }

    /// <summary>
    /// Get the mapping of key landmark indices to the full 68-point model
    /// </summary>
    public int GetOriginalLandmarkIndex(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < keyLandmarkIndices.Length)
            return keyLandmarkIndices[keyIndex];
        return -1;
    }

    private void OnGUI()
    {
        if (!showDebugLogs)
            return;

        // Display status in the corner of the screen
        GUILayout.BeginArea(new Rect(10, 150, 400, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"<b>Photon Face/Gaze Transmitter</b>");
        GUILayout.Label($"Is Mine: {photonView.IsMine}");
        GUILayout.Label($"Owner: {photonView.Owner?.NickName ?? "None"}");
        
        if (photonView.IsMine)
        {
            GUILayout.Label($"<b>Sending:</b>");
            GUILayout.Label($"  Face: {(faceMeshReceiver != null && faceMeshReceiver.IsConnected ? "✓" : "✗")}");
            GUILayout.Label($"  Gaze: {(gazeReceiver != null && gazeReceiver.IsConnected ? "✓" : "✗")}");
        }
        else
        {
            GUILayout.Label($"<b>Receiving:</b>");
            GUILayout.Label($"  Face: {(hasReceivedFaceData ? "✓" : "✗")}");
            GUILayout.Label($"  Gaze: {(hasReceivedGazeData ? "✓" : "✗")}");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
