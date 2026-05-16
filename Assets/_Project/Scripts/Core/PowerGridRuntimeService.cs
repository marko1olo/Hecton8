using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core
{
    /// <summary>
    /// Aggregate storage snapshot published by the global power runtime.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BatteryRuntimeSnapshot
    {
        /// <summary>Total committed stored energy across all runtime banks.</summary>
        public float TotalStoredEnergyWattSeconds;

        /// <summary>Total committed battery capacity across all runtime banks.</summary>
        public float TotalCapacityWattSeconds;

        /// <summary>Normalized aggregate charge across all runtime banks.</summary>
        public float ChargeNormalized;

        /// <summary>True while the grid runtime is reserving energy for emergency-only loads.</summary>
        public bool EmergencyReserveActive;
    }

    /// <summary>
    /// Aggregate power-grid runtime service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPowerGridService
    {
        /// <summary>Current runtime grid count.</summary>
        int GridCount { get; }

        /// <summary>Total active generation across all grids.</summary>
        float TotalGeneration { get; }

        /// <summary>Total active requested consumption across all grids.</summary>
        float TotalConsumption { get; }

        /// <summary>Aggregate battery state across all grids.</summary>
        BatteryRuntimeSnapshot BatterySnapshot { get; }

        /// <summary>
        /// Queues one submarine-only wireless tool-drain request against the power runtime.
        /// The request is accounted by the power owner on its next logistics evaluation.
        /// </summary>
        bool TryQueueWirelessToolDrain(float energyWattSeconds, out float grantedEnergyWattSeconds);

        /// <summary>
        /// Provides a no-copy read-only lane for visor power telemetry.
        /// </summary>
        bool TryGetGridPowerPotentialsReadOnly(int gridIndex, out NativeArray<float>.ReadOnly potentials);
    }
}
