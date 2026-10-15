using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cameli.Core;
using Cameli.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

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

        [MenuItem("Cameli/스테이지 검증")]
        static void ValidateStages()
        {
            var problems = new StringBuilder();
            int count = 0, failed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { StageFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                count++;
                string problem = Check(asset);
                if (problem == null) continue;
                failed++;
                problems.AppendLine($"{asset.name}: {problem}");
            }

            string summary = failed == 0
                ? $"스테이지 {count}개 모두 정상이에요."
                : $"스테이지 {count}개 중 {failed}개에 문제가 있어요.\n{problems}";
            if (failed == 0) Debug.Log(summary);
            else Debug.LogError(summary);
            EditorUtility.DisplayDialog("Cameli 스테이지 검증", summary, "확인");
        }

        static string Check(TextAsset asset)
        {
            try
            {
                StageFile file = JsonUtility.FromJson<StageFile>(asset.text);
                Solution solution = Solver.Solve(StageDefinition.FromFile(file));
                if (solution == null) return "풀 수 없어요.";
                if (solution.Moves != file.optimalMoves) return $"optimalMoves가 {file.optimalMoves}인데 실제 최단은 {solution.Moves}걸음이에요.";
                return null;
            }
            catch (Exception e) when (e is StageFormatException || e is ArgumentException)
            {
                return e.Message;
            }
        }
    }
}
