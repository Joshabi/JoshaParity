using JoshaUtils;

Console.WriteLine("<< Josha Parity Check! >>");

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
    }
}