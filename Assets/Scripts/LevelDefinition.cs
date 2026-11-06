using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Runtime data loaded from LevelMap.json, not a Unity authoring asset.
    public sealed class LevelDefinition
    {
        public Sprite backgroundImage;
        public string[] possibleSoundtrackTracks = Array.Empty<string>();
        public FleetDefinition[] fleets = { new FleetDefinition() };

        public List<string> ValidationErrors()
        {
            var errors = new List<string>();
            var tracks = new HashSet<string>(StringComparer.Ordinal);
            foreach (string track in possibleSoundtrackTracks ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(track) || !tracks.Add(track)) errors.Add("Soundtrack names must be nonempty and unique.");
            if (fleets == null || fleets.Length == 0) errors.Add("fleets needs at least one fleet.");
            else for (int i = 0; i < fleets.Length; i++)
            {
                if (fleets[i] == null) { errors.Add("fleets[" + i + "] must be a fleet object."); continue; }
                foreach (string error in fleets[i].ValidationErrors()) errors.Add("fleets[" + i + "]: " + error);
            }
            return errors;
        }

        public static List<EnemyColor> UniqueColors(EnemyColor[] source)
        {
            var result = new List<EnemyColor>();
            foreach (var color in source ?? Array.Empty<EnemyColor>())
                if (Enum.IsDefined(typeof(EnemyColor), color) && !result.Contains(color)) result.Add(color);
            return result;
        }
    }
}
