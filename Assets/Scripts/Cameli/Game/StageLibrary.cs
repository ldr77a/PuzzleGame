using System;
using System.Collections.Generic;
using System.Text;
using Cameli.Core;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Loads the stage JSON files from the difficulty folders under Resources/CameliStages
    /// (<see cref="StageSections"/>), ordered by folder and then by id. Files outside those folders are not loaded.
    /// </summary>
    public static class StageLibrary
    {
        public const string ResourceFolder = "CameliStages";

        public static List<StageDefinition> LoadAll()
        {
            var stages = new List<StageDefinition>();
            for (int section = 0; section < StageSections.All.Count; section++)
            {
                string folder = $"{ResourceFolder}/{StageSections.All[section].Folder}";
                foreach (TextAsset asset in Resources.LoadAll<TextAsset>(folder))
                {
                    try
                    {
                        stages.Add(StageDefinition.FromFile(JsonUtility.FromJson<StageFile>(asset.text), section));
                    }
                    catch (Exception e) when (e is StageFormatException || e is ArgumentException)
                    {
                        Debug.LogError($"Skipping stage file {folder}/{asset.name}: {e.Message}");
                    }
                }
            }
            // The audit keeps folder order and id order the same; sorting by folder first keeps each section in one
            // block in the garden even if they ever disagree.
            stages.Sort((a, b) => a.Section != b.Section ? a.Section.CompareTo(b.Section) : a.Id.CompareTo(b.Id));
            return stages;
        }
    }

    /// <summary>Korean wording shared by the HUD and messages.</summary>
    static class Words
    {
        /// <summary>"빨강 1 + 노랑 1" style description of a recipe.</summary>
        public static string Recipe(DropCounts drops)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Palette.Count; i++)
            {
                if (drops[i] == 0) continue;
                if (builder.Length > 0) builder.Append(" + ");
                builder.Append(Palette.Name(i)).Append(' ').Append(drops[i]);
            }
            return builder.ToString();
        }
    }
}
