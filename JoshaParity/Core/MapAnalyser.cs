﻿using System.Collections.Generic;
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
        public List<SwingData> swingData;
        public BPMHandler bpmHandler;
        public string mapFormat;

        public DiffAnalysis(BeatmapDifficultyRank difficultyRank, List<SwingData> swingData, BPMHandler bpmHandler, string mapFormat) : this()
        {
            this.difficultyRank = difficultyRank;
            this.swingData = swingData;
            this.bpmHandler = bpmHandler;
            this.mapFormat = mapFormat;
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
                    returnString += "\nPotential Bomb Reset Count: " + GetResetCount(diffAnalysis.difficultyRank, characteristicData.Key, ResetType.Bomb);
                    returnString += "\nPotential Reset Count: " + GetResetCount(diffAnalysis.difficultyRank, characteristicData.Key, ResetType.Rebound);
                    returnString += "\nAverage Swings Per Second: " + GetSPS(diffAnalysis.difficultyRank, characteristicData.Key);
                    returnString += "\nAverage Swing EBPM: " + GetAverageEBPM(diffAnalysis.difficultyRank, characteristicData.Key);
                    Vector2 handedness = GetHandedness(diffAnalysis.difficultyRank, characteristicData.Key);
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

        /// <summary>
        /// Returns a list of predicted SwingData on how a map is played
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <returns></returns>
        public List<SwingData> GetSwingData(BeatmapDifficultyRank difficultyID, string characteristic = "standard")
        {
            return GetDiffAnalysis(difficultyID, characteristic).swingData;
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

            float leftSPS = 0;
            float rightSPS = 0;

            if (leftHand.Count != 0)
            {
                leftSPS = leftHand.Count / TimeUtils.BeatToSeconds(MapInfo._beatsPerMinute,
                    (leftHand.Last().swingEndBeat - leftHand.First().swingStartBeat));
            }

            if (rightHand.Count != 0)
            {
                rightSPS = rightHand.Count / TimeUtils.BeatToSeconds(MapInfo._beatsPerMinute,
                    (rightHand.Last().swingEndBeat - rightHand.First().swingStartBeat));
            }

            return leftSPS + rightSPS;
        }

        /// <summary>
        /// Returns the average swing EBPM for a given map difficulty
        /// </summary>
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <returns></returns>
        public float GetAverageEBPM(BeatmapDifficultyRank difficultyID, string characteristic = "standard")
        {
            List<SwingData> swingData = GetSwingData(difficultyID, characteristic);
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
        /// <param name="difficultyID">Specific difficulty to retrieve data from</param>
        /// <param name="characteristic">Characteristic to load</param>
        /// <returns></returns>
        public Vector2 GetHandedness(BeatmapDifficultyRank difficultyID, string characteristic = "standard")
        {
            List<SwingData> swingData = GetSwingData(difficultyID, characteristic);
            List<SwingData> leftHand = swingData.FindAll(x => !x.rightHand);
            List<SwingData> rightHand = swingData.FindAll(x => x.rightHand);

            float leftPercent = (float)leftHand.Count / (float)swingData.Count * 100;
            float rightPercent = (float)rightHand.Count / (float)swingData.Count * 100;

            return new Vector2(rightPercent, leftPercent);
        }
    }
}