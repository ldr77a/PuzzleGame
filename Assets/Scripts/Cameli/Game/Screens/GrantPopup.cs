using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    enum GrantKind
    {
        Hint,
        Undo,
    }

    /// <summary>
    /// Offered when hints or undos run out (Phase 2 spec §6): "watch an ad to get more". Until the ads come, the
    /// button grants right away.
    /// </summary>
    sealed class GrantPopup
    {
        public const int HintReward = 1;
        public const int UndoReward = 3;

        readonly GameObject root;
        readonly Image icon;
        readonly Text title;
        readonly Text body;
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
            icon = Ui.NewImage("Icon", badge.transform, null, Theme.Primary);
            Ui.Inset(icon.rectTransform, 0.24f);

            title = Widgets.Label(card.transform, "Title", "", 58, Theme.Ink);
            Ui.TopStrip(title.rectTransform, 260f, 90f, 40f);
            body = Widgets.Label(card.transform, "Body", "", 40, Theme.Muted);
            Ui.TopStrip(body.rectTransform, 360f, 110f, 60f);

            Button close = Widgets.Button(card.transform, "Close", "닫기", 46, false, Hide, out _);
            close.GetComponent<Image>().color = Theme.Background;
            Ui.Box(close.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(50f, 50f), new Vector2(370f, 130f));
            Button accept = Widgets.Button(card.transform, "Accept", "받기", 46, true, Accept, out _);
            Ui.Box(accept.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-50f, 50f), new Vector2(370f, 130f));

            root.SetActive(false);
        }

        /// <param name="onGranted">Adds the reward; runs after the popup has closed.</param>
        public void Show(GrantKind kind, Action onGranted)
        {
            granted = onGranted;
            bool hint = kind == GrantKind.Hint;
            icon.sprite = hint ? SpriteFactory.BulbIcon : SpriteFactory.UndoIcon;
            title.text = hint ? "힌트 받기" : "되돌리기 받기";
            body.text = hint ? $"광고를 보면 힌트 {HintReward}개를 받아요" : $"광고를 보면 되돌리기 {UndoReward}회를 받아요";
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
