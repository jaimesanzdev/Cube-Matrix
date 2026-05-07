using UnityEngine;
using UnityEngine.InputSystem;

public class QuitOptionButton : MonoBehaviour
{
    public enum QuitChoice
    {
        Yes,
        No
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private MenuFlowController menuFlowController;
    [SerializeField] private QuitChoice choice;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                menuFlowController?.CloseQuitConfirm();
            }
            else if (Keyboard.current.enterKey.wasPressedThisFrame ||
                     Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                menuFlowController?.ConfirmQuit();
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

        if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            return;

        if (menuFlowController == null)
            return;

        switch (choice)
        {
            case QuitChoice.Yes:
                menuFlowController.ConfirmQuit();
                break;

            case QuitChoice.No:
                menuFlowController.CloseQuitConfirm();
                break;
        }
    }
}