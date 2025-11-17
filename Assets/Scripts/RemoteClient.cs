using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class RemoteClient : MonoBehaviourPunCallbacks
{
    [Header("Client Type")]
    [Tooltip("If true, instantiates player and transmits data (Remote/AR device). If false, only observes (Local/Desktop).")]
    public bool isTransmitter = true;
    
    [Header("Fly Camera Settings")]
    public float moveSpeed = 5f;
    public float fastMultiplier = 3f;
    public float slowMultiplier = 0.3f;
    public float lookSensitivity = 2f;
    public bool requireRightMouseToLook = true;
    public bool lockCursorWhenLooking = true;
    
    private GameObject remotePlayerRepresentation;
    private Vector3 remotePosition;
    private Quaternion remoteRotation;
    private Camera activeCam;
    private float yaw;
    private float pitch;

    void Start()
    {
        // ★ CRITICAL: Ensure AlignmentNetworkHub exists
        AlignmentNetworkHub hub = FindFirstObjectByType<AlignmentNetworkHub>();
        if (hub == null)
        {
            Debug.LogWarning("[RemoteClient] AlignmentNetworkHub not found in scene! Creating one...");
            GameObject hubObj = new GameObject("AlignmentNetworkHub");
            hub = hubObj.AddComponent<AlignmentNetworkHub>();
            PhotonView pv = hubObj.AddComponent<PhotonView>();
            Debug.Log("[RemoteClient] ✓ AlignmentNetworkHub created with PhotonView");
        }
        
        // Resolve camera and seed pose
        activeCam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (activeCam != null)
        {
            remotePosition = activeCam.transform.position;
            remoteRotation = activeCam.transform.rotation;
            var e = activeCam.transform.rotation.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }
        else
        {
            remotePosition = Vector3.zero;
            remoteRotation = Quaternion.identity;
        }
        
        // Auto-connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[RemoteClient] Starting Photon connection...");
            Debug.Log($"[RemoteClient] App ID configured: {PhotonNetwork.PhotonServerSettings?.AppSettings?.AppIdRealtime != null}");
            
            PhotonNetwork.ConnectUsingSettings();
            PhotonNetwork.NickName = "RemoteUser_" + Random.Range(1000, 9999);
            Debug.Log($"[RemoteClient] Connecting with nickname: {PhotonNetwork.NickName}");
        }
        else
        {
            Debug.Log("[RemoteClient] Already connected to Photon!");
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("RemoteClient connected to Master!");
        // Join the same room as LocalClient
        PhotonNetwork.JoinOrCreateRoom("MeshVRRoom", new RoomOptions { MaxPlayers = 4 }, TypedLobby.Default);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"[RemoteClient] Disconnected! Cause: {cause}");
        
        // Retry on timeout errors (includes NameServer timeout, AppOutOfFocus, etc)
        if (cause == DisconnectCause.ServerTimeout ||
            cause == DisconnectCause.ClientTimeout ||
            cause == DisconnectCause.ExceptionOnConnect ||
            cause == DisconnectCause.DnsExceptionOnConnect ||
            cause == DisconnectCause.Exception)
        {
            Debug.LogWarning($"[RemoteClient] Connection error ({cause}). Retrying in 3 seconds...");
            CancelInvoke(nameof(RetryConnection));  // Cancel any pending retry
            Invoke(nameof(RetryConnection), 3f);
        }
    }

    void RetryConnection()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[RemoteClient] Retrying Photon connection...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.Log("[RemoteClient] Already connected, retry cancelled");
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("RemoteClient joined room: " + PhotonNetwork.CurrentRoom.Name);
        Debug.Log("Players in room: " + PhotonNetwork.CurrentRoom.PlayerCount);

        // Only instantiate player if this is a transmitter (remote client)
        if (isTransmitter)
        {
            // Instantiate player representation
            // Spawn at a different location to avoid overlap
            Vector3 spawnPos = new Vector3(Random.Range(-2f, 2f), 1.5f, Random.Range(-2f, 2f));
            remotePlayerRepresentation = PhotonNetwork.Instantiate("LocalClientCube", spawnPos, Quaternion.identity);
            remotePlayerRepresentation.name = "RemotePlayer_" + PhotonNetwork.NickName;
            
            remotePosition = spawnPos;
            
            // Set up face and gaze transmission
            SetupFaceGazeTransmission();
            
            Debug.Log("Transmitter mode: Player instantiated and LSL setup initiated");
        }
        else
        {
            Debug.Log("Receiver mode: NOT instantiating player - will observe remote players only");
        }
    }
    
    private void SetupFaceGazeTransmission()
    {
        if (remotePlayerRepresentation == null)
            return;
        
        PhotonView photonView = remotePlayerRepresentation.GetComponent<PhotonView>();
        if (photonView == null)
        {
            Debug.LogError("RemoteClient: PhotonView not found on player representation!");
            return;
        }
        
        // Add LSL receivers for face and gaze data
        LslFaceMeshReceiver faceMeshReceiver = remotePlayerRepresentation.GetComponent<LslFaceMeshReceiver>();
        if (faceMeshReceiver == null)
        {
            faceMeshReceiver = remotePlayerRepresentation.AddComponent<LslFaceMeshReceiver>();
            Debug.Log("Added LslFaceMeshReceiver to remote player");
        }
        
        LslGazeReceiver gazeReceiver = remotePlayerRepresentation.GetComponent<LslGazeReceiver>();
        if (gazeReceiver == null)
        {
            gazeReceiver = remotePlayerRepresentation.AddComponent<LslGazeReceiver>();
            Debug.Log("Added LslGazeReceiver to remote player");
        }
        
        // Add Photon transmitter
        PhotonFaceGazeTransmitter transmitter = remotePlayerRepresentation.GetComponent<PhotonFaceGazeTransmitter>();
        if (transmitter == null)
        {
            transmitter = remotePlayerRepresentation.AddComponent<PhotonFaceGazeTransmitter>();
            transmitter.faceMeshReceiver = faceMeshReceiver;
            transmitter.gazeReceiver = gazeReceiver;
            transmitter.transmissionInterval = 2; // Send every 2 frames for bandwidth efficiency
            transmitter.transmitFaceMesh = true;
            transmitter.transmitGaze = true;
            Debug.Log("Added PhotonFaceGazeTransmitter to remote player");
        }
        
        // CRITICAL: Register transmitter in PhotonView.ObservedComponents
        if (!photonView.ObservedComponents.Contains(transmitter))
        {
            photonView.ObservedComponents.Add(transmitter);
            Debug.Log("✓ Registered PhotonFaceGazeTransmitter in PhotonView.ObservedComponents");
        }
    }

    private float lastConnectionCheckTime = 0f;
    private const float CONNECTION_CHECK_INTERVAL = 5f;

    void Update()
    {
        // Monitor connection status periodically
        if (Time.time - lastConnectionCheckTime > CONNECTION_CHECK_INTERVAL)
        {
            lastConnectionCheckTime = Time.time;
            
            if (!PhotonNetwork.IsConnected)
            {
                Debug.LogWarning("[RemoteClient] Connection lost! Status: " + PhotonNetwork.NetworkClientState);
            }
        }

        if (activeCam == null)
        {
            activeCam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        // Handle free-fly movement + mouse look
        HandleFlyCamera();
        
        // Update the networked player representation (only if we own it)
        if (remotePlayerRepresentation != null)
        {
            PhotonView pv = remotePlayerRepresentation.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                remotePlayerRepresentation.transform.position = remotePosition;
                remotePlayerRepresentation.transform.rotation = remoteRotation;
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
            remoteRotation = Quaternion.Euler(pitch, yaw, 0f);

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
        Vector3 worldMove = (remoteRotation * input.normalized) * (speed * Time.deltaTime);
        remotePosition += worldMove;

        // Apply to camera if available
        if (activeCam != null)
        {
            activeCam.transform.SetPositionAndRotation(remotePosition, remoteRotation);
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
