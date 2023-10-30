using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Contains map entities (Bombs, Walls, Notes).
    /// </summary>
    public class MapObjects
    {
        public List<Note> Notes { get; set; }
        public List<Bomb> Bombs { get; }
        public List<Obstacle> Obstacles { get; }

        public MapObjects(List<Note> notes, List<Bomb> bombs, List<Obstacle> walls)
        {
            Notes = new List<Note>(notes);
            Bombs = new List<Bomb>(bombs);
            Obstacles = new List<Obstacle>(walls);
        }
    }

    /// <summary>
    /// Functionality for generating swing data about a given difficulty.
    /// </summary>
    public static class SwingDataGeneration
    {
        #region Variables

        private static IParityMethod ParityMethodology = new GenericParityCheck();
        public static BPMHandler BpmHandler = BPMHandler.CreateBPMHandler(0,new(),0);
        public static MapSwingContainer mainContainer = new();
        public static MapObjects? _mapData;
        public static bool rH = true;
        private static Vector2 _playerOffset;
        private static float _lastDodgeTime;
        private static float _lastDuckTime;

        #endregion

        /// <summary>
        /// Called to check a specific map difficulty.
        /// </summary>
        /// <param name="mapDif">Map Difficulty to check</param>
        /// <param name="BPMHandler">BPM Handler for the difficulty containing base BPM and BPM Changes</param>
        /// <param name="parityMethod">Optional: Parity Check Logic</param>
        public static List<SwingData> Run(MapData mapDif, BPMHandler BPMHandler, IParityMethod? parityMethod = null)
        {
            ParityMethodology = parityMethod ??= new GenericParityCheck();
            BpmHandler = BPMHandler;
            _playerOffset = Vector2.Zero;
            _lastDodgeTime = 0; _lastDuckTime = 0;

            // Separate notes, bombs, walls and burst sliders
            List<Note> notes = new List<Note>(mapDif.DifficultyData.colorNotes.ToList());
            List<Bomb> bombs = new List<Bomb>(mapDif.DifficultyData.bombNotes.ToList());
            List<Obstacle> walls = new List<Obstacle>(mapDif.DifficultyData.obstacles.ToList());
            List<BurstSlider> burstSliders = new List<BurstSlider>(mapDif.DifficultyData.burstSliders.ToList());

            // Convert burst sliders to pseudo-notes
            notes.AddRange(burstSliders);
            notes = notes.OrderBy(x => x.b).ToList();

            // Calculate swing data for both hands
            _mapData = new MapObjects(notes, bombs, walls);
            MapSwingContainer finishedState = SimulateSwings(mainContainer, _mapData);
            List<SwingData> swings = new List<SwingData>();
            swings.AddRange(finishedState.RightHandSwings);
            swings.AddRange(finishedState.LeftHandSwings);
            return swings;
        }

        /// <summary>
        /// Simulates swings for a given state and map data
        /// </summary>
        /// <param name="curState">Current swing container state</param>
        /// <param name="mapData">Notes, obstacles and walls</param>
        /// <returns></returns>
        public static MapSwingContainer SimulateSwings(MapSwingContainer curState, MapObjects mapData) {
            // Reference Fix and Remove Prior Notes
            MapObjects mapObjects = new MapObjects(mapData.Notes, mapData.Bombs, mapData.Obstacles);
            mapObjects.Notes.RemoveAll(x => x.ms < curState.timeValue);
            
            if (mapObjects.Notes.Count == 0) { return curState; }

            // Foreach note going forwards
            for (int i = 0; i < mapObjects.Notes.Count; i++)
            {
                Note currentNote = mapObjects.Notes[i];

                // Set Ms
                BpmHandler.SetCurrentBPM(currentNote.b);
                float beatMS = 60 * 1000 / BpmHandler.BPM;
                currentNote.ms = beatMS * currentNote.b;

                // Depending on hand, update buffer
                if (currentNote.c == 0) { curState.leftHandConstructor.UpdateBuffer(currentNote); }
                else if (currentNote.c == 1) { curState.rightHandConstructor.UpdateBuffer(currentNote); }

                if (i == mapObjects.Notes.Count - 1) { curState.leftHandConstructor.EndBuffer(); curState.rightHandConstructor.EndBuffer(); }

                (SwingType leftSwingType, List<Note> leftNotesInSwing) = curState.leftHandConstructor.ClassifyBuffer();
                (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.ClassifyBuffer();

                // If done with either configure swing and add
                // Left
                if (leftSwingType != SwingType.Undecided) { curState.AddSwing(ConfigureSwing(curState,mapData, leftNotesInSwing, leftSwingType, false),false); }
                // Right
                if (rightSwingType != SwingType.Undecided) { curState.AddSwing(ConfigureSwing(curState, mapData, rightNotesInSwing, rightSwingType, true), true); }
            }

            return curState;
        }

        /// <summary>
        /// Calculates and returns a list of swing data for a set of map objects.
        /// </summary>
        /// <param name="mapData">Information about notes, walls and obstacles</param>
        /// <param name="isRightHand">Right Hand Notes?</param>
        /// <returns></returns>
        internal static SwingData ConfigureSwing(MapSwingContainer curState, MapObjects mapData, List<Note> notes, SwingType type, bool isRightHand)
        {
            // Generate base swing
            bool firstSwing = false;
            if ((isRightHand && curState.RightHandSwings.Count == 0) || (!isRightHand && curState.LeftHandSwings.Count == 0)) firstSwing = true;
            SwingData sData = new SwingData(type, notes, isRightHand, firstSwing);

            // Get previous swing
            SwingData lastSwing = (isRightHand) ?
                curState.RightHandSwings[curState.RightHandSwings.Count - 1] :
                curState.LeftHandSwings[curState.LeftHandSwings.Count - 1];

            // Get last note hit
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
            Note currentNote = notes[0];

            // Get swing EBPM, if reset then double
            sData.swingEBPM = TimeUtils.SwingEBPM(BpmHandler, currentNote.b - lastNote.b);
            if (lastSwing.IsReset) { sData.swingEBPM *= 2; }

            // Work out current player offset
            List<Obstacle> wallsInBetween = mapData.Obstacles.FindAll(x => x.b > lastNote.b && x.b < sData.notes[sData.notes.Count - 1].b);
            sData.playerOffset = CalculatePlayerOffset(sData, wallsInBetween);

            // Get bombs between swings
            List<Bomb> bombsBetweenSwings = mapData.Bombs.FindAll(x => x.b > lastNote.b + 0.01f && x.b < sData.notes[sData.notes.Count - 1].b - 0.01f);

            // Calculate the time since the last note of the last swing, then attempt to determine this swings parity
            float timeSinceLastNote = Math.Abs(currentNote.ms - lastSwing.notes[lastSwing.notes.Count - 1].ms);
            sData.swingParity = ParityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, rH, timeSinceLastNote);

            // Setting Angles
            if (sData.notes.Count == 1)
            {
                if (sData.notes.All(x => x.d == 8))
                { DotCutDirectionCalc(lastSwing, ref sData, true); }
                else
                {
                    if (sData.swingParity == Parity.Backhand)
                    {
                        sData.SetStartAngle(ParityUtils.BackhandDict(rH)[notes[0].d]);
                        sData.SetEndAngle(ParityUtils.BackhandDict(rH)[notes[0].d]);
                    }
                    else
                    {
                        sData.SetStartAngle(ParityUtils.ForehandDict(rH)[notes[0].d]);
                        sData.SetEndAngle(ParityUtils.ForehandDict(rH)[notes[0].d]);
                    }
                }
            }
            else
            {
                // Multi Note Hits
                // If Snapped
                if (sData.notes.All(x => x.b == sData.notes[0].b))
                {
                    if (sData.notes.All(x => x.d == 8)) { SnappedDotSwingAngleCalc(lastSwing, ref sData); }
                    else { SliderAngleCalc(ref sData); }
                }
                else
                {
                    SliderAngleCalc(ref sData);
                }
            }
            return sData;
        }

        #region CORE

        private static Vector2 CalculatePlayerOffset(SwingData sData, List<Obstacle> walls)
        {
            if (walls.Count != 0)
            {
                foreach (Obstacle wall in walls)
                {
                    // Duck wall detection
                    if ((wall.w >= 3 && wall.x <= 1) || (wall.w >= 2 && wall.x == 1))
                    {
                        _playerOffset.Y = -1;
                        _lastDuckTime = wall.b;
                    }

                    // Dodge wall detection
                    if (wall.x == 1 || (wall.x == 0 && wall.w > 1))
                    {
                        _playerOffset.X = 1;
                        _lastDodgeTime = wall.b;
                    }
                    else if (wall.x == 2)
                    {
                        _playerOffset.X = -1;
                        _lastDodgeTime = wall.b;
                    }
                }
            }

            // If time since dodged exceeds a set amount in seconds, undo dodge
            const float undodgeCheckTime = 0.35f;
            if (BpmHandler.ToRealTime(sData.notes[sData.notes.Count - 1].b - _lastDodgeTime) > undodgeCheckTime) { _playerOffset.X = 0; }
            if (BpmHandler.ToRealTime(sData.notes[sData.notes.Count - 1].b - _lastDuckTime) > undodgeCheckTime) { _playerOffset.Y = 0; }
            return _playerOffset;
        }

        #endregion

        #region UTILITY

        /// <summary>
        /// Used to calculate appropriate positioning and angle for non-snapped multi-note swings.
        /// </summary>
        /// <param name="currentSwing">Current Swing being calculated</param>
        internal static void SliderAngleCalc(ref SwingData currentSwing)
        {
            Note firstNote = currentSwing.notes[0];
            Note lastNote = currentSwing.notes[currentSwing.notes.Count-1];
            Note notePriorToLast = currentSwing.notes[currentSwing.notes.Count - 2];
            
            // If arrow, take the cutDir, else approximate direction from first to last note.
            int firstCutDir;
            Vector2 ATB = new Vector2(lastNote.x, lastNote.y) - new Vector2(notePriorToLast.x, notePriorToLast.y);
            ATB = new Vector2(SwingUtils.Clamp((float)Math.Round(ATB.X), -1, 1),
                                SwingUtils.Clamp((float)Math.Round(ATB.Y), -1, 1));
            if (firstNote.d != 8) {
                firstCutDir = firstNote.d;
            } else { firstCutDir = SwingUtils.DirectionalVectorToCutDirection[ATB]; }

            int lastCutDir;
            if (lastNote.d != 8) {
                lastCutDir = lastNote.d;
            } else { lastCutDir = SwingUtils.DirectionalVectorToCutDirection[ATB]; }

            float startAngle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(rH)[firstCutDir] : ParityUtils.BackhandDict(rH)[firstCutDir];
            float endAngle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(rH)[lastCutDir] : ParityUtils.BackhandDict(rH)[lastCutDir];
            currentSwing.SetStartAngle(startAngle);
            currentSwing.SetEndAngle(endAngle);
        }

        /// <summary>
        /// Given a previous swing and current swing (all dot notes), calculate saber rotation.
        /// </summary>
        /// <param name="lastSwing">Swing that came prior to this</param>
        /// <param name="currentSwing">Swing you want to calculate swing angle for</param>
        internal static void SnappedDotSwingAngleCalc(SwingData lastSwing, ref SwingData currentSwing)
        {
            // Get the first and last note based on array order
            Note firstNote = currentSwing.notes[0];
            Note lastNote = currentSwing.notes[currentSwing.notes.Count-1];

            int orientation = SwingUtils.CutDirFromNoteToNote(firstNote, lastNote);
            int altOrientation = SwingUtils.CutDirFromNoteToNote(lastNote, firstNote);

            float angle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(rH)[orientation] : ParityUtils.BackhandDict(rH)[orientation];
            float altAngle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(rH)[altOrientation] : ParityUtils.BackhandDict(rH)[altOrientation];

            float change = lastSwing.endPos.rotation - angle;
            float altChange = lastSwing.endPos.rotation - altAngle;

            // First, try based on angle change either way.
            if (Math.Abs(altChange) < Math.Abs(change)) { angle = altAngle; }
            else if (Math.Abs(altChange) == Math.Abs(change))
            {
                // If the same, attempt based on closest note
                Note lastSwingNote = lastSwing.notes[lastSwing.notes.Count - 1];

                float firstDist = Vector2.Distance(new Vector2(lastSwingNote.x, lastSwingNote.y), new Vector2(firstNote.x, firstNote.y));
                float lastDist = Vector2.Distance(new Vector2(lastSwingNote.x, lastSwingNote.y), new Vector2(lastNote.x, lastNote.y));

                if (Math.Abs(firstDist) < Math.Abs(lastDist)) 
                {
                    angle = altAngle;
                } 
                else if (firstDist == lastDist) 
                {
                    if (Math.Abs(altAngle) < Math.Abs(angle)) { angle = altAngle; }
                }
            }

            if (angle != altAngle)
            {
                currentSwing.notes.Reverse();
                currentSwing.SetStartPosition(lastNote.x, lastNote.y);
                currentSwing.SetEndPosition(firstNote.x, firstNote.y);
            }

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }

        /// <summary>
        /// Given previous and current swing (singular dot note), calculate and clamp saber rotation.
        /// </summary>
        /// <param name="lastSwing">Last swing the player would have done</param>
        /// <param name="currentSwing">Swing you want to calculate swing angle for</param>
        /// <param name="clamp">True if you want to perform clamping on the angle</param>
        internal static void DotCutDirectionCalc(SwingData lastSwing, ref SwingData currentSwing, bool clamp = true)
        {
            Note dotNote = currentSwing.notes[0];
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];

            // If same grid position, just maintain angle
            float angle;
            if (dotNote.x == lastNote.x && dotNote.y == lastNote.y)
            {
                angle = lastSwing.endPos.rotation;
            } else {
                // Get Cut Dir from last note to dot note
                int orientation = SwingUtils.CutDirFromNoteToNote(lastNote, dotNote);
                angle = (lastSwing.swingParity == Parity.Forehand && currentSwing.resetType == ResetType.None) ?
                ParityUtils.ForehandDict(rH)[orientation] :
                ParityUtils.BackhandDict(rH)[orientation];
            }

            if (clamp)
            {
                // If clamp, then apply clamping to the angle based on the ruleset below
                int xDiff = Math.Abs(dotNote.x - lastNote.x);
                int yDiff = Math.Abs(dotNote.y - lastNote.y);
                if (xDiff == 3) { angle = SwingUtils.Clamp(angle, -90, 90); }
                else if (xDiff == 2) { angle = SwingUtils.Clamp(angle, -45, 45); }
                else if (xDiff == 0 && yDiff > 1) { angle = 0; }
                else { angle = SwingUtils.Clamp(angle, -45, 0); }
            }

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }

        /// <summary>
        /// Adds empty, inverse swings for each instance of a Reset in a list of swings.
        /// </summary>
        /// <param name="swings">List of swings to add to</param>
        /// <returns></returns>
        internal static List<SwingData> AddEmptySwingsForResets(List<SwingData> swings)
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

                // Time difference between last swing and current note
                float timeDifference = TimeUtils.BeatToSeconds(BpmHandler.BPM, nextNote.b - lastNote.b);

                SwingData swing = new SwingData();
                swing.swingParity = (currentSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                swing.swingStartBeat = lastSwing.swingEndBeat + Math.Max(TimeUtils.SecondsToBeats(BpmHandler.BPM, timeDifference / 5), 0.2f);
                swing.swingEndBeat = swing.swingStartBeat + 0.1f;
                swing.SetStartPosition(lastNote.x, lastNote.y);
                swing.rightHand = rH;

                // If the last hit was a dot, pick the opposing direction based on parity.
                float diff = currentSwing.startPos.rotation - lastSwing.endPos.rotation;
                float mid = diff / 2;
                mid += lastSwing.endPos.rotation;

                // Set start and end angle, should be the same
                swing.SetStartAngle(mid);
                swing.SetEndAngle(mid);
                swing.SetEndPosition(swing.startPos.x, swing.startPos.y);

                result.Insert(i + swingsAdded, swing);
                swingsAdded++;
            }
            return result;
        }

        #endregion
    }
}