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
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float GapFraction = 0.025f;
        const float TokenInset = 0.14f;
        const float MoveSeconds = 0.12f;
        const float ShakeSeconds = 0.25f;
        const float ShakeDistance = 14f;

        readonly List<Image> cells = new List<Image>();
        StageDefinition stage;
        RectTransform token;
        Image tokenFill;
        Coroutine moveRoutine;
        int moveTarget;
        Action moveFinished;

        public bool IsAnimating => moveRoutine != null;

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

            token = Ui.NewRect("Player", transform);
            Ui.Stretch(Ui.NewImage("Outline", token, SpriteFactory.Circle, Theme.Ink).rectTransform);
            tokenFill = Ui.NewImage("Fill", token, SpriteFactory.Circle, Color.white);
            Ui.Inset(tokenFill.rectTransform, 0.1f);
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
            tokenFill.color = state.Drops.IsEmpty ? Color.white : (Color)Theme.ToColor(ColorMath.Mix(state.Drops));
        }

        public void SnapToken(int index)
        {
            Place(token, index, TokenInset);
            token.SetAsLastSibling();
        }

        /// <summary>Slides the token to another cell, then calls <paramref name="finished"/>.</summary>
        public void AnimateMove(int from, int to, Action finished)
        {
            CompleteAnimation();
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
            (Vector2 fromMin, Vector2 fromMax) = Anchors(from, TokenInset);
            (Vector2 toMin, Vector2 toMax) = Anchors(to, TokenInset);
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
