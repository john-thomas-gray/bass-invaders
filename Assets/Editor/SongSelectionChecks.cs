using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SongSelectionChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("Candy Cruisers/Run Song Selection Checks")]
        public static void Run()
        {
            using var settings = new SongSelectionCheckSettings();
            using var metronomes = new MetronomeCheckSettings();
            var names = GameplayMusicPlayer.DefaultSoundtrackNames;
            Check(SongSelectionSettings.SelectedCount(names) == names.Length, "Every song defaults checked");
            Check(!SongSelectionSettings.TrySetSelected("Unknown", false, names), "Unknown songs cannot alter the selection");
            for (int i = 1; i < names.Length; i++) Check(SongSelectionSettings.TrySetSelected(names[i], false, names), "Songs can be unchecked");
            Check(!SongSelectionSettings.TrySetSelected(names[0], false, names) && SongSelectionSettings.IsSelected(names[0]),
                "The last checked song cannot be unchecked");
            Check(SongSelectionSettings.TrySetSelected(names[1], true, names) && SongSelectionSettings.TrySetSelected(names[0], false, names),
                "Checking another song permits changing the final selection");
            Check(PlayerPrefs.GetInt(SongSelectionSettings.PreferencePrefix + names[0]) == 0, "Selections are persisted by stable resource name");
            foreach (string name in names) PlayerPrefs.SetInt(SongSelectionSettings.PreferencePrefix + name, 0);
            SongSelectionSettings.EnsureSelection(names);
            Check(SongSelectionSettings.SelectedCount(names) == 1, "Invalid empty saved selections recover safely");
            foreach (string name in names) PlayerPrefs.DeleteKey(SongSelectionSettings.PreferencePrefix + name);
            Check(SongSelectionSettings.DisplayName("IntergalacticEmotionalBreakdown") == "Intergalactic Emotional Breakdown" &&
                SongSelectionSettings.DisplayName("DownToEarthPart1") == "Down To Earth Part 1", "Resource identifiers become readable song titles");
            CheckLayouts();
            CheckSession();
            Debug.Log("Song selection checks passed: checked defaults, saved choices, last-song protection, menu input, pagination, safe areas, filtered shuffle, menu carryover and level fallback.");
        }

        private static void CheckSession()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player); session.PresentationPreview = true;
                session.LevelCatalog = LevelCatalog.Parse(LevelProgressionChecks.FixtureJson);
                if (!Application.isPlaying) Invoke(session, "OnEnable");
                Invoke(session, "Start");
                var music = grid.GetComponent<GameplayMusicPlayer>();
                try
                {
                    session.Tick(GameSession.MenuWordIntroDuration + 1);
                    Check(session.SongSelectionMenuAvailable && session.HandleMenuKey(KeyCode.S) && session.SongSelection.IsOpen,
                        "Song Selection opens from the main menu");
                    Check(session.HandleMenuKey(KeyCode.M) && session.HandleMenuKey(KeyCode.R) && session.HandleMenuKey(KeyCode.Alpha9) &&
                        session.State == GameSession.RunState.MainMenu && !session.MenuArriving, "Options consume background menu shortcuts");
                    var names = music.SoundtrackNames;
                    string only = names[0];
                    foreach (string name in names.Skip(1)) Check(music.SetSongSelected(name, false), "Music accepts saved exclusions");
                    Check(!music.SetSongSelected(only, false), "Playback also rejects an empty selection");
                    for (int i = 0; i < names.Length * 2; i++)
                    {
                        music.SelectNextTrack();
                        Check(music.Track == null || music.Track.name == only, "Shuffle never selects excluded tracks");
                    }
                    Check(music.MenuTrack == null || music.MenuTrack.name == only, "Menu carryover respects the selection");
                    Check(session.HandleMenuKey(KeyCode.Escape) && !session.SongSelection.IsOpen && !session.MenuArriving,
                        "Escape returns to the main menu without starting");
                    session.StartRun();
                    Check(!session.SongSelectionMenuAvailable && !session.HandleMenuKey(KeyCode.S), "Gameplay requires pause before editing songs");
                    Check(music.Track == null || music.Track.name == only, "A level with no selected songs uses the player's selected soundtrack");
                    Check(!music.IsTrackAllowedForLevel("AnotherJoe") && music.IsTrackAllowedForLevel(only), "Level fallback excludes unchecked songs");
                    session.Pause();
                    Check(session.HandleMenuKey(KeyCode.S) && session.SongSelection.IsOpen && session.IsPaused, "Pause opens Song Selection without resuming");
                    session.HandleMenuKey(KeyCode.Return);
                    Check(session.IsPaused && session.SongSelection.IsOpen && SongSelectionSettings.IsSelected(only),
                        "Enter toggles the focused checkbox and cannot clear the final song or resume");
                    var screen = new Vector2(390, 844);
                    float scale = GameSession.GuiScaleFor(true, screen.x, screen.y);
                    var layout = new SongSelectionMenu.Layout(screen, new Rect(Vector2.zero, screen), scale, true);
                    session.SongSelection.Activate(layout.Next.center, music, layout);
                    Check(session.SongSelection.FocusedSong == layout.Rows, "Next reveals the following song page");
                    session.SongSelection.Activate(layout.Row(0).center, music, layout);
                    Check(SongSelectionSettings.IsSelected(names[layout.Rows]), "Touching a paged row toggles the correct song");
                    session.SongSelection.Activate(new Vector2(-10, -10), music, layout);
                    Check(session.SongSelection.IsOpen && session.IsPaused, "Outside touches are consumed by the options");
                    session.SongSelection.Activate(layout.Back.center, music, layout);
                    Check(!session.SongSelection.IsOpen && session.IsPaused, "Back preserves pause");
                    music.SetSongSelected("AnotherJoe", true);
                    Check(music.IsTrackAllowedForLevel("AnotherJoe") && !music.IsTrackAllowedForLevel(only),
                        "Rechecking a level song restores the selected intersection");
                    session.Resume();
                    Check(!session.SongSelection.IsOpen && !session.IsPaused, "Resume closes song options");
                }
                finally
                {
                    if (!Application.isPlaying) Invoke(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void CheckLayouts()
        {
            foreach (var screen in new[] { new Vector2(390, 844), new Vector2(844, 390), new Vector2(960, 640), new Vector2(1280, 720), new Vector2(1170, 2532) })
                foreach (bool mobile in new[] { false, true })
                {
                    var safe = new Rect(20, 24, screen.x - 40, screen.y - 48);
                    float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
                    var layout = new SongSelectionMenu.Layout(screen, safe, scale, mobile);
                    var canvasSafe = new Rect(safe.x / scale, (screen.y - safe.yMax) / scale, safe.width / scale, safe.height / scale);
                    foreach (var rect in new[] { layout.Panel, layout.Previous, layout.Next, layout.Back,
                        layout.Row(0), layout.Row(layout.Rows - 1), SongSelectionMenu.ButtonRect(screen, safe, scale, mobile) })
                        Check(canvasSafe.Contains(rect.min) && canvasSafe.Contains(rect.max), "Every song control fits the safe area " + screen + " mobile=" + mobile);
                    Check(!SongSelectionMenu.ButtonRect(screen, safe, scale, mobile).Overlaps(GameSession.MetronomeMenuButtonRect(screen, safe, scale, mobile)),
                        "Song and metronome entries do not overlap");
                    Check(layout.Row(layout.Rows - 1).yMax <= layout.Previous.y && layout.Previous.yMax <= layout.Back.y,
                        "Song rows, paging and Back do not overlap");
                    Check(layout.Row(0).height * scale >= 44, "Rows retain touch-sized hit targets");
                }
        }

        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Hidden).Invoke(target, null);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Song selection check failed: " + message); }
    }
}
