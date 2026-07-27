using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Audio;
using Hecton.Localization;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Caves;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Owns abyssal hydrothermal updraft sampling, heat-hazard registration, cable entanglement metadata,
    /// and the indirect black-smoke plume simulation used by deep thermal vents.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-102)]
    public sealed class AbyssalThermalManager : MonoBehaviour, ITickable, ISlowTickable, IFixedTickable, ILateFrameTickable, IOriginShiftListener, IThermodynamicsService, IRandomEventListener, global::Hecton8.Gameplay.ILaserCutterEventListener, IGlobalRegistryHotSwapListener
    {
        private static AbyssalThermalManager s_activeRuntimeInstance;
        private static int s_x001AbyssalThermalManagerSignalPushDropCount;
        private const float DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;

        internal static AbyssalThermalManager ActiveRuntimeInstance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            s_activeRuntimeInstance = null;
            Volatile.Write(ref s_x001AbyssalThermalManagerSignalPushDropCount, 0);
        }

        public struct ThermalFlowSample
        {
            public byte HasFlow;
            public Vector3 FlowVelocityWS;
            public float Heat01;
            public float DragMultiplier;
            public byte IsCableZone;
            public Vector3 CableAnchorWS;
            public float CableTension01;
            public float CableCutProgress01;
            public float CableEscapeSuppression01;
        }

        private struct ThermalVentState
        {
            public Vector3 PositionWS;
            public Vector3 CableAnchorWS;
            public float RadiusWS;
            public float HeightWS;
            public float UpdraftVelocity;
            public float HeatIntensity;
            public float SmokeDensity;
            public float CableRadiusWS;
            public int HazardSourceId;
        }

        private struct RuntimeVentRegistration
        {
            public long RuntimeKey;
            public Vector3 PositionWS;
            public Vector3 CableAnchorWS;
            public float RadiusWS;
            public float HeightWS;
            public float UpdraftVelocity;
            public float HeatIntensity;
            public float SmokeDensity;
            public float CableRadiusWS;
        }

        private struct ThermalCrystallizationSample
        {
            public float3 PositionWS;
            public float PreviousTemperatureCelsius;
            public float CurrentTemperatureCelsius;
            public float RadiusMeters;
            public uint SourceId;
            public byte Pending;
        }

        private struct ThermalCrystallizationResult
        {
            public float3 PositionWS;
            public float DeltaTemperatureCelsius;
            public float RadiusMeters;
            public uint SourceId;
            public byte ShouldSpawn;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct ThermalVentGpuData
        {
            [FieldOffset(0)] public Vector3 PositionWS;
            [FieldOffset(12)] public float RadiusWS;
            [FieldOffset(16)] public float HeightWS;
            [FieldOffset(20)] public float UpdraftVelocity;
            [FieldOffset(24)] public float HeatIntensity;
            [FieldOffset(28)] public float SmokeDensity;
            [FieldOffset(32)] public Vector2 Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct AshParticleData
        {
            [FieldOffset(0)] public Vector3 PositionWS;
            [FieldOffset(12)] public float Size;
            [FieldOffset(16)] public Vector3 VelocityWS;
            [FieldOffset(28)] public float Alpha;
            [FieldOffset(32)] public float Lifetime;
            [FieldOffset(36)] public float MaxLifetime;
            [FieldOffset(40)] public float Seed;
            [FieldOffset(44)] public float VentIndex;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AbyssalThermalManagerTelemetryEntry
        {
            [FieldOffset(0)] public double3 PositionAup;
            [FieldOffset(24)] public long Frame;
            [FieldOffset(32)] public ulong Sequence;
            [FieldOffset(40)] public float TemperatureCelsius;
            [FieldOffset(44)] public float Heat01;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public int ActiveVentCount;
            [FieldOffset(56)] public uint FailureCode;
            [FieldOffset(60)] private uint _pad0;
        }

        private struct EmpNestState
        {
            public Vector3 PositionWS;
            public float RadiusWS;
            public float Charge01;
            public float Cooldown;
            public float PulsePhase;
            public int SourceVentIndex;
        }

        private static readonly int _ParticlesReadId = Shader.PropertyToID("_ParticlesRead");
        private static readonly int _ParticlesWriteId = Shader.PropertyToID("_ParticlesWrite");
        private static readonly int _ThermalVentsId = Shader.PropertyToID("_ThermalVents");
        private static readonly int _ParticleCountId = Shader.PropertyToID("_ParticleCount");
        private static readonly int _ActiveVentCountId = Shader.PropertyToID("_ActiveVentCount");
        private static readonly int _DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int _SimulationTimeId = Shader.PropertyToID("_SimulationTime");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_CameraPositionWS");
        private static readonly int _CameraRightId = Shader.PropertyToID("_CameraRightWS");
        private static readonly int _CameraUpId = Shader.PropertyToID("_CameraUpWS");
        private static readonly int _ParticleSizeRangeId = Shader.PropertyToID("_ParticleSizeRange");
        private static readonly int _NoiseParamsId = Shader.PropertyToID("_NoiseParams");
        private static readonly int _MaxViewDistanceId = Shader.PropertyToID("_MaxViewDistance");
        private static readonly int _AshParticlesId = Shader.PropertyToID("_AshParticles");
        private static readonly int _AshTintId = Shader.PropertyToID("_AshTint");
        private static readonly int _AshHotTintId = Shader.PropertyToID("_AshHotTint");
        private static readonly int _SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int _ThermalMapActiveId = Shader.PropertyToID("_HectonThermalMapActive");
        private static readonly int _ThermalMapTextureId = Shader.PropertyToID("_HectonThermalMapTexture");
        private static readonly int _ThermalMapOriginCellSizeId = Shader.PropertyToID("_HectonThermalMapOriginCellSize");
        private static readonly int _ThermalMapWorldRectId = Shader.PropertyToID("_HectonThermalMapWorldRect");
        private static readonly int _LocalThermalHeatId = Shader.PropertyToID("_HectonLocalThermalHeat01");
        private static readonly int _LocalThermalTemperatureId = Shader.PropertyToID("_HectonLocalThermalTemperatureCelsius");
        private static readonly int _ThermalCondensationId = Shader.PropertyToID("_HectonThermalCondensation01");
        private static readonly int _ThermalBubbleCommandCountId = Shader.PropertyToID("_HectonThermalBubbleCommandCount");
        private static readonly int _ThermalBubbleCommandDataId = Shader.PropertyToID("_HectonThermalBubbleCommands");

        private const int MaxVentCapacity = 16;
        private const int MaxAnchorScanCapacity = 32;
        private const int MaxSmokeParticleCapacity = 8192;
        private const int MaxEmpNestCapacity = 8;
        private const int MaxCrystallizationSampleCapacity = 32;
        private const int ThermalGridResolution = 32;
        private const int ThermalMapResolution = ThermalGridResolution;
        private const int ThermalMapPlaneCellCount = ThermalMapResolution * ThermalMapResolution;
        private const int ThermalMapCellCount = ThermalGridResolution * ThermalGridResolution * ThermalGridResolution;
        private const int ThermalMapDiffusionSliceCount = 8;
        private const int ThermalMapDiffusionSliceMask = ThermalMapDiffusionSliceCount - 1;
        private const int ThermalMapDiffusionSliceCellCount = ThermalMapCellCount / ThermalMapDiffusionSliceCount;
        private const int ThermalMapDiffusionJobBatchSize = 64;
        private const int ThermalMapAxisShift = 5;
        private const uint KccVelocityThermalMaxAgeFrames = 12u;
        private const int ThermalGridSaveRleCapacity = ThermalMapCellCount;
        private const int ThermalTelemetryCapacity = 300;
        private const int ThermalTelemetryDumpHeaderBytes = sizeof(int) * 2;
        private const int ThermalTelemetryDumpEntryBytes = (sizeof(double) * 3) +
                                                           sizeof(long) +
                                                           sizeof(ulong) +
                                                           (sizeof(float) * 2) +
                                                           sizeof(uint) +
                                                           sizeof(int) +
                                                           sizeof(uint);
        private const int ThermalTelemetryDumpPayloadBytes =
            ThermalTelemetryDumpHeaderBytes + ThermalTelemetryCapacity * ThermalTelemetryDumpEntryBytes;
        private const int VentBufferRingSize = 3;
        private const SystemID ThermalVaultOwnerSystem = SystemID.Thermodynamics;
        private const int PortableMaxComputeThreadsPerGroup = 256;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const float VentStateCompareEpsilon = 0.01f;
        private const float DefaultThermalMapWorldSizeMeters = 192f;
        private const float DefaultThermalMapColdTickSeconds = 1f;
        private const float DefaultAmbientWaterTemperatureCelsius = 2f;
        private const float DeepBrineDepthMeters = -1000f;
        private const float DeepBrineAmbientWaterTemperatureCelsius = -2f;
        private const float ThermalVentInjectionDeltaCelsius = 200f;
        private const float ThermalShockHotThresholdCelsius = 100f;
        private const float ThermalShockColdThresholdCelsius = -5f;
        private const float ThermalShockDamageMagnitude = 14f;
        private const float ThermalGridSurvivalCostWeight01 = 0.18f;
        private const float ThermalGridSurvivalVramWeight01 = 0.35f;
        private const float ThermalGridSurvivalColdTickMultiplier = 4f;
        private const float ThermalGridSurvivalDiffusionScale = 0.45f;
        private const int ThermalGridMinimumVramMb = 1024;
        private const int ThermalGridFullVramMb = 3072;
        private const float SubmarineColdSpeedMultiplier = 0.7f;
        private const float BoilingDamageThresholdCelsius = 80f;
        private const float PlayerEquivalentMassKg = 80f;
        private const float FaunaThermalAvoidanceThresholdCelsius = 50f;
        private const uint ThermalTelemetryFlagHeatSource = 1u << 0;
        private const uint ThermalTelemetryFlagPlayerAmbientTemp = 1u << 1;
        private const uint ThermalTelemetryFlagThermalShock = 1u << 2;
        private const string ThermalTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_THERMODYNAMICS_LEAD.bin";
        private const byte ThermalShockAcousticChannel = 11;
        private const Hecton8.Core.Memory.BufferID ThermalMapReadCelsiusBufferId = Hecton8.Core.Memory.BufferID.AbyssalThermalManager_ThermalMapReadCelsiusBufferId;
        private const Hecton8.Core.Memory.BufferID ThermalMapWriteCelsiusBufferId = Hecton8.Core.Memory.BufferID.AbyssalThermalManager_ThermalMapWriteCelsiusBufferId;
        private const Hecton8.Core.Memory.BufferID ThermalMapSourceCelsiusBufferId = Hecton8.Core.Memory.BufferID.AbyssalThermalManager_ThermalMapSourceCelsiusBufferId;
        private const Hecton8.Core.Memory.BufferID ThermalMapInsulationBufferId = Hecton8.Core.Memory.BufferID.AbyssalThermalManager_ThermalMapInsulationBufferId;
        private static readonly ulong ThermalMapReadbackMutationGuardMask =
            ThermalVaultMutationGuardBit(ThermalMapReadCelsiusBufferId);
        private const uint ThermalHashSeed = 0xC6BC2796u;
        private const float ThermalSpatialEventLifetimeSeconds = 1.25f;
        private const float DryAirDensityKilogramsPerCubicMeter = 1.225f;
        private const float DryAirHeatCapacityJoulesPerKilogramKelvin = 1005f;
        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Compute shader that simulates the hydrothermal ash plume.")]
        private ComputeShader blackSmokeCompute;

        [SerializeField]
        [Tooltip("Transparent billboard material used by the direct thermal ash primitive draw.")]
        private Material blackSmokeMaterial;

        [SerializeField]
        [Tooltip("Optional direct biome director override. Runtime resolves the active singleton when null.")]
        private BiomeMatrixDirector biomeMatrixDirector;

        [SerializeField]
        [Tooltip("Optional direct zone director override. Runtime resolves the active singleton when null.")]
        private WorldZoneDirector worldZoneDirector;

        [SerializeField]
        [Tooltip("Optional direct resource distribution owner used for flash-freeze Thermal Diamond spawns.")]
        private ResourceDistributionDirector resourceDistributionDirector;

        [SerializeField]
        [Tooltip("Optional direct vegetation bridge used for abyssal thermal-grid sampling.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional direct cut-manager override used when abyssal cables need to be severed by the cutter.")]
        private SargassumCutManager cutManager;

        [SerializeField]
        [Tooltip("Optional direct fluid decal manager override. Runtime resolves a local component when null.")]
        private AbyssalFluidDecalManager fluidDecalManager;

        [SerializeField]
        [Tooltip("Shared authored material assigned to the abyssal fluid decal draw pass. Runtime material creation is forbidden.")]
        private Material fluidDecalMaterial;

        [SerializeField]
        [Tooltip("Optional direct player override used for isolated validation scenes.")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Optional direct camera override for procedural smoke visibility and billboarding.")]
        private Camera viewCamera;

        [Header("── Thermal Vent Field ───────────────")]
        [SerializeField, Range(900f, 6000f)]
        [Tooltip("Minimum evaluated depth in meters before hydrothermal vents are allowed to arm.")]
        private float abyssalVentStartDepthMeters = 950f;

        [SerializeField, Range(1, MaxVentCapacity)]
        [Tooltip("Hard cap for active thermal vents registered into the local abyssal field.")]
        private int maxActiveVentCount = 10;

        [SerializeField, Range(1, 4)]
        [Tooltip("Maximum deterministic vent chimneys authored around one qualifying cartographer zone.")]
        private int maxVentsPerAnchor = 2;

        [SerializeField, Range(0.1f, 0.9f)]
        [Tooltip("Normalized fraction of the zone activation radius used when placing thermal vents around an anchor.")]
        private float ventAnchorRadiusFraction = 0.32f;

        [SerializeField, Range(2f, 30f)]
        [Tooltip("Minimum world-space hydrothermal vent radius.")]
        private float ventRadiusMin = 5f;

        [SerializeField, Range(4f, 40f)]
        [Tooltip("Maximum world-space hydrothermal vent radius.")]
        private float ventRadiusMax = 13f;

        [SerializeField, Range(4f, 90f)]
        [Tooltip("Vertical plume height used for updraft influence and smoke falloff.")]
        private float ventHeight = 24f;

        [SerializeField, Range(1f, 40f)]
        [Tooltip("Peak upward flow velocity contributed by the vent core before ocean-current blending.")]
        private float ventUpdraftVelocity = 14f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Additional drag multiplier applied inside the strongest updraft core.")]
        private float ventDragMultiplier = 1.55f;

        [SerializeField, Range(1f, 60f)]
        [Tooltip("Heat intensity registered into HectonHazardManager for each vent.")]
        private float ventHeatIntensity = 18f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Multiplier that expands the heat-hazard radius beyond the raw updraft radius.")]
        private float ventHeatRadiusMultiplier = 1.2f;

        [SerializeField, Range(0.5f, 18f)]
        [Tooltip("Seconds that all active hydrothermal vents stay in an eruptive state after a seismic trench opens.")]
        private float seismicEruptionDuration = 7f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Multiplier applied to vent updraft velocity while the seismic eruption window is active.")]
        private float seismicEruptionUpdraftMultiplier = 2.2f;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Multiplier applied to vent heat intensity while the seismic eruption window is active.")]
        private float seismicEruptionHeatMultiplier = 3.25f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Multiplier applied to vent smoke density while the seismic eruption window is active.")]
        private float seismicEruptionSmokeMultiplier = 2.4f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Multiplier applied to vent pillar height while the seismic eruption window is active.")]
        private float seismicEruptionHeightMultiplier = 1.75f;

        [Header("── Thermal Map & Gameplay ───────────────")]
        [SerializeField, Range(32f, 512f)]
        [Tooltip("World-space square covered by the 16x16 coarse thermal map around the active player/submarine area.")]
        private float thermalMapWorldSizeMeters = DefaultThermalMapWorldSizeMeters;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Seconds between thermal-map ColdTick solves. Prompt target is 1Hz.")]
        private float thermalMapColdTickSeconds = DefaultThermalMapColdTickSeconds;

        [SerializeField, Range(0.02f, 1f)]
        [Tooltip("Jacobi blend factor used to blur vent source heat into neighboring cells.")]
        private float thermalMapDiffusion01 = 0.32f;

        [SerializeField, Range(-4f, 12f)]
        [Tooltip("Baseline ambient water temperature used when no vegetation temperature field is available.")]
        private float ambientWaterTemperatureCelsius = DefaultAmbientWaterTemperatureCelsius;

        [SerializeField, Range(1f, 900f)]
        [Tooltip("Velocity-change-per-second scale used by convection before multiplying by heat and rcp(mass).")]
        private float thermalConvectionVelocityPerSecond = 420f;

        [SerializeField, Range(0f, 20f)]
        [Tooltip("Thermal burn damage per second when a target is in boiling water.")]
        private float boilingDamagePerSecond = 4f;

        [SerializeField, Range(1f, 60f)]
        [Tooltip("Temperature jump from cold to hot that triggers visor condensation.")]
        private float condensationTemperatureJumpCelsius = 24f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Decay speed for the visor condensation scalar.")]
        private float condensationDecayPerSecond = 1.6f;

        [SerializeField, Range(2f, 90f)]
        [Tooltip("Deterministic triangle-wave eruption cycle length for each thermal vent.")]
        private float ventEruptionCycleSeconds = 18f;

        [SerializeField, Range(0.05f, 0.95f)]
        [Tooltip("Fraction of the triangle-wave cycle treated as active eruption instead of sleep.")]
        private float ventEruptionDuty01 = 0.42f;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Minimum seconds between deep thermal roar audio pings.")]
        private float thermalRoarCooldownSeconds = 1.1f;

        [Header("── Thermal Crystallization ───────────────")]
        [SerializeField, Range(-300f, -100f)]
        [Tooltip("Required signed Celsius delta current-minus-previous before a thermal boundary can crystallize into Thermal Diamond.")]
        private float crystallizationDeltaTemperatureThresholdCelsius = -100f;

        [SerializeField, Range(300f, 900f)]
        [Tooltip("Minimum magma-side Celsius value required before flash-freeze crystallization is accepted.")]
        private float crystallizationMinimumSourceTemperatureCelsius = 300f;

        [SerializeField, Range(-8f, 8f)]
        [Tooltip("Ambient/current temperature at or below this value can passively flash-freeze hot vent boundaries.")]
        private float freezingCurrentTemperatureCelsius = 0f;

        [SerializeField, Range(18f, 40f)]
        [Tooltip("Converts authored vent heat intensity into Celsius for the crystallization boundary job.")]
        private float ventHeatToCelsiusScale = 18f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Thermal conductivity coefficient used when hydrothermal updraft heat bleeds into habitat modules.")]
        private float habitatThermalConductivityWattsPerSquareMeterKelvin = 0.18f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Sample radius used when testing base modules against active thermal updraft volumes.")]
        private float habitatThermalSampleRadiusMeters = 2f;

        [SerializeField, Range(0.01f, 12f)]
        [Tooltip("Hard cap for Celsius injected into a room from hydrothermal flux per SlowTick.")]
        private float habitatThermalMaxTemperatureDeltaPerSlowTick = 3f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Radius in meters supplied to the Thermal Diamond spawn request after a boundary passes the Burst job.")]
        private float crystallizationNodeRadiusMeters = 1.4f;

        [SerializeField, Range(2f, 120f)]
        [Tooltip("Minimum seconds between passive crystallization attempts per active vent slot.")]
        private float passiveCrystallizationCooldownSeconds = 45f;

        [Header("── Bio-Cable Zones ─────────────────")]
        [SerializeField, Range(0.5f, 1.5f)]
        [Tooltip("Multiplier applied to qualifying cartographer service/power radii when resolving cable entanglement.")]
        private float cableRadiusMultiplier = 0.58f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Additional planar offset applied toward the player when resolving the cable anchor point.")]
        private float cableAnchorPull = 0.8f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Recent-cut query radius used when checking whether a cable snare has been severed by the cutter.")]
        private float cableCutQueryRadius = 1.2f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Normalized recent-cut weight required before a cable snare starts to release.")]
        private float cableCutReleaseThreshold = 0.24f;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Cooldown between automatic cutter-driven cable cut stamps while the beam stays active.")]
        private float cableCutStampInterval = 0.12f;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("World-space radius stamped into the shared cut mask while the laser cutter severs a cable knot.")]
        private float cableCutStampRadius = 1.05f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Strength written into the shared cut mask when a cable knot is being severed.")]
        private float cableCutStampStrength = 0.82f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Forward offset applied from the active cutter transform before stamping the cable sever cut.")]
        private float cableCutForwardOffset = 0.95f;

        [Header("── Bio-Cable Visuals ───────────────")]
        [SerializeField]
        [Tooltip("Shared authored material assigned to BioCableIK line renderers. Runtime material creation is forbidden.")]
        private Material bioCableMaterial;

        [SerializeField]
        [Tooltip("Authored persistent BioCableIK rigs. These are preferred over runtime pool spawn and must be pre-placed or prefab-warmed.")]
        private BioCableIK[] authoredBioCableVisuals;

        [SerializeField]
        [Tooltip("Optional prewarmed pool prefab used only when authoredBioCableVisuals has no rig for a slot.")]
        private BioCableIK bioCablePrefab;

        [SerializeField, Range(0.5f, 24f)]
        [Tooltip("Maximum camera/player distance where procedural bio-cable rigs stay actively simulated.")]
        private float cableVisualActivationDistance = 14f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Approximate scooter hull radius used when resolving cable pull and wrap without per-segment colliders.")]
        private float cableVisualHullRadius = 1.35f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Tension threshold above which the cable starts visually wrapping instead of only stretching.")]
        private float cableVisualWrapThreshold = 0.58f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Extra attraction scale applied when a scooter is active inside a cable snare.")]
        private float cableVisualTransportBoost = 1.25f;

        [SerializeField, Range(0.1f, 24f)]
        [Tooltip("Initial recoil speed injected into a severed cable rig when the cutter finally snaps it.")]
        private float cableSnapRecoilSpeed = 8.5f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Time window where a severed cable keeps rendering recoil motion before it is allowed to fade out.")]
        private float cableSnapDuration = 1.35f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Vertical lift added to severed cable recoil so cut strands flick upward before settling.")]
        private float cableSnapVerticalLift = 0.85f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("How long an elastic over-tension rupture keeps the cable released before it is allowed to re-arm.")]
        private float cableElasticReleaseDuration = 1.45f;

        [Header("── EMP Nests ───────────────────")]
        [SerializeField, Range(1, MaxEmpNestCapacity)]
        [Tooltip("Hard cap for active EMP nests resolved from dense abyssal bio-cable anchors.")]
        private int maxEmpNestCount = 4;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Multiplier applied to cable radii when deriving EMP nest trigger zones.")]
        private float empNestRadiusMultiplier = 0.38f;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Seconds required to fully charge a nest while the active transport remains inside its trigger radius.")]
        private float empChargeDuration = 2.6f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Seconds required to fully bleed nest charge when the player leaves the trigger zone.")]
        private float empDischargeDuration = 1.4f;

        [SerializeField, Range(0.25f, 6f)]
        [Tooltip("Cooldown applied after an EMP nest fires before it may charge again.")]
        private float empCooldown = 3.5f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Sphere radius used by the EMP impulse cast. Keeps the hit test broad without per-frame overlap spam.")]
        private float empSphereCastRadius = 1.3f;

        [SerializeField, Range(1f, 24f)]
        [Tooltip("Maximum cast distance used when firing an EMP pulse toward the active scooter.")]
        private float empPulseRange = 12f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("Duration of the forced 100 percent misfire override injected into the active Manta after an EMP hit.")]
        private float empMisfireDuration = 2.1f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Duration of the forced PDA corrosion window pushed into LocalizationManager after an EMP hit.")]
        private float empPdaCorrosionDuration = 5f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Pulse frequency used for deterministic EMP nest charging diagnostics and future VFX sync.")]
        private float empPulseSpeed = 1.8f;

        [SerializeField, Range(2f, 64f)]
        [Tooltip("Propagation speed used when an EMP nest discharge walks its charge through linked bio-cables.")]
        private float empChainPropagationSpeed = 18f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("How long each cable segment stays overcharged after the EMP chain reaction reaches it.")]
        private float empChainGlowDuration = 0.8f;

        [Header("── Black Smoke ─────────────────────")]
        [SerializeField, Range(256, MaxSmokeParticleCapacity)]
        [Tooltip("Maximum compute-simulated ash particles rendered by the abyssal smoke pass.")]
        private int smokeParticleCount = 4096;

        [SerializeField, Range(0.02f, 0.8f)]
        [Tooltip("Minimum particle billboard size.")]
        private float smokeParticleSizeMin = 0.06f;

        [SerializeField, Range(0.04f, 1.2f)]
        [Tooltip("Maximum particle billboard size.")]
        private float smokeParticleSizeMax = 0.18f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Lateral noise applied to smoke particles so the plume reads as turbulent rather than linear.")]
        private float smokeLateralDrift = 1.35f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Small upward turbulence variation layered onto the main updraft.")]
        private float smokeUpdraftJitter = 1.1f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Noise amplitude used by the compute sim when decorrelating particles from one another.")]
        private float smokeNoiseWeight = 0.75f;

        [SerializeField, Range(10f, 300f)]
        [Tooltip("Maximum camera distance where ash keeps meaningful opacity. Beyond this the plume fades aggressively to control overdraw.")]
        private float smokeMaxViewDistance = 95f;

        [SerializeField]
        [Tooltip("Cold ash tint used by the procedural smoke shader.")]
        private Color smokeTint = new Color(0.08f, 0.08f, 0.08f, 0.28f);

        [SerializeField]
        [Tooltip("Hot-core tint used near fresh hydrothermal emission.")]
        private Color smokeHotTint = new Color(0.22f, 0.17f, 0.12f, 0.34f);

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Radial falloff sharpness for the procedural ash billboards. Higher values tighten the plume silhouette and reduce overdraw.")]
        private float smokeSoftness = 2.2f;

        [SerializeField]
        [Tooltip("Shadow mode used by the procedural smoke draw.")]
        private ShadowCastingMode smokeShadowCastingMode = ShadowCastingMode.Off;

        [Header("── Diagnostics ─────────────────────")]
        [SerializeField]
        [Tooltip("Current active hydrothermal vent count.")]
        private int _debugActiveVentCount;

        [SerializeField]
        [Tooltip("Current active cable-zone count derived from cartographer service and power anchors.")]
        private int _debugCableZoneCount;

        [SerializeField]
        [Tooltip("Current procedural smoke bounds.")]
        private Bounds _debugSmokeBounds;

        [SerializeField]
        [Tooltip("True when the abyssal manager currently considers the local biome deep enough for thermal vent simulation.")]
        private bool _debugAbyssalContext;

        [SerializeField]
        [Tooltip("True while the laser cutter beam is actively severing an abyssal cable knot.")]
        private bool _debugCutterSeveringCable;

        [SerializeField]
        [Tooltip("Latest cut progress reported by the active cable zone sampler.")]
        private float _debugCableCutProgress01;

        [SerializeField]
        [Tooltip("Current active EMP nest count derived from dense abyssal cable anchors.")]
        private int _debugEmpNestCount;

        [SerializeField]
        [Tooltip("Highest EMP nest charge currently active in the local abyssal field.")]
        private float _debugEmpCharge01;

        [SerializeField]
        [Tooltip("Seconds remaining on the current seismic vent-eruption window.")]
        private float _debugSeismicEruptionSeconds;

        [SerializeField]
        [Tooltip("Queued thermal-boundary samples waiting for the crystallization Burst job.")]
        private int _debugQueuedCrystallizationSamples;

        [SerializeField]
        [Tooltip("Thermal Diamond nodes accepted by the crystallization commit path.")]
        private int _debugCrystallizedNodeCount;

        // COLD ALLOC: WorldZoneAnchor[32] - reusable runtime cartographer anchor scratch array for abyssal vent selection - owner: AbyssalThermalManager
        private readonly WorldZoneAnchor[] _zoneAnchors = new WorldZoneAnchor[MaxAnchorScanCapacity];
        // COLD ALLOC: RuntimeVentRegistration[16] - bounded runtime hydrothermal vent registry injected by geology bridge - owner: AbyssalThermalManager
        private readonly RuntimeVentRegistration[] _runtimeVentRegistrations = new RuntimeVentRegistration[MaxVentCapacity];
        // COLD ALLOC: Plane[6] - frustum planes for smoke visibility checks - owner: AbyssalThermalManager
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private ThermalVentState[] _ventStates;
        private ThermalVentGpuData[] _ventGpuData;
        private ThermalVentGpuData[] _lastUploadedVentGpuData;
        private ThermalVentState[] _lastSeededVentStates;
        private AshParticleData[] _initialParticles;
        private EmpNestState[] _empNests;
        private Texture2D _thermalMapTexture;
        private GraphicsBuffer _particleBufferA;
        private GraphicsBuffer _particleBufferB;
        private GraphicsBuffer _particleUploadStagingBuffer;
        private GraphicsBuffer[] _ventBuffers;
        private GraphicsFence[] _ventBufferFences;
        private MaterialPropertyBlock _materialPropertyBlock;
        private BioCableIK[] _bioCableVisuals;
        private bool[] _bioCableVisualsPooled;
        private Bounds _smokeBounds;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredThermodynamicsRuntime;
        private bool _loggedMissingBioCableMaterial;
        private bool _cutterBeamActive;
        private bool _hasSmokeData;
        private int _kernelIndex = -1;
        private int _threadGroupSizeX;
        private int _dispatchGroupCount;
        private int _frameParity;
        private int _activeVentBufferIndex;
        private int _nextVentBufferUploadIndex = 1;
        private int _activeVentCount;
        private int _activeCableZoneCount;
        private int _zoneAnchorCount;
        private int _runtimeVentRegistrationCount;
        private int _instanceId;
        private float _simulationTime;
        private float _cableCutStampCooldown;
        private float _cableFluidDecalCooldown;
        private Transform _activeCutterTransform;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private IPhysicsService _physicsService;
        private IGasDynamicsSolver _gasDynamics;
        private IObjectPoolService _objectPoolService;
        private IPdaCorrosionPresentationSink _pdaCorrosionPresentationSink;
        private VoxelDeltaProcessor _voxelDeltaProcessor;
        private IDamageReceiver _playerThermalDamageReceiver;
        private Transform _playerThermalDamageTransform;
        private IDamageReceiver _submarineThermalDamageReceiver;
        private Transform _submarineThermalDamageTransform;
        private AbyssalFluidDecalManager _fluidDecalManager;
        private bool[] _cableReleasedStates;
        private float[] _cableReleaseProgress;
        private float[] _cableElasticReleaseTimers;
        private float[] _cableEmpChainDelayTimers;
        private float[] _cableEmpChainGlowTimers;
        private bool[] _ventBufferFenceArmed;
        private bool _forceVentBufferUpload = true;
        private bool _forceParticleReset = true;
        private bool _supportsGraphicsFence;
        private bool _supportsComputeShadersCold;
        private bool _supportsThermalMapTextureCold = true;
        private float _thermalGridVramWeight01 = 1f;
        private bool _smokeDispatchFenceArmed;
        private GraphicsFence _smokeDispatchFence;
        private float _seismicEruptionTimer;
        private float _seismicEruptionStrength01;
        private int _lastSeismicSignalSequence;
        private ThermalCrystallizationSample[] _crystallizationSamples;
        private ThermalCrystallizationResult[] _crystallizationResults;
        private bool _crystallizationJobActive;
        private int _pendingCrystallizationSampleCount;
        private int _scheduledCrystallizationSampleCount;
        private float[] _ventCrystallizationCooldowns;
        private VaultGenerationHandle<float> _thermalMapReadCelsiusHandle;
        private VaultGenerationHandle<float> _thermalMapWriteCelsiusHandle;
        private VaultGenerationHandle<float> _thermalMapSourceCelsiusHandle;
        private VaultGenerationHandle<float> _thermalMapInsulation01Handle;
        private ThermalMapScratchBuffers _thermalMapScratch;
        private IDataVault _thermalMapReadbackGuardVault;
        private float[] _thermalMapVisualCelsius;
        private SaveBinaryStorage.ThermalGridRleRun[] _thermalGridRleRuns;
        private VaultGenerationHandle<AbyssalThermalManagerTelemetryEntry> _thermalTelemetryRingHandle;
        private NativeArray<byte> _thermalTelemetryDumpPayload;
        private IDataVault _dataVault;
        private JobHandle _thermalMapJobHandle;
        private bool _thermalMapJobActive;
        private ISimulationBucketer _simulationBucketer;
        private int _thermalMapDiffusionSlicesCompleted;
        private int _thermalMapDiffusionSliceCursor;
        private bool _registeredFixedTick;
        private bool _registeredHotSwapListener;
        private bool _thermalTelemetryDumped;
        private bool _thermalMapTextureDirty;
        private bool _thermalMapTextureFormatRejected;
        private int _thermalMapVersion;
        private int _thermalMapTextureUploadedVersion = -1;
        private int _thermalTelemetryIndex;
        private int _thermalGridRleRunCount;
        private int _thermalGridRleByteCount;
        private int _thermalMapReadbackRetainCount;
        private uint _thermalGridRleChecksum;
        private uint _lastProcessedAupShiftFrameId;
        private int _lastPersistentThermalVentRevision = int.MinValue;
        private bool _thermalMapDisposePending;
        private bool _thermalMapReadbackGuardHeld;
        private float _thermalColdTickAccumulator;
        private float _thermalMapCellSizeMeters = DefaultThermalMapWorldSizeMeters * math.rcp(ThermalMapResolution);
        private Vector3 _thermalMapOriginWS;
        private float _lastLocalThermalHeat01 = -1f;
        private float _lastLocalThermalTemperatureCelsius = float.NaN;
        private float _pendingLocalThermalHeat01;
        private float _pendingLocalThermalTemperatureCelsius;
        private bool _pendingThermalShockAcousticDirty;
        private AcousticPingSignal _pendingThermalShockAcoustic;
        private bool _pendingThermalRoarAudioDirty;
        private Vector3 _pendingThermalRoarPositionWs;
        private float _pendingThermalRoarIntensity01;
        private float _previousPlayerTemperatureCelsius = DefaultAmbientWaterTemperatureCelsius;
        private float _previousSubmarineTemperatureCelsius = DefaultAmbientWaterTemperatureCelsius;
        private float _thermalCondensation01;
        private float _lastPublishedThermalCondensation01 = -1f;
        private float _thermalRoarCooldown;
        private float _thermalEruptionGpuRefreshTimer;
        private float _pendingSmokeVisualDeltaTime;
        private float _pendingCableVisualDeltaTime;
        private int _pendingThermalBubbleCommandCount;
        private bool _localThermalPresentationDirty;
        private bool _smokeVisualSyncRequested;
        private bool _cableVisualSyncRequested;
        private bool _thermalBubbleCommandsDirty;
        private bool _thermalMapMetadataDirty;
        private bool _pendingThermalMapActive;
        private bool _thermalMapIdleCleared;
        private Vector4[] _thermalBubbleCommands;

        private struct ThermalMapScratchBuffers : global::System.IDisposable
        {
            public NativeArray<float> WriteScratch;

            public bool IsWriteScratchReady(int requiredLength)
            {
                return WriteScratch.IsCreated &&
                       WriteScratch.Length >= requiredLength;
            }

            public void EnsureWriteScratch(int requiredLength, float defaultValue)
            {
                if (WriteScratch.IsCreated && WriteScratch.Length == requiredLength)
                    return;

                Dispose();

                // COLD ALLOC: NativeArray<float>[32768] - thermal Jacobi write scratch; prevents cross-frame DataVault write locks - owner: AbyssalThermalManager
                NativeArray<float> scratch = H8Memory.Allocate<float>(requiredLength, ThermalVaultOwnerSystem, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                try
                {
                    if (!scratch.IsCreated)
                        throw new InvalidOperationException("AbyssalThermalManager thermal map scratch allocation failed.");

                    FillThermalMap(scratch, defaultValue);
                    WriteScratch = scratch;
                }
                catch
                {
                    if (scratch.IsCreated)
                        H8Memory.Release(ref scratch, ThermalVaultOwnerSystem);
                    throw;
                }
            }

            public void FillWriteScratch(float value)
            {
                FillThermalMap(WriteScratch, value);
            }

            public void Dispose()
            {
                if (!WriteScratch.IsCreated)
                {
                    WriteScratch = default;
                    return;
                }

                H8Memory.Release(ref WriteScratch, ThermalVaultOwnerSystem);
            }
        }

        /// <summary>
        /// True once the thermodynamics owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => _registeredThermodynamicsRuntime;

        internal bool TryResolveParasiteThermalModifier(BaseModule baseModule, out float insulation01, out float bioReactorOverheatMultiplier)
        {
            return BaseDegradationSystem.TryGetParasiteThermalModifier(baseModule, out insulation01, out bioReactorOverheatMultiplier);
        }

        internal static bool IsThermalBiomeFamilyId(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
                return false;

            return string.Equals(familyId, "biome.family.tectonic_spine", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.chemosynthetic_brine", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.metallic_hadal", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.rift_spine", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(familyId, "biome.family.volcanic_hadal", System.StringComparison.OrdinalIgnoreCase);
        }

        public bool TryResolveApexMigrationThermalAttractor(out Vector3 attractorPosition, out float strength01)
        {
            attractorPosition = default;
            strength01 = 0f;
            if (_ventStates == null || _activeVentCount <= 0)
                return false;

            float bestHeat = 0f;
            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float eruptionBlend = ResolveVentEruptionBlend(i);
                if (eruptionBlend <= 0.001f)
                    continue;

                float eruptiveHeat = Mathf.Max(0f, vent.HeatIntensity) * ResolveVentHeatScale(i);
                if (eruptiveHeat <= bestHeat)
                    continue;

                bestHeat = eruptiveHeat;
                attractorPosition = vent.PositionWS;
            }

            if (bestHeat <= 0f)
                return false;

            strength01 = Mathf.Clamp01(bestHeat / Mathf.Max(1f, ventHeatIntensity * seismicEruptionHeatMultiplier));
            return true;
        }

        internal bool TryResolveNearestActiveVentAttractor(
            in AbsoluteUniversePosition queryAup,
            float searchRadiusMeters,
            out Vector3 attractorPosition,
            out float heat01)
        {
            attractorPosition = default;
            heat01 = 0f;
            if (_ventStates == null || _activeVentCount <= 0 || searchRadiusMeters <= 0f)
                return false;

            double searchRadiusSq = (double)searchRadiusMeters * searchRadiusMeters;
            double bestDistanceSq = double.MaxValue;
            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float heat = Mathf.Max(0f, vent.HeatIntensity);
                if (heat <= 0.001f)
                    continue;

                if (!TryResolveAupFromRuntimeOrigin(vent.PositionWS, out AbsoluteUniversePosition ventAup))
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in ventAup);
                if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                attractorPosition = vent.PositionWS;
                heat01 = Mathf.Clamp01(heat / Mathf.Max(1f, ventHeatIntensity));
            }

            return bestDistanceSq < double.MaxValue;
        }

        public void RegisterRuntimeVent(
            long runtimeKey,
            Vector3 positionWS,
            float radiusWS,
            float heightWS,
            float updraftVelocity,
            float heatIntensity,
            float smokeDensity,
            float cableRadiusWS)
        {
            if (runtimeKey == 0L || !MathGuard.IsFinite(positionWS))
                return;

            RuntimeVentRegistration registration = new RuntimeVentRegistration
            {
                RuntimeKey = runtimeKey,
                PositionWS = positionWS,
                CableAnchorWS = positionWS,
                RadiusWS = ResolvePositiveFinite(radiusWS, 2f, 2f),
                HeightWS = ResolvePositiveFinite(heightWS, 4f, 4f),
                UpdraftVelocity = ResolvePositiveFinite(updraftVelocity, 0.5f, 0.5f),
                HeatIntensity = ResolvePositiveFinite(heatIntensity, 0.5f, 0.5f),
                SmokeDensity = ResolvePositiveFinite(smokeDensity, 0.1f, 0.1f),
                CableRadiusWS = ResolvePositiveFinite(cableRadiusWS, 2f, 2f)
            };

            for (int i = 0; i < _runtimeVentRegistrationCount; i++)
            {
                if (_runtimeVentRegistrations[i].RuntimeKey != runtimeKey)
                    continue;

                _runtimeVentRegistrations[i] = registration;
                RebuildVentField();
                return;
            }

            if (_runtimeVentRegistrationCount >= MaxVentCapacity)
                return;

            _runtimeVentRegistrations[_runtimeVentRegistrationCount] = registration;
            _runtimeVentRegistrationCount++;
            RebuildVentField();
        }

        public void UnregisterRuntimeVent(long runtimeKey)
        {
            if (runtimeKey == 0L || _runtimeVentRegistrationCount <= 0)
                return;

            for (int i = 0; i < _runtimeVentRegistrationCount; i++)
            {
                if (_runtimeVentRegistrations[i].RuntimeKey != runtimeKey)
                    continue;

                RemoveRuntimeVentRegistrationAt(i);
                RebuildVentField();
                return;
            }
        }

        private void SyncPersistentThermalVents()
        {
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null)
                return;

            int revision = registry.ActiveThermalVentRevision;
            if (revision == _lastPersistentThermalVentRevision)
                return;
            if (_lastPersistentThermalVentRevision == int.MinValue && registry.ActiveThermalVentCount <= 0)
            {
                _lastPersistentThermalVentRevision = revision;
                return;
            }

            ClearRuntimeVentRegistrations();
            int count = math.min(registry.ActiveThermalVentCount, MaxVentCapacity);
            for (int i = 0; i < count; i++)
            {
                if (!registry.TryGetActiveThermalVent(i, out PersistentThermalVentRecord record))
                    continue;
                if (record.RuntimeKey == 0L)
                    continue;

                Vector3 positionWS = ResolvePersistentThermalVentRuntimePosition(in record.PositionAup);
                if (!MathGuard.IsFinite(positionWS))
                    continue;

                if (_runtimeVentRegistrationCount >= MaxVentCapacity)
                    break;

                _runtimeVentRegistrations[_runtimeVentRegistrationCount] = new RuntimeVentRegistration
                {
                    RuntimeKey = record.RuntimeKey,
                    PositionWS = positionWS,
                    CableAnchorWS = positionWS,
                    RadiusWS = ResolvePositiveFinite(record.RadiusWS, 2f, 2f),
                    HeightWS = ResolvePositiveFinite(record.HeightWS, 4f, 4f),
                    UpdraftVelocity = ResolvePositiveFinite(record.UpdraftVelocity, 0.5f, 0.5f),
                    HeatIntensity = ResolvePositiveFinite(record.HeatIntensity, 0.5f, 0.5f),
                    SmokeDensity = ResolvePositiveFinite(record.SmokeDensity, 0.1f, 0.1f),
                    CableRadiusWS = ResolvePositiveFinite(record.CableRadiusWS, 2f, 2f)
                };
                _runtimeVentRegistrationCount++;
            }

            _lastPersistentThermalVentRevision = revision;
        }

        private void RemoveRuntimeVentRegistrationAt(int index)
        {
            if ((uint)index >= (uint)_runtimeVentRegistrationCount)
                return;

            int lastIndex = _runtimeVentRegistrationCount - 1;
            if (index != lastIndex)
                _runtimeVentRegistrations[index] = _runtimeVentRegistrations[lastIndex];

            _runtimeVentRegistrations[lastIndex] = default;
            _runtimeVentRegistrationCount--;
        }

        private void ClearRuntimeVentRegistrations()
        {
            for (int i = 0; i < _runtimeVentRegistrationCount; i++)
                _runtimeVentRegistrations[i] = default;

            _runtimeVentRegistrationCount = 0;
        }

        private static Vector3 ResolvePersistentThermalVentRuntimePosition(in AbsoluteUniversePosition positionAup)
        {
            double3 absolute = positionAup.ToAbsoluteDouble3();
            return HectonFloatingOrigin.ToRuntimePosition(absolute);
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            _instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            CacheGraphicsCapabilitiesCold();
            SanitizeSettings();
            CacheRegistryServicesCold();
            ResolveDependencies();
            EnsureStorage();
            EnsureCableVisuals();
            EnsureBuffers();
            ClearHazardSources();
            RebuildVentField();
            PrepareThermalMapResourcesCold();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            LaserCutterEvents.Register(this);
            RandomEventEvents.Register(this);
            HectonFloatingOrigin.RegisterListener(this);
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveDependencies();
            EnsureStorage();
            EnsureCableVisuals();
            EnsureBuffers();
            RebuildVentField();
            PrepareThermalMapResourcesCold();
            TryRegister();
        }

        private void OnDisable()
        {
            LaserCutterEvents.Unregister(this);
            RandomEventEvents.Unregister(this);
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            _cutterBeamActive = false;
            _activeCutterTransform = null;
            _debugCutterSeveringCable = false;
            _seismicEruptionTimer = 0f;
            _seismicEruptionStrength01 = 0f;
            _debugSeismicEruptionSeconds = 0f;
            _hasSmokeData = false;
            _frameParity = 0;
            _simulationBucketer = null;
            _oceanKinematicsService = null;
            _gasDynamics = null;
            cutManager = null;
            _voxelDeltaProcessor = null;
            _playerThermalDamageReceiver = null;
            _playerThermalDamageTransform = null;
            _submarineThermalDamageReceiver = null;
            _submarineThermalDamageTransform = null;
            CompleteThermalMapJobIfReady(forceComplete: true);
            _thermalMapDiffusionSlicesCompleted = 0;
            _thermalMapDiffusionSliceCursor = 0;
            ClearHazardSources();
            ClearThermalFeedbackSignals();
            ReleaseBioCableVisualsToPool();
            ReleaseBuffers();
            DisposeCrystallizationBuffers();
            DisposeThermalMapBuffers();
            DisposeThermalTelemetry();
            TryUnregister();
        }

        private void OnDestroy()
        {
            LaserCutterEvents.Unregister(this);
            RandomEventEvents.Unregister(this);
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            _simulationBucketer = null;
            _oceanKinematicsService = null;
            _gasDynamics = null;
            cutManager = null;
            _voxelDeltaProcessor = null;
            _playerThermalDamageReceiver = null;
            _playerThermalDamageTransform = null;
            _submarineThermalDamageReceiver = null;
            _submarineThermalDamageTransform = null;
            CompleteThermalMapJobIfReady(forceComplete: true);
            ClearHazardSources();
            ClearThermalFeedbackSignals();
            ReleaseBioCableVisualsToPool();
            ReleaseBuffers();
            DisposeCrystallizationBuffers();
            DisposeThermalMapBuffers();
            DisposeThermalTelemetry();
            TryUnregister();

        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !MathGuard.IsFinite(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            _lastProcessedAupShiftFrameId = shiftData.Sequence;
            ApplyRuntimeOffsetToCachedState(-shiftOffset);
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            for (int i = 0; i < _runtimeVentRegistrationCount; i++)
            {
                RuntimeVentRegistration registration = _runtimeVentRegistrations[i];
                registration.PositionWS += runtimeOffset;
                registration.CableAnchorWS += runtimeOffset;
                _runtimeVentRegistrations[i] = registration;
            }

            if (_ventStates != null)
            {
                for (int i = 0; i < _ventStates.Length; i++)
                {
                    ThermalVentState vent = _ventStates[i];
                    vent.PositionWS += runtimeOffset;
                    vent.CableAnchorWS += runtimeOffset;
                    _ventStates[i] = vent;
                }
            }

            if (_lastSeededVentStates != null)
            {
                for (int i = 0; i < _lastSeededVentStates.Length; i++)
                {
                    ThermalVentState vent = _lastSeededVentStates[i];
                    vent.PositionWS += runtimeOffset;
                    vent.CableAnchorWS += runtimeOffset;
                    _lastSeededVentStates[i] = vent;
                }
            }

            if (_ventGpuData != null)
            {
                for (int i = 0; i < _ventGpuData.Length; i++)
                {
                    ThermalVentGpuData ventGpu = _ventGpuData[i];
                    ventGpu.PositionWS += runtimeOffset;
                    _ventGpuData[i] = ventGpu;
                }
            }

            if (_lastUploadedVentGpuData != null)
            {
                for (int i = 0; i < _lastUploadedVentGpuData.Length; i++)
                {
                    ThermalVentGpuData ventGpu = _lastUploadedVentGpuData[i];
                    ventGpu.PositionWS += runtimeOffset;
                    _lastUploadedVentGpuData[i] = ventGpu;
                }
            }

            if (_empNests != null)
            {
                for (int i = 0; i < _empNests.Length; i++)
                {
                    EmpNestState nest = _empNests[i];
                    nest.PositionWS += runtimeOffset;
                    _empNests[i] = nest;
                }
            }

            if (_initialParticles != null)
            {
                for (int i = 0; i < _initialParticles.Length; i++)
                {
                    AshParticleData particle = _initialParticles[i];
                    particle.PositionWS += runtimeOffset;
                    _initialParticles[i] = particle;
                }
            }

            if (!_crystallizationJobActive && _crystallizationSamples != null)
            {
                float3 offset = new float3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                for (int i = 0; i < _pendingCrystallizationSampleCount; i++)
                {
                    ThermalCrystallizationSample sample = _crystallizationSamples[i];
                    sample.PositionWS += offset;
                    _crystallizationSamples[i] = sample;
                }
            }

            Bounds smokeBounds = _smokeBounds;
            smokeBounds.center += runtimeOffset;
            _smokeBounds = smokeBounds;
            _debugSmokeBounds = smokeBounds;
            _thermalMapOriginWS += runtimeOffset;

            _forceVentBufferUpload = true;
            _forceParticleReset = true;
            MarkThermalMapTextureDirty();
            UpdateHazardSources();
            UpdateSmokeBounds();
        }

        /// <summary>
        /// Advances the thermal ash simulation and renders one indirect smoke draw while the local abyssal context is active.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            float deltaTime = math.max(0f, dt);
            AdvanceThermalMapColdTick(deltaTime);
            _simulationTime += deltaTime;
            UpdateSeismicEruption(deltaTime);
            UpdateThermalPresentationDecay(deltaTime);
            AdvanceThermalGpuRefresh(deltaTime);
            if (_debugCutterSeveringCable && !_cutterBeamActive)
                _debugCutterSeveringCable = false;
            if (_cableFluidDecalCooldown > 0f)
            {
                _cableFluidDecalCooldown -= deltaTime;
                if (_cableFluidDecalCooldown < 0f)
                    _cableFluidDecalCooldown = 0f;
            }
            UpdateEmpChainReaction(deltaTime);
            UpdateCableCutting(deltaTime);
            _pendingCableVisualDeltaTime = deltaTime;
            _cableVisualSyncRequested = true;
            UpdateEmpNests(deltaTime);

            if (!_hasSmokeData || _activeVentCount <= 0)
                return;

            QueueSmokeVisualSync(deltaTime);
        }

        /// <summary>
        /// Rebuilds local vent and cable metadata from the current abyssal cartographer context.
        /// </summary>
        public void SlowTick()
        {
            if (!HasSlowTickStorageReady())
                return;

            ApplySeismicSignalEruptionScalar();
            AdvancePassiveCrystallizationCooldowns(0.5f);
            SyncPersistentThermalVents();
            RebuildVentField();
            if (!HasThermalMapRuntimeResourcesReady())
                PublishThermalMapMetadata(active: false);
            if ((_forceVentBufferUpload || _forceParticleReset) && HasSmokeGpuRuntimeResourcesReady())
                QueueSmokeVisualSync(0f);
            ApplyThermalInfiltrationToBaseModules(0.5f);
            QueueVentBoundaryCrystallizationSamples();
            ScheduleCrystallizationJobIfNeeded();
        }

        /// <summary>
        /// Queues a flash-freeze thermal boundary for Burst validation and Thermal Diamond spawning.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space boundary center.</param>
        /// <param name="previousTemperatureCelsius">Hot-side temperature before the coolant/current shock.</param>
        /// <param name="currentTemperatureCelsius">Cold-side temperature after the shock.</param>
        /// <param name="crystallizationRadiusMeters">Approximate boundary radius in meters.</param>
        /// <param name="sourceId">Stable source id used by downstream deterministic placement jitter.</param>
        /// <returns>True when the sample was queued.</returns>
        public bool ReportFlashFreeze(
            Vector3 runtimePosition,
            float previousTemperatureCelsius,
            float currentTemperatureCelsius,
            float crystallizationRadiusMeters,
            uint sourceId = 0u)
        {
            if (_crystallizationSamples == null || _pendingCrystallizationSampleCount >= MaxCrystallizationSampleCapacity)
                return false;

            float3 position = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(position)) ||
                !math.isfinite(previousTemperatureCelsius) ||
                !math.isfinite(currentTemperatureCelsius))
            {
                return false;
            }

            _crystallizationSamples[_pendingCrystallizationSampleCount++] = new ThermalCrystallizationSample
            {
                PositionWS = position,
                PreviousTemperatureCelsius = previousTemperatureCelsius,
                CurrentTemperatureCelsius = currentTemperatureCelsius,
                RadiusMeters = math.max(0.25f, crystallizationRadiusMeters),
                SourceId = sourceId != 0u ? sourceId : (uint)math.hash(new int3(
                    (int)math.round(position.x * 10f),
                    (int)math.round(position.y * 10f),
                    (int)math.round(position.z * 10f))),
                Pending = 1
            };

            _debugQueuedCrystallizationSamples = _pendingCrystallizationSampleCount;
            ScheduleCrystallizationJobIfNeeded();
            return true;
        }

        /// <summary>
        /// Commits completed thermal-boundary Burst results during the late-frame swap window.
        /// </summary>
        public void LateFrameTick()
        {
            ConsumeAupShiftSignals();
            CompleteThermalMapJobIfReady();
            CompleteCrystallizationJobIfReady();
            UploadThermalMapTextureIfDirty();
            FlushThermalMapMetadata();
            FlushLocalThermalPresentation();
            FlushThermalBubbleCommands();
            FlushThermalFeedbackSignals();

            if (_cableVisualSyncRequested)
            {
                UpdateCableVisuals(_pendingCableVisualDeltaTime);
                _cableVisualSyncRequested = false;
            }

            if (_smokeVisualSyncRequested)
            {
                FlushSmokeVisualSync(_pendingSmokeVisualDeltaTime);
                _smokeVisualSyncRequested = false;
            }
        }

        /// <summary>
        /// Applies boiling gameplay effects for player and submarine without broad overlap queries.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            float fdt = Mathf.Max(0f, fixedDeltaTime);
            if (fdt <= 0f)
                return;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : _playerMovement;
            GameObject playerObject = playerContext != null ? playerContext.PlayerObject : playerTransform != null ? playerTransform.gameObject : null;
            Vector3 playerPosition = ResolvePlayerRuntimePosition(playerContext, playerTransform);
            float playerTemperature = ResolveAmbientTemperatureCelsius(playerPosition);
            IDamageReceiver playerDamageReceiver = _playerThermalDamageReceiver;
            Transform playerDamageTransform = _playerThermalDamageTransform != null ? _playerThermalDamageTransform : playerTransform;

            if (ProcessThermalGameplayTarget(
                    null,
                    playerObject,
                    playerPosition,
                    fdt,
                    playerMovement,
                    playerDamageReceiver,
                    playerDamageTransform,
                    publishPresentation: true,
                    out playerTemperature))
            {
                _previousPlayerTemperatureCelsius = playerTemperature;
            }
            else
            {
                _previousPlayerTemperatureCelsius = playerTemperature;
                PublishLocalThermalPresentation(playerPosition, playerTemperature, 0f);
            }

            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            Rigidbody submarineBody = submarine != null ? submarine.HullRigidbody : null;
            if (submarineBody != null)
            {
                Vector3 submarinePosition = submarineBody.worldCenterOfMass;
                float submarineTemperature = ResolveAmbientTemperatureCelsius(submarinePosition);
                ProcessThermalGameplayTarget(
                    submarineBody,
                    submarineBody.gameObject,
                    submarinePosition,
                    fdt,
                    null,
                    _submarineThermalDamageReceiver,
                    _submarineThermalDamageTransform,
                    publishPresentation: false,
                    out submarineTemperature);
                if (_previousSubmarineTemperatureCelsius >= ThermalShockHotThresholdCelsius &&
                    submarineTemperature <= ThermalShockColdThresholdCelsius)
                {
                    EmitThermalShock(
                        submarinePosition,
                        submarineTemperature - _previousSubmarineTemperatureCelsius,
                        ResolveNearestVentSourceId(submarinePosition),
                        submarineBody.gameObject,
                        _submarineThermalDamageReceiver,
                        _submarineThermalDamageTransform,
                        (byte)(TemperatureChangedSignal.FlagThermalShock | TemperatureChangedSignal.FlagSubmarineAmbient));
                }

                _previousSubmarineTemperatureCelsius = submarineTemperature;
                submarine.SetThermalSpeedMultiplier(submarineTemperature < ThermalShockColdThresholdCelsius ? SubmarineColdSpeedMultiplier : 1f);
            }

            if (_thermalRoarCooldown > 0f)
                _thermalRoarCooldown = Mathf.Max(0f, _thermalRoarCooldown - fdt);
        }

        private Vector3 ResolvePlayerRuntimePosition(IPlayerRuntimeContext playerContext, Transform fallbackTransform)
        {
            Transform resolvedTransform = playerContext != null && playerContext.PlayerTransform != null
                ? playerContext.PlayerTransform
                : fallbackTransform != null ? fallbackTransform : transform;

            return resolvedTransform != null ? resolvedTransform.position : Vector3.zero;
        }

        public bool TrySampleTemperatureCelsius(Vector3 positionWS, out float temperatureCelsius)
        {
            return TrySampleTemperatureCelsius(positionWS, out temperatureCelsius, out _);
        }

        public bool TryGetThermalMapReadback(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version)
        {
            temperatureCelsius = default;
            width = ThermalMapResolution;
            height = ThermalMapResolution;
            originWS = _thermalMapOriginWS;
            cellSizeMeters = _thermalMapCellSizeMeters;
            version = _thermalMapVersion;
            return false;
        }

        public bool TryGetThermalGridReadback(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version)
        {
            temperatureCelsius = default;
            width = ThermalGridResolution;
            height = ThermalGridResolution;
            depth = ThermalGridResolution;
            originWS = _thermalMapOriginWS;
            cellSizeMeters = _thermalMapCellSizeMeters;
            version = _thermalMapVersion;
            return UsesThermalGrid() &&
                   TryReadOnlyThermalMapBuffer(
                       in _thermalMapReadCelsiusHandle,
                       ThermalMapReadCelsiusBufferId,
                       out temperatureCelsius);
        }

        public bool TryGetThermalGridReadbackAup(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out double3 originAup,
            out float cellSizeMeters,
            out int version)
        {
            bool available = TryGetThermalGridReadback(
                out temperatureCelsius,
                out width,
                out height,
                out depth,
                out Vector3 originWS,
                out cellSizeMeters,
                out version);
            originAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(originWS);
            if (!available || !math.all(math.isfinite(originAup)))
            {
                originAup = double3.zero;
                return false;
            }

            return true;
        }

        public bool TryAcquireThermalGridReadbackAup(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out double3 originAup,
            out float cellSizeMeters,
            out int version)
        {
            if (!TryLockThermalMapReadback())
            {
                temperatureCelsius = default;
                width = 0;
                height = 0;
                depth = 0;
                originAup = double3.zero;
                cellSizeMeters = 0f;
                version = 0;
                return false;
            }

            Interlocked.Increment(ref _thermalMapReadbackRetainCount);
            if (TryGetThermalGridReadbackAup(
                    out temperatureCelsius,
                    out width,
                    out height,
                    out depth,
                    out originAup,
                    out cellSizeMeters,
                    out version))
            {
                return true;
            }

            ReleaseThermalGridReadback();
            return false;
        }

        public void ReleaseThermalGridReadback()
        {
            while (true)
            {
                int observed = Volatile.Read(ref _thermalMapReadbackRetainCount);
                if (observed <= 0)
                {
                    Interlocked.Exchange(ref _thermalMapReadbackRetainCount, 0);
                    ReleaseThermalMapReadbackGuard();
                    return;
                }

                int next = observed - 1;
                if (Interlocked.CompareExchange(ref _thermalMapReadbackRetainCount, next, observed) != observed)
                    continue;

                if (next == 0)
                {
                    if (_thermalMapDisposePending)
                        DisposeThermalMapBuffers();
                    else
                        ReleaseThermalMapReadbackGuard();
                }

                return;
            }
        }

        public bool TryInjectTransientHeatSource(Vector3 positionWS, float radiusWS, float heatIntensity, uint sourceId)
        {
            if (!Application.isPlaying ||
                radiusWS <= 0f ||
                heatIntensity <= 0f ||
                !math.all(math.isfinite(new float3(positionWS.x, positionWS.y, positionWS.z))) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(heatIntensity))
            {
                return false;
            }

            RegisterThermalSpatialEvent(positionWS, radiusWS, heatIntensity, sourceId);
            PublishTemperatureChangedSignal(
                positionWS,
                heatIntensity,
                heatIntensity,
                unchecked((int)(sourceId & 0x7FFFFFFFu)),
                TemperatureChangedSignal.FlagSubmarineAmbient);
            return true;
        }

        bool IThermodynamicsService.SampleThermalFlow(Vector3 positionWS, float radiusWS, out ThermodynamicFlowSampleDTO sample)
        {
            bool hasSample = SampleThermalFlow(positionWS, radiusWS, out ThermalFlowSample legacy);
            sample = default;
            sample.FlowVelocityWS = legacy.FlowVelocityWS;
            sample.Heat01 = legacy.Heat01;
            sample.DragMultiplier = legacy.DragMultiplier;
            sample.CableAnchorWS = legacy.CableAnchorWS;
            sample.CableTension01 = legacy.CableTension01;
            sample.CableCutProgress01 = legacy.CableCutProgress01;
            sample.CableEscapeSuppression01 = legacy.CableEscapeSuppression01;
            sample.HasFlow = legacy.HasFlow;
            sample.IsCableZone = legacy.IsCableZone;
            return hasSample;
        }

        /// <summary>
        /// Samples hydrothermal flow and cable entanglement without allocating.
        /// </summary>
        /// <param name="positionWS">World-space sample point.</param>
        /// <param name="radiusWS">Additional sample radius.</param>
        /// <param name="sample">Resolved flow and cable payload.</param>
        /// <returns>True when any updraft or cable influence is active at the sample point.</returns>
        public bool SampleThermalFlow(Vector3 positionWS, float radiusWS, out ThermalFlowSample sample)
        {
            sample = default;
            sample.DragMultiplier = 1f;

            if (_activeVentCount <= 0 || !MathGuard.IsFinite(positionWS))
                return false;

            float effectiveRadius = ResolvePositiveFinite(radiusWS, 0.1f, 0.1f);
            float strongestCable = 0f;
            Vector3 strongestCableAnchor = positionWS;
            float strongestCableCut = 0f;

            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                if (!IsFiniteVent(in vent))
                    continue;

                float eruptiveHeatScale = ResolveNonNegativeFinite(ResolveVentHeatScale(i), 1f);
                float eruptiveUpdraftScale = ResolveNonNegativeFinite(ResolveVentUpdraftScale(i), 1f);
                float ventRadius = ResolvePositiveFinite(vent.RadiusWS + effectiveRadius, 0.1f, 0.1f);
                Vector2 planarDelta = new Vector2(positionWS.x - vent.PositionWS.x, positionWS.z - vent.PositionWS.z);
                float planarDistance = ComputeAupPlanarDistance(positionWS, vent.PositionWS);
                if (!math.isfinite(planarDistance))
                    continue;

                if (planarDistance <= ventRadius)
                {
                    float radialFalloff = 1f - planarDistance / math.max(ventRadius, 0.001f);
                    float baseVentY = vent.PositionWS.y;
                    float heightGate = 1f - Mathf.Clamp01((positionWS.y - baseVentY) / math.max(ResolvePositiveFinite(vent.HeightWS, 0.001f, 0.001f), 0.001f));
                    if (heightGate > 0f)
                    {
                        float ventWeight = radialFalloff * heightGate;
                        Vector3 swirlDirection = planarDistance > 0.0001f
                            ? new Vector3(-planarDelta.y / planarDistance, 0f, planarDelta.x / planarDistance)
                            : Vector3.zero;
                        sample.HasFlow = 1;
                        float ventHeat = ResolveNonNegativeFinite(vent.HeatIntensity, 0f);
                        float ventUpdraft = ResolveNonNegativeFinite(vent.UpdraftVelocity, 0f);
                        sample.Heat01 = Mathf.Max(sample.Heat01, ventHeat * eruptiveHeatScale * ventWeight);
                        sample.DragMultiplier = Mathf.Max(sample.DragMultiplier, LerpClamped(1f, ResolvePositiveFinite(ventDragMultiplier, 1f, 1f), ventWeight));
                        sample.FlowVelocityWS += Vector3.up * (ventUpdraft * eruptiveUpdraftScale * ventWeight);
                        sample.FlowVelocityWS += swirlDirection * (ventUpdraft * 0.12f * ventWeight);
                    }
                }

                float cableRadius = ResolvePositiveFinite(vent.CableRadiusWS + effectiveRadius, 0.1f, 0.1f);
                float cableDistance = ComputeAupPlanarDistance(positionWS, vent.CableAnchorWS);
                if (!math.isfinite(cableDistance) || cableDistance > cableRadius)
                    continue;

                float cableWeight = math.saturate(1f - cableDistance / math.max(cableRadius, 0.001f));
                if (cableWeight <= strongestCable)
                    continue;

                strongestCable = cableWeight;
                strongestCableAnchor = ResolveCableAnchor(positionWS, vent.CableAnchorWS);
                if (!MathGuard.IsFinite(strongestCableAnchor))
                    strongestCableAnchor = positionWS;
                strongestCableCut = Resolve01Finite(ResolveCableCutProgress(positionWS, strongestCableAnchor, cableRadius));
            }

            if (strongestCable > 0f)
            {
                sample.IsCableZone = 1;
                sample.CableAnchorWS = strongestCableAnchor;
                sample.CableCutProgress01 = strongestCableCut;
                sample.CableEscapeSuppression01 = 1f - strongestCableCut;
                sample.CableTension01 = strongestCable * sample.CableEscapeSuppression01;
            }

            SanitizeThermalFlowSample(ref sample, positionWS);
            _debugCableCutProgress01 = sample.IsCableZone != 0 ? sample.CableCutProgress01 : 0f;
            return sample.HasFlow != 0 || sample.IsCableZone != 0;
        }

        private bool ProcessThermalGameplayTarget(
            Rigidbody body,
            GameObject targetObject,
            Vector3 positionWS,
            float fixedDeltaTime,
            HectonPlayerMovement playerMovement,
            IDamageReceiver fallbackDamageReceiver,
            Transform fallbackDamageTransform,
            bool publishPresentation,
            out float temperatureCelsius)
        {
            if (!TrySampleTemperatureCelsius(positionWS, out temperatureCelsius, out int sourceId))
                return false;

            float heat01 = TemperatureToHeat01(temperatureCelsius);
            if (publishPresentation)
            {
                float delta = temperatureCelsius - _previousPlayerTemperatureCelsius;
                if (delta >= condensationTemperatureJumpCelsius)
                    _thermalCondensation01 = math.saturate(math.max(_thermalCondensation01, delta * math.rcp(math.max(1f, BoilingDamageThresholdCelsius))));

                PublishLocalThermalPresentation(positionWS, temperatureCelsius, heat01);
                PublishTemperatureChangedSignal(positionWS, temperatureCelsius, delta, sourceId, TemperatureChangedSignal.FlagPlayerAmbient);
                if (_previousPlayerTemperatureCelsius >= ThermalShockHotThresholdCelsius &&
                    temperatureCelsius <= ThermalShockColdThresholdCelsius)
                {
                    EmitThermalShock(
                        positionWS,
                        delta,
                        sourceId,
                        targetObject,
                        fallbackDamageReceiver,
                        fallbackDamageTransform,
                        (byte)(TemperatureChangedSignal.FlagThermalShock | TemperatureChangedSignal.FlagPlayerAmbient));
                }

                TryQueueThermalRoar(positionWS, heat01);
            }

            RecordThermalTelemetry(
                positionWS,
                temperatureCelsius,
                heat01,
                (sourceId != 0 ? ThermalTelemetryFlagHeatSource : 0u) |
                (publishPresentation ? ThermalTelemetryFlagPlayerAmbientTemp : 0u));
            if (temperatureCelsius <= BoilingDamageThresholdCelsius)
                return true;

            if (heat01 > 0f)
            {
                float effectiveMass = body != null ? body.mass : PlayerEquivalentMassKg;
                float invMass = math.rcp(math.max(1f, effectiveMass));
                float velocityChange = thermalConvectionVelocityPerSecond * heat01 * invMass * fixedDeltaTime;
                if (math.isfinite(velocityChange) && velocityChange > 0f)
                {
                    Vector3 thermalImpulse = Vector3.up * velocityChange;
                    if (playerMovement != null)
                        playerMovement.QueueSubsystemExternalVelocityChange(thermalImpulse);
                    else if (body != null)
                        _physicsService?.QueueForce(body, thermalImpulse, ForceMode.VelocityChange);
                }
            }

            if (targetObject != null && boilingDamagePerSecond > 0f)
                QueueBoilingDamage(
                    targetObject,
                    positionWS,
                    temperatureCelsius,
                    heat01,
                    fixedDeltaTime,
                    sourceId,
                    fallbackDamageReceiver,
                    fallbackDamageTransform);

            return true;
        }

        private void QueueBoilingDamage(
            GameObject targetObject,
            Vector3 positionWS,
            float temperatureCelsius,
            float heat01,
            float fixedDeltaTime,
            int sourceId,
            IDamageReceiver fallbackDamageReceiver,
            Transform fallbackDamageTransform)
        {
            float amount = boilingDamagePerSecond * math.saturate(heat01) * math.max(0f, fixedDeltaTime);
            if (!(amount > 0f) || !math.isfinite(amount))
                return;

            if (!TryResolveRegisteredCombatTarget(targetObject, out int targetId, out Transform targetTransform))
            {
                ApplyThermalOwnerFallbackDamage(
                    fallbackDamageReceiver,
                    fallbackDamageTransform,
                    positionWS,
                    amount,
                    temperatureCelsius,
                    sourceId);
                return;
            }

            Hecton8.Gameplay.CombatDamageRequest signal = new Hecton8.Gameplay.CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = sourceId != 0 ? sourceId : _instanceId,
                Amount = amount,
                ImpulseMagnitude = 0f,
                Direction = new float3(0f, 1f, 0f),
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    CombatDamageTypes.Thermal,
                    CombatStatusBits.Burning,
                    CombatWeakspotTier.None)
            };

            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = ResolveTargetLocalPoint(targetTransform, positionWS),
                ArmorNormal = new float3(0f, 1f, 0f),
                LocalTemperatureCelsius = temperatureCelsius,
                StatusDurationSeconds = math.max(0.25f, heat01 * 2f)
            };

            double3 impactAup = ResolveCombatImpactAup(positionWS);
            CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);
        }

        private bool TrySampleTemperatureCelsius(Vector3 positionWS, out float temperatureCelsius, out int sourceId)
        {
            sourceId = 0;
            temperatureCelsius = ResolveAmbientTemperatureCelsius(positionWS);
            if (_activeVentCount <= 0)
                return temperatureCelsius < ambientWaterTemperatureCelsius - 0.001f;

            if (UsesThermalGrid() &&
                TrySampleThermalMap(positionWS, out temperatureCelsius))
            {
                sourceId = ResolveNearestVentSourceId(positionWS);
                return true;
            }

            return false;
        }

        private bool TrySampleThermalMap(Vector3 positionWS, out float temperatureCelsius)
        {
            temperatureCelsius = ResolveAmbientTemperatureCelsius(positionWS);
            if (!TryReadThermalMapBuffer(
                    in _thermalMapReadCelsiusHandle,
                    ThermalMapReadCelsiusBufferId,
                    out NativeArray<float> readBuffer) ||
                _thermalMapCellSizeMeters <= 0.0001f)
                return false;

            float invCellSize = math.rcp(_thermalMapCellSizeMeters);
            float localX = (positionWS.x - _thermalMapOriginWS.x) * invCellSize;
            float localY = (positionWS.y - _thermalMapOriginWS.y) * invCellSize;
            float localZ = (positionWS.z - _thermalMapOriginWS.z) * invCellSize;
            if (localX < 0f || localY < 0f || localZ < 0f ||
                localX >= ThermalGridResolution || localY >= ThermalGridResolution || localZ >= ThermalGridResolution)
                return false;

            int x = math.clamp((int)localX, 0, ThermalGridResolution - 1);
            int y = math.clamp((int)localY, 0, ThermalGridResolution - 1);
            int z = math.clamp((int)localZ, 0, ThermalGridResolution - 1);
            temperatureCelsius = readBuffer[ToThermalGridIndex(x, y, z)];
            return math.isfinite(temperatureCelsius);
        }

        private int ResolveNearestVentSourceId(Vector3 positionWS)
        {
            if (_activeVentCount <= 0)
                return 0;

            int sourceId = 0;
            double nearestSq = double.MaxValue;
            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                double distanceSq = ComputeAupDistanceSq(positionWS, vent.PositionWS);
                if (distanceSq >= nearestSq)
                    continue;

                nearestSq = distanceSq;
                sourceId = vent.HazardSourceId;
            }

            return sourceId;
        }

        private static int ToThermalGridIndex(int x, int y, int z)
        {
            return (y * ThermalGridResolution * ThermalGridResolution) + (z * ThermalGridResolution) + x;
        }

        private float ResolveAmbientTemperatureCelsius(Vector3 positionWS)
        {
            float baseAmbient = math.isfinite(ambientWaterTemperatureCelsius)
                ? ambientWaterTemperatureCelsius
                : DefaultAmbientWaterTemperatureCelsius;
            return positionWS.y < DeepBrineDepthMeters
                ? DeepBrineAmbientWaterTemperatureCelsius
                : baseAmbient;
        }

        private void PublishTemperatureChangedSignal(
            Vector3 positionWS,
            float temperatureCelsius,
            float deltaCelsius,
            int sourceId,
            byte flags)
        {
            if (!TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition positionAup))
                return;

            TemperatureChangedSignal signal = default;
            signal.PositionAup = positionAup;
            signal.TemperatureCelsius = temperatureCelsius;
            signal.DeltaCelsius = math.isfinite(deltaCelsius) ? deltaCelsius : 0f;
            signal.SourceId = sourceId <= 0 ? (ushort)0 : (ushort)math.min(sourceId, ushort.MaxValue);
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Flags = flags;
            SignalBus<TemperatureChangedSignal>.TryPushTracked(in signal, ref s_x001AbyssalThermalManagerSignalPushDropCount);
        }

        private void EmitThermalShock(
            Vector3 positionWS,
            float deltaCelsius,
            int sourceId,
            GameObject targetObject,
            IDamageReceiver fallbackDamageReceiver,
            Transform fallbackDamageTransform,
            byte temperatureFlags)
        {
            if (TryResolveRegisteredCombatTarget(targetObject, out int targetId, out Transform targetTransform))
            {
                int resolvedSourceId = sourceId <= 0 ? _instanceId : sourceId;
                CombatDamageRequest request = new CombatDamageRequest
                {
                    TargetId = targetId,
                    SourceId = resolvedSourceId,
                    Amount = ThermalShockDamageMagnitude,
                    ImpulseMagnitude = 0f,
                    Direction = new float3(0f, 1f, 0f),
                    PackedMeta = CombatDamageRuntime.PackSignalMeta(
                        CombatDamageTypes.Thermal,
                        CombatStatusBits.Burning,
                        CombatWeakspotTier.None)
                };

                CombatDamageSignalDetail detail = new CombatDamageSignalDetail
                {
                    LocalPoint = ResolveTargetLocalPoint(targetTransform, positionWS),
                    ArmorNormal = new float3(0f, 1f, 0f),
                    LocalTemperatureCelsius = math.select(0f, math.abs(deltaCelsius), math.isfinite(deltaCelsius)),
                    StatusDurationSeconds = 2f
                };

                double3 impactAup = ResolveCombatImpactAup(positionWS);
                CombatDamageRuntime.TryQueueDamage(in request, in detail, impactAup);
            }
            else if (fallbackDamageReceiver != null)
            {
                ApplyThermalOwnerFallbackDamage(
                    fallbackDamageReceiver,
                    fallbackDamageTransform,
                    positionWS,
                    ThermalShockDamageMagnitude,
                    math.select(0f, math.abs(deltaCelsius), math.isfinite(deltaCelsius)),
                    sourceId);
            }

            AcousticPingSignal acoustic = default;
            if (TryResolveAupFromRuntimeOrigin(positionWS, out acoustic.PositionAup))
            {
                acoustic.RadiusMeters = 140f;
                acoustic.Intensity01 = math.saturate(math.abs(deltaCelsius) / 140f);
                acoustic.SourceId = unchecked((uint)(sourceId <= 0 ? _instanceId : sourceId));
                acoustic.Channel = ThermalShockAcousticChannel;
                acoustic.Flags = 1;
                QueueThermalShockAcoustic(in acoustic);
            }

            PublishTemperatureChangedSignal(
                positionWS,
                ThermalShockColdThresholdCelsius,
                deltaCelsius,
                sourceId,
                temperatureFlags);
            RecordThermalTelemetry(positionWS, ThermalShockColdThresholdCelsius, 0f, ThermalTelemetryFlagThermalShock);
        }

        private static bool TryResolveRegisteredCombatTarget(
            GameObject targetObject,
            out int targetId,
            out Transform targetTransform)
        {
            targetId = 0;
            targetTransform = null;
            Transform current = targetObject != null ? targetObject.transform : null;
            for (int depth = 0; depth < 6 && current != null; depth++)
            {
                int candidateId = CombatDamageRuntime.ResolveTargetId(current.gameObject);
                if (candidateId != 0 && CombatDamageRuntime.IsTargetRegistered(candidateId))
                {
                    targetId = candidateId;
                    targetTransform = current;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void ApplyThermalOwnerFallbackDamage(
            IDamageReceiver receiver,
            Transform targetTransform,
            Vector3 positionWS,
            float amount,
            float temperatureCelsius,
            int sourceId)
        {
            if (receiver == null || !(amount > 0f) || !math.isfinite(amount))
                return;

            int resolvedSourceId = sourceId != 0 ? sourceId : _instanceId;
            DamagePacket packet = new DamagePacket
            {
                Channel = DamageChannel.Integrity,
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = amount,
                LocalPoint = ResolveTargetLocalPoint(targetTransform, positionWS),
                DamageType = CombatDamageTypes.Thermal,
                IntegrityDelta = 0,
                Depth = ResolveDamageDepthMeters(positionWS),
                SourceId = (ushort)math.clamp(resolvedSourceId, 0, ushort.MaxValue),
                TraumaLevel = 0
            };
            receiver.ReceiveDamage(in packet);
        }

        private float ResolveDamageDepthMeters(Vector3 positionWS)
        {
            return math.isfinite(positionWS.y) ? math.max(0f, ResolveDamageSeaLevelY() - positionWS.y) : 0f;
        }

        private float ResolveDamageSeaLevelY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveDamageSeaLevelY(oceanKinematics.SeaLevel, out float seaLevelY))
            {
                return seaLevelY;
            }

            return DefaultSeaLevelY;
        }

        private static bool TryResolveDamageSeaLevelY(float candidateSeaLevelY, out float seaLevelY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = candidateSeaLevelY;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private static double3 ResolveCombatImpactAup(Vector3 positionWS)
        {
            double3 impactAup = double3.zero;
            if (TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition resolvedAup) &&
                resolvedAup.IsFinite())
            {
                double3 resolved = resolvedAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(resolved)))
                    impactAup = resolved;
            }

            return impactAup;
        }

        private static float3 ResolveTargetLocalPoint(Transform targetTransform, Vector3 positionWS)
        {
            if (targetTransform == null ||
                !math.isfinite(positionWS.x) ||
                !math.isfinite(positionWS.y) ||
                !math.isfinite(positionWS.z))
            {
                return float3.zero;
            }

            Vector3 localPoint = targetTransform.InverseTransformPoint(positionWS);
            float3 localPoint3 = new float3(localPoint.x, localPoint.y, localPoint.z);
            return math.all(math.isfinite(localPoint3)) ? localPoint3 : float3.zero;
        }

        private void PublishLocalThermalPresentation(Vector3 positionWS, float temperatureCelsius, float heat01)
        {
            if (!math.isfinite(temperatureCelsius) || !math.isfinite(heat01))
            {
                DumpThermalBlackBox();
                return;
            }

            _pendingLocalThermalHeat01 = math.saturate(heat01);
            _pendingLocalThermalTemperatureCelsius = temperatureCelsius;
            _localThermalPresentationDirty = true;
        }

        private void FlushLocalThermalPresentation()
        {
            if (!_localThermalPresentationDirty &&
                Mathf.Abs(_thermalCondensation01 - _lastPublishedThermalCondensation01) <= 0.001f)
            {
                return;
            }

            float clampedHeat = _pendingLocalThermalHeat01;
            if (Mathf.Abs(clampedHeat - _lastLocalThermalHeat01) > 0.001f)
            {
                Shader.SetGlobalFloat(_LocalThermalHeatId, clampedHeat);
                _lastLocalThermalHeat01 = clampedHeat;
            }

            float temperatureCelsius = _pendingLocalThermalTemperatureCelsius;
            if (!float.IsFinite(_lastLocalThermalTemperatureCelsius) ||
                Mathf.Abs(temperatureCelsius - _lastLocalThermalTemperatureCelsius) > 0.05f)
            {
                Shader.SetGlobalFloat(_LocalThermalTemperatureId, temperatureCelsius);
                _lastLocalThermalTemperatureCelsius = temperatureCelsius;
            }

            if (Mathf.Abs(_thermalCondensation01 - _lastPublishedThermalCondensation01) > 0.001f)
            {
                Shader.SetGlobalFloat(_ThermalCondensationId, _thermalCondensation01);
                _lastPublishedThermalCondensation01 = _thermalCondensation01;
            }

            _localThermalPresentationDirty = false;
        }

        private void UpdateThermalPresentationDecay(float deltaTime)
        {
            if (_thermalCondensation01 <= 0f)
                return;

            _thermalCondensation01 = math.max(0f, _thermalCondensation01 - (math.max(0f, deltaTime) * condensationDecayPerSecond));
            if (Mathf.Abs(_thermalCondensation01 - _lastPublishedThermalCondensation01) > 0.001f)
            {
                _localThermalPresentationDirty = true;
            }
        }

        private void TryQueueThermalRoar(Vector3 positionWS, float heat01)
        {
            if (heat01 < 0.2f || _thermalRoarCooldown > 0f)
                return;

            float intensity = math.saturate(heat01);
            QueueThermalRoarAudio(positionWS, intensity);

            ImpactSignal signal = default;
            if (!TryResolveAupFromRuntimeOrigin(positionWS, out signal.PointAup))
                return;

            signal.Force = intensity;
            signal.Intensity = intensity;
            signal.PrimaryBodyId = (uint)_instanceId;
            signal.WeightClass = 2;
            SignalBus<ImpactSignal>.TryPushTracked(in signal, ref s_x001AbyssalThermalManagerSignalPushDropCount);
            _thermalRoarCooldown = thermalRoarCooldownSeconds;
        }

        private void QueueThermalShockAcoustic(in AcousticPingSignal acoustic)
        {
            if (_pendingThermalShockAcousticDirty && _pendingThermalShockAcoustic.Intensity01 > acoustic.Intensity01)
                return;

            _pendingThermalShockAcoustic = acoustic;
            _pendingThermalShockAcousticDirty = true;
        }

        private void QueueThermalRoarAudio(Vector3 positionWS, float intensity01)
        {
            float intensity = math.saturate(intensity01);
            if (_pendingThermalRoarAudioDirty && _pendingThermalRoarIntensity01 > intensity)
                return;

            _pendingThermalRoarPositionWs = positionWS;
            _pendingThermalRoarIntensity01 = intensity;
            _pendingThermalRoarAudioDirty = true;
        }

        private void FlushThermalFeedbackSignals()
        {
            if (_pendingThermalShockAcousticDirty)
            {
                _pendingThermalShockAcousticDirty = false;
                SignalBus<AcousticPingSignal>.TryPushTracked(in _pendingThermalShockAcoustic, ref s_x001AbyssalThermalManagerSignalPushDropCount);
            }

            if (_pendingThermalRoarAudioDirty)
            {
                _pendingThermalRoarAudioDirty = false;
                float intensity = _pendingThermalRoarIntensity01;
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                    _pendingThermalRoarPositionWs,
                    intensity,
                    0.65f,
                    1f,
                    Mathf.Lerp(180f, 520f, intensity),
                    ProceduralAudioPingKind.MechanicalWhirr);
            }
        }

        private void ClearThermalFeedbackSignals()
        {
            _pendingThermalShockAcousticDirty = false;
            _pendingThermalRoarAudioDirty = false;
            _pendingThermalRoarIntensity01 = 0f;
        }

        private void AdvanceThermalGpuRefresh(float deltaTime)
        {
            if (_activeVentCount <= 0)
                return;

            _thermalEruptionGpuRefreshTimer += Mathf.Max(0f, deltaTime);
            if (_thermalEruptionGpuRefreshTimer < 0.25f)
                return;

            _thermalEruptionGpuRefreshTimer = 0f;
            _forceVentBufferUpload = true;
        }

        private void RecordThermalTelemetry(Vector3 positionWS, float temperatureCelsius, float heat01, uint flags)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsThermalVaultHandle(in _thermalTelemetryRingHandle, BufferID.AbyssalThermalManagerTelemetryRing))
            {
                return;
            }

            if (!math.isfinite(temperatureCelsius) || !math.isfinite(heat01) || !MathGuard.IsFinite(positionWS))
            {
                DumpThermalBlackBox();
                return;
            }

            if (!vault.TryAcquireWriteLock(in _thermalTelemetryRingHandle, ThermalVaultOwnerSystem, out NativeArray<AbyssalThermalManagerTelemetryEntry> ring))
                return;

            try
            {
                if (!ring.IsCreated || ring.Length < ThermalTelemetryCapacity)
                    return;

                int index = _thermalTelemetryIndex % ThermalTelemetryCapacity;
                ring[index] = new AbyssalThermalManagerTelemetryEntry
                {
                    PositionAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(positionWS),
                    Frame = unchecked((long)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                    Sequence = unchecked((ulong)_thermalTelemetryIndex),
                    TemperatureCelsius = temperatureCelsius,
                    Heat01 = heat01,
                    Flags = flags,
                    ActiveVentCount = _activeVentCount,
                    FailureCode = 0u
                };
                _thermalTelemetryIndex = (_thermalTelemetryIndex + 1) % ThermalTelemetryCapacity;
            }
            finally
            {
                vault.ReleaseWriteLock(in _thermalTelemetryRingHandle, ThermalVaultOwnerSystem);
            }
        }

        private void DumpThermalBlackBox()
        {
            IDataVault vault = _dataVault;
            if (_thermalTelemetryDumped ||
                vault == null ||
                !IsThermalVaultHandle(in _thermalTelemetryRingHandle, BufferID.AbyssalThermalManagerTelemetryRing) ||
                !vault.TryReadOnlyHandle(in _thermalTelemetryRingHandle, out NativeArray<AbyssalThermalManagerTelemetryEntry>.ReadOnly ring) ||
                !ring.IsCreated)
            {
                return;
            }

            if (!IsThermalTelemetryDumpPayloadReady())
                return;

            try
            {
                if (ring.Length > ThermalTelemetryCapacity)
                    return;

                int byteCount = ThermalTelemetryDumpHeaderBytes + ring.Length * ThermalTelemetryDumpEntryBytes;
                if (byteCount > _thermalTelemetryDumpPayload.Length)
                    return;

                unsafe
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_thermalTelemetryDumpPayload);
                    int cursor = 0;
                    WriteInt32LittleEndian(destination, ref cursor, ThermalTelemetryCapacity);
                    WriteInt32LittleEndian(destination, ref cursor, _thermalTelemetryIndex);
                    for (int i = 0; i < ring.Length; i++)
                    {
                        AbyssalThermalManagerTelemetryEntry entry = ring[i];
                        WriteDoubleLittleEndian(destination, ref cursor, entry.PositionAup.x);
                        WriteDoubleLittleEndian(destination, ref cursor, entry.PositionAup.y);
                        WriteDoubleLittleEndian(destination, ref cursor, entry.PositionAup.z);
                        WriteInt64LittleEndian(destination, ref cursor, entry.Frame);
                        WriteUInt64LittleEndian(destination, ref cursor, entry.Sequence);
                        WriteFloatLittleEndian(destination, ref cursor, entry.TemperatureCelsius);
                        WriteFloatLittleEndian(destination, ref cursor, entry.Heat01);
                        WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
                        WriteInt32LittleEndian(destination, ref cursor, entry.ActiveVentCount);
                        WriteUInt32LittleEndian(destination, ref cursor, entry.FailureCode);
                    }

                    _thermalTelemetryDumped = NativeFaultDumpWriter.TryWriteAll(
                        ThermalTelemetryDumpRelativePath,
                        _thermalTelemetryDumpPayload,
                        cursor);
                }

                if (!_thermalTelemetryDumped)
                    GlobalTelemetryBus.PublishUnityLogFault(ThermalHashSeed, 0u, 1u);
            }
            catch (System.IO.IOException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(ThermalHashSeed, 0u, 1u);
            }
            catch (System.UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(ThermalHashSeed, 0u, 1u);
            }
            catch (System.ArgumentException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(ThermalHashSeed, 0u, 1u);
            }
        }

        private static unsafe void WriteDoubleLittleEndian(byte* destination, ref int cursor, double value)
        {
            long raw = *(long*)&value;
            WriteInt64LittleEndian(destination, ref cursor, raw);
        }

        private static unsafe void WriteFloatLittleEndian(byte* destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, ref int cursor, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(destination + cursor, sizeof(int)), value);
            cursor += sizeof(int);
        }

        private static unsafe void WriteInt64LittleEndian(byte* destination, ref int cursor, long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(destination + cursor, sizeof(long)), value);
            cursor += sizeof(long);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(destination + cursor, sizeof(uint)), value);
            cursor += sizeof(uint);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* destination, ref int cursor, ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(destination + cursor, sizeof(ulong)), value);
            cursor += sizeof(ulong);
        }

        private float TemperatureToHeat01(float temperatureCelsius)
        {
            float denominator = math.max(1f, BoilingDamageThresholdCelsius - ambientWaterTemperatureCelsius);
            return math.saturate((temperatureCelsius - ambientWaterTemperatureCelsius) * math.rcp(denominator));
        }

        private void AdvancePassiveCrystallizationCooldowns(float deltaSeconds)
        {
            if (_ventCrystallizationCooldowns == null)
                return;

            float dt = Mathf.Max(0f, deltaSeconds);
            for (int i = 0; i < _ventCrystallizationCooldowns.Length; i++)
            {
                if (_ventCrystallizationCooldowns[i] <= 0f)
                    continue;

                _ventCrystallizationCooldowns[i] = Mathf.Max(0f, _ventCrystallizationCooldowns[i] - dt);
            }
        }

        private void QueueVentBoundaryCrystallizationSamples()
        {
            if (_activeVentCount <= 0 ||
                vegetationBridge == null ||
                _ventCrystallizationCooldowns == null ||
                _crystallizationSamples == null)
            {
                return;
            }

            for (int i = 0; i < _activeVentCount && _pendingCrystallizationSampleCount < MaxCrystallizationSampleCapacity; i++)
            {
                if (_ventCrystallizationCooldowns[i] > 0f)
                    continue;

                ThermalVentState vent = _ventStates[i];
                float eruptionHeatScale = ResolveVentHeatScale(i);
                float hotSideTemperature = vent.HeatIntensity * eruptionHeatScale * ventHeatToCelsiusScale;
                if (hotSideTemperature < crystallizationMinimumSourceTemperatureCelsius)
                    continue;

                Vector3 boundaryPosition = ResolveCrystallizationBoundaryPosition(in vent, i);
                float ambientTemperature = vegetationBridge.GetWaterTemperature(boundaryPosition);
                if (ambientTemperature > freezingCurrentTemperatureCelsius)
                    continue;

                uint sourceId = (uint)BuildHazardSourceId(i);
                if (ReportFlashFreeze(
                        boundaryPosition,
                        hotSideTemperature,
                        ambientTemperature,
                        crystallizationNodeRadiusMeters,
                        sourceId))
                {
                    _ventCrystallizationCooldowns[i] = passiveCrystallizationCooldownSeconds;
                }
            }
        }

        private void ApplyThermalInfiltrationToBaseModules(float deltaTime)
        {
            if (_activeVentCount <= 0 ||
                deltaTime <= 0f ||
                habitatThermalConductivityWattsPerSquareMeterKelvin <= 0f)
            {
                return;
            }

            int moduleCount = BaseModule.ActiveModuleCount;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule baseModule = BaseModule.GetActiveModuleAt(moduleIndex);
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                Vector3 modulePosition = baseModule.transform.position;
                if (!SampleThermalFlow(modulePosition, habitatThermalSampleRadiusMeters, out ThermalFlowSample sample) ||
                    sample.Heat01 <= 0f)
                {
                    continue;
                }

                float externalTemperatureCelsius = sample.Heat01 * ventHeatToCelsiusScale;
                float internalTemperatureCelsius = baseModule.ResolveHostRoomTemperatureCelsius();
                float temperatureDelta = externalTemperatureCelsius - internalTemperatureCelsius;
                if (temperatureDelta <= 0f || !math.isfinite(temperatureDelta))
                    continue;

                float surfaceAreaSquareMeters = baseModule.ResolveThermalSurfaceAreaSquareMeters();
                float heatFluxJoules = habitatThermalConductivityWattsPerSquareMeterKelvin *
                                       surfaceAreaSquareMeters *
                                       temperatureDelta *
                                       deltaTime;
                if (heatFluxJoules <= 0f || !math.isfinite(heatFluxJoules))
                    continue;

                float airVolumeCubicMeters = baseModule.ModuleTemplate != null
                    ? math.max(0.1f, baseModule.ModuleTemplate.AirVolumeM3)
                    : 18f;
                float airHeatCapacity = math.max(
                    1f,
                    airVolumeCubicMeters *
                    DryAirDensityKilogramsPerCubicMeter *
                    DryAirHeatCapacityJoulesPerKilogramKelvin);
                float injectedDeltaCelsius = math.min(
                    habitatThermalMaxTemperatureDeltaPerSlowTick,
                    heatFluxJoules / airHeatCapacity);
                if (injectedDeltaCelsius > 0f && math.isfinite(injectedDeltaCelsius))
                {
                    baseModule.TryInjectHostRoomTemperatureDeltaCelsius(injectedDeltaCelsius);
                    if (baseModule.TryResolveHostAtmosphereRoomIndex(out int roomIndex))
                    {
                        IGasDynamicsSolver gasDynamics = _gasDynamics;
                        gasDynamics?.TrySetRoomTemperatureCelsius(roomIndex, internalTemperatureCelsius + injectedDeltaCelsius);
                    }
                }
            }
        }

        private Vector3 ResolveCrystallizationBoundaryPosition(in ThermalVentState vent, int ventIndex)
        {
            float angle = HashToFloat01((uint)_instanceId, (uint)(ventIndex + 1), 0x7D5C2A11u) * Mathf.PI * 2f;
            float radius = Mathf.Max(0.5f, vent.RadiusWS * 0.88f);
            Vector3 radial = new Vector3(CinematicMath.FastCos(angle), 0f, CinematicMath.FastSin(angle)) * radius;
            return vent.PositionWS + radial + Vector3.up * Mathf.Max(0.15f, vent.HeightWS * 0.08f);
        }

        private void AdvanceThermalMapColdTick(float deltaSeconds)
        {
            if (_thermalMapJobActive)
                return;

            if (!UsesThermalGrid())
            {
                _thermalMapDiffusionSlicesCompleted = 0;
                _thermalMapDiffusionSliceCursor = 0;
                PublishThermalMapMetadata(active: false);
                return;
            }

            if (_activeVentCount <= 0)
            {
                if (!HasThermalMapStorage())
                {
                    PublishThermalMapMetadata(active: false);
                    return;
                }

                if (!HasThermalMapBuffersReady())
                {
                    PublishThermalMapMetadata(active: false);
                    return;
                }

                if (_thermalMapIdleCleared)
                {
                    PublishThermalMapMetadata(active: false);
                    return;
                }

                float idleAmbientCelsius = ResolveAmbientTemperatureCelsius(ResolveThermalMapCenter());
                FillThermalMapBuffer(in _thermalMapReadCelsiusHandle, ThermalMapReadCelsiusBufferId, idleAmbientCelsius);
                FillThermalMapBuffer(in _thermalMapWriteCelsiusHandle, ThermalMapWriteCelsiusBufferId, idleAmbientCelsius);
                FillThermalMapBuffer(in _thermalMapSourceCelsiusHandle, ThermalMapSourceCelsiusBufferId, idleAmbientCelsius);
                FillThermalMapBuffer(in _thermalMapInsulation01Handle, ThermalMapInsulationBufferId, 0f);
                if (_thermalMapScratch.WriteScratch.IsCreated)
                    _thermalMapScratch.FillWriteScratch(idleAmbientCelsius);
                if (_thermalMapVisualCelsius != null)
                    FillThermalMap(_thermalMapVisualCelsius, idleAmbientCelsius);
                _thermalGridRleRunCount = 0;
                _thermalGridRleByteCount = 0;
                _thermalGridRleChecksum = 0u;
                _thermalMapDiffusionSlicesCompleted = 0;
                _thermalMapDiffusionSliceCursor = 0;
                _thermalMapVersion++;
                _thermalMapIdleCleared = true;
                MarkThermalMapTextureDirty();
                PublishThermalMapMetadata(active: false);
                return;
            }

            _thermalMapIdleCleared = false;
            if (!HasThermalMapBuffersReady())
            {
                PublishThermalMapMetadata(active: false);
                return;
            }

            if (_thermalMapDiffusionSlicesCompleted <= 0)
            {
                _thermalColdTickAccumulator += Mathf.Max(0f, deltaSeconds);
                float coldTickSeconds = ResolveThermalMapColdTickSeconds();
                if (_thermalColdTickAccumulator < coldTickSeconds)
                    return;

                _thermalColdTickAccumulator = 0f;
                if (!RebuildThermalMapSources())
                    return;

                _thermalMapDiffusionSliceCursor = ResolveThermalMapDiffusionSliceCursor();
            }

            int startIndex = _thermalMapDiffusionSliceCursor * ThermalMapDiffusionSliceCellCount;
            if (!TryReadThermalMapBuffer(
                    in _thermalMapReadCelsiusHandle,
                    ThermalMapReadCelsiusBufferId,
                    out NativeArray<float> readCelsius) ||
                !TryReadThermalMapBuffer(
                    in _thermalMapSourceCelsiusHandle,
                    ThermalMapSourceCelsiusBufferId,
                    out NativeArray<float> sourceCelsius) ||
                !TryReadThermalMapBuffer(
                    in _thermalMapInsulation01Handle,
                    ThermalMapInsulationBufferId,
                    out NativeArray<float> insulation01) ||
                !_thermalMapScratch.IsWriteScratchReady(ThermalMapCellCount))
            {
                return;
            }

            ThermalMapJacobiJob job = new ThermalMapJacobiJob
            {
                Previous = readCelsius,
                Sources = sourceCelsius,
                Insulation01 = insulation01,
                Next = _thermalMapScratch.WriteScratch,
                StartIndex = startIndex,
                Width = ThermalMapResolution,
                Height = ThermalMapResolution,
                Depth = ThermalGridResolution,
                AxisShift = ThermalMapAxisShift,
                AmbientCelsius = ambientWaterTemperatureCelsius,
                Diffusion01 = ResolveThermalMapDiffusion01()
            };

            _thermalMapJobHandle = job.Schedule(ThermalMapDiffusionSliceCellCount, ThermalMapDiffusionJobBatchSize);
            _thermalMapJobActive = true;
        }

        private void CompleteThermalMapJobIfReady(bool forceComplete = false)
        {
            if (!_thermalMapJobActive)
            {
                _thermalMapJobHandle = default;
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _thermalMapJobHandle, forceComplete))
                return;

            _thermalMapJobActive = false;
            _thermalMapDiffusionSlicesCompleted++;
            _thermalMapDiffusionSliceCursor = (_thermalMapDiffusionSliceCursor + 1) & ThermalMapDiffusionSliceMask;
            if (_thermalMapDiffusionSlicesCompleted < ThermalMapDiffusionSliceCount)
                return;

            _thermalMapDiffusionSlicesCompleted = 0;
            if (!CopyThermalMapScratchToBuffer(
                    in _thermalMapReadCelsiusHandle,
                    ThermalMapReadCelsiusBufferId))
            {
                return;
            }

            _thermalMapVersion++;
            BuildThermalMapVisualProjection();
            StageThermalGridRleDelta();
            MarkThermalMapTextureDirty();
            PublishThermalMapMetadata(active: _activeVentCount > 0 && HasThermalMapReadBuffer());
        }

        private int ResolveThermalMapDiffusionSliceCursor()
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            return bucketer != null && bucketer.IsInitialized
                ? bucketer.ActiveColdBucket & ThermalMapDiffusionSliceMask
                : _thermalMapDiffusionSliceCursor & ThermalMapDiffusionSliceMask;
        }

        private bool RebuildThermalMapSources()
        {
            Vector3 center = ResolveThermalMapCenter();
            float worldSize = math.max(thermalMapWorldSizeMeters, 1f);
            _thermalMapCellSizeMeters = worldSize * math.rcp(ThermalMapResolution);
            _thermalMapOriginWS = center - new Vector3(worldSize * 0.5f, worldSize * 0.5f, worldSize * 0.5f);

            return RebuildThermalMapSourceTemperatures() &&
                   RebuildThermalMapInsulation();
        }

        private bool RebuildThermalMapSourceTemperatures()
        {
            if (!_thermalMapScratch.IsWriteScratchReady(ThermalMapCellCount))
                return false;

            NativeArray<float> sourceCelsius = _thermalMapScratch.WriteScratch;
            for (int y = 0; y < ThermalGridResolution; y++)
            {
                float sampleY = _thermalMapOriginWS.y + ((y + 0.5f) * _thermalMapCellSizeMeters);
                for (int z = 0; z < ThermalGridResolution; z++)
                {
                    float sampleZ = _thermalMapOriginWS.z + ((z + 0.5f) * _thermalMapCellSizeMeters);
                    for (int x = 0; x < ThermalGridResolution; x++)
                    {
                        float sampleX = _thermalMapOriginWS.x + ((x + 0.5f) * _thermalMapCellSizeMeters);
                        Vector3 samplePosition = new Vector3(sampleX, sampleY, sampleZ);
                        float ambientTemperature = ResolveAmbientTemperatureCelsius(samplePosition);
                        float temperature = ambientTemperature;

                        for (int i = 0; i < _activeVentCount; i++)
                        {
                            ThermalVentState vent = _ventStates[i];
                            float radius = math.max(1f, vent.RadiusWS * ventHeatRadiusMultiplier);
                            float planarDistance = ComputeAupPlanarDistance(samplePosition, vent.PositionWS);
                            float verticalWeight = math.saturate(1f - math.abs(samplePosition.y - vent.PositionWS.y) * math.rcp(math.max(1f, vent.HeightWS)));
                            float radialWeight = math.saturate(1f - (planarDistance * math.rcp(radius))) * verticalWeight;
                            if (radialWeight <= 0f)
                                continue;

                            float heatScale = ResolveVentHeatScale(i);
                            float ventDeltaCelsius = math.max(ThermalVentInjectionDeltaCelsius, vent.HeatIntensity * ventHeatToCelsiusScale);
                            float candidate = ambientTemperature + (ventDeltaCelsius * heatScale * radialWeight);
                            if (candidate > temperature && math.isfinite(candidate))
                                temperature = candidate;
                        }

                        sourceCelsius[ToThermalGridIndex(x, y, z)] = temperature;
                    }
                }
            }

            return CopyThermalMapScratchToBuffer(in _thermalMapSourceCelsiusHandle, ThermalMapSourceCelsiusBufferId);
        }

        private bool RebuildThermalMapInsulation()
        {
            if (!_thermalMapScratch.IsWriteScratchReady(ThermalMapCellCount))
                return false;

            NativeArray<float> insulationBuffer = _thermalMapScratch.WriteScratch;
            for (int y = 0; y < ThermalGridResolution; y++)
            {
                float sampleY = _thermalMapOriginWS.y + ((y + 0.5f) * _thermalMapCellSizeMeters);
                for (int z = 0; z < ThermalGridResolution; z++)
                {
                    float sampleZ = _thermalMapOriginWS.z + ((z + 0.5f) * _thermalMapCellSizeMeters);
                    for (int x = 0; x < ThermalGridResolution; x++)
                    {
                        float sampleX = _thermalMapOriginWS.x + ((x + 0.5f) * _thermalMapCellSizeMeters);
                        Vector3 samplePosition = new Vector3(sampleX, sampleY, sampleZ);
                        insulationBuffer[ToThermalGridIndex(x, y, z)] = ResolveVoxelInsulation01(samplePosition);
                    }
                }
            }

            return CopyThermalMapScratchToBuffer(in _thermalMapInsulation01Handle, ThermalMapInsulationBufferId);
        }

        private Vector3 ResolveThermalMapCenter()
        {
            if (playerTransform != null)
                return playerTransform.position;

            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            if (submarine != null && submarine.HullRigidbody != null)
                return submarine.HullRigidbody.worldCenterOfMass;

            return transform.position;
        }

        private void PublishThermalMapMetadata(bool active)
        {
            _pendingThermalMapActive = active;
            _thermalMapMetadataDirty = true;
        }

        private void FlushThermalMapMetadata()
        {
            bool active = _pendingThermalMapActive;
            if (!_thermalMapMetadataDirty)
                return;

            Shader.SetGlobalFloat(_ThermalMapActiveId, active ? 1f : 0f);
            if (!active)
            {
                BindInactiveThermalMapTexture();
                _thermalMapMetadataDirty = false;
                return;
            }

            float worldSize = math.max(thermalMapWorldSizeMeters, _thermalMapCellSizeMeters * ThermalMapResolution);
            Shader.SetGlobalVector(_ThermalMapOriginCellSizeId, new Vector4(
                _thermalMapOriginWS.x,
                _thermalMapOriginWS.z,
                _thermalMapCellSizeMeters,
                _thermalMapVersion));
            Shader.SetGlobalVector(_ThermalMapWorldRectId, new Vector4(
                _thermalMapOriginWS.x,
                _thermalMapOriginWS.z,
                worldSize,
                FaunaThermalAvoidanceThresholdCelsius));

            _thermalMapMetadataDirty = false;
        }

        private void MarkThermalMapTextureDirty()
        {
            _thermalMapTextureDirty = true;
        }

        private void UploadThermalMapTextureIfDirty()
        {
            if (!_thermalMapTextureDirty)
                return;

            _thermalMapTextureDirty = false;
            if (!UsesThermalGrid() ||
                _activeVentCount <= 0 ||
                _thermalMapVisualCelsius == null)
            {
                BindInactiveThermalMapTexture();
                return;
            }

            if (_thermalMapTextureUploadedVersion == _thermalMapVersion)
                return;

            if (!HasThermalMapTextureReady())
            {
                Shader.SetGlobalFloat(_ThermalMapActiveId, 0f);
                BindInactiveThermalMapTexture();
                return;
            }

            _thermalMapTexture.SetPixelData(_thermalMapVisualCelsius, 0);
            _thermalMapTexture.Apply(false, false);
            Shader.SetGlobalTexture(_ThermalMapTextureId, _thermalMapTexture);
            _thermalMapTextureUploadedVersion = _thermalMapVersion;
        }

        private bool HasThermalMapTextureReady()
        {
            return _thermalMapTexture != null;
        }

        private void PrepareThermalMapTextureCold()
        {
            if (_thermalMapTexture != null)
                return;

            if (_thermalMapTextureFormatRejected)
                return;

            if (!_supportsThermalMapTextureCold)
            {
                _thermalMapTextureFormatRejected = true;
                return;
            }

            _thermalMapTexture = new Texture2D(ThermalMapResolution, ThermalMapResolution, TextureFormat.RFloat, false, true)
            {
                name = "__HectonThermalMapRFloat32",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: Texture2D[32x32 RFloat] - GPU-visible center-slice thermal Celsius field for shader/VFX sampling - owner: AbyssalThermalManager
        }

        private void BindInactiveThermalMapTexture()
        {
            if (_thermalMapTextureUploadedVersion < 0)
                return;

            Shader.SetGlobalTexture(_ThermalMapTextureId, Texture2D.blackTexture);
            _thermalMapTextureUploadedVersion = -1;
        }

        private static bool UsesThermalGrid()
        {
            return ThermalGridResolution > 0 && ThermalMapCellCount > 0;
        }

        private static float ResolveThermalGridQualityWeight01()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(weight) ? math.saturate(weight) : 1f;
        }

        private float ResolveThermalGridCostWeight01()
        {
            float quality = Smooth01(ResolveThermalGridQualityWeight01());
            float vram = Smooth01(_thermalGridVramWeight01);
            float qualityCost = math.lerp(ThermalGridSurvivalCostWeight01, 1f, quality);
            float vramCost = math.lerp(ThermalGridSurvivalVramWeight01, 1f, vram);
            return math.saturate(qualityCost * vramCost);
        }

        private float ResolveThermalMapColdTickSeconds()
        {
            float baseTickSeconds = math.max(0.25f, thermalMapColdTickSeconds);
            float costWeight = ResolveThermalGridCostWeight01();
            return baseTickSeconds * math.lerp(ThermalGridSurvivalColdTickMultiplier, 1f, costWeight);
        }

        private float ResolveThermalMapDiffusion01()
        {
            float costWeight = ResolveThermalGridCostWeight01();
            return math.saturate(thermalMapDiffusion01 * math.lerp(ThermalGridSurvivalDiffusionScale, 1f, costWeight));
        }

        private static float ResolveThermalGridVramWeight01(int graphicsMemoryMb)
        {
            if (graphicsMemoryMb <= 0)
                return 1f;

            float span = math.max(1f, ThermalGridFullVramMb - ThermalGridMinimumVramMb);
            return math.saturate((graphicsMemoryMb - ThermalGridMinimumVramMb) / span);
        }

        private static float Smooth01(float value01)
        {
            float q = math.saturate(math.isfinite(value01) ? value01 : 1f);
            return q * q * (3f - 2f * q);
        }

        private static float ResolveVoxelInsulation01(Vector3 runtimePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition runtimeAup))
                return 0f;

            double3 absolutePosition = runtimeAup.ToAbsoluteDouble3();
            if (!HectonVoxelVolume.GetSDFDensity(absolutePosition, out float density))
                return 0f;

            return density > 0f ? math.saturate(density) : 0f;
        }

        private void BuildThermalMapVisualProjection()
        {
            if (_thermalMapVisualCelsius == null ||
                !TryReadThermalMapBuffer(
                    in _thermalMapReadCelsiusHandle,
                    ThermalMapReadCelsiusBufferId,
                    out NativeArray<float> readCelsius))
                return;

            int centerY = ThermalGridResolution >> 1;
            for (int z = 0; z < ThermalGridResolution; z++)
            {
                for (int x = 0; x < ThermalGridResolution; x++)
                    _thermalMapVisualCelsius[(z * ThermalMapResolution) + x] = readCelsius[ToThermalGridIndex(x, centerY, z)];
            }
        }

        private void StageThermalGridRleDelta()
        {
            _thermalGridRleRunCount = 0;
            _thermalGridRleByteCount = 0;
            _thermalGridRleChecksum = 0u;
            if (_thermalGridRleRuns == null ||
                !TryReadThermalMapBuffer(
                    in _thermalMapReadCelsiusHandle,
                    ThermalMapReadCelsiusBufferId,
                    out NativeArray<float> readCelsius))
                return;

            int index = 0;
            while (index < readCelsius.Length && _thermalGridRleRunCount < _thermalGridRleRuns.Length)
            {
                float ambient = ResolveAmbientTemperatureCelsius(GridIndexToWorldPosition(index));
                float temperature = readCelsius[index];
                if (math.abs(temperature - ambient) <= 0.05f)
                {
                    index++;
                    continue;
                }

                int runStart = index;
                int runCount = 1;
                while (index + runCount < readCelsius.Length &&
                       runCount < ushort.MaxValue &&
                       _thermalGridRleRunCount < _thermalGridRleRuns.Length &&
                       math.abs(readCelsius[index + runCount] - temperature) <= 0.05f)
                {
                    runCount++;
                }

                _thermalGridRleRuns[_thermalGridRleRunCount++] = new SaveBinaryStorage.ThermalGridRleRun
                {
                    StartIndex = (ushort)math.min(runStart, ushort.MaxValue),
                    Count = (ushort)math.min(runCount, ushort.MaxValue),
                    TemperatureCelsius = temperature
                };
                index += runCount;
            }

            SaveBinaryStorage.TryStageThermalGridRleDelta(
                _thermalGridRleRuns,
                _thermalGridRleRunCount,
                out _thermalGridRleByteCount,
                out _thermalGridRleChecksum);
        }

        private Vector3 GridIndexToWorldPosition(int index)
        {
            int xy = ThermalGridResolution * ThermalGridResolution;
            int y = index / xy;
            int remainder = index - (y * xy);
            int z = remainder / ThermalGridResolution;
            int x = remainder - (z * ThermalGridResolution);
            return _thermalMapOriginWS + new Vector3(
                (x + 0.5f) * _thermalMapCellSizeMeters,
                (y + 0.5f) * _thermalMapCellSizeMeters,
                (z + 0.5f) * _thermalMapCellSizeMeters);
        }

        private void ConsumeAupShiftSignals()
        {
            System.ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                AupShiftSignal signal = shifts[i];
                if (signal.ShiftFrameId == 0u || signal.ShiftFrameId == _lastProcessedAupShiftFrameId)
                    continue;

                _lastProcessedAupShiftFrameId = signal.ShiftFrameId;
                Vector3 runtimeOffset = new Vector3(-signal.ShiftMeters.x, -signal.ShiftMeters.y, -signal.ShiftMeters.z);
                _thermalMapOriginWS += runtimeOffset;
                MarkThermalMapTextureDirty();
            }
        }

        private static void FillThermalMap(NativeArray<float> map, float value)
        {
            if (!map.IsCreated)
                return;

            for (int i = 0; i < map.Length; i++)
                map[i] = value;
        }

        private static void FillThermalMap(float[] map, float value)
        {
            if (map == null)
                return;

            for (int i = 0; i < map.Length; i++)
                map[i] = value;
        }

        private void ScheduleCrystallizationJobIfNeeded()
        {
            if (_crystallizationJobActive ||
                _pendingCrystallizationSampleCount <= 0 ||
                _crystallizationSamples == null ||
                _crystallizationResults == null)
            {
                return;
            }

            _scheduledCrystallizationSampleCount = Mathf.Min(_pendingCrystallizationSampleCount, MaxCrystallizationSampleCapacity);
            for (int i = 0; i < _scheduledCrystallizationSampleCount; i++)
                _crystallizationResults[i] = ResolveCrystallizationResult(_crystallizationSamples[i]);

            _crystallizationJobActive = true;
        }

        private ThermalCrystallizationResult ResolveCrystallizationResult(in ThermalCrystallizationSample sample)
        {
            ThermalCrystallizationResult result = default;
            if (sample.Pending == 0 ||
                sample.RadiusMeters < 0.25f ||
                !math.all(math.isfinite(sample.PositionWS)) ||
                !math.isfinite(sample.PreviousTemperatureCelsius) ||
                !math.isfinite(sample.CurrentTemperatureCelsius))
            {
                return result;
            }

            float delta = sample.CurrentTemperatureCelsius - sample.PreviousTemperatureCelsius;
            bool accepted = sample.PreviousTemperatureCelsius >= crystallizationMinimumSourceTemperatureCelsius &&
                            delta <= crystallizationDeltaTemperatureThresholdCelsius;
            result.PositionWS = sample.PositionWS;
            result.DeltaTemperatureCelsius = delta;
            result.RadiusMeters = math.max(0.25f, sample.RadiusMeters);
            result.SourceId = sample.SourceId;
            result.ShouldSpawn = accepted ? (byte)1 : (byte)0;
            return result;
        }

        private void CompleteCrystallizationJobIfReady()
        {
            if (!_crystallizationJobActive)
                return;

            _crystallizationJobActive = false;

            ResourceDistributionDirector director = resourceDistributionDirector;
            WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref director);
            resourceDistributionDirector = director;
            if (director != null)
            {
                for (int i = 0; i < _scheduledCrystallizationSampleCount; i++)
                {
                    ThermalCrystallizationResult result = _crystallizationResults[i];
                    if (result.ShouldSpawn == 0)
                        continue;

                    Vector3 runtimePosition = new Vector3(result.PositionWS.x, result.PositionWS.y, result.PositionWS.z);
                    if (director.TrySpawnThermalDiamondCrystallization(
                            runtimePosition,
                            result.RadiusMeters,
                            result.DeltaTemperatureCelsius,
                            result.SourceId))
                    {
                        _debugCrystallizedNodeCount++;
                    }
                }
            }

            CompactCrystallizationSamplesAfterCommit();
            _debugQueuedCrystallizationSamples = _pendingCrystallizationSampleCount;
            ScheduleCrystallizationJobIfNeeded();
        }

        private void CompactCrystallizationSamplesAfterCommit()
        {
            int consumedCount = _scheduledCrystallizationSampleCount;
            int remainingCount = math.max(0, _pendingCrystallizationSampleCount - consumedCount);
            for (int i = 0; i < remainingCount; i++)
                _crystallizationSamples[i] = _crystallizationSamples[consumedCount + i];

            for (int i = remainingCount; i < _pendingCrystallizationSampleCount; i++)
                _crystallizationSamples[i] = default;

            for (int i = 0; i < consumedCount && i < _crystallizationResults.Length; i++)
                _crystallizationResults[i] = default;

            _pendingCrystallizationSampleCount = remainingCount;
            _scheduledCrystallizationSampleCount = 0;
        }

        private void DisposeCrystallizationBuffers()
        {
            _crystallizationSamples = null;
            _crystallizationResults = null;
            _crystallizationJobActive = false;
            _pendingCrystallizationSampleCount = 0;
            _scheduledCrystallizationSampleCount = 0;
            _debugQueuedCrystallizationSamples = 0;
        }

        private void EnsureThermalMapBuffers()
        {
            float defaultThermalMapAmbient = ResolveAmbientTemperatureCelsius(ResolveThermalMapCenter());
            _thermalMapScratch.EnsureWriteScratch(ThermalMapCellCount, defaultThermalMapAmbient);

            EnsureThermalFloatBuffer(ref _thermalMapReadCelsiusHandle, ThermalMapReadCelsiusBufferId, defaultThermalMapAmbient);
            EnsureThermalFloatBuffer(ref _thermalMapWriteCelsiusHandle, ThermalMapWriteCelsiusBufferId, defaultThermalMapAmbient);
            EnsureThermalFloatBuffer(ref _thermalMapSourceCelsiusHandle, ThermalMapSourceCelsiusBufferId, defaultThermalMapAmbient);
            EnsureThermalFloatBuffer(ref _thermalMapInsulation01Handle, ThermalMapInsulationBufferId, 0f);

            if (_thermalMapVisualCelsius == null || _thermalMapVisualCelsius.Length != ThermalMapPlaneCellCount)
            {
                // COLD ALLOC: float[1024] - center-slice thermal texture staging, not simulation truth - owner: AbyssalThermalManager
                _thermalMapVisualCelsius = new float[ThermalMapPlaneCellCount];
                FillThermalMap(_thermalMapVisualCelsius, defaultThermalMapAmbient);
            }

            if (_thermalGridRleRuns == null || _thermalGridRleRuns.Length != ThermalGridSaveRleCapacity)
            {
                // COLD ALLOC: ThermalGridRleRun[32768] - save RLE staging, not cross-frame native owner state - owner: AbyssalThermalManager
                _thermalGridRleRuns = new SaveBinaryStorage.ThermalGridRleRun[ThermalGridSaveRleCapacity];
            }
        }

        private void PrepareThermalMapResourcesCold()
        {
            if (!UsesThermalGrid())
            {
                DisposeThermalMapBuffers();
                return;
            }

            EnsureThermalMapBuffers();
            PrepareThermalMapTextureCold();
        }

        private bool HasThermalMapBuffersReady()
        {
            IDataVault vault = _dataVault;
            return UsesThermalGrid() &&
                   vault != null &&
                   !vault.IsCompactionFenceActive &&
                   HasThermalVaultBuffer(vault, in _thermalMapReadCelsiusHandle, ThermalMapReadCelsiusBufferId, ThermalMapCellCount) &&
                   HasThermalVaultBuffer(vault, in _thermalMapWriteCelsiusHandle, ThermalMapWriteCelsiusBufferId, ThermalMapCellCount) &&
                   HasThermalVaultBuffer(vault, in _thermalMapSourceCelsiusHandle, ThermalMapSourceCelsiusBufferId, ThermalMapCellCount) &&
                   HasThermalVaultBuffer(vault, in _thermalMapInsulation01Handle, ThermalMapInsulationBufferId, ThermalMapCellCount) &&
                   _thermalMapScratch.IsWriteScratchReady(ThermalMapCellCount) &&
                   _thermalMapVisualCelsius != null &&
                   _thermalMapVisualCelsius.Length == ThermalMapPlaneCellCount &&
                   _thermalGridRleRuns != null &&
                   _thermalGridRleRuns.Length == ThermalGridSaveRleCapacity;
        }

        private void DisposeThermalMapBuffers()
        {
            CompleteThermalMapJobIfReady(forceComplete: true);

            if (Volatile.Read(ref _thermalMapReadbackRetainCount) > 0)
            {
                _thermalMapDisposePending = true;
                return;
            }

            _thermalMapDisposePending = false;
            IDataVault thermalMapVault = _thermalMapReadbackGuardHeld ? _thermalMapReadbackGuardVault : _dataVault;
            ReleaseThermalMapReadbackGuard();
            ReleaseThermalFloatBuffer(thermalMapVault, ref _thermalMapReadCelsiusHandle, ThermalMapReadCelsiusBufferId);
            ReleaseThermalFloatBuffer(thermalMapVault, ref _thermalMapWriteCelsiusHandle, ThermalMapWriteCelsiusBufferId);
            ReleaseThermalFloatBuffer(thermalMapVault, ref _thermalMapSourceCelsiusHandle, ThermalMapSourceCelsiusBufferId);
            ReleaseThermalFloatBuffer(thermalMapVault, ref _thermalMapInsulation01Handle, ThermalMapInsulationBufferId);

            _thermalMapScratch.Dispose();

            _thermalMapVisualCelsius = null;
            _thermalGridRleRuns = null;

            _thermalMapJobHandle = default;
            _thermalMapJobActive = false;
            _thermalMapDiffusionSlicesCompleted = 0;
            _thermalMapDiffusionSliceCursor = 0;
            _thermalMapVersion = 0;
            _thermalGridRleRunCount = 0;
            _thermalGridRleByteCount = 0;
            _thermalGridRleChecksum = 0u;
            _thermalMapTextureDirty = false;
            _thermalMapIdleCleared = false;
            PublishThermalMapMetadata(active: false);
            ReleaseThermalMapTexture();
        }

        private void ReleaseThermalMapTexture()
        {
            _thermalMapTextureUploadedVersion = -1;
            PublishThermalMapMetadata(active: false);
            _thermalMapTextureFormatRejected = false;
            if (_thermalMapTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_thermalMapTexture);
            else
                DestroyImmediate(_thermalMapTexture);

            _thermalMapTexture = null;
        }

        private bool HasThermalMapReadBuffer()
        {
            return HasThermalVaultBuffer(
                _dataVault,
                in _thermalMapReadCelsiusHandle,
                ThermalMapReadCelsiusBufferId,
                ThermalMapCellCount);
        }

        private bool HasThermalMapStorage()
        {
            return _thermalMapScratch.WriteScratch.IsCreated ||
                   _thermalMapVisualCelsius != null ||
                   _thermalGridRleRuns != null ||
                   HasThermalMapReadBuffer();
        }

        private bool EnsureThermalFloatBuffer(
            ref VaultGenerationHandle<float> handle,
            BufferID bufferId,
            float defaultValue)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                vault.IsAllocationLocked)
            {
                return false;
            }

            if (HasThermalVaultBuffer(vault, in handle, bufferId, ThermalMapCellCount))
                return true;

            ReleaseThermalFloatBuffer(vault, ref handle, bufferId);
            handle = vault.EnsureGenerationHandle<float>(
                bufferId,
                ThermalMapCellCount,
                ThermalVaultOwnerSystem,
                NativeArrayOptions.ClearMemory);
            if (!HasThermalVaultBuffer(vault, in handle, bufferId, ThermalMapCellCount))
            {
                ReleaseThermalFloatBuffer(vault, ref handle, bufferId);
                return false;
            }

            return FillThermalMapBuffer(in handle, bufferId, defaultValue);
        }

        private bool TryReadThermalMapBuffer(
            in VaultGenerationHandle<float> handle,
            BufferID bufferId,
            out NativeArray<float> buffer)
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsThermalVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= ThermalMapCellCount;
        }

        private bool TryReadOnlyThermalMapBuffer(
            in VaultGenerationHandle<float> handle,
            BufferID bufferId,
            out NativeArray<float>.ReadOnly buffer)
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsThermalVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= ThermalMapCellCount;
        }

        private bool TryAcquireThermalMapWriteBuffer(
            in VaultGenerationHandle<float> handle,
            BufferID bufferId,
            out NativeArray<float> buffer,
            out IDataVault writeVault)
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsThermalVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, ThermalVaultOwnerSystem, out buffer))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (!vault.IsCompactionFenceActive &&
                    buffer.IsCreated &&
                    buffer.Length >= ThermalMapCellCount)
                {
                    writeVault = vault;
                    ownershipTransferred = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in handle, ThermalVaultOwnerSystem);
            }
        }

        private bool FillThermalMapBuffer(
            in VaultGenerationHandle<float> handle,
            BufferID bufferId,
            float value)
        {
            if (_thermalMapScratch.IsWriteScratchReady(ThermalMapCellCount))
            {
                _thermalMapScratch.FillWriteScratch(value);
                return CopyThermalMapScratchToBuffer(in handle, bufferId);
            }

            if (!TryAcquireThermalMapWriteBuffer(in handle, bufferId, out NativeArray<float> buffer, out IDataVault writeVault))
                return false;

            try
            {
                FillThermalMap(buffer, value);
                return true;
            }
            finally
            {
                writeVault.ReleaseWriteLock(in handle, ThermalVaultOwnerSystem);
            }
        }

        private bool CopyThermalMapScratchToBuffer(
            in VaultGenerationHandle<float> destinationHandle,
            BufferID destinationBufferId)
        {
            if (!_thermalMapScratch.IsWriteScratchReady(ThermalMapCellCount) ||
                !TryAcquireThermalMapWriteBuffer(in destinationHandle, destinationBufferId, out NativeArray<float> destination, out IDataVault destinationWriteVault))
            {
                return false;
            }

            try
            {
                NativeArray<float>.Copy(_thermalMapScratch.WriteScratch, destination, ThermalMapCellCount);
                return true;
            }
            finally
            {
                destinationWriteVault.ReleaseWriteLock(in destinationHandle, ThermalVaultOwnerSystem);
            }
        }

        private bool TryLockThermalMapReadback()
        {
            IDataVault vault = _dataVault;
            if (!UsesThermalGrid() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !IsThermalVaultHandle(in _thermalMapReadCelsiusHandle, ThermalMapReadCelsiusBufferId) ||
                (_thermalMapReadbackGuardHeld && !ReferenceEquals(_thermalMapReadbackGuardVault, vault)))
            {
                return false;
            }

            if (_thermalMapReadbackGuardHeld)
                return true;

            if (!vault.TryAcquireMutationGuard(ThermalMapReadbackMutationGuardMask))
                return false;

            _thermalMapReadbackGuardVault = vault;
            _thermalMapReadbackGuardHeld = true;
            if (!vault.IsCompactionFenceActive)
                return true;

            ReleaseThermalMapReadbackGuard();
            return false;
        }

        private void ReleaseThermalMapReadbackGuard()
        {
            if (!_thermalMapReadbackGuardHeld)
                return;

            IDataVault vault = _thermalMapReadbackGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(ThermalMapReadbackMutationGuardMask);

            _thermalMapReadbackGuardHeld = false;
            _thermalMapReadbackGuardVault = null;
        }

        private static ulong ThermalVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void ReleaseThermalFloatBuffer(
            IDataVault vault,
            ref VaultGenerationHandle<float> handle,
            BufferID bufferId)
        {
            if (vault != null && IsThermalVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void DisposeThermalTelemetry()
        {
            ReleaseThermalTelemetry(_dataVault);
            DisposeThermalTelemetryDumpPayload();
            _thermalTelemetryIndex = 0;
        }

        private bool EnsureThermalTelemetry()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                vault.IsAllocationLocked)
            {
                return false;
            }

            if (HasThermalVaultBuffer(vault, in _thermalTelemetryRingHandle, BufferID.AbyssalThermalManagerTelemetryRing, ThermalTelemetryCapacity))
                return IsThermalTelemetryDumpPayloadReady();

            ReleaseThermalTelemetry(vault);
            _thermalTelemetryRingHandle = vault.EnsureGenerationHandle<AbyssalThermalManagerTelemetryEntry>(
                BufferID.AbyssalThermalManagerTelemetryRing,
                ThermalTelemetryCapacity,
                ThermalVaultOwnerSystem,
                NativeArrayOptions.ClearMemory);

            if (HasThermalVaultBuffer(vault, in _thermalTelemetryRingHandle, BufferID.AbyssalThermalManagerTelemetryRing, ThermalTelemetryCapacity))
                return IsThermalTelemetryDumpPayloadReady();

            ReleaseThermalTelemetry(vault);
            return false;
        }

        private bool EnsureThermalTelemetryDumpPayloadCold()
        {
            if (_thermalTelemetryDumpPayload.IsCreated &&
                _thermalTelemetryDumpPayload.Length >= ThermalTelemetryDumpPayloadBytes)
            {
                return true;
            }

            DisposeThermalTelemetryDumpPayload();

            _thermalTelemetryDumpPayload = H8Memory.Allocate<byte>(
                ThermalTelemetryDumpPayloadBytes,
                ThermalVaultOwnerSystem,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            return IsThermalTelemetryDumpPayloadReady();
        }

        private bool IsThermalTelemetryDumpPayloadReady()
        {
            return _thermalTelemetryDumpPayload.IsCreated &&
                   _thermalTelemetryDumpPayload.Length >= ThermalTelemetryDumpPayloadBytes;
        }

        private void DisposeThermalTelemetryDumpPayload()
        {
            if (!_thermalTelemetryDumpPayload.IsCreated)
                return;

            H8Memory.Release(ref _thermalTelemetryDumpPayload, ThermalVaultOwnerSystem);
        }

        private void ReleaseThermalTelemetry(IDataVault vault)
        {
            if (vault != null && IsThermalVaultHandle(in _thermalTelemetryRingHandle, BufferID.AbyssalThermalManagerTelemetryRing))
                vault.ReleaseBuffer(in _thermalTelemetryRingHandle);

            _thermalTelemetryRingHandle = default;
        }

        private static bool HasThermalVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsThermalVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsThermalVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)ThermalVaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        private void ResolveDependencies()
        {
            if (biomeMatrixDirector == null || !biomeMatrixDirector.isActiveAndEnabled)
            {
                biomeMatrixDirector = null;
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            }

            if (worldZoneDirector == null || !worldZoneDirector.isActiveAndEnabled)
            {
                worldZoneDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
            }

            if (resourceDistributionDirector == null || !resourceDistributionDirector.isActiveAndEnabled)
            {
                resourceDistributionDirector = null;
                WorldRuntimeReferenceUtility.TryResolveResourceDistributionDirector(ref resourceDistributionDirector);
            }

            if (vegetationBridge == null)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);

            if (playerTransform == null)
            {
                if (_playerRuntimeContext != null && _playerRuntimeContext.PlayerTransform != null)
                    playerTransform = _playerRuntimeContext.PlayerTransform;
                else if (BootstrapState.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer))
                    playerTransform = bootstrapPlayer;
            }

            RefreshPlayerComponentCaches();
            RefreshThermalDamageReceiverCaches();

            if (viewCamera == null && playerTransform != null)
            {
                viewCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
                if (viewCamera == null)
                    playerTransform.TryGetComponent(out viewCamera);
            }

            CacheVoxelDeltaProcessorCold();
            RefreshFluidDecalOwner();
        }

        private void CacheVoxelDeltaProcessorCold()
        {
            HectonVoxelEngine engine = null;
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref engine);
            _voxelDeltaProcessor = engine != null ? engine.DeltaProcessor : null;
        }

        private void RefreshPlayerComponentCaches()
        {
            if (_playerTransportCoordinator == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);
        }

        private void RefreshThermalDamageReceiverCaches()
        {
            RefreshPlayerThermalDamageReceiver();
            RefreshSubmarineThermalDamageReceiver();
        }

        private void RefreshPlayerThermalDamageReceiver()
        {
            _playerThermalDamageReceiver = null;
            _playerThermalDamageTransform = null;

            HectonPlayerHealth playerHealth = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerHealth : null;
            if (playerHealth != null)
            {
                _playerThermalDamageReceiver = playerHealth;
                _playerThermalDamageTransform = playerHealth.transform;
                return;
            }

            if (playerTransform != null &&
                FindDamageReceiverInParentsCold(playerTransform, out IDamageReceiver receiver, out Transform receiverTransform))
            {
                _playerThermalDamageReceiver = receiver;
                _playerThermalDamageTransform = receiverTransform;
            }
        }

        private void RefreshSubmarineThermalDamageReceiver()
        {
            _submarineThermalDamageReceiver = null;
            _submarineThermalDamageTransform = null;

            Rigidbody hull = _submarineRuntimeContext != null ? _submarineRuntimeContext.HullRigidbody : null;
            if (hull == null)
                return;

            if (FindDamageReceiverInParentsCold(hull.transform, out IDamageReceiver receiver, out Transform receiverTransform))
            {
                _submarineThermalDamageReceiver = receiver;
                _submarineThermalDamageTransform = receiverTransform;
            }
        }

        private static bool FindDamageReceiverInParentsCold(
            Transform start,
            out IDamageReceiver receiver,
            out Transform receiverTransform)
        {
            receiver = null;
            receiverTransform = null;
            Transform current = start;
            for (int depth = 0; depth < 6 && current != null; depth++)
            {
                if (current.TryGetComponent(out IDamageReceiver candidateReceiver))
                {
                    receiver = candidateReceiver;
                    receiverTransform = current;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void RefreshFluidDecalOwner()
        {
            if (_fluidDecalManager == null)
            {
                if (fluidDecalManager != null)
                {
                    _fluidDecalManager = fluidDecalManager;
                }
                else
                {
                    TryGetComponent(out _fluidDecalManager);
                }
            }

            if (_fluidDecalManager != null && fluidDecalMaterial != null)
                _fluidDecalManager.ConfigureMaterial(fluidDecalMaterial);
        }

        private void RebindPlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = playerRuntimeContext;
            if (_playerRuntimeContext == null)
            {
                playerTransform = null;
                viewCamera = null;
                _playerTransportCoordinator = null;
                _playerMovement = null;
                _playerThermalDamageReceiver = null;
                _playerThermalDamageTransform = null;
                return;
            }

            playerTransform = _playerRuntimeContext.PlayerTransform != null
                ? _playerRuntimeContext.PlayerTransform
                : playerTransform;
            viewCamera = _playerRuntimeContext.PlayerCamera != null
                ? _playerRuntimeContext.PlayerCamera
                : viewCamera;
            _playerTransportCoordinator = null;
            _playerMovement = _playerRuntimeContext.PlayerMovement != null
                ? _playerRuntimeContext.PlayerMovement
                : null;
            RefreshPlayerComponentCaches();
            RefreshPlayerThermalDamageReceiver();
        }

        private void RebindSubmarineRuntimeContext(ISubmarineRuntimeContext submarineRuntimeContext)
        {
            _submarineRuntimeContext = submarineRuntimeContext;
            RefreshSubmarineThermalDamageReceiver();
        }

        private void CacheRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerRuntimeContext = GlobalRegistry.Player;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _physicsService = GlobalRegistry.Physics;
            _gasDynamics = GlobalRegistry.GasDynamics;
            CacheObjectPoolService(null);
            _pdaCorrosionPresentationSink = GlobalRegistry.PdaCorrosionPresentationSink;
            if (cutManager == null)
                WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);
            if (_simulationBucketer == null)
                _simulationBucketer = GlobalRegistry.SimulationBucketer;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPoolService = pool;
                return;
            }

            _objectPoolService = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPoolService = resolved;
                pool = resolved;
                return true;
            }

            _objectPoolService = null;
            pool = null;
            return false;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsGraphicsFence = SystemInfo.supportsGraphicsFence;
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
            _supportsThermalMapTextureCold = SystemInfo.SupportsTextureFormat(TextureFormat.RFloat);
            _thermalGridVramWeight01 = ResolveThermalGridVramWeight01(SystemInfo.graphicsMemorySize);
            if (!_supportsThermalMapTextureCold)
                _thermalMapTextureFormatRejected = true;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    RebindSubmarineRuntimeContext(currentService as ISubmarineRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.GasDynamicsRuntime:
                    _gasDynamics = currentService as IGasDynamicsSolver;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    ConfigureBioCableObjectPoolServiceCold();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _pdaCorrosionPresentationSink = currentService as IPdaCorrosionPresentationSink;
                    break;
                case GlobalRegistryServiceSlot.SargassumCutRuntime:
                    cutManager = currentService as SargassumCutManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref cutManager);
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketer = currentService as ISimulationBucketer;
                    break;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    biomeMatrixDirector = currentService as BiomeMatrixDirector;
                    WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
                    if (HasSlowTickStorageReady())
                        RebuildVentField();
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    HectonVoxelEngine engine = currentService as HectonVoxelEngine;
                    WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref engine);
                    _voxelDeltaProcessor = engine != null ? engine.DeltaProcessor : null;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    DisposeThermalMapBuffers();
                    ReleaseThermalTelemetry(previousService as IDataVault ?? _dataVault);
                    _dataVault = currentService as IDataVault;
                    EnsureThermalTelemetry();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherTicks();
                    if (currentService != null)
                        TryRegister();
                    break;
            }
        }

        private void SanitizeSettings()
        {
            maxActiveVentCount = Mathf.Clamp(maxActiveVentCount, 1, MaxVentCapacity);
            maxVentsPerAnchor = Mathf.Clamp(maxVentsPerAnchor, 1, 4);
            ventAnchorRadiusFraction = Mathf.Clamp(ventAnchorRadiusFraction, 0.1f, 0.9f);
            ventRadiusMin = Mathf.Clamp(ventRadiusMin, 2f, ventRadiusMax);
            ventRadiusMax = Mathf.Max(ventRadiusMin, ventRadiusMax);
            ventHeight = Mathf.Clamp(ventHeight, 4f, 90f);
            ventUpdraftVelocity = Mathf.Clamp(ventUpdraftVelocity, 1f, 40f);
            ventDragMultiplier = Mathf.Clamp(ventDragMultiplier, 1f, 8f);
            ventHeatIntensity = Mathf.Clamp(ventHeatIntensity, 1f, 60f);
            ventHeatRadiusMultiplier = Mathf.Clamp(ventHeatRadiusMultiplier, 0.25f, 2f);
            thermalMapWorldSizeMeters = Mathf.Clamp(thermalMapWorldSizeMeters, 32f, 512f);
            thermalMapColdTickSeconds = Mathf.Clamp(thermalMapColdTickSeconds, 0.25f, 4f);
            thermalMapDiffusion01 = Mathf.Clamp01(thermalMapDiffusion01);
            ambientWaterTemperatureCelsius = Mathf.Clamp(ambientWaterTemperatureCelsius, -4f, 12f);
            thermalConvectionVelocityPerSecond = Mathf.Clamp(thermalConvectionVelocityPerSecond, 1f, 900f);
            boilingDamagePerSecond = Mathf.Clamp(boilingDamagePerSecond, 0f, 20f);
            condensationTemperatureJumpCelsius = Mathf.Clamp(condensationTemperatureJumpCelsius, 1f, 60f);
            condensationDecayPerSecond = Mathf.Clamp(condensationDecayPerSecond, 0.1f, 8f);
            ventEruptionCycleSeconds = Mathf.Clamp(ventEruptionCycleSeconds, 2f, 90f);
            ventEruptionDuty01 = Mathf.Clamp(ventEruptionDuty01, 0.05f, 0.95f);
            thermalRoarCooldownSeconds = Mathf.Clamp(thermalRoarCooldownSeconds, 0.05f, 2f);
            if (crystallizationDeltaTemperatureThresholdCelsius > 0f)
                crystallizationDeltaTemperatureThresholdCelsius = -crystallizationDeltaTemperatureThresholdCelsius;

            crystallizationDeltaTemperatureThresholdCelsius = Mathf.Clamp(crystallizationDeltaTemperatureThresholdCelsius, -300f, -100f);
            crystallizationMinimumSourceTemperatureCelsius = Mathf.Clamp(crystallizationMinimumSourceTemperatureCelsius, 300f, 900f);
            freezingCurrentTemperatureCelsius = Mathf.Clamp(freezingCurrentTemperatureCelsius, -8f, 8f);
            ventHeatToCelsiusScale = Mathf.Clamp(ventHeatToCelsiusScale, 18f, 40f);
            crystallizationNodeRadiusMeters = Mathf.Clamp(crystallizationNodeRadiusMeters, 0.5f, 8f);
            passiveCrystallizationCooldownSeconds = Mathf.Clamp(passiveCrystallizationCooldownSeconds, 2f, 120f);
            cableRadiusMultiplier = Mathf.Clamp(cableRadiusMultiplier, 0.5f, 1.5f);
            cableAnchorPull = Mathf.Clamp(cableAnchorPull, 0f, 2f);
            cableCutQueryRadius = Mathf.Clamp(cableCutQueryRadius, 0.1f, 6f);
            cableCutReleaseThreshold = Mathf.Clamp01(cableCutReleaseThreshold);
            cableCutStampInterval = Mathf.Clamp(cableCutStampInterval, 0.05f, 2f);
            cableCutStampRadius = Mathf.Clamp(cableCutStampRadius, 0.1f, 3f);
            cableCutStampStrength = Mathf.Clamp01(cableCutStampStrength);
            cableCutForwardOffset = Mathf.Clamp(cableCutForwardOffset, 0f, 2f);
            cableVisualActivationDistance = Mathf.Clamp(cableVisualActivationDistance, 0.5f, 24f);
            cableVisualHullRadius = Mathf.Clamp(cableVisualHullRadius, 0.1f, 6f);
            cableVisualWrapThreshold = Mathf.Clamp01(cableVisualWrapThreshold);
            cableVisualTransportBoost = Mathf.Clamp(cableVisualTransportBoost, 0f, 2f);
            cableSnapRecoilSpeed = Mathf.Clamp(cableSnapRecoilSpeed, 0.1f, 24f);
            cableSnapDuration = Mathf.Clamp(cableSnapDuration, 0.1f, 4f);
            cableSnapVerticalLift = Mathf.Clamp(cableSnapVerticalLift, 0f, 4f);
            cableElasticReleaseDuration = Mathf.Clamp(cableElasticReleaseDuration, 0.1f, 4f);
            maxEmpNestCount = Mathf.Clamp(maxEmpNestCount, 1, MaxEmpNestCapacity);
            empNestRadiusMultiplier = Mathf.Clamp(empNestRadiusMultiplier, 0.25f, 2f);
            empChargeDuration = Mathf.Clamp(empChargeDuration, 0.25f, 8f);
            empDischargeDuration = Mathf.Clamp(empDischargeDuration, 0.1f, 8f);
            empCooldown = Mathf.Clamp(empCooldown, 0.1f, 8f);
            empSphereCastRadius = Mathf.Clamp(empSphereCastRadius, 0.1f, 6f);
            empPulseRange = Mathf.Clamp(empPulseRange, 1f, 24f);
            empMisfireDuration = Mathf.Clamp(empMisfireDuration, 0.1f, 6f);
            empPdaCorrosionDuration = Mathf.Clamp(empPdaCorrosionDuration, 0.1f, 10f);
            empPulseSpeed = Mathf.Clamp(empPulseSpeed, 0.1f, 8f);
            empChainPropagationSpeed = Mathf.Clamp(empChainPropagationSpeed, 2f, 64f);
            empChainGlowDuration = Mathf.Clamp(empChainGlowDuration, 0.1f, 4f);
            smokeParticleCount = Mathf.Clamp(smokeParticleCount, 256, MaxSmokeParticleCapacity);
            smokeParticleSizeMin = Mathf.Clamp(smokeParticleSizeMin, 0.02f, smokeParticleSizeMax);
            smokeParticleSizeMax = Mathf.Max(smokeParticleSizeMin, smokeParticleSizeMax);
            smokeLateralDrift = Mathf.Clamp(smokeLateralDrift, 0f, 6f);
            smokeUpdraftJitter = Mathf.Clamp(smokeUpdraftJitter, 0f, 6f);
            smokeNoiseWeight = Mathf.Clamp(smokeNoiseWeight, 0f, 4f);
            smokeMaxViewDistance = Mathf.Clamp(smokeMaxViewDistance, 10f, 300f);
            smokeSoftness = Mathf.Clamp(smokeSoftness, 0.5f, 4f);
        }

        private void EnsureStorage()
        {
            if (_ventStates == null || _ventStates.Length != MaxVentCapacity)
            {
                // COLD ALLOC: ThermalVentState[16] - CPU vent metadata and cable anchors for abyssal sampling - owner: AbyssalThermalManager
                _ventStates = new ThermalVentState[MaxVentCapacity];
            }

            if (_ventGpuData == null || _ventGpuData.Length != MaxVentCapacity)
            {
                // COLD ALLOC: ThermalVentGpuData[16] - CPU upload staging for hydrothermal vent compute data - owner: AbyssalThermalManager
                _ventGpuData = new ThermalVentGpuData[MaxVentCapacity];
            }

            if (_lastUploadedVentGpuData == null || _lastUploadedVentGpuData.Length != MaxVentCapacity)
            {
                // COLD ALLOC: ThermalVentGpuData[16] - previous uploaded vent snapshot used to avoid redundant GPU uploads - owner: AbyssalThermalManager
                _lastUploadedVentGpuData = new ThermalVentGpuData[MaxVentCapacity];
                _forceVentBufferUpload = true;
            }

            if (_lastSeededVentStates == null || _lastSeededVentStates.Length != MaxVentCapacity)
            {
                // COLD ALLOC: ThermalVentState[16] - previous vent topology snapshot used to avoid redundant smoke reseeds - owner: AbyssalThermalManager
                _lastSeededVentStates = new ThermalVentState[MaxVentCapacity];
                _forceParticleReset = true;
            }

            if (_initialParticles == null || _initialParticles.Length != smokeParticleCount)
            {
                // COLD ALLOC: AshParticleData[smokeParticleCount] - deterministic initial plume state for abyssal smoke ping-pong buffers - owner: AbyssalThermalManager
                _initialParticles = new AshParticleData[smokeParticleCount];
            }

            if (_empNests == null || _empNests.Length != MaxEmpNestCapacity)
            {
                // COLD ALLOC: EmpNestState[8] - EMP nest runtime state derived from dense abyssal cable anchors - owner: AbyssalThermalManager
                _empNests = new EmpNestState[MaxEmpNestCapacity];
            }

            if (_cableReleasedStates == null || _cableReleasedStates.Length != MaxVentCapacity)
            {
                // COLD ALLOC: bool[16] - per-cable sever state used to detect snap transitions without allocations - owner: AbyssalThermalManager
                _cableReleasedStates = new bool[MaxVentCapacity];
            }

            if (_cableReleaseProgress == null || _cableReleaseProgress.Length != MaxVentCapacity)
            {
                // COLD ALLOC: float[16] - per-cable previous cut progress used for snap/rearm gating - owner: AbyssalThermalManager
                _cableReleaseProgress = new float[MaxVentCapacity];
            }

            if (_cableElasticReleaseTimers == null || _cableElasticReleaseTimers.Length != MaxVentCapacity)
            {
                // COLD ALLOC: float[16] - per-cable elastic rupture release timers so snapped cables do not instantly re-arm - owner: AbyssalThermalManager
                _cableElasticReleaseTimers = new float[MaxVentCapacity];
            }

            if (_cableEmpChainDelayTimers == null || _cableEmpChainDelayTimers.Length != MaxVentCapacity)
            {
                // COLD ALLOC: float[16] - per-cable EMP chain propagation delays so overcharge walks deterministically across linked vents - owner: AbyssalThermalManager
                _cableEmpChainDelayTimers = new float[MaxVentCapacity];
            }

            if (_cableEmpChainGlowTimers == null || _cableEmpChainGlowTimers.Length != MaxVentCapacity)
            {
                // COLD ALLOC: float[16] - per-cable EMP chain glow timers keeping emission and sparks alive after the charge front arrives - owner: AbyssalThermalManager
                _cableEmpChainGlowTimers = new float[MaxVentCapacity];
            }

            if (_ventCrystallizationCooldowns == null || _ventCrystallizationCooldowns.Length != MaxVentCapacity)
            {
                // COLD ALLOC: float[16] - per-vent passive flash-freeze cooldowns - owner: AbyssalThermalManager
                _ventCrystallizationCooldowns = new float[MaxVentCapacity];
            }

            if (_crystallizationSamples == null || _crystallizationSamples.Length != MaxCrystallizationSampleCapacity)
            {
                // COLD ALLOC: ThermalCrystallizationSample[32] - bounded thermal boundary input ring - owner: AbyssalThermalManager
                _crystallizationSamples = new ThermalCrystallizationSample[MaxCrystallizationSampleCapacity];
            }

            if (_crystallizationResults == null || _crystallizationResults.Length != MaxCrystallizationSampleCapacity)
            {
                // COLD ALLOC: ThermalCrystallizationResult[32] - bounded thermal boundary output ring - owner: AbyssalThermalManager
                _crystallizationResults = new ThermalCrystallizationResult[MaxCrystallizationSampleCapacity];
            }

            EnsureThermalTelemetryDumpPayloadCold();
            EnsureThermalTelemetry();

            if (UsesThermalGrid())
            {
                EnsureThermalMapBuffers();
            }
            else
            {
                DisposeThermalMapBuffers();
            }

            if (_bioCableVisuals == null || _bioCableVisuals.Length != MaxVentCapacity)
            {
                // COLD ALLOC: BioCableIK[16] - reusable visual cable rigs paired to active abyssal vent cable zones - owner: AbyssalThermalManager
                _bioCableVisuals = new BioCableIK[MaxVentCapacity];
            }

            if (_bioCableVisualsPooled == null || _bioCableVisualsPooled.Length != MaxVentCapacity)
            {
                // COLD ALLOC: bool[16] - pooled ownership flags for reusable abyssal bio-cable visual rigs - owner: AbyssalThermalManager
                _bioCableVisualsPooled = new bool[MaxVentCapacity];
            }

            if (_thermalBubbleCommands == null || _thermalBubbleCommands.Length != MaxVentCapacity)
            {
                // COLD ALLOC: Vector4[16] - GPU boiling-bubble command staging for active vents - owner: AbyssalThermalManager
                _thermalBubbleCommands = new Vector4[MaxVentCapacity];
            }

            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - abyssal smoke draw parameters - owner: AbyssalThermalManager
        }

        private bool HasSlowTickStorageReady()
        {
            return _ventStates != null &&
                   _ventStates.Length == MaxVentCapacity &&
                   _ventGpuData != null &&
                   _ventGpuData.Length == MaxVentCapacity &&
                   _lastUploadedVentGpuData != null &&
                   _lastUploadedVentGpuData.Length == MaxVentCapacity &&
                   _lastSeededVentStates != null &&
                   _lastSeededVentStates.Length == MaxVentCapacity &&
                   _initialParticles != null &&
                   _initialParticles.Length == smokeParticleCount &&
                   _empNests != null &&
                   _empNests.Length == MaxEmpNestCapacity &&
                   _cableReleasedStates != null &&
                   _cableReleasedStates.Length == MaxVentCapacity &&
                   _cableReleaseProgress != null &&
                   _cableReleaseProgress.Length == MaxVentCapacity &&
                   _cableElasticReleaseTimers != null &&
                   _cableElasticReleaseTimers.Length == MaxVentCapacity &&
                   _cableEmpChainDelayTimers != null &&
                   _cableEmpChainDelayTimers.Length == MaxVentCapacity &&
                   _cableEmpChainGlowTimers != null &&
                   _cableEmpChainGlowTimers.Length == MaxVentCapacity &&
                   _ventCrystallizationCooldowns != null &&
                   _ventCrystallizationCooldowns.Length == MaxVentCapacity &&
                   _crystallizationSamples != null &&
                   _crystallizationSamples.Length == MaxCrystallizationSampleCapacity &&
                   _crystallizationResults != null &&
                   _crystallizationResults.Length == MaxCrystallizationSampleCapacity &&
                   _bioCableVisuals != null &&
                   _bioCableVisuals.Length == MaxVentCapacity &&
                   _thermalBubbleCommands != null &&
                   _thermalBubbleCommands.Length == MaxVentCapacity &&
                   _materialPropertyBlock != null &&
                   HasThermalVaultBuffer(
                       _dataVault,
                       in _thermalTelemetryRingHandle,
                       BufferID.AbyssalThermalManagerTelemetryRing,
                       ThermalTelemetryCapacity);
        }

        private bool HasThermalMapRuntimeResourcesReady()
        {
            if (!UsesThermalGrid())
                return true;

            if (_activeVentCount <= 0 && !_thermalMapJobActive && !HasThermalMapStorage())
                return true;

            if (!HasThermalMapBuffersReady())
                return false;

            return _activeVentCount <= 0 ||
                   _thermalMapTexture != null ||
                   !_supportsThermalMapTextureCold ||
                   _thermalMapTextureFormatRejected;
        }

        private bool HasSmokeGpuRuntimeResourcesReady()
        {
            return IsBufferReady<AshParticleData>(_particleBufferA, smokeParticleCount) &&
                   IsBufferReady<AshParticleData>(_particleBufferB, smokeParticleCount) &&
                   IsBufferReady<AshParticleData>(_particleUploadStagingBuffer, smokeParticleCount) &&
                   HasVentBufferRingReady();
        }

        private void EnsureBuffers()
        {
            bool particleBufferARecreated = EnsureGpuWriteBuffer<AshParticleData>(ref _particleBufferA, smokeParticleCount);
            bool particleBufferBRecreated = EnsureGpuWriteBuffer<AshParticleData>(ref _particleBufferB, smokeParticleCount);
            bool particleUploadWasReady = IsBufferReady<AshParticleData>(_particleUploadStagingBuffer, smokeParticleCount);
            bool particleUploadReady = EnsureParticleUploadStagingBuffer(smokeParticleCount);
            bool ventBufferRingRecreated = EnsureVentBufferRing();
            if (particleBufferARecreated || particleBufferBRecreated)
            {
                _smokeDispatchFenceArmed = false;
                _forceParticleReset = true;
            }
            if (!particleUploadReady)
                _forceParticleReset = false;
            else if (!particleUploadWasReady)
                _forceParticleReset = true;

            if (ventBufferRingRecreated)
                _forceVentBufferUpload = true;

            if (blackSmokeCompute == null || !_supportsComputeShadersCold)
            {
                _kernelIndex = -1;
                _threadGroupSizeX = 0;
                _dispatchGroupCount = 0;
                return;
            }

            if (_kernelIndex < 0 && !TryResolveBlackSmokeKernel())
                return;

            _dispatchGroupCount = ResolveDispatchGroups(smokeParticleCount, _threadGroupSizeX);
        }

        private bool EnsureVentBufferRing()
        {
            bool recreated = false;
            if (_ventBuffers == null || _ventBuffers.Length != VentBufferRingSize)
            {
                ReleaseBufferRing(ref _ventBuffers);
                // COLD ALLOC: GraphicsBuffer[3] - triple-buffered hydrothermal vent upload ring preventing CPU writes to in-flight GPU read buffers - owner: AbyssalThermalManager
                _ventBuffers = new GraphicsBuffer[VentBufferRingSize];
                _activeVentBufferIndex = 0;
                _nextVentBufferUploadIndex = 1;
                recreated = true;
            }

            if (_ventBufferFences == null || _ventBufferFences.Length != VentBufferRingSize)
            {
                // COLD ALLOC: GraphicsFence[3] - per-slot GPU completion fences guarding vent ring reuse - owner: AbyssalThermalManager
                _ventBufferFences = new GraphicsFence[VentBufferRingSize];
                recreated = true;
            }

            if (_ventBufferFenceArmed == null || _ventBufferFenceArmed.Length != VentBufferRingSize)
            {
                // COLD ALLOC: bool[3] - per-slot fence armed bits preventing default-fence reads - owner: AbyssalThermalManager
                _ventBufferFenceArmed = new bool[VentBufferRingSize];
                recreated = true;
            }

            for (int i = 0; i < VentBufferRingSize; i++)
            {
                if (EnsureBuffer<ThermalVentGpuData>(ref _ventBuffers[i], MaxVentCapacity))
                {
                    if (_ventBufferFences != null && i < _ventBufferFences.Length)
                        _ventBufferFences[i] = default;
                    if (_ventBufferFenceArmed != null && i < _ventBufferFenceArmed.Length)
                        _ventBufferFenceArmed[i] = false;
                    recreated = true;
                }
            }

            return recreated;
        }

        private bool HasVentBufferRingReady()
        {
            if (_ventBuffers == null ||
                _ventBuffers.Length != VentBufferRingSize ||
                _ventBufferFences == null ||
                _ventBufferFences.Length != VentBufferRingSize ||
                _ventBufferFenceArmed == null ||
                _ventBufferFenceArmed.Length != VentBufferRingSize)
            {
                return false;
            }

            for (int i = 0; i < VentBufferRingSize; i++)
            {
                if (!IsBufferReady<ThermalVentGpuData>(_ventBuffers[i], MaxVentCapacity))
                    return false;
            }

            return true;
        }

        private bool HasVentUploadStorageReady()
        {
            return _ventStates != null &&
                   _ventStates.Length == MaxVentCapacity &&
                   _ventGpuData != null &&
                   _ventGpuData.Length == MaxVentCapacity &&
                   _lastUploadedVentGpuData != null &&
                   _lastUploadedVentGpuData.Length == MaxVentCapacity &&
                   HasVentBufferRingReady();
        }

        private bool HasParticleResetStorageReady()
        {
            return _ventStates != null &&
                   _ventStates.Length == MaxVentCapacity &&
                   _lastSeededVentStates != null &&
                   _lastSeededVentStates.Length == MaxVentCapacity &&
                   _initialParticles != null &&
                   _initialParticles.Length == smokeParticleCount &&
                   IsBufferReady<AshParticleData>(_particleBufferA, smokeParticleCount) &&
                   IsBufferReady<AshParticleData>(_particleBufferB, smokeParticleCount) &&
                   IsBufferReady<AshParticleData>(_particleUploadStagingBuffer, smokeParticleCount);
        }

        private void EnsureCableVisuals()
        {
            if (_bioCableVisuals == null)
                return;

            Material resolvedCableMaterial = ResolveBioCableMaterialCold();
            if (resolvedCableMaterial == null)
                return;

            for (int i = 0; i < _bioCableVisuals.Length; i++)
            {
                BioCableIK cableRig = _bioCableVisuals[i];
                if (cableRig != null)
                {
                    ConfigureBioCableVisual(cableRig, resolvedCableMaterial);
                    continue;
                }

                bool pooled = false;
                cableRig = ResolveAuthoredBioCableVisual(i);
                if (cableRig == null)
                    cableRig = SpawnBioCableVisualFromPool(out pooled);
                if (cableRig == null)
                    continue;

                ConfigureBioCableVisual(cableRig, resolvedCableMaterial);
                _bioCableVisuals[i] = cableRig;
                if (_bioCableVisualsPooled != null && i < _bioCableVisualsPooled.Length)
                    _bioCableVisualsPooled[i] = pooled;
            }
        }

        private BioCableIK ResolveAuthoredBioCableVisual(int index)
        {
            if (authoredBioCableVisuals == null ||
                index < 0 ||
                index >= authoredBioCableVisuals.Length)
            {
                return null;
            }

            return authoredBioCableVisuals[index];
        }

        private BioCableIK SpawnBioCableVisualFromPool(out bool pooled)
        {
            pooled = false;
            if (bioCablePrefab == null)
                return null;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return null;

            GameObject prefabObject = bioCablePrefab.gameObject;
            if (!pool.HasPool(prefabObject) ||
                pool.GetAvailableCount(prefabObject) <= 0)
            {
                return null;
            }

            GameObject instance = pool.Spawn(prefabObject, transform.position, transform.rotation, false);
            if (instance == null)
                return null;

            if (!pool.CanDespawnWithoutDestroy(instance))
            {
                pool.Despawn(instance);
                return null;
            }

            if (!instance.TryGetComponent(out BioCableIK cableRig))
            {
                pool.Despawn(instance);
                return null;
            }

            pooled = true;
            Transform rigTransform = instance.transform;
            rigTransform.SetParent(transform, false);
            rigTransform.localPosition = Vector3.zero;
            rigTransform.localRotation = Quaternion.identity;
            rigTransform.localScale = Vector3.one;
            return cableRig;
        }

        private void ConfigureBioCableVisual(BioCableIK cableRig, Material resolvedCableMaterial)
        {
            if (cableRig == null)
                return;

            GameObject cableObject = cableRig.gameObject;
            if (cableObject != null && !cableObject.activeSelf)
                cableObject.SetActive(true);

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            cableRig.ConfigureObjectPoolServiceCold(pool);
            cableRig.SetCableMaterialCold(resolvedCableMaterial);
            cableRig.InitializeAt(transform.position, Vector3.up);
            cableRig.SetCableActive(false);
        }

        private void ConfigureBioCableObjectPoolServiceCold()
        {
            if (_bioCableVisuals == null)
                return;

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            for (int i = 0; i < _bioCableVisuals.Length; i++)
            {
                BioCableIK cableRig = _bioCableVisuals[i];
                if (cableRig != null)
                    cableRig.ConfigureObjectPoolServiceCold(pool);
            }
        }

        private void ReleaseBioCableVisualsToPool()
        {
            if (_bioCableVisuals == null || _bioCableVisualsPooled == null)
                return;

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            for (int i = 0; i < _bioCableVisuals.Length && i < _bioCableVisualsPooled.Length; i++)
            {
                BioCableIK cableRig = _bioCableVisuals[i];
                if (cableRig == null)
                    continue;

                GameObject cableObject = cableRig.gameObject;
                if (!_bioCableVisualsPooled[i])
                {
                    cableRig.SetCableActive(false);
                    continue;
                }

                cableRig.PrepareForPoolReturnCold();
                if (pool != null && cableObject != null && pool.CanDespawnWithoutDestroy(cableObject))
                    pool.Despawn(cableObject);

                _bioCableVisuals[i] = null;
                _bioCableVisualsPooled[i] = false;
            }
        }

        private Material ResolveBioCableMaterialCold()
        {
            if (bioCableMaterial != null)
                return bioCableMaterial;

#if UNITY_EDITOR
            bioCableMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultBioCableMaterialPath);
            if (bioCableMaterial != null)
                return bioCableMaterial;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedMissingBioCableMaterial)
            {
                _loggedMissingBioCableMaterial = true;
                H8Debug.LogError("[AbyssalThermalManager] Missing bioCableMaterial asset. BioCableIK rigs are disabled until an authored material is assigned.", this);
            }
#endif

            return null;
        }

        private static bool EnsureBuffer<T>(ref GraphicsBuffer buffer, int count) where T : struct
        {
            int safeCount = Mathf.Max(1, count);
            int stride = UnsafeUtility.SizeOf<T>();
            if (buffer != null && buffer.count == safeCount && buffer.stride == stride)
                return false;

            ReleaseBuffer(ref buffer);

            // COLD ALLOC: GraphicsBuffer[count] - persistent abyssal smoke or vent GPU storage sized from the owning blittable struct - owner: AbyssalThermalManager
            buffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>(safeCount);
            return true;
        }

        private static bool EnsureGpuWriteBuffer<T>(ref GraphicsBuffer buffer, int count) where T : struct
        {
            int safeCount = Mathf.Max(1, count);
            int stride = UnsafeUtility.SizeOf<T>();
            if (buffer != null && buffer.count == safeCount && buffer.stride == stride)
                return false;

            ReleaseBuffer(ref buffer);

            // COLD ALLOC: GraphicsBuffer[count] - GPU-write-capable structured storage for RWStructuredBuffer ping-pong state - owner: AbyssalThermalManager
            buffer = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<T>(safeCount);
            return true;
        }

        private bool EnsureParticleUploadStagingBuffer(int count)
        {
            int safeCount = Mathf.Max(1, count);
            int stride = UnsafeUtility.SizeOf<AshParticleData>();
            if (_particleUploadStagingBuffer != null &&
                _particleUploadStagingBuffer.count == safeCount &&
                _particleUploadStagingBuffer.stride == stride)
            {
                return true;
            }

            ReleaseBuffer(ref _particleUploadStagingBuffer);
            _particleUploadStagingBuffer = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<AshParticleData>(safeCount);
            return _particleUploadStagingBuffer != null;
        }

        private static bool IsBufferReady<T>(GraphicsBuffer buffer, int count) where T : struct
        {
            int safeCount = Mathf.Max(1, count);
            return buffer != null &&
                   buffer.count == safeCount &&
                   buffer.stride == UnsafeUtility.SizeOf<T>();
        }

        private bool TryResolveBlackSmokeKernel()
        {
            _kernelIndex = -1;
            _threadGroupSizeX = 0;
            _dispatchGroupCount = 0;
            if (blackSmokeCompute == null || !_supportsComputeShadersCold)
                return false;

            int kernelIndex = -1;
            uint sizeX = 0u;
            uint sizeY = 0u;
            uint sizeZ = 0u;
            try
            {
                if (!blackSmokeCompute.HasKernel("CSMain"))
                    return false;

                kernelIndex = blackSmokeCompute.FindKernel("CSMain");
                if (kernelIndex < 0)
                    return false;

                if (!blackSmokeCompute.IsSupported(kernelIndex))
                    return false;

                blackSmokeCompute.GetKernelThreadGroupSizes(kernelIndex, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u)
                return false;

            if (sizeX > (uint)PortableMaxComputeThreadsPerGroup)
                return false;

            _kernelIndex = kernelIndex;
            _threadGroupSizeX = (int)sizeX;
            return true;
        }

        private static int ResolveDispatchGroups(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groups;
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _particleUploadStagingBuffer);
            ReleaseBufferRing(ref _ventBuffers);
            ClearThermalFenceState();
            MarkThermalGpuStateDirty();
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void ReleaseBufferRing(ref GraphicsBuffer[] buffers)
        {
            if (buffers == null)
                return;

            for (int i = 0; i < buffers.Length; i++)
                ReleaseBuffer(ref buffers[i]);

            buffers = null;
        }

        private void RebuildVentField()
        {
            ResolveDependencies();
            _activeVentCount = 0;
            _activeCableZoneCount = 0;
            _debugAbyssalContext = IsAbyssalThermalContext();
            _debugActiveVentCount = 0;
            _debugCableZoneCount = 0;

            if (!_debugAbyssalContext)
            {
                _hasSmokeData = false;
                MarkThermalGpuStateDirty();
                _debugEmpNestCount = 0;
                _debugEmpCharge01 = 0f;
                if (_empNests != null)
                {
                    for (int i = 0; i < _empNests.Length; i++)
                        _empNests[i] = default;
                }
                if (_cableReleasedStates != null)
                    System.Array.Clear(_cableReleasedStates, 0, _cableReleasedStates.Length);
                if (_cableReleaseProgress != null)
                    System.Array.Clear(_cableReleaseProgress, 0, _cableReleaseProgress.Length);
                if (_cableElasticReleaseTimers != null)
                    System.Array.Clear(_cableElasticReleaseTimers, 0, _cableElasticReleaseTimers.Length);
                if (_cableEmpChainDelayTimers != null)
                    System.Array.Clear(_cableEmpChainDelayTimers, 0, _cableEmpChainDelayTimers.Length);
                if (_cableEmpChainGlowTimers != null)
                    System.Array.Clear(_cableEmpChainGlowTimers, 0, _cableEmpChainGlowTimers.Length);
                ClearHazardSources();
                return;
            }

            _zoneAnchorCount = WorldZoneAnchor.CopyActiveAnchorsTo(_zoneAnchors, MaxAnchorScanCapacity);
            if (!TryResolveAupFromRuntimeOrigin(playerTransform != null ? playerTransform.position : transform.position, out AbsoluteUniversePosition playerAup))
                return;

            for (int i = 0; i < _zoneAnchorCount && _activeVentCount < maxActiveVentCount; i++)
            {
                WorldZoneAnchor anchor = _zoneAnchors[i];
                if (!IsThermalAnchor(anchor))
                    continue;

                float holdWeight = Mathf.Max(anchor.EvaluateHoldWeight(in playerAup), anchor.EvaluateActivationWeight(in playerAup));
                if (holdWeight <= 0.01f)
                    continue;

                int spawnCount = Mathf.Clamp(Mathf.RoundToInt(LerpClamped(1f, maxVentsPerAnchor, holdWeight)), 1, maxVentsPerAnchor);
                for (int ventIndex = 0; ventIndex < spawnCount && _activeVentCount < maxActiveVentCount; ventIndex++)
                {
                    RegisterVent(anchor, ventIndex, holdWeight);
                }

                _activeCableZoneCount++;
            }

            AppendRuntimeVentRegistrations();

            _debugActiveVentCount = _activeVentCount;
            _debugCableZoneCount = _activeCableZoneCount;
            RebuildEmpNestField();
            _hasSmokeData = _activeVentCount > 0 && blackSmokeCompute != null && blackSmokeMaterial != null;

            UpdateSmokeBounds();
            UpdateHazardSources();
            QueueSmokeTopologyVisualSync();
        }

        private void QueueSmokeTopologyVisualSync()
        {
            _forceVentBufferUpload = true;
            if (RequiresParticleReset())
                _forceParticleReset = true;

            _smokeVisualSyncRequested = true;
        }

        private void RegisterVent(WorldZoneAnchor anchor, int ventIndex, float anchorWeight)
        {
            Vector3 anchorPosition = anchor.transform.position;
            float anchorRadius = Mathf.Max(12f, anchor.ActivationRadius * ventAnchorRadiusFraction);
            uint hashIndex = (uint)(_activeVentCount + 1);
            float radial01 = HashToFloat01(hashIndex, (uint)(ventIndex + 1), 0x68E31DA4u);
            float angle01 = HashToFloat01(hashIndex, (uint)(ventIndex + 1), 0xB5297A4Du);
            float angle = angle01 * Mathf.PI * 2f;
            float radialDistance = LerpClamped(anchorRadius * 0.15f, anchorRadius, radial01);
            Vector3 ventOffset = new Vector3(CinematicMath.FastCos(angle), 0f, CinematicMath.FastSin(angle)) * radialDistance;
            Vector3 ventPosition = anchorPosition + ventOffset;
            float radius = LerpClamped(ventRadiusMin, ventRadiusMax, HashToFloat01(hashIndex, (uint)(ventIndex + 5), 0x1B56C4E9u));
            float updraft = ventUpdraftVelocity * LerpClamped(0.85f, 1.2f, anchorWeight);
            float heat = ventHeatIntensity * LerpClamped(0.85f, 1.15f, anchorWeight);
            float smokeDensity = LerpClamped(0.55f, 1.25f, HashToFloat01(hashIndex, (uint)(ventIndex + 9), 0x94D049BBu));
            float cableRadius = Mathf.Max(radius * 1.4f, anchor.ActivationRadius * cableRadiusMultiplier);

            _ventStates[_activeVentCount] = new ThermalVentState
            {
                PositionWS = ventPosition,
                CableAnchorWS = anchorPosition,
                RadiusWS = radius,
                HeightWS = ventHeight,
                UpdraftVelocity = updraft,
                HeatIntensity = heat,
                SmokeDensity = smokeDensity,
                CableRadiusWS = cableRadius,
                HazardSourceId = BuildHazardSourceId(_activeVentCount)
            };

            _activeVentCount++;
        }

        private void AppendRuntimeVentRegistrations()
        {
            if (_runtimeVentRegistrationCount <= 0)
                return;

            for (int i = 0; i < _runtimeVentRegistrationCount && _activeVentCount < maxActiveVentCount; i++)
            {
                RuntimeVentRegistration registration = _runtimeVentRegistrations[i];
                _ventStates[_activeVentCount] = new ThermalVentState
                {
                    PositionWS = registration.PositionWS,
                    CableAnchorWS = registration.CableAnchorWS,
                    RadiusWS = registration.RadiusWS,
                    HeightWS = registration.HeightWS,
                    UpdraftVelocity = registration.UpdraftVelocity,
                    HeatIntensity = registration.HeatIntensity,
                    SmokeDensity = registration.SmokeDensity,
                    CableRadiusWS = registration.CableRadiusWS,
                    HazardSourceId = BuildHazardSourceId(_activeVentCount)
                };

                _activeVentCount++;
            }
        }

        private void RebuildEmpNestField()
        {
            _debugEmpNestCount = 0;
            _debugEmpCharge01 = 0f;
            if (_empNests == null)
                return;

            if (_activeVentCount <= 0)
            {
                for (int i = 0; i < _empNests.Length; i++)
                    _empNests[i] = default;
                return;
            }

            int empCount = 0;
            for (int i = 0; i < _activeVentCount && empCount < maxEmpNestCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float empRadius = Mathf.Max(1.25f, vent.CableRadiusWS * empNestRadiusMultiplier);
                if (empRadius <= 1.25f)
                    continue;

                float carriedCharge = 0f;
                float carriedCooldown = 0f;
                float carriedPulsePhase = HashToFloat01((uint)_instanceId, (uint)(i + 1), 0xA54FF53Au);
                for (int previousIndex = 0; previousIndex < _empNests.Length; previousIndex++)
                {
                    EmpNestState previousNest = _empNests[previousIndex];
                    if (previousNest.SourceVentIndex != i || previousNest.RadiusWS <= 0.0001f)
                        continue;

                    carriedCharge = previousNest.Charge01;
                    carriedCooldown = previousNest.Cooldown;
                    carriedPulsePhase = previousNest.PulsePhase;
                    break;
                }

                _empNests[empCount] = new EmpNestState
                {
                    PositionWS = vent.CableAnchorWS + Vector3.up * 0.75f,
                    RadiusWS = empRadius,
                    Charge01 = carriedCharge,
                    Cooldown = carriedCooldown,
                    PulsePhase = carriedPulsePhase,
                    SourceVentIndex = i
                };
                empCount++;
            }

            for (int i = empCount; i < _empNests.Length; i++)
                _empNests[i] = default;

            _debugEmpNestCount = empCount;
        }

        private void UpdateHazardSources()
        {
            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (i < _activeVentCount)
                {
                    ThermalVentState vent = _ventStates[i];
                    float eruptiveHeatScale = ResolveVentHeatScale(i);
                    float hazardRadius = Mathf.Max(vent.RadiusWS, vent.RadiusWS * ventHeatRadiusMultiplier);
                    HectonHazardManager.Unregister(vent.HazardSourceId);
                    RegisterThermalSpatialEvent(vent.PositionWS, hazardRadius, vent.HeatIntensity * eruptiveHeatScale, unchecked((uint)vent.HazardSourceId));
                }
                else
                {
                    HectonHazardManager.Unregister(BuildHazardSourceId(i));
                }
            }
        }

        private void RegisterThermalSpatialEvent(Vector3 positionWS, float radiusWS, float heatIntensity, uint sourceId = 0u)
        {
            if (!MathGuard.IsFinite(positionWS) ||
                !math.isfinite(radiusWS) ||
                radiusWS <= 0f ||
                !math.isfinite(heatIntensity) ||
                heatIntensity <= 0f)
                return;

            PublishThermalSourceSignal(positionWS, radiusWS, heatIntensity, sourceId);

            WorldSpatialHashGrid.RegisterTransientEvent(
                positionWS,
                radiusWS,
                Mathf.Clamp01(heatIntensity / Mathf.Max(1f, ventHeatIntensity * seismicEruptionHeatMultiplier)),
                ThermalSpatialEventLifetimeSeconds,
                SpatialTransientEventType.ThermalGradient,
                SpatialInteractionFlags.ThermalReceiver,
                FieldTargetRole.Generic,
                0,
                heatIntensity * ventHeatToCelsiusScale);

            VoxelDeltaProcessor deltaProcessor = _voxelDeltaProcessor;
            if (deltaProcessor != null &&
                TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition meltAup))
            {
                double3 absoluteUniversePosition = meltAup.ToAbsoluteDouble3();
                deltaProcessor.AcceptThermalMeltEvent(new ThermalMeltEvent
                {
                    AbsoluteUniversePosition = Vector3.zero,
                    AbsoluteUniversePositionDouble = absoluteUniversePosition,
                    RadiusMeters = Mathf.Max(1f, radiusWS * 0.35f),
                    Heat01 = Mathf.Clamp01(heatIntensity / Mathf.Max(1f, ventHeatIntensity * seismicEruptionHeatMultiplier))
                });
            }
        }

        private static void PublishThermalSourceSignal(Vector3 positionWS, float radiusWS, float heatIntensity, uint sourceId)
        {
            if (!MathGuard.IsFinite(positionWS) ||
                !math.isfinite(radiusWS) ||
                radiusWS <= 0f ||
                !math.isfinite(heatIntensity) ||
                heatIntensity <= 0f)
            {
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition positionAup))
                return;

            ThermalSourceSignal signal = default;
            signal.PositionAup = positionAup;
            signal.RadiusMeters = radiusWS;
            signal.IntensityCelsiusPerSecond = heatIntensity;
            signal.SourceId = sourceId != 0u ? sourceId : BuildTransientThermalSourceId(positionWS, radiusWS);
            signal.Frame = unchecked((uint)math.max(0, HectonArenaAllocator.CurrentFrameSequence));
            SignalBus<ThermalSourceSignal>.TryPushTracked(in signal, ref s_x001AbyssalThermalManagerSignalPushDropCount);
        }

        private static uint BuildTransientThermalSourceId(Vector3 positionWS, float radiusWS)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            float3 quantized = math.round(new float3(positionWS.x, positionWS.y, positionWS.z) * 0.25f);
            float qx = quantized.x == 0f ? 0.0f : quantized.x;
            float qy = quantized.y == 0f ? 0.0f : quantized.y;
            float qz = quantized.z == 0f ? 0.0f : quantized.z;
            float rWS = radiusWS == 0f ? 0.0f : radiusWS;
            hash = FoldThermalSourceHash(hash, math.asuint(qx), fnvPrime);
            hash = FoldThermalSourceHash(hash, math.asuint(qy), fnvPrime);
            hash = FoldThermalSourceHash(hash, math.asuint(qz), fnvPrime);
            hash = FoldThermalSourceHash(hash, math.asuint(rWS), fnvPrime);
            return hash == 0u ? 1u : hash;
        }

        private static uint FoldThermalSourceHash(uint hash, uint value, uint prime)
        {
            hash ^= value;
            hash *= prime;
            return hash;
        }

        private void ClearHazardSources()
        {
            for (int i = 0; i < MaxVentCapacity; i++)
                HectonHazardManager.Unregister(BuildHazardSourceId(i));
        }

        private void BuildVentGpuUploadData()
        {
            int bubbleCommandCount = 0;
            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (i < _activeVentCount)
                {
                    ThermalVentState vent = _ventStates[i];
                    float eruptionBlend = ResolveVentEruptionBlend(i);
                    float eruptiveUpdraftScale = ResolveVentUpdraftScale(i);
                    float eruptiveHeatScale = ResolveVentHeatScale(i);
                    float eruptiveSmokeScale = ResolveVentSmokeScale(i);
                    float eruptiveHeightScale = ResolveVentHeightScale(i);
                    _ventGpuData[i] = new ThermalVentGpuData
                    {
                        PositionWS = vent.PositionWS,
                        RadiusWS = vent.RadiusWS,
                        HeightWS = vent.HeightWS * eruptiveHeightScale,
                        UpdraftVelocity = vent.UpdraftVelocity * eruptiveUpdraftScale,
                        HeatIntensity = vent.HeatIntensity * eruptiveHeatScale,
                        SmokeDensity = vent.SmokeDensity * eruptiveSmokeScale,
                        Padding = Vector2.zero
                    };

                    if (_thermalBubbleCommands != null && eruptionBlend > 0.35f && bubbleCommandCount < _thermalBubbleCommands.Length)
                    {
                        _thermalBubbleCommands[bubbleCommandCount++] = new Vector4(
                            vent.PositionWS.x,
                            vent.PositionWS.y,
                            vent.PositionWS.z,
                            eruptionBlend);
                    }
                }
                else
                {
                    _ventGpuData[i] = default;
                }
            }

            PublishThermalBubbleCommands(bubbleCommandCount);
        }

        private void PublishThermalBubbleCommands(int bubbleCommandCount)
        {
            _pendingThermalBubbleCommandCount = Mathf.Clamp(bubbleCommandCount, 0, MaxVentCapacity);
            _thermalBubbleCommandsDirty = true;
        }

        private void FlushThermalBubbleCommands()
        {
            if (!_thermalBubbleCommandsDirty)
                return;

            int safeCount = _pendingThermalBubbleCommandCount;
            Shader.SetGlobalInt(_ThermalBubbleCommandCountId, safeCount);
            if (safeCount <= 0 || _thermalBubbleCommands == null)
            {
                _thermalBubbleCommandsDirty = false;
                return;
            }

            Shader.SetGlobalVectorArray(_ThermalBubbleCommandDataId, _thermalBubbleCommands);
            _thermalBubbleCommandsDirty = false;
        }

        private bool RequiresVentBufferUpload()
        {
            if (_forceVentBufferUpload || _lastUploadedVentGpuData == null)
                return true;

            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (!Matches(_ventGpuData[i], _lastUploadedVentGpuData[i]))
                    return true;
            }

            return false;
        }

        private bool RequiresParticleReset()
        {
            if (_forceParticleReset || _lastSeededVentStates == null)
                return true;

            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (!MatchesSeedTopology(_ventStates[i], _lastSeededVentStates[i]))
                    return true;
            }

            return false;
        }

        private void CacheUploadedVentData()
        {
            if (_lastUploadedVentGpuData == null)
                return;

            for (int i = 0; i < MaxVentCapacity; i++)
                _lastUploadedVentGpuData[i] = _ventGpuData[i];
        }

        private void CacheSeededVentTopology()
        {
            if (_lastSeededVentStates == null)
                return;

            for (int i = 0; i < MaxVentCapacity; i++)
                _lastSeededVentStates[i] = _ventStates[i];
        }

        private void MarkThermalGpuStateDirty()
        {
            _forceVentBufferUpload = true;
            _forceParticleReset = true;
            _activeVentBufferIndex = 0;
            _nextVentBufferUploadIndex = 1;
        }

        private void ClearThermalFenceState()
        {
            _smokeDispatchFenceArmed = false;
            _smokeDispatchFence = default;
            if (_ventBufferFences != null)
            {
                for (int i = 0; i < _ventBufferFences.Length; i++)
                    _ventBufferFences[i] = default;
            }

            if (_ventBufferFenceArmed == null)
                return;

            for (int i = 0; i < _ventBufferFenceArmed.Length; i++)
                _ventBufferFenceArmed[i] = false;
        }

        private GraphicsBuffer ResolveActiveVentBuffer()
        {
            if (_ventBuffers == null || _ventBuffers.Length == 0)
                return null;

            return _ventBuffers[_activeVentBufferIndex];
        }

        private void CaptureInFlightFences(int ventBufferIndex)
        {
            if (!_supportsGraphicsFence)
                return;

            _smokeDispatchFence = UnityEngine.Graphics.CreateAsyncGraphicsFence();
            _smokeDispatchFenceArmed = true;

            if (_ventBufferFences == null ||
                _ventBufferFenceArmed == null ||
                ventBufferIndex < 0 ||
                ventBufferIndex >= _ventBufferFences.Length ||
                ventBufferIndex >= _ventBufferFenceArmed.Length)
            {
                return;
            }

            _ventBufferFences[ventBufferIndex] = _smokeDispatchFence;
            _ventBufferFenceArmed[ventBufferIndex] = true;
        }

        private bool CanRewriteParticleBuffers()
        {
            if (!_supportsGraphicsFence || !_smokeDispatchFenceArmed)
                return true;

            if (!_smokeDispatchFence.passed)
                return false;

            _smokeDispatchFenceArmed = false;
            return true;
        }

        private bool TryResolveReusableVentUploadBuffer(out GraphicsBuffer uploadBuffer, out int uploadBufferIndex)
        {
            uploadBuffer = null;
            uploadBufferIndex = -1;
            if (_ventBuffers == null || _ventBuffers.Length == 0)
                return false;

            int startIndex = Mathf.Clamp(_nextVentBufferUploadIndex, 0, _ventBuffers.Length - 1);
            for (int offset = 0; offset < _ventBuffers.Length; offset++)
            {
                int candidateIndex = (startIndex + offset) % _ventBuffers.Length;
                if (candidateIndex == _activeVentBufferIndex)
                    continue;

                if (!IsVentBufferSlotReusable(candidateIndex))
                    continue;

                uploadBuffer = _ventBuffers[candidateIndex];
                uploadBufferIndex = candidateIndex;
                return uploadBuffer != null;
            }

            return false;
        }

        private bool IsVentBufferSlotReusable(int slotIndex)
        {
            if (_ventBuffers == null || slotIndex < 0 || slotIndex >= _ventBuffers.Length)
                return false;

            if (!_supportsGraphicsFence ||
                _ventBufferFences == null ||
                _ventBufferFenceArmed == null ||
                slotIndex >= _ventBufferFences.Length ||
                slotIndex >= _ventBufferFenceArmed.Length ||
                !_ventBufferFenceArmed[slotIndex])
            {
                return true;
            }

            if (!_ventBufferFences[slotIndex].passed)
                return false;

            _ventBufferFenceArmed[slotIndex] = false;
            return true;
        }

        private static bool Matches(ThermalVentGpuData left, ThermalVentGpuData right)
        {
            return Approximately(left.PositionWS, right.PositionWS) &&
                   Approximately(left.RadiusWS, right.RadiusWS) &&
                   Approximately(left.HeightWS, right.HeightWS) &&
                   Approximately(left.UpdraftVelocity, right.UpdraftVelocity) &&
                   Approximately(left.HeatIntensity, right.HeatIntensity) &&
                   Approximately(left.SmokeDensity, right.SmokeDensity);
        }

        private static bool MatchesSeedTopology(ThermalVentState left, ThermalVentState right)
        {
            return Approximately(left.PositionWS, right.PositionWS) &&
                   Approximately(left.RadiusWS, right.RadiusWS) &&
                   Approximately(left.UpdraftVelocity, right.UpdraftVelocity) &&
                   Approximately(left.CableAnchorWS, right.CableAnchorWS);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= VentStateCompareEpsilon * VentStateCompareEpsilon;
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= VentStateCompareEpsilon;
        }

        private void UploadVentBuffer()
        {
            if (!HasVentUploadStorageReady())
                return;

            BuildVentGpuUploadData();
            if (!_hasSmokeData || _activeVentCount <= 0 || !RequiresVentBufferUpload())
                return;

            if (!TryResolveReusableVentUploadBuffer(out GraphicsBuffer uploadBuffer, out int uploadBufferIndex))
                return;

            GraphicsBufferUploadUtility.UploadArray(uploadBuffer, _ventGpuData, MaxVentCapacity);
            CacheUploadedVentData();
            _activeVentBufferIndex = uploadBufferIndex;
            _nextVentBufferUploadIndex = (_activeVentBufferIndex + 1) % VentBufferRingSize;
            _forceVentBufferUpload = false;
        }

        private void ResetParticles()
        {
            if (!HasParticleResetStorageReady())
                return;

            if (!CanRewriteParticleBuffers())
                return;

            if (_activeVentCount <= 0)
            {
                System.Array.Clear(_initialParticles, 0, _initialParticles.Length);
                GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_particleUploadStagingBuffer, _particleBufferA, _initialParticles, smokeParticleCount);
                GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_particleUploadStagingBuffer, _particleBufferB, _initialParticles, smokeParticleCount);
                CacheSeededVentTopology();
                _forceParticleReset = false;
                return;
            }

            for (int i = 0; i < smokeParticleCount; i++)
            {
                int ventIndex = i % _activeVentCount;
                ThermalVentState vent = _ventStates[ventIndex];
                float seed = HashToFloat01((uint)i, (uint)ventIndex, 0xA24BAEDCu);
                float angle = HashToFloat01((uint)i, (uint)ventIndex, 0xE7037ED1u) * Mathf.PI * 2f;
                float radiusT = HashToFloat01((uint)i, (uint)ventIndex, 0x8EBC6AF1u);
                float radialDistance = vent.RadiusWS * 0.45f * radiusT;
                Vector3 offset = new Vector3(CinematicMath.FastCos(angle), 0f, CinematicMath.FastSin(angle)) * radialDistance;
                Vector3 position = vent.PositionWS + offset + Vector3.up * LerpClamped(0.2f, 1.6f, HashToFloat01((uint)i, (uint)ventIndex, 0x589965CDu));
                Vector3 velocity = Vector3.up * (vent.UpdraftVelocity * LerpClamped(0.65f, 1.05f, seed));
                float size = LerpClamped(smokeParticleSizeMin, smokeParticleSizeMax, HashToFloat01((uint)i, (uint)ventIndex, 0x1D8E4E27u));
                float maxLifetime = LerpClamped(1.8f, 5.6f, HashToFloat01((uint)i, (uint)ventIndex, 0xA4093822u));

                _initialParticles[i] = new AshParticleData
                {
                    PositionWS = position,
                    Size = size,
                    VelocityWS = velocity,
                    Alpha = 0.18f,
                    Lifetime = maxLifetime * seed,
                    MaxLifetime = maxLifetime,
                    Seed = seed,
                    VentIndex = ventIndex
                };
            }

            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_particleUploadStagingBuffer, _particleBufferA, _initialParticles, smokeParticleCount);
            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_particleUploadStagingBuffer, _particleBufferB, _initialParticles, smokeParticleCount);
            CacheSeededVentTopology();
            _forceParticleReset = false;
            _frameParity = 0;
        }

        private bool TryBindSmokeUniforms(float dt)
        {
            GraphicsBuffer readBuffer = _frameParity == 0 ? _particleBufferA : _particleBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _particleBufferB : _particleBufferA;
            GraphicsBuffer activeVentBuffer = ResolveActiveVentBuffer();
            if (readBuffer == null || writeBuffer == null || activeVentBuffer == null || _kernelIndex < 0)
                return false;

            Camera activeCamera = viewCamera;
            Vector3 cameraPosition = activeCamera != null ? activeCamera.transform.position : Vector3.zero;
            Vector3 cameraRight = activeCamera != null ? activeCamera.transform.right : Vector3.right;
            Vector3 cameraUp = activeCamera != null ? activeCamera.transform.up : Vector3.up;

            blackSmokeCompute.SetBuffer(_kernelIndex, _ParticlesReadId, readBuffer);
            blackSmokeCompute.SetBuffer(_kernelIndex, _ParticlesWriteId, writeBuffer);
            blackSmokeCompute.SetBuffer(_kernelIndex, _ThermalVentsId, activeVentBuffer);
            blackSmokeCompute.SetInt(_ParticleCountId, smokeParticleCount);
            blackSmokeCompute.SetInt(_ActiveVentCountId, _activeVentCount);
            blackSmokeCompute.SetFloat(_DeltaTimeId, dt);
            blackSmokeCompute.SetFloat(_SimulationTimeId, _simulationTime);
            blackSmokeCompute.SetVector(_CameraPositionId, cameraPosition);
            blackSmokeCompute.SetVector(_ParticleSizeRangeId, new Vector4(smokeParticleSizeMin, smokeParticleSizeMax, 0f, 0f));
            blackSmokeCompute.SetVector(_NoiseParamsId, new Vector4(smokeLateralDrift, smokeUpdraftJitter, smokeNoiseWeight, 0f));
            blackSmokeCompute.SetFloat(_MaxViewDistanceId, smokeMaxViewDistance);

            _materialPropertyBlock.Clear();
            _materialPropertyBlock.SetBuffer(_AshParticlesId, writeBuffer);
            _materialPropertyBlock.SetVector(_CameraPositionId, cameraPosition);
            _materialPropertyBlock.SetVector(_CameraRightId, cameraRight);
            _materialPropertyBlock.SetVector(_CameraUpId, cameraUp);
            _materialPropertyBlock.SetFloat(_MaxViewDistanceId, smokeMaxViewDistance);
            _materialPropertyBlock.SetColor(_AshTintId, smokeTint);
            _materialPropertyBlock.SetColor(_AshHotTintId, smokeHotTint);
            _materialPropertyBlock.SetFloat(_SoftnessId, smokeSoftness);
            return true;
        }

        private void FlushSmokeVisualSync(float dt)
        {
            if (_forceVentBufferUpload)
                UploadVentBuffer();

            if (_forceParticleReset)
                ResetParticles();

            if (!_hasSmokeData || blackSmokeCompute == null || blackSmokeMaterial == null || _activeVentCount <= 0)
                return;

            if (_kernelIndex < 0 || _dispatchGroupCount <= 0)
                return;

            if (!TryBindSmokeUniforms(dt))
                return;

            blackSmokeCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);
            _frameParity ^= 1;

            if (IsSmokeVisible())
                RenderSmoke();

            CaptureInFlightFences(_activeVentBufferIndex);
        }

        private void QueueSmokeVisualSync(float dt)
        {
            float safeDt = math.max(0f, dt);
            _pendingSmokeVisualDeltaTime = _smokeVisualSyncRequested
                ? math.max(_pendingSmokeVisualDeltaTime, safeDt)
                : safeDt;
            _smokeVisualSyncRequested = true;
        }

        private bool IsSmokeVisible()
        {
            if (viewCamera == null)
                return true;

            GeometryUtility.CalculateFrustumPlanes(viewCamera, _frustumPlanes);
            return GeometryUtility.TestPlanesAABB(_frustumPlanes, _smokeBounds);
        }

        private void RenderSmoke()
        {
            RenderParams renderParams = new RenderParams(blackSmokeMaterial)
            {
                worldBounds = _smokeBounds,
                matProps = _materialPropertyBlock,
                shadowCastingMode = smokeShadowCastingMode,
                receiveShadows = false,
                layer = gameObject.layer,
                lightProbeUsage = LightProbeUsage.Off
            };
            UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, smokeParticleCount);
        }

        private void UpdateSmokeBounds()
        {
            if (_activeVentCount <= 0)
            {
                _smokeBounds = new Bounds(transform.position, Vector3.one * 4f);
                _debugSmokeBounds = _smokeBounds;
                return;
            }

            Vector3 min = _ventStates[0].PositionWS;
            Vector3 max = _ventStates[0].PositionWS + Vector3.up * _ventStates[0].HeightWS;
            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float eruptiveHeightScale = ResolveVentHeightScale(i);
                Vector3 extents = new Vector3(vent.RadiusWS * 1.6f, vent.HeightWS * eruptiveHeightScale, vent.RadiusWS * 1.6f);
                Vector3 ventMin = vent.PositionWS - extents;
                Vector3 ventMax = vent.PositionWS + extents;
                min = Vector3.Min(min, ventMin);
                max = Vector3.Max(max, ventMax);
            }

            _smokeBounds.SetMinMax(min, max);
            _debugSmokeBounds = _smokeBounds;
        }

        private void UpdateCableCutting(float dt)
        {
            if (_cableCutStampCooldown > 0f)
            {
                _cableCutStampCooldown -= dt;
                if (_cableCutStampCooldown < 0f)
                    _cableCutStampCooldown = 0f;
            }

            _debugCutterSeveringCable = false;
            if (!_cutterBeamActive || cutManager == null)
                return;

            Transform cutterTransform = _activeCutterTransform != null ? _activeCutterTransform : playerTransform;
            if (cutterTransform == null)
                return;

            if (_cableCutStampCooldown > 0f)
                return;

            Vector3 positionWS = cutterTransform.position;
            if (!TryResolveCableZone(positionWS, out Vector3 cableAnchorWS, out float cableTension, out float cableCutProgress))
                return;

            if (cableTension <= 0.0001f)
                return;

            Vector3 forward = cutterTransform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            Vector3 safeForward = ResolveSafeDirection(forward, Vector3.forward);
            Vector3 stampPosition = positionWS + safeForward * cableCutForwardOffset;
            cutManager.RegisterExternalCut(stampPosition, cableCutStampRadius, cableCutStampStrength, forward, 0.18f);
            _cableCutStampCooldown = cableCutStampInterval;
            _debugCutterSeveringCable = true;

            if (_fluidDecalManager != null && cableCutProgress >= cableCutReleaseThreshold && _cableFluidDecalCooldown <= 0f)
            {
                float decalScale = Mathf.Clamp01(cableTension * LerpClamped(0.65f, 1.15f, cableCutProgress));
                _fluidDecalManager.RegisterCableFluid(cableAnchorWS, decalScale);
                _cableFluidDecalCooldown = 1.2f;
            }
        }

        /// <summary>
        /// Receives deferred laser cutter beam-state events used for abyssal cable cutting.
        /// </summary>
        /// <param name="payload">Blittable cutter event payload.</param>
        public void OnLaserCutterEvent(in global::Hecton8.Core.Contracts.Signals.LaserCutterEventPayload payload)
        {
            if ((global::Hecton8.Core.Contracts.Signals.LaserCutterEventType)payload.EventType != global::Hecton8.Core.Contracts.Signals.LaserCutterEventType.BeamStateChanged)
                return;

            bool isActive = LaserCutterEvents.IsBeamActive(in payload);
            Transform cutterTransform = null;
            if (isActive)
                LaserCutterEvents.TryResolveCutterTransform(payload.CutterInstanceId, out cutterTransform);

            HandleCutterBeamStateChanged(cutterTransform, isActive);
        }

        private void HandleCutterBeamStateChanged(Transform cutterTransform, bool isActive)
        {
            _activeCutterTransform = isActive ? cutterTransform : null;
            _cutterBeamActive = isActive;
            if (!isActive)
                _debugCutterSeveringCable = false;
        }

        private void UpdateEmpNests(float dt)
        {
            if (_empNests == null || _debugEmpNestCount <= 0 || playerTransform == null)
                return;

            bool transportActive = _playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive();
            Vector3 playerPosition = playerTransform.position;
            float highestCharge = 0f;

            for (int i = 0; i < _debugEmpNestCount; i++)
            {
                EmpNestState nest = _empNests[i];
                if (nest.RadiusWS <= 0.0001f)
                    continue;

                if (nest.Cooldown > 0f)
                {
                    nest.Cooldown -= dt;
                    if (nest.Cooldown < 0f)
                        nest.Cooldown = 0f;
                }

                double distanceSq = ComputeAupDistanceSq(playerPosition, nest.PositionWS);
                float radius = math.max(0.001f, nest.RadiusWS);
                double radiusSq = radius * radius;
                bool canCharge = transportActive && distanceSq <= radiusSq;
                if (canCharge && nest.Cooldown <= 0f)
                {
                    float chargeRate = empChargeDuration > 0.0001f ? 1f / empChargeDuration : 1f;
                    float chargeWeight = 1f - Mathf.Clamp01((float)(distanceSq / radiusSq));
                    nest.Charge01 = Mathf.Clamp01(nest.Charge01 + chargeRate * chargeWeight * dt);
                }
                else
                {
                    float dischargeRate = empDischargeDuration > 0.0001f ? 1f / empDischargeDuration : 1f;
                    nest.Charge01 = Mathf.Clamp01(nest.Charge01 - dischargeRate * dt);
                }

                if (nest.Charge01 >= 1f && nest.Cooldown <= 0f)
                {
                    FireEmpNest(ref nest, playerPosition);
                }

                highestCharge = Mathf.Max(highestCharge, nest.Charge01);
                _empNests[i] = nest;
            }

            _debugEmpCharge01 = highestCharge;
        }

        private void UpdateEmpChainReaction(float dt)
        {
            if (_cableEmpChainDelayTimers == null || _cableEmpChainGlowTimers == null)
                return;

            int activeCount = Mathf.Min(_activeVentCount, Mathf.Min(_cableEmpChainDelayTimers.Length, _cableEmpChainGlowTimers.Length));
            for (int i = 0; i < activeCount; i++)
            {
                if (_cableEmpChainDelayTimers[i] > 0f)
                {
                    _cableEmpChainDelayTimers[i] -= dt;
                    if (_cableEmpChainDelayTimers[i] < 0f)
                        _cableEmpChainDelayTimers[i] = 0f;

                    if (_cableEmpChainDelayTimers[i] <= 0f)
                        _cableEmpChainGlowTimers[i] = Mathf.Max(_cableEmpChainGlowTimers[i], empChainGlowDuration);
                }

                if (_cableEmpChainDelayTimers[i] <= 0f && _cableEmpChainGlowTimers[i] > 0f)
                {
                    _cableEmpChainGlowTimers[i] -= dt;
                    if (_cableEmpChainGlowTimers[i] < 0f)
                        _cableEmpChainGlowTimers[i] = 0f;
                }
            }

            for (int i = activeCount; i < _cableEmpChainDelayTimers.Length; i++)
            {
                _cableEmpChainDelayTimers[i] = 0f;
                if (i < _cableEmpChainGlowTimers.Length)
                    _cableEmpChainGlowTimers[i] = 0f;
            }
        }

        private void FireEmpNest(ref EmpNestState nest, Vector3 playerPosition)
        {
            if (_playerTransportCoordinator == null ||
                !_playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner transportLifecycleOwner) ||
                !(transportLifecycleOwner is MantaScooter mantaScooter))
            {
                nest.Charge01 = 0f;
                nest.Cooldown = empCooldown;
                return;
            }

            Vector3 toPlayer = playerPosition - nest.PositionWS;
            float castDistance = math.max(empSphereCastRadius, empPulseRange);
            if (castDistance <= 0.0001f)
                castDistance = empPulseRange;
            float castDistanceSq = castDistance * castDistance;

            float3 toPlayer3 = new float3(toPlayer.x, toPlayer.y, toPlayer.z);
            bool hitPlayerTransport = math.all(math.isfinite(toPlayer3)) &&
                                      math.lengthsq(toPlayer3) <= castDistanceSq;

            if (hitPlayerTransport)
            {
                mantaScooter.ApplyEmpDisruption(empMisfireDuration);
                _pdaCorrosionPresentationSink?.RequestExternalPdaCorrosion(1f, empPdaCorrosionDuration);
            }

            TriggerEmpChainReaction(nest.SourceVentIndex, nest.PositionWS);
            nest.Charge01 = 0f;
            nest.Cooldown = empCooldown;
        }

        private void TriggerEmpChainReaction(int sourceVentIndex, Vector3 sourcePositionWS)
        {
            if (_cableEmpChainDelayTimers == null || _cableEmpChainGlowTimers == null || sourceVentIndex < 0 || sourceVentIndex >= _activeVentCount)
                return;

            Vector3 sourceAnchor = _ventStates[sourceVentIndex].CableAnchorWS;
            int activeCount = Mathf.Min(_activeVentCount, Mathf.Min(_cableEmpChainDelayTimers.Length, _cableEmpChainGlowTimers.Length));
            for (int i = 0; i < activeCount; i++)
            {
                Vector3 targetAnchor = _ventStates[i].CableAnchorWS;
                int hopDistance = math.abs(i - sourceVentIndex);
                float hopDelay = math.max(
                    0.025f,
                    math.min(empChainGlowDuration / math.max(1, activeCount), 1f / math.max(empChainPropagationSpeed, 0.001f)));
                float delay = i == sourceVentIndex
                    ? 0f
                    : hopDistance * hopDelay;

                if (_cableEmpChainGlowTimers[i] > 0f || _cableEmpChainDelayTimers[i] > 0f)
                {
                    _cableEmpChainDelayTimers[i] = Mathf.Min(_cableEmpChainDelayTimers[i], delay);
                    _cableEmpChainGlowTimers[i] = Mathf.Max(_cableEmpChainGlowTimers[i], empChainGlowDuration);
                    continue;
                }

                _cableEmpChainDelayTimers[i] = delay;
                _cableEmpChainGlowTimers[i] = i == sourceVentIndex ? empChainGlowDuration : 0f;
            }

            if (_fluidDecalManager != null)
                _fluidDecalManager.RegisterCableFluid(sourcePositionWS, 0.42f);
        }

        private static bool IsFiniteVent(in ThermalVentState vent)
        {
            return MathGuard.IsFinite(vent.PositionWS) &&
                   MathGuard.IsFinite(vent.CableAnchorWS) &&
                   math.isfinite(vent.RadiusWS) &&
                   math.isfinite(vent.HeightWS) &&
                   math.isfinite(vent.UpdraftVelocity) &&
                   math.isfinite(vent.HeatIntensity) &&
                   math.isfinite(vent.SmokeDensity) &&
                   math.isfinite(vent.CableRadiusWS);
        }

        private static void SanitizeThermalFlowSample(ref ThermalFlowSample sample, Vector3 fallbackAnchor)
        {
            if (!MathGuard.IsFinite(sample.FlowVelocityWS))
                sample.FlowVelocityWS = Vector3.zero;

            sample.Heat01 = Resolve01Finite(sample.Heat01);
            sample.DragMultiplier = math.max(1f, ResolvePositiveFinite(sample.DragMultiplier, 1f, 1f));
            if (sample.HasFlow == 0 || sample.FlowVelocityWS.sqrMagnitude <= 0.00000001f)
                sample.HasFlow = 0;
            else
                sample.HasFlow = 1;

            if (!MathGuard.IsFinite(sample.CableAnchorWS))
                sample.CableAnchorWS = MathGuard.IsFinite(fallbackAnchor) ? fallbackAnchor : Vector3.zero;

            sample.CableCutProgress01 = Resolve01Finite(sample.CableCutProgress01);
            sample.CableEscapeSuppression01 = math.clamp(
                math.isfinite(sample.CableEscapeSuppression01) ? sample.CableEscapeSuppression01 : 1f,
                0f,
                1f);
            sample.CableTension01 = Resolve01Finite(sample.CableTension01);

            if (sample.IsCableZone == 0 || sample.CableTension01 <= 0.0001f)
            {
                sample.IsCableZone = 0;
                sample.CableTension01 = 0f;
                sample.CableEscapeSuppression01 = 1f;
            }
            else
            {
                sample.IsCableZone = 1;
            }
        }

        private static float Resolve01Finite(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveNonNegativeFinite(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0f, fallback) : 0f;
            return math.isfinite(value) ? math.max(0f, value) : safeFallback;
        }

        private static float ResolvePositiveFinite(float value, float fallback, float minimum)
        {
            float safeMinimum = math.isfinite(minimum) ? math.max(0.0001f, minimum) : 0.0001f;
            float safeFallback = math.isfinite(fallback) ? math.max(safeMinimum, fallback) : safeMinimum;
            return math.isfinite(value) ? math.max(safeMinimum, value) : safeFallback;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.all(math.isfinite(new double3(positionAup.LocalX, positionAup.LocalY, positionAup.LocalZ)));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!MathGuard.IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static double ComputeAupDistanceSq(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePositionA, out AbsoluteUniversePosition a) ||
                !TryResolveAupFromRuntimeOrigin(runtimePositionB, out AbsoluteUniversePosition b))
            {
                return double.MaxValue;
            }

            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private static float ComputeAupPlanarDistance(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePositionA, out AbsoluteUniversePosition a))
                return float.MaxValue;

            return ComputeAupPlanarDistance(in a, runtimePositionB);
        }

        private static float ComputeAupPlanarDistance(in AbsoluteUniversePosition originAup, Vector3 runtimePositionB)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePositionB, out AbsoluteUniversePosition targetAup))
                return float.MaxValue;

            double3 originAbsolute = originAup.ToAbsoluteDouble3();
            double3 targetAbsolute = targetAup.ToAbsoluteDouble3();
            double deltaX = targetAbsolute.x - originAbsolute.x;
            double deltaZ = targetAbsolute.z - originAbsolute.z;
            double ax = System.Math.Abs(deltaX);
            double az = System.Math.Abs(deltaZ);
            double max = System.Math.Max(ax, az);
            double min = System.Math.Min(ax, az);
            double estimate = max + (min * 0.4142135623730951d);
            return estimate > float.MaxValue ? float.MaxValue : (float)estimate;
        }

        private static AbsoluteUniversePosition ResolveAup(Vector3 runtimePosition)
        {
            return TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup)
                ? positionAup
                : default;
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + ((to - from) * math.saturate(t));
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float lengthSq = direction.sqrMagnitude;
            return lengthSq > 0.0001f ? direction * math.rsqrt(lengthSq) : fallback;
        }

        private void UpdateCableVisuals(float dt)
        {
            if (_bioCableVisuals == null)
                return;

            bool hasPlayer = playerTransform != null;
            bool transportActive = _playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive();
            Vector3 playerPosition = hasPlayer ? playerTransform.position : transform.position;
            if (!TryResolveAupFromRuntimeOrigin(playerPosition, out AbsoluteUniversePosition playerAup))
                return;

            Vector3 playerVelocity = CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityThermalMaxAgeFrames, out Vector3 kccVelocity)
                ? kccVelocity
                : Vector3.zero;

            float hullRadius = Mathf.Max(0.1f, cableVisualHullRadius);
            for (int i = 0; i < _bioCableVisuals.Length; i++)
            {
                BioCableIK cableRig = _bioCableVisuals[i];
                if (cableRig == null)
                    continue;

                if (_cableElasticReleaseTimers != null && i < _cableElasticReleaseTimers.Length && _cableElasticReleaseTimers[i] > 0f)
                {
                    _cableElasticReleaseTimers[i] -= dt;
                    if (_cableElasticReleaseTimers[i] < 0f)
                        _cableElasticReleaseTimers[i] = 0f;
                }

                if (i >= _activeVentCount || !hasPlayer)
                {
                    if (_cableReleasedStates != null && i < _cableReleasedStates.Length)
                        _cableReleasedStates[i] = false;
                    if (_cableReleaseProgress != null && i < _cableReleaseProgress.Length)
                        _cableReleaseProgress[i] = 0f;
                    if (_cableElasticReleaseTimers != null && i < _cableElasticReleaseTimers.Length)
                        _cableElasticReleaseTimers[i] = 0f;
                    cableRig.SetEmpCharge(0f, 0f);
                    cableRig.SetCableActive(false);
                    continue;
                }

                ThermalVentState vent = _ventStates[i];
                Vector3 cableAnchor = ResolveCableAnchor(playerPosition, vent.CableAnchorWS);
                float cableRadius = Mathf.Max(0.1f, vent.CableRadiusWS);
                float activationDistance = Mathf.Min(cableRadius, cableVisualActivationDistance);
                float planarDistance = ComputeAupPlanarDistance(in playerAup, cableAnchor);
                float chainCharge01 = ResolveEmpChainChargeForVent(i);
                float empCharge01 = Mathf.Max(ResolveEmpChargeForVent(i), chainCharge01);
                bool keepVisualAliveForEmp = empCharge01 > 0.0001f;
                if (planarDistance > activationDistance && !keepVisualAliveForEmp)
                {
                    cableRig.SetEmpCharge(0f, 0f);
                    if (_cableReleasedStates[i] && cableRig.HasTransientMotion)
                    {
                        cableRig.SetCableActive(true);
                        cableRig.TickReleased(cableAnchor, Vector3.up, dt);
                    }
                    else
                    {
                        _cableReleasedStates[i] = false;
                        _cableReleaseProgress[i] = 0f;
                        if (_cableElasticReleaseTimers != null)
                            _cableElasticReleaseTimers[i] = 0f;
                        cableRig.SetCableActive(false);
                    }
                    continue;
                }

                float tension01 = 1f - planarDistance / Mathf.Max(activationDistance, 0.001f);
                float cutProgress01 = ResolveCableCutProgress(playerPosition, cableAnchor, cableRadius);
                float activeTension = tension01 * (1f - cutProgress01);
                bool elasticReleased = _cableElasticReleaseTimers != null && _cableElasticReleaseTimers[i] > 0f;
                bool snapped = cutProgress01 >= cableCutReleaseThreshold;
                if (snapped && _cableReleaseProgress[i] < cableCutReleaseThreshold)
                {
                    _cableReleasedStates[i] = true;
                    Vector3 snapDirection = playerPosition - cableAnchor;
                    snapDirection.y = Mathf.Max(0f, snapDirection.y) + cableSnapVerticalLift;
                    if (snapDirection.sqrMagnitude <= 0.0001f)
                        snapDirection = Vector3.up;

                    float recoilSpeed = cableSnapRecoilSpeed * LerpClamped(0.65f, 1.25f, tension01);
                    cableRig.TriggerSnapRecoil(ResolveSafeDirection(snapDirection, Vector3.up) * recoilSpeed, cableSnapDuration);
                    if (_fluidDecalManager != null)
                        _fluidDecalManager.RegisterCableFluid(cableAnchor, Mathf.Clamp01(LerpClamped(0.75f, 1.2f, tension01)));
                }
                else if (!snapped && !elasticReleased && _cableReleasedStates[i] && cutProgress01 <= cableCutReleaseThreshold * 0.45f)
                {
                    _cableReleasedStates[i] = false;
                }

                _cableReleaseProgress[i] = cutProgress01;
                float empPulse01 = empCharge01 > 0f
                    ? CinematicMath.FastTriangleWave01(((_simulationTime * empPulseSpeed) + i * 0.6180339f) * 0.15915494309f)
                    : 0f;
                cableRig.SetEmpCharge(empCharge01, empPulse01);

                if (_cableReleasedStates[i])
                {
                    cableRig.SetCableActive(true);
                    cableRig.TickReleased(cableAnchor, Vector3.up, dt);
                    if (!cableRig.HasTransientMotion && snapped)
                        cableRig.SetCableActive(false);
                    continue;
                }

                if (planarDistance > activationDistance && keepVisualAliveForEmp)
                {
                    cableRig.SetCableActive(true);
                    cableRig.TickCable(cableAnchor, Vector3.up, cableAnchor + Vector3.up * Mathf.Max(0.4f, cableRadius * 0.18f), Vector3.zero, 0f, 0f, dt);
                    continue;
                }

                float attraction01 = transportActive
                    ? Mathf.Clamp01(activeTension * cableVisualTransportBoost)
                    : activeTension;
                float wrap01 = transportActive && activeTension > cableVisualWrapThreshold
                    ? Mathf.InverseLerp(cableVisualWrapThreshold, 1f, activeTension)
                    : 0f;

                Vector3 toPlayer = playerPosition - cableAnchor;
                Vector3 hullDirection = ResolveSafeDirection(toPlayer, Vector3.forward);
                Vector3 attractorPosition = playerPosition - hullDirection * hullRadius;
                cableRig.SetCableActive(true);
                cableRig.TickCable(cableAnchor, Vector3.up, attractorPosition, playerVelocity, attraction01, wrap01, dt);
                if (cableRig.ConsumeElasticRupture(out Vector3 ruptureVelocityWS))
                {
                    _cableReleasedStates[i] = true;
                    if (_cableElasticReleaseTimers != null)
                        _cableElasticReleaseTimers[i] = cableElasticReleaseDuration;

                    Vector3 elasticRecoilVelocity = ruptureVelocityWS.sqrMagnitude > 0.0001f
                        ? ruptureVelocityWS
                        : (hullDirection.sqrMagnitude > 0.0001f ? hullDirection : Vector3.up) * cableSnapRecoilSpeed;
                    cableRig.TriggerSnapRecoil(elasticRecoilVelocity, cableSnapDuration);
                    if (_fluidDecalManager != null)
                        _fluidDecalManager.RegisterCableFluid(cableAnchor, Mathf.Clamp01(LerpClamped(0.8f, 1.35f, tension01)));
                }
            }
        }

        private float ResolveEmpChargeForVent(int ventIndex)
        {
            if (_empNests == null || ventIndex < 0)
                return 0f;

            int nestCount = Mathf.Min(_debugEmpNestCount, _empNests.Length);
            for (int i = 0; i < nestCount; i++)
            {
                if (_empNests[i].SourceVentIndex != ventIndex)
                    continue;

                return _empNests[i].Charge01;
            }

            return 0f;
        }

        private float ResolveEmpChainChargeForVent(int ventIndex)
        {
            if (_cableEmpChainDelayTimers == null ||
                _cableEmpChainGlowTimers == null ||
                ventIndex < 0 ||
                ventIndex >= _cableEmpChainDelayTimers.Length ||
                ventIndex >= _cableEmpChainGlowTimers.Length)
            {
                return 0f;
            }

            if (_cableEmpChainDelayTimers[ventIndex] > 0f)
                return 0f;

            if (empChainGlowDuration <= 0.0001f)
                return _cableEmpChainGlowTimers[ventIndex] > 0f ? 1f : 0f;

            return Mathf.Clamp01(_cableEmpChainGlowTimers[ventIndex] / empChainGlowDuration);
        }

        private bool TryResolveCableZone(Vector3 positionWS, out Vector3 cableAnchorWS, out float cableTension01, out float cableCutProgress01)
        {
            cableAnchorWS = positionWS;
            cableTension01 = 0f;
            cableCutProgress01 = 0f;
            if (_activeVentCount <= 0 || !MathGuard.IsFinite(positionWS))
                return false;

            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                if (!IsFiniteVent(in vent))
                    continue;

                float cableRadius = ResolvePositiveFinite(vent.CableRadiusWS, 0.1f, 0.1f);
                float planarDistance = ComputeAupPlanarDistance(positionWS, vent.CableAnchorWS);
                if (!math.isfinite(planarDistance) || planarDistance > cableRadius)
                    continue;

                float tension = math.saturate(1f - planarDistance / math.max(cableRadius, 0.001f));
                if (tension <= cableTension01)
                    continue;

                cableTension01 = tension;
                cableAnchorWS = ResolveCableAnchor(positionWS, vent.CableAnchorWS);
                if (!MathGuard.IsFinite(cableAnchorWS))
                    cableAnchorWS = positionWS;
                cableCutProgress01 = Resolve01Finite(ResolveCableCutProgress(positionWS, cableAnchorWS, cableRadius));
            }

            return cableTension01 > 0f;
        }

        private Vector3 ResolveCableAnchor(Vector3 positionWS, Vector3 cableAnchorWS)
        {
            if (!MathGuard.IsFinite(cableAnchorWS))
                return MathGuard.IsFinite(positionWS) ? positionWS : Vector3.zero;
            if (!MathGuard.IsFinite(positionWS))
                return cableAnchorWS;

            Vector3 planarDelta = positionWS - cableAnchorWS;
            planarDelta.y = 0f;
            float planarDeltaSq = planarDelta.sqrMagnitude;
            if (!math.isfinite(planarDeltaSq) || planarDeltaSq <= 0.0001f || ResolveNonNegativeFinite(cableAnchorPull, 0f) <= 0f)
                return cableAnchorWS;

            return cableAnchorWS + ResolveSafeDirection(planarDelta, Vector3.forward) * ResolveNonNegativeFinite(cableAnchorPull, 0f);
        }

        private float ResolveCableCutProgress(Vector3 positionWS, Vector3 cableAnchorWS, float cableRadiusWS)
        {
            if (cutManager == null || !MathGuard.IsFinite(positionWS) || !MathGuard.IsFinite(cableAnchorWS))
                return 0f;

            float queryRadius = Mathf.Min(
                ResolvePositiveFinite(cableRadiusWS, 0.1f, 0.1f) * 0.35f,
                ResolvePositiveFinite(cableCutQueryRadius, 0.1f, 0.1f));
            if (!cutManager.SampleRecentCutArea(positionWS, queryRadius, out float accumulatedAreaWS, out float strongestCut01))
                return 0f;

            float releaseThreshold = math.max(0.0001f, Resolve01Finite(cableCutReleaseThreshold));
            float requiredArea = Mathf.PI * queryRadius * queryRadius * releaseThreshold;
            float safeAccumulatedArea = ResolveNonNegativeFinite(accumulatedAreaWS, 0f);
            float areaProgress = requiredArea > 0.0001f ? Mathf.Clamp01(safeAccumulatedArea / requiredArea) : 0f;
            float strengthProgress = Mathf.Clamp01(ResolveNonNegativeFinite(strongestCut01, 0f) / releaseThreshold);
            return Resolve01Finite(Mathf.Max(areaProgress, strengthProgress));
        }

        private bool IsAbyssalThermalContext()
        {
            if (!TryResolveAbyssalThermalFamily(out HectonBiomeFamilyProfile family))
                return false;

            return IsThermalBiomeFamily(family);
        }

        private bool TryResolveAbyssalThermalFamily(out HectonBiomeFamilyProfile family)
        {
            family = null;
            BiomeMatrixDirector matrixDirector = biomeMatrixDirector;
            if (!IsAbyssalThermalDepthGateSatisfied(matrixDirector))
                return false;

            WorldZoneDirector zoneDirector = worldZoneDirector;
            WorldZoneAnchor currentZone = zoneDirector != null && zoneDirector.isActiveAndEnabled
                ? zoneDirector.CurrentZone
                : null;

            HectonBiomeFamilyProfile matrixFamily = matrixDirector != null && matrixDirector.isActiveAndEnabled
                ? matrixDirector.CurrentFamilyProfile
                : null;
            family = currentZone != null && currentZone.DominantBiomeFamily != null
                ? currentZone.DominantBiomeFamily
                : matrixFamily;
            return family != null;
        }

        private bool IsAbyssalThermalDepthGateSatisfied(BiomeMatrixDirector matrixDirector)
        {
            if (TryResolvePlayerDepthMeters(out float playerDepthMeters) &&
                playerDepthMeters >= abyssalVentStartDepthMeters)
            {
                return true;
            }

            if (matrixDirector != null &&
                matrixDirector.isActiveAndEnabled &&
                math.isfinite(matrixDirector.CurrentDepthMeters) &&
                matrixDirector.CurrentDepthMeters >= abyssalVentStartDepthMeters)
            {
                return true;
            }

            return false;
        }

        private bool TryResolvePlayerDepthMeters(out float depthMeters)
        {
            depthMeters = 0f;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                depthMeters = math.max(0f, movementState.DepthMeters);
                return true;
            }

            if (playerContext != null)
                return false;

            HectonPlayerMovement movement = _playerMovement;
            if (movement != null && math.isfinite(movement.CurrentDepth))
            {
                depthMeters = math.max(0f, movement.CurrentDepth);
                return true;
            }

            return false;
        }

        private bool IsThermalAnchor(WorldZoneAnchor anchor)
        {
            if (anchor == null)
                return false;

            if (anchor.Kind != WorldZoneAnchor.ZoneKind.Service &&
                anchor.Kind != WorldZoneAnchor.ZoneKind.Power &&
                anchor.Kind != WorldZoneAnchor.ZoneKind.Construction)
                return false;

            HectonBiomeFamilyProfile family = anchor.DominantBiomeFamily != null
                ? anchor.DominantBiomeFamily
                : biomeMatrixDirector != null && biomeMatrixDirector.isActiveAndEnabled
                    ? biomeMatrixDirector.CurrentFamilyProfile
                    : null;
            return IsThermalBiomeFamily(family);
        }

        void IRandomEventListener.OnRandomEventStarted(RandomEventType type, float intensity)
        {
            HandleRandomEventStarted(type, intensity);
        }

        void IRandomEventListener.OnSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            HandleSeismicShockwave(in payload);
        }

        private void HandleRandomEventStarted(RandomEventType type, float intensity)
        {
            if (type != RandomEventType.ThermalEruption)
                return;

            TriggerSeismicEruption(Mathf.Clamp01(intensity), ventHeatIntensity * Mathf.Max(1f, intensity));
        }

        private void HandleSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            if (_runtimeVentRegistrationCount <= 0 && _activeVentCount <= 0)
                return;

            float strength = Mathf.Clamp01(payload.ImpulseMagnitude / 20f);
            TriggerSeismicEruption(strength, payload.ImpulseMagnitude);
        }

        private void TriggerSeismicEruption(float strength01, float impulseMagnitude)
        {
            if (_activeVentCount <= 0 && _runtimeVentRegistrationCount <= 0)
                return;

            _seismicEruptionTimer = Mathf.Max(_seismicEruptionTimer, seismicEruptionDuration + Mathf.Clamp01(impulseMagnitude / 20f) * 2f);
            _seismicEruptionStrength01 = Mathf.Max(_seismicEruptionStrength01, Mathf.Clamp01(strength01));
            _debugSeismicEruptionSeconds = _seismicEruptionTimer;
            MarkThermalGpuStateDirty();
            UpdateHazardSources();
            UpdateSmokeBounds();
        }

        private void ApplySeismicSignalEruptionScalar()
        {
            if (!SignalBus<SeismicSignal>.TryGetLatest(out SeismicSignal signal, out int sequence) ||
                sequence == _lastSeismicSignalSequence)
            {
                return;
            }

            _lastSeismicSignalSequence = sequence;
            float scalar = math.max(1f, signal.ThermalEruptionProbabilityScalar);
            if (scalar <= 1.001f || signal.Intensity01 <= 0.8f)
                return;

            TriggerSeismicEruption(math.saturate(signal.Intensity01), ventHeatIntensity * scalar);
        }

        private void UpdateSeismicEruption(float deltaTime)
        {
            if (_seismicEruptionTimer <= 0f)
            {
                _debugSeismicEruptionSeconds = 0f;
                _seismicEruptionStrength01 = 0f;
                return;
            }

            _seismicEruptionTimer = Mathf.Max(0f, _seismicEruptionTimer - deltaTime);
            _debugSeismicEruptionSeconds = _seismicEruptionTimer;
            if (_seismicEruptionTimer <= 0f)
            {
                _seismicEruptionStrength01 = 0f;
                MarkThermalGpuStateDirty();
                UpdateHazardSources();
                UpdateSmokeBounds();
            }
            else
            {
                _forceVentBufferUpload = true;
            }
        }

        private float ResolveSeismicEruptionBlend()
        {
            if (_seismicEruptionTimer <= 0f)
                return 0f;

            float normalizedTime = Mathf.Clamp01(_seismicEruptionTimer / Mathf.Max(0.25f, seismicEruptionDuration));
            return Mathf.Clamp01(_seismicEruptionStrength01 * Mathf.SmoothStep(0f, 1f, normalizedTime));
        }

        private float ResolveVentEruptionBlend(int ventIndex)
        {
            float seismicBlend = ResolveSeismicEruptionBlend();
            float cycleBlend = ResolveDeterministicVentCycle01(ventIndex);
            return math.saturate(math.max(seismicBlend, cycleBlend));
        }

        private float ResolveVentHeatScale(int ventIndex)
        {
            return LerpClamped(1f, seismicEruptionHeatMultiplier, ResolveVentEruptionBlend(ventIndex));
        }

        private float ResolveVentUpdraftScale(int ventIndex)
        {
            return LerpClamped(1f, seismicEruptionUpdraftMultiplier, ResolveVentEruptionBlend(ventIndex));
        }

        private float ResolveVentSmokeScale(int ventIndex)
        {
            return LerpClamped(1f, seismicEruptionSmokeMultiplier, ResolveVentEruptionBlend(ventIndex));
        }

        private float ResolveVentHeightScale(int ventIndex)
        {
            return LerpClamped(1f, seismicEruptionHeightMultiplier, ResolveVentEruptionBlend(ventIndex));
        }

        private float ResolveDeterministicVentCycle01(int ventIndex)
        {
            float safeCycleSeconds = math.max(2f, ventEruptionCycleSeconds);
            float phaseOffset = HashToFloat01((uint)_instanceId, (uint)(ventIndex + 1), 0x46A3F315u);
            float triangle = TriangleWave01((_simulationTime * math.rcp(safeCycleSeconds)) + phaseOffset);
            float duty = math.saturate(ventEruptionDuty01);
            float sleepThreshold = 1f - duty;
            float eruption = math.saturate((triangle - sleepThreshold) * math.rcp(math.max(0.001f, duty)));
            return eruption * eruption * (3f - (2f * eruption));
        }

        private static float TriangleWave01(float value)
        {
            float t = value - math.floor(value);
            return 1f - math.abs((t * 2f) - 1f);
        }

        private static bool IsThermalBiomeFamily(HectonBiomeFamilyProfile family)
        {
            if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                return false;

            return IsThermalBiomeFamilyId(family.familyId);
        }

        private int BuildHazardSourceId(int ventIndex)
        {
            return (_instanceId & 0x7FFF) * 64 + ventIndex + 1;
        }

        private void TryRegister()
        {
            if (!_registeredThermodynamicsRuntime && Application.isPlaying)
            {
                if (TryAbortForUsableExistingRuntime())
                    return;

                GlobalRegistry.RegisterThermodynamicsRuntime(this);
                _registeredThermodynamicsRuntime = ReferenceEquals(GlobalRegistry.Thermodynamics, this);
                if (_registeredThermodynamicsRuntime)
                    s_activeRuntimeInstance = this;
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredFixedTick)
            {
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            AbyssalThermalManager active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsAbyssalThermalRuntimeUsable(active))
            {
                Destroy(this);
                return true;
            }

            if (ReferenceEquals(GlobalRegistry.Thermodynamics, active))
                GlobalRegistry.UnregisterThermodynamicsRuntime(active);

            if (ReferenceEquals(s_activeRuntimeInstance, active))
                s_activeRuntimeInstance = null;

            return false;
        }

        private static bool IsAbyssalThermalRuntimeUsable(AbyssalThermalManager manager)
        {
            return manager != null && manager._registeredThermodynamicsRuntime && manager.isActiveAndEnabled;
        }

        private void TryUnregister()
        {
            if (_registeredThermodynamicsRuntime)
            {
                GlobalRegistry.UnregisterThermodynamicsRuntime(this);
                _registeredThermodynamicsRuntime = false;
            }

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ThermalMapJacobiJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> Previous;
            [ReadOnly, NoAlias] public NativeArray<float> Sources;
            [ReadOnly, NoAlias] public NativeArray<float> Insulation01;
            [NoAlias]
            public NativeArray<float> Next;
            public int StartIndex;
            public int Width;
            public int Height;
            public int Depth;
            public int AxisShift;
            public float AmbientCelsius;
            public float Diffusion01;

            public void Execute(int localIndex)
            {
                int index = StartIndex + localIndex;
                int x = index & (Width - 1);
                int yz = index >> AxisShift;
                int z = yz & (Depth - 1);
                int y = yz >> AxisShift;
                int plane = Width * Depth;
                int left = (y * plane) + (z * Width) + math.max(0, x - 1);
                int right = (y * plane) + (z * Width) + math.min(Width - 1, x + 1);
                int back = (y * plane) + (math.max(0, z - 1) * Width) + x;
                int forward = (y * plane) + (math.min(Depth - 1, z + 1) * Width) + x;
                int down = (math.max(0, y - 1) * plane) + (z * Width) + x;
                int up = (math.min(Height - 1, y + 1) * plane) + (z * Width) + x;
                float neighborAverage = (Previous[left] + Previous[right] + Previous[back] + Previous[forward] + Previous[down] + Previous[up]) * 0.16666667f;
                float insulation = math.saturate(Insulation01[index]);
                float effectiveDiffusion = math.saturate(Diffusion01) * (1f - insulation);
                float blurred = math.lerp(Previous[index], neighborAverage, effectiveDiffusion);
                float heated = math.max(blurred, Sources[index]);
                float floor = math.min(AmbientCelsius, Sources[index]);
                Next[index] = math.isfinite(heated) ? math.max(floor, heated) : floor;
            }
        }

        private static float HashToFloat01(uint a, uint b, uint salt)
        {
            uint state = a * 747796405u + b * 2891336453u + ThermalHashSeed + salt;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;
            return (state & 0x00FFFFFFu) / 16777215f;
        }

#if UNITY_EDITOR
        private const string EditorDefaultBioCableMaterialPath = "Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantStem.mat";
        private const string EditorDefaultFluidDecalMaterialPath = "Assets/_Project/Art/Materials/VFX/MAT_AbyssalFluidDecal.mat";
        private const string EditorDefaultBlackSmokeComputePath = "Assets/_Project/Art/Shaders/AbyssalBlackSmoke.compute";

        private void OnValidate()
        {
            SanitizeSettings();

            bool resolvedAny = false;

            if (bioCableMaterial == null)
            {
                bioCableMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultBioCableMaterialPath);
                resolvedAny |= bioCableMaterial != null;
            }

            if (fluidDecalMaterial == null)
            {
                fluidDecalMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultFluidDecalMaterialPath);
                resolvedAny |= fluidDecalMaterial != null;
            }

            // The black smoker plume never ran. blackSmokeCompute arrived only through [SerializeField], and a
            // GUID census found AbyssalBlackSmoke.compute referenced by no scene, prefab or asset, so the field
            // was null everywhere and the guards early-returned. Identity is proven here by asset path rather
            // than by kernel name: CSMain is declared by seven computes in this project and is therefore no
            // evidence at all. The distinctive contract is the _ThermalVents / _ParticlesRead buffer pair, which
            // only this manager binds.
            if (blackSmokeCompute == null)
            {
                blackSmokeCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(EditorDefaultBlackSmokeComputePath);
                resolvedAny |= blackSmokeCompute != null;
            }

            if (fluidDecalManager == null)
                TryGetComponent(out fluidDecalManager);

            // SetDirty is what makes any of these resolves survive. Without it the assignments exist in memory
            // only, so the editor repairs them on every load while the serialized value stays null - and null is
            // what ships. That is the failure mode 30deb1fe9 documents, where the editor always looks correct and
            // the player build is dead. It was already true of both material fields above; one dirty call covers
            // all three, and it makes a new scene carrying this component wire itself.
            if (resolvedAny)
                UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
