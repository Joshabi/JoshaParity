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
    public class OffsetData {
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

        // Current Values defining this state. As swings are added these states are updated
        public float currentLeanValue = 0;
        public Vector2 playerOffset;
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
                timeValue = swing.notes.Max(x => x.ms); 
                UpdateLeanState(); 
                playerOffset = PositionData.Last(x => x.timeValue <= swing.notes[0].b).offsetValue;
            }
        }

        /// <summary>
        /// Sets player offset data
        /// </summary>
        /// <param name="offsetData">List of OffsetData to replace current List in container</param>
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
            // If either hand has no swings, don't bother
            if (LeftHandSwings.Count <= 0 || RightHandSwings.Count <= 0) return;

            // Get the last rotation values for each hand and mirror left
            float rightHandRotation = RightHandSwings[RightHandSwings.Count - 1].endPos.rotation;
            float leftHandRotation = LeftHandSwings[LeftHandSwings.Count - 1].endPos.rotation;
            leftHandRotation *= -1;

            // Calculate the average rotation
            currentLeanValue = (rightHandRotation + leftHandRotation) / 2;
            LeanData.Add(new LeanData() { timeValue = timeValue, leanValue = currentLeanValue });
        }

        /// <summary>
        /// Adds empty, inverse swings for each instance of a Reset in a list of swings
        /// </summary>
        /// <param name="bpmHandler">BPMHandler</param>
        /// <param name="swings">List of swings to add to</param>
        /// <returns></returns>
        public static List<SwingData> AddResetSwingsToList(BPMHandler bpmHandler, List<SwingData> swings)
        {
            List<SwingData> result = new List<SwingData>(swings);
            int swingsAdded = 0;

            for (int i = 1; i < swings.Count - 1; i++)
            {
                // Skip if not Reset
                if (!swings[i].IsReset) continue;

                // Reference to last swing
                SwingData lastSwing = swings[i - 1];
                SwingData currentSwing = swings[i];
                Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
                Note nextNote = currentSwing.notes[0];

                Vector2 avoidanceVector = BeatGrid.PositionToAvoidanceVector[new Vector2(lastSwing.endPos.x, lastSwing.endPos.y)];
                Vector2 swingPos = new Vector2(lastSwing.endPos.x + avoidanceVector.X, lastSwing.endPos.y + avoidanceVector.Y);
                SwingData swing = new SwingData();
                swing.swingParity = (currentSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                swing.swingStartBeat = lastSwing.swingEndBeat + Math.Min((nextNote.b - lastNote.b) / 2, 1);
                swing.swingEndBeat = swing.swingStartBeat + 0.1f;
                swing.swingStartSeconds = bpmHandler.ToRealTime(swing.swingStartBeat);
                swing.swingEndSeconds = bpmHandler.ToRealTime(swing.swingEndBeat);
                swing.SetStartPosition(swingPos.X, swingPos.Y);
                swing.rightHand = swings[0].rightHand;
                swing.swingType = SwingType.Normal;

                // If the last hit was a dot, pick the opposing direction based on parity.
                float diff = currentSwing.startPos.rotation - lastSwing.endPos.rotation;
                float mid = diff / 2;
                mid += lastSwing.endPos.rotation;

                // Set start and end angle, should be the same
                swing.SetStartAngle(mid);
                swing.SetEndAngle(mid);
                swing.SetEndPosition(swingPos.X, swingPos.Y);

                result.Insert(i + swingsAdded, swing);
                swingsAdded++;
            }
            return result;
        }
    }
}
