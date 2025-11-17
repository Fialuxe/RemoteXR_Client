using UnityEngine;
using Photon.Pun;
using System;

/// <summary>
/// Centralized network event hub for all alignment-related RPC calls
/// This is the ONLY script that needs a PhotonView for alignment networking
/// Other scripts can use static methods or subscribe to events
/// </summary>
public class AlignmentNetworkHub : MonoBehaviourPunCallbacks
{
    private static AlignmentNetworkHub instance;
    
    // Events for alignment data
    public static event Action<int, Vector3, Quaternion> OnSpatialAlignmentReceived;
    public static event Action<Vector3, Quaternion, Vector3> OnMeshAlignmentReceived;

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
