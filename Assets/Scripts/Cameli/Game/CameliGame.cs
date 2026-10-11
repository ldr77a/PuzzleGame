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
    /// Editor keys: arrows/WASD move, Z or Backspace undo, R restart.
    /// </summary>
    public sealed class CameliGame : MonoBehaviour
    {
        const int MaxBufferedMoves = 2;
        const float ClearPanelDelay = 0.3f;

        readonly Queue<Direction> bufferedMoves = new Queue<Direction>();
        List<StageDefinition> stages;
        int stageIndex;
        GameSession session;
        BoardView board;
        BoardInput boardInput;
        HudView hud;
        ClearPanel clearPanel;
        bool showingAllClear;

        StageDefinition Stage => session.Stage;
        int LastOpenIndex => Mathf.Min(Progress.Unlocked, stages.Count - 1);

        void Awake()
        {
            Application.targetFrameRate = 60;
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
            if (session == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) RequestMove(Direction.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) RequestMove(Direction.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) RequestMove(Direction.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) RequestMove(Direction.Right);
                if (keyboard.zKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) Undo();
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
            stageIndex = index;
            Progress.LastPlayed = index;
            session = new GameSession(stages[index]);
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
        }

        IEnumerator ShowClearPanel()
        {
            bufferedMoves.Clear();
            Progress.RecordClear(stageIndex, Stage.Id, session.Stars);
            hud.SetStage(Stage, stageIndex, stages.Count, stageIndex > 0, stageIndex < LastOpenIndex);
            yield return new WaitForSecondsRealtime(ClearPanelDelay);
            clearPanel.ShowStageClear(session.Stars, session.State.Moves, Stage.OptimalMoves, stageIndex < stages.Count - 1);
        }

        void Undo()
        {
            if (session == null || clearPanel.IsVisible) return;
            board.CompleteAnimation();
            bufferedMoves.Clear();
            if (session.Undo())
            {
                board.Refresh(session.State);
                board.SnapToken(session.State.Position);
                hud.Refresh(session);
                hud.ShowMessage("");
            }
            else if (!session.IsCleared)
            {
                hud.ShowMessage(session.UndosRemaining == 0 ? "되돌리기를 다 썼어요. 처음부터 다시 해 보세요." : "되돌릴 걸음이 없어요.");
            }
        }

        void Restart()
        {
            if (session == null) return;
            board.CompleteAnimation();
            StopAllCoroutines();
            bufferedMoves.Clear();
            session.Restart();
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

            hud = new HudView(safeArea, Restart, Undo, () => ChangeStage(-1), () => ChangeStage(1));

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
