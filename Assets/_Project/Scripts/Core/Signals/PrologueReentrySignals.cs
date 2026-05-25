namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Cold-start prewarm for orbital drop signal lanes.
    /// </summary>
    public static class PrologueReentrySignalLanes
    {
        /// <summary>
        /// Ensures the prologue signal lanes allocate before the whiteout moment.
        /// </summary>
        public static void Warm()
        {
            Hecton8.Core.SignalCorridorRuntime.EnsureInitialized();
        }
    }
}
