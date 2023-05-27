using JoshaUtils;

Console.WriteLine("<< Joshaparity Check! >>");

const string mapFolder = "./Maps";
const string formatString = "----------------------------------------------";

// Change this to Beatmap DifficultyRank.All if you wish to do every difficulty
const BeatmapDifficultyRank desiredDifficulty = BeatmapDifficultyRank.ExpertPlus;
List<MapStructure> maps = new()
{
    // Map Example: "maps.Add(MapLoader.LoadMap($"{mapFolder}/Radiant"));"
    MapLoader.LoadMap($"{mapFolder}/Additional"),
    MapLoader.LoadMap($"{mapFolder}/Diastrophism"),
    MapLoader.LoadMap($"{mapFolder}/Blood Moon"),
    MapLoader.LoadMap($"{mapFolder}/BS Recall"),
    MapLoader.LoadMap($"{mapFolder}/Compute"),
    MapLoader.LoadMap($"{mapFolder}/Howl")
};

// Go through every map
foreach (MapStructure map in maps)
{
    int totalDifficulties = map._difficultyBeatmapSets.Sum(t => t._difficultyBeatmaps.Length);

    if (totalDifficulties == 0) continue;

    Console.WriteLine($"\n{formatString}\nMap Name: {map._songName} {map._songSubName} by {map._songAuthorName}" +
            $"\nMapped by: {map._levelAuthorName}" +
            $"\nBPM of: {map._beatsPerMinute}" +
            $"\nTotal of {totalDifficulties} difficulties.\n{formatString}\n");

    foreach (MapDifficultyStructure characteristic in map._difficultyBeatmapSets)
    {
        // If not a lightshow, 360 degree or 90 degree characteristic
        string characteristicName = characteristic._beatmapCharacteristicName.ToLower();
        if (!characteristicName.Equals("lightshow") && !characteristicName.Equals("360degree") && !characteristicName.Equals("90degree"))
        {
            foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
            {
                if (desiredDifficulty == BeatmapDifficultyRank.All || difficulty._difficultyRank == desiredDifficulty)
                {
                    Console.WriteLine("Difficulty: " + difficulty._difficulty);
                    MapData diffData = MapLoader.LoadDifficultyData(map._mapFolder, difficulty, map);
                    SwingDataGeneration.Run(diffData, map._beatsPerMinute);
                }
            }
        }
    }
}