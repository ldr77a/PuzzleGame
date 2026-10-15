using System.Collections.Generic;

namespace Cameli.Core
{
    /// <summary>
    /// One attempt at a stage: applies moves, keeps the undo history, and scores the clear. An attempt starts on
    /// entering the stage and again on every restart.
    /// </summary>
    public sealed class GameSession
    {
        public const int UndoAllowance = 3;

        readonly Stack<BoardState> history = new Stack<BoardState>();

        public StageDefinition Stage { get; }
        public BoardState State { get; private set; }
        public int UndosRemaining { get; private set; }

        /// <summary>An undo was taken during this attempt; caps the stars (see <see cref="Scoring.AssistedMaxStars"/>).</summary>
        public bool UsedUndo { get; private set; }

        /// <summary>A "next step" hint was shown during this attempt; caps the stars. Revealing the recipe does not.</summary>
        public bool UsedStepHint { get; private set; }

        /// <summary>True when this attempt's stars are capped by an undo or a step hint.</summary>
        public bool Assisted => UsedUndo || UsedStepHint;

        public GameSession(StageDefinition stage)
        {
            Stage = stage;
            Restart();
        }

        public bool IsCleared => State.Cleared;

        /// <summary>The current mix; white while nothing has been eaten.</summary>
        public Rgb CurrentColor => ColorMath.Mix(State.Drops);

        public int Stars => IsCleared ? Scoring.Stars(State.Moves, Stage.OptimalMoves, Assisted) : 0;
        public bool CanUndo => !IsCleared && UndosRemaining > 0 && history.Count > 0;

        /// <summary>Number of earlier states kept for undo: the moves made since the attempt started.</summary>
        public int HistoryCount => history.Count;

        /// <summary>
        /// Earlier states of this attempt, most recent first: the first is what <see cref="Undo"/> would restore and
        /// the last is the initial board. Read-only; undo limits still apply when acting on it.
        /// </summary>
        public IEnumerable<BoardState> History
        {
            get
            {
                foreach (BoardState state in history) yield return state;
            }
        }

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
            UsedUndo = true;
            return true;
        }

        /// <summary>
        /// Adds undos to this attempt (the reward for watching an ad once undos run out). Using them still caps the
        /// stars; a restart refills to <see cref="UndoAllowance"/> as usual. Ignores counts below one.
        /// </summary>
        public void GrantUndos(int count)
        {
            if (count > 0) UndosRemaining += count;
        }

        /// <summary>Records that a "next step" hint was shown. Ignored once cleared, so it never lowers a scored clear.</summary>
        public void MarkStepHint()
        {
            if (!IsCleared) UsedStepHint = true;
        }

        /// <summary>
        /// Back to the initial board. Unlimited and free; it also refills undos and forgets the undo and hint use
        /// that cap the stars, the same as leaving and re-entering the stage would.
        /// </summary>
        public void Restart()
        {
            history.Clear();
            State = BoardState.Initial(Stage);
            UndosRemaining = UndoAllowance;
            UsedUndo = false;
            UsedStepHint = false;
        }
    }
}
