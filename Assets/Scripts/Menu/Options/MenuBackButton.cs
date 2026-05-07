using UnityEngine;
using UnityEngine.InputSystem;

public class MenuBackButton : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MenuFlowController menuFlowController;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                menuFlowController?.ReturnFromOptionsOrCredits();
            }
        }

        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            TryPress(Pointer.current.position.ReadValue());
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TryPress(Touchscreen.current.primaryTouch.position.ReadValue());
        }
    }

    private void TryPress(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            menuFlowController?.ReturnFromOptionsOrCredits();
        }
    }
}