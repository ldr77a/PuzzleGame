using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>Helpers for building the prototype UI in code, so it needs no prefabs or scene wiring.</summary>
    static class Ui
    {
        static Font font;

        /// <summary>Resources path of the game font, 주아 (Jua, OFL; license in the same folder).</summary>
        const string FontResource = "Fonts/Jua-Regular";

        /// <summary>
        /// The game font, Jua. Falls back to Unity's built-in dynamic font if the file is missing (missing glyphs
        /// such as Korean then fall back to system fonts). Jua has a single weight: avoid FontStyle.Bold on new
        /// screens, it only fakes bold.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>(FontResource);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors to the parent's edges with insets in canvas units.</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Full-width strip hanging from the parent's top edge.</summary>
        public static RectTransform TopStrip(RectTransform rt, float top, float height, float sideInset = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(sideInset, -top - height);
            rt.offsetMax = new Vector2(-sideInset, -top);
            return rt;
        }

        /// <summary>Full-width strip standing on the parent's bottom edge.</summary>
        public static RectTransform BottomStrip(RectTransform rt, float bottom, float height, float sideInset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(sideInset, bottom);
            rt.offsetMax = new Vector2(-sideInset, bottom + height);
            return rt;
        }

        /// <summary>Fixed-size box pinned to a normalized point of the parent (also used as its pivot).</summary>
        public static RectTransform Box(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Fills the parent minus a fraction of its size on every side.</summary>
        public static RectTransform Inset(RectTransform rt, float fraction)
        {
            rt.anchorMin = new Vector2(fraction, fraction);
            rt.anchorMax = new Vector2(1 - fraction, 1 - fraction);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        public static Text NewLabel(string name, Transform parent, string text, int size, Color color,
            TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            var label = NewRect(name, parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button NewButton(string name, Transform parent, string text, int size, Color background,
            Color foreground, Action onClick, out Text label)
        {
            Image image = NewImage(name, parent, SpriteFactory.RoundedRect, background);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            label = NewLabel("Label", image.transform, text, size, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform);
            return button;
        }
    }
}
