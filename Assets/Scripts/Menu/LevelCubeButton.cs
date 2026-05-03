using UnityEngine;

public class LevelCubeButton : MonoBehaviour
{
    [SerializeField] private int levelIndex = 0;
    private LevelSelectTransition transition;

    public void Initialize(LevelSelectTransition transitionController, int index)
    {
        transition = transitionController;
        levelIndex = index;
    }

    public void OnPressed()
    {
        Debug.Log($"Level cube pressed: {levelIndex}");

        if (transition != null)
        {
            transition.SelectLevelCube(this, levelIndex);
        }
    }
}