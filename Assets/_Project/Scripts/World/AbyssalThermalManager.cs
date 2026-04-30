using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
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
    public sealed class AbyssalThermalManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IThermodynamicsService
    {
        public struct ThermalFlowSample
        {
            public bool HasFlow;
            public Vector3 FlowVelocityWS;
            public float Heat01;
            public float DragMultiplier;
            public bool IsCableZone;
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

        private const int MaxVentCapacity = 16;
        private const int MaxAnchorScanCapacity = 32;
        private const int MaxSmokeParticleCapacity = 8192;
        private const int MaxEmpNestCapacity = 8;
        private const int MaxCrystallizationSampleCapacity = 32;
        private const int VentBufferRingSize = 3;
        private const float VentStateCompareEpsilon = 0.01f;
        private const uint ThermalHashSeed = 0xC6BC2796u;

        private static AbyssalThermalManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

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
        private AbyssalFluidDecalManager _fluidDecalManager;
        private readonly RaycastHit[] _empHits = new RaycastHit[4]; // COLD ALLOC: RaycastHit[4] - bounded EMP nest pulse cast hits - owner: AbyssalThermalManager
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
        private NativeArray<ThermalCrystallizationSample> _crystallizationSamples;
        private NativeArray<ThermalCrystallizationResult> _crystallizationResults;
        private JobHandle _crystallizationJobHandle;
        private bool _crystallizationJobActive;
        private int _pendingCrystallizationSampleCount;
        private int _scheduledCrystallizationSampleCount;
        private float[] _ventCrystallizationCooldowns;

        public static AbyssalThermalManager Instance => _instance;

        /// <summary>
        /// True once the thermodynamics owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.ThermodynamicsService, this) &&
                                     ReferenceEquals(GlobalRegistry.Thermodynamics, this);

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

            float eruptionBlend = ResolveSeismicEruptionBlend();
            if (eruptionBlend <= 0.001f)
                return false;

            float bestHeat = 0f;
            for (int i = 0; i < _activeVentCount; i++)
            {
                ThermalVentState vent = _ventStates[i];
                float eruptiveHeat = Mathf.Max(0f, vent.HeatIntensity) * Mathf.Lerp(1f, seismicEruptionHeatMultiplier, eruptionBlend);
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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("[AbyssalThermalManager] Duplicate instance detected. Destroying the newer component.", this);
                Destroy(this);
                return;
            }

            _instance = this;
            _instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            _supportsGraphicsFence = SystemInfo.supportsGraphicsFence;
            SanitizeSettings();
            ResolveDependencies();
            EnsureStorage();
            EnsureCableVisuals();
            EnsureBuffers();
            ClearHazardSources();
            RebuildVentField();
        }

        private void OnEnable()
        {
            LaserCutter.OnBeamStateChanged += HandleCutterBeamStateChanged;
            RandomEventEvents.OnSeismicShockwave += HandleSeismicShockwave;
            RandomEventEvents.OnEventStarted += HandleRandomEventStarted;
            HectonFloatingOrigin.RegisterListener(this);
            ResolveDependencies();
            EnsureStorage();
            EnsureCableVisuals();
            EnsureBuffers();
            RebuildVentField();
            TryRegister();
        }

        private void OnDisable()
        {
            LaserCutter.OnBeamStateChanged -= HandleCutterBeamStateChanged;
            RandomEventEvents.OnSeismicShockwave -= HandleSeismicShockwave;
            RandomEventEvents.OnEventStarted -= HandleRandomEventStarted;
            HectonFloatingOrigin.UnregisterListener(this);
            _cutterBeamActive = false;
            _activeCutterTransform = null;
            _debugCutterSeveringCable = false;
            _seismicEruptionTimer = 0f;
            _seismicEruptionStrength01 = 0f;
            _debugSeismicEruptionSeconds = 0f;
            _hasSmokeData = false;
            _frameParity = 0;
            ClearHazardSources();
            ReleaseBuffers();
            DisposeCrystallizationBuffers();
            TryUnregister();
        }

        private void OnDestroy()
        {
            LaserCutter.OnBeamStateChanged -= HandleCutterBeamStateChanged;
            RandomEventEvents.OnSeismicShockwave -= HandleSeismicShockwave;
            RandomEventEvents.OnEventStarted -= HandleRandomEventStarted;
            HectonFloatingOrigin.UnregisterListener(this);
            ClearHazardSources();
            ReleaseBuffers();
            DisposeCrystallizationBuffers();

            if (_instance == this)
                _instance = null;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

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

            _forceVentBufferUpload = true;
            _forceParticleReset = true;
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
            float deltaTime = Mathf.Max(0f, dt);
            UpdateSeismicEruption(deltaTime);
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

            _simulationTime += deltaTime;
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
            AdvancePassiveCrystallizationCooldowns(0.5f);
            RebuildVentField();
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
            CompleteCrystallizationJobIfReady();
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
                float eruptionBlend = ResolveSeismicEruptionBlend();
                float eruptiveHeatScale = Mathf.Lerp(1f, seismicEruptionHeatMultiplier, eruptionBlend);
                float eruptiveUpdraftScale = Mathf.Lerp(1f, seismicEruptionUpdraftMultiplier, eruptionBlend);
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
                        sample.HasFlow = true;
                        sample.Heat01 = Mathf.Max(sample.Heat01, vent.HeatIntensity * eruptiveHeatScale * ventWeight);
                        sample.DragMultiplier = Mathf.Max(sample.DragMultiplier, Mathf.Lerp(1f, ventDragMultiplier, ventWeight));
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
                sample.IsCableZone = true;
                sample.CableAnchorWS = strongestCableAnchor;
                sample.CableCutProgress01 = strongestCableCut;
                sample.CableEscapeSuppression01 = 1f - strongestCableCut;
                sample.CableTension01 = strongestCable * sample.CableEscapeSuppression01;
            }

            _debugCableCutProgress01 = strongestCableCut;
            return sample.HasFlow || sample.IsCableZone;
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

            float eruptionHeatScale = Mathf.Lerp(1f, seismicEruptionHeatMultiplier, ResolveSeismicEruptionBlend());
            for (int i = 0; i < _activeVentCount && _pendingCrystallizationSampleCount < MaxCrystallizationSampleCapacity; i++)
            {
                if (_ventCrystallizationCooldowns[i] > 0f)
                    continue;

                ThermalVentState vent = _ventStates[i];
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

        private Vector3 ResolveCrystallizationBoundaryPosition(in ThermalVentState vent, int ventIndex)
        {
            float angle = HashToFloat01((uint)_instanceId, (uint)(ventIndex + 1), 0x7D5C2A11u) * Mathf.PI * 2f;
            float radius = Mathf.Max(0.5f, vent.RadiusWS * 0.88f);
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            return vent.PositionWS + radial + Vector3.up * Mathf.Max(0.15f, vent.HeightWS * 0.08f);
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

            _crystallizationJobHandle.Complete();
            _crystallizationJobHandle = default;
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
                dependency = _crystallizationSamples.Dispose(dependency);
                _crystallizationSamples = default;
            }

            if (_crystallizationResults.IsCreated)
            {
                dependency = _crystallizationResults.Dispose(dependency);
                _crystallizationResults = default;
            }

            _crystallizationJobHandle = dependency;
            _crystallizationJobActive = false;
            _pendingCrystallizationSampleCount = 0;
            _scheduledCrystallizationSampleCount = 0;
            _debugQueuedCrystallizationSamples = 0;
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

            if (cutManager == null)
                cutManager = SargassumCutManager.Instance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (_playerRigidbody == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerRigidbody);

            if (_playerTransportCoordinator == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (viewCamera == null && playerTransform != null)
                viewCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());

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
            }

            if (!_crystallizationResults.IsCreated)
            {
                // COLD ALLOC: NativeArray<ThermalCrystallizationResult>[32] - thermal boundary job output ring - owner: AbyssalThermalManager
                _crystallizationResults = new NativeArray<ThermalCrystallizationResult>(
                    MaxCrystallizationSampleCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
            }

            if (_bioCableVisuals == null || _bioCableVisuals.Length != MaxVentCapacity)
            {
                // COLD ALLOC: BioCableIK[16] - reusable visual cable rigs paired to active abyssal vent cable zones - owner: AbyssalThermalManager
                _bioCableVisuals = new BioCableIK[MaxVentCapacity];
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
                GameObject cableObject = new GameObject($"BioCableIK_{i:00}");
                cableObject.transform.SetParent(transform, false);
                cableObject.transform.localPosition = Vector3.zero;
                cableObject.transform.localRotation = Quaternion.identity;
                cableObject.transform.localScale = Vector3.one;

                LineRenderer lineRenderer = cableObject.AddComponent<LineRenderer>();
                lineRenderer.enabled = false;
                lineRenderer.sharedMaterial = bioCableMaterial;

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

                int spawnCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maxVentsPerAnchor, holdWeight)), 1, maxVentsPerAnchor);
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
            float radialDistance = Mathf.Lerp(anchorRadius * 0.15f, anchorRadius, radial01);
            Vector3 ventOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radialDistance;
            Vector3 ventPosition = anchorPosition + ventOffset;
            float radius = Mathf.Lerp(ventRadiusMin, ventRadiusMax, HashToFloat01(hashIndex, (uint)(ventIndex + 5), 0x1B56C4E9u));
            float updraft = ventUpdraftVelocity * Mathf.Lerp(0.85f, 1.2f, anchorWeight);
            float heat = ventHeatIntensity * Mathf.Lerp(0.85f, 1.15f, anchorWeight);
            float smokeDensity = Mathf.Lerp(0.55f, 1.25f, HashToFloat01(hashIndex, (uint)(ventIndex + 9), 0x94D049BBu));
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
            float eruptionBlend = ResolveSeismicEruptionBlend();
            float eruptiveHeatScale = Mathf.Lerp(1f, seismicEruptionHeatMultiplier, eruptionBlend);
            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (i < _activeVentCount)
                {
                    ThermalVentState vent = _ventStates[i];
                    float hazardRadius = Mathf.Max(vent.RadiusWS, vent.RadiusWS * ventHeatRadiusMultiplier);
                    HectonHazardManager.Register(
                        vent.HazardSourceId,
                        vent.PositionWS,
                        vent.HeatIntensity * eruptiveHeatScale,
                        hazardRadius * Mathf.Lerp(1f, 1.45f, eruptionBlend),
                        HazardType.Heat);
                }
                else
                {
                    HectonHazardManager.Unregister(BuildHazardSourceId(i));
                }
            }
        }

        private void ClearHazardSources()
        {
            for (int i = 0; i < MaxVentCapacity; i++)
                HectonHazardManager.Unregister(BuildHazardSourceId(i));
        }

        private void BuildVentGpuUploadData()
        {
            float eruptionBlend = ResolveSeismicEruptionBlend();
            float eruptiveUpdraftScale = Mathf.Lerp(1f, seismicEruptionUpdraftMultiplier, eruptionBlend);
            float eruptiveHeatScale = Mathf.Lerp(1f, seismicEruptionHeatMultiplier, eruptionBlend);
            float eruptiveSmokeScale = Mathf.Lerp(1f, seismicEruptionSmokeMultiplier, eruptionBlend);
            float eruptiveHeightScale = Mathf.Lerp(1f, seismicEruptionHeightMultiplier, eruptionBlend);
            for (int i = 0; i < MaxVentCapacity; i++)
            {
                if (i < _activeVentCount)
                {
                    ThermalVentState vent = _ventStates[i];
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
                }
                else
                {
                    _ventGpuData[i] = default;
                }
            }
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

            _smokeDispatchFence = Graphics.CreateAsyncGraphicsFence();
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
                float radiusT = Mathf.Sqrt(HashToFloat01((uint)i, (uint)ventIndex, 0x8EBC6AF1u));
                float radialDistance = vent.RadiusWS * 0.45f * radiusT;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radialDistance;
                Vector3 position = vent.PositionWS + offset + Vector3.up * Mathf.Lerp(0.2f, 1.6f, HashToFloat01((uint)i, (uint)ventIndex, 0x589965CDu));
                Vector3 velocity = Vector3.up * (vent.UpdraftVelocity * Mathf.Lerp(0.65f, 1.05f, seed));
                float size = Mathf.Lerp(smokeParticleSizeMin, smokeParticleSizeMax, HashToFloat01((uint)i, (uint)ventIndex, 0x1D8E4E27u));
                float maxLifetime = Mathf.Lerp(1.8f, 5.6f, HashToFloat01((uint)i, (uint)ventIndex, 0xA4093822u));

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
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, smokeParticleCount);
        }

        private void UpdateSmokeBounds()
        {
            float eruptionBlend = ResolveSeismicEruptionBlend();
            float eruptiveHeightScale = Mathf.Lerp(1f, seismicEruptionHeightMultiplier, eruptionBlend);
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

            Vector3 stampPosition = positionWS + forward.normalized * cableCutForwardOffset;
            cutManager.RegisterExternalCut(stampPosition, cableCutStampRadius, cableCutStampStrength, forward, 0.18f);
            _cableCutStampCooldown = cableCutStampInterval;
            _debugCutterSeveringCable = true;

            if (_fluidDecalManager != null && cableCutProgress >= cableCutReleaseThreshold && _cableFluidDecalCooldown <= 0f)
            {
                float decalScale = Mathf.Clamp01(cableTension * Mathf.Lerp(0.65f, 1.15f, cableCutProgress));
                _fluidDecalManager.RegisterCableFluid(cableAnchorWS, decalScale);
                _cableFluidDecalCooldown = 1.2f;
            }
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
                float distance = distanceSq > 0d ? (float)math.sqrt(distanceSq) : 0f;
                bool canCharge = transportActive && distance <= nest.RadiusWS;
                if (canCharge && nest.Cooldown <= 0f)
                {
                    float chargeRate = empChargeDuration > 0.0001f ? 1f / empChargeDuration : 1f;
                    float chargeWeight = 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, nest.RadiusWS));
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
            float castDistance = Mathf.Min(empPulseRange, toPlayer.magnitude);
            if (castDistance <= 0.0001f)
                castDistance = empPulseRange;

            Vector3 castDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.forward;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                nest.PositionWS,
                empSphereCastRadius,
                castDirection,
                _empHits,
                castDistance,
                ~0,
                QueryTriggerInteraction.Collide);

            bool hitPlayerTransport = false;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = _empHits[hitIndex].collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.transform == playerTransform ||
                    hitCollider.transform.IsChildOf(playerTransform) ||
                    hitCollider.GetComponentInParent<MantaScooter>() == mantaScooter)
                {
                    hitPlayerTransport = true;
                    break;
                }
            }

            if (hitPlayerTransport)
            {
                mantaScooter.ApplyEmpDisruption(empMisfireDuration);
                LocalizationManager manager = LocalizationManager.Instance;
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
                double travelDistanceSq = ComputeAupDistanceSq(sourceAnchor, targetAnchor);
                float travelDistance = travelDistanceSq > 0d ? (float)math.sqrt(travelDistanceSq) : 0f;
                float delay = i == sourceVentIndex
                    ? 0f
                    : travelDistance / Mathf.Max(empChainPropagationSpeed, 0.001f);

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
            double planarDistanceSq = (deltaX * deltaX) + (deltaZ * deltaZ);
            return planarDistanceSq > 0d ? (float)math.sqrt(planarDistanceSq) : 0f;
        }

        private static AbsoluteUniversePosition ResolveAup(Vector3 runtimePosition)
        {
            return AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
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

                    float recoilSpeed = cableSnapRecoilSpeed * Mathf.Lerp(0.65f, 1.25f, tension01);
                    cableRig.TriggerSnapRecoil(snapDirection.normalized * recoilSpeed, cableSnapDuration);
                    if (_fluidDecalManager != null)
                        _fluidDecalManager.RegisterCableFluid(cableAnchor, Mathf.Clamp01(Mathf.Lerp(0.75f, 1.2f, tension01)));
                }
                else if (!snapped && !elasticReleased && _cableReleasedStates[i] && cutProgress01 <= cableCutReleaseThreshold * 0.45f)
                {
                    _cableReleasedStates[i] = false;
                }

                _cableReleaseProgress[i] = cutProgress01;
                float empPulse01 = empCharge01 > 0f
                    ? 0.5f + 0.5f * Mathf.Sin((_simulationTime * empPulseSpeed) + i * 0.6180339f)
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
                Vector3 hullDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.forward;
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
                        _fluidDecalManager.RegisterCableFluid(cableAnchor, Mathf.Clamp01(Mathf.Lerp(0.8f, 1.35f, tension01)));
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

            return cableAnchorWS + planarDelta.normalized * cableAnchorPull;
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

        private void HandleRandomEventStarted(RandomEventType type, float intensity)
        {
            if (type != RandomEventType.ThermalEruption)
                return;

            TriggerSeismicEruption(Mathf.Clamp01(intensity), ventHeatIntensity * Mathf.Max(1f, intensity));
        }

        private void HandleSeismicShockwave(SeismicShockwaveEvent payload)
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
                _registeredThermodynamicsRuntime = true;
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = true;
            }

            if (!_registeredLateFrameTick)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = true;
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

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct ThermalCrystallizationBoundaryJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ThermalCrystallizationSample> Samples;
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
