using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Fixed zero-GC character buffer pool for transient HUD and Babel formatting work.
    /// </summary>
    internal static class CharBufferPool
    {
        private const int SlotCount = 500;
        private const int MaskWordBits = 64;
        private const int MaskWordCount = (SlotCount + MaskWordBits - 1) / MaskWordBits;
        internal const int RequiredBabelTextCapacity = 512;
        internal const int RequiredVrTextCapacity = 256;
        internal const int EncyclopediaPageCapacity = 32768;
        private const int SlotLength = RequiredVrTextCapacity;
        private const int EncyclopediaPageSlotCount = 4;
        private const int BabelArenaLength = SlotCount * RequiredBabelTextCapacity;
        private const BufferID BabelArenaBufferId = (BufferID)70540;

        // COLD ALLOC: char[500][256] - legacy VR HUD TMP staging pool - owner: CharBufferPool
        private static readonly char[][] s_slots = CreateSlots(SlotLength);
        // COLD ALLOC: char[500][512] - TMP bridge and no-vault Babel span fallback - owner: CharBufferPool
        private static readonly char[][] s_babelTmpBridges = CreateSlots(RequiredBabelTextCapacity);
        // COLD ALLOC: char[4][32768] - long-form PDA encyclopedia TMP staging pages - owner: CharBufferPool
        private static readonly char[][] s_encyclopediaPages = CreateSlots(EncyclopediaPageSlotCount, EncyclopediaPageCapacity);
        // COLD ALLOC: ulong[8] - fixed free-slot bitmap for CharBufferPool - owner: CharBufferPool
        private static readonly ulong[] s_freeMasks = CreateFreeMasks();
        private static ulong s_encyclopediaFreeMask = CreateEncyclopediaFreeMask();
        private static IDataVault s_babelArenaVault;
        private static VaultGenerationHandle<char> s_babelArenaHandle;
        private static bool s_babelArenaVaultBacked;
        private static bool s_babelArenaProbeCompleted;
        private static int s_activeLeaseCount;

        internal static int AvailableSlotCount => SlotCount - s_activeLeaseCount;
        internal static int AvailableEncyclopediaPageCount => CountBits(s_encyclopediaFreeMask);
        internal static int SlotCapacity => SlotLength;
        internal static int BabelNativeArenaLength => BabelArenaLength;

        internal readonly struct Lease
        {
            public readonly int SlotIndex;
            public readonly char[] Buffer;

            public Lease(int slotIndex, char[] buffer)
            {
                SlotIndex = slotIndex;
                Buffer = buffer;
            }

            public bool IsValid => SlotIndex >= 0 && Buffer != null;
        }

        internal readonly struct BabelLease
        {
            public readonly int SlotIndex;
            public readonly char[] TmpBuffer;

            public BabelLease(int slotIndex, char[] tmpBuffer)
            {
                SlotIndex = slotIndex;
                TmpBuffer = tmpBuffer;
            }

            public bool IsValid => SlotIndex >= 0 && TmpBuffer != null;

            public Span<char> Span => GetBabelSpan(SlotIndex);

            public int CopyToTmpBuffer(int length)
            {
                return CopyBabelNativeToTmp(SlotIndex, TmpBuffer, length);
            }
        }

        internal readonly struct EncyclopediaLease
        {
            public readonly int SlotIndex;
            public readonly char[] Buffer;

            public EncyclopediaLease(int slotIndex, char[] buffer)
            {
                SlotIndex = slotIndex;
                Buffer = buffer;
            }

            public bool IsValid => SlotIndex >= 0 && Buffer != null;

            public Span<char> Span => Buffer != null ? Buffer.AsSpan() : Span<char>.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseBabelArenaHandle();
            s_babelArenaProbeCompleted = false;
            ResetFreeMasks();
            ResetEncyclopediaFreeMask();
            s_activeLeaseCount = 0;
            LocRegistry.ReportBufferPoolLeasesActive(0);
            Prewarm();
        }

        public static void Prewarm()
        {
            EnsureBabelArena(forceProbe: true);
            bool hasNativeArena = TryResolveBabelArena(out NativeArray<char> babelArena);
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                char[] buffer = s_slots[slotIndex];
                buffer[0] = '\0';
                buffer[RequiredVrTextCapacity - 1] = '\0';

                char[] babelBridge = s_babelTmpBridges[slotIndex];
                babelBridge[0] = '\0';
                babelBridge[RequiredBabelTextCapacity - 1] = '\0';

                if (hasNativeArena)
                {
                    int nativeBase = slotIndex * RequiredBabelTextCapacity;
                    babelArena[nativeBase] = '\0';
                    babelArena[nativeBase + RequiredBabelTextCapacity - 1] = '\0';
                }
            }

            for (int slotIndex = 0; slotIndex < EncyclopediaPageSlotCount; slotIndex++)
            {
                char[] page = s_encyclopediaPages[slotIndex];
                page[0] = '\0';
                page[EncyclopediaPageCapacity - 1] = '\0';
            }
        }

        public static bool TryAcquire(out Lease lease)
        {
            if (TryAcquireSlot(out int slotIndex))
            {
                lease = new Lease(slotIndex, s_slots[slotIndex]);
                return true;
            }

            lease = default;
            return false;
        }

        public static bool TryAcquireBabel(out BabelLease lease)
        {
            if (TryAcquireSlot(out int slotIndex))
            {
                lease = new BabelLease(slotIndex, s_babelTmpBridges[slotIndex]);
                return true;
            }

            lease = default;
            return false;
        }

        public static bool TryAcquireEncyclopedia(out EncyclopediaLease lease)
        {
            if (TryAcquireEncyclopediaSlot(out int slotIndex))
            {
                lease = new EncyclopediaLease(slotIndex, s_encyclopediaPages[slotIndex]);
                return true;
            }

            lease = default;
            return false;
        }

        public static bool TryAcquireArenaSpan(int minimumCapacity, out Span<char> span)
        {
            int safeCapacity = math.clamp(minimumCapacity, 1, SlotLength);
            return HectonArenaAllocator.TryAllocateCharSpan(safeCapacity, out span);
        }

        public static void Release(in Lease lease)
        {
            if (!lease.IsValid || (uint)lease.SlotIndex >= SlotCount)
                return;

            if (!ReferenceEquals(lease.Buffer, s_slots[lease.SlotIndex]))
                return;

            ReleaseSlot(lease.SlotIndex);
        }

        public static void Release(in BabelLease lease)
        {
            if (!lease.IsValid || (uint)lease.SlotIndex >= SlotCount)
                return;

            if (!ReferenceEquals(lease.TmpBuffer, s_babelTmpBridges[lease.SlotIndex]))
                return;

            ReleaseSlot(lease.SlotIndex);
        }

        public static void Release(in EncyclopediaLease lease)
        {
            if (!lease.IsValid || (uint)lease.SlotIndex >= EncyclopediaPageSlotCount)
                return;

            if (!ReferenceEquals(lease.Buffer, s_encyclopediaPages[lease.SlotIndex]))
                return;

            ReleaseEncyclopediaSlot(lease.SlotIndex);
        }

        private static bool TryAcquireSlot(out int slotIndex)
        {
            for (int wordIndex = 0; wordIndex < MaskWordCount; wordIndex++)
            {
                ulong availableMask = s_freeMasks[wordIndex];
                if (availableMask == 0UL)
                    continue;

                int baseSlot = wordIndex * MaskWordBits;
                for (int bit = 0; bit < MaskWordBits; bit++)
                {
                    int candidateSlot = baseSlot + bit;
                    if (candidateSlot >= SlotCount)
                        break;

                    ulong slotBit = 1UL << bit;
                    if ((availableMask & slotBit) == 0UL)
                        continue;

                    s_freeMasks[wordIndex] = availableMask & ~slotBit;
                    s_activeLeaseCount = math.min(SlotCount, s_activeLeaseCount + 1);
                    LocRegistry.ReportBufferPoolLeasesActive(s_activeLeaseCount);
                    slotIndex = candidateSlot;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        private static void ReleaseSlot(int slotIndex)
        {
            int wordIndex = slotIndex / MaskWordBits;
            int bit = slotIndex - (wordIndex * MaskWordBits);
            ulong slotBit = 1UL << bit;
            if ((s_freeMasks[wordIndex] & slotBit) != 0UL)
                return;

            s_freeMasks[wordIndex] |= slotBit;
            s_activeLeaseCount = math.max(0, s_activeLeaseCount - 1);
            LocRegistry.ReportBufferPoolLeasesActive(s_activeLeaseCount);
        }

        private static bool TryAcquireEncyclopediaSlot(out int slotIndex)
        {
            ulong availableMask = s_encyclopediaFreeMask;
            if (availableMask == 0UL)
            {
                slotIndex = -1;
                return false;
            }

            for (int bit = 0; bit < EncyclopediaPageSlotCount; bit++)
            {
                ulong slotBit = 1UL << bit;
                if ((availableMask & slotBit) == 0UL)
                    continue;

                s_encyclopediaFreeMask = availableMask & ~slotBit;
                slotIndex = bit;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        private static void ReleaseEncyclopediaSlot(int slotIndex)
        {
            if ((uint)slotIndex >= EncyclopediaPageSlotCount)
                return;

            s_encyclopediaFreeMask |= 1UL << slotIndex;
        }

        private static Span<char> GetBabelSpan(int slotIndex)
        {
            if ((uint)slotIndex >= SlotCount)
                return Span<char>.Empty;

            if (TryResolveBabelArena(out NativeArray<char> babelArena))
            {
                unsafe
                {
                    char* basePtr = (char*)NativeArrayUnsafeUtility.GetUnsafePtr(babelArena);
                    return new Span<char>(basePtr + (slotIndex * RequiredBabelTextCapacity), RequiredBabelTextCapacity);
                }
            }

            return s_babelTmpBridges[slotIndex].AsSpan();
        }

        private static int CopyBabelNativeToTmp(int slotIndex, char[] tmpBuffer, int length)
        {
            if ((uint)slotIndex >= SlotCount || tmpBuffer == null)
                return 0;

            int safeLength = math.clamp(length, 0, math.min(RequiredBabelTextCapacity, tmpBuffer.Length));
            Span<char> source = GetBabelSpan(slotIndex);
            for (int i = 0; i < safeLength; i++)
                tmpBuffer[i] = source[i];

            if (safeLength < tmpBuffer.Length)
                tmpBuffer[safeLength] = '\0';

            return safeLength;
        }

        private static char[][] CreateSlots(int slotLength)
        {
            return CreateSlots(SlotCount, slotLength);
        }

        private static char[][] CreateSlots(int slotCount, int slotLength)
        {
            char[][] slots = new char[slotCount][];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new char[slotLength];

            return slots;
        }

        private static ulong[] CreateFreeMasks()
        {
            ulong[] masks = new ulong[MaskWordCount];
            ResetFreeMasks(masks);
            return masks;
        }

        private static void ResetFreeMasks()
        {
            ResetFreeMasks(s_freeMasks);
        }

        private static void ResetFreeMasks(ulong[] masks)
        {
            if (masks == null)
                return;

            for (int i = 0; i < masks.Length; i++)
                masks[i] = ulong.MaxValue;

            int usedBitsInLastWord = SlotCount - ((MaskWordCount - 1) * MaskWordBits);
            if (usedBitsInLastWord > 0 && usedBitsInLastWord < MaskWordBits)
                masks[MaskWordCount - 1] = (1UL << usedBitsInLastWord) - 1UL;
        }

        private static ulong CreateEncyclopediaFreeMask()
        {
            return (1UL << EncyclopediaPageSlotCount) - 1UL;
        }

        private static void ResetEncyclopediaFreeMask()
        {
            s_encyclopediaFreeMask = CreateEncyclopediaFreeMask();
        }

        private static int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0UL)
            {
                value &= value - 1UL;
                count++;
            }

            return count;
        }

        private static bool EnsureBabelArena(bool forceProbe = false)
        {
            if (s_babelArenaVaultBacked)
            {
                if (TryResolveCurrentVaultArena(out _))
                    return true;

                ClearBabelArenaHandle();
            }

            if (s_babelArenaProbeCompleted && !forceProbe)
                return false;

            s_babelArenaProbeCompleted = true;
            if (TryResolveBabelVault(out IDataVault resolvedVault))
                return TryAcquireVaultBabelArena(resolvedVault);

            return false;
        }

        private static bool TryResolveBabelArena(out NativeArray<char> arena)
        {
            arena = default;
            if (!s_babelArenaVaultBacked && !EnsureBabelArena())
                return false;

            if (TryResolveCurrentVaultArena(out arena))
                return true;

            ClearBabelArenaHandle();
            arena = default;
            return false;
        }

        private static bool TryResolveCurrentVaultArena(out NativeArray<char> arena)
        {
            arena = default;
            if (!s_babelArenaVaultBacked ||
                s_babelArenaVault == null ||
                !IsVaultHandleCreated(in s_babelArenaHandle))
                return false;

            return s_babelArenaVault.TryResolveHandle(in s_babelArenaHandle, out arena) &&
                   arena.IsCreated &&
                   arena.Length >= BabelArenaLength;
        }

        private static bool TryResolveBabelVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            return vault != null;
        }

        private static bool TryAcquireVaultBabelArena(IDataVault vault)
        {
            if (vault == null)
                return false;

            VaultGenerationHandle<char> acquired = vault.EnsureGenerationHandle<char>(
                BabelArenaBufferId,
                BabelArenaLength,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<char> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < BabelArenaLength)
            {
                return false;
            }

            s_babelArenaHandle = acquired;
            s_babelArenaVault = vault;
            s_babelArenaVaultBacked = true;
            return true;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseBabelArenaHandle()
        {
            ReleaseVaultBuffer(s_babelArenaVault, ref s_babelArenaHandle);
            s_babelArenaVault = null;
            s_babelArenaVaultBacked = false;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void ClearBabelArenaHandle()
        {
            s_babelArenaHandle = default;
            s_babelArenaVault = null;
            s_babelArenaVaultBacked = false;
        }
    }
}
