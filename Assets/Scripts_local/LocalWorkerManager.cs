using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// Local Worker Manager for desktop/laptop visualization.
/// Receives face/gaze data from remote expert and visualizes them.
/// Can optionally track own position for debugging.
/// </summary>
public class LocalWorkerManager : MonoBehaviourPunCallbacks
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 100f;
    
    [Header("VR Setup")]
    public bool isVRMode = true;
    public Transform vrCamera;
    
    [Header("Network Mode")]
    [Tooltip("TRUE = Track own position. FALSE = Only observe remote expert.")]
    public bool instantiateOwnPlayer = false;
    
    private GameObject localWorkerRepresentation;
    private Vector3 localWorkerPosition;
    private Quaternion localWorkerRotation;

    void Start()
    {
        // Find VR camera if not assigned
        if (isVRMode && vrCamera == null)
        {
            TryResolveVRCamera();
        }
        
        // Initialize position
        if (isVRMode && vrCamera != null)
        {
            localWorkerPosition = vrCamera.position;
            localWorkerRotation = vrCamera.rotation;
        }
        else
        {
            localWorkerPosition = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            localWorkerRotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
        }
        
        // Auto-connect to Photon
        PhotonNetwork.ConnectUsingSettings();
        PhotonNetwork.NickName = "LocalWorker_" + Random.Range(1000, 9999);
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("LocalWorker connected to Master!");
        PhotonNetwork.JoinOrCreateRoom("MeshVRRoom", new RoomOptions { MaxPlayers = 4 }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"LocalWorker joined room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (instantiateOwnPlayer)
        {
            Vector3 spawnPos = localWorkerPosition;
            localWorkerRepresentation = PhotonNetwork.Instantiate("LocalWorkerAvatar", spawnPos, Quaternion.identity);
            localWorkerRepresentation.name = "LocalWorker_" + PhotonNetwork.NickName;

            if (isVRMode && vrCamera != null)
            {
                localWorkerRepresentation.transform.SetPositionAndRotation(vrCamera.position, vrCamera.rotation);
            }

            var rb = localWorkerRepresentation.GetComponent<Rigidbody>();
            var pv = localWorkerRepresentation.GetComponent<PhotonView>();
            if (rb != null && pv != null && pv.IsMine)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            
            Debug.Log("LocalWorker: Instantiated own worker representation");
        }
        else
        {
            Debug.Log("LocalWorker: Observer mode - will visualize remote expert");
        }
    }

    void Update()
    {
        if (!instantiateOwnPlayer)
        {
            if (isVRMode)
            {
                if (vrCamera == null)
                {
                    TryResolveVRCamera();
                }
                if (vrCamera != null)
                {
                    localWorkerPosition = vrCamera.position;
                    localWorkerRotation = vrCamera.rotation;
                }
            }
            else
            {
                HandleMovement();
            }
            return;
        }
        
        if (isVRMode)
        {
            if (vrCamera == null)
            {
                TryResolveVRCamera();
            }
            if (vrCamera != null)
            {
                localWorkerPosition = vrCamera.position;
                localWorkerRotation = vrCamera.rotation;
            }
        }
        
        if (!isVRMode)
        {
            HandleMovement();
        }
        
        if (localWorkerRepresentation != null)
        {
            PhotonView pv = localWorkerRepresentation.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                localWorkerRepresentation.transform.position = localWorkerPosition;
                localWorkerRepresentation.transform.rotation = localWorkerRotation;
            }
        }
    }

    private void TryResolveVRCamera()
    {
        if (Camera.main != null)
        {
            vrCamera = Camera.main.transform;
            if (vrCamera != null) return;
        }

        var centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            vrCamera = centerEye.transform;
            if (vrCamera != null) return;
        }

        var anyCam = Object.FindFirstObjectByType<Camera>();
        if (anyCam != null)
        {
            vrCamera = anyCam.transform;
        }
    }

    void HandleMovement()
    {
        float moveHorizontal = 0f;
        float moveVertical = 0f;
        float rotate = 0f;

        if (Input.GetKey(KeyCode.W))
            moveVertical = 1f;
        if (Input.GetKey(KeyCode.S))
            moveVertical = -1f;
        if (Input.GetKey(KeyCode.A))
            moveHorizontal = -1f;
        if (Input.GetKey(KeyCode.D))
            moveHorizontal = 1f;
        
        if (Input.GetKey(KeyCode.Q))
            rotate = -1f;
        if (Input.GetKey(KeyCode.E))
            rotate = 1f;

        Vector3 forward = localWorkerRotation * Vector3.forward;
        Vector3 right = localWorkerRotation * Vector3.right;
        
        Vector3 movement = (forward * moveVertical + right * moveHorizontal) * moveSpeed * Time.deltaTime;
        localWorkerPosition += movement;
        
        if (rotate != 0)
        {
            localWorkerRotation *= Quaternion.Euler(0, rotate * rotationSpeed * Time.deltaTime, 0);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[LocalWorker] New remote expert joined: {newPlayer.NickName}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[LocalWorker] Remote expert left: {otherPlayer.NickName}");
    }
}
