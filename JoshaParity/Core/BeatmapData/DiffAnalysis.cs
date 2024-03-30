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
    public class DiffAnalysis
    {
        // Describes which hands to consider for a statistic result
        public enum HandResult
        {
            Left, Right, Both
        }

        public BeatmapDifficultyRank difficultyRank = BeatmapDifficultyRank.ExpertPlus;
        public MapSwingContainer swingContainer = new();
        public BPMHandler bpmHandler = new(0, [], 0);
        public MapObjects mapObjects = new([], [], [], [], []);

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
        public DiffAnalysis(string difficultyDatContents, float bpm, BeatmapDifficultyRank difficultyRank, float songOffset = 0, IParityMethod? parityMethod = null)
        {
            this.difficultyRank = difficultyRank;
            Init(difficultyDatContents, bpm, songOffset, parityMethod);
        }

        /// <summary>
        /// Constructor with Info.dat
        /// </summary>
        public DiffAnalysis(string infoDatContents, string difficultyDatContents, BeatmapDifficultyRank difficultyRank, IParityMethod? parityMethod = null)
        {
            SongData mapInfo = MapLoader.LoadMapFromString(infoDatContents);
            this.difficultyRank = difficultyRank;
            Init(difficultyDatContents, mapInfo.Song.BPM, mapInfo.SongTimeOffset, parityMethod);
        }

        /// <summary>
        /// Initialisation Helper Function
        /// </summary>
        private void Init(string difficultyDatContents, float bpm, float songOffset = 0, IParityMethod? parityMethod = null)
        {
            DifficultyData diffData = MapLoader.LoadDifficultyFromString(difficultyDatContents);
            bpmHandler = BPMHandler.CreateBPMHandler(bpm, diffData.BPMChanges, songOffset);
            IParityMethod ParityMethodology = parityMethod ?? new GenericParityCheck();
            mapObjects = MapAnalyser.MapObjectsFromDiff(diffData, bpmHandler);
            swingContainer = SwingDataGeneration.Run(mapObjects, bpmHandler, ParityMethodology);
        }

        /// <summary>
        /// Returns a list of both hands' predicted SwingData
        /// </summary>
        /// <returns></returns>
        public List<SwingData> GetSwingData()
        {
            return swingContainer.GetJointSwingData();
        }

        /// <summary>
        /// Returns the NPS for either hand or both
        /// </summary>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public float GetNPS(HandResult hand)
        {
            var handColour = hand == HandResult.Left ? 0 : 1;
            var notes = hand == HandResult.Both ? mapObjects.Notes : mapObjects.Notes.Where(n => n.c == handColour);
            notes.OrderBy(x => x.ms);
            return notes.Any() ? notes.Count() / (notes.Last().ms / 1000 - notes.First().ms / 1000) : 0;
        }

        /// <summary>
        /// Returns the amount of predicted resets based on type
        /// </summary>
        /// <param name="type">Type of Reset to return count of</param>
        /// <returns></returns>
        public int GetResetCount(ResetType type = ResetType.Rebound)
        {
            if (swingContainer == null) return 0;
            return swingContainer.GetJointSwingData().Count <= 1 ? 0 : swingContainer.GetJointSwingData().Count(x => x.resetType == type);
        }

        /// <summary>
        /// Returns the SPS for either hand or both
        /// </summary>
        /// <param name="hand">Which hand to check or both</param>
        /// <returns></returns>
        public float GetSPS(HandResult hand = HandResult.Both)
        {
            if (swingContainer == null) return 0;
            List<SwingData> leftHand = swingContainer.LeftHandSwings.ToList();
            List<SwingData> rightHand = swingContainer.RightHandSwings.ToList();

            float leftSPS = (leftHand.Count == 0) ?
                0 : leftHand.Count / TimeUtils.BeatToSeconds(bpmHandler.BPM,
                    leftHand.Last().swingEndBeat - leftHand.First().swingStartBeat);
            float rightSPS = (rightHand.Count == 0) ?
                0 : rightHand.Count / TimeUtils.BeatToSeconds(bpmHandler.BPM,
                    rightHand.Last().swingEndBeat - rightHand.First().swingStartBeat);

            // Depending on result type, return SPS
            return hand switch
            {
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
        public float GetAverageEBPM(HandResult hand = HandResult.Both)
        {
            if (swingContainer == null) return 0;
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
        public Vector2 GetHandedness()
        {
            return new Vector2(GetHandedness(HandResult.Right), GetHandedness(HandResult.Left));
        }

        /// <summary>
        /// Returns the % of swings that fall on a given hand
        /// </summary>
        /// <param name="hand">Which hand to get % of swings for</param>
        /// <returns></returns>
        public float GetHandedness(HandResult hand = HandResult.Right)
        {
            if (swingContainer == null) return 0;
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
        public float GetSwingTypePercent(SwingType type = SwingType.Normal, HandResult hand = HandResult.Both)
        {
            if (swingContainer == null) return 0;
            List<SwingData> leftHand = swingContainer.LeftHandSwings;
            List<SwingData> rightHand = swingContainer.RightHandSwings;
            return hand switch
            {
                HandResult.Left => leftHand.Count(x => x.swingType == type) / (float)leftHand.Count * 100,
                HandResult.Right => rightHand.Count(x => x.swingType == type) / (float)rightHand.Count * 100,
                HandResult.Both => GetSwingData().Count(x => x.swingType == type) / (float)(leftHand.Count + rightHand.Count) * 100,
                _ => 0
            };
        }

        /// <summary>
        /// Returns the amount of doubles given a list of all swings in the map.
        /// </summary>
        /// <returns></returns>
        public float GetDoublesPercent()
        {
            if (swingContainer == null) return 0;
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
        public float GetAverageSpacing(HandResult hand = HandResult.Right)
        {
            if (swingContainer == null) return 0;
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
        public float GetAverageAngleChange(HandResult hand = HandResult.Right)
        {
            if (swingContainer == null) return 0;
            List<SwingData> leftHand = swingContainer.LeftHandSwings;
            List<SwingData> rightHand = swingContainer.RightHandSwings;
            static float AverageAngleChange(IEnumerable<SwingData> swings)
            {
                if (swings.Count() <= 1) return 0;
                return swings.Zip(swings.Skip(1), (current, next) =>
                    Math.Abs(next.startPos.rotation - current.endPos.rotation))
                    .Average();
            }

            float leftHandARC = AverageAngleChange(leftHand);
            float rightHandARC = AverageAngleChange(rightHand);
            try
            {
                return hand switch
                {
                    HandResult.Left => leftHandARC,
                    HandResult.Right => rightHandARC,
                    HandResult.Both => (leftHandARC + rightHandARC) / 2,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Formatted information about this analysis object
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
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
}
