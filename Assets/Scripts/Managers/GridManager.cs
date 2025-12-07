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

    // baseColors 배열 (StageGenerator와 동일)
    private readonly Color32[] baseColors = {
        new Color32(232, 48, 48, 255),    // 1: 빨강
        new Color32(247, 150, 70, 255),   // 2: 주황
        new Color32(255, 246, 75, 255),   // 3: 노랑
        new Color32(171, 213, 84, 255),   // 4: 연두
        new Color32(71, 163, 255, 255),   // 5: 파랑
        new Color32(35, 20, 234, 255),    // 6: 남색
        new Color32(174, 81, 199, 255)    // 7: 보라
    };

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

        // FakeColor 색상 준비 (colorPalette에 없는 색상 중 랜덤 선택)
        Color32 fakeColor = GenerateFakeColor(colorPalette);

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

    public void GenerateGrid(StageData data, Transform parent, float cellSize = 50f)
    {
        // 1. 초기화
        foreach (Transform child in parent) Destroy(child.gameObject);

        // 2. 레이아웃 설정
        GridLayoutGroup gridLayout = parent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null) gridLayout = parent.gameObject.AddComponent<GridLayoutGroup>();

        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.spacing = new Vector2(2, 2);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = data.gridSize.x;

        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        // 3. 임시 팔레트 생성 (색상 찾기용)
        Dictionary<string, Color32> tempPalette = new Dictionary<string, Color32>();
        foreach (var entry in data.colorPalette)
        {
            tempPalette[entry.id] = ColorMixer.ArrayToColor(entry.rgb);
        }

        // 4. 타일 생성
        foreach (int tileId in data.gridData)
        {
            GameObject tileObj = Instantiate(tilePrefab, parent);

            // 타일 이미지 컴포넌트 가져오기
            Image img = tileObj.GetComponent<Image>();
            if (img != null)
            {
                // 툴 프리뷰용이니 복잡한 로직 없이 색상만 표현
                if (tileId > 0 && tempPalette.ContainsKey(tileId.ToString()))
                {
                    img.sprite = null;
                    img.color = tempPalette[tileId.ToString()]; // 색상 타일
                }
                else if (tileId == -1)
                {
                    // Wall 이미지 로드
                    Sprite wallSprite = Resources.Load<Sprite>("Image/Wall");
                    if (wallSprite != null)
                    {
                        img.sprite = wallSprite;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = Color.black; // 폴백: 검정색
                    }
                }
                else if (tileId == -2)
                {
                    // Reset 타일 이미지 로드
                    Sprite resetSprite = Resources.Load<Sprite>("Image/Reset");
                    if (resetSprite != null)
                    {
                        img.sprite = resetSprite;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = new Color(0.8f, 0.8f, 0.8f, 1f); // 폴백: 밝은 회색
                    }
                }
                else
                {
                    img.sprite = null;
                    img.color = Color.white; // 빈공간 (흰색)
                }
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

    private Color32 GenerateFakeColor(Dictionary<string, Color32> colorPalette)
    {
        // colorPalette에 사용된 색상들 추출
        List<Color32> usedColors = new List<Color32>(colorPalette.Values);

        // baseColors에서 사용되지 않은 색상들 필터링
        List<Color32> unusedColors = new List<Color32>();
        foreach (Color32 baseColor in baseColors)
        {
            bool isUsed = false;
            foreach (Color32 usedColor in usedColors)
            {
                if (ColorMixer.ColorsMatch(baseColor, usedColor))
                {
                    isUsed = true;
                    break;
                }
            }
            if (!isUsed)
            {
                unusedColors.Add(baseColor);
            }
        }

        // 사용되지 않은 색상 중 랜덤 선택
        if (unusedColors.Count > 0)
        {
            int randomIndex = Random.Range(0, unusedColors.Count);
            return unusedColors[randomIndex];
        }

        // 모든 색상이 사용된 경우 (불가능하지만 안전장치)
        return baseColors[Random.Range(0, baseColors.Length)];
    }

    // ★ NEW: Public getter 메서드들
    public Tile[,] GetTiles()
    {
        return tiles;
    }

    public StageData GetCurrentStage()
    {
        return currentStage;
    }

    public Transform GetGridContainer()
    {
        return gridContainer;
    }
}
