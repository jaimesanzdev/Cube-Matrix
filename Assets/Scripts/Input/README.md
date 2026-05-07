# Mobile Input

Touchscreen input for the cube roll system. Adds two coexisting mobile
input methods alongside the existing keyboard:

- **Swipe** — quick-flick gestures (one swipe = one roll)
- **D-pad** — on-screen directional buttons with optional hold-to-repeat
  and drag-to-reposition

Both methods are fully additive and optional. The Player can have neither,
either, or both. Per-component Inspector toggles allow disabling without
removing the component.

Author: Kane

---

## What it does

### Swipe (SwipeInputProvider)
Detects four-direction swipes (up/down/left/right) on the touchscreen and
fires a roll for each completed flick. Lifts finger between swipes — does
NOT auto-repeat while held.

Touches that begin over a UI element (like the D-pad buttons) are ignored
by the swipe detector, so swipe and D-pad don't double-fire.

### D-pad (DPadController)
On-screen buttons that fire rolls when pressed. Two activation modes:

- **On-press (default):** tap = one roll, lift and tap again to roll again
- **Hold-to-repeat:** hold a button = repeated rolls at a configurable interval

Players can drag the D-pad to a different screen position by long-pressing
its background area. The new position persists across sessions via
PlayerPrefs.

### Both at once
Both can be enabled simultaneously with no conflict. The cube only responds
to one input at a time anyway because of the `isMoving` gate inside
TryRoll, and the swipe detector ignores touches that start over UI buttons.

---

## Quick start

### Swipe only
1. Drop `SwipeInputProvider.cs` into `Assets/Scripts/Input/`.
2. Pull in the updated `CubeRollMovement.cs`.
3. Select the Player. **Add Component → Swipe Input Provider**.
4. Press Play.

### D-pad — building the canvas in your scene
1. Drop `DPadController.cs` into `Assets/Scripts/Input/`.
2. Pull in the updated `CubeRollMovement.cs`.
3. **GameObject → UI → Canvas**. Set its **Render Mode** to **Screen Space - Overlay**.
   This is critical — see "Camera-independent rendering" below.
4. Add a child Panel (or empty GameObject with RectTransform), name it
   "DPadPanel". Anchor it bottom-left, size ~300x300.
5. Add an Image component to the Panel (alpha 0 if you don't want it visible
   — needed for drag-to-reposition raycast targets).
6. Add four child buttons to the Panel via **GameObject → UI → Button**.
   Name them exactly: `UpButton`, `DownButton`, `LeftButton`, `RightButton`.
   Position them in a cross shape (~70x70 each).
7. Add `DPadController` component to the Panel.
8. Drag the Player from Hierarchy into the **Cube Roll Movement** field.
9. Press Play. Tap the buttons.

### Both
Do all of the above. They coexist with no extra wiring.

If neither component is present, the game runs as before with keyboard input.

---

## Camera-independent rendering

The D-pad uses **Screen Space - Overlay** mode, which means the D-pad
renders directly onto the screen as a final pass — completely independent
of any camera in the scene.

This means:
- Teammates with different camera setups (top-down, tilted, follow,
  fixed) can drop in the D-pad and it works without changes.
- The D-pad position is fixed to screen coordinates, not world
  coordinates.
- Moving, rotating, or zooming the camera has zero effect on the D-pad.

If the D-pad disappears when the camera moves, the Canvas Render Mode
was probably set to Screen Space - Camera or World Space by mistake.
Switch it back to Screen Space - Overlay.

---

## Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Input/SwipeInputProvider.cs` | Quick-flick swipe detection on the Player. |
| `Assets/Scripts/Input/DPadController.cs` | On-screen D-pad with optional hold-to-repeat and drag-to-reposition. |
| `Assets/Scripts/CubeRollMovement.cs` | Modified — extracts a public TryRoll method so input sources route through one entry point. |

The D-pad scaffold lives directly in the scene (not as a separate prefab
in this PR). Future work could prefab it for drop-in reuse across scenes.

---

## Modifications to CubeRollMovement.cs

Existing keyboard logic preserved exactly. The change is structural:

1. Roll-trigger logic moved out of `Update()` into a new public method
   `TryRoll(Vector3 direction)`.
2. `Update()` still reads the keyboard the same way, just delegates the
   roll trigger to `TryRoll`.
3. `TryRoll` is `public` so external input sources can call it directly.

All gate checks (`isMoving`, `IsPlayingBump`, `CanMove`), pushable-block
handling, and rejection feedback live inside `TryRoll`. Every input path
— keyboard, swipe, D-pad, future sources — produces identical behavior.

If neither `SwipeInputProvider` nor `DPadController` is present in the
scene, `TryRoll` is still called every frame from the keyboard reader,
exactly as before. The change is backward-compatible.

---

## Button discovery (D-pad)

The DPadController finds its buttons **by name at runtime**, not via
Inspector drag-and-drop. Each button GameObject must:

1. Be a direct child of the Panel that holds DPadController.
2. Be named exactly to match the corresponding name field in the Inspector
   (defaults: `UpButton`, `DownButton`, `LeftButton`, `RightButton` —
   case-sensitive).
3. Have a `UnityEngine.UI.Button` component attached.

If a button isn't found, that direction is silently skipped (no crash) and
a warning is logged when verbose logging is enabled. The other directions
continue working.

The button name fields are exposed in the Inspector under "Button Discovery
(by name)" so designers can rename buttons without code changes.

---

## Editor testing without a touchscreen

### Mouse-as-touch simulation
1. Window → Analysis → Input Debugger
2. Click **Options** → check **Simulate Touch Input From Mouse or Pen**
3. Click and drag in the Game view — counts as a swipe
4. Click on D-pad buttons — counts as a touch press

### Device Simulator
1. Window → General → Device Simulator
2. The Game view becomes a simulated phone
3. Mouse drags act as finger swipes within the simulated screen

Both work with these components. Tested and verified working in editor
and on physical Android device (Galaxy A17 5G).

---

## Tuning

### SwipeInputProvider
- **Input Enabled** — master toggle
- **Min Swipe Distance** (pixels) — how far the finger must travel
- **Max Swipe Duration** (seconds) — time limit for a quick flick
- **Verbose Logging** — log swipe events to Console

### DPadController
- **Input Enabled** — master toggle
- **Hold To Repeat** — switch between on-press and hold-to-repeat modes
- **Repeat Interval** (seconds) — speed of repeat rolls when held
- **Repeat Delay** (seconds) — pause before repetition begins
- **Up/Down/Left/Right Button Name** — name of each child button GameObject
- **Allow Drag** — enable drag-to-reposition
- **Drag Hold Threshold** (seconds) — long-press duration to start drag
- **Position Prefs Key** — PlayerPrefs key for persisted position
- **Verbose Logging** — log D-pad events to Console

---

## Architecture notes

### Why TryRoll instead of an input-provider interface

A formal interface (`IInputDirectionProvider` with a list-walker in
CubeRollMovement) was considered and rejected. A single public
`TryRoll(direction)` method is the smallest API that lets new input
sources hook in without touching CubeRollMovement's existing keyboard
logic. New input sources just call `TryRoll`. No abstraction layer to
maintain. If multiple new sources land later, refactoring to a provider
list is small because they're all calling `TryRoll` anyway.

### Why find buttons by name instead of Inspector drag-and-drop

Originally the script used `[SerializeField] private Button upButton;`
fields and Inspector drag-and-drop. This caused a hard-to-debug type
collision: the project has a `Button.cs` script in the global namespace
that conflicts with `UnityEngine.UI.Button`. The C# resolver matched the
global one, making the Inspector reject Unity UI Button drops with a "no"
symbol.

Switching to runtime find-by-name avoids the issue entirely — the script
explicitly references `UnityEngine.UI.Button` via fully-qualified name
when calling `GetComponent<UnityEngine.UI.Button>()`, and there's nothing
to drag in the Inspector. Trade-off: button names must match the
Inspector strings exactly.

**For future work:** if the project's namespace situation gets cleaned
up (the existing `Button.cs` renamed or wrapped in a namespace), this
script could revert to Inspector references for cleaner editor workflow.
The find-by-name approach is documented as a defensive choice, not the
ideal long-term pattern.

### Why swipe ignores touches over UI

Without UI filtering, tapping a D-pad button would fire both the button's
own logic AND begin a swipe gesture. The swipe detector consults the
EventSystem on each touch start; if the touch lands on a UI element
(D-pad button, future pause button, hint indicator, etc.), the whole
gesture is ignored by the swipe tracker. The button still fires through
the standard UI event flow.

### Why the D-pad has its own activation toggle separate from inputEnabled

`Hold To Repeat` controls _how_ the D-pad fires (one roll vs continuous).
`Input Enabled` controls _whether_ it fires at all. Different concerns,
different toggles. A future settings menu could expose both to the player
("D-pad on/off" and "Hold to repeat on/off") without needing both to be
in one option.

### Drag-to-reposition uses long-press, not a separate "edit mode"

The simplest UX is one where the player can adjust the layout without
entering and exiting an edit mode. Long-press-and-drag is a familiar
gesture (most mobile OSes use it for icon rearrangement) and doesn't
require additional UI. The threshold (default 0.5s) is long enough not
to interfere with normal button presses but short enough to feel
responsive when the player wants to move it.

### Why PlayerPrefs for position persistence

PlayerPrefs is Unity's lightweight key-value store, perfect for small
preferences like UI position. No save-system integration required, no
new file, persists across runs automatically. Limitation: single
profile. If the project later adds multi-profile support, the
positionPrefsKey field can be appended with a profile ID.

A `ResetPosition()` public method clears the saved position so the D-pad
returns to its design-time position on next launch. Useful for a future
"Reset Layout" settings option, or for clearing stale dev positions.

---

## Troubleshooting

### "GameObject 'UpButton' exists but has no Button component"
The button GameObject was created without a Button component. Either:
- It was created via "GameObject → UI → Image" instead of "GameObject → UI → Button"
- The Button component was removed at some point
Fix: select the button, Add Component → UI → Button.

### "Button 'UpButton' not found as a child of 'DPadPanel'"
The button name in the scene doesn't match the Up Button Name field in
the Inspector (case-sensitive), or the button isn't a direct child of
the Panel.

### D-pad disappears when I move the camera
Canvas Render Mode is set to Screen Space - Camera or World Space.
Change it to Screen Space - Overlay.

### D-pad appears off-screen at runtime
A previous test session saved a drag position via PlayerPrefs that's
now off-screen for your current resolution. Call `ResetPosition()` on
the controller, or temporarily uncheck Allow Drag and edit the
RectTransform position in the Inspector to a known-good value.

### Swipe and D-pad both fire from a single tap
The swipe detector should ignore touches over UI. Verify the EventSystem
GameObject exists in the scene Hierarchy (Unity normally auto-creates it
when you add a Canvas).

---

## Limitations and future ideas

- **No diagonal swipes / rolls.** Movement is cardinal-only by design.
- **No swipe input buffering.** Swipes during an in-progress roll are
  silently dropped. A small "next move" buffer could be added for
  tighter feel.
- **No haptic on successful input.** RollFeedback handles haptics on
  rejected rolls. Successful rolls currently produce no haptic. Easy to
  add as a separate concern.
- **D-pad visuals are basic.** Default Unity UI buttons. Re-skin freely
  with sprites/animations to match game style.
- **Camera-relative input is deferred** until a follow camera with
  rotation exists. Currently swipes and D-pad both produce world-space
  directions, which feels natural with a fixed top-down camera.
- **D-pad position is screen-resolution-dependent.** The PlayerPrefs
  approach saves anchored position in pixels. On a different aspect
  ratio (tablet vs phone), the saved position may land off-screen. A
  more robust approach would save position as a normalized (0–1)
  fraction of screen size; flagged for a follow-up.
- **Find-by-name is fragile.** Renaming a button silently breaks that
  direction. Inspector references would be more robust once the
  project's namespace conflicts are resolved.
- **Settings menu integration not yet wired.** Both components expose
  `SetInputEnabled` for runtime toggling, but there's no UI yet.