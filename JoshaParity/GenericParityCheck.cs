using JoshaUtils;
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
        public Parity ParityCheck(SwingData lastSwing, ref SwingData currentSwing, List<Bomb> bombs, int playerXOffset, bool isRightHand, float timeTillNextNote = 0.1f)
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
            if (nextNote.d == 8) SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, lastSwing.swingParity, 45.0f);

            float nextAFN = (lastSwing.swingParity == Parity.Forehand) ?
                SwingDataGeneration.BackhandDict[orient] :
                SwingDataGeneration.ForehandDict[orient];

            // Angle from neutral difference
            float AFNChange = currentAFN - nextAFN;
            UpsideDown = false;

            // Determines if potentially an upside down hit based on note cut direction and last swing angle
            if (lastSwing is { swingParity: Parity.Backhand, endPos.rotation: > 0 } && nextNote.d is 0 or 8)
            { UpsideDown = true; }
            else if (lastSwing is { swingParity: Parity.Forehand, endPos.rotation: > 0 } && nextNote.d is 1 or 8)
            { UpsideDown = true; }

            #endregion

            #region Bomb Assessment

            // Current bomb method generates grids at a certain time snap for all bombs between the last note
            // and next note. It then attempts to find potential resets based on a dictionary, and simulates
            // the player moving in the opposite direction. The approach is flawed, but functions far better
            // then the previous methods of fixed reset definitions and works with a lot of common bomb decor.

            List<BeatGrid> intervalGrids = new();
            List<Bomb> bombsToAdd = new();
            const float timeSnap = 0.05f;

            // Construct play-space grid with bombs at a set interval of beats
            foreach (Bomb bomb in bombs.OrderBy(x => x.b))
            {
                if (bombsToAdd.Count == 0 || MathF.Abs(bomb.b - bombsToAdd.First().b) <= timeSnap)
                {
                    bombsToAdd.Add(bomb);
                } else {
                    BeatGrid grid = new(bombsToAdd, bombsToAdd[0].b);
                    intervalGrids.Add(grid);
                    bombsToAdd.Clear();
                    bombsToAdd.Add(bomb);
                }
            }

            // Catch extra bombs outside the interval at the end, and create grid
            if (bombsToAdd.Count > 0)
            {
                BeatGrid lastGrid = new(bombsToAdd, bombsToAdd[0].b);
                intervalGrids.Add(lastGrid);
            }

            // Attempting to simulate hand position and parity through each grid
            Vector2 simulatedHandPos = new(lastSwing.endPos.x, lastSwing.endPos.y);
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
                if (!bombResetIndicated) continue;

                // Performs a check to check occassional false flags that are likely unintended
                if (nextNote.d != 8)
                {
                    // Depending on parity, check the ending AFN (Angle from neutral).
                    // In most cases, we will assume that only forehand resets with bombs occur
                    // when the AFN is >= 90. For backhand, limit further. Furthermore, backhand / up
                    // resets are more unconventional.
                    if (simulatedParity == Parity.Forehand && (!(MathF.Abs(AFNChange) >= 90))) continue;
                    if (simulatedParity == Parity.Backhand && (!(MathF.Abs(AFNChange) >= 45))) continue;
                }

                // If the last and next swing are just singular dots, perform angle clamping to the sabers
                // angle in order to help with determining parity for future bomb detections.
                if (currentSwing.notes.All(x => x.d == 8) && currentSwing.notes.Count == 1 && lastSwing.notes.All(x => x.d == 8) && lastSwing.notes.Count == 1)
                {
                    float orientAngle = Math.Clamp(currentSwing.endPos.rotation, -45, 45);
                    currentSwing.SetStartAngle(orientAngle);
                    currentSwing.SetEndAngle(orientAngle);
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
