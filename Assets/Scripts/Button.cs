using Unity.VisualScripting;
using UnityEngine;

public class Button : MonoBehaviour
{
    public MovingPlatform platform;
    public CubeOrientation orientation;
    public bool pressurePlate = true;
    private void OnTriggerEnter(Collider other)
    {
        if ((other.CompareTag("Player") && orientation.IsHollowFaceDown()) || other.CompareTag("PushableBlock"))
        {
            Debug.Log("Button pressed");
            if (platform == null)
            {
                Debug.Log("not assigned");
            }
            else
            {
                platform.ActivateMovement();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if ((other.CompareTag("Player") && orientation.IsHollowFaceDown()) || other.CompareTag("PushableBlock"))
        {
            Debug.Log("Button Released");
            if (pressurePlate)
            {
                if (platform == null)
                {
                    Debug.Log("not assigned");
                }
                else
                {
                    platform.DeactivateMovement();
                }
            }
        }
    }
}
