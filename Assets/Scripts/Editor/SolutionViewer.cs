using UnityEngine;
using UnityEditor;

public class SolutionViewer : EditorWindow
{
    private StageData stage;
    private SolutionData solution;
    private int currentStep = 0;
    private bool isPlaying = false;
    private float playbackSpeed = 0.5f;
    private double lastStepTime = 0;
    
    private const int CELL_SIZE = 40;
    
    public static void ShowWindow(StageData stageData)
    {
        SolutionViewer window = GetWindow<SolutionViewer>("Solution Viewer");
        window.stage = stageData;
        window.CalculateSolution();
        window.minSize = new Vector2(600, 700);
        window.Show();
    }
    
    private void CalculateSolution()
    {
        AStarPathfinder pathfinder = new AStarPathfinder();
        solution = pathfinder.FindSolution(stage);
        
        if (solution == null)
        {
            EditorUtility.DisplayDialog("오류", "솔루션을 찾을 수 없습니다.", "확인");
            Close();
        }
        else
        {
            Debug.Log($"Solution found: {solution.totalMoves} moves, {solution.stateHistory.Count} states");
        }
    }
    
    private void OnGUI()
    {
        if (stage == null || solution == null)
        {
            GUILayout.Label("No solution loaded", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        
        GUILayout.Space(10);
        GUILayout.Label($"Solution Viewer - Stage #{stage.stageId:000}", EditorStyles.boldLabel);
        
        GUILayout.Space(10);
        
        // 그리드 시각화
        DrawGridVisualization();
        
        GUILayout.Space(10);
        
        // 진행 바
        DrawProgressBar();
        
        GUILayout.Space(10);
        
        // 컨트롤 버튼
        DrawControls();
        
        GUILayout.Space(10);
        
        // 상태 정보
        DrawStateInfo();
        
        // 자동 재생
        if (isPlaying && EditorApplication.timeSinceStartup - lastStepTime > playbackSpeed)
        {
            NextStep();
            lastStepTime = EditorApplication.timeSinceStartup;
            Repaint();
        }
        
        // 재생 중일 때 지속적으로 Repaint
        if (isPlaying)
        {
            Repaint();
        }
    }
    
    private void DrawGridVisualization()
    {
        int gridWidth = stage.gridSize.x * CELL_SIZE;
        int gridHeight = stage.gridSize.y * CELL_SIZE;
        
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        Rect gridRect = GUILayoutUtility.GetRect(gridWidth, gridHeight);
        
        GameState currentState = solution.stateHistory[currentStep];
        
        // 그리드 배경
        EditorGUI.DrawRect(gridRect, new Color(0.15f, 0.15f, 0.15f));
        
        // 그리드 셀 그리기
        for (int y = 0; y < stage.gridSize.y; y++)
        {
            for (int x = 0; x < stage.gridSize.x; x++)
            {
                Rect cellRect = new Rect(
                    gridRect.x + x * CELL_SIZE + 1,
                    gridRect.y + y * CELL_SIZE + 1,
                    CELL_SIZE - 2,
                    CELL_SIZE - 2
                );
                
                int index = y * stage.gridSize.x + x;
                int cellValue = stage.gridData[index];
                
                Color cellColor = GetCellColor(cellValue);
                
                // 수집된 색상 타일은 어둡게 표시
                if (cellValue > 0 && currentState.collectedColorIds.Contains(cellValue.ToString()))
                {
                    cellColor = cellColor * 0.4f;
                    cellColor.a = 1f;
                }
                
                EditorGUI.DrawRect(cellRect, cellColor);
                
                // 플레이어 위치 표시
                if (currentState.position.x == x && currentState.position.y == y)
                {
                    Rect playerRect = new Rect(
                        cellRect.x + cellRect.width * 0.2f,
                        cellRect.y + cellRect.height * 0.2f,
                        cellRect.width * 0.6f,
                        cellRect.height * 0.6f
                    );
                    EditorGUI.DrawRect(playerRect, Color.yellow);
                }
            }
        }
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
    
    private void DrawProgressBar()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Step:", GUILayout.Width(40));
        
        int newStep = (int)GUILayout.HorizontalSlider(currentStep, 0, solution.stateHistory.Count - 1);
        if (newStep != currentStep)
        {
            currentStep = newStep;
            isPlaying = false; // 수동 조작 시 자동 재생 중지
        }
        
        GUILayout.Label($"{currentStep + 1} / {solution.stateHistory.Count}", GUILayout.Width(60));
        GUILayout.EndHorizontal();
    }
    
    private void DrawControls()
    {
        GUILayout.BeginHorizontal();
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("◀◀", GUILayout.Width(50), GUILayout.Height(30)))
            FirstStep();
        
        if (GUILayout.Button("◀", GUILayout.Width(50), GUILayout.Height(30)))
            PreviousStep();
        
        string playLabel = isPlaying ? "⏸ Pause" : "▶ Play";
        if (GUILayout.Button(playLabel, GUILayout.Width(80), GUILayout.Height(30)))
        {
            isPlaying = !isPlaying;
            lastStepTime = EditorApplication.timeSinceStartup;
        }
        
        if (GUILayout.Button("▶", GUILayout.Width(50), GUILayout.Height(30)))
            NextStep();
        
        if (GUILayout.Button("▶▶", GUILayout.Width(50), GUILayout.Height(30)))
            LastStep();
        
        GUILayout.FlexibleSpace();
        
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 재생 속도
        GUILayout.BeginHorizontal();
        GUILayout.Label("Speed:", GUILayout.Width(50));
        playbackSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.1f, 2f);
        GUILayout.Label($"{playbackSpeed:F1}s", GUILayout.Width(40));
        GUILayout.EndHorizontal();
    }
    
    private void DrawStateInfo()
    {
        GameState currentState = solution.stateHistory[currentStep];
        
        GUILayout.Label("Current State", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 현재 색상
        GUILayout.BeginHorizontal();
        GUILayout.Label("Current Color:", GUILayout.Width(100));
        Rect colorRect = GUILayoutUtility.GetRect(60, 20);
        EditorGUI.DrawRect(colorRect, currentState.currentColor);
        GUILayout.Label($"RGB({currentState.currentColor.r}, {currentState.currentColor.g}, {currentState.currentColor.b})");
        GUILayout.EndHorizontal();
        
        // 목표 색상
        GUILayout.BeginHorizontal();
        GUILayout.Label("Target Color:", GUILayout.Width(100));
        Color targetColor = ColorMixer.ArrayToColor(stage.colorClear);
        colorRect = GUILayoutUtility.GetRect(60, 20);
        EditorGUI.DrawRect(colorRect, targetColor);
        GUILayout.Label($"RGB({targetColor.r}, {targetColor.g}, {targetColor.b})");
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 색상 매치 여부
        bool isMatched = ColorMixer.ColorsMatch(currentState.currentColor, targetColor);
        GUI.backgroundColor = isMatched ? new Color(0.5f, 1f, 0.5f) : Color.white;
        GUILayout.Label(isMatched ? "✓ 목표 색상 달성!" : "목표 색상 미달성", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.white;
        
        GUILayout.Space(5);
        
        // 수집된 색상들
        if (currentState.collectedColorIds.Count > 0)
        {
            GUILayout.Label("Collected Colors:", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            
            foreach (string colorId in currentState.collectedColorIds)
            {
                foreach (var entry in stage.colorPalette)
                {
                    if (entry.id == colorId)
                    {
                        Color color = ColorMixer.ArrayToColor(entry.rgb);
                        colorRect = GUILayoutUtility.GetRect(30, 30);
                        EditorGUI.DrawRect(colorRect, color);
                        break;
                    }
                }
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.Label($"Total: {currentState.collectedColorIds.Count} colors", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("Collected Colors: None", EditorStyles.miniLabel);
        }
        
        GUILayout.Space(5);
        
        // 이동 횟수
        EditorGUILayout.LabelField("Move Count", $"{currentState.moveCount} / {stage.maxMoves}");
        
        // 위치
        EditorGUILayout.LabelField("Position", $"({currentState.position.x}, {currentState.position.y})");
        
        // 솔루션 정보
        EditorGUILayout.LabelField("Solution Moves", $"{solution.totalMoves}");
        
        EditorGUILayout.EndVertical();
    }
    
    private void FirstStep()
    {
        currentStep = 0;
        isPlaying = false;
    }
    
    private void LastStep()
    {
        currentStep = solution.stateHistory.Count - 1;
        isPlaying = false;
    }
    
    private void PreviousStep()
    {
        currentStep = Mathf.Max(0, currentStep - 1);
        isPlaying = false;
    }
    
    private void NextStep()
    {
        currentStep = Mathf.Min(solution.stateHistory.Count - 1, currentStep + 1);
        
        // 마지막 단계에 도달하면 자동 재생 중지
        if (currentStep >= solution.stateHistory.Count - 1)
        {
            isPlaying = false;
        }
    }
    
    private Color GetCellColor(int cellValue)
    {
        if (cellValue == 0) return new Color(0.3f, 0.3f, 0.3f); // 빈 타일
        if (cellValue == -1) return Color.black; // 벽
        if (cellValue == -2) return Color.cyan; // 리셋
        if (cellValue == -3) return Color.magenta; // 페이크
        
        // 색상 타일
        foreach (var entry in stage.colorPalette)
        {
            if (int.Parse(entry.id) == cellValue)
                return ColorMixer.ArrayToColor(entry.rgb);
        }
        
        return Color.white;
    }
}
