using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("매니저들")]
    [SerializeField] private StageLoader stageLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private UIManager uiManager;

    [Header("설정")]
    [SerializeField] private int currentStageId = 1;

    private StageData currentStageData;
    private Color32 targetColor;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        StartStage(currentStageId);
    }

    public void StartStage(int stageId)
    {
        // StageLoader 시도 (실패하면 JSON 직접 로드)
        currentStageData = stageLoader != null ? stageLoader.LoadStage(stageId) : null;

        if (currentStageData == null)
        {
            // JSON 리소스에서 직접 로드 시도
            TextAsset stageFile = Resources.Load<TextAsset>($"Stages/stage_{stageId:000}");
            if (stageFile != null)
            {
                try
                {
                    currentStageData = JsonUtility.FromJson<StageData>(stageFile.text);
                }
                catch
                {
                    Debug.LogError($"스테이지 JSON 파싱 실패: {stageId}");
                }
            }
            else
            {
                Debug.LogError($"스테이지 파일 로드 실패: stage_{stageId:000}");
                return;
            }
        }

        if (currentStageData == null)
        {
            Debug.LogError($"스테이지 {stageId} 로드 실패");
            return;
        }

        targetColor = ColorMixer.ArrayToColor(currentStageData.colorClear);

        // ★ NEW: 기존 이벤트 구독 해제
        playerController.OnColorChanged -= uiManager.UpdatePlayerColor;
        playerController.OnMovesChanged -= uiManager.UpdateMovesCounter;
        playerController.OnWin -= HandleWin;
        playerController.OnLose -= HandleLose;

        // ★ NEW: PlayerController 다시 활성화 (Replay 시)
        playerController.enabled = true;

        // 시스템 초기화 순서
        gridManager.GenerateGrid(currentStageData);
        playerController.Initialize(currentStageData);
        uiManager.Initialize(targetColor, currentStageData.maxMoves, currentStageData.stageId);

        // 이벤트 구독
        playerController.OnColorChanged += uiManager.UpdatePlayerColor;
        playerController.OnMovesChanged += uiManager.UpdateMovesCounter;
        playerController.OnWin += HandleWin;
        playerController.OnLose += HandleLose;

        Debug.Log($"스테이지 {stageId} 시작!");
    }

    public Color32 GetTargetColor()
    {
        return targetColor;
    }

    private void HandleWin()
    {
        Debug.Log("승리!");
        uiManager.ShowWinScreen();
        playerController.enabled = false;
    }

    private void HandleLose()
    {
        Debug.Log("패배...");
        uiManager.ShowLoseScreen();
        playerController.enabled = false;
    }

    public void RestartStage()
    {
        StartStage(currentStageData.stageId);
    }

    public void LoadNextStage()
    {
        StartStage(currentStageData.stageId + 1);
    }
}
