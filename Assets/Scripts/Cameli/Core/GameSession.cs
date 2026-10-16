using System;
using System.Collections.Generic;

namespace Cameli.Core
{
    /// <summary>
    /// One visit to a stage: applies moves, counts the restarts ("다시 하기") that score the clear, and keeps the
    /// recipe colors hints revealed in the current attempt. A visit starts on entering the stage and again when a
    /// cleared stage is replayed (<see cref="StartOver"/>); an attempt starts with the visit and again on every
    /// <see cref="Restart"/>.
    /// </summary>
    public sealed class GameSession
    {
        readonly List<int> mixOrder = new List<int>();
        readonly List<int> revealed = new List<int>();
        readonly List<int> recipeColors = new List<int>();

        public StageDefinition Stage { get; }
        public BoardState State { get; private set; }

        /// <summary>Restarts made after at least one move in this visit; they set the stars (<see cref="Scoring.Stars"/>).</summary>
        public int Restarts { get; private set; }

        /// <summary>Recipe colors (palette indices) revealed by hints in the current attempt, in the order revealed.</summary>
        public IReadOnlyList<int> RevealedColors => revealed;

        /// <summary>A hint was used in the current attempt; caps the stars at <see cref="Scoring.HintedMaxStars"/>.</summary>
        public bool UsedHint => revealed.Count > 0;

        /// <summary>Colors in the current mix in the order they were eaten; a passed gate empties it.</summary>
        public IReadOnlyList<int> MixOrder => mixOrder;

        public GameSession(StageDefinition stage)
        {
            Stage = stage;
            for (int color = 0; color < Palette.Count; color++)
                if (stage.Target[color] > 0) recipeColors.Add(color);
            StartOver();
        }

        public bool IsCleared => State.Cleared;

        /// <summary>The current mix; white while nothing has been eaten.</summary>
        public Rgb CurrentColor => ColorMath.Mix(State.Drops);

        /// <summary>The stars a clear would earn right now; shown during play.</summary>
        public int StarsIfCleared => Scoring.Stars(Restarts, UsedHint);

        public int Stars => IsCleared ? StarsIfCleared : 0;

        /// <summary>"다시 하기" has something to start over: at least one move in this attempt, and not cleared yet.</summary>
        public bool CanRestart => !IsCleared && State.Moves > 0;

        /// <summary>A hint can still reveal a recipe color in this attempt.</summary>
        public bool CanRevealColor => !IsCleared && revealed.Count < recipeColors.Count;

        public MoveOutcome Move(Direction direction)
        {
            DropCounts before = State.Drops;
            MoveOutcome outcome = Rules.Step(Stage, State, direction, out BoardState next);
            if (!outcome.IsMove()) return outcome;

            State = next;
            DropCounts after = State.Drops;
            if (after.IsEmpty)
            {
                mixOrder.Clear();
            }
            else if (after.Total == before.Total + 1)
            {
                for (int color = 0; color < Palette.Count; color++)
                    if (after[color] == before[color] + 1) mixOrder.Add(color);
            }
            return outcome;
        }

        /// <summary>
        /// "다시 하기": back to the initial board for a new attempt, counted in <see cref="Restarts"/>; the hint colors
        /// are hidden again. Does nothing (and returns false) before the first move, so it can neither cost a star nor
        /// hide a hint for free, and once cleared (replay with <see cref="StartOver"/>).
        /// </summary>
        public bool Restart()
        {
            if (!CanRestart) return false;
            Restarts++;
            ResetAttempt();
            return true;
        }

        /// <summary>A new visit: the initial board with no restarts and no hints, as on entering the stage.</summary>
        public void StartOver()
        {
            Restarts = 0;
            ResetAttempt();
        }

        /// <summary>
        /// Reveals a random recipe color that this attempt has not revealed yet and returns its palette index; -1 (and
        /// nothing changes) when every color is revealed or the stage is cleared. Caps the attempt's stars.
        /// </summary>
        public int RevealColor(Random random)
        {
            if (!CanRevealColor) return -1;
            var hidden = new List<int>();
            foreach (int color in recipeColors)
                if (!revealed.Contains(color)) hidden.Add(color);
            int pick = hidden[random.Next(hidden.Count)];
            revealed.Add(pick);
            return pick;
        }

        void ResetAttempt()
        {
            State = BoardState.Initial(Stage);
            mixOrder.Clear();
            revealed.Clear();
        }
    }
}
