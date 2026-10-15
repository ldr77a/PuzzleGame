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
    /// The Phase 2 screen also uses the guides: a glowing cell (tutorial focus, hint target), a hint arrow, and a
    /// tutorial finger swipe. They never take input and are cleared by <see cref="Build"/>.
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
        const float ArrowSize = 0.42f;
        const float ArrowBob = 0.08f;
        const float FingerSize = 0.36f;
        const float SwipeCycleSeconds = 1.8f;

        readonly List<Image> cells = new List<Image>();
        StageDefinition stage;
        RectTransform token;
        Image tokenFill;
        CameliFigure figure;
        RectTransform guides;
        CanvasGroup glow;
        RectTransform arrow;
        Vector2 arrowCenter, arrowStep;
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

            // Arrow and finger draw over the token; SnapToken keeps this layer last.
            guides = Ui.Stretch(Ui.NewRect("Guides", transform));
            arrow = NewArrow(guides);
            finger = NewFinger(guides);
            SnapToken(state.Position);
            Refresh(state);
        }

        /// <summary>Updates cell colors (eaten cells turn to floor) and the token's mixed color.</summary>
        public void Refresh(BoardState state)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                switch (Rules.VisibleKind(stage, state, i))
                {
                    case CellKind.Color:
                        cells[i].color = Theme.ToColor(Palette.Color(stage.CellAt(i).Color));
                        break;
                    case CellKind.Wall:
                        cells[i].color = Theme.Wall;
                        break;
                    default:
                        cells[i].color = Theme.Floor;
                        break;
                }
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

        /// <summary>Jumps a running move to its end so undo/restart never see a half-finished move.</summary>
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

        /// <summary>Makes a cell glow (tutorial focus or hint target) until <see cref="HideGlow"/> or <see cref="ClearGuides"/>.</summary>
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

        /// <summary>Hint arrow on the edge of <paramref name="cellIndex"/>, pointing toward its neighbor in <paramref name="direction"/>.</summary>
        public void ShowArrow(int cellIndex, Direction direction)
        {
            if (arrow == null || cellIndex < 0 || cellIndex >= cells.Count) return;
            Vector2 step = Step(direction);
            arrowStep = step;
            arrowCenter = CellCenter(cellIndex) + step * 0.55f;
            PlaceAt(arrow, arrowCenter, CellSide * ArrowSize);
            arrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg);
            arrow.gameObject.SetActive(true);
        }

        public void HideArrow()
        {
            if (arrow != null) arrow.gameObject.SetActive(false);
        }

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

        /// <summary>Hides the glow, the arrow, and the finger.</summary>
        public void ClearGuides()
        {
            HideGlow();
            HideArrow();
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
            if (arrow != null && arrow.gameObject.activeSelf)
                PlaceAt(arrow, arrowCenter + arrowStep * (ArrowBob * wave), CellSide * ArrowSize);
            if (finger != null && finger.gameObject.activeSelf)
            {
                swipeClock += Time.unscaledDeltaTime;
                AnimateFinger();
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

        static RectTransform NewArrow(Transform parent)
        {
            RectTransform root = Ui.NewRect("Hint Arrow", parent);
            Ui.Stretch(Ui.NewImage("Shade", root, SpriteFactory.PlayIcon, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.6f)).rectTransform);
            Ui.Inset(Ui.NewImage("Arrow", root, SpriteFactory.PlayIcon, Color.white).rectTransform, 0.1f);
            root.gameObject.SetActive(false);
            return root;
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
            if (definition.Kind == CellKind.Start)
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
