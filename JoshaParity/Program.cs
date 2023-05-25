using JoshaUtils;

Console.WriteLine("<< Joshaparity Check! >>");

string mapFolder = "./Maps";
string formatString = "----------------------------------------------";

// Change this to BeatmapDifficultyRank.All if you wish to do every difficulty
BeatmapDifficultyRank _desiredDifficulty = BeatmapDifficultyRank.ExpertPlus;
List<MapStructure> maps = new();

// Map Example: "maps.Add(MapLoader.LoadMap($"{mapFolder}/Radiant"));"
maps.Add(MapLoader.LoadMap($"{mapFolder}/Additional"));
maps.Add(MapLoader.LoadMap($"{mapFolder}/Diastrophism"));

// Go through every map
foreach (MapStructure map in maps)
{
    int totalDifficulties = 0;
    for (int i = 0; i < map._difficultyBeatmapSets.Length; i++)
    {
        totalDifficulties += map._difficultyBeatmapSets[i]._difficultyBeatmaps.Length;
    }

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
                if (difficulty._difficultyRank == _desiredDifficulty || _desiredDifficulty == BeatmapDifficultyRank.All)
                {
                    Console.WriteLine("Difficulty: " + difficulty._difficulty);
                    MapData diffData = MapLoader.LoadDifficultyData(map._mapFolder, difficulty, map);
                    SwingDataGeneration.Run(diffData, map._beatsPerMinute);
                }
            }
        }
    }
}