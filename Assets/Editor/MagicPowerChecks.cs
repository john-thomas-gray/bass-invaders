using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MagicPowerChecks
    {
        public static void Run()
        {
            CheckTextLayering();
            foreach (float frameStep in new[] { 1f / 30, 1f / 60, 1f / 120 })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var body = player.GetComponent<SpriteRenderer>();
                foreach (float x in new[] { -.17f, .17f })
                {
                    var pupil = new GameObject("Pupil", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    pupil.transform.SetParent(player.transform, false);
                    pupil.transform.localPosition = new Vector3(x, .2f, 0);
                    pupil.transform.localScale = Vector3.one * .085f;
                    pupil.sprite = body.sprite;
                }
                VisualUpgrade.Upgrade(player.gameObject);
                var art = CharacterVisuals.Ensure(player.gameObject);
                player.Configure(grid, tongue, art.Body);
                var survivor = ProgressionChecks.Add(grid, EnemyColor.Orange, 4, 0);
                var session = grid.gameObject.AddComponent<GameSession>();
                session.LevelCatalog = null;
                session.PresentationPreview = true;
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var music = session.GetComponent<GameplayMusicPlayer>();
                music.PreviewTime = 0;
                var cameraObject = new GameObject("Magic render check", typeof(Camera), typeof(MagicPowerPresentation));
                var scenery = new GameObject("Magic scenery check", typeof(SpriteRenderer));
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 7;
                camera.transform.position = Vector3.back * 10;
                scenery.transform.position = new Vector3(1, 2, 0);
                scenery.GetComponent<SpriteRenderer>().sprite = art.Body.sprite;
                var nested = new GameObject("Nested offset artwork", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                nested.transform.SetParent(scenery.transform, false);
                nested.transform.localPosition = new Vector3(.7f, -.4f);
                nested.transform.localScale = new Vector3(.3f, .2f, 1);
                var source = art.Body.sprite;
                var offsetSprite = Sprite.Create(source.texture, source.rect, Vector2.zero, source.pixelsPerUnit);
                nested.sprite = offsetSprite;
                var textObject = new GameObject("Magic world text check", typeof(TextMesh));
                var worldText = textObject.GetComponent<TextMesh>();
                worldText.text = "25";
                worldText.font = Resources.Load<Font>("Fonts/Bungee-Regular");
                worldText.fontSize = 64;
                worldText.characterSize = .08f;
                worldText.GetComponent<Renderer>().sharedMaterial = worldText.font.material;
                textObject.transform.position = new Vector3(-1, 2);
                var effect = cameraObject.GetComponent<MagicPowerPresentation>();
                effect.Configure(session, player);
                var target = new RenderTexture(390, 844, 24);
                camera.targetTexture = target;
                camera.Render();
                try
                {
                    var colors = EnemyPalette.Colors.Where(color => color != EnemyColor.Orange).ToArray();
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var cleared = ProgressionChecks.Add(grid, colors[i], 0, 4);
                        grid.ClearMatchingChain(cleared.Id, colors[i]);
                        Check(player.MagicCharges == Mathf.Min(i + 1, 2) && player.ActiveMagicBars == i + 1,
                            "Every lit bar counts even after stored charges reach their cap");
                        Check(Mathf.Abs(session.MagicTextGrowthScale - (1.10f + i * .05f)) < .00001f,
                            "The first bar adds 10 percent and every additional bar adds 5 percent at the beat peak");
                        for (float time = 0; time < .25f; time += frameStep) effect.Tick(frameStep);
                        Check(Mathf.Abs(effect.PupilScale - 1.4f) < .00001f, "Pupils settle at 140 percent at every tested frame rate");
                        CheckRender(camera, effect, session, player, survivor, scenery.transform);
                    }
                    CheckBorder(camera, effect, player);
                    var shock = player.gameObject.AddComponent<PlayerShockVisual>();
                    shock.Begin();
                    CheckRender(camera, effect, session, player, survivor, scenery.transform);
                    shock.ResetEffect();
                    music.PreviewTime = music.SecondsAtBeat(.5f);
                    Check(Mathf.Abs(session.MagicTextGrowthScale - 1) < .00001f &&
                        Mathf.Abs(session.MagicGrowthScale(true) - 1.30f) < .00001f,
                        "The player keeps its original offbeat while text and enemies return to normal between beats");
                    CheckRender(camera, effect, session, player, survivor, scenery.transform);
                    music.PreviewTime = 0;
                    session.Pause();
                    effect.Tick(1);
                    Check(effect.PupilScale == 1.4f, "Pause preserves dilation");
                    session.Resume();
                    Check(player.Fire() && player.MagicCharges == 0 && player.ActiveMagicBars == 5,
                        "Spending stored charges keeps the pulse active during the magic shot");
                    var line = tongue.GetComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    tongue.Tick(.01f, grid);
                    var mouth = camera.WorldToViewportPoint(tongue.transform.position);
                    var tip = camera.WorldToViewportPoint(tongue.TipBulb.transform.position);
                    var bulbSize = ScreenSize(camera, tongue.TipBulb);
                    float lineWidth = line.widthMultiplier;
                    effect.ApplyPresentation();
                    try
                    {
                        Check(Vector2.Distance(camera.WorldToViewportPoint(tongue.transform.position), mouth) < .00001f &&
                            Vector2.Distance(camera.WorldToViewportPoint(tongue.TipBulb.transform.position),
                                tip) < .00001f &&
                            Vector2.Distance(ScreenSize(camera, tongue.TipBulb), (Vector2)bulbSize) < .00001f &&
                            Mathf.Abs(line.widthMultiplier - lineWidth) < .00001f,
                            "Text growth leaves the tongue width, bulb, reach and travel at their original magic-shot values");
                    }
                    finally { effect.RestorePresentation(); }
                    tongue.Cancel();
                    Check(!player.MagicPowerActive && session.MagicTextGrowthScale == 1, "The pulse ends when spent magic finishes");
                    effect.Tick(.1f);
                    Check(effect.PupilScale > 1 && effect.PupilScale < 1.4f, "Pupils ease back instead of snapping");
                    float paused = effect.PupilScale;
                    effect.Tick(0); effect.Tick(-1);
                    Check(effect.PupilScale == paused, "Zero and negative time cannot advance dilation");
                    effect.Tick(.2f);
                    Check(effect.PupilScale == 1, "Pupils return exactly to normal");
                    var originalProjection = camera.projectionMatrix;
                    var renewed = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 4);
                    grid.ClearMatchingChain(renewed.Id, EnemyColor.Red);
                    effect.Tick(.2f);
                    effect.ApplyPresentation(); effect.enabled = false;
                    Check(camera.projectionMatrix == originalProjection && effect.PupilScale == 1,
                        "Disabling an active effect restores projection and clears dilation");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(scenery);
                    UnityEngine.Object.DestroyImmediate(offsetSprite);
                    UnityEngine.Object.DestroyImmediate(textObject);
                    UnityEngine.Object.DestroyImmediate(target);
                }
            });
            Debug.Log("Magic power checks passed: layered text and actor growth beyond charge cap, independent alternating inner-border pulses, fixed outer rim, unchanged weapon geometry, pupil dilation, stable hitboxes, shot lifetime, pause and 30/60/120 frames per second.");
        }

        private static Vector3 ScreenSize(Camera camera, SpriteRenderer renderer) =>
            camera.WorldToViewportPoint(renderer.bounds.max) - camera.WorldToViewportPoint(renderer.bounds.min);

        private static void CheckTextLayering()
        {
            float[] peaks = { 1.10f, 1.15f, 1.20f, 1.25f, 1.30f, 1.35f };
            for (int bars = 1; bars <= peaks.Length; bars++)
            {
                Check(Mathf.Abs(MagicPowerPresentation.TextGrowthScale(bars, 0) - peaks[bars - 1]) < .00001f &&
                    MagicPowerPresentation.TextGrowthScale(bars, .5f) == 1,
                    "All six lit-bar growth percentages apply only at pulse peaks");
            }
            foreach (int offset in new[] { 0, 1, 3 })
            for (int beat = 0; beat < 8; beat++)
            {
                bool even = GameplayMusicPlayer.BeatInMeasure(beat, offset) % 2 == 0;
                Check(Mathf.Abs(PlayfieldFrame.InnerPulseScale(6, beat, even, offset) - 1.4715f) < .00001f &&
                    PlayfieldFrame.InnerPulseScale(6, beat, !even, offset) == 1 &&
                    PlayfieldFrame.InnerPulseScale(6, beat + .5f, even, offset) == 1 &&
                    PlayfieldFrame.InnerPulseScale(0, beat, even, offset) == 1,
                    "Inner border pulses alternate on displayed odd/even beats, including song offsets, and stop without magic");
            }
            var basis = Matrix4x4.TRS(new Vector3(20, 40), Quaternion.Euler(0, 0, 15), new Vector3(2, 2, 1));
            foreach (var rect in new[] { new Rect(10, 20, 90, 24), new Rect(800, 500, 20, 16), new Rect(300, 100, 180, 12) })
            foreach (float scale in new[] { 1.10f, 1.15f, 1.35f })
            {
                var anchor = new Vector3(rect.xMin, rect.center.y);
                var animated = basis * Matrix4x4.Translate(anchor) * Matrix4x4.Scale(Vector3.one * GameSession.GameplayTextPulseScale(0)) * Matrix4x4.Translate(-anchor);
                var pulsed = MagicPowerPresentation.ElementMatrix(animated, rect, scale);
                Check(Vector3.Distance(animated.MultiplyPoint3x4(rect.center), basis.MultiplyPoint3x4(rect.center)) > .01f,
                    "The original pulse still moves the text from its original anchor");
                Check(Vector3.Distance(pulsed.MultiplyPoint3x4(rect.center), animated.MultiplyPoint3x4(rect.center)) < .001f,
                    "Extra text growth preserves the original pulse position instead of removing or increasing its movement");
                var corner = (Vector3)rect.max;
                var center = animated.MultiplyPoint3x4(rect.center);
                Check(Vector3.Distance(pulsed.MultiplyPoint3x4(corner) - center,
                    (animated.MultiplyPoint3x4(corner) - center) * scale) < .001f,
                    "The added text size layers onto the original pulse");
            }
            var safe = new Rect(4, 4, 312, 232);
            foreach (var text in new[] { new Rect(2, 20, 180, 24), new Rect(235, 20, 80, 24) })
            {
                var inset = MagicPowerPresentation.PeakTextInset(text, safe, text.center, 1);
                var matrix = MagicPowerPresentation.ElementMatrix(Matrix4x4.identity, text,
                    MagicPowerPresentation.TextGrowthScale(6, 0));
                var min = (Vector2)matrix.MultiplyPoint3x4(text.min) + inset;
                var max = (Vector2)matrix.MultiplyPoint3x4(text.max) + inset;
                Check(safe.Contains(min) && safe.Contains(max - Vector2.one * .001f),
                    "The larger maximum text bonus stays readable at both edges of a narrow screen");
            }
        }

        private static void CheckBorder(Camera camera, MagicPowerPresentation effect, PlayerMovement player)
        {
            var root = new GameObject("Magic border exclusion check", typeof(LineRenderer), typeof(PlayfieldFrame));
            var pixels = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
            int previousMask = camera.cullingMask;
            var previousActive = RenderTexture.active;
            var music = player.Music;
            float? previousTime = music.PreviewTime;
            try
            {
                var boundary = root.GetComponent<LineRenderer>();
                boundary.useWorldSpace = false;
                boundary.loop = true;
                boundary.positionCount = 4;
                boundary.SetPositions(new[] { new Vector3(-3, -5.5f), new Vector3(-3, 5.5f),
                    new Vector3(3, 5.5f), new Vector3(3, -5.5f) });
                boundary.sharedMaterial = player.GetComponent<CharacterVisuals>().Body.sharedMaterial;
                var frame = root.GetComponent<PlayfieldFrame>();
                frame.Configure(player);
                var outline = root.transform.Find("Cabinet outline").GetComponent<LineRenderer>();
                var originalOutline = Enumerable.Range(0, outline.positionCount).Select(outline.GetPosition).ToArray();
                foreach (float beat in new[] { 0f, .1f, .5f, .9f, 1f, 1.5f, 2f, 3f })
                {
                    music.PreviewTime = music.SecondsAtBeat(beat);
                    frame.RefreshBeat(beat);
                    float edge = PlayfieldFrame.InnerPulseScale(player.ActiveMagicBars, beat, false,
                        music.CurrentDownbeatOffsetBeats, PlayfieldFrame.InnerEdgePeakScale);
                    float brackets = PlayfieldFrame.InnerPulseScale(player.ActiveMagicBars, beat, true, music.CurrentDownbeatOffsetBeats);
                    Check(Mathf.Abs(boundary.startWidth - PlayfieldFrame.BoundaryLineWidth * edge) < .00001f &&
                        outline.startWidth == PlayfieldFrame.OutlineLineWidth && outline.transform.localScale == Vector3.one &&
                        Enumerable.Range(0, outline.positionCount).All(i => outline.GetPosition(i) == originalOutline[i]),
                        "The inner edge swells independently while the outer outline remains fixed");
                    for (int i = 0; i < 4; i++)
                    {
                        var corner = root.transform.Find("Cabinet corner " + i).GetComponent<LineRenderer>();
                        float side = i < 2 ? -1 : 1, end = i % 2 == 0 ? -1 : 1;
                        var center = new Vector3(side * 2.725f, end * 5.225f);
                        Check(Vector3.Distance(corner.transform.TransformPoint(center), center) < .00001f &&
                            Mathf.Abs(corner.transform.localScale.x - brackets) < .00001f &&
                            Mathf.Abs(corner.startWidth - PlayfieldFrame.CornerLineWidth * brackets) < .00001f,
                            "All four corners grow about their own centers on the alternating beat");
                    }
                }
                var previousCelebration = player.CelebrationColor;
                try
                {
                    music.PreviewTime = music.SecondsAtBeat(music.CurrentDownbeatOffsetBeats);
                    player.SetCelebrationColor(Color.cyan);
                    frame.Refresh();
                    Check(boundary.startWidth > PlayfieldFrame.BoundaryLineWidth * 3,
                        "The regular refresh path keeps the odd-beat inner pulse visible during celebration colors");
                    CheckVisibleInnerPulse(camera, frame, boundary, player);
                }
                finally { player.SetCelebrationColor(previousCelebration); }
                foreach (var piece in root.GetComponentsInChildren<Transform>()) piece.gameObject.layer = 31;
                camera.cullingMask = 1 << 31;
                foreach (float beat in new[] { 0f, .5f })
                {
                    music.PreviewTime = music.SecondsAtBeat(beat);
                    frame.RefreshBeat(beat);
                    effect.enabled = false;
                    camera.Render();
                    RenderTexture.active = camera.targetTexture;
                    pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                    var original = pixels.GetPixels32();
                    effect.enabled = true;
                    effect.Tick(.25f);
                    camera.Render();
                    pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                    var powered = pixels.GetPixels32();
                    int changed = 0, visible = 0;
                    for (int i = 0; i < original.Length; i++)
                    {
                        if (original[i].r + original[i].g + original[i].b > 20) visible++;
                        if (Math.Abs(original[i].r - powered[i].r) + Math.Abs(original[i].g - powered[i].g) +
                            Math.Abs(original[i].b - powered[i].b) > 6) changed++;
                    }
                    Check(visible > 100 && changed < 5,
                        $"Actor growth cannot move the separately animated border: beat={beat}, changed={changed}");
                }
            }
            finally
            {
                camera.cullingMask = previousMask;
                RenderTexture.active = previousActive;
                music.PreviewTime = previousTime;
                effect.enabled = true;
                effect.Tick(.25f);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckVisibleInnerPulse(Camera camera, PlayfieldFrame frame, LineRenderer boundary,
            PlayerMovement player)
        {
            var lines = frame.GetComponentsInChildren<LineRenderer>();
            var enabled = lines.Select(line => line.enabled).ToArray();
            int mask = camera.cullingMask;
            var clear = camera.clearFlags;
            var background = camera.backgroundColor;
            var active = RenderTexture.active;
            var pixels = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
            foreach (var piece in frame.GetComponentsInChildren<Transform>()) piece.gameObject.layer = 31;
            try
            {
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                int offset = player.Music.CurrentDownbeatOffsetBeats;
                foreach (int part in new[] { 0, 1, 2 })
                {
                    foreach (var line in lines)
                        line.enabled = part == 0 ? line == boundary : part == 1 ? line.name.StartsWith("Cabinet corner") :
                            line.name == "Cabinet outline";
                    Color32[][] frames = new Color32[4][];
                    for (int beat = 0; beat < frames.Length; beat++)
                    {
                        player.Music.PreviewTime = player.Music.SecondsAtBeat(offset + beat);
                        // Exercise the real refresh path, including celebration-color priority.
                        frame.Refresh();
                        camera.Render();
                        RenderTexture.active = camera.targetTexture;
                        pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                        frames[beat] = pixels.GetPixels32();
                    }
                    var ink = frames.Select(colors => colors.Count(color => color.r + color.g + color.b > 60)).ToArray();
                    Check(ink.All(count => count > 50), "Each isolated border element is visible in the rendered check");
                    if (part == 0)
                        Check(ink[0] > ink[1] * 2.5f && ink[2] > ink[3] * 2.5f,
                            "Rendered inner-edge thickness visibly swells on beats one and three during celebration colors");
                    else if (part == 1)
                        Check(ink[1] > ink[0] * 1.25f && ink[3] > ink[2] * 1.25f,
                            "Rendered corners visibly swell on beats two and four");
                    else Check(frames.Skip(1).All(colors => colors.SequenceEqual(frames[0])),
                        "The rendered outermost outline remains identical through all four size pulses");
                }
            }
            finally
            {
                for (int i = 0; i < lines.Length; i++) lines[i].enabled = enabled[i];
                camera.cullingMask = mask;
                camera.clearFlags = clear;
                camera.backgroundColor = background;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void CheckRender(Camera camera, MagicPowerPresentation effect, GameSession session,
            PlayerMovement player, GridEnemy enemy, Transform scenery)
        {
            var art = player.GetComponent<CharacterVisuals>();
            art.RefreshBeat(player.Music.BeatPosition, true);
            enemy.Visuals.RefreshBeat(player.Music.BeatPosition, false);
            var pupil = art.Root.GetComponentsInChildren<SpriteRenderer>().First(part => part.name == "Pupil");
            var originalProjection = camera.projectionMatrix;
            var playerSize = ScreenSize(camera, art.Body);
            var enemySize = ScreenSize(camera, enemy.Visuals.Body);
            var playerPosition = camera.WorldToViewportPoint(art.Body.bounds.center);
            var enemyPosition = camera.WorldToViewportPoint(enemy.Visuals.Body.bounds.center);
            var pupilSize = ScreenSize(camera, pupil);
            var decorations = scenery.GetComponentsInChildren<SpriteRenderer>();
            var decorationCenters = decorations.Select(part => camera.WorldToViewportPoint(part.bounds.center)).ToArray();
            var decorationSizes = decorations.Select(part => ScreenSize(camera, part)).ToArray();
            var worldLabel = GameObject.Find("Magic world text check").GetComponent<Renderer>();
            var textCenter = camera.WorldToViewportPoint(worldLabel.bounds.center);
            var textSize = camera.WorldToViewportPoint(worldLabel.bounds.max) - camera.WorldToViewportPoint(worldLabel.bounds.min);
            var playerBounds = art.HitBounds;
            var enemyBounds = enemy.HitBounds;
            Vector3 renderedPlayerSize = default, renderedEnemySize = default, renderedPupilSize = default;
            Vector3 renderedPlayerPosition = default, renderedEnemyPosition = default;
            Vector3[] renderedCenters = null, renderedSizes = null;
            Vector3 renderedTextCenter = default, renderedTextSize = default;
            bool unchangedBounds = false, observed = false, unchangedCamera = false;
            Camera.CameraCallback observe = current =>
            {
                if (current != camera) return;
                observed = true;
                renderedPlayerSize = ScreenSize(camera, art.Body);
                renderedEnemySize = ScreenSize(camera, enemy.Visuals.Body);
                renderedPupilSize = ScreenSize(camera, pupil);
                renderedPlayerPosition = camera.WorldToViewportPoint(art.Body.bounds.center);
                renderedEnemyPosition = camera.WorldToViewportPoint(enemy.Visuals.Body.bounds.center);
                renderedCenters = decorations.Select(part => camera.WorldToViewportPoint(part.bounds.center)).ToArray();
                renderedSizes = decorations.Select(part => ScreenSize(camera, part)).ToArray();
                unchangedCamera = camera.projectionMatrix == originalProjection;
                renderedTextCenter = camera.WorldToViewportPoint(worldLabel.bounds.center);
                renderedTextSize = camera.WorldToViewportPoint(worldLabel.bounds.max) - camera.WorldToViewportPoint(worldLabel.bounds.min);
                unchangedBounds = art.HitBounds == playerBounds && enemy.HitBounds == enemyBounds;
            };
            Camera.onPreRender += observe;
            try { camera.Render(); }
            finally { Camera.onPreRender -= observe; }
            Check(observed && Vector2.Distance(renderedPlayerSize, (Vector2)playerSize * session.MagicGrowthScale(true)) < .00001f &&
                Vector2.Distance(renderedEnemySize, (Vector2)enemySize * session.MagicTextGrowthScale) < .00001f &&
                Vector3.Distance(renderedPlayerPosition, playerPosition) < .00001f &&
                Vector3.Distance(renderedEnemyPosition, enemyPosition) < .00001f,
                "Actors receive the bonus on their original beats without adding position movement");
            Check(Vector2.Distance(renderedPupilSize, (Vector2)pupilSize * 1.4f * session.MagicGrowthScale(true)) < .00001f,
                "Pupils remain 140 percent relative to the enlarged player artwork");
            Check(unchangedCamera && unchangedBounds && decorations.Select((part, i) =>
                Vector3.Distance(renderedCenters[i], decorationCenters[i]) < .00001f &&
                Vector2.Distance(renderedSizes[i], (Vector2)decorationSizes[i]) < .00001f).All(value => value),
                "Non-text artwork retains its original size, position and animation while the camera stays unchanged");
            Check(Vector3.Distance(renderedTextCenter, textCenter) < .00001f &&
                Vector2.Distance(renderedTextSize, (Vector2)textSize * session.MagicTextGrowthScale) < .00001f,
                "World multiplier text receives the additional size without changing its existing movement");
            Check(camera.projectionMatrix == originalProjection &&
                Vector3.Distance(ScreenSize(camera, pupil), pupilSize) < .00001f &&
                art.HitBounds == playerBounds && enemy.HitBounds == enemyBounds,
                "Every render restores the camera, authored pupil sizes and collision state");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Magic power check failed: " + message); }
    }
}
