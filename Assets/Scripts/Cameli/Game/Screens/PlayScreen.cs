using System;
using System.Collections;
using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// The Phase 2 play screen: runs a <see cref="GameSession"/> on the board, with the HUD, hints, tutorial
    /// guides, feedback, the clear sequence, and the playtest log (same attempt boundaries as
    /// <see cref="CameliGame"/>). Editor keys: arrows/WASD move, Z or Backspace undo, R restart, H hint.
    /// </summary>
    public sealed class PlayScreen : MonoBehaviour
    {
        const int MaxBufferedMoves = 2;
        const float ClearViewDelay = 0.3f;
        const string OverfilledMessage = "방울이 너무 많아요. 되돌리기나 처음부터를 눌러 보세요.";

        /// <summary>Board taps right after entering are ignored: the second tap of a double tap on a garden tile or [다음].</summary>
        const float EnterTapGuardSeconds = 0.25f;

        readonly Queue<Direction> bufferedMoves = new Queue<Direction>();
        readonly PlaytestLog playtestLog = new PlaytestLog();
        List<StageDefinition> stages;
        int stageIndex;
        GameSession session;
        Direction? stepHint;
        float tapsFrom;

        PlayHud hud;
        RectTransform boardArea;
        BoardView board;
        BoardInput boardInput;
        ClearView clearView;

        Action openMenu;
        Action openPlaytest;
        Action goToGarden;
        Action<GrantKind, Action> offerGrant;
        Func<bool> overlayOpen;

        StageDefinition Stage => session.Stage;

        /// <summary>True while a stage is open on this screen (between <see cref="Enter"/> and leaving).</summary>
        public bool HasSession => session != null;

        public bool ClearShowing => clearView.IsVisible;

        /// <summary>
        /// The open stage is cleared: from the moment the clearing move is made, through its slide and the beat
        /// before the clear view, and while the clear view shows.
        /// </summary>
        public bool StageCleared => session != null && session.IsCleared;

        /// <summary>Moves, undo, restart, and hints are accepted: a stage is open with no window over it.</summary>
        bool Interactive => session != null && !clearView.IsVisible && !overlayOpen();

        internal static PlayScreen Create(RectTransform parent, List<StageDefinition> stages, Action openMenu,
            Action openPlaytest, Action goToGarden, Action<GrantKind, Action> offerGrant, Func<bool> overlayOpen)
        {
            RectTransform layer = Widgets.Layer(parent, "Play Screen");
            var screen = layer.gameObject.AddComponent<PlayScreen>();
            screen.stages = stages;
            screen.openMenu = openMenu;
            screen.openPlaytest = openPlaytest;
            screen.goToGarden = goToGarden;
            screen.offerGrant = offerGrant;
            screen.overlayOpen = overlayOpen;
            screen.Build(layer);
            layer.gameObject.SetActive(false);
            return screen;
        }

        void Build(RectTransform root)
        {
            hud = new PlayHud(root, OnMenuPressed, Restart, OnTitleHeld, OnUndoPressed, OnHintPressed);

            boardArea = Ui.NewRect("Board Area", root);
            Image boardImage = Widgets.Round(Ui.NewImage("Board", boardArea, SpriteFactory.RoundedRect, Theme.Board), 40f);
            boardImage.raycastTarget = true;
            var fitter = boardImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            board = boardImage.gameObject.AddComponent<BoardView>();
            board.FigureToken = true;
            boardInput = boardImage.gameObject.AddComponent<BoardInput>();
            boardInput.Swiped += RequestMove;
            boardInput.Tapped += OnBoardTapped;

            clearView = ClearView.Create(root, Restart, () => goToGarden(), () => Enter(stageIndex + 1));
        }

        /// <summary>Opens a stage: a new attempt, logged from here.</summary>
        internal void Enter(int index)
        {
            if (index < 0 || index >= stages.Count) return;
            gameObject.SetActive(true);
            // Finish a running move first: its landing callback belongs to the old session, and any clear view it
            // schedules is cancelled by StopAllCoroutines right after.
            board.CompleteAnimation();
            StopAllCoroutines();
            if (session != null) playtestLog.End(PlaytestLog.ResultLeft, session.State.Moves, 0);
            stageIndex = index;
            Progress.LastPlayed = index;
            session = new GameSession(stages[index]);
            playtestLog.Begin(Stage.Id);
            bufferedMoves.Clear();
            stepHint = null;
            tapsFrom = Time.unscaledTime + EnterTapGuardSeconds;
            clearView.Hide();

            hud.SetStage(Stage, HintStore.IsRecipeRevealed(Stage.Id));
            Ui.Stretch(boardArea, 40f, PlayHud.BoardBottom, 40f, hud.BoardTop);
            board.Build(Stage, session.State);
            boardInput.Columns = Stage.Width;
            boardInput.Rows = Stage.Height;
            hud.Refresh(session);
            RefreshGuides();
        }

        /// <summary>Closes the stage for another screen; the open attempt is logged as left.</summary>
        internal void Leave() => Close(PlaytestLog.ResultLeft);

        /// <summary>Closes the stage before a progress reset; the open attempt is logged as reset.</summary>
        internal void CloseForReset() => Close(PlaytestLog.ResultReset);

        void Close(string result)
        {
            if (session != null)
            {
                board.CompleteAnimation(); // a clearing move still sliding logs its clear first
                playtestLog.End(result, session.State.Moves, 0);
            }
            StopAllCoroutines();
            bufferedMoves.Clear();
            clearView.Hide();
            session = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Phones can kill a backgrounded app without calling OnApplicationQuit, so the open attempt is written on
        /// pause and a new one continues the same stage on return.
        /// </summary>
        internal void OnAppPause(bool paused)
        {
            if (session == null) return;
            if (paused)
            {
                board.CompleteAnimation();
                playtestLog.End(PlaytestLog.ResultPause, session.State.Moves, 0);
            }
            else if (!session.IsCleared && !playtestLog.IsOpen)
            {
                playtestLog.Begin(Stage.Id, session.State.Moves);
            }
        }

        internal void OnAppQuit()
        {
            if (session != null) playtestLog.End(PlaytestLog.ResultQuit, session.State.Moves, 0);
        }

        /// <summary>Drops queued swipes, e.g. when a window opens over the board.</summary>
        internal void ClearBufferedMoves() => bufferedMoves.Clear();

        void Update()
        {
            hud.Tick(Time.unscaledDeltaTime);
            if (!Interactive) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) RequestMove(Direction.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) RequestMove(Direction.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) RequestMove(Direction.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) RequestMove(Direction.Right);
                if (keyboard.zKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) OnUndoPressed();
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.hKey.wasPressedThisFrame) OnHintPressed();
            }

            // Undo, restart, or a hint above may have closed the stage's input; check again before replaying swipes.
            if (Interactive && !board.IsAnimating && bufferedMoves.Count > 0) ApplyMove(bufferedMoves.Dequeue());
        }

        void RequestMove(Direction direction)
        {
            if (!Interactive || session.IsCleared) return;
            if (board.IsAnimating)
            {
                if (bufferedMoves.Count < MaxBufferedMoves) bufferedMoves.Enqueue(direction);
                return;
            }
            ApplyMove(direction);
        }

        void ApplyMove(Direction direction)
        {
            if (session.IsCleared) return;
            int from = session.State.Position;
            board.FaceToken(direction);
            MoveOutcome outcome = session.Move(direction);

            switch (outcome)
            {
                case MoveOutcome.Moved:
                case MoveOutcome.Ate:
                case MoveOutcome.PassedGate:
                case MoveOutcome.Cleared:
                    stepHint = null;
                    board.ClearGuides();
                    hud.ShowMessage(outcome == MoveOutcome.PassedGate ? "문이 색을 가져갔어요. 새로 섞어 보세요." : "");
                    board.AnimateMove(from, session.State.Position, () => OnMoveLanded(outcome));
                    break;

                case MoveOutcome.BlockedByGate:
                {
                    bufferedMoves.Clear();
                    int gateCell = Neighbor(from, direction);
                    board.Shake(gateCell);
                    Sfx.Play(SfxKind.Blocked);
                    GateDefinition gate = Stage.Gates[Stage.CellAt(gateCell).Gate];
                    hud.ShowMessage($"이 문은 {Words.Recipe(gate.Recipe)} 색일 때만 지나갈 수 있어요.");
                    break;
                }

                case MoveOutcome.BlockedByWall:
                    bufferedMoves.Clear();
                    board.Shake(Neighbor(from, direction));
                    Sfx.Play(SfxKind.Blocked);
                    break;

                case MoveOutcome.BlockedByEdge:
                    bufferedMoves.Clear();
                    board.Shake(-1);
                    Sfx.Play(SfxKind.Blocked);
                    break;
            }
        }

        void OnMoveLanded(MoveOutcome outcome)
        {
            board.Refresh(session.State);
            hud.Refresh(session);
            if (outcome == MoveOutcome.Ate || outcome == MoveOutcome.Cleared)
            {
                Sfx.Play(SfxKind.Eat);
                Haptics.Tap();
            }
            else if (outcome == MoveOutcome.PassedGate)
            {
                Sfx.Play(SfxKind.Gate);
            }

            if (outcome == MoveOutcome.Cleared)
            {
                StartCoroutine(ClearSequence());
                return;
            }
            if (Rules.IsOverfilled(Stage, session.State)) hud.ShowMessage(OverfilledMessage);
            RefreshGuides();
        }

        /// <summary>Records the clear right away (as the prototype did), then shows the clear view after a beat.</summary>
        IEnumerator ClearSequence()
        {
            bufferedMoves.Clear();
            int stars = session.Stars;
            int moves = session.State.Moves;
            Progress.RecordClear(stageIndex, Stage.Id, stars);
            playtestLog.End(PlaytestLog.ResultClear, moves, stars);
            yield return new WaitForSecondsRealtime(ClearViewDelay);
            clearView.Show(Theme.ToColor(Stage.TargetColor), stars, moves, Stage.OptimalMoves, CapReason(stars),
                stageIndex < stages.Count - 1);
        }

        /// <summary>The line explaining a star cap, only when the cap actually cost a star.</summary>
        string CapReason(int stars)
        {
            if (!session.Assisted || Scoring.Stars(session.State.Moves, Stage.OptimalMoves) <= stars) return "";
            return session.UsedUndo ? "되돌리기 없이 깨면 ★★★" : "힌트 없이 깨면 ★★★";
        }

        void OnUndoPressed()
        {
            if (!Interactive) return;
            board.CompleteAnimation();
            bufferedMoves.Clear();
            if (session == null || session.IsCleared) return; // the landing move may have cleared the stage

            if (session.UndosRemaining == 0)
            {
                Sfx.Play(SfxKind.Button);
                offerGrant(GrantKind.Undo, () =>
                {
                    if (session == null || session.IsCleared) return;
                    session.GrantUndos(GrantPopup.UndoReward);
                    hud.Refresh(session);
                });
                return;
            }

            if (session.Undo())
            {
                Sfx.Play(SfxKind.Undo);
                playtestLog.CountUndo();
                stepHint = null;
                board.Refresh(session.State);
                board.SnapToken(session.State.Position);
                hud.Refresh(session);
                hud.ShowMessage(Rules.IsOverfilled(Stage, session.State) ? OverfilledMessage : "");
                RefreshGuides();
            }
            else
            {
                Sfx.Play(SfxKind.Button);
                hud.ShowMessage("되돌릴 걸음이 없어요.");
            }
        }

        /// <summary>
        /// First hint on a stage reveals the recipe; later ones show the next step from the current state. A hint
        /// is only spent when it tells the player something new (spec §6), so the grant popup only comes up when
        /// a hint would be spent and none are left: the glowing-cell reminder and the stuck and search-limit
        /// messages are free and show even at 0 hints.
        /// </summary>
        void OnHintPressed()
        {
            if (!Interactive) return;
            board.CompleteAnimation();
            bufferedMoves.Clear();
            if (session == null || session.IsCleared) return;

            if (stepHint.HasValue)
            {
                hud.ShowMessage("빛나는 칸으로 가 보세요.");
                return;
            }

            if (!HintStore.IsRecipeRevealed(Stage.Id))
            {
                if (HintStore.Count == 0)
                {
                    OfferHintGrant();
                    return;
                }
                HintStore.TrySpend();
                HintStore.RevealRecipe(Stage.Id);
                hud.Card.ShowRecipe(true);
                hud.Refresh(session);
                hud.ShowMessage("목표 레시피예요. 다음 힌트부터는 갈 칸을 알려 줘요.");
                return;
            }

            Hint hint = Hints.Next(session);
            switch (hint.Kind)
            {
                case HintKind.NextStep:
                    if (HintStore.Count == 0)
                    {
                        OfferHintGrant();
                        return;
                    }
                    HintStore.TrySpend();
                    session.MarkStepHint();
                    stepHint = hint.Direction;
                    hud.Refresh(session);
                    hud.ShowMessage("");
                    RefreshGuides();
                    break;

                case HintKind.Stuck:
                    hud.ShowMessage(hint.UndoWithinReach
                        ? $"이대로는 못 풀어요. 되돌리기 {hint.UndosNeeded}번 하면 풀 수 있어요"
                        : "이대로는 못 풀어요. 처음부터 다시 해 보세요");
                    break;

                case HintKind.SearchLimit:
                    hud.ShowMessage("지금은 길을 찾지 못했어요");
                    break;
            }
        }

        void OfferHintGrant()
        {
            offerGrant(GrantKind.Hint, () =>
            {
                HintStore.Add(GrantPopup.HintReward);
                if (session != null) hud.Refresh(session);
            });
        }

        /// <summary>A new attempt on the same stage: full undos, no star cap, guides back.</summary>
        void Restart()
        {
            if (session == null || overlayOpen()) return;
            board.CompleteAnimation();
            StopAllCoroutines();
            bufferedMoves.Clear();
            // Replaying a cleared stage is a new attempt; restarting an unfinished one is part of it.
            if (playtestLog.IsOpen) playtestLog.CountRestart();
            else playtestLog.Begin(Stage.Id);
            session.Restart();
            stepHint = null;
            clearView.Hide();
            board.Refresh(session.State);
            board.SnapToken(session.State.Position);
            hud.Refresh(session);
            hud.ShowMessage("");
            RefreshGuides();
        }

        /// <summary>
        /// Shows what should guide the player now: the hinted step (glowing cell and arrow) until the next move;
        /// otherwise, before the first move, the stage's tutorial focus cell and finger swipe.
        /// </summary>
        void RefreshGuides()
        {
            board.ClearGuides();
            if (session == null || session.IsCleared) return;
            int position = session.State.Position;
            if (stepHint.HasValue)
            {
                board.ShowGlow(Neighbor(position, stepHint.Value));
                board.ShowArrow(position, stepHint.Value);
                return;
            }
            if (session.State.Moves > 0) return;
            if (Stage.FocusIndex >= 0) board.ShowGlow(Stage.FocusIndex);
            if (Stage.Swipe.HasValue) board.ShowSwipe(Stage.StartIndex, Stage.Swipe.Value);
        }

        void OnMenuPressed()
        {
            // Once cleared the clear view is showing or about to; the menu would hide the bloom under it.
            if (session == null || session.IsCleared) return;
            bufferedMoves.Clear();
            openMenu();
        }

        void OnTitleHeld()
        {
            if (session == null || overlayOpen()) return;
            bufferedMoves.Clear();
            openPlaytest();
        }

        void OnBoardTapped(int column, int row)
        {
            if (session == null || Time.unscaledTime < tapsFrom) return;
            int x = session.State.Position % Stage.Width;
            int y = session.State.Position / Stage.Width;
            int dx = column - x, dy = row - y;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) return;
            RequestMove(dx == 1 ? Direction.Right : dx == -1 ? Direction.Left : dy == 1 ? Direction.Down : Direction.Up);
        }

        int Neighbor(int index, Direction direction)
        {
            int x = index % Stage.Width + direction.Dx();
            int y = index / Stage.Width + direction.Dy();
            return x < 0 || x >= Stage.Width || y < 0 || y >= Stage.Height ? -1 : Stage.IndexOf(x, y);
        }
    }
}
