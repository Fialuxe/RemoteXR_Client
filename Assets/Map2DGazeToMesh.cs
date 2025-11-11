using UnityEngine;

public class Map2DGazeToMesh : MonoBehaviour
{
    [Header("Required References")]
    public LineRenderer lineRenderer;
    public Transform cameraTransform;
    
    [Header("Line Settings")]
    [Tooltip("Distance from camera where the gaze line ends")]
    public float gazeDistance = 10f;
    
    [Tooltip("Distance from camera where the line starts (to make it visible)")]
    public float lineStartOffset = 0.3f;
    
    [Tooltip("Width of the gaze line")]
    public float lineWidth = 0.02f;
    
    [Header("Visual Settings")]
    public Color lineStartColor = Color.red;
    public Color lineEndColor = Color.yellow;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    [Header("Simulation")]
    [Tooltip("Enable simulation mode to test gaze positions without Python server")]
    public bool enableSimulation = true;
    
    [Tooltip("Speed of simulation movement")]
    public float simulationSpeed = 0.5f;
    
    private Vector3 currentGazeWorldPosition;
    private float simulationTime = 0f;

    void Start()
    {
        // Initialize line renderer if not assigned
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }
        
        // Configure line renderer
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        
        // Setup material with Unlit/Color shader for visibility in game view
        Material lineMat = new Material(Shader.Find("Unlit/Color"));
        lineMat.color = lineStartColor;
        lineRenderer.material = lineMat;
        
        lineRenderer.startColor = lineStartColor;
        lineRenderer.endColor = lineEndColor;
        
        // Render settings
        lineRenderer.sortingOrder = 1000;
        lineRenderer.receiveShadows = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        // Initialize gaze position to center of screen
        currentGazeWorldPosition = cameraTransform.position + cameraTransform.forward * gazeDistance;
    }

    void Update()
    {
        if (lineRenderer != null && cameraTransform != null)
        {
            // Run simulation if enabled
            if (enableSimulation)
            {
                SimulateGazeMovement();
            }
            
            UpdateLineRenderer();
        }
    }
    
    /// <summary>
    /// Simulates gaze movement in a pattern to test the system
    /// </summary>
    private void SimulateGazeMovement()
    {
        simulationTime += Time.deltaTime * simulationSpeed;
        
        // Create different test patterns based on time
        float pattern = Mathf.Floor(simulationTime / 2f) % 5; // Change pattern every 2 seconds
        Vector2 gazePos = Vector2.zero;
        
        switch ((int)pattern)
        {
            case 0: // Top-left corner
                gazePos = new Vector2(0f, 0f);
                if (showDebugInfo && Mathf.Floor(simulationTime) != Mathf.Floor(simulationTime - Time.deltaTime))
                    Debug.Log("Simulation: Top-Left Corner (0, 0)");
                break;
                
            case 1: // Top-right corner
                gazePos = new Vector2(1f, 0f);
                if (showDebugInfo && Mathf.Floor(simulationTime) != Mathf.Floor(simulationTime - Time.deltaTime))
                    Debug.Log("Simulation: Top-Right Corner (1, 0)");
                break;
                
            case 2: // Bottom-right corner
                gazePos = new Vector2(1f, 1f);
                if (showDebugInfo && Mathf.Floor(simulationTime) != Mathf.Floor(simulationTime - Time.deltaTime))
                    Debug.Log("Simulation: Bottom-Right Corner (1, 1)");
                break;
                
            case 3: // Bottom-left corner
                gazePos = new Vector2(0f, 1f);
                if (showDebugInfo && Mathf.Floor(simulationTime) != Mathf.Floor(simulationTime - Time.deltaTime))
                    Debug.Log("Simulation: Bottom-Left Corner (0, 1)");
                break;
                
            case 4: // Center
                gazePos = new Vector2(0.5f, 0.5f);
                if (showDebugInfo && Mathf.Floor(simulationTime) != Mathf.Floor(simulationTime - Time.deltaTime))
                    Debug.Log("Simulation: Center (0.5, 0.5)");
                break;
        }
        
        UpdateGazePosition2D(gazePos);
    }
    
    /// <summary>
    /// Updates the line renderer to draw from camera to current gaze position
    /// </summary>
    private void UpdateLineRenderer()
    {
        Vector3[] positions = new Vector3[2];
        
        // Start line slightly in front of camera to make it visible
        positions[0] = cameraTransform.position + cameraTransform.forward * lineStartOffset;
        
        // End line at the current gaze world position
        positions[1] = currentGazeWorldPosition;
        
        lineRenderer.SetPositions(positions);
    }
    
    /// <summary>
    /// Update gaze position using 2D screen coordinates from Python server.
    /// Python server sends: (0,0) = top-left corner, (1,1) = bottom-right corner
    /// </summary>
    /// <param name="gazeScreenPos">Normalized screen coordinates (0-1 range)</param>
    public void UpdateGazePosition2D(Vector2 gazeScreenPos)
    {
        if (cameraTransform == null)
        {
            Debug.LogError("Camera transform is not assigned!");
            return;
        }
        
        // Convert from Python's coordinate system (0,0 = top-left, 1,1 = bottom-right)
        // to Unity's screen-based coordinate system centered at (0.5, 0.5)
        // Map from [0,1] range to [-1,1] range
        // X: 0 -> -1 (left), 0.5 -> 0 (center), 1 -> 1 (right)
        // Y: 0 -> 1 (top), 0.5 -> 0 (center), 1 -> -1 (bottom)
        // Note: Y is inverted because Python sends 0 for top, but Unity uses positive Y for up
        
        float xNormalized = (gazeScreenPos.x - 0.5f) * 2f;  // Map to [-1, 1]
        float yNormalized = -(gazeScreenPos.y - 0.5f) * 2f; // Map to [-1, 1] and invert
        
        if (showDebugInfo)
        {
            Debug.Log($"=== Gaze Update ===");
            Debug.Log($"Input from Python: ({gazeScreenPos.x:F3}, {gazeScreenPos.y:F3})");
            Debug.Log($"Normalized coords: x={xNormalized:F3}, y={yNormalized:F3}");
        }
        
        // Get camera's field of view to properly map screen space to world space
        Camera cam = cameraTransform.GetComponent<Camera>();
        float verticalFOV = 60f; // Default FOV
        float aspect = 16f / 9f;  // Default aspect ratio
        
        if (cam != null)
        {
            verticalFOV = cam.fieldOfView;
            aspect = cam.aspect;
        }
        
        // Calculate horizontal FOV from vertical FOV and aspect ratio
        float verticalFOVRad = verticalFOV * Mathf.Deg2Rad;
        float horizontalFOVRad = 2f * Mathf.Atan(Mathf.Tan(verticalFOVRad / 2f) * aspect);
        
        // Convert screen coordinates to angular offsets
        float horizontalAngle = xNormalized * (horizontalFOVRad / 2f);
        float verticalAngle = yNormalized * (verticalFOVRad / 2f);
        
        // Create direction vector in camera's local space
        // Start with forward direction and apply angular offsets
        Vector3 localDirection = new Vector3(
            Mathf.Tan(horizontalAngle),
            Mathf.Tan(verticalAngle),
            1f
        ).normalized;
        
        // Transform direction from camera local space to world space
        Vector3 worldDirection = cameraTransform.TransformDirection(localDirection);
        
        // Calculate final gaze position in world space
        currentGazeWorldPosition = cameraTransform.position + worldDirection * gazeDistance;
        
        if (showDebugInfo)
        {
            Debug.Log($"Local direction: {localDirection}");
            Debug.Log($"World direction: {worldDirection}");
            Debug.Log($"Final world position: {currentGazeWorldPosition}");
            Debug.Log($"Camera FOV: {verticalFOV}°, Aspect: {aspect:F2}");
        }
    }
    
    /// <summary>
    /// Simplified method for testing - directly sets screen position
    /// </summary>
    public void SetGazeScreenPosition(float x, float y)
    {
        UpdateGazePosition2D(new Vector2(x, y));
    }
}
