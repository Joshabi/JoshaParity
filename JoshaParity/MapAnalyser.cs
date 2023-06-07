using System.Collections.Generic;
using System.Linq;

namespace JoshaParity
{
    /// <summary>
    /// Stores the results of analysis with swingData and corrosponding difficulty
    /// </summary>
    public struct DiffAnalysis
    {
        public BeatmapDifficultyRank difficultyRank;
        public List<SwingData> swingData;

        public DiffAnalysis(BeatmapDifficultyRank difficultyRank, List<SwingData> swingData) : this()
        {
            this.difficultyRank = difficultyRank;
            this.swingData = swingData;
        }
    }

    /// <summary>
    /// Analysis object for predicting how a map is played and reporting useful statistics and SwingData
    /// </summary>
    public class MapAnalyser
    {
        private readonly Dictionary<string, List<DiffAnalysis>> _difficultySwingData = new();

        public MapStructure MapInfo { get; }
        public Dictionary<string, List<DiffAnalysis>> DiffSwingData => _difficultySwingData;

        public MapAnalyser(string mapPath, IParityMethod? parityMethod = null)
        {
            parityMethod ??= new GenericParityCheck();

            MapInfo = MapLoader.LoadMap(mapPath);
            foreach (MapDifficultyStructure characteristic in MapInfo._difficultyBeatmapSets)
            {
                // If standard characteristic
                string characteristicName = characteristic._beatmapCharacteristicName.ToLower();

                // Load each difficulty, calculate swing data
                foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                {
                    MapData diffData = MapLoader.LoadDifficultyData(MapInfo._mapFolder, difficulty, MapInfo);
                    List<SwingData> predictedSwings = SwingDataGeneration.Run(diffData, MapInfo._beatsPerMinute, parityMethod);

                    // If Characteristic doesn't exist, need to initialize
                    if (!_difficultySwingData.ContainsKey(characteristicName)) {
                        _difficultySwingData.Add(characteristicName, new());
                    }

                    _difficultySwingData[characteristicName].Add(new(difficulty._difficultyRank, predictedSwings));
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
                $"\n{formatString}\nMap Name: {MapInfo._songName} {MapInfo._songSubName} by {MapInfo._songAuthorName}" +
                $"\nMapped by: {MapInfo._levelAuthorName}" +
                $"\nBPM of: {MapInfo._beatsPerMinute}";

            // For every characteristic and loaded difficulty:
            foreach (KeyValuePair<string, List<DiffAnalysis>> characteristicData in _difficultySwingData)
            {
                returnString += $"\n{formatString}\nCharacteristic: " + characteristicData.Key.ToString() + $"\n{formatString}";
                
                // For every difficulty in the characteristic
                foreach (DiffAnalysis diffAnalysis in characteristicData.Value)
                {
                    returnString += "\n" + diffAnalysis.difficultyRank.ToString();
                    returnString += "\nPotential Bomb Reset Count: " + GetResetCount(diffAnalysis.difficultyRank, characteristicData.Key, ResetType.Bomb);
                    returnString += "\nPotential Reset Count: " + GetResetCount(diffAnalysis.difficultyRank, characteristicData.Key, ResetType.Rebound);
                    returnString += "\nSwings Per Second: " + GetSPS(diffAnalysis.difficultyRank, characteristicData.Key);
                }
            }

            returnString += $"\n{formatString}";
            return returnString;
        }

        /// <summary>
        /// Returns a list of predicted SwingData on how a map is played
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <returns></returns>
        public List<SwingData> GetSwingData(BeatmapDifficultyRank difficultyID, string characteristic = "standard")
        {
            // Attempt to load the characteristic
            if (!_difficultySwingData.ContainsKey(characteristic)) return new();

            List<DiffAnalysis> diffAnalysis = _difficultySwingData[characteristic];
            foreach (DiffAnalysis analysis in diffAnalysis)
            {
                if (analysis.difficultyRank == difficultyID)
                {
                    return analysis.swingData;
                }
            }
            return new();
        }

        /// <summary>
        /// Returns the amount of predicted resets in a given map depending on type (Reset or Bomb Reset)
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <param name="type">Type of reset you want the count of</param>
        /// <returns></returns>
        public int GetResetCount(BeatmapDifficultyRank difficultyID, string characteristic = "standard", ResetType type = ResetType.Rebound)
        {
            List<SwingData> swingData = GetSwingData(difficultyID, characteristic);
            return swingData.Count <= 1 ? 0 : swingData.Count(x => x.resetType == type);
        }

        /// <summary>
        /// Returns the Swings-per-second of a given map difficulty
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <returns></returns>
        public float GetSPS(BeatmapDifficultyRank difficultyID, string characteristic = "standard")
        {
            List<SwingData> swingData = GetSwingData(difficultyID, characteristic);
            List<SwingData> leftHand = swingData.FindAll(x => !x.rightHand);
            List<SwingData> rightHand = swingData.FindAll(x => x.rightHand);

            if (leftHand.Count == 0) return -1;
            if (rightHand.Count == 0) return -1;

            float leftSPS = leftHand.Count / SwingUtility.BeatToSeconds(MapInfo._beatsPerMinute, 
                (leftHand.Last().swingEndBeat - leftHand.First().swingStartBeat));

            float rightSPS = rightHand.Count / SwingUtility.BeatToSeconds(MapInfo._beatsPerMinute,
                (rightHand.Last().swingEndBeat - rightHand.First().swingStartBeat));

            return leftSPS + rightSPS;
        }
    }
}
