using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneActionCube : MonoBehaviour
{
    public enum SceneAction
    {
        GoToMainMenu,
        RestartScene
    }

    [Header("Action")]
    [SerializeField] private SceneAction action = SceneAction.GoToMainMenu;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Interaction")]
    [SerializeField] private bool allowMouse = true;
    [SerializeField] private bool allowTouch = true;

    private bool isExecuting = false;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (isExecuting)
            return;

        if (allowMouse && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            TryPress(Pointer.current.position.ReadValue());
        }

        if (allowTouch && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
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

        ExecuteAction();
    }

    public void ExecuteAction()
    {
        if (isExecuting)
            return;

        StartCoroutine(ExecuteActionRoutine());
    }

    private IEnumerator ExecuteActionRoutine()
    {
        isExecuting = true;

        switch (action)
        {
            case SceneAction.GoToMainMenu:
                if (SceneFader.Instance != null)
                {
                    yield return SceneFader.Instance.FadeOutRoutine();
                }

                SceneManager.LoadScene(mainMenuSceneName);
                break;

            case SceneAction.RestartScene:
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                break;
        }
    }
}