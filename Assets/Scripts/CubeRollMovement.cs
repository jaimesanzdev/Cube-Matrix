using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CubeRollMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float rotationSpeed = 240f;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 gridOffset = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("Deteccion de casillas")]
    [SerializeField] private LayerMask tileLayer;
    [SerializeField] private float raycastHeight = 2f;
    [SerializeField] private float raycastDistance = 5f;

    [Header("Portal")]
    [SerializeField] private float portalSinkSpeed = 3f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rollClip;
    [SerializeField] private float rollVolume = 1f;
    [SerializeField] private Vector2 rollPitchRange = new Vector2(0.94f, 1.06f);

    public bool isMoving = false;

    private CubeOrientation orientation;
    private RollFeedback rollFeedback;

    private void Start()
    {
        orientation = GetComponent<CubeOrientation>();
        rollFeedback = GetComponent<RollFeedback>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // snap to grid on start so all rolls begin from a clean position
        SnapToGrid();
        SnapRotation();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // Read keyboard input and delegate to TryRoll. The gate checks
        // (isMoving, IsPlayingBump) live inside TryRoll so external input
        // sources (swipe, gamepad, etc.) go through the same gates.
        Vector3 direction = Vector3.zero;

        if (Keyboard.current.upArrowKey.isPressed)
            direction = Vector3.forward;
        else if (Keyboard.current.downArrowKey.isPressed)
            direction = Vector3.back;
        else if (Keyboard.current.leftArrowKey.isPressed)
            direction = Vector3.left;
        else if (Keyboard.current.rightArrowKey.isPressed)
            direction = Vector3.right;

        if (direction != Vector3.zero)
            TryRoll(direction);
    }

    /// <summary>
    /// Public entry point for triggering a roll attempt from any input source
    /// (keyboard reader in Update, SwipeInputProvider, future gamepad reader,
    /// custom puzzle scripts, etc.). All gate checks and rejection feedback
    /// flow through here, so every input path produces consistent behavior.
    ///
    /// Behavior:
    ///   - If the cube is already moving (rolling, portaling, or playing a
    ///     bump animation), the call is silently ignored.
    ///   - If a tile exists in the target direction, kicks off a roll
    ///     (handling pushable blocks along the way).
    ///   - If no tile exists, fires RollFeedback.OnInvalidRollAttempted so
    ///     the player gets visual/audio/haptic feedback.
    ///
    /// Direction must be one of Vector3.forward/back/left/right. Other
    /// values produce undefined rolling behavior because the grid math
    /// expects axis-aligned cardinal directions.
    /// </summary>
    public void TryRoll(Vector3 direction)
    {
        if (isMoving) return;
        if (rollFeedback != null && rollFeedback.IsPlayingBump) return;
        if (direction == Vector3.zero) return;

        if (CanMove(direction))
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, cellSize))
            {
                Debug.Log("raycast hit: " + hit.collider.name);
                PushableBlock cube = hit.collider.GetComponent<PushableBlock>();
                if (cube != null)
                {
                    if (cube.IsMoving || !cube.CanMove(direction))
                    {
                        if (rollFeedback != null)
                            rollFeedback.OnInvalidRollAttempted(direction);

                        return;
                    }

                    cube.Push(direction);
                }
            }

            StartCoroutine(Roll(direction));
        }
        else if (rollFeedback != null)
        {
            rollFeedback.OnInvalidRollAttempted(direction);
        }
    }

    private void PlayRollSound()
    {
        if (audioSource == null || rollClip == null)
            return;

        float originalPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(rollPitchRange.x, rollPitchRange.y);
        audioSource.PlayOneShot(rollClip, rollVolume);
        audioSource.pitch = originalPitch;
    }

    private bool CanMove(Vector3 direction)
    {
        Vector3 targetPosition = transform.position + direction * cellSize;

        Vector3 rayOrigin = new Vector3(
            targetPosition.x,
            targetPosition.y + raycastHeight,
            targetPosition.z
        );

        bool hasTile = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            raycastDistance,
            tileLayer
        );

        return hasTile;
    }

    private IEnumerator Roll(Vector3 direction)
    {
        isMoving = true;

        Vector3 pivot = transform.position + (Vector3.down + direction) * (cellSize / 2f);
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction);

        float rotatedAngle = 0f;

        while (rotatedAngle < 90f)
        {
            float angleStep = rotationSpeed * Time.deltaTime;

            if (rotatedAngle + angleStep > 90f)
                angleStep = 90f - rotatedAngle;

            transform.RotateAround(pivot, rotationAxis, angleStep);
            rotatedAngle += angleStep;

            yield return null;
        }

        SnapToGrid();
        SnapRotation();

        PlayRollSound();

        isMoving = false;

        orientation.UpdateOrientation(rotationAxis, 90f);

        Vector3 checkPos = transform.position + Vector3.down * (cellSize / 2f);
        Collider[] hits = Physics.OverlapSphere(checkPos, 0.4f, tileLayer);
        foreach (Collider hit in hits)
        {
            PortalTile portal = hit.GetComponent<PortalTile>();
            if (portal != null)
            {
                StartCoroutine(PortalTransition(portal));
                break;
            }
        }
    }

    private void SnapToGrid()
    {
        Vector3 pos = transform.position;

        pos.x = Mathf.Round((pos.x - gridOffset.x) / cellSize) * cellSize + gridOffset.x;
        pos.y = Mathf.Round((pos.y - gridOffset.y) / cellSize) * cellSize + gridOffset.y;
        pos.z = Mathf.Round((pos.z - gridOffset.z) / cellSize) * cellSize + gridOffset.z;

        transform.position = pos;
    }

    private void SnapRotation()
    {
        Vector3 rot = transform.eulerAngles;

        rot.x = Mathf.Round(rot.x / 90f) * 90f;
        rot.y = Mathf.Round(rot.y / 90f) * 90f;
        rot.z = Mathf.Round(rot.z / 90f) * 90f;

        transform.eulerAngles = rot;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (Vector3 dir in directions)
        {
            Vector3 targetPosition = transform.position + dir * cellSize;

            Vector3 rayOrigin = new Vector3(
                targetPosition.x,
                targetPosition.y + raycastHeight,
                targetPosition.z
            );

            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * raycastDistance);
        }
    }

    private IEnumerator PortalTransition(PortalTile portal)
    {
        isMoving = true;

        Material mat = GetComponentInChildren<MeshRenderer>().material;
        float tileY = transform.position.y - cellSize / 2f; // tile surface Y

        // start clipping below tile surface
        mat.SetFloat("_ClipY", tileY);
        mat.SetFloat("_Clipping", 1.0f);

        // sink below
        Vector3 startPos = transform.position;
        Vector3 sinkTarget = startPos + Vector3.down * cellSize;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * portalSinkSpeed;
            transform.position = Vector3.Lerp(startPos, sinkTarget, t);
            yield return null;
        }

        // teleport to exit
        Vector3 exitPos = portal.GetExitPosition(transform.position.y);
        transform.position = exitPos;
        portal.SetCooldown();

        // pop up at exit
        Vector3 popTarget = exitPos + Vector3.up * cellSize;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * portalSinkSpeed;
            transform.position = Vector3.Lerp(exitPos, popTarget, t);
            yield return null;
        }

        // stop clipping
        mat.SetFloat("_Clipping", 0.0f);

        SnapToGrid();
        isMoving = false;
    }
}