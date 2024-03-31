using System;
using System.Collections.Generic;
using System.Linq;

namespace JoshaParity
{
    // Referenced:
    // https://github.com/LightAi39/ChroMapper-AutoModder/blob/main/ChroMapper-LightModding/BeatmapScanner/MapCheck/Timescale.cs
    // https://github.com/KivalEvan/BeatSaber-MapCheck/blob/main/src/ts/beatmap/shared/bpm.ts

    /// <summary>
    /// BPM Handler Object for handing Official BPM Changes and storing BPM Info
    /// </summary>
    public class BPMHandler
    {
        private float _bpm;
        private readonly List<BPMChangeEvent> _bpmChanges;
        private readonly List<BPMTimeScaler> _timeScale;
        private readonly float _offset;

        public float BPM { get => _bpm; }
        public List<BPMChangeEvent> BPMChanges { get => _bpmChanges; }
        public int TotalBPMChanges { get => _bpmChanges.Count; }

        /// <summary>
        /// Constructs BPMHandler given a list of bpm events and map offset
        /// </summary>
        /// <param name="bpm">Map Base BPM</param>
        /// <param name="bpmChanges">All BPM Changes (Official)</param>
        /// <param name="offset">Map Offset</param>
        public BPMHandler (float bpm, List<BPMChangeEvent> bpmChanges, float offset)
        {
            _bpm = bpm;
            _offset = offset;
            _bpmChanges = GetBPMChangeTime(bpmChanges);
            _timeScale = GetTimeScale(bpmChanges);
        }

        /// <summary>
        /// Returns a new BPMHandler given a list of bpm events and map offset
        /// </summary>
        /// <param name="bpm">Map Base BPM</param>
        /// <param name="bpmChanges">All BPM Changes (Official)</param>
        /// <param name="offset">Map Offset</param>
        public static BPMHandler CreateBPMHandler(float bpm, List<BPMEvent> bpmChanges, float offset)
        {
            List<BPMChangeEvent> change = new();
            foreach (BPMEvent bpmEvent in bpmChanges){
                change.Add(new(bpmEvent));
            }
            return new BPMHandler(bpm, change, offset);
        }

        /// <summary>
        /// Calculates NewTime for all BPMChangeEvents
        /// </summary>
        /// <param name="bpmChanges">List of BPM Changes</param>
        /// <returns></returns>
        public List<BPMChangeEvent> GetBPMChangeTime(List<BPMChangeEvent> bpmChanges)
        {
            // Order BPM Changes
            bpmChanges.OrderBy(bpm => bpm.b);
            List<BPMChangeEvent> alteredBPMChanges = new();
            BPMChangeEvent? temp = null;

            foreach (BPMChangeEvent curBPMChange in bpmChanges)
            {
                curBPMChange.newTime = temp != null
                    ? (float)Math.Ceiling(((curBPMChange.b - temp.b) / _bpm * temp.m) + temp.newTime - 0.01)
                    : (float)Math.Ceiling(curBPMChange.b - (_offset * _bpm / 60) - 0.01);

                alteredBPMChanges.Add(curBPMChange);
                temp = curBPMChange;
            }

            return alteredBPMChanges;
        }

        /// <summary>
        /// Grabs TimeScale based on BPMChanges
        /// </summary>
        /// <param name="bpmChanges">List of BPM Changes</param>
        /// <returns></returns>
        public List<BPMTimeScaler> GetTimeScale(List<BPMChangeEvent> bpmChanges)
        {
            // Order BPM Changes
            bpmChanges.OrderBy(bpm => bpm.b);
            List<BPMTimeScaler> timeScale = new();

            foreach (BPMChangeEvent bpm in bpmChanges)
            {
                BPMTimeScaler ibpm = new()
                {
                    t = bpm.b,
                    s = _bpm / bpm.m
                };
                timeScale.Add(ibpm);
            }

            return timeScale;
        }

        /// <summary>
        /// Converts a beat number to seconds accounting for BPM Changes
        /// </summary>
        /// <param name="beat">Beat to Convert</param>
        /// <param name="timescale">Utilise Timescale?</param>
        /// <returns></returns>
        public float ToRealTime(float beat, bool timescale = true)
        {
            if (!timescale) return beat / _bpm * 60;

            float calculatedBeat = 0;
            for (int i = _timeScale.Count - 1; i >= 0; i--)
            {
                if (beat > _timeScale[i].t)
                {
                    calculatedBeat += (beat - _timeScale[i].t) * _timeScale[i].s;
                    beat = _timeScale[i].t;
                }
            }

            return (beat + calculatedBeat) / _bpm * 60;
        }

        /// <summary>
        /// Converts seconds to beat number accounting for BPM Changes
        /// </summary>
        /// <param name="seconds">Seconds to Convert</param>
        /// <param name="timescale">Utilise Timescale?</param>
        /// <returns></returns>
        public float ToBeatTime(float seconds, bool timescale = false)
        {
            if (!timescale) return seconds * _bpm / 60;

            float calculatedSecond = 0;
            for (int i = _timeScale.Count - 1; i >= 0; i--)
            {
                float currentSeconds = ToRealTime(_timeScale[i].t);
                if (seconds > currentSeconds)
                {
                    calculatedSecond += (seconds - currentSeconds) / _timeScale[i].s;
                    seconds = currentSeconds;
                }
            }
            return ToBeatTime(seconds + calculatedSecond);
        }

        /// <summary>
        /// Converts to JSON File time
        /// </summary>
        /// <param name="beat">Beat to Convert</param>
        /// <returns></returns>
        public float ToJsonTime(float beat)
        {
            for (int i = _bpmChanges.Count - 1; i >= 0; i--)
            {
                if (beat > _bpmChanges[i].newTime)
                {
                    return ((beat - _bpmChanges[i].newTime) / _bpmChanges[i].m * _bpm) + _bpmChanges[i].b;
                }
            }
            return ToBeatTime(ToRealTime(beat, false) + _offset);
        }

        /// <summary>
        /// Updates the current BPM value stored based on BPM Changes and provided beat value
        /// </summary>
        /// <param name="beat">Beat to Get BPM at</param>
        public void SetCurrentBPM(float beat)
        {
            for (int i = 0; i < _bpmChanges.Count; i++)
            {
                if (beat > _bpmChanges[i].b)
                {
                    _bpm = _bpmChanges[i].m;
                }
            }
        }

        /// <summary>
        /// TimeScaler for BPM Changes
        /// </summary>
        public class BPMTimeScaler
        {
            public float t;
            public float s;
        }

        /// <summary>
        /// BPM Change Event representing Official BPMChanges
        /// </summary>
        public class BPMChangeEvent
        {
            public float b;
            public float m;
            public float p = 0;
            public float o = 0;
            public float newTime;

            public BPMChangeEvent(BPMEvent bpmEvent)
            {
                b = bpmEvent.b;
                m = bpmEvent.m;
                p = 0;
                o = 0;
                newTime = bpmEvent.b;
            }
        }
    }
}
