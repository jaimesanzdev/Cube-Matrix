using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform levelParent;
    [SerializeField] private string sceneToLoad = "SampleScene";

    [Header("Move To Center")]
    [SerializeField] private float moveToCenterDuration = 0.35f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Center Pause")]
    [SerializeField] private float centerPauseDuration = 0.08f;

    [Header("Grow Animation")]
    [SerializeField] private float growDuration = 0.18f;
    [SerializeField] private float growScaleMultiplier = 8f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Explosion")]
    [SerializeField] private float explosionForce = 12f;
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float upwardsModifier = 1.2f;
    [SerializeField] private float randomTorqueStrength = 8f;
    [SerializeField] private bool enableGravityOnExplodedCubes = false;

    [Header("Scene Transition")]
    [SerializeField] private float delayBeforeFade = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.45f;

    private bool isTransitionRunning = false;

    public bool IsTransitionRunning => isTransitionRunning;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    public void SelectLevelCube(LevelCubeButton selectedButton, int levelIndex)
    {
        if (isTransitionRunning || selectedButton == null)
            return;

        StartCoroutine(LevelSelectSequence(selectedButton.transform, levelIndex));
    }

    private IEnumerator LevelSelectSequence(Transform selectedCube, int levelIndex)
    {
        isTransitionRunning = true;

        DisableAllLevelCubeInteraction();
        DisableAllLevelCubeIdles();

        yield return StartCoroutine(MoveSelectedCubeToCenter(selectedCube));
        yield return new WaitForSeconds(centerPauseDuration);

        ExplodeOtherCubes(selectedCube);
        yield return StartCoroutine(GrowSelectedCube(selectedCube));

        yield return new WaitForSeconds(delayBeforeFade);

        if (SceneFader.Instance != null)
        {
            yield return SceneFader.Instance.FadeOutRoutine(fadeOutDuration);
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneToLoad);

        while (!loadOperation.isDone)
            yield return null;
    }

    private IEnumerator MoveSelectedCubeToCenter(Transform selectedCube)
    {
        Vector3 startPosition = selectedCube.position;
        Quaternion startRotation = selectedCube.rotation;

        Vector3 targetPosition = GetCameraCenterWorldPosition(selectedCube.position);
        Quaternion targetRotation = Quaternion.identity;

        float elapsed = 0f;

        while (elapsed < moveToCenterDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveToCenterDuration);
            float curvedT = moveCurve.Evaluate(t);

            selectedCube.position = Vector3.Lerp(startPosition, targetPosition, curvedT);
            selectedCube.rotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);

            yield return null;
        }

        selectedCube.position = targetPosition;
        selectedCube.rotation = targetRotation;
    }

    private IEnumerator GrowSelectedCube(Transform selectedCube)
    {
        Vector3 startScale = selectedCube.localScale;
        Vector3 targetScale = startScale * growScaleMultiplier;

        float elapsed = 0f;

        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);
            float curvedT = growCurve.Evaluate(t);

            selectedCube.localScale = Vector3.Lerp(startScale, targetScale, curvedT);
            yield return null;
        }

        selectedCube.localScale = targetScale;
    }

    private void ExplodeOtherCubes(Transform selectedCube)
    {
        LevelCubeButton[] allButtons = levelParent.GetComponentsInChildren<LevelCubeButton>();

        foreach (LevelCubeButton button in allButtons)
        {
            if (button.transform == selectedCube)
                continue;

            Rigidbody rb = button.GetComponent<Rigidbody>();
            if (rb == null)
                rb = button.gameObject.AddComponent<Rigidbody>();

            rb.useGravity = enableGravityOnExplodedCubes;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;

            rb.AddExplosionForce(
                explosionForce,
                selectedCube.position,
                explosionRadius,
                upwardsModifier,
                ForceMode.Impulse
            );

            Vector3 randomTorque = Random.insideUnitSphere * randomTorqueStrength;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    private void DisableAllLevelCubeInteraction()
    {
        LevelCubeButton[] allButtons = levelParent.GetComponentsInChildren<LevelCubeButton>();

        foreach (LevelCubeButton button in allButtons)
        {
            button.enabled = false;
        }
    }

    private void DisableAllLevelCubeIdles()
    {
        FloatingIdleRandom[] allIdles = levelParent.GetComponentsInChildren<FloatingIdleRandom>();

        foreach (FloatingIdleRandom idle in allIdles)
        {
            idle.enabled = false;
        }
    }

    private Vector3 GetCameraCenterWorldPosition(Vector3 referenceWorldPosition)
    {
        if (targetCamera == null)
            return referenceWorldPosition;

        Vector3 referenceScreenPoint = targetCamera.WorldToScreenPoint(referenceWorldPosition);
        float depth = referenceScreenPoint.z;

        Vector3 centerScreenPoint = new Vector3(
            Screen.width * 0.5f,
            Screen.height * 0.5f,
            depth
        );

        return targetCamera.ScreenToWorldPoint(centerScreenPoint);
    }
}