using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace CandyCruisers
{
    // Attached at runtime in the Editor and development builds. Never arms itself.
    [DefaultExecutionOrder(100)]
    public sealed class PlaytestHarness : MonoBehaviour
    {
        public PlaytestOptions Options { get; private set; }
        public PlaytestRecorder Recorder { get; private set; }
        public PlaytestDriver Driver { get; private set; }
        public string Status { get; private set; } = "Idle";
        private GameSession session;
        private PlayerMovement player;
        private bool finishing, prepared;
        private string configuration, configurationHash;
        private UnityEngine.Random.State originalRandomState;
        private bool restoreRandom;
        private bool previousBackground;
        private string[] profiles = Array.Empty<string>();
        private Vector2 scroll;
        private GUIStyle panelButton, panelLabel, panelText;
        private static bool launchOptionsConsumed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLaunch() => launchOptionsConsumed = false;
        public bool IsAutonomous => Options != null && Options.mode == "bot";
        private void OnEnable()
        {
            if (!Application.isPlaying || !DeveloperOptions.Available) return;
            var owner = GetComponent<GameSession>();
            if (owner != null && owner.Player != null) Initialize(owner, owner.Player);
        }
        public static PlaytestHarness Attach(GameSession session, PlayerMovement player)
        {
            if (!DeveloperOptions.Available || player == null) return null;
            var harness = session.GetComponent<PlaytestHarness>();
            if (harness == null) harness = session.gameObject.AddComponent<PlaytestHarness>();
            if (harness.session != session || harness.player != player || harness.Options == null)
                harness.Initialize(session, player);
            return harness;
        }
        private void Initialize(GameSession owner, PlayerMovement controller)
        {
            session = owner; player = controller;
            Options = PlaytestOptions.Load();
            if (!launchOptionsConsumed)
            {
                launchOptionsConsumed = true;
                string launch = Environment.GetEnvironmentVariable("BASS_INVADERS_PLAYTEST_OPTIONS");
                if (!string.IsNullOrEmpty(launch))
                {
                    try { Options = JsonUtility.FromJson<PlaytestOptions>(launch); Options.Save(); }
                    catch (Exception error) { Debug.LogError("Invalid launch playtest options: " + error.Message); Options = new PlaytestOptions(); }
                }
            }
            previousBackground = Application.runInBackground;
            session.IsAutomatedPlaytest = IsAutonomous;
            player.AutonomousControl = IsAutonomous;
        }
        public void ReloadOptions()
        {
            if (Recorder != null) throw new InvalidOperationException("Stop recording before changing the playtest setup.");
            Options = PlaytestOptions.Load();
            session.IsAutomatedPlaytest = IsAutonomous; player.AutonomousControl = IsAutonomous;
            RefreshProfiles();
        }
        public void PrepareRun()
        {
            if (Options.mode == "off" || !DeveloperOptions.Available) return;
            if (IsAutonomous)
            {
                originalRandomState = UnityEngine.Random.state; restoreRandom = true;
                UnityEngine.Random.InitState(unchecked(Options.seed + Options.nextRun % Mathf.Clamp(Options.runs, 1, 1000)));
                Application.runInBackground = true;
            }
            configuration = ConfigurationSnapshot(session.LevelCatalog);
            configurationHash = Hash128.Compute(configuration).ToString();
            prepared = true;
        }
        public static string ConfigurationSnapshot(LevelCatalog catalog)
        {
            if (catalog == null) return "procedural-default";
            return JsonConvert.SerializeObject(catalog.levels.Select(level => new {
                background = level.backgroundImage != null ? level.backgroundImage.name : "",
                level.possibleSoundtrackTracks, level.fleets
            }));
        }
        public void BeginRecording()
        {
            if (!prepared || Options.mode == "off") return;
            prepared = false;
            try
            {
                PlaytestProfile profile = null;
                if (IsAutonomous && !string.IsNullOrEmpty(Options.profileFile))
                {
                    string path = Resolve(Options.profileFile);
                    profile = JsonUtility.FromJson<PlaytestProfile>(File.ReadAllText(path));
                    if (profile == null || profile.schemaVersion != 1 || profile.stages == null)
                        throw new InvalidDataException("Unsupported playtest profile.");
                }
                bool assisted = DeveloperOptions.PlayerInvincible;
#if UNITY_EDITOR
                assisted |= SpawnOverride.Enabled;
#endif
                var data = new PlaytestRunData {
                    id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    utc = DateTime.UtcNow.ToString("o"), participant = Options.participant,
                    controller = IsAutonomous ? "bot" : "human", platform = Application.platform.ToString(),
                    gameVersion = Application.version, buildId = Application.buildGUID,
                    inputDevice = Application.isMobilePlatform ? "touch" : "keyboard-or-pointer",
                    startLevel = session.Progress.Level, configuration = configuration, configurationHash = configurationHash,
                    initialRandomState = JsonUtility.ToJson(UnityEngine.Random.state), assisted = assisted,
                    seed = IsAutonomous ? unchecked(Options.seed + Options.nextRun % Mathf.Clamp(Options.runs, 1, 1000)) : 0,
                    batchId = Options.batchId, profileName = profile?.name ?? (IsAutonomous ? "unmeasured-" + Options.bootstrap : "")
                };
                Recorder = new PlaytestRecorder(session, player, data, PlaytestOptions.Root);
                if (profile != null) File.WriteAllText(Path.Combine(Recorder.DirectoryPath, "profile.json"), JsonUtility.ToJson(profile, true));
                Driver = IsAutonomous ? new PlaytestDriver(player, session.GetComponent<EnemyGrid>(), profile,
                    Options.bootstrap != "novice", data.seed, configurationHash) : null;
                Status = "Recording " + data.participant;
                Debug.Log("Playtest recording: " + Recorder.DirectoryPath);
            }
            catch (Exception error) { Fail(error); }
        }
        private static string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.Combine(PlaytestOptions.Root, path);
        private void LateUpdate()
        {
            if (session == null || Options == null || !DeveloperOptions.Available) return;
            session.PlaytestPanelOpen = Options.showPanel && (session.State == GameSession.RunState.MainMenu || session.IsPaused);
            if (Options.autoStart && IsAutonomous && Recorder == null && !finishing &&
                session.State == GameSession.RunState.MainMenu && !session.OpeningWarningActive && !session.PlaytestPanelOpen)
            {
                try
                {
                    if (Options.nextRun >= Options.TotalRuns) { Options.autoStart = false; Options.Save(); return; }
                    if (Options.scenarioFiles != null && Options.scenarioFiles.Length > 0)
                        session.LevelCatalog = LevelCatalog.Parse(File.ReadAllText(Resolve(Options.scenarioFiles[Options.nextRun / Mathf.Clamp(Options.runs, 1, 1000)])));
                    session.StartLevel = Options.startLevel;
                    session.StartRun();
                }
                catch (Exception error) { Fail(error); }
            }
            if (Recorder == null) return;
            if (!session.BlocksGameplayInput)
            {
                Driver?.Tick(Time.deltaTime, session.Progress.Level);
                Recorder.Tick(Time.deltaTime);
            }
            if (session.State == GameSession.RunState.Dying || session.State == GameSession.RunState.GameOver)
                Finish("game_over", true);
            else if (IsAutonomous && Options.targetLevel > Options.startLevel && session.Progress.Level >= Options.targetLevel)
                Finish("target_reached", true);
            else if (IsAutonomous && Recorder.Data.seconds >= Mathf.Max(1, Options.limitSeconds))
                Finish("time_limit", true);
        }
        public void Finish(string outcome = "manual_stop", bool advanceBatch = false)
        {
            if (Recorder == null || finishing) return;
            finishing = true;
            var recorder = Recorder;
            Recorder = null; Driver = null;
            try
            {
                recorder.Finish(outcome);
                Status = "Saved " + recorder.DirectoryPath;
                Debug.Log(Status);
                if (IsAutonomous) PlaytestReport.WriteBatch(PlaytestOptions.Root, Options.batchId);
                if (IsAutonomous && advanceBatch && Options.autoStart)
                {
                    Options.nextRun++;
                    if (Options.nextRun >= Options.TotalRuns) Options.autoStart = false;
                    Options.Save();
                    if (Options.autoStart) session.ReturnToMenu();
                    else { session.Pause(); Status = "Batch finished. " + Options.nextRun + " runs saved."; }
                }
                else if (IsAutonomous)
                {
                    Options.autoStart = false; Options.Save();
                    if (session.State == GameSession.RunState.Playing || session.State == GameSession.RunState.Refilling) session.Pause();
                }
            }
            catch (Exception error) { Fail(error); }
            finally { RestoreRandom(); finishing = false; }
        }
        private void Fail(Exception error)
        {
            Status = "Playtest error: " + error.Message;
            Debug.LogError(Status);
            Options.autoStart = false; Options.mode = "off"; Options.Save();
            session.IsAutomatedPlaytest = false; player.AutonomousControl = false;
            Recorder?.Dispose(); Recorder = null; Driver = null; RestoreRandom();
            if (session.State == GameSession.RunState.Playing || session.State == GameSession.RunState.Refilling) session.Pause();
        }
        private void RestoreRandom()
        {
            if (restoreRandom) UnityEngine.Random.state = originalRandomState;
            restoreRandom = false; Application.runInBackground = previousBackground;
        }
        private void OnDisable()
        {
            if (Recorder != null && !finishing) Finish("scene_unloaded");
            RestoreRandom();
            if (session != null) session.PlaytestPanelOpen = false;
            if (player != null) player.AutonomousControl = false;
        }
        private void OnApplicationQuit() { if (Recorder != null) Finish("application_quit"); }
        private void RefreshProfiles()
        {
            string path = Path.Combine(PlaytestOptions.Root, "Profiles");
            profiles = Directory.Exists(path) ? Directory.GetFiles(path, "*.json") : Array.Empty<string>();
        }
        public static Rect PanelRect(Vector2 screen, Rect safe)
        {
            float width = Mathf.Min(420, Mathf.Max(1, safe.width - 16));
            return new Rect(safe.x + 8, screen.y - safe.yMax + 8, width, Mathf.Min(580, Mathf.Max(1, safe.height - 16)));
        }
        public bool MenuAvailable => DeveloperOptions.Available && Options != null && session != null &&
            !session.OpeningWarningActive && (session.State == GameSession.RunState.MainMenu || session.IsPaused);
        public static Rect AccessButtonScreenRect(Vector2 screen, Rect safe, bool mobile)
        {
            float scale = mobile ? Mathf.Max(1, screen.x / 430f) : 1;
            return new Rect(safe.x + 8 * scale, safe.y + 8 * scale,
                Mathf.Min(140 * scale, Mathf.Max(1, safe.width - 16 * scale)), 44 * scale);
        }
        public bool TryOpenPanelAtScreenPoint(Vector2 point)
        {
            if (!MenuAvailable || Options.showPanel || !AccessButtonScreenRect(new Vector2(Screen.width, Screen.height),
                Screen.safeArea, Application.isMobilePlatform).Contains(point)) return false;
            ShowPanel();
            return true;
        }
        public void ShowPanel()
        {
            if (!MenuAvailable) return;
            Options.showPanel = true; session.PlaytestPanelOpen = true; RefreshProfiles();
        }
        public void RecordHumanRun()
        {
            if (!MenuAvailable || session.State != GameSession.RunState.MainMenu || Recorder != null) return;
            Options.mode = "human"; Options.autoStart = false; Options.showPanel = false;
            Options.Save(); ReloadOptions(); session.PlaytestPanelOpen = false; session.StartRun();
        }
        public void DrawMenu()
        {
            if (!MenuAvailable) return;
            panelButton ??= new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 44 };
            panelLabel ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            panelText ??= new GUIStyle(GUI.skin.textField) { fontSize = 18, fixedHeight = 40 };
            var old = GUI.matrix;
            int oldDepth = GUI.depth;
            var oldColor = GUI.color;
            GUI.depth = -100;
            GUI.color = Color.white;
            float scale = Application.isMobilePlatform ? Mathf.Max(1, Screen.width / 430f) : 1;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var screen = new Vector2(Screen.width, Screen.height) / scale;
            var safe = new Rect(Screen.safeArea.x / scale, Screen.safeArea.y / scale, Screen.safeArea.width / scale, Screen.safeArea.height / scale);
            try
            {
                if (!Options.showPanel)
                {
                    var button = AccessButtonScreenRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, Application.isMobilePlatform);
                    var rect = new Rect(button.x / scale, (Screen.height - button.yMax) / scale, button.width / scale, button.height / scale);
                    if (GUI.Button(rect, "Playtesting", panelButton))
                        ShowPanel();
                    return;
                }
                var panel = PanelRect(screen, safe);
                var tint = GUI.color;
                GUI.color = new Color(0, 0, 0, .96f); GUI.DrawTexture(panel, Texture2D.whiteTexture); GUI.color = tint;
                GUILayout.BeginArea(panel, GUI.skin.box);
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label("DIFFICULTY PLAYTESTING", panelLabel);
                GUILayout.Label(Status, panelLabel);
                if (Recorder == null && session.State == GameSession.RunState.MainMenu)
                {
                    GUILayout.Label("Human recording label", panelLabel);
                    Options.participant = GUILayout.TextField(Options.participant, panelText);
                    if (GUILayout.Button("Record human run", panelButton))
                        RecordHumanRun();
                    GUILayout.Space(10);
                    GUILayout.Label("Autonomous profile", panelLabel);
                    if (GUILayout.Button("Unmeasured skilled policy", panelButton)) { Options.profileFile = ""; Options.bootstrap = "skilled"; }
                    if (GUILayout.Button("Unmeasured novice policy", panelButton)) { Options.profileFile = ""; Options.bootstrap = "novice"; }
                    foreach (string path in profiles)
                        if (GUILayout.Button(Path.GetFileNameWithoutExtension(path), panelButton)) Options.profileFile = path;
                    GUILayout.Label(string.IsNullOrEmpty(Options.profileFile) ? "Selected: unmeasured " + Options.bootstrap : Path.GetFileName(Options.profileFile), panelLabel);
                    if (GUILayout.Button("Run autonomous batch", panelButton))
                    {
                        Options.mode = "bot"; Options.autoStart = true; Options.nextRun = 0;
                        Options.batchId = Guid.NewGuid().ToString("N"); Options.showPanel = false; Options.Save(); ReloadOptions(); session.PlaytestPanelOpen = false;
                    }
                    GUILayout.Label("Batch: " + Options.TotalRuns + " runs, " + Options.limitSeconds + " seconds each. Configure seeds and scenarios in the Editor window.", panelLabel);
                }
                if (Recorder != null && GUILayout.Button("Stop and save recording", panelButton)) Finish();
                if (Recorder == null && GUILayout.Button("Disable playtesting", panelButton))
                { Options.mode = "off"; Options.autoStart = false; Options.Save(); ReloadOptions(); }
                GUILayout.Label("Saved under persistent data / Playtesting. Calibrate human runs using the Editor window.", panelLabel);
                if (GUILayout.Button("Close", panelButton)) { Options.showPanel = false; Options.Save(); session.PlaytestPanelOpen = false; }
                GUILayout.EndScrollView(); GUILayout.EndArea();
            }
            finally { GUI.matrix = old; GUI.depth = oldDepth; GUI.color = oldColor; }
        }
    }
}
