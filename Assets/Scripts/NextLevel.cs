using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevel : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float delayAfterWin = 0.3f;

    [Header("Scene Flow")]
    [SerializeField] private string levelScenePrefix = "Level_";
    [SerializeField] private int lastLevelIndex = 9;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private bool waitForClipToFinish = true;

    [Header("Optional Fade")]
    [SerializeField] private bool useSceneFader = false;
    [SerializeField] private float fadeOutDuration = 0.35f;

    private bool isLoading = false;
    private bool isOnGoalTile = false;
    private CubeOrientation cubeOrientation;
    private CubeRollMovement cubeMovement;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        isOnGoalTile = true;
        cubeOrientation = other.GetComponent<CubeOrientation>();
        cubeMovement = other.GetComponent<CubeRollMovement>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        isOnGoalTile = false;
        cubeOrientation = null;
        cubeMovement = null;
    }

    private void Update()
    {
        if (isLoading || !isOnGoalTile || cubeOrientation == null || cubeMovement == null)
            return;

        if (cubeMovement.isMoving)
            return;

        Debug.Log("On goal tile - Hollow down: " + cubeOrientation.IsHollowFaceDown() +
                  " | Bottom face: " + cubeOrientation.GetBottomFaceName());

        if (cubeOrientation.IsHollowFaceDown())
        {
            isLoading = true;
            StartCoroutine(LoadNextLevel());
        }
    }

    private IEnumerator LoadNextLevel()
    {
        float waitTime = delayAfterWin;

        if (audioSource != null && winClip != null)
        {
            audioSource.PlayOneShot(winClip);

            if (waitForClipToFinish)
                waitTime = winClip.length;
        }

        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        if (useSceneFader && SceneFader.Instance != null)
            yield return SceneFader.Instance.FadeOutRoutine(fadeOutDuration);

        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentLevelIndex = ExtractLevelIndex(currentSceneName);

        if (currentLevelIndex < 0)
        {
            Debug.LogWarning($"NextLevel: no se pudo extraer el índice del nivel desde la escena '{currentSceneName}'. Volviendo al menú.");
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
        }

        if (currentLevelIndex >= lastLevelIndex)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            string nextSceneName = levelScenePrefix + (currentLevelIndex + 1);
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private int ExtractLevelIndex(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return -1;

        if (!sceneName.StartsWith(levelScenePrefix))
            return -1;

        string suffix = sceneName.Substring(levelScenePrefix.Length);

        if (int.TryParse(suffix, out int levelIndex))
            return levelIndex;

        return -1;
    }
}