using System.Collections.Generic;
using UnityEngine;

public class VoxelTitleLayout : MonoBehaviour
{
    public enum LayoutMode
    {
        SingleLine,
        TwoLines
    }

    [Header("Layout")]
    [SerializeField] private LayoutMode layoutMode = LayoutMode.TwoLines;
    [SerializeField] private float letterSpacing = 1.15f;
    [SerializeField] private float wordSpacing = 1.8f;
    [SerializeField] private float lineSpacing = 1.6f;

    [Header("Two-Line Split")]
    [SerializeField] private int firstLineLetterCount = 4; // CUBE | MATRIX

    [Header("Depth")]
    [SerializeField] private float localZ = 0f;

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        List<Transform> letters = GetOrderedChildLetters();
        if (letters.Count == 0)
            return;

        switch (layoutMode)
        {
            case LayoutMode.SingleLine:
                ApplySingleLineLayout(letters);
                break;

            case LayoutMode.TwoLines:
                ApplyTwoLineLayout(letters);
                break;
        }
    }

    private void Awake()
    {
        ApplyLayout();
    }

    private List<Transform> GetOrderedChildLetters()
    {
        List<Transform> result = new();

        for (int i = 0; i < transform.childCount; i++)
        {
            result.Add(transform.GetChild(i));
        }

        return result;
    }

    private void ApplySingleLineLayout(List<Transform> letters)
    {
        List<float> xPositions = new();
        float cursor = 0f;

        for (int i = 0; i < letters.Count; i++)
        {
            xPositions.Add(cursor);

            bool isWordGap = i == firstLineLetterCount - 1;
            cursor += isWordGap ? wordSpacing : letterSpacing;
        }

        CenterPositions(xPositions);

        for (int i = 0; i < letters.Count; i++)
        {
            if (letters[i] == null)
                continue;

            letters[i].localPosition = new Vector3(
                xPositions[i],
                0f,
                localZ
            );
        }
    }

    private void ApplyTwoLineLayout(List<Transform> letters)
    {
        int split = Mathf.Clamp(firstLineLetterCount, 0, letters.Count);

        List<int> firstLineIndices = new();
        List<int> secondLineIndices = new();

        for (int i = 0; i < letters.Count; i++)
        {
            if (i < split) firstLineIndices.Add(i);
            else secondLineIndices.Add(i);
        }

        List<float> firstLineX = BuildCenteredLine(firstLineIndices.Count, letterSpacing);
        List<float> secondLineX = BuildCenteredLine(secondLineIndices.Count, letterSpacing);

        float topY = lineSpacing * 0.5f;
        float bottomY = -lineSpacing * 0.5f;

        for (int i = 0; i < firstLineIndices.Count; i++)
        {
            int letterIndex = firstLineIndices[i];
            if (letters[letterIndex] == null)
                continue;

            letters[letterIndex].localPosition = new Vector3(
                firstLineX[i],
                topY,
                localZ
            );
        }

        for (int i = 0; i < secondLineIndices.Count; i++)
        {
            int letterIndex = secondLineIndices[i];
            if (letters[letterIndex] == null)
                continue;

            letters[letterIndex].localPosition = new Vector3(
                secondLineX[i],
                bottomY,
                localZ
            );
        }
    }

    private List<float> BuildCenteredLine(int count, float spacing)
    {
        List<float> positions = new();

        float cursor = 0f;
        for (int i = 0; i < count; i++)
        {
            positions.Add(cursor);
            cursor += spacing;
        }

        CenterPositions(positions);
        return positions;
    }

    private void CenterPositions(List<float> positions)
    {
        if (positions.Count == 0)
            return;

        float min = positions[0];
        float max = positions[positions.Count - 1];
        float center = (min + max) * 0.5f;

        for (int i = 0; i < positions.Count; i++)
            positions[i] -= center;
    }
}