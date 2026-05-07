using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuFlowController : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private CubeMenuController cubeMenuController;
    [SerializeField] private Transform mainCubeRoot;

    [Header("Secondary Menu Cubes")]
    [SerializeField] private Transform optionsCubeRoot;
    [SerializeField] private Transform creditsCubeRoot;

    [Header("Quit Confirm")]
    [SerializeField] private Transform quitConfirmRoot;

    [Header("Back Icons")]
    [SerializeField] private VoxelIconDisplay optionsBackIcon;
    [SerializeField] private VoxelIconDisplay creditsBackIcon;

    [Header("Quit Icons")]
    [SerializeField] private VoxelIconDisplay quitYesIcon;
    [SerializeField] private VoxelIconDisplay quitNoIcon;

    [Header("Quit Text")]
    [SerializeField] private VoxelWordDisplay quitSureDisplay;

    [Header("Animated Block Objects")]
    [SerializeField] private List<Transform> optionsAnimatedBlocks = new();
    [SerializeField] private List<Transform> creditsAnimatedBlocks = new();
    [SerializeField] private List<Transform> quitAnimatedBlocks = new();

    [Header("Animation")]
    [SerializeField] private float transitionDuration = 0.55f;
    [SerializeField] private float hiddenYOffset = 6f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Block Scale Animation")]
    [SerializeField] private float blockScaleDuration = 0.22f;
    [SerializeField] private AnimationCurve blockScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.75f, 1.08f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] private float delayBeforeVoxelDisplays = 0.03f;

    private enum MenuState
    {
        Main,
        Options,
        Credits,
        QuitConfirm,
        Busy
    }

    private MenuState currentState = MenuState.Main;

    private Vector3 mainCubeShownPos;

    private Vector3 optionsCubeHiddenPos;
    private Vector3 optionsCubeShownPos;

    private Vector3 creditsCubeHiddenPos;
    private Vector3 creditsCubeShownPos;

    private Vector3 quitConfirmHiddenPos;
    private Vector3 quitConfirmShownPos;

    private readonly Dictionary<Transform, Vector3> originalBlockScales = new();

    private void Awake()
    {
        if (mainCubeRoot != null)
            mainCubeShownPos = mainCubeRoot.position;

        if (optionsCubeRoot != null)
        {
            optionsCubeHiddenPos = optionsCubeRoot.position;
            optionsCubeShownPos = optionsCubeHiddenPos + Vector3.down * hiddenYOffset;
        }

        if (creditsCubeRoot != null)
        {
            creditsCubeHiddenPos = creditsCubeRoot.position;
            creditsCubeShownPos = creditsCubeHiddenPos + Vector3.down * hiddenYOffset;
        }

        if (quitConfirmRoot != null)
        {
            quitConfirmHiddenPos = quitConfirmRoot.position;
            quitConfirmShownPos = quitConfirmHiddenPos + Vector3.down * hiddenYOffset;
        }

        CacheOriginalScales(optionsAnimatedBlocks);
        CacheOriginalScales(creditsAnimatedBlocks);
        CacheOriginalScales(quitAnimatedBlocks);

        if (optionsBackIcon != null)
            optionsBackIcon.ForceHiddenState();

        if (creditsBackIcon != null)
            creditsBackIcon.ForceHiddenState();

        if (quitYesIcon != null)
            quitYesIcon.ForceHiddenState();

        if (quitNoIcon != null)
            quitNoIcon.ForceHiddenState();

        if (quitSureDisplay != null)
            quitSureDisplay.ForceHiddenState();
    }

    public void OnMainFaceSelected(CubeMenuController.MainFace face)
    {
        if (currentState == MenuState.Busy)
            return;

        switch (face)
        {
            case CubeMenuController.MainFace.Options:
                StartCoroutine(OpenOptionsSequence());
                break;

            case CubeMenuController.MainFace.Credits:
                StartCoroutine(OpenCreditsSequence());
                break;

            case CubeMenuController.MainFace.Quit:
                StartCoroutine(OpenQuitConfirmSequence());
                break;
        }
    }

    public void ReturnFromOptionsOrCredits()
    {
        if (currentState != MenuState.Options && currentState != MenuState.Credits)
            return;

        StartCoroutine(ReturnToMainSequence());
    }

    public void CloseQuitConfirm()
    {
        if (currentState != MenuState.QuitConfirm)
            return;

        StartCoroutine(CloseQuitConfirmSequence());
    }

    public void ConfirmQuit()
    {
        if (quitSureDisplay != null)
            quitSureDisplay.ForceHiddenState();

        if (quitYesIcon != null)
            quitYesIcon.ForceHiddenState();

        if (quitNoIcon != null)
            quitNoIcon.ForceHiddenState();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator OpenOptionsSequence()
    {
        currentState = MenuState.Busy;
        LockInput(true);

        if (optionsBackIcon != null)
            optionsBackIcon.ForceHiddenState();

        ResetBlocksToZero(optionsAnimatedBlocks);

        yield return StartCoroutine(AnimateOutDown(mainCubeRoot, mainCubeShownPos));
        yield return StartCoroutine(AnimateFromCurrentTo(optionsCubeRoot, optionsCubeShownPos));
        yield return StartCoroutine(AnimateBlocksScaleIn(optionsAnimatedBlocks));

        if (delayBeforeVoxelDisplays > 0f)
            yield return new WaitForSeconds(delayBeforeVoxelDisplays);

        if (optionsBackIcon != null)
            optionsBackIcon.ShowIcon();

        currentState = MenuState.Options;
        LockInput(false);
    }

    private IEnumerator OpenCreditsSequence()
    {
        currentState = MenuState.Busy;
        LockInput(true);

        if (creditsBackIcon != null)
            creditsBackIcon.ForceHiddenState();

        ResetBlocksToZero(creditsAnimatedBlocks);

        yield return StartCoroutine(AnimateOutDown(mainCubeRoot, mainCubeShownPos));
        yield return StartCoroutine(AnimateFromCurrentTo(creditsCubeRoot, creditsCubeShownPos));
        yield return StartCoroutine(AnimateBlocksScaleIn(creditsAnimatedBlocks));

        if (delayBeforeVoxelDisplays > 0f)
            yield return new WaitForSeconds(delayBeforeVoxelDisplays);

        if (creditsBackIcon != null)
            creditsBackIcon.ShowIcon();

        currentState = MenuState.Credits;
        LockInput(false);
    }

    private IEnumerator OpenQuitConfirmSequence()
    {
        currentState = MenuState.Busy;
        LockInput(true);

        if (quitYesIcon != null)
            quitYesIcon.ForceHiddenState();

        if (quitNoIcon != null)
            quitNoIcon.ForceHiddenState();

        if (quitSureDisplay != null)
            quitSureDisplay.ForceHiddenState();

        ResetBlocksToZero(quitAnimatedBlocks);

        yield return StartCoroutine(AnimateOutDown(mainCubeRoot, mainCubeShownPos));
        yield return StartCoroutine(AnimateFromCurrentTo(quitConfirmRoot, quitConfirmShownPos));
        yield return StartCoroutine(AnimateBlocksScaleIn(quitAnimatedBlocks));

        if (delayBeforeVoxelDisplays > 0f)
            yield return new WaitForSeconds(delayBeforeVoxelDisplays);

        if (quitSureDisplay != null)
            quitSureDisplay.ShowWord();

        if (quitYesIcon != null)
            quitYesIcon.ShowIcon();

        if (quitNoIcon != null)
            quitNoIcon.ShowIcon();

        currentState = MenuState.QuitConfirm;
        LockInput(false);
    }

    private IEnumerator CloseQuitConfirmSequence()
    {
        currentState = MenuState.Busy;
        LockInput(true);

        yield return StartCoroutine(AnimateFromCurrentTo(quitConfirmRoot, quitConfirmHiddenPos));

        if (quitSureDisplay != null)
            quitSureDisplay.ForceHiddenState();

        if (quitYesIcon != null)
            quitYesIcon.ForceHiddenState();

        if (quitNoIcon != null)
            quitNoIcon.ForceHiddenState();

        yield return StartCoroutine(AnimateFromCurrentTo(mainCubeRoot, mainCubeShownPos));

        currentState = MenuState.Main;
        LockInput(false);
    }

    private IEnumerator ReturnToMainSequence()
    {
        currentState = MenuState.Busy;
        LockInput(true);

        bool returningFromOptions = optionsCubeRoot != null && IsNear(optionsCubeRoot.position, optionsCubeShownPos);
        bool returningFromCredits = creditsCubeRoot != null && IsNear(creditsCubeRoot.position, creditsCubeShownPos);

        if (returningFromOptions)
        {
            yield return StartCoroutine(AnimateFromCurrentTo(optionsCubeRoot, optionsCubeHiddenPos));

            if (optionsBackIcon != null)
                optionsBackIcon.ForceHiddenState();
        }

        if (returningFromCredits)
        {
            yield return StartCoroutine(AnimateFromCurrentTo(creditsCubeRoot, creditsCubeHiddenPos));

            if (creditsBackIcon != null)
                creditsBackIcon.ForceHiddenState();
        }

        yield return StartCoroutine(AnimateFromCurrentTo(mainCubeRoot, mainCubeShownPos));

        currentState = MenuState.Main;
        LockInput(false);
    }

    private IEnumerator AnimateFromCurrentTo(Transform target, Vector3 end)
    {
        if (target == null)
            yield break;

        Vector3 start = target.position;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float curved = transitionCurve.Evaluate(t);
            target.position = Vector3.Lerp(start, end, curved);
            yield return null;
        }

        target.position = end;
    }

    private IEnumerator AnimateOutDown(Transform target, Vector3 shownPosition)
    {
        if (target == null)
            yield break;

        Vector3 start = shownPosition;
        Vector3 end = shownPosition + Vector3.down * hiddenYOffset;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float curved = transitionCurve.Evaluate(t);
            target.position = Vector3.Lerp(start, end, curved);
            yield return null;
        }

        target.position = end;
    }

    private void CacheOriginalScales(List<Transform> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            Transform block = blocks[i];
            if (block == null)
                continue;

            if (!originalBlockScales.ContainsKey(block))
                originalBlockScales.Add(block, block.localScale);
        }
    }

    private void ResetBlocksToZero(List<Transform> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            Transform block = blocks[i];
            if (block == null)
                continue;

            if (!originalBlockScales.ContainsKey(block))
                originalBlockScales.Add(block, block.localScale);

            block.localScale = Vector3.zero;
        }
    }

    private IEnumerator AnimateBlocksScaleIn(List<Transform> blocks)
    {
        float elapsed = 0f;

        while (elapsed < blockScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / blockScaleDuration);
            float curved = blockScaleCurve.Evaluate(t);

            for (int i = 0; i < blocks.Count; i++)
            {
                Transform block = blocks[i];
                if (block == null)
                    continue;

                if (!originalBlockScales.TryGetValue(block, out Vector3 originalScale))
                    continue;

                block.localScale = Vector3.LerpUnclamped(Vector3.zero, originalScale, curved);
            }

            yield return null;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            Transform block = blocks[i];
            if (block == null)
                continue;

            if (!originalBlockScales.TryGetValue(block, out Vector3 originalScale))
                continue;

            block.localScale = originalScale;
        }
    }

    private void LockInput(bool value)
    {
        if (cubeMenuController != null)
            cubeMenuController.SetInputLocked(value);
    }

    private bool IsNear(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) < 0.05f;
    }
}