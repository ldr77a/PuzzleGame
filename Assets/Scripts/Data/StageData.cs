using System;
using UnityEngine;

[Serializable]
public class GridSize
{
    public int x;
    public int y;
}

[Serializable]
public class GridPosition
{
    public int x;
    public int y;
}

[Serializable]
public class ColorPaletteEntry
{
    public string id;        // "1", "2", "3"...
    public int[] rgb;        // [R, G, B]
}

[Serializable]
public class StageData
{
    public int stageId;
    public GridSize gridSize;
    public GridPosition playerStartPosition;
    public int maxMoves;
    public int[] colorClear;                    // 목표 색상 RGB
    public ColorPaletteEntry[] colorPalette;    // 사용 색상들
    public int[] gridData;                      // 1D 배열 (row-major order)
    public int[] fakeColorRgb;                  // FakeColor 타일에 사용될 색상 (옵션)
    
    // 메타데이터 (UI 관리용)
    public int difficulty;                      // 1=Easy, 2=Medium, 3=Hard
    public int solutionMoves;                   // A* 솔루션 최소 이동 횟수
    public string placementStrategy;            // "Random", "Clustered", "Scattered", "Linear"
    public int colorCount;                      // 사용된 색상 개수
    public int wallCount;                       // 벽 개수
    public bool hasSpecialTiles;                // 특수 타일 포함 여부
    public string createdDate;                  // 생성 날짜 (ISO 8601)
}

[Serializable]
public class StageDataWrapper
{
    public StageData stage;
}
