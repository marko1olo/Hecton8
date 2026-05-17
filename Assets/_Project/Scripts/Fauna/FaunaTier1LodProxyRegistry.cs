using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Collections;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct FaunaTier1LodProxyEntry
    {
        public AbsoluteUniversePositionBlit128 PositionAup;
        public uint InstanceUid;
        public ushort SpeciesId;
        public byte Flags;
        public byte HeadingOctant;
        public byte Health01;
        public byte Hunger01;
        public byte QualityTier;
        public byte Reserved0;
        public uint Reserved1;
    }

    internal static class FaunaTier1LodProxyRegistry
    {
        internal const int MaxTier1ProxyCount = 512;
        internal const int EntrySizeBytes = 64;
        internal const byte FlagDataOnly = 1 << 0;
        internal const byte FlagPredator = 1 << 1;
        internal const byte FlagApex = 1 << 2;
        internal const byte FlagLargeThreat = 1 << 3;
        internal const byte FlagDead = 1 << 4;

        // COLD ALLOC: FaunaTier1LodProxyEntry[512] - fixed Tier1 fauna visual proxy slab - owner: FaunaTier1LodProxyRegistry
        private static readonly FaunaTier1LodProxyEntry[] _entries = new FaunaTier1LodProxyEntry[MaxTier1ProxyCount];
        // COLD ALLOC: byte[512] - fixed occupied bitmap for Tier1 fauna proxy slab - owner: FaunaTier1LodProxyRegistry
        private static readonly byte[] _occupied = new byte[MaxTier1ProxyCount];
        // COLD ALLOC: int[512] - fixed free-slot stack for Tier1 fauna proxy handles - owner: FaunaTier1LodProxyRegistry
        private static readonly int[] _freeSlots = new int[MaxTier1ProxyCount];

        private static bool _initialized;
        private static int _freeSlotCount;
        private static int _activeCount;

        internal static int ActiveCount => _activeCount;
        internal static int SlotCapacity => MaxTier1ProxyCount;

        internal static int RegisterOrUpdate(int handle, in FaunaTier1LodProxyEntry entry)
        {
            EnsureInitialized();

            if (TryResolveHandleSlot(handle, out int existingSlot))
            {
                _entries[existingSlot] = entry;
                return handle;
            }

            if (!TryAllocateSlot(out int slot))
                return 0;

            _occupied[slot] = 1;
            _entries[slot] = entry;
            _activeCount++;
            return slot + 1;
        }

        internal static void Unregister(ref int handle)
        {
            EnsureInitialized();
            if (!TryResolveHandleSlot(handle, out int slot))
            {
                handle = 0;
                return;
            }

            _entries[slot] = default;
            _occupied[slot] = 0;
            _freeSlots[_freeSlotCount++] = slot;
            _activeCount--;
            handle = 0;
        }

        internal static bool TryReadSlot(int slot, out FaunaTier1LodProxyEntry entry)
        {
            EnsureInitialized();
            if ((uint)slot >= MaxTier1ProxyCount || _occupied[slot] == 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[slot];
            return true;
        }

        internal static int CopyActiveEntries(NativeArray<FaunaTier1LodProxyEntry> destination)
        {
            EnsureInitialized();
            if (!destination.IsCreated || destination.Length <= 0 || _activeCount <= 0)
                return 0;

            int written = 0;
            for (int slot = 0; slot < MaxTier1ProxyCount && written < destination.Length; slot++)
            {
                if (_occupied[slot] == 0)
                    continue;

                destination[written++] = _entries[slot];
            }

            return written;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            for (int i = 0; i < MaxTier1ProxyCount; i++)
                _freeSlots[i] = MaxTier1ProxyCount - 1 - i;

            _freeSlotCount = MaxTier1ProxyCount;
            _initialized = true;
        }

        private static bool TryAllocateSlot(out int slot)
        {
            while (_freeSlotCount > 0)
            {
                slot = _freeSlots[--_freeSlotCount];
                if (_occupied[slot] == 0)
                    return true;
            }

            slot = -1;
            return false;
        }

        private static bool TryResolveHandleSlot(int handle, out int slot)
        {
            slot = handle - 1;
            return (uint)slot < MaxTier1ProxyCount && _occupied[slot] != 0;
        }
    }
}
