using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Offered once the day's hints run out (Phase 2 spec §14): watch an ad and the hint is used right away, so
    /// nothing is stocked. Until the ads come, the button grants right away.
    /// </summary>
    sealed class GrantPopup
    {
        readonly GameObject root;
        Action granted;

        public bool IsVisible => root.activeSelf;

        public GrantPopup(RectTransform parent)
        {
            RectTransform layer = Widgets.Layer(parent, "Grant Popup");
            root = layer.gameObject;
            Widgets.Clickable(Widgets.Dim(layer, "Dim"), Hide);

            Image card = Widgets.Card(layer, "Card");
            card.raycastTarget = true; // taps on the card must not reach the dim and close it
            Ui.Box(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 720f));

            Image badge = Ui.NewImage("Badge", card.transform, SpriteFactory.Circle, Theme.Background);
            Ui.Box(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(170f, 170f));
            Image icon = Ui.NewImage("Icon", badge.transform, SpriteFactory.BulbIcon, Theme.Primary);
            Ui.Inset(icon.rectTransform, 0.24f);

            Text title = Widgets.Label(card.transform, "Title", "광고 보고 힌트", 58, Theme.Ink);
            Ui.TopStrip(title.rectTransform, 260f, 90f, 40f);
            Text body = Widgets.Label(card.transform, "Body",
                $"광고를 보면 바로 필요한 색 하나를 알려 줘요.\n힌트는 매일 {HintStore.DailyHints}개씩 채워져요.", 38, Theme.Muted);
            body.resizeTextForBestFit = true; // stays inside the card on narrow screens
            body.resizeTextMinSize = 26;
            body.resizeTextMaxSize = 38;
            Ui.TopStrip(body.rectTransform, 350f, 150f, 60f);

            Button close = Widgets.Button(card.transform, "Close", "닫기", 46, false, Hide, out _);
            close.GetComponent<Image>().color = Theme.Background;
            Ui.Box(close.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(50f, 50f), new Vector2(370f, 130f));
            Button accept = Widgets.Button(card.transform, "Accept", "광고 보기", 46, true, Accept, out _);
            Ui.Box(accept.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-50f, 50f), new Vector2(370f, 130f));

            root.SetActive(false);
        }

        /// <param name="onGranted">Uses the hint; runs after the popup has closed.</param>
        public void Show(Action onGranted)
        {
            granted = onGranted;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            granted = null;
            root.SetActive(false);
        }

        void Accept()
        {
            // 4단계: 보상형 광고 — show a rewarded ad here and grant only when it reports a completed view.
            Action reward = granted;
            Hide();
            reward?.Invoke();
        }
    }
}
