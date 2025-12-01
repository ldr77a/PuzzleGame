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
        currentStageData = stageLoader.LoadStage(stageId);

        if (currentStageData == null)
        {
            Debug.LogError($"스테이지 {stageId} 로드 실패");
            return;
        }

        targetColor = ColorMixer.ArrayToColor(currentStageData.colorClear);

        // 시스템 초기화 순서
        gridManager.GenerateGrid(currentStageData);
        playerController.Initialize(currentStageData);
        uiManager.Initialize(targetColor, currentStageData.maxMoves);

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
