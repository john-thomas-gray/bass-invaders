using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PresentationChecks
    {
        public static void Run()
        {
            MagicPowerChecks.Run();
            EnemyBeatSpriteChecks.Run();
            TongueTipChecks.Run();
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
                Check(prefab.GetComponent<SpriteRenderer>() == null && prefab.transform.Find("Visuals/Body") != null,
                    "Enemy body is separate from its gameplay root");
                Check(prefab.GetComponent<CharacterVisuals>() != null &&
                    prefab.GetComponentInChildren<VisualAnimationEvents>() != null, "Saved adapter and clip-event relay");
            }
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                var visual = red.Visuals;
                var bounds = red.HitBounds;
                var rootScale = red.transform.localScale;
                visual.Root.localScale *= 3;
                visual.Body.transform.localPosition = Vector3.right * 2;
                visual.Body.sprite = EnemyPlaceholderArt.Triangle;
                Check(red.HitBounds == bounds && red.transform.localScale == rootScale,
                    "Animated scale, offset and replacement sprites cannot change hit bounds or gameplay transform");
                Check(grid.FindMatchingHit(new Vector3(bounds.center.x, -4.6f), 0, 10,
                    EnemyColor.Red, .055f, out int hit, out float distance) && hit == red.Id,
                    "Collision uses the stable gameplay bounds, not animated artwork");
                visual.Body.transform.localPosition = Vector3.zero;
                visual.Root.localScale = Vector3.one;
                int fires = 0, matches = 0, deaths = 0, finishes = 0;
                visual.Fired.AddListener(() => fires++);
                visual.Matched.AddListener(() => matches++);
                visual.Defeated.AddListener(() => deaths++);
                visual.AnimationFinished.AddListener(() => finishes++);
                var originalCount = grid.Model.Count;
                visual.GetComponentInChildren<VisualAnimationEvents>().OnAnimationFinished();
                Check(finishes == 1 && grid.Model.Count == originalCount, "Clip completion never commits a kill");
                red.GetComponent<EnemyAbilities>().Tick(100);
                Check(fires == 1, "Actual Red missile creation emits a firing cue once");
                grid.ClearMatchingChain(red.Id, EnemyColor.Blue);
                Check(matches == 0 && deaths == 0, "Mismatches have no matching or defeat cue");
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(matches == 1 && deaths == 1 && grid.Model.Count == 1, "Match and defeat emitted once per actual defeat");

                var template = new GameObject("Replacement cue template", typeof(PresentationCue));
                var replacement = template.GetComponent<PresentationCue>();
                PresentationCue effect = null;
                try
                {
                    var settings = new SerializedObject(replacement);
                    settings.FindProperty("duration").floatValue = .8f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    effect = PresentationCue.Spawn(replacement, PresentationCue.Kind.Defeat,
                        blue.Visuals.Body, EnemyColor.Blue, 3);
                    Check(effect.Color == EnemyColor.Blue && effect.Depth == 3 &&
                        effect.GetComponentInChildren<EnemyDeathBurst>() == null,
                        "Custom prefab replaces the default burst and receives color/depth context");
                    int starts = 0, ends = 0;
                    effect.Started.AddListener(() => starts++);
                    effect.Completed.AddListener(() => ends++);
                    effect.Finish();
                    Check(!effect.Finished, "Early clip events cannot bypass the cascade delay");
                    effect.Tick(EnemyDeathBurst.RingDelay * 2);
                    Check(starts == 1 && !effect.Finished, "Replacement honors the branch delay");
                    effect.Tick(.81f);
                    effect.Finish();
                    Check(effect.Finished && ends == 1 && grid.Model.Count == 1,
                        "Missing clip event uses fallback cleanup without affecting gameplay");
                }
                finally
                {
                    if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
                    UnityEngine.Object.DestroyImmediate(template);
                }

                if (Application.isPlaying)
                {
                    var replacementObject = new GameObject("Assigned replacement", typeof(PresentationCue));
                    replacementObject.SetActive(false);
                    var assigned = replacementObject.GetComponent<PresentationCue>();
                    blue.Visuals.SetCuePrefabs(assigned, assigned, assigned);
                    var custom = blue.Visuals.Defeat(EnemyColor.Blue, 1);
                    Check(custom != null && custom.gameObject.activeSelf && custom.name.StartsWith("Assigned replacement") &&
                        custom.GetComponentInChildren<EnemyDeathBurst>() == null && grid.Model.Count == 1,
                        "Inspector replacement slot activates the custom prefab and replaces default artwork without killing");
                    var art = new GameObject("Animated artwork", typeof(VisualAnimationEvents));
                    art.transform.SetParent(custom.transform, false);
                    art.GetComponent<VisualAnimationEvents>().OnAnimationFinished();
                    Check(custom.Finished && grid.Model.Count == 1, "Child clip relay finishes custom artwork without combat effects");
                    UnityEngine.Object.DestroyImmediate(custom.gameObject);
                    UnityEngine.Object.DestroyImmediate(replacementObject);
                }

                var playerVisuals = CharacterVisuals.Ensure(player.gameObject);
                var streamCue = PresentationCue.Spawn(null, PresentationCue.Kind.Defeat, blue.Visuals.Body, EnemyColor.Blue, 1, 1, true);
                try
                {
                    var stream = streamCue.GetComponentInChildren<EnemyDeathBurst>();
                    int completions = 0;
                    streamCue.Completed.AddListener(() => completions++);
                    stream.Tick(1.2f);
                    streamCue.Tick(1.2f);
                    Check(!stream.Finished && !streamCue.Finished && streamCue.gameObject.activeSelf,
                        "Default defeat cue waits for the stream rather than cutting it off at its old fixed duration");
                    stream.Tick(EnemyDeathBurst.ColorClearMaxSeconds);
                    streamCue.Tick(EnemyDeathBurst.ColorClearMaxSeconds);
                    streamCue.Tick(1);
                    Check(stream.Finished && streamCue.Finished && completions == 1,
                        "Defeat cue completes exactly once after all dust has arrived");
                }
                finally { UnityEngine.Object.DestroyImmediate(streamCue.gameObject); }
                int playerFires = 0;
                playerVisuals.Fired.AddListener(() => playerFires++);
                player.RefreshColor();
                Check(player.Fire() && !player.Fire() && playerFires == 1,
                    "Player cue follows accepted fire only; rejected input does not animate");
                var bulb = tongue.TipBulb;
                var tongueLine = tongue.GetComponent<LineRenderer>();
                Check(bulb != null && bulb.enabled && bulb.transform.parent == tongue.transform,
                    "Active tongue has a separate replaceable tip bulb");
                Check(bulb.transform.localPosition.y > tongue.Length &&
                    Mathf.Abs(bulb.transform.localScale.x - tongueLine.endWidth * TongueShot.TipBulbWidthMultiplier) < .0001f,
                    "Tip bulb sits beyond the line and scales from its authored end width");
                Check(SameColor(bulb.color, tongueLine.colorGradient.Evaluate(1)),
                    "Ordinary bulb follows the tongue endpoint color");
                tongue.Cancel();
                Check(!bulb.enabled, "Tip bulb hides when the tongue finishes");

                Check(tongue.TryFire(EnemyColor.Red, 1, true), "Magic tongue starts for bulb presentation");
                Check(Mathf.Abs(bulb.transform.localScale.x - tongueLine.endWidth * TongueShot.TipBulbWidthMultiplier) < .0001f &&
                    SameColor(bulb.color, tongueLine.colorGradient.Evaluate(1)),
                    "Magic bulb scales with the thicker tongue and follows its endpoint color");
                tongue.Cancel();
            });
            // Detached presentation objects have no combat components and must not leak from fixtures.
            foreach (var cue in UnityEngine.Object.FindObjectsByType<PresentationCue>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(cue.gameObject);
            Debug.Log("Presentation checks passed: visual children, stable bounds, semantic hooks, replacement prefabs, cascade delays and callback cleanup.");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Presentation check failed: " + message); }
        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .01f && Mathf.Abs(a.g - b.g) < .01f && Mathf.Abs(a.b - b.b) < .01f;

        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var spawner = grid.GetComponent<EnemyRowSpawner>();
            spawner.SpawnBatch(spawner.PlanOpening());
            foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(1);
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            player.RefreshColor();
            var tongue = player.GetComponentInChildren<TongueShot>();
            tongue.TryFire(EnemyColor.Green, 2.2f);
            tongue.Tick(.12f);
            var firing = PresentationCue.Spawn(null, PresentationCue.Kind.Fire,
                CharacterVisuals.Ensure(player.gameObject).Body, player.ReadyColor.Value, 1);
            firing.Tick(.06f);
            var enemy = grid.View(grid.Model.At(2, 1).Id);
            var matched = PresentationCue.Spawn(null, PresentationCue.Kind.Match, enemy.Visuals.Body, enemy.Color, 1);
            matched.Tick(.08f);
            var defeated = PresentationCue.Spawn(null, PresentationCue.Kind.Defeat, enemy.Visuals.Body, enemy.Color, 1);
            defeated.GetComponentInChildren<EnemyDeathBurst>().Tick(.12f);
            enemy.Visuals.Body.enabled = false;
            GameplayChecks.Capture(540, 960, "presentation-cues-portrait");
            GameplayChecks.Capture(960, 540, "presentation-cues-landscape");
            GameplayChecks.Capture(390, 844, "presentation-cues-phone");
            Debug.Log("Presentation cue previews rendered.");
        }
    }
}
