using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core
{
    internal static class PowerGridRuntimeLayout
    {
        internal const int BatterySnapshotStrideBytes = 16;
    }

    /// <summary>
    /// Aggregate storage snapshot published by the global power runtime.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = PowerGridRuntimeLayout.BatterySnapshotStrideBytes)]
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
        /// Reserves one submarine-only wireless tool drain against the committed power runtime.
        /// The returned grant is deducted from the wireless budget immediately and consumed by the power owner phase.
        /// </summary>
        bool TryQueueWirelessToolDrain(float energyWattSeconds, out float grantedEnergyWattSeconds);

        /// <summary>
        /// Provides a no-copy read-only lane for visor power telemetry.
        /// </summary>
        bool TryGetGridPowerPotentialsReadOnly(int gridIndex, out NativeArray<float>.ReadOnly potentials);
    }
}
