using UnityEngine;

/// <summary>
/// Simple gaze visualization helper for displaying gaze point in 3D space.
/// Attach to a sphere or other GameObject to represent the gaze point.
/// </summary>
public class GazeVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public Color gazeColor = Color.red;
    public float sphereSize = 0.05f;
    public bool followGaze = true;
    
    private Renderer sphereRenderer;
    private Vector3 currentGazeWorldPosition;
    private bool hasValidGaze = false;

    void Start()
    {
        // Setup visualization object
        sphereRenderer = GetComponent<Renderer>();
        if (sphereRenderer == null)
        {
            // Create a sphere if none exists
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.parent = transform;
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * sphereSize;
            sphereRenderer = sphere.GetComponent<Renderer>();
        }
        
        if (sphereRenderer != null)
        {
            sphereRenderer.material.color = gazeColor;
        }
        
        gameObject.SetActive(false); // Hide until we get data
    }

    /// <summary>
    /// Update the gaze position based on normalized screen coordinates
    /// </summary>
    public void UpdateGaze(Vector2 normalizedGaze, Camera targetCamera = null)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        
        if (targetCamera == null)
            return;

        // Convert normalized gaze [0,1] to screen space
        Vector3 screenPos = new Vector3(
            normalizedGaze.x * Screen.width,
            normalizedGaze.y * Screen.height,
            0f
        );

        // Raycast to find world position
        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            currentGazeWorldPosition = hit.point;
            hasValidGaze = true;
        }
        else
        {
            // No hit - place at fixed distance
            currentGazeWorldPosition = ray.origin + ray.direction * 5f;
            hasValidGaze = true;
        }

        if (followGaze && hasValidGaze)
        {
            transform.position = currentGazeWorldPosition;
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Clear the gaze visualization
    /// </summary>
    public void ClearGaze()
    {
        hasValidGaze = false;
        gameObject.SetActive(false);
    }

    void OnDrawGizmos()
    {
        if (hasValidGaze)
        {
            Gizmos.color = gazeColor;
            Gizmos.DrawWireSphere(currentGazeWorldPosition, sphereSize);
        }
    }
}
