using System;
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
        public MapSwingContainer swingContainer;
        public BPMHandler bpmHandler;
        public string mapFormat;

        /// <summary>
        /// Constructor when SwingData is already computed
        /// </summary>
        /// <param name="difficultyRank">Difficulty Rank of the difficulty this data is for</param>
        /// <param name="swingData">Swing data for the difficulty</param>
        /// <param name="bpmHandler">BPM Handler for the difficulty</param>
        /// <param name="mapFormat">Format Version of difficulty</param>
        public DiffAnalysis(BeatmapDifficultyRank difficultyRank, MapSwingContainer container, BPMHandler bpmHandler, string mapFormat) : this()
        {
            this.difficultyRank = difficultyRank;
            swingContainer = container;
            this.bpmHandler = bpmHandler;
            this.mapFormat = mapFormat;
        }

        /// <summary>
        /// Constructor for a single run, where the map info and difficulty data is passed in
        /// </summary>
        /// <param name="mapInfoContents">String contents of info.dat</param>
        /// <param name="difficultyDatContents">String contents of difficulty.dat</param>
        public DiffAnalysis(string mapInfoContents, string difficultyDatContents, BeatmapDifficultyRank diffRank, IParityMethod? parityMethod = null)
        {
            // Load map info
            MapStructure mapInfo = MapLoader.LoadMap(mapInfoContents);
            MapData diffData = MapLoader.LoadDifficultyData(difficultyDatContents);
            IParityMethod ParityMethodology = parityMethod ??= new GenericParityCheck();

            difficultyRank = diffRank;
            bpmHandler = BPMHandler.CreateBPMHandler(mapInfo._beatsPerMinute, diffData.DifficultyData.bpmEvents.ToList(), mapInfo._songTimeOffset);
            swingContainer = SwingDataGeneration.Run(diffData, bpmHandler, ParityMethodology);
            mapFormat = mapInfo._version;
        }

        /// <summary>
        /// Returns a list of predicted SwingData
        /// </summary>
        /// <returns></returns>
        public List<SwingData> GetSwingData() {
            return swingContainer.GetJointSwingData();
        }

        /// <summary>
        /// Returns the amount of predicted resets depending on type (Reset or Bomb Reset)
        /// </summary>
        /// <param name="type">Type of reset you want the count of</param>
        /// <returns></returns>
        public int GetResetCount(ResetType type = ResetType.Rebound) {
            return swingContainer.GetJointSwingData().Count <= 1 ? 0 : swingContainer.GetJointSwingData().Count(x => x.resetType == type);
        }

        /// <summary>
        /// Returns the Swings-per-second
        /// </summary>
        /// <returns></returns>
        public float GetSPS()
        {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

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
            if (swingContainer.GetJointSwingData().Count > 0)
            {
                swingContainer.GetJointSwingData().RemoveAll(x => x.notes == null);
                return swingContainer.GetJointSwingData().Average(x => x.swingEBPM);
            }
            return 0;
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand in the form of a Vector where X = right, Y = left
        /// </summary>
        /// <returns></returns>
        public Vector2 GetHandedness()
        {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

            float leftPercent = (float)leftHand.Count / (float)swingContainer.GetJointSwingData().Count * 100;
            float rightPercent = (float)rightHand.Count / (float)swingContainer.GetJointSwingData().Count * 100;

            return new Vector2(rightPercent, leftPercent);
        }

        /// <summary>
        /// Returns the amount of a swing type (Slider, Stack ect.)
        /// </summary>
        /// <param name="type">Type of swing you want the count of</param>
        /// <returns></returns>
        public float GetSwingTypePercent(SwingType type = SwingType.Normal) {
            int count = swingContainer.GetJointSwingData().Count(x => x.swingType == type);
            return ((float)count / (float)swingContainer.GetJointSwingData().Count) * 100;
        }

        /// <summary>
        /// Returns the amount of doubles given a list of all swings in the map.
        /// </summary>
        /// <returns></returns>
        public float GetDoublesPercent()
        {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

            leftHand.RemoveAll(x => x.notes.Count == 0);
            rightHand.RemoveAll(x => x.notes.Count == 0);

            // Threshold in ms for when swings are considered at the same time
            double threshold = 0.05;
            List<SwingData> matchedSwings = leftHand
                .Where(leftSwing => rightHand.Any(rightSwing => Math.Abs(leftSwing.notes[0].ms - rightSwing.notes[0].ms) <= threshold))
                .ToList();

            return ((float)matchedSwings.Count / (leftHand.Count + rightHand.Count)) * 100;
        }
    }

    /// <summary>
    /// Analysis object for predicting how every difficulty in a map will play and storing it
    /// </summary>
    public class MapAnalyser
    {
        private readonly Dictionary<string, List<DiffAnalysis>> _difficultySwingData = new Dictionary<string, List<DiffAnalysis>>();

        public MapStructure MapInfo { get; }
        public Dictionary<string, List<DiffAnalysis>> DiffSwingData => _difficultySwingData;

        public MapAnalyser(string mapPath, bool runAllDiffs = true, IParityMethod? parityMethod = null)
        {
            parityMethod ??= new GenericParityCheck();

            MapInfo = MapLoader.LoadMapFromFile(mapPath);
            if (runAllDiffs) RunAllDifficulties(parityMethod);
        }
    
        /// <summary>
        /// Runs through every difficulty listed in info.dat and calculates Swing Data
        /// </summary>
        /// <param name="parityMethod">Method for calculating parity</param>
        private void RunAllDifficulties(IParityMethod? parityMethod = null)
        {
            foreach (MapDifficultyStructure characteristic in MapInfo._difficultyBeatmapSets)
            {
                // If standard characteristic
                string characteristicName = characteristic._beatmapCharacteristicName.ToLower();

                // Load each difficulty, calculate swing data
                foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                {
                    MapData diffData = MapLoader.LoadDifficultyDataFromFolder(MapInfo._mapFolder, difficulty);

                    // Create BPM Handler and Generate Swing Data for this Difficulty
                    BPMHandler bpmHandler = BPMHandler.CreateBPMHandler(MapInfo._beatsPerMinute, diffData.DifficultyData.bpmEvents.ToList(), MapInfo._songTimeOffset);
                    MapSwingContainer predictedSwings = SwingDataGeneration.Run(diffData, bpmHandler, parityMethod);

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
                    returnString += "\nSlider %: " + diffAnalysis.GetSwingTypePercent(SwingType.Slider);
                    returnString += "\nWindow %: " + diffAnalysis.GetSwingTypePercent(SwingType.Window);
                    returnString += "\nStack %: " + diffAnalysis.GetSwingTypePercent(SwingType.Stack);
                    returnString += "\nNormal %: " + diffAnalysis.GetSwingTypePercent(SwingType.Normal);
                    returnString += "\nDoubles %: " + diffAnalysis.GetDoublesPercent();
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
        /// <param name="difficultyRank">Specific difficulty rank to retrieve data for</param>
        /// <param name="characteristic">Characteristic difficulty belongs to</param>
        /// <returns></returns>
        public DiffAnalysis GetDiffAnalysis(BeatmapDifficultyRank difficultyRank, string characteristic = "standard")
        {
            if (!_difficultySwingData.ContainsKey(characteristic)) return new();

            List<DiffAnalysis> diffAnalysis = _difficultySwingData[characteristic];
            foreach (DiffAnalysis analysis in diffAnalysis)
            {
                if (analysis.difficultyRank == difficultyRank)
                {
                    return analysis;
                }
            }
            return new();
        }
    }
}
