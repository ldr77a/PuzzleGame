using System;

namespace Cameli.Core
{
    /// <summary>8-bit RGB color. Engine-free counterpart of UnityEngine.Color32.</summary>
    public readonly struct Rgb : IEquatable<Rgb>
    {
        public static readonly Rgb White = new Rgb(255, 255, 255);

        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is Rgb other && Equals(other);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public override string ToString() => $"({R}, {G}, {B})";
    }
}
