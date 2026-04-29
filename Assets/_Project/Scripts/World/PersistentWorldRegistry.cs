using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct AbsoluteUniversePosition
    {
        internal const int CellSizeMeters = 5000;

        [FieldOffset(0)]
        public long GridX;
        [FieldOffset(8)]
        public long GridY;
        [FieldOffset(16)]
        public long GridZ;
        [FieldOffset(24)]
        public float LocalX;
        [FieldOffset(28)]
        public float LocalY;
        [FieldOffset(32)]
        public float LocalZ;
        [FieldOffset(36)]
        private float _pad;

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
            const double cellSize = CellSizeMeters;
            long gridDeltaX = a.GridX - b.GridX;
            long gridDeltaY = a.GridY - b.GridY;
            long gridDeltaZ = a.GridZ - b.GridZ;
            double deltaX = (gridDeltaX * cellSize) + ((double)a.LocalX - b.LocalX);
            double deltaY = (gridDeltaY * cellSize) + ((double)a.LocalY - b.LocalY);
            double deltaZ = (gridDeltaZ * cellSize) + ((double)a.LocalZ - b.LocalZ);
            return (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
        }
    }

    /// <summary>
    /// 16-byte-aligned AUP transfer payload for network or memcpy lanes that require float4-friendly packing.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct AbsoluteUniversePositionBlit128
    {
        [FieldOffset(0)]
        public long GridX;
        [FieldOffset(8)]
        public long GridY;
        [FieldOffset(16)]
        public long GridZ;
        [FieldOffset(24)]
        public float4 Local;
        [FieldOffset(40)]
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

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 204)]
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
        private sealed class SectorOverrideState
        {
            public string TempPath;
            public string EntityStateTempPath;
            public float LastUnloadedTime;
            public bool IsResident;
        }

        private readonly struct SectorOverrideWriteResult
        {
            public readonly long SectorHash;
            public readonly string TempPath;
            public readonly string EntityStateTempPath;

            public SectorOverrideWriteResult(long sectorHash, string tempPath, string entityStateTempPath)
            {
                SectorHash = sectorHash;
                TempPath = tempPath;
                EntityStateTempPath = entityStateTempPath;
            }
        }

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
        private const int PagedSectorWindowWidth = 3;
        private const int PagedSectorHashCount = PagedSectorWindowWidth * PagedSectorWindowWidth;
        private const int PagedSectorEdgeLengthMeters = 1000;
        private const float SectorEvictionDistanceMeters = 2500f;
        private const float SectorOverrideCommitIntervalSeconds = 10f;
        private const float SectorOverrideCommitDelaySeconds = 300f;
        private const ulong PoolGuidMixSalt = 11400714819323198485UL;
        private const long PersistentMemoryBudgetBytes = 10485760L;
        private const string MemoryBudgetOwnerName = "PersistentWorldRegistry";
        private static readonly long HydrationFrameBudgetTicks = Math.Max(1L, (long)(Stopwatch.Frequency * 0.0015d));
        private static readonly double SectorEvictionDistanceSq = SectorEvictionDistanceMeters * SectorEvictionDistanceMeters;

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
        private NativeHashMap<uint, float3> _spawnImpulseByInstanceUid;
        private NativeQueue<int> _dehydrateQueue;
        private NativeList<int> _pendingHydrationRecords;
        private GameObject[] _hydratedInstancesBySlot;
        private Transform[] _poolSlotTransforms;
        private Rigidbody[] _poolSlotRigidbodies;
        private Dictionary<int, GameObject> _hydratedInstancesByRecordIndex;
        private Dictionary<ulong, ItemData> _itemLookupByHash;
        private List<ItemData> _itemCatalogScratch;
        private List<int> _worldPrefabPrewarmHashScratch;
        private List<int> _worldPrefabReleaseScratch;
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
        private int2 _currentPlayerSector;
        private AbsoluteUniversePosition _lastHydrationScanAup;
        private bool _indexedSectorPagingEnabled;
        private bool _indexedSectorPagingInFlight;
        private bool _playerSectorValid;
        private bool _sectorOverrideCommitInFlight;
        private float _nextSectorOverrideCommitTime;
        private string _indexedSectorSavePath;
        private string _indexedSectorOverrideDirectory;
        private List<SaveBinaryStorage.IndexedSectorEntryInfo> _indexedSectorDirectory;
        private Dictionary<long, SectorOverrideState> _sectorOverrideStates;
        private HashSet<int> _residentWorldPrefabHashes;

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
            // COLD ALLOC: NativeHashMap<uint,float3>[maxTrackedItems] â€” deferred spawn impulse staging keyed by InstanceUid for persistent debris hydration â€” owner: PersistentWorldRegistry
            _spawnImpulseByInstanceUid = new NativeHashMap<uint, float3>(maxTrackedItems, Allocator.Persistent);
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
            // COLD ALLOC: List<int>[256] â€” unique addressable prefab prewarm hash buffer for paged sector hydration â€” owner: PersistentWorldRegistry
            _worldPrefabPrewarmHashScratch = new List<int>(256);
            // COLD ALLOC: List<int>[256] â€” deferred addressable prefab release scratch buffer for paged sector eviction â€” owner: PersistentWorldRegistry
            _worldPrefabReleaseScratch = new List<int>(256);
            // COLD ALLOC: List<int>[128] — hydrated record scratch buffer for sync/dehydrate passes — owner: PersistentWorldRegistry
            _recordIndexScratch = new List<int>(128);
            // COLD ALLOC: List<IndexedSectorEntryInfo>[256] â€” cached v8 sector directory entries for paged restore â€” owner: PersistentWorldRegistry
            _indexedSectorDirectory = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(256);
            // COLD ALLOC: Dictionary<long,SectorOverrideState>[32] â€” paged sector temp-override residency map â€” owner: PersistentWorldRegistry
            _sectorOverrideStates = new Dictionary<long, SectorOverrideState>(32);
            // COLD ALLOC: HashSet<int>[256] â€” resident addressable world-prefab hash residency set â€” owner: PersistentWorldRegistry
            _residentWorldPrefabHashes = new HashSet<int>();
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

            if (_spawnImpulseByInstanceUid.IsCreated)
                _spawnImpulseByInstanceUid.Dispose();

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
            _resolvedItemCatalog?.DrainDeferredWorldPrefabReleases(4);
            DrainDehydrateQueue(MaxDehydrationsPerTick);
        }

        public void SlowTick()
        {
            if (!WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform) || _playerTransform == null)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            int2 nextSector = QuantizeSector(in playerAup);
            if (_indexedSectorPagingEnabled && (!_playerSectorValid || !math.all(nextSector == _currentPlayerSector)))
            {
                _currentPlayerSector = nextSector;
                _playerSectorValid = true;
                EnsureIndexedSectorPagingScheduled(nextSector);
            }

            TryScheduleSectorOverrideCommit();

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
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition, Vector3.zero);
        }

        internal bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition, Vector3 initialImpulse)
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
            RegisterSpawnImpulse(record.InstanceUid, initialImpulse);
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

        internal void RestoreFromLoadedRecords(PersistentWorldDeltaRecord[] loadedRecords, bool scheduleHydration = true)
        {
            CancelHydrationSession(clearQueue: true);
            DehydrateAll(syncTransformsBackToRecords: false);
            if (!_indexedSectorPagingEnabled)
                _resolvedItemCatalog?.ReleaseAllWorldPrefabHandles();
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

            if (scheduleHydration &&
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform) && _playerTransform != null)
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

        internal void RestoreFromIndexedSave(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return;

            if (_indexedSectorDirectory == null)
                _indexedSectorDirectory = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(256);

            if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(absolutePath, _indexedSectorDirectory, out _, out _))
            {
                _indexedSectorPagingEnabled = false;
                _indexedSectorSavePath = string.Empty;
                return;
            }

            _indexedSectorSavePath = absolutePath;
            _indexedSectorOverrideDirectory = Path.Combine(
                Path.GetDirectoryName(absolutePath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(absolutePath)}_sector_overrides");
            _indexedSectorPagingEnabled = _indexedSectorDirectory.Count > 0;
            _indexedSectorPagingInFlight = false;
            _playerSectorValid = false;
            _sectorOverrideCommitInFlight = false;
            _nextSectorOverrideCommitTime = 0f;
            _sectorOverrideStates?.Clear();

            if (!string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                Directory.CreateDirectory(_indexedSectorOverrideDirectory);

            if (WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform) && _playerTransform != null)
            {
                AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
                int2 playerSector = QuantizeSector(in playerAup);
                _currentPlayerSector = playerSector;
                _playerSectorValid = true;
                EnsureIndexedSectorPagingScheduled(playerSector);
            }
        }

        internal void DisableIndexedSavePaging()
        {
            if (_residentWorldPrefabHashes != null && _residentWorldPrefabHashes.Count > 0 && _resolvedItemCatalog != null)
            {
                _worldPrefabReleaseScratch.Clear();
                HashSet<int>.Enumerator releaseEnumerator = _residentWorldPrefabHashes.GetEnumerator();
                while (releaseEnumerator.MoveNext())
                    _worldPrefabReleaseScratch.Add(releaseEnumerator.Current);
                releaseEnumerator.Dispose();
                _resolvedItemCatalog.QueueWorldPrefabReleaseNonAlloc(_worldPrefabReleaseScratch);
                _residentWorldPrefabHashes.Clear();
            }

            _indexedSectorPagingEnabled = false;
            _indexedSectorPagingInFlight = false;
            _playerSectorValid = false;
            _indexedSectorSavePath = string.Empty;
            _indexedSectorOverrideDirectory = string.Empty;
            _nextSectorOverrideCommitTime = 0f;
            _indexedSectorDirectory?.Clear();
            _sectorOverrideStates?.Clear();
        }

        private void EnsureIndexedSectorPagingScheduled(int2 centerSector)
        {
            if (!_indexedSectorPagingEnabled || _indexedSectorPagingInFlight || string.IsNullOrEmpty(_indexedSectorSavePath))
                return;

            _indexedSectorPagingInFlight = true;
            _ = RunIndexedSectorPagingAsync(centerSector);
        }

        private async Awaitable RunIndexedSectorPagingAsync(int2 centerSector)
        {
            NativeArray<long> desiredSectorHashes = default;
            NativeList<PersistentWorldDeltaRecord> loadedSectorRecords = default;
            PersistentWorldDeltaRecord[] stagedRecords = null;
            Dictionary<uint, EntityDataRecord> stagedEntityStates = null;

            try
            {
                desiredSectorHashes = new NativeArray<long>(PagedSectorHashCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                int hashCursor = 0;
                for (int z = -1; z <= 1; z++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        desiredSectorHashes[hashCursor++] = PackSectorHash(centerSector + new int2(x, z));
                    }
                }

                if (!await SnapshotResidentSectorOverridesAsync(desiredSectorHashes))
                    return;
                loadedSectorRecords = new NativeList<PersistentWorldDeltaRecord>(math.max(16, maxTrackedItems), Allocator.Persistent);

                await Awaitable.BackgroundThreadAsync();
                if (!SaveBinaryStorage.TryLoadIndexedPersistentWorldSectors(_indexedSectorSavePath, desiredSectorHashes, loadedSectorRecords, out string error))
                {
                    Debug.LogError($"[PersistentWorldRegistry] Indexed sector paging failed: {error}");
                    return;
                }

                if (!ApplySectorOverrides(desiredSectorHashes, loadedSectorRecords, out string overrideError))
                {
                    Debug.LogError($"[PersistentWorldRegistry] Sector override merge failed: {overrideError}");
                    return;
                }

                if (!TryLoadSectorEntityStateOverrides(desiredSectorHashes, out stagedEntityStates, out string entityStateError))
                {
                    Debug.LogError($"[PersistentWorldRegistry] Sector entity-state restore failed: {entityStateError}");
                    return;
                }

                int loadedCount = loadedSectorRecords.Length;
                stagedRecords = loadedCount > 0 ? new PersistentWorldDeltaRecord[loadedCount] : Array.Empty<PersistentWorldDeltaRecord>();
                for (int i = 0; i < loadedCount; i++)
                    stagedRecords[i] = loadedSectorRecords[i];

                await Awaitable.MainThreadAsync();
                await AwaitSectorPrefabPrewarmAsync(stagedRecords);
                RestoreFromLoadedRecords(stagedRecords, scheduleHydration: false);
                ApplyStagedEntityStates(stagedEntityStates);
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

                UpdateResidentWorldPrefabResidency(_worldPrefabPrewarmHashScratch);
                MarkResidentSectorOverrides(desiredSectorHashes);
            }
            finally
            {
                if (loadedSectorRecords.IsCreated)
                    loadedSectorRecords.Dispose();
                if (desiredSectorHashes.IsCreated)
                    desiredSectorHashes.Dispose();

                _indexedSectorPagingInFlight = false;
            }
        }

        private async Awaitable AwaitSectorPrefabPrewarmAsync(PersistentWorldDeltaRecord[] stagedRecords)
        {
            if (stagedRecords == null || stagedRecords.Length <= 0 || !TryEnsureItemLookup() || _resolvedItemCatalog == null)
                return;

            _worldPrefabPrewarmHashScratch.Clear();
            for (int i = 0; i < stagedRecords.Length; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = stagedRecords[i];
                if (!deltaRecord.IsValid || !TryResolveItemData(deltaRecord.ItemPersistentIdHash, out ItemData itemData) || itemData == null)
                    continue;

                int itemHashId = ComputeCatalogItemHash(itemData);
                if (itemHashId == 0 || _worldPrefabPrewarmHashScratch.Contains(itemHashId))
                    continue;

                _worldPrefabPrewarmHashScratch.Add(itemHashId);
            }

            if (_worldPrefabPrewarmHashScratch.Count <= 0)
                return;

            _resolvedItemCatalog.QueueWorldPrefabPrewarmNonAlloc(_worldPrefabPrewarmHashScratch);
            while (Application.isPlaying &&
                   ReferenceEquals(_instance, this) &&
                   !_resolvedItemCatalog.AreWorldPrefabsReadyNonAlloc(_worldPrefabPrewarmHashScratch))
            {
                await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
            }
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

            if (_resolvedItemCatalog == null)
                return false;

            int itemHashId = ComputeCatalogItemHash(itemData);
            if (itemHashId == 0)
                return false;

            if (!_resolvedItemCatalog.TryGetLoadedWorldPrefab(itemHashId, out GameObject prefab) || prefab == null)
            {
                _resolvedItemCatalog.QueueWorldPrefabPrewarm(itemHashId);
                return false;
            }

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
                if (TryConsumeSpawnImpulse(record.InstanceUid, out float3 spawnImpulse))
                    PhysicsForceRouter.QueueForce(pooledRigidbody, new Vector3(spawnImpulse.x, spawnImpulse.y, spawnImpulse.z), ForceMode.Impulse);
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

        private bool TryResolveItemData(ulong itemPersistentIdHash, out ItemData itemData)
        {
            itemData = null;
            if (itemPersistentIdHash == 0UL || !TryEnsureItemLookup())
                return false;

            return _itemLookupByHash.TryGetValue(itemPersistentIdHash, out itemData) && itemData != null;
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

        private static int2 QuantizeSector(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolute.x / PagedSectorEdgeLengthMeters),
                (int)math.floor(absolute.z / PagedSectorEdgeLengthMeters));
        }

        private static long PackSectorHash(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private async Awaitable<bool> SnapshotResidentSectorOverridesAsync(NativeArray<long> desiredSectorHashes)
        {
            if (!_indexedSectorPagingEnabled ||
                string.IsNullOrEmpty(_indexedSectorOverrideDirectory) ||
                !_records.IsCreated)
            {
                return true;
            }

            SyncAllHydratedRecords();

            // COLD ALLOC: Dictionary<long,List<PersistentWorldDeltaRecord>>[16] â€” resident sector snapshot buckets during page-out â€” owner: PersistentWorldRegistry
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectors = new Dictionary<long, List<PersistentWorldDeltaRecord>>(16);
            Dictionary<long, List<EntityDataRecord>> sectorEntityStates = new Dictionary<long, List<EntityDataRecord>>(16);
            for (int i = 0; i < _records.Length; i++)
            {
                PersistentWorldItemRecord record = _records[i];
                if (record.IsCollected)
                    continue;

                long sectorHash = ComputeSectorHash(in record.Position);
                if (!sectors.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> bucket))
                {
                    // COLD ALLOC: List<PersistentWorldDeltaRecord>[16] â€” one resident sector override record bucket â€” owner: PersistentWorldRegistry
                    bucket = new List<PersistentWorldDeltaRecord>(16);
                    sectors.Add(sectorHash, bucket);
                }

                bucket.Add(PersistentWorldDeltaRecord.FromRecord(in record, chunkSizeMeters));

                if (!sectorEntityStates.TryGetValue(sectorHash, out List<EntityDataRecord> entityStateBucket))
                {
                    entityStateBucket = new List<EntityDataRecord>(16);
                    sectorEntityStates.Add(sectorHash, entityStateBucket);
                }

                entityStateBucket.Add(ResolveEntityState(in record));
            }

            if (sectors.Count <= 0)
                return true;

            float now = Time.unscaledTime;
            List<SectorOverrideWriteResult> writeResults = new List<SectorOverrideWriteResult>(sectors.Count);
            string failureMessage = string.Empty;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                foreach (KeyValuePair<long, List<PersistentWorldDeltaRecord>> pair in sectors)
                {
                    List<PersistentWorldDeltaRecord> bucket = pair.Value;
                    NativeArray<PersistentWorldDeltaRecord> sectorRecords = new NativeArray<PersistentWorldDeltaRecord>(bucket.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        for (int i = 0; i < bucket.Count; i++)
                            sectorRecords[i] = bucket[i];

                        string tempPath = ResolveSectorOverrideTempPath(pair.Key);
                        if (!SaveBinaryStorage.TryWriteIndexedPersistentWorldSectorOverride(tempPath, pair.Key, sectorRecords, chunkSizeMeters, out string error))
                        {
                            failureMessage = $"[PersistentWorldRegistry] Sector override snapshot failed for 0x{pair.Key:X16}: {error}";
                            break;
                        }

                        string entityStateTempPath = string.Empty;
                        if (sectorEntityStates.TryGetValue(pair.Key, out List<EntityDataRecord> entityStateBucket) &&
                            entityStateBucket != null &&
                            entityStateBucket.Count > 0)
                        {
                            EntityDataRecord[] sectorStateArray = entityStateBucket.ToArray();
                            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(sectorStateArray, Allocator.Temp);
                            try
                            {
                                entityStateTempPath = ResolveSectorEntityStateTempPath(pair.Key);
                                if (!SaveBinaryStorage.TryWriteIndexedSectorEntityStateOverride(entityStateTempPath, pair.Key, sectorStates, chunkSizeMeters, out string entityStateError))
                                {
                                    failureMessage = $"[PersistentWorldRegistry] Sector entity-state snapshot failed for 0x{pair.Key:X16}: {entityStateError}";
                                    break;
                                }
                            }
                            finally
                            {
                                sectorStates.Dispose();
                            }
                        }

                        writeResults.Add(new SectorOverrideWriteResult(pair.Key, tempPath, entityStateTempPath));
                    }
                    finally
                    {
                        sectorRecords.Dispose();
                    }

                    if (!string.IsNullOrEmpty(failureMessage))
                        break;
                }

                await Awaitable.MainThreadAsync();
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(failureMessage))
            {
                Debug.LogError(failureMessage);
                return false;
            }

            for (int i = 0; i < writeResults.Count; i++)
            {
                SectorOverrideWriteResult result = writeResults[i];
                if (!_sectorOverrideStates.TryGetValue(result.SectorHash, out SectorOverrideState state))
                {
                    state = new SectorOverrideState();
                    _sectorOverrideStates.Add(result.SectorHash, state);
                }

                state.TempPath = result.TempPath;
                if (!string.IsNullOrEmpty(result.EntityStateTempPath))
                {
                    state.EntityStateTempPath = result.EntityStateTempPath;
                }
                else if (!string.IsNullOrEmpty(state.EntityStateTempPath) && File.Exists(state.EntityStateTempPath))
                {
                    File.Delete(state.EntityStateTempPath);
                    state.EntityStateTempPath = string.Empty;
                }

                state.LastUnloadedTime = now;
                state.IsResident = false;
            }

            return true;
#if false
            // COLD ALLOC: NativeArray<PersistentWorldDeltaRecord>[sectorRecordCount] â€” resident sector override staging buffer â€” owner: PersistentWorldRegistry
            foreach (KeyValuePair<long, List<PersistentWorldDeltaRecord>> pair in sectors)
            {
                List<PersistentWorldDeltaRecord> bucket = pair.Value;
                NativeArray<PersistentWorldDeltaRecord> sectorRecords = new NativeArray<PersistentWorldDeltaRecord>(bucket.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                try
                {
                    for (int i = 0; i < bucket.Count; i++)
                        sectorRecords[i] = bucket[i];

                    string tempPath = ResolveSectorOverrideTempPath(pair.Key);
                    if (!SaveBinaryStorage.TryWriteIndexedPersistentWorldSectorOverride(tempPath, pair.Key, sectorRecords, chunkSizeMeters, out string error))
                    {
                        Debug.LogError($"[PersistentWorldRegistry] Sector override snapshot failed for 0x{pair.Key:X16}: {error}");
                        continue;
                    }

                    if (!_sectorOverrideStates.TryGetValue(pair.Key, out SectorOverrideState state))
                    {
                        state = new SectorOverrideState();
                        _sectorOverrideStates.Add(pair.Key, state);
                    }

                    state.TempPath = tempPath;
                    if (sectorEntityStates.TryGetValue(pair.Key, out List<EntityDataRecord> entityStateBucket) &&
                        entityStateBucket != null &&
                        entityStateBucket.Count > 0)
                    {
                        // COLD ALLOC: EntityDataRecord[entityStateBucket.Count] — sector entity-state staging snapshot for indexed paging write — owner: PersistentWorldRegistry
                        EntityDataRecord[] sectorStateArray = entityStateBucket.ToArray();
                        NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(sectorStateArray, Allocator.Temp);
                        try
                        {
                            string entityStateTempPath = ResolveSectorEntityStateTempPath(pair.Key);
                            if (!SaveBinaryStorage.TryWriteIndexedSectorEntityStateOverride(entityStateTempPath, pair.Key, sectorStates, chunkSizeMeters, out string entityStateError))
                            {
                                Debug.LogError($"[PersistentWorldRegistry] Sector entity-state snapshot failed for 0x{pair.Key:X16}: {entityStateError}");
                            }
                            else
                            {
                                state.EntityStateTempPath = entityStateTempPath;
                            }
                        }
                        finally
                        {
                            sectorStates.Dispose();
                        }
                    }
                    state.LastUnloadedTime = now;
                    state.IsResident = false;
                }
                finally
                {
                    sectorRecords.Dispose();
                }
            }
#endif
        }

        private bool ApplySectorOverrides(
            NativeArray<long> desiredSectorHashes,
            NativeList<PersistentWorldDeltaRecord> loadedSectorRecords,
            out string error)
        {
            error = string.Empty;
            if (!_indexedSectorPagingEnabled || _sectorOverrideStates == null || _sectorOverrideStates.Count <= 0)
                return true;

            // COLD ALLOC: Dictionary<long,List<PersistentWorldDeltaRecord>>[16] â€” paged sector merge map during override resolution â€” owner: PersistentWorldRegistry
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectorBuckets = new Dictionary<long, List<PersistentWorldDeltaRecord>>(16);
            for (int i = 0; i < loadedSectorRecords.Length; i++)
            {
                PersistentWorldDeltaRecord record = loadedSectorRecords[i];
                if (!record.IsValid)
                    continue;

                AbsoluteUniversePosition unpackedPosition = record.UnpackPosition(chunkSizeMeters);
                long sectorHash = ComputeSectorHash(in unpackedPosition);
                if (!sectorBuckets.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> bucket))
                {
                    // COLD ALLOC: List<PersistentWorldDeltaRecord>[16] â€” one paged sector merge bucket â€” owner: PersistentWorldRegistry
                    bucket = new List<PersistentWorldDeltaRecord>(16);
                    sectorBuckets.Add(sectorHash, bucket);
                }

                bucket.Add(record);
            }

            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                long sectorHash = desiredSectorHashes[i];
                if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                    string.IsNullOrEmpty(state.TempPath) ||
                    !File.Exists(state.TempPath))
                {
                    continue;
                }

                if (!SaveBinaryStorage.TryReadIndexedPersistentWorldSectorOverride(state.TempPath, out long loadedSectorHash, out PersistentWorldDeltaRecord[] overrideRecords, out error))
                    return false;

                if (loadedSectorHash != sectorHash)
                {
                    error = $"Sector override hash mismatch for temp block 0x{sectorHash:X16}.";
                    return false;
                }

                // COLD ALLOC: List<PersistentWorldDeltaRecord>[N] â€” override-resolved sector records loaded from temp block â€” owner: PersistentWorldRegistry
                List<PersistentWorldDeltaRecord> replacement = new List<PersistentWorldDeltaRecord>(overrideRecords.Length);
                for (int recordIndex = 0; recordIndex < overrideRecords.Length; recordIndex++)
                {
                    if (overrideRecords[recordIndex].IsValid)
                        replacement.Add(overrideRecords[recordIndex]);
                }

                sectorBuckets[sectorHash] = replacement;
            }

            loadedSectorRecords.Clear();
            Dictionary<long, List<PersistentWorldDeltaRecord>>.Enumerator enumerator = sectorBuckets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                List<PersistentWorldDeltaRecord> bucket = enumerator.Current.Value;
                for (int i = 0; i < bucket.Count; i++)
                    loadedSectorRecords.Add(bucket[i]);
            }

            enumerator.Dispose();
            return true;
        }

        private bool TryLoadSectorEntityStateOverrides(
            NativeArray<long> desiredSectorHashes,
            out Dictionary<uint, EntityDataRecord> stagedEntityStates,
            out string error)
        {
            stagedEntityStates = null;
            error = string.Empty;

            if (!_indexedSectorPagingEnabled ||
                _sectorOverrideStates == null ||
                _sectorOverrideStates.Count <= 0)
            {
                return true;
            }

            // COLD ALLOC: Dictionary<uint,EntityDataRecord>[64] â€” staged sector entity-state restore map during indexed paging â€” owner: PersistentWorldRegistry
            stagedEntityStates = new Dictionary<uint, EntityDataRecord>(64);
            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                long sectorHash = desiredSectorHashes[i];
                if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                    state == null ||
                    string.IsNullOrEmpty(state.EntityStateTempPath) ||
                    !File.Exists(state.EntityStateTempPath))
                {
                    continue;
                }

                if (!SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(
                        state.EntityStateTempPath,
                        out long loadedSectorHash,
                        out EntityDataRecord[] entityStates,
                        out error))
                {
                    return false;
                }

                if (loadedSectorHash != sectorHash)
                {
                    error = $"Sector entity-state override hash mismatch for temp block 0x{sectorHash:X16}.";
                    return false;
                }

                for (int stateIndex = 0; stateIndex < entityStates.Length; stateIndex++)
                {
                    EntityDataRecord entityState = entityStates[stateIndex];
                    if (entityState.InstanceUid == 0u)
                        continue;

                    stagedEntityStates[entityState.InstanceUid] = entityState;
                }
            }

            return true;
        }

        private void ApplyStagedEntityStates(Dictionary<uint, EntityDataRecord> stagedEntityStates)
        {
            if (stagedEntityStates == null || stagedEntityStates.Count <= 0 || !_entityStateByInstanceUid.IsCreated)
                return;

            Dictionary<uint, EntityDataRecord>.Enumerator enumerator = stagedEntityStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<uint, EntityDataRecord> pair = enumerator.Current;
                _entityStateByInstanceUid.Remove(pair.Key);
                _entityStateByInstanceUid.TryAdd(pair.Key, pair.Value);
            }

            enumerator.Dispose();
        }

        private void UpdateResidentWorldPrefabResidency(List<int> nextResidentHashes)
        {
            if (_residentWorldPrefabHashes == null || _resolvedItemCatalog == null)
                return;

            _worldPrefabReleaseScratch.Clear();
            HashSet<int>.Enumerator residentEnumerator = _residentWorldPrefabHashes.GetEnumerator();
            while (residentEnumerator.MoveNext())
            {
                int residentHash = residentEnumerator.Current;
                bool stillResident = false;
                if (nextResidentHashes != null)
                {
                    for (int i = 0; i < nextResidentHashes.Count; i++)
                    {
                        if (nextResidentHashes[i] != residentHash)
                            continue;

                        stillResident = true;
                        break;
                    }
                }

                if (!stillResident)
                    _worldPrefabReleaseScratch.Add(residentHash);
            }

            residentEnumerator.Dispose();

            if (_worldPrefabReleaseScratch.Count > 0)
                _resolvedItemCatalog.QueueWorldPrefabReleaseNonAlloc(_worldPrefabReleaseScratch);

            _residentWorldPrefabHashes.Clear();
            if (nextResidentHashes == null)
                return;

            for (int i = 0; i < nextResidentHashes.Count; i++)
                _residentWorldPrefabHashes.Add(nextResidentHashes[i]);
        }

        private void MarkResidentSectorOverrides(NativeArray<long> desiredSectorHashes)
        {
            if (_sectorOverrideStates == null || _sectorOverrideStates.Count <= 0)
                return;

            Dictionary<long, SectorOverrideState>.Enumerator enumerator = _sectorOverrideStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value.IsResident = false;
            enumerator.Dispose();

            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                if (_sectorOverrideStates.TryGetValue(desiredSectorHashes[i], out SectorOverrideState state))
                    state.IsResident = true;
            }
        }

        private void TryScheduleSectorOverrideCommit()
        {
            if (!_indexedSectorPagingEnabled ||
                _sectorOverrideCommitInFlight ||
                _sectorOverrideStates == null ||
                _sectorOverrideStates.Count <= 0 ||
                Time.unscaledTime < _nextSectorOverrideCommitTime)
            {
                return;
            }

            _nextSectorOverrideCommitTime = Time.unscaledTime + SectorOverrideCommitIntervalSeconds;
            _sectorOverrideCommitInFlight = true;
            _ = RunSectorOverrideCommitAsync();
        }

        private async Awaitable RunSectorOverrideCommitAsync()
        {
            // COLD ALLOC: List<long>[16] â€” due sector override commit queue â€” owner: PersistentWorldRegistry
            List<long> dueSectorHashes = new List<long>(16);
            try
            {
                float now = Time.unscaledTime;

                Dictionary<long, SectorOverrideState>.Enumerator enumerator = _sectorOverrideStates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<long, SectorOverrideState> pair = enumerator.Current;
                    SectorOverrideState state = pair.Value;
                    if (state == null || state.IsResident || string.IsNullOrEmpty(state.TempPath) || !File.Exists(state.TempPath))
                        continue;

                    if (now - state.LastUnloadedTime >= SectorOverrideCommitDelaySeconds)
                        dueSectorHashes.Add(pair.Key);
                }
                enumerator.Dispose();

                if (dueSectorHashes.Count <= 0)
                    return;

                await Awaitable.BackgroundThreadAsync();
                for (int i = 0; i < dueSectorHashes.Count; i++)
                {
                    long sectorHash = dueSectorHashes[i];
                    if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                        state == null ||
                        string.IsNullOrEmpty(state.TempPath) ||
                        !File.Exists(state.TempPath))
                    {
                        continue;
                    }

                    if (!SaveBinaryStorage.TryCommitIndexedPersistentWorldSectorOverride(_indexedSectorSavePath, state.TempPath, out string error))
                    {
                        Debug.LogError($"[PersistentWorldRegistry] Sector override commit failed for 0x{sectorHash:X16}: {error}");
                    }
                    else if (!string.IsNullOrEmpty(state.EntityStateTempPath) && File.Exists(state.EntityStateTempPath))
                    {
                        File.Delete(state.EntityStateTempPath);
                        state.EntityStateTempPath = string.Empty;
                    }
                }

                await Awaitable.MainThreadAsync();
                for (int i = 0; i < dueSectorHashes.Count; i++)
                {
                    long sectorHash = dueSectorHashes[i];
                    if (_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) &&
                        state != null &&
                        !state.IsResident &&
                        !string.IsNullOrEmpty(state.TempPath) &&
                        !File.Exists(state.TempPath))
                    {
                        _sectorOverrideStates.Remove(sectorHash);
                    }
                }
            }
            finally
            {
                _sectorOverrideCommitInFlight = false;
            }
        }

        private string ResolveSectorOverrideTempPath(long sectorHash)
        {
            if (string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return string.Empty;

            return Path.Combine(_indexedSectorOverrideDirectory, $"{sectorHash:X16}.sectmp");
        }

        private string ResolveSectorEntityStateTempPath(long sectorHash)
        {
            if (string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return string.Empty;

            return Path.Combine(_indexedSectorOverrideDirectory, $"{sectorHash:X16}.estatmp");
        }

        private static long ComputeSectorHash(in AbsoluteUniversePosition position)
        {
            return PackSectorHash(QuantizeSector(in position));
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
            if (_spawnImpulseByInstanceUid.IsCreated)
                _spawnImpulseByInstanceUid.Remove(record.InstanceUid);
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

        private void RegisterSpawnImpulse(uint instanceUid, Vector3 initialImpulse)
        {
            if (!_spawnImpulseByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            float3 impulse = new float3(initialImpulse.x, initialImpulse.y, initialImpulse.z);
            if (!math.all(math.isfinite(impulse)) || math.lengthsq(impulse) <= 0.000001f)
                return;

            _spawnImpulseByInstanceUid.Remove(instanceUid);
            _spawnImpulseByInstanceUid.TryAdd(instanceUid, impulse);
        }

        private bool TryConsumeSpawnImpulse(uint instanceUid, out float3 impulse)
        {
            impulse = default;
            if (!_spawnImpulseByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            if (!_spawnImpulseByInstanceUid.TryGetValue(instanceUid, out impulse))
                return false;

            _spawnImpulseByInstanceUid.Remove(instanceUid);
            return true;
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
            QueueWorldPrefabPrewarmForRecord(in record);
            _pendingHydrationRecords.AddNoResize(recordIndex);
            EnsureHydrationSessionScheduled();
        }

        private void QueueWorldPrefabPrewarmForRecord(in PersistentWorldItemRecord record)
        {
            if (!TryResolveItemData(in record, out ItemData itemData) || itemData == null || _resolvedItemCatalog == null)
                return;

            int itemHashId = ComputeCatalogItemHash(itemData);
            if (itemHashId == 0)
                return;

            _resolvedItemCatalog.QueueWorldPrefabPrewarm(itemHashId);
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

        private static int ComputeCatalogItemHash(ItemData itemData)
        {
            if (itemData == null || string.IsNullOrWhiteSpace(itemData.PersistentId))
                return 0;

            return Hecton.Localization.LocHash.Compute(itemData.PersistentId);
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
