using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CubeMenuController : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationDuration = 0.35f;
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 80f;

    [Header("Tap Settings")]
    [SerializeField] private float maxTapDistance = 20f;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform playFaceMarker;
    [SerializeField] private float playFaceDotThreshold = 0.85f;

    private bool isRotating = false;
    private Quaternion targetRotation;

    private bool pointerPressed = false;
    private Vector2 pointerStartPos;
    private Vector2 pointerEndPos;

    private void Start()
    {
        targetRotation = transform.rotation;

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        HandleKeyboardInput();
        HandlePointerInput();
    }

    private void HandleKeyboardInput()
    {
        if (isRotating || Keyboard.current == null)
            return;

        // OJO: arriba/abajo están invertidos por tu preferencia personal
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            RotateCube(Vector3.right, 90f);
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            RotateCube(Vector3.right, -90f);
        }
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            RotateCube(Vector3.up, 90f);
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            RotateCube(Vector3.up, -90f);
        }
    }

    private void HandlePointerInput()
    {
        if (isRotating)
            return;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (!pointerPressed && touch.press.wasPressedThisFrame)
            {
                pointerPressed = true;
                pointerStartPos = touch.position.ReadValue();
                pointerEndPos = pointerStartPos;
            }
            else if (pointerPressed)
            {
                pointerEndPos = touch.position.ReadValue();
            }

            if (pointerPressed && touch.press.wasReleasedThisFrame)
            {
                pointerPressed = false;
                EvaluatePointerRelease(pointerStartPos, pointerEndPos);
            }

            return;
        }

        if (Pointer.current == null)
            return;

        if (!pointerPressed && Pointer.current.press.wasPressedThisFrame)
        {
            pointerPressed = true;
            pointerStartPos = Pointer.current.position.ReadValue();
            pointerEndPos = pointerStartPos;
        }
        else if (pointerPressed)
        {
            pointerEndPos = Pointer.current.position.ReadValue();
        }

        if (pointerPressed && Pointer.current.press.wasReleasedThisFrame)
        {
            pointerPressed = false;
            EvaluatePointerRelease(pointerStartPos, pointerEndPos);
        }
    }

    private void EvaluatePointerRelease(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;

        if (delta.magnitude >= minSwipeDistance)
        {
            ProcessSwipe(delta);
            return;
        }

        if (delta.magnitude <= maxTapDistance)
        {
            TryPressPlay(end);
        }
    }

    private void ProcessSwipe(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            if (delta.x > 0f)
            {
                RotateCube(Vector3.up, -90f); // swipe derecha
            }
            else
            {
                RotateCube(Vector3.up, 90f); // swipe izquierda
            }
        }
        else
        {
            // OJO: arriba/abajo invertidos por tu preferencia
            if (delta.y > 0f)
            {
                RotateCube(Vector3.right, 90f); // swipe arriba
            }
            else
            {
                RotateCube(Vector3.right, -90f); // swipe abajo
            }
        }
    }

    private void TryPressPlay(Vector2 screenPosition)
    {
        if (targetCamera == null || playFaceMarker == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            return;

        if (IsPlayFaceActive())
        {
            Debug.Log("Play");
        }
    }

    private bool IsPlayFaceActive()
    {
        Vector3 toCamera = (targetCamera.transform.position - playFaceMarker.position).normalized;
        float dot = Vector3.Dot(playFaceMarker.forward, toCamera);

        return dot >= playFaceDotThreshold;
    }

    private void RotateCube(Vector3 axis, float angle)
    {
        if (isRotating)
            return;

        Quaternion rotationStep = Quaternion.AngleAxis(angle, axis);
        targetRotation = rotationStep * targetRotation;

        StartCoroutine(RotateToTarget(targetRotation));
    }

    private IEnumerator RotateToTarget(Quaternion newTargetRotation)
    {
        isRotating = true;

        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotationDuration);
            float curvedT = rotationCurve.Evaluate(t);

            transform.rotation = Quaternion.Slerp(startRotation, newTargetRotation, curvedT);
            yield return null;
        }

        transform.rotation = newTargetRotation;
        isRotating = false;
    }
}