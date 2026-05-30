using System;
using System.IO;
using Hecton8.Cartography;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.PDA
{
    public interface IPdaCartographyReadModel
    {
        /// <summary>Returns true while the PDA cartography owner can serve map upload requests.</summary>
        bool IsPdaCartographyReadModelActive { get; }

        /// <summary>Resolves discovered-sector metadata without uploading GPU data.</summary>
        bool TryPrepareDiscoveredSectorsInfo(
            out int axisLength,
            out int originOffset,
            out int cellSizeMeters,
            out uint revision,
            out int wordCount);

        /// <summary>Uploads the current discovered-sector bitfield into a caller-owned graphics buffer.</summary>
        bool TryUploadDiscoveredSectors(
            GraphicsBuffer destination,
            out int axisLength,
            out int originOffset,
            out int cellSizeMeters,
            out uint revision,
            out int wordCount);

        /// <summary>Uploads the latest prepared packed cartography buffer into a caller-owned graphics buffer.</summary>
        bool TryUploadPreparedCartography(
            GraphicsBuffer destination,
            float globalQualityWeight,
            out int framesBetweenUploads,
            out uint revision);

        /// <summary>Reads the current cartography tuning row.</summary>
        bool TryGetCartographyTuning(out CartographyTuningDTO tuning);
    }

    /// <summary>
    /// Tracks player movement across a dense 16m Morton-ordered exploration mask for PDA fog-of-war queries.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/Player Exploration Tracker")]
    public sealed class PlayerExplorationTracker : MonoBehaviour, ITickable, ISlowTickable, ISaveable, IMapMagicBiomeEventListener, ISonarPingEventListener, IPlayerExplorationChunkReadModel, IPdaCartographyReadModel, IGlobalRegistryHotSwapListener
    {
        private const int ExplorationChunkSizeMeters = ExplorationMapDTO.DenseChunkSizeMeters;
        private const int MaskAxisBits = ExplorationMapDTO.MortonMaskAxisBits;
        private const int MaskAxisLength = ExplorationMapDTO.MortonMaskAxisLength;
        private const int MaskOriginOffset = ExplorationMapDTO.MortonMaskOriginOffset;
        private const int MaskBitCount = ExplorationMapDTO.MortonMaskBitCount;
        private const int TotalChunkCapacity = MaskBitCount;
        private const int MaskWordCount = ExplorationMapDTO.MortonMaskWordCount;
        private const int MaskByteCount = ExplorationMapDTO.MortonMaskByteCount;
        private const int LocalMask = MaskAxisLength - 1;
        private const int AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;
        private const uint CartographyPreSimulationSystemHash = 0x53313350u;
        private const uint CartographySimulationSystemHash = 0x53313349u;
        private const uint CartographyPostSimulationSystemHash = 0x5331334Fu;
        private const uint CartographyVisualSyncSystemHash = 0x53313356u;

        [Header("References")]
        [Tooltip("Optional explicit player transform. When empty, the tracker resolves the current registry player.")]
        [SerializeField] private Transform playerTransform;

        [Header("Exploration Grid")]
        [Tooltip("Minimum movement distance before the tracker re-evaluates chunk membership.")]
        [SerializeField, Min(0.25f)] private float movementSampleDistance = 4f;
        [Tooltip("When enabled, biome changes from MapMagic automatically feed the discovery registry.")]
        [SerializeField] private bool forwardBiomeDiscovery = true;

        // COLD ALLOC: long[32768] — owner-local dense Morton mask mirror for zero-pin read APIs and save DTO staging — owner: PlayerExplorationTracker
        private readonly long[] _saveMaskWordBuffer = new long[MaskWordCount];
        // COLD ALLOC: PDAMarkerSnapshot[64] — PDA marker POI staging for cartography macro reveal — owner: PlayerExplorationTracker
        private readonly PDAMarkerSnapshot[] _poiMarkerScratch = new PDAMarkerSnapshot[CartographyGridConstants.MaxPoiRevealPerSlowTick];
        private const ulong CartographyPinDiscoveryWords = 1UL << 0;
        private const ulong CartographyPinSectorTable = 1UL << 1;
        private const ulong CartographyPinUploadPackedR8 = 1UL << 2;
        private const ulong CartographyPinTelemetryRing = 1UL << 3;
        private const ulong CartographyPinTelemetryCursor = 1UL << 4;
        private const ulong CartographyPinTuning = 1UL << 5;
        private const ulong CartographyPinScannerProfiles = 1UL << 6;
        private const ulong CartographyPinCsvScratch = 1UL << 7;
        private const ulong CartographyPinMockPings = 1UL << 8;
        private const ulong CartographyPinPendingPings = 1UL << 9;
        private const ulong CartographyPinPendingSignalCounts = 1UL << 10;
        private const ulong CartographyPinCounters = 1UL << 11;
        private const ulong CartographyPinActiveSectorHashes = 1UL << 12;
        private const ulong CartographyPinDebugVoxels = 1UL << 13;
        private const ulong CartographyPinRleRuns = 1UL << 14;
        private const ulong CartographyPinSurfaceMaskWords = 1UL << 15;
        private const ulong CartographyPinRollbackSnapshotWords = 1UL << 16;
        private const ulong CartographyPinState = 1UL << 17;
        private const ulong CartographyPinLegacyExplorationWords = 1UL << 18;
        private const ulong CartographyPinLegacyExploredBitIndices = 1UL << 19;
        private const ulong CartographyPinLegacyExploredBitIndexCount = 1UL << 20;
        private const ulong CartographyPinSimulation =
            CartographyPinDiscoveryWords |
            CartographyPinSurfaceMaskWords |
            CartographyPinPendingPings |
            CartographyPinCounters |
            CartographyPinTuning |
            CartographyPinState;
        private const ulong CartographyPinUpload =
            CartographyPinDiscoveryWords |
            CartographyPinUploadPackedR8 |
            CartographyPinRollbackSnapshotWords;
        private const ulong CartographyPinTelemetry =
            CartographyPinDiscoveryWords |
            CartographyPinTelemetryRing |
            CartographyPinTelemetryCursor |
            CartographyPinCounters |
            CartographyPinTuning |
            CartographyPinState;
        private const ulong CartographyPinCoreInitialize =
            CartographyPinDiscoveryWords |
            CartographyPinSectorTable |
            CartographyPinUploadPackedR8 |
            CartographyPinTelemetryRing |
            CartographyPinTelemetryCursor |
            CartographyPinTuning |
            CartographyPinScannerProfiles |
            CartographyPinMockPings |
            CartographyPinPendingPings |
            CartographyPinPendingSignalCounts |
            CartographyPinCounters |
            CartographyPinActiveSectorHashes |
            CartographyPinDebugVoxels |
            CartographyPinRleRuns |
            CartographyPinSurfaceMaskWords |
            CartographyPinRollbackSnapshotWords |
            CartographyPinState;
        private const ulong CartographyPinLegacy =
            CartographyPinLegacyExplorationWords |
            CartographyPinLegacyExploredBitIndices |
            CartographyPinLegacyExploredBitIndexCount;
        private IDataVault _cartographyVault;
        private CartographyVaultHandles _cartographyHandles;
        private bool _registeredToTick;
        private bool _registeredToSlowTick;
        private bool _registeredToSave;
        private bool _registeredToAcousticEvents;
        private bool _registeredToSonarEvents;
        private int _lastPhysicsEventSnapshotGeneration;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private bool _explorationMaskInitialized;
        private AbsoluteUniversePosition _lastSampledAup;
        private HectonPlayerMovement _playerMovement;
        private bool _hasLastSampledAup;
        private int _lastBitIndex = -1;
        private int _lastCartographyBitIndex = -1;
        private uint _cartographyRevision;
        private uint _cartographyFrameIndex;
        private bool _cartographyVaultReady;
        private bool _cartographyDumpedThisSession;
        private bool _cartographyBlackBoxDumpStaged;
        private uint _cartographyDeferredDumpFlags;
        private CartographyDispatcherPhaseSystem _cartographyPreSimulationPhase;
        private CartographyDispatcherPhaseSystem _cartographySimulationPhase;
        private CartographyDispatcherPhaseSystem _cartographyPostSimulationPhase;
        private CartographyDispatcherPhaseSystem _cartographyVisualSyncPhase;
        private bool _cartographyDispatcherRegistered;
        private bool _cartographyDispatcherFrameScheduled;
        private bool _cartographyDispatcherHasPlayerAup;
        private CartographyAup _cartographyDispatcherPlayerAup;
        private int _cartographyDispatcherPendingSignalCount;
        private int _cartographyDispatcherPoiCount;
        private uint _nextCartographySampleFrame;
        private uint _nextPoiRevealFrame;
        private long _cartographyMutationStartTimestamp;
        private JobHandle _cartographySimulationHandle;
        private bool _cartographySimulationPending;
        private JobHandle _cartographyUploadHandle;
        private bool _cartographyUploadPending;
        private bool _cartographyUploadPrepared;
        private bool _cartographyUploadRequested;
        private bool _cartographySimulationBuffersPinned;
        private bool _cartographyUploadBuffersPinned;
        private IDataVault _cartographySimulationPinnedVault;
        private IDataVault _cartographyUploadPinnedVault;
        private ulong _cartographySimulationPinnedMask;
        private ulong _cartographyUploadPinnedMask;
        private uint _cartographyUploadPendingRevision;
        private int _cartographyUploadPendingCadence;
        private uint _cartographyUploadPreparedRevision;
        private int _cartographyUploadPreparedCadence;
        private float _cartographyUploadRequestedQuality = 1f;
        private int _exploredChunkCountSnapshot;
        private CartographyTuningDTO _cartographyTuningSnapshot;
        private CartographyTelemetryEntry _latestCartographyTelemetrySnapshot;
        private bool _hasCartographyTuningSnapshot;
        private bool _hasLatestCartographyTelemetrySnapshot;
        private ISaveService _saveService;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private PDAMarkerRegistry _cachedMarkerRegistry;
        private PersistentWorldRegistry _cachedPersistentWorldRegistry;
        private HectonDiscoveryManager _cachedDiscoveryManager;

        /// <summary>Live registry-owned instance for PDA map systems.</summary>
        private static PlayerExplorationTracker s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntimeInstance = null;
        }

        /// <inheritdoc />
        public bool IsPdaCartographyReadModelActive =>
            isActiveAndEnabled &&
            _cartographyVaultReady &&
            _cartographyHandles.IsCoreCreated();

        /// <summary>Raised when a previously unexplored PDA chunk becomes visible.</summary>
        public event Action<Vector2Int> ChunkExplored;

        /// <summary>Total explored chunk count currently held in memory.</summary>
        public int ExploredChunkCount => math.max(0, _exploredChunkCountSnapshot);

        /// <summary>World-space size represented by one persisted exploration chunk.</summary>
        public float ChunkWorldSize => ExplorationChunkSizeMeters;

        /// <inheritdoc />
        public int SavePriority => 21;

        /// <inheritdoc />
        public int LoadPriority => 21;

        private void Awake()
        {
            PlayerExplorationTracker registered = s_activeRuntimeInstance ?? GlobalRegistry.PlayerExploration;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(this);
                return;
            }

            movementSampleDistance = math.max(0.25f, movementSampleDistance);
            CacheRegistryServicesCold();
            InitializeExplorationMask();
        }

        private void OnEnable()
        {
            TryRegisterService();
            CacheRegistryServicesCold();
            InitializeExplorationMask();
            TryRegisterHotSwapListener();
            TryRegisterWithTickManager();
            TryRegisterWithSlowTickManager();
            TryRegisterCartographyDispatcher();
            TryRegisterWithSaveManager();
            TryRegisterSignalListeners();
            MapMagicBiomeEvents.Register(this);
            RefreshPlayerTransformCache(force: true);
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            InitializeExplorationMask();
            TryRegisterHotSwapListener();
            TryRegisterWithTickManager();
            TryRegisterWithSlowTickManager();
            TryRegisterCartographyDispatcher();
            TryRegisterWithSaveManager();
            TryRegisterSignalListeners();
            RefreshPlayerTransformCache(force: true);
            SampleCurrentChunk(force: true);
        }

        private void OnDisable()
        {
            MapMagicBiomeEvents.Unregister(this);
            UnregisterSignalListeners();
            UnregisterCartographyDispatcher();
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            CompleteCartographySimulationJobForTeardown();
            CompleteCartographyUploadJobForTeardown();
            ReleaseCartographySimulationPins();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            MapMagicBiomeEvents.Unregister(this);
            UnregisterSignalListeners();
            UnregisterCartographyDispatcher();
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            CompleteCartographySimulationJobForTeardown();
            CompleteCartographyUploadJobForTeardown();
            ReleaseCartographySimulationPins();
            TryUnregisterService();
            DisposeExplorationMask();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            DrainPhysicsEventPayloads();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            _cartographyDeferredDumpFlags = 0u;
        }

        private void CartographyPreSimulationTick(in DispatcherTimingDTO timing)
        {
            InitializeExplorationMask();
            _cartographyDispatcherFrameScheduled = false;
            _cartographyDispatcherPendingSignalCount = 0;
            _cartographyDispatcherPoiCount = 0;
            _cartographyDispatcherHasPlayerAup = false;
            _cartographyDispatcherPlayerAup = default;

            const ulong pinMask = CartographyPinMockPings |
                                  CartographyPinPendingPings |
                                  CartographyPinPendingSignalCounts |
                                  CartographyPinCounters |
                                  CartographyPinTuning;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return;
            }

            bool shouldRecordFault = false;
            CartographyAup faultAup = default;
            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers) ||
                    !buffers.Counters.IsCreated ||
                    buffers.Counters.Length == 0)
                {
                    return;
                }

                CartographyTuningDTO tuning = ResolvePinnedCartographyTuning(in buffers);
                uint frame = timing.FrameId;
                if (frame >= _nextCartographySampleFrame &&
                    TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                {
                    CartographyAup playerCartographyAup = ToCartographyAup(in playerAup);
                    if (CartographyGridMath.IsFinite(in playerCartographyAup))
                    {
                        _cartographyDispatcherPlayerAup = playerCartographyAup;
                        _cartographyDispatcherHasPlayerAup = true;
                        _nextCartographySampleFrame = frame + (uint)CartographyGridMath.ResolveTickIntervalFrames(tuning.GlobalQualityWeight);
                    }
                    else
                    {
                        FlagCartographyFailure(buffers, CartographyGridConstants.TelemetryFlagOutOfBoundsAup);
                        faultAup = playerCartographyAup;
                        shouldRecordFault = true;
                    }
                }

                if (frame >= _nextPoiRevealFrame)
                {
                    _cartographyDispatcherPoiCount = AppendPoiRevealSignals(in buffers, in tuning);
                    _nextPoiRevealFrame = frame + (uint)ResolvePoiRevealIntervalFrames(tuning.GlobalQualityWeight);
                }

                _cartographyDispatcherPendingSignalCount = StagePendingMapRevealSignals(buffers);
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }

            if (shouldRecordFault)
            {
                RecordCartographyFaultAndDump(
                    in faultAup,
                    CartographyGridConstants.TelemetryFlagOutOfBoundsAup);
            }
        }

        private JobHandle ScheduleCartographySimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (!_cartographyVaultReady && !EnsureCartographyVault())
                return dependsOn;

            _cartographyDispatcherFrameScheduled = _cartographyDispatcherHasPlayerAup ||
                                                   _cartographyDispatcherPendingSignalCount > 0;
            if (!_cartographyDispatcherFrameScheduled)
                return dependsOn;

            if (!TryPinCartographySimulationBuffers())
            {
                RecordCartographyBlackBox(
                    in _cartographyDispatcherPlayerAup,
                    math.max(0, _cartographyDispatcherPendingSignalCount - _cartographyDispatcherPoiCount),
                    _cartographyDispatcherPoiCount,
                    CartographyGridConstants.TelemetryFlagVaultContention);
                _cartographyDispatcherFrameScheduled = false;
                return dependsOn;
            }

            bool scheduled = false;
            bool shouldRecordFailure = false;
            uint failureTelemetryFlags = CartographyGridConstants.TelemetryFlagVaultContention;
            try
            {
                if (!TryResolvePinnedCartographyBuffers(_cartographySimulationPinnedMask, out CartographyVaultBuffers buffers) ||
                    !buffers.Counters.IsCreated ||
                    buffers.Counters.Length == 0)
                {
                    shouldRecordFailure = true;
                    _cartographyDispatcherFrameScheduled = false;
                    return dependsOn;
                }

                CartographyTuningDTO tuning = ResolvePinnedCartographyTuning(in buffers);
                ApplyCartographyFrameDiscoveryJob job = new ApplyCartographyFrameDiscoveryJob
                {
                    DiscoveredSectors = buffers.DiscoveryWords,
                    SurfaceMaskWords = buffers.SurfaceMaskWords,
                    PendingSignals = buffers.PendingPings,
                    Counters = buffers.Counters,
                    State = buffers.State,
                    PlayerAup = _cartographyDispatcherPlayerAup,
                    PlayerRevealRadiusMeters = tuning.CellSizeMeters,
                    SurfaceThicknessMeters = tuning.SurfaceThicknessMeters,
                    GlobalQualityWeight = tuning.GlobalQualityWeight,
                    HasPlayerAup = _cartographyDispatcherHasPlayerAup ? 1 : 0,
                    PendingSignalCount = _cartographyDispatcherPendingSignalCount,
                    WordOffset = 0
                };
                _cartographyMutationStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                _cartographySimulationHandle = job.Schedule(dependsOn);
                _cartographySimulationPending = true;
                H8Memory.RegisterActiveJob(SystemID.UI, _cartographySimulationHandle);
                scheduled = true;
                _cartographyDispatcherFrameScheduled = false;
                return _cartographySimulationHandle;
            }
            finally
            {
                if (!scheduled)
                {
                    ReleaseCartographySimulationPins();
                    if (shouldRecordFailure)
                    {
                        RecordCartographyBlackBox(
                            in _cartographyDispatcherPlayerAup,
                            math.max(0, _cartographyDispatcherPendingSignalCount - _cartographyDispatcherPoiCount),
                            _cartographyDispatcherPoiCount,
                            failureTelemetryFlags);
                    }
                }
            }
        }

        private void CartographyPostSimulationTick(in DispatcherTimingDTO timing)
        {
            _cartographyDispatcherFrameScheduled = false;
            if (_cartographySimulationPending)
            {
                if (!TryFinalizePendingCartographySimulation(forceComplete: true))
                {
                    _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                    return;
                }
            }

            if (_cartographySimulationBuffersPinned)
                ReleaseCartographySimulationPins();

            TryScheduleRequestedCartographyUpload();
        }

        private void CartographyVisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_cartographyUploadPending)
                TryFinalizePendingCartographyUpload(forceComplete: true);

            FlushStagedCartographyBlackBoxDump();
        }

        private uint FinalizeCartographySimulationResultPinned(CartographyVaultBuffers buffers)
        {
            if (!buffers.Counters.IsCreated || buffers.Counters.Length == 0)
                return CartographyGridConstants.TelemetryFlagVaultContention;

            CartographyCounterDTO counter = buffers.Counters[0];
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _cartographyMutationStartTimestamp;
            long stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency > 0L
                ? System.Diagnostics.Stopwatch.Frequency
                : 1L;
            long elapsedMicroseconds = (elapsedTicks * 1000000L) / stopwatchFrequency;
            if (elapsedMicroseconds < 0L)
                elapsedMicroseconds = 0L;
            if (elapsedMicroseconds > uint.MaxValue)
                elapsedMicroseconds = uint.MaxValue;
            counter.LastMutationMicroseconds = (uint)elapsedMicroseconds;
            if (counter.LastMutationMicroseconds > 500u)
                counter.LastFailureFlags |= CartographyGridConstants.TelemetryFlagMutationBudgetExceeded;
            if (counter.Changed != 0)
            {
                _lastCartographyBitIndex = counter.LastBitIndex == uint.MaxValue ? -1 : (int)counter.LastBitIndex;
                _cartographyRevision++;
            }

            buffers.Counters[0] = counter;
            return counter.LastFailureFlags;
        }

        private int AppendPoiRevealSignals(in CartographyVaultBuffers buffers, in CartographyTuningDTO tuning)
        {
            int appended = 0;
            PDAMarkerRegistry markerRegistry = _cachedMarkerRegistry;
            int markerCount = markerRegistry != null ? markerRegistry.CopyMarkers(_poiMarkerScratch, hudOnly: false) : 0;
            int count = math.min(markerCount, CartographyGridConstants.MaxPoiRevealPerSlowTick);
            for (int i = 0; i < count; i++)
            {
                PDAMarkerSnapshot marker = _poiMarkerScratch[i];
                AbsoluteUniversePosition markerAup = marker.PositionAup;
                CartographyAup markerCartographyAup = ToCartographyAup(in markerAup);
                if (CartographyGridMath.IsFinite(in markerCartographyAup))
                {
                    MapRevealSignal signal = default;
                    signal.Center = markerCartographyAup;
                    signal.RadiusMeters = math.max(CartographyGridConstants.MacroCellSizeMeters, tuning.SonarPingRadiusMeters * 0.25f);
                    signal.Flags = MapRevealSignalFlags.Poi;
                    if (TryAppendMapRevealSignal(buffers, in signal))
                        appended++;
                }

                _poiMarkerScratch[i] = default;
            }

            PersistentWorldRegistry persistentWorldRegistry = _cachedPersistentWorldRegistry;
            if (persistentWorldRegistry != null && appended < CartographyGridConstants.MaxPoiRevealPerSlowTick)
            {
                int persistentDeltaCount = persistentWorldRegistry.SaveSnapshotCount;
                int chunkSizeMeters = math.max(1, persistentWorldRegistry.ChunkSizeMeters);
                for (int i = 0;
                     i < persistentDeltaCount &&
                     appended < CartographyGridConstants.MaxPoiRevealPerSlowTick;
                     i++)
                {
                    if (!persistentWorldRegistry.TryReadSaveSnapshotDelta(i, out PersistentWorldDeltaRecord delta))
                        break;

                    if (!PersistentWorldDeltaRecord.IsValid(in delta) || PersistentWorldDeltaRecord.IsDeleted(in delta))
                        continue;

                    AbsoluteUniversePosition position = delta.UnpackPosition(chunkSizeMeters);
                    CartographyAup persistentCartographyAup = ToCartographyAup(in position);
                    if (!CartographyGridMath.IsFinite(in persistentCartographyAup))
                        continue;

                    MapRevealSignal signal = default;
                    signal.Center = persistentCartographyAup;
                    signal.RadiusMeters = math.max(CartographyGridConstants.MacroCellSizeMeters, tuning.SonarPingRadiusMeters * 0.2f);
                    signal.Flags = MapRevealSignalFlags.Poi;
                    if (TryAppendMapRevealSignal(buffers, in signal))
                        appended++;
                    else
                        break;
                }
            }

            return appended;
        }

        private static int ResolvePoiRevealIntervalFrames(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float curve = quality * quality * (3f - (2f * quality));
            return math.clamp((int)math.round(math.lerp(60f, 6f, curve)), 6, 60);
        }

        /// <summary>
        /// Returns true when the requested PDA chunk has already been explored in the current save.
        /// </summary>
        public bool IsChunkExplored(Vector2Int chunkCoordinates)
        {
            return IsChunkExplored(chunkCoordinates.x, chunkCoordinates.y);
        }

        /// <summary>
        /// Returns true when the requested PDA chunk has already been explored in the current save.
        /// </summary>
        public bool IsChunkExplored(int chunkX, int chunkY)
        {
            if (!TryEncodeBitIndex(chunkX, 0, chunkY, out int bitIndex))
                return false;

            return IsExploredSnapshotBitSet(bitIndex);
        }

        /// <summary>
        /// Converts a world-space position into PDA exploration chunk coordinates.
        /// </summary>
        public bool TryWorldToChunk(Vector3 worldPosition, out Vector2Int chunkCoordinates)
        {
            if (!TryResolveAupFromRuntimePosition(worldPosition, out AbsoluteUniversePosition aup))
            {
                chunkCoordinates = default;
                return false;
            }

            return TryAupToChunk(in aup, out chunkCoordinates);
        }

        /// <summary>
        /// Copies explored chunk coordinates into a caller-owned buffer.
        /// </summary>
        public int CopyExploredChunks(Vector2Int[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0;

            int targetCount = math.min(buffer.Length, math.max(0, _exploredChunkCountSnapshot));
            int written = 0;
            for (int wordIndex = 0; wordIndex < _saveMaskWordBuffer.Length && written < targetCount; wordIndex++)
            {
                ulong word = unchecked((ulong)_saveMaskWordBuffer[wordIndex]);
                if (word == 0UL)
                    continue;

                int baseBitIndex = wordIndex << 6;
                for (int bit = 0; bit < 64 && written < targetCount; bit++)
                {
                    if ((word & (1UL << bit)) == 0UL)
                        continue;

                    DecodeBitIndex(baseBitIndex + bit, out int chunkX, out _, out int chunkZ);
                    buffer[written++] = new Vector2Int(chunkX, chunkZ);
                }
            }

            return written;
        }

        internal int CopyExploredChunkKeys(long[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0;

            int targetCount = math.min(buffer.Length, math.max(0, _exploredChunkCountSnapshot));
            int written = 0;
            for (int wordIndex = 0; wordIndex < _saveMaskWordBuffer.Length && written < targetCount; wordIndex++)
            {
                ulong word = unchecked((ulong)_saveMaskWordBuffer[wordIndex]);
                if (word == 0UL)
                    continue;

                int baseBitIndex = wordIndex << 6;
                for (int bit = 0; bit < 64 && written < targetCount; bit++)
                {
                    if ((word & (1UL << bit)) == 0UL)
                        continue;

                    DecodeBitIndex(baseBitIndex + bit, out int chunkX, out int chunkY, out int chunkZ);
                    buffer[written++] = PDAKeyUtility.TryPackMortonChunkKey(chunkX, chunkY, chunkZ, out long key) ? key : 0L;
                }
            }

            return written;
        }

        int IPlayerExplorationChunkReadModel.CopyExploredChunkKeys(long[] buffer)
        {
            return CopyExploredChunkKeys(buffer);
        }

        /// <summary>
        /// Marks a chunk as explored. Repeated calls are ignored.
        /// </summary>
        public bool MarkChunkExplored(Vector2Int chunkCoordinates)
        {
            return MarkChunkExplored(chunkCoordinates.x, 0, chunkCoordinates.y, raiseEvent: true);
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            InitializeExplorationMask();
            data.explorationMap.EnsureCapacity();
            data.explorationMap.chunkSizeMeters = ExplorationChunkSizeMeters;
            data.explorationMap.mortonMaskAxisBits = MaskAxisBits;
            data.explorationMap.mortonMaskOriginOffset = MaskOriginOffset;
            data.explorationMap.mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;

            NativeArray<ulong> maskWords = default;
            bool hasLegacyMask = TryAcquireCartographyPins(CartographyPinLegacy, out ulong legacyPinnedMask) &&
                                 TryEnsureLegacyExplorationBuffers(
                                      legacyPinnedMask,
                                      out maskWords,
                                     out _,
                                     out _);
            try
            {
                if (hasLegacyMask)
                    CopyLegacyMaskToSnapshot(maskWords);

                int wordCount = MaskWordCount;
                int byteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(
                    ResolveSerializedByteCount(_saveMaskWordBuffer, wordCount));
                int safeByteCount = math.min(byteCount, data.explorationMap.exploredMortonMaskBytes.Length);
                data.explorationMap.exploredMortonByteCount = safeByteCount;
                Array.Clear(data.explorationMap.exploredMortonMaskBytes, 0, data.explorationMap.exploredMortonMaskBytes.Length);
                if (safeByteCount > 0)
                    Buffer.BlockCopy(_saveMaskWordBuffer, 0, data.explorationMap.exploredMortonMaskBytes, 0, safeByteCount);

                for (int i = 0; i < wordCount; i++)
                {
                    data.explorationMap.exploredMortonMaskWords[i] = _saveMaskWordBuffer[i];
                }

                for (int i = wordCount; i < MaskWordCount; i++)
                {
                    data.explorationMap.exploredMortonMaskWords[i] = 0L;
                }

                data.explorationMap.exploredMortonWordCount = wordCount;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, legacyPinnedMask);
            }
            int keyCount = CopyExploredChunkKeys(data.explorationMap.exploredChunkKeys);
            data.explorationMap.exploredChunkCount = keyCount;
            for (int i = keyCount; i < ExplorationMapDTO.MaxExploredChunks; i++)
                data.explorationMap.exploredChunkKeys[i] = 0L;

            PopulateCartographySaveData(data);
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            InitializeExplorationMask();
            ClearExplorationMask();
            _lastBitIndex = -1;

            if (data == null)
                return;

            ExplorationMapDTO dto = data.explorationMap;
            bool loadedMask = TryLoadDenseByteMask(dto) || TryLoadDenseMask(dto);
            if (!loadedMask)
                LoadLegacyChunkKeys(dto);

            LoadCartographyMask(dto);
            SampleCurrentChunk(force: true);
        }

        private void SampleCurrentChunk(bool force)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            SampleCurrentChunk(force, in playerAup);
        }

        private void SampleCurrentChunk(bool force, in AbsoluteUniversePosition playerAup)
        {
            if (!TryAupToChunk(in playerAup, out Vector2Int currentChunk))
                return;

            if (!TryEncodeBitIndex(currentChunk.x, 0, currentChunk.y, out int currentBitIndex))
                return;

            if (!force && currentBitIndex == _lastBitIndex)
                return;

            _lastBitIndex = currentBitIndex;
            MarkChunkExplored(currentChunk);
        }

        private static bool TryAupToChunk(in AbsoluteUniversePosition aup, out Vector2Int chunkCoordinates)
        {
            chunkCoordinates = default;
            double absoluteX = ((double)aup.GridX * AupCellSizeMeters) + aup.LocalX;
            double absoluteZ = ((double)aup.GridZ * AupCellSizeMeters) + aup.LocalZ;
            double chunkX = math.floor(absoluteX / ExplorationChunkSizeMeters);
            double chunkZ = math.floor(absoluteZ / ExplorationChunkSizeMeters);
            if (!math.isfinite(chunkX) ||
                !math.isfinite(chunkZ) ||
                chunkX < -MaskOriginOffset ||
                chunkZ < -MaskOriginOffset ||
                chunkX >= MaskAxisLength - MaskOriginOffset ||
                chunkZ >= MaskAxisLength - MaskOriginOffset)
            {
                return false;
            }

            chunkCoordinates = new Vector2Int(
                (int)chunkX,
                (int)chunkZ);
            return true;
        }

        private bool MarkChunkExplored(int chunkX, int chunkY, int chunkZ, bool raiseEvent)
        {
            InitializeExplorationMask();
            if (!TryEncodeBitIndex(chunkX, chunkY, chunkZ, out int bitIndex))
                return false;

            if ((uint)bitIndex >= (uint)TotalChunkCapacity)
                return false;

            if (!TryAcquireCartographyPins(CartographyPinLegacy, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return false;
            }

            bool marked = false;
            try
            {
                if (!TryEnsureLegacyExplorationBuffers(
                        pinnedMask,
                        out NativeArray<ulong> maskWords,
                        out NativeArray<int> bitIndices,
                        out NativeArray<int> bitIndexCount))
                {
                    return false;
                }

                if (IsLegacyExplorationBitSet(maskWords, bitIndex))
                    return false;

                if (!TryAppendExploredBitIndex(bitIndices, bitIndexCount, bitIndex))
                    return false;

                SetLegacyExplorationBit(maskWords, bitIndex);
                SetExploredSnapshotBit(bitIndex);
                _exploredChunkCountSnapshot = ResolveLegacyExploredBitCount(bitIndices, bitIndexCount);
                _lastBitIndex = bitIndex;
                marked = true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }

            if (!marked)
                return false;

            if (raiseEvent)
            {
                PDAEvents.TryRaiseMapChunkExplored(chunkX, chunkZ);
                ChunkExplored?.Invoke(new Vector2Int(chunkX, chunkZ));
            }
            return true;
        }

        public bool TryPrepareDiscoveredSectorsInfo(
            out int axisLength,
            out int originOffset,
            out int cellSizeMeters,
            out uint revision,
            out int wordCount)
        {
            axisLength = CartographyGridConstants.AxisLength;
            originOffset = CartographyGridConstants.OriginOffset;
            cellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            revision = _cartographyRevision;
            bool ready = IsPdaCartographyReadModelActive;
            wordCount = ready ? CartographyGridConstants.WordCount : 0;
            return ready;
        }

        public bool TryUploadDiscoveredSectors(
            GraphicsBuffer destination,
            out int axisLength,
            out int originOffset,
            out int cellSizeMeters,
            out uint revision,
            out int wordCount)
        {
            axisLength = CartographyGridConstants.AxisLength;
            originOffset = CartographyGridConstants.OriginOffset;
            cellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            revision = _cartographyRevision;
            wordCount = 0;

            if (destination == null ||
                !destination.IsValid())
            {
                return false;
            }

            if (!TryAcquireCartographyPins(CartographyPinDiscoveryWords, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return false;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers) ||
                    !buffers.DiscoveryWords.IsCreated)
                {
                    return false;
                }

                wordCount = math.min(buffers.DiscoveryWords.Length, CartographyGridConstants.WordCount);
                GraphicsBufferUploadUtility.UploadNativeArray(
                    destination,
                    buffers.DiscoveryWords,
                    wordCount);
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        public bool EnqueueMapReveal(in MapRevealSignal signal)
        {
            InitializeExplorationMask();
            const ulong pinMask = CartographyPinMockPings | CartographyPinPendingSignalCounts | CartographyPinCounters;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return false;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers) ||
                    !buffers.MockPings.IsCreated ||
                    !buffers.PendingSignalCounts.IsCreated ||
                    !buffers.Counters.IsCreated ||
                    buffers.Counters.Length == 0)
                {
                    return false;
                }

                return TryAppendMapRevealSignal(buffers, in signal);
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private static bool TryAppendMapRevealSignal(CartographyVaultBuffers buffers, in MapRevealSignal signal)
        {
            if (!buffers.MockPings.IsCreated ||
                !buffers.PendingSignalCounts.IsCreated ||
                buffers.PendingSignalCounts.Length == 0)
            {
                return false;
            }

            int pendingCount = math.clamp(buffers.PendingSignalCounts[0], 0, buffers.MockPings.Length);
            int capacity = math.min(buffers.MockPings.Length, CartographyGridConstants.MaxRevealSignalsPerSlowTick);
            if (pendingCount >= capacity)
                return false;

            MapRevealSignal clampedSignal = signal;
            clampedSignal.RadiusMeters = ClampRevealRadius(signal.RadiusMeters);
            buffers.MockPings[pendingCount] = clampedSignal;
            buffers.PendingSignalCounts[0] = pendingCount + 1;
            return true;
        }

        private static int StagePendingMapRevealSignals(CartographyVaultBuffers buffers)
        {
            if (!buffers.MockPings.IsCreated ||
                !buffers.PendingPings.IsCreated ||
                !buffers.PendingSignalCounts.IsCreated ||
                buffers.PendingSignalCounts.Length == 0)
            {
                return 0;
            }

            int capacity = math.min(
                math.min(buffers.MockPings.Length, buffers.PendingPings.Length),
                CartographyGridConstants.MaxRevealSignalsPerSlowTick);
            int pendingCount = math.clamp(buffers.PendingSignalCounts[0], 0, capacity);
            for (int i = 0; i < pendingCount; i++)
            {
                buffers.PendingPings[i] = buffers.MockPings[i];
                buffers.MockPings[i] = default;
            }

            for (int i = pendingCount; i < capacity; i++)
                buffers.PendingPings[i] = default;

            buffers.PendingSignalCounts[0] = 0;
            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                CartographyCounterDTO counter = buffers.Counters[0];
                counter.PendingSignalCount = (uint)pendingCount;
                buffers.Counters[0] = counter;
            }

            return pendingCount;
        }

        private static int ResolveSerializedByteCount(NativeArray<ulong> maskWords, int wordCount)
        {
            int safeWordCount = math.min(wordCount, maskWords.IsCreated ? maskWords.Length : 0);
            for (int wordIndex = safeWordCount - 1; wordIndex >= 0; wordIndex--)
            {
                ulong word = maskWords[wordIndex];
                if (word == 0UL)
                    continue;

                int usedBytes = sizeof(ulong);
                while (usedBytes > 0 && ((word >> ((usedBytes - 1) * 8)) & 0xFFUL) == 0UL)
                    usedBytes--;

                return (wordIndex * sizeof(ulong)) + usedBytes;
            }

            return 0;
        }

        private static int ResolveSerializedByteCount(long[] maskWords, int wordCount)
        {
            int safeWordCount = maskWords != null ? math.min(wordCount, maskWords.Length) : 0;
            for (int wordIndex = safeWordCount - 1; wordIndex >= 0; wordIndex--)
            {
                ulong word = unchecked((ulong)maskWords[wordIndex]);
                if (word == 0UL)
                    continue;

                int usedBytes = sizeof(ulong);
                while (usedBytes > 0 && ((word >> ((usedBytes - 1) * 8)) & 0xFFUL) == 0UL)
                    usedBytes--;

                return (wordIndex * sizeof(ulong)) + usedBytes;
            }

            return 0;
        }

        private static bool IsLegacyExplorationBitSet(NativeArray<ulong>.ReadOnly maskWords, int bitIndex)
        {
            if (!maskWords.IsCreated || (uint)bitIndex >= (uint)TotalChunkCapacity)
                return false;

            int wordIndex = bitIndex >> 6;
            int bitOffset = bitIndex & 63;
            if ((uint)wordIndex >= (uint)maskWords.Length)
                return false;

            return (maskWords[wordIndex] & (1UL << bitOffset)) != 0UL;
        }

        private static bool IsLegacyExplorationBitSet(NativeArray<ulong> maskWords, int bitIndex)
        {
            return maskWords.IsCreated && IsLegacyExplorationBitSet(maskWords.AsReadOnly(), bitIndex);
        }

        private static void SetLegacyExplorationBit(NativeArray<ulong> maskWords, int bitIndex)
        {
            int wordIndex = bitIndex >> 6;
            int bitOffset = bitIndex & 63;
            if (!maskWords.IsCreated || (uint)wordIndex >= (uint)maskWords.Length)
                return;

            maskWords[wordIndex] |= 1UL << bitOffset;
        }

        private bool IsExploredSnapshotBitSet(int bitIndex)
        {
            int wordIndex = bitIndex >> 6;
            int bitOffset = bitIndex & 63;
            if ((uint)wordIndex >= (uint)_saveMaskWordBuffer.Length)
                return false;

            ulong word = unchecked((ulong)_saveMaskWordBuffer[wordIndex]);
            return (word & (1UL << bitOffset)) != 0UL;
        }

        private void SetExploredSnapshotBit(int bitIndex)
        {
            int wordIndex = bitIndex >> 6;
            int bitOffset = bitIndex & 63;
            if ((uint)wordIndex >= (uint)_saveMaskWordBuffer.Length)
                return;

            ulong word = unchecked((ulong)_saveMaskWordBuffer[wordIndex]);
            _saveMaskWordBuffer[wordIndex] = unchecked((long)(word | (1UL << bitOffset)));
        }

        private void ClearExploredSnapshot()
        {
            Array.Clear(_saveMaskWordBuffer, 0, _saveMaskWordBuffer.Length);
            _exploredChunkCountSnapshot = 0;
        }

        private void CopyLegacyMaskToSnapshot(NativeArray<ulong> maskWords)
        {
            int wordCount = maskWords.IsCreated ? math.min(maskWords.Length, _saveMaskWordBuffer.Length) : 0;
            for (int i = 0; i < wordCount; i++)
                _saveMaskWordBuffer[i] = unchecked((long)maskWords[i]);

            for (int i = wordCount; i < _saveMaskWordBuffer.Length; i++)
                _saveMaskWordBuffer[i] = 0L;
        }

        private static int ResolveLegacyExploredBitCount(NativeArray<int>.ReadOnly bitIndices, NativeArray<int>.ReadOnly bitIndexCount)
        {
            if (!bitIndices.IsCreated ||
                !bitIndexCount.IsCreated ||
                bitIndexCount.Length == 0)
            {
                return 0;
            }

            return math.clamp(bitIndexCount[0], 0, bitIndices.Length);
        }

        private static int ResolveLegacyExploredBitCount(NativeArray<int> bitIndices, NativeArray<int> bitIndexCount)
        {
            if (!bitIndices.IsCreated || !bitIndexCount.IsCreated)
                return 0;

            return ResolveLegacyExploredBitCount(bitIndices.AsReadOnly(), bitIndexCount.AsReadOnly());
        }

        private void PopulateCartographySaveData(SaveData data)
        {
            data.explorationMap.cartographyCellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            data.explorationMap.cartographyMaskAxisBits = CartographyGridConstants.AxisBits;
            data.explorationMap.cartographyMaskOriginOffset = CartographyGridConstants.OriginOffset;

            if (!TryAcquireCartographyPins(CartographyPinDiscoveryWords, out ulong pinnedMask))
            {
                data.explorationMap.discoveredSectorByteCount = 0;
                data.explorationMap.discoveredSectorWordCount = 0;
                Array.Clear(data.explorationMap.discoveredSectorMaskBytes, 0, data.explorationMap.discoveredSectorMaskBytes.Length);
                for (int i = 0; i < CartographyGridConstants.WordCount; i++)
                    data.explorationMap.discoveredSectorMaskWords[i] = 0L;
                return;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                {
                    data.explorationMap.discoveredSectorByteCount = 0;
                    data.explorationMap.discoveredSectorWordCount = 0;
                    Array.Clear(data.explorationMap.discoveredSectorMaskBytes, 0, data.explorationMap.discoveredSectorMaskBytes.Length);
                    for (int i = 0; i < CartographyGridConstants.WordCount; i++)
                        data.explorationMap.discoveredSectorMaskWords[i] = 0L;
                    return;
                }

                NativeArray<ulong> discoveredWords = buffers.DiscoveryWords;
                int wordCount = math.min(discoveredWords.Length, CartographyGridConstants.WordCount);
                int byteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(ResolveSerializedByteCount(discoveredWords, wordCount));
                data.explorationMap.discoveredSectorByteCount = byteCount;
                Array.Clear(data.explorationMap.discoveredSectorMaskBytes, 0, data.explorationMap.discoveredSectorMaskBytes.Length);
                if (byteCount > 0 && discoveredWords.IsCreated)
                {
                    unsafe
                    {
                        fixed (byte* destination = data.explorationMap.discoveredSectorMaskBytes)
                        {
                            void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(discoveredWords);
                            int destinationBytes = data.explorationMap.discoveredSectorMaskBytes.Length;
                            if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, byteCount))
                                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerExplorationTracker));
                        }
                    }
                }

                for (int i = 0; i < wordCount; i++)
                    data.explorationMap.discoveredSectorMaskWords[i] = unchecked((long)discoveredWords[i]);

                for (int i = wordCount; i < CartographyGridConstants.WordCount; i++)
                    data.explorationMap.discoveredSectorMaskWords[i] = 0L;

                data.explorationMap.discoveredSectorWordCount = wordCount;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private void LoadCartographyMask(ExplorationMapDTO dto)
        {
            ClearDiscoveredSectors();
            bool loaded = TryLoadCartographyByteMask(dto) || TryLoadCartographyWordMask(dto);
            if (loaded)
                _cartographyRevision++;
        }

        private bool TryLoadCartographyWordMask(ExplorationMapDTO dto)
        {
            if (dto.discoveredSectorMaskWords == null ||
                dto.discoveredSectorMaskWords.Length == 0 ||
                dto.discoveredSectorWordCount <= 0 ||
                !TryAcquireCartographyPins(CartographyPinDiscoveryWords | CartographyPinCounters, out ulong pinnedMask))
            {
                return false;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                NativeArray<ulong> discoveredWords = buffers.DiscoveryWords;
                int wordCount = math.min(math.min(CartographyGridConstants.WordCount, dto.discoveredSectorMaskWords.Length), dto.discoveredSectorWordCount);
                for (int i = 0; i < wordCount; i++)
                    discoveredWords[i] = unchecked((ulong)dto.discoveredSectorMaskWords[i]);

                for (int i = wordCount; i < CartographyGridConstants.WordCount; i++)
                    discoveredWords[i] = 0UL;

                SetCartographyTotal(buffers.Counters, discoveredWords);
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private bool TryLoadCartographyByteMask(ExplorationMapDTO dto)
        {
            if (!TryAcquireCartographyPins(CartographyPinDiscoveryWords | CartographyPinCounters, out ulong pinnedMask))
                return false;

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                if (dto.discoveredSectorMaskBytes == null ||
                    dto.discoveredSectorMaskBytes.Length == 0 ||
                    dto.discoveredSectorByteCount <= 0)
                {
                    return false;
                }

                int byteCount = math.min(
                    math.min(CartographyGridConstants.WordCount * sizeof(ulong), dto.discoveredSectorMaskBytes.Length),
                    SaveBinaryStorage.AlignExplorationMortonByteCount(dto.discoveredSectorByteCount));
                unsafe
                {
                    NativeArray<ulong> discoveredWords = buffers.DiscoveryWords;
                    void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(discoveredWords);
                    UnsafeUtility.MemClear(destination, CartographyGridConstants.WordCount * sizeof(ulong));
                    fixed (byte* source = dto.discoveredSectorMaskBytes)
                    {
                        int destinationBytes = CartographyGridConstants.WordCount * sizeof(ulong);
                        if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, byteCount))
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerExplorationTracker));
                    }
                }

                SetCartographyTotal(buffers.Counters, buffers.DiscoveryWords);
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private void InitializeExplorationMask()
        {
            if (_explorationMaskInitialized)
                return;

            if (!EnsureCartographyVault())
                return;

            _explorationMaskInitialized = true;
        }

        private void DisposeExplorationMask()
        {
            _cartographyVault = null;
            _cartographyHandles = default;
            _cartographyVaultReady = false;
            _explorationMaskInitialized = false;
            _cartographyUploadPending = false;
            _cartographyUploadPrepared = false;
            _cartographyUploadRequested = false;
            ClearExploredSnapshot();
            _cartographyTuningSnapshot = default;
            _latestCartographyTelemetrySnapshot = default;
            _hasCartographyTuningSnapshot = false;
            _hasLatestCartographyTelemetrySnapshot = false;
        }

        private void ClearExplorationMask()
        {
            ClearExploredSnapshot();
            if (TryAcquireCartographyPins(CartographyPinLegacy, out ulong pinnedMask))
            {
                try
                {
                    if (TryEnsureLegacyExplorationBuffers(
                            pinnedMask,
                            out NativeArray<ulong> legacyWords,
                            out NativeArray<int> bitIndices,
                            out NativeArray<int> bitIndexCount))
                    {
                        ExecuteClearCartographyUlongBuffer(legacyWords);
                        ExecuteClearCartographyIntBuffer(bitIndices);
                        if (bitIndexCount.IsCreated && bitIndexCount.Length > 0)
                        {
                            bitIndexCount[0] = 0;
                            ClearExploredSnapshot();
                        }
                    }
                }
                finally
                {
                    ReleaseCartographyPins(_cartographyVault, pinnedMask);
                }
            }

            ClearDiscoveredSectors();
        }

        private static void ExecuteClearCartographyUlongBuffer(NativeArray<ulong> buffer)
        {
            if (!buffer.IsCreated)
                return;

            ClearCartographyUlongBufferJob job = new ClearCartographyUlongBufferJob { Buffer = buffer };
            for (int i = 0; i < buffer.Length; i++)
                job.Execute(i);
        }

        private static void ExecuteClearCartographyUintBuffer(NativeArray<uint> buffer)
        {
            if (!buffer.IsCreated)
                return;

            ClearCartographyUintBufferJob job = new ClearCartographyUintBufferJob { Buffer = buffer };
            for (int i = 0; i < buffer.Length; i++)
                job.Execute(i);
        }

        private static void ExecuteClearCartographyIntBuffer(NativeArray<int> buffer)
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0;
        }

        private static void ExecuteClearCartographyRevealSignals(NativeArray<MapRevealSignal> buffer)
        {
            if (!buffer.IsCreated)
                return;

            ClearCartographyRevealSignalBufferJob job = new ClearCartographyRevealSignalBufferJob { Buffer = buffer };
            for (int i = 0; i < buffer.Length; i++)
                job.Execute(i);
        }

        private bool TryFinalizePendingCartographyUpload(bool forceComplete)
        {
            if (!TryPinCartographyUploadBuffers())
                return false;

            try
            {
                return TryResolvePinnedCartographyBuffers(_cartographyUploadPinnedMask, out CartographyVaultBuffers buffers) &&
                       TryFinalizeCartographyUploadPinned(buffers, forceComplete, out _, out _, out _);
            }
            finally
            {
                if (_cartographyUploadBuffersPinned && !_cartographyUploadPending)
                    ReleaseCartographyUploadPins();
            }
        }

        private bool TryFinalizePendingCartographySimulation(bool forceComplete)
        {
            if (!_cartographySimulationPending)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _cartographySimulationHandle) &&
                (!forceComplete || !DispatcherJobFence.TryComplete(ref _cartographySimulationHandle, forceComplete: true)))
            {
                return false;
            }

            bool shouldDumpTelemetry = false;
            uint telemetryFlags = CartographyGridConstants.TelemetryFlagVaultContention;
            int explicitSignalCount = math.max(0, _cartographyDispatcherPendingSignalCount - _cartographyDispatcherPoiCount);
            try
            {
                if (!TryResolvePinnedCartographyBuffers(_cartographySimulationPinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                uint failureFlags = FinalizeCartographySimulationResultPinned(buffers);
                shouldDumpTelemetry = (failureFlags & (CartographyGridConstants.TelemetryFlagMutationBudgetExceeded |
                                                       CartographyGridConstants.TelemetryFlagOutOfBoundsAup |
                                                       CartographyGridConstants.TelemetryFlagVaultContention)) != 0u;
                telemetryFlags = _cartographyDispatcherHasPlayerAup ? 1u : 0u;
                telemetryFlags |= 1u << 1;
                return true;
            }
            finally
            {
                _cartographySimulationPending = false;
                _cartographySimulationHandle = default;
                H8Memory.RegisterActiveJob(SystemID.UI, default);
                ReleaseCartographySimulationPins();
                RecordCartographyBlackBox(
                    in _cartographyDispatcherPlayerAup,
                    explicitSignalCount,
                    _cartographyDispatcherPoiCount,
                    telemetryFlags);
                if (shouldDumpTelemetry)
                    DumpCartographyBlackBox();
            }
        }

        private bool TryFinalizeCartographyUploadPinned(
            CartographyVaultBuffers buffers,
            bool forceComplete,
            out NativeArray<uint> packedR8,
            out uint revision,
            out int framesBetweenUploads)
        {
            packedR8 = default;
            revision = _cartographyUploadPendingRevision;
            framesBetweenUploads = math.max(1, _cartographyUploadPendingCadence);
            if (!_cartographyUploadPending)
                return false;

            bool completed = DispatcherJobFence.TryFinalizeCompleted(ref _cartographyUploadHandle);
            if (!completed && forceComplete)
                completed = DispatcherJobFence.TryComplete(ref _cartographyUploadHandle, forceComplete: true);

            if (!completed)
                return false;

            _cartographyUploadPending = false;
            H8Memory.RegisterActiveJob(SystemID.UI, default);
            packedR8 = buffers.UploadPackedR8;
            if (!packedR8.IsCreated)
                return false;

            _cartographyUploadPrepared = true;
            _cartographyUploadPreparedRevision = revision;
            _cartographyUploadPreparedCadence = framesBetweenUploads;
            return true;
        }

        private void CompleteCartographyUploadJobForTeardown()
        {
            CompleteCartographyUploadJobBlocking();
        }

        private void CompleteCartographySimulationJobForTeardown()
        {
            CompleteCartographySimulationJobBlocking();
        }

        private bool CompleteCartographyUploadJobForStructuralMutation()
        {
            return CompleteCartographyUploadJobBlocking();
        }

        private bool CompleteCartographySimulationJobBlocking()
        {
            return TryFinalizePendingCartographySimulation(forceComplete: true);
        }

        private bool CompleteCartographyUploadJobBlocking()
        {
            if (!_cartographyUploadPending)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _cartographyUploadHandle) &&
                !DispatcherJobFence.TryComplete(ref _cartographyUploadHandle, forceComplete: true))
            {
                return false;
            }

            MarkCartographyUploadJobCompleted();
            return true;
        }

        private void MarkCartographySimulationJobCompleted()
        {
            _cartographySimulationPending = false;
            H8Memory.RegisterActiveJob(SystemID.UI, default);
            ReleaseCartographySimulationPins();
        }

        private void MarkCartographyUploadJobCompleted()
        {
            _cartographyUploadPending = false;
            H8Memory.RegisterActiveJob(SystemID.UI, default);
            ReleaseCartographyUploadPins();
        }

        private bool TryPinCartographySimulationBuffers()
        {
            if (_cartographySimulationBuffersPinned)
                return true;

            if (!TryAcquireCartographyPins(
                    CartographyPinSimulation,
                    out _cartographySimulationPinnedMask,
                    out _cartographySimulationPinnedVault))
            {
                return false;
            }

            _cartographySimulationBuffersPinned = true;
            return true;
        }

        private void ReleaseCartographySimulationPins()
        {
            if (!_cartographySimulationBuffersPinned)
                return;

            ReleaseCartographyPins(_cartographySimulationPinnedVault, _cartographySimulationPinnedMask);
            _cartographySimulationPinnedVault = null;
            _cartographySimulationPinnedMask = 0UL;
            _cartographySimulationBuffersPinned = false;
        }

        private bool TryPinCartographyUploadBuffers()
        {
            if (_cartographyUploadBuffersPinned)
                return true;

            if (!TryAcquireCartographyPins(
                    CartographyPinUpload,
                    out _cartographyUploadPinnedMask,
                    out _cartographyUploadPinnedVault))
            {
                return false;
            }

            _cartographyUploadBuffersPinned = true;
            return true;
        }

        private void ReleaseCartographyUploadPins()
        {
            if (!_cartographyUploadBuffersPinned)
                return;

            ReleaseCartographyPins(_cartographyUploadPinnedVault, _cartographyUploadPinnedMask);
            _cartographyUploadPinnedVault = null;
            _cartographyUploadPinnedMask = 0UL;
            _cartographyUploadBuffersPinned = false;
        }

        private bool TryAcquireCartographyPins(ulong requestedMask, out ulong pinnedMask)
        {
            IDataVault pinnedVault;
            return TryAcquireCartographyPins(requestedMask, out pinnedMask, out pinnedVault);
        }

        private bool TryAcquireCartographyPins(ulong requestedMask, out ulong pinnedMask, out IDataVault pinnedVault)
        {
            pinnedMask = 0UL;
            pinnedVault = null;
            if (requestedMask == 0UL)
                return true;

            if (!_cartographyVaultReady && !EnsureCartographyVault())
                return false;

            IDataVault vault = _cartographyVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireCartographyPins(vault, requestedMask, out pinnedMask))
                return false;

            pinnedVault = vault;
            return true;
        }

        private static bool TryAcquireCartographyPins(IDataVault vault, ulong requestedMask, out ulong pinnedMask)
        {
            pinnedMask = 0UL;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            ulong guardMask = CartographyMutationGuardMaskFromPins(requestedMask);
            if (guardMask != 0UL && !vault.TryAcquireMutationGuard(guardMask))
                return false;

            pinnedMask = requestedMask;
            return true;
        }

        private static ulong CartographyMutationGuardMaskFromPins(ulong pinnedMask)
        {
            ulong guardMask = 0UL;
            if ((pinnedMask & CartographyPinDiscoveryWords) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.DiscoveryWords);
            if ((pinnedMask & CartographyPinSectorTable) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.SectorTable);
            if ((pinnedMask & CartographyPinUploadPackedR8) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.UploadPackedR8);
            if ((pinnedMask & CartographyPinTelemetryRing) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.TelemetryRing);
            if ((pinnedMask & CartographyPinTelemetryCursor) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.TelemetryCursor);
            if ((pinnedMask & CartographyPinTuning) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.Tuning);
            if ((pinnedMask & CartographyPinScannerProfiles) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.ScannerProfiles);
            if ((pinnedMask & CartographyPinCsvScratch) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.CsvScratch);
            if ((pinnedMask & CartographyPinMockPings) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.MockPings);
            if ((pinnedMask & CartographyPinPendingPings) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.PendingPings);
            if ((pinnedMask & CartographyPinPendingSignalCounts) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.PendingSignalCounts);
            if ((pinnedMask & CartographyPinCounters) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.Counters);
            if ((pinnedMask & CartographyPinActiveSectorHashes) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.ActiveSectorHashes);
            if ((pinnedMask & CartographyPinDebugVoxels) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.DebugVoxels);
            if ((pinnedMask & CartographyPinRleRuns) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.RleRuns);
            if ((pinnedMask & CartographyPinSurfaceMaskWords) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.SurfaceMaskWords);
            if ((pinnedMask & CartographyPinRollbackSnapshotWords) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.RollbackSnapshotWords);
            if ((pinnedMask & CartographyPinState) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.State);
            if ((pinnedMask & CartographyPinLegacyExplorationWords) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.LegacyExplorationWords);
            if ((pinnedMask & CartographyPinLegacyExploredBitIndices) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.LegacyExploredBitIndices);
            if ((pinnedMask & CartographyPinLegacyExploredBitIndexCount) != 0UL)
                guardMask |= CartographyMutationGuardBit(CartographyVaultBufferIds.LegacyExploredBitIndexCount);
            return guardMask;
        }

        private static ulong CartographyMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static void ReleaseCartographyPins(IDataVault vault, ulong pinnedMask)
        {
            if (vault == null || pinnedMask == 0UL)
                return;

            ulong guardMask = CartographyMutationGuardMaskFromPins(pinnedMask);
            if (guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static void FlagCartographyFailure(CartographyVaultBuffers buffers, uint flags)
        {
            if (!buffers.Counters.IsCreated || buffers.Counters.Length == 0)
                return;

            CartographyCounterDTO counter = buffers.Counters[0];
            counter.LastFailureFlags |= flags;
            buffers.Counters[0] = counter;
        }

        private void ClearDiscoveredSectors()
        {
            // [BLOCKING_SYNC_POINT] Structural mutation of the same Vault buffers must drain the upload writer first.
            if (!CompleteCartographyUploadJobForStructuralMutation())
                return;

            const ulong pinMask = CartographyPinDiscoveryWords |
                                  CartographyPinRollbackSnapshotWords |
                                  CartographyPinUploadPackedR8 |
                                  CartographyPinMockPings |
                                  CartographyPinPendingPings |
                                  CartographyPinPendingSignalCounts |
                                  CartographyPinCounters |
                                  CartographyPinState;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
                return;

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return;

                ExecuteClearCartographyUlongBuffer(buffers.DiscoveryWords);
                ExecuteClearCartographyUlongBuffer(buffers.RollbackSnapshotWords);
                ExecuteClearCartographyUintBuffer(buffers.UploadPackedR8);
                if (buffers.MockPings.IsCreated)
                    ExecuteClearCartographyRevealSignals(buffers.MockPings);

                if (buffers.PendingPings.IsCreated)
                    ExecuteClearCartographyRevealSignals(buffers.PendingPings);

                if (buffers.PendingSignalCounts.IsCreated && buffers.PendingSignalCounts.Length > 0)
                    buffers.PendingSignalCounts[0] = 0;

                if (buffers.Counters.IsCreated)
                {
                    for (int i = 0; i < buffers.Counters.Length; i++)
                    {
                        CartographyCounterDTO counter = buffers.Counters[i];
                        counter.Changed = 0;
                        counter.DiscoveredDelta = 0;
                        counter.LastBitIndex = 0u;
                        counter.TotalDiscoveredVoxels = 0;
                        counter.PendingSignalCount = 0u;
                        counter.LastRleRunCount = 0;
                        counter.LastRleCompressionPermille = 0u;
                        counter.LastMutationMicroseconds = 0u;
                        counter.LastFailureFlags = 0u;
                        buffers.Counters[i] = counter;
                    }
                }

                if (buffers.State.IsCreated && buffers.State.Length > 0)
                    buffers.State[0] = default;

                _lastCartographyBitIndex = -1;
                _cartographyRevision++;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private bool TryLoadDenseMask(ExplorationMapDTO dto)
        {
            if (dto.exploredMortonMaskWords == null ||
                dto.exploredMortonMaskWords.Length == 0 ||
                dto.exploredMortonWordCount <= 0 ||
                !TryAcquireCartographyPins(CartographyPinLegacy, out ulong pinnedMask))
            {
                return false;
            }

            try
            {
                if (!TryEnsureLegacyExplorationBuffers(
                        pinnedMask,
                        out NativeArray<ulong> maskWords,
                        out NativeArray<int> bitIndices,
                        out NativeArray<int> bitIndexCount))
                {
                    return false;
                }

                int wordCount = math.min(math.min(maskWords.Length, dto.exploredMortonMaskWords.Length), dto.exploredMortonWordCount);
                for (int i = 0; i < wordCount; i++)
                    maskWords[i] = unchecked((ulong)dto.exploredMortonMaskWords[i]);

                for (int i = wordCount; i < maskWords.Length; i++)
                    maskWords[i] = 0UL;

                RebuildExploredBitIndexCache(maskWords, bitIndices, bitIndexCount);
                CopyLegacyMaskToSnapshot(maskWords);
                _exploredChunkCountSnapshot = ResolveLegacyExploredBitCount(bitIndices, bitIndexCount);
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private bool TryLoadDenseByteMask(ExplorationMapDTO dto)
        {
            if (dto.exploredMortonMaskBytes == null ||
                dto.exploredMortonMaskBytes.Length == 0 ||
                dto.exploredMortonByteCount <= 0)
            {
                return false;
            }

            if (dto.mortonBuildSalt != 0u && dto.mortonBuildSalt != SaveBinaryStorage.ExplorationMortonBuildSalt32)
                return false;

            if (!TryAcquireCartographyPins(CartographyPinLegacy, out ulong pinnedMask))
                return false;

            try
            {
                if (!TryEnsureLegacyExplorationBuffers(
                        pinnedMask,
                        out NativeArray<ulong> maskWords,
                        out NativeArray<int> bitIndices,
                        out NativeArray<int> bitIndexCount))
                {
                    return false;
                }

                int byteCount = math.min(
                    math.min(MaskByteCount, dto.exploredMortonMaskBytes.Length),
                    SaveBinaryStorage.AlignExplorationMortonByteCount(dto.exploredMortonByteCount));
                unsafe
                {
                    void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(maskWords);
                    UnsafeUtility.MemClear(destination, maskWords.Length * sizeof(ulong));
                    fixed (byte* source = dto.exploredMortonMaskBytes)
                    {
                        int destinationBytes = maskWords.Length * sizeof(ulong);
                        if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, byteCount))
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerExplorationTracker));
                    }
                }

                RebuildExploredBitIndexCache(maskWords, bitIndices, bitIndexCount);
                CopyLegacyMaskToSnapshot(maskWords);
                _exploredChunkCountSnapshot = ResolveLegacyExploredBitCount(bitIndices, bitIndexCount);
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private void LoadLegacyChunkKeys(ExplorationMapDTO dto)
        {
            int count = math.clamp(dto.exploredChunkCount, 0, dto.exploredChunkKeys != null ? dto.exploredChunkKeys.Length : 0);
            for (int i = 0; i < count; i++)
            {
                Vector2Int legacyChunk = PDAKeyUtility.UnpackChunkKey(dto.exploredChunkKeys[i]);
                MarkChunkExplored(legacyChunk.x, 0, legacyChunk.y, raiseEvent: false);
            }
        }

        private void RebuildExploredBitIndexCache(
            NativeArray<ulong> maskWords,
            NativeArray<int> bitIndices,
            NativeArray<int> bitIndexCount)
        {
            ExecuteClearCartographyIntBuffer(bitIndices);
            if (bitIndexCount.IsCreated && bitIndexCount.Length > 0)
                bitIndexCount[0] = 0;

            for (int wordIndex = 0; wordIndex < maskWords.Length; wordIndex++)
            {
                ulong word = maskWords[wordIndex];
                if (word == 0UL)
                    continue;

                int baseBitIndex = wordIndex << 6;
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((word & (1UL << bit)) == 0UL)
                        continue;

                    int bitIndex = baseBitIndex + bit;
                    if (bitIndex < MaskBitCount)
                        TryAppendExploredBitIndex(bitIndices, bitIndexCount, bitIndex);
                }
            }
        }

        private static bool TryAppendExploredBitIndex(
            NativeArray<int> bitIndices,
            NativeArray<int> bitIndexCount,
            int bitIndex)
        {
            if (!bitIndices.IsCreated ||
                !bitIndexCount.IsCreated ||
                bitIndexCount.Length == 0 ||
                (uint)bitIndex >= (uint)TotalChunkCapacity ||
                bitIndexCount[0] >= bitIndices.Length)
            {
                return false;
            }

            int writeIndex = bitIndexCount[0];
            bitIndices[writeIndex] = bitIndex;
            bitIndexCount[0] = writeIndex + 1;
            return true;
        }

        private static bool TryEncodeBitIndex(int chunkX, int chunkY, int chunkZ, out int bitIndex)
        {
            int localX = chunkX + MaskOriginOffset;
            int localY = chunkY + MaskOriginOffset;
            int localZ = chunkZ + MaskOriginOffset;
            if ((uint)localX >= MaskAxisLength || (uint)localY >= MaskAxisLength || (uint)localZ >= MaskAxisLength)
            {
                bitIndex = -1;
                return false;
            }

            bitIndex = EncodeLocalMortonIndex(localX, localY, localZ);
            if ((uint)bitIndex >= (uint)TotalChunkCapacity)
            {
                bitIndex = -1;
                return false;
            }

            return true;
        }

        private static void DecodeBitIndex(int bitIndex, out int chunkX, out int chunkY, out int chunkZ)
        {
            int localX = Compact1By2((uint)bitIndex);
            int localY = Compact1By2((uint)bitIndex >> 1);
            int localZ = Compact1By2((uint)bitIndex >> 2);
            chunkX = localX - MaskOriginOffset;
            chunkY = localY - MaskOriginOffset;
            chunkZ = localZ - MaskOriginOffset;
        }

        private static int EncodeLocalMortonIndex(int x, int y, int z)
        {
            uint ux = Part1By2((uint)x & LocalMask);
            uint uy = Part1By2((uint)y & LocalMask);
            uint uz = Part1By2((uint)z & LocalMask);
            return (int)(ux | (uy << 1) | (uz << 2));
        }

        private static uint Part1By2(uint value)
        {
            value &= LocalMask;
            value = (value | (value << 16)) & 0x030000FFu;
            value = (value | (value << 8)) & 0x0300F00Fu;
            value = (value | (value << 4)) & 0x030C30C3u;
            value = (value | (value << 2)) & 0x09249249u;
            return value;
        }

        private static int Compact1By2(uint value)
        {
            value &= 0x09249249u;
            value = (value ^ (value >> 2)) & 0x030C30C3u;
            value = (value ^ (value >> 4)) & 0x0300F00Fu;
            value = (value ^ (value >> 8)) & 0x030000FFu;
            value = (value ^ (value >> 16)) & 0x0000007Fu;
            return (int)value;
        }

        private bool RefreshPlayerTransformCache(bool force)
        {
            if (!force && _playerMovement != null)
                return true;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                playerTransform = playerContext.PlayerTransform;
                _playerMovement = playerContext.PlayerMovement;
                if (_playerMovement != null)
                {
                    _lastSampledAup = _playerMovement.CurrentAup;
                    _hasLastSampledAup = true;
                    return true;
                }
            }

            if (playerTransform != null && _playerMovement == null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (_playerMovement != null)
            {
                _lastSampledAup = _playerMovement.CurrentAup;
                _hasLastSampledAup = true;
                return true;
            }

            return false;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            if (_playerMovement == null)
                return false;

            playerAup = _playerMovement.CurrentAup;
            return true;
        }

        private static bool TryResolveAupFromRuntimePosition(Vector3 runtimePosition, out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            float3 numericPosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(numericPosition)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            playerAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return playerAup.IsFinite();
        }

        private bool TryReadPinnedCartographyBuffers(ulong pinnedMask, out CartographyVaultReadBuffers buffers)
        {
            buffers = default;
            if (!_cartographyVaultReady || _cartographyVault == null || !_cartographyHandles.IsCoreCreated())
                return false;

            return TryReadPinnedCartographyBuffers(_cartographyVault, in _cartographyHandles, pinnedMask, out buffers);
        }

        private bool TryReadLegacyExplorationBuffers(
            ulong pinnedMask,
            out NativeArray<ulong>.ReadOnly maskWords,
            out NativeArray<int>.ReadOnly bitIndices,
            out NativeArray<int>.ReadOnly bitIndexCount)
        {
            maskWords = default;
            bitIndices = default;
            bitIndexCount = default;
            if (!_cartographyVaultReady ||
                _cartographyVault == null ||
                (pinnedMask & CartographyPinLegacy) != CartographyPinLegacy ||
                !_cartographyHandles.IsLegacyCreated() ||
                !_cartographyVault.TryReadOnlyHandle(in _cartographyHandles.LegacyExplorationWords, out maskWords) ||
                !_cartographyVault.TryReadOnlyHandle(in _cartographyHandles.LegacyExploredBitIndices, out bitIndices) ||
                !_cartographyVault.TryReadOnlyHandle(in _cartographyHandles.LegacyExploredBitIndexCount, out bitIndexCount) ||
                !HasMinimumLength(maskWords, CartographyGridConstants.WordCount) ||
                !HasMinimumLength(bitIndices, CartographyGridConstants.LegacyExploredBitIndexCapacity) ||
                !HasMinimumLength(bitIndexCount, 1))
            {
                maskWords = default;
                bitIndices = default;
                bitIndexCount = default;
                return false;
            }

            return true;
        }

        private bool TryEnsureLegacyExplorationBuffers(
            ulong pinnedMask,
            out NativeArray<ulong> maskWords,
            out NativeArray<int> bitIndices,
            out NativeArray<int> bitIndexCount)
        {
            maskWords = default;
            bitIndices = default;
            bitIndexCount = default;
            if (!_cartographyVaultReady ||
                _cartographyVault == null ||
                (pinnedMask & CartographyPinLegacy) != CartographyPinLegacy ||
                !_cartographyHandles.IsLegacyCreated() ||
                !_cartographyVault.TryResolveHandle(in _cartographyHandles.LegacyExplorationWords, out maskWords) ||
                !_cartographyVault.TryResolveHandle(in _cartographyHandles.LegacyExploredBitIndices, out bitIndices) ||
                !_cartographyVault.TryResolveHandle(in _cartographyHandles.LegacyExploredBitIndexCount, out bitIndexCount) ||
                !HasMinimumLength(maskWords, CartographyGridConstants.WordCount) ||
                !HasMinimumLength(bitIndices, CartographyGridConstants.LegacyExploredBitIndexCapacity) ||
                !HasMinimumLength(bitIndexCount, 1))
            {
                maskWords = default;
                bitIndices = default;
                bitIndexCount = default;
                return false;
            }

            return true;
        }

        private bool TryResolvePinnedCartographyBuffers(ulong pinnedMask, out CartographyVaultBuffers buffers)
        {
            buffers = default;
            if (!_cartographyVaultReady && !EnsureCartographyVault())
                return false;

            IDataVault vault = _cartographyVault;
            if (vault == null)
                return false;

            return TryResolvePinnedCartographyBuffers(vault, in _cartographyHandles, pinnedMask, out buffers);
        }

        private static bool TryResolvePinnedCartographyBuffers(
            IDataVault vault,
            scoped in CartographyVaultHandles handles,
            ulong pinnedMask,
            out CartographyVaultBuffers buffers)
        {
            buffers = default;
            return vault != null &&
                   ResolvePinned(vault, in handles.DiscoveryWords, pinnedMask, CartographyPinDiscoveryWords, CartographyGridConstants.TotalResidentWordCount, out buffers.DiscoveryWords) &&
                   ResolvePinned(vault, in handles.SectorTable, pinnedMask, CartographyPinSectorTable, CartographyGridConstants.ResidentSectorCount, out buffers.SectorTable) &&
                   ResolvePinned(vault, in handles.UploadPackedR8, pinnedMask, CartographyPinUploadPackedR8, CartographyGridConstants.PackedUploadWordCount, out buffers.UploadPackedR8) &&
                   ResolvePinned(vault, in handles.TelemetryRing, pinnedMask, CartographyPinTelemetryRing, CartographyGridConstants.BlackBoxFrameCount, out buffers.TelemetryRing) &&
                   ResolvePinned(vault, in handles.TelemetryCursor, pinnedMask, CartographyPinTelemetryCursor, 1, out buffers.TelemetryCursor) &&
                   ResolvePinned(vault, in handles.Tuning, pinnedMask, CartographyPinTuning, 1, out buffers.Tuning) &&
                   ResolvePinned(vault, in handles.ScannerProfiles, pinnedMask, CartographyPinScannerProfiles, CartographyGridConstants.ScannerProfileCapacity, out buffers.ScannerProfiles) &&
                   ResolvePinned(vault, in handles.CsvScratch, pinnedMask, CartographyPinCsvScratch, CartographyGridConstants.CsvScratchBytes, out buffers.CsvScratch) &&
                   ResolvePinned(vault, in handles.MockPings, pinnedMask, CartographyPinMockPings, CartographyGridConstants.MaxRevealSignalsPerSlowTick, out buffers.MockPings) &&
                   ResolvePinned(vault, in handles.PendingPings, pinnedMask, CartographyPinPendingPings, CartographyGridConstants.MaxRevealSignalsPerSlowTick, out buffers.PendingPings) &&
                   ResolvePinned(vault, in handles.PendingSignalCounts, pinnedMask, CartographyPinPendingSignalCounts, 1, out buffers.PendingSignalCounts) &&
                   ResolvePinned(vault, in handles.Counters, pinnedMask, CartographyPinCounters, CartographyGridConstants.ResidentSectorCount, out buffers.Counters) &&
                   ResolvePinned(vault, in handles.ActiveSectorHashes, pinnedMask, CartographyPinActiveSectorHashes, CartographyGridConstants.ResidentSectorCount, out buffers.ActiveSectorHashes) &&
                   ResolvePinned(vault, in handles.DebugVoxels, pinnedMask, CartographyPinDebugVoxels, CartographyGridConstants.DebugVoxelCapacity, out buffers.DebugVoxels) &&
                   ResolvePinned(vault, in handles.RleRuns, pinnedMask, CartographyPinRleRuns, CartographyGridConstants.RleRunCapacity, out buffers.RleRuns) &&
                   ResolvePinned(vault, in handles.SurfaceMaskWords, pinnedMask, CartographyPinSurfaceMaskWords, CartographyGridConstants.WordCount, out buffers.SurfaceMaskWords) &&
                   ResolvePinned(vault, in handles.RollbackSnapshotWords, pinnedMask, CartographyPinRollbackSnapshotWords, CartographyGridConstants.WordCount, out buffers.RollbackSnapshotWords) &&
                   ResolvePinned(vault, in handles.State, pinnedMask, CartographyPinState, 1, out buffers.State) &&
                   ResolvePinned(vault, in handles.LegacyExplorationWords, pinnedMask, CartographyPinLegacyExplorationWords, CartographyGridConstants.WordCount, out buffers.LegacyExplorationWords) &&
                   ResolvePinned(vault, in handles.LegacyExploredBitIndices, pinnedMask, CartographyPinLegacyExploredBitIndices, CartographyGridConstants.LegacyExploredBitIndexCapacity, out buffers.LegacyExploredBitIndices) &&
                   ResolvePinned(vault, in handles.LegacyExploredBitIndexCount, pinnedMask, CartographyPinLegacyExploredBitIndexCount, 1, out buffers.LegacyExploredBitIndexCount);
        }

        private static bool TryReadPinnedCartographyBuffers(
            IDataVault vault,
            scoped in CartographyVaultHandles handles,
            ulong pinnedMask,
            out CartographyVaultReadBuffers buffers)
        {
            buffers = default;
            return vault != null &&
                   ReadPinned(vault, in handles.DiscoveryWords, pinnedMask, CartographyPinDiscoveryWords, CartographyGridConstants.TotalResidentWordCount, out buffers.DiscoveryWords) &&
                   ReadPinned(vault, in handles.SectorTable, pinnedMask, CartographyPinSectorTable, CartographyGridConstants.ResidentSectorCount, out buffers.SectorTable) &&
                   ReadPinned(vault, in handles.UploadPackedR8, pinnedMask, CartographyPinUploadPackedR8, CartographyGridConstants.PackedUploadWordCount, out buffers.UploadPackedR8) &&
                   ReadPinned(vault, in handles.TelemetryRing, pinnedMask, CartographyPinTelemetryRing, CartographyGridConstants.BlackBoxFrameCount, out buffers.TelemetryRing) &&
                   ReadPinned(vault, in handles.TelemetryCursor, pinnedMask, CartographyPinTelemetryCursor, 1, out buffers.TelemetryCursor) &&
                   ReadPinned(vault, in handles.Tuning, pinnedMask, CartographyPinTuning, 1, out buffers.Tuning) &&
                   ReadPinned(vault, in handles.ScannerProfiles, pinnedMask, CartographyPinScannerProfiles, CartographyGridConstants.ScannerProfileCapacity, out buffers.ScannerProfiles) &&
                   ReadPinned(vault, in handles.CsvScratch, pinnedMask, CartographyPinCsvScratch, CartographyGridConstants.CsvScratchBytes, out buffers.CsvScratch) &&
                   ReadPinned(vault, in handles.MockPings, pinnedMask, CartographyPinMockPings, CartographyGridConstants.MaxRevealSignalsPerSlowTick, out buffers.MockPings) &&
                   ReadPinned(vault, in handles.PendingPings, pinnedMask, CartographyPinPendingPings, CartographyGridConstants.MaxRevealSignalsPerSlowTick, out buffers.PendingPings) &&
                   ReadPinned(vault, in handles.PendingSignalCounts, pinnedMask, CartographyPinPendingSignalCounts, 1, out buffers.PendingSignalCounts) &&
                   ReadPinned(vault, in handles.Counters, pinnedMask, CartographyPinCounters, CartographyGridConstants.ResidentSectorCount, out buffers.Counters) &&
                   ReadPinned(vault, in handles.ActiveSectorHashes, pinnedMask, CartographyPinActiveSectorHashes, CartographyGridConstants.ResidentSectorCount, out buffers.ActiveSectorHashes) &&
                   ReadPinned(vault, in handles.DebugVoxels, pinnedMask, CartographyPinDebugVoxels, CartographyGridConstants.DebugVoxelCapacity, out buffers.DebugVoxels) &&
                   ReadPinned(vault, in handles.RleRuns, pinnedMask, CartographyPinRleRuns, CartographyGridConstants.RleRunCapacity, out buffers.RleRuns) &&
                   ReadPinned(vault, in handles.SurfaceMaskWords, pinnedMask, CartographyPinSurfaceMaskWords, CartographyGridConstants.WordCount, out buffers.SurfaceMaskWords) &&
                   ReadPinned(vault, in handles.RollbackSnapshotWords, pinnedMask, CartographyPinRollbackSnapshotWords, CartographyGridConstants.WordCount, out buffers.RollbackSnapshotWords) &&
                   ReadPinned(vault, in handles.State, pinnedMask, CartographyPinState, 1, out buffers.State) &&
                   ReadPinned(vault, in handles.LegacyExplorationWords, pinnedMask, CartographyPinLegacyExplorationWords, CartographyGridConstants.WordCount, out buffers.LegacyExplorationWords) &&
                   ReadPinned(vault, in handles.LegacyExploredBitIndices, pinnedMask, CartographyPinLegacyExploredBitIndices, CartographyGridConstants.LegacyExploredBitIndexCapacity, out buffers.LegacyExploredBitIndices) &&
                   ReadPinned(vault, in handles.LegacyExploredBitIndexCount, pinnedMask, CartographyPinLegacyExploredBitIndexCount, 1, out buffers.LegacyExploredBitIndexCount);
        }

        private static bool ResolvePinned<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            ulong pinnedMask,
            ulong bit,
            int minimumLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if ((pinnedMask & bit) == 0UL)
                return true;

            return vault.TryResolveHandle(in handle, out buffer) && HasMinimumLength(buffer, minimumLength);
        }

        private static bool ReadPinned<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            ulong pinnedMask,
            ulong bit,
            int minimumLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if ((pinnedMask & bit) == 0UL)
                return true;

            return vault.TryReadOnlyHandle(in handle, out buffer) && HasMinimumLength(buffer, minimumLength);
        }

        private static bool HasMinimumLength<T>(NativeArray<T> buffer, int minimumLength)
            where T : struct
        {
            return buffer.IsCreated && buffer.Length >= minimumLength;
        }

        private static bool HasMinimumLength<T>(NativeArray<T>.ReadOnly buffer, int minimumLength)
            where T : struct
        {
            return buffer.IsCreated && buffer.Length >= minimumLength;
        }

        private bool EnsureCartographyVault()
        {
            if (_cartographyVaultReady && _cartographyVault != null && _cartographyHandles.IsCoreCreated())
                return true;

            IDataVault vault = _cartographyVault;

            if (vault == null || !CartographyVault.TryEnsure(vault, out _cartographyHandles))
                return false;

            if (!TryAcquireCartographyPins(vault, CartographyPinCoreInitialize, out ulong pinnedMask))
                return false;

            try
            {
                if (!TryResolvePinnedCartographyBuffers(vault, in _cartographyHandles, pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                if (!CartographyLayoutVerifier.ValidateRuntimeLayouts())
                    return false;

                _cartographyVault = vault;
                InitializeCartographyVaultBuffers(buffers);
                _cartographyVaultReady = true;
                return true;
            }
            finally
            {
                ReleaseCartographyPins(vault, pinnedMask);
            }
        }

        private void InitializeCartographyVaultBuffers(CartographyVaultBuffers buffers)
        {
            // [BLOCKING_SYNC_POINT] Initial buffer clear cannot race the upload writer.
            if (!CompleteCartographyUploadJobForStructuralMutation())
                return;

            float globalQualityWeight = ResolveHomeostasisQualityWeight();
            ExecuteClearCartographyUlongBuffer(buffers.DiscoveryWords);
            if (buffers.MockPings.IsCreated)
                ExecuteClearCartographyRevealSignals(buffers.MockPings);

            if (buffers.PendingPings.IsCreated)
                ExecuteClearCartographyRevealSignals(buffers.PendingPings);

            if (buffers.PendingSignalCounts.IsCreated && buffers.PendingSignalCounts.Length > 0)
                buffers.PendingSignalCounts[0] = 0;

            InitializeCartographyVaultJob initializeJob = new InitializeCartographyVaultJob
            {
                Sectors = buffers.SectorTable,
                Counters = buffers.Counters,
                TelemetryRing = buffers.TelemetryRing,
                TelemetryCursor = buffers.TelemetryCursor,
                Tuning = buffers.Tuning,
                ScannerProfiles = buffers.ScannerProfiles,
                ActiveSectorHashes = buffers.ActiveSectorHashes,
                State = buffers.State,
                GlobalQualityWeight = globalQualityWeight
            };
            initializeJob.Execute();

            CartographyTuningDTO tuning = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0
                ? buffers.Tuning[0]
                : CartographyVault.BuildDefaultTuning(globalQualityWeight);
            tuning = SanitizeCartographyTuning(in tuning);
            CacheCartographyTuning(in tuning);
            BuildMockSurfaceMaskJob surfaceMaskJob = new BuildMockSurfaceMaskJob
            {
                SurfaceMaskWords = buffers.SurfaceMaskWords,
                SurfaceThicknessMeters = tuning.SurfaceThicknessMeters,
                GlobalQualityWeight = tuning.GlobalQualityWeight
            };
            for (int i = 0; i < buffers.SurfaceMaskWords.Length; i++)
                surfaceMaskJob.Execute(i);
        }

        private CartographyTuningDTO ResolveCartographyTuning()
        {
            return _hasCartographyTuningSnapshot
                ? SanitizeCartographyTuning(in _cartographyTuningSnapshot)
                : CartographyVault.BuildDefaultTuning(ResolveHomeostasisQualityWeight());
        }

        private CartographyTuningDTO ResolvePinnedCartographyTuning(in CartographyVaultBuffers buffers)
        {
            CartographyTuningDTO tuning = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0
                ? buffers.Tuning[0]
                : ResolveCartographyTuning();

            tuning = SanitizeCartographyTuning(in tuning);
            CacheCartographyTuning(in tuning);
            return tuning;
        }

        private CartographyTuningDTO SanitizeCartographyTuning(in CartographyTuningDTO tuning)
        {
            CartographyTuningDTO sanitized = tuning;
            sanitized.GlobalQualityWeight = ResolveEffectiveCartographyQuality(sanitized.GlobalQualityWeight);
            sanitized.CellSizeMeters = math.clamp(
                math.isfinite(sanitized.CellSizeMeters) ? sanitized.CellSizeMeters : CartographyGridConstants.DefaultPlayerRevealRadiusMeters,
                CartographyGridConstants.MacroCellSizeMeters,
                CartographyGridConstants.MaxDesignerVoxelSizeMeters);
            sanitized.UploadCadenceFrames = CartographyGridMath.ResolveUploadIntervalFrames(sanitized.GlobalQualityWeight);
            return sanitized;
        }

        private void CacheCartographyTuning(in CartographyTuningDTO tuning)
        {
            _cartographyTuningSnapshot = tuning;
            _hasCartographyTuningSnapshot = true;
        }

        private static float ResolveHomeostasisQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveEffectiveCartographyQuality(float tuningQuality)
        {
            float localQuality = math.saturate(math.isfinite(tuningQuality) ? tuningQuality : 1f);
            return math.saturate(math.min(ResolveHomeostasisQualityWeight(), localQuality));
        }

        private static void ResetCartographyCounter(NativeArray<CartographyCounterDTO> counters)
        {
            if (!counters.IsCreated || counters.Length == 0)
                return;

            CartographyCounterDTO counter = counters[0];
            counter.Changed = 0;
            counter.DiscoveredDelta = 0;
            counter.LastFailureFlags = 0u;
            counters[0] = counter;
        }

        private static void SetCartographyTotal(
            NativeArray<CartographyCounterDTO> counters,
            NativeArray<ulong> discoveryWords)
        {
            if (!counters.IsCreated || counters.Length == 0)
                return;

            int wordCount = math.min(discoveryWords.IsCreated ? discoveryWords.Length : 0, CartographyGridConstants.WordCount);
            int total = 0;
            for (int i = 0; i < wordCount; i++)
            {
                ulong word = discoveryWords[i];
                total += math.countbits((uint)word);
                total += math.countbits((uint)(word >> 32));
            }

            CartographyCounterDTO counter = counters[0];
            counter.TotalDiscoveredVoxels = total;
            counters[0] = counter;
        }

        private bool RevealCartographyCell(in CartographyAup cartographyAup, MapRevealSignalFlags flags)
        {
            if (!TryAcquireCartographyPins(CartographyPinDiscoveryWords | CartographyPinCounters, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return false;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                if (!CartographyGridMath.TryEncode(
                        in cartographyAup,
                        out int bitIndex,
                        out int wordIndex,
                        out int bitOffset))
                {
                    return false;
                }

                ResetCartographyCounter(buffers.Counters);
                ulong before = buffers.DiscoveryWords[wordIndex];
                CartographyRevealAupCellJob revealJob = new CartographyRevealAupCellJob
                {
                    DiscoveredSectors = buffers.DiscoveryWords,
                    Counters = buffers.Counters,
                    Center = cartographyAup,
                    WordOffset = 0
                };
                revealJob.Execute();

                ulong after = buffers.DiscoveryWords[wordIndex];
                _lastCartographyBitIndex = bitIndex;
                return before != after || (flags & MapRevealSignalFlags.Player) == 0;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private int DrainMapRevealSignals(out bool changed)
        {
            changed = false;
            const ulong pinMask = CartographyPinDiscoveryWords |
                                  CartographyPinSurfaceMaskWords |
                                  CartographyPinMockPings |
                                  CartographyPinPendingSignalCounts |
                                  CartographyPinCounters |
                                  CartographyPinTuning;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return 0;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers) ||
                    !buffers.MockPings.IsCreated ||
                    !buffers.PendingSignalCounts.IsCreated ||
                    buffers.PendingSignalCounts.Length == 0 ||
                    !buffers.Counters.IsCreated ||
                    buffers.Counters.Length == 0)
                {
                    return 0;
                }

                ResetCartographyCounter(buffers.Counters);
                CartographyTuningDTO tuning = ResolvePinnedCartographyTuning(in buffers);
                int pendingCount = math.clamp(
                    buffers.PendingSignalCounts[0],
                    0,
                    math.min(buffers.MockPings.Length, CartographyGridConstants.MaxRevealSignalsPerSlowTick));

                int processed = 0;
                while (processed < pendingCount)
                {
                    MapRevealSignal signal = buffers.MockPings[processed];
                    buffers.MockPings[processed] = default;
                    if (!CartographyGridMath.IsFinite(in signal.Center))
                    {
                        FlagCartographyFailure(buffers, CartographyGridConstants.TelemetryFlagOutOfBoundsAup);
                        _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagOutOfBoundsAup;
                        processed++;
                        continue;
                    }

                    float radius = ClampRevealRadius(signal.RadiusMeters);
                    int useDearLie = (signal.Flags & MapRevealSignalFlags.Sonar) != MapRevealSignalFlags.None ? 1 : 0;
                    ApplySonarDiscoveryJob discoveryJob = new ApplySonarDiscoveryJob
                    {
                        DiscoveredSectors = buffers.DiscoveryWords,
                        SurfaceMaskWords = buffers.SurfaceMaskWords,
                        Counters = buffers.Counters,
                        Center = signal.Center,
                        RadiusMeters = radius,
                        SurfaceThicknessMeters = tuning.SurfaceThicknessMeters,
                        GlobalQualityWeight = tuning.GlobalQualityWeight,
                        UseExplicitCenterAup = 0,
                        UseSdfSurfaceMask = useDearLie == 0 ? 1 : 0,
                        WordOffset = 0
                    };
                    discoveryJob.Execute();
                    processed++;
                }

                CartographyCounterDTO counter = buffers.Counters[0];
                counter.PendingSignalCount = 0u;
                buffers.Counters[0] = counter;
                buffers.PendingSignalCounts[0] = 0;
                changed = buffers.Counters.IsCreated &&
                          buffers.Counters.Length > 0 &&
                          buffers.Counters[0].Changed != 0;
                if (buffers.Counters.IsCreated &&
                    buffers.Counters.Length > 0 &&
                    (buffers.Counters[0].LastFailureFlags & CartographyGridConstants.TelemetryFlagOutOfBoundsAup) != 0u)
                {
                    _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagOutOfBoundsAup;
                }

                return processed;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private int InjectPoiReveals(out bool changed)
        {
            changed = false;
            const ulong pinMask = CartographyPinDiscoveryWords |
                                  CartographyPinSurfaceMaskWords |
                                  CartographyPinCounters |
                                  CartographyPinTuning;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return 0;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return 0;

                ResetCartographyCounter(buffers.Counters);
                CartographyTuningDTO tuning = ResolvePinnedCartographyTuning(in buffers);
                PDAMarkerRegistry markerRegistry = _cachedMarkerRegistry;
                int markerCount = markerRegistry != null ? markerRegistry.CopyMarkers(_poiMarkerScratch, hudOnly: false) : 0;
                int count = math.min(markerCount, CartographyGridConstants.MaxPoiRevealPerSlowTick);
                for (int i = 0; i < count; i++)
                {
                    PDAMarkerSnapshot marker = _poiMarkerScratch[i];
                    AbsoluteUniversePosition markerAup = marker.PositionAup;
                    CartographyAup markerCartographyAup = ToCartographyAup(in markerAup);
                    if (CartographyGridMath.IsFinite(in markerCartographyAup))
                    {
                        ApplySonarDiscoveryJob markerJob = new ApplySonarDiscoveryJob
                        {
                            DiscoveredSectors = buffers.DiscoveryWords,
                            SurfaceMaskWords = buffers.SurfaceMaskWords,
                            Counters = buffers.Counters,
                            Center = markerCartographyAup,
                            RadiusMeters = math.max(CartographyGridConstants.MacroCellSizeMeters, tuning.SonarPingRadiusMeters * 0.25f),
                            SurfaceThicknessMeters = tuning.SurfaceThicknessMeters,
                            GlobalQualityWeight = tuning.GlobalQualityWeight,
                            UseExplicitCenterAup = 0,
                            UseSdfSurfaceMask = 1,
                            WordOffset = 0
                        };
                        markerJob.Execute();
                    }

                    _poiMarkerScratch[i] = default;
                }

                PersistentWorldRegistry persistentWorldRegistry = _cachedPersistentWorldRegistry;
                if (persistentWorldRegistry != null && count < CartographyGridConstants.MaxPoiRevealPerSlowTick)
                {
                    int persistentDeltaCount = persistentWorldRegistry.SaveSnapshotCount;
                    int chunkSizeMeters = math.max(1, persistentWorldRegistry.ChunkSizeMeters);
                    for (int i = 0;
                         i < persistentDeltaCount &&
                         count < CartographyGridConstants.MaxPoiRevealPerSlowTick;
                         i++)
                    {
                        if (!persistentWorldRegistry.TryReadSaveSnapshotDelta(i, out PersistentWorldDeltaRecord delta))
                            break;

                        if (!PersistentWorldDeltaRecord.IsValid(in delta) || PersistentWorldDeltaRecord.IsDeleted(in delta))
                            continue;

                        AbsoluteUniversePosition position = delta.UnpackPosition(chunkSizeMeters);
                        CartographyAup persistentCartographyAup = ToCartographyAup(in position);
                        if (CartographyGridMath.IsFinite(in persistentCartographyAup))
                        {
                            ApplySonarDiscoveryJob persistentJob = new ApplySonarDiscoveryJob
                            {
                                DiscoveredSectors = buffers.DiscoveryWords,
                                SurfaceMaskWords = buffers.SurfaceMaskWords,
                                Counters = buffers.Counters,
                                Center = persistentCartographyAup,
                                RadiusMeters = math.max(CartographyGridConstants.MacroCellSizeMeters, tuning.SonarPingRadiusMeters * 0.2f),
                                SurfaceThicknessMeters = tuning.SurfaceThicknessMeters,
                                GlobalQualityWeight = tuning.GlobalQualityWeight,
                                UseExplicitCenterAup = 0,
                                UseSdfSurfaceMask = 1,
                                WordOffset = 0
                            };
                            persistentJob.Execute();
                        }

                        count++;
                    }
                }

                changed = buffers.Counters.IsCreated &&
                          buffers.Counters.Length > 0 &&
                          buffers.Counters[0].Changed != 0;
                return count;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private static float ClampRevealRadius(float radiusMeters)
        {
            if (!math.isfinite(radiusMeters))
                return CartographyGridConstants.MacroCellSizeMeters;

            return math.clamp(
                radiusMeters,
                CartographyGridConstants.MacroCellSizeMeters,
                CartographyGridConstants.MaxRevealRadiusMeters);
        }

        private void RecordCartographyVaultContention()
        {
            CartographyAup telemetryAup = default;
            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                telemetryAup = ToCartographyAup(in playerAup);

            RecordCartographyBlackBox(
                in telemetryAup,
                0,
                0,
                CartographyGridConstants.TelemetryFlagVaultContention);
        }

        private void RecordCartographyFaultAndDump(in CartographyAup playerAup, uint stateFlags)
        {
            RecordCartographyBlackBox(
                in playerAup,
                0,
                0,
                stateFlags);
            DumpCartographyBlackBox();
        }

        private void RecordCartographyBlackBox(in CartographyAup playerAup, int signalCount, int poiCount, uint stateFlags)
        {
            if (!TryAcquireCartographyPins(CartographyPinTelemetry, out ulong pinnedMask))
                return;

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return;

                SetCartographyTotal(buffers.Counters, buffers.DiscoveryWords);
                CartographyTuningDTO tuning = ResolvePinnedCartographyTuning(in buffers);
                RecordCartographyTelemetryJob telemetryJob = new RecordCartographyTelemetryJob
                {
                    TelemetryRing = buffers.TelemetryRing,
                    TelemetryCursor = buffers.TelemetryCursor,
                    Counters = buffers.Counters,
                    State = buffers.State,
                    PlayerAup = playerAup,
                    FrameIndex = _cartographyFrameIndex++,
                    Revision = _cartographyRevision,
                    RevealedSignalCount = signalCount,
                    RevealedPoiCount = poiCount,
                    StateFlags = stateFlags,
                    GlobalQualityWeight = tuning.GlobalQualityWeight
                };
                telemetryJob.Execute();
                CacheLatestCartographyTelemetry(in buffers);
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        private void DumpCartographyBlackBox()
        {
            if (_cartographyDumpedThisSession ||
                !TryAcquireCartographyPins(CartographyPinTelemetryRing | CartographyPinTelemetryCursor, out ulong pinnedMask))
                return;

            bool staged = false;
            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return;

                _cartographyDumpedThisSession = true;
                staged = CartographyVault.TryStageBlackBoxSnapshot(in buffers);
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }

            if (staged)
                _cartographyBlackBoxDumpStaged = true;
        }

        private void FlushStagedCartographyBlackBoxDump()
        {
            if (!_cartographyBlackBoxDumpStaged)
                return;

            _cartographyBlackBoxDumpStaged = false;
            CartographyVault.TryQueueStagedBlackBoxDump(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        }

        private void CacheLatestCartographyTelemetry(in CartographyVaultBuffers buffers)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length == 0)
                return;

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0
                ? buffers.TelemetryCursor[0] - 1
                : 0;
            if (cursor < 0)
                cursor += buffers.TelemetryRing.Length;

            _latestCartographyTelemetrySnapshot = buffers.TelemetryRing[math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1)];
            _hasLatestCartographyTelemetrySnapshot = true;
        }

        public bool GenerateMockExplorationData()
        {
            InitializeExplorationMask();
            const ulong pinMask = CartographyPinDiscoveryWords |
                                  CartographyPinSectorTable |
                                  CartographyPinCounters |
                                  CartographyPinActiveSectorHashes |
                                  CartographyPinTuning;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return false;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                CartographyTuningDTO tuning = ResolvePinnedCartographyTuning(in buffers);
                ulong sectorHash = buffers.ActiveSectorHashes.IsCreated && buffers.ActiveSectorHashes.Length > 4
                    ? buffers.ActiveSectorHashes[4]
                    : CartographyGridConstants.DefaultSectorHashSeed;
                GenerateMockExplorationDataJob mockJob = new GenerateMockExplorationDataJob
                {
                    DiscoveredSectors = buffers.DiscoveryWords,
                    SectorTable = buffers.SectorTable,
                    SimulationFrameCounter = _cartographyFrameIndex,
                    SectorHash = sectorHash,
                    GlobalQualityWeight = tuning.GlobalQualityWeight,
                    WordOffset = 0
                };
                for (int i = 0; i < CartographyGridConstants.WordCount; i++)
                    mockJob.Execute(i);
                SetCartographyTotal(buffers.Counters, buffers.DiscoveryWords);
                _cartographyRevision++;
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        public bool TryUploadPreparedCartography(
            GraphicsBuffer destination,
            float globalQualityWeight,
            out int framesBetweenUploads,
            out uint revision)
        {
            framesBetweenUploads = CartographyGridMath.ResolveUploadIntervalFrames(globalQualityWeight);
            revision = _cartographyRevision;
            if (destination == null ||
                !destination.IsValid())
            {
                return false;
            }

            if (_cartographyUploadPrepared &&
                TryCopyPreparedCartographyUpload(destination, out framesBetweenUploads, out revision))
            {
                return true;
            }

            _cartographyUploadRequested = true;
            _cartographyUploadRequestedQuality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return false;
        }

        private void TryScheduleRequestedCartographyUpload()
        {
            if (!_cartographyUploadRequested ||
                _cartographyUploadPending ||
                _cartographyUploadPrepared)
            {
                return;
            }

            float quality = _cartographyUploadRequestedQuality;
            _cartographyUploadRequested = false;
            TryScheduleCartographyUpload(
                quality,
                out _,
                out _);
        }

        private bool TryCopyPreparedCartographyUpload(
            GraphicsBuffer destination,
            out int framesBetweenUploads,
            out uint revision)
        {
            framesBetweenUploads = math.max(1, _cartographyUploadPreparedCadence);
            revision = _cartographyUploadPreparedRevision;
            if (!_cartographyUploadPrepared ||
                destination == null ||
                !destination.IsValid() ||
                !_cartographyVaultReady)
            {
                return false;
            }

            IDataVault vault = _cartographyVault;
            if (!TryReadPinnedCartographyBuffers(
                    vault,
                    in _cartographyHandles,
                    CartographyPinUploadPackedR8,
                    out CartographyVaultReadBuffers buffers) ||
                !buffers.UploadPackedR8.IsCreated)
            {
                return false;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(
                destination,
                buffers.UploadPackedR8,
                math.min(buffers.UploadPackedR8.Length, CartographyGridConstants.PackedUploadWordCount));
            _cartographyUploadPrepared = false;
            return true;
        }

        private bool TryScheduleCartographyUpload(
            float globalQualityWeight,
            out int framesBetweenUploads,
            out uint revision)
        {
            InitializeExplorationMask();
            framesBetweenUploads = CartographyGridMath.ResolveUploadIntervalFrames(globalQualityWeight);
            revision = _cartographyRevision;
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            framesBetweenUploads = CartographyGridMath.ResolveUploadIntervalFrames(quality);
            if (_cartographyUploadPending)
                return true;

            _cartographyUploadPrepared = false;
            if (!TryPinCartographyUploadBuffers())
            {
                RecordCartographyBlackBox(
                    in _cartographyDispatcherPlayerAup,
                    0,
                    0,
                    CartographyGridConstants.TelemetryFlagVaultContention);
                return false;
            }

            bool scheduled = false;
            try
            {
                if (!TryResolvePinnedCartographyBuffers(_cartographyUploadPinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                FormatCartographyUploadR8Job formatJob = new FormatCartographyUploadR8Job
                {
                    DiscoveredSectors = buffers.DiscoveryWords,
                    UploadPackedR8 = buffers.UploadPackedR8,
                    GlobalQualityWeight = quality,
                    WordOffset = 0
                };
                CopyCartographyRollbackSnapshotJob snapshotJob = new CopyCartographyRollbackSnapshotJob
                {
                    DiscoveryWords = buffers.DiscoveryWords,
                    RollbackSnapshotWords = buffers.RollbackSnapshotWords,
                    WordOffset = 0
                };

                int formatBatch = SystemDispatcher.ResolveInnerloopBatchCount(CartographyGridConstants.PackedUploadWordCount, 64, 512);
                int snapshotBatch = SystemDispatcher.ResolveInnerloopBatchCount(CartographyGridConstants.WordCount, 64, 512);
                JobHandle formatHandle = formatJob.Schedule(CartographyGridConstants.PackedUploadWordCount, formatBatch);
                JobHandle snapshotHandle = snapshotJob.Schedule(CartographyGridConstants.WordCount, snapshotBatch);
                _cartographyUploadHandle = JobHandle.CombineDependencies(formatHandle, snapshotHandle);
                _cartographyUploadPending = true;
                _cartographyUploadPendingRevision = _cartographyRevision;
                _cartographyUploadPendingCadence = framesBetweenUploads;
                H8Memory.RegisterActiveJob(SystemID.UI, _cartographyUploadHandle);
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseCartographyUploadPins();
            }
        }

        public bool TryBuildCartographyRleRuns(out int runCount)
        {
            InitializeExplorationMask();
            runCount = 0;
            const ulong pinMask = CartographyPinDiscoveryWords | CartographyPinRleRuns | CartographyPinCounters;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                _cartographyDeferredDumpFlags |= CartographyGridConstants.TelemetryFlagVaultContention;
                return false;
            }

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                BuildCartographyRleRunsJob rleJob = new BuildCartographyRleRunsJob
                {
                    DiscoveryWords = buffers.DiscoveryWords,
                    RleRuns = buffers.RleRuns,
                    Counters = buffers.Counters,
                    WordOffset = 0,
                    WordCount = CartographyGridConstants.WordCount
                };
                rleJob.Execute();

                runCount = buffers.Counters.IsCreated && buffers.Counters.Length > 0
                    ? math.clamp(buffers.Counters[0].DiscoveredDelta, 0, buffers.RleRuns.Length)
                    : 0;
                return buffers.RleRuns.IsCreated;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }

        public bool TryGetLatestCartographyTelemetry(out CartographyTelemetryEntry entry)
        {
            entry = _latestCartographyTelemetrySnapshot;
            return _hasLatestCartographyTelemetrySnapshot;
        }

        public bool TryGetCartographyTuning(out CartographyTuningDTO tuning)
        {
            tuning = ResolveCartographyTuning();
            return _hasCartographyTuningSnapshot;
        }

        public bool TrySetCartographyTuning(in CartographyTuningDTO tuning)
        {
            InitializeExplorationMask();
            if (!_cartographyVaultReady && !EnsureCartographyVault())
                return false;

            if (!CartographyVault.TrySetTuning(_cartographyVault, ref _cartographyHandles, in tuning))
            {
                RecordCartographyVaultContention();
                return false;
            }

            const ulong pinMask = CartographyPinTuning | CartographyPinSurfaceMaskWords;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
            {
                RecordCartographyVaultContention();
                return false;
            }

            bool resolverFailed = false;
            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                {
                    resolverFailed = true;
                    return false;
                }

                CartographyTuningDTO sanitized = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0
                    ? buffers.Tuning[0]
                    : CartographyVault.BuildDefaultTuning(ResolveEffectiveCartographyQuality(tuning.GlobalQualityWeight));
                sanitized = SanitizeCartographyTuning(in sanitized);
                CacheCartographyTuning(in sanitized);
                BuildMockSurfaceMaskJob surfaceMaskJob = new BuildMockSurfaceMaskJob
                {
                    SurfaceMaskWords = buffers.SurfaceMaskWords,
                    SurfaceThicknessMeters = sanitized.SurfaceThicknessMeters,
                    GlobalQualityWeight = sanitized.GlobalQualityWeight
                };
                for (int i = 0; i < buffers.SurfaceMaskWords.Length; i++)
                    surfaceMaskJob.Execute(i);
                _cartographyRevision++;
                return true;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
                if (resolverFailed)
                    RecordCartographyVaultContention();
            }
        }

#if UNITY_EDITOR
        public bool TryLoadScannerProfilesCsvForEditor(string projectRoot, out int appliedRows)
        {
            InitializeExplorationMask();
            appliedRows = 0;
            const ulong pinMask = CartographyPinScannerProfiles | CartographyPinCsvScratch | CartographyPinCounters;
            if (!TryAcquireCartographyPins(pinMask, out ulong pinnedMask))
                return false;

            try
            {
                if (!TryResolvePinnedCartographyBuffers(pinnedMask, out CartographyVaultBuffers buffers))
                    return false;

                bool loaded = CartographyVault.TryLoadScannerProfilesCsvForEditor(
                    buffers,
                    projectRoot,
                    out appliedRows);
                if (loaded)
                    _cartographyRevision++;
                return loaded;
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }
#endif

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryAcquireCartographyPins(CartographyPinDiscoveryWords, out ulong pinnedMask))
                return;

            try
            {
                if (!TryReadPinnedCartographyBuffers(pinnedMask, out CartographyVaultReadBuffers buffers) ||
                    !buffers.DiscoveryWords.IsCreated)
                    return;

                CartographyAup centerAup;
                if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                    centerAup = ToCartographyAup(in playerAup);
                else
                    return;

                if (!CartographyGridMath.TryResolveMacroCell(in centerAup, out int3 centerCell))
                    return;

                Vector3 origin = playerTransform != null ? playerTransform.position : transform.position;
                float scale = CartographyGridConstants.MacroCellSizeMeters * 0.02f;
                const int debugRadius = 4;
                Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.55f);
                for (int z = -debugRadius; z <= debugRadius; z++)
                {
                    for (int y = -debugRadius; y <= debugRadius; y++)
                    {
                        for (int x = -debugRadius; x <= debugRadius; x++)
                        {
                            Vector3 local = new Vector3(x * scale, y * scale, z * scale);
                            Gizmos.DrawWireCube(origin + local, Vector3.one * (scale * 0.9f));
                        }
                    }
                }

                Gizmos.color = new Color(0.05f, 1f, 0.28f, 0.32f);
                for (int z = -debugRadius; z <= debugRadius; z++)
                {
                    for (int y = -debugRadius; y <= debugRadius; y++)
                    {
                        for (int x = -debugRadius; x <= debugRadius; x++)
                        {
                            int3 macroCell = centerCell + new int3(x, y, z);
                            if (!CartographyGridMath.TryEncodeMacroCell(macroCell, out _, out int wordIndex, out int bitOffset) ||
                                (uint)wordIndex >= (uint)buffers.DiscoveryWords.Length ||
                                (buffers.DiscoveryWords[wordIndex] & (1UL << bitOffset)) == 0UL)
                            {
                                continue;
                            }

                            Vector3 local = new Vector3(x * scale, y * scale, z * scale);
                            Gizmos.DrawCube(origin + local, Vector3.one * (scale * 0.55f));
                            Gizmos.color = new Color(0.1f, 1f, 0.4f, 0.75f);
                            Gizmos.DrawWireCube(origin + local, Vector3.one * (scale * 0.85f));
                            Gizmos.color = new Color(0.05f, 1f, 0.28f, 0.32f);
                        }
                    }
                }
            }
            finally
            {
                ReleaseCartographyPins(_cartographyVault, pinnedMask);
            }
        }
#endif

        private static CartographyAup ToCartographyAup(in AbsoluteUniversePosition aup)
        {
            return new CartographyAup
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ
            };
        }

        private void DrainPhysicsEventPayloads()
        {
            if (!_registeredToAcousticEvents)
                return;

            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType == (ushort)PhysicsEventType.AcousticPing)
                    HandleAcousticPingPayload(in payload);
            }
        }

        private void HandleAcousticPingPayload(in PhysicsEventPayload pingEvent)
        {
            if (!TryResolveAupFromRuntimePosition(pingEvent.RuntimePosition, out AbsoluteUniversePosition pingAup))
                return;

            MapRevealSignal signal = new MapRevealSignal
            {
                Center = ToCartographyAup(in pingAup),
                RadiusMeters = ClampRevealRadius(pingEvent.RadiusMeters),
                SourceId = unchecked((uint)math.max(0, pingEvent.PrimaryId)),
                Flags = MapRevealSignalFlags.Acoustic
            };
            EnqueueMapReveal(in signal);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            if (intensity <= 0.001f || !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            float radius = ClampRevealRadius(SpectrumEvents.LastSonarPulseRadiusMeters);
            MapRevealSignal signal = new MapRevealSignal
            {
                Center = ToCartographyAup(in playerAup),
                RadiusMeters = radius,
                SourceId = (uint)math.round(math.saturate(intensity) * 1000f),
                Flags = MapRevealSignalFlags.Sonar
            };
            EnqueueMapReveal(in signal);
        }

        private void HandleBiomeChanged(int biomeId)
        {
            if (!forwardBiomeDiscovery || biomeId <= 0)
                return;

            HectonDiscoveryManager discoveryManager = _cachedDiscoveryManager;
            if (discoveryManager != null)
                discoveryManager.DiscoverBiome(biomeId);
        }

        void IMapMagicBiomeEventListener.OnMapMagicBiomeChanged(int biomeId)
        {
            HandleBiomeChanged(biomeId);
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredToTick = false;
        }

        private void TryRegisterWithSlowTickManager()
        {
            if (_registeredToSlowTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void TryRegisterCartographyDispatcher()
        {
            if (_cartographyDispatcherRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            EnsureCartographyDispatcherSystems();
            bool registered =
                GlobalRegistry.TryRegisterDispatcherSystem(_cartographyPreSimulationPhase) &&
                GlobalRegistry.TryRegisterDispatcherSystem(_cartographySimulationPhase) &&
                GlobalRegistry.TryRegisterDispatcherSystem(_cartographyPostSimulationPhase) &&
                GlobalRegistry.TryRegisterDispatcherSystem(_cartographyVisualSyncPhase);
            if (!registered)
            {
                UnregisterCartographyDispatcher();
                return;
            }

            _cartographyDispatcherRegistered = true;
        }

        private void EnsureCartographyDispatcherSystems()
        {
            if (_cartographyPreSimulationPhase != null)
                return;

            // COLD ALLOC: IDispatcherSystem[4] — Kahn dispatcher phase adapters — owner: PlayerExplorationTracker
            _cartographyPreSimulationPhase = new CartographyDispatcherPhaseSystem(
                this,
                DispatcherPhase.PreSimulation,
                CartographyPreSimulationSystemHash);
            _cartographySimulationPhase = new CartographyDispatcherPhaseSystem(
                this,
                DispatcherPhase.Simulation,
                CartographySimulationSystemHash);
            _cartographyPostSimulationPhase = new CartographyDispatcherPhaseSystem(
                this,
                DispatcherPhase.PostSimulation,
                CartographyPostSimulationSystemHash);
            _cartographyVisualSyncPhase = new CartographyDispatcherPhaseSystem(
                this,
                DispatcherPhase.VisualSync,
                CartographyVisualSyncSystemHash);
        }

        private void UnregisterCartographyDispatcher()
        {
            if (_cartographyPreSimulationPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographyPreSimulationPhase);
            if (_cartographySimulationPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographySimulationPhase);
            if (_cartographyPostSimulationPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographyPostSimulationPhase);
            if (_cartographyVisualSyncPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographyVisualSyncPhase);

            _cartographyDispatcherRegistered = false;
            _cartographyDispatcherFrameScheduled = false;
        }

        private void UnregisterFromSlowTickManager()
        {
            if (!_registeredToSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredToSlowTick = false;
        }

        private void TryRegisterSignalListeners()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToAcousticEvents)
            {
                _registeredToAcousticEvents = true;
            }

            if (!_registeredToSonarEvents)
            {
                SpectrumEvents.RegisterSonarPingListener(this);
                _registeredToSonarEvents = true;
            }
        }

        private void UnregisterSignalListeners()
        {
            if (_registeredToAcousticEvents)
            {
                _registeredToAcousticEvents = false;
                _lastPhysicsEventSnapshotGeneration = 0;
            }

            if (_registeredToSonarEvents)
            {
                SpectrumEvents.UnregisterSonarPingListener(this);
                _registeredToSonarEvents = false;
            }
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;

            if (_saveService == null)
                return;

            _saveService.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredToSave = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    UnregisterFromSaveManager();
                    _saveService = currentService as ISaveService;
                    TryRegisterWithSaveManager();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindCartographyVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.PDAMarkerRuntime:
                    _cachedMarkerRegistry = currentService as PDAMarkerRegistry;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _cachedPersistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.DiscoveryRuntime:
                    _cachedDiscoveryManager = currentService as HectonDiscoveryManager;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _saveService = GlobalRegistry.Save;
            _cartographyVault = GlobalRegistry.DataVault;
            CachePlayerContext(GlobalRegistry.Player);
            _cachedMarkerRegistry = GlobalRegistry.PDAMarkers;
            _cachedPersistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _cachedDiscoveryManager = GlobalRegistry.Discovery;
        }

        private void CachePlayerContext(IPlayerRuntimeContext playerContext)
        {
            _cachedPlayerContext = playerContext;
            if (playerContext == null)
                return;

            playerTransform = playerContext.PlayerTransform;
            _playerMovement = playerContext.PlayerMovement;
            if (_playerMovement != null)
            {
                _lastSampledAup = _playerMovement.CurrentAup;
                _hasLastSampledAup = true;
            }
        }

        private void RebindCartographyVault(IDataVault nextVault)
        {
            CompleteCartographySimulationJobForTeardown();
            CompleteCartographyUploadJobForTeardown();
            ReleaseCartographySimulationPins();
            ReleaseCartographyUploadPins();
            _cartographyVault = nextVault;
            _cartographyHandles = default;
            _cartographyVaultReady = false;
            _explorationMaskInitialized = false;
            _cartographyUploadPending = false;
            _cartographyUploadPrepared = false;
            _cartographyUploadRequested = false;
            if (isActiveAndEnabled)
                InitializeExplorationMask();
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

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PlayerExplorationTracker registered = s_activeRuntimeInstance ?? GlobalRegistry.PlayerExploration;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(this);
                return;
            }

            GlobalRegistry.RegisterPlayerExplorationRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PlayerExploration, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPlayerExplorationRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

        private sealed class CartographyDispatcherPhaseSystem : IDispatcherSystem
        {
            private readonly PlayerExplorationTracker _owner;
            private readonly DispatcherPhase _phase;
            private readonly uint _systemHash;

            public CartographyDispatcherPhaseSystem(
                PlayerExplorationTracker owner,
                DispatcherPhase phase,
                uint systemHash)
            {
                _owner = owner;
                _phase = phase;
                _systemHash = systemHash;
            }

            public uint GetSystemIdHash()
            {
                return _systemHash;
            }

            public DispatcherPhase GetDispatcherPhase()
            {
                return _phase;
            }

            public byte GetBucketId()
            {
                return byte.MaxValue;
            }

            public int GetDependencyCount()
            {
                return 0;
            }

            public uint GetDependencyHash(int dependencyIndex)
            {
                return 0u;
            }

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
                if (_phase == DispatcherPhase.PreSimulation)
                    _owner.CartographyPreSimulationTick(in timing);
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return _phase == DispatcherPhase.Simulation
                    ? _owner.ScheduleCartographySimulation(in timing, in context, dependsOn)
                    : dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                if (_phase == DispatcherPhase.PostSimulation)
                    _owner.CartographyPostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                if (_phase == DispatcherPhase.VisualSync)
                    _owner.CartographyVisualSyncTick(in timing);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementSampleDistance = math.max(0.25f, movementSampleDistance);
        }
#endif
    }
}
