using UnityEngine;

namespace Cameli.Game
{
    /// <summary>Rocks a rect gently around its pivot (the "play next" bud, title flowers). Unscaled time.</summary>
    public sealed class Sway : MonoBehaviour
    {
        public float Degrees { get; set; } = 6f;
        public float Speed { get; set; } = 2.4f;

        /// <summary>Phase offset in seconds, so neighbors don't move in lockstep.</summary>
        public float Offset { get; set; }

        void Update() =>
            transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin((Time.unscaledTime + Offset) * Speed) * Degrees);

        void OnDisable() => transform.localEulerAngles = Vector3.zero;
    }
}
