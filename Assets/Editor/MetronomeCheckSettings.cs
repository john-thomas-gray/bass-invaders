using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    internal sealed class MetronomeCheckSettings : IDisposable
    {
        private readonly bool hadVoice = PlayerPrefs.HasKey(MetronomeSettings.VoiceKey);
        private readonly bool hadVisual = PlayerPrefs.HasKey(MetronomeSettings.VisualKey);
        private readonly bool hadClock = PlayerPrefs.HasKey(MetronomeSettings.RoundClockKey);
        private readonly int voice = PlayerPrefs.GetInt(MetronomeSettings.VoiceKey);
        private readonly int visual = PlayerPrefs.GetInt(MetronomeSettings.VisualKey);
        private readonly int clock = PlayerPrefs.GetInt(MetronomeSettings.RoundClockKey);
        internal MetronomeCheckSettings()
        {
            PlayerPrefs.SetInt(MetronomeSettings.VoiceKey, 0);
            PlayerPrefs.SetInt(MetronomeSettings.VisualKey, 0);
            PlayerPrefs.SetInt(MetronomeSettings.RoundClockKey, 0);
        }
        public void Dispose() => Restore(hadVoice, voice, hadVisual, visual, hadClock, clock);
        private static void Restore(bool hadVoice, int voice, bool hadVisual, int visual, bool hadClock, int clock)
        {
            if (hadVoice) PlayerPrefs.SetInt(MetronomeSettings.VoiceKey, voice); else PlayerPrefs.DeleteKey(MetronomeSettings.VoiceKey);
            if (hadVisual) PlayerPrefs.SetInt(MetronomeSettings.VisualKey, visual); else PlayerPrefs.DeleteKey(MetronomeSettings.VisualKey);
            if (hadClock) PlayerPrefs.SetInt(MetronomeSettings.RoundClockKey, clock); else PlayerPrefs.DeleteKey(MetronomeSettings.RoundClockKey);
            PlayerPrefs.Save();
        }
        internal static void BeginRuntime(string key, bool voice = false, bool visual = false)
        {
            SongSelectionCheckSettings.BeginRuntime(key);
            SessionState.SetBool(key + ".HadPlaytestOptions", PlayerPrefs.HasKey(PlaytestOptions.Key));
            SessionState.SetString(key + ".PlaytestOptions", PlayerPrefs.GetString(PlaytestOptions.Key, "{}"));
            PlayerPrefs.DeleteKey(PlaytestOptions.Key);
            SessionState.SetBool(key + ".Invincibility", DeveloperOptions.PlayerInvincible);
            SessionState.SetBool(key + ".HadVoice", PlayerPrefs.HasKey(MetronomeSettings.VoiceKey));
            SessionState.SetBool(key + ".HadVisual", PlayerPrefs.HasKey(MetronomeSettings.VisualKey));
            SessionState.SetBool(key + ".HadClock", PlayerPrefs.HasKey(MetronomeSettings.RoundClockKey));
            SessionState.SetInt(key + ".Voice", PlayerPrefs.GetInt(MetronomeSettings.VoiceKey));
            SessionState.SetInt(key + ".Visual", PlayerPrefs.GetInt(MetronomeSettings.VisualKey));
            SessionState.SetInt(key + ".Clock", PlayerPrefs.GetInt(MetronomeSettings.RoundClockKey));
            PlayerPrefs.SetInt(MetronomeSettings.VoiceKey, voice ? 1 : 0);
            PlayerPrefs.SetInt(MetronomeSettings.VisualKey, visual ? 1 : 0);
            PlayerPrefs.SetInt(MetronomeSettings.RoundClockKey, 0);
        }
        internal static void EndRuntime(string key)
        {
            SongSelectionCheckSettings.EndRuntime(key);
            if (SessionState.GetBool(key + ".HadPlaytestOptions", false))
                PlayerPrefs.SetString(PlaytestOptions.Key, SessionState.GetString(key + ".PlaytestOptions", "{}"));
            else PlayerPrefs.DeleteKey(PlaytestOptions.Key);
            DeveloperOptions.PlayerInvincible = SessionState.GetBool(key + ".Invincibility", false);
            Restore(SessionState.GetBool(key + ".HadVoice", false), SessionState.GetInt(key + ".Voice", 0),
                SessionState.GetBool(key + ".HadVisual", false), SessionState.GetInt(key + ".Visual", 0),
                SessionState.GetBool(key + ".HadClock", false), SessionState.GetInt(key + ".Clock", 0));
        }
    }
}
