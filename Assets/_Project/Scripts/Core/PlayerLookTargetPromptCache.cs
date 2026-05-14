using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed prompt text cache for hash-only player look target signals.
    /// </summary>
    public static class PlayerLookTargetPromptCache
    {
        private const int SetCount = 16;
        private const int WayCount = 4;
        private const int SlotCount = SetCount * WayCount;
        private const int MaxCharsPerPrompt = 64;
        private const int CharCapacity = SlotCount * MaxCharsPerPrompt;

        // COLD ALLOC: uint[64] - prompt hash slots - owner: PlayerLookTargetPromptCache
        private static readonly uint[] s_hashes = new uint[SlotCount];

        // COLD ALLOC: byte[64] - prompt text lengths - owner: PlayerLookTargetPromptCache
        private static readonly byte[] s_lengths = new byte[SlotCount];

        // COLD ALLOC: byte[64] - prompt replacement ages - owner: PlayerLookTargetPromptCache
        private static readonly byte[] s_ages = new byte[SlotCount];

        // COLD ALLOC: char[4096] - bounded prompt text slab - owner: PlayerLookTargetPromptCache
        private static readonly char[] s_chars = new char[CharCapacity];
        private static byte s_storeEpoch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Array.Clear(s_hashes, 0, s_hashes.Length);
            Array.Clear(s_lengths, 0, s_lengths.Length);
            Array.Clear(s_ages, 0, s_ages.Length);
            Array.Clear(s_chars, 0, s_chars.Length);
            s_storeEpoch = 0;
        }

        /// <summary>
        /// Stores a bounded copy of prompt text for later zero-GC UI staging.
        /// </summary>
        public static void Store(uint promptHash, string prompt)
        {
            if (promptHash == 0u || string.IsNullOrEmpty(prompt))
                return;

            byte writeAge = NextAge();
            int slot = ResolveSlotForStore(promptHash, writeAge);
            int sourceLength = prompt.Length;
            int length = sourceLength > MaxCharsPerPrompt ? MaxCharsPerPrompt : sourceLength;
            int offset = slot * MaxCharsPerPrompt;

            for (int i = 0; i < length; i++)
                s_chars[offset + i] = prompt[i];

            s_lengths[slot] = (byte)length;
            s_ages[slot] = writeAge;
            s_hashes[slot] = promptHash;
        }

        /// <summary>
        /// Copies cached prompt text into caller-owned storage.
        /// </summary>
        public static bool TryCopyTo(uint promptHash, char[] destination, int capacity, out int sourceLength)
        {
            sourceLength = 0;
            if (promptHash == 0u || destination == null || capacity <= 0)
                return false;

            int slot = ResolveSlotForRead(promptHash);
            if (slot < 0)
                return false;

            int length = s_lengths[slot];
            if (length > capacity)
                length = capacity;
            if (length > destination.Length)
                length = destination.Length;
            if (length <= 0)
                return false;

            int offset = slot * MaxCharsPerPrompt;
            for (int i = 0; i < length; i++)
                destination[i] = s_chars[offset + i];

            sourceLength = length;
            return true;
        }

        private static int ResolveSlotForRead(uint promptHash)
        {
            int setStart = ResolveSetStart(promptHash);
            for (int i = 0; i < WayCount; i++)
            {
                int slot = setStart + i;
                if (s_hashes[slot] == promptHash)
                    return slot;
            }

            return -1;
        }

        private static int ResolveSlotForStore(uint promptHash, byte writeAge)
        {
            int setStart = ResolveSetStart(promptHash);
            int oldestSlot = setStart;
            int oldestDistance = -1;
            for (int i = 0; i < WayCount; i++)
            {
                int slot = setStart + i;
                uint hash = s_hashes[slot];
                if (hash == promptHash || hash == 0u)
                    return slot;

                int ageDistance = unchecked((byte)(writeAge - s_ages[slot]));
                if (ageDistance > oldestDistance)
                {
                    oldestDistance = ageDistance;
                    oldestSlot = slot;
                }
            }

            return oldestSlot;
        }

        private static int ResolveSetStart(uint promptHash)
        {
            uint foldedHash = promptHash ^ (promptHash >> 16);
            return (int)(foldedHash & (SetCount - 1)) * WayCount;
        }

        private static byte NextAge()
        {
            unchecked
            {
                s_storeEpoch++;
                if (s_storeEpoch == 0)
                    s_storeEpoch = 1;

                return s_storeEpoch;
            }
        }
    }
}
