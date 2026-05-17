#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_STATICDATA_MMF_AVAILABLE
#endif

using System;
using System.IO;
#if HECTON8_STATICDATA_MMF_AVAILABLE
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
    /// Zero-copy runtime reader for H8StaticData.bin.
    /// </summary>
    public sealed unsafe class StaticDataStore : IDisposable
    {
        private const string OwnerName = "CSV_DATA_MONOLITH_SYNC.StaticDataStore";
        private const string LookupLabel = "HashToOffset";
        private const uint StateOpenHash = 0x53444F50u;
        private const uint StateMissHash = 0x53444D49u;
        private const uint StateErrorHash = 0x53444552u;
        private const uint ErrorMissingHash = 0x4D495353u;
        private const uint ErrorCrcHash = 0x43524321u;
        private const uint ErrorTypeHash = 0x54595045u;
        private const uint ErrorHeaderHash = 0x48445221u;
        private const int FileStreamBufferBytes = 64 * 1024;

#if HECTON8_STATICDATA_MMF_AVAILABLE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
#endif
        private FileStream _fileStream;
        private byte* _basePointer;
        private byte* _ownedFallbackPointer;
        private long _mappedBytes;
        private H8StaticDataHeader _header;
        private IDataVault _dataVault;
        private VaultBufferHandle<H8StaticDataTelemetryEntry> _blackBoxHandle;
        private VaultBufferHandle<int> _blackBoxCursorHandle;
        private NativeParallelHashMap<uint, long> _lookup;
        private bool _lookupRegistered;

        public bool IsOpen => _basePointer != null && _mappedBytes >= UnsafeUtility.SizeOf<H8StaticDataHeader>();
        public int LookupCount => IsOpen ? (int)_header.LookupCount : 0;
        public int RecordCount => IsOpen ? (int)_header.RecordCount : 0;
        public uint PayloadCrc32 => IsOpen ? _header.PayloadCrc32 : 0u;
        public uint BabelCrc32 => IsOpen ? _header.BabelCrc32 : 0u;

        public StaticDataStore(IDataVault dataVault = null)
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
            string path = Path.Combine(Application.dataPath, "..", "Data", "Balance", "Baked", H8StaticDataFormat.StaticDataFileName);
            return Open(Path.GetFullPath(path));
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
            if (info.Length < UnsafeUtility.SizeOf<H8StaticDataHeader>() || info.Length > int.MaxValue)
            {
                RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
                return false;
            }

#if HECTON8_STATICDATA_MMF_AVAILABLE
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

            if (!ValidateHeaderAndChecksum())
            {
                Dispose();
                return false;
            }

            if (!BuildLookupMap())
            {
                Dispose();
                return false;
            }

            RecordTelemetry(StateOpenHash, 0u, 0u, 0L);
            return true;
        }

        public bool TryReload(string path)
        {
            return Open(path);
        }

        /// <summary>
        /// Returns a direct readonly reference into the mapped binary. Missing hashes return a zero static record.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetRecord<T>(uint hash) where T : unmanaged
        {
            if (_basePointer == null || !_lookup.IsCreated || !_lookup.TryGetValue(hash, out long packedValue))
            {
                RecordTelemetry(StateMissHash, ErrorMissingHash, hash, 0L);
                return ref MissingRecord<T>.Value;
            }

            long offset = H8StaticDataFormat.UnpackLookupOffset(packedValue);
            ushort actualRecordType = H8StaticDataFormat.UnpackLookupRecordType(packedValue);
            ushort expectedRecordType = RecordContract<T>.RecordType;
            if (expectedRecordType == 0 || actualRecordType != expectedRecordType)
            {
                RecordTelemetry(StateErrorHash, ErrorTypeHash, hash, offset);
                return ref MissingRecord<T>.Value;
            }

            int size = RecordContract<T>.SizeBytes;
            if (offset < 0L || offset > _mappedBytes - size)
            {
                RecordTelemetry(StateErrorHash, ErrorMissingHash, hash, offset);
                return ref MissingRecord<T>.Value;
            }

            return ref UnsafeUtility.AsRef<T>(_basePointer + offset);
        }

        public bool TryGetLookupEntry(int index, out H8StaticDataLookupEntry entry)
        {
            if (!IsOpen || index < 0 || index >= _header.LookupCount)
            {
                entry = default;
                return false;
            }

            byte* lookupBase = _basePointer + _header.LookupOffset;
            entry = UnsafeUtility.ReadArrayElement<H8StaticDataLookupEntry>(lookupBase, index);
            return true;
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
            H8StaticDataBlackBoxDump.Write(
                resolvedPath,
                ring,
                *cursor,
                IsOpen ? _header.PayloadCrc32 : 0u,
                IsOpen ? _header.Flags : 0u);
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

        private void CloseFile()
        {
#if HECTON8_STATICDATA_MMF_AVAILABLE
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

        private bool ValidateHeaderAndChecksum()
        {
            _header = UnsafeUtility.ReadArrayElement<H8StaticDataHeader>(_basePointer, 0);
            if (_header.Magic != H8StaticDataFormat.StaticDataMagic ||
                _header.FormatVersion != H8StaticDataFormat.FormatVersion ||
                _header.HeaderSizeBytes != UnsafeUtility.SizeOf<H8StaticDataHeader>() ||
                _header.SchemaMajor != H8StaticDataFormat.ExpectedSchemaMajor ||
                _header.SchemaMinor != H8StaticDataFormat.ExpectedSchemaMinor ||
                _header.SchemaHash != H8StaticDataFormat.SchemaHash ||
                _header.FileByteLength != _mappedBytes ||
                (_header.Flags & H8StaticDataFormat.LittleEndianFlag) == 0u)
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, 0L);
                return false;
            }

            int lookupEntrySize = UnsafeUtility.SizeOf<H8StaticDataLookupEntry>();
            long lookupBytes = (long)_header.LookupCount * lookupEntrySize;
            long recordBytes = _mappedBytes - _header.RecordsOffset;
            if (_header.LookupOffset < _header.HeaderSizeBytes ||
                _header.RecordsOffset < _header.LookupOffset ||
                _header.FileByteLength < _header.RecordsOffset ||
                _header.LookupOffset + lookupBytes > _header.RecordsOffset ||
                _header.RecordCount != _header.LookupCount ||
                _header.RecordBytes != recordBytes ||
                (recordBytes & 15L) != 0L ||
                (_header.LookupOffset & 15u) != 0u ||
                (_header.RecordsOffset & 15u) != 0u)
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
            int count = (int)_header.LookupCount;
            if (count <= 0)
                return false;

            _lookup = new NativeParallelHashMap<uint, long>(count, Allocator.Persistent);
            _lookupRegistered = NativeMemorySentinel.RegisterNativeParallelHashMap(
                _lookup,
                OwnerName,
                LookupLabel,
                NativeAllocationLifetime.Session) != 0;

            byte* lookupBase = _basePointer + _header.LookupOffset;
            for (int i = 0; i < count; i++)
            {
                H8StaticDataLookupEntry entry = UnsafeUtility.ReadArrayElement<H8StaticDataLookupEntry>(lookupBase, i);
                int recordSize = H8StaticDataFormat.RecordSizeBytes(entry.RecordType);
                if ((entry.Offset & 15L) != 0L ||
                    entry.Hash == 0u ||
                    !H8StaticDataFormat.CanPackRecordType(entry.RecordType) ||
                    recordSize <= 0 ||
                    entry.ByteSize != recordSize ||
                    entry.Offset < _header.RecordsOffset ||
                    entry.Offset > _mappedBytes - entry.ByteSize)
                {
                    RecordTelemetry(StateErrorHash, ErrorMissingHash, entry.Hash, entry.Offset);
                    return false;
                }

                long packedValue = H8StaticDataFormat.PackLookupValue(entry.Offset, entry.RecordType);
                if (!_lookup.TryAdd(entry.Hash, packedValue))
                {
                    RecordTelemetry(StateErrorHash, ErrorMissingHash, entry.Hash, entry.Offset);
                    return false;
                }
            }

            return true;
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
                LookupCount = IsOpen ? _header.LookupCount : 0u,
                RecordCount = IsOpen ? _header.RecordCount : 0u,
                PayloadCrc32 = IsOpen ? _header.PayloadCrc32 : 0u,
                Flags = IsOpen ? _header.Flags : 0u,
                SchemaHash = IsOpen ? _header.SchemaHash : 0u,
                FileByteLength = _mappedBytes,
                LastOffset = offset,
                ErrorHash = errorHash
            };
            *cursor = (index + 1) % H8StaticDataFormat.TelemetryFrameCount;
        }

        private static class MissingRecord<T> where T : unmanaged
        {
            public static T Value;
        }

        private static class RecordContract<T> where T : unmanaged
        {
            public static readonly ushort RecordType = H8StaticDataFormat.RecordTypeOf<T>();
            public static readonly int SizeBytes = UnsafeUtility.SizeOf<T>();
        }
    }
}
