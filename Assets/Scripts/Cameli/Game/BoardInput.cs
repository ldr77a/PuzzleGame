using System;
using Cameli.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cameli.Game
{
    /// <summary>
    /// Turns touches and clicks on the board into a swipe direction or a tapped cell. Needs a raycast-target
    /// Graphic on the same object (the board background).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action<Direction> Swiped;
        public event Action<int, int> Tapped;

        public int Columns { get; set; } = 1;
        public int Rows { get; set; } = 1;

        Vector2 pressPosition;
        bool pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressPosition = eventData.position;
            pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;

            Vector2 delta = eventData.position - pressPosition;
            float threshold = Mathf.Max(24f, Mathf.Min(Screen.width, Screen.height) * 0.03f);
            if (delta.magnitude >= threshold)
            {
                Swiped?.Invoke(Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? delta.x > 0 ? Direction.Right : Direction.Left
                    : delta.y > 0 ? Direction.Up : Direction.Down);
                return;
            }

            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            Vector2 normalized = Rect.PointToNormalized(rect.rect, local);
            int column = Mathf.Clamp((int)(normalized.x * Columns), 0, Columns - 1);
            int row = Mathf.Clamp((int)((1f - normalized.y) * Rows), 0, Rows - 1);
            Tapped?.Invoke(column, row);
        }
    }
}
