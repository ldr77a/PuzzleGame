using System;
using System.Collections.Generic;

namespace Cameli.Core
{
    public sealed class Solution
    {
        public IReadOnlyList<Direction> Path { get; }
        public BoardState Final { get; }
        public int Moves => Path.Count;

        public Solution(IReadOnlyList<Direction> path, BoardState final)
        {
            Path = path;
            Final = final;
        }
    }

    /// <summary>
    /// <see cref="Solver.Solve(StageDefinition, int, int)"/> hit its state cap: the board is too big or complex to search,
    /// which says nothing about whether it can be cleared.
    /// </summary>
    public sealed class SearchLimitException : InvalidOperationException
    {
        public SearchLimitException(string message) : base(message) { }
    }

    /// <summary>How a bounded search ended.</summary>
    public enum SearchResult
    {
        Found,

        /// <summary>Every reachable state was explored without a clear.</summary>
        Unsolvable,

        /// <summary>The state cap was reached before the search finished; nothing is known.</summary>
        LimitExceeded,
    }

    /// <summary>
    /// Breadth-first search over board states using the same <see cref="Rules"/> as the game, so the
    /// first clear found is a shortest one. Tooling and tests only; the game itself never searches.
    /// </summary>
    public static class Solver
    {
        public const int DefaultMaxStates = 2_000_000;

        /// <summary>Returns a shortest clear, or null if the stage cannot be cleared.</summary>
        /// <param name="maxDrops">
        /// States holding more drops than this are pruned. The default, the stage's largest recipe, loses
        /// nothing: an overfilled mix can neither clear nor pass a gate (see <see cref="Rules.IsOverfilled"/>).
        /// </param>
        /// <exception cref="SearchLimitException">The search needed more than <paramref name="maxStates"/> states.</exception>
        public static Solution Solve(StageDefinition stage, int maxDrops = 0, int maxStates = DefaultMaxStates) =>
            Solve(stage, BoardState.Initial(stage), maxDrops, maxStates);

        /// <summary>
        /// Returns a shortest clear starting from <paramref name="start"/>, or null if none exists. The path holds
        /// only the moves after <paramref name="start"/>; a start that is already cleared gives an empty path.
        /// </summary>
        /// <exception cref="SearchLimitException">The search needed more than <paramref name="maxStates"/> states.</exception>
        public static Solution Solve(StageDefinition stage, BoardState start, int maxDrops = 0, int maxStates = DefaultMaxStates)
        {
            if (TrySolve(stage, start, out Solution solution, maxDrops, maxStates) == SearchResult.LimitExceeded)
                throw new SearchLimitException($"Stage {stage.Id}: search exceeded {maxStates} states.");
            return solution;
        }

        /// <summary>
        /// Like <see cref="Solve(StageDefinition, BoardState, int, int)"/>, but reports hitting the state cap as a
        /// result instead of throwing, for runtime callers. <paramref name="solution"/> is set only when found.
        /// </summary>
        public static SearchResult TrySolve(StageDefinition stage, BoardState start, out Solution solution,
            int maxDrops = 0, int maxStates = DefaultMaxStates) =>
            Search(stage, start, maxDrops, maxStates, out solution, out _);

        /// <summary>The search itself; <paramref name="explored"/> is how many states it stored, for callers sharing one budget.</summary>
        internal static SearchResult Search(StageDefinition stage, BoardState start, int maxDrops, int maxStates,
            out Solution solution, out int explored)
        {
            solution = null;
            explored = 0;
            if (maxDrops <= 0) maxDrops = stage.MaxRecipeDrops;
            if (start.Cleared)
            {
                solution = new Solution(Array.Empty<Direction>(), start);
                return SearchResult.Found;
            }

            var nodes = new List<Node> { new Node(start, -1, Direction.Up) };
            var seen = new HashSet<Key> { new Key(start) };

            for (int head = 0; head < nodes.Count; head++)
            {
                BoardState current = nodes[head].State;
                foreach (Direction direction in Directions.All)
                {
                    MoveOutcome outcome = Rules.Step(stage, current, direction, out BoardState next);
                    if (!outcome.IsMove() || next.Drops.Total > maxDrops) continue;
                    if (outcome == MoveOutcome.Cleared)
                    {
                        solution = BuildSolution(nodes, head, direction, next);
                        explored = nodes.Count;
                        return SearchResult.Found;
                    }
                    if (!seen.Add(new Key(next))) continue;

                    if (nodes.Count >= maxStates)
                    {
                        explored = nodes.Count;
                        return SearchResult.LimitExceeded;
                    }
                    nodes.Add(new Node(next, head, direction));
                }
            }
            explored = nodes.Count;
            return SearchResult.Unsolvable;
        }

        static Solution BuildSolution(List<Node> nodes, int parent, Direction last, BoardState final)
        {
            var path = new List<Direction> { last };
            for (int i = parent; nodes[i].Parent >= 0; i = nodes[i].Parent) path.Add(nodes[i].Via);
            path.Reverse();
            return new Solution(path, final);
        }

        readonly struct Node
        {
            public readonly BoardState State;
            public readonly int Parent;
            public readonly Direction Via;

            public Node(BoardState state, int parent, Direction via)
            {
                State = state;
                Parent = parent;
                Via = via;
            }
        }

        /// <summary>Everything that affects future moves; the move count is left out on purpose.</summary>
        readonly struct Key : IEquatable<Key>
        {
            readonly int position;
            readonly ulong eaten;
            readonly DropCounts drops;

            public Key(in BoardState state)
            {
                position = state.Position;
                eaten = state.Eaten;
                drops = state.Drops;
            }

            public bool Equals(Key other) => position == other.position && eaten == other.eaten && drops.Equals(other.drops);
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(position, eaten, drops);
        }
    }
}
