using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelIconDisplay : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private VoxelIconPatterns.IconType iconType = VoxelIconPatterns.IconType.BackArrow;
    [SerializeField] private GameObject voxelPrefab;

    [Header("Layout")]
    [SerializeField] private float cellPitch = 0.11f;
    [SerializeField] private bool centerOnLocalOrigin = true;

    [Header("Voxel Visual Style")]
    [SerializeField] private float voxelScaleMultiplier = 0.90f;
    [SerializeField] private Color bodyColor = Color.black;
    [SerializeField] private Color frontFaceColor = Color.black;
    [SerializeField] private bool disableShadows = true;
    [SerializeField] private bool useSharedMaterialColor = false;

    [Header("Show Animation")]
    [SerializeField] private float spawnDuration = 0.45f;
    [SerializeField] private float spawnStagger = 0.006f;
    [SerializeField] private float spawnRadiusMin = 0.35f;
    [SerializeField] private float spawnRadiusMax = 0.8f;
    [SerializeField] private float swirlStrength = 0.18f;
    [SerializeField] private float verticalBias = 0.08f;
    [SerializeField] private AnimationCurve spawnCurve = null;

    [Header("Hide Animation")]
    [SerializeField] private float hideDuration = 0.18f;
    [SerializeField] private float hideStagger = 0.002f;
    [SerializeField] private AnimationCurve hideCurve = null;

    private readonly List<VoxelPiece> pieces = new();

    private bool isBuilt = false;
    private bool isVisible = false;
    private bool isAnimatingIn = false;
    private bool isAnimatingOut = false;
    private int animationToken = 0;

    private float TargetVoxelScale => cellPitch * voxelScaleMultiplier;

    private class VoxelPiece
    {
        public Transform transform;
        public Renderer[] renderers;
        public Vector3 targetLocalPosition;
        public Quaternion spawnStartRotation;
    }

    private void Awake()
    {
        if (spawnCurve == null || spawnCurve.length == 0)
            spawnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (hideCurve == null || hideCurve.length == 0)
            hideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        BuildIfNeeded();
        ForceHiddenState();
    }

    public void ShowIcon(float delay = 0f)
    {
        BuildIfNeeded();

        if (isVisible || isAnimatingIn)
            return;

        animationToken++;
        StopAllCoroutines();
        StartCoroutine(ShowIconRoutine(delay, animationToken));
    }

    public void HideIcon(float delay = 0f)
    {
        if (!isBuilt)
            return;

        if ((!isVisible && !isAnimatingIn) || isAnimatingOut)
            return;

        animationToken++;
        StopAllCoroutines();
        StartCoroutine(HideIconRoutine(delay, animationToken));
    }

    public void ForceHiddenState()
    {
        BuildIfNeeded();

        isVisible = false;
        isAnimatingIn = false;
        isAnimatingOut = false;

        for (int i = 0; i < pieces.Count; i++)
        {
            VoxelPiece piece = pieces[i];
            if (piece.transform == null)
                continue;

            piece.transform.localPosition = piece.targetLocalPosition;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.zero;
            SetRenderersEnabled(piece.renderers, false);
        }
    }

    private IEnumerator ShowIconRoutine(float delay, int token)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (token != animationToken)
            yield break;

        isAnimatingIn = true;
        isAnimatingOut = false;
        isVisible = false;

        for (int i = 0; i < pieces.Count; i++)
        {
            StartCoroutine(AnimatePieceShow(pieces[i], i * spawnStagger, token));
        }

        float total = spawnDuration + Mathf.Max(0f, pieces.Count - 1) * spawnStagger;
        yield return new WaitForSeconds(total);

        if (token != animationToken)
            yield break;

        isAnimatingIn = false;
        isVisible = true;
    }

    private IEnumerator HideIconRoutine(float delay, int token)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (token != animationToken)
            yield break;

        isAnimatingOut = true;
        isAnimatingIn = false;

        for (int i = 0; i < pieces.Count; i++)
        {
            StartCoroutine(AnimatePieceHide(pieces[i], i * hideStagger, token));
        }

        float total = hideDuration + Mathf.Max(0f, pieces.Count - 1) * hideStagger;
        yield return new WaitForSeconds(total);

        if (token != animationToken)
            yield break;

        isAnimatingOut = false;
        isVisible = false;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].transform != null)
                SetRenderersEnabled(pieces[i].renderers, false);
        }
    }

    private void BuildIfNeeded()
    {
        if (isBuilt)
            return;

        bool[,] pattern = VoxelIconPatterns.GetPattern(iconType);
        List<Vector3> allPositions = new();

        int width = pattern.GetLength(0);
        int height = pattern.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!pattern[x, y])
                    continue;

                float px = x * cellPitch;
                float py = y * cellPitch;
                allPositions.Add(new Vector3(px, py, 0f));
            }
        }

        Vector3 centerOffset = Vector3.zero;

        if (centerOnLocalOrigin && allPositions.Count > 0)
        {
            Vector3 min = allPositions[0];
            Vector3 max = allPositions[0];

            for (int i = 1; i < allPositions.Count; i++)
            {
                min = Vector3.Min(min, allPositions[i]);
                max = Vector3.Max(max, allPositions[i]);
            }

            centerOffset = (min + max) * 0.5f;
        }

        for (int i = 0; i < allPositions.Count; i++)
        {
            Vector3 targetLocalPos = allPositions[i] - centerOffset;

            GameObject voxel = Instantiate(voxelPrefab, transform);
            voxel.name = $"IconVoxel_{i}";
            voxel.transform.localPosition = targetLocalPos;
            voxel.transform.localRotation = Quaternion.identity;
            voxel.transform.localScale = Vector3.zero;

            ApplyVoxelVisualStyle(voxel);

            Renderer[] renderers = voxel.GetComponentsInChildren<Renderer>(true);
            SetRenderersEnabled(renderers, false);

            VoxelPiece piece = new VoxelPiece
            {
                transform = voxel.transform,
                renderers = renderers,
                targetLocalPosition = targetLocalPos,
                spawnStartRotation = Random.rotation
            };

            pieces.Add(piece);
        }

        isBuilt = true;
    }

    private void ApplyVoxelVisualStyle(GameObject voxel)
    {
        ApplyColorToRenderers(voxel.transform, bodyColor);

        if (voxel.transform.childCount > 0)
        {
            Transform frontFace = voxel.transform.GetChild(0);
            ApplyColorToRenderers(frontFace, frontFaceColor);
        }
    }

    private void ApplyColorToRenderers(Transform target, Color color)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];

            if (disableShadows)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            if (useSharedMaterialColor)
            {
                if (r.sharedMaterial != null)
                    r.sharedMaterial.color = color;
            }
            else
            {
                if (r.material != null)
                    r.material.color = color;
            }
        }
    }

    private IEnumerator AnimatePieceShow(VoxelPiece piece, float delay, int token)
    {
        yield return new WaitForSeconds(delay);

        if (token != animationToken || piece.transform == null)
            yield break;

        SetRenderersEnabled(piece.renderers, true);

        Vector3 radial = piece.targetLocalPosition.sqrMagnitude > 0.0001f
            ? piece.targetLocalPosition.normalized
            : Random.insideUnitSphere.normalized;

        Vector3 tangent = Vector3.Cross(Vector3.forward, radial).normalized;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.right;

        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);

        Vector3 startPos =
            piece.targetLocalPosition +
            radial * radius +
            tangent * Random.Range(-swirlStrength, swirlStrength) +
            Vector3.up * Random.Range(-verticalBias, verticalBias);

        Vector3 control =
            tangent * Random.Range(-swirlStrength, swirlStrength) +
            Vector3.up * Random.Range(-verticalBias, verticalBias);

        piece.transform.localPosition = startPos;
        piece.transform.localRotation = piece.spawnStartRotation;
        piece.transform.localScale = Vector3.zero;

        Vector3 finalScale = Vector3.one * TargetVoxelScale;

        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            if (token != animationToken || piece.transform == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);
            float curvedT = spawnCurve.Evaluate(t);

            Vector3 p0 = startPos;
            Vector3 p1 = startPos + control;
            Vector3 p2 = piece.targetLocalPosition;

            Vector3 a = Vector3.Lerp(p0, p1, curvedT);
            Vector3 b = Vector3.Lerp(p1, p2, curvedT);
            piece.transform.localPosition = Vector3.Lerp(a, b, curvedT);

            piece.transform.localRotation = Quaternion.Slerp(piece.spawnStartRotation, Quaternion.identity, curvedT);
            piece.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, Mathf.SmoothStep(0f, 1f, curvedT));

            yield return null;
        }

        piece.transform.localPosition = piece.targetLocalPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = finalScale;
    }

    private IEnumerator AnimatePieceHide(VoxelPiece piece, float delay, int token)
    {
        yield return new WaitForSeconds(delay);

        if (token != animationToken || piece.transform == null)
            yield break;

        Vector3 startScale = piece.transform.localScale;

        float elapsed = 0f;

        while (elapsed < hideDuration)
        {
            if (token != animationToken || piece.transform == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hideDuration);
            float curvedT = hideCurve.Evaluate(t);

            piece.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, curvedT);
            yield return null;
        }

        piece.transform.localScale = Vector3.zero;
        SetRenderersEnabled(piece.renderers, false);
    }

    private void SetRenderersEnabled(Renderer[] renderers, bool value)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = value;
        }
    }
}