using System;
using System.Collections.Generic;

namespace Cameli.Core
{
    /// <summary>How a stage with gates uses them, judged from its shortest clears.</summary>
    public enum GateUse
    {
        /// <summary>The stage has no gates.</summary>
        None,

        /// <summary>The stage cannot be cleared without passing a gate, and every shortest clear ends beyond one.</summary>
        Door,

        /// <summary>The stage can be cleared with every gate turned into a wall.</summary>
        Skippable,

        /// <summary>A shortest clear ends on the start side: the gate only empties the mix, like the removed reset tile.</summary>
        AsReset,
    }

    /// <summary>Problems that fail a stage in <see cref="StageAudit"/>. Several can be set at once.</summary>
    [Flags]
    public enum StageProblems
    {
        None = 0,
        Unsolvable = 1 << 0,

        /// <summary>The stored optimalMoves differs from the solver's shortest clear.</summary>
        OptimalMismatch = 1 << 1,

        /// <summary>A shortest clear ends with a drop count other than the target's (the rules prevent it; kept as a check).</summary>
        NotExact = 1 << 2,

        /// <summary>The shortest clear passes a gate holding something other than the gate's exact recipe.</summary>
        GateNotExact = 1 << 3,

        GateSkippable = 1 << 4,
        GateAsReset = 1 << 5,

        /// <summary>Another recipe with the target's drop count, from the board's colors, mixes too close to the target.</summary>
        TargetAmbiguous = 1 << 6,

        /// <summary>The same as <see cref="TargetAmbiguous"/> for a gate recipe.</summary>
        GateAmbiguous = 1 << 7,
    }

    /// <summary>Everything <see cref="StageAudit.Audit"/> found out about one stage.</summary>
    public sealed class StageReport
    {
        public int Id;
        public string Title;
        public int Width;
        public int Height;

        /// <summary>Letters of the colors that appear as tiles, in palette order, e.g. "RYB".</summary>
        public string Colors;

        public string Target;
        public int StoredOptimal;

        public bool Solvable;

        /// <summary>The solver's fewest moves; -1 when unsolvable. The fields below are only filled for solvable stages.</summary>
        public int Optimal = -1;

        /// <summary>Distinct sets of eaten cells among the shortest clears.</summary>
        public int OptimalSets;

        /// <summary>Distinct sets of eaten cells among the clears within optimal + 2 (two stars).</summary>
        public int NearSets;

        /// <summary>Every shortest clear uses exactly the target's drop count.</summary>
        public bool Exact;

        /// <summary>The shortest clear passes every gate with exactly the gate's recipe.</summary>
        public bool GatesExact;

        public GateUse GateUse;

        /// <summary>ΔE76 from the target to the closest other recipe with the same drop count from the board's colors.</summary>
        public double TargetDeltaE = double.MaxValue;
        public string TargetNearest = "-";

        /// <summary>The same for the worst gate; MaxValue without gates.</summary>
        public double GateDeltaE = double.MaxValue;

        /// <summary>"gate recipe~nearest competitor" for the worst gate, "-" without gates.</summary>
        public string GateNearest = "-";

        /// <summary>Smallest ΔE76 between two tile colors on the board.</summary>
        public double TileDeltaE = double.MaxValue;

        /// <summary>Chance that a random walk (uniform over legal moves) clears within optimal + 2 moves.</summary>
        public double WalkClear;

        /// <summary>Times the found shortest path comes back to the start cell.</summary>
        public int StartReturns;

        public bool OptimalMismatch => Solvable && Optimal != StoredOptimal;
        public bool TargetAmbiguous => TargetDeltaE < StageAudit.AmbiguityThreshold;
        public bool GateAmbiguous => GateDeltaE < StageAudit.AmbiguityThreshold;

        public StageProblems Problems
        {
            get
            {
                if (!Solvable) return StageProblems.Unsolvable;
                StageProblems problems = StageProblems.None;
                if (OptimalMismatch) problems |= StageProblems.OptimalMismatch;
                if (!Exact) problems |= StageProblems.NotExact;
                if (!GatesExact) problems |= StageProblems.GateNotExact;
                if (GateUse == GateUse.Skippable) problems |= StageProblems.GateSkippable;
                if (GateUse == GateUse.AsReset) problems |= StageProblems.GateAsReset;
                if (TargetAmbiguous) problems |= StageProblems.TargetAmbiguous;
                if (GateAmbiguous) problems |= StageProblems.GateAmbiguous;
                return problems;
            }
        }
    }

    /// <summary>
    /// The stage checks shared by <c>lab.sh check</c> (LevelLab) and the Unity menu "Cameli/스테이지 검증": solvable,
    /// stored vs actual optimalMoves, gates used as doors and passed with their exact recipe, and the ambiguity
    /// filter, plus informational numbers. Tooling only; the solver searches can take a while on big boards.
    /// </summary>
    public static class StageAudit
    {
        /// <summary>
        /// Ambiguity filter (design doc 3.2): another recipe with the same drop count, made only from the board's
        /// colors, must mix at least this far (CIELAB ΔE76) from the target or gate color.
        /// </summary>
        public const double AmbiguityThreshold = 10;

        /// <summary>Runs every check on <paramref name="file"/>.</summary>
        /// <exception cref="StageFormatException">The file is not a valid stage.</exception>
        public static StageReport Audit(StageFile file)
        {
            StageDefinition stage = StageDefinition.FromFile(file);
            var report = new StageReport
            {
                Id = file.id,
                Title = file.title,
                Width = stage.Width,
                Height = stage.Height,
                Colors = ColorLetters(stage),
                Target = file.target,
                StoredOptimal = file.optimalMoves,
            };

            Solution solution = Solver.Solve(stage);
            if (solution == null) return report;

            report.Solvable = true;
            report.Optimal = solution.Moves;
            CountClearSets(stage, solution.Moves, out report.OptimalSets, out report.NearSets, out report.Exact, out bool beyondGate);
            report.TargetDeltaE = NearestWrong(stage, out report.TargetNearest);
            report.GateDeltaE = NearestWrongGate(stage, out report.GateNearest);
            report.TileDeltaE = MinTileDeltaE(stage);
            report.WalkClear = RandomWalkClear(stage, solution.Moves + Scoring.TwoStarSlack);
            report.StartReturns = CountStartReturns(stage, solution);
            report.GatesExact = GatesPassedExactly(stage, solution);
            if (stage.Gates.Count == 0) report.GateUse = GateUse.None;
            else if (Solver.Solve(StageDefinition.FromFile(WithoutGates(file))) != null) report.GateUse = GateUse.Skippable;
            else report.GateUse = beyondGate ? GateUse.Door : GateUse.AsReset;
            return report;
        }

        /// <summary>True when the solution passes every gate holding exactly the gate's recipe (not a multiple).</summary>
        public static bool GatesPassedExactly(StageDefinition stage, Solution solution)
        {
            var session = new GameSession(stage);
            foreach (Direction d in solution.Path)
            {
                DropCounts before = session.State.Drops;
                if (session.Move(d) != MoveOutcome.PassedGate) continue;
                int gate = stage.CellAt(session.State.Position).Gate;
                if (!before.Equals(stage.Gates[gate].Recipe)) return false;
            }
            return true;
        }

        /// <summary>Cells reachable from the start without passing a gate.</summary>
        public static HashSet<int> StartRegion(StageDefinition stage)
        {
            var region = new HashSet<int> { stage.StartIndex };
            var queue = new Queue<int>(region);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                foreach (Direction d in Directions.All)
                {
                    int x = i % stage.Width + d.Dx(), y = i / stage.Width + d.Dy();
                    if (x < 0 || x >= stage.Width || y < 0 || y >= stage.Height) continue;
                    int j = stage.IndexOf(x, y);
                    CellKind kind = stage.CellAt(j).Kind;
                    if (kind == CellKind.Wall || kind == CellKind.Gate || !region.Add(j)) continue;
                    queue.Enqueue(j);
                }
            }
            return region;
        }

        public static int CountStartReturns(StageDefinition stage, Solution solution)
        {
            var session = new GameSession(stage);
            int returns = 0;
            foreach (Direction d in solution.Path)
            {
                session.Move(d);
                if (session.State.Position == stage.StartIndex) returns++;
            }
            return returns;
        }

        /// <summary>
        /// Walks every clear within <paramref name="optimal"/> + 2 moves. <paramref name="exact"/>: every shortest
        /// clear uses the target's drop count; <paramref name="beyondGate"/>: every shortest clear ends outside
        /// <see cref="StartRegion"/>.
        /// </summary>
        public static void CountClearSets(StageDefinition stage, int optimal, out int optimalSets, out int nearSets,
            out bool exact, out bool beyondGate)
        {
            exact = true;
            beyondGate = true;
            HashSet<int> startRegion = StartRegion(stage);
            int maxDrops = stage.MaxRecipeDrops;
            var atOptimal = new HashSet<ulong>();
            var near = new HashSet<ulong>();
            BoardState start = BoardState.Initial(stage);
            var seen = new HashSet<(int, ulong, DropCounts)> { (start.Position, start.Eaten, start.Drops) };
            var frontier = new List<BoardState> { start };
            for (int depth = 1; depth <= optimal + Scoring.TwoStarSlack && frontier.Count > 0; depth++)
            {
                var next = new List<BoardState>();
                foreach (BoardState state in frontier)
                foreach (Direction d in Directions.All)
                {
                    MoveOutcome o = Rules.Step(stage, state, d, out BoardState n);
                    if (!o.IsMove() || n.Drops.Total > maxDrops) continue;
                    if (o == MoveOutcome.Cleared)
                    {
                        near.Add(n.Eaten);
                        if (depth == optimal)
                        {
                            atOptimal.Add(n.Eaten);
                            if (n.Drops.Total != stage.Target.Total) exact = false;
                            if (startRegion.Contains(n.Position)) beyondGate = false;
                        }
                        continue;
                    }
                    if (seen.Add((n.Position, n.Eaten, n.Drops))) next.Add(n);
                }
                frontier = next;
            }
            optimalSets = atOptimal.Count;
            nearSets = near.Count;
        }

        /// <summary>Palette indexes of the colors that appear as tiles on the board, in palette order.</summary>
        public static int[] BoardColors(StageDefinition stage)
        {
            var colors = new SortedSet<int>();
            for (int i = 0; i < stage.CellCount; i++)
                if (stage.CellAt(i).Kind == CellKind.Color) colors.Add(stage.CellAt(i).Color);
            var result = new int[colors.Count];
            colors.CopyTo(result);
            return result;
        }

        static string ColorLetters(StageDefinition stage)
        {
            int[] colors = BoardColors(stage);
            var letters = new char[colors.Length];
            for (int i = 0; i < colors.Length; i++) letters[i] = Palette.Letter(colors[i]);
            return new string(letters);
        }

        public static double NearestWrong(StageDefinition stage, out string nearest) =>
            NearestWrong(BoardColors(stage), stage.Target, stage.TargetColor, out nearest);

        /// <summary>
        /// Smallest ΔE76 between a recipe's color and any other recipe with the same drop count built from
        /// <paramref name="used"/>. Recipes that still match (±2) are other correct answers, not competitors.
        /// Same method as the 2026-10-11 analysis (design doc 10.1, "방울 수 공개").
        /// </summary>
        public static double NearestWrong(int[] used, DropCounts recipe, Rgb color, out string nearest)
        {
            int total = recipe.Total;
            double best = double.MaxValue;
            nearest = "-";
            foreach (int[] counts in Compositions(used.Length, total))
            {
                DropCounts drops = DropCounts.Empty;
                for (int k = 0; k < used.Length; k++)
                    for (int n = 0; n < counts[k]; n++) drops = drops.Add(used[k]);
                Rgb mix = ColorMath.Mix(drops);
                if (ColorMath.Matches(mix, color)) continue;
                double de = DeltaE(mix, color);
                if (de < best) { best = de; nearest = drops.ToString(); }
            }
            return best;
        }

        /// <summary>The worst gate's nearest competitor, or MaxValue when the stage has no gates.</summary>
        public static double NearestWrongGate(StageDefinition stage, out string nearest)
        {
            int[] used = BoardColors(stage);
            double best = double.MaxValue;
            nearest = "-";
            foreach (GateDefinition gate in stage.Gates)
            {
                double de = NearestWrong(used, gate.Recipe, gate.Color, out string n);
                if (de < best) { best = de; nearest = $"{gate.Recipe}~{n}"; }
            }
            return best;
        }

        public static double MinTileDeltaE(StageDefinition stage)
        {
            int[] used = BoardColors(stage);
            double best = double.MaxValue;
            for (int i = 0; i < used.Length; i++)
                for (int j = i + 1; j < used.Length; j++)
                    best = Math.Min(best, DeltaE(Palette.Color(used[i]), Palette.Color(used[j])));
            return best;
        }

        /// <summary>
        /// Probability that a player who picks uniformly among the legal moves at every step clears within
        /// <paramref name="limit"/> moves. Lower means the board needs more thought (design doc 10.2).
        /// </summary>
        public static double RandomWalkClear(StageDefinition stage, int limit)
        {
            int maxDrops = stage.MaxRecipeDrops;
            double cleared = 0;
            var current = new Dictionary<(int, ulong, DropCounts), (BoardState state, double p)>();
            BoardState start = BoardState.Initial(stage);
            current[(start.Position, start.Eaten, start.Drops)] = (start, 1.0);
            var moves = new List<BoardState>(4);
            for (int step = 0; step < limit && current.Count > 0; step++)
            {
                var next = new Dictionary<(int, ulong, DropCounts), (BoardState state, double p)>();
                foreach (var (state, p) in current.Values)
                {
                    moves.Clear();
                    foreach (Direction d in Directions.All)
                        if (Rules.Step(stage, state, d, out BoardState n).IsMove()) moves.Add(n);
                    foreach (BoardState n in moves)
                    {
                        double q = p / moves.Count;
                        if (n.Cleared) { cleared += q; continue; }
                        if (n.Drops.Total > maxDrops) continue;
                        var key = (n.Position, n.Eaten, n.Drops);
                        next[key] = next.TryGetValue(key, out var e) ? (e.state, e.p + q) : (n, q);
                    }
                }
                current = next;
            }
            return cleared;
        }

        static IEnumerable<int[]> Compositions(int parts, int total)
        {
            var current = new int[parts];
            IEnumerable<int[]> Rec(int index, int left)
            {
                if (index == parts - 1)
                {
                    current[index] = left;
                    yield return (int[])current.Clone();
                    yield break;
                }
                for (int v = 0; v <= left; v++)
                {
                    current[index] = v;
                    foreach (int[] c in Rec(index + 1, left - v)) yield return c;
                }
            }
            return Rec(0, total);
        }

        /// <summary>CIE76 color difference: straight-line distance in CIELAB (D65). About 2.3 is just noticeable.</summary>
        public static double DeltaE(Rgb a, Rgb b)
        {
            double[] la = Lab(a), lb = Lab(b);
            return Math.Sqrt(Math.Pow(la[0] - lb[0], 2) + Math.Pow(la[1] - lb[1], 2) + Math.Pow(la[2] - lb[2], 2));
        }

        static double[] Lab(Rgb c)
        {
            double Lin(byte v) { double x = v / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            double r = Lin(c.R), g = Lin(c.G), b = Lin(c.B);
            double X = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
            double Y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            double Z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
            double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3) : 7.787 * t + 16.0 / 116;
            return new[] { 116 * F(Y) - 16, 500 * (F(X) - F(Y)), 200 * (F(Y) - F(Z)) };
        }

        /// <summary>A copy of <paramref name="file"/> with every gate cell turned into a wall.</summary>
        public static StageFile WithoutGates(StageFile file)
        {
            var rows = new string[file.rows.Length];
            for (int y = 0; y < rows.Length; y++)
            {
                string[] tokens = file.rows[y].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < tokens.Length; x++)
                    if (tokens[x][0] == 'g') tokens[x] = "#";
                rows[y] = string.Join(" ", tokens);
            }
            return new StageFile { id = file.id, rows = rows, target = file.target, gates = file.gates };
        }
    }
}
