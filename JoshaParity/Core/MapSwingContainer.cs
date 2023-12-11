using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Timestamp and lean value pair
    /// </summary>
    public class LeanData {
        public float timeValue;
        public float leanValue;
    }

    /// <summary>
    /// Timestamp and Vector2 offset value pair
    /// </summary>
    public class OffsetData
    {
        public float timeValue;
        public Vector2 offsetValue;
    }

    /// <summary>
    /// Acts as a "state" of swing generation. Contains all the swings that make up this state and the current values of this state
    /// </summary>
    public class MapSwingContainer
    {
        // The list of swings that make up this state and per-hand swing constructors
        public List<SwingData> LeftHandSwings { get; private set; } = new();
        public List<SwingData> RightHandSwings { get; private set; } = new();
        public List<LeanData> LeanData { get; private set; } = new();
        public List<OffsetData> PositionData { get; private set; } = new();
        public MapSwingClassifier leftHandConstructor = new();
        public MapSwingClassifier rightHandConstructor = new();

        // Current Values defining this state. As swings are added this state is updated
        public float currentLeanValue = 0;
        public Vector2 playerOffset;
        public float lastDodgeTime;
        public float lastDuckTime;
        public float timeValue = 0;

        public MapSwingContainer(MapSwingContainer source) { 
            CopySwingsFrom(source); 
            timeValue = source.timeValue;
        }

        public MapSwingContainer(MapSwingContainer source, float timeValue) {
            CopySwingsFrom(source);
            this.timeValue = timeValue;
        }

        public MapSwingContainer() { }

        /// <summary>
        /// Copies the swings from another container to use as a basis for this one
        /// </summary>
        /// <param name="source">Container State to copy from</param>
        public void CopySwingsFrom(MapSwingContainer source)
        {
            LeftHandSwings = new List<SwingData>(source.LeftHandSwings);
            RightHandSwings = new List<SwingData>(source.RightHandSwings);
            LeanData = new List<LeanData>(source.LeanData);
            PositionData = new List<OffsetData>(source.PositionData);
        }

        /// <summary>
        /// Adds a swing to the container
        /// </summary>
        /// <param name="swing">Swing to add to this container</param>
        /// <param name="rightHand">Is the swing right handed?</param>
        public void AddSwing(SwingData swing, bool rightHand = true)
        {
            if (rightHand) { RightHandSwings.Add(swing); }
            else { LeftHandSwings.Add(swing); }
            if (swing.notes.Count != 0) {
                timeValue = swing.notes.Max(x => x.ms); UpdateLeanState();
            }
        }

        /// <summary>
        /// Updates the lean state of this container
        /// </summary>
        private void UpdateLeanState()
        {
            if (LeftHandSwings.Count <= 0 || RightHandSwings.Count <= 0) return;
            currentLeanValue = (RightHandSwings[RightHandSwings.Count - 1].endPos.rotation + LeftHandSwings[LeftHandSwings.Count - 1].endPos.rotation) / 2;
            LeanData.Add(new LeanData() { timeValue = timeValue, leanValue = currentLeanValue });
        }
    }
}
