using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>Cameli's expression: Happy once the stage is cleared, Oops on an overfilled mix or a bump.</summary>
    public enum CameliFace { Normal, Happy, Oops }

    /// <summary>
    /// The temporary code-drawn Cameli: body tinted with the current mix, ink outline, back crest, eye, and a
    /// curled tail, assembled from Images. Used on the board token, the target card, the speech bubble, and the
    /// title. Everything that draws Cameli goes through this class, so real art can replace it in one place.
    /// </summary>
    sealed class CameliFigure
    {
        /// <summary>
        /// How much the figure may squash and stretch: <see cref="BoardView"/>'s hop scales its amounts by this.
        /// 1 for this drawn figure; 0 for art that must keep its shape (e.g. pixel art).
        /// </summary>
        public const float SquashStretch = 1f;

        /// <summary>The tail's idle sway each way, in degrees; 0 holds it still (e.g. pixel art, which shouldn't rotate).</summary>
        const float TailSwayDegrees = 6f;
        const float TailSwaySeconds = 1.6f;

        /// <summary>Outline width as a fraction of the figure's size.</summary>
        const float Outline = 0.03f;

        /// <summary>
        /// The crest's rect: a 0.36 square centered at (0.44, 0.64), grown by <see cref="SpriteFactory.CrestPadding"/>
        /// because the sprite leaves room around the shape for its outline.
        /// </summary>
        const float CrestX = 0.44f, CrestY = 0.64f, CrestHalf = 0.18f * SpriteFactory.CrestPadding;

        /// <summary>The tail's rect, shared by its outline.</summary>
        const float TailXMin = 0f, TailYMin = 0.12f, TailSize = 0.36f;

        /// <summary>
        /// Where the tail's stalk comes out of the body (the stalk's center line meets the body's edge), in the
        /// figure square. The tail sways around this point, so the joint stays put and the rest hides in the body.
        /// </summary>
        const float TailJointX = 0.2f, TailJointY = 0.37f;

        /// <summary>The eye: white circle in an ink rim, pupil toward the front. Oops widens the white and shrinks the pupil.</summary>
        const float EyeX = 0.7f, EyeY = 0.5f, EyeRadius = 0.07f, EyeRimWidth = 0.02f;
        const float PupilX = 0.74f, PupilRadius = 0.035f;
        const float OopsEyeGrow = 1.15f, OopsPupilShrink = 0.5f;

        /// <summary>Where Cameli stands in the figure square: the bottom of the body's outline, centered. <see cref="Pose"/> scales around it.</summary>
        static readonly Vector2 Feet = new Vector2(0.5f, 0.17f - Outline);

        readonly RectTransform figure;
        readonly Image body;
        readonly Image crest;
        readonly Image tail;
        readonly Image eyeRim, eyeWhite, pupil, happyEye;

        /// <summary>
        /// Fills the parent; keep the parent square. Its pivot is at Cameli's feet. The figure starts white, facing
        /// right, with the normal face.
        /// </summary>
        public RectTransform Root { get; }

        public bool FacingLeft { get; private set; }

        public CameliFigure(Transform parent)
        {
            Root = Ui.NewRect("Cameli", parent);
            Root.pivot = Feet; // before Stretch, which places the rect for the current pivot
            Ui.Stretch(Root);
            // A child carries the facing flip, so callers can scale or animate Root freely.
            figure = Ui.Stretch(Ui.NewRect("Figure", Root));

            // Outlines first, then fills, so the ink traces the outside of the whole silhouette. Facing right.
            Image tailOutline = Part("Tail Outline", SpriteFactory.TailOutline, Theme.Ink, TailXMin, TailYMin, TailXMin + TailSize, TailYMin + TailSize);
            Part("Crest Outline", SpriteFactory.CrestOutline, Theme.Ink, CrestX - CrestHalf, CrestY - CrestHalf, CrestX + CrestHalf, CrestY + CrestHalf);
            Part("Body Outline", SpriteFactory.Circle, Theme.Ink, 0.19f - Outline, 0.17f - Outline, 0.85f + Outline, 0.67f + Outline);
            tail = Part("Tail", SpriteFactory.Tail, Color.white, TailXMin, TailYMin, TailXMin + TailSize, TailYMin + TailSize);
            crest = Part("Crest", SpriteFactory.Crest, Color.white, CrestX - CrestHalf, CrestY - CrestHalf, CrestX + CrestHalf, CrestY + CrestHalf);
            body = Part("Body", SpriteFactory.Circle, Color.white, 0.19f, 0.17f, 0.85f, 0.67f);
            eyeRim = Part("Eye Rim", SpriteFactory.Circle, Theme.Ink, 0f, 0f, 0f, 0f);
            eyeWhite = Part("Eye", SpriteFactory.Circle, Color.white, 0f, 0f, 0f, 0f);
            pupil = Part("Pupil", SpriteFactory.Circle, Theme.Ink, 0f, 0f, 0f, 0f);
            happyEye = Part("Happy Eye", SpriteFactory.HappyEye, Theme.Ink, 0f, 0f, 0f, 0f);
            Circle(happyEye, EyeX, EyeY, EyeRadius + EyeRimWidth);
            SetFace(CameliFace.Normal);

            // The fill and its outline sway together, as two parts that sit apart in the drawing order.
            SwayTail(tailOutline);
            SwayTail(tail);
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

        /// <summary>Normal: the open eye. Happy: the eye closed in an upward arc. Oops: a wider eye with a small pupil.</summary>
        public void SetFace(CameliFace face)
        {
            bool open = face != CameliFace.Happy;
            eyeRim.enabled = open;
            eyeWhite.enabled = open;
            pupil.enabled = open;
            happyEye.enabled = !open;

            bool oops = face == CameliFace.Oops;
            float white = oops ? EyeRadius * OopsEyeGrow : EyeRadius;
            Circle(eyeRim, EyeX, EyeY, white + EyeRimWidth);
            Circle(eyeWhite, EyeX, EyeY, white);
            Circle(pupil, PupilX, EyeY, oops ? PupilRadius * OopsPupilShrink : PupilRadius);
        }

        /// <summary>Turns toward a left or right move; up and down keep the last facing.</summary>
        public void Face(Direction direction)
        {
            if (direction == Direction.Left) FacingLeft = true;
            else if (direction == Direction.Right) FacingLeft = false;
            else return;
            figure.localScale = new Vector3(FacingLeft ? -1f : 1f, 1f, 1f);
        }

        /// <summary>
        /// Lifts the figure by <paramref name="lift"/> canvas units and scales it around its feet (the board's hop).
        /// <see cref="Rest"/> puts it back exactly.
        /// </summary>
        public void Pose(float lift, float scaleX, float scaleY)
        {
            Root.anchoredPosition = new Vector2(0f, lift);
            Root.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        public void Rest() => Pose(0f, 1f, 1f);

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

        /// <summary>Moves a part to a square centered at (x, y) with half side <paramref name="radius"/>.</summary>
        static void Circle(Image part, float x, float y, float radius)
        {
            part.rectTransform.anchorMin = new Vector2(x - radius, y - radius);
            part.rectTransform.anchorMax = new Vector2(x + radius, y + radius);
        }

        /// <summary>Rocks a tail part gently around the joint with the body (unscaled time, see <see cref="Sway"/>).</summary>
        static void SwayTail(Image part)
        {
            if (TailSwayDegrees > 0f)
            {
                RectTransform rt = part.rectTransform;
                // Pivot in the part's own rect; reset the offsets so the rect stays where it was.
                rt.pivot = new Vector2((TailJointX - TailXMin) / TailSize, (TailJointY - TailYMin) / TailSize);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var sway = rt.gameObject.AddComponent<Sway>();
                sway.Degrees = TailSwayDegrees;
                sway.Speed = 2f * Mathf.PI / TailSwaySeconds;
            }
        }
    }
}
