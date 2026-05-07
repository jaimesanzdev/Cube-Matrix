# Roll Feedback

Visual, audio, and haptic feedback when the player attempts an invalid roll
(rolling into a wall, or trying to push a block that can't move).

Author: Kane

---

## What it does

When the cube tries to roll somewhere it can't:

- **Visual:** The cube tilts toward the blocked direction using the same
  pivot-and-rotate motion as a real roll, but only goes a fraction of the way
  before tipping back. Reads as "the cube tried to commit to the roll, then
  got bounced back."
- **Audio:** Plays a one-shot sound effect (typically a soft thud or bump).
  Optional — silently skipped if no AudioSource or clip is assigned.
- **Haptic:** Triggers device vibration on mobile (Android/iOS). Optional —
  silently skipped on platforms without haptic support.

The feedback fires for two rejection cases:
1. **No tile in the target direction** — the cube tried to roll off the floor.
2. **Pushable block can't be pushed** — the cube tried to push a block that
   was already moving or had no space behind it.

---

## Quick start

1. Drop `RollFeedback.cs` into `Assets/Scripts/` (or wherever Player scripts live).
2. Replace `CubeRollMovement.cs` with the updated version (additive change,
   detailed below).
3. Select the **Player** GameObject in your scene.
4. **Add Component → Roll Feedback**.
5. (Optional) Assign an `AudioSource` and a `Bump Sound` clip.
6. (Optional) Tune `Tip Angle` and `Bump Duration` in the Inspector.
7. Press Play. Roll into a wall — the cube should tip toward the wall and rock back.

If the component isn't attached to the Player, CubeRollMovement's null-guarded
call does nothing — the game behaves exactly as before. **Fully optional.**

---

## Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/RollFeedback.cs` | Component that orchestrates tip animation, audio, and haptics. |
| `Assets/Scripts/CubeRollMovement.cs` | Modified — adds null-safe calls into RollFeedback at the two rejection points. |

---

## Modifications to CubeRollMovement.cs

Three additive changes — no existing behavior modified:

1. **New private field** caching the optional component:
   ```csharp
   private RollFeedback rollFeedback;
   ```

2. **In `Start()`**, fetch the component:
   ```csharp
   rollFeedback = GetComponent<RollFeedback>();
   ```

3. **In `Update()`**, two new null-guarded calls:
   - In the wall-rejection branch (`else if (direction != Vector3.zero && rollFeedback != null)`)
   - In the pushable-block rejection branch (when `cube.IsMoving || !cube.CanMove(direction)`)

4. **One new gate at the top of `Update()`** to prevent real rolls from
   starting on top of an in-progress tip animation:
   ```csharp
   if (rollFeedback != null && rollFeedback.IsPlayingBump) return;
   ```

If `RollFeedback` isn't attached, every guarded call early-exits and the
script behaves identically to its pre-modification state.

---

## Tuning

All in the Inspector under the Roll Feedback component on the Player:

### Visual Tip
- **Tip Angle** (5°–45°) — how far the cube tilts. Default 20°. A full roll
  is 90°, so 20° is roughly a quarter of a real roll.
- **Bump Duration** — total animation length (tip out + tip back). Default 0.22s.
- **Cell Size** — should match `CubeRollMovement.cellSize` (default 1).

### Audio
- **Audio Source** — drag an AudioSource here, or leave blank to auto-find one
  on the Player.
- **Bump Sound** — the one-shot AudioClip to play.
- **Bump Volume** (0–1) — volume multiplier.

### Haptics
- **Use Haptics** — toggle device vibration on/off.

### Cooldown
- **Per Direction Cooldown** — minimum seconds before the same direction can
  re-fire feedback. Prevents audio/vibration spam when the player holds a key
  against a wall. Default 0.5s. Releasing the key or changing direction resets
  immediately.

### Debug
- **Verbose Logging** — log feedback events to the Console. Off by default.

---

## Architecture notes

### Why a separate component instead of inline in CubeRollMovement

Keeps the rejection-feedback logic out of the movement script's hot path. If
the team later wants to swap the visual style (e.g., particle effects instead
of a tip), or add other feedback types (screen shake, UI hint), they can edit
RollFeedback alone without risking regressions in CubeRollMovement.

### Why the per-direction cooldown

`Update()` fires every frame the player holds a key, so without a cooldown
the feedback would re-trigger at 60Hz — audio spam, vibration drain,
overlapping animations. The cooldown gates re-firing in the same direction
but resets immediately on direction change, so genuine new attempts get
fresh feedback.

### Why the IsPlayingBump gate

`RotateAround` modifies both position and rotation. If a real roll started
on top of an in-progress tip, the two coroutines would compound rotations
and leave the cube off-grid (sometimes floating in the air). The gate at the
top of `CubeRollMovement.Update()` blocks new rolls until the tip completes,
which restores the cube exactly to its starting pose before next-frame logic
runs.

### Why tip-and-rotate instead of slide-and-translate

Earlier iteration translated the cube's position toward the wall and back.
That read as the cube "floating sideways" rather than attempting a roll. The
tip animation uses the same pivot and rotation axis as a real roll, just
aborted partway, so it visually communicates "I tried to commit to that
direction" rather than "I scooted toward the wall."

---

## Limitations and future ideas

- **Mobile haptics use Unity's `Handheld.Vibrate()`** — a single short pulse,
  no duration control. For finer haptic control (light vs heavy bumps), a
  platform-specific plugin like Lofelt Nice Vibrations would be the upgrade.
- **No gamepad rumble yet.** Desktop platforms with controllers fall through
  to a no-op. `Gamepad.current.SetMotorSpeeds(...)` would slot in cleanly
  when needed.
- **Tip animation is direction-only.** The cube tilts toward the blocked
  direction but doesn't add any roll/wobble around other axes. Could be
  enhanced for more "physical" feel if desired.
- **Single feedback for both rejection types.** Walls and unpushable blocks
  produce the same bump. Could differentiate (different sound for blocks?)
  if the team wants distinct cues.
