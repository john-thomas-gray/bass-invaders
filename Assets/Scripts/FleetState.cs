using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CandyCruisers
{
    // Immutable reservation: dimensions, colors and player weights describe the same cells.
    public sealed class FleetPlan
    {
        private readonly EnemyColor?[] cells;
        private readonly List<EnemyColor> colors;
        public IReadOnlyList<EnemyColor> Colors { get; }
        public int Columns { get; }
        public int Rows => cells.Length / Columns;
        public int CellCount => cells.Count(cell => cell.HasValue);
        // The opening palette is a fleet invariant, never the broader authoring pool.
        public IReadOnlyList<EnemyColor> SpawnPalette => Colors;
        public IReadOnlyDictionary<char, EnemyColor> Bindings { get; }
        public IReadOnlyList<string> LineSpawns { get; }
        public bool IsScripted { get; }
        public FleetPlan(EnemyColor[] cells, int columns) : this(cells == null ? null :
            Array.ConvertAll(cells, cell => (EnemyColor?)cell), columns) { }
        public FleetPlan(EnemyColor?[] cells, int columns,
            IDictionary<char, EnemyColor> bindings = null, string[] lineSpawns = null)
        {
            if (cells == null || cells.Length == 0 || columns < 1 || columns > GridModel.Columns ||
                cells.Length % columns != 0 || cells.Length / columns > GridModel.Rows)
                throw new ArgumentException("A fleet plan needs complete rows within the grid.");
            this.cells = (EnemyColor?[])cells.Clone();
            Columns = columns;
            colors = LevelDefinition.UniqueColors(OccupiedColors()); colors.Sort();
            if (colors.Count == 0) throw new ArgumentException("A fleet must contain an enemy.");
            Colors = colors.AsReadOnly();
            Bindings = new ReadOnlyDictionary<char, EnemyColor>(bindings == null ? new Dictionary<char, EnemyColor>() :
                new Dictionary<char, EnemyColor>(bindings));
            LineSpawns = Array.AsReadOnly(lineSpawns == null ? Array.Empty<string>() : (string[])lineSpawns.Clone());
            IsScripted = lineSpawns != null;
        }
        public EnemyColor?[] CopyCells() => (EnemyColor?[])cells.Clone();
        public EnemyColor[] OccupiedColors() => cells.Where(cell => cell.HasValue).Select(cell => cell.Value).ToArray();
        public bool Contains(EnemyColor color) => colors.Contains(color);
        public Dictionary<EnemyColor, int> PlayerColorWeights()
        {
            var weights = new Dictionary<EnemyColor, int>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].HasValue) continue;
                var color = cells[i].Value;
                weights.TryGetValue(color, out int previous);
                if (color == EnemyColor.Orange && previous > 0) continue;
                weights[color] = previous + i / Columns + 1;
            }
            return weights;
        }
    }

    // GridModel owns live enemies. This facade adds fleet lifetime and reward state;
    // it never maintains a second copy of live enemy counts.
    public sealed class FleetState
    {
        private readonly GridModel model;
        private readonly List<EnemyColor> colors = new List<EnemyColor>();
        private readonly HashSet<EnemyColor> cleared = new HashSet<EnemyColor>();
        private IReadOnlyList<EnemyColor> spawnPalette;
        public IReadOnlyList<EnemyColor> Colors { get; }
        public IReadOnlyList<EnemyColor> SpawnPalette => spawnPalette;
        public FleetPlan ActivePlan { get; private set; }
        public FleetPlan PendingPlan { get; private set; }
        public int Count => model.Count;
        public int ColorCount(EnemyColor color) => model.ColorCount(color);
        public List<EnemyColor> PresentColors() => model.AvailableColors();
        public List<EnemyColor> CopyColors() => new List<EnemyColor>(colors);
        public List<EnemyColor> BarColors() => colors.Count > 0 ? CopyColors() :
            PendingPlan != null ? new List<EnemyColor>(PendingPlan.Colors) : new List<EnemyColor>();
        public bool IsCleared(EnemyColor color) => cleared.Contains(color);
        public int ClearedColorCount => cleared.Count;
        public bool AllColorsCleared => colors.Count > 0 && colors.TrueForAll(color => IsCleared(color) && ColorCount(color) == 0);
        internal FleetState(GridModel model) { this.model = model; Colors = colors.AsReadOnly(); }
        public void Reserve(FleetPlan plan) => PendingPlan = plan;
        internal void Begin(FleetPlan plan, bool restrictSpawns)
        {
            ResetCycle();
            ActivePlan = plan;
            PendingPlan = null;
            spawnPalette = restrictSpawns ? plan.SpawnPalette : null;
            // Actual registration fills Colors, including sparse development fleets.
        }
        internal void Observe(EnemyColor color)
        {
            if (!colors.Contains(color)) { colors.Add(color); colors.Sort(); }
            cleared.Remove(color);
        }
        internal void MarkCleared(EnemyColor color)
        {
            if (ColorCount(color) == 0 && colors.Contains(color)) cleared.Add(color);
        }
        public void ResetClearStreak() => cleared.Clear();
        public void ResetCycle()
        {
            colors.Clear(); cleared.Clear(); spawnPalette = null;
            ActivePlan = PendingPlan = null;
            // Useful for preview tools that reset history around an existing formation.
            foreach (var color in model.AvailableColors()) Observe(color);
        }
    }
}
