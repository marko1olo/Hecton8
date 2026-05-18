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
        private IDataVault _dataVault;
        private VaultBufferHandle<H8StaticDataTelemetryEntry> _blackBoxHandle;
        private VaultBufferHandle<int> _blackBoxCursorHandle;
        private VaultBufferHandle<byte> _errorSliceHandle;
        private VaultBufferHandle<byte> _paddedDictionaryHandle;
        private JobHandle _activeLoreReadHandle;
        private int _lastTelemetryFrame = -1;
        private uint _frameLookupCount;
        private uint _frameMissingHashCount;
        private uint _lastSearchComputeTimeNs;
        private bool _errorSliceVaultBacked;
        private bool _activeLoreReadHandleValid;

        public bool IsOpen => _basePointer != null && _mappedBytes >= UnsafeUtility.SizeOf<H8BabelDictionaryHeader>();
        public int EntryCount => IsOpen ? (int)_header.EntryCount : 0;
        public uint PayloadCrc32 => IsOpen ? _header.PayloadCrc32 : 0u;
        public long MappedByteLength => _mappedBytes;
        public long SourceFileByteLength => _sourceFileBytes;
        public uint PaddingBytes => _paddingBytes;

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

                if (!ReadFileIntoPaddedBuffer(path, _ownedFallbackPointer, info.Length))
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
        public ReadOnlySpan<byte> GetUtf8(uint hash)
        {
            return GetUtf8(hash, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetUtf8(uint hash, NativeArray<uint> linkedAudioHashes)
        {
            RefreshFrameLookupCounters();
            _frameLookupCount++;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool found = TryFindIndex(hash, out BabelIndexDTO entry, out int entryIndex);
            long elapsedNs = ToNanoseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            _lastSearchComputeTimeNs = (uint)math.min(uint.MaxValue, elapsedNs);

            if (elapsedNs > SlowLookupDumpThresholdNs)
                DumpBlackBox();

            if (!found)
            {
                _frameMissingHashCount++;
                RecordTelemetry(StateMissHash, ErrorMissingHash, hash, elapsedNs);
                return ErrorSpan();
            }

            if (entry.ByteLength > int.MaxValue || entry.ByteOffset > _mappedBytes - entry.ByteLength)
            {
                RecordTelemetry(StateErrorHash, ErrorBoundsHash, hash, entry.ByteOffset);
                return ErrorSpan();
            }

            TryPublishLinkedAudio(hash, linkedAudioHashes, entryIndex);
            RecordTelemetry(StateOpenHash, 0u, hash, entry.ByteOffset);
            return new ReadOnlySpan<byte>(_basePointer + entry.ByteOffset, (int)entry.ByteLength);
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
            bool found = TryFindIndex(hash, out BabelIndexDTO entry, out _);
            long elapsedNs = ToNanoseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            _lastSearchComputeTimeNs = (uint)math.min(uint.MaxValue, elapsedNs);

            if (!found)
            {
                _frameMissingHashCount++;
                RecordTelemetry(StateMissHash, ErrorMissingHash, hash, elapsedNs);
                return false;
            }

            if (entry.ByteLength > int.MaxValue ||
                entry.ByteLength > outputBytes.Length ||
                entry.ByteOffset > _mappedBytes - entry.ByteLength)
            {
                RecordTelemetry(StateErrorHash, ErrorBoundsHash, hash, entry.ByteOffset);
                return false;
            }

            if (entry.ByteLength == 0u)
            {
                RecordTelemetry(StateOpenHash, 0u, hash, entry.ByteOffset);
                return true;
            }

            BabelLoreXorDecryptPointerJob job = new BabelLoreXorDecryptPointerJob
            {
                SourceBytes = _basePointer,
                SourceByteLength = _mappedBytes,
                DecryptionMask = decryptionMask,
                OutputBytes = outputBytes,
                SourceOffset = entry.ByteOffset,
                ByteLength = entry.ByteLength
            };

            byteLength = entry.ByteLength;
            handle = job.Schedule((int)entry.ByteLength, 64, dependency);
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
            if (!EnsureBlackBox())
                return;

            H8StaticDataTelemetryEntry* ring = (H8StaticDataTelemetryEntry*)_blackBoxHandle.ResolvePointer(_dataVault);
            int* cursor = (int*)_blackBoxCursorHandle.ResolvePointer(_dataVault);
            if (ring == null || cursor == null)
                return;

            string resolvedPath = string.IsNullOrEmpty(path)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_BABEL_FIXER.bin"))
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

        private bool ReadFileIntoPaddedBuffer(string path, byte* destination, long sourceLength)
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

            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _paddedDictionaryHandle = vault.GetBufferHandle<byte>(
                BufferID.BabelDictionaryMappedBytes,
                (int)paddedLength,
                SystemID.CoreDataVault,
                NativeArrayOptions.UninitializedMemory);
            _ownedFallbackPointer = (byte*)_paddedDictionaryHandle.ResolvePointer(vault);
            if (_ownedFallbackPointer == null)
            {
                _paddedDictionaryHandle = default;
                return false;
            }

            _dataVault = vault;
            return true;
        }

        private bool ValidateHeaderAndChecksum()
        {
            _header = UnsafeUtility.ReadArrayElement<H8BabelDictionaryHeader>(_basePointer, 0);
            if (_header.Magic == ReverseUInt32(H8StaticDataFormat.BabelMagic))
                ReverseHeaderInPlace(ref _header);

            long logicalFileBytes = _header.FileByteLength;
            if (_header.Magic != H8StaticDataFormat.BabelMagic ||
                _header.FormatVersion != H8StaticDataFormat.FormatVersion ||
                _header.HeaderSizeBytes != UnsafeUtility.SizeOf<H8BabelDictionaryHeader>() ||
                (logicalFileBytes != _sourceFileBytes && logicalFileBytes != _mappedBytes) ||
                logicalFileBytes > _mappedBytes ||
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

            uint crc = H8Crc32.Compute(_basePointer + _header.HeaderSizeBytes, (int)(logicalFileBytes - _header.HeaderSizeBytes));
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

            return true;
        }

        private bool TryFindIndex(uint hash, out BabelIndexDTO entry, out int entryIndex)
        {
            int low = 0;
            int high = _indexPointer != null ? (int)_header.EntryCount - 1 : -1;
            while (low <= high)
            {
                int mid = (int)(((uint)low + (uint)high) >> 1);
                BabelIndexDTO candidate = _indexPointer[mid];
                if (candidate.StringHash == hash)
                {
                    entry = candidate;
                    entryIndex = mid;
                    return true;
                }

                if (candidate.StringHash < hash)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            entry = default;
            entryIndex = -1;
            return false;
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
                FrameIndex = (uint)Mathf.Max(0, Time.frameCount),
                Flags = 1u
            };
            SignalBus<PlayVoiceOverSignal>.TryPush(in signal);
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
            if (_errorPointer != null)
                return true;

            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault != null)
            {
                _errorSliceHandle = vault.GetBufferHandle<byte>(
                    BufferID.BabelErrorUtf8,
                    ErrorSliceBytes,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
                _errorPointer = (byte*)_errorSliceHandle.ResolvePointer(vault);
                if (_errorPointer != null)
                {
                    _dataVault = vault;
                    _errorSliceVaultBacked = true;
                    WriteErrorSlice();
                    return true;
                }

                _errorSliceHandle = default;
            }

            RecordTelemetry(StateErrorHash, ErrorMissingHash, 0u, 0L);
            return false;
        }

        private void WriteErrorSlice()
        {
            _errorPointer[0] = (byte)'E';
            _errorPointer[1] = (byte)'R';
            _errorPointer[2] = (byte)'R';
            _errorPointer[3] = (byte)'O';
            _errorPointer[4] = (byte)'R';
        }

        private ReadOnlySpan<byte> ErrorSpan()
        {
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
                _paddedDictionaryHandle = default;
            }

            if (_errorPointer != null)
            {
                _errorPointer = null;
            }

            _errorSliceHandle = default;
            _errorSliceVaultBacked = false;
            _basePointer = null;
            _indexPointer = null;
            _mappedBytes = 0L;
            _sourceFileBytes = 0L;
            _paddingBytes = 0u;
            _lastTelemetryFrame = -1;
            _frameLookupCount = 0u;
            _frameMissingHashCount = 0u;
            _lastSearchComputeTimeNs = 0u;
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

            // [BLOCKING_SYNC_POINT] Non-blocking fence clear: IsCompleted was true before Complete().
            _activeLoreReadHandle.Complete();
            _activeLoreReadHandle = default;
            _activeLoreReadHandleValid = false;
        }

        private void CompleteActiveLoreReadsForClose()
        {
            if (!_activeLoreReadHandleValid)
                return;

            // [BLOCKING_SYNC_POINT] Structural close/reload gate. The MMF/Vault pointer must not be
            // released while a scheduled lore decrypt job still reads SourceBytes.
            _activeLoreReadHandle.Complete();
            _activeLoreReadHandle = default;
            _activeLoreReadHandleValid = false;
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

        private void RefreshFrameLookupCounters()
        {
            int frame = Mathf.Max(0, Time.frameCount);
            if (frame == _lastTelemetryFrame)
                return;

            _lastTelemetryFrame = frame;
            _frameLookupCount = 0u;
            _frameMissingHashCount = 0u;
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
                LookupCount = _frameLookupCount,
                RecordCount = IsOpen ? _header.EntryCount : 0u,
                PayloadCrc32 = IsOpen ? _header.PayloadCrc32 : 0u,
                Flags = IsOpen ? _header.Flags : 0u,
                SchemaHash = H8StaticDataFormat.SchemaHash,
                FileByteLength = _mappedBytes,
                LastOffset = offset,
                ErrorHash = errorHash,
                Reserved0 = _frameMissingHashCount,
                Reserved1 = _lastSearchComputeTimeNs,
                Reserved2 = _paddingBytes
            };
            *cursor = (index + 1) % H8StaticDataFormat.TelemetryFrameCount;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct BabelBinarySearchKernel : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // The index pointer is a read-only view into the already validated Babel MMF/Vault blob.
            // Unity's container safety cannot track this raw pointer, but the schedule contract supplies
            // EntryCount and no job writes through IndexTable.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Copying the index into a temporary NativeArray was rejected because the task explicitly
            // requires byte-offset lookup against the monolithic unmanaged blob. A managed dictionary was
            // rejected because it violates the Babel zero-GC and cache locality mandate.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Open() validates header size, index offset, entry count, sort order, and slice bounds before
            // this pointer is exposed. Execute() only reads indices in [0, EntryCount), and Output is the
            // only mutated container.
            [NoAlias, NativeDisableUnsafePtrRestriction] public BabelIndexDTO* IndexTable;
            public int EntryCount;
            [ReadOnly, NoAlias] public NativeArray<uint> TextHashes;
            [WriteOnly, NoAlias] public NativeArray<BabelLookupResultDTO> Output;

            public void Execute(int index)
            {
                uint target = TextHashes[index];
                int low = 0;
                int high = EntryCount - 1;
                while (low <= high)
                {
                    int mid = (int)(((uint)low + (uint)high) >> 1);
                    BabelIndexDTO entry = IndexTable[mid];
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

                    if (entry.StringHash < target)
                        low = mid + 1;
                    else
                        high = mid - 1;
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
        public unsafe struct BabelLoreXorDecryptPointerJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // SourceBytes points at a read-only MMF or Vault-padded Babel byte blob. Unity safety handles
            // cannot represent this pointer, but SourceByteLength and SourceOffset/ByteLength bound every
            // byte read before XOR output is written.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Copying lore source bytes into another NativeArray before decrypting was rejected because it
            // doubles bandwidth and defeats the MMF path. Decrypting to string/byte[] was rejected because
            // it allocates and crosses UI ownership.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // The caller owns OutputBytes and passes a created DecryptionMask. Each Execute(index) writes
            // only OutputBytes[index], so parallel iterations do not share output slots; the source pointer
            // is read-only for the whole scheduled range.
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* SourceBytes;
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
