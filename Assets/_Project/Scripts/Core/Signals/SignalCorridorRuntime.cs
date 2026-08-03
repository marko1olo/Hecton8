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

        public static void PreSimulationHeartbeat()
        {
            GlobalSignals.PreSimulationHeartbeat();
        }

        public static void FlushPostSimulation()
        {
            // L19 hop2 LIVE: SignalBus<InputSignal>.FlushPostSimulation → TryAcquireWriteLock
            // has produced mono_jit_compile_method AV under headless batch probes after WORLDDRIVER/INPUTHOP.
            // Probe moments already observe input via hop census; skip post-sim snapshot flush under batchmode.
            if (UnityEngine.Application.isBatchMode)
                return;

            GlobalSignals.FlushPostSimulation();
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
