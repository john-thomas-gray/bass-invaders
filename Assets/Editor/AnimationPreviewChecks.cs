using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class AnimationPreviewChecks
    {
        private const string Pending = "CandyCruisers.AnimationPreviewChecks";
        private static int index;
        private static bool preparing;
        private static double started;
        private static string leaderboard;
        private static string runtimeError;
        private static bool triggerChecked;
        private static int transportPhase;
        private static double transportSince;
        private static float heldElapsed, unscaledStart;
        private static GameSession loopSession;
        private static bool observing;
        private static int magicShots, deflections, clears, returns;
        private static Color? returnTint;
        private static bool sawQuietDash, sawWaveGain, sawDisguiseHold;
        private static string fleetBefore;
        private static int introPreference;
        private static bool hadIntroPreference;
        private static readonly System.Collections.Generic.HashSet<MenuIntroAnimation.Entrance> menuEntrances = new();
        private static readonly AnimationPreviewStage.Animation[] Animations =
            (AnimationPreviewStage.Animation[])Enum.GetValues(typeof(AnimationPreviewStage.Animation));

        static AnimationPreviewChecks()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (SessionState.GetBool(Pending, false) && (type == LogType.Exception || type == LogType.Error))
                    runtimeError = message;
            };
        }

        [MenuItem("Candy Cruisers/Run Animation Preview Checks")]
        public static void Run()
        {
            AnimationPreviewWindow.CreateScene();
            Check(!EditorBuildSettings.scenes.Any(scene => scene.path == AnimationPreviewStage.ScenePath),
                "Preview scene stays out of player builds");
            EditorSceneManager.OpenScene(AnimationPreviewStage.ScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false) || !Application.isPlaying) return;
            try
            {
                Application.runInBackground = true;
                if (runtimeError != null) throw new Exception(runtimeError);
                if (started == 0) started = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - started > 360) throw new Exception("Preview checks timed out");
                var stage = AnimationPreviewStage.Instance;
                if (stage == null || !stage.Ready) return;
                if (index >= Animations.Length) { CheckTransport(stage); return; }
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                if (!preparing)
                {
                    if (index == 0)
                    {
                        leaderboard = LeaderboardSnapshot();
                        hadIntroPreference = PlayerPrefs.HasKey(GameSession.MenuIntroSeenKey);
                        introPreference = PlayerPrefs.GetInt(GameSession.MenuIntroSeenKey, 0);
                    }
                    stage.Selected = Animations[index];
                    stage.Level = 19;
                    stage.Speed = 2;
                    stage.Tempo = 120;
                    stage.CycleSeconds = 30;
                    stage.Loop = false;
                    stage.Sound = false;
                    stage.MenuEntrance = MenuIntroAnimation.Entrance.AllVariants;
                    stage.Replay();
                    preparing = true;
                    triggerChecked = false;
                    observing = false;
                    return;
                }
                if (!observing)
                {
                    observing = true; magicShots = deflections = clears = returns = 0; returnTint = null;
                    sawQuietDash = sawWaveGain = sawDisguiseHold = false;
                    menuEntrances.Clear();
                    if (stage.Selected == AnimationPreviewStage.Animation.LifeGain)
                    {
                        var previewClock = stage.Grid.GetComponent<GameplayMusicPlayer>();
                        previewClock.Source.Stop();
                        Check(previewClock.BeatClockRunning, "Preview beats do not depend on a hardware audio voice staying active");
                    }
                    stage.Player.ShotAccepted += () => { if (stage.Player.Tongue.IsMagic) magicShots++; };
                    stage.Player.Tongue.Deflected += () => deflections++;
                    stage.Grid.ColorCleared += _ => clears++;
                    stage.Player.Tongue.RetractionStarted += () =>
                    {
                        returnTint = !stage.Player.Tongue.IsDeflected && stage.Player.MagicCharges == 0 ?
                            stage.Player.Tongue.GetComponent<LineRenderer>().startColor : (Color?)null;
                    };
                    stage.Player.ShotCompleted += _ =>
                    {
                        if (returnTint.HasValue && stage.Player.ReadyColor.HasValue)
                        {
                            Check(Vector4.Distance(returnTint.Value, EnemyPalette.Get(stage.Player.ReadyColor.Value)) < .01f,
                                "Retracting tongue previews the actual next player color");
                            returns++;
                        }
                    };
                    if (stage.Selected == AnimationPreviewStage.Animation.EnemySpawn || stage.Selected == AnimationPreviewStage.Animation.EnemyDeath)
                    {
                        var enemies = stage.Grid.GetComponentsInChildren<GridEnemy>();
                        foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                        {
                            Check(enemies.Any(e => e.Color == color), "Fleet includes every enemy color");
                            if (color == EnemyColor.Orange) continue;
                            Check(enemies.Count(e => e.Color == color && e.IsSpecial) == 3 &&
                                enemies.Any(e => e.Color == color && !e.IsSpecial),
                                "Fleet includes basics and complete special trios: " + color);
                        }
                    }
                    if (stage.Selected == AnimationPreviewStage.Animation.StartGame)
                        Check(stage.Session.State == GameSession.RunState.MainMenu &&
                            stage.Session.MenuTitleIntroElapsed >= GameSession.MenuWordIntroDuration,
                            "Start Game starts from the fully arrived idle menu");
                }
                if (stage.Elapsed < .8f) return;
                if (stage.Selected == AnimationPreviewStage.Animation.MenuArrival &&
                    stage.Session.MenuTitleIntroElapsed >= MenuIntroAnimation.CharacterStart &&
                    stage.Session.MenuTitleIntroElapsed < MenuIntroAnimation.Duration)
                    menuEntrances.Add(stage.Session.MenuEntrance);
                if (stage.Selected == AnimationPreviewStage.Animation.GreenDash)
                    foreach (var enemy in stage.Grid.GetComponentsInChildren<GridEnemy>())
                    {
                        var lines = enemy.GetComponentsInChildren<LineRenderer>();
                        sawQuietDash |= lines.Any(line => line.name.StartsWith("Green dash trail") && line.enabled) &&
                            !lines.Any(line => line.name.StartsWith("Green surge") && line.enabled);
                    }
                sawWaveGain |= stage.Player.LifeIcons.Gain.Visible && stage.Session.ClearCelebration.Active;
                if (stage.Selected == AnimationPreviewStage.Animation.YellowImitation)
                    sawDisguiseHold |= stage.Grid.GetComponentsInChildren<EnemyAbilities>().Any(ability =>
                        ability.IsDisguised && !ability.IsTransforming && Vector4.Distance(
                            ability.GetComponent<GridEnemy>().Visuals.Body.color, EnemyPalette.Get(EnemyColor.Yellow)) > .1f);
                Check(stage.Session.PresentationPreview && stage.Session.BlocksGameplayInput, "Preview does not accept gameplay input");
                var music = stage.Grid.GetComponent<GameplayMusicPlayer>();
                Check(music.PreviewTime.HasValue && Mathf.Abs(music.BeatPosition - stage.Elapsed * 2) < .001f &&
                    music.Source.clip.name == "Animation Preview Clock" && music.BeatClockRunning && music.Source.volume == 0,
                    $"Preview clock follows scaled time without playing a real track: {stage.Selected}, elapsed={stage.Elapsed}, beat={music.BeatPosition}, playing={music.Source.isPlaying}, clip={music.Source.clip?.name}, volume={music.Source.volume}");
                if (!triggerChecked && stage.Selected == AnimationPreviewStage.Animation.LifeLoss)
                    Check(stage.Player.ExtraLives == PlayerMovement.MaxExtraLives - 1 && stage.Player.LifeIcons.Active,
                        "Life loss begins without playing through a run");
                if (!triggerChecked && stage.Selected == AnimationPreviewStage.Animation.ComboBreak)
                    Check(stage.Session.ComboBreak.Active, "Combo break uses the live animation");
                if (stage.Selected == AnimationPreviewStage.Animation.GameOver)
                {
                    if (stage.Elapsed < .6f + stage.Session.GameOverBlackoutSeconds +
                        PlayerMeltDeath.Duration + GameSession.GameOverTitleFadeSeconds) return;
                    Check(stage.Session.GameOverTitleReady && stage.Session.LastLeaderboardRank == 0,
                        "Full death sequence completes without recording a score");
                    Check(LeaderboardSnapshot() == leaderboard, "Preview leaves leaderboard entries unchanged");
                }
                if (stage.Selected == AnimationPreviewStage.Animation.ColorClear)
                    Check(stage.Grid.IsColorCleared(stage.Color), "Color-clear preview actually clears its enemy group");
                if (!triggerChecked && stage.Selected == AnimationPreviewStage.Animation.YellowImitation)
                    Check(stage.Grid.GetComponentsInChildren<EnemyAbilities>().Any(ability => ability.IsTransforming),
                        "Yellow preview starts a live transformation");
                if (!triggerChecked && stage.Selected == AnimationPreviewStage.Animation.RedShot)
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Any(missile => missile.Aimed),
                        "Red preview launches an aimed missile");
                triggerChecked = true;
                if (stage.Elapsed < stage.ScenarioSeconds - .1f) return;
                if (stage.Selected == AnimationPreviewStage.Animation.Tongue)
                    Check(stage.ShotsFired == 5 && returns == 5 && magicShots == 0,
                        "All five ordinary shot contexts finish through the real next-color path");
                if (stage.Selected == AnimationPreviewStage.Animation.MagicTongue)
                    Check(stage.ShotsFired == 5 && magicShots == 5 && clears >= 5 && stage.Player.MagicCharges == 0,
                        "Magic shots clear singles and multiple groups, then the final miss depowers magic");
                if (stage.Selected == AnimationPreviewStage.Animation.ShieldDeflection)
                    Check(stage.ShotsFired == 5 && deflections == 5, "All five shot contexts hit the live shield");
                if (stage.Selected == AnimationPreviewStage.Animation.PurpleSummon)
                    Check(stage.SummonsShown == 6, "Purple summons all six colors through the real spawner");
                if (stage.Selected == AnimationPreviewStage.Animation.GreenDash)
                    Check(sawQuietDash, "Special Green actually dashes separately from its regular movement surge");
                if (stage.Selected == AnimationPreviewStage.Animation.YellowImitation)
                    Check(stage.ImitationsShown == 18 && sawDisguiseHold,
                        "Regular and special Yellows repeat across all eligible target variants and retain their finished disguises");
                if (stage.Selected == AnimationPreviewStage.Animation.MissileGaze)
                    Check(stage.MissileHits == 4, "Four angled missiles actually collide with the player");
                if (stage.Selected == AnimationPreviewStage.Animation.MenuArrival)
                    Check(stage.Session.State == GameSession.RunState.MainMenu && !stage.Session.MenuArriving &&
                        menuEntrances.Count == 3 && !stage.Session.FirstLaunchIntro,
                        "Menu Arrival visibly plays all three entrances in one preview and never starts gameplay");
                if (stage.Selected == AnimationPreviewStage.Animation.StartGame)
                    Check(stage.Session.State == GameSession.RunState.Playing && stage.Grid.Model.Count > 0,
                        "Start Game reaches a real playing fleet");
                if (stage.Selected == AnimationPreviewStage.Animation.Idle)
                    Check(stage.Session.Progress.UsesScriptedProgression && !stage.Session.Progress.LevelUpPending &&
                        stage.Session.Progress.ActiveLevelProgressFraction > 0 && stage.Session.Progress.ActiveLevelProgressFraction < 1 &&
                        Enum.GetValues(typeof(EnemyColor)).Cast<EnemyColor>().Count(stage.Grid.HasColorClearBar) >= 2 &&
                        !stage.Player.CelebrationColor.HasValue, "Idle includes completed-fleet progress, powered bars and unmasked beat colors");
                if (stage.Selected == AnimationPreviewStage.Animation.WaveTransition || stage.Selected == AnimationPreviewStage.Animation.LifeGain)
                {
                    Check(stage.Session.State == GameSession.RunState.Playing && stage.Grid.Model.Count > 0,
                        "Wave preview completes its bars and spawns the next fleet");
                    Check(stage.Player.ExtraLives == PlayerMovement.MaxExtraLives && !stage.Player.LifeIcons.Gain.Active,
                        "Wave reward travels to the tally as part of the actual clear sequence");
                    Check(sawWaveGain, "The life-gain preview includes the reward-bar wave");
                }
                Debug.Log("Animation preview passed: " + stage.Selected);
                index++; preparing = false;
                if (index < Animations.Length) return;
                Check(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).Length == 1 &&
                    UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1,
                    "Replaying scenarios does not accumulate rigs or cameras");
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Animation preview check failed: " + message); }

        private static string LeaderboardSnapshot() => string.Join(";", LocalLeaderboard.Entries()
            .Select(entry => entry.Score + ":" + entry.Level + ":" + entry.Date));

        private static void CheckTransport(AnimationPreviewStage stage)
        {
            double now = EditorApplication.timeSinceStartup;
            if (transportPhase == 0)
            {
                stage.Selected = AnimationPreviewStage.Animation.Idle;
                stage.Speed = 1; stage.Replay(); transportPhase = 1;
            }
            else if (transportPhase == 1 && stage.Elapsed > .5f)
            {
                EditorApplication.isPaused = true;
                heldElapsed = stage.Elapsed; transportSince = now; transportPhase = 2;
            }
            else if (transportPhase == 2 && now - transportSince > .2)
            {
                Check(stage.Elapsed == heldElapsed, "Pause freezes the animation and beat clock");
                EditorApplication.Step(); transportPhase = 3;
            }
            else if (transportPhase == 3 && stage.Elapsed > heldElapsed)
            {
                Check(EditorApplication.isPaused && stage.Elapsed - heldElapsed < .2f, "Step advances one frame while paused");
                stage.Speed = .25f; EditorApplication.isPaused = false; transportSince = now; transportPhase = 4;
            }
            else if (transportPhase == 4 && now - transportSince > .2)
            {
                heldElapsed = stage.Elapsed; unscaledStart = Time.unscaledTime;
                transportSince = now; transportPhase = 5;
            }
            else if (transportPhase == 5 && now - transportSince > .5)
            {
                Check(Mathf.Abs((stage.Elapsed - heldElapsed) / (Time.unscaledTime - unscaledStart) - .25f) < .025f,
                    "Quarter-speed playback keeps the animation clock synchronized");
                stage.Speed = 2; stage.CycleSeconds = 2; stage.Loop = true;
                loopSession = stage.Session; transportPhase = 6;
            }
            else if (transportPhase == 6 && stage.Session != loopSession && stage.Elapsed > .2f)
            {
                Check(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).Length == 1,
                    "Looping resets the world instead of accumulating actors");
                stage.Selected = AnimationPreviewStage.Animation.EnemySpawn; stage.Loop = false;
                stage.Replay(); transportPhase = 7;
            }
            else if (transportPhase == 7 && stage.Elapsed > .8f)
            {
                fleetBefore = stage.FleetSignature;
                stage.Replay(); transportPhase = 8;
            }
            else if (transportPhase == 8 && stage.Elapsed > .8f)
            {
                Check(stage.FleetSignature != fleetBefore, "Fleet replays vary configurations while preserving every variant");
                Check(PlayerPrefs.HasKey(GameSession.MenuIntroSeenKey) == hadIntroPreference &&
                    PlayerPrefs.GetInt(GameSession.MenuIntroSeenKey, 0) == introPreference,
                    "No preview consumes or changes first-launch intro history");
                SessionState.SetBool(Pending, false);
                Debug.Log("All " + Animations.Length + " animation previews and transport checks passed: complete scenarios, pause, step, slow motion, looping, isolated clocks and protected scores.");
                EditorApplication.Exit(0);
            }
        }
    }
}
