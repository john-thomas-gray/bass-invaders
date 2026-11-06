using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerLifeIconChecks
    {
        public static void Run()
        {
            CheckChoreography();
            CheckBeatSynchronization();
            CheckLayout();
            CheckApexHandoff();
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                player.RefreshColor();
                Check(player.Lives == PlayerMovement.MaxLives && !player.GrantLife(), "Life cap is unchanged");
                for (int hit = 1; hit <= PlayerMovement.MaxExtraLives; hit++)
                {
                    Check(player.Hit() && player.LifeIcons.Active && !player.FatallyDefeated,
                        "A nonfatal hit starts a spare's downward jump");
                    Check(player.LifeIcons.ConsumedSlot == PlayerMovement.MaxExtraLives - hit && !player.Hit(),
                        "Exactly the rightmost spare is spent and repeat damage is ignored");
                    player.TickSurvival(.1f);
                    Check(player.LifeIcons.Started && player.LifeIcons.TravelProgress > 0 && !player.Alive,
                        "Spare jumps down while the player's death animation plays");
                    float age = player.LifeIcons.Age;
                    player.TickSurvival(-3);
                    Check(player.LifeIcons.Age == age, "Negative time cannot reverse the jump");
                    player.TickSurvival(4);
                    Check(player.Alive && !player.Invulnerable && !player.LifeIcons.Active,
                        "Spare is consumed once and the player recovers normally");
                }
                Check(player.ExtraLives == 0 && player.Fire(), "Final active life remains playable");
                Check(player.Hit() && player.FatallyDefeated && !player.LifeIcons.Active && !player.GrantLife(),
                    "Fatal death creates no phantom spare or late resurrection");
                player.ResetForRun();
                Check(player.Lives == PlayerMovement.MaxLives && !player.LifeIcons.Animating,
                    "Restart clears all life animations and restores the cap");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                player.Music.StopPlayback();
                player.Hit(); player.TickSurvival(.15f);
                session.Pause();
                float age = player.LifeIcons.Age, beat = player.LifeIcons.Choreography.Beat;
                player.TickSurvival(10);
                Check(age == player.LifeIcons.Age && beat == player.LifeIcons.Choreography.Beat,
                    "Pause freezes both the downward jump and group choreography");
                session.Resume();
                player.Music.StopPlayback();
                Check(player.GrantLife(), "A reward during recovery remains allowed");
                player.TickSurvival(4); player.TickSurvival(4);
                Check(!player.LifeIcons.Animating && player.Lives == PlayerMovement.MaxLives,
                    "Queued reward survives recovery without duplicating icons");
            });
            PlayerLifeRewardChecks.Run();
            Debug.Log("Player life icon checks passed: independent choreography, four-beat boundaries, cap flips, drop-in recovery, layout, pause, rewards and restart.");
        }

        private static void CheckChoreography()
        {
            var dance = new LifeSlimeChoreography();
            dance.SetBeat(1, 1, 7);
            dance.SetBeat(12.99f, 1, 7);
            Check(!dance.Swaying, "Normal pulse is the usual routine");
            dance.SetBeat(13, 1, 7);
            Check(dance.Swaying, "Side-to-side routine starts at a four-beat measure boundary");
            dance.SetBeat(13.5f, 1, 7); float right = dance.Lean;
            dance.SetBeat(14.5f, 1, 7);
            Check(right > 0 && dance.Lean < 0 && dance.Swaying, "All miniatures rock in place on alternating beats");
            dance.SetBeat(16.99f, 1, 7);
            Check(dance.Swaying, "Routine remains unchanged throughout the measure");
            dance.SetBeat(17, 1, 7);
            Check(!dance.Swaying && Mathf.Abs(dance.Lean) < .0001f, "Normal pulse returns only at the next measure");
            dance.BeginFlip(.5f); dance.Tick(.25f, null);
            Check(dance.Flipping && Mathf.Abs(dance.FlipAngle - 180) < .01f,
                "One-off cap flip can interrupt a measure without switching its routine");
            dance.Tick(.25f, null);
            Check(!dance.Flipping && dance.FlipAngle == 0, "Group flip lands upright");
            dance.SetBeat(13, 1, 7); bool before = dance.Swaying;
            dance.SetBeat(6.4f, 0, 8);
            Check(before == dance.Swaying, "Song skips preserve the routine until the next measure");
            dance.Reset();
            Check(!dance.Flipping && !dance.Swaying, "Restart clears choreography");
            dance.BeginFlip(1); dance.Tick(.05f, null);
            Check(dance.FlipHop > 0 && dance.FlipVerticalScale < 1,
                "Full-cap celebration starts by squashing into an upward hop");
            dance.Tick(.45f, null);
            Check(dance.FlipHop > .9f && dance.FlipVerticalScale > 1,
                "Full-cap celebration reaches real verticality with a stretched body");
            dance.Tick(.5f, null);
            Check(!dance.Flipping && Mathf.Abs(dance.FlipVerticalScale - 1) < .0001f,
                "Full-cap celebration lands back at neutral scale");
            var art = new Color32[128 * 128]; var independent = new Color32[128 * 128];
            PlayerSlimeVisual.PaintDome(independent, 128, 0);
            PlayerSlimeVisual.PaintDome(art, 128, 18);
            var stationary = new Color32[128 * 128]; PlayerSlimeVisual.PaintDome(stationary, 128, 0);
            for (int i = 0; i < stationary.Length; i++)
                Check(stationary[i].Equals(independent[i]), "Player deformation cannot mutate independent icon pixels");
        }

        private static void CheckBeatSynchronization()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.PreviewTempo = 120;
                music.Source.Stop();
                var dance = player.LifeIcons.Choreography;
                var character = CharacterVisuals.Ensure(player.gameObject);
                var art = new GameObject("Pulse test artwork", typeof(SpriteRenderer));
                art.transform.SetParent(player.transform, false);
                var body = art.GetComponent<SpriteRenderer>();
                body.sprite = player.GetComponent<SpriteRenderer>().sprite;
                character.Configure(body, art.transform);
                var resting = new Rect(10, 80, 200, GameSession.LevelProgressBaseHeight);
                foreach (float beat in new[] { 0f, .5f, 1f, 9.5f, 10f })
                {
                    music.PreviewTime = beat * .5f;
                    dance.Tick(.17f, music);
                    character.RefreshBeat(beat, true);
                    bool onBeat = beat == Mathf.Floor(beat);
                    var bar = GameSession.LevelProgressSolidRect(resting, false, GameplayMusicPlayer.BeatPulse(beat));
                    Check(Mathf.Abs(dance.Beat - beat) < .0001f &&
                        Mathf.Abs(dance.Scale - (onBeat ? 1.09f : 1)) < .0001f,
                        "Extra lives use the main music beat even after a seek or without audible playback");
                    Check((GameSession.GameplayTextPulseScale(beat) > 1) == onBeat &&
                        (bar.height > resting.height) == onBeat,
                        "Lives, text and ordinary progress peak together on integer beats");
                    Check(Mathf.Abs(character.BeatScale - (onBeat ? 1 : 1.09f)) < .0001f,
                        "The player keeps its separate half-beat pulse");
                }
                foreach (int frameRate in new[] { 30, 60, 120 })
                {
                    for (int frame = 0; frame <= frameRate; frame++)
                    {
                        music.PreviewTime = frame / (float)frameRate;
                        dance.Tick(1f / frameRate, music);
                    }
                    Check(Mathf.Abs(dance.Beat - 2) < .0001f && Mathf.Abs(dance.Scale - 1.09f) < .0001f,
                        "Life timing stays on the music clock at " + frameRate + " frames per second");
                }
                music.PreviewTime = null;
                float stoppedBeat = music.BeatPosition;
                dance.Tick(5, music);
                Check(Mathf.Abs(dance.Beat - stoppedBeat) < .0001f,
                    "A stopped music clock cannot let extra lives drift onto their own beat");
            });
        }

        private static void CheckApexHandoff()
        {
            foreach (bool flip in new[] { false, true })
            {
                var icons = new PlayerLifeIcons(); icons.BeginLoss(0, flip);
                icons.Tick(PlayerLifeIcons.AnticipationSeconds);
                Check(icons.LossRotation == 0, "The takeoff flip waits until after the crouch hold");
                icons.Tick((PlayerLifeIcons.ApexSeconds - PlayerLifeIcons.AnticipationSeconds) / 2);
                Check(icons.LossFlips == flip && Mathf.Abs(icons.LossRotation - (flip ? -180 : 0)) < .01f,
                    "Replacement can start with either a flip or a plain jump");
                icons.Tick((PlayerLifeIcons.ApexSeconds - PlayerLifeIcons.AnticipationSeconds) / 2);
                Check(Mathf.Abs(icons.LossRotation - (flip ? -360 : 0)) < .01f, "Optional flip finishes at the apex");
                icons.Dispose();
            }
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                float ground = player.transform.position.y;
                player.Hit(); player.TickSurvival(PlayerLifeIcons.ApexSeconds - .001f);
                Check(!player.Alive && !player.Invulnerable, "No control or protection before apex");
                player.TickSurvival(.002f);
                Check(player.Alive && player.ReplacementFalling && player.LifeIcons.LossHandedOff && player.Invulnerable &&
                    !player.RespawnProtectionActive, "Apex hands control to an invincible actor without starting landing protection");
                float x = player.transform.position.x;
                player.Move(1, .01f);
                Check(player.transform.position.x != x && player.Fire(), "Falling player can move and fire");
                player.TickSurvival(PlayerLifeIcons.Duration - PlayerLifeIcons.ApexSeconds);
                Check(!player.ReplacementFalling && player.Invulnerable && player.RespawnProtectionActive &&
                    Mathf.Abs(player.transform.position.y - ground) < .001f,
                    "Landing begins protection at the original ground height");
                player.TickSurvival(1.49f); Check(player.Invulnerable, "Landing protection lasts a full interval");
                player.TickSurvival(.02f); Check(!player.Invulnerable, "Landing protection expires normally");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                float ground = player.transform.position.y;
                player.Hit(); player.TickSurvival(PlayerLifeIcons.ApexSeconds + .1f);
                int lives = player.Lives;
                for (int i = 0; i < 8; i++)
                {
                    Check(player.Invulnerable && !player.RespawnProtectionActive && !player.Hit() && player.Lives == lives &&
                        player.GetComponentsInChildren<SpriteRenderer>().Where(sprite => sprite.GetComponentInParent<TongueShot>() == null)
                            .All(sprite => sprite.enabled),
                        "Falling replacement rejects hits and stays solid without consuming landing protection");
                    player.TickSurvival(.1f);
                }
                var missile = new GameObject("Airborne protection missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                try
                {
                    missile.transform.position = player.transform.position;
                    missile.SetTarget(player); missile.Tick(.01f);
                    Check(missile.Finished && player.Lives == lives && player.ReplacementFalling,
                        "A direct missile collision cannot consume a falling extra life");
                }
                finally { if (missile != null) UnityEngine.Object.DestroyImmediate(missile.gameObject); }
                player.TickSurvival(PlayerLifeIcons.Duration - PlayerLifeIcons.ApexSeconds - .9f);
                Check(!player.ReplacementFalling && player.RespawnProtectionActive &&
                    Mathf.Abs(player.transform.position.y - ground) < .001f, "Landing starts the timed protection interval");
                player.TickSurvival(.05f);
                Check(player.Invulnerable && player.GetComponentsInChildren<SpriteRenderer>().All(sprite => !sprite.enabled),
                    "Invincibility flashing starts after landing, not in the air");
                player.TickSurvival(1.43f);
                Check(player.Invulnerable, "The airborne descent does not shorten the landing interval");
                player.TickSurvival(.03f);
                Check(!player.Invulnerable && player.GetComponentsInChildren<SpriteRenderer>()
                    .Where(sprite => sprite.GetComponentInParent<TongueShot>() == null).All(sprite => sprite.enabled) &&
                    player.Hit(), "Protection ends normally and restores the solid, damageable player");
            });
        }

        private static void CheckLayout()
        {
            foreach (float startX in new[] { 30f, 170f })
            {
                var slot = new Rect(startX - 9, 60, 18, 18);
                var target = new Rect(startX - 20, 400, 40, 32);
                var peak = PlayerLifeIcons.LossDisplayRect(slot, target, PlayerLifeIcons.ApexProgress, 100);
                Check(Mathf.Abs(peak.center.x - 100) < Mathf.Abs(startX - 100) &&
                    Mathf.Abs(Mathf.Abs(peak.center.x - startX) - 32) < .001f,
                    "The rise swishes inward by less than one body width even when landing directly below the slot");
                Check(PlayerLifeIcons.LossDisplayRect(slot, target, 1, 100) == target,
                    "The inward rise preserves the intended landing destination");
            }
            Check(PlayerLifeIcons.LossVerticalScale(.03f) < 1 && PlayerLifeIcons.LossVerticalScale(.23f) > 1 &&
                PlayerLifeIcons.LossVerticalScale(.5f) > 1 && PlayerLifeIcons.LossVerticalScale(.95f) < 1,
                "Replacement compresses on takeoff, stretches in flight and softens its landing");
            foreach (float t in new[] { 0, PlayerLifeIcons.ApexProgress, 1 })
                Check(Mathf.Abs(PlayerLifeIcons.LossVerticalScale(t) - 1) < .0001f,
                    "Jump starts, hands off and ends at neutral scale");
            CheckAnimatedGuiTransform();
            foreach (var size in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540), new Vector2(1179, 2556) })
            for (int slot = 0; slot < PlayerMovement.MaxExtraLives; slot++)
            {
                var row = PlayerLifeIcons.RowRect(12, 0, (int)size.y);
                var resting = PlayerLifeIcons.IconRect(row, slot);
                var pulsing = PlayerLifeIcons.IconRect(row, slot, PlayerLifeIcons.BeatScale(0));
                float originalSize = size.y < 400 ? 10 : size.y < 600 ? 14 : 18;
                Check(Mathf.Abs(resting.width - originalSize * 1.3f) < .001f,
                    "Extra lives are thirty percent larger at each layout size");
                Check(pulsing.yMin > GameSession.GameplayHeadingHeight && pulsing.yMax < PlayerLifeIcons.ProgressOffset((int)size.y) - 3,
                    "Normal life pulses do not cover the score or progress bar");
                var destination = new Rect(size.x / 2 - 20, size.y - 70, 40, 32);
                Check(PlayerLifeIcons.LossDisplayRect(resting, destination, 0) == resting,
                    "Spent slime starts in its own top-row slot");
                foreach (float fraction in new[] { .5f, .75f, 1f })
                {
                    float t = PlayerLifeIcons.AnticipationProgress * fraction;
                    Check(PlayerLifeIcons.LossDisplayRect(resting, destination, t) == resting &&
                        Mathf.Abs(PlayerLifeIcons.LossVerticalScale(t) - .5f) < .0001f,
                        "The slime visibly holds its half-height crouch in its original life slot");
                }
                var ascent = PlayerLifeIcons.LossDisplayRect(resting, destination,
                    (PlayerLifeIcons.AnticipationProgress + PlayerLifeIcons.ApexProgress) / 2);
                var apex = PlayerLifeIcons.LossDisplayRect(resting, destination, PlayerLifeIcons.ApexProgress);
                Check(ascent.center.x > resting.center.x && apex.center.x > ascent.center.x &&
                    apex.center.y < ascent.center.y && ascent.center.y < resting.center.y,
                    "The replacement arcs sideways and upward after its crouch hold");
                var afterApex = PlayerLifeIcons.LossDisplayRect(resting, destination, PlayerLifeIcons.ApexProgress + .001f);
                Check(Vector2.Distance(afterApex.center, apex.center) < 2,
                    "The arc remains continuous through the controllable handoff");
                var middle = PlayerLifeIcons.LossDisplayRect(resting, destination, .65f);
                Check(middle.center.y > resting.center.y && middle.center.y < destination.center.y,
                    "Spent slime drops down the screen toward the player");
                Check(PlayerLifeIcons.LossDisplayRect(resting, destination, 1) == destination,
                    "Spent slime lands at the player position rather than breaking apart");
                if (slot > 0)
                {
                    var previous = PlayerLifeIcons.IconRect(row, slot - 1, PlayerLifeIcons.BeatScale(0));
                    float currentLeft = pulsing.center.x - pulsing.width * 1.35f / 2;
                    float previousRight = previous.center.x + previous.width * 1.35f / 2;
                    Check(currentLeft - previousRight >= 3.999f,
                        "Fixed life slots leave four pixels between icons at maximum magic growth");
                }
                Check(PlayerLifeIcons.ApexSeconds - PlayerLifeIcons.AnticipationSeconds >= .3f,
                    "The takeoff has enough time to read before the apex handoff");
                var beforeRise = PlayerLifeIcons.LossDisplayRect(resting, destination, PlayerLifeIcons.AnticipationProgress);
                var afterRise = PlayerLifeIcons.LossDisplayRect(resting, destination, PlayerLifeIcons.AnticipationProgress + .001f);
                Check(Vector2.Distance(beforeRise.center, afterRise.center) < .02f &&
                    Vector2.Distance(beforeRise.size, afterRise.size) < .02f,
                    "Takeoff starts smoothly in position and size after the crouch");
            }
        }

        private static void CheckAnimatedGuiTransform()
        {
            var rect = new Rect(92, 48, 18, 18);
            var center = new Vector3(rect.center.x, rect.center.y, 0);
            var feet = new Vector3(rect.center.x, rect.yMax, 0);
            foreach (float guiScale in new[] { 1f, GameSession.SmallPhoneGuiScale, GameSession.MediumPhoneGuiScale,
                GameSession.LargePhoneGuiScale })
            {
                var baseMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1));
                var rotating = LifeSlimeArtwork.AnimatedGuiMatrix(baseMatrix, rect, -270, 1);
                Check(Vector2.Distance(rotating.MultiplyPoint3x4(center), baseMatrix.MultiplyPoint3x4(center)) < .001f,
                    "Rotating life copies keep the same logical pivot under phone GUI scaling");
                var squashing = LifeSlimeArtwork.AnimatedGuiMatrix(baseMatrix, rect, 0, .5f);
                Check(Vector2.Distance(squashing.MultiplyPoint3x4(feet), baseMatrix.MultiplyPoint3x4(feet)) < .001f,
                    "Squashing life copies keep their feet anchored under phone GUI scaling");
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Player life icon check failed: " + message); }
    }
}
