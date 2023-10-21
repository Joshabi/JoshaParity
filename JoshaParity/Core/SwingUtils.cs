using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
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
                .Select(pair => new NotePair { noteA = pair.noteA, noteB = pair.noteB })
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
        public static int CutDirFromAngleParity(float angle, Parity parity, float interval = 0.0f)
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
                SwingDataGeneration.ForehandDict.FirstOrDefault(x => x.Value == roundedAngle).Key :
                SwingDataGeneration.BackhandDict.FirstOrDefault(x => x.Value == roundedAngle).Key;
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
    }
}
