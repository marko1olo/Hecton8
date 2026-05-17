#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_BABEL_MMF_AVAILABLE
#endif

using System;
using System.IO;
#if HECTON8_BABEL_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Zero-copy runtime reader for Babel_Dictionary.h8bin.
    /// </summary>
    public sealed unsafe class BabelDictionaryStore : IDisposable
    {
        private const string OwnerName = "CSV_DATA_MONOLITH_SYNC.BabelDictionaryStore";
        private const string LookupLabel = "HashToUtf8Slice";
        private const uint StateOpenHash = 0x42424F50u;
        private const uint StateMissHash = 0x42424D49u;
        private const uint StateErrorHash = 0x42424552u;
        private const uint ErrorMissingHash = 0x4D495353u;
        private const uint ErrorCrcHash = 0x43524321u;
        private const uint ErrorHeaderHash = 0x48445221u;
        private const uint ErrorBoundsHash = 0x424E4453u;
        private const int FileStreamBufferBytes = 64 * 1024;

#if HECTON8_BABEL_MMF_AVAILABLE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
#endif
        private FileStream _fileStream;
        private byte* _basePointer;
        private byte* _ownedFallbackPointer;
        private long _mappedBytes;
        private H8BabelDictionaryHeader _header;
        private IDataVault _dataVault;
        private VaultBufferHandle<H8StaticDataTelemetryEntry> _blackBoxHandle;
        private VaultBufferHandle<int> _blackBoxCursorHandle;
        private NativeParallelHashMap<uint, long> _lookup;
        private bool _lookupRegistered;

        public bool IsOpen => _basePointer != null && _mappedBytes >= UnsafeUtility.SizeOf<H8BabelDictionaryHeader>();
        public int EntryCount => IsOpen ? (int)_header.EntryCount : 0;
        public uint PayloadCrc32 => IsOpen ? _header.PayloadCrc32 : 0u;

        public BabelDictionaryStore(IDataVault dataVault = null)
        {
            _dataVault = dataVault;
        }

        public void BindDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            _dataVault = dataVault;
            _blackBoxHandle = default;
            _blackBoxCursorHandle = default;
        }

        public bool OpenDefault()
        {
            string path = Path.Combine(Application.dataPath, "..", "Data", "Balance", "Baked", H8StaticDataFormat.BabelDictionaryFileName);
            return Open(Path.GetFullPath(path));
        }

        public bool OpenDefault(uint expectedPayloadCrc32)
        {
            string path = Path.Combine(Application.dataPath, "..", "Data", "Balance", "Baked", H8StaticDataFormat.BabelDictionaryFileName);
            return Open(Path.GetFullPath(path), expectedPayloadCrc32);
        }

        public bool Open(string path)
        {
            CloseFile();
            if (!EnsureBlackBox())
                return false;

            if (!BitConverter.IsLittleEndian || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
                return false;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length < UnsafeUtility.SizeOf<H8BabelDictionaryHeader>() || info.Length > int.MaxValue)
            {
                RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
                return false;
            }

#if HECTON8_BABEL_MMF_AVAILABLE
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);
            _mappedFile = MemoryMappedFile.CreateFromFile(
                _fileStream,
                null,
                info.Length,
                MemoryMappedFileAccess.Read,
                HandleInheritability.None,
                true);
            _accessor = _mappedFile.CreateViewAccessor(0L, info.Length, MemoryMappedFileAccess.Read);
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
            _mappedBytes = info.Length;
#else
            _ownedFallbackPointer = (byte*)H8Memory.AllocateRaw(
                info.Length,
                H8StaticDataFormat.AlignmentBytes,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                false);
            if (_ownedFallbackPointer == null)
            {
                RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
                return false;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))
            {
                long offset = 0L;
                while (offset < info.Length)
                {
                    int chunkBytes = (int)Math.Min(FileStreamBufferBytes, info.Length - offset);
                    int read = stream.Read(new Span<byte>(_ownedFallbackPointer + offset, chunkBytes));
                    if (read <= 0)
                        break;

                    offset += read;
                }

                if (offset != info.Length)
                {
                    RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, offset);
                    Dispose();
                    return false;
                }
            }
            _basePointer = _ownedFallbackPointer;
            _mappedBytes = info.Length;
#endif

            if (!ValidateHeaderAndChecksum() || !BuildLookupMap())
            {
                Dispose();
                return false;
            }

            RecordTelemetry(StateOpenHash, 0u, 0u, 0L);
            return true;
        }

        public bool Open(string path, uint expectedPayloadCrc32)
        {
            if (!Open(path))
                return false;

            if (ValidateExpectedPayloadCrc(expectedPayloadCrc32))
                return true;

            CloseFile();
            return false;
        }

        public bool TryReload(string path)
        {
            return Open(path);
        }

        public bool TryReload(string path, uint expectedPayloadCrc32)
        {
            return Open(path, expectedPayloadCrc32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ValidateExpectedPayloadCrc(uint expectedPayloadCrc32)
        {
            if (IsOpen && _header.PayloadCrc32 == expectedPayloadCrc32)
                return true;

            RecordTelemetry(StateErrorHash, ErrorCrcHash, 0u, 0L);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetUtf8(uint hash)
        {
            if (_basePointer == null || !_lookup.IsCreated || !_lookup.TryGetValue(hash, out long packedSlice))
            {
                RecordTelemetry(StateMissHash, ErrorMissingHash, hash, 0L);
                return ReadOnlySpan<byte>.Empty;
            }

            uint offset = H8StaticDataFormat.UnpackBabelOffset(packedSlice);
            uint length = H8StaticDataFormat.UnpackBabelLength(packedSlice);
            if (length > int.MaxValue || offset > _mappedBytes - length)
            {
                RecordTelemetry(StateErrorHash, ErrorBoundsHash, hash, offset);
                return ReadOnlySpan<byte>.Empty;
            }

            return new ReadOnlySpan<byte>(_basePointer + offset, (int)length);
        }

        public void DumpBlackBox(string path = null)
        {
            if (!EnsureBlackBox())
                return;

            H8StaticDataTelemetryEntry* ring = (H8StaticDataTelemetryEntry*)_blackBoxHandle.ResolvePointer(_dataVault);
            int* cursor = (int*)_blackBoxCursorHandle.ResolvePointer(_dataVault);
            if (ring == null || cursor == null)
                return;

            string resolvedPath = string.IsNullOrEmpty(path)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_CSV_DATA_MONOLITH_SYNC.bin"))
                : path;
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int entrySize = UnsafeUtility.SizeOf<H8StaticDataTelemetryEntry>();
            using (FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                int cursorValue = *cursor;
                if ((uint)cursorValue >= H8StaticDataFormat.TelemetryFrameCount)
                    cursorValue = 0;

                for (int i = 0; i < H8StaticDataFormat.TelemetryFrameCount; i++)
                {
                    int sourceIndex = (cursorValue + i) % H8StaticDataFormat.TelemetryFrameCount;
                    stream.Write(new ReadOnlySpan<byte>(ring + sourceIndex, entrySize));
                }
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        public void Shutdown()
        {
            CloseFile();
            _blackBoxHandle = default;
            _blackBoxCursorHandle = default;
        }

        private bool ValidateHeaderAndChecksum()
        {
            _header = UnsafeUtility.ReadArrayElement<H8BabelDictionaryHeader>(_basePointer, 0);
            if (_header.Magic != H8StaticDataFormat.BabelMagic ||
                _header.FormatVersion != H8StaticDataFormat.FormatVersion ||
                _header.HeaderSizeBytes != UnsafeUtility.SizeOf<H8BabelDictionaryHeader>() ||
                _header.FileByteLength != _mappedBytes ||
                (_header.Flags & H8StaticDataFormat.LittleEndianFlag) == 0u)
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, 0L);
                return false;
            }

            int entrySize = UnsafeUtility.SizeOf<H8BabelDictionaryEntry>();
            long indexBytes = (long)_header.EntryCount * entrySize;
            if (_header.IndexOffset < _header.HeaderSizeBytes ||
                _header.DataOffset < _header.IndexOffset ||
                _header.IndexOffset + indexBytes > _header.DataOffset ||
                _header.DataOffset > _header.FileByteLength ||
                (_header.IndexOffset & 15u) != 0u ||
                (_header.DataOffset & 15u) != 0u)
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, 0L);
                return false;
            }

            uint crc = H8Crc32.Compute(_basePointer + _header.HeaderSizeBytes, (int)(_mappedBytes - _header.HeaderSizeBytes));
            if (crc != _header.PayloadCrc32)
            {
                RecordTelemetry(StateErrorHash, ErrorCrcHash, 0u, 0L);
                return false;
            }

            return true;
        }

        private bool BuildLookupMap()
        {
            int count = (int)_header.EntryCount;
            if (count <= 0)
                return false;

            _lookup = new NativeParallelHashMap<uint, long>(count, Allocator.Persistent);
            _lookupRegistered = NativeMemorySentinel.RegisterNativeParallelHashMap(
                _lookup,
                OwnerName,
                LookupLabel,
                NativeAllocationLifetime.Session) != 0;

            byte* indexBase = _basePointer + _header.IndexOffset;
            for (int i = 0; i < count; i++)
            {
                H8BabelDictionaryEntry entry = UnsafeUtility.ReadArrayElement<H8BabelDictionaryEntry>(indexBase, i);
                if (entry.Hash == 0u ||
                    entry.Length == 0u ||
                    (entry.Offset & 15u) != 0u ||
                    entry.Offset < _header.DataOffset ||
                    entry.Offset > _mappedBytes - entry.Length)
                {
                    RecordTelemetry(StateErrorHash, ErrorBoundsHash, entry.Hash, entry.Offset);
                    return false;
                }

                long packedSlice = H8StaticDataFormat.PackBabelSlice(entry.Offset, entry.Length);
                if (!_lookup.TryAdd(entry.Hash, packedSlice))
                {
                    RecordTelemetry(StateErrorHash, ErrorBoundsHash, entry.Hash, entry.Offset);
                    return false;
                }
            }

            return true;
        }

        private void CloseFile()
        {
#if HECTON8_BABEL_MMF_AVAILABLE
            if (_accessor != null)
            {
                if (_basePointer != null && _ownedFallbackPointer == null)
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();

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

            if (_ownedFallbackPointer != null)
            {
                H8Memory.FreeRaw(_ownedFallbackPointer, Allocator.Persistent, SystemID.CoreDataVault);
                _ownedFallbackPointer = null;
            }

            _basePointer = null;
            _mappedBytes = 0L;
            _header = default;

            if (_lookup.IsCreated)
            {
                if (_lookupRegistered)
                {
                    NativeMemorySentinel.UnregisterNativeParallelHashMap(OwnerName, LookupLabel);
                    _lookupRegistered = false;
                }

                _lookup.Dispose();
                _lookup = default;
            }
        }

        private bool EnsureBlackBox()
        {
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!ReferenceEquals(_dataVault, vault))
            {
                _dataVault = vault;
                _blackBoxHandle = default;
                _blackBoxCursorHandle = default;
            }

            if (!_blackBoxHandle.IsCreated || _blackBoxHandle.Length < H8StaticDataFormat.TelemetryFrameCount)
            {
                _blackBoxHandle = vault.GetBufferHandle<H8StaticDataTelemetryEntry>(
                    BufferID.StaticDataTelemetryRing,
                    H8StaticDataFormat.TelemetryFrameCount,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_blackBoxCursorHandle.IsCreated || _blackBoxCursorHandle.Length < 1)
            {
                _blackBoxCursorHandle = vault.GetBufferHandle<int>(
                    BufferID.StaticDataTelemetryCursor,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            return _blackBoxHandle.IsCreated && _blackBoxCursorHandle.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTelemetry(uint stateHash, uint errorHash, uint requestedHash, long offset)
        {
            if (!EnsureBlackBox())
                return;

            H8StaticDataTelemetryEntry* ring = (H8StaticDataTelemetryEntry*)_blackBoxHandle.ResolvePointer(_dataVault);
            int* cursor = (int*)_blackBoxCursorHandle.ResolvePointer(_dataVault);
            if (ring == null || cursor == null)
                return;

            int index = *cursor;
            if ((uint)index >= H8StaticDataFormat.TelemetryFrameCount)
                index = 0;

            ring[index] = new H8StaticDataTelemetryEntry
            {
                FrameIndex = (uint)Mathf.Max(0, Time.frameCount),
                StateHash = stateHash,
                LastRequestedHash = requestedHash,
                LookupCount = IsOpen ? _header.EntryCount : 0u,
                RecordCount = 0u,
                PayloadCrc32 = IsOpen ? _header.PayloadCrc32 : 0u,
                Flags = IsOpen ? _header.Flags : 0u,
                SchemaHash = H8StaticDataFormat.SchemaHash,
                FileByteLength = _mappedBytes,
                LastOffset = offset,
                ErrorHash = errorHash
            };
            *cursor = (index + 1) % H8StaticDataFormat.TelemetryFrameCount;
        }
    }
}
