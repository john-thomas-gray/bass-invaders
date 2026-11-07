using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class RuntimeGameplayChecks
    {
        private const string PendingKey = "CandyCruisers.RuntimeChecks";
        private static int phase;
        private static double started;
        private static PlayerMovement player;
        private static TongueShot tongue;
        private static EnemyGrid grid;
        private static Vector3 gridStart;
        private static GridEnemy retreatingEnemy;
        private static double gameOverAt;
        private static bool previewOnly;
        private static bool observedEmptyOpening;
        private static bool observedEmptyRestart;
        private static int matchedCount;
        private static int matchedWeight;
        private static float pausedTongueLength;
        private static int pausedSoundSample;
        private static float pausedSoundPitch;
        private static bool observedGameOverFade;
        private static bool observedDeathMusicSlowdown;
        private static bool liveShieldPrepared;
        private static GridEnemy liveShooter;

        static RuntimeGameplayChecks()
        {
            EditorApplication.update += Poll;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (!SessionState.GetBool(PendingKey, false)) return;
                // Restart reloads the authored catalog too. Preserve this mechanics fixture's
                // baseline before Start reserves its opening fleet, just as on initial entry.
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var session in root.GetComponentsInChildren<GameSession>(true))
                    {
                        session.LevelCatalog = null;
                        session.gameObject.AddComponent<RuntimeOpeningObserver>();
                    }
            };
        }

        public static void Run()
        {
            MetronomeCheckSettings.BeginRuntime(PendingKey);
            SessionState.SetBool("CandyCruisers.Tests.PreviousSpawnOverride", SpawnOverride.Enabled);
            SpawnOverride.Enabled = false;
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        public static void Preview()
        {
            SessionState.SetBool("CandyCruisers.AbilityPreview", true);
            Run();
        }

        public static void RunWithWindow()
        {
            var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            game.Show();
            game.Focus();
            Run();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            // Desktop focus can change while this unattended suite is running.
            // Explicit pause assertions below (phases 17-20) retain full control.
            if (phase < 15 || phase == 61 || phase == 81)
            {
                var activeSession = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (activeSession != null && activeSession.IsPaused) activeSession.Resume();
            }
            if (Time.timeSinceLevelLoad < 0.1f) return;
            try
            {
                if (phase > 0 && EditorApplication.timeSinceStartup - started > 60)
                    throw new Exception("Runtime gameplay phase " + phase + " timed out.");
                if (phase == 0)
                {
                    grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
                    // Keep the baseline mechanics fixture independent of edited level assets.
                    // Separate live level checks exercise configured palettes and bar transitions.
                    grid.GetComponent<GameSession>().LevelCatalog = null;
                    player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                    tongue = player.GetComponentInChildren<TongueShot>();
                    var framing = Camera.main.GetComponent<GameplayFraming>();
                    var background = GameObject.Find("Star Background")?.GetComponent<SpriteRenderer>();
                    Require(framing != null && framing.SolidBlackBackground && Camera.main.backgroundColor == Color.black &&
                        (background == null || !background.enabled), "Runtime gameplay background is solid black");
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    Require(music != null && !music.Source.playOnAwake && music.Source.spatialBlend == 0,
                        "Scene creates a nonspatial gameplay music player");
                    if (grid.GetComponent<GameSession>().OpeningWarningActive)
                        EpilepsyWarningChecks.RunRuntime(grid.GetComponent<GameSession>());
                    if (grid.GetComponent<GameSession>().State == GameSession.RunState.MainMenu)
                    {
                        Require(grid.Model.Count == 0 && player.ControlsLocked && !player.Fire() && !music.InGameplayRun && music.ShouldPlayMusic,
                            "Main menu starts without live gameplay and requests menu music when local tracks exist");
                        var menuClip = music.Source.clip;
                        if (menuClip == null)
                        {
                            Require(!music.Source.isPlaying, "Main menu stays silent when no local music is installed");
                            grid.GetComponent<GameSession>().StartRun();
                            ObserveOpening(grid.GetComponent<GameSession>());
                            return;
                        }
                        // The opening warning releases music before asynchronous audio loading finishes.
                        if (!music.Source.isPlaying && Time.timeSinceLevelLoad < GameSession.EpilepsyWarningSeconds + 5) return;
                        Require(music.ContainsTrack(menuClip.name) && music.Source.isPlaying,
                            "Main menu starts with local menu music");
                        int menuPosition = music.Source.timeSamples;
                        float menuBeat = music.BeatPosition;
                        grid.GetComponent<GameSession>().StartRun();
                        ObserveOpening(grid.GetComponent<GameSession>());
                        bool repeatsMenuSong = new SerializedObject(music).FindProperty("track").objectReferenceValue == menuClip;
                        Require(music.Source.clip == menuClip && music.Source.isPlaying && music.Source.loop == repeatsMenuSong &&
                            music.Source.timeSamples >= menuPosition && music.Source.timeSamples - menuPosition < menuClip.frequency / 10 &&
                            music.BeatPosition >= menuBeat && music.BeatPosition - menuBeat < .25f,
                            "The real menu-to-gameplay transition preserves the playing song, sample position and musical beat");
                        return;
                    }
                    if (grid.GetComponent<GameSession>().State == GameSession.RunState.Refilling)
                    {
                        Require(grid.Model.Count == 0 && !player.Fire() && !player.ControlsLocked && player.ReadyColor.HasValue &&
                            music.ShouldPlayMusic && (music.Source.clip == null || music.Source.isPlaying),
                            "Opening starts empty, with movement, a planned player color and optional local music");
                        if (!observedEmptyOpening)
                            ScreenCapture.CaptureScreenshot("TestResults/runtime-empty-opening.png");
                        observedEmptyOpening = true;
                        return;
                    }
                    Require(observedEmptyOpening, "Opening reserves an empty fleet before first spawn");
                    foreach (var visual in grid.GetComponentsInChildren<EnemyPresentation>())
                        Require(visual.IsPhasing, "Opening enemies arrive through rifts");
                    if (SessionState.GetBool("CandyCruisers.AbilityPreview", false))
                    {
                        previewOnly = true;
                        SessionState.SetBool("CandyCruisers.AbilityPreview", false);
                        PreparePreview();
                        started = gameOverAt = EditorApplication.timeSinceStartup;
                        phase = 13;
                        return;
                    }
                    Require(grid.Model.Count == 2 * RunProgress.StandardRowWidth && player.ReadyColor.HasValue, "Awake/Start initialize scene");
                    var initialMovement = grid.GetComponent<EnemyGridMovement>();
                    var openingPosition = grid.transform.position;
                    initialMovement.Tick(10);
                    Require(initialMovement.CurrentSpeed == 0 && grid.transform.position == openingPosition, "No Greens means a stationary opening fleet");
                    // Continue the retained dash-mode integration coverage after checking the new default.
                    initialMovement.UseGreenDashes = true;
                    var enemy = grid.GetComponentInChildren<GridEnemy>();
                    enemy.gameObject.SetActive(false);
                    Require(grid.Model.Count == 2 * RunProgress.StandardRowWidth - 1, "Disabled enemy unregisters");
                    enemy.gameObject.SetActive(true);
                    Require(grid.Model.Count == 2 * RunProgress.StandardRowWidth, "Re-enabled enemy registers once");
                    player.CancelPointer();
                    player.transform.position = new Vector3(2.9f, player.transform.position.y, 0);
                    Require(player.Fire() && !player.Fire(), "Runtime shot lock");
                    var sound = tongue.GetComponentInChildren<AudioSource>();
                    Require(sound != null && sound.isPlaying && sound.loop && sound.clip == grid.GetComponent<SoundEffects>().GetClip(SoundEffect.TongueWhistle),
                        "Real accepted fire starts the continuous whistle");
                    Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1,
                        "Gameplay has exactly one audio listener");
                    gridStart = grid.transform.position;
                    started = EditorApplication.timeSinceStartup;
                    phase = 1;
                }
                else if (phase == 1 && EditorApplication.timeSinceStartup - started > 0.2)
                {
                    ScreenCapture.CaptureScreenshot("TestResults/runtime-opening-rifts.png");
                    Require(tongue.Active && tongue.Length > 0, "Update extends tongue");
                    var voice = tongue.GetComponentInChildren<AudioSource>();
                    Require(voice.isPlaying && voice.loop && voice.pitch > 420f / ArcadeSoundClips.TongueFrequency && voice.volume > 0,
                        "Whistle remains audible and follows extension beyond the old clip duration");
                    grid.GetComponent<EnemyGridMovement>().Tick(.5);
                    Require(grid.transform.position != gridStart, "Fleet moves after a complete step interval");
                    // Freeze after the movement assertion so slow rendered frames cannot
                    // descend into extra rows before the separate matching assertion.
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    phase = 2;
                }
                else if (phase == 2 && !tongue.Active)
                {
                    Require(player.ReadyColor.HasValue && player.Fire(), "Update returns tongue and enables next shot");
                    tongue.Tick(10);
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    var target = grid.GetComponentsInChildren<GridEnemy>()
                        .Where(enemy => enemy.Color == player.ReadyColor.Value).OrderByDescending(enemy => enemy.Row).First();
                    var matchedDepths = grid.Model.MatchingDepths(target.Id, target.Color);
                    matchedCount = matchedDepths.Count;
                    matchedWeight = matchedCount >= 3 ? matchedDepths.Values.Sum() : matchedCount;
                    player.CancelPointer();
                    player.transform.position = new Vector3(target.transform.position.x, player.transform.position.y, 0);
                    Require(player.Fire(), "Aim matching shot at an enemy");
                    phase = 3;
                }
                else if (phase == 3 && !tongue.Active)
                {
                    Require(grid.Model.Count == 2 * RunProgress.StandardRowWidth - matchedCount && grid.GetComponentsInChildren<GridEnemy>().Length == 2 * RunProgress.StandardRowWidth - matchedCount,
                        "Runtime hit clears the contiguous group from scene and model: remaining=" + grid.Model.Count +
                        " expected=" + (2 * RunProgress.StandardRowWidth - matchedCount) + " playerX=" + player.transform.position.x);
                    Require(grid.GetComponent<GameSession>().Progress.Score == 100 * matchedWeight &&
                        grid.GetComponent<GameSession>().Progress.Defeated == matchedCount, "Real hits update score and progression once");
                    Require(player.ReadyColor.HasValue && player.Fire(), "Can refire after successful chain");
                    tongue.Tick(10);
                    player.enabled = false;
                    grid.GetComponent<EnemyGridMovement>().enabled = true;
                    phase = 4;
                }
                else if (phase == 4 && grid.Model.Count > 2 * RunProgress.StandardRowWidth - matchedCount)
                {
                    Require(grid.Model.Count == 3 * RunProgress.StandardRowWidth - matchedCount && grid.GetComponentsInChildren<GridEnemy>().Length == 3 * RunProgress.StandardRowWidth - matchedCount,
                        "Right sweep end descends and creates exactly five enemies");
                    for (int column = 0; column < RunProgress.StandardRowWidth; column++)
                        Require(grid.Model.At(column, 0) != null, "New top row is filled");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(enemy => enemy.Row == 0))
                        Require(enemy.GetComponent<EnemyAbilities>() != null, "Spawned enemies receive abilities");
                    Require(grid.GetComponentsInChildren<GridEnemy>().Count(enemy => enemy.Row > 0) == 2 * RunProgress.StandardRowWidth - matchedCount,
                        "Survivors descend without duplication");
                    phase = 5;
                }
                else if (phase == 5 && grid.Model.Count > 3 * RunProgress.StandardRowWidth - matchedCount)
                {
                    Require(grid.Model.Count == 4 * RunProgress.StandardRowWidth - matchedCount && grid.GetComponentsInChildren<GridEnemy>().Length == 4 * RunProgress.StandardRowWidth - matchedCount,
                        "Left sweep end adds exactly one more row");
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    var enemies = grid.GetComponentsInChildren<GridEnemy>();
                    var anchor = enemies.First(enemy => enemy.Column == 0 && enemy.Row == 0);
                    var bridge = enemies.First(enemy => enemy.Column == 0 && enemy.Row == 1);
                    retreatingEnemy = enemies.First(enemy => enemy.Column == 0 && enemy.Row == 2);
                    foreach (var enemy in enemies)
                        if (enemy != anchor && enemy != bridge && enemy != retreatingEnemy) enemy.gameObject.SetActive(false);
                    grid.SetColor(anchor.Id, EnemyColor.Blue);
                    grid.SetColor(bridge.Id, EnemyColor.Red);
                    grid.SetColor(retreatingEnemy.Id, EnemyColor.Blue);
                    Require(grid.ClearMatchingChain(bridge.Id, EnemyColor.Red) == 1, "Clear connecting bridge");
                    phase = 6;
                }
                else if (phase == 6 && retreatingEnemy.Row == 1)
                {
                    Require(grid.Model.Count == 2 && grid.Model.At(0, 1).Id == retreatingEnemy.Id,
                        "Disconnected enemy reconnects without being removed");
                    Require(Vector3.Distance(retreatingEnemy.transform.localPosition, grid.CellPosition(0, 1)) < 0.0001f,
                        "Retreat view follows logical cell");
                    var blueAbility = retreatingEnemy.GetComponent<EnemyAbilities>();
                    if (!liveShieldPrepared)
                    {
                        grid.SetColor(retreatingEnemy.Id, EnemyColor.Green);
                        blueAbility.Tick(0);
                        grid.SetColor(retreatingEnemy.Id, EnemyColor.Blue);
                        blueAbility.Tick(0);
                        liveShieldPrepared = true;
                    }
                    Require(!retreatingEnemy.IsSpecial, "Single-use shield check uses an ordinary Blue");
                    if (!blueAbility.ShieldActive) return;
                    Require(blueAbility.ShieldActive && blueAbility.Absorb(EnemyColor.Red), "Live Blue shield absorbs mismatch");
                    blueAbility.Tick(76);
                    Require(!blueAbility.ShieldActive, "Live spent shield stays down permanently");
                    foreach (var missile in UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
                    { missile.gameObject.SetActive(false); UnityEngine.Object.Destroy(missile.gameObject); }
                    liveShooter = grid.GetComponentsInChildren<GridEnemy>().First(enemy => enemy != retreatingEnemy);
                    grid.SetColor(liveShooter.Id, EnemyColor.Red);
                    liveShooter.GetComponent<EnemyAbilities>().Tick(0);
                    phase = 61;
                }
                else if (phase == 61)
                {
                    var fired = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                    if (fired.Length == 0) return;
                    Require(fired.Length == 1, "Live Red emits a missile");
                    liveShooter.GetComponent<EnemyAbilities>().enabled = false;
                    // Earlier live missiles may have spent lives before this isolated hit check.
                    player.ResetForRun();
                    player.enabled = true;
                    fired[0].transform.position = player.transform.position + Vector3.up;
                    phase = 7;
                }
                else if (phase == 7 && !player.Alive)
                {
                    Require(player.Lives == PlayerMovement.MaxLives - 1 && !player.Fire() && !tongue.Active,
                        "Missile hit spends one life, disables firing and cancels tongue");
                    phase = 8;
                }
                else if (phase == 8 && player.Alive)
                {
                    Require(player.ReplacementFalling && player.Invulnerable && !player.RespawnProtectionActive && player.Fire(),
                        "Player can fire after the jump apex before landing protection");
                    tongue.Cancel();
                    phase = 81;
                }
                else if (phase == 81 && !player.ReplacementFalling)
                {
                    Require(player.Invulnerable && !player.Hit() && player.Fire(), "Player respawns protected and can fire");
                    tongue.Cancel();
                    phase = 9;
                }
                else if (phase == 9 && !player.Invulnerable)
                {
                    Require(player.Alive, "Player protection expires normally");
                    var remaining = grid.GetComponentsInChildren<GridEnemy>();
                    foreach (var enemy in remaining) grid.SetColor(enemy.Id, EnemyColor.Red);
                    player.CancelShot();
                    var finalTarget = remaining.OrderByDescending(enemy => enemy.Row).First();
                    player.transform.position = new Vector3(finalTarget.transform.position.x, player.transform.position.y, 0);
                    player.RefreshColor(true);
                    Require(player.Fire(), "Fire the final fleet-clearing shot");
                    float contact = finalTarget.GetComponentInChildren<SpriteRenderer>().bounds.min.y - tongue.transform.position.y;
                    tongue.Tick(contact / 14f + .005f, grid);
                    Require(grid.Model.Count == 0 && tongue.Active && tongue.Retracting,
                        "Final live hit clears fleet and keeps the tongue visibly returning");
                    Require(grid.GetComponent<GameSession>().State == GameSession.RunState.Refilling &&
                        !player.Fire() && !grid.GetComponent<EnemyRowSpawner>().TryAdvance(), "Refill pauses firing and ordinary rows");
                    Require(!player.ControlsLocked, "Movement remains unlocked during refill");
                    var beforeRefillMove = player.transform.position;
                    player.Move(1, 0.2f);
                    Require(Mathf.Abs(player.transform.position.x - PlayerMovement.Wrap(beforeRefillMove.x + 1f)) < 0.001f,
                        "Player moves and wraps during refill");
                    var pointer = Camera.main.WorldToScreenPoint(new Vector3(0, player.transform.position.y, 0));
                    player.BeginPointer(pointer);
                    player.UpdatePointer(pointer);
                    ScreenCapture.CaptureScreenshot("TestResults/color-clear-bar-arrival.png");
                    phase = 10;
                }
                else if (phase == 10 && grid.GetComponent<GameSession>().State == GameSession.RunState.Playing)
                {
                    var session = grid.GetComponent<GameSession>();
                    int batchRows = session.Progress.BatchRows;
                    int batchEnemies = session.Progress.BatchEnemies;
                    Require(grid.Model.Count == batchEnemies &&
                        grid.GetComponentsInChildren<GridEnemy>().Length == batchEnemies,
                        "Delayed refill creates the configured number of enemies");
                    Require(player.ReadyColor.HasValue && grid.Model.ColorCount(player.ReadyColor.Value) > 0 &&
                        player.DisplayColor != Color.gray && !tongue.Active, "Returned player color exists in the live next batch");
                    Require(!grid.IsColorCleared(EnemyColor.Red) && !grid.IsColorCleared(EnemyColor.Blue), "Live refill resets color locks");
                    Require(Mathf.Abs(grid.transform.position.x) < 0.1f && !player.ControlsLocked,
                        "Refill resets fleet and restores controls");
                    Require(Mathf.Abs(player.transform.position.x) < 0.1f, "Pointer movement continues during refill");
                    player.EndPointer(Camera.main.WorldToScreenPoint(player.transform.position), true);
                    Require(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).All(m => !m.Suspended),
                        "Surviving missiles stay active after refill");
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    while (player.Lives > 1)
                    {
                        Require(player.Hit(), "Spend lives before filling the player row");
                        player.TickSurvival(3.1f);
                    }
                    player.transform.position = new Vector3(2.95f, player.transform.position.y, 0);
                    for (int i = batchRows; i < GridModel.Rows; i++) Require(spawner.TryAdvance(), "Fill new fleet");
                    Require(grid.GetComponent<GameSession>().State == GameSession.RunState.Dying &&
                        player.Lives == 0 && !player.Alive,
                        "A full player row starts the death animation before game over without requiring contact");
                    var deathMusic = grid.GetComponent<GameplayMusicPlayer>();
                    Require(deathMusic.ShouldPlayMusic && deathMusic.Source.isPlaying && deathMusic.Source.volume > 0 &&
                        deathMusic.Source.pitch == 1,
                        "Fatal death preserves the real music source before its four-beat slowdown begins");
                    int filledEnemies = spawner.CurrentRowWidth * GridModel.Rows;
                    Require(grid.Model.Count == filledEnemies && !player.Fire() && !grid.enabled && !grid.GetComponent<EnemyGridMovement>().enabled,
                        "Game over freezes play without losing enemies");
                    foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>())
                        Require(ability.Suspended, "Game over suspends enemy abilities");
                    Require(!spawner.TryAdvance() && grid.Model.Count == filledEnemies, "Game over rejects more rows");
                    ScreenCapture.CaptureScreenshot("TestResults/player-death.png");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    observedDeathMusicSlowdown = false;
                    phase = 11;
                }
                else if (phase == 11)
                {
                    var session = grid.GetComponent<GameSession>();
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    if (session.State == GameSession.RunState.Dying)
                    {
                        if (session.GameOverBlackoutOpacity > .25f && session.GameOverBlackoutOpacity < .8f)
                        {
                            Require(music.Source.isPlaying && music.Source.volume > 0 && music.Source.pitch < .6f &&
                                music.Source.pitch > 0, "Real frames keep the fading music playing at a dramatically reduced speed");
                            observedDeathMusicSlowdown = true;
                        }
                        return;
                    }
                    Require(observedDeathMusicSlowdown, "The real death sequence includes the music collapse before game over");
                    if (session.GameOverTitleOpacity > .15f && session.GameOverTitleOpacity < .85f && !observedGameOverFade)
                    {
                        Require(!music.Source.isPlaying && music.Source.volume == 0 && session.GameOverBlackoutOpacity == 1,
                            "The game-over title fades in over a fully black scene with music stopped");
                        Require(!session.GameOverTitleReady && !session.HandleMenuKey(KeyCode.Return),
                            "The fading title cannot be skipped by an accidental restart");
                        ScreenCapture.CaptureScreenshot("TestResults/game-over-fading.png");
                        observedGameOverFade = true;
                        Time.timeScale = 0;
                    }
                    if (session.GameOverMusicGain > 0 || !session.GameOverTitleReady) return;
                    Require(observedGameOverFade && session.GameOverTitleOpacity == 1 && !music.Source.isPlaying,
                        "Unscaled game-over transition completes with the title fully visible and silent music");
                    Time.timeScale = 1;
                    Require(player.FatallyDefeated && !player.Alive, "Player remains defeated until restart");
                    ScreenCapture.CaptureScreenshot("TestResults/game-over.png");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 21;
                }
                else if (phase == 21 && EditorApplication.timeSinceStartup - gameOverAt > .15)
                {
                    grid.GetComponent<GameSession>().Restart();
                    phase = 12;
                }
                else if (phase == 12)
                {
                    grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
                    player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                    tongue = player.GetComponentInChildren<TongueShot>();
                    if (grid.GetComponent<GameSession>().State == GameSession.RunState.Refilling)
                    {
                        Require(grid.Model.Count == 0, "Restart also begins empty");
                        observedEmptyRestart = true;
                        return;
                    }
                    Require(observedEmptyRestart, "Restart repeats the empty opening");
                    Require(grid.Model.Count == 2 * RunProgress.StandardRowWidth && grid.GetComponent<GameSession>().State == GameSession.RunState.Playing,
                        "Restart restores opening fleet and session");
                    Require(grid.GetComponent<GameSession>().Progress.Score == 0 && grid.GetComponent<GameSession>().Progress.Level == 1,
                        "Restart resets score and level");
                    Require(player.MagicCharges == 0 && grid.GetComponent<EnemyGridMovement>().CurrentSpeed == 0,
                        "Restart resets magic and fleet speed");
                    Require(player.Lives == PlayerMovement.MaxLives && player.Alive && !player.Invulnerable &&
                        !player.ControlsLocked && player.Fire(),
                        "Restart restores player and firing");
                    Require(grid.enabled && grid.GetComponent<EnemyGridMovement>().enabled && grid.GetComponent<EnemyRowSpawner>().enabled,
                        "Restart restores movement and spawning");
                    Require(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == 0,
                        "Restart clears projectiles");
                    player.StopActions();
                    Require(tongue.GetComponentsInChildren<AudioSource>().Length == 1 &&
                        !tongue.GetComponentInChildren<AudioSource>().isPlaying && tongue.GetComponentInChildren<AudioSource>().clip == null,
                        "Restart creates one fresh voice and cancellation stops it");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                        if (enemy.Column != 2) enemy.gameObject.SetActive(false);
                    var sparseMovement = grid.GetComponent<EnemyGridMovement>();
                    sparseMovement.UseGreenDashes = true;
                    sparseMovement.ResetSweep();
                    sparseMovement.Tick(2.5);
                    Require(grid.Model.Count == 2 && sparseMovement.Direction == 1,
                        "Empty leading columns cause no descent at former full-fleet boundary");
                    sparseMovement.Tick(7.5);
                    Require(grid.Model.Count == 2 + RunProgress.StandardRowWidth && sparseMovement.Direction == -1,
                        "Occupied column contact triggers exactly one live descent and row");
                    foreach (var survivor in grid.GetComponentsInChildren<GridEnemy>().Where(enemy => enemy.Row > 0))
                        Require(survivor.Column == RunProgress.StandardRowWidth - 1 && Mathf.Abs(survivor.transform.position.x - 2.625f) < 0.001f,
                            "Live reindex preserves survivor contact position");
                    grid.OccupiedHorizontalBounds(out float fleetLeft, out float fleetRight);
                    Require(fleetLeft >= -3.0001f && fleetRight <= 3.0001f, "New live row stays inside border");
                    YellowTransformationChecks.Run();
                    TouchControlChecks.Run();
                    SpecialEnemyChecks.Run();
                    SpecialGreenTempoChecks.Run();
                    AbilityBeatChecks.Run();
                    PurpleYellowSpecialChecks.Run();
                    PresentationChecks.Run();
                    MenuChecks.Run();
                    MetronomeOptionsChecks.Run();
                    WaveTransitionChecks.RunRefillDelayEnvelope();
                    SoundEffectsChecks.Run();
                    MissileFlightSoundChecks.Run();
                    MissileBoundaryChecks.Run();
                    MusicKeyChecks.Run();
                    TempoMapChecks.Run();
                    EventPresentationChecks.Run();
                    GameOverPresentationChecks.Run();
                    ColorClearCelebrationChecks.Run();
                    JackpotChecks.Run();
                    PlayerColorAssistChecks.Run();
                    ContactGameOverChecks.Run();
                    PlayerDeathChecks.Run();
                    PlayerLifeIconChecks.Run();
                    SafeRespawnChecks.Run();
                    FullPlayerRowChecks.Run();
                    DeflectedTongueChecks.Run();
                    FleetTickChecks.Run();
                    MissilePersistenceChecks.Run();
                    MagicMultiplierChecks.Run();
                    AimedRedChecks.Run();
                    OrangeChecks.Run();
                    ComboLeaderboardBackgroundChecks.Run();
                    ShotColorComboChecks.Run();
                    SpawnPresentationChecks.RunSummonTint();
                    PreparePreview();
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 13;
                }
                else if (phase == 13 && EditorApplication.timeSinceStartup - gameOverAt > .15)
                {
                    ScreenCapture.CaptureScreenshot("TestResults/ability-feedback.png");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 14;
                }
                else if (phase == 14 && EditorApplication.timeSinceStartup - gameOverAt > .5)
                {
                    if (!previewOnly)
                    {
                        grid.GetComponent<GameSession>().Pause();
                        grid.GetComponent<GameSession>().ReturnToMenu();
                        phase = 15;
                        started = EditorApplication.timeSinceStartup;
                        return;
                    }
                    CompleteRun();
                }
                else if (phase == 15)
                {
                    grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
                    player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                    tongue = player.GetComponentInChildren<TongueShot>();
                    Require(grid.GetComponent<GameSession>().State == GameSession.RunState.MainMenu && grid.Model.Count == 0 &&
                        grid.GetComponent<GameSession>().Progress.Score == 0 && Time.timeScale == 1 && player.ControlsLocked,
                        "Leaving a paused run restores time and opens a clean main menu");
                    ScreenCapture.CaptureScreenshot("TestResults/main-menu.png");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 16;
                }
                else if (phase == 16 && EditorApplication.timeSinceStartup - gameOverAt > .3)
                {
                    Require(grid.Model.Count == 0, "Main menu does not start enemies in the background");
                    grid.GetComponent<GameSession>().StartRun();
                    phase = 17;
                }
                else if (phase == 17 && grid.GetComponent<GameSession>().State == GameSession.RunState.Playing)
                {
                    Require(player.Fire(), "Play starts a fresh usable run");
                    tongue.Tick(.1f);
                    pausedTongueLength = tongue.Length;
                    grid.GetComponent<GameSession>().Pause();
                    ScreenCapture.CaptureScreenshot("TestResults/pause-menu.png");
                    pausedSoundSample = tongue.GetComponentInChildren<AudioSource>().timeSamples;
                    pausedSoundPitch = tongue.GetComponentInChildren<AudioSource>().pitch;
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 18;
                }
                else if (phase == 18 && EditorApplication.timeSinceStartup - gameOverAt > .3)
                {
                    Require(Time.timeScale == 0 && tongue.Active && tongue.Length == pausedTongueLength,
                        "Real frames leave the paused tongue unchanged");
                    var voice = tongue.GetComponentInChildren<AudioSource>();
                    Require(!voice.isPlaying && voice.timeSamples == pausedSoundSample && voice.clip != null && voice.pitch == pausedSoundPitch,
                        "Pause freezes sound playback and pitch without losing its position");
                    grid.GetComponent<GameSession>().Resume();
                    Require(voice.isPlaying, "Resume continues the paused whistle");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 19;
                }
                else if (phase == 19 && EditorApplication.timeSinceStartup - gameOverAt > .15)
                {
                    Require(Time.timeScale == 1 && tongue.Length != pausedTongueLength, "Resume advances the preserved shot");
                    grid.GetComponent<GameSession>().Pause();
                    grid.GetComponent<GameSession>().Restart();
                    phase = 20;
                }
                else if (phase == 20)
                {
                    grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
                    if (grid.GetComponent<GameSession>().State != GameSession.RunState.Playing) return;
                    Require(Time.timeScale == 1 && grid.GetComponent<GameSession>().Progress.Score == 0 &&
                        grid.Model.Count == 2 * RunProgress.StandardRowWidth, "Restart from pause starts a clean running fleet");
                    CompleteRun();
                }
            }
            catch (Exception error)
            {
                MetronomeCheckSettings.EndRuntime(PendingKey);
                SessionState.SetBool(PendingKey, false);
                SpawnOverride.Enabled = SessionState.GetBool("CandyCruisers.Tests.PreviousSpawnOverride", false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void CompleteRun()
        {
                    MetronomeCheckSettings.EndRuntime(PendingKey);
                    SessionState.SetBool(PendingKey, false);
                    SpawnOverride.Enabled = SessionState.GetBool("CandyCruisers.Tests.PreviousSpawnOverride", false);
                    Debug.Log(previewOnly ? "Ability preview captured." :
                        "Runtime gameplay checks passed: core gameplay, abilities, restart, score and occupied-column descent with safe row alignment.");
                    EditorApplication.Exit(0);
        }

        internal static void ObserveOpening(GameSession session)
        {
            if (session.State != GameSession.RunState.Refilling || session.GetComponent<EnemyGrid>().Model.Count != 0) return;
            if (phase == 0) observedEmptyOpening = true;
            if (phase == 12) observedEmptyRestart = true;
        }

        private static void PreparePreview()
        {
            var movement = grid.GetComponent<EnemyGridMovement>();
            movement.ResetSweep();
            movement.enabled = false;
            grid.enabled = false;
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { enemy.gameObject.SetActive(false); UnityEngine.Object.Destroy(enemy.gameObject); }
            var session = grid.GetComponent<GameSession>();
            movement.UseGreenDashes = false;
            session.Progress.Reset(5);
            session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated - 1, true);
            var colors = new[] { EnemyColor.Blue, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Red, EnemyColor.Purple, EnemyColor.Yellow };
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < GridModel.Columns; column++)
                ProgressionChecks.Add(grid, colors[(column + row * 2) % 6], column, row);
            var single = grid.View(grid.Model.At(2, 0).Id);
            grid.ClearMatchingChain(single.Id, EnemyColor.Green);
            Require(session.Progress.Level == 5 && session.Progress.EarnedLevel == 6,
                "Live clear banks the next level without changing active wave tuning");
            Require(Mathf.Abs(movement.CurrentSpeed -
                .03f * CombatBalance.FleetSpeedMultiplier(session.Progress.Level) * grid.Model.ColorCount(EnemyColor.Green)) < .001f,
                "Banked level progress waits to scale live fleet speed");
            if (previewOnly) session.Progress.RegisterClear(30, false);
            var enemies = grid.GetComponentsInChildren<GridEnemy>();
            var yellow = enemies.First(e => e.Color == EnemyColor.Yellow && e.Row == 1);
            var neighbor = grid.View(grid.Model.Neighbors(yellow.Column, yellow.Row).First(n => n.Color != EnemyColor.Yellow).Id);
            var oldSprite = yellow.GetComponentInChildren<SpriteRenderer>().sprite;
            grid.SetColor(yellow.Id, neighbor.Color);
            yellow.GetComponent<EnemyAbilities>().Tick(0);
            yellow.GetComponent<EnemyPresentation>().BeginImitation(neighbor, oldSprite);
            foreach (var enemy in enemies)
            {
                var visual = enemy.GetComponent<EnemyPresentation>();
                if (enemy.Row == 2) visual.SpeedShift(1);
                if (enemy.Color == EnemyColor.Red && enemy.Row == 0) visual.PhaseIn(enemy.Color);
                visual.Tick(enemy == yellow ? .65f : .15f, enemy.Color, enemy.Color == EnemyColor.Purple || enemy.Color == EnemyColor.Green ? 1 : 0);
                enemy.GetComponent<EnemyAbilities>().Suspended = true;
            }
            player.RefreshColor(true);
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Yellow))
                grid.ClearMatchingChain(enemy.Id, EnemyColor.Yellow);
            Require(player.MagicCharges == 1, "Live last-color clear awards magic");
            Require(grid.IsColorCleared(EnemyColor.Yellow) && grid.SeenColors().Count == 5,
                "Live color clear earns one of five encountered-color segments");
            player.transform.position = new Vector3(0, -4.6f, 0);
            Require(player.Fire() && tongue.IsMagic && player.MagicCharges == 0, "Live next shot consumes magic");
            tongue.Tick(.0625f, grid);
            player.RefreshPresentation(.41f);
            var beatMusic = grid.GetComponent<GameplayMusicPlayer>();
            var beatColor = (EnemyColor)(Mathf.FloorToInt(Mathf.Max(0, beatMusic.BeatPosition) * 4) % 6);
            Require(session.ProgressBarColor == player.DisplayColor && player.DisplayColor == EnemyPalette.Get(beatColor),
                "Progress fill follows the magic player's displayed flash color");
            player.enabled = false;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Runtime gameplay check failed: " + message); }
    }

    // Inspect the restarted scene after GameSession.Start and before its first Update.
    // Editor polling can miss an entire short, beat-aligned opening on a slow frame.
    [DefaultExecutionOrder(1000)]
    internal sealed class RuntimeOpeningObserver : MonoBehaviour
    {
        private void Start() => RuntimeGameplayChecks.ObserveOpening(GetComponent<GameSession>());
    }
}
