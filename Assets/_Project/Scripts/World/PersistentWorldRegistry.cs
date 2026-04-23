using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Unity.Collections;
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
    }

    [Flags]
    internal enum PersistentWorldItemFlags : byte
    {
        None = 0,
        Collected = 1 << 0
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PersistentWorldItemRecord
    {
        public AbsoluteUniversePosition Position;
        public int3 ChunkId;
        public ulong ItemPersistentIdHash;
        public FixedString128Bytes ItemPersistentId;
        public int Quantity;
        public PersistentWorldItemFlags Flags;
        private byte _reserved0;
        private ushort _reserved1;

        public bool IsCollected => (Flags & PersistentWorldItemFlags.Collected) != 0;

        public void MarkCollected()
        {
            Flags |= PersistentWorldItemFlags.Collected;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5850)]
    public sealed class PersistentWorldRegistry : MonoBehaviour, ITickable
    {
        private const int DefaultMaxTrackedItems = 16384;
        private const int DefaultChunkSizeMeters = 64;
        private const int DefaultHydrationRadius = 1;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private static PersistentWorldRegistry _instance;

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
        private NativeList<PersistentWorldItemRecord> _saveSnapshotRecords;
        private Dictionary<int, GameObject> _hydratedInstancesByRecordIndex;
        private Dictionary<ulong, ItemData> _itemLookupByHash;
        private List<ItemData> _itemCatalogScratch;
        private List<int> _recordIndexScratch;
        private Transform _playerTransform;
        private ItemCatalog _resolvedItemCatalog;
        private bool _tickRegistered;
        private bool _playerChunkValid;
        private int3 _currentPlayerChunk;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
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

            maxTrackedItems = Mathf.Max(256, maxTrackedItems);
            chunkSizeMeters = Mathf.Max(16, chunkSizeMeters);
            hydrationRadiusInChunks = Mathf.Clamp(hydrationRadiusInChunks, 0, 2);

            // COLD ALLOC: NativeList<PersistentWorldItemRecord>[maxTrackedItems] — persistent dropped-item record store — owner: PersistentWorldRegistry
            _records = new NativeList<PersistentWorldItemRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<int3,int>[maxTrackedItems] — dropped-item chunk lookup table — owner: PersistentWorldRegistry
            _recordsByChunk = new NativeParallelMultiHashMap<int3, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldItemRecord>[maxTrackedItems] — immutable save snapshot for background binary writes — owner: PersistentWorldRegistry
            _saveSnapshotRecords = new NativeList<PersistentWorldItemRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: Dictionary<int,GameObject>[128] — hydrated world-item proxy lookup — owner: PersistentWorldRegistry
            _hydratedInstancesByRecordIndex = new Dictionary<int, GameObject>(128);
            // COLD ALLOC: Dictionary<ulong,ItemData>[1024] — persistent-id hash to ItemData lookup cache — owner: PersistentWorldRegistry
            _itemLookupByHash = new Dictionary<ulong, ItemData>(1024);
            // COLD ALLOC: List<ItemData>[1024] — item catalog scratch buffer for hash cache rebuilds — owner: PersistentWorldRegistry
            _itemCatalogScratch = new List<ItemData>(1024);
            // COLD ALLOC: List<int>[128] — hydrated record scratch buffer for sync/dehydrate passes — owner: PersistentWorldRegistry
            _recordIndexScratch = new List<int>(128);

            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            DehydrateAll(syncTransformsBackToRecords: false);
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            DehydrateAll(syncTransformsBackToRecords: false);

            if (_records.IsCreated)
                _records.Dispose();

            if (_recordsByChunk.IsCreated)
                _recordsByChunk.Dispose();

            if (_saveSnapshotRecords.IsCreated)
                _saveSnapshotRecords.Dispose();

            if (_instance == this)
                _instance = null;
        }

        public void Tick(float dt)
        {
            if (!WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform) || _playerTransform == null)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            int3 nextChunk = AbsoluteUniversePosition.ResolveChunkId(in playerAup, chunkSizeMeters);
            if (_playerChunkValid && math.all(nextChunk == _currentPlayerChunk))
                return;

            SyncAllHydratedRecords();
            _currentPlayerChunk = nextChunk;
            _playerChunkValid = true;
            RefreshHydrationWindow();
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

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = ComputePersistentIdHash(itemData.PersistentId),
                ItemPersistentId = new FixedString128Bytes(itemData.PersistentId),
                Quantity = quantity,
                Flags = PersistentWorldItemFlags.None
            };

            int recordIndex = _records.Length;
            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, recordIndex);

            if (ShouldHydrateChunk(chunkId))
                HydrateRecord(recordIndex, in record);

            UpdateDiagnostics();
            return true;
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
            RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
            _hydratedInstancesByRecordIndex.Remove(recordIndex);
            UpdateDiagnostics();
        }

        internal void CaptureSaveSnapshot()
        {
            if (!_saveSnapshotRecords.IsCreated)
                return;

            SyncAllHydratedRecords();
            _saveSnapshotRecords.Clear();

            for (int i = 0; i < _records.Length; i++)
            {
                PersistentWorldItemRecord record = _records[i];
                if (record.IsCollected)
                    continue;

                _saveSnapshotRecords.AddNoResize(record);
            }

            UpdateDiagnostics();
        }

        internal NativeArray<PersistentWorldItemRecord> GetSaveSnapshotArray()
        {
            return _saveSnapshotRecords.IsCreated
                ? _saveSnapshotRecords.AsArray()
                : default;
        }

        internal void RestoreFromLoadedRecords(PersistentWorldItemRecord[] loadedRecords)
        {
            DehydrateAll(syncTransformsBackToRecords: false);
            _records.Clear();
            _recordsByChunk.Clear();
            _saveSnapshotRecords.Clear();
            _playerChunkValid = false;

            if (loadedRecords != null)
            {
                int restoreCount = Mathf.Min(loadedRecords.Length, _records.Capacity);
                for (int i = 0; i < restoreCount; i++)
                {
                    PersistentWorldItemRecord record = loadedRecords[i];
                    if (record.IsCollected)
                        continue;

                    _records.AddNoResize(record);
                    _recordsByChunk.Add(record.ChunkId, _records.Length - 1);
                }
            }

            UpdateDiagnostics();
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _tickRegistered = true;
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _tickRegistered = false;
        }

        private void RefreshHydrationWindow()
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
                if (record.IsCollected || !ShouldHydrateChunk(record.ChunkId))
                    _recordIndexScratch.Add(recordIndex);
            }

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                DehydrateRecord(_recordIndexScratch[i], syncTransformBackToRecord: true);

            _recordIndexScratch.Clear();

            int radius = hydrationRadiusInChunks;
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
                            if (record.IsCollected || _hydratedInstancesByRecordIndex.ContainsKey(recordIndex))
                                continue;

                            HydrateRecord(recordIndex, in record);
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

                SyncRecordFromTransform(recordIndex, instance.transform);
            }

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                _hydratedInstancesByRecordIndex.Remove(_recordIndexScratch[i]);

            _recordIndexScratch.Clear();
        }

        private void HydrateRecord(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (record.IsCollected || _hydratedInstancesByRecordIndex.ContainsKey(recordIndex))
                return;

            if (!TryResolveItemData(in record, out ItemData itemData) || itemData == null)
                return;

            GameObject prefab = itemData.worldPrefab;
            if (prefab == null)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            if (!pool.HasPool(prefab))
                pool.Warmup(prefab, 1);

            float3 runtimePosition = record.Position.ToRuntimeFloat3();
            GameObject instance = pool.Spawn(prefab, new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z), Quaternion.identity, allowExpand: false);
            if (instance == null)
                return;

            if (instance.TryGetComponent(out PickupItem pickupItem))
            {
                pickupItem.Configure(itemData, record.Quantity);
                pickupItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else if (instance.TryGetComponent(out HectonItem hectonItem))
            {
                hectonItem.SetItemData(itemData, record.Quantity);
                hectonItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else
            {
                pool.Despawn(instance);
                return;
            }

            _hydratedInstancesByRecordIndex[recordIndex] = instance;
        }

        private void DehydrateRecord(int recordIndex, bool syncTransformBackToRecord)
        {
            if (!_hydratedInstancesByRecordIndex.TryGetValue(recordIndex, out GameObject instance))
                return;

            _hydratedInstancesByRecordIndex.Remove(recordIndex);

            if (instance == null)
                return;

            if (syncTransformBackToRecord)
                SyncRecordFromTransform(recordIndex, instance.transform);

            if (instance.TryGetComponent(out PickupItem pickupItem))
                pickupItem.ClearPersistentWorldRecord();

            if (instance.TryGetComponent(out HectonItem hectonItem))
                hectonItem.ClearPersistentWorldRecord();

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null && instance.TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(instance);
                return;
            }

            instance.SetActive(false);
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

        private void SyncRecordFromTransform(int recordIndex, Transform sourceTransform)
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
            _records[recordIndex] = record;
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

            int radius = hydrationRadiusInChunks;
            int3 delta = chunkId - _currentPlayerChunk;
            return math.abs(delta.x) <= radius &&
                   math.abs(delta.y) <= radius &&
                   math.abs(delta.z) <= radius;
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

        private void UpdateDiagnostics()
        {
            _debugTrackedRecordCount = _records.IsCreated ? CountActiveRecords() : 0;
            _debugHydratedRecordCount = _hydratedInstancesByRecordIndex != null ? _hydratedInstancesByRecordIndex.Count : 0;
            _debugSnapshotRecordCount = _saveSnapshotRecords.IsCreated ? _saveSnapshotRecords.Length : 0;
            _debugPlayerChunk = _playerChunkValid
                ? new Vector3Int(_currentPlayerChunk.x, _currentPlayerChunk.y, _currentPlayerChunk.z)
                : default;
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
    }
}
