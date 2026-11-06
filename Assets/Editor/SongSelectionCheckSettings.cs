using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    internal sealed class SongSelectionCheckSettings : IDisposable
    {
        private readonly string[] names = GameplayMusicPlayer.DefaultSoundtrackNames;
        private readonly int[] previous;
        internal SongSelectionCheckSettings()
        {
            previous = Array.ConvertAll(names, name => PlayerPrefs.GetInt(SongSelectionSettings.PreferencePrefix + name, -1));
            foreach (string name in names) PlayerPrefs.DeleteKey(SongSelectionSettings.PreferencePrefix + name);
        }
        public void Dispose()
        {
            for (int i = 0; i < names.Length; i++) Restore(names[i], previous[i]);
            PlayerPrefs.Save();
        }
        private static void Restore(string name, int value)
        {
            string key = SongSelectionSettings.PreferencePrefix + name;
            if (value < 0) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetInt(key, value);
        }
        internal static void BeginRuntime(string key)
        {
            foreach (string name in GameplayMusicPlayer.DefaultSoundtrackNames)
            {
                SessionState.SetInt(key + ".Song." + name, PlayerPrefs.GetInt(SongSelectionSettings.PreferencePrefix + name, -1));
                PlayerPrefs.DeleteKey(SongSelectionSettings.PreferencePrefix + name);
            }
        }
        internal static void EndRuntime(string key)
        {
            foreach (string name in GameplayMusicPlayer.DefaultSoundtrackNames)
                Restore(name, SessionState.GetInt(key + ".Song." + name, -1));
            PlayerPrefs.Save();
        }
    }
}
