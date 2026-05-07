using System.Collections;
using UnityEngine;

/// <summary>
/// Provides player feedback when the cube attempts to roll into an invalid
/// space (no tile, or a pushable block that can't move). Combines three
/// feedback channels:
///   - Visual: a brief "tip and recover" rotation toward the blocked
///     direction. The cube uses the same pivot-and-rotation as a real roll,
///     but only travels a fraction of the way before reversing — reading
///     as "the cube tried to commit to the roll but got rejected."
///   - Audio: a one-shot sound effect (typically a soft thud or bump).
///   - Haptic: device vibration on mobile, gamepad rumble on desktop with
///     a controller. Optional — silently skipped if unavailable.
///
/// Architecture:
///   This component is fully additive. It lives on the Player and exposes
///   public methods/properties that CubeRollMovement uses:
///     - OnInvalidRollAttempted(direction) — called when a move is rejected
///     - IsPlayingBump — true while the tip animation is in progress
///   If this component isn't on the Player, CubeRollMovement's null-guarded
///   call does nothing and the game behaves exactly as before.
///
/// Cooldown:
///   Update() in CubeRollMovement fires every frame the player holds a key,
///   which would re-trigger feedback at 60Hz. To prevent audio spam, vibration
///   battery drain, and overlapping animations, this component enforces a
///   per-direction cooldown. While the player is holding a key into a wall,
///   the bump fires once, then ignores subsequent calls in that direction
///   until the cooldown elapses. Releasing the key or changing direction
///   resets the cooldown immediately.
///
/// Setup:
///   1. Add this component to the Player GameObject.
///   2. (Optional) Drag an AudioSource into the Audio Source field, and
///      assign a clip to Bump Sound.
///   3. (Optional) Tune the tip animation values: Tip Angle for how far the
///      cube tilts, Bump Duration for how long the tip+recover takes.
///   4. (Optional) Disable Use Haptics if you don't want vibration.
/// </summary>
public class RollFeedback : MonoBehaviour
{
    [Header("Visual Tip")]
    [Tooltip("How many degrees the cube tilts toward the blocked direction during the tip animation. " +
             "15° is a subtle wobble; 25° is a clear 'tried to roll' gesture; 45° is dramatic. " +
             "A full roll is 90°.")]
    [Range(5f, 45f)]
    [SerializeField] private float tipAngle = 20f;

    [Tooltip("Total duration of the tip-and-recover animation in seconds.")]
    [SerializeField] private float bumpDuration = 0.22f;

    [Tooltip("Cell size used to compute the tip pivot. Should match CubeRollMovement.cellSize. " +
             "Default is 1, which matches the project's tile grid.")]
    [SerializeField] private float cellSize = 1f;

    [Header("Audio")]
    [Tooltip("AudioSource on the player to play the bump sound through. " +
             "If null, attempts to find one on this GameObject. If still null, audio is skipped.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound effect to play when an invalid roll is attempted. " +
             "Assign a short bump/thud clip. If null, audio is skipped.")]
    [SerializeField] private AudioClip bumpSound;

    [Tooltip("Volume multiplier for the bump sound (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float bumpVolume = 0.7f;

    [Header("Haptics")]
    [Tooltip("If checked, attempts to trigger device vibration on supported platforms. " +
             "Mobile: uses Handheld.Vibrate. Desktop: silently skipped (gamepad rumble can be added later).")]
    [SerializeField] private bool useHaptics = true;

    [Header("Cooldown")]
    [Tooltip("Minimum seconds between consecutive bumps in the same direction. " +
             "Prevents audio/haptic spam when the player holds a key against a wall. " +
             "Releasing the key or changing direction resets immediately.")]
    [SerializeField] private float perDirectionCooldown = 0.5f;

    [Header("Debug")]
    [Tooltip("If checked, logs feedback events to the Console. Off for production.")]
    [SerializeField] private bool verboseLogging = false;

    // -- runtime state --
    private bool isPlayingBump;
    private Vector3 lastBumpDirection;
    private float lastBumpTime = -1000f;

    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary>
    /// True while the tip animation is actively rotating the cube. CubeRollMovement
    /// reads this so it doesn't start a real roll on top of an in-progress tip
    /// (which would compound rotations and leave the cube off-grid).
    /// </summary>
    public bool IsPlayingBump => isPlayingBump;

    /// <summary>
    /// Called by CubeRollMovement when a roll attempt is rejected. Triggers
    /// visual, audio, and haptic feedback subject to the per-direction cooldown.
    /// </summary>
    public void OnInvalidRollAttempted(Vector3 direction)
    {
        // Cooldown gate: same direction within the cooldown window? Skip.
        bool sameDirection = (direction == lastBumpDirection);
        bool withinCooldown = (Time.time - lastBumpTime) < perDirectionCooldown;
        if (sameDirection && withinCooldown)
        {
            return;
        }

        // If a previous bump animation is still playing, skip rather than
        // stacking. This can happen if the player rapidly mashes different
        // directions all into walls.
        if (isPlayingBump)
        {
            Log($"Skipping bump in {direction} — animation already in progress.");
            return;
        }

        Log($"Invalid roll attempted in {direction} — firing feedback.");

        lastBumpDirection = direction;
        lastBumpTime = Time.time;

        StartCoroutine(TipAnimation(direction));
        PlayBumpSound();
        TriggerHaptics();
    }

    // ============================================================
    // VISUAL FEEDBACK — TIP & RECOVER
    // ============================================================

    /// <summary>
    /// Tip-and-recover animation. Mirrors the same pivot-and-rotate motion as
    /// a real roll (CubeRollMovement.Roll), but only travels a fraction of the
    /// way before reversing. Reads as "the cube started to roll, then got
    /// stopped and rocked back." Same gesture, aborted partway.
    ///
    /// IMPORTANT: While this coroutine is running, CubeRollMovement should
    /// not start a real roll, or both coroutines will fight over the
    /// transform and leave the cube off-grid. CubeRollMovement uses the
    /// IsPlayingBump getter to gate its Update().
    /// </summary>
    private IEnumerator TipAnimation(Vector3 direction)
    {
        isPlayingBump = true;

        // Snapshot starting position and rotation. We'll restore both exactly
        // at the end to avoid any floating-point drift from the cumulative
        // small RotateAround calls.
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        // Compute pivot: bottom edge of the cube in the direction of travel.
        // This matches CubeRollMovement.Roll's pivot computation exactly, so
        // the gesture reads as a real roll's first moments.
        Vector3 pivot = startPosition + (Vector3.down + direction) * (cellSize / 2f);

        // Rotation axis: perpendicular to direction in the horizontal plane.
        // Same as CubeRollMovement.Roll's axis.
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction);

        float halfDuration = bumpDuration * 0.5f;

        // Tip out: rotate from 0 to tipAngle degrees around the pivot.
        float currentAngle = 0f;
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            // Ease out: decelerate as we approach max tip, like running into resistance.
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float targetAngle = tipAngle * eased;
            float deltaAngle = targetAngle - currentAngle;
            transform.RotateAround(pivot, rotationAxis, deltaAngle);
            currentAngle = targetAngle;
            yield return null;
        }

        // Tip back: rotate from tipAngle back to 0.
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            // Ease in: accelerate as we recover, like the cube falling back to rest.
            float eased = t * t;
            float targetAngle = tipAngle * (1f - eased);
            float deltaAngle = targetAngle - currentAngle;
            transform.RotateAround(pivot, rotationAxis, deltaAngle);
            currentAngle = targetAngle;
            yield return null;
        }

        // Restore exactly — RotateAround accumulates floating-point error over
        // many small steps, so we explicitly set the cube back to its starting
        // pose. This is critical because CubeRollMovement's grid math depends
        // on clean 90-degree rotations and on-grid positions.
        transform.position = startPosition;
        transform.rotation = startRotation;

        isPlayingBump = false;
    }

    /// <summary>
    /// Defensive cleanup: if the script is disabled mid-animation (object
    /// deactivated, scene change, etc.), force-stop the bump flag so the
    /// cube isn't left in a stuck "playing bump" state.
    /// </summary>
    private void OnDisable()
    {
        if (isPlayingBump)
        {
            isPlayingBump = false;
            // Note: we don't try to restore transform here. If the object's
            // being disabled mid-animation, position state is the calling
            // system's responsibility.
            Log("OnDisable while bump in progress — forcing isPlayingBump=false.");
        }
    }

    // ============================================================
    // AUDIO FEEDBACK
    // ============================================================

    private void PlayBumpSound()
    {
        if (bumpSound == null) return;

        AudioSource src = audioSource;
        if (src == null) src = GetComponent<AudioSource>();
        if (src == null)
        {
            Log("Bump sound assigned but no AudioSource available — skipping audio.");
            return;
        }

        src.PlayOneShot(bumpSound, bumpVolume);
    }

    // ============================================================
    // HAPTIC FEEDBACK
    // ============================================================

    private void TriggerHaptics()
    {
        if (!useHaptics) return;

#if UNITY_ANDROID || UNITY_IOS
        // Mobile: trigger a brief device vibration. Handheld.Vibrate is a
        // single short pulse; for finer control we'd need platform-specific
        // plugins, but this is a sensible default for a "bump" feedback.
        Handheld.Vibrate();
        Log("Haptic: device vibration triggered.");
#else
        // Desktop / other platforms: gamepad rumble could go here in the
        // future. For now, silently skip.
        Log("Haptic: skipped (no haptic API on this platform).");
#endif
    }

    // ============================================================
    // LOGGING
    // ============================================================

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[RollFeedback:{name}] {message}");
    }
}