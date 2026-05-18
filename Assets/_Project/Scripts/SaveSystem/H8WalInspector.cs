using System;
using System.IO;
using Hecton8.Core;

namespace Hecton8.Core.Persistence.Paging
{
    public struct H8WalInspectionSnapshot
    {
        public string WalPath;
        public long FileBytes;
        public long RawPayloadBytes;
        public long StoredPayloadBytes;
        public long HotStateBytes;
        public long LastSectorHash;
        public uint LastPayloadType;
        public uint LastFrame;
        public int PendingTransactions;
        public int CorruptRecords;
    }

    public static unsafe class H8WalInspector
    {
        public const string WalFileName = "h8_delta.wal";
        public const long CommitThresholdBytes = 4L * 1024L * 1024L;
        public const long MicroStallThresholdBytes = 16L * 1024L * 1024L;

        private const uint WalMagic = 0x4C573848u; // H8WL
        private const ushort WalVersion = 1;
        private const int WalHeaderBytes = 64;
        private const int WalTailBytes = 4;
        private const int SectorHeaderBytes = 64;
        private const int MaxPayloadBytes = (256 * 1024) - SectorHeaderBytes;

        public static string ResolveDefaultWalPath()
        {
            return HectonPersistentPathPolicy.CombineFile(WalFileName);
        }

        public static string ResolveWalPathForWorldData(string worldDataPath)
        {
            if (string.IsNullOrEmpty(worldDataPath))
                return ResolveDefaultWalPath();

            string directory = Path.GetDirectoryName(worldDataPath);
            return string.IsNullOrEmpty(directory)
                ? ResolveDefaultWalPath()
                : Path.Combine(directory, WalFileName);
        }

        public static bool TryInspect(string walPath, out H8WalInspectionSnapshot snapshot, out string error)
        {
            snapshot = default;
            error = string.Empty;
            snapshot.WalPath = walPath;

            if (string.IsNullOrEmpty(walPath))
            {
                error = "WAL path is empty.";
                return false;
            }

            if (!File.Exists(walPath))
                return true;

            try
            {
                using FileStream stream = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                snapshot.FileBytes = stream.Length;
                Span<byte> header = stackalloc byte[WalHeaderBytes];
                Span<byte> tail = stackalloc byte[WalTailBytes];
                Span<byte> chunk = stackalloc byte[4096];

                while (stream.Position < stream.Length)
                {
                    if (stream.Length - stream.Position < WalHeaderBytes + WalTailBytes ||
                        !ReadExact(stream, header))
                    {
                        snapshot.CorruptRecords++;
                        break;
                    }

                    fixed (byte* headerPtr = header)
                    {
                        if (!TryReadWalHeader(
                                headerPtr,
                                out long sectorHash,
                                out uint payloadType,
                                out int rawBytes,
                                out int storedBytes,
                                out uint frame,
                                out int hotStateBytes,
                                out uint hotStateCrc))
                        {
                            snapshot.CorruptRecords++;
                            break;
                        }

                        if (stream.Length - stream.Position < storedBytes + hotStateBytes + WalTailBytes)
                        {
                            snapshot.CorruptRecords++;
                            break;
                        }

                        uint crc = UpdateCrc32(0xFFFFFFFFu, headerPtr, WalHeaderBytes);
                        int remaining = storedBytes;
                        while (remaining > 0)
                        {
                            int readLength = Math.Min(remaining, chunk.Length);
                            Span<byte> slice = chunk.Slice(0, readLength);
                            if (!ReadExact(stream, slice))
                            {
                                snapshot.CorruptRecords++;
                                remaining = -1;
                                break;
                            }

                            fixed (byte* chunkPtr = slice)
                            {
                                crc = UpdateCrc32(crc, chunkPtr, readLength);
                            }

                            remaining -= readLength;
                        }

                        uint hotCrc = 0xFFFFFFFFu;
                        remaining = hotStateBytes;
                        while (remaining > 0)
                        {
                            int readLength = Math.Min(remaining, chunk.Length);
                            Span<byte> slice = chunk.Slice(0, readLength);
                            if (!ReadExact(stream, slice))
                            {
                                snapshot.CorruptRecords++;
                                remaining = -1;
                                break;
                            }

                            fixed (byte* chunkPtr = slice)
                            {
                                crc = UpdateCrc32(crc, chunkPtr, readLength);
                                hotCrc = UpdateCrc32(hotCrc, chunkPtr, readLength);
                            }

                            remaining -= readLength;
                        }

                        if (remaining < 0)
                            break;

                        if (hotStateBytes > 0 && FinalizeCrc32(hotCrc) != hotStateCrc)
                        {
                            snapshot.CorruptRecords++;
                            break;
                        }

                        if (!ReadExact(stream, tail))
                        {
                            snapshot.CorruptRecords++;
                            break;
                        }

                        fixed (byte* tailPtr = tail)
                        {
                            uint expected = ReadUInt(tailPtr, 0);
                            if (FinalizeCrc32(crc) != expected)
                            {
                                snapshot.CorruptRecords++;
                                break;
                            }
                        }

                        snapshot.PendingTransactions++;
                        snapshot.RawPayloadBytes += rawBytes;
                        snapshot.StoredPayloadBytes += storedBytes;
                        snapshot.HotStateBytes += hotStateBytes;
                        snapshot.LastSectorHash = sectorHash;
                        snapshot.LastPayloadType = payloadType;
                        snapshot.LastFrame = frame;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public static bool TryCorruptTailBytes(string walPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(walPath) || !File.Exists(walPath))
            {
                error = "WAL path is empty or missing.";
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough);
                if (stream.Length < WalTailBytes)
                {
                    error = "WAL is too small to corrupt tail bytes.";
                    return false;
                }

                Span<byte> poison = stackalloc byte[8];
                for (int i = 0; i < poison.Length; i++)
                    poison[i] = unchecked((byte)(0xA5u + (uint)i));

                stream.Position = Math.Max(0L, stream.Length - poison.Length);
                stream.Write(poison);
                stream.Flush(true);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static bool ReadExact(FileStream stream, Span<byte> destination)
        {
            int total = 0;
            while (total < destination.Length)
            {
                int read = stream.Read(destination.Slice(total));
                if (read <= 0)
                    return false;
                total += read;
            }

            return true;
        }

        public static bool TryCorruptSectorBytes(string walPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(walPath) || !File.Exists(walPath))
            {
                error = "WAL path is empty or missing.";
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough);
                if (stream.Length < WalHeaderBytes + WalTailBytes + 4)
                {
                    error = "WAL is too small to corrupt sector payload bytes.";
                    return false;
                }

                long payloadOffset = WalHeaderBytes + Math.Max(0L, (stream.Length - WalHeaderBytes - WalTailBytes) / 2L);
                if (payloadOffset > stream.Length - WalTailBytes - 4)
                    payloadOffset = stream.Length - WalTailBytes - 4;

                Span<byte> poison = stackalloc byte[4];
                poison[0] = 0xDE;
                poison[1] = 0xAD;
                poison[2] = 0x34;
                poison[3] = 0x8B;
                stream.Position = payloadOffset;
                stream.Write(poison);
                stream.Flush(true);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static bool TryReadWalHeader(
            byte* header,
            out long sectorHash,
            out uint payloadType,
            out int rawBytes,
            out int storedBytes,
            out uint frame,
            out int hotStateBytes,
            out uint hotStateCrc)
        {
            sectorHash = 0L;
            payloadType = 0u;
            rawBytes = 0;
            storedBytes = 0;
            frame = 0u;
            hotStateBytes = 0;
            hotStateCrc = 0u;

            if (ReadUInt(header, 0) != WalMagic ||
                ReadUShort(header, 4) != WalVersion ||
                ReadUShort(header, 6) != WalHeaderBytes)
            {
                return false;
            }

            payloadType = ReadUInt(header, 8);
            sectorHash = ReadLong(header, 16);
            rawBytes = ReadInt(header, 24);
            storedBytes = ReadInt(header, 28);
            frame = ReadUInt(header, 36);
            hotStateBytes = ReadInt(header, 44);
            hotStateCrc = ReadUInt(header, 56);
            return rawBytes > 0 &&
                   rawBytes <= MaxPayloadBytes &&
                   storedBytes > 0 &&
                   storedBytes <= MaxPayloadBytes &&
                   hotStateBytes >= 0 &&
                   hotStateBytes <= 512;
        }

        private static uint UpdateCrc32(uint crc, byte* data, int byteCount)
        {
            for (int i = 0; i < byteCount; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return crc;
        }

        private static uint FinalizeCrc32(uint crc)
        {
            return ~crc;
        }

        private static uint ReadUInt(byte* ptr, int offset)
        {
            return ptr[offset] |
                   ((uint)ptr[offset + 1] << 8) |
                   ((uint)ptr[offset + 2] << 16) |
                   ((uint)ptr[offset + 3] << 24);
        }

        private static ushort ReadUShort(byte* ptr, int offset)
        {
            return (ushort)(ptr[offset] | (ptr[offset + 1] << 8));
        }

        private static int ReadInt(byte* ptr, int offset)
        {
            return unchecked((int)ReadUInt(ptr, offset));
        }

        private static ulong ReadULong(byte* ptr, int offset)
        {
            return ReadUInt(ptr, offset) | ((ulong)ReadUInt(ptr, offset + 4) << 32);
        }

        private static long ReadLong(byte* ptr, int offset)
        {
            return unchecked((long)ReadULong(ptr, offset));
        }
    }
}
