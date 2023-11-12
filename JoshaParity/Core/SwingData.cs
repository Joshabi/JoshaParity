using System.Collections.Generic;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Swing Reset Type
    /// </summary>
    public enum ResetType
    {
        None = 0,  // Swing does not force a reset, or triangle
        Bomb = 1,    // Swing forces a reset due to bombs
        Rebound = 2,   // Swing forces an additional swing due to rotation
    }

    /// <summary>
    /// Contains data for a given swing
    /// </summary>
    public struct SwingData
    {
        public Parity swingParity;
        public SwingType swingType;
        public ResetType resetType;
        public float swingStartBeat;
        public float swingEndBeat;
        public float swingEBPM;
        public List<Note> notes = new();
        public PositionData startPos;
        public PositionData endPos;
        public bool rightHand;
        public Vector2 playerOffset;

        /// <summary>
        /// Creates a new SwingData object with default values
        /// </summary>
        public SwingData()
        {
            swingParity = Parity.Forehand;
            swingType = SwingType.Undecided;
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

        /// <summary>
        /// Creates a new SwingData object from a list of notes
        /// </summary>
        /// <param name="type">Type of swing</param>
        /// <param name="notes">Notes making up this swing</param>
        /// <param name="rightHand">Are the notes right handed?</param>
        /// <param name="startingSwing">Is this the first swing in the map?</param>
        public SwingData(SwingType type, List<Note> notes, bool rightHand, bool startingSwing = false)
        {
            this.notes = notes;
            swingParity = Parity.Undecided;
            swingType = type;
            swingStartBeat = notes[0].b;
            swingEndBeat = notes[notes.Count - 1].b;
            this.rightHand = rightHand;

            SetStartPosition(notes[0].x, notes[0].y);
            SetEndPosition(notes[notes.Count - 1].x, notes[notes.Count - 1].y);

            // If its the first swing, we guess parity for first hit
            if (startingSwing)
            {
                var selectedDict = (notes[0].d == 0 || notes[0].d == 4 || notes[0].d == 5)
                    ? ParityUtils.BackhandDict(rightHand) : ParityUtils.ForehandDict(rightHand);

                swingParity = (notes[0].d == 0 || notes[0].d == 4 || notes[0].d == 5)
                    ? Parity.Backhand : Parity.Forehand;

                SetStartAngle(selectedDict[notes[0].d]);
                SetEndAngle(selectedDict[notes[notes.Count - 1].d]);
            }
        }

        public void SetStartPosition(float x, float y) { startPos.x = x; startPos.y = y; }
        public void SetEndPosition(float x, float y) { endPos.x = x; endPos.y = y; }
        public void SetStartAngle(float angle) { startPos.rotation = angle; }
        public void SetEndAngle(float angle) { endPos.rotation = angle; }
        public bool IsReset => resetType != 0;

        /// <summary>
        /// Writes swing information to a string
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString()
        {
            string returnString = $"Swing Note/s or Bomb/s {swingStartBeat} " +
                                  $"| Parity of this swing: {swingParity}" + " | AFN: " + startPos.rotation +
                $"\nPlayer Offset: {playerOffset.X}x {playerOffset.Y}y | " +
                $"Swing EBPM: {swingEBPM} | Reset Type: {resetType} | Swing Type: {swingType}";
            return returnString;
        }
    }
}
