using System.Collections.Generic;

namespace JoshaParity
{
    public interface IParityMethod
    {
        Parity ParityCheck(SwingData lastCut, ref SwingData currentSwing, List<Bomb> bombs, bool rightHand, float timeTillNextNote = 0.1f);
        bool UpsideDown { get; }
    }
}
