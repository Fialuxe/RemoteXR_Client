// FaceMeshVisualizer.cs
// Visualizes 68 facial landmarks as a 3D mesh in Unity scene
// Shows points and wireframe connections between landmarks
//
// Usage:
// 1. Attach to a GameObject with LslFaceMeshReceiver
// 2. Press Play to see the face mesh visualization

using UnityEngine;

[RequireComponent(typeof(LslFaceMeshReceiver))]
public class FaceMeshVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [Tooltip("Show landmark points")]
    public bool showPoints = true;
    
    [Tooltip("Show wireframe connections")]
    public bool showWireframe = true;
    
    [Tooltip("Size of landmark points")]
    [Range(0.001f, 0.05f)]
    public float pointSize = 0.01f;
    
    [Tooltip("Scale factor for the entire face mesh")]
    [Range(0.1f, 5f)]
    public float meshScale = 2.0f;
    
    [Tooltip("Offset position of the visualization")]
    public Vector3 visualizationOffset = new Vector3(0, 1.5f, 2f);
    
    [Header("Colors")]
    public Color pointColor = Color.green;
    public Color wireframeColor = new Color(0f, 1f, 0f, 0.5f);
    public Color eyeColor = Color.cyan;
    public Color mouthColor = Color.yellow;
    public Color browColor = Color.magenta;
    
    [Header("Flip/Mirror")]
    [Tooltip("Mirror the mesh horizontally (useful for camera view)")]
    public bool mirrorHorizontally = true;
    
    [Tooltip("Flip Y axis")]
    public bool flipVertically = false;
    
    private LslFaceMeshReceiver _receiver;
    private GameObject _meshContainer;
    private GameObject[] _pointObjects;
    private LineRenderer[] _lineRenderers;
    
    // Face contour connections (68-point model)
    private static readonly int[][] FACE_CONNECTIONS = new int[][]
    {
        // Jawline (0-16)
        new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 },
        
        // Right eyebrow (17-21)
        new int[] { 17, 18, 19, 20, 21 },
        
        // Left eyebrow (22-26)
        new int[] { 22, 23, 24, 25, 26 },
        
        // Nose bridge (27-30)
        new int[] { 27, 28, 29, 30 },
        
        // Nose bottom (31-35)
        new int[] { 31, 32, 33, 34, 35, 31 }, // Close loop
        
        // Right eye (36-41)
        new int[] { 36, 37, 38, 39, 40, 41, 36 }, // Close loop
        
        // Left eye (42-47)
        new int[] { 42, 43, 44, 45, 46, 47, 42 }, // Close loop
        
        // Outer mouth (48-59)
        new int[] { 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 48 }, // Close loop
        
        // Inner mouth (60-67)
        new int[] { 60, 61, 62, 63, 64, 65, 66, 67, 60 } // Close loop
    };
    
    void Start()
    {
        _receiver = GetComponent<LslFaceMeshReceiver>();
        
        // Create container for visualization
        _meshContainer = new GameObject("FaceMeshVisualization");
        _meshContainer.transform.SetParent(transform);
        _meshContainer.transform.localPosition = visualizationOffset;
        
        // Create point objects
        _pointObjects = new GameObject[68];
        for (int i = 0; i < 68; i++)
        {
            _pointObjects[i] = CreateLandmarkPoint(i);
        }
        
        // Create line renderers for wireframe
        if (showWireframe)
        {
            CreateWireframe();
        }
        
        Debug.Log("FaceMeshVisualizer: Initialized with 68 landmark points");
    }
    
    void Update()
    {
        if (!_receiver.IsConnected)
            return;
        
        // Update all landmark positions
        for (int i = 0; i < 68; i++)
        {
            if (_receiver.IsLandmarkValid(i))
            {
                Vector3 pos = _receiver.GetLandmark(i);
                
                // Apply transformations
                if (mirrorHorizontally)
                    pos.x = 1f - pos.x; // Mirror X
                
                if (flipVertically)
                    pos.y = 1f - pos.y; // Flip Y
                
                // Center and scale
                pos.x = (pos.x - 0.5f) * meshScale;
                pos.y = (pos.y - 0.5f) * meshScale;
                pos.z = pos.z * meshScale;
                
                _pointObjects[i].transform.localPosition = pos;
                _pointObjects[i].SetActive(showPoints);
            }
            else
            {
                _pointObjects[i].SetActive(false);
            }
        }
        
        // Update wireframe
        if (showWireframe && _lineRenderers != null)
        {
            UpdateWireframe();
        }
    }
    
    GameObject CreateLandmarkPoint(int index)
    {
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = $"Landmark_{index:D2}";
        point.transform.SetParent(_meshContainer.transform);
        point.transform.localScale = Vector3.one * pointSize;
        
        // Remove collider
        Destroy(point.GetComponent<Collider>());
        
        // Set color based on region
        Material mat = point.GetComponent<Renderer>().material;
        
        if (index >= 36 && index <= 47) // Eyes
        {
            mat.color = eyeColor;
        }
        else if (index >= 48 && index <= 67) // Mouth
        {
            mat.color = mouthColor;
        }
        else if (index >= 17 && index <= 26) // Eyebrows
        {
            mat.color = browColor;
        }
        else
        {
            mat.color = pointColor;
        }
        
        return point;
    }
    
    void CreateWireframe()
    {
        _lineRenderers = new LineRenderer[FACE_CONNECTIONS.Length];
        
        for (int i = 0; i < FACE_CONNECTIONS.Length; i++)
        {
            GameObject lineObj = new GameObject($"Line_{i}");
            lineObj.transform.SetParent(_meshContainer.transform);
            
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = 0.002f;
            lr.endWidth = 0.002f;
            lr.positionCount = FACE_CONNECTIONS[i].Length;
            
            // Set color based on region
            if (i >= 5 && i <= 6) // Eyes
            {
                lr.startColor = lr.endColor = eyeColor;
            }
            else if (i >= 7 && i <= 8) // Mouth
            {
                lr.startColor = lr.endColor = mouthColor;
            }
            else if (i >= 1 && i <= 2) // Eyebrows
            {
                lr.startColor = lr.endColor = browColor;
            }
            else
            {
                lr.startColor = lr.endColor = wireframeColor;
            }
            
            _lineRenderers[i] = lr;
        }
    }
    
    void UpdateWireframe()
    {
        for (int i = 0; i < FACE_CONNECTIONS.Length; i++)
        {
            LineRenderer lr = _lineRenderers[i];
            int[] connections = FACE_CONNECTIONS[i];
            
            bool allValid = true;
            for (int j = 0; j < connections.Length; j++)
            {
                int index = connections[j];
                if (!_receiver.IsLandmarkValid(index))
                {
                    allValid = false;
                    break;
                }
            }
            
            lr.enabled = allValid;
            
            if (allValid)
            {
                for (int j = 0; j < connections.Length; j++)
                {
                    int index = connections[j];
                    Vector3 worldPos = _pointObjects[index].transform.position;
                    lr.SetPosition(j, worldPos);
                }
            }
        }
    }
    
    void OnValidate()
    {
        // Update point sizes when changed in inspector
        if (_pointObjects != null)
        {
            foreach (var point in _pointObjects)
            {
                if (point != null)
                {
                    point.transform.localScale = Vector3.one * pointSize;
                }
            }
        }
        
        // Update container position
        if (_meshContainer != null)
        {
            _meshContainer.transform.localPosition = visualizationOffset;
        }
    }
    
    void OnDestroy()
    {
        if (_meshContainer != null)
        {
            Destroy(_meshContainer);
        }
    }
}
