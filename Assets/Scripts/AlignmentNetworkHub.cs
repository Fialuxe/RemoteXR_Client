using UnityEngine;
using Photon.Pun;
using System;

/// <summary>
/// Centralized network event hub for all alignment-related RPC calls
/// This is the ONLY script that needs a PhotonView for alignment networking
/// Other scripts can use static methods or subscribe to events
/// Assymetric design so that this script can be attached to either LocalClient or RemoteClient
/// </summary>
public class AlignmentNetworkHub : MonoBehaviourPunCallbacks
{
    private static AlignmentNetworkHub instance;
    
    // Events for alignment data
    public static event Action<int, Vector3, Quaternion> OnSpatialAlignmentReceived;
    public static event Action<Vector3, Quaternion, Vector3> OnMeshAlignmentReceived;
    public static event Action<bool> OnMeshAlignmentModeChanged;
    
    // Events for face and gaze data
    public static event Action<int, Vector3[], bool> OnFaceLandmarksReceived;  // playerId, landmarks, hasData
    public static event Action<int, Vector2, float, bool> OnGazeDataReceived;  // playerId, gazePos, pupilSize, hasData

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    #region Spatial Alignment (SpatialAlignmentManager)

    /// <summary>
    /// Broadcast this client's reference point to all other clients
    /// </summary>
    public static void BroadcastSpatialReference(Vector3 origin, Quaternion rotation)
    {
        if (instance == null || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("AlignmentNetworkHub: Not connected to broadcast spatial reference");
            return;
        }

        instance.photonView.RPC(
            "ReceiveSpatialReference",
            RpcTarget.AllBuffered,
            PhotonNetwork.LocalPlayer.ActorNumber,
            origin.x, origin.y, origin.z,
            rotation.x, rotation.y, rotation.z, rotation.w
        );
    }

    [PunRPC]
    void ReceiveSpatialReference(int playerId, float px, float py, float pz, float rx, float ry, float rz, float rw)
    {
        Vector3 origin = new Vector3(px, py, pz);
        Quaternion rotation = new Quaternion(rx, ry, rz, rw);
        
        Debug.Log($"<color=cyan>[AlignmentHub] Received spatial reference from Player {playerId}: {origin}</color>");
        
        OnSpatialAlignmentReceived?.Invoke(playerId, origin, rotation);
    }

    #endregion

    #region Mesh Alignment (MeshAlignmentTool)

    /// <summary>
    /// Broadcast mesh alignment transform to all other clients
    /// </summary>
    public static void BroadcastMeshAlignment(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (instance == null || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("AlignmentNetworkHub: Not connected to broadcast mesh alignment");
            return;
        }

        instance.photonView.RPC(
            "ReceiveMeshAlignment",
            RpcTarget.Others,
            position.x, position.y, position.z,
            rotation.x, rotation.y, rotation.z, rotation.w,
            scale.x, scale.y, scale.z
        );
    }

    [PunRPC]
    void ReceiveMeshAlignment(float px, float py, float pz, float rx, float ry, float rz, float rw, float sx, float sy, float sz)
    {
        Vector3 position = new Vector3(px, py, pz);
        Quaternion rotation = new Quaternion(rx, ry, rz, rw);
        Vector3 scale = new Vector3(sx, sy, sz);
        
        Debug.Log($"<color=cyan>[AlignmentHub] Received mesh alignment update</color>");
        
        OnMeshAlignmentReceived?.Invoke(position, rotation, scale);
    }

    #endregion

    #region Mesh Alignment Mode (for synchronized mode switching)

    /// <summary>
    /// Broadcast mesh alignment mode change to all other clients
    /// </summary>
    public static void BroadcastMeshAlignmentModeChanged(bool isEnabled)
    {
        if (instance == null || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("AlignmentNetworkHub: Not connected to broadcast mesh alignment mode");
            return;
        }

        instance.photonView.RPC(
            "ReceiveMeshAlignmentModeChanged",
            RpcTarget.Others,
            isEnabled
        );
    }

    [PunRPC]
    void ReceiveMeshAlignmentModeChanged(bool isEnabled)
    {
        Debug.Log($"<color=cyan>[AlignmentHub] Remote user {(isEnabled ? "entered" : "exited")} mesh alignment mode</color>");
        
        OnMeshAlignmentModeChanged?.Invoke(isEnabled);
    }

    #endregion

    #region Face and Gaze Data (PhotonFaceGazeTransmitter)

    /// <summary>
    /// Broadcast face landmarks data to all other clients
    /// </summary>
    public static void BroadcastFaceLandmarks(Vector3[] landmarks)
    {
        if (instance == null || !PhotonNetwork.InRoom || landmarks == null)
        {
            Debug.LogWarning("AlignmentNetworkHub: Cannot broadcast face landmarks");
            return;
        }

        // Convert Vector3 array to float array for RPC transmission
        float[] landmarkData = new float[landmarks.Length * 3];
        for (int i = 0; i < landmarks.Length; i++)
        {
            landmarkData[i * 3] = landmarks[i].x;
            landmarkData[i * 3 + 1] = landmarks[i].y;
            landmarkData[i * 3 + 2] = landmarks[i].z;
        }

        instance.photonView.RPC(
            "ReceiveFaceLandmarks",
            RpcTarget.Others,
            PhotonNetwork.LocalPlayer.ActorNumber,
            landmarkData,
            true
        );
    }

    /// <summary>
    /// Broadcast gaze data to all other clients
    /// </summary>
    public static void BroadcastGazeData(Vector2 gazePosition, float pupilSize)
    {
        if (instance == null || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("AlignmentNetworkHub: Cannot broadcast gaze data");
            return;
        }

        instance.photonView.RPC(
            "ReceiveGazeData",
            RpcTarget.Others,
            PhotonNetwork.LocalPlayer.ActorNumber,
            gazePosition.x, gazePosition.y,
            pupilSize,
            true
        );
    }

    [PunRPC]
    void ReceiveFaceLandmarks(int playerId, float[] landmarkData, bool hasData)
    {
        if (hasData && landmarkData != null)
        {
            // Convert float array back to Vector3 array
            Vector3[] landmarks = new Vector3[landmarkData.Length / 3];
            for (int i = 0; i < landmarks.Length; i++)
            {
                landmarks[i] = new Vector3(
                    landmarkData[i * 3],
                    landmarkData[i * 3 + 1],
                    landmarkData[i * 3 + 2]
                );
            }

            Debug.Log($"<color=green>[AlignmentHub] Received face landmarks from Player {playerId}: {landmarks.Length} points</color>");
            OnFaceLandmarksReceived?.Invoke(playerId, landmarks, hasData);
        }
        else
        {
            OnFaceLandmarksReceived?.Invoke(playerId, null, false);
        }
    }

    [PunRPC]
    void ReceiveGazeData(int playerId, float gazeX, float gazeY, float pupilSize, bool hasData)
    {
        Vector2 gazePosition = new Vector2(gazeX, gazeY);
        
        Debug.Log($"<color=green>[AlignmentHub] Received gaze data from Player {playerId}: {gazePosition}</color>");
        OnGazeDataReceived?.Invoke(playerId, gazePosition, pupilSize, hasData);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Check if the network hub is ready to send/receive
    /// </summary>
    public static bool IsReady => instance != null && PhotonNetwork.InRoom;

    /// <summary>
    /// Get the singleton instance (useful for debugging)
    /// </summary>
    public static AlignmentNetworkHub Instance => instance;

    #endregion

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
