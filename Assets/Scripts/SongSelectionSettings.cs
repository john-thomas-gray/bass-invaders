using UnityEngine;

namespace CandyCruisers
{
    public static class SongSelectionSettings
    {
        public const string PreferencePrefix = "CandyCruisers.Songs.";
        public static bool IsSelected(string resourceName) =>
            PlayerPrefs.GetInt(PreferencePrefix + resourceName, 1) != 0;

        public static int SelectedCount(string[] songs) => System.Array.FindAll(songs, IsSelected).Length;

        public static bool TrySetSelected(string resourceName, bool selected, string[] songs)
        {
            if (System.Array.IndexOf(songs, resourceName) < 0) return false;
            if (!selected && IsSelected(resourceName) && SelectedCount(songs) <= 1) return false;
            PlayerPrefs.SetInt(PreferencePrefix + resourceName, selected ? 1 : 0);
            PlayerPrefs.Save();
            return true;
        }

        public static void EnsureSelection(string[] songs)
        {
            if (songs.Length > 0 && SelectedCount(songs) == 0)
                TrySetSelected(songs[0], true, songs);
        }

        public static string DisplayName(string resourceName) =>
            System.Text.RegularExpressions.Regex.Replace(resourceName, @"(?<=[a-z0-9])(?=[A-Z0-9])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }
}
