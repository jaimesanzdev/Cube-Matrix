# Player Camera

Two-finger camera control: pinch-to-zoom, ground-aligned two-finger pan, and an optional auto-follow that smoothly tracks the player's position. Includes a `ResetView` public method that can be wired to a UI button.

Author: Kane

---

## What it does

- **Pinch** with two fingers — zoom in/out (clamped between Min and Max Distance)
- **Drag** with two fingers — pan the focal point along the world floor (ground-aligned, regardless of camera tilt)
- **Reset View** — snap back to the default zoom, clear pan offset, re-center on player (if auto-follow is on)
- **Auto-Follow** (optional, default off) — smoothly tracks player position while preserving the player's pan offset
- **Mouse fallback** in the editor — scroll wheel = zoom, middle-drag = pan

Single-finger touches are intentionally ignored. SwipeInputProvider reads single-finger touches for cube movement; this component only reads when two fingers are active. The two systems coexist cleanly.

**Rotation is intentionally NOT included.** Adding rotation creates a mismatch between the player's visual frame and the world-relative direction semantics that swipes and the D-pad currently use. Resolving that requires camera-relative input — a bigger architectural change. See "Why no rotation in v1" below.

---

## Quick start

1. Drop `PlayerCamera.cs` into `Assets/Scripts/Camera/`.
2. Select your scene's Main Camera (or whichever camera renders the play view).
3. Position and rotate it however you want the default view to look.
4. **Add Component → Player Camera**.
5. Drag the Player Transform from the Hierarchy into the Player field.
6. (Optional) Toggle Auto Follow on if you want the camera to track the player as they roll.
7. Press Play. Pinch and pan with two fingers (or use scroll/middle-drag in the editor).

Tested in Unity Editor with mouse fallback (scroll wheel + middle drag) and on physical Android device (Galaxy A17 5G) with two-finger pinch and pan gestures.

---

## Reset button

A Reset View button is recommended for player accessibility — players will inevitably drag the camera into a position they want to undo. Tapping the button calls `PlayerCamera.ResetView()` which restores default zoom and clears pan offset.

This PR includes a wired Reset button in the test scene (top-right corner of the DPadCanvas instance). It is intentionally NOT part of the DPadCanvas prefab — the button is only useful when PlayerCamera is in the scene, and bundling it into the prefab would give it to D-pad-only scenes where it would do nothing.

To add a Reset button to your own scene:

1. Right-click your DPadCanvas (or any active Canvas in your scene) → UI → Button
2. Name it "ResetButton" and position it somewhere out of the way — top-right corner is conventional
3. Click the button. Find its "On Click ()" section in the Inspector
4. Click the + to add an event
5. Drag your scene's Camera (with PlayerCamera attached) into the empty Object slot
6. In the function dropdown: PlayerCamera → ResetView ()

Tap the button during play to reset the camera to its default view.

---

## Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/PlayerCamera.cs` | Camera control component. |

This PR modifies one existing script (`SwipeInputProvider.cs`) — see "Modifications to SwipeInputProvider.cs" below.

---

## Modifications to SwipeInputProvider.cs

This PR also includes a small fix to SwipeInputProvider: when a second finger touches the screen mid-swipe, the in-progress single-finger gesture is cancelled instead of completing. This prevents accidental rolls when the player begins a two-finger camera gesture without perfect synchronization.

The fix is single-purpose and only matters when both swipe input and camera are present. Without camera, the multi-touch case never arises.

---

## Tuning

### Player Reference
- **Player** — Transform that the camera focuses on. If null, the camera uses the position it was at on Awake as its focal point.

### Zoom
- **Default Distance** — distance from focal point to camera at start. Reset returns to this value.
- **Min Distance / Max Distance** — clamps for pinch zoom.
- **Zoom Speed** — pinch sensitivity. Higher = bigger zoom changes per pixel of pinch motion.

### Pan
- **Pan Speed** — pan sensitivity. Higher = bigger pan motion per pixel of finger motion.
- **Max Pan Distance** — limits how far the player can pan from the focal anchor. Reset always returns pan offset to zero.

### Auto-Follow
- **Auto Follow** — toggle smooth player tracking on/off.
- **Auto Follow Speed** — lerp factor when following. Higher = catches up faster. 5 is smooth, 20 is near-instant.

### Editor Controls
- **Enable Mouse Controls** — when checked, mouse scroll zooms and middle-drag pans. Lets you test in the editor without a touchscreen.

### Debug
- **Verbose Logging** — log gesture events to Console.

---

## Architecture notes

### Why no rotation in v1

The cube's movement is currently world-relative: swipe up = world forward, press D-pad up = world forward. If the camera could rotate, the player's visual frame would rotate with it — but the input semantics wouldn't follow. Pressing up after a 90° camera rotation would still send the cube "world-forward" but visually that direction would now be "screen-right," which is confusing.

Resolving this requires camera-relative input — translating swipes and D-pad presses through the camera's yaw before passing to TryRoll. That's a bigger change touching SwipeInputProvider and DPadController, and it deserves a dedicated team conversation about the right approach.

For now, no rotation = no mismatch. Pinch and pan don't change the camera's orientation, so input semantics remain consistent and the player doesn't get confused.

### Why ground-aligned pan instead of screen-aligned

When the camera is tilted (not pure top-down), screen-aligned pan moves the focal point along the camera's local axes — which means dragging "up on screen" pushes the focal point partially up into the air, not just forward across the floor. The world appears to tilt awkwardly.

Ground-aligned pan projects the camera's forward direction onto the world XZ plane before applying pan deltas. Drag-up always moves the focal point world-forward across the ground. Drag-right always moves it world-right across the ground. Camera tilt becomes irrelevant — the world feels like a map being dragged underneath the camera.

### Why focal point + pan offset are tracked separately

If we tracked just one Vector3 for "where the camera looks," then auto-follow would constantly fight pan. The player would pan to scout an area, the camera would auto-follow back to the player, and the pan would feel laggy or get cancelled.

Instead we track:
- `focalAnchor` — the "automatic" focal point (player position, if auto-follow is on; or the initial position, if not)
- `panOffset` — the player's manual offset, never overwritten by tracking

The effective focal point is `focalAnchor + panOffset`. Auto-follow updates focalAnchor over time; pan updates panOffset directly. They compose without conflicting. Reset clears panOffset back to zero.

### Why the camera viewing angle is captured at Awake, not exposed

The camera's pitch/yaw angle (defined by its rotation in the scene) becomes the "default view angle" the moment the component starts. We preserve this angle throughout zoom/pan so the player always sees the world from the same perspective the level designer intended.

This is a design choice: "camera control" in this version means moving the focal point in space, not changing the viewing angle. If the team wants to add player-controllable angle later (tilt up/down), it's a clean addition without changing this base behavior.

### Why two fingers, not gestures

Could we use Unity's gesture recognition for pinch/pan? Yes. But raw two-finger reading is simpler, more predictable, and easier to debug. We're not detecting complex gestures — we're translating two finger positions into camera state every frame. Direct math beats gesture API for this.

### Why mouse fallback

Editor testing without a touchscreen is essential during development. Without the mouse fallback, you'd have to deploy to phone for every camera tweak. Scroll-zoom and middle-drag-pan are universal mouse-as-camera conventions that any teammate familiar with Maya, Blender, or Unity scene navigation will recognize instantly.

---

## Limitations and future ideas

- **No rotation.** Documented above. Deferred until camera-relative input is agreed on.
- **No tilt/pitch control.** The viewing angle is fixed at the design-time value. A pitch slider or three-finger up/down gesture could be added later.
- **Auto-follow is on/off only.** Could be improved with a "deadzone" (camera only follows when the player exits a region around screen center) for less jittery tracking.
- **No lookahead.** Some games anticipate movement by offsetting the focal point in the direction the player is heading. Could improve feel; not in scope here.
- **No persistence.** Camera state resets on scene reload. PlayerPrefs could save the last zoom/pan if the team wants that.

---

## Troubleshooting

### Camera doesn't move
Check that `Input Enabled` is checked in the Inspector. If using touch, verify Touchscreen.current is non-null (some emulators don't expose touch to Unity). If using mouse, verify `Enable Mouse Controls` is checked.

### Camera snaps to weird position on play
The camera's design-time position determines the default view. Make sure you've positioned and rotated the camera in the scene to look at the player or wherever you want the default view to be — that's the pose the component captures in Awake.

### Auto-follow feels jerky
Increase `Auto Follow Speed`. Lower values lag behind the player; higher values track immediately. 5–10 is usually good; 20+ is near-instant.

### Cube rolls accidentally during pinch / pan
Symptom: starting a single-finger touch and then bringing in a second finger triggers a roll before the camera takes over.

Cause: SwipeInputProvider was committing single-finger gestures even when a second finger was about to land. Fixed in this PR — the swipe detector now cancels in-progress gestures the moment a second touch arrives. If you still see this, verify SwipeInputProvider.cs has the multi-touch cancellation block in its Update method.

### Pan feels tilted or world appears to slide diagonally
You may have a stale screen-aligned pan implementation. Verify your `ApplyPan` method projects the camera's forward direction onto the world XZ plane before applying pan deltas (uses `cameraForward.y = 0`). Without ground-projection, panning a tilted camera moves the focal point along the camera's local up axis, which slides the world diagonally instead of flat.