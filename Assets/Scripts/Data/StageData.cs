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
}

[Serializable]
public class StageDataWrapper
{
    public StageData stage;
}
