namespace Cameli.Core
{
    /// <summary>The game's fixed 7-color palette. Stage files refer to colors by letter.</summary>
    public static class Palette
    {
        public const int Count = 7;

        const string Letters = "ROYGBNP";

        static readonly Rgb[] colors =
        {
            new Rgb(232, 48, 48),   // R 빨강
            new Rgb(247, 150, 70),  // O 주황
            new Rgb(255, 246, 75),  // Y 노랑
            new Rgb(171, 213, 84),  // G 초록
            new Rgb(71, 163, 255),  // B 파랑
            new Rgb(35, 20, 234),   // N 남색
            new Rgb(174, 81, 199),  // P 보라
        };

        static readonly string[] names = { "빨강", "주황", "노랑", "초록", "파랑", "남색", "보라" };

        public static Rgb Color(int index) => colors[index];
        public static char Letter(int index) => Letters[index];
        public static string Name(int index) => names[index];

        /// <summary>Palette index for a color letter, or -1 if the letter is not a palette color.</summary>
        public static int IndexOf(char letter) => Letters.IndexOf(letter);
    }
}
