using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Aggregate runtime power snapshot published by <see cref="PowerGridManager"/>.
    /// </summary>
    public readonly struct PowerGridTelemetrySnapshot
    {
        public PowerGridTelemetrySnapshot(
            int gridCount,
            int deficitGridCount,
            float totalGeneration,
            float totalConsumption,
            float supplyRatio,
            float batteryChargeNormalized,
            float availablePowerNormalized,
            LogisticsBrownoutTier highestBrownoutTier,
            bool hasPowerDeficit,
            bool emergencyReserveActive)
        {
            GridCount = gridCount;
            DeficitGridCount = deficitGridCount;
            TotalGeneration = totalGeneration;
            TotalConsumption = totalConsumption;
            SupplyRatio = supplyRatio;
            BatteryChargeNormalized = batteryChargeNormalized;
            AvailablePowerNormalized = availablePowerNormalized;
            HighestBrownoutTier = highestBrownoutTier;
            HasPowerDeficit = hasPowerDeficit;
            EmergencyReserveActive = emergencyReserveActive;
        }

        /// <summary>Connected runtime grid count.</summary>
        public int GridCount { get; }

        /// <summary>How many grids currently report a deficit.</summary>
        public int DeficitGridCount { get; }

        /// <summary>Total authored generation in watts.</summary>
        public float TotalGeneration { get; }

        /// <summary>Total authored demand in watts.</summary>
        public float TotalConsumption { get; }

        /// <summary>Aggregate generation to demand ratio for the current pass.</summary>
        public float SupplyRatio { get; }

        /// <summary>Aggregate battery charge normalized to 0..1.</summary>
        public float BatteryChargeNormalized { get; }

        /// <summary>
        /// Best-effort runtime power health normalized to 0..1.
        /// Uses battery charge when storage exists; otherwise falls back to supply ratio.
        /// </summary>
        public float AvailablePowerNormalized { get; }

        /// <summary>Worst brownout tier observed across all active grids.</summary>
        public LogisticsBrownoutTier HighestBrownoutTier { get; }

        /// <summary>True while any grid is undersupplied.</summary>
        public bool HasPowerDeficit { get; }

        /// <summary>True while any battery bank is in emergency-reserve mode.</summary>
        public bool EmergencyReserveActive { get; }
    }

    /// <summary>
    /// Static aggregate power telemetry bus for submarine and HUD observers.
    /// </summary>
    public static class PowerGridTelemetryEvents
    {
        /// <summary>Zero-allocation delegate for aggregate power snapshots.</summary>
        public delegate void TelemetryUpdatedHandler(in PowerGridTelemetrySnapshot snapshot);

        /// <summary>Raised after <see cref="PowerGridManager"/> completes a SlowTick evaluation pass.</summary>
        public static event TelemetryUpdatedHandler OnTelemetryUpdated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnTelemetryUpdated = null;
        }

        /// <summary>
        /// Publishes one aggregate runtime snapshot.
        /// </summary>
        public static void Raise(in PowerGridTelemetrySnapshot snapshot)
        {
            OnTelemetryUpdated?.Invoke(snapshot);
        }
    }
}
