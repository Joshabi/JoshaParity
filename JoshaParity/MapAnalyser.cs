namespace JoshaParity
{
    /// <summary>
    /// Analysis object for predicting how a map is played and potential parity breaks
    /// </summary>
    public class MapAnalyser
    {
        private readonly MapStructure _mapInfo;
        private readonly Dictionary<BeatmapDifficultyRank, List<SwingData>> _difficultySwingData = new();

        public MapAnalyser(string mapPath, IParityMethod? parityMethod = null)
        {
            parityMethod ??= new GenericParityCheck();

            _mapInfo = MapLoader.LoadMap(mapPath);
            foreach (MapDifficultyStructure characteristic in _mapInfo._difficultyBeatmapSets)
            {
                // If standard characteristic
                string characteristicName = characteristic._beatmapCharacteristicName.ToLower();
                if (!characteristicName.Equals("standard")) continue;

                // Load each difficulty, calculate swing data
                foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                {
                    MapData diffData = MapLoader.LoadDifficultyData(_mapInfo._mapFolder, difficulty, _mapInfo);
                    List<SwingData> predictedSwings = SwingDataGeneration.Run(diffData, _mapInfo._beatsPerMinute, parityMethod);
                    _difficultySwingData.Add(difficulty._difficultyRank, predictedSwings);
                }
            }
        }

        /// <summary>
        /// Formatted information about this analysis object
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            const string formatString = "----------------------------------------------";
            string returnString =
                $"\n{formatString}\nMap Name: {_mapInfo._songName} {_mapInfo._songSubName} by {_mapInfo._songAuthorName}" +
                $"\nMapped by: {_mapInfo._levelAuthorName}" +
                $"\nBPM of: {_mapInfo._beatsPerMinute}\n{formatString}";

            // Difficulty Information
            foreach (MapDifficultyStructure characteristic in _mapInfo._difficultyBeatmapSets)
            {
                // If standard characteristic
                string characteristicName = characteristic._beatmapCharacteristicName.ToLower();
                if (!characteristicName.Equals("standard")) continue;

                foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps.Reverse())
                {
                    returnString += "\n" + difficulty._difficulty;
                    returnString += "\nPotential Bomb Reset Count: " + GetResetCount(difficulty._difficultyRank, ResetType.Bomb);
                    returnString += "\nPotential Reset Count: " + GetResetCount(difficulty._difficultyRank, ResetType.Rebound);
                }
            }

            return returnString;
        }

        /// <summary>
        /// Returns a list of predicted SwingData on how a map is played
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <returns></returns>
        public List<SwingData> GetSwingData(BeatmapDifficultyRank difficultyID)
        {
            return _difficultySwingData.TryGetValue(difficultyID, out List<SwingData>? value) ? value : new();
        }

        /// <summary>
        /// Returns the amount of predicted resets in a given map depending on type (Reset or Bomb Reset)
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="type">Type of reset you want the count of</param>
        /// <returns></returns>
        public int GetResetCount(BeatmapDifficultyRank difficultyID, ResetType type = ResetType.Rebound)
        {
            if (_difficultySwingData.TryGetValue(difficultyID, out List<SwingData>? value)) {
                return value.Count(x => x.resetType == type);
            } else {
                return -1;
            }
        }
    }
}
