using UnityEngine;

public class CubeMenuIdle : MonoBehaviour
{
    [Header("Idle Float Settings")]
    [SerializeField] private float floatAmplitude = 0.15f;
    [SerializeField] private float floatSpeed = 1.2f;
    [SerializeField] private bool useUnscaledTime = false;

    private Vector3 baseLocalPosition;
    private float timeOffset;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float offsetY = Mathf.Sin((time * floatSpeed) + timeOffset) * floatAmplitude;

        transform.localPosition = baseLocalPosition + new Vector3(0f, offsetY, 0f);
    }
}