using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core.Persistence.Paging
{
    public sealed class H8BinaryWorldPager : IDisposable
    {
        private const string NativeMemoryOwner = nameof(H8BinaryWorldPager);
        private const string DumpFileName = "Dump_DATA_MONOLITH_ARCHIVIST.bin";
        private const string WorldDataFileName = "world_data.h8bin";
        private const uint PageMagic = 0x48385047u; // H8PG
        private const ushort PageVersion = 1;
        private const int SectorHeaderBytes = 64;
        private const int SectorSizeBytes = 256 * 1024;
        private const int SectorPayloadBytes = SectorSizeBytes - SectorHeaderBytes;
        private const int MaxSectors = 8192;
        private const int WriteSlotCount = 32;
        private const int ReadSlotCount = 16;
        private const int MaxSectorsMask = MaxSectors - 1;
        private const int WriteSlotMask = WriteSlotCount - 1;
        private const int ReadSlotMask = ReadSlotCount - 1;
        private const int TelemetryCapacity = 300;
        private const int QueueCapacity = 64;
        private const int WorkerIdleSleepMilliseconds = 1;
        private const int WorkerShutdownWaitMilliseconds = 250;
        private const uint PageFlagCompressed = 1u;
        private const uint PageFlagProceduralFallback = 1u << 1;

        private NativeQueue<PageWriteCommand> _writeQueue;
        private NativeQueue<PageReadCommand> _readQueue;
        private NativeParallelHashMap<uint, PageReadResult> _readResults;
        private NativeArray<byte> _writeArena;
        private NativeArray<byte> _readArena;
        private NativeArray<byte> _readSlotStates;
        private NativeArray<byte> _compressionScratch;
        private NativeArray<PagerTelemetryEntry> _telemetryRing;
        private SpinLock _writeQueueLock;
        private SpinLock _readQueueLock;
        private SpinLock _resultLock;
        private readonly object _streamLock = new object();
        private readonly object _workerStopLock = new object();
        private FileStream _stream;
        private string _path;
        private string _dumpPath;
        private int _writeSlotCursor;
        private int _readSlotCursor;
        private int _disposeRequested;
        private int _workerRunning;
        private Thread _workerThread;
        private int _initialized;
        private int _telemetryCursor;
        private int _pendingWriteCount;
        private int _pendingReadCount;
        private int _pendingReadResultCount;
        private int _pageFaultCount;
        private int _corruptReadCount;
        private int _completedReadCount;
        private int _completedWriteCount;
        private int _droppedWriteCount;
        private int _droppedReadCount;
        private int _ioErrorCount;
        private int _initializationFault;
        private int _queueHighWatermark;
        private int _lastPayloadBytes;
        private long _lastSectorHash;
        private uint _lastPayloadType;
        private uint _lastFrame;

        public string FileName => WorldDataFileName;

        public bool IsInitialized => Volatile.Read(ref _initialized) != 0;

        public bool HasInitializationFault => Volatile.Read(ref _initializationFault) != 0;

        public void Initialize(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                absolutePath = HectonPersistentPathPolicy.CombineFile(WorldDataFileName);

            if (IsInitialized && string.Equals(_path, absolutePath, StringComparison.Ordinal))
                return;

            Dispose();

            _path = absolutePath;
            _dumpPath = ResolveDumpPath();
            Volatile.Write(ref _initializationFault, 0);

            try
            {
                HectonPersistentPathPolicy.EnsureParentDirectory(_path);
                HectonPersistentPathPolicy.EnsureParentDirectory(_dumpPath);

                // COLD ALLOC: FileStream[1] - persistent random-access async world pager handle - owner: H8BinaryWorldPager
                _stream = new FileStream(
                    _path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    SectorSizeBytes,
                    FileOptions.Asynchronous | FileOptions.RandomAccess);
            }
            catch (IOException exception)
            {
                MarkInitializationFault(exception);
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                MarkInitializationFault(exception);
                return;
            }

            AllocateNativeState();
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
            if (!IsInitialized || !payload.IsCreated || byteCount <= 0 || byteCount > payload.Length || byteCount > SectorPayloadBytes)
            {
                Interlocked.Increment(ref _droppedWriteCount);
                return false;
            }

            bool lockTaken = false;
            try
            {
                _writeQueueLock.Enter(ref lockTaken);
                int pending = Volatile.Read(ref _pendingWriteCount);
                if (pending >= WriteSlotCount || !_writeQueue.IsCreated)
                {
                    Interlocked.Increment(ref _droppedWriteCount);
                    return false;
                }

                int slot = _writeSlotCursor;
                _writeSlotCursor = (_writeSlotCursor + 1) & WriteSlotMask;
                int byteOffset = slot * SectorPayloadBytes;
                unsafe
                {
                    void* src = payload.GetUnsafeReadOnlyPtr();
                    void* dst = (byte*)_writeArena.GetUnsafePtr() + byteOffset;
                    UnsafeUtility.MemCpy(dst, src, byteCount);
                }
                _writeQueue.Enqueue(new PageWriteCommand
                {
                    SectorHash = sectorHash,
                    PayloadType = payloadType,
                    ByteOffset = byteOffset,
                    ByteCount = byteCount,
                    SourceHash = sourceHash,
                    Frame = frame
                });

                int queued = Interlocked.Increment(ref _pendingWriteCount);
                SetQueueHighWatermark(queued);
                return true;
            }
            finally
            {
                if (lockTaken)
                    _writeQueueLock.Exit(false);
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
                Interlocked.Increment(ref _droppedReadCount);
                return false;
            }

            bool lockTaken = false;
            try
            {
                _readQueueLock.Enter(ref lockTaken);
                if (Volatile.Read(ref _pendingReadCount) >= QueueCapacity || !_readQueue.IsCreated)
                {
                    Interlocked.Increment(ref _droppedReadCount);
                    return false;
                }

                _readQueue.Enqueue(new PageReadCommand
                {
                    SectorHash = sectorHash,
                    PayloadType = payloadType,
                    RequestId = requestId,
                    Frame = frame
                });

                int queued = Interlocked.Increment(ref _pendingReadCount);
                SetQueueHighWatermark(queued + Volatile.Read(ref _pendingWriteCount));
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
            if (!destination.IsCreated || ticket.RequestId == 0u || !_readResults.IsCreated)
            {
                status = H8WorldPageStatus.Rejected;
                return false;
            }

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                if (!_readResults.TryGetValue(ticket.RequestId, out PageReadResult result))
                    return false;

                status = result.Status;
                if (result.Status != H8WorldPageStatus.Ready)
                {
                    _readResults.Remove(ticket.RequestId);
                    Interlocked.Decrement(ref _pendingReadResultCount);
                    return true;
                }

                if (result.ByteCount <= 0 || result.ByteCount > destination.Length || (uint)result.SlotIndex >= (uint)ReadSlotCount)
                {
                    status = H8WorldPageStatus.Rejected;
                    bytesWritten = result.ByteCount > 0 ? result.ByteCount : 0;
                    if ((uint)result.SlotIndex < (uint)ReadSlotCount)
                        _readSlotStates[result.SlotIndex] = 0;

                    _readResults.Remove(ticket.RequestId);
                    Interlocked.Decrement(ref _pendingReadResultCount);
                    return false;
                }

                unsafe
                {
                    void* src = (byte*)_readArena.GetUnsafeReadOnlyPtr() + (result.SlotIndex * SectorPayloadBytes);
                    void* dst = destination.GetUnsafePtr();
                    UnsafeUtility.MemCpy(dst, src, result.ByteCount);
                }
                bytesWritten = result.ByteCount;
                _readSlotStates[result.SlotIndex] = 0;
                _readResults.Remove(ticket.RequestId);
                Interlocked.Decrement(ref _pendingReadResultCount);
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
            if (ticket.RequestId == 0u || !_readResults.IsCreated)
            {
                status = H8WorldPageStatus.Rejected;
                return false;
            }

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                if (!_readResults.TryGetValue(ticket.RequestId, out PageReadResult result))
                    return false;

                status = result.Status;
                byteCount = result.ByteCount;
                if ((uint)result.SlotIndex < (uint)ReadSlotCount)
                    _readSlotStates[result.SlotIndex] = 0;

                _readResults.Remove(ticket.RequestId);
                Interlocked.Decrement(ref _pendingReadResultCount);
                return true;
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        public H8WorldPagerTelemetrySnapshot GetTelemetry()
        {
            return new H8WorldPagerTelemetrySnapshot
            {
                PendingDiskWrites = Volatile.Read(ref _pendingWriteCount),
                PendingDiskReads = Volatile.Read(ref _pendingReadCount),
                PendingReadResults = Volatile.Read(ref _pendingReadResultCount),
                PageFaults = Volatile.Read(ref _pageFaultCount),
                CorruptReads = Volatile.Read(ref _corruptReadCount),
                CompletedReads = Volatile.Read(ref _completedReadCount),
                CompletedWrites = Volatile.Read(ref _completedWriteCount),
                DroppedWrites = Volatile.Read(ref _droppedWriteCount),
                DroppedReads = Volatile.Read(ref _droppedReadCount),
                IoErrors = Volatile.Read(ref _ioErrorCount),
                QueueHighWatermark = Volatile.Read(ref _queueHighWatermark),
                LastPayloadBytes = Volatile.Read(ref _lastPayloadBytes),
                LastSectorHash = Volatile.Read(ref _lastSectorHash),
                LastPayloadType = Volatile.Read(ref _lastPayloadType),
                LastFrame = Volatile.Read(ref _lastFrame)
            };
        }

        public void Flush()
        {
            FileStream stream = _stream;
            if (stream == null)
                return;

            try
            {
                lock (_streamLock)
                {
                    stream.Flush();
                }
            }
            catch
            {
                Interlocked.Increment(ref _ioErrorCount);
            }
        }

        public void Dispose()
        {
            Volatile.Write(ref _disposeRequested, 1);
            bool workerStopped = WaitForWorkerExit();

            FileStream stream = _stream;
            _stream = null;
            if (stream != null)
                DisposeStream(stream, flush: true);

            if (!workerStopped)
                workerStopped = WaitForWorkerExit();

            if (!workerStopped)
            {
                Interlocked.Increment(ref _ioErrorCount);
                Volatile.Write(ref _initialized, 0);
                Volatile.Write(ref _initializationFault, 1);
                return;
            }

            DisposeNativeState();
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _initializationFault, 0);
        }

        private void MarkInitializationFault(Exception exception)
        {
            FileStream stream = _stream;
            _stream = null;
            if (stream != null)
                DisposeStream(stream, flush: false);

            DisposeNativeState();
            Volatile.Write(ref _initialized, 0);
            Volatile.Write(ref _disposeRequested, 1);
            Volatile.Write(ref _initializationFault, 1);
            Interlocked.Increment(ref _ioErrorCount);

            Debug.LogWarning(
                "H8BinaryWorldPager disabled page IO after initialization fault: " +
                exception.GetType().Name +
                ": " +
                exception.Message);
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
                        stream.Flush();
                    stream.Dispose();
                }
            }
            catch
            {
                Interlocked.Increment(ref _ioErrorCount);
            }
        }

        private void AllocateNativeState()
        {
            if (!_writeQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<PageWriteCommand>[64] - MPSC write command staging for world pager - owner: H8BinaryWorldPager
                _writeQueue = new NativeQueue<PageWriteCommand>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(_writeQueue, QueueCapacity, NativeMemoryOwner, nameof(_writeQueue), NativeAllocationLifetime.Session);
            }

            if (!_readQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<PageReadCommand>[64] - MPSC read command staging for world pager - owner: H8BinaryWorldPager
                _readQueue = new NativeQueue<PageReadCommand>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(_readQueue, QueueCapacity, NativeMemoryOwner, nameof(_readQueue), NativeAllocationLifetime.Session);
            }

            if (!_readResults.IsCreated)
            {
                // COLD ALLOC: NativeParallelHashMap<uint, PageReadResult>[16] - completed read ticket map - owner: H8BinaryWorldPager
                _readResults = new NativeParallelHashMap<uint, PageReadResult>(ReadSlotCount, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeParallelHashMap(_readResults, NativeMemoryOwner, nameof(_readResults), NativeAllocationLifetime.Session);
            }

            if (!_writeArena.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[8386560] - fixed ring of chunk write payload slots - owner: H8BinaryWorldPager
                _writeArena = new NativeArray<byte>(WriteSlotCount * SectorPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(_writeArena, NativeMemoryOwner, nameof(_writeArena), NativeAllocationLifetime.Session);
            }

            if (!_readArena.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[4193280] - fixed read-completion payload slots - owner: H8BinaryWorldPager
                _readArena = new NativeArray<byte>(ReadSlotCount * SectorPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(_readArena, NativeMemoryOwner, nameof(_readArena), NativeAllocationLifetime.Session);
            }

            if (!_readSlotStates.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[16] - read-result slot occupancy bits - owner: H8BinaryWorldPager
                _readSlotStates = new NativeArray<byte>(ReadSlotCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_readSlotStates, NativeMemoryOwner, nameof(_readSlotStates), NativeAllocationLifetime.Session);
            }

            if (!_compressionScratch.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[262080] - background RLE compression/decompression scratch page - owner: H8BinaryWorldPager
                _compressionScratch = new NativeArray<byte>(SectorPayloadBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(_compressionScratch, NativeMemoryOwner, nameof(_compressionScratch), NativeAllocationLifetime.Session);
            }

            if (!_telemetryRing.IsCreated)
            {
                // COLD ALLOC: NativeArray<PagerTelemetryEntry>[300] - world pager black-box circular buffer - owner: H8BinaryWorldPager
                _telemetryRing = new NativeArray<PagerTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeAllocationLifetime.Session);
            }
        }

        private void DisposeNativeState()
        {
            if (_writeQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_writeQueue));
                _writeQueue.Dispose();
                _writeQueue = default;
            }

            if (_readQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_readQueue));
                _readQueue.Dispose();
                _readQueue = default;
            }

            if (_readResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, nameof(_readResults));
                _readResults.Dispose();
                _readResults = default;
            }

            DisposeNativeArray(ref _writeArena);
            DisposeNativeArray(ref _readArena);
            DisposeNativeArray(ref _readSlotStates);
            DisposeNativeArray(ref _compressionScratch);
            DisposeNativeArray(ref _telemetryRing);
            _pendingWriteCount = 0;
            _pendingReadCount = 0;
            _pendingReadResultCount = 0;
            _writeSlotCursor = 0;
            _readSlotCursor = 0;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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
            catch (Exception exception)
            {
                _workerThread = null;
                Volatile.Write(ref _workerRunning, 0);
                MarkInitializationFault(exception);
            }
        }

        private void RunWorkerLoop()
        {
            try
            {
                while (Volatile.Read(ref _disposeRequested) == 0)
                {
                    bool didWork = false;
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

                    if (!didWork)
                        Thread.Sleep(WorkerIdleSleepMilliseconds);
                }
            }
            catch
            {
                MarkWorkerFault();
            }
            finally
            {
                _workerThread = null;
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
            catch
            {
                faulted = true;
            }
            finally
            {
                Interlocked.Decrement(ref _pendingWriteCount);
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
            catch
            {
                faulted = true;
            }
            finally
            {
                Interlocked.Decrement(ref _pendingReadCount);
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
            Volatile.Write(ref _initializationFault, 1);
            Volatile.Write(ref _pendingWriteCount, 0);
            Volatile.Write(ref _pendingReadCount, 0);
            Interlocked.Increment(ref _ioErrorCount);
            DumpBlackBox();
        }

        private bool TryDequeueWrite(out PageWriteCommand command)
        {
            command = default;
            if (!_writeQueue.IsCreated)
                return false;

            bool lockTaken = false;
            try
            {
                _writeQueueLock.Enter(ref lockTaken);
                return _writeQueue.TryDequeue(out command);
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
            if (!_readQueue.IsCreated)
                return false;

            bool lockTaken = false;
            try
            {
                _readQueueLock.Enter(ref lockTaken);
                return _readQueue.TryDequeue(out command);
            }
            finally
            {
                if (lockTaken)
                    _readQueueLock.Exit(false);
            }
        }

        private void ProcessWrite(in PageWriteCommand command)
        {
            unsafe
            {
            byte* input = (byte*)_writeArena.GetUnsafeReadOnlyPtr() + command.ByteOffset;
            byte* stored = input;
            int storedBytes = command.ByteCount;
            uint flags = 0u;

            if (TryCompressRle(input, command.ByteCount, (byte*)_compressionScratch.GetUnsafePtr(), _compressionScratch.Length, out int compressedBytes))
            {
                stored = (byte*)_compressionScratch.GetUnsafeReadOnlyPtr();
                storedBytes = compressedBytes;
                flags |= PageFlagCompressed;
            }

            uint crc = ComputeCrc32(input, command.ByteCount);
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
                    crc,
                    command.Frame,
                    command.SourceHash,
                    0u);
            }

            try
            {
                FileStream stream = _stream;
                if (stream == null)
                    return;

                lock (_streamLock)
                {
                    stream.Position = offset;
                    stream.Write(header);
                    stream.Write(new ReadOnlySpan<byte>(stored, storedBytes));
                }

                Interlocked.Increment(ref _completedWriteCount);
                PublishLast(command.SectorHash, command.PayloadType, command.ByteCount, command.Frame);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.Ready, flags);
            }
            catch
            {
                Interlocked.Increment(ref _ioErrorCount);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, 0u, command.ByteCount, PagerTelemetryOperation.Write, H8WorldPageStatus.IOError, flags);
            }
            }
        }

        private void ProcessRead(in PageReadCommand command)
        {
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
                    Interlocked.Increment(ref _pageFaultCount);
                    RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadMiss, H8WorldPageStatus.Missing, PageFlagProceduralFallback);
                    return;
                }

                lock (_streamLock)
                {
                    stream.Position = offset;
                    if (!ReadExact(stream, header))
                    {
                        CommitReadResult(command, H8WorldPageStatus.Missing, -1, 0);
                        Interlocked.Increment(ref _pageFaultCount);
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
                                out uint expectedCrc))
                        {
                            if (HeaderIsEmpty(headerPtr) || HeaderIsDifferentPage(headerPtr, command.SectorHash, command.PayloadType))
                            {
                                CommitReadResult(command, H8WorldPageStatus.Missing, -1, 0);
                                Interlocked.Increment(ref _pageFaultCount);
                                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadMiss, H8WorldPageStatus.Missing, PageFlagProceduralFallback);
                                return;
                            }

                            status = H8WorldPageStatus.Corrupt;
                            CommitReadResult(command, status, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount);
                            Interlocked.Increment(ref _corruptReadCount);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        if (rawBytes <= 0 || rawBytes > SectorPayloadBytes || storedBytes <= 0 || storedBytes > SectorPayloadBytes)
                        {
                            status = H8WorldPageStatus.Corrupt;
                            CommitReadResult(command, status, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount);
                            Interlocked.Increment(ref _corruptReadCount);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, status, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        if (!TryAcquireReadSlot(out resultSlot))
                        {
                            CommitReadResult(command, H8WorldPageStatus.Rejected, -1, 0);
                            Interlocked.Increment(ref _droppedReadCount);
                            return;
                        }

                        byte* resultPtr = (byte*)_readArena.GetUnsafePtr() + (resultSlot * SectorPayloadBytes);
                        if ((flags & PageFlagCompressed) != 0u)
                        {
                            if (!ReadExact(stream, new Span<byte>(_compressionScratch.GetUnsafePtr(), storedBytes)) ||
                                !TryDecompressRle((byte*)_compressionScratch.GetUnsafeReadOnlyPtr(), storedBytes, resultPtr, rawBytes))
                            {
                                ReleaseReadSlot(resultSlot);
                                CommitReadResult(command, H8WorldPageStatus.Corrupt, -1, 0);
                                Interlocked.Increment(ref _pageFaultCount);
                                Interlocked.Increment(ref _corruptReadCount);
                                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.Corrupt, PageFlagProceduralFallback);
                                DumpBlackBox();
                                return;
                            }
                        }
                        else if (!ReadExact(stream, new Span<byte>(resultPtr, rawBytes)))
                        {
                            ReleaseReadSlot(resultSlot);
                            CommitReadResult(command, H8WorldPageStatus.Corrupt, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount);
                            Interlocked.Increment(ref _corruptReadCount);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, 0, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.Corrupt, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        uint actualCrc = ComputeCrc32(resultPtr, rawBytes);
                        if (actualCrc != expectedCrc)
                        {
                            ReleaseReadSlot(resultSlot);
                            CommitReadResult(command, H8WorldPageStatus.Corrupt, -1, 0);
                            Interlocked.Increment(ref _pageFaultCount);
                            Interlocked.Increment(ref _corruptReadCount);
                            RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, rawBytes, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.Corrupt, PageFlagProceduralFallback);
                            DumpBlackBox();
                            return;
                        }

                        byteCount = rawBytes;
                    }
                }

                CommitReadResult(command, H8WorldPageStatus.Ready, resultSlot, byteCount);
                Interlocked.Increment(ref _completedReadCount);
                PublishLast(command.SectorHash, command.PayloadType, byteCount, command.Frame);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadReady, H8WorldPageStatus.Ready, flags);
            }
            catch
            {
                if (resultSlot >= 0)
                    ReleaseReadSlot(resultSlot);

                CommitReadResult(command, H8WorldPageStatus.IOError, -1, 0);
                Interlocked.Increment(ref _ioErrorCount);
                RecordTelemetry(command.SectorHash, offset, command.PayloadType, command.Frame, command.RequestId, byteCount, PagerTelemetryOperation.ReadCorrupt, H8WorldPageStatus.IOError, flags);
            }
            }
        }

        private bool TryAcquireReadSlot(out int slot)
        {
            slot = -1;
            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                if (Volatile.Read(ref _pendingReadResultCount) >= ReadSlotCount)
                    return false;

                for (int i = 0; i < ReadSlotCount; i++)
                {
                    int candidate = (_readSlotCursor + i) & ReadSlotMask;
                    if (_readSlotStates[candidate] != 0)
                        continue;

                    _readSlotStates[candidate] = 1;
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

            bool lockTaken = false;
            try
            {
                _resultLock.Enter(ref lockTaken);
                _readSlotStates[slot] = 0;
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
            }
        }

        private void CommitReadResult(in PageReadCommand command, H8WorldPageStatus status, int slot, int byteCount)
        {
            if (!_readResults.IsCreated)
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

                if (_readResults.TryGetValue(command.RequestId, out PageReadResult existing))
                {
                    if ((uint)existing.SlotIndex < (uint)ReadSlotCount)
                        _readSlotStates[existing.SlotIndex] = 0;

                    _readResults[command.RequestId] = result;
                    return;
                }

                if (_readResults.TryAdd(command.RequestId, result))
                {
                    Interlocked.Increment(ref _pendingReadResultCount);
                    return;
                }

                if ((uint)slot < (uint)ReadSlotCount)
                    _readSlotStates[slot] = 0;
                Interlocked.Increment(ref _droppedReadCount);
            }
            finally
            {
                if (lockTaken)
                    _resultLock.Exit(false);
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

        private static long ResolveOffset(long sectorHash)
        {
            ulong normalized = unchecked((ulong)sectorHash);
            ulong sector = normalized & (ulong)MaxSectorsMask;
            return (long)(sector * SectorSizeBytes);
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

        private static unsafe uint ComputeCrc32(byte* data, int byteCount)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < byteCount; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

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

        private static unsafe void WriteUInt(byte* ptr, int offset, uint value) => *(uint*)(ptr + offset) = value;
        private static unsafe void WriteUShort(byte* ptr, int offset, ushort value) => *(ushort*)(ptr + offset) = value;
        private static unsafe void WriteInt(byte* ptr, int offset, int value) => *(int*)(ptr + offset) = value;
        private static unsafe void WriteLong(byte* ptr, int offset, long value) => *(long*)(ptr + offset) = value;
        private static unsafe uint ReadUInt(byte* ptr, int offset) => *(uint*)(ptr + offset);
        private static unsafe ushort ReadUShort(byte* ptr, int offset) => *(ushort*)(ptr + offset);
        private static unsafe int ReadInt(byte* ptr, int offset) => *(int*)(ptr + offset);
        private static unsafe long ReadLong(byte* ptr, int offset) => *(long*)(ptr + offset);

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
                current = Volatile.Read(ref _queueHighWatermark);
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref _queueHighWatermark, value, current) != current);
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
            uint flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            int index = Interlocked.Increment(ref _telemetryCursor);
            if (index == int.MaxValue)
                Interlocked.Exchange(ref _telemetryCursor, 0);

            int slot = (index & int.MaxValue) % TelemetryCapacity;
            _telemetryRing[slot] = new PagerTelemetryEntry
            {
                SectorHash = sectorHash,
                Offset = offset,
                Frame = frame,
                RequestId = requestId,
                PayloadType = payloadType,
                PendingWrites = Volatile.Read(ref _pendingWriteCount),
                PendingReads = Volatile.Read(ref _pendingReadCount),
                PageFaults = Volatile.Read(ref _pageFaultCount),
                PayloadBytes = payloadBytes,
                Operation = operation,
                Status = status,
                Flags = unchecked((ushort)flags),
                TicksUtc = DateTime.UtcNow.Ticks
            };
        }

        private string ResolveDumpPath()
        {
            string projectRoot = Application.dataPath;
            if (string.IsNullOrEmpty(projectRoot))
                projectRoot = HectonPersistentPathPolicy.RootPath;
            else
                projectRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));

            return Path.Combine(projectRoot, "Docs", "AgentLogs", DumpFileName);
        }

        private unsafe void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated || string.IsNullOrEmpty(_dumpPath))
                return;

            try
            {
                HectonPersistentPathPolicy.EnsureParentDirectory(_dumpPath);
                using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
                Span<byte> header = stackalloc byte[16];
                fixed (byte* headerPtr = header)
                {
                    WriteUInt(headerPtr, 0, 0x444D4838u); // H8MD
                    WriteInt(headerPtr, 4, TelemetryCapacity);
                    WriteInt(headerPtr, 8, UnsafeUtility.SizeOf<PagerTelemetryEntry>());
                    WriteInt(headerPtr, 12, Volatile.Read(ref _telemetryCursor));
                }

                stream.Write(header);
                stream.Write(new ReadOnlySpan<byte>(_telemetryRing.GetUnsafeReadOnlyPtr(), _telemetryRing.Length * UnsafeUtility.SizeOf<PagerTelemetryEntry>()));
            }
            catch
            {
                Interlocked.Increment(ref _ioErrorCount);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PageWriteCommand
        {
            public long SectorHash;
            public uint PayloadType;
            public int ByteOffset;
            public int ByteCount;
            public uint SourceHash;
            public uint Frame;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PageReadCommand
        {
            public long SectorHash;
            public uint PayloadType;
            public uint RequestId;
            public uint Frame;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PageReadResult
        {
            public long SectorHash;
            public uint PayloadType;
            public uint RequestId;
            public int SlotIndex;
            public int ByteCount;
            public H8WorldPageStatus Status;
        }

        private enum PagerTelemetryOperation : byte
        {
            Write = 1,
            ReadReady = 2,
            ReadMiss = 3,
            ReadCorrupt = 4
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct PagerTelemetryEntry
        {
            public long SectorHash;
            public long Offset;
            public uint Frame;
            public uint RequestId;
            public uint PayloadType;
            public int PendingWrites;
            public int PendingReads;
            public int PageFaults;
            public int PayloadBytes;
            public PagerTelemetryOperation Operation;
            public H8WorldPageStatus Status;
            public ushort Flags;
            public long TicksUtc;
            public long Reserved;
        }
    }
}
