using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Context for Parity Check
    /// </summary>
    public class ParityCheckContext
    {
        public ParityCheckContext(ref SwingData currentSwing, MapSwingContainer swingContext, MapObjects mapContext) {
            this.currentSwing = currentSwing; SwingContext = swingContext; MapContext = mapContext;
        }

        public SwingData currentSwing;
        public MapSwingContainer SwingContext { get; set; }
        public MapObjects MapContext { get; set; }
    }

    /// <summary>
    /// Generic parity check method assuming absolute parity even with notes in same direction
    /// </summary>
    public class GenericParityCheck : IParityMethod
    {
        /// <summary>
        /// Performs a parity check to see if predicted parity is maintained
        /// </summary>
        /// <param name="lastSwing">Last swing data</param>
        /// <param name="currentSwing">Current swing data</param>
        /// <param name="bombs">Bombs between last and current swings</param>
        /// <param name="timeTillNextNote">Time until current swing first note from last swing last note</param>
        /// <returns></returns>
        public Parity ParityCheck(ParityCheckContext context)
        {
            // GENERIC PARITY CHECK:
            // This method uses the grid system for Bomb Reset Detection.
            // For each grid it moves your hand, and points the saber away from the bombs,
            // then determines the appropriate exit parity.

            bool rightHand = context.currentSwing.rightHand;
            List<Bomb> bombList = new(context.MapContext.Bombs.ToList());
            SwingData lastSwing = (rightHand) ?
                context.SwingContext.RightHandSwings.Last() :
                context.SwingContext.LeftHandSwings.Last();
            Note nextNote = context.currentSwing.notes[0];
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
            int prevCutDir;
            int cutDir;

            // If the last swing is all dots, get angle from prev parity and rotation
            if (lastSwing.notes.All(x => x.d == 8))
            {
                prevCutDir = SwingUtils.CutDirFromAngleParity(lastSwing.endPos.rotation, lastSwing.swingParity, context.currentSwing.rightHand, 45.0f);
            }
            else { prevCutDir = lastSwing.notes.First(x => x.d != 8).d; }

            // If current swing is all dots, get angle from direction from last to next note
            if (context.currentSwing.notes.All(x => x.d == 8))
            {
                cutDir = SwingUtils.OpposingCutDict[SwingUtils.CutDirFromNoteToNote(lastNote, nextNote)];
            }
            else { cutDir = context.currentSwing.notes.First(x => x.d != 8).d; }

            // Calculate Prev AFN and opposite parity Next AFN
            float currentAFN = (lastSwing.swingParity != Parity.Forehand) ?
                ParityUtils.BackhandDict(rightHand)[prevCutDir] :
                ParityUtils.ForehandDict(rightHand)[prevCutDir];

            float nextAFN = (lastSwing.swingParity == Parity.Forehand) ?
                ParityUtils.BackhandDict(rightHand)[cutDir] :
                ParityUtils.ForehandDict(rightHand)[cutDir];

            // Angle from neutral difference
            float AFNChange = currentAFN - nextAFN;
            context.currentSwing.upsideDown = false;

            switch (lastSwing.swingParity)
            {
                // Determines if potentially an upside down hit based on note cut direction and last swing angle
                case Parity.Backhand when lastSwing.endPos.rotation > 0 && nextNote.d == 0 || nextNote.d == 8:
                case Parity.Forehand when lastSwing.endPos.rotation > 0 && nextNote.d == 1 || nextNote.d == 8:
                    context.currentSwing.upsideDown = true;
                    break;
            }



            #region Bomb Assessment

            // The approach is flawed, but functions far better then the previous methods of
            // fixed reset definitions and works with a lot of common bomb decor.

            List<BeatGrid> intervalGrids = new List<BeatGrid>();
            List<Bomb> bombsToAdd = new List<Bomb>();
            const float timeSnap = 0.05f;

            // Construct play-space grid with bombs at a set interval of beats
            foreach (Bomb bomb in bombList.OrderBy(x => x.b))
            {
                if (bombsToAdd.Count == 0 || Math.Abs(bomb.b - bombsToAdd.First().b) <= timeSnap)
                {
                    bombsToAdd.Add(bomb);
                }
                else
                {
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
            Vector2 simulatedHandPos = new Vector2(lastSwing.endPos.x, lastSwing.endPos.y);
            Parity simulatedParity = lastSwing.swingParity;
            for (int i = 0; i < intervalGrids.Count; i++)
            {
                // Get the previous cut direction, rounded differently if a dot to help detection
                int cutDirT = (lastSwing.notes.All(x => x.d == 8)) ?
                    SwingUtils.CutDirFromAngleParity(lastSwing.endPos.rotation, simulatedParity) :
                    SwingUtils.CutDirFromAngleParity(lastSwing.endPos.rotation, simulatedParity, context.currentSwing.rightHand, 45.0f);

                // Vector result gives a new X,Y for hand position, with Z determining if parity should flip
                Vector3 result = intervalGrids[i]
                    .SaberUpdateCalc(simulatedHandPos, cutDirT, simulatedParity, (int)context.currentSwing.playerOffset.X);

                simulatedHandPos.X = result.X;
                simulatedHandPos.Y = result.Y;
                if (result.Z > 0) { simulatedParity = (simulatedParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }

                // If this is the list grid, attempt to determine exit parity
                if (i != intervalGrids.Count - 1) continue;

                // If the simulated parity differs from previous parity, possible reset, if not, we can leave
                bool bombResetIndicated = simulatedParity != lastSwing.swingParity;
                if (context.currentSwing.notes.All(x => x.d == 8))
                {
                    bombResetIndicated = simulatedParity != lastSwing.swingParity ||
                                         simulatedHandPos.X != lastSwing.endPos.x ||
                                         simulatedHandPos.Y != lastSwing.endPos.y;
                }
                if (!bombResetIndicated) continue;

                // Performs a check to check occassional false flags that are likely unintended
                // If the next swing isn't entirely dots, attempt to calculate next parity
                if (context.currentSwing.notes.Any(x => x.d != 8))
                {
                    // As a rule of thumb:
                    // If the rotation is bigger when the next swing is forehand, we go backhand, and vice versa

                    // Calculate AFN values
                    float forehandAFN = ParityUtils.ForehandDict(rightHand)[context.currentSwing.notes.First(x => x.d != 8).d];
                    float backhandAFN = ParityUtils.BackhandDict(rightHand)[context.currentSwing.notes.First(x => x.d != 8).d];

                    if (Math.Abs(forehandAFN) > Math.Abs(backhandAFN))
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
                    if ((lastSwing.endPos.y < context.currentSwing.notes.Min(x => x.y) &&
                        simulatedHandPos.Y < context.currentSwing.notes.Min(x => x.y))) break;
                }

                context.currentSwing.resetType = ResetType.Bomb;
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }

            #endregion

            #region FINISH CALC

            // If last cut is entirely dot notes and next cut is too, then parity is assumed to be maintained
            if (lastSwing.notes.All(x => x.d == 8) && context.currentSwing.notes.All(x => x.d == 8))
            {
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
            }

            // Given a last swing of 180 (not in the dictionaries)
            // Since we are upside down, attempt to try calculating a different way
            if (lastSwing.endPos.rotation == 180)
            {
                float altNextAFN = 180 + nextAFN;
                if (altNextAFN >= 0)
                {
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                }
                else
                {
                    context.currentSwing.resetType = ResetType.Rebound;
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
                }
            }

            // Get Lean Value
            float leanValue = (context.SwingContext.LeanData.Count != 0) ?
                 context.SwingContext.LeanData.Last().leanValue : 0;

            // If the angle change exceeds 270 then consider it a reset
            if (Math.Abs(AFNChange) > 270 && !context.currentSwing.upsideDown)
            {
                context.currentSwing.resetType = ResetType.Rebound;
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
            else { return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }

            #endregion
        }
    }
}
