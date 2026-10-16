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
    /// Phase 2 entry point (Garden.unity). Put it on an empty GameObject: it builds every screen in code and routes
    /// between them: Title → Garden → Play, with the menu sheet, the reward popup, and the playtest panel on top.
    /// The app always starts at the title. Escape (Android back) closes the top window; otherwise it opens the
    /// menu during play, goes from the garden to the title, and quits the app from the title.
    /// </summary>
    public sealed class GardenApp : MonoBehaviour
    {
        /// <summary>Screens wider than this (width / height, e.g. 4:3 tablets) scale the UI by height alone.</summary>
        const float WideAspect = 0.6f;

        /// <summary>A touch must travel this far (inches) before it becomes a drag, e.g. a garden scroll instead of a tap.</summary>
        const float DragThresholdInches = 0.08f;

        /// <summary>The EventSystem's default drag threshold in pixels; never go below it.</summary>
        const int MinDragThreshold = 10;

        enum Page
        {
            Title,
            Garden,
            Play,
        }

        List<StageDefinition> stages;
        Page page;
        TitleScreen title;
        GardenScreen garden;
        PlayScreen play;
        MenuSheet menu;
        GrantPopup grant;
        PlaytestPanel playtestPanel;
        Text errorLabel;
        CanvasScaler scaler;
        Vector2Int scaledFor;

        bool OverlayOpen => menu.IsVisible || grant.IsVisible || playtestPanel.IsVisible;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // playtesters stop to think; keep the screen on meanwhile
            EnsureSceneBasics();
            stages = StageLibrary.LoadAll();
            BuildUi();
            ApplyScale();
            garden.SetStages(stages);
            if (stages.Count == 0) errorLabel.text = $"스테이지 파일이 없어요 (Resources/{StageLibrary.ResourceFolder})";
            ShowTitle();
        }

        void Update()
        {
            if (Screen.width != scaledFor.x || Screen.height != scaledFor.y) ApplyScale(); // rotation, Editor Game view resize
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        void Back()
        {
            if (playtestPanel.IsVisible) playtestPanel.Hide();
            else if (grant.IsVisible) grant.Hide();
            else if (menu.IsVisible) menu.Hide();
            else if (page == Page.Play)
            {
                if (play.ClearShowing) ShowGarden();
                else if (!play.StageCleared) OpenMenu(); // cleared: the clear view is about to show; don't hide it under the menu
            }
            else if (page == Page.Garden) ShowTitle();
            else Application.Quit(); // the title: Android back leaves the app (does nothing in the Editor)
        }

        void ShowTitle()
        {
            CloseOverlays();
            LeavePlay();
            garden.Hide();
            int next = GardenScreen.ContinueIndex(stages);
            title.Show(next >= 0 ? (Color)Theme.ToColor(stages[next].TargetColor) : Color.white);
            page = Page.Title;
        }

        void ShowGarden()
        {
            if (stages.Count == 0) return;
            CloseOverlays();
            LeavePlay();
            title.Hide();
            garden.Show();
            page = Page.Garden;
        }

        void ShowPlay(int index)
        {
            CloseOverlays();
            title.Hide();
            garden.Hide();
            play.Enter(index);
            page = Page.Play;
        }

        void LeavePlay()
        {
            if (page == Page.Play) play.Leave();
        }

        void CloseOverlays()
        {
            menu.Hide();
            grant.Hide();
            playtestPanel.Hide();
        }

        void OpenMenu()
        {
            play.ClearBufferedMoves();
            menu.Show(page == Page.Play);
        }

        void OfferGrant(System.Action granted)
        {
            play.ClearBufferedMoves();
            grant.Show(granted);
        }

        void OpenPlaytest()
        {
            play.ClearBufferedMoves();
            playtestPanel.Show(PlaytestLog.Tester);
        }

        /// <summary>
        /// Hands the device to the next playtester: logs the open attempt, clears progress, stars, and today's hint
        /// count, then opens the garden.
        /// </summary>
        void NextTester()
        {
            play.CloseForReset();
            page = Page.Garden; // the play screen is already closed; don't log the attempt twice
            Progress.ResetAll(stages.ConvertAll(stage => stage.Id));
            PlaytestLog.NextTester();
            ShowGarden();
        }

        void OnApplicationPause(bool paused)
        {
            if (play != null) play.OnAppPause(paused);
        }

        void OnApplicationQuit()
        {
            if (play != null) play.OnAppQuit();
        }

        void BuildUi()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            Ui.Stretch(Ui.NewImage("Background", canvasObject.transform, null, Theme.Background).rectTransform);
            RectTransform safeArea = Ui.Stretch(Ui.NewRect("Safe Area", canvasObject.transform));
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            // Screens first, windows after them so they draw on top.
            title = new TitleScreen(safeArea, ShowGarden);
            garden = GardenScreen.Create(safeArea, OpenMenu, ShowPlay);
            play = PlayScreen.Create(safeArea, stages, OpenMenu, OpenPlaytest, ShowGarden, OfferGrant, () => OverlayOpen);
            menu = new MenuSheet(safeArea, ShowGarden, ShowTitle);
            grant = new GrantPopup(safeArea);
            playtestPanel = new PlaytestPanel(safeArea, NextTester, () => PlaytestLog.Summary(stages.Count), "정원에서");

            errorLabel = Widgets.Label(safeArea, "Error", "", 36, Theme.Accent);
            Ui.BottomStrip(errorLabel.rectTransform, 40f, 100f, 36f);
        }

        /// <summary>
        /// Phones keep the canvas between width and height matching (0.5). Wider screens such as 4:3 tablets match
        /// height alone: otherwise the fixed-height HUD takes so much of the canvas that the board shrinks to well
        /// under half the width. Re-enabling the scaler applies the new setting this frame, so layouts read in the
        /// same frame (the garden's scroll position) see the final canvas size.
        /// </summary>
        void ApplyScale()
        {
            scaledFor = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            scaler.matchWidthOrHeight = Screen.width / (float)Screen.height > WideAspect ? 1f : 0.5f;
            scaler.enabled = false;
            scaler.enabled = true;
        }

        /// <summary>
        /// Lets the app run from any scene: adds an EventSystem and a camera if missing. Also scales the drag
        /// threshold with the screen density: the default 10 px is a tiny wobble on a high-dpi phone, so taps on
        /// garden tiles turned into scroll drags.
        /// </summary>
        static void EnsureSceneBasics()
        {
            EventSystem events = FindAnyObjectByType<EventSystem>();
            if (events == null)
                events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f; // 0 when the platform can't tell; 160 = Android's baseline density
            events.pixelDragThreshold = Mathf.Max(MinDragThreshold, Mathf.RoundToInt(dpi * DragThresholdInches));

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
