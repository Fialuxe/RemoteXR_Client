# Usage:
# python3 lsl_server.py --camera 1 --filter kalman
#
# Data Format Specification:
# --------------------------------------------------------------------------------
# EyeGaze LSL Stream: [gaze_x_normalized, gaze_y_normalized, blink]
#   - gaze_x_normalized: Horizontal gaze position, normalized [0.0, 1.0]
#                        0.0 = left edge of display, 1.0 = right edge of display
#   - gaze_y_normalized: Vertical gaze position, normalized [0.0, 1.0]
#                        0.0 = top edge of display, 1.0 = bottom edge of display
#   - blink: Blink detection flag (0 or 1)
#            0 = eyes open (normal gaze)
#            1 = blink detected
#
# Transmission Rules:
#   - If blink detected:        Send (0.0, 0.0, 1.0)
#   - If eyes open and valid:   Send (x, y, 0.0) where x,y are normalized gaze coords
#   - If data is invalid:       Do NOT send anything, skip to next frame
#
# FaceMesh LSL Stream: [10 key landmark points (x,y,z)]
#   - Landmarks: nose_tip, right_eye, left_eye, mouth_right, mouth_left, 
#                chin, forehead, upper_lip, lower_lip, right_cheek
#   - x, y: normalized [0.0, 1.0]
#   - z: depth in meters (relative to camera)
# --------------------------------------------------------------------------------
# This script runs the Eyetrax gaze estimation demo and streams gaze data and MediaPipe FaceMesh data via LSL.
# --- Imports ---
import os
import time
try: 
    import cv2
except ImportError as e:
    raise SystemExit("cv2 not installed. Run: pip install opencv-python") from e
import numpy as np

try:
    import mediapipe as mp
except ImportError as e:
    raise SystemExit("mediapipe not installed. Run: pip install mediapipe") from e

 # Eyetrax imports
try:
    from eyetrax.calibration import (
        run_5_point_calibration,
        run_9_point_calibration,
        run_lissajous_calibration,
    )
    from eyetrax.cli import parse_common_args
    from eyetrax.filters import KalmanSmoother, KDESmoother, NoSmoother, make_kalman
    from eyetrax.gaze import GazeEstimator
    from eyetrax.utils.draw import draw_cursor, make_thumbnail
    from eyetrax.utils.screen import get_screen_size
    from eyetrax.utils.video import camera, fullscreen, iter_frames
except ImportError as e:
    raise SystemExit("eyetrax not installed. Run: pip install eyetrax") from e

# LSL imports
try:
    from pylsl import StreamInfo, StreamOutlet, local_clock
except ImportError as e:
    raise SystemExit("pylsl not installed. Run: pip install pylsl") from e

# --- EyeGaze LSL Stream Configuration ---
STREAM_NAME = "EyeGaze"
STREAM_TYPE = "Gaze"
CHANNEL_COUNT = 3
# Use 30.0 for fixed rate; was 0.0 for irregular rate (causing crashes)
SAMPLE_RATE = 30.0
CHANNEL_FORMAT = 'float32'
SOURCE_ID = 'eyetrax_source_001'

# --- MediaPipe FaceMesh LSL Stream Configuration ---
FACEMESH_STREAM_NAME = "FaceMesh"
FACEMESH_STREAM_TYPE = "FaceLandmarks"
FACEMESH_CHANNEL_COUNT = 10 * 3  # 10 landmark points, each with x,y,z
FACEMESH_SAMPLE_RATE = 0.0
FACEMESH_CHANNEL_FORMAT = 'float32'
FACEMESH_SOURCE_ID = 'eyetrax_facemesh_001'
# --- End LSL Configuration ---
def create_facemesh_lsl_outlet(): 
    """
    Creates and returns a new LSL StreamOutlet for MediaPipe FaceMesh data.
    Sends 10 key landmark points (x, y, z) for avatar expression, normalized to [0,1] (x, y) and z in meters.
    """
    info = StreamInfo(
        FACEMESH_STREAM_NAME,
        FACEMESH_STREAM_TYPE,
        FACEMESH_CHANNEL_COUNT,
        FACEMESH_SAMPLE_RATE,
        FACEMESH_CHANNEL_FORMAT,
        FACEMESH_SOURCE_ID,
    )
    chns = info.desc().append_child("channels")
    # Use 10 key points: 0(nose tip), 33(right eye), 263(left eye), 61(mouth right), 291(mouth left), 199(chin), 1(forehead), 13(upper lip), 14(lower lip), 17(right cheek)
    landmark_names = ["nose_tip", "right_eye", 
                      "left_eye", "mouth_right", 
                      "mouth_left", "chin", 
                      "forehead", "upper_lip", 
                      "lower_lip", "right_cheek"]
    for name in landmark_names:
        for axis in ["x", "y", "z"]:
            ch = chns.append_child("channel")
            ch.append_child_value("label", f"{name}_{axis}")
    info.desc().append_child_value("description", "10 MediaPipe FaceMesh keypoints (x,y normalized [0,1], z in meters)")
    return StreamOutlet(info, chunk_size=1, max_buffered=360)


def create_lsl_outlet() -> StreamOutlet:
    """
    Creates and returns a new LSL StreamOutlet based on the constants.
    """
    info = StreamInfo(
        STREAM_NAME,
        STREAM_TYPE,
        CHANNEL_COUNT,
        SAMPLE_RATE,
        CHANNEL_FORMAT,
        SOURCE_ID,
    )
    
    # Add channel labels as metadata
    chns = info.desc().append_child("channels")
    for label in ["gaze_x_normalized", "gaze_y_normalized", "blink"]:
        ch = chns.append_child("channel")
        ch.append_child_value("label", label)

    # Add coordinate system metadata (good practice)
    gaze_desc = info.desc().append_child("gaze_coordinate_system")
    gaze_desc.append_child_value("convention", "TopLeft")
    gaze_desc.append_child_value("units", "Normalized")
    gaze_desc.append_child_value("range_x", "[0.0, 1.0]")
    gaze_desc.append_child_value("range_y", "[0.0, 1.0]")
    gaze_desc.append_child_value("blink", "0=eyes_open, 1=blink_detected")
    
    print(f"Creating LSL outlet '{STREAM_NAME}' (Type: {STREAM_TYPE})...")
    # chunk_size=1 means we push one sample at a time
    return StreamOutlet(info, chunk_size=1, max_buffered=360)


def run_demo_with_lsl():
    """
    Runs the Eyetrax demo and streams gaze data via LSL.
    """
    args = parse_common_args()

    filter_method = args.filter
    camera_index = args.camera
    calibration_method = args.calibration
    background_path = args.background
    confidence_level = args.confidence

    gaze_estimator = GazeEstimator(model_name=args.model)

    if args.model_file and os.path.isfile(args.model_file):
        gaze_estimator.load_model(args.model_file)
        print(f"[demo] Loaded gaze model from {args.model_file}")
    else:
        if calibration_method == "9p":
            run_9_point_calibration(gaze_estimator, camera_index=camera_index)
        elif calibration_method == "5p":
            run_5_point_calibration(gaze_estimator, camera_index=camera_index)
        else:
            run_lissajous_calibration(gaze_estimator, camera_index=camera_index)

    screen_width, screen_height = get_screen_size()
    if screen_width == 0 or screen_height == 0:
        raise ValueError("Could not get valid screen dimensions. Cannot normalize coordinates.")
    
    print(f"Screen resolution detected: {screen_width}x{screen_height}")
    
    # Validate screen dimensions are reasonable (not corrupted)
    if screen_width < 100 or screen_width > 10000 or screen_height < 100 or screen_height > 10000:
        raise ValueError(f"Screen dimensions ({screen_width}x{screen_height}) appear invalid. Please check display settings.")

    if filter_method == "kalman":
        kalman = make_kalman()
        smoother = KalmanSmoother(kalman)
        smoother.tune(gaze_estimator, camera_index=camera_index)
    elif filter_method == "kde":
        kalman = None
        smoother = KDESmoother(screen_width, screen_height, confidence=confidence_level)
    else:
        kalman = None
        smoother = NoSmoother()

    if background_path and os.path.isfile(background_path):
        background = cv2.imread(background_path)
        background = cv2.resize(background, (screen_width, screen_height))
    else:
        background = np.zeros((screen_height, screen_width, 3), dtype=np.uint8)
        background[:] = (50, 50, 50)

    cam_width, cam_height = 320, 240
    BORDER = 2
    MARGIN = 20
    cursor_alpha = 0.0
    cursor_step = 0.05


    # --- Create the LSL Outlets ---
    try:
        outlet = create_lsl_outlet()
        print("LSL Outlet created. Streaming gaze data...")
    except Exception as e:
        print(f"Error creating LSL outlet: {e}")
        print("Continuing without LSL streaming.")
        outlet = None

    try:
        facemesh_outlet = create_facemesh_lsl_outlet()
        print("FaceMesh LSL Outlet created. Streaming face mesh data...")
    except Exception as e:
        print(f"Error creating FaceMesh LSL outlet: {e}")
        print("Continuing without FaceMesh LSL streaming.")
        facemesh_outlet = None
    # --- End LSL Setup ---



    mp_face_mesh = mp.solutions.face_mesh
    with camera(camera_index) as cap, mp_face_mesh.FaceMesh(static_image_mode=False, max_num_faces=1, refine_landmarks=True, min_detection_confidence=0.5, min_tracking_confidence=0.5) as face_mesh:
        prev_time = time.time()
        last_print_time = time.time()
        frame_count = 0
        gaze_data_str = "N/A"
        facemesh_data_str = "N/A"

        for frame in iter_frames(cap):
            frame_count += 1
            features, blink_detected = gaze_estimator.extract_features(frame)
            lsl_sample = None  # Will be set based on blink/gaze state

            # --- FaceMesh processing ---
            facemesh_sample = [np.nan] * FACEMESH_CHANNEL_COUNT
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = face_mesh.process(rgb_frame)
            if results.multi_face_landmarks:
                # Use first detected face
                face_landmarks = results.multi_face_landmarks[0]
                # 10 key indices: 0,33,263,61,291,199,1,13,14,17
                key_indices = [0,33,263,61,291,199,1,13,14,17]
                for i, idx in enumerate(key_indices):
                    lm = face_landmarks.landmark[idx]
                    # Validate landmark values before using them
                    if not (np.isfinite(lm.x) and np.isfinite(lm.y) and np.isfinite(lm.z)):
                        continue  # Skip invalid landmarks
                    if abs(lm.x) > 10.0 or abs(lm.y) > 10.0 or abs(lm.z) > 10.0:
                        continue  # Skip extreme values
                    
                    facemesh_sample[i*3+0] = max(0.0, min(1.0, lm.x))  # clamp to [0,1]
                    facemesh_sample[i*3+1] = max(0.0, min(1.0, lm.y))  # clamp to [0,1]
                    facemesh_sample[i*3+2] = max(-1.0, min(1.0, lm.z))  # clamp z to reasonable range

            # --- Gaze estimation and LSL sample preparation ---
            # Case 1: Blink detected -> send (0, 0, 1)
            if blink_detected:
                lsl_sample = [0.0, 0.0, 1.0]
                x_pred = y_pred = None
                contours = []
                cursor_alpha = max(cursor_alpha - cursor_step, 0.0)
            # Case 2: Valid features and no blink -> send (x, y, 0)
            elif features is not None:
                gaze_point = gaze_estimator.predict(np.array([features]))[0]
                x, y = map(int, gaze_point)
                
                # Validate raw predictions before smoothing
                if not (np.isfinite(x) and np.isfinite(y)):
                    print(f"\nWARNING: Gaze estimator returned non-finite values: ({x}, {y}). Skipping.")
                    lsl_sample = None
                    x_pred = y_pred = None
                    contours = []
                    cursor_alpha = max(cursor_alpha - cursor_step, 0.0)
                elif abs(x) > 100000 or abs(y) > 100000:
                    print(f"\nWARNING: Gaze estimator returned extreme values: ({x}, {y}). Skipping.")
                    lsl_sample = None
                    x_pred = y_pred = None
                    contours = []
                    cursor_alpha = max(cursor_alpha - cursor_step, 0.0)
                else:
                    x_pred, y_pred = smoother.step(x, y)
                    contours = smoother.debug.get("contours", [])
                    cursor_alpha = min(cursor_alpha + cursor_step, 1.0)

                    # Normalize gaze coordinates to [0, 1]
                    # Add safety checks for division by zero and invalid values
                    if screen_width <= 0 or screen_height <= 0:
                        print(f"\nWARNING: Invalid screen dimensions ({screen_width}x{screen_height}). Skipping frame.")
                        lsl_sample = None
                    elif x_pred is None or y_pred is None or not np.isfinite(x_pred) or not np.isfinite(y_pred):
                        print(f"\nWARNING: Invalid predicted gaze ({x_pred}, {y_pred}). Skipping frame.")
                        lsl_sample = None
                    else:
                        lsl_gaze_x = x_pred / screen_width
                        lsl_gaze_y = y_pred / screen_height
                        
                        # Additional safety check after division
                        if not np.isfinite(lsl_gaze_x) or not np.isfinite(lsl_gaze_y):
                            print(f"\nWARNING: Division resulted in invalid values ({lsl_gaze_x}, {lsl_gaze_y}). Skipping frame.")
                            lsl_sample = None
                        else:
                            # Clamp to valid range
                            lsl_gaze_x = max(0.0, min(1.0, lsl_gaze_x))
                            lsl_gaze_y = max(0.0, min(1.0, lsl_gaze_y))
                            lsl_sample = [lsl_gaze_x, lsl_gaze_y, 0.0]
            # Case 3: Invalid data (features is None and no blink) -> don't send
            else:
                lsl_sample = None
                x_pred = y_pred = None
                contours = []
                cursor_alpha = max(cursor_alpha - cursor_step, 0.0)

            # --- Push samples to LSL ---
            # CRITICAL: Only send valid data to prevent prediction errors/freezes on receiver side
            if outlet:
                try:
                    if lsl_sample is not None:
                        # FINAL VALIDATION: Absolutely ensure no invalid values are sent
                        all_values_valid = True
                        for val in lsl_sample:
                            if not isinstance(val, (int, float)) or not np.isfinite(val) or abs(val) > 10.0:
                                all_values_valid = False
                                print(f"\nCRITICAL: Blocked invalid gaze sample from being sent: {lsl_sample}")
                                break
                        
                        if all_values_valid:
                            # Double-check range [0, 1] for x and y
                            if not (0.0 <= lsl_sample[0] <= 1.0 and 0.0 <= lsl_sample[1] <= 1.0):
                                print(f"\nCRITICAL: Gaze values out of range [0,1]: {lsl_sample}. Not sending.")
                            else:
                                # Send valid data: either (x, y, 0) or (0, 0, 1)
                                outlet.push_sample(lsl_sample, local_clock())
                                if lsl_sample[2] == 1.0:
                                    gaze_data_str = "Gaze: BLINK (0,0,1)"
                                else:
                                    gaze_data_str = f"Gaze: x={lsl_sample[0]:.3f} y={lsl_sample[1]:.3f} (blink=0)"
                        else:
                            gaze_data_str = "Gaze: BLOCKED (invalid values)"
                    else:
                        # Invalid data - skip sending
                        gaze_data_str = "Gaze: INVALID (not sent)"
                except Exception as e:
                    gaze_data_str = f"Gaze Error: {e}"
            
            if facemesh_outlet:
                try:
                    # FINAL VALIDATION: Absolutely ensure no invalid values are sent
                    all_valid = True
                    for i, val in enumerate(facemesh_sample):
                        if not isinstance(val, (int, float)) or not np.isfinite(val) or abs(val) > 100.0:
                            all_valid = False
                            print(f"\nCRITICAL: Blocked invalid FaceMesh sample at index {i}: {val}")
                            break
                    
                    # Only push if facemesh data is valid
                    if all_valid and not np.isnan(facemesh_sample[0]):
                        # Additional range check for normalized values (x, y should be in [0, 1])
                        range_valid = True
                        for i in range(0, 30, 3):  # Check x and y coordinates
                            if not (0.0 <= facemesh_sample[i] <= 1.0 and 0.0 <= facemesh_sample[i+1] <= 1.0):
                                range_valid = False
                                print(f"\nCRITICAL: FaceMesh coordinate out of range [0,1] at landmark {i//3}: x={facemesh_sample[i]}, y={facemesh_sample[i+1]}")
                                break
                        
                        if range_valid:
                            facemesh_outlet.push_sample(facemesh_sample, local_clock())
                            # Count valid landmarks
                            valid_count = sum(1 for i in range(0, 30, 3) if not np.isnan(facemesh_sample[i]))
                            facemesh_data_str = f"FaceMesh: {valid_count}/10"
                        else:
                            facemesh_data_str = "FaceMesh: BLOCKED (out of range)"
                    else:
                        # Skip sending invalid data
                        facemesh_data_str = "FaceMesh: INVALID (not sent)"
                except Exception as e:
                    facemesh_data_str = f"FaceMesh Error: {e}"
            
            # Print data every second (overwrite previous line)
            current_time = time.time()
            if current_time - last_print_time >= 1.0:
                fps = frame_count / (current_time - last_print_time)
                print(f"\r[FPS: {fps:.1f}] {gaze_data_str} | {facemesh_data_str}          ", end="", flush=True)
                last_print_time = current_time
                frame_count = 0

            # --- Minimal debug UI ---
            canvas = np.ones((400, 320, 3), dtype=np.uint8) * 30  # dark background
            # Draw normalized gaze area (rectangle)
            gaze_rect = (40, 40, 240, 160)  # x, y, w, h
            cv2.rectangle(canvas, (gaze_rect[0], gaze_rect[1]), (gaze_rect[0]+gaze_rect[2], gaze_rect[1]+gaze_rect[3]), (100,255,100), 2)
            # Draw gaze point if valid (not blinking and not invalid)
            if lsl_sample is not None and lsl_sample[2] == 0.0:
                gx = int(gaze_rect[0] + lsl_sample[0] * gaze_rect[2])
                gy = int(gaze_rect[1] + lsl_sample[1] * gaze_rect[3])
                cv2.circle(canvas, (gx, gy), 8, (0,255,255), -1)
            # Draw improved face representation below
            face_origin = (160, 270)
            face_radius = 50
            cv2.ellipse(canvas, face_origin, (face_radius, face_radius), 0, 0, 360, (180,180,180), 2)
            # Draw eyes/mouth from facemesh if available
            if results.multi_face_landmarks:
                face_landmarks = results.multi_face_landmarks[0]
                
                # Right eye: 33 (center), 159 (top lid)
                rx = int(face_origin[0] + (face_landmarks.landmark[33].x-0.5)*face_radius*2*0.6)
                ry = int(face_origin[1] + (face_landmarks.landmark[33].y-0.5)*face_radius*2*0.5)
                r_top_y = int(face_origin[1] + (face_landmarks.landmark[159].y-0.5)*face_radius*2*0.5)
                r_open = max(4, abs(ry - r_top_y))
                cv2.ellipse(canvas, (rx, ry), (10, r_open), 0, 0, 360, (255,255,255), -1)
                
                # Left eye: 263 (center), 386 (top lid)
                lx = int(face_origin[0] + (face_landmarks.landmark[263].x-0.5)*face_radius*2*0.6)
                ly = int(face_origin[1] + (face_landmarks.landmark[263].y-0.5)*face_radius*2*0.5)
                l_top_y = int(face_origin[1] + (face_landmarks.landmark[386].y-0.5)*face_radius*2*0.5)
                l_open = max(4, abs(ly - l_top_y))
                cv2.ellipse(canvas, (lx, ly), (10, l_open), 0, 0, 360, (255,255,255), -1)
                
                # Mouth: 13 (upper lip), 14 (lower lip)
                mx = int(face_origin[0] + (face_landmarks.landmark[13].x-0.5)*face_radius*2*0.7)
                m_top_y = int(face_origin[1] + (face_landmarks.landmark[13].y-0.5)*face_radius*2*0.7)
                m_bot_y = int(face_origin[1] + (face_landmarks.landmark[14].y-0.5)*face_radius*2*0.7)
                m_open = max(2, abs(m_bot_y - m_top_y))
                m_center_y = (m_top_y + m_bot_y) // 2
                cv2.line(canvas, (mx-15, m_center_y), (mx+15, m_center_y), (200,200,255), m_open)
            # Show window
            cv2.imshow("Minimal Eye/Face Debug", canvas)
            if cv2.waitKey(1) == 27:
                print("\nEscape key pressed. Stopping stream.")
                break
        print("\nDemo loop finished.")


if __name__ == "__main__":
    run_demo_with_lsl()