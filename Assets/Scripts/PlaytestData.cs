using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CandyCruisers
{
    [Serializable]
    public sealed class PlaytestShotStats
    {
        public int fired, completed, interrupted, hits, kills, contacts, snapshotBestTotal, opportunityShots;
        public float efficiencyTotal;
        public float HitRate => Divide(hits, completed);
        public float MeanKills => Divide(kills, fired);
        public float Efficiency => Divide(efficiencyTotal, opportunityShots);
        public static float Divide(float value, float count) => count > 0 ? value / count : 0;
        public void Add(PlaytestShotStats other)
        {
            fired += other.fired; completed += other.completed; interrupted += other.interrupted;
            hits += other.hits; kills += other.kills; contacts += other.contacts;
            snapshotBestTotal += other.snapshotBestTotal; opportunityShots += other.opportunityShots;
            efficiencyTotal += other.efficiencyTotal;
        }
    }

    [Serializable]
    public sealed class PlaytestStageStats
    {
        public int level, samples, damage, comboBreaks, peakCombo, magicAwards, magicAwardsAtCap;
        public int longestMagicHitStreak, fleetsCleared, environmentalKills, threatEpisodes, threatsAvoided;
        public float elapsedSeconds, combatSeconds, readySeconds, enemySeconds, movementDistance, movingSeconds;
        public float threatResponseSeconds;
        public PlaytestShotStats normal = new PlaytestShotStats(), magic = new PlaytestShotStats();
        public int Shots => normal.fired + magic.fired;
        public float KillsPerMinute => PlaytestShotStats.Divide((normal.kills + magic.kills) * 60, combatSeconds);
        public float ShotsPerMinute => PlaytestShotStats.Divide(Shots * 60, combatSeconds);
        public float KillsPerEnemySecond => PlaytestShotStats.Divide(normal.kills + magic.kills, enemySeconds);
        public void Add(PlaytestStageStats other)
        {
            samples++; damage += other.damage; comboBreaks += other.comboBreaks;
            peakCombo = Math.Max(peakCombo, other.peakCombo); magicAwards += other.magicAwards;
            magicAwardsAtCap += other.magicAwardsAtCap; fleetsCleared += other.fleetsCleared;
            environmentalKills += other.environmentalKills; threatEpisodes += other.threatEpisodes;
            threatsAvoided += other.threatsAvoided; threatResponseSeconds += other.threatResponseSeconds;
            longestMagicHitStreak = Math.Max(longestMagicHitStreak, other.longestMagicHitStreak);
            elapsedSeconds += other.elapsedSeconds; combatSeconds += other.combatSeconds;
            readySeconds += other.readySeconds; enemySeconds += other.enemySeconds;
            movementDistance += other.movementDistance; movingSeconds += other.movingSeconds;
            normal.Add(other.normal); magic.Add(other.magic);
        }
    }

    [Serializable]
    public sealed class PlaytestRunData
    {
        public int schemaVersion = 1;
        public string id, utc, participant, controller, platform, gameVersion, buildId, configurationHash;
        public string configuration, initialRandomState, outcome, batchId, profileName, inputDevice;
        public int seed, startLevel, finalLevel, finalLives;
        public long score;
        public float seconds;
        public bool assisted, completed;
        public List<PlaytestStageStats> stages = new List<PlaytestStageStats>();
        public PlaytestStageStats Stage(int level)
        {
            var stage = stages.Find(item => item.level == level);
            if (stage == null) { stage = new PlaytestStageStats { level = level, samples = 1 }; stages.Add(stage); }
            return stage;
        }
    }

    [Serializable]
    public sealed class PlaytestBenchmark
    {
        public int level, sourceRuns, shots;
        public float normalHitRate, magicHitRate, normalEfficiency, magicEfficiency;
        public float readyDelay, movementFraction, dodgeProbability, reactionSeconds;
        public PlaytestStageStats observed = new PlaytestStageStats();
        public bool HasNormalEvidence => observed.normal.completed >= 5;
        public bool HasMagicEvidence => observed.magic.completed >= 5;
    }

    [Serializable]
    public sealed class PlaytestProfile
    {
        public int schemaVersion = 1;
        public string name, configurationHash;
        public List<string> sourceRunIds = new List<string>();
        public List<PlaytestBenchmark> stages = new List<PlaytestBenchmark>();
        public PlaytestBenchmark ForLevel(int level) => stages.Find(stage => stage.level == level);

        public static PlaytestProfile Calibrate(string name, IEnumerable<PlaytestRunData> runs)
        {
            var profile = new PlaytestProfile { name = name };
            string participant = null;
            foreach (var run in runs)
            {
                if (run.schemaVersion != 1 || run.controller != "human" || run.assisted || !run.completed)
                    throw new InvalidDataException("Calibrate only from finished, unassisted human recordings (including manually stopped recordings).");
                if (profile.sourceRunIds.Contains(run.id)) continue;
                if (participant != null && participant != run.participant)
                    throw new InvalidDataException("Select recordings from one participant for each profile.");
                participant = run.participant;
                if (profile.configurationHash != null && profile.configurationHash != run.configurationHash)
                    throw new InvalidDataException("These runs use different configurations. Calibrate each configuration separately.");
                profile.configurationHash = run.configurationHash;
                profile.sourceRunIds.Add(run.id);
                foreach (var stage in run.stages)
                {
                    var benchmark = profile.ForLevel(stage.level);
                    if (benchmark == null) { benchmark = new PlaytestBenchmark { level = stage.level }; profile.stages.Add(benchmark); }
                    benchmark.sourceRuns++;
                    benchmark.observed.Add(stage);
                }
            }
            if (profile.sourceRunIds.Count == 0) throw new InvalidDataException("Select at least one human recording.");
            foreach (var benchmark in profile.stages)
            {
                var stage = benchmark.observed;
                benchmark.shots = stage.Shots;
                benchmark.normalHitRate = stage.normal.HitRate;
                benchmark.magicHitRate = stage.magic.HitRate;
                benchmark.normalEfficiency = stage.normal.Efficiency;
                benchmark.magicEfficiency = stage.magic.Efficiency;
                benchmark.readyDelay = Mathf.Clamp(PlaytestShotStats.Divide(stage.readySeconds, stage.Shots), .05f, 5);
                // Move uses the same five-world-unit-per-second keyboard path on both targets.
                benchmark.movementFraction = Mathf.Clamp(PlaytestShotStats.Divide(stage.movementDistance, stage.movingSeconds) / 5, .1f, 1);
                benchmark.dodgeProbability = Mathf.Clamp01(PlaytestShotStats.Divide(stage.threatsAvoided, stage.threatEpisodes));
                benchmark.reactionSeconds = stage.threatsAvoided > 0
                    ? Mathf.Clamp(stage.threatResponseSeconds / stage.threatsAvoided, .08f, 2) : .4f;
            }
            return profile;
        }

        // Unmeasured policies are deliberately not called calibrated skill models.
        public static PlaytestBenchmark Bootstrap(bool skilled) => new PlaytestBenchmark {
            normalHitRate = skilled ? .96f : .65f, magicHitRate = skilled ? .97f : .7f,
            normalEfficiency = skilled ? .9f : .45f, magicEfficiency = skilled ? .9f : .5f,
            readyDelay = skilled ? .12f : .65f, movementFraction = skilled ? 1 : .65f,
            dodgeProbability = skilled ? .9f : .35f, reactionSeconds = skilled ? .15f : .65f
        };
    }

    [Serializable]
    public sealed class PlaytestOptions
    {
        public string mode = "off", participant = "skilled-human", profileFile = "", bootstrap = "skilled";
        public int seed = 12345, startLevel = 1, runs = 10, nextRun;
        public float limitSeconds = 180;
        public int targetLevel;
        public string batchId = "";
        public string[] scenarioFiles = Array.Empty<string>();
        public bool autoStart, showPanel;
        public const string Key = "CandyCruisers.Playtesting.Options";
        public static string Root => Path.Combine(Application.persistentDataPath, "Playtesting");
        public static PlaytestOptions Load()
        {
            try { return JsonUtility.FromJson<PlaytestOptions>(PlayerPrefs.GetString(Key, "{}")) ?? new PlaytestOptions(); }
            catch (ArgumentException) { return new PlaytestOptions(); }
        }
        public void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public int TotalRuns => Mathf.Clamp(runs, 1, 1000) * Math.Max(1, scenarioFiles?.Length ?? 0);
    }
}
