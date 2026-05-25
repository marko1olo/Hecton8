using Hecton8.Core.Contracts.Signals;
using UnityEngine.Scripting;

namespace Hecton8.Core.Diagnostics.Visuals
{
    /// <summary>
    /// Zero-allocation helper for publishing Architect Eye diagnostic visuals.
    /// </summary>
    [Preserve]
    public static class ArchitectEyeDebugBus
    {
        private static int s_x001ArchitectEyeDebugSignalSignalPushDropCount;
        /// <summary>Ensures the isolated diagnostics lane exists before first use.</summary>
        public static void EnsureInitialized()
        {
            global::Hecton8.Core.SignalCorridorRuntime.EnsureDebugSignalLaneInitialized();
        }

        /// <summary>Publishes one diagnostic visual payload.</summary>
        public static void Push(in DebugSignal signal)
        {
            EnsureInitialized();
            SignalBus<DebugSignal>.TryPushTracked(in signal, ref s_x001ArchitectEyeDebugSignalSignalPushDropCount);
        }

    }
}
