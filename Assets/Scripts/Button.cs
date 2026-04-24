using UnityEngine;

public class Button : MonoBehaviour
{
    public MovingPlatform platform;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Button pressed");
        }
        if (platform == null)
        {
            Debug.Log("not assigned");
        }
        else
        {
            platform.ActivateMovement();
        }
            
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Button Released");
        }
    }
}
