using UnityEngine;

public class ButtonAnySide : MonoBehaviour
{
    //Same as pressureButton script but allows all sides to activate button
    
    public MovingPlatform platform;
    public bool pressurePlate = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CubeOrientation orientation = other.GetComponent<CubeOrientation>();
            if (orientation != null && !orientation.CanPressButton()) return;
        }
        
        if (other.CompareTag("Player") || other.CompareTag("PushableBlock"))
        {
            if (platform != null)
                platform.ActivateMovement();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("PushableBlock"))
        {
            if (pressurePlate && platform != null)
                platform.DeactivateMovement();
        }
    }
}
