using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// Two-finger camera control: pinch-to-zoom and ground-aligned two-finger
/// pan. Optional auto-follow that smoothly tracks the player's position.
/// Reset method (and optional UI button) snaps the camera back to a default view.
///
/// Architecture:
///   This component is fully additive. It lives on the Camera GameObject
///   (not on the Player). When not present, the camera behaves as it
///   always has — fixed in place, controlled by whatever positioned it.
///
/// Pan model — ground-aligned:
///   Two-finger drag (or middle-mouse drag in editor) moves the focal
///   point along the WORLD horizontal plane (XZ). Drag-up = focal point
///   moves world-forward (+Z). Drag-right = focal point moves world-right
///   (+X). Camera tilt does not affect pan direction — panning always
///   feels like dragging the world across the floor, regardless of the
///   camera's viewing angle.
///
///   This contrasts with screen-aligned pan (which moves along the
///   camera's local axes). Screen-aligned pan looks weird with tilted
///   cameras — the focal point ends up sliding diagonally up into the
///   air. Ground-aligned pan keeps the world flat under the camera.
///
/// Input model:
///   Single-finger touches are intentionally ignored. SwipeInputProvider
///   reads single-finger touches for cube movement; this component reads
///   only when two fingers are active. The two systems coexist cleanly.
///
/// Editor testing:
///   - Mouse scroll wheel → pinch (zoom in/out)
///   - Middle-mouse drag → two-finger pan
/// </summary>
public class PlayerCamera : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Master toggle. Uncheck to disable camera input.")]
    [SerializeField] private bool inputEnabled = true;

    [Header("Player Reference")]
    [Tooltip("Reference to the player Transform. The camera focuses on this " +
             "transform's position by default. If null, focal point is the " +
             "camera's initial position.")]
    [SerializeField] private Transform player;

    [Header("Zoom")]
    [Tooltip("Default distance from focal point to camera. Reset returns to this.")]
    [SerializeField] private float defaultDistance = 12f;

    [Tooltip("Closest the camera is allowed to get to the focal point.")]
    [SerializeField] private float minDistance = 5f;

    [Tooltip("Furthest the camera is allowed to get from the focal point.")]
    [SerializeField] private float maxDistance = 30f;

    [Tooltip("Pinch sensitivity. Higher = bigger zoom changes per pixel of motion.")]
    [SerializeField] private float zoomSpeed = 0.02f;

    [Header("Pan")]
    [Tooltip("Pan sensitivity. Higher = bigger world motion per pixel of finger motion.")]
    [SerializeField] private float panSpeed = 0.02f;

    [Tooltip("Maximum distance the focal point can be panned from its anchor. " +
             "Prevents the player from getting completely lost. Reset returns to zero.")]
    [SerializeField] private float maxPanDistance = 20f;

    [Header("Auto-Follow")]
    [Tooltip("If checked, the camera smoothly tracks player position as they roll. " +
             "Pan offset is preserved (panning while following still works).")]
    [SerializeField] private bool autoFollow = false;

    [Tooltip("Used only when Auto-Follow is on. Higher = catches up faster. " +
             "5 is smooth, 20 is near-instant.")]
    [SerializeField] private float autoFollowSpeed = 5f;

    [Header("Editor / Desktop Testing")]
    [Tooltip("If checked, mouse scroll wheel zooms and middle-mouse drag pans. " +
             "Lets you test in the editor without a touchscreen.")]
    [SerializeField] private bool enableMouseControls = true;

    [Header("Debug")]
    [Tooltip("If checked, logs gesture events to Console.")]
    [SerializeField] private bool verboseLogging = false;

    // -- runtime state --
    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Vector3 cameraDirectionLocal;  // unit vector from focal point toward camera
    private Vector3 focalAnchor;
    private Vector3 panOffset = Vector3.zero;
    private float currentDistance;
    private Vector3 smoothedFocalPoint;

    // -- per-frame gesture state --
    private float lastPinchDistance;
    private Vector2 lastPanMidpoint;
    private bool wasTwoFingersDown;
    private bool wasMiddleMouseDown;

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Awake()
    {
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;

        focalAnchor = (player != null) ? player.position : Vector3.zero;
        Vector3 fromFocalToCamera = defaultPosition - focalAnchor;

        if (fromFocalToCamera.sqrMagnitude < 0.001f)
        {
            cameraDirectionLocal = new Vector3(0f, 0.7f, -0.7f).normalized;
        }
        else
        {
            cameraDirectionLocal = fromFocalToCamera.normalized;
        }

        currentDistance = defaultDistance;
        smoothedFocalPoint = focalAnchor;
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            UpdateCameraTransform();
            return;
        }

        ReadTouchInput();
        if (enableMouseControls)
        {
            ReadMouseInput();
        }

        UpdateCameraTransform();
    }

    // ============================================================
    // INPUT — TOUCH
    // ============================================================

    private void ReadTouchInput()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        if (touches.Count < 2)
        {
            wasTwoFingersDown = false;
            return;
        }

        Vector2 t0 = touches[0].screenPosition;
        Vector2 t1 = touches[1].screenPosition;
        float pinchDistance = Vector2.Distance(t0, t1);
        Vector2 midpoint = (t0 + t1) * 0.5f;

        if (!wasTwoFingersDown)
        {
            lastPinchDistance = pinchDistance;
            lastPanMidpoint = midpoint;
            wasTwoFingersDown = true;
            Log($"Two-finger gesture started. Pinch: {pinchDistance:F0}, midpoint: {midpoint}");
            return;
        }

        float pinchDelta = pinchDistance - lastPinchDistance;
        Vector2 panDelta = midpoint - lastPanMidpoint;

        ApplyPinch(pinchDelta);
        ApplyPan(panDelta);

        lastPinchDistance = pinchDistance;
        lastPanMidpoint = midpoint;
    }

    // ============================================================
    // INPUT — MOUSE (editor convenience)
    // ============================================================

    private void ReadMouseInput()
    {
        if (Mouse.current == null) return;

        // Scroll wheel = zoom.
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            ApplyPinch(scroll * 0.5f);
        }

        // Middle drag = pan. Track press transitions ourselves so we don't
        // jump on the first frame of a press.
        bool middleDown = Mouse.current.middleButton.isPressed;
        Vector2 currentMousePos = Mouse.current.position.ReadValue();

        if (middleDown && !wasMiddleMouseDown)
        {
            // Just pressed. Capture initial position, no pan this frame.
            lastPanMidpoint = currentMousePos;
        }
        else if (middleDown && wasMiddleMouseDown)
        {
            // Continuing a drag.
            Vector2 panDelta = currentMousePos - lastPanMidpoint;
            ApplyPan(panDelta);
            lastPanMidpoint = currentMousePos;
        }

        wasMiddleMouseDown = middleDown;
    }

    // ============================================================
    // GESTURE APPLICATION
    // ============================================================

    private void ApplyPinch(float pinchDelta)
    {
        currentDistance -= pinchDelta * zoomSpeed;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
    }

    /// <summary>
    /// Pan the focal point along the WORLD horizontal plane (XZ), not along
    /// the camera's local axes. This keeps panning feeling like "dragging the
    /// world across the floor" regardless of the camera's tilt.
    ///
    /// We compute world-forward by projecting the camera's forward direction
    /// onto the XZ plane. World-right is then the cross product with up.
    /// Both vectors are normalized so pan speed is consistent regardless
    /// of camera tilt.
    /// </summary>
    private void ApplyPan(Vector2 panDelta)
    {
        // Camera's forward direction projected onto the ground plane.
        Vector3 cameraForward = transform.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.001f)
        {
            // Camera is looking straight down. Use world Z as forward fallback.
            cameraForward = Vector3.forward;
        }
        cameraForward.Normalize();

        // World-right perpendicular to ground-forward.
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);

        // Negate both axes because dragging right should move the world LEFT
        // (camera focal point moves right, but visually the world drifts left
        // — like dragging a map). Same for up/forward.
        Vector3 worldDelta = (-cameraRight * panDelta.x + -cameraForward * panDelta.y) * panSpeed;

        Vector3 candidateOffset = panOffset + worldDelta;
        if (candidateOffset.magnitude > maxPanDistance)
        {
            candidateOffset = candidateOffset.normalized * maxPanDistance;
        }
        panOffset = candidateOffset;
    }

    // ============================================================
    // CAMERA POSITION UPDATE
    // ============================================================

    private void UpdateCameraTransform()
    {
        if (player != null && autoFollow)
        {
            smoothedFocalPoint = Vector3.Lerp(
                smoothedFocalPoint,
                player.position,
                autoFollowSpeed * Time.deltaTime
            );
            focalAnchor = smoothedFocalPoint;
        }

        Vector3 effectiveFocalPoint = focalAnchor + panOffset;
        transform.position = effectiveFocalPoint + cameraDirectionLocal * currentDistance;
        transform.rotation = defaultRotation;
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary>
    /// Snap the camera back to its default state: zoom distance reset,
    /// pan offset cleared, and (if auto-follow is on) re-centered on player.
    /// Wire this to a UI button via OnClick.
    /// </summary>
    public void ResetView()
    {
        Log("ResetView called.");
        currentDistance = defaultDistance;
        panOffset = Vector3.zero;
        if (player != null) smoothedFocalPoint = player.position;
        else smoothedFocalPoint = focalAnchor;
    }

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;
    public bool IsInputEnabled => inputEnabled;

    public void SetAutoFollow(bool enabled) => autoFollow = enabled;
    public bool IsAutoFollowEnabled => autoFollow;

    public Vector3 FocalPoint => focalAnchor + panOffset;
    public float CurrentDistance => currentDistance;

    // ============================================================
    // LOGGING
    // ============================================================

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[PlayerCamera:{name}] {message}");
    }
}