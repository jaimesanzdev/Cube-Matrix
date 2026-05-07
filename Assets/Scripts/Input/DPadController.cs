using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;

/// <summary>
/// On-screen D-pad input for mobile (and desktop testing). Four directional
/// buttons that call CubeRollMovement.TryRoll. Lives on a Canvas
/// (Screen Space - Overlay typically), independent of the Player GameObject.
///
/// Architecture:
///   This component is fully additive. It coexists with SwipeInputProvider
///   and the keyboard input reader without conflict — all three input
///   sources flow into TryRoll, which gates and routes through identical
///   logic. Per-component enable toggles let designers disable any
///   combination from the Inspector or via SetInputEnabled at runtime.
///
/// Activation models:
///   - On-Press (default): tap a button = one roll. Lift and tap again to
///     roll again. Mirrors swipe semantics — one gesture, one move.
///   - Hold-to-Repeat: hold a button down to roll repeatedly at the
///     configured interval. Mirrors keyboard hold-to-repeat behavior.
///
/// Drag-to-reposition:
///   When Allow Drag is enabled, the player can long-press anywhere inside
///   the D-pad's overall bounds (the parent panel, NOT the buttons) and
///   drag to move the entire D-pad. Position persists across sessions via
///   PlayerPrefs.
///
/// Button discovery:
///   Buttons are resolved by NAME at runtime in Awake. The script looks for
///   four child GameObjects of the Panel named "UpButton", "DownButton",
///   "LeftButton", and "RightButton", each with a UnityEngine.UI.Button
///   component. Names are configurable in the Inspector if your scene uses
///   a different convention. If a button isn't found, that direction is
///   silently disabled (no crash) and a warning is logged when verbose
///   logging is on.
///
/// Setup:
///   1. Build a DPadCanvas in the scene with a Canvas (Screen Space - Overlay).
///   2. Add a Panel child (300x300 anchored bottom-left, for example).
///   3. Add four Button children to the Panel, named "UpButton", "DownButton",
///      "LeftButton", "RightButton". Position them in a cross shape.
///   4. Add this component to the Panel.
///   5. Assign the Cube Roll Movement reference in the Inspector
///      (drag the Player from Hierarchy).
///   6. Press Play.
/// </summary>
public class DPadController : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Master toggle. Uncheck to disable D-pad input entirely. " +
             "The buttons remain visible but won't trigger rolls.")]
    [SerializeField] private bool inputEnabled = true;

    [Header("Activation Mode")]
    [Tooltip("If checked, holding a button down rolls repeatedly at Repeat " +
             "Interval. If unchecked, each press fires exactly one roll. " +
             "Default off (matches swipe semantics for consistency).")]
    [SerializeField] private bool holdToRepeat = false;

    [Tooltip("Used only when Hold To Repeat is enabled. Seconds between " +
             "repeated rolls while a button is held. 0.25 is a typical " +
             "comfortable rate.")]
    [SerializeField] private float repeatInterval = 0.25f;

    [Tooltip("Used only when Hold To Repeat is enabled. Seconds the player " +
             "must hold before repetition starts. Initial press always fires " +
             "immediately; this delay is before the second roll.")]
    [SerializeField] private float repeatDelay = 0.4f;

    [Header("Button Discovery (by name)")]
    [Tooltip("Name of the up-direction button child of this Panel. " +
             "Matches the GameObject name in the Hierarchy exactly (case-sensitive).")]
    [SerializeField] private string upButtonName = "UpButton";
    [SerializeField] private string downButtonName = "DownButton";
    [SerializeField] private string leftButtonName = "LeftButton";
    [SerializeField] private string rightButtonName = "RightButton";

    [Header("Routing")]
    [Tooltip("Reference to the CubeRollMovement to send D-pad commands to. " +
             "Drag the Player from the Hierarchy here.")]
    [SerializeField] private CubeRollMovement cubeRollMovement;

    [Header("Drag-to-Reposition")]
    [Tooltip("If checked, the player can long-press the D-pad's background " +
             "and drag to move the entire pad to a new screen position. " +
             "Position persists across sessions via PlayerPrefs.")]
    [SerializeField] private bool allowDrag = true;

    [Tooltip("Seconds the player must press-and-hold before drag begins. " +
             "Long enough to not interfere with normal button presses. " +
             "0.5s is a good default.")]
    [SerializeField] private float dragHoldThreshold = 0.5f;

    [Tooltip("PlayerPrefs key used to persist the D-pad's position across " +
             "sessions.")]
    [SerializeField] private string positionPrefsKey = "DPad_Position";

    [Header("Debug")]
    [Tooltip("If checked, logs D-pad events to the Console. Off for production.")]
    [SerializeField] private bool verboseLogging = false;

    // -- runtime state --
    private RectTransform rectTransform;
    private UnityEngine.UI.Button upButton;
    private UnityEngine.UI.Button downButton;
    private UnityEngine.UI.Button leftButton;
    private UnityEngine.UI.Button rightButton;
    private Coroutine[] holdCoroutines = new Coroutine[4]; // 0=up, 1=down, 2=left, 3=right
    private bool isDraggingPad;
    private Vector2 dragStartPointerPosition;
    private Vector2 dragStartPadPosition;
    private float dragHoldStartTime;
    private bool dragHoldActive;

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (cubeRollMovement == null)
        {
            Debug.LogWarning("[DPadController] No CubeRollMovement assigned. " +
                             "D-pad will detect input but won't trigger rolls.", this);
        }

        // Find buttons by name. Each is wired to its direction if found,
        // silently skipped (with warning) if not.
        upButton = ResolveButton(upButtonName);
        downButton = ResolveButton(downButtonName);
        leftButton = ResolveButton(leftButtonName);
        rightButton = ResolveButton(rightButtonName);

        WireUpButton(upButton, Vector3.forward, 0);
        WireUpButton(downButton, Vector3.back, 1);
        WireUpButton(leftButton, Vector3.left, 2);
        WireUpButton(rightButton, Vector3.right, 3);

        LoadSavedPosition();
    }

    private void OnEnable()
    {
        // EnhancedTouchSupport powers Touch.activeTouches. Idempotent.
        EnhancedTouchSupport.Enable();
    }

    private void OnDestroy()
    {
        UnwireButton(upButton);
        UnwireButton(downButton);
        UnwireButton(leftButton);
        UnwireButton(rightButton);
    }

    // ============================================================
    // BUTTON DISCOVERY
    // ============================================================

    /// <summary>
    /// Look up a child GameObject by name and extract its Button component.
    /// Searches direct children only (not deep descendants) so the script
    /// matches the documented hierarchy: Panel → buttons. Returns null if
    /// not found, which silently disables that direction.
    /// </summary>
    private UnityEngine.UI.Button ResolveButton(string buttonName)
    {
        Transform child = transform.Find(buttonName);
        if (child == null)
        {
            Log($"Button '{buttonName}' not found as a child of '{name}'. " +
                $"That direction will not be wired.");
            return null;
        }

        UnityEngine.UI.Button button = child.GetComponent<UnityEngine.UI.Button>();
        if (button == null)
        {
            Log($"GameObject '{buttonName}' exists but has no Button component. " +
                $"That direction will not be wired.");
            return null;
        }

        Log($"Resolved button '{buttonName}'.");
        return button;
    }

    // ============================================================
    // BUTTON WIRING
    // ============================================================

    /// <summary>
    /// Attach press/release handlers to a button. Uses EventTrigger rather
    /// than Button.onClick because we need press-vs-release distinction for
    /// hold-to-repeat. Button.onClick only fires on full click (down + up).
    /// </summary>
    private void WireUpButton(UnityEngine.UI.Button button, Vector3 direction, int directionIndex)
    {
        if (button == null) return;

        var trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var pressEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pressEntry.callback.AddListener((_) => OnButtonPressed(direction, directionIndex));
        trigger.triggers.Add(pressEntry);

        var releaseEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        releaseEntry.callback.AddListener((_) => OnButtonReleased(directionIndex));
        trigger.triggers.Add(releaseEntry);

        // Pointer exit also acts as release — covers the case where the
        // player slides their finger off the button while still touching.
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => OnButtonReleased(directionIndex));
        trigger.triggers.Add(exitEntry);
    }

    private void UnwireButton(UnityEngine.UI.Button button)
    {
        if (button == null) return;
        var trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();
    }

    // ============================================================
    // BUTTON BEHAVIOR
    // ============================================================

    private void OnButtonPressed(Vector3 direction, int directionIndex)
    {
        if (!inputEnabled) return;
        if (cubeRollMovement == null) return;

        Log($"Button pressed: {direction}");

        cubeRollMovement.TryRoll(direction);

        if (holdToRepeat)
        {
            if (holdCoroutines[directionIndex] != null)
            {
                StopCoroutine(holdCoroutines[directionIndex]);
            }
            holdCoroutines[directionIndex] = StartCoroutine(HoldRepeatLoop(direction, directionIndex));
        }
    }

    private void OnButtonReleased(int directionIndex)
    {
        if (holdCoroutines[directionIndex] != null)
        {
            StopCoroutine(holdCoroutines[directionIndex]);
            holdCoroutines[directionIndex] = null;
        }
    }

    private IEnumerator HoldRepeatLoop(Vector3 direction, int directionIndex)
    {
        yield return new WaitForSeconds(repeatDelay);
        while (true)
        {
            if (cubeRollMovement != null) cubeRollMovement.TryRoll(direction);
            yield return new WaitForSeconds(repeatInterval);
        }
    }

    // ============================================================
    // DRAG-TO-REPOSITION
    // ============================================================

    private void Update()
    {
        if (!allowDrag) return;
        UpdateDragState();
    }

    private void UpdateDragState()
    {
        Vector2 currentPointerPosition;
        bool pointerDown;

        if (!TryGetPointerState(out currentPointerPosition, out pointerDown))
        {
            return;
        }

        if (!pointerDown)
        {
            if (isDraggingPad)
            {
                EndDrag();
            }
            dragHoldActive = false;
            return;
        }

        if (isDraggingPad)
        {
            Vector2 delta = currentPointerPosition - dragStartPointerPosition;
            rectTransform.anchoredPosition = dragStartPadPosition + delta;
            return;
        }

        if (!dragHoldActive)
        {
            if (!IsPointerInPadBackground(currentPointerPosition)) return;
            dragHoldActive = true;
            dragHoldStartTime = Time.time;
            dragStartPointerPosition = currentPointerPosition;
            dragStartPadPosition = rectTransform.anchoredPosition;
            return;
        }

        if (Time.time - dragHoldStartTime >= dragHoldThreshold)
        {
            BeginDrag();
        }
    }

    /// <summary>
    /// Read pointer position and pressed state from the new Input System.
    /// Prefers touchscreen on mobile, falls back to mouse for editor/desktop.
    /// </summary>
    private bool TryGetPointerState(out Vector2 position, out bool isPressed)
    {
        if (Touchscreen.current != null)
        {
            var primaryTouch = Touchscreen.current.primaryTouch;
            if (primaryTouch.press.isPressed)
            {
                position = primaryTouch.position.ReadValue();
                isPressed = true;
                return true;
            }
        }

        if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            isPressed = Mouse.current.leftButton.isPressed;
            return true;
        }

        position = Vector2.zero;
        isPressed = false;
        return false;
    }

    /// <summary>
    /// True if the pointer is inside the D-pad rect AND not over one of the
    /// child buttons. Lets long-press on the background drag the pad,
    /// while keeping button presses behaving normally.
    /// </summary>
    private bool IsPointerInPadBackground(Vector2 screenPosition)
    {
        if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition))
            return false;

        if (IsPointerOverButton(upButton, screenPosition)) return false;
        if (IsPointerOverButton(downButton, screenPosition)) return false;
        if (IsPointerOverButton(leftButton, screenPosition)) return false;
        if (IsPointerOverButton(rightButton, screenPosition)) return false;

        return true;
    }

    private bool IsPointerOverButton(UnityEngine.UI.Button button, Vector2 screenPosition)
    {
        if (button == null) return false;
        var rt = button.GetComponent<RectTransform>();
        return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPosition);
    }

    private void BeginDrag()
    {
        isDraggingPad = true;
        Log("Drag started.");
    }

    private void EndDrag()
    {
        isDraggingPad = false;
        SaveCurrentPosition();
        Log("Drag ended — position saved.");
    }

    // ============================================================
    // POSITION PERSISTENCE
    // ============================================================

    private void SaveCurrentPosition()
    {
        Vector2 pos = rectTransform.anchoredPosition;
        PlayerPrefs.SetFloat(positionPrefsKey + "_X", pos.x);
        PlayerPrefs.SetFloat(positionPrefsKey + "_Y", pos.y);
        PlayerPrefs.Save();
    }

    private void LoadSavedPosition()
    {
        if (!PlayerPrefs.HasKey(positionPrefsKey + "_X")) return;
        float x = PlayerPrefs.GetFloat(positionPrefsKey + "_X");
        float y = PlayerPrefs.GetFloat(positionPrefsKey + "_Y");
        Vector2 candidate = new Vector2(x, y);

        // Sanity-check that the saved position would actually keep the D-pad
        // on-screen for the current resolution/orientation. PlayerPrefs saves
        // raw anchoredPosition pixels — if the user saved a position on a
        // larger screen (e.g., editor) and then ran on a smaller one (e.g.,
        // portrait phone), or rotated the device between sessions, the pad
        // could land off-screen and become unreachable.
        //
        // Strategy: temporarily apply the candidate position, compute the
        // pad's screen-space rect, and check if any portion of it intersects
        // the screen. If not, revert to the design-time position (already in
        // place since this method runs in Awake before any other position
        // changes).
        Vector2 originalPosition = rectTransform.anchoredPosition;
        rectTransform.anchoredPosition = candidate;

        if (!IsOnScreen(rectTransform))
        {
            // Saved position is off-screen for this resolution. Revert to the
            // design-time position and clear the bad save so we don't keep
            // hitting this every launch.
            rectTransform.anchoredPosition = originalPosition;
            PlayerPrefs.DeleteKey(positionPrefsKey + "_X");
            PlayerPrefs.DeleteKey(positionPrefsKey + "_Y");
            PlayerPrefs.Save();
            Log($"Saved position ({x}, {y}) would be off-screen for current " +
                $"resolution ({Screen.width}x{Screen.height}). Reverted to " +
                $"design-time position and cleared the saved value.");
            return;
        }

        Log($"Loaded saved position: ({x}, {y}).");
    }

    /// <summary>
    /// True if any portion of the given RectTransform overlaps the visible
    /// screen area. Used to validate that a loaded position from PlayerPrefs
    /// won't leave the D-pad inaccessible.
    /// </summary>
    private bool IsOnScreen(RectTransform rt)
    {
        // Get the four screen-space corners of the rect. Using a null camera
        // because the D-pad lives on a Screen Space - Overlay canvas (no
        // camera projection involved).
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);

        // If any corner is on-screen, the pad is at least partially reachable.
        // Stricter check (require ALL corners on-screen) would reject cases
        // where the pad is mostly visible but slightly clipped — too aggressive.
        foreach (Vector3 corner in corners)
        {
            if (screenRect.Contains(corner)) return true;
        }

        // Also check if the screen center is inside the rect — handles the
        // case where the pad is huge and surrounds the screen (corners
        // outside but middle on-screen). Edge case, but cheap to cover.
        if (RectTransformUtility.RectangleContainsScreenPoint(rt,
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)))
        {
            return true;
        }

        return false;
    }

    public void ResetPosition()
    {
        PlayerPrefs.DeleteKey(positionPrefsKey + "_X");
        PlayerPrefs.DeleteKey(positionPrefsKey + "_Y");
        PlayerPrefs.Save();
        Log("Saved position cleared. Will use design-time position next session.");
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            for (int i = 0; i < holdCoroutines.Length; i++)
            {
                if (holdCoroutines[i] != null)
                {
                    StopCoroutine(holdCoroutines[i]);
                    holdCoroutines[i] = null;
                }
            }
        }
    }

    public bool IsInputEnabled => inputEnabled;

    // ============================================================
    // LOGGING
    // ============================================================

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[DPadController:{name}] {message}");
    }
}