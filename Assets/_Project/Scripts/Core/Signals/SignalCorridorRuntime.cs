namespace Hecton8.Core
{
    /// <summary>
    /// Public lifecycle route for the typed signal corridor.
    /// Keeps domain bootstrap and dispatcher phases off the legacy GlobalSignals facade.
    /// </summary>
    public static class SignalCorridorRuntime
    {
        public static void EnsureInitialized()
        {
            GlobalSignals.InitializeAllQueues();
        }

        public static void Dispose()
        {
            GlobalSignals.DisposeAllQueues();
        }

        public static void FlushPreSimulation()
        {
            GlobalSignals.FlushPreSimulation();
        }

        public static void ClearPostSimulationSnapshots()
        {
            GlobalSignals.ClearPostSimulationSnapshots();
        }

        public static void EnsureDebugSignalLaneInitialized()
        {
            GlobalSignals.EnsureDebugSignalLaneInitialized();
        }

        public static void EnsureHapticPulseSignalLaneInitialized()
        {
            GlobalSignals.EnsureHapticPulseSignalLaneInitialized();
        }
    }
}
