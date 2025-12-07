using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class PreviewTooltip : MonoBehaviour
{
    private string stageInfo;
    private static GameObject tooltipObject;
    private static TextMeshProUGUI tooltipText;
    private static RectTransform tooltipRect;

    public void SetupTooltip(int stageId)
    {
        stageInfo = $"Stage {stageId:000}";

        // Image raycastTarget 활성화 (Tooltip 감지용)
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }

        // EventTrigger 추가
        EventTrigger trigger = GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        // Pointer Enter 이벤트
        EventTrigger.Entry entryEnter = new EventTrigger.Entry();
        entryEnter.eventID = EventTriggerType.PointerEnter;
        entryEnter.callback.AddListener((data) => OnPointerEnter());
        trigger.triggers.Add(entryEnter);

        // Pointer Exit 이벤트
        EventTrigger.Entry entryExit = new EventTrigger.Entry();
        entryExit.eventID = EventTriggerType.PointerExit;
        entryExit.callback.AddListener((data) => OnPointerExit());
        trigger.triggers.Add(entryExit);
    }

    public void OnPointerEnter()
    {
        ShowTooltip();
    }

    public void OnPointerExit()
    {
        HideTooltip();
    }

    private void ShowTooltip()
    {
        if (tooltipObject == null)
        {
            CreateTooltip();
        }
        tooltipText.text = stageInfo;
        tooltipObject.SetActive(true);
        UpdateTooltipPosition();
    }

    private void HideTooltip()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    private void CreateTooltip()
    {
        // 전역 Tooltip 오브젝트 생성 (한번만)
        if (tooltipObject != null) return;

        Canvas mainCanvas = GetComponentInParent<Canvas>();
        if (mainCanvas == null) return;

        // Tooltip 패널 생성
        tooltipObject = new GameObject("GlobalTooltip");
        tooltipObject.transform.SetParent(mainCanvas.transform, false);

        tooltipRect = tooltipObject.AddComponent<RectTransform>();
        tooltipRect.anchoredPosition = Vector2.zero;
        tooltipRect.sizeDelta = new Vector2(160, 50); // ★ 크기 조정: 120x35 → 160x50

        Image bgImage = tooltipObject.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.8f);
        bgImage.raycastTarget = false; // ★ Tooltip이 마우스 이벤트를 가로채지 않도록

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tooltipObject.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.text = "Stage 001";
        tooltipText.fontSize = 30; // ★ 폰트 사이즈도 키움: 18 → 22
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.color = Color.white;

        tooltipObject.SetActive(false);
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRect == null) return;

        // 마우스 위치 위에 Tooltip 표시
        Vector2 pos = Input.mousePosition;
        tooltipRect.position = new Vector3(pos.x, pos.y + 30, 0);
    }

    private void Update()
    {
        // Tooltip이 활성화되어 있으면 마우스를 따라다니기
        if (tooltipObject != null && tooltipObject.activeInHierarchy)
        {
            UpdateTooltipPosition();
        }
    }
}
