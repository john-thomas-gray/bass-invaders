using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MenuChecks
    {
        public static void Run()
        {
            bool hadIntro = PlayerPrefs.HasKey(GameSession.MenuIntroSeenKey);
            int intro = PlayerPrefs.GetInt(GameSession.MenuIntroSeenKey, 0);
            bool hadHighestLevel = PlayerPrefs.HasKey(GameSession.HighestReachedLevelKey);
            int highestLevel = PlayerPrefs.GetInt(GameSession.HighestReachedLevelKey, RunProgress.MinLevel);
            var leaderboard = LocalLeaderboard.Entries();
            try
            {
                PlayerPrefs.SetInt(GameSession.MenuIntroSeenKey, 1);
                PlayerPrefs.DeleteKey(GameSession.HighestReachedLevelKey);
                LocalLeaderboard.Clear();
                CheckFirstLaunchIntro();
                RunChecks();
            }
            finally
            {
                if (hadIntro) PlayerPrefs.SetInt(GameSession.MenuIntroSeenKey, intro);
                else PlayerPrefs.DeleteKey(GameSession.MenuIntroSeenKey);
                if (hadHighestLevel) PlayerPrefs.SetInt(GameSession.HighestReachedLevelKey, highestLevel);
                else PlayerPrefs.DeleteKey(GameSession.HighestReachedLevelKey);
                if (leaderboard.Count > 0) LocalLeaderboard.Replace(leaderboard);
                else LocalLeaderboard.Clear();
                PlayerPrefs.Save();
            }
        }

        private static void RunChecks()
        {
            MenuBacklightChecks.Run();
            PlayerSlimeChecks.Run();
            var menuKeys = (KeyCode[])typeof(GameSession).GetField("MenuKeys",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            Check(menuKeys.Contains(KeyCode.Space), "Menu input polls Space");
            Check(GameSession.GuiScaleFor(false, 1179, 2556) == 1, "Desktop builds keep the original interface scale");
            Check(GameSession.GuiScaleFor(true, 390, 844) == GameSession.SmallPhoneGuiScale,
                "Compact phone screens still enlarge the interface");
            Check(GameSession.GuiScaleFor(true, 750, 1334) == GameSession.MediumPhoneGuiScale,
                "Mid-density phone screens use the medium interface scale");
            Check(GameSession.GuiScaleFor(true, 1179, 2556) == GameSession.LargePhoneGuiScale,
                "Dense iPhone-class screens use the large interface scale");
            CheckPauseButtonPlatform();
            CheckMainMenuTitle();
            CheckMenuEyeTrackingDelay();
            CheckMenuGazeAnimation();
            CheckMenuIntroSkip();
            CheckLevelSelectUnlock();
            CheckLevelSelectTouches();
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                session.StartLevel = RunProgress.MaxMenuStartLevel + 1;
                Check(session.StartLevel == RunProgress.MaxMenuStartLevel, "Menu start level clamps to the maximum");
                session.StartLevel = RunProgress.MinLevel - 1;
                Check(session.StartLevel == RunProgress.MinLevel, "Menu start level clamps to the minimum");
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var ability = red.GetComponent<EnemyAbilities>();
                player.RefreshColor();
                Check(player.Fire(), "Shot in flight before pause");
                tongue.Tick(.1f);
                float length = tongue.Length, cooldown = ability.CooldownRemaining;
                float timeScale = Time.timeScale;
                try
                {
                    Check(session.HandleMenuKey(KeyCode.P), "P activates pause");
                    session.Pause();
                    Check(session.IsPaused && Time.timeScale == 0 && player.ControlsLocked && tongue.Active,
                        "Pause freezes time and controls without canceling the tongue");
                    Check(!session.HandleMenuKey(KeyCode.Space) && session.IsPaused,
                        "Space does not resume or navigate away from pause");
                    ability.Tick(100);
                    session.Tick(100);
                    Check(tongue.Length == length && ability.CooldownRemaining == cooldown && !player.Fire(),
                        "Paused run retains tongue, ability timer and shot lock");
                    typeof(GameSession).GetMethod("ActivateMenuTouch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, new object[] { new Vector2(Screen.width / 2f, Screen.height / 2f) });
                    Check(!session.IsPaused && Time.timeScale == timeScale && tongue.Active &&
                        !player.ControlsLocked && !ability.Suspended, "Resume restores playing state and time scale");
                    Check(!session.HandleMenuKey(KeyCode.M) && !session.HandleMenuKey(KeyCode.LeftArrow) &&
                        !session.HandleMenuKey(KeyCode.Alpha4) && !session.HandleMenuKey(KeyCode.Space),
                        "Menu-only hotkeys do not consume gameplay movement or open menus");
                    session.HandleMenuKey(KeyCode.Escape);
                    Check(session.IsPaused && session.HandleMenuKey(KeyCode.KeypadEnter) && !session.IsPaused,
                        "Escape pause and keypad Enter resume work");
                    tongue.Cancel();
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    Check(session.State == GameSession.RunState.Refilling, "Clear starts refill");
                    session.Pause();
                    session.Tick(100);
                    Check(grid.Model.Count == 0 && session.State == GameSession.RunState.Refilling, "Pause freezes refill delay");
                    session.Resume();
                    Check(!session.HandleMenuKey(KeyCode.Space), "Space does not navigate during a wave transition");
                    Check(!grid.enabled && !grid.GetComponent<EnemyGridMovement>().enabled && !player.ControlsLocked,
                        "Resume restores refill suspension but allows player movement");
                    session.Pause();
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                    Check(Time.timeScale == timeScale, "Destroying a paused session restores global time");
                }
                finally { Time.timeScale = timeScale; }
            });
            foreach (var startKey in new[] { KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying)
                    {
                        InvokeLifecycle(session, "OnEnable");
                    }
                    PlayerPrefs.SetInt(GameSession.HighestReachedLevelKey, 12);
                    InvokeLifecycle(session, "Start");
                    session.Tick(GameSession.MenuWordIntroDuration);
                    Check(session.State == GameSession.RunState.MainMenu, "Empty scene enters the main menu");
                    Check(session.MainMenuLevelSelectVisible, "Level select appears once the main menu has settled");
                    Check(session.HandleMenuKey(KeyCode.Alpha9) && session.StartLevel == 9, "Typing one digit selects that start level");
                    Check(session.HandleMenuKey(KeyCode.Alpha9) && session.StartLevel == 12,
                        "Typing above the all-time level clamps to the highest reached level");
                    Check(session.HandleMenuKey(KeyCode.Alpha0) && session.StartLevel == RunProgress.MinLevel,
                        "Typing after two digits starts a fresh capped menu entry");
                    Check(session.HandleMenuKey(KeyCode.Keypad7) && session.StartLevel == 7, "Numeric keypad digits continue typed level entry");
                    session.StartLevel = 11;
                    Check(session.HandleMenuKey(KeyCode.RightArrow) && session.StartLevel == 12, "Right increases starting level");
                    Check(session.HandleMenuKey(KeyCode.RightArrow) && session.StartLevel == 12, "Right stops at the all-time level");
                    Check(session.HandleMenuKey(KeyCode.LeftArrow) && session.StartLevel == 11, "Left decreases starting level");
                    Check(!session.HandleMenuKey(KeyCode.R) && !session.HandleMenuKey(KeyCode.M), "Unavailable menu actions are ignored");
                    session.StartLevel = 12;
                    Check(session.HandleMenuKey(startKey), startKey + " starts the selected level");
                    Check(session.MenuArriving && session.BlocksGameplayInput && grid.Model.Count == 0,
                        "Menu arrival locks input and leaves the fleet empty");
                    Check(session.MenuTitleIntroElapsed == GameSession.MenuWordIntroDuration,
                        "Starting from the settled menu preserves the completed entrance");
                    session.Tick(GameSession.MenuArrivalSeconds * .5f);
                    Check(session.HandleMenuKey(startKey), "Repeated start keys are consumed during arrival");
                    Check(session.State == GameSession.RunState.MainMenu && session.MenuArrivalProgress == .5f,
                        "Repeated start keys do not restart the arrival");
                    session.Tick(GameSession.MenuArrivalSeconds * .5f);
                    Check(session.Progress.Level == 12 && session.Progress.Defeated == session.Progress.PreviousThreshold &&
                        session.Progress.Score == 0, "Play starts at the selected level without score");
                    Check(session.State == GameSession.RunState.Refilling && grid.Model.Count == 0,
                        "Selected level start keeps the empty musical opening beat");
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    // This synchronous menu fixture advances session time, not real audio time.
                    // Real-playback arrival checks cover the musical opening separately.
                    music.StopPlayback();
                    session.Tick(music.BeatDuration + .001f);
                    Check(session.State == GameSession.RunState.Playing &&
                        grid.Model.Count == session.Progress.BatchEnemies, "Selected level start spawns that level's batch size");
                    Check(grid.Model.Count == 6 * RunProgress.WideRowWidth,
                        "Level twelve opening batch uses six six-wide rows");
                    Check(grid.GetComponent<EnemyRowSpawner>().UnlockedColors().All(color => RunProgress.IsUnlocked(color, 12)),
                        "Selected start level drives opening eligibility");
                }
                finally
                {
                    session.StartLevel = RunProgress.MinLevel;
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
            Debug.Log("Menu checks passed: pause, resume, start-level selection, in-flight tongue, cooldowns, refill state and global-time cleanup.");
        }
        private static void CheckMainMenuTitle()
        {
            foreach (var size in new[] { new Vector2(24, 17), new Vector2(42, 30), new Vector2(64, 46) })
            {
                var perched = GameSession.PerchedPlayerRect(200, 300, size);
                Check(perched.size == size && Mathf.Approximately(perched.center.x, 200) &&
                    Mathf.Approximately(perched.yMax, 300),
                    "Menu slime preserves gameplay dimensions with its feet on the title");
            }
            Check(GameSession.MenuArrivalFadeProgressFor(GameSession.MenuTakeoffSeconds * .9f) == 0 &&
                GameSession.MenuArrivalFadeProgressFor(GameSession.MenuTakeoffSeconds + .01f) > 0,
                "Main menu fade waits until the slime has jumped clear of the title");
            var jumpResting = GameSession.PerchedPlayerRect(200, 300, new Vector2(42, 30));
            var crouch = GameSession.MenuJumpRect(jumpResting, .08f);
            var stretch = GameSession.MenuJumpRect(jumpResting, .82f);
            var airborne = GameSession.MenuJumpRect(jumpResting, 1);
            Check(crouch.height < jumpResting.height && crouch.yMax < jumpResting.yMax,
                "Menu takeoff starts with an upward squash");
            Check(stretch.height > jumpResting.height && airborne.yMax < jumpResting.yMax - jumpResting.height * .55f,
                "Menu takeoff stretches upward before the fade begins");
            Check(PlayerSlimeVisual.OpposingLean(4) > 0 && PlayerSlimeVisual.OpposingLean(-4) < 0,
                "Slime leans its top opposite horizontal travel");
            foreach (var size in new[] { new Vector2(290, 625), new Vector2(540, 960), new Vector2(1280, 720) })
            {
                var resting = GameSession.MinimalTitleRect(size.x, size.y, 0);
                var peak = GameSession.MinimalTitleRect(size.x, size.y, 1);
                Check(peak.width > resting.width && peak.yMin < resting.yMin && peak.xMin >= 0 && peak.xMax <= size.x,
                    "Beat title grows upward without leaving the screen");
                var subtitleRect = GameSession.MinimalSubtitleRect(size.x, size.y);
                var subtitle = new GUIStyle { font = Resources.Load<Font>("Fonts/Bungee-Regular"), fontSize = 24,
                    wordWrap = false, padding = new RectOffset() };
                GameSession.FitMainMenuSubtitle(subtitle, subtitleRect);
                Check(subtitleRect.yMin >= resting.center.y + 26 && subtitleRect.yMin < peak.yMax && subtitleRect.yMax < size.y &&
                    subtitle.CalcSize(new GUIContent(GameSession.MainMenuSubtitle)).x <= subtitleRect.width,
                    "Larger subtitle fits closely beneath the title's visible lettering");
            }
            Check(GameSession.MainMenuTitle == "BASS INVADERS",
                "Main menu title uses the requested top line");
            Check(GameSession.MainMenuSubtitle == "THE RHYTHM IS OUT THERE",
                "Main menu subtitle uses the requested second line");
            CheckMenuIntro();
            Check(GameSession.MainMenuAnimatedWordCount == 7 &&
                GameSession.MainMenuWordRevealProgress(0, .1f) == 1 &&
                GameSession.MainMenuWordRevealProgress(1, .1f) == 1 &&
                GameSession.MainMenuWordRevealProgress(2, MenuIntroAnimation.TitleSeconds) == 0 &&
                GameSession.MainMenuWordRevealProgress(6, GameSession.MenuWordIntroDuration) > .999f,
                "The whole title descends before the subtitle reveals together");
            Check(!SameColor(GameSession.MainMenuSubtitleBeatColor(0), GameSession.MainMenuSubtitleBeatColor(1)) &&
                Mathf.Abs(GameSession.MainMenuSubtitleBeatColor(2, .4f).a - .4f) < .0001f,
                "Main menu subtitle changes color on the beat while preserving opacity");
            var firstCharacterColor = GameSession.MainMenuCharacterColor(0f);
            Check(SameColor(firstCharacterColor, GameSession.MainMenuCharacterColor(.35f)) &&
                !SameColor(firstCharacterColor, Color.white) &&
                !SameColor(firstCharacterColor, GameSession.MainMenuCharacterColor(1f)) &&
                SameColor(GameSession.MainMenuCharacterColor(1f), new Color(1f, .025f, .04f)),
                "Menu character holds a saturated color through the pulse and switches directly to the next color");
            var characterBody = new Rect(40, 20, 40, 30);
            var desktopEyeTarget = GameSession.MenuEyeTargetFor(false, true, new Vector2(120, 160), 2, 90, characterBody);
            Check(desktopEyeTarget.HasValue && desktopEyeTarget.Value == new Vector2(60, 10) &&
                !GameSession.MenuEyeTargetFor(true, true, new Vector2(120, 160), 2, 90, characterBody).HasValue &&
                !GameSession.MenuEyeTargetFor(false, false, new Vector2(120, 160), 2, 90, characterBody).HasValue,
                "Menu eyes only track a computer mouse");
            foreach (float scale in new[] { 1f, 2f, 3f })
            foreach (float x in new[] { 0f, characterBody.center.x, 200f })
            {
                Vector2 MouseAt(float y) => new Vector2(x, 90 - y) * scale;
                Check(GameSession.MenuEyeTargetFor(false, true, MouseAt(characterBody.center.y - 1), scale, 90, characterBody).HasValue &&
                    !GameSession.MenuEyeTargetFor(false, true, MouseAt(characterBody.center.y), scale, 90, characterBody).HasValue &&
                    !GameSession.MenuEyeTargetFor(false, true, MouseAt(characterBody.center.y + 1), scale, 90, characterBody).HasValue,
                    "Menu eyes follow only above the character's midsection, across cursor columns and display scales");
            }
            var eye = new GameObject("Pupil", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            var mouth = new GameObject("Smile", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            try
            {
                Check(GameSession.IsMenuEyePiece(eye) && !GameSession.IsMenuEyePiece(mouth),
                    "Menu mouse following is limited to pupil pieces");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eye.gameObject);
                UnityEngine.Object.DestroyImmediate(mouth.gameObject);
            }
            CheckMenuEyeDirections();
            var font = Resources.Load<Font>("Fonts/Bungee-Regular");
            Check(font != null, "Main menu title font is bundled");
            CheckMainMenuTitleLayout(new Vector2(540, 960), false, font);
            CheckMainMenuTitleLayout(new Vector2(390, 844), true, font);
            CheckMainMenuTitleLayout(new Vector2(1179, 2556), true, font);
        }

        private static void WithMenu(bool preview, Action<GameSession, EnemyGrid, PlayerMovement> check)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.PresentationPreview = preview;
                    session.Configure(player);
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                    InvokeLifecycle(session, "Start");
                    check(session, grid, player);
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void CheckFirstLaunchIntro()
        {
            PlayerPrefs.DeleteKey(GameSession.MenuIntroSeenKey);
            WithMenu(false, (session, grid, player) =>
            {
                Check(session.FirstLaunchIntro && session.MenuEntrance == MenuIntroAnimation.Entrance.TractorBeam,
                    "First launch always chooses the tractor beam");
                foreach (float age in new[] { 0f, MenuIntroAnimation.TitleSeconds + .2f,
                    MenuIntroAnimation.CharacterStart + .4f, MenuIntroAnimation.BeamCloseStart + .1f,
                    MenuIntroAnimation.BeamCloseStart + MenuIntroAnimation.BeamCloseSeconds + .1f })
                {
                    session.Tick(age - session.MenuTitleIntroElapsed);
                    foreach (var key in new[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter })
                        Check(session.HandleMenuKey(key), "First-launch start keys are consumed");
                    typeof(GameSession).GetMethod("ActivateMenuTouch", System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance).Invoke(session, new object[] { new Vector2(10, 10) });
                    session.BeginMenuArrival();
                    Check(Mathf.Abs(session.MenuTitleIntroElapsed - age) < .0001f && !session.MenuArriving &&
                        session.State == GameSession.RunState.MainMenu && grid.Model.Count == 0 && player.ControlsLocked && !player.Fire(),
                        "No keyboard, tap, click or departure request skips any first-launch phase, including beam closure and zip");
                }
                Check(!PlayerPrefs.HasKey(GameSession.MenuIntroSeenKey), "An interrupted first intro is not recorded as seen");
            });
            WithMenu(true, (session, grid, player) =>
            {
                Check(!session.FirstLaunchIntro, "Animation preview bypasses first-launch restrictions");
                foreach (var entrance in new[] { MenuIntroAnimation.Entrance.FlipRight,
                    MenuIntroAnimation.Entrance.SkidLeft, MenuIntroAnimation.Entrance.TractorBeam })
                {
                    session.SetMenuEntrancePreview(entrance);
                    Check(session.MenuEntrance == entrance && session.MenuTitleIntroElapsed == 0,
                        "Preview can restart each of the three entrances");
                    session.Tick(MenuIntroAnimation.Duration + 1);
                }
                Check(!PlayerPrefs.HasKey(GameSession.MenuIntroSeenKey), "Preview never consumes the real first-launch intro");
            });
            WithMenu(false, (session, grid, player) =>
            {
                Check(session.FirstLaunchIntro, "An interrupted intro remains mandatory next time");
                session.Tick(MenuIntroAnimation.Duration - .01f);
                Check(!PlayerPrefs.HasKey(GameSession.MenuIntroSeenKey), "Completion waits until the beam has zipped away");
                session.Tick(.02f);
                Check(PlayerPrefs.GetInt(GameSession.MenuIntroSeenKey, 0) == 1 && !session.MenuArriving,
                    "A completed first intro is saved without starting gameplay");
                session.HandleMenuKey(KeyCode.Space);
                Check(session.MenuArriving, "First-launch menu accepts Start once the whole intro finishes");
            });
            WithMenu(false, (session, grid, player) =>
            {
                Check(!session.FirstLaunchIntro, "A new session reads the persisted completed intro");
                session.HandleMenuKey(KeyCode.Return);
                Check(session.MenuTitleIntroElapsed == MenuIntroAnimation.Duration && !session.MenuArriving,
                    "Later launches retain the normal skip-to-menu behavior");
            });
        }

        private static void CheckMenuIntroSkip()
        {
            foreach (var key in new[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.None })
            foreach (float elapsed in new[] { 0f, MenuIntroAnimation.TitleSeconds * .6f,
                MenuIntroAnimation.TitleSeconds + .3f, MenuIntroAnimation.CharacterStart + .5f, MenuIntroAnimation.Duration })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                    InvokeLifecycle(session, "Start");
                    session.Tick(elapsed);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    var clip = music.Source.clip;
                    Func<bool> activate = () => key != KeyCode.None ? session.HandleMenuKey(key) :
                        (bool)typeof(GameSession).GetMethod("ActivateMenuTouch",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .Invoke(session, new object[] { new Vector2(Screen.width / 2f, Screen.height / 2f) });
                    Check(activate(), "Keyboard and shared tap/click activation are consumed");
                    if (elapsed < MenuIntroAnimation.Duration)
                    {
                        Check(session.State == GameSession.RunState.MainMenu && !session.MenuArriving &&
                            session.MenuTitleIntroElapsed == MenuIntroAnimation.Duration && grid.Model.Count == 0 &&
                            player.ControlsLocked && !player.Fire() && music.Source.clip == clip &&
                            MenuIntroAnimation.BeamOpacity(session.MenuTitleIntroElapsed) == 0,
                            "Skipping any intro phase settles the menu, clears the beam and leaves music/gameplay untouched");
                        session.Tick(.3f);
                        Check(!session.MenuArriving && session.State == GameSession.RunState.MainMenu,
                            "The skipped intro stays idle until a new activation");
                        Check(activate(), "A second tap, click or key press starts the game");
                    }
                    Check(session.MenuArriving && session.MenuArrivalProgress == 0,
                        "A settled menu starts the normal departure immediately");
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void CheckLevelSelectUnlock()
        {
            PlayerPrefs.DeleteKey(GameSession.HighestReachedLevelKey);
            LocalLeaderboard.Clear();
            Check(GameSession.HighestReachedLevel == RunProgress.MinLevel,
                "Highest reached level defaults to the first level");
            Check(!GameSession.MainMenuLevelSelectVisibleFor(1, GameSession.MenuWordIntroDuration, false) &&
                !GameSession.MainMenuLevelSelectVisibleFor(2, GameSession.MenuWordIntroDuration - .01f, false) &&
                !GameSession.MainMenuLevelSelectVisibleFor(2, GameSession.MenuWordIntroDuration, true) &&
                GameSession.MainMenuLevelSelectVisibleFor(2, GameSession.MenuWordIntroDuration, false),
                "Level select appears only after level two is unlocked and the startup animation has finished");
            Check(GameSession.SelectableMaxStartLevelFor(0) == RunProgress.MinLevel &&
                GameSession.SelectableMaxStartLevelFor(120) == RunProgress.MaxMenuStartLevel,
                "Selectable level cap remains within supported menu levels");
            GameSession.RecordReachedLevel(2);
            Check(GameSession.HighestReachedLevel == 2 && PlayerPrefs.GetInt(GameSession.HighestReachedLevelKey, 0) == 2,
                "Reaching level two persists the level select unlock");
            GameSession.RecordReachedLevel(1);
            Check(GameSession.HighestReachedLevel == 2, "Lower reached levels do not erase the all-time maximum");
            GameSession.RecordReachedLevel(120);
            Check(GameSession.HighestReachedLevel == RunProgress.MaxMenuStartLevel,
                "Persisted all-time level is clamped to the playable menu cap");
            PlayerPrefs.DeleteKey(GameSession.HighestReachedLevelKey);
            LocalLeaderboard.Replace(new[] { new LocalLeaderboard.Entry(12345, 7, "test") });
            Check(GameSession.HighestReachedLevel == 7, "Existing leaderboard levels seed the all-time unlock");
            LocalLeaderboard.Clear();
            PlayerPrefs.DeleteKey(GameSession.HighestReachedLevelKey);

            var font = Resources.Load<Font>("Fonts/Bungee-Regular");
            foreach (bool mobile in new[] { false, true })
            foreach (var screen in new[] { new Vector2(540, 960), new Vector2(390, 844), new Vector2(750, 1334),
                new Vector2(1206, 2622), new Vector2(2622, 1206), new Vector2(1280, 720) })
            {
                float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
                var size = screen / scale;
                var row = GameSession.MainMenuLevelSelectRect(size.x, size.y, mobile);
                var left = GameSession.MainMenuLevelSelectLeftButtonRect(row);
                var right = GameSession.MainMenuLevelSelectRightButtonRect(row);
                var value = GameSession.MainMenuLevelSelectValueRect(row);
                Check(row.xMin >= 0 && row.xMax <= size.x && row.yMin >= 0 && row.yMax <= size.y,
                    "Level select row stays inside desktop and phone menu canvases");
                Check(left.height == left.width && right.height == right.width && left.xMin == row.xMin &&
                    right.xMax == row.xMax && value.xMin > left.xMax && value.xMax < right.xMin,
                    "Level select lays out as two square arrow buttons with a centered number slot");
                var style = new GUIStyle { font = font, fontSize = Mathf.RoundToInt(row.height * .9f),
                    alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset() };
                while (style.fontSize > 12 && style.CalcSize(new GUIContent("99")).x > value.width) style.fontSize--;
                Check(style.font == font && style.CalcSize(new GUIContent("99")).x <= value.width,
                    "Level number uses the title font and fits the selector slot");
                Check(!mobile || left.width >= 44, "Phone selector buttons provide a generous square touch target");
                var start = left.center + new Vector2(5, -8);
                var end = left.center + new Vector2(-5, 0);
                var transform = Matrix4x4.Scale(new Vector3(scale, scale, 1)) * GameSession.MenuLineTransform(start, end);
                Check(Vector2.Distance(transform.MultiplyPoint3x4(Vector3.zero), start * scale) < .001f &&
                    Vector2.Distance(transform.MultiplyPoint3x4(Vector3.right * Vector2.Distance(start, end)), end * scale) < .001f,
                    "Arrow endpoints stay inside their button after the mobile interface scale is applied");
            }
        }

        private static void CheckLevelSelectTouches()
        {
            foreach (bool mobile in new[] { false, true })
            foreach (var screen in new[] { new Vector2(540, 960), new Vector2(390, 844), new Vector2(750, 1334),
                new Vector2(1206, 2622), new Vector2(2622, 1206), new Vector2(1280, 720) })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                    PlayerPrefs.SetInt(GameSession.HighestReachedLevelKey, 7);
                    InvokeLifecycle(session, "Start");
                    session.Tick(GameSession.MenuWordIntroDuration);
                    session.StartLevel = 2;
                    float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
                    var row = GameSession.MainMenuLevelSelectRect(screen.x / scale, screen.y / scale, mobile);
                    var left = GameSession.MainMenuLevelSelectLeftButtonRect(row);
                    var right = GameSession.MainMenuLevelSelectRightButtonRect(row);
                    void Tap(Vector2 point)
                    {
                        var screenPoint = new Vector2(point.x * scale, screen.y - point.y * scale);
                        Check(Vector2.Distance(GameSession.MenuPointFromScreen(screenPoint, screen, mobile), point) < .001f,
                            "Touch pixels map to the same scaled coordinates used to draw the selector");
                        typeof(GameSession).GetMethod("ActivateMainMenuTouch", System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance).Invoke(session, new object[] { screenPoint, screen, mobile });
                    }
                    Tap(right.center);
                    Check(session.StartLevel == 3 && !session.MenuArriving, "Right arrow changes level without starting the game");
                    Tap(left.center);
                    Check(session.StartLevel == 2 && !session.MenuArriving, "Left arrow changes level without starting the game");
                    foreach (var point in new[] { row.center, new Vector2(left.xMax + 2, row.center.y),
                        new Vector2(right.xMin - 2, row.center.y) }) Tap(point);
                    if (mobile) Tap(new Vector2(row.xMin - 6, row.center.y));
                    Check(session.StartLevel == 2 && !session.MenuArriving,
                        "Number, gaps and mobile touch padding consume taps without starting gameplay");
                    session.StartLevel = 7;
                    Tap(right.center);
                    Check(session.StartLevel == 7 && !session.MenuArriving, "Upper level limit cannot fall through to start");
                    session.StartLevel = 1;
                    Tap(left.center);
                    Check(session.StartLevel == 1 && !session.MenuArriving, "Lower level limit cannot fall through to start");
                    Tap(new Vector2(row.center.x, row.yMax + 40));
                    Check(session.MenuArriving, "A tap outside the selector still starts the selected game");
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void CheckMenuEyeTrackingDelay()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                    session.PresentationPreview = true;
                    InvokeLifecycle(session, "Start");
                    session.Tick(GameSession.MenuWordIntroDuration);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    float measure = music.SecondsForBeats(GameplayMusicPlayer.BeatsPerMeasure);
                    Check(!session.MenuEyeTrackingReady &&
                        !GameSession.MenuEyeTargetFor(false, true, new Vector2(120, 160), 2, 90, new Rect(40, 20, 40, 30), false).HasValue,
                        "Menu eyes hold still when the animation first finishes");
                    session.Tick(Mathf.Max(0, measure * .75f));
                    Check(!session.MenuEyeTrackingReady, "Menu eyes wait through the first three beats of the measure");
                    session.Tick(Mathf.Max(.002f, measure * .25f + .002f));
                    Check(session.MenuEyeTrackingReady &&
                        GameSession.MenuEyeTargetFor(false, true, new Vector2(120, 160), 2, 90, new Rect(40, 20, 40, 30), true).HasValue,
                        "Menu eyes begin following the mouse after a full four-four measure");
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void CheckMenuIntro()
        {
            foreach (var canvas in new[] { new Vector2(290, 625), new Vector2(540, 960), new Vector2(1280, 720) })
            {
                var resting = GameSession.MinimalTitleRect(canvas.x, canvas.y, 0);
                Check(MenuIntroAnimation.TitleRect(resting, canvas.x, 0).yMax < 0,
                    "Title begins entirely above the screen");
                Check(MenuIntroAnimation.TitleRect(resting, canvas.x, MenuIntroAnimation.TitleSeconds) == resting,
                    "Fleet descent finishes at the existing title position");
                float right = MenuIntroAnimation.TitleRect(resting, canvas.x, MenuIntroAnimation.TitleTurnTime(1)).center.x;
                float left = MenuIntroAnimation.TitleRect(resting, canvas.x, MenuIntroAnimation.TitleTurnTime(2)).center.x;
                Check(right > resting.center.x && left < resting.center.x,
                    "Title reverses direction across the screen like the fleet");
                Check(MenuIntroAnimation.TitleRect(resting, canvas.x, MenuIntroAnimation.TitleFirstBeatSeconds * .5f) ==
                    MenuIntroAnimation.TitleRect(resting, canvas.x, 0),
                    "Title waits above the screen until the first menu beat");
                var corners = Enumerable.Range(0, MenuIntroAnimation.TitleTurnCount)
                    .Select(MenuIntroAnimation.TitleTurnTime).ToArray();
                float leftCenter = resting.width / 2 + 6;
                float rightCenter = canvas.x - leftCenter;
                float expectedDrop = 0;
                for (int leg = 0; leg < corners.Length - 1; leg++)
                {
                    float startBeat = (corners[leg] - MenuIntroAnimation.TitleFirstBeatSeconds) / MenuIntroAnimation.TitleBeatSeconds;
                    float endBeat = (corners[leg + 1] - MenuIntroAnimation.TitleFirstBeatSeconds) / MenuIntroAnimation.TitleBeatSeconds;
                    Check(Mathf.Abs(startBeat - Mathf.Round(startBeat)) < .0001f &&
                        Mathf.Abs(endBeat - Mathf.Round(endBeat)) < .0001f &&
                        Mathf.Round(endBeat - startBeat) >= 1,
                        "Every title movement starts and ends on whole menu beats");
                    var from = MenuIntroAnimation.TitleRect(resting, canvas.x, corners[leg]);
                    var to = MenuIntroAnimation.TitleRect(resting, canvas.x, corners[leg + 1]);
                    bool horizontal = Mathf.Abs(from.y - to.y) < .001f;
                    bool vertical = Mathf.Abs(from.center.x - to.center.x) < .001f;
                    Check(leg % 2 == 0 ? vertical && !horizontal : horizontal && !vertical,
                        "The title strictly alternates vertical drops and horizontal sweeps without same-height reversals");
                    if (vertical)
                    {
                        float drop = to.y - from.y;
                        if (expectedDrop == 0) expectedDrop = drop;
                        Check(Mathf.Abs(drop - expectedDrop) < .001f,
                            "Every vertical title drop covers the same distance");
                    }
                    if (horizontal && leg < corners.Length - 2)
                        Check(Mathf.Abs(Mathf.Min(from.center.x, to.center.x) - leftCenter) < .001f &&
                            Mathf.Abs(Mathf.Max(from.center.x, to.center.x) - rightCenter) < .001f,
                            "Every horizontal sweep before the final centering reaches the opposite screen edge");
                    if (leg == corners.Length - 2)
                        Check(Mathf.Abs(from.y - resting.y) < .001f && to == resting,
                            "The title reaches its final vertical position before one last horizontal move to center");
                    for (int sample = 0; sample <= 10; sample++)
                    {
                        var during = MenuIntroAnimation.TitleRect(resting, canvas.x,
                            Mathf.Lerp(corners[leg], corners[leg + 1], sample / 10f));
                        Check(horizontal ? Mathf.Abs(during.y - from.y) < .001f : Mathf.Abs(during.center.x - from.center.x) < .001f,
                            "Title does not cut diagonally between its corners");
                    }
                }
                float lastY = -10000;
                for (int i = 0; i <= 200; i++)
                {
                    float elapsed = MenuIntroAnimation.Duration * i / 200f;
                    var title = MenuIntroAnimation.TitleRect(resting, canvas.x, elapsed);
                    Check(title.y >= lastY && title.xMin >= 0 && title.xMax <= canvas.x,
                        "Title descends monotonically and stays horizontally inside the screen");
                    lastY = title.y;
                }
                var subtitle = GameSession.MinimalSubtitleRect(canvas.x, canvas.y);
                var flat = MenuIntroAnimation.TransformRect(subtitle, MenuIntroAnimation.SubtitleTransform(subtitle, 0));
                var tilted = MenuIntroAnimation.TransformRect(subtitle, MenuIntroAnimation.SubtitleTransform(subtitle,
                    MenuIntroAnimation.TitleSeconds + MenuIntroAnimation.SubtitleSeconds / 2));
                Check(flat.height == 0 && tilted.height > 0 && tilted.height < subtitle.height &&
                    Mathf.Abs(tilted.yMax - subtitle.yMax) < .001f,
                    "Subtitle tilts up from a flat baseline rather than sliding or revealing word by word");
                var finished = MenuIntroAnimation.TransformRect(subtitle, MenuIntroAnimation.SubtitleTransform(subtitle, MenuIntroAnimation.Duration));
                Check(Vector2.Distance(finished.position, subtitle.position) < .001f && Vector2.Distance(finished.size, subtitle.size) < .001f,
                    "Subtitle keeps its exact resting layout");
                var player = GameSession.PerchedPlayerRect(resting.center.x, resting.y + 20, new Vector2(30, 22));
                float lastAngle = 12;
                for (int i = 0; i <= 50; i++)
                {
                    var flip = MenuIntroAnimation.CharacterPose(player, canvas.x, MenuIntroAnimation.CharacterStart +
                        MenuIntroAnimation.CharacterSeconds * Mathf.Lerp(.65f, .94f, i / 50f), MenuIntroAnimation.Entrance.SkidLeft);
                    Check(flip.Rotation <= lastAngle + .001f, "Left-entry flip turns counterclockwise in downward-positive screen coordinates");
                    lastAngle = flip.Rotation;
                }
                for (int i = 0; i <= 100; i++)
                {
                    float age = Mathf.Lerp(MenuIntroAnimation.CharacterStart, MenuIntroAnimation.BeamCloseStart, i / 100f);
                    var pose = MenuIntroAnimation.CharacterPose(player, canvas.x, age, MenuIntroAnimation.Entrance.TractorBeam);
                    var beam = MenuIntroAnimation.BeamBounds(player, age);
                    foreach (var corner in new[] { pose.Body.min, pose.Body.max,
                        new Vector2(pose.Body.xMin, pose.Body.yMax), new Vector2(pose.Body.xMax, pose.Body.yMin) })
                    {
                        var point = pose.Transform.MultiplyPoint3x4(corner);
                        float core = MenuIntroAnimation.BeamWidthAt(beam, point.y) - beam.width * .09f;
                        Check(Mathf.Abs(point.x - beam.center.x) <= core / 2,
                            "The beam encloses the entire drifting, rotating character throughout descent, including at the top");
                    }
                }
                var openBeam = MenuIntroAnimation.BeamBounds(player, MenuIntroAnimation.BeamCloseStart);
                var closingBeam = MenuIntroAnimation.BeamBounds(player, MenuIntroAnimation.BeamCloseStart + MenuIntroAnimation.BeamCloseSeconds / 2);
                var closedBeam = MenuIntroAnimation.BeamBounds(player, MenuIntroAnimation.BeamCloseStart + MenuIntroAnimation.BeamCloseSeconds);
                var zippingBeam = MenuIntroAnimation.BeamBounds(player, MenuIntroAnimation.BeamCloseStart + MenuIntroAnimation.BeamCloseSeconds + MenuIntroAnimation.BeamZipSeconds / 2);
                Check(openBeam.width > closingBeam.width && closingBeam.width > closedBeam.width &&
                    Mathf.Abs(closedBeam.width - 2) < .001f && closingBeam.height == openBeam.height &&
                    zippingBeam.height < closedBeam.height && zippingBeam.y == closedBeam.y &&
                    MenuIntroAnimation.BeamBounds(player, MenuIntroAnimation.Duration).height < .001f,
                    "After landing the beam fans shut to a thin line, then zips upward completely out of view");
                foreach (var entrance in new[] { MenuIntroAnimation.Entrance.FlipRight, MenuIntroAnimation.Entrance.SkidLeft,
                    MenuIntroAnimation.Entrance.TractorBeam })
                {
                    Check(!MenuIntroAnimation.CharacterPose(player, canvas.x, MenuIntroAnimation.TitleSeconds, entrance).Visible,
                        "Slime waits for the title and subtitle before arriving");
                    var first = MenuIntroAnimation.CharacterPose(player, canvas.x, MenuIntroAnimation.CharacterStart, entrance);
                    Check(first.Visible && (entrance == MenuIntroAnimation.Entrance.FlipRight ? first.Body.xMin > canvas.x :
                        entrance == MenuIntroAnimation.Entrance.SkidLeft ? first.Body.xMax < 0 : first.Body.yMax < 0),
                        "Each entrance starts beyond its requested screen edge");
                    var final = MenuIntroAnimation.CharacterPose(player, canvas.x, MenuIntroAnimation.Duration, entrance);
                    Check(final.Body == player && final.Rotation == 0 && final.Visible,
                        "All three entrances finish at the same normal player size and position");
                    var previous = first;
                    for (int i = 1; i <= 500; i++)
                    {
                        var current = MenuIntroAnimation.CharacterPose(player, canvas.x,
                            MenuIntroAnimation.CharacterStart + MenuIntroAnimation.CharacterSeconds * i / 500, entrance);
                        Check(Vector2.Distance(current.Body.center, previous.Body.center) < canvas.x * .025f &&
                            Mathf.Abs(Mathf.DeltaAngle(current.Rotation, previous.Rotation)) < 10,
                            "Entrance segments join without position or rotation snaps");
                        previous = current;
                    }
                }
            }
            Check(MenuIntroAnimation.BeamOpacity(MenuIntroAnimation.CharacterStart) == 0 &&
                MenuIntroAnimation.BeamOpacity(MenuIntroAnimation.CharacterStart + .5f) > .9f &&
                MenuIntroAnimation.BeamOpacity(MenuIntroAnimation.Duration) == 0,
                "White tractor beam builds around the descent and disappears after landing");
        }

        private static void CheckMenuEyeDirections()
        {
            Check(!GameSession.MenuEntranceForwardGazeFor(MenuIntroAnimation.CharacterStart - .001f, false) &&
                GameSession.MenuEntranceForwardGazeFor(MenuIntroAnimation.CharacterStart, false) &&
                !GameSession.MenuEntranceForwardGazeFor(GameSession.MenuWordIntroDuration, false) &&
                !GameSession.MenuEntranceForwardGazeFor(MenuIntroAnimation.CharacterStart + .1f, true),
                "Menu entrance centers the pupils only while the character is coming in");
            foreach (var size in new[] { new Vector2(12, 12), new Vector2(24, 24), new Vector2(30, 18) })
            {
                var eye = new Rect(new Vector2(200, 300), size);
                var pupilSize = size * (.085f / .21f);
                var pupil = new Rect(eye.center - pupilSize / 2 - Vector2.up * size.y * (.04f / .21f), pupilSize);
                var centered = GameSession.MenuEyeFollowRect(pupil, eye, eye.center);
                Check(Vector2.Distance(centered.center, eye.center) < .0001f && centered.size == pupilSize,
                    "Cursor at eye level removes the authored upward pupil offset");
                var travel = (eye.size - pupilSize) * .4f;
                for (int degrees = 0; degrees < 360; degrees += 15)
                foreach (float distance in new[] { 1f, 45f, 180f, 10000f })
                {
                    float angle = degrees * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var cursor = eye.center + direction * distance;
                    var followed = GameSession.MenuEyeFollowRect(pupil, eye, cursor);
                    var expected = eye.center + Vector2.Scale(direction, travel) * Mathf.Clamp01(distance / 180f);
                    Check(Vector2.Distance(followed.center, expected) < .001f && followed.size == pupilSize,
                        "Menu pupils follow the full circle from eye level, including directly below");
                    var innerRadii = (eye.size - pupilSize) / 2;
                    var offset = followed.center - eye.center;
                    Check(Mathf.Pow(offset.x / innerRadii.x, 2) + Mathf.Pow(offset.y / innerRadii.y, 2) <= .641f,
                        "Menu pupils stay inside the eye with a margin at every cursor angle and distance");
                    var shifted = new Rect(pupil.position + new Vector2(3, 7), pupil.size);
                    Check(GameSession.MenuEyeFollowRect(shifted, eye, cursor) == followed,
                        "Existing gameplay gaze cannot bias the title-screen cursor tracking");
                }
            }
        }

        private static void CheckMenuGazeAnimation()
        {
            var eye = new Rect(200, 300, 24, 24);
            var gaze = new MenuGazeAnimation();
            Check(gaze.DirectionFor(eye) == Vector2.zero, "Menu resting gaze starts by looking directly at the player");
            gaze.Tick(.5f, eye.center + Vector2.right * 300);
            var tracking = gaze.DirectionFor(eye);
            gaze.Tick(1f / 60, null);
            var returning = gaze.DirectionFor(eye);
            Check(tracking.x > .99f && returning.x > .5f && returning.x < tracking.x && returning.y == 0,
                "Losing the mouse target eases out of the tracked gaze instead of snapping to rest");
            gaze.Tick(.6f, null);
            Check(gaze.DirectionFor(eye).magnitude < .003f, "Menu eyes settle back into direct eye contact");
            gaze.Reset();
            Check(gaze.DirectionFor(eye) == Vector2.zero, "Restarting the menu entrance clears the previous gaze");

            int centered = 0;
            bool up = false, left = false, right = false;
            var previous = Vector2.zero;
            for (int frame = 0; frame <= MenuGazeAnimation.IdleCycleSeconds * 60; frame++)
            {
                var direction = MenuGazeAnimation.IdleDirectionFor(frame / 60f);
                if (direction == Vector2.zero) centered++;
                up |= direction.y < -.7f;
                left |= direction.x < -.7f;
                right |= direction.x > .7f;
                Check(direction.magnitude <= 1 && Vector2.Distance(direction, previous) < .065f,
                    "Idle glances stay within the eyes and transition continuously, including the cycle boundary");
                previous = direction;
            }
            Check(centered > MenuGazeAnimation.IdleCycleSeconds * 60 * .75f && up && left && right,
                "Menu eyes spend most of their idle time facing the player, with occasional upward and side glances");

            foreach (bool mobile in new[] { false, true })
            {
                Vector2? expected = null;
                foreach (int frameRate in new[] { 30, 60, 120 })
                {
                    gaze.Reset();
                    var target = GameSession.MenuEyeTargetFor(mobile, true, new Vector2(600, 800), 1, 900,
                        new Rect(200, 300, 40, 30));
                    for (int frame = 0; frame < frameRate; frame++) gaze.Tick(1f / frameRate, target);
                    for (int frame = 0; frame < frameRate * 3 / 10; frame++) gaze.Tick(1f / frameRate, null);
                    var direction = gaze.DirectionFor(eye);
                    if (expected.HasValue)
                        Check(Vector2.Distance(direction, expected.Value) < .0001f,
                            "Desktop gaze returns and phone idle animation agree at 30, 60 and 120 frames per second");
                    expected = direction;
                    gaze.Tick(6.5f, null);
                    Check(gaze.DirectionFor(eye).y < -.79f,
                        "Upward idle glances continue with desktop mouse tracking disabled or phone touch input active");
                }
            }
        }

        private static void CheckMainMenuTitleLayout(Vector2 screen, bool mobile, Font font)
        {
            float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
            var canvas = screen / scale;
            float anchor = GameSession.MenuAnchorYFor(canvas.y, true);
            var rect = GameSession.MainMenuTitleRect(canvas.x, canvas.y, anchor);
            var titleStyle = new GUIStyle
            {
                font = font,
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
            titleStyle.normal.textColor = Color.white;
            var subtitleStyle = new GUIStyle(titleStyle) { fontSize = 18 };
            var titleRect = GameSession.MainMenuTitleLineRect(rect);
            var subtitleRect = GameSession.MainMenuSubtitleLineRect(rect);
            int titleSize = GameSession.FitMainMenuTitle(titleStyle, titleRect);
            int subtitleSize = GameSession.FitMainMenuSubtitle(subtitleStyle, subtitleRect);
            var titleContent = new GUIContent(GameSession.MainMenuTitle);
            var subtitleContent = new GUIContent(GameSession.MainMenuSubtitle);
            float titleHeight = titleStyle.CalcHeight(titleContent, titleRect.width);
            float subtitleHeight = subtitleStyle.CalcHeight(subtitleContent, subtitleRect.width);
            Check(titleStyle.font == font && titleStyle.normal.textColor == Color.white &&
                subtitleStyle.font == font && subtitleStyle.normal.textColor == Color.white,
                "Main menu title uses the combo number font in white");
            Check(rect.xMin >= 0 && rect.xMax <= canvas.x && rect.yMin >= 0 && rect.yMax <= canvas.y,
                "Main menu title stays on the visible menu canvas");
            Check(titleStyle.CalcSize(titleContent).x <= titleRect.width + .01f &&
                titleHeight <= titleRect.height + .01f && titleSize >= 20,
                "Main menu title fits on one large top line");
            Check(subtitleStyle.CalcSize(subtitleContent).x <= subtitleRect.width + .01f &&
                subtitleHeight <= subtitleRect.height + .01f && subtitleSize >= 11,
                "Main menu subtitle fits on one smaller second line");
            Check(titleStyle.wordWrap == false && subtitleStyle.wordWrap == false && titleSize > subtitleSize,
                "Main menu title is larger than the subtitle and neither line wraps");
            Check(titleRect.yMax <= subtitleRect.yMin + .01f && subtitleRect.yMax <= rect.yMax + .01f,
                "Main menu subtitle sits under the title within the title block");
            Check(rect.yMax <= anchor - 144 + .01f,
                "Main menu title clears the enemy preview row");
        }

        private static void CheckPauseButtonPlatform()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                    GameSession.ForceMobileMenuForTests = false;
                    Check(!PauseButtonVisible(session), "Desktop builds hide the on-screen pause button");
                    GameSession.ForceMobileMenuForTests = true;
                    Check(PauseButtonVisible(session), "Touch builds keep the on-screen pause button");
                    session.Pause();
                    Check(!PauseButtonVisible(session), "The pause button disappears while the pause menu is already open");
                }
                finally
                {
                    GameSession.ForceMobileMenuForTests = false;
                    if (session.IsPaused) session.Resume();
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static bool PauseButtonVisible(GameSession session) => (bool)typeof(GameSession)
            .GetProperty("PauseButtonVisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(session);

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Menu check failed: " + message); }
        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .0001f && Mathf.Abs(a.g - b.g) < .0001f &&
            Mathf.Abs(a.b - b.b) < .0001f && Mathf.Abs(a.a - b.a) < .0001f;
        private static void InvokeLifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(session, null);
    }
}
