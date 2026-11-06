using System;
using System.Collections.Generic;

namespace CandyCruisers
{
    // Authoring configuration for one fleet. FleetPlan captures its resolved opening
    // colors once; all incoming lines and summons must stay within that palette.
    public sealed class FleetDefinition
    {
        public EnemyColor[] possibleColors = { EnemyColor.Red, EnemyColor.Blue };
        public EnemyColor[] requiredColors = Array.Empty<EnemyColor>();
        public string[] initialFleet = { "11221", "22112" };
        public string[] lineSpawns = { "11221", "22112" };

        public List<string> ValidationErrors()
        {
            var errors = new List<string>();
            var possible = LevelDefinition.UniqueColors(possibleColors);
            var required = LevelDefinition.UniqueColors(requiredColors);
            if (possibleColors == null || possible.Count == 0 || possible.Count != possibleColors.Length)
                errors.Add("possibleColors must contain at least one valid color and no duplicates.");
            if (requiredColors == null || required.Count != requiredColors.Length)
                errors.Add("requiredColors must contain valid, unique colors.");
            foreach (var color in required)
                if (!possible.Contains(color)) errors.Add(color + " is required but is missing from possibleColors.");
            if (initialFleet == null || initialFleet.Length == 0)
                errors.Add("initialFleet must be a nonempty list of row strings.");
            else if (initialFleet.Length > GridModel.Rows) errors.Add("initialFleet has an invalid row count.");
            int width = 0;
            int occupied = 0;
            int orange = 0;
            ValidateRows(initialFleet, "initialFleet", possible, errors, ref width, ref occupied, ref orange);
            if (occupied == 0) errors.Add("initialFleet must contain at least one enemy.");
            if (orange > 1) errors.Add("initialFleet cannot spawn more than one Orange.");
            int ignoredOccupied = 0, ignoredOrange = 0;
            ValidateRows(lineSpawns, "lineSpawns", possible, errors, ref width, ref ignoredOccupied, ref ignoredOrange);
            if (errors.Count == 0 && SpawnScript.Plan(this, false) == null)
                errors.Add("initialFleet and lineSpawns cannot satisfy numbered groups, required colors, the initial fleet palette and one-Orange limit; incoming lines cannot introduce colors absent from initialFleet.");
            return errors;
        }

        private static void ValidateRows(string[] rows, string name, List<EnemyColor> possible,
            List<string> errors, ref int width, ref int occupied, ref int totalOrange)
        {
            if (rows == null) { errors.Add(name + " must be a list of row strings."); return; }
            for (int index = 0; index < rows.Length; index++)
            {
                string label = name + "[" + index + "]";
                string row = rows[index];
                if (row == null) { errors.Add(label + " must be a row string."); continue; }
                if (row.Contains("/")) { errors.Add(label + " must contain one row; put each row in its own string instead of using '/'."); continue; }
                if (row.Length < 1 || row.Length > GridModel.Columns) errors.Add(label + " rows must have 1–6 positions.");
                if (width == 0) width = row.Length;
                if (width != row.Length) errors.Add(label + " must use the same row width as the rest of this fleet.");
                int orange = 0;
                foreach (char raw in row)
                {
                    char symbol = char.ToLowerInvariant(raw);
                    if (symbol == '0') continue;
                    occupied++;
                    if (symbol == '*' || symbol >= '1' && symbol <= '6') continue;
                    if (!SpawnScript.TryColor(symbol, out var color)) errors.Add(label + " contains unknown character '" + raw + "'.");
                    else
                    {
                        if (!possible.Contains(color)) errors.Add(label + " uses " + color + " outside possibleColors.");
                        if (color == EnemyColor.Orange) orange++;
                    }
                }
                totalOrange += orange;
                if (orange > 1) errors.Add(label + " cannot spawn more than one Orange.");
            }
        }
    }
}
