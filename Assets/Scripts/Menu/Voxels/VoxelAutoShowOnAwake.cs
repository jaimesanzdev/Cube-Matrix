using UnityEngine;

public class VoxelAutoShowOnAwake : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private VoxelWordDisplay wordDisplay;
    [SerializeField] private VoxelIconDisplay iconDisplay;

    [Header("Timing")]
    [SerializeField] private float delay = 0f;

    private void Awake()
    {
        if (wordDisplay == null)
            wordDisplay = GetComponentInChildren<VoxelWordDisplay>(true);

        if (iconDisplay == null)
            iconDisplay = GetComponentInChildren<VoxelIconDisplay>(true);

        if (wordDisplay != null)
        {
            wordDisplay.ForceHiddenState();
            wordDisplay.ShowWord(delay);
        }

        if (iconDisplay != null)
        {
            iconDisplay.ForceHiddenState();
            iconDisplay.ShowIcon(delay);
        }
    }
}