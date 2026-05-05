# Elevator System — Design Notes

Architecture decisions and trade-offs for the elevator feature. Read this if
you're modifying the elevator scripts or extending them with new behavior.

---

## Decision 1: Sibling, not subclass, of MovingPlatform

**Context.** The project already had `MovingPlatform.cs` and `Button.cs`. The
existing system moves a platform once when triggered, with `ActivateMovement()`
sending it toward the end position and `DeactivateMovement()` sending it back.
There's no "stay where you arrived" behavior, no orientation check on the
button, and no rider parenting.

**Decision.** Build `Elevator` and `ElevatorButton` as standalone components
with no inheritance from the existing classes.

**Why.** Other team members may have scenes wired up to `MovingPlatform` and
`Button` with assumptions about the current behavior. Editing those classes
risks silently breaking those scenes. Subclassing reduces that risk but still
introduces coupling. Standalone components are fully isolated, fully additive,
and the cost is just a small amount of duplicated trivial code.

---

## Decision 2: Carry rider via per-frame world position update, not parenting

**Context.** The cube's position is controlled by `CubeRollMovement`, which
snaps to a grid after each roll. The elevator moves the platform's transform
along a path. By default the cube would just stay at its world position while
the elevator moves under or away from it.

**First attempt.** Parent the cube to the elevator transform during travel,
restore the original parent on arrival. This is the Unity-idiomatic way to
express "this object's world transform follows that one."

**Bug discovered.** The elevator's transform scale is non-uniform — `(1, 0.2, 1)`
because it's a flat tile. When the cube parented into it, the cube inherited
that non-uniform scale and visually squashed to a thin rectangle. The squash
direction varied based on the cube's rotation when boarding (different bottom
face → different post-parent appearance), making the bug look random.

**Decision.** Don't parent. Instead, in the `Travel` coroutine, capture the
rider's offset from the elevator at ride start (canonicalized to a fixed
"cube above elevator center" position so misalignments don't propagate), then
update `riderRoot.position` every frame to maintain that offset.

**Why this works.** World position updates don't go through the parent
transform's scale, so no inheritance happens. The cube stays full-size and
correctly oriented. The elevator's curved motion is automatically followed
because we recompute every frame from `transform.position`.

**Cost.** Slightly more code in `Travel` (one extra Vector3 and one assignment
per frame). The `AttachRider` method now does less — it only stores the rider
reference and disables `CubeRollMovement`, doesn't actually reparent.

---

## Decision 3: Disable CubeRollMovement during travel

**Context.** `CubeRollMovement.SnapToGrid()` rounds the cube's position to
the grid after each roll. If the elevator is mid-travel and
`CubeRollMovement` is still active, a stray input could trigger a roll that
calls SnapToGrid, teleporting the cube back to the floor below.

**Decision.** Disable the script during travel (`riderMovement.enabled =
false`) and re-enable on arrival.

**Why.** Smallest possible change, zero edits to existing files. Alternative
would be adding an `isOnElevator` flag to `CubeRollMovement` itself, but
that's a more invasive coupling for the same outcome.

---

## Decision 4: Auto mode triggers after isSnapped, plus an on-elevator check

**Context.** A naive auto-elevator would call `Toggle()` immediately on
`OnTriggerEnter`. But the cube enters the trigger volume *during* its rolling
animation, before `isSnapped` is true. Triggering at that moment would launch
the elevator while the cube is still rotating.

**First version.** Auto mode launches a coroutine on `OnTriggerEnter` that
waits for `CubeState.isSnapped == true`, plus a small grace delay
(`autoTriggerDelay`), then calls `Toggle()`.

**Bug discovered.** When the cube was rolling near (but not onto) an elevator
on a multi-tile floor, the cube's collider would briefly enter and exit the
elevator's trigger zone. `OnTriggerEnter` fired, the snap-wait started, the
grace period passed, and `Toggle()` fired — but the cube had already rolled
past. Result: the elevator left without anyone, and the cube was stranded.

**Decision.** Add a final on-elevator check at the end of
`AutoTriggerAfterSnap`. After the grace period, re-run `FindRiderOnTop()`.
If no rider is actually standing on the elevator, abort the toggle.

**Why.** The graze problem is not solvable by tightening the trigger zone —
the cube collider naturally extends into adjacent tiles' airspace during
rolls. The right fix is to verify commitment at the moment of action, not
the moment of trigger entry.

**Side effect.** If the player rolls off the elevator before the snap
happens, `OnTriggerExit` cancels the pending coroutine. So briefly touching
the elevator without committing to it doesn't trigger a trip.

---

## Decision 5: Five activation modes, with Carry Rider as orthogonal flag

**Context.** Initial design had two modes: Auto and Manual. As the system
matured we identified three more useful behaviors: orientation-gated
(HollowFaceDown), continuous timing puzzle (Shuttle), and rider-triggered
with reset (AutoReturn).

**Question.** Should "is the rider carried back when the elevator returns"
be a separate field, or should it be encoded in mode names like
"ShuttleSticky" vs "ShuttleSlippery"?

**Decision.** Five modes (Auto, Manual, HollowFaceDown, Shuttle, AutoReturn)
plus an orthogonal `carryRider` checkbox.

**Why.** The rider-carry question applies to all modes that move
automatically, not just Shuttle. AutoReturn elevators benefit from being
able to reset without dragging the player back to the start. Even Auto
elevators could in principle want a "leave the rider behind" variant for
exotic puzzles. Encoding rider-carry into mode names would have created
8+ modes with combinatorial overlap. Pulling it out as its own field keeps
the mode list short and the inspector clean: each mode controls
"when does it move", and `carryRider` controls "what happens to the player
when it does."

**Cost.** Slightly more inspector fields. But each tooltip explains what the
field does, and the default (`carryRider = true`) matches the obvious
expectation for traditional elevators.

---

## Decision 6: Stay-at-destination by default, AutoReturn as opt-in

**Context.** `MovingPlatform` springs back to its start position when
`isMoving` is set false. Should `Elevator` do the same?

**Decision.** Default modes (Auto, Manual, HollowFaceDown) stay at the
destination indefinitely. AutoReturn mode opts into the spring-back behavior.

**Why.** Realistic elevator behavior in most cases. If a default elevator
auto-returned, a player who got off on the upper floor would find the
elevator gone when they came back. Stranding the player is a bug, not a
feature, for traditional transport.

But for hub-and-spoke level designs, you do want a "send and reset"
elevator that returns ready for the next user. So we added AutoReturn as
an explicit opt-in mode rather than making it the default.

---

## Decision 7: ElevatorButton has a press cooldown

**Context.** `OnTriggerEnter` fires once when a collider enters. But if the
player rolls onto the button, presses it, the elevator goes up, the player
rolls back over the button on the way to somewhere else, the button would
toggle the elevator again. That makes manual elevators feel jittery in tight
spaces.

**Decision.** A one-second cooldown on `ElevatorButton`. Configurable.

**Trade-off.** A player who genuinely wants fast cycling will hit the
cooldown. One second is a compromise; tune per-button per-level.

---

## Decision 8: Cosmetic emission via MaterialPropertyBlock

**Context.** The elevator pulses its emission color during travel. We need
per-instance shader property changes without instantiating per-renderer
materials (which would break SRP batcher batching).

**Decision.** Use `MaterialPropertyBlock` to override `_EmissionColor` and
`_BaseColor` per renderer.

**Note.** The same pattern is used in `ElevatorEnergyColumn` and would be
the right approach for any custom shader work that needs per-instance
properties.

---

## Decision 9: Quadratic Bezier for the path, control point exposed

**Context.** The first version of `Elevator` was vertical-only: two scalar
Y values plus a straight lerp. The team identified curved/non-vertical
paths as a near-term need, so the path representation was generalized
before the system shipped.

**Options considered.**
1. Stay with `Vector3.Lerp` between two points.
2. Quadratic Bezier (one control point) — supports lines, arcs, dips, and
   diagonals.
3. Cubic Bezier (two control points) — supports S-curves and inflection.
4. Unity Splines package — supports arbitrarily many anchors.

**Decision.** Option 2.

**Why.** Quadratic Bezier is the smallest representation that buys curved
paths. One control point covers the realistic design space for an
elevator-like mechanic. Cubic Bezier and splines support more shapes but
add inspector complexity and editor work without supporting use cases the
team has identified. We can promote to cubic later by adding a second
control point — additive change, no migration.

The control point defaults to the midpoint of A and B (via
`useAutoControlPoint`), so any elevator that doesn't care about path shape
is automatically a straight line.

---

## Decision 10: Cube stays world-upright during curved travel

**Context.** When the elevator follows a curved path, we have two options
for the rider's rotation:

a) Keep the cube world-upright (translates along curve, orientation unchanged).
b) Tilt the cube to match the path tangent.

**Decision.** (a) — keep the cube world-upright.

**Why.** The cube's orientation state is tracked by `CubeOrientation` in
90-degree snaps. Smooth tangent-based rotation during travel would put the
cube at arbitrary angles when it arrives, breaking the orientation system
unless we explicitly snap it back at the end. By contrast, world-upright:
- Preserves the orientation state cleanly across trips.
- Reads as "I'm standing on a moving platform" — a familiar mental model.
- Supports orientation-gated mechanics (HollowFaceDown elevators, button
  presses) without surprises.

**Cost.** Curved paths look slightly artificial because the cube doesn't
"lean into" the curve. For gentle arcs this is fine; for paths that go
nearly horizontal the illusion weakens. If we need tangent-following
rotation later, the right approach is to extend `CubeOrientation` first to
support arbitrary rotations with snap-on-stop semantics, then add an opt-in
flag (`tiltToTangent`) on `Elevator`.

---

## Decision 11: Endpoints are absolute world positions, with editor helpers

**Context.** Should `endpointA` / `endpointB` be world positions or offsets
from the elevator's transform?

**Decision.** Absolute world positions, with a custom inspector
(`ElevatorEditor.cs`) that exposes "Set Endpoint A/B from current position"
buttons to fill them in by dragging the elevator visually.

**Why.** Absolute positions show up directly in the scene-view gizmo and
can be eyeballed against level geometry. They also let one elevator's
endpoints reference another part of the scene that isn't its own transform.

**Cost discovered during dev.** If a designer drags the elevator's transform
in the scene view without updating the endpoints, the elevator jumps to the
(now stale) endpoint position on Awake. This caused real workflow friction
during demo level construction.

**Mitigation.** The custom inspector adds buttons that copy the current
transform position into Endpoint A or B in one click. Designers can drag
the elevator visually, click the button, and never type coordinates. The
inspector also has "Snap transform to Endpoint A/B" buttons to preview
where the elevator will start before pressing Play.

**Future improvement.** Consider switching to local-space endpoints (offsets
from the elevator's transform) so dragging the transform automatically
updates the path. This would be a breaking change to existing scenes but
worth the workflow improvement. Listed as a future task in README.

**Important workflow rule.** Keep elevators at the scene root. Nested under
organizational empty GameObjects, the transform inspector shows local
coordinates while the script uses world — the disconnect causes confusion.

---

## Decision 12: Runway visualizer as separate component, mode-aware

**Context.** Players need to see the elevator's path before riding it.
Initially we used a static cylinder column (`ElevatorEnergyColumn`) but
that read as architecture, not as path indication.

**Decision.** Build `ElevatorRunwayLights` as a separate script that spawns
N small glowing dots along the elevator's Bezier path at runtime. The
runway reads the elevator's `Mode` and selects a pulse pattern automatically
(forward sweep for Auto, continuous for Shuttle, bouncing for AutoReturn,
static for Manual, slow for HollowFaceDown).

**Why separate.** The visualizer doesn't need to live inside the Elevator
script — it just reads public getters (`EndpointA`, `EndpointB`,
`ControlPoint`, `Mode`, `IsTraveling`, `CurrentEndpoint`). Keeping it as
its own script means designers can disable the runway, swap it for a
custom visualizer, or attach multiple visualizers to one elevator without
touching `Elevator.cs`.

**Mode-aware patterns.** Each mode has a distinct visual rhythm so players
can predict elevator behavior at a glance. Continuous fast pulses for
Shuttle (always-active mode) read differently from a one-shot forward
sweep on Auto, which reads differently from a bouncing wave on AutoReturn.

**Auto-detect color.** The runway pulls color from the elevator's material
by default (`autoDetectColor = true`). Designers assign a colored material
to the elevator, and the runway dots auto-match. Single source of truth
per elevator. Manual override available via the same checkbox pattern as
pattern detection.

**Why colliders are destroyed.** Each spawned dot is a Unity Sphere
primitive, which comes with a SphereCollider by default. We `Destroy()`
the collider immediately after creation so the dots can never interfere
with the cube's physics or trigger detection. (We learned this the hard
way debugging an earlier orb-based visualizer that broke the cube's
attach logic.)

---

## Decision 13: Three new public getters on Elevator

**Context.** `ElevatorRunwayLights` needs to know the elevator's path
(EndpointA, EndpointB, ControlPoint) and current state (Mode, CurrentEndpoint,
IsTraveling). The latter were already public; the path fields were private.

**Decision.** Add three read-only properties: `EndpointA`, `EndpointB`,
`ControlPoint`. The control point property returns the *effective* control
point — i.e., it returns the auto-computed midpoint when
`useAutoControlPoint` is checked, or the configured control point otherwise.

**Why returning effective.** The runway visualizer doesn't care about the
distinction between auto and manual control point — it just wants to sample
the path. Exposing only the effective value keeps the visualizer simple and
prevents bugs where it samples a control point that the path math wouldn't
actually use.

---

## Things deliberately not done

- **Tweening library.** Could have used DOTween or LeanTween. Coroutine +
  SmoothStep + Bezier evaluation gets the job done with no dependencies.
- **ScriptableObject configuration.** Per-instance fields are simpler for a
  small system. If endpoints/travel-times become repeated patterns, promote
  to SOs later.
- **Animator state machine.** Could have driven the path via Animator
  curves. Code is simpler and lets us share one path representation between
  the runtime motion and the gizmo preview.
- **Path baking.** We evaluate the Bezier per frame at runtime instead of
  pre-sampling N points. The math is six multiplies and three adds per
  axis; per-frame cost is negligible.
- **Local-space endpoints.** Would solve the "drag-the-transform-and-the-path-
  updates-too" workflow issue, but is a breaking change to existing scenes.
  Punted to a future migration. Custom inspector buttons mitigate the
  current workflow friction.
- **Multi-rider support.** Would require an array of riders and per-rider
  offset tracking. Not needed for a single-player game; easy to add later.
- **Mid-travel cancel.** Once a trip starts, it completes. Cancellation would
  introduce edge cases (rider half-attached, interrupted curve sampling)
  that aren't worth the complexity for current use cases.

---

## Lessons learned during development

A few things we discovered the hard way that future maintainers should know:

1. **Trigger detection requires a Rigidbody.** Unity only fires `OnTrigger`
   events when at least one of the two colliders has a Rigidbody. The
   elevator needs `Rigidbody` (kinematic, no gravity). This was the single
   biggest debugging session early on.

2. **Parenting inherits scale.** Don't reparent objects with non-uniform-scale
   targets. We learned this when the cube became a flat rectangle after
   riding a flat tile.

3. **OnTriggerEnter doesn't mean "committed."** A cube rolling past an
   elevator can graze its trigger volume without intending to ride. Always
   verify commitment at action time, not entry time.

4. **Endpoint coordinates and transform position drift apart.** Designers
   place elevators by dragging, but the script reads endpoints. Without
   discipline (or editor helpers), these get out of sync. The custom
   inspector buttons are a workflow band-aid; the long-term fix is
   local-space endpoints.

5. **Nested GameObjects make coordinates ambiguous.** Tiles inside a parent
   show local coordinates in the inspector, not world. If you read tile
   positions to compute elevator endpoints, you'll get the wrong values.
   Use the elevator's own transform as the source of truth and keep
   elevators at scene root.