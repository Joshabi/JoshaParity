using JoshaParity;
using System.Numerics;

namespace JoshaUtils
{

    /// <summary>
    /// Current Orientation States for any given hand
    /// </summary>
    public enum PARITY_STATE
    {
        NONE,
        FOREHAND,
        BACKHAND,
    }

    /// <summary>
    /// Swing Reset Type
    /// </summary>
    public enum RESET_TYPE
    {
        NONE = 0,  // Swing does not force a reset, or triangle
        BOMB = 1,    // Swing forces a reset due to bombs
        REBOUND = 2,   // Swing forces an additional swing
    }

    /// <summary>
    /// Contains position and rotation information for a given swing
    /// </summary>
    public struct PositionData
    {
        public int x;
        public int y;
        public float rotation;
    }

    /// <summary>
    /// Contains map entities (Bombs, Walls, Notes)
    /// </summary>
    public struct MapObjects{
        // Map Entity Lists
        private List<Note> _mapNotes;
        private List<Note> _mapBombs;
        private List<Obstacle> _mapWalls;

        public List<Note> Notes { get { return _mapNotes; } set { _mapNotes = value; } }
        public List<Note> Bombs { get { return _mapBombs; } }
        public List<Obstacle> Obstacles { get { return _mapWalls; } }

        // Constructor
        public MapObjects(List<Note> notes, List<Note> bombs, List<Obstacle> walls) {
            _mapNotes = notes;
            _mapBombs = bombs;
            _mapWalls = walls;
        }
    }

    /// <summary>
    /// Contains data for a given swing
    /// </summary>
    public struct SwingData
    {
        public PARITY_STATE swingParity;
        public RESET_TYPE resetType;
        public float swingStartBeat;
        public float swingEndBeat;
        public float swingEBPM;
        public List<Note> notes;
        public PositionData startPos;
        public PositionData endPos;
        public float playerHorizontalOffset;
        public float playerVerticalOffset;

        public void SetResetType (RESET_TYPE type) { resetType = type; }
        public void SetParity(PARITY_STATE sliceParity) { this.swingParity = sliceParity; }
        public void SetStartPosition(int x, int y) { startPos.x = x; endPos.y = y; }
        public void SetEndPosition(int x, int y) { endPos.x = x; endPos.y = y; }
        public void SetStartAngle(float angle) { startPos.rotation = angle; }
        public void SetEndAngle(float angle) { endPos.rotation = angle; }
        public bool IsReset { get { return resetType != 0; } }

        public override string ToString()
        {
            string returnString = $"Swing Note/s or Bomb/s {swingStartBeat} " +
                $"|| Parity of this swing: {swingParity}" +
                $"\nHorizontal Player Offset: {playerHorizontalOffset} || Vertical Player Offset: {playerVerticalOffset}" +
                $"\nSwing EBPM: {swingEBPM} || Reset Type? {resetType}";
            return returnString;
        }
    }

    /// <summary>
    /// Calculates swing data for a given set of map data
    /// </summary>
    static class SwingDataGeneration
    {

        #region Parity and Orientation Dictionaries

        // 0 - Up hit 1 - Down hit 2 - Left Hit 3 - Right Hit
        // 4 - Up Left 5 - Up Right - 6 Down Left 7 - Down Right 8 - Dot note

        // RIGHT HAND PARITY DICTIONARIES
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
        public static readonly Dictionary<int, float> rightForehandDict = new Dictionary<int, float>()
        { { 0, -180 }, { 1, 0 }, { 2, -90 }, { 3, 90 }, { 4, -135 }, { 5, 135 }, { 6, -45 }, { 7, 45 }, { 8, 0 } };
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
         public static readonly Dictionary<int, float> rightBackhandDict = new Dictionary<int, float>()
        { { 0, 0 }, { 1, -180 }, { 2, 90 }, { 3, -90 }, { 4, 45 }, { 5, -45 }, { 6, 135 }, { 7, -135 }, { 8, 0 } };
    
        // LEFT HAND PARITY DICTIONARIES
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
        private static readonly Dictionary<int, float> leftForehandDict = new Dictionary<int, float>()
        { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
        private static readonly Dictionary<int, float> leftBackhandDict = new Dictionary<int, float>()
        { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };

        public static readonly Dictionary<int, int> opposingCutDict = new Dictionary<int, int>()
        { { 0, 1 }, { 1, 0 }, { 2, 3 }, { 3, 2 }, { 4, 7 }, { 7, 4 }, { 5, 6 }, { 6, 5 } };

        public static Dictionary<int, float> ForehandDict { get { return (_rightHand) ? rightForehandDict : leftForehandDict; } }
        public static Dictionary<int, float> BackhandDict { get { return (_rightHand) ? rightBackhandDict : leftBackhandDict; } }

        // Contains a list of directional vectors
        public static readonly Vector2[] directionalVectors =
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

        private static readonly Dictionary<Vector2, int> directionalVectorToCutDirection = new Dictionary<Vector2, int>()
        {
            { new Vector2(0, 1), 0 },
            { new Vector2(0, -1), 1 },
            { new Vector2(-1, 0), 2 },
            { new Vector2(1, 0), 3 },
            { new Vector2(1, 1), 5 },
            { new Vector2(-1, 1), 4 },
            { new Vector2(-1, -1), 6 },
            { new Vector2(1, -1), 7 }
        };

    #endregion

        #region Variables

        private static MapObjects _mapObjects;
        private static IParityMethod _parityMethodology = new GenericParityCheck();
        private static float _BPM;
        private static bool _rightHand = true;
        private static int _playerXOffset = 0;
        private static int _playerYOffset = 0;
        private static float _lastDodgeTime;
        private static float _lastDuckTime;

        #endregion

        /// <summary>
        /// Called to check a specific map difficulty file
        /// </summary>
        /// <param name="mapDif">Map Difficulty to check</param>
        /// <param name="BPM">BPM of the map</param>
        public static void Run(MapDifficulty mapDif, float BPM)
        {
            // Reset Operating Variables
            _BPM = BPM;
            _playerXOffset = 0;
            _playerYOffset = 0;

            // Seperate notes, bombs and walls
            List<Note> leftHandedNotes = mapDif._notes.
                Where(n => n._type == 0).ToList();
            List<Note> rightHandedNotes = mapDif._notes.
                Where(n => n._type == 1).ToList();
            List<Note> bombs = mapDif._notes.
                Where(n => n._type == 3).ToList();
            List<Obstacle> walls = mapDif._obstacles.
                Where(n => n._lineIndex < 3).ToList();

            // Calculate swing data for both hands
            MapObjects mapData = new(rightHandedNotes, bombs, walls);
            List<SwingData> rightHandSD = GetSwingData(mapData, true);
            mapData.Notes = leftHandedNotes;
            List<SwingData> leftHandSD = GetSwingData(mapData, false);

            // Combine swing data and sort
            List<SwingData> combinedSD = new(rightHandSD);
            combinedSD.AddRange(leftHandSD);
            combinedSD = combinedSD.OrderBy(x => x.swingStartBeat).ToList();

            Console.WriteLine("Reset Count:" + combinedSD.Count(x => x.IsReset == true));
            foreach(var swing in combinedSD.Where(x => x.IsReset)) {
                Console.WriteLine("Potential Reset of Type: " + swing.resetType + " at: " + swing.swingStartBeat);
            }

            foreach(var swing in rightHandSD)
            {
                Console.WriteLine("Swing Info: \n" + swing.ToString());
            }
        }

        /// <summary>
        /// Calculates and returns a list of swing data for a set of map objects
        /// </summary>
        /// <param name="mapObjects">Information about notes, walls and obstacles</param>
        /// <param name="isRightHand">Is Right Hand Notes?</param>
        /// <returns></returns>
        private static List<SwingData> GetSwingData(MapObjects mapObjects, bool isRightHand)
        {
            List<SwingData> result = new();
            _rightHand = isRightHand;
            _mapObjects = mapObjects;

            float sliderPrecision = 1 / 6f;
            List<Note> notesInSwing = new();

            for (int i = 0; i < _mapObjects.Notes.Count - 1; i++)
            {
                Note currentNote = _mapObjects.Notes[i];

                // If this is not the final note, check for slider or stack
                if (i != _mapObjects.Notes.Count - 1)
                {
                    Note nextNote = _mapObjects.Notes[i + 1];
                    notesInSwing.Add(currentNote);

                    // If precision falls under "Slider", or time stamp is the same, run
                    // checks to figure out if it is a slider, window, stack ect..
                    if (Math.Abs(currentNote._time - nextNote._time) <= sliderPrecision)
                    {
                        if (nextNote._cutDirection == 8 || notesInSwing[^1]._cutDirection == 8 ||
                            currentNote._cutDirection == nextNote._cutDirection || Math.Abs(ForehandDict[currentNote._cutDirection] - ForehandDict[nextNote._cutDirection]) <= 45 ||
                             Math.Abs(BackhandDict[currentNote._cutDirection] - BackhandDict[nextNote._cutDirection]) <= 45)
                        { continue; }
                    }
                }
                else 
                { 
                    notesInSwing.Add(currentNote); 
                }

                // Re-order the notesInCut in the event all the notes are on the same snap and not dots
                if (notesInSwing.All(x => x._cutDirection != 8) && notesInSwing.Count > 1)
                {
                    // Find the two notes that are furthest apart
                    var furthestNotes = (from c1 in notesInSwing
                                         from c2 in notesInSwing
                                         orderby Vector2.Distance(new Vector2(c1._lineIndex, c1._lineLayer), new Vector2(c2._lineIndex, c2._lineLayer)) descending
                                         select new { c1, c2 }).First();

                    Note noteA = furthestNotes.c1;
                    Note noteB = furthestNotes.c2;
                    Vector2 noteAPos = new(noteA._lineIndex, noteA._lineLayer);
                    Vector2 noteBPos = new(noteB._lineIndex, noteB._lineLayer);

                    // Get the direction vector from noteA to noteB
                    Vector2 ATB = noteBPos - noteAPos;

                    Vector2 noteACutVector = directionalVectorToCutDirection.FirstOrDefault(x => x.Value == noteA._cutDirection).Key;
                    float dotProduct = Vector2.Dot(noteACutVector, ATB);
                    if (dotProduct < 0)
                    {
                        ATB = -ATB;   // B before A
                    }

                    // Sort the cubes according to their position along the direction vector
                    notesInSwing.Sort((a, b) => Vector2.Dot(new Vector2(a._lineIndex, a._lineLayer) - new Vector2(noteA._lineIndex, noteA._lineLayer), ATB).CompareTo(Vector2.Dot(new Vector2(b._lineIndex, b._lineLayer) - new Vector2(noteA._lineIndex, noteA._lineLayer), ATB)));
                }

                // Assume by default swinging forehanded
                SwingData sData = new()
                {
                    notes = new List<Note>(notesInSwing),
                    swingParity = PARITY_STATE.FOREHAND,
                    swingStartBeat = notesInSwing[0]._time,
                    swingEndBeat = notesInSwing[^1]._time + 0.1f
                };
                sData.SetStartPosition(notesInSwing[0]._lineIndex, notesInSwing[0]._lineLayer);
                sData.SetEndPosition(notesInSwing[^1]._lineIndex, notesInSwing[^1]._lineLayer);

                // If first swing, check if potentially upswing start based on orientation
                if (result.Count == 0)
                {
                    if (currentNote._cutDirection == 0 || currentNote._cutDirection == 4 || currentNote._cutDirection == 5)
                    {
                        sData.swingParity = PARITY_STATE.BACKHAND;

                        sData.SetStartAngle(BackhandDict[notesInSwing[0]._cutDirection]);
                        sData.SetEndAngle(BackhandDict[notesInSwing[^1]._cutDirection]);
                    }
                    result.Add(sData);
                    notesInSwing.Clear();
                    continue;
                }

                // Get previous swing
                SwingData lastSwing = result[^1];
                Note lastNote = lastSwing.notes[^1];

                // Re-order the notesInCut in the event all the notes are dots and same snap
                if (sData.notes.Count > 1 && sData.notes.All(x => x._cutDirection == 8))
                {
                    sData.notes = new(DotStackSort(lastSwing, sData.notes, lastSwing.swingParity));
                    sData.SetStartPosition(notesInSwing[0]._cutDirection, notesInSwing[0]._lineLayer);
                    sData.SetEndPosition(notesInSwing[^1]._cutDirection, notesInSwing[^1]._lineLayer);
                }

                // Get swing EBPM, if reset then double
                sData.swingEBPM = SwingEBPM(_BPM, currentNote._time - lastNote._time);
                lastSwing.swingEndBeat = (lastNote._time - currentNote._time) / 2 + lastNote._time;
                if (sData.IsReset) { sData.swingEBPM *= 2; }

                // Work out current player XOffset for bomb calculations
                List<Obstacle> wallsInBetween = _mapObjects.Obstacles.FindAll(x => x._time > lastNote._time && x._time < notesInSwing[^1]._time);
                if (wallsInBetween.Count != 0)
                {
                    foreach (Obstacle wall in wallsInBetween)
                    {
                        // Duck wall detection
                        if ((wall._width >= 3 && wall._lineIndex <= 1) || (wall._width == 2 && wall._lineIndex == 1))
                        {
                            _playerYOffset = -1;
                            _lastDuckTime = wall._time;
                        }

                        // Dodge wall detection
                        if (wall._lineIndex == 1 || wall._lineIndex == 0 && wall._width > 1)
                        {
                            _playerXOffset = 1;
                            _lastDuckTime = wall._time;
                        }
                        else if (wall._lineIndex == 2)
                        {
                            _playerXOffset = -1;
                            _lastDuckTime = wall._time;
                        }
                    }
                }

                // If time since dodged exceeds a set amount in seconds, undo dodge
                var undodgeCheckTime = 0.35f;
                if (BeatToSeconds(_BPM, notesInSwing[^1]._time - _lastDodgeTime) > undodgeCheckTime) { _playerXOffset = 0; }
                if (BeatToSeconds(_BPM, notesInSwing[^1]._time - _lastDuckTime) > undodgeCheckTime) { _playerYOffset = 0; }

                sData.playerHorizontalOffset = _playerXOffset;
                sData.playerVerticalOffset = _playerYOffset;

                // Get bombs between swings
                List<Note> bombsBetweenSwings = _mapObjects.Bombs.FindAll(x => x._time > lastNote._time && x._time < notesInSwing[^1]._time);

                // Perform dot checks depending on swing composition.
                if (sData.notes.All(x => x._cutDirection == 8) && sData.notes.Count > 1) CalculateDotStackSwingAngle(lastSwing, ref sData);
                if (sData.notes[0]._cutDirection == 8 && sData.notes.Count == 1) CalculateDotDirection(lastSwing, ref sData);

                float timeSinceLastNote = BeatToSeconds(_BPM, currentNote._time - lastSwing.notes[^1]._time);
                sData.swingParity = _parityMethodology.ParityCheck(lastSwing, ref sData, bombsBetweenSwings, _playerXOffset, _rightHand, timeSinceLastNote);

                // Depending on parity, set angle
                if (sData.notes.Any(x => x._cutDirection != 8))
                {
                    if (sData.swingParity == PARITY_STATE.BACKHAND)
                    {
                        sData.SetStartAngle(BackhandDict[notesInSwing.First(x => x._cutDirection != 8)._cutDirection]);
                        sData.SetEndAngle(BackhandDict[notesInSwing.Last(x => x._cutDirection != 8)._cutDirection]);
                    }
                    else
                    {
                        sData.SetStartAngle(ForehandDict[notesInSwing.First(x => x._cutDirection != 8)._cutDirection]);
                        sData.SetEndAngle(ForehandDict[notesInSwing.Last(x => x._cutDirection != 8)._cutDirection]);
                    }
                }

                // If current parity method thinks we are upside down and not dot notes in next hit, flip values.
                // This catch is in place to turn -180 into 180 (because the dictionary only has a definition from all the way around
                // in one direction (which is -180)
                if (_parityMethodology.UpsideDown == true)
                {
                    if (sData.notes.All(x => x._cutDirection != 8))
                    {
                        sData.SetStartAngle(sData.startPos.rotation * -1);
                        sData.SetEndAngle(sData.endPos.rotation * -1);
                    }
                }

                // Add swing to list
                result.Add(sData);
                notesInSwing.Clear();
            }
            return result;
        }

        #region DOT FUNCTIONS AND BOMB AVOIDANCE

        private static List<Note> DotStackSort(SwingData lastSwing, List<Note> nextNotes, PARITY_STATE lastSwingParity)
        {

            // Find the two notes that are furthest apart
            var furthestNotes = (from c1 in nextNotes
                                 from c2 in nextNotes
                                 orderby Vector2.Distance(new Vector2(c1._lineIndex, c1._lineLayer), new Vector2(c2._lineIndex, c2._lineLayer)) descending
                                 select new { c1, c2 }).First();

            Note noteA = furthestNotes.c1;
            Note noteB = furthestNotes.c2;
            Vector2 noteAPos = new(noteA._lineIndex, noteA._lineLayer);
            Vector2 noteBPos = new(noteB._lineIndex, noteB._lineLayer);

            // Get the direction vector from noteA to noteB
            Vector2 ATB = noteBPos - noteAPos;

            // Incase the last note was a dot, turn the swing angle into the closest cut direction based on last swing parity
            int lastNoteClosestCutDir = ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastSwing.startPos.rotation / 45.0) * 45).Key;

            // Convert the cut direction to a directional vector then do the dot product between noteA to noteB and last swing direction
            Vector2 noteACutVector = directionalVectorToCutDirection.FirstOrDefault(x => x.Value == opposingCutDict[lastNoteClosestCutDir]).Key;
            float dotProduct = Vector2.Dot(noteACutVector, ATB);
            if (dotProduct < 0)
            {
                ATB = -ATB;
            }
            else if (dotProduct == 0)
            {
                // In the event its at a right angle, pick the note with the closest distance
                Note lastNote = lastSwing.notes[^1];

                float aDist = Vector2.Distance(noteAPos, new Vector2(lastNote._lineIndex, lastNote._lineLayer));
                float bDist = Vector2.Distance(noteBPos, new Vector2(lastNote._lineIndex, lastNote._lineLayer));

                if (Math.Abs(aDist) < Math.Abs(bDist))
                {
                    nextNotes.Sort((a, b) => Vector2.Distance(new Vector2(a._lineIndex, a._lineLayer), new Vector2(lastNote._lineIndex, lastNote._lineLayer))
                        .CompareTo(Vector2.Distance(new Vector2(b._lineIndex, b._lineLayer), new Vector2(lastNote._lineIndex, lastNote._lineLayer))));
                    return nextNotes;
                }
                else
                {
                    nextNotes.Sort((a, b) => Vector2.Distance(new Vector2(b._lineIndex, b._lineLayer), new Vector2(lastNote._lineIndex, lastNote._lineLayer))
                        .CompareTo(Vector2.Distance(new Vector2(a._lineIndex, a._lineLayer), new Vector2(lastNote._lineIndex, lastNote._lineLayer))));
                    return nextNotes;
                }

            }

            // Sort the cubes according to their position along the direction vector
            nextNotes.Sort((a, b) => Vector2.Dot(new Vector2(a._lineIndex, a._lineLayer) - new Vector2(noteA._lineIndex, noteA._lineLayer), ATB).CompareTo(Vector2.Dot(new Vector2(b._lineIndex, b._lineLayer) - new Vector2(noteA._lineIndex, noteA._lineLayer), ATB)));
            return nextNotes;
        }

        // Modifies a Swing if Dot Notes are involved
        private static void CalculateDotStackSwingAngle(SwingData lastSwing, ref SwingData currentSwing)
        {
            // Get the first and last note based on array order
            float angle, altAngle, change, altChange;
            Note firstNote = currentSwing.notes[0];
            Note lastNote = currentSwing.notes[^1];

            int orientation = CutDirFromNoteToNote(firstNote, lastNote);
            int altOrientation = CutDirFromNoteToNote(lastNote, firstNote);

            angle = (currentSwing.swingParity == PARITY_STATE.FOREHAND) ? ForehandDict[orientation] : BackhandDict[orientation];
            altAngle = (currentSwing.swingParity == PARITY_STATE.FOREHAND) ? ForehandDict[altOrientation] : BackhandDict[altOrientation];

            change = lastSwing.endPos.rotation - angle;
            altChange = lastSwing.endPos.rotation - altAngle;


            if (Math.Abs(altChange) < Math.Abs(change)) angle = altAngle;

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }

        // Calculates how a dot note should be swung according to the prior swing.
        private static void CalculateDotDirection(SwingData lastSwing, ref SwingData currentSwing)
        {
            Note dotNote = currentSwing.notes[0];
            Note lastNote = lastSwing.notes[^1];

            int orientation = CutDirFromNoteToNote(lastNote, dotNote);

            // If same grid position, just maintain angle
            if (dotNote._lineIndex == lastNote._lineIndex && dotNote._lineLayer == lastNote._lineLayer)
            {
                orientation = opposingCutDict[orientation];
            }

            float angle = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ?
                ForehandDict[orientation] :
                BackhandDict[orientation];

            float xDiff = Math.Abs(dotNote._lineIndex - lastNote._lineIndex);
            float yDiff = Math.Abs(dotNote._lineLayer - lastNote._lineLayer);
            if (xDiff == 3) { angle = Math.Clamp(angle, -90, 90); }
            else if (yDiff == 0 && xDiff < 2) { angle = Math.Clamp(angle, -45, 45); }
            else if (yDiff > 0 && xDiff > 0) { angle = Math.Clamp(angle, -45, 45); }

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);

            return;
        }

        // Attempts to add bomb avoidance based on the isReset tag for a list of swings.
        // NOTE: To improve this, probably want bomb detection in its own function and these swings
        // would be added for each bomb in the sabers path rather then only for bomb resets.
        private static List<SwingData> AddBombResetAvoidance(List<SwingData> swings)
        {
            List<SwingData> result = new(swings);
            int swingsAdded = 0;

            for (int i = 0; i < swings.Count - 1; i++)
            {
                // If Reset
                if (swings[i].IsReset)
                {
                    // Reference to last swing
                    SwingData lastSwing = swings[i - 1];
                    SwingData currentSwing = swings[i];
                    Note lastNote = lastSwing.notes[^1];
                    Note nextNote = currentSwing.notes[0];

                    // Time difference between last swing and current note
                    float timeDifference = BeatToSeconds(_BPM, nextNote._time - lastNote._time);

                    SwingData swing = new();
                    swing.swingParity = (currentSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND;
                    swing.swingStartBeat = lastSwing.swingEndBeat + SecondsToBeats(_BPM, timeDifference / 5);
                    swing.swingEndBeat = swing.swingStartBeat + SecondsToBeats(_BPM, timeDifference / 4);
                    swing.SetStartPosition(lastNote._lineIndex, lastNote._lineLayer);

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
            }
            return result;
        }

        #endregion

        #region UTILITY FUNCTIONS

        // Given 2 notes, gets the cut direction of the 2nd note based on the direction from first to last
        private static int CutDirFromNoteToNote(Note firstNote, Note lastNote)
        {
            Vector2 dir = (new Vector2(lastNote._lineIndex, lastNote._lineLayer) - new Vector2(firstNote._lineIndex, firstNote._lineLayer));
            Vector2 lowestDotProduct = directionalVectors.OrderBy(v => Vector2.Dot(dir, v)).First();
            Vector2 cutDirection = new Vector2(MathF.Round(lowestDotProduct.X), MathF.Round(lowestDotProduct.Y));
            int orientation = directionalVectorToCutDirection[cutDirection];
            return orientation;
        }

        /// <summary>
        /// Returns a timestamp given a BPM and Beat Number
        /// </summary>
        /// <param name="BPM"></param>
        /// <param name="beatNo"></param>
        /// <returns></returns>
        private static string BeatToTimestamp(float BPM, float beatNo)
        {
            var seconds = beatNo / (BPM / 60);
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            string timestamp =
                string.Format("{0:D2}m:{1:D2}s:{2:D2}ms", time.Minutes, time.Seconds, time.Milliseconds);


            return timestamp;
        }

        /// <summary>
        /// Returns the effective BPM of a swing given time in beats and song BPM
        /// </summary>
        /// <param name="BPM"></param>
        /// <param name="beatDiff"></param>
        /// <returns></returns>
        private static float SwingEBPM(float BPM, float beatDiff)
        {
            var seconds = beatDiff / (BPM / 60);
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            return (float)((60000 / time.TotalMilliseconds) / 2);
        }

        /// <summary>
        /// Converts a beat difference into Effective BPM
        /// </summary>
        /// <param name="BPM"></param>
        /// <param name="beatDiff"></param>
        /// <returns></returns>
        public static float BeatToSeconds(float BPM, float beatDiff)
        {
            return (beatDiff / (BPM / 60));
        }

        public static float SecondsToBeats(float bpm, float seconds)
        {
            return seconds * (bpm / 60.0f);
        }

        #endregion
    }
}