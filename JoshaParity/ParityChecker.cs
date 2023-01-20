namespace JoshaUtils
{

    /// <summary>
    /// Current Orientation States for any given hand
    /// </summary>
    public enum PARITY_STATE
    {
        FOREHAND,
        BACKHAND,
        UNKNOWN,
        RESET
    }

    public class SwingData
    {
        public float timeStamp = 0;
        public PARITY_STATE swingParity;
        public bool isReset = false;
        public bool isInverted = false;
        public float swingEBPM = 0;
        public float curPlayerHorizontalOffset = 0;
        public float curPlayerVerticalOffset = 0;
        public List<Note> notes = new();

        public SwingData(PARITY_STATE newParity, List<Note> swingNotes)
        {
            swingParity = newParity;
            notes.AddRange(swingNotes);
        }

        public void Reset() { notes.Clear(); swingParity = PARITY_STATE.UNKNOWN; }
        public override string ToString()
        {
            String returnString = $"Swing Note/s {timeStamp} " +
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

        private static Dictionary<int, float> ForehandDict { get { return (rightHand) ? rightForehandDict : leftForehandDict; } }
        private static Dictionary<int, float> BackhandDict { get { return (rightHand) ? rightBackhandDict : leftBackhandDict; } }

        #endregion

        private static float curMapBPM;
        private static bool rightHand = true;
        private static float playerHorizontalOffset = 0;
        private static float playerVerticalOffset = 0;
        private static float lastWallTimestamp;
        private static float lastCrouchTimestamp;

        public static void Run(MapDifficulty mapDif, float BPM)
        {
            rightHand = true;
            curMapBPM = BPM;
            playerHorizontalOffset = 0;
            playerVerticalOffset = 0;

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
            List<SwingData> rightHandSD = GetHandSwingData(rightHandedNotes, bombs, walls, true);
            List<SwingData> leftHandSD = GetHandSwingData(leftHandedNotes, bombs, walls, false);

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
        public static PARITY_STATE ParityCheck(SwingData lastSwing, Note nextNote, List<Note> bombs)
        {
            // AFN: Angle from neutral
            // Assuming a forehand down hit is neutral, and a backhand up hit
            // Rotating the hand inwards goes positive, and outwards negative
            // Using a list of definitions, turn cut direction into an angle, and check
            // if said angle makes sense.
            var nextAFN = (lastSwing.swingParity != PARITY_STATE.FOREHAND) ?
                BackhandDict[lastSwing.notes[0]._cutDirection] - ForehandDict[nextNote._cutDirection] :
                ForehandDict[lastSwing.notes[0]._cutDirection] - BackhandDict[nextNote._cutDirection];

            var currentAFN = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ?
                BackhandDict[lastSwing.notes[0]._cutDirection] :
                ForehandDict[lastSwing.notes[0]._cutDirection];

            // Checks if either bomb reset bomb locations exist
            var bombCheckLayer = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? 0 : 2;
            var containsRightmost = bombs.Find(x => x._lineIndex == 2 + playerHorizontalOffset && x._lineLayer == bombCheckLayer);
            var containsLeftmost = bombs.Find(x => x._lineIndex == 1 + playerHorizontalOffset && x._lineLayer == bombCheckLayer);

            // If there is a bomb, potentially a bomb reset
            if ((rightHand && containsLeftmost != null) || (!rightHand && containsRightmost != null))
            {
                // For decor bombs, anything more then 135 rotation either side (AFN) is considered possible to
                // ignore the bombs (135 Forehand would be palm up and thus essentially a down hit away from bombs so no reset)
                if (MathF.Abs(currentAFN) <= 90) { return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND; }
                return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            }

            // If the next AFN exceeds 180 or -180, this means the algo had to triangle / reset
            if (nextAFN > 180 || nextAFN < -180)
            {
                Console.WriteLine($"Attempted: {BackhandDict[lastSwing.notes[0]._cutDirection] - ForehandDict[nextNote._cutDirection]} or {ForehandDict[lastSwing.notes[0]._cutDirection] - BackhandDict[nextNote._cutDirection]}" +
                    $"\n[PARITY WARNING] >> Had to Triangle at {nextNote._time} with an Angle from Neutral of {nextAFN}." +
                    $"\nLast swing was {lastSwing.swingParity} and current player offset is {playerHorizontalOffset}");
                return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            }
            else { return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND; }
        }

        private static List<SwingData> GetHandSwingData(List<Note> notes, List<Note> bombs, List<Obstacle> dodgeWalls, bool rightHandSwings)
        {
            List<SwingData> result = new();
            rightHand = rightHandSwings;

            float sliderPrecision = 1 / 12f;
            List<Note> notesInSwing = new();

            for (int i = 0; i < notes.Count - 1; i++)
            {
                Note currentNote = notes[i];
                Note nextNote = notes[i + 1];

                notesInSwing.Add(currentNote);

                // If precision falls under "Slider", or time stamp is the same, run
                // checks to figure out if it is a slider, window, stack ect..
                if (MathF.Abs(currentNote._time - nextNote._time) < sliderPrecision)
                {
                    bool isStack = false;
                    if (currentNote._cutDirection == nextNote._cutDirection) isStack = true;
                    if (nextNote._cutDirection == 8) isStack = true;
                    if (MathF.Abs(ForehandDict[currentNote._cutDirection] - ForehandDict[nextNote._cutDirection]) <= 45) isStack = true;
                    // For now hard coded to accept dot then arrow as correct no matter what
                    if (notesInSwing[^1]._cutDirection == 8) isStack = true;
                    if (isStack)
                    {
                        continue;
                    }
                }

                // Assume by default swinging forehanded
                SwingData sData = new(PARITY_STATE.FOREHAND, notesInSwing)
                {
                    timeStamp = notesInSwing[0]._time
                };

                if (result.Count == 0)
                {
                    result.Add(sData);
                    notesInSwing.Clear();
                    continue;
                }
                else
                {
                    // If previous swing exists
                    SwingData lastSwing = result[^1];
                    Note lastNote = lastSwing.notes[^1];

                    // Get Walls Between the Swings
                    List<Obstacle> wallsInbetween = dodgeWalls.FindAll(x => x._time > lastNote._time && x._time < notesInSwing[0]._time);
                    if (wallsInbetween != null)
                    {
                        foreach (var wall in wallsInbetween)
                        {
                            // Duck wall detection
                            if ((wall._width >= 3 && (wall._lineIndex <= 1)) || (wall._width == 2 && wall._lineIndex == 1))
                            {
                                //Console.WriteLine($"Detected Duck wall at: {wall._time}");
                                playerVerticalOffset = -1;
                                lastCrouchTimestamp = wall._time + wall._duration;
                            }

                            // Dodge wall detection
                            if (wall._lineIndex == 1 || wall._lineIndex == 2)
                            {
                                //Console.WriteLine($"Detected Dodge Wall at: {wall._time}");
                                playerHorizontalOffset = (wall._lineIndex == 1) ? 1 : -1;
                                lastWallTimestamp = wall._time + wall._duration;
                            }
                        }
                    }

                    // If time since dodged last exceeds a set amount in Seconds (might convert to ms
                    // for everything down the line tbh), undo dodge
                    var wallEndCheckTime = 0.5f;
                    if (BeatToSeconds(curMapBPM, notesInSwing[0]._time - lastWallTimestamp) > wallEndCheckTime)
                    {
                        playerHorizontalOffset = 0;
                    }
                    if (BeatToSeconds(curMapBPM, notesInSwing[0]._time - lastCrouchTimestamp) > wallEndCheckTime)
                    {
                        playerVerticalOffset = 0;
                    }

                    sData.curPlayerHorizontalOffset = playerHorizontalOffset;
                    sData.curPlayerVerticalOffset = playerVerticalOffset;
                    sData.swingEBPM = SwingEBPM(curMapBPM, currentNote._time - lastNote._time);
                    if (sData.isReset) { sData.swingEBPM *= 2; }

                    // Work out Parity
                    List<Note> bombsBetweenSwings = bombs.FindAll(x => x._time > lastNote._time && x._time < notesInSwing[^1]._time);
                    sData.swingParity = ParityCheck(lastSwing, notesInSwing[0], bombsBetweenSwings);
                    if (sData.swingParity == lastSwing.swingParity) { sData.isReset = true; }

                    // Invert Check
                    if (sData.isInverted == false)
                    {
                        for (int last = 0; last < lastSwing.notes.Count; last++)
                        {
                            for (int next = 0; next < notesInSwing.Count; next++)
                            {
                                if (IsInvert(lastSwing.notes[last], notesInSwing[next]))
                                {
                                    sData.isInverted = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                // Add swing to list
                result.Add(sData);
                notesInSwing.Clear();
            }
            return result;
        }

        #region UTILITY FUNCTIONS
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
        private static float GetCurvePointValue(float[][] curvePoints, float keyValue)
        {
            for (int i = 0; i < curvePoints.Length; i++)
            {
                if (keyValue > curvePoints[i][0] &&
                    keyValue <= curvePoints[i + 1][0])
                {
                    var reduction = keyValue / curvePoints[i + 1][0];
                    return (curvePoints[i + 1][1] * reduction);
                }
                else if (keyValue < curvePoints[0][0]) { return curvePoints[0][1]; }
                else if (keyValue > curvePoints[^1][0]) { return curvePoints[^1][1]; }
            }
            return 0;
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
        public static float DiffNormalize(float min, float max, float value, float maxScale = 1)
        {
            return (value - min) / (max - min) * maxScale;
        }
        public static float BeatToSeconds(float BPM, float beatDiff)
        {
            return (beatDiff / (BPM / 60));
        }
        #endregion
    }
}