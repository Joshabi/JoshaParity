using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Stores the results of analysis with swingData and corrosponding difficulty
    /// </summary>
    public struct DiffAnalysis
    {
        public BeatmapDifficultyRank difficultyRank;
        private List<SwingData> swingData;
        public BPMHandler bpmHandler;
        public string mapFormat;

        /// <summary>
        /// Constructor when data is already computed
        /// </summary>
        /// <param name="difficultyRank">Diff Rank of the difficulty this data is for</param>
        /// <param name="swingData">Swing data for the difficulty</param>
        /// <param name="bpmHandler">BPM Handler for the difficulty</param>
        /// <param name="mapFormat">Format Version of difficulty</param>
        public DiffAnalysis(BeatmapDifficultyRank difficultyRank, List<SwingData> swingData, BPMHandler bpmHandler, string mapFormat) : this()
        {
            this.difficultyRank = difficultyRank;
            this.swingData = swingData;
            this.bpmHandler = bpmHandler;
            this.mapFormat = mapFormat;
        }

        /// <summary>
        /// Constructor when data needs loading, providing map content via string
        /// </summary>
        /// <param name="mapInfoContents"></param>
        /// <param name="difficultyDatContents"></param>
        public DiffAnalysis(string mapInfoContents, string difficultyDatContents)
        {
            // Load map info
            // Load diff info
            // Run map
            // Set variables
        }

        /// <summary>
        /// Returns a list of predicted SwingData on how a map is played
        /// </summary>
        /// <returns></returns>
        public List<SwingData> GetSwingData() {
            return swingData;
        }

        /// <summary>
        /// Returns the amount of predicted resets depending on type (Reset or Bomb Reset)
        /// </summary>
        /// <param name="type">Type of reset you want the count of</param>
        /// <returns></returns>
        public int GetResetCount(ResetType type = ResetType.Rebound) {
            return swingData.Count <= 1 ? 0 : swingData.Count(x => x.resetType == type);
        }

        /// <summary>
        /// Returns the Swings-per-second
        /// </summary>
        /// <returns></returns>
        public float GetSPS()
        {
            List<SwingData> leftHand = swingData.FindAll(x => !x.rightHand);
            List<SwingData> rightHand = swingData.FindAll(x => x.rightHand);

            float leftSPS = 0;
            float rightSPS = 0;

            if (leftHand.Count != 0) {
                leftSPS = leftHand.Count / TimeUtils.BeatToSeconds(bpmHandler.BPM,
                    (leftHand.Last().swingEndBeat - leftHand.First().swingStartBeat));
            }

            if (rightHand.Count != 0) {
                rightSPS = rightHand.Count / TimeUtils.BeatToSeconds(bpmHandler.BPM,
                    (rightHand.Last().swingEndBeat - rightHand.First().swingStartBeat));
            }

            return leftSPS + rightSPS;
        }

        /// <summary>
        /// Returns the average swing EBPM
        /// </summary>
        /// <returns></returns>
        public float GetAverageEBPM() {
            if (swingData.Count > 0)
            {
                swingData.RemoveAll(x => x.notes == null);
                return swingData.Average(x => x.swingEBPM);
            }
            return 0;
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand in the form of a Vector where X = right, Y = left
        /// </summary>
        /// <returns></returns>
        public Vector2 GetHandedness()
        {
            List<SwingData> leftHand = swingData.FindAll(x => !x.rightHand);
            List<SwingData> rightHand = swingData.FindAll(x => x.rightHand);

            float leftPercent = (float)leftHand.Count / (float)swingData.Count * 100;
            float rightPercent = (float)rightHand.Count / (float)swingData.Count * 100;

            return new Vector2(rightPercent, leftPercent);
        }
    }

    /// <summary>
    /// Analysis object for predicting how a map is played and reporting useful statistics and SwingData
    /// </summary>
    public class MapAnalyser
    {
        private readonly Dictionary<string, List<DiffAnalysis>> _difficultySwingData = new Dictionary<string, List<DiffAnalysis>>();

        public MapStructure MapInfo { get; }
        public Dictionary<string, List<DiffAnalysis>> DiffSwingData => _difficultySwingData;

        public MapAnalyser(string mapPath, bool runAllDiffs = true, IParityMethod? parityMethod = null)
        {
            parityMethod ??= new GenericParityCheck();

            MapInfo = MapLoader.LoadMap(mapPath);
            if (runAllDiffs) RunAllDifficulties(parityMethod);
        }
    
        /// <summary>
        /// Runs through every difficulty listed in info.dat and calculates Swing Data
        /// </summary>
        /// <param name="parityMethod"></param>
        private void RunAllDifficulties(IParityMethod? parityMethod = null)
        {
            foreach (MapDifficultyStructure characteristic in MapInfo._difficultyBeatmapSets)
            {
                // If standard characteristic
                string characteristicName = characteristic._beatmapCharacteristicName.ToLower();

                // Load each difficulty, calculate swing data
                foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                {
                    MapData diffData = MapLoader.LoadDifficultyData(MapInfo._mapFolder, difficulty, MapInfo);

                    // Create BPM Handler and Generate Swing Data for this Difficulty
                    BPMHandler bpmHandler = BPMHandler.CreateBPMHandler(MapInfo._beatsPerMinute, diffData.DifficultyData.bpmEvents.ToList(), MapInfo._songTimeOffset);
                    List<SwingData> predictedSwings = SwingDataGeneration.Run(diffData, bpmHandler, parityMethod);

                    // If Characteristic doesn't exist, need to initialize
                    if (!_difficultySwingData.ContainsKey(characteristicName))
                    {
                        _difficultySwingData.Add(characteristicName, new List<DiffAnalysis>());
                    }

                    _difficultySwingData[characteristicName].Add(new DiffAnalysis(difficulty._difficultyRank, predictedSwings, bpmHandler, diffData.DifficultyData.version));
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
                    returnString += "\nMap Format: " + diffAnalysis.mapFormat;
                    returnString += "\nTotal Official BPM Changes Detected: " + diffAnalysis.bpmHandler.TotalBPMChanges;
                    returnString += "\nPotential Bomb Reset Count: " + diffAnalysis.GetResetCount(ResetType.Bomb);
                    returnString += "\nPotential Reset Count: " + diffAnalysis.GetResetCount(ResetType.Rebound);
                    returnString += "\nAverage Swings Per Second: " + diffAnalysis.GetSPS();
                    returnString += "\nAverage Swing EBPM: " + diffAnalysis.GetAverageEBPM();
                    Vector2 handedness = diffAnalysis.GetHandedness();
                    returnString += "\nRighthand Swings %: " + handedness.X + " Lefthand Swings %: " + handedness.Y;
                }
            }

            returnString += $"\n{formatString}";
            return returnString;
        }

        /// <summary>
        /// Returns all difficulty analysis objects
        /// </summary>
        /// <returns></returns>
        public List<DiffAnalysis> GetAllDiffAnalysis()
        {
            List<DiffAnalysis> result = new List<DiffAnalysis>();
            foreach (KeyValuePair<string, List<DiffAnalysis>> characteristicData in _difficultySwingData)
            {
                // For every difficulty in the characteristic
                foreach (DiffAnalysis diffAnalysis in characteristicData.Value)
                {
                    result.Add(diffAnalysis);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a difficulty analysis object with some information about a diff
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <returns></returns>
        public DiffAnalysis GetDiffAnalysis(BeatmapDifficultyRank difficultyID, string characteristic = "standard")
        {
            if (!_difficultySwingData.ContainsKey(characteristic)) return new();

            List<DiffAnalysis> diffAnalysis = _difficultySwingData[characteristic];
            foreach (DiffAnalysis analysis in diffAnalysis)
            {
                if (analysis.difficultyRank == difficultyID)
                {
                    return analysis;
                }
            }
            return new();
        }
    }
}
