using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class CombatChecks
    {
        public static void Run()
        {
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            foreach (bool special in new[] { false, true })
            {
                var first = CombatBalance.CooldownRange(color, 1, special);
                var second = CombatBalance.CooldownRange(color, 2, special);
                var final = CombatBalance.CooldownRange(color, 10000, special);
                // Basic and upgraded enemies have independently tuned starting ranges.
                Vector2[] basic = { new(2.25f, 7.5f), new(2.25f, 7.5f), new(6, 9), new(6, 9), new(1.5f, 9), new(.75f, 3.75f) };
                Vector2[] upgraded = { new(2.25f, 13.5f), new(.75f, 3.75f), new(1.5f, 12), new(6, 18), new(1.5f, 7.5f), new(.75f, 3.75f) };
                Check(first == (special ? upgraded : basic)[(int)color], "Cooldowns preserve the current independent starting ranges: " + color);
                Check(first.x > 0 && first.y >= first.x + 3, "Cooldowns stay positive with room for variance");
                Check(second.x == first.x && second.y <= first.y && Mathf.Abs(second.y - Mathf.Max(first.x + 3, first.y - .25f)) < .001f,
                    "Only cooldown maximum decreases gently, stopping at the variance floor");
                Check(final.x == first.x && final.y == first.x + 3, "Cooldown floor preserves variance and minimum recovery");
            }
            Check(CombatBalance.FleetSpeedMultiplier(1) == 1 && Mathf.Abs(CombatBalance.FleetSpeedMultiplier(6) - 1.15f) < .001f &&
                CombatBalance.FleetSpeedMultiplier(1000) == 2, "Gentle capped fleet acceleration");

            var image = new Texture2D(2, 2);
            image.LoadImage(File.ReadAllBytes("Assets/Art/ShieldArc.png"));
            Check(image.GetPixel(64, 2).a > .5f && image.GetPixel(64, 126).a > .5f &&
                image.GetPixel(2, 64).a > .5f && image.GetPixel(126, 64).a > .5f &&
                image.GetPixel(64, 64).a == 0,
                "Shield art is a hollow ring fully encircling the enemy");
            int left = image.width, right = -1, bottom = image.height, top = -1;
            for (int y = 0; y < image.height; y++)
            for (int x = 0; x < image.width; x++)
            {
                if (image.GetPixel(x, y).a <= .01f) continue;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
                float radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(64, 64));
                Check(radius > 35 && radius < 64, "Shield thickens inward without extending its outer radius");
            }
            Check(image.GetPixel(64, 4).a > .99f && image.GetPixel(64, 14).a > .5f &&
                image.GetPixel(64, 24).a > 0 && image.GetPixel(64, 24).a < .15f && image.GetPixel(64, 30).a == 0 &&
                image.GetPixel(64, 124).a > .99f && image.GetPixel(14, 64).a > .5f &&
                image.GetPixel(24, 64).a > 0 && image.GetPixel(24, 64).a < .15f && image.GetPixel(30, 64).a == 0,
                "Solid outer rim fades through the enemy silhouette to a fully transparent inner edge");
            Check(left <= 2 && right >= 125 && bottom <= 2 && top >= 125,
                "Shield ring spans every cardinal side of the sprite frame");
            UnityEngine.Object.DestroyImmediate(image);
            var shield = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png");
            Check(shield.rect.size == new Vector2(128, 128) && shield.pivot == new Vector2(64, 64),
                "Shield import retains the full circular frame and centered pivot");
            foreach (float step in new[] { .016f, .1f, 2f }) Magic(step);
            Debug.Log("Combat checks passed: shield ring, independent basic/special cooldown ranges and scaling, level speed, magic awards, piercing, shields, charge cap and resets.");
        }

        private static void Magic(float step)
        {
            var root = new GameObject("Magic fixture", typeof(EnemyGrid));
            var playerObject = new GameObject("Magic player", typeof(PlayerMovement), typeof(SpriteRenderer));
            var mouth = new GameObject("Magic tongue", typeof(LineRenderer), typeof(TongueShot));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                var player = playerObject.GetComponent<PlayerMovement>();
                var tongue = mouth.GetComponent<TongueShot>();
                mouth.transform.SetParent(playerObject.transform, false);
                playerObject.GetComponentInChildren<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PlayerPlaceholder.png");
                player.Configure(grid, tongue, playerObject.GetComponentInChildren<SpriteRenderer>());
                var redA = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var redB = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, GridModel.Columns - 1, 0);
                grid.ClearMatchingChain(redA.Id, EnemyColor.Red);
                Check(player.MagicCharges == 0, "Partial color clear awards no magic");
                grid.ClearMatchingChain(redB.Id, EnemyColor.Blue);
                Check(player.MagicCharges == 0, "Mismatch awards no magic");
                int redId = redB.Id;
                grid.ClearMatchingChain(redId, EnemyColor.Red);
                Check(player.MagicCharges == 1, "Last enemy of a color grants magic");
                for (int flash = 0; flash < 5; flash++)
                {
                    player.RefreshPresentation(flash * .1f + .01f);
                    Check(player.DisplayColor == EnemyPalette.Get((EnemyColor)flash), "Magic player flashes through all five colors");
                }
                grid.ClearMatchingChain(redId, EnemyColor.Red);
                Check(player.MagicCharges == 1, "Duplicate hit cannot award twice");
                ProgressionChecks.Add(grid, EnemyColor.Green, GridModel.Columns - 1, 2);
                ProgressionChecks.Add(grid, EnemyColor.Green, GridModel.Columns - 2, 2);
                ProgressionChecks.Add(grid, EnemyColor.Purple, GridModel.Columns - 1, 4);
                var survivor = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                playerObject.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                Check(player.Fire() && tongue.IsMagic && player.MagicCharges == 0, "Next shot consumes one magic charge");
                Check(!player.Fire() && player.MagicCharges == 0, "Rejected fire cannot consume another charge");
                for (int i = 0; i < 200 && tongue.Active; i++) tongue.Tick(step, grid);
                Check(!tongue.Active && grid.Model.Count == 1 && grid.View(survivor.Id) == survivor,
                    "Magic pierces different colors and active shields, including same-color neighbors");
                Check(player.MagicCharges == 2, "Magic earned during piercing is capped at two");
                for (int column = GridModel.Columns - 3; column < GridModel.Columns; column++)
                    ProgressionChecks.Add(grid, EnemyColor.Red, column, 2);
                Check(player.Fire() && tongue.IsMagic && player.MagicCharges == 0, "Firing consumes all stored magic");
                for (int i = 0; i < 200 && tongue.Active; i++) tongue.Tick(step, grid);
                Check(!tongue.Active && grid.Model.Count == 1 && grid.Model.ColorCount(EnemyColor.Red) == 1 &&
                    player.MagicCharges == 0, "Destroying a chain cannot retain magic while its color survives elsewhere");
                var reward = ProgressionChecks.Add(grid, EnemyColor.Yellow, 2, 0);
                grid.ClearMatchingChain(reward.Id, EnemyColor.Yellow);
                Check(player.Fire() && tongue.IsMagic && player.MagicCharges == 0, "A new complete color clear earns magic again");
                tongue.Tick(10, grid);
                Check(player.MagicCharges == 0, "A miss cannot retain magic");
                reward = ProgressionChecks.Add(grid, EnemyColor.Green, 2, 0);
                grid.ClearMatchingChain(reward.Id, EnemyColor.Green);
                Check(player.MagicCharges == 1, "Prepare stored magic before testing player damage");
                Check(player.Hit() && player.MagicCharges == 0, "Player hit resets stored magic");
                player.TickSurvival(4);
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 2, 0);
                grid.SetColor(yellow.Id, EnemyColor.Blue);
                Check(player.MagicCharges == 0, "Color conversion alone does not earn magic");
                grid.ClearMatchingChain(yellow.Id, EnemyColor.Blue);
                Check(player.MagicCharges == 1, "Subsequent genuine color clear earns magic");
                grid.ClearMatchingChain(survivor.Id, EnemyColor.Red);
                Check(player.MagicCharges == 0 && !player.Fire(), "Fleet clear resets magic and empty field cannot fire");
                ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 0);
                Check(player.Fire() && !tongue.IsMagic, "Normal shot restored after reset");
                var keys = mouth.GetComponent<LineRenderer>().colorGradient.colorKeys;
                Check(keys.Length == 2 && keys[0].color == keys[1].color, "Normal shot does not retain rainbow gradient");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Combat check failed: " + message); }
    }
}
