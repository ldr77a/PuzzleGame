using UnityEngine;
using UnityEngine.UI;

public class Tile : MonoBehaviour
{
    [Header("타일 정보")]
    public int gridX;
    public int gridY;
    public TileType tileType;
    public Color32 tileColor;

    [Header("컴포넌트")]
    public Image imageComponent;

    private bool hasPlayer = false;

    public void Initialize(int x, int y, int gridValue, System.Collections.Generic.Dictionary<string, Color32> palette, Color32? fakeColor = null)
    {
        gridX = x;
        gridY = y;

        if (imageComponent == null)
        {
            imageComponent = GetComponent<Image>();
        }

        // gridValue에 따라 타일 타입 결정
        if (gridValue == -1)
        {
            tileType = TileType.Wall;
            imageComponent.color = Color.black;
        }
        else if (gridValue == -2)
        {
            tileType = TileType.Reset;
            imageComponent.color = Color.white;
            // TODO: 리셋 아이콘 추가
        }
        else if (gridValue == -3)
        {
            tileType = TileType.FakeColor;
            
            // FakeColor는 사용되지 않은 색상을 표시
            if (fakeColor.HasValue)
            {
                tileColor = fakeColor.Value;
                imageComponent.color = tileColor;
            }
            else
            {
                // fakeColor가 없으면 회색으로 표시
                imageComponent.color = Color.gray;
            }
        }
        else if (gridValue > 0)
        {
            tileType = TileType.ColorTile;
            string colorId = gridValue.ToString();

            if (palette.ContainsKey(colorId))
            {
                tileColor = palette[colorId];
                imageComponent.color = tileColor;
            }
        }
        else
        {
            tileType = TileType.Empty;
            imageComponent.color = new Color(0.9f, 0.9f, 0.9f); // 밝은 회색
        }
    }

    public bool IsWalkable()
    {
        return tileType != TileType.Wall;
    }

    public void SetPlayerPresent(bool present)
    {
        hasPlayer = present;
        // TODO: 플레이어 위치 시각적 표시 (테두리 등)
    }
}
