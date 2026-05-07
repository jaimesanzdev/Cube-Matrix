using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public float speed = 2f;
    public float delay = 3f;
    private Vector3 startPosition;
    public Vector3 endPosition;
    private bool isMoving = false;

    private void Start()
    {
        startPosition = transform.position;
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
