namespace JoshaParity
{
    /// <summary>
    /// A method that takes in a swing and Parity Context then determines the current Parity
    /// </summary>
    public interface IParityMethod
    {
        Parity ParityCheck(ref SwingData currentSwing, ParityCheckContext context);
    }
}
