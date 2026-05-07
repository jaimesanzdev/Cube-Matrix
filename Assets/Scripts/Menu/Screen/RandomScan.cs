using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RandomScreenScan : MonoBehaviour
{
    [Header("Material Property Names")]
    [SerializeField] private string scanPositionProperty = "_ScanPosition";
    [SerializeField] private string scanIntensityProperty = "_ScanIntensity";

    [Header("Scan Timing")]
    [SerializeField] private float minScanDuration = 0.35f;
    [SerializeField] private float maxScanDuration = 1.2f;

    [Header("Wait Between Scans")]
    [SerializeField] private float minWaitTime = 0.2f;
    [SerializeField] private float maxWaitTime = 2.0f;

    [Header("Scan Intensity")]
    [SerializeField] private float minScanIntensity = 0.06f;
    [SerializeField] private float maxScanIntensity = 0.18f;

    [Header("Scan Fade")]
    [SerializeField] private float fadeInFraction = 0.15f;
    [SerializeField] private float fadeOutFraction = 0.20f;

    [Header("Direction")]
    [SerializeField] private bool randomizeDirection = true;

    private Renderer targetRenderer;
    private Material runtimeMaterial;
    private Coroutine scanRoutine;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        runtimeMaterial = targetRenderer.material;

        HideScanCompletely();
    }

    private void OnEnable()
    {
        HideScanCompletely();
        scanRoutine = StartCoroutine(ScanLoop());
    }

    private void OnDisable()
    {
        if (scanRoutine != null)
            StopCoroutine(scanRoutine);

        HideScanCompletely();
    }

    private IEnumerator ScanLoop()
    {
        while (true)
        {
            HideScanCompletely();

            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitTime);

            float duration = Random.Range(minScanDuration, maxScanDuration);
            float peakIntensity = Random.Range(minScanIntensity, maxScanIntensity);

            bool reverse = randomizeDirection && Random.value > 0.5f;
            float start = reverse ? 1f : 0f;
            float end = reverse ? 0f : 1f;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scanPos = Mathf.Lerp(start, end, t);
                float intensity = EvaluateIntensityOverLifetime(t, peakIntensity);

                runtimeMaterial.SetFloat(scanPositionProperty, scanPos);
                runtimeMaterial.SetFloat(scanIntensityProperty, intensity);

                yield return null;
            }

            HideScanCompletely();
        }
    }

    private float EvaluateIntensityOverLifetime(float t, float peakIntensity)
    {
        float fadeInEnd = Mathf.Clamp01(fadeInFraction);
        float fadeOutStart = 1f - Mathf.Clamp01(fadeOutFraction);

        if (t < fadeInEnd && fadeInEnd > 0f)
        {
            float localT = t / fadeInEnd;
            return Mathf.Lerp(0f, peakIntensity, localT);
        }

        if (t > fadeOutStart && fadeOutStart < 1f)
        {
            float localT = Mathf.InverseLerp(fadeOutStart, 1f, t);
            return Mathf.Lerp(peakIntensity, 0f, localT);
        }

        return peakIntensity;
    }

    private void HideScanCompletely()
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(scanIntensityProperty, 0f);
        runtimeMaterial.SetFloat(scanPositionProperty, -1f);
    }
}