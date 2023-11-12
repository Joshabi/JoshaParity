using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Representation of the grid
    /// </summary>
    public class BeatGrid
    {
        // Given a position, provides a movement vector to avoid the bomb
        public static readonly Dictionary<Vector2, Vector2> PositionToAvoidanceVector = new Dictionary<Vector2, Vector2>()
        {
            { new Vector2(0, 0), new Vector2(1, 1) },
            { new Vector2(0, 1), new Vector2(1, 0) },
            { new Vector2(0, 2), new Vector2(1, -1) },
            { new Vector2(1, 0), new Vector2(0, 1) },
            { new Vector2(1, 1), new Vector2(1, 0) },
            { new Vector2(1, 2), new Vector2(0, -1) },
            { new Vector2(2, 0), new Vector2(0, 1) },
            { new Vector2(2, 1), new Vector2(-1, 0) },
            { new Vector2(2, 2), new Vector2(0, -1) },
            { new Vector2(3, 0), new Vector2(-1, 1) },
            { new Vector2(3, 1), new Vector2(-1, 0) },
            { new Vector2(3, 2), new Vector2(-1, -1) },
        };

        // Returns true if the inputted note and bomb coordinates cause a reset potentially
        private readonly Dictionary<int, Func<Vector2, int, int, Parity, bool>> _bombDetectionConditions = new Dictionary<int, Func<Vector2, int, int, Parity, bool>>()
        {
        { 0, (note, x, y, parity) => ((y >= note.Y && y != 0) || (y > note.Y && y > 0)) && x == note.X },
        { 1, (note, x, y, parity) => ((y <= note.Y && y != 2) || (y < note.Y && y < 2)) && (x == note.X || (x == note.X-1 && note.X == 3) || (x == note.X+1 && note.X == 0))},
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

        public List<GridPosition> positions;
        public float Time { get; }

        /// <summary>
        /// Initializes a new instance of BombGrid given bombs and a timestamp
        /// </summary>
        /// <param name="bombs">Bombs to add to grid</param>
        /// <param name="timeStamp">Time of grid</param>
        public BeatGrid(List<Bomb> bombs, float timeStamp)
        {
            positions = new List<GridPosition>();
            Time = timeStamp;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    GridPosition newPosition = new GridPosition() { x = i, y = j, bomb = false };
                    positions.Add(newPosition);
                }
            }

            foreach (Bomb bomb in bombs)
            {
                GridPosition pos = positions.FirstOrDefault(x => x.x == bomb.x && x.y == bomb.y);
                if (pos != null)
                {
                    pos.bomb = true;
                }
            }
        }

        /// <summary>
        /// Checks for indiciation of a bomb reset given all positions in the grid with bombs, hand position, inferred saber direction, parity and x offset
        /// </summary>
        /// <param name="positionsWithBombs">All grid positions with bombs</param>
        /// <param name="handPos">Inferred hand position currently</param>
        /// <param name="inferredCutDir">Inferred cut direction (saber direction) currently</param>
        /// <param name="lastParity">Last parity state</param>
        /// <param name="xPlayerOffset">Players lane offset</param>
        /// <returns></returns>
        public Vector2 BombCheckResetIndication(List<GridPosition> positionsWithBombs, Vector2 handPos, int inferredCutDir, Parity lastParity, int xPlayerOffset = 0)
        {
            foreach (Vector2 bombPos in positionsWithBombs.Select(t => new Vector2(t.x, t.y)))
            {
                // If in the center 2 grid spaces, no point trying
                if ((bombPos.X == 1 || bombPos.X == 2) && bombPos.Y is 1) new Vector2(-1, -1);

                // If we already found reason to reset, no need to try again
                bool bombResetIndicated = _bombDetectionConditions[inferredCutDir](new Vector2(handPos.X, handPos.Y), (int)(bombPos.X - (xPlayerOffset * 2)), (int)bombPos.Y, lastParity);
                if (bombResetIndicated) return bombPos;
            }
            return new Vector2(-1, -1);
        }

        /// <summary>
        /// Calculates if a saber movement away from the bombs is necessary indicating a reset
        /// </summary>
        /// <param name="handPos">Inferred hand position currently</param>
        /// <param name="inferredCutDir">Inferred cut direction (saber direction) currently</param>
        /// <param name="lastParity">Last parity state</param>
        /// <param name="xPlayerOffset">Players lane offset</param>
        /// <returns></returns>
        public Vector3 SaberUpdateCalc(Vector2 handPos, int inferredCutDir, Parity lastParity, int xPlayerOffset = 0)
        {
            // Check if given hand position and inferred cut direction this is any reset indication
            List<GridPosition> positionsWithBombs = positions.FindAll(x => x.bomb);
            Vector2 interactionBomb = BombCheckResetIndication(positionsWithBombs, handPos, inferredCutDir, lastParity, xPlayerOffset);
            bool resetIndication = interactionBomb.X != -1;

            bool parityFlip = false;
            Vector2 awayFromBombVector = new Vector2(0, 0);
            if (resetIndication)
            {
                awayFromBombVector = PositionToAvoidanceVector[new Vector2(interactionBomb.X, interactionBomb.Y)];
                parityFlip = true;
            }

            handPos.X += awayFromBombVector.X;
            handPos.Y += awayFromBombVector.Y;

            return (parityFlip) ? new Vector3(handPos.X, handPos.Y, 1) : new Vector3(handPos.X, handPos.Y, 0);
        }
    }

    /// <summary>
    /// Representation of a grid position
    /// </summary>
    public class GridPosition
    {
        public bool bomb;
        public int x, y;
    }
}
