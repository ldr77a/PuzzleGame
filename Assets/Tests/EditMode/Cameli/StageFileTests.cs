using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cameli.Core;
using NUnit.Framework;
using UnityEngine;

namespace Cameli.Tests
{
    /// <summary>Checks the shipped stage files: valid, solvable, and scored with the solver's optimum.</summary>
    public class StageFileTests
    {
        static string Folder => Path.Combine(Application.dataPath, "Resources", "CameliStages");

        static IEnumerable<(string name, StageFile file)> StageFiles() =>
            Directory.GetFiles(Folder, "*.json")
                .OrderBy(path => path)
                .Select(path => (Path.GetFileName(path), JsonUtility.FromJson<StageFile>(File.ReadAllText(path))));

        [Test]
        public void EveryStageIsValidSolvableAndScoredCorrectly()
        {
            Assert.IsNotEmpty(StageFiles().ToList(), "no stage files in " + Folder);
            foreach ((string name, StageFile file) in StageFiles())
            {
                StageDefinition stage = null;
                Assert.DoesNotThrow(() => stage = StageDefinition.FromFile(file), name);
                Solution solution = Solver.Solve(stage);
                Assert.IsNotNull(solution, $"{name}: cannot be cleared");
                Assert.AreEqual(solution.Moves, file.optimalMoves, $"{name}: optimalMoves should be {solution.Moves}");
                Assert.AreEqual(stage.Target.Total, solution.Final.Drops.Total,
                    $"{name}: the shortest clear should use exactly the target's drop count shown on screen");
            }
        }

        [Test]
        public void IdsRunFromOneWithoutGaps()
        {
            List<int> ids = StageFiles().Select(s => s.file.id).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, ids.Count).ToList(), ids);
        }
    }
}
