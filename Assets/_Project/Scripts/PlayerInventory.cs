// ============================================================================
// HECTON-8 - PlayerInventory.cs
// Native SOA-backed inventory owner. Managed ItemData resolution is seam-only.
// ============================================================================

namespace Hecton8.Inventory
{
    using System;
    using Hecton.Localization;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Modding;
    using Hecton8.SaveSystem;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour, ISaveable
    {
        private const ushort CraftingLockedMask = 1 << 10;
        private const ushort BiologicalItemStateMask = 1 << 6;
        private const ushort DefaultQualityMilli = 1000;

        private struct InventorySortEntry : IComparable<InventorySortEntry>
        {
            public ulong PackedKey;
            public int OriginalIndex;

            public int CompareTo(InventorySortEntry other)
            {
                int packedKeyCompare = PackedKey.CompareTo(other.PackedKey);
                if (packedKeyCompare != 0)
                    return packedKeyCompare;

                return OriginalIndex.CompareTo(other.OriginalIndex);
            }
        }

        [BurstCompile]
        private struct InventorySortJob : IJob
        {
            public NativeArray<InventorySortEntry> Entries;

            public void Execute()
            {
                NativeSortExtension.Sort(Entries);
            }
        }

        public struct CraftReservation
        {
            public int AnchorIndex;
            public int Quantity;
            public int ItemHashId;
        }

        public readonly struct ScavengeAttemptResult
        {
            public readonly int RequestedQuantity;
            public readonly int AddedQuantity;
            public readonly int RejectedQuantity;

            public bool AnyAdded => AddedQuantity > 0;
            public bool IsSuccess => AddedQuantity > 0 && RejectedQuantity == 0;

            internal ScavengeAttemptResult(int requestedQuantity, int addedQuantity)
            {
                RequestedQuantity = requestedQuantity;
                AddedQuantity = addedQuantity;
                RejectedQuantity = requestedQuantity - addedQuantity;
            }
        }

        public struct ItemPlacement
        {
            public int itemHashId;
            public int x;
            public int y;
            public ushort width;
            public ushort height;
            public ushort maxStack;
            public ushort stackCount;
            public ushort lockedCount;
            public ushort stateFlags;
            public ushort qualityMilli;
            public uint lastUpdateUnixSeconds;
            public float weight;
            public byte categoryId;
            public byte rarity;
            public bool stackable;

            public InventoryGrid.InventoryItemDescriptor Descriptor => new InventoryGrid.InventoryItemDescriptor(
                itemHashId,
                (byte)width,
                (byte)height,
                maxStack,
                weight,
                categoryId,
                rarity,
                stackable);
        }

        private static PlayerInventory _instance;

        [Header("── Grid Settings ──────────────────")]
        [Tooltip("Inventory grid column count.")]
        [SerializeField] private int columns = 8;
        [Tooltip("Inventory grid row count.")]
        [SerializeField] private int rows = 6;

        [Header("── References ─────────────────────")]
        [Tooltip("Optional survival system weight sink.")]
        [SerializeField] private HectonSurvivalSystem survival;
        [Tooltip("Item catalog used for load-time and UI seam resolution.")]
        [SerializeField] private ItemCatalog itemCatalog;

        private InventoryGrid _grid;
        private NativeArray<ushort> _stackCounts;
        private NativeArray<ushort> _craftLockedCounts;
        private NativeArray<ushort> _anchorStateFlags;
        private NativeArray<ushort> _itemStateFlags;
        private NativeArray<ushort> _qualityMilli;
        private NativeArray<uint> _lastUpdateUnixSeconds;
        private NativeArray<ushort> _scavengeSimStackCounts;
        private NativeArray<byte> _simulationOccupiedCells;
        private ItemPlacement[] _sortBuffer;
        private ItemPlacement[] _sortedPlacements;

        public static PlayerInventory Instance => _instance;
        public float TotalWeight { get; private set; }
        public InventoryGrid Grid => _grid;
        public ItemCatalog ItemCatalog => itemCatalog;
        public int InventoryVersion { get; private set; }
        public event Action InventoryChanged;

        public int SavePriority => 20;
        public int LoadPriority => 20;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _grid = new InventoryGrid(columns, rows);
            // COLD ALLOC: ushort[columns * rows] — anchor stack counts — owner: PlayerInventory
            _stackCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — craft reservations per anchor — owner: PlayerInventory
            _craftLockedCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — per-anchor state flags — owner: PlayerInventory
            _anchorStateFlags = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — persistent per-anchor item-state flags — owner: PlayerInventory
            _itemStateFlags = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — persistent per-anchor quality values (0-1000) — owner: PlayerInventory
            _qualityMilli = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: uint[columns * rows] — persistent per-anchor last update timestamps — owner: PlayerInventory
            _lastUpdateUnixSeconds = new NativeArray<uint>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ushort[columns * rows] — stack simulation scratch — owner: PlayerInventory
            _scavengeSimStackCounts = new NativeArray<ushort>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: byte[columns * rows] — occupancy simulation scratch — owner: PlayerInventory
            _simulationOccupiedCells = new NativeArray<byte>(columns * rows, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: ItemPlacement[columns * rows] — placement snapshot buffer — owner: PlayerInventory
            _sortBuffer = new ItemPlacement[columns * rows];
            // COLD ALLOC: ItemPlacement[columns * rows] — placement reorder buffer — owner: PlayerInventory
            _sortedPlacements = new ItemPlacement[columns * rows];
        }

        private void OnEnable()
        {
            GlobalRegistry.Save?.Register(this);
        }

        private void OnDisable()
        {
            GlobalRegistry.Save?.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_grid != null)
            {
                _grid.Dispose(default);
                _grid = null;
            }

            if (_stackCounts.IsCreated)
                _stackCounts.Dispose(default);

            if (_craftLockedCounts.IsCreated)
                _craftLockedCounts.Dispose(default);

            if (_anchorStateFlags.IsCreated)
                _anchorStateFlags.Dispose(default);

            if (_itemStateFlags.IsCreated)
                _itemStateFlags.Dispose(default);

            if (_qualityMilli.IsCreated)
                _qualityMilli.Dispose(default);

            if (_lastUpdateUnixSeconds.IsCreated)
                _lastUpdateUnixSeconds.Dispose(default);

            if (_scavengeSimStackCounts.IsCreated)
                _scavengeSimStackCounts.Dispose(default);

            if (_simulationOccupiedCells.IsCreated)
                _simulationOccupiedCells.Dispose(default);

            if (_instance == this)
                _instance = null;
        }

        public void RemoveItemAt(int x, int y)
        {
            if (_grid == null || !_stackCounts.IsCreated)
                return;

            int anchorIndex = AnchorIndex(x, y);
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor) || IsCraftLockedFlagSet(anchorIndex))
                return;

            int count = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            _grid.RemoveAnchorAt(anchorIndex);
            _stackCounts[anchorIndex] = 0;
            _craftLockedCounts[anchorIndex] = 0;
            _anchorStateFlags[anchorIndex] = 0;
            _itemStateFlags[anchorIndex] = 0;
            _qualityMilli[anchorIndex] = 0;
            _lastUpdateUnixSeconds[anchorIndex] = 0;

            TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight * count);
            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
        }

        public int RemoveOneItem(int anchorX, int anchorY)
        {
            if (_grid == null || !_stackCounts.IsCreated)
                return 0;

            int anchorIndex = AnchorIndex(anchorX, anchorY);
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                return 0;

            int count = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
            int unlockedCount = Mathf.Max(0, count - GetReservedCraftCount(anchorIndex));
            if (unlockedCount <= 0)
                return 0;

            if (count > 1)
            {
                _stackCounts[anchorIndex] = (ushort)(count - 1);
            }
            else
            {
                _grid.RemoveAnchorAt(anchorIndex);
                _stackCounts[anchorIndex] = 0;
                _craftLockedCounts[anchorIndex] = 0;
                _anchorStateFlags[anchorIndex] = 0;
                _itemStateFlags[anchorIndex] = 0;
                _qualityMilli[anchorIndex] = 0;
                _lastUpdateUnixSeconds[anchorIndex] = 0;
            }

            TotalWeight = Mathf.Max(0f, TotalWeight - descriptor.Weight);
            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return descriptor.HashId;
        }

        public bool ConsumeOneItem(int anchorX, int anchorY)
        {
            if (_grid == null)
                return false;

            int anchorIndex = AnchorIndex(anchorX, anchorY);
            if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                return false;

            if (!TryGetRuntimeDescriptor(descriptor.HashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor) ||
                !runtimeDescriptor.IsConsumable)
            {
                return false;
            }

            if (survival != null)
            {
                if (runtimeDescriptor.OxygenRestore > 0f)
                    survival.RefillOxygen(runtimeDescriptor.OxygenRestore);

                if (runtimeDescriptor.EnergyRestore > 0f)
                    survival.RechargeEnergy(runtimeDescriptor.EnergyRestore);

                if (runtimeDescriptor.IntegrityRestore > 0f)
                    survival.Repair(runtimeDescriptor.IntegrityRestore);
            }

            RemoveOneItem(anchorX, anchorY);
            return true;
        }

        public int GetStackCount(int anchorX, int anchorY)
        {
            if (!_stackCounts.IsCreated)
                return 0;

            int index = AnchorIndex(anchorX, anchorY);
            return (uint)index < (uint)_stackCounts.Length ? _stackCounts[index] : 0;
        }

        public int GetItemHashAt(int x, int y)
        {
            return _grid == null ? 0 : _grid.GetCellHashId(x, y);
        }

        public int CountTotal(int itemHashId)
        {
            return CountQuantityByHash(itemHashId, false);
        }

        public int CountAvailableTotal(int itemHashId)
        {
            return CountQuantityByHash(itemHashId, true);
        }

        internal bool TryFindFirstAnchorByHash(int itemHashId, out int anchorIndex)
        {
            anchorIndex = -1;
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0)
                return false;

            for (int i = 0; i < _stackCounts.Length; i++)
            {
                if (!_grid.HasAnchor(i) || _grid.GetAnchorHashId(i) != itemHashId)
                    continue;

                int stackCount = Mathf.Max(1, (int)_stackCounts[i]);
                if (GetReservedCraftCount(i) >= stackCount)
                    continue;

                anchorIndex = i;
                return true;
            }

            return false;
        }

        internal bool TryRemoveFirstMatchingItemByHash(int itemHashId)
        {
            if (!TryFindFirstAnchorByHash(itemHashId, out int anchorIndex) || _grid == null)
                return false;

            int anchorX = anchorIndex % _grid.Columns;
            int anchorY = anchorIndex / _grid.Columns;
            return RemoveOneItem(anchorX, anchorY) != 0;
        }

        public void AddWeight(float amount)
        {
            TotalWeight = Mathf.Max(0f, TotalWeight + amount);
            if (survival != null)
                survival.SetWeight(TotalWeight);
        }

        public bool ContainsItem(int itemHashId)
        {
            return CountAnchorsByHash(itemHashId) > 0;
        }

        public bool TryAddItem(int itemHashId, int quantity = 1)
        {
            return CanAcceptQuantity(itemHashId, quantity) &&
                   TryAddItemInternal(itemHashId, quantity, out _);
        }

        public bool TryReserveQuantityForCraft(int itemHashId, int quantity, CraftReservation[] reservations, ref int reservationCount)
        {
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || quantity <= 0 || reservations == null)
                return false;

            if (CountAvailableTotal(itemHashId) < quantity)
                return false;

            int startReservationCount = reservationCount;
            int remaining = quantity;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId)
                    continue;

                int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                int available = Mathf.Max(0, stackCount - GetReservedCraftCount(anchorIndex));
                if (available <= 0)
                    continue;

                if (reservationCount >= reservations.Length)
                {
                    ReleaseCraftReservationsRange(reservations, startReservationCount, reservationCount);
                    reservationCount = startReservationCount;
                    return false;
                }

                int take = Mathf.Min(available, remaining);
                _craftLockedCounts[anchorIndex] = (ushort)Mathf.Min(ushort.MaxValue, _craftLockedCounts[anchorIndex] + take);
                _anchorStateFlags[anchorIndex] |= CraftingLockedMask;
                reservations[reservationCount++] = new CraftReservation
                {
                    AnchorIndex = anchorIndex,
                    Quantity = take,
                    ItemHashId = itemHashId
                };
                remaining -= take;
            }

            if (remaining > 0)
            {
                ReleaseCraftReservationsRange(reservations, startReservationCount, reservationCount);
                reservationCount = startReservationCount;
                return false;
            }

            return true;
        }

        public void ReleaseCraftReservations(CraftReservation[] reservations, int reservationCount)
        {
            ReleaseCraftReservationsRange(reservations, 0, reservationCount);
        }

        public bool CommitCraftReservations(CraftReservation[] reservations, int reservationCount)
        {
            if (reservations == null || reservationCount <= 0 || _grid == null || !_stackCounts.IsCreated)
                return true;

            for (int i = 0; i < reservationCount; i++)
            {
                if (!IsValidCraftReservation(in reservations[i]))
                {
                    ReleaseCraftReservations(reservations, reservationCount);
                    return false;
                }
            }

            float removedWeight = 0f;
            for (int i = 0; i < reservationCount; i++)
            {
                CraftReservation reservation = reservations[i];
                if (reservation.Quantity <= 0)
                    continue;

                int anchorIndex = reservation.AnchorIndex;
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                _craftLockedCounts[anchorIndex] = (ushort)Mathf.Max(0, _craftLockedCounts[anchorIndex] - reservation.Quantity);
                if (_craftLockedCounts[anchorIndex] == 0)
                    _anchorStateFlags[anchorIndex] = (ushort)(_anchorStateFlags[anchorIndex] & ~CraftingLockedMask);

                int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                int remainingStack = stackCount - reservation.Quantity;
                if (remainingStack <= 0)
                {
                    _grid.RemoveAnchorAt(anchorIndex);
                    _stackCounts[anchorIndex] = 0;
                    _craftLockedCounts[anchorIndex] = 0;
                    _anchorStateFlags[anchorIndex] = 0;
                }
                else
                {
                    _stackCounts[anchorIndex] = (ushort)remainingStack;
                }

                removedWeight += descriptor.Weight * reservation.Quantity;
                reservations[i] = default;
            }

            TotalWeight = Mathf.Max(0f, TotalWeight - removedWeight);
            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return true;
        }

        public bool HasCraftReservations()
        {
            if (!_craftLockedCounts.IsCreated)
                return false;

            for (int i = 0; i < _craftLockedCounts.Length; i++)
            {
                if (IsCraftLockedFlagSet(i) && _craftLockedCounts[i] > 0)
                    return true;
            }

            return false;
        }

        public ScavengeAttemptResult ScavengeAttempt(int itemHashId, int quantity, Transform interactor)
        {
            if (itemHashId == 0 || quantity <= 0)
                return new ScavengeAttemptResult(Mathf.Max(0, quantity), 0);

            TryAddItemInternal(itemHashId, quantity, out int addedQuantity);
            return new ScavengeAttemptResult(quantity, addedQuantity);
        }

        public bool TryRemoveQuantity(int itemHashId, int quantity)
        {
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || quantity <= 0)
                return false;

            if (CountAvailableTotal(itemHashId) < quantity)
                return false;

            int remaining = quantity;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId)
                    continue;

                int stackCount = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                int available = Mathf.Max(0, stackCount - GetReservedCraftCount(anchorIndex));
                if (available <= 0)
                    continue;

                int take = Mathf.Min(available, remaining);
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                if (take >= stackCount && !IsCraftLockedFlagSet(anchorIndex))
                {
                    _grid.RemoveAnchorAt(anchorIndex);
                    _stackCounts[anchorIndex] = 0;
                    _craftLockedCounts[anchorIndex] = 0;
                    _anchorStateFlags[anchorIndex] = 0;
                    _itemStateFlags[anchorIndex] = 0;
                    _qualityMilli[anchorIndex] = 0;
                    _lastUpdateUnixSeconds[anchorIndex] = 0;
                }
                else
                {
                    _stackCounts[anchorIndex] = (ushort)(stackCount - take);
                }

                TotalWeight -= descriptor.Weight * take;
                remaining -= take;
            }

            TotalWeight = Mathf.Max(0f, TotalWeight);
            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            ref InventoryDTO dto = ref data.inventory;
            dto.EnsureCapacity();

            if (_grid == null)
            {
                dto.gridColumns = columns;
                dto.gridRows = rows;
                dto.totalWeight = 0f;
                dto.cellCount = 0;
                return;
            }

            dto.gridColumns = _grid.Columns;
            dto.gridRows = _grid.Rows;
            dto.totalWeight = TotalWeight;

            int cellIndex = 0;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && cellIndex < InventoryDTO.MaxCells; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex))
                    continue;

                int x = anchorIndex % _grid.Columns;
                int y = anchorIndex / _grid.Columns;
                dto.itemHashIds[cellIndex] = _grid.GetAnchorHashId(anchorIndex);
                dto.packedCellCoordinates[cellIndex] = InventoryDTO.PackCellCoordinate(x, y);
                dto.stackCounts[cellIndex] = _stackCounts[anchorIndex];
                dto.itemStateFlags[cellIndex] = _itemStateFlags[anchorIndex];
                dto.qualityMilli[cellIndex] = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli;
                dto.lastUpdateUnixSeconds[cellIndex] = _lastUpdateUnixSeconds[anchorIndex];
                cellIndex++;
            }

            dto.cellCount = cellIndex;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null || itemCatalog == null || _grid == null)
                return;

            InventoryDTO dto = data.inventory;
            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearCraftReservationState();
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_lastUpdateUnixSeconds);
            TotalWeight = 0f;

            if (dto.itemHashIds == null ||
                dto.packedCellCoordinates == null ||
                dto.stackCounts == null ||
                dto.cellCount <= 0)
            {
                return;
            }

            int count = Mathf.Min(dto.cellCount, dto.itemHashIds.Length, dto.packedCellCoordinates.Length, dto.stackCounts.Length);
            for (int i = 0; i < count; i++)
            {
                int itemHashId = dto.itemHashIds[i];
                if (itemHashId == 0)
                    continue;

                if (!TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                    !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                    continue;

                int cellX = InventoryDTO.UnpackCellX(dto.packedCellCoordinates[i]);
                int cellY = InventoryDTO.UnpackCellY(dto.packedCellCoordinates[i]);
                int loadedCount = dto.stackCounts[i] > 0 ? dto.stackCounts[i] : 1;

                if (_grid.CheckFit(cellX, cellY, descriptor.Width, descriptor.Height))
                {
                    _grid.PlaceAt(in descriptor, cellX, cellY);
                    int anchorIndex = AnchorIndex(cellX, cellY);
                    _stackCounts[anchorIndex] = (ushort)Mathf.Clamp(loadedCount, 1, ushort.MaxValue);
                    _itemStateFlags[anchorIndex] = ResolveLoadedItemStateFlags(dto, i, runtimeDescriptor.StateFlags);
                    _qualityMilli[anchorIndex] = ResolveLoadedQualityMilli(dto, i);
                    _lastUpdateUnixSeconds[anchorIndex] = ResolveLoadedTimestamp(dto, i);
                    ApplyLoadedBiologicalDecay(anchorIndex);
                    TotalWeight += descriptor.Weight * loadedCount;
                    continue;
                }

                if (_grid.TryAddItem(in descriptor, out int px, out int py))
                {
                    int anchorIndex = AnchorIndex(px, py);
                    _stackCounts[anchorIndex] = (ushort)Mathf.Clamp(loadedCount, 1, ushort.MaxValue);
                    _itemStateFlags[anchorIndex] = ResolveLoadedItemStateFlags(dto, i, runtimeDescriptor.StateFlags);
                    _qualityMilli[anchorIndex] = ResolveLoadedQualityMilli(dto, i);
                    _lastUpdateUnixSeconds[anchorIndex] = ResolveLoadedTimestamp(dto, i);
                    ApplyLoadedBiologicalDecay(anchorIndex);
                    TotalWeight += descriptor.Weight * loadedCount;
                }
            }

            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
        }

        public void SortInventory()
        {
            if (HasCraftReservations())
                return;

            int count = GetPlacements(_sortBuffer);
            if (count <= 0)
                return;

            NativeArray<InventorySortEntry> sortEntries = new NativeArray<InventorySortEntry>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int i = 0; i < count; i++)
                    sortEntries[i] = BuildInventorySortEntry(in _sortBuffer[i], i);

                // COLD SYNC JOB: inventory sort is explicit user action outside gameplay hot paths.
                JobHandle handle = new InventorySortJob { Entries = sortEntries }.Schedule();
                handle.Complete();

                for (int i = 0; i < count; i++)
                    _sortedPlacements[i] = _sortBuffer[sortEntries[i].OriginalIndex];

                _grid.Clear();
                ClearNativeArray(_stackCounts);
                ClearCraftReservationState();
                ClearNativeArray(_itemStateFlags);
                ClearNativeArray(_qualityMilli);
                ClearNativeArray(_lastUpdateUnixSeconds);
                TotalWeight = 0f;

                for (int i = 0; i < count; i++)
                {
                    ItemPlacement placement = _sortedPlacements[i];
                    InventoryGrid.InventoryItemDescriptor descriptor = placement.Descriptor;
                    if (_grid.TryAddItem(in descriptor, out int px, out int py))
                    {
                        int anchorIndex = AnchorIndex(px, py);
                        _stackCounts[anchorIndex] = placement.stackCount;
                        _itemStateFlags[anchorIndex] = placement.stateFlags;
                        _qualityMilli[anchorIndex] = placement.qualityMilli;
                        _lastUpdateUnixSeconds[anchorIndex] = placement.lastUpdateUnixSeconds;
                        TotalWeight += placement.weight * placement.stackCount;
                    }
                }
            }
            finally
            {
                if (sortEntries.IsCreated)
                    sortEntries.Dispose();
            }

            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
        }

        internal bool TryMoveOrSwapAnchor(int sourceAnchorX, int sourceAnchorY, int targetCellX, int targetCellY)
        {
            if (_grid == null ||
                !_stackCounts.IsCreated ||
                HasCraftReservations() ||
                (uint)sourceAnchorX >= (uint)_grid.Columns ||
                (uint)sourceAnchorY >= (uint)_grid.Rows ||
                (uint)targetCellX >= (uint)_grid.Columns ||
                (uint)targetCellY >= (uint)_grid.Rows)
            {
                return false;
            }

            int sourceAnchorIndex = _grid.GetCellAnchorIndex(sourceAnchorX, sourceAnchorY);
            if (sourceAnchorIndex < 0)
                return false;

            sourceAnchorX = sourceAnchorIndex % _grid.Columns;
            sourceAnchorY = sourceAnchorIndex / _grid.Columns;

            int targetAnchorIndex = _grid.GetCellAnchorIndex(targetCellX, targetCellY);
            int targetAnchorX = targetAnchorIndex >= 0 ? targetAnchorIndex % _grid.Columns : targetCellX;
            int targetAnchorY = targetAnchorIndex >= 0 ? targetAnchorIndex / _grid.Columns : targetCellY;
            if (sourceAnchorX == targetAnchorX && sourceAnchorY == targetAnchorY)
                return false;

            int destinationAnchorIndex = targetAnchorIndex >= 0
                ? targetAnchorIndex
                : (targetAnchorY * _grid.Columns) + targetAnchorX;
            if (!_grid.TryMoveOrSwapAnchor(sourceAnchorIndex, targetAnchorIndex, targetAnchorX, targetAnchorY))
                return false;

            MoveAnchorState(sourceAnchorIndex, destinationAnchorIndex, targetAnchorIndex >= 0);

            if (survival != null)
                survival.SetWeight(TotalWeight);

            NotifyInventoryChanged();
            return true;
        }

        private void MoveAnchorState(int sourceAnchorIndex, int destinationAnchorIndex, bool swappedWithExistingAnchor)
        {
            if (swappedWithExistingAnchor)
            {
                SwapAnchorState(_stackCounts, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_craftLockedCounts, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_anchorStateFlags, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_itemStateFlags, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_qualityMilli, sourceAnchorIndex, destinationAnchorIndex);
                SwapAnchorState(_lastUpdateUnixSeconds, sourceAnchorIndex, destinationAnchorIndex);
                return;
            }

            MoveAnchorStateValue(_stackCounts, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_craftLockedCounts, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_anchorStateFlags, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_itemStateFlags, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_qualityMilli, sourceAnchorIndex, destinationAnchorIndex);
            MoveAnchorStateValue(_lastUpdateUnixSeconds, sourceAnchorIndex, destinationAnchorIndex);
        }

        private static void SwapAnchorState<T>(NativeArray<T> values, int firstIndex, int secondIndex) where T : struct
        {
            if (!values.IsCreated || firstIndex == secondIndex)
                return;

            T temp = values[firstIndex];
            values[firstIndex] = values[secondIndex];
            values[secondIndex] = temp;
        }

        private static void MoveAnchorStateValue<T>(NativeArray<T> values, int sourceIndex, int destinationIndex) where T : struct
        {
            if (!values.IsCreated || sourceIndex == destinationIndex)
                return;

            values[destinationIndex] = values[sourceIndex];
            values[sourceIndex] = default;
        }

        public int GetPlacements(ItemPlacement[] buffer)
        {
            if (buffer == null || _grid == null || !_stackCounts.IsCreated)
                return 0;

            int count = 0;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && count < buffer.Length; anchorIndex++)
            {
                if (!_grid.TryGetAnchorDescriptor(anchorIndex, out InventoryGrid.InventoryItemDescriptor descriptor))
                    continue;

                buffer[count++] = new ItemPlacement
                {
                    itemHashId = descriptor.HashId,
                    x = anchorIndex % _grid.Columns,
                    y = anchorIndex / _grid.Columns,
                    width = descriptor.Width,
                    height = descriptor.Height,
                    maxStack = descriptor.MaxStack,
                    stackCount = (ushort)Mathf.Max(1, _stackCounts[anchorIndex]),
                    lockedCount = _craftLockedCounts[anchorIndex],
                    stateFlags = _itemStateFlags[anchorIndex],
                    qualityMilli = _qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli,
                    lastUpdateUnixSeconds = _lastUpdateUnixSeconds[anchorIndex],
                    weight = descriptor.Weight,
                    categoryId = descriptor.CategoryId,
                    rarity = descriptor.Rarity,
                    stackable = descriptor.Stackable
                };
            }

            return count;
        }

        public NativeArray<ushort>.ReadOnly GetStackCountsReadOnly()
        {
            return _stackCounts.IsCreated ? _stackCounts.AsReadOnly() : default;
        }

        public NativeArray<ushort>.ReadOnly GetCraftLockedCountsReadOnly()
        {
            return _craftLockedCounts.IsCreated ? _craftLockedCounts.AsReadOnly() : default;
        }

        public NativeArray<ushort>.ReadOnly GetAnchorStateFlagsReadOnly()
        {
            return _anchorStateFlags.IsCreated ? _anchorStateFlags.AsReadOnly() : default;
        }

        private bool TryAddItemInternal(int itemHashId, int quantity, out int addedQuantity)
        {
            addedQuantity = 0;
            if (_grid == null ||
                itemHashId == 0 ||
                quantity <= 0 ||
                !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor) ||
                !TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
            {
                return false;
            }

            uint timestampNow = ResolveCurrentUnixTimestamp();

            bool allAdded = true;
            for (int i = 0; i < quantity; i++)
            {
                if (descriptor.Stackable && TryStackItem(descriptor.HashId, descriptor.MaxStack, runtimeDescriptor.StateFlags, timestampNow))
                {
                    TotalWeight += descriptor.Weight;
                    addedQuantity++;
                    continue;
                }

                if (_grid.TryAddItem(in descriptor, out int placedX, out int placedY))
                {
                    int anchorIndex = AnchorIndex(placedX, placedY);
                    _stackCounts[anchorIndex] = 1;
                    _itemStateFlags[anchorIndex] = runtimeDescriptor.StateFlags;
                    _qualityMilli[anchorIndex] = DefaultQualityMilli;
                    _lastUpdateUnixSeconds[anchorIndex] = (runtimeDescriptor.StateFlags & BiologicalItemStateMask) != 0 ? timestampNow : 0u;
                    TotalWeight += descriptor.Weight;
                    addedQuantity++;
                }
                else
                {
                    allAdded = false;
                    break;
                }
            }

            if (addedQuantity > 0)
            {
                if (survival != null)
                    survival.SetWeight(TotalWeight);

                NotifyInventoryChanged();
            }

            return allAdded;
        }

        private bool TryStackItem(int itemHashId, int maxStack, ushort itemStateFlags, uint timestampNow)
        {
            if (_grid == null || !_stackCounts.IsCreated || itemHashId == 0 || maxStack <= 1)
                return false;

            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId || IsCraftLockedFlagSet(anchorIndex))
                    continue;

                if (_stackCounts[anchorIndex] < maxStack)
                {
                    _stackCounts[anchorIndex]++;
                    _itemStateFlags[anchorIndex] = itemStateFlags;
                    if (_qualityMilli[anchorIndex] == 0)
                        _qualityMilli[anchorIndex] = DefaultQualityMilli;
                    if ((itemStateFlags & BiologicalItemStateMask) != 0 && _lastUpdateUnixSeconds[anchorIndex] == 0u)
                        _lastUpdateUnixSeconds[anchorIndex] = timestampNow;
                    return true;
                }
            }

            return false;
        }

        private bool CanAcceptQuantity(int itemHashId, int quantity)
        {
            if (_grid == null ||
                itemHashId == 0 ||
                quantity <= 0 ||
                !_stackCounts.IsCreated ||
                !_scavengeSimStackCounts.IsCreated ||
                !_simulationOccupiedCells.IsCreated ||
                !TryBuildDescriptor(itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor))
            {
                return false;
            }

            CopyNativeArray(_stackCounts, _scavengeSimStackCounts);

            _grid.CopyOccupiedMask(_simulationOccupiedCells);

            int remaining = quantity;
            if (descriptor.Stackable)
            {
                for (int anchorIndex = 0; anchorIndex < _stackCounts.Length && remaining > 0; anchorIndex++)
                {
                    if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != descriptor.HashId || IsCraftLockedFlagSet(anchorIndex))
                        continue;

                    int stackCount = Mathf.Max(1, (int)_scavengeSimStackCounts[anchorIndex]);
                    if (stackCount >= descriptor.MaxStack)
                        continue;

                    int stackCapacity = descriptor.MaxStack - stackCount;
                    int transfer = Mathf.Min(stackCapacity, remaining);
                    _scavengeSimStackCounts[anchorIndex] = (ushort)(stackCount + transfer);
                    remaining -= transfer;
                }
            }

            while (remaining > 0)
            {
                if (!TryReservePlacementInSimulation(in descriptor))
                    return false;

                remaining--;
            }

            return true;
        }

        private bool TryReservePlacementInSimulation(in InventoryGrid.InventoryItemDescriptor descriptor)
        {
            int cols = _grid.Columns;
            int rows = _grid.Rows;
            int width = descriptor.Width;
            int height = descriptor.Height;
            if (width > cols || height > rows)
                return false;

            int maxX = cols - width;
            int maxY = rows - height;
            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    if (_simulationOccupiedCells[AnchorIndex(x, y)] != 0 || !CheckFitInSimulation(x, y, width, height))
                        continue;

                    MarkOccupiedInSimulation(x, y, width, height);
                    return true;
                }
            }

            return false;
        }

        private bool CheckFitInSimulation(int startX, int startY, int width, int height)
        {
            int endX = startX + width;
            int endY = startY + height;
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if (_simulationOccupiedCells[AnchorIndex(x, y)] != 0)
                        return false;
                }
            }

            return true;
        }

        private void MarkOccupiedInSimulation(int startX, int startY, int width, int height)
        {
            int endX = startX + width;
            int endY = startY + height;
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                    _simulationOccupiedCells[AnchorIndex(x, y)] = 1;
            }
        }

        private int AnchorIndex(int x, int y)
        {
            return y * _grid.Columns + x;
        }

        private bool IsCraftLockedFlagSet(int anchorIndex)
        {
            return _anchorStateFlags.IsCreated
                && (uint)anchorIndex < (uint)_anchorStateFlags.Length
                && (_anchorStateFlags[anchorIndex] & CraftingLockedMask) != 0;
        }

        private int GetReservedCraftCount(int anchorIndex)
        {
            if (!_craftLockedCounts.IsCreated || (uint)anchorIndex >= (uint)_craftLockedCounts.Length)
                return 0;

            return IsCraftLockedFlagSet(anchorIndex) ? _craftLockedCounts[anchorIndex] : 0;
        }

        private int CountAnchorsByHash(int itemHashId)
        {
            if (_grid == null || itemHashId == 0 || !_stackCounts.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < _stackCounts.Length; i++)
            {
                if (_grid.HasAnchor(i) && _grid.GetAnchorHashId(i) == itemHashId)
                    count++;
            }

            return count;
        }

        private int CountQuantityByHash(int itemHashId, bool availableOnly)
        {
            if (_grid == null || itemHashId == 0 || !_stackCounts.IsCreated)
                return 0;

            int total = 0;
            for (int anchorIndex = 0; anchorIndex < _stackCounts.Length; anchorIndex++)
            {
                if (!_grid.HasAnchor(anchorIndex) || _grid.GetAnchorHashId(anchorIndex) != itemHashId)
                    continue;

                int count = Mathf.Max(1, (int)_stackCounts[anchorIndex]);
                if (availableOnly)
                    count = Mathf.Max(0, count - GetReservedCraftCount(anchorIndex));

                total += count;
            }

            return total;
        }

        private static ulong PackInventorySortKey(byte categoryId, byte rarity, uint hashId)
        {
            return ((ulong)categoryId << 40)
                | ((ulong)rarity << 32)
                | hashId;
        }

        private bool TryBuildDescriptor(int itemHashId, out InventoryGrid.InventoryItemDescriptor descriptor)
        {
            descriptor = default;
            if (!TryGetRuntimeDescriptor(itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor))
                return false;

            descriptor = new InventoryGrid.InventoryItemDescriptor(
                runtimeDescriptor.HashId,
                runtimeDescriptor.Width,
                runtimeDescriptor.Height,
                runtimeDescriptor.MaxStack,
                runtimeDescriptor.Weight,
                runtimeDescriptor.CategoryId,
                0,
                runtimeDescriptor.Stackable);
            return descriptor.IsValid;
        }

        private bool TryApplyPlacements(ItemPlacement[] placements, int placementCount)
        {
            if (_grid == null || placements == null || !_stackCounts.IsCreated)
                return false;

            _grid.Clear();
            ClearNativeArray(_stackCounts);
            ClearNativeArray(_craftLockedCounts);
            ClearNativeArray(_anchorStateFlags);
            ClearNativeArray(_itemStateFlags);
            ClearNativeArray(_qualityMilli);
            ClearNativeArray(_lastUpdateUnixSeconds);
            TotalWeight = 0f;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                ItemPlacement placement = placements[placementIndex];
                InventoryGrid.InventoryItemDescriptor descriptor = placement.Descriptor;
                if (!descriptor.IsValid || !_grid.PlaceAt(in descriptor, placement.x, placement.y))
                    return false;

                int anchorIndex = AnchorIndex(placement.x, placement.y);
                _stackCounts[anchorIndex] = (ushort)Mathf.Max(1, placement.stackCount);
                if (_craftLockedCounts.IsCreated)
                    _craftLockedCounts[anchorIndex] = placement.lockedCount;
                if (_itemStateFlags.IsCreated)
                    _itemStateFlags[anchorIndex] = placement.stateFlags;
                if (_qualityMilli.IsCreated)
                    _qualityMilli[anchorIndex] = placement.qualityMilli;
                if (_lastUpdateUnixSeconds.IsCreated)
                    _lastUpdateUnixSeconds[anchorIndex] = placement.lastUpdateUnixSeconds;
                TotalWeight += placement.weight * Mathf.Max(1, placement.stackCount);
            }

            return true;
        }

        private static bool TryFindPlacementIndex(ItemPlacement[] placements, int placementCount, int anchorX, int anchorY, out int placementIndex)
        {
            for (int i = 0; i < placementCount; i++)
            {
                if (placements[i].x == anchorX && placements[i].y == anchorY)
                {
                    placementIndex = i;
                    return true;
                }
            }

            placementIndex = -1;
            return false;
        }

        private static InventorySortEntry BuildInventorySortEntry(in ItemPlacement placement, int originalIndex)
        {
            if (placement.itemHashId == 0)
            {
                return new InventorySortEntry
                {
                    PackedKey = PackInventorySortKey(byte.MaxValue, byte.MaxValue, uint.MaxValue),
                    OriginalIndex = originalIndex
                };
            }

            return new InventorySortEntry
            {
                PackedKey = PackInventorySortKey(placement.categoryId, placement.rarity, unchecked((uint)placement.itemHashId)),
                OriginalIndex = originalIndex
            };
        }

        private void NotifyInventoryChanged()
        {
            InventoryVersion++;
            InventoryChanged?.Invoke();
        }

        private bool TryGetRuntimeDescriptor(int itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor)
        {
            runtimeDescriptor = default;
            return itemCatalog != null &&
                   itemHashId != 0 &&
                   itemCatalog.TryGetRuntimeDescriptor(itemHashId, out runtimeDescriptor);
        }

        private void ClearCraftReservationState()
        {
            ClearNativeArray(_craftLockedCounts);
            ClearNativeArray(_anchorStateFlags);
        }

        private static uint ResolveCurrentUnixTimestamp()
        {
            long utcNowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (utcNowSeconds <= 0L)
                return 0u;

            return utcNowSeconds >= uint.MaxValue ? uint.MaxValue : (uint)utcNowSeconds;
        }

        private static ushort ResolveLoadedQualityMilli(InventoryDTO dto, int index)
        {
            if (dto.qualityMilli == null || (uint)index >= (uint)dto.qualityMilli.Length)
                return DefaultQualityMilli;

            return dto.qualityMilli[index] > 0 ? dto.qualityMilli[index] : DefaultQualityMilli;
        }

        private static uint ResolveLoadedTimestamp(InventoryDTO dto, int index)
        {
            if (dto.lastUpdateUnixSeconds == null || (uint)index >= (uint)dto.lastUpdateUnixSeconds.Length)
                return 0u;

            return dto.lastUpdateUnixSeconds[index];
        }

        private static ushort ResolveLoadedItemStateFlags(InventoryDTO dto, int index, ushort fallbackFlags)
        {
            if (dto.itemStateFlags == null || (uint)index >= (uint)dto.itemStateFlags.Length)
                return fallbackFlags;

            ushort savedFlags = dto.itemStateFlags[index];
            return savedFlags != 0 ? savedFlags : fallbackFlags;
        }

        private void ApplyLoadedBiologicalDecay(int anchorIndex)
        {
            if (!_itemStateFlags.IsCreated ||
                !_qualityMilli.IsCreated ||
                !_lastUpdateUnixSeconds.IsCreated ||
                (uint)anchorIndex >= (uint)_itemStateFlags.Length ||
                (_itemStateFlags[anchorIndex] & BiologicalItemStateMask) == 0)
            {
                return;
            }

            uint nowTimestamp = ResolveCurrentUnixTimestamp();
            uint lastTimestamp = _lastUpdateUnixSeconds[anchorIndex];
            if (lastTimestamp == 0u)
            {
                _lastUpdateUnixSeconds[anchorIndex] = nowTimestamp;
                if (_qualityMilli[anchorIndex] == 0)
                    _qualityMilli[anchorIndex] = DefaultQualityMilli;
                return;
            }

            float ambientTemperature = survival != null ? survival.EnvironmentTemperature : 2f;
            float tempFactor = math.exp((ambientTemperature - 4f) * 0.05f);
            uint elapsedSeconds = nowTimestamp >= lastTimestamp ? nowTimestamp - lastTimestamp : 0u;
            float currentQuality = math.clamp((_qualityMilli[anchorIndex] > 0 ? _qualityMilli[anchorIndex] : DefaultQualityMilli) / 1000f, 0f, 1f);
            float decayedQuality = math.clamp(currentQuality - (elapsedSeconds * 0.001f * tempFactor), 0f, 1f);
            _qualityMilli[anchorIndex] = (ushort)math.clamp((int)math.round(decayedQuality * 1000f), 0, 1000);
            _lastUpdateUnixSeconds[anchorIndex] = nowTimestamp;
        }

        private void ReleaseCraftReservationsRange(CraftReservation[] reservations, int startIndex, int endExclusive)
        {
            if (reservations == null || !_craftLockedCounts.IsCreated || !_anchorStateFlags.IsCreated)
                return;

            int max = Mathf.Min(endExclusive, reservations.Length);
            for (int i = startIndex; i < max; i++)
            {
                CraftReservation reservation = reservations[i];
                int anchorIndex = reservation.AnchorIndex;
                if ((uint)anchorIndex < (uint)_craftLockedCounts.Length && reservation.Quantity > 0)
                {
                    _craftLockedCounts[anchorIndex] = (ushort)Mathf.Max(0, _craftLockedCounts[anchorIndex] - reservation.Quantity);
                    if (_craftLockedCounts[anchorIndex] == 0)
                        _anchorStateFlags[anchorIndex] = (ushort)(_anchorStateFlags[anchorIndex] & ~CraftingLockedMask);
                }

                reservations[i] = default;
            }
        }

        private bool IsValidCraftReservation(in CraftReservation reservation)
        {
            if (_grid == null || !_stackCounts.IsCreated || reservation.Quantity <= 0 || (uint)reservation.AnchorIndex >= (uint)_stackCounts.Length)
                return false;

            if (!_grid.HasAnchor(reservation.AnchorIndex) || _grid.GetAnchorHashId(reservation.AnchorIndex) != reservation.ItemHashId)
                return false;

            if (GetReservedCraftCount(reservation.AnchorIndex) < reservation.Quantity)
                return false;

            return Mathf.Max(1, (int)_stackCounts[reservation.AnchorIndex]) >= reservation.Quantity;
        }

        private static unsafe void ClearNativeArray(NativeArray<ushort> array)
        {
            if (!array.IsCreated)
                return;

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<ushort>());
        }

        private static unsafe void ClearNativeArray(NativeArray<uint> array)
        {
            if (!array.IsCreated)
                return;

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(destinationPtr, array.Length * UnsafeUtility.SizeOf<uint>());
        }

        private static unsafe void CopyNativeArray(NativeArray<ushort> source, NativeArray<ushort> destination)
        {
            if (!source.IsCreated || !destination.IsCreated)
                return;

            int copyLength = math.min(source.Length, destination.Length);
            if (copyLength <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(source);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, copyLength * UnsafeUtility.SizeOf<ushort>());
        }
    }
}
