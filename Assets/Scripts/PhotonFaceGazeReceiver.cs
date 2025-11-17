using UnityEngine;
using Photon.Pun;

/// <summary>
/// Receives and visualizes face mesh and eye gaze data from remote clients via Photon.
/// Attach this script to a GameObject that represents a remote player.
/// It will automatically find and use the PhotonFaceGazeTransmitter component on the same object.
/// </summary>
public class PhotonFaceGazeReceiver : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the PhotonFaceGazeTransmitter component")]
    public PhotonFaceGazeTransmitter transmitter;

    [Header("Face Mesh Visualization")]
    [Tooltip("Prefab or GameObject to instantiate for each landmark")]
    public GameObject landmarkPrefab;
    
    [Tooltip("Parent transform for landmark visualizations")]
    public Transform landmarkParent;
    
    [Tooltip("Scale multiplier for landmark positions - increase if landmarks too small")]
    public float landmarkScale = 10f; // Changed from 1f to 10f for better visibility
    
    [Tooltip("Offset for landmark visualization")]
    public Vector3 landmarkOffset = Vector3.zero;
    
    [Tooltip("Size of each landmark sphere")]
    public float landmarkSphereSize = 0.05f; // Bigger default size

    [Header("Gaze Visualization")]
    [Tooltip("GameObject to represent the gaze point (e.g., a sphere)")]
    public GameObject gazeIndicator;
    
    [Tooltip("Reference to the camera for gaze raycasting")]
    public Camera targetCamera;
    
    [Tooltip("Max distance for gaze ray")]
    public float gazeRayDistance = 10f;
    
    [Tooltip("Show gaze ray in Scene view")]
    public bool showGazeRay = true;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Internal state
    private GameObject[] landmarkObjects;
    private PhotonView photonView;
    private bool isInitialized = false;

    private void Awake()
    {
        // Auto-find transmitter if not assigned
        if (transmitter == null)
            transmitter = GetComponent<PhotonFaceGazeTransmitter>();
        
        photonView = GetComponent<PhotonView>();
        
        // Setup camera reference
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Start()
    {
        // Only visualize for remote players (not our own)
        if (photonView != null && photonView.IsMine)
        {
            enabled = false;
            return;
        }

        InitializeLandmarkVisualization();
        InitializeGazeVisualization();
        
        isInitialized = true;
    }

    private void InitializeLandmarkVisualization()
    {
        if (landmarkPrefab == null)
        {
            Debug.Log("PhotonFaceGazeReceiver: Creating default landmark spheres (bright yellow, larger size)");
            landmarkPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            landmarkPrefab.transform.localScale = Vector3.one * landmarkSphereSize;
            
            // Make it bright and easy to see
            Renderer rend = landmarkPrefab.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.yellow;
            mat.SetFloat("_Glossiness", 0f);
            mat.SetFloat("_Metallic", 0f);
            rend.material = mat;
            
            // Remove collider for performance
            Collider col = landmarkPrefab.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
        }

        // Create parent if needed
        if (landmarkParent == null)
        {
            GameObject parentObj = new GameObject("RemoteFaceLandmarks");
            parentObj.transform.SetParent(transform);
            parentObj.transform.localPosition = landmarkOffset;
            landmarkParent = parentObj.transform;
        }

        // Create landmark visualization objects
        if (transmitter != null)
        {
            int landmarkCount = transmitter.keyLandmarksCount;
            landmarkObjects = new GameObject[landmarkCount];
            
            for (int i = 0; i < landmarkCount; i++)
            {
                GameObject landmark = Instantiate(landmarkPrefab, landmarkParent);
                landmark.name = $"Landmark_{i}";
                landmark.SetActive(false); // Hide until data is received
                landmarkObjects[i] = landmark;
            }
        }
    }

    private void InitializeGazeVisualization()
    {
        if (gazeIndicator == null)
        {
            // Create a simple sphere for gaze indication
            gazeIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gazeIndicator.name = "RemoteGazeIndicator";
            gazeIndicator.transform.SetParent(transform);
            gazeIndicator.transform.localScale = Vector3.one * 0.05f;
            
            Renderer renderer = gazeIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0f, 0f, 0.7f);
            }
            
            gazeIndicator.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isInitialized || transmitter == null)
            return;

        UpdateFaceMeshVisualization();
        UpdateGazeVisualization();
    }

    private void UpdateFaceMeshVisualization()
    {
        if (!transmitter.HasFaceData || landmarkObjects == null)
            return;

        Vector3[] landmarks = transmitter.GetReceivedLandmarks();
        
        for (int i = 0; i < landmarkObjects.Length && i < landmarks.Length; i++)
        {
            if (landmarkObjects[i] != null)
            {
                Vector3 position = landmarks[i];
                
                // Check if landmark data is valid
                if (position != Vector3.zero)
                {
                    // Apply scale and offset
                    Vector3 worldPos = landmarkParent.TransformPoint(position * landmarkScale);
                    landmarkObjects[i].transform.position = worldPos;
                    landmarkObjects[i].SetActive(true);
                }
                else
                {
                    landmarkObjects[i].SetActive(false);
                }
            }
        }

        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Visualizing {landmarks.Length} face landmarks from {photonView?.Owner?.NickName}");
        }
    }

    private void UpdateGazeVisualization()
    {
        if (!transmitter.HasGazeData || gazeIndicator == null || targetCamera == null)
        {
            if (gazeIndicator != null)
                gazeIndicator.SetActive(false);
            return;
        }

        Vector2 gazePos = transmitter.GetReceivedGazePosition();
        
        // Validate gaze position
        if (float.IsNaN(gazePos.x) || float.IsNaN(gazePos.y) || 
            gazePos.x < 0 || gazePos.x > 1 || gazePos.y < 0 || gazePos.y > 1)
        {
            gazeIndicator.SetActive(false);
            return;
        }

        // Convert normalized screen coordinates to world position via raycast
        Vector3 screenPos = new Vector3(
            gazePos.x * Screen.width,
            gazePos.y * Screen.height,
            0f
        );

        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        
        if (Physics.Raycast(ray, out RaycastHit hit, gazeRayDistance))
        {
            // Hit something - place indicator at hit point
            gazeIndicator.transform.position = hit.point;
            gazeIndicator.SetActive(true);

            if (showGazeRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red);
            }
        }
        else
        {
            // Didn't hit anything - place at max distance
            gazeIndicator.transform.position = ray.origin + ray.direction * gazeRayDistance;
            gazeIndicator.SetActive(true);

            if (showGazeRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * gazeRayDistance, Color.yellow);
            }
        }

        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Visualizing gaze at {gazePos} from {photonView?.Owner?.NickName}");
        }
    }

    private void OnGUI()
    {
        if (!showDebugInfo || transmitter == null)
            return;

        GUILayout.BeginArea(new Rect(420, 150, 400, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"<b>Photon Face/Gaze Receiver</b>");
        GUILayout.Label($"Remote Player: {photonView?.Owner?.NickName ?? "Unknown"}");
        GUILayout.Label($"Has Face Data: {transmitter.HasFaceData}");
        GUILayout.Label($"Has Gaze Data: {transmitter.HasGazeData}");
        
        if (transmitter.HasGazeData)
        {
            Vector2 gazePos = transmitter.GetReceivedGazePosition();
            GUILayout.Label($"Gaze Pos: ({gazePos.x:F3}, {gazePos.y:F3})");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        // Clean up landmark objects
        if (landmarkObjects != null)
        {
            foreach (var landmark in landmarkObjects)
            {
                if (landmark != null)
                    Destroy(landmark);
            }
        }

        // Clean up gaze indicator
        if (gazeIndicator != null)
            Destroy(gazeIndicator);
    }
}
