using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CandyCruisers
{
    // The JSON file is the authoring source; this is its in-memory representation.
    public sealed class LevelCatalog
    {
        public const string FilePath = "Assets/Resources/LevelMap.json";
        public LevelDefinition[] levels = Array.Empty<LevelDefinition>();
        public LevelDefinition ForLevel(int number) => number < 1 || levels == null || levels.Length == 0 ? null :
            levels[Math.Min(number, levels.Length) - 1];

        public List<string> ValidationErrors()
        {
            var errors = new List<string>();
            if (levels == null || levels.Length == 0) errors.Add("The level map needs level 1.");
            else for (int index = 0; index < levels.Length; index++)
            {
                if (levels[index] == null) { errors.Add("Assign settings to Level " + (index + 1) + "."); continue; }
                foreach (string error in levels[index].ValidationErrors()) errors.Add("Level " + (index + 1) + ": " + error);
            }
            return errors;
        }

        public static LevelCatalog Load(TextAsset asset)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return Parse(File.ReadAllText(path));
#endif
            return Parse(asset.text);
        }

        public static LevelCatalog Parse(string json)
        {
            try
            {
                var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Fields(root, "schemaVersion", "levels");
                if (Integer(root, "schemaVersion") != 3) throw new FormatException("schemaVersion must be 3; levels contain fleet objects with possibleColors, initialFleet and lineSpawns.");
                if (!(root["levels"] is JObject entries) || entries.Count == 0) throw new FormatException("levels must be an object containing level 1.");
                var catalog = new LevelCatalog { levels = new LevelDefinition[entries.Count] };
                foreach (var entry in entries.Properties())
                {
                    if (!int.TryParse(entry.Name, out int number) || number < 1 || number > entries.Count || entry.Name != number.ToString())
                        throw new FormatException("Level keys must be consecutive numbers starting at 1.");
                    if (!(entry.Value is JObject data)) throw new FormatException("Level " + number + " must be an object.");
                    Fields(data, "backgroundImage", "possibleSoundtrackTracks", "fleets");
                    var level = new LevelDefinition {
                        possibleSoundtrackTracks = Strings(data, "possibleSoundtrackTracks"),
                        fleets = Fleets(data)
                    };
                    if (data.Property("backgroundImage") == null) throw new FormatException(data.Path + ".backgroundImage is required.");
                    var background = data["backgroundImage"];
                    if (background != null && background.Type != JTokenType.String && background.Type != JTokenType.Null)
                        throw new FormatException("backgroundImage must be a resource name or an empty string.");
                    string image = background?.Value<string>();
                    if (!string.IsNullOrEmpty(image))
                    {
                        level.backgroundImage = Resources.Load<Sprite>(image);
                        if (level.backgroundImage == null) throw new FormatException("Level " + number + ": background sprite '" + image + "' was not found under Resources.");
                    }
                    catalog.levels[number - 1] = level;
                }
                var errors = catalog.ValidationErrors();
                if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
                return catalog;
            }
            catch (Exception error) when (error is JsonException || error is FormatException || error is OverflowException)
            { throw new FormatException("LevelMap.json: " + error.Message, error); }
        }

        private static void Fields(JObject data, params string[] allowed)
        {
            foreach (var property in data.Properties())
                if (Array.IndexOf(allowed, property.Name) < 0) throw new FormatException("Unknown setting '" + property.Path + "'.");
        }
        private static int Integer(JObject data, string name)
        {
            if (data[name]?.Type != JTokenType.Integer) throw new FormatException(name + " must be an integer.");
            return data[name].Value<int>();
        }
        private static string[] Strings(JObject data, string name)
        {
            if (!(data[name] is JArray array)) throw new FormatException(data.Path + "." + name + " must be a list of strings.");
            var result = new string[array.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (array[i].Type != JTokenType.String) throw new FormatException(data.Path + "." + name + "[" + i + "] must be a string.");
                result[i] = array[i].Value<string>();
            }
            return result;
        }
        private static FleetDefinition[] Fleets(JObject data)
        {
            string name = data.Path + ".fleets";
            if (!(data["fleets"] is JArray fleets)) throw new FormatException(name + " must be a list of fleet objects.");
            var result = new FleetDefinition[fleets.Count];
            for (int fleet = 0; fleet < fleets.Count; fleet++)
            {
                string label = name + "[" + fleet + "]";
                if (!(fleets[fleet] is JObject entry)) throw new FormatException(label + " must be a fleet object.");
                Fields(entry, "possibleColors", "requiredColors", "initialFleet", "lineSpawns");
                result[fleet] = new FleetDefinition
                {
                    possibleColors = Colors(entry, "possibleColors"),
                    requiredColors = entry.Property("requiredColors") == null ? Array.Empty<EnemyColor>() : Colors(entry, "requiredColors"),
                    initialFleet = Strings(entry, "initialFleet"),
                    lineSpawns = Strings(entry, "lineSpawns")
                };
            }
            return result;
        }
        private static EnemyColor[] Colors(JObject data, string name)
        {
            var names = Strings(data, name); var result = new EnemyColor[names.Length];
            for (int i = 0; i < names.Length; i++)
                if (!Enum.TryParse(names[i], true, out result[i]) || !Enum.IsDefined(typeof(EnemyColor), result[i]) ||
                    !string.Equals(names[i], result[i].ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new FormatException(name + " contains unknown color '" + names[i] + "'.");
            return result;
        }
    }
}
