using System;
using System.IO;
using Hecton8.Cartography;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Physics;
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
    /// <summary>
    /// Tracks player movement across a dense 16m Morton-ordered exploration mask for PDA fog-of-war queries.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/Player Exploration Tracker")]
    public sealed class PlayerExplorationTracker : MonoBehaviour, ITickable, ISlowTickable, ISaveable, IMapMagicBiomeEventListener, IAcousticPingEventListener, ISonarPingEventListener
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
        private const string NativeMemoryOwner = nameof(PlayerExplorationTracker);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const uint CartographyPreSimulationSystemHash = 0x53313350u;
        private const uint CartographySimulationSystemHash = 0x53313349u;
        private const uint CartographyPostSimulationSystemHash = 0x5331334Fu;

        [Header("References")]
        [Tooltip("Optional explicit player transform. When empty, the tracker resolves the current registry player.")]
        [SerializeField] private Transform playerTransform;

        [Header("Exploration Grid")]
        [Tooltip("Minimum movement distance before the tracker re-evaluates chunk membership.")]
        [SerializeField, Min(0.25f)] private float movementSampleDistance = 4f;
        [Tooltip("When enabled, biome changes from MapMagic automatically feed the discovery registry.")]
        [SerializeField] private bool forwardBiomeDiscovery = true;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;

        // COLD ALLOC: long[32768] — save DTO word staging for dense Morton exploration mask — owner: PlayerExplorationTracker
        private readonly long[] _saveMaskWordBuffer = new long[MaskWordCount];
        // COLD ALLOC: PDAMarkerSnapshot[64] — PDA marker POI staging for cartography macro reveal — owner: PlayerExplorationTracker
        private readonly PDAMarkerSnapshot[] _poiMarkerScratch = new PDAMarkerSnapshot[CartographyGridConstants.MaxPoiRevealPerSlowTick];
        private NativeBitArray _exploredChunkMask;
        private NativeList<int> _exploredBitIndices;
        private IDataVault _cartographyVault;
        private CartographyVaultHandles _cartographyHandles;
        private bool _registeredToTick;
        private bool _registeredToSlowTick;
        private bool _registeredToSave;
        private bool _registeredToAcousticEvents;
        private bool _registeredToSonarEvents;
        private bool _serviceRegistered;
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
        private CartographyDispatcherPhaseSystem _cartographyPreSimulationPhase;
        private CartographyDispatcherPhaseSystem _cartographySimulationPhase;
        private CartographyDispatcherPhaseSystem _cartographyPostSimulationPhase;
        private bool _cartographyDispatcherRegistered;
        private bool _cartographyDispatcherFrameScheduled;
        private bool _cartographyDispatcherHasPlayerAup;
        private CartographyAup _cartographyDispatcherPlayerAup;
        private int _cartographyDispatcherPendingSignalCount;
        private int _cartographyDispatcherPoiCount;
        private uint _nextPoiRevealFrame;
        private JobHandle _cartographyUploadHandle;
        private bool _cartographyUploadPending;
        private uint _cartographyUploadPendingRevision;
        private int _cartographyUploadPendingCadence;

        /// <summary>Live registry-owned instance for PDA map systems.</summary>
        public static PlayerExplorationTracker Instance => GlobalRegistry.PlayerExploration;

        /// <summary>Raised when a previously unexplored PDA chunk becomes visible.</summary>
        public event Action<Vector2Int> ChunkExplored;

        /// <summary>Total explored chunk count currently held in memory.</summary>
        public int ExploredChunkCount => _exploredBitIndices.IsCreated ? _exploredBitIndices.Length : 0;

        /// <summary>World-space size represented by one persisted exploration chunk.</summary>
        public float ChunkWorldSize => ExplorationChunkSizeMeters;

        /// <inheritdoc />
        public int SavePriority => 21;

        /// <inheritdoc />
        public int LoadPriority => 21;

        private void Awake()
        {
            PlayerExplorationTracker registered = GlobalRegistry.PlayerExploration;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(this);
                return;
            }

            movementSampleDistance = math.max(0.25f, movementSampleDistance);
            InitializeExplorationMask();
        }

        private void OnEnable()
        {
            InitializeExplorationMask();
            TryRegisterService();
            TryRegisterWithTickManager();
            TryRegisterWithSlowTickManager();
            TryRegisterCartographyDispatcher();
            TryRegisterWithSaveManager();
            TryRegisterSignalListeners();
            MapMagicBiomeEvents.Register(this);
            ResolvePlayerTransform(force: true);
        }

        private void Start()
        {
            InitializeExplorationMask();
            TryRegisterWithTickManager();
            TryRegisterWithSlowTickManager();
            TryRegisterCartographyDispatcher();
            TryRegisterWithSaveManager();
            TryRegisterSignalListeners();
            ResolvePlayerTransform(force: true);
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
            CompleteCartographyUploadJobForTeardown();
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
            CompleteCartographyUploadJobForTeardown();
            TryUnregisterService();
            DisposeExplorationMask();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition currentAup))
                return;

            float requiredDistance = movementSampleDistance;
            double requiredDistanceSq = (double)requiredDistance * requiredDistance;
            if (_hasLastSampledAup &&
                AbsoluteUniversePosition.DistanceSq(in currentAup, in _lastSampledAup) < requiredDistanceSq)
            {
                return;
            }

            _lastSampledAup = currentAup;
            _hasLastSampledAup = true;
            SampleCurrentChunk(force: false, in currentAup);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            InitializeExplorationMask();
            if (_cartographyUploadPending)
            {
                if (!TryResolveCartographyBuffers(out CartographyVaultBuffers uploadBuffers) ||
                    !TryFinalizeCartographyUpload(uploadBuffers, out _, out _, out _))
                {
                    return;
                }
            }

            if (_cartographyDispatcherRegistered)
                return;

            CartographyAup playerCartographyAup = default;
            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            int revealedSignalCount = 0;
            int revealedPoiCount = 0;
            bool changed = false;
            if (hasPlayerAup)
            {
                playerCartographyAup = ToCartographyAup(in playerAup);
                if (!CartographyGridMath.IsFinite(in playerCartographyAup))
                {
                    DumpCartographyBlackBox();
                    return;
                }

                changed |= RevealCartographyCell(in playerCartographyAup, MapRevealSignalFlags.Player);
            }

            revealedSignalCount = DrainMapRevealSignals(out bool signalChanged);
            changed |= signalChanged;

            revealedPoiCount = InjectPoiReveals(out bool poiChanged);
            changed |= poiChanged;

            if (changed)
                _cartographyRevision++;

            RecordCartographyBlackBox(in playerCartographyAup, revealedSignalCount, revealedPoiCount, hasPlayerAup ? 1u : 0u);
        }

        private void CartographyPreSimulationTick(in DispatcherTimingDTO timing)
        {
            InitializeExplorationMask();
            _cartographyDispatcherFrameScheduled = false;
            _cartographyDispatcherPendingSignalCount = 0;
            _cartographyDispatcherPoiCount = 0;
            _cartographyDispatcherHasPlayerAup = false;
            _cartographyDispatcherPlayerAup = default;

            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) ||
                !buffers.Counters.IsCreated ||
                buffers.Counters.Length == 0)
            {
                return;
            }

            if (_cartographyUploadPending &&
                !TryFinalizeCartographyUpload(buffers, out _, out _, out _))
            {
                return;
            }

            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                CartographyAup playerCartographyAup = ToCartographyAup(in playerAup);
                if (CartographyGridMath.IsFinite(in playerCartographyAup))
                {
                    _cartographyDispatcherPlayerAup = playerCartographyAup;
                    _cartographyDispatcherHasPlayerAup = true;
                }
                else
                {
                    DumpCartographyBlackBox();
                }
            }

            CartographyTuningDTO tuning = ResolveCartographyTuning();
            uint frame = _cartographyFrameIndex;
            if (frame >= _nextPoiRevealFrame)
            {
                _cartographyDispatcherPoiCount = AppendPoiRevealSignals(in buffers, in tuning);
                _nextPoiRevealFrame = frame + (uint)ResolvePoiRevealIntervalFrames(tuning.GlobalQualityWeight);
            }

            _cartographyDispatcherPendingSignalCount = StagePendingMapRevealSignals(buffers);
        }

        private JobHandle ScheduleCartographySimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) ||
                !buffers.Counters.IsCreated ||
                buffers.Counters.Length == 0)
            {
                return dependsOn;
            }

            CartographyTuningDTO tuning = ResolveCartographyTuning();
            _cartographyDispatcherFrameScheduled = _cartographyDispatcherHasPlayerAup ||
                                                   _cartographyDispatcherPendingSignalCount > 0;
            if (!_cartographyDispatcherFrameScheduled)
                return dependsOn;

            ApplyCartographyFrameDiscoveryJob job = new ApplyCartographyFrameDiscoveryJob
            {
                DiscoveredSectors = buffers.DiscoveryWords,
                SurfaceMaskWords = buffers.SurfaceMaskWords,
                PendingSignals = buffers.PendingPings,
                Counters = buffers.Counters,
                PlayerAup = _cartographyDispatcherPlayerAup,
                SurfaceThicknessMeters = tuning.SurfaceThicknessMeters,
                GlobalQualityWeight = tuning.GlobalQualityWeight,
                HasPlayerAup = _cartographyDispatcherHasPlayerAup ? 1 : 0,
                PendingSignalCount = _cartographyDispatcherPendingSignalCount,
                WordOffset = 0
            };
            return job.Schedule(dependsOn);
        }

        private void CartographyPostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!_cartographyDispatcherFrameScheduled)
                return;

            if (TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) &&
                buffers.Counters.IsCreated &&
                buffers.Counters.Length > 0)
            {
                CartographyCounterDTO counter = buffers.Counters[0];
                if (counter.Changed != 0)
                {
                    _lastCartographyBitIndex = counter.LastBitIndex == uint.MaxValue ? -1 : (int)counter.LastBitIndex;
                    _cartographyRevision++;
                }
            }

            uint stateFlags = _cartographyDispatcherHasPlayerAup ? 1u : 0u;
            stateFlags |= 1u << 1;
            int explicitSignalCount = math.max(0, _cartographyDispatcherPendingSignalCount - _cartographyDispatcherPoiCount);
            RecordCartographyBlackBox(
                in _cartographyDispatcherPlayerAup,
                explicitSignalCount,
                _cartographyDispatcherPoiCount,
                stateFlags);
            _cartographyDispatcherFrameScheduled = false;
        }

        private int AppendPoiRevealSignals(in CartographyVaultBuffers buffers, in CartographyTuningDTO tuning)
        {
            int appended = 0;
            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
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

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (persistentWorldRegistry != null && appended < CartographyGridConstants.MaxPoiRevealPerSlowTick)
            {
                NativeArray<PersistentWorldDeltaRecord> persistentDeltas = persistentWorldRegistry.GetSaveSnapshotArray();
                int chunkSizeMeters = math.max(1, persistentWorldRegistry.ChunkSizeMeters);
                for (int i = 0;
                     persistentDeltas.IsCreated &&
                     i < persistentDeltas.Length &&
                     appended < CartographyGridConstants.MaxPoiRevealPerSlowTick;
                     i++)
                {
                    PersistentWorldDeltaRecord delta = persistentDeltas[i];
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
            InitializeExplorationMask();
            return TryEncodeBitIndex(chunkX, 0, chunkY, out int bitIndex) && _exploredChunkMask.IsSet(bitIndex);
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
            InitializeExplorationMask();
            if (buffer == null || buffer.Length == 0 || _exploredBitIndices.Length == 0)
                return 0;

            int count = math.min(buffer.Length, _exploredBitIndices.Length);
            for (int i = 0; i < count; i++)
            {
                DecodeBitIndex(_exploredBitIndices[i], out int chunkX, out _, out int chunkZ);
                buffer[i] = new Vector2Int(chunkX, chunkZ);
            }

            return count;
        }

        internal int CopyExploredChunkKeys(long[] buffer)
        {
            InitializeExplorationMask();
            if (buffer == null || buffer.Length == 0 || _exploredBitIndices.Length == 0)
                return 0;

            int count = math.min(buffer.Length, _exploredBitIndices.Length);
            for (int i = 0; i < count; i++)
            {
                DecodeBitIndex(_exploredBitIndices[i], out int chunkX, out int chunkY, out int chunkZ);
                buffer[i] = PDAKeyUtility.TryPackMortonChunkKey(chunkX, chunkY, chunkZ, out long key) ? key : 0L;
            }

            return count;
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

            NativeArray<ulong> maskWords = _exploredChunkMask.AsNativeArray<ulong>();
            int wordCount = math.min(maskWords.Length, MaskWordCount);
            int byteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(ResolveSerializedByteCount(maskWords, wordCount));
            data.explorationMap.exploredMortonByteCount = byteCount;
            Array.Clear(data.explorationMap.exploredMortonMaskBytes, 0, data.explorationMap.exploredMortonMaskBytes.Length);
            unsafe
            {
                fixed (byte* destination = data.explorationMap.exploredMortonMaskBytes)
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(maskWords);
                    if (byteCount > 0)
                    {
                        int destinationBytes = data.explorationMap.exploredMortonMaskBytes.Length;
                        if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, byteCount))
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerExplorationTracker));
                    }
                }
            }

            for (int i = 0; i < wordCount; i++)
            {
                long word = unchecked((long)maskWords[i]);
                _saveMaskWordBuffer[i] = word;
                data.explorationMap.exploredMortonMaskWords[i] = word;
            }

            for (int i = wordCount; i < MaskWordCount; i++)
            {
                _saveMaskWordBuffer[i] = 0L;
                data.explorationMap.exploredMortonMaskWords[i] = 0L;
            }

            data.explorationMap.exploredMortonWordCount = wordCount;
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

            if (_exploredChunkMask.IsSet(bitIndex))
                return false;

            _exploredChunkMask.Set(bitIndex, true);
            TryAppendExploredBitIndex(bitIndex);
            _lastBitIndex = bitIndex;
            if (raiseEvent)
            {
                PDAEvents.RaiseMapChunkExplored(chunkX, chunkZ);
                ChunkExplored?.Invoke(new Vector2Int(chunkX, chunkZ));
            }
            return true;
        }

        /// <summary>
        /// Exposes the dense Morton exploration mask for headless PDA cartography jobs.
        /// </summary>
        public bool TryGetExplorationMaskPayload(
            out NativeArray<ulong>.ReadOnly maskWords,
            out int axisLength,
            out int originOffset,
            out int chunkSizeMeters)
        {
            InitializeExplorationMask();
            NativeArray<ulong> ownerMaskWords = _exploredChunkMask.AsNativeArray<ulong>();
            maskWords = ownerMaskWords.IsCreated ? ownerMaskWords.AsReadOnly() : default;
            axisLength = MaskAxisLength;
            originOffset = MaskOriginOffset;
            chunkSizeMeters = ExplorationChunkSizeMeters;
            return ownerMaskWords.IsCreated;
        }

        public bool TryGetDiscoveredSectorsPayload(
            out NativeArray<ulong> discoveredSectors,
            out int axisLength,
            out int originOffset,
            out int cellSizeMeters,
            out uint revision)
        {
            InitializeExplorationMask();
            bool resolved = TryResolveCartographyBuffers(out CartographyVaultBuffers buffers);
            discoveredSectors = resolved ? buffers.DiscoveryWords : default;
            axisLength = CartographyGridConstants.AxisLength;
            originOffset = CartographyGridConstants.OriginOffset;
            cellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            revision = _cartographyRevision;
            return resolved && discoveredSectors.IsCreated;
        }

        public bool EnqueueMapReveal(in MapRevealSignal signal)
        {
            InitializeExplorationMask();
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) ||
                !buffers.MockPings.IsCreated ||
                !buffers.PendingSignalCounts.IsCreated ||
                !buffers.Counters.IsCreated ||
                buffers.Counters.Length == 0)
            {
                return false;
            }

            return TryAppendMapRevealSignal(buffers, in signal);
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

        private void PopulateCartographySaveData(SaveData data)
        {
            data.explorationMap.cartographyCellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            data.explorationMap.cartographyMaskAxisBits = CartographyGridConstants.AxisBits;
            data.explorationMap.cartographyMaskOriginOffset = CartographyGridConstants.OriginOffset;

            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
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
                !TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
            {
                return false;
            }

            NativeArray<ulong> discoveredWords = buffers.DiscoveryWords;
            int wordCount = math.min(math.min(CartographyGridConstants.WordCount, dto.discoveredSectorMaskWords.Length), dto.discoveredSectorWordCount);
            for (int i = 0; i < wordCount; i++)
                discoveredWords[i] = unchecked((ulong)dto.discoveredSectorMaskWords[i]);

            for (int i = wordCount; i < CartographyGridConstants.WordCount; i++)
                discoveredWords[i] = 0UL;

            SetCartographyTotal(buffers.Counters, discoveredWords);
            return true;
        }

        private bool TryLoadCartographyByteMask(ExplorationMapDTO dto)
        {
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
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

        private void InitializeExplorationMask()
        {
            if (_explorationMaskInitialized)
                return;

            // COLD ALLOC: NativeBitArray[2097152 bits / 262144 bytes] — dense Morton exploration mask — owner: PlayerExplorationTracker
            _exploredChunkMask = new NativeBitArray(MaskBitCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<int>[ExplorationMapDTO.MaxExploredChunks] — explored bit-index enumeration cache — owner: PlayerExplorationTracker
            _exploredBitIndices = new NativeList<int>(ExplorationMapDTO.MaxExploredChunks, DataVaultExemptSceneScratchAllocator);
            NativeMemorySentinel.RegisterNativeArray(
                _exploredChunkMask.AsNativeArray<ulong>(),
                NativeMemoryOwner,
                nameof(_exploredChunkMask),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(
                _exploredBitIndices,
                NativeMemoryOwner,
                nameof(_exploredBitIndices),
                NativeMemoryLifetime);
            TryResolveCartographyBuffers(out _);
            _explorationMaskInitialized = true;
        }

        private void DisposeExplorationMask()
        {
            if (_exploredChunkMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_exploredChunkMask.AsNativeArray<ulong>());
                _exploredChunkMask.Dispose();
            }

            if (_exploredBitIndices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_exploredBitIndices));
                _exploredBitIndices.Dispose();
            }

            _cartographyVault = null;
            _cartographyHandles = default;
            _cartographyVaultReady = false;
            _explorationMaskInitialized = false;
        }

        private void ClearExplorationMask()
        {
            _exploredChunkMask.Clear();
            _exploredBitIndices.Clear();
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

        private static void ExecuteClearCartographyRevealSignals(NativeArray<MapRevealSignal> buffer)
        {
            if (!buffer.IsCreated)
                return;

            ClearCartographyRevealSignalBufferJob job = new ClearCartographyRevealSignalBufferJob { Buffer = buffer };
            for (int i = 0; i < buffer.Length; i++)
                job.Execute(i);
        }

        private bool TryFinalizeCartographyUpload(
            CartographyVaultBuffers buffers,
            out NativeArray<uint> packedR8,
            out uint revision,
            out int framesBetweenUploads)
        {
            packedR8 = default;
            revision = _cartographyUploadPendingRevision;
            framesBetweenUploads = math.max(1, _cartographyUploadPendingCadence);
            if (!_cartographyUploadPending ||
                !DispatcherJobFence.TryFinalizeCompleted(ref _cartographyUploadHandle))
            {
                return false;
            }

            MarkCartographyUploadJobCompleted();
            packedR8 = buffers.UploadPackedR8;
            return packedR8.IsCreated;
        }

        private void CompleteCartographyUploadJobForTeardown()
        {
            CompleteCartographyUploadJobBlocking();
        }

        private bool CompleteCartographyUploadJobForStructuralMutation()
        {
            return CompleteCartographyUploadJobBlocking();
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

        private void MarkCartographyUploadJobCompleted()
        {
            _cartographyUploadPending = false;
            H8Memory.RegisterActiveJob(SystemID.UI, default);
        }

        private void ClearDiscoveredSectors()
        {
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return;

            // [BLOCKING_SYNC_POINT] Structural mutation of the same Vault buffers must drain the upload writer first.
            if (!CompleteCartographyUploadJobForStructuralMutation())
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
                    buffers.Counters[i] = counter;
                }
            }

            _lastCartographyBitIndex = -1;
            _cartographyRevision++;
        }

        private bool TryLoadDenseMask(ExplorationMapDTO dto)
        {
            if (dto.exploredMortonMaskWords == null ||
                dto.exploredMortonMaskWords.Length == 0 ||
                dto.exploredMortonWordCount <= 0)
            {
                return false;
            }

            NativeArray<ulong> maskWords = _exploredChunkMask.AsNativeArray<ulong>();
            int wordCount = math.min(math.min(maskWords.Length, dto.exploredMortonMaskWords.Length), dto.exploredMortonWordCount);
            for (int i = 0; i < wordCount; i++)
                maskWords[i] = unchecked((ulong)dto.exploredMortonMaskWords[i]);

            for (int i = wordCount; i < maskWords.Length; i++)
                maskWords[i] = 0UL;

            RebuildExploredBitIndexCache(maskWords);
            return true;
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

            NativeArray<ulong> maskWords = _exploredChunkMask.AsNativeArray<ulong>();
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

            RebuildExploredBitIndexCache(maskWords);
            return true;
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

        private void RebuildExploredBitIndexCache(NativeArray<ulong> maskWords)
        {
            _exploredBitIndices.Clear();
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
                        TryAppendExploredBitIndex(bitIndex);
                }
            }
        }

        private bool TryAppendExploredBitIndex(int bitIndex)
        {
            if (!_exploredBitIndices.IsCreated ||
                (uint)bitIndex >= (uint)TotalChunkCapacity ||
                _exploredBitIndices.Length >= _exploredBitIndices.Capacity)
            {
                return false;
            }

            _exploredBitIndices.AddNoResize(bitIndex);
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

        private bool ResolvePlayerTransform(bool force)
        {
            if (!force && _playerMovement != null)
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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
            if (TryResolvePlayerAupFromContext(out playerAup))
                return true;

            if (!ResolvePlayerTransform(force: false))
                return false;

            if (_playerMovement == null)
                return false;

            playerAup = _playerMovement.CurrentAup;
            return true;
        }

        private static bool TryResolvePlayerAupFromContext(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || playerContext.PlayerMovement == null)
                return false;

            playerAup = playerContext.PlayerMovement.CurrentAup;
            return true;
        }

        private static bool TryResolveAupFromRuntimePosition(Vector3 runtimePosition, out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            float3 numericPosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(numericPosition)))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            playerAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return playerAup.IsFinite();
        }

        private bool TryResolveCartographyBuffers(out CartographyVaultBuffers buffers)
        {
            buffers = default;
            if (!_cartographyVaultReady && !EnsureCartographyVault())
                return false;

            IDataVault vault = _cartographyVault;
            if (vault == null)
                return false;

            if (CartographyVault.TryResolveViews(vault, ref _cartographyHandles, out buffers))
                return true;

            _cartographyVaultReady = false;
            _cartographyHandles = default;
            return EnsureCartographyVault() &&
                   CartographyVault.TryResolveViews(_cartographyVault, ref _cartographyHandles, out buffers);
        }

        private bool EnsureCartographyVault()
        {
            if (_cartographyVaultReady && _cartographyVault != null && _cartographyHandles.IsCreated())
                return true;

            IDataVault vault = GlobalRegistry.DataVault;

            if (vault == null || !CartographyVault.TryResolve(vault, out _cartographyHandles))
                return false;

            if (!CartographyVault.TryResolveViews(vault, ref _cartographyHandles, out CartographyVaultBuffers buffers))
                return false;

            if (!CartographyLayoutVerifier.ValidateRuntimeLayouts())
                return false;

            _cartographyVault = vault;
            InitializeCartographyVaultBuffers(buffers);
            _cartographyVaultReady = true;
            return true;
        }

        private void InitializeCartographyVaultBuffers(CartographyVaultBuffers buffers)
        {
            // [BLOCKING_SYNC_POINT] Initial buffer clear cannot race the upload writer.
            if (!CompleteCartographyUploadJobForStructuralMutation())
                return;

            float globalQualityWeight = ResolveHomeostasisQualityWeight();
            ExecuteClearCartographyUlongBuffer(buffers.DiscoveryWords);
            ExecuteClearCartographyUlongBuffer(buffers.SurfaceMaskWords);
            ExecuteClearCartographyUlongBuffer(buffers.RollbackSnapshotWords);
            ExecuteClearCartographyUintBuffer(buffers.UploadPackedR8);
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
                GlobalQualityWeight = globalQualityWeight
            };
            initializeJob.Execute();

            CartographyTuningDTO tuning = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0
                ? buffers.Tuning[0]
                : CartographyVault.BuildDefaultTuning(globalQualityWeight);
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
            if (TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) &&
                buffers.Tuning.IsCreated &&
                buffers.Tuning.Length > 0)
            {
                CartographyTuningDTO tuning = buffers.Tuning[0];
                tuning.GlobalQualityWeight = ResolveEffectiveCartographyQuality(tuning.GlobalQualityWeight);
                tuning.UploadCadenceFrames = CartographyGridMath.ResolveUploadIntervalFrames(tuning.GlobalQualityWeight);
                return tuning;
            }

            return CartographyVault.BuildDefaultTuning(ResolveHomeostasisQualityWeight());
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
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
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

        private int DrainMapRevealSignals(out bool changed)
        {
            changed = false;
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) ||
                !buffers.MockPings.IsCreated ||
                !buffers.PendingSignalCounts.IsCreated ||
                buffers.PendingSignalCounts.Length == 0 ||
                !buffers.Counters.IsCreated ||
                buffers.Counters.Length == 0)
            {
                return 0;
            }

            ResetCartographyCounter(buffers.Counters);
            CartographyTuningDTO tuning = ResolveCartographyTuning();
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
                    DumpCartographyBlackBox();
                    processed++;
                    continue;
                }

                float radius = ClampRevealRadius(signal.RadiusMeters);
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
                    UseSdfSurfaceMask = 1,
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
            return processed;
        }

        private int InjectPoiReveals(out bool changed)
        {
            changed = false;
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return 0;

            ResetCartographyCounter(buffers.Counters);
            CartographyTuningDTO tuning = ResolveCartographyTuning();
            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
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

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (persistentWorldRegistry != null && count < CartographyGridConstants.MaxPoiRevealPerSlowTick)
            {
                NativeArray<PersistentWorldDeltaRecord> persistentDeltas = persistentWorldRegistry.GetSaveSnapshotArray();
                int chunkSizeMeters = math.max(1, persistentWorldRegistry.ChunkSizeMeters);
                for (int i = 0;
                     persistentDeltas.IsCreated &&
                     i < persistentDeltas.Length &&
                     count < CartographyGridConstants.MaxPoiRevealPerSlowTick;
                     i++)
                {
                    PersistentWorldDeltaRecord delta = persistentDeltas[i];
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

        private static float ClampRevealRadius(float radiusMeters)
        {
            if (!math.isfinite(radiusMeters))
                return CartographyGridConstants.MacroCellSizeMeters;

            return math.clamp(
                radiusMeters,
                CartographyGridConstants.MacroCellSizeMeters,
                CartographyGridConstants.MaxRevealRadiusMeters);
        }

        private void RecordCartographyBlackBox(in CartographyAup playerAup, int signalCount, int poiCount, uint stateFlags)
        {
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return;

            CartographyTuningDTO tuning = ResolveCartographyTuning();
            RecordCartographyTelemetryJob telemetryJob = new RecordCartographyTelemetryJob
            {
                TelemetryRing = buffers.TelemetryRing,
                TelemetryCursor = buffers.TelemetryCursor,
                Counters = buffers.Counters,
                PlayerAup = playerAup,
                FrameIndex = _cartographyFrameIndex++,
                Revision = _cartographyRevision,
                RevealedSignalCount = signalCount,
                RevealedPoiCount = poiCount,
                StateFlags = stateFlags,
                GlobalQualityWeight = tuning.GlobalQualityWeight
            };
            telemetryJob.Execute();
        }

        private void DumpCartographyBlackBox()
        {
            if (_cartographyDumpedThisSession ||
                !TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return;

            _cartographyDumpedThisSession = true;
            CartographyVault.TryDumpBlackBox(in buffers, Application.dataPath + "/..");
        }

        public bool GenerateMockExplorationData()
        {
            InitializeExplorationMask();
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return false;

            CartographyTuningDTO tuning = ResolveCartographyTuning();
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
            TryPrepareCartographyUpload(tuning.GlobalQualityWeight, out _, out _, out _);
            return true;
        }

        public bool TryPrepareCartographyUpload(
            float globalQualityWeight,
            out NativeArray<uint> packedR8,
            out int framesBetweenUploads,
            out uint revision)
        {
            InitializeExplorationMask();
            packedR8 = default;
            framesBetweenUploads = CartographyGridMath.ResolveUploadIntervalFrames(globalQualityWeight);
            revision = _cartographyRevision;
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return false;

            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            framesBetweenUploads = CartographyGridMath.ResolveUploadIntervalFrames(quality);
            if (TryFinalizeCartographyUpload(buffers, out packedR8, out revision, out framesBetweenUploads))
                return true;

            if (_cartographyUploadPending)
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
            return TryFinalizeCartographyUpload(buffers, out packedR8, out revision, out framesBetweenUploads);
        }

        public bool TryBuildCartographyRleRuns(out NativeArray<CartographyRleRunDTO>.ReadOnly runs, out int runCount)
        {
            InitializeExplorationMask();
            runs = default;
            runCount = 0;
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
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

            runs = buffers.RleRuns.IsCreated ? buffers.RleRuns.AsReadOnly() : default;
            runCount = buffers.Counters.IsCreated && buffers.Counters.Length > 0
                ? math.clamp(buffers.Counters[0].DiscoveredDelta, 0, buffers.RleRuns.Length)
                : 0;
            return buffers.RleRuns.IsCreated;
        }

        public bool TryGetCartographyTuning(out CartographyTuningDTO tuning)
        {
            InitializeExplorationMask();
            tuning = default;
            return TryResolveCartographyBuffers(out _) &&
                   CartographyVault.TryGetTuning(_cartographyVault, ref _cartographyHandles, out tuning);
        }

        public bool TrySetCartographyTuning(in CartographyTuningDTO tuning)
        {
            InitializeExplorationMask();
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers) ||
                !CartographyVault.TrySetTuning(_cartographyVault, ref _cartographyHandles, in tuning))
            {
                return false;
            }

            CartographyTuningDTO sanitized = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0
                ? buffers.Tuning[0]
                : CartographyVault.BuildDefaultTuning(ResolveEffectiveCartographyQuality(tuning.GlobalQualityWeight));
            BuildMockSurfaceMaskJob surfaceMaskJob = new BuildMockSurfaceMaskJob
            {
                SurfaceMaskWords = buffers.SurfaceMaskWords,
                SurfaceThicknessMeters = sanitized.SurfaceThicknessMeters,
                GlobalQualityWeight = ResolveEffectiveCartographyQuality(sanitized.GlobalQualityWeight)
            };
            for (int i = 0; i < buffers.SurfaceMaskWords.Length; i++)
                surfaceMaskJob.Execute(i);
            _cartographyRevision++;
            return true;
        }

        public bool TryLoadScannerProfilesCsvForEditor(string projectRoot, out int appliedRows)
        {
            InitializeExplorationMask();
            appliedRows = 0;
            if (!TryResolveCartographyBuffers(out _))
                return false;

            bool loaded = CartographyVault.TryLoadScannerProfilesCsvForEditor(
                _cartographyVault,
                ref _cartographyHandles,
                projectRoot,
                out appliedRows);
            if (loaded)
                _cartographyRevision++;
            return loaded;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryResolveCartographyBuffers(out CartographyVaultBuffers buffers))
                return;

            CartographyAup centerAup;
            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                centerAup = ToCartographyAup(in playerAup);
            else
                return;

            if (!CartographyGridMath.TryResolveMacroCell(in centerAup, out int3 centerCell))
                return;

            BuildCartographyDebugVoxelsJob debugJob = new BuildCartographyDebugVoxelsJob
            {
                DiscoveryWords = buffers.DiscoveryWords,
                DebugVoxels = buffers.DebugVoxels,
                Counters = buffers.Counters,
                CenterMacroCell = centerCell,
                RadiusCells = 4,
                WordOffset = 0
            };
            debugJob.Execute();

            int count = buffers.Counters.IsCreated && buffers.Counters.Length > 0
                ? math.clamp(buffers.Counters[0].DiscoveredDelta, 0, buffers.DebugVoxels.Length)
                : 0;
            Vector3 origin = playerTransform != null ? playerTransform.position : transform.position;
            float scale = CartographyGridConstants.MacroCellSizeMeters * 0.02f;
            Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.55f);
            for (int i = 0; i < count; i++)
            {
                CartographyDebugVoxelDTO voxel = buffers.DebugVoxels[i];
                Vector3 local = new Vector3(
                    (voxel.X - centerCell.x) * scale,
                    (voxel.Y - centerCell.y) * scale,
                    (voxel.Z - centerCell.z) * scale);
                Gizmos.DrawWireCube(origin + local, Vector3.one * (scale * 0.85f));
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

        void IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent pingEvent)
        {
            if (!TryResolveAupFromRuntimePosition(pingEvent.RuntimePosition, out AbsoluteUniversePosition pingAup))
                return;

            MapRevealSignal signal = new MapRevealSignal
            {
                Center = ToCartographyAup(in pingAup),
                RadiusMeters = ClampRevealRadius(pingEvent.RadiusMeters),
                SourceId = unchecked((uint)math.max(0, pingEvent.SourceSpeciesId)),
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

            HectonDiscoveryManager discoveryManager = GlobalRegistry.Discovery;
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
                GlobalRegistry.TryRegisterDispatcherSystem(_cartographyPostSimulationPhase);
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

            // COLD ALLOC: IDispatcherSystem[3] — Kahn dispatcher phase adapters — owner: PlayerExplorationTracker
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
        }

        private void UnregisterCartographyDispatcher()
        {
            if (_cartographyPreSimulationPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographyPreSimulationPhase);
            if (_cartographySimulationPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographySimulationPhase);
            if (_cartographyPostSimulationPhase != null)
                GlobalRegistry.UnregisterDispatcherSystem(_cartographyPostSimulationPhase);

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
                PhysicsEventBus.Register((IAcousticPingEventListener)this);
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
                PhysicsEventBus.Unregister((IAcousticPingEventListener)this);
                _registeredToAcousticEvents = false;
            }

            if (_registeredToSonarEvents)
            {
                SpectrumEvents.UnregisterSonarPingListener(this);
                _registeredToSonarEvents = false;
            }
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredToSave = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PlayerExplorationTracker registered = GlobalRegistry.PlayerExploration;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(this);
                return;
            }

            GlobalRegistry.RegisterPlayerExplorationRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PlayerExploration, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPlayerExplorationRuntime(this);
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
