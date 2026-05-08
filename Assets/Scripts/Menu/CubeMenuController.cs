using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CubeMenuController : MonoBehaviour
{
    public enum MainFace
    {
        Play,
        Options,
        Credits,
        Quit
    }

    [Header("Rotation Settings")]
    [SerializeField] private float rotationDuration = 0.35f;
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 80f;

    [Header("Tap Settings")]
    [SerializeField] private float maxTapDistance = 20f;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float faceDotThreshold = 0.85f;

    [Header("Face Markers")]
    [SerializeField] private Transform playFaceMarker;
    [SerializeField] private Transform optionsFaceMarker;
    [SerializeField] private Transform creditsFaceMarker;
    [SerializeField] private Transform quitFaceMarker;

    [Header("Transitions")]
    [SerializeField] private LevelMenuTransition levelMenuTransition;
    [SerializeField] private LevelSelectTransition levelSelectTransition;

    [Header("Word System")]
    [SerializeField] private CubeFaceWordManager cubeFaceWordManager;
    [SerializeField] private VoxelWordDisplay playWordDisplay;

    [Header("Menu Flow")]
    [SerializeField] private MenuFlowController menuFlowController;

    private bool isRotating = false;
    private bool isInLevelSelect = false;
    private bool isStartingPlay = false;
    private bool inputLocked = false;

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
        if (inputLocked)
            return;

        HandleKeyboardInput();
        HandlePointerInput();
    }

    // Mantengo esto para no romper LevelMenuTransition actual
    public void SetLevelSelectMode(bool value)
    {
        isInLevelSelect = value;
    }

    public void SetInputLocked(bool value)
    {
        inputLocked = value;
        ResetPointerState();
    }

    private void ResetPointerState()
    {
        pointerPressed = false;
        pointerStartPos = Vector2.zero;
        pointerEndPos = Vector2.zero;
    }

    public MainFace GetCurrentFrontFace()
    {
        float bestDot = -999f;
        MainFace bestFace = MainFace.Play;

        EvaluateFace(playFaceMarker, MainFace.Play, ref bestDot, ref bestFace);
        EvaluateFace(optionsFaceMarker, MainFace.Options, ref bestDot, ref bestFace);
        EvaluateFace(creditsFaceMarker, MainFace.Credits, ref bestDot, ref bestFace);
        EvaluateFace(quitFaceMarker, MainFace.Quit, ref bestDot, ref bestFace);

        return bestFace;
    }

    private void EvaluateFace(Transform marker, MainFace face, ref float bestDot, ref MainFace bestFace)
    {
        if (marker == null || targetCamera == null)
            return;

        Vector3 toCamera = (targetCamera.transform.position - marker.position).normalized;
        float dot = Vector3.Dot(marker.forward, toCamera);

        if (dot > bestDot)
        {
            bestDot = dot;
            bestFace = face;
        }
    }

    private void HandleKeyboardInput()
    {
        if (isRotating || isInLevelSelect || isStartingPlay || Keyboard.current == null)
            return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            RotateCube(Vector3.up, -90f);
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            RotateCube(Vector3.up, 90f);
        }
        else if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TriggerCurrentFaceAction();
        }
    }

    private void HandlePointerInput()
    {
        if (isRotating || isStartingPlay)
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
            if (!isInLevelSelect)
            {
                ProcessSwipe(delta);
            }

            return;
        }

        if (delta.magnitude <= maxTapDistance)
        {
            if (isInLevelSelect)
            {
                TryPressLevelCube(end);
            }
            else
            {
                TriggerCurrentFaceAction();
            }
        }
    }

    private void ProcessSwipe(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
            return;

        if (delta.x > 0f)
        {
            RotateCube(Vector3.up, 90f);
        }
        else
        {
            RotateCube(Vector3.up, -90f);
        }
    }

    private void TriggerCurrentFaceAction()
    {
        if (isRotating || isStartingPlay)
            return;

        MainFace currentFace = GetCurrentFrontFace();

        if (!IsFaceReallyFront(currentFace))
            return;

        switch (currentFace)
        {
            case MainFace.Play:
                StartCoroutine(StartPlaySequence());
                break;

            case MainFace.Options:
            case MainFace.Credits:
            case MainFace.Quit:
                if (menuFlowController != null)
                    menuFlowController.OnMainFaceSelected(currentFace);
                break;
        }
    }

    private bool IsFaceReallyFront(MainFace face)
    {
        Transform marker = face switch
        {
            MainFace.Play => playFaceMarker,
            MainFace.Options => optionsFaceMarker,
            MainFace.Credits => creditsFaceMarker,
            MainFace.Quit => quitFaceMarker,
            _ => null
        };

        if (marker == null || targetCamera == null)
            return false;

        Vector3 toCamera = (targetCamera.transform.position - marker.position).normalized;
        float dot = Vector3.Dot(marker.forward, toCamera);

        return dot >= faceDotThreshold;
    }

    private IEnumerator StartPlaySequence()
        {
            isStartingPlay = true;

            if (levelMenuTransition != null && !levelMenuTransition.IsTransitionRunning)
            {
                levelMenuTransition.StartPlayTransition();
            }

            yield return null;
            isStartingPlay = false;
        }

    private void TryPressLevelCube(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        LevelCubeButton button = hit.transform.GetComponentInParent<LevelCubeButton>();
        if (button != null)
        {
            button.OnPressed();
        }
    }

    private void RotateCube(Vector3 axis, float angle)
    {
        if (isRotating)
            return;

        if (cubeFaceWordManager != null)
            cubeFaceWordManager.PreviewFaceAfterRotationStep(axis, angle);

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

        if (cubeFaceWordManager != null)
            cubeFaceWordManager.RefreshCurrentFaceAfterRotation();

        isRotating = false;
    }
}