using System.Collections.Generic;
using Hecton8.Audio;
using Hecton.Localization;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Caves;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Physics;
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

        private struct ThermalVentGpuData
        {
            public Vector3 PositionWS;
            public float RadiusWS;
            public float HeightWS;
            public float UpdraftVelocity;
            public float HeatIntensity;
            public float SmokeDensity;
            public Vector2 Padding;
        }

        private struct AshParticleData
        {
            public Vector3 PositionWS;
            public float Size;
            public Vector3 VelocityWS;
            public float Alpha;
            public float Lifetime;
            public float MaxLifetime;
            public float Seed;
            public float VentIndex;
        }

        private struct ThermalTelemetryEntry
        {
            public float3 PositionWS;
            public float TemperatureCelsius;
            public float Heat01;
            public uint Flags;
            public int Frame;
            public int ActiveVentCount;
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
        private const int ThermalMapAxisShift = 5;
        private const int ThermalGridSaveRleCapacity = ThermalMapCellCount;
        private const int ThermalTelemetryCapacity = 300;
        private const int VentBufferRingSize = 3;
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
        private const float SubmarineColdSpeedMultiplier = 0.7f;
        private const float BoilingDamageThresholdCelsius = 80f;
        private const float FaunaThermalAvoidanceThresholdCelsius = 50f;
        private const uint ThermalTelemetryFlagHeatSource = 1u << 0;
        private const uint ThermalTelemetryFlagPlayerAmbientTemp = 1u << 1;
        private const uint ThermalTelemetryFlagThermalShock = 1u << 2;
        private const byte ThermalShockAcousticChannel = 11;
        private const string NativeMemoryOwner = nameof(AbyssalThermalManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
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
        [Tooltip("Shared authored material assigned to manager-created BioCableIK line renderers. Runtime material creation is forbidden.")]
        private Material bioCableMaterial;

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

        // COLD ALLOC: List<WorldZoneAnchor>[32] - reusable runtime cartographer anchor scratch list for abyssal vent selection - owner: AbyssalThermalManager
        private readonly List<WorldZoneAnchor> _zoneAnchors = new List<WorldZoneAnchor>(MaxAnchorScanCapacity);
        // COLD ALLOC: List<RuntimeVentRegistration>[16] - bounded runtime hydrothermal vent registry injected by geology bridge - owner: AbyssalThermalManager
        private readonly List<RuntimeVentRegistration> _runtimeVentRegistrations = new List<RuntimeVentRegistration>(MaxVentCapacity);
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
        private GraphicsBuffer[] _ventBuffers;
        private GraphicsFence[] _ventBufferFences;
        private MaterialPropertyBlock _materialPropertyBlock;
        private BioCableIK[] _bioCableVisuals;
        private Bounds _smokeBounds;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredThermodynamicsRuntime;
        private bool _cutterBeamActive;
        private bool _hasSmokeData;
        private int _kernelIndex = -1;
        private int _threadGroupSizeX = 64;
        private int _dispatchGroupCount = 1;
        private int _frameParity;
        private int _activeVentBufferIndex;
        private int _nextVentBufferUploadIndex = 1;
        private int _activeVentCount;
        private int _activeCableZoneCount;
        private int _instanceId;
        private float _simulationTime;
        private float _cableCutStampCooldown;
        private float _cableFluidDecalCooldown;
        private Transform _activeCutterTransform;
        private Rigidbody _playerRigidbody;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
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
        private bool _smokeDispatchFenceArmed;
        private GraphicsFence _smokeDispatchFence;
        private float _seismicEruptionTimer;
        private float _seismicEruptionStrength01;
        private int _lastSeismicSignalSequence;
        private NativeArray<ThermalCrystallizationSample> _crystallizationSamples;
        private NativeArray<ThermalCrystallizationResult> _crystallizationResults;
        private JobHandle _crystallizationJobHandle;
        private bool _crystallizationJobActive;
        private int _pendingCrystallizationSampleCount;
        private int _scheduledCrystallizationSampleCount;
        private float[] _ventCrystallizationCooldowns;
        private NativeArray<float> _thermalMapReadCelsius;
        private NativeArray<float> _thermalMapWriteCelsius;
        private NativeArray<float> _thermalMapSourceCelsius;
        private NativeArray<float> _thermalMapInsulation01;
        private NativeArray<float> _thermalMapVisualCelsius;
        private NativeArray<SaveBinaryStorage.ThermalGridRleRun> _thermalGridRleRuns;
        private NativeArray<ThermalTelemetryEntry> _thermalTelemetryRing;
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
        private uint _thermalGridRleChecksum;
        private uint _lastProcessedAupShiftFrameId;
        private int _lastPersistentThermalVentRevision = int.MinValue;
        private float _thermalColdTickAccumulator;
        private float _thermalMapCellSizeMeters = DefaultThermalMapWorldSizeMeters * math.rcp(ThermalMapResolution);
        private Vector3 _thermalMapOriginWS;
        private float _lastLocalThermalHeat01 = -1f;
        private float _lastLocalThermalTemperatureCelsius = float.NaN;
        private float _previousPlayerTemperatureCelsius = DefaultAmbientWaterTemperatureCelsius;
        private float _previousSubmarineTemperatureCelsius = DefaultAmbientWaterTemperatureCelsius;
        private float _thermalCondensation01;
        private float _lastPublishedThermalCondensation01 = -1f;
        private float _thermalRoarCooldown;
        private float _thermalEruptionGpuRefreshTimer;
        private Vector4[] _thermalBubbleCommands;

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

        internal bool TryResolveApexMigrationThermalAttractor(out Vector3 attractorPosition, out float strength01)
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

                AbsoluteUniversePosition ventAup = AbsoluteUniversePosition.FromRuntimePosition(vent.PositionWS);
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
            if (runtimeKey == 0L)
                return;

            RuntimeVentRegistration registration = new RuntimeVentRegistration
            {
                RuntimeKey = runtimeKey,
                PositionWS = positionWS,
                CableAnchorWS = positionWS,
                RadiusWS = Mathf.Max(2f, radiusWS),
                HeightWS = Mathf.Max(4f, heightWS),
                UpdraftVelocity = Mathf.Max(0.5f, updraftVelocity),
                HeatIntensity = Mathf.Max(0.5f, heatIntensity),
                SmokeDensity = Mathf.Max(0.1f, smokeDensity),
                CableRadiusWS = Mathf.Max(2f, cableRadiusWS)
            };

            for (int i = 0; i < _runtimeVentRegistrations.Count; i++)
            {
                if (_runtimeVentRegistrations[i].RuntimeKey != runtimeKey)
                    continue;

                _runtimeVentRegistrations[i] = registration;
                RebuildVentField();
                return;
            }

            if (_runtimeVentRegistrations.Count >= MaxVentCapacity)
                return;

            _runtimeVentRegistrations.Add(registration);
            RebuildVentField();
        }

        public void UnregisterRuntimeVent(long runtimeKey)
        {
            if (runtimeKey == 0L || _runtimeVentRegistrations.Count <= 0)
                return;

            for (int i = 0; i < _runtimeVentRegistrations.Count; i++)
            {
                if (_runtimeVentRegistrations[i].RuntimeKey != runtimeKey)
                    continue;

                _runtimeVentRegistrations.RemoveAt(i);
                RebuildVentField();
                return;
            }
        }

        private void SyncPersistentThermalVents()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
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

            _runtimeVentRegistrations.Clear();
            int count = math.min(registry.ActiveThermalVentCount, MaxVentCapacity);
            for (int i = 0; i < count; i++)
            {
                if (!registry.TryGetActiveThermalVent(i, out PersistentThermalVentRecord record))
                    continue;

                Vector3 positionWS = ResolvePersistentThermalVentRuntimePosition(in record.PositionAup);
                _runtimeVentRegistrations.Add(new RuntimeVentRegistration
                {
                    RuntimeKey = record.RuntimeKey,
                    PositionWS = positionWS,
                    CableAnchorWS = positionWS,
                    RadiusWS = Mathf.Max(2f, record.RadiusWS),
                    HeightWS = Mathf.Max(4f, record.HeightWS),
                    UpdraftVelocity = Mathf.Max(0.5f, record.UpdraftVelocity),
                    HeatIntensity = Mathf.Max(0.5f, record.HeatIntensity),
                    SmokeDensity = Mathf.Max(0.1f, record.SmokeDensity),
                    CableRadiusWS = Mathf.Max(2f, record.CableRadiusWS)
                });
            }

            _lastPersistentThermalVentRevision = revision;
        }

        private static Vector3 ResolvePersistentThermalVentRuntimePosition(in AbsoluteUniversePosition positionAup)
        {
            double3 absolute = positionAup.ToAbsoluteDouble3();
            Vector3 absoluteVector = new Vector3(
                (float)absolute.x,
                (float)absolute.y,
                (float)absolute.z);
            return HectonFloatingOrigin.ToRuntimePosition(absoluteVector);
        }

        private void Awake()
        {
            AbyssalThermalManager registeredThermodynamics = GlobalRegistry.Thermodynamics;
            if (registeredThermodynamics != null && registeredThermodynamics != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[AbyssalThermalManager] Duplicate instance detected. Destroying the newer component.", this);
#endif
                Destroy(this);
                return;
            }

            _instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            _supportsGraphicsFence = SystemInfo.supportsGraphicsFence;
            SanitizeSettings();
            CacheRegistryServicesCold();
            ResolveDependencies();
            EnsureStorage();
            EnsureCableVisuals();
            EnsureBuffers();
            ClearHazardSources();
            RebuildVentField();
        }

        private void OnEnable()
        {
            LaserCutterEvents.Register(this);
            RandomEventEvents.Register(this);
            HectonFloatingOrigin.RegisterListener(this);
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveDependencies();
            EnsureStorage();
            EnsureCableVisuals();
            EnsureBuffers();
            RebuildVentField();
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
            _thermalMapDiffusionSlicesCompleted = 0;
            _thermalMapDiffusionSliceCursor = 0;
            ClearHazardSources();
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
            ClearHazardSources();
            ReleaseBuffers();
            DisposeCrystallizationBuffers();
            DisposeThermalMapBuffers();
            DisposeThermalTelemetry();

        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            _lastProcessedAupShiftFrameId = shiftData.Sequence;
            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            for (int i = 0; i < _runtimeVentRegistrations.Count; i++)
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

            if (!_crystallizationJobActive && _crystallizationSamples.IsCreated)
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
            ResolveDependencies();
            UploadThermalMapTextureIfDirty();
            float deltaTime = Mathf.Max(0f, dt);
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
            UpdateCableVisuals(deltaTime);
            UpdateEmpNests(deltaTime);

            if (!_hasSmokeData || blackSmokeCompute == null || blackSmokeMaterial == null || _activeVentCount <= 0)
                return;

            if (_forceVentBufferUpload)
                UploadVentBuffer();

            if (_forceParticleReset)
                ResetParticles();

            BindSmokeUniforms(deltaTime);
            blackSmokeCompute.Dispatch(_kernelIndex, _dispatchGroupCount, 1, 1);
            _frameParity ^= 1;

            if (IsSmokeVisible())
                RenderSmoke();

            CaptureInFlightFences(_activeVentBufferIndex);
        }

        /// <summary>
        /// Rebuilds local vent and cable metadata from the current abyssal cartographer context.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            ApplySeismicSignalEruptionScalar();
            AdvancePassiveCrystallizationCooldowns(0.5f);
            SyncPersistentThermalVents();
            RebuildVentField();
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
            if (!_crystallizationSamples.IsCreated || _pendingCrystallizationSampleCount >= MaxCrystallizationSampleCapacity)
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
        }

        /// <summary>
        /// Applies boiling gameplay effects for player and submarine without broad overlap queries.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            float fdt = Mathf.Max(0f, fixedDeltaTime);
            if (fdt <= 0f)
                return;

            ResolveDependencies();
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : _playerRigidbody;
            GameObject playerObject = playerContext != null ? playerContext.PlayerObject : playerTransform != null ? playerTransform.gameObject : null;
            Vector3 playerPosition = playerContext != null && playerContext.PlayerTransform != null
                ? playerContext.PlayerTransform.position
                : playerTransform != null ? playerTransform.position : transform.position;
            float playerTemperature = ResolveAmbientTemperatureCelsius(playerPosition);

            if (ProcessThermalGameplayTarget(playerBody, playerObject, playerPosition, fdt, publishPresentation: true, out playerTemperature))
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
                ProcessThermalGameplayTarget(submarineBody, submarineBody.gameObject, submarinePosition, fdt, publishPresentation: false, out submarineTemperature);
                if (_previousSubmarineTemperatureCelsius >= ThermalShockHotThresholdCelsius &&
                    submarineTemperature <= ThermalShockColdThresholdCelsius)
                {
                    EmitThermalShock(
                        submarinePosition,
                        submarineTemperature - _previousSubmarineTemperatureCelsius,
                        ResolveNearestVentSourceId(submarinePosition),
                        submarineBody.gameObject,
                        (byte)(TemperatureChangedSignal.FlagThermalShock | TemperatureChangedSignal.FlagSubmarineAmbient));
                }

                _previousSubmarineTemperatureCelsius = submarineTemperature;
                submarine.SetThermalSpeedMultiplier(submarineTemperature < ThermalShockColdThresholdCelsius ? SubmarineColdSpeedMultiplier : 1f);
            }

            if (_thermalRoarCooldown > 0f)
                _thermalRoarCooldown = Mathf.Max(0f, _thermalRoarCooldown - fdt);
        }

        public bool TrySampleTemperatureCelsius(Vector3 positionWS, out float temperatureCelsius)
        {
            return TrySampleTemperatureCelsius(positionWS, out temperatureCelsius, out _);
        }

        public bool TryGetThermalMapReadback(
            out NativeArray<float> temperatureCelsius,
            out int width,
            out int height,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version)
        {
            temperatureCelsius = _thermalMapVisualCelsius;
            width = ThermalMapResolution;
            height = ThermalMapResolution;
            originWS = _thermalMapOriginWS;
            cellSizeMeters = _thermalMapCellSizeMeters;
            version = _thermalMapVersion;
            return UsesThermalGrid() && _thermalMapVisualCelsius.IsCreated;
        }

        public bool TryGetThermalGridReadback(
            out NativeArray<float> temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version)
        {
            temperatureCelsius = _thermalMapReadCelsius;
            width = ThermalGridResolution;
            height = ThermalGridResolution;
            depth = ThermalGridResolution;
            originWS = _thermalMapOriginWS;
            cellSizeMeters = _thermalMapCellSizeMeters;
            version = _thermalMapVersion;
            return UsesThermalGrid() && _thermalMapReadCelsius.IsCreated;
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

            if (_activeVentCount <= 0)
                return false;

            float effectiveRadius = Mathf.Max(0.1f, radiusWS);
            float strongestCable = 0f;
            Vector3 strongestCableAnchor = positionWS;
            float strongestCableCut = 0f;

            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float eruptiveHeatScale = ResolveVentHeatScale(i);
                float eruptiveUpdraftScale = ResolveVentUpdraftScale(i);
                float ventRadius = Mathf.Max(0.1f, vent.RadiusWS + effectiveRadius);
                Vector2 planarDelta = new Vector2(positionWS.x - vent.PositionWS.x, positionWS.z - vent.PositionWS.z);
                float planarDistance = ComputeAupPlanarDistance(positionWS, vent.PositionWS);
                if (planarDistance <= ventRadius)
                {
                    float radialFalloff = 1f - planarDistance / Mathf.Max(ventRadius, 0.001f);
                    float baseVentY = vent.PositionWS.y;
                    float heightGate = 1f - Mathf.Clamp01((positionWS.y - baseVentY) / Mathf.Max(vent.HeightWS, 0.001f));
                    if (heightGate > 0f)
                    {
                        float ventWeight = radialFalloff * heightGate;
                        Vector3 swirlDirection = planarDistance > 0.0001f
                            ? new Vector3(-planarDelta.y / planarDistance, 0f, planarDelta.x / planarDistance)
                            : Vector3.zero;
                        sample.HasFlow = 1;
                        sample.Heat01 = Mathf.Max(sample.Heat01, vent.HeatIntensity * eruptiveHeatScale * ventWeight);
                        sample.DragMultiplier = Mathf.Max(sample.DragMultiplier, LerpClamped(1f, ventDragMultiplier, ventWeight));
                        sample.FlowVelocityWS += Vector3.up * (vent.UpdraftVelocity * eruptiveUpdraftScale * ventWeight);
                        sample.FlowVelocityWS += swirlDirection * (vent.UpdraftVelocity * 0.12f * ventWeight);
                    }
                }

                float cableRadius = Mathf.Max(0.1f, vent.CableRadiusWS + effectiveRadius);
                Vector2 cableDelta = new Vector2(positionWS.x - vent.CableAnchorWS.x, positionWS.z - vent.CableAnchorWS.z);
                float cableDistance = ComputeAupPlanarDistance(positionWS, vent.CableAnchorWS);
                if (cableDistance > cableRadius)
                    continue;

                float cableWeight = 1f - cableDistance / Mathf.Max(cableRadius, 0.001f);
                if (cableWeight <= strongestCable)
                    continue;

                strongestCable = cableWeight;
                strongestCableAnchor = ResolveCableAnchor(positionWS, vent.CableAnchorWS);
                strongestCableCut = ResolveCableCutProgress(positionWS, strongestCableAnchor, cableRadius);
            }

            if (strongestCable > 0f)
            {
                sample.IsCableZone = 1;
                sample.CableAnchorWS = strongestCableAnchor;
                sample.CableCutProgress01 = strongestCableCut;
                sample.CableEscapeSuppression01 = 1f - strongestCableCut;
                sample.CableTension01 = strongestCable * sample.CableEscapeSuppression01;
            }

            _debugCableCutProgress01 = strongestCableCut;
            return sample.HasFlow != 0 || sample.IsCableZone != 0;
        }

        private bool ProcessThermalGameplayTarget(
            Rigidbody body,
            GameObject targetObject,
            Vector3 positionWS,
            float fixedDeltaTime,
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

            if (body != null && heat01 > 0f)
            {
                float invMass = math.rcp(math.max(1f, body.mass));
                float velocityChange = thermalConvectionVelocityPerSecond * heat01 * invMass * fixedDeltaTime;
                if (math.isfinite(velocityChange) && velocityChange > 0f)
                    PhysicsForceRouter.QueueForce(body, Vector3.up * velocityChange, ForceMode.VelocityChange);
            }

            if (targetObject != null && boilingDamagePerSecond > 0f)
                QueueBoilingDamage(targetObject, positionWS, temperatureCelsius, heat01, fixedDeltaTime, sourceId);

            return true;
        }

        private void QueueBoilingDamage(
            GameObject targetObject,
            Vector3 positionWS,
            float temperatureCelsius,
            float heat01,
            float fixedDeltaTime,
            int sourceId)
        {
            int targetId = CombatDamageRuntime.ResolveTargetId(targetObject);
            if (targetId == 0)
                return;

            float amount = boilingDamagePerSecond * math.saturate(heat01) * math.max(0f, fixedDeltaTime);
            if (!(amount > 0f) || !math.isfinite(amount))
                return;

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
                LocalPoint = new float3(positionWS.x, positionWS.y, positionWS.z),
                ArmorNormal = new float3(0f, 1f, 0f),
                LocalTemperatureCelsius = temperatureCelsius,
                StatusDurationSeconds = math.max(0.25f, heat01 * 2f)
            };

            CombatDamageRuntime.TryQueueDamage(in signal, in detail);
        }

        private bool TrySampleTemperatureCelsius(Vector3 positionWS, out float temperatureCelsius, out int sourceId)
        {
            sourceId = 0;
            temperatureCelsius = ResolveAmbientTemperatureCelsius(positionWS);
            if (_activeVentCount <= 0)
                return temperatureCelsius < ambientWaterTemperatureCelsius - 0.001f;

            if (UsesThermalGrid() &&
                _thermalMapReadCelsius.IsCreated &&
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
            if (!_thermalMapReadCelsius.IsCreated || _thermalMapCellSizeMeters <= 0.0001f)
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
            temperatureCelsius = _thermalMapReadCelsius[ToThermalGridIndex(x, y, z)];
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
            TemperatureChangedSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(positionWS);
            signal.TemperatureCelsius = temperatureCelsius;
            signal.DeltaCelsius = math.isfinite(deltaCelsius) ? deltaCelsius : 0f;
            signal.SourceId = sourceId <= 0 ? (ushort)0 : (ushort)math.min(sourceId, ushort.MaxValue);
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Flags = flags;
            GlobalSignals.Publish(in signal);
        }

        private void EmitThermalShock(Vector3 positionWS, float deltaCelsius, int sourceId, GameObject targetObject, byte temperatureFlags)
        {
            uint targetHash = targetObject != null ? unchecked((uint)EntityId.ToULong(targetObject.GetEntityId())) : 0u;
            Hecton8.Core.Contracts.Signals.CombatDamageSignal damage = default;
            damage.ImpactAup = Hecton8.Core.Contracts.Signals.CombatDamageSignalCodec.FromRuntimePoint(positionWS);
            damage.Direction = new float3(0f, 1f, 0f);
            damage.Magnitude = ThermalShockDamageMagnitude;
            damage.DamageType = CombatDamageTypes.Thermal;
            damage.TargetHash = targetHash;
            damage.SourceHash = sourceId <= 0 ? (uint)_instanceId : (uint)sourceId;
            damage.Frame = unchecked((uint)Time.frameCount);
            damage.SourceId = sourceId <= 0 ? (ushort)0 : (ushort)math.min(sourceId, ushort.MaxValue);
            damage.TargetId = targetObject != null
                ? (ushort)math.min(CombatDamageRuntime.ResolveTargetId(targetObject), ushort.MaxValue)
                : (ushort)0;
            damage.Channel = ThermalShockAcousticChannel;
            damage.Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag;
            damage.IntegrityDelta = 1;
            GlobalSignals.Publish(in damage);

            AcousticPingSignal acoustic = default;
            acoustic.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(positionWS);
            acoustic.RadiusMeters = 140f;
            acoustic.Intensity01 = math.saturate(math.abs(deltaCelsius) / 140f);
            acoustic.SourceId = damage.SourceHash;
            acoustic.Channel = ThermalShockAcousticChannel;
            acoustic.Flags = 1;
            GlobalSignals.Publish(in acoustic);

            PublishTemperatureChangedSignal(
                positionWS,
                ThermalShockColdThresholdCelsius,
                deltaCelsius,
                sourceId,
                temperatureFlags);
            RecordThermalTelemetry(positionWS, ThermalShockColdThresholdCelsius, 0f, ThermalTelemetryFlagThermalShock);
        }

        private void PublishLocalThermalPresentation(Vector3 positionWS, float temperatureCelsius, float heat01)
        {
            if (!math.isfinite(temperatureCelsius) || !math.isfinite(heat01))
            {
                DumpThermalBlackBox();
                return;
            }

            float clampedHeat = math.saturate(heat01);
            if (Mathf.Abs(clampedHeat - _lastLocalThermalHeat01) > 0.001f)
            {
                Shader.SetGlobalFloat(_LocalThermalHeatId, clampedHeat);
                _lastLocalThermalHeat01 = clampedHeat;
            }

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
        }

        private void UpdateThermalPresentationDecay(float deltaTime)
        {
            if (_thermalCondensation01 <= 0f)
                return;

            _thermalCondensation01 = math.max(0f, _thermalCondensation01 - (math.max(0f, deltaTime) * condensationDecayPerSecond));
            if (Mathf.Abs(_thermalCondensation01 - _lastPublishedThermalCondensation01) > 0.001f)
            {
                Shader.SetGlobalFloat(_ThermalCondensationId, _thermalCondensation01);
                _lastPublishedThermalCondensation01 = _thermalCondensation01;
            }
        }

        private void TryQueueThermalRoar(Vector3 positionWS, float heat01)
        {
            if (heat01 < 0.2f || _thermalRoarCooldown > 0f)
                return;

            float intensity = math.saturate(heat01);
            ProceduralAudioEvents.RaiseAudioPingTriggered(
                positionWS,
                intensity,
                0.65f,
                1f,
                Mathf.Lerp(180f, 520f, intensity),
                ProceduralAudioPingKind.MechanicalWhirr);

            ImpactSignal signal = default;
            signal.PointAup = AbsoluteUniversePosition.FromRuntimePosition(positionWS);
            signal.Force = intensity;
            signal.Intensity = intensity;
            signal.PrimaryBodyId = (uint)_instanceId;
            signal.WeightClass = 2;
            GlobalSignals.Publish(in signal);
            _thermalRoarCooldown = thermalRoarCooldownSeconds;
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
            if (!_thermalTelemetryRing.IsCreated)
                return;

            if (!math.isfinite(temperatureCelsius) || !math.isfinite(heat01) || !MathGuard.IsFinite(positionWS))
            {
                DumpThermalBlackBox();
                return;
            }

            int index = _thermalTelemetryIndex % ThermalTelemetryCapacity;
            _thermalTelemetryRing[index] = new ThermalTelemetryEntry
            {
                PositionWS = new float3(positionWS.x, positionWS.y, positionWS.z),
                TemperatureCelsius = temperatureCelsius,
                Heat01 = heat01,
                Flags = flags,
                Frame = Time.frameCount,
                ActiveVentCount = _activeVentCount
            };
            _thermalTelemetryIndex = (_thermalTelemetryIndex + 1) % ThermalTelemetryCapacity;
        }

        private void DumpThermalBlackBox()
        {
            if (_thermalTelemetryDumped || !_thermalTelemetryRing.IsCreated)
                return;

            _thermalTelemetryDumped = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    "Docs",
                    "AgentLogs",
                    "Dump_THERMODYNAMICS_LEAD.bin");
                using (System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(ThermalTelemetryCapacity);
                    writer.Write(_thermalTelemetryIndex);
                    for (int i = 0; i < _thermalTelemetryRing.Length; i++)
                    {
                        ThermalTelemetryEntry entry = _thermalTelemetryRing[i];
                        writer.Write(entry.PositionWS.x);
                        writer.Write(entry.PositionWS.y);
                        writer.Write(entry.PositionWS.z);
                        writer.Write(entry.TemperatureCelsius);
                        writer.Write(entry.Heat01);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Frame);
                        writer.Write(entry.ActiveVentCount);
                    }
                }
            }
            catch (System.Exception)
            {
            }
#endif
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
                !_crystallizationSamples.IsCreated)
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
                        IGasDynamicsSolver gasDynamics = GlobalRegistry.GasDynamics;
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
            if (!UsesThermalGrid())
            {
                _thermalMapDiffusionSlicesCompleted = 0;
                _thermalMapDiffusionSliceCursor = 0;
                PublishThermalMapMetadata(active: false);
                return;
            }

            EnsureThermalMapBuffers();
            if (_activeVentCount <= 0)
            {
                float idleAmbientCelsius = ResolveAmbientTemperatureCelsius(ResolveThermalMapCenter());
                if (_thermalMapReadCelsius.IsCreated)
                    FillThermalMap(_thermalMapReadCelsius, idleAmbientCelsius);
                if (_thermalMapWriteCelsius.IsCreated)
                    FillThermalMap(_thermalMapWriteCelsius, idleAmbientCelsius);
                if (_thermalMapSourceCelsius.IsCreated)
                    FillThermalMap(_thermalMapSourceCelsius, idleAmbientCelsius);
                if (_thermalMapInsulation01.IsCreated)
                    FillThermalMap(_thermalMapInsulation01, 0f);
                if (_thermalMapVisualCelsius.IsCreated)
                    FillThermalMap(_thermalMapVisualCelsius, idleAmbientCelsius);
                _thermalGridRleRunCount = 0;
                _thermalGridRleByteCount = 0;
                _thermalGridRleChecksum = 0u;
                _thermalMapDiffusionSlicesCompleted = 0;
                _thermalMapDiffusionSliceCursor = 0;
                _thermalMapVersion++;
                MarkThermalMapTextureDirty();
                PublishThermalMapMetadata(active: false);
                return;
            }

            if (_thermalMapJobActive)
                return;

            if (_thermalMapDiffusionSlicesCompleted <= 0)
            {
                _thermalColdTickAccumulator += Mathf.Max(0f, deltaSeconds);
                if (_thermalColdTickAccumulator < thermalMapColdTickSeconds)
                    return;

                _thermalColdTickAccumulator = 0f;
                RebuildThermalMapSources();
                _thermalMapDiffusionSliceCursor = ResolveThermalMapDiffusionSliceCursor();
            }

            int startIndex = _thermalMapDiffusionSliceCursor * ThermalMapDiffusionSliceCellCount;
            ThermalMapJacobiJob job = new ThermalMapJacobiJob
            {
                Previous = _thermalMapReadCelsius,
                Sources = _thermalMapSourceCelsius,
                Insulation01 = _thermalMapInsulation01,
                Next = _thermalMapWriteCelsius,
                StartIndex = startIndex,
                Width = ThermalMapResolution,
                Height = ThermalMapResolution,
                Depth = ThermalGridResolution,
                AxisShift = ThermalMapAxisShift,
                AmbientCelsius = ambientWaterTemperatureCelsius,
                Diffusion01 = math.saturate(thermalMapDiffusion01)
            };

            _thermalMapJobHandle = job.Schedule(ThermalMapDiffusionSliceCellCount, 32);
            _thermalMapJobActive = true;
        }

        private void CompleteThermalMapJobIfReady()
        {
            if (!_thermalMapJobActive || !_thermalMapJobHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _thermalMapJobHandle, forceComplete: false))
                return;

            _thermalMapJobActive = false;
            _thermalMapDiffusionSlicesCompleted++;
            _thermalMapDiffusionSliceCursor = (_thermalMapDiffusionSliceCursor + 1) & ThermalMapDiffusionSliceMask;
            if (_thermalMapDiffusionSlicesCompleted < ThermalMapDiffusionSliceCount)
                return;

            _thermalMapDiffusionSlicesCompleted = 0;
            NativeArray<float> previousRead = _thermalMapReadCelsius;
            _thermalMapReadCelsius = _thermalMapWriteCelsius;
            _thermalMapWriteCelsius = previousRead;
            _thermalMapVersion++;
            BuildThermalMapVisualProjection();
            StageThermalGridRleDelta();
            MarkThermalMapTextureDirty();
            PublishThermalMapMetadata(active: _activeVentCount > 0 && _thermalMapReadCelsius.IsCreated);
        }

        private int ResolveThermalMapDiffusionSliceCursor()
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            return bucketer != null && bucketer.IsInitialized
                ? bucketer.ActiveColdBucket & ThermalMapDiffusionSliceMask
                : _thermalMapDiffusionSliceCursor & ThermalMapDiffusionSliceMask;
        }

        private void RebuildThermalMapSources()
        {
            if (!_thermalMapSourceCelsius.IsCreated || !_thermalMapInsulation01.IsCreated)
                return;

            Vector3 center = ResolveThermalMapCenter();
            float worldSize = math.max(thermalMapWorldSizeMeters, 1f);
            _thermalMapCellSizeMeters = worldSize * math.rcp(ThermalMapResolution);
            _thermalMapOriginWS = center - new Vector3(worldSize * 0.5f, worldSize * 0.5f, worldSize * 0.5f);

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
                        float temperature = ResolveAmbientTemperatureCelsius(samplePosition);
                        float insulation01 = ResolveVoxelInsulation01(samplePosition);

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
                            float candidate = ResolveAmbientTemperatureCelsius(samplePosition) + (ventDeltaCelsius * heatScale * radialWeight);
                            if (candidate > temperature && math.isfinite(candidate))
                                temperature = candidate;
                        }

                        int index = ToThermalGridIndex(x, y, z);
                        _thermalMapSourceCelsius[index] = temperature;
                        _thermalMapInsulation01[index] = insulation01;
                    }
                }
            }
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
            Shader.SetGlobalFloat(_ThermalMapActiveId, active ? 1f : 0f);
            if (!active)
            {
                BindInactiveThermalMapTexture();
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
                !_thermalMapVisualCelsius.IsCreated)
            {
                BindInactiveThermalMapTexture();
                return;
            }

            if (_thermalMapTextureUploadedVersion == _thermalMapVersion)
                return;

            if (!EnsureThermalMapTexture())
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

        private bool EnsureThermalMapTexture()
        {
            if (_thermalMapTexture != null)
                return true;

            if (_thermalMapTextureFormatRejected)
                return false;

            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RFloat))
            {
                _thermalMapTextureFormatRejected = true;
                return false;
            }

            _thermalMapTexture = new Texture2D(ThermalMapResolution, ThermalMapResolution, TextureFormat.RFloat, false, true)
            {
                name = "__HectonThermalMapRFloat32",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: Texture2D[32x32 RFloat] - GPU-visible center-slice thermal Celsius field for shader/VFX sampling - owner: AbyssalThermalManager
            return true;
        }

        private void BindInactiveThermalMapTexture()
        {
            if (_thermalMapTextureUploadedVersion < 0)
                return;

            Shader.SetGlobalTexture(_ThermalMapTextureId, Texture2D.blackTexture);
            _thermalMapTextureUploadedVersion = -1;
        }

        private bool UsesThermalGrid()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350)
                return false;

            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            return graphicsMemoryMb <= 0 || graphicsMemoryMb > 2048;
        }

        private static float ResolveVoxelInsulation01(Vector3 runtimePosition)
        {
            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            float density = HectonVoxelVolume.GetSDFDensity(new float3(
                (float)absolutePosition.x,
                (float)absolutePosition.y,
                (float)absolutePosition.z));
            return density > 0f ? math.saturate(density) : 0f;
        }

        private void BuildThermalMapVisualProjection()
        {
            if (!_thermalMapVisualCelsius.IsCreated || !_thermalMapReadCelsius.IsCreated)
                return;

            int centerY = ThermalGridResolution >> 1;
            for (int z = 0; z < ThermalGridResolution; z++)
            {
                for (int x = 0; x < ThermalGridResolution; x++)
                    _thermalMapVisualCelsius[(z * ThermalMapResolution) + x] = _thermalMapReadCelsius[ToThermalGridIndex(x, centerY, z)];
            }
        }

        private void StageThermalGridRleDelta()
        {
            _thermalGridRleRunCount = 0;
            _thermalGridRleByteCount = 0;
            _thermalGridRleChecksum = 0u;
            if (!_thermalGridRleRuns.IsCreated || !_thermalMapReadCelsius.IsCreated)
                return;

            int index = 0;
            while (index < _thermalMapReadCelsius.Length && _thermalGridRleRunCount < _thermalGridRleRuns.Length)
            {
                float ambient = ResolveAmbientTemperatureCelsius(GridIndexToWorldPosition(index));
                float temperature = _thermalMapReadCelsius[index];
                if (math.abs(temperature - ambient) <= 0.05f)
                {
                    index++;
                    continue;
                }

                int runStart = index;
                int runCount = 1;
                while (index + runCount < _thermalMapReadCelsius.Length &&
                       runCount < ushort.MaxValue &&
                       _thermalGridRleRunCount < _thermalGridRleRuns.Length &&
                       math.abs(_thermalMapReadCelsius[index + runCount] - temperature) <= 0.05f)
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

        private void ScheduleCrystallizationJobIfNeeded()
        {
            if (_crystallizationJobActive ||
                _pendingCrystallizationSampleCount <= 0 ||
                !_crystallizationSamples.IsCreated ||
                !_crystallizationResults.IsCreated)
            {
                return;
            }

            _scheduledCrystallizationSampleCount = Mathf.Min(_pendingCrystallizationSampleCount, MaxCrystallizationSampleCapacity);
            ThermalCrystallizationBoundaryJob job = new ThermalCrystallizationBoundaryJob
            {
                Samples = _crystallizationSamples,
                Results = _crystallizationResults,
                MinimumSourceTemperatureCelsius = crystallizationMinimumSourceTemperatureCelsius,
                DeltaTemperatureThresholdCelsius = crystallizationDeltaTemperatureThresholdCelsius,
                MinimumRadiusMeters = 0.25f
            };

            _crystallizationJobHandle = job.Schedule(_scheduledCrystallizationSampleCount, 8);
            _crystallizationJobActive = true;
        }

        private void CompleteCrystallizationJobIfReady()
        {
            if (!_crystallizationJobActive || !_crystallizationJobHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _crystallizationJobHandle, forceComplete: false))
                return;

            _crystallizationJobActive = false;

            ResourceDistributionDirector director = resourceDistributionDirector != null
                ? resourceDistributionDirector
                : ResourceDistributionDirector.ActiveRuntimeInstance;
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
            JobHandle dependency = _crystallizationJobActive ? _crystallizationJobHandle : default;
            if (_crystallizationSamples.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_crystallizationSamples);
                dependency = _crystallizationSamples.Dispose(dependency);
                _crystallizationSamples = default;
            }

            if (_crystallizationResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_crystallizationResults);
                dependency = _crystallizationResults.Dispose(dependency);
                _crystallizationResults = default;
            }

            _crystallizationJobHandle = dependency;
            _crystallizationJobActive = false;
            _pendingCrystallizationSampleCount = 0;
            _scheduledCrystallizationSampleCount = 0;
            _debugQueuedCrystallizationSamples = 0;
        }

        private void EnsureThermalMapBuffers()
        {
            float defaultThermalMapAmbient = ResolveAmbientTemperatureCelsius(ResolveThermalMapCenter());
            if (!_thermalMapReadCelsius.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[32768] - front 32x32x32 thermal Celsius grid for gameplay sampling - owner: AbyssalThermalManager
                _thermalMapReadCelsius = new NativeArray<float>(
                    ThermalMapCellCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalMapReadCelsius, NativeMemoryOwner, nameof(_thermalMapReadCelsius), NativeMemoryLifetime);
                FillThermalMap(_thermalMapReadCelsius, defaultThermalMapAmbient);
            }

            if (!_thermalMapWriteCelsius.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[32768] - back 32x32x32 thermal Celsius grid written by Jacobi diffusion job - owner: AbyssalThermalManager
                _thermalMapWriteCelsius = new NativeArray<float>(
                    ThermalMapCellCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalMapWriteCelsius, NativeMemoryOwner, nameof(_thermalMapWriteCelsius), NativeMemoryLifetime);
                FillThermalMap(_thermalMapWriteCelsius, defaultThermalMapAmbient);
            }

            if (!_thermalMapSourceCelsius.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[32768] - deterministic vent source heat field for ColdTick Jacobi solve - owner: AbyssalThermalManager
                _thermalMapSourceCelsius = new NativeArray<float>(
                    ThermalMapCellCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalMapSourceCelsius, NativeMemoryOwner, nameof(_thermalMapSourceCelsius), NativeMemoryLifetime);
                FillThermalMap(_thermalMapSourceCelsius, defaultThermalMapAmbient);
            }

            if (!_thermalMapInsulation01.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[32768] - voxel-SDF insulation field; positive density damps diffusion - owner: AbyssalThermalManager
                _thermalMapInsulation01 = new NativeArray<float>(
                    ThermalMapCellCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalMapInsulation01, NativeMemoryOwner, nameof(_thermalMapInsulation01), NativeMemoryLifetime);
            }

            if (!_thermalMapVisualCelsius.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[1024] - center-slice thermal map projection for GPU/VFX readback - owner: AbyssalThermalManager
                _thermalMapVisualCelsius = new NativeArray<float>(
                    ThermalMapPlaneCellCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalMapVisualCelsius, NativeMemoryOwner, nameof(_thermalMapVisualCelsius), NativeMemoryLifetime);
                FillThermalMap(_thermalMapVisualCelsius, defaultThermalMapAmbient);
            }

            if (!_thermalGridRleRuns.IsCreated)
            {
                // COLD ALLOC: NativeArray<ThermalGridRleRun>[32768] - worst-case non-ambient RLE staging for SaveBinaryStorage - owner: AbyssalThermalManager
                _thermalGridRleRuns = new NativeArray<SaveBinaryStorage.ThermalGridRleRun>(
                    ThermalGridSaveRleCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalGridRleRuns, NativeMemoryOwner, nameof(_thermalGridRleRuns), NativeMemoryLifetime);
            }
        }

        private void DisposeThermalMapBuffers()
        {
            JobHandle dependency = _thermalMapJobActive ? _thermalMapJobHandle : default;
            if (_thermalMapReadCelsius.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_thermalMapReadCelsius);
                dependency = _thermalMapReadCelsius.Dispose(dependency);
                _thermalMapReadCelsius = default;
            }

            if (_thermalMapWriteCelsius.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_thermalMapWriteCelsius);
                dependency = _thermalMapWriteCelsius.Dispose(dependency);
                _thermalMapWriteCelsius = default;
            }

            if (_thermalMapSourceCelsius.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_thermalMapSourceCelsius);
                dependency = _thermalMapSourceCelsius.Dispose(dependency);
                _thermalMapSourceCelsius = default;
            }

            if (_thermalMapInsulation01.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_thermalMapInsulation01);
                dependency = _thermalMapInsulation01.Dispose(dependency);
                _thermalMapInsulation01 = default;
            }

            if (_thermalMapVisualCelsius.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_thermalMapVisualCelsius);
                dependency = _thermalMapVisualCelsius.Dispose(dependency);
                _thermalMapVisualCelsius = default;
            }

            if (_thermalGridRleRuns.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_thermalGridRleRuns);
                dependency = _thermalGridRleRuns.Dispose(dependency);
                _thermalGridRleRuns = default;
            }

            _thermalMapJobHandle = dependency;
            _thermalMapJobActive = false;
            _thermalMapDiffusionSlicesCompleted = 0;
            _thermalMapDiffusionSliceCursor = 0;
            _thermalMapVersion = 0;
            _thermalGridRleRunCount = 0;
            _thermalGridRleByteCount = 0;
            _thermalGridRleChecksum = 0u;
            _thermalMapTextureDirty = false;
            Shader.SetGlobalFloat(_ThermalMapActiveId, 0f);
            ReleaseThermalMapTexture();
        }

        private void ReleaseThermalMapTexture()
        {
            BindInactiveThermalMapTexture();
            _thermalMapTextureFormatRejected = false;
            if (_thermalMapTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_thermalMapTexture);
            else
                DestroyImmediate(_thermalMapTexture);

            _thermalMapTexture = null;
        }

        private void DisposeThermalTelemetry()
        {
            if (!_thermalTelemetryRing.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_thermalTelemetryRing);
            _thermalTelemetryRing.Dispose();
            _thermalTelemetryRing = default;
            _thermalTelemetryIndex = 0;
        }

        private void ResolveDependencies()
        {
            if (biomeMatrixDirector == null)
                biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;

            if (worldZoneDirector == null)
                worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (resourceDistributionDirector == null)
                resourceDistributionDirector = ResourceDistributionDirector.ActiveRuntimeInstance;

            if (vegetationBridge == null)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);

            if (playerTransform == null)
            {
                if (_playerRuntimeContext != null && _playerRuntimeContext.PlayerTransform != null)
                    playerTransform = _playerRuntimeContext.PlayerTransform;
                else if (BootstrapState.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer))
                    playerTransform = bootstrapPlayer;
            }

            if (_playerRigidbody == null && _playerRuntimeContext != null)
                _playerRigidbody = _playerRuntimeContext.PlayerRigidbody;

            if (_playerRigidbody == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerRigidbody);

            if (_playerTransportCoordinator == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (viewCamera == null && playerTransform != null)
            {
                viewCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
                if (viewCamera == null)
                    playerTransform.TryGetComponent(out viewCamera);
            }

            if (_fluidDecalManager == null)
            {
                if (fluidDecalManager != null)
                {
                    _fluidDecalManager = fluidDecalManager;
                }
                else if (!TryGetComponent(out _fluidDecalManager))
                {
                    // COLD ALLOC: Component[1] - local abyssal fluid decal owner added on the thermal manager host when authoring missed it - owner: AbyssalThermalManager
                    _fluidDecalManager = gameObject.AddComponent<AbyssalFluidDecalManager>();
                }
            }

            if (_fluidDecalManager != null && fluidDecalMaterial != null)
                _fluidDecalManager.ConfigureMaterial(fluidDecalMaterial);
        }

        private void CacheRegistryServicesCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            if (cutManager == null)
                cutManager = GlobalRegistry.SargassumCut;
            if (_simulationBucketer == null)
                _simulationBucketer = GlobalRegistry.SimulationBucketer;
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
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    if (_playerRuntimeContext != null)
                    {
                        playerTransform = _playerRuntimeContext.PlayerTransform != null
                            ? _playerRuntimeContext.PlayerTransform
                            : playerTransform;
                        _playerRigidbody = _playerRuntimeContext.PlayerRigidbody != null
                            ? _playerRuntimeContext.PlayerRigidbody
                            : _playerRigidbody;
                        viewCamera = _playerRuntimeContext.PlayerCamera != null
                            ? _playerRuntimeContext.PlayerCamera
                            : viewCamera;
                    }
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.SargassumCutRuntime:
                    cutManager = currentService as SargassumCutManager;
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketer = currentService as ISimulationBucketer;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
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

            if (!_crystallizationSamples.IsCreated)
            {
                // COLD ALLOC: NativeArray<ThermalCrystallizationSample>[32] - thermal boundary job input ring - owner: AbyssalThermalManager
                _crystallizationSamples = new NativeArray<ThermalCrystallizationSample>(
                    MaxCrystallizationSampleCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_crystallizationSamples, NativeMemoryOwner, nameof(_crystallizationSamples), NativeMemoryLifetime);
            }

            if (!_crystallizationResults.IsCreated)
            {
                // COLD ALLOC: NativeArray<ThermalCrystallizationResult>[32] - thermal boundary job output ring - owner: AbyssalThermalManager
                _crystallizationResults = new NativeArray<ThermalCrystallizationResult>(
                    MaxCrystallizationSampleCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_crystallizationResults, NativeMemoryOwner, nameof(_crystallizationResults), NativeMemoryLifetime);
            }

            if (!_thermalTelemetryRing.IsCreated)
            {
                // COLD ALLOC: NativeArray<ThermalTelemetryEntry>[300] - fixed black-box ring for recent thermal state - owner: AbyssalThermalManager
                _thermalTelemetryRing = new NativeArray<ThermalTelemetryEntry>(
                    ThermalTelemetryCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_thermalTelemetryRing, NativeMemoryOwner, nameof(_thermalTelemetryRing), NativeMemoryLifetime);
            }

            if (UsesThermalGrid())
            {
                EnsureThermalMapBuffers();
            }
            else if (!_thermalMapJobActive)
            {
                DisposeThermalMapBuffers();
            }

            if (_bioCableVisuals == null || _bioCableVisuals.Length != MaxVentCapacity)
            {
                // COLD ALLOC: BioCableIK[16] - reusable visual cable rigs paired to active abyssal vent cable zones - owner: AbyssalThermalManager
                _bioCableVisuals = new BioCableIK[MaxVentCapacity];
            }

            if (_thermalBubbleCommands == null || _thermalBubbleCommands.Length != MaxVentCapacity)
            {
                // COLD ALLOC: Vector4[16] - GPU boiling-bubble command staging for active vents - owner: AbyssalThermalManager
                _thermalBubbleCommands = new Vector4[MaxVentCapacity];
            }

            _materialPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - abyssal smoke draw parameters - owner: AbyssalThermalManager
        }

        private void EnsureBuffers()
        {
            bool particleBufferARecreated = EnsureBuffer<AshParticleData>(ref _particleBufferA, smokeParticleCount);
            bool particleBufferBRecreated = EnsureBuffer<AshParticleData>(ref _particleBufferB, smokeParticleCount);
            bool ventBufferRingRecreated = EnsureVentBufferRing();
            if (particleBufferARecreated || particleBufferBRecreated)
            {
                _smokeDispatchFenceArmed = false;
                _forceParticleReset = true;
            }
            if (ventBufferRingRecreated)
                _forceVentBufferUpload = true;

            if (blackSmokeCompute == null)
                return;

            if (_kernelIndex < 0)
            {
                _kernelIndex = blackSmokeCompute.FindKernel("CSMain");
                blackSmokeCompute.GetKernelThreadGroupSizes(_kernelIndex, out uint groupSizeX, out _, out _);
                _threadGroupSizeX = Mathf.Max(1, (int)groupSizeX);
            }

            _dispatchGroupCount = Mathf.Max(1, Mathf.CeilToInt(smokeParticleCount / (float)_threadGroupSizeX));
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

        private void EnsureCableVisuals()
        {
            if (_bioCableVisuals == null)
                return;

            for (int i = 0; i < _bioCableVisuals.Length; i++)
            {
                if (_bioCableVisuals[i] != null)
                    continue;

                // COLD ALLOC: GameObject[1] - persistent abyssal bio-cable visual rig child created once per cable slot - owner: AbyssalThermalManager
                GameObject cableObject = new GameObject("BioCableIK");
                cableObject.transform.SetParent(transform, false);
                cableObject.transform.localPosition = Vector3.zero;
                cableObject.transform.localRotation = Quaternion.identity;
                cableObject.transform.localScale = Vector3.one;

                // COLD ALLOC: Component[1] - persistent abyssal bio-cable IK rig component - owner: AbyssalThermalManager
                BioCableIK cableRig = cableObject.AddComponent<BioCableIK>();
                cableRig.InitializeAt(transform.position, Vector3.up);
                cableRig.SetCableActive(false);
                _bioCableVisuals[i] = cableRig;
            }
        }

        private static bool EnsureBuffer<T>(ref GraphicsBuffer buffer, int count) where T : struct
        {
            int safeCount = Mathf.Max(1, count);
            int stride = UnsafeUtility.SizeOf<T>();
            if (buffer != null && buffer.count == safeCount && buffer.stride == stride)
                return false;

            ReleaseBuffer(ref buffer);

            // COLD ALLOC: GraphicsBuffer[count] - persistent abyssal smoke or vent GPU storage sized from the owning blittable struct - owner: AbyssalThermalManager
            buffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<T>(safeCount);
            return true;
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
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

            WorldZoneAnchor.CopyActiveAnchorsTo(_zoneAnchors);
            AbsoluteUniversePosition playerAup = ResolveAup(playerTransform != null ? playerTransform.position : transform.position);
            for (int i = 0; i < _zoneAnchors.Count && _activeVentCount < maxActiveVentCount; i++)
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
            UploadVentBuffer();
            if (RequiresParticleReset())
                ResetParticles();
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
            if (_runtimeVentRegistrations.Count <= 0)
                return;

            for (int i = 0; i < _runtimeVentRegistrations.Count && _activeVentCount < maxActiveVentCount; i++)
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
            if (radiusWS <= 0f || heatIntensity <= 0f)
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

            double3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(positionWS);
            HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;
            VoxelDeltaProcessor deltaProcessor = engine != null ? engine.GetComponent<VoxelDeltaProcessor>() : null;
            if (deltaProcessor != null)
            {
                deltaProcessor.AcceptThermalMeltEvent(new ThermalMeltEvent
                {
                    AbsoluteUniversePosition = new Vector3(
                        (float)absoluteUniversePosition.x,
                        (float)absoluteUniversePosition.y,
                        (float)absoluteUniversePosition.z),
                    AbsoluteUniversePositionDouble = absoluteUniversePosition,
                    RadiusMeters = Mathf.Max(1f, radiusWS * 0.35f),
                    Heat01 = Mathf.Clamp01(heatIntensity / Mathf.Max(1f, ventHeatIntensity * seismicEruptionHeatMultiplier))
                });
            }
        }

        private static void PublishThermalSourceSignal(Vector3 positionWS, float radiusWS, float heatIntensity, uint sourceId)
        {
            ThermalSourceSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(positionWS);
            signal.RadiusMeters = math.max(0f, radiusWS);
            signal.IntensityCelsiusPerSecond = math.max(0f, heatIntensity);
            signal.SourceId = sourceId != 0u ? sourceId : BuildTransientThermalSourceId(positionWS, radiusWS);
            signal.Frame = unchecked((uint)math.max(0, HectonArenaAllocator.CurrentFrameSequence));
            SignalBus<ThermalSourceSignal>.Push(in signal);
        }

        private static uint BuildTransientThermalSourceId(Vector3 positionWS, float radiusWS)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            float3 quantized = math.round(new float3(positionWS.x, positionWS.y, positionWS.z) * 0.25f);
            hash = FoldThermalSourceHash(hash, math.asuint(quantized.x), fnvPrime);
            hash = FoldThermalSourceHash(hash, math.asuint(quantized.y), fnvPrime);
            hash = FoldThermalSourceHash(hash, math.asuint(quantized.z), fnvPrime);
            hash = FoldThermalSourceHash(hash, math.asuint(radiusWS), fnvPrime);
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
            int safeCount = Mathf.Clamp(bubbleCommandCount, 0, MaxVentCapacity);
            Shader.SetGlobalInt(_ThermalBubbleCommandCountId, safeCount);
            if (safeCount <= 0 || _thermalBubbleCommands == null)
                return;

            Shader.SetGlobalVectorArray(_ThermalBubbleCommandDataId, _thermalBubbleCommands);
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
            EnsureStorage();
            EnsureVentBufferRing();
            BuildVentGpuUploadData();
            if (!_hasSmokeData || _activeVentCount <= 0 || !RequiresVentBufferUpload())
                return;

            if (!TryResolveReusableVentUploadBuffer(out GraphicsBuffer uploadBuffer, out int uploadBufferIndex))
                return;

            GraphicsBufferUploadUtility.UploadArraySetData(uploadBuffer, _ventGpuData, MaxVentCapacity);
            CacheUploadedVentData();
            _activeVentBufferIndex = uploadBufferIndex;
            _nextVentBufferUploadIndex = (_activeVentBufferIndex + 1) % VentBufferRingSize;
            _forceVentBufferUpload = false;
        }

        private void ResetParticles()
        {
            EnsureStorage();
            EnsureBuffer<AshParticleData>(ref _particleBufferA, smokeParticleCount);
            EnsureBuffer<AshParticleData>(ref _particleBufferB, smokeParticleCount);
            if (_initialParticles == null || _particleBufferA == null || _particleBufferB == null)
                return;

            if (!CanRewriteParticleBuffers())
                return;

            if (_activeVentCount <= 0)
            {
                System.Array.Clear(_initialParticles, 0, _initialParticles.Length);
                GraphicsBufferUploadUtility.UploadArraySetData(_particleBufferA, _initialParticles, smokeParticleCount);
                GraphicsBufferUploadUtility.UploadArraySetData(_particleBufferB, _initialParticles, smokeParticleCount);
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

            GraphicsBufferUploadUtility.UploadArraySetData(_particleBufferA, _initialParticles, smokeParticleCount);
            GraphicsBufferUploadUtility.UploadArraySetData(_particleBufferB, _initialParticles, smokeParticleCount);
            CacheSeededVentTopology();
            _forceParticleReset = false;
            _frameParity = 0;
        }

        private void BindSmokeUniforms(float dt)
        {
            GraphicsBuffer readBuffer = _frameParity == 0 ? _particleBufferA : _particleBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _particleBufferB : _particleBufferA;
            GraphicsBuffer activeVentBuffer = ResolveActiveVentBuffer();
            if (readBuffer == null || writeBuffer == null || activeVentBuffer == null)
                return;

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
                LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
                if (manager != null)
                    manager.RequestExternalPdaCorrosion(1f, empPdaCorrosionDuration);
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

        private static double ComputeAupDistanceSq(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            AbsoluteUniversePosition a = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionA);
            AbsoluteUniversePosition b = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionB);
            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private static float ComputeAupPlanarDistance(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            AbsoluteUniversePosition a = ResolveAup(runtimePositionA);
            return ComputeAupPlanarDistance(in a, runtimePositionB);
        }

        private static float ComputeAupPlanarDistance(in AbsoluteUniversePosition originAup, Vector3 runtimePositionB)
        {
            AbsoluteUniversePosition targetAup = ResolveAup(runtimePositionB);
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
            return AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
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
            AbsoluteUniversePosition playerAup = ResolveAup(playerPosition);
            Vector3 playerVelocity = hasPlayer && _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;

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
                Vector2 planarDelta = new Vector2(playerPosition.x - cableAnchor.x, playerPosition.z - cableAnchor.z);
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
            if (_activeVentCount <= 0)
                return false;

            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float cableRadius = Mathf.Max(0.1f, vent.CableRadiusWS);
                Vector2 planarDelta = new Vector2(positionWS.x - vent.CableAnchorWS.x, positionWS.z - vent.CableAnchorWS.z);
                float planarDistance = ComputeAupPlanarDistance(positionWS, vent.CableAnchorWS);
                if (planarDistance > cableRadius)
                    continue;

                float tension = 1f - planarDistance / Mathf.Max(cableRadius, 0.001f);
                if (tension <= cableTension01)
                    continue;

                cableTension01 = tension;
                cableAnchorWS = ResolveCableAnchor(positionWS, vent.CableAnchorWS);
                cableCutProgress01 = ResolveCableCutProgress(positionWS, cableAnchorWS, cableRadius);
            }

            return cableTension01 > 0f;
        }

        private Vector3 ResolveCableAnchor(Vector3 positionWS, Vector3 cableAnchorWS)
        {
            Vector3 planarDelta = positionWS - cableAnchorWS;
            planarDelta.y = 0f;
            if (planarDelta.sqrMagnitude <= 0.0001f || cableAnchorPull <= 0f)
                return cableAnchorWS;

            return cableAnchorWS + ResolveSafeDirection(planarDelta, Vector3.forward) * cableAnchorPull;
        }

        private float ResolveCableCutProgress(Vector3 positionWS, Vector3 cableAnchorWS, float cableRadiusWS)
        {
            if (cutManager == null)
                return 0f;

            float queryRadius = Mathf.Min(cableRadiusWS * 0.35f, cableCutQueryRadius);
            if (!cutManager.SampleRecentCutArea(positionWS, queryRadius, out float accumulatedAreaWS, out float strongestCut01))
                return 0f;

            float requiredArea = Mathf.PI * queryRadius * queryRadius * cableCutReleaseThreshold;
            float areaProgress = requiredArea > 0.0001f ? Mathf.Clamp01(accumulatedAreaWS / requiredArea) : 0f;
            float strengthProgress = Mathf.Clamp01(strongestCut01 / Mathf.Max(cableCutReleaseThreshold, 0.0001f));
            return Mathf.Clamp01(Mathf.Max(areaProgress, strengthProgress));
        }

        private bool IsAbyssalThermalContext()
        {
            if (biomeMatrixDirector == null || biomeMatrixDirector.CurrentDepthMeters < abyssalVentStartDepthMeters)
                return false;

            HectonBiomeFamilyProfile family = worldZoneDirector != null && worldZoneDirector.CurrentZone != null && worldZoneDirector.CurrentZone.DominantBiomeFamily != null
                ? worldZoneDirector.CurrentZone.DominantBiomeFamily
                : biomeMatrixDirector.CurrentFamilyProfile;
            return IsThermalBiomeFamily(family);
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
                : biomeMatrixDirector != null ? biomeMatrixDirector.CurrentFamilyProfile : null;
            return IsThermalBiomeFamily(family);
        }

        void IRandomEventListener.OnRandomEventStarted(RandomEventType type, float intensity)
        {
            HandleRandomEventStarted(type, intensity);
        }

        void IRandomEventListener.OnRandomEventEnded(RandomEventType type)
        {
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
            if (_runtimeVentRegistrations.Count <= 0 && _activeVentCount <= 0)
                return;

            float strength = Mathf.Clamp01(payload.ImpulseMagnitude / 20f);
            TriggerSeismicEruption(strength, payload.ImpulseMagnitude);
        }

        private void TriggerSeismicEruption(float strength01, float impulseMagnitude)
        {
            if (_activeVentCount <= 0 && _runtimeVentRegistrations.Count <= 0)
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
            if (!GlobalSignals.TryGetLatestSeismicSignal(out SeismicSignal signal, out int sequence) ||
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
                GlobalRegistry.RegisterThermodynamicsRuntime(this);
                _registeredThermodynamicsRuntime = ReferenceEquals(GlobalRegistry.Thermodynamics, this);
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_registeredFixedTick)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixedTick = GlobalRegistry.FixedTickables.Contains(this);
            }

            if (!_registeredLateFrameTick)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }
        }

        private void TryUnregister()
        {
            if (_registeredThermodynamicsRuntime)
            {
                GlobalRegistry.UnregisterThermodynamicsRuntime(this);
                _registeredThermodynamicsRuntime = false;
            }

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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ThermalCrystallizationBoundaryJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<ThermalCrystallizationSample> Samples;
            [NoAlias]
            public NativeArray<ThermalCrystallizationResult> Results;
            public float MinimumSourceTemperatureCelsius;
            public float DeltaTemperatureThresholdCelsius;
            public float MinimumRadiusMeters;

            public void Execute(int index)
            {
                ThermalCrystallizationSample sample = Samples[index];
                ThermalCrystallizationResult result = default;
                if (sample.Pending == 0 ||
                    sample.RadiusMeters < MinimumRadiusMeters ||
                    !math.all(math.isfinite(sample.PositionWS)) ||
                    !math.isfinite(sample.PreviousTemperatureCelsius) ||
                    !math.isfinite(sample.CurrentTemperatureCelsius))
                {
                    Results[index] = result;
                    return;
                }

                float delta = sample.CurrentTemperatureCelsius - sample.PreviousTemperatureCelsius;
                bool accepted = sample.PreviousTemperatureCelsius >= MinimumSourceTemperatureCelsius &&
                                delta <= DeltaTemperatureThresholdCelsius;
                result.PositionWS = sample.PositionWS;
                result.DeltaTemperatureCelsius = delta;
                result.RadiusMeters = math.max(MinimumRadiusMeters, sample.RadiusMeters);
                result.SourceId = sample.SourceId;
                result.ShouldSpawn = accepted ? (byte)1 : (byte)0;
                Results[index] = result;
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

        private void OnValidate()
        {
            SanitizeSettings();

            if (bioCableMaterial == null)
                bioCableMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultBioCableMaterialPath);

            if (fluidDecalMaterial == null)
                fluidDecalMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultFluidDecalMaterialPath);

            if (fluidDecalManager == null)
                TryGetComponent(out fluidDecalManager);
        }
#endif
    }
}
