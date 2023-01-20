using JoshaUtils;

Console.WriteLine("<< Joshaparity Check! >>");

List<Beatmap?> maps = new();
string mapFolder = "./Maps";
maps.Add(BeatmapLoader.LoadMap($"{mapFolder}/Voracity/"));

foreach(Beatmap? map in maps)
{
    if (map != null)
    {
        Console.WriteLine($"Map Name: {map._songName} {map._songSubName}by {map._songAuthorName}" +
            $"\nMapped by: {map._levelAuthorName}" +
            $"\nBPM of: {map._beatsPerMinute}" +
            $"\nTotal of {map.Difficulties.Count} difficulties.");

        foreach (KeyValuePair<string, MapDifficulty> diff in map.Difficulties)
        {
            if (!diff.Key.Contains("Standard")) { continue; }
            Console.WriteLine("Difficulty: " + diff.Key);
            ParityChecker.Run(diff.Value, map._beatsPerMinute);
        }
    }
}