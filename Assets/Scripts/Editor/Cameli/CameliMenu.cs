using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cameli.Core;
using Cameli.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using GameProgress = Cameli.Game.Progress; // UnityEditor.Progress has the same name

namespace Cameli.EditorTools
{
    /// <summary>Setup, scene creation, build, and validation commands under the Cameli menu.</summary>
    static class CameliMenu
    {
        const string ScenePath = "Assets/0. Scenes/Prototype.unity";
        const string GardenScenePath = "Assets/0. Scenes/Garden.unity";
        const string StageFolder = "Assets/Resources/" + StageLibrary.ResourceFolder;

        /// <summary>Temporary; the final bundle ID is decided in Phase 4.</summary>
        const string ApplicationId = "com.cameligarden.prototype";

        /// <summary>Builds go outside the project: ~/Builds/CameliGarden.</summary>
        static string BuildRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Builds", "CameliGarden");

        [MenuItem("Cameli/모바일 프로젝트 설정 적용")]
        static void ApplyMobileSettings()
        {
            PlayerSettings.productName = "Cameli Garden";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationId);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Cameli",
                $"세로 화면 고정, 제품명 \"Cameli Garden\", 임시 번들 ID {ApplicationId}를 적용했어요.", "확인");
        }

        [MenuItem("Cameli/프로토타입 씬 만들기")]
        static void CreatePrototypeScene() => CreateAppScene(ScenePath, "Cameli Game", typeof(CameliGame));

        /// <summary>Phase 2: the title, garden, and play screens (GardenApp).</summary>
        [MenuItem("Cameli/정원 씬 만들기")]
        static void CreateGardenScene() => CreateAppScene(GardenScenePath, "Garden App", typeof(GardenApp));

        /// <summary>
        /// Creates a scene with a camera, an EventSystem, and one GameObject carrying the entry point that builds the
        /// UI in code, then makes it the only enabled build scene; the others stay listed but off.
        /// </summary>
        static void CreateAppScene(string path, string objectName, Type entryPoint)
        {
            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("Cameli", $"{path} 파일이 이미 있어요. 새로 만들까요?", "새로 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Background;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            new GameObject(objectName, entryPoint);
            EditorSceneManager.SaveScene(scene, path);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (existing.path != path) scenes.Add(new EditorBuildSettingsScene(existing.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();   // write EditorBuildSettings.asset now instead of on the next project save
            Debug.Log($"Created {path} and made it the only enabled build scene.");
        }

        [MenuItem("Cameli/Android 빌드 후 폰에서 실행")]
        static void BuildAndroid()
        {
            // AutoRunPlayer installs and starts the APK on a phone connected with USB debugging. Without a phone
            // the APK is still built and can be shared with playtesters.
            string apk = Path.Combine(BuildRoot, "CameliGarden.apk");
            DateTime started = DateTime.Now;
            bool built = Build(BuildTarget.Android, BuildTargetGroup.Android, "Android", apk, BuildOptions.AutoRunPlayer,
                "폰에 설치·실행하는 단계에서 실패했다면 APK는 만들어졌을 수 있어요. USB 연결과 USB 디버깅을 확인해 주세요.");
            // Without a phone the run step can fail the whole build even though the APK was written.
            if (built || (File.Exists(apk) && File.GetLastWriteTime(apk) >= started)) EditorUtility.RevealInFinder(apk);
        }

        [MenuItem("Cameli/iOS Xcode 프로젝트 만들기")]
        static void BuildIos()
        {
            string folder = Path.Combine(BuildRoot, "iOS");
            if (!Build(BuildTarget.iOS, BuildTargetGroup.iOS, "iOS", folder, BuildOptions.None)) return;
            string project = Path.Combine(folder, "Unity-iPhone.xcodeproj");
            if (Directory.Exists(project)) EditorUtility.OpenWithDefaultApp(project);
            else EditorUtility.RevealInFinder(folder);
        }

        /// <summary>Builds the enabled build scenes. Switches the active platform if needed, which takes a while.</summary>
        static bool Build(BuildTarget target, BuildTargetGroup group, string label, string path, BuildOptions options,
            string failureHint = "")
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                EditorUtility.DisplayDialog("Cameli",
                    $"{label} 빌드 모듈이 설치되어 있지 않아요.\nUnity Hub → 설치 → {Application.unityVersion} 톱니바퀴 → 모듈 추가에서 {label} Build Support를 설치해 주세요.",
                    "확인");
                return false;
            }
            string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Cameli", "빌드할 씬이 없어요. 먼저 \"Cameli → 정원 씬 만들기\"를 실행해 주세요.", "확인");
                return false;
            }

            Directory.CreateDirectory(BuildRoot);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = options,
            });
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"{label} build succeeded: {path}");
                return true;
            }
            EditorUtility.DisplayDialog("Cameli",
                $"{label} 빌드에 실패했어요 (오류 {report.summary.totalErrors}개). Console 창의 빨간 오류를 확인해 주세요.\n{failureHint}".TrimEnd(),
                "확인");
            return false;
        }

        /// <summary>
        /// Runs <see cref="StageAudit"/> (the same checks as <c>lab.sh check</c>) on every stage file, in every
        /// difficulty folder. A wrong optimalMoves is rewritten in place, touching only that number; everything else
        /// (including a file outside the difficulty folders, or ids out of folder order) is reported, not fixed.
        /// Logs one Console line per stage and shows a short summary dialog. Each file is checked on its own: a file
        /// that cannot be read, parsed, audited, or rewritten is reported and the run goes on with the rest.
        /// </summary>
        [MenuItem("Cameli/스테이지 검증")]
        static void ValidateStages()
        {
            // Folder order, then id order within a folder (stage_9 before stage_10, stage_99 before stage_100);
            // files outside the difficulty folders go last.
            string[] paths = Directory.GetFiles(StageFolder, "*.json", SearchOption.AllDirectories)
                .OrderBy(p => SectionOf(p) < 0 ? int.MaxValue : SectionOf(p))
                .ThenBy(p => Path.GetFileName(p).Length)
                .ThenBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                .ToArray();
            var problems = new List<string>();
            var notes = new List<string>();
            var fixes = new List<string>();
            var ids = new List<(int id, int section)>();
            int failed = 0, unread = 0;
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(paths[i]);
                    EditorUtility.DisplayProgressBar("Cameli 스테이지 검증", name, i / (float)paths.Length);
                    List<string> found;
                    bool read;
                    try
                    {
                        found = CheckStage(paths[i], name, SectionOf(paths[i]), ids, fixes, notes, out read);
                    }
                    catch (Exception e)
                    {
                        // CheckStage reports its own errors; this only keeps an unexpected one from stopping the run.
                        found = new List<string> { $"검사 중 오류: {e.GetType().Name}: {e.Message}" };
                        read = true;
                        Debug.LogException(e);
                    }
                    if (!read) unread++;
                    if (found.Count == 0) continue;
                    failed++;
                    problems.Add($"{name}: {string.Join(", ", found)}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (fixes.Count > 0) AssetDatabase.Refresh();
            }

            bool idsOk = true;
            if (unread > 0)
            {
                // A file that could not be read has no id, so checking the rest would report a gap that isn't there.
                notes.Add($"읽지 못한 파일이 {unread}개 있어서 번호(id)가 빠짐없이 이어지는지는 검사하지 않았어요.");
            }
            else
            {
                List<int> sorted = ids.Select(s => s.id).OrderBy(id => id).ToList();
                for (int i = 0; i < sorted.Count; i++) idsOk &= sorted[i] == i + 1;
                if (!idsOk) problems.Add($"번호(id)가 1부터 빠짐없이 이어져야 해요. 지금: {string.Join(", ", sorted)}");
            }
            var order = StageSections.FirstOutOfOrder(ids.Where(s => s.section >= 0));
            if (order != null)
            {
                var o = order.Value;
                idsOk = false;
                // Names both stages: the misplaced one may be either (moved forward or moved back).
                problems.Add($"{o.previousId}번({StageSections.All[o.previousSection].Folder})과 {o.id}번({StageSections.All[o.section].Folder})의 " +
                             "폴더 순서가 어긋나요. 번호는 난이도 폴더 순서대로 이어져야 해요");
            }

            var summary = new StringBuilder(failed > 0 ? $"스테이지 {paths.Length}개 중 {failed}개에 문제가 있어요."
                : idsOk ? $"스테이지 {paths.Length}개 모두 통과했어요."
                : $"스테이지 {paths.Length}개를 검사했어요. 번호에 문제가 있어요.");
            if (fixes.Count > 0) summary.Append($"\n\noptimalMoves를 고친 스테이지 {fixes.Count}개:\n").Append(string.Join("\n", fixes));
            if (problems.Count > 0) summary.Append("\n\n").Append(string.Join("\n", problems));
            if (notes.Count > 0) summary.Append("\n\n참고:\n").Append(string.Join("\n", notes));
            string text = summary.ToString();
            if (problems.Count > 0) Debug.LogError(text);
            else if (notes.Count > 0) Debug.LogWarning(text);
            else Debug.Log(text);

            const int dialogLines = 25;
            string[] lines = text.Split('\n');
            if (lines.Length > dialogLines)
                text = string.Join("\n", lines, 0, dialogLines) + $"\n… 외 {lines.Length - dialogLines}줄은 Console 창에서 확인해 주세요.";
            EditorUtility.DisplayDialog("Cameli 스테이지 검증", text, "확인");
        }

        /// <summary>
        /// Index in <see cref="StageSections.All"/> of the difficulty folder a stage file sits in (its first folder
        /// under the stage folder, like the game's per-folder loading), or -1 when it is outside them.
        /// </summary>
        static int SectionOf(string path)
        {
            string rest = path.Substring(StageFolder.Length + 1).Replace('\\', '/');
            int slash = rest.IndexOf('/');
            return slash < 0 ? -1 : StageSections.IndexOf(rest.Substring(0, slash));
        }

        const string NotUtf8 = "파일 인코딩이 UTF-8이 아니에요 (편집기에서 UTF-8로 다시 저장해 주세요)";

        /// <summary>
        /// Audits one stage file, logs its Console line, and returns its problems (empty when it passes). Fixes a
        /// wrong optimalMoves in the file and records it in <paramref name="fixes"/>; a file name that doesn't match
        /// the id goes to <paramref name="notes"/> (a warning, not a problem). <paramref name="read"/> is false when
        /// the file could not be read or parsed, so it has no id. <paramref name="section"/> is the file's difficulty
        /// folder (<see cref="SectionOf"/>); a file outside them is a problem, since the game never loads it.
        /// </summary>
        static List<string> CheckStage(string path, string name, int section, List<(int id, int section)> ids, List<string> fixes,
            List<string> notes, out bool read)
        {
            var problems = new List<string>();
            read = false;
            if (section < 0) problems.Add($"난이도 폴더({StageSections.FolderList}) 밖에 있어서 게임에 나오지 않아요");
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                return Fail(problems, name, $"파일을 읽지 못했어요: {e.Message}");
            }

            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string json = bom ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3) : Encoding.UTF8.GetString(bytes);
            bool utf8 = json.IndexOf('�') < 0; // invalid UTF-8 bytes decode to U+FFFD
            if (!utf8) problems.Add(NotUtf8);

            StageFile file;
            try
            {
                file = JsonUtility.FromJson<StageFile>(json);
            }
            catch (Exception e)
            {
                return Fail(problems, name, $"JSON 형식 오류: {e.Message}");
            }
            if (file == null) return Fail(problems, name, "파일이 비어 있거나 JSON이 아니에요");
            read = true;
            ids.Add((file.id, section));
            string expectedName = $"stage_{file.id:00}";
            if (name != expectedName)
            {
                string note = $"{name}: 파일 이름이 id와 달라요 (id: {file.id}, 맞는 이름: {expectedName}.json)";
                notes.Add(note);
                Debug.LogWarning(note);
            }

            StageReport report;
            try
            {
                report = StageAudit.Audit(file);
            }
            catch (StageFormatException e)
            {
                return Fail(problems, name, $"형식 오류: {e.Message}");
            }
            catch (SearchLimitException)
            {
                return Fail(problems, name, "판이 너무 크거나 복잡해서 검사를 끝내지 못했어요. 에이전트에게 알려 주세요");
            }
            catch (Exception e)
            {
                return Fail(problems, name, $"검사 중 오류: {e.GetType().Name}: {e.Message}");
            }

            string label = $"{report.Id,2}. {report.Title}";
            if (!report.Solvable)
            {
                problems.Add("풀 수 없어요");
                Debug.LogError($"{label}  {string.Join(", ", problems)} (목표 {report.Target})");
                return problems;
            }

            string optimal = $"최단 {report.Optimal}";
            if (report.OptimalMismatch)
            {
                string failure = utf8
                    ? RewriteOptimalMoves(path, json, bom, report.Optimal)
                    : $"optimalMoves를 {report.Optimal}(으)로 직접 고쳐 주세요";
                if (failure == null)
                {
                    fixes.Add($"{name}: {report.StoredOptimal} → {report.Optimal}");
                    optimal += $" (저장값 {report.StoredOptimal} → 고침)";
                }
                else
                {
                    problems.Add(failure);
                    optimal += $" (저장값 {report.StoredOptimal}!)";
                }
            }

            StageProblems flags = report.Problems;
            if ((flags & StageProblems.NotExact) != 0) problems.Add("최단 풀이의 방울 수가 목표와 달라요");
            if ((flags & StageProblems.GateNotExact) != 0) problems.Add("게이트를 정확한 레시피가 아닌 상태로 지나요");
            if ((flags & StageProblems.GateSkippable) != 0) problems.Add("게이트 없이도 풀려요");
            if ((flags & StageProblems.GateAsReset) != 0) problems.Add("최단 풀이가 게이트 너머가 아니라 시작 쪽에서 끝나요");
            if ((flags & StageProblems.TargetAmbiguous) != 0)
                problems.Add($"목표 {report.Target}와 헷갈림 ({report.TargetNearest}, ΔE {report.TargetDeltaE:F1})");
            if ((flags & StageProblems.GateAmbiguous) != 0)
            {
                string[] pair = report.GateNearest.Split('~');
                problems.Add($"게이트 {pair[0]}와 헷갈림 ({pair[1]}, ΔE {report.GateDeltaE:F1})");
            }
            if ((flags & StageProblems.PathBroken) != 0) problems.Add(PathProblem(report));

            string gate = report.GateUse switch
            {
                GateUse.Door => $"  게이트ΔE {report.GateDeltaE:F1} ({report.GateNearest.Replace("~", " vs ")})",
                GateUse.Skippable => "  게이트 없어도 풀림",
                GateUse.AsReset => "  게이트를 리셋으로 씀",
                _ => "",
            };
            if (report.GateUse == GateUse.Door && report.GateAmbiguous) gate += "!";
            string line = $"{label}  {report.Width}x{report.Height} {report.Colors} 목표 {report.Target}  {optimal}" +
                $"  목표ΔE {report.TargetDeltaE:F1}{(report.TargetAmbiguous ? "!" : "")} ({report.TargetNearest}){gate}" +
                $"  타일ΔE {report.TileDeltaE:F1}  무작위 {report.WalkClear * 100:F2}%  풀이 {report.OptimalSets}가지" +
                (problems.Count == 0 ? "  통과" : $"  문제: {string.Join(", ", problems)}");
            if (problems.Count == 0) Debug.Log(line);
            else Debug.LogWarning(line);
            return problems;
        }

        /// <summary>Why the tutorial path fails, for the problem list.</summary>
        static string PathProblem(StageReport report) => report.PathCheck switch
        {
            PathCheck.Blocked => $"path의 {report.PathStep}번째 걸음이 닫힌 문에 막혀요",
            PathCheck.ClearsEarly => $"path가 끝나기 전 {report.PathStep}번째 걸음에서 클리어돼요 (전체 {report.PathLength}걸음)",
            PathCheck.NotCleared => $"path를 끝까지 걸어도 클리어되지 않아요 ({report.PathLength}걸음)",
            _ => "path가 맞지 않아요",
        };

        /// <summary>Adds <paramref name="problem"/>, logs the file's problems as an error, and returns them.</summary>
        static List<string> Fail(List<string> problems, string name, string problem)
        {
            problems.Add(problem);
            Debug.LogError($"{name}  {string.Join(", ", problems)}");
            return problems;
        }

        static readonly Regex OptimalMovesKey = new Regex("\"optimalMoves\"\\s*:");

        /// <summary>The field with an integer value: the digits must not run on into a decimal point or exponent ("7.5").</summary>
        static readonly Regex OptimalMovesField = new Regex("(\"optimalMoves\"\\s*:\\s*)-?\\d+(?![\\d.eE])");

        /// <summary>
        /// Replaces the number in the file's one "optimalMoves" field, keeping every other byte. Returns null when
        /// done, or the problem to report (field missing, duplicated, not an integer, or the file could not be written).
        /// <paramref name="json"/> is the file's text, already checked to be valid UTF-8.
        /// </summary>
        static string RewriteOptimalMoves(string path, string json, bool bom, int optimal)
        {
            int keys = OptimalMovesKey.Matches(json).Count;
            if (keys == 0) return $"optimalMoves 줄이 없어요. \"optimalMoves\": {optimal} 줄을 넣어 주세요";
            if (keys > 1) return $"optimalMoves 줄이 {keys}개예요. 하나만 남기고 {optimal}(으)로 고쳐 주세요";
            if (OptimalMovesField.Matches(json).Count != 1) return $"optimalMoves가 정수가 아니에요. {optimal}(으)로 고쳐 주세요";
            try
            {
                File.WriteAllText(path, OptimalMovesField.Replace(json, "${1}" + optimal), new UTF8Encoding(bom));
            }
            catch (Exception e)
            {
                return $"파일을 고치지 못했어요 ({e.Message}). optimalMoves를 {optimal}(으)로 직접 고쳐 주세요";
            }
            return null;
        }

        const string UnlockAllMenu = "Cameli/개발: 모든 스테이지 열기";

        /// <summary>
        /// Dev switch stored in PlayerPrefs (<see cref="GameProgress.DevUnlockAll"/>): every stage opens in the garden. Works
        /// in the editor and development builds only; release builds ignore it.
        /// </summary>
        [MenuItem(UnlockAllMenu)]
        static void ToggleUnlockAll()
        {
            GameProgress.DevUnlockAll = !GameProgress.DevUnlockAll;
            Menu.SetChecked(UnlockAllMenu, GameProgress.DevUnlockAll);
            Debug.Log(GameProgress.DevUnlockAll
                ? "모든 스테이지 열기를 켰어요. 정원 화면을 다시 열면 잠긴 스테이지도 누를 수 있어요 (에디터와 개발 빌드에서만)."
                : "모든 스테이지 열기를 껐어요. 저장된 진행만큼만 열려요.");
        }

        [MenuItem(UnlockAllMenu, true)]
        static bool ToggleUnlockAllValidate()
        {
            Menu.SetChecked(UnlockAllMenu, GameProgress.DevUnlockAll);
            return true;
        }
    }
}
