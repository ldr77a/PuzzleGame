using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>Overlay shown after a clear: stars, moves versus optimal, replay and next.</summary>
    sealed class ClearPanel
    {
        readonly GameObject root;
        readonly Text title;
        readonly Text detail;
        readonly Text nextLabel;
        readonly Image[] stars = new Image[3];

        public bool IsVisible => root.activeSelf;

        public ClearPanel(RectTransform parent, Action replay, Action next)
        {
            Image dim = Ui.NewImage("Clear Panel", parent, null, Theme.Dim);
            dim.raycastTarget = true; // swallows taps meant for the board underneath
            Ui.Stretch(dim.rectTransform);
            root = dim.gameObject;

            Image card = Ui.NewImage("Card", dim.transform, SpriteFactory.RoundedRect, Theme.Panel);
            Ui.Box(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 820));

            title = Ui.NewLabel("Title", card.transform, "", 76, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.TopStrip(title.rectTransform, 60f, 120f, 40f);

            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = Ui.NewImage($"Star {i + 1}", card.transform, SpriteFactory.Star, Theme.StarOff);
                Ui.Box(stars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 200f, -220f), new Vector2(170, 170));
            }

            detail = Ui.NewLabel("Detail", card.transform, "", 42, Theme.Muted, TextAnchor.MiddleCenter);
            Ui.TopStrip(detail.rectTransform, 430f, 90f, 40f);

            Button replayButton = Ui.NewButton("Replay", card.transform, "다시 하기", 44, Theme.Background, Theme.Ink, replay, out _);
            Ui.Box(replayButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(60, 60), new Vector2(350, 130));
            Button nextButton = Ui.NewButton("Next", card.transform, "", 44, Theme.Accent, Color.white, next, out nextLabel);
            Ui.Box(nextButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-60, 60), new Vector2(350, 130));

            root.SetActive(false);
        }

        public void ShowStageClear(int earned, int moves, int optimalMoves, bool hasNext)
        {
            title.text = "클리어!";
            SetStars(earned);
            detail.text = $"걸음 {moves} · 최단 {optimalMoves}";
            nextLabel.text = hasNext ? "다음 스테이지" : "결과 보기";
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void ShowAllClear(int earned, int totalStars, int maxStars)
        {
            title.text = "모든 스테이지 클리어!";
            SetStars(earned);
            detail.text = $"모은 별 {totalStars} / {maxStars}";
            nextLabel.text = "처음 스테이지로";
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);

        void SetStars(int earned)
        {
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < earned ? Theme.StarOn : Theme.StarOff;
        }
    }
}
