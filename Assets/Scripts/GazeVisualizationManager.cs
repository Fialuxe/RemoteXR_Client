using UnityEngine;

/// <summary>
/// Manages switching between different gaze visualization modes:
/// - Ray mode: Simple line from camera to gaze point
/// - Frustum mode: View frustum showing the FOV area
/// 
/// Both visualizations can be controlled independently and support the same gaze input.
/// </summary>
public class GazeVisualizationManager : MonoBehaviour
{
    [Header("Visualization Components")]
    [Tooltip("Reference to the ray-based gaze visualizer (Map2DGazeToMesh)")]
    public Map2DGazeToMesh rayVisualizer;
    
    [Tooltip("Reference to the frustum-based gaze visualizer")]
    public GazeFrustumVisualizer frustumVisualizer;
    
    [Header("Visualization Mode")]
    [Tooltip("Current active visualization mode")]
    public VisualizationMode currentMode = VisualizationMode.Both;
    
    [Header("Runtime Controls")]
    [Tooltip("Allow switching modes with keyboard input (Tab key)")]
    public bool enableKeyboardToggle = true;
    
    [Tooltip("Key to toggle between visualization modes")]
    public KeyCode toggleKey = KeyCode.Tab;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    /// <summary>
    /// Available gaze visualization modes
    /// </summary>
    public enum VisualizationMode
    {
        Ray,      // Simple ray from camera to gaze point
        Frustum,  // View frustum showing FOV area
        Both      // Show both visualizations simultaneously
    }
    
    private void Start()
    {
        // Validate references
        if (rayVisualizer == null)
        {
            rayVisualizer = GetComponent<Map2DGazeToMesh>();
            if (rayVisualizer == null)
            {
                Debug.LogWarning("GazeVisualizationManager: Ray visualizer (Map2DGazeToMesh) not found!");
            }
        }
        
        if (frustumVisualizer == null)
        {
            frustumVisualizer = GetComponent<GazeFrustumVisualizer>();
            if (frustumVisualizer == null)
            {
                Debug.LogWarning("GazeVisualizationManager: Frustum visualizer not found!");
            }
        }
        
        // CRITICAL: Disable simulation on both visualizers by default
        // Let the manager control visualization, not individual simulators
        if (rayVisualizer != null)
        {
            rayVisualizer.enableSimulation = false;
        }
        if (frustumVisualizer != null)
        {
            frustumVisualizer.enableSimulation = false;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"GazeVisualizationManager: Started with mode = {currentMode}");
        }
    }
    
    private void OnEnable()
    {
        // Apply mode when component is enabled
        // Use Invoke to delay until after all other Start() methods have run
        Invoke(nameof(ApplyVisualizationMode), 0.1f);
    }
    
    private void Update()
    {
        // Handle keyboard toggle
        if (enableKeyboardToggle && Input.GetKeyDown(toggleKey))
        {
            CycleVisualizationMode();
        }
    }
    
    /// <summary>
    /// Cycles through visualization modes: Ray -> Frustum -> Both -> Ray...
    /// </summary>
    public void CycleVisualizationMode()
    {
        switch (currentMode)
        {
            case VisualizationMode.Ray:
                SetVisualizationMode(VisualizationMode.Frustum);
                break;
            case VisualizationMode.Frustum:
                SetVisualizationMode(VisualizationMode.Both);
                break;
            case VisualizationMode.Both:
                SetVisualizationMode(VisualizationMode.Ray);
                break;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"GazeVisualizationManager: Cycled to mode = {currentMode}");
        }
    }
    
    /// <summary>
    /// Sets the visualization mode to a specific value
    /// </summary>
    public void SetVisualizationMode(VisualizationMode mode)
    {
        currentMode = mode;
        ApplyVisualizationMode();
        
        if (showDebugInfo)
        {
            Debug.Log($"GazeVisualizationManager: Set mode = {currentMode}");
        }
    }
    
    /// <summary>
    /// Applies the current visualization mode by enabling/disabling appropriate components
    /// </summary>
    private void ApplyVisualizationMode()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[GazeVisualizationManager] Applying mode: {currentMode}");
            Debug.Log($"[GazeVisualizationManager] Ray Visualizer: {(rayVisualizer != null ? "Found" : "NULL")}");
            Debug.Log($"[GazeVisualizationManager] Frustum Visualizer: {(frustumVisualizer != null ? "Found" : "NULL")}");
        }
        
        switch (currentMode)
        {
            case VisualizationMode.Ray:
                SetRayVisualizerActive(true);
                SetFrustumVisualizerActive(false);
                break;
                
            case VisualizationMode.Frustum:
                SetRayVisualizerActive(false);
                SetFrustumVisualizerActive(true);
                break;
                
            case VisualizationMode.Both:
                SetRayVisualizerActive(true);
                SetFrustumVisualizerActive(true);
                break;
        }
        
        // Display mode change notification (always show, not just in debug mode)
        Debug.Log($"[GazeVisualizationManager] ✓ Mode changed to: {GetCurrentModeString()}");
    }
    
    /// <summary>
    /// Enables or disables the ray visualizer
    /// </summary>
    private void SetRayVisualizerActive(bool active)
    {
        if (rayVisualizer == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[GazeVisualizationManager] Ray visualizer is NULL! Cannot set active state.");
            return;
        }
        
        if (showDebugInfo)
            Debug.Log($"[GazeVisualizationManager] Setting Ray Visualizer: enabled={active}");
        
        rayVisualizer.enabled = active;
        
        // Also control the LineRenderer visibility
        if (rayVisualizer.lineRenderer != null)
        {
            rayVisualizer.lineRenderer.enabled = active;
        }
        
        if (showDebugInfo)
            Debug.Log($"[GazeVisualizationManager] Ray Visualizer state applied: component.enabled={rayVisualizer.enabled}");
    }
    
    /// <summary>
    /// Enables or disables the frustum visualizer
    /// </summary>
    private void SetFrustumVisualizerActive(bool active)
    {
        if (frustumVisualizer == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[GazeVisualizationManager] Frustum visualizer is NULL! Cannot set active state.");
            return;
        }
        
        if (showDebugInfo)
            Debug.Log($"[GazeVisualizationManager] Setting Frustum Visualizer: enabled={active}");
        
        frustumVisualizer.enabled = active;
        frustumVisualizer.SetVisible(active);
        
        if (showDebugInfo)
            Debug.Log($"[GazeVisualizationManager] Frustum Visualizer state applied: component.enabled={frustumVisualizer.enabled}");
    }
    
    /// <summary>
    /// Updates gaze position for all active visualizers
    /// Call this from LslGazeReceiver or other gaze input source
    /// </summary>
    public void UpdateGazePosition2D(Vector2 gazeScreenPos)
    {
        if (rayVisualizer != null && rayVisualizer.enabled)
        {
            rayVisualizer.UpdateGazePosition2D(gazeScreenPos);
        }
        
        if (frustumVisualizer != null && frustumVisualizer.enabled)
        {
            frustumVisualizer.UpdateGazePosition2D(gazeScreenPos);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"GazeVisualizationManager: Updated gaze position to ({gazeScreenPos.x:F3}, {gazeScreenPos.y:F3})");
        }
    }
    
    /// <summary>
    /// Enables or disables simulation mode for all visualizers
    /// </summary>
    public void SetSimulationEnabled(bool enabled)
    {
        if (rayVisualizer != null)
        {
            rayVisualizer.enableSimulation = enabled;
        }
        
        if (frustumVisualizer != null)
        {
            frustumVisualizer.enableSimulation = enabled;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"GazeVisualizationManager: Simulation = {enabled}");
        }
    }
    
    /// <summary>
    /// Returns the current visualization mode as a string
    /// </summary>
    public string GetCurrentModeString()
    {
        switch (currentMode)
        {
            case VisualizationMode.Ray:
                return "Ray";
            case VisualizationMode.Frustum:
                return "Frustum";
            case VisualizationMode.Both:
                return "Both (Ray + Frustum)";
            default:
                return "Unknown";
        }
    }
}
