#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_PDA_H8LR_MMF_AVAILABLE
#endif

using System;
using System.IO;
#if HECTON8_PDA_H8LR_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct PdaH8lrHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint Count;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct PdaH8lrRecordDTO
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint ByteOffset;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint Reserved0;
    }

    internal sealed unsafe class PdaH8lrLoreStore : IDisposable
    {
        public const uint MagicH8lr = 0x524C3848u;
        public const uint CurrentVersion = 1u;
        public const int HeaderSizeBytes = 16;
        public const int RecordSizeBytes = 16;

        private const int MaxRecordCount = 4096;
        private const int FileStreamBufferBytes = 64 * 1024;

#if HECTON8_PDA_H8LR_MMF_AVAILABLE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
        private bool _viewPointerAcquired;
#endif
        private FileStream _fileStream;
        private byte* _basePointer;
        private int _mappedBytes;
        private int _entryCount;
        private uint _btreeOffset;
        private uint _btreeRootOffset;
        private uint _btreeEndOffset;
        private uint _btreeNodeCount;
        private uint _lastTreeDepth;
        private uint _lastTreeKeysProcessed;
        private uint _lastPrefetchTouchCount;
        private bool _vaultMirrorBacked;
        private bool _btreeAvailable;

        public bool IsOpen => _basePointer != null && _mappedBytes >= HeaderSizeBytes && _entryCount > 0;
        public int EntryCount => _entryCount;
        public int MappedBytes => _mappedBytes;
        public bool IsVaultMirrorBacked => _vaultMirrorBacked;

        public bool OpenDefault(NativeArray<byte> vaultMirror)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Data", "Lore", "Encyclopedia.h8bin"));
            return Open(path, vaultMirror);
        }

        public bool Open(string path, NativeArray<byte> vaultMirror)
        {
            Dispose();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                return false;
            }

            if (info.Length < HeaderSizeBytes || info.Length > int.MaxValue)
                return false;

#if HECTON8_PDA_H8LR_MMF_AVAILABLE
            if (TryOpenMemoryMapped(path, (int)info.Length))
                return true;
#endif

            return TryOpenVaultMirror(path, (int)info.Length, vaultMirror);
        }

        public bool TryGetUtf8(uint hash, out ReadOnlySpan<byte> utf8)
        {
            utf8 = ReadOnlySpan<byte>.Empty;
            if (!IsOpen || hash == 0u)
                return false;

            if (!_btreeAvailable ||
                !H8CacheBTree.TryFindValue(
                    _basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    hash,
                    ResolveGlobalQualityWeight(),
                    out uint recordIndex,
                    out uint depth,
                    out uint keysProcessed,
                    out uint prefetchTouchCount) ||
                recordIndex >= _entryCount)
            {
                return false;
            }

            _lastTreeDepth = depth;
            _lastTreeKeysProcessed = keysProcessed;
            _lastPrefetchTouchCount = prefetchTouchCount;
            PdaH8lrRecordDTO record = ReadRecord((int)recordIndex);
            if (record.Hash != hash || !IsRecordInBounds(in record))
                return false;

            utf8 = new ReadOnlySpan<byte>(_basePointer + record.ByteOffset, (int)record.ByteLength);
            return true;
        }

        public bool TryGetRecord(int index, out PdaH8lrRecordDTO record)
        {
            record = default;
            if (!IsOpen || (uint)index >= (uint)_entryCount)
                return false;

            record = ReadRecord(index);
            return IsRecordInBounds(in record);
        }

        public void Dispose()
        {
#if HECTON8_PDA_H8LR_MMF_AVAILABLE
            if (_accessor != null)
            {
                if (_viewPointerAcquired)
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();

                _viewPointerAcquired = false;
                _accessor.Dispose();
                _accessor = null;
            }

            if (_mappedFile != null)
            {
                _mappedFile.Dispose();
                _mappedFile = null;
            }
#endif
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            _basePointer = null;
            _mappedBytes = 0;
            _entryCount = 0;
            _btreeOffset = 0u;
            _btreeRootOffset = 0u;
            _btreeEndOffset = 0u;
            _btreeNodeCount = 0u;
            _lastTreeDepth = 0u;
            _lastTreeKeysProcessed = 0u;
            _lastPrefetchTouchCount = 0u;
            _vaultMirrorBacked = false;
            _btreeAvailable = false;
        }

#if HECTON8_PDA_H8LR_MMF_AVAILABLE
        private bool TryOpenMemoryMapped(string path, int fileBytes)
        {
            try
            {
                _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);
                _mappedFile = MemoryMappedFile.CreateFromFile(
                    _fileStream,
                    null,
                    fileBytes,
                    MemoryMappedFileAccess.Read,
                    HandleInheritability.None,
                    true);
                _accessor = _mappedFile.CreateViewAccessor(0L, fileBytes, MemoryMappedFileAccess.Read);
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
                _viewPointerAcquired = true;
                _mappedBytes = fileBytes;
                _vaultMirrorBacked = false;

                if (ValidateMappedBytes())
                    return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
            }

            Dispose();
            return false;
        }
#endif

        private bool TryOpenVaultMirror(string path, int fileBytes, NativeArray<byte> vaultMirror)
        {
            if (!vaultMirror.IsCreated || fileBytes <= 0 || fileBytes > vaultMirror.Length)
                return false;

            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(vaultMirror);
            Span<byte> mirrorSpan = new Span<byte>(destination, fileBytes);
            int totalRead = 0;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))
                {
                    while (totalRead < fileBytes)
                    {
                        int read = stream.Read(mirrorSpan.Slice(totalRead));
                        if (read <= 0)
                            break;

                        totalRead += read;
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                return false;
            }

            if (totalRead != fileBytes)
                return false;

            _basePointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(vaultMirror);
            _mappedBytes = fileBytes;
            _vaultMirrorBacked = true;

            if (ValidateMappedBytes())
                return true;

            Dispose();
            return false;
        }

        private bool ValidateMappedBytes()
        {
            if (_basePointer == null || _mappedBytes < HeaderSizeBytes)
                return false;

            PdaH8lrHeaderDTO header = new PdaH8lrHeaderDTO
            {
                Magic = ReadUInt32LittleEndian(_basePointer, 0),
                Version = ReadUInt32LittleEndian(_basePointer, 4),
                Count = ReadUInt32LittleEndian(_basePointer, 8),
                Reserved0 = ReadUInt32LittleEndian(_basePointer, 12)
            };

            if (header.Magic != MagicH8lr ||
                header.Version != CurrentVersion ||
                header.Count == 0u ||
                header.Count > MaxRecordCount ||
                header.Reserved0 != 0u)
            {
                return false;
            }

            int count = (int)header.Count;
            int recordTableBytes = HeaderSizeBytes + (count * RecordSizeBytes);
            if (recordTableBytes > _mappedBytes)
                return false;

            uint previousHash = 0u;
            uint payloadStart = uint.MaxValue;
            for (int i = 0; i < count; i++)
            {
                PdaH8lrRecordDTO record = ReadRecordUnchecked(i);
                if (record.Reserved0 != 0u ||
                    record.Hash == 0u ||
                    (i > 0 && record.Hash <= previousHash) ||
                    (record.ByteOffset & 15u) != 0u ||
                    record.ByteLength == 0u ||
                    !IsRecordInBounds(in record))
                {
                    return false;
                }

                payloadStart = math.min(payloadStart, record.ByteOffset);
                previousHash = record.Hash;
            }

            if (!H8CacheBTree.TryResolveTree(
                    H8StaticDataFormat.CacheBTreeFlag,
                    HeaderSizeBytes,
                    (uint)count,
                    RecordSizeBytes,
                    payloadStart,
                    out _btreeOffset,
                    out _btreeRootOffset,
                    out _btreeNodeCount))
            {
                return false;
            }

            _btreeEndOffset = payloadStart;
            _entryCount = count;
            if (!ValidateBTreeEdge())
                return false;

            _btreeAvailable = true;
            return true;
        }

        private bool ValidateBTreeEdge()
        {
            PdaH8lrRecordDTO first = ReadRecordUnchecked(0);
            PdaH8lrRecordDTO last = ReadRecordUnchecked(_entryCount - 1);
            return H8CacheBTree.TryFindValue(
                    _basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    first.Hash,
                    0f,
                    out uint firstIndex,
                    out _,
                    out _,
                    out _) &&
                firstIndex == 0u &&
                H8CacheBTree.TryFindValue(
                    _basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    last.Hash,
                    0f,
                    out uint lastIndex,
                    out _,
                    out _,
                    out _) &&
                lastIndex == (uint)(_entryCount - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PdaH8lrRecordDTO ReadRecord(int index)
        {
            return (uint)index < (uint)_entryCount ? ReadRecordUnchecked(index) : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PdaH8lrRecordDTO ReadRecordUnchecked(int index)
        {
            int offset = HeaderSizeBytes + (index * RecordSizeBytes);
            return new PdaH8lrRecordDTO
            {
                Hash = ReadUInt32LittleEndian(_basePointer, offset),
                ByteOffset = ReadUInt32LittleEndian(_basePointer, offset + 4),
                ByteLength = ReadUInt32LittleEndian(_basePointer, offset + 8),
                Reserved0 = ReadUInt32LittleEndian(_basePointer, offset + 12)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsRecordInBounds(in PdaH8lrRecordDTO record)
        {
            if (record.ByteOffset > int.MaxValue || record.ByteLength > int.MaxValue)
                return false;

            int offset = (int)record.ByteOffset;
            int length = (int)record.ByteLength;
            if (offset < HeaderSizeBytes || length <= 0)
                return false;

            int end = offset + length;
            return end >= offset && end <= _mappedBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32LittleEndian(byte* bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }
    }
}
