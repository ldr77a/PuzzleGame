using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Image playerVisual; // 플레이어 원형 이미지

    // 상태
    private int currentX;
    private int currentY;
    private List<Color32> collectedColors = new List<Color32>();
    private Color32 currentPlayerColor;
    private int remainingMoves;

    // 이벤트
    public System.Action<Color32> OnColorChanged;
    public System.Action<int> OnMovesChanged;
    public System.Action OnWin;
    public System.Action OnLose;

    public void Initialize(StageData stageData)
    {
        currentX = stageData.playerStartPosition.x;
        currentY = stageData.playerStartPosition.y;
        remainingMoves = stageData.maxMoves;

        collectedColors.Clear();
        currentPlayerColor = new Color32(255, 255, 255, 255); // 흰색

        UpdatePlayerPosition();
        OnColorChanged?.Invoke(currentPlayerColor);
        OnMovesChanged?.Invoke(remainingMoves);
    }

    void Update()
    {
        // 플레이어를 항상 맨 위에 표시 (매 프레임 강제 설정)
        playerVisual.rectTransform.SetAsLastSibling();
        
        // 방향키 입력 (이동 횟수 차감)
        if (Input.GetKeyDown(KeyCode.UpArrow)) TryMove(0, -1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) TryMove(0, 1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) TryMove(-1, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow)) TryMove(1, 0);

        // 스페이스 바 (이동 횟수 차감 안함!)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CollectColor();
        }
    }

    private void TryMove(int deltaX, int deltaY)
    {
        int newX = currentX + deltaX;
        int newY = currentY + deltaY;

        Debug.Log($"Trying to move from ({currentX}, {currentY}) to ({newX}, {newY})");
        
        if (gridManager.IsValidMove(newX, newY))
        {
            Debug.Log($"Move valid!");
            // 이전 위치 표시 제거
            gridManager.GetTile(currentX, currentY).SetPlayerPresent(false);

            currentX = newX;
            currentY = newY;

            UpdatePlayerPosition();
            DecrementMoves(); // 방향키 이동 시에만 차감!

            // 새 위치 표시
            gridManager.GetTile(currentX, currentY).SetPlayerPresent(true);
        }
        else
        {
            Debug.Log($"Move INVALID! Tile: {gridManager.GetTile(newX, newY)}");
        }
    }

    private void CollectColor()
    {
        Tile currentTile = gridManager.GetTile(currentX, currentY);

        if (currentTile.tileType == TileType.ColorTile)
        {
            collectedColors.Add(currentTile.tileColor);
            UpdatePlayerColor();
        }
        else if (currentTile.tileType == TileType.Reset)
        {
            collectedColors.Clear();
            UpdatePlayerColor();
        }
        else if (currentTile.tileType == TileType.FakeColor)
        {
            collectedColors.Add(currentTile.tileColor);
            UpdatePlayerColor();
        }
        // 스페이스 바는 이동 횟수 차감 안함!
    }

    private void UpdatePlayerColor()
    {
        currentPlayerColor = ColorMixer.MixColors(collectedColors);
        OnColorChanged?.Invoke(currentPlayerColor);
        CheckWinCondition();
    }

    private void UpdatePlayerPosition()
    {
        Tile tile = gridManager.GetTile(currentX, currentY);
        if (tile == null) return;

        RectTransform tileRT = tile.GetComponent<RectTransform>();
        RectTransform playerRT = playerVisual.GetComponent<RectTransform>();

        // 1. 부모 설정: MapGrid의 부모(Canvas)로 설정하여 MapGrid와 형제 관계가 되게 함
        // MapGrid 안에 있으면 레이아웃이나 순서 문제가 생길 수 있으므로 밖으로 빼냄
        if (playerRT.parent != tileRT.parent.parent)
        {
            playerRT.SetParent(tileRT.parent.parent);
        }

        // 2. 크기 동기화: 타일 크기에 딱 맞춤 (셀 크기가 변해도 대응 가능)
        playerRT.sizeDelta = tileRT.sizeDelta;

        // 3. 위치 동기화: 월드 좌표 기준 (부모가 달라도 정확히 일치)
        playerRT.position = tileRT.position;
        
        // 4. Z축 0으로 초기화
        Vector3 pos = playerRT.localPosition;
        pos.z = 0;
        playerRT.localPosition = pos;

        // 5. 맨 마지막 순서로 설정 (MapGrid보다 나중에 그려져서 위에 보임)
        playerRT.SetAsLastSibling();
    }

    private void DecrementMoves()
    {
        remainingMoves--;
        OnMovesChanged?.Invoke(remainingMoves);

        if (remainingMoves <= 0)
        {
            OnLose?.Invoke();
        }
    }

    private void CheckWinCondition()
    {
        Color32 targetColor = GameManager.Instance.GetTargetColor();
        if (ColorMixer.ColorsMatch(currentPlayerColor, targetColor))
        {
            OnWin?.Invoke();
        }
    }
}
