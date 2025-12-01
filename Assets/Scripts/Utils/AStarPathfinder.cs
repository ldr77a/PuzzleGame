using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameState
{
    public Vector2Int position;
    public List<string> collectedColorIds;
    public Color32 currentColor;
    public int moveCount;

    public GameState(Vector2Int pos)
    {
        position = pos;
        collectedColorIds = new List<string>();
        currentColor = new Color32(255, 255, 255, 255);
        moveCount = 0;
    }

    public GameState Clone()
    {
        var clone = new GameState(position);
        clone.collectedColorIds = new List<string>(collectedColorIds);
        clone.currentColor = currentColor;
        clone.moveCount = moveCount;
        return clone;
    }

    public string GetStateKey()
    {
        string colorIds = string.Join(",", collectedColorIds);
        return $"{position.x},{position.y}|{colorIds}";
    }
}

public class SolutionData
{
    public List<Vector2Int> path = new List<Vector2Int>();
    public List<string> colorSequence = new List<string>();
    public int totalMoves;
    public List<GameState> stateHistory = new List<GameState>();
}

public class AStarPathfinder
{
    private Dictionary<string, Color32> colorPalette;
    private Color32 targetColor;

    // 1D 배열 인덱싱 헬퍼 함수
    private int GetGridIndex(int x, int y, int width)
    {
        return y * width + x;
    }

    public SolutionData FindSolution(StageData stage)
    {
        BuildColorPalette(stage);
        targetColor = ColorMixer.ArrayToColor(stage.colorClear);

        var startPos = new Vector2Int(stage.playerStartPosition.x, stage.playerStartPosition.y);
        var startState = new GameState(startPos);

        // A* 알고리즘
        var openSet = new List<GameState> { startState };
        var closedSet = new HashSet<string>();
        var cameFrom = new Dictionary<string, GameState>();

        float timeoutTime = Time.realtimeSinceStartup + 10f; // 10초 타임아웃

        while (openSet.Count > 0)
        {
            if (Time.realtimeSinceStartup > timeoutTime)
            {
                Debug.LogWarning("A* 타임아웃");
                return null;
            }

            // f 값이 가장 낮은 상태 선택
            var current = openSet.OrderBy(s => CalculateFCost(s)).First();

            // 목표 달성 확인
            if (ColorMixer.ColorsMatch(current.currentColor, targetColor))
            {
                return ReconstructSolution(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current.GetStateKey());

            // 이웃 상태 탐색
            foreach (var neighbor in GetNeighbors(current, stage))
            {
                string neighborKey = neighbor.GetStateKey();

                if (closedSet.Contains(neighborKey)) continue;

                if (!openSet.Any(s => s.GetStateKey() == neighborKey))
                {
                    openSet.Add(neighbor);
                    cameFrom[neighborKey] = current;
                }
            }
        }

        return null; // 솔루션 없음
    }

    private List<GameState> GetNeighbors(GameState state, StageData stage)
    {
        var neighbors = new List<GameState>();
        Vector2Int[] directions = {
            new Vector2Int(0, -1), // 위
            new Vector2Int(0, 1),  // 아래
            new Vector2Int(-1, 0), // 왼쪽
            new Vector2Int(1, 0)   // 오른쪽
        };

        // 1. 이동만 (색상 수집 안함)
        foreach (var dir in directions)
        {
            Vector2Int newPos = state.position + dir;

            if (IsValidPosition(newPos, stage) && IsWalkable(newPos, stage))
            {
                var newState = state.Clone();
                newState.position = newPos;
                newState.moveCount++;
                neighbors.Add(newState);
            }
        }

        // 2. 현재 위치에서 색상 수집
        int index = GetGridIndex(state.position.x, state.position.y, stage.gridSize.x);
        int cellValue = stage.gridData[index];

        if (cellValue > 0) // 색상 타일
        {
            var newState = state.Clone();
            newState.collectedColorIds.Add(cellValue.ToString());

            List<Color32> colors = newState.collectedColorIds
                .Select(id => colorPalette[id])
                .ToList();

            newState.currentColor = ColorMixer.MixColors(colors);
            neighbors.Add(newState);
        }
        else if (cellValue == -2) // 리셋 타일
        {
            var newState = state.Clone();
            newState.collectedColorIds.Clear();
            newState.currentColor = new Color32(255, 255, 255, 255);
            neighbors.Add(newState);
        }

        return neighbors;
    }

    private float CalculateFCost(GameState state)
    {
        float g = state.moveCount; // 현재까지 이동 횟수
        float h = CalculateHeuristic(state.currentColor, targetColor);
        return g + h;
    }

    private float CalculateHeuristic(Color32 current, Color32 target)
    {
        // RGB 유클리드 거리
        float dr = (current.r - target.r) / 255f;
        float dg = (current.g - target.g) / 255f;
        float db = (current.b - target.b) / 255f;

        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    private bool IsValidPosition(Vector2Int pos, StageData stage)
    {
        return pos.x >= 0 && pos.x < stage.gridSize.x &&
               pos.y >= 0 && pos.y < stage.gridSize.y;
    }

    private bool IsWalkable(Vector2Int pos, StageData stage)
    {
        int index = GetGridIndex(pos.x, pos.y, stage.gridSize.x);
        int cellValue = stage.gridData[index];
        return cellValue != -1; // -1은 벽
    }

    private void BuildColorPalette(StageData stage)
    {
        colorPalette = new Dictionary<string, Color32>();
        foreach (var entry in stage.colorPalette)
        {
            colorPalette[entry.id] = ColorMixer.ArrayToColor(entry.rgb);
        }
    }

    private SolutionData ReconstructSolution(Dictionary<string, GameState> cameFrom, GameState goal)
    {
        var solution = new SolutionData();
        solution.totalMoves = goal.moveCount;

        // 역추적
        var current = goal;
        while (current != null)
        {
            solution.stateHistory.Insert(0, current);
            solution.path.Insert(0, current.position);

            string key = current.GetStateKey();
            current = cameFrom.ContainsKey(key) ? cameFrom[key] : null;
        }

        return solution;
    }
}
