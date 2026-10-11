using System;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Top bar (stage, restart), target/current color, drop count, moves, hint line, message line, and the
    /// undo button. Built in code inside the safe area.
    /// </summary>
    sealed class HudView
    {
        /// <summary>Canvas units the HUD takes at the top and bottom; the board fills the space between.</summary>
        public const float TopReserved = 540f;
        public const float BottomReserved = 300f;

        const float Side = 36f;

        readonly Text stageLabel;
        readonly Text hintLabel;
        readonly Text dropsLabel;
        readonly Text movesLabel;
        readonly Text messageLabel;
        readonly Text undoLabel;
        readonly Image targetSwatch;
        readonly Image currentSwatch;
        readonly Button previousButton;
        readonly Button nextButton;
        readonly Button undoButton;

        public HudView(RectTransform root, Action restart, Action undo, Action previous, Action next)
        {
            // Top bar: [<] stage title [>] ........ [처음부터]
            RectTransform bar = Ui.TopStrip(Ui.NewRect("Top Bar", root), 24f, 110f, Side);
            previousButton = Ui.NewButton("Previous", bar, "<", 52, Theme.Button, Theme.Ink, previous, out _);
            Ui.Box(previousButton.GetComponent<RectTransform>(), new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 100));
            stageLabel = Ui.NewLabel("Stage", bar, "", 44, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Stretch(stageLabel.rectTransform, 124f, 0, 376f, 0);
            nextButton = Ui.NewButton("Next", bar, ">", 52, Theme.Button, Theme.Ink, next, out _);
            Ui.Box(nextButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-256, 0), new Vector2(100, 100));
            Button restartButton = Ui.NewButton("Restart", bar, "처음부터", 40, Theme.Button, Theme.Ink, restart, out _);
            Ui.Box(restartButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), Vector2.zero, new Vector2(240, 100));

            // Info row: 목표 | 지금 | 방울 | 걸음
            RectTransform info = Ui.TopStrip(Ui.NewRect("Info", root), 170f, 230f, Side);
            targetSwatch = Swatch(info, 0, "목표");
            currentSwatch = Swatch(info, 1, "지금");
            dropsLabel = Counter(info, 2, "방울");
            movesLabel = Counter(info, 3, "걸음");

            hintLabel = Ui.NewLabel("Hint", root, "", 38, Theme.Muted, TextAnchor.MiddleCenter);
            Ui.TopStrip(hintLabel.rectTransform, 420f, 110f, Side);

            messageLabel = Ui.NewLabel("Message", root, "", 34, Theme.Accent, TextAnchor.MiddleCenter);
            Ui.BottomStrip(messageLabel.rectTransform, 180f, 100f, Side);

            undoButton = Ui.NewButton("Undo", root, "", 44, Theme.Button, Theme.Ink, undo, out undoLabel);
            Ui.Box(undoButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(440, 120));
        }

        public void SetStage(StageDefinition stage, int index, int count, bool canGoBack, bool canGoForward)
        {
            stageLabel.text = $"{index + 1}/{count}  {stage.Title}";
            hintLabel.text = stage.Hint;
            targetSwatch.color = Theme.ToColor(stage.TargetColor);
            previousButton.interactable = canGoBack;
            nextButton.interactable = canGoForward;
        }

        public void Refresh(GameSession session)
        {
            BoardState state = session.State;
            currentSwatch.color = state.Drops.IsEmpty ? Color.white : (Color)Theme.ToColor(ColorMath.Mix(state.Drops));
            dropsLabel.text = $"{state.Drops.Total}/{session.Stage.Target.Total}";
            dropsLabel.color = Rules.IsOverfilled(session.Stage, state) ? Theme.Accent : Theme.Ink;
            movesLabel.text = state.Moves.ToString();
            undoLabel.text = $"되돌리기 {session.UndosRemaining}";
            undoButton.interactable = session.CanUndo;
        }

        public void ShowMessage(string text) => messageLabel.text = text;

        static Image Swatch(RectTransform row, int column, string caption)
        {
            RectTransform cell = Column(row, column);
            Image outline = Ui.NewImage("Outline", cell, SpriteFactory.Circle, Theme.Ink);
            Ui.Box(outline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(140, 140));
            Image swatch = Ui.NewImage("Swatch", outline.transform, SpriteFactory.Circle, Color.white);
            Ui.Inset(swatch.rectTransform, 0.06f);
            Caption(cell, caption);
            return swatch;
        }

        static Text Counter(RectTransform row, int column, string caption)
        {
            RectTransform cell = Column(row, column);
            Text value = Ui.NewLabel("Value", cell, "0", 72, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Box(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(240, 140));
            Caption(cell, caption);
            return value;
        }

        static RectTransform Column(RectTransform row, int column)
        {
            RectTransform cell = Ui.NewRect($"Column {column}", row);
            cell.anchorMin = new Vector2(column / 4f, 0);
            cell.anchorMax = new Vector2((column + 1) / 4f, 1);
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = Vector2.zero;
            return cell;
        }

        static void Caption(RectTransform cell, string text)
        {
            Text caption = Ui.NewLabel("Caption", cell, text, 34, Theme.Muted, TextAnchor.MiddleCenter);
            Ui.BottomStrip(caption.rectTransform, 0, 60);
        }
    }
}
