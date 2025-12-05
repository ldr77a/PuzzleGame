using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum PlacementStrategy
{
    Random,     // 랜덤 배치
    Clustered,  // 색상들을 한 영역에 모음
    Scattered,  // 색상들을 최대한 멀리 배치
    Linear      // 색상들을 일직선/경로로 배치
}

[System.Serializable]
public struct DifficultyProfile
{
    [Header("그리드 설정")]
    public Vector2Int gridSize;

    [Header("색상 설정")]
    public int minColors;
    public int maxColors;
    public PlacementStrategy placementStrategy;

    [Header("목표 색상 혼합 (총 반복 횟수)")]
    public int minTotalRepeats;          // 최소 총 혼합 횟수 (Easy: 2, Medium: 4, Hard: 7)
    public int maxTotalRepeats;          // 최대 총 혼합 횟수 (Easy: 4, Medium: 6, Hard: 10)
    public bool useUnbalancedMix;        // 불균형 혼합 사용 여부 (Hard만 true)

    [Header("장애물 설정")]
    public int minWalls;
    public int maxWalls;
    public bool useSpecialTiles;
    public int specialTileCount;

    [Header("난이도 검증")]
    public int minSolutionMoves;         // 최소 이동 횟수
    public float moveMultiplier;         // 이동 횟수 배수
}

public class StageGenerator : MonoBehaviour
{
    private readonly Color32[] baseColors = {
        new Color32(232, 48, 48, 255),
        new Color32(247, 150, 70, 255),
        new Color32(255, 246, 75, 255),
        new Color32(171, 213, 84, 255),
        new Color32(71, 163, 255, 255),
        new Color32(35, 20, 234, 255),
        new Color32(174, 81, 199, 255)
    };

    [SerializeField] private DifficultyProfile[] difficultyProfiles = new DifficultyProfile[]
    {
        // Easy (난이도 1)
        new DifficultyProfile
        {
            gridSize = new Vector2Int(4, 4),
            minColors = 2, maxColors = 3,
            placementStrategy = PlacementStrategy.Random,  // 랜덤 분산 배치
            minTotalRepeats = 2, maxTotalRepeats = 4,
            useUnbalancedMix = false,
            minWalls = 0, maxWalls = 1,
            useSpecialTiles = true, specialTileCount = 1,  // Reset 타일 1개 추가
            minSolutionMoves = 2, moveMultiplier = 2.5f
        },
        // Medium (난이도 2)
        new DifficultyProfile
        {
            gridSize = new Vector2Int(5, 5),
            minColors = 2, maxColors = 4,
            placementStrategy = PlacementStrategy.Random,  // Random으로 변경 (분산)
            minTotalRepeats = 4, maxTotalRepeats = 6,
            useUnbalancedMix = false,
            minWalls = 1, maxWalls = 2,
            useSpecialTiles = true, specialTileCount = 1,  // Reset 타일 1개 추가
            minSolutionMoves = 6, moveMultiplier = 1.6f
        },
        // Hard (난이도 3)
        new DifficultyProfile
        {
            gridSize = new Vector2Int(6, 6),
            minColors = 3, maxColors = 5,
            placementStrategy = PlacementStrategy.Scattered,  // 최대한 멀리 배치
            minTotalRepeats = 7, maxTotalRepeats = 10,
            useUnbalancedMix = true,
            minWalls = 2, maxWalls = 3,
            useSpecialTiles = true, specialTileCount = 2,  // Reset/Fake 타일 2개
            minSolutionMoves = 10, moveMultiplier = 1.5f
        }
    };

    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    private int GetGridIndex(int x, int y, int width)
    {
        return y * width + x;
    }

    public StageData GenerateStage(int stageId, int difficulty)
    {
        StageData stage = new StageData();
        stage.stageId = stageId;

        // 난이도 및 그리드 설정 (기존과 동일)
        DifficultyProfile profile = difficultyProfiles[Mathf.Clamp(difficulty - 1, 0, difficultyProfiles.Length - 1)];
        stage.gridSize = new GridSize { x = profile.gridSize.x, y = profile.gridSize.y };

        // ---[ 1. 색상 선택 및 팔레트 생성 (기존 로직 유지) ]---
        int colorCount = Random.Range(profile.minColors, profile.maxColors + 1);
        List<int> selectedIndices = new List<int>();
        while (selectedIndices.Count < colorCount)
        {
            int index = Random.Range(0, baseColors.Length);
            if (!selectedIndices.Contains(index)) selectedIndices.Add(index);
        }

        stage.colorPalette = new ColorPaletteEntry[colorCount];
        for (int i = 0; i < colorCount; i++)
        {
            stage.colorPalette[i] = new ColorPaletteEntry
            {
                id = (i + 1).ToString(),
                rgb = new int[] { baseColors[selectedIndices[i]].r, baseColors[selectedIndices[i]].g, baseColors[selectedIndices[i]].b }
            };
        }

        // ---[ 2. 목표 색상 생성 (랜덤 혼합 횟수) ]---
        int totalRepeats = Random.Range(profile.minTotalRepeats, profile.maxTotalRepeats + 1);
        List<Color32> targetMix = new List<Color32>();

        if (profile.useUnbalancedMix && colorCount >= 2)
        {
            // Hard 모드: 불균형 혼합 (한 색상이 지배적)
            // 예: totalRepeats=8, colorCount=3 → [5, 2, 1] 또는 [6, 1, 1]
            int dominantIndex = Random.Range(0, colorCount);

            // 남은 반복 횟수를 다른 색상들에게 분배
            int remainingRepeats = totalRepeats;
            int[] colorRepeats = new int[colorCount];

            // 지배 색상은 최소 절반 이상
            int dominantMin = Mathf.Max(totalRepeats / 2, totalRepeats - (colorCount - 1));
            colorRepeats[dominantIndex] = Random.Range(dominantMin, totalRepeats - (colorCount - 1) + 1);
            remainingRepeats -= colorRepeats[dominantIndex];

            // 나머지 색상들에게 랜덤 분배
            for (int i = 0; i < colorCount; i++)
            {
                if (i != dominantIndex)
                {
                    int maxForThis = Mathf.Min(remainingRepeats, remainingRepeats - (colorCount - i - 1 - (i < dominantIndex ? 1 : 0)));
                    colorRepeats[i] = Random.Range(1, maxForThis + 1);
                    remainingRepeats -= colorRepeats[i];
                }
            }

            // 남은 횟수가 있으면 랜덤 색상에 추가
            while (remainingRepeats > 0)
            {
                int randomColorIndex = Random.Range(0, colorCount);
                colorRepeats[randomColorIndex]++;
                remainingRepeats--;
            }

            // 목표 색상 리스트 생성
            for (int i = 0; i < colorCount; i++)
            {
                Color32 color = ColorMixer.ArrayToColor(stage.colorPalette[i].rgb);
                for (int j = 0; j < colorRepeats[i]; j++)
                {
                    targetMix.Add(color);
                }
            }
        }
        else
        {
            // Easy/Medium 모드: 균등 혼합
            // 예: totalRepeats=5, colorCount=2 → [3, 2] 또는 [2, 3]
            int[] colorRepeats = new int[colorCount];
            int baseRepeat = totalRepeats / colorCount;
            int remainder = totalRepeats % colorCount;

            for (int i = 0; i < colorCount; i++)
            {
                colorRepeats[i] = baseRepeat;
            }

            // 나머지를 랜덤 색상에 분배
            for (int i = 0; i < remainder; i++)
            {
                int randomIndex = Random.Range(0, colorCount);
                colorRepeats[randomIndex]++;
            }

            // 목표 색상 리스트 생성
            for (int i = 0; i < colorCount; i++)
            {
                Color32 color = ColorMixer.ArrayToColor(stage.colorPalette[i].rgb);
                for (int j = 0; j < colorRepeats[i]; j++)
                {
                    targetMix.Add(color);
                }
            }
        }

        Color32 targetColor = ColorMixer.MixColors(targetMix);
        stage.colorClear = new int[] { targetColor.r, targetColor.g, targetColor.b };

        // 디버그: 목표 혼합 정보 출력
        string mixInfo = $"Stage {stageId} 목표: 총 {totalRepeats}번 혼합 [";
        if (profile.useUnbalancedMix && colorCount >= 2)
        {
            // Hard 모드
            int[] colorRepeats = new int[colorCount];
            foreach (var color in targetMix)
            {
                for (int i = 0; i < colorCount; i++)
                {
                    Color32 paletteColor = ColorMixer.ArrayToColor(stage.colorPalette[i].rgb);
                    if (color.r == paletteColor.r && color.g == paletteColor.g && color.b == paletteColor.b)
                    {
                        colorRepeats[i]++;
                        break;
                    }
                }
            }
            for (int i = 0; i < colorCount; i++)
            {
                mixInfo += $"색{i + 1}:{colorRepeats[i]}회 ";
            }
        }
        else
        {
            // Easy/Medium 모드
            int[] colorRepeats = new int[colorCount];
            int baseRepeat = totalRepeats / colorCount;
            int remainder = totalRepeats % colorCount;
            for (int i = 0; i < colorCount; i++)
            {
                colorRepeats[i] = baseRepeat;
            }
            for (int i = 0; i < remainder; i++)
            {
                int randomIndex = Random.Range(0, colorCount);
                colorRepeats[randomIndex]++;
            }
            for (int i = 0; i < colorCount; i++)
            {
                mixInfo += $"색{i + 1}:{colorRepeats[i]}회 ";
            }
        }
        mixInfo += "]";
        Debug.Log(mixInfo);

        // ==========================================================
        // [검증 핵심] 차선책(Best Candidate) 확보 및 디버깅
        // ==========================================================
        
        StageData bestCandidate = null; // 가장 나은 맵 저장
        int bestMoves = -1;
        
        int maxAttempts = 20; // 횟수 조금 증가

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 1. 배치 시도
            if (TryGenerateGrid(stage, profile, selectedIndices))
            {
                // 2. 솔루션 검증
                AStarPathfinder pathfinder = new AStarPathfinder();
                SolutionData solution = pathfinder.FindSolution(stage);

                // [디버그] 실패 원인 분석용 로그
                if (solution == null)
                {
                    Debug.LogWarning($"[생성 실패 {attempt}] A* 경로 없음 (Impossible Map)");
                }
                else
                {
                    // 디버그: A* 솔루션 수집 정보
                    if (solution.stateHistory.Count > 0)
                    {
                        var finalState = solution.stateHistory[solution.stateHistory.Count - 1];
                        Debug.Log($"[시도 {attempt}] A* 솔루션: {solution.totalMoves}이동, 수집: {finalState.collectedColorIds.Count}번 [{string.Join(", ", finalState.collectedColorIds)}]");
                    }

                    // 현재 맵이 최소 조건은 만족 못 해도, 지금까지 중 최고라면 저장 (깊은 복사)
                    if (solution.totalMoves > bestMoves)
                    {
                        bestMoves = solution.totalMoves;
                        string json = JsonUtility.ToJson(stage); // 간편한 깊은 복사
                        bestCandidate = JsonUtility.FromJson<StageData>(json);
                        bestCandidate.solutionMoves = solution.totalMoves;
                    }

                    // 3. 난이도 검증 (Pass 조건)
                    if (solution.totalMoves >= profile.minSolutionMoves)
                    {
                        // 성공!
                        FinalizeStage(bestCandidate, profile, difficulty, colorCount);
                        Debug.Log($"<color=green>스테이지 {stageId} 생성 성공!</color> (시도: {attempt + 1}, 이동: {solution.totalMoves})");
                        return bestCandidate;
                    }
                    else
                    {
                        Debug.LogWarning($"[생성 실패 {attempt}] 너무 쉬움 (Move: {solution.totalMoves} < Min: {profile.minSolutionMoves})");
                    }
                }
            }
        }

        // ---[ 3. 최후의 수단 (Fallback) ]---
        // 반복 횟수를 다 썼는데도 완벽한 맵이 없으면, 차선책이라도 반환 (Easy 모드 구원)
        if (bestCandidate != null)
        {
            Debug.LogError($"스테이지 {stageId}: 조건 충족 실패. 차선책(이동 {bestMoves})을 반환합니다.");
            FinalizeStage(bestCandidate, profile, difficulty, colorCount);
            return bestCandidate;
        }

        Debug.LogError($"스테이지 {stageId} 생성 완전 실패. (A*가 모든 맵에서 null을 반환함)");
        return null;
    }

    // 메타데이터 설정 헬퍼 함수
    private void FinalizeStage(StageData stage, DifficultyProfile profile, int difficulty, int colorCount)
    {
        stage.maxMoves = Mathf.CeilToInt(stage.solutionMoves * profile.moveMultiplier);
        // 이동 횟수가 너무 적으면 최소값 보정 (Easy 모드 보호)
        if (stage.maxMoves < 2) stage.maxMoves = 3; 

        stage.difficulty = difficulty;
        stage.placementStrategy = profile.placementStrategy.ToString();
        stage.colorCount = colorCount;
        stage.wallCount = CountWalls(stage);
        stage.hasSpecialTiles = profile.useSpecialTiles && profile.specialTileCount > 0;
        stage.createdDate = System.DateTime.UtcNow.ToString("o");
    }

    private bool TryGenerateGrid(StageData stage, DifficultyProfile profile, List<int> selectedIndices)
    {
        stage.gridData = new int[stage.gridSize.x * stage.gridSize.y];

        // 색상 타일 배치
        foreach (var colorEntry in stage.colorPalette)
        {
            if (!PlaceColorTileWithStrategy(stage, int.Parse(colorEntry.id), profile.placementStrategy))
                return false;
        }

        // 플레이어 배치
        List<Vector2Int> emptyTiles = GetEmptyTiles(stage);
        if (emptyTiles.Count == 0) return false;

        ShuffleList(emptyTiles);
        Vector2Int startPos = emptyTiles[0];
        stage.playerStartPosition = new GridPosition { x = startPos.x, y = startPos.y };
        emptyTiles.RemoveAt(0);

        // 벽 배치
        int wallCount = Random.Range(profile.minWalls, profile.maxWalls + 1);
        for (int i = 0; i < wallCount && emptyTiles.Count > 0; i++)
        {
            int idx = GetGridIndex(emptyTiles[0].x, emptyTiles[0].y, stage.gridSize.x);
            stage.gridData[idx] = -1;
            emptyTiles.RemoveAt(0);
        }

        // 특수 타일 배치 (Reset 우선, 나머지는 랜덤)
        if (profile.useSpecialTiles && profile.specialTileCount > 0)
        {
            for (int i = 0; i < profile.specialTileCount && emptyTiles.Count > 0; i++)
            {
                int idx = GetGridIndex(emptyTiles[0].x, emptyTiles[0].y, stage.gridSize.x);

                if (i == 0)
                {
                    // 첫 번째는 무조건 Reset 타일
                    stage.gridData[idx] = -2;
                }
                else
                {
                    // 두 번째부터는 Reset 또는 FakeColor 랜덤
                    if (Random.Range(0, 2) == 0)
                    {
                        stage.gridData[idx] = -2;  // Reset
                    }
                    else
                    {
                        List<int> unusedIndices = Enumerable.Range(0, baseColors.Length).Except(selectedIndices).ToList();
                        if (unusedIndices.Count > 0)
                        {
                            Color32 fake = baseColors[unusedIndices[Random.Range(0, unusedIndices.Count)]];
                            stage.fakeColorRgb = new int[] { fake.r, fake.g, fake.b };
                            stage.gridData[idx] = -3;  // FakeColor
                        }
                        else
                        {
                            stage.gridData[idx] = -2;  // Fake 불가능하면 Reset
                        }
                    }
                }
                emptyTiles.RemoveAt(0);
            }
        }

        return true;
    }

    private bool PlaceColorTileWithStrategy(StageData stage, int colorId, PlacementStrategy strategy)
    {
        List<Vector2Int> candidates = GetEmptyTiles(stage);
        if (candidates.Count == 0) return false;

        ShuffleList(candidates);

        switch (strategy)
        {
            case PlacementStrategy.Clustered:
                return PlaceClustered(stage, colorId, candidates);
            case PlacementStrategy.Scattered:
                return PlaceScattered(stage, colorId, candidates);
            case PlacementStrategy.Linear:
                return PlaceLinear(stage, colorId, candidates);
            case PlacementStrategy.Random:
            default:
                foreach (var pos in candidates)
                {
                    if (IsSpacedFromOtherColors(stage, pos.x, pos.y))
                    {
                        stage.gridData[GetGridIndex(pos.x, pos.y, stage.gridSize.x)] = colorId;
                        return true;
                    }
                }
                stage.gridData[GetGridIndex(candidates[0].x, candidates[0].y, stage.gridSize.x)] = colorId;
                return true;
        }
    }

    private bool PlaceClustered(StageData stage, int colorId, List<Vector2Int> candidates)
    {
        List<Vector2Int> existing = GetColorTilePositions(stage);
        if (existing.Count == 0)
        {
            stage.gridData[GetGridIndex(candidates[0].x, candidates[0].y, stage.gridSize.x)] = colorId;
            return true;
        }

        var bestPos = candidates
            .OrderBy(c => existing.Min(e => Vector2Int.Distance(c, e)))
            .First();

        stage.gridData[GetGridIndex(bestPos.x, bestPos.y, stage.gridSize.x)] = colorId;
        return true;
    }

    private bool PlaceScattered(StageData stage, int colorId, List<Vector2Int> candidates)
    {
        List<Vector2Int> existing = GetColorTilePositions(stage);
        if (existing.Count == 0)
        {
            stage.gridData[GetGridIndex(candidates[0].x, candidates[0].y, stage.gridSize.x)] = colorId;
            return true;
        }

        var bestCandidate = candidates
            .Select(c => new { Pos = c, MinDist = existing.Min(e => Vector2Int.Distance(c, e)) })
            .OrderByDescending(x => x.MinDist)
            .First();

        stage.gridData[GetGridIndex(bestCandidate.Pos.x, bestCandidate.Pos.y, stage.gridSize.x)] = colorId;
        return true;
    }

    private bool PlaceLinear(StageData stage, int colorId, List<Vector2Int> candidates)
    {
        List<Vector2Int> existing = GetColorTilePositions(stage);
        if (existing.Count == 0)
        {
            stage.gridData[GetGridIndex(candidates[0].x, candidates[0].y, stage.gridSize.x)] = colorId;
            return true;
        }

        Vector2Int lastPos = existing.Last();

        var linearCandidates = candidates
            .Where(c => c.x == lastPos.x || c.y == lastPos.y)
            .OrderBy(c => Vector2Int.Distance(c, lastPos))
            .ToList();

        if (linearCandidates.Count > 0)
        {
            stage.gridData[GetGridIndex(linearCandidates[0].x, linearCandidates[0].y, stage.gridSize.x)] = colorId;
            return true;
        }

        return PlaceScattered(stage, colorId, candidates);
    }

    private bool IsSpacedFromOtherColors(StageData stage, int x, int y)
    {
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            if (nx >= 0 && nx < stage.gridSize.x && ny >= 0 && ny < stage.gridSize.y)
            {
                if (stage.gridData[GetGridIndex(nx, ny, stage.gridSize.x)] > 0) return false;
            }
        }
        return true;
    }

    private List<Vector2Int> GetEmptyTiles(StageData stage)
    {
        List<Vector2Int> emptyTiles = new List<Vector2Int>();
        for (int y = 0; y < stage.gridSize.y; y++)
        {
            for (int x = 0; x < stage.gridSize.x; x++)
            {
                if (stage.gridData[GetGridIndex(x, y, stage.gridSize.x)] == 0)
                {
                    emptyTiles.Add(new Vector2Int(x, y));
                }
            }
        }
        return emptyTiles;
    }

    private List<Vector2Int> GetColorTilePositions(StageData stage)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int i = 0; i < stage.gridData.Length; i++)
        {
            if (stage.gridData[i] > 0)
                positions.Add(new Vector2Int(i % stage.gridSize.x, i / stage.gridSize.x));
        }
        return positions;
    }

    private int CountWalls(StageData stage)
    {
        return stage.gridData.Count(x => x == -1);
    }
}