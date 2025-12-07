using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SolutionVisualizer : MonoBehaviour
{
    private List<GameObject> pathLines = new List<GameObject>();

    public void ShowSolution(SolutionData solution, StageData stage, Tile[,] tiles, Transform gridContainer)
    {
        // 1. 경로 선 그리기
        for (int i = 0; i < solution.path.Count - 1; i++)
        {
            Vector2Int posA = solution.path[i];
            Vector2Int posB = solution.path[i + 1];

            Tile tileA = tiles[posA.x, posA.y];
            Tile tileB = tiles[posB.x, posB.y];

            if (tileA != null && tileB != null)
            {
                GameObject line = DrawPathLine(
                    tileA.GetComponent<RectTransform>(),
                    tileB.GetComponent<RectTransform>(),
                    gridContainer
                );
                pathLines.Add(line);
            }
        }

        // 2. 색상 수집 횟수 표시
        ShowCollectionCounts(stage, tiles);
    }

    private GameObject DrawPathLine(RectTransform start, RectTransform end, Transform parent)
    {
        // UI Image로 선 생성
        GameObject lineObj = new GameObject("PathLine");
        lineObj.transform.SetParent(parent, false);

        Image lineImage = lineObj.AddComponent<Image>();
        lineImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // 진한 회색, 80% 투명도
        lineImage.raycastTarget = false; // 마우스 이벤트 차단 방지

        RectTransform lineRT = lineImage.rectTransform;

        // 두 타일의 중심점 계산
        Vector2 startPos = start.anchoredPosition;
        Vector2 endPos = end.anchoredPosition;
        Vector2 midPoint = (startPos + endPos) / 2;

        // 거리와 각도 계산
        float distance = Vector2.Distance(startPos, endPos);
        float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;

        // 선 RectTransform 설정
        lineRT.anchorMin = new Vector2(0.5f, 0.5f);
        lineRT.anchorMax = new Vector2(0.5f, 0.5f);
        lineRT.anchoredPosition = midPoint;
        lineRT.sizeDelta = new Vector2(distance, 10f); // 5픽셀 두께로 증가
        lineRT.rotation = Quaternion.Euler(0, 0, angle);

        // 타일 위에 렌더링 (SetAsLastSibling으로 변경)
        lineRT.SetAsLastSibling();

        return lineObj;
    }

    private void ShowCollectionCounts(StageData stage, Tile[,] tiles)
    {
        int width = stage.gridSize.x;
        int height = stage.gridSize.y;

        // solutionColorRepeats 배열을 순회 (colorPalette 순서와 동일)
        for (int i = 0; i < stage.solutionColorRepeats.Length && i < stage.colorPalette.Length; i++)
        {
            string colorId = stage.colorPalette[i].id; // colorPalette의 실제 ID 사용
            int count = stage.solutionColorRepeats[i];

            // 해당 색상 ID를 가진 모든 타일 찾기
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Tile tile = tiles[x, y];
                    if (tile != null && tile.tileType == TileType.ColorTile)
                    {
                        // gridData에서 해당 위치의 색상 ID 확인
                        int gridIndex = y * width + x;
                        int gridValue = stage.gridData[gridIndex];

                        // gridValue가 colorPalette의 ID와 일치하는지 확인
                        if (gridValue > 0 && gridValue.ToString() == colorId)
                        {
                            tile.SetCollectionCount(count);
                        }
                    }
                }
            }
        }
    }

    public void HideSolution()
    {
        // 모든 경로 선 제거
        foreach (GameObject line in pathLines)
        {
            if (line != null)
            {
                Destroy(line);
            }
        }
        pathLines.Clear();
    }
}
