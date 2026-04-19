using System;
using Hecton8.Core;
using Hecton8.SaveSystem;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Atmosphere;

namespace Hecton8.Gameplay
{
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
        private const float HazardGraceDuration = 3f;

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

        public float OxygenNormalized    => oxygen    / stats.MaxOxygen;
        public float EnergyNormalized    => energy    / stats.MaxEnergy;
        public float IntegrityNormalized => integrity / stats.MaxIntegrity;
        public float HungerNormalized    => hunger    / stats.MaxHunger;
        public float ThirstNormalized    => thirst    / stats.MaxThirst;
        public float EnergyPercent       => EnergyNormalized * 100f;
        public float HungerPercent       => HungerNormalized * 100f;
        public float ThirstPercent       => ThirstNormalized * 100f;

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
                    stats.MaxOxygen,
                    oxygen + surfaceOxygenRefillRate * dt);
                return;
            }

            float pressureFactor = math.max(1f, pressure * 0.5f);
            float oxygenConsumptionScale = ResolveTransportOxygenConsumptionScale();
            if (oxygenConsumptionScale <= 0f)
                return;

            oxygen = math.max(0f, oxygen - stats.OxygenConsumptionRate * pressureFactor * oxygenConsumptionScale * dt);
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
            integrity = math.max(0f, integrity - stats.PressureDamageRate * scale * pressureDamageScale * dt);
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
            }

            // Apply dehydration damage if thirst is 0
            if (thirst <= 0f)
            {
                integrity = math.max(0f, integrity - stats.DehydrationDamageRate * dt);
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
            OnDeath?.Invoke();
            enabled = false;
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═════════════════════════════════════════════════════════

        public void RefillOxygen(float amount)
        {
            oxygen = math.min(stats.MaxOxygen, oxygen + math.max(0f, amount));
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

        public void TakeDamage(float amount)
        {
            if (!alive || amount <= 0f) return;
            integrity = math.max(0f, integrity - amount);
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
            dto.SetPosition(transform.position);
            dto.SetRotation(transform.rotation);
        }

        public void LoadFromSaveData(SaveData data)
        {
            PlayerStatsDTO dto = data.playerStats;
            oxygen    = Mathf.Clamp(dto.oxygen,    0f, stats.MaxOxygen);
            energy    = Mathf.Clamp(dto.energy,    0f, stats.MaxEnergy);
            integrity = Mathf.Clamp(dto.integrity, 0f, stats.MaxIntegrity);
            weight    = Mathf.Max(0f, dto.weight);
            hunger    = Mathf.Clamp(dto.hunger,    0f, stats.MaxHunger);
            thirst    = Mathf.Clamp(dto.thirst,    0f, stats.MaxThirst);
            alive     = oxygen > 0f && integrity > 0f;

            Vector3 pos = dto.GetPosition();
            if (!float.IsNaN(pos.x)) transform.SetPositionAndRotation(pos, dto.GetRotation());

            ForceAllDirty();
        }

        // ═════════════════════════════════════════════════════════
        //  INTERNAL UTILITY
        // ═════════════════════════════════════════════════════════

        private void ResetToMax()
        {
            oxygen    = stats.MaxOxygen;
            energy    = stats.MaxEnergy;
            integrity = stats.MaxIntegrity;
            hunger    = stats.MaxHunger;
            thirst    = stats.MaxThirst;
            depth     = 0f;
            pressure  = 1f;
            weight    = 0f;
            alive     = true;

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

        private static void ForceDirty(ref float lastPub) => lastPub = DirtySentinel;
    }
}
