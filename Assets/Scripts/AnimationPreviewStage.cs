#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyCruisers
{
    [DefaultExecutionOrder(-10000)]
    public sealed class AnimationPreviewStage : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/AnimationPreview.unity";
        public const string GameplayPath = "Assets/Scenes/Gameplay.unity";
        public const string SettingsKey = "CandyCruisers.AnimationPreview.";
        public enum Animation
        {
            Idle, MenuArrival, Tongue, MagicTongue, ShieldDeflection, EnemySpawn,
            EnemyDeath, ColorClear, WaveTransition, LifeLoss, LifeGain, ComboBreak,
            GameOver, RedShot, GreenTempo, PurpleSummon, YellowImitation, OrangeBurst, MissileGaze,
            StartGame, LevelTransitions
        }

        public static AnimationPreviewStage Instance { get; private set; }
        public Animation Selected { get; set; }
        public EnemyColor Color { get; set; } = EnemyColor.Blue;
        public int Level { get; set; } = 19;
        public float Tempo { get; set; } = 120;
        public float Speed { get; set; } = 1;
        public float CycleSeconds { get; set; } = 10;
        public bool Loop { get; set; } = true;
        public bool Sound { get; set; }
        public MenuIntroAnimation.Entrance MenuEntrance { get; set; } = MenuIntroAnimation.Entrance.AllVariants;
        public float Elapsed { get; private set; }
        public float ScenarioSeconds { get; private set; }
        public float PlaybackSeconds => Mathf.Max(CycleSeconds, ScenarioSeconds);
        public int ShotsFired { get; private set; }
        public int SummonsShown { get; private set; }
        public int ImitationsShown { get; private set; }
        public int MissileHits { get; private set; }
        public string FleetSignature { get; private set; }
        public bool Loading { get; private set; }
        public string Failure { get; private set; }
        public bool Ready => !Loading && Failure == null && Session != null;
        public GameSession Session { get; private set; }
        public PlayerMovement Player { get; private set; }
        public EnemyGrid Grid { get; private set; }
        private GameplayMusicPlayer music;
        private AudioClip clockClip;
        private EnemyRowSpawner spawner;
        private Scene previewWorld;
        private readonly List<(float time, Action action)> cues = new();
        private int nextCue;
        private float motionStart = -100;
        private int motionContext;
        private bool shotMotion;
        private GridEnemy activeGreen;
        private EnemyMissile gazeMissile;
        private readonly System.Random layouts = new System.Random();
        private float previousTimeScale;
        private bool previousAudioPause, previousInvincible, previousSpawnOverride;
        private UnityEngine.Random.State previousRandom;

        private void Awake()
        {
            Instance = this;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousInvincible = DeveloperOptions.PlayerInvincible;
            previousSpawnOverride = SpawnOverride.Enabled;
            previousRandom = UnityEngine.Random.state;
            DeveloperOptions.PlayerInvincible = false;
            SpawnOverride.Enabled = false;
            ReadSettings();
            clockClip = AudioClip.Create("Animation Preview Clock", 44100, 1, 44100, false);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => Replay();

        public void ReadSettings()
        {
            Selected = (Animation)SessionState.GetInt(SettingsKey + "Animation", (int)Animation.LifeLoss);
            Color = (EnemyColor)SessionState.GetInt(SettingsKey + "Color", (int)EnemyColor.Blue);
            Level = Mathf.Clamp(SessionState.GetInt(SettingsKey + "Level", 19), 1, 99);
            Tempo = Mathf.Clamp(SessionState.GetFloat(SettingsKey + "Tempo", 120), 40, 240);
            Speed = Mathf.Clamp(SessionState.GetFloat(SettingsKey + "Speed", 1), .1f, 2);
            CycleSeconds = Mathf.Clamp(SessionState.GetFloat(SettingsKey + "Duration", 10), 2, 30);
            Loop = SessionState.GetBool(SettingsKey + "Loop", true);
            Sound = SessionState.GetBool(SettingsKey + "Sound", false);
            MenuEntrance = (MenuIntroAnimation.Entrance)SessionState.GetInt(SettingsKey + "MenuEntranceMode", -2);
            if (MenuEntrance == MenuIntroAnimation.Entrance.Random) MenuEntrance = MenuIntroAnimation.Entrance.AllVariants;
        }

        public void Replay()
        {
            if (!Application.isPlaying || Loading) return;
            EditorApplication.isPaused = false;
            StartCoroutine(Rebuild());
        }

        private IEnumerator Rebuild()
        {
            Loading = true;
            Failure = null;
            AudioListener.pause = true;
            Time.timeScale = Mathf.Clamp(Speed, .1f, 2);
            Session = null; Player = null; Grid = null; music = null; gazeMissile = null;
            cues.Clear(); nextCue = 0; Elapsed = 0; ScenarioSeconds = 3;
            ShotsFired = SummonsShown = ImitationsShown = MissileHits = 0;
            shotMotion = false; motionStart = -100; activeGreen = null;
            SceneManager.SetActiveScene(gameObject.scene);
            if (previewWorld.IsValid() && previewWorld.isLoaded)
                yield return SceneManager.UnloadSceneAsync(previewWorld);
            previewWorld = EditorSceneManager.LoadSceneInPlayMode(GameplayPath, new LoadSceneParameters(LoadSceneMode.Additive));
            // Let the real actors complete Start before preparing a scenario.
            yield return null;
            try { Prepare(); cues.Sort((a, b) => a.time.CompareTo(b.time)); }
            catch (Exception error) { Failure = error.Message; Debug.LogException(error); }
            Loading = false;
            AudioListener.pause = !Sound;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Loading || scene.path != GameplayPath) return;
            SceneManager.SetActiveScene(scene);
            Session = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>()).Single();
            Player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerMovement>()).Single();
            Grid = Session.GetComponent<EnemyGrid>();
            spawner = Grid.GetComponent<EnemyRowSpawner>();
            Session.PresentationPreview = true;
            music = Grid.GetComponent<GameplayMusicPlayer>();
            music.PreviewTime = 0;
            music.PreviewTempo = Tempo;
            music.Source.Stop();
            music.Source.clip = clockClip;
            music.Source.loop = true;
            music.Source.volume = 0;
            music.Source.ignoreListenerPause = true;
            music.Source.Play();
            music.UpdatePlayback();
            Player.ControlsLocked = true;
        }

        private void Update()
        {
            if (!Ready) return;
            Time.timeScale = Mathf.Clamp(Speed, .1f, 2);
            AudioListener.pause = !Sound;
            float seconds = Time.deltaTime;
            Elapsed += seconds;
            music.PreviewTime = Elapsed;
            Player.ControlsLocked = Session.State != GameSession.RunState.Playing ||
                !(shotMotion || Selected == Animation.MissileGaze);
            Grid.enabled = false;
            Grid.RefreshSpecials();
            Grid.GetComponent<EnemyGridMovement>().enabled = Selected == Animation.GreenTempo ||
                Selected == Animation.StartGame && Session.State == GameSession.RunState.Playing;
            while (nextCue < cues.Count && Elapsed >= cues[nextCue].time)
            {
                cues[nextCue++].action();
            }
            if (shotMotion) TickMotion(seconds);
            if (Session.State != GameSession.RunState.MainMenu)
            {
                Player.TickSurvival(seconds);
                Player.Tongue.Tick(seconds, Grid);
                Player.RefreshColor();
                Player.RefreshPresentation(Elapsed);
            }
            if (gazeMissile != null)
                gazeMissile.transform.position = Player.transform.position + new Vector3(
                    Mathf.Sin(Elapsed * 1.5f) * 1.6f, .16f + Mathf.Cos(Elapsed * 1.5f) * 1.6f);
            if (Elapsed < PlaybackSeconds) return;
            if (Loop) Replay();
            else EditorApplication.isPaused = true;
        }

        private void Prepare()
        {
            Session.StartLevel = Selected == Animation.LevelTransitions ? 1 : Level;
            if (Selected == Animation.MenuArrival)
            {
                if ((int)MenuEntrance < 0)
                {
                    float pass = GameSession.MenuWordIntroDuration + 2;
                    Session.SetMenuEntrancePreview(MenuIntroAnimation.Entrance.FlipRight);
                    At(pass, () => Session.SetMenuEntrancePreview(MenuIntroAnimation.Entrance.SkidLeft));
                    At(pass * 2, () => Session.SetMenuEntrancePreview(MenuIntroAnimation.Entrance.TractorBeam));
                    ScenarioSeconds = pass * 3;
                }
                else
                {
                    Session.SetMenuEntrancePreview(MenuEntrance);
                    ScenarioSeconds = GameSession.MenuWordIntroDuration + 4;
                }
                return;
            }
            if (Selected == Animation.StartGame)
            {
                Session.Tick(GameSession.MenuWordIntroDuration);
                At(1.5f, Session.BeginMenuArrival);
                ScenarioSeconds = 1.5f + GameSession.MenuArrivalSeconds + 5;
                return;
            }
            Session.StartRun();
            Session.Tick(music.BeatDuration + .01f);
            foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>())
            { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            Grid.BeginColorCycle();
            Grid.enabled = false;
            foreach (var frame in FindObjectsByType<PlayfieldFrame>(FindObjectsSortMode.None)) frame.Tick(10);
            Camera.main.GetComponent<GameplayFraming>().TickBackground(10);
            if (Selected != Animation.LevelTransitions) Add(Color, 2, 1);
            Player.RefreshColor();
            Grid.GetComponent<EnemyGridMovement>().enabled = false;
            switch (Selected)
            {
                case Animation.LevelTransitions:
                    At(2, () => Session.Progress.Reset(2));
                    At(6, () => Session.Progress.Reset(3));
                    At(10, () => Session.Progress.Reset(4));
                    At(14, () => Session.Progress.Reset(5));
                    ScenarioSeconds = 18;
                    break;
                case Animation.Idle:
                    Add(OtherColor, 4, 2);
                    foreach (var color in Enum.GetValues(typeof(EnemyColor)).Cast<EnemyColor>()
                        .Where(c => c != Color && c != OtherColor).Take(2))
                    {
                        var reward = Add(color, 0, 0);
                        Grid.ClearMatchingChain(reward.Id, color);
                    }
                    if (Session.Progress.FleetsToClear > 1) Session.Progress.RegisterClear(1, true);
                    At(3, () => {
                        if (Session.Progress.ClearedFleets + 1 < Session.Progress.FleetsToClear)
                            Session.Progress.RegisterClear(1, true);
                    });
                    ScenarioSeconds = 6;
                    break;
                case Animation.Tongue:
                case Animation.MagicTongue:
                case Animation.ShieldDeflection:
                    PrepareShots();
                    break;
                case Animation.EnemySpawn:
                    PrepareFleet();
                    foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>()) enemy.Visuals.Root.gameObject.SetActive(false);
                    At(.5f, () => { foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>())
                    { enemy.Visuals.Root.gameObject.SetActive(true); enemy.GetComponent<EnemyAbilities>().BeginSpawnEffect(); } });
                    break;
                case Animation.EnemyDeath:
                    PrepareFleet();
                    for (int i = 0; i < 6; i++)
                    {
                        var color = (EnemyColor)i;
                        At(.5f + i * 1.3f, () => { foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == color).ToArray())
                            if (enemy != null && enemy.isActiveAndEnabled) Grid.ClearMatchingChain(enemy.Id, color); });
                    }
                    ScenarioSeconds = 11;
                    break;
                case Animation.ColorClear:
                    Add(Color, 3, 1); Add(Color, 4, 1);
                    Add(OtherColor, 0, 4);
                    At(.5f, () => Grid.ClearMatchingChain(Grid.Model.At(2, 1).Id, Color));
                    break;
                case Animation.WaveTransition:
                case Animation.LifeGain:
                    ClearEnemies();
                    for (int i = 0; i < 6; i++) Add((EnemyColor)i, 2, i);
                    SpendSpare();
                    At(.5f, () => { foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>().ToArray())
                        Grid.ClearMatchingChain(enemy.Id, enemy.Color); });
                    ScenarioSeconds = 18 * music.BeatDuration + 4;
                    break;
                case Animation.LifeLoss:
                    At(.5f, () => { Player.ControlsLocked = false; Player.Hit(); });
                    break;
                case Animation.ComboBreak:
                    At(.5f, () => Session.ComboBreak.Begin(8, EnemyPalette.Get(Color)));
                    break;
                case Animation.GameOver:
                    Session.Progress.RegisterClear(12, false);
                    for (int i = 0; i < PlayerMovement.MaxExtraLives; i++) SpendSpare();
                    At(.5f, () => { Player.ControlsLocked = false; Player.Hit(true); });
                    ScenarioSeconds = GameSession.GameOverBlackoutBeats * music.BeatDuration + PlayerMeltDeath.Duration + 4;
                    break;
                case Animation.RedShot:
                    ClearEnemies();
                    for (int i = 1; i <= 3; i++) Add(EnemyColor.Red, i, 1);
                    Grid.RefreshSpecials();
                    At(.5f, () => FireMissile(Grid.View(Grid.Model.At(2, 1).Id)));
                    break;
                case Animation.GreenTempo:
                    ClearEnemies();
                    for (int row = 1; row <= 4; row++)
                        for (int i = 1; i <= 4; i++) Add(EnemyColor.Green, i, row);
                    Grid.RefreshSpecials();
                    activeGreen = Grid.View(Grid.Model.At(3, 4).Id);
                    spawner.enabled = false;
                    ScenarioSeconds = 12 * music.BeatDuration + 3;
                    break;
                case Animation.PurpleSummon:
                    for (int i = 0; i < 6; i++)
                    {
                        var color = (EnemyColor)i;
                        At(.5f + i * 2.8f, () => SummonColor(color));
                    }
                    ScenarioSeconds = 18;
                    break;
                case Animation.YellowImitation:
                    int sample = 0;
                    float sampleSpacing = music.SecondsForBeats(EnemyAbilities.ImitationBeats + 3) + .5f;
                    foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                    {
                        if (color == EnemyColor.Yellow) continue;
                        for (int variant = 0; variant < (color == EnemyColor.Orange ? 1 : 2); variant++)
                        {
                            bool special = variant == 1 || color == EnemyColor.Orange;
                            At(.5f + sample++ * sampleSpacing, () => ImitateColor(color, special));
                        }
                    }
                    ScenarioSeconds = sample * sampleSpacing + 1;
                    break;
                case Animation.OrangeBurst:
                    ClearEnemies();
                    var orange = Add(EnemyColor.Orange, 2, 1);
                    Add(EnemyColor.Red, 1, 1); Add(EnemyColor.Green, 3, 1); Add(EnemyColor.Purple, 2, 2);
                    Add(EnemyColor.Blue, 5, 4);
                    At(.5f, () => Grid.ClearMatchingChain(orange.Id, EnemyColor.Orange));
                    break;
                case Animation.MissileGaze:
                    Player.transform.position = Vector3.zero;
                    gazeMissile = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"))
                        .GetComponent<EnemyMissile>();
                    gazeMissile.Suspended = true;
                    At(5, () => { Destroy(gazeMissile.gameObject); gazeMissile = null;
                        LaunchMissile(new Vector3(-2.2f, 2), new Vector3(1, -1.5f)); });
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = 45 + i * 90;
                        At(7 + i * 4, () => { Player.ResetForRun(); Player.transform.position = Vector3.zero;
                            Player.ControlsLocked = false;
                            var direction = Quaternion.Euler(0, 0, angle) * Vector3.up;
                            LaunchMissile(Player.HitBounds.center + direction * 2, -direction); });
                    }
                    Player.PlayerHit += () => MissileHits++;
                    ScenarioSeconds = 23;
                    break;
            }
            Player.ControlsLocked = true;
        }

        private EnemyColor OtherColor => Color == EnemyColor.Red ? EnemyColor.Blue : EnemyColor.Red;

        private void At(float time, Action action) => cues.Add((time, action));

        private void PrepareShots()
        {
            shotMotion = true;
            ClearEnemies();
            if (Selected == Animation.MagicTongue)
            {
                Add(EnemyColor.Orange, 5, 0);
                var primer = Add(EnemyColor.Yellow, 0, 0);
                Grid.ClearMatchingChain(primer.Id, primer.Color);
            }
            float spacing = Selected == Animation.ShieldDeflection ?
                3 + 3 / TongueShot.DeflectedReturnSpeed(Level) : 2.6f;
            for (int i = 0; i < 5; i++)
            {
                int context = i;
                float start = .5f + i * spacing;
                At(start, () => BeginShotContext(context));
                At(start + .65f, () => { Player.ControlsLocked = false; if (Player.Fire()) ShotsFired++; });
            }
            ScenarioSeconds = .5f + 5 * spacing;
        }

        private void BeginShotContext(int context)
        {
            motionContext = context; motionStart = Elapsed;
            if (Selected != Animation.MagicTongue)
            {
                ClearEnemies();
                if (Selected == Animation.ShieldDeflection)
                {
                    for (int column = 0; column < 6; column++) Add(EnemyColor.Blue, column, 8);
                    Add(EnemyColor.Red, 5, 9);
                    Player.PrepareNextWave(new[] { EnemyColor.Red }, EnemyColor.Red);
                }
                else { Add(Color, 0, 0); Add(OtherColor, 5, 0); }
            }
            else if (context < 4)
            {
                var colors = context == 0 ? new[] { EnemyColor.Blue } : context == 1 ? new[] { EnemyColor.Red } :
                    context == 2 ? new[] { EnemyColor.Green, EnemyColor.Purple } : new[] { EnemyColor.Yellow };
                for (int group = 0; group < colors.Length; group++)
                    for (int col = context == 0 ? 2 : 1; col <= (context == 0 ? 2 : 3); col++)
                        Add(colors[group], col, 5 - group * 3);
            }
            Grid.RefreshSpecials();
            Player.RefreshColor();
            float center = Grid.transform.TransformPoint(Grid.CellPosition(2, 5)).x;
            float offset = context == 0 ? 0 : context == 2 ? .78f : -.78f;
            Player.transform.position = new Vector3(center + offset, Player.transform.position.y);
        }

        private void TickMotion(float seconds)
        {
            float age = Elapsed - motionStart;
            float axis = motionContext == 0 || age < 0 || age > 1.5f ? 0 :
                motionContext == 2 ? -1 : motionContext == 3 && age >= .78f ? 0 :
                motionContext == 4 && age >= .78f ? -1 : 1;
            Player.ControlsLocked = false;
            Player.Move(axis * .24f, seconds);
        }

        private void PrepareFleet()
        {
            ClearEnemies();
            var colors = Enumerable.Range(0, 5).OrderBy(_ => layouts.Next()).Select(i => (EnemyColor)i).ToArray();
            for (int i = 0; i < colors.Length; i++)
            {
                int row = i * 2;
                bool right = layouts.Next(2) == 0;
                int first = right ? 3 : 0;
                int shape = layouts.Next(3);
                Add(colors[i], first, row); Add(colors[i], first + 1, row);
                Add(colors[i], shape == 0 ? first + 2 : first + (shape == 1 ? 0 : 1), shape == 0 ? row : row + 1);
                Add(colors[i], right ? 0 : 5, row);
            }
            Add(EnemyColor.Orange, 2, 9);
            Grid.RefreshSpecials();
            FleetSignature = string.Join(";", Grid.GetComponentsInChildren<GridEnemy>().Select(e => $"{e.Color}:{e.Column}:{e.Row}"));
        }

        private void SummonColor(EnemyColor color)
        {
            ClearEnemies();
            var purple = Add(EnemyColor.Purple, color == EnemyColor.Orange ? 2 : 0, color == EnemyColor.Orange ? 1 : 0);
            if (color != EnemyColor.Orange)
            { Add(color, 2, 3); Add(color, 3, 3); }
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            try
            {
                SpawnOverride.Enabled = true; SpawnOverride.Types = 1 << (int)color;
                var candidates = spawner.SummonCandidates(Grid.Model);
                var anchors = candidates.Keys.ToArray();
                var random = UnityEngine.Random.state;
                // Pick a reproducible random roll that exercises the real group-completing spawn.
                for (int seed = 0; seed < 4096; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    var gaps = candidates[anchors[UnityEngine.Random.Range(0, anchors.Length)]];
                    var cell = gaps[UnityEngine.Random.Range(0, gaps.Count)];
                    bool desired = color == EnemyColor.Orange ? UnityEngine.Random.Range(0, EnemyRowSpawner.OrangeSpawnOdds) == 0 :
                        Mathf.Abs(cell.x - 2) + Mathf.Abs(cell.y - 3) == 1 ||
                        Mathf.Abs(cell.x - 3) + Mathf.Abs(cell.y - 3) == 1;
                    if (!desired) continue;
                    UnityEngine.Random.InitState(seed);
                    var summoned = spawner.TrySummon(purple);
                    if (summoned != null) SummonsShown++;
                    break;
                }
                UnityEngine.Random.state = random;
            }
            finally { SpawnOverride.Enabled = enabled; SpawnOverride.Types = types; }
            Player.RefreshColor();
        }

        private void ImitateColor(EnemyColor color, bool special)
        {
            ClearEnemies();
            var basic = Add(EnemyColor.Yellow, 0, 2);
            var upper = Add(color == EnemyColor.Orange ? EnemyColor.Red : color, 1, 2);
            if (special && color != EnemyColor.Orange) { Add(color, 1, 1); Add(color, 2, 1); }
            var specialYellow = Add(EnemyColor.Yellow, 2, 6);
            Add(EnemyColor.Yellow, 1, 6); Add(EnemyColor.Yellow, 0, 6);
            var lower = Add(color, 3, 6);
            if (special && color != EnemyColor.Orange) { Add(color, 3, 5); Add(color, 4, 5); }
            Grid.RefreshSpecials();
            foreach (var pair in new[] { (basic, upper), (specialYellow, lower) })
            {
                var ability = pair.Item1.GetComponent<EnemyAbilities>();
                ability.Suspended = false;
                if (ability.BeginImitation(pair.Item2)) ImitationsShown++;
            }
        }

        private GridEnemy Add(EnemyColor color, int column, int row)
        {
            // Configure the cell before OnEnable registers the prefab with the live grid.
            var staging = new GameObject("Inactive staging");
            staging.SetActive(false);
            var instance = Instantiate(spawner.Prefab(color), staging.transform);
            var enemy = instance.GetComponent<GridEnemy>() ?? instance.AddComponent<GridEnemy>();
            enemy.Configure(color, column, row);
            instance.transform.SetParent(Grid.transform, false);
            enemy.GetComponent<EnemyAbilities>().Suspended = true;
            spawner.ApplyAppearance(enemy);
            Destroy(staging);
            return enemy;
        }

        private void ClearEnemies()
        {
            foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>())
            { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            Grid.BeginColorCycle();
        }

        private void SpendSpare()
        {
            Player.ControlsLocked = false;
            Player.Hit(true);
            Player.TickSurvival(4);
            Player.ControlsLocked = true;
        }

        private void FireMissile(GridEnemy enemy)
        {
            var heading = (Player.transform.position - enemy.transform.position).normalized;
            enemy.GetComponent<EnemyPresentation>().AimRed(heading, true, 1, true);
            LaunchMissile(enemy.transform.position + heading * .3f, heading);
            enemy.Visuals.Fire(enemy.Color);
        }

        private void LaunchMissile(Vector3 origin, Vector3 heading)
        {
            var missile = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                origin, Quaternion.identity).GetComponent<EnemyMissile>();
            missile.LaunchAimed(Player, heading);
            var sound = missile.GetComponent<MissileFlightSound>() ?? missile.gameObject.AddComponent<MissileFlightSound>();
            sound.Configure(missile, Grid.GetComponent<SoundEffects>());
        }

        private void LateUpdate()
        {
            if (!Ready) return;
            if (Session.State == GameSession.RunState.Dying || Session.State == GameSession.RunState.GameOver) return;
            // Ability logic is dormant; its actual presentation still advances at preview speed.
            foreach (var enemy in Grid.GetComponentsInChildren<GridEnemy>())
            {
                var ability = enemy.GetComponent<EnemyAbilities>();
                if (Selected == Animation.StartGame || enemy == activeGreen || enemy.Color == EnemyColor.Blue ||
                    Selected == Animation.YellowImitation && (ability.IsTransforming || ability.IsDisguised))
                { ability.Suspended = false; continue; }
                ability.Suspended = true;
                enemy.GetComponent<EnemyPresentation>().Tick(Time.deltaTime, enemy.Color, 0);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            DeveloperOptions.PlayerInvincible = previousInvincible;
            SpawnOverride.Enabled = previousSpawnOverride;
            UnityEngine.Random.state = previousRandom;
            if (clockClip != null) Destroy(clockClip);
        }
    }
}
#endif
