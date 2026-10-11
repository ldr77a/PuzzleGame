using System.Collections.Generic;

namespace Cameli.Core
{
    /// <summary>One attempt at a stage: applies moves, keeps the undo history, and scores the clear.</summary>
    public sealed class GameSession
    {
        public const int UndoAllowance = 3;

        readonly Stack<BoardState> history = new Stack<BoardState>();

        public StageDefinition Stage { get; }
        public BoardState State { get; private set; }
        public int UndosRemaining { get; private set; }

        public GameSession(StageDefinition stage)
        {
            Stage = stage;
            Restart();
        }

        public bool IsCleared => State.Cleared;

        /// <summary>The current mix; white while nothing has been eaten.</summary>
        public Rgb CurrentColor => ColorMath.Mix(State.Drops);

        public int Stars => IsCleared ? Scoring.Stars(State.Moves, Stage.OptimalMoves) : 0;
        public bool CanUndo => !IsCleared && UndosRemaining > 0 && history.Count > 0;

        public MoveOutcome Move(Direction direction)
        {
            MoveOutcome outcome = Rules.Step(Stage, State, direction, out BoardState next);
            if (outcome.IsMove())
            {
                history.Push(State);
                State = next;
            }
            return outcome;
        }

        /// <summary>Takes back the last move, restoring the eaten cell and the previous mix.</summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            State = history.Pop();
            UndosRemaining--;
            return true;
        }

        /// <summary>
        /// Back to the initial board. Unlimited and free; it also refills undos, the same as leaving and
        /// re-entering the stage would.
        /// </summary>
        public void Restart()
        {
            history.Clear();
            State = BoardState.Initial(Stage);
            UndosRemaining = UndoAllowance;
        }
    }
}
