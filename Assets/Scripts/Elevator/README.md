# Elevator System

A two-endpoint elevator for the Cube-Matrix game. Carries the player cube
between point A and point B along an optionally curved path. Supports five
activation modes and is fully additive — no existing scripts were modified to
introduce this system.

Author: Kane
Demo scene: `Assets/Scenes/ElevatorScene.unity`

---

## Quick Start

1. Open `ElevatorScene.unity` to see all activation modes in action.
2. To add an elevator to your own scene, drag the `Elevator` prefab from
   `Assets/Prefabs/Elevator/` into the scene.
3. In the inspector on the `Elevator` component, set:
   - **Endpoint A** and **Endpoint B**: world positions of the two stops.
     Easiest workflow: drag the elevator visually, then click "Set Endpoint A
     from current position" in the custom inspector buttons.
   - **Use Auto Control Point**: leave checked for a straight line. Uncheck
     to bend the path with the **Control Point**.
   - **Mode**: pick one of the five modes (see below).
   - **Carry Rider**: leave checked for traditional elevators. Uncheck for
     "slippery" elevators that leave the player behind.
4. Add an `ElevatorRunway` GameObject (Hierarchy → Create Empty, add the
   `ElevatorRunwayLights` script, drag your elevator into the Elevator field).
   This draws the runway-style guide dots along the path.

The custom inspector buttons let you set endpoints by dragging the elevator
visually instead of typing coordinates. The scene-view gizmo draws the actual
path while the elevator is selected.

---

## Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Elevator/Elevator.cs` | The platform itself. Two-endpoint state machine with Bezier path and five activation modes. |
| `Assets/Scripts/Elevator/ElevatorButton.cs` | Trigger-volume button that toggles an Elevator. Respects cube orientation. |
| `Assets/Scripts/Elevator/ElevatorRunwayLights.cs` | Visual path indicator. Spawns glowing dots along the elevator's path with mode-aware pulse patterns. |
| `Assets/Scripts/Elevator/ElevatorEnergyColumn.cs` | Optional cosmetic shaft visual. Superseded by ElevatorRunwayLights, kept for compatibility. |
| `Assets/Scripts/Elevator/Editor/ElevatorEditor.cs` | Custom inspector with quick-set endpoint buttons. Editor-only. |
| `Assets/Prefabs/Elevator/Elevator.prefab` | Drop-in elevator platform on the Tile layer. |
| `Assets/Prefabs/Elevator/ElevatorButton.prefab` | Drop-in button for Manual mode. |
| `Assets/Scenes/ElevatorScene.unity` | Demo scene showing all five modes with curved paths and visual identity. |

---

## Activation Modes

| Mode | Behavior | Typical use |
|------|----------|-------------|
| **Auto** | Triggers when player lands on it. Travels to the other endpoint. Stays there until player rides back. | Tutorial elevators, standard transport |
| **Manual** | Only moves when an external `Toggle()` call fires (typically from `ElevatorButton`). Ignores player landing. | Puzzle elevators where activation IS the puzzle |
| **HollowFaceDown** | Triggers like Auto, but only if the cube's hollow face is on the bottom when the grace period ends. | Orientation-gated elevators that require positioning the cube correctly |
| **Shuttle** | Continuously runs A→B→A→B with `lingerTime` pauses at each endpoint. Player must time their boarding. | Timing puzzles, "catch the bus" mechanics |
| **AutoReturn** | Triggers like Auto, but after arriving at the destination it waits `autoReturnDelay` seconds and returns to the starting endpoint automatically. | Reset-able transports, hub elevators that should be ready for the next user |

### Carry Rider

A separate checkbox on every elevator. When checked, the cube rides along
when the elevator moves. When unchecked, the elevator can leave the cube
behind. Most useful with Shuttle (to create "slippery" timing puzzles where
standing still means missing the ride) and AutoReturn (to allow the cube to
disembark before the elevator resets). Traditional Auto and Manual elevators
should leave this checked.

---

## Path Shapes

The elevator follows a quadratic Bezier curve `A → C → B`, where `C` is the
control point. This gives you four useful regimes from one component:

- **Straight line.** Leave `Use Auto Control Point` checked. The control point
  is computed as `(A + B) / 2` and the curve degenerates to a line. This is
  the classic vertical elevator.
- **Arc.** Uncheck Auto and place the control point off the A→B line. Pull
  it upward for an over-the-top arc, sideways for a swooping diagonal.
- **Dip.** Place the control point below the A→B line for a U-shaped path.
- **Diagonal.** A and B at different X/Z values with Auto control gives you
  a straight diagonal — no curve, just non-vertical.

The cube stays world-upright the entire time it's riding. We deliberately
do not tilt the cube to match the path tangent (see DESIGN.md for why).

### Designing curves

- For an arc that goes up and over to a destination at the same height, set
  A=(0, 0, 0), B=(3, 0, 0), and Control Point=(1.5, 2, 0). The cube
  smoothly arcs over the gap.
- For a diagonal swoop between floors, pull the Control Point far to the
  side (e.g. X=15 when A and B are near X=4). The curve bows out into open
  space, avoiding intermediate floors.
- Don't make the control point too far from the A→B line — the cube can
  overshoot far enough to clip through walls or the ground. Watch the gizmo.
- Travel feels best when `travelTime` scales with path length. Long, curvy
  paths want 2.5–3 seconds; short verticals want 1–1.5.

---

## Visual Identity (Materials and Runways)

Each mode benefits from a distinct visual color so players can read the
elevator's behavior at a glance. The recommended palette:

| Mode | Suggested color | Why |
|------|-----------------|-----|
| Auto | Cyan | Neutral, electric |
| Manual | Gray | Inert until activated |
| HollowFaceDown | Amber | "Conditions apply" warning |
| Shuttle | Orange | Active, urgent, in motion |
| AutoReturn | Purple | Cyclical, "comes back" |

Create a Material per mode in `Assets/Materials/`, set its Base Color, drag
onto the elevator's MeshRenderer.

The `ElevatorRunwayLights` script automatically pulls color from the
elevator's material (when `Auto Detect Color` is checked) so the runway dots
match the elevator's hue. Pulse pattern is also auto-detected from the
elevator's mode:

| Mode | Runway pulse |
|------|--------------|
| Auto | Forward sweep during travel |
| Manual | Static glow (no animation) |
| HollowFaceDown | Slow forward sweep ("thoughtful") |
| Shuttle | Continuous fast sweep (always pulsing) |
| AutoReturn | Forward on outbound, reverse on return |

Both can be overridden per runway via the `Auto Detect Pattern` and
`Auto Detect Color` checkboxes.

---

## Custom Inspector

The `ElevatorEditor.cs` script adds quick-action buttons under the normal
Elevator inspector fields:

- **Set Endpoint A from current position** — copies the elevator's current
  world position into Endpoint A. Drag the elevator to where you want it to
  start, click the button.
- **Set Endpoint B from current position** — same, for Endpoint B.
- **Snap transform to Endpoint A / B** — moves the elevator's transform to
  the configured endpoint coordinates. Useful for visualizing where the
  elevator will start and end before pressing Play.

This avoids having to type world coordinates into the Endpoint fields by
hand. Keep elevators at the scene root (not nested under organizational
GameObjects) so their transform position equals their world position.

---

## Public API

```csharp
public void Toggle();              // go to whichever endpoint we're not at
public void GoToA();
public void GoToB();
public bool IsTraveling { get; }
public Endpoint CurrentEndpoint { get; }   // Endpoint.A or Endpoint.B
public ActivationMode Mode { get; }
public Vector3 EndpointA { get; }
public Vector3 EndpointB { get; }
public Vector3 ControlPoint { get; }
```

All movement commands are no-ops if `IsTraveling` is true. The endpoint
getters are used by `ElevatorRunwayLights` to sample the path; external code
can use them too if you're building additional path-aware visuals.

---

## How It Plays Nicely with Existing Systems

- **CubeRollMovement**: not modified. The elevator sits on the `Tile` layer,
  so the player's "can I roll here" raycast finds it like any other tile.
  During travel, `CubeRollMovement` is disabled and the elevator manually
  carries the cube via per-frame world position updates.
- **CubeState**: read-only. Auto/HollowFaceDown/AutoReturn modes wait for
  `isSnapped` before triggering.
- **CubeOrientation**: read-only. HollowFaceDown mode and `ElevatorButton`
  both check orientation. The cube's rotation is preserved across rides
  because we don't reparent during travel.
- **Button.cs / MovingPlatform.cs**: not touched. `ElevatorButton` is a
  separate component; `Elevator` is a separate component from
  `MovingPlatform`. Existing scenes using the original combo continue to
  work unchanged.

---

## Known Limitations

- **One rider at a time.** If two players step on the elevator, only the
  first detected one will be carried.
- **No mid-travel cancel.** Once the elevator starts moving, it completes
  the trip. External Toggle calls are ignored while `IsTraveling`.
- **Cube stays upright on curves.** No path-tangent rotation. See DESIGN.md.
- **Endpoints are absolute world positions.** Use the custom inspector
  buttons to avoid manually typing them. Elevators must be at the scene root
  so transform.position equals world position.
- **Quadratic Bezier only.** Single control point per elevator — no S-curves,
  loops, or multi-stop paths. Cubic / spline upgrade is a future task.

---

## Troubleshooting

**The cube falls through the elevator.**
The elevator's main collider must NOT be a trigger (the cube needs to stand
on it). Auto-mode detection uses a *second* trigger collider on top. Check
the prefab has both: one solid, one `Is Trigger`.

**The cube rolls onto an Auto elevator but it doesn't move.**
Confirm the player is tagged `Player`. Check the trigger collider on top is
sized so the cube enters it. Verify the elevator has a `Rigidbody` (kinematic,
no gravity) — Unity needs at least one rigidbody for OnTrigger events.

**The cube becomes squashed/flat after riding.**
The cube was probably parented to the elevator's transform somehow. Confirm
you have the latest `Elevator.cs` — newer versions don't parent the cube,
they update its world position directly. The squash came from inheriting
the elevator's non-uniform scale.

**The elevator triggers when the cube just rolls past it (Auto).**
This was a known issue. The current version re-checks "is the player
actually on me right now?" before toggling. If you still see this, confirm
your `Elevator.cs` includes the `FindRiderOnTop()` check at the end of
`AutoTriggerAfterSnap`.

**The elevator jumps to a weird position when I press Play.**
The Endpoint A coordinates don't match where you placed the elevator
visually. Use the custom inspector "Set Endpoint A from current position"
button to sync them. Or check that the elevator isn't nested under a parent
with a non-zero transform — keep elevators at scene root.

**The button doesn't respond.**
Three possible causes:
1. Hollow face is on the bottom — orientation gate is rejecting the press.
2. The elevator is mid-travel — wait for it to arrive.
3. The button's `Elevator` field points to a different elevator (common
   after duplicating).

**The runway dots don't appear.**
Verify the runway has its `Elevator` field assigned. Look at the runway's
gizmo in the scene view — if you see a cyan path going somewhere unexpected,
the elevator's endpoints are wrong.

---

## Demo Scene Notes

`ElevatorScene.unity` shows multiple modes in action across multiple floors
and areas. Each elevator demonstrates a different feature:

- Vertical Auto elevator (basic transport)
- HollowFaceDown elevator (orientation-gated)
- Curved Auto elevator (Bezier swooping path)
- Shuttle elevator (continuous timing puzzle)
- AutoReturn elevator (rider-triggered with reset)

Each elevator has a colored material matching its mode, and a runway whose
dots auto-match that color. The pulse pattern of each runway also matches
the mode (continuous for Shuttle, bouncing for AutoReturn, etc.) — giving
players a learnable visual language.

The scene also has multi-area layout (Floor 1–5, Area 2 with its own
BottomFloor / Final Space / UpperFloor) demonstrating how elevators bridge
between distinct level sections. Note: floor positioning in the demo scene
uses a mix of organizational parents — be aware that nested floor parents
can introduce coordinate confusion if you read tile positions to derive
elevator endpoints. Use the custom inspector buttons instead.

---

## Future Ideas

- Multi-stop elevators (3+ endpoints along a longer path).
- Cubic Bezier (two control points) for S-curves and inflection.
- Tilt the cube to match path tangent during travel — requires extending
  `CubeOrientation` to handle smooth rotation.
- Synchronized elevator pairs (one to A = other to B).
- Sound effects on departure/arrival.
- Animated doors on each endpoint.
- Move endpoints into local space (offsets from transform) so dragging the
  elevator updates the path automatically.

See `DESIGN.md` for rationale behind specific implementation choices.