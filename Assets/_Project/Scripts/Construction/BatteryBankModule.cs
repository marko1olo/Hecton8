using Hecton8.Core;
using Hecton8.Power;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Construction
{
    /// <summary>
    /// Grid-connected battery storage bank.
    /// Owns only local charge state; dispatch planning remains inside <see cref="PowerGrid"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Battery Bank Module")]
    public sealed class BatteryBankModule : MonoBehaviour, IPowerComponent, IPoolable
    {
        private const float DispatchDeltaTimeSeconds = PowerGrid.LogisticsTickDeltaTimeSeconds;

        [Header("Storage")]
        [Tooltip("Total storable energy in watt-seconds.")]
        [SerializeField, Min(1f)] private float energyCapacityWattSeconds = 120000f;

        [Tooltip("Normalized initial charge restored on spawn and cold start.")]
        [SerializeField, Range(0f, 1f)] private float initialChargeNormalized = 1f;

        [Tooltip("Upper bound on charging power accepted from the grid.")]
        [SerializeField, Min(0f)] private float maxChargePowerWatts = 400f;

        [Tooltip("Upper bound on discharge power returned to the grid.")]
        [SerializeField, Min(0f)] private float maxDischargePowerWatts = 500f;

        [Tooltip("Fraction of incoming energy retained while charging.")]
        [SerializeField, Range(0.1f, 1f)] private float chargeEfficiency = 0.94f;

        [Tooltip("Fraction of stored energy converted back into grid output while discharging.")]
        [SerializeField, Range(0.1f, 1f)] private float dischargeEfficiency = 0.94f;

        [Tooltip("Power-priority used only while the bank is charging as a consumer.")]
        [SerializeField, Range(0, 100)] private int chargePriority = 96;
        [Header("Thermal Loss")]
        [Tooltip("Optional atmosphere owner that receives battery efficiency losses as room heat.")]
        [SerializeField, FormerlySerializedAs("atmosphereSystem")] private MonoBehaviour atmosphereSystemSource;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private float _debugStoredEnergyWattSeconds;
        [SerializeField] private float _debugChargeNormalized = 1f;
        [SerializeField] private float _debugPlannedGridPowerWatts;

        private float _storedEnergyWattSeconds;
        private float _plannedGridPowerWatts;
        private float _pendingStoredEnergyWattSeconds;
        private float _pendingHeatLossJoules;
        private bool _hasPendingDispatch;
        private bool _hasPower = true;
        private int _cachedRoomIndex = -1;
        private bool _hasCachedRoomWorldPosition;
        private float3 _cachedRoomWorldPosition;
        private Transform _cachedTransform;
        private ISubmarineAtmosphereRoomMutationSink _atmosphereSystem;

        /// <inheritdoc />
        public float PowerRating => _plannedGridPowerWatts;

        /// <inheritdoc />
        public int PowerPriority => chargePriority;

        /// <inheritdoc />
        public bool HasPower => _hasPower;

        /// <summary>Committed stored energy in watt-seconds.</summary>
        public float StoredEnergyWattSeconds => _storedEnergyWattSeconds;

        /// <summary>Total storage capacity in watt-seconds.</summary>
        public float CapacityWattSeconds => math.max(1f, energyCapacityWattSeconds);

        /// <summary>Normalized state of charge.</summary>
        public float ChargeNormalized => math.saturate(_storedEnergyWattSeconds / math.max(1f, energyCapacityWattSeconds));

        internal float ChargeEfficiency => math.max(0.1f, chargeEfficiency);

        internal float DischargeEfficiency => math.max(0.1f, dischargeEfficiency);

        internal float PlannedGridPowerWatts => _plannedGridPowerWatts;

        private void Awake()
        {
            CacheReferences();
            ResetChargeToInitialState();
        }

        private void OnEnable()
        {
            ResetCachedRoomBinding();
            CacheReferences();
            ResetDispatchPlan();
            RefreshDebugState();
        }

        /// <inheritdoc />
        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            ResetCachedRoomBinding();
            ResetChargeToInitialState();
            ResetDispatchPlan();
            RefreshDebugState();
        }

        /// <inheritdoc />
        public void OnDespawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            ResetCachedRoomBinding();
            ResetChargeToInitialState();
            ResetDispatchPlan();
            RefreshDebugState();
        }

        /// <inheritdoc />
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        internal float ResolveChargeAcceptanceWatts(float deltaTimeSeconds)
        {
            float safeDeltaTime = math.max(0.001f, deltaTimeSeconds);
            float safeCapacity = math.max(1f, energyCapacityWattSeconds);
            float missingEnergy = math.max(0f, safeCapacity - _storedEnergyWattSeconds);
            float safeEfficiency = math.max(0.1f, chargeEfficiency);
            float capacityLimitedPower = missingEnergy / (safeDeltaTime * safeEfficiency);
            return math.max(0f, math.min(math.max(0f, maxChargePowerWatts), capacityLimitedPower));
        }

        internal float ResolveDischargeAvailabilityWatts(float deltaTimeSeconds)
        {
            return ResolveDischargeAvailabilityWatts(deltaTimeSeconds, 0f);
        }

        internal float ResolveDischargeAvailabilityWatts(float deltaTimeSeconds, float reserveFloorNormalized)
        {
            float safeDeltaTime = math.max(0.001f, deltaTimeSeconds);
            float safeEfficiency = math.max(0.1f, dischargeEfficiency);
            float reserveEnergyFloor = math.saturate(reserveFloorNormalized) * math.max(1f, energyCapacityWattSeconds);
            float usableStoredEnergy = math.max(0f, _storedEnergyWattSeconds - reserveEnergyFloor);
            float energyLimitedPower = (usableStoredEnergy * safeEfficiency) / safeDeltaTime;
            return math.max(0f, math.min(math.max(0f, maxDischargePowerWatts), energyLimitedPower));
        }

        internal void ResetDispatchPlan()
        {
            _plannedGridPowerWatts = 0f;
            _pendingStoredEnergyWattSeconds = _storedEnergyWattSeconds;
            _pendingHeatLossJoules = 0f;
            _hasPendingDispatch = false;
            _debugPlannedGridPowerWatts = 0f;
        }

        internal void StageResolvedDispatch(float nextStoredEnergyWattSeconds, float plannedGridPowerWatts)
        {
            _pendingStoredEnergyWattSeconds = math.clamp(
                nextStoredEnergyWattSeconds,
                0f,
                math.max(1f, energyCapacityWattSeconds));
            _plannedGridPowerWatts = plannedGridPowerWatts;
            _pendingHeatLossJoules = ResolvePendingHeatLossJoules(_storedEnergyWattSeconds, _pendingStoredEnergyWattSeconds, plannedGridPowerWatts);
            _hasPendingDispatch = math.abs(plannedGridPowerWatts) > 0.0001f;
            _debugPlannedGridPowerWatts = plannedGridPowerWatts;
        }

        internal void CommitResolvedDispatch()
        {
            if (_hasPendingDispatch)
                _storedEnergyWattSeconds = _pendingStoredEnergyWattSeconds;

            FlushPendingHeatLoss();
            _hasPendingDispatch = false;
            RefreshDebugState();
        }

        internal void CommitResolvedDispatch(float serviceRatio)
        {
            float safeServiceRatio = math.saturate(math.select(0f, serviceRatio, math.isfinite(serviceRatio)));
            if (_hasPendingDispatch && _plannedGridPowerWatts < -0.0001f)
            {
                if (safeServiceRatio <= 0.0001f)
                {
                    ResetDispatchPlan();
                    RefreshDebugState();
                    return;
                }

                if (safeServiceRatio < 0.9999f)
                {
                    float scaledStoredGain = math.max(0f, _pendingStoredEnergyWattSeconds - _storedEnergyWattSeconds) * safeServiceRatio;
                    _pendingStoredEnergyWattSeconds = math.clamp(
                        _storedEnergyWattSeconds + scaledStoredGain,
                        0f,
                        math.max(1f, energyCapacityWattSeconds));
                    _plannedGridPowerWatts *= safeServiceRatio;
                    _pendingHeatLossJoules = ResolvePendingHeatLossJoules(
                        _storedEnergyWattSeconds,
                        _pendingStoredEnergyWattSeconds,
                        _plannedGridPowerWatts);
                    _debugPlannedGridPowerWatts = _plannedGridPowerWatts;
                }
            }

            CommitResolvedDispatch();
        }

        private void ResetChargeToInitialState()
        {
            _storedEnergyWattSeconds = math.saturate(initialChargeNormalized) * math.max(1f, energyCapacityWattSeconds);
            _pendingStoredEnergyWattSeconds = _storedEnergyWattSeconds;
        }

        private void ResetCachedRoomBinding()
        {
            _cachedRoomIndex = -1;
            _hasCachedRoomWorldPosition = false;
            _cachedRoomWorldPosition = default;
            _atmosphereSystem = null;
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
            {
                _atmosphereSystem = atmosphereSystemSource as ISubmarineAtmosphereRoomMutationSink;
                if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                    ConstructionParentLookup.TryCaptureSelfOrParent(this, out _atmosphereSystem);
            }

            if (_atmosphereSystem == null || _cachedTransform == null)
                return;

            Vector3 worldPosition = _cachedTransform.position;
            float3 currentPosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (_hasCachedRoomWorldPosition &&
                math.lengthsq(currentPosition - _cachedRoomWorldPosition) > 0.25f)
            {
                _cachedRoomIndex = -1;
                _hasCachedRoomWorldPosition = false;
            }

            if (_cachedRoomIndex >= 0)
                return;

            _cachedRoomIndex = _atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(_cachedTransform.position);
            _cachedRoomWorldPosition = currentPosition;
            _hasCachedRoomWorldPosition = _cachedRoomIndex >= 0;
        }

        private float ResolvePendingHeatLossJoules(float currentStoredEnergyWattSeconds, float nextStoredEnergyWattSeconds, float plannedGridPowerWatts)
        {
            if (plannedGridPowerWatts < -0.0001f)
            {
                float importedEnergy = -plannedGridPowerWatts * DispatchDeltaTimeSeconds;
                float storedGain = math.max(0f, nextStoredEnergyWattSeconds - currentStoredEnergyWattSeconds);
                return math.max(0f, importedEnergy - storedGain);
            }

            if (plannedGridPowerWatts > 0.0001f)
            {
                float deliveredEnergy = plannedGridPowerWatts * DispatchDeltaTimeSeconds;
                float storedLoss = math.max(0f, currentStoredEnergyWattSeconds - nextStoredEnergyWattSeconds);
                return math.max(0f, storedLoss - deliveredEnergy);
            }

            return 0f;
        }

        private void FlushPendingHeatLoss()
        {
            if (_pendingHeatLossJoules <= 0f)
                return;

            CacheReferences();
            if (_atmosphereSystem != null && _cachedRoomIndex >= 0)
                _atmosphereSystem.InjectRoomHeatEnergyJoules(_cachedRoomIndex, _pendingHeatLossJoules);

            _pendingHeatLossJoules = 0f;
        }

        private void RefreshDebugState()
        {
            _debugStoredEnergyWattSeconds = _storedEnergyWattSeconds;
            _debugChargeNormalized = ChargeNormalized;
            _debugPlannedGridPowerWatts = _plannedGridPowerWatts;
        }

        internal float TryConsumeDirectGridEnergy(float requestedGridEnergyWattSeconds, float reserveFloorNormalized)
        {
            if (requestedGridEnergyWattSeconds <= 0f)
                return 0f;

            float safeCapacity = math.max(1f, energyCapacityWattSeconds);
            float reserveEnergyFloor = math.saturate(reserveFloorNormalized) * safeCapacity;
            float safeEfficiency = math.max(0.1f, dischargeEfficiency);
            float availableStoredEnergy = math.max(0f, _storedEnergyWattSeconds - reserveEnergyFloor);
            if (availableStoredEnergy <= 0f)
                return 0f;

            float maxDeliverableByStoredEnergy = availableStoredEnergy * safeEfficiency;
            float maxDeliverableByPower = math.max(0f, maxDischargePowerWatts) * DispatchDeltaTimeSeconds;
            float deliveredGridEnergy = math.min(
                requestedGridEnergyWattSeconds,
                math.min(maxDeliverableByStoredEnergy, maxDeliverableByPower));
            if (deliveredGridEnergy <= 0f)
                return 0f;

            float nextStoredEnergy = math.max(0f, _storedEnergyWattSeconds - (deliveredGridEnergy / safeEfficiency));
            _pendingHeatLossJoules = ResolvePendingHeatLossJoules(
                _storedEnergyWattSeconds,
                nextStoredEnergy,
                deliveredGridEnergy / DispatchDeltaTimeSeconds);
            _storedEnergyWattSeconds = nextStoredEnergy;
            FlushPendingHeatLoss();
            RefreshDebugState();
            return deliveredGridEnergy;
        }
    }
}
