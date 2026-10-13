using System.Collections.Generic;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Minimal prototype progress in PlayerPrefs (unlocked stage, best stars, last stage played) so playtesters
    /// can close the app and continue. The real save system comes in Phase 2.
    /// </summary>
    static class Progress
    {
        const string UnlockedKey = "cameli.proto.unlocked";
        const string LastPlayedKey = "cameli.proto.last";
        const string StarsKeyPrefix = "cameli.proto.stars.";

        /// <summary>Highest stage index the player may open.</summary>
        public static int Unlocked => PlayerPrefs.GetInt(UnlockedKey, 0);

        public static int LastPlayed
        {
            get => PlayerPrefs.GetInt(LastPlayedKey, 0);
            set
            {
                PlayerPrefs.SetInt(LastPlayedKey, value);
                PlayerPrefs.Save();
            }
        }

        public static int BestStars(int stageId) => PlayerPrefs.GetInt(StarsKeyPrefix + stageId, 0);

        public static void RecordClear(int stageIndex, int stageId, int stars)
        {
            if (stageIndex + 1 > Unlocked) PlayerPrefs.SetInt(UnlockedKey, stageIndex + 1);
            if (stars > BestStars(stageId)) PlayerPrefs.SetInt(StarsKeyPrefix + stageId, stars);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets unlocks, stars, and the last stage played, for handing the device to the next playtester.</summary>
        public static void ResetAll(IEnumerable<int> stageIds)
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(LastPlayedKey);
            foreach (int id in stageIds) PlayerPrefs.DeleteKey(StarsKeyPrefix + id);
            PlayerPrefs.Save();
        }
    }
}
