using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
    internal struct AbsoluteUniversePosition
    {
        internal const int CellSizeMeters = 5000;

        public long GridX;
        public long GridY;
        public long GridZ;
        public float LocalX;
        public float LocalY;
        public float LocalZ;

        /// <summary>
        /// Converts the compact save-layout position into a 16-byte-aligned transfer payload for memcpy/blit lanes.
        /// </summary>
        /// <returns>Aligned AUP transfer payload.</returns>
        public AbsoluteUniversePositionBlit128 ToAlignedBlit()
        {
            return new AbsoluteUniversePositionBlit128
            {
                GridX = GridX,
                GridY = GridY,
                GridZ = GridZ,
                Local = new float4(LocalX, LocalY, LocalZ, 0f),
                Reserved = 0UL
            };
        }

        /// <summary>
        /// Reconstructs the compact save-layout AUP from an aligned transfer payload.
        /// </summary>
        /// <param name="aligned">Aligned transfer payload.</param>
        /// <returns>Compact AUP.</returns>
        public static AbsoluteUniversePosition FromAlignedBlit(in AbsoluteUniversePositionBlit128 aligned)
        {
            return new AbsoluteUniversePosition
            {
                GridX = aligned.GridX,
                GridY = aligned.GridY,
                GridZ = aligned.GridZ,
                LocalX = aligned.Local.x,
                LocalY = aligned.Local.y,
                LocalZ = aligned.Local.z
            };
        }

        public static AbsoluteUniversePosition FromRuntimePosition(Vector3 runtimePosition)
        {
            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            return FromAbsolutePosition(new double3(absolutePosition.x, absolutePosition.y, absolutePosition.z));
        }

        public static AbsoluteUniversePosition FromAbsolutePosition(double3 absolutePosition)
        {
            double cellSize = CellSizeMeters;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);

            double originX = gridX * cellSize;
            double originY = gridY * cellSize;
            double originZ = gridZ * cellSize;

            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolutePosition.x - originX),
                LocalY = (float)(absolutePosition.y - originY),
                LocalZ = (float)(absolutePosition.z - originZ)
            };
        }

        public double3 ToAbsoluteDouble3()
        {
            double cellSize = CellSizeMeters;
            return new double3(
                (GridX * cellSize) + LocalX,
                (GridY * cellSize) + LocalY,
                (GridZ * cellSize) + LocalZ);
        }

        public float3 ToRuntimeFloat3()
        {
            Vector3 committedOffset = HectonFloatingOrigin.CurrentTotalOffset;
            double cellSize = CellSizeMeters;
            double runtimeX = ((GridX * cellSize) + LocalX) - committedOffset.x;
            double runtimeY = ((GridY * cellSize) + LocalY) - committedOffset.y;
            double runtimeZ = ((GridZ * cellSize) + LocalZ) - committedOffset.z;
            return new float3((float)runtimeX, (float)runtimeY, (float)runtimeZ);
        }

        public static int3 ResolveChunkId(in AbsoluteUniversePosition position, int chunkSizeMeters)
        {
            double3 absolutePosition = position.ToAbsoluteDouble3();
            double chunkSize = math.max(1, chunkSizeMeters);
            return new int3(
                (int)math.floor(absolutePosition.x / chunkSize),
                (int)math.floor(absolutePosition.y / chunkSize),
                (int)math.floor(absolutePosition.z / chunkSize));
        }

        public static double DistanceSq(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double3 delta = a.ToAbsoluteDouble3() - b.ToAbsoluteDouble3();
            return math.lengthsq(delta);
        }
    }

    /// <summary>
    /// 16-byte-aligned AUP transfer payload for network or memcpy lanes that require float4-friendly packing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 48)]
    internal struct AbsoluteUniversePositionBlit128
    {
        public long GridX;
        public long GridY;
        public long GridZ;
        public float4 Local;
        public ulong Reserved;
    }

    [Flags]
    internal enum PersistentWorldItemFlags : byte
    {
        None = 0,
        Collected = 1 << 0
    }

    [Flags]
    internal enum PoolSlotStateFlags : byte
    {
        None = 0,
        Hydrated = 1 << 0,
        Settled = 1 << 1,
        Dirty = 1 << 2,
        Reserved = 1 << 3,
        HydrationQueued = 1 << 4,
        DehydrationQueued = 1 << 5,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    internal struct PoolSlotData
    {
        public ulong BoundGuid;
        public int3 AupCell;
        public float3 LocalOffset;
        public ushort HydrationFrame;
        public byte RefCount;
        public byte StateFlags;
        public ushort StableFrames;
        public ushort LastVisibleFrame;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct EntityDataRecord
    {
        public AbsoluteUniversePositionBlit128 Position;
        public int Quantity;
        public float Integrity01;
        public int InventoryHash;
        public uint InstanceUid;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 192)]
    internal struct PersistentWorldItemRecord
    {
        private const uint QuantityMask = 0x00FFFFFFu;
        private const int FlagsShift = 24;

        public AbsoluteUniversePosition Position;
        public int3 ChunkId;
        public ulong ItemPersistentIdHash;
        public FixedString128Bytes ItemPersistentId;
        private uint _packedQuantityAndFlags;
        public uint InstanceUid;

        public int Quantity
        {
            get => (int)(_packedQuantityAndFlags & QuantityMask);
            set
            {
                uint clampedQuantity = value <= 0
                    ? 0u
                    : (uint)math.min(value, (int)QuantityMask);
                _packedQuantityAndFlags = (_packedQuantityAndFlags & ~QuantityMask) | clampedQuantity;
            }
        }

        public PersistentWorldItemFlags Flags
        {
            get => (PersistentWorldItemFlags)((_packedQuantityAndFlags >> FlagsShift) & 0xFFu);
            set => _packedQuantityAndFlags = (_packedQuantityAndFlags & QuantityMask) | ((uint)value << FlagsShift);
        }

        public bool IsCollected => (Flags & PersistentWorldItemFlags.Collected) != 0;

        public void MarkCollected()
        {
            Flags |= PersistentWorldItemFlags.Collected;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    internal struct PersistentWorldDeltaRecord
    {
        private const uint PackedAxisMask = 0x3FFu;
        private const float PackedAxisScale = 1023f;

        public int3 ChunkId;
        public ulong ItemPersistentIdHash;
        public uint InstanceUid;
        public uint PackedLocalPosition;
        public ushort Quantity;
        public byte ItemFlags;
        public byte Reserved;

        public bool IsValid => ItemPersistentIdHash != 0UL && InstanceUid != 0u && Quantity > 0;

        public static PersistentWorldDeltaRecord FromRecord(in PersistentWorldItemRecord record, int chunkSizeMeters)
        {
            return new PersistentWorldDeltaRecord
            {
                ChunkId = record.ChunkId,
                ItemPersistentIdHash = record.ItemPersistentIdHash,
                InstanceUid = record.InstanceUid,
                PackedLocalPosition = PackLocalPosition(in record.Position, record.ChunkId, chunkSizeMeters),
                Quantity = (ushort)math.clamp(record.Quantity, 1, ushort.MaxValue),
                ItemFlags = (byte)record.Flags,
                Reserved = 0
            };
        }

        public PersistentWorldItemRecord ToRecord(int chunkSizeMeters)
        {
            AbsoluteUniversePosition position = UnpackPosition(chunkSizeMeters);
            return new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = ChunkId,
                ItemPersistentIdHash = ItemPersistentIdHash,
                ItemPersistentId = default,
                Quantity = math.max(1, Quantity),
                Flags = (PersistentWorldItemFlags)ItemFlags,
                InstanceUid = InstanceUid
            };
        }

        public AbsoluteUniversePosition UnpackPosition(int chunkSizeMeters)
        {
            UnpackLocalPosition(PackedLocalPosition, chunkSizeMeters, out float localX, out float localY, out float localZ);
            double3 chunkOrigin = new double3(
                ChunkId.x * (double)chunkSizeMeters,
                ChunkId.y * (double)chunkSizeMeters,
                ChunkId.z * (double)chunkSizeMeters);

            return AbsoluteUniversePosition.FromAbsolutePosition(chunkOrigin + new double3(localX, localY, localZ));
        }

        private static uint PackLocalPosition(in AbsoluteUniversePosition position, int3 chunkId, int chunkSizeMeters)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            double3 chunkOrigin = new double3(
                chunkId.x * (double)chunkSizeMeters,
                chunkId.y * (double)chunkSizeMeters,
                chunkId.z * (double)chunkSizeMeters);

            double maxLocal = math.max(0d, (double)chunkSizeMeters);
            float localX = (float)math.clamp(absolute.x - chunkOrigin.x, 0d, maxLocal);
            float localY = (float)math.clamp(absolute.y - chunkOrigin.y, 0d, maxLocal);
            float localZ = (float)math.clamp(absolute.z - chunkOrigin.z, 0d, maxLocal);
            float inverseChunkSize = 1f / math.max(1f, chunkSizeMeters);

            uint x = (uint)math.round(math.saturate(localX * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            uint y = (uint)math.round(math.saturate(localY * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            uint z = (uint)math.round(math.saturate(localZ * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            return x | (y << 10) | (z << 20);
        }

        private static void UnpackLocalPosition(uint packed, int chunkSizeMeters, out float localX, out float localY, out float localZ)
        {
            float chunkSize = math.max(1f, chunkSizeMeters);
            localX = ((packed & PackedAxisMask) / PackedAxisScale) * chunkSize;
            localY = (((packed >> 10) & PackedAxisMask) / PackedAxisScale) * chunkSize;
            localZ = (((packed >> 20) & PackedAxisMask) / PackedAxisScale) * chunkSize;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal struct PersistentWorldCompactDeltaRecord
    {
        public uint PackedLocalPosition;
        public uint InstanceUid;
        public ushort Quantity;
        public byte ItemFlags;
        public byte Reserved;
        public ushort ChunkIndex;
        public ushort ItemHashIndex;

        public bool IsValid => InstanceUid != 0u && Quantity > 0;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5850)]
    public sealed class PersistentWorldRegistry : MonoBehaviour, ITickable, ISlowTickable
    {
        private const int DefaultMaxTrackedItems = 16384;
        private const int DefaultChunkSizeMeters = 64;
        private const int DefaultHydrationRadius = 1;
        private const float DropScatterRadiusMeters = 0.55f;
        private const float DropScatterMinLiftMeters = 0.06f;
        private const float DropScatterMaxLiftMeters = 0.22f;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private const int InstanceUidTypeShift = 24;
        private const uint InstanceUidCounterMask = 0x00FFFFFFu;
        private const float HydrateRadiusMeters = 150f;
        private const double HydrateRadiusSq = HydrateRadiusMeters * HydrateRadiusMeters;
        private const float DehydrateRadiusMeters = 160f;
        private const double DehydrateRadiusSq = DehydrateRadiusMeters * DehydrateRadiusMeters;
        private const float HydrationRescanDistanceMeters = 16f;
        private const double HydrationRescanDistanceSq = HydrationRescanDistanceMeters * HydrationRescanDistanceMeters;
        private const int MaxHydrationsPerFrame = 30;
        private const int MaxDehydrationsPerTick = 8;
        private const ulong PoolGuidMixSalt = 11400714819323198485UL;
        private const long PersistentMemoryBudgetBytes = 10485760L;
        private const string MemoryBudgetOwnerName = "PersistentWorldRegistry";
        private static readonly long HydrationFrameBudgetTicks = Math.Max(1L, (long)(Stopwatch.Frequency * 0.0015d));

        private static PersistentWorldRegistry _instance;
        private static int _nextInstanceUidCounter;

        [Header("Settings")]
        [SerializeField, Min(256)]
        [Tooltip("Hard ceiling for tracked dropped-item records. Native containers are pre-allocated to this count and never resized at runtime.")]
        private int maxTrackedItems = DefaultMaxTrackedItems;

        [SerializeField, Min(16)]
        [Tooltip("Chunk edge length in meters used by the dropped-item spatial hash.")]
        private int chunkSizeMeters = DefaultChunkSizeMeters;

        [SerializeField, Range(0, 2)]
        [Tooltip("How many chunk rings around the player stay hydrated as live pooled proxies.")]
        private int hydrationRadiusInChunks = DefaultHydrationRadius;

        [Header("Diagnostics")]
        [SerializeField] private int _debugTrackedRecordCount;
        [SerializeField] private int _debugHydratedRecordCount;
        [SerializeField] private int _debugSnapshotRecordCount;
        [SerializeField] private Vector3Int _debugPlayerChunk;

        private NativeList<PersistentWorldItemRecord> _records;
        private NativeParallelMultiHashMap<int3, int> _recordsByChunk;
        private NativeList<PersistentWorldCompactDeltaRecord> _deltaRecords;
        private NativeHashMap<uint, int> _deltaRecordIndexByEntityId;
        private NativeHashMap<int3, ushort> _deltaChunkIndexByChunkId;
        private NativeList<int3> _deltaChunkIds;
        private NativeHashMap<ulong, ushort> _deltaItemIndexByHash;
        private NativeList<ulong> _deltaItemHashes;
        private NativeParallelMultiHashMap<uint, PersistentWorldCompactDeltaRecord> _deltaRecordsByChunk;
        private NativeList<PersistentWorldDeltaRecord> _saveSnapshotDeltas;
        private NativeArray<PoolSlotData> _poolSlotData;
        private NativeHashMap<ulong, int> _guidToPoolIndex;
        private NativeHashMap<uint, EntityDataRecord> _entityStateByInstanceUid;
        private NativeQueue<int> _dehydrateQueue;
        private NativeList<int> _pendingHydrationRecords;
        private GameObject[] _hydratedInstancesBySlot;
        private Transform[] _poolSlotTransforms;
        private Rigidbody[] _poolSlotRigidbodies;
        private Dictionary<int, GameObject> _hydratedInstancesByRecordIndex;
        private Dictionary<ulong, ItemData> _itemLookupByHash;
        private List<ItemData> _itemCatalogScratch;
        private List<int> _recordIndexScratch;
        private Transform _playerTransform;
        private ItemCatalog _resolvedItemCatalog;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _hydrationSessionRunning;
        private bool _playerChunkValid;
        private bool _hasLastHydrationScanAup;
        private ushort _hydrationFrameCounter;
        private int _pendingHydrationReadIndex;
        private int _hydrationSessionVersion;
        private int3 _currentPlayerChunk;
        private AbsoluteUniversePosition _lastHydrationScanAup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _nextInstanceUidCounter = 0;
        }

        public static PersistentWorldRegistry Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            maxTrackedItems = math.max(256, maxTrackedItems);
            chunkSizeMeters = math.max(16, chunkSizeMeters);
            hydrationRadiusInChunks = math.clamp(hydrationRadiusInChunks, 0, 2);
            _hydrationFrameCounter = 0;

            // COLD ALLOC: NativeList<PersistentWorldItemRecord>[maxTrackedItems] — persistent dropped-item record store — owner: PersistentWorldRegistry
            _records = new NativeList<PersistentWorldItemRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<int3,int>[maxTrackedItems] — dropped-item chunk lookup table — owner: PersistentWorldRegistry
            _recordsByChunk = new NativeParallelMultiHashMap<int3, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldCompactDeltaRecord>[maxTrackedItems] — authoritative 16-byte dropped-item delta store — owner: PersistentWorldRegistry
            _deltaRecords = new NativeList<PersistentWorldCompactDeltaRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,int>[maxTrackedItems] — delta entity-to-index lookup keyed by InstanceUid — owner: PersistentWorldRegistry
            _deltaRecordIndexByEntityId = new NativeHashMap<uint, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<int3,ushort>[maxTrackedItems] — chunk-id to compact delta table index — owner: PersistentWorldRegistry
            _deltaChunkIndexByChunkId = new NativeHashMap<int3, ushort>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<int3>[maxTrackedItems] — compact delta chunk table — owner: PersistentWorldRegistry
            _deltaChunkIds = new NativeList<int3>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<ulong,ushort>[maxTrackedItems] — item-hash to compact delta table index — owner: PersistentWorldRegistry
            _deltaItemIndexByHash = new NativeHashMap<ulong, ushort>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<ulong>[maxTrackedItems] — compact delta item-hash table — owner: PersistentWorldRegistry
            _deltaItemHashes = new NativeList<ulong>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<uint,PersistentWorldCompactDeltaRecord>[maxTrackedItems] — chunk-hash to compact delta lookup — owner: PersistentWorldRegistry
            _deltaRecordsByChunk = new NativeParallelMultiHashMap<uint, PersistentWorldCompactDeltaRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[maxTrackedItems] — immutable save snapshot for background binary writes — owner: PersistentWorldRegistry
            _saveSnapshotDeltas = new NativeList<PersistentWorldDeltaRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeArray<PoolSlotData>[maxTrackedItems] — persistent hydration slot state store — owner: PersistentWorldRegistry
            _poolSlotData = new NativeArray<PoolSlotData>(maxTrackedItems, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeHashMap<ulong,int>[maxTrackedItems] — hydration GUID to slot lookup — owner: PersistentWorldRegistry
            _guidToPoolIndex = new NativeHashMap<ulong, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,EntityDataRecord>[maxTrackedItems] — authoritative dehydration payload store keyed by InstanceUid — owner: PersistentWorldRegistry
            _entityStateByInstanceUid = new NativeHashMap<uint, EntityDataRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeQueue<int>(Persistent) — deferred dehydration queue — owner: PersistentWorldRegistry
            _dehydrateQueue = new NativeQueue<int>(Allocator.Persistent);
            // COLD ALLOC: NativeList<int>[maxTrackedItems] — time-sliced hydration backlog keyed by persistent record index — owner: PersistentWorldRegistry
            _pendingHydrationRecords = new NativeList<int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: GameObject[maxTrackedItems] — hydrated proxy instances by slot — owner: PersistentWorldRegistry
            _hydratedInstancesBySlot = new GameObject[maxTrackedItems];
            // COLD ALLOC: Transform[maxTrackedItems] — hydrated proxy transforms by slot — owner: PersistentWorldRegistry
            _poolSlotTransforms = new Transform[maxTrackedItems];
            // COLD ALLOC: Rigidbody[maxTrackedItems] — hydrated proxy rigidbodies by slot — owner: PersistentWorldRegistry
            _poolSlotRigidbodies = new Rigidbody[maxTrackedItems];
            // COLD ALLOC: Dictionary<int,GameObject>[128] — hydrated world-item proxy lookup — owner: PersistentWorldRegistry
            _hydratedInstancesByRecordIndex = new Dictionary<int, GameObject>(128);
            // COLD ALLOC: Dictionary<ulong,ItemData>[1024] — persistent-id hash to ItemData lookup cache — owner: PersistentWorldRegistry
            _itemLookupByHash = new Dictionary<ulong, ItemData>(1024);
            // COLD ALLOC: List<ItemData>[1024] — item catalog scratch buffer for hash cache rebuilds — owner: PersistentWorldRegistry
            _itemCatalogScratch = new List<ItemData>(1024);
            // COLD ALLOC: List<int>[128] — hydrated record scratch buffer for sync/dehydrate passes — owner: PersistentWorldRegistry
            _recordIndexScratch = new List<int>(128);
            RegisterPersistentMemoryBudget();

            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            TryRegisterRuntimeLoops();
        }

        private void Start()
        {
            TryRegisterRuntimeLoops();
        }

        private void OnDisable()
        {
            CancelHydrationSession(clearQueue: false);
            TryUnregisterRuntimeLoops();
            DehydrateAll(syncTransformsBackToRecords: false);
        }

        private void OnDestroy()
        {
            CancelHydrationSession(clearQueue: false);
            TryUnregisterRuntimeLoops();
            DehydrateAll(syncTransformsBackToRecords: false);

            if (_records.IsCreated)
                _records.Dispose();

            if (_recordsByChunk.IsCreated)
                _recordsByChunk.Dispose();

            if (_deltaRecords.IsCreated)
                _deltaRecords.Dispose();

            if (_deltaRecordIndexByEntityId.IsCreated)
                _deltaRecordIndexByEntityId.Dispose();

            if (_deltaChunkIndexByChunkId.IsCreated)
                _deltaChunkIndexByChunkId.Dispose();

            if (_deltaChunkIds.IsCreated)
                _deltaChunkIds.Dispose();

            if (_deltaItemIndexByHash.IsCreated)
                _deltaItemIndexByHash.Dispose();

            if (_deltaItemHashes.IsCreated)
                _deltaItemHashes.Dispose();

            if (_deltaRecordsByChunk.IsCreated)
                _deltaRecordsByChunk.Dispose();

            if (_saveSnapshotDeltas.IsCreated)
                _saveSnapshotDeltas.Dispose();

            if (_poolSlotData.IsCreated)
                _poolSlotData.Dispose();

            if (_guidToPoolIndex.IsCreated)
                _guidToPoolIndex.Dispose();

            if (_entityStateByInstanceUid.IsCreated)
                _entityStateByInstanceUid.Dispose();

            if (_dehydrateQueue.IsCreated)
                _dehydrateQueue.Dispose();

            if (_pendingHydrationRecords.IsCreated)
                _pendingHydrationRecords.Dispose();

            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
            if (_instance == this)
                _instance = null;
        }

        public void Tick(float dt)
        {
            DrainDehydrateQueue(MaxDehydrationsPerTick);
        }

        public void SlowTick()
        {
            if (!WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform) || _playerTransform == null)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            int3 nextChunk = AbsoluteUniversePosition.ResolveChunkId(in playerAup, chunkSizeMeters);
            bool requiresRescan = !_playerChunkValid || !math.all(nextChunk == _currentPlayerChunk);
            if (!requiresRescan && _hasLastHydrationScanAup)
                requiresRescan = AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastHydrationScanAup) >= HydrationRescanDistanceSq;

            if (!requiresRescan)
                return;

            SyncAllHydratedRecords();
            _currentPlayerChunk = nextChunk;
            _playerChunkValid = true;
            _lastHydrationScanAup = playerAup;
            _hasLastHydrationScanAup = true;
            _hydrationFrameCounter++;
            RefreshHydrationWindow(in playerAup);
            EnsureHydrationSessionScheduled();
            UpdateDiagnostics();
        }

        internal bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)
        {
            if (itemData == null || quantity <= 0 || !_records.IsCreated || _records.Length >= _records.Capacity)
                return false;

            if (string.IsNullOrWhiteSpace(itemData.PersistentId))
                return false;

            if (itemData.worldPrefab == null)
                return false;

            if (!TryGenerateInstanceUid(itemData, ComputePersistentIdHash(itemData.PersistentId), out uint instanceUid))
                return false;

            Vector3 scatteredRuntimePosition = ApplyDeterministicDropScatter(runtimePosition, instanceUid);
            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromRuntimePosition(scatteredRuntimePosition);
            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = ComputePersistentIdHash(itemData.PersistentId),
                ItemPersistentId = new FixedString128Bytes(itemData.PersistentId),
                Quantity = quantity,
                Flags = PersistentWorldItemFlags.None,
                InstanceUid = instanceUid
            };

            int recordIndex = _records.Length;
            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, recordIndex);
            RegisterOrUpdatePoolSlot(recordIndex, in record);
            RegisterOrUpdateEntityState(in record);
            UpsertDeltaRecord(in record);

            if (_hasLastHydrationScanAup && ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                QueueRecordForHydration(recordIndex, in record, in _lastHydrationScanAup);

            UpdateDiagnostics();
            return true;
        }

        internal bool TryRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)
        {
            if (itemHashId == 0 || itemCatalog == null)
                return false;

            ItemData itemData = itemCatalog.FindByHash(itemHashId);
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition);
        }

        private static Vector3 ApplyDeterministicDropScatter(Vector3 runtimePosition, uint instanceUid)
        {
            uint state = instanceUid != 0u ? instanceUid : 0xA511E9B3u;
            float angle = NextScatter01(ref state) * (math.PI * 2f);
            float radius = math.sqrt(NextScatter01(ref state)) * DropScatterRadiusMeters;
            float lift = math.lerp(DropScatterMinLiftMeters, DropScatterMaxLiftMeters, NextScatter01(ref state));

            Vector3 offset;
            offset.x = math.cos(angle) * radius;
            offset.y = lift;
            offset.z = math.sin(angle) * radius;
            return runtimePosition + offset;
        }

        private static float NextScatter01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        internal void MarkRecordCollected(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (record.IsCollected)
                return;

            record.MarkCollected();
            _records[recordIndex] = record;
            DehydrateRecord(recordIndex, syncTransformBackToRecord: false);
            RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
            RemoveEntityState(in record);
            RemoveDeltaRecord(record.InstanceUid);
            UpdateDiagnostics();
        }

        internal void CaptureSaveSnapshot()
        {
            if (!_saveSnapshotDeltas.IsCreated)
                return;

            SyncAllHydratedRecords();
            _saveSnapshotDeltas.Clear();
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                if (!_saveSnapshotDeltas.IsCreated || _saveSnapshotDeltas.Length >= _saveSnapshotDeltas.Capacity)
                    break;

                if (TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    _saveSnapshotDeltas.AddNoResize(expandedRecord);
            }

            UpdateDiagnostics();
        }

        internal NativeArray<PersistentWorldDeltaRecord> GetSaveSnapshotArray()
        {
            return _saveSnapshotDeltas.IsCreated
                ? _saveSnapshotDeltas.AsArray()
                : default;
        }

        internal void RestoreFromLoadedRecords(PersistentWorldDeltaRecord[] loadedRecords)
        {
            CancelHydrationSession(clearQueue: true);
            DehydrateAll(syncTransformsBackToRecords: false);
            _records.Clear();
            _recordsByChunk.Clear();
            _deltaRecords.Clear();
            _deltaRecordIndexByEntityId.Clear();
            _deltaChunkIndexByChunkId.Clear();
            _deltaChunkIds.Clear();
            _deltaItemIndexByHash.Clear();
            _deltaItemHashes.Clear();
            _deltaRecordsByChunk.Clear();
            _saveSnapshotDeltas.Clear();
            _playerChunkValid = false;
            _hasLastHydrationScanAup = false;
            _lastHydrationScanAup = default;
            _currentPlayerChunk = default;
            _hydrationFrameCounter = 0;
            ResetPoolSlots();

            if (loadedRecords != null)
            {
                uint maxObservedInstanceSequence = 0u;
                int restoreCount = math.min(loadedRecords.Length, _records.Capacity);
                for (int i = 0; i < restoreCount; i++)
                {
                    PersistentWorldDeltaRecord deltaRecord = loadedRecords[i];
                    if (!deltaRecord.IsValid)
                        continue;

                    PersistentWorldItemRecord record = deltaRecord.ToRecord(chunkSizeMeters);
                    if (record.IsCollected)
                        continue;

                    if (_deltaRecordIndexByEntityId.ContainsKey(record.InstanceUid))
                        continue;

                    uint observedSequence = record.InstanceUid & InstanceUidCounterMask;
                    if (observedSequence > maxObservedInstanceSequence)
                        maxObservedInstanceSequence = observedSequence;

                    _records.AddNoResize(record);
                    int recordIndex = _records.Length - 1;
                    _recordsByChunk.Add(record.ChunkId, recordIndex);
                    RegisterOrUpdatePoolSlot(recordIndex, in record);
                    RegisterOrUpdateEntityState(in record);
                    if (TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord compactRecord))
                    {
                        _deltaRecordIndexByEntityId.TryAdd(record.InstanceUid, _deltaRecords.Length);
                        _deltaRecords.AddNoResize(compactRecord);
                    }
                }

                RebuildDeltaChunkLookup();
                RebaseInstanceUidCounter(maxObservedInstanceSequence);
            }

            if (WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform) && _playerTransform != null)
            {
                AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
                _currentPlayerChunk = AbsoluteUniversePosition.ResolveChunkId(in playerAup, chunkSizeMeters);
                _playerChunkValid = true;
                _lastHydrationScanAup = playerAup;
                _hasLastHydrationScanAup = true;
                _hydrationFrameCounter++;
                RefreshHydrationWindow(in playerAup);
                EnsureHydrationSessionScheduled();
            }

            UpdateDiagnostics();
        }

        private void TryRegisterRuntimeLoops()
        {
            if (_tickRegistered && _slowTickRegistered)
                return;

            if (!Application.isPlaying)
                return;

            if (!_tickRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = true;
            }

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = true;
            }
        }

        private void TryUnregisterRuntimeLoops()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }
        }

        private void RefreshHydrationWindow(in AbsoluteUniversePosition playerAup)
        {
            _recordIndexScratch.Clear();

            Dictionary<int, GameObject>.Enumerator hydratedEnumerator = _hydratedInstancesByRecordIndex.GetEnumerator();
            while (hydratedEnumerator.MoveNext())
            {
                int recordIndex = hydratedEnumerator.Current.Key;
                if (!IsValidRecordIndex(recordIndex))
                {
                    _recordIndexScratch.Add(recordIndex);
                    continue;
                }

                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.IsCollected || !ShouldKeepHydratedRecord(in record, in playerAup))
                    QueueRecordForDehydration(recordIndex);
            }

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                ClearHydratedSlot(_recordIndexScratch[i]);

            _recordIndexScratch.Clear();

            int radius = ResolveHydrationScanChunkRadius();
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        int3 chunkId = _currentPlayerChunk + new int3(x, y, z);
                        if (!_recordsByChunk.TryGetFirstValue(chunkId, out int recordIndex, out NativeParallelMultiHashMapIterator<int3> iterator))
                            continue;

                        do
                        {
                            if (!IsValidRecordIndex(recordIndex))
                                continue;

                            PersistentWorldItemRecord record = _records[recordIndex];
                            if (record.IsCollected ||
                                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex) ||
                                !ShouldHydrateDehydratedRecord(in record, in playerAup))
                            {
                                continue;
                            }

                            QueueRecordForHydration(recordIndex, in record, in playerAup);
                        }
                        while (_recordsByChunk.TryGetNextValue(out recordIndex, ref iterator));
                    }
                }
            }
        }

        private void SyncAllHydratedRecords()
        {
            _recordIndexScratch.Clear();

            Dictionary<int, GameObject>.Enumerator hydratedEnumerator = _hydratedInstancesByRecordIndex.GetEnumerator();
            while (hydratedEnumerator.MoveNext())
            {
                int recordIndex = hydratedEnumerator.Current.Key;
                GameObject instance = hydratedEnumerator.Current.Value;
                if (instance == null)
                {
                    _recordIndexScratch.Add(recordIndex);
                    continue;
                }

                SyncRecordFromLiveInstance(recordIndex, instance, instance.transform);
            }

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                ClearHydratedSlot(_recordIndexScratch[i]);

            _recordIndexScratch.Clear();
        }

        private bool HydrateRecord(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (record.IsCollected || _hydratedInstancesByRecordIndex.ContainsKey(recordIndex))
                return false;

            if (!TryGetPoolIndex(in record, out int poolIndex))
                return false;

            if (!TryResolveItemData(in record, out ItemData itemData) || itemData == null)
                return false;

            GameObject prefab = itemData.worldPrefab;
            if (prefab == null)
                return false;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return false;

            if (!pool.HasPool(prefab))
                pool.Warmup(prefab, 1);

            EntityDataRecord state = ResolveEntityState(in record);
            AbsoluteUniversePosition hydratedPosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
            int hydratedQuantity = math.max(1, state.Quantity);
            float3 runtimePosition = hydratedPosition.ToRuntimeFloat3();
            GameObject instance = pool.Spawn(prefab, new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z), Quaternion.identity, allowExpand: false);
            if (instance == null)
                return false;

            if (instance.TryGetComponent(out PickupItem pickupItem))
            {
                pickupItem.Configure(itemData, hydratedQuantity);
                pickupItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else if (instance.TryGetComponent(out HectonItem hectonItem))
            {
                hectonItem.SetItemData(itemData, hydratedQuantity);
                hectonItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else
            {
                pool.Despawn(instance);
                return false;
            }

            _hydratedInstancesByRecordIndex[recordIndex] = instance;
            _hydratedInstancesBySlot[poolIndex] = instance;
            _poolSlotTransforms[poolIndex] = instance.transform;

            if (instance.TryGetComponent(out Rigidbody pooledRigidbody))
            {
                pooledRigidbody.isKinematic = false;
                pooledRigidbody.linearVelocity = Vector3.zero;
                pooledRigidbody.angularVelocity = Vector3.zero;
                _poolSlotRigidbodies[poolIndex] = pooledRigidbody;
            }
            else
            {
                _poolSlotRigidbodies[poolIndex] = null;
            }

            PoolSlotData slotData = _poolSlotData[poolIndex];
            WritePoolSlotPosition(ref slotData, in hydratedPosition);
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.Dirty);
            slotData.StateFlags |= (byte)PoolSlotStateFlags.Hydrated;
            slotData.RefCount = 1;
            slotData.HydrationFrame = _hydrationFrameCounter;
            slotData.LastVisibleFrame = _hydrationFrameCounter;
            slotData.StableFrames = 0;
            _poolSlotData[poolIndex] = slotData;
            return true;
        }

        private void DehydrateRecord(int recordIndex, bool syncTransformBackToRecord)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            if (!_hydratedInstancesByRecordIndex.TryGetValue(recordIndex, out GameObject instance))
                instance = _hydratedInstancesBySlot[recordIndex];

            if (instance == null)
            {
                ClearHydratedSlot(recordIndex);
                return;
            }

            _hydratedInstancesByRecordIndex.Remove(recordIndex);

            if (syncTransformBackToRecord)
                SyncRecordFromLiveInstance(recordIndex, instance, instance.transform);

            if (instance.TryGetComponent(out PickupItem pickupItem))
                pickupItem.ClearPersistentWorldRecord();

            if (instance.TryGetComponent(out HectonItem hectonItem))
                hectonItem.ClearPersistentWorldRecord();

            Rigidbody pooledRigidbody = _poolSlotRigidbodies[recordIndex];
            if (pooledRigidbody == null)
                instance.TryGetComponent(out pooledRigidbody);

            if (pooledRigidbody != null)
            {
                pooledRigidbody.linearVelocity = Vector3.zero;
                pooledRigidbody.angularVelocity = Vector3.zero;
                pooledRigidbody.isKinematic = true;
                pooledRigidbody.Sleep();
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                pool.Despawn(instance);
            }
            else
            {
                instance.SetActive(false);
            }

            ClearHydratedSlot(recordIndex);
        }

        private void DehydrateAll(bool syncTransformsBackToRecords)
        {
            if (_hydratedInstancesByRecordIndex == null || _hydratedInstancesByRecordIndex.Count <= 0)
                return;

            _recordIndexScratch.Clear();

            Dictionary<int, GameObject>.Enumerator hydratedEnumerator = _hydratedInstancesByRecordIndex.GetEnumerator();
            while (hydratedEnumerator.MoveNext())
                _recordIndexScratch.Add(hydratedEnumerator.Current.Key);

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                DehydrateRecord(_recordIndexScratch[i], syncTransformsBackToRecords);

            _recordIndexScratch.Clear();
        }

        private void SyncRecordFromLiveInstance(int recordIndex, GameObject instance, Transform sourceTransform)
        {
            if (!IsValidRecordIndex(recordIndex) || sourceTransform == null)
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (record.IsCollected)
                return;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromRuntimePosition(sourceTransform.position);
            int3 nextChunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            if (!math.all(nextChunkId == record.ChunkId))
            {
                RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
                _recordsByChunk.Add(nextChunkId, recordIndex);
                record.ChunkId = nextChunkId;
            }

            record.Position = position;
            EntityDataRecord state = CaptureEntityStateFromLiveInstance(in record, instance, in position);
            record.Quantity = state.Quantity;
            _records[recordIndex] = record;
            RegisterOrUpdatePoolSlot(recordIndex, in record);
            RegisterOrUpdateEntityState(in record, in state);
            UpsertDeltaRecord(in record);
        }

        private void RemoveRecordIndexFromChunk(int3 chunkId, int recordIndex)
        {
            if (!_recordsByChunk.TryGetFirstValue(chunkId, out int value, out NativeParallelMultiHashMapIterator<int3> iterator))
                return;

            do
            {
                if (value != recordIndex)
                    continue;

                _recordsByChunk.Remove(iterator);
                return;
            }
            while (_recordsByChunk.TryGetNextValue(out value, ref iterator));
        }

        private bool ShouldHydrateChunk(int3 chunkId)
        {
            if (!_playerChunkValid)
                return false;

            int radius = ResolveHydrationScanChunkRadius();
            int3 delta = chunkId - _currentPlayerChunk;
            return math.abs(delta.x) <= radius &&
                   math.abs(delta.y) <= radius &&
                   math.abs(delta.z) <= radius;
        }

        private bool ShouldHydrateDehydratedRecord(in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (record.IsCollected)
                return false;

            AbsoluteUniversePosition recordAup = ResolveResidencyPosition(in record);
            return AbsoluteUniversePosition.DistanceSq(in recordAup, in playerAup) <= HydrateRadiusSq;
        }

        private bool ShouldKeepHydratedRecord(in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (record.IsCollected)
                return false;

            AbsoluteUniversePosition recordAup = ResolveResidencyPosition(in record);
            return AbsoluteUniversePosition.DistanceSq(in recordAup, in playerAup) <= DehydrateRadiusSq;
        }

        private bool TryResolveItemData(in PersistentWorldItemRecord record, out ItemData itemData)
        {
            itemData = null;
            if (!TryEnsureItemLookup())
                return false;

            if (record.ItemPersistentIdHash != 0UL &&
                _itemLookupByHash.TryGetValue(record.ItemPersistentIdHash, out ItemData resolvedItem) &&
                resolvedItem != null)
            {
                itemData = resolvedItem;
                return true;
            }

            if (_resolvedItemCatalog == null)
                return false;

            itemData = _resolvedItemCatalog.FindById(record.ItemPersistentId.ToString());
            if (itemData == null)
                return false;

            _itemLookupByHash[record.ItemPersistentIdHash] = itemData;
            return true;
        }

        private bool TryEnsureItemLookup()
        {
            ItemCatalog currentCatalog = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.ItemCatalog
                : null;

            if (currentCatalog == null)
                return false;

            if (ReferenceEquals(_resolvedItemCatalog, currentCatalog) && _itemLookupByHash.Count > 0)
                return true;

            _resolvedItemCatalog = currentCatalog;
            _itemLookupByHash.Clear();
            _itemCatalogScratch.Clear();

            int itemCount = currentCatalog.GetAllItemsNonAlloc(_itemCatalogScratch);
            for (int i = 0; i < itemCount; i++)
            {
                ItemData itemData = _itemCatalogScratch[i];
                if (itemData == null)
                    continue;

                ulong itemHash = ComputePersistentIdHash(itemData.PersistentId);
                if (itemHash == 0UL)
                    continue;

                if (_itemLookupByHash.TryGetValue(itemHash, out ItemData existing) &&
                    existing != null &&
                    !ReferenceEquals(existing, itemData))
                {
                    continue;
                }

                _itemLookupByHash[itemHash] = itemData;
            }

            return _itemLookupByHash.Count > 0;
        }

        private bool IsValidRecordIndex(int recordIndex)
        {
            return recordIndex >= 0 && recordIndex < _records.Length;
        }

        private bool IsValidPoolIndex(int poolIndex)
        {
            return _poolSlotData.IsCreated && poolIndex >= 0 && poolIndex < _poolSlotData.Length;
        }

        private int ResolveHydrationScanChunkRadius()
        {
            int distanceRadius = (int)math.ceil(DehydrateRadiusMeters / math.max(1f, chunkSizeMeters));
            return math.max(hydrationRadiusInChunks, distanceRadius);
        }

        private void RegisterOrUpdatePoolSlot(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (!IsValidPoolIndex(recordIndex) || !_guidToPoolIndex.IsCreated)
                return;

            PoolSlotData slotData = _poolSlotData[recordIndex];
            ulong nextGuid = ComputePoolGuid(in record);
            ulong previousGuid = slotData.BoundGuid;
            if (previousGuid != 0UL &&
                previousGuid != nextGuid &&
                _guidToPoolIndex.TryGetValue(previousGuid, out int previousIndex) &&
                previousIndex == recordIndex)
            {
                _guidToPoolIndex.Remove(previousGuid);
            }

            _guidToPoolIndex.Remove(nextGuid);
            _guidToPoolIndex.TryAdd(nextGuid, recordIndex);

            slotData.BoundGuid = nextGuid;
            WritePoolSlotPosition(ref slotData, in record.Position);
            _poolSlotData[recordIndex] = slotData;
        }

        private void RegisterOrUpdateEntityState(in PersistentWorldItemRecord record)
        {
            EntityDataRecord state = CreateEntityStateFromRecord(in record);
            RegisterOrUpdateEntityState(in record, in state);
        }

        private void RegisterOrUpdateEntityState(in PersistentWorldItemRecord record, in EntityDataRecord state)
        {
            if (record.InstanceUid == 0u || !_entityStateByInstanceUid.IsCreated)
                return;

            _entityStateByInstanceUid.Remove(record.InstanceUid);
            _entityStateByInstanceUid.TryAdd(record.InstanceUid, state);
        }

        private void RemoveEntityState(in PersistentWorldItemRecord record)
        {
            if (!_entityStateByInstanceUid.IsCreated || record.InstanceUid == 0u)
                return;

            _entityStateByInstanceUid.Remove(record.InstanceUid);
        }

        private EntityDataRecord ResolveEntityState(in PersistentWorldItemRecord record)
        {
            if (_entityStateByInstanceUid.IsCreated &&
                record.InstanceUid != 0u &&
                _entityStateByInstanceUid.TryGetValue(record.InstanceUid, out EntityDataRecord state))
            {
                return state;
            }

            return CreateEntityStateFromRecord(in record);
        }

        private static EntityDataRecord CreateEntityStateFromRecord(in PersistentWorldItemRecord record)
        {
            return new EntityDataRecord
            {
                Position = record.Position.ToAlignedBlit(),
                Quantity = math.max(1, record.Quantity),
                Integrity01 = 1f,
                InventoryHash = 0,
                InstanceUid = record.InstanceUid
            };
        }

        private EntityDataRecord CaptureEntityStateFromLiveInstance(
            in PersistentWorldItemRecord record,
            GameObject instance,
            in AbsoluteUniversePosition position)
        {
            EntityDataRecord state = ResolveEntityState(in record);
            state.Position = position.ToAlignedBlit();
            state.InstanceUid = record.InstanceUid;
            state.Quantity = math.max(1, record.Quantity);

            if (instance != null)
            {
                if (instance.TryGetComponent(out PickupItem pickupItem))
                {
                    state.Quantity = math.max(1, pickupItem.Quantity);
                }
                else if (instance.TryGetComponent(out HectonItem hectonItem))
                {
                    state.Quantity = math.max(1, hectonItem.Quantity);
                }
            }

            if (state.Integrity01 <= 0f)
                state.Integrity01 = 1f;

            return state;
        }

        private AbsoluteUniversePosition ResolveResidencyPosition(in PersistentWorldItemRecord record)
        {
            EntityDataRecord state = ResolveEntityState(in record);
            return AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
        }

        private bool TryGetPoolIndex(in PersistentWorldItemRecord record, out int poolIndex)
        {
            poolIndex = -1;
            if (!_guidToPoolIndex.IsCreated)
                return false;

            return _guidToPoolIndex.TryGetValue(ComputePoolGuid(in record), out poolIndex) &&
                   IsValidPoolIndex(poolIndex);
        }

        private static ulong ComputePoolGuid(in PersistentWorldItemRecord record)
        {
            ulong guid = record.ItemPersistentIdHash ^ ((ulong)record.InstanceUid * PoolGuidMixSalt);
            return guid != 0UL ? guid : (PoolGuidMixSalt ^ 1UL);
        }

        private static void WritePoolSlotPosition(ref PoolSlotData slotData, in AbsoluteUniversePosition position)
        {
            slotData.AupCell = new int3((int)position.GridX, (int)position.GridY, (int)position.GridZ);
            slotData.LocalOffset = new float3(position.LocalX, position.LocalY, position.LocalZ);
        }

        private static AbsoluteUniversePosition ReadPoolSlotPosition(in PoolSlotData slotData)
        {
            return new AbsoluteUniversePosition
            {
                GridX = slotData.AupCell.x,
                GridY = slotData.AupCell.y,
                GridZ = slotData.AupCell.z,
                LocalX = slotData.LocalOffset.x,
                LocalY = slotData.LocalOffset.y,
                LocalZ = slotData.LocalOffset.z
            };
        }

        private void QueueRecordForDehydration(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex) || !_dehydrateQueue.IsCreated)
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            byte hydratedMask = (byte)PoolSlotStateFlags.Hydrated;
            byte queuedMask = (byte)PoolSlotStateFlags.DehydrationQueued;
            if ((slotData.StateFlags & hydratedMask) == 0 || (slotData.StateFlags & queuedMask) != 0)
                return;

            slotData.StateFlags |= queuedMask;
            _poolSlotData[poolIndex] = slotData;
            _dehydrateQueue.Enqueue(recordIndex);
        }

        private void QueueRecordForHydration(int recordIndex, in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (!_pendingHydrationRecords.IsCreated ||
                !IsValidRecordIndex(recordIndex) ||
                record.IsCollected ||
                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex) ||
                !ShouldHydrateDehydratedRecord(in record, in playerAup))
            {
                return;
            }

            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            CompactPendingHydrationQueueIfDrained();
            if (_pendingHydrationRecords.Length >= _pendingHydrationRecords.Capacity)
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            byte hydrationQueuedMask = (byte)PoolSlotStateFlags.HydrationQueued;
            byte hydratedMask = (byte)PoolSlotStateFlags.Hydrated;
            if ((slotData.StateFlags & hydratedMask) != 0 || (slotData.StateFlags & hydrationQueuedMask) != 0)
                return;

            slotData.StateFlags |= hydrationQueuedMask;
            _poolSlotData[poolIndex] = slotData;
            _pendingHydrationRecords.AddNoResize(recordIndex);
            EnsureHydrationSessionScheduled();
        }

        private void EnsureHydrationSessionScheduled()
        {
            if (_hydrationSessionRunning ||
                !Application.isPlaying ||
                !_pendingHydrationRecords.IsCreated)
            {
                return;
            }

            CompactPendingHydrationQueueIfDrained();
            if (_pendingHydrationReadIndex >= _pendingHydrationRecords.Length)
                return;

            _hydrationSessionRunning = true;
            int sessionVersion = ++_hydrationSessionVersion;
            _ = RunHydrationSessionAsync(sessionVersion);
        }

        private async Awaitable RunHydrationSessionAsync(int sessionVersion)
        {
            try
            {
                while (Application.isPlaying &&
                       ReferenceEquals(_instance, this) &&
                       _hydrationSessionVersion == sessionVersion)
                {
                    if (!TryProcessHydrationBurst())
                        break;

                    await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_hydrationSessionVersion == sessionVersion)
                    _hydrationSessionRunning = false;

                CompactPendingHydrationQueueIfDrained();
                if (!_hydrationSessionRunning)
                    EnsureHydrationSessionScheduled();
            }
        }

        private bool TryProcessHydrationBurst()
        {
            if (!_pendingHydrationRecords.IsCreated || _pendingHydrationReadIndex >= _pendingHydrationRecords.Length)
            {
                CompactPendingHydrationQueueIfDrained();
                return false;
            }

            long budgetDeadline = Stopwatch.GetTimestamp() + HydrationFrameBudgetTicks;
            int processedCount = 0;
            while (processedCount < MaxHydrationsPerFrame && _pendingHydrationReadIndex < _pendingHydrationRecords.Length)
            {
                if (processedCount > 0 && Stopwatch.GetTimestamp() >= budgetDeadline)
                    break;

                int recordIndex = _pendingHydrationRecords[_pendingHydrationReadIndex++];
                processedCount++;

                if (!IsValidRecordIndex(recordIndex))
                    continue;

                PersistentWorldItemRecord record = _records[recordIndex];
                ClearHydrationQueuedFlag(in record);
                if (record.IsCollected)
                    continue;

                if (_hasLastHydrationScanAup && !ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                    continue;

                if (!HydrateRecord(recordIndex, in record) &&
                    _hasLastHydrationScanAup &&
                    ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                {
                    QueueRecordForHydration(recordIndex, in record, in _lastHydrationScanAup);
                }
            }

            CompactPendingHydrationQueueIfDrained();
            return _pendingHydrationRecords.IsCreated && _pendingHydrationReadIndex < _pendingHydrationRecords.Length;
        }

        private void CompactPendingHydrationQueueIfDrained()
        {
            if (!_pendingHydrationRecords.IsCreated || _pendingHydrationReadIndex < _pendingHydrationRecords.Length)
                return;

            _pendingHydrationRecords.Clear();
            _pendingHydrationReadIndex = 0;
        }

        private void CancelHydrationSession(bool clearQueue)
        {
            _hydrationSessionVersion++;
            _hydrationSessionRunning = false;
            _pendingHydrationReadIndex = 0;

            if (clearQueue && _pendingHydrationRecords.IsCreated)
                _pendingHydrationRecords.Clear();
        }

        private void ClearHydrationQueuedFlag(in PersistentWorldItemRecord record)
        {
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.HydrationQueued);
            _poolSlotData[poolIndex] = slotData;
        }

        private void DrainDehydrateQueue(int maxDequeueCount)
        {
            if (!_dehydrateQueue.IsCreated)
                return;

            int dequeueBudget = math.max(1, maxDequeueCount);
            while (dequeueBudget-- > 0 && _dehydrateQueue.TryDequeue(out int recordIndex))
            {
                ClearDehydrationQueuedFlag(recordIndex);
                DehydrateRecord(recordIndex, syncTransformBackToRecord: true);
            }
        }

        private void ClearDehydrationQueuedFlag(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.DehydrationQueued);
            _poolSlotData[poolIndex] = slotData;
        }

        private void ClearHydratedSlot(int poolIndex)
        {
            _hydratedInstancesByRecordIndex.Remove(poolIndex);

            if (!IsValidPoolIndex(poolIndex))
                return;

            _hydratedInstancesBySlot[poolIndex] = null;
            _poolSlotTransforms[poolIndex] = null;
            _poolSlotRigidbodies[poolIndex] = null;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.RefCount = 0;
            slotData.StableFrames = 0;
            slotData.StateFlags &= unchecked((byte)~((byte)PoolSlotStateFlags.Hydrated |
                                                     (byte)PoolSlotStateFlags.Dirty |
                                                     (byte)PoolSlotStateFlags.Settled |
                                                     (byte)PoolSlotStateFlags.HydrationQueued |
                                                     (byte)PoolSlotStateFlags.DehydrationQueued));
            _poolSlotData[poolIndex] = slotData;
        }

        private void ResetPoolSlots()
        {
            if (_guidToPoolIndex.IsCreated)
                _guidToPoolIndex.Clear();

            if (_entityStateByInstanceUid.IsCreated)
                _entityStateByInstanceUid.Clear();

            if (_deltaRecordIndexByEntityId.IsCreated)
                _deltaRecordIndexByEntityId.Clear();

            if (_deltaChunkIndexByChunkId.IsCreated)
                _deltaChunkIndexByChunkId.Clear();

            if (_deltaChunkIds.IsCreated)
                _deltaChunkIds.Clear();

            if (_deltaItemIndexByHash.IsCreated)
                _deltaItemIndexByHash.Clear();

            if (_deltaItemHashes.IsCreated)
                _deltaItemHashes.Clear();

            if (_deltaRecordsByChunk.IsCreated)
                _deltaRecordsByChunk.Clear();

            if (_dehydrateQueue.IsCreated)
            {
                int maxDrainCount = math.max(1, _records.Length + 1);
                int drainedCount = 0;
                while (drainedCount < maxDrainCount && _dehydrateQueue.TryDequeue(out _))
                {
                    drainedCount++;
                }

                if (drainedCount >= maxDrainCount && _dehydrateQueue.TryDequeue(out _))
                {
                    Debug.LogError(
                        $"[PersistentWorldRegistry] ResetPoolSlots dehydrate queue drain exceeded watchdog. recordCount={_records.Length}");

                    while (_dehydrateQueue.TryDequeue(out _))
                    {
                    }
                }
            }

            if (_pendingHydrationRecords.IsCreated)
            {
                _pendingHydrationRecords.Clear();
                _pendingHydrationReadIndex = 0;
            }

            if (_poolSlotData.IsCreated)
            {
                for (int i = 0; i < _poolSlotData.Length; i++)
                    _poolSlotData[i] = default;
            }

            if (_hydratedInstancesBySlot != null)
                Array.Clear(_hydratedInstancesBySlot, 0, _hydratedInstancesBySlot.Length);

            if (_poolSlotTransforms != null)
                Array.Clear(_poolSlotTransforms, 0, _poolSlotTransforms.Length);

            if (_poolSlotRigidbodies != null)
                Array.Clear(_poolSlotRigidbodies, 0, _poolSlotRigidbodies.Length);

            _hydratedInstancesByRecordIndex?.Clear();
        }

        private void RegisterPersistentMemoryBudget()
        {
            long totalBytes =
                GetNativeListBytes(_records) +
                GetNativeParallelMultiHashMapBytes(_recordsByChunk) +
                GetNativeListBytes(_deltaRecords) +
                GetNativeHashMapBytes(_deltaRecordIndexByEntityId) +
                GetNativeHashMapBytes(_deltaChunkIndexByChunkId) +
                GetNativeListBytes(_deltaChunkIds) +
                GetNativeHashMapBytes(_deltaItemIndexByHash) +
                GetNativeListBytes(_deltaItemHashes) +
                GetNativeParallelMultiHashMapBytes(_deltaRecordsByChunk) +
                GetNativeListBytes(_saveSnapshotDeltas) +
                GetNativeArrayBytes(_poolSlotData) +
                GetNativeHashMapBytes(_guidToPoolIndex) +
                GetNativeHashMapBytes(_entityStateByInstanceUid);
            MemoryBudgetTracker.Register(MemoryBudgetOwnerName, totalBytes, PersistentMemoryBudgetBytes);
        }

        private static long GetNativeArrayBytes<T>(NativeArray<T> array) where T : unmanaged
        {
            return array.IsCreated ? (long)array.Length * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private static long GetNativeListBytes<T>(NativeList<T> list) where T : unmanaged
        {
            return list.IsCreated ? (long)list.Capacity * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private static long GetNativeHashMapBytes<TKey, TValue>(NativeHashMap<TKey, TValue> map)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return map.IsCreated
                ? (long)map.Capacity * (UnsafeUtility.SizeOf<TKey>() + UnsafeUtility.SizeOf<TValue>())
                : 0L;
        }

        private static long GetNativeParallelMultiHashMapBytes<TKey, TValue>(NativeParallelMultiHashMap<TKey, TValue> map)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return map.IsCreated
                ? (long)map.Capacity * (UnsafeUtility.SizeOf<TKey>() + UnsafeUtility.SizeOf<TValue>())
                : 0L;
        }

        private void UpdateDiagnostics()
        {
            _debugTrackedRecordCount = _records.IsCreated ? CountActiveRecords() : 0;
            _debugHydratedRecordCount = _hydratedInstancesByRecordIndex != null ? _hydratedInstancesByRecordIndex.Count : 0;
            _debugSnapshotRecordCount = _saveSnapshotDeltas.IsCreated ? _saveSnapshotDeltas.Length : 0;
            _debugPlayerChunk = _playerChunkValid
                ? new Vector3Int(_currentPlayerChunk.x, _currentPlayerChunk.y, _currentPlayerChunk.z)
                : default;
        }

        private void UpsertDeltaRecord(in PersistentWorldItemRecord record)
        {
            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || record.InstanceUid == 0u)
                return;

            if (!TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord compactRecord))
                return;

            if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int deltaIndex))
            {
                _deltaRecords[deltaIndex] = compactRecord;
            }
            else if (_deltaRecords.Length < _deltaRecords.Capacity)
            {
                _deltaRecordIndexByEntityId.TryAdd(record.InstanceUid, _deltaRecords.Length);
                _deltaRecords.AddNoResize(compactRecord);
            }
            else
            {
                return;
            }

            RebuildDeltaChunkLookup();
        }

        private void RemoveDeltaRecord(uint instanceUid)
        {
            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || instanceUid == 0u)
                return;

            if (!_deltaRecordIndexByEntityId.TryGetValue(instanceUid, out int deltaIndex) ||
                deltaIndex < 0 ||
                deltaIndex >= _deltaRecords.Length)
            {
                return;
            }

            int lastIndex = _deltaRecords.Length - 1;
            PersistentWorldCompactDeltaRecord lastRecord = _deltaRecords[lastIndex];
            _deltaRecords.RemoveAtSwapBack(deltaIndex);
            _deltaRecordIndexByEntityId.Remove(instanceUid);

            if (deltaIndex < lastIndex)
            {
                _deltaRecordIndexByEntityId.Remove(lastRecord.InstanceUid);
                _deltaRecordIndexByEntityId.TryAdd(lastRecord.InstanceUid, deltaIndex);
            }

            RebuildDeltaChunkLookup();
        }

        private void RebuildDeltaChunkLookup()
        {
            if (!_deltaRecordsByChunk.IsCreated)
                return;

            _deltaRecordsByChunk.Clear();
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                PersistentWorldCompactDeltaRecord compactRecord = _deltaRecords[i];
                if (!TryGetCompactDeltaChunkId(compactRecord, out int3 chunkId))
                    continue;

                _deltaRecordsByChunk.Add(ComputeChunkDeltaKey(chunkId), compactRecord);
            }
        }

        private static uint ComputeChunkDeltaKey(int3 chunkId)
        {
            return math.hash(chunkId);
        }

        internal int CopyChunkDeltas(int3 chunkId, NativeList<PersistentWorldDeltaRecord> destination)
        {
            if (!destination.IsCreated || !_deltaRecordsByChunk.IsCreated)
                return 0;

            uint chunkKey = ComputeChunkDeltaKey(chunkId);
            if (!_deltaRecordsByChunk.TryGetFirstValue(chunkKey, out PersistentWorldCompactDeltaRecord compactRecord, out var iterator))
                return 0;

            int copiedCount = 0;
            do
            {
                if (!TryResolveDeltaRecord(compactRecord, out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!math.all(expandedRecord.ChunkId == chunkId))
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }
            while (_deltaRecordsByChunk.TryGetNextValue(out compactRecord, ref iterator));

            return copiedCount;
        }

        private bool TryBuildCompactDeltaRecord(in PersistentWorldItemRecord record, out PersistentWorldCompactDeltaRecord compactRecord)
        {
            compactRecord = default;
            PersistentWorldDeltaRecord expandedRecord = PersistentWorldDeltaRecord.FromRecord(in record, chunkSizeMeters);
            if (!expandedRecord.IsValid)
                return false;

            if (!TryEnsureDeltaChunkIndex(expandedRecord.ChunkId, out ushort chunkIndex) ||
                !TryEnsureDeltaItemHashIndex(expandedRecord.ItemPersistentIdHash, out ushort itemHashIndex))
            {
                return false;
            }

            compactRecord = new PersistentWorldCompactDeltaRecord
            {
                PackedLocalPosition = expandedRecord.PackedLocalPosition,
                InstanceUid = expandedRecord.InstanceUid,
                Quantity = expandedRecord.Quantity,
                ItemFlags = expandedRecord.ItemFlags,
                Reserved = 0,
                ChunkIndex = chunkIndex,
                ItemHashIndex = itemHashIndex
            };
            return compactRecord.IsValid;
        }

        private bool TryResolveDeltaRecord(PersistentWorldCompactDeltaRecord compactRecord, out PersistentWorldDeltaRecord expandedRecord)
        {
            expandedRecord = default;
            if (!compactRecord.IsValid ||
                !TryGetCompactDeltaChunkId(compactRecord, out int3 chunkId) ||
                !TryGetCompactDeltaItemHash(compactRecord, out ulong itemHash))
            {
                return false;
            }

            expandedRecord = new PersistentWorldDeltaRecord
            {
                ChunkId = chunkId,
                ItemPersistentIdHash = itemHash,
                InstanceUid = compactRecord.InstanceUid,
                PackedLocalPosition = compactRecord.PackedLocalPosition,
                Quantity = compactRecord.Quantity,
                ItemFlags = compactRecord.ItemFlags,
                Reserved = compactRecord.Reserved
            };
            return expandedRecord.IsValid;
        }

        private bool TryEnsureDeltaChunkIndex(int3 chunkId, out ushort chunkIndex)
        {
            chunkIndex = 0;
            if (!_deltaChunkIndexByChunkId.IsCreated || !_deltaChunkIds.IsCreated)
                return false;

            if (_deltaChunkIndexByChunkId.TryGetValue(chunkId, out chunkIndex))
                return true;

            if (_deltaChunkIds.Length >= _deltaChunkIds.Capacity || _deltaChunkIds.Length >= ushort.MaxValue)
                return false;

            chunkIndex = (ushort)_deltaChunkIds.Length;
            _deltaChunkIds.AddNoResize(chunkId);
            _deltaChunkIndexByChunkId.TryAdd(chunkId, chunkIndex);
            return true;
        }

        private bool TryEnsureDeltaItemHashIndex(ulong itemHash, out ushort itemHashIndex)
        {
            itemHashIndex = 0;
            if (!_deltaItemIndexByHash.IsCreated || !_deltaItemHashes.IsCreated || itemHash == 0UL)
                return false;

            if (_deltaItemIndexByHash.TryGetValue(itemHash, out itemHashIndex))
                return true;

            if (_deltaItemHashes.Length >= _deltaItemHashes.Capacity || _deltaItemHashes.Length >= ushort.MaxValue)
                return false;

            itemHashIndex = (ushort)_deltaItemHashes.Length;
            _deltaItemHashes.AddNoResize(itemHash);
            _deltaItemIndexByHash.TryAdd(itemHash, itemHashIndex);
            return true;
        }

        private bool TryGetCompactDeltaChunkId(PersistentWorldCompactDeltaRecord compactRecord, out int3 chunkId)
        {
            chunkId = default;
            int chunkIndex = compactRecord.ChunkIndex;
            if (!_deltaChunkIds.IsCreated || chunkIndex < 0 || chunkIndex >= _deltaChunkIds.Length)
                return false;

            chunkId = _deltaChunkIds[chunkIndex];
            return true;
        }

        private bool TryGetCompactDeltaItemHash(PersistentWorldCompactDeltaRecord compactRecord, out ulong itemHash)
        {
            itemHash = 0UL;
            int itemHashIndex = compactRecord.ItemHashIndex;
            if (!_deltaItemHashes.IsCreated || itemHashIndex < 0 || itemHashIndex >= _deltaItemHashes.Length)
                return false;

            itemHash = _deltaItemHashes[itemHashIndex];
            return itemHash != 0UL;
        }

        private int CountActiveRecords()
        {
            int count = 0;
            for (int i = 0; i < _records.Length; i++)
            {
                if (!_records[i].IsCollected)
                    count++;
            }

            return count;
        }

        internal static ulong ComputePersistentIdHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0UL;

            ulong hash = FnvOffsetBasis64;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                hash ^= (byte)current;
                hash *= FnvPrime64;
                hash ^= (byte)(current >> 8);
                hash *= FnvPrime64;
            }

            return hash;
        }

        private static bool UID_VALIDATE(in PersistentWorldItemRecord record)
        {
            if (record.ItemPersistentIdHash == 0UL)
                return false;

            string persistentId = record.ItemPersistentId.ToString();
            if (string.IsNullOrEmpty(persistentId))
                return false;

            if (ComputePersistentIdHash(persistentId) != record.ItemPersistentIdHash)
                return false;

            if (record.InstanceUid == 0u)
                return true;

            uint expectedTypeId = ResolveInstanceUidTypeId(null, record.ItemPersistentIdHash);
            uint actualTypeId = record.InstanceUid >> InstanceUidTypeShift;
            return actualTypeId == expectedTypeId;
        }

        private bool TryEnsureInstanceUid(ref PersistentWorldItemRecord record)
        {
            if (record.InstanceUid != 0u)
                return true;

            ItemData itemData = null;
            TryResolveItemData(in record, out itemData);
            return TryGenerateInstanceUid(itemData, record.ItemPersistentIdHash, out record.InstanceUid);
        }

        private static bool TryGenerateInstanceUid(ItemData itemData, ulong persistentIdHash, out uint instanceUid)
        {
            instanceUid = 0u;

            uint sequence = unchecked((uint)Interlocked.Increment(ref _nextInstanceUidCounter));
            if (sequence == 0u || sequence > InstanceUidCounterMask)
            {
                Debug.LogError("[PersistentWorldRegistry] Exhausted 24-bit persistent item instance UID counter.");
                return false;
            }

            uint typeId = ResolveInstanceUidTypeId(itemData, persistentIdHash);
            instanceUid = (typeId << InstanceUidTypeShift) | sequence;
            return true;
        }

        private static uint ResolveInstanceUidTypeId(ItemData itemData, ulong persistentIdHash)
        {
            if (itemData != null)
                return ((uint)itemData.category) & 0xFFu;

            return persistentIdHash != 0UL
                ? (uint)((persistentIdHash >> 56) & 0xFFUL)
                : 0u;
        }

        private static void RebaseInstanceUidCounter(uint maxObservedSequence)
        {
            int target = (int)math.min(maxObservedSequence, InstanceUidCounterMask);
            int snapshot = Volatile.Read(ref _nextInstanceUidCounter);
            int compareExchangeWatchdog = 16;
            while (snapshot < target && compareExchangeWatchdog-- > 0)
            {
                int prior = Interlocked.CompareExchange(ref _nextInstanceUidCounter, target, snapshot);
                if (prior == snapshot)
                    return;

                snapshot = prior;
            }
        }
    }
}
