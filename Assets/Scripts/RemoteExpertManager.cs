using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Remote Expert Manager for VR/AR devices.
/// Receives face and gaze data via OSC and transmits over Photon network.
/// Also handles mesh alignment and camera control.
/// </summary>
public class RemoteExpertManager : MonoBehaviourPunCallbacks
{
    [Header("OSC Data Source")]
    [Tooltip("OSC receiver for face/gaze data")]
    public OscDataReceiver oscReceiver;
    
    [Header("Fly Camera Settings")]
    public float moveSpeed = 5f;
    public float fastMultiplier = 3f;
    public float slowMultiplier = 0.3f;
    public float lookSensitivity = 2f;
    public bool requireRightMouseToLook = true;
    public bool lockCursorWhenLooking = true;
    
    [Header("Data Transmission")]
    public int transmissionInterval = 2; // Send every N frames
    
    private GameObject remoteExpertRepresentation;
    private Vector3 remoteExpertPosition;
    private Quaternion remoteExpertRotation;
    private Camera activeCam;
    private float yaw;
    private float pitch;
    private int frameCounter = 0;

    void Start()
    {
        // Setup OSC receiver if not assigned
        if (oscReceiver == null)
        {
            oscReceiver = gameObject.AddComponent<OscDataReceiver>();
            oscReceiver.receivePort = 8000;
        }
        
        // Resolve camera and seed pose
        activeCam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (activeCam != null)
        {
            remoteExpertPosition = activeCam.transform.position;
            remoteExpertRotation = activeCam.transform.rotation;
            var e = activeCam.transform.rotation.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }
        else
        {
            remoteExpertPosition = Vector3.zero;
            remoteExpertRotation = Quaternion.identity;
        }
        
        // Auto-connect to Photon
        PhotonNetwork.ConnectUsingSettings();
        PhotonNetwork.NickName = "RemoteExpert_" + Random.Range(1000, 9999);
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("RemoteExpertManager connected to Master!");
        PhotonNetwork.JoinOrCreateRoom("MeshVRRoom", new RoomOptions { MaxPlayers = 4 }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"RemoteExpert joined room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // Instantiate remote expert representation
        Vector3 spawnPos = new Vector3(Random.Range(-2f, 2f), 1.5f, Random.Range(-2f, 2f));
        remoteExpertRepresentation = PhotonNetwork.Instantiate("RemoteExpertAvatar", spawnPos, Quaternion.identity);
        remoteExpertRepresentation.name = "RemoteExpert_" + PhotonNetwork.NickName;
        remoteExpertPosition = spawnPos;
        
        Debug.Log("RemoteExpert instantiated with OSC data streaming");
    }

    void Update()
    {
        if (activeCam == null)
        {
            activeCam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        // Handle free-fly movement + mouse look
        HandleFlyCamera();
        
        // Update the networked remote expert representation (only if we own it)
        if (remoteExpertRepresentation != null)
        {
            PhotonView pv = remoteExpertRepresentation.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                remoteExpertRepresentation.transform.position = remoteExpertPosition;
                remoteExpertRepresentation.transform.rotation = remoteExpertRotation;
                
                // Transmit OSC data over network
                frameCounter++;
                if (frameCounter % transmissionInterval == 0)
                {
                    TransmitFaceGazeData(pv);
                }
            }
        }
    }
    
    void TransmitFaceGazeData(PhotonView pv)
    {
        if (oscReceiver == null)
            return;
            
        // Send gaze data
        if (oscReceiver.HasGazeData)
        {
            Vector2 gaze = oscReceiver.GetGazePosition();
            float pupil = oscReceiver.GetPupilSize();
            pv.RPC("ReceiveGazeData", RpcTarget.AllBuffered, gaze.x, gaze.y, pupil);
        }
        
        // Send face landmark data (compressed - only key landmarks)
        if (oscReceiver.HasFaceData)
        {
            // Send a few key landmarks
            int[] keyLandmarks = { 0, 8, 16, 27, 30, 33, 36, 42, 48, 54 }; // Jaw, nose, eyes, mouth
            foreach (int idx in keyLandmarks)
            {
                Vector3 landmark = oscReceiver.GetFaceLandmark(idx);
                pv.RPC("ReceiveFaceLandmark", RpcTarget.AllBuffered, idx, landmark.x, landmark.y, landmark.z);
            }
        }
    }

    void HandleFlyCamera()
    {
        // Mouse look
        bool looking = !requireRightMouseToLook || Input.GetMouseButton(1);
        if (looking)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            yaw += mx * lookSensitivity;
            pitch -= my * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            remoteExpertRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (lockCursorWhenLooking)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else if (lockCursorWhenLooking)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Speed modifiers
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) speed *= slowMultiplier;

        // WASD move, QE vertical
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) input += Vector3.back;
        if (Input.GetKey(KeyCode.A)) input += Vector3.left;
        if (Input.GetKey(KeyCode.D)) input += Vector3.right;
        if (Input.GetKey(KeyCode.E)) input += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) input += Vector3.down;

        // Move relative to current rotation
        Vector3 worldMove = (remoteExpertRotation * input.normalized) * (speed * Time.deltaTime);
        remoteExpertPosition += worldMove;

        // Apply to camera if available
        if (activeCam != null)
        {
            activeCam.transform.SetPositionAndRotation(remoteExpertPosition, remoteExpertRotation);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log("New player joined: " + newPlayer.NickName);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log("Player left: " + otherPlayer.NickName);
    }
}
