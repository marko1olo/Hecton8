using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core.Memory
{
    public static class AlignmentTelemetryFlags
    {
        public const uint None = 0u;
        public const uint Pack1Detected = 1u << 0;
        public const uint MisalignedEightByteField = 1u << 1;
        public const uint InvalidStride = 1u << 2;
        public const uint DynamicCastFault = 1u << 3;
        public const uint DumpWritten = 1u << 4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AlignmentTelemetryEntry
    {
        [FieldOffset(0)] public ulong StructHash;
        [FieldOffset(8)] public ulong OffendingAddress;
        [FieldOffset(16)] public double3 AupOrRuntimePosition;
        [FieldOffset(40)] public uint BufferID;
        [FieldOffset(44)] public uint ByteOffset;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float Severity01;
        [FieldOffset(60)] public uint StateHash;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeAlignedBufferJob : IJobParallelFor
    {
        public const int CacheLineBytes = 64;
        public const int ULongsPerCacheLine = CacheLineBytes / sizeof(ulong);

        [NoAlias, NativeDisableUnsafePtrRestriction] public void* BufferPtr;
        public long ByteLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateBatchCount(long byteLength)
        {
            return byteLength <= 0L
                ? 0
                : (int)((byteLength + CacheLineBytes - 1L) / CacheLineBytes);
        }

        public void Execute(int index)
        {
            long byteOffset = (long)index * CacheLineBytes;
            long remaining = ByteLength - byteOffset;
            if (remaining <= 0L)
                return;

            byte* target = (byte*)BufferPtr + byteOffset;
            if (remaining >= CacheLineBytes)
            {
                ulong* words = (ulong*)target;
                words[0] = 0UL;
                words[1] = 0UL;
                words[2] = 0UL;
                words[3] = 0UL;
                words[4] = 0UL;
                words[5] = 0UL;
                words[6] = 0UL;
                words[7] = 0UL;
                return;
            }

            for (int i = 0; i < remaining; i++)
                target[i] = 0;
        }
    }

    public static class Arm64AlignmentTelemetry
    {
        public const int Capacity = 300;
        private const ulong DumpMagic = 0x3430325F55424F53UL; // SOBU_204 low-endian marker
        private const int DumpVersion = 1;
        private const int DumpHeaderBytes = sizeof(ulong) + (sizeof(int) * 3);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_204.bin";

        private static VaultGenerationHandle<AlignmentTelemetryEntry> _ringHandle;
        private static VaultGenerationHandle<int> _cursorHandle;
        private static IDataVault _ringVault;

        public static bool TryRecordFault(
            IDataVault vault,
            BufferID bufferId,
            ulong structHash,
            uint byteOffset,
            uint frame,
            uint flags,
            double3 aupOrRuntimePosition,
            ulong offendingAddress = 0UL,
            float severity01 = 1f)
        {
            if (vault == null || !EnsureRing(vault))
                return false;

            if (!TryResolveRing(vault, out NativeArray<AlignmentTelemetryEntry> ring) ||
                ring.Length == 0)
            {
                return false;
            }

            if (!TryResolveCursor(vault, out NativeArray<int> cursorBuffer) ||
                cursorBuffer.Length == 0)
            {
                return false;
            }

            int cursor = cursorBuffer[0];
            if ((uint)cursor >= (uint)ring.Length)
                cursor = 0;

            AlignmentTelemetryEntry entry = default;
            entry.StructHash = structHash;
            entry.OffendingAddress = offendingAddress;
            entry.AupOrRuntimePosition = aupOrRuntimePosition;
            entry.BufferID = (uint)bufferId;
            entry.ByteOffset = byteOffset;
            entry.Frame = frame;
            entry.Flags = flags;
            entry.Severity01 = math.saturate(math.isfinite(severity01) ? severity01 : 1f);
            entry.StateHash = HashEntry(structHash, (uint)bufferId, byteOffset, frame, flags);
            ring[cursor] = entry;

            cursor++;
            if (cursor >= ring.Length)
                cursor = 0;
            cursorBuffer[0] = cursor;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryWriteFaultHistory(ring, cursorBuffer);
#endif
            return true;
        }

        public static bool TryGetNewestFault(IDataVault vault, out AlignmentTelemetryEntry entry)
        {
            entry = default;
            if (vault == null || !TryResolveRing(vault, out NativeArray<AlignmentTelemetryEntry> ring))
                return false;

            if (ring.Length == 0 ||
                !TryResolveCursor(vault, out NativeArray<int> cursorBuffer) ||
                cursorBuffer.Length == 0)
            {
                return false;
            }

            int newest = cursorBuffer[0] - 1;
            if (newest < 0)
                newest = ring.Length - 1;

            entry = ring[newest];
            return entry.StateHash != 0u;
        }

        public static bool DumpFaultHistory(IDataVault vault)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (vault == null || !EnsureRing(vault))
                return false;

            if (!TryResolveRing(vault, out NativeArray<AlignmentTelemetryEntry> ring) ||
                ring.Length == 0)
            {
                return false;
            }

            if (!TryResolveCursor(vault, out NativeArray<int> cursorBuffer) ||
                cursorBuffer.Length == 0)
            {
                return false;
            }

            return TryWriteFaultHistory(ring, cursorBuffer);
#else
            return false;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool TryWriteFaultHistory(
            NativeArray<AlignmentTelemetryEntry> ring,
            NativeArray<int> cursorBuffer)
        {
            if (!ring.IsCreated ||
                ring.Length == 0 ||
                !cursorBuffer.IsCreated ||
                cursorBuffer.Length == 0)
            {
                return false;
            }

            string directory = Path.GetDirectoryName(DumpRelativePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(DumpRelativePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                int rowBytes = UnsafeUtility.SizeOf<AlignmentTelemetryEntry>();
                Span<byte> header = stackalloc byte[DumpHeaderBytes];
                WriteUInt64LittleEndian(header, DumpMagic);
                WriteInt32LittleEndian(header.Slice(sizeof(ulong)), DumpVersion);
                WriteInt32LittleEndian(header.Slice(sizeof(ulong) + sizeof(int)), ring.Length);
                WriteInt32LittleEndian(header.Slice(sizeof(ulong) + (sizeof(int) * 2)), rowBytes);
                stream.Write(header);

                int start = cursorBuffer[0];
                for (int i = 0; i < ring.Length; i++)
                {
                    int index = (start + i) % ring.Length;
                    AlignmentTelemetryEntry entry = ring[index];
                    unsafe
                    {
                        stream.Write(new ReadOnlySpan<byte>(UnsafeUtility.AddressOf(ref entry), rowBytes));
                    }
                }
            }

            return true;
        }
#endif

        private static bool EnsureRing(IDataVault vault)
        {
            if (_ringVault == vault &&
                TryResolveRing(vault, out NativeArray<AlignmentTelemetryEntry> existingRing) &&
                existingRing.Length >= Capacity &&
                TryResolveCursor(vault, out NativeArray<int> existingCursor) &&
                existingCursor.Length > 0)
            {
                return true;
            }

            if (_ringVault != null &&
                _ringVault != vault)
            {
                if (_ringHandle.BufferID != 0u)
                    _ringVault.ReleaseBuffer(in _ringHandle);
                _ringHandle = default;
                if (_cursorHandle.BufferID != 0u)
                    _ringVault.ReleaseBuffer(in _cursorHandle);
                _cursorHandle = default;
            }

            _ringHandle = vault.GetGenerationHandle<AlignmentTelemetryEntry>(
                BufferID.Arm64AlignmentTelemetryRing,
                Capacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);

            _cursorHandle = vault.GetGenerationHandle<int>(
                BufferID.Arm64AlignmentTelemetryCursor,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);

            _ringVault = vault;
            if (!TryResolveRing(vault, out NativeArray<AlignmentTelemetryEntry> ring) ||
                ring.Length < Capacity)
            {
                return false;
            }

            if (!TryResolveCursor(vault, out NativeArray<int> cursor) ||
                cursor.Length == 0)
            {
                return false;
            }

            ClearRing(ring);
            cursor[0] = 0;
            return true;
        }

        private static unsafe void ClearRing(NativeArray<AlignmentTelemetryEntry> ring)
        {
            if (!ring.IsCreated ||
                ring.Length == 0)
            {
                return;
            }

            UnsafeUtility.MemClear(
                ring.GetUnsafePtr(),
                (long)ring.Length * UnsafeUtility.SizeOf<AlignmentTelemetryEntry>());
        }

        private static bool TryResolveRing(IDataVault vault, out NativeArray<AlignmentTelemetryEntry> ring)
        {
            ring = default;
            return vault != null &&
                   _ringHandle.BufferID != 0u &&
                   vault.TryResolveHandle(in _ringHandle, out ring) &&
                   ring.IsCreated;
        }

        private static bool TryResolveCursor(IDataVault vault, out NativeArray<int> cursor)
        {
            cursor = default;
            return vault != null &&
                   _cursorHandle.BufferID != 0u &&
                   vault.TryResolveHandle(in _cursorHandle, out cursor) &&
                   cursor.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashEntry(ulong structHash, uint bufferId, uint byteOffset, uint frame, uint flags)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)structHash) * 16777619u;
            hash = (hash ^ (uint)(structHash >> 32)) * 16777619u;
            hash = (hash ^ bufferId) * 16777619u;
            hash = (hash ^ byteOffset) * 16777619u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt64LittleEndian(Span<byte> target, ulong value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
            target[4] = (byte)(value >> 32);
            target[5] = (byte)(value >> 40);
            target[6] = (byte)(value >> 48);
            target[7] = (byte)(value >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32LittleEndian(Span<byte> target, int value)
        {
            uint unsigned = (uint)value;
            target[0] = (byte)unsigned;
            target[1] = (byte)(unsigned >> 8);
            target[2] = (byte)(unsigned >> 16);
            target[3] = (byte)(unsigned >> 24);
        }
    }
}
