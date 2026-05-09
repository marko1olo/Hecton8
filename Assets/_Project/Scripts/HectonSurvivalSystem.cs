using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
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

    [Flags]
    internal enum PlayerInjuryStatus : byte
    {
        None = 0,
        Bleeding = 1 << 0,
        Fracture = 1 << 1
    }

    internal enum ThermalStressMode : byte
    {
        None = 0,
        Cold = 1,
        Heat = 2
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
            double lifeDurationSeconds,
            double peakDepthMeters,
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
        public double LifeDurationSeconds { get; }

        /// <summary>Deepest reached depth for the recorded life.</summary>
        public double PeakDepthMeters { get; }

        /// <summary>Lowest normalized oxygen reached during the recorded life.</summary>
        public float LowestOxygenNormalized { get; }

        /// <summary>Lowest normalized energy reached during the recorded life.</summary>
        public float LowestEnergyNormalized { get; }

        /// <summary>Lowest normalized integrity reached during the recorded life.</summary>
        public float LowestIntegrityNormalized { get; }
    }

    /// <summary>
    /// Parsed item-parameter row injected from the survival database text source.
    /// </summary>
    public readonly struct SurvivalDatabaseItemParameters
    {
        public SurvivalDatabaseItemParameters(
            string stableId,
            uint stableHash,
            float massKilograms,
            float volumeLiters,
            float energyDensityMegajoulesPerKilogram,
            int baseDurability)
        {
            StableId = stableId;
            StableHash = stableHash;
            MassKilograms = massKilograms;
            VolumeLiters = volumeLiters;
            EnergyDensityMegajoulesPerKilogram = energyDensityMegajoulesPerKilogram;
            BaseDurability = baseDurability;
        }

        /// <summary>Stable content identifier keyed to ItemData.PersistentId.</summary>
        public string StableId { get; }

        /// <summary>Authored stable hash parsed from the injected database row.</summary>
        public uint StableHash { get; }

        /// <summary>Authored item mass in kilograms.</summary>
        public float MassKilograms { get; }

        /// <summary>Authored item volume in liters.</summary>
        public float VolumeLiters { get; }

        /// <summary>Authored stored-energy density in MJ/kg.</summary>
        public float EnergyDensityMegajoulesPerKilogram { get; }

        /// <summary>Authored base durability budget for economy and survival consumers.</summary>
        public int BaseDurability { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SurvivalDatabaseItemRecord
    {
        public uint StableHash;
        public float MassKilograms;
        public float VolumeLiters;
        public float EnergyDensityMegajoulesPerKilogram;
        public int BaseDurability;
    }

    internal struct SurvivalDatabaseColumnMap
    {
        public int StableId;
        public int Hash;
        public int MassKilograms;
        public int VolumeLiters;
        public int EnergyDensityMegajoulesPerKilogram;
        public int BaseDurability;

        public static SurvivalDatabaseColumnMap CreateInvalid()
        {
            SurvivalDatabaseColumnMap map = default;
            map.StableId = -1;
            map.Hash = -1;
            map.MassKilograms = -1;
            map.VolumeLiters = -1;
            map.EnergyDensityMegajoulesPerKilogram = -1;
            map.BaseDurability = -1;
            return map;
        }

        public bool HasAllRequiredColumns =>
            StableId >= 0 &&
            Hash >= 0 &&
            MassKilograms >= 0 &&
            VolumeLiters >= 0 &&
            EnergyDensityMegajoulesPerKilogram >= 0 &&
            BaseDurability >= 0;
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
    public sealed class HectonSurvivalSystem : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ISaveable
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

        [Header("── Thermal ─────────────────────────────────────")]
        [Tooltip("Base Newton-cooling time constant in seconds for internal suit temperature exchange with ambient water.")]
        [SerializeField, Range(1f, 600f)] private float internalTemperatureTimeConstantSeconds = 45f;

        [Header("── Survival Database Injection ─────────────────")]
        [Tooltip("Optional survival database source parsed at cold bootstrap to seed StableId mass, volume, energy density, and durability lookups.")]
        [SerializeField] private TextAsset survivalDatabaseSource;

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
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;

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
        private TraumaDispatcher _traumaDispatcher;
        private Rigidbody _playerRigidbody;
        private HectonPlayerHealth _playerHealth;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private bool _surfaceContractUnderwater;
        private float _runtimeOxygenCapacityMultiplier = 1f;
        private SurvivalDeathCause _lastDeathCause;
        private SurvivalDeathCause _pendingIntegrityDeathCause;
        private double _currentLifeDurationSeconds;
        private double _currentLifePeakDepthMeters;
        private float _currentLifeLowestOxygenNormalized = 1f;
        private float _currentLifeLowestEnergyNormalized = 1f;
        private float _currentLifeLowestIntegrityNormalized = 1f;
        private double _currentPressureExposureSeconds;
        private double _currentPressurePeakExcessMeters;
        private double _currentPressurePeakDamagePerSecond;
        private SurvivalDeathRecord _lastDeathRecord;
        private bool _hasLastDeathRecord;
        private PlayerInjuryStatus _injuryStatus;
        private float _bleedingSecondsRemaining;
        private float _bleedingDamagePerSecond;
        private float _bleedingSeverity01;
        private float _fractureSecondsRemaining;
        private float _fracturePenalty01;
        private float _environmentTemperature = 20f;
        private float _internalTemperature = 20f;
        private float _coldSeverity01;
        private float _heatSeverity01;
        private ThermalStressMode _thermalStressMode;
        private float _lastTrackedDepthMeters;
        private float _decompressionRisk01;
        private float _rapidAscentMetersPerSecond;
        private float _nitrogenBuildUp;
        private float _nitrogenNarcosis01;
        private float _airPocketNitrogenPauseTimer;
        private bool _nitrogenLoadWarningIssued;
        private float _nutritionalToxicitySecondsRemaining;
        private float _nutritionalToxicitySeverity01;
        private float _decompressionVomitToolDropCooldown;
        private int _bloodScentSpatialHandle;
        private int _bloodScentFaunaSpatialHandle;
        private NativeArray<uint> _survivalDatabaseStableHashes;
        private NativeArray<float> _survivalDatabaseMassKilograms;
        private NativeArray<float> _survivalDatabaseVolumeLiters;
        private NativeArray<float> _survivalDatabaseEnergyDensityMegajoulesPerKilogram;
        private NativeArray<int> _survivalDatabaseBaseDurability;
        private int _survivalDatabaseItemCount;
        private float _oxygenGraceTimer;
        private float _oxygenGraceVisionBlur01;
        private bool _oxygenGraceActive;
        private PlayerRuntimeContext _runtimeContext;
        private Unity.Mathematics.Random _traumaRandom;
        private FixedCharBuffer _telemetryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] — telemetry construction — owner: HectonSurvivalSystem
        private const float HazardGraceDuration = 3f;
        private const float SaveVelocityHardCapMetersPerSecond = 80f;
        private const float SaveVelocityHardCapSq = SaveVelocityHardCapMetersPerSecond * SaveVelocityHardCapMetersPerSecond;
        private const float PressureIncidentLogDurationThreshold = 4f;
        private const float PressureIncidentLogExcessThreshold = 6f;
        private const float ThermalSeverityReferenceRange = 35f;
        private const float ExtremeColdIntegrityThreshold = 24f;
        private const float ExtremeHeatIntegrityThreshold = 32f;
        private const float HeatThirstOverloadScale = 0.9f;
        private const float AbyssalColdDepthMeters = 3000f;
        private const float AbyssalColdFullDepthRangeMeters = 750f;
        private const float AbyssalColdPenaltyCelsius = 42f;
        private const float AbyssalColdHeatingMultiplier = 10f;
        private const float MajorPhysicalDamageThreshold = 16f;
        private const float SeverePhysicalDamageThreshold = 28f;
        private const float MajorTraumaSeverityThreshold = 0.42f;
        private const float BleedingBaseDurationSeconds = 48f;
        private const float BleedingMaxDurationSeconds = 135f;
        private const float BleedingBaseDamagePerSecond = 0.35f;
        private const float BleedingMaxDamagePerSecond = 1.65f;
        private const float FractureBaseDurationSeconds = 75f;
        private const float FractureMaxDurationSeconds = 210f;
        private const float FractureBasePenalty = 0.18f;
        private const float FractureMaxPenalty = 0.52f;
        private const float BleedingTrailPulseThreshold = 0.08f;
        private const float RapidAscentRiskStartDepth = 85f;
        private const float RapidAscentRiskMaxDepth = 260f;
        private const float RapidAscentRiskStartMetersPerSecond = 3.25f;
        private const float RapidAscentRiskMaxMetersPerSecond = 10.5f;
        private const float RapidAscentThermalBoostThreshold = 0.28f;
        private const float RapidAscentDamagePerSecond = 1.6f;
        private const float RapidAscentRiskDecayPerSecond = 0.38f;
        private const float RapidAscentDamageThreshold = 0.52f;
        private const float NitrogenAscentRiskDepthMeters = 400f;
        private const float NitrogenAscentRiskMetersPerSecond = 5f;
        private const float NitrogenCriticalBuildUp = 100f;
        private const float NitrogenBuildUpHardCap = 160f;
        private const float NitrogenBuildUpPerExcessMeterSecond = 12f;
        private const float NitrogenBuildUpDepthFullRangeMeters = 400f;
        private const float NitrogenRecoveryPerSecond = 2f;
        private const float NitrogenNarcosisFullRange = 50f;
        private const float NitrogenStaminaPenaltyMultiplier = 0.8f;
        private const float NitrogenAirPocketPauseSeconds = 2f;
        private const float NitrogenAirPocketRecoveryMultiplier = 2.5f;
        private const float NitrogenLoadWarningThreshold01 = 0.5f;
        private const float NitrogenLoadWarningResetThreshold01 = 0.35f;
        private const float NitrogenRingingThreshold01 = 0.75f;
        private const string NitrogenLoadWarningMessage = "ASCENT RATE WARNING // NITROGEN LOAD";
        private const float DecompressionVomitThreshold = 150f;
        private const float DecompressionVomitToolDropCooldownSeconds = 5f;
        private const float DecompressionVomitConvulsionDurationSeconds = 0.65f;
        private const float ThermalShockBoilingThresholdCelsius = 90f;
        private const float ThermalShockFreezingThresholdCelsius = -2f;
        private const float ThermalShockFullSeverityRangeCelsius = 40f;
        private const float ThermalFlowHeatToCelsiusScale = 18f;
        private const float ThermalShieldDamageMultiplier = 0.35f;
        private const float NutritionalToxicityDefaultSeverity01 = 0.65f;
        private const float NutritionalToxicityDefaultDurationSeconds = 45f;
        private const float NutritionalToxicityDamageScale = 0.45f;
        private const float SuitPunctureBleedDamageFraction = 0.30f;
        private const float HypothermiaFrostStartCelsius = 35f;
        private const float HypothermiaFrostFullCelsius = 28f;
        private const float DefaultInternalTemperatureCelsius = 20f;
        private const float OxygenMovementScaleCeiling = 1.55f;
        private const float OxygenStressScaleCeilingBonus = 0.50f;
        private const float OxygenLeakScaleCeilingBonus = 0.70f;
        private const float OxygenCarryMassGraceKg = 18f;
        private const float OxygenCarryMassScaleCeilingBonus = 0.22f;
        private const float OxygenGraceDurationSeconds = 2f;
        private const float OxygenGraceSpeedMultiplier = 1.2f;
        private const float OverpressureSeverityFullRangeMeters = 150f;
        private const float OverpressureSeveritySafeDepthScale = 0.35f;
        private const int SurvivalDatabaseRowCapacity = 256;
        private const int SurvivalDatabaseColumnCapacity = 16;
        private const string NativeMemoryOwner = nameof(HectonSurvivalSystem);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly int _MembraneTissueHashId = LocHash.Compute("Data_MembraneTissue");
        private static readonly uint _ThermalShockNonFiniteWarningHash = unchecked((uint)LocHash.Compute("Survival.ThermalShock.NonFinite"));
        private static readonly uint _AirPocketInvalidRefillWarningHash = unchecked((uint)LocHash.Compute("Survival.AirPocket.InvalidRefill"));
        private static readonly uint _SurvivalRuntimeContextHash = unchecked((uint)LocHash.Compute(nameof(HectonSurvivalSystem)));
        private static readonly uint _NitrogenLoadWarningMessageHash = unchecked((uint)LocHash.Compute(NitrogenLoadWarningMessage));

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
        internal event Action InjuryStateChanged;
        internal event Action ThermalStateChanged;
        internal event Action<float, Vector3> BleedingTrailPulse;

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
        public double CurrentLifeDurationSeconds => _currentLifeDurationSeconds;
        /// <summary>Deepest reached depth for the currently active life.</summary>
        public double CurrentLifePeakDepthMeters => _currentLifePeakDepthMeters;
        /// <summary>True when a persisted last-loss marker record is available.</summary>
        public bool HasLastDeathRecord => _hasLastDeathRecord;
        /// <summary>World-space marker position for the latest recorded death.</summary>
        public Vector3 LastDeathMarkerPosition => _lastDeathRecord.Position;
        /// <summary>Latest persisted death telemetry record.</summary>
        public SurvivalDeathRecord LastDeathRecord => _lastDeathRecord;
        /// <summary>Signed margin to the authored safe depth. Negative values mean active overpressure.</summary>
        public float SafeDepthMarginMeters => stats != null ? ResolveEffectiveSafeDepthMeters() - depth : 0f;
        /// <summary>Positive metres beyond the safe depth envelope.</summary>
        public float OverpressureMeters => stats != null ? math.max(0f, depth - ResolveEffectiveSafeDepthMeters()) : 0f;
        /// <summary>True when the suit is already deeper than its safe depth rating.</summary>
        public bool IsBeyondSafeDepth => OverpressureMeters > 0f;
        /// <summary>Current integrity attrition per second caused by overpressure.</summary>
        public float PressureDamagePerSecond => ResolveCurrentPressureDamagePerSecond();
        /// <summary>Normalized live overpressure severity for advisory systems.</summary>
        public float PressureExposureSeverity01 => ResolvePressureExposureSeverity01();
        /// <summary>True while the player is actively bleeding.</summary>
        public bool IsBleeding => (_injuryStatus & PlayerInjuryStatus.Bleeding) != 0;
        /// <summary>True while the player is carrying a fracture movement penalty.</summary>
        public bool HasFracture => (_injuryStatus & PlayerInjuryStatus.Fracture) != 0;
        /// <summary>Combined live injury flags for UI and progression systems.</summary>
        internal PlayerInjuryStatus CurrentInjuries => _injuryStatus;
        /// <summary>Normalized severity of the active bleeding state.</summary>
        public float BleedingSeverity01 => _bleedingSeverity01;
        /// <summary>Normalized fracture penalty currently applied to swim mobility.</summary>
        public float FracturePenalty01 => _fracturePenalty01;
        /// <summary>Resolved environment temperature after local thermal hazards are added.</summary>
        public float EnvironmentTemperature => _environmentTemperature;
        /// <summary>Current internal suit temperature after exponential thermal convergence.</summary>
        public float InternalTemperature => _internalTemperature;
        /// <summary>True while cold stress is actively affecting the suit and body.</summary>
        public bool IsInColdStress => _thermalStressMode == ThermalStressMode.Cold;
        /// <summary>True while heat stress is actively affecting the suit and body.</summary>
        public bool IsInHeatStress => _thermalStressMode == ThermalStressMode.Heat;
        /// <summary>Resolved thermal stress mode currently applied to the player.</summary>
        internal ThermalStressMode CurrentThermalStressMode => _thermalStressMode;
        /// <summary>Normalized cold-stress severity for advisory systems.</summary>
        public float ColdStressSeverity01 => _coldSeverity01;
        /// <summary>Normalized heat-stress severity for advisory systems.</summary>
        public float HeatStressSeverity01 => _heatSeverity01;
        /// <summary>Highest normalized thermal-stress severity currently active.</summary>
        public float ThermalStressSeverity01 => Mathf.Max(_coldSeverity01, _heatSeverity01);
        /// <summary>Normalized decompression-risk state generated by rapid ascent and thermal updrafts.</summary>
        internal float RapidAscentRisk01 => _decompressionRisk01;
        /// <summary>Cumulative nitrogen build-up generated by fast ascent from deep water.</summary>
        public float NitrogenBuildUp => _nitrogenBuildUp;
        /// <summary>Normalized nitrogen build-up against the narcosis activation threshold.</summary>
        public float NitrogenBuildUp01 => Mathf.Clamp01(_nitrogenBuildUp / NitrogenCriticalBuildUp);
        /// <summary>Pre-narcosis high-frequency ring intensity used by the helmet DSP layer.</summary>
        public float NitrogenWarningRinging01 => ResolveNitrogenWarningRinging01(_nitrogenBuildUp);
        /// <summary>True when cumulative nitrogen build-up has crossed the sickness threshold.</summary>
        public bool IsNitrogenNarcosisActive => _nitrogenBuildUp > NitrogenCriticalBuildUp;
        /// <summary>Normalized narcosis severity used by visor and movement penalty systems.</summary>
        public float NitrogenNarcosis01 => _nitrogenNarcosis01;
        /// <summary>Normalized blur signal emitted by nitrogen sickness.</summary>
        public float NitrogenNarcosisVisionBlur01 => _nitrogenNarcosis01;
        /// <summary>True while the oxygen-depletion grace pulse is suppressing immediate death.</summary>
        public bool IsOxygenGraceActive => _oxygenGraceActive;
        /// <summary>Normalized vision-blur pulse emitted during the oxygen grace window.</summary>
        public float OxygenGraceVisionBlur01 => _oxygenGraceVisionBlur01;

        // ═════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════

        private void Awake()
        {
            if (stats == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonSurvival] SurvivalStats not assigned. Disabling.", this);
#endif
                enabled = false;
                return;
            }

            ResolveRuntimeContextDependencies();
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
            int ownerId = unchecked((int)EntityId.ToULong(GetEntityId()));
            int statsId = stats != null ? unchecked((int)EntityId.ToULong(stats.GetEntityId())) : 0;
            _traumaRandom = CreateDeterministicRandom(ownerId, statsId);
            TryBootstrapInjectedSurvivalDatabase();
            NotificationEvents.RegisterMessage(NitrogenLoadWarningMessage);
            ResetToMax();
            PublishRuntimeContextState();
        }

        private void OnEnable()
        {
            ResolveRuntimeContextDependencies();
            TryRegisterTickOwners();
            _slowTickDt = 0.5f;

            RegisterBloodScentSignal();
            GlobalRegistry.Save?.Register(this);
        }

        private void OnDisable()
        {
            TryUnregisterTickOwners();
            UnregisterBloodScentSignal();
            GlobalRegistry.Save?.Unregister(this);
            ResetOxygenGraceState();
            ResetThermalState();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                TryUnregisterTickOwners();
                GlobalRegistry.Save?.Unregister(this);
            }

            DisposeInjectedSurvivalDatabase();
        }

        private void ResolveRuntimeContextDependencies()
        {
            if (!PlayerRuntimeContextService.TryBindPlayerRoot(gameObject, out PlayerRuntimeContext runtimeContext))
                return;

            _runtimeContext = runtimeContext;
            _playerMovement = runtimeContext.PlayerMovement;
            _playerTransportCoordinator = runtimeContext.PlayerTransportCoordinator;
            _traumaDispatcher = runtimeContext.TraumaDispatcher;
            _playerRigidbody = runtimeContext.PlayerRigidbody;
            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);
        }

        private void TryRegisterTickOwners()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdatable)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
                _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredSlowTickable)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTickable = GlobalRegistry.SlowTickables.Contains(this);
            }
        }

        private void TryUnregisterTickOwners()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredUpdatable = false;
            }

            if (_registeredSlowTickable)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTickable = false;
            }
        }

        private void PublishRuntimeContextState()
        {
            if (_runtimeContext == null)
                return;

            uint flags = (uint)PlayerRuntimeSnapshotFlags.HasSurvival;
            if (_runtimeContext.IsBound)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot;
            if (_runtimeContext.PlayerMovement != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasMovement;
            if (_runtimeContext.PlayerRigidbody != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasRigidbody;
            if (_runtimeContext.ToolManager != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasToolManager;
            if (_runtimeContext.Inventory != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasInventory;
            if (_runtimeContext.PlayerTransportCoordinator != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTransport;
            if (_runtimeContext.TraumaDispatcher != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTrauma;
            if (alive)
                flags |= (uint)PlayerRuntimeSnapshotFlags.PlayerAlive;
            if (_oxygenGraceActive)
                flags |= (uint)PlayerRuntimeSnapshotFlags.OxygenGraceActive;
            if (_surfaceContractUnderwater)
                flags |= (uint)PlayerRuntimeSnapshotFlags.Underwater;

            PlayerSurvivalRuntimeState survivalState = default;
            survivalState.OxygenNormalized = math.saturate(OxygenNormalized);
            survivalState.EnergyNormalized = math.saturate(EnergyNormalized);
            survivalState.IntegrityNormalized = math.saturate(IntegrityNormalized);
            survivalState.PressureExposureSeverity01 = math.saturate(PressureExposureSeverity01);
            survivalState.ThermalStressSeverity01 = math.saturate(ThermalStressSeverity01);
            survivalState.HungerNormalized = math.saturate(HungerNormalized);
            survivalState.ThirstNormalized = math.saturate(ThirstNormalized);
            survivalState.OxygenGraceVisionBlur01 = math.saturate(_oxygenGraceVisionBlur01);
            survivalState.ColdStressSeverity01 = math.saturate(_coldSeverity01);
            survivalState.HeatStressSeverity01 = math.saturate(_heatSeverity01);
            survivalState.RapidAscentRisk01 = math.saturate(_decompressionRisk01);
            survivalState.NitrogenBuildUp01 = math.saturate(_nitrogenBuildUp / NitrogenCriticalBuildUp);
            survivalState.NitrogenNarcosis01 = math.saturate(_nitrogenNarcosis01);
            survivalState.Flags = flags;
            _runtimeContext.PublishSurvivalState(in survivalState);
        }

        private void PublishHeadlessUIState()
        {
            float maxOxygen = stats != null ? math.max(0.01f, ResolveRuntimeMaxOxygenCapacity()) : 100f;
            float maxEnergy = stats != null ? math.max(0.01f, stats.MaxEnergy) : 100f;
            float maxIntegrity = stats != null ? math.max(0.01f, stats.MaxIntegrity) : 100f;
            float carryCapacityKg = stats != null ? math.max(0.01f, stats.CarryCapacityKg) : 200f;

            UIStateStore.WriteHUDSurvivalState(
                math.saturate(oxygen / maxOxygen),
                math.saturate(energy / maxEnergy),
                math.saturate(integrity / maxIntegrity),
                math.max(0f, depth),
                math.max(1f, pressure),
                ResolveEffectiveSafeDepthMeters(),
                math.max(0f, oxygen),
                math.max(0f, energy),
                math.max(0f, integrity),
                math.max(0f, weight),
                carryCapacityKg,
                math.saturate(weight / carryCapacityKg),
                Time.unscaledTime);
            UIStateStore.WriteFrostIntensity(
                ResolveHypothermiaFrostIntensity01(_internalTemperature),
                Time.unscaledTime);
        }

        // ═════════════════════════════════════════════════════════
        //  TICK SYSTEMS
        // ═════════════════════════════════════════════════════════

        internal static float ResolveHypothermiaFrostIntensity01(float internalTemperatureCelsius)
        {
            return SomaticSurvivalMath.ResolveHypothermiaFrostIntensity01(internalTemperatureCelsius);
        }

        public void Tick(float deltaTime)
        {
            if (!alive) return;

            ComputeDepthAndPressure();
            TryApplyLocalizedOxygenPocket();
            TrackRapidAscentRisk(deltaTime);
            RefreshBloodScentSignal();
            TrackCurrentLifeTelemetry(deltaTime);
            TrackPressureExposure(deltaTime);
            PushPressureHullStress();
            UpdateOxygenGraceState(deltaTime);
            PublishRuntimeContextState();
            PublishHeadlessUIState();
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
            ApplyRapidAscentDamage(dt);
            HandleTemperature(dt);
            HandleRadiation(dt);
            HandleToxicity(dt);
            HandleNutritionalToxicity(dt);
            HandleDecompressionSicknessVomit(dt);
            UpdateHungerAndThirst(dt);
            HandleInjuries(dt);
            HandleCriticalStaminaFailure();
            ApplyInjuryMovementPenalty();
            PublishRuntimeContextState();
            PublishHeadlessUIState();
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

            float oxygenDrainPerSecond = ResolveCurrentOxygenDrainPerSecond();
            oxygen = math.max(0f, oxygen - oxygenDrainPerSecond * dt);
        }

        private bool ResolveSurfaceContractUnderwater()
        {
            if (_playerMovement != null)
            {
                switch (_playerMovement.CurrentLocomotionMode)
                {
                    case PlayerLocomotionMode.UnderwaterSwim:
                        return true;

                    case PlayerLocomotionMode.ExosuitLocomotion:
                        return _playerMovement.CurrentDepth > 0.01f || _playerMovement.IsPlayerSubmerged;

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
            float pressureDamagePerSecond = ResolveCurrentPressureDamagePerSecond();
            if (pressureDamagePerSecond <= 0f)
                return;

            integrity = math.max(0f, integrity - pressureDamagePerSecond * dt);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.PressureCollapse);
        }

        private void HandleTemperature(float dt)
        {
            var atmosphere = Hecton8.Core.GlobalRegistry.Atmosphere;
            float baseTemp = atmosphere != null ? atmosphere.CurrentTemperature : 20f;
            float localHeat = HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Heat);
            float abyssalColdPenalty = ResolveAbyssalColdPenaltyCelsius();
            float hazardTemperature = baseTemp + localHeat - abyssalColdPenalty;
            _environmentTemperature = ResolveExternalThermalShockTemperature(
                hazardTemperature,
                ResolveAbyssalThermalExternalTemperature(transform.position));
            float floodedThermalInsulationFactor = ResolveFloodedThermalInsulationFactor();
            float floodedExternalTemperature = ResolveFloodedExternalTemperature(_environmentTemperature);
            float thermalExposureScale = ResolveTransportThermalExposureScale();
            float tauEff = math.max(0.01f, internalTemperatureTimeConstantSeconds * floodedThermalInsulationFactor / thermalExposureScale);
            _internalTemperature = ResolveExponentialTemperatureStep(
                floodedExternalTemperature,
                _internalTemperature,
                dt,
                tauEff);
            ApplyThermalShockDamage(_environmentTemperature, dt);

            float coldExcess = 0f;
            float heatExcess = 0f;
            if (_internalTemperature < stats.MinSafeTemp)
                coldExcess = stats.MinSafeTemp - _internalTemperature;
            else if (_internalTemperature > stats.MaxSafeTemp)
                heatExcess = _internalTemperature - stats.MaxSafeTemp;

            if (coldExcess <= 0f && heatExcess <= 0f)
            {
                _tempGraceTimer = 0f;
                _thermalStressMode = ThermalStressMode.None;
                _coldSeverity01 = 0f;
                _heatSeverity01 = 0f;
                ThermalStateChanged?.Invoke();
                return;
            }

            float deepColdStressMultiplier = ResolveDeepColdPocketStressMultiplier();
            _thermalStressMode = coldExcess > 0f ? ThermalStressMode.Cold : ThermalStressMode.Heat;
            _coldSeverity01 = ResolveThermalSeverity01(coldExcess);
            _heatSeverity01 = ResolveThermalSeverity01(heatExcess);
            _tempGraceTimer += dt;
            ThermalStateChanged?.Invoke();
            if (_tempGraceTimer < HazardGraceDuration)
                return;

            float thermalPowerDrawPerSecond = ResolveThermalPowerDrawPerSecond(
                coldExcess,
                heatExcess,
                deepColdStressMultiplier);
            if (thermalPowerDrawPerSecond > 0f)
                energy = math.max(0f, energy - thermalPowerDrawPerSecond * dt);

            if (coldExcess > 0f)
            {
                if (energy <= 0.01f || coldExcess >= ExtremeColdIntegrityThreshold)
                {
                    float coldDamage = stats.TempDamageRate * (1f + coldExcess * 0.1f) * deepColdStressMultiplier * dt;
                    integrity = math.max(0f, integrity - coldDamage);
                    MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.ThermalFailure);
                }

                return;
            }

            float hydrationDrain = heatExcess * stats.TempEnergyScale * HeatThirstOverloadScale * dt;
            thirst = math.max(0f, thirst - hydrationDrain);

            if (thirst <= 0f || heatExcess >= ExtremeHeatIntegrityThreshold)
            {
                float heatDamage = stats.TempDamageRate * (1f + heatExcess * 0.1f) * dt;
                integrity = math.max(0f, integrity - heatDamage);
                MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.ThermalFailure);
            }
        }

        private float ResolveAbyssalThermalExternalTemperature(Vector3 worldPosition)
        {
            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            if (thermalManager == null ||
                !thermalManager.SampleThermalFlow(worldPosition, 1.1f, out AbyssalThermalManager.ThermalFlowSample sample) ||
                sample.Heat01 <= 0f)
            {
                return float.NegativeInfinity;
            }

            return sample.Heat01 * ThermalFlowHeatToCelsiusScale;
        }

        internal static float ResolveExternalThermalShockTemperature(float fallbackTemperatureCelsius, float sampledThermalTemperatureCelsius)
        {
            return SomaticSurvivalMath.ResolveExternalThermalShockTemperature(
                fallbackTemperatureCelsius,
                sampledThermalTemperatureCelsius);
        }

        internal static float ResolveThermalShockSeverity01(float externalTemperatureCelsius)
        {
            return SomaticSurvivalMath.ResolveThermalShockSeverity01(externalTemperatureCelsius);
        }

        internal static float ResolveThermalShockDamagePerSecond(
            float externalTemperatureCelsius,
            float baseTemperatureDamageRate,
            float damageMultiplier)
        {
            return SomaticSurvivalMath.ResolveThermalShockDamagePerSecond(
                externalTemperatureCelsius,
                baseTemperatureDamageRate,
                damageMultiplier);
        }

        private void ApplyThermalShockDamage(float externalTemperatureCelsius, float dt)
        {
            float damagePerSecond = ResolveThermalShockDamagePerSecond(
                externalTemperatureCelsius,
                stats != null ? stats.TempDamageRate : 0f,
                ResolveThermalShockDamageMultiplier());
            if (!math.isfinite(damagePerSecond))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ThermalShockNonFiniteWarningHash,
                    _SurvivalRuntimeContextHash,
                    externalTemperatureCelsius);
                return;
            }

            if (damagePerSecond <= 0f)
                return;

            integrity = math.max(0f, integrity - damagePerSecond * math.max(0f, dt));
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.ThermalFailure);
        }

        private float ResolveThermalShockDamageMultiplier()
        {
            float multiplier = ResolveTransportThermalExposureScale();
            if (_runtimeContext == null ||
                _runtimeContext.ToolManager == null ||
                _runtimeContext.ToolManager.CurrentTool == null)
            {
                return multiplier;
            }

            PlayerTool currentTool = _runtimeContext.ToolManager.CurrentTool;
            IModularEquipmentService equipmentService = GlobalRegistry.ModularEquipment;
            if (equipmentService == null ||
                currentTool.RuntimeToolId == 0u ||
                !equipmentService.HasUpgrade(currentTool.RuntimeToolId, ToolUpgradeBits.ThermalShield))
            {
                return multiplier;
            }

            return multiplier * ThermalShieldDamageMultiplier;
        }

        private float ResolveAbyssalColdPenaltyCelsius()
        {
            if (_playerMovement == null)
                return 0f;

            if (_playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion)
                return 0f;

            if (!_surfaceContractUnderwater || depth <= AbyssalColdDepthMeters)
                return 0f;

            float depthT = math.saturate((depth - AbyssalColdDepthMeters) / math.max(AbyssalColdFullDepthRangeMeters, 0.01f));
            return AbyssalColdPenaltyCelsius * depthT;
        }

        private float ResolveAbyssalHeatingDrainMultiplier()
        {
            if (_playerMovement == null)
                return 1f;

            if (_playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion)
                return 1f;

            if (!_surfaceContractUnderwater || depth <= AbyssalColdDepthMeters)
                return 1f;

            float depthT = math.saturate((depth - AbyssalColdDepthMeters) / math.max(AbyssalColdFullDepthRangeMeters, 0.01f));
            return math.lerp(1f, AbyssalColdHeatingMultiplier, depthT);
        }

        private float ResolveDeepColdPocketStressMultiplier()
        {
            if (_playerMovement != null && _playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion)
                return 1f;

            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge) || _vegetationBridge == null)
                return 1f;

            return math.max(1f, _vegetationBridge.GetDeepColdStressMultiplier(transform.position));
        }

        private void TrackRapidAscentRisk(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float trackedDepthBeforeUpdate = _lastTrackedDepthMeters;
            float ascentMeters = math.max(0f, trackedDepthBeforeUpdate - depth);
            _rapidAscentMetersPerSecond = TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime)
                ? math.select(0f, ascentMeters * inverseDeltaTime, math.isfinite(ascentMeters * inverseDeltaTime))
                : 0f;
            _lastTrackedDepthMeters = depth;

            float ascentOriginDepth = math.max(depth, trackedDepthBeforeUpdate);
            TrackNitrogenBuildUp(deltaTime, ascentOriginDepth, _rapidAscentMetersPerSecond);

            if (_playerMovement == null)
            {
                _decompressionRisk01 = math.max(0f, _decompressionRisk01 - RapidAscentRiskDecayPerSecond * deltaTime);
                return;
            }

            float depthRisk01 = math.saturate(
                (ascentOriginDepth - RapidAscentRiskStartDepth) /
                math.max(0.01f, RapidAscentRiskMaxDepth - RapidAscentRiskStartDepth));
            float ascentRisk01 = math.saturate(
                (_rapidAscentMetersPerSecond - RapidAscentRiskStartMetersPerSecond) /
                math.max(0.01f, RapidAscentRiskMaxMetersPerSecond - RapidAscentRiskStartMetersPerSecond));
            float thermalBoost01 = math.saturate(
                (_playerMovement.CurrentThermalUpdraftIntensity01 - RapidAscentThermalBoostThreshold) /
                math.max(0.01f, 1f - RapidAscentThermalBoostThreshold));

            float targetRisk = math.saturate(depthRisk01 * (ascentRisk01 * 0.72f + thermalBoost01 * 0.28f));
            if (targetRisk > _decompressionRisk01)
            {
                _decompressionRisk01 = targetRisk;
                return;
            }

            _decompressionRisk01 = math.max(0f, _decompressionRisk01 - RapidAscentRiskDecayPerSecond * deltaTime);
        }

        private void TrackNitrogenBuildUp(float deltaTime, float ascentOriginDepthMeters, float ascentMetersPerSecond)
        {
            if (_airPocketNitrogenPauseTimer > 0f)
            {
                _airPocketNitrogenPauseTimer = math.max(0f, _airPocketNitrogenPauseTimer - math.max(0f, deltaTime));
                float recovery = NitrogenRecoveryPerSecond * NitrogenAirPocketRecoveryMultiplier * math.max(0f, deltaTime);
                _nitrogenBuildUp = math.max(0f, _nitrogenBuildUp - recovery);
                _nitrogenNarcosis01 = ResolveNitrogenNarcosis01(_nitrogenBuildUp);
                UpdateNitrogenPreNarcosisWarningState();
                ApplyNitrogenMovementPenalty();
                return;
            }

            float buildUpDelta = ResolveNitrogenBuildUpDelta(
                ascentMetersPerSecond,
                ascentOriginDepthMeters,
                deltaTime);

            if (buildUpDelta > 0f)
            {
                _nitrogenBuildUp = math.min(NitrogenBuildUpHardCap, _nitrogenBuildUp + buildUpDelta);
            }
            else
            {
                _nitrogenBuildUp = math.max(0f, _nitrogenBuildUp - NitrogenRecoveryPerSecond * math.max(0f, deltaTime));
            }

            _nitrogenNarcosis01 = ResolveNitrogenNarcosis01(_nitrogenBuildUp);
            UpdateNitrogenPreNarcosisWarningState();
            ApplyNitrogenMovementPenalty();
        }

        internal static float ResolveNitrogenBuildUpDelta(float ascentMetersPerSecond, float ascentOriginDepthMeters, float deltaTime)
        {
            return SomaticSurvivalMath.ResolveNitrogenBuildUpDelta(
                ascentMetersPerSecond,
                ascentOriginDepthMeters,
                deltaTime);
        }

        internal static float ResolveNitrogenNarcosis01(float nitrogenBuildUp)
        {
            return SomaticSurvivalMath.ResolveNitrogenNarcosis01(nitrogenBuildUp);
        }

        internal static float ResolveNitrogenWarningRinging01(float nitrogenBuildUp)
        {
            float buildUp01 = math.saturate(nitrogenBuildUp / NitrogenCriticalBuildUp);
            return math.saturate((buildUp01 - NitrogenRingingThreshold01) / math.max(1f - NitrogenRingingThreshold01, 0.0001f));
        }

        internal static float ResolveNitrogenStaminaMultiplier(float nitrogenBuildUp)
        {
            return SomaticSurvivalMath.ResolveNitrogenStaminaMultiplier(nitrogenBuildUp);
        }

        internal static float ResolveDecompressionVomitSeverity01(float nitrogenBuildUp)
        {
            return SomaticSurvivalMath.ResolveDecompressionVomitSeverity01(nitrogenBuildUp);
        }

        private void ApplyNitrogenMovementPenalty()
        {
            if (_playerMovement == null)
                return;

            _playerMovement.SetRuntimeStaminaMultiplier(ResolveNitrogenStaminaMultiplier(_nitrogenBuildUp));
        }

        private void TryApplyLocalizedOxygenPocket()
        {
            if (!TrySamplePlayerAupAirPocket(out float oxygenRefillFraction))
                return;

            if (!math.isfinite(oxygenRefillFraction) || oxygenRefillFraction <= 0f)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _AirPocketInvalidRefillWarningHash,
                    _SurvivalRuntimeContextHash,
                    oxygenRefillFraction);
                return;
            }

            oxygen = math.max(
                oxygen,
                ResolveRuntimeMaxOxygenCapacity() * math.max(0.01f, oxygenRefillFraction));
            _airPocketNitrogenPauseTimer = math.max(_airPocketNitrogenPauseTimer, NitrogenAirPocketPauseSeconds);
            _nitrogenBuildUp = math.max(
                0f,
                _nitrogenBuildUp - NitrogenRecoveryPerSecond * NitrogenAirPocketRecoveryMultiplier * _slowTickDt);
            _nitrogenNarcosis01 = ResolveNitrogenNarcosis01(_nitrogenBuildUp);
            UpdateNitrogenPreNarcosisWarningState();
            ApplyNitrogenMovementPenalty();
            ForceDirty(ref lastPubOxygen);
        }

        private void UpdateNitrogenPreNarcosisWarningState()
        {
            float buildUp01 = NitrogenBuildUp01;
            if (buildUp01 < NitrogenLoadWarningResetThreshold01)
            {
                _nitrogenLoadWarningIssued = false;
                return;
            }

            if (_nitrogenLoadWarningIssued || buildUp01 < NitrogenLoadWarningThreshold01)
                return;

            _nitrogenLoadWarningIssued = true;
            NotificationEvents.PushRegisteredWarning(_NitrogenLoadWarningMessageHash);
        }

        private bool TrySamplePlayerAupAirPocket(out float oxygenRefillFraction)
        {
            Vector3 center = transform.position;
            if (HectonVoxelEngine.TrySampleAirPocket(center, out oxygenRefillFraction))
                return true;

            Vector3 up = transform.up;
            float halfHeight = 0.9f;
            if (HectonVoxelEngine.TrySampleAirPocket(center + up * halfHeight, out oxygenRefillFraction))
                return true;

            return HectonVoxelEngine.TrySampleAirPocket(center - up * halfHeight, out oxygenRefillFraction);
        }

        private void HandleDecompressionSicknessVomit(float dt)
        {
            if (_decompressionVomitToolDropCooldown > 0f)
                _decompressionVomitToolDropCooldown = math.max(0f, _decompressionVomitToolDropCooldown - math.max(0f, dt));

            float severity01 = ResolveDecompressionVomitSeverity01(_nitrogenBuildUp);
            if (severity01 <= 0f)
                return;

            if (_playerMovement != null)
                _playerMovement.ApplyRuntimeNarcosisConvulsion(severity01, DecompressionVomitConvulsionDurationSeconds);

            if (_decompressionVomitToolDropCooldown > 0f ||
                _runtimeContext == null ||
                _runtimeContext.ToolManager == null)
            {
                return;
            }

            if (_traumaRandom.NextFloat() > math.lerp(0.35f, 0.85f, severity01))
                return;

            Vector3 impulse = (transform.forward * math.lerp(1.2f, 2.8f, severity01)) + (Vector3.up * 0.35f);
            if (_runtimeContext.ToolManager.TryForceDropCurrentToolFromHands(impulse))
                _decompressionVomitToolDropCooldown = DecompressionVomitToolDropCooldownSeconds;
        }

        private void HandleCriticalStaminaFailure()
        {
            if (_playerMovement == null || stats == null)
                return;

            float encumbranceRatio = weight / math.max(0.01f, stats.CarryCapacityKg);
            if (HectonPlayerMovement.ShouldTriggerCriticalStaminaFailure(encumbranceRatio, EnergyNormalized))
                _playerMovement.TriggerCriticalStaminaFailure();
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private void ApplyRapidAscentDamage(float dt)
        {
            if (_decompressionRisk01 < RapidAscentDamageThreshold)
                return;

            float severity = math.saturate(
                (_decompressionRisk01 - RapidAscentDamageThreshold) /
                math.max(0.01f, 1f - RapidAscentDamageThreshold));
            float damage = RapidAscentDamagePerSecond * severity * dt;
            integrity = math.max(0f, integrity - damage);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.PressureCollapse);
        }

        private void HandleRadiation(float dt)
        {
            var atmosphere = Hecton8.Core.GlobalRegistry.Atmosphere;
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

        private void HandleToxicity(float dt)
        {
            if (Hecton8.Core.GlobalRegistry.HazardZones != null)
                return;

            float toxicity = HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Toxicity);
            if (toxicity <= 0.001f)
                return;

            float toxicityExposureScale = ResolveTransportRadiationExposureScale();
            if (toxicityExposureScale <= 0f)
                return;

            float damageMultiplier = DynamicDifficultyDirector.Current.DamageMultiplier;
            float damage = stats.RadiationDamageRate * 0.65f * toxicity * toxicityExposureScale * damageMultiplier * dt;
            integrity = math.max(0f, integrity - damage);
            MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.IntegrityFailure);
        }

        private void HandleNutritionalToxicity(float dt)
        {
            if (_nutritionalToxicitySecondsRemaining <= 0f)
                return;

            float clampedDt = math.max(0f, dt);
            float damagePerSecond = ResolveNutritionalToxicityDamagePerSecond(
                _nutritionalToxicitySeverity01,
                stats != null ? stats.RadiationDamageRate : 0f);
            if (damagePerSecond > 0f)
            {
                integrity = math.max(0f, integrity - damagePerSecond * clampedDt);
                MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.IntegrityFailure);
            }

            _nutritionalToxicitySecondsRemaining = math.max(0f, _nutritionalToxicitySecondsRemaining - clampedDt);
            if (_nutritionalToxicitySecondsRemaining <= 0f)
                _nutritionalToxicitySeverity01 = 0f;
        }

        internal static float ResolveNutritionalToxicityDamagePerSecond(float severity01, float baseDamageRate)
        {
            return SomaticSurvivalMath.ResolveNutritionalToxicityDamagePerSecond(severity01, baseDamageRate);
        }

        internal float ResolveEnvironmentalResistance(HazardType hazardType)
        {
            float exposureScale = hazardType == HazardType.Heat
                ? ResolveTransportThermalExposureScale()
                : ResolveTransportRadiationExposureScale();
            if (exposureScale <= 0.0001f)
                return 1000f;

            return 1f / exposureScale;
        }

        private PlayerTransportPreset ResolveActiveTransportPreset()
        {
            return _playerTransportCoordinator != null
                ? _playerTransportCoordinator.ResolveTransportPreset()
                : null;
        }

        private VehicleUpgradeModule ResolveActiveVehicleUpgradeModule()
        {
            if (_playerTransportCoordinator == null ||
                !_playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner) ||
                lifecycleOwner == null)
            {
                return null;
            }

            MonoBehaviour transportBehaviour = lifecycleOwner as MonoBehaviour;
            if (transportBehaviour == null)
                return null;

            return transportBehaviour.TryGetComponent(out VehicleUpgradeModule upgradeModule)
                ? upgradeModule
                : null;
        }

        private float ResolveTransportSafeDepthBonusMeters()
        {
            VehicleUpgradeModule upgradeModule = ResolveActiveVehicleUpgradeModule();
            return upgradeModule != null
                ? upgradeModule.SafeDepthBonusMeters
                : 0f;
        }

        private float ResolveEffectiveSafeDepthMeters()
        {
            return stats != null
                ? math.max(0f, stats.SafeDepth + ResolveTransportSafeDepthBonusMeters())
                : 0f;
        }

        private float ResolveTransportOxygenConsumptionScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? transportPreset.OxygenConsumptionScale
                : 1f;
        }

        private float ResolveTransportPressureDamageScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            float presetScale = transportPreset != null
                ? transportPreset.PressureDamageScale
                : 1f;
            VehicleUpgradeModule upgradeModule = ResolveActiveVehicleUpgradeModule();
            float upgradeScale = upgradeModule != null
                ? math.max(0.1f, upgradeModule.PressureDamageScale)
                : 1f;
            return presetScale * upgradeScale;
        }

        private float ResolveTransportThermalExposureScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            float presetScale = transportPreset != null
                ? transportPreset.ThermalExposureScale
                : 1f;
            VehicleUpgradeModule upgradeModule = ResolveActiveVehicleUpgradeModule();
            float upgradeScale = upgradeModule != null
                ? math.max(0.1f, upgradeModule.ThermalExposureScale)
                : 1f;
            return presetScale * upgradeScale;
        }

        private float ResolveTransportRadiationExposureScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? transportPreset.RadiationExposureScale
                : 1f;
        }

        private float ResolveCurrentOxygenDrainPerSecond()
        {
            float baseRate = ResolveBaseOxygenDrainPerSecond();
            float pressureFactor = ResolveOxygenPressureScale();
            float movementFactor = ResolveOxygenMovementScale();
            float stressFactor = ResolveOxygenStressScale();
            float leakFactor = ResolveOxygenLeakScale();
            float carryMassFactor = ResolveOxygenCarryMassScale();
            return ResolveMultiplicativeOxygenDrain(
                baseRate,
                pressureFactor,
                movementFactor,
                stressFactor,
                leakFactor,
                carryMassFactor);
        }

        private float ResolveBaseOxygenDrainPerSecond()
        {
            DifficultyModifierData modifiers = DynamicDifficultyDirector.Current;
            return stats.OxygenConsumptionRate *
                   ResolveTransportOxygenConsumptionScale() *
                   modifiers.OxygenDepletionRate;
        }

        private float ResolveOxygenPressureScale()
        {
            return 1f + ResolveOverpressureSeverity01();
        }

        private float ResolveOxygenMovementScale()
        {
            float authoredCruiseSpeed = ResolveAuthoredCruiseSpeedMetersPerSecond();
            float currentSpeed = ResolveCurrentMovementSpeedMetersPerSecond();
            float move01 = math.saturate(currentSpeed / authoredCruiseSpeed);
            return math.lerp(1f, OxygenMovementScaleCeiling, move01);
        }

        private float ResolveOxygenStressScale()
        {
            return ResolveHeartrateOxygenMultiplier(ResolveOxygenStressMagnitude01());
        }

        private float ResolveOxygenLeakScale()
        {
            float suitLeakScale = 1f + (1f - IntegrityNormalized) * OxygenLeakScaleCeilingBonus;
            float vehicleLeakScale = _traumaDispatcher != null
                ? _traumaDispatcher.AdditionalVehicleOxygenDrainScale
                : 1f;
            return suitLeakScale * vehicleLeakScale;
        }

        private float ResolveOxygenCarryMassScale()
        {
            float carry01 = math.saturate(weight / math.max(0.01f, stats.CarryCapacityKg));
            return 1f + carry01 * OxygenCarryMassScaleCeilingBonus;
        }

        private float ResolveOxygenStressSeverity01()
        {
            float injurySeverity = math.max(_bleedingSeverity01, _fracturePenalty01);
            float thermalSeverity = math.max(_coldSeverity01, _heatSeverity01);
            float hullStressSeverity = _playerMovement != null
                ? _playerMovement.CurrentHullStress01
                : 0f;
            return math.saturate(
                hullStressSeverity * 0.35f +
                (1f - OxygenNormalized) * 0.20f +
                injurySeverity * 0.15f +
                thermalSeverity * 0.15f +
                _decompressionRisk01 * 0.10f +
                (1f - IntegrityNormalized) * 0.05f);
        }

        private float ResolveOxygenStressMagnitude01()
        {
            float physiologicalStress = ResolveOxygenStressSeverity01();
            float movementStress = ResolveMovementStressMagnitude01();
            float traumaStress = ResolveTraumaStressMagnitude01();
            return math.saturate(math.max(physiologicalStress, math.max(movementStress, traumaStress)));
        }

        internal static float ResolveHeartrateOxygenMultiplier(float stressMagnitude01)
        {
            return ApproximateExpPositive(math.saturate(stressMagnitude01) * OxygenStressScaleCeilingBonus);
        }

        private static float ApproximateExpPositive(float x)
        {
            return 1f / math.max(ApproximateExpNegPositive(x), 0.0001f);
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private static float ResolveMagnitude(float sqrMagnitude)
        {
            return sqrMagnitude > 0.000001f
                ? math.sqrt(sqrMagnitude)
                : 0f;
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return math.lerp(from, to, math.saturate(t));
        }

        private static Vector3 ResolveSafeSavedVelocity(Vector3 velocity)
        {
            if (!IsFinite(velocity))
                return Vector3.zero;

            float speedSq = velocity.sqrMagnitude;
            if (!float.IsFinite(speedSq) || speedSq <= 0.000001f)
                return Vector3.zero;

            return speedSq <= SaveVelocityHardCapSq
                ? velocity
                : velocity * (SaveVelocityHardCapMetersPerSecond * math.rsqrt(speedSq));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private float ResolveMovementStressMagnitude01()
        {
            if (_playerMovement == null)
                return 0f;

            float underwaterStress = math.saturate(_playerMovement.CurrentUnderwaterStressIntensity01);
            float hullStress = math.saturate(_playerMovement.CurrentHullStress01);
            float fatalPressureStress = math.saturate(_playerMovement.CurrentFatalPressureSequence01);
            return math.max(underwaterStress, math.max(hullStress, fatalPressureStress));
        }

        private float ResolveTraumaStressMagnitude01()
        {
            if (_traumaDispatcher == null)
                return 0f;

            float integrityStress = math.saturate(_traumaDispatcher.IntegrityChannel01);
            float powerStress = math.saturate(_traumaDispatcher.PowerChannel01) * 0.75f;
            float clarityStress = math.saturate(_traumaDispatcher.ClarityChannel01);
            float hazardStress = math.max(
                math.saturate(_traumaDispatcher.HazardRadiationSignal01),
                math.max(
                    math.saturate(_traumaDispatcher.HazardThermalSignal01),
                    math.saturate(_traumaDispatcher.HazardToxicSignal01)));

            return math.max(
                math.max(integrityStress, clarityStress),
                math.max(powerStress, hazardStress));
        }

        private float ResolveCurrentMovementSpeedMetersPerSecond()
        {
            return _playerRigidbody != null
                ? ResolveMagnitude(_playerRigidbody.linearVelocity.sqrMagnitude)
                : 0f;
        }

        private float ResolveAuthoredCruiseSpeedMetersPerSecond()
        {
            float authoredCruiseSpeed = 1f;

            if (_playerMovement != null && _playerMovement.CurrentSuit != null)
                authoredCruiseSpeed = math.max(0.01f, _playerMovement.CurrentSuit.maxSwimSpeed);

            if (_playerTransportCoordinator != null)
                authoredCruiseSpeed *= math.max(0.01f, _playerTransportCoordinator.ResolveTransportSpeedMultiplier());

            return math.max(0.01f, authoredCruiseSpeed);
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

            var atmosphere = Hecton8.Core.GlobalRegistry.Atmosphere;

            // Temperature Publishing (Atmosphere + Local)
            float baseTemp = atmosphere != null ? atmosphere.CurrentTemperature : 20f;
            float totalTemp = baseTemp +
                HectonHazardManager.GetHazardIntensity(transform.position, HazardType.Heat) -
                ResolveAbyssalColdPenaltyCelsius();
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
            bool integrityFailure = integrity <= 0f;
            bool oxygenFailure = oxygen <= 0f && !_oxygenGraceActive;
            if (!integrityFailure && !oxygenFailure)
                return;

            alive = false;
            _lastDeathCause = ResolveDeathCause();
            CaptureDeathRecord();
            RecordDeathTelemetry();
            OnDeath?.Invoke();
            HectonEventBus.Publish(new PlayerDiedEvent(this, _lastDeathCause, _lastDeathRecord));
            enabled = false;
        }

        private void UpdateOxygenGraceState(float deltaTime)
        {
            if (integrity <= 0f)
            {
                ResetOxygenGraceState();
                return;
            }

            if (oxygen > 0f)
            {
                ResetOxygenGraceState();
                return;
            }

            if (!_oxygenGraceActive)
            {
                _oxygenGraceActive = true;
                _oxygenGraceTimer = OxygenGraceDurationSeconds;
            }
            else
            {
                _oxygenGraceTimer = Mathf.Max(0f, _oxygenGraceTimer - Mathf.Max(0f, deltaTime));
            }

            float elapsedGraceSeconds = OxygenGraceDurationSeconds - _oxygenGraceTimer;
            float gracePhase = Mathf.Clamp01(elapsedGraceSeconds / Mathf.Max(0.01f, OxygenGraceDurationSeconds));
            _oxygenGraceVisionBlur01 = Mathf.Sin(gracePhase * Mathf.PI);

            if (_playerMovement != null)
                _playerMovement.SetRuntimeEmergencyMovementMultiplier(OxygenGraceSpeedMultiplier);

            if (_oxygenGraceTimer <= 0f)
            {
                _oxygenGraceActive = false;
                _oxygenGraceVisionBlur01 = 1f;
                if (_playerMovement != null)
                    _playerMovement.SetRuntimeEmergencyMovementMultiplier(1f);
            }
        }

        private void ResetOxygenGraceState()
        {
            _oxygenGraceActive = false;
            _oxygenGraceTimer = 0f;
            _oxygenGraceVisionBlur01 = 0f;
            if (_playerMovement != null)
                _playerMovement.SetRuntimeEmergencyMovementMultiplier(1f);
        }

        private float ResolveFloodedThermalInsulationFactor()
        {
            return _traumaDispatcher != null
                ? _traumaDispatcher.FloodedThermalInsulationFactor
                : 1f;
        }

        private float ResolveFloodedExternalTemperature(float fallbackEnvironmentTemperature)
        {
            if (_traumaDispatcher == null || !_traumaDispatcher.HasFloodedTemperatureOverride)
                return fallbackEnvironmentTemperature;

            return _traumaDispatcher.FloodedModuleAmbientTemperatureCelsius;
        }

        private void DisposeInjectedSurvivalDatabase()
        {
            DisposeTrackedNativeArray(ref _survivalDatabaseStableHashes);
            DisposeTrackedNativeArray(ref _survivalDatabaseMassKilograms);
            DisposeTrackedNativeArray(ref _survivalDatabaseVolumeLiters);
            DisposeTrackedNativeArray(ref _survivalDatabaseEnergyDensityMegajoulesPerKilogram);
            DisposeTrackedNativeArray(ref _survivalDatabaseBaseDurability);
            _survivalDatabaseItemCount = 0;
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeMemoryLifetime);
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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
            TryApplyDamageTrauma(amount);
            ForceDirty(ref lastPubIntegrity);
            CheckLethalConditions();
        }

        /// <summary>
        /// Reports heavy physical trauma from collision or fauna impact so survival injuries can be applied without inventing a second body-state owner.
        /// </summary>
        /// <param name="damageMagnitude">Raw incoming trauma magnitude.</param>
        /// <param name="severity01">Normalized trauma severity.</param>
        internal void ReportPhysicalTrauma(float damageMagnitude, float severity01)
        {
            if (!alive)
                return;

            float clampedSeverity = Mathf.Clamp01(severity01);
            if (damageMagnitude < MajorPhysicalDamageThreshold && clampedSeverity < MajorTraumaSeverityThreshold)
                return;

            TryApplyTraumaStates(damageMagnitude, clampedSeverity);
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

        public void ApplyNutritionalToxicity(
            float severity01 = NutritionalToxicityDefaultSeverity01,
            float durationSeconds = NutritionalToxicityDefaultDurationSeconds)
        {
            float clampedSeverity = math.saturate(severity01);
            float clampedDuration = math.max(0f, durationSeconds);
            if (clampedSeverity <= 0f || clampedDuration <= 0f)
                return;

            _nutritionalToxicitySeverity01 = math.max(_nutritionalToxicitySeverity01, clampedSeverity);
            _nutritionalToxicitySecondsRemaining = math.max(_nutritionalToxicitySecondsRemaining, clampedDuration);

            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);

            if (_playerHealth != null)
                _playerHealth.ApplyNutritionalToxicity(clampedSeverity, clampedDuration);
        }

        internal static bool ShouldApplyNutritionalToxicityOnConsume(int itemHashId)
        {
            return itemHashId == _MembraneTissueHashId;
        }

        internal static bool ShouldApplyNutritionalToxicityOnConsume(ItemData item)
        {
            if (item == null)
                return false;

            if (ShouldApplyNutritionalToxicityOnConsume(LocHash.Compute(item.PersistentId)))
                return true;

            return item.isRawResource &&
                   item.resourceFamily == ResourceFamily.Organic &&
                   (item.category == ItemCategory.Material || item.category == ItemCategory.Organic);
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

        /// <summary>
        /// Parses and injects item parameters from a tabular survival database asset.
        /// </summary>
        /// <param name="databaseAsset">Text source containing StableId keyed item rows.</param>
        public bool TryInjectSurvivalDatabase(TextAsset databaseAsset)
        {
            return databaseAsset != null && TryInjectSurvivalDatabase(databaseAsset.text);
        }

        /// <summary>
        /// Parses and injects item parameters from raw survival database text.
        /// </summary>
        /// <param name="databaseText">Raw table text containing StableId keyed item rows.</param>
        public bool TryInjectSurvivalDatabase(string databaseText)
        {
            if (!TryParseSurvivalDatabase(databaseText, out NativeArray<SurvivalDatabaseItemRecord> parsedItems, out int parsedItemCount))
                return false;

            DisposeInjectedSurvivalDatabase();
            if (parsedItemCount <= 0)
            {
                parsedItems.Dispose();
                return false;
            }

            _survivalDatabaseStableHashes = new NativeArray<uint>(parsedItemCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _survivalDatabaseMassKilograms = new NativeArray<float>(parsedItemCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _survivalDatabaseVolumeLiters = new NativeArray<float>(parsedItemCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _survivalDatabaseEnergyDensityMegajoulesPerKilogram = new NativeArray<float>(parsedItemCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _survivalDatabaseBaseDurability = new NativeArray<int>(parsedItemCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(_survivalDatabaseStableHashes, nameof(_survivalDatabaseStableHashes));
            RegisterTrackedNativeArray(_survivalDatabaseMassKilograms, nameof(_survivalDatabaseMassKilograms));
            RegisterTrackedNativeArray(_survivalDatabaseVolumeLiters, nameof(_survivalDatabaseVolumeLiters));
            RegisterTrackedNativeArray(_survivalDatabaseEnergyDensityMegajoulesPerKilogram, nameof(_survivalDatabaseEnergyDensityMegajoulesPerKilogram));
            RegisterTrackedNativeArray(_survivalDatabaseBaseDurability, nameof(_survivalDatabaseBaseDurability));
            _survivalDatabaseItemCount = parsedItemCount;

            for (int i = 0; i < parsedItemCount; i++)
            {
                SurvivalDatabaseItemRecord parsedItem = parsedItems[i];
                _survivalDatabaseStableHashes[i] = parsedItem.StableHash;
                _survivalDatabaseMassKilograms[i] = parsedItem.MassKilograms;
                _survivalDatabaseVolumeLiters[i] = parsedItem.VolumeLiters;
                _survivalDatabaseEnergyDensityMegajoulesPerKilogram[i] = parsedItem.EnergyDensityMegajoulesPerKilogram;
                _survivalDatabaseBaseDurability[i] = parsedItem.BaseDurability;
            }

            parsedItems.Dispose();
            return _survivalDatabaseItemCount > 0;
        }

        /// <summary>
        /// Resolves injected survival parameters for a stable item identifier.
        /// </summary>
        /// <param name="stableId">Persistent item identifier.</param>
        /// <param name="parameters">Parsed item parameters when the lookup succeeds.</param>
        public bool TryGetInjectedItemParameters(string stableId, out SurvivalDatabaseItemParameters parameters)
        {
            parameters = default;

            if (string.IsNullOrWhiteSpace(stableId) ||
                !_survivalDatabaseStableHashes.IsCreated ||
                _survivalDatabaseItemCount <= 0)
            {
                return false;
            }

            uint stableHash = ComputeStableIdHash(stableId.AsSpan());
            for (int i = 0; i < _survivalDatabaseItemCount; i++)
            {
                if (_survivalDatabaseStableHashes[i] != stableHash)
                    continue;

                parameters = new SurvivalDatabaseItemParameters(
                    stableId,
                    _survivalDatabaseStableHashes[i],
                    _survivalDatabaseMassKilograms[i],
                    _survivalDatabaseVolumeLiters[i],
                    _survivalDatabaseEnergyDensityMegajoulesPerKilogram[i],
                    _survivalDatabaseBaseDurability[i]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves injected survival parameters for an ItemData asset via its persistent identifier.
        /// </summary>
        /// <param name="itemData">Authored item asset.</param>
        /// <param name="parameters">Parsed item parameters when the lookup succeeds.</param>
        public bool TryGetInjectedItemParameters(ItemData itemData, out SurvivalDatabaseItemParameters parameters)
        {
            parameters = default;
            return itemData != null && TryGetInjectedItemParameters(itemData.PersistentId, out parameters);
        }

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
            dto.injuryFlags = (byte)_injuryStatus;
            dto.bleedingSecondsRemaining = _bleedingSecondsRemaining;
            dto.bleedingDamagePerSecond = _bleedingDamagePerSecond;
            dto.bleedingSeverity01 = _bleedingSeverity01;
            dto.fractureSecondsRemaining = _fractureSecondsRemaining;
            dto.fracturePenalty01 = _fracturePenalty01;
            dto.environmentTemperature = _environmentTemperature;
            dto.coldStressSeverity01 = _coldSeverity01;
            dto.heatStressSeverity01 = _heatSeverity01;
            dto.nitrogenBuildUp = _nitrogenBuildUp;
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
            dto.SetVelocity(_playerRigidbody != null ? ResolveSafeSavedVelocity(_playerRigidbody.linearVelocity) : Vector3.zero);
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
            _currentLifeDurationSeconds = hasTelemetryV23 ? Math.Max(0d, dto.currentLifeDurationSeconds) : 0d;
            _currentLifePeakDepthMeters = hasTelemetryV23 ? Math.Max(0d, dto.currentLifePeakDepthMeters) : 0d;
            _currentLifeLowestOxygenNormalized = hasTelemetryV23 ? Mathf.Clamp01(dto.currentLifeLowestOxygenNormalized) : OxygenNormalized;
            _currentLifeLowestEnergyNormalized = hasTelemetryV23 ? Mathf.Clamp01(dto.currentLifeLowestEnergyNormalized) : EnergyNormalized;
            _currentLifeLowestIntegrityNormalized = hasTelemetryV23 ? Mathf.Clamp01(dto.currentLifeLowestIntegrityNormalized) : IntegrityNormalized;
            _injuryStatus = (PlayerInjuryStatus)dto.injuryFlags;
            _bleedingSecondsRemaining = Mathf.Max(0f, dto.bleedingSecondsRemaining);
            _bleedingDamagePerSecond = Mathf.Max(0f, dto.bleedingDamagePerSecond);
            _bleedingSeverity01 = Mathf.Clamp01(dto.bleedingSeverity01);
            _fractureSecondsRemaining = Mathf.Max(0f, dto.fractureSecondsRemaining);
            _fracturePenalty01 = Mathf.Clamp01(dto.fracturePenalty01);
            _environmentTemperature = dto.environmentTemperature;
            _internalTemperature = _environmentTemperature;
            _coldSeverity01 = Mathf.Clamp01(dto.coldStressSeverity01);
            _heatSeverity01 = Mathf.Clamp01(dto.heatStressSeverity01);
            _thermalStressMode = ResolveThermalStressModeFromState();
            _nitrogenBuildUp = Mathf.Clamp(dto.nitrogenBuildUp, 0f, NitrogenBuildUpHardCap);
            _nitrogenNarcosis01 = ResolveNitrogenNarcosis01(_nitrogenBuildUp);
            ResetOxygenGraceState();
            alive     = integrity > 0f;
            _lastDeathCause = alive ? SurvivalDeathCause.None : ResolveDeathCause();
            _pendingIntegrityDeathCause = SurvivalDeathCause.None;
            _hasLastDeathRecord = hasTelemetryV23 && dto.hasLastDeathRecord;
            _lastDeathRecord = _hasLastDeathRecord
                ? new SurvivalDeathRecord(
                    (SurvivalDeathCause)dto.lastDeathCause,
                    dto.GetLastDeathPosition(),
                    Math.Max(0d, dto.lastDeathLifeDurationSeconds),
                    Math.Max(0d, dto.lastDeathPeakDepthMeters),
                    Mathf.Clamp01(dto.lastDeathLowestOxygenNormalized),
                    Mathf.Clamp01(dto.lastDeathLowestEnergyNormalized),
                    Mathf.Clamp01(dto.lastDeathLowestIntegrityNormalized))
                : default;
            ResetPressureExposureTracking();

            Vector3 pos = dto.GetPosition();
            Quaternion rotation = dto.GetRotation();
            if (IsFinite(pos) && IsFinite(rotation))
                transform.SetPositionAndRotation(pos, rotation);

            if (_playerRigidbody != null)
            {
                _playerRigidbody.linearVelocity = ResolveSafeSavedVelocity(dto.GetVelocity());
                _playerRigidbody.angularVelocity = Vector3.zero;
            }

            ApplyInjuryMovementPenalty();
            ApplyNitrogenMovementPenalty();
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
            _currentLifeDurationSeconds = 0d;
            _currentLifePeakDepthMeters = 0d;
            _currentLifeLowestOxygenNormalized = 1f;
            _currentLifeLowestEnergyNormalized = 1f;
            _currentLifeLowestIntegrityNormalized = 1f;
            ResetPressureExposureTracking();
            ResetInjuryState();
            ResetThermalState();
            ResetOxygenGraceState();

            _tempGraceTimer = 0f;
            _radGraceTimer  = 0f;

            ApplyInjuryMovementPenalty();
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

        private void HandleInjuries(float dt)
        {
            bool injuryChanged = false;

            if (IsBleeding)
            {
                integrity = math.max(0f, integrity - _bleedingDamagePerSecond * dt);
                MarkIntegrityDeathCauseIfNeeded(SurvivalDeathCause.IntegrityFailure);

                if (_bleedingSeverity01 > BleedingTrailPulseThreshold)
                    BleedingTrailPulse?.Invoke(_bleedingSeverity01, transform.position);

                _bleedingSecondsRemaining = math.max(0f, _bleedingSecondsRemaining - dt);
                if (_bleedingSecondsRemaining <= 0f)
                {
                    _injuryStatus &= ~PlayerInjuryStatus.Bleeding;
                    _bleedingDamagePerSecond = 0f;
                    _bleedingSeverity01 = 0f;
                    injuryChanged = true;
                }
            }

            if (HasFracture)
            {
                _fractureSecondsRemaining = math.max(0f, _fractureSecondsRemaining - dt);
                if (_fractureSecondsRemaining <= 0f)
                {
                    _injuryStatus &= ~PlayerInjuryStatus.Fracture;
                    _fracturePenalty01 = 0f;
                    injuryChanged = true;
                }
            }

            if (injuryChanged)
                NotifyInjuryStateChanged();
        }

        private void ApplyInjuryMovementPenalty()
        {
            if (_playerMovement == null)
                return;

            float injuryMultiplier = HasFracture
                ? Mathf.Clamp(1f - _fracturePenalty01, 0.35f, 1f)
                : 1f;
            _playerMovement.SetRuntimeInjurySwimSpeedMultiplier(injuryMultiplier);
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

            _telemetryBuffer.Clear();
            BuildPressureExposureSummary(_telemetryBuffer);

            FieldOperationLogSystem.RecordOperation(
                "SUIT",
                "PRESSURE WINDOW BREACHED",
                _telemetryBuffer,
                "WARN");
        }

        private void RecordDeathTelemetry()
        {
            if (!_hasLastDeathRecord)
                return;

            _telemetryBuffer.Clear();
            _telemetryBuffer.Append("Cause ");
            _telemetryBuffer.Append(ResolveDeathCauseLabel(_lastDeathRecord.Cause));
            _telemetryBuffer.Append(" // Life ");
            _telemetryBuffer.AppendInt((int)_lastDeathRecord.LifeDurationSeconds);
            _telemetryBuffer.Append("s // Peak ");
            _telemetryBuffer.AppendInt((int)_lastDeathRecord.PeakDepthMeters);
            _telemetryBuffer.Append("m // O2 low ");
            _telemetryBuffer.AppendInt((int)(_lastDeathRecord.LowestOxygenNormalized * 100f));
            _telemetryBuffer.Append("% // PWR low ");
            _telemetryBuffer.AppendInt((int)(_lastDeathRecord.LowestEnergyNormalized * 100f));
            _telemetryBuffer.Append("% // Marker ");
            _telemetryBuffer.AppendInt((int)_lastDeathRecord.Position.x);
            _telemetryBuffer.Append(",");
            _telemetryBuffer.AppendInt((int)_lastDeathRecord.Position.y);
            _telemetryBuffer.Append(",");
            _telemetryBuffer.AppendInt((int)_lastDeathRecord.Position.z);
            _telemetryBuffer.Append(". Advice: ");
            _telemetryBuffer.Append(ResolveDeathAdvice(_lastDeathRecord.Cause));

            FieldOperationLogSystem.RecordOperation(
                "SUIT",
                "LAST LOSS MARKER UPDATED",
                _telemetryBuffer,
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
            float pressureDamagePerSecond =
                stats.PressureDamageRate *
                (1f + overpressureMeters * stats.PressureScalePerMeter) *
                pressureDamageScale;
            return pressureDamagePerSecond * DynamicDifficultyDirector.Current.DamageMultiplier;
        }

        private float ResolvePressureExposureSeverity01()
        {
            if (stats == null)
                return 0f;

            float overpressureSeverity = ResolveOverpressureSeverity01();
            float damageSeverity = Mathf.Clamp01(ResolveCurrentPressureDamagePerSecond() / Mathf.Max(1f, stats.MaxIntegrity * 0.08f));
            return Mathf.Clamp01(overpressureSeverity * 0.65f + damageSeverity * 0.35f);
        }

        private void BuildPressureExposureSummary(FixedCharBuffer buffer)
        {
            buffer.Append("Exceeded safe depth by ");
            buffer.AppendInt((int)_currentPressurePeakExcessMeters);
            buffer.Append("m for ");
            buffer.AppendInt((int)_currentPressureExposureSeconds);
            buffer.Append("s // Peak hull attrition ");
            buffer.AppendFloat((float)_currentPressurePeakDamagePerSecond, 1);
            buffer.Append("/s // Suit rating ");
            buffer.AppendInt((int)ResolveEffectiveSafeDepthMeters());
            buffer.Append("m");
        }

        private void ResetPressureExposureTracking()
        {
            _currentPressureExposureSeconds = 0d;
            _currentPressurePeakExcessMeters = 0d;
            _currentPressurePeakDamagePerSecond = 0d;
        }

        private void ResetInjuryState()
        {
            _injuryStatus = PlayerInjuryStatus.None;
            _bleedingSecondsRemaining = 0f;
            _bleedingDamagePerSecond = 0f;
            _bleedingSeverity01 = 0f;
            _fractureSecondsRemaining = 0f;
            _fracturePenalty01 = 0f;
            NotifyInjuryStateChanged();
        }

        private void ResetThermalState()
        {
            _environmentTemperature = DefaultInternalTemperatureCelsius;
            _internalTemperature = DefaultInternalTemperatureCelsius;
            _coldSeverity01 = 0f;
            _heatSeverity01 = 0f;
            _thermalStressMode = ThermalStressMode.None;
            _decompressionRisk01 = 0f;
            _rapidAscentMetersPerSecond = 0f;
            _lastTrackedDepthMeters = 0f;
            _airPocketNitrogenPauseTimer = 0f;
            _nutritionalToxicitySecondsRemaining = 0f;
            _nutritionalToxicitySeverity01 = 0f;
            _decompressionVomitToolDropCooldown = 0f;
            ResetNitrogenNarcosisState();
            _oxygenGraceVisionBlur01 = 0f;
        }

        private void ResetNitrogenNarcosisState()
        {
            _nitrogenBuildUp = 0f;
            _nitrogenNarcosis01 = 0f;
            _nitrogenLoadWarningIssued = false;
            ApplyNitrogenMovementPenalty();
        }

        private void TryBootstrapInjectedSurvivalDatabase()
        {
            if (survivalDatabaseSource == null)
                return;

            if (!TryInjectSurvivalDatabase(survivalDatabaseSource))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonSurvival] Failed to parse injected survival database source. Item parameter lookup disabled.");
#endif
            }
        }

        private static bool TryParseSurvivalDatabase(
            string databaseText,
            out SurvivalDatabaseItemParameters[] parsedItems,
            out Dictionary<string, int> parsedLookup)
        {
            parsedItems = Array.Empty<SurvivalDatabaseItemParameters>();
            parsedLookup = null;

            if (string.IsNullOrWhiteSpace(databaseText))
                return false;

            // COLD ALLOC: List<SurvivalDatabaseItemParameters>[256] — injected survival database row staging during cold parse — owner: HectonSurvivalSystem
            List<SurvivalDatabaseItemParameters> parsedRows = new List<SurvivalDatabaseItemParameters>(SurvivalDatabaseRowCapacity);
            // COLD ALLOC: Dictionary<string, int>[16] — survival database header column map during cold parse — owner: HectonSurvivalSystem
            Dictionary<string, int> columnLookup = new Dictionary<string, int>(SurvivalDatabaseColumnCapacity, StringComparer.Ordinal);

            bool headerFound = false;

            using (StringReader reader = new StringReader(databaseText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (headerFound && parsedRows.Count > 0)
                            break;

                        continue;
                    }

                    if (!headerFound)
                    {
                        if (!line.StartsWith("StableId|", StringComparison.Ordinal))
                            continue;

                        PopulateSurvivalDatabaseColumnLookup(line, columnLookup);
                        if (!HasRequiredSurvivalDatabaseColumns(columnLookup))
                            return false;

                        headerFound = true;
                        continue;
                    }

                    if (line[0] == '=' || line[0] == '[' || line[0] == '-')
                        break;

                    if (line.IndexOf('|') < 0)
                        break;

                    if (!TryParseSurvivalDatabaseRow(line, columnLookup, out SurvivalDatabaseItemParameters row))
                        return false;

                    parsedRows.Add(row);
                }
            }

            if (!headerFound || parsedRows.Count == 0)
                return false;

            // COLD ALLOC: Dictionary<string, int>[parsedRows.Count] — StableId to injected item-parameter index map — owner: HectonSurvivalSystem
            parsedLookup = new Dictionary<string, int>(parsedRows.Count, StringComparer.Ordinal);
            for (int i = 0; i < parsedRows.Count; i++)
            {
                string stableId = parsedRows[i].StableId;
                if (parsedLookup.ContainsKey(stableId))
                    return false;

                parsedLookup.Add(stableId, i);
            }

            // COLD ALLOC: SurvivalDatabaseItemParameters[parsedRows.Count] — immutable injected item-parameter snapshot — owner: HectonSurvivalSystem
            parsedItems = parsedRows.ToArray();
            return true;
        }

        private static void PopulateSurvivalDatabaseColumnLookup(string headerLine, Dictionary<string, int> columnLookup)
        {
            columnLookup.Clear();
            // COLD ALLOC: string[header token count] — survival database header tokenization during cold parse — owner: HectonSurvivalSystem
            string[] headerTokens = headerLine.Split('|');
            for (int i = 0; i < headerTokens.Length; i++)
            {
                string token = headerTokens[i].Trim();
                if (token.Length == 0 || columnLookup.ContainsKey(token))
                    continue;

                columnLookup.Add(token, i);
            }
        }

        private static bool HasRequiredSurvivalDatabaseColumns(Dictionary<string, int> columnLookup)
        {
            return
                columnLookup.ContainsKey("StableId") &&
                columnLookup.ContainsKey("Hash") &&
                columnLookup.ContainsKey("MassKg") &&
                columnLookup.ContainsKey("VolumeL") &&
                columnLookup.ContainsKey("EnergyDensityMJkg") &&
                columnLookup.ContainsKey("BaseDurability");
        }

        private static bool TryParseSurvivalDatabaseRow(
            string rowLine,
            Dictionary<string, int> columnLookup,
            out SurvivalDatabaseItemParameters row)
        {
            row = default;
            // COLD ALLOC: string[row token count] — survival database row tokenization during cold parse — owner: HectonSurvivalSystem
            string[] tokens = rowLine.Split('|');

            if (!TryGetRequiredColumnValue(tokens, columnLookup, "StableId", out string stableId) ||
                !TryGetRequiredColumnValue(tokens, columnLookup, "Hash", out string hashToken) ||
                !TryGetRequiredColumnValue(tokens, columnLookup, "MassKg", out string massToken) ||
                !TryGetRequiredColumnValue(tokens, columnLookup, "VolumeL", out string volumeToken) ||
                !TryGetRequiredColumnValue(tokens, columnLookup, "EnergyDensityMJkg", out string energyDensityToken) ||
                !TryGetRequiredColumnValue(tokens, columnLookup, "BaseDurability", out string durabilityToken))
            {
                return false;
            }

            if (!TryParseStableHash(hashToken, out uint stableHash) ||
                !float.TryParse(massToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float massKilograms) ||
                !float.TryParse(volumeToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float volumeLiters) ||
                !float.TryParse(energyDensityToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float energyDensityMegajoulesPerKilogram) ||
                !int.TryParse(durabilityToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int baseDurability))
            {
                return false;
            }

            row = new SurvivalDatabaseItemParameters(
                stableId,
                stableHash,
                massKilograms,
                volumeLiters,
                energyDensityMegajoulesPerKilogram,
                baseDurability);
            return true;
        }

        private static bool TryGetRequiredColumnValue(
            string[] tokens,
            Dictionary<string, int> columnLookup,
            string columnName,
            out string value)
        {
            value = null;

            if (!columnLookup.TryGetValue(columnName, out int columnIndex))
                return false;

            if ((uint)columnIndex >= (uint)tokens.Length)
                return false;

            value = tokens[columnIndex].Trim();
            return value.Length > 0;
        }

        private static bool TryParseStableHash(string hashToken, out uint stableHash)
        {
            stableHash = 0u;
            if (string.IsNullOrWhiteSpace(hashToken))
                return false;

            string normalizedHashToken = hashToken.Trim();
            if (normalizedHashToken.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                normalizedHashToken = normalizedHashToken.Substring(2);

            return uint.TryParse(
                normalizedHashToken,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out stableHash);
        }

        private static bool TryParseSurvivalDatabase(
            string databaseText,
            out NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            out int parsedItemCount)
        {
            parsedItems = default;
            parsedItemCount = 0;

            if (string.IsNullOrWhiteSpace(databaseText))
                return false;

            ReadOnlySpan<char> databaseSpan = databaseText.AsSpan();
            // COLD ALLOC: SurvivalDatabaseItemRecord[256] — injected survival database row staging during cold parse — owner: HectonSurvivalSystem
            NativeArray<SurvivalDatabaseItemRecord> stagingRows = new NativeArray<SurvivalDatabaseItemRecord>(
                SurvivalDatabaseRowCapacity,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            SurvivalDatabaseColumnMap columnMap = SurvivalDatabaseColumnMap.CreateInvalid();
            bool headerFound = false;
            int cursor = 0;

            while (TryReadNextLine(databaseSpan, ref cursor, out ReadOnlySpan<char> line))
            {
                ReadOnlySpan<char> trimmedLine = TrimSurvivalDatabaseSpan(line);
                if (trimmedLine.Length == 0)
                {
                    if (headerFound && parsedItemCount > 0)
                        break;

                    continue;
                }

                if (!headerFound)
                {
                    if (!HasSurvivalDatabaseHeaderPrefix(trimmedLine))
                        continue;

                    if (!TryBuildSurvivalDatabaseColumnMap(trimmedLine, out columnMap))
                    {
                        stagingRows.Dispose();
                        return false;
                    }

                    headerFound = true;
                    continue;
                }

                char lead = trimmedLine[0];
                if (lead == '=' || lead == '[' || lead == '-')
                    break;

                if (trimmedLine.IndexOf('|') < 0)
                    break;

                if ((uint)parsedItemCount >= (uint)stagingRows.Length)
                {
                    stagingRows.Dispose();
                    return false;
                }

                if (!TryParseSurvivalDatabaseRowFlat(trimmedLine, in columnMap, out SurvivalDatabaseItemRecord row))
                {
                    stagingRows.Dispose();
                    return false;
                }

                stagingRows[parsedItemCount++] = row;
            }

            if (!headerFound || parsedItemCount == 0)
            {
                stagingRows.Dispose();
                return false;
            }

            for (int i = 0; i < parsedItemCount; i++)
            {
                for (int j = i + 1; j < parsedItemCount; j++)
                {
                    if (stagingRows[i].StableHash == stagingRows[j].StableHash)
                    {
                        stagingRows.Dispose();
                        return false;
                    }
                }
            }

            // COLD ALLOC: SurvivalDatabaseItemRecord[parsedRowCount] — immutable injected item-parameter snapshot — owner: HectonSurvivalSystem
            parsedItems = new NativeArray<SurvivalDatabaseItemRecord>(
                parsedItemCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < parsedItemCount; i++)
                parsedItems[i] = stagingRows[i];

            stagingRows.Dispose();
            return true;
        }

        private static bool HasSurvivalDatabaseHeaderPrefix(ReadOnlySpan<char> line)
        {
            ReadOnlySpan<char> prefix = "StableId|".AsSpan();
            return line.Length >= prefix.Length && line.Slice(0, prefix.Length).SequenceEqual(prefix);
        }

        private static bool TryBuildSurvivalDatabaseColumnMap(
            ReadOnlySpan<char> headerLine,
            out SurvivalDatabaseColumnMap columnMap)
        {
            columnMap = SurvivalDatabaseColumnMap.CreateInvalid();
            int tokenCursor = 0;
            int tokenIndex = 0;
            while (TryReadNextDelimitedToken(headerLine, ref tokenCursor, '|', out ReadOnlySpan<char> token))
            {
                ReadOnlySpan<char> trimmedToken = TrimSurvivalDatabaseSpan(token);
                if (trimmedToken.Length == 0)
                {
                    tokenIndex++;
                    continue;
                }

                if (trimmedToken.SequenceEqual("StableId".AsSpan()))
                    columnMap.StableId = tokenIndex;
                else if (trimmedToken.SequenceEqual("Hash".AsSpan()))
                    columnMap.Hash = tokenIndex;
                else if (trimmedToken.SequenceEqual("MassKg".AsSpan()))
                    columnMap.MassKilograms = tokenIndex;
                else if (trimmedToken.SequenceEqual("VolumeL".AsSpan()))
                    columnMap.VolumeLiters = tokenIndex;
                else if (trimmedToken.SequenceEqual("EnergyDensityMJkg".AsSpan()))
                    columnMap.EnergyDensityMegajoulesPerKilogram = tokenIndex;
                else if (trimmedToken.SequenceEqual("BaseDurability".AsSpan()))
                    columnMap.BaseDurability = tokenIndex;

                tokenIndex++;
            }

            return columnMap.HasAllRequiredColumns;
        }

        private static bool TryParseSurvivalDatabaseRowFlat(
            ReadOnlySpan<char> rowLine,
            in SurvivalDatabaseColumnMap columnMap,
            out SurvivalDatabaseItemRecord row)
        {
            row = default;
            ReadOnlySpan<char> stableId = default;
            ReadOnlySpan<char> hashToken = default;
            ReadOnlySpan<char> massToken = default;
            ReadOnlySpan<char> volumeToken = default;
            ReadOnlySpan<char> energyDensityToken = default;
            ReadOnlySpan<char> durabilityToken = default;
            bool hasStableId = false;
            bool hasHash = false;
            bool hasMass = false;
            bool hasVolume = false;
            bool hasEnergyDensity = false;
            bool hasDurability = false;
            int tokenCursor = 0;
            int tokenIndex = 0;

            while (TryReadNextDelimitedToken(rowLine, ref tokenCursor, '|', out ReadOnlySpan<char> token))
            {
                ReadOnlySpan<char> trimmedToken = TrimSurvivalDatabaseSpan(token);
                if (tokenIndex == columnMap.StableId)
                {
                    stableId = trimmedToken;
                    hasStableId = trimmedToken.Length > 0;
                }
                else if (tokenIndex == columnMap.Hash)
                {
                    hashToken = trimmedToken;
                    hasHash = trimmedToken.Length > 0;
                }
                else if (tokenIndex == columnMap.MassKilograms)
                {
                    massToken = trimmedToken;
                    hasMass = trimmedToken.Length > 0;
                }
                else if (tokenIndex == columnMap.VolumeLiters)
                {
                    volumeToken = trimmedToken;
                    hasVolume = trimmedToken.Length > 0;
                }
                else if (tokenIndex == columnMap.EnergyDensityMegajoulesPerKilogram)
                {
                    energyDensityToken = trimmedToken;
                    hasEnergyDensity = trimmedToken.Length > 0;
                }
                else if (tokenIndex == columnMap.BaseDurability)
                {
                    durabilityToken = trimmedToken;
                    hasDurability = trimmedToken.Length > 0;
                }

                tokenIndex++;
            }

            if (!hasStableId || !hasHash || !hasMass || !hasVolume || !hasEnergyDensity || !hasDurability)
                return false;

            if (!TryParseStableHash(hashToken, out uint stableHash) ||
                !float.TryParse(massToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float massKilograms) ||
                !float.TryParse(volumeToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float volumeLiters) ||
                !float.TryParse(energyDensityToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float energyDensityMegajoulesPerKilogram) ||
                !int.TryParse(durabilityToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int baseDurability))
            {
                return false;
            }

            if (ComputeStableIdHash(stableId) != stableHash)
                return false;

            row.StableHash = stableHash;
            row.MassKilograms = massKilograms;
            row.VolumeLiters = volumeLiters;
            row.EnergyDensityMegajoulesPerKilogram = energyDensityMegajoulesPerKilogram;
            row.BaseDurability = baseDurability;
            return true;
        }

        private static bool TryParseStableHash(ReadOnlySpan<char> hashToken, out uint stableHash)
        {
            stableHash = 0u;
            ReadOnlySpan<char> normalizedHashToken = TrimSurvivalDatabaseSpan(hashToken);
            if (normalizedHashToken.Length == 0)
                return false;

            if (normalizedHashToken.Length >= 2 &&
                normalizedHashToken[0] == '0' &&
                (normalizedHashToken[1] == 'x' || normalizedHashToken[1] == 'X'))
            {
                normalizedHashToken = normalizedHashToken.Slice(2);
            }

            return uint.TryParse(
                normalizedHashToken,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out stableHash);
        }

        private static uint ComputeStableIdHash(ReadOnlySpan<char> stableId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < stableId.Length; i++)
                {
                    hash ^= stableId[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static bool TryReadNextLine(ReadOnlySpan<char> source, ref int cursor, out ReadOnlySpan<char> line)
        {
            line = default;
            if ((uint)cursor >= (uint)source.Length)
                return false;

            int lineStart = cursor;
            while ((uint)cursor < (uint)source.Length)
            {
                char c = source[cursor];
                if (c == '\r' || c == '\n')
                    break;

                cursor++;
            }

            line = source.Slice(lineStart, cursor - lineStart);
            if ((uint)cursor < (uint)source.Length && source[cursor] == '\r')
                cursor++;
            if ((uint)cursor < (uint)source.Length && source[cursor] == '\n')
                cursor++;

            return true;
        }

        private static bool TryReadNextDelimitedToken(
            ReadOnlySpan<char> source,
            ref int cursor,
            char delimiter,
            out ReadOnlySpan<char> token)
        {
            if (cursor > source.Length)
            {
                token = default;
                return false;
            }

            int tokenStart = cursor;
            while ((uint)cursor < (uint)source.Length && source[cursor] != delimiter)
                cursor++;

            token = source.Slice(tokenStart, cursor - tokenStart);
            if ((uint)cursor < (uint)source.Length && source[cursor] == delimiter)
                cursor++;

            return true;
        }

        private static ReadOnlySpan<char> TrimSurvivalDatabaseSpan(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;

            while (start < value.Length && char.IsWhiteSpace(value[start]))
                start++;

            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            return start > end
                ? ReadOnlySpan<char>.Empty
                : value.Slice(start, end - start + 1);
        }

        private static float ResolveMultiplicativeOxygenDrain(
            float baseRate,
            float pressureFactor,
            float movementFactor,
            float stressFactor,
            float leakFactor,
            float carryMassFactor)
        {
            return baseRate * pressureFactor * movementFactor * stressFactor * leakFactor * carryMassFactor;
        }

        private static float ResolveExponentialTemperatureStep(
            float environmentTemperature,
            float currentInternalTemperature,
            float deltaTime,
            float tauSeconds)
        {
            float safeTau = math.max(0.01f, tauSeconds);
            float temperatureDecay = ApproximateExpNegPositive(deltaTime / safeTau);
            return environmentTemperature + (currentInternalTemperature - environmentTemperature) * temperatureDecay;
        }

        private float ResolveThermalPowerDrawPerSecond(
            float coldExcess,
            float heatExcess,
            float deepColdStressMultiplier)
        {
            float coldHeatingDrawPerSecond =
                coldExcess *
                stats.TempEnergyScale *
                ResolveAbyssalHeatingDrainMultiplier() *
                deepColdStressMultiplier;
            float heatCoolingDrawPerSecond = heatExcess * stats.TempEnergyScale;
            return coldHeatingDrawPerSecond + heatCoolingDrawPerSecond;
        }

        private void PushPressureHullStress()
        {
            if (_playerMovement == null)
                return;

            _playerMovement.RequestExternalHullStress(ResolvePressureExposureSeverity01());
        }

        private float ResolveOverpressureSeverity01()
        {
            return ResolveOverpressureSeverity01(
                OverpressureMeters,
                ResolveEffectiveSafeDepthMeters());
        }

        private static float ResolveOverpressureSeverity01(
            float overpressureMeters,
            float effectiveSafeDepthMeters)
        {
            if (overpressureMeters <= 0f)
                return 0f;

            float fullSeverityRange = math.max(
                OverpressureSeverityFullRangeMeters,
                math.max(1f, effectiveSafeDepthMeters) * OverpressureSeveritySafeDepthScale);
            return math.saturate(overpressureMeters / fullSeverityRange);
        }

        private void NotifyInjuryStateChanged()
        {
            InjuryStateChanged?.Invoke();
        }

        private void RegisterBloodScentSignal()
        {
            if (_bloodScentSpatialHandle != 0)
                return;

            _bloodScentSpatialHandle = WorldSpatialHashGrid.RegisterSignal(this, transform, FieldTargetRole.HazardProbe);
            _bloodScentFaunaSpatialHandle = FaunaSpatialHashRegistry.RegisterSignal(this, transform, FieldTargetRole.HazardProbe);
        }

        private void UnregisterBloodScentSignal()
        {
            if (_bloodScentSpatialHandle != 0)
            {
                WorldSpatialHashGrid.Unregister(_bloodScentSpatialHandle);
                _bloodScentSpatialHandle = 0;
            }

            if (_bloodScentFaunaSpatialHandle != 0)
            {
                FaunaSpatialHashRegistry.Unregister(_bloodScentFaunaSpatialHandle);
                _bloodScentFaunaSpatialHandle = 0;
            }
        }

        private void RefreshBloodScentSignal()
        {
            if (_bloodScentSpatialHandle == 0)
                return;

            WorldSpatialHashGrid.Refresh(_bloodScentSpatialHandle);
            if (_bloodScentFaunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_bloodScentFaunaSpatialHandle);
        }

        private void TryApplyDamageTrauma(float damageAmount)
        {
            float maxIntegrity = stats != null ? math.max(0.01f, stats.MaxIntegrity) : 100f;
            if (ShouldForceSuitPunctureBleeding(damageAmount, maxIntegrity))
                ApplyBleeding(1f, damageAmount);

            if (damageAmount < MajorPhysicalDamageThreshold)
                return;

            float severity = damageAmount >= SeverePhysicalDamageThreshold
                ? 1f
                : Mathf.InverseLerp(MajorPhysicalDamageThreshold, SeverePhysicalDamageThreshold, damageAmount);
            TryApplyTraumaStates(damageAmount, severity);
        }

        internal static bool ShouldForceSuitPunctureBleeding(float damageAmount, float maxIntegrity)
        {
            return SomaticSurvivalMath.ShouldForceSuitPunctureBleeding(damageAmount, maxIntegrity);
        }

        private void TryApplyTraumaStates(float damageMagnitude, float severity01)
        {
            float clampedSeverity = Mathf.Clamp01(severity01);
            if (clampedSeverity <= 0f)
                return;

            if (ShouldApplyBleeding(clampedSeverity))
                ApplyBleeding(clampedSeverity, damageMagnitude);

            if (ShouldApplyFracture(clampedSeverity))
                ApplyFracture(clampedSeverity, damageMagnitude);
        }

        private bool ShouldApplyBleeding(float severity01)
        {
            float bleedChance = LerpClamped(0.22f, 0.82f, severity01);
            return _traumaRandom.NextFloat() <= bleedChance;
        }

        private bool ShouldApplyFracture(float severity01)
        {
            float fractureChance = LerpClamped(0.08f, 0.54f, severity01);
            return _traumaRandom.NextFloat() <= fractureChance;
        }

        private void ApplyBleeding(float severity01, float damageMagnitude)
        {
            float severityScale = math.saturate(math.max(severity01, damageMagnitude / SeverePhysicalDamageThreshold));
            float duration = LerpClamped(BleedingBaseDurationSeconds, BleedingMaxDurationSeconds, severityScale);
            float damagePerSecond = LerpClamped(BleedingBaseDamagePerSecond, BleedingMaxDamagePerSecond, severityScale);
            bool stateChanged = !IsBleeding;

            _injuryStatus |= PlayerInjuryStatus.Bleeding;
            _bleedingSecondsRemaining = Mathf.Max(_bleedingSecondsRemaining, duration);
            _bleedingDamagePerSecond = Mathf.Max(_bleedingDamagePerSecond, damagePerSecond);
            _bleedingSeverity01 = Mathf.Max(_bleedingSeverity01, severityScale);

            if (stateChanged)
                NotifyInjuryStateChanged();
        }

        private void ApplyFracture(float severity01, float damageMagnitude)
        {
            float severityScale = math.saturate(math.max(severity01, damageMagnitude / SeverePhysicalDamageThreshold));
            float duration = LerpClamped(FractureBaseDurationSeconds, FractureMaxDurationSeconds, severityScale);
            float penalty = LerpClamped(FractureBasePenalty, FractureMaxPenalty, severityScale);
            bool stateChanged = !HasFracture;

            _injuryStatus |= PlayerInjuryStatus.Fracture;
            _fractureSecondsRemaining = Mathf.Max(_fractureSecondsRemaining, duration);
            _fracturePenalty01 = Mathf.Max(_fracturePenalty01, penalty);
            ApplyInjuryMovementPenalty();

            if (stateChanged)
                NotifyInjuryStateChanged();
        }

        private static float ResolveThermalSeverity01(float excess)
        {
            if (excess <= 0f)
                return 0f;

            return Mathf.Clamp01(excess / ThermalSeverityReferenceRange);
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(int ownerId, int salt)
        {
            uint seed = math.hash(new uint4(unchecked((uint)ownerId), unchecked((uint)salt), 0xD1F34F5Bu, 0x8A61C8D1u));
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }

        private ThermalStressMode ResolveThermalStressModeFromState()
        {
            if (_coldSeverity01 > 0f)
                return ThermalStressMode.Cold;

            if (_heatSeverity01 > 0f)
                return ThermalStressMode.Heat;

            return ThermalStressMode.None;
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
