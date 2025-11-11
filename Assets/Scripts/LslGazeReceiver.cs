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
                var gazeMapper = GetComponent<Map2DGazeToMesh>();
                if (gazeMapper != null)
                {
                    gazeMapper.UpdateGazePosition2D(new Vector2(_sample[0], _sample[1]));
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
}
