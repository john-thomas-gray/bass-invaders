using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GreenCloakChecks
    {
        public static void Run()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                var visual = green.GetComponent<EnemyPresentation>();
                var body = green.Visuals.Body;
                var bounds = green.HitBounds;
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                foreach (float tempo in new[] { 75f, 115.03f, 180f })
                foreach (int rate in new[] { 30, 60, 120 })
                {
                    music.PreviewTempo = tempo;
                    float beat = 60f / tempo;
                    music.PreviewTime = beat * 4.05f;
                    visual.MovementPulse();
                    Check(body.color.a == 0, "Regular Green disappears immediately when its arrow starts");
                    music.PreviewTime = beat * 4.11f;
                    visual.Tick(0, EnemyColor.Green, 0);
                    Check(body.color.a == 0, "Body stays hidden during the arrow animation");
                    music.PreviewTime = beat * 4.2f;
                    visual.Tick(0, EnemyColor.Green, 0);
                    Check(body.color.a == 0 && body.enabled, "Regular Green artwork disappears without disabling the renderer");
                    Check(grid.Model.Find(green.Id) != null && grid.Model.MatchingDepths(green.Id, EnemyColor.Green).Count == 1 &&
                        green.HitBounds == bounds, "Cloaked enemy retains occupancy, matching and collision bounds");
                    visual.Tick(10, EnemyColor.Green, 0);
                    Check(body.color.a == 0, "Paused soundtrack holds the cloak");
                    for (int frame = 1; frame < rate; frame++)
                    {
                        music.PreviewTime = beat * Mathf.Lerp(4.2f, 4.5f, (float)frame / rate);
                        visual.Tick(1f / rate, EnemyColor.Green, 0);
                        Check(body.color.a == 0, "Regular Green stays hidden during the arrow animation");
                    }
                    music.PreviewTime = beat * 4.5f;
                    visual.Tick(0, EnemyColor.Green, 0);
                    Check(body.color.a == 0, "Regular Green remains hidden halfway through the arrow animation");
                    Check(green.GetComponentsInChildren<MeshRenderer>().All(layer => layer.enabled),
                        "Reappearance does not interrupt the arrow's full-beat animation");
                    music.PreviewTime = beat * 5;
                    visual.Tick(0, EnemyColor.Green, 0);
                    Check(body.color.a == 0, "Fade begins after the arrow ends");
                    Check(green.GetComponentsInChildren<MeshRenderer>().All(layer => !layer.enabled),
                        "Regular arrow ends before the body fades in");
                    for (int frame = 1; frame < rate; frame++)
                    {
                        music.PreviewTime = beat * (5 + (float)frame / rate);
                        visual.Tick(1f / rate, EnemyColor.Green, 0);
                        Check(Mathf.Abs(body.color.a - Mathf.SmoothStep(0, 1, (float)frame / rate)) < .001f,
                            "Body fades smoothly over one soundtrack beat");
                    }
                    music.PreviewTime = beat * 6;
                    visual.Tick(0, EnemyColor.Green, 0);
                    Check(body.color.a == 1, "Body returns to full opacity after one beat");
                }
                music.PreviewTime = 0;
                visual.MovementPulse();
                music.PreviewTime = music.BeatDuration * .2f;
                visual.Tick(0, EnemyColor.Green, 0);
                visual.Clear();
                Check(body.color.a == 1, "Clearing the effect restores body opacity");
                visual.MovementPulse();
                music.PreviewTime = music.BeatDuration * .4f;
                visual.Tick(0, EnemyColor.Green, 0);
                music.PreviewTime = null;
                visual.Tick(music.BeatDuration * 2, EnemyColor.Green, 0);
                Check(body.color.a == 1, "Track changes cannot strand an ordinary Green in its cloak");
                music.PreviewTime = 0;
                visual.MovementPulse();
                music.PreviewTime = music.BeatDuration * .2f;
                visual.Tick(0, EnemyColor.Green, 0);
                grid.SetColor(green.Id, EnemyColor.Red);
                green.GetComponent<EnemyAbilities>().Tick(0);
                Check(body.color.a == 1, "Recoloring restores visible artwork");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                ProgressionChecks.Add(grid, EnemyColor.Green, 1, 0);
                ProgressionChecks.Add(grid, EnemyColor.Green, 2, 0);
                grid.RefreshSpecials();
                var visual = green.GetComponent<EnemyPresentation>();
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.PreviewTime = 0;
                Check(green.Visuals.Body.color.a == 1, "Promotion alone does not cloak a special");
                SpecialGreenTempoChecks.Activate(green);
                music.PreviewTime = music.BeatDuration * .2f;
                visual.Tick(0, EnemyColor.Green, 0);
                Check(green.Visuals.Body.color.a == 0, "Special uses the same cloak on activation");
                music.PreviewTime = music.BeatDuration * 8.5f;
                green.GetComponent<EnemyAbilities>().Tick(0);
                Check(green.Visuals.Body.color.a == 0 &&
                    green.GetComponentsInChildren<MeshRenderer>().All(layer => layer.enabled),
                    "Special stays cloaked across later offbeats while its arrow remains visible");
                music.PreviewTime = null;
                visual.Tick(10, EnemyColor.Green, 0);
                Check(green.Visuals.Body.color.a == 0, "Changing tracks does not reveal a spent special");
                grid.SetColor(green.Id, EnemyColor.Red);
                green.GetComponent<EnemyAbilities>().Tick(0);
                Check(green.Visuals.Body.color.a == 1, "Special cloak ends when it stops being Green");
            });
            Debug.Log("Green cloak checks passed: immediate disappearance, one-beat fade after arrows, permanent special cloak, pause, track changes, matching and collision geometry at multiple tempos and frame rates.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Green cloak check failed: " + message); }
    }
}
