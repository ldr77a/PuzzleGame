using Cameli.Core;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// The "동백 잎" colors for everything that is not a game color (Phase 2 spec §1). The prototype screens use
    /// them too.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = Hex(0xF1F3F0);
        public static readonly Color Board = Hex(0xD8DDD9);

        /// <summary>Empty floor, the start cell, and gate cells.</summary>
        public static readonly Color Floor = Hex(0xE9ECE9);

        public static readonly Color Wall = Hex(0x3D4741);
        public static readonly Color Ink = Hex(0x1E2B25);
        public static readonly Color Muted = Hex(0x66736C);

        /// <summary>Target card, speech bubble, garden beds, menu sheet.</summary>
        public static readonly Color Card = Hex(0xFFFFFF);

        /// <summary>Same as <see cref="Card"/>; the name the Phase 1 panels use.</summary>
        public static readonly Color Panel = Card;

        /// <summary>Ordinary button and its label.</summary>
        public static readonly Color Button = Hex(0xFFFFFF);
        public static readonly Color ButtonInk = Hex(0x24493B);

        /// <summary>Main button (이어하기, 다음, 계속하기) and its label.</summary>
        public static readonly Color Primary = Hex(0x2F5D4A);
        public static readonly Color PrimaryInk = Hex(0xFFFFFF);

        /// <summary>Emphasis: too many drops, the stage to play next.</summary>
        public static readonly Color Accent = Hex(0xC73A44);

        public static readonly Color StarOn = Hex(0xF2B33D);
        public static readonly Color StarOff = Hex(0xDADFDA);

        /// <summary>Darkens the screen behind a window or sheet.</summary>
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>Camellia center and the green of sepals, for the code-drawn flowers and buds.</summary>
        public static readonly Color Pollen = Hex(0xF6CF4A);
        public static readonly Color Leaf = Hex(0x4E8A5E);

        /// <summary>Locked stage bud in the garden (its target color stays hidden).</summary>
        public static readonly Color Locked = Hex(0xBFC6C1);

        public static Color32 ToColor(Rgb rgb) => new Color32(rgb.R, rgb.G, rgb.B, 255);

        /// <summary>Mixes a color toward white; used for the lighter inner petals.</summary>
        public static Color Lighten(Color color, float amount) => Color.Lerp(color, Color.white, amount);

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
