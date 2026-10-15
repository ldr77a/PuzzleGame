using System;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// First screen (Phase 2 spec §4): Cameli in one target color, the name, a blinking "tap to start", and a few
    /// camellias around. A tap anywhere goes on to the garden.
    /// </summary>
    sealed class TitleScreen
    {
        readonly GameObject root;
        readonly CameliFigure cameli;

        public GameObject Root => root;

        public TitleScreen(RectTransform parent, Action start)
        {
            RectTransform layer = Widgets.Layer(parent, "Title Screen");
            root = layer.gameObject;

            // Decorative camellias: normalized position, size, palette color, tilt.
            Decor(layer, 0.13f, 0.88f, 210f, 0, 18f, 0.0f);
            Decor(layer, 0.86f, 0.80f, 170f, 6, -12f, 0.7f);
            Decor(layer, 0.70f, 0.95f, 110f, 2, 30f, 1.3f);
            Decor(layer, 0.10f, 0.20f, 160f, 4, -20f, 0.4f);
            Decor(layer, 0.88f, 0.13f, 220f, 1, 10f, 1.0f);
            Decor(layer, 0.30f, 0.06f, 110f, 3, -8f, 1.8f);

            RectTransform cameliBox = Ui.Box(Ui.NewRect("Cameli Box", layer), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(460f, 460f));
            cameli = new CameliFigure(cameliBox);

            Text name = Widgets.Label(layer, "Name", "Cameli Garden", 116, Theme.Ink);
            Ui.Box(name.rectTransform, new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(1000f, 150f));
            // Jua has no "·", so the subtitle uses a hyphen.
            Text subtitle = Widgets.Label(layer, "Subtitle", "카멜리의 정원 - 컬러 퍼즐", 48, Theme.Muted);
            Ui.Box(subtitle.rectTransform, new Vector2(0.5f, 0.375f), Vector2.zero, new Vector2(1000f, 80f));

            Text prompt = Widgets.Label(layer, "Prompt", "화면을 눌러 시작", 46, Theme.Primary);
            Ui.Box(prompt.rectTransform, new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(1000f, 80f));
            prompt.gameObject.AddComponent<Blink>();

            // Last, so it takes every tap; transparent images still receive raycasts.
            Image catcher = Ui.NewImage("Tap Anywhere", layer, null, Color.clear);
            Ui.Stretch(catcher.rectTransform, -Widgets.Bleed, -Widgets.Bleed, -Widgets.Bleed, -Widgets.Bleed);
            Widgets.Clickable(catcher, () =>
            {
                Sfx.Play(SfxKind.Button);
                start();
            });
        }

        /// <param name="cameliColor">Cameli's color: the target of the stage to play next.</param>
        public void Show(Color cameliColor)
        {
            cameli.SetColor(cameliColor);
            root.SetActive(true);
        }

        public void Hide() => root.SetActive(false);

        static void Decor(RectTransform parent, float x, float y, float size, int color, float tilt, float phase)
        {
            RectTransform holder = Ui.Box(Ui.NewRect("Camellia", parent), new Vector2(x, y), Vector2.zero, new Vector2(size, size));
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.localEulerAngles = new Vector3(0f, 0f, tilt);
            RectTransform flower = Ui.Stretch(Blossoms.NewFlower(holder, Theme.ToColor(Palette.Color(color))));
            Sway sway = flower.gameObject.AddComponent<Sway>();
            sway.Degrees = 4f;
            sway.Speed = 1.1f;
            sway.Offset = phase;
        }
    }
}
