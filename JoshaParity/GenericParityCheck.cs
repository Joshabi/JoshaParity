using JoshaUtils;
using System.Numerics;

namespace JoshaParity
{
    public class GenericParityCheck : IParityMethod
    {
        public bool UpsideDown { get { return _upsideDown; } }
        private bool _upsideDown;

        // Returns true if the inputted note and bomb coordinates cause a reset potentially
        private readonly Dictionary<int, Func<Vector2, int, int, PARITY_STATE, bool>> _bombDetectionConditions = new()
    {
        { 0, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X },
        { 1, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X },
        { 2, (note, x, y, parity) => (parity == PARITY_STATE.FOREHAND && (y == note.Y || y == note.Y - 1) && ((note.X != 3 && x < note.X) || (note.X < 3 && x <= note.X))) ||
            (parity == PARITY_STATE.BACKHAND && y == note.Y && ((note.X != 0 && x < note.X) || (note.X > 0 && x <= note.X))) },
        { 3, (note, x, y, parity) => (parity == PARITY_STATE.FOREHAND && (y == note.Y || y == note.Y - 1) && ((note.X != 0 && x > note.X) || (note.X > 0 && x >= note.X))) ||
            (parity == PARITY_STATE.BACKHAND && y == note.Y && ((note.X != 3 && x > note.X) || (note.X < 3 && x >= note.X))) },
        { 4, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X && x != 3 && parity != PARITY_STATE.FOREHAND },
        { 5, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X && x != 0 && parity != PARITY_STATE.FOREHAND },
        { 6, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X && x != 3 && parity != PARITY_STATE.BACKHAND },
        { 7, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X && x != 0 && parity != PARITY_STATE.BACKHAND },
        { 8, (note,x,y, parity) => false }
    };

        public bool BombResetCheck(SwingData lastCut, List<Note> bombs, int xPlayerOffset)
        {
            // Not found yet
            bool bombResetIndicated = false;
            for (int i = 0; i < bombs.Count; i++)
            {
                // Get current bomb
                Note bomb = bombs[i];
                Note? note;

                // If in the center 2 grid spaces, no point trying
                if ((bomb._lineIndex == 1 || bomb._lineIndex == 2) && bomb._lineLayer == 1) continue;

                // Get the last note. In the case of a stack, picks the note that isnt at 2 or 0 as
                // it triggers a reset when it shouldn't.
                note = lastCut.notes[^1];

                // Get the last notes cut direction based on the last swings angle
                var lastNoteCutDir = (lastCut.swingParity == PARITY_STATE.FOREHAND) ?
                    SwingDataGeneration.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.startPos.rotation / 45.0) * 45).Key :
                    SwingDataGeneration.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.startPos.rotation / 45.0) * 45).Key;

                // Offset the checking if the entire outerlane bombs indicate moving the check inwards
                int xOffset = 0;

                bool bombOffsetting = bombs.Any(bomb => bomb._lineIndex == note._lineIndex && (bomb._lineLayer <= note._lineLayer && lastCut.swingParity == PARITY_STATE.BACKHAND && lastCut.endPos.rotation >= 0)) ||
                    bombs.Any(bomb => bomb._lineIndex == note._lineIndex && (bomb._lineLayer >= note._lineLayer && lastCut.swingParity == PARITY_STATE.FOREHAND && lastCut.endPos.rotation >= 0));

                if (bombOffsetting && note._lineIndex == 0) xOffset = 1;
                if (bombOffsetting && note._lineIndex == 3) xOffset = -1;

                // Determine if lastnote and current bomb cause issue
                // If we already found reason to reset, no need to try again
                bombResetIndicated = _bombDetectionConditions[lastNoteCutDir](new Vector2(note._lineIndex, note._lineLayer), bomb._lineIndex - (xPlayerOffset * 2) - xOffset, bomb._lineLayer, lastCut.swingParity);
                if (bombResetIndicated) return true;
            }
            return false;
        }

        public PARITY_STATE ParityCheck(SwingData lastCut, ref SwingData currentSwing, List<Note> bombs, int playerXOffset, bool rightHand, float timeTillNextNote = 0.1f)
        {
            // AFN: Angle from neutral
            // Assuming a forehand down hit is neutral, and a backhand up hit
            // Rotating the hand inwards goes positive, and outwards negative
            // Using a list of definitions, turn cut direction into an angle, and check
            // if said angle makes sense.

            Note nextNote = currentSwing.notes[0];

            float currentAFN = (lastCut.swingParity != PARITY_STATE.FOREHAND) ?
                SwingDataGeneration.BackhandDict[lastCut.notes[0]._cutDirection] :
                SwingDataGeneration.ForehandDict[lastCut.notes[0]._cutDirection];

            int orient = nextNote._cutDirection;
            if (nextNote._cutDirection == 8) orient = (lastCut.swingParity == PARITY_STATE.FOREHAND) ?
                    SwingDataGeneration.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPos.rotation / 45.0) * 45).Key :
                    SwingDataGeneration.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPos.rotation / 45.0) * 45).Key;

            float nextAFN = (lastCut.swingParity == PARITY_STATE.FOREHAND) ?
                SwingDataGeneration.BackhandDict[orient] :
                SwingDataGeneration.ForehandDict[orient];

            // Angle from neutral difference
            float angleChange = currentAFN - nextAFN;
            _upsideDown = false;

            // Determines if potentially an upside down hit based on note cut direction and last swing angle
            if (lastCut.swingParity == PARITY_STATE.BACKHAND && lastCut.endPos.rotation > 0 && (nextNote._cutDirection == 0 || nextNote._cutDirection == 8))
            {
                _upsideDown = true;
            }
            else if (lastCut.swingParity == PARITY_STATE.FOREHAND && lastCut.endPos.rotation > 0 && (nextNote._cutDirection == 1 || nextNote._cutDirection == 8))
            {
                _upsideDown = true;
            }

            // Check if bombs are in the position to indicate a reset
            bool bombResetIndicated = BombResetCheck(lastCut, bombs, playerXOffset);

            // Want to do a seconday check:
            // Checks whether resetting will cause another reset, which helps to catch some edge cases
            // in bomb detection where it triggers for decor bombs.
            bool bombResetParityImplied = false;
            if (bombResetIndicated)
            {
                if (nextNote._cutDirection == 8 && lastCut.notes.All(x => x._cutDirection == 8)) bombResetParityImplied = true;
                else
                {
                    // In case of dots, calculate using previous swing swing-angle
                    int altOrient = (lastCut.swingParity == PARITY_STATE.FOREHAND) ?
                            SwingDataGeneration.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPos.rotation / 45.0) * 45).Key :
                            SwingDataGeneration.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastCut.endPos.rotation / 45.0) * 45).Key;

                    if (lastCut.swingParity == PARITY_STATE.FOREHAND)
                    {
                        if (Math.Abs(SwingDataGeneration.ForehandDict[altOrient] + SwingDataGeneration.BackhandDict[nextNote._cutDirection]) >= 90) { bombResetParityImplied = true; }
                    }
                    else
                    {
                        if (Math.Abs(SwingDataGeneration.BackhandDict[altOrient] + SwingDataGeneration.ForehandDict[nextNote._cutDirection]) >= 90) { bombResetParityImplied = true; }
                    }
                }
            }

            // If bomb reset indicated and direction implies, then reset
            if (bombResetIndicated && bombResetParityImplied)
            {
                currentSwing.resetType = RESET_TYPE.BOMB;
                return (lastCut.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            }

            // If last cut is entirely dot notes and next cut is too, then parity is assumed to be maintained
            if (lastCut.notes.All(x => x._cutDirection == 8) && currentSwing.notes.All(x => x._cutDirection == 8))
            {
                return (lastCut.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND;
            }

            // AKA, If a 180 anticlockwise (right) clockwise (left) rotation
            // FIXES ISSUES with uhh, some upside down hits?
            if (lastCut.endPos.rotation == 180)
            {
                var altNextAFN = 180 + nextAFN;
                if (altNextAFN >= 0)
                {
                    return (lastCut.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND;
                }
                else
                {
                    currentSwing.resetType = RESET_TYPE.REBOUND;
                    return (lastCut.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
                }
            }

            // If the angle change exceeds 180 even after accounting for bigger rotations then triangle
            if (Math.Abs(angleChange) > 270 && !UpsideDown)
            {
                currentSwing.resetType = RESET_TYPE.REBOUND;
                return (lastCut.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            }
            else { return (lastCut.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND; }
        }
    }
}
