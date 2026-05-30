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
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Zero-copy runtime reader for H8StaticData.bin.
    /// </summary>
    public sealed unsafe class StaticDataStore : IDisposable
    {
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
        private VaultGenerationHandle<H8StaticDataTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<int> _blackBoxCursorHandle;
        private VaultGenerationHandle<BTreeTelemetryEntry> _btreeTelemetryHandle;
        private VaultGenerationHandle<int> _btreeTelemetryCursorHandle;
        private VaultGenerationHandle<BTreeTelemetryAccumulatorDTO> _btreeTelemetryAccumulatorHandle;
        private int _blackBoxWriteIndex;
        private int _btreeTelemetryWriteIndex;
        private H8StaticDataLookupEntry* _lookupPointer;
        private uint _btreeOffset;
        private uint _btreeRootOffset;
        private uint _btreeEndOffset;
        private uint _btreeNodeCount;
        private uint _lastTreeDepth;
        private uint _lastTreeKeysProcessed;
        private uint _lastPrefetchTouchCount;
        private uint _lastSearchComputeTimeNs;
        private bool _btreeAvailable;

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

            ReleaseVaultHandles(_dataVault);
            _dataVault = dataVault;
            _blackBoxHandle = default;
            _blackBoxCursorHandle = default;
            _btreeTelemetryHandle = default;
            _btreeTelemetryCursorHandle = default;
            _btreeTelemetryAccumulatorHandle = default;
            _blackBoxWriteIndex = 0;
            _btreeTelemetryWriteIndex = 0;
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

            if (!BuildLookupTree())
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
        /// Fetches a direct readonly reference into the mapped binary. Missing hashes return a zero static record.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T FetchRecord<T>(uint hash) where T : unmanaged
        {
            return ref FetchRecordInternal<T>(hash, false);
        }

        /// <summary>
        /// Tracks a record lookup and explicitly records owner-phase diagnostics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T TrackRecordLookup<T>(uint hash) where T : unmanaged
        {
            return ref FetchRecordInternal<T>(hash, true);
        }

        private ref readonly T FetchRecordInternal<T>(uint hash, bool recordTelemetry) where T : unmanaged
        {
            if (_basePointer == null || !_btreeAvailable || _lookupPointer == null)
            {
                if (recordTelemetry)
                    RecordTelemetry(StateMissHash, ErrorMissingHash, hash, 0L);
                return ref MissingRecord<T>.Value;
            }

            long searchStart = recordTelemetry ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            bool found = H8CacheBTree.TryFindValue(
                _basePointer,
                _btreeOffset,
                _btreeRootOffset,
                _btreeEndOffset,
                hash,
                ResolveGlobalQualityWeight(),
                out uint lookupIndex,
                out uint depth,
                out uint keysProcessed,
                out uint prefetchTouchCount);
            if (recordTelemetry)
            {
                long elapsedNs = ToNanoseconds(System.Diagnostics.Stopwatch.GetTimestamp() - searchStart);
                _lastSearchComputeTimeNs = elapsedNs <= 0L
                    ? 0u
                    : elapsedNs >= uint.MaxValue
                        ? uint.MaxValue
                        : (uint)elapsedNs;
                if (_lastSearchComputeTimeNs > H8CacheBTree.BTreeSlowBatchThresholdNs)
                    DumpBTreeTelemetry();

                _lastTreeDepth = depth;
                _lastTreeKeysProcessed = keysProcessed;
                _lastPrefetchTouchCount = prefetchTouchCount;
            }

            if (!found || lookupIndex >= _header.LookupCount)
            {
                if (recordTelemetry)
                    RecordTelemetry(StateMissHash, ErrorMissingHash, hash, 0L);
                return ref MissingRecord<T>.Value;
            }

            H8StaticDataLookupEntry lookupEntry = UnsafeUtility.ReadArrayElement<H8StaticDataLookupEntry>(_lookupPointer, (int)lookupIndex);
            if (lookupEntry.Hash != hash)
            {
                if (recordTelemetry)
                    RecordTelemetry(StateMissHash, ErrorMissingHash, hash, lookupEntry.Offset);
                return ref MissingRecord<T>.Value;
            }

            long packedValue = H8StaticDataFormat.PackLookupValue(lookupEntry.Offset, lookupEntry.RecordType);
            long offset = H8StaticDataFormat.UnpackLookupOffset(packedValue);
            ushort actualRecordType = H8StaticDataFormat.UnpackLookupRecordType(packedValue);
            ushort expectedRecordType = RecordContract<T>.RecordType;
            if (expectedRecordType == 0 || actualRecordType != expectedRecordType)
            {
                if (recordTelemetry)
                    RecordTelemetry(StateErrorHash, ErrorTypeHash, hash, offset);
                return ref MissingRecord<T>.Value;
            }

            int size = RecordContract<T>.SizeBytes;
            if (offset < 0L || offset > _mappedBytes - size)
            {
                if (recordTelemetry)
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
            if (!TryReadBlackBox(out NativeArray<H8StaticDataTelemetryEntry>.ReadOnly ring, out NativeArray<int>.ReadOnly cursor))
            {
                return;
            }

            string resolvedPath = string.IsNullOrEmpty(path)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_SHINOBU_207.bin"))
                : path;
            H8StaticDataTelemetryEntry* ringPtr = (H8StaticDataTelemetryEntry*)ring.GetUnsafeReadOnlyPtr();
            H8StaticDataBlackBoxDump.Write(
                resolvedPath,
                ringPtr,
                cursor[0],
                IsOpen ? _header.PayloadCrc32 : 0u,
                IsOpen ? _header.Flags : 0u);
        }

        public void DumpBTreeTelemetry(string path = null)
        {
            if (!TryReadBTreeTelemetry(
                    out NativeArray<BTreeTelemetryEntry>.ReadOnly ring,
                    out NativeArray<int>.ReadOnly cursor,
                    out _))
            {
                return;
            }

            string resolvedPath = string.IsNullOrEmpty(path)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_SHINOBU_207.bin"))
                : path;
            BTreeTelemetryEntry* ringPtr = (BTreeTelemetryEntry*)ring.GetUnsafeReadOnlyPtr();
            H8BTreeTelemetryDump.Write(
                resolvedPath,
                ringPtr,
                cursor[0],
                H8CacheBTree.BTreeTelemetrySlowBatchFlag);
        }

        public void Dispose()
        {
            Shutdown();
        }

        public void Shutdown()
        {
            CloseFile();
            ReleaseVaultHandles(_dataVault);
            _blackBoxHandle = default;
            _blackBoxCursorHandle = default;
            _btreeTelemetryHandle = default;
            _btreeTelemetryCursorHandle = default;
            _btreeTelemetryAccumulatorHandle = default;
            _blackBoxWriteIndex = 0;
            _btreeTelemetryWriteIndex = 0;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _blackBoxHandle);
            ReleaseVaultHandle(vault, ref _blackBoxCursorHandle);
            ReleaseVaultHandle(vault, ref _btreeTelemetryHandle);
            ReleaseVaultHandle(vault, ref _btreeTelemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _btreeTelemetryAccumulatorHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static long ToNanoseconds(long stopwatchTicks)
        {
            if (stopwatchTicks <= 0L)
                return 0L;

            double ns = stopwatchTicks * 1000000000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (ns <= 0.0)
                return 0L;
            if (ns >= long.MaxValue)
                return long.MaxValue;
            return (long)ns;
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
            _lookupPointer = null;
            _btreeOffset = 0u;
            _btreeRootOffset = 0u;
            _btreeEndOffset = 0u;
            _btreeNodeCount = 0u;
            _lastTreeDepth = 0u;
            _lastTreeKeysProcessed = 0u;
            _lastPrefetchTouchCount = 0u;
            _lastSearchComputeTimeNs = 0u;
            _btreeAvailable = false;
            _mappedBytes = 0L;
            _header = default;
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
                (_header.RecordsOffset & (H8StaticDataFormat.CacheLineBytes - 1u)) != 0u)
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, 0L);
                return false;
            }

            uint crc = H8Crc32.Compute(new ReadOnlySpan<byte>(
                _basePointer + _header.HeaderSizeBytes,
                (int)(_mappedBytes - _header.HeaderSizeBytes)));
            if (crc != _header.PayloadCrc32)
            {
                RecordTelemetry(StateErrorHash, ErrorCrcHash, 0u, 0L);
                return false;
            }

            return true;
        }

        private bool BuildLookupTree()
        {
            int count = (int)_header.LookupCount;
            if (count <= 0)
                return false;

            if (!H8CacheBTree.TryResolveTree(
                    _header.Flags,
                    _header.LookupOffset,
                    _header.LookupCount,
                    (uint)UnsafeUtility.SizeOf<H8StaticDataLookupEntry>(),
                    _header.RecordsOffset,
                    out _btreeOffset,
                    out _btreeRootOffset,
                    out _btreeNodeCount))
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, _header.RecordsOffset);
                return false;
            }

            _btreeEndOffset = _header.RecordsOffset;
            _lookupPointer = (H8StaticDataLookupEntry*)(_basePointer + _header.LookupOffset);
            for (int i = 0; i < count; i++)
            {
                H8StaticDataLookupEntry entry = UnsafeUtility.ReadArrayElement<H8StaticDataLookupEntry>(_lookupPointer, i);
                int recordSize = H8StaticDataFormat.RecordSizeBytes(entry.RecordType);
                if ((entry.Offset & (H8StaticDataFormat.CacheLineBytes - 1L)) != 0L ||
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
            }

            if (!ValidateBTreeEdge(count))
                return false;

            _btreeAvailable = true;
            return true;
        }

        private bool ValidateBTreeEdge(int count)
        {
            H8StaticDataLookupEntry first = UnsafeUtility.ReadArrayElement<H8StaticDataLookupEntry>(_lookupPointer, 0);
            H8StaticDataLookupEntry last = UnsafeUtility.ReadArrayElement<H8StaticDataLookupEntry>(_lookupPointer, count - 1);
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
                lastIndex == (uint)(count - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private bool EnsureBlackBox()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (_blackBoxHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _blackBoxHandle, out NativeArray<H8StaticDataTelemetryEntry> ring) ||
                !ring.IsCreated ||
                ring.Length < H8StaticDataFormat.TelemetryFrameCount)
            {
                _blackBoxHandle = vault.EnsureGenerationHandle<H8StaticDataTelemetryEntry>(
                    BufferID.StaticDataTelemetryRing,
                    H8StaticDataFormat.TelemetryFrameCount,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (_blackBoxCursorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _blackBoxCursorHandle, out NativeArray<int> cursor) ||
                !cursor.IsCreated ||
                cursor.Length < 1)
            {
                _blackBoxCursorHandle = vault.EnsureGenerationHandle<int>(
                    BufferID.StaticDataTelemetryCursor,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            return TryReadBlackBox(out _, out _);
        }

        private bool EnsureBTreeTelemetry()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (_btreeTelemetryHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _btreeTelemetryHandle, out NativeArray<BTreeTelemetryEntry> ring) ||
                !ring.IsCreated ||
                ring.Length < H8StaticDataFormat.TelemetryFrameCount)
            {
                _btreeTelemetryHandle = vault.EnsureGenerationHandle<BTreeTelemetryEntry>(
                    H8CacheBTree.BTreeTelemetryRingBufferId,
                    H8StaticDataFormat.TelemetryFrameCount,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (_btreeTelemetryCursorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _btreeTelemetryCursorHandle, out NativeArray<int> cursor) ||
                !cursor.IsCreated ||
                cursor.Length < 1)
            {
                _btreeTelemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                    H8CacheBTree.BTreeTelemetryCursorBufferId,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (_btreeTelemetryAccumulatorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _btreeTelemetryAccumulatorHandle, out NativeArray<BTreeTelemetryAccumulatorDTO> accumulator) ||
                !accumulator.IsCreated ||
                accumulator.Length < 1)
            {
                _btreeTelemetryAccumulatorHandle = vault.EnsureGenerationHandle<BTreeTelemetryAccumulatorDTO>(
                    H8CacheBTree.BTreeTelemetryAccumulatorBufferId,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            return TryReadBTreeTelemetry(out _, out _, out _);
        }

        private bool TryReadBlackBox(
            out NativeArray<H8StaticDataTelemetryEntry>.ReadOnly ring,
            out NativeArray<int>.ReadOnly cursor)
        {
            ring = default;
            cursor = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _blackBoxHandle.BufferID != 0u &&
                   _blackBoxCursorHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _blackBoxHandle, out ring) &&
                   ring.IsCreated &&
                   ring.Length >= H8StaticDataFormat.TelemetryFrameCount &&
                   vault.TryReadOnlyHandle(in _blackBoxCursorHandle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length >= 1;
        }

        private bool TryReadBTreeTelemetry(
            out NativeArray<BTreeTelemetryEntry>.ReadOnly ring,
            out NativeArray<int>.ReadOnly cursor,
            out NativeArray<BTreeTelemetryAccumulatorDTO>.ReadOnly accumulator)
        {
            ring = default;
            cursor = default;
            accumulator = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _btreeTelemetryHandle.BufferID != 0u &&
                   _btreeTelemetryCursorHandle.BufferID != 0u &&
                   _btreeTelemetryAccumulatorHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _btreeTelemetryHandle, out ring) &&
                   ring.IsCreated &&
                   ring.Length >= H8StaticDataFormat.TelemetryFrameCount &&
                   vault.TryReadOnlyHandle(in _btreeTelemetryCursorHandle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length >= 1 &&
                   vault.TryReadOnlyHandle(in _btreeTelemetryAccumulatorHandle, out accumulator) &&
                   accumulator.IsCreated &&
                   accumulator.Length >= 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTelemetry(uint stateHash, uint errorHash, uint requestedHash, long offset)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                _blackBoxHandle.BufferID == 0u ||
                _blackBoxCursorHandle.BufferID == 0u)
                return;

            int index = _blackBoxWriteIndex;
            if ((uint)index >= H8StaticDataFormat.TelemetryFrameCount)
                index = 0;

            H8StaticDataTelemetryEntry entry = default;
            entry.FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.StateHash = stateHash;
            entry.LastRequestedHash = requestedHash;
            entry.LookupCount = IsOpen ? _header.LookupCount : 0u;
            entry.RecordCount = IsOpen ? _header.RecordCount : 0u;
            entry.PayloadCrc32 = IsOpen ? _header.PayloadCrc32 : 0u;
            entry.Flags = IsOpen ? _header.Flags : 0u;
            entry.SchemaHash = IsOpen ? _header.SchemaHash : 0u;
            entry.FileByteLength = _mappedBytes;
            entry.LastOffset = offset;
            entry.ErrorHash = errorHash;
            entry.Reserved0 = _lastTreeDepth;
            entry.Reserved1 = _lastTreeKeysProcessed;
            entry.Reserved2 = _lastPrefetchTouchCount;

            if (!vault.TryAcquireWriteLock(in _blackBoxHandle, SystemID.CoreDataVault, out NativeArray<H8StaticDataTelemetryEntry> ring))
                return;

            try
            {
                if (!ring.IsCreated || ring.Length < H8StaticDataFormat.TelemetryFrameCount)
                    return;

                ring[index] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.CoreDataVault);
            }

            _blackBoxWriteIndex = (index + 1) % H8StaticDataFormat.TelemetryFrameCount;
            PublishTelemetryCursor(vault, in _blackBoxCursorHandle, _blackBoxWriteIndex);
            RecordBTreeTelemetry(stateHash == StateOpenHash && errorHash == 0u, errorHash, requestedHash, offset);
        }

        private void RecordBTreeTelemetry(bool found, uint errorHash, uint requestedHash, long offset)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                _btreeTelemetryHandle.BufferID == 0u ||
                _btreeTelemetryCursorHandle.BufferID == 0u ||
                _btreeTelemetryAccumulatorHandle.BufferID == 0u)
                return;

            uint safeOffset = offset >= 0L && offset <= uint.MaxValue ? (uint)offset : H8CacheBTree.NotFound;
            uint frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            if (!TryAccumulateBTreeTelemetryOneLock(
                    vault,
                    frameIndex,
                    found,
                    requestedHash,
                    safeOffset,
                    errorHash,
                    out BTreeTelemetryAccumulatorDTO accumulator))
            {
                return;
            }

            BTreeTelemetryAccumulatorDTO immediate = accumulator;
            immediate.Flags |= H8CacheBTree.BTreeTelemetryImmediateSampleFlag;
            int index = _btreeTelemetryWriteIndex;
            if ((uint)index >= H8StaticDataFormat.TelemetryFrameCount)
                index = 0;

            if (!TryWriteBTreeTelemetryRingOneLock(vault, index, in immediate))
                return;

            _btreeTelemetryWriteIndex = (index + 1) % H8StaticDataFormat.TelemetryFrameCount;
            PublishTelemetryCursor(vault, in _btreeTelemetryCursorHandle, _btreeTelemetryWriteIndex);
        }

        private bool TryAccumulateBTreeTelemetryOneLock(
            IDataVault vault,
            uint frameIndex,
            bool found,
            uint requestedHash,
            uint safeOffset,
            uint errorHash,
            out BTreeTelemetryAccumulatorDTO accumulator)
        {
            accumulator = default;
            if (vault == null ||
                !vault.TryAcquireWriteLock(
                    in _btreeTelemetryAccumulatorHandle,
                    SystemID.CoreDataVault,
                    out NativeArray<BTreeTelemetryAccumulatorDTO> accumulatorBuffer))
            {
                return false;
            }

            try
            {
                if (!accumulatorBuffer.IsCreated || accumulatorBuffer.Length < 1)
                    return false;

                accumulator = accumulatorBuffer[0];
                H8CacheBTree.AccumulateTelemetry(
                    ref accumulator,
                    frameIndex,
                    found,
                    requestedHash,
                    safeOffset,
                    _lastTreeDepth,
                    _lastTreeKeysProcessed,
                    _lastPrefetchTouchCount,
                    _btreeNodeCount,
                    _btreeRootOffset,
                    _lastSearchComputeTimeNs,
                    ResolveGlobalQualityWeight(),
                    errorHash);
                accumulatorBuffer[0] = accumulator;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _btreeTelemetryAccumulatorHandle, SystemID.CoreDataVault);
            }
        }

        private bool TryWriteBTreeTelemetryRingOneLock(
            IDataVault vault,
            int index,
            in BTreeTelemetryAccumulatorDTO immediate)
        {
            if (vault == null)
                return false;

            if (!vault.TryAcquireWriteLock(in _btreeTelemetryHandle, SystemID.CoreDataVault, out NativeArray<BTreeTelemetryEntry> ring))
                return false;

            try
            {
                if (!ring.IsCreated || ring.Length < H8StaticDataFormat.TelemetryFrameCount)
                    return false;

                ring[index] = H8CacheBTree.BuildTelemetryEntry(in immediate);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _btreeTelemetryHandle, SystemID.CoreDataVault);
            }
        }

        private static void PublishTelemetryCursor(
            IDataVault vault,
            in VaultGenerationHandle<int> cursorHandle,
            int writeIndex)
        {
            if (vault == null || cursorHandle.BufferID == 0u)
                return;

            if (!vault.TryAcquireWriteLock(in cursorHandle, SystemID.CoreDataVault, out NativeArray<int> cursor))
                return;

            try
            {
                if (cursor.IsCreated && cursor.Length >= 1)
                    cursor[0] = writeIndex;
            }
            finally
            {
                vault.ReleaseWriteLock(in cursorHandle, SystemID.CoreDataVault);
            }
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
