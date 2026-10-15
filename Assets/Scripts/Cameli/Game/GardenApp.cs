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
    /// menu during play, goes from the garden to the title, and does nothing on the title.
    /// </summary>
    public sealed class GardenApp : MonoBehaviour
    {
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

        bool OverlayOpen => menu.IsVisible || grant.IsVisible || playtestPanel.IsVisible;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // playtesters stop to think; keep the screen on meanwhile
            EnsureSceneBasics();
            stages = StageLibrary.LoadAll();
            BuildUi();
            garden.SetStages(stages);
            if (stages.Count == 0) errorLabel.text = $"스테이지 파일이 없어요 (Resources/{StageLibrary.ResourceFolder})";
            ShowTitle();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        void Back()
        {
            if (playtestPanel.IsVisible) playtestPanel.Hide();
            else if (grant.IsVisible) grant.Hide();
            else if (menu.IsVisible) menu.Hide();
            else if (page == Page.Play && play.ClearShowing) ShowGarden();
            else if (page == Page.Play) OpenMenu();
            else if (page == Page.Garden) ShowTitle();
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

        void OfferGrant(GrantKind kind, System.Action granted)
        {
            play.ClearBufferedMoves();
            grant.Show(kind, granted);
        }

        void OpenPlaytest()
        {
            play.ClearBufferedMoves();
            playtestPanel.Show(PlaytestLog.Tester);
        }

        /// <summary>
        /// Hands the device to the next playtester: logs the open attempt, clears progress, stars, hints, and
        /// revealed recipes, then opens the garden.
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
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Ui.Stretch(Ui.NewImage("Background", canvasObject.transform, null, Theme.Background).rectTransform);
            RectTransform safeArea = Ui.Stretch(Ui.NewRect("Safe Area", canvasObject.transform));
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            // Screens first, windows after them so they draw on top.
            title = new TitleScreen(safeArea, ShowGarden);
            garden = GardenScreen.Create(safeArea, OpenMenu, ShowPlay);
            play = PlayScreen.Create(safeArea, stages, OpenMenu, OpenPlaytest, ShowGarden, OfferGrant, () => OverlayOpen);
            menu = new MenuSheet(safeArea, ShowGarden, ShowTitle);
            grant = new GrantPopup(safeArea);
            playtestPanel = new PlaytestPanel(safeArea, NextTester, () => PlaytestLog.Summary(stages.Count));

            errorLabel = Widgets.Label(safeArea, "Error", "", 36, Theme.Accent);
            Ui.BottomStrip(errorLabel.rectTransform, 40f, 100f, 36f);
        }

        /// <summary>Lets the app run from any scene: adds an EventSystem and a camera if missing.</summary>
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
