using UnityEngine;
using Photon.Pun;

/// <summary>
/// Checks why landmarks aren't visible and provides fixes.
/// Attach this to the same GameObject as PhotonFaceGazeReceiver.
/// </summary>
public class VisualizationDebugger : MonoBehaviour
{
    private PhotonFaceGazeReceiver receiver;
    private PhotonFaceGazeTransmitter transmitter;
    
    void Start()
    {
        receiver = GetComponent<PhotonFaceGazeReceiver>();
        transmitter = GetComponent<PhotonFaceGazeTransmitter>();
        
        InvokeRepeating("CheckVisualization", 2f, 3f);
    }
    
    void CheckVisualization()
    {
        if (receiver == null || transmitter == null)
        {
            Debug.LogError("[VizDebug] Missing receiver or transmitter component!");
            return;
        }
        
        Debug.Log("========== VISUALIZATION DEBUG ==========");
        Debug.Log($"Receiver enabled: {receiver.enabled}");
        Debug.Log($"Receiver initialized: {receiver.isActiveAndEnabled}");
        Debug.Log($"Has Face Data: {transmitter.HasFaceData}");
        Debug.Log($"Has Gaze Data: {transmitter.HasGazeData}");
        
        if (!transmitter.HasFaceData)
        {
            Debug.LogWarning("[VizDebug] No face data being received!");
            Debug.LogWarning("  → Check remote client is transmitting");
            return;
        }
        
        Vector3[] landmarks = transmitter.GetReceivedLandmarks();
        Debug.Log($"Received {landmarks.Length} landmarks");
        
        // Check if landmarks have valid data
        int validCount = 0;
        int zeroCount = 0;
        
        for (int i = 0; i < landmarks.Length; i++)
        {
            if (landmarks[i] != Vector3.zero)
            {
                validCount++;
                if (i == 0)
                {
                    Debug.Log($"  Sample Landmark[0]: {landmarks[0]}");
                }
            }
            else
            {
                zeroCount++;
            }
        }
        
        Debug.Log($"Valid landmarks: {validCount}, Zero landmarks: {zeroCount}");
        
        if (validCount == 0)
        {
            Debug.LogError("[VizDebug] ALL landmarks are zero!");
            Debug.LogError("  → Remote client LSL might not be sending data");
            return;
        }
        
        // Check if visualization objects exist
        Transform landmarkParent = transform.Find("RemoteFaceLandmarks");
        if (landmarkParent == null)
        {
            Debug.LogError("[VizDebug] Landmark parent not found!");
            Debug.LogError("  → Receiver might not have initialized properly");
            return;
        }
        
        Debug.Log($"Landmark parent found: {landmarkParent.name}");
        Debug.Log($"  Position: {landmarkParent.position}");
        Debug.Log($"  Children: {landmarkParent.childCount}");
        
        // Check individual landmarks
        int activeCount = 0;
        int inactiveCount = 0;
        
        for (int i = 0; i < landmarkParent.childCount; i++)
        {
            Transform child = landmarkParent.GetChild(i);
            if (child.gameObject.activeSelf)
            {
                activeCount++;
                if (i == 0)
                {
                    Debug.Log($"  Landmark[0] position: {child.position}");
                    Debug.Log($"  Landmark[0] scale: {child.localScale}");
                    
                    // Check if it's visible to camera
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 screenPos = cam.WorldToScreenPoint(child.position);
                        bool onScreen = screenPos.z > 0 && 
                                       screenPos.x >= 0 && screenPos.x <= Screen.width &&
                                       screenPos.y >= 0 && screenPos.y <= Screen.height;
                        
                        Debug.Log($"  Landmark[0] on screen: {onScreen}");
                        Debug.Log($"    Screen pos: {screenPos}");
                        
                        if (!onScreen)
                        {
                            Debug.LogWarning("[VizDebug] Landmarks exist but are OFF SCREEN!");
                            Debug.LogWarning("  → They might be too far away or behind camera");
                            Debug.LogWarning("  → Try adjusting landmarkScale or camera position");
                        }
                    }
                }
            }
            else
            {
                inactiveCount++;
            }
        }
        
        Debug.Log($"Active landmarks: {activeCount}, Inactive: {inactiveCount}");
        
        if (activeCount == 0)
        {
            Debug.LogError("[VizDebug] No active landmark objects!");
            Debug.LogError("  → All landmarks are hidden/inactive");
        }
        else
        {
            Debug.Log($"✓ {activeCount} landmarks should be visible");
            
            if (activeCount > 0)
            {
                Debug.LogWarning("[VizDebug] Landmarks exist and are active but you can't see them?");
                Debug.LogWarning("  POSSIBLE ISSUES:");
                Debug.LogWarning("  1. Landmarks too small (scale = 0.01) - increase landmarkScale");
                Debug.LogWarning("  2. Landmarks behind camera - check camera position");
                Debug.LogWarning("  3. Landmarks too far away - check landmarkParent position");
                Debug.LogWarning("  4. Camera culling - check camera far plane");
                Debug.LogWarning("  5. Landmarks inside other objects");
                Debug.LogWarning("");
                Debug.LogWarning("  QUICK FIX: In receiver component, set landmarkScale = 10");
            }
        }
        
        // Check gaze indicator
        Transform gazeIndicator = transform.Find("RemoteGazeIndicator");
        if (gazeIndicator != null)
        {
            Debug.Log($"Gaze indicator found: {gazeIndicator.gameObject.activeSelf}");
            Debug.Log($"  Position: {gazeIndicator.position}");
        }
        else
        {
            Debug.LogWarning("[VizDebug] Gaze indicator not found");
        }
        
        Debug.Log("=========================================");
    }
    
    void OnGUI()
    {
        if (receiver == null || transmitter == null)
            return;
        
        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 400, 140));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("<b>Visualization Status</b>");
        GUILayout.Label($"Has Data: {(transmitter.HasFaceData ? "✓" : "✗")}");
        
        if (transmitter.HasFaceData)
        {
            Transform parent = transform.Find("RemoteFaceLandmarks");
            if (parent != null)
            {
                int activeCount = 0;
                foreach (Transform child in parent)
                {
                    if (child.gameObject.activeSelf)
                        activeCount++;
                }
                GUILayout.Label($"Active Landmarks: {activeCount}");
                
                if (activeCount == 0)
                {
                    GUI.contentColor = Color.red;
                    GUILayout.Label("⚠️ No active landmarks!");
                }
                else
                {
                    GUI.contentColor = Color.yellow;
                    GUILayout.Label("If you can't see them:");
                    GUILayout.Label("  Increase 'Landmark Scale' to 10+");
                }
                GUI.contentColor = Color.white;
            }
        }
        
        if (GUILayout.Button("Check Visualization"))
        {
            CheckVisualization();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
