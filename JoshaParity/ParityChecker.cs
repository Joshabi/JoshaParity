using JoshaParity;
using System;
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
        REBOUND = 2,   // Swing forces an additional swing not due to bombs
        ROLL = 3   // Swing requires rolling the wrist (Magnetism Style)
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
    public class SwingData
    {
        public PARITY_STATE swingParity;
        public RESET_TYPE resetType;
        public float swingStartBeat;
        public float swingEndBeat;
        public float swingEBPM = 0;
        public bool isInverted = false;
        public List<Note> notes = new();
        public PositionData startPos = new();
        public PositionData endPos = new();
        public float playerHorizontalOffset;
        public float playerVerticalOffset;

        public SwingData(PARITY_STATE parity, List<Note> notes)
        {
            swingParity = parity;
            resetType = RESET_TYPE.NONE;
            this.notes = new(notes);
            swingStartBeat = notes[0]._time;
            swingEndBeat = notes[^1]._time + 0.2f; // NOTE: Eventually determine by EBPM

            startPos.x = notes[0]._lineIndex;
            startPos.y = notes[0]._lineLayer;
            endPos.x = notes[^1]._lineIndex;
            endPos.y = notes[^1]._lineLayer;
        
        }

        public void SetStartPosition(int x, int y) { startPos.x = x; startPos.y = y; }
        public void SetEndPosition(int x, int y) { endPos.x = x; endPos.y = y; }
        public void SetStartAngle(float rotation) { startPos.rotation = rotation; }
        public void SetEndAngle(float rotation) { endPos.rotation = rotation; }
        public bool IsReset { get { return resetType != RESET_TYPE.NONE && resetType != RESET_TYPE.BOMB; } }

        public override string ToString()
        {
            String returnString = $"Swing Note/s or Bomb/s {swingStartBeat} " +
                $"|| Parity of this swing: {swingParity}" +
                $"\nHorizontal Player Offset: {playerHorizontalOffset} || Vertical Player Offset: {playerVerticalOffset}" +
                $"\nSwing EBPM: {swingEBPM} || Is Invert? {isInverted} || Reset Type? {resetType}";
            return returnString;
        }
    }

    /// <summary>
    /// Generates a list of potential parity issues
    /// </summary>
    static class ParityChecker
    {
        // 0 - Up hit 1 - Down hit 2 - Left Hit 3 - Right Hit
        // 4 - Up Left 5 - Up Right - 6 Down Left 7 - Down Right 8 - Dot note

        #region Positional and Rotational Definitions

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

        // Contains a list of directional vecotrs
        public static readonly Vector2[] directionalVectors = {
            new Vector2(0, 1),   // up
            new Vector2(0, -1),  // down
            new Vector2(-1, 0),  // left
            new Vector2(1, 0),   // right
            new Vector2(1, 1),   // up right
            new Vector2(-1, 1),  // up left
            new Vector2(-1, -1), // down left
            new Vector2(1, -1)   // down right
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
        private static float _playerXOffset = 0;
        private static float _playerYOffset = 0;
        private static float _lastDodgeTime;
        private static float _lastWallTime;

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
        }

        /// <summary>
        /// Given the last swing, and the next swing notes, get information about the swing
        /// </summary>
        /// <param name="lastSwing">The swing data of the last taken swing</param>
        /// <param name="nextNotes">Next notes to be hit</param>
        /// <returns></returns>
        private static SwingData AssessSwing(SwingData lastSwing, List<Note> nextNotes)
        {
            // Create new swing data
            SwingData sData = new(PARITY_STATE.NONE, nextNotes);

            // Check if there is a bomb causing a reset
            List<Note> bombsInbetween = _mapObjects.Bombs.FindAll(x => x._time > lastSwing.swingStartBeat && x._time < nextNotes[0]._time);

            #region Wall Detection

            // Dodge Walls and Duck Walls Detection.
            // Accounts for leaning bomb resets.
            List<Obstacle> wallsInbetween = _mapObjects.Obstacles.FindAll(x => x._time > lastSwing.swingStartBeat && x._time < nextNotes[0]._time);
            if(wallsInbetween != null) { 
                foreach(Obstacle wall in wallsInbetween)
                {
                    // Duck wall detection
                    if ((wall._width >= 3 && (wall._lineIndex <= 1)) || (wall._width == 2 && wall._lineIndex == 1)) {
                        _playerYOffset = -1;
                        _lastWallTime = wall._time + wall._duration;
                        break;
                    }

                    // Dodge wall detection
                    if (wall._lineIndex == 1 || wall._lineIndex == 2)
                    {
                        _playerXOffset = (wall._lineIndex == 1) ? 1 : -1;
                        _lastDodgeTime = wall._time + wall._duration;
                    }
                }
            }

            // If time since dodged last exceeds a set amount in Seconds (might convert to ms
            // for everything down the line tbh), undo dodge
            var wallEndCheckTime = 0.35f;
            if (BeatToSeconds(_BPM, nextNotes[0]._time - _lastDodgeTime) > wallEndCheckTime) {
                _playerXOffset = 0;
            }
            if (BeatToSeconds(_BPM, nextNotes[0]._time - _lastWallTime) > wallEndCheckTime) {
                _playerYOffset = 0;
            }

            #endregion

            // Work out Parity
            sData.swingParity = _parityMethodology.ParityCheck(lastSwing, ref sData, bombsInbetween, _playerXOffset, _playerYOffset, _rightHand);

            // Depending on parity, set angle
            if (sData.swingParity == PARITY_STATE.BACKHAND) {
                sData.SetStartAngle(BackhandDict[nextNotes[0]._cutDirection]);
                sData.SetEndAngle(BackhandDict[nextNotes[^1]._cutDirection]);
            } else {
                sData.SetStartAngle(ForehandDict[nextNotes[0]._cutDirection]);
                sData.SetEndAngle(ForehandDict[nextNotes[^1]._cutDirection]);
            }

            // Update player position for swing, and EBPM
            sData.playerHorizontalOffset = _playerXOffset;
            sData.playerVerticalOffset = _playerYOffset;
            sData.swingEBPM = SwingEBPM(_BPM, nextNotes[0]._time - lastSwing.notes[0]._time);
            if (sData.IsReset) { sData.swingEBPM *= 2; }

            // Invert Check
            if (sData.isInverted == false)
            {
                for (int last = 0; last < lastSwing.notes.Count; last++)
                {
                    for (int next = 0; next < nextNotes.Count; next++)
                    {
                        if (IsInvert(lastSwing.notes[last], nextNotes[next]))
                        {
                            sData.isInverted = true;
                            break;
                        }
                    }
                }
            }

            if (sData.swingParity == lastSwing.swingParity && sData.resetType != RESET_TYPE.BOMB) { sData.resetType = RESET_TYPE.REBOUND; }
            if (sData.notes[0]._cutDirection == 8 && sData.notes.Count == 1) CalculateDotDirection(lastSwing, ref sData);

            return sData;
        }

        /// <summary>
        /// Calculates and returns a list of swing data for a set of map objects
        /// </summary>
        /// <param name="mapObjects">Information about notes, walls and obstacles</param>
        /// <param name="rightHandSwings">Is Right Hand Notes?</param>
        /// <returns></returns>
        private static List<SwingData> GetSwingData(MapObjects mapObjects, bool rightHandSwings)
        {
            List<SwingData> result = new();
            _rightHand = rightHandSwings;
            _mapObjects = mapObjects;

            float sliderPrecision = 1 / 6f;
            List<Note> notesInSwing = new();

            for (int i = 0; i < _mapObjects.Notes.Count - 1; i++)
            {
                Note currentNote = _mapObjects.Notes[i];
                Note nextNote = _mapObjects.Notes[i + 1];

                notesInSwing.Add(currentNote);

                if (MathF.Abs(currentNote._time - nextNote._time) < sliderPrecision) {
                    if(nextNote._cutDirection == 8 || notesInSwing[^1]._cutDirection == 8 ||
                        currentNote._cutDirection == nextNote._cutDirection ||
                        MathF.Abs(ForehandDict[currentNote._cutDirection] - ForehandDict[nextNote._cutDirection]) <= 45) 
                    { continue; }
                }

                // If first swing, figure out starting orientation
                if (result.Count == 0) {
                    if (currentNote._cutDirection == 0 || currentNote._cutDirection == 4 || currentNote._cutDirection == 5) {
                        SwingData sData = new(PARITY_STATE.BACKHAND, notesInSwing);
                        result.Add(sData);
                    } else {
                        SwingData sData = new(PARITY_STATE.FOREHAND, notesInSwing);
                        result.Add(sData);
                    }
                    notesInSwing.Clear();
                    continue;
                }

                SwingData nextSwing = AssessSwing(result[^1], notesInSwing);
                //if (nextSwing.swingParity == PARITY_STATE) { i--; }
                result.Add(nextSwing);
                notesInSwing.Clear();
            }
            return result;
        }

        // Calculates how a dot note should be swung according to the prior swing.
        private static void CalculateDotDirection(SwingData lastSwing, ref SwingData currentSwing)
        {
            Note dotNote = currentSwing.notes[0];
            Note lastNote = lastSwing.notes[^1];

            int orientation = CutDirFromNoteToNote(lastNote, dotNote);

            if (dotNote._lineIndex == lastNote._lineIndex && dotNote._lineLayer == lastNote._lineLayer)
            {
                orientation = opposingCutDict[orientation];
            }

            float angle = (lastSwing.swingParity == PARITY_STATE.BACKHAND) ?
                BackhandDict[orientation] :
                ForehandDict[orientation];

            if (lastSwing.endPos.rotation == 0 && angle == -180) angle = 0;

            // Checks for angle based on X difference between the 2 notes
            float xDiff = MathF.Abs(dotNote._lineIndex- lastNote._lineLayer);
            if (xDiff < 3) angle = angle < -90 ? -90 : angle > 45 ? 45 : angle;
            if (xDiff == 3) angle = angle < -90 ? -90 : angle > 90 ? 90 : angle;

            // Clamps inwards backhand hits if the note is only 1 away
            if (xDiff == 1 && lastNote._lineIndex > dotNote._lineIndex && _rightHand && currentSwing.swingParity == PARITY_STATE.FOREHAND) angle = 0;
            else if (xDiff == 1 && lastNote._lineIndex < dotNote._lineIndex && !_rightHand && currentSwing.swingParity == PARITY_STATE.BACKHAND) angle = 0;

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);

            return;
        }


        #region UTILITY FUNCTIONS

        private static Vector2 Normalize(Vector2 a)
        {
            float length = (float)Math.Sqrt(a.X * a.X + a.Y * a.Y);
            return new Vector2(a.X / length, a.Y / length);
        }
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
        /// Returns true if the next note is an invert to the last
        /// </summary>
        /// <param name="lastNote">The note hit last</param>
        /// <param name="nextNote">The next note to be hit</param>
        /// <returns></returns>
        private static bool IsInvert(Note lastNote, Note nextNote)
        {
            // Is Note B in the direction of Note A's cutDirection.
            switch (lastNote._cutDirection)
            {
                case 0:
                    // Up note
                    if (nextNote._lineLayer > lastNote._lineLayer) return true;
                    break;
                case 1:
                    // Down note
                    if (nextNote._lineLayer < lastNote._lineLayer) return true;
                    break;
                case 2:
                    // Left note
                    if (nextNote._lineIndex < lastNote._lineIndex) return true;
                    break;
                case 3:
                    // Right note
                    if (nextNote._lineIndex > lastNote._lineIndex) return true;
                    break;
                case 4:
                    // Up, Left note
                    if (nextNote._lineIndex < lastNote._lineIndex &&
                        nextNote._lineLayer > lastNote._lineLayer) return true;
                    break;
                case 5:
                    // Up, Right note
                    if (nextNote._lineIndex > lastNote._lineIndex &&
                        nextNote._lineLayer > lastNote._lineLayer) return true;
                    break;
                case 6:
                    // Down, Left note
                    if (nextNote._lineIndex < lastNote._lineIndex &&
                        nextNote._lineLayer < lastNote._lineLayer) return true;
                    break;
                case 7:
                    // Down, Right note
                    if (nextNote._lineIndex > lastNote._lineIndex &&
                        nextNote._lineLayer < lastNote._lineLayer) return true;
                    break;
            }
            return false;
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
        #endregion
    }
}