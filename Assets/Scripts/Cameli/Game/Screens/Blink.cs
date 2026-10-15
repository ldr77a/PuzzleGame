using UnityEngine;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>Fades the Graphic on the same object in and out ("화면을 눌러 시작"). Unscaled time.</summary>
    public sealed class Blink : MonoBehaviour
    {
        public float Period { get; set; } = 1.6f;
        public float MinAlpha { get; set; } = 0.2f;

        Graphic graphic;

        void Awake() => graphic = GetComponent<Graphic>();

        void Update()
        {
            if (graphic == null) return;
            float wave = Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / Period) * 0.5f + 0.5f;
            Color color = graphic.color;
            color.a = Mathf.Lerp(MinAlpha, 1f, wave);
            graphic.color = color;
        }
    }
}
