using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core
{
    /// <summary>
    /// Aggregate storage snapshot published by the global power runtime.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BatteryRuntimeSnapshot
    {
        /// <summary>Total committed stored energy across all runtime banks.</summary>
        [FieldOffset(0)] public float TotalStoredEnergyWattSeconds;

        /// <summary>Total committed battery capacity across all runtime banks.</summary>
        [FieldOffset(4)] public float TotalCapacityWattSeconds;

        /// <summary>Normalized aggregate charge across all runtime banks.</summary>
        [FieldOffset(8)] public float ChargeNormalized;

        /// <summary>Non-zero while the grid runtime is reserving energy for emergency-only loads.</summary>
        [FieldOffset(12)] public byte EmergencyReserveActive;
        [FieldOffset(13)] private byte _pad0;
        [FieldOffset(14)] private byte _pad1;
        [FieldOffset(15)] private byte _pad2;
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
