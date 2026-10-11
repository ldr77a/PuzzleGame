using System;

namespace Cameli.Core
{
    /// <summary>
    /// Color mixing rules. Same math as the legacy ColorMixer: per-channel geometric mean of every
    /// drop, computed in single precision and rounded half-to-even like Mathf.RoundToInt.
    /// </summary>
    public static class ColorMath
    {
        /// <summary>Allowed per-channel difference when comparing a mix to a target or gate color.</summary>
        public const int MatchTolerance = 2;

        public static Rgb Mix(DropCounts drops)
        {
            int total = drops.Total;
            if (total == 0) return Rgb.White;

            float r = 1f, g = 1f, b = 1f;
            for (int i = 0; i < Palette.Count; i++)
            {
                Rgb color = Palette.Color(i);
                for (int n = drops[i]; n > 0; n--)
                {
                    r *= color.R / 255f;
                    g *= color.G / 255f;
                    b *= color.B / 255f;
                }
            }

            float exponent = 1f / total;
            return new Rgb(ToByte(Pow(r, exponent)), ToByte(Pow(g, exponent)), ToByte(Pow(b, exponent)));
        }

        public static bool Matches(Rgb a, Rgb b, int tolerance = MatchTolerance) =>
            Math.Abs(a.R - b.R) <= tolerance &&
            Math.Abs(a.G - b.G) <= tolerance &&
            Math.Abs(a.B - b.B) <= tolerance;

        static float Pow(float value, float exponent) => (float)Math.Pow(value, exponent);
        static byte ToByte(float channel) => (byte)(int)Math.Round(channel * 255f);
    }
}
