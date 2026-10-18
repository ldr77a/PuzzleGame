namespace Cameli.Core
{
    /// <summary>
    /// The game's fixed 7-color palette. Stage files refer to colors by letter. Softened on 2026-10-18 (Phase 2 spec
    /// §21): CIELAB chroma 85% and lightness +6 of the first palette, which took the neon edge off while keeping the
    /// colors apart (a real pastel made too many recipes look alike).
    /// </summary>
    public static class Palette
    {
        public const int Count = 7;

        const string Letters = "ROYGBNP";

        static readonly Rgb[] colors =
        {
            new Rgb(238, 85, 73),   // R 빨강
            new Rgb(255, 170, 102), // O 주황
            new Rgb(255, 254, 114), // Y 노랑
            new Rgb(194, 228, 119), // G 초록
            new Rgb(115, 178, 255), // B 파랑
            new Rgb(84, 51, 226),   // N 남색
            new Rgb(184, 105, 204), // P 보라
        };

        static readonly string[] names = { "빨강", "주황", "노랑", "초록", "파랑", "남색", "보라" };

        public static Rgb Color(int index) => colors[index];
        public static char Letter(int index) => Letters[index];
        public static string Name(int index) => names[index];

        /// <summary>Palette index for a color letter, or -1 if the letter is not a palette color.</summary>
        public static int IndexOf(char letter) => Letters.IndexOf(letter);
    }
}
