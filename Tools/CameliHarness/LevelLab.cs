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

    /// <summary>Prints one line per stage from <see cref="StageAudit"/>; the Unity menu "Cameli/스테이지 검증" runs the same checks.</summary>
    static int Check(string dir)
    {
        int problems = 0;
        Console.WriteLine($"ambiguity: nearest other same-drop recipe from board colors, CIELAB ΔE76; FAIL below {StageAudit.AmbiguityThreshold}");
        Console.WriteLine("id  size  colors target  stored opt  optSets nearSets  ΔE(nearest)  gateΔE(gate~nearest)  tileΔE walk%  exact gate      title");
        foreach (string path in Directory.GetFiles(dir, "*.json").OrderBy(p => p))
        {
            StageFile file = JsonSerializer.Deserialize<StageFile>(File.ReadAllText(path), Json);
            StageReport a;
            try { a = StageAudit.Audit(file); }
            catch (StageFormatException e) { Console.WriteLine($"{Path.GetFileName(path)}: {e.Message}"); problems++; continue; }
            if (!a.Solvable) { Console.WriteLine($"{file.id,2}  UNSOLVABLE"); problems++; continue; }
            bool mismatch = a.OptimalMismatch || !a.GatesExact;
            if (mismatch || !a.Exact) problems++;
            string gate = a.GateUse switch
            {
                GateUse.Door => "door",
                GateUse.Skippable => "SKIPPABLE",
                GateUse.AsReset => "AS-RESET",
                _ => "-",
            };
            if (a.GateUse == GateUse.Skippable || a.GateUse == GateUse.AsReset) problems++;
            if (a.TargetAmbiguous || a.GateAmbiguous) problems++;
            string targetDe = $"{a.TargetDeltaE,5:F1}{(a.TargetAmbiguous ? "!" : " ")}({a.TargetNearest})";
            string gateDe = a.GateUse == GateUse.None ? "-" : $"{a.GateDeltaE,5:F1}{(a.GateAmbiguous ? "!" : " ")}({a.GateNearest})";
            Console.WriteLine($"{file.id,2}  {file.rows.Length}x{file.rows.Length}  {a.Colors,-6} {file.target,-6}  {file.optimalMoves,6} {a.Optimal,3}{(mismatch ? "!" : " ")}  {a.OptimalSets,7} {a.NearSets,8}  {targetDe,-12} {gateDe,-21} {a.TileDeltaE,6:F1} {a.WalkClear * 100,5:F2}  {(a.Exact ? "yes" : "NO"),-5} {gate,-9} {file.title}");
            if (a.TargetAmbiguous) Console.WriteLine($"    AMBIGUOUS target {file.target}: {a.TargetNearest} is only ΔE {a.TargetDeltaE:F1} away");
            if (a.GateAmbiguous) Console.WriteLine($"    AMBIGUOUS gate {a.GateNearest.Replace("~", ": ")} is only ΔE {a.GateDeltaE:F1} away");
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
    static bool EveryWallMatters(StageFile file, string colors, StageReport a)
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
                    StageAudit.CountClearSets(stage, s.Moves, out int sets, out _, out _, out _);
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
        double minAmbiguity = o.TryGetValue("amb", out string am) ? double.Parse(am) : StageAudit.AmbiguityThreshold;
        int tries = o.TryGetValue("n", out string n) ? int.Parse(n) : 20000;
        int top = o.TryGetValue("top", out string tp) ? int.Parse(tp) : 5;
        bool requireAllColors = !o.ContainsKey("anycolors");
        bool noHub = o.ContainsKey("nohub");
        bool wallsMatter = o.ContainsKey("wallsmatter");
        var rng = new Random(o.TryGetValue("seed", out string sd) ? int.Parse(sd) : 1);

        var found = new Dictionary<string, (StageReport a, string[] rows)>();
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
            if (!StageAudit.GatesPassedExactly(quick, first)) continue;
            if (gates != null && Solver.Solve(StageDefinition.FromFile(StageAudit.WithoutGates(file))) != null) continue;
            if (StageAudit.NearestWrong(quick, out _) < minAmbiguity || StageAudit.NearestWrongGate(quick, out _) < minAmbiguity) continue;
            StageReport a = StageAudit.Audit(file);
            if (!a.Solvable || a.Optimal < optRange[0] || a.Optimal > optRange[1]) continue;
            if (gates != null && a.GateUse != GateUse.Door) continue;
            if (a.TargetDeltaE < minAmbiguity || a.GateDeltaE < minAmbiguity) continue;
            if (!a.Exact) continue;
            if (noHub && a.StartReturns > 0) continue;
            if (wallsMatter && !EveryWallMatters(file, colors, a)) continue;
            found[key] = (a, rows);
        }

        Console.WriteLine($"{found.Count} candidates");
        bool byWalk = o.TryGetValue("sort", out string sort) && sort == "walk";
        IEnumerable<(StageReport a, string[] rows)> ranked = byWalk
            ? found.Values.OrderBy(v => v.a.WalkClear).ThenBy(v => v.a.OptimalSets)
            : found.Values.OrderBy(v => v.a.OptimalSets).ThenBy(v => v.a.NearSets).ThenByDescending(v => v.a.Optimal);
        foreach (var (a, rows) in ranked.Take(top))
        {
            string gateDe = a.GateDeltaE == double.MaxValue ? "" : $"  gateΔE {a.GateDeltaE:F1} ({a.GateNearest})";
            Console.WriteLine($"opt {a.Optimal}  optSets {a.OptimalSets}  nearSets {a.NearSets}  ΔE {a.TargetDeltaE:F1} ({a.TargetNearest}){gateDe}  tileΔE {a.TileDeltaE:F1}  walk {a.WalkClear * 100:F2}%  startReturns {a.StartReturns}");
            foreach (string row in rows) Console.WriteLine("   " + row);
        }
        return 0;
    }
}
