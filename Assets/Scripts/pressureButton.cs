using System;
using UnityEngine;

public class pressureButton : MonoBehaviour
{
    public MovingPlatform platform;
    public bool pressurePlate = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("HollowFace") || other.CompareTag("PushableBlock"))
        {
            if (platform != null)
            {
                platform.ActivateMovement();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("HollowFace") || other.CompareTag("PushableBlock"))
        {
            if (pressurePlate && platform != null)
            {
                platform.DeactivateMovement();
            }
        }
    }
}