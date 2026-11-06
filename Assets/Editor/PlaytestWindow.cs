using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public sealed class PlaytestWindow : EditorWindow
    {
        private PlaytestOptions options;
        private string profileName = "skilled", message = "";
        private readonly HashSet<string> selected = new HashSet<string>();
        private Vector2 scroll;
        [MenuItem("Candy Cruisers/Difficulty Playtesting")]
        public static void Open() => GetWindow<PlaytestWindow>("Difficulty Playtesting").Show();
        private void OnEnable() { options = PlaytestOptions.Load(); minSize = new Vector2(420, 450); }
        private void OnGUI()
        {
            options ??= PlaytestOptions.Load();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Record a human run, select its summary below, and calibrate a profile. Autonomous policies approximate the recorded behavior; inspect the batch results against human benchmarks before drawing difficulty conclusions.", MessageType.Info);
            options.participant = EditorGUILayout.TextField("Participant label", options.participant);
            if (GUILayout.Button("Arm human recording and open game")) BeginPlaytest("human");
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Autonomous experiments", EditorStyles.boldLabel);
            options.seed = EditorGUILayout.IntField("First seed", options.seed);
            options.runs = Mathf.Clamp(EditorGUILayout.IntField("Runs per scenario", options.runs), 1, 1000);
            options.startLevel = Mathf.Clamp(EditorGUILayout.IntField("Starting stage", options.startLevel), 1, 99);
            options.limitSeconds = Mathf.Clamp(EditorGUILayout.FloatField("Seconds per run", options.limitSeconds), 1, 36000);
            options.targetLevel = Mathf.Max(0, EditorGUILayout.IntField("Stop at stage (0 disables)", options.targetLevel));
            options.bootstrap = EditorGUILayout.Toggle("Skilled fallback policy", options.bootstrap != "novice") ? "skilled" : "novice";
            EditorGUILayout.LabelField("Fallbacks and stages with fewer than five completed shots are reported as unmeasured or sparse.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Profile: " + (string.IsNullOrEmpty(options.profileFile) ? "Unmeasured " + options.bootstrap : Path.GetFileName(options.profileFile)));
            if (GUILayout.Button("Choose calibrated profile"))
            {
                string path = EditorUtility.OpenFilePanel("Select playtest profile", PlaytestOptions.Root, "json");
                if (!string.IsNullOrEmpty(path)) options.profileFile = path;
            }
            if (GUILayout.Button("Use unmeasured fallback only")) options.profileFile = "";
            if (GUILayout.Button("Add level-map scenario"))
            {
                string path = EditorUtility.OpenFilePanel("Select level map", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path); LevelCatalog.Parse(json);
                        string directory = Path.Combine(PlaytestOptions.Root, "Scenarios"); Directory.CreateDirectory(directory);
                        string name = Path.GetFileNameWithoutExtension(path) + "-" + Hash128.Compute(json).ToString().Substring(0, 8) + ".json";
                        File.WriteAllText(Path.Combine(directory, name), json);
                        var files = new List<string>(options.scenarioFiles ?? Array.Empty<string>());
                        string relative = Path.Combine("Scenarios", name);
                        if (!files.Contains(relative)) files.Add(relative);
                        options.scenarioFiles = files.ToArray();
                    }
                    catch (Exception error) { message = error.Message; }
                }
            }
            foreach (string scenario in options.scenarioFiles ?? Array.Empty<string>()) EditorGUILayout.LabelField(Path.GetFileName(scenario));
            if (GUILayout.Button("Use current level map only")) options.scenarioFiles = Array.Empty<string>();
            if (GUILayout.Button("Run autonomous batch (" + options.TotalRuns + " runs)")) BeginPlaytest("bot");
            var harness = Application.isPlaying ? UnityEngine.Object.FindFirstObjectByType<PlaytestHarness>() : null;
            if (harness != null)
            {
                EditorGUILayout.HelpBox(harness.Status, MessageType.None);
                if (GUILayout.Button("Stop batch / save recording"))
                {
                    harness.Finish(); options = PlaytestOptions.Load(); options.autoStart = false; options.Save(); harness.ReloadOptions();
                }
            }
            if (GUILayout.Button("Disable playtesting"))
            {
                harness?.Finish(); options.mode = "off"; options.autoStart = false; options.showPanel = false; options.Save();
                harness?.ReloadOptions();
            }
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Calibrate selected human recordings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select runs from one participant and one configuration. One run seeds a profile; repeated sessions are needed to measure variation. Calibration rejects assisted and automated runs.", MessageType.Info);
            profileName = EditorGUILayout.TextField("Profile name", profileName);
            string root = Path.Combine(PlaytestOptions.Root, "Runs");
            if (Directory.Exists(root))
            foreach (string path in Directory.GetFiles(root, "summary.json", SearchOption.AllDirectories))
            {
                try
                {
                    var run = JsonUtility.FromJson<PlaytestRunData>(File.ReadAllText(path));
                    if (run.controller != "human") continue;
                    bool next = EditorGUILayout.ToggleLeft(run.participant + " | " + run.utc + " | stage " + run.finalLevel + (run.assisted ? " | ASSISTED" : ""), selected.Contains(path));
                    if (next) selected.Add(path); else selected.Remove(path);
                }
                catch (Exception error) { EditorGUILayout.LabelField("Unreadable run: " + error.Message); }
            }
            if (GUILayout.Button("Create profile from selection"))
            {
                try
                {
                    var runs = new List<PlaytestRunData>();
                    foreach (string path in selected) runs.Add(JsonUtility.FromJson<PlaytestRunData>(File.ReadAllText(path)));
                    var profile = PlaytestProfile.Calibrate(profileName, runs);
                    string directory = Path.Combine(PlaytestOptions.Root, "Profiles"); Directory.CreateDirectory(directory);
                    string name = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
                    if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Enter a profile name.");
                    string output = Path.Combine(directory, name + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json");
                    File.WriteAllText(output, JsonUtility.ToJson(profile, true));
                    options.profileFile = output; options.Save(); message = "Saved " + output;
                }
                catch (Exception error) { message = error.Message; }
            }
            if (GUILayout.Button("Open recordings, profiles and batch reports"))
            { Directory.CreateDirectory(PlaytestOptions.Root); EditorUtility.RevealInFinder(PlaytestOptions.Root); }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        private void BeginPlaytest(string mode)
        {
            if (Application.isPlaying)
            {
                var harness = UnityEngine.Object.FindFirstObjectByType<PlaytestHarness>();
                harness?.Finish();
                options.mode = mode; options.autoStart = mode == "bot"; options.nextRun = 0;
                options.batchId = mode == "bot" ? Guid.NewGuid().ToString("N") : ""; options.showPanel = false; options.Save();
                harness?.ReloadOptions();
                UnityEngine.Object.FindFirstObjectByType<GameSession>()?.ReturnToMenu();
                return;
            }
            options.mode = mode; options.autoStart = mode == "bot"; options.nextRun = 0;
            options.batchId = mode == "bot" ? Guid.NewGuid().ToString("N") : ""; options.showPanel = false; options.Save();
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
