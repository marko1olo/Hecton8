using System;
using System.IO;
#if UNITY_EDITOR || UNITY_STANDALONE || HECTON8_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Persistence.Paging
{
    public sealed class H8BinaryWorldPager : IDisposable, IGlobalRegistryHotSwapListener
    {
        private const string NativeMemoryOwner = nameof(H8BinaryWorldPager);
        private const string DumpFileName = "Dump_1312_VoxelPaging.bin";
        private const string CrashDumpFileName = "Dump_CRASH.bin";
        private const string DumpH8FileName = "Dump_SAVE_SURGEON.h8dump";
        private const string CrashDumpH8FileName = "Dump_CRASH.h8dump";
        private const string WorldDataFileName = "world_data.h8bin";
        private const string WalFileName = "h8_delta.wal";
        private const uint PageMagic = 0x48385047u; // H8PG
        private const uint WalMagic = 0x4C573848u; // H8WL
        private const uint DirectoryMagic = 0x44573848u; // H8WD
        private const ushort PageVersion = 1;
        private const ushort WalVersion = 1;
        private const int SectorHeaderBytes = 64;
        private const int WalHeaderBytes = 64;
        private const int WalTailBytes = 4;
        private const int WorldDirectoryBytes = 4096;
        private const int WorldDirectoryHeaderBytes = 64;
        private const int DirectoryEntryBytes = 16;
        private const int DirectorySlotCount = (WorldDirectoryBytes - WorldDirectoryHeaderBytes) / DirectoryEntryBytes;
        private const int HotStateMaxBytes = 512;
        private const int SectorSizeBytes = 256 * 1024;
        private const int SectorPayloadBytes = SectorSizeBytes - SectorHeaderBytes;
        private const int MaxSectors = 8192;
        private const int WriteSlotCount = 32;
        private const int ReadSlotCount = 16;
        private const int MaxSectorsMask = MaxSectors - 1;
        private const int WriteSlotMask = WriteSlotCount - 1;
        private const int ReadSlotMask = ReadSlotCount - 1;
        private const SystemID VaultOwner = SystemID.SavePersistence;
        private const int TelemetryCapacity = 300;
        private const int QueueCapacity = 64;
        private const int ReadQueueMask = QueueCapacity - 1;
        private const int WorkerIdleSleepMilliseconds = 1;
        private const int WorkerShutdownWaitMilliseconds = 250;
        private const long WalCommitThresholdBytes = 4L * 1024L * 1024L;
        private const long WalMicroStallThresholdBytes = 16L * 1024L * 1024L;
        private const uint PageFlagCompressed = 1u;
        private const uint PageFlagProceduralFallback = 1u << 1;
        private const uint PageFlagPayloadHashXxHash3 = 1u << 2;
        private const uint PagerTelemetryFlagWalAppend = 1u << 8;
        private const uint PagerTelemetryFlagWalReplay = 1u << 9;
        private const uint PagerTelemetryFlagMmfCommit = 1u << 10;
        private const uint PagerTelemetryFlagFileStreamCommit = 1u << 11;
        private const uint PagerTelemetryFlagDirectoryCollision = 1u << 12;
        private const uint PagerTelemetryFlagPayloadOverflowRejected = 1u << 13;

        private VaultGenerationHandle<PageWriteCommand> _writeCommandsHandle;
        private VaultGenerationHandle<PageReadCommand> _readCommandsHandle;
        private VaultGenerationHandle<PageReadResult> _readResultsHandle;
        private VaultGenerationHandle<byte> _writeArenaHandle;
        private VaultGenerationHandle<byte> _readArenaHandle;
        private VaultGenerationHandle<byte> _readSlotStatesHandle;
        private VaultGenerationHandle<byte> _readStagingHandle;
        private VaultGenerationHandle<byte> _compressionScratchHandle;
        private VaultGenerationHandle<byte> _hotStateArenaHandle;
        private VaultGenerationHandle<H8BinaryWorldPagerTelemetryEntry> _telemetryRingHandle;
        private IDataVault _vault;
        private SpinLock _writeQueueLock;
        private SpinLock _readQueueLock;
        private SpinLock _resultLock;
        private readonly object _streamLock = new object();
        private readonly object _walLock = new object();
        private readonly object _hotStateLock = new object();
        private readonly object _workerStopLock = new object();
        private FileStream _stream;
        private FileStream _walStream;
        private string _path;
        private string _walPath;
        private string _dumpPath;
        private string _crashDumpPath;
        private string _dumpH8Path;
        private string _crashDumpH8Path;
        private int _writeSlotCursor;
        private int _readSlotCursor;
        private int _disposeRequested;
        private int _workerRunning;
        private int _workerThreadId;
        private int _dumpRequestPending;
        private Thread _workerThread;
        private int _initialized;
        private bool _registeredHotSwap;
        private CacheLineInt _telemetryCursor;
        private CacheLineInt _pendingWriteCount;
        private CacheLineInt _pendingReadCount;
        private CacheLineInt _pendingReadResultCount;
        private int _writeQueueHead;
        private int _writeQueueTail;
        private CacheLineInt _writeQueueCount;
        private int _readQueueHead;
        private int _readQueueTail;
        private CacheLineInt _readQueueCount;
        private CacheLineInt _pageFaultCount;
        private CacheLineInt _corruptReadCount;
        private CacheLineInt _completedReadCount;
        private CacheLineInt _completedWriteCount;
        private CacheLineInt _droppedWriteCount;
        private CacheLineInt _droppedReadCount;
        private CacheLineInt _ioErrorCount;
        private CacheLineInt _initializationFault;
        private CacheLineInt _walReplayCount;
        private CacheLineInt _walCorruptCount;
        private CacheLineInt _walAppendFailureCount;
        private CacheLineInt _walMicroStallCount;
        private CacheLineInt _workerFlushRequestCount;
        private int _hotStateBytes;
        private uint _hotStateSchemaHash;
        private uint _hotStateFrame;
        private uint _hotStateCrc32;
        private CacheLineInt _queueHighWatermark;
        private int _lastPayloadBytes;
        private long _lastSectorHash;
        private uint _lastPayloadType;
        private uint _lastFrame;

        public string FileName => WorldDataFileName;

        public bool IsInitialized => Volatile.Read(ref _initialized) != 0;

        public bool HasInitializationFault => Volatile.Read(ref _initializationFault.Value) != 0;

        public void Initialize(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                absolutePath = HectonPersistentPathPolicy.CombineFile(WorldDataFileName);

            if (IsInitialized && string.Equals(_path, absolutePath, StringComparison.Ordinal))
                return;

            Dispose();

            _path = absolutePath;
            _walPath = ResolveWalPath(_path);
            _dumpPath = ResolveDumpPath();
            _crashDumpPath = ResolveCrashDumpPath();
            _dumpH8Path = ResolveAgentLogPath(DumpH8FileName);
            _crashDumpH8Path = ResolveAgentLogPath(CrashDumpH8FileName);
            Volatile.Write(ref _initializationFault.Value, 0);

            try
            {
                HectonPersistentPathPolicy.EnsureParentDirectory(_path);
                HectonPersistentPathPolicy.EnsureParentDirectory(_walPath);
                HectonPersistentPathPolicy.EnsureParentDirectory(_dumpPath);

                // COLD ALLOC: FileStream[1] - persistent random-access async world pager handle - owner: H8BinaryWorldPager
                _stream = new FileStream(
                    _path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    SectorSizeBytes,
                    FileOptions.Asynchronous | FileOptions.RandomAccess);

                // COLD ALLOC: FileStream[1] - write-ahead log append handle, flushed before world_data mutation - owner: H8BinaryWorldPager
                _walStream = new FileStream(
                    _walPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough | FileOptions.SequentialScan);
            }
            catch (IOException)
            {
                MarkInitializationFault();
                return;
            }
            catch (UnauthorizedAccessException)
            {
                MarkInitializationFault();
                return;
            }

            AllocateNativeState();
            if (HasInitializationFault)
                return;

            EnsureDirectoryPage();
            ReplayWalIfPresent();
            Volatile.Write(ref _disposeRequested, 0);
            Volatile.Write(ref _initialized, 1);
            StartWorker();
        }

        public bool TryEnqueueWrite(
            long sectorHash,
            uint payloadType,
            NativeArray<byte> payload,
            int byteCount,
            uint sourceHash,
            uint frame)
        {
            bool payloadOverflow = payload.IsCreated && byteCount > SectorPayloadBytes;
            if (!IsInitialized || !payload.IsCreated || byteCount <= 0 || byteCount > payload.Length || payloadOverflow)
            {
                Interlocked.Increment(ref _droppedWriteCount.Value);
                if (payloadOverflow)
                {
                    int directorySlot = ResolveDirectorySlot(sectorHash);
                    RecordTelemetry(
                        sectorHash,
                        ResolveOffset(sectorHash),
                        payloadType,
                        frame,
                        0u,
                        byteCount,
                        PagerTelemetryOperation.WriteRejected,
                        H8WorldPageStatus.IOError,
                        PagerTelemetryFlagPayloadOverflowRejected,
                        directorySlot,
                        unchecked((uint)byteCount));
                    DumpBlackBox();
                }

                return false;
            }

            ResolveWriteArena(out NativeArray<byte> writeArena);
            ResolveWriteCommands(out NativeArray<PageWriteCommand> writeCommands);
            if (!writeArena.IsCreated ||
                !writeCommands.IsCreated ||
                writeArena.Length < WriteSlotCount * SectorPayloadBytes ||
                writeCommands.Length < WriteSlotCount)
            {
                Interlocked.Increment(ref _droppedWriteCount.Value);
                return false;
            }

            bool lockTaken = false;
            try
            {
                _writeQueueLock.Enter(ref lockTaken);
                int pending = Volatile.Read(ref _pendingWriteCount.Value);
                if (pending >= WriteSlotCount || _writeQueueCount.Value >= WriteSlotCount)
                {
                    Interlocked.Increment(ref _droppedWriteCount.Value);
                    return false;
                }

                int slot = _writeSlotCursor;
                _writeSlotCursor = (_writeSlotCursor + 1) & WriteSlotMask;
                int byteOffset = slot * SectorPayloadBytes;
                unsafe
                {
                    void* src = payload.GetUnsafeReadOnlyPtr();
                    void* dst = (byte*)writeArena.GetUnsafePtr() + byteOffset;
                    UnsafeUtility.MemCpy(dst, src, byteCount);
                }
                writeCommands[_writeQueueTail] = new PageWriteCommand
                {
                    SectorHash = sectorHash,
                    PayloadType = payloadType,
                    ByteOffset = byteOffset,
                    ByteCount = byteCount,
                    SourceHash = sourceHash,
                    Frame = frame
                };
                _writeQueueTail = (_writeQueueTail + 1) & WriteSlotMask;
                _writeQueueCount.Value++;

                int queued = Interlocked.Increment(ref _pendingWriteCount.Value);
                SetQueueHighWatermark(queued);
                return true;
            }
            finally
            {
                if (lockTaken)
                    _writeQueueLock.Exit(false);
            }
        }

        public bool TryStageHotState(
            NativeArray<byte> payload,
            int byteCount,
            uint schemaHash,
            uint frame)
        {
            if (!payload.IsCreated ||
                byteCount < 0 ||
                byteCount > payload.Length ||
                byteCount > HotStateMaxBytes)
            {
                return false;
            }

            ResolveHotStateArena(out NativeArray<byte> hotStateArena);
            if (!hotStateArena.IsCreated || hotStateArena.Length < HotStateMaxBytes)
                return false;

            unsafe
            {
                lock (_hotStateLock)
                {
                    byte* destination = (byte*)hotStateArena.GetUnsafePtr();
                    if (byteCount > 0)
                    {
                        byte* source = (byte*)payload.GetUnsafeReadOnlyPtr();
                        UnsafeUtility.MemCpy(destination, source, byteCount);
                        if (byteCount < HotStateMaxBytes)
                            UnsafeUtility.MemClear(destination + byteCount, HotStateMaxBytes - byteCount);
                    }
                    else
                    {
                        UnsafeUtility.MemClear(destination, HotStateMaxBytes);
                    }

                    _hotStateBytes = byteCount;
                    _hotStateSchemaHash = schemaHash;
                    _hotStateFrame = frame;
                    _hotStateCrc32 = byteCount > 0 ? ComputeCrc32(destination, byteCount) : 0u;
                    return true;
                }
            }
        }

        public bool TryRequestRead(
            long sectorHash,
            uint payloadType,
            uint requestId,
            uint frame,
            out H8WorldPageReadTicket ticket)
        {
            ticket = new H8WorldPageReadTicket
            {
                SectorHash = sectorHash,
                PayloadType = payloadType,
                RequestId = requestId,
                Frame = frame,
                Status = H8WorldPageStatus.Rejected
            };

            if (!IsInitialized || requestId == 0u)
            {
                Interlocked.Increment(ref _droppedReadCount.Value);
                return false;
            }

            ResolveReadCommands(out NativeArray<PageReadCommand> readCommands);
            if (!readCommands.IsCreated || readCommands.Length < QueueCapacity)
            {
                Interlocked.Increment(ref _droppedReadCount.Value);
                return false;
            }

            bool lockTaken = false;
            try
            {
                _readQueueLock.Enter(ref lockTaken);
                if (Volatile.Read(ref _pendingReadCount.Value) >= QueueCapacity || _readQueueCount.Value >= QueueCapacity)
                {
                    Interlocked.Increment(ref _droppedReadCount.Value);
                    return false;
                }

                readCommands[_readQueueTail] = new PageReadCommand
                {
                    SectorHash = sectorHash,
                    PayloadType = payloadType,
                    RequestId = requestId,
                    Frame = frame
                };
                _readQueueTail = (_readQueueTail + 1) & ReadQueueMask;
                _readQueueCount.Value++;

                int queued = Interlocked.Increment(ref _pendingReadCount.Value);
                SetQueueHighWatermark(queued + Volatile.Read(ref _pendingWriteCount.Value));
                ticket.Status = H8WorldPageStatus.Queued;
                return true;
            }
            finally
            {
                if (lockTaken)
                    _readQueueLock.Exit(false);
            }
        }

        public bool TryCopyCompletedPage(
            in H8WorldPageReadTicket ticket,
            NativeArray<byte> destination,
            out int bytesWritten,
            out H8WorldPageStatus status)
        {
            bytesWritten = 0;
            status = H8WorldPageStatus.Queued;
            ResolveReadArena(out NativeArray<byte> readArena);
            ResolveReadSlotStates(out NativeArray<byte> readSlotStates);
            ResolveReadResults(out NativeArray<PageReadResult> readResults);
            if (!destination.IsCreated || ticket.RequestId == 0u || !readResults.IsCreated || !readArena.IsCreated || !readSlotStates.IsCreated)
            {
                status = H8WorldPageStatus.Rejected;
                return false;
            }

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                if (!TryFindReadResultIndex(ticket.RequestId, readResults, out int resultIndex))
                    return false;

                PageReadResult result = readResults[resultIndex];
                status = result.Status;
                if (result.Status != H8WorldPageStatus.Ready)
                {
                    readResults[resultIndex] = default;
                    Interlocked.Decrement(ref _pendingReadResultCount.Value);
                    return true;
                }

                if (result.ByteCount <= 0 || result.ByteCount > destination.Length || (uint)result.SlotIndex >= (uint)ReadSlotCount)
                {
                    status = H8WorldPageStatus.Rejected;
                    bytesWritten = result.ByteCount > 0 ? result.ByteCount : 0;
                    if ((uint)result.SlotIndex < (uint)ReadSlotCount)
                        readSlotStates[result.SlotIndex] = 0;

                    readResults[resultIndex] = default;
                    Interlocked.Decrement(ref _pendingReadResultCount.Value);
                    return false;
                }

                unsafe
                {
                    void* src = (byte*)readArena.GetUnsafeReadOnlyPtr() + (result.SlotIndex * SectorPayloadBytes);
                    void* dst = destination.GetUnsafePtr();
                    UnsafeUtility.MemCpy(dst, src, result.ByteCount);
                }
                bytesWritten = result.ByteCount;
                readSlotStates[result.SlotIndex] = 0;
                readResults[resultIndex] = default;
                Interlocked.Decrement(ref _pendingReadResultCount.Value);
                return true;
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        public bool TryRetireCompletedPage(
            in H8WorldPageReadTicket ticket,
            out H8WorldPageStatus status,
            out int byteCount)
        {
            status = H8WorldPageStatus.Queued;
            byteCount = 0;
            ResolveReadSlotStates(out NativeArray<byte> readSlotStates);
            ResolveReadResults(out NativeArray<PageReadResult> readResults);
            if (ticket.RequestId == 0u || !readResults.IsCreated || !readSlotStates.IsCreated)
            {
                status = H8WorldPageStatus.Rejected;
                return false;
            }

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                if (!TryFindReadResultIndex(ticket.RequestId, readResults, out int resultIndex))
                    return false;

                PageReadResult result = readResults[resultIndex];
                status = result.Status;
                byteCount = result.ByteCount;
                if ((uint)result.SlotIndex < (uint)ReadSlotCount)
                    readSlotStates[result.SlotIndex] = 0;

                readResults[resultIndex] = default;
                Interlocked.Decrement(ref _pendingReadResultCount.Value);
                return true;
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        public unsafe bool TryReadPageIntoVaultSlice(
            long sectorHash,
            uint payloadType,
            uint frame,
            out VaultSliceHandle<byte> slice,
            out int bytesWritten,
            out H8WorldPageStatus status)
        {
            slice = default;
            bytesWritten = 0;
            status = H8WorldPageStatus.Rejected;

            IDataVault vault = _vault;
            if (!IsInitialized ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                vault.IsAllocationLocked ||
                _stream == null)
            {
                return false;
            }

            if (!TryResolveDirectReadStaging(vault, out VaultSliceHandle<byte> stagingSlice, out NativeArray<byte> sliceBytes))
            {
                return false;
            }

            long offset = ResolveOffset(sectorHash);
            Span<byte> header = stackalloc byte[SectorHeaderBytes];
            try
            {
                FileStream stream = _stream;
                lock (_streamLock)
                {
                    if (stream.Length < offset + SectorHeaderBytes)
                    {
                        status = H8WorldPageStatus.Missing;
                        RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, 0, PagerTelemetryOperation.ReadMiss, status, PageFlagProceduralFallback);
                        return true;
                    }

                    stream.Position = offset;
                    if (!ReadExact(stream, header))
                    {
                        status = H8WorldPageStatus.Missing;
                        RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, 0, PagerTelemetryOperation.ReadMiss, status, PageFlagProceduralFallback);
                        return true;
                    }

                    fixed (byte* headerPtr = header)
                    {
                        if (!TryReadHeader(headerPtr, sectorHash, payloadType, out int rawBytes, out int storedBytes, out uint flags, out uint expectedPayloadCheck))
                        {
                            status = HeaderIsEmpty(headerPtr) || HeaderIsDifferentPage(headerPtr, sectorHash, payloadType)
                                ? H8WorldPageStatus.Missing
                                : H8WorldPageStatus.Corrupt;
                            RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, 0, status == H8WorldPageStatus.Corrupt ? PagerTelemetryOperation.ReadCorrupt : PagerTelemetryOperation.ReadMiss, status, PageFlagProceduralFallback);
                            return true;
                        }

                        if (rawBytes <= 0 || rawBytes > SectorPayloadBytes || storedBytes <= 0 || storedBytes > SectorPayloadBytes)
                        {
                            status = H8WorldPageStatus.Corrupt;
                            RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return true;
                        }

                        byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sliceBytes);
                        if ((flags & PageFlagCompressed) != 0u)
                        {
                            byte* storedPtr = rawPtr + SectorPayloadBytes;
                            if (!ReadExact(stream, new Span<byte>(storedPtr, storedBytes)) ||
                                !TryDecompressRle(storedPtr, storedBytes, rawPtr, rawBytes))
                            {
                                status = H8WorldPageStatus.Corrupt;
                                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                                DumpBlackBox();
                                return true;
                            }
                        }
                        else if (!ReadExact(stream, new Span<byte>(rawPtr, rawBytes)))
                        {
                            status = H8WorldPageStatus.Corrupt;
                            RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return true;
                        }

                        uint actualPayloadCheck = ComputePayloadCheck32(rawPtr, rawBytes, flags);
                        if (actualPayloadCheck != expectedPayloadCheck)
                        {
                            status = H8WorldPageStatus.Corrupt;
                            RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, rawBytes, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return true;
                        }

                        bytesWritten = rawBytes;
                        status = H8WorldPageStatus.Ready;
                        slice = stagingSlice;
                        RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, rawBytes, PagerTelemetryOperation.ReadReady, status, flags);
                        return true;
                    }
                }
            }
            catch (IOException)
            {
                status = H8WorldPageStatus.IOError;
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, bytesWritten, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                status = H8WorldPageStatus.IOError;
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, bytesWritten, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                return true;
            }
            catch (ObjectDisposedException)
            {
                status = H8WorldPageStatus.IOError;
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, bytesWritten, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                return true;
            }
            catch (NotSupportedException)
            {
                status = H8WorldPageStatus.IOError;
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, bytesWritten, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                return true;
            }
            catch (ArgumentException)
            {
                status = H8WorldPageStatus.IOError;
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, bytesWritten, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                return true;
            }
            catch (InvalidOperationException)
            {
                status = H8WorldPageStatus.IOError;
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, bytesWritten, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                return true;
            }
        }

        public H8WorldPagerTelemetrySnapshot GetTelemetry()
        {
            return new H8WorldPagerTelemetrySnapshot
            {
                PendingDiskWrites = Volatile.Read(ref _pendingWriteCount.Value),
                PendingDiskReads = Volatile.Read(ref _pendingReadCount.Value),
                PendingReadResults = Volatile.Read(ref _pendingReadResultCount.Value),
                PageFaults = Volatile.Read(ref _pageFaultCount.Value),
                CorruptReads = Volatile.Read(ref _corruptReadCount.Value),
                CompletedReads = Volatile.Read(ref _completedReadCount.Value),
                CompletedWrites = Volatile.Read(ref _completedWriteCount.Value),
                DroppedWrites = Volatile.Read(ref _droppedWriteCount.Value),
                DroppedReads = Volatile.Read(ref _droppedReadCount.Value),
                IoErrors = Volatile.Read(ref _ioErrorCount.Value),
                QueueHighWatermark = Volatile.Read(ref _queueHighWatermark.Value),
                LastPayloadBytes = Volatile.Read(ref _lastPayloadBytes),
                LastSectorHash = Volatile.Read(ref _lastSectorHash),
                LastPayloadType = Volatile.Read(ref _lastPayloadType),
                LastFrame = Volatile.Read(ref _lastFrame)
            };
        }

        public void Flush()
        {
            if (!IsInitialized)
                return;

            Interlocked.Exchange(ref _workerFlushRequestCount.Value, 1);
        }

        public void Dispose()
        {
            Volatile.Write(ref _disposeRequested, 1);
            bool workerStopped = WaitForWorkerExit();

            FileStream stream = _stream;
            _stream = null;
            if (stream != null)
                DisposeStream(stream, flush: true);

            FileStream walStream = _walStream;
            _walStream = null;
            if (walStream != null)
                DisposeWalStream(walStream, flush: true);

            if (!workerStopped)
                workerStopped = WaitForWorkerExit();

            if (!workerStopped)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                Volatile.Write(ref _initialized, 0);
                Volatile.Write(ref _initializationFault.Value, 1);
                return;
            }

            DisposeNativeState();
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _initializationFault.Value, 0);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            Volatile.Write(ref _disposeRequested, 1);
            bool workerStopped = WaitForWorkerExit();
            if (!workerStopped)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                Volatile.Write(ref _initialized, 0);
                Volatile.Write(ref _initializationFault.Value, 1);
                return;
            }

            DumpBlackBox();
            ClearPagerTransientBuffers();
            ReleasePagerVaultHandles(previousService as IDataVault ?? _vault);
            _vault = null;
            ResetPagerTransientState();
            _hotStateBytes = 0;
            _hotStateSchemaHash = 0u;
            _hotStateFrame = 0u;
            _hotStateCrc32 = 0u;
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _initializationFault.Value, 1);
        }

        private void MarkInitializationFault()
        {
            FileStream stream = _stream;
            _stream = null;
            if (stream != null)
                DisposeStream(stream, flush: false);

            FileStream walStream = _walStream;
            _walStream = null;
            if (walStream != null)
                DisposeWalStream(walStream, flush: false);

            DisposeNativeState();
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _disposeRequested, 1);
            Volatile.Write(ref _initializationFault.Value, 1);
            Interlocked.Increment(ref _ioErrorCount.Value);

            Hecton8.Core.H8Debug.LogWarning("H8BinaryWorldPager disabled page IO after initialization fault.");
        }

        private bool WaitForWorkerExit()
        {
            if (Volatile.Read(ref _workerRunning) == 0)
                return true;

            Thread workerThread = _workerThread;
            if (workerThread != null)
            {
                try
                {
                    if (workerThread.Join(WorkerShutdownWaitMilliseconds))
                        return true;
                }
                catch (ThreadStateException)
                {
                }
            }

            lock (_workerStopLock)
            {
                if (Volatile.Read(ref _workerRunning) == 0)
                    return true;

                Monitor.Wait(_workerStopLock, WorkerShutdownWaitMilliseconds);
                return Volatile.Read(ref _workerRunning) == 0;
            }
        }

        private void DisposeStream(FileStream stream, bool flush)
        {
            try
            {
                lock (_streamLock)
                {
                    if (flush)
                        stream.Flush(true);
                    stream.Dispose();
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        private void DisposeWalStream(FileStream stream, bool flush)
        {
            try
            {
                lock (_walLock)
                {
                    if (flush)
                        stream.Flush(true);
                    stream.Dispose();
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        private void AllocateNativeState()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
            {
                MarkInitializationFault();
                return;
            }

            _vault = vault;
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            _writeCommandsHandle = vault.EnsureGenerationHandle<PageWriteCommand>(
                BufferID.SaveWorldPagerWriteCommands,
                WriteSlotCount,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _readCommandsHandle = vault.EnsureGenerationHandle<PageReadCommand>(
                BufferID.SaveWorldPagerReadCommands,
                QueueCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _readResultsHandle = vault.EnsureGenerationHandle<PageReadResult>(
                BufferID.SaveWorldPagerReadResults,
                ReadSlotCount,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _writeArenaHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveWorldPagerWriteArena,
                WriteSlotCount * SectorPayloadBytes,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _readArenaHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveWorldPagerReadArena,
                ReadSlotCount * SectorPayloadBytes,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _readSlotStatesHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveWorldPagerReadSlotStates,
                ReadSlotCount,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _readStagingHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveWorldPagerReadStaging,
                SectorPayloadBytes * 2,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _compressionScratchHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveWorldPagerCompressionScratch,
                SectorPayloadBytes,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _hotStateArenaHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveWorldPagerHotState,
                HotStateMaxBytes,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<H8BinaryWorldPagerTelemetryEntry>(
                BufferID.SaveWorldPagerTelemetryRing,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);

            if (!ArePagerVaultHandlesReady())
            {
                ReleasePagerVaultHandles(vault);
                MarkInitializationFault();
                return;
            }

            ResetPagerTransientState();
            ClearPagerTransientBuffers();
        }

        private void DisposeNativeState()
        {
            ClearPagerTransientBuffers();

            ReleasePagerVaultHandles(_vault);
            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            _vault = null;
            ResetPagerTransientState();
            _hotStateBytes = 0;
            _hotStateSchemaHash = 0u;
            _hotStateFrame = 0u;
            _hotStateCrc32 = 0u;
        }

        private void ResetPagerTransientState()
        {
            _pendingWriteCount.Value = 0;
            _pendingReadCount.Value = 0;
            _pendingReadResultCount.Value = 0;
            _writeSlotCursor = 0;
            _readSlotCursor = 0;
            _writeQueueHead = 0;
            _writeQueueTail = 0;
            _writeQueueCount.Value = 0;
            _readQueueHead = 0;
            _readQueueTail = 0;
            _readQueueCount.Value = 0;
            _workerFlushRequestCount.Value = 0;
        }

        private void ClearPagerTransientBuffers()
        {
            ResolveWriteCommands(out NativeArray<PageWriteCommand> writeCommands);
            ResolveReadCommands(out NativeArray<PageReadCommand> readCommands);
            ResolveReadResults(out NativeArray<PageReadResult> readResults);
            ResolveReadSlotStates(out NativeArray<byte> readSlotStates);

            ClearNativeArray(writeCommands);
            ClearNativeArray(readCommands);
            ClearNativeArray(readResults);
            ClearNativeArray(readSlotStates);
        }

        private static unsafe void ClearNativeArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            UnsafeUtility.MemClear(array.GetUnsafePtr(), (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        private bool ArePagerVaultHandlesReady()
        {
            return
                HasPagerVaultBuffer(in _writeCommandsHandle, BufferID.SaveWorldPagerWriteCommands, WriteSlotCount) &&
                HasPagerVaultBuffer(in _readCommandsHandle, BufferID.SaveWorldPagerReadCommands, QueueCapacity) &&
                HasPagerVaultBuffer(in _readResultsHandle, BufferID.SaveWorldPagerReadResults, ReadSlotCount) &&
                HasPagerVaultBuffer(in _writeArenaHandle, BufferID.SaveWorldPagerWriteArena, WriteSlotCount * SectorPayloadBytes) &&
                HasPagerVaultBuffer(in _readArenaHandle, BufferID.SaveWorldPagerReadArena, ReadSlotCount * SectorPayloadBytes) &&
                HasPagerVaultBuffer(in _readSlotStatesHandle, BufferID.SaveWorldPagerReadSlotStates, ReadSlotCount) &&
                HasPagerVaultBuffer(in _readStagingHandle, BufferID.SaveWorldPagerReadStaging, SectorPayloadBytes * 2) &&
                HasPagerVaultBuffer(in _compressionScratchHandle, BufferID.SaveWorldPagerCompressionScratch, SectorPayloadBytes) &&
                HasPagerVaultBuffer(in _hotStateArenaHandle, BufferID.SaveWorldPagerHotState, HotStateMaxBytes) &&
                HasPagerVaultBuffer(in _telemetryRingHandle, BufferID.SaveWorldPagerTelemetryRing, TelemetryCapacity);
        }

        private bool HasPagerVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return TryReadPagerVaultBuffer(in handle, bufferId, requiredLength, out _);
        }

        private bool TryResolvePagerVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> array) where T : struct
        {
            IDataVault vault = _vault;
            array = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsPagerVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out array) &&
                   array.IsCreated &&
                   array.Length >= requiredLength;
        }

        private bool TryReadPagerVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly array) where T : struct
        {
            IDataVault vault = _vault;
            array = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsPagerVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out array) &&
                   array.IsCreated &&
                   array.Length >= requiredLength;
        }

        private static bool IsPagerVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwner &&
                   handle.Generation != 0u;
        }

        private void ReleasePagerVaultHandles(IDataVault vault)
        {
            ReleasePagerVaultHandle(vault, ref _writeCommandsHandle, BufferID.SaveWorldPagerWriteCommands);
            ReleasePagerVaultHandle(vault, ref _readCommandsHandle, BufferID.SaveWorldPagerReadCommands);
            ReleasePagerVaultHandle(vault, ref _readResultsHandle, BufferID.SaveWorldPagerReadResults);
            ReleasePagerVaultHandle(vault, ref _writeArenaHandle, BufferID.SaveWorldPagerWriteArena);
            ReleasePagerVaultHandle(vault, ref _readArenaHandle, BufferID.SaveWorldPagerReadArena);
            ReleasePagerVaultHandle(vault, ref _readSlotStatesHandle, BufferID.SaveWorldPagerReadSlotStates);
            ReleasePagerVaultHandle(vault, ref _readStagingHandle, BufferID.SaveWorldPagerReadStaging);
            ReleasePagerVaultHandle(vault, ref _compressionScratchHandle, BufferID.SaveWorldPagerCompressionScratch);
            ReleasePagerVaultHandle(vault, ref _hotStateArenaHandle, BufferID.SaveWorldPagerHotState);
            ReleasePagerVaultHandle(vault, ref _telemetryRingHandle, BufferID.SaveWorldPagerTelemetryRing);
        }

        private static void ReleasePagerVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsPagerVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ResolveWriteCommands(out NativeArray<PageWriteCommand> array)
        {
            TryResolvePagerVaultBuffer(in _writeCommandsHandle, BufferID.SaveWorldPagerWriteCommands, WriteSlotCount, out array);
        }

        private void ResolveReadCommands(out NativeArray<PageReadCommand> array)
        {
            TryResolvePagerVaultBuffer(in _readCommandsHandle, BufferID.SaveWorldPagerReadCommands, QueueCapacity, out array);
        }

        private void ResolveReadResults(out NativeArray<PageReadResult> array)
        {
            TryResolvePagerVaultBuffer(in _readResultsHandle, BufferID.SaveWorldPagerReadResults, ReadSlotCount, out array);
        }

        private void ResolveWriteArena(out NativeArray<byte> array)
        {
            TryResolvePagerVaultBuffer(in _writeArenaHandle, BufferID.SaveWorldPagerWriteArena, WriteSlotCount * SectorPayloadBytes, out array);
        }

        private void ResolveReadArena(out NativeArray<byte> array)
        {
            TryResolvePagerVaultBuffer(in _readArenaHandle, BufferID.SaveWorldPagerReadArena, ReadSlotCount * SectorPayloadBytes, out array);
        }

        private bool TryResolveDirectReadStaging(
            IDataVault vault,
            out VaultSliceHandle<byte> slice,
            out NativeArray<byte> sliceBytes)
        {
            slice = default;
            sliceBytes = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsPagerVaultHandle(in _readStagingHandle, BufferID.SaveWorldPagerReadStaging) ||
                !vault.TryResolveHandle(in _readStagingHandle, out NativeArray<byte> staging) ||
                !staging.IsCreated ||
                staging.Length < SectorPayloadBytes * 2)
            {
                return false;
            }

            slice.BufferID = _readStagingHandle.BufferID;
            slice.SystemID = _readStagingHandle.SystemID;
            slice.Generation = _readStagingHandle.Generation;
            slice.HandleFlags = _readStagingHandle.Flags;
            slice.StartIndex = 0;
            slice.Length = SectorPayloadBytes * 2;
            slice.Flags = 0u;
            slice.Reserved0 = 0u;
            sliceBytes = staging;
            return true;
        }

        private void ResolveReadSlotStates(out NativeArray<byte> array)
        {
            TryResolvePagerVaultBuffer(in _readSlotStatesHandle, BufferID.SaveWorldPagerReadSlotStates, ReadSlotCount, out array);
        }

        private void ResolveCompressionScratch(out NativeArray<byte> array)
        {
            TryResolvePagerVaultBuffer(in _compressionScratchHandle, BufferID.SaveWorldPagerCompressionScratch, SectorPayloadBytes, out array);
        }

        private void ResolveHotStateArena(out NativeArray<byte> array)
        {
            TryResolvePagerVaultBuffer(in _hotStateArenaHandle, BufferID.SaveWorldPagerHotState, HotStateMaxBytes, out array);
        }

        private void ResolveTelemetryRing(out NativeArray<H8BinaryWorldPagerTelemetryEntry> array)
        {
            TryResolvePagerVaultBuffer(in _telemetryRingHandle, BufferID.SaveWorldPagerTelemetryRing, TelemetryCapacity, out array);
        }

        private void StartWorker()
        {
            if (Interlocked.Exchange(ref _workerRunning, 1) != 0)
                return;

            try
            {
                Thread workerThread = new Thread(RunWorkerLoop)
                {
                    IsBackground = true,
                    Name = "H8 Binary World Pager"
                };

                _workerThread = workerThread;
                workerThread.Start();
            }
            catch (ThreadStateException)
            {
                _workerThread = null;
                Volatile.Write(ref _workerRunning, 0);
                MarkInitializationFault();
            }
        }

        private void RunWorkerLoop()
        {
            try
            {
                Volatile.Write(ref _workerThreadId, Thread.CurrentThread.ManagedThreadId);
                while (Volatile.Read(ref _disposeRequested) == 0)
                {
                    bool didWork = false;
                    if (TryConsumeDumpRequest())
                    {
                        didWork = true;
                        WriteBlackBoxDumps();
                    }

                    if (TryDequeueWrite(out PageWriteCommand writeCommand))
                    {
                        didWork = true;
                        if (!ProcessDequeuedWrite(in writeCommand))
                            break;
                    }

                    if (TryDequeueRead(out PageReadCommand readCommand))
                    {
                        didWork = true;
                        if (!ProcessDequeuedRead(in readCommand))
                            break;
                    }

                    if (TryConsumeWorkerFlushRequest())
                    {
                        didWork = true;
                        FlushStreamsOnWorker();
                    }

                    if (!didWork)
                        Thread.Sleep(WorkerIdleSleepMilliseconds);
                }
            }
            catch (IOException)
            {
                MarkWorkerFault();
            }
            catch (UnauthorizedAccessException)
            {
                MarkWorkerFault();
            }
            catch (ObjectDisposedException)
            {
                MarkWorkerFault();
            }
            catch (NotSupportedException)
            {
                MarkWorkerFault();
            }
            catch (ArgumentException)
            {
                MarkWorkerFault();
            }
            catch (InvalidOperationException)
            {
                MarkWorkerFault();
            }
            catch (ThreadInterruptedException)
            {
                MarkWorkerFault();
            }
            finally
            {
                _workerThread = null;
                Volatile.Write(ref _workerThreadId, 0);
                Volatile.Write(ref _workerRunning, 0);
                lock (_workerStopLock)
                {
                    Monitor.PulseAll(_workerStopLock);
                }
            }
        }

        private bool ProcessDequeuedWrite(in PageWriteCommand command)
        {
            bool faulted = false;
            try
            {
                ProcessWrite(in command);
            }
            catch (IOException)
            {
                faulted = true;
            }
            catch (UnauthorizedAccessException)
            {
                faulted = true;
            }
            catch (ObjectDisposedException)
            {
                faulted = true;
            }
            catch (NotSupportedException)
            {
                faulted = true;
            }
            catch (ArgumentException)
            {
                faulted = true;
            }
            catch (InvalidOperationException)
            {
                faulted = true;
            }
            finally
            {
                Interlocked.Decrement(ref _pendingWriteCount.Value);
            }

            if (!faulted)
                return true;

            RecordTelemetry(
                command.SectorHash,
                ResolveOffset(command.SectorHash),
                command.PayloadType,
                command.Frame,
                0u,
                command.ByteCount,
                PagerTelemetryOperation.Write,
                H8WorldPageStatus.IOError,
                PageFlagProceduralFallback);
            MarkWorkerFault();
            return false;
        }

        private bool ProcessDequeuedRead(in PageReadCommand command)
        {
            bool faulted = false;
            try
            {
                ProcessRead(in command);
            }
            catch (IOException)
            {
                faulted = true;
            }
            catch (UnauthorizedAccessException)
            {
                faulted = true;
            }
            catch (ObjectDisposedException)
            {
                faulted = true;
            }
            catch (NotSupportedException)
            {
                faulted = true;
            }
            catch (ArgumentException)
            {
                faulted = true;
            }
            catch (InvalidOperationException)
            {
                faulted = true;
            }
            finally
            {
                Interlocked.Decrement(ref _pendingReadCount.Value);
            }

            if (!faulted)
                return true;

            CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
            RecordTelemetry(
                command.SectorHash,
                ResolveOffset(command.SectorHash),
                command.PayloadType,
                command.Frame,
                command.RequestId,
                0,
                PagerTelemetryOperation.ReadCorrupt,
                H8WorldPageStatus.IOError,
                PageFlagProceduralFallback);
            MarkWorkerFault();
            return false;
        }

        private void MarkWorkerFault()
        {
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _disposeRequested, 1);
            Volatile.Write(ref _initializationFault.Value, 1);
            Volatile.Write(ref _pendingWriteCount.Value, 0);
            Volatile.Write(ref _pendingReadCount.Value, 0);
            Volatile.Write(ref _writeQueueCount.Value, 0);
            Volatile.Write(ref _readQueueCount.Value, 0);
            Volatile.Write(ref _workerFlushRequestCount.Value, 0);
            Interlocked.Increment(ref _ioErrorCount.Value);
            DumpBlackBox();
        }

        private bool TryConsumeWorkerFlushRequest()
        {
            return Interlocked.Exchange(ref _workerFlushRequestCount.Value, 0) != 0;
        }

        private void FlushStreamsOnWorker()
        {
            try
            {
                FileStream stream = _stream;
                if (stream != null)
                {
                    lock (_streamLock)
                    {
                        stream.Flush(true);
                    }
                }

                FileStream walStream = _walStream;
                if (walStream != null)
                {
                    lock (_walLock)
                    {
                        walStream.Flush(true);
                    }
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        private bool TryDequeueWrite(out PageWriteCommand command)
        {
            command = default;
            ResolveWriteCommands(out NativeArray<PageWriteCommand> writeCommands);
            if (!writeCommands.IsCreated || writeCommands.Length < WriteSlotCount)
                return false;

            bool lockTaken = false;
            try
            {
                _writeQueueLock.Enter(ref lockTaken);
                if (_writeQueueCount.Value <= 0)
                    return false;

                command = writeCommands[_writeQueueHead];
                writeCommands[_writeQueueHead] = default;
                _writeQueueHead = (_writeQueueHead + 1) & WriteSlotMask;
                _writeQueueCount.Value--;
                return true;
            }
            finally
            {
                if (lockTaken)
                    _writeQueueLock.Exit(false);
            }
        }

        private bool TryDequeueRead(out PageReadCommand command)
        {
            command = default;
            ResolveReadCommands(out NativeArray<PageReadCommand> readCommands);
            if (!readCommands.IsCreated || readCommands.Length < QueueCapacity)
                return false;

            bool lockTaken = false;
            try
            {
                _readQueueLock.Enter(ref lockTaken);
                if (_readQueueCount.Value <= 0)
                    return false;

                command = readCommands[_readQueueHead];
                readCommands[_readQueueHead] = default;
                _readQueueHead = (_readQueueHead + 1) & ReadQueueMask;
                _readQueueCount.Value--;
                return true;
            }
            finally
            {
                if (lockTaken)
                    _readQueueLock.Exit(false);
            }
        }

        private void ProcessWrite(in PageWriteCommand command)
        {
            ResolveWriteArena(out NativeArray<byte> writeArena);
            ResolveCompressionScratch(out NativeArray<byte> compressionScratch);
            if (!writeArena.IsCreated || !compressionScratch.IsCreated)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, ResolveOffset(command.SectorHash), command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, PageFlagProceduralFallback);
                return;
            }

            unsafe
            {
                byte* input = (byte*)writeArena.GetUnsafeReadOnlyPtr() + command.ByteOffset;
                byte* stored = input;
                int storedBytes = command.ByteCount;
                uint flags = PageFlagPayloadHashXxHash3;

                if (TryCompressRle(input, command.ByteCount, (byte*)compressionScratch.GetUnsafePtr(), compressionScratch.Length, out int compressedBytes))
                {
                    stored = (byte*)compressionScratch.GetUnsafeReadOnlyPtr();
                    storedBytes = compressedBytes;
                    flags |= PageFlagCompressed;
                }

                uint payloadHash32 = ComputePayloadHash32(input, command.ByteCount);
                long offset = ResolveOffset(command.SectorHash);
                Span<byte> header = stackalloc byte[SectorHeaderBytes];
                fixed (byte* headerPtr = header)
                {
                    WriteHeader(
                        headerPtr,
                        command.SectorHash,
                        command.PayloadType,
                        command.ByteCount,
                        storedBytes,
                        flags,
                        payloadHash32,
                        command.Frame,
                        command.SourceHash,
                        0u);

                    if (!AppendWalRecord(headerPtr, stored, storedBytes, command.Frame))
                    {
                        Interlocked.Increment(ref _walAppendFailureCount.Value);
                        Interlocked.Increment(ref _ioErrorCount.Value);
                        RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.WalAppendFailed, H8WorldPageStatus.IOError, flags);
                        return;
                    }
                }

                try
                {
                    FileStream stream = _stream;
                    if (stream == null)
                        return;

                    bool mappedWrite = TryWriteWorldPageMapped(stream, offset, header, stored, storedBytes);
                    if (!mappedWrite)
                    {
                        lock (_streamLock)
                        {
                            EnsureStreamLength(stream, offset + SectorHeaderBytes + storedBytes);
                            stream.Position = offset;
                            stream.Write(header);
                            stream.Write(new ReadOnlySpan<byte>(stored, storedBytes));
                            stream.Flush(true);
                        }
                    }

                    if (!WriteDirectoryEntry(
                            command.SectorHash,
                            offset,
                            out int directorySlot,
                            out bool directoryCollision,
                            out long previousSectorHash))
                    {
                        RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend, directorySlot, FoldSectorHash(previousSectorHash));
                        return;
                    }

                    ClearWalAfterCommit();
                    Interlocked.Increment(ref _completedWriteCount.Value);
                    PublishLast(command.SectorHash, command.PayloadType, command.ByteCount, command.Frame);
                    uint telemetryFlags = flags | PagerTelemetryFlagWalAppend | (mappedWrite ? PagerTelemetryFlagMmfCommit : PagerTelemetryFlagFileStreamCommit);
                    if (directoryCollision)
                        telemetryFlags |= PagerTelemetryFlagDirectoryCollision;
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.Ready, telemetryFlags, directorySlot, FoldSectorHash(previousSectorHash));
                }
                catch (IOException)
                {
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend);
                }
                catch (UnauthorizedAccessException)
                {
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend);
                }
                catch (ObjectDisposedException)
                {
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend);
                }
                catch (NotSupportedException)
                {
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend);
                }
                catch (ArgumentException)
                {
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags | PagerTelemetryFlagWalAppend);
                }
            }
        }

        private void ProcessRead(in PageReadCommand command)
        {
            ResolveReadArena(out NativeArray<byte> readArena);
            ResolveCompressionScratch(out NativeArray<byte> compressionScratch);
            if (!readArena.IsCreated || !compressionScratch.IsCreated)
            {
                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                return;
            }

            unsafe
            {
            long offset = ResolveOffset(command.SectorHash);
            Span<byte> header = stackalloc byte[SectorHeaderBytes];
            H8WorldPageStatus status = H8WorldPageStatus.Missing;
            int resultSlot = -1;
            int byteCount = 0;
            uint flags = 0u;

            try
            {
                FileStream stream = _stream;
                if (stream == null || stream.Length < offset + SectorHeaderBytes)
                {
                    CommitReadResult(command, H8WorldPageStatus.Missing, -1, 0);
                    Interlocked.Increment(ref _pageFaultCount.Value);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadMiss, H8WorldPageStatus.Missing, PageFlagProceduralFallback);
                    return;
                }

                lock (_streamLock)
                {
                    stream.Position = offset;
                    if (!ReadExact(stream, header))
                    {
                        CommitReadResult(command, H8WorldPageStatus.Missing, -1, 0);
                        Interlocked.Increment(ref _pageFaultCount.Value);
                        RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadMiss, H8WorldPageStatus.Missing, PageFlagProceduralFallback);
                        return;
                    }

                    fixed (byte* headerPtr = header)
                    {
                        if (!TryReadHeader(
                                headerPtr,
                                command.SectorHash,
                                command.PayloadType,
                                out int rawBytes,
                                out int storedBytes,
                                out flags,
                                out uint expectedPayloadCheck))
                        {
                            if (HeaderIsEmpty(headerPtr) || HeaderIsDifferentPage(headerPtr, command.SectorHash, command.PayloadType))
                            {
                                CommitReadResult(command, H8WorldPageStatus.Missing, -1, 0);
                                Interlocked.Increment(ref _pageFaultCount.Value);
                                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadMiss, H8WorldPageStatus.Missing, PageFlagProceduralFallback);
                                return;
                            }

                            status = H8WorldPageStatus.Corrupt;
                            CommitReadResult(command, status, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount.Value);
                            Interlocked.Increment(ref _corruptReadCount.Value);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        if (rawBytes <= 0 || rawBytes > SectorPayloadBytes || storedBytes <= 0 || storedBytes > SectorPayloadBytes)
                        {
                            status = H8WorldPageStatus.Corrupt;
                            CommitReadResult(command, status, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount.Value);
                            Interlocked.Increment(ref _corruptReadCount.Value);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        if (!TryAcquireReadSlot(out resultSlot))
                        {
                            CommitReadResult(command, H8WorldPageStatus.Rejected, -1, 0);
                            Interlocked.Increment(ref _droppedReadCount.Value);
                            return;
                        }

                        byte* resultPtr = (byte*)readArena.GetUnsafePtr() + (resultSlot * SectorPayloadBytes);
                        if ((flags & PageFlagCompressed) != 0u)
                        {
                            if (!ReadExact(stream, new Span<byte>(compressionScratch.GetUnsafePtr(), storedBytes)) ||
                                !TryDecompressRle((byte*)compressionScratch.GetUnsafeReadOnlyPtr(), storedBytes, resultPtr, rawBytes))
                            {
                                ReleaseReadSlot(resultSlot);
                                CommitReadResult(command, H8WorldPageStatus.Corrupt, -1, 0);
                                Interlocked.Increment(ref _pageFaultCount.Value);
                                Interlocked.Increment(ref _corruptReadCount.Value);
                                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.Corrupt, PageFlagProceduralFallback);
                                DumpBlackBox();
                                return;
                            }
                        }
                        else if (!ReadExact(stream, new Span<byte>(resultPtr, rawBytes)))
                        {
                            ReleaseReadSlot(resultSlot);
                            CommitReadResult(command, H8WorldPageStatus.Corrupt, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount.Value);
                            Interlocked.Increment(ref _corruptReadCount.Value);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.Corrupt, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        uint actualPayloadCheck = ComputePayloadCheck32(resultPtr, rawBytes, flags);
                        if (actualPayloadCheck != expectedPayloadCheck)
                        {
                            ReleaseReadSlot(resultSlot);
                            CommitReadResult(command, H8WorldPageStatus.Corrupt, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount.Value);
                            Interlocked.Increment(ref _corruptReadCount.Value);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, rawBytes, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.Corrupt, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        byteCount = rawBytes;
                    }
                }

                CommitReadResult(command, H8WorldPageStatus.Ready, resultSlot, byteCount);
                Interlocked.Increment(ref _completedReadCount.Value);
                PublishLast(command.SectorHash, command.PayloadType, byteCount, command.Frame);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadReady, H8WorldPageStatus.Ready, flags);
            }
            catch (IOException)
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            catch (UnauthorizedAccessException)
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            catch (ObjectDisposedException)
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            catch (NotSupportedException)
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            catch (ArgumentException)
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            catch (InvalidOperationException)
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount.Value);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            }
        }

        private bool TryAcquireReadSlot(out int slot)
        {
            slot = -1;
            ResolveReadSlotStates(out NativeArray<byte> readSlotStates);
            if (!readSlotStates.IsCreated || readSlotStates.Length < ReadSlotCount)
                return false;

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                if (Volatile.Read(ref _pendingReadResultCount.Value) >= ReadSlotCount)
                    return false;

                for (int i = 0; i < ReadSlotCount; i++)
                {
                    int candidate = (_readSlotCursor + i) & ReadSlotMask;
                    if (readSlotStates[candidate] != 0)
                        continue;

                    readSlotStates[candidate] = 1;
                    _readSlotCursor = (candidate + 1) & ReadSlotMask;
                    slot = candidate;
                    return true;
                }

                return false;
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        private void ReleaseReadSlot(int slot)
        {
            if ((uint)slot >= (uint)ReadSlotCount)
                return;

            ResolveReadSlotStates(out NativeArray<byte> readSlotStates);
            if (!readSlotStates.IsCreated || readSlotStates.Length < ReadSlotCount)
                return;

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                readSlotStates[slot] = 0;
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        private void CommitReadResult(in PageReadCommand command, H8WorldPageStatus status, int slot, int byteCount)
        {
            ResolveReadSlotStates(out NativeArray<byte> readSlotStates);
            ResolveReadResults(out NativeArray<PageReadResult> readResults);
            if (!readSlotStates.IsCreated ||
                !readResults.IsCreated ||
                readSlotStates.Length < ReadSlotCount ||
                readResults.Length < ReadSlotCount)
                return;

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                PageReadResult result = new PageReadResult
                {
                    SectorHash = command.SectorHash,
                    PayloadType = command.PayloadType,
                    RequestId = command.RequestId,
                    SlotIndex = slot,
                    ByteCount = byteCount,
                    Status = status
                };

                if (TryFindReadResultIndex(command.RequestId, readResults, out int existingIndex))
                {
                    PageReadResult existing = readResults[existingIndex];
                    if ((uint)existing.SlotIndex < (uint)ReadSlotCount)
                        readSlotStates[existing.SlotIndex] = 0;

                    readResults[existingIndex] = result;
                    return;
                }

                if (Volatile.Read(ref _pendingReadResultCount.Value) < ReadSlotCount &&
                    TryFindFreeReadResultIndex(readResults, out int freeIndex))
                {
                    readResults[freeIndex] = result;
                    Interlocked.Increment(ref _pendingReadResultCount.Value);
                    return;
                }

                if ((uint)slot < (uint)ReadSlotCount)
                    readSlotStates[slot] = 0;
                Interlocked.Increment(ref _droppedReadCount.Value);
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        private static bool TryFindReadResultIndex(uint requestId, NativeArray<PageReadResult> readResults, out int index)
        {
            index = -1;
            if (requestId == 0u || !readResults.IsCreated)
                return false;

            int length = math.min(readResults.Length, ReadSlotCount);
            for (int i = 0; i < length; i++)
            {
                if (readResults[i].RequestId != requestId)
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        private static bool TryFindFreeReadResultIndex(NativeArray<PageReadResult> readResults, out int index)
        {
            index = -1;
            if (!readResults.IsCreated)
                return false;

            int length = math.min(readResults.Length, ReadSlotCount);
            for (int i = 0; i < length; i++)
            {
                if (readResults[i].RequestId != 0u)
                    continue;

                index = i;
                return true;
            }

            return false;
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

        private static long ResolveOffset(long sectorHash)
        {
            ulong normalized = unchecked((ulong)sectorHash);
            ulong sector = normalized & (ulong)MaxSectorsMask;
            return WorldDirectoryBytes + (long)(sector * SectorSizeBytes);
        }

        private static int ResolveDirectorySlot(long sectorHash)
        {
            ulong mixed = unchecked((ulong)sectorHash);
            mixed ^= mixed >> 33;
            mixed *= 0xff51afd7ed558ccdUL;
            mixed ^= mixed >> 33;
            return (int)(mixed % (ulong)DirectorySlotCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FoldSectorHash(long sectorHash)
        {
            ulong value = unchecked((ulong)sectorHash);
            return (uint)value ^ (uint)(value >> 32);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateMockWorldPageWriteJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<long> SectorHashes;
            [WriteOnly] public NativeArray<int> DirectorySlots;
            [WriteOnly] public NativeArray<int> PayloadBytes;
            [WriteOnly] public NativeArray<uint> Flags;
            public ulong Seed;
            public int PayloadByteCount;

            public void Execute(int index)
            {
                ulong value = Seed + ((ulong)(uint)index * 0x9E3779B97F4A7C15UL);
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;

                long sectorHash = unchecked((long)value);
                int requestedBytes = math.max(0, PayloadByteCount);
                int safeBytes = math.min(requestedBytes, SectorPayloadBytes);
                uint flags = requestedBytes > SectorPayloadBytes ? PagerTelemetryFlagPayloadOverflowRejected : 0u;

                SectorHashes[index] = sectorHash;
                DirectorySlots[index] = ResolveDirectorySlot(sectorHash);
                PayloadBytes[index] = safeBytes;
                Flags[index] = flags;
            }
        }

        private unsafe void EnsureDirectoryPage()
        {
            FileStream stream = _stream;
            if (stream == null)
                return;

            Span<byte> directory = stackalloc byte[WorldDirectoryBytes];
            fixed (byte* directoryPtr = directory)
            {
                UnsafeUtility.MemClear(directoryPtr, WorldDirectoryBytes);
                WriteUInt(directoryPtr, 0, DirectoryMagic);
                WriteUShort(directoryPtr, 4, PageVersion);
                WriteUShort(directoryPtr, 6, WorldDirectoryBytes);
                WriteInt(directoryPtr, 8, SectorSizeBytes);
                WriteInt(directoryPtr, 12, MaxSectors);
                WriteInt(directoryPtr, 16, DirectorySlotCount);
                WriteInt(directoryPtr, 20, DirectoryEntryBytes);
            }

            try
            {
                lock (_streamLock)
                {
                    bool rewriteDirectory = stream.Length < WorldDirectoryBytes;
                    if (!rewriteDirectory)
                    {
                        Span<byte> header = stackalloc byte[24];
                        stream.Position = 0L;
                        rewriteDirectory = !ReadExact(stream, header);
                        if (!rewriteDirectory)
                        {
                            fixed (byte* headerPtr = header)
                            {
                                rewriteDirectory =
                                    ReadUInt(headerPtr, 0) != DirectoryMagic ||
                                    ReadUShort(headerPtr, 4) != PageVersion ||
                                    ReadUShort(headerPtr, 6) != WorldDirectoryBytes ||
                                    ReadInt(headerPtr, 8) != SectorSizeBytes;
                            }
                        }
                    }

                    if (!rewriteDirectory)
                        return;

                    EnsureStreamLength(stream, WorldDirectoryBytes);
                    stream.Position = 0L;
                    stream.Write(directory);
                    stream.Flush(true);
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        private unsafe bool WriteDirectoryEntry(long sectorHash, long offset)
        {
            return WriteDirectoryEntry(sectorHash, offset, out _, out _, out _);
        }

        private unsafe bool WriteDirectoryEntry(long sectorHash, long offset, out int directorySlot, out bool collision, out long previousSectorHash)
        {
            directorySlot = ResolveDirectorySlot(sectorHash);
            collision = false;
            previousSectorHash = 0L;
            FileStream stream = _stream;
            if (stream == null)
                return false;

            Span<byte> entry = stackalloc byte[DirectoryEntryBytes];
            fixed (byte* entryPtr = entry)
            {
                WriteLong(entryPtr, 0, sectorHash);
                WriteLong(entryPtr, 8, offset);
            }

            try
            {
                lock (_streamLock)
                {
                    long directoryOffset = WorldDirectoryHeaderBytes + ((long)directorySlot * DirectoryEntryBytes);
                    if (stream.Length >= directoryOffset + DirectoryEntryBytes)
                    {
                        Span<byte> existing = stackalloc byte[DirectoryEntryBytes];
                        stream.Position = directoryOffset;
                        if (ReadExact(stream, existing))
                        {
                            fixed (byte* existingPtr = existing)
                            {
                                previousSectorHash = ReadLong(existingPtr, 0);
                                long previousOffset = ReadLong(existingPtr, 8);
                                collision = previousOffset != 0L && previousSectorHash != sectorHash;
                            }
                        }
                    }

                    stream.Position = directoryOffset;
                    stream.Write(entry);
                    stream.Flush(true);
                }

                return true;
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                return false;
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                return false;
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                return false;
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                return false;
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
                return false;
            }
        }

        private static string ResolveWalPath(string worldDataPath)
        {
            if (string.IsNullOrEmpty(worldDataPath))
                return HectonPersistentPathPolicy.CombineFile(WalFileName);

            string directory = Path.GetDirectoryName(worldDataPath);
            return string.IsNullOrEmpty(directory)
                ? HectonPersistentPathPolicy.CombineFile(WalFileName)
                : Path.Combine(directory, WalFileName);
        }

        private unsafe bool AppendWalRecord(byte* pageHeader, byte* storedPayload, int storedBytes, uint frame)
        {
            FileStream walStream = _walStream;
            if (walStream == null || pageHeader == null || storedPayload == null || storedBytes <= 0 || storedBytes > SectorPayloadBytes)
                return false;

            Span<byte> walHeader = stackalloc byte[WalHeaderBytes];
            fixed (byte* walHeaderPtr = walHeader)
            {
                try
                {
                    lock (_hotStateLock)
                    {
                        ResolveHotStateArena(out NativeArray<byte> hotStateArena);
                        int hotStateBytes = hotStateArena.IsCreated ? Math.Min(_hotStateBytes, HotStateMaxBytes) : 0;
                        uint hotStateCrc = hotStateBytes > 0 ? _hotStateCrc32 : 0u;
                        uint hotStateSchemaHash = hotStateBytes > 0 ? _hotStateSchemaHash : 0u;
                        byte* hotStatePtr = hotStateBytes > 0 ? (byte*)hotStateArena.GetUnsafeReadOnlyPtr() : null;

                        WriteWalHeader(
                            walHeaderPtr,
                            ReadLong(pageHeader, 16),
                            ReadUInt(pageHeader, 8),
                            ReadInt(pageHeader, 24),
                            storedBytes,
                            ReadUInt(pageHeader, 12),
                            ReadUInt(pageHeader, 32),
                            frame,
                            ReadUInt(pageHeader, 40),
                            hotStateBytes,
                            hotStateCrc,
                            hotStateSchemaHash,
                            frame);

                        uint recordCrc = ComputeCrc32Pair(walHeaderPtr, WalHeaderBytes, storedPayload, storedBytes);
                        if (hotStateBytes > 0)
                            recordCrc = FinalizeCrc32(UpdateCrc32(~recordCrc, hotStatePtr, hotStateBytes));

                        Span<byte> tail = stackalloc byte[WalTailBytes];
                        fixed (byte* tailPtr = tail)
                        {
                            WriteUInt(tailPtr, 0, recordCrc);
                        }

                        lock (_walLock)
                        {
                            walStream.Position = walStream.Length;
                            walStream.Write(walHeader);
                            walStream.Write(new ReadOnlySpan<byte>(storedPayload, storedBytes));
                            if (hotStateBytes > 0)
                                walStream.Write(new ReadOnlySpan<byte>(hotStatePtr, hotStateBytes));
                            walStream.Write(tail);
                            walStream.Flush(true);
                            long walBytes = walStream.Length;
                            if (walBytes >= WalMicroStallThresholdBytes)
                            {
                                Interlocked.Increment(ref _walMicroStallCount.Value);
                                Thread.Sleep(1);
                            }
                        }
                    }

                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        private void ClearWalAfterCommit()
        {
            FileStream walStream = _walStream;
            if (walStream == null)
                return;

            try
            {
                lock (_walLock)
                {
                    walStream.SetLength(0L);
                    walStream.Position = 0L;
                    walStream.Flush(true);
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        private unsafe void ReplayWalIfPresent()
        {
            FileStream walStream = _walStream;
            FileStream worldStream = _stream;
            ResolveCompressionScratch(out NativeArray<byte> compressionScratch);
            ResolveWriteArena(out NativeArray<byte> writeArena);
            ResolveHotStateArena(out NativeArray<byte> hotStateArena);
            if (walStream == null || worldStream == null || !compressionScratch.IsCreated || !writeArena.IsCreated)
                return;

            try
            {
                lock (_walLock)
                {
                    long walLength = walStream.Length;
                    if (walLength <= 0L)
                        return;

                    walStream.Position = 0L;
                    Span<byte> walHeader = stackalloc byte[WalHeaderBytes];
                    Span<byte> pageHeader = stackalloc byte[SectorHeaderBytes];
                    Span<byte> tail = stackalloc byte[WalTailBytes];
                    bool truncateWal = false;

                    while (walStream.Position < walLength)
                    {
                        if (walLength - walStream.Position < WalHeaderBytes + WalTailBytes ||
                            !ReadExact(walStream, walHeader))
                        {
                            Interlocked.Increment(ref _walCorruptCount.Value);
                            truncateWal = true;
                            break;
                        }

                        fixed (byte* walHeaderPtr = walHeader)
                        {
                            if (!TryReadWalHeader(
                                    walHeaderPtr,
                                    out long sectorHash,
                                    out uint payloadType,
                                    out int rawBytes,
                                    out int storedBytes,
                                    out uint flags,
                                    out uint rawPayloadCheck,
                                    out uint frame,
                                    out uint sourceHash,
                                    out int hotStateBytes,
                                    out uint hotStateCrc,
                                    out uint hotStateSchemaHash))
                            {
                                Interlocked.Increment(ref _walCorruptCount.Value);
                                truncateWal = true;
                                break;
                            }

                            if (walLength - walStream.Position < storedBytes + hotStateBytes + WalTailBytes)
                            {
                                Interlocked.Increment(ref _walCorruptCount.Value);
                                truncateWal = true;
                                break;
                            }

                            byte* storedPtr = (byte*)compressionScratch.GetUnsafePtr();
                            byte* hotStatePtr = hotStateBytes > 0 && hotStateArena.IsCreated
                                ? (byte*)hotStateArena.GetUnsafePtr()
                                : null;
                            byte* rawPtr = storedPtr;
                            if (!ReadExact(walStream, new Span<byte>(storedPtr, storedBytes)) ||
                                (hotStateBytes > 0 && (hotStatePtr == null || !ReadExact(walStream, new Span<byte>(hotStatePtr, hotStateBytes)))) ||
                                !ReadExact(walStream, tail))
                            {
                                Interlocked.Increment(ref _walCorruptCount.Value);
                                truncateWal = true;
                                break;
                            }

                            fixed (byte* tailPtr = tail)
                            {
                                uint expectedTailCrc = ReadUInt(tailPtr, 0);
                                uint actualTailCrc = ComputeCrc32Pair(walHeaderPtr, WalHeaderBytes, storedPtr, storedBytes);
                                if (hotStateBytes > 0)
                                    actualTailCrc = FinalizeCrc32(UpdateCrc32(~actualTailCrc, hotStatePtr, hotStateBytes));
                                if (actualTailCrc != expectedTailCrc)
                                {
                                    Interlocked.Increment(ref _walCorruptCount.Value);
                                    truncateWal = true;
                                    break;
                                }
                            }

                            if (hotStateBytes > 0 && ComputeCrc32(hotStatePtr, hotStateBytes) != hotStateCrc)
                            {
                                Interlocked.Increment(ref _walCorruptCount.Value);
                                truncateWal = true;
                                break;
                            }
                            if (hotStateBytes > 0)
                            {
                                _hotStateBytes = hotStateBytes;
                                _hotStateCrc32 = hotStateCrc;
                                _hotStateSchemaHash = hotStateSchemaHash;
                                _hotStateFrame = frame;
                            }

                            if ((flags & PageFlagCompressed) != 0u)
                            {
                                rawPtr = (byte*)writeArena.GetUnsafePtr();
                                if (!TryDecompressRle(storedPtr, storedBytes, rawPtr, rawBytes))
                                {
                                    Interlocked.Increment(ref _walCorruptCount.Value);
                                    truncateWal = true;
                                    break;
                                }
                            }

                            uint actualRawPayloadCheck = ComputePayloadCheck32(rawPtr, rawBytes, flags);
                            if (actualRawPayloadCheck != rawPayloadCheck)
                            {
                                Interlocked.Increment(ref _walCorruptCount.Value);
                                truncateWal = true;
                                break;
                            }

                            fixed (byte* pageHeaderPtr = pageHeader)
                            {
                                WriteHeader(pageHeaderPtr, sectorHash, payloadType, rawBytes, storedBytes, flags, rawPayloadCheck, frame, sourceHash, 0u);
                            }

                            long offset = ResolveOffset(sectorHash);
                            bool mappedWrite = TryWriteWorldPageMapped(worldStream, offset, pageHeader, storedPtr, storedBytes);
                            if (!mappedWrite)
                            {
                                lock (_streamLock)
                                {
                                    EnsureStreamLength(worldStream, offset + SectorHeaderBytes + storedBytes);
                                    worldStream.Position = offset;
                                    worldStream.Write(pageHeader);
                                    worldStream.Write(new ReadOnlySpan<byte>(storedPtr, storedBytes));
                                    worldStream.Flush(true);
                                }
                            }

                            if (!WriteDirectoryEntry(sectorHash, offset))
                            {
                                truncateWal = false;
                                break;
                            }

                            Interlocked.Increment(ref _walReplayCount.Value);
                            RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, rawBytes, PagerTelemetryOperation.WalReplay, H8WorldPageStatus.Ready, flags | PagerTelemetryFlagWalReplay | (mappedWrite ? PagerTelemetryFlagMmfCommit : PagerTelemetryFlagFileStreamCommit));
                            truncateWal = true;
                        }
                    }

                    if (truncateWal)
                    {
                        walStream.SetLength(0L);
                        walStream.Position = 0L;
                        walStream.Flush(true);
                    }
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        private unsafe bool TryWriteWorldPageMapped(FileStream stream, long offset, Span<byte> header, byte* storedPayload, int storedBytes)
        {
            if (stream == null || storedPayload == null || storedBytes <= 0)
                return false;

#if UNITY_EDITOR || UNITY_STANDALONE || HECTON8_MMF_AVAILABLE
            long endOffset = offset + SectorHeaderBytes + storedBytes;
            try
            {
                lock (_streamLock)
                {
                    EnsureStreamLength(stream, endOffset);
                    using MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                        stream,
                        null,
                        endOffset,
                        MemoryMappedFileAccess.ReadWrite,
                        HandleInheritability.None,
                        false);
                    using MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(
                        offset,
                        SectorHeaderBytes + storedBytes,
                        MemoryMappedFileAccess.Write);
                    byte* mappedPtr = null;
                    try
                    {
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPtr);
                        byte* target = mappedPtr + (int)accessor.PointerOffset;
                        fixed (byte* headerPtr = header)
                        {
                            UnsafeUtility.MemCpy(target, headerPtr, SectorHeaderBytes);
                        }

                        UnsafeUtility.MemCpy(target + SectorHeaderBytes, storedPayload, storedBytes);
                        accessor.Flush();
                        stream.Flush(true);
                    }
                    finally
                    {
                        if (mappedPtr != null)
                            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }

                return true;
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        private static void EnsureStreamLength(FileStream stream, long minimumLength)
        {
            if (stream != null && minimumLength > stream.Length)
                stream.SetLength(minimumLength);
        }

        private static unsafe bool TryCompressRle(byte* input, int inputBytes, byte* output, int outputCapacity, out int outputBytes)
        {
            outputBytes = 0;
            int read = 0;
            while (read < inputBytes)
            {
                byte value = input[read];
                int run = 1;
                while (read + run < inputBytes && run < ushort.MaxValue && input[read + run] == value)
                    run++;

                if (outputBytes + 3 > outputCapacity)
                    return false;

                output[outputBytes++] = value;
                ushort run16 = (ushort)run;
                output[outputBytes++] = unchecked((byte)run16);
                output[outputBytes++] = unchecked((byte)(run16 >> 8));
                read += run;
            }

            return outputBytes > 0 && outputBytes < inputBytes;
        }

        private static unsafe bool TryDecompressRle(byte* input, int inputBytes, byte* output, int expectedOutputBytes)
        {
            int read = 0;
            int write = 0;
            while (read + 2 < inputBytes)
            {
                byte value = input[read++];
                int run = input[read++] | (input[read++] << 8);
                if (run <= 0 || write + run > expectedOutputBytes)
                    return false;

                UnsafeUtility.MemSet(output + write, value, run);
                write += run;
            }

            return read == inputBytes && write == expectedOutputBytes;
        }

        private static unsafe uint ComputePayloadCheck32(byte* data, int byteCount, uint flags)
        {
            return (flags & PageFlagPayloadHashXxHash3) != 0u
                ? ComputePayloadHash32(data, byteCount)
                : ComputeCrc32(data, byteCount);
        }

        private static unsafe uint ComputePayloadHash32(byte* data, int byteCount)
        {
            if (data == null || byteCount <= 0)
                return 0u;

            uint2 hash = xxHash3.Hash64(data, (long)byteCount);
            return hash.x ^ hash.y;
        }

        private static unsafe uint ComputeCrc32(byte* data, int byteCount)
        {
            return FinalizeCrc32(UpdateCrc32(0xFFFFFFFFu, data, byteCount));
        }

        private static unsafe uint ComputeCrc32Pair(byte* first, int firstBytes, byte* second, int secondBytes)
        {
            uint crc = 0xFFFFFFFFu;
            crc = UpdateCrc32(crc, first, firstBytes);
            crc = UpdateCrc32(crc, second, secondBytes);
            return FinalizeCrc32(crc);
        }

        private static unsafe uint UpdateCrc32(uint crc, byte* data, int byteCount)
        {
            if (data == null || byteCount <= 0)
                return crc;

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

        private static unsafe void WriteHeader(
            byte* header,
            long sectorHash,
            uint payloadType,
            int rawBytes,
            int storedBytes,
            uint flags,
            uint crc32,
            uint frame,
            uint sourceHash,
            uint requestId)
        {
            UnsafeUtility.MemClear(header, SectorHeaderBytes);
            WriteUInt(header, 0, PageMagic);
            WriteUShort(header, 4, PageVersion);
            WriteUShort(header, 6, SectorHeaderBytes);
            WriteUInt(header, 8, payloadType);
            WriteUInt(header, 12, flags);
            WriteLong(header, 16, sectorHash);
            WriteInt(header, 24, rawBytes);
            WriteInt(header, 28, storedBytes);
            WriteUInt(header, 32, crc32);
            WriteUInt(header, 36, frame);
            WriteUInt(header, 40, sourceHash);
            WriteUInt(header, 44, requestId);
        }

        private static unsafe void WriteWalHeader(
            byte* header,
            long sectorHash,
            uint payloadType,
            int rawBytes,
            int storedBytes,
            uint flags,
            uint rawCrc32,
            uint frame,
            uint sourceHash,
            int hotStateBytes,
            uint hotStateCrc32,
            uint hotStateSchemaHash,
            ulong sequence)
        {
            UnsafeUtility.MemClear(header, WalHeaderBytes);
            WriteUInt(header, 0, WalMagic);
            WriteUShort(header, 4, WalVersion);
            WriteUShort(header, 6, WalHeaderBytes);
            WriteUInt(header, 8, payloadType);
            WriteUInt(header, 12, flags);
            WriteLong(header, 16, sectorHash);
            WriteInt(header, 24, rawBytes);
            WriteInt(header, 28, storedBytes);
            WriteUInt(header, 32, rawCrc32);
            WriteUInt(header, 36, frame);
            WriteUInt(header, 40, sourceHash);
            WriteInt(header, 44, hotStateBytes);
            WriteULong(header, 48, sequence);
            WriteUInt(header, 56, hotStateCrc32);
            WriteUInt(header, 60, hotStateSchemaHash);
        }

        private static unsafe bool TryReadWalHeader(
            byte* header,
            out long sectorHash,
            out uint payloadType,
            out int rawBytes,
            out int storedBytes,
            out uint flags,
            out uint rawCrc32,
            out uint frame,
            out uint sourceHash,
            out int hotStateBytes,
            out uint hotStateCrc32,
            out uint hotStateSchemaHash)
        {
            sectorHash = 0L;
            payloadType = 0u;
            rawBytes = 0;
            storedBytes = 0;
            flags = 0u;
            rawCrc32 = 0u;
            frame = 0u;
            sourceHash = 0u;
            hotStateBytes = 0;
            hotStateCrc32 = 0u;
            hotStateSchemaHash = 0u;

            if (ReadUInt(header, 0) != WalMagic ||
                ReadUShort(header, 4) != WalVersion ||
                ReadUShort(header, 6) != WalHeaderBytes)
            {
                return false;
            }

            payloadType = ReadUInt(header, 8);
            flags = ReadUInt(header, 12);
            sectorHash = ReadLong(header, 16);
            rawBytes = ReadInt(header, 24);
            storedBytes = ReadInt(header, 28);
            rawCrc32 = ReadUInt(header, 32);
            frame = ReadUInt(header, 36);
            sourceHash = ReadUInt(header, 40);
            hotStateBytes = ReadInt(header, 44);
            hotStateCrc32 = ReadUInt(header, 56);
            hotStateSchemaHash = ReadUInt(header, 60);

            return rawBytes > 0 &&
                   rawBytes <= SectorPayloadBytes &&
                   storedBytes > 0 &&
                   storedBytes <= SectorPayloadBytes &&
                   hotStateBytes >= 0 &&
                   hotStateBytes <= HotStateMaxBytes;
        }

        private static unsafe bool TryReadHeader(
            byte* header,
            long sectorHash,
            uint payloadType,
            out int rawBytes,
            out int storedBytes,
            out uint flags,
            out uint crc32)
        {
            rawBytes = 0;
            storedBytes = 0;
            flags = 0u;
            crc32 = 0u;
            if (ReadUInt(header, 0) != PageMagic ||
                ReadUShort(header, 4) != PageVersion ||
                ReadUShort(header, 6) != SectorHeaderBytes ||
                ReadUInt(header, 8) != payloadType ||
                ReadLong(header, 16) != sectorHash)
            {
                return false;
            }

            flags = ReadUInt(header, 12);
            rawBytes = ReadInt(header, 24);
            storedBytes = ReadInt(header, 28);
            crc32 = ReadUInt(header, 32);
            return true;
        }

        private static unsafe bool HeaderIsEmpty(byte* header)
        {
            for (int i = 0; i < SectorHeaderBytes; i++)
            {
                if (header[i] != 0)
                    return false;
            }

            return true;
        }

        private static unsafe bool HeaderIsDifferentPage(byte* header, long sectorHash, uint payloadType)
        {
            return ReadUInt(header, 0) == PageMagic &&
                   ReadUShort(header, 4) == PageVersion &&
                   ReadUShort(header, 6) == SectorHeaderBytes &&
                   (ReadUInt(header, 8) != payloadType || ReadLong(header, 16) != sectorHash);
        }

        private static unsafe void WriteUInt(byte* ptr, int offset, uint value)
        {
            ptr[offset] = unchecked((byte)value);
            ptr[offset + 1] = unchecked((byte)(value >> 8));
            ptr[offset + 2] = unchecked((byte)(value >> 16));
            ptr[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static unsafe void WriteUShort(byte* ptr, int offset, ushort value)
        {
            ptr[offset] = unchecked((byte)value);
            ptr[offset + 1] = unchecked((byte)(value >> 8));
        }

        private static unsafe void WriteInt(byte* ptr, int offset, int value)
        {
            WriteUInt(ptr, offset, unchecked((uint)value));
        }

        private static unsafe void WriteULong(byte* ptr, int offset, ulong value)
        {
            WriteUInt(ptr, offset, unchecked((uint)value));
            WriteUInt(ptr, offset + 4, unchecked((uint)(value >> 32)));
        }

        private static unsafe void WriteLong(byte* ptr, int offset, long value)
        {
            WriteULong(ptr, offset, unchecked((ulong)value));
        }

        private static unsafe uint ReadUInt(byte* ptr, int offset)
        {
            return ptr[offset] |
                   ((uint)ptr[offset + 1] << 8) |
                   ((uint)ptr[offset + 2] << 16) |
                   ((uint)ptr[offset + 3] << 24);
        }

        private static unsafe ushort ReadUShort(byte* ptr, int offset)
        {
            return (ushort)(ptr[offset] | (ptr[offset + 1] << 8));
        }

        private static unsafe int ReadInt(byte* ptr, int offset)
        {
            return unchecked((int)ReadUInt(ptr, offset));
        }

        private static unsafe ulong ReadULong(byte* ptr, int offset)
        {
            return ReadUInt(ptr, offset) | ((ulong)ReadUInt(ptr, offset + 4) << 32);
        }

        private static unsafe long ReadLong(byte* ptr, int offset)
        {
            return unchecked((long)ReadULong(ptr, offset));
        }

        private void PublishLast(long sectorHash, uint payloadType, int payloadBytes, uint frame)
        {
            Volatile.Write(ref _lastSectorHash, sectorHash);
            Volatile.Write(ref _lastPayloadType, payloadType);
            Volatile.Write(ref _lastPayloadBytes, payloadBytes);
            Volatile.Write(ref _lastFrame, frame);
        }

        private void SetQueueHighWatermark(int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref _queueHighWatermark.Value);
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref _queueHighWatermark.Value, value, current) != current);
        }

        private void RecordTelemetry(
            long sectorHash,
            long offset,
            uint payloadType,
            uint frame,
            uint requestId,
            int payloadBytes,
            PagerTelemetryOperation operation,
            H8WorldPageStatus status,
            uint flags,
            int directorySlot = -1,
            uint metrics = 0u)
        {
            ResolveTelemetryRing(out NativeArray<H8BinaryWorldPagerTelemetryEntry> telemetryRing);
            if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                return;

            int index = Interlocked.Increment(ref _telemetryCursor.Value);
            if (index == int.MaxValue)
                Interlocked.Exchange(ref _telemetryCursor.Value, 0);

            int slot = (index & int.MaxValue) % TelemetryCapacity;
            telemetryRing[slot] = new H8BinaryWorldPagerTelemetryEntry
            {
                SectorHash = sectorHash,
                Offset = offset,
                Frame = frame,
                RequestId = requestId,
                PayloadType = payloadType,
                PendingWrites = Volatile.Read(ref _pendingWriteCount.Value),
                PendingReads = Volatile.Read(ref _pendingReadCount.Value),
                PageFaults = Volatile.Read(ref _pageFaultCount.Value),
                PayloadBytes = payloadBytes,
                Operation = operation,
                Status = status,
                Flags = unchecked((ushort)flags),
                TicksUtc = DateTime.UtcNow.Ticks,
                DirectorySlot = directorySlot,
                Metrics = metrics
            };
        }

        private string ResolveDumpPath()
        {
            return ResolveAgentLogPath(DumpFileName);
        }

        private string ResolveCrashDumpPath()
        {
            return ResolveAgentLogPath(CrashDumpFileName);
        }

        private static string ResolveAgentLogPath(string fileName)
        {
            string projectRoot = Application.dataPath;
            if (string.IsNullOrEmpty(projectRoot))
                projectRoot = HectonPersistentPathPolicy.RootPath;
            else
                projectRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));

            return Path.Combine(projectRoot, "Docs", "AgentLogs", fileName);
        }

        private unsafe void DumpBlackBox()
        {
            if (Volatile.Read(ref _workerRunning) != 0 &&
                Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref _workerThreadId))
            {
                Volatile.Write(ref _dumpRequestPending, 1);
                return;
            }

            WriteBlackBoxDumps();
        }

        private bool TryConsumeDumpRequest()
        {
            return Interlocked.Exchange(ref _dumpRequestPending, 0) != 0;
        }

        private unsafe void WriteBlackBoxDumps()
        {
            ResolveTelemetryRing(out NativeArray<H8BinaryWorldPagerTelemetryEntry> telemetryRing);
            if (!telemetryRing.IsCreated || string.IsNullOrEmpty(_dumpPath))
                return;

            WriteBlackBoxDump(_dumpPath);
            WriteBlackBoxDump(_crashDumpPath);
            WriteBlackBoxDump(_dumpH8Path);
            WriteBlackBoxDump(_crashDumpH8Path);
        }

        private unsafe void WriteBlackBoxDump(string dumpPath)
        {
            ResolveTelemetryRing(out NativeArray<H8BinaryWorldPagerTelemetryEntry> telemetryRing);
            if (string.IsNullOrEmpty(dumpPath) || !telemetryRing.IsCreated)
                return;

            try
            {
                HectonPersistentPathPolicy.EnsureParentDirectory(dumpPath);
                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
                Span<byte> header = stackalloc byte[16];
                fixed (byte* headerPtr = header)
                {
                    WriteUInt(headerPtr, 0, 0x444D4838u); // H8MD
                    WriteInt(headerPtr, 4, TelemetryCapacity);
                    WriteInt(headerPtr, 8, UnsafeUtility.SizeOf<H8BinaryWorldPagerTelemetryEntry>());
                    WriteInt(headerPtr, 12, Volatile.Read(ref _telemetryCursor.Value));
                }

                stream.Write(header);
                stream.Write(new ReadOnlySpan<byte>(telemetryRing.GetUnsafeReadOnlyPtr(), telemetryRing.Length * UnsafeUtility.SizeOf<H8BinaryWorldPagerTelemetryEntry>()));
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref _ioErrorCount.Value);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CacheLineInt
        {
            [FieldOffset(0)] public int Value;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct PageWriteCommand
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public uint PayloadType;
            [FieldOffset(12)] public int ByteOffset;
            [FieldOffset(16)] public int ByteCount;
            [FieldOffset(20)] public uint SourceHash;
            [FieldOffset(24)] public uint Frame;
            [FieldOffset(28)] public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PageReadCommand
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public uint PayloadType;
            [FieldOffset(12)] public uint RequestId;
            [FieldOffset(16)] public uint Frame;
            [FieldOffset(20)] public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct PageReadResult
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public uint PayloadType;
            [FieldOffset(12)] public uint RequestId;
            [FieldOffset(16)] public int SlotIndex;
            [FieldOffset(20)] public int ByteCount;
            [FieldOffset(24)] public uint Reserved2;
            [FieldOffset(28)] public ushort Reserved1;
            [FieldOffset(30)] public H8WorldPageStatus Status;
            [FieldOffset(31)] public byte Reserved0;
        }

        private enum PagerTelemetryOperation : byte
        {
            Write = 1,
            ReadReady = 2,
            ReadMiss = 3,
            ReadCorrupt = 4,
            WalReplay = 5,
            WalAppendFailed = 6,
            WriteRejected = 7
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct H8BinaryWorldPagerTelemetryEntry
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public long Offset;
            [FieldOffset(16)] public long TicksUtc;
            [FieldOffset(24)] public uint PayloadType;
            [FieldOffset(28)] public uint Frame;
            [FieldOffset(32)] public uint RequestId;
            [FieldOffset(36)] public int PayloadBytes;
            [FieldOffset(40)] public int PendingWrites;
            [FieldOffset(44)] public int PendingReads;
            [FieldOffset(48)] public int PageFaults;
            [FieldOffset(52)] public uint Metrics;
            [FieldOffset(56)] public int DirectorySlot;
            [FieldOffset(60)] public ushort Flags;
            [FieldOffset(62)] public PagerTelemetryOperation Operation;
            [FieldOffset(63)] public H8WorldPageStatus Status;
        }
    }
}
