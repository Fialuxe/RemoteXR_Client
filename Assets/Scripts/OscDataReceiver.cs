using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Receives face mesh and gaze data via OSC (Open Sound Control) protocol.
/// Replaces the glitchy LSL implementation with a more reliable UDP-based solution.
/// 
/// OSC Message Format:
/// - /gaze x y pupil (3 floats: normalized gaze position + pupil size)
/// - /facemesh landmark_index x y z (68 messages per frame, each with 4 values)
/// </summary>
public class OscDataReceiver : MonoBehaviour
{
    [Header("OSC Settings")]
    [Tooltip("Port to listen on for OSC messages")]
    public int receivePort = 8000;
    
    [Header("Test Mode")]
    [Tooltip("Generate fake data for testing without Python server")]
    public bool useFakeData = false;
    [Tooltip("Update rate for fake data (Hz)")]
    public float fakeDataRate = 30f;
    
    [Header("Face Mesh Data")]
    [Tooltip("68 facial landmarks (standard model)")]
    private Vector3[] faceLandmarks = new Vector3[68];
    private bool hasFaceData = false;
    private float lastFaceUpdateTime = 0f;
    
    [Header("Gaze Data")]
    private Vector2 gazePosition = Vector2.zero;
    private float pupilSize = 0f;
    private bool hasGazeData = false;
    private float lastGazeUpdateTime = 0f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Fake data generation
    private float fakeDataTimer = 0f;
    private float fakeGazeAngle = 0f;
    
    private UdpClient udpClient;
    private bool isRunning = false;
    
    // Timeout for data staleness (in seconds)
    private const float DATA_TIMEOUT = 2f;

    void Start()
    {
        StartOscReceiver();
    }

    void StartOscReceiver()
    {
        try
        {
            udpClient = new UdpClient(receivePort);
            udpClient.Client.ReceiveTimeout = 100; // Non-blocking with timeout
            isRunning = true;
            
            Debug.Log($"<color=green>OSC Receiver started on port {receivePort}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start OSC receiver on port {receivePort}: {e.Message}");
            isRunning = false;
        }
    }

    void Update()
    {
        // Fake data mode for testing
        if (useFakeData)
        {
            GenerateFakeData();
            return;
        }
        
        if (!isRunning || udpClient == null)
            return;

        // Process all available OSC messages
        while (udpClient.Available > 0)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                ProcessOscMessage(data);
            }
            catch (SocketException)
            {
                // Timeout or no data available - this is normal
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"OSC receive error: {e.Message}");
                break;
            }
        }
        
        // Check for stale data
        if (Time.time - lastFaceUpdateTime > DATA_TIMEOUT)
            hasFaceData = false;
        
        if (Time.time - lastGazeUpdateTime > DATA_TIMEOUT)
            hasGazeData = false;
    }
    
    void GenerateFakeData()
    {
        fakeDataTimer += Time.deltaTime;
        
        if (fakeDataTimer >= 1f / fakeDataRate)
        {
            fakeDataTimer = 0f;
            
            // Generate fake gaze data (circular pattern)
            fakeGazeAngle += Time.deltaTime * 2f; // Rotate over time
            float radius = 0.3f;
            gazePosition = new Vector2(
                0.5f + Mathf.Cos(fakeGazeAngle) * radius,
                0.5f + Mathf.Sin(fakeGazeAngle) * radius
            );
            pupilSize = 3f + Mathf.Sin(Time.time * 2f) * 0.5f; // Pulsing pupil
            hasGazeData = true;
            lastGazeUpdateTime = Time.time;
            
            // Generate fake face landmarks (subtle animation)
            for (int i = 0; i < 68; i++)
            {
                float angle = (i / 68f) * Mathf.PI * 2f;
                float breathe = Mathf.Sin(Time.time * 1.5f) * 0.02f;
                
                // Create a rough face shape
                float x = 0.5f + Mathf.Cos(angle) * (0.15f + breathe);
                float y = 0.5f + Mathf.Sin(angle) * (0.2f + breathe);
                float z = Mathf.Sin(angle * 4f) * 0.05f + breathe;
                
                // Add some variation for different facial features
                if (i >= 36 && i <= 47) // Eyes
                {
                    y += 0.1f;
                    float blink = Mathf.Max(0f, Mathf.Sin(Time.time * 3f));
                    y -= blink * 0.02f;
                }
                else if (i >= 48 && i <= 67) // Mouth
                {
                    y -= 0.15f;
                    float talk = Mathf.Sin(Time.time * 5f) * 0.03f;
                    y -= Mathf.Abs(talk);
                }
                else if (i >= 27 && i <= 35) // Nose
                {
                    z += 0.05f;
                }
                
                faceLandmarks[i] = new Vector3(x, y, z);
            }
            hasFaceData = true;
            lastFaceUpdateTime = Time.time;
        }
    }

    void ProcessOscMessage(byte[] data)
    {
        if (data == null || data.Length < 8)
            return;

        try
        {
            // Parse OSC message
            int index = 0;
            string address = ReadOscString(data, ref index);
            
            // Skip type tag string (e.g., ",fff" for 3 floats)
            string typeTags = ReadOscString(data, ref index);
            
            if (address == "/gaze")
            {
                // Parse gaze data: x, y, pupil
                float x = ReadOscFloat(data, ref index);
                float y = ReadOscFloat(data, ref index);
                float pupil = ReadOscFloat(data, ref index);
                
                // Validate data
                if (IsValidFloat(x) && IsValidFloat(y) && IsValidFloat(pupil))
                {
                    gazePosition = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
                    pupilSize = Mathf.Clamp(pupil, 0f, 10f);
                    hasGazeData = true;
                    lastGazeUpdateTime = Time.time;
                }
            }
            else if (address == "/facemesh")
            {
                // Parse face mesh data: landmark_index, x, y, z
                int landmarkIndex = (int)ReadOscFloat(data, ref index);
                float x = ReadOscFloat(data, ref index);
                float y = ReadOscFloat(data, ref index);
                float z = ReadOscFloat(data, ref index);
                
                // Validate and store
                if (landmarkIndex >= 0 && landmarkIndex < 68 && 
                    IsValidFloat(x) && IsValidFloat(y) && IsValidFloat(z))
                {
                    faceLandmarks[landmarkIndex] = new Vector3(
                        Mathf.Clamp(x, -10f, 10f),
                        Mathf.Clamp(y, -10f, 10f),
                        Mathf.Clamp(z, -10f, 10f)
                    );
                    hasFaceData = true;
                    lastFaceUpdateTime = Time.time;
                }
            }
        }
        catch (Exception e)
        {
            if (showDebugInfo)
                Debug.LogWarning($"OSC parsing error: {e.Message}");
        }
    }

    // OSC parsing helpers
    string ReadOscString(byte[] data, ref int index)
    {
        int start = index;
        while (index < data.Length && data[index] != 0)
            index++;
        
        string result = Encoding.ASCII.GetString(data, start, index - start);
        
        // Skip null terminator and align to 4-byte boundary
        index++;
        while (index % 4 != 0)
            index++;
        
        return result;
    }

    float ReadOscFloat(byte[] data, ref int index)
    {
        if (index + 4 > data.Length)
            return 0f;
        
        // OSC uses big-endian byte order
        byte[] floatBytes = new byte[4];
        floatBytes[0] = data[index + 3];
        floatBytes[1] = data[index + 2];
        floatBytes[2] = data[index + 1];
        floatBytes[3] = data[index + 0];
        
        index += 4;
        return BitConverter.ToSingle(floatBytes, 0);
    }

    bool IsValidFloat(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) < 1e6f;
    }

    // Public accessors
    public bool HasGazeData => hasGazeData;
    public bool HasFaceData => hasFaceData;
    
    public Vector2 GetGazePosition() => gazePosition;
    public float GetPupilSize() => pupilSize;
    
    public Vector3 GetFaceLandmark(int index)
    {
        if (index >= 0 && index < 68)
            return faceLandmarks[index];
        return Vector3.zero;
    }
    
    public Vector3[] GetAllFaceLandmarks() => faceLandmarks;

    void OnDestroy()
    {
        isRunning = false;
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 180));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== OSC DATA RECEIVER ===", GUI.skin.box);
        GUILayout.Label($"Mode: {(useFakeData ? "FAKE DATA (Testing)" : "OSC Real Data")}");
        if (!useFakeData)
        {
            GUILayout.Label($"Port: {receivePort}");
            GUILayout.Label($"Running: {isRunning}");
        }
        else
        {
            GUILayout.Label($"Update Rate: {fakeDataRate} Hz");
        }
        GUILayout.Label($"Gaze: {(hasGazeData ? $"{gazePosition.x:F2}, {gazePosition.y:F2}" : "No data")}");
        GUILayout.Label($"Face: {(hasFaceData ? "Receiving" : "No data")}");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
