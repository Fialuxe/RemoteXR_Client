using UnityEngine;
using Photon.Pun;

/// <summary>
/// Tool for easy spatial alignment calibration
/// Attach this to both projects and use keyboard shortcuts to align
/// </summary>
public class AlignmentCalibrationTool : MonoBehaviourPunCallbacks
{
    [Header("References")]
    public Transform scannedMesh;
    public SpatialAlignmentManager alignmentManager;
    
    [Header("Calibration Points")]
    [Tooltip("Place markers at known positions in your scanned mesh")]
    public Transform[] calibrationMarkers;
    
    [Header("Quick Alignment")]
    [Tooltip("Enable to use keyboard shortcuts for manual adjustment")]
    public bool enableKeyboardAdjustment = true;
    public float adjustmentStep = 0.1f;
    public float rotationStep = 5f;
    
    private Vector3 manualOffset = Vector3.zero;
    private Vector3 manualRotation = Vector3.zero;
    
    void Start()
    {
        if (alignmentManager == null)
            alignmentManager = FindFirstObjectByType<SpatialAlignmentManager>();
        
        if (scannedMesh != null)
        {
            Debug.Log($"<color=cyan>Scanned Mesh Position: {scannedMesh.position}</color>");
            Debug.Log($"<color=cyan>Scanned Mesh Rotation: {scannedMesh.rotation.eulerAngles}</color>");
        }
    }

    void Update()
    {
        if (!enableKeyboardAdjustment) return;
        
        bool changed = false;
        
        // Position adjustments (Arrow keys + modifiers)
        if (Input.GetKey(KeyCode.LeftShift))
        {
            // X-axis
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                manualOffset.x -= adjustmentStep;
                changed = true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                manualOffset.x += adjustmentStep;
                changed = true;
            }
            
            // Z-axis
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                manualOffset.z += adjustmentStep;
                changed = true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                manualOffset.z -= adjustmentStep;
                changed = true;
            }
            
            // Y-axis
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                manualOffset.y += adjustmentStep;
                changed = true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                manualOffset.y -= adjustmentStep;
                changed = true;
            }
        }
        
        // Rotation adjustments (Ctrl + Arrow keys)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                manualRotation.y -= rotationStep;
                changed = true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                manualRotation.y += rotationStep;
                changed = true;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                manualRotation.x -= rotationStep;
                changed = true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                manualRotation.x += rotationStep;
                changed = true;
            }
        }
        
        // Reset alignment
        if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
        {
            manualOffset = Vector3.zero;
            manualRotation = Vector3.zero;
            changed = true;
            Debug.Log("<color=yellow>Alignment reset!</color>");
        }
        
        // Save alignment
        if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl))
        {
            SaveAlignment();
        }
        
        // Load alignment
        if (Input.GetKeyDown(KeyCode.L) && Input.GetKey(KeyCode.LeftControl))
        {
            LoadAlignment();
        }
        
        if (changed && alignmentManager != null)
        {
            alignmentManager.SetManualAlignment(manualOffset, manualRotation);
            Debug.Log($"<color=cyan>Alignment adjusted - Offset: {manualOffset}, Rotation: {manualRotation}</color>");
        }
    }

    void OnGUI()
    {
        if (!enableKeyboardAdjustment) return;
        
        GUILayout.BeginArea(new Rect(10, 520, 400, 300));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== ALIGNMENT CALIBRATION ===", GUI.skin.box);
        GUILayout.Label("SHIFT + Arrows: Move X/Z");
        GUILayout.Label("SHIFT + PgUp/PgDn: Move Y");
        GUILayout.Label("CTRL + Arrows: Rotate");
        GUILayout.Label("CTRL + R: Reset");
        GUILayout.Label("CTRL + S: Save alignment");
        GUILayout.Label("CTRL + L: Load alignment");
        
        GUILayout.Space(10);
        GUILayout.Label($"Current Offset: {manualOffset.ToString("F2")}");
        GUILayout.Label($"Current Rotation: {manualRotation.ToString("F1")}");
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Auto-Align from Mesh"))
        {
            AutoAlignFromMesh();
        }
        
        if (GUILayout.Button("Recalibrate with Remote"))
        {
            if (alignmentManager != null)
                alignmentManager.RecalibrateAlignment();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void AutoAlignFromMesh()
    {
        if (scannedMesh == null)
        {
            Debug.LogWarning("No scanned mesh assigned!");
            return;
        }
        
        if (alignmentManager != null)
        {
            alignmentManager.meshReferencePoint = scannedMesh;
            alignmentManager.RecalibrateAlignment();
            Debug.Log("<color=green>Auto-aligned to scanned mesh!</color>");
        }
    }

    void SaveAlignment()
    {
        AlignmentPersistence.SaveOffsets("AlignmentCalibration_", manualOffset, manualRotation);
        Debug.Log("<color=green>Alignment saved!</color>");
    }

    void LoadAlignment()
    {
        if (AlignmentPersistence.LoadOffsets("AlignmentCalibration_", out Vector3 offset, out Vector3 rotation))
        {
            manualOffset = offset;
            manualRotation = rotation;
            
            if (alignmentManager != null)
            {
                alignmentManager.SetManualAlignment(manualOffset, manualRotation);
            }
            
            Debug.Log("<color=green>Alignment loaded!</color>");
        }
        else
        {
            Debug.LogWarning("No saved alignment found!");
        }
    }

    /// <summary>
    /// Use this to align based on calibration markers in both scenes
    /// </summary>
    public void AlignFromMarkers()
    {
        if (calibrationMarkers == null || calibrationMarkers.Length < 3)
        {
            Debug.LogWarning("Need at least 3 calibration markers!");
            return;
        }
        
        // Calculate alignment based on marker positions
        // This is a simplified version - for production you'd want more sophisticated alignment
        Vector3 centerPoint = Vector3.zero;
        foreach (var marker in calibrationMarkers)
        {
            if (marker != null)
                centerPoint += marker.position;
        }
        centerPoint /= calibrationMarkers.Length;
        
        Debug.Log($"<color=cyan>Marker-based alignment center: {centerPoint}</color>");
        
        // Share this with the alignment manager
        if (alignmentManager != null && alignmentManager.meshReferencePoint != null)
        {
            manualOffset = centerPoint - alignmentManager.meshReferencePoint.position;
            alignmentManager.SetManualAlignment(manualOffset, manualRotation);
        }
    }
}
