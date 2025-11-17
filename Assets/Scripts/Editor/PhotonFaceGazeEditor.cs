using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for PhotonFaceGazeTransmitter to provide helpful UI and debugging tools
/// </summary>
[CustomEditor(typeof(PhotonFaceGazeTransmitter))]
public class PhotonFaceGazeTransmitterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PhotonFaceGazeTransmitter transmitter = (PhotonFaceGazeTransmitter)target;
        
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        
        // Runtime status display
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical("box");
            
            // Component status
            EditorGUILayout.LabelField("Component Status:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Face Mesh Receiver:", transmitter.faceMeshReceiver != null ? "✓ Found" : "✗ Missing");
            EditorGUILayout.LabelField("Gaze Receiver:", transmitter.gazeReceiver != null ? "✓ Found" : "✗ Missing");
            
            EditorGUILayout.Space();
            
            // Connection status
            if (transmitter.faceMeshReceiver != null)
            {
                EditorGUILayout.LabelField("Face Mesh LSL:", transmitter.faceMeshReceiver.IsConnected ? "✓ Connected" : "✗ Disconnected");
            }
            
            if (transmitter.gazeReceiver != null)
            {
                EditorGUILayout.LabelField("Gaze LSL:", transmitter.gazeReceiver.IsConnected ? "✓ Connected" : "✗ Disconnected");
            }
            
            EditorGUILayout.Space();
            
            // Data status
            EditorGUILayout.LabelField("Data Status:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Has Face Data:", transmitter.HasFaceData ? "✓ Yes" : "✗ No");
            EditorGUILayout.LabelField("Has Gaze Data:", transmitter.HasGazeData ? "✓ Yes" : "✗ No");
            
            EditorGUILayout.EndVertical();
            
            // Sample data display
            if (transmitter.HasFaceData)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Sample Face Landmarks:", EditorStyles.boldLabel);
                
                for (int i = 0; i < Mathf.Min(3, transmitter.keyLandmarksCount); i++)
                {
                    Vector3 landmark = transmitter.GetReceivedLandmark(i);
                    EditorGUILayout.LabelField($"  Landmark {i}:", landmark.ToString("F3"));
                }
                
                EditorGUILayout.EndVertical();
            }
            
            if (transmitter.HasGazeData)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Gaze Data:", EditorStyles.boldLabel);
                Vector2 gazePos = transmitter.GetReceivedGazePosition();
                float pupil = transmitter.GetReceivedPupilSize();
                EditorGUILayout.LabelField($"  Position: ({gazePos.x:F3}, {gazePos.y:F3})");
                EditorGUILayout.LabelField($"  Pupil Size: {pupil:F3}");
                EditorGUILayout.EndVertical();
            }
            
            // Force repaint for live updates
            Repaint();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see runtime status", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        // Helper buttons
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Auto-Find LSL Receivers"))
        {
            Undo.RecordObject(transmitter, "Auto-Find LSL Receivers");
            
            if (transmitter.faceMeshReceiver == null)
            {
                transmitter.faceMeshReceiver = transmitter.GetComponent<LslFaceMeshReceiver>();
                if (transmitter.faceMeshReceiver != null)
                    Debug.Log("Found LslFaceMeshReceiver");
            }
            
            if (transmitter.gazeReceiver == null)
            {
                transmitter.gazeReceiver = transmitter.GetComponent<LslGazeReceiver>();
                if (transmitter.gazeReceiver != null)
                    Debug.Log("Found LslGazeReceiver");
            }
            
            EditorUtility.SetDirty(transmitter);
        }
        
        if (GUILayout.Button("Add Missing LSL Receivers"))
        {
            Undo.RecordObject(transmitter, "Add LSL Receivers");
            
            if (transmitter.faceMeshReceiver == null)
            {
                transmitter.faceMeshReceiver = Undo.AddComponent<LslFaceMeshReceiver>(transmitter.gameObject);
                Debug.Log("Added LslFaceMeshReceiver");
            }
            
            if (transmitter.gazeReceiver == null)
            {
                transmitter.gazeReceiver = Undo.AddComponent<LslGazeReceiver>(transmitter.gameObject);
                Debug.Log("Added LslGazeReceiver");
            }
            
            EditorUtility.SetDirty(transmitter);
        }
        
        // Warnings
        EditorGUILayout.Space();
        
        if (transmitter.faceMeshReceiver == null || transmitter.gazeReceiver == null)
        {
            EditorGUILayout.HelpBox("LSL receivers are not assigned. Use the buttons above to add them.", MessageType.Warning);
        }
        
        if (transmitter.GetComponent<Photon.Pun.PhotonView>() == null)
        {
            EditorGUILayout.HelpBox("PhotonView component is required for network transmission!", MessageType.Error);
        }
    }
}

/// <summary>
/// Custom editor for PhotonFaceGazeReceiver
/// </summary>
[CustomEditor(typeof(PhotonFaceGazeReceiver))]
public class PhotonFaceGazeReceiverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PhotonFaceGazeReceiver receiver = (PhotonFaceGazeReceiver)target;
        
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        
        // Runtime status
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical("box");
            
            if (receiver.transmitter != null)
            {
                EditorGUILayout.LabelField("Transmitter:", "✓ Found");
                EditorGUILayout.LabelField("Has Face Data:", receiver.transmitter.HasFaceData ? "✓ Yes" : "✗ No");
                EditorGUILayout.LabelField("Has Gaze Data:", receiver.transmitter.HasGazeData ? "✓ Yes" : "✗ No");
            }
            else
            {
                EditorGUILayout.LabelField("Transmitter:", "✗ Not found");
            }
            
            EditorGUILayout.EndVertical();
            
            // Force repaint
            Repaint();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see runtime status", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Auto-Find Transmitter"))
        {
            Undo.RecordObject(receiver, "Auto-Find Transmitter");
            
            if (receiver.transmitter == null)
            {
                receiver.transmitter = receiver.GetComponent<PhotonFaceGazeTransmitter>();
                if (receiver.transmitter != null)
                {
                    Debug.Log("Found PhotonFaceGazeTransmitter");
                    EditorUtility.SetDirty(receiver);
                }
            }
        }
        
        if (GUILayout.Button("Create Default Landmark Prefab"))
        {
            // Create a simple sphere prefab for landmarks
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * 0.01f;
            sphere.GetComponent<Renderer>().material.color = Color.yellow;
            
            receiver.landmarkPrefab = sphere;
            Debug.Log("Created default landmark prefab (sphere)");
            EditorUtility.SetDirty(receiver);
        }
        
        if (GUILayout.Button("Create Default Gaze Indicator"))
        {
            // Create a simple sphere for gaze
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * 0.05f;
            sphere.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, 0.7f);
            
            receiver.gazeIndicator = sphere;
            Debug.Log("Created default gaze indicator (red sphere)");
            EditorUtility.SetDirty(receiver);
        }
        
        // Warnings
        EditorGUILayout.Space();
        
        if (receiver.transmitter == null)
        {
            EditorGUILayout.HelpBox("Transmitter is not assigned. Use 'Auto-Find Transmitter' button.", MessageType.Warning);
        }
    }
}
