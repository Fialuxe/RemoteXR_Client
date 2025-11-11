using UnityEngine;
using Photon.Pun;

/// <summary>
/// Interactive tool for VR users to align the scanned mesh with the real world
/// Use controllers or keyboard to move, rotate, and scale the mesh
/// Once aligned, save the transformation for consistent alignment
/// 
/// NOTE: PhotonView Configuration:
/// - This component uses RPC calls only (no continuous synchronization needed)
/// - Add PhotonView component to this GameObject
/// - Observed Components can be left EMPTY - that's correct!
/// - We only send mesh alignment via RPC when user saves
/// </summary>
public class MeshAlignmentTool : MonoBehaviourPunCallbacks
{
    [Header("Mesh to Align")]
    public Transform scannedMesh;
    
    [Header("Alignment Mode")]
    public bool alignmentMode = false;
    [Tooltip("Start in alignment mode on launch")]
    public bool startInAlignmentMode = true;
    
    [Header("Movement Settings")]
    public float moveSpeed = 0.5f;
    public float rotateSpeed = 30f;
    public float scaleSpeed = 0.1f;
    public float fineAdjustMultiplier = 0.1f;
    
    [Header("VR Controller Inputs")]
    [Tooltip("Enable VR controller support")]
    public bool useVRControllers = true;
    
    [Header("Keyboard Controls (Fallback)")]
    public bool useKeyboard = true;
    
    [Header("Visual Feedback")]
    public Material alignmentMaterial;
    public Color alignmentColor = new Color(0, 1, 0, 0.3f);
    public bool showGrid = true;
    public GameObject gridPrefab;
    
    [Header("Persistence")]
    public bool autoSaveOnExit = true;
    public string saveKey = "MeshAlignment_";
    
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Material originalMaterial;
    private GameObject alignmentGrid;
    private bool isFineAdjustMode = false;
    
    // Store the mesh's current transformation
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 savedScale;

    void Start()
    {
        if (scannedMesh == null)
        {
            Debug.LogError("Scanned mesh not assigned to MeshAlignmentTool!");
            return;
        }
        
        // Store original values
        originalPosition = scannedMesh.position;
        originalRotation = scannedMesh.rotation;
        originalScale = scannedMesh.localScale;
        
        // Try to load saved alignment
        LoadAlignment();
        
        // Start in alignment mode if specified
        if (startInAlignmentMode)
        {
            EnableAlignmentMode();
        }
    }

    void Update()
    {
        if (scannedMesh == null) return;
        
        // Toggle alignment mode
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleAlignmentMode();
        }
        
        if (!alignmentMode) return;
        
        // Toggle fine adjust mode
        if (Input.GetKeyDown(KeyCode.F))
        {
            isFineAdjustMode = !isFineAdjustMode;
            Debug.Log($"Fine adjust mode: {(isFineAdjustMode ? "ON" : "OFF")}");
        }
        
        // Handle keyboard controls
        if (useKeyboard)
        {
            HandleKeyboardControls();
        }
        
        // VR controller handling would go here
        if (useVRControllers)
        {
            HandleVRControllers();
        }
    }

    void HandleKeyboardControls()
    {
        float speedMultiplier = isFineAdjustMode ? fineAdjustMultiplier : 1f;
        float deltaTime = Time.deltaTime;
        
        // === POSITION CONTROLS ===
        Vector3 movement = Vector3.zero;
        
        // Numpad for precise movement
        if (Input.GetKey(KeyCode.Keypad8)) // Forward
            movement += Vector3.forward;
        if (Input.GetKey(KeyCode.Keypad2)) // Back
            movement += Vector3.back;
        if (Input.GetKey(KeyCode.Keypad4)) // Left
            movement += Vector3.left;
        if (Input.GetKey(KeyCode.Keypad6)) // Right
            movement += Vector3.right;
        if (Input.GetKey(KeyCode.Keypad9)) // Up
            movement += Vector3.up;
        if (Input.GetKey(KeyCode.Keypad3)) // Down
            movement += Vector3.down;
        
        // Alternative: Arrow keys + modifiers
        if (Input.GetKey(KeyCode.UpArrow))
            movement += Vector3.forward;
        if (Input.GetKey(KeyCode.DownArrow))
            movement += Vector3.back;
        if (Input.GetKey(KeyCode.LeftArrow))
            movement += Vector3.left;
        if (Input.GetKey(KeyCode.RightArrow))
            movement += Vector3.right;
        
        // Vertical movement with Page Up/Down
        if (Input.GetKey(KeyCode.PageUp))
            movement += Vector3.up;
        if (Input.GetKey(KeyCode.PageDown))
            movement += Vector3.down;
        
        if (movement != Vector3.zero)
        {
            scannedMesh.position += movement * moveSpeed * speedMultiplier * deltaTime;
        }
        
        // === ROTATION CONTROLS ===
        Vector3 rotation = Vector3.zero;
        
        // Numpad with Ctrl for rotation
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKey(KeyCode.Keypad4))
                rotation.y -= 1f; // Rotate left
            if (Input.GetKey(KeyCode.Keypad6))
                rotation.y += 1f; // Rotate right
            if (Input.GetKey(KeyCode.Keypad8))
                rotation.x -= 1f; // Pitch down
            if (Input.GetKey(KeyCode.Keypad2))
                rotation.x += 1f; // Pitch up
            if (Input.GetKey(KeyCode.Keypad7))
                rotation.z -= 1f; // Roll left
            if (Input.GetKey(KeyCode.Keypad9))
                rotation.z += 1f; // Roll right
        }
        
        if (rotation != Vector3.zero)
        {
            scannedMesh.Rotate(rotation * rotateSpeed * speedMultiplier * deltaTime, Space.World);
        }
        
        // === SCALE CONTROLS ===
        if (Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals))
        {
            scannedMesh.localScale += Vector3.one * scaleSpeed * speedMultiplier * deltaTime;
        }
        if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))
        {
            scannedMesh.localScale -= Vector3.one * scaleSpeed * speedMultiplier * deltaTime;
            // Prevent negative scale
            scannedMesh.localScale = Vector3.Max(scannedMesh.localScale, Vector3.one * 0.01f);
        }
        
        // === QUICK ACTIONS ===
        
        // Save alignment
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SaveAlignment();
        }
        
        // Reset to original
        if (Input.GetKeyDown(KeyCode.R) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            ResetToOriginal();
        }
        
        // Load saved
        if (Input.GetKeyDown(KeyCode.L) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            LoadAlignment();
        }
        
        // Exit alignment mode
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            DisableAlignmentMode();
        }
    }

    void HandleVRControllers()
    {
        // TODO: Implement VR controller support
        // This is a placeholder for VR controller integration
        // You would check for controller button presses and joystick movements here
        
        // Example structure:
        // OVRInput.Get(OVRInput.Button.PrimaryThumbstick) for movement
        // OVRInput.Get(OVRInput.Button.SecondaryThumbstick) for rotation
        // OVRInput.Get(OVRInput.Button.One) to save
        // etc.
    }

    public void EnableAlignmentMode()
    {
        alignmentMode = true;
        Debug.Log("<color=green>=== MESH ALIGNMENT MODE ENABLED ===</color>");
        Debug.Log("Use numpad or arrow keys to move/rotate the mesh");
        Debug.Log("Press 'M' to exit alignment mode");
        Debug.Log("Press ENTER to save alignment");
        
        // Visual feedback
        if (alignmentMaterial != null)
        {
            // TODO: Apply visual highlight to mesh
        }
        
        if (showGrid && gridPrefab != null)
        {
            alignmentGrid = Instantiate(gridPrefab);
            alignmentGrid.transform.position = scannedMesh.position;
        }
        else if (showGrid)
        {
            CreateSimpleGrid();
        }
        
        // Broadcast to other clients that we're in alignment mode
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("OnAlignmentModeChanged", RpcTarget.Others, true);
        }
    }

    public void DisableAlignmentMode()
    {
        alignmentMode = false;
        Debug.Log("<color=yellow>=== MESH ALIGNMENT MODE DISABLED ===</color>");
        
        if (alignmentGrid != null)
        {
            Destroy(alignmentGrid);
        }
        
        // Broadcast to other clients
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("OnAlignmentModeChanged", RpcTarget.Others, false);
        }
    }

    public void ToggleAlignmentMode()
    {
        if (alignmentMode)
            DisableAlignmentMode();
        else
            EnableAlignmentMode();
    }

    [PunRPC]
    void OnAlignmentModeChanged(bool isEnabled)
    {
        Debug.Log($"Remote user {(isEnabled ? "entered" : "exited")} alignment mode");
    }

    public void SaveAlignment()
    {
        savedPosition = scannedMesh.position;
        savedRotation = scannedMesh.rotation;
        savedScale = scannedMesh.localScale;
        
        // Save to PlayerPrefs
        PlayerPrefs.SetFloat(saveKey + "PosX", savedPosition.x);
        PlayerPrefs.SetFloat(saveKey + "PosY", savedPosition.y);
        PlayerPrefs.SetFloat(saveKey + "PosZ", savedPosition.z);
        
        PlayerPrefs.SetFloat(saveKey + "RotX", savedRotation.x);
        PlayerPrefs.SetFloat(saveKey + "RotY", savedRotation.y);
        PlayerPrefs.SetFloat(saveKey + "RotZ", savedRotation.z);
        PlayerPrefs.SetFloat(saveKey + "RotW", savedRotation.w);
        
        PlayerPrefs.SetFloat(saveKey + "ScaleX", savedScale.x);
        PlayerPrefs.SetFloat(saveKey + "ScaleY", savedScale.y);
        PlayerPrefs.SetFloat(saveKey + "ScaleZ", savedScale.z);
        
        PlayerPrefs.Save();
        
        Debug.Log($"<color=green>✓ Mesh alignment SAVED!</color>");
        Debug.Log($"Position: {savedPosition}");
        Debug.Log($"Rotation: {savedRotation.eulerAngles}");
        Debug.Log($"Scale: {savedScale}");
        
        // Broadcast the new alignment to other clients
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("ReceiveMeshAlignment", RpcTarget.Others,
                savedPosition.x, savedPosition.y, savedPosition.z,
                savedRotation.x, savedRotation.y, savedRotation.z, savedRotation.w,
                savedScale.x, savedScale.y, savedScale.z);
        }
    }

    public void LoadAlignment()
    {
        if (PlayerPrefs.HasKey(saveKey + "PosX"))
        {
            savedPosition = new Vector3(
                PlayerPrefs.GetFloat(saveKey + "PosX"),
                PlayerPrefs.GetFloat(saveKey + "PosY"),
                PlayerPrefs.GetFloat(saveKey + "PosZ")
            );
            
            savedRotation = new Quaternion(
                PlayerPrefs.GetFloat(saveKey + "RotX"),
                PlayerPrefs.GetFloat(saveKey + "RotY"),
                PlayerPrefs.GetFloat(saveKey + "RotZ"),
                PlayerPrefs.GetFloat(saveKey + "RotW")
            );
            
            savedScale = new Vector3(
                PlayerPrefs.GetFloat(saveKey + "ScaleX"),
                PlayerPrefs.GetFloat(saveKey + "ScaleY"),
                PlayerPrefs.GetFloat(saveKey + "ScaleZ")
            );
            
            scannedMesh.position = savedPosition;
            scannedMesh.rotation = savedRotation;
            scannedMesh.localScale = savedScale;
            
            Debug.Log($"<color=green>✓ Mesh alignment LOADED!</color>");
            Debug.Log($"Position: {savedPosition}");
            Debug.Log($"Rotation: {savedRotation.eulerAngles}");
        }
        else
        {
            Debug.Log("<color=yellow>No saved alignment found. Using default position.</color>");
        }
    }

    [PunRPC]
    void ReceiveMeshAlignment(float px, float py, float pz, float rx, float ry, float rz, float rw, float sx, float sy, float sz)
    {
        Vector3 newPosition = new Vector3(px, py, pz);
        Quaternion newRotation = new Quaternion(rx, ry, rz, rw);
        Vector3 newScale = new Vector3(sx, sy, sz);
        
        if (scannedMesh != null)
        {
            scannedMesh.position = newPosition;
            scannedMesh.rotation = newRotation;
            scannedMesh.localScale = newScale;
            
            Debug.Log($"<color=cyan>Received mesh alignment update from VR user</color>");
            Debug.Log($"Position: {newPosition}");
        }
    }

    public void ResetToOriginal()
    {
        scannedMesh.position = originalPosition;
        scannedMesh.rotation = originalRotation;
        scannedMesh.localScale = originalScale;
        
        Debug.Log("<color=yellow>Mesh reset to original position</color>");
    }

    void CreateSimpleGrid()
    {
        alignmentGrid = new GameObject("AlignmentGrid");
        LineRenderer[] lines = new LineRenderer[20];
        
        // Create a simple grid using line renderers
        // This is a basic implementation - you may want to use a more sophisticated grid
        for (int i = 0; i < 10; i++)
        {
            // Horizontal lines
            GameObject hLine = new GameObject($"GridLine_H{i}");
            hLine.transform.SetParent(alignmentGrid.transform);
            LineRenderer lr = hLine.AddComponent<LineRenderer>();
            lr.startWidth = 0.01f;
            lr.endWidth = 0.01f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = alignmentColor;
            lr.SetPosition(0, new Vector3(-5 + i, 0, -5));
            lr.SetPosition(1, new Vector3(-5 + i, 0, 5));
            
            // Vertical lines
            GameObject vLine = new GameObject($"GridLine_V{i}");
            vLine.transform.SetParent(alignmentGrid.transform);
            LineRenderer lr2 = vLine.AddComponent<LineRenderer>();
            lr2.startWidth = 0.01f;
            lr2.endWidth = 0.01f;
            lr2.material = new Material(Shader.Find("Sprites/Default"));
            lr2.startColor = lr2.endColor = alignmentColor;
            lr2.SetPosition(0, new Vector3(-5, 0, -5 + i));
            lr2.SetPosition(1, new Vector3(5, 0, -5 + i));
        }
        
        alignmentGrid.transform.position = scannedMesh.position;
    }

    void OnGUI()
    {
        if (!alignmentMode) return;
        
        // Show alignment UI
        GUILayout.BeginArea(new Rect(10, Screen.height - 350, 500, 340));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== MESH ALIGNMENT MODE ===", GUI.skin.box);
        
        GUILayout.Space(5);
        GUILayout.Label("<b>POSITION:</b> Numpad 8/2/4/6 (or Arrow Keys)");
        GUILayout.Label("<b>HEIGHT:</b> PageUp/PageDown (or Numpad 9/3)");
        GUILayout.Label("<b>ROTATION:</b> CTRL + Numpad");
        GUILayout.Label("<b>SCALE:</b> +/- keys");
        
        GUILayout.Space(5);
        GUILayout.Label($"<b>Fine Adjust:</b> {(isFineAdjustMode ? "ON (F to toggle)" : "OFF (F to toggle)")}");
        
        GUILayout.Space(10);
        GUILayout.Label("--- Current Transform ---", GUI.skin.box);
        GUILayout.Label($"Position: {scannedMesh.position.ToString("F2")}");
        GUILayout.Label($"Rotation: {scannedMesh.rotation.eulerAngles.ToString("F1")}");
        GUILayout.Label($"Scale: {scannedMesh.localScale.ToString("F2")}");
        
        GUILayout.Space(10);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SAVE (Enter)"))
        {
            SaveAlignment();
        }
        if (GUILayout.Button("Reset (Ctrl+R)"))
        {
            ResetToOriginal();
        }
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Saved"))
        {
            LoadAlignment();
        }
        if (GUILayout.Button("Exit (Esc/M)"))
        {
            DisableAlignmentMode();
        }
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void OnApplicationQuit()
    {
        if (autoSaveOnExit && alignmentMode)
        {
            SaveAlignment();
        }
    }

    void OnDestroy()
    {
        if (alignmentGrid != null)
        {
            Destroy(alignmentGrid);
        }
    }
}
