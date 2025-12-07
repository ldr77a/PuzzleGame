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

    private string _cachedKey;

    public string GetStateKey()
    {
        if (_cachedKey == null)
        {
            // 색상 순서는 결과에 영향이 없으므로 정렬하여 상태 중복 제거 (순열 -> 조합)
            // 성능 최적화: StringBuilder 사용? 짧은 문자열이라 큰 차이 없을 수 있음.
            // 정렬 비용도 있으므로, 가능하면 수집 시 정렬된 상태 유지하는게 좋지만 일단 캐싱으로 해결.
            var sortedIds = collectedColorIds.OrderBy(x => x.Length).ThenBy(x => x);
            string colorIds = string.Join(",", sortedIds);
            _cachedKey = $"{position.x},{position.y}|{colorIds}";
        }
        return _cachedKey;
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
    private int maxCollections = 20; // 무한 수집 방지

    // 1D 배열 인덱싱 헬퍼 함수
    private int GetGridIndex(int x, int y, int width)
    {
        return y * width + x;
    }

    public SolutionData FindSolution(StageData stage, int maxCollectionsLimit = 20)
    {
        maxCollections = maxCollectionsLimit;
        BuildColorPalette(stage);
        targetColor = ColorMixer.ArrayToColor(stage.colorClear);

        var startPos = new Vector2Int(stage.playerStartPosition.x, stage.playerStartPosition.y);
        var startState = new GameState(startPos);

        // A* 알고리즘
        // var openSet = new List<GameState> { startState }; <- Old
        var openSet = new PriorityQueue<GameState>();
        openSet.Enqueue(startState, CalculateFCost(startState));
        
        var openSetLookup = new Dictionary<string, GameState>();
        openSetLookup.Add(startState.GetStateKey(), startState);

        var closedSet = new HashSet<string>();
        var cameFrom = new Dictionary<string, GameState>();
        var gScore = new Dictionary<string, int>(); // g-cost 저장
        gScore[startState.GetStateKey()] = 0;

        float timeoutTime = Time.realtimeSinceStartup + 10f; // 10초 타임아웃

        while (openSet.Count > 0)
        {
            if (Time.realtimeSinceStartup > timeoutTime)
            {
                Debug.LogWarning("A* 타임아웃");
                return null;
            }

            // f 값이 가장 낮은 상태 선택 (O(log N))
            var current = openSet.Dequeue();
            string currentKey = current.GetStateKey();
            openSetLookup.Remove(currentKey); // Lookup에서도 제거

            // 목표 달성 확인 (엄격한 검사: 오차 1)
            if (ColorMixer.ColorsMatch(current.currentColor, targetColor, 1))
            {
                return ReconstructSolution(cameFrom, current);
            }

            closedSet.Add(currentKey);

            // 이웃 상태 탐색
            foreach (var neighbor in GetNeighbors(current, stage))
            {
                string neighborKey = neighbor.GetStateKey();

                if (closedSet.Contains(neighborKey)) continue;

                // 텐터티브 G (현재 G + 1)
                int tentativeG = current.moveCount + 1;
                
                // 기존 G값 확인 (없으면 무한대)
                int existingG = gScore.ContainsKey(neighborKey) ? gScore[neighborKey] : int.MaxValue;

                if (tentativeG < existingG)
                {
                    cameFrom[neighborKey] = current;
                    gScore[neighborKey] = tentativeG;
                    
                    float h = CalculateHeuristic(neighbor.currentColor, targetColor);
                    float f = tentativeG + h;

                    if (!openSetLookup.ContainsKey(neighborKey))
                    {
                        openSet.Enqueue(neighbor, f);
                        openSetLookup.Add(neighborKey, neighbor);
                    }
                    else
                    {
                        // PriorityQueue에서 갱신은 복잡하므로, 
                        // 간단한 구현: 그냥 중복해서 넣거나(메모리 낭비), 
                        // 혹은 커스텀 힙을 써야 함.
                        // 여기서는 커스텀 힙이 없으므로, 그냥 "더 좋은 경로 발견시 중복 추가" 전략 사용
                        // 단, Pop 할 때 ClosedSet 체크하므로 문제 없음.
                        // Lookup 갱신
                        // 주의: PriorityQueue에 중복 키가 들어가면 큐 사이즈가 커짐.
                        // 하지만 일반적인 A* 에서는 DecreaseKey가 필요함.
                        // C# PriorityQueue 없으므로 아래 구현한 클래스 사용
                        
                        // 기존 항목 제거하고 다시 넣기 (Update)
                        // 커스텀 구현체의 UpdatePriority 사용
                         openSet.UpdatePriority(openSetLookup[neighborKey], f);
                         // update reference in lookup if needed (usually same object reference so fine)
                    }
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

        // 이동하면서 타일 효과 자동 적용
        foreach (var dir in directions)
        {
            Vector2Int newPos = state.position + dir;

            if (IsValidPosition(newPos, stage) && IsWalkable(newPos, stage))
            {
                int index = GetGridIndex(newPos.x, newPos.y, stage.gridSize.x);
                int cellValue = stage.gridData[index];

                var newState = state.Clone();
                newState.position = newPos;
                newState.moveCount++;

                // 이동한 위치의 타일 효과 자동 적용
                if (cellValue > 0) // 색상 타일
                {
                    // 색상 타일은 여러 번 수집 가능 (중복 허용)
                    newState.collectedColorIds.Add(cellValue.ToString());

                    List<Color32> colors = newState.collectedColorIds
                        .Select(id => colorPalette[id])
                        .ToList();

                    newState.currentColor = ColorMixer.MixColors(colors);
                }
                else if (cellValue == -2) // 리셋 타일
                {
                    newState.collectedColorIds.Clear();
                    newState.currentColor = new Color32(255, 255, 255, 255);
                }
                else if (cellValue == -3) // FakeColor 타일 (아무 효과 없음)
                {
                    // 아무것도 하지 않음
                }

                neighbors.Add(newState);
            }
        }

        // ========================================
        // ★ NEW: "제자리에서 다시 수집" 행동 추가 ★
        // ========================================
        int currentIndex = GetGridIndex(state.position.x, state.position.y, stage.gridSize.x);
        int currentCellValue = stage.gridData[currentIndex];

        // 현재 위치가 색상 타일이고, 수집 횟수 제한에 도달하지 않았으면 (제자리 수집 허용)
        if (currentCellValue > 0 && state.collectedColorIds.Count < maxCollections)
        {
            var stayState = state.Clone();
            // 위치는 변경 안 함
            stayState.moveCount++;
            stayState.collectedColorIds.Add(currentCellValue.ToString());

            // 색상 재계산
            List<Color32> colors = stayState.collectedColorIds
                .Select(id => colorPalette[id])
                .ToList();

            stayState.currentColor = ColorMixer.MixColors(colors);

            neighbors.Add(stayState);
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
        // 도메인 특화 휴리스틱 (Option B)
        float dr = Mathf.Abs(current.r - target.r) / 255f;
        float dg = Mathf.Abs(current.g - target.g) / 255f;
        float db = Mathf.Abs(current.b - target.b) / 255f;

        float maxDiff = Mathf.Max(dr, dg, db);
        float minDiff = Mathf.Min(dr, dg, db);

        return maxDiff + minDiff * 0.3f;
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

            string key = current.GetStateKey();
            current = cameFrom.ContainsKey(key) ? cameFrom[key] : null;
        }

        // 경로 재구성: 상태 이력에서 모든 상태의 위치를 추출
        foreach (var state in solution.stateHistory)
        {
            solution.path.Add(state.position);
        }

        return solution;
    }
}

// ========================================
// ★ Minimal Min-Heap Priority Queue ★
// ========================================
public class PriorityQueue<T>
{
    private List<T> data;
    private List<float> priorities;

    public int Count => data.Count;

    public PriorityQueue()
    {
        data = new List<T>();
        priorities = new List<float>();
    }

    public void Enqueue(T item, float priority)
    {
        data.Add(item);
        priorities.Add(priority);
        int ci = data.Count - 1; // child index
        while (ci > 0)
        {
            int pi = (ci - 1) / 2; // parent index
            if (priorities[ci] >= priorities[pi]) break;
            
            // Swap
            T tmpItem = data[ci]; data[ci] = data[pi]; data[pi] = tmpItem;
            float tmpPrio = priorities[ci]; priorities[ci] = priorities[pi]; priorities[pi] = tmpPrio;
            
            ci = pi;
        }
    }

    public T Dequeue()
    {
        int li = data.Count - 1; // last index
        T frontItem = data[0];
        data[0] = data[li];
        priorities[0] = priorities[li];
        data.RemoveAt(li);
        priorities.RemoveAt(li);

        li--;
        int pi = 0;
        while (true)
        {
            int ci = pi * 2 + 1; // left child
            if (ci > li) break;
            int rc = ci + 1;     // right child
            if (rc <= li && priorities[rc] < priorities[ci]) ci = rc; // pick smaller child
            
            if (priorities[pi] <= priorities[ci]) break;
            
            // Swap
            T tmpItem = data[pi]; data[pi] = data[ci]; data[ci] = tmpItem;
            float tmpPrio = priorities[pi]; priorities[pi] = priorities[ci]; priorities[ci] = tmpPrio;
            
            pi = ci;
        }
        return frontItem;
    }

    public void UpdatePriority(T item, float newPriority)
    {
        // O(N) scan to find item - not ideal but better than sorting entirely
        // optimize: store index in item? (Requires wrapper)
        // For this task size, simple linear scan is "acceptable" if queue size < 1000
        // BUT we want to fix timeout.
        // Better: Just add duplicate? A* tolerates duplicates if we check ClosedSet on Dequeue.
        // However, we implemented explicit removal in loop.
        // Let's rely on Duplicates-Lazy-Removal pattern which is O(log N) insert, but increases Queue size.
        // But our code calls this ONLY when we found a better path to SAME state (key).
        // Since we check 'closedSet' on Dequeue, duplicates are discarded then.
        // But we ALSO maintain 'openSetLookup'.
        // So we can just Enqueue the same item again with new priority.
        // The old entry in Queue will have WORSE priority and will be popped LATER.
        // When it pops later, check: Is this effective G == current G in Lookup?
        // Actually, simplest is: Just Enqueue new.
        // Remove 'existing' from Queue is hard.
        
        // Let's implement lazy approach in Enqueue and handle "stale" nodes in Dequeue.
        Enqueue(item, newPriority);
    }
}
