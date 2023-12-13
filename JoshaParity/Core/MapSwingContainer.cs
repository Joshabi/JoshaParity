using System;
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

        public MapSwingContainer(MapSwingContainer source) : this() { 
            CopySwingsFrom(source); 
            timeValue = source.timeValue;
        }

        public MapSwingContainer(MapSwingContainer source, float timeValue) : this() {
            CopySwingsFrom(source);
            this.timeValue = timeValue;
        }

        public MapSwingContainer() {
            PositionData.Add(new() { timeValue = 0, offsetValue = Vector2.Zero });
            playerOffset = PositionData[0].offsetValue;
        }

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
        /// Sets player offset data
        /// </summary>
        /// <param name="offsetData"></param>
        public void SetPlayerOffsetData(List<OffsetData> offsetData) { PositionData =  offsetData; }

        /// <summary>
        /// Get Combined Swing Data
        /// </summary>
        public List<SwingData> GetJointSwingData()
        {
            List<SwingData> combined = new List<SwingData>(LeftHandSwings);
            combined.AddRange(new List<SwingData>(RightHandSwings));
            combined.OrderBy(x => x.swingStartBeat);
            return combined;
        }

        /// <summary>
        /// Updates the lean state of this container
        /// </summary>
        private void UpdateLeanState()
        {
            if (LeftHandSwings.Count <= 0 || RightHandSwings.Count <= 0) return;

            // Get the last rotation values for each hand
            float rightHandRotation = RightHandSwings[RightHandSwings.Count - 1].endPos.rotation;
            float leftHandRotation = LeftHandSwings[LeftHandSwings.Count - 1].endPos.rotation;

            // Adjust the left hand rotation to align with the right hand's direction
            leftHandRotation *= -1;

            // Calculate the average rotation considering the adjustment
            currentLeanValue = (rightHandRotation + leftHandRotation) / 2;
            LeanData.Add(new LeanData() { timeValue = timeValue, leanValue = currentLeanValue });
        }
    }
}
