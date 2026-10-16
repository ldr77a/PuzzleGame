using System.Collections;
using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Phase 1 prototype entry point. Put it on an empty GameObject: it builds the UI, loads the stages, and
    /// turns swipes, taps, and keys into moves on a <see cref="GameSession"/>.
    /// Editor keys: arrows/WASD move, R restart. Holding the stage title for two seconds opens
    /// the playtest tools (next tester, playtest log summary).
    /// </summary>
    public sealed class CameliGame : MonoBehaviour
    {
        const int MaxBufferedMoves = 2;
        const float ClearPanelDelay = 0.3f;
        const string OverfilledMessage = "방울이 너무 많아요. 처음부터를 눌러 보세요.";

        readonly Queue<Direction> bufferedMoves = new Queue<Direction>();
        List<StageDefinition> stages;
        int stageIndex;
        GameSession session;
        BoardView board;
        BoardInput boardInput;
        HudView hud;
        ClearPanel clearPanel;
        PlaytestPanel playtestPanel;
        readonly PlaytestLog playtestLog = new PlaytestLog();
        bool showingAllClear;

        StageDefinition Stage => session.Stage;
        int LastOpenIndex => Mathf.Min(Progress.Unlocked, stages.Count - 1);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // playtesters stop to think; keep the screen on meanwhile
            EnsureSceneBasics();
            BuildUi();

            stages = StageLibrary.LoadAll();
            if (stages.Count == 0)
            {
                hud.ShowMessage($"스테이지 파일이 없어요 (Resources/{StageLibrary.ResourceFolder})");
                return;
            }
            StartStage(Mathf.Clamp(Progress.LastPlayed, 0, LastOpenIndex));
        }

        void Update()
        {
            if (session == null || playtestPanel.IsVisible) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) RequestMove(Direction.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) RequestMove(Direction.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) RequestMove(Direction.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) RequestMove(Direction.Right);
                if (keyboard.rKey.wasPressedThisFrame) Restart();
            }

            if (!board.IsAnimating && bufferedMoves.Count > 0) ApplyMove(bufferedMoves.Dequeue());
        }

        void StartStage(int index)
        {
            // Finish a running move first: its landing callback belongs to the old session, and any clear
            // panel it schedules is cancelled by StopAllCoroutines right after.
            board.CompleteAnimation();
            StopAllCoroutines();
            if (session != null) playtestLog.End(PlaytestLog.ResultLeft, session.State.Moves, 0);
            stageIndex = index;
            Progress.LastPlayed = index;
            session = new GameSession(stages[index]);
            playtestLog.Begin(Stage.Id);
            bufferedMoves.Clear();
            clearPanel.Hide();
            showingAllClear = false;

            board.Build(Stage, session.State);
            boardInput.Columns = Stage.Width;
            boardInput.Rows = Stage.Height;
            hud.SetStage(Stage, index, stages.Count, index > 0, index < LastOpenIndex);
            hud.Refresh(session);
            hud.ShowMessage("");
        }

        void RequestMove(Direction direction)
        {
            if (session == null || session.IsCleared || clearPanel.IsVisible) return;
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
            MoveOutcome outcome = session.Move(direction);

            switch (outcome)
            {
                case MoveOutcome.Moved:
                case MoveOutcome.Ate:
                case MoveOutcome.PassedGate:
                case MoveOutcome.Cleared:
                    hud.ShowMessage(outcome == MoveOutcome.PassedGate ? "문이 색을 가져갔어요. 새로 섞어 보세요." : "");
                    board.AnimateMove(from, session.State.Position, () => OnMoveLanded(outcome));
                    break;

                case MoveOutcome.BlockedByGate:
                {
                    bufferedMoves.Clear();
                    int gateCell = Neighbor(from, direction);
                    board.Shake(gateCell);
                    GateDefinition gate = Stage.Gates[Stage.CellAt(gateCell).Gate];
                    hud.ShowMessage($"이 문은 {Words.Recipe(gate.Recipe)} 색일 때만 지나갈 수 있어요.");
                    break;
                }

                case MoveOutcome.BlockedByWall:
                    bufferedMoves.Clear();
                    board.Shake(Neighbor(from, direction));
                    break;

                case MoveOutcome.BlockedByEdge:
                    bufferedMoves.Clear();
                    board.Shake(-1);
                    break;
            }
        }

        void OnMoveLanded(MoveOutcome outcome)
        {
            board.Refresh(session.State);
            hud.Refresh(session);
            if (outcome == MoveOutcome.Cleared) StartCoroutine(ShowClearPanel());
            else if (Rules.IsOverfilled(Stage, session.State)) hud.ShowMessage(OverfilledMessage);
        }

        IEnumerator ShowClearPanel()
        {
            bufferedMoves.Clear();
            Progress.RecordClear(stageIndex, Stage.Id, session.Stars);
            playtestLog.End(PlaytestLog.ResultClear, session.State.Moves, session.Stars);
            hud.SetStage(Stage, stageIndex, stages.Count, stageIndex > 0, stageIndex < LastOpenIndex);
            yield return new WaitForSecondsRealtime(ClearPanelDelay);
            clearPanel.ShowStageClear(session.Stars, session.Restarts, stageIndex < stages.Count - 1);
        }

        void Restart()
        {
            if (session == null) return;
            board.CompleteAnimation();
            StopAllCoroutines();
            bufferedMoves.Clear();
            // Replaying a cleared stage is a new visit and attempt; restarting an unfinished one is part of it.
            if (session.IsCleared)
            {
                if (!playtestLog.IsOpen) playtestLog.Begin(Stage.Id);
                session.StartOver();
            }
            else if (session.Restart())
            {
                playtestLog.CountRestart();
            }
            clearPanel.Hide();
            showingAllClear = false;
            board.Refresh(session.State);
            board.SnapToken(session.State.Position);
            hud.Refresh(session);
            hud.ShowMessage("");
        }

        void ChangeStage(int delta)
        {
            int target = stageIndex + delta;
            if (stages == null || target < 0 || target > LastOpenIndex) return;
            StartStage(target);
        }

        /// <summary>Next stage; after the last stage, first a results card, then back to stage 1.</summary>
        void OnClearPanelNext()
        {
            if (stageIndex < stages.Count - 1)
            {
                StartStage(stageIndex + 1);
            }
            else if (!showingAllClear)
            {
                showingAllClear = true;
                int total = 0;
                foreach (StageDefinition stage in stages) total += Progress.BestStars(stage.Id);
                clearPanel.ShowAllClear(session.Stars, total, stages.Count * 3);
            }
            else
            {
                StartStage(0);
            }
        }

        void OpenPlaytestPanel()
        {
            if (session == null) return;
            bufferedMoves.Clear();
            playtestPanel.Show(PlaytestLog.Tester);
        }

        /// <summary>Hands the device to the next playtester: logs the open attempt, then clears all progress.</summary>
        void NextTester()
        {
            playtestLog.End(PlaytestLog.ResultReset, session.State.Moves, 0);
            Progress.ResetAll(stages.ConvertAll(stage => stage.Id));
            PlaytestLog.NextTester();
            StartStage(0);
        }

        /// <summary>
        /// Phones can kill a backgrounded app without calling OnApplicationQuit, so the open attempt is written on
        /// pause and a new one continues the same stage on return.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (session == null) return;
            if (paused)
            {
                board.CompleteAnimation(); // a clearing move still sliding must log its clear before the pause row
                playtestLog.End(PlaytestLog.ResultPause, session.State.Moves, 0);
            }
            else if (!session.IsCleared && !playtestLog.IsOpen)
            {
                playtestLog.Begin(Stage.Id, session.State.Moves);
            }
        }

        void OnApplicationQuit()
        {
            if (session != null) playtestLog.End(PlaytestLog.ResultQuit, session.State.Moves, 0);
        }

        void OnBoardTapped(int column, int row)
        {
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

        void BuildUi()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Ui.Stretch(Ui.NewImage("Background", canvasObject.transform, null, Theme.Background).rectTransform);
            RectTransform safeArea = Ui.Stretch(Ui.NewRect("Safe Area", canvasObject.transform));
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            hud = new HudView(safeArea, Restart, () => ChangeStage(-1), () => ChangeStage(1), OpenPlaytestPanel);

            RectTransform boardArea = Ui.Stretch(Ui.NewRect("Board Area", safeArea), 40f, HudView.BottomReserved, 40f, HudView.TopReserved);
            Image boardImage = Ui.NewImage("Board", boardArea, SpriteFactory.RoundedRect, Theme.Board);
            boardImage.raycastTarget = true;
            var fitter = boardImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            board = boardImage.gameObject.AddComponent<BoardView>();
            boardInput = boardImage.gameObject.AddComponent<BoardInput>();
            boardInput.Swiped += RequestMove;
            boardInput.Tapped += OnBoardTapped;

            clearPanel = new ClearPanel(safeArea, Restart, OnClearPanelNext);
            playtestPanel = new PlaytestPanel(safeArea, NextTester, () => PlaytestLog.Summary(stages?.Count ?? 0), "1번 스테이지부터");
        }

        /// <summary>Lets the prototype run from any scene: adds an EventSystem and a camera if missing.</summary>
        static void EnsureSceneBasics()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (Camera.main == null)
            {
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Theme.Background;
            }
        }
    }
}
