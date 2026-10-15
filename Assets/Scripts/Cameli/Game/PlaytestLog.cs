using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Phase 1 playtest record: one CSV row per stage attempt in persistentDataPath/playtest.csv, summarized per
    /// tester for the playtest panel. Testers share one device; "다음 테스터" advances the tester number, which
    /// survives progress resets.
    /// An attempt starts when a stage opens (or is replayed after a clear) and ends on a clear, on leaving the
    /// stage, on a progress reset, or when the app is backgrounded or quits. Restarts in between belong to the
    /// same attempt. Unfinished attempts with no moves, undos, or restarts are not written.
    /// </summary>
    sealed class PlaytestLog
    {
        public const string ResultClear = "clear";
        public const string ResultLeft = "left";
        public const string ResultReset = "reset";
        public const string ResultPause = "pause";
        public const string ResultQuit = "quit";

        /// <summary>Stage ids 1..this are the tutorial (design doc §5).</summary>
        public const int TutorialStages = 5;

        const string TesterKey = "cameli.proto.tester";
        const string TesterUsedKey = "cameli.proto.tester.used";
        const string Header = "tester,stage,result,moves,undos,restarts,seconds,stars,time";

        int stageId;
        int startMoves;
        float startedAt;
        int undos;
        int restarts;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "playtest.csv");
        public static int Tester => PlayerPrefs.GetInt(TesterKey, 1);

        public bool IsOpen { get; private set; }

        /// <summary>Advances the tester number, unless the current tester has no rows yet.</summary>
        public static void NextTester()
        {
            if (PlayerPrefs.GetInt(TesterUsedKey, 0) == 0) return;
            PlayerPrefs.SetInt(TesterKey, Tester + 1);
            PlayerPrefs.SetInt(TesterUsedKey, 0);
            PlayerPrefs.Save();
        }

        /// <param name="movesSoFar">Moves already on the board, when an attempt continues after the app was paused.</param>
        public void Begin(int id, int movesSoFar = 0)
        {
            stageId = id;
            startMoves = movesSoFar;
            startedAt = Time.realtimeSinceStartup;
            undos = 0;
            restarts = 0;
            IsOpen = true;
        }

        public void CountUndo()
        {
            if (IsOpen) undos++;
        }

        public void CountRestart()
        {
            if (IsOpen) restarts++;
        }

        public void End(string result, int moves, int stars)
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (result != ResultClear && moves == startMoves && undos == 0 && restarts == 0) return; // nothing played
            int seconds = Mathf.RoundToInt(Time.realtimeSinceStartup - startedAt);
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string row = string.Join(",", Tester, stageId, result, moves, undos, restarts, seconds, stars, time);
            try
            {
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, Header + "\n");
                File.AppendAllText(FilePath, row + "\n");
                PlayerPrefs.SetInt(TesterUsedKey, 1);
                PlayerPrefs.Save();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"플레이테스트 기록을 저장하지 못했어요: {e.Message}");
            }
        }

        /// <summary>Per tester: stages cleared, tutorial cleared, undos, restarts, and minutes played.</summary>
        public static string Summary(int stageCount)
        {
            var testers = new SortedDictionary<int, Totals>();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        string[] f = line.Split(',');
                        if (f.Length < 9 || !int.TryParse(f[0], out int tester)) continue; // header or a broken row
                        if (!testers.TryGetValue(tester, out Totals t)) testers[tester] = t = new Totals();
                        if (f[2] == ResultClear && int.TryParse(f[1], out int stage)) t.Cleared.Add(stage);
                        t.Undos += ParseOrZero(f[4]);
                        t.Restarts += ParseOrZero(f[5]);
                        t.Seconds += ParseOrZero(f[6]);
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return $"기록을 읽지 못했어요: {e.Message}";
            }
            if (testers.Count == 0) return "아직 기록이 없어요.";

            var text = new StringBuilder();
            foreach (KeyValuePair<int, Totals> pair in testers)
            {
                Totals t = pair.Value;
                bool tutorial = true;
                for (int id = 1; id <= TutorialStages; id++) tutorial &= t.Cleared.Contains(id);
                text.AppendLine($"테스터 {pair.Key}  클리어 {t.Cleared.Count}/{stageCount}, 튜토리얼 {(tutorial ? "통과" : "미통과")}");
                text.AppendLine($"    되돌리기 {t.Undos}, 재시작 {t.Restarts}, {t.Seconds / 60}분");
            }
            return text.ToString().TrimEnd();
        }

        static int ParseOrZero(string value) => int.TryParse(value, out int n) ? n : 0;

        sealed class Totals
        {
            public readonly HashSet<int> Cleared = new HashSet<int>();
            public int Undos;
            public int Restarts;
            public int Seconds;
        }
    }
}
