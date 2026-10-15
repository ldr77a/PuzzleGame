using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cameli.Core;
using NUnit.Framework;
using UnityEngine;

namespace Cameli.Tests
{
    /// <summary>
    /// Checks the shipped stage files: valid, solvable, scored with the solver's optimum, and filed in the difficulty
    /// folders with ids running 1..n in folder order.
    /// </summary>
    public class StageFileTests
    {
        static string Folder => Path.Combine(Application.dataPath, "Resources", "CameliStages");

        /// <summary>The first folder under <see cref="Folder"/>, or "" for a file directly in it.</summary>
        static string FolderOf(string path)
        {
            string[] parts = Path.GetRelativePath(Folder, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length < 2 ? "" : parts[0];
        }

        static IEnumerable<(string name, string folder, StageFile file)> StageFiles() =>
            Directory.GetFiles(Folder, "*.json", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .Select(path => (Path.GetFileName(path), FolderOf(path), JsonUtility.FromJson<StageFile>(File.ReadAllText(path))));

        [Test]
        public void EveryStageIsValidSolvableAndScoredCorrectly()
        {
            Assert.IsNotEmpty(StageFiles().ToList(), "no stage files in " + Folder);
            foreach ((string name, _, StageFile file) in StageFiles())
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

        [Test]
        public void EveryStageIsInADifficultyFolderInIdOrder()
        {
            var stages = StageFiles().ToList();
            foreach ((string name, string folder, _) in stages)
                Assert.AreNotEqual(-1, StageSections.IndexOf(folder), $"{name} is not in a difficulty folder, so the game won't load it");
            var order = StageSections.FirstOutOfOrder(stages.Select(s => (s.file.id, StageSections.IndexOf(s.folder))));
            Assert.IsNull(order, $"ids must follow the folder order: {order}");
        }
    }
}
