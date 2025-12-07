using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class StageListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button btn;

    private StageData data;
    private Action<StageData> onClickCallback;

    public void Setup(StageData stageData, Action<StageData> callback)
    {
        data = stageData;
        onClickCallback = callback;

        // 버튼 텍스트 예: "Stage 1 [Easy]"
        string difficulty = data.difficulty switch
        {
            1 => "Easy",
            2 => "Medium",
            3 => "Hard",
            _ => "Unknown"
        };
        infoText.text = $"Stage {data.stageId} [{difficulty}]";

        // 클릭 이벤트 연결
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClickCallback?.Invoke(data));
    }
}