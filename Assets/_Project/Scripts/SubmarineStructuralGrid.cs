using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.VFX;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    /// <summary>
    /// Read-only structural breach publication contract consumed by submarine flooding systems.
    /// </summary>
    public interface ISubmarineHullBreachReadModel
    {
        /// <summary>True when the structural grid has initialized its native state and published buffers.</summary>
        bool IsReady { get; }

        /// <summary>Number of 64-bit words in the published hull breach mask.</summary>
        int BreachMaskWordCount { get; }

        /// <summary>Current active local-space breach count available for visual repair coupling.</summary>
        int ActiveBreachCount { get; }

        /// <summary>Returns one published 64-bit word from the hull breach mask. Invalid indices return zero.</summary>
        ulong GetHullBreachMaskWord(int wordIndex);

        /// <summary>Returns the published breach area in square meters for a compartment. Invalid indices return zero.</summary>
        float GetCompartmentBreachAreaSquareMeters(int compartmentIndex);

        /// <summary>Returns one active local-space breach as xyz position and w severity. Invalid indices return false.</summary>
        bool TryGetActiveBreach(int index, out Vector4 localPointSeverity);
    }

    /// <summary>
    /// Repair-tool contract for submarine-local breach patching without exposing structural internals.
    /// </summary>
    public interface ISubmarineDamageControlTarget
    {
        /// <summary>Queues a repair hit resolved by the RaycastCommand interaction lane.</summary>
        bool TryQueueRepairHit(Vector3 worldHitPoint, float deltaTime, float repairUnitsPerSecond, float intensity01);
    }

    /// <summary>
    /// Maps repair hits to gas-dynamics room indices without coupling tools to submarine internals.
    /// </summary>
    public interface ISubmarineRepairRoomResolver
    {
        /// <summary>Returns the nearest mapped compartment for a repair hit. Room ids match gas-dynamics room ids.</summary>
        bool TryResolveRepairRoom(Vector3 worldHitPoint, out int roomId);
    }

    /// <summary>
    /// Fixed-step voxelized hull integrity grid with Burst-distributed impact diffusion and double-buffered breach publication.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Submarine Structural Grid")]
    public sealed class SubmarineStructuralGrid : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, Hecton8.Gameplay.IDamageSignalReceiver, ISubmarineHullBreachReadModel, ISubmarineDamageControlTarget, ISubmarineRepairRoomResolver
    {
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.FixedTick");
        private static readonly ProfilerMarker _damageScheduleProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.Damage.Schedule");
        private static readonly ProfilerMarker _damageConsumeProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.Damage.Consume");
        private static readonly ProfilerMarker _breachRepairProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.BreachRepair");
        private static readonly int _ShaderCrushCenterRadiusId = Shader.PropertyToID("_HectonSubmarineCrushCenterRadius");
        private static readonly int _ShaderCrushDepthParamsId = Shader.PropertyToID("_HectonSubmarineCrushDepthParams");
        private static readonly int _LeakBreachBufferId = Shader.PropertyToID("_BreachBuffer");
        private static readonly int _LeakParticleBufferId = Shader.PropertyToID("_LeakPlumeParticleBuffer");
        private static readonly int _LeakBreachCountId = Shader.PropertyToID("_BreachCount");
        private static readonly int _LeakVisibleBreachCountId = Shader.PropertyToID("_VisibleBreachCount");
        private static readonly int _LeakDeltaTimeId = Shader.PropertyToID("_DeltaTimeSeconds");
        private static readonly int _LeakTimeId = Shader.PropertyToID("_TimeSeconds");
        private static readonly int _LeakParamsId = Shader.PropertyToID("_LeakParams");
        private static readonly int _LeakUseParticleBufferId = Shader.PropertyToID("_UseLeakParticleBuffer");
        private static readonly int _LeakParticleSizeId = Shader.PropertyToID("_LeakPlumeParticleSize");
        private static readonly int _LeakLocalToWorldId = Shader.PropertyToID("_SubmarineLocalToWorld");
        private static readonly int _LeakCameraRightId = Shader.PropertyToID("_CameraRightWS");
        private static readonly int _LeakCameraUpId = Shader.PropertyToID("_CameraUpWS");

        private const int CompartmentCapacity = 8;
        private const int MaxQueuedImpacts = 16;
        private const int MaxActiveBreaches = 64;
        private const int DeferredBreachAddCapacity = 16;
        private const int MinVisibleBreachLimit = 8;
        private const int LeakPlumeParticleCapacity = MaxActiveBreaches * 4;
        private const int DamageControlTelemetryCapacity = 300;
        private const byte FullIntegrity = byte.MaxValue;
        private const byte UnmappedCompartment = byte.MaxValue;
        private const float DefaultMinimumImpactRadiusMeters = 0.45f;
        private const float DefaultImpactRadiusPerMeterPerSecond = 0.035f;
        private const float DefaultMinimumSigmaMeters = 0.2f;
        private const float DefaultSigmaScale = 0.45f;
        private const float DefaultImpactSpeedToCellDamageScale = 9f;
        private const float DefaultIntegrityByteToCellDamageScale = 1.15f;
        private const float DefaultFatiguePressureThresholdKPa = 150f;
        private const byte DefaultFatigueIntegrityLossPerCycle = 4;
        private const float DefaultCompressionDepthThresholdMeters = 3000f;
        private const float DefaultCompressionFullPressureKPa = 60000f;
        private const float DefaultMaximumVolumeCompressionNormalized = 0.15f;
        private const float RecentImpactSeverityDecayPerSecond = 2.8f;
        private const float BreachMergeRadiusMeters = 0.75f;
        private const float BreachRepairRadiusMeters = 1f;
        private const float DefaultPressureToBreachSeverityScale = 0.00025f;
        private const float DefaultRepairUnitsToBreachSeverityScale = 0.01f;
        private const float DefaultLeakAudioCadenceSeconds = 0.35f;
        private const uint CriticalBreachWarningHash = 0x43524B4Cu;
        private const byte LeakImpactFlags = 0x20;
        private const float CriticalBreachThreshold = 0.9f;
        private const float CriticalBreachWarningCadenceSeconds = 1.5f;
        private const uint DamageControlTelemetryInvalidFlag = 1u;
        private const uint DamageControlTelemetryRepairJobInFlightFlag = 2u;
        private const uint HullDentVisualDamageType = 3u;
        private const uint HullDentVisualSourceHash = 0xD3CA0149u;
        private const float DefaultLeakPlumeParticleSizeMeters = 0.18f;
        private const float DefaultLeakPlumeRenderBoundsPaddingMeters = 4f;
        private const float LeakPlumeClockMaxSeconds = 16777215f;
        private const float Epsilon = 0.0001f;
        private const string LeakPlumeComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_LeakPlume.compute";
        private const string LeakPlumeMaterialAssetPath = "Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat";
        private const string DamageControlDumpPath = "Docs/AgentLogs/Dump_SUBMARINE_DAMAGE_CONTROL.bin";
        private const string NativeMemoryOwner = nameof(SubmarineStructuralGrid);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullDamageDiffusionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> InputIntegrity;
            [ReadOnly, NoAlias] public NativeArray<byte> CellCompartmentIndices;
            [ReadOnly, NoAlias] public NativeArray<ImpactCommand> Impacts;

            [NoAlias] public NativeArray<byte> OutputIntegrity;
            [NoAlias] public NativeArray<ulong> OutputBreachMaskWords;
            [NoAlias] public NativeArray<float> OutputCompartmentBreachAreas;

            public int GridWidth;
            public int GridHeight;
            public int GridDepth;
            public int CellCount;
            public int ImpactCount;
            public float3 GridCenterLocal;
            public float3 GridSizeLocal;
            public float CellBreachAreaSquareMeters;

            public void Execute()
            {
                for (int i = 0; i < CellCount; i++)
                    OutputIntegrity[i] = InputIntegrity[i];

                for (int i = 0; i < OutputBreachMaskWords.Length; i++)
                    OutputBreachMaskWords[i] = 0UL;

                for (int i = 0; i < OutputCompartmentBreachAreas.Length; i++)
                    OutputCompartmentBreachAreas[i] = 0f;

                float3 cellSize = new float3(
                    GridWidth > 0 ? GridSizeLocal.x / GridWidth : 0f,
                    GridHeight > 0 ? GridSizeLocal.y / GridHeight : 0f,
                    GridDepth > 0 ? GridSizeLocal.z / GridDepth : 0f);

                float3 gridMin = GridCenterLocal - (GridSizeLocal * 0.5f);

                for (int impactIndex = 0; impactIndex < ImpactCount; impactIndex++)
                {
                    ImpactCommand impact = Impacts[impactIndex];
                    if (impact.DamageBytes <= 0 || impact.RadiusMeters <= Epsilon)
                        continue;

                    float sigmaMeters = math.max(impact.SigmaMeters, Epsilon);
                    float invTwoSigmaSq = 1f / (2f * sigmaMeters * sigmaMeters);
                    float radiusSq = impact.RadiusMeters * impact.RadiusMeters;

                    int minX = math.max(0, (int)math.floor((impact.LocalPoint.x - impact.RadiusMeters - gridMin.x) / math.max(cellSize.x, Epsilon)));
                    int maxX = math.min(GridWidth - 1, (int)math.floor((impact.LocalPoint.x + impact.RadiusMeters - gridMin.x) / math.max(cellSize.x, Epsilon)));
                    int minY = math.max(0, (int)math.floor((impact.LocalPoint.y - impact.RadiusMeters - gridMin.y) / math.max(cellSize.y, Epsilon)));
                    int maxY = math.min(GridHeight - 1, (int)math.floor((impact.LocalPoint.y + impact.RadiusMeters - gridMin.y) / math.max(cellSize.y, Epsilon)));
                    int minZ = math.max(0, (int)math.floor((impact.LocalPoint.z - impact.RadiusMeters - gridMin.z) / math.max(cellSize.z, Epsilon)));
                    int maxZ = math.min(GridDepth - 1, (int)math.floor((impact.LocalPoint.z + impact.RadiusMeters - gridMin.z) / math.max(cellSize.z, Epsilon)));

                    for (int z = minZ; z <= maxZ; z++)
                    {
                        float localZ = gridMin.z + ((z + 0.5f) * cellSize.z);
                        for (int y = minY; y <= maxY; y++)
                        {
                            float localY = gridMin.y + ((y + 0.5f) * cellSize.y);
                            int yzBase = (z * GridHeight + y) * GridWidth;
                            for (int x = minX; x <= maxX; x++)
                            {
                                float localX = gridMin.x + ((x + 0.5f) * cellSize.x);
                                float3 delta = new float3(localX, localY, localZ) - impact.LocalPoint;
                                float distSq = math.lengthsq(delta);
                                if (distSq > radiusSq)
                                    continue;

                                int cellIndex = yzBase + x;
                                int currentIntegrity = OutputIntegrity[cellIndex];
                                if (currentIntegrity <= 0)
                                    continue;

                                float weight = ApproximateExpNegPositive(distSq * invTwoSigmaSq);
                                int damage = (int)math.round(impact.DamageBytes * weight);
                                if (damage <= 0)
                                    continue;

                                OutputIntegrity[cellIndex] = (byte)math.max(0, currentIntegrity - damage);
                            }
                        }
                    }
                }

                for (int cellIndex = 0; cellIndex < CellCount; cellIndex++)
                {
                    if (OutputIntegrity[cellIndex] > 0)
                        continue;

                    int wordIndex = cellIndex >> 6;
                    int bitIndex = cellIndex & 63;
                    OutputBreachMaskWords[wordIndex] |= 1UL << bitIndex;

                    byte compartmentIndex = CellCompartmentIndices[cellIndex];
                    if (compartmentIndex < OutputCompartmentBreachAreas.Length)
                        OutputCompartmentBreachAreas[compartmentIndex] += CellBreachAreaSquareMeters;
                }
            }

            private static float ApproximateExpNegPositive(float x)
            {
                float clamped = math.clamp(x, 0f, 8f);
                float x2 = clamped * clamped;
                float x3 = x2 * clamped;
                float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
                float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
                return math.saturate(numerator / math.max(denominator, Epsilon));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullCompartmentMappingJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> CompartmentCentroids;
            [NoAlias] public NativeArray<byte> CellCompartmentIndices;

            public int CompartmentCount;
            public int GridWidth;
            public int GridHeight;
            public int GridDepth;
            public float3 GridCenterLocal;
            public float3 GridSizeLocal;

            public void Execute(int cellIndex)
            {
                if (CompartmentCount <= 0)
                {
                    CellCompartmentIndices[cellIndex] = UnmappedCompartment;
                    return;
                }

                int x = cellIndex % GridWidth;
                int yz = cellIndex / GridWidth;
                int y = yz % GridHeight;
                int z = yz / GridHeight;
                float3 gridMin = GridCenterLocal - (GridSizeLocal * 0.5f);
                float3 cellSize = new float3(
                    GridWidth > 0 ? GridSizeLocal.x / GridWidth : 0f,
                    GridHeight > 0 ? GridSizeLocal.y / GridHeight : 0f,
                    GridDepth > 0 ? GridSizeLocal.z / GridDepth : 0f);
                float3 cellLocalPoint = gridMin + (new float3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSize);
                byte nearestIndex = UnmappedCompartment;
                float nearestDistanceSq = float.MaxValue;

                for (int compartmentIndex = 0; compartmentIndex < CompartmentCount; compartmentIndex++)
                {
                    float distanceSq = math.lengthsq(cellLocalPoint - CompartmentCentroids[compartmentIndex]);
                    if (distanceSq < nearestDistanceSq)
                    {
                        nearestDistanceSq = distanceSq;
                        nearestIndex = (byte)compartmentIndex;
                    }
                }

                CellCompartmentIndices[cellIndex] = nearestIndex;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullFatigueCompartmentJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> CellCompartmentIndices;
            [NoAlias] public NativeArray<byte> CellIntegrityFront;
            [NoAlias] public NativeArray<byte> CellIntegrityBack;
            [NoAlias] public NativeArray<byte> CellFatigue;
            [NoAlias] public NativeArray<byte> FatigueCompartmentFlags;
            [NoAlias] public NativeArray<float> FatigueIntegrityLossPerCycle;
            [NoAlias] public NativeArray<float> PeakNormalized;

            public int CellCount;

            public void Execute()
            {
                float peak = PeakNormalized.Length > 0 ? PeakNormalized[0] : 0f;
                for (int cellIndex = 0; cellIndex < CellCount; cellIndex++)
                {
                    byte compartmentIndex = CellCompartmentIndices[cellIndex];
                    if (compartmentIndex >= FatigueCompartmentFlags.Length ||
                        FatigueCompartmentFlags[compartmentIndex] == 0)
                    {
                        continue;
                    }

                    byte fatigue = CellFatigue[cellIndex];
                    if (fatigue < byte.MaxValue)
                        fatigue++;

                    CellFatigue[cellIndex] = fatigue;
                    peak = math.max(peak, fatigue / (float)byte.MaxValue);
                    float scaledIntegrityLossPerCycle = math.max(0f, FatigueIntegrityLossPerCycle[compartmentIndex]);
                    int integrityCap = math.max(0, (int)math.floor(FullIntegrity - (fatigue * scaledIntegrityLossPerCycle)));
                    byte cappedIntegrity = (byte)integrityCap;
                    if (CellIntegrityFront[cellIndex] > cappedIntegrity)
                        CellIntegrityFront[cellIndex] = cappedIntegrity;

                    if (CellIntegrityBack[cellIndex] > cappedIntegrity)
                        CellIntegrityBack[cellIndex] = cappedIntegrity;
                }

                for (int i = 0; i < FatigueCompartmentFlags.Length; i++)
                    FatigueCompartmentFlags[i] = 0;

                if (PeakNormalized.Length > 0)
                    PeakNormalized[0] = peak;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ImpactCommand
        {
            [FieldOffset(0)]
            public float3 LocalPoint;
            [FieldOffset(12)]
            public float RadiusMeters;
            [FieldOffset(16)]
            public float SigmaMeters;
            [FieldOffset(20)]
            public int DamageBytes;
            [FieldOffset(24)]
            private ulong _pad0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BreachRepairJob : IJob
        {
            [NoAlias] public NativeArray<float4> Breaches;
            [NoAlias] public NativeArray<float> SeveritySum;
            public int ActiveCount;
            public float3 LocalHitPoint;
            public float RepairDelta;
            public float RepairRadiusSq;

            public void Execute()
            {
                float sum = 0f;
                int count = math.clamp(ActiveCount, 0, Breaches.Length);
                for (int i = 0; i < count; i++)
                {
                    float4 breach = Breaches[i];
                    float severity = math.max(0f, breach.w);
                    if (severity > 0f)
                    {
                        float3 breachDelta = new float3(breach.x, breach.y, breach.z) - LocalHitPoint;
                        if (math.lengthsq(breachDelta) <= RepairRadiusSq)
                            severity = math.max(0f, severity - RepairDelta);

                        breach.w = severity;
                        Breaches[i] = breach;
                    }

                    sum += severity;
                }

                if (SeveritySum.IsCreated && SeveritySum.Length > 0)
                    SeveritySum[0] = math.isfinite(sum) ? sum : 0f;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct DamageControlTelemetryEntry
        {
            [FieldOffset(0)]
            public float3 FirstBreachLocal;
            [FieldOffset(12)]
            public float SeveritySum;
            [FieldOffset(16)]
            public ushort ActiveBreachCount;
            [FieldOffset(18)]
            public ushort VisibleBreachCount;
            [FieldOffset(20)]
            public uint Frame;
            [FieldOffset(24)]
            public uint Flags;
            [FieldOffset(28)]
            public uint StateHash;
        }

        [Header("Grid Authoring")]
        [Tooltip("Voxel columns along the submarine local X axis.")]
        [SerializeField, Min(1)] private int gridWidth = 16;
        [Tooltip("Voxel rows along the submarine local Y axis.")]
        [SerializeField, Min(1)] private int gridHeight = 6;
        [Tooltip("Voxel slices along the submarine local Z axis.")]
        [SerializeField, Min(1)] private int gridDepth = 8;
        [Tooltip("Center of the structural grid in submarine-local space.")]
        [SerializeField] private Vector3 localGridCenter = Vector3.zero;
        [Tooltip("Size of the structural grid in submarine-local space.")]
        [SerializeField] private Vector3 localGridSize = new Vector3(6f, 2.5f, 14f);
        [Tooltip("When enabled, the grid bounds derive from the cached hull collider at startup.")]
        [SerializeField] private bool deriveGridBoundsFromHullCollider = true;

        [Header("Damage Diffusion")]
        [Tooltip("Minimum spherical damage radius in meters for any structural impact.")]
        [SerializeField, Min(0.05f)] private float minimumImpactRadiusMeters = DefaultMinimumImpactRadiusMeters;
        [Tooltip("Additional damage radius in meters added per impact speed unit.")]
        [SerializeField, Min(0f)] private float impactRadiusPerMeterPerSecond = DefaultImpactRadiusPerMeterPerSecond;
        [Tooltip("Minimum Gaussian sigma in meters.")]
        [SerializeField, Min(0.01f)] private float minimumSigmaMeters = DefaultMinimumSigmaMeters;
        [Tooltip("Sigma scale applied to the resolved impact radius.")]
        [SerializeField, Range(0.1f, 1f)] private float sigmaScale = DefaultSigmaScale;
        [Tooltip("Cell-integrity damage contributed by one meter per second of collision speed.")]
        [SerializeField, Min(0f)] private float impactSpeedToCellDamageScale = DefaultImpactSpeedToCellDamageScale;
        [Tooltip("Cell-integrity damage contributed by one integrity byte from the incoming damage signal.")]
        [SerializeField, Min(0f)] private float integrityByteToCellDamageScale = DefaultIntegrityByteToCellDamageScale;

        [Header("Hull Impact Decals")]
        [Tooltip("Minimum projected dent decal size in meters for heavy impacts.")]
        [SerializeField, Min(0.05f)] private float minimumDentDecalSizeMeters = 0.35f;
        [Tooltip("Additional projected decal size added at full heavy-impact severity.")]
        [SerializeField, Min(0f)] private float dentDecalSizeFromSeverityMeters = 0.95f;
        [Tooltip("Projection depth of the dent decal volume in meters.")]
        [SerializeField, Min(0.01f)] private float dentDecalProjectionDepthMeters = 0.18f;
        [Tooltip("Surface-normal offset used to avoid decal z-fighting.")]
        [SerializeField, Min(0f)] private float dentDecalSurfaceOffsetMeters = 0.015f;
        [Tooltip("Lifetime before the pooled decal is returned.")]
        [SerializeField, Min(0.1f)] private float dentDecalLifetimeSeconds = 4f;
        [Tooltip("Optional shared material for GPU-instanced visual-only hull impact sparks.")]
        [SerializeField] private Material hullImpactSparkMaterial;
        [Tooltip("Maximum pooled spark particles reserved for submarine hull impacts.")]
        [SerializeField, Min(8)] private int hullImpactSparkMaxParticles = 192;
        [Tooltip("Maximum visual spark burst emitted at full impact severity.")]
        [SerializeField, Range(1, 64)] private int hullImpactSparkMaxBurstCount = 50;

        [Header("References")]
        [Tooltip("Optional authored hull collider used for automatic local bounds fitting.")]
        [SerializeField] private Collider hullCollider;
        [Tooltip("Optional authored submarine fluid owner consuming published breach areas.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;
        [Tooltip("Optional authored atmosphere owner used for pressure-cycle fatigue.")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;
        [Tooltip("Compute kernel that expands the packed hull breach buffer into GPU leak plume particles.")]
        [SerializeField] private ComputeShader leakPlumeCompute;
        [Tooltip("Material using HECTON/VFX/LeakPlume. Required to draw GPU leak plume points emitted by the compute kernel.")]
        [SerializeField] private Material leakPlumeRenderMaterial;

        [Header("Damage Control")]
        [Tooltip("Pressure-to-severity scalar for packed hull breaches. Local breach logic remains capped at 64 entries.")]
        [SerializeField, Min(0f)] private float pressureToBreachSeverityScale = DefaultPressureToBreachSeverityScale;
        [Tooltip("Repair-tool units converted to breach severity reduction per second.")]
        [SerializeField, Min(0f)] private float repairUnitsToBreachSeverityScale = DefaultRepairUnitsToBreachSeverityScale;
        [Tooltip("Seconds between water-leak impact/audio signals while any breach remains active.")]
        [SerializeField, Min(0.05f)] private float leakAudioCadenceSeconds = DefaultLeakAudioCadenceSeconds;
        [Tooltip("World-space billboard size for each GPU leak plume point.")]
        [SerializeField, Min(0.01f)] private float leakPlumeParticleSizeMeters = DefaultLeakPlumeParticleSizeMeters;
        [Tooltip("Extra world-space render-bound padding around the submarine while leak plumes are active.")]
        [SerializeField, Min(0f)] private float leakPlumeRenderBoundsPaddingMeters = DefaultLeakPlumeRenderBoundsPaddingMeters;

        [Header("Fatigue")]
        [Tooltip("Pressure threshold in kPa that counts as one full pressurization cycle.")]
        [SerializeField, Min(0f)] private float fatiguePressureThresholdKPa = DefaultFatiguePressureThresholdKPa;
        [Tooltip("Permanent integrity bytes lost each time a compartment crosses into the high-pressure band.")]
        [SerializeField, Range(1, 32)] private byte fatigueIntegrityLossPerCycle = DefaultFatigueIntegrityLossPerCycle;

        [Header("Abyssal Compression")]
        [Tooltip("Depth threshold in meters where ambient pressure starts compressing compartment volume.")]
        [SerializeField, Min(0f)] private float compressionDepthThresholdMeters = DefaultCompressionDepthThresholdMeters;
        [Tooltip("Hydrostatic pressure in kPa where maximum hull-volume compression is reached.")]
        [SerializeField, Min(1f)] private float compressionFullPressureKPa = DefaultCompressionFullPressureKPa;
        [Tooltip("Maximum normalized compartment-volume loss applied at full crush pressure.")]
        [SerializeField, Range(0f, 0.5f)] private float maximumVolumeCompressionNormalized = DefaultMaximumVolumeCompressionNormalized;

        [Header("Fake Crush Depth")]
        [Tooltip("Depth where visual-only hull buckling reaches full strength.")]
        [SerializeField, Min(1f)] private float fakeCrushDepthMeters = 4000f;
        [Tooltip("Maximum GPU vertex displacement in meters at full fake crush depth.")]
        [SerializeField, Range(0f, 1f)] private float fakeCrushMaxVertexDisplacementMeters = 0.22f;
        [Tooltip("World-space radius around the submarine center affected by fake crush shader globals.")]
        [SerializeField, Min(0f)] private float fakeCrushEffectRadiusMeters = 18f;
        [Tooltip("Voronoi noise scale used by the hull shader fake crush displacement.")]
        [SerializeField, Min(0.001f)] private float fakeCrushVoronoiScale = 0.18f;

        private bool _registered;
        private bool _registeredLateFrame;
        private bool _damageReceiverRegistered;
        private bool _damageJobRunning;
        private bool _nativeStateReady;
        private int _queuedImpactCount;
        private int _scheduledImpactCount;
        private int _mappedCompartmentCount;
        private int _activeBreachCount;
        private int _visibleBreachCount;
        private int _pendingMappedCompartmentCount;
        private int _leakPlumeKernelIndex = -1;
        private int _activeBreachGpuBufferIndex;
        private int _damageControlTelemetryHead;
        private int _deferredBreachAddCount;

        private float _cellBreachAreaSquareMeters;
        private float _fatiguePeakNormalized;
        private float _recentImpactSeverityNormalized;
        private float _activeBreachSeveritySum;
        private float _pendingRepairSeverityDelta;
        private float3 _pendingRepairLocalPoint;
        private float _leakAudioTimer;
        private float _leakPlumeClockSeconds;
        private float _criticalBreachWarningTimer;
        private float _debugCompressionScale = 1f;
        private Vector4 _publishedCrushCenterRadius = new Vector4(float.NaN, 0f, 0f, 0f);
        private Vector4 _publishedCrushDepthParams = new Vector4(float.NaN, 0f, 0f, 0f);
        private JobHandle _damageJobHandle;
        private JobHandle _mappingJobHandle;
        private JobHandle _fatigueJobHandle;
        private JobHandle _breachRepairJobHandle;
        private IDamageSignalEmitter _damageEmitter;
        private Transform _cachedTransform;
        private Rigidbody _cachedHullRigidbody;
        private ParticleSystem _hullImpactSparkParticles;
        private ParticleSystemRenderer _hullImpactSparkRenderer;
        private ParticleSystem.EmitParams _hullImpactSparkEmitParams;
        private MaterialPropertyBlock _leakPlumeDrawProperties;
        private IDataVault _dataVault;
        private VaultGenerationHandle<float4> _breachesHandle;
        private VaultGenerationHandle<DamageControlTelemetryEntry> _damageControlTelemetryHandle;
        private bool _breachRepairJobRunning;
        private bool _pendingRepairQueued;
        private bool _breachGpuDirty;
        private readonly List<MonoBehaviour> _componentSearchBuffer = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour>(4) - local component search scratch for interface-only wiring - owner: SubmarineStructuralGrid

        private NativeArray<byte> _cellIntegrityFront;
        private NativeArray<byte> _cellIntegrityBack;
        private NativeArray<byte> _cellFatigue;
        private NativeArray<byte> _cellCompartmentIndices;
        private NativeArray<ulong> _hullBreachMaskFront;
        private NativeArray<ulong> _hullBreachMaskBack;
        private NativeArray<float> _compartmentBreachAreasFront;
        private NativeArray<float> _compartmentBreachAreasBack;
        private NativeArray<ImpactCommand> _queuedImpacts;
        private NativeArray<ImpactCommand> _scheduledImpacts;
        private NativeArray<float3> _compartmentCentroids;
        private NativeArray<byte> _fatigueCompartmentFlags;
        private NativeArray<float> _fatigueIntegrityLossPerCycle;
        private NativeArray<float> _fatiguePeakResult;
        private NativeArray<float> _breachSeveritySumResult;
        private readonly float4[] _deferredBreachAdds = new float4[DeferredBreachAddCapacity]; // COLD ALLOC: float4[16] - breach adds deferred while Burst repair owns the NativeArray - owner: SubmarineStructuralGrid
        private GraphicsBuffer _breachGpuBufferA;
        private GraphicsBuffer _breachGpuBufferB;
        private GraphicsBuffer _leakPlumeParticleBuffer;
        private bool _mappingJobRunning;
        private bool _fatigueJobRunning;
        // COLD ALLOC: float[8] - previous compartment pressures used to detect fatigue cycles - owner: SubmarineStructuralGrid
        private readonly float[] _previousCompartmentPressuresKPa = new float[CompartmentCapacity];

        /// <inheritdoc />
        public bool IsReady => _nativeStateReady && _cellIntegrityFront.IsCreated;

        /// <inheritdoc />
        public int BreachMaskWordCount => _hullBreachMaskFront.IsCreated ? _hullBreachMaskFront.Length : 0;

        /// <inheritdoc />
        public int ActiveBreachCount => _nativeStateReady && TryResolveBreachBuffer(out var breaches)
            ? math.min(_activeBreachCount, breaches.Length)
            : 0;

        internal float FatiguePeakNormalized => _fatiguePeakNormalized;
        internal float RecentImpactSeverityNormalized => _recentImpactSeverityNormalized;

#if UNITY_EDITOR
        private void OnValidate()
        {
            pressureToBreachSeverityScale = math.max(0f, pressureToBreachSeverityScale);
            repairUnitsToBreachSeverityScale = math.max(0f, repairUnitsToBreachSeverityScale);
            leakAudioCadenceSeconds = math.max(0.05f, leakAudioCadenceSeconds);
            leakPlumeParticleSizeMeters = math.max(0.01f, leakPlumeParticleSizeMeters);
            leakPlumeRenderBoundsPaddingMeters = math.max(0f, leakPlumeRenderBoundsPaddingMeters);
            if (leakPlumeCompute == null)
                leakPlumeCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(LeakPlumeComputeAssetPath);
            if (leakPlumeRenderMaterial == null)
                leakPlumeRenderMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(LeakPlumeMaterialAssetPath);
        }
#endif

        private void Awake()
        {
            CacheReferences();
            // COLD ALLOC: MaterialPropertyBlock[1] - procedural leak plume draw properties for RenderPrimitives - owner: SubmarineStructuralGrid
            _leakPlumeDrawProperties = new MaterialPropertyBlock();
            ResolveGridBounds();
            EnsureNativeState();
            SeedStructuralState();
            EnsureHullImpactSparkParticles();
        }

        private void OnEnable()
        {
            CacheReferences();
            ResolveGridBounds();
            EnsureNativeState();
            SeedStructuralState();
            GlobalRegistry.RegisterSubmarineHullBreach(this);
            TryRegister();
            TryRegisterDamageReceiver();
            EnsureHullImpactSparkParticles();
        }

        private void OnDisable()
        {
            StopHullImpactSparkParticles();
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            ResetFakeCrushDepthGlobals();
            ReleaseLeakPlumeGpuResources();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            StopHullImpactSparkParticles();
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            ResetFakeCrushDepthGlobals();
            ReleaseLeakPlumeGpuResources();
            DisposeNativeStateDeferred();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            using (_fixedTickProfilerMarker.Auto())
            {
                if (!_nativeStateReady || !_cellIntegrityFront.IsCreated)
                    return;

                _recentImpactSeverityNormalized = math.max(
                    0f,
                    _recentImpactSeverityNormalized - math.max(0f, fixedDeltaTime) * RecentImpactSeverityDecayPerSecond);
                if (!EnsureCompartmentMappingReady())
                    return;

                ApplyAbyssalCompression();
                if (!_damageJobRunning)
                    ApplyPressureCycleFatigue();

                if (!_breachRepairJobRunning && _pendingRepairQueued)
                    ScheduleBreachRepairJob();

                if (!_damageJobRunning && !_fatigueJobRunning && _queuedImpactCount > 0)
                    ScheduleDamageJob();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedMappingJob();
            ConsumeCompletedFatigueJob();
            ConsumeCompletedDamageJob();
            ConsumeCompletedBreachRepairJob();
            if (_breachRepairJobRunning)
            {
                WriteDamageControlTelemetry(DamageControlTelemetryRepairJobInFlightFlag, false);
                return;
            }

            FlushDeferredBreachAdds();
            CompactInactiveBreaches();
            PushDamageControlCoupling(fixedDeltaTime);
            DispatchLeakPlumeCompute(fixedDeltaTime);
            WriteDamageControlTelemetry(0u);
        }

        public void LateFrameTick()
        {
            RenderLeakPlumeParticles();
        }

        /// <summary>
        /// Queues a hull-local impact for the next fixed-step diffusion pass.
        /// </summary>
        public void QueueImpactLocal(float3 localPoint, float impactSpeed, byte integrityDelta)
        {
            if (!_nativeStateReady || !_queuedImpacts.IsCreated || _queuedImpactCount >= _queuedImpacts.Length)
                return;

            float radius = math.max(minimumImpactRadiusMeters, impactSpeed * impactRadiusPerMeterPerSecond);
            float sigma = math.max(minimumSigmaMeters, radius * sigmaScale);
            float damageFromImpact = math.max(0f, impactSpeed) * impactSpeedToCellDamageScale;
            float damageFromSignal = integrityDelta * integrityByteToCellDamageScale;
            int damageBytes = (int)math.round(math.clamp(damageFromImpact + damageFromSignal, 1f, FullIntegrity));
            _recentImpactSeverityNormalized = math.max(
                _recentImpactSeverityNormalized,
                math.saturate(damageBytes / (float)FullIntegrity));

            _queuedImpacts[_queuedImpactCount++] = new ImpactCommand
            {
                LocalPoint = localPoint,
                RadiusMeters = radius,
                SigmaMeters = sigma,
                DamageBytes = damageBytes
            };
        }

        /// <summary>
        /// Queues one visual impact decal in hull-local space without spawning projector objects or mutating hull mesh data.
        /// </summary>
        public void QueueHullImpactDecalLocal(float3 localPoint, float3 localNormal, float impactSpeed, float severity01)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 localPointVector = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            Vector3 localNormalVector = new Vector3(localNormal.x, localNormal.y, localNormal.z);
            Vector3 worldPoint = cachedTransform.TransformPoint(localPointVector);
            Vector3 worldNormal = cachedTransform.TransformDirection(localNormalVector);
            SpawnHullImpactSparks(worldPoint, worldNormal, severity01);
            EnqueueHullImpactDecal(worldPoint, worldNormal, impactSpeed, severity01);
        }

        /// <summary>
        /// Queues a visual-only hull impact decal from an external kinematic sweep without mutating mesh data.
        /// </summary>
        public void QueueHullImpactDecalWorld(Vector3 worldPoint, Vector3 outwardNormal, float impactSpeed, float severity01)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            float severity = math.saturate(severity01);
            Vector3 normal = ResolveSafeDirection(outwardNormal, cachedTransform.up);
            EnqueueHullImpactDecal(worldPoint, normal, impactSpeed, severity);
            TriggerHullImpactCameraShake(severity, worldPoint, normal);
        }

        internal static float DebugResolveHullImpactDentDecalSize(
            float minimumSizeMeters,
            float sizeFromSeverityMeters,
            float impactSpeed,
            float severity01)
        {
            float safeSeverity = math.saturate(severity01);
            float size = math.max(minimumSizeMeters, minimumSizeMeters + sizeFromSeverityMeters * safeSeverity);
            return size + math.saturate(math.max(0f, impactSpeed) / 30f) * 0.2f;
        }

        private void SpawnHullImpactSparks(Vector3 worldPoint, Vector3 outwardNormal, float severity01)
        {
            if (_hullImpactSparkParticles == null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 normal = ResolveSafeDirection(outwardNormal, cachedTransform.up);
            Transform sparkTransform = _hullImpactSparkParticles.transform;
            sparkTransform.SetPositionAndRotation(
                worldPoint + normal * math.max(0f, dentDecalSurfaceOffsetMeters),
                Quaternion.LookRotation(normal, ResolveStableDecalUp(normal)));

            float safeSeverity = math.saturate(severity01);
            int maxBurstCount = math.max(1, hullImpactSparkMaxBurstCount);
            int burstCount = math.clamp((int)math.ceil(math.lerp(6f, maxBurstCount, safeSeverity)), 1, maxBurstCount);

            _hullImpactSparkEmitParams.position = sparkTransform.position;
            _hullImpactSparkEmitParams.velocity = normal * math.lerp(1.5f, 4.5f, safeSeverity);
            _hullImpactSparkEmitParams.startLifetime = math.lerp(0.16f, 0.42f, safeSeverity);
            _hullImpactSparkEmitParams.startSize = math.lerp(0.025f, 0.08f, safeSeverity);
            _hullImpactSparkEmitParams.startColor = LerpColorClamped(new Color(1f, 0.45f, 0.08f, 0.85f), Color.white, safeSeverity);
            _hullImpactSparkParticles.Emit(_hullImpactSparkEmitParams, burstCount);
        }

        private void EnqueueHullImpactDecal(Vector3 worldPoint, Vector3 outwardNormal, float impactSpeed, float severity01)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 normal = ResolveSafeDirection(outwardNormal, cachedTransform.up);
            Vector3 position = worldPoint + normal * math.max(0f, dentDecalSurfaceOffsetMeters);
            float size = DebugResolveHullImpactDentDecalSize(
                math.max(0.05f, minimumDentDecalSizeMeters),
                math.max(0f, dentDecalSizeFromSeverityMeters),
                impactSpeed,
                severity01);
            CombatDamageSignal signal = default;
            float3 direction = default;
            direction.x = -normal.x;
            direction.y = -normal.y;
            direction.z = -normal.z;
            signal.ImpactAup = CombatDamageSignalCodec.FromRuntimePoint(position);
            signal.Direction = direction;
            signal.Magnitude = math.max(size * 18f, impactSpeed * 0.2f);
            signal.DamageType = HullDentVisualDamageType;
            signal.TargetHash = unchecked((uint)math.max(1, GetInstanceID()));
            signal.SourceHash = HullDentVisualSourceHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.SourceId = 0;
            signal.TargetId = 0;
            signal.Channel = 0;
            signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
            signal.IntegrityDelta = (byte)math.clamp(math.round(math.saturate(severity01) * 255f), 0f, 255f);
            GlobalSignals.Publish(in signal);
        }

        private static void TriggerHullImpactCameraShake(float severity01, Vector3 worldPoint, Vector3 worldNormal)
        {
            CameraJuiceSignals.PublishImpact(severity01, worldPoint, -worldNormal);
        }

        private void EnsureHullImpactSparkParticles()
        {
            if (_hullImpactSparkParticles != null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            GameObject sparkObject = new GameObject("PFX_SubmarineHull_ImpactSparks"); // COLD ALLOC: GameObject[1] - visual-only hull impact particle owner - owner: SubmarineStructuralGrid
            sparkObject.transform.SetParent(cachedTransform, false);
            _hullImpactSparkParticles = sparkObject.AddComponent<ParticleSystem>(); // COLD ALLOC: ParticleSystem[1] - pooled hull impact sparks - owner: SubmarineStructuralGrid
            _hullImpactSparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main = _hullImpactSparkParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = math.max(8, hullImpactSparkMaxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.38f, 0.06f, 0.9f),
                new Color(1f, 0.92f, 0.66f, 1f));

            ParticleSystem.EmissionModule emission = _hullImpactSparkParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = _hullImpactSparkParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.035f;
            shape.length = 0.08f;

            ParticleSystem.LightsModule lights = _hullImpactSparkParticles.lights;
            lights.enabled = false;
            ParticleSystem.CollisionModule collision = _hullImpactSparkParticles.collision;
            collision.enabled = false;
            ParticleSystem.TrailModule trails = _hullImpactSparkParticles.trails;
            trails.enabled = false;

            if (_hullImpactSparkRenderer != null)
            {
                _hullImpactSparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                _hullImpactSparkRenderer.lengthScale = 2.4f;
                _hullImpactSparkRenderer.velocityScale = 0.08f;
                _hullImpactSparkRenderer.cameraVelocityScale = 0f;
                _hullImpactSparkRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _hullImpactSparkRenderer.receiveShadows = false;
                _hullImpactSparkRenderer.enableGPUInstancing = true;
                if (hullImpactSparkMaterial != null)
                    _hullImpactSparkRenderer.sharedMaterial = hullImpactSparkMaterial;
            }
        }

        private void StopHullImpactSparkParticles()
        {
            if (_hullImpactSparkParticles == null)
                return;

            _hullImpactSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private Vector3 ResolveStableDecalUp(Vector3 normal)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 up = Vector3.Cross(normal, cachedTransform.right);
            if (up.sqrMagnitude <= Epsilon)
                up = Vector3.Cross(normal, cachedTransform.forward);

            return ResolveSafeDirection(up, Vector3.up);
        }

        /// <inheritdoc />
        public ulong GetHullBreachMaskWord(int wordIndex)
        {
            return _hullBreachMaskFront.IsCreated && (uint)wordIndex < (uint)_hullBreachMaskFront.Length
                ? _hullBreachMaskFront[wordIndex]
                : 0UL;
        }

        /// <inheritdoc />
        public float GetCompartmentBreachAreaSquareMeters(int compartmentIndex)
        {
            return _compartmentBreachAreasFront.IsCreated && (uint)compartmentIndex < (uint)_compartmentBreachAreasFront.Length
                ? _compartmentBreachAreasFront[compartmentIndex]
                : 0f;
        }

        /// <inheritdoc />
        public bool TryGetActiveBreach(int index, out Vector4 localPointSeverity)
        {
            localPointSeverity = default;
            if (!_nativeStateReady ||
                !TryResolveBreachBuffer(out var breaches) ||
                (uint)index >= (uint)math.min(_activeBreachCount, breaches.Length))
            {
                return false;
            }

            float4 breach = breaches[index];
            if (breach.w <= 0f || !math.all(math.isfinite(breach)))
                return false;

            localPointSeverity = new Vector4(breach.x, breach.y, breach.z, breach.w);
            return true;
        }

        /// <inheritdoc />
        public bool TryResolveRepairRoom(Vector3 worldHitPoint, out int roomId)
        {
            roomId = -1;
            if (!_nativeStateReady ||
                !_compartmentCentroids.IsCreated ||
                !TryResolveLocalPointAup(worldHitPoint, out float3 localPoint))
            {
                return false;
            }

            if (!EnsureCompartmentMappingReady() || _mappedCompartmentCount <= 0)
                return false;

            float bestDistanceSq = float.MaxValue;
            int bestRoomId = -1;
            int count = math.min(_mappedCompartmentCount, _compartmentCentroids.Length);
            for (int compartmentIndex = 0; compartmentIndex < count; compartmentIndex++)
            {
                float distanceSq = math.lengthsq(localPoint - _compartmentCentroids[compartmentIndex]);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestRoomId = compartmentIndex;
            }

            roomId = bestRoomId;
            return bestRoomId >= 0;
        }

        /// <inheritdoc />
        public bool TryQueueRepairHit(Vector3 worldHitPoint, float deltaTime, float repairUnitsPerSecond, float intensity01)
        {
            if (!_nativeStateReady ||
                !TryResolveBreachBuffer(out var breaches) ||
                _breachRepairJobRunning ||
                _activeBreachCount <= 0 ||
                !IsFiniteVector(worldHitPoint))
            {
                return false;
            }

            if (!TryResolveLocalPointAup(worldHitPoint, out float3 localPoint))
                return false;

            float repairRadiusSq = BreachRepairRadiusMeters * BreachRepairRadiusMeters;
            int count = math.min(_activeBreachCount, breaches.Length);
            for (int i = 0; i < count; i++)
            {
                float4 breach = breaches[i];
                if (breach.w <= 0f)
                    continue;

                float3 breachDelta = new float3(breach.x, breach.y, breach.z) - localPoint;
                if (math.lengthsq(breachDelta) > repairRadiusSq)
                    continue;

                _pendingRepairLocalPoint = localPoint;
                _pendingRepairSeverityDelta = math.max(0f, deltaTime) *
                                              math.max(0f, repairUnitsPerSecond) *
                                              math.max(0f, repairUnitsToBreachSeverityScale) *
                                              math.max(0.1f, math.saturate(intensity01));
                _pendingRepairQueued = _pendingRepairSeverityDelta > 0f;
                return _pendingRepairQueued;
            }

            return false;
        }

        /// <inheritdoc />
        public void OnIntegrityChanged(float prev, float next, Hecton8.Gameplay.HabitatDamageSignal src)
        {
            float damageDelta = math.max(0f, prev - next);
            if (damageDelta <= 0f)
                return;

            QueueImpactLocal(src.localPoint, math.max(src.magnitude, damageDelta * 10f), src.integrityDelta);
        }

        /// <inheritdoc />
        public void OnPowerChanged(float prev, float next, Hecton8.Gameplay.HabitatDamageSignal src) { }

        /// <inheritdoc />
        public void OnClarityChanged(float prev, float next, Hecton8.Gameplay.HabitatDamageSignal src) { }

        /// <inheritdoc />
        public void OnTraumaThresholdCrossed(TraumaLevel level) { }

        /// <inheritdoc />
        public void OnHullBreach(float3 localPoint, float depth, float pressureDelta)
        {
            QueueImpactLocal(localPoint, math.max(pressureDelta, 1f) * 12f, FullIntegrity);
            AddOrRefreshBreachLocal(localPoint, math.saturate(math.max(pressureDelta, 1f) * pressureToBreachSeverityScale));
        }

        private void AddOrRefreshBreachLocal(float3 localPoint, float severity01)
        {
            if (!_nativeStateReady || !TryResolveBreachBuffer(out var breaches) || !math.all(math.isfinite(localPoint)))
                return;

            float severity = math.saturate(severity01);
            if (severity <= 0f)
                return;

            if (_breachRepairJobRunning)
            {
                QueueDeferredBreachAdd(localPoint, severity);
                return;
            }

            int count = math.min(_activeBreachCount, breaches.Length);
            float mergeRadiusSq = BreachMergeRadiusMeters * BreachMergeRadiusMeters;
            for (int i = 0; i < count; i++)
            {
                float4 breach = breaches[i];
                float3 breachDelta = new float3(breach.x, breach.y, breach.z) - localPoint;
                if (math.lengthsq(breachDelta) > mergeRadiusSq)
                    continue;

                _activeBreachSeveritySum -= math.max(0f, breach.w);
                breach.w = math.saturate(math.max(breach.w, severity));
                _activeBreachSeveritySum += breach.w;
                breaches[i] = breach;
                _breachGpuDirty = true;
                RegisterBreachScreenSpaceFeedback(localPoint, breach.w);
                return;
            }

            if (count >= breaches.Length)
                return;

            breaches[count] = new float4(localPoint, severity);
            _activeBreachCount = count + 1;
            _activeBreachSeveritySum += severity;
            _breachGpuDirty = true;
            RegisterBreachScreenSpaceFeedback(localPoint, severity);
        }

        private void QueueDeferredBreachAdd(float3 localPoint, float severity01)
        {
            if (!math.all(math.isfinite(localPoint)))
                return;

            float severity = math.saturate(severity01);
            if (severity <= 0f)
                return;

            float mergeRadiusSq = BreachMergeRadiusMeters * BreachMergeRadiusMeters;
            for (int i = 0; i < _deferredBreachAddCount; i++)
            {
                float4 deferred = _deferredBreachAdds[i];
                float3 deferredDelta = new float3(deferred.x, deferred.y, deferred.z) - localPoint;
                if (math.lengthsq(deferredDelta) > mergeRadiusSq)
                    continue;

                deferred.w = math.saturate(math.max(deferred.w, severity));
                _deferredBreachAdds[i] = deferred;
                return;
            }

            if (_deferredBreachAddCount < _deferredBreachAdds.Length)
            {
                _deferredBreachAdds[_deferredBreachAddCount] = new float4(localPoint, severity);
                _deferredBreachAddCount++;
                return;
            }

            int weakestIndex = 0;
            float weakestSeverity = _deferredBreachAdds[0].w;
            for (int i = 1; i < _deferredBreachAdds.Length; i++)
            {
                float candidateSeverity = _deferredBreachAdds[i].w;
                if (candidateSeverity >= weakestSeverity)
                    continue;

                weakestSeverity = candidateSeverity;
                weakestIndex = i;
            }

            if (severity > weakestSeverity)
                _deferredBreachAdds[weakestIndex] = new float4(localPoint, severity);
        }

        private void FlushDeferredBreachAdds()
        {
            int count = _deferredBreachAddCount;
            if (count <= 0 || _breachRepairJobRunning)
                return;

            _deferredBreachAddCount = 0;
            for (int i = 0; i < count; i++)
            {
                float4 deferred = _deferredBreachAdds[i];
                _deferredBreachAdds[i] = float4.zero;
                AddOrRefreshBreachLocal(new float3(deferred.x, deferred.y, deferred.z), deferred.w);
            }
        }

        private void RegisterBreachScreenSpaceFeedback(float3 localPoint, float severity01)
        {
            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 local = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            Vector3 worldPoint = cachedTransform.TransformPoint(local);
            Vector3 inward = cachedTransform.position - worldPoint;
            if (inward.sqrMagnitude <= Epsilon)
                inward = -cachedTransform.up;

            fluidDecals.RegisterPressureSpray(worldPoint, ResolveSafeDirection(inward, -cachedTransform.up), math.saturate(severity01));
        }

        private void ScheduleBreachRepairJob()
        {
            if (!_pendingRepairQueued || !TryResolveBreachBuffer(out var breaches) || _activeBreachCount <= 0)
            {
                _pendingRepairQueued = false;
                return;
            }

            using (_breachRepairProfilerMarker.Auto())
            {
                if (_breachSeveritySumResult.IsCreated)
                    _breachSeveritySumResult[0] = _activeBreachSeveritySum;

                _breachRepairJobHandle = new BreachRepairJob
                {
                    Breaches = breaches,
                    SeveritySum = _breachSeveritySumResult,
                    ActiveCount = _activeBreachCount,
                    LocalHitPoint = _pendingRepairLocalPoint,
                    RepairDelta = math.max(0f, _pendingRepairSeverityDelta),
                    RepairRadiusSq = BreachRepairRadiusMeters * BreachRepairRadiusMeters
                }.Schedule();

                _breachRepairJobRunning = true;
                _pendingRepairQueued = false;
            }
        }

        private void ConsumeCompletedBreachRepairJob()
        {
            if (!_breachRepairJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _breachRepairJobHandle, false))
                return;

            _breachRepairJobHandle = default;
            _breachRepairJobRunning = false;
            _activeBreachSeveritySum = _breachSeveritySumResult.IsCreated && _breachSeveritySumResult.Length > 0
                ? math.max(0f, _breachSeveritySumResult[0])
                : RecalculateBreachSeveritySum();
            _breachGpuDirty = true;
        }

        private void CompactInactiveBreaches()
        {
            if (!TryResolveBreachBuffer(out var breaches) || _breachRepairJobRunning)
                return;

            int count = math.min(_activeBreachCount, breaches.Length);
            float sum = 0f;
            bool compacted = false;
            int i = 0;
            while (i < count)
            {
                float4 breach = breaches[i];
                if (breach.w > 0f && math.all(math.isfinite(breach)))
                {
                    sum += breach.w;
                    i++;
                    continue;
                }

                int lastIndex = count - 1;
                breaches[i] = breaches[lastIndex];
                breaches[lastIndex] = float4.zero;
                count--;
                compacted = true;
            }

            _activeBreachCount = count;
            _activeBreachSeveritySum = math.isfinite(sum) ? sum : 0f;
            if (compacted)
                _breachGpuDirty = true;
        }

        private float RecalculateBreachSeveritySum()
        {
            if (!TryResolveBreachBuffer(out var breaches))
                return 0f;

            float sum = 0f;
            int count = math.min(_activeBreachCount, breaches.Length);
            for (int i = 0; i < count; i++)
                sum += math.max(0f, breaches[i].w);

            return math.isfinite(sum) ? sum : 0f;
        }

        private void PushDamageControlCoupling(float fixedDeltaTime)
        {
            float severitySum = math.max(0f, _activeBreachSeveritySum);
            if (severitySum <= 0f)
                return;

            float ambientPressureKPa = ResolveAmbientPressureKPa();
            if (fluidDynamics != null)
                fluidDynamics.ApplyDamageControlLeakMass(severitySum * ambientPressureKPa, fixedDeltaTime);

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            _criticalBreachWarningTimer -= safeDeltaTime;
            if (_criticalBreachWarningTimer <= 0f)
            {
                float peakSeverity = ResolvePeakBreachSeverity();
                if (peakSeverity >= CriticalBreachThreshold)
                {
                    PublishCriticalBreachWarning(peakSeverity);
                    _criticalBreachWarningTimer = CriticalBreachWarningCadenceSeconds;
                }
            }

            _leakAudioTimer -= safeDeltaTime;
            if (_leakAudioTimer > 0f)
                return;

            PublishLeakImpactSignal(severitySum, ambientPressureKPa);
            _leakAudioTimer = math.max(0.05f, leakAudioCadenceSeconds);
        }

        private float ResolveAmbientPressureKPa()
        {
            float depthMeters = fluidDynamics != null ? math.max(0f, fluidDynamics.ExternalDepthMeters) : 0f;
            return (depthMeters * HectonPhysicsContract.HydrostaticPressureKPaPerMeter) + HectonSurvivalContract.KPaPerAtmosphere;
        }

        private void PublishLeakImpactSignal(float severitySum, float ambientPressureKPa)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            if (!TryResolveAupFromRuntimeOrigin(cachedTransform.position, out double3 absolute))
                return;

            Rigidbody hullBody = ResolveHullRigidbody();
            ImpactSignal signal = new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute),
                Force = math.max(0f, severitySum * ambientPressureKPa),
                Intensity = math.saturate(severitySum / math.max(1f, MaxActiveBreaches)),
                PrimaryBodyId = hullBody != null ? unchecked((uint)EntityId.ToULong(hullBody.GetEntityId())) : 0u,
                WeightClass = 1,
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = LeakImpactFlags
            };
            GlobalSignals.Publish(in signal);
        }

        private float ResolvePeakBreachSeverity()
        {
            if (!TryResolveBreachBuffer(out var breaches))
                return 0f;

            float peak = 0f;
            int count = math.min(_activeBreachCount, breaches.Length);
            for (int i = 0; i < count; i++)
                peak = math.max(peak, math.max(0f, breaches[i].w));

            return peak;
        }

        private void PublishCriticalBreachWarning(float severity01)
        {
            Rigidbody hullBody = ResolveHullRigidbody();
            VocalWarningSignal warning = new VocalWarningSignal
            {
                WarningHash = VocalWarningHashes.HullBreach,
                SourceId = hullBody != null ? unchecked((uint)EntityId.ToULong(hullBody.GetEntityId())) : 0u,
                Severity01 = math.saturate(severity01),
                CooldownSeconds = CriticalBreachWarningCadenceSeconds,
                Priority = (byte)VocalWarningId.HullBreach,
                Flags = VocalWarningSignalFlags.HabitatIntegrityCompromised
            };
            GlobalSignals.Publish(in warning);
        }

        private void DispatchLeakPlumeCompute(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime)
                ? math.max(0f, fixedDeltaTime)
                : 0f;
            AdvanceLeakPlumeClock(safeFixedDeltaTime);

            if (!TryResolveBreachBuffer(out var breaches) || leakPlumeCompute == null)
                return;

            if (!EnsureLeakPlumeGpuResources())
                return;

            _visibleBreachCount = ResolveVisibleBreachCount();
            bool uploadedThisFrame = false;
            if (_breachGpuDirty)
            {
                _activeBreachGpuBufferIndex ^= 1;
                GraphicsBuffer uploadBuffer = ResolveWritableBreachGpuBuffer();
                if (uploadBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(uploadBuffer, breaches, math.max(1, _activeBreachCount));
                _breachGpuDirty = false;
                uploadedThisFrame = true;
            }

            GraphicsBuffer breachBuffer = ResolveWritableBreachGpuBuffer();
            if (breachBuffer == null || _leakPlumeParticleBuffer == null)
                return;

            if (_visibleBreachCount <= 0 && !uploadedThisFrame)
            {
                Shader.SetGlobalBuffer(_LeakParticleBufferId, _leakPlumeParticleBuffer);
                Shader.SetGlobalInt(_LeakVisibleBreachCountId, 0);
                return;
            }

            leakPlumeCompute.SetBuffer(_leakPlumeKernelIndex, _LeakBreachBufferId, breachBuffer);
            leakPlumeCompute.SetBuffer(_leakPlumeKernelIndex, _LeakParticleBufferId, _leakPlumeParticleBuffer);
            leakPlumeCompute.SetInt(_LeakBreachCountId, _activeBreachCount);
            leakPlumeCompute.SetInt(_LeakVisibleBreachCountId, _visibleBreachCount);
            leakPlumeCompute.SetFloat(_LeakDeltaTimeId, safeFixedDeltaTime);
            leakPlumeCompute.SetFloat(_LeakTimeId, ResolveLeakPlumeClockSeconds());
            leakPlumeCompute.SetVector(_LeakParamsId, new Vector4(LeakPlumeParticleCapacity, MaxActiveBreaches, 0f, 0f));
            leakPlumeCompute.Dispatch(_leakPlumeKernelIndex, 1, 1, 1);
            Shader.SetGlobalBuffer(_LeakParticleBufferId, _leakPlumeParticleBuffer);
            Shader.SetGlobalInt(_LeakVisibleBreachCountId, _visibleBreachCount);
        }

        private void AdvanceLeakPlumeClock(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _leakPlumeClockSeconds = math.min(LeakPlumeClockMaxSeconds, _leakPlumeClockSeconds + deltaTime);
        }

        private float ResolveLeakPlumeClockSeconds()
        {
            return _leakPlumeClockSeconds;
        }

        private void RenderLeakPlumeParticles()
        {
            int instanceCount = math.min(math.max(0, _visibleBreachCount) * 4, LeakPlumeParticleCapacity);
            if (instanceCount <= 0 ||
                leakPlumeRenderMaterial == null ||
                _leakPlumeParticleBuffer == null ||
                _leakPlumeDrawProperties == null)
            {
                return;
            }

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Camera viewCamera = ResolvePlayerCamera();
            Transform cameraTransform = viewCamera != null ? viewCamera.transform : null;
            Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            Vector3 cameraUp = cameraTransform != null ? cameraTransform.up : Vector3.up;

            _leakPlumeDrawProperties.Clear();
            _leakPlumeDrawProperties.SetBuffer(_LeakParticleBufferId, _leakPlumeParticleBuffer);
            _leakPlumeDrawProperties.SetFloat(_LeakUseParticleBufferId, 1f);
            _leakPlumeDrawProperties.SetFloat(_LeakParticleSizeId, math.max(0.01f, leakPlumeParticleSizeMeters));
            _leakPlumeDrawProperties.SetMatrix(_LeakLocalToWorldId, cachedTransform.localToWorldMatrix);
            _leakPlumeDrawProperties.SetVector(_LeakCameraRightId, cameraRight);
            _leakPlumeDrawProperties.SetVector(_LeakCameraUpId, cameraUp);

            RenderParams renderParams = new RenderParams(leakPlumeRenderMaterial)
            {
                worldBounds = ResolveLeakPlumeRenderBounds(cachedTransform),
                matProps = _leakPlumeDrawProperties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
                lightProbeUsage = LightProbeUsage.Off,
                camera = viewCamera
            };
            UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, instanceCount);
        }

        private Bounds ResolveLeakPlumeRenderBounds(Transform cachedTransform)
        {
            Bounds bounds = hullCollider != null
                ? hullCollider.bounds
                : new Bounds(cachedTransform.position, Vector3.one * 4f);
            float padding = math.max(0f, leakPlumeRenderBoundsPaddingMeters + leakPlumeParticleSizeMeters);
            bounds.Expand(padding * 2f);
            return bounds;
        }

        private static Camera ResolvePlayerCamera()
        {
            IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            return playerContext != null ? playerContext.PlayerCamera : null;
        }

        private int ResolveVisibleBreachCount()
        {
            int activeCount = math.clamp(_activeBreachCount, 0, MaxActiveBreaches);
            float quality = ResolveLeakPresentationQuality01();
            float curve = quality * quality * (3f - 2f * quality);
            int visibleBudget = (int)math.round(math.lerp(MinVisibleBreachLimit, MaxActiveBreaches, curve));
            return math.min(activeCount, math.clamp(visibleBudget, MinVisibleBreachLimit, MaxActiveBreaches));
        }

        private static float ResolveLeakPresentationQuality01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private bool EnsureLeakPlumeGpuResources()
        {
            if (leakPlumeCompute == null)
                return false;

            if (_leakPlumeKernelIndex < 0)
            {
                if (!leakPlumeCompute.HasKernel("CSSpawnLeakParticles"))
                    return false;

                _leakPlumeKernelIndex = leakPlumeCompute.FindKernel("CSSpawnLeakParticles");
            }

            if (_breachGpuBufferA == null)
                _breachGpuBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(MaxActiveBreaches);
            if (_breachGpuBufferB == null)
                _breachGpuBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(MaxActiveBreaches);
            if (_leakPlumeParticleBuffer == null)
                _leakPlumeParticleBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(LeakPlumeParticleCapacity);

            return _leakPlumeKernelIndex >= 0 &&
                   _breachGpuBufferA != null &&
                   _breachGpuBufferB != null &&
                   _leakPlumeParticleBuffer != null;
        }

        private GraphicsBuffer ResolveWritableBreachGpuBuffer()
        {
            return (_activeBreachGpuBufferIndex & 1) == 0 ? _breachGpuBufferA : _breachGpuBufferB;
        }

        private void ReleaseLeakPlumeGpuResources()
        {
            ReleaseGraphicsBuffer(ref _breachGpuBufferA);
            ReleaseGraphicsBuffer(ref _breachGpuBufferB);
            ReleaseGraphicsBuffer(ref _leakPlumeParticleBuffer);
            _leakPlumeKernelIndex = -1;
            _activeBreachGpuBufferIndex = 0;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void WriteDamageControlTelemetry(uint reasonFlags)
        {
            WriteDamageControlTelemetry(reasonFlags, true);
        }

        private void WriteDamageControlTelemetry(uint reasonFlags, bool allowNativeBreachRead)
        {
            if (!TryResolveDamageControlTelemetry(out var telemetry) || telemetry.Length <= 0)
                return;

            int index = _damageControlTelemetryHead % telemetry.Length;
            float4 first = allowNativeBreachRead &&
                           TryResolveBreachBuffer(out var breaches) &&
                           _activeBreachCount > 0
                ? breaches[0]
                : float4.zero;
            bool invalid = allowNativeBreachRead &&
                           _activeBreachCount > 0 &&
                           (!math.all(math.isfinite(first)) || !math.isfinite(_activeBreachSeveritySum));
            uint flags = reasonFlags | (invalid ? DamageControlTelemetryInvalidFlag : 0u);
            DamageControlTelemetryEntry entry = new DamageControlTelemetryEntry
            {
                FirstBreachLocal = new float3(first.x, first.y, first.z),
                SeveritySum = math.isfinite(_activeBreachSeveritySum) ? _activeBreachSeveritySum : 0f,
                ActiveBreachCount = (ushort)math.clamp(_activeBreachCount, 0, ushort.MaxValue),
                VisibleBreachCount = (ushort)math.clamp(_visibleBreachCount, 0, ushort.MaxValue),
                Frame = unchecked((uint)Time.frameCount),
                Flags = flags,
                StateHash = BuildDamageControlTelemetryHash(first, _activeBreachSeveritySum, _activeBreachCount, flags)
            };
            telemetry[index] = entry;
            _damageControlTelemetryHead = (_damageControlTelemetryHead + 1) % telemetry.Length;

            if (invalid)
            {
                DumpDamageControlTelemetry();
                _activeBreachSeveritySum = RecalculateBreachSeveritySum();
            }
        }

        private static uint BuildDamageControlTelemetryHash(float4 first, float severitySum, int activeCount, uint flags)
        {
            uint hash = 2166136261u;
            hash = HashDamageControlTelemetry(hash, (uint)math.round(first.x * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.round(first.y * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.round(first.z * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.round(severitySum * 1000f));
            hash = HashDamageControlTelemetry(hash, (uint)math.max(0, activeCount));
            return HashDamageControlTelemetry(hash, flags);
        }

        private static uint HashDamageControlTelemetry(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private void DumpDamageControlTelemetry()
        {
            if (!TryResolveDamageControlTelemetry(out var telemetry))
                return;

            string path = Path.Combine(Application.dataPath, "..", DamageControlDumpPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(telemetry.Length);
                writer.Write(_damageControlTelemetryHead);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    DamageControlTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.FirstBreachLocal.x);
                    writer.Write(entry.FirstBreachLocal.y);
                    writer.Write(entry.FirstBreachLocal.z);
                    writer.Write(entry.SeveritySum);
                    writer.Write(entry.ActiveBreachCount);
                    writer.Write(entry.VisibleBreachCount);
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.StateHash);
                }
            }
        }

        private Rigidbody ResolveHullRigidbody()
        {
            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            Rigidbody registryBody = submarine != null ? submarine.HullRigidbody : null;
            if (registryBody != null)
            {
                _cachedHullRigidbody = registryBody;
                return registryBody;
            }

            if (_cachedHullRigidbody != null)
                return _cachedHullRigidbody;

            if (hullCollider != null && hullCollider.attachedRigidbody != null)
            {
                _cachedHullRigidbody = hullCollider.attachedRigidbody;
                return _cachedHullRigidbody;
            }

            TryGetComponent(out _cachedHullRigidbody);
            return _cachedHullRigidbody;
        }

        private bool TryResolveLocalPointAup(Vector3 worldPoint, out float3 localPoint)
        {
            localPoint = default;
            if (!IsFiniteVector(worldPoint))
                return false;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            if (cachedTransform == null ||
                !IsFiniteVector(cachedTransform.position) ||
                !IsFiniteQuaternion(cachedTransform.rotation))
            {
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(worldPoint, out double3 hitAup) ||
                !TryResolveAupFromRuntimeOrigin(cachedTransform.position, out double3 rootAup))
            {
                return false;
            }

            double3 relativeWorldDouble = hitAup - rootAup;
            if (!math.all(math.isfinite(relativeWorldDouble)))
                return false;

            Vector3 relativeWorld = new Vector3(
                (float)relativeWorldDouble.x,
                (float)relativeWorldDouble.y,
                (float)relativeWorldDouble.z);
            if (!IsFiniteVector(relativeWorld))
                return false;

            Vector3 localVector = Quaternion.Inverse(cachedTransform.rotation) * relativeWorld;
            Vector3 lossyScale = cachedTransform.lossyScale;
            localVector.x /= ResolveSafeScale(lossyScale.x);
            localVector.y /= ResolveSafeScale(lossyScale.y);
            localVector.z /= ResolveSafeScale(lossyScale.z);
            if (!IsFiniteVector(localVector))
                return false;

            localPoint = new float3(localVector.x, localVector.y, localVector.z);
            return math.all(math.isfinite(localPoint));
        }

        private bool TryResolveLocalDirection(Vector3 worldDirection, out float3 localDirection)
        {
            localDirection = default;
            if (!IsFiniteVector(worldDirection))
                return false;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            if (cachedTransform == null || !IsFiniteQuaternion(cachedTransform.rotation))
                return false;

            Vector3 localVector = Quaternion.Inverse(cachedTransform.rotation) * worldDirection;
            if (!IsFiniteVector(localVector))
                return false;

            localDirection = NormalizeSafe(
                new float3(localVector.x, localVector.y, localVector.z),
                new float3(0f, 1f, 0f));
            return math.all(math.isfinite(localDirection));
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (IsFiniteVector(value) && math.isfinite(lengthSq) && lengthSq > Epsilon)
                return value * math.rsqrt(lengthSq);

            float fallbackLengthSq = fallback.sqrMagnitude;
            if (IsFiniteVector(fallback) && math.isfinite(fallbackLengthSq) && fallbackLengthSq > Epsilon)
                return fallback * math.rsqrt(fallbackLengthSq);

            return Vector3.up;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            aup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(aup));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static float ResolveSafeScale(float scale)
        {
            return math.isfinite(scale) && math.abs(scale) > Epsilon ? scale : 1f;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > Epsilon
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static Color LerpColorClamped(Color from, Color to, float t)
        {
            float clampedT = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, clampedT),
                math.lerp(from.g, to.g, clampedT),
                math.lerp(from.b, to.b, clampedT),
                math.lerp(from.a, to.a, clampedT));
        }

        private float3 ResolveOutwardHullNormal(float3 localPoint, float3 candidateNormal)
        {
            float3 outward = localPoint - new float3(localGridCenter.x, localGridCenter.y, localGridCenter.z);
            float3 resolvedNormal = NormalizeSafe(candidateNormal, float3.zero);
            if (math.lengthsq(resolvedNormal) <= Epsilon)
                return NormalizeSafe(outward, new float3(0f, 1f, 0f));

            if (math.dot(resolvedNormal, outward) < 0f)
                resolvedNormal = -resolvedNormal;

            return resolvedNormal;
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (atmosphereSystem == null)
                TryGetComponent(out atmosphereSystem);

            if (hullCollider == null)
                TryGetComponent(out hullCollider);

            ResolveHullRigidbody();

            if (_damageEmitter == null)
            {
                _componentSearchBuffer.Clear();
                GetComponents(_componentSearchBuffer);
                for (int i = 0; i < _componentSearchBuffer.Count; i++)
                {
                    MonoBehaviour component = _componentSearchBuffer[i];
                    if (component is IDamageSignalEmitter emitter)
                    {
                        _damageEmitter = emitter;
                        break;
                    }
                }
            }
        }

        private void ApplyAbyssalCompression()
        {
            if (fluidDynamics == null)
            {
                PublishFakeCrushDepthGlobals(0f);
                return;
            }

            float depthMeters = math.max(0f, fluidDynamics.ExternalDepthMeters);
            PublishFakeCrushDepthGlobals(depthMeters);
            if (depthMeters <= math.max(0f, compressionDepthThresholdMeters))
            {
                _debugCompressionScale = 1f;
                fluidDynamics.SetCompartmentCompressionScale(1f);
                return;
            }

            float hydrostaticPressureKPa =
                (depthMeters * HectonPhysicsContract.HydrostaticPressureKPaPerMeter) + HectonSurvivalContract.KPaPerAtmosphere;
            float startPressureKPa =
                (math.max(0f, compressionDepthThresholdMeters) * HectonPhysicsContract.HydrostaticPressureKPaPerMeter) + HectonSurvivalContract.KPaPerAtmosphere;
            float pressureRangeKPa = math.max(1f, compressionFullPressureKPa - startPressureKPa);
            float compression01 = math.saturate((hydrostaticPressureKPa - startPressureKPa) / pressureRangeKPa);
            float compressionScale = 1f - (compression01 * math.saturate(maximumVolumeCompressionNormalized));
            _debugCompressionScale = compressionScale;
            fluidDynamics.SetCompartmentCompressionScale(compressionScale);
        }

        private void PublishFakeCrushDepthGlobals(float depthMeters)
        {
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 center = cachedTransform.position;
            Vector4 centerRadius = new Vector4(
                center.x,
                center.y,
                center.z,
                math.max(0f, fakeCrushEffectRadiusMeters));
            Vector4 depthParams = new Vector4(
                math.max(0f, depthMeters),
                math.max(1f, fakeCrushDepthMeters),
                math.max(0f, fakeCrushMaxVertexDisplacementMeters),
                math.max(0.001f, fakeCrushVoronoiScale));

            if (Approximately(_publishedCrushCenterRadius, centerRadius) &&
                Approximately(_publishedCrushDepthParams, depthParams))
            {
                return;
            }

            Shader.SetGlobalVector(_ShaderCrushCenterRadiusId, centerRadius);
            Shader.SetGlobalVector(_ShaderCrushDepthParamsId, depthParams);
            _publishedCrushCenterRadius = centerRadius;
            _publishedCrushDepthParams = depthParams;
        }

        private void ResetFakeCrushDepthGlobals()
        {
            Vector4 zero = Vector4.zero;
            Shader.SetGlobalVector(_ShaderCrushCenterRadiusId, zero);
            Shader.SetGlobalVector(_ShaderCrushDepthParamsId, zero);
            _publishedCrushCenterRadius = new Vector4(float.NaN, 0f, 0f, 0f);
            _publishedCrushDepthParams = new Vector4(float.NaN, 0f, 0f, 0f);
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            const float EpsilonSq = 0.0001f;
            Vector4 delta = a - b;
            return delta.sqrMagnitude <= EpsilonSq;
        }

        private void ResolveGridBounds()
        {
            if (!deriveGridBoundsFromHullCollider || hullCollider == null)
                return;

            Bounds bounds = hullCollider.bounds;
            if (!TryResolveLocalPointAup(bounds.center, out float3 localCenterAup))
                return;

            Vector3 localCenter = new Vector3(localCenterAup.x, localCenterAup.y, localCenterAup.z);
            Vector3 localExtents = transform.InverseTransformVector(bounds.extents);
            localGridCenter = localCenter;
            localGridSize = new Vector3(
                math.max(math.abs(localExtents.x) * 2f, 0.5f),
                math.max(math.abs(localExtents.y) * 2f, 0.5f),
                math.max(math.abs(localExtents.z) * 2f, 0.5f));
        }

        private IDataVault ResolveDataVault()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            _dataVault = vault;
            return vault;
        }

        private bool EnsureBreachVaultState()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!TryResolveVaultBuffer(vault, in _breachesHandle, BufferID.SubmarineStructuralBreaches, MaxActiveBreaches, out NativeArray<float4> _))
            {
                if (vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle(BufferID.SubmarineStructuralBreaches, out _breachesHandle))
                        return false;
                }
                else
                {
                    _breachesHandle = vault.GetGenerationHandle<float4>(
                        BufferID.SubmarineStructuralBreaches,
                        MaxActiveBreaches,
                        SystemID.VehiclesPhysics,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!TryResolveVaultBuffer(vault, in _damageControlTelemetryHandle, BufferID.SubmarineDamageControlBlackBox, DamageControlTelemetryCapacity, out NativeArray<DamageControlTelemetryEntry> _))
            {
                if (vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle(BufferID.SubmarineDamageControlBlackBox, out _damageControlTelemetryHandle))
                        return false;
                }
                else
                {
                    _damageControlTelemetryHandle = vault.GetGenerationHandle<DamageControlTelemetryEntry>(
                        BufferID.SubmarineDamageControlBlackBox,
                        DamageControlTelemetryCapacity,
                        SystemID.VehiclesPhysics,
                        NativeArrayOptions.ClearMemory);
                }
            }

            return TryResolveVaultBuffer(vault, in _breachesHandle, BufferID.SubmarineStructuralBreaches, MaxActiveBreaches, out NativeArray<float4> _) &&
                   TryResolveVaultBuffer(vault, in _damageControlTelemetryHandle, BufferID.SubmarineDamageControlBlackBox, DamageControlTelemetryCapacity, out NativeArray<DamageControlTelemetryEntry> _);
        }

        private bool TryResolveBreachBuffer(out NativeArray<float4> breaches)
        {
            breaches = default;
            if (!EnsureBreachVaultState())
                return false;

            IDataVault vault = ResolveDataVault();
            return TryResolveVaultBuffer(vault, in _breachesHandle, BufferID.SubmarineStructuralBreaches, MaxActiveBreaches, out breaches);
        }

        private bool TryResolveDamageControlTelemetry(out NativeArray<DamageControlTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (!EnsureBreachVaultState())
                return false;

            IDataVault vault = ResolveDataVault();
            return TryResolveVaultBuffer(vault, in _damageControlTelemetryHandle, BufferID.SubmarineDamageControlBlackBox, DamageControlTelemetryCapacity, out telemetry);
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void EnsureNativeState()
        {
            if (_cellIntegrityFront.IsCreated)
                return;

            int cellCount = ResolveCellCount();
            int breachWordCount = (cellCount + 63) >> 6;

            // COLD ALLOC: NativeArray<byte>[cellCount] - published hull integrity front buffer - owner: SubmarineStructuralGrid
            _cellIntegrityFront = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[cellCount] - write-side hull integrity back buffer - owner: SubmarineStructuralGrid
            _cellIntegrityBack = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _cellFatigue = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[cellCount] - immutable cell-to-compartment lookup - owner: SubmarineStructuralGrid
            _cellCompartmentIndices = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ulong>[breachWordCount] - published hull breach bitmask front buffer - owner: SubmarineStructuralGrid
            _hullBreachMaskFront = new NativeArray<ulong>(breachWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ulong>[breachWordCount] - write-side hull breach bitmask back buffer - owner: SubmarineStructuralGrid
            _hullBreachMaskBack = new NativeArray<ulong>(breachWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - published per-compartment breach areas - owner: SubmarineStructuralGrid
            _compartmentBreachAreasFront = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - write-side per-compartment breach areas - owner: SubmarineStructuralGrid
            _compartmentBreachAreasBack = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ImpactCommand>[16] - queued impact staging buffer - owner: SubmarineStructuralGrid
            _queuedImpacts = new NativeArray<ImpactCommand>(MaxQueuedImpacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ImpactCommand>[16] - scheduled impact snapshot buffer - owner: SubmarineStructuralGrid
            _scheduledImpacts = new NativeArray<ImpactCommand>(MaxQueuedImpacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] - compartment centroids staged for Burst hull mapping - owner: SubmarineStructuralGrid
            _compartmentCentroids = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[8] - pressure-fatigue compartment flags consumed by Burst job - owner: SubmarineStructuralGrid
            _fatigueCompartmentFlags = new NativeArray<byte>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - pressure-fatigue per-compartment loss scalars consumed by Burst job - owner: SubmarineStructuralGrid
            _fatigueIntegrityLossPerCycle = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[1] - pressure-fatigue peak metric returned by Burst job - owner: SubmarineStructuralGrid
            _fatiguePeakResult = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            EnsureBreachVaultState();
            // COLD ALLOC: NativeArray<float>[1] - Burst repair severity sum return lane - owner: SubmarineStructuralGrid
            _breachSeveritySumResult = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeStateMemorySentinel();

            _nativeStateReady = true;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _mappedCompartmentCount = 0;
            _pendingMappedCompartmentCount = 0;
        }

        private void SeedStructuralState()
        {
            if (!_cellIntegrityFront.IsCreated)
                return;

            int cellCount = _cellIntegrityFront.Length;
            for (int i = 0; i < cellCount; i++)
            {
                _cellIntegrityFront[i] = FullIntegrity;
                _cellIntegrityBack[i] = FullIntegrity;
                _cellFatigue[i] = 0;
                _cellCompartmentIndices[i] = UnmappedCompartment;
            }

            for (int i = 0; i < _hullBreachMaskFront.Length; i++)
            {
                _hullBreachMaskFront[i] = 0UL;
                _hullBreachMaskBack[i] = 0UL;
            }

            for (int i = 0; i < CompartmentCapacity; i++)
            {
                _compartmentBreachAreasFront[i] = 0f;
                _compartmentBreachAreasBack[i] = 0f;
            }

            _cellBreachAreaSquareMeters = ResolveCellBreachAreaSquareMeters();
            _fatiguePeakNormalized = 0f;
            if (_fatiguePeakResult.IsCreated)
                _fatiguePeakResult[0] = 0f;

            if (TryResolveBreachBuffer(out var breaches))
            {
                for (int i = 0; i < breaches.Length; i++)
                    breaches[i] = float4.zero;
            }

            if (_breachSeveritySumResult.IsCreated)
                _breachSeveritySumResult[0] = 0f;

            _recentImpactSeverityNormalized = 0f;
            _activeBreachCount = 0;
            _visibleBreachCount = 0;
            _activeBreachSeveritySum = 0f;
            _pendingRepairQueued = false;
            _breachGpuDirty = true;
            _mappedCompartmentCount = 0;
            EnsureCompartmentMappingReady();
            for (int i = 0; i < CompartmentCapacity; i++)
                _previousCompartmentPressuresKPa[i] = 0f;
        }

        private bool EnsureCompartmentMappingReady()
        {
            if (!_cellCompartmentIndices.IsCreated || fluidDynamics == null || fluidDynamics.CompartmentCount <= 0)
                return true;

            if (_mappingJobRunning)
                return false;

            int compartmentCount = math.min(fluidDynamics.CompartmentCount, CompartmentCapacity);
            if (_mappedCompartmentCount == compartmentCount)
                return true;

            for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
                _compartmentCentroids[compartmentIndex] = fluidDynamics.GetCompartmentCentroid(compartmentIndex);

            _pendingMappedCompartmentCount = compartmentCount;
            _mappingJobHandle = new HullCompartmentMappingJob
            {
                CompartmentCentroids = _compartmentCentroids,
                CellCompartmentIndices = _cellCompartmentIndices,
                CompartmentCount = compartmentCount,
                GridWidth = math.max(1, gridWidth),
                GridHeight = math.max(1, gridHeight),
                GridDepth = math.max(1, gridDepth),
                GridCenterLocal = localGridCenter,
                GridSizeLocal = localGridSize
            }.Schedule(_cellCompartmentIndices.Length, 32);
            _mappingJobRunning = true;
            return false;
        }

        private void ConsumeCompletedMappingJob()
        {
            if (!_mappingJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _mappingJobHandle, false))
                return;

            _mappingJobRunning = false;
            _mappedCompartmentCount = _pendingMappedCompartmentCount;
            _pendingMappedCompartmentCount = 0;
        }

        private void ApplyPressureCycleFatigue()
        {
            if (!_cellIntegrityFront.IsCreated ||
                !_cellFatigue.IsCreated ||
                !_fatigueCompartmentFlags.IsCreated ||
                _fatigueJobRunning ||
                atmosphereSystem == null ||
                fluidDynamics == null)
            {
                return;
            }

            int compartmentCount = math.min(fluidDynamics.CompartmentCount, CompartmentCapacity);
            float thresholdKPa = math.max(0f, fatiguePressureThresholdKPa);
            bool scheduledAny = false;
            for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
            {
                float previousPressure = _previousCompartmentPressuresKPa[compartmentIndex];
                float currentPressure = atmosphereSystem.GetRoomPressureKPa(compartmentIndex);
                _previousCompartmentPressuresKPa[compartmentIndex] = currentPressure;

                if (previousPressure >= thresholdKPa || currentPressure < thresholdKPa)
                    continue;

                float thermalMultiplier = atmosphereSystem.ResolveThermalFatigueMultiplier(compartmentIndex);
                _fatigueCompartmentFlags[compartmentIndex] = 1;
                _fatigueIntegrityLossPerCycle[compartmentIndex] = math.max(0f, fatigueIntegrityLossPerCycle * thermalMultiplier);
                scheduledAny = true;
            }

            if (scheduledAny)
                ScheduleFatigueJob();
        }

        private void ScheduleFatigueJob()
        {
            if (_fatigueJobRunning ||
                !_cellIntegrityFront.IsCreated ||
                !_cellFatigue.IsCreated ||
                !_cellCompartmentIndices.IsCreated ||
                !_fatigueCompartmentFlags.IsCreated)
            {
                return;
            }

            _fatiguePeakResult[0] = _fatiguePeakNormalized;
            _fatigueJobHandle = new HullFatigueCompartmentJob
            {
                CellCompartmentIndices = _cellCompartmentIndices,
                CellIntegrityFront = _cellIntegrityFront,
                CellIntegrityBack = _cellIntegrityBack,
                CellFatigue = _cellFatigue,
                FatigueCompartmentFlags = _fatigueCompartmentFlags,
                FatigueIntegrityLossPerCycle = _fatigueIntegrityLossPerCycle,
                PeakNormalized = _fatiguePeakResult,
                CellCount = _cellIntegrityFront.Length
            }.Schedule();
            _fatigueJobRunning = true;
        }

        private void ConsumeCompletedFatigueJob()
        {
            if (!_fatigueJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _fatigueJobHandle, false))
                return;

            _fatigueJobRunning = false;
            _fatiguePeakNormalized = _fatiguePeakResult.IsCreated
                ? math.max(_fatiguePeakNormalized, _fatiguePeakResult[0])
                : _fatiguePeakNormalized;
        }

        private void ScheduleDamageJob()
        {
            if (_damageJobRunning || !_scheduledImpacts.IsCreated || _queuedImpactCount <= 0)
                return;

            using (_damageScheduleProfilerMarker.Auto())
            {
                _scheduledImpactCount = _queuedImpactCount;
                for (int i = 0; i < _scheduledImpactCount; i++)
                    _scheduledImpacts[i] = _queuedImpacts[i];

                _queuedImpactCount = 0;
                _damageJobHandle = new HullDamageDiffusionJob
                {
                    InputIntegrity = _cellIntegrityFront,
                    CellCompartmentIndices = _cellCompartmentIndices,
                    Impacts = _scheduledImpacts,
                    OutputIntegrity = _cellIntegrityBack,
                    OutputBreachMaskWords = _hullBreachMaskBack,
                    OutputCompartmentBreachAreas = _compartmentBreachAreasBack,
                    GridWidth = gridWidth,
                    GridHeight = gridHeight,
                    GridDepth = gridDepth,
                    CellCount = _cellIntegrityFront.Length,
                    ImpactCount = _scheduledImpactCount,
                    GridCenterLocal = localGridCenter,
                    GridSizeLocal = localGridSize,
                    CellBreachAreaSquareMeters = _cellBreachAreaSquareMeters
                }.Schedule();
                _damageJobRunning = true;
            }
        }

        private void ConsumeCompletedDamageJob()
        {
            if (!_damageJobRunning)
                return;

            using (_damageConsumeProfilerMarker.Auto())
            {
                if (!DispatcherJobSwap.TryComplete(ref _damageJobHandle, false))
                    return;

                _damageJobRunning = false;
                _scheduledImpactCount = 0;

                NativeArray<byte> integrityFront = _cellIntegrityFront;
                _cellIntegrityFront = _cellIntegrityBack;
                _cellIntegrityBack = integrityFront;

                NativeArray<ulong> breachMaskFront = _hullBreachMaskFront;
                _hullBreachMaskFront = _hullBreachMaskBack;
                _hullBreachMaskBack = breachMaskFront;

                NativeArray<float> breachAreaFront = _compartmentBreachAreasFront;
                _compartmentBreachAreasFront = _compartmentBreachAreasBack;
                _compartmentBreachAreasBack = breachAreaFront;
            }
        }

        private void TryRegister()
        {
            if ((_registered && _registeredLateFrame) || !Application.isPlaying)
                return;
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
                _registered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
            }

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered && !_registeredLateFrame)
                return;

            if (_registered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterDamageReceiver()
        {
            if (_damageReceiverRegistered || _damageEmitter == null)
                return;

            _damageEmitter.RegisterDamageReceiver(this);
            _damageReceiverRegistered = true;
        }

        private void TryUnregisterDamageReceiver()
        {
            if (!_damageReceiverRegistered || _damageEmitter == null)
                return;

            _damageEmitter.UnregisterDamageReceiver(this);
            _damageReceiverRegistered = false;
        }

        private void DisposeNativeStateDeferred()
        {
            JobHandle dependency = _damageJobRunning ? _damageJobHandle : default;
            if (_mappingJobRunning)
                dependency = JobHandle.CombineDependencies(dependency, _mappingJobHandle);
            if (_fatigueJobRunning)
                dependency = JobHandle.CombineDependencies(dependency, _fatigueJobHandle);
            if (_breachRepairJobRunning)
                dependency = JobHandle.CombineDependencies(dependency, _breachRepairJobHandle);

            DisposeDeferred(ref _cellIntegrityFront, ref dependency);
            DisposeDeferred(ref _cellIntegrityBack, ref dependency);
            DisposeDeferred(ref _cellFatigue, ref dependency);
            DisposeDeferred(ref _cellCompartmentIndices, ref dependency);
            DisposeDeferred(ref _hullBreachMaskFront, ref dependency);
            DisposeDeferred(ref _hullBreachMaskBack, ref dependency);
            DisposeDeferred(ref _compartmentBreachAreasFront, ref dependency);
            DisposeDeferred(ref _compartmentBreachAreasBack, ref dependency);
            DisposeDeferred(ref _queuedImpacts, ref dependency);
            DisposeDeferred(ref _scheduledImpacts, ref dependency);
            DisposeDeferred(ref _compartmentCentroids, ref dependency);
            DisposeDeferred(ref _fatigueCompartmentFlags, ref dependency);
            DisposeDeferred(ref _fatigueIntegrityLossPerCycle, ref dependency);
            DisposeDeferred(ref _fatiguePeakResult, ref dependency);
            DisposeDeferred(ref _breachSeveritySumResult, ref dependency);
            _breachesHandle = default;
            _damageControlTelemetryHandle = default;
            _dataVault = null;
            _damageJobHandle = default;
            _mappingJobHandle = default;
            _fatigueJobHandle = default;
            _breachRepairJobHandle = default;
            _damageJobRunning = false;
            _mappingJobRunning = false;
            _fatigueJobRunning = false;
            _breachRepairJobRunning = false;
            _nativeStateReady = false;
            _recentImpactSeverityNormalized = 0f;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _mappedCompartmentCount = 0;
            _pendingMappedCompartmentCount = 0;
            _deferredBreachAddCount = 0;
            _activeBreachCount = 0;
            _visibleBreachCount = 0;
            _activeBreachSeveritySum = 0f;
            _pendingRepairQueued = false;
            _breachGpuDirty = true;
        }

        private int ResolveCellCount()
        {
            int safeWidth = math.max(1, gridWidth);
            int safeHeight = math.max(1, gridHeight);
            int safeDepth = math.max(1, gridDepth);
            return safeWidth * safeHeight * safeDepth;
        }

        private float ResolveCellBreachAreaSquareMeters()
        {
            float safeWidth = math.max(1, gridWidth);
            float safeHeight = math.max(1, gridHeight);
            float safeDepth = math.max(1, gridDepth);
            float cellSizeX = math.max(localGridSize.x / safeWidth, Epsilon);
            float cellSizeY = math.max(localGridSize.y / safeHeight, Epsilon);
            float cellSizeZ = math.max(localGridSize.z / safeDepth, Epsilon);
            float areaXY = cellSizeX * cellSizeY;
            float areaXZ = cellSizeX * cellSizeZ;
            float areaYZ = cellSizeY * cellSizeZ;
            return math.min(areaXY, math.min(areaXZ, areaYZ));
        }

        private static void DisposeDeferred<T>(ref NativeArray<T> array, ref JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            dependency = array.Dispose(dependency);
            array = default;
        }

        private void RegisterNativeStateMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_cellIntegrityFront, NativeMemoryOwner, nameof(_cellIntegrityFront), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_cellIntegrityBack, NativeMemoryOwner, nameof(_cellIntegrityBack), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_cellFatigue, NativeMemoryOwner, nameof(_cellFatigue), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_cellCompartmentIndices, NativeMemoryOwner, nameof(_cellCompartmentIndices), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_hullBreachMaskFront, NativeMemoryOwner, nameof(_hullBreachMaskFront), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_hullBreachMaskBack, NativeMemoryOwner, nameof(_hullBreachMaskBack), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_compartmentBreachAreasFront, NativeMemoryOwner, nameof(_compartmentBreachAreasFront), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_compartmentBreachAreasBack, NativeMemoryOwner, nameof(_compartmentBreachAreasBack), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_queuedImpacts, NativeMemoryOwner, nameof(_queuedImpacts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_scheduledImpacts, NativeMemoryOwner, nameof(_scheduledImpacts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_compartmentCentroids, NativeMemoryOwner, nameof(_compartmentCentroids), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_fatigueCompartmentFlags, NativeMemoryOwner, nameof(_fatigueCompartmentFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_fatigueIntegrityLossPerCycle, NativeMemoryOwner, nameof(_fatigueIntegrityLossPerCycle), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_fatiguePeakResult, NativeMemoryOwner, nameof(_fatiguePeakResult), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_breachSeveritySumResult, NativeMemoryOwner, nameof(_breachSeveritySumResult), NativeMemoryLifetime);
        }

    }
}
