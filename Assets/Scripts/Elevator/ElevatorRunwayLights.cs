using UnityEngine;

/// <summary>
/// Runway-lights style path indicator for an elevator.
///
/// Spawns N small glowing dots evenly spaced along the elevator's Bezier path.
/// While idle, the dots glow softly. While the elevator is traveling (or in
/// always-active modes like Shuttle), the dots pulse in patterns that match
/// the elevator's behavior — forward sweeps for one-way trips, bounce for
/// AutoReturn, continuous for Shuttle, etc.
///
/// Pattern selection:
///   - By default, the runway reads its elevator's Mode and picks a matching
///     pulse pattern automatically (Auto → ForwardPulse, Shuttle → ContinuousPulse,
///     AutoReturn → BounceSweep, etc.).
///   - To override, uncheck "Auto Detect Pattern" and pick a Pulse Pattern manually.
///
/// Color selection:
///   - By default, the runway pulls its color from the elevator's material
///     so the dots match the elevator's hue automatically.
///   - To override, uncheck "Auto Detect Color" and tune Idle Color and
///     Travel Color manually.
///
/// SETUP:
///   1. Hierarchy → right-click → Create Empty. Name it something like "ElevatorRunway".
///   2. Add this script.
///   3. Drag the Elevator from Hierarchy into the Elevator field.
///   4. Press Play.
/// </summary>
public class ElevatorRunwayLights : MonoBehaviour
{
    public enum PulsePattern
    {
        ForwardPulse,     // single wave sweeping A→B repeatedly
        StaticGlow,       // all dots glow at idle brightness, no motion
        SlowPulse,        // like ForwardPulse but slower with longer gap — for conditional elevators
        ContinuousPulse,  // fast continuous forward sweep — for always-active elevators
        BounceSweep       // forward pulse A→B, then return pulse B→A — for AutoReturn
    }

    [SerializeField] private Elevator elevator;

    [Header("Pattern")]
    [Tooltip("If checked, the runway picks its pulse pattern based on the " +
             "elevator's Mode automatically. Uncheck to override with a custom pattern.")]
    [SerializeField] private bool autoDetectPattern = true;

    [Tooltip("Pulse pattern when 'Auto Detect Pattern' is unchecked. Lets designers " +
             "override the default mode→pattern mapping.")]
    [SerializeField] private PulsePattern overridePattern = PulsePattern.ForwardPulse;

    [Header("Color Source")]
    [Tooltip("If checked, the runway pulls its color from the elevator's material " +
             "automatically (no need to set Idle/Travel Color below). Uncheck to " +
             "use the manual Idle Color and Travel Color fields instead. If the " +
             "elevator has no material, falls back to the manual values.")]
    [SerializeField] private bool autoDetectColor = true;

    [Header("Layout")]
    [Tooltip("Number of dots along the path. 6-10 reads cleanly. More = denser path.")]
    [Range(3, 20)]
    [SerializeField] private int dotCount = 8;

    [Tooltip("Diameter of each dot in world units.")]
    [SerializeField] private float dotSize = 0.18f;

    [Header("Idle Appearance")]
    [Tooltip("Color of the dots when the elevator is at rest. Lower alpha = ghostlier. " +
             "Overridden if Auto Detect Color is checked.")]
    [SerializeField] private Color idleColor = new Color(0.3f, 0.7f, 1f, 0.4f);

    [Tooltip("Brightness multiplier while idle. <1 dims the dots vs travel.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleBrightness = 0.3f;

    [Header("Travel Appearance")]
    [Tooltip("Color of the dots while the elevator is traveling. " +
             "Overridden if Auto Detect Color is checked.")]
    [SerializeField] private Color travelColor = new Color(0.4f, 1f, 1f, 1f);

    [Tooltip("How fast the pulse wave sweeps along the path. " +
             "1 = one sweep per second. 2 = two sweeps per second. " +
             "Continuous and Slow patterns multiply this for tempo variation.")]
    [SerializeField] private float pulseSpeed = 1.5f;

    [Tooltip("Width of the bright band that travels along the dots, as a " +
             "fraction of the path length. 0.2 = a band covering ~20% of the " +
             "dots glows brightly at any time.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float pulseWidth = 0.25f;

    [Header("Debug")]
    [Tooltip("If checked, the runway logs its setup state to the Console. " +
             "Useful for diagnosing why dots aren't appearing or matching colors. " +
             "Leave off for production.")]
    [SerializeField] private bool verboseLogging = false;

    // -- runtime state --
    private Transform[] dots;
    private Material[] dotMaterials;
    private PulsePattern resolvedPattern;

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Start()
    {
        if (elevator == null)
        {
            Debug.LogWarning("[ElevatorRunwayLights] No elevator assigned. Disabling.", this);
            enabled = false;
            return;
        }
        resolvedPattern = ResolvePattern();
        ResolveColors();
        SpawnDots();
        Log($"Runway initialized. Pattern={resolvedPattern}, dots={dotCount}");
    }

    private void Update()
    {
        if (elevator == null || dots == null) return;

        bool traveling = elevator.IsTraveling;

        for (int i = 0; i < dotCount; i++)
        {
            float dotT = (i + 1f) / (dotCount + 1f);
            float pulse = ComputePulseForDot(dotT, traveling);

            float brightness = Mathf.Lerp(idleBrightness, 1f, pulse);
            Color baseColor = Color.Lerp(idleColor, travelColor, pulse);

            Color final = baseColor * brightness;
            final.a = baseColor.a;
            dotMaterials[i].color = final;
        }
    }

    private void OnDestroy()
    {
        if (dotMaterials == null) return;
        foreach (var mat in dotMaterials) if (mat != null) Destroy(mat);
    }

    // ============================================================
    // SETUP
    // ============================================================

    /// <summary>
    /// Pull the runway's idle and travel colors from the elevator's material.
    /// Idle = same hue at lower alpha, travel = same hue at full alpha.
    /// If autoDetectColor is off, or the elevator has no material, leave the
    /// manual fields as-is.
    /// </summary>
    private void ResolveColors()
    {
        if (!autoDetectColor) return;

        MeshRenderer mr = elevator.GetComponent<MeshRenderer>();
        if (mr == null || mr.sharedMaterial == null)
        {
            Log("Auto-color enabled but elevator has no MeshRenderer/material. Using manual colors.");
            return;
        }

        Color baseColor = mr.sharedMaterial.color;
        travelColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        idleColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f);
    }

    /// <summary>
    /// Decide which pattern to use based on either the elevator's Mode (auto-detect)
    /// or the manual override field.
    /// </summary>
    private PulsePattern ResolvePattern()
    {
        if (!autoDetectPattern) return overridePattern;

        switch (elevator.Mode)
        {
            case Elevator.ActivationMode.Auto: return PulsePattern.ForwardPulse;
            case Elevator.ActivationMode.Manual: return PulsePattern.StaticGlow;
            case Elevator.ActivationMode.HollowFaceDown: return PulsePattern.SlowPulse;
            case Elevator.ActivationMode.Shuttle: return PulsePattern.ContinuousPulse;
            case Elevator.ActivationMode.AutoReturn: return PulsePattern.BounceSweep;
            default: return PulsePattern.ForwardPulse;
        }
    }

    private void SpawnDots()
    {
        dots = new Transform[dotCount];
        dotMaterials = new Material[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = $"RunwayDot_{i}";
            // CRITICAL: kill the collider so it can't interfere with anything,
            // especially the elevator's player-detection trigger volume.
            Collider col = dot.GetComponent<Collider>();
            if (col != null) Destroy(col);

            dot.transform.SetParent(transform, worldPositionStays: false);
            dot.transform.localScale = Vector3.one * dotSize;

            // Each dot gets its own material instance so we can animate color
            // independently. Unlit/Color shader keeps the dots bright regardless
            // of scene lighting.
            MeshRenderer mr = dot.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = idleColor;
            mr.material = mat;
            dotMaterials[i] = mat;

            dots[i] = dot.transform;
        }

        PositionDots();
    }

    private void PositionDots()
    {
        for (int i = 0; i < dotCount; i++)
        {
            float t = (i + 1f) / (dotCount + 1f);
            dots[i].position = SamplePath(t);
        }
    }

    // ============================================================
    // PULSE PATTERNS
    // ============================================================

    /// <summary>
    /// Compute the pulse intensity (0..1) for the dot at parameter dotT,
    /// based on the current pattern. Returns 0 = idle, 1 = peak bright.
    /// </summary>
    private float ComputePulseForDot(float dotT, bool traveling)
    {
        switch (resolvedPattern)
        {
            case PulsePattern.StaticGlow:
                // Manual mode: all dots glow at idle brightness, no animation.
                // Slight brightening when actually traveling.
                return traveling ? 0.5f : 0f;

            case PulsePattern.ForwardPulse:
                if (!traveling) return 0f;
                return ForwardWave(dotT, pulseSpeed);

            case PulsePattern.SlowPulse:
                // Half-speed forward sweep — "thoughtful" feel for conditional elevators.
                if (!traveling) return 0f;
                return ForwardWave(dotT, pulseSpeed * 0.5f);

            case PulsePattern.ContinuousPulse:
                // Always pulsing, even when idle. Faster tempo for urgency.
                // For Shuttle elevators that are always active.
                return ForwardWave(dotT, pulseSpeed * 1.5f);

            case PulsePattern.BounceSweep:
                // Forward sweep when going A→B, reverse sweep when going B→A.
                // CurrentEndpoint reflects the origin during travel, so:
                //   currentEndpoint == A → heading to B → wave moves 0→1
                //   currentEndpoint == B → heading to A → wave moves 1→0
                if (!traveling) return 0f;
                bool headingToB = (elevator.CurrentEndpoint == Elevator.Endpoint.A);
                return DirectionalWave(dotT, pulseSpeed, headingToB);

            default:
                return 0f;
        }
    }

    /// <summary>
    /// Forward-traveling wave: the bright band's center moves 0→1 along the path.
    /// </summary>
    private float ForwardWave(float dotT, float speed)
    {
        float wavePhase = (Time.time * speed) % 1f;
        float dist = Mathf.Abs(dotT - wavePhase);
        dist = Mathf.Min(dist, 1f - dist); // wrap-around for seamless loop
        float pulse = Mathf.Clamp01(1f - dist / pulseWidth);
        return pulse * pulse;
    }

    /// <summary>
    /// Wave that travels in a chosen direction. headingToB=true → wave moves
    /// 0→1. headingToB=false → wave moves 1→0.
    /// </summary>
    private float DirectionalWave(float dotT, float speed, bool headingToB)
    {
        float wavePhase = (Time.time * speed) % 1f;
        if (!headingToB) wavePhase = 1f - wavePhase;
        float dist = Mathf.Abs(dotT - wavePhase);
        dist = Mathf.Min(dist, 1f - dist);
        float pulse = Mathf.Clamp01(1f - dist / pulseWidth);
        return pulse * pulse;
    }

    // ============================================================
    // PATH SAMPLING
    // ============================================================

    private Vector3 SamplePath(float t)
    {
        Vector3 a = elevator.EndpointA;
        Vector3 b = elevator.EndpointB;
        Vector3 c = elevator.ControlPoint;
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * c + (t * t) * b;
    }

    // ============================================================
    // GIZMOS
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (elevator == null) return;
        Gizmos.color = new Color(0.3f, 1f, 1f, 0.4f);
        Vector3 prev = elevator.EndpointA;
        for (int i = 1; i <= 16; i++)
        {
            float t = i / 16f;
            Vector3 next = SamplePath(t);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        if (!Application.isPlaying)
        {
            Gizmos.color = new Color(0.4f, 1f, 1f, 0.8f);
            for (int i = 0; i < dotCount; i++)
            {
                float t = (i + 1f) / (dotCount + 1f);
                Gizmos.DrawWireSphere(SamplePath(t), dotSize * 0.5f);
            }
        }
    }

    // ============================================================
    // LOGGING
    // ============================================================

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[ElevatorRunwayLights:{name}] {message}");
    }
}