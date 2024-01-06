namespace JoshaParity
{
    /// <summary>
    /// A method that takes in a swing and the next swing to come, bombs and time till next note and returns a parity prediction
    /// </summary>
    public interface IParityMethod
    {
        Parity ParityCheck(ParityCheckContext context);
    }
}
