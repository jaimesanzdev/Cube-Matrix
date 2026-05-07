using UnityEngine;

public class LevelCubeButton : MonoBehaviour
{
    [SerializeField] private int levelIndex = 0;
    private LevelSelectTransition transition;

    public void Initialize(LevelSelectTransition transitionController, int index)
    {
        transition = transitionController;
        levelIndex = index;
        Debug.Log($"Initialize LevelCubeButton -> index {levelIndex}, transition null? {transition == null}");
    }

    public void OnPressed()
    {
        Debug.Log($"Level cube pressed -> index {levelIndex}, transition null? {transition == null}");

        if (transition != null)
        {
            transition.SelectLevelCube(this, levelIndex);
        }
        else
        {
            Debug.LogWarning("LevelCubeButton: transition es null");
        }
    }
}