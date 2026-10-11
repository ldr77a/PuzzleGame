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
    /// Breadth-first search over board states using the same <see cref="Rules"/> as the game, so the
    /// first clear found is a shortest one. Meant for tooling and tests, not for runtime use.
    /// </summary>
    public static class Solver
    {
        /// <summary>Returns a shortest clear, or null if the stage cannot be cleared.</summary>
        /// <param name="maxDrops">
        /// States holding more drops than this are pruned. A clear may use a multiple of the target ratio,
        /// so the default allows at least twice the target's drop count.
        /// </param>
        public static Solution Solve(StageDefinition stage, int maxDrops = 0, int maxStates = 2_000_000)
        {
            if (maxDrops <= 0) maxDrops = Math.Max(12, stage.Target.Total * 2);

            BoardState start = BoardState.Initial(stage);
            var nodes = new List<Node> { new Node(start, -1, Direction.Up) };
            var seen = new HashSet<Key> { new Key(start) };

            for (int head = 0; head < nodes.Count; head++)
            {
                BoardState current = nodes[head].State;
                foreach (Direction direction in Directions.All)
                {
                    MoveOutcome outcome = Rules.Step(stage, current, direction, out BoardState next);
                    if (!outcome.IsMove() || next.Drops.Total > maxDrops) continue;
                    if (outcome == MoveOutcome.Cleared) return BuildSolution(nodes, head, direction, next);
                    if (!seen.Add(new Key(next))) continue;

                    if (nodes.Count >= maxStates)
                        throw new InvalidOperationException($"Stage {stage.Id}: search exceeded {maxStates} states.");
                    nodes.Add(new Node(next, head, direction));
                }
            }
            return null;
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
