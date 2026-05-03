using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelMenuTransition : MonoBehaviour
{
    [Header("Main Cube")]
    [SerializeField] private Transform menuCubeRoot;
    [SerializeField] private Transform rotatingCube;
    [SerializeField] private CubeMenuController cubeMenuController;
    [SerializeField] private MonoBehaviour cubeMenuIdle;

    [Header("Level Select")]
    [SerializeField] private LevelSelectTransition levelSelectTransition;

    [Header("Main Cube Exit Animation")]
    [SerializeField] private float exitDuration = 1.1f;
    [SerializeField] private float exitDropDistance = 8f;
    [SerializeField] private float exitSpinSpeed = 900f;
    [SerializeField] private AnimationCurve exitYCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exitSpinCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.6f);

    [Header("Level Cubes")]
    [SerializeField] private GameObject levelCubePrefab;
    [SerializeField] private Transform levelParent;
    [SerializeField] private int totalLevels = 10;

    [Header("Grid Layout")]
    [SerializeField] private float cellSpacingX = 2.2f;
    [SerializeField] private float rowSpacingY = 2.2f;
    [SerializeField] private Vector3 gridCenter = new Vector3(0f, 0f, 0f);

    [Header("Spawn Timing")]
    [SerializeField] private float delayBeforeFirstLevelCube = 0.35f;
    [SerializeField] private float spawnDelayBetweenCubes = 0.08f;
    [SerializeField] private float topRowExtraDelay = 0.18f;

    [Header("Spawn Animation")]
    [SerializeField] private float spawnStartYOffset = 7f;
    [SerializeField] private float spawnDuration = 0.55f;
    [SerializeField] private float overshootDistance = 0.35f;
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Small Rotation On Spawn")]
    [SerializeField] private bool addSpawnTilt = true;
    [SerializeField] private Vector3 spawnRotationOffset = new Vector3(12f, 18f, -8f);

    private bool isTransitionRunning = false;
    private readonly List<GameObject> spawnedLevelCubes = new();

    public bool IsTransitionRunning => isTransitionRunning;
    public IReadOnlyList<GameObject> SpawnedLevelCubes => spawnedLevelCubes;

    public void StartPlayTransition()
    {
        if (isTransitionRunning)
            return;

        StartCoroutine(PlayTransitionCoroutine());
    }

    private IEnumerator PlayTransitionCoroutine()
    {
        isTransitionRunning = true;

        DisableMainMenuInteraction();
        EnsureValidLevelParent();

        Coroutine exitRoutine = StartCoroutine(AnimateMainCubeExit());
        yield return StartCoroutine(SpawnLevelGridSequence());
        yield return exitRoutine;

        if (cubeMenuController != null)
            cubeMenuController.SetLevelSelectMode(true);

        isTransitionRunning = false;
    }

    private void DisableMainMenuInteraction()
    {
        // OJO: no desactivamos cubeMenuController,
        // porque lo necesitamos activo para detectar el clic en los cubos de nivel.
        if (cubeMenuIdle != null)
            cubeMenuIdle.enabled = false;
    }

    private void EnsureValidLevelParent()
    {
        if (levelParent == null || !levelParent.gameObject.scene.IsValid())
        {
            levelParent = this.transform;
        }
    }

    private IEnumerator AnimateMainCubeExit()
    {
        if (menuCubeRoot == null || rotatingCube == null)
            yield break;

        Vector3 startPos = menuCubeRoot.position;
        Vector3 endPos = startPos + Vector3.down * exitDropDistance;

        float elapsed = 0f;

        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / exitDuration);

            float yT = exitYCurve.Evaluate(t);
            float spinT = exitSpinCurve.Evaluate(t);

            menuCubeRoot.position = Vector3.Lerp(startPos, endPos, yT);

            float frameSpin = exitSpinSpeed * spinT * Time.deltaTime;
            rotatingCube.Rotate(Vector3.up, frameSpin, Space.World);
            rotatingCube.Rotate(Vector3.right, frameSpin * 0.55f, Space.World);

            yield return null;
        }

        menuCubeRoot.position = endPos;
    }

    private IEnumerator SpawnLevelGridSequence()
    {
        if (levelCubePrefab == null)
        {
            Debug.LogWarning("LevelMenuTransition: falta asignar levelCubePrefab.");
            yield break;
        }

        EnsureValidLevelParent();

        if (delayBeforeFirstLevelCube > 0f)
            yield return new WaitForSeconds(delayBeforeFirstLevelCube);

        List<SpawnData> spawnPlan = BuildSpawnPlan();

        foreach (SpawnData data in spawnPlan)
        {
            if (data.isDelayOnly)
            {
                yield return new WaitForSeconds(data.delayFromPrevious);
                continue;
            }

            StartCoroutine(SpawnSingleLevelCube(data));
            yield return new WaitForSeconds(data.delayFromPrevious);
        }
    }

    private List<SpawnData> BuildSpawnPlan()
    {
        List<SpawnData> result = new();

        int bottomRowStart = Mathf.Min(5, totalLevels);
        int bottomRowEnd = Mathf.Min(10, totalLevels);

        // Fila inferior: 6 7 8 9 10
        for (int i = bottomRowStart; i < bottomRowEnd; i++)
        {
            result.Add(new SpawnData
            {
                levelNumber = i + 1,
                targetPosition = GetGridPosition(i),
                delayFromPrevious = spawnDelayBetweenCubes,
                isDelayOnly = false
            });
        }

        if (bottomRowStart < totalLevels && topRowExtraDelay > 0f)
        {
            result.Add(new SpawnData
            {
                levelNumber = -1,
                targetPosition = Vector3.zero,
                delayFromPrevious = topRowExtraDelay,
                isDelayOnly = true
            });
        }

        int topRowEnd = Mathf.Min(5, totalLevels);

        // Fila superior: 1 2 3 4 5
        for (int i = 0; i < topRowEnd; i++)
        {
            result.Add(new SpawnData
            {
                levelNumber = i + 1,
                targetPosition = GetGridPosition(i),
                delayFromPrevious = spawnDelayBetweenCubes,
                isDelayOnly = false
            });
        }

        return result;
    }

    private Vector3 GetGridPosition(int index)
    {
        int row = index / 5;
        int column = index % 5;

        float totalWidth = 4f * cellSpacingX;
        float startX = -totalWidth * 0.5f;

        float x = startX + (column * cellSpacingX);
        float y = (row == 0) ? rowSpacingY * 0.5f : -rowSpacingY * 0.5f;

        return gridCenter + new Vector3(x, y, 0f);
    }

    private IEnumerator SpawnSingleLevelCube(SpawnData data)
    {
        Vector3 finalPos = data.targetPosition;
        Vector3 overshootPos = finalPos + Vector3.down * overshootDistance;
        Vector3 startPos = finalPos + Vector3.up * spawnStartYOffset;

        GameObject cube = Instantiate(levelCubePrefab, startPos, Quaternion.identity, levelParent);
        cube.name = $"LevelCube_{data.levelNumber}";
        spawnedLevelCubes.Add(cube);

        Quaternion finalRot = Quaternion.identity;
        Quaternion startRot = addSpawnTilt
            ? Quaternion.Euler(spawnRotationOffset)
            : finalRot;

        cube.transform.position = startPos;
        cube.transform.rotation = startRot;

        LevelCubeButton button = cube.GetComponent<LevelCubeButton>();
        if (button != null)
        {
            button.Initialize(levelSelectTransition, data.levelNumber);
        }
        else
        {
            Debug.LogWarning($"LevelMenuTransition: {cube.name} no tiene LevelCubeButton.");
        }

        float fallDuration = spawnDuration * 0.7f;
        float settleDuration = spawnDuration * 0.3f;

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float curveT = fallCurve.Evaluate(t);

            cube.transform.position = Vector3.Lerp(startPos, overshootPos, curveT);
            cube.transform.rotation = Quaternion.Slerp(startRot, finalRot, curveT);

            yield return null;
        }

        cube.transform.position = overshootPos;
        cube.transform.rotation = finalRot;

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float curveT = settleCurve.Evaluate(t);

            cube.transform.position = Vector3.Lerp(overshootPos, finalPos, curveT);
            yield return null;
        }

        cube.transform.position = finalPos;

        FloatingIdleRandom idle = cube.GetComponent<FloatingIdleRandom>();
        if (idle != null)
            idle.InitializeRandomized();
    }

    private struct SpawnData
    {
        public int levelNumber;
        public Vector3 targetPosition;
        public float delayFromPrevious;
        public bool isDelayOnly;
    }
}