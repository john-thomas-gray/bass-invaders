using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MetronomeOptionsChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            using var settings = new MetronomeCheckSettings();
            PlayerPrefs.DeleteKey(MetronomeSettings.VoiceKey);
            PlayerPrefs.DeleteKey(MetronomeSettings.VisualKey);
            PlayerPrefs.DeleteKey(MetronomeSettings.RoundClockKey);
            Check(!MetronomeSettings.VoiceEnabled && !MetronomeSettings.VisualEnabled, "Both metronomes default off");
            Check(!MetronomeSettings.RoundClockEnabled, "The round clock defaults off");
            Check(!GameplayMusicPlayer.DefaultSoundtrackNames.Contains(GameplayMusicPlayer.CountingResourceName),
                "The spoken metronome is not a regular soundtrack entry");
            CheckLayout();
            CheckRoundClock();
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player); session.PresentationPreview = true;
                if (!Application.isPlaying) Invoke(session, "OnEnable");
                Invoke(session, "Start");
                var music = grid.GetComponent<GameplayMusicPlayer>();
                try
                {
                    Check(music.Source.clip != null && music.ContainsTrack(music.Source.clip.name), "The menu starts with regular music");
                    var screen = new Vector2(390, 844);
                    const bool mobile = true;
                    float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
                    var safe = new Rect(Vector2.zero, screen);
                    var entry = GameSession.MetronomeMenuButtonRect(screen, safe, scale, mobile);
                    Check(!session.MetronomeMenuAvailable && !Activate(session, entry.center, screen, safe, mobile) &&
                        !session.MetronomeMenuOpen && session.State == GameSession.RunState.MainMenu,
                        "The main menu has no metronome entry or active hit target");
                    Check(!session.HandleMenuKey(KeyCode.F9) && !session.HandleMenuKey(KeyCode.F10) &&
                        !MetronomeSettings.VoiceEnabled && !MetronomeSettings.VisualEnabled,
                        "Main-menu shortcuts cannot change metronomes");
                    session.StartRun();
                    Check(!session.MetronomeMenuAvailable && !Activate(session, entry.center, screen, safe, mobile),
                        "Unpaused gameplay has no metronome controls");
                    session.Pause();
                    Check(session.MetronomeMenuAvailable && Activate(session, entry.center, screen, safe, mobile) &&
                        session.MetronomeMenuOpen && session.IsPaused, "Pause opens all metronome controls");
                    var panel = GameSession.MetronomeMenuRect(screen, safe, scale, mobile);
                    Activate(session, GameSession.MetronomeMenuRowRect(panel, 0).center, screen, safe, mobile);
                    Check(MetronomeSettings.VoiceEnabled && !MetronomeSettings.VisualEnabled &&
                        music.Source.clip.name == GameplayMusicPlayer.CountingResourceName && music.Source.loop && !music.ShouldPlayMusic,
                        "Voice ON selects the spoken loop while preserving pause and the independent visual switch");
                    Check(!music.ContainsTrack(GameplayMusicPlayer.CountingResourceName) && !music.SkipCurrentSong() &&
                        music.CurrentDownbeatOffsetBeats == 0, "Voice mode retains its beat metadata outside the shuffle");
                    Activate(session, GameSession.MetronomeMenuRowRect(panel, 1).center, screen, safe, mobile);
                    Check(MetronomeSettings.VisualEnabled && PlayerPrefs.GetInt(MetronomeSettings.VisualKey) == 1 &&
                        PlayerPrefs.GetInt(MetronomeSettings.VoiceKey) == 1, "The independent switches save their values");
                    Activate(session, Vector2.zero, screen, safe, mobile);
                    Check(session.MetronomeMenuOpen && session.IsPaused, "Outside taps cannot resume behind the options panel");
                    Check(session.HandleMenuKey(KeyCode.Return) && session.IsPaused, "Enter cannot resume behind options");
                    Activate(session, GameSession.MetronomeMenuRowRect(panel, 2).center, screen, safe, mobile);
                    Check(MetronomeSettings.RoundClockEnabled && PlayerPrefs.GetInt(MetronomeSettings.RoundClockKey) == 1 &&
                        MetronomeSettings.VisualEnabled && MetronomeSettings.VoiceEnabled,
                        "The saved round clock switch leaves both metronomes enabled");
                    Activate(session, GameSession.MetronomeMenuRowRect(panel, 3).center, screen, safe, mobile);
                    Check(!session.MetronomeMenuOpen && session.IsPaused, "Back returns to the pause menu");
                    session.Resume();
                    Check(music.Source.clip.name == GameplayMusicPlayer.CountingResourceName && music.Source.loop &&
                        session.VisualMetronomeVisible, "Saved voice and visual choices take effect after resume");
                    Check(!session.HandleMenuKey(KeyCode.F9) && !session.HandleMenuKey(KeyCode.F10) &&
                        MetronomeSettings.VoiceEnabled && MetronomeSettings.VisualEnabled, "Unpaused shortcuts cannot change metronomes");
                    session.Pause();
                    Activate(session, entry.center, screen, safe, mobile);
                    Activate(session, GameSession.MetronomeMenuRowRect(panel, 0).center, screen, safe, mobile);
                    Check(session.IsPaused && !MetronomeSettings.VoiceEnabled && MetronomeSettings.VisualEnabled &&
                        music.Source.clip.name != GameplayMusicPlayer.CountingResourceName && !music.ShouldPlayMusic,
                        "Voice OFF restores regular music while preserving pause and the visual selection");
                    Check(session.HandleMenuKey(KeyCode.Escape) && !session.MetronomeMenuOpen && session.IsPaused,
                        "Escape closes options without also resuming");
                    Check(session.HandleMenuKey(KeyCode.F10) && !MetronomeSettings.VisualEnabled && session.IsPaused,
                        "The visual shortcut works while paused");
                    Check(session.HandleMenuKey(KeyCode.F7) && !MetronomeSettings.RoundClockEnabled && session.IsPaused,
                        "The clock shortcut works independently while paused");
                    Check(session.HandleMenuKey(KeyCode.F9) && MetronomeSettings.VoiceEnabled && session.IsPaused &&
                        session.HandleMenuKey(KeyCode.F9) && !MetronomeSettings.VoiceEnabled,
                        "The voice shortcut works while paused");
                    session.Resume();
                    Check(!session.VisualMetronomeVisible && !MetronomeSettings.VisualEnabled,
                        "Resume preserves the disabled visual counter");
                    for (int i = 0; i < music.TrackCount * 2; i++)
                    {
                        music.SelectNextTrack();
                        Check(music.Track != null && music.Track.name != GameplayMusicPlayer.CountingResourceName,
                            "Repeated regular soundtrack rotations never select the spoken metronome");
                    }
                }
                finally
                {
                    if (!Application.isPlaying) Invoke(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
            Debug.Log("Metronome options checks passed: regular-only shuffle, independent saved switches, round clock timing, pause-only menu and shortcuts, voice loop, regular music restoration and safe-area layouts.");
        }

        private static void CheckRoundClock()
        {
            Check(GameSession.FormatRoundTime(0) == "00:00" && GameSession.FormatRoundTime(-1) == "00:00" &&
                GameSession.FormatRoundTime(59.99) == "00:59" && GameSession.FormatRoundTime(60) == "01:00" &&
                GameSession.FormatRoundTime(3599.99) == "59:59" && GameSession.FormatRoundTime(3600) == "60:00" &&
                GameSession.FormatRoundTime(6000) == "100:00", "Clock uses whole minutes and seconds without wrapping at one hour");
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player); session.PresentationPreview = true;
                session.LevelCatalog = null;
                if (!Application.isPlaying) Invoke(session, "OnEnable");
                Invoke(session, "Start");
                try
                {
                    session.Tick(2);
                    Check(session.ElapsedRoundSeconds == 0 && !session.RoundClockVisible && !session.HandleMenuKey(KeyCode.F7),
                        "Menu time and shortcuts cannot start the round clock");
                    session.StartRun();
                    Check(session.State == GameSession.RunState.Refilling && session.RoundClockText == "00:00",
                        "A new round starts at zero during its opening refill");
                    session.Tick(.5f);
                    session.Tick(.75f);
                    Check(session.ElapsedRoundSeconds == 1.25 && session.RoundClockText == "00:01" && !session.RoundClockVisible,
                        "Refill and play accumulate fractional seconds while the display is off");
                    session.Tick(-2); session.Tick(0);
                    Check(session.ElapsedRoundSeconds == 1.25, "Zero or negative ticks cannot change elapsed time");
                    session.Pause();
                    session.Tick(90);
                    Check(session.HandleMenuKey(KeyCode.F7) && MetronomeSettings.RoundClockEnabled &&
                        !MetronomeSettings.VisualEnabled && !session.RoundClockVisible && session.ElapsedRoundSeconds == 1.25,
                        "Pause freezes the clock and allows enabling its display independently");
                    session.Resume();
                    Check(session.RoundClockVisible && !session.HandleMenuKey(KeyCode.F7),
                        "Resume shows accumulated time and protects the clock from gameplay shortcuts");
                    session.Tick(.75f);
                    Check(session.RoundClockText == "00:02", "Resume preserves fractional progress");
                    var state = typeof(GameSession).GetProperty(nameof(GameSession.State));
                    foreach (var stopped in new[] { GameSession.RunState.Dying, GameSession.RunState.GameOver })
                    {
                        state.SetValue(session, stopped);
                        session.Tick(.01f);
                        Check(session.ElapsedRoundSeconds == 2 && !session.RoundClockVisible,
                            "Fatal defeat and game-over presentation stop and hide the clock");
                    }
                    state.SetValue(session, GameSession.RunState.MainMenu);
                    session.StartRun();
                    Check(session.RoundClockText == "00:00" && session.RoundClockVisible && MetronomeSettings.RoundClockEnabled,
                        "Starting another round resets elapsed time while retaining clock visibility");
                }
                finally
                {
                    if (session.IsPaused) session.Resume();
                    if (!Application.isPlaying) Invoke(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                    MetronomeSettings.RoundClockEnabled = false;
                }
            });
        }

        private static void CheckLayout()
        {
            foreach (var screen in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540),
                new Vector2(1206, 2622), new Vector2(2622, 1206) })
            foreach (bool mobile in new[] { false, true })
            {
                float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
                var safe = new Rect(16, screen.y * .03f, screen.x - 32, screen.y * .9f);
                var bounds = new Rect(safe.x / scale, (screen.y - safe.yMax) / scale, safe.width / scale, safe.height / scale);
                var entry = GameSession.MetronomeMenuButtonRect(screen, safe, scale, mobile);
                var panel = GameSession.MetronomeMenuRect(screen, safe, scale, mobile);
                Check(bounds.Contains(entry.min) && bounds.Contains(entry.max) && bounds.Contains(panel.min) && bounds.Contains(panel.max),
                    "Menu entry and options panel remain within desktop and phone safe areas");
                for (int i = 0; i < 4; i++)
                {
                    var row = GameSession.MetronomeMenuRowRect(panel, i);
                    Check(row.height > 0 && panel.Contains(row.min) && panel.Contains(row.max) &&
                        (i == 0 || row.yMin > GameSession.MetronomeMenuRowRect(panel, i - 1).yMax),
                        "Each metronome switch has a distinct touch target inside the panel");
                }
                foreach (bool beatVisible in new[] { false, true })
                {
                    var clock = GameSession.RoundClockRect(screen, safe, scale, beatVisible);
                    var beat = GameSession.DevelopmentMetronomeRect(screen, safe, scale, beatVisible);
                    Check(bounds.Contains(clock.min) && bounds.Contains(clock.max) &&
                        (!beatVisible || !clock.Overlaps(beat)),
                        "The round clock fits the safe area without overlapping the enabled beat counter");
                }
                Rect field = screen.y > screen.x * 1.4f ?
                    new Rect(0, screen.y * .14f, screen.x, screen.y * .8f) :
                    new Rect(screen.x * .25f, screen.y * .02f, screen.x * .5f, screen.y * .96f);
                foreach (bool clockVisible in new[] { false, true })
                foreach (bool beatVisible in new[] { false, true })
                {
                    var overlays = DevelopmentOverlayLayout.Calculate(screen, safe, scale, field, clockVisible, beatVisible);
                    var blocked = new Rect(field.x / scale, (screen.y - field.yMax) / scale, field.width / scale, field.height / scale);
                    foreach (var rect in new[] { clockVisible ? overlays.Clock : default, beatVisible ? overlays.Metronome : default })
                    {
                        if (rect == default) continue;
                        Check(rect.width > 0 && rect.height > 0 && bounds.Contains(rect.min) && bounds.Contains(rect.max) &&
                            !rect.Overlaps(blocked), "Development readouts fit unused safe-area gutters outside all playfield artwork: " + screen);
                    }
                    Check(!clockVisible || overlays.Clock.width > 0, "Enabled clock has a visible gutter slot");
                    Check(!beatVisible || overlays.Metronome.width > 0, "Enabled metronome has a visible gutter slot");
                    Check(!clockVisible || !beatVisible || !overlays.Clock.Overlaps(overlays.Metronome),
                        "Development readouts remain separate in narrow, portrait and landscape gutters");
                }
            }
        }
        private static bool Activate(GameSession session, Vector2 point, Vector2 screen, Rect safe, bool mobile) =>
            (bool)typeof(GameSession).GetMethod("TryActivateMetronomeMenu", Hidden).Invoke(session, new object[] { point, screen, safe, mobile });
        private static void Invoke(GameSession session, string method) => typeof(GameSession).GetMethod(method, Hidden).Invoke(session, null);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Metronome options check failed: " + message); }
    }
}
