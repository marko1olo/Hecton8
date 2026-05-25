using Hecton8.Core.Memory;
using Hecton8.Logistics.Grid.Contracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Read-only lease for a registered WFC outpost native grid.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly struct WfcOutpostGridLease
    {
        public readonly WfcOutpostGridDescriptor Descriptor;
        public readonly NativeArray<byte> Cells;

        public WfcOutpostGridLease(in WfcOutpostGridDescriptor descriptor, NativeArray<byte> cells)
        {
            Descriptor = descriptor;
            Cells = cells;
        }
    }

    /// <summary>
    /// Fixed-slot native handoff registry between WFC generation and logistics power boot.
    /// </summary>
    public static class WfcOutpostGridRegistry
    {
        private const int SlotCount = 4;
        private const int DataVaultExemptGridSlotCount = SlotCount;
        private const SystemID LogisticsGridSystemId = (SystemID)512;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        // COLD ALLOC: NativeArray<byte>[4] - registered WFC grid slot handles - owner: WfcOutpostGridRegistry
        private static readonly NativeArray<byte>[] _gridSlots = new NativeArray<byte>[DataVaultExemptGridSlotCount];
        // COLD ALLOC: WfcOutpostGridDescriptor[4] - registered WFC grid descriptors - owner: WfcOutpostGridRegistry
        private static readonly WfcOutpostGridDescriptor[] _descriptors = new WfcOutpostGridDescriptor[SlotCount];
        // COLD ALLOC: uint[4] - stable WFC grid handles - owner: WfcOutpostGridRegistry
        private static readonly uint[] _handles = new uint[SlotCount];
        private static uint _nextHandle = 1u;
        private static int _nextSlot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                NativeArray<byte> slot = _gridSlots[i];
                if (slot.IsCreated)
                    H8Memory.Release(ref slot, LogisticsGridSystemId);

                _gridSlots[i] = slot;
                _descriptors[i] = default;
                _handles[i] = 0u;
            }

            _nextHandle = 1u;
            _nextSlot = 0;
        }

        public static bool RegisterGrid(in WfcOutpostGridDescriptor descriptor, NativeArray<byte> cells, out uint handle)
        {
            handle = 0u;
            if (!cells.IsCreated)
                return false;

            return RegisterGrid(in descriptor, cells.AsReadOnly(), out handle);
        }

        public static bool RegisterGrid(in WfcOutpostGridDescriptor descriptor, NativeArray<byte>.ReadOnly cells, out uint handle)
        {
            handle = 0u;
            if (!cells.IsCreated || !IsValidDescriptor(in descriptor))
                return false;

            int expectedCount = descriptor.Dimensions.x * descriptor.Dimensions.y * descriptor.Dimensions.z;
            int cellCount = math.min(
                math.min(cells.Length, descriptor.CellCount),
                math.min(WfcOutpostGridConstants.MaxCellCount, expectedCount));
            if (cellCount <= 0)
                return false;

            int slot = FindSlot(descriptor.SectorHash, descriptor.GenerationSequence);
            if (slot < 0)
                slot = ReserveSlot();

            if (!EnsureSlot(slot))
                return false;

            _handles[slot] = 0u;
            _descriptors[slot] = default;
            NativeArray<byte> destination = _gridSlots[slot];
            for (int i = 0; i < cellCount; i++)
                destination[i] = cells[i];
            for (int i = cellCount; i < destination.Length; i++)
                destination[i] = 0;

            WfcOutpostGridDescriptor stored = descriptor;
            stored.CellCount = (ushort)cellCount;
            if (stored.GridHash == 0u)
                stored.GridHash = ComputeGridHash(destination, cellCount, descriptor.SectorHash, descriptor.GenerationSequence);

            handle = NextHandle(slot);
            _descriptors[slot] = stored;
            _handles[slot] = handle;
            return true;
        }

        private static bool IsValidDescriptor(in WfcOutpostGridDescriptor descriptor)
        {
            int3 dimensions = descriptor.Dimensions;
            if (descriptor.SectorHash == 0UL ||
                descriptor.GenerationSequence == 0u ||
                descriptor.CellCount == 0 ||
                dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0 ||
                dimensions.x > WfcOutpostGridConstants.FullWidth ||
                dimensions.y > WfcOutpostGridConstants.FullHeight ||
                dimensions.z > WfcOutpostGridConstants.FullDepth)
            {
                return false;
            }

            int expectedCount = dimensions.x * dimensions.y * dimensions.z;
            if (expectedCount <= 0 || descriptor.CellCount > expectedCount)
                return false;

            float3 originLocal = new float3(
                descriptor.OriginAup.LocalX,
                descriptor.OriginAup.LocalY,
                descriptor.OriginAup.LocalZ);
            return math.all(math.isfinite(originLocal)) &&
                   math.isfinite(descriptor.CellSizeMeters) &&
                   math.isfinite(descriptor.FloorHeightMeters) &&
                   descriptor.CellSizeMeters >= 1f &&
                   descriptor.FloorHeightMeters >= 1f;
        }

        public static bool TryGetGrid(uint handle, out WfcOutpostGridLease lease)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_handles[i] != handle || !_gridSlots[i].IsCreated)
                    continue;

                lease = new WfcOutpostGridLease(in _descriptors[i], _gridSlots[i]);
                return true;
            }

            lease = default;
            return false;
        }

        public static void ReleaseGrid(uint handle)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_handles[i] != handle)
                    continue;

                _handles[i] = 0u;
                _descriptors[i] = default;
                if (_gridSlots[i].IsCreated)
                {
                    NativeArray<byte> slot = _gridSlots[i];
                    for (int cellIndex = 0; cellIndex < slot.Length; cellIndex++)
                        slot[cellIndex] = 0;
                    _gridSlots[i] = slot;
                }

                return;
            }
        }

        private static int FindSlot(ulong sectorHash, uint generationSequence)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_handles[i] == 0u)
                    continue;

                WfcOutpostGridDescriptor descriptor = _descriptors[i];
                if (descriptor.SectorHash == sectorHash && descriptor.GenerationSequence == generationSequence)
                    return i;
            }

            return -1;
        }

        private static int ReserveSlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_handles[i] == 0u)
                    return i;
            }

            int slot = _nextSlot;
            _nextSlot = (_nextSlot + 1) % SlotCount;
            return slot;
        }

        private static bool EnsureSlot(int slot)
        {
            if ((uint)slot >= SlotCount)
                return false;

            if (_gridSlots[slot].IsCreated)
                return true;

            _gridSlots[slot] = H8Memory.Allocate<byte>(
                WfcOutpostGridConstants.MaxCellCount,
                LogisticsGridSystemId,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[500] - registered WFC grid copy - owner: WfcOutpostGridRegistry
            return _gridSlots[slot].IsCreated;
        }

        private static uint NextHandle(int slot)
        {
            uint generation = _nextHandle++;
            if (_nextHandle == 0u)
                _nextHandle = 1u;

            return (generation << 3) | (uint)(slot + 1);
        }

        private static uint ComputeGridHash(NativeArray<byte> cells, int cellCount, ulong sectorHash, uint generationSequence)
        {
            uint hash = FnvOffset;
            hash = (hash ^ (uint)sectorHash) * FnvPrime;
            hash = (hash ^ (uint)(sectorHash >> 32)) * FnvPrime;
            hash = (hash ^ generationSequence) * FnvPrime;
            for (int i = 0; i < cellCount; i++)
                hash = (hash ^ cells[i]) * FnvPrime;
            return hash == 0u ? 1u : hash;
        }
    }
}
