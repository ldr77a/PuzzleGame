using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StageGenerator : MonoBehaviour
{
    // 기본 색상 팔레트 (빨, 주, 노, 연두, 파, 남, 보)
    private readonly Color32[] baseColors = {
        new Color32(232, 48, 48, 255),    // 1: 빨강
        new Color32(247, 150, 70, 255),   // 2: 주황
        new Color32(255, 246, 75, 255),   // 3: 노랑
        new Color32(171, 213, 84, 255),   // 4: 연두
        new Color32(71, 163, 255, 255),    // 5: 파랑
        new Color32(35, 20, 234, 255),    // 6: 남색
        new Color32(174, 81, 199, 255)    // 7: 보라
    };

    // 1D 배열 인덱싱 헬퍼 함수
    private int GetGridIndex(int x, int y, int width)
    {
        return y * width + x;
    }

    public StageData GenerateStage(int stageId, int difficulty)
    {
        StageData stage = new StageData();
        stage.stageId = stageId;

        // 1. 그리드 크기 결정 (난이도별)
        stage.gridSize = new GridSize();
        if (difficulty == 1) // Easy
        {
            stage.gridSize.x = 4;
            stage.gridSize.y = 4;
        }
        else if (difficulty == 2) // Medium
        {
            stage.gridSize.x = 5;
            stage.gridSize.y = 5;
        }
        else // Hard
        {
            stage.gridSize.x = 6;
            stage.gridSize.y = 6;
        }

        // 2. 색상 선택 (2-3개)
        int colorCount = Random.Range(2, 4);
        List<int> selectedIndices = new List<int>();

        for (int i = 0; i < colorCount; i++)
        {
            int index = Random.Range(0, baseColors.Length);
            while (selectedIndices.Contains(index))
            {
                index = Random.Range(0, baseColors.Length);
            }
            selectedIndices.Add(index);
        }

        // 3. 색상 팔레트 생성
        stage.colorPalette = new ColorPaletteEntry[colorCount];
        for (int i = 0; i < colorCount; i++)
        {
            stage.colorPalette[i] = new ColorPaletteEntry
            {
                id = (i + 1).ToString(),
                rgb = new int[] {
                    baseColors[selectedIndices[i]].r,
                    baseColors[selectedIndices[i]].g,
                    baseColors[selectedIndices[i]].b
                }
            };
        }

        // 4. 목표 색상 생성 (최소 2개 색상 조합)
        List<Color32> targetMix = new List<Color32>();
        for (int i = 0; i < colorCount; i++)
        {
            int repeatCount = Random.Range(1, 3);
            for (int j = 0; j < repeatCount; j++)
            {
                targetMix.Add(ColorMixer.ArrayToColor(stage.colorPalette[i].rgb));
            }
        }

        Color32 targetColor = ColorMixer.MixColors(targetMix);
        stage.colorClear = new int[] { targetColor.r, targetColor.g, targetColor.b };

        // 5. 그리드 생성 (최대 10회 재시도)
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (TryGenerateGrid(stage, difficulty, selectedIndices))
            {
                // 6. A* 검증
                AStarPathfinder pathfinder = new AStarPathfinder();
                SolutionData solution = pathfinder.FindSolution(stage);

                if (solution != null)
                {
                    // 7. 이동 횟수 설정 (솔루션 * 1.5배)
                    stage.maxMoves = Mathf.CeilToInt(solution.totalMoves * 1.5f);
                    Debug.Log($"스테이지 {stageId} 생성 성공! (솔루션: {solution.totalMoves}이동)");
                    return stage;
                }
            }
        }

        Debug.LogWarning($"스테이지 {stageId} 생성 실패 (10회 시도)");
        return null;
    }

    private bool TryGenerateGrid(StageData stage, int difficulty, List<int> selectedIndices)
    {
        int width = stage.gridSize.x;
        int height = stage.gridSize.y;

        // 그리드 초기화 (1D 배열, 모두 0)
        stage.gridData = new int[width * height];

        // 색상 타일 배치
        foreach (var colorEntry in stage.colorPalette)
        {
            if (!PlaceColorTile(stage, int.Parse(colorEntry.id)))
            {
                return false;
            }
        }

        // 플레이어 시작 위치 (빈 타일)
        List<Vector2Int> emptyTiles = GetEmptyTiles(stage);
        if (emptyTiles.Count == 0) return false;

        Vector2Int startPos = emptyTiles[Random.Range(0, emptyTiles.Count)];
        stage.playerStartPosition = new GridPosition { x = startPos.x, y = startPos.y };

        // 기믹 추가 (난이도별)
        if (difficulty >= 2)
        {
            PlaceWalls(stage, difficulty - 1);
        }

        if (difficulty >= 3)
        {
            PlaceResetOrFakeTile(stage, selectedIndices);
        }

        return true;
    }

    private bool PlaceColorTile(StageData stage, int colorId)
    {
        int width = stage.gridSize.x;
        int height = stage.gridSize.y;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            int index = GetGridIndex(x, y, width);

            if (stage.gridData[index] == 0 && IsSpacedFromOtherColors(stage, x, y))
            {
                stage.gridData[index] = colorId;
                return true;
            }
        }

        return false;
    }

    private bool IsSpacedFromOtherColors(StageData stage, int x, int y)
    {
        // 상하좌우 인접 타일 검사
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (nx >= 0 && nx < stage.gridSize.x && ny >= 0 && ny < stage.gridSize.y)
            {
                int index = GetGridIndex(nx, ny, stage.gridSize.x);
                if (stage.gridData[index] > 0)
                {
                    return false; // 인접한 색상 타일 있음
                }
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
                int index = GetGridIndex(x, y, stage.gridSize.x);
                if (stage.gridData[index] == 0)
                {
                    emptyTiles.Add(new Vector2Int(x, y));
                }
            }
        }

        return emptyTiles;
    }

    private void PlaceWalls(StageData stage, int count)
    {
        List<Vector2Int> emptyTiles = GetEmptyTiles(stage);

        for (int i = 0; i < count && emptyTiles.Count > 0; i++)
        {
            int index = Random.Range(0, emptyTiles.Count);
            Vector2Int pos = emptyTiles[index];
            int gridIndex = GetGridIndex(pos.x, pos.y, stage.gridSize.x);
            stage.gridData[gridIndex] = -1; // 벽
            emptyTiles.RemoveAt(index);
        }
    }

    private void PlaceResetOrFakeTile(StageData stage, List<int> selectedIndices)
    {
        List<Vector2Int> emptyTiles = GetEmptyTiles(stage);
        if (emptyTiles.Count == 0) return;

        Vector2Int pos = emptyTiles[Random.Range(0, emptyTiles.Count)];
        int gridIndex = GetGridIndex(pos.x, pos.y, stage.gridSize.x);
        
        // 50% 확률로 Reset 또는 FakeColor 선택
        if (Random.Range(0, 2) == 0)
        {
            // Reset 타일
            stage.gridData[gridIndex] = -2;
        }
        else
        {
            // FakeColor 타일: 사용되지 않은 색상 팔레트 중 하나 선택
            List<int> unusedIndices = new List<int>();
            for (int i = 0; i < baseColors.Length; i++)
            {
                if (!selectedIndices.Contains(i))
                {
                    unusedIndices.Add(i);
                }
            }

            if (unusedIndices.Count > 0)
            {
                // 사용되지 않은 색상 중 랜덤 선택
                int fakeColorIndex = unusedIndices[Random.Range(0, unusedIndices.Count)];
                Color32 fakeColor = baseColors[fakeColorIndex];
                
                // FakeColor 정보를 StageData에 저장
                stage.fakeColorRgb = new int[] { fakeColor.r, fakeColor.g, fakeColor.b };
                stage.gridData[gridIndex] = -3;
            }
            else
            {
                // 사용되지 않은 색상이 없으면 Reset 타일로 대체
                stage.gridData[gridIndex] = -2;
            }
        }
    }
}
