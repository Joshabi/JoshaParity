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
            mainContainer.playerOffset = Vector2.Zero;
            mainContainer.lastDodgeTime = 0; mainContainer.lastDuckTime = 0;
            mainContainer = new();

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
            swings.AddRange(AddEmptySwingsForResets(finishedState.RightHandSwings));
            swings.AddRange(AddEmptySwingsForResets(finishedState.LeftHandSwings));

            swings = swings.OrderBy(x => x.swingStartBeat).ToList();
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

            int lastLeftIndex = mapObjects.Notes.FindLastIndex(x => x.c == 0);
            int lastRightIndex = mapObjects.Notes.FindLastIndex(x => x.c == 1);

            // Foreach note going forwards
            for (int i = 0; i < mapObjects.Notes.Count; i++)
            {
                Note currentNote = mapObjects.Notes[i];
                currentNote = SwingUtils.ValidateNote(currentNote);

                // Set Ms
                BpmHandler.SetCurrentBPM(currentNote.b);
                float beatMS = 60 * 1000 / BpmHandler.BPM;
                currentNote.ms = beatMS * currentNote.b;

                // Depending on hand, update buffer
                if (currentNote.c == 0) { 
                    (SwingType leftSwingType, List<Note> leftNotesInSwing) = curState.leftHandConstructor.UpdateBuffer(currentNote);
                    if (leftSwingType != SwingType.Undecided) {
                        if (leftNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapData, leftNotesInSwing, leftSwingType, false), false);
                    }
                }
                else if (currentNote.c == 1) { 
                    (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.UpdateBuffer(currentNote);
                    if (rightSwingType != SwingType.Undecided) {
                        if (rightNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapData, rightNotesInSwing, rightSwingType, true), true);
                    }
                }
                
                // If this is the last swing for either hand we need to clear out the remainder of the buffer
                // This fixes the behaviour for now and it acts like it did before. May change it to just be a version
                // of update buffer without a note, then an IF earlier to remove duplicate code.
                if (i == lastLeftIndex) { 
                    curState.leftHandConstructor.EndBuffer();
                    (SwingType leftSwingType, List<Note> leftNotesInSwing) = curState.leftHandConstructor.UpdateBuffer(currentNote);
                    if (leftSwingType != SwingType.Undecided)
                    {
                        if (leftNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapData, leftNotesInSwing, leftSwingType, false), false);
                    }
                }

                if (i == lastRightIndex) { 
                    curState.rightHandConstructor.EndBuffer();
                    (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.UpdateBuffer(currentNote);
                    if (rightSwingType != SwingType.Undecided)
                    {
                        if (rightNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapData, rightNotesInSwing, rightSwingType, true), true);
                    }
                }
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

            // If first we leave
            if (firstSwing) return sData;

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
            sData.playerOffset = CalculatePlayerOffset(curState, currentNote, wallsInBetween);

            // Get bombs between swings
            List<Bomb> bombsBetweenSwings = mapData.Bombs.FindAll(x => x.b > lastNote.b + 0.01f && x.b < sData.notes[sData.notes.Count - 1].b - 0.01f);

            // Calculate the time since the last note of the last swing, then attempt to determine this swings parity
            float timeSinceLastNote = Math.Abs(currentNote.ms - lastSwing.notes[lastSwing.notes.Count - 1].ms);
            sData.swingParity = ParityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, isRightHand, timeSinceLastNote);

            // Setting Angles
            if (sData.notes.Count == 1)
            {
                if (sData.notes.All(x => x.d == 8))
                { SwingUtils.DotCutDirectionCalc(lastSwing, ref sData, true); }
                else
                {
                    if (sData.swingParity == Parity.Backhand)
                    {
                        sData.SetStartAngle(ParityUtils.BackhandDict(isRightHand)[notes[0].d]);
                        sData.SetEndAngle(ParityUtils.BackhandDict(isRightHand)[notes[0].d]);
                    }
                    else
                    {
                        sData.SetStartAngle(ParityUtils.ForehandDict(isRightHand)[notes[0].d]);
                        sData.SetEndAngle(ParityUtils.ForehandDict(isRightHand)[notes[0].d]);
                    }
                }
            }
            else
            {
                // Multi Note Hits
                // If Snapped
                if (sData.notes.All(x => x.b == sData.notes[0].b))
                {
                    if (sData.notes.All(x => x.d == 8)) { SwingUtils.SnappedDotSwingAngleCalc(lastSwing, ref sData); }
                    else { SwingUtils.SliderAngleCalc(ref sData); }
                }
                else
                {
                    SwingUtils.SliderAngleCalc(ref sData);
                }
            }
            // Temporary, will be replaced with new lean system once implemented.
            if (ParityMethodology.UpsideDown == true)
            {
                if (sData.notes.All(x => x.d != 8))
                {
                    sData.SetStartAngle(sData.startPos.rotation * -1);
                    sData.SetEndAngle(sData.endPos.rotation * -1);
                }
            }
            return sData;
        }

        /// <summary>
        /// Calculates wall offset
        /// </summary>
        /// <param name="curState"></param>
        /// <param name="currentNote"></param>
        /// <param name="walls"></param>
        /// <returns></returns>
        private static Vector2 CalculatePlayerOffset(MapSwingContainer curState, Note currentNote, List<Obstacle> walls)
        {
            if (walls.Count != 0)
            {
                foreach (Obstacle wall in walls)
                {
                    // Duck wall detection
                    if ((wall.w >= 3 && wall.x <= 1) || (wall.w >= 2 && wall.x == 1))
                    {
                        curState.playerOffset.Y = -1;
                        curState.lastDuckTime = wall.b;
                    }

                    // Dodge wall detection
                    if (wall.x == 1 || (wall.x == 0 && wall.w > 1))
                    {
                        curState.playerOffset.X = 1;
                        curState.lastDodgeTime = wall.b;
                    }
                    else if (wall.x == 2)
                    {
                        curState.playerOffset.X = -1;
                        curState.lastDodgeTime = wall.b;
                    }
                }
            }

            // If time since dodged exceeds a set amount in seconds, undo dodge
            const float undodgeCheckTime = 0.35f;
            if (BpmHandler.ToRealTime(currentNote.b - curState.lastDodgeTime) > undodgeCheckTime) { curState.playerOffset.X = 0; }
            if (BpmHandler.ToRealTime(currentNote.b - curState.lastDuckTime) > undodgeCheckTime) { curState.playerOffset.Y = 0; }
            return curState.playerOffset;
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
                swing.rightHand = swings[0].rightHand;

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
    }
}