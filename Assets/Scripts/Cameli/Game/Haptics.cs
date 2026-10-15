using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Cameli.Game
{
    /// <summary>
    /// A light tap of vibration when a color is eaten. Android: the Vibrator service for about 20 ms. iOS:
    /// UIImpactFeedbackGenerator (light), through Assets/Plugins/iOS/CameliHaptics.mm. Does nothing in the editor
    /// or while <see cref="Settings.Haptics"/> is off.
    /// </summary>
    static class Haptics
    {
        public static void Tap()
        {
            if (!Settings.Haptics) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidTap();
#elif UNITY_IOS && !UNITY_EDITOR
            CameliHapticsLightTap();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void CameliHapticsLightTap();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        const long TapMilliseconds = 20;
        const int DefaultAmplitude = -1; // VibrationEffect.DEFAULT_AMPLITUDE

        static AndroidJavaObject vibrator;
        static int sdkLevel;
        static bool unavailable;

        static void AndroidTap()
        {
            if (unavailable) return;
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        sdkLevel = version.GetStatic<int>("SDK_INT");
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    {
                        unavailable = true;
                        return;
                    }
                }

                if (sdkLevel >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = effects.CallStatic<AndroidJavaObject>("createOneShot", TapMilliseconds, DefaultAmplitude))
                        vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", TapMilliseconds);
                }
            }
            catch (Exception e)
            {
                // A missing permission or an odd device should never break play; stop trying after the first failure.
                unavailable = true;
                Debug.LogWarning($"진동을 쓸 수 없어요: {e.Message}");
            }
        }
#endif

        /// <summary>
        /// Never called. Unity adds android.permission.VIBRATE to the manifest only when scripts reference
        /// Handheld.Vibrate, and the Vibrator calls above need that permission. [Preserve] keeps code stripping
        /// from removing it.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        static void KeepVibratePermission() => Handheld.Vibrate();
    }
}
