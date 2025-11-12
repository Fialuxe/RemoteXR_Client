// FaceMeshToExpression.cs
// Converts LSL FaceMesh landmark data to avatar facial expressions.
// Attach this to your avatar GameObject alongside LslFaceMeshReceiver.
// 
// This script reads 10 key facial landmarks from LslFaceMeshReceiver and calculates:
// - Eye blink (left/right)
// - Mouth openness
// - Eyebrow movement
// - Jaw openness
// 
// The calculated values can drive blend shapes, bone transforms, or animation parameters.

using UnityEngine;

[RequireComponent(typeof(LslFaceMeshReceiver))]
public class FaceMeshToExpression : MonoBehaviour
{
    [Header("Avatar Components")]
    [Tooltip("SkinnedMeshRenderer containing facial blend shapes (optional)")]
    public SkinnedMeshRenderer faceMesh;
    
    [Header("Blend Shape Names")]
    [Tooltip("Blend shape name for left eye blink (e.g., 'eye_blink_left')")]
    public string leftEyeBlinkShapeName = "eye_blink_left";
    [Tooltip("Blend shape name for right eye blink (e.g., 'eye_blink_right')")]
    public string rightEyeBlinkShapeName = "eye_blink_right";
    [Tooltip("Blend shape name for mouth open (e.g., 'mouth_open')")]
    public string mouthOpenShapeName = "mouth_open";
    [Tooltip("Blend shape name for jaw open (e.g., 'jaw_open')")]
    public string jawOpenShapeName = "jaw_open";
    [Tooltip("Blend shape name for eyebrow raise (e.g., 'brow_raise')")]
    public string browRaiseShapeName = "brow_raise";
    
    [Header("Expression Smoothing")]
    [Range(0.01f, 1f)]
    [Tooltip("Lower = smoother but slower response")]
    public float smoothingFactor = 0.15f;
    
    [Header("Expression Multipliers")]
    [Range(0f, 2f)]
    public float eyeBlinkMultiplier = 1.0f;
    [Range(0f, 2f)]
    public float mouthOpenMultiplier = 1.0f;
    [Range(0f, 2f)]
    public float jawOpenMultiplier = 1.0f;
    [Range(0f, 2f)]
    public float browRaiseMultiplier = 1.0f;
    
    [Header("Calibration")]
    [Tooltip("Normalize expressions based on neutral face (set during Start)")]
    public bool useCalibration = true;
    [Tooltip("Seconds to wait before capturing neutral face for calibration")]
    public float calibrationDelay = 2.0f;
    
    [Header("Debug")]
    public bool showDebugGUI = true;
    
    private LslFaceMeshReceiver _faceMeshReceiver;
    
    // Blend shape indices (cached for performance)
    private int _leftEyeBlinkIndex = -1;
    private int _rightEyeBlinkIndex = -1;
    private int _mouthOpenIndex = -1;
    private int _jawOpenIndex = -1;
    private int _browRaiseIndex = -1;
    
    // Current smoothed expression values [0-100]
    private float _leftEyeBlink = 0f;
    private float _rightEyeBlink = 0f;
    private float _mouthOpen = 0f;
    private float _jawOpen = 0f;
    private float _browRaise = 0f;
    
    // Calibration baselines
    private float _neutralEyeHeight = 0f;
    private float _neutralMouthHeight = 0f;
    private float _neutralBrowY = 0f;
    private bool _calibrated = false;
    private float _calibrationTimer = 0f;
    
    void Start()
    {
        _faceMeshReceiver = GetComponent<LslFaceMeshReceiver>();
        
        if (faceMesh != null)
        {
            // Cache blend shape indices
            _leftEyeBlinkIndex = FindBlendShapeIndex(leftEyeBlinkShapeName);
            _rightEyeBlinkIndex = FindBlendShapeIndex(rightEyeBlinkShapeName);
            _mouthOpenIndex = FindBlendShapeIndex(mouthOpenShapeName);
            _jawOpenIndex = FindBlendShapeIndex(jawOpenShapeName);
            _browRaiseIndex = FindBlendShapeIndex(browRaiseShapeName);
            
            Debug.Log($"FaceMeshToExpression: Found blend shapes - " +
                     $"LeftEye:{_leftEyeBlinkIndex}, RightEye:{_rightEyeBlinkIndex}, " +
                     $"Mouth:{_mouthOpenIndex}, Jaw:{_jawOpenIndex}, Brow:{_browRaiseIndex}");
        }
        else
        {
            Debug.LogWarning("FaceMeshToExpression: No SkinnedMeshRenderer assigned. Expression values will be calculated but not applied.");
        }
    }
    
    void Update()
    {
        if (_faceMeshReceiver == null || !_faceMeshReceiver.IsConnected)
            return;
        
        // Handle calibration timing
        if (useCalibration && !_calibrated)
        {
            _calibrationTimer += Time.deltaTime;
            if (_calibrationTimer >= calibrationDelay)
            {
                CalibrateNeutralFace();
            }
            else
            {
                return; // Don't process expressions until calibrated
            }
        }
        
        // Calculate expression values from landmarks
        CalculateExpressions();
        
        // Apply to blend shapes if available
        if (faceMesh != null)
        {
            ApplyBlendShapes();
        }
    }
    
    void CalculateExpressions()
    {
        // Get landmark positions using 68-point model
        // Eyes: Use multiple points for accurate detection
        Vector3 rightEyeTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_TOP);
        Vector3 rightEyeBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_BOTTOM);
        Vector3 leftEyeTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.LEFT_EYE_TOP);
        Vector3 leftEyeBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.LEFT_EYE_BOTTOM);
        
        // Mouth: Use top and bottom lip points
        Vector3 upperLipTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.UPPER_LIP_TOP);
        Vector3 lowerLipBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.LOWER_LIP_BOTTOM);
        Vector3 upperLipInner = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.UPPER_LIP_INNER);
        Vector3 lowerLipInner = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.LOWER_LIP_INNER);
        
        // Face structure
        Vector3 noseTip = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.NOSE_TIP);
        Vector3 chin = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.CHIN);
        
        // Eyebrows: Use average of eyebrow points
        Vector3 rightBrow = _faceMeshReceiver.GetAverageLandmark(
            LslFaceMeshReceiver.RIGHT_BROW_START, LslFaceMeshReceiver.RIGHT_BROW_END);
        Vector3 leftBrow = _faceMeshReceiver.GetAverageLandmark(
            LslFaceMeshReceiver.LEFT_BROW_START, LslFaceMeshReceiver.LEFT_BROW_END);
        
        // DEBUG: Log landmark positions every second for debugging
        if (Time.frameCount % 30 == 0 && showDebugGUI)
        {
            Debug.Log($"[FaceMesh 68pt] " +
                     $"UpperLip: {upperLipTop.y:F4}, LowerLip: {lowerLipBottom.y:F4}, " +
                     $"MouthHeight: {Mathf.Abs(lowerLipBottom.y - upperLipTop.y):F4}, " +
                     $"REyeTop: {rightEyeTop.y:F4}, REyeBottom: {rightEyeBottom.y:F4}, " +
                     $"EyeHeight: {Mathf.Abs(rightEyeTop.y - rightEyeBottom.y):F4}");
        }
        
        // --- Eye Blink Detection (Improved with 68-point model) ---
        // --- Eye Blink Detection (Improved with 68-point model) ---
        // Measure vertical distance between top and bottom eyelid
        // Smaller distance = more closed eye
        bool rightEyeValid = _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.RIGHT_EYE_TOP) && 
                            _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.RIGHT_EYE_BOTTOM);
        bool leftEyeValid = _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.LEFT_EYE_TOP) && 
                           _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.LEFT_EYE_BOTTOM);
        
        if (rightEyeValid)
        {
            // Direct measurement of eye opening
            float eyeHeight = Mathf.Abs(rightEyeTop.y - rightEyeBottom.y);
            float targetBlink = 0f;
            
            if (_calibrated && _neutralEyeHeight > 0f)
            {
                // Normalize against calibrated neutral eye height
                float normalizedHeight = eyeHeight / _neutralEyeHeight;
                // When eye closes, height decreases -> blink increases
                targetBlink = Mathf.Clamp01(1f - normalizedHeight) * 100f * eyeBlinkMultiplier;
            }
            else
            {
                // Without calibration, use typical eye opening range
                // Typical eye height: 0.02-0.04 in normalized coords
                float openEyeHeight = 0.03f;
                targetBlink = Mathf.Clamp01(1f - (eyeHeight / openEyeHeight)) * 100f * eyeBlinkMultiplier;
            }
            
            _rightEyeBlink = Mathf.Lerp(_rightEyeBlink, targetBlink, smoothingFactor);
        }
        
        if (leftEyeValid)
        {
            float eyeHeight = Mathf.Abs(leftEyeTop.y - leftEyeBottom.y);
            float targetBlink = 0f;
            
            if (_calibrated && _neutralEyeHeight > 0f)
            {
                float normalizedHeight = eyeHeight / _neutralEyeHeight;
                targetBlink = Mathf.Clamp01(1f - normalizedHeight) * 100f * eyeBlinkMultiplier;
            }
            else
            {
                float openEyeHeight = 0.03f;
                targetBlink = Mathf.Clamp01(1f - (eyeHeight / openEyeHeight)) * 100f * eyeBlinkMultiplier;
            }
            
            _leftEyeBlink = Mathf.Lerp(_leftEyeBlink, targetBlink, smoothingFactor);
        }
        
        // --- Mouth Open Detection (Improved with 68-point model) ---
        bool mouthValid = _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.UPPER_LIP_TOP) && 
                         _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.LOWER_LIP_BOTTOM);
        
        if (mouthValid)
        {
            // Direct measurement of outer lip distance
            float mouthHeight = Mathf.Abs(lowerLipBottom.y - upperLipTop.y);
            float targetMouthOpen = 0f;
            
            if (_calibrated && _neutralMouthHeight > 0f)
            {
                // Normalize against calibrated neutral
                float normalizedHeight = mouthHeight / _neutralMouthHeight;
                // Mouth open increases with height
                targetMouthOpen = Mathf.Clamp01((normalizedHeight - 1f) * 3f) * 100f * mouthOpenMultiplier;
            }
            else
            {
                // Without calibration, use typical closed mouth height
                // Typical closed mouth: 0.01-0.02, open: 0.05-0.15
                float closedMouthHeight = 0.015f;
                float mouthRange = 0.1f;
                targetMouthOpen = Mathf.Clamp01((mouthHeight - closedMouthHeight) / mouthRange) * 100f * mouthOpenMultiplier;
            }
            
            _mouthOpen = Mathf.Lerp(_mouthOpen, targetMouthOpen, smoothingFactor);
        }
        
        // --- Jaw Open Detection ---
        bool jawValid = _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.NOSE_TIP) && 
                       _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.CHIN);
        
        if (jawValid)
        {
            float jawHeight = Mathf.Abs(chin.y - noseTip.y);
            float targetJawOpen = 0f;
            
            // Jaw open stretches the face vertically
            // Typical range: 0.15-0.25 in normalized coords
            float neutralJawHeight = 0.18f;
            float jawRange = 0.08f;
            targetJawOpen = Mathf.Clamp01((jawHeight - neutralJawHeight) / jawRange) * 100f * jawOpenMultiplier;
            
            _jawOpen = Mathf.Lerp(_jawOpen, targetJawOpen, smoothingFactor);
        }
        
        // --- Eyebrow Raise Detection (Improved with 68-point model) ---
        bool browValid = _faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.RIGHT_BROW_START);
        
        if (browValid)
        {
            float browY = rightBrow.y;
            float targetBrowRaise = 0f;
            
            if (_calibrated && _neutralBrowY > 0f)
            {
                // Eyebrows move up when raised (y decreases in screen coords, but increases in normalized [0,1])
                float browDelta = browY - _neutralBrowY;
                targetBrowRaise = Mathf.Clamp01(browDelta * 15f) * 100f * browRaiseMultiplier;
            }
            else
            {
                // Without calibration, detect relative position
                // Brows should be above eyes
                Vector3 rightEye = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_TOP);
                if (_faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.RIGHT_EYE_TOP))
                {
                    float browToEye = browY - rightEye.y;
                    // Typical range: -0.03 to -0.06 (brow above eye in screen coords)
                    targetBrowRaise = Mathf.Clamp01((browToEye + 0.04f) / 0.03f) * 100f * browRaiseMultiplier;
                }
            }
            
            _browRaise = Mathf.Lerp(_browRaise, targetBrowRaise, smoothingFactor);
        }
    }
    
    void ApplyBlendShapes()
    {
        if (_leftEyeBlinkIndex >= 0)
            faceMesh.SetBlendShapeWeight(_leftEyeBlinkIndex, _leftEyeBlink);
        
        if (_rightEyeBlinkIndex >= 0)
            faceMesh.SetBlendShapeWeight(_rightEyeBlinkIndex, _rightEyeBlink);
        
        if (_mouthOpenIndex >= 0)
            faceMesh.SetBlendShapeWeight(_mouthOpenIndex, _mouthOpen);
        
        if (_jawOpenIndex >= 0)
            faceMesh.SetBlendShapeWeight(_jawOpenIndex, _jawOpen);
        
        if (_browRaiseIndex >= 0)
            faceMesh.SetBlendShapeWeight(_browRaiseIndex, _browRaise);
    }
    
    void CalibrateNeutralFace()
    {
        // Capture current facial landmark positions as "neutral"
        Vector3 rightEyeTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_TOP);
        Vector3 rightEyeBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_BOTTOM);
        Vector3 upperLipTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.UPPER_LIP_TOP);
        Vector3 lowerLipBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.LOWER_LIP_BOTTOM);
        Vector3 rightBrow = _faceMeshReceiver.GetAverageLandmark(
            LslFaceMeshReceiver.RIGHT_BROW_START, LslFaceMeshReceiver.RIGHT_BROW_END);
        
        if (_faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.RIGHT_EYE_TOP))
        {
            _neutralEyeHeight = Mathf.Abs(rightEyeTop.y - rightEyeBottom.y);
        }
        
        if (_faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.UPPER_LIP_TOP))
        {
            _neutralMouthHeight = Mathf.Abs(lowerLipBottom.y - upperLipTop.y);
        }
        
        if (_faceMeshReceiver.IsLandmarkValid(LslFaceMeshReceiver.RIGHT_BROW_START))
        {
            _neutralBrowY = rightBrow.y;
        }
        
        _calibrated = true;
        Debug.Log($"FaceMeshToExpression: Calibrated neutral face (68-point model) - " +
                 $"EyeHeight:{_neutralEyeHeight:F4}, MouthHeight:{_neutralMouthHeight:F4}, BrowY:{_neutralBrowY:F4}");
    }
    
    int FindBlendShapeIndex(string shapeName)
    {
        if (faceMesh == null || string.IsNullOrEmpty(shapeName))
            return -1;
        
        int shapeCount = faceMesh.sharedMesh.blendShapeCount;
        for (int i = 0; i < shapeCount; i++)
        {
            if (faceMesh.sharedMesh.GetBlendShapeName(i).Equals(shapeName, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        
        return -1;
    }
    
    void OnGUI()
    {
        if (!showDebugGUI)
            return;
        
        GUILayout.BeginArea(new Rect(10, 10, 400, 400));
        GUILayout.Box("FaceMesh Expression Debug");
        
        if (!_faceMeshReceiver.IsConnected)
        {
            GUILayout.Label("LSL FaceMesh: NOT CONNECTED");
        }
        else
        {
            GUILayout.Label("LSL FaceMesh: CONNECTED");
            
            if (useCalibration && !_calibrated)
            {
                float remaining = calibrationDelay - _calibrationTimer;
                GUILayout.Label($"Calibrating in: {remaining:F1}s");
                GUILayout.Label("(Keep neutral expression)");
            }
            else
            {
                GUILayout.Label($"Left Eye Blink: {_leftEyeBlink:F1}%");
                GUILayout.Label($"Right Eye Blink: {_rightEyeBlink:F1}%");
                GUILayout.Label($"Mouth Open: {_mouthOpen:F1}%");
                GUILayout.Label($"Jaw Open: {_jawOpen:F1}%");
                GUILayout.Label($"Brow Raise: {_browRaise:F1}%");
                
                // Show raw landmark values
                GUILayout.Space(10);
                GUILayout.Label("--- Raw Landmarks (68pt) ---");
                Vector3 upperLipTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.UPPER_LIP_TOP);
                Vector3 lowerLipBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.LOWER_LIP_BOTTOM);
                Vector3 rightEyeTop = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_TOP);
                Vector3 rightEyeBottom = _faceMeshReceiver.GetLandmark(LslFaceMeshReceiver.RIGHT_EYE_BOTTOM);
                
                GUILayout.Label($"Upper Lip Y: {upperLipTop.y:F4}");
                GUILayout.Label($"Lower Lip Y: {lowerLipBottom.y:F4}");
                GUILayout.Label($"Mouth Height: {Mathf.Abs(lowerLipBottom.y - upperLipTop.y):F4}");
                GUILayout.Label($"R Eye Top Y: {rightEyeTop.y:F4}");
                GUILayout.Label($"R Eye Bot Y: {rightEyeBottom.y:F4}");
                GUILayout.Label($"Eye Height: {Mathf.Abs(rightEyeTop.y - rightEyeBottom.y):F4}");
                
                if (_calibrated)
                {
                    GUILayout.Label($"Neutral Mouth: {_neutralMouthHeight:F4}");
                    GUILayout.Label($"Neutral Eye: {_neutralEyeHeight:F4}");
                }
            }
        }
        
        GUILayout.EndArea();
    }
    
    // Public API for accessing expression values
    public float GetLeftEyeBlink() => _leftEyeBlink;
    public float GetRightEyeBlink() => _rightEyeBlink;
    public float GetMouthOpen() => _mouthOpen;
    public float GetJawOpen() => _jawOpen;
    public float GetBrowRaise() => _browRaise;
    
    // Manual recalibration
    public void Recalibrate()
    {
        _calibrated = false;
        _calibrationTimer = 0f;
    }
}
