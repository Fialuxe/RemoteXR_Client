using UnityEngine;

/// <summary>
/// Visualizes the local user's view frustum (pyramid shape) instead of remote user's gaze.
/// Shows the Field of View area that the LOCAL user (you) is looking at.
/// Frustum follows the local camera's position and rotation.
/// Designed to be non-intrusive with thin wireframe lines.
/// </summary>
public class GazeFrustumVisualizer : MonoBehaviour
{
    [Header("Required References")]
    public Transform cameraTransform;
    
    [Header("Frustum Settings")]
    [Tooltip("Distance from camera where the frustum ends")]
    public float frustumDistance = 5f;
    
    [Tooltip("Distance from camera where the frustum starts")]
    public float frustumStartDistance = 0.3f;
    
    [Tooltip("Use local camera's FOV (if false, uses custom FOV below)")]
    public bool useLocalCameraFOV = true;
    
    [Tooltip("Custom horizontal field of view for the frustum (in degrees) - only used if useLocalCameraFOV is false")]
    public float customHorizontalFOV = 30f;
    
    [Tooltip("Custom vertical field of view for the frustum (in degrees) - only used if useLocalCameraFOV is false")]
    public float customVerticalFOV = 20f;
    
    [Header("Visual Settings")]
    [Tooltip("Color of the frustum wireframe")]
    public Color frustumColor = new Color(0f, 1f, 1f, 0.7f); // Cyan with transparency
    
    [Tooltip("Width of the frustum lines")]
    public float lineWidth = 0.005f;
    
    [Tooltip("Show the near plane of the frustum")]
    public bool showNearPlane = true;
    
    [Tooltip("Show the far plane of the frustum")]
    public bool showFarPlane = true;
    
    [Header("Gaze Direction")]
    [Tooltip("This is now IGNORED - frustum follows local camera direction")]
    public Vector2 gazeOffset = Vector2.zero;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    [Header("Simulation - NOT USED")]
    [Tooltip("Simulation is disabled - frustum follows camera")]
    public bool enableSimulation = false;
    
    [Tooltip("Speed of simulation movement")]
    public float simulationSpeed = 0.5f;
    
    private LineRenderer[] edgeLines;      // 4 edges connecting near to far
    private LineRenderer[] nearPlaneLines; // 4 lines for near rectangle
    private LineRenderer[] farPlaneLines;  // 4 lines for far rectangle
    
    // Frustum corner points
    private Vector3[] nearCorners = new Vector3[4];
    private Vector3[] farCorners = new Vector3[4];

    void Awake()
    {
        // Initialize frustum in Awake to ensure it happens before any other script tries to use it
        if (showDebugInfo)
            Debug.Log("[GazeFrustumVisualizer] Awake() called - Initializing frustum...");
        
        InitializeFrustum();
        
        if (showDebugInfo)
            Debug.Log("[GazeFrustumVisualizer] Frustum initialized successfully in Awake()");
    }

    void Start()
    {
        // Ensure visibility is applied after initialization
        if (showDebugInfo)
            Debug.Log("[GazeFrustumVisualizer] Start() called");
        
        SetVisible(enabled);
    }

    void OnEnable()
    {
        // When component is enabled, ensure lines are visible if already initialized
        if (edgeLines != null && edgeLines.Length > 0 && edgeLines[0] != null)
        {
            SetVisible(true);
        }
    }

    void Update()
    {
        if (cameraTransform == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[GazeFrustumVisualizer] Camera transform is NULL!");
            return;
        }
        
        // Frustum always follows local camera - no simulation needed
        UpdateFrustumGeometry();
        DrawFrustum();
    }
    
    /// <summary>
    /// Initializes all LineRenderers for the frustum wireframe
    /// </summary>
    private void InitializeFrustum()
    {
        // Create 4 edge lines (connecting near to far corners)
        edgeLines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject edgeObj = new GameObject($"FrustumEdge_{i}");
            edgeObj.transform.parent = transform;
            edgeLines[i] = CreateLineRenderer(edgeObj);
        }
        
        // Create 4 lines for near plane rectangle
        nearPlaneLines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject nearObj = new GameObject($"FrustumNear_{i}");
            nearObj.transform.parent = transform;
            nearPlaneLines[i] = CreateLineRenderer(nearObj);
        }
        
        // Create 4 lines for far plane rectangle
        farPlaneLines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject farObj = new GameObject($"FrustumFar_{i}");
            farObj.transform.parent = transform;
            farPlaneLines[i] = CreateLineRenderer(farObj);
        }
    }
    
    /// <summary>
    /// Creates and configures a LineRenderer for the frustum wireframe
    /// </summary>
    private LineRenderer CreateLineRenderer(GameObject obj)
    {
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        
        // Configure line renderer
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;
        
        // Setup material with Unlit/Color shader
        Material lineMat = new Material(Shader.Find("Unlit/Color"));
        lineMat.color = frustumColor;
        lr.material = lineMat;
        
        lr.startColor = frustumColor;
        lr.endColor = frustumColor;
        
        // Render settings
        lr.sortingOrder = 999; // Slightly lower than gaze ray to avoid z-fighting
        lr.receiveShadows = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        return lr;
    }
    
    /// <summary>
    /// This method is called by GazeVisualizationManager but is IGNORED.
    /// Frustum follows local camera, not remote gaze data.
    /// </summary>
    public void UpdateGazePosition2D(Vector2 gazeScreenPos)
    {
        // Intentionally do nothing - frustum follows local camera only
        if (showDebugInfo)
        {
            Debug.Log($"[GazeFrustumVisualizer] UpdateGazePosition2D called but IGNORED. Frustum follows local camera.");
        }
    }
    
    /// <summary>
    /// Calculates the frustum corner positions based on LOCAL camera's position and direction.
    /// Frustum always points in the direction the local camera is facing.
    /// </summary>
    private void UpdateFrustumGeometry()
    {
        if (cameraTransform == null) return;
        
        Vector3 cameraPos = cameraTransform.position;
        
        // Get camera's actual FOV if available
        Camera cam = cameraTransform.GetComponent<Camera>();
        float camVerticalFOV = 60f;
        float aspect = 16f / 9f;
        
        if (cam != null)
        {
            camVerticalFOV = cam.fieldOfView;
            aspect = cam.aspect;
        }
        
        // CRITICAL CHANGE: Use local camera's forward direction directly
        // No gaze offset - frustum points exactly where camera points
        Vector3 frustumDirection = cameraTransform.forward;
        
        // Calculate frustum center points along the camera's forward direction
        Vector3 nearCenter = cameraPos + frustumDirection * frustumStartDistance;
        Vector3 farCenter = cameraPos + frustumDirection * frustumDistance;
        
        // Determine frustum FOV
        float frustumHorizontalFOV, frustumVerticalFOV;
        
        if (useLocalCameraFOV)
        {
            // Use the local camera's actual FOV
            // This way, the far plane matches exactly what the local user sees
            frustumVerticalFOV = camVerticalFOV;
            float frustumHorizontalFOVRad = 2f * Mathf.Atan(Mathf.Tan(camVerticalFOV * Mathf.Deg2Rad / 2f) * aspect);
            frustumHorizontalFOV = frustumHorizontalFOVRad * Mathf.Rad2Deg;
        }
        else
        {
            // Use custom FOV values
            frustumHorizontalFOV = customHorizontalFOV;
            frustumVerticalFOV = customVerticalFOV;
        }
        
        // Convert FOV to radians
        float hFOVRad = frustumHorizontalFOV * Mathf.Deg2Rad;
        float vFOVRad = frustumVerticalFOV * Mathf.Deg2Rad;
        
        // Calculate half-widths and half-heights at near and far planes
        float nearHalfWidth = frustumStartDistance * Mathf.Tan(hFOVRad / 2f);
        float nearHalfHeight = frustumStartDistance * Mathf.Tan(vFOVRad / 2f);
        
        float farHalfWidth = frustumDistance * Mathf.Tan(hFOVRad / 2f);
        float farHalfHeight = frustumDistance * Mathf.Tan(vFOVRad / 2f);
        
        // Get right and up vectors based on camera's orientation (not gaze direction!)
        Vector3 right = cameraTransform.right;
        Vector3 up = cameraTransform.up;
        
        // Calculate 4 corners for near plane
        // Order: TopLeft, TopRight, BottomRight, BottomLeft
        nearCorners[0] = nearCenter + up * nearHalfHeight - right * nearHalfWidth;
        nearCorners[1] = nearCenter + up * nearHalfHeight + right * nearHalfWidth;
        nearCorners[2] = nearCenter - up * nearHalfHeight + right * nearHalfWidth;
        nearCorners[3] = nearCenter - up * nearHalfHeight - right * nearHalfWidth;
        
        // Calculate 4 corners for far plane
        farCorners[0] = farCenter + up * farHalfHeight - right * farHalfWidth;
        farCorners[1] = farCenter + up * farHalfHeight + right * farHalfWidth;
        farCorners[2] = farCenter - up * farHalfHeight + right * farHalfWidth;
        farCorners[3] = farCenter - up * farHalfHeight - right * farHalfWidth;
        
        if (showDebugInfo)
        {
            Debug.Log($"Frustum Geometry: Camera Dir={frustumDirection}, H-FOV={frustumHorizontalFOV:F1}°, V-FOV={frustumVerticalFOV:F1}°");
        }
    }
    
    /// <summary>
    /// Draws the frustum wireframe using LineRenderers
    /// </summary>
    private void DrawFrustum()
    {
        // Draw 4 edges connecting near to far
        for (int i = 0; i < 4; i++)
        {
            edgeLines[i].SetPosition(0, nearCorners[i]);
            edgeLines[i].SetPosition(1, farCorners[i]);
            edgeLines[i].enabled = true;
        }
        
        // Draw near plane rectangle
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            nearPlaneLines[i].SetPosition(0, nearCorners[i]);
            nearPlaneLines[i].SetPosition(1, nearCorners[next]);
            nearPlaneLines[i].enabled = showNearPlane;
        }
        
        // Draw far plane rectangle
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            farPlaneLines[i].SetPosition(0, farCorners[i]);
            farPlaneLines[i].SetPosition(1, farCorners[next]);
            farPlaneLines[i].enabled = showFarPlane;
        }
    }
    
    /// <summary>
    /// Sets visibility of all frustum lines
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (showDebugInfo)
            Debug.Log($"[GazeFrustumVisualizer] SetVisible called: {visible}");
        
        // Check if frustum has been initialized yet
        if (edgeLines == null || edgeLines.Length == 0)
        {
            if (showDebugInfo)
                Debug.LogWarning("[GazeFrustumVisualizer] SetVisible called before initialization! Lines will be set visible after Start().");
            return;
        }
        
        int edgeCount = 0, nearCount = 0, farCount = 0;
        
        if (edgeLines != null)
        {
            foreach (var lr in edgeLines)
            {
                if (lr != null)
                {
                    lr.enabled = visible;
                    edgeCount++;
                }
            }
        }
        
        if (nearPlaneLines != null)
        {
            foreach (var lr in nearPlaneLines)
            {
                if (lr != null)
                {
                    lr.enabled = visible && showNearPlane;
                    nearCount++;
                }
            }
        }
        
        if (farPlaneLines != null)
        {
            foreach (var lr in farPlaneLines)
            {
                if (lr != null)
                {
                    lr.enabled = visible && showFarPlane;
                    farCount++;
                }
            }
        }
        
        if (showDebugInfo)
            Debug.Log($"[GazeFrustumVisualizer] Line visibility set: edges={edgeCount}, near={nearCount}, far={farCount}");
    }
    
    /// <summary>
    /// This method is deprecated and does nothing.
    /// Frustum follows local camera automatically.
    /// </summary>
    public void SetGazeScreenPosition(float x, float y)
    {
        // Intentionally do nothing - frustum follows local camera only
        if (showDebugInfo)
        {
            Debug.Log($"[GazeFrustumVisualizer] SetGazeScreenPosition called but IGNORED.");
        }
    }
}
