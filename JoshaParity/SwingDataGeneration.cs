using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

/// Notes
/// Currently everything parity related hinges on this classes' state for which hand as it calls
/// the Forehand and Backhand Dicts. Would like to change this so its done inside an object, not
/// inside a static class. Will also allow lean and easier checking as well as live feeding in of
/// map data. Something akin to:
/// - Feed object mapData as it happens
/// - Runs the swing contents detection
/// - When done with that, add swing to list

namespace JoshaParity
{
    /// <summary>
    /// Current Orientation States for any given hand.
    /// </summary>
    public enum Parity
    {
        Forehand,
        Backhand
    }

    /// <summary>
    /// Swing Reset Type.
    /// </summary>
    public enum ResetType
    {
        None = 0,  // Swing does not force a reset, or triangle
        Bomb = 1,    // Swing forces a reset due to bombs
        Rebound = 2,   // Swing forces an additional swing
    }

    /// <summary>
    /// Contains position and rotation information for a given swing.
    /// </summary>
    public struct PositionData
    {
        public float x;
        public float y;
        public float rotation;
    }

    /// <summary>
    /// Pair of notes used for comparisons in LINQ
    /// </summary>
    public class NotePair
    {
        public Note noteA = new Note();
        public Note noteB = new Note();
    }

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
    /// Contains data for a given swing.
    /// </summary>
    public struct SwingData
    {
        public Parity swingParity;
        public ResetType resetType;
        public float swingStartBeat;
        public float swingEndBeat;
        public float swingEBPM;
        public List<Note> notes = new();
        public PositionData startPos;
        public PositionData endPos;
        public bool rightHand;
        public Vector2 playerOffset;

        public SwingData()
        {
            swingParity = Parity.Forehand;
            resetType = ResetType.None;
            swingStartBeat = 0;
            swingEndBeat = 0;
            swingEBPM = 0;
            notes = new List<Note>();
            startPos = new PositionData();
            endPos = new PositionData();
            rightHand = true;
            playerOffset = Vector2.Zero;
        }

        public void SetStartPosition(float x, float y) { startPos.x = x; startPos.y = y; }
        public void SetEndPosition(float x, float y) { endPos.x = x; endPos.y = y; }
        public void SetStartAngle(float angle) { startPos.rotation = angle; }
        public void SetEndAngle(float angle) { endPos.rotation = angle; }
        public bool IsReset => resetType != 0;

        public override string ToString()
        {
            string returnString = $"Swing Note/s or Bomb/s {swingStartBeat} " +
                                  $"| Parity of this swing: {swingParity}" + " | AFN: " + startPos.rotation +
                $"\nPlayer Offset: {playerOffset.X}x {playerOffset.Y}y | " +
                $"Swing EBPM: {swingEBPM} | Reset Type: {resetType}";
            return returnString;
        }
    }

    /// <summary>
    /// Functionality for generating swing data about a given difficulty.
    /// </summary>
    public static class SwingDataGeneration
    {
        // 0 - Up hit 1 - Down hit 2 - Left Hit 3 - Right Hit
        // 4 - Up Left 5 - Up Right - 6 Down Left 7 - Down Right 8 - Any
        #region Parity Dictionaries

        // RIGHT HAND PARITY DICTIONARIES
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
        private static readonly Dictionary<int, float> RightForehandDict = new Dictionary<int, float>()
        { { 0, -180 }, { 1, 0 }, { 2, -90 }, { 3, 90 }, { 4, -135 }, { 5, 135 }, { 6, -45 }, { 7, 45 }, { 8, 0 } };
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
        private static readonly Dictionary<int, float> RightBackhandDict = new Dictionary<int, float>()
        { { 0, 0 }, { 1, -180 }, { 2, 90 }, { 3, -90 }, { 4, 45 }, { 5, -45 }, { 6, 135 }, { 7, -135 }, { 8, 0 } };

        // LEFT HAND PARITY DICTIONARIES
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
        private static readonly Dictionary<int, float> LeftForehandDict = new Dictionary<int, float>()
        { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
        private static readonly Dictionary<int, float> LeftBackhandDict = new Dictionary<int, float>()
        { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };

        public static Dictionary<int, float> ForehandDict => (_rightHand) ? RightForehandDict : LeftForehandDict;
        public static Dictionary<int, float> BackhandDict => (_rightHand) ? RightBackhandDict : LeftBackhandDict;

        #endregion

        #region Variables

        private static IParityMethod ParityMethodology = new GenericParityCheck();
        private static BPMHandler _bpmHandler = BPMHandler.CreateBPMHandler(0,new(),0);
        private static bool _rightHand = true;
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
            // Reset Operating Variables
            _bpmHandler = BPMHandler;
            _playerOffset = Vector2.Zero;
            _lastDodgeTime = 0; _lastDuckTime = 0;

            // Separate notes, bombs, walls and burst sliders
            List<Note> notes = new List<Note>(mapDif.DifficultyData.colorNotes.ToList());
            List<Bomb> bombs = new List<Bomb>(mapDif.DifficultyData.bombNotes.ToList());
            List<Obstacle> walls = new List<Obstacle>(mapDif.DifficultyData.obstacles.ToList());
            List<BurstSlider> burstSliders = new List<BurstSlider>(mapDif.DifficultyData.burstSliders.ToList());

            // Convert burst sliders to pseudo-notes
            notes.AddRange(burstSliders.Select(slider => new Note() { x = slider.x, y = slider.y, c = slider.c, d = slider.d, b = slider.b }));
            notes.AddRange(burstSliders.Select(slider => new Note() { x = slider.tx, y = slider.ty, c = slider.c, d = 8, b = slider.tb }));
            notes = notes.OrderBy(x => x.b).ToList();

            // Calculate swing data for both hands
            MapObjects mapData = new MapObjects(notes, bombs, walls);
            List<SwingData> rightHandSD = GetSwingData(mapData, true);
            List<SwingData> leftHandSD = GetSwingData(mapData, false);

            // Combine swing data and sort
            List<SwingData> combinedSD = new List<SwingData>(rightHandSD);
            combinedSD.AddRange(leftHandSD);
            combinedSD = combinedSD.OrderBy(x => x.swingStartBeat).ToList();
            return combinedSD;
        }

        /// <summary>
        /// Calculates and returns a list of swing data for a set of map objects.
        /// </summary>
        /// <param name="mapData">Information about notes, walls and obstacles</param>
        /// <param name="isRightHand">Right Hand Notes?</param>
        /// <returns></returns>
        internal static List<SwingData> GetSwingData(MapObjects mapData, bool isRightHand)
        {
            // Set hand, Initialize result and objects
            MapObjects mapObjects = new MapObjects(mapData.Notes, mapData.Bombs, mapData.Obstacles);
            List<SwingData> result = new List<SwingData>();
            _rightHand = isRightHand;
            mapObjects.Notes.RemoveAll(x => _rightHand ? x.c == 0 : x.c == 1);

            // Catch the event there is 0 notes
            if (mapObjects.Notes.Count == 0) { return new List<SwingData>(); }

            // Slider precision, initialise list to hold notes for this swing
            const float sliderPrecision = 59f; // In miliseconds
            List<Note> notesInSwing = new List<Note>();

            // Attempt to find the notes for constructing this swing
            for (int i = 0; i <= mapObjects.Notes.Count - 1; i++)
            {
                Note currentNote = mapObjects.Notes[i];
                _bpmHandler.SetCurrentBPM(currentNote.b);
                float beatMS = 60 * 1000 / _bpmHandler.BPM;

                #region Swing Composition

                // If not the last note, check if slider or stack
                if (i != mapObjects.Notes.Count - 1)
                {
                    Note nextNote = mapObjects.Notes[i + 1];
                    notesInSwing.Add(currentNote);

                    // If ms precision falls under "Slider", or timestamp is the same, tis a slider
                    float currentNoteMS = currentNote.b * beatMS;
                    float nextNoteMS = nextNote.b * beatMS;
                    float timeDiff = Math.Abs(currentNoteMS - nextNoteMS);
                    if (timeDiff <= sliderPrecision)
                    {
                        if (nextNote.d == 8 || notesInSwing[notesInSwing.Count-1].d == 8 ||
                            currentNote.d == nextNote.d || Math.Abs(ForehandDict[currentNote.d] - ForehandDict[nextNote.d]) <= 45 ||
                             Math.Abs(BackhandDict[currentNote.d] - BackhandDict[nextNote.d]) <= 45)
                        { continue; }
                    }
                }
                else
                {
                    notesInSwing.Add(currentNote);
                }

                #endregion

                // Attempt to sort snapped swing if not all dots
                if (notesInSwing.Count > 1 && notesInSwing.All(x => x.b == notesInSwing[0].b)) notesInSwing = SnappedSwingSort(notesInSwing);

                // Assume by default swinging forehanded
                SwingData sData = new SwingData();
                sData.notes = new List<Note>(notesInSwing);
                sData.swingParity = Parity.Forehand;
                sData.swingStartBeat = sData.notes[0].b;
                sData.swingEndBeat = sData.notes[sData.notes.Count-1].b;
                sData.rightHand = isRightHand;
                sData.SetStartPosition(sData.notes[0].x, sData.notes[0].y);
                sData.SetEndPosition(sData.notes[sData.notes.Count - 1].x, sData.notes[sData.notes.Count - 1].y);

                // If first swing, check if potentially upswing start based on orientation
                if (result.Count == 0)
                {
                    if (currentNote.d == 0 || currentNote.d == 4 || currentNote.d == 5)
                    {
                        sData.swingParity = Parity.Backhand;
                        sData.SetStartAngle(BackhandDict[sData.notes[0].d]);
                        sData.SetEndAngle(BackhandDict[sData.notes[sData.notes.Count - 1].d]);
                    }
                    else
                    {
                        sData.SetStartAngle(ForehandDict[sData.notes[0].d]);
                        sData.SetEndAngle(ForehandDict[sData.notes[sData.notes.Count - 1].d]);
                    }
                    result.Add(sData);
                    notesInSwing.Clear();
                    continue;
                }

                // Get previous swing
                SwingData lastSwing = result[result.Count-1];
                Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];

                // Get swing EBPM, if reset then double
                sData.swingEBPM = TimeUtils.SwingEBPM(_bpmHandler, currentNote.b - lastNote.b);
                if (lastSwing.IsReset) { sData.swingEBPM *= 2; }

                // Work out current player XOffset for bomb calculations
                List<Obstacle> wallsInBetween = mapObjects.Obstacles.FindAll(x => x.b > lastNote.b && x.b < sData.notes[sData.notes.Count - 1].b);
                if (wallsInBetween.Count != 0)
                {
                    foreach (Obstacle wall in wallsInBetween)
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
                if (_bpmHandler.ToRealTime(sData.notes[sData.notes.Count - 1].b - _lastDodgeTime) > undodgeCheckTime) { _playerOffset.X = 0; }
                if (_bpmHandler.ToRealTime(sData.notes[sData.notes.Count - 1].b - _lastDuckTime) > undodgeCheckTime) { _playerOffset.Y = 0; }
                sData.playerOffset = _playerOffset;

                // Get bombs between swings
                List<Bomb> bombsBetweenSwings = mapObjects.Bombs.FindAll(x => x.b > lastNote.b + 0.01f && x.b < sData.notes[sData.notes.Count - 1].b - 0.01f);

                // Calculate the time since the last note of the last swing, then attempt to determine this swings parity
                float timeSinceLastNote = Math.Abs(currentNote.b * beatMS - lastSwing.notes[lastSwing.notes.Count - 1].b * beatMS);
                sData.swingParity = ParityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, _rightHand, timeSinceLastNote);

                // Setting Angles
                if (sData.notes.Count == 1)
                {
                    if (sData.notes.All(x => x.d == 8))
                    { DotCutDirectionCalc(lastSwing, ref sData, true); } 
                    else
                    {
                        if (sData.swingParity == Parity.Backhand)
                        {
                            sData.SetStartAngle(BackhandDict[notesInSwing[0].d]);
                            sData.SetEndAngle(BackhandDict[notesInSwing[0].d]);
                        } else {
                            sData.SetStartAngle(ForehandDict[notesInSwing[0].d]);
                            sData.SetEndAngle(ForehandDict[notesInSwing[0].d]);
                        }
                    }
                } else {
                    // Multi Note Hits
                    // If Snapped
                    if (sData.notes.All(x => x.b == sData.notes[0].b))
                    {
                        if (sData.notes.All(x => x.d == 8)) { SnappedDotSwingAngleCalc(lastSwing, ref sData); }
                        else { SliderAngleCalc(ref sData); }
                    } else {
                        SliderAngleCalc(ref sData);
                    }
                }

                // Because the parity dictionaries go from -180 to 135 and I haven't implemented proper lean detection,
                // If upside-down is deemed to be true in the parity check then flip the angle
                // NOTICE: Will be reworked when attempting to add lean detection and positional checks for parity
                if (ParityMethodology.UpsideDown == true)
                {
                    if (sData.notes.All(x => x.d != 8))
                    {
                        sData.SetStartAngle(sData.startPos.rotation * -1);
                        sData.SetEndAngle(sData.endPos.rotation * -1);
                    }
                }

                // Add swing to list
                result.Add(sData);
                notesInSwing.Clear();
            }

            // Adds opposing parity swings for each instance of a reset
            result = AddEmptySwingsForResets(result);
            return result;
        }

        #region DOT UTILITY & BOMB AVOIDANCE

        /// <summary>
        /// Used to sort notes in a swing where they are snapped to the same beat, and not all dots
        /// </summary>
        /// <param name="notesToSort">List of notes you want to sort</param>
        /// <returns></returns>
        internal static List<Note> SnappedSwingSort(List<Note> notesToSort)
        {
            // Find the two notes that are furthest apart and their positions
            NotePair farNotes = SwingUtils.FurthestNotesFromList(notesToSort);
            Vector2 noteAPos = new Vector2(farNotes.noteA.x, farNotes.noteA.y);
            Vector2 noteBPos = new Vector2(farNotes.noteB.x, farNotes.noteB.y);

            // Get the direction vector ATB
            // Check if any cut directions oppose this, if so, flip to BTA
            Vector2 atb = noteBPos - noteAPos;
            if (notesToSort.Any(x => x.d != 8))
            {
                bool reverseOrder = notesToSort.Any(note => note.d != 8 && Vector2.Dot(SwingUtils.DirectionalVectors[note.d], atb) < 0);
                if (reverseOrder) atb = -atb;
            }

            // Sort the cubes according to their position along the direction vector
            List<Note> sortedNotes = notesToSort.OrderBy(note => Vector2.Dot(new Vector2(note.x, note.y) - noteAPos, atb)).ToList();
            return sortedNotes;
        }

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

            float startAngle = (currentSwing.swingParity == Parity.Forehand) ? ForehandDict[firstCutDir] : BackhandDict[firstCutDir];
            float endAngle = (currentSwing.swingParity == Parity.Forehand) ? ForehandDict[lastCutDir] : BackhandDict[lastCutDir];
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

            float angle = (currentSwing.swingParity == Parity.Forehand) ? ForehandDict[orientation] : BackhandDict[orientation];
            float altAngle = (currentSwing.swingParity == Parity.Forehand) ? ForehandDict[altOrientation] : BackhandDict[altOrientation];

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
                ForehandDict[orientation] :
                BackhandDict[orientation];
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
                float timeDifference = TimeUtils.BeatToSeconds(_bpmHandler.BPM, nextNote.b - lastNote.b);

                SwingData swing = new SwingData();
                swing.swingParity = (currentSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                swing.swingStartBeat = lastSwing.swingEndBeat + Math.Max(TimeUtils.SecondsToBeats(_bpmHandler.BPM, timeDifference / 5), 0.2f);
                swing.swingEndBeat = swing.swingStartBeat + 0.1f;
                swing.SetStartPosition(lastNote.x, lastNote.y);
                swing.rightHand = _rightHand;

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