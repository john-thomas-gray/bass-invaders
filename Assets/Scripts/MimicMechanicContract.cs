using System;
using UnityEngine;

namespace CandyCruisers
{
    // Executed in the Editor and in isolated desktop/phone players using the same public combat paths.
    public static class MimicMechanicContract
    {
        private sealed class Board : IDisposable
        {
            public readonly EnemyGrid Grid;
            private readonly GameObject[] prefabs;
            private GameSession session;
            public Board(GameObject[] prefabs, GameObject missile, Sprite shield)
            {
                this.prefabs = prefabs;
                Grid = new GameObject("Mimic contract", typeof(EnemyGrid), typeof(EnemyRowSpawner)).GetComponent<EnemyGrid>();
                Grid.enabled = false;
                Grid.GetComponent<EnemyGridMovement>().enabled = false;
                Grid.ConfigureAbilities(missile, shield);
                var spawner = Grid.GetComponent<EnemyRowSpawner>();
                spawner.Configure(prefabs[1], prefabs[0]);
                spawner.ConfigureNewTypes(prefabs[2], prefabs[3], prefabs[4], prefabs[5]);
                spawner.ConfigureSpecialSprite(prefabs[5].GetComponentInChildren<SpriteRenderer>().sprite);
            }
            public GridEnemy Add(EnemyColor color, int x, int y, bool special = false)
            {
                var instance = UnityEngine.Object.Instantiate(prefabs[(int)color]);
                var enemy = instance.GetComponent<GridEnemy>() ?? instance.AddComponent<GridEnemy>();
                enemy.Configure(color, x, y); instance.transform.SetParent(Grid.transform, false); Grid.Register(enemy);
                enemy.GetComponent<EnemyAbilities>().Suspended = true;
                if (special) { enemy.Promote(); enemy.GetComponent<EnemyAbilities>().OnPromoted(); }
                return enemy;
            }
            public void Copy(GridEnemy yellow, GridEnemy target, int rate)
            {
                var ability = yellow.GetComponent<EnemyAbilities>();
                ability.Suspended = false;
                Require(ability.BeginImitation(target), "An eligible adjacent target starts copying");
                if (yellow.IsSpecial) Require(!ability.IsTransforming && Grid.Model.IsAttached(yellow.Id), "Special copying is immediate");
                else
                {
                    Require(ability.IsTransforming, "Regular copying retains the two-beat windup");
                    for (int i = 0; i < rate * 3 && ability.IsTransforming; i++) ability.Tick(1f / rate);
                    Require(!ability.IsTransforming, "Regular copying completes using frame updates");
                }
                ability.Suspended = true;
            }
            public PlayerMovement Player(float x)
            {
                var root = new GameObject("Contract player", typeof(PlayerMovement), typeof(SpriteRenderer));
                root.transform.SetParent(Grid.transform, false); root.transform.position = new Vector3(x, -4.6f, 0);
                var body = root.GetComponent<SpriteRenderer>(); body.sprite = EnemyPlaceholderArt.Triangle;
                var mouth = new GameObject("Contract tongue", typeof(LineRenderer), typeof(TongueShot));
                mouth.transform.SetParent(root.transform, false);
                var player = root.GetComponent<PlayerMovement>();
                player.Configure(Grid, mouth.GetComponent<TongueShot>(), body);
                return player;
            }
            public GameSession Session(PlayerMovement player)
            {
                session = Grid.gameObject.AddComponent<GameSession>();
                session.LevelCatalog = null;
                session.Configure(player);
                if (!Application.isPlaying) SessionLifecycle("OnEnable");
                if (session.OpeningWarningActive) session.HandleMenuKey(KeyCode.Space);
                return session;
            }
            private void SessionLifecycle(string method) => typeof(GameSession).GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
            public void Dispose()
            {
                if (session != null && !Application.isPlaying) SessionLifecycle("OnDisable");
                UnityEngine.Object.DestroyImmediate(Grid.gameObject);
            }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new Exception("Mimic contract: " + message); }

        public static void PopulateGallery(EnemyGrid grid, Func<EnemyColor, int, int, GridEnemy> add)
        {
            void Copy(GridEnemy yellow, GridEnemy host, bool special = false)
            {
                if (special) { yellow.Promote(); yellow.GetComponent<EnemyAbilities>().OnPromoted(); }
                var ability = yellow.GetComponent<EnemyAbilities>();
                ability.Suspended = false; ability.BeginImitation(host);
                if (!special) ability.Tick(EnemyAbilities.DefaultImitationSeconds);
                ability.Suspended = true;
            }
            add(EnemyColor.Green, 0, 1);
            var threeQuarters = add(EnemyColor.Yellow, 1, 2);
            add(EnemyColor.Blue, 1, 1);
            Copy(threeQuarters, add(EnemyColor.Blue, 0, 2));
            add(EnemyColor.Green, 3, 1);
            add(EnemyColor.Red, 3, 2);
            add(EnemyColor.Green, 3, 3);
            var vertical = add(EnemyColor.Yellow, 4, 2);
            add(EnemyColor.Blue, 4, 3);
            Copy(vertical, add(EnemyColor.Blue, 4, 1));
            var permanent = add(EnemyColor.Yellow, 1, 6);
            add(EnemyColor.Green, 2, 6);
            Copy(permanent, add(EnemyColor.Green, 0, 6));
            Copy(add(EnemyColor.Yellow, 3, 6), add(EnemyColor.Purple, 4, 6), true);
            var shielded = add(EnemyColor.Yellow, 1, 9);
            add(EnemyColor.Blue, 3, 9);
            Copy(shielded, add(EnemyColor.Blue, 2, 9), true);
            Copy(add(EnemyColor.Yellow, 1, 8), shielded, true);
            grid.enabled = false;
            grid.GetComponent<EnemyGridMovement>().enabled = false;
        }

        public static int Run(GameObject[] prefabs, GameObject missile, Sprite shield)
        {
            int cases = RunHostJunctions(prefabs, missile, shield);
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (EnemyColor color in new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Orange })
            foreach (bool special in new[] { false, true })
            foreach (bool yellowHit in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var grid = board.Grid;
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, special);
                var sibling = board.Add(EnemyColor.Yellow, 1, 3);
                var host = board.Add(color, 3, 3);
                var second = color == EnemyColor.Orange ? null : board.Add(color, 4, 3);
                board.Copy(mimic, host, rate);
                var ability = mimic.GetComponent<EnemyAbilities>();
                Require(mimic.Color == EnemyColor.Yellow && ability.AbilityColor == color && mimic.IsSpecial == special,
                    "Yellow identity and ability tier are independent of displayed color and host tier");
                if (second != null) Require(host.IsSpecial && second.IsSpecial, "Mimics promote two real hosts");
                int mimicId = mimic.Id, siblingId = sibling.Id, hostId = host.Id;
                var shot = yellowHit ? EnemyColor.Yellow : color;
                Require(grid.FindMatchingHit(new Vector3(mimic.transform.position.x, -4.6f, 0), 0, 10, shot,
                    TongueShot.NormalHitRadius, out int hit, out _) && hit == mimic.Id, "Shot sweep targets the correct mimic");
                var result = grid.ResolveTongueHit(hit, shot);
                if (yellowHit)
                {
                    Require(grid.View(mimicId) == null && grid.View(siblingId) == null && grid.View(hostId) == host,
                        "Yellow destroys the mimic group and preserves the host group");
                    Require(!host.IsSpecial && (second == null || !second.IsSpecial), "Hosts below three demote on mimic loss");
                }
                else if (!special)
                {
                    Require(grid.View(mimicId) == null && grid.View(hostId) == null && (second == null || grid.View(second.Id) == null) &&
                        grid.View(siblingId) == sibling, "Host color kills regular mimic and hosts, never neighboring yellows");
                }
                else if (color == EnemyColor.Blue)
                {
                    Require(result == EnemyGrid.TongueHitResult.Deflected && grid.Model.IsAttached(mimicId) && ability.ShieldActive &&
                        ability.ShieldTint == EnemyPalette.Get(EnemyColor.Yellow), "Blue hits a special mimic's yellow shield with the usual penalty");
                    grid.ResolveTongueHit(mimicId, EnemyColor.Yellow);
                    Require(grid.View(mimicId) == null && grid.View(siblingId) == null && grid.View(hostId) == host,
                        "Yellow bypasses the special Blue mimic's shield");
                }
                else
                {
                    Require(result == EnemyGrid.TongueHitResult.Revealed && grid.View(mimicId) == mimic && !grid.Model.IsAttached(mimicId) &&
                        mimic.Color == EnemyColor.Yellow && mimic.IsSpecial && mimic.Visuals.Body.color == Color.white,
                        "A special reveals, flashes and detaches on a host-color hit");
                    Require(grid.View(siblingId) == sibling && grid.View(hostId) == host && !host.IsSpecial && (second == null || !second.IsSpecial),
                        "Special reveal preserves both groups and demotes unsupported hosts");
                }
                cases++;
            }
            foreach (int mask in new[] { 1, 2, 4, 8, 5, 6, 9, 10, 3, 12, 7, 15 })
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 4);
                GridEnemy target = null;
                foreach (var entry in new[] { (1,-1,0), (2,1,0), (4,0,-1), (8,0,1) })
                    if ((mask & entry.Item1) != 0) target = board.Add(EnemyColor.Red, 2 + entry.Item2, 4 + entry.Item3);
                board.Copy(yellow, target, 60);
                bool full = (mask & 3) == 3 || (mask & 12) == 12;
                Require((yellow.Color == EnemyColor.Red) == full && board.Grid.Model.IsAttached(yellow.Id) != full,
                    "Only opposite halves permanently convert a regular mimic");
                if (!full) Require(yellow.GetComponent<EnemyPresentation>().HostMask == mask, "Renderer preserves every half in half/three-quarter coverage");
                cases++;
            }
            foreach (bool specialTarget in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var host = board.Add(EnemyColor.Red, 3, 3);
                var first = board.Add(EnemyColor.Yellow, 2, 3, specialTarget);
                board.Copy(first, host, 60);
                var next = board.Add(EnemyColor.Yellow, 1, 3);
                var plain = board.Add(EnemyColor.Yellow, 1, 4);
                plain.Promote();
                var pool = board.Grid.Model.ImitationTargets(next.Id);
                Require(pool.Exists(item => item.Id == first.Id) && !pool.Exists(item => item.Id == plain.Id),
                    "Target pool includes copying yellows and excludes plain yellows");
                board.Copy(next, first, 60);
                Require(next.GetComponent<EnemyAbilities>().AbilityColor == EnemyColor.Red, "Copying another mimic copies its color, not yellow");
                board.Grid.RefreshSpecials();
                Require(next.IsSpecial && first.IsSpecial && next.GetComponent<EnemyAbilities>().IsDisguised,
                    "Three connected true yellows promote even when their appearances differ");
                cases++;
            }
            foreach (int hostCount in new[] { 2, 3 })
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 3);
                var hosts = new GridEnemy[hostCount];
                for (int i = 0; i < hostCount; i++) hosts[i] = board.Add(EnemyColor.Blue, 3 + i, 3);
                board.Copy(yellow, hosts[0], 60);
                board.Grid.ResolveTongueHit(yellow.Id, EnemyColor.Yellow);
                foreach (var host in hosts)
                    Require(host.IsSpecial == (hostCount >= 3), "Remaining host count determines regular/special tier");
                cases++;
            }
            foreach (bool hostSpecial in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 3);
                var host = board.Add(EnemyColor.Blue, 3, 3, hostSpecial);
                board.Copy(yellow, host, 60);
                Require(!yellow.IsSpecial && !yellow.GetComponent<EnemyAbilities>().ShieldActive, "Regular mimic always starts with the regular Blue ability");
                cases++;
            }
            foreach (bool special in new[] { false, true })
            foreach (EnemyColor color in new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Orange })
            foreach (EnemyColor selected in EnemyPalette.Colors)
            using (var board = new Board(prefabs, missile, shield))
            {
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, special);
                var sibling = board.Add(EnemyColor.Yellow, 1, 3);
                var host = board.Add(color, 3, 3);
                board.Copy(mimic, host, 60);
                int mimicId = mimic.Id, siblingId = sibling.Id, hostId = host.Id;
                var tongue = new GameObject("Magic contract tongue", typeof(LineRenderer), typeof(TongueShot)).GetComponent<TongueShot>();
                tongue.transform.SetParent(board.Grid.transform, false);
                tongue.transform.position = new Vector3(mimic.transform.position.x, -4.6f, 0);
                Require(tongue.TryFire(selected, 10, true), "Rainbow shot fires");
                tongue.Tick(2, board.Grid);
                Require(board.Grid.View(mimicId) == null && board.Grid.View(siblingId) == null,
                    "Every rainbow shot destroys the complete yellow group, including special Blue mimics");
                Require((board.Grid.View(hostId) != null) == special,
                    "Rainbow destroys a regular mimic's hosts but preserves a special mimic's hosts");
                cases++;
            }
            foreach (bool yellowShot in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var redHost = board.Add(EnemyColor.Red, 3, 3);
                var blueHost = board.Add(EnemyColor.Blue, 1, 2);
                var redMimic = board.Add(EnemyColor.Yellow, 2, 3);
                var blueMimic = board.Add(EnemyColor.Yellow, 1, 3);
                board.Copy(redMimic, redHost, 60); board.Copy(blueMimic, blueHost, 60);
                int redId = redMimic.Id, blueId = blueMimic.Id, hostId = redHost.Id;
                board.Grid.ResolveTongueHit(redId, yellowShot ? EnemyColor.Yellow : EnemyColor.Red);
                Require(board.Grid.View(redId) == null && (board.Grid.View(blueId) == null) == yellowShot,
                    "Yellow grouping follows original identity even when adjacent mimics copy different colors");
                Require((board.Grid.View(hostId) != null) == yellowShot && board.Grid.View(blueHost.Id) == blueHost,
                    "Host hits cannot propagate through a differently colored yellow mimic");
                cases++;
            }
            using (var board = new Board(prefabs, missile, shield))
            {
                var host = board.Add(EnemyColor.Blue, 3, 3);
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, true);
                board.Copy(mimic, host, 60);
                var player = board.Player(mimic.transform.position.x);
                var tongue = player.GetComponentInChildren<TongueShot>();
                Require(tongue.TryFire(EnemyColor.Blue, 10), "Blue penalty fixture fires");
                for (int i = 0; i < 100 && !tongue.Retracting; i++) tongue.Tick(.01f, board.Grid);
                Require(player.Stunned && tongue.ShockActive && board.Grid.Model.IsAttached(mimic.Id),
                    "Blue shield collision actually shocks/stuns the player, not just returning an enum");
                tongue.Tick(4, board.Grid);
                Require(!player.Stunned, "Shield penalty ends with the returning tongue");
                cases++;
            }
            using (var board = new Board(prefabs, missile, shield))
            {
                var mimic = board.Add(EnemyColor.Yellow, 2, 4);
                var host = board.Add(EnemyColor.Red, 3, 4);
                board.Copy(mimic, host, 60);
                board.Add(EnemyColor.Red, 2, 3);
                var ability = mimic.GetComponent<EnemyAbilities>();
                ability.Suspended = false; ability.Tick(0); ability.Suspended = true;
                Require(mimic.GetComponent<EnemyPresentation>().HostMask == 6 && mimic.Color == EnemyColor.Yellow,
                    "Adding a perpendicular host changes a live mimic to three-quarter coverage");
                board.Add(EnemyColor.Red, 1, 4); board.Grid.RefreshMimicCoverage();
                Require(mimic.Color == EnemyColor.Red && !board.Grid.Model.IsAttached(mimic.Id), "Live opposite-side coverage permanently converts");
                Require(!board.Grid.Model.MatchesShot(mimic.Id, EnemyColor.Yellow), "Permanent conversion removes Yellow vulnerability");
                cases++;
            }
            foreach (int hostCount in new[] { 1, 2, 3 })
            using (var board = new Board(prefabs, missile, shield))
            {
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, true);
                var hosts = new GridEnemy[hostCount];
                for (int i = 0; i < hostCount; i++) hosts[i] = board.Add(EnemyColor.Blue, 3 + i, 3);
                board.Copy(mimic, hosts[0], 60);
                var perimeter = board.Grid.GroupShield;
                int expectedEdges = hostCount == 1 ? 4 : 2 * (hostCount + 1) + 2;
                Require(perimeter.Protects(mimic.Id) && perimeter.EdgeCount == expectedEdges && perimeter.ContourCount == 1,
                    "Yellow special shield is square and merges with adjacent Blue perimeter cells");
                Require(!mimic.Visuals.Root.Find("Shield").GetComponent<SpriteRenderer>().enabled,
                    "Square perimeter replaces the mimic's circular shield renderer");
                var mesh = perimeter.GetComponentInChildren<MeshFilter>().sharedMesh;
                Require(mesh.vertexCount == expectedEdges * 9, "Mixed-color boundaries share the same corner vertices without seams");
                bool yellow = false, blue = false;
                foreach (var tint in mesh.colors)
                {
                    yellow |= tint.a > .9f && tint.b < .5f;
                    blue |= tint.a > .9f && tint.b > .9f;
                }
                Require(yellow && (hostCount == 1 || blue), "Perimeter retains yellow and blue segments in a connected contour");
                var origin = mimic.transform.position + Vector3.down * 2;
                Require(perimeter.FindHit(origin, 0, 4, .03f, out int yellowOwner, out _, EnemyColor.Blue) && yellowOwner == mimic.Id,
                    "Blue collides with the Yellow portion of the shared shield");
                Require(!perimeter.FindHit(origin, 0, 4, .03f, out _, out _, EnemyColor.Yellow),
                    "Yellow passes the Yellow portion without hitting removed internal seams");
                if (hostCount > 1)
                {
                    var hostOrigin = hosts[0].transform.position + Vector3.down * 2;
                    Require(!perimeter.FindHit(hostOrigin, 0, 4, .03f, out _, out _, EnemyColor.Blue) &&
                        perimeter.FindHit(hostOrigin, 0, 4, .03f, out _, out _, EnemyColor.Yellow),
                        "The connected Blue portion retains Blue immunity and Yellow deflection");
                }
                board.Grid.ResolveTongueHit(mimic.Id, EnemyColor.Yellow);
                Require(perimeter.EdgeCount == (hostCount == 3 ? 8 : 0), "Removal closes the remaining shield or removes unsupported shielding");
                cases++;
            }
            using (var board = new Board(prefabs, missile, shield))
            {
                var host = board.Add(EnemyColor.Blue, 3, 3);
                var right = board.Add(EnemyColor.Yellow, 2, 3, true);
                var left = board.Add(EnemyColor.Yellow, 1, 3, true);
                board.Copy(right, host, 60); board.Copy(left, right, 60);
                Require(board.Grid.GroupShield.ContourCount == 1 && board.Grid.GroupShield.EdgeCount == 6 &&
                    board.Grid.GroupShield.Protects(left.Id) && board.Grid.GroupShield.Protects(right.Id),
                    "Adjacent Yellow special shields form one rectangle with no shared dividing edge");
                cases++;
            }
            using (var board = new Board(prefabs, missile, shield))
            {
                var host = board.Add(EnemyColor.Blue, 2, 2);
                board.Add(EnemyColor.Blue, 3, 2);
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, true);
                board.Copy(mimic, host, 60);
                var perimeter = board.Grid.GroupShield;
                Require(perimeter.EdgeCount == 8 && perimeter.ContourCount == 1, "Mixed L-shaped shields share a concave contour");
                var origin = board.Grid.transform.TransformPoint(board.Grid.CellPosition(2, 4));
                Require(perimeter.FindHit(origin, 0, 4, .03f, out _, out float distance, EnemyColor.Yellow) && distance > 1.8f,
                    "No internal horizontal seam catches a Yellow shot after its friendly Yellow boundary");
                foreach (var vertex in perimeter.GetComponentInChildren<MeshFilter>().sharedMesh.vertices)
                    Require(!float.IsNaN(vertex.x) && !float.IsNaN(vertex.y), "Concave mixed corners remain finite");
                cases++;
            }
            foreach (EnemyColor color in new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Orange })
            foreach (bool special in new[] { false, true })
            foreach (bool extraYellow in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, special);
                if (extraYellow) board.Add(EnemyColor.Yellow, 0, 0);
                var player = board.Player(mimic.transform.position.x);
                player.RefreshColor();
                Require(player.ReadyColor == EnemyColor.Yellow, "Player begins Yellow before hosts arrive");
                var host = board.Add(color, 3, 3);
                if (color != EnemyColor.Orange) board.Add(color, 4, 3);
                var ability = mimic.GetComponent<EnemyAbilities>();
                ability.Suspended = false; Require(ability.BeginImitation(host), "Color-hold fixture starts copying");
                player.RefreshColor();
                Require(player.ReadyColor == EnemyColor.Yellow && player.DisplayColor == EnemyPalette.Get(EnemyColor.Yellow),
                    "Beginning a copy never replaces a held Yellow, including the last Yellow");
                if (!special)
                {
                    ability.Tick(EnemyAbilities.DefaultImitationSeconds * .5f);
                    player.RefreshColor();
                    Require(player.ReadyColor == EnemyColor.Yellow && board.Grid.Model.SelectablePlayerColors().Contains(EnemyColor.Yellow) &&
                        board.Grid.PlayerColorWeights().ContainsKey(EnemyColor.Yellow), "Yellow remains selected and selectable during windup");
                    ability.Tick(EnemyAbilities.DefaultImitationSeconds * .5f + .001f);
                }
                ability.Suspended = true; player.RefreshColor();
                Require(player.ReadyColor == EnemyColor.Yellow && player.DisplayColor == EnemyPalette.Get(EnemyColor.Yellow),
                    "Completed copying and new host shields preserve the held Yellow");
                board.Grid.ResolveTongueHit(mimic.Id, EnemyColor.Yellow);
                player.RefreshColor();
                Require((player.ReadyColor == EnemyColor.Yellow) == extraYellow,
                    "Automatic selection changes only after the final actual Yellow is gone");
                cases++;
            }
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 3);
                var player = board.Player(yellow.transform.position.x); player.RefreshColor();
                Require(player.ReadyColor == EnemyColor.Yellow, "Conversion control begins Yellow");
                var host = board.Add(EnemyColor.Red, 1, 3); board.Add(EnemyColor.Red, 3, 3);
                board.Copy(yellow, host, 60); player.RefreshColor();
                Require(board.Grid.Model.ColorCount(EnemyColor.Yellow) == 0 && player.ReadyColor == EnemyColor.Red,
                    "Permanent conversion may change selection only because the last actual Yellow no longer exists");
                cases++;
            }
            foreach (bool chained in new[] { false, true })
            foreach (int hostCount in new[] { 2, 3 })
            foreach (int rate in new[] { 30, 60, 120 })
            using (var board = new Board(prefabs, missile, shield))
            {
                var first = board.Add(EnemyColor.Yellow, 0, 3);
                var second = board.Add(EnemyColor.Yellow, 1, 3);
                var third = board.Add(EnemyColor.Yellow, 2, 3);
                var reds = new GridEnemy[hostCount];
                for (int i = 0; i < hostCount; i++) reds[i] = board.Add(EnemyColor.Red, 3 + i, 3);
                board.Grid.RefreshSpecials();
                board.Copy(third, reds[0], rate);
                if (chained) { board.Copy(second, third, rate); board.Copy(first, second, rate); }
                foreach (var red in reds) Require(red.IsSpecial, "yyyrr promotes its two reds while the third Yellow copies Red");
                var redShot = new GameObject("yyyrr red shot", typeof(LineRenderer), typeof(TongueShot)).GetComponent<TongueShot>();
                redShot.transform.SetParent(board.Grid.transform, false);
                redShot.transform.position = new Vector3(third.transform.position.x, -4.6f, 0);
                Require(redShot.TryFire(EnemyColor.Red, 10), "yyyrr fires an actual Red tongue");
                for (int frame = 0; frame < rate && board.Grid.Model.IsAttached(third.Id); frame++) redShot.Tick(1f / rate, board.Grid);
                Require(!board.Grid.Model.IsAttached(third.Id) && redShot.Active && !redShot.Retracting,
                    "An actual Red tongue reveals the third Yellow and keeps extending");
                foreach (var red in reds)
                {
                    Require(red.IsSpecial == (hostCount >= 3), "Revealing yyyrr immediately demotes the real red pair, including chained copies");
                    Require(red.Visuals.Body.sprite == board.Grid.GetComponent<EnemyRowSpawner>().AppearanceSprite(EnemyColor.Red, hostCount >= 3),
                        "The red pair visibly returns to regular ship artwork on the reveal frame");
                }
                foreach (var yellow in new[] { first, second, third })
                    Require(board.Grid.View(yellow.Id) == yellow && yellow.Color == EnemyColor.Yellow && !board.Grid.Model.IsAttached(yellow.Id),
                        "Revealing the bridge Yellow preserves all Yellows and detaches copies with no path to a real host");
                for (int frame = 0; frame < rate / 6; frame++)
                {
                    board.Grid.RefreshSpecials(); board.Grid.RefreshMimicCoverage();
                    foreach (var red in reds) Require(red.IsSpecial == (hostCount >= 3),
                        "yyyrr reveals back to two regular reds; yyyrrr keeps three special reds without phantom support");
                }
                cases++;
            }
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (int finish in new[] { 0, 1, 2 })
            foreach (bool multiple in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var host = board.Add(EnemyColor.Red, 3, 3);
                var player = board.Player(board.Grid.CellPosition(2, 3).x); player.RefreshColor();
                var mimic = board.Add(EnemyColor.Yellow, 2, 3, true); board.Copy(mimic, host, rate);
                GridEnemy upper = null;
                if (multiple)
                {
                    upper = board.Add(EnemyColor.Yellow, 2, 2, true);
                    board.Copy(upper, board.Add(EnemyColor.Red, 3, 2), rate);
                }
                var session = board.Session(player);
                // This synchronous fixture advances abilities with elapsed seconds, independently of live music.
                var music = board.Grid.GetComponent<GameplayMusicPlayer>();
                music.Source.Stop(); music.Source.clip = null;
                bool hitBehind = finish == 1;
                var behind = finish == 0 ? null : board.Add(hitBehind ? EnemyColor.Red : EnemyColor.Blue, 2, 0);
                if (finish == 2)
                {
                    var blue = behind.GetComponent<EnemyAbilities>();
                    blue.Suspended = false; blue.Tick(blue.CooldownRemaining + .35f); blue.Suspended = true;
                    Require(blue.ShieldActive, "A regular Blue shield is active beyond the reveal");
                }
                for (int i = 0; i < 4; i++)
                { session.Progress.BeginShot(); session.Progress.RegisterShotColor(EnemyColor.Green); session.Progress.FinishShot(true); }
                session.Tick(0);
                var tongue = player.Tongue;
                int retractions = 0;
                Action<bool> retracting = hit =>
                {
                    retractions++;
                    Require(tongue.Length > mimic.HitBounds.max.y - tongue.transform.position.y && hit == hitBehind,
                        "The tongue passes entirely beyond the revealed Yellow before its one return");
                };
                player.ShotRetractionStarted += retracting;
                var observation = new PlaytestObservation(board.Grid, player);
                Require(observation.At(player.transform.position.x) == (hitBehind ? 1 : 0) && board.Grid.Model.IsAttached(mimic.Id),
                    "Shot estimates pass through disguised Yellows without revealing them");
                Require(player.Fire(), "Combo fixture fires a Red reveal shot");
                for (int frame = 0; tongue.Active && frame < rate * 2; frame++) tongue.Tick(1f / rate, board.Grid);
                player.ShotRetractionStarted -= retracting;
                Require(!tongue.Active && retractions == 1 && tongue.RevealedYellow && tongue.HitRegularShield == (finish == 2) &&
                    !board.Grid.Model.IsAttached(mimic.Id) && (upper == null || !board.Grid.Model.IsAttached(upper.Id)) &&
                    session.Progress.ComboStreak == (hitBehind ? 5 : 0) && session.Progress.Score == (hitBehind ? 500 : 0) &&
                    session.Progress.Defeated == (hitBehind ? 1 : 0) && session.ComboBreak.Active != hitBehind,
                    "One or more reveals pass through; only an actual matching hit preserves the combo" +
                    $" (rate={rate}, finish={finish}, multiple={multiple}, combo={session.Progress.ComboStreak}, score={session.Progress.Score}," +
                    $" kills={session.Progress.Defeated}, break={session.ComboBreak.Active}, revealed={tongue.RevealedYellow}, shield={tongue.HitRegularShield}, returns={retractions})");
                Require(board.Grid.View(mimic.Id) == mimic && board.Grid.View(host.Id) == host &&
                    (behind == null || (board.Grid.View(behind.Id) == null) == hitBehind), "Reveals preserve enemies; only the later matching target dies");
                player.transform.position = new Vector3(10, -4.6f, 0);
                Require(player.Fire() && !tongue.RevealedYellow && !tongue.HitRegularShield, "New shot resets reveal and shield flags");
                tongue.Tick(2, board.Grid);
                Require(session.Progress.ComboStreak == 0 && session.ComboBreak.Active, "The next genuine miss still breaks the combo");
                cases++;
            }
            Debug.Log("Mimic contract passed: " + cases + " cases for grouping, shots, tiers, target pool, coverage and permanent conversion.");
            return cases;
        }

        private static int RunHostJunctions(GameObject[] prefabs, GameObject missile, Sprite shield)
        {
            int cases = 0;
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (bool opposite in new[] { true, false })
            foreach (int rotation in new[] { 0, 1, 2, 3 })
            foreach (int targetIndex in new[] { 0, 1 })
            for (int shot = 0; shot < (opposite ? 1 : 2); shot++)
            using (var board = new Board(prefabs, missile, shield))
            {
                Vector2Int Rotate(int x, int y)
                {
                    var point = new Vector2Int(x, y);
                    for (int turn = 0; turn < rotation; turn++) point = new Vector2Int(-point.y, point.x);
                    return point;
                }
                GridEnemy Add(EnemyColor color, int x, int y)
                {
                    var point = Rotate(x, y);
                    return board.Add(color, 2 + point.x, 4 + point.y);
                }
                var yellow = Add(EnemyColor.Yellow, 0, 0);
                var hosts = new[] { Add(EnemyColor.Blue, 0, -1), Add(EnemyColor.Blue, opposite ? 0 : -1, opposite ? 1 : 0) };
                var green = Add(EnemyColor.Green, -1, -1);
                GridEnemy red = null, otherGreen = null;
                if (opposite)
                {
                    red = Add(EnemyColor.Red, -1, 0);
                    otherGreen = Add(EnemyColor.Green, -1, 1);
                }
                board.Copy(yellow, hosts[targetIndex], rate);
                if (opposite)
                {
                    Require(yellow.Color == EnemyColor.Blue && !board.Grid.Model.IsAttached(yellow.Id) &&
                        !board.Grid.Model.MatchesShot(yellow.Id, EnemyColor.Yellow),
                        "gb/ry/gb permanently converts to Blue from either vertical host, in every orientation");
                    Require(yellow.IsSpecial && yellow.GetComponent<EnemyAbilities>().AbilityColor == EnemyColor.Blue,
                        "The fully converted Blue joins and promotes the true three-Blue group");
                }
                else
                {
                    int expectedMask = 0;
                    foreach (var host in hosts)
                        expectedMask |= host.Column < yellow.Column ? 1 : host.Column > yellow.Column ? 2 : host.Row < yellow.Row ? 4 : 8;
                    Require(yellow.Color == EnemyColor.Yellow && !yellow.IsSpecial &&
                        yellow.GetComponent<EnemyPresentation>().HostMask == expectedMask &&
                        yellow.GetComponent<EnemyAbilities>().AbilityColor == EnemyColor.Blue,
                        "gb/by gives the regular Yellow three-quarter Blue coverage from either host");
                }
                foreach (var host in hosts)
                    Require(host.IsSpecial && host.Visuals.Body.sprite == board.Grid.GetComponent<EnemyRowSpawner>().AppearanceSprite(EnemyColor.Blue, true),
                        "The Yellow bridges two otherwise separate Blues and promotes both, with special artwork");
                int yellowId = yellow.Id;
                board.Grid.ResolveTongueHit(yellowId, shot == 1 ? EnemyColor.Yellow : EnemyColor.Blue);
                Require(board.Grid.View(yellowId) == null, "A matching shot destroys the junction enemy");
                foreach (var host in hosts)
                {
                    if (shot == 0) Require(board.Grid.View(host.Id) == null, "Host-color hit destroys both host branches through the regular mimic");
                    else Require(board.Grid.View(host.Id) == host && !host.IsSpecial &&
                        host.Visuals.Body.sprite == board.Grid.GetComponent<EnemyRowSpawner>().AppearanceSprite(EnemyColor.Blue, false),
                        "Yellow hit preserves both disconnected hosts and returns each to regular artwork");
                }
                Require(board.Grid.View(green.Id) == green && !green.IsSpecial &&
                    (red == null || board.Grid.View(red.Id) == red) && (otherGreen == null || board.Grid.View(otherGreen.Id) == otherGreen),
                    "Unrelated neighboring colors never join the copied host group");
                cases++;
            }
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (int targetIndex in new[] { 0, 1 })
            foreach (bool reveal in new[] { false, true })
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 4, true);
                var hosts = new[] { board.Add(EnemyColor.Red, 2, 3), board.Add(EnemyColor.Red, 1, 4) };
                board.Copy(yellow, hosts[targetIndex], rate);
                Require(hosts[0].IsSpecial && hosts[1].IsSpecial, "A special mimic also promotes both host branches");
                board.Grid.ResolveTongueHit(yellow.Id, reveal ? EnemyColor.Red : EnemyColor.Yellow);
                foreach (var host in hosts)
                    Require(board.Grid.View(host.Id) == host && !host.IsSpecial,
                        "Revealing or destroying a special junction demotes both disconnected host branches");
                cases++;
            }
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (int targetIndex in new[] { 0, 1 })
            foreach (int hitIndex in new[] { 0, 1 })
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 4);
                var hosts = new[] { board.Add(EnemyColor.Blue, 2, 3), board.Add(EnemyColor.Blue, 1, 4) };
                board.Copy(yellow, hosts[targetIndex], rate);
                board.Grid.ResolveTongueHit(hosts[hitIndex].Id, EnemyColor.Blue);
                board.Grid.RefreshSpecials();
                var survivor = hosts[1 - hitIndex];
                Require(board.Grid.View(survivor.Id) == survivor && !survivor.IsSpecial &&
                    board.Grid.View(yellow.Id) == yellow && board.Grid.Model.IsAttached(yellow.Id) == (hitIndex != targetIndex),
                    "Removing either real Blue demotes the surviving host, even when it was not the selected host");
                cases++;
            }
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (int targetIndex in new[] { 0, 1 })
            using (var board = new Board(prefabs, missile, shield))
            {
                var yellow = board.Add(EnemyColor.Yellow, 2, 4);
                board.Add(EnemyColor.Yellow, 3, 4);
                var hosts = new[] { board.Add(EnemyColor.Blue, 2, 3), board.Add(EnemyColor.Blue, 1, 4) };
                board.Copy(yellow, hosts[targetIndex], rate);
                board.Grid.ResolveTongueHit(yellow.Id, EnemyColor.Green, true);
                Require(board.Grid.Model.Count == 0,
                    "A rainbow hit on the regular junction destroys both host branches and its Yellow group");
                cases++;
            }
            return cases;
        }
    }
}
