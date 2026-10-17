using System;

namespace Cameli.Core
{
    /// <summary>Immutable snapshot of a run. Small enough to copy freely (solver queue).</summary>
    public readonly struct BoardState
    {
        public readonly int Position;

        /// <summary>Bit i set = color cell i has been eaten and is now floor.</summary>
        public readonly ulong Eaten;

        public readonly DropCounts Drops;
        public readonly int Moves;
        public readonly bool Cleared;

        public BoardState(int position, ulong eaten, DropCounts drops, int moves, bool cleared)
        {
            Position = position;
            Eaten = eaten;
            Drops = drops;
            Moves = moves;
            Cleared = cleared;
        }

        public static BoardState Initial(StageDefinition stage) =>
            new BoardState(stage.StartIndex, 0, DropCounts.Empty, 0, false);

        public bool IsEaten(int index) => (Eaten & (1UL << index)) != 0;
    }

    public enum MoveOutcome
    {
        /// <summary>Stepped onto floor, the start cell, or an already eaten cell.</summary>
        Moved,

        /// <summary>Ate a color cell.</summary>
        Ate,

        /// <summary>Met the gate's recipe; the gate took the mixed color.</summary>
        PassedGate,

        /// <summary>Ate a color cell and the mix now meets the target recipe (color and drop count).</summary>
        Cleared,

        BlockedByEdge,
        BlockedByWall,
        BlockedByGate,
        AlreadyCleared,
    }

    public static class MoveOutcomes
    {
        /// <summary>True when the player actually moved (the move counts).</summary>
        public static bool IsMove(this MoveOutcome outcome) => outcome <= MoveOutcome.Cleared;
    }

    /// <summary>The one implementation of the game rules, shared by the game and the solver.</summary>
    public static class Rules
    {
        public static MoveOutcome Step(StageDefinition stage, in BoardState state, Direction direction, out BoardState next)
        {
            next = state;
            if (state.Cleared) return MoveOutcome.AlreadyCleared;

            int x = state.Position % stage.Width + direction.Dx();
            int y = state.Position / stage.Width + direction.Dy();
            if (x < 0 || x >= stage.Width || y < 0 || y >= stage.Height) return MoveOutcome.BlockedByEdge;

            int target = stage.IndexOf(x, y);
            Cell cell = stage.CellAt(target);
            int moves = state.Moves + 1;

            switch (cell.Kind)
            {
                case CellKind.Wall:
                    return MoveOutcome.BlockedByWall;

                case CellKind.Gate:
                {
                    if (!CanPassGate(stage.Gates[cell.Gate], state.Drops)) return MoveOutcome.BlockedByGate;
                    next = new BoardState(target, state.Eaten, DropCounts.Empty, moves, false);
                    return MoveOutcome.PassedGate;
                }

                case CellKind.Color when !state.IsEaten(target):
                {
                    DropCounts drops = state.Drops.Add(cell.Color);
                    bool cleared = Meets(drops, stage.Target, stage.TargetColor);
                    next = new BoardState(target, state.Eaten | (1UL << target), drops, moves, cleared);
                    return cleared ? MoveOutcome.Cleared : MoveOutcome.Ate;
                }

                default:
                    next = new BoardState(target, state.Eaten, state.Drops, moves, false);
                    return MoveOutcome.Moved;
            }
        }

        public static bool CanPassGate(GateDefinition gate, DropCounts drops) => Meets(drops, gate.Recipe, gate.Color);

        /// <summary>
        /// A recipe is met when the mix has exactly its drop count and matches its color (±2). Ratio multiples
        /// such as RRBB for RB mix to the same color but don't count, so the drop counter on screen and the
        /// color together decide.
        /// </summary>
        public static bool Meets(DropCounts drops, DropCounts recipe, Rgb color) =>
            drops.Total == recipe.Total && ColorMath.Matches(ColorMath.Mix(drops), color);

        /// <summary>
        /// True when the mix holds more drops than the target and every gate recipe. Drops only grow until a
        /// gate takes them, so nothing can be cleared or opened any more without restarting.
        /// </summary>
        public static bool IsOverfilled(StageDefinition stage, in BoardState state) =>
            state.Drops.Total > stage.MaxRecipeDrops;

        /// <summary>
        /// True when the mix holds more drops of some color than the target recipe uses. Drops only grow until a gate
        /// takes them, so on a stage without gates the attempt can no longer clear.
        /// </summary>
        public static bool ExceedsTarget(StageDefinition stage, in BoardState state)
        {
            for (int color = 0; color < Palette.Count; color++)
                if (state.Drops[color] > stage.Target[color]) return true;
            return false;
        }

        /// <summary>The cell kind as the player currently sees it: eaten color cells read as floor.</summary>
        public static CellKind VisibleKind(StageDefinition stage, in BoardState state, int index)
        {
            Cell cell = stage.CellAt(index);
            return cell.Kind == CellKind.Color && state.IsEaten(index) ? CellKind.Floor : cell.Kind;
        }
    }

    public static class Scoring
    {
        /// <summary>Most stars a clear can earn after a hint in the same attempt.</summary>
        public const int HintedMaxStars = 2;

        /// <summary>
        /// 3 with no restarts, 2 after one, 1 after more (design doc 3.3); moves don't count. Capped at
        /// <see cref="HintedMaxStars"/> when <paramref name="hinted"/>.
        /// </summary>
        public static int Stars(int restarts, bool hinted)
        {
            int stars = restarts <= 0 ? 3 : restarts == 1 ? 2 : 1;
            return hinted ? Math.Min(stars, HintedMaxStars) : stars;
        }
    }
}
