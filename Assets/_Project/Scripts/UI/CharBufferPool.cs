using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Fixed zero-GC character buffer pool for transient HUD formatting work.
    /// </summary>
    internal static class CharBufferPool
    {
        private const int SlotCount = 16;
        private const int SlotLength = 512;

        // COLD ALLOC: char[16][] — transient HUD text staging pool — owner: CharBufferPool
        private static readonly char[][] s_slots =
        {
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength],
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength],
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength],
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength]
        };

        private static ushort _slotMask = 0xFFFF;

        internal static int AvailableSlotCount => CountAvailableSlots(_slotMask);
        internal static int SlotCapacity => SlotLength;

        internal readonly struct Lease
        {
            public Lease(int slotIndex, char[] buffer)
            {
                SlotIndex = slotIndex;
                Buffer = buffer;
            }

            public int SlotIndex { get; }
            public char[] Buffer { get; }
            public bool IsValid => SlotIndex >= 0 && Buffer != null;
        }

        public static void Prewarm()
        {
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                char[] buffer = s_slots[slotIndex];
                if (buffer.Length > 0)
                    buffer[0] = '\0';
            }
        }

        public static bool TryAcquire(out Lease lease)
        {
            if (_slotMask == 0)
            {
                lease = default;
                return false;
            }

            int slotIndex = FindFirstFreeSlot(_slotMask);
            if ((uint)slotIndex >= SlotCount)
            {
                lease = default;
                return false;
            }

            _slotMask = (ushort)(_slotMask & ~(1 << slotIndex));
            lease = new Lease(slotIndex, s_slots[slotIndex]);
            return true;
        }

        public static void Release(in Lease lease)
        {
            if (!lease.IsValid || (uint)lease.SlotIndex >= SlotCount)
                return;

            _slotMask = (ushort)(_slotMask | (1 << lease.SlotIndex));
        }

        private static int FindFirstFreeSlot(ushort slotMask)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if ((slotMask & (1 << i)) != 0)
                    return i;
            }

            return -1;
        }

        private static int CountAvailableSlots(ushort slotMask)
        {
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if ((slotMask & (1 << i)) != 0)
                    count++;
            }

            return count;
        }
    }
}
