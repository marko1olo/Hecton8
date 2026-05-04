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
        private const int HullDentPositionStrideBytes = 12;
        private const int HullDentNormalStrideBytes = 12;
        private const int HullDentUvStrideBytes = 8;
        private const int HullDentInterleavedStrideBytes = 32;
        private const int HullDentInterleavedNormalOffsetBytes = 12;
        private const int HullDentInterleavedUvOffsetBytes = 24;
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

        private struct ImpactCommand
        {
            public float3 LocalPoint;
            public float RadiusMeters;
            public float SigmaMeters;
            public int DamageBytes;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HullDentJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> InputVertices;
            [ReadOnly] public NativeArray<HullDentCommand> DentCommands;
            public NativeArray<float3> OutputVertices;
            public int DentCount;

            public void Execute(int index)
            {
                float3 vertex = InputVertices[index];

                for (int dentIndex = 0; dentIndex < DentCount; dentIndex++)
                {
                    HullDentCommand dent = DentCommands[dentIndex];
                    if (dent.DepthMeters <= Epsilon || dent.RadiusMeters <= Epsilon)
                        continue;

                    float3 safeNormal = math.normalizesafe(dent.LocalNormal, new float3(0f, 1f, 0f));
                    float3 delta = vertex - dent.LocalPoint;
                    float normalDistance = math.dot(delta, safeNormal);
                    if (normalDistance < -dent.FrontFaceToleranceMeters || normalDistance > dent.RadiusMeters)
                        continue;

                    float3 radial = delta - (safeNormal * normalDistance);
                    float radialSq = math.lengthsq(radial);
                    if (radialSq > dent.RadiusSq)
                        continue;

                    float weight = math.exp(-radialSq * dent.InverseTwoSigmaSq);
                    vertex -= safeNormal * (dent.DepthMeters * weight);
                }

                OutputVertices[index] = vertex;
            }
        }

        private struct HullDentCommand
        {
            public float3 LocalPoint;
            public float3 LocalNormal;
            public float RadiusMeters;
            public float RadiusSq;
            public float DepthMeters;
            public float InverseTwoSigmaSq;
            public float FrontFaceToleranceMeters;
        }

        private struct HullDentVertex
        {
            public float3 Position;
            public float3 Normal;
            public float2 UV;
        }

        private struct HullDentMeshLayout
        {
            public bool Interleaved;
            public int PositionStream;
            public int NormalStream;
            public int UvStream;
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

        [Header("── Hull Denting ──────────────────")]
        [Tooltip("Minimum Gaussian dent radius in meters for heavy impacts.")]
        [SerializeField, Min(0.05f)] private float minimumDentRadiusMeters = 0.35f;
        [Tooltip("Additional dent radius added at full heavy-impact severity.")]
        [SerializeField, Min(0f)] private float dentRadiusFromSeverityMeters = 0.95f;
        [Tooltip("Minimum inward dent depth in meters.")]
        [SerializeField, Min(0.001f)] private float minimumDentDepthMeters = 0.015f;
        [Tooltip("Additional inward dent depth added at full heavy-impact severity.")]
        [SerializeField, Min(0f)] private float dentDepthFromSeverityMeters = 0.18f;
        [Tooltip("Local-space tolerance used to limit denting to vertices near the struck face.")]
        [SerializeField, Min(0.001f)] private float dentFrontFaceToleranceMeters = 0.08f;
        [Tooltip("Minimum collision kinetic energy before the hull queues structural damage or a visual dent.")]
        [SerializeField, Min(0f)] private float hullCollisionYieldEnergyJoules = 12000f;
        [Tooltip("Kinetic energy at or above this value maps to full dent severity.")]
        [SerializeField, Min(1f)] private float hullCollisionFullDentEnergyJoules = 65000f;
        [Tooltip("Maximum integrity delta contributed by a single heavy hull collision.")]
        [SerializeField, Range(1f, 255f)] private float hullCollisionMaxIntegrityDelta = 96f;

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Optional authored hull collider used for automatic local bounds fitting.")]
        [SerializeField] private Collider hullCollider;
        [Tooltip("Optional authored hull visual mesh used for procedural dent publication. Null disables dent rendering.")]
        [SerializeField] private MeshFilter hullDeformMeshFilter;
        [Tooltip("Optional mesh collider updated to the dented runtime hull mesh after publication.")]
        [SerializeField] private MeshCollider hullDeformMeshCollider;
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

        private bool _registered;
        private bool _damageReceiverRegistered;
        private bool _damageJobRunning;
        private bool _dentJobRunning;
        private bool _nativeStateReady;
        private bool _hullDentMeshReady;
        private bool _hullDentWritableMeshDataApplied;
        private int _queuedImpactCount;
        private int _scheduledImpactCount;
        private int _queuedDentCount;
        private int _scheduledDentCount;
        private int _mappedCompartmentCount;
        private int _hullDentIndexCount;
        private int _hullDentSubMeshCount;

        private float _cellBreachAreaSquareMeters;
        private float _fatiguePeakNormalized;
        private float _recentImpactSeverityNormalized;
        private float _debugCompressionScale = 1f;
        private JobHandle _damageJobHandle;
        private JobHandle _dentJobHandle;
        private IDamageSignalEmitter _damageEmitter;
        private Rigidbody _cachedHullRigidbody;
        private SubmarineHullImpactRelay _hullImpactRelay;
        private Mesh _runtimeHullDentMesh;
        private Bounds _hullDentBoundsLocal;
        private SubMeshDescriptor[] _hullDentSubMeshes;
        private readonly List<MeshFilter> _meshFilterSearchBuffer = new List<MeshFilter>(4); // COLD ALLOC: List<MeshFilter>(4) - hull visual search scratch - owner: SubmarineStructuralGrid
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
        private NativeArray<HullDentCommand> _queuedDentCommands;
        private NativeArray<HullDentCommand> _scheduledDentCommands;
        private NativeArray<float3> _hullDentVerticesFront;
        private NativeArray<float3> _hullDentVerticesBack;
        private NativeArray<float3> _hullDentNormals;
        private NativeArray<float2> _hullDentUvs;
        private NativeArray<uint> _hullDentIndices;
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
            EnsureHullDentRuntime();
        }

        private void OnEnable()
        {
            CacheReferences();
            ResolveGridBounds();
            EnsureNativeState();
            SeedStructuralState();
            EnsureHullDentRuntime();
            GlobalRegistry.RegisterSubmarineHullBreach(this);
            TryRegister();
            TryRegisterDamageReceiver();
            EnsureHullCollisionRelay();
        }

        private void OnDisable()
        {
            ClearHullCollisionRelay();
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            ClearHullCollisionRelay();
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
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
                RefreshCompartmentMapping();
                ApplyAbyssalCompression();
                ApplyPressureCycleFatigue();

                if (!_dentJobRunning && _queuedDentCount > 0)
                    ScheduleHullDentJob();

                if (!_damageJobRunning && _queuedImpactCount > 0)
                    ScheduleDamageJob();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedDamageJob();
            ConsumeCompletedHullDentJob();
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
            Transform cachedTransform = transform;
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
            QueueHullDentLocal(localPoint, localNormal, impactSpeed, severity01);
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
        /// Queues one hull-local Gaussian dent for the next dent publication pass.
        /// </summary>
        public void QueueHullDentLocal(float3 localPoint, float3 localNormal, float impactSpeed, float severity01)
        {
            if (!_hullDentMeshReady ||
                !_queuedDentCommands.IsCreated ||
                _queuedDentCount >= _queuedDentCommands.Length)
            {
                return;
            }

            float safeSeverity = math.saturate(severity01);
            float radiusMeters = math.max(
                minimumDentRadiusMeters,
                minimumDentRadiusMeters + (dentRadiusFromSeverityMeters * safeSeverity));
            radiusMeters += math.saturate(math.max(0f, impactSpeed) / 30f) * 0.2f;
            float sigmaMeters = math.max(minimumSigmaMeters, radiusMeters * sigmaScale);
            float depthMeters = math.max(
                minimumDentDepthMeters,
                minimumDentDepthMeters + (dentDepthFromSeverityMeters * safeSeverity));
            float3 safeNormal = math.normalizesafe(localNormal, new float3(0f, 1f, 0f));

            _queuedDentCommands[_queuedDentCount++] = new HullDentCommand
            {
                LocalPoint = localPoint,
                LocalNormal = safeNormal,
                RadiusMeters = radiusMeters,
                RadiusSq = radiusMeters * radiusMeters,
                DepthMeters = depthMeters,
                InverseTwoSigmaSq = 1f / (2f * sigmaMeters * sigmaMeters),
                FrontFaceToleranceMeters = math.max(0.005f, dentFrontFaceToleranceMeters)
            };
        }

        internal static float3 DebugEvaluateHullDentVertex(
            float3 vertex,
            float3 localPoint,
            float3 localNormal,
            float radiusMeters,
            float depthMeters,
            float sigmaMeters,
            float frontFaceToleranceMeters)
        {
            if (depthMeters <= Epsilon || radiusMeters <= Epsilon)
                return vertex;

            float3 safeNormal = math.normalizesafe(localNormal, new float3(0f, 1f, 0f));
            float3 delta = vertex - localPoint;
            float normalDistance = math.dot(delta, safeNormal);
            if (normalDistance < -frontFaceToleranceMeters || normalDistance > radiusMeters)
                return vertex;

            float3 radial = delta - safeNormal * normalDistance;
            float radialSq = math.lengthsq(radial);
            float radiusSq = radiusMeters * radiusMeters;
            if (radialSq > radiusSq)
                return vertex;

            float safeSigma = math.max(sigmaMeters, Epsilon);
            float weight = math.exp(-radialSq / (2f * safeSigma * safeSigma));
            return vertex - safeNormal * (depthMeters * weight);
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
            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (atmosphereSystem == null)
                TryGetComponent(out atmosphereSystem);

            if (hullCollider == null)
                TryGetComponent(out hullCollider);

            ResolveHullRigidbody();

            if (hullDeformMeshFilter == null)
            {
                _meshFilterSearchBuffer.Clear();
                GetComponentsInChildren(true, _meshFilterSearchBuffer);
                for (int i = 0; i < _meshFilterSearchBuffer.Count; i++)
                {
                    MeshFilter candidate = _meshFilterSearchBuffer[i];
                    if (candidate == null || candidate.sharedMesh == null)
                        continue;

                    hullDeformMeshFilter = candidate;
                    break;
                }
            }

            if (hullDeformMeshCollider == null)
                hullDeformMeshCollider = hullCollider as MeshCollider;

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
                return;

            float depthMeters = math.max(0f, fluidDynamics.ExternalDepthMeters);
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
            // COLD ALLOC: NativeArray<HullDentCommand>[16] - queued hull dent staging buffer - owner: SubmarineStructuralGrid
            _queuedDentCommands = new NativeArray<HullDentCommand>(MaxQueuedImpacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<HullDentCommand>[16] - scheduled hull dent snapshot buffer - owner: SubmarineStructuralGrid
            _scheduledDentCommands = new NativeArray<HullDentCommand>(MaxQueuedImpacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeStateMemorySentinel();

            _nativeStateReady = true;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _queuedDentCount = 0;
            _scheduledDentCount = 0;
            _mappedCompartmentCount = 0;
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
            _recentImpactSeverityNormalized = 0f;
            RefreshCompartmentMapping();
            for (int i = 0; i < CompartmentCapacity; i++)
                _previousCompartmentPressuresKPa[i] = 0f;
        }

        private void EnsureHullDentRuntime()
        {
            if (_hullDentMeshReady || hullDeformMeshFilter == null || hullDeformMeshFilter.sharedMesh == null)
                return;

            Mesh sourceMesh = hullDeformMeshFilter.sharedMesh;
            if (!TryCaptureHullDentMeshData(sourceMesh))
                return;

            // COLD ALLOC: Mesh[1] - runtime dentable hull mesh clone - owner: SubmarineStructuralGrid
            _runtimeHullDentMesh = Instantiate(sourceMesh);
            _runtimeHullDentMesh.name = $"{sourceMesh.name}_RuntimeDent";
            _runtimeHullDentMesh.MarkDynamic();
            hullDeformMeshFilter.sharedMesh = _runtimeHullDentMesh;
            if (hullDeformMeshCollider != null)
                hullDeformMeshCollider.sharedMesh = _runtimeHullDentMesh;

            _hullDentMeshReady = true;
        }

        private bool TryCaptureHullDentMeshData(Mesh sourceMesh)
        {
            if (sourceMesh == null)
                return false;

            using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh);
            Mesh.MeshData sourceData = meshDataArray[0];
            if (!TryResolveHullDentMeshLayout(sourceData, out HullDentMeshLayout layout))
                return false;

            int vertexCount = sourceData.vertexCount;
            int subMeshCount = sourceData.subMeshCount;
            if (vertexCount <= 0 || subMeshCount <= 0)
                return false;

            int totalIndexCount = 0;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                SubMeshDescriptor subMesh = sourceData.GetSubMesh(subMeshIndex);
                totalIndexCount += subMesh.indexCount;
            }

            if (totalIndexCount <= 0)
                return false;

            JobHandle dependency = _dentJobRunning ? _dentJobHandle : default;
            DisposeHullDentStateDeferred(ref dependency);

            // COLD ALLOC: NativeArray<float3>[vertexCount] - front dented hull positions - owner: SubmarineStructuralGrid
            _hullDentVerticesFront = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[vertexCount] - back dented hull positions - owner: SubmarineStructuralGrid
            _hullDentVerticesBack = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[vertexCount] - immutable hull normals for dent publication - owner: SubmarineStructuralGrid
            _hullDentNormals = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float2>[vertexCount] - immutable hull UV0 for dent publication - owner: SubmarineStructuralGrid
            _hullDentUvs = new NativeArray<float2>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[indexCount] - immutable hull triangle index buffer - owner: SubmarineStructuralGrid
            _hullDentIndices = new NativeArray<uint>(totalIndexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterHullDentMemorySentinel();
            _hullDentSubMeshes = new SubMeshDescriptor[subMeshCount]; // COLD ALLOC: SubMeshDescriptor[subMeshCount] - runtime hull submesh descriptors - owner: SubmarineStructuralGrid

            if (layout.Interleaved)
            {
                NativeArray<HullDentVertex> vertices = sourceData.GetVertexData<HullDentVertex>(layout.PositionStream);
                for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                {
                    HullDentVertex vertex = vertices[vertexIndex];
                    _hullDentVerticesFront[vertexIndex] = vertex.Position;
                    _hullDentVerticesBack[vertexIndex] = vertex.Position;
                    _hullDentNormals[vertexIndex] = vertex.Normal;
                    _hullDentUvs[vertexIndex] = vertex.UV;
                }
            }
            else
            {
                NativeArray<Vector3> positions = sourceData.GetVertexData<Vector3>(layout.PositionStream);
                NativeArray<Vector3> normals = sourceData.GetVertexData<Vector3>(layout.NormalStream);
                NativeArray<Vector2> uvs = sourceData.GetVertexData<Vector2>(layout.UvStream);
                for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                {
                    float3 position = positions[vertexIndex];
                    _hullDentVerticesFront[vertexIndex] = position;
                    _hullDentVerticesBack[vertexIndex] = position;
                    _hullDentNormals[vertexIndex] = normals[vertexIndex];
                    _hullDentUvs[vertexIndex] = uvs[vertexIndex];
                }
            }

            int copiedIndexCount = 0;
            bool useUintIndices = sourceMesh.indexFormat == IndexFormat.UInt32;
            NativeArray<uint> indexData32 = useUintIndices ? sourceData.GetIndexData<uint>() : default;
            NativeArray<ushort> indexData16 = useUintIndices ? default : sourceData.GetIndexData<ushort>();
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                SubMeshDescriptor sourceSubMesh = sourceData.GetSubMesh(subMeshIndex);
                _hullDentSubMeshes[subMeshIndex] = new SubMeshDescriptor(copiedIndexCount, sourceSubMesh.indexCount, sourceSubMesh.topology)
                {
                    bounds = sourceMesh.bounds,
                    baseVertex = sourceSubMesh.baseVertex,
                    firstVertex = sourceSubMesh.firstVertex,
                    vertexCount = sourceSubMesh.vertexCount
                };

                for (int indexOffset = 0; indexOffset < sourceSubMesh.indexCount; indexOffset++)
                {
                    _hullDentIndices[copiedIndexCount + indexOffset] = useUintIndices
                        ? indexData32[sourceSubMesh.indexStart + indexOffset]
                        : indexData16[sourceSubMesh.indexStart + indexOffset];
                }

                copiedIndexCount += sourceSubMesh.indexCount;
            }

            _hullDentIndexCount = copiedIndexCount;
            _hullDentSubMeshCount = subMeshCount;
            _hullDentBoundsLocal = sourceMesh.bounds;
            _dentJobHandle = default;
            _dentJobRunning = false;
            return true;
        }

        private static bool TryResolveHullDentMeshLayout(Mesh.MeshData sourceData, out HullDentMeshLayout layout)
        {
            layout = default;
            if (!ValidateHullDentAttribute(sourceData, VertexAttribute.Position, VertexAttributeFormat.Float32, 3) ||
                !ValidateHullDentAttribute(sourceData, VertexAttribute.Normal, VertexAttributeFormat.Float32, 3) ||
                !ValidateHullDentAttribute(sourceData, VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2))
            {
                return false;
            }

            int positionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position);
            int normalStream = sourceData.GetVertexAttributeStream(VertexAttribute.Normal);
            int uvStream = sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0);
            if (positionStream < 0 || normalStream < 0 || uvStream < 0)
                return false;

            if (positionStream == normalStream && positionStream == uvStream &&
                sourceData.GetVertexAttributeOffset(VertexAttribute.Position) == 0 &&
                sourceData.GetVertexAttributeOffset(VertexAttribute.Normal) == HullDentInterleavedNormalOffsetBytes &&
                sourceData.GetVertexAttributeOffset(VertexAttribute.TexCoord0) == HullDentInterleavedUvOffsetBytes &&
                sourceData.GetVertexBufferStride(positionStream) == HullDentInterleavedStrideBytes)
            {
                layout.Interleaved = true;
                layout.PositionStream = positionStream;
                layout.NormalStream = normalStream;
                layout.UvStream = uvStream;
                return true;
            }

            if (!ValidateHullDentSeparateAttributeStream(sourceData, VertexAttribute.Position, positionStream, HullDentPositionStrideBytes) ||
                !ValidateHullDentSeparateAttributeStream(sourceData, VertexAttribute.Normal, normalStream, HullDentNormalStrideBytes) ||
                !ValidateHullDentSeparateAttributeStream(sourceData, VertexAttribute.TexCoord0, uvStream, HullDentUvStrideBytes))
            {
                return false;
            }

            layout.Interleaved = false;
            layout.PositionStream = positionStream;
            layout.NormalStream = normalStream;
            layout.UvStream = uvStream;
            return true;
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
                relay = hullBody.gameObject.AddComponent<SubmarineHullImpactRelay>(); // COLD ALLOC: SubmarineHullImpactRelay[1] - hull-rigidbody collision forwarding to structural grid - owner: SubmarineStructuralGrid

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

        private static bool ValidateHullDentAttribute(
            Mesh.MeshData sourceData,
            VertexAttribute attribute,
            VertexAttributeFormat expectedFormat,
            int expectedDimension)
        {
            if (!sourceData.HasVertexAttribute(attribute))
                return false;

            return sourceData.GetVertexAttributeFormat(attribute) == expectedFormat &&
                   sourceData.GetVertexAttributeDimension(attribute) == expectedDimension;
        }

        private static bool ValidateHullDentSeparateAttributeStream(
            Mesh.MeshData sourceData,
            VertexAttribute attribute,
            int stream,
            int expectedStride)
        {
            return sourceData.GetVertexAttributeOffset(attribute) == 0 &&
                   sourceData.GetVertexBufferStride(stream) == expectedStride;
        }

        private void RefreshCompartmentMapping()
        {
            if (!_cellCompartmentIndices.IsCreated || fluidDynamics == null || fluidDynamics.CompartmentCount <= 0)
                return;

            int compartmentCount = fluidDynamics.CompartmentCount;
            if (_mappedCompartmentCount == compartmentCount)
                return;

            float3 gridMin = (float3)localGridCenter - ((float3)localGridSize * 0.5f);
            float3 cellSize = new float3(
                localGridSize.x / math.max(gridWidth, 1),
                localGridSize.y / math.max(gridHeight, 1),
                localGridSize.z / math.max(gridDepth, 1));

            int cellIndex = 0;
            for (int z = 0; z < gridDepth; z++)
            {
                float localZ = gridMin.z + ((z + 0.5f) * cellSize.z);
                for (int y = 0; y < gridHeight; y++)
                {
                    float localY = gridMin.y + ((y + 0.5f) * cellSize.y);
                    for (int x = 0; x < gridWidth; x++, cellIndex++)
                    {
                        float localX = gridMin.x + ((x + 0.5f) * cellSize.x);
                        float3 cellLocalPoint = new float3(localX, localY, localZ);
                        byte nearestIndex = UnmappedCompartment;
                        float nearestDistanceSq = float.MaxValue;

                        for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
                        {
                            Vector3 centroid = fluidDynamics.GetCompartmentCentroid(compartmentIndex);
                            float distanceSq = math.lengthsq(cellLocalPoint - (float3)centroid);
                            if (distanceSq < nearestDistanceSq)
                            {
                                nearestDistanceSq = distanceSq;
                                nearestIndex = (byte)compartmentIndex;
                            }
                        }

                        _cellCompartmentIndices[cellIndex] = nearestIndex;
                    }
                }
            }

            _mappedCompartmentCount = compartmentCount;
        }

        private void ApplyPressureCycleFatigue()
        {
            if (!_cellIntegrityFront.IsCreated || !_cellFatigue.IsCreated || atmosphereSystem == null || fluidDynamics == null)
                return;

            int compartmentCount = math.min(fluidDynamics.CompartmentCount, CompartmentCapacity);
            float thresholdKPa = math.max(0f, fatiguePressureThresholdKPa);
            for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
            {
                float previousPressure = _previousCompartmentPressuresKPa[compartmentIndex];
                float currentPressure = atmosphereSystem.GetRoomPressureKPa(compartmentIndex);
                _previousCompartmentPressuresKPa[compartmentIndex] = currentPressure;

                if (previousPressure >= thresholdKPa || currentPressure < thresholdKPa)
                    continue;

                ApplyFatigueToCompartment(compartmentIndex);
            }
        }

        private void ApplyFatigueToCompartment(int compartmentIndex)
        {
            if (!_cellIntegrityFront.IsCreated || !_cellFatigue.IsCreated || !_cellCompartmentIndices.IsCreated)
                return;

            float thermalMultiplier = atmosphereSystem != null
                ? atmosphereSystem.ResolveThermalFatigueMultiplier(compartmentIndex)
                : 1f;
            float scaledIntegrityLossPerCycle = math.max(0f, fatigueIntegrityLossPerCycle * thermalMultiplier);
            int cellCount = _cellIntegrityFront.Length;
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                if (_cellCompartmentIndices[cellIndex] != compartmentIndex)
                    continue;

                byte fatigue = _cellFatigue[cellIndex];
                if (fatigue < byte.MaxValue)
                    fatigue++;

                _cellFatigue[cellIndex] = fatigue;
                _fatiguePeakNormalized = math.max(_fatiguePeakNormalized, fatigue / (float)byte.MaxValue);
                int integrityCap = math.max(0, (int)math.floor(FullIntegrity - (fatigue * scaledIntegrityLossPerCycle)));
                byte cappedIntegrity = (byte)integrityCap;
                if (_cellIntegrityFront[cellIndex] > cappedIntegrity)
                    _cellIntegrityFront[cellIndex] = cappedIntegrity;

                if (_cellIntegrityBack.IsCreated && _cellIntegrityBack[cellIndex] > cappedIntegrity)
                    _cellIntegrityBack[cellIndex] = cappedIntegrity;
            }
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

        private void ScheduleHullDentJob()
        {
            if (_dentJobRunning ||
                !_hullDentMeshReady ||
                !_scheduledDentCommands.IsCreated ||
                !_hullDentVerticesFront.IsCreated ||
                _queuedDentCount <= 0)
            {
                return;
            }

            _scheduledDentCount = _queuedDentCount;
            for (int i = 0; i < _scheduledDentCount; i++)
                _scheduledDentCommands[i] = _queuedDentCommands[i];

            _queuedDentCount = 0;
            _dentJobHandle = new HullDentJob
            {
                InputVertices = _hullDentVerticesFront,
                DentCommands = _scheduledDentCommands,
                OutputVertices = _hullDentVerticesBack,
                DentCount = _scheduledDentCount
            }.Schedule(_hullDentVerticesFront.Length, 64);
            _dentJobRunning = true;
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

        private void ConsumeCompletedHullDentJob()
        {
            if (!_dentJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _dentJobHandle, false))
                return;

            _dentJobRunning = false;
            _scheduledDentCount = 0;
            _hullDentWritableMeshDataApplied = false;

            NativeArray<float3> frontVertices = _hullDentVerticesFront;
            _hullDentVerticesFront = _hullDentVerticesBack;
            _hullDentVerticesBack = frontVertices;
            PublishHullDentMesh();
        }

        private void PublishHullDentMesh()
        {
            if (!_hullDentMeshReady ||
                _runtimeHullDentMesh == null ||
                !_hullDentVerticesFront.IsCreated ||
                !_hullDentNormals.IsCreated ||
                !_hullDentUvs.IsCreated ||
                !_hullDentIndices.IsCreated ||
                _hullDentSubMeshes == null ||
                _hullDentWritableMeshDataApplied)
            {
                return;
            }

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            bool meshApplied = false;

            try
            {
                Mesh.MeshData meshData = writableMeshData[0];
                meshData.SetVertexBufferParams(
                    _hullDentVerticesFront.Length,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
                meshData.SetIndexBufferParams(_hullDentIndexCount, IndexFormat.UInt32);

                NativeArray<HullDentVertex> destinationVertices = meshData.GetVertexData<HullDentVertex>();
                NativeArray<uint> destinationIndices = meshData.GetIndexData<uint>();
                for (int vertexIndex = 0; vertexIndex < _hullDentVerticesFront.Length; vertexIndex++)
                {
                    destinationVertices[vertexIndex] = new HullDentVertex
                    {
                        Position = _hullDentVerticesFront[vertexIndex],
                        Normal = _hullDentNormals[vertexIndex],
                        UV = _hullDentUvs[vertexIndex]
                    };
                }

                for (int index = 0; index < _hullDentIndexCount; index++)
                    destinationIndices[index] = _hullDentIndices[index];

                meshData.subMeshCount = _hullDentSubMeshCount;
                for (int subMeshIndex = 0; subMeshIndex < _hullDentSubMeshCount; subMeshIndex++)
                    meshData.SetSubMesh(subMeshIndex, _hullDentSubMeshes[subMeshIndex], MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

                Mesh.ApplyAndDisposeWritableMeshData(
                    writableMeshData,
                    _runtimeHullDentMesh,
                    MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
                meshApplied = true;
                _hullDentWritableMeshDataApplied = true;
                _runtimeHullDentMesh.bounds = _hullDentBoundsLocal;
                if (hullDeformMeshCollider != null)
                {
                    hullDeformMeshCollider.sharedMesh = null;
                    hullDeformMeshCollider.sharedMesh = _runtimeHullDentMesh;
                }
            }
            finally
            {
                if (!meshApplied)
                    writableMeshData.Dispose();
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
            if (_dentJobRunning)
                dependency = JobHandle.CombineDependencies(dependency, _dentJobHandle);
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
            DisposeDeferred(ref _queuedDentCommands, ref dependency);
            DisposeDeferred(ref _scheduledDentCommands, ref dependency);
            DisposeDeferred(ref _hullDentVerticesFront, ref dependency);
            DisposeDeferred(ref _hullDentVerticesBack, ref dependency);
            DisposeDeferred(ref _hullDentNormals, ref dependency);
            DisposeDeferred(ref _hullDentUvs, ref dependency);
            DisposeDeferred(ref _hullDentIndices, ref dependency);
            _damageJobHandle = default;
            _damageJobRunning = false;
            _dentJobHandle = default;
            _dentJobRunning = false;
            _nativeStateReady = false;
            _hullDentMeshReady = false;
            _hullDentWritableMeshDataApplied = false;
            _recentImpactSeverityNormalized = 0f;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _queuedDentCount = 0;
            _scheduledDentCount = 0;
            _mappedCompartmentCount = 0;
            _hullDentIndexCount = 0;
            _hullDentSubMeshCount = 0;
            _hullDentSubMeshes = null;
            if (_runtimeHullDentMesh != null)
            {
                Destroy(_runtimeHullDentMesh);
                _runtimeHullDentMesh = null;
            }
        }

        private void DisposeHullDentStateDeferred(ref JobHandle dependency)
        {
            DisposeDeferred(ref _hullDentVerticesFront, ref dependency);
            DisposeDeferred(ref _hullDentVerticesBack, ref dependency);
            DisposeDeferred(ref _hullDentNormals, ref dependency);
            DisposeDeferred(ref _hullDentUvs, ref dependency);
            DisposeDeferred(ref _hullDentIndices, ref dependency);
            _hullDentSubMeshes = null;
            _hullDentMeshReady = false;
            _hullDentWritableMeshDataApplied = false;
            _hullDentIndexCount = 0;
            _hullDentSubMeshCount = 0;
            if (_runtimeHullDentMesh != null)
            {
                Destroy(_runtimeHullDentMesh);
                _runtimeHullDentMesh = null;
            }
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
            NativeMemorySentinel.RegisterNativeArray(_queuedDentCommands, NativeMemoryOwner, nameof(_queuedDentCommands), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_scheduledDentCommands, NativeMemoryOwner, nameof(_scheduledDentCommands), NativeMemoryLifetime);
        }

        private void RegisterHullDentMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_hullDentVerticesFront, NativeMemoryOwner, nameof(_hullDentVerticesFront), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_hullDentVerticesBack, NativeMemoryOwner, nameof(_hullDentVerticesBack), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_hullDentNormals, NativeMemoryOwner, nameof(_hullDentNormals), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_hullDentUvs, NativeMemoryOwner, nameof(_hullDentUvs), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_hullDentIndices, NativeMemoryOwner, nameof(_hullDentIndices), NativeMemoryLifetime);
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
