using Hecton8.Gameplay;
using Hecton8.Core;
using Hecton8.Core.Memory;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    internal static class WorldProceduralScatterDirectorMigratorySargassumLayout
    {
        public const int MigratorySargassumSourceStateStrideBytes = 80;
        public const int MigratorySargassumIslandStateStrideBytes = 96;
    }

    public sealed partial class WorldProceduralScatterDirector
    {
        private const int MaxMigratorySargassumIslandCount = 24;
        private const float MigratorySargassumMinimumSpatialRadiusMeters = 1f;
        private const float MigratorySargassumMaximumDeltaTimeSeconds = 2f;

        private static readonly int _MigratorySargassumSpeciesHash = ComputeStableStringHash("flora.halo_sargassum");
        private static readonly ProfilerMarker _migratorySargassumProfilerMarker = new("WorldScatter.MigratorySargassum");
        private const uint MigratorySargassumJobPinIslands = 1u << 0;
        private const uint MigratorySargassumJobPinFlowSamples = 1u << 1;
        private const uint MigratorySargassumStatePinIslands = 1u << 0;
        private const uint MigratorySargassumStatePinScratchIslands = 1u << 1;
        private const uint MigratorySargassumStatePinSelectedSources = 1u << 2;
        private const uint MigratorySargassumStatePinSpatialHandles = 1u << 3;
        private const uint MigratorySargassumStatePinScratchSpatialHandles = 1u << 4;

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

        private MigratoryVaultArray<MigratorySargassumIslandState> _migratorySargassumIslands;
        private MigratoryVaultArray<MigratorySargassumIslandState> _migratorySargassumScratchIslands;
        private MigratoryVaultArray<MigratorySargassumSourceState> _migratorySargassumSelectedSources;
        private MigratoryVaultArray<float3> _migratorySargassumFlowSamples;
        private MigratoryVaultArray<int> _migratorySargassumSpatialHandles;
        private MigratoryVaultArray<int> _migratorySargassumScratchSpatialHandles;
        private IDataVault _migratorySargassumDataVault;
        private HectonSpatialHash _migratorySargassumSpatialHash;
        private JobHandle _migratorySargassumJobHandle;
        private bool _migratorySargassumJobRunning;
        private bool _migratorySargassumJobBufferLocksHeld;
        private uint _migratorySargassumJobBufferPinMask;
        private int _migratorySargassumIslandCount;
        private float _lastMigratorySargassumTickTime;
        private float _nextMigratorySargassumKillZoneTime;
        private float _migratorySargassumTideHeightMeters;
        private uint _migratorySargassumTideSequence;

        private struct MigratoryVaultArray<T> where T : struct
        {
            private IDataVault _vault;
            private VaultGenerationHandle<T> _handle;
            private BufferID _bufferId;

            public bool IsCreated => TryResolve(out NativeArray<T> buffer) && buffer.IsCreated;

            public int Length => TryResolve(out NativeArray<T> buffer) ? buffer.Length : 0;

            public T this[int index]
            {
                get
                {
                    NativeArray<T> buffer = Resolve();
                    return buffer[index];
                }
                set
                {
                    NativeArray<T> buffer = Resolve();
                    buffer[index] = value;
                }
            }

            public void Bind(IDataVault vault, in VaultGenerationHandle<T> handle, BufferID bufferId)
            {
                _vault = vault;
                _handle = handle;
                _bufferId = bufferId;
            }

            public bool TryResolve(out NativeArray<T> buffer)
            {
                buffer = default;
                return _vault != null &&
                       IsMigratorySargassumHandle(in _handle, _bufferId) &&
                       _vault.TryResolveHandle(in _handle, out buffer) &&
                       buffer.IsCreated;
            }

            public void ReleaseBuffer()
            {
                if (_vault != null && IsMigratorySargassumHandle(in _handle, _bufferId))
                    _vault.ReleaseBuffer(in _handle);

                _vault = null;
                _handle = default;
                _bufferId = default;
            }

            private NativeArray<T> Resolve()
            {
                return TryResolve(out NativeArray<T> buffer) ? buffer : default;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = WorldProceduralScatterDirectorMigratorySargassumLayout.MigratorySargassumSourceStateStrideBytes)]
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

        [StructLayout(LayoutKind.Explicit, Size = WorldProceduralScatterDirectorMigratorySargassumLayout.MigratorySargassumIslandStateStrideBytes)]
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

        private bool EnsureMigratorySargassumLane()
        {
            if (_migratorySargassumIslands.IsCreated)
                return true;

            IDataVault vault = ResolveMigratorySargassumVaultCold();
            if (vault == null || vault.IsAllocationLocked)
                return false;

            bool buffersReady =
                TryEnsureMigratoryVaultArray(ref _migratorySargassumIslands, BufferID.WorldScatterMigratorySargassumIslands, vault) &&
                TryEnsureMigratoryVaultArray(ref _migratorySargassumScratchIslands, BufferID.WorldScatterMigratorySargassumScratchIslands, vault) &&
                TryEnsureMigratoryVaultArray(ref _migratorySargassumSelectedSources, BufferID.WorldScatterMigratorySargassumSelectedSources, vault) &&
                TryEnsureMigratoryVaultArray(ref _migratorySargassumFlowSamples, BufferID.WorldScatterMigratorySargassumFlowSamples, vault) &&
                TryEnsureMigratoryVaultArray(ref _migratorySargassumSpatialHandles, BufferID.WorldScatterMigratorySargassumSpatialHandles, vault) &&
                TryEnsureMigratoryVaultArray(ref _migratorySargassumScratchSpatialHandles, BufferID.WorldScatterMigratorySargassumScratchSpatialHandles, vault);
            if (!buffersReady)
            {
                ReleaseMigratorySargassumVaultBuffers();
                return false;
            }
            // COLD VAULT ALLOC: six fixed migratory Sargassum buffers, all descriptor-owned by SystemID.WorldSargassum.
            // COLD ALLOC: HectonSpatialHash[MaxMigratorySargassumIslandCount] — AUP broadphase for migratory Sargassum island volumes — owner: WorldProceduralScatterDirector
            if (_migratorySargassumSpatialHash == null)
                _migratorySargassumSpatialHash = new HectonSpatialHash(MaxMigratorySargassumIslandCount, MaxMigratorySargassumIslandCount * 128, 32d);

            return true;
        }

        private void DisposeMigratorySargassumLane()
        {
            CompleteMigratorySargassumJobForDispose();

            if (_migratorySargassumSpatialHash != null)
            {
                _migratorySargassumSpatialHash.Dispose();
                _migratorySargassumSpatialHash = null;
            }

            ReleaseMigratorySargassumVaultBuffers();

            _migratorySargassumIslandCount = 0;
            _lastMigratorySargassumTickTime = 0f;
            _nextMigratorySargassumKillZoneTime = 0f;
            _migratorySargassumTideHeightMeters = 0f;
            _migratorySargassumTideSequence = 0u;
        }

        private void CompleteMigratorySargassumJobForDispose()
        {
            if (_migratorySargassumJobRunning)
                TryCompleteMigratorySargassumJobForDispose(ref _migratorySargassumJobHandle);

            _migratorySargassumJobHandle = default;
            _migratorySargassumJobRunning = false;
            ReleaseMigratorySargassumJobBufferLocks();
        }

        private static bool TryCompleteMigratorySargassumJobForDispose(ref JobHandle handle)
        {
            bool completed;
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                completed = DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            return completed;
        }

        private IDataVault ResolveMigratorySargassumVaultCold()
        {
            if (_migratorySargassumDataVault != null)
                return _migratorySargassumDataVault;

            _migratorySargassumDataVault = GlobalRegistry.DataVault;
            return _migratorySargassumDataVault;
        }

        private static bool TryEnsureMigratoryVaultArray<T>(
            ref MigratoryVaultArray<T> target,
            BufferID bufferId,
            IDataVault vault)
            where T : struct
        {
            if (target.IsCreated && target.Length >= MaxMigratorySargassumIslandCount)
                return true;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                MaxMigratorySargassumIslandCount,
                SystemID.WorldSargassum,
                NativeArrayOptions.ClearMemory);
            if (!IsMigratorySargassumHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < MaxMigratorySargassumIslandCount)
            {
                if (IsMigratorySargassumHandle(in handle, bufferId))
                    vault.ReleaseBuffer(in handle);
                return false;
            }

            target.Bind(vault, in handle, bufferId);
            return true;
        }

        private static bool IsMigratorySargassumHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.WorldSargassum &&
                   handle.Generation != 0u;
        }

        private void ReleaseMigratorySargassumVaultBuffers()
        {
            ReleaseMigratorySargassumJobBufferLocks();
            _migratorySargassumIslands.ReleaseBuffer();
            _migratorySargassumScratchIslands.ReleaseBuffer();
            _migratorySargassumSelectedSources.ReleaseBuffer();
            _migratorySargassumFlowSamples.ReleaseBuffer();
            _migratorySargassumSpatialHandles.ReleaseBuffer();
            _migratorySargassumScratchSpatialHandles.ReleaseBuffer();
        }

        private void OnMigratorySargassumDataVaultReplaced(IDataVault nextVault)
        {
            if (ReferenceEquals(_migratorySargassumDataVault, nextVault))
                return;

            CompleteMigratorySargassumJobForDispose();
            ReleaseMigratorySargassumVaultBuffers();
            _migratorySargassumDataVault = nextVault;
            _migratorySargassumIslandCount = 0;
        }

        private void TickMigratorySargassumLane(float now)
        {
            if (!enableMigratorySargassumIslands || _migratorySargassumJobRunning)
                return;

            using (_migratorySargassumProfilerMarker.Auto())
            {
                RefreshMigratorySargassumTideSnapshot();
                if (!EnsureMigratorySargassumLane())
                    return;

                if (!TryLockMigratorySargassumStateBuffers(out IDataVault stateVault, out uint statePinMask))
                    return;

                try
                {
                    RefreshMigratorySargassumIslandsFromDesiredPlacements();
                }
                finally
                {
                    ReleaseMigratorySargassumStateBufferLocks(stateVault, statePinMask);
                }

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
            ReleaseMigratorySargassumJobBufferLocks();
            PublishMigratorySargassumSpatialStateOneGuard();
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

        private bool TryPrepareMigratorySargassumFlowSamples(
            NativeArray<MigratorySargassumIslandState> islands,
            NativeArray<float3> flowSamples)
        {
            int sampleCount = math.min(_migratorySargassumIslandCount, math.min(islands.Length, flowSamples.Length));
            if (sampleCount < _migratorySargassumIslandCount)
                return false;

            for (int i = 0; i < sampleCount; i++)
            {
                MigratorySargassumIslandState island = islands[i];
                Vector3 runtimePosition = ToRuntimeMigratorySargassumPosition(in island.Position);
                flowSamples[i] =
                    environmentalVegetationBridge.TrySampleAbyssalFlow(runtimePosition, out Vector3 flowVector)
                        ? new float3(flowVector.x, flowVector.y, flowVector.z)
                        : float3.zero;
            }

            return true;
        }

        private bool TryPrepareMigratorySargassumJobBuffers()
        {
            if (!TryLockMigratorySargassumJobBuffers(
                    out NativeArray<MigratorySargassumIslandState> islands,
                    out NativeArray<float3> flowSamples))
            {
                return false;
            }

            try
            {
                return TryPrepareMigratorySargassumFlowSamples(islands, flowSamples);
            }
            finally
            {
                ReleaseMigratorySargassumJobBufferLocks();
            }
        }

        private bool TryLockMigratorySargassumJobBuffers(
            out NativeArray<MigratorySargassumIslandState> islands,
            out NativeArray<float3> flowSamples)
        {
            islands = default;
            flowSamples = default;

            if (_migratorySargassumJobBufferLocksHeld)
                return false;

            IDataVault vault = _migratorySargassumDataVault;
            if (vault == null)
                return false;

            uint pinMask = 0u;
            bool success = false;
            try
            {
                if (!TryLockMigratorySargassumJobBuffer(vault, BufferID.WorldScatterMigratorySargassumIslands, MigratorySargassumJobPinIslands, ref pinMask) ||
                    !TryLockMigratorySargassumJobBuffer(vault, BufferID.WorldScatterMigratorySargassumFlowSamples, MigratorySargassumJobPinFlowSamples, ref pinMask))
                {
                    return false;
                }

                if (_migratorySargassumIslands.TryResolve(out islands) &&
                    _migratorySargassumFlowSamples.TryResolve(out flowSamples) &&
                    islands.Length >= _migratorySargassumIslandCount &&
                    flowSamples.Length >= _migratorySargassumIslandCount)
                {
                    _migratorySargassumJobBufferLocksHeld = true;
                    _migratorySargassumJobBufferPinMask = pinMask;
                    pinMask = 0u;
                    success = true;
                    return true;
                }

                islands = default;
                flowSamples = default;
                return false;
            }
            finally
            {
                if (!success)
                    ReleaseMigratorySargassumJobBufferLocks(vault, pinMask);
            }
        }

        private void ReleaseMigratorySargassumJobBufferLocks()
        {
            if (!_migratorySargassumJobBufferLocksHeld)
                return;

            IDataVault vault = _migratorySargassumDataVault;
            uint pinMask = _migratorySargassumJobBufferPinMask;
            _migratorySargassumJobBufferPinMask = 0u;
            _migratorySargassumJobBufferLocksHeld = false;
            ReleaseMigratorySargassumJobBufferLocks(vault, pinMask);
        }

        private static bool TryLockMigratorySargassumJobBuffer(
            IDataVault vault,
            BufferID bufferId,
            uint pinBit,
            ref uint pinMask)
        {
            if ((pinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.WorldSargassum))
                return false;

            pinMask |= pinBit;
            return true;
        }

        private static void ReleaseMigratorySargassumJobBufferLocks(IDataVault vault, uint pinMask)
        {
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockMigratorySargassumJobBuffer(vault, pinMask, MigratorySargassumJobPinFlowSamples, BufferID.WorldScatterMigratorySargassumFlowSamples);
            TryUnlockMigratorySargassumJobBuffer(vault, pinMask, MigratorySargassumJobPinIslands, BufferID.WorldScatterMigratorySargassumIslands);
        }

        private static void TryUnlockMigratorySargassumJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.WorldSargassum);
        }

        private bool TryLockMigratorySargassumStateBuffers(out IDataVault vault, out uint pinMask)
        {
            vault = _migratorySargassumDataVault;
            pinMask = 0u;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool success = false;
            try
            {
                if (!TryLockMigratorySargassumStateBuffer(vault, BufferID.WorldScatterMigratorySargassumIslands, MigratorySargassumStatePinIslands, ref pinMask) ||
                    !TryLockMigratorySargassumStateBuffer(vault, BufferID.WorldScatterMigratorySargassumScratchIslands, MigratorySargassumStatePinScratchIslands, ref pinMask) ||
                    !TryLockMigratorySargassumStateBuffer(vault, BufferID.WorldScatterMigratorySargassumSelectedSources, MigratorySargassumStatePinSelectedSources, ref pinMask) ||
                    !TryLockMigratorySargassumStateBuffer(vault, BufferID.WorldScatterMigratorySargassumSpatialHandles, MigratorySargassumStatePinSpatialHandles, ref pinMask) ||
                    !TryLockMigratorySargassumStateBuffer(vault, BufferID.WorldScatterMigratorySargassumScratchSpatialHandles, MigratorySargassumStatePinScratchSpatialHandles, ref pinMask))
                {
                    return false;
                }

                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    ReleaseMigratorySargassumStateBufferLocks(vault, pinMask);
                    pinMask = 0u;
                }
            }
        }

        private void PublishMigratorySargassumSpatialStateOneGuard()
        {
            if (!TryLockMigratorySargassumStateBuffers(out IDataVault stateVault, out uint statePinMask))
                return;

            try
            {
                PublishMigratorySargassumSpatialState();
            }
            finally
            {
                ReleaseMigratorySargassumStateBufferLocks(stateVault, statePinMask);
            }
        }

        private static bool TryLockMigratorySargassumStateBuffer(
            IDataVault vault,
            BufferID bufferId,
            uint pinBit,
            ref uint pinMask)
        {
            if ((pinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.WorldSargassum))
                return false;

            pinMask |= pinBit;
            return true;
        }

        private static void ReleaseMigratorySargassumStateBufferLocks(IDataVault vault, uint pinMask)
        {
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockMigratorySargassumStateBuffer(vault, pinMask, MigratorySargassumStatePinScratchSpatialHandles, BufferID.WorldScatterMigratorySargassumScratchSpatialHandles);
            TryUnlockMigratorySargassumStateBuffer(vault, pinMask, MigratorySargassumStatePinSpatialHandles, BufferID.WorldScatterMigratorySargassumSpatialHandles);
            TryUnlockMigratorySargassumStateBuffer(vault, pinMask, MigratorySargassumStatePinSelectedSources, BufferID.WorldScatterMigratorySargassumSelectedSources);
            TryUnlockMigratorySargassumStateBuffer(vault, pinMask, MigratorySargassumStatePinScratchIslands, BufferID.WorldScatterMigratorySargassumScratchIslands);
            TryUnlockMigratorySargassumStateBuffer(vault, pinMask, MigratorySargassumStatePinIslands, BufferID.WorldScatterMigratorySargassumIslands);
        }

        private static void TryUnlockMigratorySargassumStateBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.WorldSargassum);
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

            if (!TryPrepareMigratorySargassumJobBuffers())
                return;

            if (!TryLockMigratorySargassumJobBuffers(
                    out NativeArray<MigratorySargassumIslandState> islands,
                    out NativeArray<float3> flowSamples))
                return;

            try
            {
                UpdateMigratorySargassumIslandsJob job = new UpdateMigratorySargassumIslandsJob
                {
                    Islands = islands,
                    FlowSamples = flowSamples,
                    DeltaTime = deltaTime,
                    DriftScale = math.max(0f, migratorySargassumFlowDriftScale),
                    MaxSpeed = math.max(0f, migratorySargassumMaxSpeedMetersPerSecond),
                    VelocityDamping = math.max(0f, migratorySargassumVelocityDamping)
                };
                _migratorySargassumJobHandle = job.Schedule(_migratorySargassumIslandCount, 8);
                H8Memory.RegisterActiveJob(SystemID.WorldSargassum, _migratorySargassumJobHandle);
                _migratorySargassumJobRunning = true;
            }
            catch
            {
                _migratorySargassumJobRunning = false;
                _migratorySargassumJobHandle = default;
                ReleaseMigratorySargassumJobBufferLocks();
                throw;
            }
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
            organicManager = migratorySargassumOrganicManager;
            return organicManager != null;
        }

        private void CacheMigratorySargassumOrganicManagerCold()
        {
            if (migratorySargassumOrganicManager == null && Application.isPlaying)
                TryGetComponent<DestructibleOrganicManager>(out migratorySargassumOrganicManager);
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
