using System.Collections.Generic;

namespace JoshaParity
{
    /// <summary>
    /// A method that takes in a swing and the next swing to come, bombs and time till next note and returns a parity prediction
    /// </summary>
    public interface IParityMethod
    {
        Parity ParityCheck(SwingData lastSwing, ref SwingData currentSwing, List<Bomb> bombs, float timeTillNextNote = 0.1f);
        bool UpsideDown { get; }
    }
}
