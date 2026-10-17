using System.Collections.Generic;
using Cameli.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>Cameli's expression: Happy once the stage is cleared, Oops on an overfilled mix or a bump.</summary>
    public enum CameliFace { Normal, Happy, Oops }

    /// <summary>
    /// Cameli, stacked from the layer PNGs in <c>Resources/CameliArt/</c> (drawn by <c>Tools/CameliArt</c>, Phase 2 spec
    /// §19): the body's ink silhouette, the tail (fill and line) over it so the two join without a line, the body fill
    /// over the tail's root, then the face. The fills are white and take the current mix. Used on the board token, the
    /// speech bubble, and the title. Everything that draws Cameli goes through this class, so new art only needs new PNGs.
    /// </summary>
    sealed class CameliFigure
    {
        /// <summary>
        /// How much the figure may squash and stretch: <see cref="BoardView"/>'s hop scales its amounts by this.
        /// 1 for this drawing; 0 for art that must keep its shape (e.g. pixel art).
        /// </summary>
        public const float SquashStretch = 1f;

        /// <summary>The tail's idle sway each way, in degrees; 0 holds it still (e.g. pixel art, which shouldn't rotate).</summary>
        const float TailSwayDegrees = 5f;
        const float TailSwaySeconds = 1.6f;

        /// <summary>
        /// Where the tail leaves the rump, in the figure square (x from left, y from bottom). The tail sways around it,
        /// so the joint stays put under the body fill.
        /// </summary>
        static readonly Vector2 TailJoint = new Vector2(0.324f, 0.489f);

        /// <summary>Where Cameli stands in the figure square: between the feet, at their bottom. <see cref="Pose"/> scales around it. Facing left mirrors x.</summary>
        static readonly Vector2 Feet = new Vector2(0.602f, 0.229f);

        const string ArtFolder = "CameliArt/";
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        readonly RectTransform figure;
        readonly Image bodyFill;
        readonly Image tailFill;
        readonly Image faceNormal, faceHappy, faceOops;

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

            Layer("Body Line", "body_line", figure);
            RectTransform tail = Ui.NewRect("Tail", figure);
            tail.pivot = TailJoint;
            Ui.Stretch(tail);
            tailFill = Layer("Tail Fill", "tail_fill", tail);
            Layer("Tail Line", "tail_line", tail);
            bodyFill = Layer("Body Fill", "body_fill", figure);
            faceNormal = Layer("Face Normal", "face_normal", figure);
            faceHappy = Layer("Face Happy", "face_happy", figure);
            faceOops = Layer("Face Oops", "face_oops", figure);
            SetFace(CameliFace.Normal);

            if (TailSwayDegrees > 0f)
            {
                var sway = tail.gameObject.AddComponent<Sway>();
                sway.Degrees = TailSwayDegrees;
                sway.Speed = 2f * Mathf.PI / TailSwaySeconds;
            }
        }

        /// <summary>Tints the body and the tail. Pass <see cref="ColorOf"/> for a mix (white when empty).</summary>
        public void SetColor(Color color)
        {
            bodyFill.color = color;
            tailFill.color = color;
        }

        /// <summary>The body color for a mix: white while nothing has been eaten.</summary>
        public static Color ColorOf(DropCounts drops) =>
            drops.IsEmpty ? Color.white : (Color)Theme.ToColor(ColorMath.Mix(drops));

        /// <summary>Normal: the open eye. Happy: the eye closed in an upward arc. Oops: a wider eye with a small pupil.</summary>
        public void SetFace(CameliFace face)
        {
            faceNormal.enabled = face == CameliFace.Normal;
            faceHappy.enabled = face == CameliFace.Happy;
            faceOops.enabled = face == CameliFace.Oops;
        }

        /// <summary>Turns toward a left or right move; up and down keep the last facing.</summary>
        public void Face(Direction direction)
        {
            if (direction == Direction.Left) FacingLeft = true;
            else if (direction == Direction.Right) FacingLeft = false;
            else return;
            figure.localScale = new Vector3(FacingLeft ? -1f : 1f, 1f, 1f);
            // The feet sit right of center, so the flip moves them: mirror the pivot too, so Pose still scales
            // around them. Stretch re-places the rect for the new pivot (and drops any lift; Pose sets it each frame).
            Root.pivot = FacingLeft ? new Vector2(1f - Feet.x, Feet.y) : Feet;
            Ui.Stretch(Root);
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

        /// <summary>A layer filling the figure square. The PNGs carry their own ink and white; fills get tinted.</summary>
        static Image Layer(string name, string file, RectTransform parent)
        {
            Image image = Ui.NewImage(name, parent, Art(file), Color.white);
            Ui.Stretch(image.rectTransform);
            return image;
        }

        /// <summary>
        /// A layer PNG from <c>Resources/CameliArt/</c>, loaded once. Imported as a sprite; a plain texture import still
        /// works, as a sprite made here.
        /// </summary>
        static Sprite Art(string file)
        {
            if (sprites.TryGetValue(file, out Sprite sprite)) return sprite;
            sprite = Resources.Load<Sprite>(ArtFolder + file);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(ArtFolder + file);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                else
                    Debug.LogError($"카멜리 그림이 없어요: Resources/{ArtFolder}{file}.png (Tools/CameliArt/render.sh로 만들어요)");
            }
            sprites[file] = sprite;
            return sprite;
        }
    }
}
