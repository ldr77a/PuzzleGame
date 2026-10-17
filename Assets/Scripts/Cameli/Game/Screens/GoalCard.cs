using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// The white card above the board (Phase 2 spec §2): the bud to bloom in the target color, the drop slots
    /// (eaten drops as colored dots in eating order, the rest dashed), "방울 x/y", a chip per recipe color the
    /// attempt's hints revealed (color only, no drop count), and Cameli in the current mix. The chips stay in the
    /// middle column: on one row, or on two when that lets them be bigger, shrunk to fit either way (up to five).
    /// </summary>
    sealed class GoalCard
    {
        public const float Height = 340f;

        const float SideColumn = 230f;
        const float FigureSize = 170f;
        const float SlotSize = 58f;
        const float SlotGap = 14f;
        const float SlotRowTop = 50f;
        const float ChipTop = SlotRowTop + SlotSize + 96f;
        const float ChipBottomMargin = 12f;
        const float ChipHeight = 64f;
        const float ChipGap = 14f;
        const float ChipRowGap = 10f;
        const float ChipDot = 32f;
        const float ChipPadLeft = 18f, ChipPadRight = 22f, ChipSpacing = 10f;

        /// <summary>How far the chips may reach into the side columns: less than the space beside the bud and Cameli.</summary>
        const float ChipOverhang = 20f;

        readonly RectTransform bud;
        readonly CameliFigure cameli;
        readonly RectTransform slotRow;
        readonly List<Slot> slots = new List<Slot>();
        readonly Text dropsLabel;
        readonly RectTransform chipArea;
        readonly RectTransform chipBlock;
        readonly List<RectTransform> chips = new List<RectTransform>();
        readonly List<float> chipWidths = new List<float>();
        readonly List<int> chipColors = new List<int>();

        public GoalCard(RectTransform parent, float top, float sideInset)
        {
            Image card = Widgets.Card(parent, "Goal Card");
            RectTransform root = Ui.TopStrip(card.rectTransform, top, Height, sideInset);

            RectTransform left = Column(root, "Target", 0f);
            RectTransform budBox = Ui.Box(Ui.NewRect("Bud Box", left), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(FigureSize, FigureSize));
            bud = Ui.Stretch(Blossoms.NewBud(budBox, Color.white));
            Caption(left, "피울 꽃");

            RectTransform right = Column(root, "Cameli", 1f);
            RectTransform cameliBox = Ui.Box(Ui.NewRect("Cameli Box", right), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(FigureSize, FigureSize));
            cameli = new CameliFigure(cameliBox);
            cameli.Face(Direction.Left); // toward the bud
            Caption(right, "카멜리");

            RectTransform middle = Ui.Stretch(Ui.NewRect("Drops", root), SideColumn, 0f, SideColumn, 0f);
            slotRow = Ui.TopStrip(Ui.NewRect("Slots", middle), SlotRowTop, SlotSize);
            dropsLabel = Widgets.Label(middle, "Drop Count", "", 42, Theme.Ink);
            Ui.TopStrip(dropsLabel.rectTransform, SlotRowTop + SlotSize + 18f, 60f);

            chipArea = Ui.TopStrip(Ui.NewRect("Recipe", middle), ChipTop, Height - ChipTop - ChipBottomMargin, -ChipOverhang);
            // The chips sit in one block hanging from the top center of the area; LayoutChips sizes and scales it.
            chipBlock = Ui.Box(Ui.NewRect("Chips", chipArea), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        }

        /// <summary>New stage: bud color, and no revealed colors yet.</summary>
        public void SetStage(StageDefinition stage)
        {
            Blossoms.SetBudColor(bud, Theme.ToColor(stage.TargetColor));
            SetChips(new List<int>());
        }

        public void Refresh(GameSession session)
        {
            BoardState state = session.State;
            cameli.SetColor(CameliFigure.ColorOf(state.Drops));
            int target = session.Stage.Target.Total;
            bool overfilled = Rules.IsOverfilled(session.Stage, state);
            dropsLabel.text = $"방울 {state.Drops.Total}/{target}";
            dropsLabel.color = overfilled ? Theme.Accent : Theme.Ink;

            IReadOnlyList<int> order = session.MixOrder;
            int count = Mathf.Max(target, order.Count);
            while (slots.Count < count) slots.Add(new Slot(slotRow));
            float width = slotRow.rect.width > 0f ? slotRow.rect.width : 500f;
            float size = Mathf.Min(SlotSize, (width - (count - 1) * SlotGap) / count);
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                slot.Root.gameObject.SetActive(i < count);
                if (i >= count) continue;
                float x = (i - (count - 1) / 2f) * (size + SlotGap);
                Ui.Box(slot.Root, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(size, size));
                if (i < order.Count) slot.ShowDrop(Theme.ToColor(Palette.Color(order[i])));
                else slot.ShowEmpty();
            }
            if (!SameColors(session.RevealedColors, chipColors)) SetChips(session.RevealedColors);
            LayoutChips();
        }

        /// <summary>Cameli's expression on the card; <see cref="PlayScreen"/> keeps it the same as the board token's.</summary>
        public void SetFace(CameliFace face) => cameli.SetFace(face);

        /// <summary>One chip per color, in this order; the chip area hides when there are none.</summary>
        void SetChips(IReadOnlyList<int> colors)
        {
            foreach (RectTransform chip in chips) Object.Destroy(chip.gameObject);
            chips.Clear();
            chipWidths.Clear();
            chipColors.Clear();
            foreach (int color in colors)
            {
                AddChip(color);
                chipColors.Add(color);
            }
            chipArea.gameObject.SetActive(chips.Count > 0);
        }

        static bool SameColors(IReadOnlyList<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Puts the chips on one row, or two (the first one longer) when that lets them be clearly bigger, centered, and
        /// scales the block down until it fits the area. Rerun when the area's width may have changed.
        /// </summary>
        void LayoutChips()
        {
            int count = chips.Count;
            if (count == 0) return;
            float areaWidth = chipArea.rect.width > 0f ? chipArea.rect.width : 540f;
            float areaHeight = Height - ChipTop - ChipBottomMargin;

            float oneRow = RowWidth(0, count);
            float oneScale = Mathf.Min(1f, areaWidth / oneRow, areaHeight / ChipHeight);
            int firstRow = count;
            float blockWidth = oneRow, blockHeight = ChipHeight, scale = oneScale;
            if (count > 1)
            {
                int split = (count + 1) / 2;
                float twoRows = Mathf.Max(RowWidth(0, split), RowWidth(split, count));
                float twoHeight = 2f * ChipHeight + ChipRowGap;
                float twoScale = Mathf.Min(1f, areaWidth / twoRows, areaHeight / twoHeight);
                if (twoScale > oneScale + 0.05f) // a single row reads better, so two rows must be clearly bigger
                {
                    firstRow = split;
                    blockWidth = twoRows;
                    blockHeight = twoHeight;
                    scale = twoScale;
                }
            }

            chipBlock.sizeDelta = new Vector2(blockWidth, blockHeight);
            chipBlock.localScale = new Vector3(scale, scale, 1f);
            PlaceRow(0, firstRow, blockWidth, 0f);
            if (firstRow < count) PlaceRow(firstRow, count, blockWidth, ChipHeight + ChipRowGap);
        }

        float RowWidth(int from, int to)
        {
            float width = (to - from - 1) * ChipGap;
            for (int i = from; i < to; i++) width += chipWidths[i];
            return width;
        }

        /// <summary>Lays chips [from, to) left to right, the row centered in the block, its top <paramref name="top"/> down.</summary>
        void PlaceRow(int from, int to, float blockWidth, float top)
        {
            float x = (blockWidth - RowWidth(from, to)) / 2f;
            for (int i = from; i < to; i++)
            {
                RectTransform chip = chips[i];
                chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(0f, 1f);
                chip.sizeDelta = new Vector2(chipWidths[i], ChipHeight);
                chip.anchoredPosition = new Vector2(x, -top);
                x += chipWidths[i] + ChipGap;
            }
        }

        void AddChip(int color)
        {
            Image chip = Widgets.Round(Ui.NewImage($"Chip {Palette.Letter(color)}", chipBlock, SpriteFactory.RoundedRect, Theme.Background), 32f);
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)ChipPadLeft, (int)ChipPadRight, 0, 0);
            layout.spacing = ChipSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image dot = Ui.NewImage("Dot", chip.transform, SpriteFactory.Circle, Theme.ToColor(Palette.Color(color)));
            var dotSize = dot.gameObject.AddComponent<LayoutElement>();
            dotSize.preferredWidth = dotSize.preferredHeight = ChipDot;
            Ui.Stretch(Ui.NewImage("Ring", dot.transform, SpriteFactory.Ring, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.25f)).rectTransform);

            Text label = Widgets.Label(chip.transform, "Label", Palette.Name(color), 34, Theme.Ink);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The chip's own width, fixed here so LayoutChips can arrange the rows without a layout pass.
            chips.Add(chip.rectTransform);
            chipWidths.Add(Mathf.Ceil(ChipPadLeft + ChipDot + ChipSpacing + label.preferredWidth + ChipPadRight));
        }

        static RectTransform Column(RectTransform card, string name, float side)
        {
            RectTransform column = Ui.NewRect(name, card);
            column.anchorMin = new Vector2(side, 0f);
            column.anchorMax = new Vector2(side, 1f);
            column.pivot = new Vector2(side, 0.5f);
            column.anchoredPosition = Vector2.zero;
            column.sizeDelta = new Vector2(SideColumn, 0f);
            return column;
        }

        static void Caption(RectTransform column, string text)
        {
            Text caption = Widgets.Label(column, "Caption", text, 34, Theme.Muted);
            Ui.BottomStrip(caption.rectTransform, 28f, 50f);
        }

        /// <summary>One drop slot: a colored dot with a faint outline, or a dashed empty circle.</summary>
        sealed class Slot
        {
            public readonly RectTransform Root;
            readonly Image fill;
            readonly Image ring;

            public Slot(Transform parent)
            {
                Root = Ui.NewRect("Slot", parent);
                fill = Ui.NewImage("Fill", Root, SpriteFactory.Circle, Color.white);
                Ui.Stretch(fill.rectTransform);
                ring = Ui.NewImage("Ring", Root, SpriteFactory.Ring, Color.white);
                Ui.Stretch(ring.rectTransform);
            }

            public void ShowDrop(Color color)
            {
                fill.enabled = true;
                fill.color = color;
                ring.sprite = SpriteFactory.Ring;
                ring.color = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.25f);
            }

            public void ShowEmpty()
            {
                fill.enabled = false;
                ring.sprite = SpriteFactory.DashedRing;
                ring.color = Theme.Muted;
            }
        }
    }
}
