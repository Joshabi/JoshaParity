using JoshaParity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class ExperimentalBombTest : IParityMethod
{

    public bool UpsideDown { get; private set; }


    // Contains a list of directional vectors
    public readonly Dictionary<Vector2, Vector2> PositionToAvoidanceVector = new Dictionary<Vector2, Vector2>()
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
        { new Vector2(3, 2), new Vector2(-1, -1) }
    };

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

        // Get Next Note, Last Note, and Cut Dir
        Note nextNote = currentSwing.notes[0];
        Note lastNote = lastSwing.notes[lastSwing.notes.Count - 1];
        int prevCutDir;
        int cutDir;

        // If the last swing is all dots, get angle from prev parity and rotation
        if (lastSwing.notes.All(x => x.d == 8))
        {
            prevCutDir = SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, lastSwing.swingParity, 45.0f);
        }
        else { prevCutDir = lastSwing.notes.First(x => x.d != 8).d; }

        // If current swing is all dots, get angle from direction from last to next note
        if (currentSwing.notes.All(x => x.d == 8))
        {
            cutDir = SwingDataGeneration.opposingCutDict[SwingDataGeneration.CutDirFromNoteToNote(lastNote, nextNote)];
        }
        else { cutDir = currentSwing.notes.First(x => x.d != 8).d; }

        // Calculate Prev AFN and opposite parity Next AFN
        float currentAFN = (lastSwing.swingParity != Parity.Forehand) ?
            SwingDataGeneration.BackhandDict[prevCutDir] :
            SwingDataGeneration.ForehandDict[prevCutDir];

        float nextAFN = (lastSwing.swingParity == Parity.Forehand) ?
            SwingDataGeneration.BackhandDict[cutDir] :
            SwingDataGeneration.ForehandDict[cutDir];

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

        #region Bomb Assessment Setup

        // Current bomb method generates grids at a certain time snap for all bombs between the last note
        // and next note. It then attempts to find potential resets based on a dictionary, and simulates
        // the player moving in the opposite direction. The approach is flawed, but functions far better
        // then the previous methods of fixed reset definitions and works with a lot of common bomb decor.

        List<BeatGrid> intervalGrids = new List<BeatGrid>();
        List<Bomb> bombsToAdd = new List<Bomb>();
        const float timeSnap = 0.325f;

        // Construct play-space grid with bombs at a set interval of beats
        foreach (Bomb bomb in bombs.OrderBy(x => x.b))
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
        Vector2 simulatedSaberDirection = SwingDataGeneration.DirectionalVectors[lastSwing.notes.All(x => x.d == 8) ?
                    SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, lastSwing.swingParity) :
                    SwingDataGeneration.CutDirFromAngle(lastSwing.endPos.rotation, lastSwing.swingParity, 45.0f)];

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
                        Vector2 positionDir = PositionToAvoidanceVector[new Vector2(x, y)];
                        Vector2 avoidanceVector = (positionDir * 0.65f) + (differenceDir * 0.35f);

                        // Skip if bomb is far away, and then add vector to final result
                        finalAvoidanceVector += Vector2.Normalize(avoidanceVector);
                    }
                }
            }

            // Rough Hand Position Movement then Normalize
            float approxPosX = (float)Math.Round(simulatedHandPos.Y + finalAvoidanceVector.Y);
            float approxPosY = (float)Math.Round(simulatedHandPos.Y + finalAvoidanceVector.Y);
            simulatedHandPos = new Vector2(SwingUtility.Clamp((float)Math.Round(approxPosX), 0, 3), SwingUtility.Clamp((float)Math.Round(approxPosY), 0, 2));

            // Calculate saber point direction
            finalAvoidanceVector = Vector2.Normalize(finalAvoidanceVector);
            simulatedSaberDirection = finalAvoidanceVector;

            // If not last grid continue to next
            if (i != intervalGrids.Count - 1) continue;

            // Get Saber Direction away from bombs using avoidance vector
            if (simulatedSaberDirection.X == 0 && simulatedSaberDirection.Y == 0) { simulatedSaberDirection = new Vector2(0, 1); }
            Vector2 saberDir = new Vector2(
                SwingUtility.Clamp((float)Math.Round(simulatedSaberDirection.X), -1, 1), SwingUtility.Clamp((float)Math.Round(simulatedSaberDirection.Y), -1, 1));
            int approxCutDir = SwingDataGeneration.DirectionalVectorToCutDirection[saberDir];
            Note fakeNote = new Note() { x = lastNote.x, y = (int)simulatedHandPos.Y };
            int approxDotCutDir = SwingDataGeneration.opposingCutDict[SwingDataGeneration.CutDirFromNoteToNote(fakeNote, nextNote)];

            // Calculate the AFN values for current pointing direction for either parity, and for next note
            float foreAFN = SwingDataGeneration.ForehandDict[approxCutDir];
            float backAFN = SwingDataGeneration.BackhandDict[approxCutDir];
            float nextForeAFN = SwingDataGeneration.ForehandDict[approxDotCutDir];
            float nextBackAFN = SwingDataGeneration.BackhandDict[approxDotCutDir];

            // First do a check against which next AFN is least:
            // If we assume the hand is currently BH and next would be FH
            if (Math.Abs(nextForeAFN - backAFN) < Math.Abs(nextBackAFN - foreAFN))
            {
                // If pretending next value is forehand, then if backhand its not a reset
                if (lastSwing.swingParity == Parity.Backhand) break;
            }
            else if (Math.Abs(nextBackAFN - foreAFN) < Math.Abs(nextForeAFN - backAFN))
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
            }

            // Currently if no bombs in path, should also say AND angle change <= 90 I think
            Vector2 directionToNote = new Vector2(nextNote.x, nextNote.y) - new Vector2(lastNote.x, lastNote.y);
            directionToNote = new Vector2(
               SwingUtility.Clamp((float)Math.Round(directionToNote.X), -1, 1), SwingUtility.Clamp((float)Math.Round(directionToNote.Y), -1, 1));
            //if (!BombInSwingPath(directionToNote, new Vector2(lastSwing.endPos.x, lastSwing.endPos.y), overallBombDensity, 5)) break;

            currentSwing.resetType = ResetType.Bomb;
            return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }

        #endregion

        #region RESET CALC

        // If last cut is entirely dot notes and next cut is too, then parity is assumed to be maintained
        if (lastSwing.notes.All(x => x.d == 8) && currentSwing.notes.All(x => x.d == 8))
        {
            return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand;
        }

        // If we can evaluate based on timeTillNextNote
        //if (timeTillNextNote != -1 && currentSwing.notes.Any(x => x.d != 8))
        //{
        //    // If time exceeds 400ms
        //    if (timeTillNextNote >= 512 && (Math.Abs(lastSwing.endPos.rotation) == 180 || Math.Abs(nextAFN) == 180))
        //    {
        //       currentSwing.resetType = ResetType.Rebound;
        //        return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        //
        //    }
        //}

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
        if (Math.Abs(AFNChange) > 270 && !UpsideDown)
        {
            currentSwing.resetType = ResetType.Rebound;
            return (lastSwing.swingParity == Parity.Forehand) ? Parity.Forehand : Parity.Backhand;
        }
        else { return (lastSwing.swingParity == Parity.Forehand) ? Parity.Backhand : Parity.Forehand; }

        #endregion
    }

    static bool BombInSwingPath(Vector2 direction, Vector2 currentPos, int[,] bombDensity, int spaces = 2)
    {
        // Step through grid spaces and check if bombs are there
        // Shouldn't need to be normalized since its grid spaces not actual space, 1,1 ect.. is fine
        for (int step = 0; step < spaces; step++)
        {
            Vector2 posToCheck = currentPos + (direction * step);
            posToCheck = new Vector2(SwingUtility.Clamp((float)Math.Round(posToCheck.X), 0, 3), SwingUtility.Clamp((float)Math.Round(posToCheck.Y), 0, 2));
            if (bombDensity[(int)posToCheck.X, (int)posToCheck.Y] > 0)
            {
                return true;
            }
        }

        return false;
    }
}
