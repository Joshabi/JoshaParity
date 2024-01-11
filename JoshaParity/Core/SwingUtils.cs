using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Contains position and rotation information for a given swing
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
    /// Note and Swing related functionalities
    /// </summary>
    public static class SwingUtils
    {
        // Contains a list of directional vectors
        public static readonly Vector2[] DirectionalVectors =
        {
            new Vector2(0, 1),   // up
            new Vector2(0, -1),  // down
            new Vector2(-1, 0),  // left
            new Vector2(1, 0),   // right
            new Vector2(-1, 1),   // up left
            new Vector2(1, 1),  // up right
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
            { new Vector2(-1, 1), 4 },
            { new Vector2(1, 1), 5 },
            { new Vector2(-1, -1), 6 },
            { new Vector2(1, -1), 7 },
            { new Vector2(0, 0), 8 }
        };

        // Gives the opposing cut direction for a cutID
        public static readonly Dictionary<int, int> OpposingCutDict = new Dictionary<int, int>()
        { { 0, 1 }, { 1, 0 }, { 2, 3 }, { 3, 2 }, { 5, 7 }, { 7, 5 }, { 4, 6 }, { 6, 4 }, { 8, 8 } };

        /// <summary>
        /// Used to sort notes in a swing where they are snapped to the same beat, and not all dots
        /// </summary>
        /// <param name="notesToSort">List of notes you want to sort</param>
        /// <returns></returns>
        public static List<Note> SnappedSwingSort(List<Note> notesToSort)
        {
            // Refactored Method:
            if (notesToSort.Any(x => x.d != 8)) {
                Vector2 totalDirection = Vector2.Zero;
                foreach (var note in notesToSort)
                {
                    if (note.d == 8) continue;
                    totalDirection += DirectionalVectors[note.d];
                }
                var avgDirection = totalDirection / notesToSort.Count;

                return notesToSort.OrderBy(x => Vector2.Dot(new Vector2(x.x, x.y), avgDirection)).ToList();
            }

            // Purely used on entirely dot swings. Likely should be depreciated and removed
            // as dot stacked swings get re-ordered based on parity.
            // Old Method:

            // Find the two notes that are furthest apart and their positions
            NotePair farNotes = FurthestNotesFromList(notesToSort);
            Vector2 noteAPos = new Vector2(farNotes.noteA.x, farNotes.noteA.y);
            Vector2 noteBPos = new Vector2(farNotes.noteB.x, farNotes.noteB.y);

            // Get the direction vector ATB
            // Check if any cut directions oppose this, if so, flip to BTA
            Vector2 atb = noteBPos - noteAPos;
            if (notesToSort.Any(x => x.d != 8))
            {
                bool reverseOrder = notesToSort.Any(note => note.d != 8 && Vector2.Dot(DirectionalVectors[note.d], atb) < 0);
                if (reverseOrder) atb = -atb;
            }

            // Sort the cubes according to their position along the direction vector
            List<Note> sortedNotes = notesToSort.OrderBy(note => Vector2.Dot(new Vector2(note.x, note.y) - noteAPos, atb)).ToList();
            return sortedNotes;
        }

        /// <summary>
        /// Gets 2 furthest notes from a list of notes
        /// </summary>
        /// <param name="notes">Notes to compare</param>
        /// <returns></returns>
        public static NotePair FurthestNotesFromList(List<Note> notes)
        {
            // Find the two notes that are furthest apart
            NotePair furthestNotes = notes
                .SelectMany(b1 => notes.Select(b2 => new NotePair { noteA = b1, noteB = b2 }))
                .OrderByDescending(pair => Vector2.Distance(new Vector2(pair.noteA.x, pair.noteA.y), new Vector2(pair.noteB.x, pair.noteB.y)))
                .First();
            return furthestNotes;
        }

        /// <summary>
        /// Returns a cut direction given angle and parity, and optionally a rounding interval
        /// </summary>
        /// <param name="angle">Saber Rotation from neutral</param>
        /// <param name="parity">Current saber parity</param>
        /// <param name="interval">Rounding interval (Intervals of 45)</param>
        /// <returns></returns>
        public static int CutDirFromAngleParity(float angle, Parity parity, bool rightHand = true, float interval = 0.0f)
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
                ParityUtils.ForehandDict(rightHand).FirstOrDefault(x => x.Value == roundedAngle).Key :
                ParityUtils.BackhandDict(rightHand).FirstOrDefault(x => x.Value == roundedAngle).Key;
        }

        /// <summary>
        /// Given a Vector2 direction, calculate a cutDirection ID for that direction
        /// </summary>
        /// <param name="direction">Direction to cut</param>
        /// <returns></returns>
        public static int CutDirFromVector(Vector2 direction)
        {
            // Get the direction from first to last note, get the lowest dot product when compared
            // to all possible direction vectors for notes, then calculates a cut direction and invert it
            direction = Vector2.Normalize(direction);
            Vector2 lowestDotProduct = DirectionalVectors.OrderBy(v => Vector2.Dot(direction, v)).First();
            Vector2 cutDirection = new Vector2((float)Math.Round(lowestDotProduct.X), (float)Math.Round(lowestDotProduct.Y));
            int orientation = DirectionalVectorToCutDirection[cutDirection];
            return orientation;
        }

        /// <summary>
        /// Given 2 notes, calculate a cutDirection ID for the direction from first to last note
        /// </summary>
        /// <param name="firstNote">First note</param>
        /// <param name="lastNote">Second note</param>
        /// <returns></returns>
        public static int CutDirFromNoteToNote(Note firstNote, Note lastNote) {
            return CutDirFromVector(new Vector2(lastNote.x, lastNote.y) - new Vector2(firstNote.x, firstNote.y));
        }

        /// <summary>
        /// Clamps float value between a minimum and maximum
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float Clamp(float value, float min, float max) {
            return value < min ? min : value > max ? max : value;
        }

        /// <summary>
        /// Used to calculate appropriate angle for non-snapped multi-note swings
        /// </summary>
        /// <param name="currentSwing">Current Swing being calculated</param>
        internal static void SliderAngleCalc(ref SwingData currentSwing)
        {
            Note firstNote = currentSwing.notes[0];
            Note lastNote = currentSwing.notes[currentSwing.notes.Count - 1];

            int firstCutDir;
            int lastCutDir;
            if (currentSwing.notes.Count == 2) {
                // If arrow, take the cutDir, else approximate direction from first to last note.
                firstCutDir = (firstNote.d != 8) ? firstNote.d : CutDirFromNoteToNote(lastNote, firstNote);
                lastCutDir = (lastNote.d != 8) ? lastNote.d : CutDirFromNoteToNote(lastNote, firstNote);
            } else {
                // If arrow, take the cutDir, else approximate direction from first to last note.
                Note middleNote = currentSwing.notes[currentSwing.notes.Count / 2];
                firstCutDir = (firstNote.d != 8) ? firstNote.d : CutDirFromNoteToNote(middleNote, firstNote);
                lastCutDir = (lastNote.d != 8) ? lastNote.d : CutDirFromNoteToNote(lastNote, middleNote);
            }

            float startAngle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(currentSwing.rightHand)[firstCutDir] : ParityUtils.BackhandDict(currentSwing.rightHand)[firstCutDir];
            float endAngle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(currentSwing.rightHand)[lastCutDir] : ParityUtils.BackhandDict(currentSwing.rightHand)[lastCutDir];
            currentSwing.SetStartAngle(startAngle);
            currentSwing.SetEndAngle(endAngle);
        }

        /// <summary>
        /// Given a previous swing and current swing (all dot notes), calculate saber rotation
        /// </summary>
        /// <param name="lastSwing">Swing that came prior to this</param>
        /// <param name="currentSwing">Swing you want to calculate swing angle for</param>
        internal static void SnappedDotSwingAngleCalc(SwingData lastSwing, ref SwingData currentSwing)
        {
            // Get the first and last note based on array order
            Note firstNote = currentSwing.notes[0];
            Note lastNote = currentSwing.notes[currentSwing.notes.Count - 1];

            int orientation = SwingUtils.CutDirFromNoteToNote(firstNote, lastNote);
            int altOrientation = SwingUtils.CutDirFromNoteToNote(lastNote, firstNote);

            float angle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(currentSwing.rightHand)[orientation] : ParityUtils.BackhandDict(currentSwing.rightHand)[orientation];
            float altAngle = (currentSwing.swingParity == Parity.Forehand) ? ParityUtils.ForehandDict(currentSwing.rightHand)[altOrientation] : ParityUtils.BackhandDict(currentSwing.rightHand)[altOrientation];

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
        /// Given previous and current swing (singular dot note), calculate and clamp saber rotation
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
            }
            else
            {
                // Get Cut Dir from last note to dot note
                int orientation = CutDirFromNoteToNote(lastNote, dotNote);
                angle = (lastSwing.swingParity == Parity.Forehand && currentSwing.resetType == ResetType.None) ?
                    ParityUtils.ForehandDict(currentSwing.rightHand)[orientation] :
                    ParityUtils.BackhandDict(currentSwing.rightHand)[orientation];
            }

            // Arbitrary clamping, may be replaced when badcut detection is implemented
            // and proper positioning is included in SwingData
            if (clamp)
            {
                // If clamp, then apply clamping to the angle based on the ruleset below
                int xDiff = Math.Abs(dotNote.x - lastNote.x);
                int yDiff = Math.Abs(dotNote.y - lastNote.y);
                if (xDiff == 3) { angle = Clamp(angle, -90, 90); }
                else if (xDiff == 2) { angle = Clamp(angle, -45, 45); }
                else if (xDiff == 0 && yDiff > 1) { angle = 0; }
                else { angle = Clamp(angle, -45, 0); }
            }

            currentSwing.SetStartAngle(angle);
            currentSwing.SetEndAngle(angle);
        }
    
        /// <summary>
        /// Performs validation on a Note Object
        /// </summary>
        /// <param name="note">Note to validate</param>
        /// <returns></returns>
        public static Note ValidateNote(Note note) {
            if (note.d > 8) { note.d = 8; } else if ( note.d < 0) { note.d = 0; }
            return note;
        }
    }
}
