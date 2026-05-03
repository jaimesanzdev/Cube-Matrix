using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Default Timings")]
    [SerializeField] private float defaultFadeOutDuration = 0.45f;
    [SerializeField] private float defaultFadeInDuration = 0.45f;

    [Header("Auto Fade In On Scene Load")]
    [SerializeField] private bool autoFadeInOnSceneLoad = true;

    private Coroutine currentFadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoFadeInOnSceneLoad || canvasGroup == null)
            return;

        StartFadeIn(defaultFadeInDuration);
    }

    public void StartFadeOut(float duration = -1f)
    {
        if (duration <= 0f)
            duration = defaultFadeOutDuration;

        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        currentFadeCoroutine = StartCoroutine(FadeRoutine(canvasGroup.alpha, 1f, duration, true));
    }

    public void StartFadeIn(float duration = -1f)
    {
        if (duration <= 0f)
            duration = defaultFadeInDuration;

        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        currentFadeCoroutine = StartCoroutine(FadeRoutine(canvasGroup.alpha, 0f, duration, false));
    }

    public IEnumerator FadeOutRoutine(float duration = -1f)
    {
        if (duration <= 0f)
            duration = defaultFadeOutDuration;

        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        yield return FadeRoutine(canvasGroup.alpha, 1f, duration, true);
        currentFadeCoroutine = null;
    }

    public IEnumerator FadeInRoutine(float duration = -1f)
    {
        if (duration <= 0f)
            duration = defaultFadeInDuration;

        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        yield return FadeRoutine(canvasGroup.alpha, 0f, duration, false);
        currentFadeCoroutine = null;
    }

    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration, bool blockRaycasts)
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.blocksRaycasts = blockRaycasts;
        canvasGroup.interactable = blockRaycasts;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = blockRaycasts;
        canvasGroup.interactable = blockRaycasts && targetAlpha > 0.99f;
    }
}