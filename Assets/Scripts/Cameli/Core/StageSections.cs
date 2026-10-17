using System.Collections.Generic;
using System.Linq;

namespace Cameli.Core
{
    /// <summary>One difficulty folder under Resources/CameliStages and its garden heading.</summary>
    public sealed class StageSection
    {
        public string Folder { get; }
        public string Name { get; }

        public StageSection(string folder, string name)
        {
            Folder = folder;
            Name = name;
        }
    }

    /// <summary>
    /// The difficulty folders under Resources/CameliStages, in play order (design doc §5: 5 tutorial, 40 Easy,
    /// 50 Medium, 50 Hard). A stage's folder is its garden section. Ids still run 1..n across all folders, and
    /// every folder's ids come after the previous folder's. Unity can't list Resources folders at runtime, so a
    /// new folder has to be added here as well.
    /// </summary>
    public static class StageSections
    {
        /// <summary>Index in <see cref="All"/> of the tutorial folder, 0_Tutorial.</summary>
        public const int Tutorial = 0;

        public static readonly IReadOnlyList<StageSection> All = new[]
        {
            new StageSection("0_Tutorial", "튜토리얼"),
            new StageSection("1_Easy", "Easy"),
            new StageSection("2_Medium", "Medium"),
            new StageSection("3_Hard", "Hard"),
        };

        /// <summary>Index in <see cref="All"/> of the folder with this name, or -1.</summary>
        public static int IndexOf(string folder)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Folder == folder) return i;
            return -1;
        }

        /// <summary>"0_Tutorial, 1_Easy, …" for messages.</summary>
        public static string FolderList => string.Join(", ", All.Select(s => s.Folder));

        /// <summary>
        /// Sorted by id, the first stage filed under an earlier section than the stage before it (for example id 12
        /// in 1_Easy after id 11 in 2_Medium), or null when the sections follow the ids. Leave out stages outside
        /// every section folder.
        /// </summary>
        public static (int id, int section, int previousId, int previousSection)? FirstOutOfOrder(
            IEnumerable<(int id, int section)> stages)
        {
            int previousId = 0, previousSection = int.MinValue;
            foreach ((int id, int section) in stages.OrderBy(s => s.id))
            {
                if (section < previousSection) return (id, section, previousId, previousSection);
                previousId = id;
                previousSection = section;
            }
            return null;
        }
    }
}
