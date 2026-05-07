using UnityEngine;

public static class VoxelIconPatterns
{
    public enum IconType
    {
        BackArrow,
        CheckMark,
        CrossMark
    }

    public static bool[,] GetPattern(IconType iconType)
    {
        switch (iconType)
        {
            case IconType.BackArrow:
                return PatternFromRows(
                    "0001111100",
                    "1011111110",
                    "1111000111",
                    "1110000011",
                    "1111000011",
                    "0000000011",
                    "0011000111",
                    "0011111110",
                    "0001111100"
                );

            case IconType.CheckMark:
                return PatternFromRows(
                    "0000000000",
                    "0000000011",
                    "0000000111",
                    "0000001110",
                    "1100011100",
                    "1110111000",
                    "0111110000",
                    "0011100000",
                    "0001000000"
                );

            case IconType.CrossMark:
                return PatternFromRows(
                    "1100000011",
                    "1110000111",
                    "0111001110",
                    "0011111100",
                    "0001111000",
                    "0011111100",
                    "0111001110",
                    "1110000111",
                    "1100000011"
                );

            default:
                return PatternFromRows(
                    "000",
                    "000",
                    "000"
                );
        }
    }

    private static bool[,] PatternFromRows(params string[] rows)
    {
        int height = rows.Length;
        int width = rows[0].Length;

        bool[,] result = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, height - 1 - y] = rows[y][x] == '1';
            }
        }

        return result;
    }
}