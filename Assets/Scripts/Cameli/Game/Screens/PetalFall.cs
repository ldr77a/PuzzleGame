using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// Camellia petals that fall once across its rect when the clear screen's flower opens: they start at varied
    /// heights above the top edge, spread across the width, and drift, spin, and fade out on the way down. Decorative
    /// only (no raycast targets). Unscaled time; disabling the object, or <see cref="Stop"/>, removes them at once.
    /// </summary>
    public sealed class PetalFall : MonoBehaviour
    {
        const int Count = 11;

        /// <summary>The petal's square; the shape inside is about 50 x 66 (see <see cref="SpriteFactory.Petal"/>).</summary>
        const float PetalSize = 72f;

        const float FallSeconds = 3.2f;
        const float MaxDelaySeconds = 0.24f;
        const float FadeInPart = 0.08f;
        const float FadeOutPart = 0.18f;
        const float MinAbove = 70f, MaxAbove = 360f;
        const float MinDrift = 40f, MaxDrift = 160f;
        const float MinSpin = 160f, MaxSpin = 560f;
        const float FirstX = 0.06f, StepX = 0.085f;

        /// <summary>Every third petal is lightened toward white this much, like the flower's inner petals.</summary>
        const float LightTint = 0.28f;

        readonly Image[] petals = new Image[Count];
        readonly float[] delay = new float[Count];
        readonly float[] above = new float[Count];
        readonly float[] drift = new float[Count];
        readonly float[] spin = new float[Count];
        readonly float[] angle = new float[Count];
        float clock;
        float distance;
        bool falling;

        /// <summary>Builds the (idle) petals in a full-size child of <paramref name="parent"/>.</summary>
        internal static PetalFall Create(Transform parent)
        {
            RectTransform root = Widgets.Layer(parent, "Petals");
            var fall = root.gameObject.AddComponent<PetalFall>();
            for (int i = 0; i < Count; i++)
            {
                Image petal = Ui.NewImage($"Petal {i + 1}", root, SpriteFactory.Petal, Color.white);
                RectTransform rt = Ui.Box(petal.rectTransform, new Vector2(FirstX + i * StepX, 1f), Vector2.zero, new Vector2(PetalSize, PetalSize));
                rt.pivot = new Vector2(0.5f, 0.5f);
                petal.gameObject.SetActive(false);
                fall.petals[i] = petal;
            }
            return fall;
        }

        /// <summary>Starts one fall in <paramref name="color"/> (every third petal lighter), replacing any still falling.</summary>
        public void Drop(Color color)
        {
            Color light = Theme.Lighten(color, LightTint);
            distance = ((RectTransform)transform).rect.height + PetalSize;
            for (int i = 0; i < Count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                delay[i] = Random.Range(0f, MaxDelaySeconds);
                above[i] = Random.Range(MinAbove, MaxAbove);
                drift[i] = side * Random.Range(MinDrift, MaxDrift);
                spin[i] = -side * Random.Range(MinSpin, MaxSpin);
                angle[i] = Random.Range(0f, 360f);

                petals[i].color = i % 3 == 2 ? light : color;
                petals[i].gameObject.SetActive(true);
                Pose(i, 0f);
            }
            clock = 0f;
            falling = true;
        }

        /// <summary>Removes the petals at once.</summary>
        public void Stop()
        {
            falling = false;
            foreach (Image petal in petals) petal.gameObject.SetActive(false);
        }

        void OnDisable() => Stop();

        void Update()
        {
            if (!falling) return;
            clock += Time.unscaledDeltaTime;
            bool any = false;
            for (int i = 0; i < Count; i++)
            {
                Image petal = petals[i];
                if (!petal.gameObject.activeSelf) continue;
                float t = (clock - delay[i]) / FallSeconds;
                if (t >= 1f)
                {
                    petal.gameObject.SetActive(false);
                    continue;
                }
                any = true;
                Pose(i, Mathf.Max(t, 0f));
            }
            falling = any;
        }

        /// <summary>Places petal <paramref name="i"/> at <paramref name="t"/> (0..1) of its fall.</summary>
        void Pose(int i, float t)
        {
            RectTransform rt = petals[i].rectTransform;
            float fall = (above[i] + distance) * t * t; // ease-in
            rt.anchoredPosition = new Vector2(drift[i] * t, above[i] - fall);
            rt.localEulerAngles = new Vector3(0f, 0f, angle[i] + spin[i] * t);
            Color color = petals[i].color;
            color.a = Mathf.Min(Mathf.Clamp01(t / FadeInPart), Mathf.Clamp01((1f - t) / FadeOutPart));
            petals[i].color = color;
        }
    }
}
