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
using Hecton8.SaveSystem;
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
        private const int WriteSlotCount = 8;
        private const int ReadSlotCount = 4;
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

        private VaultGenerationHandle<byte> _readStagingHandle;
        private VaultGenerationHandle<H8BinaryWorldPagerTelemetryEntry> _telemetryRingHandle;
        private PagerNativeState _nativeState;
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

        private void InvalidateWorldReadCache()
        {
            if (!string.IsNullOrEmpty(_path))
                AsyncWriteManager.InvalidateCachedReadWindows(_path);
        }

        private void InvalidateWalReadCache()
        {
            if (!string.IsNullOrEmpty(_walPath))
                AsyncWriteManager.InvalidateCachedReadWindows(_walPath);
        }

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
                MarkInitializationFault(PagerInitializationFaultReason.OpenStream);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                MarkInitializationFault(PagerInitializationFaultReason.OpenWriteAheadLog);
                return;
            }

            if (!AllocateNativeState() || HasInitializationFault)
                return;

            EnsureDirectoryPage();
            ReplayWalIfPresent();
            Volatile.Write(ref _disposeRequested, 0);
            Volatile.Write(ref _initialized, 1);
            if (!StartWorker())
                MarkInitializationFault(PagerInitializationFaultReason.WorkerStart);
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

            if (!TryAcquireDirectReadStagingWrite(vault, out VaultSliceHandle<byte> stagingSlice, out NativeArray<byte> sliceBytes, out IDataVault stagingVault))
            {
                return false;
            }

            long offset = ResolveOffset(sectorHash);
            Span<byte> header = stackalloc byte[SectorHeaderBytes];
            bool recordTelemetry = false;
            bool dumpBlackBox = false;
            int telemetryBytes = 0;
            uint telemetryFlags = PageFlagProceduralFallback;
            PagerTelemetryOperation telemetryOperation = PagerTelemetryOperation.ReadMiss;
            try
            {
                try
                {
                    do
                    {
                        FileStream stream = _stream;
                        if (stream == null)
                        {
                            status = H8WorldPageStatus.IOError;
                            telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                            recordTelemetry = true;
                            break;
                        }

                        lock (_streamLock)
                        {
                            if (stream.Length < offset + SectorHeaderBytes)
                            {
                                status = H8WorldPageStatus.Missing;
                                recordTelemetry = true;
                                break;
                            }

                            stream.Position = offset;
                            if (!ReadExact(stream, header))
                            {
                                status = H8WorldPageStatus.Missing;
                                recordTelemetry = true;
                                break;
                            }

                            fixed (byte* headerPtr = header)
                            {
                                if (!TryReadHeader(headerPtr, sectorHash, payloadType, out int rawBytes, out int storedBytes, out uint flags, out uint expectedPayloadCheck))
                                {
                                    status = HeaderIsEmpty(headerPtr) || HeaderIsDifferentPage(headerPtr, sectorHash, payloadType)
                                        ? H8WorldPageStatus.Missing
                                        : H8WorldPageStatus.Corrupt;
                                    telemetryOperation = status == H8WorldPageStatus.Corrupt ? PagerTelemetryOperation.ReadCorrupt : PagerTelemetryOperation.ReadMiss;
                                    recordTelemetry = true;
                                    break;
                                }

                                if (rawBytes <= 0 || rawBytes > SectorPayloadBytes || storedBytes <= 0 || storedBytes > SectorPayloadBytes)
                                {
                                    status = H8WorldPageStatus.Corrupt;
                                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                                    recordTelemetry = true;
                                    dumpBlackBox = true;
                                    break;
                                }

                                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sliceBytes);
                                if ((flags & PageFlagCompressed) != 0u)
                                {
                                    byte* storedPtr = rawPtr + SectorPayloadBytes;
                                    if (!ReadExact(stream, new Span<byte>(storedPtr, storedBytes)) ||
                                        !TryDecompressRle(storedPtr, storedBytes, rawPtr, rawBytes))
                                    {
                                        status = H8WorldPageStatus.Corrupt;
                                        telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                                        recordTelemetry = true;
                                        dumpBlackBox = true;
                                        break;
                                    }
                                }
                                else if (!ReadExact(stream, new Span<byte>(rawPtr, rawBytes)))
                                {
                                    status = H8WorldPageStatus.Corrupt;
                                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                                    recordTelemetry = true;
                                    dumpBlackBox = true;
                                    break;
                                }

                                uint actualPayloadCheck = ComputePayloadCheck32(rawPtr, rawBytes, flags);
                                if (actualPayloadCheck != expectedPayloadCheck)
                                {
                                    status = H8WorldPageStatus.Corrupt;
                                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                                    telemetryBytes = rawBytes;
                                    recordTelemetry = true;
                                    dumpBlackBox = true;
                                    break;
                                }

                                bytesWritten = rawBytes;
                                status = H8WorldPageStatus.Ready;
                                slice = stagingSlice;
                                telemetryOperation = PagerTelemetryOperation.ReadReady;
                                telemetryBytes = rawBytes;
                                telemetryFlags = flags;
                                recordTelemetry = true;
                            }
                        }
                    }
                    while (false);
                }
                catch (IOException)
                {
                    status = H8WorldPageStatus.IOError;
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                    telemetryBytes = bytesWritten;
                    recordTelemetry = true;
                }
                catch (UnauthorizedAccessException)
                {
                    status = H8WorldPageStatus.IOError;
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                    telemetryBytes = bytesWritten;
                    recordTelemetry = true;
                }
                catch (ObjectDisposedException)
                {
                    status = H8WorldPageStatus.IOError;
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                    telemetryBytes = bytesWritten;
                    recordTelemetry = true;
                }
                catch (NotSupportedException)
                {
                    status = H8WorldPageStatus.IOError;
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                    telemetryBytes = bytesWritten;
                    recordTelemetry = true;
                }
                catch (ArgumentException)
                {
                    status = H8WorldPageStatus.IOError;
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                    telemetryBytes = bytesWritten;
                    recordTelemetry = true;
                }
                catch (InvalidOperationException)
                {
                    status = H8WorldPageStatus.IOError;
                    Interlocked.Increment(ref _ioErrorCount.Value);
                    telemetryOperation = PagerTelemetryOperation.ReadCorrupt;
                    telemetryBytes = bytesWritten;
                    recordTelemetry = true;
                }
            }
            finally
            {
                ReleasePagerVaultWrite(stagingVault, in _readStagingHandle, BufferID.SaveWorldPagerReadStaging);
            }

            if (recordTelemetry)
                RecordTelemetry(sectorHash, offset, payloadType, frame, 0u, telemetryBytes, telemetryOperation, status, telemetryFlags);

            if (dumpBlackBox)
                DumpBlackBox();

            return true;
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
            _nativeState.Dispose();
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

        private void AbortInitializationAttempt(IDataVault vault)
        {
            FileStream stream = _stream;
            _stream = null;
            if (stream != null)
                DisposeStream(stream, flush: false);

            FileStream walStream = _walStream;
            _walStream = null;
            if (walStream != null)
                DisposeWalStream(walStream, flush: false);

            ClearPagerTransientBuffers();
            _nativeState.Dispose();
            ReleasePagerVaultHandles(vault ?? _vault);
            _vault = null;
            ResetPagerTransientState();
            _hotStateBytes = 0;
            _hotStateSchemaHash = 0u;
            _hotStateFrame = 0u;
            _hotStateCrc32 = 0u;
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _disposeRequested, 1);
        }

        private void MarkInitializationFault(PagerInitializationFaultReason reason = PagerInitializationFaultReason.Unknown)
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

            Hecton8.Core.H8Debug.LogWarning("H8BinaryWorldPager disabled page IO after initialization fault. reason=" + reason);
        }

        private bool WaitForWorkerExit()
        {
            if (Volatile.Read(ref _workerRunning) == 0)
                return true;

            Thread workerThread = _workerThread;
            if (workerThread != null)
            {
                if (ReferenceEquals(Thread.CurrentThread, workerThread))
                    return false;

                try
                {
                    workerThread.Join(WorkerShutdownWaitMilliseconds);
                    if (!workerThread.IsAlive)
                        return true;
                }
                catch (Exception)
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

        private bool AllocateNativeState()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
            {
                AbortInitializationAttempt(vault);
                return false;
            }

            _vault = vault;
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (!_nativeState.EnsureAll())
            {
                MarkInitializationFault(PagerInitializationFaultReason.NativeStateAllocation);
                return false;
            }

            _readStagingHandle = EnsureOwnedPagerVaultHandle<byte>(
                vault,
                BufferID.SaveWorldPagerReadStaging,
                SectorPayloadBytes * 2,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = EnsureOwnedPagerVaultHandle<H8BinaryWorldPagerTelemetryEntry>(
                vault,
                BufferID.SaveWorldPagerTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory);

            if (!ArePagerVaultHandlesReady())
            {
                AbortInitializationAttempt(vault);
                return false;
            }

            ResetPagerTransientState();
            ClearPagerTransientBuffers();
            return true;
        }

        private void DisposeNativeState()
        {
            ClearPagerTransientBuffers();

            _nativeState.Dispose();
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
                _nativeState.IsReady() &&
                HasPagerVaultBuffer(in _readStagingHandle, BufferID.SaveWorldPagerReadStaging, SectorPayloadBytes * 2) &&
                HasPagerVaultBuffer(in _telemetryRingHandle, BufferID.SaveWorldPagerTelemetryRing, TelemetryCapacity);
        }

        private bool HasPagerVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return TryReadPagerVaultBuffer(in handle, bufferId, requiredLength, out _);
        }

        private static VaultGenerationHandle<T> EnsureOwnedPagerVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                VaultOwner,
                options);
            if (IsPagerVaultHandle(in handle, bufferId))
                return handle;

            for (int releaseAttempt = 0; releaseAttempt < 8 &&
                 handle.BufferID == unchecked((uint)(int)bufferId) &&
                 handle.Generation != 0u &&
                 handle.SystemID != (uint)VaultOwner; releaseAttempt++)
            {
                if (!vault.ReleaseBuffer(in handle))
                    break;

                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    VaultOwner,
                    options);
                if (IsPagerVaultHandle(in handle, bufferId))
                    return handle;
            }

            return handle;
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
            ReleasePagerVaultHandle(vault, ref _readStagingHandle, BufferID.SaveWorldPagerReadStaging);
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
            array = _nativeState.WriteCommands;
        }

        private void ResolveReadCommands(out NativeArray<PageReadCommand> array)
        {
            array = _nativeState.ReadCommands;
        }

        private void ResolveReadResults(out NativeArray<PageReadResult> array)
        {
            array = _nativeState.ReadResults;
        }

        private void ResolveWriteArena(out NativeArray<byte> array)
        {
            array = _nativeState.WriteArena;
        }

        private void ResolveReadArena(out NativeArray<byte> array)
        {
            array = _nativeState.ReadArena;
        }

        private bool TryAcquireDirectReadStagingWrite(
            IDataVault vault,
            out VaultSliceHandle<byte> slice,
            out NativeArray<byte> sliceBytes,
            out IDataVault lockedVault)
        {
            slice = default;
            sliceBytes = default;
            lockedVault = null;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsPagerVaultHandle(in _readStagingHandle, BufferID.SaveWorldPagerReadStaging) ||
                !vault.TryAcquireWriteLock(in _readStagingHandle, VaultOwner, out NativeArray<byte> staging))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (!staging.IsCreated || staging.Length < SectorPayloadBytes * 2)
                    return false;

                slice.BufferID = _readStagingHandle.BufferID;
                slice.SystemID = _readStagingHandle.SystemID;
                slice.Generation = _readStagingHandle.Generation;
                slice.HandleFlags = _readStagingHandle.Flags;
                slice.StartIndex = 0;
                slice.Length = SectorPayloadBytes * 2;
                slice.Flags = 0u;
                slice.Reserved0 = 0u;
                sliceBytes = staging;
                lockedVault = vault;
                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in _readStagingHandle, VaultOwner);
            }
        }

        private void ResolveReadSlotStates(out NativeArray<byte> array)
        {
            array = _nativeState.ReadSlotStates;
        }

        private void ResolveCompressionScratch(out NativeArray<byte> array)
        {
            array = _nativeState.CompressionScratch;
        }

        private void ResolveHotStateArena(out NativeArray<byte> array)
        {
            array = _nativeState.HotStateArena;
        }

        private bool TryReadTelemetryRing(out NativeArray<H8BinaryWorldPagerTelemetryEntry>.ReadOnly array)
        {
            return TryReadPagerVaultBuffer(in _telemetryRingHandle, BufferID.SaveWorldPagerTelemetryRing, TelemetryCapacity, out array);
        }

        private bool TryAcquirePagerVaultWrite<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> array,
            out IDataVault lockedVault) where T : struct
        {
            array = default;
            lockedVault = null;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsPagerVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwner, out array))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (array.IsCreated && array.Length >= requiredLength)
                {
                    lockedVault = vault;
                    ownershipTransferred = true;
                    return true;
                }

                array = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in handle, VaultOwner);
            }
        }

        private static void ReleasePagerVaultWrite<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsPagerVaultHandle(in handle, bufferId))
                vault.ReleaseWriteLock(in handle, VaultOwner);
        }

        private bool StartWorker()
        {
            if (Interlocked.Exchange(ref _workerRunning, 1) != 0)
                return true;

            try
            {
                Thread workerThread = new Thread(RunWorkerLoop)
                {
                    IsBackground = true,
                    Name = "H8 Binary World Pager"
                };

                _workerThread = workerThread;
                workerThread.Start();
                return true;
            }
            catch (Exception)
            {
                _workerThread = null;
                Volatile.Write(ref _workerThreadId, 0);
                Volatile.Write(ref _workerRunning, 0);
                return false;
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
                            InvalidateWorldReadCache();
                            try
                            {
                                EnsureStreamLength(stream, offset + SectorHeaderBytes + storedBytes);
                                stream.Position = offset;
                                stream.Write(header);
                                stream.Write(new ReadOnlySpan<byte>(stored, storedBytes));
                                stream.Flush(true);
                            }
                            finally
                            {
                                InvalidateWorldReadCache();
                            }
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

                    InvalidateWorldReadCache();
                    try
                    {
                        EnsureStreamLength(stream, WorldDirectoryBytes);
                        stream.Position = 0L;
                        stream.Write(directory);
                        stream.Flush(true);
                    }
                    finally
                    {
                        InvalidateWorldReadCache();
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

                    InvalidateWorldReadCache();
                    try
                    {
                        stream.Position = directoryOffset;
                        stream.Write(entry);
                        stream.Flush(true);
                    }
                    finally
                    {
                        InvalidateWorldReadCache();
                    }
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
                            InvalidateWalReadCache();
                            try
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
                            finally
                            {
                                InvalidateWalReadCache();
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
                    InvalidateWalReadCache();
                    try
                    {
                        walStream.SetLength(0L);
                        walStream.Position = 0L;
                        walStream.Flush(true);
                    }
                    finally
                    {
                        InvalidateWalReadCache();
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
                                    InvalidateWorldReadCache();
                                    try
                                    {
                                        EnsureStreamLength(worldStream, offset + SectorHeaderBytes + storedBytes);
                                        worldStream.Position = offset;
                                        worldStream.Write(pageHeader);
                                        worldStream.Write(new ReadOnlySpan<byte>(storedPtr, storedBytes));
                                        worldStream.Flush(true);
                                    }
                                    finally
                                    {
                                        InvalidateWorldReadCache();
                                    }
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
                        InvalidateWalReadCache();
                        try
                        {
                            walStream.SetLength(0L);
                            walStream.Position = 0L;
                            walStream.Flush(true);
                        }
                        finally
                        {
                            InvalidateWalReadCache();
                        }
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
                    InvalidateWorldReadCache();
                    try
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
                    finally
                    {
                        InvalidateWorldReadCache();
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
            VaultGenerationHandle<H8BinaryWorldPagerTelemetryEntry> telemetryHandle = _telemetryRingHandle;
            if (!TryAcquirePagerVaultWrite(
                    in telemetryHandle,
                    BufferID.SaveWorldPagerTelemetryRing,
                    TelemetryCapacity,
                    out NativeArray<H8BinaryWorldPagerTelemetryEntry> telemetryRing,
                    out IDataVault lockedVault))
            {
                return;
            }

            try
            {
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
            finally
            {
                ReleasePagerVaultWrite(lockedVault, in telemetryHandle, BufferID.SaveWorldPagerTelemetryRing);
            }
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
            if (!TryReadTelemetryRing(out NativeArray<H8BinaryWorldPagerTelemetryEntry>.ReadOnly telemetryRing))
                return;

            WriteBlackBoxDump(_dumpPath, telemetryRing);
            WriteBlackBoxDump(_dumpH8Path, telemetryRing);
        }

        private unsafe void WriteBlackBoxDump(
            string dumpPath,
            NativeArray<H8BinaryWorldPagerTelemetryEntry>.ReadOnly telemetryRing)
        {
            if (string.IsNullOrEmpty(dumpPath) || !telemetryRing.IsCreated)
                return;

            string absoluteDumpPath = null;
            string tempPath = null;
            try
            {
                absoluteDumpPath = Path.GetFullPath(dumpPath);
                tempPath = absoluteDumpPath + ".tmp";
                HectonPersistentPathPolicy.EnsureParentDirectory(absoluteDumpPath);
                TryDeleteBlackBoxDumpTempFile(tempPath);
                int count = math.min(telemetryRing.Length, TelemetryCapacity);
                long expectedBytes = (long)count * UnsafeUtility.SizeOf<H8BinaryWorldPagerTelemetryEntry>();
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                try
                {
                    using (FileStream stream = new FileStream(
                               tempPath,
                               FileMode.Create,
                               FileAccess.Write,
                               FileShare.Read,
                               4096,
                               FileOptions.WriteThrough | FileOptions.SequentialScan))
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            H8BinaryWorldPagerTelemetryEntry entry = telemetryRing[i];
                            writer.Write(entry.SectorHash);
                            writer.Write(entry.Offset);
                            writer.Write(entry.TicksUtc);
                            writer.Write(entry.PayloadType);
                            writer.Write(entry.Frame);
                            writer.Write(entry.RequestId);
                            writer.Write(entry.PayloadBytes);
                            writer.Write(entry.PendingWrites);
                            writer.Write(entry.PendingReads);
                            writer.Write(entry.PageFaults);
                            writer.Write(entry.Metrics);
                            writer.Write(entry.DirectorySlot);
                            writer.Write(entry.Flags);
                            writer.Write((byte)entry.Operation);
                            writer.Write((byte)entry.Status);
                        }

                        writer.Flush();
                        stream.Flush(true);
                        if (stream.Length != expectedBytes)
                            throw new IOException("H8BinaryWorldPager black-box dump length mismatch.");
                    }
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                }

                if (!TryFlushAndValidateBlackBoxDumpFile(tempPath, expectedBytes))
                {
                    TryDeleteBlackBoxDumpTempFile(tempPath);
                    return;
                }

                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteDumpPath);
                try
                {
                    if (File.Exists(absoluteDumpPath))
                        File.Replace(tempPath, absoluteDumpPath, null, true);
                    else
                        File.Move(tempPath, absoluteDumpPath);
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                    AsyncWriteManager.InvalidateCachedReadWindows(absoluteDumpPath);
                }

                TryFlushAndValidateBlackBoxDumpFile(absoluteDumpPath, expectedBytes);
            }
            catch (IOException)
            {
                TryDeleteBlackBoxDumpTempFile(tempPath);
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteBlackBoxDumpTempFile(tempPath);
            }
            catch (NotSupportedException)
            {
                TryDeleteBlackBoxDumpTempFile(tempPath);
            }
            catch (ArgumentException)
            {
                TryDeleteBlackBoxDumpTempFile(tempPath);
            }
            catch (ObjectDisposedException)
            {
                TryDeleteBlackBoxDumpTempFile(tempPath);
            }
            catch (System.Security.SecurityException)
            {
                TryDeleteBlackBoxDumpTempFile(tempPath);
            }
        }

        private static bool TryFlushAndValidateBlackBoxDumpFile(string path, long expectedBytes)
        {
            if (string.IsNullOrEmpty(path) || expectedBytes < 0L)
                return false;

            return AsyncWriteManager.FlushCriticalSavePath(path, expectedBytes, out _);
        }

        private static void TryDeleteBlackBoxDumpTempFile(string tempPath)
        {
            try
            {
                if (string.IsNullOrEmpty(tempPath))
                    return;

                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        private struct PagerNativeState : IDisposable
        {
            public NativeArray<PageWriteCommand> WriteCommands;
            public NativeArray<PageReadCommand> ReadCommands;
            public NativeArray<PageReadResult> ReadResults;
            public NativeArray<byte> WriteArena;
            public NativeArray<byte> ReadArena;
            public NativeArray<byte> ReadSlotStates;
            public NativeArray<byte> CompressionScratch;
            public NativeArray<byte> HotStateArena;

            public bool IsReady()
            {
                return
                    WriteCommands.IsCreated &&
                    WriteCommands.Length >= WriteSlotCount &&
                    ReadCommands.IsCreated &&
                    ReadCommands.Length >= QueueCapacity &&
                    ReadResults.IsCreated &&
                    ReadResults.Length >= ReadSlotCount &&
                    WriteArena.IsCreated &&
                    WriteArena.Length >= WriteSlotCount * SectorPayloadBytes &&
                    ReadArena.IsCreated &&
                    ReadArena.Length >= ReadSlotCount * SectorPayloadBytes &&
                    ReadSlotStates.IsCreated &&
                    ReadSlotStates.Length >= ReadSlotCount &&
                    CompressionScratch.IsCreated &&
                    CompressionScratch.Length >= SectorPayloadBytes &&
                    HotStateArena.IsCreated &&
                    HotStateArena.Length >= HotStateMaxBytes;
            }

            public bool EnsureAll()
            {
                if (IsReady())
                    return true;

                Dispose();
                try
                {
                    Allocate(ref WriteCommands, WriteSlotCount, NativeArrayOptions.UninitializedMemory, nameof(WriteCommands));
                    Allocate(ref ReadCommands, QueueCapacity, NativeArrayOptions.UninitializedMemory, nameof(ReadCommands));
                    Allocate(ref ReadResults, ReadSlotCount, NativeArrayOptions.ClearMemory, nameof(ReadResults));
                    Allocate(ref WriteArena, WriteSlotCount * SectorPayloadBytes, NativeArrayOptions.UninitializedMemory, nameof(WriteArena));
                    Allocate(ref ReadArena, ReadSlotCount * SectorPayloadBytes, NativeArrayOptions.UninitializedMemory, nameof(ReadArena));
                    Allocate(ref ReadSlotStates, ReadSlotCount, NativeArrayOptions.ClearMemory, nameof(ReadSlotStates));
                    Allocate(ref CompressionScratch, SectorPayloadBytes, NativeArrayOptions.UninitializedMemory, nameof(CompressionScratch));
                    Allocate(ref HotStateArena, HotStateMaxBytes, NativeArrayOptions.ClearMemory, nameof(HotStateArena));
                    return IsReady();
                }
                catch (Exception)
                {
                    Dispose();
                    return false;
                }
            }

            public void Dispose()
            {
                Dispose(ref HotStateArena);
                Dispose(ref CompressionScratch);
                Dispose(ref ReadSlotStates);
                Dispose(ref ReadArena);
                Dispose(ref WriteArena);
                Dispose(ref ReadResults);
                Dispose(ref ReadCommands);
                Dispose(ref WriteCommands);
            }

            private static void Allocate<T>(
                ref NativeArray<T> array,
                int length,
                NativeArrayOptions options,
                string label) where T : struct
            {
                array = H8Memory.Allocate<T>(length, VaultOwner, Allocator.Persistent, options);
                if (!array.IsCreated)
                    throw new InvalidOperationException($"{nameof(H8BinaryWorldPager)} native allocation failed for {label}.");
            }

            private static void Dispose<T>(ref NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                H8Memory.Release(ref array, VaultOwner);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CacheLineInt
        {
            [FieldOffset(0)] public int Value;
        }

        private enum PagerInitializationFaultReason : byte
        {
            Unknown = 0,
            OpenStream = 1,
            OpenWriteAheadLog = 2,
            DataVaultUnavailable = 3,
            NativeStateAllocation = 4,
            VaultHandleUnavailable = 5,
            WorkerStart = 6
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
