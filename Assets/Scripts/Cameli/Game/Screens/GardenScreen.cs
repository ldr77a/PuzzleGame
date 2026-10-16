using System;
using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Stage select and garden in one (Phase 2 spec §4): a scrolling 4-column bed of tiles grouped by section.
    /// Cleared stages show a camellia in their target color and their best stars, the stage to play next a
    /// swaying bud and "지금", locked stages a grey bud and a lock (their color stays hidden). "이어하기" at the
    /// bottom enters the next stage, or the last one played once everything is cleared.
    /// </summary>
    public sealed class GardenScreen : MonoBehaviour
    {
        const int Columns = 4;
        const float HeaderTop = 24f;
        const float HeaderHeight = 120f;
        const float ScrollTop = 168f;
        const float ScrollBottom = 230f;
        const float SidePadding = 36f;
        const float TileGap = 24f;
        const float TileHeight = 300f;
        const float RowGap = 24f;
        const float SectionHeight = 96f;
        const float FlowerSize = 150f;

        /// <summary>
        /// Taps are ignored this long after the garden opens, so the second tap of a double tap on the title (or a
        /// button that leads here) can't open a stage.
        /// </summary>
        const float TapGuardSeconds = 0.25f;

        readonly List<Tile> tiles = new List<Tile>();
        List<StageDefinition> stages;
        Action<int> play;
        RectTransform viewport;
        RectTransform content;
        ScrollRect scroll;
        CanvasGroup inputGate;
        float tapsFrom = -1f;
        Text starsLabel;
        Button continueButton;
        Text continueLabel;
        int continueIndex = -1;
        int scrollTarget = -1;
        int scrollFrame;
        float scrolledHeight;

        internal static GardenScreen Create(RectTransform parent, Action openMenu, Action<int> play)
        {
            RectTransform layer = Widgets.Layer(parent, "Garden Screen");
            var screen = layer.gameObject.AddComponent<GardenScreen>();
            screen.Build(layer, openMenu, play);
            return screen;
        }

        /// <summary>The first open stage not cleared yet, or -1 when every stage is cleared.</summary>
        internal static int NextToPlay(List<StageDefinition> stages)
        {
            int lastOpen = LastOpenIndex(stages);
            for (int i = 0; i <= lastOpen; i++)
                if (Progress.BestStars(stages[i].Id) == 0) return i;
            return -1;
        }

        /// <summary>Where "이어하기" goes: the next stage, or the last one played when all are cleared.</summary>
        internal static int ContinueIndex(List<StageDefinition> stages)
        {
            if (stages.Count == 0) return -1;
            int next = NextToPlay(stages);
            return next >= 0 ? next : Mathf.Clamp(Progress.LastPlayed, 0, LastOpenIndex(stages));
        }

        internal static int LastOpenIndex(List<StageDefinition> stages) => Mathf.Min(Progress.Unlocked, stages.Count - 1);

        void Build(RectTransform root, Action openMenu, Action<int> onPlay)
        {
            play = onPlay;
            inputGate = root.gameObject.AddComponent<CanvasGroup>();

            // Camellia leaves peeking in from the edges behind the beds (Phase 2 spec §15); first, so they draw under everything.
            Widgets.Leaf(root, new Vector2(0f, 0.80f), new Vector2(-20f, 0f), 240f, -40f, Theme.LeafDark);
            Widgets.Leaf(root, new Vector2(1f, 0.58f), new Vector2(20f, 0f), 220f, 35f, Theme.LeafLight);
            Widgets.Leaf(root, new Vector2(0f, 0.32f), new Vector2(-20f, 0f), 200f, -65f, Theme.LeafLight);
            Widgets.Leaf(root, new Vector2(1f, 0.14f), new Vector2(10f, 0f), 240f, 50f, Theme.LeafDark);

            RectTransform header = Ui.TopStrip(Ui.NewRect("Header", root), HeaderTop, HeaderHeight, 36f);
            Button menuButton = Widgets.IconButton(header, "Menu", SpriteFactory.MenuIcon, openMenu);
            Ui.Box(menuButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(HeaderHeight, HeaderHeight));
            Text title = Widgets.Label(header, "Title", "카멜리의 정원", 56, Theme.Ink, TextAnchor.MiddleLeft);
            Ui.Stretch(title.rectTransform, HeaderHeight + 30f, 0f, 260f, 0f);
            Image star = Ui.NewImage("Star", header, SpriteFactory.Star, Theme.StarOn);
            Ui.Box(star.rectTransform, new Vector2(1f, 0.5f), new Vector2(-196f, 0f), new Vector2(58f, 58f));
            starsLabel = Widgets.Label(header, "Stars", "", 46, Theme.Ink, TextAnchor.MiddleLeft);
            Ui.Box(starsLabel.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(186f, 80f));

            viewport = Ui.Stretch(Ui.NewRect("Viewport", root), 0f, ScrollBottom, 0f, ScrollTop);
            Image dragArea = viewport.gameObject.AddComponent<Image>();
            dragArea.color = Color.clear; // drags between tiles still scroll
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Ui.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            continueButton = Widgets.Button(root, "Continue", "", 46, true, () =>
            {
                if (continueIndex >= 0) play(continueIndex);
            }, out continueLabel, SpriteFactory.PlayIcon);
            Ui.BottomStrip(continueButton.GetComponent<RectTransform>(), 44f, 150f, 48f);
        }

        /// <summary>Lays out one tile per stage, under section headings. Call once the stages are loaded.</summary>
        internal void SetStages(List<StageDefinition> list)
        {
            stages = list;
            tiles.Clear();
            foreach (Transform child in content) Destroy(child.gameObject);

            float y = 12f;
            int section = -1;
            int column = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                int s = stages[i].Section;
                if (s != section)
                {
                    if (column > 0) y += TileHeight + RowGap;
                    column = 0;
                    section = s;
                    Text heading = Widgets.Label(content, "Section", StageSections.All[s].Name, 44, Theme.Muted, TextAnchor.LowerLeft);
                    Ui.TopStrip(heading.rectTransform, y, SectionHeight - 24f, SidePadding + 8f);
                    y += SectionHeight;
                }
                int index = i;
                tiles.Add(new Tile(content, stages[i], column, y, () => play(index)));
                if (++column == Columns)
                {
                    column = 0;
                    y += TileHeight + RowGap;
                }
            }
            if (column > 0) y += TileHeight + RowGap;
            content.sizeDelta = new Vector2(0f, y + 12f);
        }

        /// <summary>Refreshes every tile from the saved progress, then shows the screen scrolled to the next stage.</summary>
        internal void Show()
        {
            gameObject.SetActive(true);
            inputGate.blocksRaycasts = false;
            tapsFrom = Time.unscaledTime + TapGuardSeconds;
            if (stages == null) return;

            int lastOpen = LastOpenIndex(stages);
            int next = NextToPlay(stages);
            int total = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                int best = Progress.BestStars(stages[i].Id);
                total += best;
                TileState state = i > lastOpen ? TileState.Locked
                    : best > 0 ? TileState.Cleared
                    : i == next ? TileState.Next
                    : TileState.Open;
                tiles[i].Refresh(state, best);
            }
            starsLabel.text = $"{total}/{stages.Count * 3}";

            continueIndex = ContinueIndex(stages);
            continueButton.interactable = continueIndex >= 0;
            // Jua has no "·", so the stage follows two spaces, numbered with a period.
            continueLabel.text = continueIndex >= 0
                ? $"이어하기  {stages[continueIndex].Id}. {stages[continueIndex].Title}"
                : "이어하기";

            // Scroll right away so the garden never shows at the top first. Should the canvas still change size
            // this frame (the very first frame, a resize), Update scrolls once more next frame with the final height.
            scrollTarget = continueIndex;
            scrollFrame = Time.frameCount;
            ScrollTo(scrollTarget);
        }

        internal void Hide() => gameObject.SetActive(false);

        void Update()
        {
            if (tapsFrom >= 0f && Time.unscaledTime >= tapsFrom)
            {
                tapsFrom = -1f;
                inputGate.blocksRaycasts = true;
            }

            if (scrollTarget < 0 || Time.frameCount == scrollFrame) return;
            if (!Mathf.Approximately(viewport.rect.height, scrolledHeight)) ScrollTo(scrollTarget);
            scrollTarget = -1;
        }

        /// <summary>Centers tile <paramref name="index"/> in the viewport as far as the content allows.</summary>
        void ScrollTo(int index)
        {
            if (index < 0 || index >= tiles.Count) return;
            scroll.StopMovement();
            float view = viewport.rect.height;
            float max = Mathf.Max(0f, content.sizeDelta.y - view);
            float y = tiles[index].Top - (view - TileHeight) / 2f;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
            scrolledHeight = view;
        }

        enum TileState
        {
            Cleared,
            Next,
            Open,
            Locked,
        }

        /// <summary>One stage in the garden bed.</summary>
        sealed class Tile
        {
            public readonly float Top;
            readonly StageDefinition stage;
            readonly Button button;
            readonly Image frame;
            readonly RectTransform flower;
            readonly RectTransform bud;
            readonly Sway sway;
            readonly Text number;
            readonly RectTransform starRow;
            readonly Image[] stars;
            readonly Text now;
            readonly Image lockIcon;

            public Tile(RectTransform parent, StageDefinition stage, int column, float top, Action onTap)
            {
                this.stage = stage;
                Top = top;

                Image card = Widgets.Card(parent, $"Stage {stage.Id}");
                card.raycastTarget = true;
                RectTransform rt = card.rectTransform;
                rt.anchorMin = new Vector2(column / (float)Columns, 1f);
                rt.anchorMax = new Vector2((column + 1) / (float)Columns, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                // Each column owns a quarter of the width; these offsets turn the quarters into equal tiles with
                // SidePadding at the edges and TileGap between them.
                float left = SidePadding + column * (TileGap - 2f * SidePadding) / Columns;
                rt.offsetMin = new Vector2(left, -top - TileHeight);
                rt.offsetMax = new Vector2(left - (2f * SidePadding + (Columns - 1) * TileGap) / Columns, -top);
                button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                ColorBlock colors = button.colors;
                colors.disabledColor = Color.white; // locked tiles look locked through their content, not a grey wash
                button.colors = colors;
                button.onClick.AddListener(() =>
                {
                    Sfx.Play(SfxKind.Button);
                    onTap();
                });

                frame = Widgets.Round(Ui.NewImage("Next Frame", card.transform, SpriteFactory.RoundedFrame, Theme.Accent), Widgets.CardRadius);
                Ui.Stretch(frame.rectTransform);

                RectTransform art = Ui.Box(Ui.NewRect("Art", card.transform), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(FlowerSize, FlowerSize));
                Color color = Theme.ToColor(stage.TargetColor);
                flower = Ui.Stretch(Blossoms.NewFlower(art, color));
                bud = Ui.Stretch(Blossoms.NewBud(art, color));
                bud.pivot = new Vector2(0.5f, 0.05f); // sway from the stem
                sway = bud.gameObject.AddComponent<Sway>();
                sway.Offset = stage.Id * 0.37f;

                number = Widgets.Label(card.transform, "Number", stage.Id.ToString(), 44, Theme.Ink);
                Ui.TopStrip(number.rectTransform, 172f, 56f);

                starRow = Ui.BottomStrip(Ui.NewRect("Stars", card.transform), 22f, 44f);
                stars = Widgets.Stars(starRow, 3, 40f, 4f);
                now = Widgets.Label(card.transform, "Now", "지금", 36, Theme.Accent);
                Ui.BottomStrip(now.rectTransform, 18f, 52f);
                lockIcon = Ui.NewImage("Lock", card.transform, SpriteFactory.LockIcon, Theme.Locked);
                Ui.Box(lockIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(46f, 46f));
            }

            public void Refresh(TileState state, int bestStars)
            {
                bool cleared = state == TileState.Cleared;
                bool locked = state == TileState.Locked;
                button.interactable = !locked;
                frame.gameObject.SetActive(state == TileState.Next);
                flower.gameObject.SetActive(cleared);
                bud.gameObject.SetActive(!cleared);
                Blossoms.SetBudColor(bud, locked ? Theme.Locked : (Color)Theme.ToColor(stage.TargetColor));
                sway.enabled = state == TileState.Next;
                number.color = locked ? Theme.Muted : Theme.Ink;
                starRow.gameObject.SetActive(cleared);
                if (cleared) Widgets.SetStars(stars, bestStars);
                now.gameObject.SetActive(state == TileState.Next);
                lockIcon.gameObject.SetActive(locked);
            }
        }
    }
}
