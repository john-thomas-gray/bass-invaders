using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerMeltDeathChecks
    {
        public static void RunFullValidation()
        {
            GameplayChecks.Run();
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            CapturePreview(true);
            RenderRisingPreview();
        }

        public static void RenderRisingPreview()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            Directory.CreateDirectory("TestResults/fatal-melt-rise-frames");
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var art = CharacterVisuals.Ensure(player.gameObject);
            player.gameObject.AddComponent<PlayerSlimeVisual>().Configure(art);
            var cover = GameOverBlackout.Create(null, null);
            cover.HoldPlayer(art.Root);
            var camera = Camera.main;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(540, 960, 24);
            var pixels = new Texture2D(540, 960, TextureFormat.RGB24, false);
            PresentationCue cue = null;
            const float riseSeconds = 2;
            try
            {
                camera.targetTexture = target;
                camera.GetComponent<GameplayFraming>().Refresh();
                for (int frame = 0; frame <= 144; frame++)
                {
                    float age = frame / 30f;
                    if (age < riseSeconds) cover.Present(Mathf.SmoothStep(0, 1, age / riseSeconds), age);
                    else
                    {
                        if (cue == null)
                        {
                            cover.Present(1, riseSeconds);
                            cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerMelt, art.Body, EnemyColor.Blue, 1);
                            cue.enabled = false;
                            cover.ShowDeath(cue);
                            foreach (var sprite in art.Root.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
                        }
                        cue.GetComponentInChildren<PlayerMeltDeath>().Present(age - riseSeconds);
                        cover.Present(1);
                    }
                    UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes("TestResults/fatal-melt-rise-frames/" + frame.ToString("D3") + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(cover.gameObject);
                if (cue != null) UnityEngine.Object.DestroyImmediate(cue.gameObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        public static void Run()
        {
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var art = CharacterVisuals.Ensure(player.gameObject);
            if (player.GetComponent<PlayerSlimeVisual>() == null)
                player.gameObject.AddComponent<PlayerSlimeVisual>().Configure(art);
            var sourceBounds = art.Body.bounds;
            var random = UnityEngine.Random.state;
            var livePupils = art.Root.GetComponentsInChildren<SpriteRenderer>()
                .Where(sprite => sprite.name == "Pupil").OrderBy(sprite => sprite.bounds.center.x).ToArray();
            var eyes = art.Root.GetComponentsInChildren<SpriteRenderer>()
                .Where(sprite => sprite.name == "Eye").OrderBy(sprite => sprite.bounds.center.x).ToArray();
            var cover = GameOverBlackout.Create(null, null);
            cover.HoldPlayer(art.Root, 2.13f);
            var initialOffsets = livePupils.Select((pupil, i) => pupil.transform.position - eyes[i].bounds.center).ToArray();
            cover.Present(.02f, .03f);
            var earlyOffsets = livePupils.Select((pupil, i) => pupil.transform.position - eyes[i].bounds.center).ToArray();
            Check(Vector2.SignedAngle(initialOffsets[0], earlyOffsets[0]) > 0 &&
                Vector2.SignedAngle(initialOffsets[1], earlyOffsets[1]) < 0,
                "The left pupil spins counterclockwise and the right pupil clockwise while rising");
            cover.Present(.8f, 1.8f);
            var nearTop = livePupils.Select(pupil => pupil.transform.position).ToArray();
            cover.Present(.999f, 2.12f);
            var incomingOffsets = livePupils.Select((pupil, i) => pupil.transform.position - eyes[i].bounds.center).ToArray();
            cover.Present(1, 2.13f);
            var finalOffsets = livePupils.Select((pupil, i) => pupil.transform.position - eyes[i].bounds.center).ToArray();
            var risingEnd = livePupils.Select(pupil => pupil.transform.position).ToArray();
            float centeredFloor = art.Body.bounds.min.y;
            var resting = livePupils.Select(pupil => player.GetComponent<PlayerSlimeVisual>()
                .DefaultPupilPosition(pupil.transform)).ToArray();
            Check(livePupils.Select((pupil, i) => Vector3.Distance(nearTop[i], pupil.transform.position) > .005f).All(moved => moved),
                "The pupils keep spinning all the way to the middle");
            var cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerMelt, art.Body, EnemyColor.Blue, 9);
            cue.enabled = false;
            cover.ShowDeath(cue);
            try
            {
                var melt = cue.GetComponentInChildren<PlayerMeltDeath>();
                var sprites = melt.GetComponentsInChildren<SpriteRenderer>();
                var pupils = sprites.Where(sprite => sprite.name.Contains("Pupil")).OrderBy(sprite => sprite.bounds.center.x).ToArray();
                var crosses = sprites.Where(sprite => sprite.name == "X eye").OrderBy(sprite => sprite.transform.position.x).ToArray();
                var body = sprites.Single(sprite => sprite.name == art.Body.name + " melt snapshot");
                var puddle = sprites.Single(sprite => sprite.name == "Melted puddle");
                Check(pupils.Length == 2 && crosses.Length == 2 && crosses.All(cross => !cross.enabled) &&
                    pupils.Select((pupil, i) => Vector3.Distance(pupil.transform.position, risingEnd[i]) < .0001f).All(same => same),
                    "The centered character continues from the rising eye pose without snapping");
                Check(cue.GetComponentInChildren<PlayerDeathBurst>() == null &&
                    cue.GetComponentInChildren<PlayerFatalDustBurst>() == null &&
                    cue.GetComponentsInChildren<LineRenderer>().Length == 0,
                    "The final death has no burst, dust or shockwave");
                var pivots = risingEnd.Select((position, i) => (position + resting[i]) * .5f).ToArray();
                var previous = risingEnd.Select((position, i) => position - pivots[i]).ToArray();
                var totalTurn = new float[2];
                melt.Present(.01f);
                for (int i = 0; i < 2; i++)
                {
                    float incoming = Vector2.SignedAngle(incomingOffsets[i], finalOffsets[i]);
                    float outgoing = Vector2.SignedAngle(previous[i], crosses[i].transform.position - pivots[i]);
                    Check(Mathf.Abs(incoming - outgoing) < .2f && Mathf.Abs(outgoing) > 4,
                        "The final turn continues the rising spin's direction and speed without stopping");
                }
                var roundMask = crosses[0].sprite.texture.GetPixels32();
                for (int frame = 1; frame <= 48; frame++)
                {
                    melt.Present(PlayerMeltDeath.FinalTurnSeconds * frame / 48);
                    for (int i = 0; i < 2; i++)
                    {
                        var offset = crosses[i].transform.position - pivots[i];
                        float step = Vector2.SignedAngle(previous[i], offset);
                        Check(i == 0 ? step > 0 : step < 0,
                            "Each pupil keeps orbiting in its original direction throughout the morph");
                        totalTurn[i] += step;
                        previous[i] = offset;
                    }
                }
                Check(Mathf.Abs(totalTurn[0] - 180) < .02f && Mathf.Abs(totalTurn[1] + 180) < .02f &&
                    crosses.Select((cross, i) => Vector3.Distance(cross.transform.position, resting[i]) < .0001f).All(centered => centered),
                    "Both pupils complete exactly 180 degrees and land on their default positions");
                melt.Present(PlayerMeltDeath.FinalTurnSeconds / 2);
                var middleMask = crosses[0].sprite.texture.GetPixels32();
                Check(crosses.Select((cross, i) => Vector3.Distance(cross.transform.position, resting[i]) > .005f).All(turning => turning),
                    "The round-to-X morph happens while the pupils are still turning");
                melt.Present(PlayerMeltDeath.FinalTurnSeconds);
                var finalMask = crosses[0].sprite.texture.GetPixels32();
                Check(roundMask.Zip(middleMask, (a, b) => a.a != b.a).Count(changed => changed) > 100 &&
                    middleMask.Zip(finalMask, (a, b) => a.a != b.a).Count(changed => changed) > 100,
                    "The pupil silhouette morphs progressively into an X instead of abruptly swapping sprites");
                Check(pupils.All(pupil => !pupil.enabled) && crosses.All(cross => cross.enabled && cross.color.a > .99f) &&
                    Mathf.Abs(crosses[0].bounds.size.x - eyes[0].bounds.size.x * .68f * .7f) < .001f,
                    "The finished X's are fully visible and have 30 percent less diameter");
                Check(finalMask.Skip(48 * 96).Take(96).Count(pixel => pixel.a > 127) > 22,
                    "The smaller X's retain visibly thicker strokes");
                Check(Vector3.Distance(body.bounds.size, sourceBounds.size) < .001f && !puddle.enabled,
                    "The X-eye hold precedes the body melt");
                melt.Present(PlayerMeltDeath.MeltStartSeconds);
                Check(!body.enabled && puddle.enabled && Vector3.Distance(puddle.bounds.size, sourceBounds.size) < .001f &&
                    Mathf.Abs(puddle.bounds.min.y - centeredFloor) < .001f,
                    "The smoosh starts from the body's exact size and floor with one continuous silhouette");
                var previousSize = puddle.transform.localScale;
                for (int frame = 1; frame <= 48; frame++)
                {
                    melt.Present(Mathf.Lerp(PlayerMeltDeath.MeltStartSeconds, PlayerMeltDeath.LinePeakSeconds, frame / 48f));
                    var size = puddle.transform.localScale;
                    Check(size.x > previousSize.x && size.y < previousSize.y,
                        "The silhouette keeps widening and flattening throughout the entire smoosh-to-line motion");
                    previousSize = size;
                }
                const float sample = .004f;
                melt.Present(PlayerMeltDeath.WhiteSeconds - sample);
                var sizeBefore = puddle.transform.localScale;
                melt.Present(PlayerMeltDeath.WhiteSeconds);
                var sizeAt = puddle.transform.localScale;
                melt.Present(PlayerMeltDeath.WhiteSeconds + sample);
                var sizeAfter = puddle.transform.localScale;
                var velocityIn = (sizeAt - sizeBefore) / sample;
                var velocityOut = (sizeAfter - sizeAt) / sample;
                Check(velocityIn.x > 0 && velocityOut.x > 0 && velocityIn.y < 0 && velocityOut.y < 0 &&
                    Mathf.Abs(velocityIn.x - velocityOut.x) / velocityIn.x < .06f &&
                    Mathf.Abs(velocityIn.y - velocityOut.y) / Mathf.Abs(velocityIn.y) < .06f,
                    "The old puddle-to-television boundary has no pause or change of velocity");
                melt.Present((PlayerMeltDeath.MeltStartSeconds + PlayerMeltDeath.WhiteSeconds) / 2);
                var halfWhite = Color.Lerp(art.Body.color, Color.white, .5f);
                Check(Vector3.Distance(new Vector3(puddle.color.r, puddle.color.g, puddle.color.b),
                    new Vector3(halfWhite.r, halfWhite.g, halfWhite.b)) < .001f,
                    "The body fades halfway from its original color to white during the smoosh");
                melt.Present(1.7f);
                Check(puddle.bounds.size.x > sourceBounds.size.x && puddle.bounds.size.y < sourceBounds.size.y * .7f &&
                    Mathf.Abs(puddle.bounds.min.y - centeredFloor) < .001f && puddle.enabled && !body.enabled,
                    "The body slumps outward from a fixed floor into a growing puddle");
                melt.Present(1.9f);
                Check(sprites.Count(sprite => sprite.enabled) == 1 && puddle.enabled,
                    "The flattened body and face clear before they can protrude as an extra line beside the puddle");
                Check(puddle.color == Color.white, "The smoosh is fully white before the television switch-off stretch");
                float smooshWidth = puddle.bounds.size.x, smooshHeight = puddle.bounds.size.y;
                melt.Present(PlayerMeltDeath.LinePeakSeconds);
                Check(puddle.bounds.size.x > smooshWidth && puddle.bounds.size.y <= smooshHeight &&
                    puddle.bounds.size.x > puddle.bounds.size.y * 30 &&
                    puddle.color == Color.white && sprites.Count(sprite => sprite.enabled) == 1,
                    "The same white silhouette continues stretching into one thin, bright horizontal line");
                var view = Camera.main;
                Check(view.WorldToViewportPoint(puddle.bounds.min).x <= 0 && view.WorldToViewportPoint(puddle.bounds.max).x >= 1,
                    "The white line reaches both screen edges at the current aspect ratio");
                float peakWidth = puddle.bounds.size.x;
                melt.Present((PlayerMeltDeath.LinePeakSeconds + PlayerMeltDeath.PointSeconds) / 2);
                Check(puddle.bounds.size.x < peakWidth * .4f && puddle.color == Color.white,
                    "The switch-off line contracts rapidly toward its center");
                melt.Present(PlayerMeltDeath.PointSeconds);
                Check(Mathf.Abs(puddle.bounds.size.x - puddle.bounds.size.y) < .001f && puddle.bounds.size.x < sourceBounds.size.x * .15f &&
                    puddle.color == Color.white && puddle.enabled,
                    "The line finishes as a small white point before its afterglow fades");
                melt.Present(PlayerMeltDeath.LinePeakSeconds);
                Check(!body.enabled && crosses.All(cross => !cross.enabled) && puddle.enabled &&
                    puddle.bounds.size.x > sourceBounds.size.x * 1.6f && puddle.bounds.size.y < sourceBounds.size.y * .2f,
                    "Only a wide, shallow puddle remains after the eyes sink");
                var settled = puddle.transform.localScale;
                foreach (float step in new[] { 1f / 30, 1f / 60, 1f / 120, .37f })
                {
                    float elapsed = 0;
                    while (elapsed < PlayerMeltDeath.LinePeakSeconds)
                    {
                        elapsed = Mathf.Min(PlayerMeltDeath.LinePeakSeconds, elapsed + step);
                        melt.Present(elapsed);
                    }
                    Check(puddle.transform.localScale == settled && !body.enabled,
                        "Puddle geometry is identical across desktop and phone frame rates");
                }
                melt.Present(PlayerMeltDeath.Duration - .1f);
                Check(puddle.color.a > 0 && puddle.color.a < 1, "The last white point fades away before the title");
                melt.Present(PlayerMeltDeath.Duration);
                Check(sprites.All(sprite => !sprite.enabled), "The completed melt clears every sprite before the title");
                Check(UnityEngine.Random.state.Equals(random) && art.Body.bounds == sourceBounds,
                    "Death presentation leaves gameplay randomness and live artwork untouched");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cue.gameObject);
                UnityEngine.Object.DestroyImmediate(cover.gameObject);
            }
            Debug.Log("Player melt checks passed: opposite pupil orbits during the rise, seamless handoff, continuous final half-turn, simultaneous X morph, no flattened-body line, one fluid smoosh, continuous stretch to both screen edges, white point, cleanup and frame-rate independence.");
        }

        public static void CapturePreview(bool animation = false)
        {
            Directory.CreateDirectory("TestResults/fatal-melt-frames");
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var art = CharacterVisuals.Ensure(player.gameObject);
            if (player.GetComponent<PlayerSlimeVisual>() == null)
                player.gameObject.AddComponent<PlayerSlimeVisual>().Configure(art);
            var cue = PresentationCue.Spawn(null, PresentationCue.Kind.PlayerMelt, art.Body, EnemyColor.Blue, 1);
            cue.enabled = false;
            var melt = cue.GetComponentInChildren<PlayerMeltDeath>();
            var camera = Camera.main;
            var cover = GameOverBlackout.Create(null, cue);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            cue.transform.position -= art.Body.bounds.center;
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 480), new Vector2Int(540, 960), new Vector2Int(960, 540), new Vector2Int(1179, 2556) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var ages = new[] { .4f, 1.55f, 1.9f, PlayerMeltDeath.LinePeakSeconds, 2.4f, PlayerMeltDeath.PointSeconds + .01f };
                    var strip = new Texture2D(size.x * ages.Length, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target;
                        camera.GetComponent<GameplayFraming>().Refresh();
                        foreach (int frame in Enumerable.Range(0, ages.Length))
                        {
                            melt.Present(ages[frame]);
                            cover.Present(1);
                            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                            camera.Render();
                            RenderTexture.active = target;
                            pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                            pixels.Apply();
                            var data = pixels.GetPixels32();
                            var lit = Enumerable.Range(0, data.Length).Where(i => data[i].r + data[i].g + data[i].b > 40).ToArray();
                            Check(lit.Length > (ages[frame] >= PlayerMeltDeath.PointSeconds ? 0 : 20),
                                "Every phase, including the final point, stays visible at " + size + " and " + ages[frame]);
                            if (ages[frame] >= PlayerMeltDeath.WhiteSeconds)
                                Check(lit.All(i => Math.Abs(data[i].r - data[i].g) <= 1 && Math.Abs(data[i].g - data[i].b) <= 1),
                                    "The television line and point render pure white at " + size);
                            if (Mathf.Abs(ages[frame] - PlayerMeltDeath.LinePeakSeconds) < .001f)
                            {
                                int left = lit.Min(i => i % size.x), right = lit.Max(i => i % size.x);
                                int bottom = lit.Min(i => i / size.x), top = lit.Max(i => i / size.x);
                                Check(right - left > (top - bottom + 1) * 20 && left == 0 && right == size.x - 1,
                                    "Opaque white reaches both physical screen edges in portrait and landscape");
                            }
                            strip.SetPixels32(frame * size.x, 0, size.x, size.y, data);
                        }
                        strip.Apply();
                        File.WriteAllBytes("TestResults/fatal-melt-sequence-" + size.x + "x" + size.y + ".png", strip.EncodeToPNG());
                        if (animation && size.x == 540)
                            for (int frame = 0; frame < 85; frame++)
                            {
                                melt.Present(frame / 30f);
                                cover.Present(1);
                                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                                camera.Render();
                                RenderTexture.active = target;
                                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                                pixels.Apply();
                                File.WriteAllBytes("TestResults/fatal-melt-frames/" + frame.ToString("D3") + ".png", pixels.EncodeToPNG());
                            }
                        melt.Present(PlayerMeltDeath.Duration);
                        UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                        camera.Render();
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                        pixels.Apply();
                        Check(pixels.GetPixels32().All(pixel => pixel.r + pixel.g + pixel.b < 5),
                            "The melt clears to black before the title at " + size);
                    }
                    finally
                    {
                        camera.targetTexture = previousTarget;
                        RenderTexture.active = previousActive;
                        target.Release();
                        UnityEngine.Object.DestroyImmediate(target);
                        UnityEngine.Object.DestroyImmediate(pixels);
                        UnityEngine.Object.DestroyImmediate(strip);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cue.gameObject);
                UnityEngine.Object.DestroyImmediate(cover.gameObject);
                camera.GetComponent<GameplayFraming>().Refresh();
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Player melt check failed: " + message); }
    }
}
