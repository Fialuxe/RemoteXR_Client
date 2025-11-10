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
    public int channelCount = 30;                // 10 landmarks × 3 (x,y,z)

    [Header("Timing & Logging")]
    [Tooltip("Seconds to wait when resolving stream before retrying.")]
    public double resolveTimeout = 2.0;
    [Tooltip("Timeout (seconds) for pull_sample; 0 = non-blocking.")]
    public double pullTimeout = 0.0;
    [Tooltip("If true, logs every received sample; otherwise logs at interval.")]
    public bool logEveryFrame = false;
    [Tooltip("Seconds between logs when logEveryFrame is false.")]
    public float logInterval = 1.0f;

    private StreamInlet _inlet;
    private float[] _sample;
    private double _lastTimestamp;
    private float _logTimer;

    public bool IsConnected => _inlet != null;

    // Landmark indices matching lsl_server.py
    public const int NOSE_TIP = 0;
    public const int RIGHT_EYE = 1;
    public const int LEFT_EYE = 2;
    public const int MOUTH_RIGHT = 3;
    public const int MOUTH_LEFT = 4;
    public const int CHIN = 5;
    public const int FOREHEAD = 6;
    public const int UPPER_LIP = 7;
    public const int LOWER_LIP = 8;
    public const int RIGHT_CHEEK = 9;

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
            if (logEveryFrame)
            {
                LogSample(ts);
            }
        }

        if (!logEveryFrame && _lastTimestamp != 0.0)
        {
            _logTimer += Time.deltaTime;
            if (_logTimer >= logInterval)
            {
                _logTimer = 0f;
                LogSample(_lastTimestamp);
            }
        }
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

        // Log sample with landmark data
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"LSL FaceMesh [t={ts:F3}]\n");
        
        string[] names = { "nose_tip", "right_eye", "left_eye", "mouth_right", "mouth_left", 
                          "chin", "forehead", "upper_lip", "lower_lip", "right_cheek" };
        
        for (int i = 0; i < 10; i++)
        {
            float x = _sample[i * 3 + 0];
            float y = _sample[i * 3 + 1];
            float z = _sample[i * 3 + 2];
            
            if (!float.IsNaN(x) && !float.IsNaN(y) && !float.IsNaN(z))
            {
                sb.Append($"  {names[i]}: x={x:F3} y={y:F3} z={z:F3}\n");
            }
        }
        
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
                _inlet = new StreamInlet(results[0], max_buflen: 360, max_chunklen: channelCount);
                _inlet.open_stream(resolveTimeout);
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
        if (_sample == null || index < 0 || index >= 10)
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
        if (_sample == null || index < 0 || index >= 10)
            return false;
        
        float x = _sample[index * 3 + 0];
        return !float.IsNaN(x);
    }
}
