#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StageDataEntry
{
    public StageData data;
    public SolutionData solution;
    public string difficulty;
    public System.DateTime createdDate;
}

public enum SortMode
{
    StageId,
    Difficulty,
    MoveCount,
    ColorCount
}

public enum FilterMode
{
    All,
    Difficulty,
    Solvable
}

public class StageDataManager : EditorWindow
{
    private List<StageDataEntry> allStages = new List<StageDataEntry>();
    private Vector2 scrollPosition;
    private SortMode sortMode = SortMode.StageId;
    private FilterMode filterMode = FilterMode.All;
    private StageGenerator generator;

    [MenuItem("도구/스테이지 관리 도구")]
    public static void ShowWindow()
    {
        GetWindow<StageDataManager>("스테이지 관리");
    }

    void OnEnable()
    {
        // StageGenerator 인스턴스 생성
        GameObject tempObj = new GameObject("TempGenerator");
        generator = tempObj.AddComponent<StageGenerator>();
    }

    void OnDestroy()
    {
        if (generator != null)
        {
            DestroyImmediate(generator.gameObject);
        }
    }

    void OnGUI()
    {
        GUILayout.Label("=== 스테이지 데이터 관리 ===", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // 생성 버튼
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Easy 스테이지 3개 생성", GUILayout.Height(30)))
        {
            GenerateStages(3, 1);
        }

        if (GUILayout.Button("Medium 스테이지 4개 생성", GUILayout.Height(30)))
        {
            GenerateStages(4, 2);
        }

        if (GUILayout.Button("Hard 스테이지 3개 생성", GUILayout.Height(30)))
        {
            GenerateStages(3, 3);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 정렬/필터
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("정렬:", GUILayout.Width(50));
        sortMode = (SortMode)EditorGUILayout.EnumPopup(sortMode, GUILayout.Width(150));

        EditorGUILayout.LabelField("필터:", GUILayout.Width(50));
        filterMode = (FilterMode)EditorGUILayout.EnumPopup(filterMode, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 스테이지 리스트
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var sortedStages = SortStages(allStages);

        foreach (var entry in sortedStages)
        {
            DrawStageEntry(entry);
        }

        EditorGUILayout.EndScrollView();
    }

    private void GenerateStages(int count, int difficulty)
    {
        for (int i = 0; i < count; i++)
        {
            int stageId = allStages.Count + 1;
            StageData data = generator.GenerateStage(stageId, difficulty);

            if (data != null)
            {
                AStarPathfinder pathfinder = new AStarPathfinder();
                SolutionData solution = pathfinder.FindSolution(data);

                var entry = new StageDataEntry
                {
                    data = data,
                    solution = solution,
                    difficulty = difficulty == 1 ? "Easy" : (difficulty == 2 ? "Medium" : "Hard"),
                    createdDate = System.DateTime.Now
                };

                allStages.Add(entry);
                ExportToJSON(entry);
            }
        }

        Debug.Log($"{count}개 스테이지 생성 완료!");
    }

    private void DrawStageEntry(StageDataEntry entry)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Stage {entry.data.stageId}", EditorStyles.boldLabel, GUILayout.Width(80));
        GUILayout.Label($"난이도: {entry.difficulty}", GUILayout.Width(100));
        GUILayout.Label($"이동: {entry.data.maxMoves}", GUILayout.Width(80));
        GUILayout.Label($"색상: {entry.data.colorPalette.Length}개", GUILayout.Width(80));

        if (entry.solution != null)
        {
            GUILayout.Label($"✓ ({entry.solution.totalMoves}이동)", GUILayout.Width(80));
        }

        EditorGUILayout.EndHorizontal();

        // 버튼들
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("JSON 내보내기", GUILayout.Width(120)))
        {
            ExportToJSON(entry);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private List<StageDataEntry> SortStages(List<StageDataEntry> stages)
    {
        switch (sortMode)
        {
            case SortMode.StageId:
                return stages.OrderBy(s => s.data.stageId).ToList();
            case SortMode.Difficulty:
                return stages.OrderBy(s => s.difficulty).ToList();
            case SortMode.MoveCount:
                return stages.OrderBy(s => s.data.maxMoves).ToList();
            case SortMode.ColorCount:
                return stages.OrderBy(s => s.data.colorPalette.Length).ToList();
            default:
                return stages;
        }
    }

    private void ExportToJSON(StageDataEntry entry)
    {
        string json = JsonUtility.ToJson(entry.data, true);
        string path = $"Assets/Resources/Stages/stage_{entry.data.stageId:000}.json";

        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        Debug.Log($"스테이지 {entry.data.stageId} JSON 저장: {path}");
    }
}
#endif
