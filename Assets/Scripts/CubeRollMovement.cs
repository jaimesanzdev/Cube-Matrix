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
    
    public bool isMoving = false;
    private CubeOrientation orientation;

    void Start()
    {
        orientation = GetComponent<CubeOrientation>();
    
        // snap to grid on start so all rolls begin from a clean position
        SnapToGrid();
        SnapRotation();
    }
    
    void Update()
    {
        if (isMoving) return;
        if (Keyboard.current == null) return;

        Vector3 direction = Vector3.zero;

        if (Keyboard.current.upArrowKey.isPressed)
            direction = Vector3.forward;
        else if (Keyboard.current.downArrowKey.isPressed)
            direction = Vector3.back;
        else if (Keyboard.current.leftArrowKey.isPressed)
            direction = Vector3.left;
        else if (Keyboard.current.rightArrowKey.isPressed)
            direction = Vector3.right;

        if (direction != Vector3.zero && CanMove(direction))
        {
            StartCoroutine(Roll(direction));
        }
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