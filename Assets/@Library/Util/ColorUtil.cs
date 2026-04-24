using UnityEngine;

namespace Library
{

    public static class ColorUtil
    {
        public static Color Mix(Color color1, Color color2, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            return Color.Lerp(color1, color2, ratio);
        }

        public static Color SetAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        public static Color GetRandomColor()
        {
            return new Color(Random.value, Random.value, Random.value);
        }

        public static Color FromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }

            Debug.LogWarning($"Invalid Hex Color Code: {hex}");
            return Color.white;
        }

        public static string ToHex(this Color color, bool includeAlpha = true)
        {
            return ColorUtility.ToHtmlStringRGBA(color).ToLower();
        }

        public static bool AreColorsEqual(Color color1, Color color2, float tolerance = 0.01f)
        {
            return Mathf.Abs(color1.r - color2.r) < tolerance &&
                   Mathf.Abs(color1.g - color2.g) < tolerance &&
                   Mathf.Abs(color1.b - color2.b) < tolerance &&
                   Mathf.Abs(color1.a - color2.a) < tolerance;
        }
    }
}
