using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// Quick-flick swipe input for mobile. Detects directional swipes on the
/// touchscreen and translates them into roll commands by calling
/// CubeRollMovement.TryRoll. One swipe = one roll. Lift finger between
/// swipes to roll again.
///
/// Architecture:
///   This component is fully additive. It lives on the Player alongside
///   CubeRollMovement and runs independently of the existing keyboard input
///   reader and the optional D-pad UI. All three input sources flow into
///   the same TryRoll() entry point, so all gate checks (isMoving,
///   IsPlayingBump, CanMove) and feedback (RollFeedback) work identically.
///
///   If this component isn't on the Player, the game behaves as before —
///   keyboard-only on desktop. Adding it enables swipe alongside everything.
///
/// UI awareness:
///   When the player taps a UI button (like the D-pad), the touch
///   technically begins on the screen and would be detected by this script
///   as the start of a swipe. To prevent that, every touch start is
///   checked against the EventSystem — if it landed on a UI element, this
///   tracker ignores the entire gesture. The D-pad button still fires its
///   own logic via Unity's UI system.
///
/// Detection model — quick flick:
///   1. Player touches the screen (on a non-UI area). Touch start recorded.
///   2. Player drags their finger.
///   3. When their finger has traveled at least Min Swipe Distance pixels
///      within Max Swipe Duration seconds, the gesture commits as a swipe.
///   4. The dominant axis (horizontal or vertical) determines direction.
///   5. Roll fires. The same touch cannot fire another roll — the player
///      must lift their finger and start a new touch.
///
/// Setup:
///   1. Drop this script into your scripts folder.
///   2. Select the Player GameObject.
///   3. Add Component → Swipe Input Provider.
///   4. Press Play. Swipe in any of four cardinal directions.
///
/// Editor testing without a touchscreen:
///   Window → Analysis → Input Debugger → Options → Simulate Touch Input
///   From Mouse or Pen. Then click-and-drag in the Game view.
/// </summary>
public class SwipeInputProvider : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Master toggle. Uncheck to disable swipe input entirely without " +
             "removing the component. Useful for settings menus or " +
             "per-scene input config.")]
    [SerializeField] private bool inputEnabled = true;

    [Header("Swipe Detection")]
    [Tooltip("Minimum finger travel distance (in pixels) to qualify as a swipe. " +
             "Lower = more sensitive. Higher = requires deliberate gesture. " +
             "50–100 is typical for a phone screen.")]
    [SerializeField] private float minSwipeDistance = 60f;

    [Tooltip("Maximum time (seconds) between touch start and swipe-distance threshold. " +
             "Slow drags past this duration are ignored — only quick flicks count. " +
             "0.5 is a comfortable default.")]
    [SerializeField] private float maxSwipeDuration = 0.5f;

    [Header("Routing")]
    [Tooltip("Reference to the CubeRollMovement to send swipe commands to. " +
             "If null, attempts to find one on this GameObject. If still null, " +
             "the component disables itself with a warning.")]
    [SerializeField] private CubeRollMovement cubeRollMovement;

    [Header("Debug")]
    [Tooltip("If checked, logs swipe detection events to the Console. Off for production.")]
    [SerializeField] private bool verboseLogging = false;

    // -- runtime state --
    private bool isTracking;
    private Vector2 touchStartPosition;
    private float touchStartTime;
    private bool currentTouchConsumed;

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Awake()
    {
        if (cubeRollMovement == null)
        {
            cubeRollMovement = GetComponent<CubeRollMovement>();
        }

        if (cubeRollMovement == null)
        {
            Debug.LogWarning("[SwipeInputProvider] No CubeRollMovement found. Disabling — " +
                             "this component must live on (or reference) a CubeRollMovement.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        // EnhancedTouchSupport powers Touch.activeTouches. Idempotent.
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        isTracking = false;
        currentTouchConsumed = false;
    }

    private void Update()
    {
        if (!inputEnabled) return;

        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        // No touches? Reset tracking state.
        if (touches.Count == 0)
        {
            if (isTracking) Log("Touch ended — resetting tracker.");
            isTracking = false;
            currentTouchConsumed = false;
            return;
        }

        // Use the first active touch. Multi-touch (camera pinch/twist) is
        // intentionally ignored by this script.
        var touch = touches[0];

        switch (touch.phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
                // Filter out touches that began over UI elements (e.g., D-pad
                // buttons). Without this, a D-pad tap would also start a
                // swipe gesture, potentially firing two rolls.
                if (IsTouchOverUI(touch.screenPosition))
                {
                    Log("Touch began over UI — ignoring for swipe detection.");
                    isTracking = false;
                    currentTouchConsumed = true;
                    return;
                }
                BeginTracking(touch.screenPosition);
                break;

            case UnityEngine.InputSystem.TouchPhase.Moved:
            case UnityEngine.InputSystem.TouchPhase.Stationary:
                if (isTracking && !currentTouchConsumed)
                {
                    EvaluateSwipe(touch.screenPosition);
                }
                break;

            case UnityEngine.InputSystem.TouchPhase.Ended:
            case UnityEngine.InputSystem.TouchPhase.Canceled:
                isTracking = false;
                currentTouchConsumed = false;
                break;
        }
    }

    // ============================================================
    // UI FILTERING
    // ============================================================

    /// <summary>
    /// Returns true if the given screen position is over a UI element that
    /// should consume the input (button, scroll area, etc.). Used to prevent
    /// swipe detection from firing when the player taps a UI control.
    /// </summary>
    private bool IsTouchOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    // ============================================================
    // SWIPE DETECTION
    // ============================================================

    private void BeginTracking(Vector2 startPosition)
    {
        isTracking = true;
        currentTouchConsumed = false;
        touchStartPosition = startPosition;
        touchStartTime = Time.time;
        Log($"Touch began at {startPosition}.");
    }

    private void EvaluateSwipe(Vector2 currentPosition)
    {
        Vector2 delta = currentPosition - touchStartPosition;
        float duration = Time.time - touchStartTime;

        if (duration > maxSwipeDuration)
        {
            currentTouchConsumed = true;
            Log("Touch exceeded max duration — marking as consumed (no swipe).");
            return;
        }

        if (delta.magnitude < minSwipeDistance)
        {
            return;
        }

        // Dominant axis determines direction. Screen Y up = world Z forward.
        Vector3 worldDirection;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            worldDirection = (delta.x > 0f) ? Vector3.right : Vector3.left;
        }
        else
        {
            worldDirection = (delta.y > 0f) ? Vector3.forward : Vector3.back;
        }

        Log($"Swipe detected: delta={delta}, duration={duration:F2}s, direction={worldDirection}.");

        cubeRollMovement.TryRoll(worldDirection);
        currentTouchConsumed = true;
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary>
    /// External enable/disable hook. Lets a future settings menu turn swipe
    /// input on/off without referencing the inspector field directly.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            isTracking = false;
            currentTouchConsumed = false;
        }
    }

    public bool IsInputEnabled => inputEnabled;

    // ============================================================
    // LOGGING
    // ============================================================

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[SwipeInputProvider:{name}] {message}");
    }
}