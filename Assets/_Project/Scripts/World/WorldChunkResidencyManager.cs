using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Data;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Bitmask state for world chunk residency and streaming transitions.
    /// </summary>
    [Flags]
    public enum ChunkState : byte
    {
        Unloaded = 0,
        Resident = 1 << 0,
        Loading = 1 << 1,
        Evicting = 1 << 2,
        Staged = 1 << 3,
        LOD0 = 1 << 4,
        LOD1 = 1 << 5,
        HighPriority = 1 << 6,
        Pinned = 1 << 7
    }

    /// <summary>
    /// Hardware class used for streaming radius and async GPU upload budgets.
    /// </summary>
    public enum ChunkStreamingScalabilityTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Optional chunk-local readiness contract used to delay scatter until base voxel mesh baking finishes.
    /// </summary>
    public interface IChunkVoxelBakeReadiness
    {
        bool IsBaseVoxelMeshReady(long chunkId);
    }

    /// <summary>
    /// Native load request packet consumed by the main-thread Addressables dispatcher.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ChunkLoadRequest
    {
        public long ChunkId;
        public float DistanceSq;
        public byte Priority;
        public byte Flags;
        public ushort Padding0;
        public uint Frame;
        public ulong Padding1;
    }

    /// <summary>
    /// Native sort packet for load priority. Lower score is dispatched first.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct ChunkLoadSortRecord : IComparable<ChunkLoadSortRecord>
    {
        public long ChunkId;
        public float SortScore;

        /// <inheritdoc />
        public int CompareTo(ChunkLoadSortRecord other)
        {
            if (SortScore < other.SortScore)
                return -1;
            if (SortScore > other.SortScore)
                return 1;
            if (ChunkId < other.ChunkId)
                return -1;
            return ChunkId > other.ChunkId ? 1 : 0;
        }
    }

    /// <summary>
    /// Fixed black-box telemetry sample for the chunk residency system.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ChunkResidencyTelemetryEntry
    {
        public uint Frame;
        public uint Flags;
        public long FocusChunkId;
        public long PlayerGridX;
        public long PlayerGridY;
        public long PlayerGridZ;
        public float3 PlayerLocal;
        public ushort PendingLoads;
        public ushort ResidentCount;
        public ushort LoadingCount;
        public ushort EvictingCount;
        public uint StateHash;
    }

    /// <summary>
    /// Burst job that evaluates chunk residency by comparing the player AUP against chunk-center AUPs.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct RadiusBasedStreamingJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<long> ChunkIds;
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit> ChunkCenters;
        [ReadOnly] public NativeParallelHashMap<long, ChunkState> ChunkStates;
        public NativeList<long>.ParallelWriter ChunksToLoad;
        public NativeList<long>.ParallelWriter ChunksToUnload;
        public double3 PlayerAbsolute;
        public float3 PlayerVelocity;
        public double LoadRadiusSq;
        public double UnloadRadiusSq;
        public double PredictiveDistanceMeters;
        public double TailUnloadRadiusSq;
        public byte PredictiveEnabled;

        /// <inheritdoc />
        public void Execute(int index)
        {
            long chunkId = ChunkIds[index];
            ChunkState state = ChunkState.Unloaded;
            ChunkStates.TryGetValue(chunkId, out state);

            double3 player = PlayerAbsolute;
            double3 chunk = ToAbsoluteDouble3(ChunkCenters[index]);
            double3 delta = chunk - player;
            double distSq = math.lengthsq(delta);
            float speedSq = math.lengthsq(PlayerVelocity);
            bool usePrediction = PredictiveEnabled != 0 && PredictiveDistanceMeters > 0d && speedSq > 0.0001f;
            double3 velocityDirection = default;
            if (usePrediction)
            {
                float invSpeed = math.rsqrt(speedSq);
                velocityDirection = new double3(PlayerVelocity.x * invSpeed, PlayerVelocity.y * invSpeed, PlayerVelocity.z * invSpeed);
            }

            bool resident = HasFlag(state, ChunkState.Resident);
            bool loading = HasFlag(state, ChunkState.Loading);
            bool pinned = HasFlag(state, ChunkState.Pinned);
            bool evicting = HasFlag(state, ChunkState.Evicting);

            bool insideLoadZone = distSq <= LoadRadiusSq;
            if (!insideLoadZone && usePrediction)
            {
                double ahead = math.dot(delta, velocityDirection);
                if (ahead > 0d)
                {
                    double clampedAhead = math.min(ahead, PredictiveDistanceMeters);
                    double3 nearestDelta = delta - (velocityDirection * clampedAhead);
                    insideLoadZone = math.lengthsq(nearestDelta) <= LoadRadiusSq;
                }
            }

            if (!resident && !loading && !evicting && insideLoadZone)
            {
                ChunksToLoad.AddNoResize(chunkId);
                return;
            }

            double unloadSq = UnloadRadiusSq;
            if (usePrediction && math.dot(delta, velocityDirection) < 0d)
                unloadSq = TailUnloadRadiusSq;

            if (resident && !pinned && !evicting && distSq >= unloadSq)
                ChunksToUnload.AddNoResize(chunkId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(ChunkState state, ChunkState flag)
        {
            return ((byte)state & (byte)flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.Local.x,
                (position.GridY * CellSize) + position.Local.y,
                (position.GridZ * CellSize) + position.Local.z);
        }
    }

    /// <summary>
    /// Burst-native sort for the bounded load list. It prioritizes chunks nearest to the projected AUP without managed sorting.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
    public struct ChunkLoadPrioritySortJob : IJob
    {
        public NativeList<long> ChunksToLoad;
        public NativeList<ChunkLoadSortRecord> SortRecords;
        [ReadOnly] public NativeParallelHashMap<long, int> ChunkIndexById;
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit> ChunkCenters;
        public double3 ProjectedAbsolute;

        public void Execute()
        {
            SortRecords.Clear();
            int count = ChunksToLoad.Length;
            for (int i = 0; i < count; i++)
            {
                long chunkId = ChunksToLoad[i];
                float score = float.MaxValue;
                if (ChunkIndexById.TryGetValue(chunkId, out int index))
                {
                    double scoreSq = math.distancesq(ProjectedAbsolute, ToAbsoluteDouble3(ChunkCenters[index]));
                    score = (float)math.min(scoreSq, float.MaxValue);
                }

                SortRecords.AddNoResize(new ChunkLoadSortRecord
                {
                    ChunkId = chunkId,
                    SortScore = score
                });
            }

            SortRecords.Sort();
            for (int i = 0; i < count; i++)
                ChunksToLoad[i] = SortRecords[i].ChunkId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.Local.x,
                (position.GridY * CellSize) + position.Local.y,
                (position.GridZ * CellSize) + position.Local.z);
        }
    }

    /// <summary>
    /// Data-driven residency manager for Addressables-backed world chunks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4140)] // Streaming must register after dispatcher bootstrap and before world content lanes.
    public sealed class WorldChunkResidencyManager : MonoBehaviour, ITickable, ISlowTickable, IBaseAirlockEventListener, IDisposable
    {
        private const int DefaultMaxChunkCount = 512;
        private const int DefaultLoadQueueCapacity = 256;
        private const int TelemetryCapacity = 300;
        private const int MaxActivationsPerFrame = 5;
        private const int MemoryGuardBytes = 500 * 1024 * 1024;
        private const float DefaultLoadRadiusMeters = 500f;
        private const float DefaultUnloadRadiusMeters = 600f;
        private const float ChunkFadeSeconds = 2f;
        private const float ChunkFadeSecondsRcp = 0.5f;
        private const float PredictiveLookaheadSeconds = 5f;
        private const float TeleportDistanceMeters = 160f;
        private const int MaxPredictiveBiomePrefabs = 5;
        private const int HabitatTransitionPauseFrames = 180;
        private const int TeleportImmediateLoadDispatchBudget = 4;
        private const int LowTierLoadDispatchBudget = 1;
        private const int MiddleTierLoadDispatchBudget = 2;
        private const int HighTierLoadDispatchBudget = 3;
        private const int UltraTierLoadDispatchBudget = 4;
        private const int AssetLifecycleFarBehindDrainBudget = 8;
        private const long PredictiveVramAbortBytes = 1600L * 1024L * 1024L;
        private const long PredictiveVramResumeBytes = 1400L * 1024L * 1024L;
        private const float StreamerStressSpeedSqRcp = 0.00111111112f;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_ASSET_STREAMING_PREDICTIVE.bin";
        private const byte LoadRequestFlagPredictive = 1 << 0;
        private const byte LoadRequestFlagTeleport = 1 << 1;
        private const uint TelemetryInvalidAupFlag = 1u << 0;
        private const uint TelemetryShiftFlag = 1u << 1;
        private const uint TelemetryMemoryBreachFlag = 1u << 2;
        private const uint TelemetryTeleportFlag = 1u << 3;
        private const uint TelemetryPredictiveSuspendedFlag = 1u << 4;
        private const uint TelemetryPredictivePrewarmFaultFlag = 1u << 5;
        private const uint TelemetryActivationOverflowFlag = 1u << 6;
        private const uint TelemetryDuplicateChunkIdFlag = 1u << 7;
        private const uint TelemetryAdditiveSceneFaultFlag = 1u << 8;
        private const uint TelemetryReleaseAllResetFlag = 1u << 9;
        private const uint TelemetryAddressablesFaultFlag = 1u << 10;
        private const uint TelemetryActivationFaultFlag = 1u << 11;
        private const uint MemoryBreachContextHash = 0x43535452u; // "CSTR"
        private const uint NativeQueueOverflowWarningHash = 0x43534F56u; // "CSOV"
        private const uint TeleportContextHash = 0x53545250u; // "STRP"
        private const uint ChunkStateHashSeed = 2166136261u;
        private static readonly int _chunkFadeMaskId = Shader.PropertyToID("_ChunkFadeMask");
        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.World.ChunkResidency.Tick");
        private static readonly ProfilerMarker _loadDispatchMarker = new ProfilerMarker("H8.World.ChunkResidency.LoadDispatch");
        private static readonly ProfilerMarker _releaseMarker = new ProfilerMarker("H8.World.ChunkResidency.Release");

        private enum AdditiveSceneLoadState : byte
        {
            NotNeeded = 0,
            Pending = 1,
            Failed = 2
        }

        [Serializable]
        public struct ChunkDefinition
        {
            [Tooltip("Optional stable label for editor diagnostics. Not used by hot-path code.")]
            public string label;

            [Tooltip("Optional H8BiomeRecord hash from the Data Monolith. Zero falls back to chunk depth lookup.")]
            public uint biomeHash;

            [Tooltip("Absolute chunk center in meters before runtime floating-origin presentation offsets.")]
            public Vector3 absoluteCenterMeters;

            [Tooltip("Chunk size in meters used for the deterministic 64-bit chunk ID.")]
            [Min(1)] public int chunkSizeMeters;

            [Tooltip("Addressables address for the root chunk prefab or payload asset.")]
            public string addressableAddress;

            [Tooltip("Optional additive scene loaded for massive structural chunks.")]
            public string additiveSceneName;

            [Tooltip("True when this chunk should load through SceneManager.LoadSceneAsync(additive).")]
            public bool useAdditiveScene;

            [Tooltip("Never evict this chunk once it becomes resident.")]
            public bool pinned;

            [Tooltip("Prefab dependencies prewarmed into ObjectPoolManager when the chunk data is resident.")]
            public GameObject[] prefabDependencies;

            [Tooltip("Top prefab frequency list emitted from H8BiomeRecord/Data Monolith authoring. First five entries are predictive-prewarmed.")]
            public GameObject[] predictivePrewarmPrefabs;

            [Tooltip("Activation prefabs spawned from ObjectPoolManager in a five-per-frame Awaitable pass.")]
            public GameObject[] activationPrefabs;

            [Tooltip("Pool count per prefab dependency. Zero uses one warm instance per dependency.")]
            [Min(0)] public int warmupCountPerPrefab;

            [Tooltip("Wait for this optional voxel readiness provider before scatter/flora activation.")]
            public MonoBehaviour voxelBakeReadinessProvider;
        }

        [Header("Residency")]
        [Tooltip("Authoring records for streamable chunks. Runtime state is mirrored into NativeCollections.")]
        [SerializeField] private ChunkDefinition[] chunkDefinitions;

        [Tooltip("Hard cap for native chunk storage. Must be >= authored chunk count.")]
        [SerializeField, Min(1)] private int maxChunkCount = DefaultMaxChunkCount;

        [Tooltip("Native load request queue capacity.")]
        [SerializeField, Min(1)] private int loadQueueCapacity = DefaultLoadQueueCapacity;

        [Tooltip("Distance in meters where unloaded chunks are requested.")]
        [SerializeField, Min(1f)] private float loadRadiusMeters = DefaultLoadRadiusMeters;

        [Tooltip("Distance in meters where resident chunks are evicted. Must stay above load radius.")]
        [SerializeField, Min(1f)] private float unloadRadiusMeters = DefaultUnloadRadiusMeters;

        [Tooltip("Automatically schedule a residency evaluation after AUP origin-shift signals.")]
        [SerializeField] private bool reactToAupShiftSignals = true;

        [Tooltip("Apply QualitySettings async upload budgets at runtime based on the detected tier.")]
        [SerializeField] private bool applyAsyncUploadBudget = true;

        [Tooltip("Suspend predictive expansion while habitat or docking systems mark the player as inside dry space.")]
        [SerializeField] private bool suspendPredictiveStreamingInHabitat = true;

        [Header("Diagnostics")]
        [Tooltip("Current number of resident chunks.")]
        [SerializeField] private int _debugResidentChunks;

        [Tooltip("Current number of loading chunks.")]
        [SerializeField] private int _debugLoadingChunks;

        [Tooltip("Current number of evicting chunks.")]
        [SerializeField] private int _debugEvictingChunks;

        [Tooltip("Current native load request count.")]
        [SerializeField] private int _debugPendingLoadRequests;

        [Tooltip("Last observed AUP shift frame id.")]
        [SerializeField] private uint _debugLastAupShiftFrameId;

        [Tooltip("0..1 pressure metric for Streamer Stress UI. No string formatting in hot path.")]
        [SerializeField, Range(0f, 1f)] private float _debugStreamerStress01;

        [Tooltip("True when predictive loading is currently suspended by VRAM, habitat, or external systems.")]
        [SerializeField] private bool _debugPredictiveSuspended;

        private NativeArray<long> _chunkIds;
        private NativeArray<AbsoluteUniversePositionBlit> _chunkCenters;
        private NativeParallelHashMap<long, ChunkState> _chunkStates;
        private NativeParallelHashMap<long, int> _chunkIndexById;
        private NativeQueue<ChunkLoadRequest> _loadRequests;
        private NativeList<long> _chunksToLoad;
        private NativeList<long> _chunksToUnload;
        private NativeList<ChunkLoadSortRecord> _chunkLoadSortRecords;
        private NativeArray<ChunkResidencyTelemetryEntry> _telemetryRing;
        private JobHandle _residencyJobHandle;
        private bool _residencyJobScheduled;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredAirlockEvents;
        private bool _disposed;
        private bool _forceResidencyEvaluation;
        private bool _fadeActive;
        private bool _hasLastPlayerAup;
        private bool _externalPredictiveSuspended;
        private bool _habitatPredictivePauseActive;
        private bool _transportPredictivePauseActive;
        private bool _predictiveVramAborted;
        private bool _stateDiagnosticsDirty;
        private float _fadeTimer;
        private float _lastPredictionDistanceMeters;
        private float _loadQueueCapacityRcp;
        private float _maxChunkCountRcp;
        private float3 _lastPlayerVelocity;
        private AbsoluteUniversePositionBlit _lastPlayerAup;
        private AbsoluteUniversePositionBlit _lastProjectedAup;
        private AbsoluteUniversePosition _lastTeleportProbeAup;
        private int _chunkCount;
        private int _pendingLoadRequestCount;
        private int _pendingAdditiveSceneOperationCount;
        private int _telemetryCursor;
        private int _habitatTransitionPauseFrames;
        private uint _debugStateHash = ChunkStateHashSeed;
        private ChunkStreamingScalabilityTier _activeTier = (ChunkStreamingScalabilityTier)255;
        private ChunkStreamingScalabilityTier _resolvedTier = ChunkStreamingScalabilityTier.Low;
        private long[] _chunkIdsByDefinitionIndex;
        private GameObject[][] _spawnedInstancesByChunk;
        private int[] _spawnedCountsByChunk;
        private bool[] _activationInProgress;
        private int[] _activationVersions;
        private bool[] _predictivePrewarmInProgress;
        private bool[] _predictivePrewarmComplete;
        private int[] _predictivePrewarmVersions;
        private AsyncOperation[] _additiveSceneOperations;
        private bool[] _additiveSceneActivationRequested;
        private bool[] _additiveSceneLoaded;
        private bool[] _additiveSceneUnloadWhenLoaded;
        private bool[] _loadRequestQueuedByChunk;
        private bool[] _evictRequestQueuedByChunk;
        private long[] _deferredEvictChunkIds;
        private int _deferredEvictCount;
#if UNITY_ADDRESSABLES_EXIST
        private int _pendingAddressableLoadCount;
        private int _pendingAddressableCacheClearCount;
        private AsyncOperationHandle<GameObject>[] _addressableHandles;
        private bool[] _hasAddressableHandle;
        private bool[] _addressableLoadPending;
        private AsyncOperationHandle<bool>[] _addressableCacheClearHandles;
        private bool[] _hasAddressableCacheClearHandle;
#endif

        /// <summary>
        /// Number of authored chunks mirrored into native residency state.
        /// </summary>
        public int ChunkCount => _chunkCount;

        /// <summary>
        /// Streamer pressure metric exposed for lightweight UI binding.
        /// </summary>
        public float StreamerStress01 => _debugStreamerStress01;

        /// <summary>
        /// True while speculative prediction is disabled by VRAM, habitat, or external docking code.
        /// </summary>
        public bool IsPredictiveStreamingSuspended => PredictiveStreamingPausedNow;

        /// <summary>
        /// External docking/habitat code can suspend speculative streaming without taking a concrete dependency on this manager.
        /// </summary>
        public void SetPredictiveStreamingSuspended(bool suspended)
        {
            _externalPredictiveSuspended = suspended;
            _forceResidencyEvaluation = true;
        }

        /// <summary>
        /// Computes the deterministic 64-bit chunk ID from an Absolute Universe Position.
        /// </summary>
        /// <param name="position">Chunk center AUP.</param>
        /// <param name="chunkSizeMeters">Chunk size in meters.</param>
        /// <returns>Non-negative 64-bit chunk identifier.</returns>
        public static long BuildChunkId(in AbsoluteUniversePosition position, int chunkSizeMeters)
        {
            int safeChunkSize = math.max(1, chunkSizeMeters);
            int3 chunk = AbsoluteUniversePosition.ResolveChunkId(in position, safeChunkSize);
            ulong hash = 1469598103934665603UL;
            hash = MixHash(hash, (uint)chunk.x);
            hash = MixHash(hash, (uint)chunk.y);
            hash = MixHash(hash, (uint)chunk.z);
            hash = MixHash(hash, (uint)safeChunkSize);
            return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
        }

        /// <summary>
        /// Returns true when the chunk is currently resident.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        public bool IsResident(long chunkId)
        {
            if (_residencyJobScheduled)
                return false;

            return _chunkStates.IsCreated &&
                   _chunkStates.TryGetValue(chunkId, out ChunkState state) &&
                   HasFlag(state, ChunkState.Resident);
        }

        private void Awake()
        {
            ClampSettings();
            AllocateNativeState();
            AllocateManagedState();
            BuildChunkTables();
            _resolvedTier = ResolveScalabilityTier();
            ApplyAsyncUploadBudgetForTier(_resolvedTier);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            CompleteResidencyJobForTeardown();
            ReleaseAllChunks();
        }

        private void OnDestroy()
        {
            DisposeInternal(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeInternal(true);
        }

        private void DisposeInternal(bool releaseChunks)
        {
            if (_disposed)
                return;

            _disposed = true;
            CompleteResidencyJobForTeardown();

            if (releaseChunks)
                ReleaseAllChunks();

            DisposeNativeState();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            using (_tickMarker.Auto())
            {
                TickPredictiveSuspension();
                DrainAupShiftSignals();
                DetectAndHandleTeleport();
                CompleteResidencyJobIfFinished();
                ProcessResidencyResults();
                if (!_residencyJobScheduled)
                {
                    ProcessDeferredEvictions();
                    ProcessLoadDispatchBudget();
                    PollAddressableLoads();
                    PollAddressableCacheClears();
                }

                TryActivateReadySubScenes();
                UpdateChunkFade(deltaTime);
                UpdateStreamerStressMetric();
                WriteTelemetrySample(0L, 0u);
                if (!_residencyJobScheduled)
                    ScheduleForcedResidencyEvaluation();
            }
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (_chunkCount <= 0)
                return;

            if (_residencyJobScheduled)
                return;

            ScheduleResidencyJob();
        }

        /// <summary>
        /// Queues a load request for a chunk without creating duplicate loading work.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        /// <param name="priority">Priority byte. Higher value means more urgent for this queue.</param>
        public void RequestLoad(long chunkId, byte priority)
        {
            RequestLoad(chunkId, priority, 0, 0f);
        }

        private void RequestLoad(long chunkId, byte priority, byte flags, float distanceSq)
        {
            if (!_loadRequests.IsCreated || !_chunkStates.IsCreated || !_chunkIndexById.IsCreated)
                return;

            if (!_chunkIndexById.TryGetValue(chunkId, out int index))
                return;

            if (_loadRequestQueuedByChunk != null && _loadRequestQueuedByChunk[index])
                return;

            if (_pendingLoadRequestCount >= loadQueueCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(NativeQueueOverflowWarningHash, MemoryBreachContextHash, _pendingLoadRequestCount);
                return;
            }

            if (!_residencyJobScheduled)
            {
                if (!_chunkStates.TryGetValue(chunkId, out ChunkState state))
                    return;

                if (HasFlag(state, ChunkState.Resident) || HasFlag(state, ChunkState.Loading) || HasFlag(state, ChunkState.Evicting))
                    return;

                state |= ChunkState.Loading;
                SetChunkState(chunkId, state);
            }
            else
            {
                _forceResidencyEvaluation = true;
            }

            _loadRequests.Enqueue(new ChunkLoadRequest
            {
                ChunkId = chunkId,
                DistanceSq = distanceSq,
                Priority = priority,
                Flags = flags,
                Padding0 = 0,
                Frame = unchecked((uint)Time.frameCount),
                Padding1 = 0UL
            });
            if (_loadRequestQueuedByChunk != null)
                _loadRequestQueuedByChunk[index] = true;

            _pendingLoadRequestCount++;
            _debugPendingLoadRequests = _pendingLoadRequestCount;
        }

        /// <summary>
        /// Evicts a resident chunk and releases tracked Addressables handles.
        /// </summary>
        /// <param name="chunkId">Deterministic chunk id.</param>
        public void RequestEvict(long chunkId)
        {
            RequestEvict(chunkId, ShouldClearAddressableCacheOnEvict(chunkId));
        }

        private void RequestEvict(long chunkId, bool clearAddressableCache)
        {
            if (!_chunkStates.IsCreated || !_chunkIndexById.IsCreated)
                return;

            if (!_chunkIndexById.TryGetValue(chunkId, out int index))
                return;

            if (_residencyJobScheduled)
            {
                QueueDeferredEviction(index, chunkId);
                _forceResidencyEvaluation = true;
                return;
            }

            EvictChunkNow(index, chunkId, clearAddressableCache);
        }

        private void EvictChunkNow(int index, long chunkId, bool clearAddressableCache)
        {
            if (!_chunkStates.TryGetValue(chunkId, out ChunkState state))
                return;

            if (HasFlag(state, ChunkState.Pinned))
                return;

            state |= ChunkState.Evicting;
            state &= unchecked((ChunkState)~(byte)ChunkState.Resident);
            SetChunkState(chunkId, state);

            DespawnChunkInstances(index);
            ReleaseChunkHandles(index, clearAddressableCache);
            state = ChunkState.Unloaded;
            SetChunkState(chunkId, state);
            _forceResidencyEvaluation = true;
        }

        private void ClampSettings()
        {
            maxChunkCount = math.max(1, maxChunkCount);
            int authoredCount = chunkDefinitions != null ? chunkDefinitions.Length : 0;
            maxChunkCount = math.max(maxChunkCount, authoredCount);
            loadQueueCapacity = math.max(1, loadQueueCapacity);
            loadRadiusMeters = math.max(1f, loadRadiusMeters);
            unloadRadiusMeters = math.max(loadRadiusMeters + 1f, unloadRadiusMeters);
            _loadQueueCapacityRcp = math.rcp((float)loadQueueCapacity);
            _maxChunkCountRcp = math.rcp((float)maxChunkCount);
        }

        private void AllocateNativeState()
        {
            int capacity = math.max(1, maxChunkCount);
            // COLD ALLOC: NativeArray<long>[maxChunkCount] - chunk id SoA for Burst residency scans - owner: WorldChunkResidencyManager
            _chunkIds = new NativeArray<long>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<AbsoluteUniversePositionBlit>[maxChunkCount] - AUP center SoA for Burst residency scans - owner: WorldChunkResidencyManager
            _chunkCenters = new NativeArray<AbsoluteUniversePositionBlit>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeParallelHashMap<long,ChunkState>[maxChunkCount] - residency state table - owner: WorldChunkResidencyManager
            _chunkStates = new NativeParallelHashMap<long, ChunkState>(capacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<long,int>[maxChunkCount] - chunk id to definition index map - owner: WorldChunkResidencyManager
            _chunkIndexById = new NativeParallelHashMap<long, int>(capacity, Allocator.Persistent);
            // COLD ALLOC: NativeQueue<ChunkLoadRequest>[loadQueueCapacity] - throttled Addressables request lane - owner: WorldChunkResidencyManager
            _loadRequests = new NativeQueue<ChunkLoadRequest>(Allocator.Persistent);
            // COLD ALLOC: NativeList<long>[maxChunkCount] - Burst load output list - owner: WorldChunkResidencyManager
            _chunksToLoad = new NativeList<long>(capacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<long>[maxChunkCount] - Burst unload output list - owner: WorldChunkResidencyManager
            _chunksToUnload = new NativeList<long>(capacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<ChunkLoadSortRecord>[maxChunkCount] - native sort scratch for load prioritization - owner: WorldChunkResidencyManager
            _chunkLoadSortRecords = new NativeList<ChunkLoadSortRecord>(capacity, Allocator.Persistent);
            // COLD ALLOC: NativeArray<ChunkResidencyTelemetryEntry>[300] - black-box circular telemetry - owner: WorldChunkResidencyManager
            _telemetryRing = new NativeArray<ChunkResidencyTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            NativeMemorySentinel.RegisterNativeArray(_chunkIds, nameof(WorldChunkResidencyManager), nameof(_chunkIds), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_chunkCenters, nameof(WorldChunkResidencyManager), nameof(_chunkCenters), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_chunkStates, nameof(WorldChunkResidencyManager), nameof(_chunkStates), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_chunkIndexById, nameof(WorldChunkResidencyManager), nameof(_chunkIndexById), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeQueue(_loadRequests, loadQueueCapacity, nameof(WorldChunkResidencyManager), nameof(_loadRequests), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_chunksToLoad, nameof(WorldChunkResidencyManager), nameof(_chunksToLoad), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_chunksToUnload, nameof(WorldChunkResidencyManager), nameof(_chunksToUnload), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_chunkLoadSortRecords, nameof(WorldChunkResidencyManager), nameof(_chunkLoadSortRecords), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_telemetryRing, nameof(WorldChunkResidencyManager), nameof(_telemetryRing), NativeAllocationLifetime.Session);
        }

        private void BuildChunkTables()
        {
            _chunkCount = 0;
            if (chunkDefinitions == null)
                return;

            for (int i = 0; i < chunkDefinitions.Length && i < maxChunkCount; i++)
            {
                ChunkDefinition definition = chunkDefinitions[i];
                AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    definition.absoluteCenterMeters.x,
                    definition.absoluteCenterMeters.y,
                    definition.absoluteCenterMeters.z));
                if (!MathGuard.IsFinite(in centerAup))
                {
                    DumpTelemetry(TelemetryInvalidAupFlag);
                    continue;
                }

                int chunkSize = definition.chunkSizeMeters > 0 ? definition.chunkSizeMeters : Mathf.RoundToInt(math.max(1f, unloadRadiusMeters));
                long chunkId = BuildChunkId(in centerAup, chunkSize);
                if (_chunkIndexById.ContainsKey(chunkId))
                {
                    WriteTelemetrySample(chunkId, TelemetryDuplicateChunkIdFlag);
                    continue;
                }

                _chunkIds[_chunkCount] = chunkId;
                _chunkCenters[_chunkCount] = AbsoluteUniversePositionBlit.FromAup(in centerAup);
                _chunkIdsByDefinitionIndex[i] = chunkId;
                ChunkState initialState = definition.pinned ? ChunkState.Pinned : ChunkState.Unloaded;
                _chunkStates.TryAdd(chunkId, initialState);
                _chunkIndexById.TryAdd(chunkId, i);
                _chunkCount++;
            }

            _stateDiagnosticsDirty = true;
        }

        private void AllocateManagedState()
        {
            int count = math.max(1, chunkDefinitions != null ? chunkDefinitions.Length : 0);
            // COLD ALLOC: long[chunkDefinitions] - definition index to deterministic chunk id map - owner: WorldChunkResidencyManager
            _chunkIdsByDefinitionIndex = new long[count];
            // COLD ALLOC: GameObject[][][chunkDefinitions] - spawned instance tracking for chunk unload - owner: WorldChunkResidencyManager
            _spawnedInstancesByChunk = new GameObject[count][];
            // COLD ALLOC: int[chunkDefinitions] - spawned count tracking for chunk unload - owner: WorldChunkResidencyManager
            _spawnedCountsByChunk = new int[count];
            // COLD ALLOC: bool[chunkDefinitions] - activation Awaitable ownership guard - owner: WorldChunkResidencyManager
            _activationInProgress = new bool[count];
            // COLD ALLOC: int[chunkDefinitions] - activation generation guard for unload/reload races - owner: WorldChunkResidencyManager
            _activationVersions = new int[count];
            // COLD ALLOC: bool[chunkDefinitions] - predictive pool prewarm Awaitable ownership guard - owner: WorldChunkResidencyManager
            _predictivePrewarmInProgress = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - predictive pool prewarm completion guard - owner: WorldChunkResidencyManager
            _predictivePrewarmComplete = new bool[count];
            // COLD ALLOC: int[chunkDefinitions] - predictive prewarm generation guard for unload/reload races - owner: WorldChunkResidencyManager
            _predictivePrewarmVersions = new int[count];
            // COLD ALLOC: AsyncOperation[chunkDefinitions] - additive scene load handles - owner: WorldChunkResidencyManager
            _additiveSceneOperations = new AsyncOperation[count];
            // COLD ALLOC: bool[chunkDefinitions] - additive scene activation gate state - owner: WorldChunkResidencyManager
            _additiveSceneActivationRequested = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - additive scene loaded state - owner: WorldChunkResidencyManager
            _additiveSceneLoaded = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - additive scene deferred unload state - owner: WorldChunkResidencyManager
            _additiveSceneUnloadWhenLoaded = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - explicit/deferred load request duplicate guard - owner: WorldChunkResidencyManager
            _loadRequestQueuedByChunk = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - explicit/deferred evict request duplicate guard - owner: WorldChunkResidencyManager
            _evictRequestQueuedByChunk = new bool[count];
            // COLD ALLOC: long[loadQueueCapacity] - deferred evict ids while residency job owns state reads - owner: WorldChunkResidencyManager
            _deferredEvictChunkIds = new long[math.max(1, loadQueueCapacity)];
#if UNITY_ADDRESSABLES_EXIST
            // COLD ALLOC: AsyncOperationHandle<GameObject>[chunkDefinitions] - explicit Addressables release tracking - owner: WorldChunkResidencyManager
            _addressableHandles = new AsyncOperationHandle<GameObject>[count];
            // COLD ALLOC: bool[chunkDefinitions] - valid Addressables handle occupancy map - owner: WorldChunkResidencyManager
            _hasAddressableHandle = new bool[count];
            // COLD ALLOC: bool[chunkDefinitions] - Addressables completion poll occupancy map - owner: WorldChunkResidencyManager
            _addressableLoadPending = new bool[count];
            // COLD ALLOC: AsyncOperationHandle<bool>[chunkDefinitions] - explicit Addressables cache-clear handles - owner: WorldChunkResidencyManager
            _addressableCacheClearHandles = new AsyncOperationHandle<bool>[count];
            // COLD ALLOC: bool[chunkDefinitions] - Addressables cache-clear occupancy map - owner: WorldChunkResidencyManager
            _hasAddressableCacheClearHandle = new bool[count];
#endif

            if (chunkDefinitions == null)
                return;

            for (int i = 0; i < chunkDefinitions.Length; i++)
            {
                int activationCount = chunkDefinitions[i].activationPrefabs != null
                    ? chunkDefinitions[i].activationPrefabs.Length
                    : 0;
                if (activationCount > 0)
                {
                    // COLD ALLOC: GameObject[activationPrefabs] - chunk-local spawned instance slots - owner: WorldChunkResidencyManager
                    _spawnedInstancesByChunk[i] = new GameObject[activationCount];
                }
            }
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredAirlockEvents)
            {
                BaseAirlockEvents.Register(this);
                _registeredAirlockEvents = true;
            }
        }

        private void TryUnregister()
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

            if (_registeredAirlockEvents)
            {
                BaseAirlockEvents.Unregister(this);
                _registeredAirlockEvents = false;
            }
        }

        /// <inheritdoc />
        public void OnBaseAirlockEvent(in BaseAirlockEventPayload payload)
        {
            if (!suspendPredictiveStreamingInHabitat)
                return;

            BaseAirlockEventType eventType = (BaseAirlockEventType)payload.EventType;
            if (eventType == BaseAirlockEventType.CycleStarted)
            {
                _habitatTransitionPauseFrames = HabitatTransitionPauseFrames;
                _forceResidencyEvaluation = true;
                return;
            }

            if (eventType == BaseAirlockEventType.CycleCompleted || eventType == BaseAirlockEventType.EnvironmentChanged)
            {
                _habitatPredictivePauseActive = payload.Dry;
                _habitatTransitionPauseFrames = payload.Dry ? HabitatTransitionPauseFrames : 0;
                _forceResidencyEvaluation = true;
            }
        }

        private void TickPredictiveSuspension()
        {
            if (_habitatTransitionPauseFrames > 0)
                _habitatTransitionPauseFrames--;

            _transportPredictivePauseActive = false;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.PlayerTransportCoordinator != null)
            {
                PlayerTransportCoordinator coordinator = runtimeContext.PlayerTransportCoordinator;
                _transportPredictivePauseActive = coordinator.HasActiveTransportSource() &&
                                                 !coordinator.IsTransportActive() &&
                                                 coordinator.BlocksHandheldToolUsage();
            }
        }

        private void DetectAndHandleTeleport()
        {
            if (!TryResolvePlayerMotion(out AbsoluteUniversePosition playerAup, out _))
                return;

            if (!MathGuard.IsFinite(in playerAup))
                return;

            if (!_hasLastPlayerAup)
            {
                _lastTeleportProbeAup = playerAup;
                _hasLastPlayerAup = true;
                return;
            }

            double distSq = DistanceSq(in _lastTeleportProbeAup, in playerAup);
            double thresholdSq = (double)TeleportDistanceMeters * TeleportDistanceMeters;
            _lastTeleportProbeAup = playerAup;

            if (distSq < thresholdSq)
                return;

            HandleTeleport(in playerAup);
        }

        private void HandleTeleport(in AbsoluteUniversePosition playerAup)
        {
            CompleteResidencyJobForTeleport();
            ClearStreamingQueues();
            _lastPlayerAup = AbsoluteUniversePositionBlit.FromAup(in playerAup);
            _lastPlayerVelocity = default;
            _lastPredictionDistanceMeters = 0f;
            ForceImmediateRadiusLoad(in playerAup);
            for (int i = 0; i < TeleportImmediateLoadDispatchBudget && _pendingLoadRequestCount > 0; i++)
                ProcessOneLoadRequest();

            WriteTelemetrySample(0L, TelemetryTeleportFlag);
            GlobalTelemetryBus.PublishPerformanceWarning(TeleportContextHash, MemoryBreachContextHash, _pendingLoadRequestCount);
        }

        private void CompleteResidencyJobForTeleport()
        {
            if (!_residencyJobScheduled)
                return;

            // [BLOCKING_SYNC_POINT] Teleport invalidates queued residency data; complete once, then repopulate the new immediate radius.
            _residencyJobHandle.Complete();
            _residencyJobScheduled = false;
        }

        private void ClearStreamingQueues()
        {
            if (_loadRequests.IsCreated)
            {
                while (_loadRequests.TryDequeue(out _))
                {
                }
            }

            if (_chunksToLoad.IsCreated)
                _chunksToLoad.Clear();
            if (_chunksToUnload.IsCreated)
                _chunksToUnload.Clear();

            _pendingLoadRequestCount = 0;
            _debugPendingLoadRequests = 0;
            _deferredEvictCount = 0;

            if (_loadRequestQueuedByChunk != null)
            {
                for (int i = 0; i < _loadRequestQueuedByChunk.Length; i++)
                {
                    if (_loadRequestQueuedByChunk[i] && !IsChunkLoadInFlight(i))
                    {
                        long queuedChunkId = _chunkIdsByDefinitionIndex != null && (uint)i < (uint)_chunkIdsByDefinitionIndex.Length
                            ? _chunkIdsByDefinitionIndex[i]
                            : 0L;
                        if (queuedChunkId != 0L && _chunkStates.TryGetValue(queuedChunkId, out ChunkState queuedState))
                        {
                            queuedState &= unchecked((ChunkState)~(byte)ChunkState.Loading);
                            SetChunkState(queuedChunkId, queuedState);
                        }
                    }

                    _loadRequestQueuedByChunk[i] = false;
                }
            }

            if (_evictRequestQueuedByChunk != null)
            {
                for (int i = 0; i < _evictRequestQueuedByChunk.Length; i++)
                    _evictRequestQueuedByChunk[i] = false;
            }
        }

        private bool IsChunkLoadInFlight(int index)
        {
            if (_additiveSceneOperations != null &&
                (uint)index < (uint)_additiveSceneOperations.Length &&
                _additiveSceneOperations[index] != null)
            {
                return true;
            }

#if UNITY_ADDRESSABLES_EXIST
            return _hasAddressableHandle != null &&
                   (uint)index < (uint)_hasAddressableHandle.Length &&
                   _hasAddressableHandle[index];
#else
            return false;
#endif
        }

        private void ForceImmediateRadiusLoad(in AbsoluteUniversePosition playerAup)
        {
            double loadRadiusSq = (double)loadRadiusMeters * loadRadiusMeters;
            double playerX = ToAbsoluteX(in playerAup);
            double playerY = ToAbsoluteY(in playerAup);
            double playerZ = ToAbsoluteZ(in playerAup);

            for (int i = 0; i < _chunkCount; i++)
            {
                long chunkId = _chunkIds[i];
                if (!_chunkStates.TryGetValue(chunkId, out ChunkState state))
                    continue;

                if (HasFlag(state, ChunkState.Resident) || HasFlag(state, ChunkState.Loading) || HasFlag(state, ChunkState.Evicting))
                    continue;

                AbsoluteUniversePositionBlit center = _chunkCenters[i];
                double dx = ToAbsoluteX(in center) - playerX;
                double dy = ToAbsoluteY(in center) - playerY;
                double dz = ToAbsoluteZ(in center) - playerZ;
                double distSq = (dx * dx) + (dy * dy) + (dz * dz);
                if (distSq <= loadRadiusSq)
                    RequestLoad(chunkId, 4, LoadRequestFlagTeleport, (float)math.min(distSq, float.MaxValue));
            }

            _forceResidencyEvaluation = true;
        }

        private void ScheduleResidencyJob()
        {
            if (!TryResolvePlayerMotion(out AbsoluteUniversePosition playerAup, out float3 playerVelocity))
                return;

            if (!MathGuard.IsFinite(in playerAup) || !IsFinite(playerVelocity))
            {
                DumpTelemetry(TelemetryInvalidAupFlag);
                return;
            }

            ChunkStreamingScalabilityTier tier = _resolvedTier;
            bool predictivePaused = PredictiveStreamingPausedNow;
            _predictiveVramAborted = ResolvePredictiveVramAbortState();
            bool predictiveEnabled = !predictivePaused && !_predictiveVramAborted;
            float predictionDistanceMeters = predictiveEnabled ? ResolvePredictionDistanceMeters(playerVelocity, tier) : 0f;
            float tailUnloadRadiusMeters = predictiveEnabled ? ResolveTailUnloadRadiusMeters(predictionDistanceMeters) : unloadRadiusMeters;
            AbsoluteUniversePosition projectedAup = BuildProjectedAup(in playerAup, playerVelocity, predictionDistanceMeters);

            _lastPlayerAup = AbsoluteUniversePositionBlit.FromAup(in playerAup);
            _lastProjectedAup = AbsoluteUniversePositionBlit.FromAup(in projectedAup);
            _lastPlayerVelocity = playerVelocity;
            _lastPredictionDistanceMeters = predictionDistanceMeters;
            _debugPredictiveSuspended = !predictiveEnabled;

            _chunksToLoad.Clear();
            _chunksToUnload.Clear();
            RadiusBasedStreamingJob job = new RadiusBasedStreamingJob
            {
                ChunkIds = _chunkIds.GetSubArray(0, _chunkCount),
                ChunkCenters = _chunkCenters.GetSubArray(0, _chunkCount),
                ChunkStates = _chunkStates,
                ChunksToLoad = _chunksToLoad.AsParallelWriter(),
                ChunksToUnload = _chunksToUnload.AsParallelWriter(),
                PlayerAbsolute = ToAbsoluteDouble3(in playerAup),
                PlayerVelocity = playerVelocity,
                LoadRadiusSq = (double)loadRadiusMeters * loadRadiusMeters,
                UnloadRadiusSq = (double)unloadRadiusMeters * unloadRadiusMeters,
                PredictiveDistanceMeters = predictionDistanceMeters,
                TailUnloadRadiusSq = (double)tailUnloadRadiusMeters * tailUnloadRadiusMeters,
                PredictiveEnabled = predictiveEnabled ? (byte)1 : (byte)0
            };

            JobHandle scanHandle = job.Schedule(_chunkCount, 32);
            ChunkLoadPrioritySortJob sortJob = new ChunkLoadPrioritySortJob
            {
                ChunksToLoad = _chunksToLoad,
                SortRecords = _chunkLoadSortRecords,
                ChunkIndexById = _chunkIndexById,
                ChunkCenters = _chunkCenters,
                ProjectedAbsolute = ToAbsoluteDouble3(in projectedAup)
            };

            _residencyJobHandle = sortJob.Schedule(scanHandle);
            _residencyJobScheduled = true;
            _forceResidencyEvaluation = false;
        }

        private void CompleteResidencyJobIfFinished()
        {
            if (!_residencyJobScheduled || !_residencyJobHandle.IsCompleted)
                return;

            _residencyJobHandle.Complete();
            _residencyJobScheduled = false;
        }

        private void CompleteResidencyJobForTeardown()
        {
            if (!_residencyJobScheduled)
                return;

            _residencyJobHandle.Complete();
            _residencyJobScheduled = false;
        }

        private void ProcessResidencyResults()
        {
            if (_residencyJobScheduled)
                return;

            for (int i = 0; i < _chunksToLoad.Length; i++)
            {
                long chunkId = _chunksToLoad[i];
                byte flags = ResolveLoadFlagsForChunk(chunkId);
                byte priority = HasFlag(flags, LoadRequestFlagPredictive) ? (byte)2 : (byte)3;
                RequestLoad(chunkId, priority, flags, ResolveProjectedDistanceSq(chunkId));
            }

            for (int i = 0; i < _chunksToUnload.Length; i++)
            {
                long chunkId = _chunksToUnload[i];
                bool clearCache = ShouldClearAddressableCacheOnEvict(chunkId);
                RequestEvict(chunkId, clearCache);
            }

            _chunksToLoad.Clear();
            _chunksToUnload.Clear();
        }

        private void ScheduleForcedResidencyEvaluation()
        {
            if (!_forceResidencyEvaluation || _residencyJobScheduled || _chunkCount <= 0)
                return;

            ScheduleResidencyJob();
        }

        private void ProcessDeferredEvictions()
        {
            if (_residencyJobScheduled || _deferredEvictCount <= 0 || _deferredEvictChunkIds == null)
                return;

            int count = _deferredEvictCount;
            _deferredEvictCount = 0;
            for (int i = 0; i < count; i++)
            {
                long chunkId = _deferredEvictChunkIds[i];
                _deferredEvictChunkIds[i] = 0L;
                if (!_chunkIndexById.TryGetValue(chunkId, out int index))
                    continue;

                if (_evictRequestQueuedByChunk != null)
                    _evictRequestQueuedByChunk[index] = false;

                EvictChunkNow(index, chunkId, ShouldClearAddressableCacheOnEvict(chunkId));
            }
        }

        private void QueueDeferredEviction(int index, long chunkId)
        {
            if (_deferredEvictChunkIds == null || _evictRequestQueuedByChunk == null || (uint)index >= (uint)_evictRequestQueuedByChunk.Length)
                return;

            if (_evictRequestQueuedByChunk[index])
                return;

            if (_deferredEvictCount >= _deferredEvictChunkIds.Length)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(NativeQueueOverflowWarningHash, MemoryBreachContextHash, _deferredEvictCount);
                return;
            }

            _evictRequestQueuedByChunk[index] = true;
            _deferredEvictChunkIds[_deferredEvictCount] = chunkId;
            _deferredEvictCount++;
        }

        private void ProcessLoadDispatchBudget()
        {
            if (_pendingLoadRequestCount <= 0)
                return;

            int budget = ResolveLoadDispatchBudget();
            for (int i = 0; i < budget && _pendingLoadRequestCount > 0; i++)
                ProcessOneLoadRequest();
        }

        private int ResolveLoadDispatchBudget()
        {
            if (_predictiveVramAborted)
                return LowTierLoadDispatchBudget;

            ChunkStreamingScalabilityTier tier = _resolvedTier;
            if (tier == ChunkStreamingScalabilityTier.Ultra)
                return UltraTierLoadDispatchBudget;
            if (tier == ChunkStreamingScalabilityTier.High)
                return HighTierLoadDispatchBudget;
            if (tier == ChunkStreamingScalabilityTier.Middle)
                return MiddleTierLoadDispatchBudget;
            return LowTierLoadDispatchBudget;
        }

        private void ProcessOneLoadRequest()
        {
            if (_pendingLoadRequestCount <= 0 || !_loadRequests.IsCreated)
                return;

            using (_loadDispatchMarker.Auto())
            {
                if (!_loadRequests.TryDequeue(out ChunkLoadRequest request))
                    return;

                _pendingLoadRequestCount = math.max(0, _pendingLoadRequestCount - 1);
                _debugPendingLoadRequests = _pendingLoadRequestCount;
                if (!_chunkIndexById.TryGetValue(request.ChunkId, out int index))
                    return;

                if (_loadRequestQueuedByChunk != null)
                    _loadRequestQueuedByChunk[index] = false;

                if (!_chunkStates.TryGetValue(request.ChunkId, out ChunkState state))
                    return;

                if (HasFlag(state, ChunkState.Resident) || HasFlag(state, ChunkState.Evicting))
                    return;

                if (!HasFlag(state, ChunkState.Loading))
                {
                    state |= ChunkState.Loading;
                    SetChunkState(request.ChunkId, state);
                }

                if (RuntimeWatchdog.GetAvailableMemory() < MemoryGuardBytes)
                {
                    GlobalTelemetryBus.PublishMemoryBreachEvent(MemoryBreachContextHash, Profiler.GetTotalReservedMemoryLong() * GlobalTelemetryBus.BytesToMegabytes);
                    WriteTelemetrySample(request.ChunkId, TelemetryMemoryBreachFlag);
                    ClearLoadingFlag(request.ChunkId);
                    return;
                }

                bool predictiveAbortNow = ResolvePredictiveVramAbortState();
                _predictiveVramAborted = predictiveAbortNow;
                if (HasFlag(request.Flags, LoadRequestFlagPredictive) && predictiveAbortNow)
                {
                    _debugPredictiveSuspended = true;
                    WriteTelemetrySample(request.ChunkId, TelemetryPredictiveSuspendedFlag);
                    ClearLoadingFlag(request.ChunkId);
                    return;
                }

                DispatchChunkLoad(index, request.ChunkId, HasFlag(request.Flags, LoadRequestFlagPredictive));
            }
        }

        private void DispatchChunkLoad(int index, long chunkId, bool predictive)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return;

            ChunkDefinition definition = chunkDefinitions[index];
            AdditiveSceneLoadState additiveSceneState = BeginOrTrackAdditiveSceneLoad(index, chunkId, in definition);
            if (additiveSceneState == AdditiveSceneLoadState.Failed)
            {
                ClearLoadingFlag(chunkId);
                return;
            }

            if (predictive)
                BeginPredictivePrewarm(index);
            else
                WarmChunkPrefabDependencies(index);

#if UNITY_ADDRESSABLES_EXIST
            if (!string.IsNullOrEmpty(definition.addressableAddress))
            {
                if (_addressableHandles == null ||
                    _hasAddressableHandle == null ||
                    _addressableLoadPending == null ||
                    (uint)index >= (uint)_addressableHandles.Length ||
                    (uint)index >= (uint)_hasAddressableHandle.Length ||
                    (uint)index >= (uint)_addressableLoadPending.Length)
                {
                    WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                    ReleaseChunkHandles(index);
                    ClearLoadingFlag(chunkId);
                    return;
                }

                if (!_hasAddressableHandle[index])
                {
                    _addressableHandles[index] = Addressables.LoadAssetAsync<GameObject>(definition.addressableAddress);
                    _hasAddressableHandle[index] = true;
                    _addressableLoadPending[index] = true;
                    _pendingAddressableLoadCount++;
                    return;
                }

                if (_addressableLoadPending[index])
                    return;

                AsyncOperationHandle<GameObject> handle = _addressableHandles[index];
                if (!handle.IsValid() || !handle.IsDone || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                    ReleaseChunkHandles(index);
                    ClearLoadingFlag(chunkId);
                    return;
                }

                if (additiveSceneState == AdditiveSceneLoadState.Pending)
                    return;

                PromoteChunkResident(index, chunkId, handle.Result);
                return;
            }
#else
            if (!string.IsNullOrEmpty(definition.addressableAddress))
            {
                WriteTelemetrySample(chunkId, TelemetryAddressablesFaultFlag);
                ReleaseChunkHandles(index);
                ClearLoadingFlag(chunkId);
                return;
            }
#endif

            if (additiveSceneState == AdditiveSceneLoadState.Pending)
                return;

            PromoteChunkResident(index, chunkId, null);
        }

        private AdditiveSceneLoadState BeginOrTrackAdditiveSceneLoad(int index, long chunkId, in ChunkDefinition definition)
        {
            if (!definition.useAdditiveScene || string.IsNullOrEmpty(definition.additiveSceneName))
                return AdditiveSceneLoadState.NotNeeded;

            if (_additiveSceneLoaded == null ||
                _additiveSceneOperations == null ||
                _additiveSceneActivationRequested == null ||
                _additiveSceneUnloadWhenLoaded == null ||
                (uint)index >= (uint)_additiveSceneLoaded.Length ||
                (uint)index >= (uint)_additiveSceneOperations.Length ||
                (uint)index >= (uint)_additiveSceneActivationRequested.Length ||
                (uint)index >= (uint)_additiveSceneUnloadWhenLoaded.Length)
            {
                WriteTelemetrySample(chunkId, TelemetryAdditiveSceneFaultFlag);
                return AdditiveSceneLoadState.Failed;
            }

            if (_additiveSceneLoaded[index])
            {
                return AdditiveSceneLoadState.NotNeeded;
            }

            if (_additiveSceneOperations[index] != null)
            {
                _additiveSceneUnloadWhenLoaded[index] = false;
                return AdditiveSceneLoadState.Pending;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(definition.additiveSceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                WriteTelemetrySample(chunkId, TelemetryAdditiveSceneFaultFlag);
                return AdditiveSceneLoadState.Failed;
            }

            operation.allowSceneActivation = false;
            _additiveSceneOperations[index] = operation;
            _additiveSceneActivationRequested[index] = false;
            _pendingAdditiveSceneOperationCount++;
            return AdditiveSceneLoadState.Pending;
        }

        private void PollAddressableLoads()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_addressableLoadPending == null ||
                _addressableHandles == null ||
                _chunkIdsByDefinitionIndex == null ||
                _pendingAddressableLoadCount <= 0)
            {
                return;
            }

            int count = math.min(_addressableLoadPending.Length, math.min(_addressableHandles.Length, _chunkIdsByDefinitionIndex.Length));
            for (int i = 0; i < count; i++)
            {
                if (!_addressableLoadPending[i])
                    continue;

                AsyncOperationHandle<GameObject> handle = _addressableHandles[i];
                long chunkId = _chunkIdsByDefinitionIndex[i];
                if (chunkId == 0L || !_chunkIndexById.TryGetValue(chunkId, out _))
                {
                    ReleaseChunkHandles(i);
                    continue;
                }

                if (!handle.IsValid())
                {
                    ReleaseChunkHandles(i);
                    ClearLoadingFlag(chunkId);
                    continue;
                }

                if (!handle.IsDone)
                    continue;

                if (!_chunkStates.TryGetValue(chunkId, out ChunkState state))
                {
                    ReleaseChunkHandles(i);
                    continue;
                }

                if (HasFlag(state, ChunkState.Resident))
                {
                    ClearAddressableLoadPending(i);
                    continue;
                }

                if (!HasFlag(state, ChunkState.Loading) || HasFlag(state, ChunkState.Evicting))
                {
                    ReleaseChunkHandles(i);
                    ClearLoadingFlag(chunkId);
                    continue;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (IsAdditiveSceneLoadPending(i))
                        continue;

                    ClearAddressableLoadPending(i);
                    PromoteChunkResident(i, chunkId, handle.Result);
                }
                else
                {
                    ReleaseChunkHandles(i);
                    ClearLoadingFlag(chunkId);
                }
            }
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private void ClearAddressableLoadPending(int index)
        {
            if (_addressableLoadPending == null ||
                (uint)index >= (uint)_addressableLoadPending.Length ||
                !_addressableLoadPending[index])
            {
                return;
            }

            _addressableLoadPending[index] = false;
            _pendingAddressableLoadCount = math.max(0, _pendingAddressableLoadCount - 1);
        }
#endif

        private void PromoteChunkResident(int index, long chunkId, GameObject loadedPrefab)
        {
            ChunkState state = ChunkState.Resident | ChunkState.Staged | ChunkState.LOD0;
            if (chunkDefinitions[index].pinned)
                state |= ChunkState.Pinned;

            SetChunkState(chunkId, state);
            if (loadedPrefab != null)
                WarmPrefab(loadedPrefab, math.max(1, chunkDefinitions[index].warmupCountPerPrefab));

            StartFade();
            if (_activationInProgress == null ||
                _activationVersions == null ||
                (uint)index >= (uint)_activationInProgress.Length ||
                (uint)index >= (uint)_activationVersions.Length)
            {
                WriteTelemetrySample(chunkId, TelemetryActivationFaultFlag);
                ClearStagedFlag(chunkId);
                return;
            }

            if (!_activationInProgress[index])
            {
                _ = ActivateChunkAsync(index, _activationVersions[index], destroyCancellationToken);
            }
        }

        private async Awaitable ActivateChunkAsync(int index, int version, CancellationToken cancellationToken)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0) ||
                _activationInProgress == null ||
                _chunkIdsByDefinitionIndex == null ||
                (uint)index >= (uint)_activationInProgress.Length ||
                (uint)index >= (uint)_chunkIdsByDefinitionIndex.Length)
            {
                return;
            }

            _activationInProgress[index] = true;
            try
            {
                long chunkId = _chunkIdsByDefinitionIndex[index];
                if (!IsActivationCurrent(index, version, chunkId))
                    return;

                if (_spawnedInstancesByChunk == null ||
                    _spawnedCountsByChunk == null ||
                    (uint)index >= (uint)_spawnedInstancesByChunk.Length ||
                    (uint)index >= (uint)_spawnedCountsByChunk.Length)
                {
                    WriteTelemetrySample(chunkId, TelemetryActivationFaultFlag);
                    ClearStagedFlag(chunkId);
                    return;
                }

                while (IsPredictivePrewarmBusy(index) || !IsChunkVoxelBakeReady(index, chunkId))
                {
                    if (!IsActivationCurrent(index, version, chunkId))
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                    if (!IsActivationCurrent(index, version, chunkId))
                        return;
                }

                ChunkDefinition definition = chunkDefinitions[index];
                GameObject[] prefabs = definition.activationPrefabs;
                if (prefabs == null || prefabs.Length == 0)
                {
                    if (IsActivationCurrent(index, version, chunkId))
                        ClearStagedFlag(chunkId);
                    return;
                }

                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool == null)
                {
                    if (IsActivationCurrent(index, version, chunkId))
                        ClearStagedFlag(chunkId);
                    return;
                }

                int spawnedThisFrame = 0;
                GameObject[] slots = _spawnedInstancesByChunk[index];
                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (!IsActivationCurrent(index, version, chunkId))
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    GameObject prefab = prefabs[i];
                    if (prefab != null && slots != null)
                    {
                        GameObject instance = pool.Spawn(prefab, definition.absoluteCenterMeters, Quaternion.identity);
                        if (instance != null)
                        {
                            int slotIndex = _spawnedCountsByChunk[index];
                            if ((uint)slotIndex < (uint)slots.Length)
                            {
                                slots[slotIndex] = instance;
                                _spawnedCountsByChunk[index] = slotIndex + 1;
                            }
                            else
                            {
                                _spawnedCountsByChunk[index] = slots.Length;
                                pool.Despawn(instance);
                                WriteTelemetrySample(chunkId, TelemetryActivationOverflowFlag);
                            }
                        }
                    }

                    spawnedThisFrame++;
                    if (spawnedThisFrame >= MaxActivationsPerFrame && i + 1 < prefabs.Length)
                    {
                        spawnedThisFrame = 0;
                        await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                        if (!IsActivationCurrent(index, version, chunkId))
                            return;
                    }
                }

                if (IsActivationCurrent(index, version, chunkId))
                    ClearStagedFlag(chunkId);
            }
            finally
            {
                if (_activationVersions != null &&
                    _activationInProgress != null &&
                    (uint)index < (uint)_activationVersions.Length &&
                    (uint)index < (uint)_activationInProgress.Length &&
                    _activationVersions[index] == version)
                {
                    _activationInProgress[index] = false;
                }
            }
        }

        private bool IsActivationCurrent(int index, int version, long chunkId)
        {
            if (_disposed ||
                _activationVersions == null ||
                _activationInProgress == null ||
                (uint)index >= (uint)_activationVersions.Length ||
                (uint)index >= (uint)_activationInProgress.Length ||
                _activationVersions[index] != version)
            {
                return false;
            }

            return _chunkStates.TryGetValue(chunkId, out ChunkState state) &&
                   HasFlag(state, ChunkState.Resident) &&
                   HasFlag(state, ChunkState.Staged);
        }

        private bool IsPredictivePrewarmBusy(int index)
        {
            return _predictivePrewarmInProgress != null &&
                   (uint)index < (uint)_predictivePrewarmInProgress.Length &&
                   _predictivePrewarmInProgress[index];
        }

        private bool IsAdditiveSceneLoadPending(int index)
        {
            if (_additiveSceneLoaded == null ||
                _additiveSceneOperations == null ||
                chunkDefinitions == null ||
                (uint)index >= (uint)_additiveSceneLoaded.Length ||
                (uint)index >= (uint)chunkDefinitions.Length)
            {
                return false;
            }

            ChunkDefinition definition = chunkDefinitions[index];
            return definition.useAdditiveScene &&
                   !string.IsNullOrEmpty(definition.additiveSceneName) &&
                   !_additiveSceneLoaded[index];
        }

        private void TryPromoteAfterAdditiveSceneReady(int index, long chunkId)
        {
            if (chunkId == 0L || !_chunkStates.TryGetValue(chunkId, out ChunkState state))
                return;

            if (!HasFlag(state, ChunkState.Loading) ||
                HasFlag(state, ChunkState.Resident) ||
                HasFlag(state, ChunkState.Evicting))
            {
                return;
            }

#if UNITY_ADDRESSABLES_EXIST
            if (_addressableLoadPending != null &&
                (uint)index < (uint)_addressableLoadPending.Length &&
                _addressableLoadPending[index])
            {
                return;
            }
#endif

            PromoteChunkResident(index, chunkId, null);
        }

        private void TryActivateReadySubScenes()
        {
            if (_additiveSceneOperations == null ||
                _additiveSceneActivationRequested == null ||
                _additiveSceneLoaded == null ||
                _additiveSceneUnloadWhenLoaded == null ||
                _chunkIdsByDefinitionIndex == null ||
                chunkDefinitions == null ||
                _pendingAdditiveSceneOperationCount <= 0)
            {
                return;
            }

            int count = math.min(
                _additiveSceneOperations.Length,
                math.min(
                    _additiveSceneActivationRequested.Length,
                    math.min(
                        _additiveSceneLoaded.Length,
                        math.min(
                            _additiveSceneUnloadWhenLoaded.Length,
                            math.min(_chunkIdsByDefinitionIndex.Length, chunkDefinitions.Length)))));
            for (int i = 0; i < count; i++)
            {
                AsyncOperation operation = _additiveSceneOperations[i];
                if (operation == null)
                    continue;

                if (!_additiveSceneActivationRequested[i])
                {
                    if (operation.progress < 0.9f)
                        continue;

                    operation.allowSceneActivation = true;
                    _additiveSceneActivationRequested[i] = true;
                    return;
                }

                if (!operation.isDone)
                    continue;

                _additiveSceneLoaded[i] = true;
                _additiveSceneOperations[i] = null;
                _pendingAdditiveSceneOperationCount = math.max(0, _pendingAdditiveSceneOperationCount - 1);
                if (_additiveSceneUnloadWhenLoaded[i])
                {
                    UnloadAdditiveScene(i);
                    return;
                }

                TryPromoteAfterAdditiveSceneReady(i, _chunkIdsByDefinitionIndex[i]);
            }
        }

        private void WarmChunkPrefabDependencies(int index)
        {
            ChunkDefinition definition = chunkDefinitions[index];
            GameObject[] dependencies = definition.prefabDependencies;
            if (dependencies == null || dependencies.Length == 0)
                return;

            int warmupCount = math.max(1, definition.warmupCountPerPrefab);
            for (int i = 0; i < dependencies.Length; i++)
                WarmPrefab(dependencies[i], warmupCount);

            if (_predictivePrewarmComplete != null && (uint)index < (uint)_predictivePrewarmComplete.Length)
                _predictivePrewarmComplete[index] = true;
        }

        private void BeginPredictivePrewarm(int index)
        {
            if (_predictiveVramAborted || _predictivePrewarmInProgress == null || _predictivePrewarmComplete == null || _predictivePrewarmVersions == null)
                return;

            if ((uint)index >= (uint)_predictivePrewarmInProgress.Length || _predictivePrewarmInProgress[index] || _predictivePrewarmComplete[index])
                return;

            _predictivePrewarmInProgress[index] = true;
            int version = unchecked(++_predictivePrewarmVersions[index]);
            _ = PredictivePrewarmAsync(index, version, destroyCancellationToken);
        }

        private async Awaitable PredictivePrewarmAsync(int index, int version, CancellationToken cancellationToken)
        {
            bool completed = false;
            try
            {
                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool == null || (uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                    return;

                ChunkDefinition definition = chunkDefinitions[index];
                TryResolveBiomeRecordForChunk(index, out _);
                GameObject[] prefabs = ResolvePredictivePrefabList(in definition);
                int count = prefabs != null ? math.min(MaxPredictiveBiomePrefabs, prefabs.Length) : 0;
                int warmupCount = math.max(1, definition.warmupCountPerPrefab);
                for (int i = 0; i < count; i++)
                {
                    if (!IsPredictivePrewarmCurrent(index, version))
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    GameObject prefab = prefabs[i];
                    if (prefab != null && !HasEarlierPrefab(prefabs, i, prefab))
                    {
                        await pool.WarmupPrefabAsync(prefab, warmupCount, 0.2d, cancellationToken);

                        if (!IsPredictivePrewarmCurrent(index, version))
                            return;
                    }

                    if (i + 1 < count)
                    {
                        await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                        if (!IsPredictivePrewarmCurrent(index, version))
                            return;
                    }
                }

                completed = true;
            }
            catch (OperationCanceledException)
            {
                completed = false;
            }
            catch (Exception)
            {
                completed = false;
                long chunkId = 0L;
                if (_chunkIds.IsCreated && (uint)index < (uint)_chunkIds.Length)
                    chunkId = _chunkIds[index];
                WriteTelemetrySample(chunkId, TelemetryPredictivePrewarmFaultFlag);
            }
            finally
            {
                if (_predictivePrewarmVersions != null &&
                    _predictivePrewarmInProgress != null &&
                    _predictivePrewarmComplete != null &&
                    (uint)index < (uint)_predictivePrewarmVersions.Length &&
                    _predictivePrewarmVersions[index] == version)
                {
                    _predictivePrewarmInProgress[index] = false;
                    _predictivePrewarmComplete[index] = completed;
                }
            }
        }

        private bool IsPredictivePrewarmCurrent(int index, int version)
        {
            return !_disposed &&
                   _predictivePrewarmVersions != null &&
                   (uint)index < (uint)_predictivePrewarmVersions.Length &&
                   _predictivePrewarmVersions[index] == version;
        }

        private static bool HasEarlierPrefab(GameObject[] prefabs, int index, GameObject prefab)
        {
            for (int i = 0; i < index; i++)
            {
                if (ReferenceEquals(prefabs[i], prefab))
                    return true;
            }

            return false;
        }

        private static GameObject[] ResolvePredictivePrefabList(in ChunkDefinition definition)
        {
            if (definition.predictivePrewarmPrefabs != null && definition.predictivePrewarmPrefabs.Length > 0)
                return definition.predictivePrewarmPrefabs;
            if (definition.prefabDependencies != null && definition.prefabDependencies.Length > 0)
                return definition.prefabDependencies;
            return definition.activationPrefabs;
        }

        private unsafe bool TryResolveBiomeRecordForChunk(int index, out H8BiomeRecord record)
        {
            record = default;
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return false;

            ChunkDefinition definition = chunkDefinitions[index];
            if (definition.biomeHash != 0u && TryResolveBiomeRecord(definition.biomeHash, out record))
                return true;

            float depthMeters = math.max(0f, -definition.absoluteCenterMeters.y);
            H8BiomeRecord* records = (H8BiomeRecord*)H8StaticDataArena.GetSectionDataPointer(
                H8DataSectionId.Biomes,
                H8DataLayoutConstants.BiomeRecordSize,
                out int count);
            if (records == null || count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                H8BiomeRecord candidate = records[i];
                if (depthMeters < candidate.MinDepthMeters || depthMeters > candidate.MaxDepthMeters)
                    continue;

                record = candidate;
                return true;
            }

            return false;
        }

        private static unsafe bool TryResolveBiomeRecord(uint biomeHash, out H8BiomeRecord record)
        {
            record = default;
            if (biomeHash == 0u)
                return false;

            H8BiomeRecord* records = (H8BiomeRecord*)H8StaticDataArena.GetSectionDataPointer(
                H8DataSectionId.Biomes,
                H8DataLayoutConstants.BiomeRecordSize,
                out int count);
            if (records == null || count <= 0)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                H8BiomeRecord candidate = records[mid];
                if (candidate.BiomeHash == biomeHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.BiomeHash < biomeHash)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return false;
        }

        private bool IsChunkVoxelBakeReady(int index, long chunkId)
        {
            if ((uint)index >= (uint)(chunkDefinitions != null ? chunkDefinitions.Length : 0))
                return true;

            if (chunkDefinitions[index].voxelBakeReadinessProvider is IChunkVoxelBakeReadiness readiness)
                return readiness.IsBaseVoxelMeshReady(chunkId);

            return true;
        }

        private void ClearStagedFlag(long chunkId)
        {
            if (!_chunkStates.TryGetValue(chunkId, out ChunkState state) || !HasFlag(state, ChunkState.Staged))
                return;

            state &= unchecked((ChunkState)~(byte)ChunkState.Staged);
            SetChunkState(chunkId, state);
        }

        private static void WarmPrefab(GameObject prefab, int count)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null || prefab == null)
                return;

            pool.Warmup(prefab, count);
        }

        private void DespawnChunkInstances(int index)
        {
            if (_spawnedInstancesByChunk == null ||
                _spawnedCountsByChunk == null ||
                (uint)index >= (uint)_spawnedInstancesByChunk.Length ||
                (uint)index >= (uint)_spawnedCountsByChunk.Length)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject[] slots = _spawnedInstancesByChunk[index];
            int count = _spawnedCountsByChunk[index];
            if (slots != null && count > slots.Length)
                count = slots.Length;
            if (pool != null && slots != null)
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject instance = slots[i];
                    slots[i] = null;
                    if (instance != null)
                        pool.Despawn(instance);
                }
            }

            _spawnedCountsByChunk[index] = 0;
        }

        private void ReleaseChunkHandles(int index, bool clearAddressableCache = false)
        {
            bool hasDefinition = chunkDefinitions != null && (uint)index < (uint)chunkDefinitions.Length;
            using (_releaseMarker.Auto())
            {
#if UNITY_ADDRESSABLES_EXIST
                if (_hasAddressableHandle != null &&
                    _addressableHandles != null &&
                    (uint)index < (uint)_hasAddressableHandle.Length &&
                    (uint)index < (uint)_addressableHandles.Length &&
                    _hasAddressableHandle[index])
                {
                    ClearAddressableLoadPending(index);
                    AsyncOperationHandle<GameObject> handle = _addressableHandles[index];
                    if (handle.IsValid())
                        Addressables.Release(handle);

                    _addressableHandles[index] = default;
                    _hasAddressableHandle[index] = false;
                }

                if (clearAddressableCache)
                    RequestAddressablesCacheClear(index);
#endif
                if (clearAddressableCache)
                    GlobalRegistry.AssetLifecycle?.DrainPendingReleaseQueueBudgeted(AssetLifecycleFarBehindDrainBudget);

                if (_predictivePrewarmVersions != null && (uint)index < (uint)_predictivePrewarmVersions.Length)
                    _predictivePrewarmVersions[index] = unchecked(_predictivePrewarmVersions[index] + 1);
                if (_predictivePrewarmInProgress != null && (uint)index < (uint)_predictivePrewarmInProgress.Length)
                    _predictivePrewarmInProgress[index] = false;
                if (_predictivePrewarmComplete != null && (uint)index < (uint)_predictivePrewarmComplete.Length)
                    _predictivePrewarmComplete[index] = false;
                if (_activationVersions != null && (uint)index < (uint)_activationVersions.Length)
                    _activationVersions[index] = unchecked(_activationVersions[index] + 1);
                if (_activationInProgress != null && (uint)index < (uint)_activationInProgress.Length)
                    _activationInProgress[index] = false;

                if (_additiveSceneLoaded == null ||
                    _additiveSceneOperations == null ||
                    _additiveSceneActivationRequested == null ||
                    _additiveSceneUnloadWhenLoaded == null ||
                    !hasDefinition ||
                    (uint)index >= (uint)_additiveSceneLoaded.Length ||
                    (uint)index >= (uint)_additiveSceneOperations.Length ||
                    (uint)index >= (uint)_additiveSceneActivationRequested.Length ||
                    (uint)index >= (uint)_additiveSceneUnloadWhenLoaded.Length ||
                    string.IsNullOrEmpty(chunkDefinitions[index].additiveSceneName))
                {
                    return;
                }

                AsyncOperation operation = _additiveSceneOperations[index];
                if (operation != null && !_additiveSceneLoaded[index])
                {
                    _additiveSceneUnloadWhenLoaded[index] = true;
                    operation.allowSceneActivation = true;
                    _additiveSceneActivationRequested[index] = true;
                    return;
                }

                if (_additiveSceneLoaded[index])
                    UnloadAdditiveScene(index);
            }
        }

        private void UnloadAdditiveScene(int index)
        {
            if (chunkDefinitions == null ||
                _additiveSceneLoaded == null ||
                _additiveSceneActivationRequested == null ||
                _additiveSceneUnloadWhenLoaded == null ||
                _additiveSceneOperations == null ||
                (uint)index >= (uint)chunkDefinitions.Length ||
                (uint)index >= (uint)_additiveSceneLoaded.Length ||
                (uint)index >= (uint)_additiveSceneActivationRequested.Length ||
                (uint)index >= (uint)_additiveSceneUnloadWhenLoaded.Length ||
                (uint)index >= (uint)_additiveSceneOperations.Length)
            {
                return;
            }

            string sceneName = chunkDefinitions[index].additiveSceneName;
            if (!string.IsNullOrEmpty(sceneName))
                SceneManager.UnloadSceneAsync(sceneName);

            _additiveSceneLoaded[index] = false;
            _additiveSceneActivationRequested[index] = false;
            _additiveSceneUnloadWhenLoaded[index] = false;
            _additiveSceneOperations[index] = null;
        }

        private void RequestAddressablesCacheClear(int index)
        {
#if UNITY_ADDRESSABLES_EXIST
            if (chunkDefinitions == null ||
                _hasAddressableCacheClearHandle == null ||
                _addressableCacheClearHandles == null ||
                (uint)index >= (uint)chunkDefinitions.Length ||
                (uint)index >= (uint)_hasAddressableCacheClearHandle.Length ||
                (uint)index >= (uint)_addressableCacheClearHandles.Length ||
                _hasAddressableCacheClearHandle[index])
            {
                return;
            }

            string address = chunkDefinitions[index].addressableAddress;
            if (string.IsNullOrEmpty(address))
                return;

            _addressableCacheClearHandles[index] = Addressables.ClearDependencyCacheAsync(address, false);
            _hasAddressableCacheClearHandle[index] = true;
            _pendingAddressableCacheClearCount++;
#endif
        }

        private void PollAddressableCacheClears()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_hasAddressableCacheClearHandle == null ||
                _addressableCacheClearHandles == null ||
                _pendingAddressableCacheClearCount <= 0)
            {
                return;
            }

            int count = math.min(_hasAddressableCacheClearHandle.Length, _addressableCacheClearHandles.Length);
            for (int i = 0; i < count; i++)
            {
                if (!_hasAddressableCacheClearHandle[i])
                    continue;

                AsyncOperationHandle<bool> handle = _addressableCacheClearHandles[i];
                if (!handle.IsValid())
                {
                    _addressableCacheClearHandles[i] = default;
                    _hasAddressableCacheClearHandle[i] = false;
                    _pendingAddressableCacheClearCount = math.max(0, _pendingAddressableCacheClearCount - 1);
                    continue;
                }

                if (!handle.IsDone)
                    continue;

                Addressables.Release(handle);
                _addressableCacheClearHandles[i] = default;
                _hasAddressableCacheClearHandle[i] = false;
                _pendingAddressableCacheClearCount = math.max(0, _pendingAddressableCacheClearCount - 1);
            }
#endif
        }

        private void ReleaseAllChunks()
        {
            if (chunkDefinitions == null)
                return;

            for (int i = 0; i < chunkDefinitions.Length; i++)
            {
                DespawnChunkInstances(i);
                ReleaseChunkHandles(i);
                ResetChunkRuntimeStateAfterRelease(i);
            }

            DrainRuntimeQueuesAfterReleaseAll();
            ReleasePendingAddressablesCacheClearHandles();
            _forceResidencyEvaluation = true;
            _stateDiagnosticsDirty = true;
            WriteTelemetrySample(0L, TelemetryReleaseAllResetFlag);
        }

        private void ResetChunkRuntimeStateAfterRelease(int index)
        {
            if (_loadRequestQueuedByChunk != null && (uint)index < (uint)_loadRequestQueuedByChunk.Length)
                _loadRequestQueuedByChunk[index] = false;
            if (_evictRequestQueuedByChunk != null && (uint)index < (uint)_evictRequestQueuedByChunk.Length)
                _evictRequestQueuedByChunk[index] = false;

            if (_chunkIdsByDefinitionIndex == null ||
                (uint)index >= (uint)_chunkIdsByDefinitionIndex.Length ||
                !_chunkStates.IsCreated ||
                !_chunkIndexById.IsCreated)
            {
                return;
            }

            long chunkId = _chunkIdsByDefinitionIndex[index];
            if (chunkId == 0L || !_chunkIndexById.ContainsKey(chunkId))
                return;

            ChunkState resetState = chunkDefinitions[index].pinned ? ChunkState.Pinned : ChunkState.Unloaded;
            SetChunkState(chunkId, resetState);
        }

        private void DrainRuntimeQueuesAfterReleaseAll()
        {
            if (_loadRequests.IsCreated)
            {
                while (_loadRequests.TryDequeue(out _))
                {
                }
            }

            if (_chunksToLoad.IsCreated)
                _chunksToLoad.Clear();
            if (_chunksToUnload.IsCreated)
                _chunksToUnload.Clear();
            if (_chunkLoadSortRecords.IsCreated)
                _chunkLoadSortRecords.Clear();

            _pendingLoadRequestCount = 0;
            _debugPendingLoadRequests = 0;
            _deferredEvictCount = 0;
        }

        private void ReleasePendingAddressablesCacheClearHandles()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_hasAddressableCacheClearHandle == null || _addressableCacheClearHandles == null)
                return;

            int count = math.min(_hasAddressableCacheClearHandle.Length, _addressableCacheClearHandles.Length);
            for (int i = 0; i < count; i++)
            {
                if (!_hasAddressableCacheClearHandle[i])
                    continue;

                AsyncOperationHandle<bool> handle = _addressableCacheClearHandles[i];
                if (handle.IsValid())
                    Addressables.Release(handle);

                _addressableCacheClearHandles[i] = default;
                _hasAddressableCacheClearHandle[i] = false;
            }

            _pendingAddressableCacheClearCount = 0;
#endif
        }

        private void DrainAupShiftSignals()
        {
            if (!reactToAupShiftSignals)
                return;

            bool sawShift = false;
            while (GlobalSignals.TryDequeueAupShift(out AupShiftSignal signal))
            {
                _debugLastAupShiftFrameId = signal.ShiftFrameId;
                sawShift = true;
            }

            if (sawShift)
            {
                _forceResidencyEvaluation = true;
                WriteTelemetrySample(0L, TelemetryShiftFlag);
            }
        }

        private void UpdateChunkFade(float deltaTime)
        {
            if (!_fadeActive)
                return;

            _fadeTimer += math.max(0f, deltaTime);
            float fade01 = math.saturate(_fadeTimer * ChunkFadeSecondsRcp);
            Shader.SetGlobalFloat(_chunkFadeMaskId, fade01);
            if (fade01 >= 1f)
                _fadeActive = false;
        }

        private void StartFade()
        {
            _fadeTimer = 0f;
            _fadeActive = true;
            Shader.SetGlobalFloat(_chunkFadeMaskId, 0f);
        }

        private void ApplyAsyncUploadBudgetForTier(ChunkStreamingScalabilityTier tier)
        {
            if (!applyAsyncUploadBudget || _activeTier == tier)
                return;

            _activeTier = tier;
            switch (tier)
            {
                case ChunkStreamingScalabilityTier.Low:
                    QualitySettings.asyncUploadBufferSize = 64;
                    QualitySettings.asyncUploadTimeSlice = 1;
                    break;
                case ChunkStreamingScalabilityTier.Middle:
                    QualitySettings.asyncUploadBufferSize = 128;
                    QualitySettings.asyncUploadTimeSlice = 2;
                    break;
                default:
                    QualitySettings.asyncUploadBufferSize = 256;
                    QualitySettings.asyncUploadTimeSlice = 4;
                    break;
            }

            QualitySettings.asyncUploadPersistentBuffer = true;
        }

        private static ChunkStreamingScalabilityTier ResolveScalabilityTier()
        {
            int vram = SystemInfo.graphicsMemorySize;
            int ram = SystemInfo.systemMemorySize;
            if (vram <= 2048 || ram <= 8192)
                return ChunkStreamingScalabilityTier.Low;

            if (vram <= 4096 || ram <= 12288)
                return ChunkStreamingScalabilityTier.Middle;

            if (vram <= 8192 || ram <= 16384)
                return ChunkStreamingScalabilityTier.High;

            return ChunkStreamingScalabilityTier.Ultra;
        }

        private bool TryResolvePlayerMotion(out AbsoluteUniversePosition playerAup, out float3 velocity)
        {
            velocity = default;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.IsBound)
            {
                velocity = runtimeContext.MovementState.Velocity;
                if (!IsFinite(velocity))
                    velocity = default;

                playerAup = runtimeContext.MovementState.PredictedAup;
                if (MathGuard.IsFinite(in playerAup))
                    return true;

                if (runtimeContext.PlayerMovement != null)
                {
                    playerAup = runtimeContext.PlayerMovement.CurrentAup;
                    if (MathGuard.IsFinite(in playerAup))
                        return true;
                }

                if (runtimeContext.PlayerTransform != null)
                {
                    playerAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeContext.PlayerTransform.position);
                    return MathGuard.IsFinite(in playerAup);
                }
            }

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
            return MathGuard.IsFinite(in playerAup);
        }

        private bool PredictiveStreamingPausedNow =>
            _externalPredictiveSuspended ||
            _transportPredictivePauseActive ||
            (suspendPredictiveStreamingInHabitat && (_habitatPredictivePauseActive || _habitatTransitionPauseFrames > 0));

        private bool ResolvePredictiveVramAbortState()
        {
            if (SystemInfo.graphicsMemorySize > 2048)
                return false;

            long usedBytes = VRAMBudgetTracker.EstimatedVRAMBytes;
            var monitor = GlobalRegistry.VRAMMonitor;
            if (monitor != null && monitor.TotalVRAMBytes > usedBytes)
                usedBytes = monitor.TotalVRAMBytes;

            return _predictiveVramAborted
                ? usedBytes >= PredictiveVramResumeBytes
                : usedBytes >= PredictiveVramAbortBytes;
        }

        private static float ResolvePredictionDistanceMeters(float3 velocity, ChunkStreamingScalabilityTier tier)
        {
            float speedSq = math.lengthsq(velocity);
            if (speedSq <= 0.0001f)
                return 0f;

            float speed = speedSq * math.rsqrt(speedSq);
            float maxDistance = tier == ChunkStreamingScalabilityTier.Low ? 50f :
                                tier == ChunkStreamingScalabilityTier.Middle ? 100f : 200f;
            return math.min(maxDistance, speed * PredictiveLookaheadSeconds);
        }

        private float ResolveTailUnloadRadiusMeters(float predictionDistanceMeters)
        {
            if (predictionDistanceMeters <= 0f)
                return unloadRadiusMeters;

            float hysteresisFloor = loadRadiusMeters * 1.05f;
            float shrink = math.min(unloadRadiusMeters - hysteresisFloor, predictionDistanceMeters * 0.6f);
            return math.max(hysteresisFloor, unloadRadiusMeters - math.max(0f, shrink));
        }

        private static AbsoluteUniversePosition BuildProjectedAup(in AbsoluteUniversePosition playerAup, float3 velocity, float predictionDistanceMeters)
        {
            float speedSq = math.lengthsq(velocity);
            if (predictionDistanceMeters <= 0f || speedSq <= 0.0001f)
                return playerAup;

            float invSpeed = math.rsqrt(speedSq);
            double3 playerAbs = ToAbsoluteDouble3(in playerAup);
            double3 direction = new double3(velocity.x * invSpeed, velocity.y * invSpeed, velocity.z * invSpeed);
            return AbsoluteUniversePosition.FromAbsolutePosition(playerAbs + (direction * predictionDistanceMeters));
        }

        private byte ResolveLoadFlagsForChunk(long chunkId)
        {
            if (_lastPredictionDistanceMeters <= 0f || !_chunkIndexById.TryGetValue(chunkId, out int index))
                return 0;

            AbsoluteUniversePositionBlit center = _chunkCenters[index];
            AbsoluteUniversePositionBlit playerAup = _lastPlayerAup;
            double distSq = DistanceSq(in center, in playerAup);
            double loadRadiusSq = (double)loadRadiusMeters * loadRadiusMeters;
            return distSq > loadRadiusSq ? LoadRequestFlagPredictive : (byte)0;
        }

        private float ResolveProjectedDistanceSq(long chunkId)
        {
            if (!_chunkIndexById.TryGetValue(chunkId, out int index))
                return float.MaxValue;

            AbsoluteUniversePositionBlit center = _chunkCenters[index];
            AbsoluteUniversePositionBlit projectedAup = _lastProjectedAup;
            double distSq = DistanceSq(in center, in projectedAup);
            return (float)math.min(distSq, float.MaxValue);
        }

        private bool ShouldClearAddressableCacheOnEvict(long chunkId)
        {
            if (_lastPredictionDistanceMeters <= 0f || !_chunkIndexById.TryGetValue(chunkId, out int index))
                return false;

            float speedSq = math.lengthsq(_lastPlayerVelocity);
            if (speedSq <= 0.0001f)
                return false;

            float invSpeed = math.rsqrt(speedSq);
            double3 direction = new double3(_lastPlayerVelocity.x * invSpeed, _lastPlayerVelocity.y * invSpeed, _lastPlayerVelocity.z * invSpeed);
            AbsoluteUniversePositionBlit center = _chunkCenters[index];
            AbsoluteUniversePositionBlit playerAup = _lastPlayerAup;
            double3 delta = ToAbsoluteDouble3(in center) - ToAbsoluteDouble3(in playerAup);
            double behind = math.dot(delta, direction);
            if (behind >= -loadRadiusMeters)
                return false;

            double distSq = math.lengthsq(delta);
            double loadRadiusSq = (double)loadRadiusMeters * loadRadiusMeters;
            return distSq > loadRadiusSq;
        }

        private void UpdateStreamerStressMetric()
        {
            RefreshStateDiagnosticsIfDirty();
            float queuePressure = math.saturate(_pendingLoadRequestCount * _loadQueueCapacityRcp);
            float residentPressure = math.saturate(_debugResidentChunks * _maxChunkCountRcp);
            float speedPressure = math.saturate(math.lengthsq(_lastPlayerVelocity) * StreamerStressSpeedSqRcp);
            float suspendPressure = (_predictiveVramAborted || PredictiveStreamingPausedNow) ? 1f : 0f;
            _debugStreamerStress01 = math.saturate((queuePressure * 0.45f) + (residentPressure * 0.2f) + (speedPressure * 0.2f) + (suspendPressure * 0.15f));
            _debugPredictiveSuspended = _predictiveVramAborted || PredictiveStreamingPausedNow;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.IsBound)
            {
                playerAup = runtimeContext.MovementState.PredictedAup;
                if (MathGuard.IsFinite(in playerAup))
                    return true;

                if (runtimeContext.PlayerMovement != null)
                {
                    playerAup = runtimeContext.PlayerMovement.CurrentAup;
                    return MathGuard.IsFinite(in playerAup);
                }

                if (runtimeContext.PlayerTransform != null)
                {
                    playerAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeContext.PlayerTransform.position);
                    return MathGuard.IsFinite(in playerAup);
                }
            }

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
            return MathGuard.IsFinite(in playerAup);
        }

        private void ClearLoadingFlag(long chunkId)
        {
            if (!_chunkStates.TryGetValue(chunkId, out ChunkState state))
                return;

            state &= unchecked((ChunkState)~(byte)ChunkState.Loading);
            SetChunkState(chunkId, state);
        }

        private void SetChunkState(long chunkId, ChunkState state)
        {
            if (_residencyJobScheduled)
            {
                _forceResidencyEvaluation = true;
                return;
            }

            if (_chunkStates.ContainsKey(chunkId))
                _chunkStates[chunkId] = state;
            else
                _chunkStates.TryAdd(chunkId, state);

            _stateDiagnosticsDirty = true;
        }

        private void RefreshStateDiagnosticsIfDirty()
        {
            if (!_stateDiagnosticsDirty || _residencyJobScheduled)
                return;

            UpdateStateDiagnostics();
        }

        private void UpdateStateDiagnostics()
        {
            if (!_chunkStates.IsCreated)
            {
                _stateDiagnosticsDirty = false;
                return;
            }

            int resident = 0;
            int loading = 0;
            int evicting = 0;
            uint stateHash = ChunkStateHashSeed;
            for (int i = 0; i < _chunkCount; i++)
            {
                long chunkId = _chunkIds[i];
                if (!_chunkStates.TryGetValue(chunkId, out ChunkState state))
                    continue;

                stateHash = MixHash(stateHash, unchecked((uint)chunkId));
                stateHash = MixHash(stateHash, (uint)(byte)state);
                if (HasFlag(state, ChunkState.Resident))
                    resident++;
                if (HasFlag(state, ChunkState.Loading))
                    loading++;
                if (HasFlag(state, ChunkState.Evicting))
                    evicting++;
            }

            _debugResidentChunks = resident;
            _debugLoadingChunks = loading;
            _debugEvictingChunks = evicting;
            _debugStateHash = stateHash;
            _stateDiagnosticsDirty = false;
        }

        private void WriteTelemetrySample(long focusChunkId, uint flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            RefreshStateDiagnosticsIfDirty();
            AbsoluteUniversePosition playerAup = default;
            TryResolvePlayerAup(out playerAup);
            int resident = _debugResidentChunks;
            int loading = _debugLoadingChunks;
            int evicting = _debugEvictingChunks;
            uint stateHash = _debugStateHash;

            _telemetryRing[_telemetryCursor] = new ChunkResidencyTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                Flags = flags,
                FocusChunkId = focusChunkId,
                PlayerGridX = playerAup.GridX,
                PlayerGridY = playerAup.GridY,
                PlayerGridZ = playerAup.GridZ,
                PlayerLocal = new float3(playerAup.LocalX, playerAup.LocalY, playerAup.LocalZ),
                PendingLoads = (ushort)math.min(ushort.MaxValue, _pendingLoadRequestCount),
                ResidentCount = (ushort)math.min(ushort.MaxValue, resident),
                LoadingCount = (ushort)math.min(ushort.MaxValue, loading),
                EvictingCount = (ushort)math.min(ushort.MaxValue, evicting),
                StateHash = stateHash
            };
            _telemetryCursor++;
            if (_telemetryCursor >= TelemetryCapacity)
                _telemetryCursor = 0;
        }

        private void DumpTelemetry(uint reasonFlags)
        {
            WriteTelemetrySample(0L, reasonFlags);
            if (!_telemetryRing.IsCreated)
                return;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    ChunkResidencyTelemetryEntry entry = _telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.FocusChunkId);
                    writer.Write(entry.PlayerGridX);
                    writer.Write(entry.PlayerGridY);
                    writer.Write(entry.PlayerGridZ);
                    writer.Write(entry.PlayerLocal.x);
                    writer.Write(entry.PlayerLocal.y);
                    writer.Write(entry.PlayerLocal.z);
                    writer.Write(entry.PendingLoads);
                    writer.Write(entry.ResidentCount);
                    writer.Write(entry.LoadingCount);
                    writer.Write(entry.EvictingCount);
                    writer.Write(entry.StateHash);
                }
            }
        }

        private void DisposeNativeState()
        {
            if (_chunkIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_chunkIds);
                _chunkIds.Dispose();
            }

            if (_chunkCenters.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_chunkCenters);
                _chunkCenters.Dispose();
            }

            if (_chunkStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(WorldChunkResidencyManager), nameof(_chunkStates));
                _chunkStates.Dispose();
            }

            if (_chunkIndexById.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(WorldChunkResidencyManager), nameof(_chunkIndexById));
                _chunkIndexById.Dispose();
            }

            if (_loadRequests.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(WorldChunkResidencyManager), nameof(_loadRequests));
                _loadRequests.Dispose();
            }

            if (_chunksToLoad.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(WorldChunkResidencyManager), nameof(_chunksToLoad));
                _chunksToLoad.Dispose();
            }

            if (_chunksToUnload.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(WorldChunkResidencyManager), nameof(_chunksToUnload));
                _chunksToUnload.Dispose();
            }

            if (_chunkLoadSortRecords.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(WorldChunkResidencyManager), nameof(_chunkLoadSortRecords));
                _chunkLoadSortRecords.Dispose();
            }

            if (_telemetryRing.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_telemetryRing);
                _telemetryRing.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceSq(in AbsoluteUniversePosition lhs, in AbsoluteUniversePosition rhs)
        {
            return math.distancesq(ToAbsoluteDouble3(in lhs), ToAbsoluteDouble3(in rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceSq(in AbsoluteUniversePositionBlit lhs, in AbsoluteUniversePositionBlit rhs)
        {
            return math.distancesq(ToAbsoluteDouble3(in lhs), ToAbsoluteDouble3(in rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.LocalX,
                (position.GridY * CellSize) + position.LocalY,
                (position.GridZ * CellSize) + position.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit position)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * CellSize) + position.Local.x,
                (position.GridY * CellSize) + position.Local.y,
                (position.GridZ * CellSize) + position.Local.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteX(in AbsoluteUniversePosition position)
        {
            return (position.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + position.LocalX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteY(in AbsoluteUniversePosition position)
        {
            return (position.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) + position.LocalY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteZ(in AbsoluteUniversePosition position)
        {
            return (position.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + position.LocalZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteX(in AbsoluteUniversePositionBlit position)
        {
            return (position.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + position.Local.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteY(in AbsoluteUniversePositionBlit position)
        {
            return (position.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) + position.Local.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ToAbsoluteZ(in AbsoluteUniversePositionBlit position)
        {
            return (position.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + position.Local.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(ChunkState state, ChunkState flag)
        {
            return ((byte)state & (byte)flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFlag(byte state, byte flag)
        {
            return (state & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixHash(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }
    }
}
