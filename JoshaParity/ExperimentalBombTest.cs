using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoshaParity
{
    /// <summary>
    /// Experimental bomb testing parity check method using grid system and bomb density
    /// </summary>
    public class ExperimentalBombTest : IParityMethod
    {
        /// <summary>
        /// Performs a parity check to see if predicted parity is maintained
        /// </summary>
        /// <param name="lastSwing">Last SwingData</param>
        /// <param name="currentSwing">Current SwingData</param>
        /// <param name="bombs">Bombs between last and current swings</param>
        /// <param name="timeTillNextNote">Time until current swing first note from last swing last note</param>
        /// <returns></returns>
        public Parity ParityCheck(ref SwingData currentSwing, ParityCheckContext context)
        {
            // EXPERIMENTAL BOMB TEST:
            // This method uses the grid system to calculate a potential reset based on bomb density
            // and how that bomb density would affect the player's hand position and saber direction.
            // So far this method has been on average, more successful, however as a result of being
            // more complex, it has sometimes failed at basic bomb resets. Bombs are still just as
            // difficult to predict as before when there are dot notes using this method compared to
            // fixed definitions.

            bool rightHand = currentSwing.rightHand;
            SwingData lastSwing = (rightHand) ?
                context.SwingContext.RightHandSwings.Last() :
                context.SwingContext.LeftHandSwings.Last();
            Note nextNote = currentSwing.notes[0];
            Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
            List<Bomb> bombList = context.MapContext.Bombs.FindAll(x => x.b > lastNote.b + 0.01f && x.b < nextNote.b - 0.01f);
            int prevCutDir;
            int cutDir;

            // If the last swing is all dots, get angle from prev parity and rotation
            if (lastSwing.notes.All(x => x.d == 8))
            {
                prevCutDir = SwingUtils.CutDirFromAngleParity(lastSwing.endPos.rotation, lastSwing.swingParity, currentSwing.rightHand, 45.0f);
            }
            else { prevCutDir = lastSwing.notes.First(x => x.d != 8).d; }

            // If current swing is all dots, get angle from direction from last to next note
            if (currentSwing.notes.All(x => x.d == 8))
            {
                cutDir = SwingUtils.OpposingCutDict[SwingUtils.CutDirFromNoteToNote(lastNote, nextNote)];
            }
            else { cutDir = currentSwing.notes.First(x => x.d != 8).d; }

            // Calculate Prev AFN and opposite parity Next AFN
            float currentAFN = (lastSwing.swingParity != Parity.Forehand) ?
                ParityUtils.BackhandDict(rightHand)[prevCutDir] :
                ParityUtils.ForehandDict(rightHand)[prevCutDir];

            float nextAFN = (lastSwing.swingParity == Parity.Forehand) ?
                ParityUtils.BackhandDict(rightHand)[cutDir] :
                ParityUtils.ForehandDict(rightHand)[cutDir];

            // Angle from neutral difference
            float AFNChange = currentAFN - nextAFN;
            currentSwing.SetUpsideDown(false);

            switch (lastSwing.swingParity)
            {
                // Determines if potentially an upside down hit based on note cut direction and last swing angle
                case Parity.Backhand when lastSwing.endPos.rotation > 0 && nextNote.d == 0 || nextNote.d == 8:
                case Parity.Forehand when lastSwing.endPos.rotation > 0 && nextNote.d == 1 || nextNote.d == 8:
                    currentSwing.SetUpsideDown(true);
                    break;
            }

            #region Bomb Assessment Setup

            // Current bomb method generates grids at a certain time snap for all bombs between the last note
            // and next note. It then attempts to find potential resets based on a dictionary, and simulates
            // the player moving in the opposite direction. The approach is flawed, but functions far better
            // then the previous methods of fixed reset definitions and works with a lot of common bomb decor.

            List<BeatGrid> intervalGrids = new List<BeatGrid>();
            List<Bomb> bombsToAdd = new List<Bomb>();
            const float timeSnap = 0.325f;

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

            // Attempting to simulate Hand Pos and Parity through each Grid
            Vector2 simulatedHandPos = new Vector2(lastSwing.endPos.x, lastSwing.endPos.y);
            Vector2 simulatedSaberDirection = SwingUtils.DirectionalVectors[lastSwing.notes.All(x => x.d == 8) ?
                        SwingUtils.CutDirFromAngleParity(lastSwing.endPos.rotation, lastSwing.swingParity) :
                        SwingUtils.CutDirFromAngleParity(lastSwing.endPos.rotation, lastSwing.swingParity, currentSwing.rightHand, 45.0f)];
            bool hadToMove = false;

            int[,] overallBombDensity = new int[4, 3];
            for (int g = 0; g < intervalGrids.Count; g++)
            {
                foreach (GridPosition position in intervalGrids[g].positions.Where(x => x.bomb))
                {
                    overallBombDensity[position.x, position.y]++;
                }
            }

            #endregion

            #region Bomb Assessment

            // For every grid, check if bomb is in the path of the saber, if so, move hand and saber
            for (int i = 0; i < intervalGrids.Count; i++)
            {
                // Generate Bomb Density
                // For now, only check current grid not future grids
                int[,] bombDensity = new int[4, 3];
                foreach (GridPosition position in intervalGrids[i].positions.Where(x => x.bomb))
                {
                    bombDensity[position.x, position.y]++;
                }

                // Calculate the sum of the avoidance vectors then normalize:
                // For every grid space, calculate vector and add to total.
                Vector2 finalAvoidanceVector = Vector2.Zero;
                for (int x = 0; x < 4; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        // Check if bomb is present at the grid space
                        if (bombDensity[x, y] > 0)
                        {
                            // Calculate position vector
                            // Calculate hand to bomb vector
                            // Add then divide by 2
                            Vector2 differenceDir = simulatedHandPos - new Vector2(x, y);
                            Vector2 positionDir = BeatGrid.PositionToAvoidanceVector[new Vector2(x, y)];
                            Vector2 avoidanceVector = (positionDir * 0.65f) + (differenceDir * 0.35f);

                            // Skip if bomb is far away, and then add vector to final result
                            if (differenceDir.Length() > 2.5) break;
                            finalAvoidanceVector += Vector2.Normalize(avoidanceVector);
                        }
                    }
                }

                // If there is a final avoidance vector, then move hand and saber
                if (finalAvoidanceVector != Vector2.Zero)
                {
                    // Rough Hand Position Movement then Normalize
                    float approxPosX = (float)Math.Round(simulatedHandPos.X + finalAvoidanceVector.X);
                    float approxPosY = (float)Math.Round(simulatedHandPos.Y + finalAvoidanceVector.Y);
                    simulatedHandPos = new Vector2(SwingUtils.Clamp((float)Math.Round(approxPosX), 0, 3), SwingUtils.Clamp((float)Math.Round(approxPosY), 0, 2));
                    hadToMove = true;
                    simulatedSaberDirection = finalAvoidanceVector;
                }

                // If not last grid continue to next
                if (i != intervalGrids.Count - 1 || !hadToMove) continue;

                Vector2 saberDir = new Vector2(
                    SwingUtils.Clamp((float)Math.Round(simulatedSaberDirection.X), -1, 1), SwingUtils.Clamp((float)Math.Round(simulatedSaberDirection.Y), -1, 1));
                int approxCutDir = SwingUtils.DirectionalVectorToCutDirection[saberDir];
                Note fakeNote = new Note() { x = (int)simulatedHandPos.X, y = (int)simulatedHandPos.Y };
                int approxDotCutDir = (currentSwing.notes.All(x => x.d == 8)) ? SwingUtils.OpposingCutDict[SwingUtils.CutDirFromNoteToNote(fakeNote, nextNote)] :
                    currentSwing.notes.First(x => x.d != 8).d;

                if (currentSwing.notes.All(x => x.d == 8) && currentSwing.notes.Count > 1)
                {
                    approxDotCutDir = SwingUtils.CutDirFromNoteToNote(currentSwing.notes[0], currentSwing.notes[currentSwing.notes.Count - 1]);
                }

                // Calculate the AFN values for current pointing direction for either parity, and for next note
                float foreAFN = ParityUtils.ForehandDict(rightHand)[approxCutDir];
                float backAFN = ParityUtils.BackhandDict(rightHand)[approxCutDir];
                float nextForeAFN = ParityUtils.ForehandDict(rightHand)[approxDotCutDir];
                float nextBackAFN = ParityUtils.BackhandDict(rightHand)[approxDotCutDir];

                float FBDiff = Math.Abs(nextForeAFN - backAFN);
                float BFDiff = Math.Abs(nextBackAFN - foreAFN);

                // First do a check against which next AFN is least:
                // If we assume the hand is currently BH and next would be FH
                if (FBDiff < BFDiff)
                {
                    // If pretending next value is forehand, then if backhand its not a reset
                    if (lastSwing.swingParity == Parity.Backhand) break;
                }
                else if (BFDiff < FBDiff)
                {
                    // If pretending next value is backhand, then if forehand its not a reset
                    if (lastSwing.swingParity == Parity.Forehand) break;
                }
                else
                {
                    if (Math.Abs(nextForeAFN) > Math.Abs(nextBackAFN))
                    {
                        if (lastSwing.swingParity == Parity.Forehand) break;
                    }
                    else if (Math.Abs(nextForeAFN) < Math.Abs(nextBackAFN))
                    {
                        if (lastSwing.swingParity == Parity.Backhand) break;
                    }
                    else
                    {
                        if (Math.Abs(nextForeAFN - ParityUtils.ForehandDict(rightHand)[cutDir]) <= 45) break;
                        if (Math.Abs(nextBackAFN - ParityUtils.BackhandDict(rightHand)[cutDir]) <= 45) break;
                    }
                }

                currentSwing.resetType = ResetType.Bomb;
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }

            #endregion

            #region FINISH CALC

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
                if (altNextAFN >= 0)
                {
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
                }
                else
                {
                    currentSwing.resetType = ResetType.Rebound;
                    return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
                }
            }

            // If the angle change exceeds 270 then consider it a reset
            if (Math.Abs(AFNChange) > 270 && !currentSwing.upsideDown)
            {
                currentSwing.resetType = ResetType.Rebound;
                return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
            }
            else { return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }

            #endregion
        }
    }
}