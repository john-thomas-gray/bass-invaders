using System;
using System.IO;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlaytestRecorder : IDisposable
    {
        [Serializable]
        public sealed class Event
        {
            public string type, detail;
            public float time, x, y, axis;
            public int frame, level, shot, color, enemies, kills, contacts, snapshotBest, combo, magicCharges;
            public bool magic, cancelled;
        }
        private sealed class Shot
        {
            public int number, level, best, kills, contacts, comboBefore;
            public bool magic, brokeCombo;
        }
        public PlaytestRunData Data { get; }
        public string DirectoryPath { get; }
        public string Error { get; private set; }
        private readonly GameSession session;
        private readonly EnemyGrid grid;
        private readonly PlayerMovement player;
        private StreamWriter events;
        private Shot shot;
        private int shotNumber, magicStreak, magicStreakLevel, lastCombo;
        private float sampleClock;
        private float threatStarted;
        private EnemyMissile threat;
        private bool trackingThreat;
        private int threatLevel, threatDamage;
        private bool disposed;
        public PlaytestRecorder(GameSession session, PlayerMovement player, PlaytestRunData data, string root)
        {
            this.session = session; this.player = player; grid = session.GetComponent<EnemyGrid>();
            Data = data;
            DirectoryPath = Path.Combine(root, "Runs", data.id);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, "metadata.json"), JsonUtility.ToJson(data, true));
            events = new StreamWriter(Path.Combine(DirectoryPath, "events.jsonl"), false);
            player.ShotAccepted += BeginShot;
            player.HorizontalAction += Movement;
            player.HorizontalTravelled += Travel;
            player.ShotCompleted += CompleteShot;
            player.ShotRetractionStarted += Retract;
            player.PlayerHit += Damage;
            player.MagicChargeAwarded += MagicAward;
            session.CombatResolved += Combat;
            grid.MatchColorScored += ComboColor;
            Write(new Event { type = "run_started" });
        }
        public void Tick(float seconds)
        {
            if (disposed || seconds <= 0 || session.IsPaused || session.BlocksGameplayInput) return;
            if (session.State != GameSession.RunState.Playing && session.State != GameSession.RunState.Refilling) return;
            Data.seconds += seconds;
            var stage = Data.Stage(session.Progress.Level);
            stage.elapsedSeconds += seconds;
            float x = player.transform.position.x;
            if (player.Alive && !player.ControlsLocked)
            {
                if (player.HorizontalDirection != 0) stage.movingSeconds += seconds;
                if (grid.Fleet.Count > 0 && session.State == GameSession.RunState.Playing)
                {
                    stage.combatSeconds += seconds;
                    stage.enemySeconds += grid.Fleet.Count * seconds;
                    if (!player.ShotActive && !player.Stunned) stage.readySeconds += seconds;
                }
            }
            stage.peakCombo = Math.Max(stage.peakCombo, session.Progress.ComboStreak);
            lastCombo = session.Progress.ComboStreak;
            if (DeveloperOptions.PlayerInvincible) Data.assisted = true;
#if UNITY_EDITOR
            if (SpawnOverride.Enabled) Data.assisted = true;
#endif
            CheckThreat();
            sampleClock += seconds;
            if (sampleClock < .2f) return;
            sampleClock %= .2f;
            Write(new Event { type = "state", x = x, y = player.transform.position.y,
                axis = player.HorizontalDirection, enemies = grid.Fleet.Count, combo = session.Progress.ComboStreak,
                magicCharges = player.MagicCharges, detail = player.Alive ? "alive" : "respawning" });
            events?.Flush();
        }
        private void CheckThreat()
        {
            if (trackingThreat && (threat == null || !threat.gameObject.activeInHierarchy || threat.Finished || !threat.PointedAtTarget ||
                threat.transform.position.y < player.HitBounds.min.y))
            {
                if (Data.Stage(threatLevel).damage == threatDamage && player.Alive)
                {
                    Data.Stage(threatLevel).threatsAvoided++;
                    Data.Stage(threatLevel).threatResponseSeconds += Data.seconds - threatStarted;
                    Write(new Event { type = "threat_avoided", detail = "heading no longer intersects player" });
                }
                threat = null; trackingThreat = false;
            }
            if (threat != null || !player.Alive || player.Invulnerable) return;
            foreach (var missile in UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
            {
                if (missile.Finished || missile.Suspended || !missile.PointedAtTarget ||
                    missile.transform.position.y < player.HitBounds.min.y ||
                    Vector2.Distance(missile.transform.position, player.transform.position) > 3) continue;
                threat = missile; trackingThreat = true; threatStarted = Data.seconds; threatLevel = session.Progress.Level;
                var stage = Data.Stage(threatLevel);
                stage.threatEpisodes++; threatDamage = stage.damage;
                Write(new Event { type = "threat_entered" });
                break;
            }
        }
        private void BeginShot()
        {
            var observation = new PlaytestObservation(grid, player);
            shot = new Shot { number = ++shotNumber, level = session.Progress.Level, magic = player.Tongue.IsMagic,
                best = observation.Best, comboBefore = session.Progress.ComboStreak };
            Stats.fired++;
            Write(new Event { type = "shot_started", shot = shot.number, magic = shot.magic, snapshotBest = shot.best,
                color = (int)player.Tongue.ShotColor, x = player.transform.position.x,
                enemies = grid.Fleet.Count, combo = shot.comboBefore });
        }
        private PlaytestShotStats Stats => shot.magic ? Data.Stage(shot.level).magic : Data.Stage(shot.level).normal;
        private void Combat(int level, int count, bool cleared, bool playerShot)
        {
            if (playerShot && shot != null)
            {
                shot.kills += count; shot.contacts++;
                Stats.kills += count; Stats.contacts++;
                Write(new Event { type = "shot_contact", level = shot.level, shot = shot.number, kills = count, contacts = shot.contacts, magic = shot.magic });
            }
            else
            {
                Data.Stage(level).environmentalKills += count;
                Write(new Event { type = "environmental_clear", level = level, kills = count });
            }
            if (!cleared) return;
            Data.Stage(level).fleetsCleared++;
            Write(new Event { type = "fleet_cleared", level = level });
        }
        private void Retract(bool hit)
        {
            if (shot == null || hit || shot.comboBefore <= 0) return;
            Data.Stage(shot.level).comboBreaks++; shot.brokeCombo = true;
            lastCombo = 0;
            Write(new Event { type = "combo_broken", shot = shot.number, detail = "miss_or_shield" });
        }
        private void CompleteShot(bool hit)
        {
            if (shot == null) return;
            bool cancelled = player.Tongue.LastCompletionCancelled;
            var stage = Data.Stage(shot.level);
            stage.peakCombo = Math.Max(stage.peakCombo, session.Progress.ComboStreak);
            if (cancelled) Stats.interrupted++;
            else
            {
                Stats.completed++;
                if (shot.kills > 0) Stats.hits++;
                if (shot.best > 0) { Stats.snapshotBestTotal += shot.best; Stats.opportunityShots++; Stats.efficiencyTotal += shot.kills / (float)shot.best; }
                if (magicStreakLevel != shot.level) magicStreak = 0;
                magicStreakLevel = shot.level;
                magicStreak = shot.magic && shot.kills > 0 ? magicStreak + 1 : 0;
                stage.longestMagicHitStreak = Math.Max(stage.longestMagicHitStreak, magicStreak);
            }
            Write(new Event { type = "shot_finished", shot = shot.number, kills = shot.kills, contacts = shot.contacts,
                snapshotBest = shot.best, magic = shot.magic, cancelled = cancelled, combo = session.Progress.ComboStreak });
            shot = null;
        }
        private void MagicAward(bool added)
        {
            var stage = Data.Stage(shot?.level ?? session.Progress.Level);
            if (added) stage.magicAwards++; else stage.magicAwardsAtCap++;
            Write(new Event { type = "magic_award", detail = added ? "charge_added" : "at_cap" });
        }
        private void ComboColor(EnemyColor color)
        {
            lastCombo = session.Progress.ComboStreak;
            var stage = Data.Stage(shot?.level ?? session.Progress.Level);
            stage.peakCombo = Math.Max(stage.peakCombo, lastCombo);
        }
        private void Movement(float distance) => Write(new Event { type = "move", axis = distance, x = player.transform.position.x });
        private void Travel(float distance) => Data.Stage(session.Progress.Level).movementDistance += distance;
        private void Damage()
        {
            var stage = Data.Stage(session.Progress.Level);
            stage.damage++;
            if (lastCombo > 0) stage.comboBreaks++;
            magicStreak = 0;
            Write(new Event { type = "player_hit", detail = player.Lives.ToString() });
        }
        private void Write(Event value)
        {
            if (events == null) return;
            value.time = Data.seconds; value.frame = Time.frameCount;
            if (value.level == 0) value.level = shot?.level ?? session.Progress.Level;
            try { events.WriteLine(JsonUtility.ToJson(value)); }
            catch (IOException error) { Error = error.Message; events.Dispose(); events = null; Debug.LogError("Playtest recording failed: " + Error); }
        }
        public void Finish(string outcome)
        {
            if (disposed) return;
            if (shot != null) { Stats.interrupted++; Write(new Event { type = "shot_interrupted", shot = shot.number, cancelled = true }); shot = null; }
            Data.outcome = outcome; Data.completed = Error == null;
            Data.finalLevel = session.Progress.Level; Data.finalLives = player.Lives; Data.score = session.Progress.Score;
            Write(new Event { type = "run_finished", detail = outcome });
            Dispose();
            string path = Path.Combine(DirectoryPath, "summary.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Data, true));
            File.Move(path + ".tmp", path);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            player.ShotAccepted -= BeginShot; player.ShotCompleted -= CompleteShot;
            player.HorizontalAction -= Movement; player.HorizontalTravelled -= Travel;
            player.ShotRetractionStarted -= Retract; player.PlayerHit -= Damage; player.MagicChargeAwarded -= MagicAward;
            session.CombatResolved -= Combat;
            grid.MatchColorScored -= ComboColor;
            events?.Dispose(); events = null;
        }
    }
}
