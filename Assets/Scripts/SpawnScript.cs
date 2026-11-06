using System;
using System.Collections.Generic;

namespace CandyCruisers
{
    public static class SpawnScript
    {
        public static bool TryColor(char symbol, out EnemyColor color)
        {
            switch (char.ToLowerInvariant(symbol))
            {
                case 'r': color = EnemyColor.Red; return true;
                case 'b': color = EnemyColor.Blue; return true;
                case 'g': color = EnemyColor.Green; return true;
                case 'p': color = EnemyColor.Purple; return true;
                case 'y': color = EnemyColor.Yellow; return true;
                case 'o': color = EnemyColor.Orange; return true;
                default: color = default; return false;
            }
        }

        // The small, bounded search guarantees valid assignments without random retry failures.
        public static FleetPlan Plan(FleetDefinition definition, bool randomize = true)
        {
            if (definition == null || definition.initialFleet == null || definition.initialFleet.Length == 0 ||
                definition.lineSpawns == null || definition.requiredColors == null) return null;
            string pattern = string.Join("/", definition.initialFleet);
            var mandatory = new HashSet<EnemyColor>(definition.requiredColors);
            var digits = new List<char>();
            var scripts = new List<string> { pattern }; scripts.AddRange(definition.lineSpawns);
            foreach (string script in scripts)
            foreach (char raw in script)
            {
                char symbol = char.ToLowerInvariant(raw);
                if (TryColor(symbol, out var color)) mandatory.Add(color);
                else if (symbol >= '1' && symbol <= '6' && !digits.Contains(symbol)) digits.Add(symbol);
            }
            var pool = LevelDefinition.UniqueColors(definition.possibleColors);
            if (pool.Count == 0 || digits.Count > pool.Count) return null;
            foreach (var color in mandatory) if (!pool.Contains(color)) return null;
            if (randomize) Shuffle(pool);
            var bindings = new Dictionary<char, EnemyColor>();
            var used = new HashSet<EnemyColor>();
            FleetPlan result = null;
            bool Bind(int index)
            {
                if (index == digits.Count)
                {
                    var cells = DecodeFixed(pattern, bindings, out var wildcards);
                    var needed = new HashSet<EnemyColor>(mandatory);
                    foreach (var color in bindings.Values) needed.Add(color);
                    if (!FillWildcards(cells, wildcards, pool, needed, randomize, false)) return false;
                    var actual = new HashSet<EnemyColor>(); int orange = 0;
                    foreach (var cell in cells) if (cell.HasValue)
                    { actual.Add(cell.Value); if (cell == EnemyColor.Orange) orange++; }
                    if (actual.Count == 0 || orange > 1) return false;
                    foreach (var required in definition.requiredColors) if (!actual.Contains(required)) return false;
                    foreach (string line in definition.lineSpawns)
                    {
                        int lineOrange = 0;
                        // Incoming wildcards roll when that line spawns; only fixed cells constrain the opening.
                        foreach (var cell in DecodeFixed(line, bindings, out _))
                        {
                            if (cell.HasValue && !actual.Contains(cell.Value)) return false;
                            if (cell == EnemyColor.Orange) lineOrange++;
                        }
                        if (lineOrange > 1) return false;
                    }
                    result = new FleetPlan(cells, definition.initialFleet[0].Length, bindings, definition.lineSpawns);
                    return true;
                }
                foreach (var color in pool)
                {
                    if (!used.Add(color)) continue;
                    bindings[digits[index]] = color;
                    if (Bind(index + 1)) return true;
                    used.Remove(color); bindings.Remove(digits[index]);
                }
                return false;
            }
            Bind(0);
            return result;
        }

        public static EnemyColor?[] Decode(string pattern, IReadOnlyDictionary<char, EnemyColor> bindings,
            IReadOnlyList<EnemyColor> wildcardColors = null, bool randomize = true)
        {
            var cells = DecodeFixed(pattern, bindings, out var wildcards);
            if (wildcards.Count == 0) return cells;
            if (wildcardColors == null) throw new FormatException("Wildcard spawn characters need a color palette.");
            if (!FillWildcards(cells, wildcards, new List<EnemyColor>(wildcardColors), Array.Empty<EnemyColor>(), randomize, true))
                throw new FormatException("Cannot resolve wildcard spawn colors within the one-Orange limit.");
            return cells;
        }

        private static EnemyColor?[] DecodeFixed(string pattern, IReadOnlyDictionary<char, EnemyColor> bindings,
            out List<int> wildcards)
        {
            var result = new List<EnemyColor?>();
            wildcards = new List<int>();
            foreach (char raw in pattern)
            {
                char symbol = char.ToLowerInvariant(raw);
                if (symbol == '/') continue;
                if (symbol == '*') { wildcards.Add(result.Count); result.Add(null); }
                else if (symbol == '0') result.Add(null);
                else if (TryColor(symbol, out var color)) result.Add(color);
                else if (bindings.TryGetValue(symbol, out color)) result.Add(color);
                else throw new FormatException("Unassigned spawn character '" + raw + "'.");
            }
            return result.ToArray();
        }

        private static bool FillWildcards(EnemyColor?[] cells, List<int> positions, List<EnemyColor> pool,
            IEnumerable<EnemyColor> required, bool randomize, bool allowBlanks)
        {
            var present = new HashSet<EnemyColor>();
            int orange = 0;
            foreach (var cell in cells) if (cell.HasValue)
            { present.Add(cell.Value); if (cell == EnemyColor.Orange) orange++; }
            if (orange > 1) return false;
            var missing = new List<EnemyColor>();
            foreach (var color in required)
                if (!present.Contains(color) && !missing.Contains(color))
                {
                    if (!pool.Contains(color)) return false;
                    missing.Add(color);
                }
            if (missing.Count > positions.Count) return false;
            if (randomize) Shuffle(positions);
            int index = 0;
            // Reserve required colors before the independent rolls, avoiding random validation failures.
            foreach (var color in missing)
            {
                cells[positions[index++]] = color;
                if (color == EnemyColor.Orange) orange++;
            }
            var available = new List<EnemyColor>(pool);
            if (orange > 0) available.RemoveAll(color => color == EnemyColor.Orange);
            for (; index < positions.Count; index++)
            {
                if (available.Count == 0) { if (!allowBlanks) return false; continue; }
                var color = available[randomize ? UnityEngine.Random.Range(0, available.Count) : 0];
                cells[positions[index]] = color;
                if (color == EnemyColor.Orange) available.RemoveAll(candidate => candidate == EnemyColor.Orange);
            }
            return true;
        }

        private static void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            { int j = UnityEngine.Random.Range(0, i + 1); var swap = items[i]; items[i] = items[j]; items[j] = swap; }
        }
    }
}
