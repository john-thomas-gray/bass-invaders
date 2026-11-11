using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerDeathChecks
    {
        public static void Run()
        {
            CheckRecoverableHits();
            CheckFatalTiming();
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 2, GridModel.Rows - 1);
                player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                player.RefreshColor();
                Check(player.Hit() && player.Lives == PlayerMovement.MaxLives - 1, "First hit spends a spare life");
                player.TickSurvival(3.1f);
                Check(player.Hit() && player.Lives == 1, "Second hit spends the last spare, not the active life");
                player.TickSurvival(3.1f);
                int defeats = 0;
                CharacterVisuals.Ensure(player.gameObject).Defeated.AddListener(() => defeats++);
                var missile = new GameObject("Death-test missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                missile.SetTarget(player);
                player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                Check(session.CheckPlayerContact() && session.State == GameSession.RunState.Dying, "Final contact begins death, not game-over overlay");
                Check(!session.HandleMenuKey(KeyCode.Space), "Space cannot skip the fatal fade");
                Check(defeats == 0 && player.FatallyDefeated && !player.Alive && player.ControlsLocked,
                    "Fatal defeat locks the player without triggering the regular death animation");
                Check(CharacterVisuals.Ensure(player.gameObject).Body.enabled, "Live player art remains intact during the fade");
                Check(!grid.enabled && !grid.GetComponent<EnemyGridMovement>().enabled && !grid.GetComponent<EnemyRowSpawner>().enabled && missile.Suspended,
                    "Fleet, spawns and missiles freeze during death");
                var position = player.transform.position;
                player.Move(1, 10); player.TickSurvival(10);
                Check(player.transform.position == position && !player.Fire() && !player.Hit() && !player.Alive, "Fatal player cannot move, fire, take another hit or respawn");
                session.Pause();
                Check(!session.IsPaused, "Pause overlay cannot obscure the short death transition");
                Check(!session.CheckPlayerContact() && defeats == 0, "Repeated contact cannot skip the fade or trigger a death cue");
                session.Tick(session.GameOverBlackoutSeconds);
                Check(defeats == 1 && player.GetComponentsInChildren<SpriteRenderer>().All(sprite => !sprite.enabled),
                    "Only after four beats does the game-over cue replace the intact player");
                session.Tick(.45f);
                Check(session.State == GameSession.RunState.Dying, "Game-over overlay stays hidden mid-animation");
                Check(!session.HandleMenuKey(KeyCode.Space), "Space cannot skip the fatal animation");
                if (Application.isPlaying)
                {
                    var cue = (PresentationCue)typeof(GameSession).GetField("deathCue",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(session);
                    var burst = cue.GetComponentInChildren<PlayerMeltDeath>();
                    Check(burst != null && burst.GetComponentsInChildren<SpriteRenderer>().Any(sprite => sprite.enabled),
                        "The active fatal death cue displays the dizzy player");
                }
                session.Tick(PlayerMeltDeath.Duration - .46f);
                Check(session.State == GameSession.RunState.Dying, "Game over waits for the whole death animation");
                session.Tick(.02f);
                Check(session.State == GameSession.RunState.GameOver && defeats == 1, "Game over follows animation exactly once");
                Check(!session.HandleMenuKey(KeyCode.Space), "Space cannot skip the game-over title reveal");
                session.Tick(10);
                Check(!player.Alive && defeats == 1, "Finished death never reappears or retriggers");
            });

            if (Application.isPlaying)
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var custom = new GameObject("Replacement player death", typeof(PresentationCue)).GetComponent<PresentationCue>();
                try
                {
                    var settings = new UnityEditor.SerializedObject(custom);
                    settings.FindProperty("duration").floatValue = 1.5f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    CharacterVisuals.Ensure(player.gameObject).SetCuePrefabs(null, null, custom);
                    grid.transform.position = Vector3.up * 3;
                    var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                    var enemy = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, GridModel.Rows - 1);
                    player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                    for (int i = 0; i < PlayerMovement.MaxExtraLives; i++)
                    { player.Hit(); player.TickSurvival(4); }
                    player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                    Check(session.CheckPlayerContact(), "Replacement death starts");
                    session.Tick(session.GameOverBlackoutSeconds);
                    session.Tick(1);
                    Check(session.State == GameSession.RunState.Dying, "Replacement duration can outlast the placeholder");
                    session.Tick(.51f);
                    Check(session.State == GameSession.RunState.GameOver, "Replacement fallback duration completes game over");
                }
                finally { UnityEngine.Object.DestroyImmediate(custom.gameObject); }
            });
            Debug.Log("Player death checks passed: four-beat fade before fatal animation, frozen gameplay, no respawn, exactly-once transition and replacement cues.");
        }

        private static void CheckFatalTiming()
        {
            foreach (float step in new[] { .016f, .05f, 2f })
            foreach (float tempo in new[] { 90f, 180f })
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    var sounds = grid.GetComponent<SoundEffects>();
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    int shatters = 0;
                    sounds.CuePlayed += (effect, pitch) => { if (effect == SoundEffect.PlayerShatter) shatters++; };
                    Check(player.Hit(), "Recoverable hit begins without ending the run");
                    Check(music.ShouldPlayMusic && session.GameOverBlackoutOpacity == 0 &&
                        grid.GetComponentInChildren<GameOverBlackout>() == null && shatters == 0,
                        "A spare-life death does not stop music, black out the scene or play the fatal shatter");
                    player.TickSurvival(4);
                    var settings = new UnityEditor.SerializedObject(music);
                    settings.FindProperty("soundtrack").arraySize = 0;
                    settings.FindProperty("beatsPerMinute").floatValue = tempo;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    int defeats = 0;
                    CharacterVisuals.Ensure(player.gameObject).Defeated.AddListener(() => defeats++);
                    typeof(GameSession).GetMethod("BeginPlayerDeath",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                    float fade = 4 * 60f / tempo;
                    Check(Mathf.Abs(session.GameOverBlackoutSeconds - fade) < .0001f,
                        "The fade lasts exactly four beats at the fatal hit's current tempo");
                    settings.Update();
                    settings.FindProperty("beatsPerMinute").floatValue = tempo * 2;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    session.Tick(-1);
                    session.Tick(0);
                    Check(shatters == 0 && defeats == 0 && session.GameOverBlackoutOpacity == 0 && music.ShouldPlayMusic &&
                        music.Source.pitch == 1 && session.GameOverMusicGain == 1,
                        "Nonpositive death ticks neither advance the blackout nor release the shatter");
                    float elapsed = 0;
                    while (session.State == GameSession.RunState.Dying)
                    {
                        elapsed += step;
                        session.Tick(step);
                        Check(shatters == 0,
                            "The melt never plays explosion audio, even on short or long frames");
                        Check(defeats == (elapsed >= fade ? 1 : 0), "Death hooks cannot run before the fade finishes");
                        Check(elapsed < fade + PlayerMeltDeath.Duration ? session.State == GameSession.RunState.Dying :
                            session.State == GameSession.RunState.GameOver,
                            "Only time beyond the fade boundary advances the death animation, even on long frames");
                        if (elapsed < fade)
                        {
                            float opacity = Mathf.SmoothStep(0, 1, elapsed / fade);
                            Check(Mathf.Abs(session.GameOverBlackoutOpacity - opacity) < .0001f &&
                                Mathf.Abs(session.GameOverScoreOpacity - (1 - opacity)) < .0001f &&
                                session.GameOverTitleOpacity == 0,
                                "Scene and score share the full four-beat fade while the game-over title stays hidden");
                            Check(CharacterVisuals.Ensure(player.gameObject).Body.enabled &&
                                grid.GetComponentInChildren<FatalImpactBackdrop>() == null,
                                "The player stays whole and the impact waits during the fade");
                        }
                        if (elapsed < fade)
                            Check(music.ShouldPlayMusic && music.Source.pitch > 0 && music.Source.pitch < 1 &&
                                music.Source.volume > 0, "Fatal music slows and fades while the blackout is advancing");
                        else
                            Check(!music.ShouldPlayMusic && !music.Source.isPlaying && music.Source.volume == 0,
                                "Music is silent before the dizzy eyes and melt begin");
                    }
                    session.Tick(10);
                    Check(shatters == 0 && defeats == 1 && session.GameOverBlackoutOpacity == 1 &&
                        Mathf.Abs(session.GameOverBlackoutSeconds - fade) < .0001f,
                        "Final blackout holds without explosion audio after death completion");
                });
        }

        private static void CheckRecoverableHits()
        {
            foreach (bool yellowPenalty in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 2, 0);
                player.RefreshColor();
                Check(player.ReadyColor == EnemyColor.Yellow, "Penalty fixture starts Yellow");
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                int deaths = 0;
                CharacterVisuals.Ensure(player.gameObject).Defeated.AddListener(() => deaths++);
                if (yellowPenalty)
                {
                    var ability = yellow.GetComponent<EnemyAbilities>();
                    Check(ability.BeginImitation(red), "Last Yellow starts transformation");
                    ability.Tick(EnemyAbilities.DefaultImitationSeconds);
                }
                else
                {
                    var missile = new GameObject("Recovery missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    missile.transform.position = player.transform.position + Vector3.up;
                    missile.SetTarget(player);
                    SpecialEnemyChecks.StartAtCruiseSpeed(missile);
                    missile.Tick(.3f);
                    Check(missile.Finished, "Missile hits the player");
                }
                Check(deaths == 1 && player.Lives == PlayerMovement.MaxLives - 1 && !player.Alive &&
                    !player.FatallyDefeated && !player.Invulnerable,
                    "Missile and last-Yellow penalties trigger recoverable death cues");
                Check(!player.Hit() && deaths == 1, "Repeated hit cannot duplicate death animation");
                float takeoff = PlayerLifeIcons.ApexSeconds + .1f;
                player.TickSurvival(takeoff);
                Check(player.Alive && player.ReplacementFalling && player.Invulnerable && !player.RespawnProtectionActive &&
                    player.GetComponentsInChildren<SpriteRenderer>().Any(sprite => sprite.enabled),
                    "Actual player is controllable and invincible after the apex, without timed landing protection yet");
                if (Application.isPlaying)
                    Check(UnityEngine.Object.FindObjectsByType<PlayerDeathBurst>(FindObjectsSortMode.None).Any(),
                        "Recoverable death keeps rendering the ordinary player burst");
                player.TickSurvival(PlayerLifeIcons.Duration - takeoff);
                Check(player.Alive && player.Invulnerable && !player.FatallyDefeated,
                    "Normal respawn remains at 1.5 seconds with protection");
                player.TickSurvival(1.5f);
                Check(player.Alive && !player.Invulnerable && deaths == 1, "Recovery ends normally after three seconds");
            });
        }

        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerDefeat,
                CharacterVisuals.Ensure(player.gameObject).Body, EnemyColor.Blue, 1);
            foreach (var sprite in player.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
            cue.Tick(.10f);
            GameplayChecks.Capture(540, 960, "player-death-flash");
            cue.Tick(.22f);
            GameplayChecks.Capture(540, 960, "player-death-burst");
            GameplayChecks.Capture(960, 540, "player-death-burst-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Player death check failed: " + message); }
    }
}
