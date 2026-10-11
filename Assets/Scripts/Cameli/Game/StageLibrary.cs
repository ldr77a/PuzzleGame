using System;
using System.Collections.Generic;
using System.Text;
using Cameli.Core;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>Loads the stage JSON files from Resources/CameliStages, ordered by id.</summary>
    public static class StageLibrary
    {
        public const string ResourceFolder = "CameliStages";

        public static List<StageDefinition> LoadAll()
        {
            var stages = new List<StageDefinition>();
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>(ResourceFolder))
            {
                try
                {
                    stages.Add(StageDefinition.FromFile(JsonUtility.FromJson<StageFile>(asset.text)));
                }
                catch (Exception e) when (e is StageFormatException || e is ArgumentException)
                {
                    Debug.LogError($"Skipping stage file {asset.name}: {e.Message}");
                }
            }
            stages.Sort((a, b) => a.Id.CompareTo(b.Id));
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
