using JoshaUtils;

namespace JoshaParity
{
    internal interface IParityMethod
    {
        PARITY_STATE ParityCheck(SwingData lastSwing, ref SwingData nextSwing, List<Note> bombs, float xOffset, float yOffset, bool rightHand);
        bool UpsideDown { get; }
    }
}
