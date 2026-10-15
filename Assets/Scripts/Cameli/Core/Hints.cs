namespace Cameli.Core
{
    public enum HintKind
    {
        /// <summary>Step in <see cref="Hint.Direction"/>: the first move of a shortest clear from the current state.</summary>
        NextStep,

        /// <summary>
        /// No clear from the current state. <see cref="Hint.UndosNeeded"/> says how far back a clear exists again,
        /// and <see cref="Hint.UndoWithinReach"/> whether the remaining undos get there (otherwise: restart).
        /// </summary>
        Stuck,

        /// <summary>The search hit its state cap; nothing is known, so the hint should not be spent.</summary>
        SearchLimit,

        AlreadyCleared,
    }

    /// <summary>Result of <see cref="Hints.Next"/>.</summary>
    public readonly struct Hint
    {
        public readonly HintKind Kind;

        /// <summary>The direction to step; meaningful for <see cref="HintKind.NextStep"/> only.</summary>
        public readonly Direction Direction;

        /// <summary>
        /// For <see cref="HintKind.Stuck"/>: the fewest undos that reach a state with a clear, or -1 when no earlier
        /// state of the attempt has one (a broken stage). 0 for the other kinds.
        /// </summary>
        public readonly int UndosNeeded;

        /// <summary>For <see cref="HintKind.Stuck"/>: true when <see cref="UndosNeeded"/> undos are still available.</summary>
        public readonly bool UndoWithinReach;

        Hint(HintKind kind, Direction direction, int undosNeeded, bool undoWithinReach)
        {
            Kind = kind;
            Direction = direction;
            UndosNeeded = undosNeeded;
            UndoWithinReach = undoWithinReach;
        }

        public static Hint Step(Direction direction) => new Hint(HintKind.NextStep, direction, 0, false);
        public static Hint Stuck(int undosNeeded, bool withinReach) => new Hint(HintKind.Stuck, Direction.Up, undosNeeded, withinReach);
        public static Hint SearchLimit => new Hint(HintKind.SearchLimit, Direction.Up, 0, false);
        public static Hint Cleared => new Hint(HintKind.AlreadyCleared, Direction.Up, 0, false);
    }

    /// <summary>The "next step" hint: a bounded solver search from the session's current state, run at hint time.</summary>
    public static class Hints
    {
        /// <summary>
        /// States one hint may explore, shared by every search it runs. The shipped stages need at most about
        /// 2,000 states even when exploring everything reachable from the start, so this leaves ample room for
        /// 6×6 boards while bounding the time and memory on a phone.
        /// </summary>
        public const int DefaultMaxStates = 200_000;

        /// <summary>
        /// Works out the hint for <paramref name="session"/>'s current state. It does not change the session; call
        /// <see cref="GameSession.MarkStepHint"/> when a <see cref="HintKind.NextStep"/> result is shown.
        /// </summary>
        public static Hint Next(GameSession session, int maxStates = DefaultMaxStates)
        {
            if (session.IsCleared) return Hint.Cleared;

            int budget = maxStates;
            SearchResult result = Solver.Search(session.Stage, session.State, 0, budget, out Solution solution, out int explored);
            if (result == SearchResult.LimitExceeded) return Hint.SearchLimit;
            if (result == SearchResult.Found) return Hint.Step(solution.Path[0]);

            // Stuck: find the fewest undos that lead back to a state with a clear.
            int undos = 0;
            foreach (BoardState earlier in session.History)
            {
                undos++;
                budget -= explored;
                if (budget <= 0) return Hint.SearchLimit;
                result = Solver.Search(session.Stage, earlier, 0, budget, out _, out explored);
                if (result == SearchResult.LimitExceeded) return Hint.SearchLimit;
                if (result == SearchResult.Found) return Hint.Stuck(undos, undos <= session.UndosRemaining);
            }
            return Hint.Stuck(-1, false);
        }
    }
}
