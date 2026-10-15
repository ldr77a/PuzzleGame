using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Code-drawn camellias and buds (design doc 8.1), tinted with a stage's target color. Each one is a
    /// square stack of Images under a returned root; position it like any rect (Ui.Box, Ui.Stretch, a layout
    /// group). Keep the root square, or the petals stretch.
    /// </summary>
    static class Blossoms
    {
        /// <summary>How far the inner petals are lightened toward white.</summary>
        const float InnerLighten = 0.3f;

        /// <summary>Center disk, as the inset on each side of the flower square.</summary>
        const float CenterInset = 0.36f;

        /// <summary>Outer petals in <paramref name="color"/>, inner petals a little lighter, yellow center.</summary>
        public static RectTransform NewFlower(Transform parent, Color color)
        {
            RectTransform root = Ui.NewRect("Flower", parent);
            Ui.Stretch(Ui.NewImage("Outer Petals", root, SpriteFactory.FlowerOuter, color).rectTransform);
            Ui.Stretch(Ui.NewImage("Inner Petals", root, SpriteFactory.FlowerInner, Theme.Lighten(color, InnerLighten)).rectTransform);
            Ui.Inset(Ui.NewImage("Center", root, SpriteFactory.Circle, Theme.Pollen).rectTransform, CenterInset);
            return root;
        }

        /// <summary>Teardrop body in <paramref name="color"/> on a green calyx.</summary>
        public static RectTransform NewBud(Transform parent, Color color)
        {
            RectTransform root = Ui.NewRect("Bud", parent);
            Ui.Stretch(Ui.NewImage("Body", root, SpriteFactory.Bud, color).rectTransform);
            Ui.Stretch(Ui.NewImage("Sepal", root, SpriteFactory.Sepal, Theme.Leaf).rectTransform);
            return root;
        }

        /// <summary>Re-tints a flower made by <see cref="NewFlower"/>.</summary>
        public static void SetFlowerColor(RectTransform flower, Color color)
        {
            flower.GetChild(0).GetComponent<Image>().color = color;
            flower.GetChild(1).GetComponent<Image>().color = Theme.Lighten(color, InnerLighten);
        }

        /// <summary>Re-tints a bud made by <see cref="NewBud"/> (e.g. <see cref="Theme.Locked"/> for a locked stage).</summary>
        public static void SetBudColor(RectTransform bud, Color color) =>
            bud.GetChild(0).GetComponent<Image>().color = color;
    }
}
