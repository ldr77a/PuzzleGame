using System;
using System.Globalization;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// The day's free hints, shared by all stages, in PlayerPrefs (Phase 2 spec §14): <see cref="DailyHints"/> each
    /// day by the device's local date, and unused ones don't carry over. Once they run out, a hint comes from a
    /// rewarded ad and is used on the spot, so nothing is ever stocked.
    /// </summary>
    static class HintStore
    {
        public const int DailyHints = 3;

        const string CountKey = "cameli.hints";
        const string DayKey = "cameli.hints.day";

        static string Today => DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>Hints left today; a new day (or no record yet) starts at <see cref="DailyHints"/>.</summary>
        public static int Count => PlayerPrefs.GetString(DayKey, "") == Today
            ? Mathf.Clamp(PlayerPrefs.GetInt(CountKey, DailyHints), 0, DailyHints)
            : DailyHints;

        /// <summary>Uses one of today's hints; false (and nothing changes) when none are left.</summary>
        public static bool TrySpend()
        {
            int count = Count;
            if (count == 0) return false;
            PlayerPrefs.SetString(DayKey, Today);
            PlayerPrefs.SetInt(CountKey, count - 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Back to a full day of hints, for the next playtester.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(CountKey);
            PlayerPrefs.DeleteKey(DayKey);
            PlayerPrefs.Save();
        }
    }
}
