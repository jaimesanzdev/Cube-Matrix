using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Vector3 moveOffset = new Vector3(0, 3, 0);
    public float speed = 2f;
    public float delay = 3f;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private bool isMoving = false;

    private void Start()
    {
        startPosition = transform.position;
        endPosition = startPosition + moveOffset;
    }
    private void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, endPosition, speed * Time.deltaTime);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, speed * Time.deltaTime);
        }
    }
    public void ActivateMovement()
    {   

        isMoving = true;
        Debug.Log("activated");
    }
    public void DeactivateMovement()
    {
        isMoving = false;
    }
}
