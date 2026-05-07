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
///   the same TryRoll() entry point.
///
/// Multi-touch cancellation:
///   When a second finger touches the screen mid-swipe, we ABANDON the
///   in-progress single-finger gesture without firing a roll. This prevents
///   the case where a player starts moving one finger toward a pinch/pan
///   gesture but accidentally triggers a roll before the second finger
///   lands. The two-finger gesture then becomes the camera's responsibility
///   (PlayerCamera reads two-finger input).
///
/// UI awareness:
///   When the player taps a UI button (like the D-pad), the touch
///   technically begins on the screen and would be detected by this script
///   as the start of a swipe. To prevent that, every touch start is
///   checked against the EventSystem — if it landed on a UI element, this
///   tracker ignores the entire gesture.
///
/// Detection model — quick flick:
///   1. Player touches the screen (on a non-UI area, single finger).
///   2. Player drags their finger.
///   3. When their finger has traveled at least Min Swipe Distance pixels
///      within Max Swipe Duration seconds, the gesture commits as a swipe.
///   4. The dominant axis (horizontal or vertical) determines direction.
///   5. Roll fires.
///   The same touch cannot fire another roll — the player must lift their
///   finger and start a new touch. If a second finger touches before the
///   swipe commits, the gesture is cancelled.
/// </summary>
public class SwipeInputProvider : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Master toggle. Uncheck to disable swipe input entirely.")]
    [SerializeField] private bool inputEnabled = true;

    [Header("Swipe Detection")]
    [Tooltip("Minimum finger travel distance (pixels) to qualify as a swipe.")]
    [SerializeField] private float minSwipeDistance = 60f;

    [Tooltip("Maximum time (seconds) between touch start and swipe-distance threshold. " +
             "Slow drags past this are ignored.")]
    [SerializeField] private float maxSwipeDuration = 0.5f;

    [Header("Routing")]
    [Tooltip("Reference to the CubeRollMovement to send swipe commands to.")]
    [SerializeField] private CubeRollMovement cubeRollMovement;

    [Header("Debug")]
    [Tooltip("If checked, logs swipe detection events to the Console.")]
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
            Debug.LogWarning("[SwipeInputProvider] No CubeRollMovement found. Disabling.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
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

        // Multi-touch cancellation: if a second finger arrives mid-swipe,
        // abandon the gesture. The two-finger gesture is the camera's
        // territory; we don't want to fire a roll based on the partial
        // single-finger motion that happened before the second finger.
        if (touches.Count >= 2)
        {
            if (isTracking && !currentTouchConsumed)
            {
                Log("Second finger detected mid-swipe — cancelling gesture.");
            }
            isTracking = false;
            currentTouchConsumed = true;  // mark as consumed so we don't re-track when it goes back to 1
            return;
        }

        // No touches? Reset tracking state.
        if (touches.Count == 0)
        {
            if (isTracking) Log("Touch ended — resetting tracker.");
            isTracking = false;
            currentTouchConsumed = false;
            return;
        }

        // Exactly one touch: standard swipe detection.
        var touch = touches[0];

        switch (touch.phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
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
            Log("Touch exceeded max duration — marking as consumed.");
            return;
        }

        if (delta.magnitude < minSwipeDistance)
        {
            return;
        }

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