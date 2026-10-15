using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Each time the object is enabled, slides its rect up from <see cref="Distance"/> below its resting position
    /// (the menu sheet). Unscaled time; disabling mid-slide leaves the rect at rest. Add it after positioning the
    /// rect: the resting position is read when the component wakes.
    /// </summary>
    public sealed class SlideIn : MonoBehaviour
    {
        public float Distance { get; set; } = 800f;
        public float Seconds { get; set; } = 0.22f;

        RectTransform rect;
        Vector2 rest;
        float elapsed;
        bool sliding;

        void Awake()
        {
            rect = (RectTransform)transform;
            rest = rect.anchoredPosition;
        }

        void OnEnable()
        {
            elapsed = 0f;
            sliding = true;
            rect.anchoredPosition = rest - new Vector2(0f, Distance);
        }

        void OnDisable()
        {
            sliding = false;
            rect.anchoredPosition = rest;
        }

        void Update()
        {
            if (!sliding) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Seconds);
            float k = 1f - (1f - t) * (1f - t) * (1f - t);
            rect.anchoredPosition = rest - new Vector2(0f, Distance * (1f - k));
            if (t >= 1f) sliding = false;
        }
    }
}
