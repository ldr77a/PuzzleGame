using System;
using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Flat white shapes generated at runtime and tinted with Image.color, so the prototype needs no art
    /// assets. Game colors stay flat and exact, as the design requires.
    /// </summary>
    static class SpriteFactory
    {
        const float StarInnerRatio = 0.46f;

        static Sprite roundedRect;
        static Sprite circle;
        static Sprite ring;
        static Sprite star;

        /// <summary>Rounded square, 9-sliced so it can stretch to any size.</summary>
        public static Sprite RoundedRect => roundedRect != null ? roundedRect : roundedRect = Make("RoundedRect", 64, 22, RoundedRectAlpha);

        public static Sprite Circle => circle != null ? circle : circle = Make("Circle", 128, 0, CircleAlpha);
        public static Sprite Ring => ring != null ? ring : ring = Make("Ring", 128, 0, RingAlpha);
        public static Sprite Star => star != null ? star : star = Make("Star", 128, 0, StarAlpha);

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

        static float RoundedRectAlpha(float px, float py, int size)
        {
            const float radius = 18f;
            float half = size / 2f;
            float qx = Mathf.Abs(px - half) - (half - radius);
            float qy = Mathf.Abs(py - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
            return Mathf.Clamp01(0.5f - outside);
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
