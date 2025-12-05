using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class StageDataManager : EditorWindow
{
    // 데이터베이스
    private StageDatabase database;
    
    // UI 상태
    private StageDataEntry selectedStage;
    private Vector2 scrollPosition;
    private StageFilter filter = new StageFilter();
    private SortCriteria sortCriteria = SortCriteria.ID;
    private bool sortAscending = true;
    
    // 필터 UI 상태
    private bool filterEasy = false;
    private bool filterMedium = false;
    private bool filterHard = false;
    
    // 레이아웃 설정
    private const int FILTER_PANEL_WIDTH = 200;
    private const int DETAIL_PANEL_WIDTH = 300;
    private const int THUMBNAIL_SIZE = 120;
    private const int THUMBNAIL_COLUMNS = 5;
    private const int THUMBNAIL_SPACING = 10;
    
    // 스타일
    private GUIStyle selectedThumbnailStyle;
    private GUIStyle normalThumbnailStyle;
    
    [MenuItem("Tools/Stage Data Manager")]
    public static void ShowWindow()
    {
        StageDataManager window = GetWindow<StageDataManager>("Stage Data Manager");
        window.minSize = new Vector2(1000, 600);
        window.Show();
    }
    
    private void OnEnable()
    {
        // 데이터베이스 로드 또는 생성
        string[] guids = AssetDatabase.FindAssets("t:StageDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            database = AssetDatabase.LoadAssetAtPath<StageDatabase>(path);
        }
        else
        {
            database = CreateInstance<StageDatabase>();
            AssetDatabase.CreateAsset(database, "Assets/StageDatabase.asset");
            AssetDatabase.SaveAssets();
        }
        
        // Resources에서 스테이지 로드
        database.LoadAllStagesFromResources();
    }
    
    private void InitializeStyles()
    {
        normalThumbnailStyle = new GUIStyle(GUI.skin.button);
        normalThumbnailStyle.padding = new RectOffset(2, 2, 2, 2);
        
        selectedThumbnailStyle = new GUIStyle(GUI.skin.button);
        selectedThumbnailStyle.padding = new RectOffset(2, 2, 2, 2);
        selectedThumbnailStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.6f, 1f));
    }
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    
    private void OnGUI()
    {
        // 스타일 초기화 (OnGUI에서만 가능)
        if (normalThumbnailStyle == null)
        {
            InitializeStyles();
        }
        
        if (database == null)
        {
            GUILayout.Label("Database not loaded", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        
        GUILayout.Label("Stage Data Manager", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // 좌측: 필터 패널
        DrawFilterPanel();
        
        // 중앙: 썸네일 그리드
        DrawThumbnailGrid();
        
        // 우측: 상세 정보 패널
        DrawDetailPanel();
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawFilterPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(FILTER_PANEL_WIDTH));
        
        GUILayout.Label("필터", EditorStyles.boldLabel);
        
        // 난이도 필터
        GUILayout.Label("난이도", EditorStyles.label);
        filterEasy = GUILayout.Toggle(filterEasy, "Easy");
        filterMedium = GUILayout.Toggle(filterMedium, "Medium");
        filterHard = GUILayout.Toggle(filterHard, "Hard");
        
        UpdateDifficultyFilter();
        
        GUILayout.Space(10);
        
        // 색상 개수 필터
        GUILayout.Label("색상 개수", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Min:", GUILayout.Width(40));
        filter.minColors = EditorGUILayout.IntField(filter.minColors, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Max:", GUILayout.Width(40));
        filter.maxColors = EditorGUILayout.IntField(filter.maxColors, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 이동 횟수 필터
        GUILayout.Label("이동 횟수", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Min:", GUILayout.Width(40));
        filter.minMoves = EditorGUILayout.IntField(filter.minMoves, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Max:", GUILayout.Width(40));
        filter.maxMoves = EditorGUILayout.IntField(filter.maxMoves, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 검색
        GUILayout.Label("검색", EditorStyles.label);
        filter.searchQuery = EditorGUILayout.TextField(filter.searchQuery);
        
        GUILayout.Space(10);
        
        // 정렬
        GUILayout.Label("정렬 기준", EditorStyles.boldLabel);
        sortCriteria = (SortCriteria)GUILayout.SelectionGrid(
            (int)sortCriteria, 
            new string[] { "ID", "난이도", "색상 개수", "이동 횟수" }, 
            1
        );
        
        sortAscending = GUILayout.Toggle(sortAscending, "오름차순");
        
        GUILayout.Space(10);
        
        // 필터 초기화
        if (GUILayout.Button("필터 초기화"))
        {
            filter.Reset();
            filterEasy = filterMedium = filterHard = false;
        }
        
        GUILayout.Space(10);
        
        // 새로고침
        if (GUILayout.Button("새로고침"))
        {
            database.LoadAllStagesFromResources();
            RefreshThumbnails();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void UpdateDifficultyFilter()
    {
        filter.difficulties.Clear();
        if (filterEasy) filter.difficulties.Add(1);
        if (filterMedium) filter.difficulties.Add(2);
        if (filterHard) filter.difficulties.Add(3);
    }
    
    private void DrawThumbnailGrid()
    {
        EditorGUILayout.BeginVertical();
        
        var filteredStages = ApplyFiltersAndSort();
        
        GUILayout.Label($"스테이지: {filteredStages.Count}개", EditorStyles.miniLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        for (int i = 0; i < filteredStages.Count; i++)
        {
            if (i % THUMBNAIL_COLUMNS == 0)
                EditorGUILayout.BeginHorizontal();
            
            DrawThumbnailCard(filteredStages[i]);
            
            if ((i + 1) % THUMBNAIL_COLUMNS == 0 || i == filteredStages.Count - 1)
                EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawThumbnailCard(StageDataEntry entry)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(THUMBNAIL_SIZE + 20));
        
        // 썸네일 이미지
        if (entry.thumbnail == null)
            entry.thumbnail = GenerateThumbnail(entry.data, THUMBNAIL_SIZE);
        
        bool isSelected = (selectedStage == entry);
        GUIStyle style = isSelected ? selectedThumbnailStyle : normalThumbnailStyle;
        
        if (GUILayout.Button(entry.thumbnail, style, GUILayout.Width(THUMBNAIL_SIZE), GUILayout.Height(THUMBNAIL_SIZE)))
        {
            selectedStage = entry;
        }
        
        // 난이도 배지
        string difficultyLabel = GetDifficultyLabel(entry.data.difficulty);
        Color badgeColor = GetDifficultyColor(entry.data.difficulty);
        
        GUI.backgroundColor = badgeColor;
        GUILayout.Label($"#{entry.data.stageId:000}", EditorStyles.boldLabel);
        GUILayout.Label(difficultyLabel, EditorStyles.miniLabel);
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawDetailPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(DETAIL_PANEL_WIDTH));
        
        if (selectedStage == null)
        {
            GUILayout.Label("스테이지를 선택하세요", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }
        
        StageData stage = selectedStage.data;
        
        GUILayout.Label("스테이지 정보", EditorStyles.boldLabel);
        
        EditorGUILayout.LabelField("Stage ID", $"#{stage.stageId:000}");
        EditorGUILayout.LabelField("난이도", GetDifficultyLabel(stage.difficulty));
        EditorGUILayout.LabelField("그리드 크기", $"{stage.gridSize.x}x{stage.gridSize.y}");
        EditorGUILayout.LabelField("색상 개수", stage.colorCount.ToString());
        EditorGUILayout.LabelField("솔루션 이동", stage.solutionMoves.ToString());
        EditorGUILayout.LabelField("최대 이동", stage.maxMoves.ToString());
        EditorGUILayout.LabelField("배치 전략", stage.placementStrategy ?? "N/A");
        EditorGUILayout.LabelField("벽 개수", stage.wallCount.ToString());
        EditorGUILayout.LabelField("특수 타일", stage.hasSpecialTiles ? "있음" : "없음");
        
        GUILayout.Space(10);
        
        // 목표 색상 표시
        if (stage.colorClear != null && stage.colorClear.Length >= 3)
        {
            GUILayout.Label("목표 색상", EditorStyles.boldLabel);
            Color targetColor = ColorMixer.ArrayToColor(stage.colorClear);
            Rect colorRect = GUILayoutUtility.GetRect(100, 30);
            EditorGUI.DrawRect(colorRect, targetColor);
        }
        
        GUILayout.Space(10);
        
        // 색상 팔레트 표시
        if (stage.colorPalette != null && stage.colorPalette.Length > 0)
        {
            GUILayout.Label("색상 팔레트", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var entry in stage.colorPalette)
            {
                Color color = ColorMixer.ArrayToColor(entry.rgb);
                Rect rect = GUILayoutUtility.GetRect(40, 40);
                EditorGUI.DrawRect(rect, color);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        GUILayout.Space(20);
        
        // 액션 버튼
        if (GUILayout.Button("플레이", GUILayout.Height(30)))
        {
            PlayStage(stage);
        }
        
        if (GUILayout.Button("솔루션 보기", GUILayout.Height(30)))
        {
            OpenSolutionViewer(stage);
        }
        
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("삭제", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("삭제 확인", 
                $"스테이지 #{stage.stageId:000}을(를) 삭제하시겠습니까?", 
                "삭제", "취소"))
            {
                DeleteStage(stage);
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndVertical();
    }
    
    // 썸네일 생성
    private Texture2D GenerateThumbnail(StageData stage, int size)
    {
        Texture2D thumbnail = new Texture2D(size, size);
        Color bgColor = new Color(0.2f, 0.2f, 0.2f);
        
        // 배경 채우기
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                thumbnail.SetPixel(x, y, bgColor);
        
        // 그리드 그리기
        int cellSize = size / Mathf.Max(stage.gridSize.x, stage.gridSize.y);
        int offsetX = (size - cellSize * stage.gridSize.x) / 2;
        int offsetY = (size - cellSize * stage.gridSize.y) / 2;
        
        for (int gy = 0; gy < stage.gridSize.y; gy++)
        {
            for (int gx = 0; gx < stage.gridSize.x; gx++)
            {
                int index = gy * stage.gridSize.x + gx;
                int cellValue = stage.gridData[index];
                
                Color cellColor = GetCellColor(cellValue, stage);
                
                // 셀 그리기
                for (int py = 0; py < cellSize - 1; py++)
                {
                    for (int px = 0; px < cellSize - 1; px++)
                    {
                        int pixelX = offsetX + gx * cellSize + px;
                        int pixelY = offsetY + gy * cellSize + py;
                        
                        if (pixelX >= 0 && pixelX < size && pixelY >= 0 && pixelY < size)
                            thumbnail.SetPixel(pixelX, pixelY, cellColor);
                    }
                }
            }
        }
        
        thumbnail.Apply();
        return thumbnail;
    }
    
    private Color GetCellColor(int cellValue, StageData stage)
    {
        if (cellValue == 0) return new Color(0.3f, 0.3f, 0.3f); // 빈 타일
        if (cellValue == -1) return Color.black; // 벽
        if (cellValue == -2) return Color.cyan; // 리셋
        if (cellValue == -3) return Color.magenta; // 페이크
        
        // 색상 타일
        if (stage.colorPalette != null)
        {
            foreach (var entry in stage.colorPalette)
            {
                if (int.Parse(entry.id) == cellValue)
                {
                    return ColorMixer.ArrayToColor(entry.rgb);
                }
            }
        }
        
        return Color.white;
    }
    
    // 헬퍼 메서드
    private List<StageDataEntry> ApplyFiltersAndSort()
    {
        var filtered = database.FilterStages(filter);
        
        // 정렬 적용
        filtered = database.GetSortedStages(sortCriteria, sortAscending);
        
        // 필터 재적용 (정렬 후)
        filtered = FilterList(filtered);
        
        return filtered;
    }
    
    private List<StageDataEntry> FilterList(List<StageDataEntry> list)
    {
        var filtered = list.AsEnumerable();
        
        if (filter.difficulties.Count > 0)
            filtered = filtered.Where(s => filter.difficulties.Contains(s.data.difficulty));
        
        if (filter.minColors > 0)
            filtered = filtered.Where(s => s.data.colorCount >= filter.minColors);
        
        if (filter.maxColors > 0)
            filtered = filtered.Where(s => s.data.colorCount <= filter.maxColors);
        
        if (filter.minMoves > 0)
            filtered = filtered.Where(s => s.data.solutionMoves >= filter.minMoves);
        
        if (filter.maxMoves > 0)
            filtered = filtered.Where(s => s.data.solutionMoves <= filter.maxMoves);
        
        if (!string.IsNullOrEmpty(filter.searchQuery))
        {
            string query = filter.searchQuery.ToLower();
            filtered = filtered.Where(s => 
                s.data.stageId.ToString().Contains(query) ||
                (s.data.placementStrategy != null && s.data.placementStrategy.ToLower().Contains(query)));
        }
        
        return filtered.ToList();
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
    
    private Color GetDifficultyColor(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return new Color(0.5f, 1f, 0.5f); // 연두
            case 2: return new Color(1f, 0.8f, 0.3f); // 주황
            case 3: return new Color(1f, 0.3f, 0.3f); // 빨강
            default: return Color.gray;
        }
    }
    
    private void RefreshThumbnails()
    {
        foreach (var entry in database.Stages)
        {
            entry.thumbnail = null;
        }
    }
    
    private void PlayStage(StageData stage)
    {
        // TODO: 게임 씬으로 전환하고 스테이지 로드
        Debug.Log($"Playing stage #{stage.stageId:000}");
        EditorUtility.DisplayDialog("플레이", 
            $"스테이지 #{stage.stageId:000}을(를) 플레이합니다.\n(게임 씬 전환 기능은 추후 구현)", 
            "확인");
    }
    
    private void OpenSolutionViewer(StageData stage)
    {
        SolutionViewer.ShowWindow(stage);
    }
    
    private void DeleteStage(StageData stage)
    {
        database.RemoveStage(stage.stageId);
        selectedStage = null;
        
        // TODO: JSON 파일도 삭제
        Debug.Log($"Deleted stage #{stage.stageId:000}");
    }
}
