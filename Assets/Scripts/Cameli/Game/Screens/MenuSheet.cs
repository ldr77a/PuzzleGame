using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Bottom sheet over a dimmed screen (Phase 2 spec §4): 계속하기, 정원으로 (only over the play screen), sound and
    /// vibration switches bound to <see cref="Settings"/>, and 처음 화면으로. Tapping the dim closes it.
    /// </summary>
    sealed class MenuSheet
    {
        const float RowHeight = 130f;

        readonly GameObject root;
        readonly GameObject gardenRow;
        readonly Switch sound;
        readonly Switch haptics;

        public bool IsVisible => root.activeSelf;

        public MenuSheet(RectTransform parent, Action toGarden, Action toTitle)
        {
            RectTransform layer = Widgets.Layer(parent, "Menu Sheet");
            root = layer.gameObject;
            Widgets.Clickable(Widgets.Dim(layer, "Dim"), Hide);

            Image sheet = Widgets.Round(Ui.NewImage("Sheet", layer, SpriteFactory.RoundedRect, Theme.Card), Widgets.CardRadius);
            sheet.raycastTarget = true; // taps on the sheet itself must not reach the dim and close it
            RectTransform sheetRect = sheet.rectTransform;
            sheetRect.anchorMin = Vector2.zero;
            sheetRect.anchorMax = new Vector2(1f, 0f);
            sheetRect.pivot = new Vector2(0.5f, 0f);
            sheetRect.anchoredPosition = Vector2.zero;
            sheetRect.sizeDelta = Vector2.zero;
            var column = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 40, 48);
            column.spacing = 22f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The sheet reaches past the bottom of the safe area (home bar) and hides its own bottom corners.
            Image skirt = Ui.NewImage("Skirt", sheet.transform, null, Theme.Card);
            skirt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            skirt.rectTransform.anchorMin = Vector2.zero;
            skirt.rectTransform.anchorMax = new Vector2(1f, 0f);
            skirt.rectTransform.offsetMin = new Vector2(0f, -Widgets.Bleed);
            skirt.rectTransform.offsetMax = new Vector2(0f, Widgets.CardRadius);

            RectTransform handleRow = Ui.NewRect("Handle", sheet.transform);
            handleRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;
            Image handle = Widgets.Round(Ui.NewImage("Bar", handleRow, SpriteFactory.RoundedRect, Theme.StarOff), 7f);
            Ui.Box(handle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 14f));

            Row(Widgets.Button(sheet.transform, "Continue", "계속하기", 46, true, Hide, out _, SpriteFactory.PlayIcon));
            gardenRow = Row(Plain(Widgets.Button(sheet.transform, "Garden", "정원으로", 46, false, () =>
            {
                Hide();
                toGarden();
            }, out _, SpriteFactory.GridIcon))).gameObject;
            sound = new Switch(sheet.transform, "소리", () => Settings.Sound, on => Settings.Sound = on);
            haptics = new Switch(sheet.transform, "진동", () => Settings.Haptics, on => Settings.Haptics = on);
            Row(Plain(Widgets.Button(sheet.transform, "Title", "처음 화면으로", 46, false, () =>
            {
                Hide();
                toTitle();
            }, out _, SpriteFactory.BackIcon)));

            sheet.gameObject.AddComponent<SlideIn>().Distance = 1100f;
            root.SetActive(false);
        }

        /// <param name="overPlay">Shows 정원으로, which only makes sense over the play screen.</param>
        public void Show(bool overPlay)
        {
            gardenRow.SetActive(overPlay);
            sound.Refresh();
            haptics.Refresh();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);

        static Button Row(Button button)
        {
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            return button;
        }

        /// <summary>Ordinary buttons on the white sheet use the background tint so they stand out.</summary>
        static Button Plain(Button button)
        {
            button.GetComponent<Image>().color = Theme.Background;
            return button;
        }

        /// <summary>A row with a label and an on/off switch; the whole row toggles.</summary>
        sealed class Switch
        {
            readonly Func<bool> read;
            readonly Image track;
            readonly RectTransform knob;
            readonly Text state;

            public Switch(Transform parent, string caption, Func<bool> read, Action<bool> write)
            {
                this.read = read;
                Image row = Widgets.Round(Ui.NewImage(caption, parent, SpriteFactory.RoundedRect, Theme.Background), Widgets.ButtonRadius);
                row.raycastTarget = true;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row;
                button.onClick.AddListener(() =>
                {
                    write(!read());
                    Sfx.Play(SfxKind.Button); // after the change, so turning sound on clicks and turning it off is silent
                    Refresh();
                });

                Text label = Widgets.Label(row.transform, "Label", caption, 46, Theme.ButtonInk, TextAnchor.MiddleLeft);
                Ui.Stretch(label.rectTransform, 44f, 0f, 300f, 0f);

                track = Widgets.Round(Ui.NewImage("Track", row.transform, SpriteFactory.RoundedRect, Theme.StarOff), 42f);
                Ui.Box(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(150f, 84f));
                knob = Ui.NewImage("Knob", track.transform, SpriteFactory.Circle, Color.white).rectTransform;
                Ui.Box(knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(68f, 68f));

                state = Widgets.Label(row.transform, "State", "", 38, Theme.Muted, TextAnchor.MiddleRight);
                Ui.Stretch(state.rectTransform, 300f, 0f, 210f, 0f);
            }

            public void Refresh()
            {
                bool on = read();
                track.color = on ? Theme.Primary : Theme.StarOff;
                knob.anchoredPosition = new Vector2(on ? 33f : -33f, 0f);
                state.text = on ? "켜짐" : "꺼짐";
            }
        }
    }
}
