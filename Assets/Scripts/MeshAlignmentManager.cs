using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

/// <summary>
/// Manages spatial mesh alignment between remote and local clients.
/// Allows interactive adjustment of mesh position, rotation, and scale.
/// Synchronizes alignment across network via Photon.
/// </summary>
public class MeshAlignmentManager : MonoBehaviourPunCallbacks
{
    [Header("Mesh Reference")]
    public Transform meshToAlign;
    
    [Header("Alignment Controls")]
    public bool alignmentModeEnabled = false;
    public KeyCode toggleAlignmentKey = KeyCode.M;
    
    [Header("VR Local Worker Mode")]
    [Tooltip("Enable this for VR local worker to use controllers for mesh alignment")]
    public bool useVRControllers = false;
    
    [Header("VR Controller Settings")]
    [Tooltip("Which controller to use for mesh manipulation (Left or Right)")]
    public XRNode controllerNode = XRNode.RightHand;
    [Tooltip("Speed multiplier for controller-based adjustments")]
    public float vrAdjustSpeed = 1f;
    [Tooltip("Grip button threshold for enabling adjustment mode")]
    public float gripThreshold = 0.5f;
    
    [Header("Adjustment Speeds")]
    public float moveSpeed = 0.5f;
    public float rotateSpeed = 30f;
    public float scaleSpeed = 0.1f;
    public float fineAdjustMultiplier = 0.1f;
    
    [Header("Persistence")]
    public string saveKey = "MeshAlignment_";
    
    private bool isFineAdjustMode = false;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 savedScale;

    void Start()
    {
        if (meshToAlign == null)
        {
            Debug.LogWarning("MeshAlignmentManager: No mesh assigned. Searching for mesh in scene...");
            // Try to find a mesh in the scene
            GameObject meshObj = GameObject.Find("ScannedRoom") ?? GameObject.Find("Mesh");
            if (meshObj != null)
                meshToAlign = meshObj.transform;
        }
        
        if (meshToAlign != null)
            LoadAlignment();
    }

    void Update()
    {
        if (meshToAlign == null)
            return;

        // Toggle alignment mode (keyboard only for remote, VR uses manual toggle)
        if (!useVRControllers && Input.GetKeyDown(toggleAlignmentKey))
        {
            alignmentModeEnabled = !alignmentModeEnabled;
            Debug.Log($"<color={(alignmentModeEnabled ? "green" : "yellow")}>Mesh alignment mode: {(alignmentModeEnabled ? "ON" : "OFF")}</color>");
        }

        if (!alignmentModeEnabled)
            return;

        // Toggle fine adjust (keyboard only)
        if (!useVRControllers && Input.GetKeyDown(KeyCode.F))
        {
            isFineAdjustMode = !isFineAdjustMode;
            Debug.Log($"Fine adjust: {(isFineAdjustMode ? "ON" : "OFF")}");
        }

        if (useVRControllers)
            HandleVRControllerInput();
        else
            HandleAlignmentInput();
    }

    void HandleVRControllerInput()
    {
        // VR controller input handling for local worker
        float dt = Time.deltaTime;
        
        // Get the input device for the selected controller
        InputDevice controller = InputDevices.GetDeviceAtXRNode(controllerNode);
        
        if (!controller.isValid)
        {
            Debug.LogWarning("VR Controller not found or not valid. Make sure XR is initialized.");
            return;
        }
        
        // Check grip button to enable/disable alignment mode dynamically
        if (controller.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            // If grip is pressed above threshold, enable alignment
            bool wasEnabled = alignmentModeEnabled;
            alignmentModeEnabled = (gripValue > gripThreshold);
            
            if (alignmentModeEnabled && !wasEnabled)
            {
                Debug.Log("<color=green>VR Mesh alignment mode: ON (Grip pressed)</color>");
            }
            else if (!alignmentModeEnabled && wasEnabled)
            {
                Debug.Log("<color=yellow>VR Mesh alignment mode: OFF (Grip released)</color>");
            }
        }
        
        if (!alignmentModeEnabled)
            return;
        
        // === POSITION CONTROL ===
        // Use primary 2D axis (thumbstick) for XZ movement
        if (controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 primary2DAxis))
        {
            if (primary2DAxis.magnitude > 0.1f) // Dead zone
            {
                Vector3 movement = new Vector3(primary2DAxis.x, 0f, primary2DAxis.y);
                meshToAlign.position += movement * moveSpeed * vrAdjustSpeed * dt;
            }
        }
        
        // Use trigger for vertical movement (Y axis)
        if (controller.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            if (triggerValue > 0.1f) // Dead zone
            {
                // Trigger pressed = move up
                meshToAlign.position += Vector3.up * triggerValue * moveSpeed * vrAdjustSpeed * dt;
            }
        }
        
        // Use secondary 2D axis for vertical movement (alternative - move down)
        if (controller.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 secondary2DAxis))
        {
            if (Mathf.Abs(secondary2DAxis.y) > 0.1f) // Dead zone
            {
                // Thumbstick Y axis for up/down
                meshToAlign.position += Vector3.up * secondary2DAxis.y * moveSpeed * vrAdjustSpeed * dt;
            }
            
            // Use secondary thumbstick X for rotation around Y axis
            if (Mathf.Abs(secondary2DAxis.x) > 0.1f)
            {
                meshToAlign.Rotate(Vector3.up, secondary2DAxis.x * rotateSpeed * vrAdjustSpeed * dt, Space.World);
            }
        }
        
        // === ROTATION CONTROL ===
        // Use primary button (A/X) + secondary button (B/Y) for rotation
        bool primaryButtonPressed = false;
        bool secondaryButtonPressed = false;
        
        if (controller.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonPressed) && primaryButtonPressed)
        {
            // Primary button + thumbstick for pitch/yaw rotation
            if (controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rotAxis))
            {
                if (rotAxis.magnitude > 0.1f)
                {
                    Vector3 rotation = new Vector3(-rotAxis.y, rotAxis.x, 0f);
                    meshToAlign.Rotate(rotation * rotateSpeed * vrAdjustSpeed * dt, Space.World);
                }
            }
        }
        
        if (controller.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButtonPressed) && secondaryButtonPressed)
        {
            // Secondary button + thumbstick for roll rotation
            if (controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rollAxis))
            {
                if (Mathf.Abs(rollAxis.x) > 0.1f)
                {
                    meshToAlign.Rotate(Vector3.forward, rollAxis.x * rotateSpeed * vrAdjustSpeed * dt, Space.World);
                }
            }
        }
        
        // === SCALE CONTROL ===
        // Use D-pad up/down for scaling
        if (controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 dpadAxis))
        {
            // Check if primary button is NOT pressed (to avoid conflict with rotation)
            if (!primaryButtonPressed && !secondaryButtonPressed)
            {
                // Use thumbstick up/down for fine scale adjustment
                if (Mathf.Abs(dpadAxis.y) > 0.8f) // Only at extreme positions
                {
                    float scaleChange = dpadAxis.y * scaleSpeed * vrAdjustSpeed * dt;
                    meshToAlign.localScale += Vector3.one * scaleChange;
                    meshToAlign.localScale = Vector3.Max(meshToAlign.localScale, Vector3.one * 0.01f);
                }
            }
        }
        
        // === SAVE/LOAD ===
        // Use menu button to save
        if (controller.TryGetFeatureValue(CommonUsages.menuButton, out bool menuPressed) && menuPressed)
        {
            SaveAlignment();
            BroadcastAlignment();
            Debug.Log("<color=green>VR: Alignment saved and broadcasted!</color>");
        }
    }
    
    void HandleAlignmentInput()
    {
        float speed = isFineAdjustMode ? fineAdjustMultiplier : 1f;
        float dt = Time.deltaTime;

        // === POSITION ===
        Vector3 movement = Vector3.zero;
        
        if (Input.GetKey(KeyCode.Keypad8) || Input.GetKey(KeyCode.UpArrow))
            movement += Vector3.forward;
        if (Input.GetKey(KeyCode.Keypad2) || Input.GetKey(KeyCode.DownArrow))
            movement += Vector3.back;
        if (Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.LeftArrow))
            movement += Vector3.left;
        if (Input.GetKey(KeyCode.Keypad6) || Input.GetKey(KeyCode.RightArrow))
            movement += Vector3.right;
        if (Input.GetKey(KeyCode.Keypad9) || Input.GetKey(KeyCode.PageUp))
            movement += Vector3.up;
        if (Input.GetKey(KeyCode.Keypad3) || Input.GetKey(KeyCode.PageDown))
            movement += Vector3.down;

        if (movement != Vector3.zero)
            meshToAlign.position += movement * moveSpeed * speed * dt;

        // === ROTATION ===
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            Vector3 rotation = Vector3.zero;
            
            if (Input.GetKey(KeyCode.Keypad4))
                rotation.y -= 1f;
            if (Input.GetKey(KeyCode.Keypad6))
                rotation.y += 1f;
            if (Input.GetKey(KeyCode.Keypad8))
                rotation.x -= 1f;
            if (Input.GetKey(KeyCode.Keypad2))
                rotation.x += 1f;
            if (Input.GetKey(KeyCode.Keypad7))
                rotation.z -= 1f;
            if (Input.GetKey(KeyCode.Keypad9))
                rotation.z += 1f;

            if (rotation != Vector3.zero)
                meshToAlign.Rotate(rotation * rotateSpeed * speed * dt, Space.World);
        }

        // === SCALE ===
        if (Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals))
        {
            meshToAlign.localScale += Vector3.one * scaleSpeed * speed * dt;
        }
        if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))
        {
            meshToAlign.localScale -= Vector3.one * scaleSpeed * speed * dt;
            meshToAlign.localScale = Vector3.Max(meshToAlign.localScale, Vector3.one * 0.01f);
        }

        // === SAVE/LOAD ===
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SaveAlignment();
            BroadcastAlignment();
        }
        
        if (Input.GetKeyDown(KeyCode.L) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            LoadAlignment();
        }
    }

    public void SaveAlignment()
    {
        if (meshToAlign == null)
            return;

        savedPosition = meshToAlign.position;
        savedRotation = meshToAlign.rotation;
        savedScale = meshToAlign.localScale;

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
        
        Debug.Log($"<color=green>✓ Mesh alignment saved!</color>\nPos: {savedPosition}\nRot: {savedRotation.eulerAngles}\nScale: {savedScale}");
    }

    public void LoadAlignment()
    {
        if (meshToAlign == null)
            return;

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
            
            meshToAlign.position = savedPosition;
            meshToAlign.rotation = savedRotation;
            meshToAlign.localScale = savedScale;
            
            Debug.Log($"<color=green>✓ Mesh alignment loaded!</color>");
        }
    }

    void BroadcastAlignment()
    {
        if (!PhotonNetwork.InRoom || photonView == null)
            return;

        photonView.RPC("ReceiveAlignment", RpcTarget.Others,
            savedPosition.x, savedPosition.y, savedPosition.z,
            savedRotation.x, savedRotation.y, savedRotation.z, savedRotation.w,
            savedScale.x, savedScale.y, savedScale.z);
    }

    [PunRPC]
    void ReceiveAlignment(float px, float py, float pz, float rx, float ry, float rz, float rw, float sx, float sy, float sz)
    {
        if (meshToAlign == null)
            return;

        meshToAlign.position = new Vector3(px, py, pz);
        meshToAlign.rotation = new Quaternion(rx, ry, rz, rw);
        meshToAlign.localScale = new Vector3(sx, sy, sz);
        
        Debug.Log("<color=cyan>Received mesh alignment from remote user</color>");
    }

    void OnGUI()
    {
        if (!alignmentModeEnabled || meshToAlign == null)
            return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 280, 400, 270));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label(useVRControllers ? "=== VR MESH ALIGNMENT MODE ===" : "=== MESH ALIGNMENT MODE ===", GUI.skin.box);
        GUILayout.Space(5);
        
        if (useVRControllers)
        {
            GUILayout.Label("VR Controller Mode Active");
            GUILayout.Space(3);
            GUILayout.Label("GRIP: Hold to enable alignment");
            GUILayout.Label("THUMBSTICK: Move mesh (XZ plane)");
            GUILayout.Label("TRIGGER: Move mesh up");
            GUILayout.Label("2ND THUMBSTICK Y: Move up/down");
            GUILayout.Label("2ND THUMBSTICK X: Rotate Y-axis");
            GUILayout.Label("A/X BUTTON + STICK: Rotate pitch/yaw");
            GUILayout.Label("B/Y BUTTON + STICK: Rotate roll");
            GUILayout.Label("STICK EXTREMES: Scale");
            GUILayout.Label("MENU BUTTON: Save alignment");
        }
        else
        {
            GUILayout.Label($"Fine Adjust (F): {(isFineAdjustMode ? "ON" : "OFF")}");
        }
        GUILayout.Space(5);
        
        if (!useVRControllers)
        {
            GUILayout.Label("POSITION: Arrow Keys / Numpad 8,2,4,6");
            GUILayout.Label("HEIGHT: PageUp/Down / Numpad 9,3");
            GUILayout.Label("ROTATION: CTRL + Numpad");
            GUILayout.Label("SCALE: +/- keys");
        }
        GUILayout.Space(10);
        GUILayout.Label($"Position: {meshToAlign.position.ToString("F2")}");
        GUILayout.Label($"Rotation: {meshToAlign.rotation.eulerAngles.ToString("F1")}");
        GUILayout.Label($"Scale: {meshToAlign.localScale.ToString("F2")}");
        GUILayout.Space(10);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SAVE (Enter)"))
        {
            SaveAlignment();
            BroadcastAlignment();
        }
        if (GUILayout.Button("LOAD (Ctrl+L)"))
            LoadAlignment();
        if (GUILayout.Button("EXIT (M)"))
            alignmentModeEnabled = false;
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
