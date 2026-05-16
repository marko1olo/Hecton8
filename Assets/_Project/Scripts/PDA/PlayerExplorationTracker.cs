using System;
using System.IO;
using Hecton8.Cartography;
using Hecton8.Core;
using Hecton8.Core.Contracts;
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
        private const string CartographyDumpPath = "Docs/AgentLogs/Dump_CARTOGRAPHY_UX_LEAD.bin";

        [Header("References")]
        [Tooltip("Optional explicit player transform. When empty, the tracker resolves the current registry player.")]
        [SerializeField] private Transform playerTransform;

        [Header("Exploration Grid")]
        [Tooltip("Minimum movement distance before the tracker re-evaluates chunk membership.")]
        [SerializeField, Min(0.25f)] private float movementSampleDistance = 4f;
        [Tooltip("When enabled, biome changes from MapMagic automatically feed the discovery registry.")]
        [SerializeField] private bool forwardBiomeDiscovery = true;

        // COLD ALLOC: long[32768] — save DTO word staging for dense Morton exploration mask — owner: PlayerExplorationTracker
        private readonly long[] _saveMaskWordBuffer = new long[MaskWordCount];
        // COLD ALLOC: PDAMarkerSnapshot[64] — PDA marker POI staging for cartography macro reveal — owner: PlayerExplorationTracker
        private readonly PDAMarkerSnapshot[] _poiMarkerScratch = new PDAMarkerSnapshot[CartographyGridConstants.MaxPoiRevealPerSlowTick];
        private NativeBitArray _exploredChunkMask;
        private NativeList<int> _exploredBitIndices;
        private NativeArray<ulong> _discoveredSectors;
        private NativeArray<CartographyPoiRecord> _poiRecordScratch;
        private NativeArray<int> _cartographyChangeScratch;
        private NativeArray<CartographyBlackBoxEntry> _cartographyBlackBox;
        private NativeQueue<MapRevealSignal> _pendingMapRevealSignals;
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
        private int _cartographyBlackBoxCursor;
        private uint _cartographyRevision;
        private uint _cartographyFrameIndex;
        private bool _cartographyDumpedThisSession;

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
            TryRegisterWithSaveManager();
            TryRegisterSignalListeners();
            ResolvePlayerTransform(force: true);
            SampleCurrentChunk(force: true);
        }

        private void OnDisable()
        {
            MapMagicBiomeEvents.Unregister(this);
            UnregisterSignalListeners();
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            UnregisterFromSaveManager();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            MapMagicBiomeEvents.Unregister(this);
            UnregisterSignalListeners();
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            UnregisterFromSaveManager();
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
            out NativeArray<ulong> maskWords,
            out int axisLength,
            out int originOffset,
            out int chunkSizeMeters)
        {
            InitializeExplorationMask();
            maskWords = _exploredChunkMask.AsNativeArray<ulong>();
            axisLength = MaskAxisLength;
            originOffset = MaskOriginOffset;
            chunkSizeMeters = ExplorationChunkSizeMeters;
            return maskWords.IsCreated;
        }

        public bool TryGetDiscoveredSectorsPayload(
            out NativeArray<ulong> discoveredSectors,
            out int axisLength,
            out int originOffset,
            out int cellSizeMeters,
            out uint revision)
        {
            InitializeExplorationMask();
            discoveredSectors = _discoveredSectors;
            axisLength = CartographyGridConstants.AxisLength;
            originOffset = CartographyGridConstants.OriginOffset;
            cellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            revision = _cartographyRevision;
            return discoveredSectors.IsCreated;
        }

        public bool EnqueueMapReveal(in MapRevealSignal signal)
        {
            InitializeExplorationMask();
            if (!_pendingMapRevealSignals.IsCreated)
                return false;

            MapRevealSignal clampedSignal = signal;
            clampedSignal.RadiusMeters = ClampRevealRadius(signal.RadiusMeters);
            _pendingMapRevealSignals.Enqueue(clampedSignal);
            return true;
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

            int wordCount = math.min(_discoveredSectors.IsCreated ? _discoveredSectors.Length : 0, CartographyGridConstants.WordCount);
            int byteCount = SaveBinaryStorage.AlignExplorationMortonByteCount(ResolveSerializedByteCount(_discoveredSectors, wordCount));
            data.explorationMap.discoveredSectorByteCount = byteCount;
            Array.Clear(data.explorationMap.discoveredSectorMaskBytes, 0, data.explorationMap.discoveredSectorMaskBytes.Length);
            if (byteCount > 0 && _discoveredSectors.IsCreated)
            {
                unsafe
                {
                    fixed (byte* destination = data.explorationMap.discoveredSectorMaskBytes)
                    {
                        void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_discoveredSectors);
                        int destinationBytes = data.explorationMap.discoveredSectorMaskBytes.Length;
                        if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, byteCount))
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerExplorationTracker));
                    }
                }
            }

            for (int i = 0; i < wordCount; i++)
                data.explorationMap.discoveredSectorMaskWords[i] = unchecked((long)_discoveredSectors[i]);

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
                dto.discoveredSectorWordCount <= 0)
            {
                return false;
            }

            int wordCount = math.min(math.min(_discoveredSectors.Length, dto.discoveredSectorMaskWords.Length), dto.discoveredSectorWordCount);
            for (int i = 0; i < wordCount; i++)
                _discoveredSectors[i] = unchecked((ulong)dto.discoveredSectorMaskWords[i]);

            for (int i = wordCount; i < _discoveredSectors.Length; i++)
                _discoveredSectors[i] = 0UL;

            return true;
        }

        private bool TryLoadCartographyByteMask(ExplorationMapDTO dto)
        {
            if (!_discoveredSectors.IsCreated)
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
                void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_discoveredSectors);
                UnsafeUtility.MemClear(destination, _discoveredSectors.Length * sizeof(ulong));
                fixed (byte* source = dto.discoveredSectorMaskBytes)
                {
                    int destinationBytes = _discoveredSectors.Length * sizeof(ulong);
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, byteCount))
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(PlayerExplorationTracker));
                }
            }

            return true;
        }

        private void InitializeExplorationMask()
        {
            if (_explorationMaskInitialized)
                return;

            // COLD ALLOC: NativeBitArray[2097152 bits / 262144 bytes] — dense Morton exploration mask — owner: PlayerExplorationTracker
            _exploredChunkMask = new NativeBitArray(MaskBitCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<int>[ExplorationMapDTO.MaxExploredChunks] — explored bit-index enumeration cache — owner: PlayerExplorationTracker
            _exploredBitIndices = new NativeList<int>(ExplorationMapDTO.MaxExploredChunks, Allocator.Persistent);
            _discoveredSectors = new NativeArray<ulong>(
                CartographyGridConstants.WordCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ulong>[32768] — 1-bit 50m cartography sector mask — owner: PlayerExplorationTracker
            _poiRecordScratch = new NativeArray<CartographyPoiRecord>(
                CartographyGridConstants.MaxPoiRevealPerSlowTick,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CartographyPoiRecord>[64] — POI reveal staging — owner: PlayerExplorationTracker
            _cartographyChangeScratch = new NativeArray<int>(
                1,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] — cartography bit-flip dirty flag — owner: PlayerExplorationTracker
            _cartographyBlackBox = new NativeArray<CartographyBlackBoxEntry>(
                CartographyGridConstants.BlackBoxFrameCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CartographyBlackBoxEntry>[300] — cartography crash telemetry ring — owner: PlayerExplorationTracker
            _pendingMapRevealSignals = new NativeQueue<MapRevealSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<MapRevealSignal>[16 prewarmed] — decoupled map reveal lane — owner: PlayerExplorationTracker
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
            NativeMemorySentinel.RegisterNativeArray(
                _discoveredSectors,
                NativeMemoryOwner,
                nameof(_discoveredSectors),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _poiRecordScratch,
                NativeMemoryOwner,
                nameof(_poiRecordScratch),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _cartographyChangeScratch,
                NativeMemoryOwner,
                nameof(_cartographyChangeScratch),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _cartographyBlackBox,
                NativeMemoryOwner,
                nameof(_cartographyBlackBox),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingMapRevealSignals,
                CartographyGridConstants.MaxRevealSignalsPerSlowTick,
                NativeMemoryOwner,
                nameof(_pendingMapRevealSignals),
                NativeMemoryLifetime);
            PrewarmMapRevealQueue();
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

            if (_discoveredSectors.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_discoveredSectors);
                _discoveredSectors.Dispose();
            }

            if (_poiRecordScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_poiRecordScratch);
                _poiRecordScratch.Dispose();
            }

            if (_cartographyChangeScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_cartographyChangeScratch);
                _cartographyChangeScratch.Dispose();
            }

            if (_cartographyBlackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_cartographyBlackBox);
                _cartographyBlackBox.Dispose();
            }

            if (_pendingMapRevealSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_pendingMapRevealSignals));
                _pendingMapRevealSignals.Dispose();
            }

            _explorationMaskInitialized = false;
        }

        private void ClearExplorationMask()
        {
            _exploredChunkMask.Clear();
            _exploredBitIndices.Clear();
            ClearDiscoveredSectors();
        }

        private void ClearDiscoveredSectors()
        {
            if (!_discoveredSectors.IsCreated)
                return;

            for (int i = 0; i < _discoveredSectors.Length; i++)
                _discoveredSectors[i] = 0UL;
            _lastCartographyBitIndex = -1;
            _cartographyRevision++;
        }

        private void PrewarmMapRevealQueue()
        {
            if (!_pendingMapRevealSignals.IsCreated)
                return;

            for (int i = 0; i < CartographyGridConstants.MaxRevealSignalsPerSlowTick; i++)
                _pendingMapRevealSignals.Enqueue(default);

            while (_pendingMapRevealSignals.TryDequeue(out _))
            {
            }
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

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return true;
        }

        private bool RevealCartographyCell(in CartographyAup cartographyAup, MapRevealSignalFlags flags)
        {
            if (!_discoveredSectors.IsCreated)
                return false;

            if (!CartographyGridMath.TryEncode(
                    in cartographyAup,
                    out int bitIndex,
                    out int wordIndex,
                    out int bitOffset))
            {
                return false;
            }

            ulong before = _discoveredSectors[wordIndex];
            new CartographyRevealAupCellJob
            {
                DiscoveredSectors = _discoveredSectors,
                Center = cartographyAup
            }.Run();

            ulong after = _discoveredSectors[wordIndex];
            _lastCartographyBitIndex = bitIndex;
            return before != after || (flags & MapRevealSignalFlags.Player) == 0;
        }

        private int DrainMapRevealSignals(out bool changed)
        {
            changed = false;
            if (!_pendingMapRevealSignals.IsCreated || !_discoveredSectors.IsCreated)
                return 0;

            bool canTrackChange = _cartographyChangeScratch.IsCreated;
            if (canTrackChange)
                _cartographyChangeScratch[0] = 0;

            int processed = 0;
            while (processed < CartographyGridConstants.MaxRevealSignalsPerSlowTick &&
                   _pendingMapRevealSignals.TryDequeue(out MapRevealSignal signal))
            {
                if (!CartographyGridMath.IsFinite(in signal.Center))
                {
                    DumpCartographyBlackBox();
                    processed++;
                    continue;
                }

                float radius = ClampRevealRadius(signal.RadiusMeters);
                new CartographyRevealSphereJob
                {
                    DiscoveredSectors = _discoveredSectors,
                    Changed = _cartographyChangeScratch,
                    Center = signal.Center,
                    RadiusMeters = radius
                }.Run();
                processed++;
            }

            changed = canTrackChange ? _cartographyChangeScratch[0] != 0 : processed > 0;
            return processed;
        }

        private int InjectPoiReveals(out bool changed)
        {
            changed = false;
            if (!_poiRecordScratch.IsCreated || !_discoveredSectors.IsCreated)
                return 0;

            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
            int markerCount = markerRegistry != null ? markerRegistry.CopyMarkers(_poiMarkerScratch, hudOnly: false) : 0;
            int count = math.min(markerCount, CartographyGridConstants.MaxPoiRevealPerSlowTick);
            for (int i = 0; i < count; i++)
            {
                PDAMarkerSnapshot marker = _poiMarkerScratch[i];
                AbsoluteUniversePosition markerAup = marker.PositionAup;
                _poiRecordScratch[i] = new CartographyPoiRecord
                {
                    Position = ToCartographyAup(in markerAup),
                    Kind = (uint)marker.IconType,
                    Hash = marker.MarkerHashID
                };
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
                    if (!delta.IsValid || delta.IsDeleted)
                        continue;

                    AbsoluteUniversePosition position = delta.UnpackPosition(chunkSizeMeters);
                    _poiRecordScratch[count] = new CartographyPoiRecord
                    {
                        Position = ToCartographyAup(in position),
                        Kind = (uint)MapRevealSignalFlags.Poi,
                        Hash = unchecked((uint)delta.ItemPersistentIdHash)
                    };
                    count++;
                }
            }

            if (count <= 0)
                return 0;

            bool canTrackChange = _cartographyChangeScratch.IsCreated;
            if (canTrackChange)
                _cartographyChangeScratch[0] = 0;

            new CartographyInjectPoiJob
            {
                PoiRecords = _poiRecordScratch,
                DiscoveredSectors = _discoveredSectors,
                Changed = _cartographyChangeScratch,
                Count = count
            }.Run();

            for (int i = 0; i < count; i++)
                _poiRecordScratch[i] = default;

            changed = canTrackChange ? _cartographyChangeScratch[0] != 0 : count > 0;
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
            if (!_cartographyBlackBox.IsCreated || _cartographyBlackBox.Length == 0)
                return;

            int index = _cartographyBlackBoxCursor;
            _cartographyBlackBox[index] = new CartographyBlackBoxEntry
            {
                FrameIndex = _cartographyFrameIndex++,
                Revision = _cartographyRevision,
                LastBitIndex = _lastCartographyBitIndex,
                RevealedSignalCount = signalCount,
                RevealedPoiCount = poiCount,
                StateFlags = stateFlags,
                PlayerAup = playerAup
            };

            _cartographyBlackBoxCursor++;
            if (_cartographyBlackBoxCursor >= _cartographyBlackBox.Length)
                _cartographyBlackBoxCursor = 0;
        }

        private void DumpCartographyBlackBox()
        {
            if (_cartographyDumpedThisSession || !_cartographyBlackBox.IsCreated)
                return;

            _cartographyDumpedThisSession = true;
            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using FileStream stream = new FileStream(CartographyDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(0x43545848u);
                writer.Write(_cartographyRevision);
                writer.Write(_cartographyBlackBoxCursor);
                writer.Write(_cartographyBlackBox.Length);
                for (int i = 0; i < _cartographyBlackBox.Length; i++)
                {
                    CartographyBlackBoxEntry entry = _cartographyBlackBox[i];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.Revision);
                    writer.Write(entry.LastBitIndex);
                    writer.Write(entry.RevealedSignalCount);
                    writer.Write(entry.RevealedPoiCount);
                    writer.Write(entry.StateFlags);
                    writer.Write(entry.PlayerAup.GridX);
                    writer.Write(entry.PlayerAup.GridY);
                    writer.Write(entry.PlayerAup.GridZ);
                    writer.Write(entry.PlayerAup.LocalX);
                    writer.Write(entry.PlayerAup.LocalY);
                    writer.Write(entry.PlayerAup.LocalZ);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementSampleDistance = math.max(0.25f, movementSampleDistance);
        }
#endif
    }
}
