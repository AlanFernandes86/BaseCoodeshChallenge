namespace OrderGenerator.Infrastructure.FIX
{
    /// <summary>
    /// Manages the lifecycle of the FIX Session.
    /// </summary>
    public interface IFIXSessionManager
    {
        void Start(string configFilePath);
        void Stop();
    }
}
