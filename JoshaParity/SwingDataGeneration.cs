﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
        public float playerHorizontalOffset;
        public float playerVerticalOffset;

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
            playerHorizontalOffset = 0;
            playerVerticalOffset = 0;
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
                $"\nPlayer Offset: {playerHorizontalOffset}x {playerVerticalOffset}y | " +
                $"Swing EBPM: {swingEBPM} | Reset Type: {resetType}";
            return returnString;
        }
    }

    /// <summary>
    /// Functionality for generating swing data about a given difficulty.
    /// </summary>
    public static class SwingDataGeneration
    {

        #region Parity and Orientation Dictionaries

        // 0 - Up hit 1 - Down hit 2 - Left Hit 3 - Right Hit
        // 4 - Up Left 5 - Up Right - 6 Down Left 7 - Down Right 8 - Any

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

        public static readonly Dictionary<int, int> opposingCutDict = new Dictionary<int, int>()
        { { 0, 1 }, { 1, 0 }, { 2, 3 }, { 3, 2 }, { 5, 7 }, { 7, 5 }, { 4, 6 }, { 6, 4 } };

        public static Dictionary<int, float> ForehandDict => (_rightHand) ? RightForehandDict : LeftForehandDict;
        public static Dictionary<int, float> BackhandDict => (_rightHand) ? RightBackhandDict : LeftBackhandDict;

        // Contains a list of directional vectors
        public static readonly Vector2[] DirectionalVectors =
        {
            new Vector2(0, 1),   // up
            new Vector2(0, -1),  // down
            new Vector2(-1, 0),  // left
            new Vector2(1, 0),   // right
            new Vector2(1, 1),   // up right
            new Vector2(-1, 1),  // up left
            new Vector2(-1, -1), // down left
            new Vector2(1, -1)  // down right
        };

        // Converts a direction vector into a cut direction
        public static readonly Dictionary<Vector2, int> DirectionalVectorToCutDirection = new Dictionary<Vector2, int>()
        {
            { new Vector2(0, 1), 0 },
            { new Vector2(0, -1), 1 },
            { new Vector2(-1, 0), 2 },
            { new Vector2(1, 0), 3 },
            { new Vector2(1, 1), 5 },
            { new Vector2(-1, 1), 4 },
            { new Vector2(-1, -1), 6 },
            { new Vector2(1, -1), 7 },
            { new Vector2(0, 0), 8 }
        };

        #endregion

        #region Variables

        private static IParityMethod ParityMethodology = new GenericParityCheck();
        private static float _bpm;
        private static bool _rightHand = true;
        private static int _playerXOffset = 0;
        private static int _playerYOffset = 0;
        private static float _lastDodgeTime;
        private static float _lastDuckTime;

        #endregion

        /// <summary>
        /// Called to check a specific map difficulty.
        /// </summary>
        /// <param name="mapDif">Map Difficulty to check</param>
        /// <param name="bpm">BPM of the map</param>
        /// <param name="parityMethod">Optional: Parity Check Logic</param>
        public static List<SwingData> Run(MapData mapDif, float bpm, IParityMethod? parityMethod = null)
        {
            ParityMethodology = parityMethod ??= new GenericParityCheck();
            // Reset Operating Variables
            _bpm = bpm;
            _playerXOffset = 0;
            _playerYOffset = 0;

            // Separate notes, bombs, walls and burst sliders
            List<Note> notes = new List<Note>(mapDif.DifficultyData.colorNotes.ToList());
            List<Bomb> bombs = new List<Bomb>(mapDif.DifficultyData.bombNotes.ToList());
            List<Obstacle> walls = new List<Obstacle>(mapDif.DifficultyData.obstacles.ToList());
            List<BurstSlider> burstSliders = new List<BurstSlider>(mapDif.DifficultyData.burstSliders.ToList());

            // Convert burst sliders to pseudo-notes
            notes.AddRange(burstSliders.Select(slider => new Note() { x = slider.x, y = slider.y, c = slider.c, d = slider.d, b = slider.b }));
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
            MapObjects mapObjects = new MapObjects(mapData.Notes, mapData.Bombs, mapData.Obstacles);
            List<SwingData> result = new List<SwingData>();
            _rightHand = isRightHand;

            // Remove notes for the opposite hand
            mapObjects.Notes.RemoveAll(x => _rightHand ? x.c == 0 : x.c == 1);

            // Catch the event there is 0 notes
            if (mapObjects.Notes.Count == 0) { return new List<SwingData>(); }

            // Slider precision, initialise list to hold notes for this swing
            const float sliderPrecision = 59f; // In miliseconds
            float beatMS = 60 * 1000 / _bpm;
            List<Note> notesInSwing = new List<Note>();

            // Attempt to find the notes for constructing this swing
            for (int i = 0; i <= mapObjects.Notes.Count - 1; i++)
            {
                Note currentNote = mapObjects.Notes[i];

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

                // Re-order the notes if all notes are on the same snap and not dots
                if (notesInSwing.All(x => x.d != 8) && notesInSwing.Count > 1 && notesInSwing.All(x => x.b == notesInSwing[0].b))
                {
                    // Find the two notes that are furthest apart
                    NotePair furthestNotes = notesInSwing
                        .SelectMany(b1 => notesInSwing.Select(b2 => new NotePair { noteA = b1, noteB = b2 }))
                        .OrderByDescending(pair => Vector2.Distance(new Vector2(pair.noteA.x, pair.noteA.y), new Vector2(pair.noteB.x, pair.noteB.y)))
                        .Select(pair => new NotePair { noteA = pair.noteA, noteB = pair.noteB })
                        .First();

                    Note noteA = furthestNotes.noteA;
                    Note noteB = furthestNotes.noteB;
                    Vector2 noteAPos = new Vector2(noteA.x, noteA.y);
                    Vector2 noteBPos = new Vector2(noteB.x, noteB.y);

                    // Get the direction vector from noteA to noteB
                    Vector2 atb = noteBPos - noteAPos;

                    Vector2 noteACutVector = DirectionalVectorToCutDirection.FirstOrDefault(x => x.Value == noteA.d).Key;
                    float dotProduct = Vector2.Dot(noteACutVector, atb);
                    if (dotProduct < 0)
                    {
                        atb = -atb;   // B before A
                    }

                    // Sort the cubes according to their position along the direction vector
                    notesInSwing.Sort((a, b) => Vector2.Dot(new Vector2(a.x, a.y) - new Vector2(noteA.x, noteA.y), atb).CompareTo(Vector2.Dot(new Vector2(b.x, b.y) - new Vector2(noteA.x, noteA.y), atb)));
                }

                // Assume by default swinging forehanded
                SwingData sData = new SwingData()
                {
                    notes = new List<Note>(notesInSwing),
                    swingParity = Parity.Forehand,
                    swingStartBeat = notesInSwing[0].b,
                    swingEndBeat = notesInSwing[notesInSwing.Count-1].b,
                    rightHand = isRightHand
                };
                sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
                sData.SetEndPosition(notesInSwing[notesInSwing.Count - 1].x, notesInSwing[notesInSwing.Count - 1].y);

                // If first swing, check if potentially upswing start based on orientation
                if (result.Count == 0)
                {
                    if (currentNote.d == 0 || currentNote.d == 4 || currentNote.d == 5)
                    {
                        sData.swingParity = Parity.Backhand;
                        sData.SetStartAngle(BackhandDict[notesInSwing[0].d]);
                        sData.SetEndAngle(BackhandDict[notesInSwing[notesInSwing.Count - 1].d]);
                    }
                    else
                    {
                        sData.SetStartAngle(ForehandDict[notesInSwing[0].d]);
                        sData.SetEndAngle(ForehandDict[notesInSwing[notesInSwing.Count - 1].d]);
                    }
                    result.Add(sData);
                    notesInSwing.Clear();
                    continue;
                }

                // Get previous swing
                SwingData lastSwing = result[result.Count-1];
                Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];

                // Re-order the notesInCut in the event all the notes are dots and same snap
                if (sData.notes.Count > 1 && sData.notes.All(x => x.d == 8) && sData.notes.All(x => x.b == sData.notes[0].b))
                {
                    notesInSwing = new List<Note>(DotStackSort(lastSwing, sData.notes));
                    sData.SetStartPosition(notesInSwing[0].x, notesInSwing[0].y);
                    sData.SetEndPosition(notesInSwing[notesInSwing.Count - 1].x, notesInSwing[notesInSwing.Count - 1].y);
                }

                // Get swing EBPM, if reset then double
                sData.swingEBPM = SwingUtility.SwingEBPM(_bpm, currentNote.b - lastNote.b);
                lastSwing.swingEndBeat = (lastNote.b - currentNote.b) / 2 + lastNote.b;
                if (lastSwing.IsReset) { sData.swingEBPM *= 2; }

                // Work out current player XOffset for bomb calculations
                List<Obstacle> wallsInBetween = mapObjects.Obstacles.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[notesInSwing.Count - 1].b);
                if (wallsInBetween.Count != 0)
                {
                    foreach (Obstacle wall in wallsInBetween)
                    {
                        // Duck wall detection
                        if ((wall.w >= 3 && wall.x <= 1) || (wall.w >= 2 && wall.x == 1))
                        {
                            _playerYOffset = -1;
                            _lastDuckTime = wall.b;
                        }

                        // Dodge wall detection
                        if (wall.x == 1 || (wall.x == 0 && wall.w > 1))
                        {
                            _playerXOffset = 1;
                            _lastDodgeTime = wall.b;
                        }
                        else if (wall.x == 2)
                        {
                            _playerXOffset = -1;
                            _lastDodgeTime = wall.b;
                        }
                    }
                }

                // If time since dodged exceeds a set amount in seconds, undo dodge
                const float undodgeCheckTime = 0.35f;
                if (SwingUtility.BeatToSeconds(_bpm, notesInSwing[notesInSwing.Count - 1].b - _lastDodgeTime) > undodgeCheckTime) { _playerXOffset = 0; }
                if (SwingUtility.BeatToSeconds(_bpm, notesInSwing[notesInSwing.Count - 1].b - _lastDuckTime) > undodgeCheckTime) { _playerYOffset = 0; }

                sData.playerHorizontalOffset = _playerXOffset;
                sData.playerVerticalOffset = _playerYOffset;

                // Get bombs between swings
                List<Bomb> bombsBetweenSwings = mapObjects.Bombs.FindAll(x => x.b > lastNote.b && x.b < notesInSwing[notesInSwing.Count - 1].b);

                // Calculate the time since the last note of the last swing, then attempt to determine this swings parity
                float timeSinceLastNote = Math.Abs(currentNote.b * beatMS - lastSwing.notes[lastSwing.notes.Count - 1].b * beatMS);
                sData.swingParity = ParityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, _playerXOffset, _rightHand, timeSinceLastNote);

                // Depending on swing composition, calculate swing angle for dot-based multi-note swings
                if (sData.notes.All(x => x.d == 8) && sData.notes.Count > 1) CalculateDotStackSwingAngle(lastSwing, ref sData);
                // Calculate dot cut direction
                if (sData.notes.All(x => x.d == 8) && sData.notes.Count == 1) CalculateDotDirection(lastSwing, ref sData, true);

                // Now that parity state is determined, set the angle for the swing based on parity
                if (sData.notes.Any(x => x.d != 8))
                {
                    if (sData.swingParity == Parity.Backhand)
                    {
                        sData.SetStartAngle(BackhandDict[notesInSwing.First(x => x.d != 8).d]);
                        sData.SetEndAngle(BackhandDict[notesInSwing.Last(x => x.d != 8).d]);
                    }
                    else
                    {
                        sData.SetStartAngle(ForehandDict[notesInSwing.First(x => x.d != 8).d]);
                        sData.SetEndAngle(ForehandDict[notesInSwing.Last(x => x.d != 8).d]);
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
        /// Re-orders a list of dots in the order they should be hit according to last swing data.
        /// </summary>
        /// <param name="lastSwing">Last swing the player would have done</param>
        /// <param name="dotNotes">List of dots in swing</param>
        /// <returns></returns>
        internal static List<Note> DotStackSort(SwingData lastSwing, List<Note> dotNotes)
        {
            // Find the two notes that are furthest apart
            NotePair furthestNotes = dotNotes
                .SelectMany(b1 => dotNotes.Select(b2 => new NotePair { noteA = b1, noteB = b2 }))
                .OrderByDescending(pair => Vector2.Distance(new Vector2(pair.noteA.x, pair.noteA.y), new Vector2(pair.noteB.x, pair.noteB.y)))
                .Select(pair => new NotePair { noteA = pair.noteA, noteB = pair.noteB })
                .First();

            Note noteA = furthestNotes.noteA;
            Note noteB = furthestNotes.noteB;
            Vector2 noteAPos = new Vector2(noteA.x, noteA.y);
            Vector2 noteBPos = new Vector2(noteB.x, noteB.y);

            // Get the direction vector from noteA to noteB
            Vector2 atb = noteBPos - noteAPos;

            // In-case the last note was a dot, turn the swing angle into the closest cut direction based on last swing parity
            int lastCutDirApprox = SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, lastSwing.swingParity, 45.0f);

            // Convert the cut direction to a directional vector then do the dot product between noteA to noteB and last swing direction
            Vector2 noteACutVector = DirectionalVectorToCutDirection.FirstOrDefault(x => x.Value == opposingCutDict[lastCutDirApprox]).Key;
            float dotProduct = Vector2.Dot(noteACutVector, atb);
            if (dotProduct < 0)
            {
                // Flip the direction the dots will be hit
                atb = -atb;
            }
            else if (Math.Abs(dotProduct) < float.Epsilon)
            {

                // In the event its a right angle, do a distance check and prefer the closest note.
                // This won't sort correctly if the distances are the same, will rework this later.
                Note lastNote = lastSwing.notes[lastSwing.notes.Count-1];

                float aDist = Vector2.Distance(noteAPos, new Vector2(lastNote.x, lastNote.y));
                float bDist = Vector2.Distance(noteBPos, new Vector2(lastNote.x, lastNote.y));

                if (Math.Abs(aDist) > Math.Abs(bDist))
                {
                    atb = -atb;
                }
            }

            // Sort the notes according to their position along the direction vector
            dotNotes.Sort((a, b) => Vector2.Dot(new Vector2(a.x, a.y) - new Vector2(noteA.x, noteA.y), atb).CompareTo(Vector2.Dot(new Vector2(b.x, b.y) - new Vector2(noteA.x, noteA.y), atb)));
            return dotNotes;
        }

        /// <summary>
        /// Given a previous swing and current swing (all dot notes), calculate saber rotation.
        /// </summary>
        /// <param name="lastSwing">Last swing the player would have done</param>
        /// <param name="currentSwing">Swing you want to calculate swing angle for</param>
        internal static void CalculateDotStackSwingAngle(SwingData lastSwing, ref SwingData currentSwing)
        {
            // Get the first and last note based on array order
            Note firstNote = currentSwing.notes[0];
            Note lastNote = currentSwing.notes[currentSwing.notes.Count-1];

            int orientation = CutDirFromNoteToNote(firstNote, lastNote);
            int altOrientation = CutDirFromNoteToNote(lastNote, firstNote);

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

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }

        /// <summary>
        /// Given previous and current swing (singular dot note), calculate and clamp saber rotation.
        /// </summary>
        /// <param name="lastSwing">Last swing the player would have done</param>
        /// <param name="currentSwing">Swing you want to calculate swing angle for</param>
        /// <param name="clamp">True if you want to perform clamping on the angle</param>
        internal static void CalculateDotDirection(SwingData lastSwing, ref SwingData currentSwing, bool clamp = true)
        {
            Note dotNote = currentSwing.notes[0];
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];

            // Get Cut Dir from last note to dot note
            int orientation = CutDirFromNoteToNote(lastNote, dotNote);

            // If same grid position, just maintain angle
            if (dotNote.x == lastNote.x && dotNote.y == lastNote.y)
            {
                orientation = opposingCutDict[orientation];
            }

            // Get the angle
            float angle = (lastSwing.swingParity == Parity.Forehand && currentSwing.resetType == ResetType.None) ?
                ForehandDict[orientation] :
                BackhandDict[orientation];

            if (clamp)
            {
                // If clamp, then apply clamping to the angle based on the ruleset below
                int xDiff = Math.Abs(dotNote.x - lastNote.x);
                int yDiff = Math.Abs(dotNote.y - lastNote.y);
                if (xDiff == 3) { angle = SwingUtility.Clamp(angle, -90, 90); }
                else if (xDiff == 2) { angle = SwingUtility.Clamp(angle, -45, 45); }
                else if (xDiff == 0 && yDiff > 1) { angle = 0; }
                else { angle = SwingUtility.Clamp(angle, -45, 0); }
            }

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }

        // Attempts to add extra swings based on the isReset tag for a list of swings.
        // NOTICE: For those using the data for map visualization purposes (using data to play the map),
        // This will simply add a swing in the inverse parity of the swing which is flagged as isReset.
        // This does not add active bomb avoidance to the position of the saber.

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
                float timeDifference = SwingUtility.BeatToSeconds(_bpm, nextNote.b - lastNote.b);

                SwingData swing = new SwingData();
                swing.swingParity = (currentSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                swing.swingStartBeat = lastSwing.swingEndBeat + Math.Max(SwingUtility.SecondsToBeats(_bpm, timeDifference / 5), 0.2f);
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

        #region GENERAL UTILITY

        /// <summary>
        /// Given 2 notes, calculate a cutDirectionID of the lastNote based on direction from first to last.
        /// </summary>
        /// <param name="firstNote">First note</param>
        /// <param name="lastNote">Second note</param>
        /// <returns></returns>
        public static int CutDirFromNoteToNote(Note firstNote, Note lastNote)
        {
            // Get the direction from first to last note, get the lowest dot product when compared
            // to all possible direction vectors for notes, then calculates a cut direction and invert it
            Vector2 dir = new Vector2(lastNote.x, lastNote.y) - new Vector2(firstNote.x, firstNote.y);
            dir = Vector2.Normalize(dir);
            Vector2 lowestDotProduct = DirectionalVectors.OrderBy(v => Vector2.Dot(dir, v)).First();
            Vector2 cutDirection = new Vector2((float)Math.Round(lowestDotProduct.X), (float)Math.Round(lowestDotProduct.Y));
            int orientation = DirectionalVectorToCutDirection[cutDirection];
            return orientation;
        }

        /// <summary>
        /// Returns a cut direction given angle and parity, and optionally a rounding interval
        /// </summary>
        /// <param name="angle">Saber Rotation</param>
        /// <param name="parity">Current saber parity</param>
        /// <param name="interval">Rounding interval (Intervals of 45)</param>
        /// <returns></returns>
        public static int CutDirFromAngle(float angle, Parity parity, float interval = 0.0f)
        {
            // If not using an interval, round so that -49 becomes 0, 49 becomes 0, but 91
            // becomes 90 and -91 becomes -90
            float roundedAngle;
            if (interval != 0.0f)
            {
                float intervalTimes = angle / interval;
                roundedAngle = (float)((intervalTimes >= 0)
                    ? Math.Floor(intervalTimes) * interval
                    : Math.Ceiling(intervalTimes) * interval);
            }
            else
            {
                roundedAngle = (float)Math.Floor(angle / 45) * 45;
            }

            return (parity == Parity.Forehand) ?
                ForehandDict.FirstOrDefault(x => x.Value == roundedAngle).Key :
                BackhandDict.FirstOrDefault(x => x.Value == roundedAngle).Key;
        }

        #endregion
    }
}