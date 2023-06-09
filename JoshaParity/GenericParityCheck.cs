using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Generic parity check method assuming absolute parity even with notes in same direction
    /// </summary>
    public class GenericParityCheck : IParityMethod
    {
        public bool UpsideDown { get; private set; }

        /// <summary>
        /// Performs a parity check to see if predicted parity is maintained.
        /// </summary>
        /// <param name="lastSwing">Last swing data</param>
        /// <param name="currentSwing">Current swing data</param>
        /// <param name="bombs">Bombs between last and current swings</param>
        /// <param name="playerXOffset">Players X Offset cauesd by dodge walls</param>
        /// <param name="isRightHand">Right handed notes?</param>
        /// <param name="timeTillNextNote">Time until current swing first note from last swing last note</param>
        /// <returns></returns>
        public Parity ParityCheck(SwingData lastSwing, ref SwingData currentSwing, List<Bomb> bombs, int playerXOffset, bool isRightHand, float timeTillNextNote = -1f)
        {
            // The parity method uses dictionaries to define the saber rotation based on parity (and hand)
            // Assuming a forehand down hit is neutral and backhand up hit
            // Rotating the hand inwards is positive and outwards negative
            // Attempt to calculate if the new angle makes sense

            // NOTE: There are flaws to this method, improvements will be made over time, such as lean and positional
            // information being factored into this.

            #region AFN Calc and Upside Down

            Note nextNote = currentSwing.notes[0];

            float currentAFN = (lastSwing.swingParity != Parity.Forehand) ?
                SwingDataGeneration.BackhandDict[lastSwing.notes[0].d] :
                SwingDataGeneration.ForehandDict[lastSwing.notes[0].d];

            int orient = nextNote.d;
            if (nextNote.d == 8) orient = SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, lastSwing.swingParity, 45.0f);

            float nextAFN = (lastSwing.swingParity == Parity.Forehand) ?
                SwingDataGeneration.BackhandDict[orient] :
                SwingDataGeneration.ForehandDict[orient];

            // Angle from neutral difference
            float AFNChange = currentAFN - nextAFN;
            UpsideDown = false;

            switch (lastSwing.swingParity)
            {
                // Determines if potentially an upside down hit based on note cut direction and last swing angle
                case Parity.Backhand when lastSwing.endPos.rotation > 0 && nextNote.d == 0 || nextNote.d == 8:
                case Parity.Forehand when lastSwing.endPos.rotation > 0 && nextNote.d == 1 || nextNote.d == 8:
                    UpsideDown = true;
                    break;
            }

            #endregion

            #region Bomb Assessment

            // Current bomb method generates grids at a certain time snap for all bombs between the last note
            // and next note. It then attempts to find potential resets based on a dictionary, and simulates
            // the player moving in the opposite direction. The approach is flawed, but functions far better
            // then the previous methods of fixed reset definitions and works with a lot of common bomb decor.

            List<BeatGrid> intervalGrids = new List<BeatGrid>();
            List<Bomb> bombsToAdd = new List<Bomb>();
            const float timeSnap = 0.05f;

            // Construct play-space grid with bombs at a set interval of beats
            foreach (Bomb bomb in bombs.OrderBy(x => x.b))
            {
                if (bombsToAdd.Count == 0 || MathF.Abs(bomb.b - bombsToAdd.First().b) <= timeSnap)
                {
                    bombsToAdd.Add(bomb);
                } else {
                    BeatGrid grid = new BeatGrid(bombsToAdd, bombsToAdd[0].b);
                    intervalGrids.Add(grid);
                    bombsToAdd.Clear();
                    bombsToAdd.Add(bomb);
                }
            }

            // Catch extra bombs outside the interval at the end, and create grid
            if (bombsToAdd.Count > 0)
            {
                BeatGrid lastGrid = new BeatGrid(bombsToAdd, bombsToAdd[0].b);
                intervalGrids.Add(lastGrid);
            }

            // Attempting to simulate hand position and parity through each grid
            Vector2 simulatedHandPos = new Vector2( lastSwing.endPos.x, lastSwing.endPos.y);
            Parity simulatedParity = lastSwing.swingParity;
            for (int i = 0; i < intervalGrids.Count; i++)
            {
                // Get the previous cut direction, rounded differently if a dot to help detection
                int cutDir = (lastSwing.notes.All(x => x.d == 8)) ?
                    SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, simulatedParity) :
                    SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, simulatedParity, 45.0f);

                // Vector result gives a new X,Y for hand position, with Z determining if parity should flip
                Vector3 result = intervalGrids[i]
                    .SaberUpdateCalc(simulatedHandPos, cutDir, simulatedParity, playerXOffset);

                simulatedHandPos.X = result.X;
                simulatedHandPos.Y = result.Y;
                if (result.Z > 0) { simulatedParity = (simulatedParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }

                // If this is the list grid, attempt to determine exit parity
                if (i != intervalGrids.Count - 1) continue;

                // If the simulated parity differs from previous parity, possible reset, if not, we can leave
                bool bombResetIndicated = simulatedParity != lastSwing.swingParity;
                if (currentSwing.notes.All(x => x.d == 8))
                {
                    bombResetIndicated = simulatedParity != lastSwing.swingParity ||
                                         simulatedHandPos.X != lastSwing.endPos.x ||
                                         simulatedHandPos.Y != lastSwing.endPos.y;}
                if (!bombResetIndicated) continue;

                // Performs a check to check occassional false flags that are likely unintended
                // If the next swing isn't entirely dots, attempt to calculate next parity
                if (currentSwing.notes.Any(x => x.d != 8))
                {
                    // As a rule of thumb:
                    // If the rotation is bigger when the next swing is forehand, we go backhand, and vice versa

                    // Calculate AFN values
                    float forehandAFN = SwingDataGeneration.ForehandDict[currentSwing.notes.First(x => x.d != 8).d];
                    float backhandAFN = SwingDataGeneration.BackhandDict[currentSwing.notes.First(x => x.d != 8).d];

                    if (MathF.Abs(forehandAFN) > MathF.Abs(backhandAFN))
                    {
                        if (Parity.Forehand == lastSwing.swingParity) break;
                    }
                    else
                    {
                        if (Parity.Backhand == lastSwing.swingParity) break;
                    }
                }
                else
                {
                    if ((lastSwing.endPos.y < currentSwing.notes.Min(x => x.y) &&
                        simulatedHandPos.Y < currentSwing.notes.Min(x => x.y))) break;
                }

                currentSwing.resetType = ResetType.Bomb;
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }

            #endregion

            // If last cut is entirely dot notes and next cut is too, then parity is assumed to be maintained
            if (lastSwing.notes.All(x => x.d == 8) && currentSwing.notes.All(x => x.d == 8))
            {
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
            }

            // If we can evaluate based on timeTillNextNote
            if (timeTillNextNote != -1) {
                // If time exceeds 2 seconds
                if (timeTillNextNote > 2 && MathF.Abs(lastSwing.endPos.rotation) == 180) {
                    currentSwing.resetType = ResetType.Rebound;
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
                }
            }

            // Given a last swing of 180 (not in the dictionaries)
            // Since we are upside down, attempt to try calculating a different way
            if (lastSwing.endPos.rotation == 180)
            {
                float altNextAFN = 180 + nextAFN;
                if (altNextAFN >= 0) {
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                } else {
                    currentSwing.resetType = ResetType.Rebound;
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
                }
            }

            // If the angle change exceeds 270 then consider it a reset
            if (Math.Abs(AFNChange) > 270 && !UpsideDown)
            {
                currentSwing.resetType = ResetType.Rebound;
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
            else { return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }
        }
    }
}
