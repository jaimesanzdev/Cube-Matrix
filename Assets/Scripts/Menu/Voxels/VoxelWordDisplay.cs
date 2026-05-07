using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelWordDisplay : MonoBehaviour
{
    [Header("Word")]
    [SerializeField] private string word = "PLAY";
    [SerializeField] private GameObject voxelPrefab;

    [Header("Layout")]
    [SerializeField] private float cellPitch = 0.115f;
    [SerializeField] private float letterSpacing = 0.06f;
    [SerializeField] private bool centerOnLocalOrigin = true;

    [Header("Voxel Visual Style")]
    [SerializeField] private float voxelScaleMultiplier = 0.90f;
    [SerializeField] private Color bodyColor = new Color(0.12f, 0.20f, 0.85f);
    [SerializeField] private Color frontFaceColor = new Color(0.20f, 0.32f, 1.0f);
    [SerializeField] private bool disableShadows = true;
    [SerializeField] private bool useSharedMaterialColor = false;

    [Header("Show Animation")]
    [SerializeField] private float spawnDuration = 0.55f;
    [SerializeField] private float spawnStagger = 0.008f;
    [SerializeField] private float spawnRadiusMin = 0.9f;
    [SerializeField] private float spawnRadiusMax = 1.6f;
    [SerializeField] private float swirlStrength = 0.30f;
    [SerializeField] private float verticalBias = 0.12f;
    [SerializeField] private AnimationCurve spawnCurve = null;

    [Header("Idle")]
    [SerializeField] private bool enableIdle = true;
    [SerializeField] private float idleAmplitude = 0.004f;
    [SerializeField] private float idleSpeed = 1.15f;

    [Header("Hide Animation")]
    [SerializeField] private float hideDuration = 0.22f;
    [SerializeField] private float hideStagger = 0.003f;
    [SerializeField] private float hideDistanceMin = 0.35f;
    [SerializeField] private float hideDistanceMax = 0.9f;
    [SerializeField] private float hideSwirl = 0.18f;
    [SerializeField] private float hideFall = 0.08f;
    [SerializeField] private AnimationCurve hideCurve = null;

    [Header("Disintegration")]
    [SerializeField] private float disintegrateDuration = 0.50f;
    [SerializeField] private float disintegrateStagger = 0.006f;
    [SerializeField] private float disintegrateDistanceMin = 1.8f;
    [SerializeField] private float disintegrateDistanceMax = 3.4f;
    [SerializeField] private float disintegrateSwirl = 0.45f;
    [SerializeField] private float disintegrateFall = 0.2f;
    [SerializeField] private AnimationCurve disintegrateCurve = null;
    [SerializeField] private bool destroyOnDisintegrate = true;

    private readonly List<VoxelPiece> pieces = new();

    private bool isBuilt = false;
    private bool isVisible = false;
    private bool isAnimatingIn = false;
    private bool isAnimatingOut = false;
    private bool isDisintegrating = false;
    private int animationToken = 0;

    private float TargetVoxelScale => cellPitch * voxelScaleMultiplier;

    private class VoxelPiece
    {
        public Transform transform;
        public Renderer[] renderers;
        public Vector3 targetLocalPosition;
        public Vector3 idleAxis;
        public float idleTimeOffset;
        public Quaternion spawnStartRotation;
    }

    private void Awake()
    {
        if (spawnCurve == null || spawnCurve.length == 0)
            spawnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (hideCurve == null || hideCurve.length == 0)
            hideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (disintegrateCurve == null || disintegrateCurve.length == 0)
            disintegrateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        BuildIfNeeded();
        ForceHiddenState();
    }

    private void Update()
    {
        if (!enableIdle || !isVisible || isAnimatingIn || isAnimatingOut || isDisintegrating)
            return;

        float time = Time.time * idleSpeed;

        for (int i = 0; i < pieces.Count; i++)
        {
            VoxelPiece piece = pieces[i];
            if (piece.transform == null)
                continue;

            float wave = Mathf.Sin(time + piece.idleTimeOffset) * idleAmplitude;
            piece.transform.localPosition = piece.targetLocalPosition + piece.idleAxis * wave;
        }
    }

    public void ShowWord(float delay = 0f)
    {
        BuildIfNeeded();
        animationToken++;
        isDisintegrating = false;
        StopAllCoroutines();
        StartCoroutine(ShowWordRoutine(delay, animationToken));
    }

    public void HideWord(float delay = 0f)
    {
        if (!isBuilt)
            return;

        animationToken++;
        isDisintegrating = false;
        StopAllCoroutines();
        StartCoroutine(HideWordRoutine(delay, animationToken));
    }

    public IEnumerator DisintegrateRoutine(float delay = 0f)
    {
        BuildIfNeeded();

        animationToken++;
        int token = animationToken;

        StopAllCoroutines();

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (token != animationToken)
            yield break;

        isDisintegrating = true;
        isAnimatingIn = false;
        isAnimatingOut = false;
        isVisible = false;

        for (int i = 0; i < pieces.Count; i++)
        {
            StartCoroutine(AnimatePieceDisintegrate(pieces[i], i * disintegrateStagger, token));
        }

        float total = disintegrateDuration + Mathf.Max(0f, pieces.Count - 1) * disintegrateStagger;
        yield return new WaitForSeconds(total);

        if (token != animationToken)
            yield break;

        isDisintegrating = false;

        if (destroyOnDisintegrate)
        {
            DestroyBuiltPieces();
            isBuilt = false;
        }
        else
        {
            ForceHiddenState();
        }
    }

    public void ForceHiddenState()
    {
        BuildIfNeeded();

        isVisible = false;
        isAnimatingIn = false;
        isAnimatingOut = false;
        isDisintegrating = false;

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

    private IEnumerator ShowWordRoutine(float delay, int token)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (token != animationToken)
            yield break;

        isAnimatingIn = true;
        isAnimatingOut = false;
        isDisintegrating = false;
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

    private IEnumerator HideWordRoutine(float delay, int token)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (token != animationToken)
            yield break;

        isAnimatingOut = true;
        isAnimatingIn = false;
        isDisintegrating = false;

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

        List<Vector3> allPositions = new();
        float cursorX = 0f;

        for (int charIndex = 0; charIndex < word.Length; charIndex++)
        {
            bool[,] pattern = VoxelLetterPatterns.GetPattern(word[charIndex]);
            int width = pattern.GetLength(0);
            int height = pattern.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!pattern[x, y])
                        continue;

                    float px = cursorX + x * cellPitch;
                    float py = y * cellPitch;
                    allPositions.Add(new Vector3(px, py, 0f));
                }
            }

            cursorX += width * cellPitch + letterSpacing;
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
            voxel.name = $"Voxel_{i}";
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
                idleAxis = (Random.insideUnitSphere * 0.35f + Vector3.up * 0.65f).normalized,
                idleTimeOffset = Random.Range(0f, Mathf.PI * 2f),
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

        SetRenderersEnabled(piece.renderers, true);

        Vector3 startPos = piece.transform.localPosition;
        Quaternion startRot = piece.transform.localRotation;
        Vector3 startScale = piece.transform.localScale;

        Vector3 radial = piece.targetLocalPosition.sqrMagnitude > 0.0001f
            ? piece.targetLocalPosition.normalized
            : Random.insideUnitSphere.normalized;

        Vector3 tangent = Vector3.Cross(Vector3.forward, radial).normalized;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.right;

        float distance = Random.Range(hideDistanceMin, hideDistanceMax);

        Vector3 endPos =
            startPos +
            radial * distance +
            tangent * Random.Range(-hideSwirl, hideSwirl) +
            Vector3.down * Random.Range(0f, hideFall);

        Quaternion endRot = Random.rotation;

        float sideRandom = Random.Range(-hideSwirl, hideSwirl);
        Vector3 controlOffset = tangent * sideRandom;

        float elapsed = 0f;

        while (elapsed < hideDuration)
        {
            if (token != animationToken || piece.transform == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hideDuration);
            float curvedT = hideCurve.Evaluate(t);

            Vector3 p0 = startPos;
            Vector3 p1 = startPos + controlOffset;
            Vector3 p2 = endPos;

            Vector3 a = Vector3.Lerp(p0, p1, curvedT);
            Vector3 b = Vector3.Lerp(p1, p2, curvedT);
            piece.transform.localPosition = Vector3.Lerp(a, b, curvedT);

            piece.transform.localRotation = Quaternion.Slerp(startRot, endRot, curvedT);
            piece.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, curvedT);

            yield return null;
        }

        piece.transform.localScale = Vector3.zero;
        SetRenderersEnabled(piece.renderers, false);
    }

    private IEnumerator AnimatePieceDisintegrate(VoxelPiece piece, float delay, int token)
    {
        yield return new WaitForSeconds(delay);

        if (token != animationToken || piece.transform == null)
            yield break;

        SetRenderersEnabled(piece.renderers, true);

        Vector3 startPos = piece.transform.localPosition;
        Quaternion startRot = piece.transform.localRotation;
        Vector3 startScale = piece.transform.localScale == Vector3.zero
            ? Vector3.one * TargetVoxelScale
            : piece.transform.localScale;

        Vector3 radial = piece.targetLocalPosition.sqrMagnitude > 0.0001f
            ? piece.targetLocalPosition.normalized
            : Random.insideUnitSphere.normalized;

        Vector3 tangent = Vector3.Cross(Vector3.forward, radial).normalized;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.right;

        float distance = Random.Range(disintegrateDistanceMin, disintegrateDistanceMax);

        Vector3 endPos =
            startPos +
            radial * distance +
            tangent * Random.Range(-disintegrateSwirl, disintegrateSwirl) +
            Vector3.down * Random.Range(0f, disintegrateFall);

        Quaternion endRot = Random.rotation;
        Vector3 controlOffset = tangent * Random.Range(-disintegrateSwirl, disintegrateSwirl);

        float elapsed = 0f;

        while (elapsed < disintegrateDuration)
        {
            if (token != animationToken || piece.transform == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / disintegrateDuration);
            float curvedT = disintegrateCurve.Evaluate(t);

            Vector3 p0 = startPos;
            Vector3 p1 = startPos + controlOffset;
            Vector3 p2 = endPos;

            Vector3 a = Vector3.Lerp(p0, p1, curvedT);
            Vector3 b = Vector3.Lerp(p1, p2, curvedT);
            piece.transform.localPosition = Vector3.Lerp(a, b, curvedT);

            piece.transform.localRotation = Quaternion.Slerp(startRot, endRot, curvedT);
            piece.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, curvedT);

            yield return null;
        }

        if (destroyOnDisintegrate)
        {
            if (piece.transform != null)
                Destroy(piece.transform.gameObject);
        }
        else
        {
            if (piece.transform != null)
            {
                piece.transform.localScale = Vector3.zero;
                SetRenderersEnabled(piece.renderers, false);
            }
        }
    }

    private void DestroyBuiltPieces()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].transform != null)
                Destroy(pieces[i].transform.gameObject);
        }

        pieces.Clear();
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

    public void SetWord(string newWord)
{
    if (string.IsNullOrWhiteSpace(newWord))
        newWord = "?";

    if (word == newWord && isBuilt)
    {
        ForceHiddenState();
        return;
    }

    word = newWord;
    RebuildWord();
}

    private void RebuildWord()
    {
        StopAllCoroutines();
        animationToken++;

        DestroyBuiltPieces();

        isBuilt = false;
        isVisible = false;
        isAnimatingIn = false;
        isAnimatingOut = false;
        isDisintegrating = false;

        BuildIfNeeded();
        ForceHiddenState();
    }

}