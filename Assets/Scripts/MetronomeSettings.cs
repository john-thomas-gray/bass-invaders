using UnityEngine;

namespace CandyCruisers
{
    public static class MetronomeSettings
    {
        public const string VoiceKey = "CandyCruisers.Metronome.Voice";
        public const string VisualKey = "CandyCruisers.Metronome.Visual";
        public const string RoundClockKey = "CandyCruisers.RoundClock.Visible";
        public static bool VoiceEnabled
        {
            get => PlayerPrefs.GetInt(VoiceKey, 0) != 0;
            set { PlayerPrefs.SetInt(VoiceKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool VisualEnabled
        {
            get => PlayerPrefs.GetInt(VisualKey, 0) != 0;
            set { PlayerPrefs.SetInt(VisualKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool RoundClockEnabled
        {
            get => PlayerPrefs.GetInt(RoundClockKey, 0) != 0;
            set { PlayerPrefs.SetInt(RoundClockKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
