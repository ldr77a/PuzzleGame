using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private Image playerColorDisplay;   // PlayerColor
    [SerializeField] private Image targetColorDisplay;   // TargetColor
    [SerializeField] private TextMeshProUGUI movesText;  // MoveText
    // [SerializeField] private TextMeshProUGUI matchPercentageText;  // 일치도 표시 (미사용)

    [Header("결과 패널")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    private Color32 targetColor;

    public void Initialize(Color32 targetColorParam, int maxMoves)
    {
        targetColor = targetColorParam;
        targetColorDisplay.color = targetColor;
        UpdatePlayerColor(new Color32(255, 255, 255, 255)); // 흰색
        UpdateMovesCounter(maxMoves);

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
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

    public void ShowWinScreen()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
    }

    public void ShowLoseScreen()
    {
        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }
    }
}
