using System;
using System.Text;

namespace Cameli.Core
{
    /// <summary>How many drops of each palette color have been mixed. Immutable; packs 8 bits per color.</summary>
    public readonly struct DropCounts : IEquatable<DropCounts>
    {
        public const int MaxPerColor = 255;
        public static readonly DropCounts Empty = default;

        const int Bits = 8;
        const ulong Mask = 0xFF;

        readonly ulong packed;

        DropCounts(ulong packed)
        {
            this.packed = packed;
        }

        public int this[int color] => (int)((packed >> (color * Bits)) & Mask);
        public bool IsEmpty => packed == 0;

        public int Total
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Palette.Count; i++) total += this[i];
                return total;
            }
        }

        public DropCounts Add(int color)
        {
            if (this[color] == MaxPerColor) throw new InvalidOperationException("Too many drops of one color.");
            return new DropCounts(packed + (1UL << (color * Bits)));
        }

        /// <summary>Parses recipe notation such as "RRY" (two red, one yellow).</summary>
        public static DropCounts Parse(string recipe)
        {
            if (string.IsNullOrEmpty(recipe)) throw new FormatException("Recipe is empty.");

            DropCounts counts = Empty;
            foreach (char letter in recipe)
            {
                int color = Palette.IndexOf(letter);
                if (color < 0) throw new FormatException($"Unknown color letter '{letter}' in recipe \"{recipe}\".");
                counts = counts.Add(color);
            }
            return counts;
        }

        public bool Equals(DropCounts other) => packed == other.packed;
        public override bool Equals(object obj) => obj is DropCounts other && Equals(other);
        public override int GetHashCode() => packed.GetHashCode();

        /// <summary>Recipe notation in palette order, e.g. "RRY".</summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Palette.Count; i++) builder.Append(Palette.Letter(i), this[i]);
            return builder.ToString();
        }
    }
}
