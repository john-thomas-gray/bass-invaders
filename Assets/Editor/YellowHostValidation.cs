using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class YellowHostValidation
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            YellowTransformationChecks.Run();
            AbilityBeatChecks.Run();
            SpecialEnemyChecks.Run();
            PurpleYellowSpecialChecks.Run();
            OrangeChecks.Run();
            ProgressionChecks.Run();
            Capture();
            Debug.Log("Yellow host validation passed.");
        }

        public static void BuildPlatform()
        {
            string scenePath = "Assets/__YellowHostPlatform.unity";
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Probe camera", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5.8f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                var probe = new GameObject("Yellow host platform checks").AddComponent<YellowHostPlatformProbe>();
                probe.Prefabs = new GameObject[6];
                foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                    probe.Prefabs[(int)color] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
                probe.Missile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab");
                probe.Shield = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png");
                EditorSceneManager.SaveScene(scene, scenePath);
                bool phone = System.Environment.GetEnvironmentVariable("YELLOW_HOST_PLATFORM") == "ios";
                var target = phone ? BuildTarget.iOS : BuildTarget.StandaloneOSX;
                if (phone)
                {
                    PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                    PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                }
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { scenePath }, target = target,
                    locationPathName = phone ? "/tmp/yellow-host-ios" : "/tmp/YellowHostProbe.app",
                    options = BuildOptions.Development });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new System.Exception("Yellow platform build failed: " + report.summary.result);
            }
            finally
            {
                AssetDatabase.DeleteAsset(scenePath);
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
                if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget);
            }
        }

        public static void CaptureRevealRegression()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var yellows = new GridEnemy[3];
            for (int i = 0; i < 3; i++) yellows[i] = ProgressionChecks.Add(grid, EnemyColor.Yellow, i, 3);
            var red = ProgressionChecks.Add(grid, EnemyColor.Red, 3, 3);
            ProgressionChecks.Add(grid, EnemyColor.Red, 4, 3);
            grid.RefreshSpecials();
            yellows[2].GetComponent<EnemyAbilities>().BeginImitation(red);
            yellows[1].GetComponent<EnemyAbilities>().BeginImitation(yellows[2]);
            yellows[0].GetComponent<EnemyAbilities>().BeginImitation(yellows[1]);
            GameplayChecks.Capture(960, 540, "yyyrr-before-reveal");
            grid.ResolveTongueHit(yellows[2].Id, EnemyColor.Red);
            foreach (var yellow in yellows) yellow.GetComponent<EnemyPresentation>().Tick(.6f, EnemyColor.Yellow, 0);
            GameplayChecks.Capture(960, 540, "yyyrr-after-reveal");
            GameplayChecks.Capture(390, 844, "yyyrr-after-reveal-phone");
        }

        public static void Capture()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            MimicMechanicContract.PopulateGallery(grid, (color, x, y) => ProgressionChecks.Add(grid, color, x, y));
            GameplayChecks.Capture(1280, 720, "yellow-host-desktop");
            GameplayChecks.Capture(390, 844, "yellow-host-phone");
        }
    }
}
