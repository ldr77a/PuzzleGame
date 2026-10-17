using System;
using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// First screen (Phase 2 spec §4): Cameli in one target color, the name as a two-line logo, a blinking "tap to
    /// start", and a few camellias around. A tap anywhere goes on to the garden.
    /// </summary>
    sealed class TitleScreen
    {
        /// <summary>Taps this soon after showing are ignored, so a double tap that brought us here doesn't leave again.</summary>
        const float TapGuardSeconds = 0.25f;

        /// <summary>
        /// The logo's font size. "Garden", the wider line, is about 3.15 font sizes wide (650 with the border), which
        /// leaves margin on the narrowest phone canvas (about 940 wide at 21:9).
        /// </summary>
        const int LogoSize = 200;
        const float LogoWidth = 1000f;

        /// <summary>White border and the shadow under it, as fractions of <see cref="LogoSize"/>; the shadow is ink at this alpha.</summary>
        const float LogoBorderFraction = 0.07f;
        const float LogoShadowFraction = 0.13f;
        const float LogoShadowSoftFraction = 0.02f;
        const float LogoShadowAlpha = 0.18f;

        /// <summary>The camellia over the i's dot, as a fraction of <see cref="LogoSize"/>, and the i's index among the glyphs of "Cameli".</summary>
        const float LogoFlowerFraction = 0.57f;
        const int LogoDotGlyph = 5;

        readonly GameObject root;
        readonly CameliFigure cameli;
        float shownAt;

        public GameObject Root => root;

        public TitleScreen(RectTransform parent, Action start)
        {
            RectTransform layer = Widgets.Layer(parent, "Title Screen");
            root = layer.gameObject;

            // Camellia leaves peeking in from the edges, behind the flowers (Phase 2 spec §15).
            Widgets.Leaf(layer, new Vector2(0f, 0.93f), new Vector2(30f, 0f), 260f, -125f, Theme.LeafDark);
            Widgets.Leaf(layer, new Vector2(1f, 0.70f), new Vector2(-10f, 0f), 210f, 110f, Theme.LeafLight);
            Widgets.Leaf(layer, new Vector2(0f, 0.30f), new Vector2(-10f, 0f), 200f, -70f, Theme.LeafLight);
            Widgets.Leaf(layer, new Vector2(1f, 0.06f), new Vector2(-30f, 0f), 260f, 130f, Theme.LeafDark);

            // Decorative camellias: normalized position, size, palette color, tilt.
            Decor(layer, 0.13f, 0.88f, 210f, 0, 18f, 0.0f);
            Decor(layer, 0.86f, 0.80f, 170f, 6, -12f, 0.7f);
            Decor(layer, 0.70f, 0.95f, 110f, 2, 30f, 1.3f);
            Decor(layer, 0.10f, 0.20f, 160f, 4, -20f, 0.4f);
            Decor(layer, 0.88f, 0.13f, 220f, 1, 10f, 1.0f);
            Decor(layer, 0.30f, 0.06f, 110f, 3, -8f, 1.8f);

            // The column: Cameli, the two-line logo, the subtitle, the prompt. With the drawing's ink about 280 tall
            // and the logo about 410, they keep 25 or more apart down to a 1850-unit safe area.
            RectTransform cameliBox = Ui.Box(Ui.NewRect("Cameli Box", layer), new Vector2(0.5f, 0.655f), Vector2.zero, new Vector2(460f, 460f));
            cameli = new CameliFigure(cameliBox);

            Logo(layer, new Vector2(0.5f, 0.45f));
            // Jua has no "·", so the subtitle uses a hyphen.
            Text subtitle = Widgets.Label(layer, "Subtitle", "카멜리의 정원 - 컬러 퍼즐", 48, Theme.Muted);
            Ui.Box(subtitle.rectTransform, new Vector2(0.5f, 0.30f), Vector2.zero, new Vector2(1000f, 80f));

            Text prompt = Widgets.Label(layer, "Prompt", "화면을 눌러 시작", 46, Theme.Primary);
            Ui.Box(prompt.rectTransform, new Vector2(0.5f, 0.20f), Vector2.zero, new Vector2(1000f, 80f));
            prompt.gameObject.AddComponent<Blink>();

            // Last, so it takes every tap; transparent images still receive raycasts.
            Image catcher = Ui.NewImage("Tap Anywhere", layer, null, Color.clear);
            Ui.Stretch(catcher.rectTransform, -Widgets.Bleed, -Widgets.Bleed, -Widgets.Bleed, -Widgets.Bleed);
            Widgets.Clickable(catcher, () =>
            {
                if (Time.unscaledTime - shownAt < TapGuardSeconds) return;
                Sfx.Play(SfxKind.Button);
                start();
            });
        }

        /// <param name="cameliColor">Cameli's color: the target of the stage to play next.</param>
        public void Show(Color cameliColor)
        {
            cameli.SetColor(cameliColor);
            shownAt = Time.unscaledTime;
            root.SetActive(true);
        }

        public void Hide() => root.SetActive(false);

        /// <summary>
        /// The name as a sticker-like logo: "Cameli" in leaf green over "Garden" in camellia red, one font size apart,
        /// both with a white border and a soft shadow, and a small camellia for the dot of the i. Its letters are
        /// centered on <paramref name="anchor"/>.
        /// </summary>
        static void Logo(RectTransform parent, Vector2 anchor)
        {
            RectTransform logo = Ui.Box(Ui.NewRect("Logo", parent), anchor, Vector2.zero, new Vector2(LogoWidth, 2f * LogoSize));
            Text top = LogoLine(logo, "Cameli", Theme.Primary, 0.5f * LogoSize);
            Text bottom = LogoLine(logo, "Garden", Theme.Accent, -0.5f * LogoSize);

            // Unity places a line's glyphs off the middle of its box (by the font's ascent), so center the letters
            // themselves: measure them the way the labels lay them out, then move both lines together.
            List<Rect> topGlyphs = GlyphRects(top);
            List<Rect> bottomGlyphs = GlyphRects(bottom);
            if (topGlyphs.Count == 0 || bottomGlyphs.Count == 0) return;
            float yMax = float.MinValue, yMin = float.MaxValue;
            foreach (Rect glyph in topGlyphs) yMax = Mathf.Max(yMax, glyph.yMax + top.rectTransform.anchoredPosition.y);
            foreach (Rect glyph in bottomGlyphs) yMin = Mathf.Min(yMin, glyph.yMin + bottom.rectTransform.anchoredPosition.y);
            Vector2 shift = new Vector2(0f, -(yMax + yMin) / 2f);
            top.rectTransform.anchoredPosition += shift;
            bottom.rectTransform.anchoredPosition += shift;

            // The i's dot is a round dot as wide as the glyph, at its top. The last visible glyph of "Cameli" is the i.
            if (topGlyphs.Count < LogoDotGlyph + 1) return;
            Rect i = topGlyphs[LogoDotGlyph];
            float flowerSize = LogoFlowerFraction * LogoSize;
            RectTransform holder = Ui.NewRect("Dot Camellia", top.rectTransform);
            holder.anchorMin = holder.anchorMax = top.rectTransform.pivot; // the label's local origin
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = new Vector2(i.center.x, i.yMax - i.width / 2f);
            holder.sizeDelta = new Vector2(flowerSize, flowerSize);
            RectTransform flower = Ui.Stretch(Blossoms.NewFlower(holder, Theme.Accent));
            Sway sway = flower.gameObject.AddComponent<Sway>();
            sway.Degrees = 4f;
            sway.Speed = 1.1f;
            sway.Offset = 0.9f;
        }

        /// <summary>One line of the logo, its box one font size tall and centered <paramref name="y"/> above the logo's middle.</summary>
        static Text LogoLine(RectTransform logo, string text, Color color, float y)
        {
            Text line = Widgets.Label(logo, "Logo " + text, text, LogoSize, color);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Box(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(LogoWidth, LogoSize));
            StickerOutline sticker = line.gameObject.AddComponent<StickerOutline>();
            sticker.BorderColor = Color.white;
            sticker.BorderWidth = LogoBorderFraction * LogoSize;
            sticker.ShadowColor = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, LogoShadowAlpha);
            sticker.ShadowDistance = LogoShadowFraction * LogoSize;
            sticker.ShadowSoftness = LogoShadowSoftFraction * LogoSize;
            return line;
        }

        /// <summary>
        /// The quads of <paramref name="label"/>'s visible characters in its local space (origin at its pivot), from the
        /// same text generator settings the label draws with. Empty if the font made no glyphs.
        /// </summary>
        static List<Rect> GlyphRects(Text label)
        {
            var generator = new TextGenerator();
            generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
            float unitsPerPixel = 1f / label.pixelsPerUnit;
            IList<UIVertex> verts = generator.verts;
            var rects = new List<Rect>();
            for (int v = 0; v + 3 < verts.Count; v += 4)
            {
                Vector2 min = verts[v].position, max = min;
                for (int corner = 1; corner < 4; corner++)
                {
                    min = Vector2.Min(min, verts[v + corner].position);
                    max = Vector2.Max(max, verts[v + corner].position);
                }
                rects.Add(Rect.MinMaxRect(min.x * unitsPerPixel, min.y * unitsPerPixel, max.x * unitsPerPixel, max.y * unitsPerPixel));
            }
            return rects;
        }

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
