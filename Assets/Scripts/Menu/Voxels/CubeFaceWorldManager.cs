using UnityEngine;

public class CubeFaceWordManager : MonoBehaviour
{
    [System.Serializable]
    public class FaceWordEntry
    {
        public string faceId;
        public Transform marker;
        public VoxelWordDisplay wordDisplay;

        // Pose local del word cuando está correctamente orientado
        // Se captura en Awake desde la rotación inicial del WordRoot en el editor
        [HideInInspector] public Quaternion localRestRotation;
    }

    [Header("References")]
    [SerializeField] private Transform rotatingCube;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private FaceWordEntry[] faces;

    [Header("Timing")]
    [SerializeField] private float initialFrontFaceDelay = 2f;
    [SerializeField] private float showDelayOnFaceSwitch = 0f;
    [SerializeField] private float hideDelayOnFaceLeave = 0f;

    private string currentFaceId = "";

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        CacheRestRotations();
    }

    private void Start()
    {
        HideAllImmediate();

        string initialFace = GetFrontFaceId(rotatingCube.rotation);
        currentFaceId = initialFace;
        ShowFace(initialFace, initialFrontFaceDelay, true, rotatingCube.rotation);
    }

    public void PreviewFaceAfterRotationStep(Vector3 axis, float angle)
    {
        if (rotatingCube == null || targetCamera == null) return;

        Quaternion simulatedRotation = Quaternion.AngleAxis(angle, axis) * rotatingCube.rotation;
        string nextFace = GetFrontFaceId(simulatedRotation);

        if (nextFace == currentFaceId) return;

        HideFace(currentFaceId, hideDelayOnFaceLeave);
        ShowFace(nextFace, showDelayOnFaceSwitch, true, simulatedRotation);
        currentFaceId = nextFace;
    }

    public void RefreshCurrentFaceAfterRotation()
    {
        if (rotatingCube == null || targetCamera == null) return;

        string frontFace = GetFrontFaceId(rotatingCube.rotation);

        if (frontFace != currentFaceId)
        {
            HideFace(currentFaceId, 0f);
            ShowFace(frontFace, 0f, true, rotatingCube.rotation);
            currentFaceId = frontFace;
        }
        else
        {
            FaceWordEntry entry = FindEntry(frontFace);
            if (entry != null)
                ApplyWordOrientation(entry, rotatingCube.rotation);
        }
    }

    // Guarda la localRotation inicial de cada word tal como está en el editor.
    // Esta es la "pose correcta de fábrica": cada word apunta bien cuando el cubo
    // está en su rotación inicial y la cara mira a cámara.
    private void CacheRestRotations()
    {
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].wordDisplay != null)
                faces[i].localRestRotation = faces[i].wordDisplay.transform.localRotation;
        }
    }

    private string GetFrontFaceId(Quaternion cubeRotation)
    {
        float bestDot = -999f;
        string bestId = "";

        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].marker == null) continue;

            Quaternion markerWorldRot = cubeRotation * faces[i].marker.localRotation;
            Vector3 markerWorldPos = rotatingCube.position + (cubeRotation * faces[i].marker.localPosition);
            Vector3 markerForward = markerWorldRot * Vector3.forward;
            Vector3 toCamera = (targetCamera.transform.position - markerWorldPos).normalized;
            float dot = Vector3.Dot(markerForward, toCamera);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestId = faces[i].faceId;
            }
        }

        return bestId;
    }

    private void HideAllImmediate()
    {
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].wordDisplay != null)
                faces[i].wordDisplay.ForceHiddenState();
        }
    }

    private void ShowFace(string faceId, float delay, bool resetBeforeShow, Quaternion cubeRotation)
    {
        FaceWordEntry entry = FindEntry(faceId);
        if (entry == null || entry.wordDisplay == null || entry.marker == null) return;

        ApplyWordOrientation(entry, cubeRotation);

        if (resetBeforeShow)
            entry.wordDisplay.ForceHiddenState();

        entry.wordDisplay.ShowWord(delay);
    }

    private void HideFace(string faceId, float delay)
    {
        FaceWordEntry entry = FindEntry(faceId);
        if (entry != null && entry.wordDisplay != null)
            entry.wordDisplay.HideWord(delay);
    }

private void ApplyWordOrientation(FaceWordEntry entry, Quaternion cubeRotation)
{
    if (entry.wordDisplay == null || entry.marker == null || targetCamera == null || rotatingCube == null)
        return;

    // Pose base del word en mundo
    Quaternion restWorldRot = cubeRotation * entry.localRestRotation;

    // La normal real de la cara es el forward del word en su pose base,
    // NO el forward del marker (que puede apuntar al revés si el word tiene Y=180)
    Vector3 faceNormalWorld = restWorldRot * Vector3.forward;

    // Up actual del word en su pose base, proyectado sobre el plano de la cara
    Vector3 wordUpWorld = restWorldRot * Vector3.up;
    Vector3 currentUpOnFace = Vector3.ProjectOnPlane(wordUpWorld, faceNormalWorld).normalized;

    if (currentUpOnFace.sqrMagnitude < 0.001f)
    {
        entry.wordDisplay.transform.localRotation = entry.localRestRotation;
        return;
    }

    // Up deseado: el up de cámara proyectado sobre el plano de la cara,
    // snapeado al eje del cubo más cercano
    Vector3 camUpWorld = targetCamera.transform.up;
    Vector3 desiredUpOnFace = Vector3.ProjectOnPlane(camUpWorld, faceNormalWorld);

    if (desiredUpOnFace.sqrMagnitude < 0.001f)
        desiredUpOnFace = Vector3.ProjectOnPlane(Vector3.up, faceNormalWorld);

    if (desiredUpOnFace.sqrMagnitude < 0.001f)
    {
        entry.wordDisplay.transform.localRotation = entry.localRestRotation;
        return;
    }

    desiredUpOnFace.Normalize();

    Vector3 snappedUp = SnapUpToCubeAxis(desiredUpOnFace, faceNormalWorld, cubeRotation);

    // Ángulo de corrección entre up actual y up snapeado
    Vector3 toCamera = (targetCamera.transform.position - 
    (rotatingCube.position + cubeRotation * entry.marker.localPosition)).normalized;
float angle = Vector3.SignedAngle(currentUpOnFace, snappedUp, toCamera);
    float snappedAngle = Mathf.Round(angle / 90f) * 90f;

    // Aplicar corrección sobre la pose base
    Quaternion correctionWorld = Quaternion.AngleAxis(snappedAngle, faceNormalWorld);
    Quaternion finalWorldRot = correctionWorld * restWorldRot;

    entry.wordDisplay.transform.localRotation = Quaternion.Inverse(cubeRotation) * finalWorldRot;
}
    // Devuelve el eje del cubo (en mundo) que está más alineado con desiredUp
    // y que además es perpendicular a faceNormal (es decir, yace en el plano de la cara).
    private Vector3 SnapUpToCubeAxis(Vector3 desiredUp, Vector3 faceNormal, Quaternion cubeRotation)
    {
        // Los 6 ejes posibles del cubo en mundo
        Vector3[] axes =
        {
            cubeRotation * Vector3.right,
            cubeRotation * Vector3.left,
            cubeRotation * Vector3.up,
            cubeRotation * Vector3.down,
            cubeRotation * Vector3.forward,
            cubeRotation * Vector3.back,
        };

        Vector3 best = desiredUp;
        float bestDot = -2f;

        for (int i = 0; i < axes.Length; i++)
        {
            // Descartar ejes que apunten hacia/desde la cara (no son "up" válidos para ella)
            if (Mathf.Abs(Vector3.Dot(axes[i], faceNormal)) > 0.5f)
                continue;

            float dot = Vector3.Dot(axes[i], desiredUp);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = axes[i];
            }
        }

        return best;
    }

    private FaceWordEntry FindEntry(string faceId)
    {
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].faceId == faceId) return faces[i];
        }
        return null;
    }
}