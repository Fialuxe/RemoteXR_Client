// Simple Unity LSL receiver for dummy eye gaze data.
// Expects an LSL stream named "EyeGaze" of type "Gaze" with 3 float channels: [gaze_x, gaze_y, pupil].
// Attach this to any active GameObject and press Play to see logs in the Console.
//
// Requirements (in your Unity project):
// - Import the LabStreamingLayer C#/Unity bindings so the `LSL` namespace is available
//   (e.g., liblsl-CSharp or LSL4Unity). Ensure the liblsl native DLLs are present for your target platform.

using UnityEngine;
using LSL;

public class LslGazeReceiver : MonoBehaviour
{
    [Header("LSL Stream Settings")] 
    public string streamName = "EyeGaze";    // match the Python streamer
    public string streamType = "Gaze";       // optional fallback lookup
    public int channelCount = 3;              // [x, y, pupil]

    [Header("Timing & Logging")] 
    [Tooltip("Seconds to wait when resolving stream before retrying.")]
    public double resolveTimeout = 2.0;
    [Tooltip("Timeout (seconds) for pull_sample; 0 = non-blocking.")]
    public double pullTimeout = 0.0;
    [Tooltip("If true, logs every received sample; otherwise logs at interval.")]
    public bool logEveryFrame = true;
    [Tooltip("Seconds between logs when logEveryFrame is false.")]
    public float logInterval = 0.5f;

    private StreamInlet _inlet;
    private float[] _sample;
    private double _lastTimestamp;
    private float _logTimer;

    public bool IsConnected => _inlet != null;

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
                    Debug.LogWarning($"LSL received corrupted data in channel {i}: {_sample[i]}. Clearing buffer.");
                    sampleValid = false;
                    break;
                }
                
                // Additional check: values should be reasonable for normalized coordinates
                // Gaze: x,y in [0,1], blink in {0,1}
                if (i < 2 && (_sample[i] < -10f || _sample[i] > 10f))
                {
                    Debug.LogWarning($"LSL received out-of-range gaze data in channel {i}: {_sample[i]}. Clearing buffer.");
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
            if (logEveryFrame)
            {
                //Debug.Log($"LSL EyeGaze [t={ts:F3}] x={_sample[0]:F3} y={_sample[1]:F3} pupil={_sample[2]:F3}");
                
                // CRITICAL: Final validation before using values
                float x = _sample[0];
                float y = _sample[1];
                
                // Triple-layer validation
                // Layer 1: Type check
                if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                {
                    Debug.LogWarning($"LSL received NaN/Infinity gaze data: x={x}, y={y}. Skipping frame.");
                    return;
                }
                
                // Layer 2: Range check (extended range for safety)
                if (x < -10f || x > 10f || y < -10f || y > 10f)
                {
                    Debug.LogWarning($"LSL received out-of-range gaze data: x={x}, y={y}. Skipping frame.");
                    return;
                }
                
                // Layer 3: Sanity check - values should be close to [0,1] for normalized coordinates
                if (Mathf.Abs(x) > 2f || Mathf.Abs(y) > 2f)
                {
                    Debug.LogWarning($"LSL received suspicious gaze data (far from expected range): x={x}, y={y}. Skipping frame.");
                    return;
                }
                
                // Clamp to valid range [0, 1] as final safety measure
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
                
                // Try to use GazeVisualizationManager first (new system)
                var vizManager = GetComponent<GazeVisualizationManager>();
                if (vizManager != null)
                {
                    vizManager.UpdateGazePosition2D(new Vector2(x, y));
                }
                else
                {
                    // Fallback to legacy Map2DGazeToMesh for backward compatibility
                    var gazeMapper = GetComponent<Map2DGazeToMesh>();
                    if (gazeMapper != null)
                    {
                        gazeMapper.UpdateGazePosition2D(new Vector2(x, y));
                    }
                }
            }
        }

        if (!logEveryFrame && _lastTimestamp != 0.0)
        {
            _logTimer += Time.deltaTime;
            if (_logTimer >= logInterval)
            {
                _logTimer = 0f;
                Debug.Log($"LSL EyeGaze [t={_lastTimestamp:F3}] x={_sample[0]:F3} y={_sample[1]:F3} pupil={_sample[2]:F3}");
            }
        }
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
                // CRITICAL FIX: Set max_buflen to 4 seconds (120 samples at 30 FPS).
                // Python server now sends with nominal_srate=30.0 instead of IRREGULAR_RATE (0.0).
                // This prevents buffer allocation issues in liblsl.dylib on Apple M1.
                _inlet = new StreamInlet(results[0], max_buflen: 4, max_chunklen: 1);
                _inlet.open_stream(resolveTimeout);
                
                // CRITICAL: Flush any old/corrupted data from the buffer
                Debug.Log($"Flushing LSL inlet buffer to remove any old data...");
                
                // Pull and discard all samples currently in the buffer
                float[] discardSample = new float[channelCount];
                int flushedCount = 0;
                while (_inlet.pull_sample(discardSample, 0.0) != 0.0)
                {
                    flushedCount++;
                    if (flushedCount > 1000) break; // Safety limit
                }
                Debug.Log($"Flushed {flushedCount} old samples from buffer.");
                
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
}
