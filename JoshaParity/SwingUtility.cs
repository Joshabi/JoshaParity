﻿namespace JoshaUtils
{
    public static class SwingUtility
    {
        /// <summary>
        /// Returns a string timestamp given map BPM and a beat number.
        /// </summary>
        /// <param name="BPM">Beats per minute of map</param>
        /// <param name="beat">Beat you want timestamp for</param>
        /// <returns></returns>
        public static string BeatToTimestamp(float BPM, float beat)
        {
            var seconds = beat / (BPM / 60);
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            string timestamp =
                string.Format("{0:D2}m:{1:D2}s:{2:D2}ms", time.Minutes, time.Seconds, time.Milliseconds);


            return timestamp;
        }

        /// <summary>
        /// Returns the effective BPM of a swing given time in beats and song BPM.
        /// </summary>
        /// <param name="BPM">Beats per minute of map</param>
        /// <param name="beats">Time between 2 swings in beats</param>
        /// <returns></returns>
        public static float SwingEBPM(float BPM, float beats)
        {
            var seconds = beats / (BPM / 60);
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            return (float)((60000 / time.TotalMilliseconds) / 2);
        }

        /// <summary>
        /// Converts a length in beats into seconds given map BPM and beat time.
        /// </summary>
        /// <param name="BPM">Beats per minute of map</param>
        /// <param name="beats">Length of time in beats</param>
        /// <returns></returns>
        public static float BeatToSeconds(float BPM, float beats)
        {
            return (beats / (BPM / 60));
        }

        /// <summary>
        /// Converts a length of time in seconds into beats given map BPM and seconds
        /// </summary>
        /// <param name="BPM">Beats per minute of map</param>
        /// <param name="seconds">Length of time in seconds</param>
        /// <returns></returns>
        public static float SecondsToBeats(float BPM, float seconds)
        {
            return seconds * (BPM / 60.0f);
        }
    }
}