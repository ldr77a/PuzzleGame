using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cameli.Core;
using Cameli.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cameli.EditorTools
{
    /// <summary>Phase 1 setup and validation commands under the Cameli menu.</summary>
    static class CameliMenu
    {
        const string ScenePath = "Assets/0. Scenes/Prototype.unity";
        const string StageFolder = "Assets/Resources/" + StageLibrary.ResourceFolder;

        /// <summary>Temporary; the final bundle ID is decided in Phase 4.</summary>
        const string ApplicationId = "com.cameligarden.prototype";

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
        static void CreatePrototypeScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Cameli", $"{ScenePath} 파일이 이미 있어요. 새로 만들까요?", "새로 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Background;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            new GameObject("Cameli Game", typeof(CameliGame));
            EditorSceneManager.SaveScene(scene, ScenePath);

            // The prototype is the only enabled build scene; the old scenes (with the dev dashboard) stay listed but off.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (existing.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(existing.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();   // write EditorBuildSettings.asset now instead of on the next project save
            Debug.Log($"Created {ScenePath} and made it the only enabled build scene.");
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
