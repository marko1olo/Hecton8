using System;
using System.IO;
using Hecton8.Core;

namespace Hecton8.Narrative
{
    public enum LoreMmfLoadStatus : byte
    {
        Ok = 0,
        NotOpen = 1,
        InvalidPath = 2,
        IoFailed = 3,
        CorruptIndex = 4,
        MissingEntry = 5,
        UnsupportedEncoding = 6,
        DestinationTooSmall = 7
    }

    public sealed class LoreMmfEncyclopedia : IDisposable
    {
        public const uint IndexMagic = 0x454C3848u; // H8LE
        public const ushort CurrentVersion = 1;
        public const ushort EncodingUtf16LittleEndian = 1;

        private const int HeaderSizeBytes = 16;
        private const int EntrySizeBytes = 24;
        private const int MaxEntryCount = 4096;
        private const int MaxIndexBytes = HeaderSizeBytes + (MaxEntryCount * EntrySizeBytes);

        private FileStream _payloadStream;
        private byte[] _indexBytes;
        private byte[] _payloadScratch;
        private long _payloadLength;
        private int _entryCount;

        public bool IsOpen => _indexBytes != null && _payloadStream != null && _payloadStream.CanRead && _entryCount > 0;
        public int EntryCount => _entryCount;

        public LoreMmfLoadStatus TryOpen(string indexPath, string payloadPath)
        {
            Dispose();

            if (string.IsNullOrWhiteSpace(indexPath) || string.IsNullOrWhiteSpace(payloadPath))
                return LoreMmfLoadStatus.InvalidPath;

            try
            {
                using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _payloadStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long indexLength = indexStream.Length;
                _payloadLength = _payloadStream.Length;
                if (indexLength < HeaderSizeBytes || indexLength > MaxIndexBytes || _payloadLength <= 0L)
                    return FailOpen(LoreMmfLoadStatus.CorruptIndex);

                _indexBytes = new byte[(int)indexLength]; // COLD ALLOC: lore encyclopedia index snapshot - owner: LoreMmfEncyclopedia
                if (!TryReadExact(indexStream, _indexBytes, _indexBytes.Length))
                    return FailOpen(LoreMmfLoadStatus.IoFailed);

                LoreMmfLoadStatus headerStatus = ValidateIndexHeader(indexLength);
                if (headerStatus != LoreMmfLoadStatus.Ok)
                    return FailOpen(headerStatus);

                if (!TryResolveMaxPayloadScratchBytes(out int maxPayloadBytes))
                    return FailOpen(LoreMmfLoadStatus.CorruptIndex);

                _payloadScratch = maxPayloadBytes > 0
                    ? new byte[maxPayloadBytes] // COLD ALLOC: largest lore payload entry scratch - owner: LoreMmfEncyclopedia
                    : Array.Empty<byte>();

                return LoreMmfLoadStatus.Ok;
            }
            catch (Exception)
            {
                return FailOpen(LoreMmfLoadStatus.IoFailed);
            }
        }

        public LoreMmfLoadStatus TryLoadEntryUtf16(
            uint hash,
            char[] destination,
            out int charsWritten)
        {
            charsWritten = 0;
            if (!IsOpen)
                return LoreMmfLoadStatus.NotOpen;

            if (destination == null || destination.Length <= 0)
                return LoreMmfLoadStatus.DestinationTooSmall;

            if (!TryFindEntry(hash, out IndexEntry entry))
                return LoreMmfLoadStatus.MissingEntry;

            if (entry.Encoding != EncodingUtf16LittleEndian)
                return LoreMmfLoadStatus.UnsupportedEncoding;

            if ((entry.ByteLength & 1) != 0 ||
                entry.ByteLength < 0 ||
                entry.ByteOffset < 0L ||
                entry.ByteOffset + entry.ByteLength > _payloadLength)
            {
                return LoreMmfLoadStatus.CorruptIndex;
            }

            int charCount = entry.ByteLength >> 1;
            if (charCount > destination.Length)
                return LoreMmfLoadStatus.DestinationTooSmall;

            try
            {
                if (_payloadScratch == null || _payloadScratch.Length < entry.ByteLength)
                    return LoreMmfLoadStatus.CorruptIndex;

                _payloadStream.Position = entry.ByteOffset;
                if (!TryReadExact(_payloadStream, _payloadScratch, entry.ByteLength))
                    return LoreMmfLoadStatus.IoFailed;

                for (int i = 0; i < charCount; i++)
                {
                    int byteIndex = i << 1;
                    destination[i] = (char)(_payloadScratch[byteIndex] | (_payloadScratch[byteIndex + 1] << 8));
                }
            }
            catch (Exception)
            {
                return LoreMmfLoadStatus.IoFailed;
            }

            charsWritten = charCount;
            return LoreMmfLoadStatus.Ok;
        }

        public void Dispose()
        {
            _payloadStream?.Dispose();

            _payloadStream = null;
            _indexBytes = null;
            _payloadScratch = null;
            _payloadLength = 0L;
            _entryCount = 0;
        }

        private LoreMmfLoadStatus FailOpen(LoreMmfLoadStatus status)
        {
            Dispose();
            return status;
        }

        private LoreMmfLoadStatus ValidateIndexHeader(long indexLength)
        {
            if (_indexBytes == null || _indexBytes.Length < HeaderSizeBytes)
                return LoreMmfLoadStatus.CorruptIndex;

            uint magic = ReadUInt32LittleEndian(_indexBytes, 0);
            ushort version = ReadUInt16LittleEndian(_indexBytes, 4);
            ushort entrySize = ReadUInt16LittleEndian(_indexBytes, 6);
            int entryCount = ReadInt32LittleEndian(_indexBytes, 8);
            if (magic != IndexMagic ||
                version != CurrentVersion ||
                entrySize != EntrySizeBytes ||
                entryCount < 0 ||
                entryCount > MaxEntryCount)
            {
                return LoreMmfLoadStatus.CorruptIndex;
            }

            long requiredBytes = HeaderSizeBytes + ((long)entryCount * EntrySizeBytes);
            if (requiredBytes > indexLength)
                return LoreMmfLoadStatus.CorruptIndex;

            _entryCount = entryCount;
            return LoreMmfLoadStatus.Ok;
        }

        private bool TryFindEntry(uint hash, out IndexEntry entry)
        {
            entry = default;
            if (hash == 0u || _entryCount <= 0)
                return false;

            int min = 0;
            int max = _entryCount - 1;
            while (min <= max)
            {
                int midpoint = min + ((max - min) >> 1);
                ReadIndexEntry(midpoint, out IndexEntry candidate);
                uint candidateHash = candidate.Hash;
                if (candidateHash == hash)
                {
                    entry = candidate;
                    return true;
                }

                if (candidateHash < hash)
                    min = midpoint + 1;
                else
                    max = midpoint - 1;
            }

            return false;
        }

        private bool TryResolveMaxPayloadScratchBytes(out int maxPayloadBytes)
        {
            maxPayloadBytes = 0;
            for (int i = 0; i < _entryCount; i++)
            {
                ReadIndexEntry(i, out IndexEntry entry);
                if (entry.ByteLength < 0 ||
                    entry.ByteOffset < 0L ||
                    entry.ByteOffset + entry.ByteLength > _payloadLength)
                {
                    return false;
                }

                if (entry.ByteLength > maxPayloadBytes)
                    maxPayloadBytes = entry.ByteLength;
            }

            return true;
        }

        private void ReadIndexEntry(int index, out IndexEntry entry)
        {
            int offset = HeaderSizeBytes + (index * EntrySizeBytes);
            entry = new IndexEntry
            {
                Hash = ReadUInt32LittleEndian(_indexBytes, offset),
                ByteOffset = ReadInt64LittleEndian(_indexBytes, offset + 4),
                ByteLength = ReadInt32LittleEndian(_indexBytes, offset + 12),
                Encoding = ReadUInt16LittleEndian(_indexBytes, offset + 16),
                Flags = ReadUInt16LittleEndian(_indexBytes, offset + 18),
                Reserved = ReadUInt32LittleEndian(_indexBytes, offset + 20)
            };
        }

        private static bool TryReadExact(Stream stream, byte[] buffer, int byteCount)
        {
            int offset = 0;
            while (offset < byteCount)
            {
                int read = stream.Read(buffer, offset, byteCount - offset);
                if (read <= 0)
                    return false;

                offset += read;
            }

            return true;
        }

        private static ushort ReadUInt16LittleEndian(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        private static int ReadInt32LittleEndian(byte[] bytes, int offset)
        {
            return (int)ReadUInt32LittleEndian(bytes, offset);
        }

        private static long ReadInt64LittleEndian(byte[] bytes, int offset)
        {
            uint lo = ReadUInt32LittleEndian(bytes, offset);
            uint hi = ReadUInt32LittleEndian(bytes, offset + 4);
            return (long)(((ulong)hi << 32) | lo);
        }

        private struct IndexEntry
        {
            public uint Hash;
            public long ByteOffset;
            public int ByteLength;
            public ushort Encoding;
            public ushort Flags;
            public uint Reserved;
        }
    }
}
