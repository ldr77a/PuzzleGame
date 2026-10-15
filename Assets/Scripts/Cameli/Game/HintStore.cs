using System.Collections.Generic;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Hint inventory shared by all stages, and which stages have their recipe revealed (Phase 2 spec §6), in
    /// PlayerPrefs. The first hint used on a stage reveals its recipe for good; later ones show the next step.
    /// </summary>
    static class HintStore
    {
        public const int StartingHints = 3;

        const string CountKey = "cameli.hints";
        const string RecipeKeyPrefix = "cameli.recipe.";

        public static int Count => Mathf.Max(0, PlayerPrefs.GetInt(CountKey, StartingHints));

        /// <summary>Adds hints (the reward for watching an ad once they run out).</summary>
        public static void Add(int count)
        {
            if (count <= 0) return;
            PlayerPrefs.SetInt(CountKey, Count + count);
            PlayerPrefs.Save();
        }

        /// <summary>Uses one hint; false (and nothing changes) when none are left.</summary>
        public static bool TrySpend()
        {
            int count = Count;
            if (count == 0) return false;
            PlayerPrefs.SetInt(CountKey, count - 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool IsRecipeRevealed(int stageId) => PlayerPrefs.GetInt(RecipeKeyPrefix + stageId, 0) != 0;

        public static void RevealRecipe(int stageId)
        {
            PlayerPrefs.SetInt(RecipeKeyPrefix + stageId, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Back to <see cref="StartingHints"/> hints and no revealed recipes, for the next playtester.</summary>
        public static void ResetAll(IEnumerable<int> stageIds)
        {
            PlayerPrefs.DeleteKey(CountKey);
            foreach (int id in stageIds) PlayerPrefs.DeleteKey(RecipeKeyPrefix + id);
            PlayerPrefs.Save();
        }
    }
}
