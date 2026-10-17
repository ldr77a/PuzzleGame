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
    /// <see cref="CameliGame"/>). Editor keys: arrows/WASD move, R restart ("다시 하기"), H hint.
    /// </summary>
    public sealed class PlayScreen : MonoBehaviour
    {
        const int MaxBufferedMoves = 2;
        const float ClearViewDelay = 0.3f;
        const string OverfilledMessage = "방울이 너무 많아요. 다시 하기를 눌러 보세요.";
        const string StuckMessage = "이대로는 못 깨요. 다시 하기를 눌러 보세요.";
        const string HintIntroMessage = "막히면 힌트! 필요한 색 하나를 알려 줘요.";

        /// <summary>Restarts in one visit that introduce the hint button (the stars drop to ★), once ever.</summary>
        const int HintIntroRestarts = 2;

        /// <summary>How long Cameli looks startled after bumping into a wall, the edge, or a closed gate.</summary>
        const float OopsSeconds = 0.6f;

        /// <summary>Board taps right after entering are ignored: the second tap of a double tap on a garden tile or [다음].</summary>
        const float EnterTapGuardSeconds = 0.25f;

        readonly Queue<Direction> bufferedMoves = new Queue<Direction>();
        readonly PlaytestLog playtestLog = new PlaytestLog();
        readonly System.Random hintRandom = new System.Random();
        List<StageDefinition> stages;
        int stageIndex;
        GameSession session;
        float tapsFrom;

        /// <summary>Unscaled time until which a bump keeps the Oops face; 0 when no bump is pending.</summary>
        float oopsUntil;

        /// <summary>Index in <see cref="StageDefinition.Path"/> of the next step the path guide shows; -1 once it is off.</summary>
        int pathStep = -1;

        PlayHud hud;
        RectTransform boardArea;
        BoardView board;
        BoardInput boardInput;
        ClearView clearView;

        Action openMenu;
        Action openPlaytest;
        Action goToGarden;
        Action<Action> offerGrant;
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

        /// <summary>Moves, restart, and hints are accepted: a stage is open with no window over it.</summary>
        bool Interactive => session != null && !clearView.IsVisible && !overlayOpen();

        internal static PlayScreen Create(RectTransform parent, List<StageDefinition> stages, Action openMenu,
            Action openPlaytest, Action goToGarden, Action<Action> offerGrant, Func<bool> overlayOpen)
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
            hud = new PlayHud(root, OnMenuPressed, OnTitleHeld, Restart, OnHintPressed);

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

            clearView = ClearView.Create(root, Replay, () => goToGarden(), () => Enter(stageIndex + 1));
        }

        /// <summary>Opens a stage: a new visit (no restarts, no hints), logged from here.</summary>
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
            tapsFrom = Time.unscaledTime + EnterTapGuardSeconds;
            clearView.Hide();

            hud.SetStage(Stage);
            Ui.Stretch(boardArea, 40f, PlayHud.BoardBottom, 40f, hud.BoardTop);
            board.Build(Stage, session.State);
            boardInput.Columns = Stage.Width;
            boardInput.Rows = Stage.Height;
            hud.Refresh(session);
            oopsUntil = 0f;
            RefreshFace();
            ResetPathGuide();
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
            oopsUntil = 0f;
            pathStep = -1;
            hud.StopPulses();
            RefreshFace();
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

        /// <summary>Coming back to the app may cross midnight, when the day's hints refill: update the hint button.</summary>
        void OnApplicationFocus(bool focused)
        {
            if (focused && session != null) hud.Refresh(session);
        }

        /// <summary>Drops queued swipes, e.g. when a window opens over the board.</summary>
        internal void ClearBufferedMoves() => DropBufferedMoves();

        /// <summary>
        /// Empties the swipe queue. The path glow waits while swipes are queued (<see cref="RefreshGlow"/>), so it is
        /// brought back here.
        /// </summary>
        void DropBufferedMoves()
        {
            bufferedMoves.Clear();
            RefreshGlow();
        }

        void Update()
        {
            hud.Tick(Time.unscaledDeltaTime);
            // A bump's Oops wears off; not mid-move, so a clearing move's Happy face still waits for its landing.
            if (oopsUntil > 0f && Time.unscaledTime >= oopsUntil && !board.IsAnimating)
            {
                oopsUntil = 0f;
                RefreshFace();
            }
            if (!Interactive) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) RequestMove(Direction.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) RequestMove(Direction.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) RequestMove(Direction.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) RequestMove(Direction.Right);
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.hKey.wasPressedThisFrame) OnHintPressed();
            }

            // A restart or a hint above may have opened a window over the stage; check again before replaying swipes.
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
            AdvancePathGuide(direction, outcome);

            switch (outcome)
            {
                case MoveOutcome.Moved:
                case MoveOutcome.Ate:
                case MoveOutcome.PassedGate:
                case MoveOutcome.Cleared:
                    board.ClearGuides(); // also stops a hint's blinking; the next guides show on landing
                    hud.SetCountPulse(false);
                    hud.ShowMessage(outcome == MoveOutcome.PassedGate ? "문이 색을 가져갔어요. 새로 섞어 보세요." : "");
                    board.AnimateMove(from, session.State.Position, () => OnMoveLanded(outcome));
                    break;

                case MoveOutcome.BlockedByGate:
                {
                    DropBufferedMoves(); // also refreshes the glow, as a blocked move ends the path guide
                    int gateCell = Neighbor(from, direction);
                    board.Shake(gateCell);
                    Sfx.Play(SfxKind.Blocked);
                    GateDefinition gate = Stage.Gates[Stage.CellAt(gateCell).Gate];
                    hud.ShowMessage($"이 문은 {Words.Recipe(gate.Recipe)} 색일 때만 지나갈 수 있어요.");
                    Startle();
                    break;
                }

                case MoveOutcome.BlockedByWall:
                    DropBufferedMoves();
                    board.Shake(Neighbor(from, direction));
                    Sfx.Play(SfxKind.Blocked);
                    Startle();
                    break;

                case MoveOutcome.BlockedByEdge:
                    DropBufferedMoves();
                    board.Shake(-1);
                    Sfx.Play(SfxKind.Blocked);
                    Startle();
                    break;
            }
        }

        /// <summary>A bump: the Oops face for <see cref="OopsSeconds"/>.</summary>
        void Startle()
        {
            oopsUntil = Time.unscaledTime + OopsSeconds;
            RefreshFace();
        }

        /// <summary>
        /// The board token's face: Happy once the clearing move has landed (this is only called when no move is
        /// sliding), Oops while the mix is overfilled or right after a bump, else Normal.
        /// </summary>
        void RefreshFace()
        {
            CameliFace face = CameliFace.Normal;
            if (session != null)
            {
                if (session.IsCleared) face = CameliFace.Happy;
                else if (Rules.IsOverfilled(Stage, session.State) || Time.unscaledTime < oopsUntil) face = CameliFace.Oops;
            }
            board.SetFace(face);
        }

        void OnMoveLanded(MoveOutcome outcome)
        {
            board.Refresh(session.State);
            hud.Refresh(session);
            RefreshFace();
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
                hud.StopPulses();
                StartCoroutine(ClearSequence());
                return;
            }
            if (CannotClear())
            {
                hud.ShowMessage(StuckMessage);
                hud.SetRestartPulse(true);
            }
            else if (Rules.IsOverfilled(Stage, session.State))
            {
                hud.ShowMessage(OverfilledMessage);
            }
            RefreshGuides();
        }

        /// <summary>
        /// Tutorial nudge: on a tutorial stage without gates, a color has more drops than the target uses, so the attempt
        /// can't clear any more. Other stages leave it to the player (only the overfill message).
        /// </summary>
        bool CannotClear() => Stage.IsTutorial && Stage.Gates.Count == 0 && Rules.ExceedsTarget(Stage, session.State);

        /// <summary>Records the clear right away (as the prototype did), then shows the clear view after a beat.</summary>
        IEnumerator ClearSequence()
        {
            bufferedMoves.Clear();
            int stars = session.Stars;
            int moves = session.State.Moves;
            Progress.RecordClear(stageIndex, Stage.Id, stars);
            playtestLog.End(PlaytestLog.ResultClear, moves, stars);
            yield return new WaitForSecondsRealtime(ClearViewDelay);
            clearView.Show(Theme.ToColor(Stage.TargetColor), stars, session.Restarts, CapReason(stars),
                stageIndex < stages.Count - 1);
        }

        /// <summary>What cost the missing stars: the restarts, the hint, or both. "" at ★★★.</summary>
        string CapReason(int stars)
        {
            if (stars >= 3) return "";
            if (session.Restarts == 0) return "힌트 없이 깨면 ★★★";
            return session.UsedHint ? "다시 하기와 힌트 없이 깨면 ★★★" : "다시 하기 없이 깨면 ★★★";
        }

        /// <summary>
        /// Outside the tutorial, the first time ever a visit reaches <see cref="HintIntroRestarts"/> restarts, the hint
        /// button pulses (until pressed, cleared, or left) with a line about it.
        /// </summary>
        void MaybeIntroduceHint()
        {
            if (Stage.IsTutorial || session.Restarts != HintIntroRestarts || HintStore.IntroShown) return;
            HintStore.IntroShown = true;
            hud.SetHintPulse(true);
            hud.ShowMessage(HintIntroMessage);
        }

        /// <summary>
        /// Shows one recipe color the attempt hasn't revealed yet, picked at random: its cells blink until the next
        /// move and it stays on the goal card until a restart. Uses one of today's hints, or once they are gone
        /// offers the ad, which uses the hint right away. Free once every color is revealed (it only says so).
        /// </summary>
        void OnHintPressed()
        {
            if (!Interactive || Stage.IsTutorial) return; // the tutorial has no hint button
            board.CompleteAnimation();
            DropBufferedMoves();
            if (session == null || session.IsCleared) return; // the landing move may have cleared the stage
            hud.SetHintPulse(false);

            if (!session.CanRevealColor)
            {
                hud.ShowMessage("필요한 색을 모두 알려 줬어요.");
                return;
            }
            if (HintStore.TrySpend())
            {
                RevealColor();
                return;
            }
            offerGrant(() =>
            {
                if (session != null && session.CanRevealColor) RevealColor();
            });
        }

        void RevealColor()
        {
            int color = session.RevealColor(hintRandom);
            if (color < 0) return;
            playtestLog.CountHint();
            hud.Refresh(session);
            string name = Palette.Name(color);
            hud.ShowMessage(board.ShowBlink(color) > 0
                ? $"{Words.WithSubjectParticle(name)} 필요해요."
                : $"{Words.WithSubjectParticle(name)} 필요해요. 판에 남은 {name} 칸은 없어요.");
        }

        /// <summary>
        /// "다시 하기" during play: back to the start as a new attempt, one more restart on the stars, the hint colors
        /// hidden again. Nothing happens before the first move, or once cleared: the clear view is on its way, and its
        /// [다시] replays (<see cref="Replay"/>).
        /// </summary>
        void Restart()
        {
            if (session == null || overlayOpen()) return;
            board.CompleteAnimation(); // a clearing move still sliding finishes and clears first
            if (session.IsCleared || !session.Restart()) return;
            Sfx.Play(SfxKind.Restart);
            bufferedMoves.Clear();
            playtestLog.CountRestart();
            ShowFreshBoard();
            MaybeIntroduceHint();
        }

        /// <summary>
        /// Plays a cleared stage again (the clear view's [다시]): a new visit, so restarts start from zero, and a new
        /// playtest attempt.
        /// </summary>
        void Replay()
        {
            if (session == null || overlayOpen()) return;
            board.CompleteAnimation();
            StopAllCoroutines();
            bufferedMoves.Clear();
            if (!playtestLog.IsOpen) playtestLog.Begin(Stage.Id);
            session.StartOver();
            clearView.Hide();
            ShowFreshBoard();
        }

        /// <summary>A new attempt or visit at the start: tutorial guides from the beginning, and no restart nudge.</summary>
        void ShowFreshBoard()
        {
            board.Refresh(session.State);
            board.SnapToken(session.State.Position);
            hud.Refresh(session);
            hud.ShowMessage("");
            hud.SetRestartPulse(false);
            oopsUntil = 0f;
            RefreshFace();
            ResetPathGuide();
            RefreshGuides();
        }

        /// <summary>
        /// Shows the tutorial guides for the board at rest: the glow (<see cref="RefreshGlow"/>), and before the first
        /// move the finger swipe and the count highlight. Also stops a hint's blinking, so call it only when that ends anyway:
        /// on landing, entering, or a fresh start.
        /// </summary>
        void RefreshGuides()
        {
            board.ClearGuides();
            bool fresh = session != null && !session.IsCleared && session.State.Moves == 0;
            hud.SetCountPulse(fresh && Stage.Highlight == StageHighlight.Count);
            RefreshGlow();
            if (fresh && Stage.Swipe.HasValue) board.ShowSwipe(Stage.StartIndex, Stage.Swipe.Value);
        }

        /// <summary>
        /// The glowing cell: the path guide's next cell, else the focus cell before the first move. Leaves the hint's
        /// blinking alone. Waits while a move slides or swipes are queued, so the glow never runs ahead of the token;
        /// the landing (or <see cref="DropBufferedMoves"/>) shows it.
        /// </summary>
        void RefreshGlow()
        {
            if (session == null || board.IsAnimating) return;
            int cell = -1;
            if (!session.IsCleared)
            {
                if (pathStep >= 0 && bufferedMoves.Count == 0) cell = Neighbor(session.State.Position, Stage.Path[pathStep]);
                else if (pathStep < 0 && session.State.Moves == 0) cell = Stage.FocusIndex;
            }
            if (cell >= 0) board.ShowGlow(cell);
            else board.HideGlow();
        }

        /// <summary>Starts the path guide over at its first step, or leaves it off on stages without a path.</summary>
        void ResetPathGuide() => pathStep = Stage.Path.Count > 0 ? 0 : -1;

        /// <summary>
        /// A move along the path moves the guide on to the next step (off after the last); any other move, blocked
        /// ones included, ends it for the attempt. The new glow shows once the move lands.
        /// </summary>
        void AdvancePathGuide(Direction direction, MoveOutcome outcome)
        {
            if (pathStep < 0) return;
            if (outcome.IsMove() && direction == Stage.Path[pathStep]) pathStep++;
            else pathStep = -1;
            if (pathStep >= Stage.Path.Count) pathStep = -1;
        }

        void OnMenuPressed()
        {
            // Once cleared the clear view is showing or about to; the menu would hide the bloom under it.
            if (session == null || session.IsCleared) return;
            DropBufferedMoves();
            openMenu();
        }

        void OnTitleHeld()
        {
            if (session == null || overlayOpen()) return;
            DropBufferedMoves();
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
