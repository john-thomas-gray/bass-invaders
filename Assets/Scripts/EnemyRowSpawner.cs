using System.Linq;
using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid), typeof(EnemyGridMovement))]
    public sealed class EnemyRowSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject bluePrefab;
        [SerializeField] private GameObject redPrefab;
        [SerializeField] private GameObject greenPrefab;
        [SerializeField] private GameObject purplePrefab;
        [SerializeField] private GameObject yellowPrefab;
        [SerializeField] private GameObject orangePrefab;
        [SerializeField] private Sprite specialSprite;
        private EnemyGrid grid;
        private EnemyGridMovement movement;
        private FleetState Fleet => (grid != null ? grid : grid = GetComponent<EnemyGrid>()).Fleet;
        private LevelDefinition Definition => GetComponent<GameSession>()?.CurrentLevelDefinition;
        private bool UsesLevelDefinition
        {
            get
            {
#if UNITY_EDITOR
                if (SpawnOverride.Enabled) return false;
#endif
                return Definition != null;
            }
        }
        public const int OrangeSpawnOdds = 75;
        public event System.Action BottomReached;

        public void Configure(GameObject blue, GameObject red)
        { bluePrefab = blue; redPrefab = red; }
        public void ConfigureNewTypes(GameObject green, GameObject purple, GameObject yellow, GameObject orange = null)
        { greenPrefab = green; purplePrefab = purple; yellowPrefab = yellow; orangePrefab = orange; }
        public void ConfigureOrange(GameObject orange) => orangePrefab = orange;
        public void ConfigureSpecialSprite(Sprite sprite) => specialSprite = sprite;
        public Sprite SpecialSprite => specialSprite != null ? specialSprite : EnemyPlaceholderArt.Triangle;
        public GameObject Prefab(EnemyColor color) => color == EnemyColor.Blue ? bluePrefab :
            color == EnemyColor.Red ? redPrefab : color == EnemyColor.Green ? greenPrefab :
            color == EnemyColor.Purple ? purplePrefab : color == EnemyColor.Orange ? orangePrefab : yellowPrefab;
        public Sprite AppearanceSprite(EnemyColor color, bool special = false) =>
            special || color == EnemyColor.Orange ? SpecialSprite : Prefab(color)?.GetComponentInChildren<SpriteRenderer>()?.sprite;
        public int Level => GetComponent<GameSession>()?.Progress.Level ?? 1;
        public int CurrentRowWidth => Fleet.Count > 0 && Fleet.ActivePlan != null ?
            Fleet.ActivePlan.Columns : RunProgress.RowWidthForLevel(Level);
        public void ApplyAppearance(GridEnemy enemy)
        {
            var prefab = Prefab(enemy.Color);
            if (prefab == null) return;
            var ordinary = prefab.GetComponentInChildren<SpriteRenderer>().sprite;
            var sprite = AppearanceSprite(enemy.Color, enemy.IsSpecial);
            enemy.Visuals.Body.sprite = sprite;
            enemy.Visuals.Root.localScale = Vector3.one * (ordinary.bounds.size.x / sprite.bounds.size.x);
        }

        public System.Collections.Generic.List<EnemyColor> UnlockedColors()
        {
            if (UsesLevelDefinition)
                return new System.Collections.Generic.List<EnemyColor>(Fleet.SpawnPalette ??
                    Fleet.PendingPlan?.SpawnPalette ?? System.Array.Empty<EnemyColor>());
            var eligible = new System.Collections.Generic.List<EnemyColor>();
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
            {
                bool allowed = RunProgress.IsUnlocked(color, Level);
#if UNITY_EDITOR
                if (SpawnOverride.Enabled) allowed = SpawnOverride.Allows(color);
#endif
                if (allowed) eligible.Add(color);
            }
            return eligible;
        }

        private System.Collections.Generic.List<EnemyColor> SpawnColors(bool newCycle = false)
        {
            if (grid == null) grid = GetComponent<EnemyGrid>();
            var colors = UnlockedColors();
            colors.RemoveAll(color => (!newCycle && grid.IsColorCleared(color)) || Prefab(color) == null ||
                Prefab(color).GetComponentInChildren<SpriteRenderer>() == null);
            return colors;
        }

        public GridEnemy TrySummon(GridEnemy source)
        {
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled) return null;
            if (grid == null) grid = GetComponent<EnemyGrid>();
            if (grid.View(source.Id) != source) return null;
            var session = GetComponent<GameSession>();
            if (session != null && (session.State != GameSession.RunState.Playing || session.IsPaused)) return null;
            var colors = SpawnColors();
            if (colors.Count == 0) return null;
            var candidates = SummonCandidates(grid.Model, source.Color == EnemyColor.Purple && source.IsSpecial);
            foreach (int anchor in new System.Collections.Generic.List<int>(candidates.Keys))
            {
                candidates[anchor].RemoveAll(cell => !colors.Exists(color => CanSpawn(color, cell.x, cell.y)));
                if (candidates[anchor].Count == 0) candidates.Remove(anchor);
            }
            // Match the original two-stage random choice: anchor, then neighboring gap.
            var anchors = new System.Collections.Generic.List<int>(candidates.Keys);
            if (anchors.Count == 0) return null;
            var gaps = candidates[anchors[Random.Range(0, anchors.Count)]];
            var cell = gaps[Random.Range(0, gaps.Count)];
            colors.RemoveAll(color => !CanSpawn(color, cell.x, cell.y));
            bool orange = colors.Remove(EnemyColor.Orange) && Random.Range(0, OrangeSpawnOdds) == 0;
            if (!orange && colors.Count == 0) return null;
            var summoned = Spawn(orange ? EnemyColor.Orange : colors[Random.Range(0, colors.Count)], cell.x, cell.y,
                EnemyColor.Purple, true);
            if (summoned != null)
            {
                source.GetComponent<EnemyPresentation>()?.SummonFlash();
                GetComponent<SoundEffects>()?.PlayCue(SoundEffect.PurpleWarp);
            }
            session?.CheckPlayerContact();
            return summoned;
        }

        public bool CanSummon(GridEnemy source)
        {
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled) return false;
            if (grid == null) grid = GetComponent<EnemyGrid>();
            if (grid.View(source.Id) != source) return false;
            var session = GetComponent<GameSession>();
            if (session != null && (session.State != GameSession.RunState.Playing || session.IsPaused)) return false;
            var colors = SpawnColors();
            if (colors.Count == 0) return false;
            var candidates = SummonCandidates(grid.Model, source.Color == EnemyColor.Purple && source.IsSpecial);
            foreach (int anchor in new System.Collections.Generic.List<int>(candidates.Keys))
            {
                candidates[anchor].RemoveAll(cell => !colors.Exists(color => CanSpawn(color, cell.x, cell.y)));
                if (candidates[anchor].Count == 0) candidates.Remove(anchor);
            }
            return candidates.Count > 0;
        }

        public System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector2Int>> SummonCandidates(GridModel model, bool special = false)
        {
            var candidates = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector2Int>>();
            var offsets = new[] { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up };
            int farthest = -1;
            int lowestOccupiedRow = -1;
            for (int row = 0; row < GridModel.Rows; row++)
            for (int column = 0; column < GridModel.Columns; column++)
            {
                if (model.At(column, row) == null) continue;
                lowestOccupiedRow = row;
                var gaps = new System.Collections.Generic.List<Vector2Int>();
                foreach (var offset in offsets)
                {
                    var cell = new Vector2Int(column, row) + offset;
                    if (GridModel.InBounds(cell.x, cell.y) && model.At(cell.x, cell.y) == null) gaps.Add(cell);
                }
                if (gaps.Count == 0) continue;
                farthest = row * GridModel.Columns + column;
                candidates.Add(farthest, gaps);
            }
            foreach (int anchor in new System.Collections.Generic.List<int>(candidates.Keys))
            {
                int row = anchor / GridModel.Columns, column = anchor % GridModel.Columns;
                candidates[anchor].RemoveAll(cell =>
                    (special ? cell.y > lowestOccupiedRow + 1 :
                        row == farthest / GridModel.Columns && (cell.y > row || anchor == farthest && cell.x > column)) ||
                    !FitsWaveRowWidth(model, cell) ||
                    Mathf.Abs(transform.TransformPoint(GetComponent<EnemyGrid>().CellPosition(cell.x, cell.y)).x) + .375f > PlayerMovement.HalfWidth + .00001f);
                if (candidates[anchor].Count == 0) candidates.Remove(anchor);
            }
            return candidates;
        }

        private bool FitsWaveRowWidth(GridModel model, Vector2Int cell)
        {
            int maximum = Fleet.ActivePlan != null ? Fleet.ActivePlan.Columns : CurrentRowWidth;
            int first = cell.x, last = cell.x;
            for (int column = 0; column < GridModel.Columns; column++)
            {
                if (model.At(column, cell.y) == null) continue;
                first = Mathf.Min(first, column);
                last = Mathf.Max(last, column);
            }
            return last - first + 1 <= maximum;
        }

        private void OnEnable()
        {
            grid = GetComponent<EnemyGrid>();
            movement = GetComponent<EnemyGridMovement>();
            movement.SweepEnded += OnSweepEnded;
        }
        private void OnDisable()
        {
            if (movement != null) movement.SweepEnded -= OnSweepEnded;
        }
        private void OnSweepEnded() => TryAdvance();

        public bool TryAdvance()
        {
            var session = GetComponent<GameSession>();
            if (session != null && (session.State != GameSession.RunState.Playing || session.IsPaused)) return false;
            if (grid == null) grid = GetComponent<EnemyGrid>();
            var colors = SpawnColors();
            if (colors.Count == 0 && SpawnColors(true).Count == 0)
            {
                Debug.LogWarning("No configured enemy prefabs match the allowed spawn types.", this);
                return false;
            }
            if (!grid.Model.CanDescend) { BottomReached?.Invoke(); return false; }
            int rowWidth = CurrentRowWidth;
            if (!grid.AlignForNewRow(rowWidth))
            {
                Debug.LogError("Cannot fit the new row within the playfield.", this);
                return false;
            }
            if (!grid.TryDescend()) return false;
            if (Fleet.ActivePlan != null && Fleet.ActivePlan.IsScripted) SpawnScriptedLine();
            else if (colors.Count > 0) SpawnRow(0, colors, rowWidth);
            session?.CheckPlayerContact();
            return true;
        }

        private int sequenceLevel = -1;
        private int nextFleet, nextLine;
        public void ResetSequences() { sequenceLevel = -1; nextFleet = nextLine = 0; }
        private void EnsureSequenceLevel()
        {
            if (sequenceLevel == Level) return;
            sequenceLevel = Level; nextFleet = nextLine = 0;
        }

        public FleetPlan PlanNextFleet()
        {
            if (!UsesLevelDefinition)
                return SnapshotPlan(Level == RunProgress.MinLevel ? PlanOpening() : PlanBatch(GetComponent<GameSession>().Progress.BatchRows));
            EnsureSequenceLevel();
            var definition = Definition;
            var errors = definition.ValidationErrors();
            if (errors.Count > 0) { Debug.LogError(string.Join(" ", errors), this); return null; }
            var plan = SpawnScript.Plan(definition.fleets[nextFleet]);
            if (plan == null) { Debug.LogError("Cannot resolve the scripted fleet for level " + Level, this); return null; }
            foreach (var color in plan.SpawnPalette)
                if (Prefab(color) == null || Prefab(color).GetComponentInChildren<SpriteRenderer>() == null)
                { Debug.LogError("Missing enemy prefab for scripted color " + color, this); return null; }
            nextFleet = (nextFleet + 1) % definition.fleets.Length;
            return plan;
        }

        private void SpawnScriptedLine()
        {
            var round = Fleet.ActivePlan;
            if (round.LineSpawns.Count == 0) return;
            var wildcardColors = new System.Collections.Generic.List<EnemyColor>(round.SpawnPalette);
            wildcardColors.RemoveAll(color => Fleet.IsCleared(color) ||
                color == EnemyColor.Orange && Fleet.ColorCount(color) > 0);
            var cells = SpawnScript.Decode(round.LineSpawns[nextLine % round.LineSpawns.Count], round.Bindings, wildcardColors);
            nextLine = (nextLine + 1) % round.LineSpawns.Count;
            for (int column = 0; column < cells.Length; column++)
                if (cells[column].HasValue && !Fleet.IsCleared(cells[column].Value) && CanSpawn(cells[column].Value, column, 0))
                    Spawn(cells[column].Value, column, 0);
        }

        public bool SpawnBatch(int rows)
        {
            return SpawnBatch(PlanBatch(rows));
        }

        public EnemyColor[] PlanOpening()
        {
            var colors = SpawnColors(true);
            if (colors.Count != 2 || !colors.Contains(EnemyColor.Red) || !colors.Contains(EnemyColor.Blue))
                return PlanBatch(2, RunProgress.StandardRowWidth);
            return RedBlueOpening();
        }

        private static EnemyColor[] RedBlueOpening()
        {
            int rowWidth = RunProgress.StandardRowWidth;
            var plan = new EnemyColor[2 * rowWidth];
            for (int i = 0; i < plan.Length; i++)
                plan[i] = ((i % rowWidth != rowWidth / 2) == (i / rowWidth == 0))
                    ? EnemyColor.Blue : EnemyColor.Red;
            return plan;
        }

        public EnemyColor[] PlanBatch(int rows)
        {
            return PlanBatch(rows, RunProgress.RowWidthForLevel(Level));
        }

        private EnemyColor[] PlanBatch(int rows, int rowWidth)
        {
            var colors = SpawnColors(true);
            if (rows < 1 || rows > GridModel.Rows || colors.Count == 0) return null;
            rowWidth = Mathf.Clamp(rowWidth, 1, GridModel.Columns);
            var plan = new EnemyColor[rows * rowWidth];
            bool orange = colors.Remove(EnemyColor.Orange);
            if (colors.Count == 0)
            {
                // The spawn cap leaves just one occupied slot for an Orange-only override.
                for (int i = 0; i < plan.Length; i++) plan[i] = EnemyColor.Orange;
                return plan;
            }
            int orangeRow = orange ? Random.Range(0, rows) : -1;
            for (int row = 0; row < rows; row++)
                System.Array.Copy(row == orangeRow ? PlanOrangeRow(colors, rowWidth) : PlanRow(colors, rowWidth),
                    0, plan, row * rowWidth, rowWidth);
            return plan;
        }

        private static EnemyColor[] PlanOrangeRow(System.Collections.Generic.List<EnemyColor> colors, int rowWidth)
        {
            var row = new EnemyColor[rowWidth];
            if (rowWidth < RunProgress.WideRowWidth && colors.Count == 1)
            {
                // AAOAA keeps the unique Orange and caps both ordinary runs at two.
                for (int column = 0; column < rowWidth; column++)
                    row[column] = column == rowWidth / 2 ? EnemyColor.Orange : colors[0];
                return row;
            }
            bool left = Random.Range(0, 2) == 0;
            row[left ? 0 : rowWidth - 1] = EnemyColor.Orange;
            if (rowWidth > 1) System.Array.Copy(PlanRow(colors, rowWidth - 1, 2, null,
                MaxRunLengthForWidth(rowWidth)), 0, row, left ? 1 : 0, rowWidth - 1);
            return row;
        }

        private static int MaxRunLengthForWidth(int rowWidth) => rowWidth < RunProgress.WideRowWidth ? 2 : rowWidth;

        private static EnemyColor[] PlanRow(System.Collections.Generic.List<EnemyColor> eligible, int rowWidth, int maxGroups = 3,
            System.Collections.Generic.List<EnemyColor> required = null, int maxRunLength = 0)
        {
            var colors = new System.Collections.Generic.List<EnemyColor>(eligible);
            rowWidth = Mathf.Clamp(rowWidth, 1, GridModel.Columns);
            if (maxRunLength == 0) maxRunLength = MaxRunLengthForWidth(rowWidth);
            // A single eligible color cannot fill a row without exceeding the cap.
            if (colors.Count == 1) maxRunLength = rowWidth;
            int groups = Mathf.Min(maxGroups, colors.Count, rowWidth);
            if (required != null)
                for (int i = 0; i < required.Count; i++)
                {
                    int index = colors.IndexOf(required[i]);
                    var swap = colors[i]; colors[i] = colors[index]; colors[index] = swap;
                }
            // Select distinct colors first so required colors retain their guaranteed appearance.
            for (int i = 0; i < groups; i++)
            {
                int selected = required != null && i < required.Count ? i : Random.Range(i, colors.Count);
                var swap = colors[i]; colors[i] = colors[selected]; colors[selected] = swap;
            }
            var runColors = colors.GetRange(0, groups);
            // Five slots with two colors need three runs: ABA or BAB.
            if (groups * maxRunLength < rowWidth)
            {
                if (Random.Range(0, 2) == 0) runColors.Reverse();
                runColors.Add(runColors[0]);
            }
            groups = runColors.Count;
            var sizes = new int[groups];
            for (int i = 0; i < groups; i++) sizes[i] = 1;
            var expandable = new System.Collections.Generic.List<int>();
            for (int i = 0; i < groups; i++) if (runColors[i] != EnemyColor.Orange && maxRunLength > 1) expandable.Add(i);
            for (int i = groups; i < rowWidth; i++)
            {
                int selected = expandable[Random.Range(0, expandable.Count)];
                if (++sizes[selected] >= maxRunLength) expandable.Remove(selected);
            }
            var row = new EnemyColor[rowWidth];
            int column = 0;
            for (int group = 0; group < groups; group++)
                for (int i = 0; i < sizes[group]; i++) row[column++] = runColors[group];
            return row;
        }

        public bool SpawnBatch(EnemyColor[] plan)
        {
            int rowWidth = RowWidthForPlan(plan);
            return rowWidth > 0 && SpawnBatch(new FleetPlan(plan, rowWidth));
        }

        public FleetPlan SnapshotPlan(EnemyColor[] plan)
        {
            int width = RowWidthForPlan(plan);
            return width > 0 ? new FleetPlan(plan, width) : null;
        }

        public bool SpawnBatch(FleetPlan plan)
        {
            if (grid == null) grid = GetComponent<EnemyGrid>();
            if (Fleet.Count != 0 || plan == null) return false;
            var cells = plan.CopyCells();
            foreach (var color in cells)
                if (color.HasValue && (!System.Enum.IsDefined(typeof(EnemyColor), color.Value) || Prefab(color.Value) == null ||
                    Prefab(color.Value).GetComponentInChildren<SpriteRenderer>() == null)) return false;
            Fleet.Begin(plan, UsesLevelDefinition);
            int rowWidth = plan.Columns;
            if (plan.IsScripted)
            {
                nextLine = 0;
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i].HasValue) Spawn(cells[i].Value, i % rowWidth, i / rowWidth);
            }
            else for (int start = 0; start < cells.Length; start += rowWidth)
            {
                var row = new EnemyColor[rowWidth];
                for (int column = 0; column < rowWidth; column++) row[column] = cells[start + column].Value;
                SpawnPlannedRow(row, start / rowWidth);
            }
            return true;
        }

        public int RowWidthForPlan(EnemyColor[] plan)
        {
            if (plan == null || plan.Length == 0) return 0;
            int rowWidth = RunProgress.RowWidthForLevel(Level);
            if (plan.Length % rowWidth == 0 && plan.Length / rowWidth <= GridModel.Rows) return rowWidth;
            if (plan.Length % RunProgress.StandardRowWidth == 0 && plan.Length / RunProgress.StandardRowWidth <= GridModel.Rows)
                return RunProgress.StandardRowWidth;
            return plan.Length % RunProgress.WideRowWidth == 0 && plan.Length / RunProgress.WideRowWidth <= GridModel.Rows ? RunProgress.WideRowWidth : 0;
        }

        private void SpawnRow(int row, System.Collections.Generic.List<EnemyColor> colors, int rowWidth)
        {
            bool eligibleOrange = colors.Remove(EnemyColor.Orange) && grid.Fleet.ColorCount(EnemyColor.Orange) == 0;
            bool orange = false;
            // One trial per new slot, stopping once the unique Orange is selected.
            for (int slot = 0; eligibleOrange && !orange && slot < rowWidth; slot++)
                orange = Random.Range(0, OrangeSpawnOdds) == 0;
            if (colors.Count == 0)
            {
                if (orange) Spawn(EnemyColor.Orange, Random.Range(0, rowWidth), row);
                return;
            }
            var plan = orange ? PlanOrangeRow(colors, rowWidth) : PlanRow(colors, rowWidth);
            SpawnPlannedRow(plan, row);
        }

        private bool CanSpawn(EnemyColor color, int column, int row) =>
            (Fleet.SpawnPalette == null || Fleet.SpawnPalette.Contains(color)) &&
            (color != EnemyColor.Orange || grid.Fleet.ColorCount(EnemyColor.Orange) == 0 && !grid.Model.HasOrangeNeighbor(column, row));

        private void SpawnPlannedRow(EnemyColor[] plan, int row)
        {
            for (int column = 0; column < plan.Length; column++)
            {
                if (!CanSpawn(plan[column], column, row))
                {
                    // Extend a neighboring color run when a planned Orange conflicts vertically.
                    if (column > 0 && plan[column - 1] != EnemyColor.Orange) plan[column] = plan[column - 1];
                    else if (column + 1 < plan.Length && plan[column + 1] != EnemyColor.Orange) plan[column] = plan[column + 1];
                    else continue; // Orange-only overrides intentionally leave gaps.
                }
                Spawn(plan[column], column, row);
            }
        }

        private GridEnemy Spawn(EnemyColor color, int column, int row, EnemyColor? effectColor = null, bool warp = false)
        {
                if (!CanSpawn(color, column, row)) return null;
                // Configure while inactive and outside the grid, before OnEnable can register it.
                var instance = Instantiate(Prefab(color));
                instance.SetActive(false);
                var enemy = instance.GetComponent<GridEnemy>();
                if (enemy == null) enemy = instance.AddComponent<GridEnemy>();
                enemy.Configure(color, column, row);
                instance.name = color + " Enemy";
                instance.transform.SetParent(transform, false);
                instance.transform.localPosition = grid.CellPosition(column, row);
                instance.SetActive(true);
                grid.Register(enemy);
                instance.GetComponent<EnemyAbilities>()?.BeginSpawnEffect(effectColor, warp);
                return enemy;
        }
    }
}
