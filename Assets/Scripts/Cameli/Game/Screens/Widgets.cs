using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Phase 2 building blocks on top of <see cref="Ui"/>: soft cards, rounded buttons that click, icon buttons,
    /// star rows, and the dim behind windows. Labels use Jua's single weight (no fake bold). Jua lacks "·", arrows,
    /// and similar symbols, so labels stick to Hangul, Latin, digits, and ★; icons come from <see cref="SpriteFactory"/>.
    /// </summary>
    static class Widgets
    {
        /// <summary>Corner radius drawn into <see cref="SpriteFactory.RoundedRect"/>, in sprite pixels.</summary>
        const float SpriteRadius = 18f;

        public const float CardRadius = 44f;
        public const float ButtonRadius = 36f;

        /// <summary>How far a dim reaches past the safe area, so it also covers notches and the home bar.</summary>
        public const float Bleed = 600f;

        static readonly Color ShadowColor = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.09f);

        /// <summary>Sets the rendered corner radius (canvas units) of an Image using the sliced rounded rect.</summary>
        public static Image Round(Image image, float radius)
        {
            image.pixelsPerUnitMultiplier = SpriteRadius / radius;
            return image;
        }

        /// <summary>Soft drop shadow under a card or button.</summary>
        public static void AddShadow(Graphic graphic, float distance = 8f)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = new Vector2(0f, -distance);
        }

        /// <summary>White rounded card with a soft shadow; position it with the Ui helpers.</summary>
        public static Image Card(Transform parent, string name)
        {
            Image card = Round(Ui.NewImage(name, parent, SpriteFactory.RoundedRect, Theme.Card), CardRadius);
            AddShadow(card);
            return card;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter) =>
            Ui.NewLabel(name, parent, text, size, color, alignment);

        /// <summary>
        /// Rounded button: <see cref="Theme.Primary"/> when <paramref name="primary"/>, else <see cref="Theme.Button"/>.
        /// An optional icon sits before (or after) the label, the pair centered. Plays <paramref name="sound"/> on
        /// click unless null (the handler then plays its own).
        /// </summary>
        public static Button Button(Transform parent, string name, string text, int size, bool primary, Action onClick,
            out Text label, Sprite icon = null, bool iconAfter = false, SfxKind? sound = SfxKind.Button)
        {
            Color ink = primary ? Theme.PrimaryInk : Theme.ButtonInk;
            Image background = Round(Ui.NewImage(name, parent, SpriteFactory.RoundedRect, primary ? Theme.Primary : Theme.Button), ButtonRadius);
            background.raycastTarget = true;
            AddShadow(background, 6f);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                if (sound.HasValue) Sfx.Play(sound.Value);
                onClick();
            });

            RectTransform content = Ui.Stretch(Ui.NewRect("Content", background.transform), 20f, 0f, 20f, 0f);
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = size * 0.35f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            label = Label(content, "Label", text, size, ink);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (icon != null)
            {
                Image glyph = Ui.NewImage("Icon", content, icon, ink);
                var layout = glyph.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = layout.preferredHeight = Mathf.Round(size * 1.1f);
                if (!iconAfter) glyph.transform.SetAsFirstSibling();
            }
            return button;
        }

        /// <summary>Square white button showing only an icon (menu, restart).</summary>
        public static Button IconButton(Transform parent, string name, Sprite icon, Action onClick)
        {
            Image background = Round(Ui.NewImage(name, parent, SpriteFactory.RoundedRect, Theme.Button), ButtonRadius);
            background.raycastTarget = true;
            AddShadow(background, 6f);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                Sfx.Play(SfxKind.Button);
                onClick();
            });
            Ui.Inset(Ui.NewImage("Icon", background.transform, icon, Theme.ButtonInk).rectTransform, 0.26f);
            return button;
        }

        /// <summary>A row of stars centered on the parent's center, left to right.</summary>
        public static Image[] Stars(Transform parent, int count, float size, float spacing)
        {
            var stars = new Image[count];
            for (int i = 0; i < count; i++)
            {
                stars[i] = Ui.NewImage($"Star {i + 1}", parent, SpriteFactory.Star, Theme.StarOff);
                float x = (i - (count - 1) / 2f) * (size + spacing);
                Ui.Box(stars[i].rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size)).anchoredPosition = new Vector2(x, 0f);
            }
            return stars;
        }

        public static void SetStars(Image[] stars, int earned)
        {
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < earned ? Theme.StarOn : Theme.StarOff;
        }

        /// <summary>Full-screen dim that swallows taps meant for what is underneath.</summary>
        public static Image Dim(Transform parent, string name)
        {
            Image dim = Ui.NewImage(name, parent, null, Theme.Dim);
            dim.raycastTarget = true;
            Ui.Stretch(dim.rectTransform, -Bleed, -Bleed, -Bleed, -Bleed);
            return dim;
        }

        /// <summary>Makes a Graphic clickable without any visual feedback (tap-anywhere areas).</summary>
        public static Button Clickable(Graphic graphic, Action onClick)
        {
            graphic.raycastTarget = true;
            var button = graphic.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>A rect that fills its parent; screens and overlays start from one.</summary>
        public static RectTransform Layer(Transform parent, string name) => Ui.Stretch(Ui.NewRect(name, parent));
    }
}
