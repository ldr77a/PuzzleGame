using System;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Flat white shapes generated at runtime and tinted with Image.color, so the prototype needs no art
    /// assets. Game colors stay flat and exact, as the design requires. Each sprite is drawn once, on first
    /// use, and cached.
    /// </summary>
    static class SpriteFactory
    {
        const float StarInnerRatio = 0.46f;

        static Sprite roundedRect;
        static Sprite circle;
        static Sprite ring;
        static Sprite star;
        static Sprite roundedFrame, dashedRing;
        static Sprite flowerOuter, flowerInner, bud, sepal;
        static Sprite crest, crestOutline, tail, tailOutline;
        static Sprite menuIcon, restartIcon, bulbIcon, gridIcon, playIcon, lockIcon, backIcon;
        static Sprite fencePlanks, fenceRails, fenceOutline, leaf, leafVein;

        /// <summary>Rounded square, 9-sliced so it can stretch to any size.</summary>
        public static Sprite RoundedRect => roundedRect != null ? roundedRect : roundedRect = Make("RoundedRect", 64, 22, RoundedRectAlpha);

        public static Sprite Circle => circle != null ? circle : circle = Make("Circle", 128, 0, CircleAlpha);
        public static Sprite Ring => ring != null ? ring : ring = Make("Ring", 128, 0, RingAlpha);
        public static Sprite Star => star != null ? star : star = Make("Star", 128, 0, StarAlpha);

        /// <summary>Outline of <see cref="RoundedRect"/>, 9-sliced the same way: the glow around a highlighted cell.</summary>
        public static Sprite RoundedFrame => roundedFrame != null ? roundedFrame : roundedFrame = Make("RoundedFrame", 64, 22, RoundedFrameAlpha);

        /// <summary>Dashed circle: an empty drop slot on the target card.</summary>
        public static Sprite DashedRing => dashedRing != null ? dashedRing : dashedRing = Make("DashedRing", 128, 0, DashedRingAlpha);

        // Camellia (design doc 8.1). Layers share one square, so stack them in the same rect: see Blossoms.

        /// <summary>Five broad outer petals; tinted with the flower's color.</summary>
        public static Sprite FlowerOuter => flowerOuter != null ? flowerOuter : flowerOuter = MakeShape("Flower Outer", 256, FlowerOuterShape);

        /// <summary>Five smaller inner petals between the outer ones; tinted a little lighter.</summary>
        public static Sprite FlowerInner => flowerInner != null ? flowerInner : flowerInner = MakeShape("Flower Inner", 256, FlowerInnerShape);

        /// <summary>Teardrop bud body, point up, sitting in the upper part of the square.</summary>
        public static Sprite Bud => bud != null ? bud : bud = MakeShape("Bud", 256, BudShape);

        /// <summary>Green calyx and stem that cup the bottom of <see cref="Bud"/>.</summary>
        public static Sprite Sepal => sepal != null ? sepal : sepal = MakeShape("Sepal", 256, SepalShape);

        // Cameli parts (see CameliFigure). The outline versions are the same shape grown by the outline width.

        /// <summary>
        /// The crest is drawn this much smaller inside its square so its outline (out to |x| ≈ 1.07 at full size)
        /// fits; <see cref="CameliFigure"/> grows the crest's rect by the same factor, so it shows at the same size.
        /// </summary>
        public const float CrestPadding = 1.12f;

        public static Sprite Crest => crest != null ? crest : crest = MakeShape("Crest", 128, PaddedCrestShape);
        public static Sprite CrestOutline => crestOutline != null ? crestOutline : crestOutline = MakeShape("Crest Outline", 128, p => PaddedCrestShape(p) - CameliOutline / CrestPadding);
        public static Sprite Tail => tail != null ? tail : tail = MakeShape("Tail", 128, TailShape);
        public static Sprite TailOutline => tailOutline != null ? tailOutline : tailOutline = MakeShape("Tail Outline", 128, p => TailShape(p) - CameliOutline);

        // Wooden fence for wall cells (Phase 2 spec §15). Layers share one square: outline, rails, then planks.
        // BoardView bridges the gap between neighboring fence cells with the geometry below.

        /// <summary>Fence rails in normalized units (-1..1, y up): two bars across the whole cell.</summary>
        public const float FenceUpperRailBottom = 0.167f, FenceUpperRailTop = 0.417f;
        public const float FenceLowerRailBottom = -0.542f, FenceLowerRailTop = -0.292f;

        /// <summary>The middle plank's half width, the outer planks' outer edge, and the planks' bottom and shoulder (where the point starts).</summary>
        public const float FenceMiddlePlankHalfWidth = 0.208f;
        public const float FenceOuterPlankEdge = 0.833f;
        public const float FencePlankBottom = -0.917f, FencePlankShoulder = 0.583f;

        public const float FenceOutlineWidth = 0.07f;

        /// <summary>Three upright planks with pointed tops.</summary>
        public static Sprite FencePlanks => fencePlanks != null ? fencePlanks : fencePlanks = MakeShape("Fence Planks", 256, FencePlanksShape);

        /// <summary>Two rails running edge to edge, behind the planks.</summary>
        public static Sprite FenceRails => fenceRails != null ? fenceRails : fenceRails = MakeShape("Fence Rails", 256, FenceRailsShape);

        /// <summary>Planks and rails grown by the outline width; drawn first, in a darker brown.</summary>
        public static Sprite FenceOutline => fenceOutline != null ? fenceOutline : fenceOutline =
            MakeShape("Fence Outline", 256, p => Mathf.Min(FencePlanksShape(p), FenceRailsShape(p)) - FenceOutlineWidth);

        /// <summary>Camellia leaf: a pointed oval, point up, about 35% of the square wide and 80% tall.</summary>
        public static Sprite Leaf => leaf != null ? leaf : leaf = MakeShape("Leaf", 128, LeafShape);

        /// <summary>The leaf's midrib, drawn over <see cref="Leaf"/> in a lighter tint.</summary>
        public static Sprite LeafVein => leafVein != null ? leafVein : leafVein =
            MakeShape("Leaf Vein", 128, p => Segment(p, new Vector2(0f, -0.66f), new Vector2(0f, 0.6f), 0.025f));

        // Icons, tinted by Image.color.

        /// <summary>Three lines.</summary>
        public static Sprite MenuIcon => menuIcon != null ? menuIcon : menuIcon = MakeShape("Menu Icon", 128, MenuShape);

        /// <summary>Clockwise circular arrow.</summary>
        public static Sprite RestartIcon => restartIcon != null ? restartIcon : restartIcon = MakeShape("Restart Icon", 128, RestartShape);

        public static Sprite BulbIcon => bulbIcon != null ? bulbIcon : bulbIcon = MakeShape("Bulb Icon", 128, BulbShape);

        /// <summary>2×2 squares: the garden.</summary>
        public static Sprite GridIcon => gridIcon != null ? gridIcon : gridIcon = MakeShape("Grid Icon", 128, GridShape);

        /// <summary>Triangle pointing right.</summary>
        public static Sprite PlayIcon => playIcon != null ? playIcon : playIcon = MakeShape("Play Icon", 128, PlayShape);

        public static Sprite LockIcon => lockIcon != null ? lockIcon : lockIcon = MakeShape("Lock Icon", 128, LockShape);

        /// <summary>Chevron pointing left.</summary>
        public static Sprite BackIcon => backIcon != null ? backIcon : backIcon = MakeShape("Back Icon", 128, BackShape);

        static Sprite Make(string name, int size, int border, Func<float, float, int, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x + 0.5f, y + 0.5f, size) * 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            return sprite;
        }

        /// <summary>
        /// Draws a shape given as a signed distance (negative inside) over normalized coordinates: -1..1 on both
        /// axes, y up. One sample per pixel is enough because the distance gives the antialiasing directly.
        /// </summary>
        static Sprite MakeShape(string name, int size, Func<Vector2, float> distance)
        {
            float half = size / 2f;
            return Make(name, size, 0, (px, py, s) => Mathf.Clamp01(0.5f - distance(new Vector2(px / half - 1f, py / half - 1f)) * half));
        }

        const float IconStroke = 0.1f;
        const float CameliOutline = 0.17f;

        static float FlowerOuterShape(Vector2 p) => Petals(p, 90f, 0.52f, 0.43f, 0.45f);
        static float FlowerInnerShape(Vector2 p) => Petals(p, 126f, 0.3f, 0.28f, 0.33f);

        /// <summary>Five round petals around a disk that fills the gaps between them.</summary>
        static float Petals(Vector2 p, float firstAngle, float distance, float petalRadius, float coreRadius)
        {
            float d = Disk(p, Vector2.zero, coreRadius);
            for (int i = 0; i < 5; i++)
                d = Mathf.Min(d, Disk(p, Polar(firstAngle + i * 72f, distance), petalRadius));
            return d;
        }

        static Vector2[] budOutline;

        static float BudShape(Vector2 p)
        {
            if (budOutline == null)
            {
                // Teardrop curve: the point (t = 0) on top, the round body below.
                budOutline = new Vector2[72];
                for (int i = 0; i < budOutline.Length; i++)
                {
                    float t = i * 2f * Mathf.PI / budOutline.Length;
                    budOutline[i] = new Vector2(0.9f * Mathf.Sin(t) * Mathf.Sin(t / 2f), 0.085f + 0.865f * Mathf.Cos(t));
                }
            }
            return Polygon(p, budOutline);
        }

        static float SepalShape(Vector2 p)
        {
            var root = new Vector2(0f, -0.82f);
            float d = Disk(p, new Vector2(0f, -0.76f), 0.17f);
            d = Mathf.Min(d, Tapered(p, root, new Vector2(-0.5f, -0.32f), 0.15f, 0.02f));
            d = Mathf.Min(d, Tapered(p, root, new Vector2(0.5f, -0.32f), 0.15f, 0.02f));
            d = Mathf.Min(d, Tapered(p, root, new Vector2(0f, -0.4f), 0.16f, 0.03f));
            return Mathf.Min(d, Segment(p, root, new Vector2(0f, -0.95f), 0.05f));
        }

        /// <summary><see cref="CrestShape"/> shrunk by <see cref="CrestPadding"/>; the distance stays in sprite units.</summary>
        static float PaddedCrestShape(Vector2 p) => CrestShape(p * CrestPadding) / CrestPadding;

        /// <summary>Three bumps along the top; the base is hidden behind the body.</summary>
        static float CrestShape(Vector2 p)
        {
            float d = Box(p, new Vector2(0f, -0.3f), new Vector2(0.6f, 0.55f), 0.1f);
            for (int i = -1; i <= 1; i++) d = Mathf.Min(d, Disk(p, new Vector2(i * 0.6f, 0.25f), 0.3f));
            return d;
        }

        /// <summary>A stalk from the right edge (where it joins the body) that curls inward on the left.</summary>
        static float TailShape(Vector2 p)
        {
            const float w = 0.14f;
            float d = Segment(p, new Vector2(0.98f, 0.3f), new Vector2(-0.25f, 0.45f), w);
            d = Mathf.Min(d, Arc(p, new Vector2(-0.25f, 0f), 0.45f, 90f, 270f, w));
            d = Mathf.Min(d, Arc(p, new Vector2(-0.25f, -0.1f), 0.35f, 270f, 450f, w));
            return Mathf.Min(d, Arc(p, new Vector2(-0.25f, 0.05f), 0.2f, 90f, 200f, w));
        }

        static float MenuShape(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = -1; i <= 1; i++)
                d = Mathf.Min(d, Segment(p, new Vector2(-0.62f, i * 0.5f), new Vector2(0.62f, i * 0.5f), IconStroke));
            return d;
        }

        static float RestartShape(Vector2 p)
        {
            const float radius = 0.58f, end = 80f;
            float d = Arc(p, Vector2.zero, radius, end, 380f, IconStroke);
            // Arrowhead at the open end, pointing clockwise into the gap.
            Vector2 at = Polar(end, radius);
            Vector2 forward = new Vector2(Mathf.Sin(end * Mathf.Deg2Rad), -Mathf.Cos(end * Mathf.Deg2Rad));
            Vector2 outward = at.normalized;
            return Mathf.Min(d, Polygon(p, new[] { at + outward * 0.27f, at + forward * 0.3f, at - outward * 0.27f }));
        }

        static float FencePlanksShape(Vector2 p)
        {
            float d = float.MaxValue;
            foreach ((float left, float right) in new[] { (-FenceOuterPlankEdge, -0.333f), (-FenceMiddlePlankHalfWidth, FenceMiddlePlankHalfWidth), (0.333f, FenceOuterPlankEdge) })
            {
                float middle = (left + right) / 2f;
                d = Mathf.Min(d, Polygon(p, new[]
                {
                    new Vector2(left, FencePlankBottom), new Vector2(right, FencePlankBottom), new Vector2(right, FencePlankShoulder),
                    new Vector2(middle, 0.833f), new Vector2(left, FencePlankShoulder),
                }));
            }
            return d;
        }

        static float FenceRailsShape(Vector2 p)
        {
            float upper = Box(p, new Vector2(0f, (FenceUpperRailBottom + FenceUpperRailTop) / 2f), new Vector2(1.2f, (FenceUpperRailTop - FenceUpperRailBottom) / 2f), 0.02f);
            float lower = Box(p, new Vector2(0f, (FenceLowerRailBottom + FenceLowerRailTop) / 2f), new Vector2(1.2f, (FenceLowerRailTop - FenceLowerRailBottom) / 2f), 0.02f);
            return Mathf.Min(upper, lower);
        }

        /// <summary>Where two circles overlap: a lens with points at the top and bottom.</summary>
        static float LeafShape(Vector2 p) => Mathf.Max(Disk(p, new Vector2(-0.75f, 0f), 1.1f), Disk(p, new Vector2(0.75f, 0f), 1.1f));

        static float BulbShape(Vector2 p)
        {
            float d = Disk(p, new Vector2(0f, 0.22f), 0.5f);
            d = Mathf.Min(d, Box(p, new Vector2(0f, -0.37f), new Vector2(0.24f, 0.11f), 0.03f));
            return Mathf.Min(d, Box(p, new Vector2(0f, -0.62f), new Vector2(0.16f, 0.07f), 0.06f));
        }

        static float GridShape(Vector2 p)
        {
            float d = float.MaxValue;
            for (int y = -1; y <= 1; y += 2)
                for (int x = -1; x <= 1; x += 2)
                    d = Mathf.Min(d, Box(p, new Vector2(x * 0.42f, y * 0.42f), new Vector2(0.33f, 0.33f), 0.1f));
            return d;
        }

        static float PlayShape(Vector2 p) =>
            Polygon(p, new[] { new Vector2(-0.4f, 0.58f), new Vector2(-0.4f, -0.58f), new Vector2(0.62f, 0f) }) - 0.08f;

        static float LockShape(Vector2 p)
        {
            float d = Box(p, new Vector2(0f, -0.3f), new Vector2(0.6f, 0.45f), 0.12f);
            d = Mathf.Min(d, Arc(p, new Vector2(0f, 0.2f), 0.36f, 0f, 180f, IconStroke));
            d = Mathf.Min(d, Segment(p, new Vector2(-0.36f, 0.2f), new Vector2(-0.36f, -0.1f), IconStroke));
            d = Mathf.Min(d, Segment(p, new Vector2(0.36f, 0.2f), new Vector2(0.36f, -0.1f), IconStroke));
            return Mathf.Max(d, -Disk(p, new Vector2(0f, -0.28f), 0.11f)); // keyhole
        }

        static float BackShape(Vector2 p)
        {
            var corner = new Vector2(-0.3f, 0f);
            return Mathf.Min(Segment(p, new Vector2(0.25f, 0.6f), corner, 0.11f), Segment(p, corner, new Vector2(0.25f, -0.6f), 0.11f));
        }

        // Signed distances in normalized units (negative inside); combine with Min (union) and Max (cut).

        static Vector2 Polar(float degrees, float radius) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;

        static float Disk(Vector2 p, Vector2 center, float radius) => (p - center).magnitude - radius;

        /// <summary>Rounded box; <paramref name="half"/> is the half size including the rounding.</summary>
        static float Box(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>Stroke with round caps.</summary>
        static float Segment(Vector2 p, Vector2 a, Vector2 b, float halfWidth) => Tapered(p, a, b, halfWidth, halfWidth);

        /// <summary>Stroke whose half width goes from <paramref name="fromWidth"/> at a to <paramref name="toWidth"/> at b.</summary>
        static float Tapered(Vector2 p, Vector2 a, Vector2 b, float fromWidth, float toWidth)
        {
            Vector2 ab = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - a - ab * h).magnitude - Mathf.Lerp(fromWidth, toWidth, h);
        }

        /// <summary>Stroke along a circle, counterclockwise from one angle to another (degrees; the end may exceed 360).</summary>
        static float Arc(Vector2 p, Vector2 center, float radius, float fromDegrees, float toDegrees, float halfWidth)
        {
            Vector2 v = p - center;
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (Mathf.Repeat(angle - fromDegrees, 360f) <= toDegrees - fromDegrees)
                return Mathf.Abs(v.magnitude - radius) - halfWidth;
            float toStart = (v - Polar(fromDegrees, radius)).magnitude;
            float toEnd = (v - Polar(toDegrees, radius)).magnitude;
            return Mathf.Min(toStart, toEnd) - halfWidth;
        }

        static float Polygon(Vector2 p, Vector2[] points)
        {
            float distance = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i], b = points[j];
                Vector2 ab = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
                distance = Mathf.Min(distance, (p - a - ab * h).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < ab.x * (p.y - a.y) / ab.y + a.x) inside = !inside;
            }
            return inside ? -distance : distance;
        }

        static float RoundedRectAlpha(float px, float py, int size)
        {
            const float radius = 18f;
            float half = size / 2f;
            float qx = Mathf.Abs(px - half) - (half - radius);
            float qy = Mathf.Abs(py - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
            return Mathf.Clamp01(0.5f - outside);
        }

        static float RoundedFrameAlpha(float px, float py, int size)
        {
            const float stroke = 7f;
            float inside = RoundedRectAlpha(px, py, size);
            // The same rounded rect shrunk by the stroke, cut out of the middle.
            const float radius = 18f - stroke;
            float half = size / 2f;
            float qx = Mathf.Abs(px - half) - (half - stroke - radius);
            float qy = Mathf.Abs(py - half) - (half - stroke - radius);
            float hole = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
            return Mathf.Min(inside, Mathf.Clamp01(0.5f + hole));
        }

        static float DashedRingAlpha(float px, float py, int size)
        {
            const int dashes = 10;
            float ring = RingAlpha(px, py, size);
            float c = size / 2f;
            float turns = (Mathf.Atan2(py - c, px - c) / (2f * Mathf.PI) + 1f) * dashes;
            // Antialiased dash edges: distance to the nearest gap center, in fractions of a dash period.
            float phase = Mathf.Abs(Mathf.Repeat(turns, 1f) - 0.5f);
            float edge = (phase - 0.28f) * 2f * Mathf.PI * DistanceFromCenter(px, py, size) / dashes;
            return Mathf.Min(ring, Mathf.Clamp01(0.5f - edge));
        }

        static float CircleAlpha(float px, float py, int size) =>
            Mathf.Clamp01(size / 2f - 1f - DistanceFromCenter(px, py, size) + 0.5f);

        static float RingAlpha(float px, float py, int size)
        {
            float outer = size / 2f - 1f;
            float inner = outer - size * 0.11f;
            float d = DistanceFromCenter(px, py, size);
            return Mathf.Clamp01(Mathf.Min(outer - d, d - inner) + 0.5f);
        }

        static float StarAlpha(float px, float py, int size)
        {
            // 4x4 supersampling of a point-in-polygon test gives smooth edges.
            int inside = 0;
            for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (InStar(px - 0.5f + (sx + 0.5f) / 4f, py - 0.5f + (sy + 0.5f) / 4f, size)) inside++;
            return inside / 16f;
        }

        static bool InStar(float x, float y, int size)
        {
            float center = size / 2f;
            float outer = center - 2f;
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                Vector2 a = StarPoint(i, center, outer);
                Vector2 b = StarPoint(j, center, outer);
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        static Vector2 StarPoint(int index, float center, float outer)
        {
            float angle = Mathf.PI / 2f + index * Mathf.PI / 5f;
            float radius = index % 2 == 0 ? outer : outer * StarInnerRatio;
            return new Vector2(center + Mathf.Cos(angle) * radius, center + Mathf.Sin(angle) * radius);
        }

        static float DistanceFromCenter(float px, float py, int size)
        {
            float c = size / 2f;
            return Mathf.Sqrt((px - c) * (px - c) + (py - c) * (py - c));
        }
    }
}
