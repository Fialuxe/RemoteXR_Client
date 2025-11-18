using UnityEngine;
using Photon.Pun;

/// <summary>
/// Receives face and gaze data via Photon RPCs.
/// Attach this to BOTH RemoteExpertAvatar and LocalWorkerAvatar prefabs.
/// </summary>
public class NetworkedDataReceiver : MonoBehaviourPunCallbacks
{
    //[Header("Visualization")]
    public enum GazeVisualizationMode
    {
        LineRenderer,
        Frustum,
        SurfaceCircle
    }
    
    public GazeVisualizationMode visualizationMode = GazeVisualizationMode.SurfaceCircle;
    public GameObject faceLandmarkPrefab;
    public float landmarkScale = 10f;
    
    [Header("Line Renderer Settings")]
    public Material lineRendererMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.cyan;
    
    [Header("Frustum Settings")]
    public Material frustumMaterial;
    public float frustumLength = 2f;
    public float frustumAngle = 5f;
    public Color frustumColor = new Color(0f, 1f, 1f, 0.3f);
    
    [Header("Surface Circle Settings")]
    public Material circleMaterial;
    public float circleRadius = 0.1f;
    public Color circleColor = Color.red;
    
    [Header("Test Mode")]
    [Tooltip("Disable gaze/face visualization to test player transforms only")]
    public bool testTransformsOnly = false;
    
    [Header("Debug")]
    [Tooltip("Show debug logs for received data")]
    public bool showDebugLogs = true;
    
    [Header("Mesh Alignment")]
    [Tooltip("Name of the mesh GameObject to align (will search by name if meshToAlign not assigned)")]
    public string meshName = "ScannedRoom";
    [Tooltip("Optional direct reference to the mesh Transform to apply alignment to")]
    public Transform meshToAlign;
    [Tooltip("If enabled, log alignment updates")]
    public bool logAlignment = true;
    
    private Vector2 gazePosition;
    private float pupilSize;
    private System.Collections.Generic.Dictionary<int, Vector3> faceLandmarks = new System.Collections.Generic.Dictionary<int, Vector3>();
    
    private int gazeDataCount = 0;
    private int landmarkDataCount = 0;
    private float lastDebugTime = 0f;
    
    private GameObject gazeIndicator;
    private LineRenderer gazeLineRenderer;
    private GameObject gazeFrustum;
    private GameObject gazeCircle;
    private System.Collections.Generic.Dictionary<int, GameObject> landmarkObjects = new System.Collections.Generic.Dictionary<int, GameObject>();
    
    void Start()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[NetworkedDataReceiver] Initialized on {gameObject.name} | IsMine: {photonView.IsMine} | Owner: {photonView.Owner?.NickName}");
        }
        
        // Setup visualization for all players unless test mode is enabled
        if (!testTransformsOnly)
        {
            SetupVisualization();
            if (showDebugLogs)
            {
                Debug.Log($"[NetworkedDataReceiver] Visualization setup complete | IsMine: {photonView.IsMine}");
            }
        }

        // Resolve meshToAlign by name if not provided
        if (meshToAlign == null && !string.IsNullOrEmpty(meshName))
        {
            GameObject meshObj = GameObject.Find(meshName);
            if (meshObj != null)
            {
                meshToAlign = meshObj.transform;
                if (showDebugLogs && logAlignment)
                    Debug.Log($"[NetworkedDataReceiver] Found mesh to align: {meshName}");
            }
            else
            {
                if (showDebugLogs && logAlignment)
                    Debug.LogWarning($"[NetworkedDataReceiver] Could not find mesh named '{meshName}' in scene.");
            }
        }

        // Register to receive Photon RaiseEvent alignment messages
        try
        {
            PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
            if (showDebugLogs && logAlignment)
                Debug.Log("[NetworkedDataReceiver] Subscribed to Photon events for alignment.");
        }
        catch (System.Exception ex)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[NetworkedDataReceiver] Could not subscribe to Photon events: {ex.Message}");
        }
    }
    
    void SetupVisualization()
    {
        // Setup Line Renderer
        Debug.Log("[NetworkedDataReceiver] Setting up Line Renderer");
        GameObject lineObj = new GameObject("GazeLineRenderer");
        lineObj.transform.parent = transform;
        gazeLineRenderer = lineObj.AddComponent<LineRenderer>();
        gazeLineRenderer.startWidth = lineWidth;
        gazeLineRenderer.endWidth = lineWidth;
        gazeLineRenderer.positionCount = 2;
        gazeLineRenderer.material = lineRendererMaterial != null ? lineRendererMaterial : new Material(Shader.Find("Sprites/Default"));
        gazeLineRenderer.startColor = lineColor;
        gazeLineRenderer.endColor = lineColor;
        gazeLineRenderer.enabled = false;
        
        // Setup Frustum
        gazeFrustum = CreateGazeFrustum();
        gazeFrustum.transform.parent = transform;
        gazeFrustum.SetActive(false);
        
        // Setup Surface Circle
        gazeCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gazeCircle.name = "GazeSurfaceCircle";
        gazeCircle.transform.parent = transform;
        gazeCircle.transform.localScale = new Vector3(circleRadius * 2f, 0.001f, circleRadius * 2f);
        
        Renderer circleRenderer = gazeCircle.GetComponent<Renderer>();
        if (circleMaterial != null)
            circleRenderer.material = circleMaterial;
        else
            circleRenderer.material.color = circleColor;
        
        // Remove collider from circle
        if (gazeCircle.GetComponent<Collider>())
            Destroy(gazeCircle.GetComponent<Collider>());
        
        gazeCircle.SetActive(false);
    }
    
    GameObject CreateGazeFrustum()
    {
        GameObject frustum = new GameObject("GazeFrustum");
        MeshFilter meshFilter = frustum.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = frustum.AddComponent<MeshRenderer>();
        
        // Create frustum mesh (pyramid shape)
        Mesh mesh = new Mesh();
        float halfAngle = frustumAngle * Mathf.Deg2Rad;
        float nearSize = Mathf.Tan(halfAngle) * 0.1f;
        float farSize = Mathf.Tan(halfAngle) * frustumLength;
        
        // 5 vertices: 1 apex + 4 far corners
        Vector3[] vertices = new Vector3[5]
        {
            Vector3.zero, // 0: Apex at origin
            new Vector3(-farSize, farSize, frustumLength),   // 1: Top-left
            new Vector3(farSize, farSize, frustumLength),    // 2: Top-right
            new Vector3(farSize, -farSize, frustumLength),   // 3: Bottom-right
            new Vector3(-farSize, -farSize, frustumLength)   // 4: Bottom-left
        };
        
        // Create 4 triangular faces + 1 square base
        int[] triangles = new int[]
        {
            // 4 sides (each is 2 triangles from apex to edge)
            0, 2, 1,  // Top face
            0, 3, 2,  // Right face
            0, 4, 3,  // Bottom face
            0, 1, 4,  // Left face
            
            // Far base (2 triangles)
            1, 2, 3,
            1, 3, 4
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        
        if (frustumMaterial != null)
            meshRenderer.material = frustumMaterial;
        else
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = frustumColor;
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            meshRenderer.material = mat;
        }
        
        return frustum;
    }
    
    [PunRPC]
    void ReceiveGazeData(float x, float y, float pupil)
    {
        gazeDataCount++;
        
        if (showDebugLogs && Time.time - lastDebugTime > 1f)
        {
            Debug.Log($"[NetworkedDataReceiver] Received gaze data: ({x:F3}, {y:F3}) pupil: {pupil:F2} | Total received: {gazeDataCount} | IsMine: {photonView.IsMine}");
            lastDebugTime = Time.time;
        }
        
        if (testTransformsOnly)
            return;
        
        gazePosition = new Vector2(x, y);
        pupilSize = pupil;
        
        UpdateGazeVisualization();
    }
    
    [PunRPC]
    void ReceiveFaceLandmark(int index, float x, float y, float z)
    {
        landmarkDataCount++;
        
        if (showDebugLogs && landmarkDataCount % 50 == 0)
        {
            Debug.Log($"[NetworkedDataReceiver] Received landmark #{index}: ({x:F3}, {y:F3}, {z:F3}) | Total received: {landmarkDataCount} | IsMine: {photonView.IsMine}");
        }
        
        if (testTransformsOnly)
            return;
        
        faceLandmarks[index] = new Vector3(x, y, z);
        UpdateFaceLandmarkVisualization(index);
    }
    
    void UpdateGazeVisualization()
    {
        if (Camera.main == null)
            return;
        
        // Convert normalized gaze to world position via raycast
        // Invert Y because screen coordinates are top-left origin, but gaze data is bottom-left
        Vector3 screenPos = new Vector3(
            gazePosition.x * Screen.width,
            (1f - gazePosition.y) * Screen.height,
            0f
        );
        
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Vector3 gazeOrigin = transform.position; // Use avatar position as gaze origin
        
        // Disable all visualizations first
        if (gazeLineRenderer != null) gazeLineRenderer.enabled = false;
        if (gazeFrustum != null) gazeFrustum.SetActive(false);
        if (gazeCircle != null) gazeCircle.SetActive(false);
        
        bool hitSurface = Physics.Raycast(ray, out RaycastHit hit, 10f);
        Vector3 gazeEndPoint = hitSurface ? hit.point : ray.origin + ray.direction * 5f;
        
        switch (visualizationMode)
        {
            case GazeVisualizationMode.LineRenderer:
                if (gazeLineRenderer != null)
                {
                    gazeLineRenderer.SetPosition(0, gazeOrigin);
                    gazeLineRenderer.SetPosition(1, gazeEndPoint);
                    gazeLineRenderer.enabled = true;
                }
                break;
                
            case GazeVisualizationMode.Frustum:
                if (gazeFrustum != null)
                {
                    gazeFrustum.transform.position = gazeOrigin;
                    gazeFrustum.transform.rotation = Quaternion.LookRotation(ray.direction);
                    gazeFrustum.SetActive(true);
                }
                break;
                
            case GazeVisualizationMode.SurfaceCircle:
                if (gazeCircle != null && hitSurface)
                {
                    gazeCircle.transform.position = hit.point + hit.normal * 0.001f;
                    gazeCircle.transform.rotation = Quaternion.LookRotation(hit.normal);
                    //add 90 degree rotation
                    gazeCircle.transform.Rotate(90f, 0f, 0f);
                    gazeCircle.SetActive(true);
                }
                break;
        }
    }
    
    void UpdateFaceLandmarkVisualization(int landmarkIndex)
    {
        if (!landmarkObjects.ContainsKey(landmarkIndex))
        {
            // Create landmark visualization
            GameObject landmark = faceLandmarkPrefab != null ? 
                Instantiate(faceLandmarkPrefab) : 
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            
            landmark.transform.parent = transform;
            landmark.transform.localScale = Vector3.one * 0.05f;
            landmark.name = $"Landmark_{landmarkIndex}";
            
            if (landmark.GetComponent<Renderer>() != null)
                landmark.GetComponent<Renderer>().material.color = Color.yellow;
            
            landmarkObjects[landmarkIndex] = landmark;
        }
        
        GameObject landmarkObj = landmarkObjects[landmarkIndex];
        Vector3 localPos = faceLandmarks[landmarkIndex] * landmarkScale;
        landmarkObj.transform.localPosition = localPos;
        landmarkObj.SetActive(true);
    }
    
    void OnDestroy()
    {
        // Clean up visualization objects
        if (gazeLineRenderer != null)
            Destroy(gazeLineRenderer.gameObject);
        if (gazeFrustum != null)
            Destroy(gazeFrustum);
        if (gazeCircle != null)
            Destroy(gazeCircle);
        
        foreach (var landmark in landmarkObjects.Values)
        {
            if (landmark != null)
                Destroy(landmark);
        }

        // Unsubscribe Photon event
        try
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnPhotonEvent;
        }
        catch {}
    }

    // Photon event handler for custom events (alignment)
    void OnPhotonEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        const byte alignmentEventCode = 101;
        if (photonEvent.Code != alignmentEventCode)
            return;

        object data = photonEvent.CustomData;
        object[] arr = data as object[];
        if (arr == null || arr.Length < 10)
        {
            if (showDebugLogs && logAlignment)
                Debug.LogWarning("[NetworkedDataReceiver] Alignment event received with invalid data.");
            return;
        }

        // Convert values safely
        float px = ConvertToFloat(arr[0]);
        float py = ConvertToFloat(arr[1]);
        float pz = ConvertToFloat(arr[2]);
        float rx = ConvertToFloat(arr[3]);
        float ry = ConvertToFloat(arr[4]);
        float rz = ConvertToFloat(arr[5]);
        float rw = ConvertToFloat(arr[6]);
        float sx = ConvertToFloat(arr[7]);
        float sy = ConvertToFloat(arr[8]);
        float sz = ConvertToFloat(arr[9]);

        if (showDebugLogs && logAlignment)
            Debug.Log($"[NetworkedDataReceiver] Alignment event -> Pos({px:F3},{py:F3},{pz:F3}) Rot({rx:F3},{ry:F3},{rz:F3},{rw:F3}) Scale({sx:F3},{sy:F3},{sz:F3})");

        if (meshToAlign == null && !string.IsNullOrEmpty(meshName))
        {
            GameObject meshObj = GameObject.Find(meshName);
            if (meshObj != null)
                meshToAlign = meshObj.transform;
        }

        if (meshToAlign == null)
        {
            if (showDebugLogs && logAlignment)
                Debug.LogWarning("[NetworkedDataReceiver] No meshToAlign to apply alignment.");
            return;
        }

        meshToAlign.position = new Vector3(px, py, pz);
        meshToAlign.rotation = new Quaternion(rx, ry, rz, rw);
        meshToAlign.localScale = new Vector3(sx, sy, sz);
        
        Debug.Log("<color=lime>[NetworkedDataReceiver] ✓ Mesh alignment applied successfully!</color>");
    }

    static float ConvertToFloat(object o)
    {
        if (o is float f) return f;
        if (o is double d) return (float)d;
        if (o is int i) return (float)i;
        if (o is long l) return (float)l;
        float.TryParse(o?.ToString() ?? "0", out float res);
        return res;
    }
}
