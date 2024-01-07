using System;

namespace JoshaParity
{
    /// <summary>
    /// Time utility related functionalities
    /// </summary>
    public class TimeUtils
    {
        /// <summary>
        /// Returns a string timestamp given map BPM and a beat number
        /// </summary>
        /// <param name="BPM">Beats per minute of the map</param>
        /// <param name="beat">Beat you want timestamp for</param>
        /// <returns></returns>
        public static string BeatToTimestamp(float BPM, float beat)
        {
            float seconds = beat / (BPM / 60);
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            string timestamp =
                $"{time.Minutes:D2}m:{time.Seconds:D2}s:{time.Milliseconds:D2}ms";


            return timestamp;
        }

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
        /// Converts a length in beats into seconds given BPM and beats disregarding BPM Changes
        /// </summary>
        /// <param name="BPM">Beats per minute of the map</param>
        /// <param name="beats">Length of time in beats</param>
        /// <returns></returns>
        public static float BeatToSeconds(float BPM, float beats)
        {
            return (beats / (BPM / 60));
        }

        /// <summary>
        /// Converts a length of time in seconds into beats given BPM and seconds disregarding BPM Changes
        /// </summary>
        /// <param name="BPM">Beats per minute of the map</param>
        /// <param name="seconds">Length of time in seconds</param>
        /// <returns></returns>
        public static float SecondsToBeats(float BPM, float seconds)
        {
            return seconds * (BPM / 60.0f);
        }
    }
}
