using Hecton8.Gameplay;
using Hecton8.Core;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private const int MaxMigratorySargassumIslandCount = 24;
        private const float MigratorySargassumMinimumSpatialRadiusMeters = 1f;
        private const float MigratorySargassumMaximumDeltaTimeSeconds = 2f;
        private const string MigratorySargassumNativeMemoryOwner = "WorldProceduralScatterDirector.MigratorySargassum";
        private const NativeAllocationLifetime MigratorySargassumNativeMemoryLifetime = NativeAllocationLifetime.Scene;

        private static readonly int _MigratorySargassumSpeciesHash = ComputeStableStringHash("flora.halo_sargassum");
        private static readonly ProfilerMarker _migratorySargassumProfilerMarker = new("WorldScatter.MigratorySargassum");

        [Header("Migratory Sargassum Islands")]
        [SerializeField]
        [Tooltip("Moves canopy kelp clusters as data-only migratory Sargassum islands and publishes them into the AUP spatial hash.")]
        private bool enableMigratorySargassumIslands = true;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum source family cluster radius before a kelp placement can seed a migratory island.")]
        private float migratorySargassumMinimumSourceRadiusMeters = 18f;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum water-column depth required before a canopy kelp placement can become a migratory Sargassum island.")]
        private float migratorySargassumMinimumWaterDepthMeters = 80f;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum vertical lift from seabed to the drifting Sargassum canopy.")]
        private float migratorySargassumMinimumLiftMeters = 18f;

        [SerializeField, Min(0f)]
        [Tooltip("Meters below the sampled water surface where migratory islands should drift.")]
        private float migratorySargassumSurfaceClearanceMeters = 14f;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum radius for migratory island spatial registration and photosynthetic shade.")]
        private float migratorySargassumMinimumRadiusMeters = 22f;

        [SerializeField, Min(1f)]
        [Tooltip("Maximum radius for migratory island spatial registration and photosynthetic shade.")]
        private float migratorySargassumMaximumRadiusMeters = 85f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Vertical half-height used by the AUP spatial hash for migratory island canopy volume.")]
        private float migratorySargassumHalfHeightMeters = 12f;

        [SerializeField, Min(0f)]
        [Tooltip("Multiplier applied to AbyssalFlow velocity before the Burst drift solve.")]
        private float migratorySargassumFlowDriftScale = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum horizontal drift speed in meters per second.")]
        private float migratorySargassumMaxSpeedMetersPerSecond = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Velocity damping for the Burst drift solve.")]
        private float migratorySargassumVelocityDamping = 1.8f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Cadence in seconds for applying photosynthetic kill-zone decomposition under migratory islands.")]
        private float migratorySargassumKillZoneCadenceSeconds = 2f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Radius multiplier used when decomposing seabed flora under migratory island shade.")]
        private float migratorySargassumKillZoneRadiusScale = 0.85f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chemical transient intensity published into the spatial hash so fauna can follow drifting islands.")]
        private float migratorySargassumChemicalSignalIntensity = 0.45f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Small local chemical beacon radius. Kept below canopy radius to avoid large transient-cell expansion.")]
        private float migratorySargassumChemicalSignalRadiusMeters = 6f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Lifetime in seconds for migratory island chemical spatial-hash transients.")]
        private float migratorySargassumChemicalSignalLifetimeSeconds = 1.25f;

        [SerializeField]
        [Tooltip("Organic manager that receives photosynthetic kill-zone decomposition. If unset, the director resolves a sibling component once.")]
        private DestructibleOrganicManager migratorySargassumOrganicManager;

        private NativeArray<MigratorySargassumIslandState> _migratorySargassumIslands;
        private NativeArray<MigratorySargassumIslandState> _migratorySargassumScratchIslands;
        private NativeArray<MigratorySargassumSourceState> _migratorySargassumSelectedSources;
        private NativeArray<float3> _migratorySargassumFlowSamples;
        private NativeArray<int> _migratorySargassumSpatialHandles;
        private NativeArray<int> _migratorySargassumScratchSpatialHandles;
        private HectonSpatialHash _migratorySargassumSpatialHash;
        private JobHandle _migratorySargassumJobHandle;
        private bool _migratorySargassumJobRunning;
        private int _migratorySargassumIslandCount;
        private float _lastMigratorySargassumTickTime;
        private float _nextMigratorySargassumKillZoneTime;
        private float _migratorySargassumTideHeightMeters;
        private uint _migratorySargassumTideSequence;

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct MigratorySargassumSourceState
        {
            [FieldOffset(0)]
            public long SourceKey;
            [FieldOffset(8)]
            public int SourceHash;
            [FieldOffset(12)]
            private uint _padPreAup;
            [FieldOffset(16)]
            public AbsoluteUniversePosition Position;
            [FieldOffset(64)]
            public float RadiusMeters;
            [FieldOffset(68)]
            public byte Active;
            [FieldOffset(69)]
            private byte _pad0;
            [FieldOffset(70)]
            private ushort _pad1;
            [FieldOffset(72)]
            private ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct MigratorySargassumIslandState
        {
            [FieldOffset(0)]
            public long SourceKey;
            [FieldOffset(8)]
            public int SourceHash;
            [FieldOffset(12)]
            private uint _padPreAup;
            [FieldOffset(16)]
            public AbsoluteUniversePosition Position;
            [FieldOffset(64)]
            public float3 Velocity;
            [FieldOffset(76)]
            public float RadiusMeters;
            [FieldOffset(80)]
            public byte Active;
            [FieldOffset(81)]
            private byte _pad0;
            [FieldOffset(82)]
            private ushort _pad1;
            [FieldOffset(84)]
            private uint _pad2;
            [FieldOffset(88)]
            private ulong _pad3;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct UpdateMigratorySargassumIslandsJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<MigratorySargassumIslandState> Islands;
            [NoAlias, ReadOnly] public NativeArray<float3> FlowSamples;
            public float DeltaTime;
            public float DriftScale;
            public float MaxSpeed;
            public float VelocityDamping;

            public void Execute(int index)
            {
                MigratorySargassumIslandState island = Islands[index];
                if (island.Active == 0)
                    return;

                float3 flow = FlowSamples[index];
                flow.y = 0f;
                float flowMagnitudeSq = math.lengthsq(flow);
                float3 desiredVelocity = float3.zero;
                if (flowMagnitudeSq > 0.0001f)
                {
                    float speedScale = DriftScale;
                    float maxSpeedSq = MaxSpeed * MaxSpeed;
                    float driftSpeedSq = flowMagnitudeSq * DriftScale * DriftScale;
                    if (driftSpeedSq > maxSpeedSq)
                        speedScale = MaxSpeed * math.rsqrt(flowMagnitudeSq);

                    desiredVelocity = flow * speedScale;
                }

                float blend = math.saturate(DeltaTime * VelocityDamping);
                island.Velocity = math.lerp(island.Velocity, desiredVelocity, blend);
                float3 localPosition = new float3(island.Position.LocalX, island.Position.LocalY, island.Position.LocalZ);
                NormalizeAupAfterDrift(ref island.Position, localPosition + island.Velocity * DeltaTime);
                Islands[index] = island;
            }

            private static void NormalizeAupAfterDrift(ref AbsoluteUniversePosition position, float3 localPosition)
            {
                const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
                if (localPosition.x < 0f || localPosition.x >= cellSize)
                {
                    long gridDelta = (long)math.floor(localPosition.x / cellSize);
                    position.GridX += gridDelta;
                    localPosition.x -= gridDelta * cellSize;
                }

                if (localPosition.y < 0f || localPosition.y >= cellSize)
                {
                    long gridDelta = (long)math.floor(localPosition.y / cellSize);
                    position.GridY += gridDelta;
                    localPosition.y -= gridDelta * cellSize;
                }

                if (localPosition.z < 0f || localPosition.z >= cellSize)
                {
                    long gridDelta = (long)math.floor(localPosition.z / cellSize);
                    position.GridZ += gridDelta;
                    localPosition.z -= gridDelta * cellSize;
                }

                position.LocalX = localPosition.x;
                position.LocalY = localPosition.y;
                position.LocalZ = localPosition.z;
            }
        }

        private void EnsureMigratorySargassumLane()
        {
            if (_migratorySargassumIslands.IsCreated)
                return;

            // COLD ALLOC: NativeArray<MigratorySargassumIslandState>[MaxMigratorySargassumIslandCount] — persistent data-only migratory canopy state — owner: WorldProceduralScatterDirector
            _migratorySargassumIslands = new NativeArray<MigratorySargassumIslandState>(MaxMigratorySargassumIslandCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<MigratorySargassumIslandState>[MaxMigratorySargassumIslandCount] — stable source reconciliation scratch — owner: WorldProceduralScatterDirector
            _migratorySargassumScratchIslands = new NativeArray<MigratorySargassumIslandState>(MaxMigratorySargassumIslandCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<MigratorySargassumSourceState>[MaxMigratorySargassumIslandCount] — deterministic source selection scratch — owner: WorldProceduralScatterDirector
            _migratorySargassumSelectedSources = new NativeArray<MigratorySargassumSourceState>(MaxMigratorySargassumIslandCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<float3>[MaxMigratorySargassumIslandCount] — AbyssalFlow samples for Burst drift solve — owner: WorldProceduralScatterDirector
            _migratorySargassumFlowSamples = new NativeArray<float3>(MaxMigratorySargassumIslandCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<int>[MaxMigratorySargassumIslandCount] — AUP spatial hash handles — owner: WorldProceduralScatterDirector
            _migratorySargassumSpatialHandles = new NativeArray<int>(MaxMigratorySargassumIslandCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<int>[MaxMigratorySargassumIslandCount] — AUP spatial hash handle reconciliation scratch — owner: WorldProceduralScatterDirector
            _migratorySargassumScratchSpatialHandles = new NativeArray<int>(MaxMigratorySargassumIslandCount, Allocator.Persistent);
            RegisterMigratoryNativeArray(_migratorySargassumIslands, nameof(_migratorySargassumIslands));
            RegisterMigratoryNativeArray(_migratorySargassumScratchIslands, nameof(_migratorySargassumScratchIslands));
            RegisterMigratoryNativeArray(_migratorySargassumSelectedSources, nameof(_migratorySargassumSelectedSources));
            RegisterMigratoryNativeArray(_migratorySargassumFlowSamples, nameof(_migratorySargassumFlowSamples));
            RegisterMigratoryNativeArray(_migratorySargassumSpatialHandles, nameof(_migratorySargassumSpatialHandles));
            RegisterMigratoryNativeArray(_migratorySargassumScratchSpatialHandles, nameof(_migratorySargassumScratchSpatialHandles));
            // COLD ALLOC: HectonSpatialHash[MaxMigratorySargassumIslandCount] — AUP broadphase for migratory Sargassum island volumes — owner: WorldProceduralScatterDirector
            _migratorySargassumSpatialHash = new HectonSpatialHash(MaxMigratorySargassumIslandCount, MaxMigratorySargassumIslandCount * 128, 32d);
        }

        private void DisposeMigratorySargassumLane()
        {
            JobHandle disposeDependency = CancelMigratorySargassumJobForDispose();

            if (_migratorySargassumSpatialHash != null)
            {
                _migratorySargassumSpatialHash.Dispose();
                _migratorySargassumSpatialHash = null;
            }

            DisposeMigratoryNativeArray(ref _migratorySargassumIslands, disposeDependency);
            DisposeMigratoryNativeArray(ref _migratorySargassumScratchIslands, disposeDependency);
            DisposeMigratoryNativeArray(ref _migratorySargassumSelectedSources, disposeDependency);
            DisposeMigratoryNativeArray(ref _migratorySargassumFlowSamples, disposeDependency);
            DisposeMigratoryNativeArray(ref _migratorySargassumSpatialHandles, disposeDependency);
            DisposeMigratoryNativeArray(ref _migratorySargassumScratchSpatialHandles, disposeDependency);
            JobHandle.ScheduleBatchedJobs();

            _migratorySargassumIslandCount = 0;
            _lastMigratorySargassumTickTime = 0f;
            _nextMigratorySargassumKillZoneTime = 0f;
            _migratorySargassumTideHeightMeters = 0f;
            _migratorySargassumTideSequence = 0u;
        }

        private JobHandle CancelMigratorySargassumJobForDispose()
        {
            if (!_migratorySargassumJobRunning)
                return default;

            JobHandle disposeDependency = _migratorySargassumJobHandle;
            _migratorySargassumJobHandle = default;
            _migratorySargassumJobRunning = false;
            return disposeDependency;
        }

        private static void DisposeMigratoryNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private static void RegisterMigratoryNativeArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                MigratorySargassumNativeMemoryOwner,
                label,
                MigratorySargassumNativeMemoryLifetime);
        }

        private void TickMigratorySargassumLane(float now)
        {
            if (!enableMigratorySargassumIslands)
                return;

            using (_migratorySargassumProfilerMarker.Auto())
            {
                RefreshMigratorySargassumTideSnapshot();
                EnsureMigratorySargassumLane();
                RefreshMigratorySargassumIslandsFromDesiredPlacements();
                ApplyMigratorySargassumKillZones(now);
                ScheduleMigratorySargassumJob(now);
            }
        }

        private void CompleteMigratorySargassumJobIfReady()
        {
            if (!_migratorySargassumJobRunning || !_migratorySargassumJobHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _migratorySargassumJobHandle, forceComplete: false))
                return;

            _migratorySargassumJobRunning = false;
            PublishMigratorySargassumSpatialState();
        }

        private void RefreshMigratorySargassumIslandsFromDesiredPlacements()
        {
            if (!_migratorySargassumIslands.IsCreated || _desiredPlacements == null)
                return;

            for (int i = 0; i < MaxMigratorySargassumIslandCount; i++)
                _migratorySargassumSelectedSources[i] = default;

            int selectedCount = 0;
            var enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (!TryBuildMigratorySargassumSource(placement, out MigratorySargassumSourceState source))
                    continue;

                selectedCount = InsertMigratorySargassumSourceSorted(source, selectedCount);
            }

            ReconcileMigratorySargassumSources(selectedCount);
        }

        private bool TryBuildMigratorySargassumSource(ScatterPlacement placement, out MigratorySargassumSourceState source)
        {
            source = default;
            if (placement == null || placement.Family == null)
                return false;

            if (!IsMigratorySargassumSourceFamily(placement.Family))
                return false;

            float sourceRadius = math.max(placement.Family.clusterRadiusMeters, placement.EffectiveSpacing);
            if (sourceRadius < math.max(1f, migratorySargassumMinimumSourceRadiusMeters))
                return false;

            if (placement.DepthMeters < math.max(1f, migratorySargassumMinimumWaterDepthMeters))
                return false;

            float waterColumnLift = math.max(
                math.max(1f, migratorySargassumMinimumLiftMeters),
                placement.DepthMeters - math.max(0f, migratorySargassumSurfaceClearanceMeters));
            Vector3 absolutePosition = placement.Position;
            absolutePosition.y += waterColumnLift;

            source = new MigratorySargassumSourceState
            {
                SourceKey = placement.Key,
                SourceHash = _MigratorySargassumSpeciesHash,
                Position = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    absolutePosition.x,
                    absolutePosition.y,
                    absolutePosition.z)),
                RadiusMeters = math.clamp(sourceRadius, migratorySargassumMinimumRadiusMeters, migratorySargassumMaximumRadiusMeters),
                Active = 1
            };
            return true;
        }

        private int InsertMigratorySargassumSourceSorted(MigratorySargassumSourceState source, int selectedCount)
        {
            int capacity = MaxMigratorySargassumIslandCount;
            if (selectedCount >= capacity && source.SourceKey >= _migratorySargassumSelectedSources[capacity - 1].SourceKey)
                return selectedCount;

            int insertIndex = selectedCount < capacity ? selectedCount : capacity - 1;
            while (insertIndex > 0 && _migratorySargassumSelectedSources[insertIndex - 1].SourceKey > source.SourceKey)
            {
                if (insertIndex < capacity)
                    _migratorySargassumSelectedSources[insertIndex] = _migratorySargassumSelectedSources[insertIndex - 1];
                insertIndex--;
            }

            _migratorySargassumSelectedSources[insertIndex] = source;
            return selectedCount < capacity ? selectedCount + 1 : selectedCount;
        }

        private void ReconcileMigratorySargassumSources(int selectedCount)
        {
            for (int i = 0; i < MaxMigratorySargassumIslandCount; i++)
            {
                _migratorySargassumScratchIslands[i] = default;
                _migratorySargassumScratchSpatialHandles[i] = 0;
            }

            for (int i = 0; i < selectedCount; i++)
            {
                MigratorySargassumSourceState source = _migratorySargassumSelectedSources[i];
                if (TryFindMigratorySargassumIsland(source.SourceKey, out int existingIndex))
                {
                    MigratorySargassumIslandState island = _migratorySargassumIslands[existingIndex];
                    island.SourceHash = source.SourceHash;
                    island.RadiusMeters = source.RadiusMeters;
                    island.Active = 1;
                    if (!IsFinite(in island.Position))
                        island.Position = source.Position;

                    _migratorySargassumScratchIslands[i] = island;
                    _migratorySargassumScratchSpatialHandles[i] = _migratorySargassumSpatialHandles[existingIndex];
                    _migratorySargassumSpatialHandles[existingIndex] = 0;
                }
                else
                {
                    _migratorySargassumScratchIslands[i] = new MigratorySargassumIslandState
                    {
                        SourceKey = source.SourceKey,
                        SourceHash = source.SourceHash,
                        Position = source.Position,
                        Velocity = float3.zero,
                        RadiusMeters = source.RadiusMeters,
                        Active = 1
                    };
                }
            }

            for (int i = 0; i < MaxMigratorySargassumIslandCount; i++)
            {
                int staleHandle = _migratorySargassumSpatialHandles[i];
                if (staleHandle != 0)
                    _migratorySargassumSpatialHash.Unregister(staleHandle);

                _migratorySargassumIslands[i] = _migratorySargassumScratchIslands[i];
                _migratorySargassumSpatialHandles[i] = _migratorySargassumScratchSpatialHandles[i];
            }

            _migratorySargassumIslandCount = selectedCount;
            PublishMigratorySargassumSpatialState();
        }

        private bool TryFindMigratorySargassumIsland(long sourceKey, out int index)
        {
            for (int i = 0; i < _migratorySargassumIslandCount; i++)
            {
                MigratorySargassumIslandState island = _migratorySargassumIslands[i];
                if (island.Active != 0 && island.SourceKey == sourceKey)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private void ScheduleMigratorySargassumJob(float now)
        {
            if (_migratorySargassumJobRunning || _migratorySargassumIslandCount <= 0)
                return;

            if (!EnsureEnvironmentalVegetationBridgeResolved())
                return;

            float deltaTime = _lastMigratorySargassumTickTime > 0f
                ? math.clamp(now - _lastMigratorySargassumTickTime, 0f, MigratorySargassumMaximumDeltaTimeSeconds)
                : 0f;
            _lastMigratorySargassumTickTime = now;

            for (int i = 0; i < _migratorySargassumIslandCount; i++)
            {
                MigratorySargassumIslandState island = _migratorySargassumIslands[i];
                Vector3 runtimePosition = ToRuntimeMigratorySargassumPosition(in island.Position);
                _migratorySargassumFlowSamples[i] =
                    environmentalVegetationBridge.TrySampleAbyssalFlow(runtimePosition, out Vector3 flowVector)
                        ? new float3(flowVector.x, flowVector.y, flowVector.z)
                        : float3.zero;
            }

            UpdateMigratorySargassumIslandsJob job = new UpdateMigratorySargassumIslandsJob
            {
                Islands = _migratorySargassumIslands,
                FlowSamples = _migratorySargassumFlowSamples,
                DeltaTime = deltaTime,
                DriftScale = math.max(0f, migratorySargassumFlowDriftScale),
                MaxSpeed = math.max(0f, migratorySargassumMaxSpeedMetersPerSecond),
                VelocityDamping = math.max(0f, migratorySargassumVelocityDamping)
            };
            _migratorySargassumJobHandle = job.Schedule(_migratorySargassumIslandCount, 8);
            _migratorySargassumJobRunning = true;
        }

        private void PublishMigratorySargassumSpatialState()
        {
            if (!_migratorySargassumIslands.IsCreated || _migratorySargassumSpatialHash == null)
                return;

            float halfHeight = math.max(0.1f, migratorySargassumHalfHeightMeters);
            for (int i = 0; i < _migratorySargassumIslandCount; i++)
            {
                MigratorySargassumIslandState island = _migratorySargassumIslands[i];
                if (island.Active == 0)
                    continue;

                float radius = math.max(MigratorySargassumMinimumSpatialRadiusMeters, island.RadiusMeters);
                AbsoluteUniversePosition position = island.Position;
                float3 halfExtents = new float3(radius, halfHeight, radius);
                ulong flags = (ulong)(SpatialInteractionFlags.Signal | SpatialInteractionFlags.ChemicalReceiver);
                int handle = _migratorySargassumSpatialHandles[i];
                if (handle == 0)
                {
                    _migratorySargassumSpatialHandles[i] = _migratorySargassumSpatialHash.Register(
                        in position,
                        halfExtents,
                        (int)SpatialTargetKind.Signal,
                        flags,
                        island.SourceHash);
                }
                else
                {
                    if (!_migratorySargassumSpatialHash.TryUpdateEntry(
                        handle,
                        in position,
                        halfExtents,
                        (int)SpatialTargetKind.Signal,
                        flags,
                        island.SourceHash))
                    {
                        _migratorySargassumSpatialHash.Unregister(handle);
                        _migratorySargassumSpatialHandles[i] = 0;
                    }
                }

                Vector3 runtimePosition = ToRuntimeMigratorySargassumPosition(in island.Position);
                WorldSpatialHashGrid.RegisterTransientEvent(
                    runtimePosition,
                    math.min(radius, math.max(0.5f, migratorySargassumChemicalSignalRadiusMeters)),
                    migratorySargassumChemicalSignalIntensity,
                    migratorySargassumChemicalSignalLifetimeSeconds,
                    SpatialTransientEventType.ChemicalCloud,
                    SpatialInteractionFlags.ChemicalReceiver,
                    FieldTargetRole.BioformDormant,
                    island.SourceHash);
            }
        }

        private void ApplyMigratorySargassumKillZones(float now)
        {
            if (now < _nextMigratorySargassumKillZoneTime || _migratorySargassumIslandCount <= 0)
                return;

            _nextMigratorySargassumKillZoneTime = now + math.max(0.1f, migratorySargassumKillZoneCadenceSeconds);
            if (!TryResolveMigratorySargassumOrganicManager(out DestructibleOrganicManager organicManager))
                return;

            float radiusScale = math.max(0.1f, migratorySargassumKillZoneRadiusScale);
            for (int i = 0; i < _migratorySargassumIslandCount; i++)
            {
                MigratorySargassumIslandState island = _migratorySargassumIslands[i];
                if (island.Active == 0)
                    continue;

                Vector3 runtimePosition = ToRuntimeMigratorySargassumPosition(in island.Position);
                organicManager.ApplyConstructionDecomposition(runtimePosition, island.RadiusMeters * radiusScale);
            }
        }

        private bool TryResolveMigratorySargassumOrganicManager(out DestructibleOrganicManager organicManager)
        {
            if (migratorySargassumOrganicManager == null && Application.isPlaying)
                TryGetComponent<DestructibleOrganicManager>(out migratorySargassumOrganicManager);

            organicManager = migratorySargassumOrganicManager;
            return organicManager != null;
        }

        private bool TryRaycastUpMigratorySargassumCanopy(Vector3 absoluteSeabedPosition, out float occlusion01)
        {
            occlusion01 = 0f;
            if (!enableMigratorySargassumIslands ||
                !_migratorySargassumIslands.IsCreated ||
                _migratorySargassumIslandCount <= 0)
            {
                return false;
            }

            float bestOcclusion = 0f;
            for (int i = 0; i < _migratorySargassumIslandCount; i++)
            {
                MigratorySargassumIslandState island = _migratorySargassumIslands[i];
                if (island.Active == 0)
                    continue;

                float3 islandAbsolutePosition = ToAbsoluteFloat3(in island.Position);
                float verticalDelta = islandAbsolutePosition.y - absoluteSeabedPosition.y;
                if (verticalDelta <= 0f)
                    continue;

                float radius = math.max(MigratorySargassumMinimumSpatialRadiusMeters, island.RadiusMeters);
                float dx = islandAbsolutePosition.x - absoluteSeabedPosition.x;
                float dz = islandAbsolutePosition.z - absoluteSeabedPosition.z;
                float radiusSq = radius * radius;
                float planarSq = (dx * dx) + (dz * dz);
                if (planarSq > radiusSq)
                    continue;

                float planarOcclusion = 1f - math.saturate(planarSq / math.max(0.001f, radiusSq));
                float verticalOcclusion = math.saturate(verticalDelta / math.max(1f, migratorySargassumMinimumWaterDepthMeters));
                bestOcclusion = math.max(bestOcclusion, planarOcclusion * verticalOcclusion);
            }

            occlusion01 = bestOcclusion;
            return bestOcclusion > 0.01f;
        }

        internal bool TryEvaluateMigratorySargassumShade(Vector3 runtimeSeabedPosition, out float occlusion01)
        {
            return TryRaycastUpMigratorySargassumCanopy(ToAbsoluteScatterPosition(runtimeSeabedPosition), out occlusion01);
        }

        internal bool TryGetNearestMigratorySargassumIsland(
            Vector3 runtimeOrigin,
            float radiusMeters,
            out Vector3 runtimePosition,
            out float islandRadiusMeters)
        {
            runtimePosition = default;
            islandRadiusMeters = 0f;
            if (!_migratorySargassumIslands.IsCreated || radiusMeters <= 0f || _migratorySargassumIslandCount <= 0)
                return false;

            Vector3 absoluteOrigin = ToAbsoluteScatterPosition(runtimeOrigin);
            float radiusSq = radiusMeters * radiusMeters;
            float bestSq = radiusSq;
            int bestIndex = -1;
            for (int i = 0; i < _migratorySargassumIslandCount; i++)
            {
                MigratorySargassumIslandState island = _migratorySargassumIslands[i];
                if (island.Active == 0)
                    continue;

                float3 islandAbsolutePosition = ToAbsoluteFloat3(in island.Position);
                float dx = islandAbsolutePosition.x - absoluteOrigin.x;
                float dy = islandAbsolutePosition.y - absoluteOrigin.y;
                float dz = islandAbsolutePosition.z - absoluteOrigin.z;
                float distanceSq = (dx * dx) + (dy * dy) + (dz * dz);
                if (distanceSq >= bestSq)
                    continue;

                bestSq = distanceSq;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return false;

            MigratorySargassumIslandState bestIsland = _migratorySargassumIslands[bestIndex];
            runtimePosition = ToRuntimeMigratorySargassumPosition(in bestIsland.Position);
            islandRadiusMeters = bestIsland.RadiusMeters;
            return true;
        }

        private bool ShouldRejectForMigratorySargassumShade(
            WorldPrefabFamilyProfile family,
            in ScatterCandidatePreview candidatePreview)
        {
            if (family == null || IsMigratorySargassumSourceFamily(family))
                return false;

            if (candidatePreview.HeightLayerIndex != 0)
                return false;

            if (family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Ground &&
                family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Cluster)
            {
                return false;
            }

            if (ResolveFloraBudgetClass(family) == FloraBudgetClass.None)
                return false;

            return TryRaycastUpMigratorySargassumCanopy(candidatePreview.Position, out _);
        }

        private static bool IsMigratorySargassumSourceFamily(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            return family.FamilyHash == _FamilyKelpCanopyHash ||
                   family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Kelp &&
                   family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster;
        }

        private static bool IsFinite(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static float3 ToAbsoluteFloat3(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            return new float3((float)absolute.x, (float)absolute.y, (float)absolute.z);
        }

        private void RefreshMigratorySargassumTideSnapshot()
        {
            uint sequence = GlobalRegistry.CelestialRuntimeSnapshotSequence;
            if (sequence == _migratorySargassumTideSequence)
                return;

            _migratorySargassumTideSequence = sequence;
            CelestialRuntimeSnapshot celestial = GlobalRegistry.CelestialRuntimeSnapshot;
            _migratorySargassumTideHeightMeters =
                (celestial.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u &&
                math.isfinite(celestial.TideHeightMeters)
                    ? celestial.TideHeightMeters
                    : 0f;
        }

        private Vector3 ToRuntimeMigratorySargassumPosition(in AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            runtimePosition.y += _migratorySargassumTideHeightMeters;

            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }
    }
}
