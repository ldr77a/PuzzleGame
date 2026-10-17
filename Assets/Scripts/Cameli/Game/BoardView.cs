using System;
using System.Collections;
using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Draws the grid and the player token and animates moves. Cells are positioned with normalized anchors,
    /// so the board scales with its RectTransform and needs no pixel math.
    /// The Phase 2 screen also uses the guides: a glowing cell (tutorial focus), the hinted color's cells
    /// blinking, and a tutorial finger swipe. They never take input and are cleared by <see cref="Build"/>.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float GapFraction = 0.025f;
        const float TokenInset = 0.14f;
        const float FigureInset = 0.03f;
        const float MoveSeconds = 0.12f;
        const float ShakeSeconds = 0.25f;
        const float ShakeDistance = 14f;

        /// <summary>The glow frame sticks out this fraction of a cell on every side.</summary>
        const float GlowGrow = 0.08f;
        const float GlowPulseSpeed = 4f;
        const float BlinksPerSecond = 1.6f;
        const float BlinkLighten = 0.6f;
        const float BlinkGrow = 0.06f;
        const float FingerSize = 0.36f;
        const float SwipeCycleSeconds = 1.8f;

        readonly List<Image> cells = new List<Image>();
        StageDefinition stage;
        RectTransform token;
        Image tokenFill;
        CameliFigure figure;
        RectTransform guides;
        CanvasGroup glow;
        BoardState shown;
        int blinkColor = -1;
        float blinkClock;
        CanvasGroup finger;
        Vector2 swipeFrom, swipeTo;
        float swipeClock;
        Coroutine moveRoutine;
        int moveTarget;
        Action moveFinished;

        public bool IsAnimating => moveRoutine != null;

        /// <summary>Draws the token as <see cref="CameliFigure"/> instead of the Phase 1 disc. Takes effect on the next <see cref="Build"/>.</summary>
        public bool FigureToken { get; set; }

        /// <summary>Token inset of the current build (the figure fills more of its cell than the disc).</summary>
        float Inset { get; set; } = TokenInset;

        public void Build(StageDefinition definition, BoardState state)
        {
            CompleteAnimation();
            StopAllCoroutines();
            stage = definition;
            cells.Clear();
            foreach (Transform child in transform) Destroy(child.gameObject);

            for (int i = 0; i < stage.CellCount; i++)
            {
                Image cell = Ui.NewImage($"Cell {i}", transform, SpriteFactory.RoundedRect, Theme.Floor);
                Place(cell.rectTransform, i, 0f);
                AddDecoration(cell.transform, stage.CellAt(i));
                cells.Add(cell);
            }
            JoinFences();

            glow = NewGlow();

            token = Ui.NewRect("Player", transform);
            Inset = FigureToken ? FigureInset : TokenInset;
            figure = null;
            tokenFill = null;
            if (FigureToken)
            {
                figure = new CameliFigure(token);
            }
            else
            {
                Ui.Stretch(Ui.NewImage("Outline", token, SpriteFactory.Circle, Theme.Ink).rectTransform);
                tokenFill = Ui.NewImage("Fill", token, SpriteFactory.Circle, Color.white);
                Ui.Inset(tokenFill.rectTransform, 0.1f);
            }

            // The finger draws over the token; SnapToken keeps this layer last.
            guides = Ui.Stretch(Ui.NewRect("Guides", transform));
            blinkColor = -1;
            finger = NewFinger(guides);
            SnapToken(state.Position);
            Refresh(state);
        }

        /// <summary>Updates cell colors (eaten cells turn to floor) and the token's mixed color.</summary>
        public void Refresh(BoardState state)
        {
            shown = state;
            for (int i = 0; i < cells.Count; i++)
            {
                cells[i].rectTransform.localScale = Vector3.one;
                // Everything but an uneaten color tile is plain floor; a wall's fence stands on it (AddDecoration).
                cells[i].color = Rules.VisibleKind(stage, state, i) == CellKind.Color
                    ? (Color)Theme.ToColor(Palette.Color(stage.CellAt(i).Color))
                    : Theme.Floor;
            }
            if (figure != null) figure.SetColor(CameliFigure.ColorOf(state.Drops));
            else tokenFill.color = state.Drops.IsEmpty ? Color.white : (Color)Theme.ToColor(ColorMath.Mix(state.Drops));
        }

        public void SnapToken(int index)
        {
            Place(token, index, Inset);
            token.SetAsLastSibling();
            if (guides != null) guides.SetAsLastSibling();
        }

        /// <summary>Turns the Cameli token toward a left or right move (also a blocked one); no effect on the disc.</summary>
        public void FaceToken(Direction direction) => figure?.Face(direction);

        /// <summary>Slides the token to another cell, then calls <paramref name="finished"/>.</summary>
        public void AnimateMove(int from, int to, Action finished)
        {
            CompleteAnimation();
            int dx = to % stage.Width - from % stage.Width;
            if (dx != 0) FaceToken(dx > 0 ? Direction.Right : Direction.Left);
            moveTarget = to;
            moveFinished = finished;
            moveRoutine = StartCoroutine(Slide(from, to));
        }

        /// <summary>Jumps a running move to its end so a restart or hint never sees a half-finished move.</summary>
        public void CompleteAnimation()
        {
            if (moveRoutine == null) return;
            StopCoroutine(moveRoutine);
            FinishMove();
        }

        /// <summary>Wiggles a cell (blocked move) or, with -1, the token (edge of the board).</summary>
        public void Shake(int cellIndex)
        {
            RectTransform target = cellIndex >= 0 && cellIndex < cells.Count ? cells[cellIndex].rectTransform : token;
            StartCoroutine(Wiggle(target));
        }

        IEnumerator Slide(int from, int to)
        {
            (Vector2 fromMin, Vector2 fromMax) = Anchors(from, Inset);
            (Vector2 toMin, Vector2 toMax) = Anchors(to, Inset);
            for (float t = 0f; t < MoveSeconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / MoveSeconds) * (1f - t / MoveSeconds);
                token.anchorMin = Vector2.LerpUnclamped(fromMin, toMin, k);
                token.anchorMax = Vector2.LerpUnclamped(fromMax, toMax, k);
                yield return null;
            }
            FinishMove();
        }

        void FinishMove()
        {
            moveRoutine = null;
            SnapToken(moveTarget);
            Action done = moveFinished;
            moveFinished = null;
            done?.Invoke();
        }

        IEnumerator Wiggle(RectTransform target)
        {
            for (float t = 0f; t < ShakeSeconds; t += Time.unscaledDeltaTime)
            {
                float decay = 1f - t / ShakeSeconds;
                target.anchoredPosition = new Vector2(Mathf.Sin(t * 60f) * ShakeDistance * decay, 0f);
                yield return null;
            }
            target.anchoredPosition = Vector2.zero;
        }

        /// <summary>Makes a cell glow (tutorial focus) until <see cref="HideGlow"/> or <see cref="ClearGuides"/>.</summary>
        public void ShowGlow(int cellIndex)
        {
            if (glow == null || cellIndex < 0 || cellIndex >= cells.Count) return;
            Place((RectTransform)glow.transform, cellIndex, -GlowGrow);
            glow.gameObject.SetActive(true);
        }

        public void HideGlow()
        {
            if (glow != null) glow.gameObject.SetActive(false);
        }

        /// <summary>
        /// Hint: every uneaten cell of palette color <paramref name="color"/> blinks until <see cref="HideBlink"/> or
        /// <see cref="ClearGuides"/>. Returns how many cells blink (0 when none of that color is left).
        /// </summary>
        public int ShowBlink(int color)
        {
            HideBlink();
            if (stage == null) return 0;
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
                if (IsBlinking(i, color)) count++;
            if (count == 0) return 0;
            blinkColor = color;
            blinkClock = 0f;
            return count;
        }

        public void HideBlink()
        {
            if (blinkColor < 0) return;
            blinkColor = -1;
            Refresh(shown);
        }

        bool IsBlinking(int cellIndex, int color) =>
            Rules.VisibleKind(stage, shown, cellIndex) == CellKind.Color && stage.CellAt(cellIndex).Color == color;

        /// <summary>Tutorial: a fingertip swipes from <paramref name="cellIndex"/> toward <paramref name="direction"/>, over and over.</summary>
        public void ShowSwipe(int cellIndex, Direction direction)
        {
            if (finger == null || cellIndex < 0 || cellIndex >= cells.Count) return;
            swipeFrom = CellCenter(cellIndex);
            swipeTo = swipeFrom + Step(direction);
            if (!finger.gameObject.activeSelf) swipeClock = 0f;
            finger.gameObject.SetActive(true);
            AnimateFinger();
        }

        public void HideSwipe()
        {
            if (finger != null) finger.gameObject.SetActive(false);
        }

        /// <summary>Hides the glow, the blinking, and the finger.</summary>
        public void ClearGuides()
        {
            HideGlow();
            HideBlink();
            HideSwipe();
        }

        void Update()
        {
            float wave = Mathf.Sin(Time.unscaledTime * GlowPulseSpeed) * 0.5f + 0.5f;
            if (glow != null && glow.gameObject.activeSelf)
            {
                glow.alpha = Mathf.Lerp(0.55f, 1f, wave);
                glow.transform.localScale = Vector3.one * (1f + 0.035f * wave);
            }
            if (blinkColor >= 0) AnimateBlink();
            if (finger != null && finger.gameObject.activeSelf)
            {
                swipeClock += Time.unscaledDeltaTime;
                AnimateFinger();
            }
        }

        /// <summary>The hinted cells flash toward white and swell a little, starting lit so the first blink shows at once.</summary>
        void AnimateBlink()
        {
            blinkClock += Time.unscaledDeltaTime;
            float lit = Mathf.Cos(blinkClock * BlinksPerSecond * 2f * Mathf.PI) * 0.5f + 0.5f;
            Color tile = Theme.ToColor(Palette.Color(blinkColor));
            Color flash = Color.Lerp(tile, Color.white, BlinkLighten * lit);
            Vector3 scale = Vector3.one * (1f + BlinkGrow * lit);
            for (int i = 0; i < cells.Count; i++)
            {
                if (!IsBlinking(i, blinkColor)) continue;
                cells[i].color = flash;
                cells[i].rectTransform.localScale = scale;
            }
        }

        /// <summary>One swipe per cycle: fade in on the start cell, glide to the neighbor, fade out, pause.</summary>
        void AnimateFinger()
        {
            float t = Mathf.Repeat(swipeClock, SwipeCycleSeconds) / SwipeCycleSeconds;
            float travel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, t));
            finger.alpha = t < 0.15f ? t / 0.15f : t < 0.6f ? 1f : Mathf.Clamp01(1f - (t - 0.6f) / 0.2f);
            PlaceAt((RectTransform)finger.transform, Vector2.Lerp(swipeFrom, swipeTo, travel), CellSide * FingerSize);
        }

        CanvasGroup NewGlow()
        {
            RectTransform root = Ui.NewRect("Glow", transform);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Image shade = Ui.NewImage("Shade", root, SpriteFactory.RoundedFrame, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.35f));
            shade.pixelsPerUnitMultiplier = 0.55f;
            Ui.Stretch(shade.rectTransform);
            Image light = Ui.NewImage("Light", root, SpriteFactory.RoundedFrame, Color.white);
            light.pixelsPerUnitMultiplier = 0.6f;
            Ui.Stretch(light.rectTransform, 5f, 5f, 5f, 5f);
            root.gameObject.SetActive(false);
            return group;
        }

        static CanvasGroup NewFinger(Transform parent)
        {
            RectTransform root = Ui.NewRect("Finger", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Ui.Stretch(Ui.NewImage("Touch", root, SpriteFactory.Circle, new Color(1f, 1f, 1f, 0.85f)).rectTransform);
            Ui.Stretch(Ui.NewImage("Ring", root, SpriteFactory.Ring, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.55f)).rectTransform);
            root.gameObject.SetActive(false);
            return group;
        }

        /// <summary>Width of one cell in normalized board units.</summary>
        float CellWidth => (1f - (stage.Width + 1) * GapFraction) / stage.Width;
        float CellHeight => (1f - (stage.Height + 1) * GapFraction) / stage.Height;

        /// <summary>A square of a cell's shorter side, in normalized board units.</summary>
        Vector2 CellSide => Vector2.one * Mathf.Min(CellWidth, CellHeight);

        Vector2 CellCenter(int index)
        {
            (Vector2 min, Vector2 max) = Anchors(index, 0f);
            return (min + max) / 2f;
        }

        /// <summary>One cell's step in <paramref name="direction"/>, in normalized board units (y up).</summary>
        Vector2 Step(Direction direction) =>
            new Vector2(direction.Dx() * (CellWidth + GapFraction), -direction.Dy() * (CellHeight + GapFraction));

        static void PlaceAt(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = center - size / 2f;
            rt.anchorMax = center + size / 2f;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void AddDecoration(Transform cell, Cell definition)
        {
            if (definition.Kind == CellKind.Wall)
            {
                Ui.Stretch(Ui.NewImage("Fence Outline", cell, SpriteFactory.FenceOutline, Theme.FenceOutline).rectTransform);
                Ui.Stretch(Ui.NewImage("Fence Rails", cell, SpriteFactory.FenceRails, Theme.FenceRail).rectTransform);
                Ui.Stretch(Ui.NewImage("Fence Plank Outline", cell, SpriteFactory.FencePlankOutline, Theme.FenceOutline).rectTransform);
                Ui.Stretch(Ui.NewImage("Fence Planks", cell, SpriteFactory.FencePlanks, Theme.FencePlank).rectTransform);
            }
            else if (definition.Kind == CellKind.Start)
            {
                Image mark = Ui.NewImage("Start Mark", cell, SpriteFactory.Ring, new Color(Theme.Muted.r, Theme.Muted.g, Theme.Muted.b, 0.5f));
                Ui.Inset(mark.rectTransform, 0.3f);
            }
            else if (definition.Kind == CellKind.Gate)
            {
                GateDefinition gate = stage.Gates[definition.Gate];
                Ui.Inset(Ui.NewImage("Gate Ring", cell, SpriteFactory.Ring, Theme.ToColor(gate.Color)).rectTransform, 0.06f);
                AddRecipeDots(cell, gate.Recipe);
            }
        }

        /// <summary>
        /// Joins neighboring fence cells so walls read as one fence: the rails continue across the gap to a fence on
        /// the right, and a post (the middle plank) continues down to a fence below. Drawn over the cells, each
        /// piece on an outline-colored piece one outline wider, like the fence sprites.
        /// </summary>
        void JoinFences()
        {
            float w = CellWidth, h = CellHeight;
            // Rail bridges reach this far into each neighbor (of a cell): less than the rail-only strip at the cell's
            // edge, outside the outer planks and their outline, since the bridges draw over the cells.
            float overlap = 0.4f * (1f - SpriteFactory.FenceOuterPlankEdge - SpriteFactory.FenceOutlineWidth) / 2f;
            for (int i = 0; i < stage.CellCount; i++)
            {
                if (stage.CellAt(i).Kind != CellKind.Wall) continue;
                int x = i % stage.Width, y = i / stage.Width;
                (Vector2 min, Vector2 max) = Anchors(i, 0f);
                if (x + 1 < stage.Width && stage.CellAt(i + 1).Kind == CellKind.Wall)
                {
                    float left = max.x - w * overlap, right = max.x + GapFraction + w * overlap;
                    Bridge("Rail Bridge", left, min.y + h * Fraction(SpriteFactory.FenceUpperRailBottom), right, min.y + h * Fraction(SpriteFactory.FenceUpperRailTop),
                        0f, h * OutlineFraction, Theme.FenceRail);
                    Bridge("Rail Bridge", left, min.y + h * Fraction(SpriteFactory.FenceLowerRailBottom), right, min.y + h * Fraction(SpriteFactory.FenceLowerRailTop),
                        0f, h * OutlineFraction, Theme.FenceRail);
                }
                if (y + 1 < stage.Height && stage.CellAt(i + stage.Width).Kind == CellKind.Wall)
                {
                    // From this plank's foot down into the plank below, covering its point so the two read as one post.
                    float half = SpriteFactory.FenceMiddlePlankHalfWidth / 2f;
                    float lowerTop = min.y - GapFraction;
                    Bridge("Post Bridge", min.x + w * (0.5f - half), lowerTop - h * (1f - Fraction(SpriteFactory.FencePlankShoulder)) - h * 0.02f,
                        min.x + w * (0.5f + half), min.y + h * (Fraction(SpriteFactory.FencePlankBottom) + 0.02f), w * OutlineFraction, 0f, Theme.FencePlank);
                }
            }
        }

        /// <summary>The fence outline's width as a fraction of a cell (sprite units span 2).</summary>
        const float OutlineFraction = SpriteFactory.FenceOutlineWidth / 2f;

        /// <summary>Normalized sprite units (-1..1) to a fraction of the cell from its bottom edge.</summary>
        static float Fraction(float normalized) => (normalized + 1f) / 2f;

        /// <summary>A flat piece in board units, over an outline piece grown by <paramref name="outlineX"/> and <paramref name="outlineY"/>.</summary>
        void Bridge(string name, float left, float bottom, float right, float top, float outlineX, float outlineY, Color color)
        {
            Place(Ui.NewImage(name + " Outline", transform, null, Theme.FenceOutline).rectTransform,
                left - outlineX, bottom - outlineY, right + outlineX, top + outlineY);
            Place(Ui.NewImage(name, transform, null, color).rectTransform, left, bottom, right, top);
        }

        static void Place(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(left, bottom);
            rt.anchorMax = new Vector2(right, top);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Ingredient dots inside a gate (shown in Medium per the design).</summary>
        static void AddRecipeDots(Transform cell, DropCounts recipe)
        {
            const float size = 0.17f;
            const float spacing = 0.05f;
            int total = recipe.Total;
            int slot = 0;
            for (int color = 0; color < Palette.Count; color++)
            {
                for (int n = 0; n < recipe[color]; n++, slot++)
                {
                    Image dot = Ui.NewImage("Ingredient", cell, SpriteFactory.Circle, Theme.ToColor(Palette.Color(color)));
                    float center = 0.5f + (slot - (total - 1) / 2f) * (size + spacing);
                    dot.rectTransform.anchorMin = new Vector2(center - size / 2f, 0.5f - size / 2f);
                    dot.rectTransform.anchorMax = new Vector2(center + size / 2f, 0.5f + size / 2f);
                    dot.rectTransform.offsetMin = Vector2.zero;
                    dot.rectTransform.offsetMax = Vector2.zero;
                }
            }
        }

        void Place(RectTransform rt, int index, float inset)
        {
            (rt.anchorMin, rt.anchorMax) = Anchors(index, inset);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        (Vector2 min, Vector2 max) Anchors(int index, float inset)
        {
            int x = index % stage.Width;
            int y = index / stage.Width;
            float width = (1f - (stage.Width + 1) * GapFraction) / stage.Width;
            float height = (1f - (stage.Height + 1) * GapFraction) / stage.Height;
            float left = GapFraction + x * (width + GapFraction);
            float top = 1f - (GapFraction + y * (height + GapFraction));
            return (new Vector2(left + width * inset, top - height + height * inset),
                    new Vector2(left + width - width * inset, top - height * inset));
        }
    }
}
