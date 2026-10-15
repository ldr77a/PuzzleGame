using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cameli.Core;

// Level design helper built on Cameli.Core (same rules as the game).
//   check  <dir>                 analyze every stage JSON in the folder
//   search key=value ...         random-fill a template and list the best candidates
//   solve  key=value ...         print a shortest solution step by step (rows=, target=, gates=)
static class LevelLab
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

    /// <summary>
    /// Ambiguity filter (design doc 3.2): another recipe with the same drop count, made only from the board's
    /// colors, must mix at least this far (CIELAB ΔE76) from the target or gate color.
    /// </summary>
    const double AmbiguityThreshold = 10;

    static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "check") return Check(args[1]);
        if (args.Length > 0 && args[0] == "search") return Search(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0] == "solve") return Solve(args.Skip(1).ToArray());
        Console.WriteLine("usage: check <dir> | search key=value... | solve key=value...");
        return 2;
    }

    sealed class Analysis
    {
        public int Optimal;
        public int OptimalSets;     // distinct eaten-cell sets among optimal clears
        public int NearSets;        // distinct eaten-cell sets among clears within optimal + 2
        public double Ambiguity;    // ΔE76 from target to the closest different recipe with the same drop count
        public string Nearest;
        public double GateAmbiguity = double.MaxValue;  // same, for the worst gate recipe (MaxValue without gates)
        public string GateNearest = "-";                // "gate recipe~nearest competitor"
        public double TileDeltaE;   // smallest ΔE76 between two tile colors on the board
        public double WalkClear;    // chance a random walk (uniform over legal moves) clears within optimal + 2 moves
        public bool GateNeeded;
        public bool Exact;          // every optimal clear uses exactly the target's drop count (the rules guarantee it now; kept as a check)
        public int StartReturns;    // times the found shortest path comes back to the start cell
        public bool BeyondGate;     // every optimal clear ends outside the start region (gates used as doors, not resets)
    }

    static Analysis Analyze(StageFile file)
    {
        StageDefinition stage = StageDefinition.FromFile(file);
        Solution solution = Solver.Solve(stage);
        if (solution == null) return null;

        var result = new Analysis { Optimal = solution.Moves };
        CountClearSets(stage, solution.Moves, out result.OptimalSets, out result.NearSets, out result.Exact, out result.BeyondGate);
        result.Ambiguity = NearestWrong(stage, out result.Nearest);
        result.GateAmbiguity = NearestWrongGate(stage, out result.GateNearest);
        result.TileDeltaE = MinTileDeltaE(stage);
        result.WalkClear = RandomWalkClear(stage, solution.Moves + Scoring.TwoStarSlack);
        result.StartReturns = CountStartReturns(stage, solution);
        result.GateNeeded = stage.Gates.Count > 0 && Solver.Solve(StageDefinition.FromFile(WithoutGates(file))) == null;
        return result;
    }

    /// <summary>True when the solution passes every gate holding exactly the gate's recipe (not a multiple).</summary>
    static bool GatesPassedExactly(StageDefinition stage, Solution solution)
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
    static HashSet<int> StartRegion(StageDefinition stage)
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

    static int CountStartReturns(StageDefinition stage, Solution solution)
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

    static void CountClearSets(StageDefinition stage, int optimal, out int optimalSets, out int nearSets, out bool exact, out bool beyondGate)
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

    /// <summary>Palette indexes of the colors that appear as tiles on the board.</summary>
    static int[] BoardColors(StageDefinition stage)
    {
        var colors = new SortedSet<int>();
        for (int i = 0; i < stage.CellCount; i++)
            if (stage.CellAt(i).Kind == CellKind.Color) colors.Add(stage.CellAt(i).Color);
        return colors.ToArray();
    }

    static double NearestWrong(StageDefinition stage, out string nearest) =>
        NearestWrong(BoardColors(stage), stage.Target, stage.TargetColor, out nearest);

    /// <summary>
    /// Smallest ΔE76 between a recipe's color and any other recipe with the same drop count built from
    /// <paramref name="used"/>. Recipes that still match (±2) are other correct answers, not competitors.
    /// Same method as the 2026-10-11 analysis (design doc 10.1, "방울 수 공개").
    /// </summary>
    static double NearestWrong(int[] used, DropCounts recipe, Rgb color, out string nearest)
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
    static double NearestWrongGate(StageDefinition stage, out string nearest)
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

    static double MinTileDeltaE(StageDefinition stage)
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
    static double RandomWalkClear(StageDefinition stage, int limit)
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

    static double DeltaE(Rgb a, Rgb b)
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

    static StageFile WithoutGates(StageFile file) => new StageFile
    {
        id = file.id,
        rows = file.rows.Select(r => string.Join(" ", r.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.StartsWith("g") ? "#" : t))).ToArray(),
        target = file.target,
        gates = file.gates,
    };

    static int Check(string dir)
    {
        int problems = 0;
        Console.WriteLine($"ambiguity: nearest other same-drop recipe from board colors, CIELAB ΔE76; FAIL below {AmbiguityThreshold}");
        Console.WriteLine("id  size  colors target  stored opt  optSets nearSets  ΔE(nearest)  gateΔE(gate~nearest)  tileΔE walk%  exact gate      title");
        foreach (string path in Directory.GetFiles(dir, "*.json").OrderBy(p => p))
        {
            StageFile file = JsonSerializer.Deserialize<StageFile>(File.ReadAllText(path), Json);
            Analysis a;
            try { a = Analyze(file); }
            catch (StageFormatException e) { Console.WriteLine($"{Path.GetFileName(path)}: {e.Message}"); problems++; continue; }
            if (a == null) { Console.WriteLine($"{file.id,2}  UNSOLVABLE"); problems++; continue; }
            StageDefinition stage = StageDefinition.FromFile(file);
            bool gateExact = GatesPassedExactly(stage, Solver.Solve(stage));
            bool mismatch = a.Optimal != file.optimalMoves || !gateExact;
            if (mismatch || !a.Exact) problems++;
            string gate = file.gates == null || file.gates.Length == 0 ? "-" : !a.GateNeeded ? "SKIPPABLE" : a.BeyondGate ? "door" : "AS-RESET";
            if (gate == "SKIPPABLE" || gate == "AS-RESET") problems++;
            bool targetAmbiguous = a.Ambiguity < AmbiguityThreshold, gateAmbiguous = a.GateAmbiguity < AmbiguityThreshold;
            if (targetAmbiguous || gateAmbiguous) problems++;
            string colors = string.Concat(BoardColors(stage).Select(Palette.Letter));
            string targetDe = $"{a.Ambiguity,5:F1}{(targetAmbiguous ? "!" : " ")}({a.Nearest})";
            string gateDe = stage.Gates.Count == 0 ? "-" : $"{a.GateAmbiguity,5:F1}{(gateAmbiguous ? "!" : " ")}({a.GateNearest})";
            Console.WriteLine($"{file.id,2}  {file.rows.Length}x{file.rows.Length}  {colors,-6} {file.target,-6}  {file.optimalMoves,6} {a.Optimal,3}{(mismatch ? "!" : " ")}  {a.OptimalSets,7} {a.NearSets,8}  {targetDe,-12} {gateDe,-21} {a.TileDeltaE,6:F1} {a.WalkClear * 100,5:F2}  {(a.Exact ? "yes" : "NO"),-5} {gate,-9} {file.title}");
            if (targetAmbiguous) Console.WriteLine($"    AMBIGUOUS target {file.target}: {a.Nearest} is only ΔE {a.Ambiguity:F1} away");
            if (gateAmbiguous) Console.WriteLine($"    AMBIGUOUS gate {a.GateNearest.Replace("~", ": ")} is only ΔE {a.GateAmbiguity:F1} away");
        }
        Console.WriteLine(problems == 0 ? "OK" : $"{problems} problem(s)");
        return problems == 0 ? 0 : 1;
    }

    static int Solve(string[] args)
    {
        var o = args.Select(a => a.Split('=', 2)).ToDictionary(a => a[0], a => a.Length > 1 ? a[1] : "");
        var file = new StageFile
        {
            rows = o["rows"].Split('|'),
            target = o["target"],
            gates = o.TryGetValue("gates", out string g) && g.Length > 0 ? g.Split(',') : null,
        };
        StageDefinition stage = StageDefinition.FromFile(file);
        Solution solution = Solver.Solve(stage);
        if (solution == null) { Console.WriteLine("unsolvable"); return 1; }
        var session = new GameSession(stage);
        foreach (Direction d in solution.Path)
        {
            MoveOutcome outcome = session.Move(d);
            int p = session.State.Position;
            Console.WriteLine($"{d,-5} -> ({p % stage.Width},{p / stage.Width}) {outcome,-10} drops {session.State.Drops}");
        }
        return 0;
    }

    /// <summary>
    /// True when every wall shapes the route: turning it into some tile color from <paramref name="colors"/>
    /// shortens the optimal clear or adds another optimal eaten-cell set (search option wallsmatter).
    /// </summary>
    static bool EveryWallMatters(StageFile file, string colors, Analysis a)
    {
        string[][] grid = file.rows.Select(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        for (int y = 0; y < grid.Length; y++)
        for (int x = 0; x < grid[y].Length; x++)
        {
            if (grid[y][x] != "#") continue;
            bool matters = false;
            foreach (char c in colors)
            {
                grid[y][x] = c.ToString();
                var open = new StageFile { id = 0, rows = grid.Select(r => string.Join(" ", r)).ToArray(), target = file.target, gates = file.gates };
                StageDefinition stage = StageDefinition.FromFile(open);
                Solution s = Solver.Solve(stage);
                if (s != null && s.Moves < a.Optimal) { matters = true; break; }
                if (s != null)
                {
                    CountClearSets(stage, s.Moves, out int sets, out _, out _, out _);
                    if (sets > a.OptimalSets) { matters = true; break; }
                }
            }
            grid[y][x] = "#";
            if (!matters) return false;
        }
        return true;
    }

    static int Search(string[] args)
    {
        var o = args.Select(a => a.Split('=', 2)).ToDictionary(a => a[0], a => a.Length > 1 ? a[1] : "");
        string[] template = o["template"].Split('|');
        string colors = o["colors"];
        string target = o["target"];
        string[] gates = o.TryGetValue("gates", out string g) && g.Length > 0 ? g.Split(',') : null;
        int[] optRange = o.TryGetValue("opt", out string or) ? or.Split('-').Select(int.Parse).ToArray() : new[] { 1, 99 };
        int randomWalls = o.TryGetValue("randwalls", out string rw) ? int.Parse(rw) : 0;
        double minAmbiguity = o.TryGetValue("amb", out string am) ? double.Parse(am) : AmbiguityThreshold;
        int tries = o.TryGetValue("n", out string n) ? int.Parse(n) : 20000;
        int top = o.TryGetValue("top", out string tp) ? int.Parse(tp) : 5;
        bool requireAllColors = !o.ContainsKey("anycolors");
        bool noHub = o.ContainsKey("nohub");
        bool wallsMatter = o.ContainsKey("wallsmatter");
        var rng = new Random(o.TryGetValue("seed", out string sd) ? int.Parse(sd) : 1);

        var found = new Dictionary<string, (Analysis a, string[] rows)>();
        for (int t = 0; t < tries; t++)
        {
            string[][] grid = template.Select(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
            var open = new List<(int x, int y)>();
            for (int y = 0; y < grid.Length; y++)
                for (int x = 0; x < grid[y].Length; x++)
                    if (grid[y][x] == "?") open.Add((x, y));
            foreach (var (x, y) in open.OrderBy(_ => rng.Next()).Take(randomWalls)) grid[y][x] = "#";
            foreach (var (x, y) in open)
                if (grid[y][x] == "?") grid[y][x] = colors[rng.Next(colors.Length)].ToString();

            string[] rows = grid.Select(r => string.Join(" ", r)).ToArray();
            string key = string.Join("|", rows);
            if (found.ContainsKey(key)) continue;
            if (requireAllColors && colors.Any(c => !key.Contains(c))) continue;

            var file = new StageFile { id = 0, rows = rows, target = target, gates = gates };
            StageDefinition quick = StageDefinition.FromFile(file);
            Solution first = Solver.Solve(quick);
            if (first == null || first.Moves < optRange[0] || first.Moves > optRange[1]) continue;
            if (first.Final.Drops.Total != quick.Target.Total) continue;
            if (!GatesPassedExactly(quick, first)) continue;
            if (gates != null && Solver.Solve(StageDefinition.FromFile(WithoutGates(file))) != null) continue;
            if (NearestWrong(quick, out _) < minAmbiguity || NearestWrongGate(quick, out _) < minAmbiguity) continue;
            Analysis a = Analyze(file);
            if (a == null || a.Optimal < optRange[0] || a.Optimal > optRange[1]) continue;
            if (gates != null && (!a.GateNeeded || !a.BeyondGate)) continue;
            if (a.Ambiguity < minAmbiguity || a.GateAmbiguity < minAmbiguity) continue;
            if (!a.Exact) continue;
            if (noHub && a.StartReturns > 0) continue;
            if (wallsMatter && !EveryWallMatters(file, colors, a)) continue;
            found[key] = (a, rows);
        }

        Console.WriteLine($"{found.Count} candidates");
        bool byWalk = o.TryGetValue("sort", out string sort) && sort == "walk";
        IEnumerable<(Analysis a, string[] rows)> ranked = byWalk
            ? found.Values.OrderBy(v => v.a.WalkClear).ThenBy(v => v.a.OptimalSets)
            : found.Values.OrderBy(v => v.a.OptimalSets).ThenBy(v => v.a.NearSets).ThenByDescending(v => v.a.Optimal);
        foreach (var (a, rows) in ranked.Take(top))
        {
            string gateDe = a.GateAmbiguity == double.MaxValue ? "" : $"  gateΔE {a.GateAmbiguity:F1} ({a.GateNearest})";
            Console.WriteLine($"opt {a.Optimal}  optSets {a.OptimalSets}  nearSets {a.NearSets}  ΔE {a.Ambiguity:F1} ({a.Nearest}){gateDe}  tileΔE {a.TileDeltaE:F1}  walk {a.WalkClear * 100:F2}%  startReturns {a.StartReturns}");
            foreach (string row in rows) Console.WriteLine("   " + row);
        }
        return 0;
    }
}
