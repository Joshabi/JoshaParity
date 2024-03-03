using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Contains map entities (Bombs, Walls, Notes)
    /// </summary>
    public class MapObjects
    {
        public List<Note> Notes { get; set; }
        public List<Bomb> Bombs { get; }
        public List<Obstacle> Obstacles { get; }

        /// <summary>
        /// Creates a new, clean copy of MapObjects
        /// </summary>
        /// <param name="notes">Notes</param>
        /// <param name="bombs">Bombs</param>
        /// <param name="walls">Walls</param>
        public MapObjects(List<Note> notes, List<Bomb> bombs, List<Obstacle> walls)
        {
            Notes = new List<Note>(notes);
            Bombs = new List<Bomb>(bombs);
            Obstacles = new List<Obstacle>(walls);
        }
    }

    /// <summary>
    /// Functionality for generating swing data about a given difficulty
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
        /// Called to check a specific map difficulty
        /// </summary>
        /// <param name="mapDif">Map Difficulty to check</param>
        /// <param name="BPMHandler">BPM Handler for the difficulty containing base BPM and BPM Changes</param>
        /// <param name="parityMethod">Optional: Parity Check Logic</param>
        public static MapSwingContainer Run(MapData mapDif, BPMHandler BPMHandler, IParityMethod? parityMethod = null)
        {
            ParityMethodology = parityMethod ??= new GenericParityCheck();
            BpmHandler = BPMHandler;
            mainContainer = new();

            // Separate notes, bombs, walls and burst sliders
            List<Note> notes = new List<Note>(mapDif.DifficultyData.colorNotes.ToList());
            List<Bomb> bombs = new List<Bomb>(mapDif.DifficultyData.bombNotes.ToList());
            List<Obstacle> walls = new List<Obstacle>(mapDif.DifficultyData.obstacles.ToList());
            List<BurstSlider> burstSliders = new List<BurstSlider>(mapDif.DifficultyData.burstSliders.ToList());

            // Convert burst sliders to pseudo-notes
            notes.AddRange(burstSliders);
            notes = notes.OrderBy(x => x.b).ToList();

            // Set MS values for notes
            foreach (Note note in notes) {
                float seconds = BpmHandler.ToRealTime(note.b);
                note.ms = seconds * 1000;
            }

            // Calculate swing data for both hands
            _mapData = new MapObjects(notes, bombs, walls);
            MapSwingContainer finishedState = SimulateSwings(mainContainer, _mapData);
            finishedState.SetPlayerOffsetData(CalculateOffsetData(_mapData.Obstacles));
            return finishedState;
        }

        /// <summary>
        /// Simulates swings for a given state and map data
        /// </summary>
        /// <param name="curState">Current swing container state</param>
        /// <param name="mapData">Notes, obstacles and walls</param>
        /// <returns></returns>
        public static MapSwingContainer SimulateSwings(MapSwingContainer curState, MapObjects mapData) {

            // Reference Fix and Remove Prior Notes
            MapObjects mapObjects = new(mapData.Notes, mapData.Bombs, mapData.Obstacles);
            mapObjects.Notes.RemoveAll(x => x.ms < curState.timeValue);
            if (mapObjects.Notes.Count == 0) { return curState; }

            int lastLeftIndex = mapObjects.Notes.FindLastIndex(x => x.c == 0);
            int lastRightIndex = mapObjects.Notes.FindLastIndex(x => x.c == 1);

            // Foreach note going forwards
            for (int i = 0; i < mapObjects.Notes.Count; i++)
            {
                Note currentNote = mapObjects.Notes[i];
                currentNote = SwingUtils.ValidateNote(currentNote);

                // Depending on hand, update buffer
                if (currentNote.c == 0) { 
                    (SwingType leftSwingType, List<Note> leftNotesInSwing) = curState.leftHandConstructor.UpdateBuffer(currentNote);
                    if (leftSwingType != SwingType.Undecided) {
                        if (leftNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, leftNotesInSwing, leftSwingType, false), false);
                    }
                }
                else if (currentNote.c == 1) { 
                    (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.UpdateBuffer(currentNote);
                    if (rightSwingType != SwingType.Undecided) {
                        if (rightNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, rightNotesInSwing, rightSwingType, true), true);
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
                        if (leftNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, leftNotesInSwing, leftSwingType, false), false);
                    }
                }

                if (i == lastRightIndex) { 
                    curState.rightHandConstructor.EndBuffer();
                    (SwingType rightSwingType, List<Note> rightNotesInSwing) = curState.rightHandConstructor.UpdateBuffer(currentNote);
                    if (rightSwingType != SwingType.Undecided)
                    {
                        if (rightNotesInSwing.Count != 0) curState.AddSwing(ConfigureSwing(curState, mapObjects, rightNotesInSwing, rightSwingType, true), true);
                    }
                }
            }

            return curState;
        }

        /// <summary>
        /// Calculates and returns a list of swing data for a set of map objects
        /// </summary>
        /// <param name="curState">Current swing container state</param>
        /// <param name="mapData">Information about notes, walls and obstacles</param>
        /// <param name="notes">Notes in swing</param>
        /// <param name="type">Type of swing</param>
        /// <param name="isRightHand">Right Hand Notes?</param>
        /// <returns></returns>
        internal static SwingData ConfigureSwing(MapSwingContainer curState, MapObjects mapData, List<Note> notes, SwingType type, bool isRightHand)
        {
            // Generate base swing
            bool firstSwing = false;
            if ((isRightHand && curState.RightHandSwings.Count == 0) || (!isRightHand && curState.LeftHandSwings.Count == 0)) firstSwing = true;
            SwingData sData = new SwingData(type, notes, isRightHand, firstSwing);
            sData.swingStartSeconds = BpmHandler.ToRealTime(sData.swingStartBeat);
            sData.swingEndSeconds = BpmHandler.ToRealTime(sData.swingEndBeat);

            // If first we leave
            if (firstSwing) return sData;

            // Get previous swing
            SwingData lastSwing = (isRightHand) ?
                curState.RightHandSwings[curState.RightHandSwings.Count - 1] :
                curState.LeftHandSwings[curState.LeftHandSwings.Count - 1];

            // Get swing EBPM
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
            Note currentNote = sData.notes[0];
            sData.swingEBPM = TimeUtils.SwingEBPM(BpmHandler, lastNote.b, currentNote.b);
            if (lastSwing.IsReset) { sData.swingEBPM *= 2; }

            // Calculate Parity
            sData.swingParity = ParityMethodology.ParityCheck(ref sData, new ParityCheckContext(curState, mapData));

            // Dont need to do angle calc for chains
            if (sData.swingType == SwingType.Chain) return sData;

            // Setting angles for: Single-Note Swings
            if (sData.notes.Count == 1) {
                if (sData.notes.All(x => x.d == 8)) { 
                    SwingUtils.DotCutDirectionCalc(lastSwing, ref sData, true); 
                } else {
                    // Get Parity Dictionary
                    var parityDict = (sData.swingParity == Parity.Backhand) ?
                        ParityUtils.BackhandDict(isRightHand) : ParityUtils.ForehandDict(isRightHand);

                    sData.SetStartAngle(parityDict[sData.notes[0].d]);
                    sData.SetEndAngle(parityDict[sData.notes[0].d]);
                }
            } else {
                // Setting angles for: Multi-note Snapped Swings
                if (sData.notes.All(x => Math.Abs(sData.notes[0].b) - x.b < 0.01f)) {
                    // Snapped all dots, else:
                    if (sData.notes.All(x => x.d == 8)) { 
                        SwingUtils.SnappedDotSwingAngleCalc(lastSwing, ref sData); 
                    } else { 
                        SwingUtils.SliderAngleCalc(ref sData); 
                    }
                } else {
                    SwingUtils.SliderAngleCalc(ref sData);
                }
            }

            // Temporary Angle Flip till lean is fully implemented:
            if (sData.upsideDown)
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
        /// Returns the Player Offset a wall influences
        /// </summary>
        /// <param name="obstacle"></param>
        /// <returns></returns>
        private static Vector2 WallImpactAssess(Obstacle obstacle, Obstacle lastObstacle)
        {
            Vector2 returnVec = Vector2.Zero;

            if ((obstacle.w >= 3 && obstacle.x <= 1) || (obstacle.w >= 2 && obstacle.x == 1))
            {
                returnVec.Y = -0.7f;  // Duck
            }
            else if ((obstacle.x == 1 || (obstacle.x == 0 && obstacle.w > 1)) && (lastObstacle.x == 2) && (obstacle.b + obstacle.d) - (lastObstacle.b + lastObstacle.d) < 0.5f)
            {
                return new(0,-0.7f);  // Duck
            }
            else if ((obstacle.x == 2) && (lastObstacle.x == 1 || (lastObstacle.x == 0 && lastObstacle.w > 1)) && (obstacle.b + obstacle.d) - (lastObstacle.b + lastObstacle.d) < 0.5f)
            {
                return new(0, -0.7f);  // Duck
            }

            if ((obstacle.x == 1 && obstacle.w <= 1) || (obstacle.x == 0 && obstacle.w == 2)) {
                returnVec.X = 0.55f;  // Dodge Right
            }
            else if (obstacle.x == 2)
            {
                returnVec.X = -0.55f;  // Dodge Left
            }

            return returnVec;
        }

        /// <summary>
        /// Given some obstacles, return all player head avoidance data for the map
        /// </summary>
        /// <param name="obstacles">List of V3 obstacles</param>
        /// <returns></returns>
        private static List<OffsetData> CalculateOffsetData(List<Obstacle> obstacles)
        {
            List<OffsetData> offsetData = new List<OffsetData>();
            Obstacle lastInteractive = new();

            // Old Method:
            foreach (Obstacle obstacle in obstacles) {
                Vector2 pOffset = WallImpactAssess(obstacle, lastInteractive);
                if (pOffset == Vector2.Zero && obstacle.b < lastInteractive.b + lastInteractive.d) { continue; }
                if (obstacle.b > lastInteractive.b + lastInteractive.d + 1f) { offsetData.Add(new() { timeValue = lastInteractive.b + lastInteractive.d + 1f, offsetValue = new(0, 0) }); }
                lastInteractive = obstacle;
                offsetData.Add(new() { timeValue = obstacle.b, offsetValue = pOffset });
                offsetData.Add(new() { timeValue = obstacle.b + obstacle.d, offsetValue = pOffset });
            }

            offsetData.OrderBy(x => x.timeValue);

            // Apply modifications to offsetData to maintain ducks (prevents overlap issues as frequent)
            for (int i = 0; i < offsetData.Count; i++)
            {
                // If not ducking we dont care
                if (offsetData[i].offsetValue.Y >= 0) { continue; }

                for (int j = i + 1; j < offsetData.Count; j++)
                {
                    if (offsetData[j].timeValue > offsetData[i].timeValue + 0.35f) { break; }
                    if (offsetData[j].offsetValue.Y >= 0) { offsetData[j].offsetValue.Y = offsetData[i].offsetValue.Y; }
                }
            }

            return offsetData;
        }
    }
}