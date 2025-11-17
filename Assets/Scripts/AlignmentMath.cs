using UnityEngine;

/// <summary>
/// Pure math utilities for spatial alignment transformations
/// Handles rigid-body transforms (position + rotation) between coordinate systems
/// </summary>
public static class AlignmentMath
{
    /// <summary>
    /// Compute rotation offset to map from remote frame to local frame
    /// Result: localRot = rotationOffset * remoteRot
    /// </summary>
    /// <param name="localRotation">Q_l - local reference rotation</param>
    /// <param name="remoteRotation">Q_r - remote reference rotation</param>
    /// <returns>Q_l * Q_r^{-1}</returns>
    public static Quaternion ComputeRotationOffset(Quaternion localRotation, Quaternion remoteRotation)
    {
        return localRotation * Quaternion.Inverse(remoteRotation);
    }

    /// <summary>
    /// Transform a position from remote coordinate system to local
    /// Formula: l = O_l + Q_l * Q_r^{-1} * (r - O_r)
    /// </summary>
    /// <param name="remotePosition">r - position in remote world space</param>
    /// <param name="remoteOrigin">O_r - remote reference origin</param>
    /// <param name="remoteRotation">Q_r - remote reference rotation</param>
    /// <param name="localOrigin">O_l - local reference origin</param>
    /// <param name="localRotation">Q_l - local reference rotation</param>
    /// <param name="scale">Optional scale factor (default 1.0)</param>
    /// <returns>Position in local world space</returns>
    public static Vector3 TransformPositionToLocal(
        Vector3 remotePosition,
        Vector3 remoteOrigin,
        Quaternion remoteRotation,
        Vector3 localOrigin,
        Quaternion localRotation,
        float scale = 1f)
    {
        Vector3 deltaFromRemoteOrigin = remotePosition - remoteOrigin;
        Quaternion rotationOffset = ComputeRotationOffset(localRotation, remoteRotation);
        Vector3 rotated = rotationOffset * deltaFromRemoteOrigin;
        if (scale != 1f)
        {
            rotated *= scale;
        }
        return localOrigin + rotated;
    }

    /// <summary>
    /// Transform a rotation from remote coordinate system to local
    /// Formula: localRot = (Q_l * Q_r^{-1}) * remoteRot
    /// </summary>
    public static Quaternion TransformRotationToLocal(
        Quaternion remoteRotation,
        Quaternion remoteReferenceRotation,
        Quaternion localReferenceRotation)
    {
        Quaternion rotationOffset = ComputeRotationOffset(localReferenceRotation, remoteReferenceRotation);
        return rotationOffset * remoteRotation;
    }

    /// <summary>
    /// Transform a position from local coordinate system to remote (inverse operation)
    /// Formula: r = O_r + Q_r * Q_l^{-1} * (l - O_l)
    /// </summary>
    public static Vector3 TransformPositionToRemote(
        Vector3 localPosition,
        Vector3 localOrigin,
        Quaternion localRotation,
        Vector3 remoteOrigin,
        Quaternion remoteRotation,
        float scale = 1f)
    {
        Vector3 deltaFromLocalOrigin = localPosition - localOrigin;
        Quaternion inverseRotationOffset = ComputeRotationOffset(remoteRotation, localRotation);
        Vector3 rotated = inverseRotationOffset * deltaFromLocalOrigin;
        if (scale != 1f)
        {
            rotated /= scale;
        }
        return remoteOrigin + rotated;
    }

    /// <summary>
    /// Transform a rotation from local coordinate system to remote (inverse operation)
    /// </summary>
    public static Quaternion TransformRotationToRemote(
        Quaternion localRotation,
        Quaternion localReferenceRotation,
        Quaternion remoteReferenceRotation)
    {
        Quaternion inverseRotationOffset = ComputeRotationOffset(remoteReferenceRotation, localReferenceRotation);
        return inverseRotationOffset * localRotation;
    }

    /// <summary>
    /// Simple translation-only position transform (for backward compatibility)
    /// </summary>
    public static Vector3 TransformPositionSimple(Vector3 position, Vector3 offset)
    {
        return position + offset;
    }

    /// <summary>
    /// Compute position offset between two reference points (for simple translation-only mode)
    /// </summary>
    public static Vector3 ComputePositionOffset(Vector3 localOrigin, Vector3 remoteOrigin)
    {
        return localOrigin - remoteOrigin;
    }
}
