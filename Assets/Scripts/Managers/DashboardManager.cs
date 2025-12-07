using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.Search;

public class DashboardManager : MonoBehaviour
{
    [Header("Left Panel")]
    [SerializeField] private Transform listContent;       // LeftPanel > Scroll View > Viewport > Content
    [SerializeField] private GameObject listItemPrefab;   // StageListItem 프리팹
    [SerializeField] private TMP_Dropdown sortDropdown;   // Dropdown_Sort
    [SerializeField] private TMP_Dropdown filterDropdown; // Dropdown_Filter

    [Header("Center Panel")]
    [SerializeField] private Transform mainGridParent;    // CenterPanel > Play 오브젝트 (타일이 생성될 곳)
    [SerializeField] private GridManager gridManager;     // GameManager 오브젝트
    [SerializeField] private GameManager gameManager;     // ★ NEW: 게임 시작용

    // 검증/리셋 버튼 등은 CenterPanel에 있다면 여기에 연결해서 쓰면 됩니다.
    [SerializeField] private Button verifyButton;         // (선택) Btn_Verify

    [Header("Right Panel")]
    [SerializeField] private Transform previewContent;    // RightPanel > PreviewScrollView > Viewport > Content
    [SerializeField] private GameObject previewPrefab;    // MapPreviewItem 프리팹 (Image 하나만 있는 것)

    [SerializeField] private TMP_Text stageInfoText;
    [SerializeField] private TMP_Text solutionText;

    private List<StageData> allStages = new List<StageData>();
    private StageData currentSelectedStage;

    void Start()
    {
        LoadAllStages();
        InitializeDropdowns();

        // 드롭다운 변경 시 리스트 갱신 이벤트 연결
        sortDropdown.onValueChanged.AddListener(delegate { RefreshList(); });
        filterDropdown.onValueChanged.AddListener(delegate { RefreshList(); });

        // 검증 버튼 등 추가 기능 연결 (버튼이 있다면)
        if (verifyButton != null)
            verifyButton.onClick.AddListener(OnVerifyClicked);

        RefreshList(); // 시작하자마자 리스트 그리기
    }

    void InitializeDropdowns()
    {
        // Sort 드롭다운 초기화
        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string> { "ID", "Grid Size" });
        sortDropdown.value = 0;

        // Filter 드롭다운 초기화
        filterDropdown.ClearOptions();
        filterDropdown.AddOptions(new List<string> { "All", "Hard (6x6+)" });
        filterDropdown.value = 0;
    }

    void LoadAllStages()
    {
        var files = Resources.LoadAll<TextAsset>("Stages");
        foreach (var file in files)
        {
            try
            {
                StageData data = JsonUtility.FromJson<StageData>(file.text);
                if (data != null) allStages.Add(data);
            }
            catch { Debug.LogWarning($"파일 로드 실패: {file.name}"); }
        }
    }

    public void RefreshList()
    {
        // 1. 기존 목록 싹 지우기 (왼쪽 & 오른쪽)
        foreach (Transform child in listContent) Destroy(child.gameObject);
        foreach (Transform child in previewContent) Destroy(child.gameObject);

        IEnumerable<StageData> query = allStages;

        // 필터 (예: Hard 모드만 보기)
        if (filterDropdown.value == 1)
            query = query.Where(s => s.gridSize.x >= 6);

        if (sortDropdown.value == 0)
            query = query.OrderBy(s => s.stageId);
        else
            query = query.OrderByDescending(s => s.gridSize.x);

        foreach (var stage in query)
        {
            GameObject leftItem = Instantiate(listItemPrefab, listContent);
            leftItem.GetComponent<StageListItem>().Setup(stage, OnStageSelected);

            GameObject rightItem = Instantiate(previewPrefab, previewContent);

            float previewBoxSize = 230f;

            // 양옆 여백(Padding)을 20만큼 뺌
            float usableSpace = previewBoxSize - 20f;

            // 그리드 칸수(N)로 나누어 타일 1개 크기 도출
            float dynamicCellSize = usableSpace / stage.gridSize.x;

            // 계산된 크기로 그리드 그리기
            gridManager.GenerateGrid(stage, rightItem.transform, dynamicCellSize);

            // (선택) 미리보기 클릭 시 선택 기능
            Button btn = rightItem.GetComponent<Button>();
            if (btn == null) btn = rightItem.AddComponent<Button>();
            btn.onClick.AddListener(() => OnStageSelected(stage));
        }
    }

    // 리스트나 미리보기를 클릭했을 때 실행
    void OnStageSelected(StageData stage)
    {
        currentSelectedStage = stage;
        Debug.Log($"선택됨: Stage {stage.stageId}");

        // 오른쪽 패널 정보 갱신
        if (stageInfoText)
            stageInfoText.text = $"ID: {stage.stageId}\nSize: {stage.gridSize.x}x{stage.gridSize.y}\nMoves: {stage.maxMoves}\n" +
                                 $"Total Repeats: {stage.totalRepeats}\nSolution Repeats: [{string.Join(", ", stage.solutionColorRepeats ?? new int[0])}]";

        if (solutionText) solutionText.text = "게임 시작...";

        // ★ NEW: GameManager를 통해 게임 시작 (그리드, 플레이어, UI 모두 초기화)
        if (gameManager != null)
        {
            gameManager.StartStage(stage.stageId);
            Debug.Log($"Stage {stage.stageId} 게임 시작!");
        }
        else
        {
            Debug.LogWarning("GameManager를 찾을 수 없습니다!");
            // 폴백: 직접 그리드만 그리기
            if (gridManager && mainGridParent)
            {
                gridManager.GenerateGrid(stage, mainGridParent, 55f);
            }
        }
    }

    void OnVerifyClicked()
    {
        if (currentSelectedStage == null) return;

        // 솔루션 검증 로직 (필요시 구현)
        AStarPathfinder pathfinder = new AStarPathfinder();
        SolutionData solution = pathfinder.FindSolution(currentSelectedStage);

        Debug.Log(solution != null ? $"성공! {solution.totalMoves}이동" : "실패");
    }
}