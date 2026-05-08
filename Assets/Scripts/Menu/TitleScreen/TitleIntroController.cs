using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TitleIntroController : MonoBehaviour
{
    [Header("Title Letters")]
    [SerializeField] private List<VoxelWordDisplay> titleLetters = new();

    [Header("Title Timing")]
    [SerializeField] private float initialDelay = 0.2f;
    [SerializeField] private float letterShowStagger = 0.045f;
    [SerializeField] private float letterHideStagger = 0.02f;
    [SerializeField] private float waitAfterTitleAppears = 0.15f;

    [Header("Menu Cube")]
    [SerializeField] private GameObject menuCubeObject;
    [SerializeField] private Transform menuCubeRoot;
    [SerializeField] private float menuCubeStartYOffset = 8f;
    [SerializeField] private float menuCubeDropDuration = 0.65f;
    [SerializeField] private AnimationCurve menuCubeDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Menu Systems To Enable After Intro")]
    [SerializeField] private MonoBehaviour cubeMenuController;
    [SerializeField] private MonoBehaviour cubeMenuIdle;
    [SerializeField] private MonoBehaviour cubeFaceWordManager;

    [Header("Input")]
    [SerializeField] private bool allowSpace = true;
    [SerializeField] private bool allowMouseClick = true;
    [SerializeField] private bool allowTouch = true;

    private bool titleFinishedShowing = false;
    private bool introTransitionStarted = false;
    private Vector3 menuCubeShownPosition;

    private void Awake()
    {
        if (menuCubeRoot != null)
            menuCubeShownPosition = menuCubeRoot.position;

        // Ocultar y desactivar el cubo del menú al principio
        if (menuCubeObject != null)
            menuCubeObject.SetActive(false);

        if (cubeMenuController != null)
            cubeMenuController.enabled = false;

        if (cubeMenuIdle != null)
            cubeMenuIdle.enabled = false;

        if (cubeFaceWordManager != null)
            cubeFaceWordManager.enabled = false;

        // Dejar el título oculto y preparado
        for (int i = 0; i < titleLetters.Count; i++)
        {
            if (titleLetters[i] != null)
                titleLetters[i].ForceHiddenState();
        }
    }

    private void Start()
    {
        StartCoroutine(IntroSequence());
    }

    private void Update()
    {
        if (!titleFinishedShowing || introTransitionStarted)
            return;

        if (allowSpace && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartCoroutine(ExitTitleAndEnterMenu());
            return;
        }

        if (allowMouseClick && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            StartCoroutine(ExitTitleAndEnterMenu());
            return;
        }

        if (allowTouch && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            StartCoroutine(ExitTitleAndEnterMenu());
        }
    }

    private IEnumerator IntroSequence()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        for (int i = 0; i < titleLetters.Count; i++)
        {
            if (titleLetters[i] != null)
                titleLetters[i].ShowWord(i * letterShowStagger);
        }

        float totalShowTime = waitAfterTitleAppears + Mathf.Max(0f, titleLetters.Count - 1) * letterShowStagger + 0.6f;
        yield return new WaitForSeconds(totalShowTime);

        titleFinishedShowing = true;
    }

    private IEnumerator ExitTitleAndEnterMenu()
    {
        introTransitionStarted = true;

        // Ocultar letras con animación
        for (int i = 0; i < titleLetters.Count; i++)
        {
            if (titleLetters[i] != null)
                titleLetters[i].HideWord(i * letterHideStagger);
        }

        float totalHideTime = Mathf.Max(0f, titleLetters.Count - 1) * letterHideStagger + 0.25f;
        yield return new WaitForSeconds(totalHideTime);

        // Activar el cubo del menú y colocarlo arriba
        if (menuCubeObject != null)
            menuCubeObject.SetActive(true);

        if (menuCubeRoot != null)
        {
            menuCubeRoot.position = menuCubeShownPosition + Vector3.up * menuCubeStartYOffset;
            yield return StartCoroutine(AnimateMenuCubeDrop());
        }

        // Solo después de llegar, se activa el flujo normal del menú
        if (cubeFaceWordManager != null)
            cubeFaceWordManager.enabled = true;

        if (cubeMenuController != null)
            cubeMenuController.enabled = true;

        if (cubeMenuIdle != null)
            cubeMenuIdle.enabled = true;
    }

    private IEnumerator AnimateMenuCubeDrop()
    {
        Vector3 start = menuCubeShownPosition + Vector3.up * menuCubeStartYOffset;
        Vector3 end = menuCubeShownPosition;

        float elapsed = 0f;
        while (elapsed < menuCubeDropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / menuCubeDropDuration);
            float curved = menuCubeDropCurve.Evaluate(t);

            menuCubeRoot.position = Vector3.Lerp(start, end, curved);
            yield return null;
        }

        menuCubeRoot.position = end;
    }
}