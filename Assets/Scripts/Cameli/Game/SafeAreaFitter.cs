using UnityEngine;

namespace Cameli.Game
{
    /// <summary>Keeps a full-screen RectTransform inside Screen.safeArea (notches, rounded corners, home bar).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int screenSize;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || screenSize.x != Screen.width || screenSize.y != Screen.height) Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
