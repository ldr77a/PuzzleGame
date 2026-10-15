using System.Collections.Generic;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Minimal prototype progress in PlayerPrefs (unlocked stage, best stars, last stage played) so playtesters
    /// can close the app and continue. The real save system comes in Phase 2.
    /// </summary>
    public static class Progress
    {
        const string UnlockedKey = "cameli.proto.unlocked";
        const string DevUnlockAllKey = "cameli.dev.unlockAll";
        const string LastPlayedKey = "cameli.proto.last";
        const string StarsKeyPrefix = "cameli.proto.stars.";

        /// <summary>Highest stage index the player may open; every stage while <see cref="DevUnlockAll"/> is on.</summary>
        public static int Unlocked => DevUnlockAll ? int.MaxValue : SavedUnlocked;

        static int SavedUnlocked => PlayerPrefs.GetInt(UnlockedKey, 0);

        /// <summary>
        /// Developer switch "모든 스테이지 열기" (menu "Cameli/개발: 모든 스테이지 열기"): every stage counts as unlocked
        /// while it is on. Only honored in the editor and development builds, so a release build ignores a stray flag.
        /// The saved unlock progress is kept, and only clears of stages it already opened move it forward (see
        /// <see cref="RecordClear"/>), so turning the switch off leaves exactly the stages earned by playing.
        /// </summary>
        public static bool DevUnlockAll
        {
            get => (Application.isEditor || Debug.isDebugBuild) && PlayerPrefs.GetInt(DevUnlockAllKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(DevUnlockAllKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

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

        /// <summary>
        /// Opens the next stage only when <paramref name="stageIndex"/> was opened by saved progress, so a stage played
        /// through <see cref="DevUnlockAll"/> doesn't unlock everything before it. Best stars are recorded either way.
        /// </summary>
        public static void RecordClear(int stageIndex, int stageId, int stars)
        {
            if (stageIndex == SavedUnlocked) PlayerPrefs.SetInt(UnlockedKey, stageIndex + 1);
            if (stars > BestStars(stageId)) PlayerPrefs.SetInt(StarsKeyPrefix + stageId, stars);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Forgets unlocks, stars, the last stage played, the hint inventory, and revealed recipes
        /// (<see cref="HintStore"/>), for handing the device to the next playtester.
        /// </summary>
        public static void ResetAll(IEnumerable<int> stageIds)
        {
            var ids = new List<int>(stageIds);
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(LastPlayedKey);
            foreach (int id in ids) PlayerPrefs.DeleteKey(StarsKeyPrefix + id);
            HintStore.ResetAll(ids);
            PlayerPrefs.Save();
        }
    }
}
