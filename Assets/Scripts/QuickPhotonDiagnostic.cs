using UnityEngine;
using Photon.Pun;

/// <summary>
/// Simple diagnostic tool - attach to any GameObject to see what's wrong.
/// This will tell you EXACTLY why data isn't flowing.
/// </summary>
public class QuickPhotonDiagnostic : MonoBehaviour
{
    void Start()
    {
        Invoke("RunDiagnostic", 3f); // Wait 3 seconds after scene starts
    }
    
    [ContextMenu("Run Diagnostic Now")]
    public void RunDiagnostic()
    {
        Debug.Log("========================================");
        Debug.Log("PHOTON DIAGNOSTIC REPORT");
        Debug.Log("========================================");
        
        // Check Photon connection
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogError("❌ NOT CONNECTED to Photon!");
            Debug.LogError("   → Make sure you're calling PhotonNetwork.ConnectUsingSettings()");
            return;
        }
        
        Debug.Log("✓ Connected to Photon");
        
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("❌ NOT IN A ROOM!");
            Debug.LogError("   → Make sure you're calling PhotonNetwork.JoinOrCreateRoom()");
            return;
        }
        
        Debug.Log($"✓ In room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"  Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
        
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            Debug.LogWarning("⚠️ ONLY 1 PLAYER in room!");
            Debug.LogWarning("   → You need BOTH remote and local clients running");
            Debug.LogWarning("   → Start the other client and wait for them to join");
            return;
        }
        
        Debug.Log($"✓ Multiple players detected ({PhotonNetwork.CurrentRoom.PlayerCount} total)");
        Debug.Log("");
        
        // Find all players
        PhotonView[] allViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        Debug.Log($"Found {allViews.Length} PhotonView objects in scene");
        Debug.Log("");
        
        int myPlayers = 0;
        int remotePlayers = 0;
        int transmittersFound = 0;
        int receiversFound = 0;
        
        foreach (PhotonView pv in allViews)
        {
            if (pv.Owner == null)
                continue;
            
            PhotonFaceGazeTransmitter transmitter = pv.GetComponent<PhotonFaceGazeTransmitter>();
            if (transmitter == null)
                continue; // Not a player with face/gaze data
            
            Debug.Log($"--- PLAYER: {pv.Owner.NickName} (Actor {pv.Owner.ActorNumber}) ---");
            Debug.Log($"  GameObject: {pv.gameObject.name}");
            Debug.Log($"  IsMine: {pv.IsMine}");
            
            if (pv.IsMine)
            {
                myPlayers++;
                Debug.Log($"  → This is MY player (transmitter mode)");
                
                // Check if configured as transmitter
                if (transmitter.faceMeshReceiver != null)
                {
                    Debug.Log($"  ✓ Has Face Mesh Receiver");
                    Debug.Log($"    Connected: {transmitter.faceMeshReceiver.IsConnected}");
                    
                    if (!transmitter.faceMeshReceiver.IsConnected)
                    {
                        Debug.LogWarning($"    ⚠️ Face LSL NOT CONNECTED!");
                        Debug.LogWarning($"       → Start LSL server: python lsl_server.py");
                    }
                }
                else
                {
                    Debug.LogWarning($"  ⚠️ Face Mesh Receiver is NULL");
                    Debug.LogWarning($"     → This player won't transmit face data");
                }
                
                if (transmitter.gazeReceiver != null)
                {
                    Debug.Log($"  ✓ Has Gaze Receiver");
                    Debug.Log($"    Connected: {transmitter.gazeReceiver.IsConnected}");
                }
                else
                {
                    Debug.LogWarning($"  ⚠️ Gaze Receiver is NULL");
                }
                
                transmittersFound++;
            }
            else
            {
                remotePlayers++;
                Debug.Log($"  → This is a REMOTE player (receiver mode)");
                Debug.Log($"  Has Face Data: {transmitter.HasFaceData}");
                Debug.Log($"  Has Gaze Data: {transmitter.HasGazeData}");
                
                if (!transmitter.HasFaceData)
                {
                    Debug.LogError($"  ❌ NOT RECEIVING face data from {pv.Owner.NickName}!");
                    Debug.LogError($"     Possible reasons:");
                    Debug.LogError($"     1. Remote player's LSL not connected");
                    Debug.LogError($"     2. Network issue");
                    Debug.LogError($"     3. PhotonView not properly configured");
                }
                
                // Check for receiver component
                PhotonFaceGazeReceiver receiver = pv.GetComponent<PhotonFaceGazeReceiver>();
                if (receiver != null)
                {
                    Debug.Log($"  ✓ Has PhotonFaceGazeReceiver (will visualize)");
                    receiversFound++;
                }
                else
                {
                    Debug.LogWarning($"  ⚠️ MISSING PhotonFaceGazeReceiver component!");
                    Debug.LogWarning($"     → Data won't be visualized");
                    Debug.LogWarning($"     → Add PhotonFaceGazeReceiver component to this GameObject");
                }
            }
            
            Debug.Log("");
        }
        
        // Summary
        Debug.Log("========================================");
        Debug.Log("SUMMARY:");
        Debug.Log($"  My players: {myPlayers}");
        Debug.Log($"  Remote players: {remotePlayers}");
        Debug.Log($"  Transmitters: {transmittersFound}");
        Debug.Log($"  Receivers with visualization: {receiversFound}");
        Debug.Log("");
        
        // Diagnosis
        if (myPlayers == 0 && remotePlayers == 0)
        {
            Debug.LogError("❌ PROBLEM: No players with PhotonFaceGazeTransmitter found!");
            Debug.LogError("   SOLUTION: Add PhotonFaceGazeTransmitter to your player prefab");
        }
        else if (myPlayers > 1)
        {
            Debug.LogError("❌ PROBLEM: Multiple 'mine' players detected!");
            Debug.LogError("   SOLUTION: You should only instantiate ONE player per client");
            Debug.LogError("   → Check if you're calling PhotonNetwork.Instantiate() multiple times");
        }
        else if (myPlayers == 1 && remotePlayers == 0)
        {
            Debug.LogWarning("⚠️ ISSUE: You see your own player but no remote players");
            Debug.LogWarning("   This means:");
            Debug.LogWarning("   1. You're the only one in the room, OR");
            Debug.LogWarning("   2. Remote player hasn't instantiated their player yet");
            Debug.LogWarning("   WAIT for remote player to join and instantiate");
        }
        else if (myPlayers == 0 && remotePlayers >= 1)
        {
            Debug.Log("✓ GOOD: Seeing remote player(s) without own player");
            Debug.Log("  This is correct for LOCAL CLIENT in observer mode");
            
            if (receiversFound == 0)
            {
                Debug.LogError("❌ BUT: No receivers found!");
                Debug.LogError("   SOLUTION: Add PhotonFaceGazeReceiver to remote player GameObjects");
            }
            else
            {
                Debug.Log("✓ PERFECT: Everything looks correct!");
            }
        }
        else if (myPlayers == 1 && remotePlayers >= 1)
        {
            Debug.Log("✓ GOOD: Both local and remote players detected");
            
            if (receiversFound == 0)
            {
                Debug.LogError("❌ BUT: No receivers for visualization!");
                Debug.LogError("   SOLUTION: Add PhotonFaceGazeReceiver to remote player GameObjects");
            }
            else
            {
                Debug.Log("✓ PERFECT: Everything looks correct!");
            }
        }
        
        Debug.Log("========================================");
    }
    
    void Update()
    {
        // Press 'R' to re-run diagnostic
        if (Input.GetKeyDown(KeyCode.R))
        {
            RunDiagnostic();
        }
    }
}
