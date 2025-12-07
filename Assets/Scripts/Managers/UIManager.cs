using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private Image playerColorDisplay;   // PlayerColor
    [SerializeField] private Image targetColorDisplay;   // TargetColor
    [SerializeField] private TextMeshProUGUI movesText;  // MoveText
    [SerializeField] private TextMeshProUGUI stageIdText; // StageID 텍스트
    // [SerializeField] private TextMeshProUGUI matchPercentageText;  // 일치도 표시 (미사용)

    [Header("결과 패널")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private Button replayButton;  // ★ NEW: Replay 버튼

    [Header("Solution 버튼")]
    [SerializeField] private Button solutionButton;
    [SerializeField] private SolutionVisualizer solutionVisualizer;
    [SerializeField] private GridManager gridManager;

    private Color32 targetColor;
    private bool isSolutionShown = false;
    private StageData currentStageData;

    public void Initialize(Color32 targetColorParam, int maxMoves, int stageId, StageData stageData)
    {
        targetColor = targetColorParam;
        currentStageData = stageData;
        targetColorDisplay.color = targetColor;
        UpdatePlayerColor(new Color32(255, 255, 255, 255)); // 흰색
        UpdateMovesCounter(maxMoves);
        UpdateStageId(stageId);

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // ★ NEW: Replay 버튼 초기화
        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(OnReplayClicked);
            replayButton.gameObject.SetActive(false); // 초기에는 숨김
        }

        // ★ NEW: Solution 버튼 초기화
        if (solutionButton != null)
        {
            solutionButton.onClick.RemoveAllListeners();
            solutionButton.onClick.AddListener(OnSolutionButtonClicked);
        }

        isSolutionShown = false;
    }

    public void UpdatePlayerColor(Color32 color)
    {
        playerColorDisplay.color = color;
        
        /* 일치도 계산 및 표시 (미사용)
        if (matchPercentageText != null)
        {
            float matchPercentage = CalculateColorMatchPercentage(color, targetColor);
            matchPercentageText.text = $"일치도: {matchPercentage:F1}%";
            
            // 피드백 텍스트 추가
            if (matchPercentage >= 95f)
            {
                matchPercentageText.text += " - ACCURATE!";
                matchPercentageText.color = new Color32(0, 255, 0, 255); // 초록색
            }
            else if (matchPercentage >= 80f)
            {
                matchPercentageText.text += " - GOOD";
                matchPercentageText.color = new Color32(255, 200, 0, 255); // 노란색
            }
            else
            {
                matchPercentageText.color = Color.white;
            }
        }
        */
    }
    
    /* 일치도 계산 함수 (미사용)
    private float CalculateColorMatchPercentage(Color32 current, Color32 target)
    {
        // RGB 각 채널의 차이를 계산 (0-255 범위)
        float rDiff = Mathf.Abs(current.r - target.r);
        float gDiff = Mathf.Abs(current.g - target.g);
        float bDiff = Mathf.Abs(current.b - target.b);
        
        // 평균 차이 계산
        float avgDiff = (rDiff + gDiff + bDiff) / 3f;
        
        // 일치도 퍼센트로 변환 (0-100%)
        float matchPercentage = (1f - (avgDiff / 255f)) * 100f;
        
        return Mathf.Clamp(matchPercentage, 0f, 100f);
    }
    */

    public void UpdateMovesCounter(int remainingMoves)
    {
        movesText.text = $"Moves: {remainingMoves}";
    }

    public void UpdateStageId(int stageId)
    {
        if (stageIdText != null)
        {
            stageIdText.text = $"StageID: {stageId}";
        }
    }

    public void ShowWinScreen()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        // ★ NEW: Replay 버튼 활성화
        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(true);
        }
    }

    public void ShowLoseScreen()
    {
        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }

        // ★ NEW: Replay 버튼 활성화
        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(true);
        }
    }

    // ★ NEW: Replay 버튼 클릭 핸들러
    private void OnReplayClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartStage();
            Debug.Log("스테이지 재시작!");
        }
        else
        {
            Debug.LogWarning("GameManager를 찾을 수 없습니다!");
        }
    }

    // ★ NEW: Solution 버튼 클릭 핸들러
    private void OnSolutionButtonClicked()
    {
        if (isSolutionShown)
        {
            HideSolutionVisualization();
        }
        else
        {
            ShowSolutionVisualization();
        }
    }

    private void ShowSolutionVisualization()
    {
        if (currentStageData == null || solutionVisualizer == null || gridManager == null)
        {
            Debug.LogWarning("Solution 시각화에 필요한 컴포넌트가 없습니다!");
            return;
        }

        // A* 경로 계산
        AStarPathfinder pathfinder = new AStarPathfinder();
        SolutionData solution = pathfinder.FindSolution(currentStageData);

        if (solution != null)
        {
            // Solution 시각화
            solutionVisualizer.ShowSolution(
                solution,
                currentStageData,
                gridManager.GetTiles(),
                gridManager.GetGridContainer()
            );
            isSolutionShown = true;
            Debug.Log("풀이 표시됨!");
        }
        else
        {
            Debug.LogWarning("풀이를 계산할 수 없습니다!");
        }
    }

    private void HideSolutionVisualization()
    {
        if (solutionVisualizer != null)
        {
            solutionVisualizer.HideSolution();
        }

        // 모든 타일의 수집 횟수 초기화
        if (gridManager != null)
        {
            Tile[,] tiles = gridManager.GetTiles();
            if (tiles != null)
            {
                for (int x = 0; x < tiles.GetLength(0); x++)
                {
                    for (int y = 0; y < tiles.GetLength(1); y++)
                    {
                        if (tiles[x, y] != null)
                        {
                            tiles[x, y].ClearCollectionCount();
                        }
                    }
                }
            }
        }

        isSolutionShown = false;
        Debug.Log("풀이 숨김!");
    }
}
