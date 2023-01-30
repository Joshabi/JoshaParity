using JoshaUtils;
Console.WriteLine("<< Joshaparity Check! >>");

// New list of maps to check
List<Beatmap?> maps = new();
string mapFolder = "./Maps";

// Loads a new map to check:
// "Example: maps.Add(BeatmapLoader.LoadMap($"{mapFolder}/Voracity/"));
maps.Add(BeatmapLoader.LoadMap($"{mapFolder}/Diastrophism/"));

// Go through every map
foreach(Beatmap? map in maps)
{ 
    if (map != null)
    {
        Console.WriteLine($"Map Name: {map._songName} {map._songSubName}by {map._songAuthorName}" +
            $"\nMapped by: {map._levelAuthorName}" +
            $"\nBPM of: {map._beatsPerMinute}" +
            $"\nTotal of {map.Difficulties.Count} difficulties.");


        // For each difficulty, if standard game mode.
        // NOTE: Comment Line 27 (diff.key.contains()) to go through lawless too
        foreach (KeyValuePair<string, MapDifficulty> diff in map.Difficulties)
        {
            // Skip Lightshow Difficulties
            if (diff.Value._notes.Count() == 0) { continue; }
            if (!diff.Key.Contains("Standard")) { continue; }
            Console.WriteLine("Difficulty: " + diff.Key);
            ParityChecker.Run(diff.Value, map._beatsPerMinute);
        }
    }
}