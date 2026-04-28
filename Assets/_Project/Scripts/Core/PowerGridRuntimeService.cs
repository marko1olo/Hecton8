namespace Hecton8.Core
{
    /// <summary>
    /// Aggregate storage snapshot published by the global power runtime.
    /// </summary>
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
    }
}
