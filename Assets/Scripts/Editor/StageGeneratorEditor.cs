using UnityEngine;
using UnityEditor;
using System.IO;

public class StageGeneratorEditor : EditorWindow
{
    private int startStageId = 1;
    private int endStageId = 10;
    private int difficulty = 1; // 1=Easy, 2=Medium, 3=Hard
    
    private StageGenerator generator;
    
    [MenuItem("Tools/Stage Generator")]
    public static void ShowWindow()
    {
        StageGeneratorEditor window = GetWindow<StageGeneratorEditor>("Stage Generator");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }
    
    private void OnEnable()
    {
        // StageGenerator 인스턴스 생성
        GameObject tempObj = new GameObject("TempGenerator");
        generator = tempObj.AddComponent<StageGenerator>();
        tempObj.hideFlags = HideFlags.HideAndDontSave;
    }
    
    private void OnDisable()
    {
        if (generator != null)
        {
            DestroyImmediate(generator.gameObject);
        }
    }
    
    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Stage Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        // 스테이지 ID 범위
        GUILayout.Label("스테이지 ID 범위", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("시작 ID:", GUILayout.Width(80));
        startStageId = EditorGUILayout.IntField(startStageId, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("종료 ID:", GUILayout.Width(80));
        endStageId = EditorGUILayout.IntField(endStageId, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 난이도 선택
        GUILayout.Label("난이도", EditorStyles.boldLabel);
        difficulty = GUILayout.SelectionGrid(difficulty - 1, new string[] { "Easy", "Medium", "Hard" }, 3) + 1;
        
        GUILayout.Space(20);
        
        // 생성 버튼
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("스테이지 생성", GUILayout.Height(40)))
        {
            GenerateStages();
        }
        GUI.backgroundColor = Color.white;
        
        GUILayout.Space(10);
        
        // 단일 스테이지 생성
        GUILayout.Label("단일 스테이지 생성", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("스테이지 ID:", GUILayout.Width(80));
        int singleId = EditorGUILayout.IntField(startStageId, GUILayout.Width(100));
        if (GUILayout.Button("생성", GUILayout.Width(60)))
        {
            GenerateSingleStage(singleId, difficulty);
        }
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(20);
        
        // 정보
        EditorGUILayout.HelpBox(
            $"생성될 스테이지: {endStageId - startStageId + 1}개\n" +
            $"난이도: {GetDifficultyLabel(difficulty)}\n" +
            $"저장 위치: Assets/Resources/Stages/",
            MessageType.Info
        );
    }
    
    private void GenerateStages()
    {
        if (startStageId > endStageId)
        {
            EditorUtility.DisplayDialog("오류", "시작 ID가 종료 ID보다 큽니다.", "확인");
            return;
        }
        
        int successCount = 0;
        int failCount = 0;
        
        for (int id = startStageId; id <= endStageId; id++)
        {
            if (GenerateSingleStage(id, difficulty))
                successCount++;
            else
                failCount++;
        }
        
        EditorUtility.DisplayDialog(
            "생성 완료",
            $"성공: {successCount}개\n실패: {failCount}개",
            "확인"
        );
        
        AssetDatabase.Refresh();
    }
    
    private bool GenerateSingleStage(int stageId, int difficulty)
    {
        StageData stage = generator.GenerateStage(stageId, difficulty);
        
        if (stage == null)
        {
            Debug.LogError($"스테이지 {stageId} 생성 실패");
            return false;
        }
        
        // JSON으로 저장
        string json = JsonUtility.ToJson(stage, true);
        
        // Resources/Stages 폴더 생성
        string folderPath = "Assets/Resources/Stages";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        // JSON 파일 저장
        string filePath = $"{folderPath}/stage_{stageId:000}.json";
        File.WriteAllText(filePath, json);
        
        Debug.Log($"스테이지 {stageId} 생성 완료: {filePath}");
        return true;
    }
    
    private string GetDifficultyLabel(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return "Easy";
            case 2: return "Medium";
            case 3: return "Hard";
            default: return "Unknown";
        }
    }
}
