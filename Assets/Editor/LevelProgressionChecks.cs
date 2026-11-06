using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class LevelProgressionChecks
    {
        public const string FixtureJson = @"{
  ""schemaVersion"": 3,
  ""levels"": {
    ""1"": {
      ""backgroundImage"": """",
      ""possibleSoundtrackTracks"": [
        ""AnotherJoe""
      ],
      ""fleets"": [
        {
          ""possibleColors"": [
            ""Red"",
            ""Blue"",
            ""Green"",
            ""Purple"",
            ""Yellow""
          ],
          ""requiredColors"": [
            ""Green"",
            ""Purple""
          ],
          ""initialFleet"": [
            ""011223"",
            ""331120"",
            ""022331"",
            ""113320""
          ],
          ""lineSpawns"": [
            ""022113"",
            ""011332""
          ]
        },
        {
          ""possibleColors"": [
            ""Red"",
            ""Blue"",
            ""Green"",
            ""Purple"",
            ""Yellow""
          ],
          ""requiredColors"": [
            ""Green"",
            ""Purple""
          ],
          ""initialFleet"": [
            ""031122"",
            ""223310""
          ],
          ""lineSpawns"": [
            ""022113"",
            ""011332""
          ]
        }
      ]
    },
    ""2"": {
      ""backgroundImage"": """",
      ""possibleSoundtrackTracks"": [
        ""Contact""
      ],
      ""fleets"": [
        {
          ""possibleColors"": [
            ""Blue""
          ],
          ""requiredColors"": [
            ""Blue""
          ],
          ""initialFleet"": [
            ""0bbbbb"",
            ""0bbbbb"",
            ""0bbbbb""
          ],
          ""lineSpawns"": [
            ""0bbbbb""
          ]
        }
      ]
    }
  }
}";
        [MenuItem("Candy Cruisers/Run Level Map Checks")]
        public static void Run()
        {
            var random = UnityEngine.Random.state;
            bool previousOverride = SpawnOverride.Enabled;
            try
            {
                SpawnOverride.Enabled = false;
                UnityEngine.Random.InitState(673091);
                var catalog = LevelCatalog.Parse(FixtureJson);
                Check(catalog.ForLevel(1) == catalog.levels[0] && catalog.ForLevel(99) == catalog.levels[1], "Numbered map and final-level inheritance");
                CheckFleetRows(catalog);
                CheckValidation();
                CheckFleetProgression(catalog);
                CheckScripts();
                CheckWildcards();
                CheckWildcardLines();
                CheckSequences();
                CheckOpeningPalette();
                CheckFullClearSequence();
                CheckBackground(catalog, catalog.levels[0], catalog.levels[1]);
                var saved = LevelCatalog.Parse(File.ReadAllText(LevelCatalog.FilePath));
                Check(saved.ValidationErrors().Count == 0, "The actual saved level map validates");
                Debug.Log("Level map checks passed: JSON parsing, invalid settings, sparse fleets, exact letters, random palettes, required colors, round bindings, independent fleet palettes and line loops, cleared locks, restart/level resets, immutable plans, full-clear level advancement, progress bars and desktop/phone backgrounds.");
            }
            finally { SpawnOverride.Enabled = previousOverride; UnityEngine.Random.state = random; }
        }

        private static void CheckValidation()
        {
            Invalid(root => root["schemaVersion"] = 2, "schemaVersion must be 3");
            Invalid(root => root["levels"]["1"]["initialWaveLines"] = 2, "Unknown setting");
            Invalid(root => root["levels"]["1"]["enemiesToDefeat"] = 20, "Unknown setting");
            Invalid(root => root["levels"]["1"]["possibleColors"] = new JArray("Red"), "Unknown setting");
            Invalid(root => root["levels"]["1"]["fleets"] = JValue.CreateNull(), "list of fleet objects");
            Invalid(root => root["levels"]["1"]["fleets"] = new JArray(), "at least one fleet");
            Invalid(root => root["levels"]["1"]["fleets"][0] = new JArray("rrr"), "fleet object");
            Invalid(root => root["levels"]["1"]["fleets"][0] = JValue.CreateNull(), "fleet object");
            Invalid(root => FirstFleet(root)["spawnPaletteSource"] = "InitialFleet", "Unknown setting");
            Invalid(root => FirstFleet(root)["initialFleet"] = new JArray(), "nonempty list");
            Invalid(root => FirstFleet(root)["initialFleet"] = JValue.CreateNull(), "list of strings");
            Invalid(root => FirstFleet(root)["initialFleet"][0] = 123, "must be a string");
            Invalid(root => FirstFleet(root)["initialFleet"][0] = JValue.CreateNull(), "must be a string");
            Invalid(root => FirstFleet(root)["initialFleet"][0] = "011223/331120", "put each row in its own string");
            Invalid(root => FirstFleet(root)["initialFleet"] = new JArray("000000"), "at least one enemy");
            Invalid(root => FirstFleet(root)["initialFleet"] = new JArray("00000q"), "unknown character");
            Invalid(root => FirstFleet(root)["initialFleet"] = new JArray("12345", "123456"), "same row width");
            Invalid(root => FirstFleet(root)["initialFleet"][0] = "", "1–6 positions");
            Invalid(root => FirstFleet(root)["initialFleet"][0] = "rbrrbrr", "1–6 positions");
            Invalid(root => FirstFleet(root)["initialFleet"] = new JArray(Enumerable.Repeat("011223", 12)), "row count");
            Invalid(root => FirstFleet(root)["lineSpawns"] = new JArray("123123/123123"), "one row");
            Invalid(root => FirstFleet(root)["lineSpawns"] = new JArray("123"), "same row width");
            Invalid(root => FirstFleet(root)["possibleColors"] = new JArray("Red", "beige"), "unknown color");
            Invalid(root => FirstFleet(root)["possibleColors"] = new JArray(), "at least one valid color");
            Invalid(root => FirstFleet(root)["possibleColors"] = new JArray("Red", "Red"), "no duplicates");
            Invalid(root => FirstFleet(root)["requiredColors"] = new JArray("Orange"), "missing from possibleColors");
            Invalid(root => FirstFleet(root)["requiredColors"] = JValue.CreateNull(), "list of strings");
            Invalid(root => FirstFleet(root)["requiredColors"] = new JArray("Green", "Green"), "unique colors");
            Invalid(root => ((JObject)FirstFleet(root)).Property("initialFleet").Remove(), "initialFleet must be a list");
            Invalid(root => ((JObject)FirstFleet(root)).Property("lineSpawns").Remove(), "lineSpawns must be a list");
            Invalid(root => ((JObject)FirstFleet(root)).Property("possibleColors").Remove(), "possibleColors must be a list");
            Invalid(root => root["levels"]["2"]["fleets"][0]["initialFleet"] = new JArray("123123"), "numbered groups");
            Invalid(root => ((JObject)root["levels"]).Property("1").Remove(), "starting at 1");
            var optional = JObject.Parse(FixtureJson);
            ((JObject)FirstFleet(optional)).Property("requiredColors").Remove();
            Check(LevelCatalog.Parse(optional.ToString()).levels[0].fleets[0].requiredColors.Length == 0,
                "Omitted requiredColors defaults to an empty list");
            FirstFleet(optional)["requiredColors"] = new JArray();
            Check(LevelCatalog.Parse(optional.ToString()).levels[0].fleets[0].requiredColors.Length == 0,
                "Explicit empty requiredColors is also valid");
            try { LevelCatalog.Parse(FixtureJson.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 3, \"schemaVersion\": 3")); throw new Exception("Duplicate keys accepted"); }
            catch (FormatException) { }
        }
        private static JToken FirstFleet(JObject root) => root["levels"]["1"]["fleets"][0];

        private static void CheckFleetRows(LevelCatalog catalog)
        {
            var level = catalog.levels[0];
            var plan = SpawnScript.Plan(level.fleets[0]);
            Check(plan.Rows == 4 && plan.Columns == 6 && plan.CellCount == 20, "Row arrays preserve fleet dimensions and occupied counts");
            var cells = plan.CopyCells();
            string[] rows = { "011223", "331120", "022331", "113320" };
            for (int row = 0; row < rows.Length; row++)
            {
                var expected = SpawnScript.Decode(rows[row], plan.Bindings);
                for (int column = 0; column < expected.Length; column++)
                    Check(cells[row * plan.Columns + column] == expected[column], "Row arrays preserve top-to-bottom order, gaps and round-wide color bindings");
            }
            var single = SpawnScript.Plan(catalog.levels[1].fleets[0]);
            Check(single.Rows == 3 && single.CellCount == 15 && single.OccupiedColors().All(color => color == EnemyColor.Blue),
                "Literal rows load without changing colors");
        }
        private static void Invalid(Action<JObject> change, string expected)
        {
            var root = JObject.Parse(FixtureJson); change(root);
            try { LevelCatalog.Parse(root.ToString()); throw new Exception("Invalid map accepted: " + expected); }
            catch (FormatException error) { Check(error.Message.Contains(expected), "Actionable validation: " + expected + " got " + error.Message); }
        }

        private static void CheckScripts()
        {
            var fleet = new FleetDefinition {
                possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow },
                requiredColors = new[] { EnemyColor.Purple }, initialFleet = new[] { "312213" }, lineSpawns = new[] { "123123" } };
            Check(fleet.ValidationErrors().Count == 0, "Random script is valid");
            var assignments = new System.Collections.Generic.HashSet<string>();
            for (int sample = 0; sample < 80; sample++)
            {
                var plan = SpawnScript.Plan(fleet); var cells = plan.CopyCells();
                Check(cells[0] == cells[5] && cells[1] == cells[4] && cells[2] == cells[3] && plan.Colors.Count == 3 &&
                    plan.Colors.Contains(EnemyColor.Purple), "Equal digits agree, different digits differ, and required colors appear");
                Check(plan.SpawnPalette.SequenceEqual(plan.Colors) && plan.SpawnPalette.Count == 3,
                    "The spawn palette captures the actual opening rather than all possible colors");
                var line = SpawnScript.Decode("123123", plan.Bindings);
                Check(line[0] == cells[1] && line[1] == cells[2] && line[2] == cells[0], "Incoming lines reuse the fleet mapping");
                assignments.Add(string.Join(",", cells));
            }
            Check(assignments.Count > 1, "New fleets reroll their assignments");
            fleet.possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Purple };
            fleet.requiredColors = Array.Empty<EnemyColor>();
            fleet.initialFleet = new[] { "rrpprr" }; fleet.lineSpawns = new[] { "112211" };
            var literal = SpawnScript.Plan(fleet);
            Check(fleet.ValidationErrors().Count == 0 && literal.SpawnPalette.Count == 2 &&
                !literal.SpawnPalette.Contains(EnemyColor.Blue) && SpawnScript.Decode("112211", literal.Bindings).All(c => literal.Contains(c.Value)),
                "Line-only numbered groups stay within the literal opening's palette");
            fleet.lineSpawns = new[] { "123123" };
            Check(SpawnScript.Plan(fleet) == null && fleet.ValidationErrors().Any(e => e.Contains("initial fleet palette")),
                "A third line-only numbered group cannot introduce an absent color");
            fleet.lineSpawns = new[] { "bbbbbb" };
            Check(SpawnScript.Plan(fleet) == null, "Literal incoming colors also cannot introduce Blue after rrpprr");
            fleet.lineSpawns = Array.Empty<string>();
            fleet.requiredColors = new[] { EnemyColor.Blue };
            Check(SpawnScript.Plan(fleet) == null, "Required colors must actually appear in the opening");
            fleet.requiredColors = Array.Empty<EnemyColor>();
            fleet.initialFleet = new[] { "111111" }; fleet.lineSpawns = new[] { "pppppp" };
            for (int sample = 0; sample < 20; sample++)
                Check(SpawnScript.Plan(fleet).Colors.SequenceEqual(new[] { EnemyColor.Purple }),
                    "Planning selects compatible randomized openings instead of sometimes failing on literal lines");
            fleet.possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue };
            fleet.initialFleet = new[] { "0R0r0R", "b0B0b0" }; fleet.lineSpawns = new[] { "000000" };
            var sparse = SpawnScript.Plan(fleet);
            Check(sparse.Columns == 6 && sparse.Rows == 2 && sparse.CellCount == 6 && sparse.CopyCells()[0] == null &&
                sparse.CopyCells()[7] == null && sparse.PlayerColorWeights().Count == 2 &&
                sparse.PlayerColorWeights()[EnemyColor.Blue] == 6, "Blanks preserve positions, row depth, occupied counts and player weights");
            var copied = sparse.CopyCells(); copied[0] = EnemyColor.Green;
            Check(sparse.CopyCells()[0] == null, "A copied cell array cannot mutate a reserved fleet");
            fleet.possibleColors = EnemyPalette.Colors.ToArray();
            fleet.initialFleet = new[] { "o11223", "334455" }; fleet.lineSpawns = new[] { "112233" };
            var orange = SpawnScript.Plan(fleet);
            Check(orange != null && orange.OccupiedColors().Count(c => c == EnemyColor.Orange) == 1 &&
                !orange.Bindings.Values.Contains(EnemyColor.Orange), "A literal Orange stays unique across random color groups");
        }

        private static void CheckWildcards()
        {
            var json = JObject.Parse(FixtureJson);
            FirstFleet(json)["initialFleet"] = new JArray("1**001");
            FirstFleet(json)["lineSpawns"] = new JArray("**1**1");
            Check(LevelCatalog.Parse(json.ToString()).levels[0].fleets[0].initialFleet[0] == "1**001",
                "The authored map accepts stars alongside numbered groups and gaps");
            var fleet = new FleetDefinition { possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green },
                initialFleet = new[] { "1**001" }, lineSpawns = new[] { "*11***" } };
            var random = UnityEngine.Random.state;
            Check(fleet.ValidationErrors().Count == 0 && UnityEngine.Random.state.Equals(random),
                "Wildcard validation is deterministic and does not consume gameplay randomness");
            bool matched = false, differed = false;
            var variations = new System.Collections.Generic.HashSet<string>();
            for (int sample = 0; sample < 100; sample++)
            {
                var plan = SpawnScript.Plan(fleet); var cells = plan.CopyCells();
                Check(cells[0] == cells[5] && cells[1].HasValue && cells[2].HasValue && cells[3] == null && cells[4] == null &&
                    plan.Bindings.Count == 1 && !plan.Bindings.ContainsKey('*'),
                    "Each star resolves to its own cell without binding stars together or changing digits and gaps");
                matched |= cells[1] == cells[2]; differed |= cells[1] != cells[2];
                variations.Add(string.Join(",", cells));
                Check(cells.Where(c => c.HasValue).All(c => fleet.possibleColors.Contains(c.Value)) &&
                    plan.SpawnPalette.All(color => cells.Contains((EnemyColor?)color)), "Opening stars use only candidates and capture actual colors");
            }
            Check(matched && differed && variations.Count > 1, "Independent stars can agree or differ across randomized openings");

            fleet.possibleColors = fleet.requiredColors = EnemyPalette.Colors.ToArray();
            fleet.initialFleet = Enumerable.Repeat("******", GridModel.Rows).ToArray(); fleet.lineSpawns = new[] { "******" };
            for (int sample = 0; sample < 20; sample++)
            {
                var plan = SpawnScript.Plan(fleet);
                Check(plan != null && plan.CellCount == GridModel.Rows * GridModel.Columns && plan.Colors.Count == 6 &&
                    plan.OccupiedColors().Count(c => c == EnemyColor.Orange) == 1,
                    "A full grid of stars always satisfies required colors without duplicate Oranges or random retry failures");
            }
            fleet.possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green };
            fleet.requiredColors = new[] { EnemyColor.Blue, EnemyColor.Green }; fleet.initialFleet = new[] { "r*0000" };
            fleet.lineSpawns = Array.Empty<string>();
            Check(SpawnScript.Plan(fleet) == null, "One star cannot supply two missing required colors");
            fleet.requiredColors = Array.Empty<EnemyColor>(); fleet.initialFleet = new[] { "**0000" };
            fleet.lineSpawns = new[] { "112211" };
            var numbered = SpawnScript.Plan(fleet);
            Check(numbered != null && numbered.Colors.Count == 2 && numbered.Bindings.Values.All(numbered.Contains),
                "Opening stars can supply colors for line-only numbered groups without expanding the opening palette later");
            fleet.lineSpawns = new[] { "rrbbrr" };
            var constrained = SpawnScript.Plan(fleet);
            Check(constrained != null && constrained.Colors.Count == 2 && constrained.Contains(EnemyColor.Red) && constrained.Contains(EnemyColor.Blue),
                "Fixed incoming colors constrain stars in the opening so every line remains possible");
            fleet.possibleColors = new[] { EnemyColor.Orange }; fleet.initialFleet = new[] { "**" }; fleet.lineSpawns = Array.Empty<string>();
            Check(SpawnScript.Plan(fleet) == null, "An Orange-only pool cannot fill two opening stars within the one-Orange limit");

            fleet.possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Purple };
            fleet.initialFleet = new[] { "rrpprr" }; fleet.lineSpawns = new[] { "******" };
            var literal = SpawnScript.Plan(fleet);
            variations.Clear();
            var empty = new System.Collections.Generic.Dictionary<char, EnemyColor>();
            var orangePool = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Orange };
            for (int sample = 0; sample < 60; sample++)
            {
                var line = SpawnScript.Decode("******", literal.Bindings, literal.SpawnPalette);
                Check(line.All(c => c.HasValue && literal.Contains(c.Value) && c != EnemyColor.Blue),
                    "Incoming stars never introduce a candidate absent from the opening");
                variations.Add(string.Join(",", line));
                var orange = SpawnScript.Decode("***o**", empty, orangePool);
                Check(orange[3] == EnemyColor.Orange && orange.Count(c => c == EnemyColor.Orange) == 1 && orange.All(c => c.HasValue),
                    "A fixed Orange reserves its slot before independent stars are rolled");
            }
            Check(variations.Count > 1, "A repeated incoming wildcard script rerolls its cells on each use");
            var boundOrange = SpawnScript.Decode("*****1", new System.Collections.Generic.Dictionary<char, EnemyColor> { ['1'] = EnemyColor.Orange }, orangePool);
            Check(boundOrange[5] == EnemyColor.Orange && boundOrange.Count(c => c == EnemyColor.Orange) == 1,
                "A numbered Orange also reserves its slot before stars roll");
            var limited = SpawnScript.Decode("******", empty, new[] { EnemyColor.Orange });
            Check(limited.Count(c => c == EnemyColor.Orange) == 1 && limited.Count(c => c.HasValue) == 1 &&
                SpawnScript.Decode("******", empty, Array.Empty<EnemyColor>()).All(c => c == null),
                "Incoming stars leave gaps when the Orange cap or cleared-color locks exhaust their eligible pool");
        }

        private static void CheckWildcardLines()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var fleet = new FleetDefinition { possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Orange },
                    initialFleet = new[] { "rborbb" }, lineSpawns = new[] { "******", "*1***1" } };
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                session.LevelCatalog = new LevelCatalog { levels = new[] { new LevelDefinition { fleets = new[] { fleet } } } };
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                var plan = spawner.PlanNextFleet();
                Check(spawner.SpawnBatch(plan), "Wildcard-line fixture activates its opening palette");
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Red).ToArray())
                    grid.ClearMatchingChain(enemy.Id, enemy.Color);
                Check(grid.IsColorCleared(EnemyColor.Red) && grid.Fleet.ColorCount(EnemyColor.Orange) == 1 && spawner.TryAdvance(),
                    "The wildcard line uses the live cleared-color locks and Orange count");
                for (int column = 0; column < 6; column++)
                    Check(grid.Model.At(column, 0)?.Color == EnemyColor.Blue, "Every wildcard chooses the remaining eligible Blue instead of leaving random holes");
                Check(spawner.TryAdvance(), "A mixed wildcard and numbered incoming line spawns");
                for (int column = 0; column < 6; column++)
                {
                    EnemyColor? expected = (column == 1 || column == 5) && plan.Bindings['1'] == EnemyColor.Red ? (EnemyColor?)null : EnemyColor.Blue;
                    Check(grid.Model.At(column, 0)?.Color == expected, "Stars reroll legally while fixed numbered cells keep their original assignment and clear locks");
                }
                Check(grid.Fleet.ColorCount(EnemyColor.Orange) == 1 && grid.Fleet.ColorCount(EnemyColor.Red) == 0,
                    "Incoming wildcards cannot duplicate Orange or revive a cleared color");
            });
        }

        private static void CheckSequences()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                var first = new LevelDefinition { fleets = new[] {
                    new FleetDefinition { possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green },
                        initialFleet = new[] { "101020", "200010" }, lineSpawns = new[] { "100002", "020010" } },
                    new FleetDefinition { possibleColors = new[] { EnemyColor.Purple, EnemyColor.Yellow },
                        initialFleet = new[] { "212" }, lineSpawns = new[] { "121", "202" } }
                } };
                var second = new LevelDefinition { fleets = new[] {
                    new FleetDefinition { initialFleet = new[] { "0r0b00" }, lineSpawns = new[] { "b0000r" } } } };
                session.LevelCatalog = new LevelCatalog { levels = new[] { first, second } };
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                var opening = spawner.PlanNextFleet();
                Check(opening.Rows == 2 && opening.CellCount == 5 && spawner.SpawnBatch(opening), "First fleet uses entry zero with exact gaps");
                for (int i = 0; i < 3; i++)
                {
                    Check(spawner.TryAdvance(), "Scripted top row advances");
                    VerifyRow(grid, first.fleets[0].lineSpawns[i % 2], opening);
                }
                Empty(grid);
                var next = spawner.PlanNextFleet();
                Check(next.Rows == 1 && next.Columns == 3 && next.CellCount == 3 && spawner.SpawnBatch(next) &&
                    next.Colors.All(c => c == EnemyColor.Purple || c == EnemyColor.Yellow),
                    "The next fleet owns independent width, palette, row count and incoming scripts");
                Check(spawner.TryAdvance(), "Next fleet has an incoming line");
                VerifyRow(grid, first.fleets[1].lineSpawns[0], next);
                Check(grid.Model.At(3, 0) == null, "The narrower fleet does not inherit the previous width");
                Empty(grid);
                var wrapped = spawner.PlanNextFleet();
                Check(wrapped.Rows == 2 && wrapped.CellCount == 5, "Manual fleet list lookup wraps to its first entry");
                var copiedBindings = wrapped.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value);
                first.fleets[0].lineSpawns[0] = "000000";
                Check(wrapped.LineSpawns[0] == "100002" && wrapped.Bindings.All(pair => copiedBindings[pair.Key] == pair.Value),
                    "Pending line scripts and mappings survive edits");
                session.Progress.Reset(2);
                var levelTwo = spawner.PlanNextFleet();
                Check(spawner.SpawnBatch(levelTwo) && spawner.TryAdvance(), "Entering a level resets both cursors");
                VerifyRow(grid, "b0000r", levelTwo);
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Red).ToArray())
                    if (grid.View(enemy.Id) != null) grid.ClearMatchingChain(enemy.Id, enemy.Color);
                Check(grid.IsColorCleared(EnemyColor.Red) && spawner.TryAdvance() && grid.Fleet.ColorCount(EnemyColor.Red) == 0 &&
                    grid.Model.At(5, 0) == null, "Cleared colors leave holes instead of changing their assignments or respawning");
                Empty(grid); session.Progress.Reset(1); spawner.ResetSequences();
                Check(spawner.PlanNextFleet().Rows == 2, "A run reset restarts the fleet sequence");
            });
        }

        private static void CheckOpeningPalette()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                session.LevelCatalog = new LevelCatalog { levels = new[] { new LevelDefinition { fleets = new[] {
                    new FleetDefinition { possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Purple },
                        initialFleet = new[] { "rrpprr" }, lineSpawns = new[] { "112211" } }
                } } } };
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                Check(spawner.SpawnBatch(spawner.PlanNextFleet()), "Literal opening fixture spawns");
                Check(grid.TryDescend(), "Expose legal summon gaps above the full opening row");
                var source = grid.GetComponentsInChildren<GridEnemy>().First(e => e.Color == EnemyColor.Purple);
                for (int i = 0; i < 40; i++)
                {
                    var summoned = spawner.TrySummon(source);
                    Check(summoned != null && summoned.Color != EnemyColor.Blue,
                        "Purple summons only colors captured in the opening despite a broader candidate pool");
                    grid.Unregister(summoned); UnityEngine.Object.DestroyImmediate(summoned.gameObject);
                }
                Check(spawner.TryAdvance() && !grid.Fleet.Colors.Contains(EnemyColor.Blue),
                    "Incoming numbered lines cannot expand the opening palette either");
            });
        }

        private static void VerifyRow(EnemyGrid grid, string pattern, FleetPlan round)
        {
            var expected = SpawnScript.Decode(pattern, round.Bindings);
            for (int column = 0; column < expected.Length; column++)
                Check(grid.Model.At(column, 0)?.Color == expected[column], "Exact top-row positions and round bindings");
        }
        private static void CheckFullClearSequence()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var first = new LevelDefinition { fleets = new[] {
                    new FleetDefinition { initialFleet = new[] { "r0b000" }, lineSpawns = new[] { "00000b", "0b0000" } },
                    new FleetDefinition { initialFleet = new[] { "bb0rr0", "00b000" }, lineSpawns = new[] { "0b0000", "00000b" } }
                } };
                var second = new LevelDefinition { fleets = new[] {
                    new FleetDefinition { possibleColors = new[] { EnemyColor.Green },
                        initialFleet = new[] { "0g0g00" }, lineSpawns = Array.Empty<string>() } } };
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                session.LevelCatalog = new LevelCatalog { levels = new[] { first, second } }; session.PresentationPreview = true;
                var lifecycle = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable", lifecycle).Invoke(session, null);
                typeof(GameSession).GetMethod("Start", lifecycle).Invoke(session, null);
                var music = grid.GetComponent<GameplayMusicPlayer>(); music.StopPlayback();
                session.StartLevel = 1;
                session.StartRun(); session.Tick(20);
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                Check(grid.Model.Count == 2 && session.Progress.ClearedFleets == 0, "Starting the first scripted fleet does not complete it");
                grid.ClearMatchingChain(grid.Model.At(0, 0).Id, EnemyColor.Red);
                Check(session.Progress.Level == 1 && session.Progress.ClearedFleets == 0 && spawner.TryAdvance(),
                    "A partial clear keeps the same fleet and can still add incoming enemies");
                grid.ClearMatchingChain(grid.Model.At(2, 1).Id, EnemyColor.Blue);
                Check(grid.Model.Count == 1 && session.Progress.ClearedFleets == 0 && session.State == GameSession.RunState.Playing,
                    "Clearing the original enemies does not finish the fleet while an incoming-line enemy survives");
                grid.ClearMatchingChain(grid.Model.At(5, 0).Id, EnemyColor.Blue);
                Check(session.Progress.Level == 1 && session.Progress.ClearedFleets == 1 && session.Progress.ActiveLevelProgressFraction == .5f &&
                    session.State == GameSession.RunState.Refilling && session.Fleet.PendingPlan.Rows == 2,
                    "The first full clear reserves the second scripted fleet and shows half level progress");
                session.Tick(100); session.Tick(100);
                Check(session.State == GameSession.RunState.Playing && grid.Model.Count == 5 && grid.Model.At(2, 1)?.Color == EnemyColor.Blue,
                    "The next spawn uses the next fleet entry instead of wrapping or changing levels early");
                Check(spawner.TryAdvance() && grid.Model.At(1, 0)?.Color == EnemyColor.Blue && grid.Model.At(5, 0) == null,
                    "Each incoming-line cursor starts at zero for its own fleet");
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().ToArray())
                    if (grid.View(enemy.Id) != null) grid.ClearMatchingChain(enemy.Id, enemy.Color);
                Check(session.Progress.Level == 2 && session.Progress.ClearedFleets == 0 && session.Fleet.PendingPlan.CellCount == 2 &&
                    session.Fleet.PendingPlan.Colors.SequenceEqual(new[] { EnemyColor.Green }),
                    "Clearing the last scripted fleet advances once and reserves entry zero of the new level");
                session.Tick(100); session.Tick(100);
                Check(grid.Model.Count == 2 && grid.Model.ColorCount(EnemyColor.Green) == 2, "The next level starts with its own first fleet");
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnDisable", lifecycle).Invoke(session, null);
            });
        }
        private static void Empty(EnemyGrid grid)
        {
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }
        private static void CheckFleetProgression(LevelCatalog catalog)
        {
            var progress = new RunProgress { LevelCatalog = catalog };
            progress.Reset();
            Check(progress.UsesScriptedProgression && progress.FleetsToClear == 2 && progress.ClearedFleets == 0 &&
                progress.ActiveLevelProgressFraction == 0, "Level progress starts at zero of its scripted fleet count");
            progress.RegisterClear(1000, false, 6000);
            Check(progress.Level == 1 && progress.Defeated == 1000 && progress.ClearedFleets == 0 &&
                !progress.LevelUpPending && progress.BankedLevelProgressFraction == 0 && progress.ActiveLevelProgressFraction == 0,
                "Enemy defeats and score multipliers cannot advance or bank a scripted level");
            Check(progress.RegisterClear(1, true) == 10100 && progress.Level == 1 && progress.ClearedFleets == 1 &&
                progress.ActiveLevelProgressFraction == .5f, "The first full clear completes one fleet and keeps the current level");
            progress.RegisterClear(1000, false);
            Check(progress.Level == 1 && progress.ClearedFleets == 1 && progress.ActiveLevelProgressFraction == .5f,
                "Additional enemies within the next round do not consume another scripted fleet");
            long score = progress.Score;
            Check(progress.RegisterClear(0, true) == 0 && progress.Score == score && progress.ClearedFleets == 1,
                "Duplicate empty clears cannot advance the sequence or award another bonus");
            Check(progress.RegisterClear(1, true) == 10100 && progress.Level == 2 && progress.ClearedFleets == 0 &&
                progress.FleetsToClear == 1 && progress.ActiveLevelProgressFraction == 0 && progress.Defeated == 2002,
                "The last fleet advances exactly one level, resets fleet progress and preserves actual defeats");
            Check(progress.RegisterClear(1, true) == 20110 && progress.Level == 3 && progress.ClearedFleets == 0,
                "A one-fleet level advances on its first full clear and scores at the outgoing level");
            progress.RegisterClear(1, true);
            Check(progress.Level == 4 && progress.FleetsToClear == 1, "Levels beyond the map repeat the final fleet sequence");
            progress.Reset(1);
            progress.RegisterClear(1, true);
            progress.Reset(1);
            Check(progress.ClearedFleets == 0 && progress.FleetsToClear == 2 && progress.Defeated == 0 && progress.Score == 0,
                "Restart clears fleet completion, defeats and score");
            progress.Reset(99);
            Check(progress.Level == 99 && progress.FleetsToClear == 1 && progress.ClearedFleets == 0 && progress.Defeated == 0,
                "Starting at a later level begins with its first fleet and no fabricated defeats");
            progress.LevelCatalog = null; progress.Reset(2);
            Check(!progress.UsesScriptedProgression && progress.Defeated == 20 && progress.NextThreshold == 60,
                "Procedural development fixtures retain their independent progression");
        }

        private static void CheckBackground(LevelCatalog catalog, LevelDefinition first, LevelDefinition second)
        {
            var root = new GameObject("Level background camera", typeof(Camera), typeof(GameplayFraming));
            var background = new GameObject("Level background", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            var sessionRoot = new GameObject("Level background session", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var texture = new Texture2D(16, 8);
            var image = Sprite.Create(texture, new Rect(0, 0, 16, 8), new Vector2(.1f, .8f), 8);
            try
            {
                var session = sessionRoot.AddComponent<GameSession>(); session.LevelCatalog = catalog;
                var framing = root.GetComponent<GameplayFraming>();
                var camera = root.GetComponent<Camera>(); camera.orthographic = true;
                framing.SetSession(session); framing.SetBackground(background);
                first.backgroundImage = image;
                foreach (var screen in new[] { new Vector2(1920, 1080), new Vector2(540, 960), new Vector2(1206, 2622) })
                {
                    camera.aspect = screen.x / screen.y;
                    GameplayFraming.ForceIphoneNotchOffsetForTests = screen.x == 1206;
                    framing.SetWaveCrunch(1);
                    framing.TickBackground(5);
                    Check(background.enabled && background.sprite == image && background.color == Color.white,
                        "Configured image overrides black mode without tinting");
                    var bounds = background.bounds;
                    Check(camera.WorldToViewportPoint(bounds.min).x <= .0001f && camera.WorldToViewportPoint(bounds.min).y <= .0001f &&
                        camera.WorldToViewportPoint(bounds.max).x >= .9999f && camera.WorldToViewportPoint(bounds.max).y >= .9999f,
                        "Image covers desktop and phone aspect ratios, including a custom pivot and notch offset");
                    var position = background.transform.position;
                    framing.TickBackground(5);
                    Check(background.transform.position == position && background.transform.rotation == Quaternion.identity,
                        "Repeated updates do not drift or rotate configured art");
                    framing.SetWaveCrunch(0);
                }
                session.Progress.Reset(2); framing.TickBackground(0);
                Check(!background.enabled && camera.backgroundColor == Color.black, "Unassigned next-level image restores black");
            }
            finally
            {
                first.backgroundImage = null;
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(background.gameObject);
                UnityEngine.Object.DestroyImmediate(sessionRoot); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Level map check failed: " + message); }
    }
}
