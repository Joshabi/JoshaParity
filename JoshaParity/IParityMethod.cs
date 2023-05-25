using JoshaUtils;

namespace JoshaParity
{
    public interface IParityMethod
    {
        PARITY_STATE ParityCheck(SwingData lastCut, ref SwingData currentSwing, List<Bomb> bombs, int playerXOffset, bool rightHand, float timeTillNextNote = 0.1f);
        bool UpsideDown { get; }
    }
}
