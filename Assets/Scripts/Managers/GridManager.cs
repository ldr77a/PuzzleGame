using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("프리팹 및 컨테이너")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private RectTransform gridContainer; // MapGrid UI

    [Header("그리드 설정")]
    [SerializeField] private float tileSize = 50f;
    [SerializeField] private float tileSpacing = 2f;

    private Tile[,] tiles;
    private StageData currentStage;
    private Dictionary<string, Color32> colorPalette;

    public void GenerateGrid(StageData stageData)
    {
        ClearGrid();

        currentStage = stageData;
        int width = stageData.gridSize.x;
        int height = stageData.gridSize.y;

        tiles = new Tile[width, height];

        // 색상 팔레트 빌드
        colorPalette = new Dictionary<string, Color32>();
        foreach (var entry in stageData.colorPalette)
        {
            colorPalette[entry.id] = ColorMixer.ArrayToColor(entry.rgb);
        }

        // FakeColor 색상 준비
        Color32? fakeColor = null;
        if (stageData.fakeColorRgb != null && stageData.fakeColorRgb.Length == 3)
        {
            fakeColor = ColorMixer.ArrayToColor(stageData.fakeColorRgb);
        }

        // GridLayoutGroup 컴포넌트 가져오기
        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        int paddingLeft = 0;
        int paddingRight = 0;
        int paddingTop = 0;
        int paddingBottom = 0;

        if (gridLayout != null)
        {
            // GridLayoutGroup의 설정값을 가져와서 적용
            tileSize = gridLayout.cellSize.x;       // Cell Size X 가져오기
            tileSpacing = gridLayout.spacing.x;     // Spacing X 가져오기

            paddingLeft = gridLayout.padding.left;
            paddingRight = gridLayout.padding.right;
            paddingTop = gridLayout.padding.top;
            paddingBottom = gridLayout.padding.bottom;
            
            // 자동 배치 비활성화 (수동 제어 위함)
            gridLayout.enabled = false; 
        }

        // 그리드 콘텐츠(타일들)의 순수 크기
        float contentWidth = width * (tileSize + tileSpacing) - tileSpacing;
        float contentHeight = height * (tileSize + tileSpacing) - tileSpacing;

        // MapGrid(컨테이너)의 크기 = 콘텐츠 크기 + 패딩
        float totalWidth = contentWidth + paddingLeft + paddingRight;
        float totalHeight = contentHeight + paddingTop + paddingBottom;

        // MapGrid 크기 적용
        RectTransform gridRT = gridContainer.GetComponent<RectTransform>();
        gridRT.sizeDelta = new Vector2(totalWidth, totalHeight);
        
        // 타일들을 중앙 기준으로 배치
        // 전체 크기(total)의 절반에서 패딩을 고려하여 시작 위치 계산
        // 왼쪽 위 기준: -totalWidth/2 + paddingLeft
        // 타일 중심 보정: + tileSize/2
        float startX = -totalWidth / 2 + paddingLeft + tileSize / 2;
        float startY = totalHeight / 2 - paddingTop - tileSize / 2;

        // 타일 생성
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject tileObj = Instantiate(tilePrefab, gridContainer);
                RectTransform rt = tileObj.GetComponent<RectTransform>();

                // 앵커를 중앙으로 강제 설정
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);

                // 타일 위치 설정 (중앙 기준)
                rt.anchoredPosition = new Vector2(
                    startX + x * (tileSize + tileSpacing),
                    startY - y * (tileSize + tileSpacing)
                );
                rt.sizeDelta = new Vector2(tileSize, tileSize);

                // 타일 초기화 (1D 배열 인덱싱: row-major order)
                Tile tile = tileObj.GetComponent<Tile>();
                int index = y * width + x;
                int gridValue = stageData.gridData[index];
                tile.Initialize(x, y, gridValue, colorPalette, fakeColor);

                tiles[x, y] = tile;
            }
        }
    }

    private void CenterGrid(int width, int height)
    {
        float totalWidth = width * (tileSize + tileSpacing) - tileSpacing;
        // float totalHeight = height * (tileSize + tileSpacing) - tileSpacing;

        // X만 중앙 정렬, Y는 그대로 유지
        float currentY = gridContainer.anchoredPosition.y;
        gridContainer.anchoredPosition = new Vector2(-totalWidth / 2, currentY);
    }

    private void ClearGrid()
    {
        if (tiles != null)
        {
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }
        }
    }

    public Tile GetTile(int x, int y)
    {
        if (x < 0 || x >= currentStage.gridSize.x ||
            y < 0 || y >= currentStage.gridSize.y)
        {
            return null;
        }
        return tiles[x, y];
    }

    public bool IsValidMove(int x, int y)
    {
        Tile tile = GetTile(x, y);
        return tile != null && tile.IsWalkable();
    }
}
