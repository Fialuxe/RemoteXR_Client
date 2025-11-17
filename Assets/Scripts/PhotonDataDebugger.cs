using UnityEngine;
using Photon.Pun;

/// <summary>
/// Debug tool to monitor Photon face/gaze data transmission.
/// Attach this to any GameObject in the scene to see detailed transmission logs.
/// </summary>
public class PhotonDataDebugger : MonoBehaviourPun
{
    [Header("Debug Settings")]
    public bool enableDebug = true;
    public bool logEveryFrame = false;
    public float logInterval = 2f;
    
    [Header("Visual Display")]
    public bool showOnScreenDebug = true;
    public int maxLogLines = 20;
    
    private float logTimer = 0f;
    private System.Collections.Generic.List<string> debugLog = new System.Collections.Generic.List<string>();
    
    void Update()
    {
        if (!enableDebug)
            return;
        
        logTimer += Time.deltaTime;
        
        if (logEveryFrame || logTimer >= logInterval)
        {
            CheckAllPlayers();
            logTimer = 0f;
        }
    }
    
    private void CheckAllPlayers()
    {
        AddLog("=== PHOTON DATA DEBUG ===");
        AddLog($"Time: {Time.time:F2}s");
        AddLog($"Connected: {PhotonNetwork.IsConnected}");
        AddLog($"In Room: {PhotonNetwork.InRoom}");
        AddLog($"Room: {PhotonNetwork.CurrentRoom?.Name ?? "None"}");
        AddLog($"Players in Room: {PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}");
        AddLog("");
        
        // Find all PhotonViews
        PhotonView[] allViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        AddLog($"Total PhotonViews found: {allViews.Length}");
        AddLog("");
        
        int transmitterCount = 0;
        int receiverCount = 0;
        
        foreach (PhotonView pv in allViews)
        {
            if (pv.Owner == null)
                continue;
            
            // Check for transmitter
            PhotonFaceGazeTransmitter transmitter = pv.GetComponent<PhotonFaceGazeTransmitter>();
            if (transmitter != null)
            {
                AddLog($"--- Player: {pv.Owner.NickName} (ID: {pv.Owner.ActorNumber}) ---");
                AddLog($"  GameObject: {pv.gameObject.name}");
                AddLog($"  IsMine: {pv.IsMine}");
                AddLog($"  ViewID: {pv.ViewID}");
                AddLog($"  Mode: {(pv.IsMine ? "TRANSMITTER" : "RECEIVER")}");
                
                if (pv.IsMine)
                {
                    // This is a transmitter (remote client)
                    transmitterCount++;
                    
                    AddLog($"  Face Mesh Receiver: {(transmitter.faceMeshReceiver != null ? "Present" : "Missing")}");
                    AddLog($"  Gaze Receiver: {(transmitter.gazeReceiver != null ? "Present" : "Missing")}");
                    
                    if (transmitter.faceMeshReceiver != null)
                    {
                        AddLog($"  Face LSL Connected: {transmitter.faceMeshReceiver.IsConnected}");
                    }
                    
                    if (transmitter.gazeReceiver != null)
                    {
                        AddLog($"  Gaze LSL Connected: {transmitter.gazeReceiver.IsConnected}");
                    }
                    
                    AddLog($"  Transmit Face: {transmitter.transmitFaceMesh}");
                    AddLog($"  Transmit Gaze: {transmitter.transmitGaze}");
                }
                else
                {
                    // This is a receiver (local client viewing remote)
                    receiverCount++;
                    
                    AddLog($"  Has Face Data: {transmitter.HasFaceData}");
                    AddLog($"  Has Gaze Data: {transmitter.HasGazeData}");
                    
                    if (transmitter.HasFaceData)
                    {
                        Vector3[] landmarks = transmitter.GetReceivedLandmarks();
                        AddLog($"  Received Landmarks: {landmarks.Length}");
                        
                        if (landmarks.Length > 0)
                        {
                            // Show first landmark as sample
                            AddLog($"  Sample Landmark[0]: {landmarks[0]}");
                        }
                    }
                    
                    if (transmitter.HasGazeData)
                    {
                        Vector2 gazePos = transmitter.GetReceivedGazePosition();
                        float pupilSize = transmitter.GetReceivedPupilSize();
                        AddLog($"  Gaze Position: ({gazePos.x:F3}, {gazePos.y:F3})");
                        AddLog($"  Pupil Size: {pupilSize:F3}");
                    }
                    
                    // Check for receiver component
                    PhotonFaceGazeReceiver receiver = pv.GetComponent<PhotonFaceGazeReceiver>();
                    if (receiver != null)
                    {
                        AddLog($"  Receiver Component: Present");
                    }
                    else
                    {
                        AddLog($"  Receiver Component: MISSING (no visualization)");
                    }
                }
                
                AddLog("");
            }
        }
        
        AddLog($"Summary: {transmitterCount} transmitter(s), {receiverCount} receiver(s)");
        
        // Warnings
        if (transmitterCount == 0 && receiverCount == 0)
        {
            AddLog("⚠️ WARNING: No transmitters or receivers found!");
            AddLog("   Make sure PhotonFaceGazeTransmitter is attached to player prefab.");
        }
        
        if (transmitterCount > 0 && receiverCount == 0)
        {
            AddLog("⚠️ NOTE: Transmitter found but no receivers.");
            AddLog("   This is normal if you're the only player in the room.");
        }
        
        if (receiverCount > 0)
        {
            AddLog("✓ Receiver(s) detected - check if data is being received above.");
        }
        
        AddLog("=== END DEBUG ===");
    }
    
    private void AddLog(string message)
    {
        if (enableDebug)
        {
            Debug.Log($"[PhotonDataDebug] {message}");
            
            debugLog.Add(message);
            
            // Keep only recent logs
            if (debugLog.Count > maxLogLines)
            {
                debugLog.RemoveAt(0);
            }
        }
    }
    
    private void OnGUI()
    {
        if (!showOnScreenDebug || !enableDebug)
            return;
        
        // Display on-screen debug info
        float boxWidth = 600;
        float boxHeight = Mathf.Min(maxLogLines * 20 + 40, Screen.height - 20);
        
        GUILayout.BeginArea(new Rect(Screen.width - boxWidth - 10, 10, boxWidth, boxHeight));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("<b>Photon Face/Gaze Data Debugger</b>");
        GUILayout.Label($"Press 'D' to toggle debug | Logging every {(logEveryFrame ? "frame" : logInterval + "s")}");
        
        GUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(boxHeight - 60));
        
        foreach (string log in debugLog)
        {
            if (log.Contains("WARNING") || log.Contains("⚠️"))
            {
                GUI.contentColor = Color.yellow;
            }
            else if (log.Contains("ERROR") || log.Contains("Missing"))
            {
                GUI.contentColor = Color.red;
            }
            else if (log.Contains("✓") || log.Contains("Present") || log.Contains("True"))
            {
                GUI.contentColor = Color.green;
            }
            else if (log.StartsWith("==="))
            {
                GUI.contentColor = Color.cyan;
            }
            else
            {
                GUI.contentColor = Color.white;
            }
            
            GUILayout.Label(log);
        }
        
        GUI.contentColor = Color.white;
        GUILayout.EndScrollView();
        
        if (GUILayout.Button("Check Now"))
        {
            CheckAllPlayers();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
        
        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.D))
        {
            enableDebug = !enableDebug;
        }
    }
    
    // Public method to manually trigger check
    [ContextMenu("Check Photon Data Now")]
    public void CheckNow()
    {
        CheckAllPlayers();
    }
}
