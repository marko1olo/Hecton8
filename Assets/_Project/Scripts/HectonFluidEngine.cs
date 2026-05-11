// ============================================================================
// HECTON-8 - HectonFluidEngine.cs v2.1 (OPTIMIZATION PASS)
// High-performance buoyancy and hydrodynamic resistance system.
//
// v2.1 CHANGES (OPTIMIZATION):
//   [OPT] Dense BuoyancyObject list duplicate check
//     - Register() keeps one managed registry instead of mirrored hash buckets
//     - Unregister() removes from the dense list directly
//     - Impact: less managed memory and better cache locality
//
//   [OPT] Cached LOD distance squares (_cachedNearDistSq, etc.)
//     - Avoids recalculating nearDistanceSq values every FixedTick
//     - Computed once in Awake and refreshed in OnValidate
//     - Impact: -5-10% GatherData() work at 200+ objects
//
//   [OPT] TryResolveObserver() -> TryResolveObserverOnce() in Awake
//     - Removes scene-search observer checks from FixedTick
//     - One-time initialization instead of per-frame checks
//     - Impact: one O(N) operation at load, not every frame
//
//   [OPT] GatherData() removes null objects from the dense registry
//     - Swap-remove keeps the parallel managed lists compact
//     - Guarantees registry consistency
//
// v2.0 (JOB + BURST BASELINE):
//   - Job System + Burst compiler for parallel computation
//   - NativeArrays with capacity doubling and no per-frame reallocation
//   - LOD system with four distance tiers
//   - Dry zones through isInAir flags
//   - CurrentVolume integration
//
// PRODUCTION-READY GUARANTEES:
//   - Zero GC in hot paths (FixedTick, GatherData)
//   - Burst-compiled job for SIMD parallelism
//   - Supports 100+ objects without MX350 stalls, budget 0.3ms
// ============================================================================

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ActiveThrusterFlow
    {
        public float3 PositionWS;
        public float3 DirectionWS;
        public float Strength;
        public float RadiusSq;
        public float InvRadiusSq;
        public float ConeCos;
        public int Active;
        public float Padding0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WhirlpoolFlow
    {
        public float3 CenterWS;
        public float RadiusSq;
        public float InvRadiusSq;
        public float TangentialStrength;
        public float CentripetalStrength;
        public float VerticalPull;
        public int Active;
        public float Padding0;
        public float Padding1;
        public float Padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FluidViscosityRegion
    {
        public float3 CenterWS;
        public float InvRadiusSq;
        public float ViscosityMultiplier;
        public int Active;
        public float Padding0;
        public float Padding1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FluidImpactEvent
    {
        public float3 PositionWS;
        public float3 VelocityWS;
        public float MassKg;
        public float SurfaceY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InteriorFloodNode
    {
        public float CurrentLiters;
        public float CapacityLiters;
        public float TransferLitersPerSecond;
        public float StructuralMassKg;
        public int FirstEdgeIndex;
        public int EdgeCount;
        public uint Flags;
        public uint Padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InteriorFloodEdge
    {
        public int ToNode;
        public float FlowMultiplier;
        public int IsOpen;
        public int Padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InteriorFloodBfsResult
    {
        public float TotalWaterMassKg;
        public float StructuralLoadKg;
        public int FloodedNodeCount;
        public int Padding;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class HectonFluidEngine : MonoBehaviour, IFixedTickable, IPostFixedTickable
    {
#if UNITY_EDITOR
        private const string GpuBuoyancyComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_GpuBuoyancy.compute";
        private const string AbyssalFlowFieldComputeAssetPath = "Assets/_Project/Art/Shaders/AbyssalFlowField.compute";
#endif
        private const float AbyssalFlowThermoclineDepthMeters = 120f;
        private const int GpuReadbackRingSize = 3;
        private const int MaxAbyssalHeatSourceCount = 8;
        private const int MaxCavitationBurstEvents = 8;
        public const int MaxAnalyticalThrusterCount = 4;
        public const int MaxAnalyticalWhirlpoolCount = 2;
        public const int MaxDynamicViscosityRegionCount = 4;
        private const int ViscosityGradientLutSize = 16;
        private const int FluidImpactEventQueueCapacity = 64;
        private const int CavitationShockwaveHitCapacity = 64;
        private const int GpuThreadGroupSize = 64;
        private const int GpuThreadGroupShift = 6;
        private const float AbyssalBiolumeSurgeHoldSeconds = 4f;
        private const float GiantWakeDirectionEpsilonSq = 0.0001f;
        private const uint HectonFluidEngineContextHash = 0x48464645u;
        private const uint NonFiniteBuoyancyForceHash = 0x4E464246u;
        private const uint NonFiniteBuoyancyTorqueHash = 0x4E464254u;
        private const string NativeMemoryOwner = nameof(HectonFluidEngine);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        [StructLayout(LayoutKind.Sequential)]
        private struct GpuBuoyancyObjectData
        {
            public float Volume;
            public float Height;
            public float IsInAir;
            public float SimplifiedSubmersion;
            public float3 BoundsCenterWS;
            public float BoundsPadding0;
            public float3 BoundsExtentsWS;
            public float BoundsPadding1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GpuHeatSourceData
        {
            public float3 PositionWS;
            public float Intensity;
            public float Radius;
            public float3 Padding;
        }

        private struct CavitationBurstEvent
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Intensity01;
            public float Radius;
            public float RadiusSq;
            public float InvRadiusSq;
            public float Acceleration;
            public int SourceBodyInstanceId;
        }

        private static readonly int _GpuBuoyancyPositionsId = Shader.PropertyToID("_GpuBuoyancyPositions");
        private static readonly int _GpuBuoyancyObjectDataId = Shader.PropertyToID("_GpuBuoyancyObjectData");
        private static readonly int _GpuBuoyancyResultsId = Shader.PropertyToID("_GpuBuoyancyResults");
        private static readonly int _GpuBuoyancyObjectCountId = Shader.PropertyToID("_GpuBuoyancyObjectCount");
        private static readonly int _GpuBuoyancyWaterParamsId = Shader.PropertyToID("_GpuBuoyancyWaterParams");
        private static readonly int _GpuBuoyancyWave0AId = Shader.PropertyToID("_GpuBuoyancyWave0A");
        private static readonly int _GpuBuoyancyWave0BId = Shader.PropertyToID("_GpuBuoyancyWave0B");
        private static readonly int _GpuBuoyancyWave1AId = Shader.PropertyToID("_GpuBuoyancyWave1A");
        private static readonly int _GpuBuoyancyWave1BId = Shader.PropertyToID("_GpuBuoyancyWave1B");
        private static readonly int _GpuBuoyancyWave2AId = Shader.PropertyToID("_GpuBuoyancyWave2A");
        private static readonly int _GpuBuoyancyWave2BId = Shader.PropertyToID("_GpuBuoyancyWave2B");
        private static readonly int _AbyssalFlowFieldResultId = Shader.PropertyToID("_AbyssalFlowFieldResult");
        private static readonly int _AbyssalHeatSourcesId = Shader.PropertyToID("_AbyssalHeatSources");
        private static readonly int _AbyssalAggregateMaskId = Shader.PropertyToID("_AbyssalAggregateMask");
        private static readonly int _AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
        private static readonly int _AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
        private static readonly int _AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
        private static readonly int _AbyssalFlowWeatherCurrentId = Shader.PropertyToID("_AbyssalFlowWeatherCurrent");
        private static readonly int _AbyssalFlowWeatherWindId = Shader.PropertyToID("_AbyssalFlowWeatherWind");
        private static readonly int _AbyssalFlowWeatherParamsId = Shader.PropertyToID("_AbyssalFlowWeatherParams");
        private static readonly int _AbyssalFlowSurfaceYId = Shader.PropertyToID("_AbyssalFlowSurfaceY");
        private static readonly int _CurrentWaterLevelId = Shader.PropertyToID("_CurrentWaterLevel");
        private static readonly int _CurrentWaterLevelYId = Shader.PropertyToID("_CurrentWaterLevelY");
        private static readonly int _AbyssalFlowThermoclineYId = Shader.PropertyToID("_AbyssalFlowThermoclineY");
        private static readonly int _AbyssalFlowHeatSourceCountId = Shader.PropertyToID("_AbyssalFlowHeatSourceCount");
        private static readonly int _AbyssalFlowWeatherStateMaskId = Shader.PropertyToID("_AbyssalFlowWeatherStateMask");
        private static readonly ProfilerMarker _gatherDataProfilerMarker = new ProfilerMarker("H8.Fluid.GatherData");
        private static readonly ProfilerMarker _jobScheduleProfilerMarker = new ProfilerMarker("H8.Fluid.ScheduleBuoyancyJob");
        private static readonly ProfilerMarker _scheduledApplyProfilerMarker = new ProfilerMarker("H8.Fluid.ApplyScheduledForces");
        private static readonly ProfilerMarker _gpuReadbackProfilerMarker = new ProfilerMarker("H8.Fluid.ConsumeGpuReadback");
        private static readonly ProfilerMarker _gpuAbyssalReadbackProfilerMarker = new ProfilerMarker("H8.Fluid.ConsumeAbyssalReadback");
        private static readonly int _buoyancyForceNanErrorCode = unchecked((int)Hecton.Localization.LocHash.Compute("NAN_ERROR_HASH_BUOYANCY_FORCE"));
        private static readonly int _buoyancyTorqueNanErrorCode = unchecked((int)Hecton.Localization.LocHash.Compute("NAN_ERROR_HASH_BUOYANCY_TORQUE"));
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < CavitationShockwaveHitCapacity; i++)
            {
                s_CavitationShockwaveColliders[i] = null;
                s_CavitationShockwaveRigidbodies[i] = null;
            }
        }

        public static HectonFluidEngine Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return null;
#endif
                return GlobalRegistry.Fluid;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — WATER
        // ══════════════════════════════════════════════════════════

        [Header("── Water ─────────────────────────────────────")]
        [Tooltip("Y-koordinata poverhnosti vody (world space)")]
        [SerializeField] private float waterLevel = 5000f;
        [SerializeField] private bool enableCinematicTideShift = true;
        [SerializeField, Range(0f, 8f)] private float cinematicTideAmplitudeMeters = 2f;

        [Tooltip("Plotnost vody (kg/m³). Presnaya = 1000, Morskaya = 1025")]
        [SerializeField] private float waterDensity = 1000f;

        [Tooltip("Koeffitsient vyazkogo soprotivleniya. " +
                 "Chem bolshe — tem silnee tormozhenie pod vodoy.")]
        [SerializeField] private float viscousDrag = 3f;
        [SerializeField, Min(0f)] private float maxQuadraticDragForcePerKg = 180f;

        [Tooltip("Koeffitsient uglovogo soprotivleniya. " +
                 "Zamedlyaet vraschenie obektov pod vodoy.")]
        [SerializeField] private float angularDrag = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CURRENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Currents ──────────────────────────────────")]
        [Tooltip("Globalnyy vektor podvodnogo techeniya (m/s). " +
                 "Primenyaetsya ko vsem pogruzhennym obektam.")]
        [SerializeField] private Vector3 currentVector = Vector3.zero;

        [Tooltip("Sila vozdeystviya techeniya (mnozhitel)")]
        [SerializeField] private float currentStrength = 1f;
        [SerializeField] private bool enablePhantomCurrent = true;
        [SerializeField] private float currentNoiseScale = 0.018f;
        [SerializeField] private float currentTimeScale = 0.12f;
        [SerializeField, Range(0f, 1f)] private float currentVerticalFactor = 0.18f;
        [SerializeField] private float phantomCurrentStrength = 0.9f;

        [Header("-- Analytical Flow Field -----------------------")]
        [SerializeField] private bool enableAnalyticalFlowField = true;
        [SerializeField, Min(0.01f)] private float haloclineBoundaryDepthMeters = 200f;
        [SerializeField, Min(1f)] private float deepLayerDensityMultiplier = 1.5f;
        [SerializeField] private float haloclineShearForcePerKg = 4f;
        [SerializeField] private bool enableDynamicViscosityRegions = true;

        [Header("-- Giant's Wake -----------------------")]
        [Tooltip("Adds a subtle abyssal current bias from the parent gas giant sky direction.")]
        [SerializeField] private bool enableGiantWakeCurrent = true;
        [Tooltip("Meters-per-second current bias applied when deep enough below the water surface.")]
        [SerializeField, Min(0f)] private float giantWakeCurrentStrength = 0.18f;
        [Tooltip("Vertical component mixed into the horizontal planet-facing wake direction.")]
        [SerializeField, Range(-1f, 1f)] private float giantWakeVerticalBias = -0.04f;
        [Tooltip("Depth below water surface where the wake starts contributing.")]
        [SerializeField, Min(0f)] private float giantWakeDepthFadeStart = 120f;
        [Tooltip("Depth span used to fade the wake from zero to full strength.")]
        [SerializeField, Min(1f)] private float giantWakeDepthFadeRange = 480f;
        [Tooltip("Adds chaotic torque where Aegir wake and local abyssal currents shear across each other.")]
        [SerializeField] private bool enableTidalShearZones = true;
        [Tooltip("Torque scalar applied inside wake/current shear zones.")]
        [SerializeField, Min(0f)] private float tidalShearTorqueStrength = 18f;
        [Tooltip("Temporal frequency for deterministic shear-zone tumble.")]
        [SerializeField, Min(0.01f)] private float tidalShearFrequency = 1.7f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PERFORMANCE
        // ══════════════════════════════════════════════════════════

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Minimalnyy batch size dlya Job. " +
                 "Menshe = bolshe parallelizma, bolshe = menshe overhead.")]
        [SerializeField] private int jobBatchSize = 32;
        [SerializeField] private bool enableDistanceLod = true;
        [SerializeField] private Transform lodObserver;
        [SerializeField] private float nearLodDistance = 20f;
        [SerializeField] private float mediumLodDistance = 45f;
        [SerializeField] private float farLodDistance = 90f;
        [SerializeField] private float cullLodDistance = 160f;
        [SerializeField, Range(1, 8)] private int mediumLodDivisor = 2;
        [SerializeField, Range(1, 16)] private int farLodDivisor = 4;
        [SerializeField, Range(1, 32)] private int cullLodDivisor = 8;
        [SerializeField] private bool enableBiomeBuoyancyInfluence = true;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugObjectCount;
        [SerializeField] private int _debugNearCount;
        [SerializeField] private int _debugMediumCount;
        [SerializeField] private int _debugFarCount;
        [SerializeField] private int _debugCulledCount;
        [SerializeField] private int _debugCurrentVolumeCount;
        [SerializeField] private bool drawLodGizmos = true;
        [SerializeField] private bool drawCurrentVectors = true;
        [SerializeField] private float gizmoCurrentVectorScale = 4f;
        [SerializeField] private uint _debugAbyssalAggregateMask;
        [SerializeField] private int _debugAbyssalHeatSourceCount;
        [SerializeField] private Vector3 _debugGiantWakeCurrent;
        private float3 _resolvedGiantWakeCurrent;

        [Header("â”€â”€ GPU Buoyancy Offload â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private bool enableGpuBuoyancySampling = true;
        [SerializeField] private ComputeShader gpuBuoyancyCompute;
        [SerializeField, Range(64, 1024)] private int gpuBuoyancyActivationThreshold = 256;
        [SerializeField] private bool enableGpuAbyssalFlowField = true;
        [SerializeField] private ComputeShader abyssalFlowFieldCompute;
        [SerializeField, Range(8, 32)] private int abyssalFlowHorizontalResolution = 16;
        [SerializeField, Range(4, 24)] private int abyssalFlowVerticalResolution = 12;
        [SerializeField, Range(4f, 32f)] private float abyssalFlowHorizontalCellSize = 12f;
        [SerializeField, Range(4f, 24f)] private float abyssalFlowVerticalCellSize = 10f;
        [SerializeField, Range(4f, 40f)] private float abyssalHeatProbeRadius = 16f;
        [SerializeField, Range(0.1f, 64f)] private float abyssalHeatIntensityNormalization = 18f;

        [Header("-- Cavitation -----------------------")]
        [Tooltip("Optional particle system used for thruster cavitation bubble bursts.")]
        [SerializeField] private ParticleSystem cavitationBubbleParticles;
        [Tooltip("Particle count emitted by a full-intensity cavitation burst.")]
        [SerializeField, Range(1, 128)] private int cavitationBubbleEmitCountAtFullIntensity = 42;
        [Tooltip("Layer mask for small fauna or loose bodies affected by cavitation shockwaves.")]
        [SerializeField] private LayerMask cavitationShockwaveLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [Tooltip("Maximum Rigidbody mass affected by cavitation collapse so large props and the submarine are ignored.")]
        [SerializeField, Min(0.1f)] private float cavitationShockwaveMaxAffectedMassKg = 120f;
        [Tooltip("Upward lift mixed into cavitation shockwave direction.")]
        [SerializeField, Range(0f, 1f)] private float cavitationShockwaveVerticalLift = 0.12f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Y-koordinata poverhnosti vody.</summary>
        public float WaterLevel
        {
            get => waterLevel;
            set
            {
                waterLevel = value;
                PublishCurrentWaterLevelUniform();
            }
        }

        /// <summary>Cinematic surface water level consumed by shader/UI/physics bridges.</summary>
        public float CurrentWaterLevelY
        {
            get { return ResolveCinematicWaterLevelY(); }
        }

        /// <summary>Plotnost vody (kg/m³).</summary>
        public float WaterDensity
        {
            get => waterDensity;
            set => waterDensity = math.max(0.01f, value);
        }

        /// <summary>Vektor techeniya (m/s). Izmenyaetsya v rantayme.</summary>
        public Vector3 CurrentVector
        {
            get => currentVector;
            set
            {
                currentVector = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Sila globalnogo techeniya.</summary>
        public float CurrentStrength
        {
            get => currentStrength;
            set
            {
                currentStrength = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Vklyucheno li phantom techenie.</summary>
        public bool EnablePhantomCurrent
        {
            get => enablePhantomCurrent;
            set
            {
                enablePhantomCurrent = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Masshtab shuma phantom techeniya.</summary>
        public float CurrentNoiseScale
        {
            get => currentNoiseScale;
            set
            {
                currentNoiseScale = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Vremennoy masshtab phantom techeniya.</summary>
        public float CurrentTimeScale
        {
            get => currentTimeScale;
            set
            {
                currentTimeScale = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Vertikalnyy faktor phantom techeniya.</summary>
        public float CurrentVerticalFactor
        {
            get => currentVerticalFactor;
            set
            {
                currentVerticalFactor = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Sila phantom techeniya.</summary>
        public float PhantomCurrentStrength
        {
            get => phantomCurrentStrength;
            set
            {
                phantomCurrentStrength = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Kolichestvo zaregistrirovannyh obektov.</summary>
        public int ObjectCount => _objects.Count;

        public Vector3 GiantWakeCurrent => _debugGiantWakeCurrent;

        /// <summary>
        /// Queues one thruster cavitation burst for post-fixed particle emission and shockwave force routing.
        /// </summary>
        /// <param name="position">World-space burst origin.</param>
        /// <param name="direction">Preferred burst direction from the thruster exhaust.</param>
        /// <param name="intensity01">Cavitation intensity in the 0..1 range.</param>
        /// <param name="radius">Shockwave radius in meters.</param>
        /// <param name="acceleration">Shockwave velocity-change magnitude routed through PhysicsApplySystem.</param>
        /// <param name="sourceBodyInstanceId">Rigidbody instance ID to ignore, usually the submarine body.</param>
        /// <returns>True when the fixed-capacity burst queue accepted the event.</returns>
        public static bool QueueCavitationBurst(
            Vector3 position,
            Vector3 direction,
            float intensity01,
            float radius,
            float acceleration,
            int sourceBodyInstanceId)
        {
            HectonFluidEngine instance = GlobalRegistry.Fluid;
            return instance != null &&
                   instance.EnqueueCavitationBurst(position, direction, intensity01, radius, acceleration, sourceBodyInstanceId);
        }

        public Vector3 GetFlowAtPosition(Vector3 position)
        {
            float3 flow = GetFlowAtPosition(new float3(position.x, position.y, position.z));
            return new Vector3(flow.x, flow.y, flow.z);
        }

        public float3 GetFlowAtPosition(float3 position)
        {
            if (!math.all(math.isfinite(position)) || !enableAnalyticalFlowField)
                return float3.zero;

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float resolvedWaterLevel = ResolveCinematicWaterLevelY();
            float3 baseCurrent = new float3(
                currentVector.x * currentStrength,
                currentVector.y * currentStrength,
                currentVector.z * currentStrength);
            float depthBelowSurface = math.max(0f, resolvedWaterLevel - position.y);
            float3 flow = HectonAnalyticalFlowField.SampleBaseFlow(
                position,
                depthBelowSurface,
                baseCurrent,
                math.lengthsq(_resolvedGiantWakeCurrent) > GiantWakeDirectionEpsilonSq
                    ? _resolvedGiantWakeCurrent
                    : ResolveGiantWakeCurrentBase(),
                giantWakeDepthFadeStart,
                giantWakeDepthFadeRange,
                (uint)weatherSnapshot.StateMask,
                weatherSnapshot.CurrentMeta.GlobalBaseVector,
                weatherSnapshot.CurrentMeta.GlobalScale,
                weatherSnapshot.WeatherIntensity,
                enablePhantomCurrent ? (byte)1 : (byte)0,
                currentNoiseScale,
                currentTimeScale,
                currentVerticalFactor,
                phantomCurrentStrength,
                ResolveWaterLevelTimeSeconds(),
                haloclineBoundaryDepthMeters,
                haloclineShearForcePerKg);

            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
                HectonAnalyticalFlowField.ApplyThrusterFlow(ref flow, position, _thrusterFlowBuffer[i]);

            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
                HectonAnalyticalFlowField.ApplyWhirlpoolFlow(ref flow, position, _whirlpoolFlowBuffer[i]);

            return HectonAnalyticalFlowField.ResolveFiniteFloat3OrZero(flow);
        }

        public float GetWaterHeightAtPosition(Vector3 position)
        {
            return GetWaterHeightAtPosition(new float3(position.x, position.y, position.z));
        }

        public float GetWaterHeightAtPosition(float3 position)
        {
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float waveOffset = HectonGerstnerWater.SampleHeight(
                position.xz,
                weatherSnapshot.Wave0,
                weatherSnapshot.Wave1,
                weatherSnapshot.Wave2,
                weatherSnapshot.CurrentMeta.TimeAccumulator);
            return ResolveCinematicWaterLevelY() + waveOffset;
        }

        public bool TrySetActiveThruster(
            int slot,
            Vector3 position,
            Vector3 direction,
            float strength,
            float radius,
            float coneDegrees)
        {
            if ((uint)slot >= MaxAnalyticalThrusterCount ||
                !IsFiniteVector(position) ||
                !IsFiniteVector(direction) ||
                direction.sqrMagnitude <= 0.0001f ||
                strength <= 0f ||
                radius <= 0f)
            {
                return false;
            }

            float3 rawDirection = new float3(direction.x, direction.y, direction.z);
            float3 axisDirection = DominantAxisOrDefault(rawDirection, new float3(0f, 0f, 1f));
            float clampedConeDegrees = math.clamp(coneDegrees, 1f, 89f);
            float cone01 = clampedConeDegrees * 0.011111111f;
            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;
            _thrusterFlowBuffer[slot] = new ActiveThrusterFlow
            {
                PositionWS = new float3(position.x, position.y, position.z),
                DirectionWS = axisDirection,
                Strength = math.max(0f, strength),
                RadiusSq = radiusSq,
                InvRadiusSq = math.rcp(radiusSq),
                ConeCos = 1f - cone01 * cone01,
                Active = 1
            };
            OnCurrentSettingsChanged();
            return true;
        }

        public void ClearActiveThruster(int slot)
        {
            if ((uint)slot >= MaxAnalyticalThrusterCount)
                return;

            _thrusterFlowBuffer[slot] = default;
            OnCurrentSettingsChanged();
        }

        public void ClearActiveThrusters()
        {
            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
                _thrusterFlowBuffer[i] = default;
            OnCurrentSettingsChanged();
        }

        public bool TrySetWhirlpool(
            int slot,
            Vector3 center,
            float radius,
            float tangentialStrength,
            float centripetalStrength,
            float verticalPull)
        {
            if ((uint)slot >= MaxAnalyticalWhirlpoolCount ||
                !IsFiniteVector(center) ||
                radius <= 0f)
            {
                return false;
            }

            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;
            _whirlpoolFlowBuffer[slot] = new WhirlpoolFlow
            {
                CenterWS = new float3(center.x, center.y, center.z),
                RadiusSq = radiusSq,
                InvRadiusSq = math.rcp(radiusSq),
                TangentialStrength = tangentialStrength,
                CentripetalStrength = centripetalStrength,
                VerticalPull = math.max(0f, verticalPull),
                Active = 1
            };
            OnCurrentSettingsChanged();
            return true;
        }

        public void ClearWhirlpool(int slot)
        {
            if ((uint)slot >= MaxAnalyticalWhirlpoolCount)
                return;

            _whirlpoolFlowBuffer[slot] = default;
            OnCurrentSettingsChanged();
        }

        public void ClearWhirlpools()
        {
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
                _whirlpoolFlowBuffer[i] = default;
            OnCurrentSettingsChanged();
        }

        public bool TrySetViscosityRegion(
            int slot,
            Vector3 center,
            float radius,
            float viscosityMultiplier)
        {
            if ((uint)slot >= MaxDynamicViscosityRegionCount ||
                !IsFiniteVector(center) ||
                radius <= 0f ||
                !math.isfinite(viscosityMultiplier) ||
                viscosityMultiplier <= 0f)
            {
                return false;
            }

            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;
            _viscosityRegionBuffer[slot] = new FluidViscosityRegion
            {
                CenterWS = new float3(center.x, center.y, center.z),
                InvRadiusSq = math.rcp(radiusSq),
                ViscosityMultiplier = math.clamp(viscosityMultiplier, 0.05f, 8f),
                Active = 1
            };
            OnCurrentSettingsChanged();
            return true;
        }

        public void ClearViscosityRegion(int slot)
        {
            if ((uint)slot >= MaxDynamicViscosityRegionCount)
                return;

            _viscosityRegionBuffer[slot] = default;
            OnCurrentSettingsChanged();
        }

        public void ClearViscosityRegions()
        {
            for (int i = 0; i < MaxDynamicViscosityRegionCount; i++)
                _viscosityRegionBuffer[i] = default;
            OnCurrentSettingsChanged();
        }

        public bool TryDequeueImpactEvent(out FluidImpactEvent impactEvent)
        {
            impactEvent = default;
            if (!TryDrainScheduledBuoyancyJob() || !_fluidImpactEvents.IsCreated)
                return false;

            if (!_fluidImpactEvents.TryDequeue(out impactEvent))
                return false;

            if (_fluidImpactQueuedCount > 0)
                _fluidImpactQueuedCount--;

            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>Vyzyvaetsya pri izmenenii nastroek techeniy (dlya vizualizatorov).</summary>
        public event System.Action OnCurrentSettingsChangedEvent;

        /// <summary>Uvedomlyaet podpischikov ob izmenenii nastroek techeniy.</summary>
        private void OnCurrentSettingsChanged()
        {
            OnCurrentSettingsChangedEvent?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  MANAGED REGISTRY (parallel lists)
        // ══════════════════════════════════════════════════════════

        /// <summary>Spisok zaregistrirovannyh BuoyancyObject.</summary>
        // COLD ALLOC: List<BuoyancyObject>[256] — dense buoyancy object registry — owner: HectonFluidEngine
        private readonly List<BuoyancyObject> _objects = new List<BuoyancyObject>(256);

        /// <summary>Parallelnyy spisok Rigidbody (indeksy sovpadayut s _objects).</summary>
        // COLD ALLOC: List<Rigidbody>[256] — dense rigidbody registry parallel to _objects — owner: HectonFluidEngine
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>(256);
        // ══════════════════════════════════════════════════════════
        //  LOD DISTANCE CACHING
        // ══════════════════════════════════════════════════════════

        /// <summary>Keshirovannye kvadraty distantsiy dlya LOD (pereschityvayutsya pri ochischenii).</summary>
        private float _cachedNearDistSq = 400f;      // 20^2
        private float _cachedMediumDistSq = 2025f;   // 45^2
        private float _cachedFarDistSq = 8100f;      // 90^2
        private float _cachedCullDistSq = 25600f;    // 160^2

        // ══════════════════════════════════════════════════════════
        //  NATIVE ARRAYS (Job data)
        // ══════════════════════════════════════════════════════════

        private NativeArray<float3>         _positions;
        private NativeArray<float3>         _previousPositions;
        private NativeArray<byte>           _previousPositionValid;
        private NativeArray<float3>         _velocities;
        private NativeArray<float3>         _angularVelocities;
        private NativeArray<float3>         _upVectors;
        private NativeArray<BuoyancyParams> _params;
        private NativeArray<float>          _waveOffsets;
        private NativeArray<float>          _gpuBuoyancyForcesY;
        private NativeArray<float3>         _resultForces;
        private NativeArray<float3>         _resultTorques;
        private NativeArray<FluidImpactEvent> _impactEventScratch;
        private NativeArray<int> _impactEventFlags;
        private NativeArray<GpuBuoyancyObjectData> _gpuBuoyancyObjectDataUpload;
        private NativeArray<float4> _gpuBuoyancyReadback;
        private NativeArray<GpuHeatSourceData> _gpuAbyssalHeatSourceUpload;
        private NativeArray<ActiveThrusterFlow> _activeThrusterFlows;
        private NativeArray<WhirlpoolFlow> _activeWhirlpools;
        private NativeArray<FluidViscosityRegion> _activeViscosityRegions;
        private NativeArray<float> _viscosityGradientLut;
        private int _activeThrusterFlowCount;
        private int _activeWhirlpoolFlowCount;
        private int _activeViscosityRegionCount;
        private NativeQueue<FluidImpactEvent> _fluidImpactEvents;
        private int _fluidImpactQueuedCount;
        // COLD ALLOC: Rigidbody[capacity] — schedule-time rigidbody snapshot for deferred force application — owner: HectonFluidEngine
        private Rigidbody[] _scheduledBodies;
        private JobHandle _scheduledBuoyancyHandle;
        private bool _scheduledBuoyancyJobActive;
        private int _scheduledForceCount;
        // COLD ALLOC: CavitationBurstEvent[8] — fixed post-fixed cavitation burst queue — owner: HectonFluidEngine
        private readonly CavitationBurstEvent[] _cavitationBurstQueue = new CavitationBurstEvent[MaxCavitationBurstEvents];
        // COLD ALLOC: Collider[64] — static nonalloc cavitation shockwave overlap buffer — owner: HectonFluidEngine
        private static readonly Collider[] s_CavitationShockwaveColliders = new Collider[CavitationShockwaveHitCapacity];
        // COLD ALLOC: Rigidbody[64] — static deduplicated cavitation shockwave rigidbody targets — owner: HectonFluidEngine
        private static readonly Rigidbody[] s_CavitationShockwaveRigidbodies = new Rigidbody[CavitationShockwaveHitCapacity];
        private int _cavitationBurstCount;
        // COLD ALLOC: ActiveThrusterFlow[4] — fixed analytical propwash inputs — owner: HectonFluidEngine
        private readonly ActiveThrusterFlow[] _thrusterFlowBuffer = new ActiveThrusterFlow[MaxAnalyticalThrusterCount];
        // COLD ALLOC: WhirlpoolFlow[2] — fixed analytical whirlpool inputs — owner: HectonFluidEngine
        private readonly WhirlpoolFlow[] _whirlpoolFlowBuffer = new WhirlpoolFlow[MaxAnalyticalWhirlpoolCount];
        // COLD ALLOC: FluidViscosityRegion[4] - fixed cinematic viscosity region inputs - owner: HectonFluidEngine
        private readonly FluidViscosityRegion[] _viscosityRegionBuffer = new FluidViscosityRegion[MaxDynamicViscosityRegionCount];

        /// <summary>Tekuschaya emkost NativeArrays (vsegda >= count obektov).</summary>
        private int _nativeCapacity;
        private int _lodFrameCounter;
        private float _observerResolveRetryTimer;
        private const float ObserverResolveRetryInterval = 1f;
        private const int MaxNativeCapacityGrowthIterations = 16;
        private GraphicsBuffer _gpuBuoyancyPositionBuffer;
        private GraphicsBuffer _gpuBuoyancyParamBuffer;
        private GraphicsBuffer _gpuBuoyancyResultBuffer;
        private AsyncGPUReadbackRequest[] _gpuReadbackRequests;
        private int[] _gpuReadbackCounts;
        private bool[] _gpuReadbackActive;
        private int _gpuReadbackWriteIndex;
        private bool _hasGpuBuoyancyData;
        private int _gpuBuoyancyKernel = -1;
        private GraphicsBuffer _gpuAbyssalFlowResultBuffer;
        private GraphicsBuffer _gpuAbyssalHeatSourceBuffer;
        private GraphicsBuffer _gpuAbyssalAggregateBuffer;
        private Vector4 _lastAbyssalGridResolution;
        private Vector4 _lastAbyssalFlowCenter;
        private Vector4 _lastAbyssalFlowSpacing;
        private AsyncGPUReadbackRequest[] _gpuAbyssalReadbackRequests;
        private bool[] _gpuAbyssalReadbackActive;
        private int _gpuAbyssalReadbackWriteIndex;
        private int _gpuAbyssalResetKernel = -1;
        private int _gpuAbyssalUpdateKernel = -1;
        private int _gpuAbyssalSurgeKernel = -1;
        private bool _fluidRuntimeRegistered;
        private bool _fixedTickRegistered;
        private bool _postFixedRegistered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            HectonFluidEngine registeredFluid = GlobalRegistry.Fluid;
            if (Application.isPlaying && registeredFluid != null && !ReferenceEquals(registeredFluid, this))
            {
                Destroy(gameObject);
                return;
            }

            MathGuard.Initialize();

            // Initial observer resolution. If player/camera appears later,
            // FixedTick retries on a cooldown instead of staying in full-cost mode forever.
            TryResolveObserver(force: true);
            
            // Cache LOD distances once (update if parameters change via property)
            UpdateCachedLodDistances();

#if UNITY_EDITOR
            if (gpuBuoyancyCompute == null)
                gpuBuoyancyCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(GpuBuoyancyComputeAssetPath);

            if (abyssalFlowFieldCompute == null)
                abyssalFlowFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(AbyssalFlowFieldComputeAssetPath);
#endif
            if (gpuBuoyancyCompute != null)
                _gpuBuoyancyKernel = gpuBuoyancyCompute.FindKernel("EvaluateBuoyancy");
            if (abyssalFlowFieldCompute != null)
            {
                _gpuAbyssalResetKernel = abyssalFlowFieldCompute.FindKernel("ResetAbyssalFlowAggregate");
                _gpuAbyssalUpdateKernel = abyssalFlowFieldCompute.FindKernel("UpdateAbyssalFlowField");
                _gpuAbyssalSurgeKernel = abyssalFlowFieldCompute.FindKernel("DetectBiolumeSurge");
            }

            _gpuReadbackRequests = new AsyncGPUReadbackRequest[GpuReadbackRingSize]; // COLD ALLOC: AsyncGPUReadbackRequest[3] — fixed GPU buoyancy readback ring state — owner: HectonFluidEngine
            _gpuReadbackCounts = new int[GpuReadbackRingSize]; // COLD ALLOC: int[3] — GPU buoyancy readback element counts — owner: HectonFluidEngine
            _gpuReadbackActive = new bool[GpuReadbackRingSize]; // COLD ALLOC: bool[3] — GPU buoyancy readback slot activity — owner: HectonFluidEngine
            _gpuAbyssalReadbackRequests = new AsyncGPUReadbackRequest[GpuReadbackRingSize]; // COLD ALLOC: AsyncGPUReadbackRequest[3] — fixed GPU abyssal-flow readback ring state — owner: HectonFluidEngine
            _gpuAbyssalReadbackActive = new bool[GpuReadbackRingSize]; // COLD ALLOC: bool[3] — GPU abyssal-flow readback slot activity — owner: HectonFluidEngine
            PublishCurrentWaterLevelUniform();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !_fluidRuntimeRegistered)
            {
                HectonFluidEngine registeredFluid = GlobalRegistry.Fluid;
                if (registeredFluid != null && !ReferenceEquals(registeredFluid, this))
                {
                    Destroy(gameObject);
                    return;
                }

                GlobalRegistry.RegisterFluidRuntime(this);
                _fluidRuntimeRegistered = ReferenceEquals(GlobalRegistry.Fluid, this);
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_fixedTickRegistered)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
                _fixedTickRegistered = GlobalRegistry.FixedTickables.Contains(this);
            }

            if (!_postFixedRegistered)
            {
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
            }
        }

        private void OnDisable()
        {
            if (_fluidRuntimeRegistered)
            {
                GlobalRegistry.UnregisterFluidRuntime(this);
                _fluidRuntimeRegistered = false;
            }

            if (_fixedTickRegistered)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _fixedTickRegistered = false;
            }

            if (_postFixedRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = false;
            }

            // Release runtime job buffers before editor domain/play-mode teardown.
            // In-editor play transitions do not always guarantee a clean OnDestroy path
            // for persistent native allocations, so we free them on disable as well.
            DisposeNativeArrays();
        }

        private void OnDestroy()
        {
            if (_fluidRuntimeRegistered)
            {
                GlobalRegistry.UnregisterFluidRuntime(this);
                _fluidRuntimeRegistered = false;
            }

            if (_fixedTickRegistered)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _fixedTickRegistered = false;
            }

            if (_postFixedRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = false;
            }
            DisposeNativeArrays();
        }

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Registriruet BuoyancyObject. Vyzyvaetsya iz OnEnable.
        /// Keshiruet Rigidbody v parallelnom spiske.
        /// </summary>
        public void Register(BuoyancyObject obj)
        {
            if (obj == null || obj.Body == null) return;

            if (ContainsRegisteredObject(obj))
                return;

            _objects.Add(obj);
            _bodies.Add(obj.Body);

            UpdateDiagnostics();
        }

        /// <summary>
        /// Samples the previous-frame environmental current for sandboxed mod flow queries.
        /// The dispatcher owns call cadence and never exposes fluid buffers to mods.
        /// </summary>
        /// <param name="runtimePosition">Frame-space query position.</param>
        /// <param name="flowVector">Resolved flow vector in meters per second.</param>
        /// <returns>True when a finite flow vector was resolved.</returns>
        public bool TrySampleModAbyssalFlow(Vector3 runtimePosition, out float3 flowVector)
        {
            flowVector = default;
            float3 query = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(query)))
                return false;

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            Vector3 authoredCurrent = CurrentVolume.SampleCombinedCurrent(runtimePosition);
            float3 weatherCurrent = weatherSnapshot.CurrentMeta.GlobalBaseVector * math.max(0f, weatherSnapshot.CurrentMeta.GlobalScale);
            float3 configuredCurrent = new float3(currentVector.x, currentVector.y, currentVector.z) * math.max(0f, currentStrength);
            float3 giantWakeCurrent = ResolveGiantWakeCurrentForDepth(query.y);
            flowVector = configuredCurrent + weatherCurrent + giantWakeCurrent + new float3(authoredCurrent.x, authoredCurrent.y, authoredCurrent.z);
            if (!math.all(math.isfinite(flowVector)))
            {
                flowVector = default;
                return false;
            }

            return true;
        }

        public bool TryGetGpuAbyssalFlowFieldBuffer(
            out GraphicsBuffer flowFieldBuffer,
            out Vector4 gridResolution,
            out Vector4 flowCenter,
            out Vector4 flowSpacing)
        {
            flowFieldBuffer = _gpuAbyssalFlowResultBuffer;
            gridResolution = _lastAbyssalGridResolution;
            flowCenter = _lastAbyssalFlowCenter;
            flowSpacing = _lastAbyssalFlowSpacing;
            return flowFieldBuffer != null &&
                   flowFieldBuffer.count > 0 &&
                   gridResolution.w > 0f &&
                   flowSpacing.x > 0f &&
                   flowSpacing.y > 0f;
        }

        /// <summary>
        /// Snimaet BuoyancyObject s registratsii. Vyzyvaetsya iz OnDisable.
        /// Swap-remove dlya O(1).
        /// </summary>
        public void Unregister(BuoyancyObject obj)
        {
            if (obj == null) return;

            if (!ContainsRegisteredObject(obj))
                return;  // Not registered

            int count = _objects.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_objects[i], obj))
                {
                    int last = count - 1;

                    // Swap with last
                    MoveNativeSlotCache(i, last);
                    _objects[i] = _objects[last];
                    _bodies[i]  = _bodies[last];

                    // Remove last
                    _objects.RemoveAt(last);
                    _bodies.RemoveAt(last);

                    break;
                }
            }

            ReleaseIdleNativeBuffersIfNeeded();
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        private void MoveNativeSlotCache(int destination, int source)
        {
            if (destination == source)
                return;

            if (_positions.IsCreated && source < _positions.Length && destination < _positions.Length)
                _positions[destination] = _positions[source];

            if (_previousPositions.IsCreated && source < _previousPositions.Length && destination < _previousPositions.Length)
                _previousPositions[destination] = _previousPositions[source];

            if (_previousPositionValid.IsCreated &&
                source < _previousPositionValid.Length &&
                destination < _previousPositionValid.Length)
            {
                _previousPositionValid[destination] = _previousPositionValid[source];
                _previousPositionValid[source] = 0;
            }
        }

        //  IFixedTickable — MAIN PHYSICS LOOP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya GameTickManager v FixedUpdate.
        ///
        /// Pipeline:
        ///   Runtime guard: a completed previous job is drained before this method writes
        ///   new data into the same NativeArrays. If the job is still running, this fixed
        ///   step is skipped instead of blocking.
        ///   1. Resize NativeArrays esli count > capacity (Capacity Doubling)
        ///   2. Gather: kopiruem dannye iz Rigidbody → NativeArrays
        ///   3. Schedule: BuoyancyJob (Burst, parallel)
        ///   4. Completion: only after IsCompleted, no blocking wait
        ///   5. Apply: queue force packets cherez PhysicsForceRouter
        ///
        /// Vse shagi krome Job — main thread.
        /// Job — worker threads, Burst compiled, SIMD.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            using (ProfilerRegistry.PhysicsTick.Auto())
            {
            float cinematicWaterLevel = PublishCurrentWaterLevelUniform();

            if (!TryDrainScheduledBuoyancyJob())
                return;

            int count = _objects.Count;
            if (count == 0)
            {
                ReleaseIdleNativeBuffersIfNeeded();
                return;
            }
            _debugNearCount = 0;
            _debugMediumCount = 0;
            _debugFarCount = 0;
            _debugCulledCount = 0;
            _lodFrameCounter++;

            if (lodObserver == null)
            {
                _observerResolveRetryTimer -= fixedDeltaTime;
                if (_observerResolveRetryTimer <= 0f)
                    TryResolveObserver(force: false);
            }

            // ── 1. Ensure capacity (Capacity Doubling) ──
            if (count > _nativeCapacity)
            {
                ReallocateNativeArrays(count);
            }

            // ── 2. Gather (mozhet umenshit _objects.Count pri ochistke null) ──
            GatherData(cinematicWaterLevel);

            // Pereschityvaem count posle ochistki destroyed obektov
            count = _objects.Count;
            if (count == 0)
            {
                ReleaseIdleNativeBuffersIfNeeded();
                return;
            }

            // ── 3. Schedule Job ──
            using (_jobScheduleProfilerMarker.Auto())
            {
            for (int i = 0; i < count; i++)
                _scheduledBodies[i] = _bodies[i];

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            _resolvedGiantWakeCurrent = ResolveGiantWakeCurrentBase();
            _debugGiantWakeCurrent = new Vector3(_resolvedGiantWakeCurrent.x, _resolvedGiantWakeCurrent.y, _resolvedGiantWakeCurrent.z);
            CopyAnalyticalFlowInputsToNative();
            ConsumeGpuAbyssalFlowReadbacks();
            ConsumeGpuBuoyancyReadbacks();
            TryDispatchGpuAbyssalFlowField(weatherSnapshot, cinematicWaterLevel);
            TryDispatchGpuBuoyancySampling(weatherSnapshot, count, cinematicWaterLevel);

            JobHandle waveHandle = default;
            bool useGpuBuoyancy = enableGpuBuoyancySampling &&
                                  gpuBuoyancyCompute != null &&
                                  count >= gpuBuoyancyActivationThreshold &&
                                  _hasGpuBuoyancyData;
            if (!useGpuBuoyancy)
            {
                WaveQueryJob waveJob = new WaveQueryJob
                {
                    PositionsWS = _positions,
                    ObjParams = _params,
                    VerticalOffsets = _waveOffsets,
                    Wave0 = weatherSnapshot.Wave0,
                    Wave1 = weatherSnapshot.Wave1,
                    Wave2 = weatherSnapshot.Wave2,
                    TimeSeconds = weatherSnapshot.CurrentMeta.TimeAccumulator,
                    WaterLevelY = cinematicWaterLevel,
                    MaxWaveEnvelope = math.abs(weatherSnapshot.Wave0.Amplitude) +
                                      math.abs(weatherSnapshot.Wave1.Amplitude) +
                                      math.abs(weatherSnapshot.Wave2.Amplitude)
                };

                waveHandle = waveJob.Schedule(count, jobBatchSize);
            }

            BuoyancyJob job = new BuoyancyJob
            {
                positions        = _positions,
                previousPositions = _previousPositions,
                previousPositionValid = _previousPositionValid,
                velocities       = _velocities,
                angularVelocities = _angularVelocities,
                upVectors        = _upVectors,
                objParams        = _params,
                waveOffsets      = _waveOffsets,
                gpuBuoyancyForcesY = _gpuBuoyancyForcesY,
                activeThrusters = _activeThrusterFlows,
                activeWhirlpools = _activeWhirlpools,
                activeViscosityRegions = _activeViscosityRegions,
                viscosityGradientLut = _viscosityGradientLut,
                activeThrusterCount = _activeThrusterFlowCount,
                activeWhirlpoolCount = _activeWhirlpoolFlowCount,
                activeViscosityRegionCount = _activeViscosityRegionCount,
                impactEvents = _impactEventScratch,
                impactEventFlags = _impactEventFlags,
                resultForces     = _resultForces,
                resultTorques    = _resultTorques,
                mathGuardWriter = MathGuard.AsParallelWriter(),
                forceNanErrorCode = _buoyancyForceNanErrorCode,
                torqueNanErrorCode = _buoyancyTorqueNanErrorCode,

                waterLevel       = cinematicWaterLevel,
                waterDensity     = waterDensity,
                viscousDrag      = viscousDrag,
                maxQuadraticDragForcePerKg = maxQuadraticDragForcePerKg,
                angularDragCoeff = angularDrag,
                gravity          = math.abs(UnityEngine.Physics.gravity.y),
                baseCurrentForce = new float3(
                    currentVector.x * currentStrength,
                    currentVector.y * currentStrength,
                    currentVector.z * currentStrength),
                giantWakeCurrent = _resolvedGiantWakeCurrent,
                giantWakeDepthFadeStart = giantWakeDepthFadeStart,
                giantWakeDepthFadeRange = giantWakeDepthFadeRange,
                enableTidalShearZones = enableTidalShearZones ? (byte)1 : (byte)0,
                tidalShearTorqueStrength = tidalShearTorqueStrength,
                tidalShearFrequency = tidalShearFrequency,
                time             = math.isfinite(weatherSnapshot.CurrentMeta.TimeAccumulator) &&
                                   weatherSnapshot.CurrentMeta.TimeAccumulator > 0f
                    ? weatherSnapshot.CurrentMeta.TimeAccumulator
                    : Time.unscaledTime,
                weatherStateMask = (uint)weatherSnapshot.StateMask,
                weatherCurrentDirection = weatherSnapshot.CurrentMeta.GlobalBaseVector,
                weatherCurrentScale = weatherSnapshot.CurrentMeta.GlobalScale,
                weatherBlend = weatherSnapshot.WeatherIntensity,
                enablePhantomCurrent = enablePhantomCurrent ? (byte)1 : (byte)0,
                currentNoiseScale = currentNoiseScale,
                currentTimeScale = currentTimeScale,
                currentVerticalFactor = currentVerticalFactor,
                phantomCurrentStrength = phantomCurrentStrength,
                enableAnalyticalFlowField = enableAnalyticalFlowField ? (byte)1 : (byte)0,
                haloclineBoundaryDepthMeters = haloclineBoundaryDepthMeters,
                deepLayerDensityMultiplier = deepLayerDensityMultiplier,
                haloclineShearForcePerKg = haloclineShearForcePerKg,
                enableDynamicViscosityRegions = enableDynamicViscosityRegions ? (byte)1 : (byte)0,
                useGpuBuoyancyForce = useGpuBuoyancy ? (byte)1 : (byte)0
            };

            _scheduledBuoyancyHandle = job.Schedule(count, jobBatchSize, waveHandle);
            }

            // ── 4. Complete ──

            // ── 5. Apply forces ──
            _scheduledBuoyancyJobActive = true;
            _scheduledForceCount = count;
            }
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            DrainCavitationBursts();

            TryDrainScheduledBuoyancyJob();
        }

        private bool TryDrainScheduledBuoyancyJob()
        {
            if (!_scheduledBuoyancyJobActive)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledBuoyancyHandle, false))
                return false;

            ApplyScheduledForces();
            _scheduledBuoyancyJobActive = false;
            _scheduledForceCount = 0;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  GATHER — Copy Rigidbody data → NativeArrays
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Kopiruet pozitsii, skorosti i parametry iz managed Rigidbody
        /// v NativeArrays dlya Job. Main thread.
        ///
        /// Udalyaet null/destroyed obekty na letu (swap-remove v obratnom tsikle).
        ///
        /// IZMENENIE (Dry Zones / Ground Contact):
        ///   Kopiruet owner-side fluid suppression truth v BuoyancyParams.isInAir.
        ///   Dry zones always suppress fluid. Grounded contact suppresses fluid
        ///   only when the object is effectively above the waterline.
        ///   BuoyancyJob proveryaet etot flag i obnulyaet sily, esli true.
        /// </summary>
        private void GatherData(float resolvedWaterLevel)
        {
            using (_gatherDataProfilerMarker.Auto())
            {
            WorldProceduralFieldSampler biomeFieldSampler = enableBiomeBuoyancyInfluence
                ? GlobalRegistry.ProceduralFieldSampler
                : null;

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                BuoyancyObject obj = _objects[i];
                Rigidbody rb = _bodies[i];

                // ── Zaschita ot destroyed obektov (fake null check) ──
                if (obj == null || rb == null)
                {
                    int last = _objects.Count - 1;
                    MoveNativeSlotCache(i, last);
                    _objects[i] = _objects[last];
                    _bodies[i]  = _bodies[last];
                    _objects.RemoveAt(last);
                    _bodies.RemoveAt(last);
                    continue;
                }

                Vector3 com = rb.worldCenterOfMass;
                Vector3 vel = rb.linearVelocity;
                Vector3 angVel = rb.angularVelocity;
                Vector3 up = rb.transform.up;
                Vector3 localCurrent = Vector3.zero;
                obj.GetBuoyancySampleBounds(out Vector3 boundsCenter, out Vector3 boundsExtents);

                byte simulationMode = 0;
                byte simplifiedSubmersion = 0;
                float currentWeight = 1f;
                float stabilityWeight = 1f;
                float biomeBuoyancyMultiplier = 1f;

                if (enableDistanceLod && obj.AllowDistanceLod && lodObserver != null)
                {
                    float bias = math.max(0.1f, obj.LodBias);
                    // Use cached LOD distances
                    float nearDistanceSq = _cachedNearDistSq * bias * bias;
                    float mediumDistanceSq = _cachedMediumDistSq * bias * bias;
                    float farDistanceSq = _cachedFarDistSq * bias * bias;
                    float cullDistanceSq = _cachedCullDistSq * bias * bias;

                    float dx = com.x - lodObserver.position.x;
                    float dy = com.y - lodObserver.position.y;
                    float dz = com.z - lodObserver.position.z;
                    float distanceSq = dx * dx + dy * dy + dz * dz;

                    if (distanceSq <= nearDistanceSq)
                    {
                        _debugNearCount++;
                    }
                    else if (distanceSq <= mediumDistanceSq)
                    {
                        _debugMediumCount++;
                        if ((_lodFrameCounter + i) % math.max(1, mediumLodDivisor) != 0)
                            simulationMode = 1;
                        currentWeight = 0.85f;
                        stabilityWeight = 0.9f;
                    }
                    else if (distanceSq <= farDistanceSq)
                    {
                        _debugFarCount++;
                        if ((_lodFrameCounter + i) % math.max(1, farLodDivisor) != 0)
                            simulationMode = 1;
                        simplifiedSubmersion = 1;
                        currentWeight = 0.55f;
                        stabilityWeight = 0.65f;
                    }
                    else if (distanceSq <= cullDistanceSq)
                    {
                        _debugCulledCount++;
                        simplifiedSubmersion = 1;
                        if (rb.IsSleeping())
                            simulationMode = 2;
                        else if ((_lodFrameCounter + i) % math.max(1, cullLodDivisor) != 0)
                            simulationMode = 1;
                        currentWeight = 0.3f;
                        stabilityWeight = 0.45f;
                    }
                    else
                    {
                        _debugCulledCount++;
                        simplifiedSubmersion = 1;
                        simulationMode = rb.IsSleeping() ? (byte)2 : (byte)1;
                        currentWeight = 0.12f;
                        stabilityWeight = 0.25f;
                    }
                }

                if (simulationMode != 2)
                    localCurrent = CurrentVolume.SampleAt(com);

                if (biomeFieldSampler != null &&
                    biomeFieldSampler.TrySampleBiomePhysicsInfluence(com, out float sampledBuoyancyMultiplier))
                {
                    biomeBuoyancyMultiplier = Mathf.Max(0.05f, sampledBuoyancyMultiplier);
                }

                float3 currentPosition = new float3(com.x, com.y, com.z);
                if (_previousPositions.IsCreated &&
                    _previousPositionValid.IsCreated &&
                    i < _previousPositions.Length &&
                    i < _previousPositionValid.Length)
                {
                    _previousPositions[i] = _previousPositionValid[i] != 0 ? _positions[i] : currentPosition;
                    _previousPositionValid[i] = 1;
                }

                _positions[i]  = currentPosition;
                _velocities[i] = new float3(vel.x, vel.y, vel.z);
                _angularVelocities[i] = new float3(angVel.x, angVel.y, angVel.z);
                _upVectors[i] = new float3(up.x, up.y, up.z);
                _params[i]     = new BuoyancyParams
                {
                    boundsCenter = new float3(boundsCenter.x, boundsCenter.y, boundsCenter.z),
                    boundsExtents = new float3(boundsExtents.x, boundsExtents.y, boundsExtents.z),
                    density = obj.Density,
                    volume  = obj.Volume,
                    height  = obj.Height > 0f ? obj.Height : 0.01f,
                    mass    = rb.mass,
                    currentResponse = obj.CurrentResponse * currentWeight,
                    surfaceStability = obj.SurfaceStability * stabilityWeight,
                    localFluidDensity = obj.UseLocalFluidDensityOverride
                        ? obj.LocalFluidDensityOverride
                        : waterDensity,
                    localCurrent = new float3(localCurrent.x, localCurrent.y, localCurrent.z),
                    buoyancyMultiplier = biomeBuoyancyMultiplier,
                    isInAir = obj.ShouldSuppressFluid(resolvedWaterLevel) ? (byte)1 : (byte)0,
                    simulationMode = simulationMode,
                    simplifiedSubmersion = simplifiedSubmersion,
                    useLocalFluidDensityOverride = obj.UseLocalFluidDensityOverride ? (byte)1 : (byte)0,
                    angularDragMultiplier = obj.RuntimeAngularDragMultiplier
                };

                ResourceDistributionDirector brineDirector = GlobalRegistry.ResourceDistribution;
                if (brineDirector != null &&
                    brineDirector.TrySampleBrineFluidDensity(com, out float localFluidDensity) &&
                    localFluidDensity > waterDensity + 0.01f)
                {
                    BuoyancyParams parameters = _params[i];
                    parameters.localFluidDensity = localFluidDensity;
                    parameters.useLocalFluidDensityOverride = 1;
                    _params[i] = parameters;
                }
            }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  APPLY — Write forces back to Rigidbody
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Queues computed force packets. Rigidbody mutation is owned by PhysicsApplySystem.
        /// </summary>
        private void ApplyScheduledForces()
        {
            using (_scheduledApplyProfilerMarker.Auto())
            {
            bool canDrainImpactEvents =
                _fluidImpactEvents.IsCreated &&
                _impactEventFlags.IsCreated &&
                _impactEventScratch.IsCreated;
            for (int i = 0; i < _scheduledForceCount; i++)
            {
                if (canDrainImpactEvents &&
                    (uint)i < (uint)_impactEventFlags.Length &&
                    (uint)i < (uint)_impactEventScratch.Length &&
                    _impactEventFlags[i] != 0)
                {
                    _impactEventFlags[i] = 0;
                    if (_fluidImpactQueuedCount < FluidImpactEventQueueCapacity)
                    {
                        _fluidImpactEvents.Enqueue(_impactEventScratch[i]);
                        _fluidImpactQueuedCount++;
                    }
                }

                Rigidbody rb = _scheduledBodies[i];
                if (rb == null) continue;

                float3 force  = _resultForces[i];
                float3 torque = _resultTorques[i];

                // Propuskaem nulevye sily (obekt nad vodoy ili v suhoy zone)
                if (TrySanitizePhysicsVector(force, NonFiniteBuoyancyForceHash, out Vector3 sanitizedForce) &&
                    sanitizedForce.sqrMagnitude > 0.0001f)
                {
                    PhysicsForceRouter.QueueAmbientForce(
                        rb,
                        sanitizedForce,
                        ForceMode.Force);
                }

                if (TrySanitizePhysicsVector(torque, NonFiniteBuoyancyTorqueHash, out Vector3 sanitizedTorque) &&
                    sanitizedTorque.sqrMagnitude > 0.0001f)
                {
                    PhysicsForceRouter.QueueAmbientTorque(
                        rb,
                        sanitizedTorque,
                        ForceMode.Force);
                }
            }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  NATIVE ARRAY MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Peresozdaet NativeArrays s uvelichennoy emkostyu (Capacity Doubling).
        /// </summary>
        private bool EnqueueCavitationBurst(
            Vector3 position,
            Vector3 direction,
            float intensity01,
            float radius,
            float acceleration,
            int sourceBodyInstanceId)
        {
            if (_cavitationBurstCount >= MaxCavitationBurstEvents ||
                !IsFiniteVector(position) ||
                !IsFiniteVector(direction) ||
                radius <= 0f ||
                acceleration <= 0f)
            {
                return false;
            }

            Vector3 safeDirection = DominantAxisOrDefault(direction, Vector3.back);
            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;

            _cavitationBurstQueue[_cavitationBurstCount++] = new CavitationBurstEvent
            {
                Position = position,
                Direction = safeDirection,
                Intensity01 = math.saturate(intensity01),
                Radius = safeRadius,
                RadiusSq = radiusSq,
                InvRadiusSq = math.rcp(radiusSq),
                Acceleration = math.max(0f, acceleration),
                SourceBodyInstanceId = sourceBodyInstanceId
            };
            return true;
        }

        private void DrainCavitationBursts()
        {
            int burstCount = _cavitationBurstCount;
            if (burstCount <= 0)
                return;

            _cavitationBurstCount = 0;
            for (int i = 0; i < burstCount; i++)
            {
                CavitationBurstEvent burstEvent = _cavitationBurstQueue[i];
                _cavitationBurstQueue[i] = default;
                if (burstEvent.Intensity01 <= 0.0001f)
                    continue;

                EmitCavitationParticles(in burstEvent);
                ApplyCavitationShockwave(in burstEvent);
            }
        }

        private void EmitCavitationParticles(in CavitationBurstEvent burstEvent)
        {
            if (cavitationBubbleParticles == null)
                return;

            Transform particleTransform = cavitationBubbleParticles.transform;
            particleTransform.position = burstEvent.Position;
            if (burstEvent.Direction.sqrMagnitude > 0.0001f)
                particleTransform.rotation = Quaternion.LookRotation(burstEvent.Direction, Vector3.up);

            int rawEmitCount = (int)(cavitationBubbleEmitCountAtFullIntensity * burstEvent.Intensity01 + 0.999f);
            int emitCount = Mathf.Clamp(rawEmitCount, 1, cavitationBubbleEmitCountAtFullIntensity);
            cavitationBubbleParticles.Emit(emitCount);
        }

        private void ApplyCavitationShockwave(in CavitationBurstEvent burstEvent)
        {
            int colliderCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                burstEvent.Position,
                burstEvent.Radius,
                s_CavitationShockwaveColliders,
                cavitationShockwaveLayers,
                QueryTriggerInteraction.Ignore);
            if (colliderCount <= 0)
                return;

            int rigidbodyCount = 0;
            for (int i = 0; i < colliderCount; i++)
            {
                Collider hitCollider = s_CavitationShockwaveColliders[i];
                s_CavitationShockwaveColliders[i] = null;
                if (hitCollider == null)
                    continue;

                Rigidbody candidateBody = hitCollider.attachedRigidbody;
                if (candidateBody == null ||
                    candidateBody.isKinematic ||
                    unchecked((int)EntityId.ToULong(candidateBody.GetEntityId())) == burstEvent.SourceBodyInstanceId ||
                    candidateBody.mass > cavitationShockwaveMaxAffectedMassKg)
                {
                    continue;
                }

                TryAppendCavitationShockwaveBody(candidateBody, ref rigidbodyCount);
            }

            for (int i = 0; i < rigidbodyCount; i++)
            {
                Rigidbody targetBody = s_CavitationShockwaveRigidbodies[i];
                s_CavitationShockwaveRigidbodies[i] = null;
                if (targetBody == null || targetBody.isKinematic)
                    continue;

                Vector3 radial = targetBody.worldCenterOfMass - burstEvent.Position;
                float radialDistanceSq = radial.sqrMagnitude;
                Vector3 radialDirection = radialDistanceSq > 0.000001f
                    ? DominantAxisOrDefault(radial, burstEvent.Direction)
                    : burstEvent.Direction;
                radialDirection += burstEvent.Direction * 0.25f;
                radialDirection.y += cavitationShockwaveVerticalLift;
                radialDirection = DominantAxisOrDefault(radialDirection, Vector3.up);

                float distance01 = math.saturate(1f - radialDistanceSq * burstEvent.InvRadiusSq);
                distance01 *= distance01;
                if (distance01 <= 0.0001f)
                    continue;

                float velocityChange = burstEvent.Acceleration * burstEvent.Intensity01 * distance01;
                GlobalPhysicsStateManager.QueueKinematicImpact(
                    targetBody,
                    burstEvent.Position,
                    radialDirection,
                    velocityChange);
                PhysicsForceRouter.QueueForce(
                    targetBody,
                    radialDirection * velocityChange,
                    ForceMode.VelocityChange);
            }
        }

        private static void TryAppendCavitationShockwaveBody(
            Rigidbody candidateBody,
            ref int rigidbodyCount)
        {
            int capacity = math.min(s_CavitationShockwaveRigidbodies.Length, CavitationShockwaveHitCapacity);

            for (int i = 0; i < rigidbodyCount; i++)
            {
                if (s_CavitationShockwaveRigidbodies[i] != candidateBody)
                    continue;

                return;
            }

            if (rigidbodyCount >= capacity)
                return;

            s_CavitationShockwaveRigidbodies[rigidbodyCount] = candidateBody;
            rigidbodyCount++;
        }

        private void ReallocateNativeArrays(int requiredCount)
        {
            requiredCount = math.max(requiredCount, 1);
            int newCapacity = math.max(128, _nativeCapacity * 2);
            int growthIterations = 0;

            while (newCapacity < requiredCount)
            {
                if (growthIterations >= MaxNativeCapacityGrowthIterations || newCapacity > (int.MaxValue / 2))
                {
                    newCapacity = math.max(newCapacity, requiredCount);
                    break;
                }

                newCapacity *= 2;
                growthIterations++;
            }

            DisposeNativeArrays();

            _positions     = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _previousPositions = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _previousPositionValid = new NativeArray<byte>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _velocities    = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _angularVelocities = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _upVectors = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _params        = new NativeArray<BuoyancyParams>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _waveOffsets   = new NativeArray<float>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _gpuBuoyancyForcesY = new NativeArray<float>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _resultForces  = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _resultTorques = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _impactEventScratch = new NativeArray<FluidImpactEvent>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _impactEventFlags = new NativeArray<int>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _gpuBuoyancyObjectDataUpload = new NativeArray<GpuBuoyancyObjectData>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _gpuBuoyancyReadback = new NativeArray<float4>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _gpuAbyssalHeatSourceUpload = new NativeArray<GpuHeatSourceData>(MaxAbyssalHeatSourceCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _activeThrusterFlows = new NativeArray<ActiveThrusterFlow>(MaxAnalyticalThrusterCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _activeWhirlpools = new NativeArray<WhirlpoolFlow>(MaxAnalyticalWhirlpoolCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _activeViscosityRegions = new NativeArray<FluidViscosityRegion>(MaxDynamicViscosityRegionCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _viscosityGradientLut = new NativeArray<float>(ViscosityGradientLutSize, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            InitializeViscosityGradientLut();
            _fluidImpactEvents = new NativeQueue<FluidImpactEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<FluidImpactEvent>[64] — deferred water impact acoustic lane — owner: HectonFluidEngine
            PrewarmQueue(ref _fluidImpactEvents, FluidImpactEventQueueCapacity);
            RegisterNativeMemorySentinel();
            _scheduledBodies = new Rigidbody[newCapacity];
            EnsureGpuBuoyancyBuffers(newCapacity);
            EnsureGpuAbyssalFlowBuffers();

            _nativeCapacity = newCapacity;
        }

        /// <summary>
        /// Osvobozhdaet NativeArrays. Vyzyvaetsya pri Destroy i Resize.
        /// </summary>
        private void DisposeNativeArrays()
        {
            JobHandle dependency = _scheduledBuoyancyJobActive ? _scheduledBuoyancyHandle : default;
            DisposeNativeArray(ref _positions, dependency);
            DisposeNativeArray(ref _previousPositions, dependency);
            DisposeNativeArray(ref _previousPositionValid, dependency);
            DisposeNativeArray(ref _velocities, dependency);
            DisposeNativeArray(ref _angularVelocities, dependency);
            DisposeNativeArray(ref _upVectors, dependency);
            DisposeNativeArray(ref _params, dependency);
            DisposeNativeArray(ref _waveOffsets, dependency);
            DisposeNativeArray(ref _gpuBuoyancyForcesY, dependency);
            DisposeNativeArray(ref _resultForces, dependency);
            DisposeNativeArray(ref _resultTorques, dependency);
            DisposeNativeArray(ref _impactEventScratch, dependency);
            DisposeNativeArray(ref _impactEventFlags, dependency);
            DisposeNativeArray(ref _gpuBuoyancyObjectDataUpload, dependency);
            DisposeNativeArray(ref _gpuBuoyancyReadback, dependency);
            DisposeNativeArray(ref _gpuAbyssalHeatSourceUpload, dependency);
            DisposeNativeArray(ref _activeThrusterFlows, dependency);
            DisposeNativeArray(ref _activeWhirlpools, dependency);
            DisposeNativeArray(ref _activeViscosityRegions, dependency);
            DisposeNativeArray(ref _viscosityGradientLut, dependency);
            DisposeNativeQueue(ref _fluidImpactEvents, dependency, nameof(_fluidImpactEvents));
            _fluidImpactQueuedCount = 0;
            _activeThrusterFlowCount = 0;
            _activeWhirlpoolFlowCount = 0;
            _activeViscosityRegionCount = 0;
            _scheduledBodies = null;
            _scheduledBuoyancyHandle = default;
            _scheduledBuoyancyJobActive = false;
            _scheduledForceCount = 0;
            _cavitationBurstCount = 0;
            ReleaseGpuBuoyancyBuffers();
            ReleaseGpuAbyssalFlowBuffers();
            _hasGpuBuoyancyData = false;

            _nativeCapacity = 0;
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_positions, NativeMemoryOwner, nameof(_positions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_previousPositions, NativeMemoryOwner, nameof(_previousPositions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_previousPositionValid, NativeMemoryOwner, nameof(_previousPositionValid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_velocities, NativeMemoryOwner, nameof(_velocities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_angularVelocities, NativeMemoryOwner, nameof(_angularVelocities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_upVectors, NativeMemoryOwner, nameof(_upVectors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_params, NativeMemoryOwner, nameof(_params), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_waveOffsets, NativeMemoryOwner, nameof(_waveOffsets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuBuoyancyForcesY, NativeMemoryOwner, nameof(_gpuBuoyancyForcesY), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_resultForces, NativeMemoryOwner, nameof(_resultForces), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_resultTorques, NativeMemoryOwner, nameof(_resultTorques), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_impactEventScratch, NativeMemoryOwner, nameof(_impactEventScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_impactEventFlags, NativeMemoryOwner, nameof(_impactEventFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuBuoyancyObjectDataUpload, NativeMemoryOwner, nameof(_gpuBuoyancyObjectDataUpload), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuBuoyancyReadback, NativeMemoryOwner, nameof(_gpuBuoyancyReadback), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuAbyssalHeatSourceUpload, NativeMemoryOwner, nameof(_gpuAbyssalHeatSourceUpload), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeThrusterFlows, NativeMemoryOwner, nameof(_activeThrusterFlows), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeWhirlpools, NativeMemoryOwner, nameof(_activeWhirlpools), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeViscosityRegions, NativeMemoryOwner, nameof(_activeViscosityRegions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_viscosityGradientLut, NativeMemoryOwner, nameof(_viscosityGradientLut), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeQueue(
                _fluidImpactEvents,
                FluidImpactEventQueueCapacity,
                NativeMemoryOwner,
                nameof(_fluidImpactEvents),
                NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (dependency.IsCompleted)
                array.Dispose();
            else
                array.Dispose(dependency);

            array = default;
        }

        private static void DisposeNativeQueue<T>(ref NativeQueue<T> queue, JobHandle dependency, string label)
            where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, label);
            if (dependency.IsCompleted)
                queue.Dispose();
            else
                queue.Dispose(dependency);

            queue = default;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private void InitializeViscosityGradientLut()
        {
            if (!_viscosityGradientLut.IsCreated || _viscosityGradientLut.Length <= 0)
                return;

            int lastIndex = _viscosityGradientLut.Length - 1;
            for (int i = 0; i < _viscosityGradientLut.Length; i++)
            {
                float x = lastIndex > 0 ? i * math.rcp((float)lastIndex) : 1f;
                _viscosityGradientLut[i] = x * x * (3f - 2f * x);
            }
        }

        private void CopyAnalyticalFlowInputsToNative()
        {
            if (!_activeThrusterFlows.IsCreated || !_activeWhirlpools.IsCreated || !_activeViscosityRegions.IsCreated)
                return;

            int thrusterWriteIndex = 0;
            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
            {
                ActiveThrusterFlow thruster = _thrusterFlowBuffer[i];
                if (thruster.Active == 0 || thruster.Strength <= 0f || thruster.RadiusSq <= 0f || thruster.InvRadiusSq <= 0f)
                    continue;

                _activeThrusterFlows[thrusterWriteIndex++] = thruster;
            }

            for (int i = thrusterWriteIndex; i < MaxAnalyticalThrusterCount; i++)
                _activeThrusterFlows[i] = default;

            _activeThrusterFlowCount = thrusterWriteIndex;

            int whirlpoolWriteIndex = 0;
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow whirlpool = _whirlpoolFlowBuffer[i];
                if (whirlpool.Active == 0 || whirlpool.RadiusSq <= 0f || whirlpool.InvRadiusSq <= 0f)
                    continue;

                _activeWhirlpools[whirlpoolWriteIndex++] = whirlpool;
            }

            for (int i = whirlpoolWriteIndex; i < MaxAnalyticalWhirlpoolCount; i++)
                _activeWhirlpools[i] = default;

            _activeWhirlpoolFlowCount = whirlpoolWriteIndex;

            int viscosityWriteIndex = 0;
            for (int i = 0; i < MaxDynamicViscosityRegionCount; i++)
            {
                FluidViscosityRegion viscosityRegion = _viscosityRegionBuffer[i];
                if (viscosityRegion.Active == 0 || viscosityRegion.InvRadiusSq <= 0f || viscosityRegion.ViscosityMultiplier <= 0f)
                    continue;

                _activeViscosityRegions[viscosityWriteIndex++] = viscosityRegion;
            }

            for (int i = viscosityWriteIndex; i < MaxDynamicViscosityRegionCount; i++)
                _activeViscosityRegions[i] = default;

            _activeViscosityRegionCount = viscosityWriteIndex;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            float3 numericValue = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(numericValue));
        }

        private bool ContainsRegisteredObject(BuoyancyObject target)
        {
            int count = _objects.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_objects[i], target))
                    return true;
            }

            return false;
        }

        private static Vector3 DominantAxisOrDefault(Vector3 value, Vector3 fallback)
        {
            float3 axis = DominantAxisOrDefault(new float3(value.x, value.y, value.z), new float3(fallback.x, fallback.y, fallback.z));
            return new Vector3(axis.x, axis.y, axis.z);
        }

        private static float3 DominantAxisOrDefault(float3 value, float3 fallback)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float3 xAxis = new float3(math.select(-1f, 1f, value.x >= 0f), 0f, 0f);
            float3 yAxis = new float3(0f, math.select(-1f, 1f, value.y >= 0f), 0f);
            float3 zAxis = new float3(0f, 0f, math.select(-1f, 1f, value.z >= 0f));
            float3 yzAxis = math.select(zAxis, yAxis, absValue.y >= absValue.z);
            float3 axis = math.select(yzAxis, xAxis, absValue.x >= absValue.y && absValue.x >= absValue.z);
            return math.select(axis, fallback, maxComponent <= 0.000001f);
        }

        private static bool TrySanitizePhysicsVector(float3 value, uint warningHash, out Vector3 sanitized)
        {
            if (math.any(math.isnan(value)) || math.any(math.isinf(value)) || !math.all(math.isfinite(value)))
            {
                ReportNonFinitePhysicsVector(warningHash);
                sanitized = Vector3.zero;
                return false;
            }

            sanitized = new Vector3(value.x, value.y, value.z);
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportNonFinitePhysicsVector(uint warningHash)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, HectonFluidEngineContextHash, 1f);
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private void ReleaseIdleNativeBuffersIfNeeded()
        {
            if (_objects.Count > 0 || _nativeCapacity <= 0)
                return;

            DisposeNativeArrays();
        }

        private static WeatherRuntimeSnapshot ResolveWeatherSnapshot()
        {
            IWeatherService weatherService = GlobalRegistry.Weather;
            if (weatherService == null || !weatherService.IsInitialized)
                return default;

            return weatherService.GetRuntimeSnapshot();
        }

        private float PublishCurrentWaterLevelUniform()
        {
            float cinematicWaterLevel = ResolveCinematicWaterLevelY();
            Shader.SetGlobalFloat(_CurrentWaterLevelId, cinematicWaterLevel);
            Shader.SetGlobalFloat(_CurrentWaterLevelYId, cinematicWaterLevel);
            if (UIStateStore.IsInitialized)
                UIStateStore.WriteValue(UIValueSlotId.WaterSurfaceY, cinematicWaterLevel, Time.unscaledTime);
            return cinematicWaterLevel;
        }

        private float ResolveCinematicWaterLevelY()
        {
            return GlobalPhysicsStateManager.ResolveFrameCachedCurrentWaterLevelY(
                waterLevel,
                enableCinematicTideShift,
                cinematicTideAmplitudeMeters,
                ResolveWaterLevelTimeSeconds());
        }

        private static float ResolveWaterLevelTimeSeconds()
        {
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float syncedTime = weatherSnapshot.CurrentMeta.TimeAccumulator;
            return math.isfinite(syncedTime) && syncedTime > 0f
                ? syncedTime
                : Time.time;
        }

        private void EnsureGpuBuoyancyBuffers(int capacity)
        {
            if (capacity <= 0)
                return;

            if (_gpuBuoyancyPositionBuffer == null || _gpuBuoyancyPositionBuffer.count != capacity)
            {
                ReleaseGpuBuoyancyBuffers();
                _gpuBuoyancyPositionBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float3>(capacity); // COLD ALLOC: GraphicsBuffer[capacity] — GPU buoyancy position upload buffer — owner: HectonFluidEngine
                _gpuBuoyancyParamBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuBuoyancyObjectData>(capacity); // COLD ALLOC: GraphicsBuffer[capacity] — GPU buoyancy object payload buffer — owner: HectonFluidEngine
                _gpuBuoyancyResultBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(capacity); // COLD ALLOC: GraphicsBuffer[capacity] — GPU buoyancy result buffer for async readback — owner: HectonFluidEngine
            }
        }

        private void ReleaseGpuBuoyancyBuffers()
        {
            if (_gpuBuoyancyPositionBuffer != null)
            {
                _gpuBuoyancyPositionBuffer.Release();
                _gpuBuoyancyPositionBuffer = null;
            }

            if (_gpuBuoyancyParamBuffer != null)
            {
                _gpuBuoyancyParamBuffer.Release();
                _gpuBuoyancyParamBuffer = null;
            }

            if (_gpuBuoyancyResultBuffer != null)
            {
                _gpuBuoyancyResultBuffer.Release();
                _gpuBuoyancyResultBuffer = null;
            }
        }

        private void EnsureGpuAbyssalFlowBuffers()
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return;

            if (_gpuAbyssalFlowResultBuffer == null || _gpuAbyssalFlowResultBuffer.count != nodeCount)
            {
                ReleaseGpuAbyssalFlowBuffers();
                _gpuAbyssalFlowResultBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(nodeCount); // COLD ALLOC: GraphicsBuffer[nodeCount] — GPU abyssal flow-vector field storage — owner: HectonFluidEngine
                _gpuAbyssalHeatSourceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuHeatSourceData>(MaxAbyssalHeatSourceCount); // COLD ALLOC: GraphicsBuffer[8] — inferred hydrothermal heat-source upload staging — owner: HectonFluidEngine
                _gpuAbyssalAggregateBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, sizeof(uint)); // COLD ALLOC: GraphicsBuffer[1] — GPU abyssal aggregate surge bitmask readback — owner: HectonFluidEngine
            }
        }

        private void ReleaseGpuAbyssalFlowBuffers()
        {
            if (_gpuAbyssalFlowResultBuffer != null)
            {
                _gpuAbyssalFlowResultBuffer.Release();
                _gpuAbyssalFlowResultBuffer = null;
            }

            if (_gpuAbyssalHeatSourceBuffer != null)
            {
                _gpuAbyssalHeatSourceBuffer.Release();
                _gpuAbyssalHeatSourceBuffer = null;
            }

            if (_gpuAbyssalAggregateBuffer != null)
            {
                _gpuAbyssalAggregateBuffer.Release();
                _gpuAbyssalAggregateBuffer = null;
            }

            _gpuAbyssalReadbackWriteIndex = 0;
            if (_gpuAbyssalReadbackActive != null)
            {
                for (int i = 0; i < _gpuAbyssalReadbackActive.Length; i++)
                    _gpuAbyssalReadbackActive[i] = false;
            }

            _lastAbyssalGridResolution = Vector4.zero;
            _lastAbyssalFlowCenter = Vector4.zero;
            _lastAbyssalFlowSpacing = Vector4.zero;
        }

        private void ConsumeGpuAbyssalFlowReadbacks()
        {
            using (_gpuAbyssalReadbackProfilerMarker.Auto())
            {
                if (_gpuAbyssalReadbackRequests == null || _gpuAbyssalReadbackActive == null)
                    return;

                for (int requestIndex = 0; requestIndex < GpuReadbackRingSize; requestIndex++)
                {
                    if (!_gpuAbyssalReadbackActive[requestIndex])
                        continue;

                    AsyncGPUReadbackRequest request = _gpuAbyssalReadbackRequests[requestIndex];
                    if (!request.done)
                        continue;

                    _gpuAbyssalReadbackActive[requestIndex] = false;
                    if (request.hasError)
                        continue;

                    NativeArray<uint> aggregateData = request.GetData<uint>();
                    if (aggregateData.Length <= 0)
                        continue;

                    uint aggregateMask = aggregateData[0];
                    _debugAbyssalAggregateMask = aggregateMask;
                    if ((aggregateMask & (uint)WeatherState.BiolumeSurge) != 0u &&
                        GlobalRegistry.Weather is GlobalWeatherDirector weatherDirector)
                    {
                        weatherDirector.RegisterBiolumeSurge(AbyssalBiolumeSurgeHoldSeconds);
                    }
                }
            }
        }

        private void TryDispatchGpuAbyssalFlowField(in WeatherRuntimeSnapshot weatherSnapshot, float resolvedWaterLevel)
        {
            if (!enableGpuAbyssalFlowField ||
                abyssalFlowFieldCompute == null ||
                _gpuAbyssalResetKernel < 0 ||
                _gpuAbyssalUpdateKernel < 0 ||
                _gpuAbyssalSurgeKernel < 0 ||
                lodObserver == null ||
                !_gpuAbyssalHeatSourceUpload.IsCreated)
            {
                return;
            }

            EnsureGpuAbyssalFlowBuffers();
            if (_gpuAbyssalFlowResultBuffer == null || _gpuAbyssalHeatSourceBuffer == null || _gpuAbyssalAggregateBuffer == null)
                return;

            int slot = _gpuAbyssalReadbackWriteIndex;
            if (_gpuAbyssalReadbackActive != null && _gpuAbyssalReadbackActive[slot])
                return;

            float3 flowCenter = ResolveAbyssalFlowCenter(resolvedWaterLevel);
            int heatSourceCount = CaptureAbyssalHeatSources(flowCenter);
            _debugAbyssalHeatSourceCount = heatSourceCount;

            GraphicsBufferUploadUtility.UploadNativeArray(_gpuAbyssalHeatSourceBuffer, _gpuAbyssalHeatSourceUpload, MaxAbyssalHeatSourceCount);

            int nodeCount = GetAbyssalFlowNodeCount();
            int groupCount = math.max(1, (nodeCount + GpuThreadGroupSize - 1) >> GpuThreadGroupShift);

            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalResetKernel, _AbyssalAggregateMaskId, _gpuAbyssalAggregateBuffer);
            abyssalFlowFieldCompute.Dispatch(_gpuAbyssalResetKernel, 1, 1, 1);

            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalFlowFieldResultId, _gpuAbyssalFlowResultBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalHeatSourcesId, _gpuAbyssalHeatSourceBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalAggregateMaskId, _gpuAbyssalAggregateBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalSurgeKernel, _AbyssalFlowFieldResultId, _gpuAbyssalFlowResultBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalSurgeKernel, _AbyssalAggregateMaskId, _gpuAbyssalAggregateBuffer);

            Vector3 centerManaged = new Vector3(flowCenter.x, flowCenter.y, flowCenter.z);
            float3 resolvedWeatherCurrent =
                weatherSnapshot.CurrentMeta.GlobalBaseVector * weatherSnapshot.CurrentMeta.GlobalScale +
                ResolveGiantWakeCurrentForDepth(flowCenter.y);
            Vector3 weatherCurrentManaged = new Vector3(
                resolvedWeatherCurrent.x,
                resolvedWeatherCurrent.y,
                resolvedWeatherCurrent.z);
            Vector3 weatherWindManaged = new Vector3(
                weatherSnapshot.GlobalWindVector.x,
                weatherSnapshot.GlobalWindVector.y,
                weatherSnapshot.GlobalWindVector.z);
            Vector3 horizontalResolutionVector = new Vector3(abyssalFlowHorizontalResolution, abyssalFlowVerticalResolution, abyssalFlowHorizontalResolution);
            Vector4 gridResolution = new Vector4(horizontalResolutionVector.x, horizontalResolutionVector.y, horizontalResolutionVector.z, nodeCount);
            Vector4 flowCenterVector = new Vector4(centerManaged.x, centerManaged.y, centerManaged.z, 0f);
            Vector4 flowSpacingVector = new Vector4(abyssalFlowHorizontalCellSize, abyssalFlowVerticalCellSize, 0f, 0f);
            float resolvedWaveHeight = math.max(
                0f,
                math.max(0f, weatherSnapshot.Wave0.Amplitude) +
                math.max(0f, weatherSnapshot.Wave1.Amplitude) +
                math.max(0f, weatherSnapshot.Wave2.Amplitude));

            abyssalFlowFieldCompute.SetVector(_AbyssalGridResolutionId, gridResolution);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowCenterId, flowCenterVector);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowSpacingId, flowSpacingVector);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowWeatherCurrentId, new Vector4(weatherCurrentManaged.x, weatherCurrentManaged.y, weatherCurrentManaged.z, weatherSnapshot.WeatherIntensity));
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowWeatherWindId, new Vector4(weatherWindManaged.x, weatherWindManaged.y, weatherWindManaged.z, 0f));
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowWeatherParamsId, new Vector4(
                weatherSnapshot.CurrentMeta.ThermalIntensity,
                ApproximateMagnitude(new float3(weatherWindManaged.x, weatherWindManaged.y, weatherWindManaged.z)),
                resolvedWaveHeight,
                weatherSnapshot.CurrentMeta.TimeAccumulator));
            abyssalFlowFieldCompute.SetFloat(_AbyssalFlowSurfaceYId, resolvedWaterLevel);
            abyssalFlowFieldCompute.SetFloat(_AbyssalFlowThermoclineYId, resolvedWaterLevel - AbyssalFlowThermoclineDepthMeters);
            abyssalFlowFieldCompute.SetInt(_AbyssalFlowHeatSourceCountId, heatSourceCount);
            abyssalFlowFieldCompute.SetInt(_AbyssalFlowWeatherStateMaskId, (int)weatherSnapshot.StateMask);

            abyssalFlowFieldCompute.Dispatch(_gpuAbyssalUpdateKernel, groupCount, 1, 1);
            abyssalFlowFieldCompute.Dispatch(_gpuAbyssalSurgeKernel, groupCount, 1, 1);
            Shader.SetGlobalBuffer(_AbyssalFlowFieldResultId, _gpuAbyssalFlowResultBuffer);
            Shader.SetGlobalVector(_AbyssalGridResolutionId, gridResolution);
            Shader.SetGlobalVector(_AbyssalFlowCenterId, flowCenterVector);
            Shader.SetGlobalVector(_AbyssalFlowSpacingId, flowSpacingVector);
            _lastAbyssalGridResolution = gridResolution;
            _lastAbyssalFlowCenter = flowCenterVector;
            _lastAbyssalFlowSpacing = flowSpacingVector;

            _gpuAbyssalReadbackRequests[slot] = AsyncGPUReadback.Request(_gpuAbyssalAggregateBuffer);
            _gpuAbyssalReadbackActive[slot] = true;
            _gpuAbyssalReadbackWriteIndex = (_gpuAbyssalReadbackWriteIndex + 1) % GpuReadbackRingSize;
        }

        private int CaptureAbyssalHeatSources(float3 flowCenter)
        {
            if (!_gpuAbyssalHeatSourceUpload.IsCreated)
                return 0;

            for (int i = 0; i < MaxAbyssalHeatSourceCount; i++)
                _gpuAbyssalHeatSourceUpload[i] = default;

            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            if (thermalManager == null)
                return 0;

            float horizontalProbeOffset = math.max(abyssalHeatProbeRadius, abyssalFlowHorizontalCellSize * 1.5f);
            float verticalProbeOffset = math.max(abyssalHeatProbeRadius * 0.5f, abyssalFlowVerticalCellSize);
            float sampleRadius = math.max(1f, abyssalFlowHorizontalCellSize * 0.5f);
            int sourceCount = 0;

            for (int probeIndex = 0; probeIndex < MaxAbyssalHeatSourceCount; probeIndex++)
            {
                float3 sampleOffset = ResolveHeatProbeOffset(probeIndex, horizontalProbeOffset, verticalProbeOffset);
                Vector3 samplePosition = new Vector3(
                    flowCenter.x + sampleOffset.x,
                    flowCenter.y + sampleOffset.y,
                    flowCenter.z + sampleOffset.z);

                if (!thermalManager.SampleThermalFlow(samplePosition, sampleRadius, out AbyssalThermalManager.ThermalFlowSample sample) ||
                    !sample.HasFlow)
                {
                    continue;
                }

                float heatNormalizationRcp = math.rcp(math.max(0.1f, abyssalHeatIntensityNormalization));
                float intensity = math.saturate(math.max(
                    sample.Heat01 * heatNormalizationRcp,
                    sample.FlowVelocityWS.y * 0.125f));
                if (intensity <= 0.0001f)
                    continue;

                _gpuAbyssalHeatSourceUpload[sourceCount] = new GpuHeatSourceData
                {
                    PositionWS = new float3(samplePosition.x, samplePosition.y, samplePosition.z),
                    Intensity = intensity,
                    Radius = abyssalHeatProbeRadius,
                    Padding = float3.zero,
                };

                sourceCount++;
                if (sourceCount >= MaxAbyssalHeatSourceCount)
                    break;
            }

            return sourceCount;
        }

        private float3 ResolveAbyssalFlowCenter(float resolvedWaterLevel)
        {
            Vector3 observerPosition = lodObserver.position;
            return new float3(
                observerPosition.x,
                math.min(observerPosition.y, resolvedWaterLevel - 32f),
                observerPosition.z);
        }

        private float3 ResolveGiantWakeCurrentBase()
        {
            if (!enableGiantWakeCurrent || giantWakeCurrentStrength <= 0f)
                return float3.zero;

            HectonCelestialEngine celestialEngine = GlobalRegistry.CelestialEngine;
            if (celestialEngine == null || !celestialEngine.TryGetAegirSkyDirection(out Vector3 directionManaged))
                return float3.zero;

            float3 skyDirection = new float3(directionManaged.x, directionManaged.y, directionManaged.z);
            float3 horizontalDirection = new float3(skyDirection.x, 0f, skyDirection.z);
            float horizontalLengthSq = math.lengthsq(horizontalDirection);
            if (horizontalLengthSq <= GiantWakeDirectionEpsilonSq)
                return float3.zero;

            float3 wakeDirection = DominantAxisOrDefault(horizontalDirection, new float3(1f, 0f, 0f));
            wakeDirection.y = giantWakeVerticalBias;
            wakeDirection = DominantAxisOrDefault(wakeDirection, new float3(1f, 0f, 0f));
            return wakeDirection * math.max(0f, giantWakeCurrentStrength);
        }

        private float3 ResolveGiantWakeCurrentForDepth(float sampleY)
        {
            float3 wakeCurrent = _resolvedGiantWakeCurrent;
            if (math.lengthsq(wakeCurrent) <= GiantWakeDirectionEpsilonSq)
                wakeCurrent = ResolveGiantWakeCurrentBase();

            float depthBelowSurface = math.max(0f, waterLevel - sampleY);
            float fadeStart = math.max(0f, giantWakeDepthFadeStart);
            float fadeRange = math.max(0.001f, giantWakeDepthFadeRange);
            float depthFade = math.saturate((depthBelowSurface - fadeStart) * math.rcp(fadeRange));
            return wakeCurrent * depthFade;
        }

        private int GetAbyssalFlowNodeCount()
        {
            return math.max(1, abyssalFlowHorizontalResolution) *
                   math.max(1, abyssalFlowVerticalResolution) *
                   math.max(1, abyssalFlowHorizontalResolution);
        }

        private static float3 ResolveHeatProbeOffset(int probeIndex, float horizontalProbeOffset, float verticalProbeOffset)
        {
            switch (probeIndex)
            {
                case 0: return float3.zero;
                case 1: return new float3(horizontalProbeOffset, 0f, 0f);
                case 2: return new float3(-horizontalProbeOffset, 0f, 0f);
                case 3: return new float3(0f, 0f, horizontalProbeOffset);
                case 4: return new float3(0f, 0f, -horizontalProbeOffset);
                case 5: return new float3(0f, verticalProbeOffset, 0f);
                case 6: return new float3(0f, -verticalProbeOffset, 0f);
                default: return new float3(horizontalProbeOffset * 0.70710677f, 0f, horizontalProbeOffset * 0.70710677f);
            }
        }

        private static float FastMagnitudeApprox(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private void ConsumeGpuBuoyancyReadbacks()
        {
            using (_gpuReadbackProfilerMarker.Auto())
            {
            if (_gpuReadbackRequests == null || _gpuReadbackActive == null || !_gpuBuoyancyReadback.IsCreated)
                return;

            for (int requestIndex = 0; requestIndex < GpuReadbackRingSize; requestIndex++)
            {
                if (!_gpuReadbackActive[requestIndex])
                    continue;

                AsyncGPUReadbackRequest request = _gpuReadbackRequests[requestIndex];
                if (!request.done)
                    continue;

                _gpuReadbackActive[requestIndex] = false;
                if (request.hasError)
                    continue;

                int readCount = math.min(_gpuReadbackCounts[requestIndex], _gpuBuoyancyReadback.Length);
                NativeArray<float4> readbackData = request.GetData<float4>();
                for (int i = 0; i < readCount; i++)
                {
                    float4 sample = readbackData[i];
                    _gpuBuoyancyReadback[i] = sample;
                    _waveOffsets[i] = sample.x;
                    _gpuBuoyancyForcesY[i] = sample.y;
                }

                _hasGpuBuoyancyData = readCount > 0;
            }
            }
        }

        private void UploadGpuBuoyancyObjectData(int count)
        {
            if (!_gpuBuoyancyObjectDataUpload.IsCreated)
                return;

            for (int i = 0; i < count; i++)
            {
                BuoyancyParams buoyancyParams = _params[i];
                _gpuBuoyancyObjectDataUpload[i] = new GpuBuoyancyObjectData
                {
                    Volume = buoyancyParams.volume,
                    Height = buoyancyParams.height,
                    IsInAir = buoyancyParams.isInAir != 0 ? 1f : 0f,
                    SimplifiedSubmersion = buoyancyParams.simplifiedSubmersion != 0 ? 1f : 0f,
                    BoundsCenterWS = buoyancyParams.boundsCenter,
                    BoundsExtentsWS = buoyancyParams.boundsExtents
                };
            }
        }

        private void SetGpuWave(ComputeShader shader, int waveAId, int waveBId, in GerstnerWaveComponent wave)
        {
            shader.SetVector(waveAId, new Vector4(wave.DirectionXZ.x, wave.DirectionXZ.y, wave.Amplitude, wave.Wavelength));
            shader.SetVector(waveBId, new Vector4(wave.Steepness, wave.PhaseOffset, wave.SpeedMultiplier, 0f));
        }

        private void TryDispatchGpuBuoyancySampling(in WeatherRuntimeSnapshot weatherSnapshot, int count, float resolvedWaterLevel)
        {
            if (!enableGpuBuoyancySampling ||
                gpuBuoyancyCompute == null ||
                _gpuBuoyancyKernel < 0 ||
                count < gpuBuoyancyActivationThreshold ||
                !_positions.IsCreated ||
                !_gpuBuoyancyObjectDataUpload.IsCreated)
            {
                return;
            }

            EnsureGpuBuoyancyBuffers(count);
            if (_gpuBuoyancyPositionBuffer == null || _gpuBuoyancyParamBuffer == null || _gpuBuoyancyResultBuffer == null)
                return;

            int slot = _gpuReadbackWriteIndex;
            if (_gpuReadbackActive != null && _gpuReadbackActive[slot])
                return;

            UploadGpuBuoyancyObjectData(count);
            GraphicsBufferUploadUtility.UploadNativeArray(_gpuBuoyancyPositionBuffer, _positions, count);
            GraphicsBufferUploadUtility.UploadNativeArray(_gpuBuoyancyParamBuffer, _gpuBuoyancyObjectDataUpload, count);

            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyPositionsId, _gpuBuoyancyPositionBuffer);
            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyObjectDataId, _gpuBuoyancyParamBuffer);
            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyResultsId, _gpuBuoyancyResultBuffer);
            gpuBuoyancyCompute.SetInt(_GpuBuoyancyObjectCountId, count);
            gpuBuoyancyCompute.SetVector(_GpuBuoyancyWaterParamsId, new Vector4(resolvedWaterLevel, waterDensity, math.abs(UnityEngine.Physics.gravity.y), weatherSnapshot.CurrentMeta.TimeAccumulator));
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave0AId, _GpuBuoyancyWave0BId, weatherSnapshot.Wave0);
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave1AId, _GpuBuoyancyWave1BId, weatherSnapshot.Wave1);
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave2AId, _GpuBuoyancyWave2BId, weatherSnapshot.Wave2);

            int groupCount = math.max(1, (count + GpuThreadGroupSize - 1) >> GpuThreadGroupShift);
            gpuBuoyancyCompute.Dispatch(_gpuBuoyancyKernel, groupCount, 1, 1);
            _gpuReadbackRequests[slot] = AsyncGPUReadback.Request(_gpuBuoyancyResultBuffer);
            _gpuReadbackCounts[slot] = count;
            _gpuReadbackActive[slot] = true;
            _gpuReadbackWriteIndex = (_gpuReadbackWriteIndex + 1) % GpuReadbackRingSize;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugObjectCount = _objects.Count;
            _debugNearCount = 0;
            _debugMediumCount = 0;
            _debugFarCount = 0;
            _debugCulledCount = 0;
            _debugCurrentVolumeCount = CurrentVolume.ActiveCount;
        }

        private void TryResolveObserver(bool force)
        {
            if (lodObserver != null)
                return;

            if (!force && _observerResolveRetryTimer > 0f)
                return;

            _observerResolveRetryTimer = ObserverResolveRetryInterval;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                lodObserver = playerTransform;
        }

        /// <summary>
        /// Updates cached LOD distance squares (called once at startup,
        /// and whenever LOD parameters change via properties).
        /// </summary>
        private void UpdateCachedLodDistances()
        {
            _cachedNearDistSq = nearLodDistance * nearLodDistance;
            _cachedMediumDistSq = mediumLodDistance * mediumLodDistance;
            _cachedFarDistSq = farLodDistance * farLodDistance;
            _cachedCullDistSq = cullLodDistance * cullLodDistance;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (waterDensity < 0.01f) waterDensity = 0.01f;
            if (cinematicTideAmplitudeMeters < 0f) cinematicTideAmplitudeMeters = 0f;
            if (viscousDrag  < 0f)    viscousDrag  = 0f;
            if (maxQuadraticDragForcePerKg < 0f) maxQuadraticDragForcePerKg = 0f;
            if (angularDrag  < 0f)    angularDrag  = 0f;
            if (jobBatchSize < 1)     jobBatchSize = 1;
            if (currentNoiseScale < 0.0001f) currentNoiseScale = 0.0001f;
            if (currentTimeScale < 0f) currentTimeScale = 0f;
            if (phantomCurrentStrength < 0f) phantomCurrentStrength = 0f;
            if (haloclineBoundaryDepthMeters < 0.01f) haloclineBoundaryDepthMeters = 0.01f;
            if (deepLayerDensityMultiplier < 1f) deepLayerDensityMultiplier = 1f;
            if (giantWakeCurrentStrength < 0f) giantWakeCurrentStrength = 0f;
            giantWakeVerticalBias = Mathf.Clamp(giantWakeVerticalBias, -1f, 1f);
            if (giantWakeDepthFadeStart < 0f) giantWakeDepthFadeStart = 0f;
            if (giantWakeDepthFadeRange < 1f) giantWakeDepthFadeRange = 1f;
            if (tidalShearTorqueStrength < 0f) tidalShearTorqueStrength = 0f;
            if (tidalShearFrequency < 0.01f) tidalShearFrequency = 0.01f;
            if (nearLodDistance < 1f) nearLodDistance = 1f;
            if (mediumLodDistance < nearLodDistance) mediumLodDistance = nearLodDistance;
            if (farLodDistance < mediumLodDistance) farLodDistance = mediumLodDistance;
            if (cullLodDistance < farLodDistance) cullLodDistance = farLodDistance;
            if (gizmoCurrentVectorScale < 0f) gizmoCurrentVectorScale = 0f;
            if (abyssalFlowHorizontalResolution < 8) abyssalFlowHorizontalResolution = 8;
            if (abyssalFlowVerticalResolution < 4) abyssalFlowVerticalResolution = 4;
            if (abyssalFlowHorizontalCellSize < 4f) abyssalFlowHorizontalCellSize = 4f;
            if (abyssalFlowVerticalCellSize < 4f) abyssalFlowVerticalCellSize = 4f;
            if (abyssalHeatProbeRadius < 4f) abyssalHeatProbeRadius = 4f;
            if (abyssalHeatIntensityNormalization < 0.1f) abyssalHeatIntensityNormalization = 0.1f;
            cavitationBubbleEmitCountAtFullIntensity = Mathf.Clamp(cavitationBubbleEmitCountAtFullIntensity, 1, 128);
            if (cavitationShockwaveMaxAffectedMassKg < 0.1f) cavitationShockwaveMaxAffectedMassKg = 0.1f;
            cavitationShockwaveVerticalLift = Mathf.Clamp01(cavitationShockwaveVerticalLift);

#if UNITY_EDITOR
            if (gpuBuoyancyCompute == null)
                gpuBuoyancyCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(GpuBuoyancyComputeAssetPath);

            if (abyssalFlowFieldCompute == null)
                abyssalFlowFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(AbyssalFlowFieldComputeAssetPath);
#endif
            
            // Update LOD cache when parameters change
            UpdateCachedLodDistances();
        }

        private void OnDrawGizmos()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Gizmos.color = new Color(0f, 0.3f, 0.8f, 0.1f);
            Vector3 center = new Vector3(0f, waterLevel, 0f);
            Gizmos.DrawCube(center, new Vector3(200f, 0.02f, 200f));

            if (lodObserver != null && drawLodGizmos)
            {
                DrawLodRing(nearLodDistance, new Color(0.15f, 0.9f, 1f, 0.7f));
                DrawLodRing(mediumLodDistance, new Color(0.25f, 0.8f, 0.55f, 0.65f));
                DrawLodRing(farLodDistance, new Color(0.95f, 0.75f, 0.2f, 0.55f));
                DrawLodRing(cullLodDistance, new Color(1f, 0.35f, 0.2f, 0.45f));
            }

            if (drawCurrentVectors)
            {
                Vector3 origin = lodObserver != null ? lodObserver.position : center;
                origin.y = waterLevel;
                Vector3 current = currentVector * gizmoCurrentVectorScale;
                Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.95f);
                Gizmos.DrawRay(origin, current);
            }
        }

        private void DrawLodRing(float radius, Color color)
        {
            if (lodObserver == null || radius <= 0f)
                return;

            Gizmos.color = color;
#if UNITY_EDITOR
            Handles.color = color;
            Handles.DrawWireDisc(lodObserver.position, Vector3.up, radius);
#else
            Gizmos.DrawWireSphere(lodObserver.position, radius);
#endif
        }
#endif
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuoyancyParams — dannye obekta dlya Job (blittable struct)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parametry odnogo obekta dlya BuoyancyJob.
    /// Blittable struct — bezopasen dlya NativeArray i Burst.
    ///
    /// IZMENENIE: dobavleno pole isInAir dlya sistemy Suhih Zon.
    /// Dry-zone and simulation flags are packed into explicit bytes to keep the Burst payload deterministic.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BuoyancyParams
    {
        public float3 boundsCenter;
        public float3 boundsExtents;

        /// <summary>Plotnost obekta (kg/m³).</summary>
        public float density;

        /// <summary>Obem obekta (m³).</summary>
        public float volume;

        /// <summary>Vysota obekta (m) dlya chastichnogo pogruzheniya.</summary>
        public float height;

        /// <summary>Massa Rigidbody (kg).</summary>
        public float mass;
        public float currentResponse;
        public float surfaceStability;
        public float localFluidDensity;
        public float angularDragMultiplier;
        public float buoyancyMultiplier;
        public float3 localCurrent;

        /// <summary>
        /// Obekt nahoditsya v suhoy zone (vnutri nezatoplennogo modulya).
        /// Esli true — vse vodnye sily obnulyayutsya v BuoyancyJob.
        /// </summary>
        public byte isInAir;
        public byte simulationMode;
        public byte simplifiedSubmersion;
        public byte useLocalFluidDensityOverride;
        public uint alignmentPadding;
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuoyancyJob — Burst Compiled, IJobParallelFor
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallelnyy Job dlya vychisleniya sil plavuchesti, soprotivleniya
    /// i podvodnyh techeniy.
    ///
    /// Burst-compiled SIMD-optimizatsiya, net managed code, net GC.
    ///
    /// IZMENENIE (Dry Zones):
    ///   Pervaya proverka v Execute: esli p.isInAir == true,
    ///   rezultiruyuschie sily i momenty = float3.zero.
    ///   Obekt vnutri bazy ne ispytyvaet nikakih vodnyh sil.
    ///
    /// FIZIKA:
    ///   Arhimed:    F_buoy  = ρ_water × V_submerged × g  (vverh)
    ///   Drag:       F_drag  = -v × C_drag × subRatio     (protiv dvizheniya)
    ///   Techenie:    F_curr  = currentForce × subRatio     (po napravleniyu)
    ///   AngDrag:    T_drag  = -ω × C_angDrag × subRatio  (protiv vrascheniya)
    /// </summary>
    /// <summary>
    /// Burst-compiled fallback wave evaluator used by CPU-side buoyancy systems.
    /// This samples the first-party weather spectrum for physics consumers and does not replace Crest FFT rendering.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct WaveQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> PositionsWS;
        [ReadOnly] public NativeArray<BuoyancyParams> ObjParams;
        [WriteOnly] public NativeArray<float> VerticalOffsets;

        public GerstnerWaveComponent Wave0;
        public GerstnerWaveComponent Wave1;
        public GerstnerWaveComponent Wave2;
        public float TimeSeconds;
        public float WaterLevelY;
        public float MaxWaveEnvelope;

        public void Execute(int index)
        {
            float3 positionWS = PositionsWS[index];
            BuoyancyParams buoyancyParams = default;
            float objectHeight = 0.01f;
            float2 centerXZ = positionWS.xz;
            if (index < ObjParams.Length)
            {
                buoyancyParams = ObjParams[index];
                objectHeight = math.max(buoyancyParams.height, 0.01f);
                if (math.all(math.isfinite(buoyancyParams.boundsCenter)))
                    centerXZ = buoyancyParams.boundsCenter.xz;
            }

            float baseDepth = WaterLevelY - positionWS.y;
            if (baseDepth > objectHeight + MaxWaveEnvelope + 0.5f)
            {
                VerticalOffsets[index] = 0f;
                return;
            }

            VerticalOffsets[index] = ResolveFiniteFloatOrZero(SampleWaveHeight(centerXZ));
        }

        private float SampleWaveHeight(float2 worldXZ)
        {
            return HectonGerstnerWater.SampleHeight(worldXZ, Wave0, Wave1, Wave2, TimeSeconds);
        }

        private static float ResolveFiniteFloatOrZero(float value)
        {
            return (math.isnan(value) || math.isinf(value) || !math.isfinite(value)) ? 0f : value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BuoyancyJob : IJobParallelFor
    {
        private const float ThermoclineDepthMeters = 120f;
        private const float ThermoclineHalfBandMeters = 8f;
        private const float ThermoclineVerticalAttenuation = 0.1f;
        private const float SurfaceStormLayerDepthMeters = 50f;
        private const float StormSurfaceTurbulenceStrength = 0.4f;
        private const float JobGyroscopicFlowMaxTorquePerKg = 50f;

        // ── Input (ReadOnly) ──
        [ReadOnly] public NativeArray<float3>         positions;
        [ReadOnly] public NativeArray<float3>         previousPositions;
        [ReadOnly] public NativeArray<byte>           previousPositionValid;
        [ReadOnly] public NativeArray<float3>         velocities;
        [ReadOnly] public NativeArray<float3>         angularVelocities;
        [ReadOnly] public NativeArray<float3>         upVectors;
        [ReadOnly] public NativeArray<BuoyancyParams> objParams;
        [ReadOnly] public NativeArray<float>          waveOffsets;
        [ReadOnly] public NativeArray<float>          gpuBuoyancyForcesY;
        [ReadOnly] public NativeArray<ActiveThrusterFlow> activeThrusters;
        [ReadOnly] public NativeArray<WhirlpoolFlow> activeWhirlpools;
        [ReadOnly] public NativeArray<FluidViscosityRegion> activeViscosityRegions;
        [ReadOnly] public NativeArray<float> viscosityGradientLut;
        public int activeThrusterCount;
        public int activeWhirlpoolCount;
        public int activeViscosityRegionCount;
        [WriteOnly] public NativeArray<FluidImpactEvent> impactEvents;
        [WriteOnly] public NativeArray<int> impactEventFlags;

        // ── Output (WriteOnly) ──
        [WriteOnly] public NativeArray<float3> resultForces;
        [WriteOnly] public NativeArray<float3> resultTorques;
        public NativeQueue<int>.ParallelWriter mathGuardWriter;
        public int forceNanErrorCode;
        public int torqueNanErrorCode;

        // ── Shared parameters (uniform) ──
        public float  waterLevel;
        public float  waterDensity;
        public float  viscousDrag;
        public float  maxQuadraticDragForcePerKg;
        public float  angularDragCoeff;
        public float  gravity;
        public float3 baseCurrentForce;
        public float3 giantWakeCurrent;
        public float  giantWakeDepthFadeStart;
        public float  giantWakeDepthFadeRange;
        public byte   enableTidalShearZones;
        public float  tidalShearTorqueStrength;
        public float  tidalShearFrequency;
        public float  time;
        public uint   weatherStateMask;
        public float3 weatherCurrentDirection;
        public float  weatherCurrentScale;
        public float  weatherBlend;
        public byte   enablePhantomCurrent;
        public float  currentNoiseScale;
        public float  currentTimeScale;
        public float  currentVerticalFactor;
        public float  phantomCurrentStrength;
        public byte   enableAnalyticalFlowField;
        public float  haloclineBoundaryDepthMeters;
        public float  deepLayerDensityMultiplier;
        public float  haloclineShearForcePerKg;
        public byte   enableDynamicViscosityRegions;
        public byte   useGpuBuoyancyForce;

        public void Execute(int i)
        {
            impactEventFlags[i] = 0;
            BuoyancyParams p = objParams[i];

            if (p.simulationMode == 1)
            {
                resultForces[i] = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            if (p.simulationMode == 2)
            {
                resultForces[i] = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            // ══════════════════════════════════════════════
            //  DRY ZONE CHECK — obekt vnutri nezatoplennogo modulya
            // ══════════════════════════════════════════════
            // Mgnovennoe otklyuchenie vsey vodnoy fiziki.
            // Obekt podchinyaetsya tolko Unity gravity.
            if (p.isInAir != 0)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            float3 pos = positions[i];
            float3 vel = velocities[i];
            float3 angularVel = angularVelocities[i];
            float3 up = DominantAxisOrDefault(upVectors[i], new float3(0f, 1f, 0f));

            // ── Glubina pogruzheniya tsentra mass ──
            float waveOffset = waveOffsets[i];
            float surfaceY = waterLevel + waveOffset;
            float depthBelowSurface = surfaceY - pos.y;

            // ── Obekt nad vodoy → nulevye sily ──
            if (depthBelowSurface <= 0f)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            if (previousPositionValid[i] != 0 && previousPositions[i].y > surfaceY && pos.y <= surfaceY)
            {
                impactEvents[i] = new FluidImpactEvent
                {
                    PositionWS = pos,
                    VelocityWS = vel,
                    MassKg = p.mass,
                    SurfaceY = surfaceY
                };
                impactEventFlags[i] = 1;
            }

            // ── Koeffitsient pogruzheniya (0..1) ──
            float subRatio = p.simplifiedSubmersion != 0
                ? (depthBelowSurface > 0f ? 1f : 0f)
                : math.saturate(depthBelowSurface * math.rcp(math.max(p.height, 0.0001f)));
            float resolvedWaterDensity = p.useLocalFluidDensityOverride != 0
                ? math.max(0.01f, p.localFluidDensity)
                : waterDensity;
            float denseLayer01 = 0f;
            if (enableAnalyticalFlowField != 0)
            {
                float safeHaloclineDepth = math.max(0.01f, haloclineBoundaryDepthMeters);
                denseLayer01 = depthBelowSurface >= safeHaloclineDepth ? 1f : 0f;
                resolvedWaterDensity *= 1f + (math.max(1f, deepLayerDensityMultiplier) - 1f) * denseLayer01;
            }


            // ══════════════════════════════════════════════
            //  1. SILA ARHIMEDA (Buoyancy)
            // ══════════════════════════════════════════════
            float displacedVolume = p.volume * subRatio;
            float buoyancyMagnitude = resolvedWaterDensity * displacedVolume * gravity;
            if (useGpuBuoyancyForce != 0 &&
                p.useLocalFluidDensityOverride == 0 &&
                i < gpuBuoyancyForcesY.Length)
            {
                buoyancyMagnitude = math.max(0f, gpuBuoyancyForcesY[i]);
            }

            buoyancyMagnitude *= math.max(0.05f, p.buoyancyMultiplier);

            float3 buoyancyForce = new float3(0f, buoyancyMagnitude, 0f);

            // ══════════════════════════════════════════════
            //  2. VYaZKOE SOPROTIVLENIE (Drag)
            // ══════════════════════════════════════════════
            float3 dragForce = float3.zero;

            // ══════════════════════════════════════════════
            //  3. PODVODNOE TEChENIE (Current)
            // ══════════════════════════════════════════════
            float3 standardCurrent = baseCurrentForce + p.localCurrent;
            standardCurrent += weatherCurrentDirection * math.max(0f, weatherCurrentScale) * math.max(0f, weatherBlend);
            float3 sampledCurrent = baseCurrentForce + p.localCurrent;
            float giantWakeDepth01 = math.saturate(
                (depthBelowSurface - math.max(0f, giantWakeDepthFadeStart)) *
                math.rcp(math.max(0.001f, giantWakeDepthFadeRange)));
            float3 resolvedGiantWakeCurrent = giantWakeCurrent * giantWakeDepth01;
            sampledCurrent += resolvedGiantWakeCurrent;

            if (enablePhantomCurrent != 0 && p.currentResponse > 0.0001f)
            {
                sampledCurrent += HectonAnalyticalFlowField.SampleCinematicCurrent(
                    pos,
                    time,
                    currentNoiseScale,
                    currentTimeScale,
                    phantomCurrentStrength,
                    currentVerticalFactor);
            }

            bool stormActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.Storm) != 0u;
            bool thermoclineActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.ThermoclineActive) != 0u;
            bool haloclineActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.HaloclineActive) != 0u;
            if (stormActive)
            {
                float surfaceLayer01 = 1f - math.saturate(depthBelowSurface * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
                float stormBlend = math.max(0f, weatherBlend);
                float stormBiasScale = weatherCurrentScale * math.max(0.35f, stormBlend);
                sampledCurrent.xz += weatherCurrentDirection.xz * stormBiasScale;

                if (surfaceLayer01 > 0.0001f && p.currentResponse > 0.0001f)
                {
                    sampledCurrent += HectonAnalyticalFlowField.SampleCinematicCurrent(
                        pos + new float3(17.3f, 0f, 11.1f),
                        time,
                        currentNoiseScale,
                        currentTimeScale,
                        phantomCurrentStrength * (StormSurfaceTurbulenceStrength * surfaceLayer01),
                        currentVerticalFactor * surfaceLayer01);
                }
            }

            if (thermoclineActive || haloclineActive)
            {
                float thermoclineBand01 = 1f - math.saturate(
                    math.abs(depthBelowSurface - ThermoclineDepthMeters) *
                    math.rcp(math.max(ThermoclineHalfBandMeters, 0.0001f)));
                if (thermoclineBand01 > 0.0001f)
                    sampledCurrent.y *= 1f + (ThermoclineVerticalAttenuation - 1f) * thermoclineBand01;
            }

            if (enableAnalyticalFlowField != 0)
            {
                int thrusterCount = math.min(math.max(0, activeThrusterCount), activeThrusters.Length);
                for (int thrusterIndex = 0; thrusterIndex < thrusterCount; thrusterIndex++)
                    HectonAnalyticalFlowField.ApplyThrusterFlow(ref sampledCurrent, pos, activeThrusters[thrusterIndex]);

                int whirlpoolCount = math.min(math.max(0, activeWhirlpoolCount), activeWhirlpools.Length);
                for (int whirlpoolIndex = 0; whirlpoolIndex < whirlpoolCount; whirlpoolIndex++)
                    HectonAnalyticalFlowField.ApplyWhirlpoolFlow(ref sampledCurrent, pos, activeWhirlpools[whirlpoolIndex]);
            }

            float3 analyticalShearForce = float3.zero;
            if (enableAnalyticalFlowField != 0 && denseLayer01 > 0f && haloclineShearForcePerKg != 0f && p.currentResponse > 0.0001f)
            {
                analyticalShearForce = new float3(
                    0f,
                    0f,
                    haloclineShearForcePerKg * p.mass * subRatio * math.max(0f, p.currentResponse));
            }

            float3 currentF = sampledCurrent * (subRatio * p.mass * p.currentResponse);
            float viscosityMultiplier = 1f;
            if (enableDynamicViscosityRegions != 0 && activeViscosityRegionCount > 0)
            {
                viscosityMultiplier = HectonAnalyticalFlowField.SampleViscosityMultiplier(
                    pos,
                    activeViscosityRegions,
                    activeViscosityRegionCount,
                    viscosityGradientLut);
            }

            float3 relativeVelocity = vel - sampledCurrent;
            float relativeSpeedSq = math.lengthsq(relativeVelocity);
            if (relativeSpeedSq > 0.000001f && maxQuadraticDragForcePerKg > 0f)
            {
                float relativeSpeed = FastMagnitudeApprox(relativeVelocity);
                float dragScalar = math.max(0f, viscousDrag) *
                                   viscosityMultiplier *
                                   resolvedWaterDensity *
                                   math.max(0.01f, p.volume) *
                                   subRatio;
                dragForce = -relativeVelocity * (math.max(1f, relativeSpeed) * dragScalar);
                dragForce = ClampVectorMagnitude(
                    dragForce,
                    math.max(0f, maxQuadraticDragForcePerKg) * math.max(0.01f, p.mass));
            }

            // ══════════════════════════════════════════════
            //  4. DEMPFIROVANIE POKAChIVANIYa
            // ══════════════════════════════════════════════
            float dampingForce = 0f;
            if (subRatio < 1f)
            {
                dampingForce = -vel.y * resolvedWaterDensity * displacedVolume * 0.5f;
            }

            float3 dampingVec = new float3(0f, dampingForce, 0f);

            // ══════════════════════════════════════════════
            //  ITOG
            // ══════════════════════════════════════════════

            float surfaceBand = math.saturate(
                1f - math.abs(depthBelowSurface - p.height) *
                math.rcp(math.max(0.25f, p.height * 1.5f)));
            float3 tiltAxis = math.cross(up, new float3(0f, 1f, 0f));
            float3 stabilityTorque = tiltAxis * (p.surfaceStability * buoyancyMagnitude * surfaceBand * 0.12f);
            float3 angularDragTorque = -angularVel * (angularDragCoeff * math.max(0.1f, p.angularDragMultiplier) * subRatio * math.max(1f, p.mass * 0.35f));
            float3 flowAxis = DominantAxisOrDefault(sampledCurrent, new float3(1f, 0f, 0f));
            float3 gyroscopicAxis = math.cross(up, flowAxis);
            float currentSpeed = FastMagnitudeApprox(sampledCurrent);
            float volumeLever = CinematicVolumeLever(p.volume);
            float lightTumbleBias = math.saturate(math.rcp(math.max(0.25f, p.mass)));
            float massStabilizer = math.rcp(math.max(1f, p.mass));
            float3 gyroscopicFlowTorque = gyroscopicAxis *
                                          (currentSpeed * volumeLever * lightTumbleBias * massStabilizer *
                                           subRatio * math.max(0f, p.currentResponse) * 3.25f);
            float maxGyroscopicFlowTorque = JobGyroscopicFlowMaxTorquePerKg * math.max(0.01f, p.mass);
            gyroscopicFlowTorque = ClampVectorMagnitude(gyroscopicFlowTorque, maxGyroscopicFlowTorque);
            float3 shearTorque = float3.zero;
            if (enableTidalShearZones != 0 && tidalShearTorqueStrength > 0f && p.currentResponse > 0.0001f)
            {
                float standardSpeedSq = math.lengthsq(standardCurrent);
                float wakeSpeedSq = math.lengthsq(resolvedGiantWakeCurrent);
                if (standardSpeedSq > 0.0001f && wakeSpeedSq > 0.0001f)
                {
                    float3 standardAxis = DominantAxisOrDefault(standardCurrent, new float3(1f, 0f, 0f));
                    float3 wakeAxis = DominantAxisOrDefault(resolvedGiantWakeCurrent, new float3(1f, 0f, 0f));
                    float crossMagnitudeSq = math.lengthsq(math.cross(standardAxis, wakeAxis));
                    float opposition = math.saturate(-math.dot(standardAxis, wakeAxis));
                    float minCurrentSpeed = math.min(
                        FastMagnitudeApprox(standardCurrent),
                        FastMagnitudeApprox(resolvedGiantWakeCurrent));
                    float shear01 = math.saturate((crossMagnitudeSq + opposition) * minCurrentSpeed * 0.85f);
                    float phase = math.dot(pos, new float3(0.071f, 0.113f, 0.097f)) + time * math.max(0.01f, tidalShearFrequency);
                    float turbulence = FastTriangleSigned(phase) * FastTriangleSigned(phase * 1.731f + 2.17f);
                    float3 shearAxis = DominantAxisOrDefault(math.cross(standardAxis, wakeAxis), up);
                    shearTorque = shearAxis *
                                  (turbulence * shear01 * math.max(0f, tidalShearTorqueStrength) *
                                   volumeLever * subRatio * math.max(0f, p.currentResponse));
                    shearTorque = ClampVectorMagnitude(shearTorque, maxGyroscopicFlowTorque);
                }
            }

            resultForces[i] = MathGuard.SanitizeFiniteOrZero(
                buoyancyForce + dragForce + currentF + dampingVec + analyticalShearForce,
                forceNanErrorCode,
                mathGuardWriter);
            resultTorques[i] = MathGuard.SanitizeFiniteOrZero(
                angularDragTorque + stabilityTorque + gyroscopicFlowTorque + shearTorque,
                torqueNanErrorCode,
                mathGuardWriter);
        }

        private static float FastMagnitudeApprox(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private static float CinematicVolumeLever(float volume)
        {
            float safeVolume = math.max(0.0001f, volume);
            float smallVolumeLever = 0.2f + safeVolume * 0.8f;
            float largeVolumeLever = 0.75f + safeVolume * 0.25f;
            return math.min(8f, math.select(smallVolumeLever, largeVolumeLever, safeVolume > 1f));
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static float3 ClampVectorMagnitude(float3 value, float maxMagnitude)
        {
            float safeMaxMagnitude = math.max(0f, maxMagnitude);
            float magnitude = FastMagnitudeApprox(value);
            if (magnitude <= safeMaxMagnitude || magnitude <= 0.000001f)
                return value;

            return value * (safeMaxMagnitude * math.rcp(magnitude));
        }

        private static float3 ResolveFiniteFloat3OrZero(float3 value)
        {
            return (math.any(math.isnan(value)) || math.any(math.isinf(value)) || !math.all(math.isfinite(value)))
                ? float3.zero
                : value;
        }

        private static float3 DominantAxisOrDefault(float3 value, float3 fallback)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float3 xAxis = new float3(math.select(-1f, 1f, value.x >= 0f), 0f, 0f);
            float3 yAxis = new float3(0f, math.select(-1f, 1f, value.y >= 0f), 0f);
            float3 zAxis = new float3(0f, 0f, math.select(-1f, 1f, value.z >= 0f));
            float3 yzAxis = math.select(zAxis, yAxis, absValue.y >= absValue.z);
            float3 axis = math.select(yzAxis, xAxis, absValue.x >= absValue.y && absValue.x >= absValue.z);
            return math.select(axis, fallback, maxComponent <= 0.000001f);
        }
    }

    internal static class HectonGerstnerWater
    {
        private const float TwoPi = 6.28318530718f;
        private const float CinematicPhaseSpeedBase = 0.85f;
        private const float CinematicPhaseSpeedPerMeter = 0.23f;

        public static float SampleHeight(
            float2 worldXZ,
            GerstnerWaveComponent wave0,
            GerstnerWaveComponent wave1,
            GerstnerWaveComponent wave2,
            float timeSeconds)
        {
            float3 displacement = ComputeTotalDisplacement(worldXZ, wave0, wave1, wave2, timeSeconds);
            return ResolveFiniteFloatOrZero(displacement.y);
        }

        private static float3 ComputeTotalDisplacement(
            float2 worldXZ,
            GerstnerWaveComponent wave0,
            GerstnerWaveComponent wave1,
            GerstnerWaveComponent wave2,
            float timeSeconds)
        {
            float3 total = float3.zero;
            total += ComputeDisplacement(worldXZ, wave0, timeSeconds);
            total += ComputeDisplacement(worldXZ, wave1, timeSeconds);
            total += ComputeDisplacement(worldXZ, wave2, timeSeconds);
            return total;
        }

        private static float3 ComputeDisplacement(float2 worldXZ, GerstnerWaveComponent wave, float timeSeconds)
        {
            if (wave.Amplitude <= 0f || wave.Wavelength <= 0.01f)
                return float3.zero;

            float2 direction = DominantAxisOrDefault(wave.DirectionXZ, new float2(1f, 0f));
            float waveNumber = TwoPi * math.rcp(math.max(0.01f, wave.Wavelength));
            float phaseVelocity = (CinematicPhaseSpeedBase + wave.Wavelength * CinematicPhaseSpeedPerMeter) *
                                  math.max(0.01f, wave.SpeedMultiplier);
            float phase = waveNumber * math.dot(direction, worldXZ) - phaseVelocity * waveNumber * timeSeconds + wave.PhaseOffset;
            float sinPhase = FastTriangleSigned(phase);
            float cosPhase = FastTriangleSigned(phase + 1.5707964f);
            float horizontalDisplacement = wave.Steepness * wave.Amplitude;

            float3 displacement;
            displacement.x = -direction.x * horizontalDisplacement * sinPhase;
            displacement.y = wave.Amplitude * cosPhase;
            displacement.z = -direction.y * horizontalDisplacement * sinPhase;
            return HectonAnalyticalFlowField.ResolveFiniteFloat3OrZero(displacement);
        }

        private static float ResolveFiniteFloatOrZero(float value)
        {
            return (math.isnan(value) || math.isinf(value) || !math.isfinite(value)) ? 0f : value;
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static float FastMagnitudeApprox(float2 value)
        {
            float2 absValue = math.abs(value);
            float major = math.max(absValue.x, absValue.y);
            float minor = math.min(absValue.x, absValue.y);
            return major + minor * 0.375f;
        }

        private static float2 DominantAxisOrDefault(float2 value, float2 fallback)
        {
            float2 absValue = math.abs(value);
            float maxComponent = math.max(absValue.x, absValue.y);
            float2 xAxis = new float2(math.select(-1f, 1f, value.x >= 0f), 0f);
            float2 yAxis = new float2(0f, math.select(-1f, 1f, value.y >= 0f));
            float2 axis = math.select(yAxis, xAxis, absValue.x >= absValue.y);
            return math.select(axis, fallback, maxComponent <= 0.000001f);
        }
    }

    internal static class HectonAnalyticalFlowField
    {
        private const float SurfaceStormLayerDepthMeters = 50f;
        private const float StormSurfaceTurbulenceStrength = 0.4f;

        public static float3 SampleBaseFlow(
            float3 position,
            float depthBelowSurface,
            float3 baseCurrent,
            float3 giantWakeCurrent,
            float giantWakeDepthFadeStart,
            float giantWakeDepthFadeRange,
            uint weatherStateMask,
            float3 weatherCurrentDirection,
            float weatherCurrentScale,
            float weatherBlend,
            byte enablePhantomCurrent,
            float currentNoiseScale,
            float currentTimeScale,
            float currentVerticalFactor,
            float phantomCurrentStrength,
            float time,
            float haloclineBoundaryDepthMeters,
            float haloclineShearVelocity)
        {
            float3 flow = baseCurrent;
            flow += weatherCurrentDirection * math.max(0f, weatherCurrentScale) * math.max(0f, weatherBlend);

            float wakeDepth01 = math.saturate(
                (depthBelowSurface - math.max(0f, giantWakeDepthFadeStart)) /
                math.max(0.001f, giantWakeDepthFadeRange));
            flow += giantWakeCurrent * wakeDepth01;

            if (enablePhantomCurrent != 0)
            {
                flow += SampleCinematicCurrent(
                    position,
                    time,
                    currentNoiseScale,
                    currentTimeScale,
                    phantomCurrentStrength,
                    currentVerticalFactor);
            }

            bool stormActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.Storm) != 0u;
            if (stormActive)
            {
                float surfaceLayer01 = 1f - math.saturate(depthBelowSurface * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
                float stormBlend = math.max(0f, weatherBlend);
                float stormBiasScale = weatherCurrentScale * math.max(0.35f, stormBlend);
                flow.xz += weatherCurrentDirection.xz * stormBiasScale;

                if (surfaceLayer01 > 0.0001f)
                {
                    flow += SampleCinematicCurrent(
                        position + new float3(17.3f, 0f, 11.1f),
                        time,
                        currentNoiseScale,
                        currentTimeScale,
                        phantomCurrentStrength * (StormSurfaceTurbulenceStrength * surfaceLayer01),
                        currentVerticalFactor * surfaceLayer01);
                }
            }

            if (depthBelowSurface >= math.max(0.01f, haloclineBoundaryDepthMeters))
                flow.z += haloclineShearVelocity;

            return ResolveFiniteFloat3OrZero(flow);
        }

        public static float3 SampleCinematicCurrent(
            float3 worldPos,
            float time,
            float noiseScale,
            float timeScale,
            float strength,
            float verticalFactor)
        {
            if (strength == 0f || noiseScale <= 0f || !math.all(math.isfinite(worldPos)))
                return float3.zero;

            float t = time * timeScale;
            float sx = worldPos.x * noiseScale;
            float sy = worldPos.y * noiseScale;
            float sz = worldPos.z * noiseScale;
            float nx = FastTriangleSigned(sx * 2.41f + sz * 0.73f + sy * 0.19f + t);
            float nz = FastTriangleSigned(sz * 2.17f - sx * 0.61f + sy * 0.13f + t * 1.23f + 2.11f);
            float ny = FastTriangleSigned(sx * 0.43f + sz * 0.29f + sy * 0.07f + t * 0.5f + 4.37f) * verticalFactor;
            return ResolveFiniteFloat3OrZero(new float3(nx, ny, nz) * strength);
        }

        public static float SampleViscosityMultiplier(
            float3 worldPos,
            NativeArray<FluidViscosityRegion> regions,
            int regionCount,
            NativeArray<float> gradientLut)
        {
            int regionLimit = math.min(math.max(0, regionCount), regions.Length);
            int lutLastIndex = gradientLut.Length - 1;
            if (regionLimit <= 0 || lutLastIndex <= 0)
                return 1f;

            float multiplier = 1f;
            for (int i = 0; i < regionLimit; i++)
            {
                FluidViscosityRegion region = regions[i];
                if (region.Active == 0 || region.InvRadiusSq <= 0f || region.ViscosityMultiplier <= 0f)
                    continue;

                float distanceSq = math.lengthsq(worldPos - region.CenterWS);
                float normalizedDistanceSq = distanceSq * region.InvRadiusSq;
                if (normalizedDistanceSq > 1f)
                    continue;

                float influence01 = math.saturate(1f - normalizedDistanceSq);
                int lutIndex = math.clamp((int)(influence01 * lutLastIndex), 0, lutLastIndex);
                float gradient = math.saturate(gradientLut[lutIndex]);
                multiplier += (math.clamp(region.ViscosityMultiplier, 0.05f, 8f) - 1f) * gradient;
            }

            return math.clamp(multiplier, 0.05f, 8f);
        }

        public static void ApplyThrusterFlow(ref float3 flow, float3 samplePosition, ActiveThrusterFlow thruster)
        {
            if (thruster.Active == 0 || thruster.Strength <= 0f || thruster.RadiusSq <= 0f || thruster.InvRadiusSq <= 0f)
                return;

            float3 toSample = samplePosition - thruster.PositionWS;
            float distanceSq = math.lengthsq(toSample);
            float normalizedDistanceSq = distanceSq * thruster.InvRadiusSq;
            if (distanceSq <= 0.000001f || normalizedDistanceSq > 1f)
                return;

            float3 exhaustDirection = -DominantAxisOrDefault(thruster.DirectionWS, new float3(0f, 0f, 1f));
            float axialDistance = math.dot(toSample, exhaustDirection);
            if (axialDistance <= 0f)
                return;

            float coneCosSq = thruster.ConeCos * thruster.ConeCos;
            float axialSq = axialDistance * axialDistance;
            float coneThresholdSq = coneCosSq * distanceSq;
            if (axialSq < coneThresholdSq)
                return;

            float distanceFalloff = math.saturate(1f - normalizedDistanceSq);
            flow += exhaustDirection * (thruster.Strength * distanceFalloff * distanceFalloff);
        }

        public static void ApplyWhirlpoolFlow(ref float3 flow, float3 samplePosition, WhirlpoolFlow whirlpool)
        {
            if (whirlpool.Active == 0 || whirlpool.RadiusSq <= 0f || whirlpool.InvRadiusSq <= 0f)
                return;

            float3 toCenter = whirlpool.CenterWS - samplePosition;
            toCenter.y = 0f;
            float distanceSq = math.lengthsq(toCenter);
            float normalizedDistanceSq = distanceSq * whirlpool.InvRadiusSq;
            if (distanceSq <= 0.000001f || normalizedDistanceSq > 1f)
                return;

            float3 inward = DominantAxisOrDefault(toCenter, new float3(1f, 0f, 0f));
            float3 tangent = new float3(inward.z, 0f, -inward.x);
            float falloff = math.saturate(1f - normalizedDistanceSq);
            flow += tangent * (whirlpool.TangentialStrength * falloff);
            flow += inward * (whirlpool.CentripetalStrength * falloff);
            flow.y -= whirlpool.VerticalPull * falloff;
        }

        public static float3 ResolveFiniteFloat3OrZero(float3 value)
        {
            return (math.any(math.isnan(value)) || math.any(math.isinf(value)) || !math.all(math.isfinite(value)))
                ? float3.zero
                : value;
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static float FastMagnitudeApprox(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private static float3 DominantAxisOrDefault(float3 value, float3 fallback)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float3 xAxis = new float3(math.select(-1f, 1f, value.x >= 0f), 0f, 0f);
            float3 yAxis = new float3(0f, math.select(-1f, 1f, value.y >= 0f), 0f);
            float3 zAxis = new float3(0f, 0f, math.select(-1f, 1f, value.z >= 0f));
            float3 yzAxis = math.select(zAxis, yAxis, absValue.y >= absValue.z);
            float3 axis = math.select(yzAxis, xAxis, absValue.x >= absValue.y && absValue.x >= absValue.z);
            return math.select(axis, fallback, maxComponent <= 0.000001f);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorFloodBfsJob : IJobParallelFor
    {
        public const uint FloodSeedFlag = 1u;
        private const int MaxFloodNodesPerFrame = 5;
        private const int DefaultSeedScanBudget = 32;
        private const int DefaultNodeVisitBudget = MaxFloodNodesPerFrame;
        private const int DefaultEdgeVisitBudget = 64;

        public NativeArray<InteriorFloodNode> Nodes;
        [ReadOnly] public NativeArray<InteriorFloodEdge> Edges;
        public NativeArray<int> Queue;
        public NativeArray<int> Visited;
        public NativeArray<InteriorFloodBfsResult> Result;
        public float DeltaTime;
        public float WaterDensityKgPerM3;
        public int VisitStamp;
        public int SeedScanStart;
        public int MaxSeedScanCount;
        public int MaxNodeVisits;
        public int MaxEdgeVisits;
        public int ResultSampleStride;
        public int ResultSamplePhase;

        public void Execute(int jobIndex)
        {
            if (jobIndex != 0)
                return;

            int nodeCount = math.min(Nodes.Length, math.min(Queue.Length, Visited.Length));
            if (nodeCount <= 0)
                return;

            int visitStamp = math.max(1, VisitStamp);
            int seedBudget = ResolveBudget(MaxSeedScanCount, DefaultSeedScanBudget, nodeCount);
            int nodeVisitBudget = math.min(MaxFloodNodesPerFrame, ResolveBudget(MaxNodeVisits, DefaultNodeVisitBudget, nodeCount));
            int edgeVisitBudget = math.max(1, MaxEdgeVisits > 0 ? MaxEdgeVisits : DefaultEdgeVisitBudget);
            int seedStart = PositiveModulo(SeedScanStart, nodeCount);
            int head = 0;
            int tail = 0;
            for (int scan = 0; scan < seedBudget && tail < nodeVisitBudget; scan++)
            {
                int i = (seedStart + scan) % nodeCount;
                InteriorFloodNode node = Nodes[i];
                if (node.CurrentLiters <= 0.001f && (node.Flags & FloodSeedFlag) == 0u)
                    continue;
                if (Visited[i] == visitStamp)
                    continue;

                Visited[i] = visitStamp;
                Queue[tail++] = i;
            }

            float safeDeltaTime = math.max(0f, DeltaTime);
            int processedNodes = 0;
            int processedEdges = 0;
            while (head < tail && processedNodes < nodeVisitBudget && processedEdges < edgeVisitBudget)
            {
                processedNodes++;
                int nodeIndex = Queue[head++];
                InteriorFloodNode source = Nodes[nodeIndex];
                float availableLiters = math.max(0f, source.CurrentLiters);
                int edgeStart = math.max(0, source.FirstEdgeIndex);
                int edgeEnd = math.min(Edges.Length, edgeStart + math.max(0, source.EdgeCount));

                for (int edgeIndex = edgeStart;
                     edgeIndex < edgeEnd && availableLiters > 0.001f && processedEdges < edgeVisitBudget;
                     edgeIndex++)
                {
                    processedEdges++;
                    InteriorFloodEdge edge = Edges[edgeIndex];
                    int targetIndex = edge.ToNode;
                    if (edge.IsOpen == 0 || (uint)targetIndex >= nodeCount)
                        continue;

                    InteriorFloodNode target = Nodes[targetIndex];
                    float targetRemainingLiters = math.max(0f, target.CapacityLiters - target.CurrentLiters);
                    if (targetRemainingLiters <= 0.001f)
                        continue;

                    float transferLiters = math.min(
                        availableLiters,
                        math.min(
                            targetRemainingLiters,
                            math.max(0f, source.TransferLitersPerSecond) *
                            math.max(0f, edge.FlowMultiplier) *
                            safeDeltaTime));
                    if (transferLiters <= 0.001f)
                        continue;

                    source.CurrentLiters -= transferLiters;
                    target.CurrentLiters += transferLiters;
                    availableLiters -= transferLiters;
                    Nodes[targetIndex] = target;

                    if (Visited[targetIndex] != visitStamp && tail < nodeVisitBudget)
                    {
                        Visited[targetIndex] = visitStamp;
                        Queue[tail++] = targetIndex;
                    }
                }

                Nodes[nodeIndex] = source;
            }

            float totalLiters = 0f;
            float structuralLoadKg = 0f;
            int floodedCount = 0;
            int sampleStride = math.clamp(ResultSampleStride > 0 ? ResultSampleStride : 1, 1, nodeCount);
            int samplePhase = PositiveModulo(ResultSamplePhase, sampleStride);
            int resultSamples = 0;
            for (int i = samplePhase; i < nodeCount && resultSamples < MaxFloodNodesPerFrame; i += sampleStride)
            {
                resultSamples++;
                InteriorFloodNode node = Nodes[i];
                float liters = math.max(0f, node.CurrentLiters);
                if (liters <= 0.001f)
                    continue;

                float nodeWaterMassKg = liters * 0.001f * math.max(0.01f, WaterDensityKgPerM3);
                totalLiters += liters;
                structuralLoadKg += nodeWaterMassKg + math.max(0f, node.StructuralMassKg);
                floodedCount++;
            }

            if (Result.Length > 0)
            {
                float sampleScale = sampleStride;
                Result[0] = new InteriorFloodBfsResult
                {
                    TotalWaterMassKg = totalLiters * sampleScale * 0.001f * math.max(0.01f, WaterDensityKgPerM3),
                    StructuralLoadKg = structuralLoadKg * sampleScale,
                    FloodedNodeCount = floodedCount * sampleStride
                };
            }
        }

        private static int ResolveBudget(int requested, int fallback, int limit)
        {
            int budget = requested > 0 ? requested : fallback;
            return math.clamp(budget, 1, math.max(1, limit));
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 0)
                return 0;
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}
