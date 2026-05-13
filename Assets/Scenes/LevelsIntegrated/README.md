# Levels Integrated

Parallel duplicates of the team's level scenes with the full mobile input, feedback, and camera systems wired in. Originals in `Assets/Scenes/Levels/` are owned by their respective authors and remain untouched.

Author: Kane

---

## Why duplicates exist

The team's level scenes shipped without integration of the systems from PRs #13 and #14:
- `RollFeedback` (invalid roll feedback — tip animation, audio, haptics)
- `SwipeInputProvider` (touchscreen swipe input)
- `DPadCanvas` (on-screen directional buttons)
- `PlayerCamera` (pinch zoom + ground-aligned pan)
- Reset button (camera home view restore)

Rather than editing teammates' scenes directly (risk of overwriting their level design work, conflicts on rebases, blocking their pushes), this folder contains parallel duplicates with full integration. Both folders coexist with the same filenames. Unity allows this because scenes are identified by path, but loaded by name.

---

## How activation works

The main menu loads levels by string name: `SceneManager.LoadScene("Level_0")`. Unity loads whichever scene named `Level_0` is in Build Settings. The Tools menu swaps which folder's scenes are referenced.

**Tools → Integration → Activate Integrated Levels**
Swaps Build Settings to point at `LevelsIntegrated/Level_X.unity`. Each level now loads its integrated version.

**Tools → Integration → Activate Original Levels**
Reverts Build Settings to point at the original team-authored versions.

**Tools → Integration → Show Current Build Settings**
Prints the current Build Settings scene list to Console.

The menu's code is unchanged — it always calls `LoadScene("Level_X")`. Build Settings determines which file gets loaded.

---

## Typical workflows

### To demo / record / build the integrated version
1. Tools → Integration → Activate Integrated Levels
2. Verify with Show Current Build Settings
3. Press Play, or File → Build And Run

### To return to original team build
1. Tools → Integration → Activate Original Levels

### To re-sync after a teammate updates their original
1. Open the original (`Assets/Scenes/Levels/Level_3.unity`)
2. Open the integrated copy (`Assets/Scenes/LevelsIntegrated/Level_3.unity`) additively
3. Identify what changed in the original (level geometry, puzzle elements, decoration)
4. Replicate the change in the integrated copy
5. Save the integrated copy

---

## Pain points discovered during integration

These are real bugs / footguns we hit while wiring the levels. Documented here so future integrators don't have to re-discover them.

### Background Image was blocking all swipe input

**Symptom:** SwipeInputProvider logs "Touch began over UI — ignoring for swipe detection" for every touch. No swipes fire.

**Cause:** Each level has a Canvas with a full-screen background Image (the gradient/atmospheric backdrop). The Image's `Raycast Target` was enabled by default. Unity's EventSystem reports any UI element under a touch, including transparent or background images, so the background was "catching" every touch before SwipeInputProvider could process it.

**Fix:** Uncheck `Raycast Target` on the background Image. The Image still renders, it just doesn't catch input. Done in each integrated scene during this PR.

**For new levels:** Every level added in the future should follow the same pattern — background Images don't need Raycast Target on. Worth raising as a separate cleanup PR to fix the originals too.

### Editor mouse-as-touch causes phantom multi-touch in PlayerCamera

**Symptom:** PlayerCamera reports two-finger gestures from a single mouse drag. Pinch values like 1146px appear immediately. Scroll/middle-drag camera controls stop working.

**Cause:** Unity's "Simulate Touch Input From Mouse or Pen" (Input Debugger → Options) reports phantom multi-touch in some Unity versions. PlayerCamera correctly detects 2+ touches and processes them as pinch/pan, drowning out the mouse fallback.

**Workaround:** Toggle mouse-as-touch based on what you're testing:
- **Camera testing:** turn it OFF, use scroll wheel + middle-drag
- **Swipe testing:** turn it ON, use mouse drag
- **Both at once:** test on a real phone where multi-touch works correctly

This is an editor-only issue. Real phones handle multi-touch correctly and both systems coexist as designed.

### Camera placement varies per level — Reset View depends on it

**Symptom:** "Reset View" snaps the camera to an awkward spot instead of centering on the player.

**Cause:** PlayerCamera captures the camera's design-time position in Awake as the "home view." Each level author placed their camera with their own framing intent — some are wide-shots showing the whole puzzle, some are angled views, some inadvertently don't include the player at all. The home view inherits this framing.

**Fix during integration:** For each level where the player wasn't framed at scene start, the Main Camera was repositioned using Unity's "Align With View" trick (navigate Scene view to a good framing, then GameObject → Align With View). This becomes the new home for Reset.

**For level authors:** Position your Main Camera so the player is visible at scene start. That's what Reset View returns to. If you want a wide framing at scene start instead, that's also fine — players can still pan/zoom from there.

### Input directions are world-relative, not screen-relative

**Symptom:** With a tilted camera, "swipe up" doesn't roll the cube along screen-up. It rolls along world-forward (+Z), which on a tilted view might be diagonal-up-right or similar.

**Cause:** Deliberate design choice from PR #14. Swipes and D-pad both produce world-space directions because resolving "screen up = world ???" requires camera-relative input, which conflicts with rotation and is a deeper architectural change.

**Workaround for now:** Players adjust quickly to "swipe up = cube goes away from me on the world floor" — most 3D puzzle games work this way. Pan/zoom let players adjust the view if a level's framing feels confusing.

**Future improvement:** Camera-relative input is flagged in the Camera README as a future PR. Will require coordinated changes to SwipeInputProvider, DPadController, and PlayerCamera together.

### `Button.cs` in global namespace conflicts with `UnityEngine.UI.Button`

**Symptom:** Inspector drag-and-drop for Button references rejects valid Unity UI Buttons with a "no" cursor. Picker shows empty.

**Cause:** The project has a `Button.cs` script (separate from Unity's built-in UI Button) in the global namespace. The C# resolver picks the global one when DPadController originally tried to reference `Button` fields.

**Workaround in this project:** DPadController uses fully-qualified `UnityEngine.UI.Button` everywhere and discovers buttons by name at runtime instead of via Inspector drag-and-drop. See Input README for details.

**Future cleanup:** Rename the global `Button.cs` or wrap it in a namespace. Then DPadController could revert to Inspector references for a cleaner workflow.

---

## Files in this folder

| File | Purpose |
|------|---------|
| `Level_0.unity` through `Level_9.unity` | Duplicated levels with full systems wired |
| `TestLevel.unity` | Duplicate of TestLevel with full systems wired |
| `README.md` | This file |

Related files outside this folder:

| File | Purpose |
|------|---------|
| `Assets/Scenes/AllSystems_Reference.unity` | Canonical example showing how all systems integrate |
| `Assets/Scripts/Editor/LevelIntegrationSwapper.cs` | Editor utility for swapping Build Settings between folders |

---

## Limitations & future improvements

- **Re-sync is manual.** When a teammate updates their original level, the integrated copy doesn't auto-update. Manual replication required.
- **Build Settings swap is project-wide.** Only one set of levels can be active at a time. Both team members can't simultaneously have different folders active without uncommitted Build Settings changes.
- **Shared prefab assets across both folders.** Prefab references (DPadCanvas, etc.) are shared by GUID between original and integrated scenes. A prefab change affects both. Usually desirable.
- **No automated test.** Verification that each integrated scene works is done by opening and playing each one. A scripted test would be a nice addition.

### Future improvements that would reduce this folder's necessity

- **Prefab-ify Player + systems** as a single drop-in prefab. Levels would integrate by dragging in one asset, eliminating the per-level wiring step entirely.
- **Automate the duplicate-and-wire step** as another editor menu: "Duplicate Selected Level and Integrate."
- **Long-term: retire this folder.** Once a stable integration pattern exists, the duplicates become redundant. Either fold integration into the originals (with team consent) or migrate to a prefab-based pattern.

---

## Tested

- Unity Editor (mouse fallback + mouse-as-touch toggling)
- Physical Android device (Galaxy A17 5G)
- All levels load via main menu, all inputs work, all camera controls work, all reset behavior works
