using Hecton8.Core;
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
    public readonly ref struct WfcOutpostGridLease
    {
        public readonly WfcOutpostGridDescriptor Descriptor;
        public readonly NativeArray<byte> Cells;
        public readonly BufferID BufferId;
        public readonly SystemID SystemId;

        public WfcOutpostGridLease(
            in WfcOutpostGridDescriptor descriptor,
            NativeArray<byte> cells,
            BufferID bufferId,
            SystemID systemId)
        {
            Descriptor = descriptor;
            Cells = cells;
            BufferId = bufferId;
            SystemId = systemId;
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

        private const BufferID GridSlotBase = (BufferID)731620;
        private static readonly VaultGenerationHandle<byte>[] _gridSlots = new VaultGenerationHandle<byte>[DataVaultExemptGridSlotCount];
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
                if (_gridSlots[i].BufferID != 0u && TryResolveVault(out IDataVault vault))
                    vault.ReleaseBuffer(in _gridSlots[i]);

                _gridSlots[i] = default;
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

            if (!EnsureSlot(slot, out NativeArray<byte> destination))
                return false;

            if (!TryResolveVault(out IDataVault vault) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(ResolveSlotMutationGuardMask(slot)))
            {
                return false;
            }

            try
            {
                if (!TryResolveSlot(slot, out destination))
                    return false;

                _handles[slot] = 0u;
                _descriptors[slot] = default;
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
            finally
            {
                vault.ReleaseMutationGuard(ResolveSlotMutationGuardMask(slot));
            }
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
            lease = default;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_handles[i] != handle)
                    continue;

                if (!TryResolveVault(out IDataVault vault) ||
                    vault.IsCompactionFenceActive)
                {
                    return false;
                }

                BufferID bufferId = ResolveSlotBufferId(i);
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireMutationGuard(ResolveSlotMutationGuardMask(i)))
                    return false;

                if (!TryResolveSlot(i, out NativeArray<byte> cells) ||
                    _handles[i] != handle)
                {
                    vault.ReleaseMutationGuard(ResolveSlotMutationGuardMask(i));
                    return false;
                }

                lease = new WfcOutpostGridLease(in _descriptors[i], cells, bufferId, LogisticsGridSystemId);
                return true;
            }

            return false;
        }

        public static void ReleaseGridLease(in WfcOutpostGridLease lease)
        {
            ReleaseGridLease(lease.BufferId, lease.SystemId);
        }

        public static void ReleaseGridLease(BufferID bufferId, SystemID systemId)
        {
            if (bufferId == BufferID.Unknown ||
                systemId != LogisticsGridSystemId ||
                !TryResolveSlotIndex(bufferId, out int slot) ||
                !TryResolveVault(out IDataVault vault))
            {
                return;
            }

            vault.ReleaseMutationGuard(ResolveSlotMutationGuardMask(slot));
        }

        public static void ReleaseGrid(uint handle)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_handles[i] != handle)
                    continue;

                if (!TryResolveVault(out IDataVault vault) ||
                    vault.IsCompactionFenceActive ||
                    !vault.TryAcquireMutationGuard(ResolveSlotMutationGuardMask(i)))
                {
                    return;
                }

                try
                {
                    _handles[i] = 0u;
                    _descriptors[i] = default;
                    if (TryResolveSlot(i, out NativeArray<byte> slot))
                    {
                        for (int cellIndex = 0; cellIndex < slot.Length; cellIndex++)
                            slot[cellIndex] = 0;
                    }
                }
                finally
                {
                    vault.ReleaseMutationGuard(ResolveSlotMutationGuardMask(i));
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

        private static bool EnsureSlot(int slot, out NativeArray<byte> cells)
        {
            cells = default;
            if ((uint)slot >= SlotCount)
                return false;

            if (TryResolveSlot(slot, out cells))
                return true;

            if (!TryResolveVault(out IDataVault vault))
                return false;

            _gridSlots[slot] = vault.EnsureGenerationHandle<byte>(
                (BufferID)((int)GridSlotBase + slot),
                WfcOutpostGridConstants.MaxCellCount,
                LogisticsGridSystemId,
                NativeArrayOptions.ClearMemory);
            return TryResolveSlot(slot, out cells);
        }

        private static BufferID ResolveSlotBufferId(int slot)
        {
            return (BufferID)((int)GridSlotBase + slot);
        }

        private static ulong ResolveSlotMutationGuardMask(int slot)
        {
            return SlotMutationGuardBit(ResolveSlotBufferId(slot));
        }

        private static ulong SlotMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static bool TryResolveSlot(int slot, out NativeArray<byte> cells)
        {
            cells = default;
            if ((uint)slot >= SlotCount ||
                !IsSlotHandle(slot, in _gridSlots[slot]) ||
                !TryResolveVault(out IDataVault vault))
            {
                return false;
            }

            return vault.TryResolveHandle(in _gridSlots[slot], out cells) &&
                   cells.IsCreated &&
                   cells.Length >= WfcOutpostGridConstants.MaxCellCount;
        }

        private static bool TryResolveSlotIndex(BufferID bufferId, out int slot)
        {
            int rawSlot = (int)bufferId - (int)GridSlotBase;
            if ((uint)rawSlot >= SlotCount)
            {
                slot = -1;
                return false;
            }

            slot = rawSlot;
            return true;
        }

        private static bool IsSlotHandle(int slot, in VaultGenerationHandle<byte> handle)
        {
            return (uint)slot < SlotCount &&
                   handle.BufferID == unchecked((uint)(int)ResolveSlotBufferId(slot)) &&
                   handle.SystemID == (uint)LogisticsGridSystemId &&
                   handle.Generation != 0u;
        }

        private static bool TryResolveVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            return vault != null && !vault.IsCompactionFenceActive;
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
