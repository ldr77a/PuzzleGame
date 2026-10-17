using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Sticker look for a static label (the title logo): a round border all around the glyphs and a soft drop shadow
    /// behind it. uGUI's Outline only adds the four diagonal copies, which gets lumpy at a thick border; this adds a
    /// ring of copies instead. Set the properties right after adding it, before the label first draws.
    /// </summary>
    public sealed class StickerOutline : BaseMeshEffect
    {
        /// <summary>Copies around the ring. At 12 the border dips at most 3% between two copies.</summary>
        const int BorderCopies = 12;

        /// <summary>Shadow copies: one at the offset and four around it, so the shadow's edge fades.</summary>
        const int ShadowCopies = 5;

        static readonly List<UIVertex> source = new List<UIVertex>();
        static readonly List<UIVertex> output = new List<UIVertex>();

        public Color BorderColor { get; set; } = Color.white;

        /// <summary>Border width in canvas units.</summary>
        public float BorderWidth { get; set; }

        /// <summary>The shadow's color where its copies all overlap; its alpha is split among them.</summary>
        public Color ShadowColor { get; set; } = Color.clear;

        /// <summary>How far the shadow sits below the glyphs, in canvas units.</summary>
        public float ShadowDistance { get; set; }

        /// <summary>How far the outer shadow copies spread from the middle one: the width of the fading edge.</summary>
        public float ShadowSoftness { get; set; }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            source.Clear();
            vh.GetUIVertexStream(source);
            output.Clear();

            // Drawn first, so the border and the glyphs cover it.
            if (ShadowColor.a > 0f)
            {
                Color copy = ShadowColor;
                copy.a = 1f - Mathf.Pow(1f - ShadowColor.a, 1f / ShadowCopies);
                Append(new Vector2(0f, -ShadowDistance), copy);
                Append(new Vector2(-ShadowSoftness, -ShadowDistance), copy);
                Append(new Vector2(ShadowSoftness, -ShadowDistance), copy);
                Append(new Vector2(0f, -ShadowDistance - ShadowSoftness), copy);
                Append(new Vector2(0f, -ShadowDistance + ShadowSoftness), copy);
            }

            if (BorderWidth > 0f)
            {
                for (int i = 0; i < BorderCopies; i++)
                {
                    float angle = i * 2f * Mathf.PI / BorderCopies;
                    Append(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BorderWidth, BorderColor);
                }
            }

            output.AddRange(source);
            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }

        /// <summary>Adds a copy of the glyphs moved by <paramref name="offset"/>, in <paramref name="color"/> times their alpha.</summary>
        static void Append(Vector2 offset, Color color)
        {
            for (int i = 0; i < source.Count; i++)
            {
                UIVertex vertex = source[i];
                vertex.position += (Vector3)offset;
                Color tint = color;
                tint.a *= vertex.color.a / 255f;
                vertex.color = tint;
                output.Add(vertex);
            }
        }
    }
}
