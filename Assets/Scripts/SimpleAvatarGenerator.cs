// SimpleAvatarGenerator.cs
// Creates a simple procedural avatar with blend shapes for facial expressions.
// Attach to an empty GameObject and click "Generate Avatar" in the Inspector.
//
// This generates:
// - Head with facial blend shapes (eye blink, mouth open, etc.)
// - Body, arms, legs
// - Properly configured for FaceMeshToExpression

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimpleAvatarGenerator : MonoBehaviour
{
    [Header("Avatar Settings")]
    public string avatarName = "SimpleAvatar";
    public Material avatarMaterial;
    
    [Header("Generation")]
    [Tooltip("Click to generate the avatar")]
    public bool generateAvatar = false;
    
    private GameObject _generatedAvatar;
    
    public void GenerateAvatar()
    {
        // Clean up existing avatar
        if (_generatedAvatar != null)
        {
            DestroyImmediate(_generatedAvatar);
        }
        
        // Create root object
        _generatedAvatar = new GameObject(avatarName);
        _generatedAvatar.transform.SetParent(transform);
        _generatedAvatar.transform.localPosition = Vector3.zero;
        
        // Create material if not assigned
        Material mat = avatarMaterial;
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.8f, 0.7f); // Skin tone
        }
        
        // --- Head ---
        GameObject head = CreateHead(_generatedAvatar.transform, mat);
        
        // --- Body ---
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(_generatedAvatar.transform);
        body.transform.localPosition = new Vector3(0, -0.6f, 0);
        body.transform.localScale = new Vector3(0.4f, 0.6f, 0.3f);
        body.GetComponent<Renderer>().material = mat;
        
        // --- Arms ---
        CreateArm(_generatedAvatar.transform, mat, "LeftArm", new Vector3(-0.3f, -0.4f, 0));
        CreateArm(_generatedAvatar.transform, mat, "RightArm", new Vector3(0.3f, -0.4f, 0));
        
        // --- Legs ---
        CreateLeg(_generatedAvatar.transform, mat, "LeftLeg", new Vector3(-0.1f, -1.2f, 0));
        CreateLeg(_generatedAvatar.transform, mat, "RightLeg", new Vector3(0.1f, -1.2f, 0));
        
        Debug.Log($"Generated avatar '{avatarName}' with facial blend shapes!");
        
#if UNITY_EDITOR
        // Save as prefab
        string prefabPath = $"Assets/Prefabs/Avatar/{avatarName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(_generatedAvatar, prefabPath);
        Debug.Log($"Saved avatar prefab to {prefabPath}");
#endif
    }
    
    GameObject CreateHead(Transform parent, Material mat)
    {
        GameObject head = new GameObject("Head");
        head.transform.SetParent(parent);
        head.transform.localPosition = new Vector3(0, 0.2f, 0);
        
        // Create mesh with blend shapes
        MeshFilter meshFilter = head.AddComponent<MeshFilter>();
        SkinnedMeshRenderer renderer = head.AddComponent<SkinnedMeshRenderer>();
        
        // Generate head mesh with blend shapes
        Mesh headMesh = CreateHeadMeshWithBlendShapes();
        meshFilter.sharedMesh = headMesh;
        renderer.sharedMesh = headMesh;
        renderer.material = mat;
        
        // Add eyes
        CreateEye(head.transform, mat, "LeftEye", new Vector3(-0.1f, 0.05f, 0.18f));
        CreateEye(head.transform, mat, "RightEye", new Vector3(0.1f, 0.05f, 0.18f));
        
        return head;
    }
    
    Mesh CreateHeadMeshWithBlendShapes()
    {
        // Create a sphere-like head mesh
        Mesh mesh = new Mesh();
        mesh.name = "HeadMesh";
        
        // Simple sphere approximation (use Unity's sphere for base)
        GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh baseMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tempSphere);
        
        // Copy base mesh
        Vector3[] baseVertices = baseMesh.vertices;
        int[] triangles = baseMesh.triangles;
        Vector3[] normals = baseMesh.normals;
        Vector2[] uv = baseMesh.uv;
        
        mesh.vertices = baseVertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.uv = uv;
        
        // Create blend shapes for expressions
        
        // 1. Left Eye Blink
        Vector3[] leftEyeBlinkDelta = new Vector3[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            // Vertices near left eye (x < 0, y > 0, z > 0.3)
            if (v.x < -0.05f && v.y > 0.0f && v.z > 0.3f)
            {
                leftEyeBlinkDelta[i] = new Vector3(0, -0.15f, -0.05f); // Close eye
            }
        }
        mesh.AddBlendShapeFrame("eye_blink_left", 100f, leftEyeBlinkDelta, null, null);
        
        // 2. Right Eye Blink
        Vector3[] rightEyeBlinkDelta = new Vector3[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            // Vertices near right eye (x > 0, y > 0, z > 0.3)
            if (v.x > 0.05f && v.y > 0.0f && v.z > 0.3f)
            {
                rightEyeBlinkDelta[i] = new Vector3(0, -0.15f, -0.05f); // Close eye
            }
        }
        mesh.AddBlendShapeFrame("eye_blink_right", 100f, rightEyeBlinkDelta, null, null);
        
        // 3. Mouth Open
        Vector3[] mouthOpenDelta = new Vector3[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            // Vertices near mouth (y < 0, z > 0.3)
            if (v.y < -0.1f && v.y > -0.3f && v.z > 0.3f)
            {
                mouthOpenDelta[i] = new Vector3(0, -0.2f, 0.05f); // Open mouth down
            }
        }
        mesh.AddBlendShapeFrame("mouth_open", 100f, mouthOpenDelta, null, null);
        
        // 4. Jaw Open
        Vector3[] jawOpenDelta = new Vector3[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            // Lower half of face
            if (v.y < 0)
            {
                float factor = Mathf.Clamp01(-v.y / 0.5f);
                jawOpenDelta[i] = new Vector3(0, -0.3f * factor, 0);
            }
        }
        mesh.AddBlendShapeFrame("jaw_open", 100f, jawOpenDelta, null, null);
        
        // 5. Brow Raise
        Vector3[] browRaiseDelta = new Vector3[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            // Vertices at top front (y > 0.3, z > 0)
            if (v.y > 0.3f && v.z > 0)
            {
                browRaiseDelta[i] = new Vector3(0, 0.15f, 0.05f);
            }
        }
        mesh.AddBlendShapeFrame("brow_raise", 100f, browRaiseDelta, null, null);
        
        return mesh;
    }
    
    void CreateEye(Transform parent, Material mat, string name, Vector3 localPos)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = name;
        eye.transform.SetParent(parent);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        
        Material eyeMat = new Material(Shader.Find("Standard"));
        eyeMat.color = Color.black;
        eye.GetComponent<Renderer>().material = eyeMat;
    }
    
    void CreateArm(Transform parent, Material mat, string name, Vector3 localPos)
    {
        GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        arm.name = name;
        arm.transform.SetParent(parent);
        arm.transform.localPosition = localPos;
        arm.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
        arm.GetComponent<Renderer>().material = mat;
    }
    
    void CreateLeg(Transform parent, Material mat, string name, Vector3 localPos)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leg.name = name;
        leg.transform.SetParent(parent);
        leg.transform.localPosition = localPos;
        leg.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
        leg.GetComponent<Renderer>().material = mat;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimpleAvatarGenerator))]
public class SimpleAvatarGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        SimpleAvatarGenerator generator = (SimpleAvatarGenerator)target;
        
        if (GUILayout.Button("Generate Avatar", GUILayout.Height(30)))
        {
            generator.GenerateAvatar();
        }
    }
}
#endif
