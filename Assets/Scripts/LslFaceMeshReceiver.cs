// Simple Unity LSL receiver for FaceMesh landmark data.
// Expects an LSL stream named "FaceMesh" of type "FaceLandmarks" with 30 float channels:
// 10 landmarks × 3 (x,y,z): nose_tip, right_eye, left_eye, mouth_right, mouth_left, 
// chin, forehead, upper_lip, lower_lip, right_cheek
// Attach this to any active GameObject and press Play to see logs in the Console.
//
// Requirements (in your Unity project):
// - Import the LabStreamingLayer C#/Unity bindings so the `LSL` namespace is available
//   (e.g., liblsl-CSharp or LSL4Unity). Ensure the liblsl native DLLs are present for your target platform.

using UnityEngine;
using LSL;

public class LslFaceMeshReceiver : MonoBehaviour
{
    [Header("LSL Stream Settings")]
    public string streamName = "FaceMesh";       // match the Python streamer
    public string streamType = "FaceLandmarks"; // optional fallback lookup
    public int channelCount = 204;               // 68 landmarks × 3 (x,y,z)

    [Header("Timing & Logging")]
    [Tooltip("Seconds to wait when resolving stream before retrying.")]
    public double resolveTimeout = 2.0;
    [Tooltip("Timeout (seconds) for pull_sample; 0 = non-blocking.")]
    public double pullTimeout = 0.0;
    [Tooltip("If true, logs every received sample; otherwise logs at interval.")]
    public bool logEveryFrame = false;
    [Tooltip("Seconds between logs when logEveryFrame is false.")]
    public float logInterval = 5.0f;

    private StreamInlet _inlet;
    private float[] _sample;
    private double _lastTimestamp;
    private float _logTimer;

    public bool IsConnected => _inlet != null;

    // 68-point facial landmark indices (based on standard model)
    // Jawline (0-16)
    public const int JAW_START = 0;
    public const int JAW_END = 16;
    
    // Right eyebrow (17-21)
    public const int RIGHT_BROW_START = 17;
    public const int RIGHT_BROW_END = 21;
    public const int RIGHT_BROW_INNER = 21;
    public const int RIGHT_BROW_OUTER = 17;
    
    // Left eyebrow (22-26)
    public const int LEFT_BROW_START = 22;
    public const int LEFT_BROW_END = 26;
    public const int LEFT_BROW_INNER = 22;
    public const int LEFT_BROW_OUTER = 26;
    
    // Nose bridge (27-30)
    public const int NOSE_BRIDGE_START = 27;
    public const int NOSE_BRIDGE_END = 30;
    public const int NOSE_TIP = 30;
    
    // Nose bottom (31-35)
    public const int NOSE_BOTTOM_START = 31;
    public const int NOSE_BOTTOM_END = 35;
    
    // Right eye (36-41)
    public const int RIGHT_EYE_START = 36;
    public const int RIGHT_EYE_END = 41;
    public const int RIGHT_EYE_OUTER = 36;
    public const int RIGHT_EYE_INNER = 39;
    public const int RIGHT_EYE_TOP = 37;
    public const int RIGHT_EYE_BOTTOM = 40;
    
    // Left eye (42-47)
    public const int LEFT_EYE_START = 42;
    public const int LEFT_EYE_END = 47;
    public const int LEFT_EYE_OUTER = 45;
    public const int LEFT_EYE_INNER = 42;
    public const int LEFT_EYE_TOP = 43;
    public const int LEFT_EYE_BOTTOM = 46;
    
    // Outer lip (48-59)
    public const int OUTER_LIP_START = 48;
    public const int OUTER_LIP_END = 59;
    public const int MOUTH_RIGHT = 48;
    public const int MOUTH_LEFT = 54;
    public const int UPPER_LIP_TOP = 51;
    public const int LOWER_LIP_BOTTOM = 57;
    
    // Inner lip (60-67)
    public const int INNER_LIP_START = 60;
    public const int INNER_LIP_END = 67;
    public const int UPPER_LIP_INNER = 62;
    public const int LOWER_LIP_INNER = 66;
    
    // Convenience aliases
    public const int CHIN = 8;  // Center of jawline
    public const int FOREHEAD = 27;  // Nose bridge top (approximation)

    private void Start()
    {
        TryConnect();
    }

    private void Update()
    {
        if (_inlet == null)
        {
            // Attempt to (re)connect periodically
            TryConnect();
            return;
        }

        if (_sample == null || _sample.Length != channelCount)
            _sample = new float[channelCount];

        // Pull one sample (non-blocking by default)
        double ts = 0.0;
        try
        {
            ts = _inlet.pull_sample(_sample, pullTimeout);
            
            // CRITICAL: Immediately validate ALL raw sample data before any processing
            bool sampleValid = true;
            for (int i = 0; i < _sample.Length; i++)
            {
                // Check for NaN, Infinity, or extreme values
                if (float.IsNaN(_sample[i]) || float.IsInfinity(_sample[i]) || 
                    Mathf.Abs(_sample[i]) > 1e10f)
                {
                    Debug.LogWarning($"LSL FaceMesh received corrupted data in channel {i}: {_sample[i]}. Clearing buffer.");
                    sampleValid = false;
                    break;
                }
                
                // Additional check: x,y coordinates should be in [0,1], z should be reasonable
                if (i % 3 < 2 && (_sample[i] < -10f || _sample[i] > 10f))
                {
                    Debug.LogWarning($"LSL FaceMesh received out-of-range coordinate in channel {i}: {_sample[i]}. Clearing buffer.");
                    sampleValid = false;
                    break;
                }
            }
            
            if (!sampleValid)
            {
                // Clear the sample and skip processing
                System.Array.Clear(_sample, 0, _sample.Length);
                ts = 0.0;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"LSL pull_sample failed: {ex.Message}. Will attempt reconnect.");
            CloseInlet();
            return;
        }

        if (ts != 0.0)
        {
            _lastTimestamp = ts;
            // Logging disabled to prevent crashes on macOS ARM64
            // if (logEveryFrame)
            // {
            //     LogSample(ts);
            // }
        }

        // Logging disabled to prevent crashes on macOS ARM64
        // if (!logEveryFrame && _lastTimestamp != 0.0)
        // {
        //     _logTimer += Time.deltaTime;
        //     if (_logTimer >= logInterval)
        //     {
        //         _logTimer = 0f;
        //         LogSample(_lastTimestamp);
        //     }
        // }
    }

    private void LogSample(double ts)
    {
        // Check if data is valid (not all NaN)
        bool isValid = false;
        for (int i = 0; i < channelCount; i++)
        {
            if (!float.IsNaN(_sample[i]))
            {
                isValid = true;
                break;
            }
        }

        if (!isValid)
        {
            Debug.Log($"LSL FaceMesh [t={ts:F3}] INVALID (NaN)");
            return;
        }

        // Log sample with key landmark data
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"LSL FaceMesh [t={ts:F3}] 68 landmarks received\n");
        
        // Sample some key points
        Vector3 rightEye = GetLandmark(RIGHT_EYE_OUTER);
        Vector3 leftEye = GetLandmark(LEFT_EYE_OUTER);
        Vector3 upperLip = GetLandmark(UPPER_LIP_TOP);
        Vector3 lowerLip = GetLandmark(LOWER_LIP_BOTTOM);
        
        sb.Append($"  Right Eye: {rightEye}\n");
        sb.Append($"  Left Eye: {leftEye}\n");
        sb.Append($"  Upper Lip: {upperLip}\n");
        sb.Append($"  Lower Lip: {lowerLip}\n");
        
        Debug.Log(sb.ToString());
    }

    private void TryConnect()
    {
        try
        {
            // First try by stream name
            var results = LSL.LSL.resolve_stream("name", streamName, 1, resolveTimeout);
            if (results.Length == 0)
            {
                // Fallback: resolve by type
                results = LSL.LSL.resolve_stream("type", streamType, 1, resolveTimeout);
            }

            if (results.Length > 0)
            {
                // Get actual channel count from stream info
                int actualChannelCount = results[0].channel_count();
                
                Debug.Log($"LSL FaceMesh stream found: {results[0].name()} with {actualChannelCount} channels (expected {channelCount})");
                
                // Update channel count if different
                if (actualChannelCount != channelCount)
                {
                    Debug.LogWarning($"Channel count mismatch! Expected {channelCount}, got {actualChannelCount}. Updating to match stream.");
                    channelCount = actualChannelCount;
                }
                
                // CRITICAL FIX: Set max_buflen to 4 seconds (120 samples at 30 FPS).
                // Python server now sends with nominal_srate=30.0 instead of IRREGULAR_RATE (0.0).
                // This prevents buffer allocation issues in liblsl.dylib on Apple M1.
                _inlet = new StreamInlet(results[0], max_buflen: 4, max_chunklen: 1);
                _inlet.open_stream(resolveTimeout);
                
                // CRITICAL: Flush any old/corrupted data from the buffer
                Debug.Log($"Flushing LSL FaceMesh inlet buffer to remove any old data...");
                
                // Pull and discard all samples currently in the buffer
                float[] discardSample = new float[channelCount];
                int flushedCount = 0;
                while (_inlet.pull_sample(discardSample, 0.0) != 0.0)
                {
                    flushedCount++;
                    if (flushedCount > 1000) break; // Safety limit
                }
                Debug.Log($"Flushed {flushedCount} old FaceMesh samples from buffer.");
                
                Debug.Log($"Connected LSL inlet to '{results[0].name()}' (type '{results[0].type()}').");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"LSL resolve/connect failed: {ex.Message}");
            _inlet = null;
        }
    }

    private void OnDisable() => CloseInlet();
    private void OnDestroy() => CloseInlet();

    private void CloseInlet()
    {
        if (_inlet != null)
        {
            try { _inlet.close_stream(); } catch { /* ignore */ }
            _inlet = null;
        }
    }

    // Public accessors for landmark data
    public Vector3 GetLandmark(int index)
    {
        if (_sample == null || index < 0 || index >= 68)
            return Vector3.zero;
        
        // Check if sample array is large enough
        int requiredSize = (index + 1) * 3;
        if (_sample.Length < requiredSize)
            return Vector3.zero;
        
        float x = _sample[index * 3 + 0];
        float y = _sample[index * 3 + 1];
        float z = _sample[index * 3 + 2];
        
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
            return Vector3.zero;
        
        return new Vector3(x, y, z);
    }

    public bool IsLandmarkValid(int index)
    {
        if (_sample == null || index < 0 || index >= 68)
            return false;
        
        // Check if sample array is large enough
        int requiredSize = (index + 1) * 3;
        if (_sample.Length < requiredSize)
            return false;
        
        float x = _sample[index * 3 + 0];
        return !float.IsNaN(x);
    }
    
    // Get average position of a range of landmarks
    public Vector3 GetAverageLandmark(int startIndex, int endIndex)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (IsLandmarkValid(i))
            {
                sum += GetLandmark(i);
                count++;
            }
        }
        
        return count > 0 ? sum / count : Vector3.zero;
    }
}
