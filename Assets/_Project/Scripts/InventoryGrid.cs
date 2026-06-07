// ============================================================================
// HECTON-8 - InventoryGrid.cs
// Native SOA inventory occupancy + item metadata store.
// Runtime truth is numeric and contiguous. Managed ItemData resolution is UI-only.
// ============================================================================

namespace Hecton8.Inventory
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Memory;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    public sealed class InventoryGrid
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public readonly struct InventoryItemDescriptor
        {
            public readonly int HashId;
            public readonly byte Width;
            public readonly byte Height;
            public readonly ushort MaxStack;
            public readonly float Weight;
            public readonly byte CategoryId;
            public readonly byte Rarity;
            public readonly byte Stackable;

            public InventoryItemDescriptor(
                int hashId,
                byte width,
                byte height,
                ushort maxStack,
                float weight,
                byte categoryId,
                byte rarity,
                bool stackable)
            {
                HashId = hashId;
                Width = width;
                Height = height;
                MaxStack = maxStack;
                Weight = weight;
                CategoryId = categoryId;
                Rarity = rarity;
                Stackable = stackable ? (byte)1 : (byte)0;
            }

        }

        private readonly int _columns;
        private readonly int _rows;
        private NativeArray<int> _cellAnchorIndices;
        private NativeArray<int> _anchorHashIds;
        private NativeArray<byte> _anchorWidths;
        private NativeArray<byte> _anchorHeights;
        private NativeArray<ushort> _anchorMaxStacks;
        private NativeArray<float> _anchorWeights;
        private NativeArray<byte> _anchorCategoryIds;
        private NativeArray<byte> _anchorRarityIds;
        private NativeArray<byte> _anchorFlags;
        private int _occupiedCells;
        private ulong _singleCellFreeMask;
        private const SystemID NativeArrayOwnerSystem = SystemID.GameplayLoot;

        public int Columns => _columns;
        public int Rows => _rows;
        public int TotalCells => _columns * _rows;
        public int OccupiedCells => _occupiedCells;
        public int FreeCells => TotalCells - _occupiedCells;
        public bool IsFull => _occupiedCells >= TotalCells;

        public NativeArray<int>.ReadOnly AnchorHashIds => _anchorHashIds.IsCreated ? _anchorHashIds.AsReadOnly() : default;
        public NativeArray<byte>.ReadOnly AnchorWidths => _anchorWidths.IsCreated ? _anchorWidths.AsReadOnly() : default;
        public NativeArray<byte>.ReadOnly AnchorHeights => _anchorHeights.IsCreated ? _anchorHeights.AsReadOnly() : default;
        public NativeArray<ushort>.ReadOnly AnchorMaxStacks => _anchorMaxStacks.IsCreated ? _anchorMaxStacks.AsReadOnly() : default;
        public NativeArray<float>.ReadOnly AnchorWeights => _anchorWeights.IsCreated ? _anchorWeights.AsReadOnly() : default;
        public NativeArray<byte>.ReadOnly AnchorCategoryIds => _anchorCategoryIds.IsCreated ? _anchorCategoryIds.AsReadOnly() : default;
        public NativeArray<byte>.ReadOnly AnchorRarityIds => _anchorRarityIds.IsCreated ? _anchorRarityIds.AsReadOnly() : default;
        public NativeArray<byte>.ReadOnly AnchorFlags => _anchorFlags.IsCreated ? _anchorFlags.AsReadOnly() : default;

        public static bool IsValidDescriptor(in InventoryItemDescriptor descriptor)
        {
            return descriptor.HashId != 0 && descriptor.Width > 0 && descriptor.Height > 0;
        }

        public InventoryGrid(int columns, int rows)
        {
            _columns = columns;
            _rows = rows;

            int totalCells = columns * rows;
            _cellAnchorIndices = H8Memory.Allocate<int>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorHashIds = H8Memory.Allocate<int>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorWidths = H8Memory.Allocate<byte>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorHeights = H8Memory.Allocate<byte>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorMaxStacks = H8Memory.Allocate<ushort>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorWeights = H8Memory.Allocate<float>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorCategoryIds = H8Memory.Allocate<byte>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorRarityIds = H8Memory.Allocate<byte>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _anchorFlags = H8Memory.Allocate<byte>(totalCells, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (totalCells > 0 && !AllNativeArraysCreated())
            {
                Dispose(default);
                throw new InvalidOperationException("InventoryGrid native allocation failed.");
            }

            _occupiedCells = 0;
            _singleCellFreeMask = BuildFirst64Mask(totalCells);
        }

        public void Dispose(JobHandle dependency)
        {
            JobHandle disposeHandle = dependency;
            bool hasDependency = !dependency.IsCompleted;
            if (!hasDependency)
                DispatcherJobFence.TryFinalizeCompleted(ref disposeHandle);

            DisposeNativeArray(ref _cellAnchorIndices, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorHashIds, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorWidths, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorHeights, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorMaxStacks, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorWeights, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorCategoryIds, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorRarityIds, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _anchorFlags, ref disposeHandle, ref hasDependency);

            if (hasDependency)
                DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true);

            _occupiedCells = 0;
            _singleCellFreeMask = 0UL;
        }

        private bool AllNativeArraysCreated()
        {
            return _cellAnchorIndices.IsCreated &&
                   _anchorHashIds.IsCreated &&
                   _anchorWidths.IsCreated &&
                   _anchorHeights.IsCreated &&
                   _anchorMaxStacks.IsCreated &&
                   _anchorWeights.IsCreated &&
                   _anchorCategoryIds.IsCreated &&
                   _anchorRarityIds.IsCreated &&
                   _anchorFlags.IsCreated;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle dependency, ref bool hasDependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            if (hasDependency)
            {
                dependency = H8Memory.Release(ref array, dependency, NativeArrayOwnerSystem);
            }
            else
            {
                H8Memory.Release(ref array, NativeArrayOwnerSystem);
            }
        }

        public bool HasAnchor(int anchorIndex)
        {
            return _anchorHashIds.IsCreated &&
                   (uint)anchorIndex < (uint)_anchorHashIds.Length &&
                   _anchorHashIds[anchorIndex] != 0;
        }

        public int GetAnchorHashId(int anchorIndex)
        {
            return HasAnchor(anchorIndex) ? _anchorHashIds[anchorIndex] : 0;
        }

        public int GetAnchorWidth(int anchorIndex)
        {
            return HasAnchor(anchorIndex) ? _anchorWidths[anchorIndex] : 0;
        }

        public int GetAnchorHeight(int anchorIndex)
        {
            return HasAnchor(anchorIndex) ? _anchorHeights[anchorIndex] : 0;
        }

        public int GetAnchorMaxStack(int anchorIndex)
        {
            return HasAnchor(anchorIndex) ? _anchorMaxStacks[anchorIndex] : 0;
        }

        public float GetAnchorWeight(int anchorIndex)
        {
            return HasAnchor(anchorIndex) ? _anchorWeights[anchorIndex] : 0f;
        }

        public bool TryGetAnchorDescriptor(int anchorIndex, out InventoryItemDescriptor descriptor)
        {
            if (!HasAnchor(anchorIndex))
            {
                descriptor = default;
                return false;
            }

            descriptor = new InventoryItemDescriptor(
                _anchorHashIds[anchorIndex],
                _anchorWidths[anchorIndex],
                _anchorHeights[anchorIndex],
                _anchorMaxStacks[anchorIndex],
                _anchorWeights[anchorIndex],
                _anchorCategoryIds[anchorIndex],
                _anchorRarityIds[anchorIndex],
                (_anchorFlags[anchorIndex] & 0x01) != 0);
            return true;
        }

        public int GetCellAnchorIndex(int x, int y)
        {
            if ((uint)x >= (uint)_columns || (uint)y >= (uint)_rows || !_cellAnchorIndices.IsCreated)
                return -1;

            int encodedIndex = _cellAnchorIndices[CellIndex(x, y)];
            return encodedIndex > 0 ? encodedIndex - 1 : -1;
        }

        public bool IsCellOccupied(int x, int y)
        {
            return GetCellAnchorIndex(x, y) >= 0;
        }

        public int GetCellHashId(int x, int y)
        {
            int anchorIndex = GetCellAnchorIndex(x, y);
            return anchorIndex >= 0 ? _anchorHashIds[anchorIndex] : 0;
        }

        public bool TryGetCellDescriptor(int x, int y, out InventoryItemDescriptor descriptor)
        {
            int anchorIndex = GetCellAnchorIndex(x, y);
            return TryGetAnchorDescriptor(anchorIndex, out descriptor);
        }

        public void CopyOccupiedMask(NativeArray<byte> destination)
        {
            if (!destination.IsCreated || destination.Length < TotalCells || !_cellAnchorIndices.IsCreated)
                return;

            for (int i = 0; i < TotalCells; i++)
                destination[i] = _cellAnchorIndices[i] != 0 ? (byte)1 : (byte)0;
        }

        public bool TryAddItem(in InventoryItemDescriptor descriptor, out int placedX, out int placedY)
        {
            if (!IsValidDescriptor(in descriptor) || !_cellAnchorIndices.IsCreated)
            {
                placedX = -1;
                placedY = -1;
                return false;
            }

            int width = descriptor.Width;
            int height = descriptor.Height;
            if (width > _columns || height > _rows || _occupiedCells >= TotalCells)
            {
                placedX = -1;
                placedY = -1;
                return false;
            }

            if (width == 1 &&
                height == 1 &&
                TryAddSingleCellItem(in descriptor, out placedX, out placedY))
            {
                return true;
            }

            int maxX = _columns - width;
            int maxY = _rows - height;
            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    if (_cellAnchorIndices[CellIndex(x, y)] != 0)
                        continue;

                    if (!CheckFitInternal(x, y, width, height))
                        continue;

                    PlaceDescriptor(in descriptor, x, y);
                    placedX = x;
                    placedY = y;
                    return true;
                }
            }

            placedX = -1;
            placedY = -1;
            return false;
        }

        public bool CheckFit(int startX, int startY, int width, int height)
        {
            if (startX < 0 || startY < 0)
                return false;

            if (startX + width > _columns || startY + height > _rows)
                return false;

            return CheckFitInternal(startX, startY, width, height);
        }

        public bool PlaceAt(in InventoryItemDescriptor descriptor, int x, int y)
        {
            if (!IsValidDescriptor(in descriptor) || !CheckFit(x, y, descriptor.Width, descriptor.Height))
                return false;

            PlaceDescriptor(in descriptor, x, y);
            return true;
        }

        public void RemoveItem(int x, int y, int width, int height)
        {
            int anchorIndex = GetCellAnchorIndex(x, y);
            if (anchorIndex >= 0)
            {
                RemoveAnchorAt(anchorIndex);
                return;
            }

            int x0 = x < 0 ? 0 : x;
            int y0 = y < 0 ? 0 : y;
            int x1 = x + width;
            int y1 = y + height;
            if (x1 > _columns)
                x1 = _columns;
            if (y1 > _rows)
                y1 = _rows;

            for (int iy = y0; iy < y1; iy++)
            {
                for (int ix = x0; ix < x1; ix++)
                {
                    int cellAnchorIndex = GetCellAnchorIndex(ix, iy);
                    if (cellAnchorIndex >= 0)
                        RemoveAnchorAt(cellAnchorIndex);
                }
            }
        }

        public void RemoveAnchorAt(int anchorIndex)
        {
            if (!TryGetAnchorDescriptor(anchorIndex, out InventoryItemDescriptor descriptor))
                return;

            DecodeCellIndex(anchorIndex, out int anchorX, out int anchorY);
            int endX = anchorX + descriptor.Width;
            int endY = anchorY + descriptor.Height;
            for (int y = anchorY; y < endY; y++)
            {
                for (int x = anchorX; x < endX; x++)
                {
                    int cellIndex = CellIndex(x, y);
                    if (_cellAnchorIndices[cellIndex] != 0)
                    {
                        _cellAnchorIndices[cellIndex] = 0;
                        _occupiedCells--;
                    }
                }
            }

            ClearAnchorMetadata(anchorIndex);
        }

        public bool TryMoveOrSwapAnchor(int sourceAnchorIndex, int targetAnchorIndex, int targetAnchorX, int targetAnchorY)
        {
            if (!TryGetAnchorDescriptor(sourceAnchorIndex, out InventoryItemDescriptor sourceDescriptor))
                return false;

            DecodeCellIndex(sourceAnchorIndex, out int sourceAnchorX, out int sourceAnchorY);
            bool hasTargetAnchor = targetAnchorIndex >= 0;
            InventoryItemDescriptor targetDescriptor = default;
            if (hasTargetAnchor && !TryGetAnchorDescriptor(targetAnchorIndex, out targetDescriptor))
                return false;

            targetAnchorX = targetAnchorX < 0 ? 0 : (targetAnchorX >= _columns ? _columns - 1 : targetAnchorX);
            targetAnchorY = targetAnchorY < 0 ? 0 : (targetAnchorY >= _rows ? _rows - 1 : targetAnchorY);

            if (!CheckFitExcludingAnchors(targetAnchorX, targetAnchorY, sourceDescriptor.Width, sourceDescriptor.Height, sourceAnchorIndex, targetAnchorIndex))
                return false;

            if (hasTargetAnchor &&
                !CheckFitExcludingAnchors(sourceAnchorX, sourceAnchorY, targetDescriptor.Width, targetDescriptor.Height, sourceAnchorIndex, targetAnchorIndex))
            {
                return false;
            }

            ClearAnchorCells(sourceAnchorIndex, sourceDescriptor.Width, sourceDescriptor.Height);
            if (hasTargetAnchor)
                ClearAnchorCells(targetAnchorIndex, targetDescriptor.Width, targetDescriptor.Height);

            if (hasTargetAnchor)
            {
                CopyAnchorMetadata(targetAnchorIndex, in sourceDescriptor);
                CopyAnchorMetadata(sourceAnchorIndex, in targetDescriptor);
                FillAnchorCells(targetAnchorIndex, targetAnchorX, targetAnchorY, sourceDescriptor.Width, sourceDescriptor.Height);
                FillAnchorCells(sourceAnchorIndex, sourceAnchorX, sourceAnchorY, targetDescriptor.Width, targetDescriptor.Height);
            }
            else
            {
                int destinationAnchorIndex = CellIndex(targetAnchorX, targetAnchorY);
                CopyAnchorMetadata(destinationAnchorIndex, in sourceDescriptor);
                ClearAnchorMetadata(sourceAnchorIndex);
                FillAnchorCells(destinationAnchorIndex, targetAnchorX, targetAnchorY, sourceDescriptor.Width, sourceDescriptor.Height);
            }

            return true;
        }

        public void Clear()
        {
            if (!_cellAnchorIndices.IsCreated)
            {
                _occupiedCells = 0;
                _singleCellFreeMask = 0UL;
                return;
            }

            for (int i = 0; i < TotalCells; i++)
            {
                _cellAnchorIndices[i] = 0;
                _anchorHashIds[i] = 0;
                _anchorWidths[i] = 0;
                _anchorHeights[i] = 0;
                _anchorMaxStacks[i] = 0;
                _anchorWeights[i] = 0f;
                _anchorCategoryIds[i] = 0;
                _anchorRarityIds[i] = 0;
                _anchorFlags[i] = 0;
            }

            _occupiedCells = 0;
            _singleCellFreeMask = BuildFirst64Mask(TotalCells);
        }

        private int CellIndex(int x, int y)
        {
            return y * _columns + x;
        }

        private void DecodeCellIndex(int cellIndex, out int x, out int y)
        {
            y = cellIndex / _columns;
            x = cellIndex - (y * _columns);
        }

        private bool CheckFitInternal(int startX, int startY, int width, int height)
        {
            int endX = startX + width;
            int endY = startY + height;
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if (_cellAnchorIndices[CellIndex(x, y)] != 0)
                        return false;
                }
            }

            return true;
        }

        private bool TryAddSingleCellItem(in InventoryItemDescriptor descriptor, out int placedX, out int placedY)
        {
            int totalCells = TotalCells;
            ulong availableMask = _singleCellFreeMask;
            if (availableMask != 0UL)
            {
                int anchorIndex = (int)math.tzcnt(availableMask);
                DecodeCellIndex(anchorIndex, out placedX, out placedY);
                PlaceDescriptor(in descriptor, placedX, placedY);
                return true;
            }

            for (int anchorIndex = 64; anchorIndex < totalCells; anchorIndex++)
            {
                if (_anchorHashIds[anchorIndex] != 0 || _cellAnchorIndices[anchorIndex] != 0)
                    continue;

                DecodeCellIndex(anchorIndex, out placedX, out placedY);
                PlaceDescriptor(in descriptor, placedX, placedY);
                return true;
            }

            placedX = -1;
            placedY = -1;
            return false;
        }

        private static ulong BuildFirst64Mask(int totalCells)
        {
            int cappedCells = math.clamp(totalCells, 0, 64);
            return cappedCells == 64 ? ulong.MaxValue : ((1UL << cappedCells) - 1UL);
        }

        private void SetSingleCellFreeBit(int cellIndex)
        {
            if ((uint)cellIndex >= 64u)
                return;

            _singleCellFreeMask |= 1UL << cellIndex;
        }

        private void ClearSingleCellFreeBit(int cellIndex)
        {
            if ((uint)cellIndex >= 64u)
                return;

            _singleCellFreeMask &= ~(1UL << cellIndex);
        }

        private bool CheckFitExcludingAnchors(int startX, int startY, int width, int height, int ignoredAnchorA, int ignoredAnchorB)
        {
            if (startX < 0 || startY < 0 || width <= 0 || height <= 0)
                return false;

            int endX = startX + width;
            int endY = startY + height;
            if (endX > _columns || endY > _rows)
                return false;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int encodedAnchorIndex = _cellAnchorIndices[CellIndex(x, y)];
                    if (encodedAnchorIndex == 0)
                        continue;

                    int anchorIndex = encodedAnchorIndex - 1;
                    if (anchorIndex == ignoredAnchorA || anchorIndex == ignoredAnchorB)
                        continue;

                    return false;
                }
            }

            return true;
        }

        private void PlaceDescriptor(in InventoryItemDescriptor descriptor, int x, int y)
        {
            int anchorIndex = CellIndex(x, y);
            CopyAnchorMetadata(anchorIndex, in descriptor);
            FillAnchorCells(anchorIndex, x, y, descriptor.Width, descriptor.Height);
        }

        private void CopyAnchorMetadata(int anchorIndex, in InventoryItemDescriptor descriptor)
        {
            _anchorHashIds[anchorIndex] = descriptor.HashId;
            _anchorWidths[anchorIndex] = descriptor.Width;
            _anchorHeights[anchorIndex] = descriptor.Height;
            _anchorMaxStacks[anchorIndex] = descriptor.MaxStack;
            _anchorWeights[anchorIndex] = descriptor.Weight;
            _anchorCategoryIds[anchorIndex] = descriptor.CategoryId;
            _anchorRarityIds[anchorIndex] = descriptor.Rarity;
            _anchorFlags[anchorIndex] = descriptor.Stackable != 0 ? (byte)0x01 : (byte)0x00;
        }

        private void FillAnchorCells(int anchorIndex, int startX, int startY, int width, int height)
        {
            int encodedAnchorIndex = anchorIndex + 1;
            int endX = startX + width;
            int endY = startY + height;
            for (int cellY = startY; cellY < endY; cellY++)
            {
                for (int cellX = startX; cellX < endX; cellX++)
                {
                    int cellIndex = CellIndex(cellX, cellY);
                    _cellAnchorIndices[cellIndex] = encodedAnchorIndex;
                    ClearSingleCellFreeBit(cellIndex);
                }
            }

            _occupiedCells += width * height;
        }

        private void ClearAnchorCells(int anchorIndex, int width, int height)
        {
            DecodeCellIndex(anchorIndex, out int anchorX, out int anchorY);
            int endX = anchorX + width;
            int endY = anchorY + height;
            for (int y = anchorY; y < endY; y++)
            {
                for (int x = anchorX; x < endX; x++)
                {
                    int cellIndex = CellIndex(x, y);
                    if (_cellAnchorIndices[cellIndex] != 0)
                    {
                        _cellAnchorIndices[cellIndex] = 0;
                        _occupiedCells--;
                        if (_anchorHashIds[cellIndex] == 0)
                            SetSingleCellFreeBit(cellIndex);
                    }
                }
            }
        }

        private void ClearAnchorMetadata(int anchorIndex)
        {
            _anchorHashIds[anchorIndex] = 0;
            _anchorWidths[anchorIndex] = 0;
            _anchorHeights[anchorIndex] = 0;
            _anchorMaxStacks[anchorIndex] = 0;
            _anchorWeights[anchorIndex] = 0f;
            _anchorCategoryIds[anchorIndex] = 0;
            _anchorRarityIds[anchorIndex] = 0;
            _anchorFlags[anchorIndex] = 0;
            if ((uint)anchorIndex < (uint)_cellAnchorIndices.Length &&
                _cellAnchorIndices[anchorIndex] == 0)
            {
                SetSingleCellFreeBit(anchorIndex);
            }
        }
    }
}
