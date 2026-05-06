using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

public class PushableBlock : MonoBehaviour
{
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private float raycastHeight = 2f;
    [SerializeField] private float raycastDistance = 5f;
    [SerializeField] private Vector3 gridOffset = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private LayerMask tileLayer;
    [SerializeField] private float slideSpeed = 2f;

    private bool isMoving = false;
    public bool IsMoving => isMoving;

    public void Push(Vector3 direction)
    {
        if (isMoving) return;
        Vector3 targetPosition = transform.position + direction * cellSize; //calculate target position
        transform.position = SnapToGrid(targetPosition);
        StartCoroutine(Slide(targetPosition)); //coroutine for sliding of block
    }

    private IEnumerator Slide(Vector3 targetPosition)
    {
        isMoving = true;
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, slideSpeed * Time.deltaTime); //slide the pushable block to its destination instead of instant movement
            yield return null;
        }
        transform.position = targetPosition;
        isMoving = false;
    }

    public bool CanMove(Vector3 direction)
    {
        Vector3 targetPosition = transform.position + direction * cellSize; //calculate where the pushable block will end up if it moves
        Vector3 rayOrigin = new Vector3(targetPosition.x, targetPosition.y + raycastHeight, targetPosition.z);
        bool hasTile = Physics.Raycast(rayOrigin, Vector3.down, raycastDistance, tileLayer); //use raycast to detect if there is a tile below
        return hasTile; //if there is a tile, valid movement and proceed to move the pushable block
    }

    private Vector3 SnapToGrid(Vector3 pos)
    {
        pos.x = Mathf.Round((pos.x - gridOffset.x) / cellSize) * cellSize + gridOffset.x; //ensures a block cannot be in between two tiles
        pos.y = Mathf.Round((pos.y - gridOffset.y) / cellSize) * cellSize + gridOffset.y;
        pos.z = Mathf.Round((pos.z - gridOffset.z) / cellSize) * cellSize + gridOffset.z;
        return pos;
    }

}
