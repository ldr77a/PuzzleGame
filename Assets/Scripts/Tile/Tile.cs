using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Tile : MonoBehaviour
{
    [Header("타일 정보")]
    public int gridX;
    public int gridY;
    public TileType tileType;
    public Color32 tileColor;

    [Header("컴포넌트")]
    public Image imageComponent;
    [SerializeField] private TextMeshProUGUI countText;

    private bool hasPlayer = false;

    public void Initialize(int x, int y, int gridValue, System.Collections.Generic.Dictionary<string, Color32> palette, Color32 fakeColor)
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
            Sprite wallSprite = Resources.Load<Sprite>("Image/Wall");
            if (wallSprite != null)
            {
                imageComponent.sprite = wallSprite;
                imageComponent.color = Color.white;
            }
        }
        else if (gridValue == -2)
        {
            tileType = TileType.Reset;
            Sprite resetSprite = Resources.Load<Sprite>("Image/Reset");
            if (resetSprite != null)
            {
                imageComponent.sprite = resetSprite;
                imageComponent.color = Color.white; 
            }
        }
        else if (gridValue == -3)
        {
            tileType = TileType.FakeColor;
            tileColor = fakeColor;
            imageComponent.color = tileColor;
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
            imageComponent.color = new Color(1f, 1f, 1f);
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

    public void SetCollectionCount(int count)
    {
        if (countText != null && count > 0)
        {
            countText.text = count.ToString();
            countText.gameObject.SetActive(true);
        }
    }

    public void ClearCollectionCount()
    {
        if (countText != null)
        {
            countText.gameObject.SetActive(false);
        }
    }
}
