using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

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
    public sealed class SubmarineStructuralGrid : MonoBehaviour, IFixedTickable, IDamageSignalReceiver, ISubmarineHullBreachReadModel
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
        private const float DefaultCompressionDepthThresholdMeters = 4000f;
        private const float DefaultCompressionFullPressureKPa = 60000f;
        private const float DefaultMaximumVolumeCompressionNormalized = 0.15f;
        private const float RecentImpactSeverityDecayPerSecond = 2.8f;
        private const float Epsilon = 0.0001f;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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
        private JobHandle _damageJobHandle;
        private IDamageSignalEmitter _damageEmitter;
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
        }

        private void OnDisable()
        {
            TryUnregisterDamageReceiver();
            TryUnregister();
            if (ReferenceEquals(GlobalRegistry.SubmarineHullBreach, this))
                GlobalRegistry.UnregisterSubmarineHullBreach(this);
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
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
                ConsumeCompletedDamageJob();
                RefreshCompartmentMapping();
                ApplyAbyssalCompression();
                ApplyPressureCycleFatigue();

                if (_damageJobRunning || _queuedImpactCount <= 0)
                    return;

                ScheduleDamageJob();
            }
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

        private void CacheReferences()
        {
            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (atmosphereSystem == null)
                TryGetComponent(out atmosphereSystem);

            if (hullCollider == null)
                TryGetComponent(out hullCollider);

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

            _nativeStateReady = true;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
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

        private void ConsumeCompletedDamageJob()
        {
            if (!_damageJobRunning || !_damageJobHandle.IsCompleted)
                return;

            using (_damageConsumeProfilerMarker.Auto())
            {
                _damageJobHandle.Complete();
                _damageJobHandle = default;
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

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

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
            _damageJobHandle = default;
            _damageJobRunning = false;
            _nativeStateReady = false;
            _recentImpactSeverityNormalized = 0f;
            _queuedImpactCount = 0;
            _scheduledImpactCount = 0;
            _mappedCompartmentCount = 0;
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

            dependency = array.Dispose(dependency);
            array = default;
        }
    }
}
