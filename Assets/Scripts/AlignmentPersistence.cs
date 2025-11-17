using UnityEngine;

/// <summary>
/// Centralized persistence utilities for alignment data using PlayerPrefs
/// Handles saving/loading of transforms, offsets, and rotations
/// </summary>
public static class AlignmentPersistence
{
    /// <summary>
    /// Save a Transform (position, rotation, scale) to PlayerPrefs
    /// </summary>
    /// <param name="key">Base key for storage (will append PosX, RotX, etc.)</param>
    /// <param name="transform">Transform to save</param>
    public static void SaveTransform(string key, Transform transform)
    {
        SaveTransform(key, transform.position, transform.rotation, transform.localScale);
    }

    /// <summary>
    /// Save transform data (position, rotation, scale) to PlayerPrefs
    /// </summary>
    public static void SaveTransform(string key, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Position
        PlayerPrefs.SetFloat(key + "PosX", position.x);
        PlayerPrefs.SetFloat(key + "PosY", position.y);
        PlayerPrefs.SetFloat(key + "PosZ", position.z);
        
        // Rotation (as quaternion)
        PlayerPrefs.SetFloat(key + "RotX", rotation.x);
        PlayerPrefs.SetFloat(key + "RotY", rotation.y);
        PlayerPrefs.SetFloat(key + "RotZ", rotation.z);
        PlayerPrefs.SetFloat(key + "RotW", rotation.w);
        
        // Scale
        PlayerPrefs.SetFloat(key + "ScaleX", scale.x);
        PlayerPrefs.SetFloat(key + "ScaleY", scale.y);
        PlayerPrefs.SetFloat(key + "ScaleZ", scale.z);
        
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load a Transform from PlayerPrefs
    /// </summary>
    /// <param name="key">Base key for storage</param>
    /// <param name="position">Output position</param>
    /// <param name="rotation">Output rotation</param>
    /// <param name="scale">Output scale</param>
    /// <returns>True if data was found and loaded</returns>
    public static bool LoadTransform(string key, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        if (PlayerPrefs.HasKey(key + "PosX"))
        {
            position = new Vector3(
                PlayerPrefs.GetFloat(key + "PosX"),
                PlayerPrefs.GetFloat(key + "PosY"),
                PlayerPrefs.GetFloat(key + "PosZ")
            );
            
            rotation = new Quaternion(
                PlayerPrefs.GetFloat(key + "RotX"),
                PlayerPrefs.GetFloat(key + "RotY"),
                PlayerPrefs.GetFloat(key + "RotZ"),
                PlayerPrefs.GetFloat(key + "RotW")
            );
            
            scale = new Vector3(
                PlayerPrefs.GetFloat(key + "ScaleX"),
                PlayerPrefs.GetFloat(key + "ScaleY"),
                PlayerPrefs.GetFloat(key + "ScaleZ")
            );
            
            return true;
        }
        
        position = Vector3.zero;
        rotation = Quaternion.identity;
        scale = Vector3.one;
        return false;
    }

    /// <summary>
    /// Apply loaded transform to a Transform component
    /// </summary>
    public static bool LoadAndApplyTransform(string key, Transform target)
    {
        if (LoadTransform(key, out Vector3 pos, out Quaternion rot, out Vector3 scale))
        {
            target.position = pos;
            target.rotation = rot;
            target.localScale = scale;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Save offset values (position and rotation as euler angles)
    /// </summary>
    /// <param name="key">Base key for storage</param>
    /// <param name="positionOffset">Position offset to save</param>
    /// <param name="rotationEuler">Rotation offset in euler angles</param>
    public static void SaveOffsets(string key, Vector3 positionOffset, Vector3 rotationEuler)
    {
        PlayerPrefs.SetFloat(key + "OffsetX", positionOffset.x);
        PlayerPrefs.SetFloat(key + "OffsetY", positionOffset.y);
        PlayerPrefs.SetFloat(key + "OffsetZ", positionOffset.z);
        
        PlayerPrefs.SetFloat(key + "RotationX", rotationEuler.x);
        PlayerPrefs.SetFloat(key + "RotationY", rotationEuler.y);
        PlayerPrefs.SetFloat(key + "RotationZ", rotationEuler.z);
        
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load offset values
    /// </summary>
    /// <param name="key">Base key for storage</param>
    /// <param name="positionOffset">Output position offset</param>
    /// <param name="rotationEuler">Output rotation offset (euler angles)</param>
    /// <returns>True if data was found and loaded</returns>
    public static bool LoadOffsets(string key, out Vector3 positionOffset, out Vector3 rotationEuler)
    {
        if (PlayerPrefs.HasKey(key + "OffsetX"))
        {
            positionOffset = new Vector3(
                PlayerPrefs.GetFloat(key + "OffsetX"),
                PlayerPrefs.GetFloat(key + "OffsetY"),
                PlayerPrefs.GetFloat(key + "OffsetZ")
            );
            
            rotationEuler = new Vector3(
                PlayerPrefs.GetFloat(key + "RotationX"),
                PlayerPrefs.GetFloat(key + "RotationY"),
                PlayerPrefs.GetFloat(key + "RotationZ")
            );
            
            return true;
        }
        
        positionOffset = Vector3.zero;
        rotationEuler = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Check if saved data exists for a given key
    /// </summary>
    public static bool HasSavedData(string key)
    {
        return PlayerPrefs.HasKey(key + "PosX") || PlayerPrefs.HasKey(key + "OffsetX");
    }

    /// <summary>
    /// Clear saved data for a given key
    /// </summary>
    public static void ClearSavedData(string key)
    {
        // Transform keys
        PlayerPrefs.DeleteKey(key + "PosX");
        PlayerPrefs.DeleteKey(key + "PosY");
        PlayerPrefs.DeleteKey(key + "PosZ");
        PlayerPrefs.DeleteKey(key + "RotX");
        PlayerPrefs.DeleteKey(key + "RotY");
        PlayerPrefs.DeleteKey(key + "RotZ");
        PlayerPrefs.DeleteKey(key + "RotW");
        PlayerPrefs.DeleteKey(key + "ScaleX");
        PlayerPrefs.DeleteKey(key + "ScaleY");
        PlayerPrefs.DeleteKey(key + "ScaleZ");
        
        // Offset keys
        PlayerPrefs.DeleteKey(key + "OffsetX");
        PlayerPrefs.DeleteKey(key + "OffsetY");
        PlayerPrefs.DeleteKey(key + "OffsetZ");
        PlayerPrefs.DeleteKey(key + "RotationX");
        PlayerPrefs.DeleteKey(key + "RotationY");
        PlayerPrefs.DeleteKey(key + "RotationZ");
        
        PlayerPrefs.Save();
    }
}
