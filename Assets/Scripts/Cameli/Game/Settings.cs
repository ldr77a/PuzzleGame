using UnityEngine;

namespace Cameli.Game
{
    /// <summary>Player settings from the menu, saved in PlayerPrefs. Both default to on.</summary>
    static class Settings
    {
        const string SoundKey = "cameli.settings.sound";
        const string HapticsKey = "cameli.settings.haptics";

        /// <summary>Sound effects (<see cref="Sfx"/>).</summary>
        public static bool Sound
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) != 0;
            set => Save(SoundKey, value);
        }

        /// <summary>Vibration (<see cref="Haptics"/>).</summary>
        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) != 0;
            set => Save(HapticsKey, value);
        }

        static void Save(string key, bool on)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
