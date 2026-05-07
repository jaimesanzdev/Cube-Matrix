using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelMenuTransition : MonoBehaviour
{
    [Header("Main Cube")]
    [SerializeField] private Transform menuCubeRoot;
    [SerializeField] private Transform rotatingCube;
    [SerializeField] private MonoBehaviour cubeMenuController;
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

    [Header("Spawn Animation")]
    [SerializeField] private float spawnStartYOffset = 7f;
    [SerializeField] private float spawnDuration = 0.55f;
    [SerializeField] private float spawnDelayBetweenCubes = 0.08f;
    [SerializeField] private float topRowExtraDelay = 0.18f;
    [SerializeField] private float overshootDistance = 0.35f;
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Small Rotation On Spawn")]
    [SerializeField] private bool addSpawnTilt = true;
    [SerializeField] private Vector3 spawnRotationOffset = new Vector3(12f, 18f, -8f);

    [Header("Spawn Timing")]
    [SerializeField] private float delayBeforeFirstLevelCube = 0.35f;

    private bool isTransitionRunning = false;
    private readonly List<GameObject> spawnedLevelCubes = new();
    private readonly List<VoxelWordDisplay> spawnedLevelNumberDisplays = new();

    public bool IsTransitionRunning => isTransitionRunning;

    public void StartPlayTransition()
    {
        if (isTransitionRunning)
            return;

        StartCoroutine(PlayTransitionCoroutine());
    }

    private IEnumerator PlayTransitionCoroutine()
    {
        isTransitionRunning = true;
        spawnedLevelNumberDisplays.Clear();

        DisableMainMenuInteraction();

        Coroutine exitRoutine = StartCoroutine(AnimateMainCubeExit());
        yield return StartCoroutine(SpawnLevelGridSequence());

        yield return exitRoutine;

        ShowAllLevelNumbers();

        isTransitionRunning = false;
    }

    private void DisableMainMenuInteraction()
    {
        // IMPORTANTE:
        // No desactivamos CubeMenuController porque sigue siendo quien detecta
        // los clicks sobre los cubos de nivel en modo level select.
        CubeMenuController controller = cubeMenuController as CubeMenuController;
        if (controller != null)
        {
            controller.SetLevelSelectMode(true);
            controller.SetInputLocked(false);
        }

        if (cubeMenuIdle != null)
            cubeMenuIdle.enabled = false;
    }

    private IEnumerator AnimateMainCubeExit()
    {
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

        if (levelParent == null)
            levelParent = transform;

        if (delayBeforeFirstLevelCube > 0f)
            yield return new WaitForSeconds(delayBeforeFirstLevelCube);

        List<SpawnData> spawnPlan = BuildSpawnPlan();

        foreach (SpawnData data in spawnPlan)
        {
            StartCoroutine(SpawnSingleLevelCube(data));
            yield return new WaitForSeconds(data.delayFromPrevious);
        }
    }

    private List<SpawnData> BuildSpawnPlan()
    {
        List<SpawnData> result = new();

        int clampedTotal = Mathf.Clamp(totalLevels, 1, 10);

        int bottomStart = Mathf.Min(5, clampedTotal);
        int bottomEnd = clampedTotal;

        // Fila inferior: índices 5..9 visualmente = números 5..9
        for (int i = bottomStart; i < bottomEnd; i++)
        {
            result.Add(new SpawnData
            {
                levelNumber = i + 1,
                targetPosition = GetGridPosition(i),
                delayFromPrevious = spawnDelayBetweenCubes
            });
        }

        // Pausa antes de la fila superior
        result.Add(new SpawnData
        {
            levelNumber = -1,
            delayFromPrevious = topRowExtraDelay,
            isDelayOnly = true
        });

        // Fila superior: índices 0..4 visualmente = números 0..4
        int topEnd = Mathf.Min(5, clampedTotal);
        for (int i = 0; i < topEnd; i++)
        {
            result.Add(new SpawnData
            {
                levelNumber = i + 1,
                targetPosition = GetGridPosition(i),
                delayFromPrevious = spawnDelayBetweenCubes
            });
        }

        return result;
    }

    private Vector3 GetGridPosition(int index)
    {
        int row = index / 5;     // 0 = fila superior, 1 = fila inferior
        int column = index % 5;

        float totalWidth = 4 * cellSpacingX;
        float startX = -totalWidth * 0.5f;

        float x = startX + (column * cellSpacingX);
        float y = (row == 0) ? rowSpacingY * 0.5f : -rowSpacingY * 0.5f;

        return gridCenter + new Vector3(x, y, 0f);
    }

    private IEnumerator SpawnSingleLevelCube(SpawnData data)
    {
        if (data.isDelayOnly)
            yield break;

        Vector3 finalPos = data.targetPosition;
        Vector3 overshootPos = finalPos + Vector3.down * overshootDistance;
        Vector3 startPos = finalPos + Vector3.up * spawnStartYOffset;

        GameObject cube = Instantiate(levelCubePrefab, startPos, Quaternion.identity, levelParent);
        cube.name = $"LevelCube_{data.levelNumber}";
        spawnedLevelCubes.Add(cube);

        // Botón funcional del nivel
        LevelCubeButton button = cube.GetComponent<LevelCubeButton>();
        if (button != null && levelSelectTransition != null)
        {
            // Visualmente mostramos 0..9, así que también usamos índice lógico 0..9
            button.Initialize(levelSelectTransition, data.levelNumber - 1);
        }

        // Número visual 0..9
        VoxelWordDisplay numberDisplay = cube.GetComponentInChildren<VoxelWordDisplay>(true);
        if (numberDisplay != null)
        {
            numberDisplay.SetWord((data.levelNumber - 1).ToString());
            numberDisplay.ForceHiddenState();
            spawnedLevelNumberDisplays.Add(numberDisplay);
        }

        Quaternion finalRot = Quaternion.identity;
        Quaternion startRot = addSpawnTilt
            ? Quaternion.Euler(spawnRotationOffset)
            : finalRot;

        cube.transform.position = startPos;
        cube.transform.rotation = startRot;

        float halfDuration = spawnDuration * 0.7f;
        float settleDuration = spawnDuration * 0.3f;

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
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

    private void ShowAllLevelNumbers()
    {
        for (int i = 0; i < spawnedLevelNumberDisplays.Count; i++)
        {
            if (spawnedLevelNumberDisplays[i] != null)
                spawnedLevelNumberDisplays[i].ShowWord();
        }
    }

    private struct SpawnData
    {
        public int levelNumber;
        public Vector3 targetPosition;
        public float delayFromPrevious;
        public bool isDelayOnly;
    }
}