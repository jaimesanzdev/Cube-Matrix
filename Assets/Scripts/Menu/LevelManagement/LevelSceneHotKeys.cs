using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelSceneHotkeys : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isExecuting = false;

    private void Update()
    {
        if (isExecuting || Keyboard.current == null)
            return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartCoroutine(RestartSceneRoutine());
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            StartCoroutine(GoToMainMenuRoutine());
        }
    }

    private IEnumerator RestartSceneRoutine()
    {
        isExecuting = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        yield break;
    }

    private IEnumerator GoToMainMenuRoutine()
    {
        isExecuting = true;

        if (SceneFader.Instance != null)
        {
            yield return SceneFader.Instance.FadeOutRoutine();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}