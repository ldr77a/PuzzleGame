using UnityEngine;

namespace Cameli.Game
{
    /// <summary>
    /// Swells a rect gently and back, around its pivot (keep it centered), to ask for a tap or a look: the drop count,
    /// [다시 하기], the hint button. Starts from rest when enabled and rests again when disabled. Unscaled time.
    /// </summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Scale { get; set; } = 1.12f;
        public float Period { get; set; } = 1.2f;

        float clock;

        /// <summary>Adds a pulse to <paramref name="target"/>'s object, switched off until enabled.</summary>
        public static Pulse AddTo(Component target, float scale)
        {
            var pulse = target.gameObject.AddComponent<Pulse>();
            pulse.Scale = scale;
            pulse.enabled = false;
            return pulse;
        }

        void OnEnable() => clock = 0f;

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            float wave = 0.5f - Mathf.Cos(clock * 2f * Mathf.PI / Period) * 0.5f;
            float scale = Mathf.Lerp(1f, Scale, wave);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        void OnDisable() => transform.localScale = Vector3.one;
    }
}
