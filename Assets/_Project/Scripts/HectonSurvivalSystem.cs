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

        // ═════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ═════════════════════════════════════════════════════════

        private float oxygen;
        private float energy;
        private float depth;
        private float integrity;
        private float pressure;
        private float weight;
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

        // Hazard Grace Periods
        private float _tempGraceTimer;
        private float _radGraceTimer;
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
        public bool  IsAlive             => alive;
        public SurvivalStats Stats       => stats;

        public float OxygenNormalized    => oxygen    / stats.MaxOxygen;
        public float EnergyNormalized    => energy    / stats.MaxEnergy;
        public float IntegrityNormalized => integrity / stats.MaxIntegrity;
        public float EnergyPercent       => EnergyNormalized * 100f;

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

            DrainOxygen(dt);
            DrainPassiveEnergy(dt);
            ApplyPressureDamage(dt);
            HandleTemperature(dt);
            HandleRadiation(dt);
        }

        // ═════════════════════════════════════════════════════════
        //  SIMULATION STEPS
        // ═════════════════════════════════════════════════════════

        private void ComputeDepthAndPressure()
        {
            depth    = math.max(0f, surfaceWorldY - transform.position.y);
            pressure = 1f + depth * 0.1f;
        }

        private void DrainOxygen(float dt)
        {
            float pressureFactor = math.max(1f, pressure * 0.5f);
            oxygen = math.max(0f, oxygen - stats.OxygenConsumptionRate * pressureFactor * dt);
        }

        private void DrainPassiveEnergy(float dt)
        {
            float weightFactor = 1f + weight * 0.005f;
            energy = math.max(0f, energy - stats.EnergyConsumptionRate * weightFactor * dt);
        }

        private void ApplyPressureDamage(float dt)
        {
            if (depth <= stats.SafeDepth) return;

            float excess = depth - stats.SafeDepth;
            float scale  = 1f + excess * stats.PressureScalePerMeter;
            integrity = math.max(0f, integrity - stats.PressureDamageRate * scale * dt);
        }

        private void HandleTemperature(float dt)
        {
            var atmosphere = HectonAtmosphereManager.Instance;
            if (atmosphere == null) return;
            
            float currentTemp = atmosphere.CurrentTemperature;

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

            _tempGraceTimer += dt;
            if (_tempGraceTimer < HazardGraceDuration) return;

            // Suit energy drain for thermal regulation
            float heatDrain = excess * stats.TempEnergyScale * dt;
            energy = math.max(0f, energy - heatDrain);

            // Thermal integrity damage
            float damage = stats.TempDamageRate * (1f + excess * 0.1f) * dt;
            integrity = math.max(0f, integrity - damage);
        }

        private void HandleRadiation(float dt)
        {
            var atmosphere = HectonAtmosphereManager.Instance;
            if (atmosphere == null) return;
            
            float currentRad = atmosphere.CurrentRadiation;

            if (currentRad <= stats.RadiationThreshold)
            {
                _radGraceTimer = 0f;
                return;
            }

            _radGraceTimer += dt;
            if (_radGraceTimer < HazardGraceDuration) return;

            float excess = currentRad - stats.RadiationThreshold;
            float damage = excess * stats.RadiationDamageRate * dt;
            
            integrity = math.max(0f, integrity - damage);
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
            if (atmosphere != null)
            {
                float currentTemp = atmosphere.CurrentTemperature;
                if (math.abs(currentTemp - lastPubTemp) > Epsilon)
                {
                    lastPubTemp = currentTemp;
                    OnTemperatureChanged?.Invoke(currentTemp);
                }

                float currentRad = atmosphere.CurrentRadiation;
                if (math.abs(currentRad - lastPubRad) > Epsilon)
                {
                    lastPubRad = currentRad;
                    OnRadiationChanged?.Invoke(currentRad);
                }
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
        }

        private static void ForceDirty(ref float lastPub) => lastPub = DirtySentinel;
    }
}
