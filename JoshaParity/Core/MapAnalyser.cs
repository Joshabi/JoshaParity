using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace JoshaParity
{
    /// <summary>
    /// Results container representing a specific difficulty
    /// </summary>
    public struct DiffAnalysis
    {
        // Describes which hands to consider for a statistic result
        public enum HandResult {
            Left, Right, Both
        }

        public BeatmapDifficultyRank difficultyRank = BeatmapDifficultyRank.ExpertPlus;
        public MapSwingContainer swingContainer = new();
        public BPMHandler bpmHandler = new(0,new(),0);
        public MapObjects mapObjects = new([],[],[]);

        /// <summary>
        /// Constructor when SwingData is already computed
        /// </summary>
        public DiffAnalysis(BeatmapDifficultyRank difficultyRank, MapSwingContainer container, BPMHandler bpmHandler, MapObjects mapObjects)
        {
            this.difficultyRank = difficultyRank;
            swingContainer = container;
            this.bpmHandler = bpmHandler;
            this.mapObjects = mapObjects;
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
            mapObjects = MapAnalyser.MapObjectsFromDiff(diffData, bpmHandler);
            swingContainer = SwingDataGeneration.Run(mapObjects, bpmHandler, ParityMethodology);
        }

        /// <summary>
        /// Returns a list of both hands' predicted SwingData
        /// </summary>
        /// <returns></returns>
        public readonly List<SwingData> GetSwingData() {
            return swingContainer.GetJointSwingData();
        }

        /// <summary>
        /// Returns the amount of predicted resets based on type
        /// </summary>
        /// <param name="type">Type of Reset to return count of</param>
        /// <returns></returns>
        public readonly int GetResetCount(ResetType type = ResetType.Rebound) {
            return swingContainer.GetJointSwingData().Count <= 1 ? 0 : swingContainer.GetJointSwingData().Count(x => x.resetType == type);
        }

        /// <summary>
        /// Returns the SPS for either hand or both
        /// </summary>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public readonly float GetSPS(HandResult hand = HandResult.Both)
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
        public readonly float GetAverageEBPM(HandResult hand = HandResult.Both) {
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

            return hand switch
            {
                HandResult.Left => (leftHand.Count == 0) ? 0 : leftHand.Average(x => x.swingEBPM),
                HandResult.Right => (rightHand.Count == 0) ? 0 : rightHand.Average(x => x.swingEBPM),
                HandResult.Both => (leftHand.Count + rightHand.Count == 0) ? 0 : GetSwingData().Average(x => x.swingEBPM),
                _ => 0
            };
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand in the form of a Vector where X = right, Y = left
        /// </summary>
        /// <returns></returns>
        public readonly Vector2 GetHandedness() {
            return new Vector2(GetHandedness(HandResult.Right), GetHandedness(HandResult.Left));
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand
        /// </summary>
        /// <param name="hand">Which hand to get % of swings for</param>
        /// <returns></returns>
        public readonly float GetHandedness(HandResult hand = HandResult.Right)
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
        public readonly float GetSwingTypePercent(SwingType type = SwingType.Normal, HandResult hand = HandResult.Both) {
            List<SwingData> leftHand = swingContainer.LeftHandSwings;
            List<SwingData> rightHand = swingContainer.RightHandSwings;
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
        public readonly float GetDoublesPercent()
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
        /// Gets the Average Grid Spacing from the end of a swing till the start of the next
        /// </summary>
        /// <param name="hand">Which hand to check: Left or Right</param>
        /// <returns></returns>
        public readonly float GetAverageSpacing(HandResult hand = HandResult.Right) {
            List<SwingData> handSwings = hand == HandResult.Left ? swingContainer.LeftHandSwings.ToList() : swingContainer.RightHandSwings.ToList();
            if (handSwings.Count <= 1) { return 0; }
            return handSwings
                .Zip(handSwings.Skip(1), (current, next) => {
                    float dX = next.startPos.x - current.endPos.x;
                    float dY = next.startPos.y - current.endPos.y;
                    return (float)Math.Sqrt(dX * dX + dY * dY);
                }).Average();
        }

        /// <summary>
        /// Gets the Average Angle Change from the end of a swing till the start of the next
        /// </summary>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public readonly float GetAverageAngleChange(HandResult hand = HandResult.Right)
        {
            List<SwingData> leftHand = swingContainer.LeftHandSwings;
            List<SwingData> rightHand = swingContainer.RightHandSwings;
            static float AverageAngleChange(IEnumerable<SwingData> swings) {
                if (swings.Count() <= 1) return 0;
                return swings.Zip(swings.Skip(1), (current, next) =>
                    Math.Abs(next.startPos.rotation - current.endPos.rotation))
                    .Average();
            }

            float leftHandARC = AverageAngleChange(leftHand);
            float rightHandARC = AverageAngleChange(rightHand);
            try {
                return hand switch {
                    HandResult.Left => leftHandARC,
                    HandResult.Right => rightHandARC,
                    HandResult.Both => (leftHandARC + rightHandARC) / 2,
                    _ => 0
                };
            } catch {
                return 0;
            }
        }

        /// <summary>
        /// Formatted information about this analysis object
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() {
            StringBuilder sb = new();
            sb.AppendLine($"{difficultyRank}");
            sb.AppendLine("-----------------------");
            sb.AppendLine($"Potential Resets:");
            sb.AppendLine($" - Normal Resets: {GetResetCount(ResetType.Rebound)}");
            sb.AppendLine($" - Bomb Resets: {GetResetCount(ResetType.Bomb)}");
            sb.AppendLine($"Average Swings per Second (SPS):");
            sb.AppendLine($" - Total: {GetSPS():F2}");
            sb.AppendLine($" - Left Hand: {GetSPS(HandResult.Left):F2}");
            sb.AppendLine($" - Right Hand: {GetSPS(HandResult.Right):F2}");
            sb.AppendLine($"Swing EBPM:");
            sb.AppendLine($" - Both Hands: {GetAverageEBPM():F2}");
            sb.AppendLine($" - Left Hand: {GetAverageEBPM(HandResult.Left):F2}");
            sb.AppendLine($" - Right Hand: {GetAverageEBPM(HandResult.Right):F2}");
            sb.AppendLine($"Handedness:");
            sb.AppendLine($" - Left Hand: {GetHandedness(HandResult.Left):F2}%");
            sb.AppendLine($" - Right Hand: {GetHandedness(HandResult.Right):F2}%");
            sb.AppendLine($"Percentage of Swing Types:");
            sb.AppendLine($" - Chain: {GetSwingTypePercent(SwingType.Chain):F2}%");
            sb.AppendLine($" - Slider: {GetSwingTypePercent(SwingType.Slider):F2}%");
            sb.AppendLine($" - Window: {GetSwingTypePercent(SwingType.Window):F2}%");
            sb.AppendLine($" - Stack: {GetSwingTypePercent(SwingType.Stack):F2}%");
            sb.AppendLine($" - Normal: {GetSwingTypePercent(SwingType.Normal):F2}%");
            sb.AppendLine($" - Doubles: {GetDoublesPercent():F2}%");
            sb.AppendLine($"Average Swing Spacing (Grid Spaces):");
            sb.AppendLine($" - Left Hand: {GetAverageSpacing(HandResult.Left):F2}");
            sb.AppendLine($" - Right Hand: {GetAverageSpacing(HandResult.Right):F2}");
            sb.AppendLine($"Average Angle Change:");
            sb.AppendLine($" - Both Hands: {GetAverageAngleChange(HandResult.Both):F2}");
            sb.AppendLine($" - Left Hand: {GetAverageAngleChange(HandResult.Left):F2}");
            sb.AppendLine($" - Right Hand: {GetAverageAngleChange(HandResult.Right):F2}");
            return sb.ToString();
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
                    MapObjects mapObjects = MapObjectsFromDiff(diffData, bpmHandler);
                    MapSwingContainer predictedSwings = SwingDataGeneration.Run(mapObjects, bpmHandler, parityMethod);

                    // If Characteristic doesn't exist, need to initialize
                    if (!_difficultySwingData.ContainsKey(characteristicName)) {
                        _difficultySwingData.Add(characteristicName, new List<DiffAnalysis>());
                    }

                    _difficultySwingData[characteristicName].Add(new DiffAnalysis(difficulty._difficultyRank, predictedSwings, bpmHandler, mapObjects));
                }
            }
        }

        /// <summary>
        /// Converts MapData and BPMHandler to Map Objects
        /// </summary>
        /// <param name="diff"></param>
        /// <param name="bpmHandler"></param>
        /// <returns></returns>
        internal static MapObjects MapObjectsFromDiff(MapData diff, BPMHandler bpmHandler) {
            List<Note> notes = new List<Note>(diff.DifficultyData.colorNotes.ToList());
            List<Bomb> bombs = new List<Bomb>(diff.DifficultyData.bombNotes.ToList());
            List<Obstacle> walls = new List<Obstacle>(diff.DifficultyData.obstacles.ToList());
            List<BurstSlider> burstSliders = new List<BurstSlider>(diff.DifficultyData.burstSliders.ToList());

            // Convert burst sliders to pseudo-notes
            notes.AddRange(burstSliders);
            notes = notes.OrderBy(x => x.b).ToList();

            // Set MS values for notes
            foreach (Note note in notes) {
                float seconds = bpmHandler.ToRealTime(note.b);
                note.ms = seconds * 1000;
            }

            // Calculate swing data for both hands
            return new MapObjects(notes, bombs, walls);
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
        public string MapInfoToString() {
            StringBuilder sb = new();
            sb.AppendLine("Map Information");
            sb.AppendLine("-------------------------------------------------");
            sb.AppendLine($"Map Name: {MapInfo._songName} {MapInfo._songSubName} by {MapInfo._songAuthorName}");
            sb.AppendLine($"Mapped by: {MapInfo._levelAuthorName}");
            sb.AppendLine($"Base BPM: {MapInfo._beatsPerMinute}");
            sb.AppendLine($"Environment: {MapInfo._environmentName}");
            sb.AppendLine($"Format: {MapInfo._version}");
            return sb.ToString();
        }

        /// <summary>
        /// Formatted information about this mapset
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(MapInfoToString());
            sb.AppendLine();
            // For every characteristic and loaded difficulty:
            foreach (KeyValuePair<string, List<DiffAnalysis>> characteristicData in _difficultySwingData) {
                sb.AppendLine("-------------------------------------------------");
                sb.AppendLine($"Characteristic: {characteristicData.Key}");
                sb.AppendLine("-------------------------------------------------");
                // For every difficulty in the characteristic
                foreach (DiffAnalysis diffAnalysis in characteristicData.Value) {
                    sb.AppendLine("-----------------------");
                    sb.AppendLine(diffAnalysis.ToString());
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
