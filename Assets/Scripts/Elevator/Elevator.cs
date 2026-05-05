using System.Collections;
using UnityEngine;

/// <summary>
/// Two-endpoint elevator that carries the player cube along a (optionally
/// curved) path between point A and point B. Supports five activation modes
/// (Auto, Manual, HollowFaceDown, Shuttle, AutoReturn) and an orthogonal
/// CarryRider flag for "slippery" elevators that can leave the player behind.
///
/// Path shape:
///   The elevator follows a quadratic Bezier curve defined by endpointA,
///   endpointB, and a controlPoint. If useAutoControlPoint is true (the
///   default), the control point is computed as the midpoint of A and B,
///   which produces a straight line — i.e. the classic vertical elevator.
///   Drag controlPoint elsewhere to bend the path: J-curves, U-dips,
///   diagonal swoops, etc. The cube stays world-upright throughout travel
///   so the orientation system (CubeOrientation) is not disturbed.
///
/// Activation modes:
///   - Auto:           triggers when the player lands on it.
///   - Manual:         only moves when an external caller invokes Toggle().
///   - HollowFaceDown: triggers like Auto, but only if the cube's hollow
///                     face is on the bottom when the grace period ends.
///   - Shuttle:        runs continuously A→B→A→B with linger pauses; the
///                     player must time their boarding.
///   - AutoReturn:     triggers like Auto, then automatically returns to the
///                     starting endpoint after autoReturnDelay seconds.
///
/// Integration notes:
///   - The elevator GameObject must be on the "Tile" layer so CubeRollMovement
///     treats it as walkable (its raycast looks for tiles via tileLayer).
///   - The elevator must have a non-trigger collider for the cube to stand
///     on, plus a trigger collider for player-landing detection. The prefab
///     ships with both.
///   - The elevator must have a kinematic Rigidbody so OnTrigger events fire.
///   - The player prefab must be tagged "Player" (matches existing Button.cs
///     and WinBlock.cs conventions).
///   - During travel the player's CubeRollMovement is disabled and the player
///     is carried by per-frame world position updates (NOT parenting, which
///     would cause non-uniform scale inheritance).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Elevator : MonoBehaviour
{
    public enum ActivationMode
    {
        Auto,            // moves automatically when the player lands on it
        Manual,          // only moves when Toggle() is called externally (e.g., by a button)
        HollowFaceDown,  // moves automatically, but only if the cube lands with its hollow face down
        Shuttle,         // continuously runs A→B→A→B with a configurable linger at each endpoint;
                         //   no rider needed to start. Player must time their boarding.
        AutoReturn       // moves when ridden (like Auto), then automatically returns to the
                         //   starting endpoint after a configurable delay
    }

    public enum Endpoint
    {
        A,
        B
    }

    [Header("Endpoints")]
    [Tooltip("World-space position of endpoint A. Tip: drag the elevator " +
             "visually in the scene, then click 'Set Endpoint A from current " +
             "position' in the custom inspector below.")]
    [SerializeField] private Vector3 endpointA = new Vector3(0f, -0.1f, 0f);

    [Tooltip("World-space position of endpoint B.")]
    [SerializeField] private Vector3 endpointB = new Vector3(0f, 2.9f, 0f);

    [Tooltip("Which endpoint the elevator starts at when the scene loads.")]
    [SerializeField] private Endpoint startingEndpoint = Endpoint.A;

    [Header("Path Shape")]
    [Tooltip("If true, the path is a straight line between A and B (control " +
             "point is set to the midpoint automatically). Uncheck to bend " +
             "the path with a custom control point.")]
    [SerializeField] private bool useAutoControlPoint = true;

    [Tooltip("Bezier control point. Drag this to bend the path. Only used " +
             "when 'Use Auto Control Point' is unchecked. Tip: pull it " +
             "perpendicular to the A→B line for a smooth arc.")]
    [SerializeField] private Vector3 controlPoint = new Vector3(0f, 1.4f, 0f);

    [Header("Motion")]
    [Tooltip("Seconds to travel from one endpoint to the other. Eased with SmoothStep.")]
    [SerializeField] private float travelTime = 1.5f;

    [Header("Activation")]
    [SerializeField] private ActivationMode mode = ActivationMode.Auto;

    [Tooltip("If checked, when the elevator moves with the player on board, " +
             "the player rides along. If unchecked, the elevator can leave " +
             "without the player (useful for Shuttle mode 'slippery' behavior). " +
             "For traditional Auto/Manual elevators, leave this checked.")]
    [SerializeField] private bool carryRider = true;

    [Tooltip("Auto / HollowFaceDown / AutoReturn modes. Seconds to wait after " +
             "the player lands before starting the trip. Gives the player time " +
             "to register that they've landed before it starts moving.")]
    [SerializeField] private float autoTriggerDelay = 1.0f;

    [Tooltip("Seconds to ignore re-triggers after arriving at an endpoint. " +
             "Prevents the elevator from immediately turning around if the " +
             "player hasn't rolled off yet. Set to 0 to allow instant re-trigger.")]
    [SerializeField] private float postArrivalCooldown = 1.5f;

    [Tooltip("Used by Shuttle and AutoReturn modes. Seconds the elevator " +
             "pauses at each endpoint before starting the next leg. Longer = " +
             "more relaxed timing for the player. Shorter = tighter puzzle.")]
    [SerializeField] private float lingerTime = 2.0f;

    [Tooltip("AutoReturn only. Seconds after arriving at endpoint B before the " +
             "elevator automatically returns to A. Use this to give the player " +
             "time to roll off before the elevator leaves.")]
    [SerializeField] private float autoReturnDelay = 1.5f;

    [Header("Visual Feedback (optional)")]
    [Tooltip("MeshRenderer whose material's emission will pulse during travel. " +
             "Leave null to disable. The material must have _EmissionColor enabled.")]
    [SerializeField] private MeshRenderer pulseRenderer;

    [SerializeField] private Color travelEmissionColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private float travelEmissionIntensity = 2f;

    [Header("Debug")]
    [Tooltip("If checked, the elevator logs detailed state transitions to the " +
             "Console (trigger entries, snap waits, mode bails, toggles, etc.). " +
             "Useful for diagnosing why an elevator isn't behaving as expected. " +
             "Leave off for production.")]
    [SerializeField] private bool verboseLogging = false;

    // -- runtime state --
    private Endpoint currentEndpoint;
    private bool isTraveling;
    private Coroutine autoTriggerCoroutine;
    private Transform riderRoot;             // the player root currently being carried
    private Transform riderOriginalParent;
    private CubeRollMovement riderMovement;
    private MaterialPropertyBlock propBlock;
    private float lastArrivalTime = -1000f;  // when the elevator last finished a trip

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Awake()
    {
        currentEndpoint = startingEndpoint;
        transform.position = (startingEndpoint == Endpoint.A) ? endpointA : endpointB;

        if (pulseRenderer != null)
        {
            propBlock = new MaterialPropertyBlock();
            SetEmission(0f);
        }
    }

    private void Start()
    {
        // Shuttle elevators run continuously on their own. Kick off the loop.
        if (mode == ActivationMode.Shuttle)
        {
            StartCoroutine(ShuttleLoop());
        }
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary> Send the elevator to whichever endpoint it is not currently at. </summary>
    public void Toggle()
    {
        Log($"Toggle() called. isTraveling={isTraveling}, currentEndpoint={currentEndpoint}");
        if (isTraveling) return;
        if (currentEndpoint == Endpoint.A) GoToB();
        else GoToA();
    }

    public void GoToA()
    {
        if (isTraveling || currentEndpoint == Endpoint.A) return;
        Log("GoToA — starting travel.");
        StartCoroutine(Travel(Endpoint.A));
    }

    public void GoToB()
    {
        if (isTraveling || currentEndpoint == Endpoint.B) return;
        Log("GoToB — starting travel.");
        StartCoroutine(Travel(Endpoint.B));
    }

    public bool IsTraveling => isTraveling;
    public Endpoint CurrentEndpoint => currentEndpoint;
    public ActivationMode Mode => mode;

    /// <summary> World-space position of endpoint A (path start). Read-only. </summary>
    public Vector3 EndpointA => endpointA;

    /// <summary> World-space position of endpoint B (path end). Read-only. </summary>
    public Vector3 EndpointB => endpointB;

    /// <summary>
    /// World-space Bezier control point. If "Use Auto Control Point" is enabled,
    /// returns the midpoint of A and B (so the path is a straight line). Otherwise
    /// returns the configured control point. Read-only.
    /// </summary>
    public Vector3 ControlPoint => GetEffectiveControlPoint();

    // ============================================================
    // SHUTTLE LOOP
    // ============================================================

    /// <summary>
    /// Shuttle behavior: linger at one endpoint, travel to the other, linger,
    /// travel back, forever. The player times their boarding to catch a ride.
    /// </summary>
    private IEnumerator ShuttleLoop()
    {
        Log("Shuttle starting.");
        while (true)
        {
            yield return new WaitForSeconds(lingerTime);
            Endpoint dest = (currentEndpoint == Endpoint.A) ? Endpoint.B : Endpoint.A;
            yield return StartCoroutine(Travel(dest));
        }
    }

    // ============================================================
    // PLAYER DETECTION (Auto / HollowFaceDown / AutoReturn)
    // ============================================================

    private void OnTriggerEnter(Collider other)
    {
        Log($"OnTriggerEnter fired. Other: {other.name}, mode: {mode}, isTraveling: {isTraveling}");

        if (mode == ActivationMode.Manual)
        {
            Log("Bailing — mode is Manual (button-only).");
            return;
        }
        if (mode == ActivationMode.Shuttle)
        {
            // Shuttle runs on its own loop. The player just needs to be on board
            // when the linger window ends and Travel begins. No trigger response.
            return;
        }
        Transform playerRoot = ResolvePlayerRoot(other);
        if (playerRoot == null)
        {
            Log($"Bailing — '{other.name}' is not a Player or descendant of one.");
            return;
        }
        if (isTraveling)
        {
            Log("Bailing — already traveling.");
            return;
        }
        if (Time.time - lastArrivalTime < postArrivalCooldown)
        {
            Log($"Bailing — within post-arrival cooldown ({postArrivalCooldown}s).");
            return;
        }

        Log($"All checks passed — scheduling AutoTriggerAfterSnap on root '{playerRoot.name}'.");
        if (autoTriggerCoroutine != null) StopCoroutine(autoTriggerCoroutine);
        autoTriggerCoroutine = StartCoroutine(AutoTriggerAfterSnap(playerRoot));
    }

    private void OnTriggerExit(Collider other)
    {
        if (ResolvePlayerRoot(other) == null) return;
        if (autoTriggerCoroutine != null)
        {
            Log("Player exited — canceling pending auto-trigger.");
            StopCoroutine(autoTriggerCoroutine);
            autoTriggerCoroutine = null;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Defensive backup for OnTriggerEnter — sometimes Enter is missed when
        // the cube lands via SnapToGrid (direct transform set) rather than physics
        // motion. Stay fires every frame the player is inside; the autoTrigger
        // null-check prevents double-scheduling.
        if (mode == ActivationMode.Manual) return;
        if (mode == ActivationMode.Shuttle) return;
        if (isTraveling) return;
        if (autoTriggerCoroutine != null) return;
        if (Time.time - lastArrivalTime < postArrivalCooldown) return;
        Transform playerRoot = ResolvePlayerRoot(other);
        if (playerRoot == null) return;

        Log("OnTriggerStay caught the player (Enter was missed). Scheduling.");
        autoTriggerCoroutine = StartCoroutine(AutoTriggerAfterSnap(playerRoot));
    }

    /// <summary>
    /// Returns the player's root transform when 'col' is on the Player or any of
    /// its descendants. Returns null if not part of a Player. We walk up the
    /// hierarchy so a collider on a child mesh (e.g. a 'Plane' child used for
    /// rendering a face) still resolves to the parent that owns CubeRollMovement,
    /// CubeState, and CubeOrientation.
    /// </summary>
    private static Transform ResolvePlayerRoot(Collider col)
    {
        Transform t = col.transform;
        while (t != null)
        {
            if (t.CompareTag("Player")) return t;
            t = t.parent;
        }
        return null;
    }

    private IEnumerator AutoTriggerAfterSnap(Transform player)
    {
        Log("AutoTriggerAfterSnap started — waiting for snap.");

        CubeState state = player.GetComponent<CubeState>();
        if (state == null) state = player.GetComponentInChildren<CubeState>();
        if (state == null && verboseLogging)
        {
            Debug.LogWarning("[Elevator] Player has no CubeState component anywhere on its hierarchy!");
        }

        float waited = 0f;
        while (state != null && !state.isSnapped && waited < 2f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        Log($"Snap wait complete. waited={waited:F2}s, isSnapped={state?.isSnapped}");

        yield return new WaitForSeconds(autoTriggerDelay);
        Log("Grace period over. About to Toggle().");

        // For HollowFaceDown mode, check the cube's orientation now that it's
        // settled. If the hollow face isn't on the bottom, silently bail —
        // the player can re-roll into position to try again.
        if (mode == ActivationMode.HollowFaceDown)
        {
            CubeOrientation orientation = player.GetComponent<CubeOrientation>();
            if (orientation == null) orientation = player.GetComponentInChildren<CubeOrientation>();
            if (orientation == null)
            {
                if (verboseLogging)
                    Debug.LogWarning("[Elevator] HollowFaceDown mode but Player has no CubeOrientation component!");
                autoTriggerCoroutine = null;
                yield break;
            }
            if (!orientation.IsHollowFaceOnBottom())
            {
                Log("HollowFaceDown mode: hollow face is NOT on bottom — not moving. Re-roll to try again.");
                autoTriggerCoroutine = null;
                yield break;
            }
            Log("HollowFaceDown mode: hollow face IS on bottom — proceeding with Toggle.");
        }

        // Final check: is the player ACTUALLY standing on the elevator right
        // now, or did they roll past while we were waiting? OnTriggerEnter only
        // requires brushing the trigger zone, but Toggle should only fire if
        // the cube has committed to landing on this elevator.
        Transform riderNow = FindRiderOnTop();
        if (riderNow == null)
        {
            Log("Player rolled off / past before grace period ended — aborting Toggle.");
            autoTriggerCoroutine = null;
            yield break;
        }

        if (!isTraveling)
        {
            Toggle();
        }
        else
        {
            Log("Skipping Toggle — already traveling.");
        }

        autoTriggerCoroutine = null;
    }

    // ============================================================
    // TRAVEL
    // ============================================================

    private IEnumerator Travel(Endpoint destination)
    {
        isTraveling = true;

        // Find the player if they're standing on us, but only if this elevator
        // is configured to carry riders. Shuttle in "slippery" mode
        // (carryRider = false) leaves the player behind to test their timing.
        Transform rider = carryRider ? FindRiderOnTop() : null;
        if (rider != null) AttachRider(rider);

        // Use a canonical offset: cube center sits directly above the elevator
        // center, with its bottom resting on the elevator's top surface. This
        // is independent of where the cube was when the ride started.
        //   X, Z: cube directly above elevator center
        //   Y: elevator transform Y + 0.1 (tile top above transform)
        //                          + 0.5 (cube center above tile top) = +0.6
        Vector3 riderStartOffset = new Vector3(0f, 0.6f, 0f);
        if (riderRoot != null)
        {
            riderRoot.position = transform.position + riderStartOffset;
        }

        Vector3 endPos = (destination == Endpoint.A) ? endpointA : endpointB;
        Vector3 control = GetEffectiveControlPoint();

        // The curve is parameterized A → control → B as t goes 0→1.
        // If we're traveling B→A, we evaluate the same curve in reverse so
        // the path shape is identical in both directions.
        bool reverse = (currentEndpoint == Endpoint.B);

        SetEmission(travelEmissionIntensity);

        float elapsed = 0f;
        while (elapsed < travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float curveT = reverse ? (1f - eased) : eased;

            transform.position = EvaluateBezier(endpointA, control, endpointB, curveT);

            // Carry the rider by updating its world position to track ours.
            // No reparenting = no scale inheritance.
            if (riderRoot != null)
            {
                riderRoot.position = transform.position + riderStartOffset;
            }

            // Pulse the emission while traveling: brighter mid-travel, softer at ends.
            float pulse = Mathf.Sin(t * Mathf.PI);
            SetEmission(travelEmissionIntensity * (0.5f + 0.5f * pulse));

            yield return null;
        }

        transform.position = endPos;
        currentEndpoint = destination;

        DetachRider();
        SetEmission(0f);

        isTraveling = false;
        lastArrivalTime = Time.time;

        // AutoReturn: after arriving at the non-starting endpoint, schedule a
        // return trip. We only auto-return on outbound arrivals, not on the
        // return trip itself — that would create an infinite ping-pong.
        if (mode == ActivationMode.AutoReturn && destination != startingEndpoint)
        {
            StartCoroutine(AutoReturnAfterDelay());
        }
    }

    private IEnumerator AutoReturnAfterDelay()
    {
        Log($"AutoReturn scheduled — returning to {startingEndpoint} in {autoReturnDelay}s.");
        yield return new WaitForSeconds(autoReturnDelay);
        if (!isTraveling)
        {
            Log("AutoReturn — starting return trip.");
            StartCoroutine(Travel(startingEndpoint));
        }
    }

    // ============================================================
    // BEZIER HELPERS
    // ============================================================

    /// <summary>
    /// Quadratic Bezier: P(t) = (1-t)²·A + 2(1-t)t·C + t²·B.
    /// Reduces to a straight line when C is the midpoint of A and B.
    /// </summary>
    private static Vector3 EvaluateBezier(Vector3 a, Vector3 c, Vector3 b, float t)
    {
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * c + (t * t) * b;
    }

    private Vector3 GetEffectiveControlPoint()
    {
        return useAutoControlPoint ? (endpointA + endpointB) * 0.5f : controlPoint;
    }

    // ============================================================
    // RIDER MANAGEMENT
    // ============================================================

    private Transform FindRiderOnTop()
    {
        // Cast a small box upward from the elevator's position to find a player
        // sitting on top. This avoids relying on lingering OnTrigger state.
        Collider col = GetComponent<Collider>();
        Bounds b = col.bounds;
        Vector3 center = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
        Vector3 halfExtents = new Vector3(b.extents.x * 0.9f, 0.5f, b.extents.z * 0.9f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity);
        foreach (Collider hit in hits)
        {
            Transform root = ResolvePlayerRoot(hit);
            if (root != null) return root;
        }
        return null;
    }

    private void AttachRider(Transform rider)
    {
        riderRoot = rider;
        riderOriginalParent = rider.parent;
        riderMovement = rider.GetComponent<CubeRollMovement>();
        if (riderMovement == null) riderMovement = rider.GetComponentInChildren<CubeRollMovement>();
        if (riderMovement != null) riderMovement.enabled = false;
        // NOTE: We deliberately do NOT call rider.SetParent(transform). Parenting
        // would cause the rider to inherit the elevator's non-uniform scale
        // (e.g. (1, 0.2, 1) for a flat tile), visually squashing the cube.
        // Instead, Travel() carries the rider by updating its world position
        // every frame to follow the elevator. See riderStartOffset there.
    }

    private void DetachRider()
    {
        if (riderRoot == null) return;
        Transform rider = riderRoot;

        // Re-snap the rider in X/Z to the grid. Y is reconstructed from the
        // arrived endpoint's Y plus the tile-top-to-cube-center offset
        // (tile top sits 0.1 above transform.position.y, cube center is 0.5 above tile top).
        Vector3 arrivedPos = (currentEndpoint == Endpoint.A) ? endpointA : endpointB;
        Vector3 p = rider.position;
        p.x = Mathf.Round(p.x - 0.5f) + 0.5f;
        p.z = Mathf.Round(p.z - 0.5f) + 0.5f;
        p.y = arrivedPos.y + 0.1f + 0.5f;
        rider.position = p;

        if (riderMovement != null) riderMovement.enabled = true;
        riderMovement = null;
        riderRoot = null;
        riderOriginalParent = null;
    }

    // ============================================================
    // EMISSION
    // ============================================================

    private void SetEmission(float intensity)
    {
        if (pulseRenderer == null || propBlock == null) return;
        pulseRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(EmissionColorId, travelEmissionColor * intensity);
        pulseRenderer.SetPropertyBlock(propBlock);
    }

    // ============================================================
    // GIZMOS
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 control = GetEffectiveControlPoint();

        // Endpoints — cyan wireframe boxes show where the elevator starts and ends.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(endpointA, new Vector3(1f, 0.2f, 1f));
        Gizmos.DrawWireCube(endpointB, new Vector3(1f, 0.2f, 1f));

        // Control point (only highlight if it's actually being used).
        if (!useAutoControlPoint)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(control, 0.15f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(endpointA, control);
            Gizmos.DrawLine(control, endpointB);
        }

        // Sampled curve preview — the actual path the elevator will travel.
        Gizmos.color = Color.cyan;
        const int segments = 24;
        Vector3 prev = endpointA;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 next = EvaluateBezier(endpointA, control, endpointB, t);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    // ============================================================
    // LOGGING
    // ============================================================

    /// <summary>
    /// Logs the message to the Console only when verboseLogging is enabled.
    /// Lets us keep all the diagnostic instrumentation in place for future
    /// debugging without spamming the console in production. Toggle the
    /// "Verbose Logging" checkbox in the inspector to enable.
    /// </summary>
    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[Elevator:{name}] {message}");
    }
}