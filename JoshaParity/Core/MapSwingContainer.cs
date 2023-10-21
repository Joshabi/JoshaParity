using System.Collections.Generic;

namespace JoshaParity
{
    public class MapSwingContainer
    {
        public List<SwingData> LeftHandSwings { get; private set; } = new();
        public List<SwingData> RightHandSwings { get; private set; } = new();
        public float leanValue = 0;

        public MapSwingContainer(MapSwingContainer source) { CopySwingsFrom(source); }

        /// <summary>
        /// Copies the swings from another container to use as a basis for this one
        /// </summary>
        /// <param name="source"></param>
        public void CopySwingsFrom(MapSwingContainer source)
        {
            LeftHandSwings = new List<SwingData>(source.LeftHandSwings);
            RightHandSwings = new List<SwingData>(source.RightHandSwings);
            UpdateLeanState(); // Update lean based on copied swings.
        }

        /// <summary>
        /// Adds a swing to the container
        /// </summary>
        /// <param name="swing"></param>
        /// <param name="rightHand"></param>
        public void AddSwing(SwingData swing, bool rightHand = true)
        {
            if (rightHand) RightHandSwings.Add(swing);
            else LeftHandSwings.Add(swing);
            UpdateLeanState();
        }

        /// <summary>
        /// Updates the state of the containers lean
        /// </summary>
        private void UpdateLeanState()
        {
            if (LeftHandSwings.Count <= 0 || RightHandSwings.Count <= 0) return;
            leanValue = (RightHandSwings[RightHandSwings.Count - 1].endPos.rotation + LeftHandSwings[LeftHandSwings.Count - 1].endPos.rotation) / 2;
        }
    }
}
