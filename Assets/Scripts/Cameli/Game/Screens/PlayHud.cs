using System;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Everything on the play screen around the board (Phase 2 spec §2): top bar, goal card, Cameli's speech
    /// bubble, the info row, the message line, and the undo and hint buttons. <see cref="BoardTop"/> and
    /// <see cref="BoardBottom"/> say how much the board area leaves free.
    /// </summary>
    sealed class PlayHud
    {
        public const float BoardBottom = 400f;

        const float Side = 36f;
        const float TopBarTop = 24f;
        const float TopBarHeight = 120f;
        const float CardTop = 168f;
        const float BubbleTop = CardTop + GoalCard.Height + 22f;
        const float BubbleHeight = 140f;
        const float MessageSeconds = 3.2f;
        const float MessageFadeSeconds = 0.4f;

        readonly Text title;
        readonly GoalCard card;
        readonly RectTransform bubble;
        readonly Text bubbleText;
        readonly Text movesLabel;
        readonly Text optimalLabel;
        readonly Text messageLabel;
        readonly Text undoLabel;
        readonly Text hintLabel;
        readonly Button undoButton;
        float messageLeft;

        /// <summary>Canvas units from the top of the safe area to the board area, which depends on the bubble.</summary>
        public float BoardTop => bubble.gameObject.activeSelf ? BubbleTop + BubbleHeight + 24f : BubbleTop;

        public GoalCard Card => card;

        public PlayHud(RectTransform root, Action openMenu, Action restart, Action openPlaytest, Action undo, Action hint)
        {
            // Top bar: [menu] title [restart]
            RectTransform bar = Ui.TopStrip(Ui.NewRect("Top Bar", root), TopBarTop, TopBarHeight, Side);
            Button menuButton = Widgets.IconButton(bar, "Menu", SpriteFactory.MenuIcon, openMenu);
            Ui.Box(menuButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(TopBarHeight, TopBarHeight));
            Button restartButton = Widgets.IconButton(bar, "Restart", SpriteFactory.RestartIcon, restart);
            Ui.Box(restartButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(TopBarHeight, TopBarHeight));
            title = Widgets.Label(bar, "Title", "", 50, Theme.Ink);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 30;
            title.resizeTextMaxSize = 50;
            Ui.Stretch(title.rectTransform, TopBarHeight + 20f, 0f, TopBarHeight + 20f, 0f);
            title.raycastTarget = true; // hidden playtest tools: hold the title for two seconds
            title.gameObject.AddComponent<LongPress>().Held += openPlaytest;

            card = new GoalCard(root, CardTop, Side);

            bubble = BuildBubble(root, out bubbleText);

            // Info row: 걸음 N ........ (★★★ M걸음)
            RectTransform info = Ui.BottomStrip(Ui.NewRect("Info", root), 290f, 90f, Side + 12f);
            movesLabel = Widgets.Label(info, "Moves", "", 46, Theme.Ink, TextAnchor.MiddleLeft);
            Ui.Stretch(movesLabel.rectTransform);
            Image pill = Widgets.Card(info, "Optimal");
            Widgets.Round(pill, 40f);
            Ui.Box(pill.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(330f, 80f));
            RectTransform starBox = Ui.Box(Ui.NewRect("Stars", pill.transform), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(140f, 44f));
            Widgets.SetStars(Widgets.Stars(starBox, 3, 42f, 6f), 3);
            optimalLabel = Widgets.Label(pill.transform, "Label", "", 38, Theme.ButtonInk, TextAnchor.MiddleRight);
            Ui.Stretch(optimalLabel.rectTransform, 170f, 0f, 30f, 0f);

            messageLabel = Widgets.Label(root, "Message", "", 36, Theme.Accent);
            messageLabel.verticalOverflow = VerticalWrapMode.Truncate;
            messageLabel.resizeTextForBestFit = true;
            messageLabel.resizeTextMinSize = 24;
            messageLabel.resizeTextMaxSize = 36;
            Ui.BottomStrip(messageLabel.rectTransform, 192f, 92f, Side);

            // Bottom bar: [undo n] [hint n]
            RectTransform bottom = Ui.BottomStrip(Ui.NewRect("Bottom Bar", root), 40f, 140f, Side);
            undoButton = Widgets.Button(bottom, "Undo", "", 44, false, undo, out undoLabel, SpriteFactory.UndoIcon, sound: null);
            Half(undoButton, 0f);
            Button hintButton = Widgets.Button(bottom, "Hint", "", 44, false, hint, out hintLabel, SpriteFactory.BulbIcon);
            Half(hintButton, 1f);
        }

        public void SetStage(StageDefinition stage, bool recipeRevealed)
        {
            // Jua has no "·", so the number and the name are joined with a period.
            title.text = $"{stage.Id}. {stage.Title}";
            optimalLabel.text = $"{stage.OptimalMoves}걸음";
            bubbleText.text = stage.Hint;
            bubble.gameObject.SetActive(!string.IsNullOrEmpty(stage.Hint));
            card.SetStage(stage, recipeRevealed);
            ShowMessage("");
        }

        public void Refresh(GameSession session)
        {
            card.Refresh(session);
            movesLabel.text = $"걸음 {session.State.Moves}";
            int undos = session.UndosRemaining;
            undoLabel.text = undos == 0 ? "받기" : $"되돌리기 {undos}";
            // At zero the button offers more undos, so it stays pressable; otherwise it needs a move to take back.
            undoButton.interactable = !session.IsCleared && (undos == 0 || session.HistoryCount > 0);
            int hints = HintStore.Count;
            hintLabel.text = hints == 0 ? "받기" : $"힌트 {hints}";
        }

        /// <summary>Shows a line between the board and the buttons for a few seconds; "" clears it.</summary>
        public void ShowMessage(string text)
        {
            messageLabel.text = text;
            messageLeft = string.IsNullOrEmpty(text) ? 0f : MessageSeconds;
            SetMessageAlpha(1f);
        }

        /// <summary>Fades the message out when its time is up; call every frame.</summary>
        public void Tick(float deltaTime)
        {
            if (messageLeft <= 0f) return;
            messageLeft -= deltaTime;
            if (messageLeft <= 0f) messageLabel.text = "";
            else if (messageLeft < MessageFadeSeconds) SetMessageAlpha(messageLeft / MessageFadeSeconds);
        }

        void SetMessageAlpha(float alpha)
        {
            Color color = messageLabel.color;
            color.a = alpha;
            messageLabel.color = color;
        }

        /// <summary>Cameli's face in a white circle, then a white bubble with the stage's hint text.</summary>
        static RectTransform BuildBubble(RectTransform root, out Text text)
        {
            RectTransform row = Ui.TopStrip(Ui.NewRect("Speech", root), BubbleTop, BubbleHeight, Side);

            Image face = Ui.NewImage("Face", row, SpriteFactory.Circle, Theme.Card);
            Widgets.AddShadow(face, 6f);
            Ui.Box(face.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BubbleHeight - 10f, BubbleHeight - 10f));
            RectTransform faceBox = Ui.Inset(Ui.NewRect("Cameli Box", face.transform), 0.1f);
            new CameliFigure(faceBox); // plain white, like an empty mix

            Image tail = Ui.NewImage("Tail", row, SpriteFactory.PlayIcon, Theme.Card);
            Ui.Box(tail.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tail.rectTransform.anchoredPosition = new Vector2(BubbleHeight + 10f, 0f);
            tail.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f); // points back at Cameli

            Image bubble = Widgets.Card(row, "Bubble");
            Widgets.Round(bubble, 36f);
            Ui.Stretch(bubble.rectTransform, BubbleHeight + 18f, 0f, 0f, 0f);
            text = Widgets.Label(bubble.transform, "Text", "", 36, Theme.Ink, TextAnchor.MiddleLeft);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = 36;
            Ui.Stretch(text.rectTransform, 32f, 12f, 28f, 12f);
            return row;
        }

        /// <summary>Left (0) or right (1) half of the bottom bar, with a gap in the middle.</summary>
        static void Half(Button button, float side)
        {
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(side * 0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f + side * 0.5f, 1f);
            rt.offsetMin = new Vector2(side == 0f ? 0f : 15f, 0f);
            rt.offsetMax = new Vector2(side == 0f ? -15f : 0f, 0f);
        }
    }
}
