using UnityEngine;

public class FloatingIdleRandom : MonoBehaviour
{
    [Header("Position Idle")]
    [SerializeField] private float floatAmplitudeMin = 0.06f;
    [SerializeField] private float floatAmplitudeMax = 0.14f;
    [SerializeField] private float floatSpeedMin = 0.8f;
    [SerializeField] private float floatSpeedMax = 1.4f;

    [Header("Rotation Idle")]
    [SerializeField] private float rotationAmplitudeMin = 2f;
    [SerializeField] private float rotationAmplitudeMax = 5f;
    [SerializeField] private float rotationSpeedMin = 0.7f;
    [SerializeField] private float rotationSpeedMax = 1.3f;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    private float floatAmplitude;
    private float floatSpeed;
    private float floatOffset;

    private float rotAmplitude;
    private float rotSpeed;
    private float rotOffset;

    private bool initialized = false;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        InitializeRandomized();
    }

    public void InitializeRandomized()
    {
        floatAmplitude = Random.Range(floatAmplitudeMin, floatAmplitudeMax);
        floatSpeed = Random.Range(floatSpeedMin, floatSpeedMax);
        floatOffset = Random.Range(0f, Mathf.PI * 2f);

        rotAmplitude = Random.Range(rotationAmplitudeMin, rotationAmplitudeMax);
        rotSpeed = Random.Range(rotationSpeedMin, rotationSpeedMax);
        rotOffset = Random.Range(0f, Mathf.PI * 2f);

        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        float time = Time.time;

        float y = Mathf.Sin(time * floatSpeed + floatOffset) * floatAmplitude;
        float rotY = Mathf.Sin(time * rotSpeed + rotOffset) * rotAmplitude;

        transform.localPosition = baseLocalPosition + new Vector3(0f, y, 0f);
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, rotY, 0f);
    }
}