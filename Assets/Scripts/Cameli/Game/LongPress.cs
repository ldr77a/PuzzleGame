using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cameli.Game
{
    /// <summary>
    /// Raises <see cref="Held"/> once the pointer has stayed down on this object for <see cref="Seconds"/>.
    /// Needs a raycast-target Graphic on the same object.
    /// </summary>
    public sealed class LongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action Held;

        public float Seconds { get; set; } = 2f;

        float pressedAt = -1f;

        public void OnPointerDown(PointerEventData eventData) => pressedAt = Time.unscaledTime;
        public void OnPointerUp(PointerEventData eventData) => pressedAt = -1f;
        public void OnPointerExit(PointerEventData eventData) => pressedAt = -1f;

        void Update()
        {
            if (pressedAt < 0f || Time.unscaledTime - pressedAt < Seconds) return;
            pressedAt = -1f;
            Held?.Invoke();
        }
    }
}
