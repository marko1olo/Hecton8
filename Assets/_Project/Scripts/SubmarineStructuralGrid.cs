using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        /// <summary>Returns one published 64-bit word from the hull breach mask. Invalid indices return zero.</summary>
        ulong GetHullBreachMaskWord(int wordIndex);

        /// <summary>Returns the published breach area in square meters for a compartment. Invalid indices return zero.</summary>
        float GetCompartmentBreachAreaSquareMeters(int compartmentIndex);
    }

    /// <summary>
    /// Fixed-step voxelized hull integrity grid with Burst-distributed impact diffusion and double-buffered breach publication.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Submarine Structural Grid")]
    public sealed class SubmarineStructuralGrid : MonoBehaviour, IFixedTickable, IPostFixedTickable, IDamageSignalReceiver, ISubmarineHullBreachReadModel
    {
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.FixedTick");
        private static readonly ProfilerMarker _damageScheduleProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.Damage.Schedule");
        private static readonly ProfilerMarker _damageConsumeProfilerMarker = new ProfilerMarker("H8.Submarine.StructuralGrid.Damage.Consume");
        private static readonly int _ShaderCrushCenterRadiusId = Shader.PropertyToID("_HectonSubmarineCrushCenterRadius");
        private static readonly int _ShaderCrushDepthParamsId = Shader.PropertyToID("_HectonSubmarineCrushDepthParams");

        private const int CompartmentCapacity = 8;
        private const int MaxQueuedImpacts = 16;
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
        private const float Epsilon = 0.0001f;
        private const string NativeMemoryOwner = nameof(SubmarineStructuralGrid);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullDamageDiffusionJob : IJob
        {
            [ReadOnly] public NativeArray<byte> InputIntegrity;
            [ReadOnly] public NativeArray<byte> CellCompartmentIndices;
            [ReadOnly] public NativeArray<ImpactCommand> Impacts;

            public NativeArray<byte> OutputIntegrity;
            public NativeArray<ulong> OutputBreachMaskWords;
            public NativeArray<float> OutputCompartmentBreachAreas;

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

                                float weight = math.exp(-distSq * invTwoSigmaSq);
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
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullCompartmentMappingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> CompartmentCentroids;
            public NativeArray<byte> CellCompartmentIndices;

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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullFatigueCompartmentJob : IJob
        {
            [ReadOnly] public NativeArray<byte> CellCompartmentIndices;
            public NativeArray<byte> CellIntegrityFront;
            public NativeArray<byte> CellIntegrityBack;
            public NativeArray<byte> CellFatigue;
            public NativeArray<byte> FatigueCompartmentFlags;
            public NativeArray<float> FatigueIntegrityLossPerCycle;
            public NativeArray<float> PeakNormalized;

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

        private struct ImpactCommand
        {
            public float3 LocalPoint;
            public float RadiusMeters;
            public float SigmaMeters;
            public int DamageBytes;
        }

        [Header("â”€â”€ Grid Authoring â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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

        [Header("â”€â”€ Damage Diffusion â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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
        [Tooltip("Pooled URP decal projector prefab used for heavy impact dent visuals. Null disables dent visuals without touching mesh data.")]
        [SerializeField] private DecalProjector hullImpactDentDecalPrefab;
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
        [Tooltip("Minimum collision kinetic energy before the hull queues structural damage or a visual dent.")]
        [SerializeField, Min(0f)] private float hullCollisionYieldEnergyJoules = 12000f;
        [Tooltip("Kinetic energy at or above this value maps to full dent severity.")]
        [SerializeField, Min(1f)] private float hullCollisionFullDentEnergyJoules = 65000f;
        [Tooltip("Maximum integrity delta contributed by a single heavy hull collision.")]
        [SerializeField, Range(1f, 255f)] private float hullCollisionMaxIntegrityDelta = 96f;
        [Tooltip("Optional shared material for GPU-instanced visual-only hull impact sparks.")]
        [SerializeField] private Material hullImpactSparkMaterial;
        [Tooltip("Maximum pooled spark particles reserved for submarine hull impacts.")]
        [SerializeField, Min(8)] private int hullImpactSparkMaxParticles = 192;
        [Tooltip("Maximum visual spark burst emitted at full impact severity.")]
        [SerializeField, Range(1, 64)] private int hullImpactSparkMaxBurstCount = 34;
        [Tooltip("Optional glowing scratch decal prefab. Falls back to the dent decal prefab when unset.")]
        [SerializeField] private DecalProjector hullImpactScratchDecalPrefab;

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Optional authored hull collider used for automatic local bounds fitting.")]
        [SerializeField] private Collider hullCollider;
        [Tooltip("Optional authored submarine fluid owner consuming published breach areas.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;
        [Tooltip("Optional authored atmosphere owner used for pressure-cycle fatigue.")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Fatigue Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Pressure threshold in kPa that counts as one full pressurization cycle.")]
        [SerializeField, Min(0f)] private float fatiguePressureThresholdKPa = DefaultFatiguePressureThresholdKPa;
        [Tooltip("Permanent integrity bytes lost each time a compartment crosses into the high-pressure band.")]
        [SerializeField, Range(1, 32)] private byte fatigueIntegrityLossPerCycle = DefaultFatigueIntegrityLossPerCycle;

        [Header("── Abyssal Compression ──────────────────")]
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
        private bool _damageReceiverRegistered;
        private bool _damageJobRunning;
        private bool _nativeStateReady;
        private int _queuedImpactCount;
        private int _scheduledImpactCount;
        private int _mappedCompartmentCount;

        private float _cellBreachAreaSquareMeters;
        private float _fatiguePeakNormalized;
        private float _recentImpactSeverityNormalized;
        private float _debugCompressionScale = 1f;
        private Vector4 _publishedCrushCenterRadius = new Vector4(float.NaN, 0f, 0f, 0f);
        private Vector4 _publishedCrushDepthParams = new Vector4(float.NaN, 0f, 0f, 0f);
        private JobHandle _damageJobHandle;
        private JobHandle _mappingJobHandle;
        private JobHandle _fatigueJobHandle;
        private IDamageSignalEmitter _damageEmitter;
        private Transform _cachedTransform;
        private Rigidbody _cachedHullRigidbody;
        private SubmarineHullImpactRelay _hullImpactRelay;
        private ParticleSystem _hullImpactSparkParticles;
        private ParticleSystemRenderer _hullImpactSparkRenderer;
        private ParticleSystem.EmitParams _hullImpactSparkEmitParams;
        private readonly List<MonoBehaviour> _componentSearchBuffer = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour>(4) â€” local component search scratch for interface-only wiring â€” owner: SubmarineStructuralGrid

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
        private bool _mappingJobRunning;
        private bool _fatigueJobRunning;
        private int _pendingMappedCompartmentCount;
        // COLD ALLOC: float[8] Ã¢â‚¬â€ previous compartment pressures used to detect fatigue cycles Ã¢â‚¬â€ owner: SubmarineStructuralGrid
        private readonly float[] _previousCompartmentPressuresKPa = new float[CompartmentCapacity];

        /// <inheritdoc />
        public bool IsReady => _nativeStateReady && _cellIntegrityFront.IsCreated;

        /// <inheritdoc />
        public int BreachMaskWordCount => _hullBreachMaskFront.IsCreated ? _hullBreachMaskFront.Length : 0;

        internal float FatiguePeakNormalized => _fatiguePeakNormalized;
        internal float RecentImpactSeverityNormalized => _recentImpactSeverityNormalized;

        private void Awake()
        {
            CacheReferences();
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
            EnsureHullCollisionRelay();
            EnsureHullImpactSparkParticles();
        }

        private void OnDisable()
        {
            StopHullImpactSparkParticles();
            ClearHullCollisionRelay();
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            ResetFakeCrushDepthGlobals();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            StopHullImpactSparkParticles();
            ClearHullCollisionRelay();
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            ResetFakeCrushDepthGlobals();
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

                if (!_damageJobRunning && !_fatigueJobRunning && _queuedImpactCount > 0)
                    ScheduleDamageJob();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedMappingJob();
            ConsumeCompletedFatigueJob();
            ConsumeCompletedDamageJob();
        }

        private void OnCollisionEnter(Collision collision)
        {
            ProcessHullCollision(collision);
        }

        private void ProcessRelayedHullCollision(Collision collision)
        {
            ProcessHullCollision(collision);
        }

        private void ProcessHullCollision(Collision collision)
        {
            if (!_nativeStateReady ||
                collision == null ||
                collision.contactCount <= 0)
            {
                return;
            }

            Rigidbody hullBody = ResolveHullRigidbody();
            if (hullBody == null)
                return;

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            if (submarine == null || !ReferenceEquals(submarine.HullRigidbody, hullBody))
                return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed <= Epsilon)
                return;

            float effectiveMass = ResolveEffectiveCollisionMass(hullBody, collision.rigidbody);
            float kineticEnergy = 0.5f * effectiveMass * impactSpeed * impactSpeed;
            float yieldEnergy = math.max(Epsilon, hullCollisionYieldEnergyJoules);
            if (kineticEnergy < yieldEnergy)
                return;

            float fullDentEnergy = math.max(yieldEnergy + Epsilon, hullCollisionFullDentEnergyJoules);
            float severity01 = math.saturate((kineticEnergy - yieldEnergy) / (fullDentEnergy - yieldEnergy));
            ContactPoint contact = collision.GetContact(0);
            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 localPointVector = cachedTransform.InverseTransformPoint(contact.point);
            Vector3 localNormalVector = cachedTransform.InverseTransformDirection(contact.normal);
            float3 localPoint = new float3(localPointVector.x, localPointVector.y, localPointVector.z);
            float3 localNormal = ResolveOutwardHullNormal(
                localPoint,
                new float3(localNormalVector.x, localNormalVector.y, localNormalVector.z));
            byte integrityDelta = (byte)math.clamp(
                (int)math.round(math.lerp(1f, math.max(1f, hullCollisionMaxIntegrityDelta), severity01)),
                1,
                255);

            QueueImpactLocal(localPoint, impactSpeed, integrityDelta);
            QueueHullImpactDecalLocal(localPoint, localNormal, impactSpeed, severity01);
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
        /// Spawns one pooled visual impact decal in hull-local space without modifying hull mesh data.
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
            SpawnHullImpactScratchDecal(worldPoint, worldNormal, impactSpeed, severity01);
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
            Vector3 normal = outwardNormal.sqrMagnitude > Epsilon ? outwardNormal.normalized : cachedTransform.up;
            Transform sparkTransform = _hullImpactSparkParticles.transform;
            sparkTransform.SetPositionAndRotation(
                worldPoint + normal * math.max(0f, dentDecalSurfaceOffsetMeters),
                Quaternion.LookRotation(normal, ResolveStableDecalUp(normal)));

            float safeSeverity = math.saturate(severity01);
            int burstCount = Mathf.Clamp(
                Mathf.CeilToInt(math.lerp(6f, math.max(1, hullImpactSparkMaxBurstCount), safeSeverity)),
                1,
                math.max(1, hullImpactSparkMaxBurstCount));

            _hullImpactSparkEmitParams.position = sparkTransform.position;
            _hullImpactSparkEmitParams.velocity = normal * math.lerp(1.5f, 4.5f, safeSeverity);
            _hullImpactSparkEmitParams.startLifetime = math.lerp(0.16f, 0.42f, safeSeverity);
            _hullImpactSparkEmitParams.startSize = math.lerp(0.025f, 0.08f, safeSeverity);
            _hullImpactSparkEmitParams.startColor = Color.Lerp(new Color(1f, 0.45f, 0.08f, 0.85f), Color.white, safeSeverity);
            _hullImpactSparkParticles.Emit(_hullImpactSparkEmitParams, burstCount);
        }

        private void SpawnHullImpactScratchDecal(Vector3 worldPoint, Vector3 outwardNormal, float impactSpeed, float severity01)
        {
            DecalProjector scratchPrefab = hullImpactScratchDecalPrefab != null
                ? hullImpactScratchDecalPrefab
                : hullImpactDentDecalPrefab;
            if (scratchPrefab == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            Vector3 normal = outwardNormal.sqrMagnitude > Epsilon ? outwardNormal.normalized : cachedTransform.up;
            Quaternion rotation = Quaternion.LookRotation(-normal, ResolveStableDecalUp(normal));
            Vector3 position = worldPoint + normal * math.max(0f, dentDecalSurfaceOffsetMeters);
            GameObject instance = pool.Spawn(scratchPrefab, position, rotation);
            if (instance == null || !instance.TryGetComponent(out DecalProjector projector))
            {
                if (instance != null)
                    pool.Despawn(instance);

                return;
            }

            float size = DebugResolveHullImpactDentDecalSize(
                math.max(0.05f, minimumDentDecalSizeMeters),
                math.max(0f, dentDecalSizeFromSeverityMeters),
                impactSpeed,
                severity01);
            projector.size = new Vector3(size, size, math.max(0.01f, dentDecalProjectionDepthMeters));
            projector.pivot = new Vector3(0f, 0f, projector.size.z * 0.5f);
            projector.fadeFactor = math.lerp(0.55f, 1f, math.saturate(severity01));
            pool.Despawn(projector, math.max(0.1f, dentDecalLifetimeSeconds));
        }

        private void EnsureHullImpactSparkParticles()
        {
            if (_hullImpactSparkParticles != null)
                return;

            Transform cachedTransform = _cachedTransform != null ? _cachedTransform : transform;
            _cachedTransform = cachedTransform;
            GameObject sparkObject = new GameObject("PFX_SubmarineHull_ImpactSparks"); // COLD ALLOC: GameObject[1] — visual-only hull impact particle owner — owner: SubmarineStructuralGrid
            sparkObject.transform.SetParent(cachedTransform, false);
            _hullImpactSparkParticles = sparkObject.AddComponent<ParticleSystem>(); // COLD ALLOC: ParticleSystem[1] — pooled hull impact sparks — owner: SubmarineStructuralGrid
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

            return up.sqrMagnitude > Epsilon ? up.normalized : Vector3.up;
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
        public void OnIntegrityChanged(float prev, float next, DamageSignal src)
        {
            float damageDelta = math.max(0f, prev - next);
            if (damageDelta <= 0f)
                return;

            QueueImpactLocal(src.localPoint, math.max(src.magnitude, damageDelta * 10f), src.integrityDelta);
        }

        /// <inheritdoc />
        public void OnPowerChanged(float prev, float next, DamageSignal src) { }

        /// <inheritdoc />
        public void OnClarityChanged(float prev, float next, DamageSignal src) { }

        /// <inheritdoc />
        public void OnTraumaThresholdCrossed(TraumaLevel level) { }

        /// <inheritdoc />
        public void OnHullBreach(float3 localPoint, float depth, float pressureDelta)
        {
            QueueImpactLocal(localPoint, math.max(pressureDelta, 1f) * 12f, FullIntegrity);
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

        private static float ResolveEffectiveCollisionMass(Rigidbody hullBody, Rigidbody otherBody)
        {
            float hullMass = hullBody != null ? math.max(1f, hullBody.mass) : 1f;
            if (otherBody == null || otherBody.isKinematic)
                return hullMass;

            float otherMass = math.max(1f, otherBody.mass);
            return (hullMass * otherMass) / math.max(1f, hullMass + otherMass);
        }

        private float3 ResolveOutwardHullNormal(float3 localPoint, float3 candidateNormal)
        {
            float3 outward = localPoint - new float3(localGridCenter.x, localGridCenter.y, localGridCenter.z);
            float3 resolvedNormal = math.normalizesafe(candidateNormal, float3.zero);
            if (math.lengthsq(resolvedNormal) <= Epsilon)
                return math.normalizesafe(outward, new float3(0f, 1f, 0f));

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
                (depthMeters * 1025f * 9.80665f * 0.001f) + 101.325f;
            float startPressureKPa =
                (math.max(0f, compressionDepthThresholdMeters) * 1025f * 9.80665f * 0.001f) + 101.325f;
            float pressureRangeKPa = math.max(1f, compressionFullPressureKPa - startPressureKPa);
            float compression01 = math.saturate((hydrostaticPressureKPa - startPressureKPa) / pressureRangeKPa);
            float compressionScale = 1f - (compression01 * math.saturate(maximumVolumeCompressionNormalized));
            _debugCompressionScale = compressionScale;
            fluidDynamics.SetCompartmentCompressionScale(compressionScale);
        }

        private void PublishFakeCrushDepthGlobals(float depthMeters)
        {
            Vector3 center = transform.position;
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
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localExtents = transform.InverseTransformVector(bounds.extents);
            localGridCenter = localCenter;
            localGridSize = new Vector3(
                math.max(math.abs(localExtents.x) * 2f, 0.5f),
                math.max(math.abs(localExtents.y) * 2f, 0.5f),
                math.max(math.abs(localExtents.z) * 2f, 0.5f));
        }

        private void EnsureNativeState()
        {
            if (_cellIntegrityFront.IsCreated)
                return;

            int cellCount = ResolveCellCount();
            int breachWordCount = (cellCount + 63) >> 6;

            // COLD ALLOC: NativeArray<byte>[cellCount] â€” published hull integrity front buffer â€” owner: SubmarineStructuralGrid
            _cellIntegrityFront = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[cellCount] â€” write-side hull integrity back buffer â€” owner: SubmarineStructuralGrid
            _cellIntegrityBack = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _cellFatigue = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[cellCount] â€” immutable cell-to-compartment lookup â€” owner: SubmarineStructuralGrid
            _cellCompartmentIndices = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ulong>[breachWordCount] â€” published hull breach bitmask front buffer â€” owner: SubmarineStructuralGrid
            _hullBreachMaskFront = new NativeArray<ulong>(breachWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ulong>[breachWordCount] â€” write-side hull breach bitmask back buffer â€” owner: SubmarineStructuralGrid
            _hullBreachMaskBack = new NativeArray<ulong>(breachWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] â€” published per-compartment breach areas â€” owner: SubmarineStructuralGrid
            _compartmentBreachAreasFront = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] â€” write-side per-compartment breach areas â€” owner: SubmarineStructuralGrid
            _compartmentBreachAreasBack = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ImpactCommand>[16] â€” queued impact staging buffer â€” owner: SubmarineStructuralGrid
            _queuedImpacts = new NativeArray<ImpactCommand>(MaxQueuedImpacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ImpactCommand>[16] â€” scheduled impact snapshot buffer â€” owner: SubmarineStructuralGrid
            _scheduledImpacts = new NativeArray<ImpactCommand>(MaxQueuedImpacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] — compartment centroids staged for Burst hull mapping — owner: SubmarineStructuralGrid
            _compartmentCentroids = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[8] — pressure-fatigue compartment flags consumed by Burst job — owner: SubmarineStructuralGrid
            _fatigueCompartmentFlags = new NativeArray<byte>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — pressure-fatigue per-compartment loss scalars consumed by Burst job — owner: SubmarineStructuralGrid
            _fatigueIntegrityLossPerCycle = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[1] — pressure-fatigue peak metric returned by Burst job — owner: SubmarineStructuralGrid
            _fatiguePeakResult = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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

            _recentImpactSeverityNormalized = 0f;
            _mappedCompartmentCount = 0;
            EnsureCompartmentMappingReady();
            for (int i = 0; i < CompartmentCapacity; i++)
                _previousCompartmentPressuresKPa[i] = 0f;
        }

        private void EnsureHullCollisionRelay()
        {
            Rigidbody hullBody = ResolveHullRigidbody();
            if (hullBody == null || hullBody.gameObject == gameObject)
                return;

            if (_hullImpactRelay != null && _hullImpactRelay.gameObject == hullBody.gameObject)
            {
                _hullImpactRelay.Bind(this);
                return;
            }

            if (!hullBody.TryGetComponent(out SubmarineHullImpactRelay relay))
                relay = hullBody.gameObject.AddComponent<SubmarineHullImpactRelay>(); // COLD ALLOC: SubmarineHullImpactRelay[1] — hull-rigidbody collision forwarding to structural grid — owner: SubmarineStructuralGrid

            relay.Bind(this);
            _hullImpactRelay = relay;
        }

        private void ClearHullCollisionRelay()
        {
            if (_hullImpactRelay == null)
                return;

            _hullImpactRelay.Clear(this);
            _hullImpactRelay = null;
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
            if (_registered || !Application.isPlaying)
                return;
            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registered = false;
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
            _damageJobHandle = default;
            _mappingJobHandle = default;
            _fatigueJobHandle = default;
            _damageJobRunning = false;
            _mappingJobRunning = false;
            _fatigueJobRunning = false;
            _nativeStateReady = false;
            _recentImpactSeverityNormalized = 0f;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _mappedCompartmentCount = 0;
            _pendingMappedCompartmentCount = 0;
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
        }

        private sealed class SubmarineHullImpactRelay : MonoBehaviour
        {
            private SubmarineStructuralGrid _owner;

            public void Bind(SubmarineStructuralGrid owner)
            {
                _owner = owner;
            }

            public void Clear(SubmarineStructuralGrid owner)
            {
                if (ReferenceEquals(_owner, owner))
                    _owner = null;
            }

            private void OnCollisionEnter(Collision collision)
            {
                SubmarineStructuralGrid owner = _owner;
                if (owner == null || !owner.isActiveAndEnabled)
                    return;

                owner.ProcessRelayedHullCollision(collision);
            }
        }
    }
}
