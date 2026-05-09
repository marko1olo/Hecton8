using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Tracks player movement across a dense 16m Morton-ordered exploration mask for PDA fog-of-war queries.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/Player Exploration Tracker")]
    public sealed class PlayerExplorationTracker : MonoBehaviour, ITickable, ISaveable, IMapMagicBiomeEventListener
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
        private const int AupCellSizeMeters = 5000;
        private const string NativeMemoryOwner = nameof(PlayerExplorationTracker);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        [Header("References")]
        [Tooltip("Optional explicit player transform. When empty, the tracker resolves the current registry player.")]
        [SerializeField] private Transform playerTransform;

        [Header("Exploration Grid")]
        [Tooltip("Minimum movement distance before the tracker re-evaluates chunk membership.")]
        [SerializeField, Min(0.25f)] private float movementSampleDistance = 4f;
        [Tooltip("When enabled, biome changes from MapMagic automatically feed the discovery registry.")]
        [SerializeField] private bool forwardBiomeDiscovery = true;

        // COLD ALLOC: long[32768] - save DTO word staging for dense Morton exploration mask - owner: PlayerExplorationTracker
        private readonly long[] _saveMaskWordBuffer = new long[MaskWordCount];
        private NativeBitArray _exploredChunkMask;
        private NativeList<int> _exploredBitIndices;
        private bool _registeredToTick;
        private bool _registeredToSave;
        private bool _serviceRegistered;
        private bool _explorationMaskInitialized;
        private AbsoluteUniversePosition _lastSampledAup;
        private HectonPlayerMovement _playerMovement;
        private bool _hasLastSampledAup;
        private int _lastBitIndex = -1;

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
            TryRegisterWithSaveManager();
            MapMagicBiomeEvents.Register(this);
            ResolvePlayerTransform(force: true);
        }

        private void Start()
        {
            InitializeExplorationMask();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            ResolvePlayerTransform(force: true);
            SampleCurrentChunk(force: true);
        }

        private void OnDisable()
        {
            MapMagicBiomeEvents.Unregister(this);
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            MapMagicBiomeEvents.Unregister(this);
            UnregisterFromTickManager();
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

        private void InitializeExplorationMask()
        {
            if (_explorationMaskInitialized)
                return;

            // COLD ALLOC: NativeBitArray[2097152 bits / 262144 bytes] - dense Morton exploration mask - owner: PlayerExplorationTracker
            _exploredChunkMask = new NativeBitArray(MaskBitCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<int>[ExplorationMapDTO.MaxExploredChunks] - explored bit-index enumeration cache - owner: PlayerExplorationTracker
            _exploredBitIndices = new NativeList<int>(ExplorationMapDTO.MaxExploredChunks, Allocator.Persistent);
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

            _explorationMaskInitialized = false;
        }

        private void ClearExplorationMask()
        {
            _exploredChunkMask.Clear();
            _exploredBitIndices.Clear();
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
