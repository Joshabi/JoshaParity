namespace JoshaUtils
{

    /// <summary>
    /// Current Orientation States for any given hand
    /// </summary>
    public enum PARITY_STATE
    {
        FOREHAND,
        BACKHAND,
        RESET,
        BOMB_RESET,
        UNKNOWN
    }

    /// <summary>
    /// Contains map entities (Bombs, Walls, Notes)
    /// </summary>
    public struct MapObjects{
        // Map Entity Lists
        private List<Note> _mapNotes;
        private List<Note> _mapBombs;
        private List<Obstacle> _mapWalls;

        // Getters
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
        public float timeStamp = 0;
        public PARITY_STATE swingParity;
        public bool rightHand = true;
        public bool isReset = false;
        public bool isInverted = false;
        public float swingEBPM = 0;
        public float curPlayerHorizontalOffset = 0;
        public float curPlayerVerticalOffset = 0;
        public Note? prevNote = null;
        public List<Note> notes = new();

        public SwingData(PARITY_STATE newParity, List<Note> swingNotes)
        {
            swingParity = newParity;
            notes.AddRange(swingNotes);
            timeStamp = notes[0]._time;
        }
        public override string ToString()
        {
            String returnString = $"Swing Note/s or Bomb/s {timeStamp} " +
                $"|| Parity of this swing: {swingParity}" +
                $"\nHorizontal Player Offset: {curPlayerHorizontalOffset} || Vertical Player Offset: {curPlayerVerticalOffset}" +
                $"\nSwing EBPM: {swingEBPM} || Is Invert? {isInverted} || Is Reset? {isReset}";
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
        // FOR RIGHT HAND (slightly mirr'd depending on hand
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
        private static readonly Dictionary<int, float> rightForehandDict = new()
        { { 0, -180 }, { 1, 0 }, { 2, -90 }, { 3, 90 }, { 4, -135 }, { 5, 135 }, { 6, -45 }, { 7, 45 }, { 8, 0 } };
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
        private static readonly Dictionary<int, float> rightBackhandDict = new()
        { { 0, 0 }, { 1, -180 }, { 2, 90 }, { 3, -90 }, { 4, 45 }, { 5, -45 }, { 6, 135 }, { 7, -135 }, { 8, 0 } };

        // FOR LEFT HAND
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Forehand Swing
        private static readonly Dictionary<int, float> leftForehandDict = new()
        { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
        // Cut Direction -> Angle from Neutral (up down 0 degrees) given a Backhand Swing
        private static readonly Dictionary<int, float> leftBackhandDict = new()
        { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };

        private static List<int> forehandResetDict = new()
        { 1, 2, 3, 6, 7, 8 };
        private static List<int> backhandResetDict = new()
        { 0, 4, 5, 8 };

        private static Dictionary<int, float> ForehandDict { get { return (_rightHand) ? rightForehandDict : leftForehandDict; } }
        private static Dictionary<int, float> BackhandDict { get { return (_rightHand) ? rightBackhandDict : leftBackhandDict; } }

        #endregion

        #region Variables
        private static MapObjects _mapObjects;
        private static float _curMapBPM;
        private static bool _rightHand = true;
        private static float _playerHorizontalOffset = 0;
        private static float _playerVerticalOffset = 0;
        private static float _lastWallTimestamp;
        private static float _lastCrouchTimestamp;
        #endregion

        /// <summary>
        /// Called to check a specific map difficulty file
        /// </summary>
        /// <param name="mapDif">Map Difficulty to check</param>
        /// <param name="BPM">BPM of the map</param>
        public static void Run(MapDifficulty mapDif, float BPM)
        {
            // Reset Operating Variables
            _curMapBPM = BPM;
            _playerHorizontalOffset = 0;
            _playerVerticalOffset = 0;

            // Seperate notes, bombs and walls
            List<Note> leftHandedNotes = mapDif._notes.
                Where(n => n._type == 0).ToList();
            List<Note> rightHandedNotes = mapDif._notes.
                Where(n => n._type == 1).ToList();
            List<Note> bombs = mapDif._notes.
                Where(n => n._type == 3).ToList();
            List<Obstacle> walls = mapDif._obstacles.
                Where(n => n._lineIndex < 3).ToList();
            //Console.WriteLine("Dodge Wall Count: " + walls.Count);

            // Calculate swing data for both hands
            MapObjects mapObj = new(rightHandedNotes, bombs, walls);
            List<SwingData> rightHandSD = GetSwingData(mapObj, true);
            //foreach (SwingData data in rightHandSD) { Console.WriteLine(data.ToString()); }
            mapObj.Notes = leftHandedNotes;
            List<SwingData> leftHandSD = GetSwingData(mapObj, false);

            // Combine swing data and sort
            List<SwingData> combinedSD = rightHandSD;
            combinedSD.AddRange(leftHandSD);
            combinedSD = combinedSD.OrderBy(x => x.timeStamp).ToList();
        }

        /// <summary>
        /// Given the last swing, and the next note, perform a check to see if it complies with parity, and return it
        /// </summary>
        /// <param name="lastSwing">The swing data of the last taken swing</param>
        /// <param name="nextNote">Next note to be hit</param>
        /// <param name="bombsBetweenNotes">Any bombs inbetween the last hit and next</param>
        /// <returns></returns>
        public static PARITY_STATE ParityCheck(SwingData lastSwing, Note nextNote)
        {
            // AFN: Angle from neutral
            // Assuming a forehand down hit is neutral, and a backhand up hit
            // Rotating the hand inwards goes positive, and outwards negative
            // Using a list of definitions, turn cut direction into an angle, and check
            // if said angle makes sense.
            var nextAFN = (lastSwing.swingParity != PARITY_STATE.FOREHAND) ?
                BackhandDict[lastSwing.notes[0]._cutDirection] - ForehandDict[nextNote._cutDirection] :
                ForehandDict[lastSwing.notes[0]._cutDirection] - BackhandDict[nextNote._cutDirection];

            // If the next AFN exceeds 180 or -180, this means the algo had to triangle / reset
            if (nextAFN > 180 || nextAFN < -180)
            {
                Console.WriteLine($"Attempted: {BackhandDict[lastSwing.notes[0]._cutDirection] - ForehandDict[nextNote._cutDirection]} or {ForehandDict[lastSwing.notes[0]._cutDirection] - BackhandDict[nextNote._cutDirection]}" +
                    $"\n[PARITY WARNING] >> Had to Triangle at {nextNote._time} with an Angle from Neutral of {nextAFN}." +
                    $"\nLast swing was {lastSwing.swingParity} and current player offset is {_playerHorizontalOffset}");
                return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            }
            else { return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND; }
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
            SwingData sData = new(PARITY_STATE.UNKNOWN, nextNotes);

            // Check if there is a bomb causing a reset
            List<Note> bombsInbetween = _mapObjects.Bombs.FindAll(x => x._time > lastSwing.timeStamp && x._time < nextNotes[0]._time);
            Note? bomb = IsBombReset(bombsInbetween, lastSwing);
            if (bomb != null)
            {
                List<Note> bombs = new(); bombs.Add(bomb);
                sData = new(PARITY_STATE.BOMB_RESET, bombs);
                sData.isReset = true;
                return sData;
            }

            #region Wall Detection

            // Dodge Walls and Duck Walls Detection.
            // Accounts for leaning bomb resets.
            List<Obstacle> wallsInbetween = _mapObjects.Obstacles.FindAll(x => x._time > lastSwing.timeStamp && x._time < nextNotes[0]._time);
            if(wallsInbetween != null) { 
                foreach(Obstacle wall in wallsInbetween)
                {
                    // Duck wall detection
                    if ((wall._width >= 3 && (wall._lineIndex <= 1)) || (wall._width == 2 && wall._lineIndex == 1)) {
                        _playerVerticalOffset = -1;
                        _lastCrouchTimestamp = wall._time + wall._duration;
                        break;
                    }

                    // Dodge wall detection
                    if (wall._lineIndex == 1 || wall._lineIndex == 2)
                    {
                        _playerHorizontalOffset = (wall._lineIndex == 1) ? 1 : -1;
                        _lastWallTimestamp = wall._time + wall._duration;
                    }
                }
            }

            // If time since dodged last exceeds a set amount in Seconds (might convert to ms
            // for everything down the line tbh), undo dodge
            var wallEndCheckTime = 0.5f;
            if (BeatToSeconds(_curMapBPM, nextNotes[0]._time - _lastWallTimestamp) > wallEndCheckTime) {
                _playerHorizontalOffset = 0;
            }
            if (BeatToSeconds(_curMapBPM, nextNotes[0]._time - _lastCrouchTimestamp) > wallEndCheckTime) {
                _playerVerticalOffset = 0;
            }

            #endregion

            // Work out Parity
            sData.swingParity = ParityCheck(lastSwing, nextNotes[0]);
            if (sData.swingParity == lastSwing.swingParity) { sData.isReset = true; }

            // Update player position for swing, and EBPM
            sData.curPlayerHorizontalOffset = _playerHorizontalOffset;
            sData.curPlayerVerticalOffset = _playerVerticalOffset;
            sData.rightHand = _rightHand;
            sData.swingEBPM = SwingEBPM(_curMapBPM, nextNotes[0]._time - lastSwing.notes[0]._time);
            if (sData.isReset) { sData.swingEBPM *= 2; }

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

            float sliderPrecision = 1 / 12f;
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
                        SwingData sData = new(PARITY_STATE.BACKHAND, notesInSwing) { timeStamp = currentNote._time };
                        result.Add(sData);
                    } else {
                        SwingData sData = new(PARITY_STATE.FOREHAND, notesInSwing) { timeStamp = currentNote._time };
                        result.Add(sData);
                    }
                    notesInSwing.Clear();
                    continue;
                }

                SwingData nextSwing = AssessSwing(result[^1], notesInSwing);
                if (nextSwing.swingParity == PARITY_STATE.BOMB_RESET) { i--; }
                result.Add(nextSwing);
                notesInSwing.Clear();
            }
            return result;
        }

        #region UTILITY FUNCTIONS
        /// <summary>
        /// Returns Note (Bomb) causing a reset, if non, returns null
        /// </summary>
        /// <param name="bombs">List of "Note" between swings</param>
        /// <param name="lastSwing">The last swing information</param>
        /// <returns></returns>
        private static Note? IsBombReset(List<Note> bombs, SwingData lastSwing) {
                
            var currentAFN = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ?
            BackhandDict[lastSwing.notes[0]._cutDirection] :
            ForehandDict[lastSwing.notes[0]._cutDirection];

            // Checks if either bomb reset bomb locations exist
            var bombCheckLayer = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? 0 : 2;
            Note? containsRightmost = bombs.Find(x => x._lineIndex == 2 + _playerHorizontalOffset && x._lineLayer == bombCheckLayer);
            Note? containsLeftmost = bombs.Find(x => x._lineIndex == 1 + _playerHorizontalOffset && x._lineLayer == bombCheckLayer);

            // If there is a bomb, potentially a bomb reset
            if ((_rightHand && containsLeftmost != null) || (!_rightHand && containsRightmost != null))
            {
                bool shouldReset = false;
                List<int> resetDirectionList = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? forehandResetDict : backhandResetDict;
                if (resetDirectionList.Contains(lastSwing.notes[0]._cutDirection)) {
                    shouldReset = true;
                }

                // CURRENT ISSUES:
                // Cannot figure out non-resets with bombs nearby involving a DOT note prior.
                // Need to implement a backtracking checker pass to fix these (Since it will usually cause a reset later down the line)
                // Could add a pass to check from the last bomb reset to potential triangle to figure out if not resetting fixes it?

                if(shouldReset) return (_rightHand) ? containsRightmost : containsLeftmost;
                return null;
            }
            return null;
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