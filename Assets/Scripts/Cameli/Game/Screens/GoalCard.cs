using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// The white card above the board (Phase 2 spec §2): the bud to bloom in the target color, the drop slots
    /// (eaten drops as colored dots in eating order, the rest dashed), "방울 x/y", the recipe chips once a hint
    /// revealed them, and Cameli in the current mix.
    /// </summary>
    sealed class GoalCard
    {
        public const float Height = 340f;

        const float SideColumn = 230f;
        const float FigureSize = 170f;
        const float SlotSize = 58f;
        const float SlotGap = 14f;
        const float SlotRowTop = 50f;

        readonly RectTransform bud;
        readonly CameliFigure cameli;
        readonly RectTransform slotRow;
        readonly List<Slot> slots = new List<Slot>();
        readonly Text dropsLabel;
        readonly RectTransform chipRow;
        readonly List<int> order = new List<int>();

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

            chipRow = Ui.TopStrip(Ui.NewRect("Recipe", middle), SlotRowTop + SlotSize + 100f, 64f, -40f);
            var layout = chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }

        /// <summary>New stage: bud color and recipe chips (shown only when <paramref name="recipeRevealed"/>).</summary>
        public void SetStage(StageDefinition stage, bool recipeRevealed)
        {
            Blossoms.SetBudColor(bud, Theme.ToColor(stage.TargetColor));
            foreach (Transform chip in chipRow) Object.Destroy(chip.gameObject);
            for (int color = 0; color < Palette.Count; color++)
                if (stage.Target[color] > 0) AddChip(color, stage.Target[color]);
            ShowRecipe(recipeRevealed);
        }

        public void ShowRecipe(bool visible) => chipRow.gameObject.SetActive(visible);

        public void Refresh(GameSession session)
        {
            BoardState state = session.State;
            cameli.SetColor(CameliFigure.ColorOf(state.Drops));
            int target = session.Stage.Target.Total;
            bool overfilled = Rules.IsOverfilled(session.Stage, state);
            dropsLabel.text = $"방울 {state.Drops.Total}/{target}";
            dropsLabel.color = overfilled ? Theme.Accent : Theme.Ink;

            EatenOrder(session, order);
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
        }

        /// <summary>
        /// Colors in the current mix in the order they were eaten, rebuilt from the attempt's history (the core
        /// state only counts drops). A passed gate empties the mix and starts the order over.
        /// </summary>
        static void EatenOrder(GameSession session, List<int> result)
        {
            result.Clear();
            var states = new List<BoardState>(session.History);
            states.Reverse();
            states.Add(session.State);
            for (int i = 1; i < states.Count; i++)
            {
                DropCounts before = states[i - 1].Drops, after = states[i].Drops;
                if (after.IsEmpty)
                {
                    result.Clear();
                    continue;
                }
                if (after.Total != before.Total + 1) continue;
                for (int color = 0; color < Palette.Count; color++)
                    if (after[color] == before[color] + 1) result.Add(color);
            }
            // Should the history ever disagree with the counts, fall back to the counts in palette order.
            if (result.Count != session.State.Drops.Total)
            {
                result.Clear();
                DropCounts drops = session.State.Drops;
                for (int color = 0; color < Palette.Count; color++)
                    for (int n = 0; n < drops[color]; n++) result.Add(color);
            }
        }

        void AddChip(int color, int count)
        {
            Image chip = Widgets.Round(Ui.NewImage($"Chip {Palette.Letter(color)}", chipRow, SpriteFactory.RoundedRect, Theme.Background), 32f);
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 22, 0, 0);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image dot = Ui.NewImage("Dot", chip.transform, SpriteFactory.Circle, Theme.ToColor(Palette.Color(color)));
            var dotSize = dot.gameObject.AddComponent<LayoutElement>();
            dotSize.preferredWidth = dotSize.preferredHeight = 32f;
            Ui.Stretch(Ui.NewImage("Ring", dot.transform, SpriteFactory.Ring, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.25f)).rectTransform);

            Text label = Widgets.Label(chip.transform, "Label", $"{Palette.Name(color)} {count}", 34, Theme.Ink);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
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
