using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CandyCruisers
{
    public static class PlaytestReport
    {
        [Serializable]
        private sealed class Batch
        {
            public int schemaVersion = 1, runs, gameOvers, timeLimits, targetsReached, interrupted;
            public string id;
            public List<PlaytestRunData> results = new List<PlaytestRunData>();
        }
        public static void WriteBatch(string root, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var batch = new Batch { id = id };
            var csv = new StringBuilder("run,profile,platform,configuration,seed,outcome,assisted,seconds,final_level,score,stage,combat_seconds,shots_per_minute,kills_per_minute,kills_per_enemy_second,normal_shots,normal_hit_rate,normal_kills_per_shot,normal_snapshot_efficiency,magic_shots,magic_hit_rate,magic_kills_per_shot,magic_snapshot_efficiency,magic_awards,magic_hit_streak,damage,combo_breaks,peak_combo,threats,avoided_threats,benchmark_status,target_normal_hit_rate,target_magic_hit_rate,target_normal_efficiency,target_magic_efficiency\n");
            string runs = Path.Combine(root, "Runs");
            if (!Directory.Exists(runs)) return;
            foreach (string path in Directory.GetFiles(runs, "summary.json", SearchOption.AllDirectories).OrderBy(path => path))
            {
                var run = JsonUtility.FromJson<PlaytestRunData>(File.ReadAllText(path));
                if (run == null || run.batchId != id) continue;
                batch.results.Add(run); batch.runs++;
                if (run.outcome == "game_over") batch.gameOvers++;
                else if (run.outcome == "time_limit") batch.timeLimits++;
                else if (run.outcome == "target_reached") batch.targetsReached++;
                else batch.interrupted++;
                string profilePath = Path.Combine(Path.GetDirectoryName(path), "profile.json");
                var profile = File.Exists(profilePath) ? JsonUtility.FromJson<PlaytestProfile>(File.ReadAllText(profilePath)) : null;
                foreach (var stage in run.stages)
                {
                    var target = profile?.ForLevel(stage.level);
                    string status = target == null ? "unmeasured" : profile.configurationHash != run.configurationHash ? "transferred_configuration" :
                        target.HasNormalEvidence && (stage.magic.fired == 0 || target.HasMagicEvidence) ? "measured_seed_profile" : "sparse_evidence";
                    object[] fields = { run.id, run.profileName, run.platform, run.configurationHash, run.seed, run.outcome, run.assisted,
                        run.seconds, run.finalLevel, run.score, stage.level, stage.combatSeconds, stage.ShotsPerMinute, stage.KillsPerMinute, stage.KillsPerEnemySecond,
                        stage.normal.fired, stage.normal.HitRate, stage.normal.MeanKills, stage.normal.Efficiency,
                        stage.magic.fired, stage.magic.HitRate, stage.magic.MeanKills, stage.magic.Efficiency,
                        stage.magicAwards, stage.longestMagicHitStreak, stage.damage, stage.comboBreaks, stage.peakCombo, stage.threatEpisodes, stage.threatsAvoided,
                        status, target == null ? "" : target.normalHitRate.ToString(CultureInfo.InvariantCulture),
                        target == null ? "" : target.magicHitRate.ToString(CultureInfo.InvariantCulture),
                        target == null ? "" : target.normalEfficiency.ToString(CultureInfo.InvariantCulture),
                        target == null ? "" : target.magicEfficiency.ToString(CultureInfo.InvariantCulture) };
                    csv.AppendLine(string.Join(",", fields.Select(Csv)));
                }
            }
            string output = Path.Combine(root, "Batches"); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, id + ".json"), JsonUtility.ToJson(batch, true));
            File.WriteAllText(Path.Combine(output, id + ".csv"), csv.ToString());
        }
        private static string Csv(object value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"";
    }
}
