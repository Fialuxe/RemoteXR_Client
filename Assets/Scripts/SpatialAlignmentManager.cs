using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// Handles spatial alignment between different coordinate systems
/// Use this to align VR headset coordinates with desktop/laptop coordinates
/// </summary>
public class SpatialAlignmentManager : MonoBehaviourPunCallbacks
{
    [Header("Alignment Settings")]
    [Tooltip("The shared mesh reference point in the scene")]
    public Transform meshReferencePoint;
    
    [Header("Alignment Mode")]
    public AlignmentMode alignmentMode = AlignmentMode.AutoAlign;
    
    [Header("Manual Alignment (if using Manual mode)")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;
    public float scaleMultiplier = 1f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public GameObject alignmentMarkerPrefab;
    
    private Dictionary<int, AlignmentData> playerAlignments = new Dictionary<int, AlignmentData>();
    private bool isAligned = false;
    private List<GameObject> debugMarkers = new List<GameObject>();

    public enum AlignmentMode
    {
        AutoAlign,      // Automatically align based on mesh reference
        ManualAlign,    // Use manual offset values
        MarkerBased,    // Use alignment markers to calibrate
        SharedOrigin    // Both systems share the same origin (no alignment needed)
    }

    [System.Serializable]
    public class AlignmentData
    {
        public int playerId;
        public Vector3 positionOffset;
        public Quaternion rotationOffset;
        public float scale;
        public Vector3 meshOrigin; // Where the player's mesh origin is
        
        public AlignmentData(int id)
        {
            playerId = id;
            positionOffset = Vector3.zero;
            rotationOffset = Quaternion.identity;
            scale = 1f;
            meshOrigin = Vector3.zero;
        }
    }

    void Start()
    {
        if (meshReferencePoint == null)
        {
            Debug.LogWarning("Mesh reference point not set! Using scene origin.");
            GameObject refObj = new GameObject("MeshReferencePoint");
            meshReferencePoint = refObj.transform;
        }
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        
        // Share our mesh origin with other players
        if (alignmentMode != AlignmentMode.SharedOrigin)
        {
            StartCoroutine(InitiateAlignment());
        }
    }

    System.Collections.IEnumerator InitiateAlignment()
    {
        yield return new WaitForSeconds(1f); // Wait for all players to join
        
        // Send our mesh reference position to other players
        Vector3 myMeshOrigin = meshReferencePoint.position;
        Quaternion myMeshRotation = meshReferencePoint.rotation;
        
        photonView.RPC("ReceiveAlignmentData", RpcTarget.AllBuffered, 
            PhotonNetwork.LocalPlayer.ActorNumber,
            myMeshOrigin.x, myMeshOrigin.y, myMeshOrigin.z,
            myMeshRotation.x, myMeshRotation.y, myMeshRotation.z, myMeshRotation.w);
        
        Debug.Log($"<color=cyan>Sent alignment data: Origin at {myMeshOrigin}</color>");
    }

    [PunRPC]
    void ReceiveAlignmentData(int playerId, float px, float py, float pz, float rx, float ry, float rz, float rw)
    {
        Vector3 remoteOrigin = new Vector3(px, py, pz);
        Quaternion remoteRotation = new Quaternion(rx, ry, rz, rw);
        
        if (playerId != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            Debug.Log($"<color=green>Received alignment from Player {playerId}: Origin at {remoteOrigin}</color>");
            
            // Calculate offset between our mesh and their mesh
            AlignmentData alignment = new AlignmentData(playerId);
            alignment.meshOrigin = remoteOrigin;
            alignment.positionOffset = meshReferencePoint.position - remoteOrigin;
            alignment.rotationOffset = Quaternion.Inverse(remoteRotation) * meshReferencePoint.rotation;
            
            playerAlignments[playerId] = alignment;
            
            if (showDebugInfo)
            {
                CreateDebugMarker(remoteOrigin, playerId);
            }
            
            isAligned = true;
        }
    }

    /// <summary>
    /// Transform a position from another player's coordinate system to ours
    /// </summary>
    public Vector3 TransformFromPlayer(int playerId, Vector3 theirPosition)
    {
        if (alignmentMode == AlignmentMode.SharedOrigin)
            return theirPosition;

        if (alignmentMode == AlignmentMode.ManualAlign)
            return theirPosition + positionOffset;

        if (playerAlignments.TryGetValue(playerId, out AlignmentData alignment))
        {
            // Transform their position to our coordinate system
            Vector3 transformed = theirPosition + alignment.positionOffset;
            return transformed;
        }

        return theirPosition; // No alignment data, return as-is
    }

    /// <summary>
    /// Transform a rotation from another player's coordinate system to ours
    /// </summary>
    public Quaternion TransformFromPlayer(int playerId, Quaternion theirRotation)
    {
        if (alignmentMode == AlignmentMode.SharedOrigin)
            return theirRotation;

        if (alignmentMode == AlignmentMode.ManualAlign)
            return theirRotation * Quaternion.Euler(rotationOffset);

        if (playerAlignments.TryGetValue(playerId, out AlignmentData alignment))
        {
            return alignment.rotationOffset * theirRotation;
        }

        return theirRotation;
    }

    /// <summary>
    /// Get the alignment status
    /// </summary>
    public bool IsAligned()
    {
        return isAligned || alignmentMode == AlignmentMode.SharedOrigin;
    }

    void CreateDebugMarker(Vector3 position, int playerId)
    {
        GameObject marker;
        if (alignmentMarkerPrefab != null)
        {
            marker = Instantiate(alignmentMarkerPrefab, position, Quaternion.identity);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.2f;
            marker.GetComponent<Renderer>().material.color = Color.yellow;
        }
        
        marker.name = $"AlignmentMarker_Player{playerId}";
        debugMarkers.Add(marker);
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(420, 10, 350, 400));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== SPATIAL ALIGNMENT ===", GUI.skin.box);
        GUILayout.Label($"Mode: {alignmentMode}");
        GUILayout.Label($"Aligned: {isAligned}");
        GUILayout.Label($"Mesh Origin: {meshReferencePoint.position}");
        
        if (playerAlignments.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("--- PLAYER ALIGNMENTS ---", GUI.skin.box);
            foreach (var alignment in playerAlignments.Values)
            {
                GUILayout.Label($"Player {alignment.playerId}:");
                GUILayout.Label($"  Offset: {alignment.positionOffset.ToString("F2")}");
                GUILayout.Label($"  Their Origin: {alignment.meshOrigin.ToString("F2")}");
            }
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Call this to manually recalibrate alignment
    /// </summary>
    public void RecalibrateAlignment()
    {
        playerAlignments.Clear();
        isAligned = false;
        
        foreach (var marker in debugMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        debugMarkers.Clear();
        
        StartCoroutine(InitiateAlignment());
    }

    /// <summary>
    /// Set manual alignment values (useful for testing)
    /// </summary>
    public void SetManualAlignment(Vector3 posOffset, Vector3 rotOffset, float scale = 1f)
    {
        alignmentMode = AlignmentMode.ManualAlign;
        positionOffset = posOffset;
        rotationOffset = rotOffset;
        scaleMultiplier = scale;
        isAligned = true;
    }
}
