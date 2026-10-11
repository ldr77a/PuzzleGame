using Cameli.Core;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>Prototype colors for everything that is not a game color.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex(0xEEF2EC);
        public static readonly Color Board = Hex(0xD3DBD1);
        public static readonly Color Floor = Hex(0xE7ECE4);
        public static readonly Color Wall = Hex(0x4A544D);
        public static readonly Color Ink = Hex(0x1F2A24);
        public static readonly Color Muted = Hex(0x6B776F);
        public static readonly Color Panel = Hex(0xFFFFFF);
        public static readonly Color Button = Hex(0xFFFFFF);
        public static readonly Color Accent = Hex(0xC8463D);
        public static readonly Color StarOn = Hex(0xF2B33D);
        public static readonly Color StarOff = Hex(0xDCE1DA);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);

        public static Color32 ToColor(Rgb rgb) => new Color32(rgb.R, rgb.G, rgb.B, 255);

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
