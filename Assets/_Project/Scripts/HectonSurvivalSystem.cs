using System;
using Hecton8.Core;
using Hecton8.Meta;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Atmosphere;

namespace Hecton8.Gameplay
{
    public enum SurvivalDeathCause : byte
    {
        None = 0,
        OxygenDepletion = 1,
        PressureCollapse = 2,
        ThermalFailure = 3,
        RadiationExposure = 4,
        Starvation = 5,
        Dehydration = 6,
        IntegrityFailure = 7
    }

    /// <summary>
    /// Persisted telemetry for the last completed life.
    /// Used by death-facing UX and navigation systems to surface the latest loss marker.
    /// </summary>
    public readonly struct SurvivalDeathRecord
    {
        public SurvivalDeathRecord(
            SurvivalDeathCause cause,
            Vector3 position,
            float lifeDurationSeconds,
            float peakDepthMeters,
            float lowestOxygenNormalized,
            float lowestEnergyNormalized,
            float lowestIntegrityNormalized)
        {
            Cause = cause;
            Position = position;
            LifeDurationSeconds = lifeDurationSeconds;
            PeakDepthMeters = peakDepthMeters;
            LowestOxygenNormalized = lowestOxygenNormalized;
            LowestEnergyNormalized = lowestEnergyNormalized;
            LowestIntegrityNormalized = lowestIntegrityNormalized;
        }

        /// <summary>Resolved fatal cause for the recorded life.</summary>
        public SurvivalDeathCause Cause { get; }

        /// <summary>World-space position where the last life ended.</summary>
        public Vector3 Position { get; }

        /// <summary>Total survived time for the recorded life.</summary>
        public float LifeDurationSeconds { get; }

        /// <summary>Deepest reached depth for the recorded life.</summary>
        public float PeakDepthMeters { get; }

        /// <summary>Lowest normalized oxygen reached during the recorded life.</summary>
        public float LowestOxygenNormalized { get; }

        /// <summary>Lowest normalized energy reached during the recorded life.</summary>
        public float LowestEnergyNormalized { get; }

        /// <summary>Lowest normalized integrity reached during the recorded life.</summary>
        public float LowestIntegrityNormalized { get; }
    }

    /// <summary>
    /// Core survival simulation for the Hecton diving suit.
    /// Attach to the player GameObject and assign a SurvivalStats asset.
    /// 
    /// FEATURES:
    ///   • Zero-GC Tick System (ITickable, ISlowTickable)
    ///   • Atmospheric Hazards (Pressure, Temperature, Radiation)
    ///   • Suit Resource Management (O₂, Energy, Integrity)
    ///   • Persistence (ISaveable)
    ///   • Throttled HUD Events
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonSurvivalSystem : MonoBehaviour, ITickable, ISlowTickable, ISaveable
    {
        // ═════════════════════════════════════════════════════════
        //  INSPECTOR
        // ═════════════════════════════════════════════════════════

        [Header("── Data ────────────────────────────────────")]
        [Tooltip("Drag a SurvivalStats .asset here to configure all suit parameters.")]
        [SerializeField] private SurvivalStats stats;

        [Header("── Scene ───────────────────────────────────")]
        [Tooltip("World-space Y coordinate of the water surface.")]
        [SerializeField] private float surfaceWorldY;
        [Tooltip("Surface oxygen refill rate per second when the shared surface contract says the head is in air.")]
        [SerializeField] private float surfaceOxygenRefillRate = 15f;

        // ═════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ═════════════════════════════════════════════════════════

        private float oxygen;
        private float energy;
        private float depth;
        private float integrity;
        private float pressure;
        private float weight;
        private float hunger;
        private float thirst;
        private bool  alive = true;

        private float _slowTickDt = 0.5f;

        // Throttling / Event publishing
        private float lastPubOxygen;
        private float lastPubEnergy;
        private float lastPubDepth;
        private float lastPubIntegrity;
        private float lastPubPressure;
        private float lastPubTemp;
        private float lastPubRad;
        private float lastPubHunger;
        private float lastPubThirst;

        // Hazard Grace Periods
        private float _tempGraceTimer;
        private float _radGraceTimer;
        private HectonPlayerMovement _playerMovement;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private bool _surfaceContractUnderwater;
        private float _runtimeOxygenCapacityMultiplier = 1f;
        private SurvivalDeathCause _lastDeathCause;
        private SurvivalDeathCause _pendingIntegrityDeathCause;
        private float _currentLifeDurationSeconds;
        private float _currentLifePeakDepthMeters;
        private float _currentLifeLowestOxygenNormalized = 1f;
        private float _currentLifeLowestEnergyNormalized = 1f;
        private float _currentLifeLowestIntegrityNormalized = 1f;
        private float _currentPressureExposureSeconds;
        private float _currentPressurePeakExcessMeters;
        private float _currentPressurePeakDamagePerSecond;
        private SurvivalDeathRecord _lastDeathRecord;
        private bool _hasLastDeathRecord;
        private const float HazardGraceDuration = 3f;
        private const float PressureIncidentLogDurationThreshold = 4f;
        private const float PressureIncidentLogExcessThreshold = 6f;

        private const float Epsilon       = 0.1f;
        private const float DirtySentinel = -9999f;

        // ═════════════════════════════════════════════════════════
        //  PUBLIC EVENTS
        // ═════════════════════════════════════════════════════════

        public event Action<float> OnOxygenChanged;
        public event Action<float> OnEnergyChanged;
        public event Action<float> OnDepthChanged;
        public event Action<float> OnIntegrityChanged;
        public event Action<float> OnPressureChanged;
        public event Action<float> OnWeightChanged;
        public event Action<float> OnOxygenCritical;
        public event Action<float> OnTemperatureChanged;
        public event Action<float> OnRadiationChanged;
        public event Action<float> OnHungerChanged;
        public event Action<float> OnThirstChanged;
        public event Action<float> OnHungerCritical;
        public event Action<float> OnThirstCritical;
        public event Action        OnDeath;

        // ═════════════════════════════════════════════════════════
        //  PROPERTIES
        // ═════════════════════════════════════════════════════════

        public float Oxygen              => oxygen;
        public float Energy              => energy;
        public float Depth               => depth;
        public float Integrity           => integrity;
        public float Pressure            => pressure;
        public float Weight              => weight;
        public float Hunger              => hunger;
        public float Thirst              => thirst;
        public bool  IsAlive             => alive;
        public SurvivalStats Stats       => stats;

        public float OxygenNormalized    => oxygen    / ResolveRuntimeMaxOxygenCapacity();
        public float EnergyNormalized    => energy    / stats.MaxEnergy;
        public float IntegrityNormalized => integrity / stats.MaxIntegrity;
        public float HungerNormalized    => hunger    / stats.MaxHunger;
        public float ThirstNormalized    => thirst    / stats.MaxThirst;
        public float EnergyPercent       => EnergyNormalized * 100f;
        public float HungerPercent       => HungerNormalized * 100f;
        public float ThirstPercent       => ThirstNormalized * 100f;
        public SurvivalDeathCause LastDeathCause => _lastDeathCause;
        /// <summary>Total elapsed time for the currently active life.</summary>
        public float CurrentLifeDurationSeconds => _currentLifeDurationSeconds;
        /// <summary>Deepest reached depth for the currently active life.</summary>
        public float CurrentLifePeakDepthMeters => _currentLifePeakDepthMeters;
        /// <summary>True when a persisted last-loss marker record is available.</summary>
        public bool HasLastDeathRecord => _hasLastDeathRecord;
        /// <summary>World-space marker position for the latest recorded death.</summary>
        public Vector3 LastDeathMarkerPosition => _lastDeathRecord.Position;
        /// <summary>Latest persisted death telemetry record.</summary>
        public SurvivalDeathRecord LastDeathRecord => _lastDeathRecord;
        /// <summary>Signed margin to the authored safe depth. Negative values mean active overpressure.</summary>
        public float SafeDepthMarginMeters => stats != null ? stats.SafeDepth - depth : 0f;
        /// <summary>Positive metres beyond the safe depth envelope.</summary>
        public float OverpressureMeters => stats != null ? math.max(0f, depth - stats.SafeDepth) : 0f;
        /// <summary>True when the suit is already deeper than its safe depth rating.</summary>
        public bool IsBeyondSafeDepth => OverpressureMeters > 0f;
        /// <summary>Current integrity attrition per second caused by overpressure.</summary>
        public float PressureDamagePerSecond => ResolveCurrentPressureDamagePerSecond();
        /// <summary>Normalized live overpressure severity for advisory systems.</summary>
        public float PressureExposureSeverity01 => ResolvePressureExposureSeverity01();

        // ═════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════

        private void Awake()
        {
            if (stats == null)
            {
                Debug.LogError($"[HectonSurvival] SurvivalStats not assigned on {name}. Disabling.");
                enabled = false;
                return;
            }

            TryGetComponent(out _playerMovement);
            TryGetComponent(out _playerTransportCoordinator);
            ResetToMax();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.RegisterAll(this);
                _slowTickDt = 0.5f; // Match with manager
            }
            SaveManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.UnregisterAll(this);
            SaveManager.Instance?.Unregister(this);
        }

        // ═════════════════════════════════════════════════════════
        //  TICK SYSTEMS
        // ═════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (!alive) return;

            ComputeDepthAndPressure();
            TrackCurrentLifeTelemetry(deltaTime);
            TrackPressureExposure(deltaTime);
            PublishDirty();
            CheckLethalConditions();
        }

        public void SlowTick()
        {
            if (!alive) return;

            float dt = _slowTickDt;

            UpdateOxygen(dt);
            DrainPassiveEnergy(dt);
            ApplyPressureDamage(dt);
            HandleTemperature(dt);
            HandleRadiation(dt);
            UpdateHungerAndThirst(dt);
        }

        // ═════════════════════════════════════════════════════════
        //  SIMULATION STEPS
        // ═════════════════════════════════════════════════════════

        private void ComputeDepthAndPressure()
        {
            if (_playerMovement != null)
            {
                surfaceWorldY = _playerMovement.CurrentWaterSurfaceY;
                depth = math.max(0f, _playerMovement.CurrentDepth);
                pressure = 1f + depth * 0.1f;
                return;
            }

            depth    = math.max(0f, surfaceWorldY - transform.position.y);
            pressure = 1f + depth * 0.1f;
        }

        private void UpdateOxygen(float dt)
        {
            _surfaceContractUnderwater = ResolveSurfaceContractUnderwater();

            if (!_surfaceContractUnderwater)
            {
                oxygen = math.min(
                    ResolveRuntimeMaxOxygenCapacity(),
                    oxygen + surfaceOxygenRefillRate * dt);
                return;
            }

            float pressureFactor = math.max(1f, pressure * 0.5f);
            float oxygenConsumptionScale = ResolveTransportOxygenConsumptionScale();
            if (oxygenConsumptionScale <= 0f)
                return;

            DifficultyModifierData modifiers = DynamicDifficultyDirector.Current;
            oxygen = math.max(0f, oxygen - stats.OxygenConsumptionRate * pressureFactor * oxygenConsumptionScale * modifiers.OxygenDepletionRate * dt);
        }

        private bool ResolveSurfaceContractUnderwater()
        {
            if (_playerMovement != null)
            {
                switch (_playerMovement.CurrentLocomotionMode)
                {
                    case PlayerLocomotionMode.UnderwaterSwim:
                        return true;

                    case PlayerLocomotionMode.SurfaceSwim:
                        return _playerMovement.IsPlayerSubmerged;

                    default:
                        return false;
                }
            }

            return SurfaceStateUtility.ResolveUnderwaterFromDepth(
                depth,
                _surfaceContractUnderwater);
        }

        private void DrainPassiveEnergy(float dt)
        {
            float weightFactor = 1f + weight * 0.005f;
            energy = math.max(0f, energy - stats.EnergyConsumptionRate * weightFactor * dt);
        }

        private void ApplyPressureDamage(float dt)
        {
            if (depth <= stats.SafeDepth) return;

            float pressureDamageScale = ResolveTransportPressureDamageScale();
            if (pressureDamageScale <= 0f) return;

            float excess = depth - stats.SafeDepth;
            float scale  = 1f + excess * stats.PressureScalePerMeter;
            float damageMultiplier = DynamicDifficultyDirector.Current.DamageMultiplier;
            integrity = math.max(0f, integrity - stats.PressureDamageRate * scale * pressureDamageScale * damageMultiplier * dt);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.PressureCollapse);
        }

        private void HandleTemperature(float dt)
        {
            var atmosphere = HectonAtmosphereManager.Instance;
            float baseTemp = atmosphere != null ? atmosphere.CurrentTemperature : 20f;
            
            // Add local heat sources
            float localHeat = HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Heat);
            float currentTemp = baseTemp + localHeat;

            float excess = 0f;
            if (currentTemp < stats.MinSafeTemp)
                excess = stats.MinSafeTemp - currentTemp;
            else if (currentTemp > stats.MaxSafeTemp)
                excess = currentTemp - stats.MaxSafeTemp;

            if (excess <= 0f)
            {
                _tempGraceTimer = 0f;
                return;
            }

            float thermalExposureScale = ResolveTransportThermalExposureScale();
            if (thermalExposureScale <= 0f)
            {
                _tempGraceTimer = 0f;
                return;
            }

            _tempGraceTimer += dt;
            if (_tempGraceTimer < HazardGraceDuration) return;

            // Suit energy drain for thermal regulation
            float heatDrain = excess * stats.TempEnergyScale * thermalExposureScale * dt;
            energy = math.max(0f, energy - heatDrain);

            // Thermal integrity damage
            float damage = stats.TempDamageRate * (1f + excess * 0.1f) * thermalExposureScale * dt;
            integrity = math.max(0f, integrity - damage);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.ThermalFailure);
        }

        private void HandleRadiation(float dt)
        {
            var atmosphere = HectonAtmosphereManager.Instance;
            float baseRad = atmosphere != null ? atmosphere.CurrentRadiation : 0f;

            // Add local radiation sources
            float localRad = HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Radiation);
            float currentRad = baseRad + localRad;

            if (currentRad <= stats.RadiationThreshold)
            {
                _radGraceTimer = 0f;
                return;
            }

            float radiationExposureScale = ResolveTransportRadiationExposureScale();
            if (radiationExposureScale <= 0f)
            {
                _radGraceTimer = 0f;
                return;
            }

            _radGraceTimer += dt;
            if (_radGraceTimer < HazardGraceDuration) return;

            float excess = currentRad - stats.RadiationThreshold;
            float damage = excess * stats.RadiationDamageRate * radiationExposureScale * dt;
            
            integrity = math.max(0f, integrity - damage);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.RadiationExposure);
        }

        private PlayerTransportPreset ResolveActiveTransportPreset()
        {
            return _playerTransportCoordinator != null
                ? _playerTransportCoordinator.ResolveTransportPreset()
                : null;
        }

        private float ResolveTransportOxygenConsumptionScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? math.max(0f, transportPreset.OxygenConsumptionScale)
                : 1f;
        }

        private float ResolveTransportPressureDamageScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? math.max(0f, transportPreset.PressureDamageScale)
                : 1f;
        }

        private float ResolveTransportThermalExposureScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? math.max(0f, transportPreset.ThermalExposureScale)
                : 1f;
        }

        private float ResolveTransportRadiationExposureScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? math.max(0f, transportPreset.RadiationExposureScale)
                : 1f;
        }

        private void UpdateHungerAndThirst(float dt)
        {
            // Drain hunger
            hunger = math.max(0f, hunger - stats.HungerDrainRate * dt);

            // Drain thirst (slightly faster)
            thirst = math.max(0f, thirst - stats.ThirstDrainRate * dt);

            // Apply starvation damage if hunger is 0
            if (hunger <= 0f)
            {
                integrity = math.max(0f, integrity - stats.StarvationDamageRate * dt);
                MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.Starvation);
            }

            // Apply dehydration damage if thirst is 0
            if (thirst <= 0f)
            {
                integrity = math.max(0f, integrity - stats.DehydrationDamageRate * dt);
                MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.Dehydration);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  EVENT PUBLISHING
        // ═════════════════════════════════════════════════════════

        private void PublishDirty()
        {
            if (math.abs(oxygen - lastPubOxygen) > Epsilon)
            {
                lastPubOxygen = oxygen;
                OnOxygenChanged?.Invoke(oxygen);
                if (OxygenNormalized < 0.15f) OnOxygenCritical?.Invoke(OxygenNormalized);
            }

            if (math.abs(energy - lastPubEnergy) > Epsilon)
            {
                lastPubEnergy = energy;
                OnEnergyChanged?.Invoke(energy);
            }

            if (math.abs(depth - lastPubDepth) > Epsilon)
            {
                lastPubDepth = depth;
                OnDepthChanged?.Invoke(depth);
            }

            if (math.abs(integrity - lastPubIntegrity) > Epsilon)
            {
                lastPubIntegrity = integrity;
                OnIntegrityChanged?.Invoke(integrity);
            }

            if (math.abs(pressure - lastPubPressure) > Epsilon)
            {
                lastPubPressure = pressure;
                OnPressureChanged?.Invoke(pressure);
            }

            var atmosphere = HectonAtmosphereManager.Instance;
            
            // Temperature Publishing (Atmosphere + Local)
            float baseTemp = atmosphere != null ? atmosphere.CurrentTemperature : 20f;
            float totalTemp = baseTemp + HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Heat);
            if (math.abs(totalTemp - lastPubTemp) > Epsilon)
            {
                lastPubTemp = totalTemp;
                OnTemperatureChanged?.Invoke(totalTemp);
            }

            // Radiation Publishing (Atmosphere + Local)
            float baseRad = atmosphere != null ? atmosphere.CurrentRadiation : 0f;
            float totalRad = baseRad + HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Radiation);
            if (math.abs(totalRad - lastPubRad) > Epsilon)
            {
                lastPubRad = totalRad;
                OnRadiationChanged?.Invoke(totalRad);
            }

            // Hunger Publishing
            if (math.abs(hunger - lastPubHunger) > Epsilon)
            {
                lastPubHunger = hunger;
                OnHungerChanged?.Invoke(hunger);
                if (HungerNormalized < 0.15f) OnHungerCritical?.Invoke(HungerNormalized);
            }

            // Thirst Publishing
            if (math.abs(thirst - lastPubThirst) > Epsilon)
            {
                lastPubThirst = thirst;
                OnThirstChanged?.Invoke(thirst);
                if (ThirstNormalized < 0.15f) OnThirstCritical?.Invoke(ThirstNormalized);
            }
        }

        private void CheckLethalConditions()
        {
            if (oxygen > 0f && integrity > 0f) return;

            alive = false;
            _lastDeathCause = ResolveDeathCause();
            CaptureDeathRecord();
            RecordDeathTelemetry();
            OnDeath?.Invoke();
            HectonEventBus.Publish(new PlayerDiedEvent(this, _lastDeathCause, _lastDeathRecord));
            enabled = false;
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═════════════════════════════════════════════════════════

        public void RefillOxygen(float amount)
        {
            oxygen = math.min(ResolveRuntimeMaxOxygenCapacity(), oxygen + math.max(0f, amount));
            ForceDirty(ref lastPubOxygen);
        }

        /// <summary>
        /// Applies a runtime-only oxygen-capacity multiplier without mutating the authored SurvivalStats asset.
        /// </summary>
        /// <param name="multiplier">Runtime oxygen-capacity multiplier.</param>
        public void SetRuntimeOxygenCapacityMultiplier(float multiplier)
        {
            _runtimeOxygenCapacityMultiplier = Mathf.Clamp(multiplier, 0.5f, 4f);
            oxygen = Mathf.Clamp(oxygen, 0f, ResolveRuntimeMaxOxygenCapacity());
            ForceDirty(ref lastPubOxygen);
        }

        public void RechargeEnergy(float amount)
        {
            energy = math.clamp(energy + amount, 0f, stats.MaxEnergy);
            ForceDirty(ref lastPubEnergy);
        }

        /// <summary>
        /// Consumes a fixed amount of suit energy immediately.
        /// </summary>
        /// <param name="amount">Absolute amount of energy to remove.</param>
        public void DrainEnergy(float amount)
        {
            if (amount <= 0f)
                return;

            energy = math.max(0f, energy - amount);
            ForceDirty(ref lastPubEnergy);
            CheckLethalConditions();
        }

        /// <summary>
        /// Consumes a fixed amount of suit oxygen immediately.
        /// </summary>
        /// <param name="amount">Absolute amount of oxygen to remove.</param>
        public void DrainOxygen(float amount)
        {
            if (amount <= 0f)
                return;

            oxygen = math.max(0f, oxygen - amount);
            ForceDirty(ref lastPubOxygen);
            CheckLethalConditions();
        }

        public void TakeDamage(float amount)
        {
            if (!alive || amount <= 0f) return;

            PlayerTakeDamageEvent damageEvent = HectonEventBus.Publish(new PlayerTakeDamageEvent(this, amount));
            if (damageEvent == null || damageEvent.IsCancelled)
                return;

            amount = damageEvent.DamageAmount;
            if (amount <= 0f)
                return;

            amount *= DynamicDifficultyDirector.Current.DamageMultiplier;
            if (amount <= 0f)
                return;

            integrity = math.max(0f, integrity - amount);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.IntegrityFailure);
            ForceDirty(ref lastPubIntegrity);
            CheckLethalConditions();
        }

        public void Repair(float amount)
        {
            integrity = math.min(stats.MaxIntegrity, integrity + math.max(0f, amount));
            ForceDirty(ref lastPubIntegrity);
        }

        /// <summary>
        /// Restores hunger by the specified amount.
        /// </summary>
        public void AddHunger(float amount)
        {
            hunger = math.min(stats.MaxHunger, hunger + math.max(0f, amount));
            ForceDirty(ref lastPubHunger);
        }

        /// <summary>
        /// Restores thirst by the specified amount.
        /// </summary>
        public void AddThirst(float amount)
        {
            thirst = math.min(stats.MaxThirst, thirst + math.max(0f, amount));
            ForceDirty(ref lastPubThirst);
        }

        public void SetWeight(float kg)
        {
            weight = math.max(0f, kg);
            OnWeightChanged?.Invoke(weight);
        }

        public void SetSurfaceY(float y) => surfaceWorldY = y;

        public void OverrideStats(SurvivalStats newStats)
        {
            if (newStats == null) return;
            stats = newStats;
            ForceAllDirty();
        }

        /// <summary>
        /// Returns the latest persisted death record when one exists.
        /// </summary>
        /// <param name="record">Latest last-loss telemetry record.</param>
        public bool TryGetLastDeathRecord(out SurvivalDeathRecord record)
        {
            record = _lastDeathRecord;
            return _hasLastDeathRecord;
        }

        /// <summary>
        /// Resolves player-facing survival advice for a fatal cause.
        /// </summary>
        /// <param name="cause">Fatal cause to translate into tactical advice.</param>
        public string GetDeathAdvice(SurvivalDeathCause cause)
        {
            return ResolveDeathAdvice(cause);
        }

        // ═════════════════════════════════════════════════════════
        //  SAVE SYSTEM
        // ═════════════════════════════════════════════════════════

        public int SavePriority => 10;
        public int LoadPriority => 10;

        public void PopulateSaveData(SaveData data)
        {
            ref PlayerStatsDTO dto = ref data.playerStats;
            dto.oxygen = oxygen;
            dto.energy = energy;
            dto.integrity = integrity;
            dto.weight = weight;
            dto.hunger = hunger;
            dto.thirst = thirst;
            dto.currentLifeDurationSeconds = _currentLifeDurationSeconds;
            dto.currentLifePeakDepthMeters = _currentLifePeakDepthMeters;
            dto.currentLifeLowestOxygenNormalized = _currentLifeLowestOxygenNormalized;
            dto.currentLifeLowestEnergyNormalized = _currentLifeLowestEnergyNormalized;
            dto.currentLifeLowestIntegrityNormalized = _currentLifeLowestIntegrityNormalized;
            dto.hasLastDeathRecord = _hasLastDeathRecord;
            dto.lastDeathCause = (byte)_lastDeathRecord.Cause;
            dto.lastDeathLifeDurationSeconds = _lastDeathRecord.LifeDurationSeconds;
            dto.lastDeathPeakDepthMeters = _lastDeathRecord.PeakDepthMeters;
            dto.lastDeathLowestOxygenNormalized = _lastDeathRecord.LowestOxygenNormalized;
            dto.lastDeathLowestEnergyNormalized = _lastDeathRecord.LowestEnergyNormalized;
            dto.lastDeathLowestIntegrityNormalized = _lastDeathRecord.LowestIntegrityNormalized;
            dto.SetLastDeathPosition(_lastDeathRecord.Position);
            dto.SetPosition(transform.position);
            dto.SetRotation(transform.rotation);
        }

        public void LoadFromSaveData(SaveData data)
        {
            PlayerStatsDTO dto = data.playerStats;
            bool hasTelemetryV23 = data.version >= 23;
            oxygen    = Mathf.Clamp(dto.oxygen,    0f, ResolveRuntimeMaxOxygenCapacity());
            energy    = Mathf.Clamp(dto.energy,    0f, stats.MaxEnergy);
            integrity = Mathf.Clamp(dto.integrity, 0f, stats.MaxIntegrity);
            weight    = Mathf.Max(0f, dto.weight);
            hunger    = Mathf.Clamp(dto.hunger,    0f, stats.MaxHunger);
            thirst    = Mathf.Clamp(dto.thirst,    0f, stats.MaxThirst);
            _currentLifeDurationSeconds = hasTelemetryV23 ? Mathf.Max(0f, dto.currentLifeDurationSeconds) : 0f;
            _currentLifePeakDepthMeters = hasTelemetryV23 ? Mathf.Max(0f, dto.currentLifePeakDepthMeters) : 0f;
            _currentLifeLowestOxygenNormalized = hasTelemetryV23 ? Mathf.Clamp01(dto.currentLifeLowestOxygenNormalized) : OxygenNormalized;
            _currentLifeLowestEnergyNormalized = hasTelemetryV23 ? Mathf.Clamp01(dto.currentLifeLowestEnergyNormalized) : EnergyNormalized;
            _currentLifeLowestIntegrityNormalized = hasTelemetryV23 ? Mathf.Clamp01(dto.currentLifeLowestIntegrityNormalized) : IntegrityNormalized;
            alive     = oxygen > 0f && integrity > 0f;
            _lastDeathCause = alive ? SurvivalDeathCause.None : ResolveDeathCause();
            _pendingIntegrityDeathCause = SurvivalDeathCause.None;
            _hasLastDeathRecord = hasTelemetryV23 && dto.hasLastDeathRecord;
            _lastDeathRecord = _hasLastDeathRecord
                ? new SurvivalDeathRecord(
                    (SurvivalDeathCause)dto.lastDeathCause,
                    dto.GetLastDeathPosition(),
                    Mathf.Max(0f, dto.lastDeathLifeDurationSeconds),
                    Mathf.Max(0f, dto.lastDeathPeakDepthMeters),
                    Mathf.Clamp01(dto.lastDeathLowestOxygenNormalized),
                    Mathf.Clamp01(dto.lastDeathLowestEnergyNormalized),
                    Mathf.Clamp01(dto.lastDeathLowestIntegrityNormalized))
                : default;
            ResetPressureExposureTracking();

            Vector3 pos = dto.GetPosition();
            if (!float.IsNaN(pos.x)) transform.SetPositionAndRotation(pos, dto.GetRotation());

            ForceAllDirty();
        }

        // ═════════════════════════════════════════════════════════
        //  INTERNAL UTILITY
        // ═════════════════════════════════════════════════════════

        private void ResetToMax()
        {
            oxygen    = ResolveRuntimeMaxOxygenCapacity();
            energy    = stats.MaxEnergy;
            integrity = stats.MaxIntegrity;
            hunger    = stats.MaxHunger;
            thirst    = stats.MaxThirst;
            depth     = 0f;
            pressure  = 1f;
            weight    = 0f;
            alive     = true;
            _lastDeathCause = SurvivalDeathCause.None;
            _pendingIntegrityDeathCause = SurvivalDeathCause.None;
            _currentLifeDurationSeconds = 0f;
            _currentLifePeakDepthMeters = 0f;
            _currentLifeLowestOxygenNormalized = 1f;
            _currentLifeLowestEnergyNormalized = 1f;
            _currentLifeLowestIntegrityNormalized = 1f;
            ResetPressureExposureTracking();

            _tempGraceTimer = 0f;
            _radGraceTimer  = 0f;

            ForceAllDirty();
        }

        private void ForceAllDirty()
        {
            lastPubOxygen    = DirtySentinel;
            lastPubEnergy    = DirtySentinel;
            lastPubDepth     = DirtySentinel;
            lastPubIntegrity = DirtySentinel;
            lastPubPressure  = DirtySentinel;
            lastPubTemp      = DirtySentinel;
            lastPubRad       = DirtySentinel;
            lastPubHunger    = DirtySentinel;
            lastPubThirst    = DirtySentinel;
        }

        private void MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause cause)
        {
            if (integrity <= 0f && cause != SurvivalDeathCause.None)
                _pendingIntegrityDeathCause = cause;
        }

        private void TrackCurrentLifeTelemetry(float deltaTime)
        {
            _currentLifeDurationSeconds += deltaTime;

            if (depth > _currentLifePeakDepthMeters)
                _currentLifePeakDepthMeters = depth;

            float oxygenNormalized = Mathf.Clamp01(OxygenNormalized);
            if (oxygenNormalized < _currentLifeLowestOxygenNormalized)
                _currentLifeLowestOxygenNormalized = oxygenNormalized;

            float energyNormalized = Mathf.Clamp01(EnergyNormalized);
            if (energyNormalized < _currentLifeLowestEnergyNormalized)
                _currentLifeLowestEnergyNormalized = energyNormalized;

            float integrityNormalized = Mathf.Clamp01(IntegrityNormalized);
            if (integrityNormalized < _currentLifeLowestIntegrityNormalized)
                _currentLifeLowestIntegrityNormalized = integrityNormalized;
        }

        private void TrackPressureExposure(float deltaTime)
        {
            float overpressureMeters = OverpressureMeters;
            if (overpressureMeters <= 0f)
            {
                TryRecordPressureExposureTelemetry();
                ResetPressureExposureTracking();
                return;
            }

            _currentPressureExposureSeconds += deltaTime;
            if (overpressureMeters > _currentPressurePeakExcessMeters)
                _currentPressurePeakExcessMeters = overpressureMeters;

            float damagePerSecond = ResolveCurrentPressureDamagePerSecond();
            if (damagePerSecond > _currentPressurePeakDamagePerSecond)
                _currentPressurePeakDamagePerSecond = damagePerSecond;
        }

        private SurvivalDeathCause ResolveDeathCause()
        {
            if (oxygen <= 0f)
                return SurvivalDeathCause.OxygenDepletion;

            if (integrity <= 0f)
            {
                if (_pendingIntegrityDeathCause != SurvivalDeathCause.None)
                    return _pendingIntegrityDeathCause;

                return SurvivalDeathCause.IntegrityFailure;
            }

            return SurvivalDeathCause.None;
        }

        private void CaptureDeathRecord()
        {
            _lastDeathRecord = new SurvivalDeathRecord(
                _lastDeathCause,
                transform.position,
                _currentLifeDurationSeconds,
                _currentLifePeakDepthMeters,
                _currentLifeLowestOxygenNormalized,
                _currentLifeLowestEnergyNormalized,
                _currentLifeLowestIntegrityNormalized);
            _hasLastDeathRecord = true;
        }

        private void TryRecordPressureExposureTelemetry()
        {
            if (_currentPressureExposureSeconds < PressureIncidentLogDurationThreshold &&
                _currentPressurePeakExcessMeters < PressureIncidentLogExcessThreshold)
            {
                return;
            }

            FieldOperationLogSystem.RecordOperation(
                "SUIT",
                "PRESSURE WINDOW BREACHED",
                BuildPressureExposureSummary(),
                "WARN");
        }

        private void RecordDeathTelemetry()
        {
            if (!_hasLastDeathRecord)
                return;

            string summary = string.Format(
                "Cause {0} // Life {1:0}s // Peak {2:0}m // O2 low {3:0}% // PWR low {4:0}% // Marker {5:0},{6:0},{7:0}. Advice: {8}",
                ResolveDeathCauseLabel(_lastDeathRecord.Cause),
                _lastDeathRecord.LifeDurationSeconds,
                _lastDeathRecord.PeakDepthMeters,
                _lastDeathRecord.LowestOxygenNormalized * 100f,
                _lastDeathRecord.LowestEnergyNormalized * 100f,
                _lastDeathRecord.Position.x,
                _lastDeathRecord.Position.y,
                _lastDeathRecord.Position.z,
                ResolveDeathAdvice(_lastDeathRecord.Cause));

            FieldOperationLogSystem.RecordOperation(
                "SUIT",
                "LAST LOSS MARKER UPDATED",
                summary,
                "CRITICAL");
        }

        private static string ResolveDeathAdvice(SurvivalDeathCause cause)
        {
            switch (cause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return "Break ascent and return routing at 25% oxygen. Do not wait for critical reserve.";
                case SurvivalDeathCause.PressureCollapse:
                    return "Respect safe-depth margin. Pull back before hull stress starts compounding.";
                case SurvivalDeathCause.ThermalFailure:
                    return "Do not hold in thermal pockets without power reserve or heat shielding.";
                case SurvivalDeathCause.RadiationExposure:
                    return "Cross irradiated lanes fast. Do not idle inside contaminated sectors.";
                case SurvivalDeathCause.Starvation:
                    return "Carry food before long extraction pushes. Integrity attrition is slower but terminal.";
                case SurvivalDeathCause.Dehydration:
                    return "Hydration is a hard timer. Refill before deep transit, not after.";
                case SurvivalDeathCause.IntegrityFailure:
                    return "Repair hull damage early. Stacked chip damage is what kills late in the run.";
                default:
                    return "Rebuild a shorter route and recover margin before the next deep push.";
            }
        }

        private float ResolveCurrentPressureDamagePerSecond()
        {
            if (stats == null)
                return 0f;

            float overpressureMeters = OverpressureMeters;
            if (overpressureMeters <= 0f)
                return 0f;

            float pressureDamageScale = ResolveTransportPressureDamageScale();
            if (pressureDamageScale <= 0f)
                return 0f;

            float scale = 1f + overpressureMeters * stats.PressureScalePerMeter;
            return stats.PressureDamageRate * scale * pressureDamageScale;
        }

        private float ResolvePressureExposureSeverity01()
        {
            if (stats == null)
                return 0f;

            float safeDepth = Mathf.Max(1f, stats.SafeDepth);
            float overpressureSeverity = Mathf.Clamp01(OverpressureMeters / Mathf.Max(8f, safeDepth * 0.3f));
            float damageSeverity = Mathf.Clamp01(ResolveCurrentPressureDamagePerSecond() / Mathf.Max(1f, stats.MaxIntegrity * 0.08f));
            return Mathf.Clamp01(overpressureSeverity * 0.65f + damageSeverity * 0.35f);
        }

        private string BuildPressureExposureSummary()
        {
            return string.Format(
                "Exceeded safe depth by {0:0}m for {1:0}s // Peak hull attrition {2:0.0}/s // Suit rating {3:0}m",
                _currentPressurePeakExcessMeters,
                _currentPressureExposureSeconds,
                _currentPressurePeakDamagePerSecond,
                stats != null ? stats.SafeDepth : 0f);
        }

        private void ResetPressureExposureTracking()
        {
            _currentPressureExposureSeconds = 0f;
            _currentPressurePeakExcessMeters = 0f;
            _currentPressurePeakDamagePerSecond = 0f;
        }

        private static string ResolveDeathCauseLabel(SurvivalDeathCause cause)
        {
            switch (cause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return "OXYGEN";
                case SurvivalDeathCause.PressureCollapse:
                    return "PRESSURE";
                case SurvivalDeathCause.ThermalFailure:
                    return "THERMAL";
                case SurvivalDeathCause.RadiationExposure:
                    return "RADIATION";
                case SurvivalDeathCause.Starvation:
                    return "STARVATION";
                case SurvivalDeathCause.Dehydration:
                    return "DEHYDRATION";
                case SurvivalDeathCause.IntegrityFailure:
                    return "INTEGRITY";
                default:
                    return "UNKNOWN";
            }
        }

        private float ResolveRuntimeMaxOxygenCapacity()
        {
            if (stats == null)
                return 0f;

            return Mathf.Max(1f, stats.MaxOxygen * _runtimeOxygenCapacityMultiplier);
        }

        private static void ForceDirty(ref float lastPub) => lastPub = DirtySentinel;
    }
}
