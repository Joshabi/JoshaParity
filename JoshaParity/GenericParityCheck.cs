using JoshaUtils;
using System.Numerics;

namespace JoshaParity
{
    internal class GenericParityCheck : IParityMethod
    {
        // Used for situations in which rotation is 180.
        public bool UpsideDown { get { return _upsideDown; } }
        public bool _upsideDown;

        // Returns true if the inputted note and bomb coordinates cause a reset potentially
        private Dictionary<int, Func<Vector2, int, int, PARITY_STATE, bool>> _bombDetectionConditions = new() {
        { 0, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X },
        { 1, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X },
        { 2, (note, x, y, parity) => (parity == PARITY_STATE.FOREHAND && (y == note.Y || y == note.Y - 1) && ((note.X != 3 && x < note.X) || (note.X < 3 && x <= note.X))) ||
            (parity == PARITY_STATE.BACKHAND && y == note.Y && ((note.X != 0 && x < note.X) || (note.X > 0 && x <= note.X))) },
        { 3, (note, x, y, parity) => (parity == PARITY_STATE.FOREHAND && (y == note.Y || y == note.Y - 1) && ((note.Y != 0 && x > note.Y) || (note.X > 0 && x >= note.X))) ||
            (parity == PARITY_STATE.BACKHAND && y == note.Y && ((note.X != 3 && x > note.X) || (note.X < 3 && x >= note.X))) },
        { 4, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X && x != 3 },
        { 5, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X && x != 0 },
        { 6, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X && x != 3 },
        { 7, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X && x != 0 },
        { 8, (note,x,y, parity) => false }
        };

        // Returns True if potential bomb reset detected based on detection condition dictionary
        private bool BombResetCheck(SwingData lastSwing, List<Note> bombs)
        {
            bool isReset = false;
            for (int i = 0; i < bombs.Count; i++)
            {
                // Get current bomb
                Note bomb = bombs[i];
                Note note = lastSwing.notes[^1];

                // If in the center 2 grid spaces, no point trying
                if ((bomb._lineIndex == 1 || bomb._lineIndex == 2) && bomb._lineLayer == 1) continue;

                // Get the last notes cut direction based on the last swings angle
                var lastNoteCutDir = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ?
                    ParityChecker.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastSwing.endPos.rotation / 45.0) * 45).Key :
                    ParityChecker.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastSwing.endPos.rotation / 45.0) * 45).Key;

                int bombXOffset = 0;
                bool bombOffsetting = bombs.Any(bomb => bomb._lineIndex == note._lineIndex && (bomb._lineLayer <= note._lineLayer && lastSwing.swingParity == PARITY_STATE.BACKHAND && lastSwing.endPos.rotation >= 0)) ||
                bombs.Any(bomb => bomb._lineIndex == note._lineIndex && (bomb._lineLayer >= note._lineLayer && lastSwing.swingParity == PARITY_STATE.FOREHAND && lastSwing.endPos.rotation >= 0));

                if (bombOffsetting && note._lineIndex == 0) bombXOffset = 1;
                if (bombOffsetting && note._lineIndex == 3) bombXOffset = -1;

                isReset = _bombDetectionConditions[lastNoteCutDir](new Vector2(note._lineIndex + bombXOffset, note._lineLayer), bomb._lineIndex, bomb._lineLayer, lastSwing.swingParity);
                if (isReset) return true;
            }
            return false;
        }

        // Performs a parity check to see if next swing should alternate previous parity
        public PARITY_STATE ParityCheck(SwingData lastSwing, ref SwingData nextSwing, List<Note> bombs, float xOffset, float yOffset, bool rightHand)
        {
            Note nextNote = nextSwing.notes[0];

            float currentAFN = (lastSwing.swingParity != PARITY_STATE.FOREHAND) ?
                ParityChecker.BackhandDict[lastSwing.notes[0]._cutDirection] :
                ParityChecker.ForehandDict[lastSwing.notes[0]._cutDirection];

            int orient = nextNote._cutDirection;
            if (nextNote._cutDirection == 8) orient = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ?
                    ParityChecker.BackhandDict.FirstOrDefault(x => x.Value == Math.Round(lastSwing.endPos.rotation / 45.0) * 45).Key :
                    ParityChecker.ForehandDict.FirstOrDefault(x => x.Value == Math.Round(lastSwing.endPos.rotation / 45.0) * 45).Key;

            float nextAFN = (lastSwing.swingParity == PARITY_STATE.FOREHAND) ?
                ParityChecker.BackhandDict[orient] :
                ParityChecker.ForehandDict[orient];

            float angleChange = currentAFN - nextAFN;
            _upsideDown = false;

            if (lastSwing.swingParity == PARITY_STATE.BACKHAND && lastSwing.endPos.rotation > 0 && (nextNote._cutDirection == 0 || nextNote._cutDirection == 8)) _upsideDown = true;
            if (lastSwing.swingParity == PARITY_STATE.FOREHAND && lastSwing.endPos.rotation > 0 && (nextNote._cutDirection == 1 || nextNote._cutDirection == 8)) _upsideDown = true;

            bool bombReset = BombResetCheck(lastSwing, bombs);

            if (bombReset) {
                nextSwing.resetType = RESET_TYPE.BOMB;
                return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            }

            if (lastSwing.endPos.rotation == 180) {
                var altNextAFN = 180 + nextAFN;
                if (altNextAFN >= 0) { return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND; }
                else { return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND; }
            }

            if(MathF.Abs(angleChange) > 180 && !UpsideDown) {
                // NOTE: Add angle checks here later to determine if
                // triangling is the better way to reset then adding a swing 
                nextSwing.resetType = RESET_TYPE.REBOUND;
                return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.FOREHAND : PARITY_STATE.BACKHAND;
            } else { return (lastSwing.swingParity == PARITY_STATE.FOREHAND) ? PARITY_STATE.BACKHAND : PARITY_STATE.FOREHAND; }
        }
    }
}
