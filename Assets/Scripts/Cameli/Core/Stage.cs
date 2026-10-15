using System;
using System.Collections.Generic;

namespace Cameli.Core
{
    /// <summary>
    /// JSON shape of a stage file in Resources/CameliStages. Field names are the JSON keys and stay
    /// compatible with UnityEngine.JsonUtility.
    /// </summary>
    [Serializable]
    public class StageFile
    {
        public int id;
        public string title;

        /// <summary>One-line tutorial text shown above the board (optional).</summary>
        public string hint;

        /// <summary>
        /// One string per row, top row first, cells separated by single spaces. Tokens: palette letters
        /// (R O Y G B N P), S = start, # = wall, . = floor, g0/g1/… = gate by index into <see cref="gates"/>.
        /// </summary>
        public string[] rows;

        /// <summary>Target recipe, e.g. "RRY".</summary>
        public string target;

        /// <summary>Gate recipes, referenced by gN tokens (optional).</summary>
        public string[] gates;

        /// <summary>Fewest moves to clear, from the solver. Used for stars.</summary>
        public int optimalMoves;

        /// <summary>Tutorial: cell [x, y] (column, row from the top) that glows until the first move (optional).</summary>
        public int[] focus;

        /// <summary>Tutorial: "up", "down", "left" or "right"; a finger swipes that way from the start until the first move (optional).</summary>
        public string swipe;
    }

    public sealed class StageFormatException : Exception
    {
        public StageFormatException(string message) : base(message) { }
    }

    /// <summary>A consuming color gate: entering requires the current color to match, and passing empties the mix.</summary>
    public sealed class GateDefinition
    {
        public DropCounts Recipe { get; }
        public Rgb Color { get; }

        public GateDefinition(DropCounts recipe)
        {
            Recipe = recipe;
            Color = ColorMath.Mix(recipe);
        }
    }

    /// <summary>A validated, immutable stage.</summary>
    public sealed class StageDefinition
    {
        /// <summary>Eaten cells are tracked in a 64-bit mask.</summary>
        public const int MaxCells = 64;

        readonly Cell[] cells;
        readonly GateDefinition[] gates;

        public int Id { get; }
        public string Title { get; }
        public string Hint { get; }
        public int Width { get; }
        public int Height { get; }
        public int StartIndex { get; }
        public DropCounts Target { get; }
        public Rgb TargetColor { get; }
        public int OptimalMoves { get; }

        /// <summary>Tutorial cell to highlight before the first move, as a cell index; -1 for none.</summary>
        public int FocusIndex { get; }

        /// <summary>Tutorial swipe to show from the start cell before the first move; null for none.</summary>
        public Direction? Swipe { get; }

        /// <summary>The largest drop count among the target and the gate recipes; any more can never be used.</summary>
        public int MaxRecipeDrops { get; }

        public int CellCount => cells.Length;
        public IReadOnlyList<GateDefinition> Gates => gates;

        StageDefinition(StageFile file, int width, int height, Cell[] cells, int startIndex, DropCounts target, GateDefinition[] gates,
            int focusIndex, Direction? swipe)
        {
            Id = file.id;
            Title = file.title ?? string.Empty;
            Hint = file.hint ?? string.Empty;
            Width = width;
            Height = height;
            this.cells = cells;
            StartIndex = startIndex;
            Target = target;
            TargetColor = ColorMath.Mix(target);
            OptimalMoves = file.optimalMoves;
            this.gates = gates;
            FocusIndex = focusIndex;
            Swipe = swipe;
            MaxRecipeDrops = target.Total;
            foreach (GateDefinition gate in gates) MaxRecipeDrops = Math.Max(MaxRecipeDrops, gate.Recipe.Total);
        }

        public Cell CellAt(int index) => cells[index];
        public int IndexOf(int x, int y) => y * Width + x;

        /// <summary>
        /// The start cell the design requires: the exact center on odd sizes, and the top-left cell of the
        /// central 2×2 on even sizes.
        /// </summary>
        public static int CenterIndex(int width, int height) => (height - 1) / 2 * width + (width - 1) / 2;

        public static StageDefinition FromFile(StageFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            string label = $"Stage {file.id}";

            var gates = new GateDefinition[file.gates?.Length ?? 0];
            for (int i = 0; i < gates.Length; i++) gates[i] = new GateDefinition(ParseRecipe(file.gates[i], label, $"gate g{i}"));

            if (file.rows == null || file.rows.Length == 0) throw new StageFormatException($"{label}: rows is empty.");
            int height = file.rows.Length;
            int width = SplitRow(file.rows[0]).Length;
            if (width * height > MaxCells) throw new StageFormatException($"{label}: {width}x{height} exceeds {MaxCells} cells.");

            var cells = new Cell[width * height];
            int start = -1;
            for (int y = 0; y < height; y++)
            {
                string[] tokens = SplitRow(file.rows[y]);
                if (tokens.Length != width)
                    throw new StageFormatException($"{label}: row {y} has {tokens.Length} cells, expected {width}.");

                for (int x = 0; x < width; x++)
                {
                    Cell cell = ParseToken(tokens[x], gates.Length, label, x, y);
                    if (cell.Kind == CellKind.Start)
                    {
                        if (start >= 0) throw new StageFormatException($"{label}: more than one start cell (S).");
                        start = y * width + x;
                    }
                    cells[y * width + x] = cell;
                }
            }

            if (start < 0) throw new StageFormatException($"{label}: no start cell (S).");
            int center = CenterIndex(width, height);
            if (start != center)
                throw new StageFormatException($"{label}: start must be at column {center % width}, row {center / width}.");

            DropCounts target = ParseRecipe(file.target, label, "target");
            int focus = ParseFocus(file.focus, width, height, cells, label);
            Direction? swipe = ParseSwipe(file.swipe, label);
            if (swipe.HasValue) CheckSwipe(swipe.Value, start, width, height, cells, label);
            return new StageDefinition(file, width, height, cells, start, target, gates, focus, swipe);
        }

        /// <summary>
        /// Missing or empty = no focus (JsonUtility reads a missing array as empty). The focus must be a cell the
        /// player can step to: not a wall, and not the start cell they already stand on.
        /// </summary>
        static int ParseFocus(int[] focus, int width, int height, Cell[] cells, string label)
        {
            if (focus == null || focus.Length == 0) return -1;
            if (focus.Length != 2) throw new StageFormatException($"{label}: focus must be [x, y], got {focus.Length} number(s).");
            int x = focus[0], y = focus[1];
            if (x < 0 || x >= width || y < 0 || y >= height)
                throw new StageFormatException($"{label}: focus ({x}, {y}) is outside the {width}x{height} board.");
            CellKind kind = cells[y * width + x].Kind;
            if (kind == CellKind.Wall || kind == CellKind.Start)
                throw new StageFormatException($"{label}: focus ({x}, {y}) is {(kind == CellKind.Wall ? "a wall" : "the start cell")}, not a cell to step to.");
            return y * width + x;
        }

        /// <summary>The tutorial swipe must show a real first step: from the start onto the board, not into a wall.</summary>
        static void CheckSwipe(Direction swipe, int start, int width, int height, Cell[] cells, string label)
        {
            int x = start % width + swipe.Dx();
            int y = start / width + swipe.Dy();
            if (x < 0 || x >= width || y < 0 || y >= height)
                throw new StageFormatException($"{label}: swipe {swipe} leads off the board from the start.");
            if (cells[y * width + x].Kind == CellKind.Wall)
                throw new StageFormatException($"{label}: swipe {swipe} leads into a wall from the start.");
        }

        static Direction? ParseSwipe(string swipe, string label)
        {
            switch (swipe)
            {
                case null:
                case "": return null;
                case "up": return Direction.Up;
                case "down": return Direction.Down;
                case "left": return Direction.Left;
                case "right": return Direction.Right;
            }
            throw new StageFormatException($"{label}: swipe must be up, down, left or right, got \"{swipe}\".");
        }

        static string[] SplitRow(string row) =>
            (row ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        static Cell ParseToken(string token, int gateCount, string label, int x, int y)
        {
            if (token.Length == 1)
            {
                switch (token[0])
                {
                    case 'S': return Cell.Start;
                    case '#': return Cell.Wall;
                    case '.': return Cell.Floor;
                }
                int color = Palette.IndexOf(token[0]);
                if (color >= 0) return Cell.OfColor(color);
            }
            else if (token[0] == 'g' && int.TryParse(token.Substring(1), out int gate))
            {
                if (gate < 0 || gate >= gateCount)
                    throw new StageFormatException($"{label}: cell ({x}, {y}) refers to gate g{gate}, but only {gateCount} gate(s) are defined.");
                return Cell.OfGate(gate);
            }
            throw new StageFormatException($"{label}: unknown cell token \"{token}\" at ({x}, {y}).");
        }

        static DropCounts ParseRecipe(string recipe, string label, string what)
        {
            try
            {
                return DropCounts.Parse(recipe);
            }
            catch (FormatException e)
            {
                throw new StageFormatException($"{label}: invalid {what} recipe. {e.Message}");
            }
        }
    }
}
