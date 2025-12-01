public enum TileType
{
    Empty = 0,       // 빈 타일 (이동 가능)
    ColorTile = 1,   // 색상 타일 (양수 ID)
    Wall = -1,       // 벽 (이동 불가)
    Reset = -2,      // 리셋 타일 (색상 초기화)
    FakeColor = -3   // 가짜 색상 타일
}
