using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
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
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
            _pad4 = 0;
        }

        /// <summary>Total survived time for the recorded life.</summary>
        [FieldOffset(0)]
        public readonly double LifeDurationSeconds;

        /// <summary>Deepest reached depth for the recorded life.</summary>
        [FieldOffset(8)]
        public readonly double PeakDepthMeters;

        /// <summary>World-space position where the last life ended.</summary>
        [FieldOffset(16)]
        public readonly Vector3 Position;

        /// <summary>Lowest normalized oxygen reached during the recorded life.</summary>
        [FieldOffset(28)]
        public readonly float LowestOxygenNormalized;

        /// <summary>Lowest normalized energy reached during the recorded life.</summary>
        [FieldOffset(32)]
        public readonly float LowestEnergyNormalized;

        /// <summary>Lowest normalized integrity reached during the recorded life.</summary>
        [FieldOffset(36)]
        public readonly float LowestIntegrityNormalized;

        /// <summary>Resolved fatal cause for the recorded life.</summary>
        [FieldOffset(40)]
        public readonly SurvivalDeathCause Cause;

        [FieldOffset(41)]
        private readonly byte _pad0;

        [FieldOffset(42)]
        private readonly ushort _pad1;

        [FieldOffset(44)]
        private readonly uint _pad2;

        [FieldOffset(48)]
        private readonly ulong _pad3;

        [FieldOffset(56)]
        private readonly ulong _pad4;
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

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct SurvivalDatabaseItemRecord
    {
        [FieldOffset(0)] public uint StableHash;
        [FieldOffset(4)] public float MassKilograms;
        [FieldOffset(8)] public float VolumeLiters;
        [FieldOffset(12)] public float EnergyDensityMegajoulesPerKilogram;
        [FieldOffset(16)] public int BaseDurability;
        [FieldOffset(20)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SurvivalBlackboxSnapshot
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint FrameIndex;
        [FieldOffset(8)] public uint PlayerEntityHash;
        [FieldOffset(12)] public float Oxygen01;
        [FieldOffset(16)] public float Integrity01;
        [FieldOffset(20)] public float DepthMeters;
        [FieldOffset(24)] public float PressureAtm;
        [FieldOffset(28)] public float SafeDepthMeters;
        [FieldOffset(32)] public float OverpressureMeters;
        [FieldOffset(36)] public float PressureExposureSeverity01;
        [FieldOffset(40)] public float NitrogenLoad01;
        [FieldOffset(44)] public float NitrogenNarcosis01;
        [FieldOffset(48)] public float DecompressionRisk01;
        [FieldOffset(52)] public float InternalTemperatureCelsius;
        [FieldOffset(56)] public uint StatusMask;
        [FieldOffset(60)] public uint Flags;
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
    ///   - Zero-GC Tick System (ISlowTickable, ILateFrameTickable)
    ///   - Atmospheric Hazards (Pressure, Temperature, Radiation)
    ///   - Suit Resource Management (O2, Energy, Integrity)
    ///   - Persistence (ISaveable)
    ///   - Throttled HUD Events
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonSurvivalSystem : MonoBehaviour, ISlowTickable, ILateFrameTickable, ISaveable, IGlobalRegistryHotSwapListener, IPlayerSurvivalEnvironmentReadModel, IPlayerBleedingReadModel
    {
        private const float DefaultWaterSurfaceY = 14.02f;
        private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;
        private static int s_x001HectonSurvivalSystemSignalPushDropCount;
        // ---------------------------------------------------------
        //  INSPECTOR
        // ---------------------------------------------------------

        [Header("-- Data ------------------------------------")]
        [Tooltip("Drag a SurvivalStats .asset here to configure all suit parameters.")]
        [SerializeField] private SurvivalStats stats;

        [Header("-- Scene -----------------------------------")]
        [Tooltip("World-space Y coordinate of the water surface.")]
        [SerializeField] private float surfaceWorldY = DefaultWaterSurfaceY;
        [Tooltip("Surface oxygen refill rate per second when the shared surface contract says the head is in air.")]
        [SerializeField] private float surfaceOxygenRefillRate = 15f;

        [Header("-- Thermal -------------------------------------")]
        [Tooltip("Base Newton-cooling time constant in seconds for internal suit temperature exchange with ambient water.")]
        [SerializeField, Range(1f, 600f)] private float internalTemperatureTimeConstantSeconds = 45f;

        [Header("-- Survival Database Injection -----------------")]
        [Tooltip("Optional survival database source parsed at cold bootstrap to seed StableId mass, volume, energy density, and durability lookups.")]
        [SerializeField] private TextAsset survivalDatabaseSource;

        // ---------------------------------------------------------
        //  PRIVATE STATE
        // ---------------------------------------------------------

        private float oxygen;
        private float energy;
        private float depth;
        private float integrity;
        private float pressure;
        private float weight;
        private float hunger;
        private float thirst;
        private bool  alive = true;

        private float _slowTickDt = 0.1f;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredHotSwapListener;
        private uint _survivalVitalsSignalSourceId;
        private uint _survivalVitalsSignalSequence;
        private uint _playerEntityHash;
        private uint _pendingRespawnReconciliationSequence;
        private uint _lastAppliedRespawnReconciliationSequence;
        private float _pendingNarcosisShaderScalar;
        private bool _hasPendingNarcosisShaderScalar;

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
        private int _combatTargetId;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private IAtmosphereReadModel _atmosphereRuntime;
        private IPhysicsService _physicsService;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private AbyssalThermalManager _thermalManager;
        private IModularEquipmentService _modularEquipment;
        private HazardZoneManager _hazardZoneRuntime;
        private IPlayerTransportLifecycleOwner _cachedUpgradeTransportOwner;
        private VehicleUpgradeModule _cachedVehicleUpgradeModule;
        private bool _saveRegistered;
        private IDataVault _survivalDataVault;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolicStateHandle;
        private VaultGenerationHandle<SuitIntegrityDTO> _suitIntegrityStateHandle;
        private bool _metabolicStateHandleReady;
        private bool _suitIntegrityStateHandleReady;
        private uint _nextMetabolicStateHandleRetryFrame;
        private uint _nextSuitIntegrityStateHandleRetryFrame;
        private bool _metabolicOxygenStateSyncedThisTick;
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
        private float _bleedingSeverity01;
        private float _fracturePenalty01;
        private float _environmentTemperature = 20f;
        private float _internalTemperature = 20f;
        private float _coldSeverity01;
        private float _heatSeverity01;
        private ThermalStressMode _thermalStressMode;
        private float _lastTrackedDepthMeters;
        private float _decompressionRisk01;
        private bool _physiologyBendsActive;
        private float _rapidAscentMetersPerSecond;
        private float _nitrogenBuildUp;
        private float _nitrogenLoad = 1f;
        private float _nitrogenNarcosis01;
        private PhysiologyStateSignal _cachedShinobuPhysiologySignal;
        private float _airPocketNitrogenPauseTimer;
        private float _toxicityStaminaMultiplier = 1f;
        private float _toxicity01;
        private float _movementIntentLengthSq;
        private float _movementStaminaDrainMultiplier = 1f;
        private float _lastPublishedNarcosisShaderScalar = float.PositiveInfinity;
        private uint _statusMask;
        private ulong _cachedCombatStatusMask;
        private bool _hasCachedShinobuPhysiologySignal;
        private bool _hasCachedCombatStatusMask;
        private bool _nitrogenLoadWarningIssued;
        private int _nitrogenLoadNotificationRetryFrame;
        private int _nitrogenLoadNotificationMissCount;
        private float _decompressionVomitToolDropCooldown;
        private int _bloodScentSpatialHandle;
        private int _bloodScentFaunaSpatialHandle;
        private VaultGenerationHandle<uint> _survivalDatabaseStableHashesHandle;
        private VaultGenerationHandle<float> _survivalDatabaseMassKilogramsHandle;
        private VaultGenerationHandle<float> _survivalDatabaseVolumeLitersHandle;
        private VaultGenerationHandle<float> _survivalDatabaseEnergyDensityMegajoulesPerKilogramHandle;
        private VaultGenerationHandle<int> _survivalDatabaseBaseDurabilityHandle;
        private NativeArray<SurvivalBlackboxSnapshot> _survivalBlackboxSnapshot;
        private int _survivalDatabaseItemCount;
        private int _survivalBlackboxSourceSlot = -1;
        private float _oxygenGraceTimer;
        private float _oxygenGraceVisionBlur01;
        private bool _oxygenGraceActive;
        private PlayerRuntimeContext _runtimeContext;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private Unity.Mathematics.Random _traumaRandom;
        private FixedCharBuffer _telemetryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - telemetry construction - owner: HectonSurvivalSystem
        private const float HazardGraceDuration = 3f;
        private const float SaveVelocityHardCapMetersPerSecond = 80f;
        private const float SaveVelocityHardCapSq = SaveVelocityHardCapMetersPerSecond * SaveVelocityHardCapMetersPerSecond;
        private const uint KccVelocitySurvivalMaxAgeFrames = 12u;
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
        private const BufferID MetabolicStateBufferId = (BufferID)ShinobuMetabolismVaultContract.MetabolismStatesBufferId;
        private const BufferID SuitIntegrityStateBufferId = (BufferID)ShinobuSuitIntegrityVaultContract.StateBufferId;
        private const float BleedingBaseDurationSeconds = 48f;
        private const float BleedingMaxDurationSeconds = 135f;
        private const float BleedingBaseDamagePerSecond = 0.35f;
        private const float BleedingMaxDamagePerSecond = 1.65f;
        private const float FractureBaseDurationSeconds = 75f;
        private const float FractureMaxDurationSeconds = 210f;
        private const float FractureBasePenalty = 0.18f;
        private const float FractureMaxPenalty = 0.52f;
        private const float RapidAscentRiskDecayPerSecond = 0.38f;
        private const float NitrogenBaselinePressureAtm = 1f;
        private const float NitrogenTissueLoadHardCapAtm = 64f;
        private const float NitrogenTissueLoadBendsThresholdAtm = 11f;
        private const float NitrogenCriticalBuildUp = 100f;
        private const float NitrogenBuildUpHardCap = 160f;
        private const float DepthNarcosisStartMeters = 150f;
        private const float DepthNarcosisFullRangeMeters = 150f;
        private const float NarcosisPressureThresholdAtm = 1f + DepthNarcosisStartMeters * 0.1f;
        private const float NarcosisPressureFullRangeAtm = DepthNarcosisFullRangeMeters * 0.1f;
        private const float NarcosisShaderPublishEpsilon = 0.0005f;
        private const float NitrogenStaminaPenaltyMultiplier = 0.8f;
        private const float HypercapniaStaminaPenaltyMultiplier = 0.5f;
        private const float HypothermiaStaminaMultiplier = 0.2f;
        private const float NitrogenLoadWarningThreshold01 = 0.5f;
        private const float NitrogenLoadWarningResetThreshold01 = 0.35f;
        private const int NitrogenLoadNotificationRetryFrames = 30;
        private const float NitrogenRingingThreshold01 = 0.75f;
        private const string NitrogenLoadWarningMessage = "ASCENT RATE WARNING // NITROGEN LOAD";
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
        private const float NutritionalToxicitySignalDeltaScale = 0.04f;
        private const float SuitPunctureBleedDamageFraction = 0.30f;
        private const float HypothermiaFrostStartCelsius = 35f;
        private const float HypothermiaFrostFullCelsius = 28f;
        private const float DefaultInternalTemperatureCelsius = SaveData.PlayerEnvironmentTemperatureDefault;
        private const float ColdNutritionFullBoostRangeCelsius = 12f;
        private const float OxygenMovementScaleCeiling = 1.55f;
        private const float OxygenStressScaleCeilingBonus = 0.50f;
        private const float PsychoMetricsOxygenDrainScaleCeiling = 2.5f;
        private const uint PsychoMetricsOxygenSignalFreshFrames = 240u;
        private const float OxygenLeakScaleCeilingBonus = 0.70f;
        private const float OxygenCarryMassGraceKg = 18f;
        private const float OxygenCarryMassScaleCeilingBonus = 0.22f;
        private const float OxygenRebreatherDrainMultiplier = 0.70f;
        private const float OxygenGraceDurationSeconds = ShinobuMetabolismVaultContract.HypoxiaAgonyDurationSeconds;
        private const float OxygenGraceSpeedMultiplier = 1.2f;
        private const float BarotraumaOxygenDrainMaxMultiplier = 5f;
        private const float BarotraumaOxygenDrainHardClamp = 10f;
        private const uint PhysiologyHandleRetryFrames = 30u;
        private const float OverpressureSeverityFullRangeMeters = 150f;
        private const float OverpressureSeveritySafeDepthScale = 0.35f;
        private const int SurvivalDatabaseRowCapacity = 256;
        private const int SurvivalDatabaseColumnCapacity = 16;
        private const int SurvivalBlackboxSnapshotCapacity = 1;
        private const int SurvivalBlackboxSnapshotSizeBytes = 64;
        private const uint SurvivalBlackboxSourceHash = 0x53555256u; // SURV
        private const uint SurvivalBlackboxFlagAlive = 1u << 0;
        private const uint SurvivalBlackboxFlagUnderwater = 1u << 1;
        private const uint SurvivalBlackboxFlagBeyondSafeDepth = 1u << 2;
        private const uint SurvivalBlackboxFlagOxygenGrace = 1u << 3;
        private const uint SurvivalBlackboxFlagBends = 1u << 4;
        private const uint SurvivalBlackboxFlagFreshPhysiology = 1u << 5;
        private const uint SurvivalBlackboxFlagNarcosis = 1u << 6;
        private const uint SurvivalBlackboxFlagToxicity = 1u << 7;
        private const uint SurvivalBlackboxFlagThermalStress = 1u << 8;
        private const uint SurvivalBlackboxFlagHasStats = 1u << 9;
        private const int SurvivalBlackboxDeathCauseShift = 24;
        private const string NativeMemoryOwner = nameof(HectonSurvivalSystem);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly int _MembraneTissueHashId = LocHash.Compute("Data_MembraneTissue");
        private static readonly uint _ThermalShockNonFiniteWarningHash = unchecked((uint)LocHash.Compute("Survival.ThermalShock.NonFinite"));
        private static readonly uint _AirPocketInvalidRefillWarningHash = unchecked((uint)LocHash.Compute("Survival.AirPocket.InvalidRefill"));
        private static readonly uint _SurvivalRuntimeContextHash = unchecked((uint)LocHash.Compute(nameof(HectonSurvivalSystem)));
        private static readonly uint _NitrogenLoadWarningMessageHash = unchecked((uint)LocHash.Compute(NitrogenLoadWarningMessage));
        private static readonly uint _NitrogenLoadNotificationMissWarningHash = unchecked((uint)LocHash.Compute("Survival.NitrogenLoad.NotificationMiss"));
        private static readonly uint _SurvivalVitalsQueueDropWarningHash = unchecked((uint)LocHash.Compute("Survival.Vitals.QueueDrop"));
        private static readonly uint _SurvivalVitalsQueueContextHash = unchecked((uint)LocHash.Compute("Survival.Vitals"));
        private static readonly uint _NutritionalToxicityChemicalHash = unchecked((uint)LocHash.Compute("Survival.NutritionalToxicity"));
        private static readonly uint _EnvironmentalToxicityChemicalHash = unchecked((uint)LocHash.Compute("Survival.EnvironmentalToxicity"));
        private static readonly int _NarcosisScalarShaderId = Shader.PropertyToID("_HectonNarcosisScalar");

        private const float Epsilon       = 0.1f;
        private const float DirtySentinel = -9999f;

        // ---------------------------------------------------------
        //  PUBLIC EVENTS
        // ---------------------------------------------------------

        // ---------------------------------------------------------
        //  PROPERTIES
        // ---------------------------------------------------------

        public float Oxygen              => SafeNonNegative(oxygen);
        public float Energy              => SafeNonNegative(energy);
        public float Depth               => SafeNonNegative(depth);
        public float Integrity           => SafeNonNegative(integrity);
        public float Pressure            => FiniteAtLeast(pressure, 1f, 1f);
        public float Weight              => SafeNonNegative(weight);
        public float Hunger              => SafeNonNegative(hunger);
        public float Thirst              => SafeNonNegative(thirst);
        public bool  IsAlive             => alive;
        internal bool RespawnReconciliationPending => _pendingRespawnReconciliationSequence != 0u;
        public SurvivalStats Stats       => stats;

        public float OxygenNormalized    => ResolveSafeRatio01(oxygen, ResolveRuntimeMaxOxygenCapacity());
        public float EnergyNormalized    => stats != null ? ResolveSafeRatio01(energy, stats.MaxEnergy) : 0f;
        public float IntegrityNormalized => stats != null ? ResolveSafeRatio01(integrity, stats.MaxIntegrity) : 0f;
        public float HungerNormalized    => stats != null ? ResolveSafeRatio01(hunger, stats.MaxHunger) : 0f;
        public float ThirstNormalized    => stats != null ? ResolveSafeRatio01(thirst, stats.MaxThirst) : 0f;
        public float EnergyPercent       => EnergyNormalized * 100f;
        public float HungerPercent       => HungerNormalized * 100f;
        public float ThirstPercent       => ThirstNormalized * 100f;
        public SurvivalDeathCause LastDeathCause => _lastDeathCause;
        /// <summary>Total elapsed time for the currently active life.</summary>
        public double CurrentLifeDurationSeconds => SafeNonNegative(_currentLifeDurationSeconds);
        /// <summary>Deepest reached depth for the currently active life.</summary>
        public double CurrentLifePeakDepthMeters => SafeNonNegative(_currentLifePeakDepthMeters);
        /// <summary>True when a persisted last-loss marker record is available.</summary>
        public bool HasLastDeathRecord => _hasLastDeathRecord;
        /// <summary>World-space marker position for the latest recorded death.</summary>
        public Vector3 LastDeathMarkerPosition => _lastDeathRecord.Position;
        /// <summary>Latest persisted death telemetry record.</summary>
        public SurvivalDeathRecord LastDeathRecord => _lastDeathRecord;
        /// <summary>Signed margin to the authored safe depth. Negative values mean active overpressure.</summary>
        public float SafeDepthMarginMeters => stats != null ? ResolveEffectiveSafeDepthMeters() - SafeNonNegative(depth) : 0f;
        /// <summary>Positive metres beyond the safe depth envelope.</summary>
        public float OverpressureMeters => stats != null ? SafeNonNegative(SafeNonNegative(depth) - ResolveEffectiveSafeDepthMeters()) : 0f;
        /// <summary>True when the suit is already deeper than its safe depth rating.</summary>
        public bool IsBeyondSafeDepth => OverpressureMeters > 0f;
        /// <summary>Current integrity attrition per second caused by overpressure.</summary>
        public float PressureDamagePerSecond => ResolveCurrentPressureDamagePerSecond();
        /// <summary>Normalized live overpressure severity for advisory systems.</summary>
        public float PressureExposureSeverity01 => ResolvePressureExposureSeverity01();
        /// <summary>True while the player is actively bleeding.</summary>
        public bool IsBleeding => HasCachedCombatStatusEffect(CombatStatusBits.Bleeding64);
        /// <summary>True while the player is carrying a fracture movement penalty.</summary>
        public bool HasFracture => HasCachedCombatStatusEffect(CombatStatusBits.Fractured64);
        /// <summary>Combined live injury flags for UI and progression systems.</summary>
        internal PlayerInjuryStatus CurrentInjuries => ResolveCurrentInjuries();
        /// <summary>Normalized severity of the active bleeding state.</summary>
        public float BleedingSeverity01 => IsBleeding ? math.max(0.1f, SafeSaturate(_bleedingSeverity01)) : 0f;
        /// <summary>Normalized fracture penalty currently applied to swim mobility.</summary>
        public float FracturePenalty01 => HasFracture ? SafeSaturate(_fracturePenalty01) : 0f;
        /// <summary>Resolved environment temperature after local thermal hazards are added.</summary>
        public float EnvironmentTemperature => math.select(DefaultInternalTemperatureCelsius, _environmentTemperature, math.isfinite(_environmentTemperature));
        /// <summary>Current internal suit temperature after exponential thermal convergence.</summary>
        public float InternalTemperature => math.select(DefaultInternalTemperatureCelsius, _internalTemperature, math.isfinite(_internalTemperature));
        /// <summary>True while cold stress is actively affecting the suit and body.</summary>
        public bool IsInColdStress => _thermalStressMode == ThermalStressMode.Cold;
        /// <summary>True while heat stress is actively affecting the suit and body.</summary>
        public bool IsInHeatStress => _thermalStressMode == ThermalStressMode.Heat;
        /// <summary>Resolved thermal stress mode currently applied to the player.</summary>
        internal ThermalStressMode CurrentThermalStressMode => _thermalStressMode;
        /// <summary>Normalized cold-stress severity for advisory systems.</summary>
        public float ColdStressSeverity01 => SafeSaturate(_coldSeverity01);

        public bool TryGetSurvivalEnvironmentSnapshot(out PlayerSurvivalEnvironmentSnapshot snapshot)
        {
            snapshot = default;
            float environmentTemperature = math.select(
                DefaultInternalTemperatureCelsius,
                _environmentTemperature,
                math.isfinite(_environmentTemperature));
            float depthMeters = math.select(0f, depth, math.isfinite(depth) && depth > 0f);

            snapshot.EnvironmentTemperatureCelsius = environmentTemperature;
            snapshot.DepthMeters = depthMeters;
            snapshot.Flags = (uint)PlayerRuntimeSnapshotFlags.HasSurvival;
            return true;
        }

        /// <summary>Normalized heat-stress severity for advisory systems.</summary>
        public float HeatStressSeverity01 => SafeSaturate(_heatSeverity01);
        /// <summary>Highest normalized thermal-stress severity currently active.</summary>
        public float ThermalStressSeverity01 => math.max(ColdStressSeverity01, HeatStressSeverity01);
        /// <summary>Normalized presentation risk derived from rapid ascent and SHINOBU physiology snapshots.</summary>
        internal float RapidAscentRisk01 => SafeSaturate(_decompressionRisk01);
        /// <summary>Compatibility mirror of SHINOBU physiology risk; legacy scalar nitrogen accumulation is disabled.</summary>
        public float NitrogenBuildUp => SafeNonNegative(_nitrogenBuildUp);
        /// <summary>SHINOBU physiology nitrogen load mirror in atmosphere units.</summary>
        public float NitrogenLoad => SafeNonNegative(_nitrogenLoad);
        /// <summary>Normalized SHINOBU physiology load against the presentation threshold.</summary>
        public float NitrogenLoad01 => SafeSaturate(_nitrogenLoad * math.rcp(NitrogenTissueLoadBendsThresholdAtm));
        public int NitrogenLoadNotificationMissCount => _nitrogenLoadNotificationMissCount;
        /// <summary>Normalized SHINOBU physiology mirror against the legacy presentation threshold.</summary>
        public float NitrogenBuildUp01 => SafeSaturate(_nitrogenBuildUp / NitrogenCriticalBuildUp);
        /// <summary>Pre-narcosis high-frequency ring intensity used by the helmet DSP layer.</summary>
        public float NitrogenWarningRinging01 => ResolveNitrogenWarningRinging01(_nitrogenBuildUp);
        /// <summary>True when cumulative nitrogen build-up has crossed the sickness threshold.</summary>
        public bool IsNitrogenNarcosisActive => _nitrogenNarcosis01 > 0.001f;
        /// <summary>Normalized narcosis severity used by visor and movement penalty systems.</summary>
        public float NitrogenNarcosis01 => SafeSaturate(_nitrogenNarcosis01);
        /// <summary>Normalized blur signal emitted by nitrogen sickness.</summary>
        public float NitrogenNarcosisVisionBlur01 => NitrogenNarcosis01;
        /// <summary>Fixed condition bits for UI and medical item clearing.</summary>
        public uint StatusMask => _statusMask;
        /// <summary>Composite body toxicity scalar from hazards, nutrition and radiation exposure.</summary>
        public float Toxicity01 => SafeSaturate(_toxicity01);
        /// <summary>True while the oxygen-depletion grace pulse is suppressing immediate death.</summary>
        public bool IsOxygenGraceActive => _oxygenGraceActive;
        /// <summary>Normalized vision-blur pulse emitted during the oxygen grace window.</summary>
        public float OxygenGraceVisionBlur01 => SafeSaturate(_oxygenGraceVisionBlur01);

        // ---------------------------------------------------------
        //  LIFECYCLE
        // ---------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            Volatile.Write(ref s_x001HectonSurvivalSystemSignalPushDropCount, 0);
        }

        private void Awake()
        {
            if (stats == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[HectonSurvival] SurvivalStats not assigned. Disabling.", this);
#endif
                enabled = false;
                return;
            }

            ResolveRuntimeContextDependencies();
            RefreshColdRegistryReferences();
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
            ulong ownerEntityId = EntityId.ToULong(GetEntityId());
            int ownerId = unchecked((int)ownerEntityId);
            int statsId = stats != null ? unchecked((int)EntityId.ToULong(stats.GetEntityId())) : 0;
            _survivalVitalsSignalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(ownerEntityId);
            _playerEntityHash = unchecked((uint)ownerEntityId);
            _traumaRandom = CreateDeterministicRandom(ownerId, statsId);
#if UNITY_EDITOR
            TryBootstrapInjectedSurvivalDatabase();
#endif
            NotificationEvents.RegisterMessage(NitrogenLoadWarningMessage.AsSpan());
            ResetToMax();
            PublishRuntimeContextState();
        }

        private void OnEnable()
        {
            ResolveRuntimeContextDependencies();
            RefreshColdRegistryReferences();
            RefreshSurvivalIdentityCold();
            TryRegisterHotSwapListener();
            TryRegisterTickOwners();
            EnsureSurvivalBlackboxSnapshot();
            _slowTickDt = 0.1f;

            RegisterBloodScentSignal();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterTickOwners();
            UnregisterBloodScentSignal();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            ResetOxygenGraceState();
            TryWriteMetabolicOxygenStateToVault(ResolveRealOxygen01(oxygen), 0f, 0, out _);
            ResetThermalState();
            ClearPendingRespawnReconciliation();
            ClearNitrogenLoadNotificationDiagnostics();
            DisposeSurvivalBlackboxSnapshot();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                TryUnregisterTickOwners();
                TryUnregisterSaveParticipant();
                TryUnregisterHotSwapListener();
            }

            ClearNitrogenLoadNotificationDiagnostics();
            ClearPendingRespawnReconciliation();
            DisposeSurvivalBlackboxSnapshot();
            DisposeInjectedSurvivalDatabase();
        }

        private void ResolveRuntimeContextDependencies()
        {
            if (!PlayerRuntimeContextService.TryBindPlayerRoot(gameObject, out PlayerRuntimeContext runtimeContext))
                return;

            _runtimeContext = runtimeContext;
            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            _playerMovement = runtimeContext.PlayerMovement;
            _playerTransportCoordinator = runtimeContext.PlayerTransportCoordinator;
            _traumaDispatcher = runtimeContext.TraumaDispatcher;
            _playerRigidbody = runtimeContext.PlayerRigidbody;
            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);
            _combatTargetId = ResolveCachedCombatTargetId();
        }

        private int ResolveCachedCombatTargetId()
        {
            if (_combatTargetId != 0)
                return _combatTargetId;

            _combatTargetId = _playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(_playerHealth.gameObject)
                : CombatDamageRuntime.ResolveTargetId(gameObject);
            return _combatTargetId;
        }

        private void RefreshColdRegistryReferences()
        {
            _atmosphereRuntime = GlobalRegistry.AtmosphereReadModel;
            _physicsService = GlobalRegistry.Physics;
            _saveService = GlobalRegistry.Save;
            _survivalDataVault = GlobalRegistry.DataVault;
            BindPhysiologyVaultHandles(_survivalDataVault);
            _thermalManager = GlobalRegistry.Thermodynamics;
            _modularEquipment = GlobalRegistry.ModularEquipment;
            _hazardZoneRuntime = GlobalRegistry.HazardZones;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private void TryRegisterTickOwners()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTickable)
                _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);

            if (!_registeredLateFrameTickable)
                _registeredLateFrameTickable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTickOwners()
        {
            if (_registeredSlowTickable)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTickable = false;
            }

            if (_registeredLateFrameTickable)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTickable = false;
            }

            _hasPendingNarcosisShaderScalar = false;
        }

        public void LateFrameTick()
        {
            ConsumeCommittedRespawnReconciliationSignals();
            FlushNarcosisShaderScalar();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _atmosphereRuntime = currentService as IAtmosphereReadModel;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsRuntime:
                    _thermalManager = currentService as AbyssalThermalManager;
                    break;
                case GlobalRegistryServiceSlot.ModularEquipment:
                    _modularEquipment = currentService as IModularEquipmentService;
                    break;
                case GlobalRegistryServiceSlot.HazardZoneRuntime:
                    _hazardZoneRuntime = currentService as HazardZoneManager;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    if (ReferenceEquals(_saveService, currentService))
                    {
                        TryRegisterSaveParticipant();
                        break;
                    }

                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _survivalDataVault = currentService as IDataVault;
                    BindPhysiologyVaultHandles(_survivalDataVault);
                    DisposeInjectedSurvivalDatabase();
                    if (_survivalDataVault != null)
                    {
#if UNITY_EDITOR
                        TryBootstrapInjectedSurvivalDatabase();
#endif
                    }
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterTickOwners();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterTickOwners();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
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
            survivalState.OxygenNormalized = OxygenNormalized;
            survivalState.EnergyNormalized = EnergyNormalized;
            survivalState.IntegrityNormalized = IntegrityNormalized;
            survivalState.PressureExposureSeverity01 = SafeSaturate(PressureExposureSeverity01);
            survivalState.ThermalStressSeverity01 = ThermalStressSeverity01;
            survivalState.HungerNormalized = HungerNormalized;
            survivalState.ThirstNormalized = ThirstNormalized;
            survivalState.OxygenGraceVisionBlur01 = OxygenGraceVisionBlur01;
            survivalState.ColdStressSeverity01 = ColdStressSeverity01;
            survivalState.HeatStressSeverity01 = HeatStressSeverity01;
            survivalState.RapidAscentRisk01 = RapidAscentRisk01;
            survivalState.NitrogenBuildUp01 = NitrogenBuildUp01;
            survivalState.NitrogenLoad01 = NitrogenLoad01;
            survivalState.NitrogenNarcosis01 = NitrogenNarcosis01;
            survivalState.Toxicity01 = Toxicity01;
            survivalState.CoreTemperatureCelsius = math.select(
                DefaultInternalTemperatureCelsius,
                _internalTemperature,
                math.isfinite(_internalTemperature));
            survivalState.RadiationDose = SafeNonNegative(_runtimeContext.RadiationDose);
            survivalState.RadiationIntensity01 = SafeSaturate(_runtimeContext.RadiationIntensity01);
            survivalState.RadiationMaxHealthPenalty01 = SafeSaturate(_runtimeContext.RadiationMaxHealthPenalty01);
            survivalState.StatusMask = _statusMask;
            survivalState.Flags = flags;
            _runtimeContext.PublishSurvivalState(in survivalState);
        }

        private void PublishHeadlessUIState()
        {
            float maxOxygen = stats != null ? math.max(0.01f, ResolveRuntimeMaxOxygenCapacity()) : 100f;
            float maxEnergy = stats != null ? math.max(0.01f, stats.MaxEnergy) : 100f;
            float maxIntegrity = stats != null ? math.max(0.01f, stats.MaxIntegrity) : 100f;
            float carryCapacityKg = stats != null ? math.max(0.01f, stats.CarryCapacityKg) : 200f;
            float uiTimestamp = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;

            UIStateStore.WriteHUDSurvivalState(
                ResolveSafeRatio01(oxygen, maxOxygen),
                ResolveSafeRatio01(energy, maxEnergy),
                ResolveSafeRatio01(integrity, maxIntegrity),
                SafeNonNegative(depth),
                FiniteAtLeast(pressure, 1f, 1f),
                ResolveEffectiveSafeDepthMeters(),
                SafeNonNegative(oxygen),
                SafeNonNegative(energy),
                SafeNonNegative(integrity),
                SafeNonNegative(weight),
                carryCapacityKg,
                ResolveSafeRatio01(weight, carryCapacityKg),
                uiTimestamp);
            UIStateStore.WriteFrostIntensity(
                ResolveHypothermiaFrostIntensity01(_internalTemperature),
                uiTimestamp);
            UIStateStore.WriteSurvivalStatusMask(_statusMask, uiTimestamp);
        }

        // ---------------------------------------------------------
        //  TICK SYSTEMS
        // ---------------------------------------------------------

        internal static float ResolveHypothermiaFrostIntensity01(float internalTemperatureCelsius)
        {
            return Hecton8.PureLogic.Systems.HypothermiaShiverCurveCalculator.Compute(internalTemperatureCelsius, DefaultInternalTemperatureCelsius, HypothermiaFrostStartCelsius, HypothermiaFrostFullCelsius);
        }

        public void SlowTick()
        {
            ConsumeCommittedRespawnReconciliationSignals();
            if (!alive) return;

            float dt = _slowTickDt;
            _metabolicOxygenStateSyncedThisTick = false;

            ComputeDepthAndPressure();
            RefreshBloodScentSignal();
            TryApplyLocalizedOxygenPocket();
            TrackRapidAscentRisk(dt);
            TrackCurrentLifeTelemetry(dt);
            TrackPressureExposure(dt);
            PushPressureHullStress();
            RefreshCombatStatusMaskCache();
            UpdateOxygen(dt);
            ConsumeOxygenCriticalSignals();
            UpdateOxygenGraceState(dt);
            DrainPassiveEnergy(dt);
            ApplyPressureDamage(dt);
            UpdatePhysiologyScalars(dt);
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
            ApplyNitrogenMovementPenalty();
            RefreshSurvivalStatusMask();
            PublishRuntimeContextState();
            PublishHeadlessUIState();
            PublishDirty();
            CheckLethalConditions();
            WriteSurvivalBlackboxSnapshot();
        }

        // ---------------------------------------------------------
        //  SIMULATION STEPS
        // ---------------------------------------------------------

        private void ComputeDepthAndPressure()
        {
            if (_playerMovement != null)
            {
                float movementSurfaceY = _playerMovement.CurrentWaterSurfaceY;
                if (math.isfinite(movementSurfaceY))
                    surfaceWorldY = movementSurfaceY;

                float movementDepth = _playerMovement.CurrentDepth;
                depth = math.isfinite(movementDepth) ? math.max(0f, movementDepth) : 0f;
                pressure = 1f + depth * 0.1f;
                return;
            }

            depth    = math.max(0f, surfaceWorldY - ResolveSurvivalRuntimePosition().y);
            pressure = 1f + depth * 0.1f;
        }

        private void UpdateOxygen(float dt)
        {
            _surfaceContractUnderwater = ResolveSurfaceContractUnderwater();

            if (!_surfaceContractUnderwater)
            {
                bool surfaceLockDenied = false;
                float surfaceNextOxygen = math.min(
                    ResolveRuntimeMaxOxygenCapacity(),
                    oxygen + surfaceOxygenRefillRate * dt);
                if (TryWriteMetabolicOxygenStateToVault(
                        ResolveRealOxygen01(surfaceNextOxygen),
                        0f,
                        0,
                        out surfaceLockDenied))
                {
                    _metabolicOxygenStateSyncedThisTick = true;
                }
                else if (surfaceLockDenied)
                {
                    return;
                }

                oxygen = surfaceNextOxygen;
                return;
            }

            if (_oxygenGraceActive)
            {
                oxygen = 0f;
                return;
            }

            bool wroteMetabolicState = false;
            bool oxygenDrainLockDenied = false;
            float oxygenDrainPerSecond = ResolveCurrentOxygenDrainPerSecond();
            float nextOxygen = math.max(0f, oxygen - oxygenDrainPerSecond * dt);
            byte nextHypoxiaState = (byte)math.select(0, 1, nextOxygen <= 0f);
            float nextAgonyTimeRemaining = ResolveStagedAgonyTimeRemaining(nextHypoxiaState);
            if (TryWriteMetabolicOxygenStateToVault(
                    ResolveRealOxygen01(nextOxygen),
                    nextAgonyTimeRemaining,
                    nextHypoxiaState,
                    out oxygenDrainLockDenied))
            {
                wroteMetabolicState = true;
            }
            else if (oxygenDrainLockDenied)
            {
                // If FrostTickDefrag owns the vault this frame, drop the oxygen sample instead of risking a stale pointer.
                return;
            }

            oxygen = nextOxygen;
            _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
        }

        private void ConsumeOxygenCriticalSignals()
        {
            ReadOnlySpan<OxygenCriticalSignal> signals = SignalBus<OxygenCriticalSignal>.GetFrameSnapshot();
            if (signals.Length <= 0 || stats == null)
                return;

            float maxOxygen = ResolveRuntimeMaxOxygenCapacity();
            float targetOxygen = oxygen;
            for (int i = 0; i < signals.Length; i++)
            {
                OxygenCriticalSignal signal = signals[i];
                float oxygen01 = math.saturate(math.select(0f, signal.Oxygen01, math.isfinite(signal.Oxygen01)));
                targetOxygen = math.min(targetOxygen, maxOxygen * oxygen01);
            }

            if (targetOxygen >= oxygen - Epsilon)
                return;

            float nextOxygen = math.max(0f, targetOxygen);
            byte nextHypoxiaState = (byte)math.select(0, 1, nextOxygen <= 0f);
            float nextAgonyTimeRemaining = ResolveStagedAgonyTimeRemaining(nextHypoxiaState);
            bool lockDenied = false;
            bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                ResolveRealOxygen01(nextOxygen),
                nextAgonyTimeRemaining,
                nextHypoxiaState,
                out lockDenied);
            if (lockDenied)
                return;

            oxygen = nextOxygen;
            _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
            ForceDirty(ref lastPubOxygen);
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
                        float movementDepth = _playerMovement.CurrentDepth;
                        return (math.isfinite(movementDepth) && movementDepth > 0.01f) ||
                               _playerMovement.IsPlayerSubmerged;

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
            float effortLoadMetabolicFactor = ResolveEffortLoadEnergyMetabolicMultiplier();
            float temperatureAdjustedConsumptionRate = stats.EnergyConsumptionRate * Hecton8.PureLogic.Systems.SuitBatteryThermalEfficiencyCalculator.Compute(_environmentTemperature, stats.EnergyConsumptionRate);
            energy = math.max(0f, energy - temperatureAdjustedConsumptionRate * effortLoadMetabolicFactor * dt);
        }

        private void ApplyPressureDamage(float dt)
        {
            _ = dt;
            // SHINOBU_323 owns pressure-collapse authority through SuitIntegrityDTO and CombatDamageSignal.
            // This legacy surface remains read-only for warning/UI scalars and must not mutate player integrity.
        }

        private void UpdatePhysiologyScalars(float dt)
        {
            _toxicity01 = ResolveBodyToxicity01(ResolveHazardIntensity(HazardType.Toxicity));

            float movementStaminaDrain =
                math.max(0f, _movementIntentLengthSq) *
                math.max(0f, ResolveMovementStaminaDrainPerSecond()) *
                math.max(0f, dt);
            if (movementStaminaDrain > 0f)
                DrainEnergy(movementStaminaDrain);

            RefreshNitrogenNarcosisRuntimeState();
            RefreshSurvivalStatusMask();
        }

        private float ResolveMovementStaminaDrainPerSecond()
        {
            if (stats == null)
                return 0f;

            return math.max(0f, stats.EnergyConsumptionRate) *
                   math.max(0f, _movementStaminaDrainMultiplier) *
                   ResolveEffortLoadEnergyMetabolicMultiplier();
        }

        private void RefreshSurvivalStatusMask(uint seedStatusMask = 0u)
        {
            uint status = seedStatusMask;
            status |= math.select(
                0u,
                SurvivalStatusMasks.Bends,
                _physiologyBendsActive);
            status |= math.select(0u, SurvivalStatusMasks.Freezing, _internalTemperature < HypothermiaFrostStartCelsius);
            status |= math.select(0u, SurvivalStatusMasks.Starving, HungerNormalized <= 0.0001f);
            status |= math.select(0u, SurvivalStatusMasks.Dehydrated, ThirstNormalized <= 0.0001f);
            status |= math.select(0u, SurvivalStatusMasks.Narcosis, _nitrogenNarcosis01 > 0.0001f);
            status |= math.select(0u, SurvivalStatusMasks.Toxicity, _toxicity01 > 0.0001f);
            status |= math.select(0u, SurvivalStatusMasks.CrushWarning, PressureExposureSeverity01 > 0.0001f);
            status |= math.select(0u, SurvivalStatusMasks.RadiationPenalty, _runtimeContext.RadiationMaxHealthPenalty01 > 0.0001f);
            _statusMask = status;
        }

        private void HandleTemperature(float dt)
        {
            IAtmosphereReadModel atmosphere = _atmosphereRuntime;
            float baseTemp = atmosphere != null ? atmosphere.CurrentTemperature : 20f;
            Vector3 survivalRuntimePosition = ResolveSurvivalRuntimePosition();
            float localHeat = ResolveHazardIntensity(HazardType.Heat);
            float abyssalColdPenalty = ResolveAbyssalColdPenaltyCelsius();
            float hazardTemperature = baseTemp + localHeat - abyssalColdPenalty;
            _environmentTemperature = ResolveExternalThermalShockTemperature(
                hazardTemperature,
                ResolveAbyssalThermalExternalTemperature(survivalRuntimePosition));
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
                PublishSurvivalVitalsChanged(SurvivalVitalsChangedSignalFlags.Thermal);
                return;
            }

            float deepColdStressMultiplier = ResolveDeepColdPocketStressMultiplier();
            _thermalStressMode = coldExcess > 0f ? ThermalStressMode.Cold : ThermalStressMode.Heat;
            _coldSeverity01 = ResolveThermalSeverity01(coldExcess);
            _heatSeverity01 = ResolveThermalSeverity01(heatExcess);
            _tempGraceTimer += dt;
            PublishSurvivalVitalsChanged(SurvivalVitalsChangedSignalFlags.Thermal);
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
            AbyssalThermalManager thermalManager = _thermalManager;
            if (thermalManager == null ||
                !thermalManager.SampleThermalFlow(worldPosition, 1.1f, out AbyssalThermalManager.ThermalFlowSample sample) ||
                !math.isfinite(sample.Heat01) ||
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
            IModularEquipmentService equipmentService = _modularEquipment;
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

            if (_vegetationBridge == null)
                return 1f;

            return FiniteAtLeast(_vegetationBridge.GetDeepColdStressMultiplier(ResolveSurvivalRuntimePosition()), 1f, 1f);
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

            if (!_physiologyBendsActive)
                _decompressionRisk01 = math.max(0f, _decompressionRisk01 - RapidAscentRiskDecayPerSecond * deltaTime);
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

        internal static bool ShouldApplyImmediateDecompressionDamage(float ascentMetersPerSecond, float ascentOriginDepthMeters)
        {
            // SHINOBU_321/X_009: decompression damage authority lives in ShinobuPhysiologyRuntime's 3-tissue Vault model.
            return false;
        }

        internal static bool ShouldApplyBendsDamage(float ascentMetersPerSecond, float nitrogenLoad)
        {
            _ = ascentMetersPerSecond;
            _ = nitrogenLoad;
            return false;
        }

        internal static float ResolveNitrogenTissueLoad(float currentLoad, float ambientPressure, float deltaTime)
        {
            _ = ambientPressure;
            _ = deltaTime;
            float safeCurrent = math.select(
                NitrogenBaselinePressureAtm,
                currentLoad,
                math.isfinite(currentLoad) && currentLoad > 0f);
            return math.clamp(safeCurrent, NitrogenBaselinePressureAtm, NitrogenTissueLoadHardCapAtm);
        }

        internal static float ResolvePressureNarcosis01(float ambientPressure)
        {
            _ = ambientPressure;
            return 0f;
        }

        internal static float ResolveColdNutritionDrainMultiplier(
            float ambientTemperatureCelsius,
            float safeMinimumCelsius,
            float fullBoostRangeCelsius)
        {
            if (!math.isfinite(ambientTemperatureCelsius) || !math.isfinite(safeMinimumCelsius))
                return 1f;

            float cold01 = math.saturate(
                (safeMinimumCelsius - ambientTemperatureCelsius) *
                math.rcp(math.max(0.01f, fullBoostRangeCelsius)));
            return math.lerp(1f, 2f, cold01);
        }

        internal static float ResolveImmediateDecompressionSeverity01(float ascentMetersPerSecond, float ascentOriginDepthMeters)
        {
            _ = ascentMetersPerSecond;
            _ = ascentOriginDepthMeters;
            return 0f;
        }

        internal static float ResolveDepthNarcosis01(float depthMeters, bool isHardenedSubmarine)
        {
            if (isHardenedSubmarine || !math.isfinite(depthMeters) || depthMeters <= DepthNarcosisStartMeters)
                return 0f;

            return math.saturate(
                (depthMeters - DepthNarcosisStartMeters) /
                math.max(0.01f, DepthNarcosisFullRangeMeters));
        }

        internal static float ResolveNitrogenWarningRinging01(float nitrogenBuildUp)
        {
            float buildUp01 = SafeSaturate(nitrogenBuildUp / NitrogenCriticalBuildUp);
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

        private void RefreshNitrogenNarcosisRuntimeState()
        {
            TryApplyPhysiologyAuthoritySnapshot();
            if (_playerMovement != null)
                _playerMovement.SetRuntimeNarcosisInputNoise(_nitrogenNarcosis01);
            QueueNarcosisShaderScalar(_nitrogenNarcosis01);
        }

        private bool TryApplyPhysiologyAuthoritySnapshot()
        {
            if (!TryGetFreshShinobuPhysiologySignal(out PhysiologyStateSignal signal))
            {
                ClearPhysiologyAuthoritySnapshot();
                return false;
            }

            float nitrogenLoad = math.max(0f, signal.NitrogenLoadAtm);
            if (math.isfinite(nitrogenLoad) && nitrogenLoad > 0f)
                _nitrogenLoad = math.clamp(nitrogenLoad, NitrogenBaselinePressureAtm, NitrogenTissueLoadHardCapAtm);

            _nitrogenNarcosis01 = math.saturate(math.select(0f, signal.Narcosis01, math.isfinite(signal.Narcosis01)));
            float supersaturation = math.saturate(math.select(0f, signal.Supersaturation01, math.isfinite(signal.Supersaturation01)));
            _physiologyBendsActive = signal.Cause == PhysiologyStateSignal.CauseDecompression &&
                                     (signal.TissueOverMValueMask != 0u || supersaturation > 0.0001f);
            if (_physiologyBendsActive)
                _decompressionRisk01 = math.max(_decompressionRisk01, supersaturation);

            _nitrogenBuildUp = math.saturate(math.max(_nitrogenNarcosis01, supersaturation)) * NitrogenCriticalBuildUp;
            UpdateNitrogenPreNarcosisWarningState();
            return true;
        }

        private bool TryGetFreshShinobuPhysiologySignal(out PhysiologyStateSignal result)
        {
            result = default;
            uint currentFrame = TimeSliceScheduler.CurrentFrameId;
            uint bestFrameDelta = uint.MaxValue;
            bool found = false;

            ReadOnlySpan<PhysiologyStateSignal> signals = SignalBus<PhysiologyStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysiologyStateSignal signal = signals[i];
                if (!IsFreshShinobuPhysiologySignal(in signal, currentFrame, out uint frameDelta))
                    continue;

                if (found && frameDelta >= bestFrameDelta)
                    continue;

                result = signal;
                bestFrameDelta = frameDelta;
                found = true;
            }

            if (found)
            {
                _cachedShinobuPhysiologySignal = result;
                _hasCachedShinobuPhysiologySignal = true;
                return true;
            }

            if (_hasCachedShinobuPhysiologySignal &&
                IsFreshShinobuPhysiologySignal(in _cachedShinobuPhysiologySignal, currentFrame, out _))
            {
                result = _cachedShinobuPhysiologySignal;
                return true;
            }

            return false;
        }

        private static bool IsFreshShinobuPhysiologySignal(
            in PhysiologyStateSignal signal,
            uint currentFrame,
            out uint frameDelta)
        {
            frameDelta = unchecked(currentFrame - signal.Frame);
            return signal.SourceHash == PhysiologyStateSignal.SourceShinobuPhysiology &&
                   signal.Frame != 0u &&
                   frameDelta <= PsychoMetricsOxygenSignalFreshFrames;
        }

        private void ClearPhysiologyAuthoritySnapshot()
        {
            _physiologyBendsActive = false;
            _decompressionRisk01 = 0f;
            _nitrogenNarcosis01 = 0f;
            _nitrogenBuildUp = 0f;
            _nitrogenLoad = NitrogenBaselinePressureAtm;
            _nitrogenLoadWarningIssued = false;
            _nitrogenLoadNotificationRetryFrame = 0;
            _cachedShinobuPhysiologySignal = default;
            _hasCachedShinobuPhysiologySignal = false;
        }

        private void QueueNarcosisShaderScalar(float scalar01)
        {
            float clamped = math.saturate(scalar01);
            if (math.abs(_lastPublishedNarcosisShaderScalar - clamped) <= NarcosisShaderPublishEpsilon)
                return;

            _pendingNarcosisShaderScalar = clamped;
            _hasPendingNarcosisShaderScalar = true;
        }

        private void FlushNarcosisShaderScalar()
        {
            if (!_hasPendingNarcosisShaderScalar)
                return;

            _hasPendingNarcosisShaderScalar = false;
            float clamped = _pendingNarcosisShaderScalar;
            Shader.SetGlobalFloat(_NarcosisScalarShaderId, clamped);
            _lastPublishedNarcosisShaderScalar = clamped;
        }

        private bool IsInHardenedSubmarine()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null &&
                   transportPreset.OccupancyMode == PlayerTransportOccupancyMode.EnclosedCabin;
        }

        private void ApplyNitrogenMovementPenalty()
        {
            if (_playerMovement == null)
                return;

            float nitrogenStaminaMultiplier = math.lerp(1f, NitrogenStaminaPenaltyMultiplier, NitrogenNarcosis01);
            float hypothermiaMultiplier = _internalTemperature < HypothermiaFrostStartCelsius
                ? HypothermiaStaminaMultiplier
                : 1f;
            _playerMovement.SetRuntimeStaminaMultiplier(math.min(math.min(nitrogenStaminaMultiplier, _toxicityStaminaMultiplier), hypothermiaMultiplier));
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

            float nextOxygen = math.max(
                oxygen,
                ResolveRuntimeMaxOxygenCapacity() * math.max(0.01f, oxygenRefillFraction));
            if (nextOxygen > oxygen + Epsilon)
            {
                bool lockDenied = false;
                bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                    ResolveRealOxygen01(nextOxygen),
                    0f,
                    0,
                    out lockDenied);
                if (lockDenied)
                    return;

                oxygen = nextOxygen;
                _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
                ResetOxygenGraceState();
            }

            RefreshNitrogenNarcosisRuntimeState();
            ApplyNitrogenMovementPenalty();
            ForceDirty(ref lastPubOxygen);
        }

        private void UpdateNitrogenPreNarcosisWarningState()
        {
            float buildUp01 = NitrogenBuildUp01;
            if (buildUp01 < NitrogenLoadWarningResetThreshold01)
            {
                _nitrogenLoadWarningIssued = false;
                _nitrogenLoadNotificationRetryFrame = 0;
                return;
            }

            if (_nitrogenLoadWarningIssued || buildUp01 < NitrogenLoadWarningThreshold01)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_nitrogenLoadNotificationRetryFrame > frame)
                return;

            if (NotificationEvents.TryPushRegisteredWarning(_NitrogenLoadWarningMessageHash))
            {
                _nitrogenLoadWarningIssued = true;
                _nitrogenLoadNotificationRetryFrame = 0;
                return;
            }

            _nitrogenLoadNotificationRetryFrame = frame + NitrogenLoadNotificationRetryFrames;
            ReportNitrogenLoadNotificationMiss();
        }

        private void ReportNitrogenLoadNotificationMiss()
        {
            _nitrogenLoadNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _NitrogenLoadNotificationMissWarningHash,
                _SurvivalRuntimeContextHash,
                math.max(1, _nitrogenLoadNotificationMissCount));
        }

        private void ClearNitrogenLoadNotificationDiagnostics()
        {
            _nitrogenLoadNotificationRetryFrame = 0;
            _nitrogenLoadNotificationMissCount = 0;
        }

        private bool TrySamplePlayerAupAirPocket(out float oxygenRefillFraction)
        {
            Vector3 center = ResolveSurvivalRuntimePosition();
            if (HectonVoxelEngine.TrySampleAirPocket(center, out oxygenRefillFraction))
                return true;

            Vector3 up = Vector3.up;
            float halfHeight = 0.9f;
            if (HectonVoxelEngine.TrySampleAirPocket(center + up * halfHeight, out oxygenRefillFraction))
                return true;

            return HectonVoxelEngine.TrySampleAirPocket(center - up * halfHeight, out oxygenRefillFraction);
        }

        private void HandleDecompressionSicknessVomit(float dt)
        {
            if (_decompressionVomitToolDropCooldown > 0f)
                _decompressionVomitToolDropCooldown = math.max(0f, _decompressionVomitToolDropCooldown - math.max(0f, dt));

            float severity01 = RapidAscentRisk01;
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

            if (TryResolveEffortLoadSnapshot(out PlayerEffortLoadRuntimeState state))
            {
                if (Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ShouldTriggerCriticalStaminaFailure(
                        state.LoadRatio,
                        state.Stamina01,
                        state.CriticalEncumbranceRatio,
                        state.CriticalStaminaFailureThreshold01))
                {
                    _playerMovement.TriggerCriticalStaminaFailure();
                }

                return;
            }

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
            // SHINOBU_321: legacy velocity-based bends signal is disabled to prevent double authority.
            return;
        }

        private void HandleRadiation(float dt)
        {
            IAtmosphereReadModel atmosphere = _atmosphereRuntime;
            float baseRad = atmosphere != null ? atmosphere.CurrentRadiation : 0f;

            float currentRad = baseRad;

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
            float rawRadiationLevel = excess * stats.RadiationDamageRate * 3600f;
            float leadThicknessCm = radiationExposureScale > 0f ? (float)-Math.Log(radiationExposureScale) : float.PositiveInfinity;
            float shieldingQuality = 1f;
            float shieldedRate = Hecton8.PureLogic.Systems.RadiationLeadShieldingCalculator.Compute(rawRadiationLevel, leadThicknessCm, shieldingQuality);
            float dose = Hecton8.PureLogic.Systems.RadiationDoseAccumulator.Calculate(0f, shieldedRate, 0f, dt);
            if (TryResolveSurvivalAup(out AbsoluteUniversePosition radiationAup))
                RadiationHazardGrid.ReportExternalDose(dose, math.saturate(currentRad), in radiationAup);
            else
                RadiationHazardGrid.ReportExternalDose(dose, math.saturate(currentRad), ResolveSurvivalRuntimePosition());
        }

        private void HandleToxicity(float dt)
        {
            float toxicity = ResolveHazardIntensity(HazardType.Toxicity);
            _toxicity01 = ResolveBodyToxicity01(toxicity);
            if (toxicity <= 0.001f)
            {
                ClearToxicityStaminaPenalty();
                return;
            }

            _toxicityStaminaMultiplier = math.lerp(
                1f,
                HypercapniaStaminaPenaltyMultiplier,
                math.saturate(toxicity));
            ApplyNitrogenMovementPenalty();

            if (_hazardZoneRuntime != null)
                return;

            float toxicityExposureScale = ResolveTransportRadiationExposureScale();
            if (toxicityExposureScale <= 0f)
                return;

            PublishEnvironmentalToxicityStatus(toxicity, toxicityExposureScale, dt);
        }

        private void ClearToxicityStaminaPenalty()
        {
            if (math.abs(_toxicityStaminaMultiplier - 1f) <= 0.0001f)
                return;

            _toxicityStaminaMultiplier = 1f;
            ApplyNitrogenMovementPenalty();
        }

        private void HandleNutritionalToxicity(float dt)
        {
            _ = dt;
            _toxicity01 = ResolveBodyToxicity01(ResolveHazardIntensity(HazardType.Toxicity));
        }

        private float ResolveBodyToxicity01(float hazardToxicity01)
        {
            float hazard01 = SafeSaturate(hazardToxicity01);
            float poison01 = SafeSaturate(ResolvePoisonStatus01());
            float radiationToxicity01 = _playerHealth != null ? SafeSaturate(_playerHealth.RadiationExposure) : 0f;
            return math.max(hazard01, math.max(poison01, radiationToxicity01));
        }

        private float ResolvePoisonStatus01()
        {
            return HasCachedCombatStatusEffect(CombatStatusBits.Poisoned64)
                ? 1f
                : 0f;
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
                _cachedUpgradeTransportOwner = null;
                _cachedVehicleUpgradeModule = null;
                return null;
            }

            if (ReferenceEquals(_cachedUpgradeTransportOwner, lifecycleOwner))
                return _cachedVehicleUpgradeModule;

            MonoBehaviour transportBehaviour = lifecycleOwner as MonoBehaviour;
            if (!PlayerTransportLifecycleRegistry.TryGetRegistered(
                    lifecycleOwner,
                    transportBehaviour,
                    out _,
                    out _,
                    out _,
                    out _,
                    out VehicleUpgradeModule upgradeModule,
                    out _,
                    out _))
            {
                upgradeModule = null;
            }

            _cachedUpgradeTransportOwner = lifecycleOwner;
            _cachedVehicleUpgradeModule = upgradeModule;
            return _cachedVehicleUpgradeModule;
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
                ? SafeNonNegative(stats.SafeDepth + ResolveTransportSafeDepthBonusMeters())
                : 0f;
        }

        private float ResolveTransportOxygenConsumptionScale()
        {
            PlayerTransportPreset transportPreset = ResolveActiveTransportPreset();
            return transportPreset != null
                ? transportPreset.OxygenConsumptionScale
                : 1f;
        }

        /// <summary>
        /// Combined transport + upgrade pressure damage transfer scale. Lower means a better-rated
        /// hull. Survival owns this fact because it already resolves the active preset and the
        /// active <see cref="VehicleUpgradeModule"/> behind lifecycle-owner caching. Crush-depth
        /// hull stress in <c>HectonPlayerMovement.UpdateHullStress</c> reads it from here rather
        /// than resolving a second copy of the same chain.
        /// </summary>
        internal float TransportPressureDamageScale => ResolveTransportPressureDamageScale();

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
            float equipmentFactor = ResolveOxygenRebreatherScale();
            float barotraumaFactor = ResolveBarotraumaOxygenDrainMultiplier();

            float effortLoadMetabolicFactor = ResolveEffortLoadOxygenMetabolicMultiplier();
            float pureO2Drain = Hecton8.PureLogic.Systems.SurvivalSuitOxygenBurnRate.Calculate(baseRate, movementFactor, pressureFactor);

            return pureO2Drain * stressFactor * leakFactor * carryMassFactor * equipmentFactor * barotraumaFactor * effortLoadMetabolicFactor;
        }

        private float ResolveEffortLoadEnergyMetabolicMultiplier()
        {
            return TryResolveEffortLoadSnapshot(out PlayerEffortLoadRuntimeState state)
                ? Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeEnergyMetabolicMultiplier(
                    state.Load01,
                    state.MovementIntent01,
                    state.MovementStaminaDrainMultiplier,
                    state.UpwardSwimMultiplier,
                    (state.Flags & (uint)PlayerEffortLoadRuntimeFlags.Sprinting) != 0u,
                    (state.Flags & (uint)PlayerEffortLoadRuntimeFlags.Submerged) != 0u,
                    Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultMaximumEnergyMetabolicMultiplier)
                : 1f;
        }

        private float ResolveEffortLoadOxygenMetabolicMultiplier()
        {
            return TryResolveEffortLoadSnapshot(out PlayerEffortLoadRuntimeState state)
                ? Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeOxygenMetabolicMultiplier(
                    state.Load01,
                    state.MovementIntent01,
                    state.UpwardSwimMultiplier,
                    (state.Flags & (uint)PlayerEffortLoadRuntimeFlags.Sprinting) != 0u,
                    (state.Flags & (uint)PlayerEffortLoadRuntimeFlags.Submerged) != 0u,
                    Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultMaximumOxygenMetabolicMultiplier)
                : 1f;
        }

        private bool TryResolveEffortLoadSnapshot(out PlayerEffortLoadRuntimeState state)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.TryGetEffortLoadRuntimeState(out state))
                return true;

            state = default;
            return false;
        }

        private float ResolveOxygenRebreatherScale()
        {
            if (_runtimeContext == null ||
                _runtimeContext.ToolManager == null ||
                _runtimeContext.ToolManager.CurrentTool == null)
            {
                return 1f;
            }

            PlayerTool currentTool = _runtimeContext.ToolManager.CurrentTool;
            IModularEquipmentService equipmentService = _modularEquipment;
            if (equipmentService == null ||
                currentTool.RuntimeToolId == 0u ||
                !equipmentService.HasUpgrade(currentTool.RuntimeToolId, ToolUpgradeBits.OxygenRebreather))
            {
                return 1f;
            }

            return OxygenRebreatherDrainMultiplier;
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
            float ambientPressureAtm = math.isfinite(pressure) && pressure > 0f
                ? pressure
                : 1f + SafeNonNegative(depth) * 0.1f;
            return math.clamp(ambientPressureAtm, 1f, 16f);
        }

        private float ResolveOxygenMovementScale()
        {
            float authoredCruiseSpeed = FiniteAtLeast(ResolveAuthoredCruiseSpeedMetersPerSecond(), 1f, 0.01f);
            float authoredCruiseSpeedSq = FiniteAtLeast(authoredCruiseSpeed * authoredCruiseSpeed, 1f, 0.0001f);
            float move01 = math.saturate(ResolveCurrentMovementSpeedSq() / authoredCruiseSpeedSq);
            return math.lerp(1f, OxygenMovementScaleCeiling, move01);
        }

        private float ResolveOxygenStressScale()
        {
            float survivalStressScale = ResolveHeartrateOxygenMultiplier(ResolveOxygenStressMagnitude01());
            float psychoMetricsScale = ResolvePsychoMetricsOxygenDrainScale();
            return math.max(survivalStressScale, psychoMetricsScale);
        }

        private float ResolvePsychoMetricsOxygenDrainScale()
        {
            if (!TryGetFreshShinobuPhysiologySignal(out PhysiologyStateSignal signal))
                return 1f;

            float multiplier = signal.O2DrainMultiplier;
            return math.isfinite(multiplier)
                ? math.clamp(multiplier, 1f, PsychoMetricsOxygenDrainScaleCeiling)
                : 1f;
        }

        private float ResolveOxygenLeakScale()
        {
            float suitLeakScale = 1f + (1f - IntegrityNormalized) * OxygenLeakScaleCeilingBonus;
            float vehicleLeakScale = _traumaDispatcher != null
                ? _traumaDispatcher.AdditionalVehicleOxygenDrainScale
                : 1f;
            return suitLeakScale * vehicleLeakScale;
        }

        private float ResolveBarotraumaOxygenDrainMultiplier()
        {
            return TryReadSuitCrushDepthIntegrity01(out float crushDepthIntegrity01)
                ? ResolveBarotraumaOxygenDrainMultiplier(crushDepthIntegrity01)
                : 1f;
        }

        internal static float ResolveBarotraumaOxygenDrainMultiplier(float crushDepthIntegrity01)
        {
            float safeIntegrity01 = math.saturate(math.select(1f, crushDepthIntegrity01, math.isfinite(crushDepthIntegrity01)));
            float damage01 = math.saturate(math.unlerp(1f, 0f, safeIntegrity01));
            float curvedDamage = damage01 * damage01;
            float multiplier = 1f + curvedDamage * (BarotraumaOxygenDrainMaxMultiplier - 1f);
            return math.clamp(multiplier, 1f, BarotraumaOxygenDrainHardClamp);
        }

        private float ResolveOxygenCarryMassScale()
        {
            if (TryResolveEffortLoadSnapshot(out _))
                return 1f;

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
            float playerStress = _playerHealth != null ? math.saturate(_playerHealth.Stress01) : 0f;
            return math.saturate(math.max(playerStress, math.max(physiologicalStress, math.max(movementStress, traumaStress))));
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

        private float ResolveHazardIntensity(HazardType type)
        {
            return TryResolveSurvivalAbsoluteAup(out double3 playerAup)
                ? HectonHazardManager.GetHazardIntensity(playerAup, type)
                : 0f;
        }

        private bool TryResolveSurvivalAbsoluteAup(out double3 playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite())
                {
                    playerAup = snapshot.Aup.ToAbsoluteDouble3();
                    if (math.all(math.isfinite(playerAup)))
                        return true;
                }

                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite())
                {
                    playerAup = movementState.PredictedAup.ToAbsoluteDouble3();
                    if (math.all(math.isfinite(playerAup)))
                        return true;
                }

                return false;
            }

            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    playerAup = currentAup.ToAbsoluteDouble3();
                    return math.all(math.isfinite(playerAup));
                }
            }

            return false;
        }

        private bool TryResolveSurvivalAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite())
                {
                    playerAup = snapshot.Aup;
                    return true;
                }

                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite())
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }

                return false;
            }

            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    playerAup = currentAup;
                    return true;
                }
            }

            return false;
        }

        private Vector3 ResolveSurvivalRuntimePosition()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite() &&
                    math.all(math.isfinite(snapshot.RuntimePosition)))
                {
                    Vector3 resolved = default;
                    resolved.x = snapshot.RuntimePosition.x;
                    resolved.y = snapshot.RuntimePosition.y;
                    resolved.z = snapshot.RuntimePosition.z;
                    return resolved;
                }

                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite() &&
                    math.all(math.isfinite(movementState.WorldPosition)))
                {
                    Vector3 resolved = default;
                    resolved.x = movementState.WorldPosition.x;
                    resolved.y = movementState.WorldPosition.y;
                    resolved.z = movementState.WorldPosition.z;
                    return resolved;
                }

                return Vector3.zero;
            }

            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    float3 runtimePosition = currentAup.ToRuntimeFloat3();
                    if (math.all(math.isfinite(runtimePosition)))
                    {
                        Vector3 resolved = default;
                        resolved.x = runtimePosition.x;
                        resolved.y = runtimePosition.y;
                        resolved.z = runtimePosition.z;
                        return resolved;
                    }
                }
            }

            Vector3 runtimePositionFallback = transform.position;
            return IsFinite(runtimePositionFallback) ? runtimePositionFallback : Vector3.zero;
        }

        private static float FiniteAtLeast(float value, float fallback, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : math.max(minimum, fallback);
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

        private float ResolveCurrentMovementSpeedSq()
        {
            if (!TryResolveKccVelocity(out Vector3 velocity))
                return 0f;

            float speedSq = velocity.sqrMagnitude;
            return math.isfinite(speedSq) && speedSq > 0f ? speedSq : 0f;
        }

        private static bool TryResolveKccVelocity(out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) || signal.Sequence == 0u)
                return false;

            uint currentFrame = SystemDispatcher.CurrentFrameId;
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            if (currentFrame != 0u &&
                signalFrame != 0u &&
                (signalFrame > currentFrame || currentFrame - signalFrame > KccVelocitySurvivalMaxAgeFrames))
            {
                return false;
            }

            float3 value = signal.Velocity;
            if (!math.all(math.isfinite(value)))
                return false;

            velocity = new Vector3(value.x, value.y, value.z);
            return true;
        }

        private float ResolveAuthoredCruiseSpeedMetersPerSecond()
        {
            float authoredCruiseSpeed = 1f;

            if (_playerMovement != null && _playerMovement.CurrentSuit != null)
                authoredCruiseSpeed = FiniteAtLeast(_playerMovement.CurrentSuit.maxSwimSpeed, 1f, 0.01f);

            if (_playerTransportCoordinator != null)
                authoredCruiseSpeed *= FiniteAtLeast(_playerTransportCoordinator.ResolveTransportSpeedMultiplier(), 1f, 0.01f);

            return FiniteAtLeast(authoredCruiseSpeed, 1f, 0.01f);
        }

        private void UpdateHungerAndThirst(float dt)
        {
            float coldNutritionMultiplier = ResolveColdNutritionDrainMultiplier(
                _environmentTemperature,
                stats != null ? stats.MinSafeTemp : HypothermiaFrostStartCelsius,
                ColdNutritionFullBoostRangeCelsius);
            hunger = math.max(0f, hunger - stats.HungerDrainRate * coldNutritionMultiplier * dt);

            // Drain thirst (slightly faster)
            float exertion = (_playerMovement != null && _playerMovement.IsSprinting) ? 1f : 0f;
            float sweatLossRate = Hecton8.PureLogic.Systems.HydrationSweatLossCalculator.Compute(
                exertion,
                _environmentTemperature,
                stats.ThirstDrainRate,
                stats != null ? stats.MaxSafeTemp : 35f
            );
            thirst = math.max(0f, thirst - sweatLossRate * dt);

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

        // ---------------------------------------------------------
        //  EVENT PUBLISHING
        // ---------------------------------------------------------

        private void PublishDirty()
        {
            uint survivalVitalsFlags = 0u;
            float safeOxygen = Oxygen;
            float safeEnergy = Energy;
            float safeDepth = Depth;
            float safeIntegrity = Integrity;
            float safePressure = Pressure;
            float safeHunger = Hunger;
            float safeThirst = Thirst;

            if (math.abs(safeOxygen - lastPubOxygen) > Epsilon)
            {
                lastPubOxygen = safeOxygen;
                survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.Oxygen;
                float oxygenNormalized = OxygenNormalized;
                if (oxygenNormalized < 0.15f)
                    survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.OxygenCritical;
            }

            if (math.abs(safeEnergy - lastPubEnergy) > Epsilon)
            {
                lastPubEnergy = safeEnergy;
                survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.Energy;
            }

            if (math.abs(safeDepth - lastPubDepth) > Epsilon)
            {
                lastPubDepth = safeDepth;
                survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.Depth;
            }

            if (math.abs(safeIntegrity - lastPubIntegrity) > Epsilon)
            {
                lastPubIntegrity = safeIntegrity;
                survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.Integrity;
            }

            if (math.abs(safePressure - lastPubPressure) > Epsilon)
            {
                lastPubPressure = safePressure;
                survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.Pressure;
            }

            IAtmosphereReadModel atmosphere = _atmosphereRuntime;

            // Temperature Publishing (Atmosphere + Local)
            float atmosphereTemperature = atmosphere != null ? atmosphere.CurrentTemperature : DefaultInternalTemperatureCelsius;
            float baseTemp = math.isfinite(atmosphereTemperature) ? atmosphereTemperature : DefaultInternalTemperatureCelsius;
            float totalTemp = baseTemp +
                SafeNonNegative(ResolveHazardIntensity(HazardType.Heat)) -
                SafeNonNegative(ResolveAbyssalColdPenaltyCelsius());
            if (math.abs(totalTemp - lastPubTemp) > Epsilon)
            {
                lastPubTemp = totalTemp;
                survivalVitalsFlags |= SurvivalVitalsChangedSignalFlags.Temperature;
            }

            // Radiation publishing: atmospheric baseline plus RadiationHazardGrid-owned local dose.
            float baseRad = atmosphere != null ? SafeNonNegative(atmosphere.CurrentRadiation) : 0f;
            float gridRad = SafeSaturate(_runtimeContext.RadiationIntensity01);
            float totalRad = math.max(baseRad, gridRad);
            if (math.abs(totalRad - lastPubRad) > Epsilon)
                lastPubRad = totalRad;

            // Hunger Publishing
            if (math.abs(safeHunger - lastPubHunger) > Epsilon)
                lastPubHunger = safeHunger;

            // Thirst Publishing
            if (math.abs(safeThirst - lastPubThirst) > Epsilon)
                lastPubThirst = safeThirst;

            PublishSurvivalVitalsChanged(survivalVitalsFlags);
        }

        private void PublishSurvivalVitalsChanged(uint flags)
        {
            if (flags == 0u || stats == null)
                return;

            uint sourceId = ResolveSurvivalVitalsSignalSourceId();

            _survivalVitalsSignalSequence++;
            if (_survivalVitalsSignalSequence == 0u)
                _survivalVitalsSignalSequence = 1u;

            float maxOxygen = FiniteAtLeast(ResolveRuntimeMaxOxygenCapacity(), 100f, 0.01f);
            float maxEnergy = FiniteAtLeast(stats.MaxEnergy, 100f, 0.01f);
            float maxIntegrity = FiniteAtLeast(stats.MaxIntegrity, 100f, 0.01f);
            SurvivalVitalsChangedSignal signal = default;
            signal.SourceId = sourceId;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            signal.Sequence = _survivalVitalsSignalSequence;
            signal.Flags = flags;
            signal.Oxygen01 = ResolveSafeRatio01(oxygen, maxOxygen);
            signal.Energy01 = ResolveSafeRatio01(energy, maxEnergy);
            signal.Integrity01 = ResolveSafeRatio01(integrity, maxIntegrity);
            signal.DeathCause = (byte)_lastDeathCause;
            if (!SurvivalSignalRoute.TryQueueVitals(in signal))
                ReportSurvivalVitalsSignalDrop();
        }

        private static void ReportSurvivalVitalsSignalDrop()
        {
            int dropCount = Interlocked.Increment(ref s_x001HectonSurvivalSystemSignalPushDropCount);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _SurvivalVitalsQueueDropWarningHash,
                _SurvivalVitalsQueueContextHash,
                math.max(1, dropCount));
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
            PublishSurvivalVitalsChanged(
                SurvivalVitalsChangedSignalFlags.Death |
                SurvivalVitalsChangedSignalFlags.Integrity |
                SurvivalVitalsChangedSignalFlags.Oxygen |
                SurvivalVitalsChangedSignalFlags.Depth);
            uint playerHash = ResolvePlayerEntityHash();
            bool hasDeathAup = TryResolveSurvivalAbsoluteAup(out double3 deathAup);
            if (!hasDeathAup)
                deathAup = MissingRespawnDeathAup();

            bool respawnAccepted = PlayerDeathReconciliationBridge.RequestRespawn(
                deathAup,
                unchecked((uint)_lastDeathCause),
                playerHash,
                out uint respawnSequence);
            if (!respawnAccepted)
                return;

            _pendingRespawnReconciliationSequence = respawnSequence;
            return;
        }

        private static double3 MissingRespawnDeathAup()
        {
            double3 missing = default;
            missing.x = double.NaN;
            missing.y = double.NaN;
            missing.z = double.NaN;
            return missing;
        }

        private void UpdateOxygenGraceState(float deltaTime)
        {
            if (integrity <= 0f)
            {
                bool lockDenied = false;
                bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                    ResolveRealOxygen01(oxygen),
                    0f,
                    0,
                    out lockDenied);
                if (lockDenied)
                    return;

                ResetOxygenGraceState();
                _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
                return;
            }

            if (oxygen > 0f)
            {
                if (!_metabolicOxygenStateSyncedThisTick)
                {
                    bool lockDenied = false;
                    bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                        ResolveRealOxygen01(oxygen),
                        0f,
                        0,
                        out lockDenied);
                    if (lockDenied)
                        return;

                    _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
                }

                ResetOxygenGraceState();
                return;
            }

            bool nextGraceActive = true;
            float nextGraceTimer = _oxygenGraceActive
                ? math.max(0f, _oxygenGraceTimer - math.max(0f, deltaTime))
                : OxygenGraceDurationSeconds;

            float elapsedGraceSeconds = OxygenGraceDurationSeconds - nextGraceTimer;
            float invGraceDuration = math.rcp(math.max(0.01f, OxygenGraceDurationSeconds));
            float gracePhase = math.saturate(elapsedGraceSeconds * invGraceDuration);
            float nextGraceVisionBlur01 = math.smoothstep(0f, 1f, gracePhase);

            if (nextGraceTimer <= 0f)
            {
                nextGraceActive = false;
                nextGraceVisionBlur01 = 1f;
            }

            if (!_metabolicOxygenStateSyncedThisTick)
            {
                bool lockDenied = false;
                bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                    0f,
                    nextGraceTimer,
                    1,
                    out lockDenied);
                if (lockDenied)
                    return;

                _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
            }

            _oxygenGraceActive = nextGraceActive;
            _oxygenGraceTimer = nextGraceTimer;
            _oxygenGraceVisionBlur01 = nextGraceVisionBlur01;

            if (_playerMovement != null)
                _playerMovement.SetRuntimeEmergencyMovementMultiplier(math.select(1f, OxygenGraceSpeedMultiplier, nextGraceActive));
        }

        private void ResetOxygenGraceState()
        {
            _oxygenGraceActive = false;
            _oxygenGraceTimer = 0f;
            _oxygenGraceVisionBlur01 = 0f;
            if (_playerMovement != null)
                _playerMovement.SetRuntimeEmergencyMovementMultiplier(1f);
        }

        private float ResolveRealOxygen01(float oxygenValue)
        {
            return math.saturate(oxygenValue * math.rcp(math.max(0.01f, ResolveRuntimeMaxOxygenCapacity())));
        }

        private float ResolveStagedAgonyTimeRemaining(byte nextHypoxiaState)
        {
            float activeTimer = math.max(0f, _oxygenGraceTimer);
            float hypoxiaTimer = math.select(OxygenGraceDurationSeconds, activeTimer, _oxygenGraceActive);
            return math.select(0f, hypoxiaTimer, nextHypoxiaState != 0);
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
            _survivalDatabaseStableHashesHandle = default;
            _survivalDatabaseMassKilogramsHandle = default;
            _survivalDatabaseVolumeLitersHandle = default;
            _survivalDatabaseEnergyDensityMegajoulesPerKilogramHandle = default;
            _survivalDatabaseBaseDurabilityHandle = default;
            _survivalDatabaseItemCount = 0;
        }

        private unsafe void EnsureSurvivalBlackboxSnapshot()
        {
            if (!Application.isPlaying)
                return;

            if (!_survivalBlackboxSnapshot.IsCreated)
            {
                _survivalBlackboxSnapshot = H8Memory.Allocate<SurvivalBlackboxSnapshot>(
                    SurvivalBlackboxSnapshotCapacity,
                    SystemID.GameplayPlayer,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SurvivalBlackboxSnapshot>[1] - survival physiology blackbox source payload - owner: HectonSurvivalSystem
            }

            if (!_survivalBlackboxSnapshot.IsCreated)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_survivalBlackboxSnapshot);
            if (GlobalTelemetryBus.TryRegisterBlackboxSource(
                    SurvivalBlackboxSourceHash,
                    sourcePtr,
                    SurvivalBlackboxSnapshotSizeBytes,
                    GlobalTelemetryBus.ShinobuBlackboxSourceFlagFloatScan,
                    out int slot))
            {
                _survivalBlackboxSourceSlot = slot;
                _survivalBlackboxSnapshot[0] = BuildSurvivalBlackboxSnapshot();
            }
        }

        private void DisposeSurvivalBlackboxSnapshot()
        {
            if (_survivalBlackboxSourceSlot >= 0)
            {
                GlobalTelemetryBus.UnregisterBlackboxSource(SurvivalBlackboxSourceHash);
                _survivalBlackboxSourceSlot = -1;
            }

            H8Memory.Release(ref _survivalBlackboxSnapshot, SystemID.GameplayPlayer);
        }

        private void WriteSurvivalBlackboxSnapshot()
        {
            if (!_survivalBlackboxSnapshot.IsCreated || _survivalBlackboxSourceSlot < 0)
                EnsureSurvivalBlackboxSnapshot();

            if (!_survivalBlackboxSnapshot.IsCreated)
                return;

            _survivalBlackboxSnapshot[0] = BuildSurvivalBlackboxSnapshot();
        }

        private SurvivalBlackboxSnapshot BuildSurvivalBlackboxSnapshot()
        {
            SurvivalBlackboxSnapshot snapshot = default;
            snapshot.SourceHash = SurvivalBlackboxSourceHash;
            snapshot.FrameIndex = SystemDispatcher.CurrentFrameId;
            snapshot.PlayerEntityHash = ResolvePlayerEntityHash();
            snapshot.Oxygen01 = SafeSaturate(stats != null ? OxygenNormalized : 0f);
            snapshot.Integrity01 = SafeSaturate(stats != null ? IntegrityNormalized : 0f);
            snapshot.DepthMeters = SafeNonNegative(depth);
            snapshot.PressureAtm = math.max(1f, SafeNonNegative(pressure));
            snapshot.SafeDepthMeters = SafeNonNegative(stats != null ? ResolveEffectiveSafeDepthMeters() : 0f);
            snapshot.OverpressureMeters = SafeNonNegative(stats != null ? OverpressureMeters : 0f);
            snapshot.PressureExposureSeverity01 = SafeSaturate(PressureExposureSeverity01);
            snapshot.NitrogenLoad01 = SafeSaturate(NitrogenLoad01);
            snapshot.NitrogenNarcosis01 = SafeSaturate(_nitrogenNarcosis01);
            snapshot.DecompressionRisk01 = SafeSaturate(_decompressionRisk01);
            snapshot.InternalTemperatureCelsius = math.select(
                DefaultInternalTemperatureCelsius,
                _internalTemperature,
                math.isfinite(_internalTemperature));
            snapshot.StatusMask = _statusMask;
            snapshot.Flags = BuildSurvivalBlackboxFlags();
            return snapshot;
        }

        private uint BuildSurvivalBlackboxFlags()
        {
            uint flags = 0u;
            flags |= math.select(0u, SurvivalBlackboxFlagAlive, alive);
            flags |= math.select(0u, SurvivalBlackboxFlagUnderwater, _surfaceContractUnderwater);
            flags |= math.select(0u, SurvivalBlackboxFlagBeyondSafeDepth, stats != null && IsBeyondSafeDepth);
            flags |= math.select(0u, SurvivalBlackboxFlagOxygenGrace, _oxygenGraceActive);
            flags |= math.select(0u, SurvivalBlackboxFlagBends, _physiologyBendsActive);
            flags |= math.select(0u, SurvivalBlackboxFlagFreshPhysiology, _hasCachedShinobuPhysiologySignal);
            flags |= math.select(0u, SurvivalBlackboxFlagNarcosis, _nitrogenNarcosis01 > 0.0001f);
            flags |= math.select(0u, SurvivalBlackboxFlagToxicity, _toxicity01 > 0.0001f);
            flags |= math.select(0u, SurvivalBlackboxFlagThermalStress, _thermalStressMode != ThermalStressMode.None);
            flags |= math.select(0u, SurvivalBlackboxFlagHasStats, stats != null);
            flags |= (((uint)_lastDeathCause) & 0xFFu) << SurvivalBlackboxDeathCauseShift;
            return flags;
        }

        private static float SafeSaturate(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        private static float SafeNonNegative(float value)
        {
            return math.max(0f, math.select(0f, value, math.isfinite(value)));
        }

        private static double SafeNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value)
                ? Math.Max(0d, value)
                : 0d;
        }

        private static float ResolveSafeRatio01(float numerator, float denominator)
        {
            if (!math.isfinite(numerator) || !math.isfinite(denominator) || denominator <= 0f)
                return 0f;

            return math.saturate(numerator / denominator);
        }

        private bool TryPrepareInjectedSurvivalDatabaseBuffers(int requiredLength)
        {
            if (requiredLength <= 0)
                return false;

            IDataVault vault = _survivalDataVault;
            if (vault == null)
                return false;

            return OpenOrAcquireSurvivalVaultBuffer(
                       vault,
                       ref _survivalDatabaseStableHashesHandle,
                       BufferID.SurvivalDatabaseStableHashes,
                       requiredLength,
                       NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireSurvivalVaultBuffer(
                       vault,
                       ref _survivalDatabaseMassKilogramsHandle,
                       BufferID.SurvivalDatabaseMassKilograms,
                       requiredLength,
                       NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireSurvivalVaultBuffer(
                       vault,
                       ref _survivalDatabaseVolumeLitersHandle,
                       BufferID.SurvivalDatabaseVolumeLiters,
                       requiredLength,
                       NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireSurvivalVaultBuffer(
                       vault,
                       ref _survivalDatabaseEnergyDensityMegajoulesPerKilogramHandle,
                       BufferID.SurvivalDatabaseEnergyDensityMegajoulesPerKilogram,
                       requiredLength,
                       NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireSurvivalVaultBuffer(
                       vault,
                       ref _survivalDatabaseBaseDurabilityHandle,
                       BufferID.SurvivalDatabaseBaseDurability,
                       requiredLength,
                       NativeArrayOptions.ClearMemory);
        }

        private bool TryResolveInjectedSurvivalDatabaseBuffers(
            out NativeArray<uint>.ReadOnly stableHashes,
            out NativeArray<float>.ReadOnly massKilograms,
            out NativeArray<float>.ReadOnly volumeLiters,
            out NativeArray<float>.ReadOnly energyDensityMegajoulesPerKilogram,
            out NativeArray<int>.ReadOnly baseDurability)
        {
            stableHashes = default;
            massKilograms = default;
            volumeLiters = default;
            energyDensityMegajoulesPerKilogram = default;
            baseDurability = default;

            if (_survivalDatabaseItemCount <= 0)
                return false;

            IDataVault vault = _survivalDataVault;
            return TryResolveInjectedSurvivalDatabaseBuffers(
                vault,
                _survivalDatabaseItemCount,
                out stableHashes,
                out massKilograms,
                out volumeLiters,
                out energyDensityMegajoulesPerKilogram,
                out baseDurability);
        }

        private bool TryResolveInjectedSurvivalDatabaseBuffers(
            IDataVault vault,
            int requiredLength,
            out NativeArray<uint>.ReadOnly stableHashes,
            out NativeArray<float>.ReadOnly massKilograms,
            out NativeArray<float>.ReadOnly volumeLiters,
            out NativeArray<float>.ReadOnly energyDensityMegajoulesPerKilogram,
            out NativeArray<int>.ReadOnly baseDurability)
        {
            stableHashes = default;
            massKilograms = default;
            volumeLiters = default;
            energyDensityMegajoulesPerKilogram = default;
            baseDurability = default;

            if (vault == null ||
                requiredLength <= 0)
            {
                return false;
            }

            if (!TryOpenSurvivalVaultBuffer(
                    vault,
                    ref _survivalDatabaseStableHashesHandle,
                    BufferID.SurvivalDatabaseStableHashes,
                    requiredLength,
                    out stableHashes) ||
                !TryOpenSurvivalVaultBuffer(
                    vault,
                    ref _survivalDatabaseMassKilogramsHandle,
                    BufferID.SurvivalDatabaseMassKilograms,
                    requiredLength,
                    out massKilograms) ||
                !TryOpenSurvivalVaultBuffer(
                    vault,
                    ref _survivalDatabaseVolumeLitersHandle,
                    BufferID.SurvivalDatabaseVolumeLiters,
                    requiredLength,
                    out volumeLiters) ||
                !TryOpenSurvivalVaultBuffer(
                    vault,
                    ref _survivalDatabaseEnergyDensityMegajoulesPerKilogramHandle,
                    BufferID.SurvivalDatabaseEnergyDensityMegajoulesPerKilogram,
                    requiredLength,
                    out energyDensityMegajoulesPerKilogram) ||
                !TryOpenSurvivalVaultBuffer(
                    vault,
                    ref _survivalDatabaseBaseDurabilityHandle,
                    BufferID.SurvivalDatabaseBaseDurability,
                    requiredLength,
                    out baseDurability))
            {
                return false;
            }

            return stableHashes.IsCreated &&
                massKilograms.IsCreated &&
                volumeLiters.IsCreated &&
                energyDensityMegajoulesPerKilogram.IsCreated &&
                baseDurability.IsCreated &&
                stableHashes.Length >= requiredLength &&
                massKilograms.Length >= requiredLength &&
                volumeLiters.Length >= requiredLength &&
                energyDensityMegajoulesPerKilogram.Length >= requiredLength &&
                baseDurability.Length >= requiredLength;
        }

        private static bool OpenOrAcquireSurvivalVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            NativeArray<T>.ReadOnly buffer;
            if (TryOpenSurvivalVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;

                return TryOpenSurvivalVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayPlayer,
                options);
            return TryOpenSurvivalVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenSurvivalVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsSurvivalVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsSurvivalVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.GameplayPlayer &&
                   handle.Generation != 0u;
        }

        private bool TryWriteInjectedSurvivalDatabaseBuffers(
            NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            int itemCount)
        {
            if (!parsedItems.IsCreated ||
                itemCount <= 0 ||
                parsedItems.Length < itemCount)
            {
                return false;
            }

            IDataVault vault = _survivalDataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return TryWriteInjectedSurvivalStableHashes(vault, parsedItems, itemCount) &&
                   TryWriteInjectedSurvivalMassKilograms(vault, parsedItems, itemCount) &&
                   TryWriteInjectedSurvivalVolumeLiters(vault, parsedItems, itemCount) &&
                   TryWriteInjectedSurvivalEnergyDensity(vault, parsedItems, itemCount) &&
                   TryWriteInjectedSurvivalBaseDurability(vault, parsedItems, itemCount);
        }

        private bool TryWriteInjectedSurvivalStableHashes(
            IDataVault vault,
            NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            int itemCount)
        {
            VaultGenerationHandle<uint> handle = _survivalDatabaseStableHashesHandle;
            bool lockAcquired = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.GameplayPlayer, out NativeArray<uint> target))
                {
                    return false;
                }

                lockAcquired = true;
                if (!target.IsCreated || target.Length < itemCount)
                    return false;

                for (int i = 0; i < itemCount; i++)
                    target[i] = parsedItems[i].StableHash;

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.GameplayPlayer);
            }
        }

        private bool TryWriteInjectedSurvivalMassKilograms(
            IDataVault vault,
            NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            int itemCount)
        {
            VaultGenerationHandle<float> handle = _survivalDatabaseMassKilogramsHandle;
            bool lockAcquired = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.GameplayPlayer, out NativeArray<float> target))
                {
                    return false;
                }

                lockAcquired = true;
                if (!target.IsCreated || target.Length < itemCount)
                    return false;

                for (int i = 0; i < itemCount; i++)
                    target[i] = parsedItems[i].MassKilograms;

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.GameplayPlayer);
            }
        }

        private bool TryWriteInjectedSurvivalVolumeLiters(
            IDataVault vault,
            NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            int itemCount)
        {
            VaultGenerationHandle<float> handle = _survivalDatabaseVolumeLitersHandle;
            bool lockAcquired = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.GameplayPlayer, out NativeArray<float> target))
                {
                    return false;
                }

                lockAcquired = true;
                if (!target.IsCreated || target.Length < itemCount)
                    return false;

                for (int i = 0; i < itemCount; i++)
                    target[i] = parsedItems[i].VolumeLiters;

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.GameplayPlayer);
            }
        }

        private bool TryWriteInjectedSurvivalEnergyDensity(
            IDataVault vault,
            NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            int itemCount)
        {
            VaultGenerationHandle<float> handle = _survivalDatabaseEnergyDensityMegajoulesPerKilogramHandle;
            bool lockAcquired = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.GameplayPlayer, out NativeArray<float> target))
                {
                    return false;
                }

                lockAcquired = true;
                if (!target.IsCreated || target.Length < itemCount)
                    return false;

                for (int i = 0; i < itemCount; i++)
                    target[i] = parsedItems[i].EnergyDensityMegajoulesPerKilogram;

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.GameplayPlayer);
            }
        }

        private bool TryWriteInjectedSurvivalBaseDurability(
            IDataVault vault,
            NativeArray<SurvivalDatabaseItemRecord> parsedItems,
            int itemCount)
        {
            VaultGenerationHandle<int> handle = _survivalDatabaseBaseDurabilityHandle;
            bool lockAcquired = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.GameplayPlayer, out NativeArray<int> target))
                {
                    return false;
                }

                lockAcquired = true;
                if (!target.IsCreated || target.Length < itemCount)
                    return false;

                for (int i = 0; i < itemCount; i++)
                    target[i] = parsedItems[i].BaseDurability;

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.GameplayPlayer);
            }
        }

        private void BindPhysiologyVaultHandles(IDataVault vault)
        {
            _metabolicStateHandle = default;
            _suitIntegrityStateHandle = default;
            _metabolicStateHandleReady = false;
            _suitIntegrityStateHandleReady = false;
            _nextMetabolicStateHandleRetryFrame = 0u;
            _nextSuitIntegrityStateHandleRetryFrame = 0u;

            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (vault.TryGetGenerationHandle(
                    MetabolicStateBufferId,
                    out VaultGenerationHandle<MetabolicStateDTO> metabolicHandle) &&
                IsSurvivalVaultHandle(in metabolicHandle, MetabolicStateBufferId))
            {
                _metabolicStateHandle = metabolicHandle;
                _metabolicStateHandleReady = true;
            }

            if (vault.TryGetGenerationHandle(
                    SuitIntegrityStateBufferId,
                    out VaultGenerationHandle<SuitIntegrityDTO> suitIntegrityHandle) &&
                IsSurvivalVaultHandle(in suitIntegrityHandle, SuitIntegrityStateBufferId))
            {
                _suitIntegrityStateHandle = suitIntegrityHandle;
                _suitIntegrityStateHandleReady = true;
            }
        }

        private static bool IsBeforeFrame(uint currentFrame, uint targetFrame)
        {
            return currentFrame != 0u &&
                   targetFrame != 0u &&
                   unchecked((int)(currentFrame - targetFrame)) < 0;
        }

        private bool CanRetryMetabolicStateHandle()
        {
            uint currentFrame = SystemDispatcher.CurrentFrameId;
            if (currentFrame == 0u)
                currentFrame = unchecked((uint)Time.frameCount);

            if (IsBeforeFrame(currentFrame, _nextMetabolicStateHandleRetryFrame))
                return false;

            _nextMetabolicStateHandleRetryFrame = currentFrame != 0u
                ? currentFrame + PhysiologyHandleRetryFrames
                : 0u;
            return true;
        }

        private bool CanRetrySuitIntegrityStateHandle()
        {
            uint currentFrame = SystemDispatcher.CurrentFrameId;
            if (currentFrame == 0u)
                currentFrame = unchecked((uint)Time.frameCount);

            if (IsBeforeFrame(currentFrame, _nextSuitIntegrityStateHandleRetryFrame))
                return false;

            _nextSuitIntegrityStateHandleRetryFrame = currentFrame != 0u
                ? currentFrame + PhysiologyHandleRetryFrames
                : 0u;
            return true;
        }

        private bool TryRefreshMetabolicStateHandle(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (_metabolicStateHandleReady &&
                IsSurvivalVaultHandle(in _metabolicStateHandle, MetabolicStateBufferId))
            {
                return true;
            }

            if (!CanRetryMetabolicStateHandle())
                return false;

            if (!vault.TryGetGenerationHandle(
                    MetabolicStateBufferId,
                    out _metabolicStateHandle) ||
                !IsSurvivalVaultHandle(in _metabolicStateHandle, MetabolicStateBufferId))
            {
                _metabolicStateHandleReady = false;
                return false;
            }

            _metabolicStateHandleReady = true;
            return true;
        }

        private bool TryRefreshSuitIntegrityStateHandle(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (_suitIntegrityStateHandleReady &&
                IsSurvivalVaultHandle(in _suitIntegrityStateHandle, SuitIntegrityStateBufferId))
            {
                return true;
            }

            if (!CanRetrySuitIntegrityStateHandle())
                return false;

            if (!vault.TryGetGenerationHandle(
                    SuitIntegrityStateBufferId,
                    out _suitIntegrityStateHandle) ||
                !IsSurvivalVaultHandle(in _suitIntegrityStateHandle, SuitIntegrityStateBufferId))
            {
                _suitIntegrityStateHandleReady = false;
                return false;
            }

            _suitIntegrityStateHandleReady = true;
            return true;
        }

        private bool TryReadSuitCrushDepthIntegrity01(out float crushDepthIntegrity01)
        {
            crushDepthIntegrity01 = 1f;
            IDataVault vault = _survivalDataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !TryRefreshSuitIntegrityStateHandle(vault))
            {
                return false;
            }

            if (!vault.TryReadOnlyHandle(in _suitIntegrityStateHandle, out NativeArray<SuitIntegrityDTO>.ReadOnly states) ||
                vault.IsCompactionFenceActive ||
                states.Length <= 0)
            {
                _suitIntegrityStateHandleReady = false;
                _nextSuitIntegrityStateHandleRetryFrame = 0u;
                return false;
            }

            float value = states[0].CurrentIntegrity01;
            crushDepthIntegrity01 = math.saturate(math.select(1f, value, math.isfinite(value)));
            return true;
        }

        private bool TryWriteMetabolicOxygenStateToVault(
            float realO201,
            float agonyTimeRemaining,
            byte isInHypoxia,
            out bool lockDenied)
        {
            lockDenied = false;
            IDataVault vault = _survivalDataVault;
            if (vault == null)
                return false;

            if (vault.IsCompactionFenceActive)
            {
                lockDenied = true;
                return false;
            }

            if (!TryRefreshMetabolicStateHandle(vault))
            {
                lockDenied = vault.IsCompactionFenceActive;
                return false;
            }

            float safeRealO201 = math.saturate(math.select(0f, realO201, math.isfinite(realO201)));
            float safeAgonyTimeRemaining = math.max(0f, math.select(0f, agonyTimeRemaining, math.isfinite(agonyTimeRemaining)));
            byte safeHypoxiaState = (byte)math.select(0, 1, isInHypoxia != 0);
            uint signalSourceId = ResolveSurvivalVitalsSignalSourceId();

            VaultGenerationHandle<MetabolicStateDTO> writeHandle = _metabolicStateHandle;
            bool fenceActiveBeforeLock = vault.IsCompactionFenceActive;
            if (fenceActiveBeforeLock ||
                !vault.TryAcquireWriteLock(in writeHandle, SystemID.GameplayPlayer, out NativeArray<MetabolicStateDTO> states))
            {
                lockDenied = true;
                if (!fenceActiveBeforeLock && !vault.IsCompactionFenceActive)
                {
                    _metabolicStateHandle = default;
                    _metabolicStateHandleReady = false;
                    _nextMetabolicStateHandleRetryFrame = 0u;
                }

                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !states.IsCreated ||
                    states.Length <= 0)
                {
                    lockDenied = true;
                    _metabolicStateHandle = default;
                    _metabolicStateHandleReady = false;
                    _nextMetabolicStateHandleRetryFrame = 0u;
                    return false;
                }

                MetabolicStateDTO state = states[0];
                state.RealO2 = safeRealO201;
                state.AgonyTimeRemaining = safeAgonyTimeRemaining;
                state.IsInHypoxia = safeHypoxiaState;
                if (state.EntityHashID == 0u)
                    state.EntityHashID = signalSourceId;
                state.Flags = safeHypoxiaState != 0
                    ? state.Flags | ShinobuMetabolismVaultContract.FlagHypoxia
                    : state.Flags & ~ShinobuMetabolismVaultContract.FlagHypoxia;
                states[0] = state;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in writeHandle, SystemID.GameplayPlayer);
            }
        }

        private uint ResolveSurvivalVitalsSignalSourceId()
        {
            uint sourceId = _survivalVitalsSignalSourceId;
            return sourceId != 0u ? sourceId : _SurvivalRuntimeContextHash;
        }

        private uint ResolvePlayerEntityHash()
        {
            uint playerHash = _playerEntityHash;
            return playerHash != 0u ? playerHash : _SurvivalRuntimeContextHash;
        }

        private uint ResolvePlayerToxicitySignalEntityId()
        {
            GameObject playerObject = null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                playerObject = playerContext.PlayerObject;
            if (playerObject == null && _playerHealth != null)
                playerObject = _playerHealth.gameObject;
            if (playerObject == null)
                playerObject = BootstrapState.CurrentPlayerObject;
            if (playerObject == null)
                playerObject = gameObject;

            uint entityHash = playerObject != null ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId())) : 0u;
            if (entityHash != 0u)
                return entityHash;

            uint playerHash = _playerEntityHash;
            return playerHash != 0u ? playerHash : PlayerToxicityFallbackEntityHash;
        }

        private void RefreshSurvivalIdentityCold()
        {
            if (_survivalVitalsSignalSourceId != 0u && _playerEntityHash != 0u)
                return;

            ulong ownerEntityId = EntityId.ToULong(GetEntityId());
            if (_survivalVitalsSignalSourceId == 0u)
                _survivalVitalsSignalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(ownerEntityId);
            if (_playerEntityHash == 0u)
                _playerEntityHash = unchecked((uint)ownerEntityId);
        }

        // ---------------------------------------------------------
        //  PUBLIC API
        // ---------------------------------------------------------

        public void RefillOxygen(float amount)
        {
            ApplyOxygenRefill(amount);
        }

        public void ApplyOxygenRefill(float amount)
        {
            if (amount <= 0f)
                return;

            float nextOxygen = math.min(ResolveRuntimeMaxOxygenCapacity(), oxygen + math.max(0f, amount));
            bool lockDenied = false;
            bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                ResolveRealOxygen01(nextOxygen),
                0f,
                0,
                out lockDenied);
            if (lockDenied)
                return;

            oxygen = nextOxygen;
            if (oxygen > 0f)
            {
                ResetOxygenGraceState();
                _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
            }

            ForceDirty(ref lastPubOxygen);
        }

        public void ClearSurvivalStatusBits(uint statusBits)
        {
            _statusMask &= ~statusBits;
        }

        public bool HasSurvivalStatus(uint statusBit)
        {
            return (_statusMask & statusBit) != 0u;
        }

        /// <summary>
        /// Applies a runtime-only oxygen-capacity multiplier without mutating the authored SurvivalStats asset.
        /// </summary>
        /// <param name="multiplier">Runtime oxygen-capacity multiplier.</param>
        public void SetRuntimeOxygenCapacityMultiplier(float multiplier)
        {
            float previousMultiplier = _runtimeOxygenCapacityMultiplier;
            float nextMultiplier = math.clamp(multiplier, 0.5f, 4f);
            _runtimeOxygenCapacityMultiplier = nextMultiplier;

            float nextOxygen = math.clamp(oxygen, 0f, ResolveRuntimeMaxOxygenCapacity());
            byte nextHypoxiaState = (byte)math.select(0, 1, nextOxygen <= 0f);
            float nextAgonyTimeRemaining = ResolveStagedAgonyTimeRemaining(nextHypoxiaState);
            bool lockDenied = false;
            bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                ResolveRealOxygen01(nextOxygen),
                nextAgonyTimeRemaining,
                nextHypoxiaState,
                out lockDenied);
            if (lockDenied)
            {
                _runtimeOxygenCapacityMultiplier = previousMultiplier;
                return;
            }

            oxygen = nextOxygen;
            _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
            if (oxygen > 0f)
                ResetOxygenGraceState();
            else
                UpdateOxygenGraceState(0f);
            ForceDirty(ref lastPubOxygen);
        }

        public void RechargeEnergy(float amount)
        {
            energy = math.clamp(energy + amount, 0f, stats.MaxEnergy);
            ForceDirty(ref lastPubEnergy);
        }

        internal void SetMovementStaminaBurnInput(float intendedMovementLengthSq, float drainMultiplier)
        {
            _movementIntentLengthSq = math.max(0f, intendedMovementLengthSq);
            _movementStaminaDrainMultiplier = math.max(0f, drainMultiplier);
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

            float nextOxygen = math.max(0f, oxygen - amount);
            byte nextHypoxiaState = (byte)math.select(0, 1, nextOxygen <= 0f);
            float nextAgonyTimeRemaining = ResolveStagedAgonyTimeRemaining(nextHypoxiaState);
            bool lockDenied = false;
            bool wroteMetabolicState = TryWriteMetabolicOxygenStateToVault(
                ResolveRealOxygen01(nextOxygen),
                nextAgonyTimeRemaining,
                nextHypoxiaState,
                out lockDenied);
            if (lockDenied)
                return;

            oxygen = nextOxygen;
            _metabolicOxygenStateSyncedThisTick = wroteMetabolicState;
            if (oxygen <= 0f)
                UpdateOxygenGraceState(0f);
            ForceDirty(ref lastPubOxygen);
            CheckLethalConditions();
        }

        public void TakeDamage(float amount)
        {
            if (!alive || amount <= 0f) return;

            amount = math.max(0f, amount);
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
            float clampedSeverity = SafeSaturate(severity01);
            float clampedDuration = SafeNonNegative(durationSeconds);
            if (clampedSeverity <= 0f || clampedDuration <= 0f)
                return;

            _toxicity01 = ResolveBodyToxicity01(ResolveHazardIntensity(HazardType.Toxicity));
            PublishNutritionalToxicityStatus(clampedSeverity, clampedDuration);
        }

        internal static bool ShouldApplyNutritionalToxicityOnConsume(int itemHashId)
        {
            return itemHashId == _MembraneTissueHashId;
        }

        private void PublishNutritionalToxicityStatus(float severity01, float durationSeconds)
        {
            float severity = SafeSaturate(severity01);
            float duration = math.max(0.1f, SafeNonNegative(durationSeconds));
            if (severity <= 0.0001f)
                return;

            int targetId = _playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(_playerHealth.gameObject)
                : CombatDamageRuntime.ResolveTargetId(gameObject);
            uint signalEntityId = ResolvePlayerToxicitySignalEntityId();

            if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))
            {
                CombatDamageRuntime.TryQueueStatusEffect(
                    targetId,
                    CombatStatusBits.Poisoned64,
                    duration,
                    DamageSourceIds.EnvironmentHazard,
                    severity);
            }

            bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);

            ToxicityExposureSignal signal = default;
            signal.Exposure01 = severity;
            signal.ToxemiaDelta = math.saturate(severity * NutritionalToxicitySignalDeltaScale);
            signal.EntityId = signalEntityId;
            signal.ChemicalHash = _NutritionalToxicityChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            if (hasSourceAup)
            {
                signal.AUP = playerAup;
                signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;
            }

            SignalBus<ToxicityExposureSignal>.TryPushTracked(in signal, ref s_x001HectonSurvivalSystemSignalPushDropCount);
        }

        private void PublishEnvironmentalToxicityStatus(float toxicity01, float exposureScale, float dt)
        {
            float toxicity = SafeSaturate(toxicity01);
            float exposure = SafeNonNegative(exposureScale);
            float safeDt = SafeNonNegative(dt);
            float severity = math.saturate(toxicity * exposure);
            if (severity <= 0.0001f)
                return;

            int targetId = _playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(_playerHealth.gameObject)
                : CombatDamageRuntime.ResolveTargetId(gameObject);
            uint signalEntityId = ResolvePlayerToxicitySignalEntityId();

            float duration = math.max(0.1f, safeDt * 2f);
            if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))
            {
                CombatDamageRuntime.TryQueueStatusEffect(
                    targetId,
                    CombatStatusBits.Poisoned64,
                    duration,
                    DamageSourceIds.EnvironmentHazard,
                    severity);
            }

            bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);

            ToxicityExposureSignal signal = default;
            signal.Exposure01 = severity;
            signal.ToxemiaDelta = math.saturate(severity * safeDt * 0.08f);
            signal.EntityId = signalEntityId;
            signal.ChemicalHash = _EnvironmentalToxicityChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            if (hasSourceAup)
            {
                signal.AUP = playerAup;
                signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;
            }

            SignalBus<ToxicityExposureSignal>.TryPushTracked(in signal, ref s_x001HectonSurvivalSystemSignalPushDropCount);
        }

        internal static bool ShouldApplyNutritionalToxicityOnConsume(ItemData item)
        {
            if (item == null)
                return false;

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId != 0 && ShouldApplyNutritionalToxicityOnConsume(itemHashId))
                return true;

            return item.isRawResource &&
                   item.resourceFamily == ResourceFamily.Organic &&
                   (item.category == ItemCategory.Material || item.category == ItemCategory.Organic);
        }

        public void SetWeight(float kg)
        {
            weight = math.max(0f, kg);
        }

        public void SetSurfaceY(float y) => surfaceWorldY = SanitizeSurfaceY(y);

        private static float SanitizeSurfaceY(float y)
        {
            return math.isfinite(y) && math.abs(y) > 0.0001f
                ? y
                : DefaultWaterSurfaceY;
        }

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

        // ---------------------------------------------------------
        //  SAVE SYSTEM
        // ---------------------------------------------------------

#if UNITY_EDITOR
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
                ReleaseSurvivalDatabaseRows(ref parsedItems);
                return false;
            }

            if (!TryPrepareInjectedSurvivalDatabaseBuffers(parsedItemCount))
            {
                ReleaseSurvivalDatabaseRows(ref parsedItems);
                return false;
            }

            if (!TryWriteInjectedSurvivalDatabaseBuffers(parsedItems, parsedItemCount))
            {
                _survivalDatabaseItemCount = 0;
                ReleaseSurvivalDatabaseRows(ref parsedItems);
                return false;
            }

            _survivalDatabaseItemCount = parsedItemCount;
            ReleaseSurvivalDatabaseRows(ref parsedItems);
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
                _survivalDatabaseItemCount <= 0 ||
                !TryResolveInjectedSurvivalDatabaseBuffers(
                    out NativeArray<uint>.ReadOnly stableHashes,
                    out NativeArray<float>.ReadOnly massKilograms,
                    out NativeArray<float>.ReadOnly volumeLiters,
                    out NativeArray<float>.ReadOnly energyDensityMegajoulesPerKilogram,
                    out NativeArray<int>.ReadOnly baseDurability))
            {
                return false;
            }

            uint stableHash = ComputeStableIdHash(stableId.AsSpan());
            for (int i = 0; i < _survivalDatabaseItemCount; i++)
            {
                if (stableHashes[i] != stableHash)
                    continue;

                parameters = new SurvivalDatabaseItemParameters(
                    stableId,
                    stableHashes[i],
                    massKilograms[i],
                    volumeLiters[i],
                    energyDensityMegajoulesPerKilogram[i],
                    baseDurability[i]);
                return true;
            }

            return false;
        }
#endif

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
            if (data == null)
                return;

            ref PlayerStatsDTO dto = ref data.playerStats;
            dto.oxygen = oxygen;
            dto.energy = energy;
            dto.integrity = ResolvePersistedIntegrityForCurrentLife();
            dto.weight = weight;
            dto.hunger = hunger;
            dto.thirst = thirst;
            dto.currentLifeDurationSeconds = _currentLifeDurationSeconds;
            dto.currentLifePeakDepthMeters = _currentLifePeakDepthMeters;
            dto.currentLifeLowestOxygenNormalized = _currentLifeLowestOxygenNormalized;
            dto.currentLifeLowestEnergyNormalized = _currentLifeLowestEnergyNormalized;
            dto.currentLifeLowestIntegrityNormalized = _currentLifeLowestIntegrityNormalized;
            PlayerInjuryStatus savedInjuries = ResolveCurrentInjuries();
            dto.injuryFlags = (byte)((byte)savedInjuries & SaveData.PlayerInjurySupportedFlagMask);
            dto.bleedingSecondsRemaining = 0f;
            dto.bleedingDamagePerSecond = 0f;
            dto.bleedingSeverity01 = (savedInjuries & PlayerInjuryStatus.Bleeding) != 0
                ? SafeSaturate(_bleedingSeverity01)
                : 0f;
            dto.fractureSecondsRemaining = 0f;
            dto.fracturePenalty01 = (savedInjuries & PlayerInjuryStatus.Fracture) != 0
                ? SafeSaturate(_fracturePenalty01)
                : 0f;
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
            dto.SetVelocity(ResolveSavedPlayerVelocity());
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref dto);
        }

        private float ResolvePersistedIntegrityForCurrentLife()
        {
            // PlayerStatsDTO has no independent alive bit; a dead current life must not reload as alive.
            return alive ? integrity : 0f;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearNitrogenLoadNotificationDiagnostics();
            ClearPendingRespawnReconciliation();
            if (data == null)
            {
                if (stats != null)
                    PublishLoadedSurvivalState();
                return;
            }

            if (stats == null)
                return;

            PlayerStatsDTO dto = data.playerStats;
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref dto);
            bool hasTelemetryV23 = data.version >= 23;
            oxygen    = Mathf.Clamp(dto.oxygen,    0f, ResolveRuntimeMaxOxygenCapacity());
            energy    = Mathf.Clamp(dto.energy,    0f, stats.MaxEnergy);
            integrity = Mathf.Clamp(dto.integrity, 0f, stats.MaxIntegrity);
            weight    = Mathf.Max(0f, dto.weight);
            hunger    = Mathf.Clamp(dto.hunger,    0f, stats.MaxHunger);
            thirst    = Mathf.Clamp(dto.thirst,    0f, stats.MaxThirst);
            _currentLifeDurationSeconds = hasTelemetryV23 ? SafeNonNegative(dto.currentLifeDurationSeconds) : 0d;
            _currentLifePeakDepthMeters = hasTelemetryV23 ? SafeNonNegative(dto.currentLifePeakDepthMeters) : 0d;
            _currentLifeLowestOxygenNormalized = hasTelemetryV23 ? SafeSaturate(dto.currentLifeLowestOxygenNormalized) : OxygenNormalized;
            _currentLifeLowestEnergyNormalized = hasTelemetryV23 ? SafeSaturate(dto.currentLifeLowestEnergyNormalized) : EnergyNormalized;
            _currentLifeLowestIntegrityNormalized = hasTelemetryV23 ? SafeSaturate(dto.currentLifeLowestIntegrityNormalized) : IntegrityNormalized;
            _injuryStatus = (PlayerInjuryStatus)(dto.injuryFlags & SaveData.PlayerInjurySupportedFlagMask);
            _bleedingSeverity01 = (_injuryStatus & PlayerInjuryStatus.Bleeding) != 0
                ? SafeSaturate(dto.bleedingSeverity01)
                : 0f;
            _fracturePenalty01 = (_injuryStatus & PlayerInjuryStatus.Fracture) != 0
                ? SafeSaturate(dto.fracturePenalty01)
                : 0f;
            ClearCombatStatusReadModel(CombatStatusBits.Bleeding64 | CombatStatusBits.Fractured64);
            _environmentTemperature = math.isfinite(dto.environmentTemperature) ? dto.environmentTemperature : DefaultInternalTemperatureCelsius;
            _internalTemperature = _environmentTemperature;
            _coldSeverity01 = SafeSaturate(dto.coldStressSeverity01);
            _heatSeverity01 = SafeSaturate(dto.heatStressSeverity01);
            _thermalStressMode = ResolveThermalStressModeFromState();
            _nitrogenBuildUp = math.isfinite(dto.nitrogenBuildUp)
                ? Mathf.Clamp(dto.nitrogenBuildUp, 0f, NitrogenBuildUpHardCap)
                : 0f;
            _nitrogenLoad = NitrogenBaselinePressureAtm;
            RefreshNitrogenNarcosisRuntimeState();
            if (oxygen > 0f)
            {
                ResetOxygenGraceState();
                _metabolicOxygenStateSyncedThisTick = TryWriteMetabolicOxygenStateToVault(
                    ResolveRealOxygen01(oxygen),
                    0f,
                    0,
                    out _);
            }
            else
            {
                _oxygenGraceActive = true;
                _oxygenGraceTimer = OxygenGraceDurationSeconds;
                _oxygenGraceVisionBlur01 = 0f;
                _metabolicOxygenStateSyncedThisTick = TryWriteMetabolicOxygenStateToVault(
                    0f,
                    OxygenGraceDurationSeconds,
                    1,
                    out _);
            }

            alive     = integrity > 0f;
            _pendingIntegrityDeathCause = SurvivalDeathCause.None;
            _hasLastDeathRecord = hasTelemetryV23 && dto.hasLastDeathRecord;
            _lastDeathRecord = _hasLastDeathRecord
                ? new SurvivalDeathRecord(
                    (SurvivalDeathCause)dto.lastDeathCause,
                    dto.GetLastDeathPosition(),
                    SafeNonNegative(dto.lastDeathLifeDurationSeconds),
                    SafeNonNegative(dto.lastDeathPeakDepthMeters),
                    SafeSaturate(dto.lastDeathLowestOxygenNormalized),
                    SafeSaturate(dto.lastDeathLowestEnergyNormalized),
                    SafeSaturate(dto.lastDeathLowestIntegrityNormalized))
                : default;
            _lastDeathCause = alive ? SurvivalDeathCause.None : ResolveLoadedDeathCause();
            ResetPressureExposureTracking();

            Vector3 pos = dto.GetPosition();
            Quaternion rotation = dto.GetRotation();
            if (IsFinite(pos) && IsFinite(rotation))
                transform.SetPositionAndRotation(pos, rotation);

            if (_playerRigidbody != null)
            {
                Vector3 restoredVelocity = ResolveSafeSavedVelocity(dto.GetVelocity());
                HectonPlayerMotor playerMotor = null;
                if (_playerRigidbody.TryGetComponent(out playerMotor))
                {
                    playerMotor.SetLinearVelocity(restoredVelocity);
                    playerMotor.SetAngularVelocity(Vector3.zero, wake: false);
                }
            }

            ApplyInjuryMovementPenalty();
            ApplyNitrogenMovementPenalty();
            PublishLoadedSurvivalState();
        }

        // ---------------------------------------------------------
        //  INTERNAL UTILITY
        // ---------------------------------------------------------

        private Vector3 ResolveSavedPlayerVelocity()
        {
            return TryResolveKccVelocity(out Vector3 velocity)
                ? ResolveSafeSavedVelocity(velocity)
                : Vector3.zero;
        }

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
            _metabolicOxygenStateSyncedThisTick = TryWriteMetabolicOxygenStateToVault(1f, 0f, 0, out _);

            _tempGraceTimer = 0f;
            _radGraceTimer  = 0f;

            ApplyInjuryMovementPenalty();
            ForceAllDirty();
        }

        private void PublishLoadedSurvivalState()
        {
            RefreshSurvivalIdentityCold();
            RefreshSurvivalStatusMask();
            ForceAllDirty();
            PublishRuntimeContextState();
            PublishHeadlessUIState();
            PublishDirty();
            if (!alive)
            {
                PublishSurvivalVitalsChanged(
                    SurvivalVitalsChangedSignalFlags.Death |
                    SurvivalVitalsChangedSignalFlags.Integrity |
                    SurvivalVitalsChangedSignalFlags.Oxygen |
                    SurvivalVitalsChangedSignalFlags.Depth);
            }

            WriteSurvivalBlackboxSnapshot();
        }

        private SurvivalDeathCause ResolveLoadedDeathCause()
        {
            if (_hasLastDeathRecord && _lastDeathRecord.Cause != SurvivalDeathCause.None)
                return _lastDeathRecord.Cause;

            return ResolveDeathCause();
        }

        private void ApplyRespawnReconciliationSurvival()
        {
            ResetToMax();
            float maxHunger = stats != null ? math.max(0.01f, stats.MaxHunger) : math.max(0.01f, hunger);
            float maxThirst = stats != null ? math.max(0.01f, stats.MaxThirst) : math.max(0.01f, thirst);
            hunger = maxHunger * 0.65f;
            thirst = maxThirst * 0.70f;
            _nitrogenBuildUp = 0f;
            _nitrogenLoad = NitrogenBaselinePressureAtm;
            _nitrogenNarcosis01 = 0f;
            _toxicityStaminaMultiplier = 1f;
            _toxicity01 = 0f;
            _statusMask = 0u;
            _cachedCombatStatusMask = 0UL;
            _hasCachedCombatStatusMask = false;
            alive = true;
            enabled = true;
            PublishRespawnedSurvivalState();
        }

        private void ConsumeCommittedRespawnReconciliationSignals()
        {
            uint pendingSequence = _pendingRespawnReconciliationSequence;
            if (pendingSequence == 0u || pendingSequence == _lastAppliedRespawnReconciliationSequence)
                return;

            ReadOnlySpan<PlayerRespawnSignal> signals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            uint playerHash = ResolvePlayerEntityHash();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerRespawnSignal signal = signals[i];
                if (!PlayerDeathReconciliationBridge.IsAcceptedCommittedRespawnSignal(in signal, pendingSequence, playerHash))
                    continue;

                ApplyRespawnReconciliationSurvival();
                _lastAppliedRespawnReconciliationSequence = pendingSequence;
                _pendingRespawnReconciliationSequence = 0u;
                return;
            }
        }

        private void ClearPendingRespawnReconciliation()
        {
            _pendingRespawnReconciliationSequence = 0u;
        }

        private void PublishRespawnedSurvivalState()
        {
            RefreshSurvivalIdentityCold();
            RefreshSurvivalStatusMask();
            ForceAllDirty();
            PublishRuntimeContextState();
            PublishHeadlessUIState();
            PublishDirty();
            PublishSurvivalVitalsChanged(
                SurvivalVitalsChangedSignalFlags.Thermal |
                SurvivalVitalsChangedSignalFlags.Injury);
            WriteSurvivalBlackboxSnapshot();
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

            if ((_injuryStatus & PlayerInjuryStatus.Bleeding) != 0)
            {
                QueueLegacyBleedingStatus();
                ClearLegacyBleedingState(ref injuryChanged);
            }

            if ((_injuryStatus & PlayerInjuryStatus.Fracture) != 0)
            {
                QueueLegacyFractureStatus();
                ClearLegacyFractureState(ref injuryChanged);
            }

            if (!HasFracture && _fracturePenalty01 > 0f)
            {
                _fracturePenalty01 = 0f;
                injuryChanged = true;
            }

            if (injuryChanged)
                NotifyInjuryVitalsChanged();
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
                ResolveSurvivalRuntimePosition(),
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
            float crushAccelerationDamage = ResolveCrushDepthAccelerationDamage(overpressureMeters);
            float pressureDamagePerSecond =
                stats.PressureDamageRate *
                (1f + (overpressureMeters + crushAccelerationDamage) * stats.PressureScalePerMeter) *
                pressureDamageScale;
            return SafeNonNegative(pressureDamagePerSecond * DynamicDifficultyDirector.Current.DamageMultiplier);
        }

        internal static float ResolveCrushDepthAccelerationDamage(float overDepthMeters)
        {
            float overDepth = math.max(0f, math.isfinite(overDepthMeters) ? overDepthMeters : 0f);
            return overDepth > 0f ? overDepth * overDepth * math.rsqrt(overDepth) : 0f;
        }

        private float ResolvePressureExposureSeverity01()
        {
            if (stats == null)
                return 0f;

            float overpressureSeverity = ResolveOverpressureSeverity01();
            float damageSeverity = ResolveSafeRatio01(ResolveCurrentPressureDamagePerSecond(), Mathf.Max(1f, stats.MaxIntegrity * 0.08f));
            return SafeSaturate(overpressureSeverity * 0.65f + damageSeverity * 0.35f);
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
            _bleedingSeverity01 = 0f;
            _fracturePenalty01 = 0f;
            ClearCombatStatusReadModel(CombatStatusBits.Bleeding64 | CombatStatusBits.Fractured64);
            NotifyInjuryVitalsChanged();
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
            _toxicityStaminaMultiplier = 1f;
            _toxicity01 = 0f;
            _statusMask = 0u;
            _cachedCombatStatusMask = 0UL;
            _hasCachedCombatStatusMask = false;
            _decompressionVomitToolDropCooldown = 0f;
            ResetNitrogenNarcosisState();
            _oxygenGraceVisionBlur01 = 0f;
        }

        private void ResetNitrogenNarcosisState()
        {
            _nitrogenBuildUp = 0f;
            _nitrogenLoad = NitrogenBaselinePressureAtm;
            _nitrogenNarcosis01 = 0f;
            _nitrogenLoadWarningIssued = false;
            _nitrogenLoadNotificationRetryFrame = 0;
            if (_playerMovement != null)
                _playerMovement.SetRuntimeNarcosisInputNoise(0f);
            QueueNarcosisShaderScalar(0f);
            ApplyNitrogenMovementPenalty();
        }

#if UNITY_EDITOR
        private void TryBootstrapInjectedSurvivalDatabase()
        {
            if (survivalDatabaseSource == null)
                return;

            if (!TryInjectSurvivalDatabase(survivalDatabaseSource))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[HectonSurvival] Failed to parse injected survival database source. Item parameter lookup disabled.");
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

            // COLD ALLOC: List<SurvivalDatabaseItemParameters>[256] - injected survival database row staging during cold parse - owner: HectonSurvivalSystem
            List<SurvivalDatabaseItemParameters> parsedRows = new List<SurvivalDatabaseItemParameters>(SurvivalDatabaseRowCapacity);
            // COLD ALLOC: Dictionary<string, int>[16] - survival database header column map during cold parse - owner: HectonSurvivalSystem
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

            // COLD ALLOC: Dictionary<string, int>[parsedRows.Count] - StableId to injected item-parameter index map - owner: HectonSurvivalSystem
            parsedLookup = new Dictionary<string, int>(parsedRows.Count, StringComparer.Ordinal);
            for (int i = 0; i < parsedRows.Count; i++)
            {
                string stableId = parsedRows[i].StableId;
                if (parsedLookup.ContainsKey(stableId))
                    return false;

                parsedLookup.Add(stableId, i);
            }

            // COLD ALLOC: SurvivalDatabaseItemParameters[parsedRows.Count] - immutable injected item-parameter snapshot - owner: HectonSurvivalSystem
            parsedItems = new SurvivalDatabaseItemParameters[parsedRows.Count];
            parsedRows.CopyTo(parsedItems);
            return true;
        }

        private static void PopulateSurvivalDatabaseColumnLookup(string headerLine, Dictionary<string, int> columnLookup)
        {
            columnLookup.Clear();
            ReadOnlySpan<char> headerSpan = headerLine.AsSpan();
            int tokenCursor = 0;
            int tokenIndex = 0;
            while (TryReadNextDelimitedToken(headerSpan, ref tokenCursor, '|', out ReadOnlySpan<char> token))
            {
                ReadOnlySpan<char> trimmedToken = TrimSurvivalDatabaseSpan(token);
                if (trimmedToken.Length != 0)
                    TryAddSurvivalColumn(trimmedToken, tokenIndex, columnLookup);

                tokenIndex++;
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
            ReadOnlySpan<char> rowSpan = rowLine.AsSpan();

            if (!TryGetRequiredColumnValue(rowSpan, columnLookup, "StableId", out string stableId) ||
                !TryGetRequiredColumnValue(rowSpan, columnLookup, "Hash", out string hashToken) ||
                !TryGetRequiredColumnValue(rowSpan, columnLookup, "MassKg", out string massToken) ||
                !TryGetRequiredColumnValue(rowSpan, columnLookup, "VolumeL", out string volumeToken) ||
                !TryGetRequiredColumnValue(rowSpan, columnLookup, "EnergyDensityMJkg", out string energyDensityToken) ||
                !TryGetRequiredColumnValue(rowSpan, columnLookup, "BaseDurability", out string durabilityToken))
            {
                return false;
            }

            if (!TryParseStableHash(hashToken.AsSpan(), out uint stableHash) ||
                !TryParseSurvivalFloat(massToken.AsSpan(), out float massKilograms) ||
                !TryParseSurvivalFloat(volumeToken.AsSpan(), out float volumeLiters) ||
                !TryParseSurvivalFloat(energyDensityToken.AsSpan(), out float energyDensityMegajoulesPerKilogram) ||
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

        private static void TryAddSurvivalColumn(
            ReadOnlySpan<char> token,
            int tokenIndex,
            Dictionary<string, int> columnLookup)
        {
            if (token.SequenceEqual("StableId".AsSpan()))
                TryAddSurvivalColumnName("StableId", tokenIndex, columnLookup);
            else if (token.SequenceEqual("Hash".AsSpan()))
                TryAddSurvivalColumnName("Hash", tokenIndex, columnLookup);
            else if (token.SequenceEqual("MassKg".AsSpan()))
                TryAddSurvivalColumnName("MassKg", tokenIndex, columnLookup);
            else if (token.SequenceEqual("VolumeL".AsSpan()))
                TryAddSurvivalColumnName("VolumeL", tokenIndex, columnLookup);
            else if (token.SequenceEqual("EnergyDensityMJkg".AsSpan()))
                TryAddSurvivalColumnName("EnergyDensityMJkg", tokenIndex, columnLookup);
            else if (token.SequenceEqual("BaseDurability".AsSpan()))
                TryAddSurvivalColumnName("BaseDurability", tokenIndex, columnLookup);
        }

        private static void TryAddSurvivalColumnName(
            string columnName,
            int tokenIndex,
            Dictionary<string, int> columnLookup)
        {
            if (!columnLookup.ContainsKey(columnName))
                columnLookup.Add(columnName, tokenIndex);
        }

        private static bool TryGetRequiredColumnValue(
            ReadOnlySpan<char> row,
            Dictionary<string, int> columnLookup,
            string columnName,
            out string value)
        {
            value = null;

            if (!columnLookup.TryGetValue(columnName, out int columnIndex))
                return false;

            int tokenIndex = 0;
            int tokenCursor = 0;
            while (tokenCursor <= row.Length)
            {
                int tokenStart = tokenCursor;
                int tokenEnd = tokenStart;
                while (tokenEnd < row.Length && row[tokenEnd] != '|')
                    tokenEnd++;

                if (tokenIndex == columnIndex)
                {
                    ReadOnlySpan<char> trimmed = TrimSurvivalDatabaseSpan(row.Slice(tokenStart, tokenEnd - tokenStart));
                    if (trimmed.Length == 0)
                        return false;

                    value = trimmed.ToString();
                    return true;
                }

                tokenIndex++;
                if (tokenEnd >= row.Length)
                    break;

                tokenCursor = tokenEnd + 1;
            }

            return false;
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
            // COLD ALLOC: SurvivalDatabaseItemRecord[256] - injected survival database row staging during cold parse - owner: HectonSurvivalSystem
            NativeArray<SurvivalDatabaseItemRecord> stagingRows = H8Memory.Allocate<SurvivalDatabaseItemRecord>(
                SurvivalDatabaseRowCapacity,
                SystemID.GameplayPlayer,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            if (!stagingRows.IsCreated)
                return false;

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
                        ReleaseSurvivalDatabaseRows(ref stagingRows);
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
                    ReleaseSurvivalDatabaseRows(ref stagingRows);
                    return false;
                }

                if (!TryParseSurvivalDatabaseRowFlat(trimmedLine, in columnMap, out SurvivalDatabaseItemRecord row))
                {
                    ReleaseSurvivalDatabaseRows(ref stagingRows);
                    return false;
                }

                stagingRows[parsedItemCount++] = row;
            }

            if (!headerFound || parsedItemCount == 0)
            {
                ReleaseSurvivalDatabaseRows(ref stagingRows);
                return false;
            }

            for (int i = 0; i < parsedItemCount; i++)
            {
                for (int j = i + 1; j < parsedItemCount; j++)
                {
                    if (stagingRows[i].StableHash == stagingRows[j].StableHash)
                    {
                        ReleaseSurvivalDatabaseRows(ref stagingRows);
                        return false;
                    }
                }
            }

            // COLD ALLOC: SurvivalDatabaseItemRecord[parsedRowCount] - immutable injected item-parameter snapshot - owner: HectonSurvivalSystem
            parsedItems = H8Memory.Allocate<SurvivalDatabaseItemRecord>(
                parsedItemCount,
                SystemID.GameplayPlayer,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            if (!parsedItems.IsCreated)
            {
                ReleaseSurvivalDatabaseRows(ref stagingRows);
                parsedItemCount = 0;
                return false;
            }

            for (int i = 0; i < parsedItemCount; i++)
                parsedItems[i] = stagingRows[i];

            ReleaseSurvivalDatabaseRows(ref stagingRows);
            return true;
        }

        private static void ReleaseSurvivalDatabaseRows(ref NativeArray<SurvivalDatabaseItemRecord> rows)
        {
            H8Memory.Release(ref rows, SystemID.GameplayPlayer);
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
            uint stableIdHash = 0u;
            uint stableHash = 0u;
            float massKilograms = 0f;
            float volumeLiters = 0f;
            float energyDensityMegajoulesPerKilogram = 0f;
            int baseDurability = 0;
            bool hasStableId = false;
            bool hasHash = false;
            bool hasMass = false;
            bool hasVolume = false;
            bool hasEnergyDensity = false;
            bool hasDurability = false;
            int tokenCursor = 0;
            int tokenIndex = 0;

            while (tokenCursor <= rowLine.Length)
            {
                int tokenStart = tokenCursor;
                int tokenEnd = tokenStart;
                while (tokenEnd < rowLine.Length && rowLine[tokenEnd] != '|')
                    tokenEnd++;

                ReadOnlySpan<char> trimmedToken = TrimSurvivalDatabaseSpan(rowLine.Slice(tokenStart, tokenEnd - tokenStart));
                if (tokenIndex == columnMap.StableId)
                {
                    hasStableId = trimmedToken.Length > 0;
                    if (hasStableId)
                        stableIdHash = ComputeStableIdHash(trimmedToken);
                }
                else if (tokenIndex == columnMap.Hash)
                {
                    hasHash = trimmedToken.Length > 0 &&
                              TryParseStableHash(trimmedToken, out stableHash);
                }
                else if (tokenIndex == columnMap.MassKilograms)
                {
                    hasMass = trimmedToken.Length > 0 &&
                              TryParseSurvivalFloat(trimmedToken, out massKilograms);
                }
                else if (tokenIndex == columnMap.VolumeLiters)
                {
                    hasVolume = trimmedToken.Length > 0 &&
                                TryParseSurvivalFloat(trimmedToken, out volumeLiters);
                }
                else if (tokenIndex == columnMap.EnergyDensityMegajoulesPerKilogram)
                {
                    hasEnergyDensity = trimmedToken.Length > 0 &&
                                       TryParseSurvivalFloat(trimmedToken, out energyDensityMegajoulesPerKilogram);
                }
                else if (tokenIndex == columnMap.BaseDurability)
                {
                    hasDurability = trimmedToken.Length > 0 &&
                                    int.TryParse(trimmedToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out baseDurability);
                }

                tokenIndex++;
                if (tokenEnd >= rowLine.Length)
                    break;

                tokenCursor = tokenEnd + 1;
            }

            if (!hasStableId || !hasHash || !hasMass || !hasVolume || !hasEnergyDensity || !hasDurability)
                return false;

            if (stableIdHash != stableHash)
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

        private static bool TryParseSurvivalFloat(ReadOnlySpan<char> token, out float value)
        {
            value = 0f;
            ReadOnlySpan<char> text = TrimSurvivalDatabaseSpan(token);
            if (text.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            char first = text[0];
            if (first == '-' || first == '+')
            {
                negative = first == '-';
                index++;
                if (index >= text.Length)
                    return false;
            }

            double result = 0d;
            bool hasDigit = false;
            while (index < text.Length && IsAsciiDigit(text[index]))
            {
                result = (result * 10d) + (text[index] - '0');
                index++;
                hasDigit = true;
            }

            if (index < text.Length && text[index] == '.')
            {
                index++;
                double scale = 0.1d;
                while (index < text.Length && IsAsciiDigit(text[index]))
                {
                    result += (text[index] - '0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                index++;
                bool exponentNegative = false;
                if (index < text.Length && (text[index] == '-' || text[index] == '+'))
                {
                    exponentNegative = text[index] == '-';
                    index++;
                }

                int exponent = 0;
                bool hasExponentDigit = false;
                while (index < text.Length && IsAsciiDigit(text[index]))
                {
                    exponent = math.min(38, (exponent * 10) + (text[index] - '0'));
                    index++;
                    hasExponentDigit = true;
                }

                if (!hasExponentDigit)
                    return false;

                result = ScaleByFloatPow10(result, exponentNegative ? -exponent : exponent);
            }

            if (index != text.Length)
                return false;

            if (negative)
                result = -result;

            if (double.IsNaN(result) || double.IsInfinity(result) || result > float.MaxValue || result < -float.MaxValue)
                return false;

            value = (float)result;
            return math.isfinite(value);
        }

        private static bool IsAsciiDigit(char value)
        {
            return (uint)(value - '0') <= 9u;
        }

        private static double ScaleByFloatPow10(double value, int exponent)
        {
            if (value == 0d || exponent == 0)
                return value;
            if (exponent > 38)
                return value > 0d ? double.PositiveInfinity : double.NegativeInfinity;
            if (exponent < -46)
                return 0d;

            int count = exponent < 0 ? -exponent : exponent;
            double scale = 1d;
            for (int i = 0; i < count; i++)
                scale *= 10d;

            return exponent < 0 ? value / scale : value * scale;
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
            if (cursor >= source.Length)
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
#endif

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
            overpressureMeters = SafeNonNegative(overpressureMeters);
            effectiveSafeDepthMeters = SafeNonNegative(effectiveSafeDepthMeters);
            if (overpressureMeters <= 0f)
                return 0f;

            float fullSeverityRange = math.max(
                OverpressureSeverityFullRangeMeters,
                math.max(1f, effectiveSafeDepthMeters) * OverpressureSeveritySafeDepthScale);
            return math.saturate(overpressureMeters / fullSeverityRange);
        }

        private void NotifyInjuryVitalsChanged()
        {
            PublishSurvivalVitalsChanged(SurvivalVitalsChangedSignalFlags.Injury);
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

            _bleedingSeverity01 = Mathf.Max(_bleedingSeverity01, severityScale);
            _injuryStatus &= ~PlayerInjuryStatus.Bleeding;

            if (QueueBleedingStatus(severityScale, duration, damagePerSecond))
                NotifyInjuryVitalsChanged();
        }

        private bool QueueLegacyBleedingStatus()
        {
            float severity = math.saturate(math.max(0.1f, _bleedingSeverity01));
            float duration = LerpClamped(BleedingBaseDurationSeconds, BleedingMaxDurationSeconds, severity);
            float damagePerSecond = LerpClamped(BleedingBaseDamagePerSecond, BleedingMaxDamagePerSecond, severity);
            return QueueBleedingStatus(severity, duration, damagePerSecond);
        }

        private bool QueueLegacyFractureStatus()
        {
            float penalty = math.saturate(math.max(0.1f, _fracturePenalty01));
            float severity = Mathf.InverseLerp(FractureBasePenalty, FractureMaxPenalty, penalty);
            float duration = LerpClamped(FractureBaseDurationSeconds, FractureMaxDurationSeconds, severity);
            return QueueFractureStatus(penalty, duration);
        }

        private bool QueueBleedingStatus(float severity01, float durationSeconds, float damagePerSecond)
        {
            float severity = math.saturate(severity01);
            if (severity <= 0.0001f)
                return false;

            int targetId = ResolveCachedCombatTargetId();
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            float duration = math.max(0.1f, durationSeconds);
            float magnitude = math.max(0f, math.select(0f, damagePerSecond, math.isfinite(damagePerSecond)));
            bool queued = CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Bleeding64,
                duration,
                DamageSourceIds.EnvironmentHazard,
                math.max(severity, magnitude));
            if (queued)
                MarkCombatStatusReadModel(CombatStatusBits.Bleeding64);
            return queued;
        }

        private bool QueueFractureStatus(float penalty01, float durationSeconds)
        {
            float penalty = math.saturate(penalty01);
            if (penalty <= 0.0001f)
                return false;

            int targetId = ResolveCachedCombatTargetId();
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            bool queued = CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Fractured64,
                math.max(0.1f, durationSeconds),
                DamageSourceIds.EnvironmentHazard,
                penalty);
            if (queued)
                MarkCombatStatusReadModel(CombatStatusBits.Fractured64);
            return queued;
        }

        private void ClearLegacyBleedingState(ref bool injuryChanged)
        {
            _injuryStatus &= ~PlayerInjuryStatus.Bleeding;
            injuryChanged = true;
        }

        private void ClearLegacyFractureState(ref bool injuryChanged)
        {
            _injuryStatus &= ~PlayerInjuryStatus.Fracture;
            injuryChanged = true;
        }

        private PlayerInjuryStatus ResolveCurrentInjuries()
        {
            PlayerInjuryStatus injuries = (PlayerInjuryStatus)((byte)_injuryStatus & SaveData.PlayerInjurySupportedFlagMask);
            if (HasCachedCombatStatusEffect(CombatStatusBits.Bleeding64))
                injuries |= PlayerInjuryStatus.Bleeding;
            if (HasCachedCombatStatusEffect(CombatStatusBits.Fractured64))
                injuries |= PlayerInjuryStatus.Fracture;
            return injuries;
        }

        private void RefreshCombatStatusMaskCache()
        {
            int targetId = _combatTargetId;
            if (targetId == 0)
            {
                _cachedCombatStatusMask = 0UL;
                _hasCachedCombatStatusMask = false;
                return;
            }

            if (!CombatDamageRuntime.TryGetStatusEffectMask(targetId, out ulong activeMask))
                return;

            _cachedCombatStatusMask = activeMask & CombatStatusBits.KnownRuntimeMask64;
            _hasCachedCombatStatusMask = true;
        }

        private void MarkCombatStatusReadModel(ulong statusMask)
        {
            _cachedCombatStatusMask |= statusMask & CombatStatusBits.KnownRuntimeMask64;
            _hasCachedCombatStatusMask = true;
        }

        private void ClearCombatStatusReadModel(ulong statusMask)
        {
            _cachedCombatStatusMask &= ~(statusMask & CombatStatusBits.KnownRuntimeMask64);
            _hasCachedCombatStatusMask = _cachedCombatStatusMask != 0UL;
        }

        private bool HasCachedCombatStatusEffect(ulong statusMask)
        {
            return _hasCachedCombatStatusMask && (_cachedCombatStatusMask & statusMask) != 0UL;
        }

        private void ApplyFracture(float severity01, float damageMagnitude)
        {
            float severityScale = math.saturate(math.max(severity01, damageMagnitude / SeverePhysicalDamageThreshold));
            float duration = LerpClamped(FractureBaseDurationSeconds, FractureMaxDurationSeconds, severityScale);
            float penalty = LerpClamped(FractureBasePenalty, FractureMaxPenalty, severityScale);
            bool wasFractured = HasFracture;
            float previousPenalty = _fracturePenalty01;

            if (!QueueFractureStatus(penalty, duration))
                return;

            _injuryStatus &= ~PlayerInjuryStatus.Fracture;
            _fracturePenalty01 = Mathf.Max(_fracturePenalty01, penalty);

            if (!wasFractured || _fracturePenalty01 > previousPenalty)
                NotifyInjuryVitalsChanged();
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
    
        #region JulesLink_CaloricDeficitPenaltyCalculator
        private static void JulesLink_CaloricDeficitPenaltyCalculator() { _ = typeof(Hecton8.PureLogic.Systems.CaloricDeficitPenaltyCalculator); }
        #endregion
}
}
