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
        public bool GateNeeded;
        public bool Exact;          // every optimal clear uses exactly the target's drop count
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
        int maxDrops = Math.Max(12, stage.Target.Total * 2);
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

    static double NearestWrong(StageDefinition stage, out string nearest)
    {
        var colors = new SortedSet<int>();
        for (int i = 0; i < stage.CellCount; i++)
            if (stage.CellAt(i).Kind == CellKind.Color) colors.Add(stage.CellAt(i).Color);
        int[] used = colors.ToArray();
        int total = stage.Target.Total;
        double best = double.MaxValue;
        nearest = "-";
        foreach (int[] counts in Compositions(used.Length, total))
        {
            DropCounts drops = DropCounts.Empty;
            for (int k = 0; k < used.Length; k++)
                for (int n = 0; n < counts[k]; n++) drops = drops.Add(used[k]);
            Rgb mix = ColorMath.Mix(drops);
            if (ColorMath.Matches(mix, stage.TargetColor)) continue;
            double de = DeltaE(mix, stage.TargetColor);
            if (de < best) { best = de; nearest = drops.ToString(); }
        }
        return best;
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
        Console.WriteLine("id  size  target  stored opt  optSets nearSets  ΔE(nearest)  exact gate      title");
        foreach (string path in Directory.GetFiles(dir, "*.json").OrderBy(p => p))
        {
            StageFile file = JsonSerializer.Deserialize<StageFile>(File.ReadAllText(path), Json);
            Analysis a;
            try { a = Analyze(file); }
            catch (StageFormatException e) { Console.WriteLine($"{Path.GetFileName(path)}: {e.Message}"); problems++; continue; }
            if (a == null) { Console.WriteLine($"{file.id,2}  UNSOLVABLE"); problems++; continue; }
            bool gateExact = GatesPassedExactly(StageDefinition.FromFile(file), Solver.Solve(StageDefinition.FromFile(file)));
            bool mismatch = a.Optimal != file.optimalMoves || !gateExact;
            if (mismatch || !a.Exact) problems++;
            string gate = file.gates == null || file.gates.Length == 0 ? "-" : !a.GateNeeded ? "SKIPPABLE" : a.BeyondGate ? "door" : "AS-RESET";
            if (gate == "SKIPPABLE" || gate == "AS-RESET") problems++;
            Console.WriteLine($"{file.id,2}  {file.rows.Length}x{file.rows.Length}  {file.target,-6}  {file.optimalMoves,6} {a.Optimal,3}{(mismatch ? "!" : " ")}  {a.OptimalSets,7} {a.NearSets,8}  {a.Ambiguity,5:F1} ({a.Nearest,-5})  {(a.Exact ? "yes" : "NO"),-5} {gate,-9} {file.title}");
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

    static int Search(string[] args)
    {
        var o = args.Select(a => a.Split('=', 2)).ToDictionary(a => a[0], a => a.Length > 1 ? a[1] : "");
        string[] template = o["template"].Split('|');
        string colors = o["colors"];
        string target = o["target"];
        string[] gates = o.TryGetValue("gates", out string g) && g.Length > 0 ? g.Split(',') : null;
        int[] optRange = o.TryGetValue("opt", out string or) ? or.Split('-').Select(int.Parse).ToArray() : new[] { 1, 99 };
        int randomWalls = o.TryGetValue("randwalls", out string rw) ? int.Parse(rw) : 0;
        double minAmbiguity = o.TryGetValue("amb", out string am) ? double.Parse(am) : 8;
        int tries = o.TryGetValue("n", out string n) ? int.Parse(n) : 20000;
        int top = o.TryGetValue("top", out string tp) ? int.Parse(tp) : 5;
        bool requireAllColors = !o.ContainsKey("anycolors");
        bool noHub = o.ContainsKey("nohub");
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
            if (NearestWrong(quick, out _) < minAmbiguity) continue;
            Analysis a = Analyze(file);
            if (a == null || a.Optimal < optRange[0] || a.Optimal > optRange[1]) continue;
            if (gates != null && (!a.GateNeeded || !a.BeyondGate)) continue;
            if (a.Ambiguity < minAmbiguity) continue;
            if (!a.Exact) continue;
            if (noHub && a.StartReturns > 0) continue;
            found[key] = (a, rows);
        }

        Console.WriteLine($"{found.Count} candidates");
        foreach (var (a, rows) in found.Values
                     .OrderBy(v => v.a.OptimalSets)
                     .ThenBy(v => v.a.NearSets)
                     .ThenByDescending(v => v.a.Optimal)
                     .Take(top))
        {
            Console.WriteLine($"opt {a.Optimal}  optSets {a.OptimalSets}  nearSets {a.NearSets}  ΔE {a.Ambiguity:F1} ({a.Nearest})  startReturns {a.StartReturns}");
            foreach (string row in rows) Console.WriteLine("   " + row);
        }
        return 0;
    }
}
