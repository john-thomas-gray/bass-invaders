using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SpecialEnemyChecks
    {
        public static void CaptureShockPreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var art = CharacterVisuals.Ensure(player.gameObject);
            player.gameObject.AddComponent<PlayerSlimeVisual>().Configure(art);
            player.transform.position = Vector3.zero;
            player.SetCelebrationColor(EnemyPalette.Get(EnemyColor.Green));
            GameObject.Find("Playfield Border").GetComponent<PlayfieldFrame>().RefreshPlayerClip();
            var camera = Camera.main;
            camera.GetComponent<GameplayFraming>().enabled = false;
            camera.transform.position = new Vector3(0, .1f, -10);
            camera.orthographicSize = .8f; camera.aspect = 1;
            camera.backgroundColor = new Color(.07f, .07f, .09f);
            camera.ResetProjectionMatrix();
            var effect = player.gameObject.AddComponent<PlayerShockVisual>();
            effect.Begin(); effect.Tick(.04f); CaptureShockFrame(camera, "shock-impact");
            effect.Tick(.15f); CaptureShockFrame(camera, "shock-soot");
            effect.BeginRecovery(); effect.Tick(.07f); CaptureShockFrame(camera, "shock-blink");
            effect.Tick(.3f); CaptureShockFrame(camera, "shock-cinders");
            Debug.Log("Shock presentation previews captured.");
        }
        private static void CaptureShockFrame(Camera camera, string name)
        {
            var target = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory("TestResults");
                System.IO.File.WriteAllBytes("TestResults/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var effects = new System.Collections.Generic.List<EnemyDeathBurst>();
            try
            {
                Add(grid, EnemyColor.Blue, 0, 0);
                Add(grid, EnemyColor.Blue, 1, 0);
                Add(grid, EnemyColor.Blue, 1, 1);
                Add(grid, EnemyColor.Red, 3, 0);
                Add(grid, EnemyColor.Red, 4, 0);
                Add(grid, EnemyColor.Red, 4, 1);
                foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(0);
                grid.RefreshSpecials();
                for (int x = 0; x < 5; x++)
                {
                    var enemy = Add(grid, EnemyColor.Purple, x, 4);
                    var effect = EnemyDeathBurst.Create(enemy.GetComponentInChildren<SpriteRenderer>(), EnemyColor.Purple, Mathf.Abs(x - 2) + 1);
                    effect.Tick(.18f);
                    effects.Add(effect);
                    grid.Unregister(enemy);
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                }
                var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                player.RefreshColor();
                GameplayChecks.Capture(540, 960, "special-enemies-portrait");
                GameplayChecks.Capture(960, 540, "special-enemies-landscape");
                GameplayChecks.Capture(390, 844, "special-enemies-phone");
                foreach (var effect in effects)
                {
                    var text = effect.GetComponentInChildren<TextMesh>();
                    Check(text.text == "" || text.GetComponent<MeshRenderer>().bounds.size.x > .05f, "Visible death digit has renderable glyph geometry");
                }
            }
            finally
            {
                foreach (var effect in effects) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            }
            Debug.Log("Special enemy previews rendered in portrait, landscape and tall phone views.");
        }

        public static void Run()
        {
            CheckFeedingTendrils();
            CheckShockVisual();
            var model = new GridModel();
            model.TryAdd(1, EnemyColor.Red, 2, 2);
            model.TryAdd(2, EnemyColor.Red, 1, 2);
            model.TryAdd(3, EnemyColor.Red, 3, 2);
            model.TryAdd(4, EnemyColor.Red, 2, 1);
            model.TryAdd(5, EnemyColor.Red, 1, 1);
            model.TryAdd(6, EnemyColor.Yellow, 3, 1);
            model.BeginImitation(6, 3);
            var depths = model.MatchingDepths(1, EnemyColor.Red);
            Check(depths[1] == 1 && depths[2] == 2 && depths[3] == 2 && depths[4] == 2 && depths[5] == 3 && depths[6] == 3,
                "Death digits use shortest connection depth, including branches, loops and Yellow links");
            Check(model.MatchingDepths(1, EnemyColor.Blue).Count == 0 && model.Count == 6, "Depth preview neither kills nor accepts a mismatch");
            Check(model.ClearMatchingChain(1, EnemyColor.Red).Count == 6 && model.Count == 0, "Every planned branch clears exactly once");

            Fixture((grid, player, tongue) =>
            {
                var red = Add(grid, EnemyColor.Red, 4, 0);
                player.RefreshColor();
                var blue = Add(grid, EnemyColor.Blue, 0, 0);
                Add(grid, EnemyColor.Blue, 1, 0);
                grid.RefreshSpecials();
                Check(!blue.IsSpecial && grid.GroupShield.EdgeCount == 0, "A Blue pair remains ordinary");
                var lower = Add(grid, EnemyColor.Blue, 1, 1);
                grid.RefreshSpecials();
                Check(blue.Tier == 2 && lower.Tier == 2 && !red.IsSpecial && grid.GroupShield.EdgeCount == 8,
                    "Three connected Blues upgrade and produce only the eight exterior edges of an L group");
                Check(blue.GetComponentInChildren<SpriteRenderer>().sprite == grid.GetComponent<EnemyRowSpawner>().SpecialSprite &&
                    !blue.Visuals.Root.Find("Shield").GetComponentInChildren<SpriteRenderer>().enabled,
                    "Tier two uses special art and replaces individual shields with a group perimeter");
                player.transform.position = new Vector3(lower.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Fire a mismatched ordinary shot at the rigid shield");
                var beforeShock = player.DisplayColor;
                int shockArrivals = 0;
                tongue.ShieldShockArrived += () => shockArrivals++;
                for (int i = 0; i < 1000 && !player.Stunned; i++) tongue.Tick(.001f, grid);
                Check(tongue.ShockActive && player.Stunned && player.DisplayColor == beforeShock && shockArrivals == 0,
                    "Shield contact launches electricity without graying the player before arrival");
                var core = tongue.transform.Find("Shield shock core").GetComponent<LineRenderer>();
                var glow = tongue.transform.Find("Shield shock glow").GetComponent<LineRenderer>();
                float firstY = core.GetPosition(0).y;
                float phase = tongue.ShockProgress;
                tongue.Tick(0, grid); tongue.Tick(-1, grid);
                Check(tongue.ShockProgress == phase, "Paused and negative ticks cannot advance the shield shock");
                tongue.Tick(tongue.ShockDuration / 2, grid);
                Check(tongue.ShockActive && core.enabled && glow.enabled && core.GetPosition(0).y < firstY &&
                    core.startColor.b > .9f && glow.startWidth > core.startWidth && player.DisplayColor == beforeShock,
                    "Blue-white electricity travels from the shield toward the still-colored player");
                var trail = tongue.GetComponent<LineRenderer>().colorGradient;
                Check(trail.Evaluate(.95f) == Color.gray && trail.Evaluate(.05f) != Color.gray,
                    "Only the tongue behind the traveling shock becomes gray");
                tongue.Tick(tongue.ShockDuration / 2, grid);
                Check(!tongue.ShockActive && !core.enabled && !glow.enabled && shockArrivals == 1,
                    "The electrical pulse arrives once and cleans up both visual layers");
                Check(tongue.Active && tongue.Retracting && player.Stunned && player.DisplayColor == Color.white && grid.Model.Count == 4,
                    "Rigid shield survives and flashes the player white on electrical impact");
                player.GetComponent<PlayerShockVisual>().Tick(PlayerShockVisual.FlashSeconds + .01f);
                Check(player.DisplayColor == Color.black, "Electrical impact leaves the player soot black");
                var position = player.transform.position;
                player.Move(1, 1);
                player.BeginPointer(Vector2.zero);
                Check(player.transform.position == position && !player.Fire(), "Stun blocks movement and firing");
                tongue.Tick(1, grid);
                Check(!player.Stunned && !tongue.Active && player.DisplayColor != Color.gray && player.Alive && !player.Invulnerable,
                    "Retraction restores color and controls without a death or invulnerability period");
                player.Move(1, .1f);
                Check(player.transform.position != position, "Movement resumes after tongue return");
                player.transform.position = position;
                // The return now selects a new ready color; explicitly repeat the mismatched shot.
                tongue.TryFire(EnemyColor.Red, 10); tongue.Tick(.26f, grid);
                Check(player.Stunned, "Second mismatch also deflects without breaking shield");
                player.CancelShot();
                Check(!player.Stunned && !tongue.ShockActive && !core.enabled && !glow.enabled,
                    "Cancellation cannot strand the player in grey stun or leave electricity visible");
                tongue.TryFire(EnemyColor.Blue, 10);
                tongue.Tick(1, grid);
                Check(grid.Model.ColorCount(EnemyColor.Blue) == 0 && grid.GroupShield.EdgeCount == 0 && !player.Stunned && !tongue.ShockActive,
                    "Matching Blue passes through and clears the shielded group and outline");
            });

            Fixture((grid, player, tongue) =>
            {
                var a = Add(grid, EnemyColor.Blue, 0, 0);
                Add(grid, EnemyColor.Blue, 1, 0);
                Add(grid, EnemyColor.Blue, 2, 0);
                var separate = Add(grid, EnemyColor.Blue, 4, 2);
                grid.RefreshSpecials();
                Check(!grid.GroupShield.Protects(separate.Id), "Disconnected Blue group is not shielded by another group");
                foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.BeginSpawnEffect();
                grid.RefreshSpecials();
                Check(grid.GroupShield.EdgeCount == 0, "Rigid shield waits for arrival and power-up");
                foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(1.15f);
                grid.RefreshSpecials();
                Check(grid.GroupShield.EdgeCount == 8, "Three-cell line has only its exterior perimeter after power-up");
                var perimeter = grid.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(grid.GroupShield.ContourCount == 1 && perimeter.vertexCount == grid.GroupShield.EdgeCount * 9,
                    "Horizontal perimeter joins share vertices instead of overlapping edge ends");
                player.transform.position = new Vector3(a.transform.position.x, -4.6f, 0);
                tongue.TryFire(EnemyColor.Red, 10, true);
                tongue.Tick(1, grid);
                Check(grid.Model.ColorCount(EnemyColor.Blue) == 1 && !player.Stunned, "Magic retains its shield bypass");
            });

            Fixture((grid, player, tongue) =>
            {
                var red = Add(grid, EnemyColor.Red, 1, 0);
                Add(grid, EnemyColor.Red, 2, 0);
                Add(grid, EnemyColor.Red, 3, 0);
                Add(grid, EnemyColor.Green, 0, 3);
                Add(grid, EnemyColor.Green, 1, 3);
                Add(grid, EnemyColor.Green, 2, 3);
                grid.RefreshSpecials();
                Check(red.IsSpecial && grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Green).All(e => e.IsSpecial),
                    "Red and Green groups both receive tier two");
                red.GetComponent<EnemyAbilities>().Tick(100);
                var homing = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Last(m => m.Aimed);
                Check(!homing.Homing, "Special Red launches an aimed straight shot, not a homing missile");
                // Retain checks for the unused legacy homing mode.
                homing.SetTarget(player, true);
                StartAtCruiseSpeed(homing);
                homing.transform.position = new Vector3(-2, 2, 0);
                player.transform.position = new Vector3(2, -4.6f, 0);
                homing.Tick(.4f);
                Check(homing.transform.position.x > -1.99f && homing.transform.position.x < -1.85f &&
                    Mathf.Abs(Mathf.DeltaAngle(0, homing.transform.eulerAngles.z) - 7.2f) < .01f &&
                    Mathf.Abs(homing.transform.position.y - .6f) < .02f,
                    "Homing turns directly toward the player with original gentle steering and 3.5-unit speed");
                homing.Suspended = true;
                var point = homing.transform.position;
                homing.Tick(1);
                Check(homing.transform.position == point, "Suspended homing missile cannot move");
                homing.Suspended = false;
                homing.Tick(6);
                Check(homing.Finished, "Homing missile collides or expires within its lifetime");
                player.TickSurvival(10);
                var coarse = new GameObject("Coarse homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                var fine = new GameObject("Fine homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                coarse.SetTarget(player, true); fine.SetTarget(player, true);
                StartAtCruiseSpeed(coarse); StartAtCruiseSpeed(fine);
                coarse.transform.position = fine.transform.position = new Vector3(-2, 2, 0);
                coarse.Tick(.5f);
                for (int i = 0; i < 60; i++) fine.Tick(1f / 120);
                Check(Vector3.Distance(coarse.transform.position, fine.transform.position) < .001f,
                    "Long-frame and stepped homing agree");
                UnityEngine.Object.DestroyImmediate(coarse.gameObject);
                UnityEngine.Object.DestroyImmediate(fine.gameObject);
                var limited = new GameObject("Limited homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                limited.SetTarget(player, true);
                StartAtCruiseSpeed(limited);
                limited.transform.position = new Vector3(-2.9f, 5.29f, 0);
                for (int step = 0; step < 360; step++)
                {
                    player.transform.position = new Vector3(limited.transform.position.x + 2, 10, 0);
                    limited.Tick(1f / 120);
                }
                Check(!limited.Finished && Mathf.Abs(Mathf.DeltaAngle(0, limited.transform.eulerAngles.z) - 50) < .01f,
                    "Homing tilt is capped at 50 degrees even when the target is above");
                float previousY = limited.transform.position.y;
                for (int step = 0; step < 60; step++)
                {
                    player.transform.position = new Vector3(limited.transform.position.x - 2, 10, 0);
                    limited.Tick(1f / 120);
                }
                Check(limited.transform.position.y < previousY &&
                    Mathf.Abs(Mathf.DeltaAngle(0, limited.transform.eulerAngles.z) - 41) < .01f,
                    "Reversing the target eases steering without looping upward");
                limited.Tick(3);
                Check(limited.Finished, "Missile leaving the bottom is retired permanently");
                UnityEngine.Object.DestroyImmediate(limited.gameObject);
                foreach (float side in new[] { -1f, 1f })
                {
                    var escaped = new GameObject("Offscreen homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                    escaped.SetTarget(player, true);
                    StartAtCruiseSpeed(escaped);
                    escaped.transform.position = new Vector3(side * (PlayerMovement.HalfWidth + .01f), 0, 0);
                    var outside = escaped.transform.position;
                    escaped.Tick(.5f);
                    Check(escaped.Finished && escaped.transform.position == outside,
                        "Inaudible offscreen homing missiles retire without wrapping back into the field");
                    UnityEngine.Object.DestroyImmediate(escaped.gameObject);
                }
                player.transform.position = new Vector3(2, -4.6f, 0);
                var incoming = new GameObject("Incoming homing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                incoming.SetTarget(player, true);
                StartAtCruiseSpeed(incoming);
                incoming.transform.position = player.transform.position + Vector3.up * 2;
                incoming.Tick(1);
                Check(incoming.Finished && !player.Alive, "Swept homing collision cannot tunnel through the player");
                UnityEngine.Object.DestroyImmediate(incoming.gameObject);
                var ordinary = new GameObject("Straight missile", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                ordinary.SetTarget(player);
                StartAtCruiseSpeed(ordinary);
                Check(ordinary.SmokeTrail != null && !ordinary.SmokeTrail.enabled, "Ordinary missile smoke trail starts empty");
                ordinary.transform.position = new Vector3(-2, 2, 0);
                ordinary.Tick(.5f);
                Check(ordinary.transform.position.x == -2 && !ordinary.Homing, "Ordinary missile stays straight");
                var smoke = ordinary.SmokeTrail;
                Check(smoke != null && smoke.enabled && smoke.positionCount == 4, "Missiles draw a smoke trail");
                Check(Mathf.Abs(smoke.startWidth - smoke.endWidth) < .001f &&
                    Mathf.Abs(smoke.startWidth - EnemyMissile.FallbackMissileWidth * EnemyMissile.SmokeTrailWidthRatio) < .001f,
                    "Missile smoke trail keeps a uniform half-missile width");
                Vector3 smokeTail = smoke.GetPosition(0), smokeHead = smoke.GetPosition(smoke.positionCount - 1);
                float screenThird = (Camera.main != null && Camera.main.orthographic ?
                    Camera.main.orthographicSize * 2 : EnemyMissile.FallbackViewHeight) * EnemyMissile.SmokeTrailScreenRatio;
                Check(Vector3.Distance(smokeTail, smokeHead) < screenThird &&
                    Mathf.Abs(Vector3.Distance(smokeTail, smokeHead) - 2.5f) < .02f,
                    "Ordinary missile smoke trail grows from flight distance before reaching full length");
                ordinary.Tick(.4f);
                smokeTail = smoke.GetPosition(0); smokeHead = smoke.GetPosition(smoke.positionCount - 1);
                Check(Mathf.Abs(Vector3.Distance(smokeTail, smokeHead) - screenThird) < .02f,
                    "Missile smoke trail reaches about one third of the screen");
                Check(Vector3.Angle(smokeHead - smokeTail, Vector3.down) < .05f,
                    "Ordinary missile smoke trail follows its heading");
                UnityEngine.Object.DestroyImmediate(ordinary.gameObject);
                var flashing = new GameObject("Flashing homing", typeof(SpriteRenderer), typeof(EnemyMissile));
                var renderer = flashing.GetComponentInChildren<SpriteRenderer>();
                renderer.color = EnemyPalette.Get(EnemyColor.Red);
                var original = renderer.color;
                var pulse = flashing.GetComponent<EnemyMissile>();
                StartAtCruiseSpeed(pulse);
                // This fallback-tempo check must not borrow the live scene's song clock.
                var flashClock = flashing.AddComponent<GameplayMusicPlayer>();
                flashClock.enabled = false;
                flashClock.StopPlayback();
                flashClock.Source.clip = null;
                pulse.ConfigureFlashClock(flashClock);
                flashing.transform.position = Vector3.up * 4.5f;
                pulse.SetTarget(null, true);
                float halfFlash = 30f / GameplayMusicPlayer.DefaultBeatsPerMinute * EnemyMissile.DefaultFarFlashBeats;
                pulse.Tick(halfFlash);
                Check(renderer.color.g > original.g + .5f && renderer.color.a == original.a,
                    "Homing missile brightens on its beat flash without becoming transparent");
                var bright = renderer.color;
                pulse.Suspended = true;
                pulse.Tick(1);
                Check(renderer.color == bright, "Suspension freezes the flash with the missile");
                pulse.Suspended = false;
                pulse.Tick(halfFlash);
                Check(Vector4.Distance(renderer.color, original) < .02f, "Beat pulse returns to its original color after one far flash cycle");
                pulse.SetTarget(null, false);
                pulse.Tick(halfFlash);
                Check(renderer.color.g > original.g + .5f, "Ordinary missiles share the beat flash");
                UnityEngine.Object.DestroyImmediate(flashing);
            });

            foreach (var color in new[] { EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow })
                Fixture((grid, player, tongue) =>
                {
                    var first = Add(grid, color, 0, 0);
                    Add(grid, color, 1, 0);
                    var diagonal = Add(grid, color, 2, 1);
                    grid.RefreshSpecials();
                    Check(!first.IsSpecial && !diagonal.IsSpecial, "Pairs and diagonals do not promote " + color);
                    Add(grid, color, 1, 1);
                    grid.RefreshSpecials();
                    var specialSprite = grid.GetComponent<EnemyRowSpawner>().SpecialSprite;
                    Check(grid.GetComponentsInChildren<GridEnemy>().All(e => e.IsSpecial &&
                        e.GetComponentInChildren<SpriteRenderer>().sprite == specialSprite), "Connected group promotes every " + color);
                    grid.SetColor(first.Id, EnemyColor.Red);
                    Check(!first.IsSpecial, "Changing color resets the upgraded tier");
                });

            Fixture((grid, player, tongue) =>
            {
                var movement = grid.GetComponent<EnemyGridMovement>();
                var green = Add(grid, EnemyColor.Green, 0, 0);
                Add(grid, EnemyColor.Green, 1, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                ability.Tick(100);
                Check(!ability.IsCasting && !ability.Warning && !movement.UseGreenDashes && grid.transform.position == Vector3.zero,
                    "Ordinary Greens remain count-based without warnings or dashes");
                Add(grid, EnemyColor.Green, 2, 0);
                grid.RefreshSpecials();
                ability.Tick(0);
                Check(!ability.IsCasting && !ability.Warning, "Promotion starts a fresh cooldown instead of releasing an overdue dash");
                Check(Mathf.Abs(movement.CurrentSpeed - .09f) < .001f, "Special Greens still contribute normal count-based movement");
                for (int i = 0; i < 160 && !ability.IsCasting; i++) ability.Tick(.1f);
                Check(ability.IsCasting && ability.Warning && grid.transform.position == Vector3.zero, "Special Green warns before its dash");
                ability.Suspended = true;
                ability.Tick(5);
                Check(ability.IsCasting && grid.transform.position == Vector3.zero, "Suspension freezes the warning");
                ability.Suspended = false;
                ability.Tick(.6f);
                ability.Tick(.18f);
                Check(Mathf.Abs(grid.transform.position.x - .1f) < .001f && !movement.UseGreenDashes,
                    "Tier-two Green restores the short fast-forward step without enabling global legacy mode");
                grid.SetColor(green.Id, EnemyColor.Red);
                ability.Tick(0);
                var position = grid.transform.position;
                ability.Tick(.18f);
                Check(grid.transform.position == position, "Changing color cancels remaining Green movement");
            });

            Fixture((grid, player, tongue) =>
            {
                var source = Add(grid, EnemyColor.Red, 0, 0).GetComponentInChildren<SpriteRenderer>();
                var first = EnemyDeathBurst.Create(source, EnemyColor.Red, 1);
                var third = EnemyDeathBurst.Create(source, EnemyColor.Blue, 3);
                try
                {
                    Check(first.Exploded && !third.Exploded, "Impact explodes before the outer branches");
                    third.Tick(EnemyDeathBurst.RingDelay * 2);
                    Color digitColor = third.GetComponentInChildren<TextMesh>().color;
                    Check(third.Exploded && third.GetComponentInChildren<TextMesh>().text == "3" &&
                        Vector4.Distance(digitColor, EnemyPalette.Get(EnemyColor.Blue)) < .01f, "Branch becomes its own colored digit");
                    Check(third.GetComponentInChildren<GridEnemy>() == null && third.GetComponentInChildren<EnemyAbilities>() == null,
                        "Death placeholders cannot fire, be hit or score again");
                    first.Tick(1); third.Tick(1);
                    Check(first.Finished && third.Finished, "Death effects cleanly finish");
                }
                finally
                {
                    if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
                    if (third != null) UnityEngine.Object.DestroyImmediate(third.gameObject);
                }
            });
            CheckGreenSpin();
            Debug.Log("Special enemy checks passed: branching digits, cascade timing, promotion, Green activation spin, contours, deflection, stun recovery, matching/magic bypass and homing.");
        }

        private static void CheckFeedingTendrils()
        {
            Fixture((grid, player, tongue) =>
            {
                var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                Add(grid, EnemyColor.Yellow, 1, 0); Add(grid, EnemyColor.Yellow, 2, 0);
                var target = Add(grid, EnemyColor.Purple, 0, 1);
                grid.RefreshSpecials();
                var ability = yellow.GetComponent<EnemyAbilities>();
                Check(ability.BeginImitation(target), "Special Yellow starts its organic disguise");
                ability.Tick(.5f);
                var lines = yellow.GetComponentsInChildren<LineRenderer>()
                    .Where(line => line.name.StartsWith("Disguise feeding tendril")).ToArray();
                Check(lines.Length == 5 && lines.All(line => line.enabled && line.positionCount == 25),
                    "Special Yellow draws multiple curved feeding tendrils");
                Check(!grid.Model.TryGetImitationTarget(yellow.Id, out _),
                    "Feeding artwork does not create a regular Yellow combat link");
                var before = lines[0].colorGradient.colorKeys.Select(key => key.color).ToArray();
                ability.Tick(.25f);
                Check(!lines[0].colorGradient.colorKeys.Select(key => key.color).SequenceEqual(before),
                    "Neighbor color visibly travels through the tendrils");
                ability.Tick(EnemyAbilities.ImitationSeconds - .75f);
                Check(!ability.IsTransforming && ability.IsDisguised && lines.All(line => !line.enabled),
                    "Tendrils end at the normal imitation duration while the disguise remains");
                ability.RevealDisguise(EnemyColor.Red);
                Check(lines.All(line => !line.enabled), "Revealing a disguise leaves no feeding artwork behind");
            });
        }

        private static void CheckGreenSpin()
        {
            Fixture((grid, player, tongue) =>
            {
                var green = Add(grid, EnemyColor.Green, 0, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                var body = green.Visuals.Body.transform;
                var resting = Quaternion.Euler(0, 0, 17);
                body.localRotation = resting;
                ability.Tick(100);
                Check(body.localRotation == resting, "Ordinary count-based Greens do not spin");
                var neighbor = Add(grid, EnemyColor.Green, 1, 0);
                Add(grid, EnemyColor.Green, 2, 0);
                grid.RefreshSpecials();
                ability.Tick(0);
                ability.Tick(ability.CooldownRemaining);
                Check(ability.IsCasting && body.localRotation == resting, "Special Green waits until activation to spin");
                var localPosition = green.transform.localPosition;
                var boundsSize = green.HitBounds.size;
                var boundsOffset = green.HitBounds.center - green.transform.position;
                ability.Tick(10);
                Check(!ability.IsCasting && body.localRotation == resting,
                    "A long frame completing the windup cannot consume the new activation spin");
                float cooldown = ability.CooldownRemaining;
                ability.Tick(EnemyPresentation.GreenSpinSeconds / 4);
                Check(Quaternion.Angle(body.localRotation, resting * Quaternion.Euler(0, 0, -90)) < .01f,
                    "The activating special Green turns a quarter revolution");
                Check(neighbor.Visuals.Body.transform.localRotation == Quaternion.identity,
                    "The dash does not spin other fleet members");
                Check(green.transform.localPosition == localPosition && green.transform.localRotation == Quaternion.identity &&
                    green.HitBounds.size == boundsSize &&
                    Vector3.Distance(green.HitBounds.center - green.transform.position, boundsOffset) < .0001f,
                    "Spin changes only artwork, preserving grid placement and collision dimensions");
                var pausedRotation = body.localRotation;
                var pausedPosition = grid.transform.position;
                float pausedCooldown = ability.CooldownRemaining;
                ability.Suspended = true;
                ability.Tick(5);
                Check(body.localRotation == pausedRotation && grid.transform.position == pausedPosition &&
                    ability.CooldownRemaining == pausedCooldown, "Pause freezes spin, dash and cooldown together");
                ability.Suspended = false;
                ability.Tick(-1);
                ability.Tick(0);
                Check(body.localRotation == pausedRotation, "Nonpositive time does not advance the spin");
                ability.Tick(EnemyPresentation.GreenSpinSeconds / 4);
                Check(Quaternion.Angle(body.localRotation, resting * Quaternion.Euler(0, 0, -180)) < .01f,
                    "Resuming continues the spin from its paused angle");
                ability.Tick(EnemyPresentation.GreenSpinSeconds / 2);
                Check(Quaternion.Angle(body.localRotation, resting) < .01f &&
                    Mathf.Abs(grid.transform.position.x - .1f) < .0001f &&
                    Mathf.Abs(ability.CooldownRemaining - (cooldown - EnemyPresentation.GreenSpinSeconds)) < .0001f,
                    "One full spin restores the authored pose without changing dash distance or cooldown duration");
                ability.Tick(ability.CooldownRemaining);
                ability.Tick(.6f);
                ability.Tick(.05f);
                Check(Quaternion.Angle(body.localRotation, resting) > 1, "Each later special activation spins again");
                grid.SetColor(green.Id, EnemyColor.Red);
                ability.Tick(0);
                Check(Quaternion.Angle(body.localRotation, resting) < .01f, "Changing color clears a partial Green spin");
            });
            foreach (float step in new[] { .01f, .05f, 2f })
                Fixture((grid, player, tongue) =>
                {
                    var green = Add(grid, EnemyColor.Green, 0, 0);
                    var presentation = green.GetComponent<EnemyPresentation>();
                    var body = green.Visuals.Body.transform;
                    var resting = body.localRotation;
                    presentation.DashGreen(1);
                    presentation.Tick(.01f, EnemyColor.Green, 0);
                    var trails = green.GetComponentsInChildren<LineRenderer>().Where(line => line.name.StartsWith("Green dash trail")).ToArray();
                    Check(trails.Length == 5 && trails.All(line => line.enabled &&
                        line.GetPosition(2).x < line.GetPosition(0).x && line.endColor.a == 0),
                        "Green dash streaks trail opposite rightward travel and fade along their length");
                    presentation.SetDashDirection(-.1f); presentation.Tick(.01f, EnemyColor.Green, 0);
                    Check(trails.All(line => line.GetPosition(2).x > line.GetPosition(0).x),
                        "Dash trails follow a reversal in actual movement");
                    presentation.SpinGreen();
                    float elapsed = 0;
                    while (elapsed < EnemyPresentation.GreenSpinSeconds)
                    {
                        presentation.Tick(step, EnemyColor.Green, 0);
                        elapsed += step;
                    }
                    Check(Quaternion.Angle(body.localRotation, resting) < .01f, "Spin completes with short or long frames");
                    presentation.Tick(1, EnemyColor.Green, 0);
                    Check(trails.All(line => !line.enabled), "Dash streaks clean up after fading");
                    presentation.SpinGreen();
                    presentation.Tick(.05f, EnemyColor.Green, 0);
                    var ability = green.GetComponent<EnemyAbilities>();
                    ability.enabled = false;
                    if (!Application.isPlaying) typeof(EnemyAbilities).GetMethod("OnDisable",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ability, null);
                    Check(Quaternion.Angle(body.localRotation, resting) < .01f, "Disabling an ability restores its artwork pose");
                });
            Fixture((grid, player, tongue) =>
            {
                grid.GetComponent<EnemyGridMovement>().UseGreenDashes = true;
                var green = Add(grid, EnemyColor.Green, 0, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                ability.Tick(ability.CooldownRemaining);
                ability.Tick(.6f);
                ability.Tick(.05f);
                Check(green.Visuals.Body.transform.localRotation == Quaternion.identity,
                    "Ordinary legacy dashes do not gain the special-only spin");
            });
        }

        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void CheckShockVisual()
        {
            Fixture((grid, player, tongue) =>
            {
                VisualUpgrade.Upgrade(player.gameObject);
                var art = CharacterVisuals.Ensure(player.gameObject);
                player.Configure(grid, tongue, art.Body);
                var eye = new GameObject("Eye", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                var pupil = new GameObject("Pupil", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                eye.transform.SetParent(art.Root, false); pupil.transform.SetParent(art.Root, false);
                eye.sprite = pupil.sprite = art.Body.sprite;
                eye.sortingOrder = art.Body.sortingOrder + 1;
                pupil.sortingOrder = art.Body.sortingOrder + 2;
                eye.transform.localScale = Vector3.one * .2f; pupil.transform.localScale = Vector3.one * .08f;
                var eyeScale = eye.transform.localScale; var pupilScale = pupil.transform.localScale;
                var bounds = player.HitBounds; var position = player.transform.position;
                var shock = player.gameObject.AddComponent<PlayerShockVisual>();
                shock.Begin();
                Check(player.DisplayColor == Color.white && pupil.transform.localScale.x < pupilScale.x * .4f,
                    "Shock impact flashes brightly and contracts pupils");
                shock.Tick(.13f);
                Check(player.DisplayColor == Color.black && shock.GetComponentsInChildren<LineRenderer>().Length >= 4,
                    "Blackened player has an impact outline and smoke trails");
                shock.BeginRecovery(); shock.Tick(.07f);
                Check(eye.transform.localScale.y < eyeScale.y * .1f,
                    "The soot recovery begins with a blink");
                shock.Tick(.2f);
                var pieces = player.GetComponentsInChildren<SpriteRenderer>().Where(part => part.name == "Soot cinder").ToArray();
                Check(pieces.Length == 30 && pieces.Any(part => part.color.a < 1) && pieces.Any(part => part.color.a == 1),
                    "Soot breaks off in staggered pieces while some still cover the new color");
                Check(pieces.All(part => part.transform.IsChildOf(art.Root) && part.sortingOrder < eye.sortingOrder &&
                    part.sortingOrder < pupil.sortingOrder), "All soot shares the player's rendering group and stays behind the face");
                var lifeArt = new LifeSlimeArtwork(); lifeArt.Bind(art);
                Check(lifeArt.PartCount == 3, "Temporary soot never becomes part of extra-life artwork");
                lifeArt.Dispose();
                var pose = pieces[0].transform.position;
                shock.Tick(0); shock.Tick(-1);
                Check(pieces[0].transform.position == pose, "Pause cannot advance the cinders");
                shock.Tick(1);
                Check(!shock.Active && pupil.transform.localScale == pupilScale && eye.transform.localScale == eyeScale &&
                    player.HitBounds == bounds && player.transform.position == position,
                    "Recovery restores the face without changing physics or movement");
                Check(eye.sortingOrder == art.Body.sortingOrder + 1 && pupil.sortingOrder == art.Body.sortingOrder + 2,
                    "Recovery restores the authored eye and pupil draw orders");
                shock.Begin(); player.ResetForRun();
                Check(!shock.Active && pupil.transform.localScale == pupilScale, "Restart clears soot, smoke and pupil changes");
            });
        }

        internal static void StartAtCruiseSpeed(EnemyMissile missile)
        {
            // Steering, boundary and trail checks isolate cruising behavior.
            // AimedRedChecks covers the prefab's backward launch and acceleration.
            var settings = new SerializedObject(missile);
            settings.FindProperty("launchSpeedRatio").floatValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Fixture(Action<EnemyGrid, PlayerMovement, TongueShot> check)
        {
            var random = UnityEngine.Random.state;
            var existingMissiles = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
            var root = new GameObject("Special fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var controller = new GameObject("Special test player", typeof(PlayerMovement), typeof(SpriteRenderer));
            var mouth = new GameObject("Special test tongue", typeof(LineRenderer), typeof(TongueShot));
            mouth.transform.SetParent(controller.transform, false);
            controller.transform.position = Vector3.down * 4.6f;
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                spawner.ConfigureSpecialSprite(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/space-invader-sentinel.png"));
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                var body = controller.GetComponentInChildren<SpriteRenderer>();
                body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PlayerPlaceholder.png");
                var player = controller.GetComponent<PlayerMovement>();
                player.Configure(grid, mouth.GetComponent<TongueShot>(), body);
                check(grid, player, mouth.GetComponent<TongueShot>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var missile in UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!existingMissiles.Contains(missile)) UnityEngine.Object.DestroyImmediate(missile.gameObject);
                UnityEngine.Random.state = random;
            }
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Special enemy check failed: " + message); }
    }
}
