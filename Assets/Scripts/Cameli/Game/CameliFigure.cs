using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// The temporary code-drawn Cameli: body tinted with the current mix, ink outline, back crest, eye, and a
    /// curled tail, assembled from Images. Used on the board token, the target card, the speech bubble, and the
    /// title. Everything that draws Cameli goes through this class, so real art can replace it in one place.
    /// </summary>
    sealed class CameliFigure
    {
        /// <summary>Outline width as a fraction of the figure's size.</summary>
        const float Outline = 0.03f;

        readonly RectTransform figure;
        readonly Image body;
        readonly Image crest;
        readonly Image tail;

        /// <summary>Fills the parent; keep the parent square. The figure starts white and facing right.</summary>
        public RectTransform Root { get; }

        public bool FacingLeft { get; private set; }

        public CameliFigure(Transform parent)
        {
            Root = Ui.Stretch(Ui.NewRect("Cameli", parent));
            // A child carries the facing flip, so callers can scale or animate Root freely.
            figure = Ui.Stretch(Ui.NewRect("Figure", Root));

            // Outlines first, then fills, so the ink traces the outside of the whole silhouette. Facing right.
            Part("Tail Outline", SpriteFactory.TailOutline, Theme.Ink, 0f, 0.12f, 0.36f, 0.48f);
            Part("Crest Outline", SpriteFactory.CrestOutline, Theme.Ink, 0.26f, 0.46f, 0.62f, 0.82f);
            Part("Body Outline", SpriteFactory.Circle, Theme.Ink, 0.19f - Outline, 0.17f - Outline, 0.85f + Outline, 0.67f + Outline);
            tail = Part("Tail", SpriteFactory.Tail, Color.white, 0f, 0.12f, 0.36f, 0.48f);
            crest = Part("Crest", SpriteFactory.Crest, Color.white, 0.26f, 0.46f, 0.62f, 0.82f);
            body = Part("Body", SpriteFactory.Circle, Color.white, 0.19f, 0.17f, 0.85f, 0.67f);
            Part("Eye Rim", SpriteFactory.Circle, Theme.Ink, 0.61f, 0.41f, 0.79f, 0.59f);
            Part("Eye", SpriteFactory.Circle, Color.white, 0.63f, 0.43f, 0.77f, 0.57f);
            Part("Pupil", SpriteFactory.Circle, Theme.Ink, 0.705f, 0.465f, 0.775f, 0.535f);
        }

        /// <summary>Tints the body, crest, and tail. Pass <see cref="ColorOf"/> for a mix (white when empty).</summary>
        public void SetColor(Color color)
        {
            body.color = color;
            crest.color = color;
            tail.color = color;
        }

        /// <summary>The body color for a mix: white while nothing has been eaten.</summary>
        public static Color ColorOf(DropCounts drops) =>
            drops.IsEmpty ? Color.white : (Color)Theme.ToColor(ColorMath.Mix(drops));

        /// <summary>Turns toward a left or right move; up and down keep the last facing.</summary>
        public void Face(Direction direction)
        {
            if (direction == Direction.Left) FacingLeft = true;
            else if (direction == Direction.Right) FacingLeft = false;
            else return;
            figure.localScale = new Vector3(FacingLeft ? -1f : 1f, 1f, 1f);
        }

        /// <summary>An Image spanning a fraction of the figure square (x from left, y from bottom).</summary>
        Image Part(string name, Sprite sprite, Color color, float xMin, float yMin, float xMax, float yMax)
        {
            Image image = Ui.NewImage(name, figure, sprite, color);
            image.rectTransform.anchorMin = new Vector2(xMin, yMin);
            image.rectTransform.anchorMax = new Vector2(xMax, yMax);
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }
    }
}
