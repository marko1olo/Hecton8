using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physics
{
    internal static unsafe class TetherBlackBoxDumpWriter
    {
        private const int HeaderBytes = 32;
        private const uint Version = 1u;
        private const string DumpPayloadLabel = "tetherBlackBoxDumpPayload";

        public static void WritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            TryWritePrimaryAndLegacy(primaryH8DumpPath, legacyBinPath, magic, ring, head, reasonFlags);
        }

        public static bool TryWritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            if ((string.IsNullOrEmpty(primaryH8DumpPath) && string.IsNullOrEmpty(legacyBinPath)) ||
                !ring.IsCreated ||
                ring.Length <= 0)
            {
                return false;
            }

            int recordBytes = UnsafeUtility.SizeOf<T>();
            if (recordBytes <= 0)
            {
                return false;
            }

            int count = ring.Length;
            if (head < 0 || head >= count)
            {
                head = 0;
            }

            long payloadBytesLong = HeaderBytes + (long)count * recordBytes;
            if (payloadBytesLong <= 0 || payloadBytesLong > int.MaxValue)
            {
                return false;
            }

            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    (int)payloadBytesLong,
                    nameof(TetherBlackBoxDumpWriter),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                int cursor = 0;

                WriteUInt64(destination, ref cursor, magic);
                WriteUInt32(destination, ref cursor, Version);
                WriteUInt32(destination, ref cursor, reasonFlags);
                WriteUInt32(destination, ref cursor, unchecked((uint)count));
                WriteUInt32(destination, ref cursor, unchecked((uint)recordBytes));
                WriteUInt32(destination, ref cursor, unchecked((uint)head));
                WriteUInt32(destination, ref cursor, 0u);

                for (int i = 0; i < count; i++)
                {
                    int sourceIndex = PositiveModulo(head + i, count);
                    if (!UnsafeMemoryCopyGuard.SafeCopy(
                            destination + cursor,
                            payload.Length - cursor,
                            source + sourceIndex * recordBytes,
                            recordBytes))
                    {
                        return false;
                    }

                    cursor += recordBytes;
                }

                bool wroteAny = false;
                bool ok = true;
                if (!string.IsNullOrEmpty(primaryH8DumpPath))
                {
                    wroteAny = true;
                    ok &= NativeFaultDumpWriter.TryWriteAll(primaryH8DumpPath, payload, cursor);
                }

                if (!string.IsNullOrEmpty(legacyBinPath))
                {
                    wroteAny = true;
                    ok &= NativeFaultDumpWriter.TryWriteAll(legacyBinPath, payload, cursor);
                }

                return wroteAny && ok;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(TetherBlackBoxDumpWriter),
                    DumpPayloadLabel);
            }
        }

        public static bool TryQueuePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            return TryWritePrimaryAndLegacy(primaryH8DumpPath, legacyBinPath, magic, ring, head, reasonFlags);
        }

        private static int PositiveModulo(int value, int length)
        {
            int safeLength = Math.Max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        private static void WriteUInt32(byte* destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += sizeof(uint);
        }

        private static void WriteUInt64(byte* destination, ref int cursor, ulong value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            destination[cursor + 4] = (byte)(value >> 32);
            destination[cursor + 5] = (byte)(value >> 40);
            destination[cursor + 6] = (byte)(value >> 48);
            destination[cursor + 7] = (byte)(value >> 56);
            cursor += sizeof(ulong);
        }
    }
}
