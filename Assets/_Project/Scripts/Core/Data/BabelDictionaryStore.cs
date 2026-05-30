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
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Zero-copy runtime reader for Babel_Dictionary.h8bin.
    /// </summary>
    public sealed unsafe class BabelDictionaryStore : IDisposable
    {
        private static int s_x001BabelDictionaryStoreSignalPushDropCount;
        private const uint StateOpenHash = 0x42424F50u;
        private const uint StateMissHash = 0x42424D49u;
        private const uint StateErrorHash = 0x42424552u;
        private const uint ErrorMissingHash = 0x4D495353u;
        private const uint ErrorCrcHash = 0x43524321u;
        private const uint ErrorHeaderHash = 0x48445221u;
        private const uint ErrorBoundsHash = 0x424E4453u;
        private const uint ErrorSortHash = 0x534F5254u;
        private const int FileStreamBufferBytes = 64 * 1024;
        private const int ErrorSliceBytes = 16;
        private const int LoreDecryptionMaskBytes = 16;
        private const long SlowLookupDumpThresholdNs = 100000L;

#if HECTON8_BABEL_MMF_AVAILABLE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
#endif
        private FileStream _fileStream;
        private byte* _basePointer;
        private byte* _ownedFallbackPointer;
        private byte* _errorPointer;
        private long _mappedBytes;
        private long _sourceFileBytes;
        private uint _paddingBytes;
        private H8BabelDictionaryHeader _header;
        private BabelIndexDTO* _indexPointer;
        private uint _btreeOffset;
        private uint _btreeRootOffset;
        private uint _btreeEndOffset;
        private uint _btreeNodeCount;
        private IDataVault _dataVault;
        private VaultGenerationHandle<H8StaticDataTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<int> _blackBoxCursorHandle;
        private VaultGenerationHandle<BTreeTelemetryEntry> _btreeTelemetryHandle;
        private VaultGenerationHandle<int> _btreeTelemetryCursorHandle;
        private VaultGenerationHandle<BTreeTelemetryAccumulatorDTO> _btreeTelemetryAccumulatorHandle;
        private VaultGenerationHandle<byte> _errorSliceHandle;
        private VaultGenerationHandle<byte> _mappedBytesHandle;
        private JobHandle _activeLoreReadHandle;
        private int _lastTelemetryFrame = -1;
        private uint _frameLookupCount;
        private uint _frameMissingHashCount;
        private uint _lastSearchComputeTimeNs;
        private uint _lastTreeDepth;
        private uint _lastTreeKeysProcessed;
        private uint _lastPrefetchTouchCount;
        private bool _errorSliceVaultBacked;
        private bool _activeLoreReadHandleValid;
        private bool _btreeAvailable;

        public bool IsOpen => _basePointer != null && _mappedBytes >= UnsafeUtility.SizeOf<H8BabelDictionaryHeader>();
        public int EntryCount => IsOpen ? (int)_header.EntryCount : 0;
        public uint PayloadCrc32 => IsOpen ? _header.PayloadCrc32 : 0u;
        public long MappedByteLength => _mappedBytes;
        public long SourceFileByteLength => _sourceFileBytes;
        public uint PaddingBytes => _paddingBytes;

        public BabelDictionaryStore(IDataVault dataVault = null)
        {
            PrewarmSignalLanes();
            _dataVault = dataVault;
        }

        private static void PrewarmSignalLanes()
        {
            SignalBus<PlayVoiceOverSignal>.Configure(expectedCapacity: 32, maxFrameSignals: 32, lowTierFrameSignals: 8);
            SignalBus<PlayVoiceOverSignal>.EnsureInitialized();
        }

        public void BindDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            if (_ownedFallbackPointer != null)
                CloseFile();

            ReleaseVaultHandles(_dataVault);
            _dataVault = dataVault;
            _blackBoxHandle = default;
            _blackBoxCursorHandle = default;
            _btreeTelemetryHandle = default;
            _btreeTelemetryCursorHandle = default;
            _btreeTelemetryAccumulatorHandle = default;
            if (_errorSliceVaultBacked)
            {
                _errorPointer = null;
                _errorSliceHandle = default;
                _errorSliceVaultBacked = false;
            }
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

            _sourceFileBytes = info.Length;
            long paddedLength = H8StaticDataFormat.AlignUp16(info.Length);
            _paddingBytes = (uint)(paddedLength - info.Length);

#if HECTON8_BABEL_MMF_AVAILABLE
            if (_paddingBytes == 0u)
            {
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
            }
            else
#endif
            {
                if (!TryAcquirePaddedDictionaryBuffer(paddedLength))
                {
                    RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
                    return false;
                }

                if (!LoadFileIntoPaddedBufferCold(path, _ownedFallbackPointer, info.Length))
                {
                    Dispose();
                    return false;
                }

                if (_paddingBytes > 0u)
                    UnsafeUtility.MemClear(_ownedFallbackPointer + info.Length, _paddingBytes);

                _basePointer = _ownedFallbackPointer;
                _mappedBytes = paddedLength;
            }

            if (!EnsureErrorSlice() || !ValidateHeaderAndChecksum() || !BuildIndexTable())
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
        public ReadOnlySpan<byte> FetchUtf8(uint hash)
        {
            if (!TryFindIndex(hash, out BabelIndexDTO entry, out _, out byte* basePointer, out long mappedBytes))
                return ReadOnlySpan<byte>.Empty;

            if (entry.ByteLength > int.MaxValue || entry.ByteOffset > mappedBytes - entry.ByteLength)
                return ReadOnlySpan<byte>.Empty;

            return new ReadOnlySpan<byte>(basePointer + entry.ByteOffset, (int)entry.ByteLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> FetchUtf8(uint hash, NativeArray<uint> linkedAudioHashes)
        {
            return FetchUtf8(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> TrackUtf8Lookup(uint hash)
        {
            return TrackUtf8Lookup(hash, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> TrackUtf8Lookup(uint hash, NativeArray<uint> linkedAudioHashes)
        {
            RefreshFrameLookupCounters();
            _frameLookupCount++;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool found = TryFindIndex(
                hash,
                out BabelIndexDTO entry,
                out int entryIndex,
                out uint depth,
                out uint keysProcessed,
                out uint prefetchTouchCount,
                out byte* basePointer,
                out long mappedBytes);
            long elapsedNs = ToNanoseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            CaptureLookupStats(depth, keysProcessed, prefetchTouchCount, elapsedNs);

            if (elapsedNs > SlowLookupDumpThresholdNs)
                DumpBlackBox();
            if (_lastSearchComputeTimeNs > H8CacheBTree.BTreeSlowBatchThresholdNs)
                DumpBTreeTelemetry();

            if (!found)
            {
                _frameMissingHashCount++;
                RecordTelemetry(StateMissHash, ErrorMissingHash, hash, elapsedNs);
                return ErrorSpan();
            }

            if (entry.ByteLength > int.MaxValue || entry.ByteOffset > mappedBytes - entry.ByteLength)
            {
                RecordTelemetry(StateErrorHash, ErrorBoundsHash, hash, entry.ByteOffset);
                return ErrorSpan();
            }

            TryPublishLinkedAudio(hash, linkedAudioHashes, entryIndex);
            RecordTelemetry(StateOpenHash, 0u, hash, entry.ByteOffset);
            return new ReadOnlySpan<byte>(basePointer + entry.ByteOffset, (int)entry.ByteLength);
        }

        /// <summary>
        /// Schedules XOR lore decryption into caller-owned native output. No managed strings are created.
        /// </summary>
        /// <param name="hash">FNV-1a text hash.</param>
        /// <param name="decryptionMask">Progress-derived XOR mask. Pass a 16-byte zero mask for clean bytes.</param>
        /// <param name="outputBytes">Caller-owned output buffer, sized for the returned byte length.</param>
        /// <param name="dependency">Input dependency for the scheduled job.</param>
        /// <param name="handle">Scheduled job handle, or the input dependency on failure.</param>
        /// <param name="byteLength">Requested UTF-8 byte length when scheduled.</param>
        /// <returns>True when a decrypt/copy job was scheduled.</returns>
        public bool TryScheduleLoreDecryption(
            uint hash,
            NativeArray<byte> decryptionMask,
            NativeArray<byte> outputBytes,
            JobHandle dependency,
            out JobHandle handle,
            out uint byteLength)
        {
            handle = dependency;
            byteLength = 0u;
            RefreshFrameLookupCounters();
            _frameLookupCount++;

            if (!IsOpen || !outputBytes.IsCreated || !decryptionMask.IsCreated || decryptionMask.Length <= 0)
            {
                RecordTelemetry(StateErrorHash, ErrorMissingHash, hash, 0L);
                return false;
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool found = TryFindIndex(
                hash,
                out BabelIndexDTO entry,
                out _,
                out uint depth,
                out uint keysProcessed,
                out uint prefetchTouchCount,
                out byte* basePointer,
                out long mappedBytes);
            long elapsedNs = ToNanoseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            CaptureLookupStats(depth, keysProcessed, prefetchTouchCount, elapsedNs);
            if (_lastSearchComputeTimeNs > H8CacheBTree.BTreeSlowBatchThresholdNs)
                DumpBTreeTelemetry();

            if (!found)
            {
                _frameMissingHashCount++;
                RecordTelemetry(StateMissHash, ErrorMissingHash, hash, elapsedNs);
                return false;
            }

            if (entry.ByteLength > int.MaxValue ||
                entry.ByteLength > outputBytes.Length ||
                entry.ByteOffset > mappedBytes - entry.ByteLength)
            {
                RecordTelemetry(StateErrorHash, ErrorBoundsHash, hash, entry.ByteOffset);
                return false;
            }

            if (entry.ByteLength == 0u)
            {
                RecordTelemetry(StateOpenHash, 0u, hash, entry.ByteOffset);
                return true;
            }

            byteLength = entry.ByteLength;
            if (_ownedFallbackPointer != null)
            {
                if (!TryResolveMappedBytesView(out NativeArray<byte> sourceBytes) ||
                    sourceBytes.Length < mappedBytes)
                {
                    RecordTelemetry(StateErrorHash, ErrorBoundsHash, hash, entry.ByteOffset);
                    return false;
                }

                BabelLoreXorDecryptJob job = new BabelLoreXorDecryptJob
                {
                    SourceBytes = sourceBytes,
                    DecryptionMask = decryptionMask,
                    OutputBytes = outputBytes,
                    SourceOffset = entry.ByteOffset,
                    ByteLength = entry.ByteLength
                };
                handle = job.Schedule((int)entry.ByteLength, 64, dependency);
            }
            else
            {
                BabelLoreXorDecryptPointerJob job = new BabelLoreXorDecryptPointerJob
                {
                    SourceBytes = basePointer,
                    SourceByteLength = mappedBytes,
                    DecryptionMask = decryptionMask,
                    OutputBytes = outputBytes,
                    SourceOffset = entry.ByteOffset,
                    ByteLength = entry.ByteLength
                };
                handle = job.Schedule((int)entry.ByteLength, 64, dependency);
            }

            RegisterLoreReadHandle(handle);
            RecordTelemetry(StateOpenHash, 0u, hash, entry.ByteOffset);
            return true;
        }

        /// <summary>
        /// Builds a deterministic 16-byte XOR mask from player progress. Full progress writes a zero mask.
        /// </summary>
        /// <remarks>
        /// Pass requiredKeyMask bits for the lore fragment and collectedKeyMask bits from player progression.
        /// Any missing bit generates deterministic noise; collecting all required bits clears the text.
        /// </remarks>
        public static bool TryBuildProgressDecryptionMask(
            NativeArray<byte> decryptionMask,
            uint collectedKeyMask,
            uint requiredKeyMask,
            uint loreSaltHash)
        {
            if (!decryptionMask.IsCreated || decryptionMask.Length < LoreDecryptionMaskBytes)
                return false;

            uint missingBits = requiredKeyMask & ~collectedKeyMask;
            if (missingBits == 0u)
            {
                for (int i = 0; i < LoreDecryptionMaskBytes; i++)
                    decryptionMask[i] = 0;
                return true;
            }

            uint state = BabelLoreMaskMath.Mix32(missingBits ^ loreSaltHash ^ 0xBABA004Du);
            for (int lane = 0; lane < 4; lane++)
            {
                state = BabelLoreMaskMath.Mix32(state + (uint)(lane * 0x9E3779B9u) + requiredKeyMask);
                int offset = lane << 2;
                decryptionMask[offset] = (byte)state;
                decryptionMask[offset + 1] = (byte)(state >> 8);
                decryptionMask[offset + 2] = (byte)(state >> 16);
                decryptionMask[offset + 3] = (byte)(state >> 24);
            }

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
            ReleaseVaultHandle(vault, ref _mappedBytesHandle);
            ReleaseVaultHandle(vault, ref _errorSliceHandle);
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

        private bool LoadFileIntoPaddedBufferCold(string path, byte* destination, long sourceLength)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))
            {
                long offset = 0L;
                while (offset < sourceLength)
                {
                    int chunkBytes = (int)Math.Min(FileStreamBufferBytes, sourceLength - offset);
                    int read = stream.Read(new Span<byte>(destination + offset, chunkBytes));
                    if (read <= 0)
                        break;

                    offset += read;
                }

                if (offset == sourceLength)
                    return true;

                RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, offset);
                return false;
            }
        }

        private bool TryAcquirePaddedDictionaryBuffer(long paddedLength)
        {
            if (paddedLength <= 0L || paddedLength > int.MaxValue)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _mappedBytesHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.BabelDictionaryMappedBytes,
                (int)paddedLength,
                SystemID.CoreDataVault,
                NativeArrayOptions.UninitializedMemory);

            if (!vault.TryResolveHandle(in _mappedBytesHandle, out NativeArray<byte> paddedBytes))
            {
                ReleaseVaultHandle(vault, ref _mappedBytesHandle);
                _ownedFallbackPointer = null;
                return false;
            }

            if (!paddedBytes.IsCreated || paddedBytes.Length < paddedLength)
            {
                ReleaseVaultHandle(vault, ref _mappedBytesHandle);
                _ownedFallbackPointer = null;
                return false;
            }

            _ownedFallbackPointer = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(paddedBytes);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveMappedBytes(out byte* basePointer, out long mappedBytes)
        {
            basePointer = _basePointer;
            mappedBytes = _mappedBytes;

            if (_ownedFallbackPointer == null)
                return basePointer != null && mappedBytes >= UnsafeUtility.SizeOf<H8BabelDictionaryHeader>();

            IDataVault vault = _dataVault;
            if (vault == null || _mappedBytesHandle.BufferID == 0u)
            {
                basePointer = null;
                mappedBytes = 0L;
                return false;
            }

            if (!vault.TryResolveHandle(in _mappedBytesHandle, out NativeArray<byte> mappedBytesView) ||
                !mappedBytesView.IsCreated ||
                mappedBytesView.Length < _mappedBytes)
            {
                basePointer = null;
                mappedBytes = 0L;
                return false;
            }

            basePointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(mappedBytesView);
            mappedBytes = _mappedBytes;
            return basePointer != null && mappedBytes >= UnsafeUtility.SizeOf<H8BabelDictionaryHeader>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveMappedBytesView(out NativeArray<byte> mappedBytesView)
        {
            mappedBytesView = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _mappedBytesHandle.BufferID != 0u &&
                   vault.TryResolveHandle(in _mappedBytesHandle, out mappedBytesView) &&
                   mappedBytesView.IsCreated &&
                   mappedBytesView.Length >= _mappedBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveReadableView(out byte* basePointer, out BabelIndexDTO* indexPointer, out long mappedBytes)
        {
            indexPointer = null;
            if (!TryResolveMappedBytes(out basePointer, out mappedBytes) ||
                _header.EntryCount == 0u ||
                _header.IndexOffset > mappedBytes)
            {
                basePointer = null;
                mappedBytes = 0L;
                return false;
            }

            indexPointer = (BabelIndexDTO*)(basePointer + _header.IndexOffset);
            return indexPointer != null;
        }

        private bool ValidateHeaderAndChecksum()
        {
            if (!TryResolveMappedBytes(out byte* basePointer, out long mappedBytes))
                return false;

            _header = UnsafeUtility.ReadArrayElement<H8BabelDictionaryHeader>(basePointer, 0);
            if (_header.Magic == ReverseUInt32(H8StaticDataFormat.BabelMagic))
                ReverseHeaderInPlace(ref _header);

            long logicalFileBytes = _header.FileByteLength;
            if (_header.Magic != H8StaticDataFormat.BabelMagic ||
                _header.FormatVersion != H8StaticDataFormat.FormatVersion ||
                _header.HeaderSizeBytes != UnsafeUtility.SizeOf<H8BabelDictionaryHeader>() ||
                (logicalFileBytes != _sourceFileBytes && logicalFileBytes != mappedBytes) ||
                logicalFileBytes > mappedBytes ||
                (_header.Flags & H8StaticDataFormat.LittleEndianFlag) == 0u)
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, 0L);
                return false;
            }

            int entrySize = UnsafeUtility.SizeOf<BabelIndexDTO>();
            long indexBytes = (long)_header.EntryCount * entrySize;
            if (_header.EntryCount > int.MaxValue ||
                _header.IndexOffset < _header.HeaderSizeBytes ||
                _header.DataOffset < _header.IndexOffset ||
                _header.IndexOffset + indexBytes > _header.DataOffset ||
                _header.DataOffset > _header.FileByteLength ||
                (_header.IndexOffset & 15u) != 0u ||
                (_header.DataOffset & 15u) != 0u)
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, 0L);
                return false;
            }

            uint crc = H8Crc32.Compute(new ReadOnlySpan<byte>(
                basePointer + _header.HeaderSizeBytes,
                (int)(logicalFileBytes - _header.HeaderSizeBytes)));
            if (crc != _header.PayloadCrc32)
            {
                RecordTelemetry(StateErrorHash, ErrorCrcHash, 0u, 0L);
                return false;
            }

            return true;
        }

        private bool BuildIndexTable()
        {
            int count = (int)_header.EntryCount;
            if (count <= 0)
                return false;

            _indexPointer = (BabelIndexDTO*)(_basePointer + _header.IndexOffset);
            if (!H8CacheBTree.TryResolveTree(
                    _header.Flags,
                    _header.IndexOffset,
                    _header.EntryCount,
                    (uint)UnsafeUtility.SizeOf<BabelIndexDTO>(),
                    _header.DataOffset,
                    out _btreeOffset,
                    out _btreeRootOffset,
                    out _btreeNodeCount))
            {
                RecordTelemetry(StateErrorHash, ErrorHeaderHash, 0u, _header.DataOffset);
                return false;
            }

            _btreeEndOffset = _header.DataOffset;
            uint previousHash = 0u;
            for (int i = 0; i < count; i++)
            {
                BabelIndexDTO entry = UnsafeUtility.ReadArrayElement<BabelIndexDTO>(_indexPointer, i);
                if ((entry.StringHash == 0u && count > 1) ||
                    (i > 0 && entry.StringHash <= previousHash) ||
                    entry.ByteLength == 0u ||
                    (entry.ByteOffset & 15u) != 0u ||
                    entry.ByteOffset < _header.DataOffset ||
                    entry.ByteLength > _header.FileByteLength ||
                    entry.ByteOffset > _header.FileByteLength - entry.ByteLength)
                {
                    RecordTelemetry(StateErrorHash, i > 0 && entry.StringHash <= previousHash ? ErrorSortHash : ErrorBoundsHash, entry.StringHash, entry.ByteOffset);
                    return false;
                }

                previousHash = entry.StringHash;
            }

            if (!ValidateBTreeEdge(entriesCount: count))
                return false;

            _btreeAvailable = true;
            return true;
        }

        private bool TryFindIndex(uint hash, out BabelIndexDTO entry, out int entryIndex)
        {
            return TryFindIndex(hash, out entry, out entryIndex, out _, out _, out _);
        }

        private bool TryFindIndex(
            uint hash,
            out BabelIndexDTO entry,
            out int entryIndex,
            out byte* basePointer,
            out long mappedBytes)
        {
            return TryFindIndex(hash, out entry, out entryIndex, out _, out _, out _, out basePointer, out mappedBytes);
        }

        private bool TryFindIndex(
            uint hash,
            out BabelIndexDTO entry,
            out int entryIndex,
            out uint depth,
            out uint keysProcessed,
            out uint prefetchTouchCount)
        {
            return TryFindIndex(hash, out entry, out entryIndex, out depth, out keysProcessed, out prefetchTouchCount, out _, out _);
        }

        private bool TryFindIndex(
            uint hash,
            out BabelIndexDTO entry,
            out int entryIndex,
            out uint depth,
            out uint keysProcessed,
            out uint prefetchTouchCount,
            out byte* basePointer,
            out long mappedBytes)
        {
            if (!_btreeAvailable || !TryResolveReadableView(out basePointer, out BabelIndexDTO* indexPointer, out mappedBytes))
            {
                entry = default;
                entryIndex = -1;
                depth = 0u;
                keysProcessed = 0u;
                prefetchTouchCount = 0u;
                basePointer = null;
                mappedBytes = 0L;
                return false;
            }

            bool found = H8CacheBTree.TryFindValue(
                basePointer,
                _btreeOffset,
                _btreeRootOffset,
                _btreeEndOffset,
                hash,
                ResolveGlobalQualityWeight(),
                out uint value,
                out depth,
                out keysProcessed,
                out prefetchTouchCount);

            if (!found || value >= _header.EntryCount)
            {
                entry = default;
                entryIndex = -1;
                return false;
            }

            entryIndex = (int)value;
            entry = UnsafeUtility.ReadArrayElement<BabelIndexDTO>(indexPointer, entryIndex);
            return entry.StringHash == hash;
        }

        private void CaptureLookupStats(uint depth, uint keysProcessed, uint prefetchTouchCount, long elapsedNs)
        {
            _lastSearchComputeTimeNs = elapsedNs <= 0L
                ? 0u
                : elapsedNs >= uint.MaxValue
                    ? uint.MaxValue
                    : (uint)elapsedNs;
            _lastTreeDepth = depth;
            _lastTreeKeysProcessed = keysProcessed;
            _lastPrefetchTouchCount = prefetchTouchCount;
        }

        private bool ValidateBTreeEdge(int entriesCount)
        {
            if (entriesCount <= 0 ||
                !TryResolveReadableView(out byte* basePointer, out BabelIndexDTO* indexPointer, out _))
                return false;

            BabelIndexDTO first = UnsafeUtility.ReadArrayElement<BabelIndexDTO>(indexPointer, 0);
            BabelIndexDTO last = UnsafeUtility.ReadArrayElement<BabelIndexDTO>(indexPointer, entriesCount - 1);
            return H8CacheBTree.TryFindValue(
                    basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    first.StringHash,
                    0f,
                    out uint firstIndex,
                    out _,
                    out _,
                    out _) &&
                firstIndex == 0u &&
                H8CacheBTree.TryFindValue(
                    basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    last.StringHash,
                    0f,
                    out uint lastIndex,
                    out _,
                    out _,
                    out _) &&
                lastIndex == (uint)(entriesCount - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private static void TryPublishLinkedAudio(uint textHash, NativeArray<uint> linkedAudioHashes, int entryIndex)
        {
            if (!linkedAudioHashes.IsCreated || (uint)entryIndex >= (uint)linkedAudioHashes.Length)
                return;

            uint voiceHash = linkedAudioHashes[entryIndex];
            if (voiceHash == 0u)
                return;

            PlayVoiceOverSignal signal = new PlayVoiceOverSignal
            {
                TextHash = textHash,
                VoiceHash = voiceHash,
                FrameIndex = SystemDispatcher.CurrentFrameId,
                Flags = 1u
            };
            SignalBus<PlayVoiceOverSignal>.TryPushTracked(in signal, ref s_x001BabelDictionaryStoreSignalPushDropCount);
        }

        private static void ReverseHeaderInPlace(ref H8BabelDictionaryHeader header)
        {
            header.Magic = ReverseUInt32(header.Magic);
            header.FormatVersion = ReverseUInt16(header.FormatVersion);
            header.HeaderSizeBytes = ReverseUInt16(header.HeaderSizeBytes);
            header.EntryCount = ReverseUInt32(header.EntryCount);
            header.IndexOffset = ReverseUInt32(header.IndexOffset);
            header.DataOffset = ReverseUInt32(header.DataOffset);
            header.FileByteLength = ReverseUInt32(header.FileByteLength);
            header.PayloadCrc32 = ReverseUInt32(header.PayloadCrc32);
            header.Flags = ReverseUInt32(header.Flags);
        }

        private static ushort ReverseUInt16(ushort value)
        {
            return (ushort)((value << 8) | (value >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseUInt32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private bool EnsureErrorSlice()
        {
            if (_errorSliceVaultBacked && TryReadErrorSlice(out _))
                return true;

            if (!_errorSliceVaultBacked && _errorPointer != null)
                return true;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                    return false;

                _errorSliceHandle = vault.EnsureGenerationHandle<byte>(
                    BufferID.BabelErrorUtf8,
                    ErrorSliceBytes,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
                if (TryWriteErrorSlice(vault))
                {
                    _errorPointer = null;
                    _errorSliceVaultBacked = true;
                    return true;
                }

                ReleaseVaultHandle(vault, ref _errorSliceHandle);
            }

            RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
            return false;
        }

        private bool TryReadErrorSlice(out NativeArray<byte>.ReadOnly errorBytes)
        {
            errorBytes = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _errorSliceHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _errorSliceHandle, out errorBytes) &&
                   errorBytes.IsCreated &&
                   errorBytes.Length >= ErrorSliceBytes;
        }

        private bool TryWriteErrorSlice(IDataVault vault)
        {
            if (vault == null ||
                _errorSliceHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _errorSliceHandle, SystemID.CoreDataVault, out NativeArray<byte> errorBytes))
            {
                return false;
            }

            try
            {
                if (!errorBytes.IsCreated || errorBytes.Length < ErrorSliceBytes)
                    return false;

                WriteErrorSlice(errorBytes);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _errorSliceHandle, SystemID.CoreDataVault);
            }
        }

        private static void WriteErrorSlice(NativeArray<byte> destination)
        {
            destination[0] = (byte)'E';
            destination[1] = (byte)'R';
            destination[2] = (byte)'R';
            destination[3] = (byte)'O';
            destination[4] = (byte)'R';
        }

        private ReadOnlySpan<byte> ErrorSpan()
        {
            if (_errorSliceVaultBacked)
            {
                return TryReadErrorSlice(out NativeArray<byte>.ReadOnly errorBytes)
                    ? new ReadOnlySpan<byte>(errorBytes.GetUnsafeReadOnlyPtr(), 5)
                    : ReadOnlySpan<byte>.Empty;
            }

            return _errorPointer == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(_errorPointer, 5);
        }

        private void CloseFile()
        {
            CompleteActiveLoreReadsForClose();

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
                _ownedFallbackPointer = null;
            }

            if (_mappedBytesHandle.BufferID != 0u)
                _dataVault?.ReleaseBuffer(in _mappedBytesHandle);
            _mappedBytesHandle = default;

            if (_errorPointer != null)
            {
                _errorPointer = null;
            }

            if (_errorSliceHandle.BufferID != 0u)
                _dataVault?.ReleaseBuffer(in _errorSliceHandle);
            _errorSliceHandle = default;
            _errorSliceVaultBacked = false;
            _basePointer = null;
            _indexPointer = null;
            _btreeOffset = 0u;
            _btreeRootOffset = 0u;
            _btreeEndOffset = 0u;
            _btreeNodeCount = 0u;
            _btreeAvailable = false;
            _mappedBytes = 0L;
            _sourceFileBytes = 0L;
            _paddingBytes = 0u;
            _lastTelemetryFrame = -1;
            _frameLookupCount = 0u;
            _frameMissingHashCount = 0u;
            _lastSearchComputeTimeNs = 0u;
            _lastTreeDepth = 0u;
            _lastTreeKeysProcessed = 0u;
            _lastPrefetchTouchCount = 0u;
            _activeLoreReadHandle = default;
            _activeLoreReadHandleValid = false;
            _header = default;
        }

        private void RegisterLoreReadHandle(JobHandle handle)
        {
            ClearCompletedLoreReadFence();
            _activeLoreReadHandle = _activeLoreReadHandleValid
                ? JobHandle.CombineDependencies(_activeLoreReadHandle, handle)
                : handle;
            _activeLoreReadHandleValid = true;
        }

        private void ClearCompletedLoreReadFence()
        {
            if (!_activeLoreReadHandleValid || !_activeLoreReadHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeLoreReadHandle))
                return;

            _activeLoreReadHandleValid = false;
        }

        private void CompleteActiveLoreReadsForClose()
        {
            if (!_activeLoreReadHandleValid)
                return;

            // [BLOCKING_SYNC_POINT] Structural close/reload gate. The MMF/Vault pointer must not be
            // released while a scheduled lore decrypt job still reads SourceBytes.
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _activeLoreReadHandle, forceComplete: true);
                _activeLoreReadHandleValid = false;
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
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
                    BufferID.BabelTelemetryRing,
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
                    BufferID.BabelTelemetryCursor,
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
                    BufferID.BabelBTreeTelemetryRing,
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
                    BufferID.BabelBTreeTelemetryCursor,
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
                    BufferID.BabelBTreeTelemetryAccumulator,
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

        private void RefreshFrameLookupCounters()
        {
            int frame = Mathf.Max(0, SystemDispatcher.CurrentFrameIndex);
            if (frame == _lastTelemetryFrame)
                return;

            _lastTelemetryFrame = frame;
            _frameLookupCount = 0u;
            _frameMissingHashCount = 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTelemetry(uint stateHash, uint errorHash, uint requestedHash, long offset)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                _blackBoxHandle.BufferID == 0u ||
                _blackBoxCursorHandle.BufferID == 0u ||
                !TryReadBlackBox(out _, out NativeArray<int>.ReadOnly cursor))
            {
                return;
            }

            int index = cursor[0];
            if ((uint)index >= H8StaticDataFormat.TelemetryFrameCount)
                index = 0;

            H8StaticDataTelemetryEntry entry = default;
            entry.FrameIndex = SystemDispatcher.CurrentFrameId;
            entry.StateHash = stateHash;
            entry.LastRequestedHash = requestedHash;
            entry.LookupCount = _frameLookupCount;
            entry.RecordCount = IsOpen ? _header.EntryCount : 0u;
            entry.PayloadCrc32 = IsOpen ? _header.PayloadCrc32 : 0u;
            entry.Flags = IsOpen ? _header.Flags : 0u;
            entry.SchemaHash = H8StaticDataFormat.SchemaHash;
            entry.FileByteLength = _mappedBytes;
            entry.LastOffset = offset;
            entry.ErrorHash = errorHash;
            entry.Reserved0 = _frameMissingHashCount;
            entry.Reserved1 = _lastSearchComputeTimeNs;
            entry.Reserved2 = (_lastTreeDepth & 0xFFu) |
                ((_lastPrefetchTouchCount & 0xFFu) << 8) |
                (((_lastTreeKeysProcessed > 0xFFFFu ? 0xFFFFu : _lastTreeKeysProcessed) & 0xFFFFu) << 16);

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

            PublishTelemetryCursor(vault, in _blackBoxCursorHandle, (index + 1) % H8StaticDataFormat.TelemetryFrameCount);
            RecordBTreeTelemetry(stateHash == StateOpenHash && errorHash == 0u, errorHash, requestedHash, offset);
        }

        private void RecordBTreeTelemetry(bool found, uint errorHash, uint requestedHash, long offset)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                _btreeTelemetryHandle.BufferID == 0u ||
                _btreeTelemetryCursorHandle.BufferID == 0u ||
                _btreeTelemetryAccumulatorHandle.BufferID == 0u ||
                !TryReadBTreeTelemetry(out _, out NativeArray<int>.ReadOnly cursor, out _))
            {
                return;
            }

            uint safeOffset = offset >= 0L && offset <= uint.MaxValue ? (uint)offset : H8CacheBTree.NotFound;
            uint frameIndex = SystemDispatcher.CurrentFrameId;
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
            int index = cursor[0];
            if ((uint)index >= H8StaticDataFormat.TelemetryFrameCount)
                index = 0;

            if (!TryWriteBTreeTelemetryRingOneLock(vault, index, in immediate))
                return;

            PublishTelemetryCursor(vault, in _btreeTelemetryCursorHandle, (index + 1) % H8StaticDataFormat.TelemetryFrameCount);
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal unsafe struct BabelBTreeSearchKernel : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // The index pointer and base pointer are read-only views into the already validated Babel MMF/Vault blob.
            // Unity's container safety cannot track these raw pointers, but Open() validates the B-Tree section,
            // index bounds, entry count, and UTF-8 slice bounds before any job is scheduled.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Copying the index into a temporary NativeArray was rejected because lookup must stay inside the
            // monolithic unmanaged blob. Managed dictionaries and flat midpoint search were rejected because they
            // violate the zero-GC and cache-line-locality mandates.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Execute() only writes Output[index]. The tree value is a validated index into IndexTable; malformed
            // values are converted to miss flags rather than dereferenced.
            [NoAlias, NativeDisableUnsafePtrRestriction] internal byte* BasePointer;
            [NoAlias, NativeDisableUnsafePtrRestriction] internal BabelIndexDTO* IndexTable;
            [ReadOnly, NoAlias] public NativeArray<uint> TextHashes;
            [WriteOnly, NoAlias] public NativeArray<BabelLookupResultDTO> Output;
            public uint EntryCount;
            public uint TreeOffset;
            public uint RootOffset;
            public uint TreeEndOffset;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                uint target = TextHashes[index];
                bool found = H8CacheBTree.TryFindValue(
                    BasePointer,
                    TreeOffset,
                    RootOffset,
                    TreeEndOffset,
                    target,
                    GlobalQualityWeight,
                    out uint entryIndex,
                    out _,
                    out _,
                    out _);

                if (found && entryIndex < EntryCount)
                {
                    BabelIndexDTO entry = IndexTable[entryIndex];
                    if (entry.StringHash == target)
                    {
                        Output[index] = new BabelLookupResultDTO
                        {
                            TextHash = target,
                            ByteOffset = entry.ByteOffset,
                            ByteLength = entry.ByteLength,
                            Flags = 1u
                        };
                        return;
                    }
                }

                Output[index] = new BabelLookupResultDTO
                {
                    TextHash = target,
                    Flags = 2u
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct BabelEndiannessValidationJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> DictionaryBytes;
            [NoAlias] public NativeArray<BabelIndexDTO> IndexTable;
            [WriteOnly, NoAlias] public NativeArray<int> ResultCode;

            public void Execute()
            {
                if (!DictionaryBytes.IsCreated || DictionaryBytes.Length < 4 || !IndexTable.IsCreated)
                {
                    WriteResult(-1);
                    return;
                }

                uint magic = ReadUInt32(DictionaryBytes, 0);
                if (magic == H8StaticDataFormat.BabelMagic)
                {
                    WriteResult(0);
                    return;
                }

                if (magic != ReverseUInt32(H8StaticDataFormat.BabelMagic))
                {
                    WriteResult(-2);
                    return;
                }

                for (int i = 0; i < IndexTable.Length; i++)
                {
                    BabelIndexDTO entry = IndexTable[i];
                    entry.StringHash = ReverseUInt32(entry.StringHash);
                    entry.ByteOffset = ReverseUInt32(entry.ByteOffset);
                    entry.ByteLength = ReverseUInt32(entry.ByteLength);
                    entry._pad0 = ReverseUInt32(entry._pad0);
                    IndexTable[i] = entry;
                }

                WriteResult(1);
            }

            private void WriteResult(int value)
            {
                if (ResultCode.IsCreated && ResultCode.Length > 0)
                    ResultCode[0] = value;
            }

            private static uint ReadUInt32(NativeArray<byte> bytes, int offset)
            {
                return (uint)bytes[offset] |
                    ((uint)bytes[offset + 1] << 8) |
                    ((uint)bytes[offset + 2] << 16) |
                    ((uint)bytes[offset + 3] << 24);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ReverseUInt32(uint value)
            {
                return ((value & 0x000000FFu) << 24) |
                       ((value & 0x0000FF00u) << 8) |
                       ((value & 0x00FF0000u) >> 8) |
                       ((value & 0xFF000000u) >> 24);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct BabelLoreXorDecryptJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceBytes;
            [ReadOnly, NoAlias] public NativeArray<byte> DecryptionMask;
            [WriteOnly, NoAlias] public NativeArray<byte> OutputBytes;
            public uint SourceOffset;
            public uint ByteLength;

            public void Execute(int index)
            {
                if ((uint)index >= ByteLength || (uint)index >= (uint)OutputBytes.Length)
                    return;

                int sourceIndex = (int)SourceOffset + index;
                if ((uint)sourceIndex >= (uint)SourceBytes.Length)
                {
                    OutputBytes[index] = 0;
                    return;
                }

                int maskLength = DecryptionMask.IsCreated ? DecryptionMask.Length : 0;
                byte mask = maskLength > 0 ? DecryptionMask[index % maskLength] : (byte)0;
                OutputBytes[index] = (byte)(SourceBytes[sourceIndex] ^ mask);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal unsafe struct BabelLoreXorDecryptPointerJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // SourceBytes points at a read-only MMF-backed Babel byte blob. Vault mirrors use
            // BabelLoreXorDecryptJob so Unity safety handles carry the NativeArray source view.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Copying lore source bytes into another NativeArray before decrypting was rejected because it
            // doubles bandwidth and defeats the MMF path. Decrypting to string/byte[] was rejected because
            // it allocates and crosses UI ownership.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // The caller owns OutputBytes and passes a created DecryptionMask. Each Execute(index) writes
            // only OutputBytes[index], so parallel iterations do not share output slots; the source pointer
            // is read-only for the whole scheduled range.
            [NoAlias, NativeDisableUnsafePtrRestriction] internal byte* SourceBytes;
            public long SourceByteLength;
            [ReadOnly, NoAlias] public NativeArray<byte> DecryptionMask;
            [WriteOnly, NoAlias] public NativeArray<byte> OutputBytes;
            public uint SourceOffset;
            public uint ByteLength;

            public void Execute(int index)
            {
                if ((uint)index >= ByteLength || (uint)index >= (uint)OutputBytes.Length)
                    return;

                long sourceIndex = (long)SourceOffset + index;
                if (SourceBytes == null || sourceIndex < 0L || sourceIndex >= SourceByteLength)
                {
                    OutputBytes[index] = 0;
                    return;
                }

                int maskLength = DecryptionMask.IsCreated ? DecryptionMask.Length : 0;
                byte mask = maskLength > 0 ? DecryptionMask[index % maskLength] : (byte)0;
                OutputBytes[index] = (byte)(SourceBytes[sourceIndex] ^ mask);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct MockSpanCountJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Utf8Blob;
            [ReadOnly, NoAlias] public NativeArray<BabelLookupResultDTO> Slices;
            [WriteOnly, NoAlias] public NativeArray<int> CharacterCounts;

            public void Execute(int index)
            {
                BabelLookupResultDTO slice = Slices[index];
                long offset = slice.ByteOffset;
                long length = slice.ByteLength;
                CharacterCounts[index] = offset <= Utf8Blob.Length && length <= Utf8Blob.Length - offset
                    ? (int)slice.ByteLength
                    : 0;
            }
        }
    }

    public static class BabelLookupScalability
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveFrameLookupBudget(float globalQualityWeight, int requestedCount)
        {
            if (requestedCount <= 0)
                return 0;

            float requested = requestedCount;
            float lowBudget = math.min(20f, requested);
            float quality = math.saturate(globalQualityWeight);
            float ramp = math.saturate((quality - 0.5f) * 2f);
            float smooth = ramp * ramp * (3f - (2f * ramp));
            float budget = math.lerp(lowBudget, requested, smooth);
            return math.clamp((int)math.ceil(budget), 1, requestedCount);
        }
    }

    internal static class BabelLoreMaskMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix32(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }
    }
}
