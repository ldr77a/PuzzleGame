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
    [SerializeField] private TMP_Dropdown levelDropdown;      // Dropdown_Level (난이도)
    [SerializeField] private TMP_Dropdown colorCountDropdown; // Dropdown_ColorCount (색상 개수)
    [SerializeField] private TMP_Dropdown totalColorDropdown; // Dropdown_TotalColor (총 색상 수집 횟수)

    [Header("Center Panel")]
    [SerializeField] private Transform mainGridParent;    // CenterPanel > Play 오브젝트 (타일이 생성될 곳)
    [SerializeField] private GridManager gridManager;     // GameManager 오브젝트
    [SerializeField] private GameManager gameManager;     // ★ NEW: 게임 시작용

    // 검증/리셋 버튼 등은 CenterPanel에 있다면 여기에 연결해서 쓰면 됩니다.
    [SerializeField] private Button verifyButton;         // (선택) Btn_Verify
    [SerializeField] private Button solutionButton;       // ★ NEW: Solution 버튼

    [Header("Right Panel")]
    [SerializeField] private Transform previewContent;    // RightPanel > PreviewScrollView > Viewport > Content
    [SerializeField] private GameObject previewPrefab;    // MapPreviewItem 프리팹 (Image 하나만 있는 것)

    [SerializeField] private TMP_Text stageInfoText;
    [SerializeField] private TMP_Text solutionText;
    [SerializeField] private TMP_Text stageIdText; // ★ NEW: StageID 표시용

    private List<StageData> allStages = new List<StageData>();
    private StageData currentSelectedStage;

    // ★ NEW: Solution 시각화 관련 필드
    private bool isSolutionVisible = false;
    private SolutionVisualizer solutionVisualizer;

    void InitializeDropdowns()
    {
        // 1. Sort Dropdown 설정 (기존 옵션 변경)
        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string> { "All", "ID", "Moves" });

        // 2. Level Dropdown 설정
        levelDropdown.ClearOptions();
        levelDropdown.AddOptions(new List<string> { "All", "Easy", "Medium", "Hard" });

        // 3. Color Count Dropdown 설정
        colorCountDropdown.ClearOptions();
        List<string> colorOptions = new List<string> { "All" };
        for (int i = 2; i <= 5; i++)
        {
            colorOptions.Add(i.ToString());
        }
        colorCountDropdown.AddOptions(colorOptions);

        // 4. Total Color Dropdown 설정
        totalColorDropdown.ClearOptions();
        List<string> totalColorOptions = new List<string> { "All" };
        for (int i = 2; i <= 10; i++)
        {
            totalColorOptions.Add(i.ToString());
        }
        totalColorDropdown.AddOptions(totalColorOptions);
    }

    void Start()
    {
        LoadAllStages();
        InitializeDropdowns(); // ★ NEW: 드롭다운 옵션 초기화

        // 드롭다운 변경 시 리스트 갱신 이벤트 연결
        sortDropdown.onValueChanged.AddListener(delegate { RefreshList(); });
        levelDropdown.onValueChanged.AddListener(delegate { RefreshList(); });       // ★ NEW
        colorCountDropdown.onValueChanged.AddListener(delegate { RefreshList(); });  // ★ NEW
        totalColorDropdown.onValueChanged.AddListener(delegate { RefreshList(); });  // ★ NEW

        // ★ NEW: SolutionVisualizer 초기화
        solutionVisualizer = gameObject.AddComponent<SolutionVisualizer>();

        // ★ NEW: Solution 버튼 연결
        if (solutionButton != null)
            solutionButton.onClick.AddListener(OnSolutionClicked);

        // 검증 버튼 등 추가 기능 연결 (버튼이 있다면)
        if (verifyButton != null)
            verifyButton.onClick.AddListener(OnVerifyClicked);

        RefreshList(); // 시작하자마자 리스트 그리기
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
        // 1. 기존 UI 정리
        foreach (Transform child in listContent) Destroy(child.gameObject);
        foreach (Transform child in previewContent) Destroy(child.gameObject);

        IEnumerable<StageData> query = allStages;

        // ========== 필터 적용 (AND 조건) ==========

        // 필터 1: Level (Difficulty)
        int levelIndex = levelDropdown.value;
        if (levelIndex > 0) // 0 = "All"
        {
            int targetDifficulty = levelIndex; // 1=Easy, 2=Medium, 3=Hard
            query = query.Where(s => s.difficulty == targetDifficulty);
        }

        // 필터 2: Color Count
        int colorCountIndex = colorCountDropdown.value;
        if (colorCountIndex > 0) // 0 = "All"
        {
            int targetColorCount = colorCountIndex + 1; // 인덱스 1 -> 색상 2개
            query = query.Where(s => s.colorCount == targetColorCount);
        }

        // 필터 3: Total Color (totalRepeats)
        int totalColorIndex = totalColorDropdown.value;
        if (totalColorIndex > 0) // 0 = "All"
        {
            int targetTotalRepeats = totalColorIndex + 1; // 인덱스 1 -> 2회
            query = query.Where(s => s.totalRepeats == targetTotalRepeats);
        }

        // ========== 정렬 적용 ==========
        if (sortDropdown.value == 1) // "ID"
        {
            query = query.OrderBy(s => s.stageId);
        }
        else if (sortDropdown.value == 2) // "Moves"
        {
            query = query.OrderBy(s => s.solutionMoves);
        }

        // ========== UI 생성 ==========
        List<StageData> stageList = query.ToList();

        if (stageList.Count == 0)
        {
            // 필터링 결과가 없으면 "No Data" 메시지 표시
            GameObject noDataItem = new GameObject("NoDataMessage");
            noDataItem.transform.SetParent(previewContent, false);

            RectTransform noDataRect = noDataItem.AddComponent<RectTransform>();
            noDataRect.sizeDelta = new Vector2(250, 50);

            TextMeshProUGUI noDataText = noDataItem.AddComponent<TextMeshProUGUI>();
            noDataText.text = "No Data";
            noDataText.fontSize = 36;
            noDataText.alignment = TextAlignmentOptions.Center;
            noDataText.color = new Color(0.7f, 0.7f, 0.7f); // 회색
        }
        else
        {
            foreach (var stage in stageList)
            {
                // 왼쪽 패널: 스테이지 리스트 아이템
                GameObject leftItem = Instantiate(listItemPrefab, listContent);
                leftItem.GetComponent<StageListItem>().Setup(stage, OnStageSelected);

                // 오른쪽 패널: 미리보기 그리드
                GameObject rightItem = Instantiate(previewPrefab, previewContent);
                float previewBoxSize = 230f;
                float usableSpace = previewBoxSize - 20f;
                float dynamicCellSize = usableSpace / stage.gridSize.x;

                gridManager.GenerateGrid(stage, rightItem.transform, dynamicCellSize);

                // ★ Image raycastTarget 비활성화 (스크롤 방해 방지)
                Image previewImage = rightItem.GetComponent<Image>();
                if (previewImage != null)
                {
                    previewImage.raycastTarget = false;
                }

                // 미리보기 클릭 시 선택 기능
                Button btn = rightItem.GetComponent<Button>();
                if (btn == null)
                {
                    btn = rightItem.AddComponent<Button>();
                    // Button이 클릭 감지를 위해 raycastTarget 필요
                    if (previewImage != null)
                    {
                        previewImage.raycastTarget = true;
                    }
                }
                btn.onClick.AddListener(() => OnStageSelected(stage));

                // ★ NEW: 툴팁 추가
                PreviewTooltip tooltip = rightItem.AddComponent<PreviewTooltip>();
                tooltip.SetupTooltip(stage.stageId);
            }
        }
    }

    // 리스트나 미리보기를 클릭했을 때 실행
    void OnStageSelected(StageData stage)
    {
        // ★ NEW: 새 스테이지 선택 시 기존 솔루션 제거
        if (isSolutionVisible)
        {
            OnSolutionClicked(); // 토글하여 숨김
        }

        currentSelectedStage = stage;
        Debug.Log($"선택됨: Stage {stage.stageId}");

        // ★ NEW: StageID 텍스트 업데이트
        if (stageIdText != null)
        {
            stageIdText.text = $"StageID: {stage.stageId}";
        }

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

    // ★ NEW: Solution 버튼 클릭 이벤트
    void OnSolutionClicked()
    {
        if (currentSelectedStage == null || gridManager == null)
        {
            Debug.LogWarning("스테이지를 먼저 선택해주세요!");
            return;
        }

        isSolutionVisible = !isSolutionVisible;

        if (isSolutionVisible)
        {
            // A* 알고리즘으로 솔루션 계산
            AStarPathfinder pathfinder = new AStarPathfinder();
            SolutionData solution = pathfinder.FindSolution(currentSelectedStage);

            if (solution != null)
            {
                // 시각화 표시
                Tile[,] tiles = gridManager.GetTiles();
                solutionVisualizer.ShowSolution(solution, currentSelectedStage, tiles, mainGridParent);

                // Solution 텍스트 업데이트
                if (solutionText != null)
                {
                    solutionText.text = $"Solution: {solution.totalMoves} moves";
                }

                Debug.Log($"Solution found: {solution.totalMoves} moves, Path length: {solution.path.Count}");
            }
            else
            {
                Debug.LogWarning("솔루션을 찾을 수 없습니다!");
                isSolutionVisible = false;
            }
        }
        else
        {
            // 시각화 제거
            solutionVisualizer.HideSolution();

            // 모든 타일의 숫자 제거
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

            // Solution 텍스트 초기화
            if (solutionText != null)
            {
                solutionText.text = "게임 시작...";
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