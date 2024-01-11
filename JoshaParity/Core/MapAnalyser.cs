using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Results container representing a specific difficulty
    /// </summary>
    public struct DiffAnalysis
    {
        // Describes which hands to consider for a statistic result
        public enum HandResult
        {
            Left, Right, Both
        }

        public BeatmapDifficultyRank difficultyRank = BeatmapDifficultyRank.ExpertPlus;
        public MapSwingContainer swingContainer = new();
        public BPMHandler bpmHandler = new(0,new(),0);

        /// <summary>
        /// Constructor when SwingData is already computed
        /// </summary>
        public DiffAnalysis(BeatmapDifficultyRank difficultyRank, MapSwingContainer container, BPMHandler bpmHandler)
        {
            this.difficultyRank = difficultyRank;
            swingContainer = container;
            this.bpmHandler = bpmHandler;
        }

        /// <summary>
        ///  Constructor without Info.dat
        /// </summary>
        public DiffAnalysis(string difficultyDatContents, float bpm, BeatmapDifficultyRank difficultyRank, float songOffset = 0, IParityMethod? parityMethod = null) {
            this.difficultyRank = difficultyRank;
            Init(difficultyDatContents, bpm, songOffset, parityMethod);
        }

        /// <summary>
        /// Constructor with Info.dat
        /// </summary>
        public DiffAnalysis(string infoDatContents, string difficultyDatContents, BeatmapDifficultyRank difficultyRank, IParityMethod? parityMethod = null)
        {
            MapStructure mapInfo = MapLoader.LoadMap(infoDatContents);
            this.difficultyRank = difficultyRank;
            Init(difficultyDatContents, mapInfo._beatsPerMinute, mapInfo._songTimeOffset, parityMethod);
        }

        /// <summary>
        /// Initialisation Helper Function
        /// </summary>
        private void Init(string difficultyDatContents, float bpm, float songOffset = 0, IParityMethod? parityMethod = null)
        {
            MapData diffData = MapLoader.LoadDifficultyData(difficultyDatContents);
            bpmHandler = BPMHandler.CreateBPMHandler(bpm, diffData.DifficultyData.bpmEvents.ToList(), songOffset);
            IParityMethod ParityMethodology = parityMethod ?? new GenericParityCheck();
            swingContainer = SwingDataGeneration.Run(diffData, bpmHandler, ParityMethodology);
        }

        /// <summary>
        /// Returns a list of both hands' predicted SwingData
        /// </summary>
        /// <returns></returns>
        public List<SwingData> GetSwingData() {
            return swingContainer.GetJointSwingData();
        }

        /// <summary>
        /// Returns the amount of predicted resets based on type
        /// </summary>
        /// <param name="type">Type of Reset to return count of</param>
        /// <returns></returns>
        public int GetResetCount(ResetType type = ResetType.Rebound) {
            return swingContainer.GetJointSwingData().Count <= 1 ? 0 : swingContainer.GetJointSwingData().Count(x => x.resetType == type);
        }

        /// <summary>
        /// Returns the SPS for either hand or both
        /// </summary>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public float GetSPS(HandResult hand = HandResult.Both)
        {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

            float leftSPS = (leftHand.Count == 0) ? 
                0 : leftHand.Count / TimeUtils.BeatToSeconds(bpmHandler.BPM,
                    leftHand.Last().swingEndBeat - leftHand.First().swingStartBeat);
            float rightSPS = (rightHand.Count == 0) ?
                0 : rightHand.Count / TimeUtils.BeatToSeconds(bpmHandler.BPM,
                    rightHand.Last().swingEndBeat - rightHand.First().swingStartBeat);

            // Depending on result type, return SPS
            return hand switch {
                HandResult.Left => leftSPS,
                HandResult.Right => rightSPS,
                HandResult.Both => leftSPS + rightSPS,
                _ => 0
            };
        }

        /// <summary>
        /// Returns the average swing EBPM for either hand or both
        /// </summary>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public float GetAverageEBPM(HandResult hand = HandResult.Both) {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

            return hand switch
            {
                HandResult.Left => leftHand.Average(x => x.swingEBPM),
                HandResult.Right => rightHand.Average(x => x.swingEBPM),
                HandResult.Both => GetSwingData().Average(x => x.swingEBPM),
                _ => 0
            };
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand in the form of a Vector where X = right, Y = left
        /// </summary>
        /// <returns></returns>
        public Vector2 GetHandedness() {
            return new Vector2(GetHandedness(HandResult.Right), GetHandedness(HandResult.Left));
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand
        /// </summary>
        /// <param name="hand">Which hand to get % of swings for</param>
        /// <returns></returns>
        public float GetHandedness(HandResult hand = HandResult.Right)
        {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();
            int swingCount = leftHand.Count + rightHand.Count;
            return hand switch
            {
                HandResult.Left => (leftHand.Count == 0) ? 0 : (float)leftHand.Count / swingCount * 100,
                HandResult.Right or HandResult.Both => (rightHand.Count == 0) ? 0 : ((float)rightHand.Count / swingCount * 100),
                _ => 0
            };
        }

        /// <summary>
        /// Returns the amount of a swing type (Slider, Stack ect.)
        /// </summary>
        /// <param name="type">Type of swing you want the count of</param>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public float GetSwingTypePercent(SwingType type = SwingType.Normal, HandResult hand = HandResult.Both) {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();
            return hand switch
            {
                HandResult.Left => leftHand.Count(x => x.swingType == type) / (float)leftHand.Count * 100,
                HandResult.Right => rightHand.Count(x => x.swingType == type) / (float)rightHand.Count * 100,
                HandResult.Both => GetSwingData().Count(x => x.swingType == type) / (float)(leftHand.Count + rightHand.Count)*100,
                _ => 0
            };
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

        /// <summary>
        /// Formatted information about this analysis object
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            const string formatString = "----------------------------------------------";
            string returnString = $"{formatString}\n";
            returnString += "\n" + difficultyRank.ToString();
            returnString += "\nTotal Official BPM Changes Detected: " + bpmHandler.TotalBPMChanges;
            returnString += "\nPotential Bomb Reset Count: " + GetResetCount(ResetType.Bomb);
            returnString += "\nPotential Reset Count: " + GetResetCount(ResetType.Rebound);
            returnString += "\nAverage Swings Per Second:\n Total: " + GetSPS().ToString("0.00") + "\tLeft: " + GetSPS(HandResult.Left).ToString("0.00") + "\tRight: " + GetSPS(HandResult.Right).ToString("0.00");
            returnString += "\nAverage Swing EBPM:\n Total: " + GetAverageEBPM().ToString("0.00") + "\tLeft: " + GetAverageEBPM(HandResult.Left).ToString("0.00") + "\tRight: " + GetAverageEBPM(HandResult.Right).ToString("0.00");
            Vector2 handedness = GetHandedness();
            returnString += "\nRight Swings: " + handedness.X.ToString("0.00") + "%\tLeft Swings: " + handedness.Y.ToString("0.00") + "%";
            returnString += "\nSlider: " + GetSwingTypePercent(SwingType.Slider).ToString("0.00") + "%";
            returnString += "\nWindow: " + GetSwingTypePercent(SwingType.Window).ToString("0.00") + "%";
            returnString += "\nStack: " + GetSwingTypePercent(SwingType.Stack).ToString("0.00") + "%";
            returnString += "\nNormal: " + GetSwingTypePercent(SwingType.Normal).ToString("0.00") + "%";
            returnString += "\nDoubles: " + GetDoublesPercent().ToString("0.00") + "%";
            return returnString;
        }
    }

    /// <summary>
    /// Analysis object for running a mapset
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
            // Foreach characteristic:
            foreach (MapDifficultyStructure characteristic in MapInfo._difficultyBeatmapSets)
            {
                // Foreach difficulty:
                string characteristicName = characteristic._beatmapCharacteristicName.ToLower();
                foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                {
                    // Generate Swing Container
                    MapData diffData = MapLoader.LoadDifficultyDataFromFolder(MapInfo._mapFolder, difficulty);
                    BPMHandler bpmHandler = BPMHandler.CreateBPMHandler(MapInfo._beatsPerMinute, diffData.DifficultyData.bpmEvents.ToList(), MapInfo._songTimeOffset);
                    MapSwingContainer predictedSwings = SwingDataGeneration.Run(diffData, bpmHandler, parityMethod);

                    // If Characteristic doesn't exist, need to initialize
                    if (!_difficultySwingData.ContainsKey(characteristicName)) {
                        _difficultySwingData.Add(characteristicName, new List<DiffAnalysis>());
                    }

                    _difficultySwingData[characteristicName].Add(new DiffAnalysis(difficulty._difficultyRank, predictedSwings, bpmHandler));
                }
            }
        }

        /// <summary>
        /// Returns all difficulty analysis objects
        /// </summary>
        /// <returns></returns>
        public List<DiffAnalysis> GetAllDiffAnalysis()
        {
            // For every characteristic and every difficulty:
            List<DiffAnalysis> result = new List<DiffAnalysis>();
            foreach (KeyValuePair<string, List<DiffAnalysis>> characteristicData in _difficultySwingData)
            {
                foreach (DiffAnalysis diffAnalysis in characteristicData.Value)
                {
                    result.Add(diffAnalysis);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a difficulty analysis object based on Rank and Characteristic
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

        /// <summary>
        /// Formatted information about this mapset
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
                    returnString += $"\n{formatString}\n" + diffAnalysis.ToString();
                }
            }

            returnString += $"\n{formatString}";
            return returnString;
        }
    }
}
