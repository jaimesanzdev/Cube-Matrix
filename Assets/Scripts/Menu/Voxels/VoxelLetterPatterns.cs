using UnityEngine;

public static class VoxelLetterPatterns
{
    public static bool[,] GetPattern(char c)
    {
        c = char.ToUpper(c);

        switch (c)
        {
            case 'A':
                return PatternFromRows(
                    "00111100",
                    "01100110",
                    "11000011",
                    "11111111",
                    "11000011",
                    "11000011",
                    "11000011",
                    "11000011"
                );

            case 'C':
                return PatternFromRows(
                    "00111110",
                    "01100011",
                    "11000000",
                    "11000000",
                    "11000000",
                    "11000000",
                    "01100011",
                    "00111110"
                );

            case 'D':
                return PatternFromRows(
                    "11111000",
                    "11001100",
                    "11000110",
                    "11000110",
                    "11000110",
                    "11000110",
                    "11001100",
                    "11111000"
                );

            case 'E':
                return PatternFromRows(
                    "11111110",
                    "11000000",
                    "11000000",
                    "11111100",
                    "11000000",
                    "11000000",
                    "11000000",
                    "11111110"
                );

            case 'I':
                return PatternFromRows(
                    "11111111",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000",
                    "11111111"
                );

            case 'L':
                return PatternFromRows(
                    "11000000",
                    "11000000",
                    "11000000",
                    "11000000",
                    "11000000",
                    "11000000",
                    "11111110",
                    "11111110"
                );

            case 'N':
                return PatternFromRows(
                    "11000011",
                    "11100011",
                    "11110011",
                    "11011011",
                    "11001111",
                    "11000111",
                    "11000011",
                    "11000011"
                );

            case 'O':
                return PatternFromRows(
                    "00111100",
                    "01100110",
                    "11000011",
                    "11000011",
                    "11000011",
                    "11000011",
                    "01100110",
                    "00111100"
                );

            case 'P':
                return PatternFromRows(
                    "11111100",
                    "11000110",
                    "11000110",
                    "11111100",
                    "11000000",
                    "11000000",
                    "11000000",
                    "11000000"
                );

            case 'Q':
                return PatternFromRows(
                    "00111100",
                    "01100110",
                    "11000011",
                    "11000011",
                    "11000011",
                    "11001011",
                    "01100110",
                    "00111101"
                );

            case 'R':
                return PatternFromRows(
                    "11111100",
                    "11000110",
                    "11000110",
                    "11111100",
                    "11011000",
                    "11001100",
                    "11000110",
                    "11000011"
                );

            case 'S':
                return PatternFromRows(
                    "00111100",
                    "11000110",
                    "11000000",
                    "00111100",
                    "00000110",
                    "00000110",
                    "11000110",
                    "01111100"
                );

            case 'T':
                return PatternFromRows(
                    "11111111",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000"
                );

            case 'U':
                return PatternFromRows(
                    "11000011",
                    "11000011",
                    "11000011",
                    "11000011",
                    "11000011",
                    "11000011",
                    "01100110",
                    "00111100"
                );

            case 'Y':
                return PatternFromRows(
                    "11000011",
                    "11000011",
                    "01100110",
                    "00111100",
                    "00011000",
                    "00011000",
                    "00011000",
                    "00011000"
                );

            default:
                return PatternFromRows(
                    "00000000",
                    "00000000",
                    "00000000",
                    "00000000",
                    "00000000",
                    "00000000",
                    "00000000",
                    "00000000"
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