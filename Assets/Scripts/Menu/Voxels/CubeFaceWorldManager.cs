using UnityEngine;

public class CubeFaceWordManager : MonoBehaviour
{
    [System.Serializable]
    public class FaceWordEntry
    {
        public string faceId;
        public Transform marker;
        public VoxelWordDisplay wordDisplay;

        [HideInInspector] public Quaternion baseLocalRotation;
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

        CacheBaseRotations();
    }

    private void Start()
    {
        HideAllImmediate();

        string initialFace = GetFrontFaceId(rotatingCube.rotation);
        currentFaceId = initialFace;
        ShowFace(initialFace, initialFrontFaceDelay, true);
    }

    public void PreviewFaceAfterRotationStep(Vector3 axis, float angle)
    {
        if (rotatingCube == null || targetCamera == null)
            return;

        Quaternion simulatedRotation = Quaternion.AngleAxis(angle, axis) * rotatingCube.rotation;
        string nextFace = GetFrontFaceId(simulatedRotation);

        if (nextFace == currentFaceId)
            return;

        HideFace(currentFaceId, hideDelayOnFaceLeave);
        ShowFace(nextFace, showDelayOnFaceSwitch, true, simulatedRotation);

        currentFaceId = nextFace;
    }

    public void RefreshCurrentFaceAfterRotation()
    {
        if (rotatingCube == null || targetCamera == null)
            return;

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
            {
                ApplyBestWordOrientation(entry, rotatingCube.rotation);
            }
        }
    }

    private void CacheBaseRotations()
    {
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].wordDisplay != null)
            {
                faces[i].baseLocalRotation = faces[i].wordDisplay.transform.localRotation;
            }
        }
    }

    private string GetFrontFaceId(Quaternion cubeRotation)
    {
        float bestDot = -999f;
        string bestId = "";

        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].marker == null)
                continue;

            Vector3 markerWorldPos = rotatingCube.position + (cubeRotation * faces[i].marker.localPosition);
            Quaternion markerWorldRot = cubeRotation * faces[i].marker.localRotation;
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

    private void ShowFace(string faceId, float delay, bool resetBeforeShow, Quaternion? cubeRotationOverride = null)
    {
        FaceWordEntry entry = FindEntry(faceId);
        if (entry == null || entry.wordDisplay == null || entry.marker == null)
            return;

        Quaternion cubeRotation = cubeRotationOverride ?? rotatingCube.rotation;

        ApplyBestWordOrientation(entry, cubeRotation);

        if (resetBeforeShow)
        {
            // Esta es la clave para tu caso de volver rápido:
            // si la palabra estaba medio desapareciendo, la ocultamos del todo de golpe
            // y la regeneramos limpia.
            entry.wordDisplay.ForceHiddenState();
        }

        entry.wordDisplay.ShowWord(delay);
    }

    private void HideFace(string faceId, float delay)
    {
        FaceWordEntry entry = FindEntry(faceId);
        if (entry != null && entry.wordDisplay != null)
        {
            entry.wordDisplay.HideWord(delay);
        }
    }

    private void ApplyBestWordOrientation(FaceWordEntry entry, Quaternion cubeRotation)
    {
        if (entry.wordDisplay == null || entry.marker == null || targetCamera == null)
            return;

        Transform wordTransform = entry.wordDisplay.transform;

        Quaternion baseLocalRotation = entry.baseLocalRotation;
        Vector3 faceNormalWorld = cubeRotation * (entry.marker.localRotation * Vector3.forward);

        Vector3 desiredUpWorld = Vector3.ProjectOnPlane(targetCamera.transform.up, faceNormalWorld);
        if (desiredUpWorld.sqrMagnitude < 0.0001f)
        {
            desiredUpWorld = Vector3.ProjectOnPlane(Vector3.up, faceNormalWorld);
        }

        desiredUpWorld.Normalize();

        float bestDot = -999f;
        Quaternion bestLocalRotation = baseLocalRotation;

        float[] candidateAngles = { 0f, 90f, 180f, 270f };

        for (int i = 0; i < candidateAngles.Length; i++)
        {
            Quaternion candidateLocal =
                baseLocalRotation *
                Quaternion.AngleAxis(candidateAngles[i], Vector3.forward);

            Vector3 candidateUpWorld = cubeRotation * (candidateLocal * Vector3.up);
            float dot = Vector3.Dot(candidateUpWorld, desiredUpWorld);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestLocalRotation = candidateLocal;
            }
        }

        wordTransform.localRotation = bestLocalRotation;
    }

    private FaceWordEntry FindEntry(string faceId)
    {
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].faceId == faceId)
                return faces[i];
        }

        return null;
    }
}