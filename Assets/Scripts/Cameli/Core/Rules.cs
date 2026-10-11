namespace Cameli.Core
{
    /// <summary>Immutable snapshot of a run. Small enough to copy freely (undo history, solver queue).</summary>
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

        /// <summary>Matched a gate; the gate took the mixed color.</summary>
        PassedGate,

        /// <summary>Ate a color cell and the mix now matches the target.</summary>
        Cleared,

        BlockedByEdge,
        BlockedByWall,
        BlockedByGate,
        AlreadyCleared,
    }

    public static class MoveOutcomes
    {
        /// <summary>True when the player actually moved (the move counts and can be undone).</summary>
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
                    bool cleared = ColorMath.Matches(ColorMath.Mix(drops), stage.TargetColor);
                    next = new BoardState(target, state.Eaten | (1UL << target), drops, moves, cleared);
                    return cleared ? MoveOutcome.Cleared : MoveOutcome.Ate;
                }

                default:
                    next = new BoardState(target, state.Eaten, state.Drops, moves, false);
                    return MoveOutcome.Moved;
            }
        }

        public static bool CanPassGate(GateDefinition gate, DropCounts drops) =>
            !drops.IsEmpty && ColorMath.Matches(ColorMath.Mix(drops), gate.Color);

        /// <summary>The cell kind as the player currently sees it: eaten color cells read as floor.</summary>
        public static CellKind VisibleKind(StageDefinition stage, in BoardState state, int index)
        {
            Cell cell = stage.CellAt(index);
            return cell.Kind == CellKind.Color && state.IsEaten(index) ? CellKind.Floor : cell.Kind;
        }
    }

    public static class Scoring
    {
        /// <summary>Extra moves over the optimum that still earn two stars.</summary>
        public const int TwoStarSlack = 2;

        /// <summary>3 = optimal moves, 2 = within optimal + 2, 1 = any clear.</summary>
        public static int Stars(int moves, int optimalMoves)
        {
            if (optimalMoves <= 0) return 1;
            if (moves <= optimalMoves) return 3;
            return moves <= optimalMoves + TwoStarSlack ? 2 : 1;
        }
    }
}
