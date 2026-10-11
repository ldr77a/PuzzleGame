namespace Cameli.Core
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }

    public static class Directions
    {
        public static readonly Direction[] All = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        /// <summary>Column offset; x grows to the right.</summary>
        public static int Dx(this Direction direction) =>
            direction == Direction.Left ? -1 : direction == Direction.Right ? 1 : 0;

        /// <summary>Row offset; row 0 is the top row, so Up is -1.</summary>
        public static int Dy(this Direction direction) =>
            direction == Direction.Up ? -1 : direction == Direction.Down ? 1 : 0;
    }

    public enum CellKind : byte
    {
        Floor,
        Start,
        Wall,
        Color,
        Gate,
    }

    public readonly struct Cell
    {
        public readonly CellKind Kind;

        /// <summary>Palette index for Color cells, otherwise -1.</summary>
        public readonly int Color;

        /// <summary>Index into StageDefinition.Gates for Gate cells, otherwise -1.</summary>
        public readonly int Gate;

        Cell(CellKind kind, int color, int gate)
        {
            Kind = kind;
            Color = color;
            Gate = gate;
        }

        public static Cell Floor => new Cell(CellKind.Floor, -1, -1);
        public static Cell Start => new Cell(CellKind.Start, -1, -1);
        public static Cell Wall => new Cell(CellKind.Wall, -1, -1);
        public static Cell OfColor(int color) => new Cell(CellKind.Color, color, -1);
        public static Cell OfGate(int gate) => new Cell(CellKind.Gate, -1, gate);
    }
}
