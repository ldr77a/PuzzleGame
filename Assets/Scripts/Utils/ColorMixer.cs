using UnityEngine;
using System.Collections.Generic;

public static class ColorMixer
{
    /// <summary>
    /// 기하평균을 사용한 색상 혼합 (설계 문서 공식)
    /// </summary>
    public static Color32 MixColors(List<Color32> colors)
    {
        if (colors == null || colors.Count == 0)
        {
            return new Color32(255, 255, 255, 255); // 흰색 (초기 상태)
        }

        float r = 1f, g = 1f, b = 1f;
        int count = colors.Count;

        // 1. 정규화 및 곱셈
        foreach (Color32 color in colors)
        {
            r *= (color.r / 255f);
            g *= (color.g / 255f);
            b *= (color.b / 255f);
        }

        // 2. N제곱근 (기하평균)
        r = Mathf.Pow(r, 1f / count);
        g = Mathf.Pow(g, 1f / count);
        b = Mathf.Pow(b, 1f / count);

        // 3. 0-255 범위로 변환
        return new Color32(
            (byte)Mathf.RoundToInt(r * 255),
            (byte)Mathf.RoundToInt(g * 255),
            (byte)Mathf.RoundToInt(b * 255),
            255
        );
    }

    /// <summary>
    /// 색상 비교 (부동소수점 오차 고려)
    /// </summary>
    public static bool ColorsMatch(Color32 a, Color32 b, int tolerance = 2)
    {
        return Mathf.Abs(a.r - b.r) <= tolerance &&
               Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance;
    }

    /// <summary>
    /// int[] RGB 배열을 Color32로 변환
    /// </summary>
    public static Color32 ArrayToColor(int[] rgb)
    {
        return new Color32((byte)rgb[0], (byte)rgb[1], (byte)rgb[2], 255);
    }
}
