namespace JoshaParity
{
    /// <summary>
    /// Time utility related functionalities
    /// </summary>
    public class TimeUtils
    {
        /// <summary>
        /// Returns the effective BPM of a swing given BPM Info and 2 points
        /// </summary>
        /// <param name="bpmHandler">BPMHandler for the map</param>
        /// <param name="startBeat">Beat time of last swing</param>
        /// <param name="endBeat">Beat time of current swing</param>
        /// <returns></returns>
        public static float SwingEBPM(BPMHandler bpmHandler, float startBeat, float endBeat)
        {
            if (startBeat == 0 && endBeat == 0) { return 0; }
            float secondsDiff = bpmHandler.ToRealTime(endBeat) - bpmHandler.ToRealTime(startBeat);
            return (float)(60 / (2 * secondsDiff));
        }

        /// <summary>
        /// Converts a length in beats into seconds given persistent BPM and a beat, disregarding BPM Changes
        /// </summary>
        /// <param name="BPM">Beats per minute of the map</param>
        /// <param name="beats">Length of time in beats</param>
        /// <returns></returns>
        public static float BeatToSeconds(float BPM, float beats)
        {
            return beats / (BPM / 60);
        }

        /// <summary>
        /// Converts 2 points in time (in beats) and returns the seconds, accounting for BPM Changes
        /// </summary>
        /// <param name="bpmHandler">BPMHandler for the map</param>
        /// <param name="startBeat">Beat time of last swing</param>
        /// <param name="endBeat">Beat time of current swing</param>
        /// <returns></returns>
        public static float BeatsToSeconds(BPMHandler bpmHandler, float startBeat, float endBeat)
        {
            return startBeat == 0 && endBeat == 0 ? 0 : bpmHandler.ToRealTime(endBeat) - bpmHandler.ToRealTime(startBeat);
        }
    }
}
