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

        /// <summary>
        /// How the goal card's similarity falls off with the color difference: ΔE 10 (the stage audit's ambiguity
        /// threshold, two clearly different colors) shows about 75.
        /// </summary>
        public const double SimilarityFalloff = 35;

        /// <summary>
        /// How close a mix looks to a target, 0 to 100, for the goal card: 100 when it matches like the clear check
        /// does (the drop count aside), otherwise falling off quickly with the CIELAB difference and at most 99.
        /// </summary>
        public static int Similarity(Rgb target, Rgb mix)
        {
            if (Matches(target, mix)) return 100;
            int percent = (int)Math.Round(100 * Math.Exp(-DeltaE(target, mix) / SimilarityFalloff));
            return Math.Max(0, Math.Min(99, percent));
        }

        /// <summary>CIE76 color difference: straight-line distance in CIELAB (D65). About 2.3 is just noticeable.</summary>
        public static double DeltaE(Rgb a, Rgb b)
        {
            double[] la = Lab(a), lb = Lab(b);
            return Math.Sqrt(Math.Pow(la[0] - lb[0], 2) + Math.Pow(la[1] - lb[1], 2) + Math.Pow(la[2] - lb[2], 2));
        }

        static double[] Lab(Rgb c)
        {
            double Lin(byte v) { double x = v / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            double r = Lin(c.R), g = Lin(c.G), b = Lin(c.B);
            double X = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
            double Y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            double Z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
            double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3) : 7.787 * t + 16.0 / 116;
            return new[] { 116 * F(Y) - 16, 500 * (F(X) - F(Y)), 200 * (F(Y) - F(Z)) };
        }

        static float Pow(float value, float exponent) => (float)Math.Pow(value, exponent);
        static byte ToByte(float channel) => (byte)(int)Math.Round(channel * 255f);
    }
}
