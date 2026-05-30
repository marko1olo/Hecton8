using System.Runtime.InteropServices;
using Hecton8.Core.Memory.Layout;
using Hecton8.World;
using Unity.Collections;

namespace Hecton8.AI
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = FaunaTier1LodProxyRegistry.EntrySizeBytes)]
    internal struct FaunaTier1LodProxyEntry
    {
        private const int FlagsShift = 0;
        private const int HeadingShift = 8;
        private const int HealthShift = 12;
        private const int HungerShift = 20;
        private const int QualityShift = 28;
        private const uint ByteMask = 0xFFu;
        private const uint NibbleMask = 0x0Fu;

        [FieldOffset(0)]
        public AbsoluteUniversePositionBlit128 PositionAup;

        [FieldOffset(48)]
        public uint InstanceUid;

        [FieldOffset(52)]
        public ushort SpeciesId;

        [FieldOffset(54)]
        private ushort _pad0;

        [FieldOffset(56)]
        public uint StatusFlags;

        [FieldOffset(60)]
        public uint Reserved1;

        public static uint PackStatusFlags(byte flags, byte headingOctant, byte health01, byte hunger01, byte qualityTier)
        {
            uint packed = flags;
            packed |= (uint)(headingOctant & NibbleMask) << HeadingShift;
            packed |= (uint)health01 << HealthShift;
            packed |= (uint)hunger01 << HungerShift;
            packed |= (uint)(qualityTier & NibbleMask) << QualityShift;
            return packed;
        }

        public static byte ReadFlags(uint statusFlags)
        {
            return (byte)((statusFlags >> FlagsShift) & ByteMask);
        }

        public static byte ReadHeadingOctant(uint statusFlags)
        {
            return (byte)((statusFlags >> HeadingShift) & NibbleMask);
        }

        public static byte ReadHealth01(uint statusFlags)
        {
            return (byte)((statusFlags >> HealthShift) & ByteMask);
        }

        public static byte ReadHunger01(uint statusFlags)
        {
            return (byte)((statusFlags >> HungerShift) & ByteMask);
        }

        public static byte ReadQualityTier(uint statusFlags)
        {
            return (byte)((statusFlags >> QualityShift) & NibbleMask);
        }
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
            if (!_initialized)
            {
                entry = default;
                return false;
            }

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
            if (!_initialized || !destination.IsCreated || destination.Length <= 0 || _activeCount <= 0)
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
