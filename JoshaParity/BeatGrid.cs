using System.Numerics;
using JoshaUtils;

namespace JoshaParity
{
    /// <summary>
    /// Representation of the grid
    /// </summary>
    public class BeatGrid
    {
        private readonly Dictionary<Vector2, Vector2> _positionToAvoidanceVector = new()
        {
        { new Vector2(0, 0), new Vector2(1, 1) },
        { new Vector2(0, 1), new Vector2(1, 0) },
        { new Vector2(0, 2), new Vector2(1, -1) },
        { new Vector2(1, 0), new Vector2(0, 2) },
        { new Vector2(1, 1), new Vector2(1, 0) },
        { new Vector2(1, 2), new Vector2(0, -2) },
        { new Vector2(2, 0), new Vector2(0, 2) },
        { new Vector2(2, 1), new Vector2(-1, 0) },
        { new Vector2(2, 2), new Vector2(0, -2) },
        { new Vector2(3, 0), new Vector2(-1, -1) },
        { new Vector2(3, 1), new Vector2(0, -1) },
        { new Vector2(3, 2), new Vector2(-1, -1) },
        };

        // Returns true if the inputted note and bomb coordinates cause a reset potentially
        private readonly Dictionary<int, Func<Vector2, int, int, Parity, bool>> _bombDetectionConditions = new()
        {
        { 0, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X },
        { 1, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X },
        { 2, (note, x, y, parity) => (parity == Parity.Forehand && (y == note.Y || y == note.Y - 1) && ((note.X != 3 && x < note.X) || (note.X < 3 && x <= note.X))) ||
                                     (parity == Parity.Backhand && y == note.Y && ((note.X != 0 && x < note.X) || (note.X > 0 && x <= note.X))) },
        { 3, (note, x, y, parity) => (parity == Parity.Forehand && (y == note.Y || y == note.Y - 1) && ((note.X != 0 && x > note.X) || (note.X > 0 && x >= note.X))) ||
                                     (parity == Parity.Backhand && y == note.Y && ((note.X != 3 && x > note.X) || (note.X < 3 && x >= note.X))) },
        { 4, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X && !(x == 3 && y is 1) && parity != Parity.Forehand },
        { 5, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X && !(x == 0 && y is 1) && parity != Parity.Forehand },
        { 6, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X && !(x == 3 && y is 1) && parity != Parity.Backhand },
        { 7, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && x == note.X && !(x == 0 && y is 1) && parity != Parity.Backhand },
        { 8, (note,x,y, parity) => false }
        };

        private readonly List<GridPosition> _positions;
        public float Time { get; }

        public BeatGrid(List<Bomb> bombs, float timeStamp)
        {
            _positions = new List<GridPosition>();
            Time = timeStamp;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    GridPosition newPosition = new() { x = i, y = j, bomb = false };
                    _positions.Add(newPosition);
                }
            }

            foreach (Bomb bomb in bombs)
            {
                _positions.First(x => x.x == bomb.x && x.y == bomb.y).bomb = true;
            }
        }

        public bool BombCheckResetIndication(List<GridPosition> positionsWithBombs, Vector2 handPos, int inferredCutDir, Parity lastParity, int xPlayerOffset = 0)
        {
            foreach (Vector2 bombPos in positionsWithBombs.Select(t => new Vector2(t.x, t.y)))
            {
                // If in the center 2 grid spaces, no point trying
                if ((bombPos.X is 1 or 2) && bombPos.Y is 1) return false;

                // If we already found reason to reset, no need to try again
                bool bombResetIndicated = _bombDetectionConditions[inferredCutDir](new Vector2(handPos.X, handPos.Y), (int)(bombPos.X - (xPlayerOffset * 2)), (int)bombPos.Y, lastParity);
                if (bombResetIndicated) return true;
            }
            return false;
        }

        // Calculate if saber movement needed
        public Vector3 SaberUpdateCalc(Vector2 handPos, int inferredCutDir, Parity lastParity, int xPlayerOffset = 0)
        {
            // Check if given hand position and inferred cut direction this is any reset indication
            List<GridPosition> positionsWithBombs = _positions.FindAll(x => x.bomb);
            bool resetIndication =
                BombCheckResetIndication(positionsWithBombs, handPos, inferredCutDir, lastParity, xPlayerOffset);

            // If there is an inferred reset, we will pretend to reset the player
            bool parityFlip = false;
            Vector2 awayFromBombVector = new(0, 0);
            if (resetIndication)
            {
                awayFromBombVector = _positionToAvoidanceVector[new Vector2(handPos.X, handPos.Y)];
                parityFlip = true;
            }

            handPos.X = Math.Clamp(handPos.X + awayFromBombVector.X, 0, 3);
            handPos.Y = Math.Clamp(handPos.Y + awayFromBombVector.Y, 0, 2);

            return (parityFlip) ? new Vector3(handPos.X, handPos.Y, 1) : new Vector3(handPos.X, handPos.Y, 0);
        }
    }

    public class GridPosition
    {
        public bool bomb;
        public int x, y;
    }
}
