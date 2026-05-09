using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Fixed zero-GC character buffer pool for transient HUD formatting work.
    /// </summary>
    internal static class CharBufferPool
    {
        private const int SlotCount = 16;
        internal const int RequiredVrTextCapacity = 256;
        private const int SlotLength = RequiredVrTextCapacity;
        private const uint AllSlotsMask = (1u << SlotCount) - 1u;

        // COLD ALLOC: char[16][256] — transient VR HUD text staging pool — owner: CharBufferPool
        private static readonly char[][] s_slots =
        {
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength],
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength],
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength],
            new char[SlotLength], new char[SlotLength], new char[SlotLength], new char[SlotLength]
        };

        private static uint _slotMask = AllSlotsMask;

        internal static int AvailableSlotCount => (int)math.countbits(_slotMask & AllSlotsMask);
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _slotMask = AllSlotsMask;
            Prewarm();
        }

        public static void Prewarm()
        {
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                char[] buffer = s_slots[slotIndex];
                if (buffer.Length < RequiredVrTextCapacity)
                    continue;

                buffer[0] = '\0';
                buffer[RequiredVrTextCapacity - 1] = '\0';
            }
        }

        public static bool TryAcquire(out Lease lease)
        {
            uint availableMask = _slotMask & AllSlotsMask;
            if (availableMask == 0u)
            {
                _slotMask = 0u;
                lease = default;
                return false;
            }

            if (availableMask != _slotMask)
                _slotMask = availableMask;

            int slotIndex = (int)math.tzcnt(availableMask);
            if ((uint)slotIndex >= SlotCount)
            {
                lease = default;
                return false;
            }

            _slotMask = availableMask & ~(1u << slotIndex);
            lease = new Lease(slotIndex, s_slots[slotIndex]);
            return true;
        }

        public static void Release(in Lease lease)
        {
            if (!lease.IsValid || (uint)lease.SlotIndex >= SlotCount)
                return;

            if (!ReferenceEquals(lease.Buffer, s_slots[lease.SlotIndex]))
                return;

            uint slotBit = 1u << lease.SlotIndex;
            if ((_slotMask & slotBit) != 0u)
                return;

            _slotMask = (_slotMask | slotBit) & AllSlotsMask;
        }
    }
}
