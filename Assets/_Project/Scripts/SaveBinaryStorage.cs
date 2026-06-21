using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.Quest;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    public static unsafe class AsyncWriteManager
    {
        internal ref struct ReadOnlyMapping
        {
            public NativeArray<byte> Bytes;
            public IntPtr View;
            public long Length;
        }

        private readonly struct NativeWindowsWriteSegmentArgs
        {
            public readonly IntPtr Handle;
            public readonly void* Source;
            public readonly int ByteCount;
            public readonly bool PaceWrites;

            public NativeWindowsWriteSegmentArgs(IntPtr handle, void* source, int byteCount, bool paceWrites)
            {
                Handle = handle;
                Source = source;
                ByteCount = byteCount;
                PaceWrites = paceWrites;
            }
        }

        private readonly struct NativeUnixWriteSegmentArgs
        {
            public readonly int Fd;
            public readonly void* Source;
            public readonly int ByteCount;
            public readonly bool PaceWrites;

            public NativeUnixWriteSegmentArgs(int fd, void* source, int byteCount, bool paceWrites)
            {
                Fd = fd;
                Source = source;
                ByteCount = byteCount;
                PaceWrites = paceWrites;
            }
        }

        private const long DiskFlushThrottleBytesPerSecond = 10L * 1024L * 1024L;
        private const int DiskFlushQueueCapacity = 32;
        private const int ReadWindowCount = 4;
        private const int ReadWindowSizeBytes = 1024 * 1024;
        private const int ReadWindowPrefetchThresholdBytes = 256 * 1024;
        private const int ReadPrefetchQueueCapacity = 16;
        private const int OsAllocationGranularityBytes = 64 * 1024;
        private const int NativeReadChunkBytes = 1024 * 1024;
        private const int NativeWriteThreadYieldPageBytes = OsAllocationGranularityBytes;
        private const int NativeWriteChunkBytes = NativeWriteThreadYieldPageBytes;
        private const int NativeWritePagePaceMilliseconds = 1;
        private const string NativeMemoryOwner = nameof(AsyncWriteManager);
        private const string NativeMemoryRegistrationFailureMessage = "NativeMemorySentinel registration failed for AsyncWriteManager buffer.";
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint NativeGenericRead = 0x80000000u;
        private const uint NativeGenericWrite = 0x40000000u;
        private const uint NativeFileShareRead = 0x00000001u;
        private const uint NativeFileShareReadWriteDelete = 0x00000001u | 0x00000002u | 0x00000004u;
        private const uint NativeCreateAlways = 2u;
        private const uint NativeOpenExisting = 3u;
        private const uint NativeFileBegin = 0u;
        private const uint NativeFileAttributeNormal = 0x00000080u;
        private const uint NativeFileFlagBackupSemantics = 0x02000000u;
        private static readonly IntPtr NativeInvalidHandleValue = new IntPtr(-1);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        private const int NativeUnixPathCapacity = 4096;
        private const int NativeUnixOpenReadOnly = 0;
        private const int NativeUnixOpenWriteOnly = 1;
        private const int NativeUnixCreateModeOwnerReadWrite = 384;
        private const int NativeUnixSeekEnd = 2;
        private const int NativeUnixSeekSet = 0;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const int NativeUnixOpenCreate = 0x0200;
        private const int NativeUnixOpenTruncate = 0x0400;
#else
        private const int NativeUnixOpenCreate = 0x0040;
        private const int NativeUnixOpenTruncate = 0x0200;
#endif
        private unsafe struct NativeUnixPathBuffer
        {
            public fixed byte Bytes[NativeUnixPathCapacity];
        }
#endif

        private static readonly object s_flushLock = new object();
        private static readonly AutoResetEvent s_flushSignal = new AutoResetEvent(false);
        private static readonly DiskFlushRequest[] s_flushQueue = new DiskFlushRequest[DiskFlushQueueCapacity];
        private static int s_flushReadIndex;
        private static int s_flushWriteIndex;
        private static int s_flushCount;
        private static int s_flushThreadStarted;

        private static readonly object s_readWindowLock = new object();
        private static readonly CachedReadWindow[] s_readWindows = new CachedReadWindow[ReadWindowCount];
        private static int s_readWindowClock;
        private static int s_readCacheInvalidationGeneration;
        private static readonly object s_readPrefetchLock = new object();
        private static readonly AutoResetEvent s_readPrefetchSignal = new AutoResetEvent(false);
        private static readonly ReadPrefetchRequest[] s_readPrefetchQueue = new ReadPrefetchRequest[ReadPrefetchQueueCapacity];
        private static int s_readPrefetchReadIndex;
        private static int s_readPrefetchWriteIndex;
        private static int s_readPrefetchCount;
        private static int s_readPrefetchWorkerStarted;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        private static readonly object s_nativeUnixPathLock = new object();
        private static NativeUnixPathBuffer s_nativeUnixPathUtf8;
#endif

        private struct DiskFlushRequest
        {
            public string AbsolutePath;
            public long ByteCount;
        }

        private struct ReadPrefetchRequest
        {
            public string AbsolutePath;
            public long WindowOffset;
            public long FileLength;
            public int InvalidationGeneration;
        }

        private readonly struct NativeWriteResult
        {
            public readonly bool Success;
            public readonly string Error;

            public NativeWriteResult(bool success, string error)
            {
                Success = success;
                Error = error ?? string.Empty;
            }
        }

        private struct CachedReadWindow
        {
            public string AbsolutePath;
            public NativeArray<byte> Bytes;
            public int BytesSentinelId;
            public long WindowOffset;
            public long WindowLength;
            public long FileLength;
            public int LastUse;

            public bool IsCreated => Bytes.IsCreated;
        }

        internal static bool QueueThrottledFlush(string absolutePath, long byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Flush path is empty.";
                return false;
            }

            if (!IsNativeFlushSupported())
            {
                error = "Native flush is unsupported on this platform.";
                return false;
            }

            if (!EnsureFlushThread())
            {
                ThrottleFlush(byteCount);
                if (!TryFlushPath(absolutePath))
                {
                    error = "Flush worker is unavailable and immediate flush failed.";
                    return false;
                }

                return true;
            }

            bool flushImmediately = false;
            lock (s_flushLock)
            {
                if (s_flushCount == DiskFlushQueueCapacity)
                {
                    flushImmediately = true;
                }
                else
                {
                    s_flushQueue[s_flushWriteIndex] = new DiskFlushRequest
                    {
                        AbsolutePath = absolutePath,
                        ByteCount = byteCount > 0L ? byteCount : 0L
                    };
                    s_flushWriteIndex = (s_flushWriteIndex + 1) % DiskFlushQueueCapacity;
                    s_flushCount++;
                }
            }

            if (flushImmediately)
            {
                s_flushSignal.Set();
                ThrottleFlush(byteCount);
                if (!TryFlushPath(absolutePath))
                {
                    error = "Flush queue is full and immediate flush failed.";
                    return false;
                }

                return true;
            }

            s_flushSignal.Set();
            return true;
        }

        internal static bool FlushCriticalSavePath(string absolutePath, long byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Critical save flush path is empty.";
                return false;
            }

            if (byteCount < 0L)
            {
                error = "Critical save flush byte count is invalid.";
                return false;
            }

            if (!IsNativeFlushSupported())
            {
                error = "Critical save flush is unsupported on this platform.";
                return false;
            }

            if (!TryGetFileLength(absolutePath, out long currentBytes, out string lengthError))
            {
                error = string.IsNullOrEmpty(lengthError)
                    ? "Critical save flush file length could not be resolved."
                    : lengthError;
                return false;
            }

            if (currentBytes != byteCount)
            {
                error = "Critical save flush byte count does not match file length.";
                return false;
            }

            if (!TryFlushPathAndParentDirectory(absolutePath, out error))
                return false;

            if (!TryGetFileLength(absolutePath, out long flushedBytes, out lengthError))
            {
                error = string.IsNullOrEmpty(lengthError)
                    ? "Critical save flushed file length could not be resolved."
                    : lengthError;
                return false;
            }

            if (flushedBytes != byteCount)
            {
                error = "Critical save file length changed during flush.";
                return false;
            }

            return true;
        }

        internal static void InvalidateCachedReadWindows(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return;

            lock (s_readWindowLock)
            {
                unchecked
                {
                    s_readCacheInvalidationGeneration++;
                }

                for (int i = 0; i < s_readWindows.Length; i++)
                {
                    if (s_readWindows[i].IsCreated &&
                        string.Equals(s_readWindows[i].AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
                    {
                        DisposeCachedReadWindow(ref s_readWindows[i]);
                    }
                }
            }

            lock (s_readPrefetchLock)
            {
                for (int i = 0; i < s_readPrefetchCount; i++)
                {
                    int queueIndex = (s_readPrefetchReadIndex + i) % ReadPrefetchQueueCapacity;
                    if (string.Equals(s_readPrefetchQueue[queueIndex].AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
                        s_readPrefetchQueue[queueIndex] = default;
                }
            }
        }

        internal static bool TryCopyFromCachedReadWindow(
            string absolutePath,
            long byteOffset,
            void* destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || destination == null || byteOffset < 0L || byteCount < 0)
            {
                error = "Cached file read request is invalid.";
                return false;
            }

            if (byteCount == 0)
                return true;

            if (!TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength <= 0L || byteOffset > fileLength || byteCount > fileLength - byteOffset)
            {
                error = "Cached file read range exceeds file length.";
                return false;
            }

            lock (s_readWindowLock)
            {
                byte* destinationBytes = (byte*)destination;
                long sourceCursor = byteOffset;
                int destinationCursor = 0;
                int remaining = byteCount;
                while (remaining > 0)
                {
                    int slotIndex = AcquireCachedReadWindowLocked(absolutePath, sourceCursor, 1, fileLength, out error);
                    if (slotIndex < 0)
                        return false;

                    CachedReadWindow window = s_readWindows[slotIndex];
                    long sourceOffset = sourceCursor - window.WindowOffset;
                    if (sourceOffset < 0L || sourceOffset > int.MaxValue)
                    {
                        error = "Cached file read offset exceeds the supported range.";
                        return false;
                    }

                    long availableWindowBytes = window.WindowLength - sourceOffset;
                    int chunkBytes = remaining < availableWindowBytes ? remaining : (int)availableWindowBytes;
                    if (chunkBytes <= 0)
                    {
                        error = "Cached file read window produced an empty copy span.";
                        return false;
                    }

                    NativeArray<byte> windowBytes = window.Bytes;
                    if (!windowBytes.IsCreated ||
                        sourceOffset > windowBytes.Length ||
                        chunkBytes > windowBytes.Length - (int)sourceOffset)
                    {
                        error = "Cached file read window byte span is invalid.";
                        return false;
                    }

                    byte* windowPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(windowBytes);
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationBytes + destinationCursor, byteCount - destinationCursor, windowPtr + (int)sourceOffset, chunkBytes))
                    {
                        error = "Cached file read copy exceeded destination bounds.";
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(AsyncWriteManager));
                        return false;
                    }

                    sourceCursor += chunkBytes;
                    destinationCursor += chunkBytes;
                    remaining -= chunkBytes;
                }

                return true;
            }
        }

        internal static bool TryUploadCachedReadWindowToGraphicsBuffer<T>(
            string absolutePath,
            long byteOffset,
            int elementCount,
            GraphicsBuffer destination,
            out string error)
            where T : struct
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || destination == null || byteOffset < 0L || elementCount < 0)
            {
                error = "Cached file GPU upload request is invalid.";
                return false;
            }

            int safeCount = math.min(elementCount, destination.count);
            if (safeCount <= 0)
                return true;

            int elementSize = UnsafeUtility.SizeOf<T>();
            long byteCountLong = (long)safeCount * elementSize;
            if (byteCountLong > int.MaxValue)
            {
                error = "Cached file GPU upload exceeds the supported range.";
                return false;
            }

            if (!TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength <= 0L || byteOffset > fileLength || byteCountLong > fileLength - byteOffset)
            {
                error = "Cached file GPU upload range exceeds file length.";
                return false;
            }

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            try
            {
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long destinationBytes = (long)mapped.Length * elementSize;
                lock (s_readWindowLock)
                {
                    long sourceCursor = byteOffset;
                    long destinationCursor = 0L;
                    long remaining = byteCountLong;
                    while (remaining > 0L)
                    {
                        int slotIndex = AcquireCachedReadWindowLocked(absolutePath, sourceCursor, 1, fileLength, out error);
                        if (slotIndex < 0)
                            return false;

                        CachedReadWindow window = s_readWindows[slotIndex];
                        long sourceOffset = sourceCursor - window.WindowOffset;
                        if (sourceOffset < 0L || sourceOffset > int.MaxValue)
                        {
                            error = "Cached file GPU upload offset exceeds the supported range.";
                            return false;
                        }

                        long availableWindowBytes = window.WindowLength - sourceOffset;
                        long chunkBytesLong = remaining < availableWindowBytes ? remaining : availableWindowBytes;
                        if (chunkBytesLong <= 0L || chunkBytesLong > int.MaxValue)
                        {
                            error = "Cached file GPU upload window produced an invalid copy span.";
                            return false;
                        }

                        NativeArray<byte> windowBytes = window.Bytes;
                        if (!windowBytes.IsCreated ||
                            sourceOffset > windowBytes.Length ||
                            chunkBytesLong > windowBytes.Length - sourceOffset)
                        {
                            error = "Cached file GPU upload byte span is invalid.";
                            return false;
                        }

                        byte* windowPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(windowBytes);
                        if (!UnsafeMemoryCopyGuard.TryMemCpy((byte*)destinationPtr + destinationCursor, destinationBytes - destinationCursor, windowPtr + (int)sourceOffset, chunkBytesLong))
                        {
                            error = "Cached file GPU upload copy exceeded destination bounds.";
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(AsyncWriteManager));
                            return false;
                        }

                        sourceCursor += chunkBytesLong;
                        destinationCursor += chunkBytesLong;
                        remaining -= chunkBytesLong;
                    }
                }
                return true;
            }
            finally
            {
                destination.UnlockBufferAfterWrite<T>(safeCount);
            }
        }

        private static bool EnsureFlushThread()
        {
            if (Interlocked.CompareExchange(ref s_flushThreadStarted, 1, 0) != 0)
                return true;

            try
            {
                Thread thread = new Thread(FlushWorkerLoop)
                {
                    IsBackground = true,
                    Name = "H8.SaveFlushQueue"
                };
                thread.Start();
                return true;
            }
            catch
            {
                Volatile.Write(ref s_flushThreadStarted, 0);
                return false;
            }
        }

        private static void EnsureReadPrefetchWorker()
        {
            if (Interlocked.CompareExchange(ref s_readPrefetchWorkerStarted, 1, 0) != 0)
                return;

            try
            {
                Thread thread = new Thread(ReadPrefetchWorkerLoop)
                {
                    IsBackground = true,
                    Name = "H8.FileReadPrefetch"
                };
                thread.Start();
            }
            catch
            {
                Volatile.Write(ref s_readPrefetchWorkerStarted, 0);
            }
        }

        private static void FlushWorkerLoop()
        {
            while (true)
            {
                if (!TryDequeueFlush(out DiskFlushRequest request))
                {
                    s_flushSignal.WaitOne();
                    continue;
                }

                ThrottleFlush(request.ByteCount);
                _ = TryFlushPath(request.AbsolutePath);
            }
        }

        private static bool TryDequeueFlush(out DiskFlushRequest request)
        {
            lock (s_flushLock)
            {
                if (s_flushCount <= 0)
                {
                    request = default;
                    return false;
                }

                request = s_flushQueue[s_flushReadIndex];
                s_flushQueue[s_flushReadIndex] = default;
                s_flushReadIndex = (s_flushReadIndex + 1) % DiskFlushQueueCapacity;
                s_flushCount--;
                return true;
            }
        }

        private static void ThrottleFlush(long byteCount)
        {
            if (byteCount <= 0L)
                return;

            long delayMs = (byteCount * 1000L) / DiskFlushThrottleBytesPerSecond;
            if (delayMs <= 0L)
                return;

            Thread.Sleep(delayMs > int.MaxValue ? int.MaxValue : (int)delayMs);
        }

        private static bool TryFlushPath(string absolutePath)
        {
            try
            {
                if (string.IsNullOrEmpty(absolutePath))
                    return false;

                return TryFlushPathNative(absolutePath);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFlushPathAndParentDirectory(string absolutePath, out string error)
        {
            error = string.Empty;
            if (!TryFlushPath(absolutePath))
            {
                error = "Critical save file flush failed.";
                return false;
            }

            if (!TryFlushParentDirectory(absolutePath))
            {
                error = "Critical save directory flush failed.";
                return false;
            }

            return true;
        }

        private static bool TryFlushParentDirectory(string absolutePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrEmpty(directory))
                    return false;

                return TryFlushParentDirectoryNative(directory);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsNativeFlushSupported()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }

        private static void ReadPrefetchWorkerLoop()
        {
            while (true)
            {
                if (!TryDequeueReadPrefetch(out ReadPrefetchRequest request))
                {
                    s_readPrefetchSignal.WaitOne();
                    continue;
                }

                if (string.IsNullOrEmpty(request.AbsolutePath) ||
                    request.FileLength <= 0L ||
                    request.WindowOffset < 0L ||
                    request.WindowOffset >= request.FileLength)
                {
                    continue;
                }

                lock (s_readWindowLock)
                {
                    if (request.InvalidationGeneration != s_readCacheInvalidationGeneration)
                        continue;

                    if (FindCachedReadWindowLocked(request.AbsolutePath, request.WindowOffset, 1, request.FileLength) >= 0)
                        continue;
                }

                if (!TryCreateCachedReadWindow(request.AbsolutePath, request.WindowOffset, 1, request.FileLength, out CachedReadWindow prefetchedWindow, out _))
                    continue;

                lock (s_readWindowLock)
                {
                    if (request.InvalidationGeneration != s_readCacheInvalidationGeneration)
                    {
                        DisposeCachedReadWindow(ref prefetchedWindow);
                        continue;
                    }

                    if (FindCachedReadWindowLocked(request.AbsolutePath, request.WindowOffset, 1, request.FileLength) >= 0)
                    {
                        DisposeCachedReadWindow(ref prefetchedWindow);
                        continue;
                    }

                    InsertCachedReadWindowLocked(ref prefetchedWindow);
                }
            }
        }

        private static bool TryDequeueReadPrefetch(out ReadPrefetchRequest request)
        {
            lock (s_readPrefetchLock)
            {
                if (s_readPrefetchCount <= 0)
                {
                    request = default;
                    return false;
                }

                request = s_readPrefetchQueue[s_readPrefetchReadIndex];
                s_readPrefetchQueue[s_readPrefetchReadIndex] = default;
                s_readPrefetchReadIndex = (s_readPrefetchReadIndex + 1) % ReadPrefetchQueueCapacity;
                s_readPrefetchCount--;
                return true;
            }
        }

        private static int AcquireCachedReadWindowLocked(string absolutePath, long byteOffset, int byteCount, long fileLength, out string error)
        {
            error = string.Empty;
            if (byteCount < 0 || byteOffset < 0L)
            {
                error = "Cached file read range is invalid.";
                return -1;
            }

            if (fileLength <= 0L || byteOffset > fileLength || byteCount > fileLength - byteOffset)
            {
                error = "Cached file read range exceeds file length.";
                return -1;
            }

            int existingSlotIndex = FindCachedReadWindowLocked(absolutePath, byteOffset, byteCount, fileLength);
            if (existingSlotIndex >= 0)
            {
                CachedReadWindow existingWindow = s_readWindows[existingSlotIndex];
                QueuePredictiveReadWindowLocked(in existingWindow, byteOffset, byteCount);
                return existingSlotIndex;
            }

            int mappedSlotIndex = MapCachedReadWindowLocked(absolutePath, byteOffset, byteCount, fileLength, out _, out error);
            if (mappedSlotIndex >= 0)
                QueuePredictiveReadWindowLocked(in s_readWindows[mappedSlotIndex], byteOffset, byteCount);

            return mappedSlotIndex;
        }

        private static int FindCachedReadWindowLocked(string absolutePath, long byteOffset, int byteCount, long fileLength)
        {
            for (int i = 0; i < s_readWindows.Length; i++)
            {
                CachedReadWindow candidate = s_readWindows[i];
                if (!candidate.IsCreated ||
                    !string.Equals(candidate.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (candidate.FileLength != fileLength)
                {
                    DisposeCachedReadWindow(ref s_readWindows[i]);
                    continue;
                }

                if (byteOffset >= candidate.WindowOffset &&
                    byteOffset + byteCount <= candidate.WindowOffset + candidate.WindowLength)
                {
                    candidate.LastUse = ++s_readWindowClock;
                    s_readWindows[i] = candidate;
                    return i;
                }
            }

            return -1;
        }

        private static int MapCachedReadWindowLocked(
            string absolutePath,
            long byteOffset,
            int byteCount,
            long fileLength,
            out long mappedWindowOffset,
            out string error)
        {
            mappedWindowOffset = 0L;
            error = string.Empty;
            if (fileLength <= 0L || byteOffset < 0L || byteOffset > fileLength || byteCount < 0 || byteCount > fileLength - byteOffset)
            {
                error = "Cached file window range exceeds file length.";
                return -1;
            }

            if (!TryCreateCachedReadWindow(absolutePath, byteOffset, byteCount, fileLength, out CachedReadWindow mappedWindow, out error))
                return -1;

            mappedWindowOffset = mappedWindow.WindowOffset;
            return InsertCachedReadWindowLocked(ref mappedWindow);
        }

        private static bool TryCreateCachedReadWindow(
            string absolutePath,
            long byteOffset,
            int byteCount,
            long fileLength,
            out CachedReadWindow window,
            out string error)
        {
            window = default;
            error = string.Empty;
            if (fileLength <= 0L || byteOffset < 0L || byteOffset > fileLength || byteCount < 0 || byteCount > fileLength - byteOffset)
            {
                error = "Cached file window range exceeds file length.";
                return false;
            }

            long windowOffset = AlignReadWindowOffset(byteOffset);
            long remainingFileLength = fileLength - windowOffset;
            long windowLength = remainingFileLength < ReadWindowSizeBytes ? remainingFileLength : ReadWindowSizeBytes;
            if (windowLength <= 0L)
            {
                error = "Cached file window is empty.";
                return false;
            }

            if (windowLength > int.MaxValue)
            {
                error = "Cached file read window exceeds the supported native buffer range.";
                return false;
            }

            NativeArray<byte> windowBytes = default;
            int windowBytesSentinelId = 0;
            bool transferredWindowBytes = false;
            try
            {
                // COLD ALLOC: NativeArray<byte>[windowLength] - cached save read window - owner: AsyncWriteManager
                windowBytes = AllocateCachedReadWindowBytes((int)windowLength, out windowBytesSentinelId);
                byte* windowPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(windowBytes);
                if (!TryReadAbsoluteFileRangeToNativeBuffer(absolutePath, windowOffset, windowPtr, (int)windowLength, out error))
                    return false;

                window = new CachedReadWindow
                {
                    AbsolutePath = absolutePath,
                    Bytes = windowBytes,
                    BytesSentinelId = windowBytesSentinelId,
                    WindowOffset = windowOffset,
                    WindowLength = windowLength,
                    FileLength = fileLength,
                    LastUse = 0
                };

                transferredWindowBytes = true;
                return true;
            }
            catch (Exception)
            {
                error = "Cached file read-window load failed.";
                return false;
            }
            finally
            {
                if (!transferredWindowBytes && (windowBytes.IsCreated || windowBytesSentinelId > 0))
                    DisposeCachedReadWindowBytes(ref windowBytes, ref windowBytesSentinelId);
            }
        }

        private static int InsertCachedReadWindowLocked(ref CachedReadWindow window)
        {
            int slotIndex = SelectCachedReadWindowSlotLocked();
            DisposeCachedReadWindow(ref s_readWindows[slotIndex]);
            window.LastUse = ++s_readWindowClock;
            s_readWindows[slotIndex] = window;
            window = default;
            return slotIndex;
        }

        private static long AlignReadWindowOffset(long byteOffset)
        {
            long alignedToOs = (byteOffset / OsAllocationGranularityBytes) * OsAllocationGranularityBytes;
            return (alignedToOs / ReadWindowSizeBytes) * ReadWindowSizeBytes;
        }

        private static void QueuePredictiveReadWindowLocked(in CachedReadWindow window, long byteOffset, int byteCount)
        {
            if (!window.IsCreated || window.FileLength <= 0L || byteCount <= 0)
                return;

            long requestEnd = byteOffset + byteCount;
            long windowEnd = window.WindowOffset + window.WindowLength;
            if (windowEnd >= window.FileLength ||
                requestEnd < windowEnd - ReadWindowPrefetchThresholdBytes)
            {
                return;
            }

            long nextWindowOffset = AlignReadWindowOffset(windowEnd);
            if (nextWindowOffset <= window.WindowOffset)
                nextWindowOffset = window.WindowOffset + ReadWindowSizeBytes;

            if (nextWindowOffset >= window.FileLength ||
                FindCachedReadWindowLocked(window.AbsolutePath, nextWindowOffset, 1, window.FileLength) >= 0)
            {
                return;
            }

            EnsureReadPrefetchWorker();
            lock (s_readPrefetchLock)
            {
                for (int i = 0; i < s_readPrefetchCount; i++)
                {
                    int queueIndex = (s_readPrefetchReadIndex + i) % ReadPrefetchQueueCapacity;
                    ReadPrefetchRequest queued = s_readPrefetchQueue[queueIndex];
                    if (queued.WindowOffset == nextWindowOffset &&
                        string.Equals(queued.AbsolutePath, window.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                if (s_readPrefetchCount == ReadPrefetchQueueCapacity)
                {
                    s_readPrefetchReadIndex = (s_readPrefetchReadIndex + 1) % ReadPrefetchQueueCapacity;
                    s_readPrefetchCount--;
                }

                s_readPrefetchQueue[s_readPrefetchWriteIndex] = new ReadPrefetchRequest
                {
                    AbsolutePath = window.AbsolutePath,
                    WindowOffset = nextWindowOffset,
                    FileLength = window.FileLength,
                    InvalidationGeneration = s_readCacheInvalidationGeneration
                };
                s_readPrefetchWriteIndex = (s_readPrefetchWriteIndex + 1) % ReadPrefetchQueueCapacity;
                s_readPrefetchCount++;
            }

            s_readPrefetchSignal.Set();
        }

        private static int SelectCachedReadWindowSlotLocked()
        {
            int selected = 0;
            int oldestUse = int.MaxValue;
            for (int i = 0; i < s_readWindows.Length; i++)
            {
                if (!s_readWindows[i].IsCreated)
                    return i;

                if (s_readWindows[i].LastUse < oldestUse)
                {
                    oldestUse = s_readWindows[i].LastUse;
                    selected = i;
                }
            }

            return selected;
        }

        private static void DisposeCachedReadWindow(ref CachedReadWindow window)
        {
            if (window.Bytes.IsCreated || window.BytesSentinelId > 0)
                DisposeCachedReadWindowBytes(ref window.Bytes, ref window.BytesSentinelId);

            window = default;
        }

        private static NativeArray<byte> AllocateCachedReadWindowBytes(int length, out int sentinelId)
        {
            sentinelId = 0;
            NativeArray<byte> bytes = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeArray(bytes, NativeMemoryOwner, nameof(CachedReadWindow), NativeMemoryLifetime);
                if (sentinelId > 0)
                    return bytes;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                try
                {
                    DisposeCachedReadWindowBytes(ref bytes, ref sentinelId);
                }
                catch (Exception cleanupFault)
                {
                    cleanupException = cleanupFault;
                }

                if (cleanupException != null)
                    throw new AggregateException(
                        "Cached read window byte allocation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }

            InvalidOperationException registrationException = new InvalidOperationException(NativeMemoryRegistrationFailureMessage);
            Exception registrationCleanupException = null;
            try
            {
                DisposeCachedReadWindowBytes(ref bytes, ref sentinelId);
            }
            catch (Exception cleanupFault)
            {
                registrationCleanupException = cleanupFault;
            }

            if (registrationCleanupException != null)
                throw new AggregateException(
                    "Cached read window byte registration failed and cleanup also failed.",
                    registrationException,
                    registrationCleanupException);

            throw registrationException;
        }

        private static void DisposeCachedReadWindowBytes(ref NativeArray<byte> bytes, ref int sentinelId)
        {
            Exception firstException = null;
            void* trackedPointer = bytes.IsCreated
                ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes)
                : null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }
            else if (trackedPointer != null)
            {
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            if (bytes.IsCreated)
            {
                try
                {
                    bytes.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    bytes = default;
                }
            }
            else
            {
                bytes = default;
            }

            if (firstException != null)
                throw firstException;
        }

        public static bool WriteAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            return WriteAll(absolutePath, buffer, byteCount, null, 0, out error);
        }

        public static bool WriteDiagnosticDumpAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            NativeWriteResult result = WriteAllSynchronous(
                absolutePath,
                buffer,
                byteCount,
                null,
                0,
                suppressDiagnosticDumpPath: false,
                paceWrites: false);
            error = result.Error;
            return result.Success;
        }

        public static bool WriteAllPaged(string absolutePath, void* buffer, int byteCount, out string error)
        {
            NativeWriteResult result = WriteAllSynchronous(
                absolutePath,
                buffer,
                byteCount,
                null,
                0,
                suppressDiagnosticDumpPath: true,
                paceWrites: true);
            error = result.Error;
            return result.Success;
        }

        public static bool WriteAll(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            out string error)
        {
            NativeWriteResult result = WriteAllSynchronous(
                absolutePath,
                firstBuffer,
                firstByteCount,
                secondBuffer,
                secondByteCount,
                suppressDiagnosticDumpPath: true,
                paceWrites: false);
            error = result.Error;
            return result.Success;
        }

        private static NativeWriteResult WriteAllSynchronous(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            bool suppressDiagnosticDumpPath,
            bool paceWrites)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return new NativeWriteResult(false, "Native write path is empty.");
            }

            long totalBytesLong = (long)math.max(firstByteCount, 0) + math.max(secondByteCount, 0);
            if (totalBytesLong <= 0L)
            {
                return new NativeWriteResult(false, "Native write requested zero bytes.");
            }

            if (totalBytesLong > int.MaxValue)
            {
                return new NativeWriteResult(false, "Native write exceeds the supported range.");
            }

            int totalBytes = (int)totalBytesLong;
            if (suppressDiagnosticDumpPath && IsSuppressedDiagnosticDumpPath(absolutePath))
                return new NativeWriteResult(true, string.Empty);

            try
            {
                InvalidateCachedReadWindows(absolutePath);

                if (!TryWriteAllNative(absolutePath, firstBuffer, firstByteCount, secondBuffer, secondByteCount, totalBytes, createAlways: true, paceWrites, out string writeError))
                    return new NativeWriteResult(false, writeError);

                InvalidateCachedReadWindows(absolutePath);

                if (!QueueThrottledFlush(absolutePath, totalBytes, out string flushError))
                    return new NativeWriteResult(false, flushError);

                return new NativeWriteResult(true, string.Empty);
            }
            catch (Exception)
            {
                return new NativeWriteResult(false, "Sequential native write failed.");
            }
        }

        public static bool OverwriteAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            return OverwriteAllInternal(absolutePath, buffer, byteCount, criticalFlush: false, out error);
        }

        public static bool OverwriteAllCritical(string absolutePath, void* buffer, int byteCount, out string error)
        {
            return OverwriteAllInternal(absolutePath, buffer, byteCount, criticalFlush: true, out error);
        }

        private static bool OverwriteAllInternal(string absolutePath, void* buffer, int byteCount, bool criticalFlush, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || buffer == null || byteCount <= 0)
            {
                error = "Native overwrite request is invalid.";
                return false;
            }

            if (IsSuppressedDiagnosticDumpPath(absolutePath))
                return true;

            try
            {
                InvalidateCachedReadWindows(absolutePath);
                if (!TryWriteAllNative(absolutePath, buffer, byteCount, null, 0, byteCount, createAlways: false, paceWrites: false, out error))
                    return false;

                InvalidateCachedReadWindows(absolutePath);

                if (criticalFlush)
                {
                    if (!FlushCriticalSavePath(absolutePath, byteCount, out error))
                        return false;
                }
                else
                {
                    if (!QueueThrottledFlush(absolutePath, byteCount, out error))
                        return false;
                }

                return true;
            }
            catch (Exception)
            {
                error = criticalFlush
                    ? "Critical native overwrite failed."
                    : "Sequential native overwrite failed.";
                return false;
            }
        }

        private static bool IsSuppressedDiagnosticDumpPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return false;

            string fileName = Path.GetFileName(absolutePath);
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.StartsWith("Dump_", StringComparison.OrdinalIgnoreCase))
                return true;

            return absolutePath.IndexOf("AgentLogs", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".h8dump", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryWriteAllNative(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            int totalBytes,
            bool createAlways,
            bool paceWrites,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || totalBytes <= 0)
            {
                error = "Native write request is invalid.";
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return TryWriteAllNativeWindows(absolutePath, firstBuffer, firstByteCount, secondBuffer, secondByteCount, totalBytes, createAlways, paceWrites, out error);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
            return TryWriteAllNativeUnix(absolutePath, firstBuffer, firstByteCount, secondBuffer, secondByteCount, totalBytes, createAlways, paceWrites, out error);
#else
            error = "Native write is unsupported on this platform.";
            return false;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PaceAfterNativeWritePage(int bytesWritten, int remainingBytes)
        {
            if (remainingBytes > 0 && bytesWritten > 0)
                Thread.Sleep(NativeWritePagePaceMilliseconds);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static bool TryWriteAllNativeWindows(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            int totalBytes,
            bool createAlways,
            bool paceWrites,
            out string error)
        {
            error = string.Empty;
            fixed (char* path = absolutePath)
            {
                IntPtr handle = CreateFileWNative(
                    path,
                    NativeGenericWrite,
                    0u,
                    IntPtr.Zero,
                    createAlways ? NativeCreateAlways : NativeOpenExisting,
                    NativeFileAttributeNormal,
                    IntPtr.Zero);

                if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                {
                    error = "Native write open failed.";
                    return false;
                }

                try
                {
                    long cursor = 0L;
                    if (!TrySeekWindowsHandle(handle, 0L, out error))
                        return false;

                    var firstArgs = new NativeWindowsWriteSegmentArgs(handle: handle, source: firstBuffer, byteCount: math.max(firstByteCount, 0), paceWrites: paceWrites);
                    if (!TryWriteWindowsSegment(in firstArgs, ref cursor, out error))
                        return false;

                    var secondArgs = new NativeWindowsWriteSegmentArgs(handle: handle, source: secondBuffer, byteCount: math.max(secondByteCount, 0), paceWrites: paceWrites);
                    if (!TryWriteWindowsSegment(in secondArgs, ref cursor, out error))
                        return false;

                    if (!TrySeekWindowsHandle(handle, totalBytes, out error) || !SetEndOfFileNative(handle))
                    {
                        error = "Native write truncate failed.";
                        return false;
                    }

                    return true;
                }
                finally
                {
                    CloseHandleNative(handle);
                }
            }
        }

        private static bool TrySeekWindowsHandle(IntPtr handle, long byteOffset, out string error)
        {
            error = string.Empty;
            if (!SetFilePointerExNative(handle, byteOffset, out _, NativeFileBegin))
            {
                error = "Native write seek failed.";
                return false;
            }

            return true;
        }

        private static bool TryWriteWindowsSegment(in NativeWindowsWriteSegmentArgs args, ref long cursor, out string error)
        {
            error = string.Empty;
            if (args.ByteCount <= 0)
                return true;

            if (args.Source == null)
            {
                cursor += args.ByteCount;
                return TrySeekWindowsHandle(args.Handle, cursor, out error);
            }

            byte* sourceBytes = (byte*)args.Source;
            int offset = 0;
            while (offset < args.ByteCount)
            {
                int chunkBytes = math.min(NativeWriteChunkBytes, args.ByteCount - offset);
                if (!WriteFileNative(args.Handle, sourceBytes + offset, (uint)chunkBytes, out uint bytesWritten, IntPtr.Zero))
                {
                    error = "Native write failed.";
                    return false;
                }

                if (bytesWritten == 0u)
                {
                    error = "Native write accepted zero bytes.";
                    return false;
                }

                offset += (int)bytesWritten;
                cursor += bytesWritten;
                if (args.PaceWrites)
                    PaceAfterNativeWritePage((int)bytesWritten, args.ByteCount - offset);
            }

            return true;
        }

        private static bool TryFlushPathNative(string absolutePath)
        {
            fixed (char* path = absolutePath)
            {
                IntPtr handle = CreateFileWNative(
                    path,
                    NativeGenericWrite,
                    NativeFileShareReadWriteDelete,
                    IntPtr.Zero,
                    NativeOpenExisting,
                    NativeFileAttributeNormal,
                    IntPtr.Zero);

                if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                    return false;

                try
                {
                    return FlushFileBuffersNative(handle);
                }
                finally
                {
                    CloseHandleNative(handle);
                }
            }
        }

        private static bool TryFlushParentDirectoryNative(string absoluteDirectory)
        {
            if (!Directory.Exists(absoluteDirectory))
                return false;

            fixed (char* path = absoluteDirectory)
            {
                IntPtr handle = CreateFileWNative(
                    path,
                    NativeGenericRead | NativeGenericWrite,
                    NativeFileShareReadWriteDelete,
                    IntPtr.Zero,
                    NativeOpenExisting,
                    NativeFileAttributeNormal | NativeFileFlagBackupSemantics,
                    IntPtr.Zero);

                if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                    return true;

                try
                {
                    // Windows directory handles do not provide a POSIX fsync contract; file flush is the hard gate.
                    _ = FlushFileBuffersNative(handle);
                    return true;
                }
                finally
                {
                    CloseHandleNative(handle);
                }
            }
        }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        private static bool TryWriteAllNativeUnix(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            int totalBytes,
            bool createAlways,
            bool paceWrites,
            out string error)
        {
            error = string.Empty;
            int fd = -1;
            lock (s_nativeUnixPathLock)
            {
                fixed (byte* path = s_nativeUnixPathUtf8.Bytes)
                {
                    if (!TryEncodePathUtf8(absolutePath, path, NativeUnixPathCapacity, out _))
                    {
                        error = "Native write path encoding failed.";
                        return false;
                    }

                    int flags = NativeUnixOpenWriteOnly;
                    if (createAlways)
                        flags |= NativeUnixOpenCreate | NativeUnixOpenTruncate;

                    fd = createAlways
                        ? OpenUnixWithMode(path, flags, NativeUnixCreateModeOwnerReadWrite)
                        : OpenUnix(path, flags);
                }
            }

            if (fd < 0)
            {
                error = "Native write open failed.";
                return false;
            }

            try
            {
                long cursor = 0L;
                if (LSeekUnix(fd, 0L, NativeUnixSeekSet) < 0L)
                {
                    error = "Native write seek failed.";
                    return false;
                }

                var firstArgs = new NativeUnixWriteSegmentArgs(fd: fd, source: firstBuffer, byteCount: math.max(firstByteCount, 0), paceWrites: paceWrites);
                if (!TryWriteUnixSegment(in firstArgs, ref cursor, out error))
                    return false;

                var secondArgs = new NativeUnixWriteSegmentArgs(fd: fd, source: secondBuffer, byteCount: math.max(secondByteCount, 0), paceWrites: paceWrites);
                if (!TryWriteUnixSegment(in secondArgs, ref cursor, out error))
                    return false;

                if (FTruncateUnix(fd, totalBytes) != 0)
                {
                    error = "Native write truncate failed.";
                    return false;
                }

                return true;
            }
            finally
            {
                CloseUnix(fd);
            }
        }

        private static bool TryWriteUnixSegment(in NativeUnixWriteSegmentArgs args, ref long cursor, out string error)
        {
            error = string.Empty;
            if (args.ByteCount <= 0)
                return true;

            if (args.Source == null)
            {
                cursor += args.ByteCount;
                if (LSeekUnix(args.Fd, cursor, NativeUnixSeekSet) < 0L)
                {
                    error = "Native write seek failed.";
                    return false;
                }

                return true;
            }

            byte* sourceBytes = (byte*)args.Source;
            int offset = 0;
            while (offset < args.ByteCount)
            {
                int chunkBytes = math.min(NativeWriteChunkBytes, args.ByteCount - offset);
                long bytesWritten = WriteUnix(args.Fd, sourceBytes + offset, (UIntPtr)chunkBytes).ToInt64();
                if (bytesWritten < 0L)
                {
                    error = "Native write failed.";
                    return false;
                }

                if (bytesWritten == 0L)
                {
                    error = "Native write accepted zero bytes.";
                    return false;
                }

                offset += (int)bytesWritten;
                cursor += bytesWritten;
                if (args.PaceWrites)
                    PaceAfterNativeWritePage((int)bytesWritten, args.ByteCount - offset);
            }

            return true;
        }

        private static bool TryFlushPathNative(string absolutePath)
        {
            int fd = -1;
            lock (s_nativeUnixPathLock)
            {
                fixed (byte* path = s_nativeUnixPathUtf8.Bytes)
                {
                    if (!TryEncodePathUtf8(absolutePath, path, NativeUnixPathCapacity, out _))
                        return false;

                    fd = OpenUnix(path, NativeUnixOpenWriteOnly);
                }
            }

            if (fd < 0)
                return false;

            try
            {
                return FSyncUnix(fd) == 0;
            }
            finally
            {
                CloseUnix(fd);
            }
        }

        private static bool TryFlushParentDirectoryNative(string absoluteDirectory)
        {
            int fd = -1;
            lock (s_nativeUnixPathLock)
            {
                fixed (byte* path = s_nativeUnixPathUtf8.Bytes)
                {
                    if (!TryEncodePathUtf8(absoluteDirectory, path, NativeUnixPathCapacity, out _))
                        return false;

                    fd = OpenUnix(path, NativeUnixOpenReadOnly);
                }
            }

            if (fd < 0)
                return false;

            try
            {
                return FSyncUnix(fd) == 0;
            }
            finally
            {
                CloseUnix(fd);
            }
        }
#else
        private static bool TryFlushPathNative(string absolutePath)
        {
            return false;
        }

        private static bool TryFlushParentDirectoryNative(string absoluteDirectory)
        {
            return false;
        }
#endif

        private static bool TryReadAbsoluteFileRangeToNativeBuffer(
            string absolutePath,
            long byteOffset,
            byte* destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || byteOffset < 0L || byteCount < 0)
            {
                error = "Native file read request is invalid.";
                return false;
            }

            if (byteCount == 0)
                return true;

            if (destination == null)
            {
                error = "Native file read destination is null.";
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fixed (char* path = absolutePath)
            {
                IntPtr handle = CreateFileWNative(
                    path,
                    NativeGenericRead,
                    NativeFileShareReadWriteDelete,
                    IntPtr.Zero,
                    NativeOpenExisting,
                    NativeFileAttributeNormal,
                    IntPtr.Zero);

                if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                {
                    error = "Native file range read open failed.";
                    return false;
                }

                try
                {
                    if (!SetFilePointerExNative(handle, byteOffset, out _, NativeFileBegin))
                    {
                        error = "Native file range read seek failed.";
                        return false;
                    }

                    return TryReadWindowsHandleToNativeBuffer(handle, destination, byteCount, out error);
                }
                finally
                {
                    CloseHandleNative(handle);
                }
            }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
            int fd = -1;
            lock (s_nativeUnixPathLock)
            {
                fixed (byte* path = s_nativeUnixPathUtf8.Bytes)
                {
                    if (!TryEncodePathUtf8(absolutePath, path, NativeUnixPathCapacity, out _))
                    {
                        error = "Native file range read path encoding failed.";
                        return false;
                    }

                    fd = OpenUnix(path, NativeUnixOpenReadOnly);
                }
            }

            if (fd < 0)
            {
                error = "Native file range read open failed.";
                return false;
            }

            try
            {
                if (LSeekUnix(fd, byteOffset, NativeUnixSeekSet) < 0L)
                {
                    error = "Native file range read seek failed.";
                    return false;
                }

                return TryReadUnixFdToNativeBuffer(fd, destination, byteCount, out error);
            }
            finally
            {
                CloseUnix(fd);
            }
#else
            error = "Native file range read is unsupported on this platform.";
            return false;
#endif
        }

        public static bool TryReadAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || buffer == null || byteCount <= 0)
            {
                error = "Native read request is invalid.";
                return false;
            }

            return TryCopyFromCachedReadWindow(absolutePath, 0L, buffer, byteCount, out error);
        }

        public static bool TryCopyFileRangeToNativeArray(
            string absolutePath,
            long byteOffset,
            NativeArray<byte> destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) ||
                byteOffset < 0L ||
                !destination.IsCreated ||
                byteCount < 0 ||
                byteCount > destination.Length)
            {
                error = "Cached file native range copy request is invalid.";
                return false;
            }

            byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            if (byteCount > 0)
                UnsafeUtility.MemClear(destinationPtr, byteCount);

            return TryCopyFromCachedReadWindow(absolutePath, byteOffset, destinationPtr, byteCount, out error);
        }

        public static bool TryGetFileLength(string absolutePath, out long fileLength, out string error)
        {
            fileLength = 0L;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native read path is empty.";
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return TryGetFileLengthWindows(absolutePath, out fileLength, out error);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
            return TryGetFileLengthUnix(absolutePath, out fileLength, out error);
#else
            error = "Native file length query unsupported on this platform.";
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static bool TryGetFileLengthWindows(string absolutePath, out long fileLength, out string error)
        {
            fileLength = 0L;
            error = string.Empty;
            fixed (char* path = absolutePath)
            {
                IntPtr handle = CreateFileWNative(
                    path,
                    NativeGenericRead,
                    NativeFileShareReadWriteDelete,
                    IntPtr.Zero,
                    NativeOpenExisting,
                    NativeFileAttributeNormal,
                    IntPtr.Zero);

                if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                {
                    error = "Native file length open failed.";
                    return false;
                }

                try
                {
                    if (!GetFileSizeExNative(handle, out fileLength) || fileLength < 0L)
                    {
                        error = "Native file length query failed.";
                        fileLength = 0L;
                        return false;
                    }

                    return true;
                }
                finally
                {
                    CloseHandleNative(handle);
                }
            }
        }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        private static bool TryGetFileLengthUnix(string absolutePath, out long fileLength, out string error)
        {
            fileLength = 0L;
            error = string.Empty;
            lock (s_nativeUnixPathLock)
            {
                fixed (byte* path = s_nativeUnixPathUtf8.Bytes)
                {
                    if (!TryEncodePathUtf8(absolutePath, path, NativeUnixPathCapacity, out _))
                    {
                        error = "Native file length path encoding failed.";
                        return false;
                    }

                    int fd = OpenUnix(path, NativeUnixOpenReadOnly);
                    if (fd < 0)
                    {
                        error = "Native file length open failed.";
                        return false;
                    }

                    try
                    {
                        long length = LSeekUnix(fd, 0L, NativeUnixSeekEnd);
                        if (length < 0L)
                        {
                            error = "Native file length query failed.";
                            return false;
                        }

                        fileLength = length;
                        return true;
                    }
                    finally
                    {
                        CloseUnix(fd);
                    }
                }
            }
        }

        private static bool TryEncodePathUtf8(string source, byte* destination, int destinationCapacity, out int byteCount)
        {
            byteCount = 0;
            if (string.IsNullOrEmpty(source) || destination == null || destinationCapacity <= 0)
                return false;

            for (int i = 0; i < source.Length; i++)
            {
                int code = source[i];
                if (char.IsHighSurrogate(source[i]))
                {
                    if (i + 1 >= source.Length || !char.IsLowSurrogate(source[i + 1]))
                        return false;

                    code = char.ConvertToUtf32(source[i], source[++i]);
                }
                else if (char.IsLowSurrogate(source[i]))
                {
                    return false;
                }

                if (code <= 0x7F)
                {
                    if (byteCount + 1 >= destinationCapacity)
                        return false;

                    destination[byteCount++] = (byte)code;
                }
                else if (code <= 0x7FF)
                {
                    if (byteCount + 2 >= destinationCapacity)
                        return false;

                    destination[byteCount++] = (byte)(0xC0 | (code >> 6));
                    destination[byteCount++] = (byte)(0x80 | (code & 0x3F));
                }
                else if (code <= 0xFFFF)
                {
                    if (byteCount + 3 >= destinationCapacity)
                        return false;

                    destination[byteCount++] = (byte)(0xE0 | (code >> 12));
                    destination[byteCount++] = (byte)(0x80 | ((code >> 6) & 0x3F));
                    destination[byteCount++] = (byte)(0x80 | (code & 0x3F));
                }
                else
                {
                    if (byteCount + 4 >= destinationCapacity)
                        return false;

                    destination[byteCount++] = (byte)(0xF0 | (code >> 18));
                    destination[byteCount++] = (byte)(0x80 | ((code >> 12) & 0x3F));
                    destination[byteCount++] = (byte)(0x80 | ((code >> 6) & 0x3F));
                    destination[byteCount++] = (byte)(0x80 | (code & 0x3F));
                }
            }

            destination[byteCount] = 0;
            return true;
        }
#endif

        internal static bool TryOpenReadOnlyMapping(string absolutePath, out ReadOnlyMapping mapping, out string error)
        {
            mapping = default;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native read path is empty.";
                return false;
            }

            NativeArray<byte> fileBytes = default;
            try
            {
                if (!TryGetFileLength(absolutePath, out long fileLength, out error))
                    return false;

                if (fileLength <= 0L)
                {
                    error = "Native read requested an empty file.";
                    return false;
                }

                if (fileLength > int.MaxValue)
                {
                    error = "Native read exceeds the supported native buffer range.";
                    return false;
                }

                // COLD ALLOC: NativeArray<byte>[fileLength] - native read-only save snapshot - owner: AsyncWriteManager
                fileBytes = AllocateReadOnlyMappingBytes((int)fileLength);
                byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                if (!TryReadAbsoluteFileToNativeBuffer(absolutePath, filePtr, (int)fileLength, out error))
                    return false;

                mapping.Bytes = fileBytes;
                mapping.View = (IntPtr)filePtr;
                mapping.Length = fileLength;
                fileBytes = default;
                return true;
            }
            catch (Exception)
            {
                error = "Native read-only open failed.";
                return false;
            }
            finally
            {
                if (fileBytes.IsCreated)
                    DisposeReadOnlyMappingBytes(ref fileBytes);
            }
        }

        private static NativeArray<byte> AllocateReadOnlyMappingBytes(int length)
        {
            NativeArray<byte> bytes = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            try
            {
                int registrationId = NativeMemorySentinel.RegisterNativeArray(bytes, NativeMemoryOwner, nameof(ReadOnlyMapping), NativeMemoryLifetime);
                if (registrationId > 0)
                    return bytes;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                try
                {
                    DisposeTrackedNativeArrayByPointer(ref bytes);
                }
                catch (Exception cleanupFault)
                {
                    cleanupException = cleanupFault;
                }

                if (cleanupException != null)
                    throw new AggregateException(
                        "Read-only mapping byte allocation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }

            InvalidOperationException registrationException = new InvalidOperationException(NativeMemoryRegistrationFailureMessage);
            Exception registrationCleanupException = null;
            try
            {
                DisposeTrackedNativeArrayByPointer(ref bytes);
            }
            catch (Exception cleanupFault)
            {
                registrationCleanupException = cleanupFault;
            }

            if (registrationCleanupException != null)
                throw new AggregateException(
                    "Read-only mapping byte registration failed and cleanup also failed.",
                    registrationException,
                    registrationCleanupException);

            throw registrationException;
        }

        private static bool TryReadAbsoluteFileToNativeBuffer(
            string absolutePath,
            byte* destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            if (byteCount < 0 || destination == null)
            {
                error = "Native read buffer is invalid.";
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fixed (char* path = absolutePath)
            {
                IntPtr handle = CreateFileWNative(
                    path,
                    NativeGenericRead,
                    NativeFileShareRead,
                    IntPtr.Zero,
                    NativeOpenExisting,
                    NativeFileAttributeNormal,
                    IntPtr.Zero);

                if (handle == NativeInvalidHandleValue)
                {
                    error = "Native read open failed.";
                    return false;
                }

                try
                {
                    return TryReadWindowsHandleToNativeBuffer(handle, destination, byteCount, out error);
                }
                finally
                {
                    CloseHandleNative(handle);
                }
            }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
            lock (s_nativeUnixPathLock)
            {
                fixed (byte* path = s_nativeUnixPathUtf8.Bytes)
                {
                    if (!TryEncodePathUtf8(absolutePath, path, NativeUnixPathCapacity, out _))
                    {
                        error = "Native read path encoding failed.";
                        return false;
                    }

                    int fd = OpenUnix(path, NativeUnixOpenReadOnly);
                    if (fd < 0)
                    {
                        error = "Native read open failed.";
                        return false;
                    }

                    try
                    {
                        return TryReadUnixFdToNativeBuffer(fd, destination, byteCount, out error);
                    }
                    finally
                    {
                        CloseUnix(fd);
                    }
                }
            }
#else
            error = "Native read is unsupported on this platform.";
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static bool TryReadWindowsHandleToNativeBuffer(
            IntPtr handle,
            byte* destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            int offset = 0;
            while (offset < byteCount)
            {
                int chunkBytes = math.min(NativeReadChunkBytes, byteCount - offset);
                if (!ReadFileNative(handle, destination + offset, (uint)chunkBytes, out uint bytesRead, IntPtr.Zero))
                {
                    error = "Native read failed.";
                    return false;
                }

                if (bytesRead == 0u)
                {
                    error = "Native read ended before expected length.";
                    return false;
                }

                offset += (int)bytesRead;
            }

            return true;
        }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        private static bool TryReadUnixFdToNativeBuffer(
            int fd,
            byte* destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            int offset = 0;
            while (offset < byteCount)
            {
                int chunkBytes = math.min(NativeReadChunkBytes, byteCount - offset);
                long bytesRead = ReadUnix(fd, destination + offset, (UIntPtr)chunkBytes).ToInt64();
                if (bytesRead < 0L)
                {
                    error = "Native read failed.";
                    return false;
                }

                if (bytesRead == 0L)
                {
                    error = "Native read ended before expected length.";
                    return false;
                }

                offset += (int)bytesRead;
            }

            return true;
        }
#endif

        internal static void CloseReadOnlyMapping(ref ReadOnlyMapping mapping)
        {
            NativeArray<byte> bytes = mapping.Bytes;
            if (bytes.IsCreated)
            {
                DisposeReadOnlyMappingBytes(ref bytes);
                mapping.Bytes = bytes;
            }

            mapping = default;
        }

        private static void DisposeReadOnlyMappingBytes(ref NativeArray<byte> bytes)
        {
            if (!bytes.IsCreated)
                return;

            DisposeTrackedNativeArrayByPointer(ref bytes);
        }

        private static void DisposeTrackedNativeArrayByPointer<T>(ref NativeArray<T> array)
            where T : struct
        {
            Exception firstException = null;
            void* trackedPointer = array.IsCreated
                ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array)
                : null;

            if (trackedPointer != null)
            {
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            if (array.IsCreated)
            {
                try
                {
                    array.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    array = default;
                }
            }
            else
            {
                array = default;
            }

            if (firstException != null)
                throw firstException;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileWNative(
            char* lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", EntryPoint = "GetFileSizeEx", SetLastError = true)]
        private static extern bool GetFileSizeExNative(IntPtr hFile, out long lpFileSize);

        [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
        private static extern bool ReadFileNative(
            IntPtr hFile,
            void* lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
        private static extern bool WriteFileNative(
            IntPtr hFile,
            void* lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", EntryPoint = "SetFilePointerEx", SetLastError = true)]
        private static extern bool SetFilePointerExNative(
            IntPtr hFile,
            long liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);

        [DllImport("kernel32.dll", EntryPoint = "SetEndOfFile", SetLastError = true)]
        private static extern bool SetEndOfFileNative(IntPtr hFile);

        [DllImport("kernel32.dll", EntryPoint = "FlushFileBuffers", SetLastError = true)]
        private static extern bool FlushFileBuffersNative(IntPtr hFile);

        [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
        private static extern bool CloseHandleNative(IntPtr hObject);
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_ANDROID
        [DllImport("libc", SetLastError = true, EntryPoint = "open")]
        private static extern int OpenUnix(byte* pathname, int flags);

        [DllImport("libc", SetLastError = true, EntryPoint = "open")]
        private static extern int OpenUnixWithMode(byte* pathname, int flags, int mode);

        [DllImport("libc", SetLastError = true, EntryPoint = "lseek")]
        private static extern long LSeekUnix(int fd, long offset, int whence);

        [DllImport("libc", SetLastError = true, EntryPoint = "read")]
        private static extern IntPtr ReadUnix(int fd, void* buf, UIntPtr count);

        [DllImport("libc", SetLastError = true, EntryPoint = "write")]
        private static extern IntPtr WriteUnix(int fd, void* buf, UIntPtr count);

        [DllImport("libc", SetLastError = true, EntryPoint = "ftruncate")]
        private static extern int FTruncateUnix(int fd, long length);

        [DllImport("libc", SetLastError = true, EntryPoint = "fsync")]
        private static extern int FSyncUnix(int fd);

        [DllImport("libc", SetLastError = true, EntryPoint = "close")]
        private static extern int CloseUnix(int fd);
#endif
    }
    internal static unsafe class SaveBinaryStorage
    {
        internal const uint Magic = 0x48454354u;
        internal const int ErrorCodeNone = 0;
        internal const int ErrorCodeHeaderReadFailed = unchecked((int)0x48385244);
        internal const int ErrorCodeMagicMismatch = unchecked((int)0x48384D47);
        internal const ushort CurrentVersion = 0x000B;
        internal const uint ExplorationMortonBuildSalt32 = 0x48384D4Fu;
        internal const int ExplorationMortonMaskAlignmentBytes = 64;
        internal const ushort MinimumSupportedVersion = 0x0003;
        internal const byte CurrentCompatMask = 0x07;
        internal const byte FlagLz4Blocks = 0x01;
        internal const byte FlagTokenSubstitution = 0x02;
        internal const byte FlagIndexedSectorBlocks = 0x04;
        internal const byte FlagProtectedLz4Blocks = 0x08;
        internal const int CurrentHeaderSize = 56;
        internal const int LegacyHeaderSize = 44;
        internal const int SavePageSizeBytes = 64 * 1024;
        internal const int DialogueChoiceFlagCapacity = 16;
        internal const ushort PlayerDialogueChoiceSaveFacilityMask = 1 << 0;
        internal const ushort PlayerDialogueChoiceKnownFlagsMask = PlayerDialogueChoiceSaveFacilityMask;
        internal const int BlockSizeBytes = 256 * 1024;
        internal const int RawPayloadCapacityBytes = 64 * 1024 * 1024;
        internal const int MaxCompressedPayloadBytes = 68 * 1024 * 1024;

        private const long UnixEpochTicks = 621355968000000000L;
        private const int PayloadPrefixSizeBytes = SaveDataMigration_AupV8.CurrentPayloadPrefixSizeBytes;
        private const int PackedQuestStateSectionHeaderSize = 64;
        private const int LegacyPersistentWorldSectionHeaderSize = 12;
        private const int PersistentWorldSectionHeaderSize = 16;
        private const int LegacyEcosystemSectionHeaderSize = 4;
        private const int EcosystemSectionHeaderSize = 8;
        private const int SaveFileHeaderPrefixSize = 8;
        private const int LegacyHeaderHashSizeBytes = 36;
        private const int IndexedHeaderV8Size = 52;
        private const int IndexedHeaderV8HashSizeBytes = 44;
        private const int CurrentHeaderHashSizeBytes = 48;
        private const int HeaderPackedQuestWordCountBits = 16;
        private const uint HeaderPackedQuestWordCountMask = (1u << HeaderPackedQuestWordCountBits) - 1u;
        private const uint HeaderDialogueChoiceFlagsMask = 0xFFFF0000u;
        private const int HeaderDialogueChoiceFlagsShift = HeaderPackedQuestWordCountBits;
        private const int HeaderDialogueChoiceSourceWordIndex = QuestRuntimeLayout.NarrativeWordStart;
        private const int MaxPackedQuestStateWordCount = QuestRuntimeLayout.WordCapacity;
        private const ushort First64BitHashVersion = 0x0004;
        private const ushort CompactPersistentWorldSectionVersion = 0x0005;
        private const ushort EcosystemSectionVersion = 0x0006;
        private const ushort TokenizedPayloadVersion = 0x0007;
        private const ushort IndexedBlockStorageVersion = 0x0008;
        private const ushort HeaderChecksumVersion = 0x0009;
        private const ushort AlignedIndexedSectorDirectoryVersion = 0x000A;
        private const ushort AlignedSectionHeaderVersion = 0x000B;
        private const int TokenizedPayloadHeaderSize = 8;
        private const int TokenBlockSizeBytes = 16;
        private const int MaxTokenCount = 64;
        private const byte TokenEscapeMarker = 0xFF;
        private const int IndexedSectorDirectoryHeaderSize = 16;
        private const int IndexedSectorBlockHeaderSize = 8;
        private const int IndexedSectorDirectorySlotCount = 4096;
        private const int LegacySectorEntryBytes = 28;
        private const int SectorEntryBytes = 32;
        private const int StandardCompressedBlockHeaderBytes = 8;
        private const int ProtectedCompressedBlockHeaderBytes = 64;
        private const uint ProtectedCompressedBlockMagic = 0x4C423848u; // "H8BL"
        private const int ManagedDeflateBlockLengthFlag = 1 << 30;
        private const int CompressedBlockLengthMask = ManagedDeflateBlockLengthFlag - 1;
        internal const int ModPayloadSubBlockSizeBytes = SaveBinaryPayloadCodec.ProtectedLz4BlockSizeBytes;
        internal const int ModPayloadHeaderSizeBytes = 32;
        internal const int ModPayloadMaxBytes = ModPayloadSubBlockSizeBytes - ModPayloadHeaderSizeBytes;
        internal const int IndexedSectorDirectoryCapacity = IndexedSectorDirectorySlotCount;
        private const int PersistentWorldSectorEdgeLengthMeters = 1000;
        private const double PersistentWorldSectorLoadRadiusMeters = 1000.0;
        private const double PersistentWorldSectorLoadRadiusSq = PersistentWorldSectorLoadRadiusMeters * PersistentWorldSectorLoadRadiusMeters;
        internal const int DefaultIndexedPersistentWorldChunkSizeMeters = 64;
        private const ushort PersistentWorldDeletedItemHashIndex = ushort.MaxValue;
        private const uint ModPayloadMagic = 0x50444F4Du; // "MODP"
        private const ushort ModPayloadVersion = 1;
        private const ulong ModPayloadSectorPrefix = 0x4D50000000000000UL;
        private const ulong ModPayloadSectorMask = 0xFFFF000000000000UL;
        private const int ProtectedLz4BlockSizeShift = 14;
        private const int ProtectedLz4BlockSizeMask = ModPayloadSubBlockSizeBytes - 1;
        private const int ManagedCompressionStackScratchBytes = 16 * 1024;
        internal const int IndexedSectorQuarantineHashCapacity = 16;
        private const string Lz4DllName = "liblz4";
        private const string NativeMemoryOwner = nameof(SaveBinaryStorage);
        private const string NativeMemoryRegistrationFailureMessage = "NativeMemorySentinel registration failed for SaveBinaryStorage buffer.";
        private const string NativeMemoryTransientRegistrationFailureMessage = "NativeMemorySentinel registration failed for transient SaveBinaryStorage buffer.";

        private static void DisposeTrackedNativeArrayByPointer<T>(ref NativeArray<T> array)
            where T : struct
        {
            Exception firstException = null;
            void* trackedPointer = array.IsCreated
                ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array)
                : null;

            if (trackedPointer != null)
            {
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            if (array.IsCreated)
            {
                try
                {
                    array.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    array = default;
                }
            }
            else
            {
                array = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private const string EntityStateWriteSourceStatesLabel = "indexedSectorEntityStateSourceStates";
        private const string EntityStateWriteSortEntriesLabel = "indexedSectorEntityStateSortEntries";
        private const string EntityStateWriteRadixScratchLabel = "indexedSectorEntityStateRadixScratch";
        private const string EntityStateWriteSortedStatesLabel = "indexedSectorEntityStateSortedStates";
        private const string EntityStateWriteFileBytesLabel = "indexedSectorEntityStateFileBytes";
        private const string EntityStateWriteResultLengthLabel = "indexedSectorEntityStateResultLength";
        private const string EntityStateWriteRadixCountsLabel = "indexedSectorEntityStateRadixCounts";
        private const string EntityStateWriteRadixOffsetsLabel = "indexedSectorEntityStateRadixOffsets";
        private const string BackupRecoverySourceRecordsLabel = "indexedSectorBackupRecoverySourceRecords";
        private const string BackupRecoverySectorRecordsLabel = "indexedSectorBackupRecoveryRecords";
        private const string IndexedSectorCompactionBufferLabel = "indexedSectorCompactionBuffer";
        private const string IndexedSectorCommitBufferLabel = "indexedSectorCommitBuffer";
        private const string IndexedSectorDirectoryScratchLabel = "indexedSectorDirectoryScratch";
        private const string IndexedSectorReadRawScratchLabel = "indexedSectorReadRawScratch";
        private const string IndexedSectorOverrideRawSectionScratchLabel = "indexedSectorOverrideRawSectionScratch";
        private const string IndexedSectorOverrideFileScratchLabel = "indexedSectorOverrideFileScratch";
        private const string IndexedSectorModPayloadRawBlockScratchLabel = "indexedSectorModPayloadRawBlockScratch";
        private const string IndexedSectorOverrideCompressedBlockScratchLabel = "indexedSectorOverrideCompressedBlockScratch";
        private const string IndexedSectorOverrideVerifyRawBlockScratchLabel = "indexedSectorOverrideVerifyRawBlockScratch";
        private const string IndexedSectorGroupSlotScratchLabel = "indexedSectorGroupSlotScratch";
        private const string IndexedSectorRecordGroupScratchLabel = "indexedSectorRecordGroupScratch";
        private const string IndexedSectorGroupListScratchLabel = "indexedSectorGroupListScratch";
        private const string IndexedSectorGroupRecordListScratchLabel = "indexedSectorGroupRecordListScratch";
        private const string IndexedSectorAggregatedWorldDeltasScratchLabel = "indexedSectorAggregatedWorldDeltasScratch";
        private const string IndexedSectorBackupAppendRecordsScratchLabel = "indexedSectorBackupAppendRecordsScratch";
        private const string PersistentWorldSectionChunkLookupScratchLabel = "persistentWorldSectionChunkLookupScratch";
        private const string PersistentWorldSectionChunkTableScratchLabel = "persistentWorldSectionChunkTableScratch";
        private const string PersistentWorldSectionItemHashLookupScratchLabel = "persistentWorldSectionItemHashLookupScratch";
        private const string PersistentWorldSectionItemHashTableScratchLabel = "persistentWorldSectionItemHashTableScratch";
        private static readonly WaitCallback s_indexedSectorDefragmentationCallback = RunIndexedPersistentWorldDefragmentation;
        private static string s_indexedSectorDefragmentationPath;
        private static int s_indexedSectorDefragmentationQueued;
        private const string EntityStateWriteCompactStatesLabel = "indexedSectorEntityStateCompact16";
        private const string PristineResetEmptySectorRecordsLabel = "indexedSectorPristineResetEmptyRecords";
        private const uint SectorEntityStateCompact16Magic = 0xFA160016u;
        private const uint SectorEntityStateTypeMask = 0xFF000000u;
        private const uint SectorFaunaHibernationStateTypeMask = 0xF9000000u;
        private const uint SectorWhaleFallStateTypeMask = 0xF8000000u;
        private const uint SectorFaunaEggStateTypeMask = 0xF7000000u;
        private const uint SectorFloraSpawnTimestampStateTypeMask = 0xFA000000u;
        private const uint SectorCompactAxisMask = 0x3FFu;
        private const float SectorCompactAxisScale = 1023f;
        private const float SectorCompactDepthMinMeters = -8192f;
        private const float SectorCompactDepthMaxMeters = 1024f;
        private const float SectorCompactTimeQuantumSeconds = 1f;
        private const uint SectorCompactTimeMaxEncoded = 0x00FFFFFFu;
        private static int s_indexedSectorQuarantineReported;
        private static int s_indexedSectorBackupRecoveryReported;
        // COLD ALLOC: long[16] - quarantined indexed sector reset queue - owner: SaveBinaryStorage
        private static readonly long[] s_indexedSectorQuarantineHashes = new long[IndexedSectorQuarantineHashCapacity];
        // COLD ALLOC: long[16] - backup-recovered indexed sector repair queue - owner: SaveBinaryStorage
        private static readonly long[] s_indexedSectorBackupRecoveryHashes = new long[IndexedSectorQuarantineHashCapacity];
        // COLD ALLOC: object[1] - quarantine hash queue lock for background paging workers - owner: SaveBinaryStorage
        private static readonly object s_indexedSectorQuarantineLock = new object();
        // COLD ALLOC: object[1] - backup recovery hash queue lock for background paging workers - owner: SaveBinaryStorage
        private static readonly object s_indexedSectorBackupRecoveryLock = new object();
        // COLD ALLOC: object[1] - serialized managed compression fallback scratch guard - owner: SaveBinaryStorage
        private static readonly object s_managedCompressionLock = new object();
        private static int s_indexedSectorQuarantineHashCount;
        private static int s_indexedSectorBackupRecoveryHashCount;
        private static readonly uint _indexedSectorQuarantineWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.IndexedSectorQuarantine"));
        private static readonly uint _indexedSectorBackupRecoveryWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.IndexedSectorBackupRecovery"));
        private static int s_lastReadErrorCode;

        internal static int LastReadErrorCode => Volatile.Read(ref s_lastReadErrorCode);

        [StructLayout(LayoutKind.Explicit, Size = TokenizedPayloadHeaderSize)]
        private struct TokenizedPayloadHeader
        {
            [FieldOffset(0)] public uint ExpandedPayloadLength;
            [FieldOffset(4)] public ushort TokenCount;
            [FieldOffset(6)] public ushort Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct IndexedSectorGroup
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public int DirectorySlot;
            [FieldOffset(12)] public int StartIndex;
            [FieldOffset(16)] public int Count;
            [FieldOffset(20)] public int WriteCount;
            [FieldOffset(24)] public int Reserved0;
            [FieldOffset(28)] public int Reserved1;
        }

        private ref struct IndexedSectorGroupBuffer
        {
            public NativeList<IndexedSectorGroup> Groups;
            public NativeList<PersistentWorldDeltaRecord> Records;
            public int GroupsSentinelId;
            public int RecordsSentinelId;
            public int InvalidRecordCount;

            public int Count => Groups.IsCreated ? Groups.Length : 0;

            public void Dispose()
            {
                DisposeRegisteredTransientNativeList(ref Records, ref RecordsSentinelId, IndexedSectorGroupRecordListScratchLabel);
                DisposeRegisteredTransientNativeList(ref Groups, ref GroupsSentinelId, IndexedSectorGroupListScratchLabel);
                this = default;
            }
        }

        private struct PersistentWorldSectionTableSentinelIds
        {
            public int ChunkLookup;
            public int ChunkTable;
            public int ItemHashLookup;
            public int ItemHashTable;
        }

        private readonly struct IndexedSectorCommitTarget
        {
            public readonly byte ReusedExistingSlot;
            public readonly byte InsertedNewSlot;
            public readonly int SlotIndex;
            public readonly long WriteOffset;
            public readonly long NewFileLength;

            public IndexedSectorCommitTarget(bool reusedExistingSlot, bool insertedNewSlot, int slotIndex, long writeOffset, long newFileLength)
            {
                ReusedExistingSlot = reusedExistingSlot ? (byte)1 : (byte)0;
                InsertedNewSlot = insertedNewSlot ? (byte)1 : (byte)0;
                SlotIndex = slotIndex;
                WriteOffset = writeOffset;
                NewFileLength = newFileLength;
            }
        }

        internal ref struct IndexedSectorEntityStateWriteHandle
        {
            internal bool IsCreated;
            internal string AbsolutePath;
            internal long SectorHash;
            internal JobHandle Handle;
            public NativeArray<EntityDataRecord> SourceStates;
            public NativeArray<SectorEntityStateSortEntry> SortEntries;
            public NativeArray<SectorEntityStateSortEntry> RadixScratch;
            public NativeArray<EntityDataRecord> SortedEntityStates;
            internal NativeArray<SectorCompactEntityStateRecord16> CompactStates;
            public NativeArray<byte> FileBytes;
            public NativeArray<int> ResultLength;
            public NativeArray<int> RadixCounts;
            public NativeArray<int> RadixOffsets;

            internal bool IsCompleted => IsCreated && Handle.IsCompleted;

            internal static NativeArray<T> AllocateRegisteredArray<T>(
                int length,
                string label,
                NativeArrayOptions options)
                where T : struct
            {
                NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
                try
                {
                    int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                    if (registrationId > 0)
                        return array;
                }
                catch (Exception exception)
                {
                    Exception cleanupException = null;
                    try
                    {
                        DisposeTrackedNativeArrayByPointer(ref array);
                    }
                    catch (Exception cleanupFault)
                    {
                        cleanupException = cleanupFault;
                    }

                    if (cleanupException != null)
                        throw new AggregateException(
                            "Indexed-sector entity-state scratch allocation failed and cleanup also failed.",
                            exception,
                            cleanupException);

                    throw;
                }

                InvalidOperationException registrationException = new InvalidOperationException(NativeMemoryTransientRegistrationFailureMessage);
                Exception registrationCleanupException = null;
                try
                {
                    DisposeTrackedNativeArrayByPointer(ref array);
                }
                catch (Exception cleanupFault)
                {
                    registrationCleanupException = cleanupFault;
                }

                if (registrationCleanupException != null)
                    throw new AggregateException(
                        "Indexed-sector entity-state scratch registration failed and cleanup also failed.",
                        registrationException,
                        registrationCleanupException);

                throw registrationException;
            }

            private static void DisposeRegisteredArray<T>(ref NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                DisposeTrackedNativeArrayByPointer(ref array);
            }

            internal void Dispose()
            {
                DisposeRegisteredArray(ref SourceStates);
                DisposeRegisteredArray(ref SortEntries);
                DisposeRegisteredArray(ref RadixScratch);
                DisposeRegisteredArray(ref SortedEntityStates);
                DisposeRegisteredArray(ref CompactStates);
                DisposeRegisteredArray(ref FileBytes);
                DisposeRegisteredArray(ref ResultLength);
                DisposeRegisteredArray(ref RadixCounts);
                DisposeRegisteredArray(ref RadixOffsets);

                this = default;
            }

            internal JobHandle DisposeDeferred(JobHandle dependency)
            {
                JobHandle disposeHandle = JobHandle.CombineDependencies(Handle, dependency);
                // Keep TempJob ownership visible to NativeMemorySentinel until no scheduled job can still read the arrays.
                disposeHandle.Complete();
                Dispose();
                return default;
            }
        }

        private static NativeArray<T> AllocateRegisteredPersistentScratchNativeArray<T>(
            int length,
            NativeArrayOptions options,
            string label)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, options);
            try
            {
                RegisterPersistentScratchNativeArray(array, label);
                return array;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                try
                {
                    DisposeTrackedNativeArrayByPointer(ref array);
                }
                catch (Exception cleanupFault)
                {
                    cleanupException = cleanupFault;
                }

                if (cleanupException != null)
                    throw new AggregateException(
                        "Indexed-sector persistent scratch allocation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }
        }

        private static void RegisterPersistentScratchNativeArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TransientArena);
            if (registrationId <= 0)
                throw new InvalidOperationException(NativeMemoryRegistrationFailureMessage);
        }

        private static void DisposeRegisteredPersistentScratchNativeArray<T>(ref NativeArray<T> array, string label)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            DisposeTrackedNativeArrayByPointer(ref array);
        }

        private struct RegisteredTransientNativeArray<T> : IDisposable
            where T : struct
        {
            private string _label;
            private NativeAllocationLifetime _lifetime;

            public NativeArray<T> Array;

            public RegisteredTransientNativeArray(
                int length,
                NativeArrayOptions options,
                string label,
                NativeAllocationLifetime lifetime)
            {
                _label = label;
                _lifetime = lifetime;
                Array = default;
                int registrationId = 0;
                try
                {
                    // COLD ALLOC: NativeArray<T>[length] - tracked transient save scratch - owner: SaveBinaryStorage
                    Array = new NativeArray<T>(length, Allocator.Temp, options);
                    registrationId = NativeMemorySentinel.RegisterNativeArray(Array, NativeMemoryOwner, label, lifetime);
                    if (registrationId <= 0)
                        throw new InvalidOperationException(NativeMemoryTransientRegistrationFailureMessage);
                }
                catch (Exception exception)
                {
                    Exception cleanupException = null;
                    try
                    {
                        DisposeRegisteredTransientNativeArray(ref Array, _label, _lifetime);
                    }
                    catch (Exception cleanupFault)
                    {
                        cleanupException = cleanupFault;
                    }

                    if (cleanupException != null)
                        throw new AggregateException(
                            "Transient NativeArray creation failed and cleanup also failed.",
                            exception,
                            cleanupException);

                    throw;
                }
            }

            public void Dispose()
            {
                DisposeRegisteredTransientNativeArray(ref Array, _label, _lifetime);
                _label = null;
                _lifetime = default;
            }
        }

        private static RegisteredTransientNativeArray<T> CreateRegisteredTransientNativeArray<T>(
            int length,
            NativeArrayOptions options,
            string label,
            NativeAllocationLifetime lifetime = NativeAllocationLifetime.TransientArena)
            where T : struct
        {
            return new RegisteredTransientNativeArray<T>(length, options, label, lifetime);
        }

        private static void DisposeRegisteredTransientNativeArray<T>(
            ref NativeArray<T> array,
            string label,
            NativeAllocationLifetime lifetime)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            DisposeTrackedNativeArrayByPointer(ref array);
        }

        private static NativeList<T> CreateRegisteredTransientNativeList<T>(
            int capacity,
            string label,
            out int registrationId,
            NativeAllocationLifetime lifetime = NativeAllocationLifetime.TransientArena)
            where T : unmanaged
        {
            registrationId = 0;
            NativeList<T> list = default;
            try
            {
                // COLD ALLOC: NativeList<T>[capacity] - tracked transient save scratch - owner: SaveBinaryStorage
                list = new NativeList<T>(capacity, Allocator.Temp);
                registrationId = NativeMemorySentinel.RegisterNativeListInstance(list, NativeMemoryOwner, label, lifetime);
                if (registrationId <= 0)
                    throw new InvalidOperationException(NativeMemoryTransientRegistrationFailureMessage);

                return list;
            }
            catch (Exception exception)
            {
                try
                {
                    DisposeRegisteredTransientNativeList(ref list, ref registrationId, label, lifetime);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "Transient NativeList creation failed and cleanup also failed.",
                        exception,
                        cleanupException);
                }

                throw;
            }
        }

        private static void DisposeRegisteredTransientNativeList<T>(
            ref NativeList<T> list,
            ref int registrationId,
            string label,
            NativeAllocationLifetime lifetime = NativeAllocationLifetime.TransientArena)
            where T : unmanaged
        {
            Exception firstException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    registrationId = 0;
                }
            }

            if (list.IsCreated)
            {
                try
                {
                    list.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    list = default;
                }
            }
            else
            {
                list = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static NativeParallelHashMap<TKey, TValue> CreateRegisteredTransientNativeParallelHashMap<TKey, TValue>(
            int capacity,
            string label,
            out int registrationId,
            NativeAllocationLifetime lifetime = NativeAllocationLifetime.TransientArena)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            registrationId = 0;
            NativeParallelHashMap<TKey, TValue> map = default;
            try
            {
                // COLD ALLOC: NativeParallelHashMap<TKey,TValue>[capacity] - tracked transient save scratch - owner: SaveBinaryStorage
                map = new NativeParallelHashMap<TKey, TValue>(capacity, Allocator.Temp);
                registrationId = NativeMemorySentinel.RegisterNativeParallelHashMapInstance(map, NativeMemoryOwner, label, lifetime);
                if (registrationId <= 0)
                    throw new InvalidOperationException(NativeMemoryTransientRegistrationFailureMessage);

                return map;
            }
            catch (Exception exception)
            {
                try
                {
                    DisposeRegisteredTransientNativeParallelHashMap(ref map, ref registrationId, label, lifetime);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "Transient NativeParallelHashMap creation failed and cleanup also failed.",
                        exception,
                        cleanupException);
                }

                throw;
            }
        }

        private static void DisposeRegisteredTransientNativeParallelHashMap<TKey, TValue>(
            ref NativeParallelHashMap<TKey, TValue> map,
            ref int registrationId,
            string label,
            NativeAllocationLifetime lifetime = NativeAllocationLifetime.TransientArena)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            Exception firstException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    registrationId = 0;
                }
            }

            if (map.IsCreated)
            {
                try
                {
                    map.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    map = default;
                }
            }
            else
            {
                map = default;
            }

            if (firstException != null)
                throw firstException;
        }

        internal struct SectorEntityStateSortEntry
        {
            public ulong SortKey;
            public EntityDataRecord Record;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        internal struct SectorCompactEntityStateRecord16
        {
            [FieldOffset(0)] public uint PackedSectorPosition;
            [FieldOffset(4)] public uint InstanceUid;
            [FieldOffset(8)] public ushort Quantity;
            [FieldOffset(10)] public ushort PackedAux;
            [FieldOffset(12)] public uint PackedState;
        }

        [BinaryBlittableSafe]
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        internal struct QuantizedAupLocalOffsetShort3
        {
            [FieldOffset(0)] public short XMillimeters;
            [FieldOffset(2)] public short YMillimeters;
            [FieldOffset(4)] public short ZMillimeters;
            [FieldOffset(6)] public ushort Reserved0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildSectorEntityStateSortEntriesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityDataRecord> SourceStates;
            public NativeArray<SectorEntityStateSortEntry> Entries;
            public int ChunkSizeMeters;

            public void Execute(int index)
            {
                EntityDataRecord record = SourceStates[index];
                AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAlignedBlit(in record.Position);

                Entries[index] = new SectorEntityStateSortEntry
                {
                    SortKey = SaveBinaryPayloadCodec.BuildSectorEntitySpatialSortKey(in position, ChunkSizeMeters),
                    Record = record
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct RadixSortSectorEntityStateEntriesJob : IJob
        {
            public NativeArray<SectorEntityStateSortEntry> Entries;
            public NativeArray<SectorEntityStateSortEntry> Scratch;
            public NativeArray<int> Counts;
            public NativeArray<int> Offsets;

            public void Execute()
            {
                int entryCount = Entries.Length;
                if (entryCount <= 1)
                    return;

                RadixPass(Entries, Scratch, 0);
                RadixPass(Scratch, Entries, 16);
                RadixPass(Entries, Scratch, 32);
                RadixPass(Scratch, Entries, 48);
            }

            private void RadixPass(
                NativeArray<SectorEntityStateSortEntry> source,
                NativeArray<SectorEntityStateSortEntry> destination,
                int shift)
            {
                for (int i = 0; i < Counts.Length; i++)
                {
                    Counts[i] = 0;
                    Offsets[i] = 0;
                }

                for (int i = 0; i < source.Length; i++)
                {
                    int bucket = (int)((source[i].SortKey >> shift) & 0xFFFFUL);
                    Counts[bucket]++;
                }

                int cursor = 0;
                for (int i = 0; i < Counts.Length; i++)
                {
                    Offsets[i] = cursor;
                    cursor += Counts[i];
                }

                for (int i = 0; i < source.Length; i++)
                {
                    SectorEntityStateSortEntry entry = source[i];
                    int bucket = (int)((entry.SortKey >> shift) & 0xFFFFUL);
                    int destinationIndex = Offsets[bucket]++;
                    destination[destinationIndex] = entry;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ExtractSortedSectorEntityStatesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorEntityStateSortEntry> Entries;
            [WriteOnly] public NativeArray<EntityDataRecord> SortedStates;

            public void Execute(int index)
            {
                SortedStates[index] = Entries[index].Record;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildCompactSectorEntityStatesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityDataRecord> SortedStates;
            [WriteOnly] public NativeArray<SectorCompactEntityStateRecord16> CompactStates;
            public long SectorHash;

            public void Execute(int index)
            {
                EntityDataRecord sortedState = SortedStates[index];
                CompactStates[index] = PackCompactEntityState16(in sortedState, SectorHash);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CompressSectorEntityStateJob : IJob
        {
            [ReadOnly] public NativeArray<SectorCompactEntityStateRecord16> CompactStates;
            public NativeArray<byte> FileBytes;
            public NativeArray<int> ResultLength;
            public long SectorHash;

            public void Execute()
            {
                ResultLength[0] = 0;
            }
        }

        private static SectorCompactEntityStateRecord16 PackCompactEntityState16(in EntityDataRecord state, long sectorHash)
        {
            uint typeMask = (uint)state.InventoryHash & SectorEntityStateTypeMask;
            bool hibernationState = typeMask == SectorFaunaHibernationStateTypeMask;
            uint packedQuantity = hibernationState
                ? (uint)math.clamp(state.Quantity, 1, ushort.MaxValue)
                : (uint)math.max(1, state.Quantity);
            ushort packedAux = typeMask == SectorFaunaHibernationStateTypeMask
                ? PackCompactFaunaVitals(in state)
                : (ushort)((packedQuantity >> 16) & 0xFFFFu);
            uint packedState = PackCompactEntityStateValue(in state, typeMask);

            return new SectorCompactEntityStateRecord16
            {
                PackedSectorPosition = PackCompactSectorPosition(in state.Position, sectorHash),
                InstanceUid = state.InstanceUid,
                Quantity = (ushort)(packedQuantity & 0xFFFFu),
                PackedAux = packedAux,
                PackedState = packedState
            };
        }

        private static EntityDataRecord UnpackCompactEntityState16(in SectorCompactEntityStateRecord16 compactState, long sectorHash)
        {
            uint typeMask = compactState.PackedState & SectorEntityStateTypeMask;
            AbsoluteUniversePosition position = UnpackCompactSectorPosition(compactState.PackedSectorPosition, sectorHash);
            AbsoluteUniversePositionBlit128 packedPosition = position.ToAlignedBlit();
            float integrity01 = 1f;

            if (typeMask == SectorFaunaHibernationStateTypeMask)
            {
                UnpackCompactFaunaVitals(compactState.PackedAux, out integrity01, out float hunger01);
                packedPosition.Reserved = PackCompactFaunaVitalsReserved(integrity01, hunger01);
            }
            else if (typeMask == SectorFaunaEggStateTypeMask || typeMask == SectorWhaleFallStateTypeMask)
            {
                integrity01 = UnpackCompactTimeSeconds(compactState.PackedState & SectorCompactTimeMaxEncoded);
            }

            return new EntityDataRecord
            {
                Position = packedPosition,
                Quantity = ResolveCompactQuantity(in compactState, typeMask),
                Integrity01 = integrity01,
                InventoryHash = unchecked((int)compactState.PackedState),
                InstanceUid = compactState.InstanceUid
            };
        }

        internal static QuantizedAupLocalOffsetShort3 QuantizeAupLocalOffsetShort3(
            in AbsoluteUniversePosition position,
            int3 chunkId,
            int chunkSizeMeters)
        {
            if (chunkSizeMeters <= 0 ||
                !position.IsFinite() ||
                !AbsoluteUniversePosition.IsValidChunkId(chunkId))
            {
                return default;
            }

            double safeChunkSize = chunkSizeMeters;
            double halfChunkSize = safeChunkSize * 0.5d;
            double3 absolute = position.ToAbsoluteDouble3();
            double3 chunkCenter = new double3(
                (chunkId.x * safeChunkSize) + halfChunkSize,
                (chunkId.y * safeChunkSize) + halfChunkSize,
                (chunkId.z * safeChunkSize) + halfChunkSize);
            double3 relativeMeters = absolute - chunkCenter;

            return new QuantizedAupLocalOffsetShort3
            {
                XMillimeters = QuantizeMetersToShortMillimeters(relativeMeters.x),
                YMillimeters = QuantizeMetersToShortMillimeters(relativeMeters.y),
                ZMillimeters = QuantizeMetersToShortMillimeters(relativeMeters.z)
            };
        }

        internal static AbsoluteUniversePosition UnpackAupLocalOffsetShort3(
            int3 chunkId,
            int chunkSizeMeters,
            in QuantizedAupLocalOffsetShort3 packedOffset)
        {
            if (chunkSizeMeters <= 0 || !AbsoluteUniversePosition.IsValidChunkId(chunkId))
                return AbsoluteUniversePosition.Invalid();

            double safeChunkSize = chunkSizeMeters;
            double halfChunkSize = safeChunkSize * 0.5d;
            double3 chunkCenter = new double3(
                (chunkId.x * safeChunkSize) + halfChunkSize,
                (chunkId.y * safeChunkSize) + halfChunkSize,
                (chunkId.z * safeChunkSize) + halfChunkSize);
            double3 relativeMeters = new double3(
                packedOffset.XMillimeters,
                packedOffset.YMillimeters,
                packedOffset.ZMillimeters) * 0.001d;

            return AbsoluteUniversePosition.FromAbsolutePosition(chunkCenter + relativeMeters);
        }

        private static short QuantizeMetersToShortMillimeters(double meters)
        {
            int millimeters = (int)math.round(meters * 1000d);
            return (short)math.clamp(millimeters, short.MinValue, short.MaxValue);
        }

        private static int ResolveCompactQuantity(in SectorCompactEntityStateRecord16 compactState, uint typeMask)
        {
            if (typeMask == SectorFaunaHibernationStateTypeMask)
                return math.max(1, compactState.Quantity);

            uint quantity = ((uint)compactState.PackedAux << 16) | compactState.Quantity;
            return (int)math.max(1u, quantity);
        }

        private static uint PackCompactEntityStateValue(in EntityDataRecord state, uint typeMask)
        {
            if (typeMask == SectorFaunaHibernationStateTypeMask)
                return (uint)state.InventoryHash;

            if (typeMask == SectorFaunaEggStateTypeMask || typeMask == SectorWhaleFallStateTypeMask)
                return typeMask | PackCompactTimeSeconds(state.Integrity01);

            if (typeMask == SectorFloraSpawnTimestampStateTypeMask)
                return typeMask;

            return (uint)state.InventoryHash;
        }

        private static uint PackCompactSectorPosition(in AbsoluteUniversePositionBlit128 alignedPosition, long sectorHash)
        {
            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAlignedBlit(in alignedPosition);
            if (!position.IsFinite())
                return 0u;

            double3 absolute = position.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(absolute)))
                return 0u;

            int2 sector = UnpackSectorHash(sectorHash);
            double sectorOriginX = (double)sector.x * PersistentWorldSectorEdgeLengthMeters;
            double sectorOriginZ = (double)sector.y * PersistentWorldSectorEdgeLengthMeters;
            float x01 = math.saturate((float)((absolute.x - sectorOriginX) / PersistentWorldSectorEdgeLengthMeters));
            float z01 = math.saturate((float)((absolute.z - sectorOriginZ) / PersistentWorldSectorEdgeLengthMeters));
            float y01 = math.saturate((float)((absolute.y - SectorCompactDepthMinMeters) / (SectorCompactDepthMaxMeters - SectorCompactDepthMinMeters)));

            uint x = (uint)math.round(x01 * SectorCompactAxisScale) & SectorCompactAxisMask;
            uint y = (uint)math.round(y01 * SectorCompactAxisScale) & SectorCompactAxisMask;
            uint z = (uint)math.round(z01 * SectorCompactAxisScale) & SectorCompactAxisMask;
            return x | (y << 10) | (z << 20);
        }

        private static AbsoluteUniversePosition UnpackCompactSectorPosition(uint packedPosition, long sectorHash)
        {
            int2 sector = UnpackSectorHash(sectorHash);
            float x01 = (packedPosition & SectorCompactAxisMask) / SectorCompactAxisScale;
            float y01 = ((packedPosition >> 10) & SectorCompactAxisMask) / SectorCompactAxisScale;
            float z01 = ((packedPosition >> 20) & SectorCompactAxisMask) / SectorCompactAxisScale;
            double x = ((double)sector.x * PersistentWorldSectorEdgeLengthMeters) + (x01 * PersistentWorldSectorEdgeLengthMeters);
            double y = math.lerp(SectorCompactDepthMinMeters, SectorCompactDepthMaxMeters, y01);
            double z = ((double)sector.y * PersistentWorldSectorEdgeLengthMeters) + (z01 * PersistentWorldSectorEdgeLengthMeters);
            return AbsoluteUniversePosition.FromAbsolutePosition(new double3(x, y, z));
        }

        private static ushort PackCompactFaunaVitals(in EntityDataRecord state)
        {
            float health01 = state.Integrity01;
            float hunger01 = 0f;
            ulong reserved = state.Position.Reserved;
            if (reserved != 0UL)
            {
                health01 = math.asfloat((uint)(reserved >> 32));
                hunger01 = math.asfloat((uint)reserved);
            }

            uint health = (uint)math.round(math.saturate(health01) * 255f);
            uint hunger = (uint)math.round(math.saturate(hunger01) * 255f);
            return (ushort)((health << 8) | hunger);
        }

        private static void UnpackCompactFaunaVitals(ushort packedVitals, out float health01, out float hunger01)
        {
            health01 = ((packedVitals >> 8) & 0xFF) * (1f / 255f);
            hunger01 = (packedVitals & 0xFF) * (1f / 255f);
        }

        private static ulong PackCompactFaunaVitalsReserved(float health01, float hunger01)
        {
            uint packedHealth = math.asuint(math.saturate(health01));
            uint packedHunger = math.asuint(math.saturate(hunger01));
            return ((ulong)packedHealth << 32) | packedHunger;
        }

        private static uint PackCompactTimeSeconds(float timeSeconds)
        {
            return (uint)math.min(SectorCompactTimeMaxEncoded, (uint)math.round(math.max(0f, timeSeconds) / SectorCompactTimeQuantumSeconds));
        }

        private static float UnpackCompactTimeSeconds(uint packedTime)
        {
            return packedTime * SectorCompactTimeQuantumSeconds;
        }

        [StructLayout(LayoutKind.Explicit, Size = IndexedSectorDirectoryHeaderSize)]
        private struct IndexedSectorDirectoryHeader
        {
            [FieldOffset(0)] public uint SectorCount;
            [FieldOffset(4)] public int ChunkSizeMeters;
            [FieldOffset(8)] public int MetadataCompressedSize;
            [FieldOffset(12)] public int MetadataDecompressedSize;
        }

        [StructLayout(LayoutKind.Explicit, Size = IndexedSectorBlockHeaderSize)]
        private struct IndexedSectorBlockHeader
        {
            [FieldOffset(0)] public uint Flags;
            [FieldOffset(4)] public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = ProtectedCompressedBlockHeaderBytes)]
        private struct ProtectedCompressedBlockHeader
        {
            [FieldOffset(0)] public int CompressedLength;
            [FieldOffset(4)] public int RawLength;
            [FieldOffset(8)] public uint RawChecksumLow32;
            [FieldOffset(12)] public uint Magic;
            [FieldOffset(16)] public ulong Reserved0;
            [FieldOffset(24)] public ulong Reserved1;
            [FieldOffset(32)] public ulong Reserved2;
            [FieldOffset(40)] public ulong Reserved3;
            [FieldOffset(48)] public ulong Reserved4;
            [FieldOffset(56)] public ulong Reserved5;
        }

        [StructLayout(LayoutKind.Explicit, Size = LegacySectorEntryBytes)]
        private struct LegacySectorEntry28
        {
            [FieldOffset(0)]
            public long SectorHash;
            [FieldOffset(8)]
            public long ByteOffset;
            [FieldOffset(16)]
            public int CompressedSize;
            [FieldOffset(20)]
            public int DecompressedSize;
            [FieldOffset(24)]
            public uint Checksum;
        }

        [BinaryBlittableSafe]
        [StructLayout(LayoutKind.Explicit, Size = SectorEntryBytes)]
        internal struct SectorEntry
        {
            [FieldOffset(0)]
            public long SectorHash;
            [FieldOffset(8)]
            public long ByteOffset;
            [FieldOffset(16)]
            public int CompressedSize;
            [FieldOffset(20)]
            public int DecompressedSize;
            [FieldOffset(24)]
            public uint Checksum;
            [FieldOffset(28)]
            public uint Reserved0;
        }

        private static int ResolveIndexedSectorEntrySize(ushort saveVersion)
        {
            return saveVersion >= AlignedIndexedSectorDirectoryVersion
                ? SectorEntryBytes
                : LegacySectorEntryBytes;
        }

        private static SectorEntry ReadIndexedSectorEntry(byte* entryPtr, int entrySize)
        {
            if (entrySize == LegacySectorEntryBytes)
            {
                LegacySectorEntry28 legacy = UnsafeUtility.ReadArrayElement<LegacySectorEntry28>(entryPtr, 0);
                return new SectorEntry
                {
                    SectorHash = legacy.SectorHash,
                    ByteOffset = legacy.ByteOffset,
                    CompressedSize = legacy.CompressedSize,
                    DecompressedSize = legacy.DecompressedSize,
                    Checksum = legacy.Checksum,
                    Reserved0 = 0u
                };
            }

            return UnsafeUtility.ReadArrayElement<SectorEntry>(entryPtr, 0);
        }

        private static void WriteIndexedSectorEntry(byte* entryPtr, int entrySize, in SectorEntry entry)
        {
            if (entrySize == LegacySectorEntryBytes)
            {
                LegacySectorEntry28 legacy = new LegacySectorEntry28
                {
                    SectorHash = entry.SectorHash,
                    ByteOffset = entry.ByteOffset,
                    CompressedSize = entry.CompressedSize,
                    DecompressedSize = entry.DecompressedSize,
                    Checksum = entry.Checksum
                };
                UnsafeUtility.CopyStructureToPtr(ref legacy, entryPtr);
                return;
            }

            SectorEntry aligned = entry;
            aligned.Reserved0 = 0u;
            UnsafeUtility.CopyStructureToPtr(ref aligned, entryPtr);
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct SectorOverrideFileHeader
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public int CompressedSize;
            [FieldOffset(12)] public int DecompressedSize;
            [FieldOffset(16)] public uint Checksum;
            [FieldOffset(20)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct SectorEntityStateFileHeader
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public int CompressedSize;
            [FieldOffset(12)] public int DecompressedSize;
            [FieldOffset(16)] public uint RecordCount;
            [FieldOffset(20)] public uint Checksum;
        }

        internal readonly struct IndexedSectorEntryInfo
        {
            public readonly long SectorHash;
            public readonly long ByteOffset;
            public readonly int CompressedSize;
            public readonly int DecompressedSize;
            public readonly uint Checksum;

            public IndexedSectorEntryInfo(long sectorHash, long byteOffset, int compressedSize, int decompressedSize, uint checksum)
            {
                SectorHash = sectorHash;
                ByteOffset = byteOffset;
                CompressedSize = compressedSize;
                DecompressedSize = decompressedSize;
                Checksum = checksum;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = ModPayloadHeaderSizeBytes)]
        private struct ModPayloadSubSectorHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public ushort Version;
            [FieldOffset(6)] public ushort HeaderSize;
            [FieldOffset(8)] public uint ModHash;
            [FieldOffset(12)] public ushort PayloadLength;
            [FieldOffset(14)] public ushort Flags;
            [FieldOffset(16)] public long PagedSectorHash;
            [FieldOffset(24)] public uint PayloadChecksum;
            [FieldOffset(28)] public uint Reserved;
        }

        internal readonly struct ModPayloadSectorInfo
        {
            public readonly long SectorHash;
            public readonly uint ModHash;
            public readonly long PagedSectorHash;
            public readonly int PayloadLength;
            public readonly uint PayloadChecksum;

            public ModPayloadSectorInfo(long sectorHash, uint modHash, long pagedSectorHash, int payloadLength, uint payloadChecksum)
            {
                SectorHash = sectorHash;
                ModHash = modHash;
                PagedSectorHash = pagedSectorHash;
                PayloadLength = payloadLength;
                PayloadChecksum = payloadChecksum;
            }
        }

        internal delegate bool ModPayloadReadHandler(
            in ModPayloadSectorInfo sectorInfo,
            NativeArray<byte> payloadBytes,
            int payloadLength,
            out string error);

        [StructLayout(LayoutKind.Explicit, Size = SaveFileHeaderPrefixSize)]
        private struct SaveFileHeaderPrefix
        {
            [FieldOffset(0)] public uint MagicValue;
            [FieldOffset(4)] public ushort Version;
            [FieldOffset(6)] public byte CompatMask;
            [FieldOffset(7)] public byte Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = CurrentHeaderSize)]
        internal struct SaveFileHeader
        {
            [FieldOffset(0)] public uint MagicValue;
            [FieldOffset(4)] public ushort Version;
            [FieldOffset(6)] public byte CompatMask;
            [FieldOffset(7)] public byte Flags;
            [FieldOffset(8)] public ulong TimestampUnixMs;
            [FieldOffset(16)] public uint Checksum;
            [FieldOffset(20)] public uint DeltaCount;
            [FieldOffset(24)] public uint EntityCount;
            [FieldOffset(28)] public uint PlayerOffset;
            [FieldOffset(32)] public uint DeltaOffset;
            [FieldOffset(36)] public uint EntityOffset;
            [FieldOffset(40)] public ulong HashPayload64;
            [FieldOffset(48)] public ulong HashHeader64;
        }

        [StructLayout(LayoutKind.Explicit, Size = IndexedHeaderV8Size)]
        private struct IndexedSaveFileHeaderV8
        {
            [FieldOffset(0)]
            public uint MagicValue;
            [FieldOffset(4)]
            public ushort Version;
            [FieldOffset(6)]
            public byte CompatMask;
            [FieldOffset(7)]
            public byte Flags;
            [FieldOffset(8)]
            public ulong TimestampUnixMs;
            [FieldOffset(16)]
            public uint DeltaCount;
            [FieldOffset(20)]
            public uint EntityCount;
            [FieldOffset(24)]
            public uint PlayerOffset;
            [FieldOffset(28)]
            public uint DeltaOffset;
            [FieldOffset(32)]
            public uint EntityOffset;
            [FieldOffset(36)]
            public uint HashPayload64Lo;
            [FieldOffset(40)]
            public uint HashPayload64Hi;
            [FieldOffset(44)]
            public uint HashHeader64Lo;
            [FieldOffset(48)]
            public uint HashHeader64Hi;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct LegacyIndexedChecksumLeaf
        {
            [FieldOffset(0)] public long SectorHash;
            [FieldOffset(8)] public uint Checksum;
            [FieldOffset(12)] public int CompressedSize;
            [FieldOffset(16)] public int DecompressedSize;
            [FieldOffset(20)] public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        internal struct SaveCloudMetadataHeader
        {
            [FieldOffset(0)] public ulong TimestampUnixMs;
            [FieldOffset(8)] public ulong HashPayload64;
            [FieldOffset(16)] public ulong HashHeader64;
            [FieldOffset(24)] public uint MagicValue;
            [FieldOffset(28)] public uint Checksum;
            [FieldOffset(32)] public uint PlayerOffset;
            [FieldOffset(36)] public uint DeltaOffset;
            [FieldOffset(40)] public uint EntityOffset;
            [FieldOffset(44)] public ushort Version;
            [FieldOffset(46)] public byte CompatMask;
            [FieldOffset(47)] public byte Flags;
            [FieldOffset(48)] public uint Reserved0;
            [FieldOffset(52)] private uint _pad0;
            [FieldOffset(56)] public ulong Reserved1;
            [FieldOffset(64)] public ulong Reserved2;
            [FieldOffset(72)] public ulong Reserved3;
            [FieldOffset(80)] public ulong Reserved4;
            [FieldOffset(88)] public ulong Reserved5;
            [FieldOffset(96)] public ulong Reserved6;
            [FieldOffset(104)] public ulong Reserved7;
            [FieldOffset(112)] public ulong Reserved8;
            [FieldOffset(120)] public ulong Reserved9;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ExtractPlayerDialogueChoiceFlags(NativeArray<uint> packedQuestStateWords)
        {
            if (!packedQuestStateWords.IsCreated ||
                packedQuestStateWords.Length <= HeaderDialogueChoiceSourceWordIndex)
                return 0;

            return SanitizePlayerDialogueChoiceFlags(
                (ushort)(packedQuestStateWords[HeaderDialogueChoiceSourceWordIndex] & HeaderPackedQuestWordCountMask));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ExtractPlayerDialogueChoiceFlags(uint[] packedQuestStateWords)
        {
            if (packedQuestStateWords == null ||
                packedQuestStateWords.Length <= HeaderDialogueChoiceSourceWordIndex)
                return 0;

            return SanitizePlayerDialogueChoiceFlags(
                (ushort)(packedQuestStateWords[HeaderDialogueChoiceSourceWordIndex] & HeaderPackedQuestWordCountMask));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort DecodePlayerDialogueChoiceFlags(in SaveFileHeader header)
        {
            if (header.Version < AlignedSectionHeaderVersion)
                return 0;

            return SanitizePlayerDialogueChoiceFlags(
                (ushort)((header.DeltaCount & HeaderDialogueChoiceFlagsMask) >> HeaderDialogueChoiceFlagsShift));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort SanitizePlayerDialogueChoiceFlags(ushort flags)
        {
            return (ushort)(flags & PlayerDialogueChoiceKnownFlagsMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ResolveLoadedPlayerDialogueChoiceFlags(
            ushort saveFileVersion,
            ushort decodedHeaderFlags,
            uint[] packedQuestStateWords)
        {
            decodedHeaderFlags = SanitizePlayerDialogueChoiceFlags(decodedHeaderFlags);
            if (saveFileVersion >= AlignedSectionHeaderVersion)
                return decodedHeaderFlags;

            return SanitizePlayerDialogueChoiceFlags(
                (ushort)(decodedHeaderFlags | ExtractPlayerDialogueChoiceFlags(packedQuestStateWords)));
        }

        private static bool TryEncodeHeaderDeltaCount(
            int packedQuestWordCount,
            ushort playerDialogueChoiceFlags,
            out uint encodedDeltaCount,
            out string error)
        {
            encodedDeltaCount = 0u;
            error = string.Empty;

            if ((uint)packedQuestWordCount > (uint)MaxPackedQuestStateWordCount)
            {
                error = "Packed quest-state count exceeds the quest runtime word capacity.";
                return false;
            }

            if ((uint)packedQuestWordCount > HeaderPackedQuestWordCountMask)
            {
                error = "Packed quest-state count exceeds the v0x000B 16-bit header field.";
                return false;
            }

            playerDialogueChoiceFlags = SanitizePlayerDialogueChoiceFlags(playerDialogueChoiceFlags);
            encodedDeltaCount =
                ((uint)playerDialogueChoiceFlags << HeaderDialogueChoiceFlagsShift) |
                (uint)packedQuestWordCount;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DecodePackedQuestWordCount(in SaveFileHeader header)
        {
            uint encodedCount = header.Version >= AlignedSectionHeaderVersion
                ? header.DeltaCount & HeaderPackedQuestWordCountMask
                : header.DeltaCount;
            return encodedCount <= int.MaxValue ? (int)encodedCount : MaxPackedQuestStateWordCount + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryDecodePackedQuestWordCount(in SaveFileHeader header, out int packedQuestWordCount, out string error)
        {
            packedQuestWordCount = DecodePackedQuestWordCount(in header);
            if ((uint)packedQuestWordCount > (uint)MaxPackedQuestStateWordCount)
            {
                error = "Save packed quest-state count exceeds the quest runtime word capacity.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidatePackedQuestSectionHeader(
            in QuestSaveHeader packedQuestHeader,
            int expectedPackedQuestWordCount,
            out string error)
        {
            if ((uint)expectedPackedQuestWordCount > (uint)MaxPackedQuestStateWordCount)
            {
                error = "Packed quest-state word count exceeds runtime capacity.";
                return false;
            }

            if (packedQuestHeader.Magic != QuestSaveHeader.HeaderMagic)
            {
                error = "Packed quest-state header magic mismatch.";
                return false;
            }

            if (packedQuestHeader.FlagCount != (uint)expectedPackedQuestWordCount ||
                packedQuestHeader.FlagCount > (uint)MaxPackedQuestStateWordCount)
            {
                error = "Packed quest-state word count header mismatch.";
                return false;
            }

            uint schemaVersion = packedQuestHeader.ReadSchemaVersion();
            if (schemaVersion != 0u && schemaVersion != QuestSaveHeader.CurrentSchemaVersion)
            {
                error = "Packed quest-state schema version mismatch.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateCurrentBinaryLayouts(out string error)
        {
            int currentHeaderSize = UnsafeUtility.SizeOf<SaveFileHeader>();
            if (currentHeaderSize != CurrentHeaderSize || (currentHeaderSize & 7) != 0)
            {
                error = "Save header layout drift detected.";
                return false;
            }

            int questSaveHeaderSize = UnsafeUtility.SizeOf<QuestSaveHeader>();
            if (questSaveHeaderSize != PackedQuestStateSectionHeaderSize || (questSaveHeaderSize & 7) != 0)
            {
                error = "Quest save header layout drift detected.";
                return false;
            }

            int dialogueChoiceFlagCapacity = DialogueChoiceFlagCapacity;
            uint playerDialogueChoiceSaveFacilityMask = PlayerDialogueChoiceSaveFacilityMask;
            uint playerDialogueChoiceKnownFlagsMask = PlayerDialogueChoiceKnownFlagsMask;
            int headerPackedQuestWordCountBits = HeaderPackedQuestWordCountBits;
            uint headerPackedQuestWordCountMask = HeaderPackedQuestWordCountMask;
            uint headerDialogueChoiceFlagsMask = HeaderDialogueChoiceFlagsMask;
            int headerDialogueChoiceFlagsShift = HeaderDialogueChoiceFlagsShift;
            int headerDialogueChoiceSourceWordIndex = HeaderDialogueChoiceSourceWordIndex;
            int narrativeWordStart = QuestRuntimeLayout.NarrativeWordStart;
            int narrativeWordEnd = narrativeWordStart + QuestRuntimeLayout.NarrativeWordCount;
            int maxPackedQuestStateWordCount = MaxPackedQuestStateWordCount;
            if (dialogueChoiceFlagCapacity != headerPackedQuestWordCountBits ||
                playerDialogueChoiceSaveFacilityMask != 1u << 0 ||
                playerDialogueChoiceKnownFlagsMask != playerDialogueChoiceSaveFacilityMask ||
                (playerDialogueChoiceKnownFlagsMask & ~headerPackedQuestWordCountMask) != 0u ||
                headerDialogueChoiceFlagsMask != (headerPackedQuestWordCountMask << headerDialogueChoiceFlagsShift) ||
                headerDialogueChoiceSourceWordIndex < narrativeWordStart ||
                headerDialogueChoiceSourceWordIndex >= narrativeWordEnd ||
                narrativeWordEnd > maxPackedQuestStateWordCount ||
                (uint)maxPackedQuestStateWordCount > headerPackedQuestWordCountMask)
            {
                error = "Save header dialogue-choice bit-pack contract drift detected.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        [StructLayout(LayoutKind.Explicit, Size = LegacyHeaderSize)]
        private struct LegacySaveFileHeader
        {
            [FieldOffset(0)]
            public uint MagicValue;
            [FieldOffset(4)]
            public ushort Version;
            [FieldOffset(6)]
            public byte CompatMask;
            [FieldOffset(7)]
            public byte Flags;
            [FieldOffset(8)]
            public ulong TimestampUnixMs;
            [FieldOffset(16)]
            public uint DeltaCount;
            [FieldOffset(20)]
            public uint EntityCount;
            [FieldOffset(24)]
            public uint PlayerOffset;
            [FieldOffset(28)]
            public uint DeltaOffset;
            [FieldOffset(32)]
            public uint EntityOffset;
            [FieldOffset(36)]
            public uint HashHeader32;
            [FieldOffset(40)]
            public uint HashPayload32;
        }

        [BinaryBlittableSafe]
        [Serializable]
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct DeltaCell
        {
            /// <summary>
            /// Packed universe-space cell key.
            /// </summary>
            [FieldOffset(0)] public ulong UniverseKey;

            /// <summary>
            /// Signed-distance delta in meters.
            /// </summary>
            [FieldOffset(8)] public float SdfValue;

            /// <summary>
            /// Material palette index.
            /// </summary>
            [FieldOffset(12)] public byte MaterialId;

            /// <summary>
            /// Packed per-cell state flags.
            /// </summary>
            [FieldOffset(13)] public byte Flags;

            /// <summary>
            /// Material-specific metadata payload.
            /// </summary>
            [FieldOffset(14)] public ushort Metadata;

            /// <summary>
            /// Reserved expansion bytes required by the 24-byte aligned runtime layout.
            /// </summary>
            [FieldOffset(16)] public uint Reserved;
            [FieldOffset(20)] public uint Reserved1;
        }

        [BinaryBlittableSafe]
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        internal struct ThermalGridRleRun
        {
            [FieldOffset(0)] public ushort StartIndex;
            [FieldOffset(2)] public ushort Count;
            [FieldOffset(4)] public float TemperatureCelsius;
        }

        internal static bool TryStageThermalGridRleDelta(
            NativeArray<ThermalGridRleRun> runs,
            int runCount,
            out int byteCount,
            out uint checksum)
        {
            byteCount = 0;
            checksum = 2166136261u;
            if (!runs.IsCreated || runCount < 0 || runCount > runs.Length)
                return false;

            int safeCount = math.min(runCount, runs.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ThermalGridRleRun run = runs[i];
                checksum = MixChecksum32(checksum, run.StartIndex);
                checksum = MixChecksum32(checksum, run.Count);
                checksum = MixChecksum32(checksum, math.asuint(run.TemperatureCelsius));
            }

            return TryComputeThermalGridRleByteCount(safeCount, out byteCount);
        }

        internal static bool TryStageThermalGridRleDelta(
            ThermalGridRleRun[] runs,
            int runCount,
            out int byteCount,
            out uint checksum)
        {
            byteCount = 0;
            checksum = 2166136261u;
            if (runs == null || runCount < 0 || runCount > runs.Length)
                return false;

            int safeCount = math.min(runCount, runs.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ThermalGridRleRun run = runs[i];
                checksum = MixChecksum32(checksum, run.StartIndex);
                checksum = MixChecksum32(checksum, run.Count);
                checksum = MixChecksum32(checksum, math.asuint(run.TemperatureCelsius));
            }

            return TryComputeThermalGridRleByteCount(safeCount, out byteCount);
        }

        private static bool TryComputeThermalGridRleByteCount(int runCount, out int byteCount)
        {
            byteCount = 0;
            if (runCount < 0)
                return false;

            long byteCountLong = (long)runCount * UnsafeUtility.SizeOf<ThermalGridRleRun>();
            if (byteCountLong > int.MaxValue)
                return false;

            byteCount = (int)byteCountLong;
            return true;
        }

        [StructLayout(LayoutKind.Explicit, Size = PayloadPrefixSizeBytes)]
        private struct PayloadPrefix
        {
            [FieldOffset(0)]
            public ulong TimestampUnixMs;
            [FieldOffset(8)]
            public float PlayTimeSeconds;
            [FieldOffset(12)]
            public AbsoluteUniversePosition PlayerPosition;
            [FieldOffset(60)]
            public int SaveDataVersion;
            [FieldOffset(64)]
            public uint SaveDataByteLength;
            [FieldOffset(68)]
            public ushort SceneNameByteLength;
            [FieldOffset(70)]
            public ushort GameVersionByteLength;
        }

        [StructLayout(LayoutKind.Explicit, Size = LegacyPersistentWorldSectionHeaderSize)]
        private struct LegacyPersistentWorldSectionHeader12
        {
            [FieldOffset(0)]
            public uint ChunkCount;
            [FieldOffset(4)]
            public uint ItemHashCount;
            [FieldOffset(8)]
            public uint RecordCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = PersistentWorldSectionHeaderSize)]
        private struct PersistentWorldSectionHeader
        {
            [FieldOffset(0)]
            public uint ChunkCount;
            [FieldOffset(4)]
            public uint ItemHashCount;
            [FieldOffset(8)]
            public uint RecordCount;
            [FieldOffset(12)]
            public uint Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = LegacyEcosystemSectionHeaderSize)]
        private struct LegacyEcosystemSectionHeader4
        {
            [FieldOffset(0)]
            public uint RecordCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = EcosystemSectionHeaderSize)]
        private struct EcosystemSectionHeader
        {
            [FieldOffset(0)]
            public uint RecordCount;
            [FieldOffset(4)]
            public uint Reserved0;
        }

        private static int ResolvePersistentWorldSectionHeaderSize(ushort saveVersion)
        {
            return saveVersion >= AlignedSectionHeaderVersion
                ? PersistentWorldSectionHeaderSize
                : LegacyPersistentWorldSectionHeaderSize;
        }

        private static int ResolveEcosystemSectionHeaderSize(ushort saveVersion)
        {
            return saveVersion >= AlignedSectionHeaderVersion
                ? EcosystemSectionHeaderSize
                : LegacyEcosystemSectionHeaderSize;
        }

        private static PersistentWorldSectionHeader ReadPersistentWorldSectionHeader(byte* sectionPtr, int headerSize)
        {
            if (headerSize == LegacyPersistentWorldSectionHeaderSize)
            {
                LegacyPersistentWorldSectionHeader12 legacy =
                    UnsafeUtility.ReadArrayElement<LegacyPersistentWorldSectionHeader12>(sectionPtr, 0);
                return new PersistentWorldSectionHeader
                {
                    ChunkCount = legacy.ChunkCount,
                    ItemHashCount = legacy.ItemHashCount,
                    RecordCount = legacy.RecordCount,
                    Reserved0 = 0u
                };
            }

            return UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(sectionPtr, 0);
        }

        private static void WritePersistentWorldSectionHeader(
            byte* sectionPtr,
            int headerSize,
            in PersistentWorldSectionHeader header)
        {
            if (headerSize == LegacyPersistentWorldSectionHeaderSize)
            {
                LegacyPersistentWorldSectionHeader12 legacy = new LegacyPersistentWorldSectionHeader12
                {
                    ChunkCount = header.ChunkCount,
                    ItemHashCount = header.ItemHashCount,
                    RecordCount = header.RecordCount
                };
                UnsafeUtility.CopyStructureToPtr(ref legacy, sectionPtr);
                return;
            }

            PersistentWorldSectionHeader aligned = header;
            aligned.Reserved0 = 0u;
            UnsafeUtility.CopyStructureToPtr(ref aligned, sectionPtr);
        }

        private static EcosystemSectionHeader ReadEcosystemSectionHeader(byte* sectionPtr, int headerSize)
        {
            if (headerSize == LegacyEcosystemSectionHeaderSize)
            {
                LegacyEcosystemSectionHeader4 legacy =
                    UnsafeUtility.ReadArrayElement<LegacyEcosystemSectionHeader4>(sectionPtr, 0);
                return new EcosystemSectionHeader
                {
                    RecordCount = legacy.RecordCount,
                    Reserved0 = 0u
                };
            }

            return UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(sectionPtr, 0);
        }

        private static void WriteEcosystemSectionHeader(
            byte* sectionPtr,
            int headerSize,
            in EcosystemSectionHeader header)
        {
            if (headerSize == LegacyEcosystemSectionHeaderSize)
            {
                LegacyEcosystemSectionHeader4 legacy = new LegacyEcosystemSectionHeader4
                {
                    RecordCount = header.RecordCount
                };
                UnsafeUtility.CopyStructureToPtr(ref legacy, sectionPtr);
                return;
            }

            EcosystemSectionHeader aligned = header;
            aligned.Reserved0 = 0u;
            UnsafeUtility.CopyStructureToPtr(ref aligned, sectionPtr);
        }

        private static bool TryConvertSectionCount(uint source, out int count)
        {
            if (source > int.MaxValue)
            {
                count = 0;
                return false;
            }

            count = (int)source;
            return true;
        }

        private static bool TryConvertPersistentWorldSectionCounts(
            in PersistentWorldSectionHeader sectionHeader,
            out int recordCount,
            out int chunkCount,
            out int itemHashCount)
        {
            if (!TryConvertSectionCount(sectionHeader.RecordCount, out recordCount) ||
                !TryConvertSectionCount(sectionHeader.ChunkCount, out chunkCount) ||
                !TryConvertSectionCount(sectionHeader.ItemHashCount, out itemHashCount))
            {
                recordCount = 0;
                chunkCount = 0;
                itemHashCount = 0;
                return false;
            }

            return true;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct PersistentWorldSaveRecord16
        {
            [FieldOffset(0)] public uint PackedLocalPosition;
            [FieldOffset(4)] public uint InstanceUid;
            [FieldOffset(8)] public ushort Quantity;
            [FieldOffset(10)] public byte ItemFlags;
            [FieldOffset(11)] public byte Reserved;
            [FieldOffset(12)] public ushort ChunkIndex;
            [FieldOffset(14)] public ushort ItemHashIndex;
        }

        // Legacy v3/v4 file-format record only. Do not use in NativeArray/Burst/runtime DTO paths.
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct PersistentWorldDeltaRecordLegacy64
        {
            [FieldOffset(0)] public int3 ChunkId;
            [FieldOffset(12)] private uint _pad0;
            [FieldOffset(16)] public ulong ItemPersistentIdHash;
            [FieldOffset(24)] public uint InstanceUid;
            [FieldOffset(28)] public uint PackedLocalPosition;
            [FieldOffset(32)] public ushort Quantity;
            [FieldOffset(34)] public byte ItemFlags;
            [FieldOffset(35)] public byte Reserved;
            [FieldOffset(36)] private uint _pad1;
            [FieldOffset(40)] private ulong _pad2;
            [FieldOffset(48)] private ulong _pad3;
            [FieldOffset(56)] private ulong _pad4;

            public PersistentWorldDeltaRecord ToRuntimeRecord()
            {
                return new PersistentWorldDeltaRecord
                {
                    ItemPersistentIdHash = ItemPersistentIdHash,
                    ChunkId = ChunkId,
                    InstanceUid = InstanceUid,
                    PackedLocalPosition = PackedLocalPosition,
                    Quantity = Quantity,
                    ItemFlags = ItemFlags,
                    Reserved = Reserved
                };
            }
        }

        internal static void WarmRuntime()
        {
            byte value = 0;
            _ = Hash64(&value, 1L);
            Interlocked.Exchange(ref s_indexedSectorQuarantineReported, 0);
            Interlocked.Exchange(ref s_indexedSectorBackupRecoveryReported, 0);
            lock (s_indexedSectorQuarantineLock)
            {
                Array.Clear(s_indexedSectorQuarantineHashes, 0, s_indexedSectorQuarantineHashes.Length);
                s_indexedSectorQuarantineHashCount = 0;
            }

            lock (s_indexedSectorBackupRecoveryLock)
            {
                Array.Clear(s_indexedSectorBackupRecoveryHashes, 0, s_indexedSectorBackupRecoveryHashes.Length);
                s_indexedSectorBackupRecoveryHashCount = 0;
            }
        }

        internal static bool IsBinaryContainer(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return false;

            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out _)
                || fileLength < SaveFileHeaderPrefixSize)
            {
                return false;
            }

            return TryReadHeaderPrefixFastFail(absolutePath, out _, out _);
        }

        private static void SetReadErrorCode(int errorCode)
        {
            Volatile.Write(ref s_lastReadErrorCode, errorCode);
        }

        private static bool TryReadHeaderPrefixFastFail(
            string absolutePath,
            out SaveFileHeaderPrefix prefix,
            out string error)
        {
            prefix = default;
            SaveFileHeaderPrefix prefixScratch = default;
            if (!AsyncWriteManager.TryReadAll(absolutePath, &prefixScratch, SaveFileHeaderPrefixSize, out error))
            {
                SetReadErrorCode(ErrorCodeHeaderReadFailed);
                return false;
            }

            prefix = prefixScratch;
            if (prefix.MagicValue != Magic)
            {
                error = "Save file magic mismatch.";
                SetReadErrorCode(ErrorCodeMagicMismatch);
                return false;
            }

            SetReadErrorCode(ErrorCodeNone);
            return true;
        }

        internal static bool TryWriteSaveFile(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            ushort playerDialogueChoiceFlags,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out string error)
        {
            return TryWriteSaveFileIndexedV8(
                absolutePath,
                metadata,
                data,
                persistentWorldDeltas,
                ecosystemSectorStates,
                packedQuestHeader,
                packedQuestStateWords,
                playerDialogueChoiceFlags,
                voxelDeltaSnapshot,
                rawBuffer,
                compressedBuffer,
                DefaultIndexedPersistentWorldChunkSizeMeters,
                out payloadHash64,
                out rawPayloadLength,
                out error);
        }

        internal static bool TryReadMetadata(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out SaveMetadata metadata,
            out int detectedVersion,
            out string error)
        {
            metadata = null;
            detectedVersion = 0;

            if (TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping v8Mapping, out SaveFileHeader v8Header, out _, out string headerError))
            {
                try
                {
                    if ((v8Header.Flags & FlagIndexedSectorBlocks) != 0 && v8Header.Version >= IndexedBlockStorageVersion)
                    {
                        return TryReadMetadataIndexedV8(absolutePath, slotName, rawBuffer, in v8Header, ref v8Mapping, out metadata, out detectedVersion, out error);
                    }
                }
                finally
                {
                    AsyncWriteManager.CloseReadOnlyMapping(ref v8Mapping);
                }
            }
            else if (!string.IsNullOrEmpty(headerError))
            {
                error = headerError;
                return false;
            }

            if (!TryReadPayload(absolutePath, rawBuffer, compressedBuffer, out SaveFileHeader header, out PayloadPrefixInfo prefix, out byte* rawPtr, out int rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            if (!TryToRuntimePosition(prefix.PlayerPosition, out Vector3 playerPosition))
            {
                error = "Save metadata contains an invalid player AUP position.";
                return false;
            }

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = SaveMetadata.NormalizeSceneName(sceneName),
                PlayerPosition = playerPosition,
                Checksum = FormatPayloadChecksum(in header)
            };

            error = string.Empty;
            return true;
        }

        private static bool TryWriteSaveFileIndexedV8(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            ushort playerDialogueChoiceFlagsSnapshot,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            int chunkSizeMeters,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out string error)
        {
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Save path is empty.";
                return false;
            }

            if (!rawBuffer.IsCreated || !compressedBuffer.IsCreated)
            {
                error = "Native save buffers are not initialized.";
                return false;
            }

            if (metadata == null || data == null)
            {
                error = "Save payload is null.";
                return false;
            }

            if (!TryValidateCurrentBinaryLayouts(out error))
                return false;

            if (chunkSizeMeters <= 0)
            {
                error = "Indexed persistent-world chunk size is invalid.";
                return false;
            }

            string sceneName = SaveMetadata.NormalizeSceneName(metadata.SceneName);
            string gameVersion = string.IsNullOrEmpty(metadata.GameVersion) ? "Unknown" : metadata.GameVersion;
            long sceneBytesLengthLong = (long)sceneName.Length * sizeof(char);
            long versionBytesLengthLong = (long)gameVersion.Length * sizeof(char);
            if (sceneBytesLengthLong > ushort.MaxValue || versionBytesLengthLong > ushort.MaxValue)
            {
                error = "Save metadata strings exceed the payload prefix limits.";
                return false;
            }

            int sceneBytesLength = (int)sceneBytesLengthLong;
            int versionBytesLength = (int)versionBytesLengthLong;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);
            UnsafeUtility.MemClear(rawPtr, rawBuffer.Length);
            UnsafeUtility.MemClear(filePtr, compressedBuffer.Length);

            int packedQuestWordCount = packedQuestStateWords.IsCreated ? packedQuestStateWords.Length : 0;
            ushort playerDialogueChoiceFlags =
                (ushort)(playerDialogueChoiceFlagsSnapshot | ExtractPlayerDialogueChoiceFlags(packedQuestStateWords));
            if (!TryEncodeHeaderDeltaCount(packedQuestWordCount, playerDialogueChoiceFlags, out uint headerDeltaCount, out error))
                return false;

            int ecosystemSectorCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
            int voxelDeltaByteLength = voxelDeltaSnapshot.IsCreated ? voxelDeltaSnapshot.Length : 0;
            int packedQuestSectionLength = packedQuestWordCount > 0
                ? PackedQuestStateSectionHeaderSize + (packedQuestWordCount * UnsafeUtility.SizeOf<uint>())
                : 0;
            if (!TryComputeEcosystemSectionLength(ecosystemSectorCount, CurrentVersion, out int ecosystemSectionLength))
            {
                error = "Ecosystem section exceeds supported bounds.";
                return false;
            }

            int metadataCursor = PayloadPrefixSizeBytes + sceneBytesLength + versionBytesLength;
            if (!SaveBinaryPayloadCodec.TryWrite(data, AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, out int saveDataByteLength, out error))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(metadata.PlayerPosition, out AbsoluteUniversePosition playerAup))
            {
                error = "Save metadata contains an invalid runtime player position.";
                return false;
            }

            ulong timestampUnixMs = ToUnixMilliseconds(metadata.Timestamp);
            PayloadPrefix prefix = new PayloadPrefix
            {
                TimestampUnixMs = timestampUnixMs,
                PlayTimeSeconds = metadata.PlayTimeSeconds,
                PlayerPosition = playerAup,
                SaveDataVersion = math.max(data.version, 0),
                SaveDataByteLength = (uint)saveDataByteLength,
                SceneNameByteLength = (ushort)sceneBytesLength,
                GameVersionByteLength = (ushort)versionBytesLength
            };

            UnsafeUtility.CopyStructureToPtr(ref prefix, rawPtr);
            metadataCursor = PayloadPrefixSizeBytes;
            if (!TryCopyUtf16StringToUnmanaged(sceneName, AddByteOffset(rawPtr, metadataCursor), sceneBytesLength, out error))
                return false;
            metadataCursor += sceneBytesLength;
            if (!TryCopyUtf16StringToUnmanaged(gameVersion, AddByteOffset(rawPtr, metadataCursor), versionBytesLength, out error))
                return false;
            metadataCursor += versionBytesLength;
            metadataCursor += saveDataByteLength;
            int packedQuestOffsetInMetadataPayload = metadataCursor;

            if (packedQuestWordCount > 0)
            {
                QuestSaveHeader serializedQuestHeader = packedQuestHeader;
                serializedQuestHeader.Magic = QuestSaveHeader.HeaderMagic;
                serializedQuestHeader.FlagCount = (uint)packedQuestWordCount;
                serializedQuestHeader.WriteSchemaVersion();
                serializedQuestHeader.Checksum = ComputePackedQuestStateChecksum(packedQuestStateWords);
                UnsafeUtility.CopyStructureToPtr(ref serializedQuestHeader, AddByteOffset(rawPtr, metadataCursor));
                metadataCursor += PackedQuestStateSectionHeaderSize;

                void* packedQuestSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateWords);
                int packedQuestBytes = packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, packedQuestSourcePtr, packedQuestBytes))
                {
                    error = "Packed quest-state write exceeded metadata buffer bounds.";
                    return false;
                }

                metadataCursor += packedQuestBytes;
            }

            int ecosystemOffsetInMetadataPayload = metadataCursor;
            if (!WriteEcosystemSection(AddByteOffset(rawPtr, metadataCursor), ecosystemSectorStates, out error))
                return false;
            metadataCursor += ecosystemSectionLength;

            if (voxelDeltaByteLength > 0)
            {
                void* voxelSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, voxelSourcePtr, voxelDeltaByteLength))
                {
                    error = "Voxel delta snapshot write exceeded metadata buffer bounds.";
                    return false;
                }

                metadataCursor += voxelDeltaByteLength;
            }

            int metadataRawLength = metadataCursor;
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);
            uint metadataChecksum = unchecked((uint)metadataHash64);

            IndexedSectorGroupBuffer sectorGroups = BuildIndexedSectorGroups(persistentWorldDeltas, chunkSizeMeters);
            if (sectorGroups.InvalidRecordCount > 0)
            {
                error = "Indexed persistent world save contains invalid AUP sector records.";
                sectorGroups.Dispose();
                return false;
            }

            int sectorCount = sectorGroups.Count;
            if (sectorCount > IndexedSectorDirectorySlotCount)
            {
                long overflowSectorHash = sectorGroups.Groups[IndexedSectorDirectorySlotCount].SectorHash;
                ReportIndexedSectorDirectoryCapacityExceeded(overflowSectorHash, sectorCount);
                sectorCount = IndexedSectorDirectorySlotCount;
            }

            int sectorEntrySize = ResolveIndexedSectorEntrySize(CurrentVersion);
            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * sectorEntrySize);
            int metadataBlockOffset = CurrentHeaderSize + directoryBytes;
            int fileCursor = metadataBlockOffset;

            bool anyTokenSubstitution = false;
            int metadataCompressedSize = 0;
            if (!TryWriteIndexedCompressedBlock(
                    rawPtr,
                    metadataRawLength,
                    filePtr,
                    compressedBuffer.Length,
                    ref fileCursor,
                    out metadataCompressedSize,
                    out uint metadataBlockFlags,
                    out error))
            {
                sectorGroups.Dispose();
                return false;
            }

            anyTokenSubstitution |= (metadataBlockFlags & FlagTokenSubstitution) != 0;

            using RegisteredTransientNativeArray<SectorEntry> sectorEntriesOwner = CreateRegisteredTransientNativeArray<SectorEntry>(
                IndexedSectorDirectorySlotCount,
                NativeArrayOptions.ClearMemory,
                IndexedSectorDirectoryScratchLabel);
            NativeArray<SectorEntry> sectorEntries = sectorEntriesOwner.Array;
            if (!TryCountIndexedSectorRecords(sectorGroups, sectorCount, out int totalEntityCount))
            {
                error = "Indexed persistent-world entity count exceeds supported bounds.";
                sectorGroups.Dispose();
                return false;
            }

            NativeParallelHashMap<int3, ushort> persistentWorldChunkLookup = default;
            NativeList<int3> persistentWorldChunkTable = default;
            NativeParallelHashMap<ulong, ushort> persistentWorldItemHashLookup = default;
            NativeList<ulong> persistentWorldItemHashTable = default;
            PersistentWorldSectionTableSentinelIds persistentWorldSectionTableSentinelIds = default;

            try
            {
                for (int sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
                {
                    IndexedSectorGroup group = sectorGroups.Groups[sectorIndex];
                    int recordCount = group.Count;
                    if (recordCount <= 0)
                    {
                        continue;
                    }

                    if (!TryBuildPersistentWorldSectionTables(
                            sectorGroups.Records,
                            group.StartIndex,
                            recordCount,
                            out persistentWorldChunkLookup,
                            out persistentWorldChunkTable,
                            out persistentWorldItemHashLookup,
                            out persistentWorldItemHashTable,
                            out persistentWorldSectionTableSentinelIds,
                            out error))
                    {
                        return false;
                    }

                    if (!TryComputePersistentWorldSectionLength(
                            recordCount,
                            persistentWorldChunkTable.Length,
                            persistentWorldItemHashTable.Length,
                            CurrentVersion,
                            out int sectorRawLength))
                    {
                        error = "Indexed persistent-world sector section exceeds supported bounds.";
                        return false;
                    }

                    UnsafeUtility.MemClear(rawPtr, sectorRawLength);
                    if (!WritePersistentWorldSection(
                            rawPtr,
                            sectorGroups.Records,
                            group.StartIndex,
                            recordCount,
                            persistentWorldChunkLookup,
                            persistentWorldChunkTable,
                            persistentWorldItemHashLookup,
                            persistentWorldItemHashTable,
                            out error))
                    {
                        return false;
                    }

                    uint sectorChecksum = Hash32(rawPtr, sectorRawLength);
                    long sectorByteOffset = fileCursor;
                    if (!TryWriteIndexedCompressedBlock(
                            rawPtr,
                            sectorRawLength,
                            filePtr,
                            compressedBuffer.Length,
                            ref fileCursor,
                            out int sectorCompressedSize,
                            out uint sectorBlockFlags,
                            out error))
                    {
                        return false;
                    }

                    anyTokenSubstitution |= (sectorBlockFlags & FlagTokenSubstitution) != 0;
                    if (!TryAssignIndexedSectorEntryAtKnownSlot(sectorEntries, group.DirectorySlot, group.SectorHash, new SectorEntry
                    {
                        SectorHash = group.SectorHash,
                        ByteOffset = sectorByteOffset,
                        CompressedSize = sectorCompressedSize,
                        DecompressedSize = sectorRawLength,
                        Checksum = sectorChecksum
                    }, out error))
                    {
                        return false;
                    }

                    DisposePersistentWorldSectionTables(
                        ref persistentWorldChunkLookup,
                        ref persistentWorldChunkTable,
                        ref persistentWorldItemHashLookup,
                        ref persistentWorldItemHashTable,
                        ref persistentWorldSectionTableSentinelIds);
                }
            }
            finally
            {
                DisposePersistentWorldSectionTables(
                    ref persistentWorldChunkLookup,
                    ref persistentWorldChunkTable,
                    ref persistentWorldItemHashLookup,
                    ref persistentWorldItemHashTable,
                    ref persistentWorldSectionTableSentinelIds);
                sectorGroups.Dispose();
            }

            IndexedSectorDirectoryHeader directoryHeader = new IndexedSectorDirectoryHeader
            {
                SectorCount = (uint)sectorCount,
                ChunkSizeMeters = chunkSizeMeters,
                MetadataCompressedSize = metadataCompressedSize,
                MetadataDecompressedSize = metadataRawLength
            };
            UnsafeUtility.CopyStructureToPtr(ref directoryHeader, filePtr + CurrentHeaderSize);

            int directoryCursor = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize;
            for (int i = 0; i < sectorEntries.Length; i++)
            {
                SectorEntry entry = sectorEntries[i];
                WriteIndexedSectorEntry(filePtr + directoryCursor, sectorEntrySize, in entry);
                directoryCursor += sectorEntrySize;
            }

            ulong directoryHash64 = Hash64(filePtr + CurrentHeaderSize, directoryBytes);
            payloadHash64 = metadataHash64 ^ directoryHash64;
            uint checksumRoot = ComputeIndexedChecksumRoot(metadataChecksum, sectorEntries);
            rawPayloadLength = metadataRawLength;

            SaveFileHeader header = new SaveFileHeader
            {
                MagicValue = Magic,
                Version = CurrentVersion,
                CompatMask = CurrentCompatMask,
                Flags = (byte)(FlagLz4Blocks | FlagIndexedSectorBlocks | FlagProtectedLz4Blocks),
                TimestampUnixMs = timestampUnixMs,
                Checksum = checksumRoot,
                DeltaCount = headerDeltaCount,
                EntityCount = (uint)math.max(totalEntityCount, 0),
                PlayerOffset = (uint)metadataBlockOffset,
                DeltaOffset = (uint)(metadataBlockOffset + packedQuestOffsetInMetadataPayload),
                EntityOffset = (uint)metadataBlockOffset,
                HashPayload64 = payloadHash64,
                HashHeader64 = 0UL
            };

            if (anyTokenSubstitution)
                header.Flags |= FlagTokenSubstitution;

            header.HashHeader64 = ComputeHeaderHash(ref header);
            UnsafeUtility.CopyStructureToPtr(ref header, filePtr);

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!AsyncWriteManager.WriteAllPaged(absolutePath, filePtr, fileCursor, out error))
                return false;

            metadata.Checksum = FormatPayloadChecksum(in header);
            return true;
        }

        private static bool TryReadValidatedHeader(
            string absolutePath,
            out AsyncWriteManager.ReadOnlyMapping mapping,
            out SaveFileHeader header,
            out int headerSizeBytes,
            out string error)
        {
            mapping = default;
            header = default;
            headerSizeBytes = 0;
            error = string.Empty;

            if (!TryReadHeaderPrefixFastFail(absolutePath, out SaveFileHeaderPrefix fastPrefix, out error))
                return false;

            headerSizeBytes = ResolveHeaderSize(fastPrefix.Version);
            if (headerSizeBytes <= 0)
            {
                error = "Unsupported save header version.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out mapping, out error))
                return false;

            bool success = false;
            try
            {
                if (mapping.Length < SaveFileHeaderPrefixSize)
                {
                    error = "Save file is smaller than the header prefix.";
                    return false;
                }

                byte* filePtr = (byte*)mapping.View;
                SaveFileHeaderPrefix headerPrefix = UnsafeUtility.ReadArrayElement<SaveFileHeaderPrefix>(filePtr, 0);
                if (headerPrefix.MagicValue != Magic)
                {
                    error = "Save file magic mismatch.";
                    SetReadErrorCode(ErrorCodeMagicMismatch);
                    return false;
                }

                headerSizeBytes = ResolveHeaderSize(headerPrefix.Version);
                if (headerSizeBytes <= 0)
                {
                    error = $"Unsupported save header version {headerPrefix.Version}.";
                    return false;
                }

                if (mapping.Length < headerSizeBytes)
                {
                    error = "Save file is smaller than its declared header size.";
                    return false;
                }

                header = ReadVersionedSaveFileHeader(filePtr, headerPrefix.Version);

                if (!TryValidateHeader(header, out error))
                    return false;

                ulong expectedHeaderHash = ComputeHeaderHash(ref header);
                if (expectedHeaderHash != header.HashHeader64)
                {
                    error = $"Save header checksum mismatch. Expected 0x{expectedHeaderHash:X16}, found 0x{header.HashHeader64:X16}.";
                    return false;
                }

                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadCloudMetadataHeader128(
            string absolutePath,
            out SaveCloudMetadataHeader metadata,
            out string error)
        {
            metadata = default;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Cloud metadata header path is empty.";
                return false;
            }

            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength < SaveFileHeaderPrefixSize)
            {
                error = "Save file is smaller than the cloud metadata header prefix.";
                return false;
            }

            int bytesToRead = (int)(fileLength < 128L ? fileLength : 128L);
            byte* headerBytes = stackalloc byte[128];
            UnsafeUtility.MemClear(headerBytes, 128);
            if (!AsyncWriteManager.TryCopyFromCachedReadWindow(absolutePath, 0L, headerBytes, bytesToRead, out error))
                return false;

            SaveFileHeaderPrefix prefix = UnsafeUtility.ReadArrayElement<SaveFileHeaderPrefix>(headerBytes, 0);
            if (prefix.MagicValue != Magic)
            {
                error = "Cloud metadata header magic mismatch.";
                SetReadErrorCode(ErrorCodeMagicMismatch);
                return false;
            }

            SetReadErrorCode(ErrorCodeNone);

            int headerSize = ResolveHeaderSize(prefix.Version);
            if (headerSize <= 0 || headerSize > bytesToRead)
            {
                error = "Cloud metadata header is truncated or unsupported.";
                return false;
            }

            SaveFileHeader header = ReadVersionedSaveFileHeader(headerBytes, prefix.Version);
            if (!TryValidateHeader(header, out error))
                return false;

            metadata = new SaveCloudMetadataHeader
            {
                MagicValue = header.MagicValue,
                Version = header.Version,
                CompatMask = header.CompatMask,
                Flags = header.Flags,
                Checksum = header.Checksum,
                TimestampUnixMs = header.TimestampUnixMs,
                HashPayload64 = header.HashPayload64,
                HashHeader64 = header.HashHeader64,
                PlayerOffset = header.PlayerOffset,
                DeltaOffset = header.DeltaOffset,
                EntityOffset = header.EntityOffset,
                Reserved0 = DecodePlayerDialogueChoiceFlags(in header)
            };
            return true;
        }

        internal static bool TryUploadMappedHeightmapToGraphicsBuffer<T>(
            string absolutePath,
            long byteOffset,
            int elementCount,
            GraphicsBuffer destination,
            out string error)
            where T : struct
        {
            return AsyncWriteManager.TryUploadCachedReadWindowToGraphicsBuffer<T>(
                absolutePath,
                byteOffset,
                elementCount,
                destination,
                out error);
        }

        private static bool TryReadIndexedDirectory(
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out IndexedSectorDirectoryHeader directoryHeader,
            out SectorEntry[] sectorEntries,
            out string error)
        {
            directoryHeader = default;
            sectorEntries = null;
            error = string.Empty;

            if (!TryValidateIndexedBlockStorageHeader(in header, out error))
            {
                return false;
            }

            int directoryOffset = ResolveExpectedHeaderSize(header.Version);
            if (mapping.Length < directoryOffset ||
                (long)directoryOffset > mapping.Length - IndexedSectorDirectoryHeaderSize)
            {
                error = "Indexed sector directory header is truncated.";
                return false;
            }

            byte* filePtr = (byte*)mapping.View;
            directoryHeader = UnsafeUtility.ReadArrayElement<IndexedSectorDirectoryHeader>(filePtr + directoryOffset, 0);
            if (!TryDecodeIndexedSectorCount(in directoryHeader, out int sectorCount, out error))
                return false;
            if (directoryHeader.ChunkSizeMeters <= 0)
            {
                error = "Indexed sector directory chunk size is invalid.";
                return false;
            }

            int sectorEntrySize = ResolveIndexedSectorEntrySize(header.Version);
            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * sectorEntrySize);
            if ((long)directoryOffset > mapping.Length - directoryBytes)
            {
                error = "Indexed sector directory exceeds the file bounds.";
                return false;
            }

            long directoryEndOffset = directoryOffset + (long)directoryBytes;
            long metadataOffset = header.PlayerOffset;
            if (metadataOffset < directoryEndOffset || metadataOffset >= mapping.Length)
            {
                error = "Indexed metadata block offset is out of bounds.";
                return false;
            }

            if (directoryHeader.MetadataCompressedSize < 0 ||
                metadataOffset > mapping.Length - directoryHeader.MetadataCompressedSize)
            {
                error = "Indexed metadata block size is out of bounds.";
                return false;
            }

            long metadataEndOffset = metadataOffset + directoryHeader.MetadataCompressedSize;
            sectorEntries = new SectorEntry[IndexedSectorDirectorySlotCount];
            int entryCursor = directoryOffset + IndexedSectorDirectoryHeaderSize;
            int populatedCount = 0;
            for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
            {
                SectorEntry entry = ReadIndexedSectorEntry(filePtr + entryCursor, sectorEntrySize);
                if (IsIndexedSectorEntryPopulated(in entry) &&
                    !IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                {
                    error = "Indexed sector entry exceeded the file bounds.";
                    return false;
                }

                sectorEntries[i] = entry;
                if (IsIndexedSectorEntryPopulated(in entry))
                    populatedCount++;
                entryCursor += sectorEntrySize;
            }

            if (populatedCount != sectorCount)
            {
                error = "Indexed sector directory count mismatch.";
                return false;
            }

            return true;
        }

        private static bool TryDecodeIndexedSectorCount(
            in IndexedSectorDirectoryHeader directoryHeader,
            out int sectorCount,
            out string error)
        {
            sectorCount = 0;
            if (directoryHeader.SectorCount > IndexedSectorDirectorySlotCount)
            {
                error = $"Indexed sector directory count {directoryHeader.SectorCount} exceeded slot capacity {IndexedSectorDirectorySlotCount}.";
                return false;
            }

            sectorCount = (int)directoryHeader.SectorCount;
            error = string.Empty;
            return true;
        }

        private static bool TryValidateIndexedBlockStorageHeader(in SaveFileHeader header, out string error)
        {
            error = string.Empty;
            if ((header.Flags & FlagIndexedSectorBlocks) == 0)
            {
                error = "Save header is not an indexed sector container.";
                return false;
            }

            if (header.Version < IndexedBlockStorageVersion || header.Version > CurrentVersion)
            {
                error = $"Indexed sector block decompression requires save header version >= 0x{IndexedBlockStorageVersion:X4}. Found 0x{header.Version:X4}.";
                return false;
            }

            return true;
        }

        private static bool IsIndexedSectorEntryPopulated(in SectorEntry entry)
        {
            return entry.CompressedSize > 0 && entry.ByteOffset >= CurrentHeaderSize;
        }

        private static bool IsIndexedSectorEntryWithinFileBounds(
            in SectorEntry entry,
            long minimumByteOffset,
            long fileLength)
        {
            if (!IsIndexedSectorEntryPopulated(in entry) ||
                minimumByteOffset < CurrentHeaderSize ||
                fileLength < minimumByteOffset)
            {
                return false;
            }

            return SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds(
                entry.ByteOffset,
                entry.CompressedSize,
                minimumByteOffset,
                fileLength);
        }

        private static int ResolveIndexedSectorDirectorySlot(long sectorHash)
        {
            ulong hash = unchecked((ulong)sectorHash);
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            return (int)(hash & (IndexedSectorDirectorySlotCount - 1));
        }

        private static bool TryAssignIndexedSectorEntry(SectorEntry[] sectorEntries, long sectorHash, in SectorEntry entry, out string error)
        {
            error = string.Empty;
            if (sectorEntries == null || sectorEntries.Length != IndexedSectorDirectorySlotCount)
            {
                error = "Indexed sector directory slot buffer is invalid.";
                return false;
            }

            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                if (!IsIndexedSectorEntryPopulated(in sectorEntries[slot]) || sectorEntries[slot].SectorHash == sectorHash)
                {
                    sectorEntries[slot] = entry;
                    return true;
                }
            }

            error = $"Indexed sector directory is full while assigning sector 0x{sectorHash:X16}.";
            ReportIndexedSectorDirectoryCapacityExceeded(sectorHash, IndexedSectorDirectorySlotCount + 1);
            return false;
        }

        private static bool TryAssignIndexedSectorEntryAtKnownSlot(
            NativeArray<SectorEntry> sectorEntries,
            int knownSlot,
            long sectorHash,
            in SectorEntry entry,
            out string error)
        {
            error = string.Empty;
            if (!sectorEntries.IsCreated || sectorEntries.Length != IndexedSectorDirectorySlotCount)
            {
                error = "Indexed sector native directory buffer is invalid.";
                return false;
            }

            if ((uint)knownSlot >= IndexedSectorDirectorySlotCount)
            {
                error = "Indexed sector known directory slot is invalid.";
                return false;
            }

            SectorEntry existingEntry = sectorEntries[knownSlot];
            if (!IsIndexedSectorEntryPopulated(in existingEntry) || existingEntry.SectorHash == sectorHash)
            {
                sectorEntries[knownSlot] = entry;
                return true;
            }

            error = "Indexed sector known slot collision while assigning sector.";
            return false;
        }

        private static bool TryFindIndexedSectorEntryIndex(SectorEntry[] sectorEntries, long sectorHash, out int slotIndex)
        {
            slotIndex = -1;
            if (sectorEntries == null || sectorEntries.Length != IndexedSectorDirectorySlotCount)
                return false;

            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                SectorEntry entry = sectorEntries[slot];
                if (!IsIndexedSectorEntryPopulated(in entry))
                    return false;

                if (entry.SectorHash == sectorHash)
                {
                    slotIndex = slot;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveIndexedSectorCommitTarget(
            SectorEntry[] sectorEntries,
            long sectorHash,
            int overrideCompressedSize,
            long originalLength,
            out IndexedSectorCommitTarget commitTarget,
            out int sectorCountDelta,
            out string error)
        {
            commitTarget = default;
            sectorCountDelta = 0;
            error = string.Empty;

            if (sectorEntries == null || sectorEntries.Length != IndexedSectorDirectorySlotCount)
            {
                error = "Indexed sector commit directory buffer is invalid.";
                return false;
            }

            if (overrideCompressedSize <= 0 || originalLength < CurrentHeaderSize)
            {
                error = "Indexed sector commit sizing is invalid.";
                return false;
            }

            if (!TryResolveAppendFileLength(originalLength, overrideCompressedSize, out long appendFileLength, out error))
                return false;

            if (TryFindIndexedSectorEntryIndex(sectorEntries, sectorHash, out int existingSlotIndex))
            {
                commitTarget = new IndexedSectorCommitTarget(
                    reusedExistingSlot: false,
                    insertedNewSlot: false,
                    slotIndex: existingSlotIndex,
                    writeOffset: originalLength,
                    newFileLength: appendFileLength);
                return true;
            }

            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                if (IsIndexedSectorEntryPopulated(in sectorEntries[slot]))
                    continue;

                commitTarget = new IndexedSectorCommitTarget(
                    reusedExistingSlot: false,
                    insertedNewSlot: true,
                    slotIndex: slot,
                    writeOffset: originalLength,
                    newFileLength: appendFileLength);
                sectorCountDelta = 1;
                return true;
            }

            error = $"Indexed sector directory is full while resolving commit target for sector 0x{sectorHash:X16}.";
            ReportIndexedSectorDirectoryCapacityExceeded(sectorHash, IndexedSectorDirectorySlotCount + 1);
            return false;
        }

        private static bool TryResolveAppendFileLength(
            long originalLength,
            int appendBytes,
            out long newFileLength,
            out string error)
        {
            newFileLength = 0L;
            error = string.Empty;
            if (originalLength < 0L || appendBytes <= 0 || originalLength > long.MaxValue - appendBytes)
            {
                error = "Indexed sector append length overflowed the file range.";
                return false;
            }

            newFileLength = originalLength + appendBytes;
            return true;
        }

        private static bool IsMmfMoveRangeWithinFile(long offset, long length, long totalFileSize)
        {
            return offset >= 0L &&
                   length >= 0L &&
                   totalFileSize >= 0L &&
                   offset <= totalFileSize &&
                   length <= totalFileSize - offset;
        }

        private static bool TryCountIndexedSectorRecords(
            in IndexedSectorGroupBuffer sectorGroups,
            int sectorCount,
            out int totalRecords)
        {
            totalRecords = 0;
            if (!sectorGroups.Groups.IsCreated || sectorCount <= 0)
                return true;

            int safeCount = math.min(sectorCount, sectorGroups.Count);
            long total = 0L;
            for (int i = 0; i < safeCount; i++)
            {
                IndexedSectorGroup group = sectorGroups.Groups[i];
                total += group.Count;
                if (total > int.MaxValue)
                    return false;
            }

            totalRecords = (int)total;
            return true;
        }

        private static void ReportIndexedSectorDirectoryCapacityExceeded(long sectorHash, int attemptedSectorCount)
        {
            CrashTelemetryBuffer.ReportSaveSystemCriticalFault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError(
                $"[SaveBinaryStorage] Indexed sector directory capacity exceeded. " +
                $"capacity={IndexedSectorDirectorySlotCount}, attempted={attemptedSectorCount}, sector=0x{sectorHash:X16}. " +
                "Chunk save dropped to protect the fixed-size v8 directory.");
#endif
        }

        private static bool TryReadIndexedCompressedBlock(
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            long blockOffset,
            int storedBlockLength,
            int expectedDecompressedLength,
            NativeArray<byte> destinationBuffer,
            out int decompressedLength,
            out string error)
        {
            decompressedLength = 0;
            error = string.Empty;

            if (!destinationBuffer.IsCreated || expectedDecompressedLength <= 0 || expectedDecompressedLength > destinationBuffer.Length)
            {
                error = "Indexed block destination buffer is invalid.";
                return false;
            }

            if (storedBlockLength <= IndexedSectorBlockHeaderSize)
            {
                error = "Indexed block is smaller than the block header.";
                return false;
            }

            byte* filePtr = (byte*)mapping.View;
            IndexedSectorBlockHeader blockHeader = UnsafeUtility.ReadArrayElement<IndexedSectorBlockHeader>(filePtr + blockOffset, 0);
            int compressedPayloadLength = storedBlockLength - IndexedSectorBlockHeaderSize;
            byte* compressedPayloadPtr = filePtr + blockOffset + IndexedSectorBlockHeaderSize;
            byte* decompressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destinationBuffer);
            if ((blockHeader.Flags & FlagProtectedLz4Blocks) != 0)
            {
                decompressedLength = Lz4ProtectedBlockDecompress(
                    compressedPayloadPtr,
                    compressedPayloadLength,
                    decompressedPtr,
                    destinationBuffer.Length);
            }
            else
            {
                decompressedLength = Lz4BlockDecompressStandard(
                    compressedPayloadPtr,
                    compressedPayloadLength,
                    decompressedPtr,
                    destinationBuffer.Length,
                    out _);
            }

            if (decompressedLength <= 0)
            {
                error = "Indexed LZ4 block decompression failed.";
                return false;
            }

            if ((blockHeader.Flags & FlagTokenSubstitution) != 0 &&
                !TryExpandTokenizedPayloadInPlace(decompressedPtr, decompressedLength, destinationBuffer.Length, out decompressedLength, out error))
            {
                error = "Indexed token payload expansion failed.";
                return false;
            }

            if (decompressedLength != expectedDecompressedLength)
            {
                error = $"Indexed block length mismatch. Expected {expectedDecompressedLength} bytes, got {decompressedLength}.";
                return false;
            }

            return true;
        }

        private static bool TryReadPersistentWorldSectionFromBuffer(
            byte* sectionPtr,
            int sectionLength,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out string error)
        {
            persistentWorldDeltas = null;
            error = string.Empty;

            if (sectionPtr == null || sectionLength < LegacyPersistentWorldSectionHeaderSize)
            {
                error = "Indexed persistent-world sector section is truncated.";
                return false;
            }

            int sectionHeaderSize = LegacyPersistentWorldSectionHeaderSize;
            int recordCount = 0;
            int chunkCount = 0;
            int itemHashCount = 0;
            int expectedLength = -1;
            bool sectionHeaderMatched = false;

            if (sectionLength >= PersistentWorldSectionHeaderSize)
            {
                sectionHeaderSize = ResolvePersistentWorldSectionHeaderSize(CurrentVersion);
                PersistentWorldSectionHeader sectionHeader = ReadPersistentWorldSectionHeader(sectionPtr, sectionHeaderSize);
                if (TryConvertPersistentWorldSectionCounts(in sectionHeader, out recordCount, out chunkCount, out itemHashCount) &&
                    TryComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount, CurrentVersion, out expectedLength) &&
                    expectedLength == sectionLength)
                {
                    sectionHeaderMatched = true;
                }
            }

            if (!sectionHeaderMatched)
            {
                sectionHeaderSize = LegacyPersistentWorldSectionHeaderSize;
                PersistentWorldSectionHeader sectionHeader = ReadPersistentWorldSectionHeader(sectionPtr, sectionHeaderSize);
                if (!TryConvertPersistentWorldSectionCounts(in sectionHeader, out recordCount, out chunkCount, out itemHashCount) ||
                    !TryComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount, MinimumSupportedVersion, out expectedLength))
                {
                    error = "Indexed persistent-world sector compact header exceeds supported bounds.";
                    return false;
                }
            }

            if (expectedLength != sectionLength)
            {
                error = "Indexed persistent-world sector length does not match its compact header.";
                return false;
            }

            persistentWorldDeltas = recordCount > 0 ? new PersistentWorldDeltaRecord[recordCount] : Array.Empty<PersistentWorldDeltaRecord>();
            if (recordCount <= 0)
                return true;

            int cursor = sectionHeaderSize;
            byte* chunkTablePtr = sectionPtr + cursor;
            cursor += chunkCount * UnsafeUtility.SizeOf<int3>();
            byte* itemHashTablePtr = sectionPtr + cursor;
            cursor += itemHashCount * UnsafeUtility.SizeOf<ulong>();
            byte* saveRecordPtr = sectionPtr + cursor;

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldSaveRecord16 saveRecord = UnsafeUtility.ReadArrayElement<PersistentWorldSaveRecord16>(saveRecordPtr, i);
                bool isDeleted = (((PersistentWorldItemFlags)saveRecord.ItemFlags) & PersistentWorldItemFlags.Deleted) != 0;
                if (saveRecord.ChunkIndex >= chunkCount)
                {
                    error = "Indexed persistent-world record referenced an out-of-range lookup table entry.";
                    return false;
                }

                int3 chunkId = UnsafeUtility.ReadArrayElement<int3>(chunkTablePtr, saveRecord.ChunkIndex);
                ulong itemHash = 0UL;
                if (!isDeleted)
                {
                    if (saveRecord.ItemHashIndex >= itemHashCount)
                    {
                        error = "Indexed persistent-world record referenced an out-of-range item-hash entry.";
                        return false;
                    }

                    itemHash = UnsafeUtility.ReadArrayElement<ulong>(itemHashTablePtr, saveRecord.ItemHashIndex);
                }

                persistentWorldDeltas[i] = new PersistentWorldDeltaRecord
                {
                    ChunkId = chunkId,
                    ItemPersistentIdHash = itemHash,
                    InstanceUid = saveRecord.InstanceUid,
                    PackedLocalPosition = saveRecord.PackedLocalPosition,
                    Quantity = saveRecord.Quantity < 1 ? (ushort)1 : saveRecord.Quantity,
                    ItemFlags = saveRecord.ItemFlags,
                    Reserved = saveRecord.Reserved
                };
            }

            return true;
        }

        private static bool TryReadMetadataIndexedV8(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out SaveMetadata metadata,
            out int detectedVersion,
            out string error)
        {
            metadata = null;
            detectedVersion = 0;
            error = string.Empty;

            if (!TryReadIndexedDirectoryHeaderForMappedScan(
                    in header,
                    ref mapping,
                    out IndexedSectorDirectoryHeader directoryHeader,
                    out int directoryEntryCursor,
                    out long metadataEndOffset,
                    out error))
            {
                return false;
            }

            if (!TryReadIndexedCompressedBlock(ref mapping, header.PlayerOffset, directoryHeader.MetadataCompressedSize, directoryHeader.MetadataDecompressedSize, rawBuffer, out int metadataRawLength, out error))
                return false;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            int sectorEntrySize = ResolveIndexedSectorEntrySize(header.Version);
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);
            int headerSizeBytes = ResolveExpectedHeaderSize(header.Version);
            ulong directoryHash64 = header.PlayerOffset > headerSizeBytes
                ? Hash64((byte*)mapping.View + headerSizeBytes, (int)(header.PlayerOffset - headerSizeBytes))
                : 0UL;
            if ((metadataHash64 ^ directoryHash64) != header.HashPayload64)
            {
                error = "Indexed metadata aggregate checksum mismatch.";
                return false;
            }

            bool hasChecksumChain = header.Version >= HeaderChecksumVersion;
            if (!TryDecodeIndexedSectorCount(in directoryHeader, out int indexedSectorCount, out error))
                return false;
            if (!TryComputeIndexedChecksumRootFromMappedDirectory(
                    (byte*)mapping.View,
                    directoryEntryCursor,
                    sectorEntrySize,
                    metadataEndOffset,
                    mapping.Length,
                    indexedSectorCount,
                    unchecked((uint)metadataHash64),
                    hasChecksumChain,
                    out uint checksumRoot,
                    out error))
            {
                return false;
            }

            if (hasChecksumChain)
            {
                if (!IsIndexedChecksumRootValid(
                        checksumRoot,
                        (byte*)mapping.View,
                        directoryEntryCursor,
                        sectorEntrySize,
                        unchecked((uint)metadataHash64),
                        header.Checksum))
                {
                    error = "Indexed metadata checksum-chain root mismatch.";
                    return false;
                }
            }

            if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, metadataRawLength, header.Version, out PayloadPrefixInfo prefix, out error))
                return false;

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            if (!TryToRuntimePosition(prefix.PlayerPosition, out Vector3 playerPosition))
            {
                error = "Indexed metadata contains an invalid player AUP position.";
                return false;
            }

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = SaveMetadata.NormalizeSceneName(sceneName),
                PlayerPosition = playerPosition,
                Checksum = FormatPayloadChecksum(in header)
            };
            return true;
        }

        private static bool TryLoadSaveDataIndexedV8(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            NativeArray<byte> voxelDeltaSnapshotDestination,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out int voxelDeltaSnapshotBytes,
            out ushort playerDialogueChoiceFlags,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out int detectedVersion,
            out bool indexedBackupRecoveryUsed,
            out string error)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldDeltas = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshotBytes = 0;
            playerDialogueChoiceFlags = DecodePlayerDialogueChoiceFlags(in header);
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            detectedVersion = 0;
            indexedBackupRecoveryUsed = false;
            error = string.Empty;

            if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                return false;

            if (!TryReadIndexedCompressedBlock(ref mapping, header.PlayerOffset, directoryHeader.MetadataCompressedSize, directoryHeader.MetadataDecompressedSize, rawBuffer, out int metadataRawLength, out error))
                return false;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);
            int headerSizeBytes = ResolveExpectedHeaderSize(header.Version);
            ulong directoryHash64 = header.PlayerOffset > headerSizeBytes
                ? Hash64((byte*)mapping.View + headerSizeBytes, (int)(header.PlayerOffset - headerSizeBytes))
                : 0UL;
            payloadHash64 = metadataHash64 ^ directoryHash64;
            if (payloadHash64 != header.HashPayload64)
            {
                error = "Indexed aggregate payload checksum mismatch.";
                return false;
            }

            if (header.Version >= HeaderChecksumVersion)
            {
                if (!IsIndexedChecksumRootValid(unchecked((uint)metadataHash64), sectorEntries, header.Checksum))
                {
                    error = "Indexed checksum-chain root mismatch.";
                    return false;
                }
            }

            if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, metadataRawLength, header.Version, out PayloadPrefixInfo prefix, out error))
                return false;

            detectedVersion = prefix.SaveDataVersion;

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            if (!TryDecodeSaveDataByteLength(in prefix, out int saveDataLength, out error))
                return false;
            if (!IsByteRangeWithin(cursor, saveDataLength, metadataRawLength))
            {
                error = "Indexed metadata save-data range is invalid.";
                return false;
            }

            if (!SaveBinaryPayloadCodec.TryRead(AddByteOffset(rawPtr, cursor), saveDataLength, out data, out int bytesRead, out error))
                return false;
            if (bytesRead != saveDataLength)
            {
                error = "Indexed metadata save-data length mismatch.";
                return false;
            }

            int payloadCursor = cursor + saveDataLength;
            if (!TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error))
                return false;
            if (packedQuestWordCount > 0)
            {
                if (!IsByteRangeWithin(payloadCursor, PackedQuestStateSectionHeaderSize, metadataRawLength))
                {
                    error = "Indexed packed quest section header is truncated.";
                    return false;
                }

                packedQuestHeader = UnsafeUtility.ReadArrayElement<QuestSaveHeader>(AddByteOffset(rawPtr, payloadCursor), 0);
                if (!TryValidatePackedQuestSectionHeader(in packedQuestHeader, packedQuestWordCount, out error))
                    return false;

                packedQuestStateWords = new uint[packedQuestWordCount];
                payloadCursor += PackedQuestStateSectionHeaderSize;
                if (packedQuestHeader.FlagCount > 0)
                {
                    fixed (uint* destinationPtr = packedQuestStateWords)
                    {
                        int packedQuestBytes = packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                        if (!IsByteRangeWithin(payloadCursor, packedQuestBytes, metadataRawLength))
                        {
                            error = "Indexed packed quest section exceeds the metadata payload bounds.";
                            return false;
                        }

                        if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>(), AddByteOffset(rawPtr, payloadCursor), packedQuestBytes))
                        {
                            error = "Indexed packed quest-state copy exceeded destination bounds.";
                            return false;
                        }
                    }

                    if (ComputePackedQuestStateChecksum(packedQuestStateWords) != packedQuestHeader.Checksum)
                    {
                        error = "Indexed packed quest section checksum mismatch.";
                        return false;
                    }
                }

                payloadCursor += packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
            }
            else
            {
                packedQuestHeader = default;
                packedQuestStateWords = Array.Empty<uint>();
            }
            playerDialogueChoiceFlags = ResolveLoadedPlayerDialogueChoiceFlags(
                header.Version,
                playerDialogueChoiceFlags,
                packedQuestStateWords);

            int ecosystemHeaderSize = ResolveEcosystemSectionHeaderSize(header.Version);
            if (!IsByteRangeWithin(payloadCursor, ecosystemHeaderSize, metadataRawLength))
            {
                error = "Indexed ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader ecosystemHeader = ReadEcosystemSectionHeader(AddByteOffset(rawPtr, payloadCursor), ecosystemHeaderSize);
            if (!TryConvertSectionCount(ecosystemHeader.RecordCount, out int ecosystemRecordCount) ||
                !TryComputeEcosystemSectionLength(ecosystemRecordCount, header.Version, out int ecosystemSectionLength))
            {
                error = "Indexed ecosystem section header exceeds supported bounds.";
                return false;
            }

            if (!IsByteRangeWithin(payloadCursor, ecosystemSectionLength, metadataRawLength))
            {
                error = "Indexed ecosystem section exceeds the metadata payload bounds.";
                return false;
            }

            ecosystemSectorStates = ecosystemRecordCount > 0 ? new EcosystemSectorSaveRecord[ecosystemRecordCount] : Array.Empty<EcosystemSectorSaveRecord>();
            if (ecosystemRecordCount > 0)
            {
                fixed (EcosystemSectorSaveRecord* destinationPtr = ecosystemSectorStates)
                {
                    int ecosystemBytes = ecosystemRecordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
                    if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, ecosystemSectorStates.Length * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>(), AddByteOffset(rawPtr, payloadCursor + ecosystemHeaderSize), ecosystemBytes))
                    {
                        error = "Indexed ecosystem section copy exceeded destination bounds.";
                        return false;
                    }
                }
            }

            payloadCursor += ecosystemSectionLength;
            int voxelByteLength = math.max(0, metadataRawLength - payloadCursor);
            if (voxelByteLength > 0)
            {
                if (!voxelDeltaSnapshotDestination.IsCreated || voxelDeltaSnapshotDestination.Length < voxelByteLength)
                {
                    error = "Indexed voxel delta snapshot destination is too small.";
                    return false;
                }

                void* voxelDestinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshotDestination);
                if (!UnsafeMemoryCopyGuard.SafeCopy(voxelDestinationPtr, voxelDeltaSnapshotDestination.Length, AddByteOffset(rawPtr, payloadCursor), voxelByteLength))
                {
                    error = "Indexed voxel delta snapshot copy exceeded destination bounds.";
                    return false;
                }

                voxelDeltaSnapshotBytes = voxelByteLength;
            }

            NativeList<PersistentWorldDeltaRecord> aggregatedWorldDeltas = CreateRegisteredTransientNativeList<PersistentWorldDeltaRecord>(
                256,
                IndexedSectorAggregatedWorldDeltasScratchLabel,
                out int aggregatedWorldDeltasSentinelId);
            try
            {
                if (sectorEntries != null)
                {
                    for (int i = 0; i < sectorEntries.Length; i++)
                    {
                        SectorEntry entry = sectorEntries[i];
                        if (entry.CompressedSize <= 0 || entry.DecompressedSize <= 0)
                            continue;

                        if (!IsPersistentWorldPagedSectorWithinLoadWindow(entry.SectorHash, in prefix.PlayerPosition))
                            continue;

                        using RegisteredTransientNativeArray<byte> sectorRawOwner = CreateRegisteredTransientNativeArray<byte>(
                            entry.DecompressedSize,
                            NativeArrayOptions.UninitializedMemory,
                            IndexedSectorReadRawScratchLabel);
                        NativeArray<byte> sectorRaw = sectorRawOwner.Array;
                        if (!TryReadIndexedCompressedBlock(ref mapping, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, sectorRaw, out _, out error))
                        {
                            string sectorError = error;
                            if (TryAppendIndexedPersistentWorldSectorFromBackup(absolutePath, entry.SectorHash, aggregatedWorldDeltas, out string backupError))
                            {
                                indexedBackupRecoveryUsed = true;
                                error = string.Empty;
                                continue;
                            }

                            ReportIndexedSectorQuarantine(entry.SectorHash, sectorError, backupError);
                            error = string.Empty;
                            continue;
                        }

                        byte* sectorRawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sectorRaw);
                        if (!TryVerifyIndexedSectorPayloadChecksum(in entry, sectorRawPtr, entry.DecompressedSize, out error))
                        {
                            string sectorError = error;
                            if (TryAppendIndexedPersistentWorldSectorFromBackup(absolutePath, entry.SectorHash, aggregatedWorldDeltas, out string backupError))
                            {
                                indexedBackupRecoveryUsed = true;
                                error = string.Empty;
                                continue;
                            }

                            ReportIndexedSectorQuarantine(entry.SectorHash, sectorError, backupError);
                            error = string.Empty;
                            continue;
                        }

                        if (!TryReadPersistentWorldSectionFromBuffer(sectorRawPtr, entry.DecompressedSize, out PersistentWorldDeltaRecord[] sectorRecords, out error))
                        {
                            string sectorError = error;
                            if (TryAppendIndexedPersistentWorldSectorFromBackup(absolutePath, entry.SectorHash, aggregatedWorldDeltas, out string backupError))
                            {
                                indexedBackupRecoveryUsed = true;
                                error = string.Empty;
                                continue;
                            }

                            ReportIndexedSectorQuarantine(entry.SectorHash, sectorError, backupError);
                            error = string.Empty;
                            continue;
                        }

                        if (sectorRecords == null || sectorRecords.Length <= 0)
                            continue;

                        for (int recordIndex = 0; recordIndex < sectorRecords.Length; recordIndex++)
                            aggregatedWorldDeltas.Add(sectorRecords[recordIndex]);
                    }
                }

                if (aggregatedWorldDeltas.Length > 0)
                {
                    persistentWorldDeltas = new PersistentWorldDeltaRecord[aggregatedWorldDeltas.Length];
                    for (int i = 0; i < persistentWorldDeltas.Length; i++)
                        persistentWorldDeltas[i] = aggregatedWorldDeltas[i];
                }
                else
                {
                    persistentWorldDeltas = Array.Empty<PersistentWorldDeltaRecord>();
                }
            }
            finally
            {
                DisposeRegisteredTransientNativeList(
                    ref aggregatedWorldDeltas,
                    ref aggregatedWorldDeltasSentinelId,
                    IndexedSectorAggregatedWorldDeltasScratchLabel);
            }

            rawPayloadLength = metadataRawLength;
            if (!TryToRuntimePosition(prefix.PlayerPosition, out Vector3 playerPosition))
            {
                error = "Indexed save payload contains an invalid player AUP position.";
                return false;
            }

            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = SaveMetadata.NormalizeSceneName(sceneName),
                PlayerPosition = playerPosition,
                Checksum = FormatPayloadChecksum(in header)
            };
            return true;
        }

        internal static bool TryReadIndexedPersistentWorldDirectory(
            string absolutePath,
            List<IndexedSectorEntryInfo> results,
            out int chunkSizeMeters,
            out string error)
        {
            chunkSizeMeters = DefaultIndexedPersistentWorldChunkSizeMeters;
            error = string.Empty;
            if (results == null)
            {
                error = "Indexed sector result list is null.";
                return false;
            }

            results.Clear();
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if ((header.Flags & FlagIndexedSectorBlocks) == 0 || header.Version < IndexedBlockStorageVersion)
                {
                    error = "Save file is not an indexed sector container.";
                    return false;
                }

                if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                    return false;

                chunkSizeMeters = directoryHeader.ChunkSizeMeters;
                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    results.Add(new IndexedSectorEntryInfo(entry.SectorHash, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, entry.Checksum));
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadIndexedPersistentWorldDirectory(
            string absolutePath,
            IndexedSectorEntryInfo[] results,
            out int resultCount,
            out int chunkSizeMeters,
            out string error)
        {
            resultCount = 0;
            chunkSizeMeters = DefaultIndexedPersistentWorldChunkSizeMeters;
            error = string.Empty;
            if (results == null || results.Length <= 0)
            {
                error = "Indexed sector result array is empty.";
                return false;
            }

            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if ((header.Flags & FlagIndexedSectorBlocks) == 0 || header.Version < IndexedBlockStorageVersion)
                {
                    error = "Save file is not an indexed sector container.";
                    return false;
                }

                if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                    return false;

                chunkSizeMeters = directoryHeader.ChunkSizeMeters;
                for (int i = 0; i < sectorEntries.Length && resultCount < results.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    results[resultCount++] = new IndexedSectorEntryInfo(entry.SectorHash, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, entry.Checksum);
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static bool TryCorruptFirstIndexedSectorBlockForSmoke(
            string absolutePath,
            out long sectorHash,
            out string error)
        {
            sectorHash = 0L;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Smoke corruption path is missing.";
                return false;
            }

            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping readMapping, out SaveFileHeader header, out _, out error))
                return false;

            SectorEntry selectedEntry = default;
            bool foundSector = false;
            try
            {
                if (!TryReadIndexedDirectory(in header, ref readMapping, out _, out SectorEntry[] sectorEntries, out error))
                    return false;

                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry) ||
                        entry.CompressedSize <= IndexedSectorBlockHeaderSize + StandardCompressedBlockHeaderBytes ||
                        !IsIndexedSectorEntryWithinFileBounds(in entry, header.PlayerOffset, readMapping.Length))
                    {
                        continue;
                    }

                    selectedEntry = entry;
                    foundSector = true;
                    break;
                }
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref readMapping);
            }

            if (!foundSector)
            {
                error = "Smoke corruption could not find an indexed sector block.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out AsyncWriteManager.ReadOnlyMapping corruptionMapping, out error))
                return false;

            try
            {
                long fileLength = corruptionMapping.Length;
                if (fileLength > int.MaxValue)
                {
                    error = "Smoke corruption file exceeds the native overwrite range.";
                    return false;
                }

                if (selectedEntry.ByteOffset < 0L || selectedEntry.ByteOffset > fileLength - IndexedSectorBlockHeaderSize)
                {
                    error = "Smoke corruption block header offset exceeded the file length.";
                    return false;
                }

                byte* filePtr = (byte*)corruptionMapping.View;
                byte* blockHeaderBytes = filePtr + (int)selectedEntry.ByteOffset;

                uint blockFlags =
                    (uint)blockHeaderBytes[0] |
                    ((uint)blockHeaderBytes[1] << 8) |
                    ((uint)blockHeaderBytes[2] << 16) |
                    ((uint)blockHeaderBytes[3] << 24);
                int subBlockHeaderBytes = (blockFlags & FlagProtectedLz4Blocks) != 0
                    ? ProtectedCompressedBlockHeaderBytes
                    : StandardCompressedBlockHeaderBytes;
                long payloadOffset = selectedEntry.ByteOffset + IndexedSectorBlockHeaderSize + subBlockHeaderBytes;
                if (payloadOffset >= fileLength)
                {
                    error = "Smoke corruption payload offset exceeded the file length.";
                    return false;
                }

                int payloadIndex = (int)payloadOffset;
                filePtr[payloadIndex] = (byte)(filePtr[payloadIndex] ^ 0x5Au);
                if (!AsyncWriteManager.OverwriteAllCritical(absolutePath, filePtr, (int)fileLength, out error))
                    return false;

                sectorHash = selectedEntry.SectorHash;
                return true;
            }
            catch (Exception)
            {
                error = "Smoke corruption failed.";
                return false;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref corruptionMapping);
            }
        }
#endif

        internal static bool TryLoadIndexedPersistentWorldSectors(
            string absolutePath,
            NativeArray<long> desiredSectorHashes,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;
            if (!desiredSectorHashes.IsCreated || !destination.IsCreated)
            {
                error = "Indexed sector paging inputs are invalid.";
                return false;
            }

            destination.Clear();
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                    return false;

                for (int requestedIndex = 0; requestedIndex < desiredSectorHashes.Length; requestedIndex++)
                {
                    long desiredSectorHash = desiredSectorHashes[requestedIndex];
                    if (desiredSectorHash == long.MinValue)
                        continue;

                    if (!TryFindIndexedSectorEntryIndex(sectorEntries, desiredSectorHash, out int sectorEntryIndex))
                        continue;

                    SectorEntry entry = sectorEntries[sectorEntryIndex];
                    if (!TryLoadIndexedPersistentWorldSectorRecordsCore(
                            ref mapping,
                            in entry,
                            directoryHeader.ChunkSizeMeters,
                            destination,
                            out string sectorError))
                    {
                        string backupPath = ResolveIndexedSaveBackupPath(absolutePath);
                        string backupError = "backup not attempted";
                        bool backupRecovered = false;
                        if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                            backupRecovered = TryLoadIndexedPersistentWorldSectorFromBackup(backupPath, desiredSectorHash, destination, out backupError);

                        if (!backupRecovered)
                        {
                            ReportIndexedSectorQuarantine(desiredSectorHash, sectorError, backupError);
                            error = $"QUARANTINED indexed sector 0x{desiredSectorHash:X16}; pristine reset sector will be generated by runtime streaming. Primary: {sectorError} Backup: {backupError}";
                            continue;
                        }

                        ReportIndexedSectorBackupRecovery(desiredSectorHash, sectorError);
                    }
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        private static bool TryLoadIndexedPersistentWorldSectorRecordsCore(
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            in SectorEntry entry,
            int chunkSizeMeters,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;

            using RegisteredTransientNativeArray<byte> sectorRawOwner = CreateRegisteredTransientNativeArray<byte>(
                entry.DecompressedSize,
                NativeArrayOptions.UninitializedMemory,
                IndexedSectorReadRawScratchLabel);
            NativeArray<byte> sectorRaw = sectorRawOwner.Array;
            if (!TryReadIndexedCompressedBlock(ref mapping, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, sectorRaw, out int decompressedLength, out error))
                return false;

            if (decompressedLength != entry.DecompressedSize)
            {
                error = $"Indexed sector length mismatch for sector 0x{entry.SectorHash:X16}.";
                return false;
            }

            byte* sectorRawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sectorRaw);
            if (!TryVerifyIndexedSectorPayloadChecksum(in entry, sectorRawPtr, entry.DecompressedSize, out error))
                return false;

            if (!TryReadPersistentWorldSectionFromBuffer(sectorRawPtr, entry.DecompressedSize, out PersistentWorldDeltaRecord[] sectorRecords, out error))
                return false;

            if (!TryValidatePersistentWorldSectorRecords(
                    sectorRecords,
                    entry.SectorHash,
                    chunkSizeMeters,
                    out error))
            {
                return false;
            }

            for (int recordIndex = 0; recordIndex < sectorRecords.Length; recordIndex++)
                destination.Add(sectorRecords[recordIndex]);

            return true;
        }

        private static bool TryLoadIndexedPersistentWorldSectorFromBackup(
            string backupPath,
            long desiredSectorHash,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;
            if (!TryReadValidatedHeader(backupPath, out AsyncWriteManager.ReadOnlyMapping backupMapping, out SaveFileHeader backupHeader, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectory(in backupHeader, ref backupMapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] backupEntries, out error))
                    return false;

                if (!TryFindIndexedSectorEntryIndex(backupEntries, desiredSectorHash, out int backupSectorEntryIndex))
                {
                    error = $"Indexed sector 0x{desiredSectorHash:X16} is missing from backup save.";
                    return false;
                }

                SectorEntry backupEntry = backupEntries[backupSectorEntryIndex];
                return TryLoadIndexedPersistentWorldSectorRecordsCore(
                    ref backupMapping,
                    in backupEntry,
                    directoryHeader.ChunkSizeMeters,
                    destination,
                    out error);
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref backupMapping);
            }
        }

        private static bool TryAppendIndexedPersistentWorldSectorFromBackup(
            string primaryPath,
            long desiredSectorHash,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;
            if (!destination.IsCreated)
            {
                error = "Indexed sector backup append destination is not initialized.";
                return false;
            }

            string backupPath = ResolveIndexedSaveBackupPath(primaryPath);
            if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
            {
                error = $"Indexed sector 0x{desiredSectorHash:X16} backup is missing.";
                return false;
            }

            NativeList<PersistentWorldDeltaRecord> backupRecords = CreateRegisteredTransientNativeList<PersistentWorldDeltaRecord>(
                16,
                IndexedSectorBackupAppendRecordsScratchLabel,
                out int backupRecordsSentinelId);
            try
            {
                if (!TryLoadIndexedPersistentWorldSectorFromBackup(backupPath, desiredSectorHash, backupRecords, out error))
                    return false;

                for (int i = 0; i < backupRecords.Length; i++)
                    destination.Add(backupRecords[i]);

                return true;
            }
            finally
            {
                DisposeRegisteredTransientNativeList(
                    ref backupRecords,
                    ref backupRecordsSentinelId,
                    IndexedSectorBackupAppendRecordsScratchLabel);
            }
        }

        internal static bool TryResetIndexedPersistentWorldSectorToPristine(
            string absoluteSavePath,
            long sectorHash,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || !File.Exists(absoluteSavePath))
            {
                error = "Pristine sector reset save path is invalid.";
                return false;
            }

            string directory = Path.GetDirectoryName(absoluteSavePath);
            if (string.IsNullOrEmpty(directory))
            {
                error = "Pristine sector reset directory is invalid.";
                return false;
            }

            string tempOverridePath = Path.Combine(directory, sectorHash.ToString("X16") + ".reset.sectmp");
            using RegisteredTransientNativeArray<PersistentWorldDeltaRecord> emptySectorRecordsOwner =
                CreateRegisteredTransientNativeArray<PersistentWorldDeltaRecord>(
                    0,
                    NativeArrayOptions.UninitializedMemory,
                    PristineResetEmptySectorRecordsLabel);
            NativeArray<PersistentWorldDeltaRecord> emptySectorRecords = emptySectorRecordsOwner.Array;
            if (!TryWriteIndexedPersistentWorldSectorOverride(tempOverridePath, sectorHash, emptySectorRecords, chunkSizeMeters, out error))
                return false;

            if (TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error))
                return true;

            _ = TryDeleteFileIfExists(tempOverridePath, out _);

            return false;
        }

        internal static bool TryRestoreIndexedPersistentWorldSectorFromBackup(
            string absoluteSavePath,
            long sectorHash,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || !File.Exists(absoluteSavePath))
            {
                error = "Backup sector restore save path is invalid.";
                return false;
            }

            string backupPath = ResolveIndexedSaveBackupPath(absoluteSavePath);
            if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
            {
                error = $"Indexed sector 0x{sectorHash:X16} backup is missing.";
                return false;
            }

            string directory = Path.GetDirectoryName(absoluteSavePath);
            if (string.IsNullOrEmpty(directory))
            {
                error = "Backup sector restore directory is invalid.";
                return false;
            }

            string tempOverridePath = Path.Combine(directory, sectorHash.ToString("X16") + ".backup.sectmp");
            NativeList<PersistentWorldDeltaRecord> backupRecords = CreateRegisteredTransientNativeList<PersistentWorldDeltaRecord>(
                16,
                BackupRecoverySourceRecordsLabel,
                out int backupRecordsSentinelId);
            NativeArray<PersistentWorldDeltaRecord> sectorRecords = default;
            try
            {
                if (!TryLoadIndexedPersistentWorldSectorFromBackup(backupPath, sectorHash, backupRecords, out error))
                    return false;

                using RegisteredTransientNativeArray<PersistentWorldDeltaRecord> sectorRecordsOwner =
                    CreateRegisteredTransientNativeArray<PersistentWorldDeltaRecord>(
                        backupRecords.Length,
                        NativeArrayOptions.UninitializedMemory,
                        BackupRecoverySectorRecordsLabel);
                sectorRecords = sectorRecordsOwner.Array;

                for (int i = 0; i < backupRecords.Length; i++)
                    sectorRecords[i] = backupRecords[i];

                if (!TryWriteIndexedPersistentWorldSectorOverride(tempOverridePath, sectorHash, sectorRecords, chunkSizeMeters, out error))
                    return false;

                if (TryCommitIndexedPersistentWorldSectorOverride(
                        absoluteSavePath,
                        tempOverridePath,
                        out error,
                        refreshBackupBeforeCommit: false))
                {
                    return true;
                }
            }
            finally
            {
                DisposeRegisteredTransientNativeList(
                    ref backupRecords,
                    ref backupRecordsSentinelId,
                    BackupRecoverySourceRecordsLabel);
            }

            _ = TryDeleteFileIfExists(tempOverridePath, out _);
            return false;
        }

        internal static bool TryWriteIndexedPersistentWorldSectorOverride(
            string absolutePath,
            long sectorHash,
            NativeArray<PersistentWorldDeltaRecord> sectorRecords,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Sector override path is empty.";
                return false;
            }

            if (!sectorRecords.IsCreated)
            {
                error = "Sector override record array is not initialized.";
                return false;
            }

            NativeArray<PersistentWorldDeltaRecord>.ReadOnly sectorRecordsReadOnly = sectorRecords.AsReadOnly();
            if (!TryValidatePersistentWorldSectorRecords(
                    sectorRecordsReadOnly,
                    sectorHash,
                    chunkSizeMeters,
                    out error))
            {
                return false;
            }

            if (!TryBuildPersistentWorldSectionTables(
                    sectorRecordsReadOnly,
                    out NativeParallelHashMap<int3, ushort> chunkLookup,
                    out NativeList<int3> chunkTable,
                    out NativeParallelHashMap<ulong, ushort> itemHashLookup,
                    out NativeList<ulong> itemHashTable,
                    out PersistentWorldSectionTableSentinelIds tableSentinelIds,
                    out error))
            {
                return false;
            }

            try
            {
                if (!TryComputePersistentWorldSectionLength(
                        sectorRecords.Length,
                        chunkTable.Length,
                        itemHashTable.Length,
                        CurrentVersion,
                        out int rawSectionLength))
                {
                    error = "Sector override persistent-world section exceeds supported bounds.";
                    return false;
                }

                int blockCount = ResolveProtectedLz4BlockCount(rawSectionLength);
                long compressedCapacityLong =
                    (long)rawSectionLength +
                    (rawSectionLength / 255) +
                    16L +
                    ((long)blockCount * ProtectedCompressedBlockHeaderBytes) +
                    IndexedSectorBlockHeaderSize +
                    32L;
                long fileCapacityLong = UnsafeUtility.SizeOf<SectorOverrideFileHeader>() + compressedCapacityLong;
                if (compressedCapacityLong <= 0L || fileCapacityLong > int.MaxValue)
                {
                    error = "Sector override compressed buffer exceeds supported bounds.";
                    return false;
                }

                int fileCapacity = (int)fileCapacityLong;

                using RegisteredTransientNativeArray<byte> rawSectionBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    rawSectionLength,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorOverrideRawSectionScratchLabel);
                using RegisteredTransientNativeArray<byte> fileBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    fileCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorOverrideFileScratchLabel);
                NativeArray<byte> rawSectionBytes = rawSectionBytesOwner.Array;
                NativeArray<byte> fileBytes = fileBytesOwner.Array;
                byte* rawSectionPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawSectionBytes);
                byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                if (!WritePersistentWorldSection(
                        rawSectionPtr,
                        sectorRecordsReadOnly,
                        chunkLookup,
                        chunkTable,
                        itemHashLookup,
                        itemHashTable,
                        out error))
                {
                    return false;
                }
                uint rawSectionChecksum = Hash32(rawSectionPtr, rawSectionLength);

                int fileCursor = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
                if (!TryWriteIndexedCompressedBlock(
                        rawSectionPtr,
                        rawSectionLength,
                        filePtr,
                        fileBytes.Length,
                        ref fileCursor,
                        out int storedBlockLength,
                        out uint blockFlags,
                        out error))
                {
                    return false;
                }

                SectorOverrideFileHeader overrideHeader = new SectorOverrideFileHeader
                {
                    SectorHash = sectorHash,
                    CompressedSize = storedBlockLength,
                    DecompressedSize = rawSectionLength,
                    Checksum = rawSectionChecksum,
                    Flags = blockFlags
                };

                UnsafeUtility.CopyStructureToPtr(ref overrideHeader, filePtr);
                if (!AsyncWriteManager.WriteAll(absolutePath, filePtr, fileCursor, out error))
                    return false;

                return AsyncWriteManager.FlushCriticalSavePath(absolutePath, fileCursor, out error);
            }
            finally
            {
                DisposePersistentWorldSectionTables(
                    ref chunkLookup,
                    ref chunkTable,
                    ref itemHashLookup,
                    ref itemHashTable,
                    ref tableSentinelIds);
            }
        }

        internal static bool TryReadIndexedPersistentWorldSectorOverride(
            string absolutePath,
            out long sectorHash,
            out PersistentWorldDeltaRecord[] sectorRecords,
            out string error)
        {
            sectorHash = 0L;
            sectorRecords = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Sector override file is missing.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out error))
                return false;

            try
            {
                int overrideHeaderSize = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
                if (mapping.Length < overrideHeaderSize + IndexedSectorBlockHeaderSize)
                {
                    error = "Sector override file is truncated.";
                    return false;
                }

                SectorOverrideFileHeader overrideHeader = UnsafeUtility.ReadArrayElement<SectorOverrideFileHeader>((byte*)mapping.View, 0);
                sectorHash = overrideHeader.SectorHash;
                if (overrideHeader.CompressedSize <= IndexedSectorBlockHeaderSize ||
                    overrideHeader.DecompressedSize <= 0 ||
                    mapping.Length < overrideHeaderSize ||
                    overrideHeader.CompressedSize > mapping.Length - overrideHeaderSize)
                {
                    error = "Sector override header is invalid.";
                    return false;
                }

                using RegisteredTransientNativeArray<byte> rawBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    overrideHeader.DecompressedSize,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorReadRawScratchLabel);
                NativeArray<byte> rawBytes = rawBytesOwner.Array;
                if (!TryReadIndexedCompressedBlock(
                        ref mapping,
                        overrideHeaderSize,
                        overrideHeader.CompressedSize,
                        overrideHeader.DecompressedSize,
                        rawBytes,
                        out int decompressedLength,
                        out error))
                {
                    return false;
                }

                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBytes);
                if (decompressedLength != overrideHeader.DecompressedSize)
                {
                    error = "Sector override decompressed length mismatch.";
                    return false;
                }

                if (overrideHeader.Checksum != 0u && Hash32(rawPtr, decompressedLength) != overrideHeader.Checksum)
                {
                    error = "Sector override checksum mismatch.";
                    return false;
                }

                return TryReadPersistentWorldSectionFromBuffer(rawPtr, decompressedLength, out sectorRecords, out error);
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryWriteIndexedSectorEntityStateOverride(
            string absolutePath,
            long sectorHash,
            NativeArray<EntityDataRecord> entityStates,
            int chunkSizeMeters,
            out string error)
        {
            if (!TryScheduleIndexedSectorEntityStateOverrideWrite(
                    absolutePath,
                    sectorHash,
                    entityStates,
                    chunkSizeMeters,
                    out IndexedSectorEntityStateWriteHandle writeHandle,
                    out error))
            {
                return false;
            }

            return TryCompleteIndexedSectorEntityStateOverrideWrite(ref writeHandle, out error);
        }

        internal static bool TryScheduleIndexedSectorEntityStateOverrideWrite(
            string absolutePath,
            long sectorHash,
            NativeArray<EntityDataRecord> entityStates,
            int chunkSizeMeters,
            out IndexedSectorEntityStateWriteHandle writeHandle,
            out string error)
        {
            writeHandle = default;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Sector entity-state override path is empty.";
                return false;
            }

            if (!entityStates.IsCreated)
            {
                error = "Sector entity-state override source array is not initialized.";
                return false;
            }

            if (chunkSizeMeters <= 0)
            {
                error = "Sector entity-state chunk size is invalid.";
                return false;
            }

            int recordCount = entityStates.Length;
            if (recordCount <= 0)
            {
                error = "Sector entity-state override contains no records.";
                return false;
            }

            if (sectorHash == long.MinValue)
            {
                error = "Sector entity-state override has an invalid sector hash.";
                return false;
            }

            for (int i = 0; i < recordCount; i++)
            {
                EntityDataRecord state = entityStates[i];
                AbsoluteUniversePosition statePosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
                long stateSectorHash = ComputePersistentWorldPagedSectorHash(in statePosition);
                if (stateSectorHash == long.MinValue || stateSectorHash != sectorHash)
                {
                    error = "Sector entity-state override contains an invalid or cross-sector AUP position.";
                    return false;
                }
            }

            long rawByteLengthLong = (long)recordCount * UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>();
            if (rawByteLengthLong <= 0L || rawByteLengthLong > int.MaxValue)
            {
                error = "Sector entity-state raw buffer exceeds supported bounds.";
                return false;
            }

            int rawByteLength = (int)rawByteLengthLong;
            int blockCount = math.max(1, (rawByteLength + BlockSizeBytes - 1) / BlockSizeBytes);
            long compressedCapacityLong =
                (long)rawByteLength +
                (rawByteLength / 255) +
                16L +
                ((long)blockCount * StandardCompressedBlockHeaderBytes);
            long fileCapacityLong = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>() + compressedCapacityLong;
            if (compressedCapacityLong <= 0L || fileCapacityLong > int.MaxValue)
            {
                error = "Sector entity-state compressed buffer exceeds supported bounds.";
                return false;
            }

            int fileCapacity = (int)fileCapacityLong;

            try
            {
                writeHandle.IsCreated = true;
                writeHandle.AbsolutePath = absolutePath;
                writeHandle.SectorHash = sectorHash;
                writeHandle.SourceStates = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<EntityDataRecord>(
                    recordCount,
                    EntityStateWriteSourceStatesLabel,
                    NativeArrayOptions.UninitializedMemory);
                writeHandle.SortEntries = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<SectorEntityStateSortEntry>(
                    recordCount,
                    EntityStateWriteSortEntriesLabel,
                    NativeArrayOptions.UninitializedMemory);
                writeHandle.RadixScratch = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<SectorEntityStateSortEntry>(
                    recordCount,
                    EntityStateWriteRadixScratchLabel,
                    NativeArrayOptions.UninitializedMemory);
                writeHandle.SortedEntityStates = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<EntityDataRecord>(
                    recordCount,
                    EntityStateWriteSortedStatesLabel,
                    NativeArrayOptions.UninitializedMemory);
                writeHandle.CompactStates = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<SectorCompactEntityStateRecord16>(
                    recordCount,
                    EntityStateWriteCompactStatesLabel,
                    NativeArrayOptions.UninitializedMemory);
                writeHandle.FileBytes = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<byte>(
                    fileCapacity,
                    EntityStateWriteFileBytesLabel,
                    NativeArrayOptions.UninitializedMemory);
                writeHandle.ResultLength = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<int>(
                    1,
                    EntityStateWriteResultLengthLabel,
                    NativeArrayOptions.ClearMemory);
                writeHandle.RadixCounts = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<int>(
                    1 << 16,
                    EntityStateWriteRadixCountsLabel,
                    NativeArrayOptions.ClearMemory);
                writeHandle.RadixOffsets = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<int>(
                    1 << 16,
                    EntityStateWriteRadixOffsetsLabel,
                    NativeArrayOptions.ClearMemory);
            }
            catch (Exception ex)
            {
                writeHandle.Dispose();
                error = $"Sector entity-state write buffers could not be allocated: {ex.Message}";
                return false;
            }

            for (int i = 0; i < recordCount; i++)
                writeHandle.SourceStates[i] = entityStates[i];

            BuildSectorEntityStateSortEntriesJob buildJob = new BuildSectorEntityStateSortEntriesJob
            {
                SourceStates = writeHandle.SourceStates,
                Entries = writeHandle.SortEntries,
                ChunkSizeMeters = chunkSizeMeters
            };
            RadixSortSectorEntityStateEntriesJob sortJob = new RadixSortSectorEntityStateEntriesJob
            {
                Entries = writeHandle.SortEntries,
                Scratch = writeHandle.RadixScratch,
                Counts = writeHandle.RadixCounts,
                Offsets = writeHandle.RadixOffsets
            };
            ExtractSortedSectorEntityStatesJob extractJob = new ExtractSortedSectorEntityStatesJob
            {
                Entries = writeHandle.SortEntries,
                SortedStates = writeHandle.SortedEntityStates
            };
            BuildCompactSectorEntityStatesJob compactJob = new BuildCompactSectorEntityStatesJob
            {
                SortedStates = writeHandle.SortedEntityStates,
                CompactStates = writeHandle.CompactStates,
                SectorHash = sectorHash
            };
            CompressSectorEntityStateJob compressJob = new CompressSectorEntityStateJob
            {
                CompactStates = writeHandle.CompactStates,
                FileBytes = writeHandle.FileBytes,
                ResultLength = writeHandle.ResultLength,
                SectorHash = sectorHash
            };

            JobHandle scheduledHandle = default;
            try
            {
                JobHandle buildHandle = buildJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)));
                scheduledHandle = buildHandle;
                JobHandle sortHandle = sortJob.Schedule(buildHandle);
                scheduledHandle = sortHandle;
                JobHandle extractHandle = extractJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)), sortHandle);
                scheduledHandle = extractHandle;
                JobHandle compactHandle = compactJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)), extractHandle);
                scheduledHandle = compactHandle;
                JobHandle compressHandle = compressJob.Schedule(compactHandle);
                scheduledHandle = compressHandle;
                writeHandle.Handle = compressHandle;
            }
            catch (Exception ex)
            {
                writeHandle.Handle = scheduledHandle;
                SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);
                JobHandle.ScheduleBatchedJobs();
                error = $"Sector entity-state write job scheduling failed: {ex.Message}";
                return false;
            }

            return true;
        }

        internal static bool TryCompleteIndexedSectorEntityStateOverrideWrite(
            ref IndexedSectorEntityStateWriteHandle writeHandle,
            out string error)
        {
            error = string.Empty;
            if (!writeHandle.IsCreated)
            {
                error = "Sector entity-state write handle is not initialized.";
                return false;
            }

            DispatcherJobSwap.TryComplete(ref writeHandle.Handle, forceComplete: true);

            int fileLength = writeHandle.ResultLength[0];
            if (fileLength <= UnsafeUtility.SizeOf<SectorEntityStateFileHeader>())
                fileLength = TryBuildSectorEntityStateManagedFallback(ref writeHandle);

            if (fileLength <= UnsafeUtility.SizeOf<SectorEntityStateFileHeader>())
            {
                error = "Sector entity-state LZ4 compression failed.";
                writeHandle.Dispose();
                return false;
            }

            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writeHandle.FileBytes);
            bool written = AsyncWriteManager.WriteAll(writeHandle.AbsolutePath, filePtr, fileLength, out error);
            if (written)
                written = AsyncWriteManager.FlushCriticalSavePath(writeHandle.AbsolutePath, fileLength, out error);
            writeHandle.Dispose();
            return written;
        }

        private static unsafe int TryBuildSectorEntityStateManagedFallback(ref IndexedSectorEntityStateWriteHandle writeHandle)
        {
            int recordCount = writeHandle.CompactStates.IsCreated ? writeHandle.CompactStates.Length : 0;
            if (recordCount <= 0 || !writeHandle.FileBytes.IsCreated)
                return 0;

            int headerSize = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>();
            if (writeHandle.FileBytes.Length <= headerSize)
                return 0;

            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writeHandle.FileBytes);
            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writeHandle.CompactStates);
            int rawByteLength = recordCount * UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>();
            int compressedLength = Lz4BlockCompressStandardManagedFallback(
                rawPtr,
                rawByteLength,
                filePtr + headerSize,
                writeHandle.FileBytes.Length - headerSize);
            if (compressedLength <= 0)
                return 0;

            SectorEntityStateFileHeader header = new SectorEntityStateFileHeader
            {
                SectorHash = writeHandle.SectorHash,
                CompressedSize = compressedLength,
                DecompressedSize = rawByteLength,
                RecordCount = (uint)recordCount,
                Checksum = SectorEntityStateCompact16Magic
            };
            UnsafeUtility.CopyStructureToPtr(ref header, filePtr);
            return headerSize + compressedLength;
        }

        internal static void DisposeIndexedSectorEntityStateOverrideWrite(ref IndexedSectorEntityStateWriteHandle writeHandle)
        {
            if (!writeHandle.IsCreated)
                return;

            SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);
            JobHandle.ScheduleBatchedJobs();
        }

        internal static JobHandle DisposeIndexedSectorEntityStateOverrideWriteDeferred(
            ref IndexedSectorEntityStateWriteHandle writeHandle,
            JobHandle dependency)
        {
            if (!writeHandle.IsCreated)
                return dependency;

            return writeHandle.DisposeDeferred(dependency);
        }

        internal static bool TryReadIndexedSectorEntityStateOverride(
            string absolutePath,
            out long sectorHash,
            out EntityDataRecord[] entityStates,
            out string error)
        {
            sectorHash = 0L;
            entityStates = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Sector entity-state override file is missing.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out error))
                return false;

            try
            {
                int headerSize = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>();
                if (mapping.Length < headerSize + 8)
                {
                    error = "Sector entity-state override file is truncated.";
                    return false;
                }

                SectorEntityStateFileHeader header = UnsafeUtility.ReadArrayElement<SectorEntityStateFileHeader>((byte*)mapping.View, 0);
                sectorHash = header.SectorHash;
                if (header.CompressedSize <= 0 ||
                    header.DecompressedSize <= 0 ||
                    header.RecordCount == 0 ||
                    mapping.Length < headerSize ||
                    header.CompressedSize > mapping.Length - headerSize)
                {
                    error = "Sector entity-state override header is invalid.";
                    return false;
                }

                bool compact16 = header.Checksum == SectorEntityStateCompact16Magic;
                if (header.Checksum != 0u && !compact16)
                {
                    error = "Sector entity-state override format marker is unsupported.";
                    return false;
                }

                if (!TryConvertSectionCount(header.RecordCount, out int recordCount))
                {
                    error = "Sector entity-state override record count exceeds supported bounds.";
                    return false;
                }

                int recordStride = compact16
                    ? UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>()
                    : UnsafeUtility.SizeOf<EntityDataRecord>();
                long expectedByteLength64 = (long)recordCount * recordStride;
                if (expectedByteLength64 > int.MaxValue)
                {
                    error = "Sector entity-state override byte count exceeds supported bounds.";
                    return false;
                }

                int expectedByteLength = (int)expectedByteLength64;
                if (expectedByteLength != header.DecompressedSize)
                {
                    error = "Sector entity-state override byte count does not match the record count.";
                    return false;
                }

                using RegisteredTransientNativeArray<byte> rawBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    header.DecompressedSize,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorReadRawScratchLabel);
                NativeArray<byte> rawBytes = rawBytesOwner.Array;
                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBytes);
                int decompressedLength = Lz4BlockDecompress(
                    (byte*)mapping.View + headerSize,
                    header.CompressedSize,
                    rawPtr,
                    header.DecompressedSize,
                    out _);
                if (decompressedLength != header.DecompressedSize)
                {
                    error = "Sector entity-state override decompression failed.";
                    return false;
                }

                entityStates = new EntityDataRecord[recordCount];
                if (compact16)
                {
                    SectorCompactEntityStateRecord16* compactPtr = (SectorCompactEntityStateRecord16*)rawPtr;
                    for (int i = 0; i < entityStates.Length; i++)
                        entityStates[i] = UnpackCompactEntityState16(in compactPtr[i], sectorHash);
                }
                else
                {
                    fixed (EntityDataRecord* destinationPtr = entityStates)
                    {
                        if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, entityStates.Length * UnsafeUtility.SizeOf<EntityDataRecord>(), rawPtr, decompressedLength))
                        {
                            error = "Entity-state override copy exceeded destination bounds.";
                            return false;
                        }
                    }
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static long ComputeModPayloadPagedSectorHash(long gridX, float localX, long gridZ, float localZ)
        {
            if (!math.all(math.isfinite(new float2(localX, localZ))))
                return long.MinValue;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromGridLocal(
                gridX,
                0L,
                gridZ,
                new float3(localX, 0f, localZ));
            if (!AbsoluteUniversePosition.TryResolveSectorCoord(
                    in position,
                    PersistentWorldSectorEdgeLengthMeters,
                    out int2 sectorCoord))
                return long.MinValue;

            return PackSectorHash(sectorCoord);
        }

        internal static long ComputePersistentWorldPagedSectorHash(in AbsoluteUniversePosition position)
        {
            return TryQuantizePersistentWorldPagedSector(in position, out int2 sectorCoord)
                ? PackSectorHash(sectorCoord)
                : long.MinValue;
        }

        private static bool TryQuantizePersistentWorldPagedSector(in AbsoluteUniversePosition position, out int2 sectorCoord)
        {
            return AbsoluteUniversePosition.TryResolveSectorCoord(
                in position,
                PersistentWorldSectorEdgeLengthMeters,
                out sectorCoord);
        }

        private static int2 UnpackSectorHash(long sectorHash)
        {
            return new int2((int)(sectorHash >> 32), unchecked((int)(uint)sectorHash));
        }

        private static bool IsPersistentWorldPagedSectorWithinLoadWindow(long sectorHash, in AbsoluteUniversePosition centerPosition)
        {
            int2 sector = UnpackSectorHash(sectorHash);
            double3 center = centerPosition.ToAbsoluteDouble3();
            double sectorCenterX = ((double)sector.x + 0.5) * PersistentWorldSectorEdgeLengthMeters;
            double sectorCenterZ = ((double)sector.y + 0.5) * PersistentWorldSectorEdgeLengthMeters;
            double deltaX = sectorCenterX - center.x;
            double deltaZ = sectorCenterZ - center.z;
            return (deltaX * deltaX) + (deltaZ * deltaZ) <= PersistentWorldSectorLoadRadiusSq;
        }

        private static void ReportIndexedSectorQuarantine(long sectorHash, string primaryError, string backupError)
        {
            Interlocked.Exchange(ref s_indexedSectorQuarantineReported, 1);
            RecordIndexedSectorQuarantineHash(sectorHash);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _indexedSectorQuarantineWarningHash,
                unchecked((uint)sectorHash),
                1f);
            CrashTelemetryBuffer.ReportSaveSystemCriticalFault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning($"[SaveBinaryStorage] QUARANTINED indexed sector 0x{sectorHash:X16}. Primary: {primaryError} Backup: {backupError}");
#endif
        }

        private static void ReportIndexedSectorBackupRecovery(long sectorHash, string primaryError)
        {
            Interlocked.Exchange(ref s_indexedSectorBackupRecoveryReported, 1);
            RecordIndexedSectorBackupRecoveryHash(sectorHash);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _indexedSectorBackupRecoveryWarningHash,
                unchecked((uint)sectorHash),
                1f);
            CrashTelemetryBuffer.ReportSaveSystemCriticalFault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning($"[SaveBinaryStorage] RECOVERED indexed sector 0x{sectorHash:X16} from backup. Primary: {primaryError}");
#endif
        }

        private static void RecordIndexedSectorQuarantineHash(long sectorHash)
        {
            lock (s_indexedSectorQuarantineLock)
            {
                for (int i = 0; i < s_indexedSectorQuarantineHashCount; i++)
                {
                    if (s_indexedSectorQuarantineHashes[i] == sectorHash)
                        return;
                }

                if (s_indexedSectorQuarantineHashCount >= s_indexedSectorQuarantineHashes.Length)
                    return;

                s_indexedSectorQuarantineHashes[s_indexedSectorQuarantineHashCount] = sectorHash;
                s_indexedSectorQuarantineHashCount++;
            }
        }

        private static void RecordIndexedSectorBackupRecoveryHash(long sectorHash)
        {
            lock (s_indexedSectorBackupRecoveryLock)
            {
                for (int i = 0; i < s_indexedSectorBackupRecoveryHashCount; i++)
                {
                    if (s_indexedSectorBackupRecoveryHashes[i] == sectorHash)
                        return;
                }

                if (s_indexedSectorBackupRecoveryHashCount >= s_indexedSectorBackupRecoveryHashes.Length)
                    return;

                s_indexedSectorBackupRecoveryHashes[s_indexedSectorBackupRecoveryHashCount] = sectorHash;
                s_indexedSectorBackupRecoveryHashCount++;
            }
        }

        internal static long ComputeModPayloadSectorHash(uint modHash, long pagedSectorHash)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                hash = (hash ^ modHash) * 1099511628211UL;
                hash = (hash ^ (ulong)pagedSectorHash) * 1099511628211UL;
                hash &= 0x0000FFFFFFFFFFFFUL;
                return (long)(ModPayloadSectorPrefix | hash);
            }
        }

        internal static bool IsModPayloadSectorHash(long sectorHash)
        {
            return (((ulong)sectorHash) & ModPayloadSectorMask) == ModPayloadSectorPrefix;
        }

        internal static bool ConsumeIndexedSectorQuarantineFlag()
        {
            return Interlocked.Exchange(ref s_indexedSectorQuarantineReported, 0) != 0;
        }

        internal static bool ConsumeIndexedSectorBackupRecoveryFlag()
        {
            return Interlocked.Exchange(ref s_indexedSectorBackupRecoveryReported, 0) != 0;
        }

        internal static int CopyAndClearIndexedSectorQuarantineHashes(long[] destination)
        {
            if (destination == null || destination.Length <= 0)
                return 0;

            lock (s_indexedSectorQuarantineLock)
            {
                int copyCount = math.min(destination.Length, s_indexedSectorQuarantineHashCount);
                for (int i = 0; i < copyCount; i++)
                    destination[i] = s_indexedSectorQuarantineHashes[i];

                Array.Clear(s_indexedSectorQuarantineHashes, 0, s_indexedSectorQuarantineHashCount);
                s_indexedSectorQuarantineHashCount = 0;
                return copyCount;
            }
        }

        internal static int CopyAndClearIndexedSectorBackupRecoveryHashes(long[] destination)
        {
            if (destination == null || destination.Length <= 0)
                return 0;

            lock (s_indexedSectorBackupRecoveryLock)
            {
                int copyCount = math.min(destination.Length, s_indexedSectorBackupRecoveryHashCount);
                for (int i = 0; i < copyCount; i++)
                    destination[i] = s_indexedSectorBackupRecoveryHashes[i];

                Array.Clear(s_indexedSectorBackupRecoveryHashes, 0, s_indexedSectorBackupRecoveryHashCount);
                s_indexedSectorBackupRecoveryHashCount = 0;
                return copyCount;
            }
        }

        internal static bool TryCommitModPayloadSubSector(
            string absoluteSavePath,
            string tempOverridePath,
            uint modHash,
            long pagedSectorHash,
            NativeArray<byte> payloadBytes,
            int payloadLength,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || string.IsNullOrEmpty(tempOverridePath))
            {
                error = "Mod payload commit paths are invalid.";
                return false;
            }

            if (!TryDeleteFileIfExists(tempOverridePath, out string staleTempDeleteError))
            {
                error = staleTempDeleteError;
                return false;
            }

            if (modHash == 0u)
            {
                error = "Mod payload owner hash is zero.";
                return false;
            }

            if (!payloadBytes.IsCreated || payloadLength < 0 || payloadLength > ModPayloadMaxBytes || payloadLength > payloadBytes.Length)
            {
                error = "Mod payload exceeds the 16KB isolated sub-sector budget.";
                return false;
            }

            if ((payloadLength & 1) != 0)
            {
                error = "Mod payload rejected: odd byte length.";
                return false;
            }

            using RegisteredTransientNativeArray<byte> rawBlockBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                ModPayloadSubBlockSizeBytes,
                NativeArrayOptions.ClearMemory,
                IndexedSectorModPayloadRawBlockScratchLabel);
            NativeArray<byte> rawBlockBytes = rawBlockBytesOwner.Array;
            int compressedCapacity = ModPayloadSubBlockSizeBytes + (ModPayloadSubBlockSizeBytes / 255) + 16 + ProtectedCompressedBlockHeaderBytes + IndexedSectorBlockHeaderSize + 32;
            int fileCapacity = UnsafeUtility.SizeOf<SectorOverrideFileHeader>() + compressedCapacity;
            using RegisteredTransientNativeArray<byte> fileBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                fileCapacity,
                NativeArrayOptions.UninitializedMemory,
                IndexedSectorOverrideFileScratchLabel);
            NativeArray<byte> fileBytes = fileBytesOwner.Array;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlockBytes);
            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
            byte* payloadSource = payloadLength > 0
                ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payloadBytes)
                : null;

            if (payloadLength > 0)
            {
                if (!UnsafeMemoryCopyGuard.SafeCopy(rawPtr + ModPayloadHeaderSizeBytes, rawBlockBytes.Length - ModPayloadHeaderSizeBytes, payloadSource, payloadLength))
                {
                    error = "Mod payload write exceeded sub-sector bounds.";
                    return false;
                }
            }

            uint payloadChecksum = payloadLength > 0
                ? Hash32(payloadSource, payloadLength)
                : 0u;
            ModPayloadSubSectorHeader payloadHeader = new ModPayloadSubSectorHeader
            {
                Magic = ModPayloadMagic,
                Version = ModPayloadVersion,
                HeaderSize = ModPayloadHeaderSizeBytes,
                ModHash = modHash,
                PayloadLength = unchecked((ushort)payloadLength),
                Flags = 0,
                PagedSectorHash = pagedSectorHash,
                PayloadChecksum = payloadChecksum,
                Reserved = 0u
            };
            UnsafeUtility.CopyStructureToPtr(ref payloadHeader, rawPtr);
            uint rawBlockChecksum = Hash32(rawPtr, ModPayloadSubBlockSizeBytes);

            int fileCursor = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
            if (!TryWriteIndexedCompressedBlock(
                    rawPtr,
                    ModPayloadSubBlockSizeBytes,
                    filePtr,
                    fileBytes.Length,
                    ref fileCursor,
                    out int storedBlockLength,
                    out uint blockFlags,
                    out error))
            {
                return false;
            }

            long sectorHash = ComputeModPayloadSectorHash(modHash, pagedSectorHash);
            SectorOverrideFileHeader overrideHeader = new SectorOverrideFileHeader
            {
                SectorHash = sectorHash,
                CompressedSize = storedBlockLength,
                DecompressedSize = ModPayloadSubBlockSizeBytes,
                Checksum = rawBlockChecksum,
                Flags = blockFlags
            };

            UnsafeUtility.CopyStructureToPtr(ref overrideHeader, filePtr);
            string tempDirectory = Path.GetDirectoryName(tempOverridePath);
            if (!string.IsNullOrEmpty(tempDirectory))
                Directory.CreateDirectory(tempDirectory);

            if (!AsyncWriteManager.WriteAll(tempOverridePath, filePtr, fileCursor, out error))
            {
                _ = TryDeleteFileIfExists(tempOverridePath, out _);
                return false;
            }

            if (!AsyncWriteManager.FlushCriticalSavePath(tempOverridePath, fileCursor, out error))
            {
                _ = TryDeleteFileIfExists(tempOverridePath, out _);
                return false;
            }

            if (TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error))
                return true;

            _ = TryDeleteFileIfExists(tempOverridePath, out _);
            return false;
        }

        internal static bool TryReadIndexedModPayloadDirectory(
            string absolutePath,
            List<ModPayloadSectorInfo> results,
            out string error)
        {
            error = string.Empty;
            if (results == null)
            {
                error = "Mod payload directory destination is null.";
                return false;
            }

            results.Clear();
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectoryHeaderForMappedScan(
                        in header,
                        ref mapping,
                        out IndexedSectorDirectoryHeader directoryHeader,
                        out int entryCursor,
                        out long metadataEndOffset,
                        out error))
                {
                    return false;
                }

                byte* filePtr = (byte*)mapping.View;
                int sectorEntrySize = ResolveIndexedSectorEntrySize(header.Version);
                if (!TryDecodeIndexedSectorCount(in directoryHeader, out int indexedSectorCount, out error))
                    return false;
                int populatedCount = 0;
                using RegisteredTransientNativeArray<byte> rawBlockBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    ModPayloadSubBlockSizeBytes,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorModPayloadRawBlockScratchLabel);
                NativeArray<byte> rawBlockBytes = rawBlockBytesOwner.Array;
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = ReadIndexedSectorEntry(filePtr + entryCursor, sectorEntrySize);
                    entryCursor += sectorEntrySize;
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                    {
                        error = "Indexed sector entry exceeded the file bounds.";
                        return false;
                    }

                    populatedCount++;
                    if (!IsModPayloadSectorHash(entry.SectorHash))
                        continue;

                    if (!TryReadModPayloadHeaderFromEntry(ref mapping, in entry, rawBlockBytes, out ModPayloadSubSectorHeader payloadHeader, out _))
                        continue;

                    if (payloadHeader.ModHash == 0u ||
                        entry.SectorHash != ComputeModPayloadSectorHash(payloadHeader.ModHash, payloadHeader.PagedSectorHash))
                    {
                        error = "Mod payload directory identity mismatch.";
                        return false;
                    }

                    results.Add(new ModPayloadSectorInfo(
                        entry.SectorHash,
                        payloadHeader.ModHash,
                        payloadHeader.PagedSectorHash,
                        payloadHeader.PayloadLength,
                        payloadHeader.PayloadChecksum));
                }

                if (populatedCount != indexedSectorCount)
                {
                    error = "Indexed sector directory count mismatch.";
                    return false;
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadIndexedModPayloads(
            string absolutePath,
            List<ModPayloadSectorInfo> results,
            NativeArray<byte> payloadBytes,
            ModPayloadReadHandler readHandler,
            out string error)
        {
            error = string.Empty;
            if (!payloadBytes.IsCreated || payloadBytes.Length < ModPayloadMaxBytes || readHandler == null)
            {
                error = "Mod payload batch read request is invalid.";
                return false;
            }

            bool collectResults = results != null;
            if (collectResults)
                results.Clear();

            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectoryHeaderForMappedScan(
                        in header,
                        ref mapping,
                        out IndexedSectorDirectoryHeader directoryHeader,
                        out int entryCursor,
                        out long metadataEndOffset,
                        out error))
                {
                    return false;
                }

                byte* filePtr = (byte*)mapping.View;
                int sectorEntrySize = ResolveIndexedSectorEntrySize(header.Version);
                if (!TryDecodeIndexedSectorCount(in directoryHeader, out int indexedSectorCount, out error))
                    return false;
                int populatedCount = 0;
                int skippedModSectorCount = 0;
                using RegisteredTransientNativeArray<byte> rawBlockBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    ModPayloadSubBlockSizeBytes,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorModPayloadRawBlockScratchLabel);
                NativeArray<byte> rawBlockBytes = rawBlockBytesOwner.Array;
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = ReadIndexedSectorEntry(filePtr + entryCursor, sectorEntrySize);
                    entryCursor += sectorEntrySize;
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                    {
                        error = "Indexed sector entry exceeded the file bounds.";
                        return false;
                    }

                    populatedCount++;
                    if (!IsModPayloadSectorHash(entry.SectorHash))
                        continue;

                    if (!TryReadModPayloadHeaderFromEntry(ref mapping, in entry, rawBlockBytes, out ModPayloadSubSectorHeader payloadHeader, out _))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    if (payloadHeader.ModHash == 0u ||
                        entry.SectorHash != ComputeModPayloadSectorHash(payloadHeader.ModHash, payloadHeader.PagedSectorHash))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    ModPayloadSectorInfo sectorInfo = new ModPayloadSectorInfo(
                        entry.SectorHash,
                        payloadHeader.ModHash,
                        payloadHeader.PagedSectorHash,
                        payloadHeader.PayloadLength,
                        payloadHeader.PayloadChecksum);

                    if (!TryCopyModPayloadFromRawBlock(rawBlockBytes, payloadHeader.PayloadLength, payloadBytes, out error))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    if (!readHandler(in sectorInfo, payloadBytes, payloadHeader.PayloadLength, out error))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    if (collectResults)
                        results.Add(sectorInfo);
                }

                if (populatedCount != indexedSectorCount)
                {
                    error = "Indexed sector directory count mismatch.";
                    return false;
                }

                if (skippedModSectorCount > 0)
                    error = $"Skipped {skippedModSectorCount} invalid mod payload sector(s).";

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadModPayloadSubSector(
            string absolutePath,
            uint modHash,
            long pagedSectorHash,
            NativeArray<byte> destination,
            out int payloadLength,
            out string error)
        {
            payloadLength = 0;
            error = string.Empty;
            if (modHash == 0u || !destination.IsCreated)
            {
                error = "Mod payload read request is invalid.";
                return false;
            }

            long sectorHash = ComputeModPayloadSectorHash(modHash, pagedSectorHash);
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                    return false;

                if (!TryFindIndexedSectorEntryIndex(sectorEntries, sectorHash, out int sectorEntryIndex))
                {
                    error = $"Mod payload sector 0x{sectorHash:X16} is missing.";
                    return false;
                }

                SectorEntry entry = sectorEntries[sectorEntryIndex];
                using RegisteredTransientNativeArray<byte> rawBlockBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    ModPayloadSubBlockSizeBytes,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorModPayloadRawBlockScratchLabel);
                NativeArray<byte> rawBlockBytes = rawBlockBytesOwner.Array;
                if (!TryReadModPayloadHeaderFromEntry(ref mapping, in entry, rawBlockBytes, out ModPayloadSubSectorHeader payloadHeader, out error))
                    return false;

                if (payloadHeader.ModHash != modHash || payloadHeader.PagedSectorHash != pagedSectorHash)
                {
                    error = "Mod payload header identity mismatch.";
                    return false;
                }

                payloadLength = payloadHeader.PayloadLength;
                if (payloadLength > destination.Length)
                {
                    error = "Destination buffer is smaller than the mod payload.";
                    return false;
                }

                return TryCopyModPayloadFromRawBlock(rawBlockBytes, payloadLength, destination, out error);
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadModPayloadSubSector(
            string absolutePath,
            long sectorHash,
            uint modHash,
            long pagedSectorHash,
            NativeArray<byte> destination,
            out int payloadLength,
            out string error)
        {
            long expectedSectorHash = ComputeModPayloadSectorHash(modHash, pagedSectorHash);
            if (sectorHash != expectedSectorHash)
            {
                payloadLength = 0;
                error = "Mod payload sector hash does not match the mod/page identity.";
                return false;
            }

            return TryReadModPayloadSubSector(
                absolutePath,
                modHash,
                pagedSectorHash,
                destination,
                out payloadLength,
                out error);
        }

        private static bool TryReadModPayloadHeaderFromEntry(
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            in SectorEntry entry,
            NativeArray<byte> rawBlockBytes,
            out ModPayloadSubSectorHeader payloadHeader,
            out string error)
        {
            payloadHeader = default;
            if (!rawBlockBytes.IsCreated || rawBlockBytes.Length < ModPayloadSubBlockSizeBytes)
            {
                error = "Mod payload read buffer is not initialized.";
                return false;
            }

            if (entry.DecompressedSize != ModPayloadSubBlockSizeBytes)
            {
                error = "Mod payload sector decompressed size mismatch.";
                return false;
            }

            if (!TryReadIndexedCompressedBlock(ref mapping, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, rawBlockBytes, out int decompressedLength, out error))
                return false;

            if (decompressedLength != ModPayloadSubBlockSizeBytes)
            {
                error = "Mod payload sector length mismatch.";
                return false;
            }

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlockBytes);
            if (!TryVerifyIndexedSectorPayloadChecksum(in entry, rawPtr, decompressedLength, out error))
                return false;

            payloadHeader = UnsafeUtility.ReadArrayElement<ModPayloadSubSectorHeader>(rawPtr, 0);
            if (payloadHeader.Magic != ModPayloadMagic ||
                payloadHeader.Version != ModPayloadVersion ||
                payloadHeader.HeaderSize != ModPayloadHeaderSizeBytes ||
                payloadHeader.PayloadLength > ModPayloadMaxBytes ||
                (payloadHeader.PayloadLength & 1) != 0)
            {
                error = "Mod payload header is invalid.";
                return false;
            }

            if (payloadHeader.PayloadLength > 0)
            {
                uint computedPayloadChecksum = Hash32(rawPtr + ModPayloadHeaderSizeBytes, payloadHeader.PayloadLength);
                if (computedPayloadChecksum != payloadHeader.PayloadChecksum)
                {
                    error = "Mod payload checksum mismatch.";
                    return false;
                }
            }
            else if (payloadHeader.PayloadChecksum != 0u)
            {
                error = "Empty mod payload checksum is non-zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryCopyModPayloadFromRawBlock(
            NativeArray<byte> rawBlockBytes,
            int payloadLength,
            NativeArray<byte> destination,
            out string error)
        {
            error = string.Empty;
            if (!rawBlockBytes.IsCreated ||
                !destination.IsCreated ||
                rawBlockBytes.Length < ModPayloadSubBlockSizeBytes ||
                payloadLength < 0 ||
                payloadLength > ModPayloadMaxBytes ||
                payloadLength > destination.Length)
            {
                error = "Mod payload copy request is invalid.";
                return false;
            }

            if (payloadLength <= 0)
                return true;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlockBytes);
            byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, destination.Length, rawPtr + ModPayloadHeaderSizeBytes, payloadLength))
            {
                error = "Mod payload read exceeded destination bounds.";
                return false;
            }

            return true;
        }

        private static bool TryReadIndexedDirectoryHeaderForMappedScan(
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out IndexedSectorDirectoryHeader directoryHeader,
            out int entryCursor,
            out long metadataEndOffset,
            out string error)
        {
            directoryHeader = default;
            entryCursor = 0;
            metadataEndOffset = 0L;
            error = string.Empty;

            if (!TryValidateIndexedBlockStorageHeader(in header, out error))
                return false;

            int directoryOffset = ResolveExpectedHeaderSize(header.Version);
            if (mapping.Length < directoryOffset ||
                (long)directoryOffset > mapping.Length - IndexedSectorDirectoryHeaderSize)
            {
                error = "Indexed sector directory header is truncated.";
                return false;
            }

            byte* filePtr = (byte*)mapping.View;
            directoryHeader = UnsafeUtility.ReadArrayElement<IndexedSectorDirectoryHeader>(filePtr + directoryOffset, 0);
            if (!TryDecodeIndexedSectorCount(in directoryHeader, out _, out error))
                return false;

            if (directoryHeader.ChunkSizeMeters <= 0)
            {
                error = "Indexed sector directory chunk size is invalid.";
                return false;
            }

            int sectorEntrySize = ResolveIndexedSectorEntrySize(header.Version);
            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * sectorEntrySize);
            if ((long)directoryOffset > mapping.Length - directoryBytes)
            {
                error = "Indexed sector directory exceeds the file bounds.";
                return false;
            }

            long directoryEndOffset = directoryOffset + (long)directoryBytes;
            long metadataOffset = header.PlayerOffset;
            if (metadataOffset < directoryEndOffset || metadataOffset >= mapping.Length)
            {
                error = "Indexed metadata block offset is out of bounds.";
                return false;
            }

            if (directoryHeader.MetadataCompressedSize < 0 ||
                metadataOffset > mapping.Length - directoryHeader.MetadataCompressedSize)
            {
                error = "Indexed metadata block size is out of bounds.";
                return false;
            }

            entryCursor = directoryOffset + IndexedSectorDirectoryHeaderSize;
            metadataEndOffset = metadataOffset + directoryHeader.MetadataCompressedSize;
            return true;
        }

        internal static bool TryCompactIndexedPersistentWorldSectors(string absolutePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Indexed sector compaction path is invalid.";
                return false;
            }

            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            NativeArray<byte> compactBytes = default;
            int compactLength = 0;
            bool writePrepared = false;
            try
            {
                if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                    return false;

                int headerSizeBytes = ResolveExpectedHeaderSize(header.Version);
                int sectorEntrySize = ResolveIndexedSectorEntrySize(header.Version);
                int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * sectorEntrySize);
                long metadataOffset = header.PlayerOffset;
                long metadataEndOffset = metadataOffset + directoryHeader.MetadataCompressedSize;
                if (metadataOffset < headerSizeBytes + directoryBytes ||
                    metadataEndOffset < metadataOffset ||
                    metadataEndOffset > mapping.Length)
                {
                    error = "Indexed sector compaction metadata range is invalid.";
                    return false;
                }

                long compactLengthLong = metadataEndOffset;
                if (!TryDecodeIndexedSectorCount(in directoryHeader, out int indexedSectorCount, out error))
                    return false;
                int populatedSectorCount = 0;
                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    populatedSectorCount++;
                    compactLengthLong += entry.CompressedSize;
                    if (compactLengthLong > int.MaxValue)
                    {
                        error = "Indexed sector compaction exceeds the portable native buffer range.";
                        return false;
                    }
                }

                if (populatedSectorCount != indexedSectorCount)
                {
                    error = "Indexed sector compaction directory count mismatch.";
                    return false;
                }

                if (compactLengthLong >= mapping.Length)
                    return true;

                byte* mappedFilePtr = (byte*)mapping.View;
                byte* directoryPtr = mappedFilePtr + headerSizeBytes;
                if (!TryRecoverCachedIndexedMetadataHashLow32(
                        in header,
                        directoryPtr,
                        directoryBytes,
                        sectorEntries,
                        out ulong metadataHash64))
                {
                    error = "Indexed sector compaction metadata hash recovery failed.";
                    return false;
                }

                compactLength = (int)compactLengthLong;
                compactBytes = AllocateRegisteredPersistentScratchNativeArray<byte>(
                    compactLength,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorCompactionBufferLabel);
                byte* compactPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compactBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(compactPtr, compactLength, mappedFilePtr, metadataEndOffset))
                {
                    error = "Indexed sector compaction metadata copy exceeded bounds.";
                    return false;
                }

                long writeCursor = metadataEndOffset;
                int directoryEntryCursor = headerSizeBytes + IndexedSectorDirectoryHeaderSize;
                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                    {
                        directoryEntryCursor += sectorEntrySize;
                        continue;
                    }

                    if (!UnsafeMemoryCopyGuard.SafeCopy(
                            compactPtr + writeCursor,
                            compactLength - writeCursor,
                            mappedFilePtr + entry.ByteOffset,
                            entry.CompressedSize))
                    {
                        error = "Indexed sector compaction sector copy exceeded bounds.";
                        return false;
                    }

                    entry.ByteOffset = writeCursor;
                    sectorEntries[i] = entry;
                    WriteIndexedSectorEntry(compactPtr + directoryEntryCursor, sectorEntrySize, in entry);
                    directoryEntryCursor += sectorEntrySize;
                    writeCursor += entry.CompressedSize;
                }

                SaveFileHeader updatedHeader = ReadVersionedSaveFileHeader(compactPtr, header.Version);
                ulong directoryHash64 = updatedHeader.PlayerOffset > headerSizeBytes
                    ? Hash64(compactPtr + headerSizeBytes, (int)(updatedHeader.PlayerOffset - headerSizeBytes))
                    : 0UL;
                updatedHeader.HashPayload64 = metadataHash64 ^ directoryHash64;
                updatedHeader.Checksum = ComputeIndexedChecksumRoot(unchecked((uint)metadataHash64), sectorEntries);
                updatedHeader.HashHeader64 = 0UL;
                updatedHeader.HashHeader64 = ComputeHeaderHash(ref updatedHeader);
                WriteVersionedSaveFileHeader(compactPtr, ref updatedHeader);
                writePrepared = true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
                if (!writePrepared && compactBytes.IsCreated)
                    DisposeRegisteredPersistentScratchNativeArray(ref compactBytes, IndexedSectorCompactionBufferLabel);
            }

            try
            {
                byte* compactPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compactBytes);
                return AsyncWriteManager.OverwriteAllCritical(absolutePath, compactPtr, compactLength, out error);
            }
            finally
            {
                if (compactBytes.IsCreated)
                    DisposeRegisteredPersistentScratchNativeArray(ref compactBytes, IndexedSectorCompactionBufferLabel);
            }
        }

        private static void QueueIndexedPersistentWorldDefragmentation(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) ||
                Interlocked.CompareExchange(ref s_indexedSectorDefragmentationQueued, 1, 0) != 0)
            {
                return;
            }

            s_indexedSectorDefragmentationPath = absolutePath;
            if (!ThreadPool.QueueUserWorkItem(s_indexedSectorDefragmentationCallback))
            {
                s_indexedSectorDefragmentationPath = null;
                Interlocked.Exchange(ref s_indexedSectorDefragmentationQueued, 0);
            }
        }

        private static void RunIndexedPersistentWorldDefragmentation(object state)
        {
            string queuedPath = s_indexedSectorDefragmentationPath;
            try
            {
                if (!string.IsNullOrEmpty(queuedPath))
                    TryCompactIndexedPersistentWorldSectors(queuedPath, out _);
            }
            finally
            {
                s_indexedSectorDefragmentationPath = null;
                Interlocked.Exchange(ref s_indexedSectorDefragmentationQueued, 0);
            }
        }

        internal static bool TryCommitIndexedPersistentWorldSectorOverride(
            string absoluteSavePath,
            string sectorOverridePath,
            out string error,
            bool refreshBackupBeforeCommit = true)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || string.IsNullOrEmpty(sectorOverridePath))
            {
                error = "Sector override commit paths are invalid.";
                return false;
            }

            if (!File.Exists(absoluteSavePath) || !File.Exists(sectorOverridePath))
            {
                error = "Sector override commit source file is missing.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(sectorOverridePath, out AsyncWriteManager.ReadOnlyMapping overrideMapping, out error))
                return false;

            long sectorHash = 0L;
            int overrideCompressedSize = 0;
            int overrideDecompressedSize = 0;
            uint overrideChecksum = 0u;
            RegisteredTransientNativeArray<byte> overrideBlockBytesOwner = default;
            NativeArray<byte> overrideBlockBytes = default;
            bool overrideBlockPrepared = false;
            try
            {
                int overrideHeaderSize = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
                if (overrideMapping.Length < overrideHeaderSize + IndexedSectorBlockHeaderSize)
                {
                    error = "Sector override commit file is truncated.";
                    return false;
                }

                SectorOverrideFileHeader overrideHeader = UnsafeUtility.ReadArrayElement<SectorOverrideFileHeader>((byte*)overrideMapping.View, 0);
                sectorHash = overrideHeader.SectorHash;
                overrideCompressedSize = overrideHeader.CompressedSize;
                overrideDecompressedSize = overrideHeader.DecompressedSize;
                overrideChecksum = overrideHeader.Checksum;
                if (overrideCompressedSize <= IndexedSectorBlockHeaderSize ||
                    overrideDecompressedSize <= 0 ||
                    overrideMapping.Length < overrideHeaderSize ||
                    overrideCompressedSize > overrideMapping.Length - overrideHeaderSize)
                {
                    error = "Sector override commit header is invalid.";
                    return false;
                }

                overrideBlockBytesOwner = CreateRegisteredTransientNativeArray<byte>(
                    overrideCompressedSize,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorOverrideCompressedBlockScratchLabel);
                overrideBlockBytes = overrideBlockBytesOwner.Array;
                byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(overrideBlockBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, overrideBlockBytes.Length, (byte*)overrideMapping.View + overrideHeaderSize, overrideCompressedSize))
                {
                    error = "Sector override staging copy exceeded destination bounds.";
                    return false;
                }

                if (!TryVerifySectorOverrideCompressedBlock(overrideBlockBytes, overrideCompressedSize, overrideDecompressedSize, overrideChecksum, out error))
                    return false;

                overrideBlockPrepared = true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref overrideMapping);
                if (!overrideBlockPrepared)
                    overrideBlockBytesOwner.Dispose();
            }

            if (!TryReadValidatedHeader(absoluteSavePath, out AsyncWriteManager.ReadOnlyMapping saveMapping, out SaveFileHeader saveHeader, out _, out error))
                return false;

            SectorEntry[] sectorEntries;
            IndexedSectorDirectoryHeader directoryHeader;
            ulong metadataHash64;
            IndexedSectorCommitTarget commitTarget;
            int sectorCountDelta;
            long originalSaveLength = saveMapping.Length;
            try
            {
                if (!TryReadIndexedDirectory(in saveHeader, ref saveMapping, out directoryHeader, out sectorEntries, out error))
                    return false;

                int headerSizeBytes = ResolveExpectedHeaderSize(saveHeader.Version);
                long directoryBytesLong = (long)saveHeader.PlayerOffset - headerSizeBytes;
                if (directoryBytesLong < 0L || directoryBytesLong > int.MaxValue)
                {
                    error = "Indexed sector directory length exceeds the supported range.";
                    return false;
                }

                int directoryBytes = (int)directoryBytesLong;
                byte* directoryPtr = directoryBytes > 0
                    ? (byte*)saveMapping.View + headerSizeBytes
                    : null;
                if (!TryRecoverCachedIndexedMetadataHashLow32(
                        in saveHeader,
                        directoryPtr,
                        directoryBytes,
                        sectorEntries,
                        out metadataHash64))
                {
                    error = "Cached indexed metadata hash low32 failed checksum-root validation.";
                    return false;
                }

                if (!TryResolveIndexedSectorCommitTarget(
                        sectorEntries,
                        sectorHash,
                        overrideCompressedSize,
                        originalSaveLength,
                        out commitTarget,
                        out sectorCountDelta,
                        out error))
                {
                    return false;
                }
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref saveMapping);
            }

            NativeArray<byte> commitBytes = default;
            try
            {
                if (refreshBackupBeforeCommit && !TryPrepareIndexedSectorCommitBackup(absoluteSavePath, out error))
                    return false;

                long newLength = commitTarget.NewFileLength;
                if (newLength <= 0L || newLength > int.MaxValue)
                {
                    error = "Sector override commit exceeds the portable native buffer range.";
                    return false;
                }

                AsyncWriteManager.InvalidateCachedReadWindows(absoluteSavePath);
                // COLD ALLOC: NativeArray<byte>[newLength] - portable indexed-sector commit buffer - owner: SaveBinaryStorage
                commitBytes = AllocateRegisteredPersistentScratchNativeArray<byte>(
                    (int)newLength,
                    NativeArrayOptions.UninitializedMemory,
                    IndexedSectorCommitBufferLabel);
                byte* mappedFilePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(commitBytes);
                UnsafeUtility.MemClear(mappedFilePtr, newLength);
                int bytesToCopy = (int)(originalSaveLength < newLength ? originalSaveLength : newLength);
                if (bytesToCopy > 0 &&
                    !AsyncWriteManager.TryCopyFromCachedReadWindow(absoluteSavePath, 0L, mappedFilePtr, bytesToCopy, out error))
                {
                    return false;
                }

                byte* overrideBlockPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(overrideBlockBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(mappedFilePtr + commitTarget.WriteOffset, newLength - commitTarget.WriteOffset, overrideBlockPtr, overrideCompressedSize))
                {
                    error = "Sector override commit copy exceeded mapped file bounds.";
                    return false;
                }

                if (commitTarget.ReusedExistingSlot != 0)
                {
                    int previousCompressedSize = sectorEntries[commitTarget.SlotIndex].CompressedSize;
                    int trailingSlack = previousCompressedSize - overrideCompressedSize;
                    if (trailingSlack > 0)
                        UnsafeUtility.MemClear(mappedFilePtr + commitTarget.WriteOffset + overrideCompressedSize, trailingSlack);
                }

                int headerSizeBytes = ResolveExpectedHeaderSize(saveHeader.Version);
                int sectorEntrySize = ResolveIndexedSectorEntrySize(saveHeader.Version);
                int directoryEntryOffset = headerSizeBytes + IndexedSectorDirectoryHeaderSize + (commitTarget.SlotIndex * sectorEntrySize);
                SectorEntry updatedEntry = new SectorEntry
                {
                    SectorHash = sectorHash,
                    ByteOffset = commitTarget.WriteOffset,
                    CompressedSize = overrideCompressedSize,
                    DecompressedSize = overrideDecompressedSize,
                    Checksum = overrideChecksum
                };
                WriteIndexedSectorEntry(mappedFilePtr + directoryEntryOffset, sectorEntrySize, in updatedEntry);

                if (commitTarget.InsertedNewSlot != 0 && sectorCountDelta != 0)
                {
                    long updatedSectorCount = (long)directoryHeader.SectorCount + sectorCountDelta;
                    if (updatedSectorCount < 0L || updatedSectorCount > IndexedSectorDirectorySlotCount)
                    {
                        error = $"Indexed sector directory count {updatedSectorCount} exceeded slot capacity {IndexedSectorDirectorySlotCount}.";
                        return false;
                    }

                    directoryHeader.SectorCount = (uint)updatedSectorCount;
                    UnsafeUtility.CopyStructureToPtr(ref directoryHeader, mappedFilePtr + headerSizeBytes);
                }

                sectorEntries[commitTarget.SlotIndex] = updatedEntry;
                SaveFileHeader updatedHeader = ReadVersionedSaveFileHeader(mappedFilePtr, saveHeader.Version);
                ulong newDirectoryHash64 = updatedHeader.PlayerOffset > headerSizeBytes
                    ? Hash64(mappedFilePtr + headerSizeBytes, (int)(updatedHeader.PlayerOffset - headerSizeBytes))
                    : 0UL;
                updatedHeader.HashPayload64 = metadataHash64 ^ newDirectoryHash64;
                updatedHeader.Checksum = ComputeIndexedChecksumRoot(unchecked((uint)metadataHash64), sectorEntries);
                updatedHeader.HashHeader64 = 0UL;
                updatedHeader.HashHeader64 = ComputeHeaderHash(ref updatedHeader);
                WriteVersionedSaveFileHeader(mappedFilePtr, ref updatedHeader);
                if (!AsyncWriteManager.OverwriteAllCritical(absoluteSavePath, mappedFilePtr, (int)newLength, out error))
                    return false;
            }
            catch (Exception ex)
            {
                error = $"Sector override commit failed: {ex.Message}";
                return false;
            }
            finally
            {
                if (commitBytes.IsCreated)
                    DisposeRegisteredPersistentScratchNativeArray(ref commitBytes, IndexedSectorCommitBufferLabel);
                overrideBlockBytesOwner.Dispose();
            }

            if (!TryDeleteFileIfExists(sectorOverridePath, out error))
                return false;

            QueueIndexedPersistentWorldDefragmentation(absoluteSavePath);
            return true;
        }

        internal static bool TryMeasureLoadVoxelDeltaSnapshotByteLength(
            string absolutePath,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out int voxelDeltaSnapshotByteLength,
            out string error)
        {
            voxelDeltaSnapshotByteLength = 0;
            error = string.Empty;

            if (TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping v8Mapping, out SaveFileHeader v8Header, out _, out string headerError))
            {
                try
                {
                    if ((v8Header.Flags & FlagIndexedSectorBlocks) != 0 && v8Header.Version >= IndexedBlockStorageVersion)
                    {
                        return TryMeasureIndexedV8VoxelDeltaSnapshotByteLength(
                            in v8Header,
                            ref v8Mapping,
                            rawBuffer,
                            out voxelDeltaSnapshotByteLength,
                            out error);
                    }
                }
                finally
                {
                    AsyncWriteManager.CloseReadOnlyMapping(ref v8Mapping);
                }
            }
            else if (!string.IsNullOrEmpty(headerError))
            {
                error = headerError;
                return false;
            }

            if (!TryReadPayload(absolutePath, rawBuffer, compressedBuffer, out SaveFileHeader header, out _, out byte* rawPtr, out int rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            return TryResolveVoxelDeltaSnapshotByteRange(
                rawPtr,
                rawPayloadLength,
                header,
                out _,
                out voxelDeltaSnapshotByteLength,
                out error);
        }

        private static bool TryMeasureIndexedV8VoxelDeltaSnapshotByteLength(
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            NativeArray<byte> rawBuffer,
            out int voxelDeltaSnapshotByteLength,
            out string error)
        {
            voxelDeltaSnapshotByteLength = 0;
            error = string.Empty;

            if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                return false;

            if (!TryReadIndexedCompressedBlock(ref mapping, header.PlayerOffset, directoryHeader.MetadataCompressedSize, directoryHeader.MetadataDecompressedSize, rawBuffer, out int metadataRawLength, out error))
                return false;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);
            int headerSizeBytes = ResolveExpectedHeaderSize(header.Version);
            ulong directoryHash64 = header.PlayerOffset > headerSizeBytes
                ? Hash64((byte*)mapping.View + headerSizeBytes, (int)(header.PlayerOffset - headerSizeBytes))
                : 0UL;
            ulong payloadHash64 = metadataHash64 ^ directoryHash64;
            if (payloadHash64 != header.HashPayload64)
            {
                error = "Indexed aggregate payload checksum mismatch.";
                return false;
            }

            if (header.Version >= HeaderChecksumVersion && !IsIndexedChecksumRootValid(unchecked((uint)metadataHash64), sectorEntries, header.Checksum))
            {
                error = "Indexed checksum-chain root mismatch.";
                return false;
            }

            if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, metadataRawLength, header.Version, out PayloadPrefixInfo prefix, out error))
                return false;

            int payloadCursor = prefix.PrefixSizeBytes;
            if (!IsByteRangeWithin(payloadCursor, prefix.SceneNameByteLength, metadataRawLength))
            {
                error = "Indexed metadata scene-name range is invalid.";
                return false;
            }

            payloadCursor += prefix.SceneNameByteLength;
            if (!IsByteRangeWithin(payloadCursor, prefix.GameVersionByteLength, metadataRawLength))
            {
                error = "Indexed metadata game-version range is invalid.";
                return false;
            }

            payloadCursor += prefix.GameVersionByteLength;
            if (!TryDecodeSaveDataByteLength(in prefix, out int saveDataLength, out error))
                return false;
            if (!IsByteRangeWithin(payloadCursor, saveDataLength, metadataRawLength))
            {
                error = "Indexed metadata save-data range is invalid.";
                return false;
            }

            payloadCursor += saveDataLength;
            if (!TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error))
                return false;
            if (packedQuestWordCount > 0)
            {
                if (!IsByteRangeWithin(payloadCursor, PackedQuestStateSectionHeaderSize, metadataRawLength))
                {
                    error = "Indexed packed quest section header is truncated.";
                    return false;
                }

                QuestSaveHeader packedQuestHeader = UnsafeUtility.ReadArrayElement<QuestSaveHeader>(AddByteOffset(rawPtr, payloadCursor), 0);
                if (!TryValidatePackedQuestSectionHeader(in packedQuestHeader, packedQuestWordCount, out error))
                    return false;

                int packedQuestBytes = packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                payloadCursor += PackedQuestStateSectionHeaderSize;
                if (!IsByteRangeWithin(payloadCursor, packedQuestBytes, metadataRawLength))
                {
                    error = "Indexed packed quest section exceeds the metadata payload bounds.";
                    return false;
                }

                payloadCursor += packedQuestBytes;
            }

            int ecosystemHeaderSize = ResolveEcosystemSectionHeaderSize(header.Version);
            if (!IsByteRangeWithin(payloadCursor, ecosystemHeaderSize, metadataRawLength))
            {
                error = "Indexed ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader ecosystemHeader = ReadEcosystemSectionHeader(AddByteOffset(rawPtr, payloadCursor), ecosystemHeaderSize);
            if (!TryConvertSectionCount(ecosystemHeader.RecordCount, out int ecosystemRecordCount) ||
                !TryComputeEcosystemSectionLength(ecosystemRecordCount, header.Version, out int ecosystemSectionLength))
            {
                error = "Indexed ecosystem section header exceeds supported bounds.";
                return false;
            }

            if (!IsByteRangeWithin(payloadCursor, ecosystemSectionLength, metadataRawLength))
            {
                error = "Indexed ecosystem section exceeds the metadata payload bounds.";
                return false;
            }

            payloadCursor += ecosystemSectionLength;
            voxelDeltaSnapshotByteLength = math.max(0, metadataRawLength - payloadCursor);
            return true;
        }

        internal static bool TryLoadSaveData(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            NativeArray<byte> voxelDeltaSnapshotDestination,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out int voxelDeltaSnapshotBytes,
            out ushort playerDialogueChoiceFlags,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out int detectedVersion,
            out bool indexedBackupRecoveryUsed,
            out string error)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldDeltas = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshotBytes = 0;
            playerDialogueChoiceFlags = 0;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            detectedVersion = 0;
            indexedBackupRecoveryUsed = false;

            if (TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping v8Mapping, out SaveFileHeader v8Header, out _, out string headerError))
            {
                try
                {
                    if ((v8Header.Flags & FlagIndexedSectorBlocks) != 0 && v8Header.Version >= IndexedBlockStorageVersion)
                    {
                        return TryLoadSaveDataIndexedV8(
                            absolutePath,
                            slotName,
                            rawBuffer,
                            in v8Header,
                            ref v8Mapping,
                            voxelDeltaSnapshotDestination,
                            out data,
                            out packedQuestHeader,
                            out packedQuestStateWords,
                            out persistentWorldDeltas,
                            out ecosystemSectorStates,
                            out voxelDeltaSnapshotBytes,
                            out playerDialogueChoiceFlags,
                            out metadata,
                            out payloadHash64,
                            out rawPayloadLength,
                            out detectedVersion,
                            out indexedBackupRecoveryUsed,
                            out error);
                    }
                }
                finally
                {
                    AsyncWriteManager.CloseReadOnlyMapping(ref v8Mapping);
                }
            }
            else if (!string.IsNullOrEmpty(headerError))
            {
                error = headerError;
                return false;
            }

            if (!TryReadPayload(absolutePath, rawBuffer, compressedBuffer, out SaveFileHeader header, out PayloadPrefixInfo prefix, out byte* rawPtr, out rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            payloadHash64 = header.HashPayload64;
            playerDialogueChoiceFlags = DecodePlayerDialogueChoiceFlags(in header);

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            if (!TryDecodeSaveDataByteLength(in prefix, out int saveDataLength, out error))
                return false;
            if (!IsByteRangeWithin(cursor, saveDataLength, rawPayloadLength))
            {
                error = "Save payload byte range is invalid.";
                return false;
            }

            if (!SaveBinaryPayloadCodec.TryRead(AddByteOffset(rawPtr, cursor), saveDataLength, out data, out int bytesRead, out error))
                return false;

            if (bytesRead != saveDataLength)
            {
                error = "Binary save payload length mismatch.";
                return false;
            }

            if (!TryReadPackedQuestStateWords(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    cursor + saveDataLength,
                    out packedQuestHeader,
                    out packedQuestStateWords,
                    out error))
            {
                return false;
            }
            playerDialogueChoiceFlags = ResolveLoadedPlayerDialogueChoiceFlags(
                header.Version,
                playerDialogueChoiceFlags,
                packedQuestStateWords);

            if (!TryReadPersistentWorldDeltas(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    out persistentWorldDeltas,
                    out error))
            {
                return false;
            }

            if (!TryReadEcosystemSectorStates(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    out ecosystemSectorStates,
                    out error))
            {
                return false;
            }

            if (!TryReadVoxelDeltaSnapshot(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    voxelDeltaSnapshotDestination,
                    out voxelDeltaSnapshotBytes,
                    out error))
            {
                return false;
            }

            detectedVersion = prefix.SaveDataVersion;
            if (!TryToRuntimePosition(prefix.PlayerPosition, out Vector3 playerPosition))
            {
                error = "Save payload contains an invalid player AUP position.";
                return false;
            }

            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = SaveMetadata.NormalizeSceneName(sceneName),
                PlayerPosition = playerPosition,
                Checksum = FormatPayloadChecksum(in header)
            };

            error = string.Empty;
            return true;
        }

        internal static uint Hash32(void* ptr, long length)
        {
            ulong hash = Hash64(ptr, length);
            return (uint)hash ^ (uint)(hash >> 32);
        }

        private static uint ComputeIndexedChecksumRoot(uint metadataChecksum, SectorEntry[] sectorEntries)
        {
            unchecked
            {
                uint root = metadataChecksum == 0u ? 2166136261u : metadataChecksum;
                if (sectorEntries == null)
                    return root;

                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    uint leafHash = FoldIndexedSectorChecksum(in entry);
                    root = (root ^ leafHash) * 16777619u;
                }

                return root == 0u ? 2166136261u : root;
            }
        }

        private static uint ComputeIndexedChecksumRoot(uint metadataChecksum, NativeArray<SectorEntry> sectorEntries)
        {
            unchecked
            {
                uint root = metadataChecksum == 0u ? 2166136261u : metadataChecksum;
                if (!sectorEntries.IsCreated)
                    return root;

                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    uint leafHash = FoldIndexedSectorChecksum(in entry);
                    root = (root ^ leafHash) * 16777619u;
                }

                return root == 0u ? 2166136261u : root;
            }
        }

        private static bool IsIndexedChecksumRootValid(uint metadataChecksum, SectorEntry[] sectorEntries, uint expectedChecksum)
        {
            uint checksumRoot = ComputeIndexedChecksumRoot(metadataChecksum, sectorEntries);
            if (checksumRoot == expectedChecksum)
                return true;

            return ComputeIndexedChecksumRootLegacyHash(metadataChecksum, sectorEntries) == expectedChecksum;
        }

        private static bool TryRecoverCachedIndexedMetadataHashLow32(
            in SaveFileHeader header,
            byte* directoryPtr,
            int directoryBytes,
            SectorEntry[] sectorEntries,
            out ulong metadataHash64)
        {
            metadataHash64 = 0UL;
            if (directoryBytes < 0 || (directoryBytes > 0 && directoryPtr == null))
                return false;

            ulong directoryHash64 = directoryBytes > 0
                ? Hash64(directoryPtr, directoryBytes)
                : 0UL;
            metadataHash64 = header.HashPayload64 ^ directoryHash64;
            if (header.Version < HeaderChecksumVersion)
                return true;

            return IsIndexedChecksumRootValid(unchecked((uint)metadataHash64), sectorEntries, header.Checksum);
        }

        private static bool IsIndexedChecksumRootValid(
            uint checksumRoot,
            byte* filePtr,
            int entryCursor,
            int sectorEntrySize,
            uint metadataChecksum,
            uint expectedChecksum)
        {
            if (checksumRoot == expectedChecksum)
                return true;

            return ComputeIndexedChecksumRootLegacyHashFromMappedDirectory(filePtr, entryCursor, sectorEntrySize, metadataChecksum) == expectedChecksum;
        }

        private static uint FoldIndexedSectorChecksum(in SectorEntry entry)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = MixChecksum32(hash, (uint)entry.SectorHash);
                hash = MixChecksum32(hash, (uint)((ulong)entry.SectorHash >> 32));
                hash = MixChecksum32(hash, entry.Checksum);
                hash = MixChecksum32(hash, (uint)entry.CompressedSize);
                hash = MixChecksum32(hash, (uint)entry.DecompressedSize);
                return hash == 0u ? 2166136261u : hash;
            }
        }

        private static uint MixChecksum32(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        private static bool TryComputeIndexedChecksumRootFromMappedDirectory(
            byte* filePtr,
            int entryCursor,
            int sectorEntrySize,
            long metadataEndOffset,
            long fileLength,
            int expectedSectorCount,
            uint metadataChecksum,
            bool computeChecksumRoot,
            out uint checksumRoot,
            out string error)
        {
            checksumRoot = metadataChecksum == 0u ? 2166136261u : metadataChecksum;
            error = string.Empty;
            int populatedCount = 0;

            unchecked
            {
                if (computeChecksumRoot)
                {
                    for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                    {
                        SectorEntry entry = ReadIndexedSectorEntry(filePtr + entryCursor, sectorEntrySize);
                        if (IsIndexedSectorEntryPopulated(in entry))
                        {
                            if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, fileLength))
                            {
                                error = "Indexed sector entry exceeded the file bounds.";
                                return false;
                            }

                            populatedCount++;
                            checksumRoot = (checksumRoot ^ FoldIndexedSectorChecksum(in entry)) * 16777619u;
                        }

                        entryCursor += sectorEntrySize;
                    }
                }
                else
                {
                    for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                    {
                        SectorEntry entry = ReadIndexedSectorEntry(filePtr + entryCursor, sectorEntrySize);
                        if (IsIndexedSectorEntryPopulated(in entry))
                        {
                            if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, fileLength))
                            {
                                error = "Indexed sector entry exceeded the file bounds.";
                                return false;
                            }

                            populatedCount++;
                        }

                        entryCursor += sectorEntrySize;
                    }
                }

                if (checksumRoot == 0u)
                    checksumRoot = 2166136261u;
            }

            if (populatedCount != expectedSectorCount)
            {
                error = "Indexed sector directory count mismatch.";
                return false;
            }

            return true;
        }

        private static uint ComputeIndexedChecksumRootLegacyHashFromMappedDirectory(
            byte* filePtr,
            int entryCursor,
            int sectorEntrySize,
            uint metadataChecksum)
        {
            unchecked
            {
                uint root = metadataChecksum == 0u ? 2166136261u : metadataChecksum;
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = ReadIndexedSectorEntry(filePtr + entryCursor, sectorEntrySize);
                    if (IsIndexedSectorEntryPopulated(in entry))
                    {
                        uint leafHash = FoldIndexedSectorChecksumLegacyHash(in entry);
                        root = (root ^ leafHash) * 16777619u;
                    }

                    entryCursor += sectorEntrySize;
                }

                return root == 0u ? 2166136261u : root;
            }
        }

        private static uint ComputeIndexedChecksumRootLegacyHash(uint metadataChecksum, SectorEntry[] sectorEntries)
        {
            unchecked
            {
                uint root = metadataChecksum == 0u ? 2166136261u : metadataChecksum;
                if (sectorEntries == null)
                    return root;

                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    uint leafHash = FoldIndexedSectorChecksumLegacyHash(in entry);
                    root = (root ^ leafHash) * 16777619u;
                }

                return root == 0u ? 2166136261u : root;
            }
        }

        private static uint FoldIndexedSectorChecksumLegacyHash(in SectorEntry entry)
        {
            LegacyIndexedChecksumLeaf leaf = new LegacyIndexedChecksumLeaf
            {
                SectorHash = entry.SectorHash,
                Checksum = entry.Checksum,
                CompressedSize = entry.CompressedSize,
                DecompressedSize = entry.DecompressedSize
            };

            return Hash32(
                UnsafeUtility.AddressOf(ref leaf),
                UnsafeUtility.SizeOf<LegacyIndexedChecksumLeaf>());
        }

        private static bool TryVerifyIndexedSectorPayloadChecksum(
            in SectorEntry entry,
            byte* rawPayloadPtr,
            int rawPayloadLength,
            out string error)
        {
            error = string.Empty;
            if (entry.Checksum == 0u)
                return true;

            if (rawPayloadPtr == null || rawPayloadLength <= 0)
            {
                error = "Indexed sector checksum input is invalid.";
                return false;
            }

            uint computedChecksum = Hash32(rawPayloadPtr, rawPayloadLength);
            if (computedChecksum != entry.Checksum)
            {
                error = $"Indexed sector checksum mismatch for sector 0x{entry.SectorHash:X16}.";
                return false;
            }

            return true;
        }

        private static bool TryVerifySectorOverrideCompressedBlock(
            NativeArray<byte> compressedBlock,
            int compressedBlockLength,
            int expectedDecompressedLength,
            uint expectedChecksum,
            out string error)
        {
            error = string.Empty;
            if (!compressedBlock.IsCreated ||
                compressedBlockLength <= IndexedSectorBlockHeaderSize ||
                compressedBlockLength > compressedBlock.Length ||
                expectedDecompressedLength <= 0)
            {
                error = "Sector override verification input is invalid.";
                return false;
            }

            using RegisteredTransientNativeArray<byte> rawBlockOwner = CreateRegisteredTransientNativeArray<byte>(
                expectedDecompressedLength,
                NativeArrayOptions.UninitializedMemory,
                IndexedSectorOverrideVerifyRawBlockScratchLabel);
            NativeArray<byte> rawBlock = rawBlockOwner.Array;
            byte* compressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(compressedBlock);
            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlock);
            IndexedSectorBlockHeader blockHeader = UnsafeUtility.ReadArrayElement<IndexedSectorBlockHeader>(compressedPtr, 0);
            int compressedPayloadLength = compressedBlockLength - IndexedSectorBlockHeaderSize;
            int decompressedLength;
            if ((blockHeader.Flags & FlagProtectedLz4Blocks) != 0)
            {
                decompressedLength = Lz4ProtectedBlockDecompress(
                    compressedPtr + IndexedSectorBlockHeaderSize,
                    compressedPayloadLength,
                    rawPtr,
                    rawBlock.Length);
            }
            else
            {
                decompressedLength = Lz4BlockDecompressStandard(
                    compressedPtr + IndexedSectorBlockHeaderSize,
                    compressedPayloadLength,
                    rawPtr,
                    rawBlock.Length,
                    out _);
            }

            if (decompressedLength <= 0)
            {
                error = "Sector override verification decompression failed.";
                return false;
            }

            if ((blockHeader.Flags & FlagTokenSubstitution) != 0 &&
                !TryExpandTokenizedPayloadInPlace(rawPtr, decompressedLength, rawBlock.Length, out decompressedLength, out error))
            {
                error = "Sector override token payload expansion failed.";
                return false;
            }

            if (decompressedLength != expectedDecompressedLength)
            {
                error = "Sector override verification expanded length mismatch.";
                return false;
            }

            if (expectedChecksum != 0u && Hash32(rawPtr, decompressedLength) != expectedChecksum)
            {
                error = "Sector override verification checksum mismatch.";
                return false;
            }

            return true;
        }

        internal static int AlignExplorationMortonByteCount(int byteCount)
        {
            if (byteCount <= 0)
                return 0;

            int aligned = (byteCount + (ExplorationMortonMaskAlignmentBytes - 1)) & ~(ExplorationMortonMaskAlignmentBytes - 1);
            return math.min(aligned, ExplorationMapDTO.MortonMaskByteCount);
        }

        internal static uint ComputeExplorationMortonSeed(uint payload)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ ExplorationMortonBuildSalt32) * 16777619u;
                hash = (hash ^ payload) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        private static string ResolveIndexedSaveBackupPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || absolutePath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return $"{absolutePath}.bak";
        }

        private static bool TryDeleteFileIfExists(string absolutePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
                return true;

            try
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);

                return true;
            }
            catch (IOException ex)
            {
                error = $"File delete failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = $"File delete unauthorized for '{absolutePath}': {ex.Message}";
                return false;
            }
            catch (ArgumentException ex)
            {
                error = $"File delete path invalid for '{absolutePath}': {ex.Message}";
                return false;
            }
            catch (NotSupportedException ex)
            {
                error = $"File delete path unsupported for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            }
        }

        private static bool TryPrepareIndexedSectorCommitBackup(string absolutePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Indexed sector commit backup source is missing.";
                return false;
            }

            string backupPath = ResolveIndexedSaveBackupPath(absolutePath);
            if (string.IsNullOrEmpty(backupPath))
            {
                error = "Indexed sector commit backup path is invalid.";
                return false;
            }

            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long sourceBytes, out string sourceLengthError))
            {
                error = string.IsNullOrEmpty(sourceLengthError)
                    ? "Indexed sector commit backup source length could not be resolved."
                    : sourceLengthError;
                return false;
            }

            string backupTempPath = backupPath + ".tmp";
            try
            {
                string backupDirectory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDirectory))
                    Directory.CreateDirectory(backupDirectory);

                AsyncWriteManager.InvalidateCachedReadWindows(backupTempPath);
                if (!TryDeleteFileIfExists(backupTempPath, out error))
                    return false;

                File.Copy(absolutePath, backupTempPath, true);
                AsyncWriteManager.InvalidateCachedReadWindows(backupTempPath);
                if (!AsyncWriteManager.TryGetFileLength(backupTempPath, out long backupTempBytes, out string tempLengthError))
                {
                    error = string.IsNullOrEmpty(tempLengthError)
                        ? "Indexed sector commit backup temp file length could not be resolved."
                        : tempLengthError;
                    _ = TryDeleteFileIfExists(backupTempPath, out _);
                    return false;
                }

                if (backupTempBytes != sourceBytes)
                {
                    error = "Indexed sector commit backup temp file length did not match source.";
                    _ = TryDeleteFileIfExists(backupTempPath, out _);
                    return false;
                }

                if (!AsyncWriteManager.FlushCriticalSavePath(backupTempPath, backupTempBytes, out string tempFlushError))
                {
                    error = string.IsNullOrEmpty(tempFlushError)
                        ? "Indexed sector commit backup temp critical flush failed."
                        : tempFlushError;
                    _ = TryDeleteFileIfExists(backupTempPath, out _);
                    return false;
                }

                AsyncWriteManager.InvalidateCachedReadWindows(backupPath);
                if (File.Exists(backupPath))
                {
                    File.Replace(backupTempPath, backupPath, null);
                }
                else
                {
                    File.Move(backupTempPath, backupPath);
                }
                AsyncWriteManager.InvalidateCachedReadWindows(backupTempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(backupPath);

                if (!AsyncWriteManager.TryGetFileLength(backupPath, out long backupBytes, out string lengthError))
                {
                    error = string.IsNullOrEmpty(lengthError)
                        ? "Indexed sector commit backup file length could not be resolved."
                        : lengthError;
                    return false;
                }

                if (backupBytes != sourceBytes)
                {
                    error = "Indexed sector commit backup file length did not match source.";
                    return false;
                }

                if (!AsyncWriteManager.FlushCriticalSavePath(backupPath, backupBytes, out string flushError))
                {
                    error = string.IsNullOrEmpty(flushError)
                        ? "Indexed sector commit backup critical flush failed."
                        : flushError;
                    return false;
                }

                return true;
            }
            catch (IOException ex)
            {
                error = $"Indexed sector commit backup failed for '{absolutePath}': {ex.Message}";
                _ = TryDeleteFileIfExists(backupTempPath, out _);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = $"Indexed sector commit backup unauthorized for '{absolutePath}': {ex.Message}";
                _ = TryDeleteFileIfExists(backupTempPath, out _);
                return false;
            }
            catch (System.Security.SecurityException ex)
            {
                error = $"Indexed sector commit backup security failed for '{absolutePath}': {ex.Message}";
                _ = TryDeleteFileIfExists(backupTempPath, out _);
                return false;
            }
            catch (ArgumentException ex)
            {
                error = $"Indexed sector commit backup path invalid for '{absolutePath}': {ex.Message}";
                _ = TryDeleteFileIfExists(backupTempPath, out _);
                return false;
            }
            catch (NotSupportedException ex)
            {
                error = $"Indexed sector commit backup path unsupported for '{absolutePath}': {ex.Message}";
                _ = TryDeleteFileIfExists(backupTempPath, out _);
                return false;
            }
        }

        internal static bool IsIndexedBlockStorageRecoveryError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;

            return error.IndexOf("Save header checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Header checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save file magic mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save magic mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save file is smaller than", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save file is truncated inside the fixed header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Unsupported save header version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save header is not an indexed sector container", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed sector block decompression requires save header version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed sector directory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed metadata block", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed LZ4 block decompression failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed block length mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed sector checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed aggregate payload checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static ulong Hash64(void* ptr, long length)
        {
            if (ptr == null || length <= 0L)
                return 0UL;

            if (length <= int.MaxValue)
                return MemorySentinelMath.ComputeXXHash3Full64(ptr, (int)length);

            return Hash64ChunkedDeterministic(ptr, length);
        }

        private static ulong Hash64ChunkedDeterministic(void* ptr, long length)
        {
            byte* cursor = (byte*)ptr;
            long remaining = length;
            ulong hash = 14695981039346656037UL ^ unchecked((ulong)length);
            while (remaining > 0L)
            {
                int chunkBytes = remaining > 1048576L ? 1048576 : (int)remaining;
                ulong chunkHash = MemorySentinelMath.ComputeXXHash3Full64(cursor, chunkBytes);
                hash = MixHash64(hash ^ chunkHash ^ unchecked((ulong)(uint)chunkBytes));
                cursor += chunkBytes;
                remaining -= chunkBytes;
            }

            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixHash64(ulong hash)
        {
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;
            hash ^= hash >> 33;
            return hash;
        }

        private static byte* AddByteOffset(void* source, int byteOffset)
        {
            // Byte-wise pointer math only. Never advance typed pointers by raw byte offsets.
            return (byte*)source + byteOffset;
        }

        private static bool IsByteRangeWithin(int byteOffset, int byteLength, int totalByteLength)
        {
            return byteOffset >= 0 &&
                   byteLength >= 0 &&
                   totalByteLength >= 0 &&
                   byteOffset <= totalByteLength &&
                   byteLength <= totalByteLength - byteOffset;
        }

        private static long ComputePersistentWorldSectorHash(in PersistentWorldDeltaRecord record, int chunkSizeMeters)
        {
            AbsoluteUniversePosition position = record.UnpackPosition(chunkSizeMeters);
            return ComputePersistentWorldPagedSectorHash(in position);
        }

        private static bool TryValidatePersistentWorldSectorRecords(
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly sectorRecords,
            long sectorHash,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;
            if (sectorHash == long.MinValue)
            {
                error = "Sector override has an invalid sector hash.";
                return false;
            }

            int recordCount = sectorRecords.IsCreated ? sectorRecords.Length : 0;
            if (chunkSizeMeters <= 0)
            {
                error = "Sector override chunk size is invalid.";
                return false;
            }

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord record = sectorRecords[i];
                if (!PersistentWorldDeltaRecord.IsValid(in record))
                {
                    error = "Sector override contains an invalid persistent-world delta record.";
                    return false;
                }

                long recordSectorHash = ComputePersistentWorldSectorHash(in record, chunkSizeMeters);
                if (recordSectorHash == long.MinValue || recordSectorHash != sectorHash)
                {
                    error = "Sector override contains a cross-sector persistent-world delta record.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidatePersistentWorldSectorRecords(
            PersistentWorldDeltaRecord[] sectorRecords,
            long sectorHash,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;
            if (sectorHash == long.MinValue)
            {
                error = "Indexed persistent-world sector has an invalid sector hash.";
                return false;
            }

            int recordCount = sectorRecords != null ? sectorRecords.Length : 0;
            if (chunkSizeMeters <= 0)
            {
                error = "Indexed persistent-world sector chunk size is invalid.";
                return false;
            }

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord record = sectorRecords[i];
                if (!PersistentWorldDeltaRecord.IsValid(in record))
                {
                    error = "Indexed persistent-world sector contains an invalid delta record.";
                    return false;
                }

                long recordSectorHash = ComputePersistentWorldSectorHash(in record, chunkSizeMeters);
                if (recordSectorHash == long.MinValue || recordSectorHash != sectorHash)
                {
                    error = "Indexed persistent-world sector contains a cross-sector delta record.";
                    return false;
                }
            }

            return true;
        }

        private static long PackSectorHash(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static IndexedSectorGroupBuffer BuildIndexedSectorGroups(NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas, int chunkSizeMeters)
        {
            int sourceLength = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int capacity = math.max(4, sourceLength);
            IndexedSectorGroupBuffer buffer = default;
            try
            {
                buffer.Groups = CreateRegisteredTransientNativeList<IndexedSectorGroup>(
                    math.min(IndexedSectorDirectorySlotCount, capacity),
                    IndexedSectorGroupListScratchLabel,
                    out int groupsSentinelId);
                buffer.GroupsSentinelId = groupsSentinelId;
                buffer.Records = CreateRegisteredTransientNativeList<PersistentWorldDeltaRecord>(
                    capacity,
                    IndexedSectorGroupRecordListScratchLabel,
                    out int recordsSentinelId);
                buffer.RecordsSentinelId = recordsSentinelId;
            }
            catch
            {
                buffer.Dispose();
                throw;
            }

            if (sourceLength <= 0)
                return buffer;

            if (chunkSizeMeters <= 0)
            {
                buffer.InvalidRecordCount = sourceLength;
                return buffer;
            }

            RegisteredTransientNativeArray<short> slotToGroupIndexOwner = CreateRegisteredTransientNativeArray<short>(
                IndexedSectorDirectorySlotCount,
                NativeArrayOptions.UninitializedMemory,
                IndexedSectorGroupSlotScratchLabel);
            RegisteredTransientNativeArray<short> recordGroupIndexOwner = CreateRegisteredTransientNativeArray<short>(
                sourceLength,
                NativeArrayOptions.UninitializedMemory,
                IndexedSectorRecordGroupScratchLabel);
            NativeArray<short> slotToGroupIndex = slotToGroupIndexOwner.Array;
            NativeArray<short> recordGroupIndex = recordGroupIndexOwner.Array;

            try
            {
                for (int i = 0; i < slotToGroupIndex.Length; i++)
                    slotToGroupIndex[i] = -1;

                for (int i = 0; i < sourceLength; i++)
                {
                    PersistentWorldDeltaRecord record = persistentWorldDeltas[i];
                    if (!PersistentWorldDeltaRecord.IsValid(in record))
                    {
                        recordGroupIndex[i] = -1;
                        continue;
                    }

                    long sectorHash = ComputePersistentWorldSectorHash(in record, chunkSizeMeters);
                    if (sectorHash == long.MinValue)
                    {
                        recordGroupIndex[i] = -1;
                        buffer.InvalidRecordCount++;
                        continue;
                    }

                    int groupIndex = FindOrCreateIndexedSectorGroup(sectorHash, slotToGroupIndex, buffer.Groups);
                    if (groupIndex < 0)
                    {
                        recordGroupIndex[i] = -1;
                        ReportIndexedSectorDirectoryCapacityExceeded(sectorHash, IndexedSectorDirectorySlotCount + 1);
                        continue;
                    }

                    recordGroupIndex[i] = (short)groupIndex;
                    IndexedSectorGroup group = buffer.Groups[groupIndex];
                    group.Count++;
                    buffer.Groups[groupIndex] = group;
                }

                int recordOffset = 0;
                for (int i = 0; i < buffer.Groups.Length; i++)
                {
                    IndexedSectorGroup group = buffer.Groups[i];
                    group.StartIndex = recordOffset;
                    group.WriteCount = 0;
                    buffer.Groups[i] = group;
                    recordOffset += group.Count;
                }

                buffer.Records.ResizeUninitialized(recordOffset);
                for (int i = 0; i < sourceLength; i++)
                {
                    int groupIndex = recordGroupIndex[i];
                    if (groupIndex < 0)
                        continue;

                    PersistentWorldDeltaRecord record = persistentWorldDeltas[i];
                    IndexedSectorGroup group = buffer.Groups[groupIndex];
                    int writeIndex = group.StartIndex + group.WriteCount;
                    buffer.Records[writeIndex] = record;
                    group.WriteCount++;
                    buffer.Groups[groupIndex] = group;
                }
            }
            finally
            {
                recordGroupIndexOwner.Dispose();
                slotToGroupIndexOwner.Dispose();
            }

            return buffer;
        }

        private static int FindOrCreateIndexedSectorGroup(
            long sectorHash,
            NativeArray<short> slotToGroupIndex,
            NativeList<IndexedSectorGroup> groups)
        {
            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                int groupIndex = slotToGroupIndex[slot];
                if (groupIndex < 0)
                {
                    groupIndex = groups.Length;
                    if (groupIndex > short.MaxValue)
                        return -1;

                    groups.Add(new IndexedSectorGroup
                    {
                        SectorHash = sectorHash,
                        DirectorySlot = slot
                    });
                    slotToGroupIndex[slot] = (short)groupIndex;
                    return groupIndex;
                }

                if (groups[groupIndex].SectorHash == sectorHash)
                    return groupIndex;
            }

            return -1;
        }

        internal static int Lz4BlockCompress(byte* source, int sourceLength, byte* destination, int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 8)
                return 0;

            return Lz4BlockCompressStandard(source, sourceLength, destination, destinationCapacity);
        }

        private static int Lz4BlockCompressStandard(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 8)
                return 0;

            int blockCount = (sourceLength + BlockSizeBytes - 1) / BlockSizeBytes;
            int sourceOffset = 0;
            int destinationOffset = 0;

            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int rawBlockLength = math.min(BlockSizeBytes, sourceLength - sourceOffset);
                if (destinationOffset + StandardCompressedBlockHeaderBytes > destinationCapacity)
                    return 0;

                byte* rawBlockSource = source + sourceOffset;
                byte* blockDestination = destination + destinationOffset + StandardCompressedBlockHeaderBytes;
                int blockDestinationCapacity = destinationCapacity - destinationOffset - StandardCompressedBlockHeaderBytes;
                if (!TryCompressBlockNativeOnly(
                        rawBlockSource,
                        rawBlockLength,
                        blockDestination,
                        blockDestinationCapacity,
                        out int blockCompressedLength))
                {
                    return 0;
                }

                int encodedBlockCompressedLength = EncodeCompressedBlockLength(blockCompressedLength);
                if (encodedBlockCompressedLength == 0)
                    return 0;

                UnsafeUtility.WriteArrayElement(destination + destinationOffset, 0, encodedBlockCompressedLength);
                UnsafeUtility.WriteArrayElement(destination + destinationOffset + 4, 0, rawBlockLength);

                sourceOffset += rawBlockLength;
                destinationOffset += StandardCompressedBlockHeaderBytes + blockCompressedLength;
            }

            return destinationOffset;
        }

        private static int Lz4BlockCompressStandardManagedFallback(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 8)
                return 0;

            int blockCount = (sourceLength + BlockSizeBytes - 1) / BlockSizeBytes;
            int sourceOffset = 0;
            int destinationOffset = 0;

            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int rawBlockLength = math.min(BlockSizeBytes, sourceLength - sourceOffset);
                if (destinationOffset + StandardCompressedBlockHeaderBytes > destinationCapacity)
                    return 0;

                byte* rawBlockSource = source + sourceOffset;
                byte* blockDestination = destination + destinationOffset + StandardCompressedBlockHeaderBytes;
                int blockDestinationCapacity = destinationCapacity - destinationOffset - StandardCompressedBlockHeaderBytes;
                if (!TryCompressBlock(
                        rawBlockSource,
                        rawBlockLength,
                        blockDestination,
                        blockDestinationCapacity,
                        out int blockCompressedLength))
                {
                    return 0;
                }

                int encodedBlockCompressedLength = EncodeCompressedBlockLength(blockCompressedLength);
                if (encodedBlockCompressedLength == 0)
                    return 0;

                UnsafeUtility.WriteArrayElement(destination + destinationOffset, 0, encodedBlockCompressedLength);
                UnsafeUtility.WriteArrayElement(destination + destinationOffset + 4, 0, rawBlockLength);

                sourceOffset += rawBlockLength;
                destinationOffset += StandardCompressedBlockHeaderBytes + blockCompressedLength;
            }

            return destinationOffset;
        }

        private static int ResolveProtectedLz4BlockCount(int rawByteLength)
        {
            return rawByteLength <= 0
                ? 0
                : (rawByteLength + ProtectedLz4BlockSizeMask) >> ProtectedLz4BlockSizeShift;
        }

        private static int Lz4ProtectedBlockCompress(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= ProtectedCompressedBlockHeaderBytes)
                return 0;

            int blockCount = ResolveProtectedLz4BlockCount(sourceLength);
            int sourceOffset = 0;
            int destinationOffset = 0;

            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int remainingRawBytes = sourceLength - sourceOffset;
                int rawBlockLength = remainingRawBytes > ModPayloadSubBlockSizeBytes
                    ? ModPayloadSubBlockSizeBytes
                    : remainingRawBytes;
                if (destinationOffset + ProtectedCompressedBlockHeaderBytes > destinationCapacity)
                    return 0;

                byte* rawBlockSource = source + sourceOffset;
                byte* blockDestination = destination + destinationOffset + ProtectedCompressedBlockHeaderBytes;
                int blockDestinationCapacity = destinationCapacity - destinationOffset - ProtectedCompressedBlockHeaderBytes;
                if (!TryCompressBlock(
                        rawBlockSource,
                        rawBlockLength,
                        blockDestination,
                        blockDestinationCapacity,
                        out int blockCompressedLength))
                {
                    return 0;
                }

                int encodedBlockCompressedLength = EncodeCompressedBlockLength(blockCompressedLength);
                if (encodedBlockCompressedLength == 0)
                    return 0;

                ProtectedCompressedBlockHeader blockHeader = new ProtectedCompressedBlockHeader
                {
                    CompressedLength = encodedBlockCompressedLength,
                    RawLength = rawBlockLength,
                    RawChecksumLow32 = Hash32(rawBlockSource, rawBlockLength),
                    Magic = ProtectedCompressedBlockMagic
                };
                UnsafeUtility.CopyStructureToPtr(ref blockHeader, destination + destinationOffset);

                sourceOffset += rawBlockLength;
                destinationOffset += ProtectedCompressedBlockHeaderBytes + blockCompressedLength;
            }

            return destinationOffset;
        }

        private static int Lz4ProtectedBlockDecompress(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity)
        {
            if (source == null ||
                destination == null ||
                compressedLength <= 0 ||
                compressedLength > MaxCompressedPayloadBytes ||
                destinationCapacity <= 0 ||
                destinationCapacity > RawPayloadCapacityBytes)
            {
                return 0;
            }

            int sourceOffset = 0;
            int destinationOffset = 0;
            while (sourceOffset < compressedLength)
            {
                if (sourceOffset + ProtectedCompressedBlockHeaderBytes > compressedLength)
                    return 0;

                ProtectedCompressedBlockHeader blockHeader = UnsafeUtility.ReadArrayElement<ProtectedCompressedBlockHeader>(source + sourceOffset, 0);
                int blockCompressedLength = DecodeCompressedBlockLength(blockHeader.CompressedLength);
                bool isManagedDeflateBlock = IsManagedDeflateBlock(blockHeader.CompressedLength);
                if (blockHeader.Magic != ProtectedCompressedBlockMagic ||
                    blockCompressedLength <= 0 ||
                    blockHeader.RawLength <= 0 ||
                    blockHeader.RawLength > ModPayloadSubBlockSizeBytes)
                {
                    return 0;
                }

                sourceOffset += ProtectedCompressedBlockHeaderBytes;
                if (sourceOffset > compressedLength - blockCompressedLength ||
                    destinationOffset > destinationCapacity - blockHeader.RawLength)
                {
                    return 0;
                }

                if (!TryDecompressBlock(
                        source + sourceOffset,
                        blockCompressedLength,
                        destination + destinationOffset,
                        blockHeader.RawLength,
                        isManagedDeflateBlock,
                        out _))
                {
                    return 0;
                }

                uint computedChecksum = Hash32(destination + destinationOffset, blockHeader.RawLength);
                if (computedChecksum != blockHeader.RawChecksumLow32)
                    return 0;

                sourceOffset += blockCompressedLength;
                destinationOffset += blockHeader.RawLength;
            }

            return destinationOffset;
        }

        private static bool TryReadPayload(
            string absolutePath,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out SaveFileHeader header,
            out PayloadPrefixInfo prefix,
            out byte* rawPtr,
            out int rawPayloadLength,
            out string error)
        {
            header = default;
            prefix = default;
            rawPtr = null;
            rawPayloadLength = 0;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Save file is missing.";
                return false;
            }

            if (!rawBuffer.IsCreated)
            {
                error = "Native raw save buffer is not initialized.";
                return false;
            }

            if (!compressedBuffer.IsCreated)
            {
                error = "Native compressed save buffer is not initialized.";
                return false;
            }

            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength < LegacyHeaderSize)
            {
                error = "Save file is smaller than the fixed header.";
                return false;
            }

            if (fileLength > int.MaxValue)
            {
                error = "Save file exceeds the supported native staging range.";
                return false;
            }

            if (fileLength > CurrentHeaderSize + MaxCompressedPayloadBytes)
            {
                error = "Save file exceeds the maximum supported compressed payload size.";
                return false;
            }

            rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            byte* compressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);

            if (!TryReadHeaderPrefixFastFail(absolutePath, out SaveFileHeaderPrefix headerPrefix, out error))
                return false;

            int headerSizeBytes = ResolveHeaderSize(headerPrefix.Version);
            if (headerSizeBytes <= 0)
            {
                error = $"Unsupported save header version {headerPrefix.Version}.";
                return false;
            }

            if (fileLength < headerSizeBytes)
            {
                error = "Save file is truncated inside the fixed header.";
                return false;
            }

            byte* headerBytes = stackalloc byte[CurrentHeaderSize];
            UnsafeUtility.MemClear(headerBytes, CurrentHeaderSize);
            if (!AsyncWriteManager.TryCopyFromCachedReadWindow(absolutePath, 0L, headerBytes, headerSizeBytes, out error))
                return false;

            bool isCurrentHeader = headerPrefix.Version >= First64BitHashVersion;
            if (isCurrentHeader)
            {
                header = ReadVersionedSaveFileHeader(headerBytes, headerPrefix.Version);
                ulong computedHeaderHash = ComputeHeaderHash(ref header);
                if (computedHeaderHash != header.HashHeader64)
                {
                    error = "Header checksum mismatch.";
                    return false;
                }
            }
            else
            {
                LegacySaveFileHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacySaveFileHeader>(headerBytes, 0);
                uint computedHeaderHash = Hash32(headerBytes, LegacyHeaderHashSizeBytes);
                if (computedHeaderHash != legacyHeader.HashHeader32)
                {
                    error = "Header checksum mismatch.";
                    return false;
                }

                header = ConvertLegacyHeader(in legacyHeader);
            }

            if (!TryValidateHeader(header, out error))
                return false;

            long compressedPayloadLengthLong = fileLength - headerSizeBytes;
            if (compressedPayloadLengthLong <= 0L)
            {
                error = "Save payload is missing.";
                return false;
            }

            if (compressedPayloadLengthLong > MaxCompressedPayloadBytes || compressedPayloadLengthLong > compressedBuffer.Length)
            {
                error = "Compressed save payload exceeds the supported decoder budget.";
                return false;
            }

            int compressedPayloadLength = (int)compressedPayloadLengthLong;
            if (!AsyncWriteManager.TryCopyFileRangeToNativeArray(absolutePath, headerSizeBytes, compressedBuffer, compressedPayloadLength, out error))
                return false;

            rawPayloadLength = Lz4BlockDecompress(
                compressedPtr,
                compressedPayloadLength,
                rawPtr,
                rawBuffer.Length,
                out _);
            if (rawPayloadLength <= 0)
            {
                error = "LZ4 block decompression failed.";
                return false;
            }

            if ((header.Flags & FlagTokenSubstitution) != 0)
            {
                if (!TryExpandTokenizedPayloadInPlace(rawPtr, rawPayloadLength, rawBuffer.Length, out rawPayloadLength, out error))
                    return false;
            }

            if (rawPayloadLength > RawPayloadCapacityBytes || rawPayloadLength > rawBuffer.Length)
            {
                error = "Decompressed save payload exceeded the decoder budget.";
                return false;
            }

            if (isCurrentHeader)
            {
                ulong computedPayloadHash = Hash64(rawPtr, rawPayloadLength);
                if (computedPayloadHash != header.HashPayload64)
                {
                    error = "Payload checksum mismatch.";
                    return false;
                }
            }
            else
            {
                uint computedPayloadHash = Hash32(rawPtr, rawPayloadLength);
                if (computedPayloadHash != (uint)header.HashPayload64)
                {
                    error = "Payload checksum mismatch.";
                    return false;
                }
            }

            if (header.Version < SaveDataMigration_AupV8.AupV8Version)
            {
                if (!SaveDataMigration_AupV8.TryMigratePayloadToV8(
                        rawPtr,
                        rawPayloadLength,
                        rawBuffer.Length,
                        out prefix,
                        out rawPayloadLength,
                        out int payloadByteShift,
                        out error))
                {
                    return false;
                }

                if (!TryShiftPayloadOffset(header.DeltaOffset, payloadByteShift, out uint shiftedDeltaOffset, out error) ||
                    !TryShiftPayloadOffset(header.EntityOffset, payloadByteShift, out uint shiftedEntityOffset, out error))
                {
                    return false;
                }

                header.DeltaOffset = shiftedDeltaOffset;
                header.EntityOffset = shiftedEntityOffset;
                header.HashPayload64 = Hash64(rawPtr, rawPayloadLength);
            }
            else if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, rawPayloadLength, header.Version, out prefix, out error))
            {
                return false;
            }

            int metadataBytes = prefix.PrefixSizeBytes + prefix.SceneNameByteLength + prefix.GameVersionByteLength;
            if (metadataBytes > rawPayloadLength)
            {
                error = "Payload prefix string lengths exceed the decompressed payload length.";
                return false;
            }

            if (!TryDecodeSaveDataByteLength(in prefix, out int saveDataLength, out error))
                return false;

            long playerPayloadLengthLong = (long)metadataBytes + saveDataLength;
            if (playerPayloadLengthLong > int.MaxValue)
            {
                error = "Serialized save data exceeds the supported decoder range.";
                return false;
            }

            int playerPayloadLength = (int)playerPayloadLengthLong;
            if (playerPayloadLength > rawPayloadLength)
            {
                error = "Serialized save data exceeds the decompressed payload length.";
                return false;
            }

            if (!TryResolvePayloadOffsets(in header, out _, out int deltaSectionOffset, out int entitySectionOffset, out error))
                return false;
            if (deltaSectionOffset < playerPayloadLength || deltaSectionOffset > rawPayloadLength)
            {
                error = "Packed quest-state offset exceeds the decompressed payload bounds.";
                return false;
            }

            if (entitySectionOffset < deltaSectionOffset || entitySectionOffset > rawPayloadLength)
            {
                error = "Entity payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            return true;
        }

        private static bool TryReadPackedQuestStateWords(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            int playerPayloadLength,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out string error)
        {
            packedQuestHeader = default;
            packedQuestStateWords = null;
            error = string.Empty;

            if (!TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error))
                return false;
            if (!TryResolvePayloadOffsets(in header, out _, out int packedQuestSectionOffset, out int entitySectionOffset, out error))
                return false;
            if (packedQuestWordCount <= 0 || packedQuestSectionOffset == entitySectionOffset)
            {
                if (packedQuestSectionOffset < playerPayloadLength || packedQuestSectionOffset > rawPayloadLength)
                {
                    error = "Packed quest-state section offset is invalid.";
                    return false;
                }

                return true;
            }

            if (packedQuestSectionOffset < playerPayloadLength || packedQuestSectionOffset >= entitySectionOffset)
            {
                error = "Packed quest-state section overlaps the serialized player payload.";
                return false;
            }

            int sectionLength = entitySectionOffset - packedQuestSectionOffset;
            if (sectionLength < PackedQuestStateSectionHeaderSize)
            {
                error = "Packed quest-state section is truncated.";
                return false;
            }

            packedQuestHeader = UnsafeUtility.ReadArrayElement<QuestSaveHeader>(AddByteOffset(rawPtr, packedQuestSectionOffset), 0);
            if (!TryValidatePackedQuestSectionHeader(in packedQuestHeader, packedQuestWordCount, out error))
                return false;

            int expectedSectionLength = PackedQuestStateSectionHeaderSize + (packedQuestWordCount * UnsafeUtility.SizeOf<uint>());
            if (sectionLength != expectedSectionLength)
            {
                error = "Packed quest-state section length mismatch.";
                return false;
            }

            packedQuestStateWords = new uint[packedQuestWordCount];
            if (packedQuestWordCount <= 0)
                return true;

            fixed (uint* destinationPtr = packedQuestStateWords)
            {
                byte* packedQuestSourcePtr = AddByteOffset(rawPtr, packedQuestSectionOffset + PackedQuestStateSectionHeaderSize);
                int packedQuestBytes = packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>(), packedQuestSourcePtr, packedQuestBytes))
                {
                    error = "Packed quest-state section copy exceeded destination bounds.";
                    return false;
                }
            }

            uint computedChecksum = ComputePackedQuestStateChecksum(packedQuestStateWords);
            if (computedChecksum != packedQuestHeader.Checksum)
            {
                error = "Packed quest-state checksum mismatch.";
                return false;
            }

            return true;
        }

        private static bool TryReadPersistentWorldDeltas(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out string error)
        {
            persistentWorldDeltas = null;
            error = string.Empty;

            if (!TryConvertSectionCount(header.EntityCount, out int entityCount))
            {
                error = "Entity count exceeds supported bounds.";
                return false;
            }

            if (entityCount <= 0)
                return true;

            if (header.Version >= CompactPersistentWorldSectionVersion)
                return TryReadPersistentWorldDeltasV5(rawPtr, rawPayloadLength, header, out persistentWorldDeltas, out error);

            int entityRecordSize = UnsafeUtility.SizeOf<PersistentWorldDeltaRecordLegacy64>();
            if (!TryResolvePayloadOffsets(in header, out _, out _, out int entitySectionOffset, out error))
                return false;

            long entityBytesLong = (long)entityCount * entityRecordSize;
            if (entityBytesLong > int.MaxValue)
            {
                error = "Entity payload length exceeds the supported range.";
                return false;
            }

            int entityBytes = (int)entityBytesLong;
            if (entitySectionOffset < 0 || entitySectionOffset + entityBytes > rawPayloadLength)
            {
                error = "Entity payload exceeds the decompressed payload bounds.";
                return false;
            }

            persistentWorldDeltas = new PersistentWorldDeltaRecord[entityCount];
            if (entityBytes == 0)
                return true;

            byte* entitySourcePtr = AddByteOffset(rawPtr, entitySectionOffset);
            for (int i = 0; i < entityCount; i++)
            {
                PersistentWorldDeltaRecordLegacy64 legacyRecord =
                    UnsafeUtility.ReadArrayElement<PersistentWorldDeltaRecordLegacy64>(entitySourcePtr, i);
                persistentWorldDeltas[i] = legacyRecord.ToRuntimeRecord();
            }

            return true;
        }

        private static bool TryResolveVoxelDeltaSnapshotByteRange(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out int voxelSectionOffset,
            out int voxelByteLength,
            out string error)
        {
            voxelSectionOffset = 0;
            voxelByteLength = 0;
            error = string.Empty;

            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out int entitySectionLength, out error))
                return false;

            voxelSectionOffset = entitySectionOffset + entitySectionLength;
            if (header.Version >= EcosystemSectionVersion)
            {
                if (!TryResolveEcosystemSectionLength(
                        rawPtr,
                        rawPayloadLength,
                        header,
                        entitySectionOffset,
                        entitySectionLength,
                        out int ecosystemSectionOffset,
                        out int ecosystemSectionLength,
                        out error))
                {
                    return false;
                }

                voxelSectionOffset = ecosystemSectionOffset + ecosystemSectionLength;
            }

            if (voxelSectionOffset < 0 || voxelSectionOffset > rawPayloadLength)
            {
                error = "Voxel delta payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            voxelByteLength = rawPayloadLength - voxelSectionOffset;
            return true;
        }

        private static bool TryReadVoxelDeltaSnapshot(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            NativeArray<byte> voxelDeltaSnapshotDestination,
            out int voxelDeltaSnapshotBytes,
            out string error)
        {
            voxelDeltaSnapshotBytes = 0;
            if (!TryResolveVoxelDeltaSnapshotByteRange(rawPtr, rawPayloadLength, header, out int voxelSectionOffset, out int voxelByteLength, out error))
                return false;

            if (voxelByteLength <= 0)
                return true;

            if (!voxelDeltaSnapshotDestination.IsCreated || voxelDeltaSnapshotDestination.Length < voxelByteLength)
            {
                error = "Voxel delta snapshot destination is too small.";
                return false;
            }

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshotDestination);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, voxelDeltaSnapshotDestination.Length, AddByteOffset(rawPtr, voxelSectionOffset), voxelByteLength))
            {
                error = "Voxel delta payload copy exceeded destination bounds.";
                return false;
            }

            voxelDeltaSnapshotBytes = voxelByteLength;
            return true;
        }

        private static ulong ComputeHeaderHash(ref SaveFileHeader header)
        {
            if (header.Version >= First64BitHashVersion && header.Version < HeaderChecksumVersion)
            {
                IndexedSaveFileHeaderV8 legacyHeader = ConvertToIndexedHeaderV8(in header);
                legacyHeader.HashHeader64Lo = 0u;
                legacyHeader.HashHeader64Hi = 0u;
                return Hash64(UnsafeUtility.AddressOf(ref legacyHeader), IndexedHeaderV8HashSizeBytes);
            }

            SaveFileHeader copy = header;
            copy.HashHeader64 = 0UL;
            return Hash64(UnsafeUtility.AddressOf(ref copy), CurrentHeaderHashSizeBytes);
        }

        private static bool TryBuildPersistentWorldSectionTables(
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            out NativeParallelHashMap<int3, ushort> chunkLookup,
            out NativeList<int3> chunkTable,
            out NativeParallelHashMap<ulong, ushort> itemHashLookup,
            out NativeList<ulong> itemHashTable,
            out PersistentWorldSectionTableSentinelIds sentinelIds,
            out string error)
        {
            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int capacity = math.max(recordCount, 1);
            chunkLookup = default;
            chunkTable = default;
            itemHashLookup = default;
            itemHashTable = default;
            sentinelIds = default;
            error = string.Empty;
            try
            {
                chunkLookup = CreateRegisteredTransientNativeParallelHashMap<int3, ushort>(
                    capacity,
                    PersistentWorldSectionChunkLookupScratchLabel,
                    out int chunkLookupSentinelId);
                sentinelIds.ChunkLookup = chunkLookupSentinelId;
                chunkTable = CreateRegisteredTransientNativeList<int3>(
                    capacity,
                    PersistentWorldSectionChunkTableScratchLabel,
                    out int chunkTableSentinelId);
                sentinelIds.ChunkTable = chunkTableSentinelId;
                itemHashLookup = CreateRegisteredTransientNativeParallelHashMap<ulong, ushort>(
                    capacity,
                    PersistentWorldSectionItemHashLookupScratchLabel,
                    out int itemHashLookupSentinelId);
                sentinelIds.ItemHashLookup = itemHashLookupSentinelId;
                itemHashTable = CreateRegisteredTransientNativeList<ulong>(
                    capacity,
                    PersistentWorldSectionItemHashTableScratchLabel,
                    out int itemHashTableSentinelId);
                sentinelIds.ItemHashTable = itemHashTableSentinelId;
            }
            catch
            {
                DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                throw;
            }

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord))
                {
                    error = "Persistent-world delta table contains an invalid record.";
                    DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                    return false;
                }

                if (!chunkLookup.ContainsKey(deltaRecord.ChunkId))
                {
                    if (chunkTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta chunk table exceeded 65535 unique chunks.";
                        DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                        return false;
                    }

                    ushort chunkIndex = (ushort)chunkTable.Length;
                    chunkLookup.Add(deltaRecord.ChunkId, chunkIndex);
                    chunkTable.Add(deltaRecord.ChunkId);
                }

                if (PersistentWorldDeltaRecord.IsDeleted(in deltaRecord))
                    continue;

                if (!itemHashLookup.ContainsKey(deltaRecord.ItemPersistentIdHash))
                {
                    if (itemHashTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta item table exceeded 65535 unique item hashes.";
                        DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                        return false;
                    }

                    ushort itemIndex = (ushort)itemHashTable.Length;
                    itemHashLookup.Add(deltaRecord.ItemPersistentIdHash, itemIndex);
                    itemHashTable.Add(deltaRecord.ItemPersistentIdHash);
                }
            }

            return true;
        }

        private static bool TryBuildPersistentWorldSectionTables(
            NativeList<PersistentWorldDeltaRecord> persistentWorldDeltas,
            int startIndex,
            int recordCount,
            out NativeParallelHashMap<int3, ushort> chunkLookup,
            out NativeList<int3> chunkTable,
            out NativeParallelHashMap<ulong, ushort> itemHashLookup,
            out NativeList<ulong> itemHashTable,
            out PersistentWorldSectionTableSentinelIds sentinelIds,
            out string error)
        {
            chunkLookup = default;
            chunkTable = default;
            itemHashLookup = default;
            itemHashTable = default;
            sentinelIds = default;
            error = string.Empty;

            if (!persistentWorldDeltas.IsCreated ||
                startIndex < 0 ||
                recordCount < 0 ||
                startIndex > persistentWorldDeltas.Length - recordCount)
            {
                error = "Persistent-world delta record range is invalid.";
                return false;
            }

            int capacity = math.max(recordCount, 1);
            try
            {
                chunkLookup = CreateRegisteredTransientNativeParallelHashMap<int3, ushort>(
                    capacity,
                    PersistentWorldSectionChunkLookupScratchLabel,
                    out int chunkLookupSentinelId);
                sentinelIds.ChunkLookup = chunkLookupSentinelId;
                chunkTable = CreateRegisteredTransientNativeList<int3>(
                    capacity,
                    PersistentWorldSectionChunkTableScratchLabel,
                    out int chunkTableSentinelId);
                sentinelIds.ChunkTable = chunkTableSentinelId;
                itemHashLookup = CreateRegisteredTransientNativeParallelHashMap<ulong, ushort>(
                    capacity,
                    PersistentWorldSectionItemHashLookupScratchLabel,
                    out int itemHashLookupSentinelId);
                sentinelIds.ItemHashLookup = itemHashLookupSentinelId;
                itemHashTable = CreateRegisteredTransientNativeList<ulong>(
                    capacity,
                    PersistentWorldSectionItemHashTableScratchLabel,
                    out int itemHashTableSentinelId);
                sentinelIds.ItemHashTable = itemHashTableSentinelId;
            }
            catch
            {
                DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                throw;
            }

            int endIndex = startIndex + recordCount;
            for (int i = startIndex; i < endIndex; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord))
                {
                    error = "Persistent-world delta table contains an invalid record.";
                    DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                    return false;
                }

                if (!chunkLookup.ContainsKey(deltaRecord.ChunkId))
                {
                    if (chunkTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta chunk table exceeded 65535 unique chunks.";
                        DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                        return false;
                    }

                    ushort chunkIndex = (ushort)chunkTable.Length;
                    chunkLookup.Add(deltaRecord.ChunkId, chunkIndex);
                    chunkTable.Add(deltaRecord.ChunkId);
                }

                if (PersistentWorldDeltaRecord.IsDeleted(in deltaRecord))
                    continue;

                if (!itemHashLookup.ContainsKey(deltaRecord.ItemPersistentIdHash))
                {
                    if (itemHashTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta item table exceeded 65535 unique item hashes.";
                        DisposePersistentWorldSectionTables(ref chunkLookup, ref chunkTable, ref itemHashLookup, ref itemHashTable, ref sentinelIds);
                        return false;
                    }

                    ushort itemIndex = (ushort)itemHashTable.Length;
                    itemHashLookup.Add(deltaRecord.ItemPersistentIdHash, itemIndex);
                    itemHashTable.Add(deltaRecord.ItemPersistentIdHash);
                }
            }

            return true;
        }

        private static void DisposePersistentWorldSectionTables(
            ref NativeParallelHashMap<int3, ushort> chunkLookup,
            ref NativeList<int3> chunkTable,
            ref NativeParallelHashMap<ulong, ushort> itemHashLookup,
            ref NativeList<ulong> itemHashTable,
            ref PersistentWorldSectionTableSentinelIds sentinelIds)
        {
            DisposeRegisteredTransientNativeList(
                ref itemHashTable,
                ref sentinelIds.ItemHashTable,
                PersistentWorldSectionItemHashTableScratchLabel);
            DisposeRegisteredTransientNativeParallelHashMap(
                ref itemHashLookup,
                ref sentinelIds.ItemHashLookup,
                PersistentWorldSectionItemHashLookupScratchLabel);
            DisposeRegisteredTransientNativeList(
                ref chunkTable,
                ref sentinelIds.ChunkTable,
                PersistentWorldSectionChunkTableScratchLabel);
            DisposeRegisteredTransientNativeParallelHashMap(
                ref chunkLookup,
                ref sentinelIds.ChunkLookup,
                PersistentWorldSectionChunkLookupScratchLabel);
            sentinelIds = default;
        }

        private static bool TryComputePersistentWorldSectionLength(
            int entityCount,
            int chunkCount,
            int itemHashCount,
            ushort saveVersion,
            out int sectionLength)
        {
            sectionLength = 0;
            if (entityCount < 0 || chunkCount < 0 || itemHashCount < 0)
                return false;

            long total =
                ResolvePersistentWorldSectionHeaderSize(saveVersion) +
                ((long)chunkCount * UnsafeUtility.SizeOf<int3>()) +
                ((long)itemHashCount * UnsafeUtility.SizeOf<ulong>()) +
                ((long)entityCount * UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>());

            if (total > int.MaxValue)
                return false;

            sectionLength = (int)total;
            return true;
        }

        private static bool TryComputeEcosystemSectionLength(int recordCount, ushort saveVersion, out int sectionLength)
        {
            sectionLength = 0;
            if (recordCount < 0)
                return false;

            long total =
                ResolveEcosystemSectionHeaderSize(saveVersion) +
                ((long)recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>());

            if (total > int.MaxValue)
                return false;

            sectionLength = (int)total;
            return true;
        }

        private static bool WritePersistentWorldSection(
            byte* destination,
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltas,
            NativeParallelHashMap<int3, ushort> chunkLookup,
            NativeList<int3> chunkTable,
            NativeParallelHashMap<ulong, ushort> itemHashLookup,
            NativeList<ulong> itemHashTable,
            out string error)
        {
            error = string.Empty;
            if (destination == null)
            {
                error = "Persistent-world section destination is null.";
                return false;
            }

            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            if (!TryComputePersistentWorldSectionLength(recordCount, chunkTable.Length, itemHashTable.Length, CurrentVersion, out int sectionLength))
            {
                error = "Persistent-world section exceeds supported bounds.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = new PersistentWorldSectionHeader
            {
                ChunkCount = (uint)chunkTable.Length,
                ItemHashCount = (uint)itemHashTable.Length,
                RecordCount = (uint)recordCount
            };

            int sectionHeaderSize = ResolvePersistentWorldSectionHeaderSize(CurrentVersion);
            WritePersistentWorldSectionHeader(destination, sectionHeaderSize, in sectionHeader);
            int cursor = sectionHeaderSize;

            if (chunkTable.Length > 0)
            {
                void* chunkSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkTable.AsArray());
                int chunkBytes = chunkTable.Length * UnsafeUtility.SizeOf<int3>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, chunkSourcePtr, chunkBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    error = "Persistent-world chunk table write exceeded section bounds.";
                    return false;
                }

                cursor += chunkBytes;
            }

            if (itemHashTable.Length > 0)
            {
                void* itemSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(itemHashTable.AsArray());
                int itemBytes = itemHashTable.Length * UnsafeUtility.SizeOf<ulong>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, itemSourcePtr, itemBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    error = "Persistent-world item hash table write exceeded section bounds.";
                    return false;
                }

                cursor += itemBytes;
            }

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                PersistentWorldSaveRecord16 saveRecord = default;
                bool wroteRecord = false;
                if (PersistentWorldDeltaRecord.IsDeleted(in deltaRecord) &&
                    chunkLookup.TryGetValue(deltaRecord.ChunkId, out ushort deletedChunkIndex))
                {
                    saveRecord = new PersistentWorldSaveRecord16
                    {
                        PackedLocalPosition = deltaRecord.PackedLocalPosition,
                        InstanceUid = deltaRecord.InstanceUid,
                        Quantity = 1,
                        ItemFlags = deltaRecord.ItemFlags,
                        Reserved = 0,
                        ChunkIndex = deletedChunkIndex,
                        ItemHashIndex = PersistentWorldDeletedItemHashIndex
                    };
                    wroteRecord = true;
                }
                else if (PersistentWorldDeltaRecord.IsValid(in deltaRecord) &&
                         chunkLookup.TryGetValue(deltaRecord.ChunkId, out ushort chunkIndex) &&
                         itemHashLookup.TryGetValue(deltaRecord.ItemPersistentIdHash, out ushort itemHashIndex))
                {
                    saveRecord = new PersistentWorldSaveRecord16
                    {
                        PackedLocalPosition = deltaRecord.PackedLocalPosition,
                        InstanceUid = deltaRecord.InstanceUid,
                        Quantity = (ushort)math.clamp(deltaRecord.Quantity, 1, ushort.MaxValue),
                        ItemFlags = deltaRecord.ItemFlags,
                        Reserved = 0,
                        ChunkIndex = chunkIndex,
                        ItemHashIndex = itemHashIndex
                    };
                    wroteRecord = true;
                }

                if (!wroteRecord)
                {
                    error = "Persistent-world section record lookup failed.";
                    return false;
                }

                UnsafeUtility.CopyStructureToPtr(ref saveRecord, AddByteOffset(destination, cursor));
                cursor += UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>();
            }

            return true;
        }

        private static bool WritePersistentWorldSection(
            byte* destination,
            NativeList<PersistentWorldDeltaRecord> persistentWorldDeltas,
            int startIndex,
            int recordCount,
            NativeParallelHashMap<int3, ushort> chunkLookup,
            NativeList<int3> chunkTable,
            NativeParallelHashMap<ulong, ushort> itemHashLookup,
            NativeList<ulong> itemHashTable,
            out string error)
        {
            error = string.Empty;
            if (destination == null)
            {
                error = "Persistent-world section destination is null.";
                return false;
            }

            if (!persistentWorldDeltas.IsCreated ||
                startIndex < 0 ||
                recordCount < 0 ||
                startIndex > persistentWorldDeltas.Length - recordCount)
            {
                error = "Persistent-world section record range is invalid.";
                return false;
            }

            int safeRecordCount = recordCount;
            if (!TryComputePersistentWorldSectionLength(safeRecordCount, chunkTable.Length, itemHashTable.Length, CurrentVersion, out int sectionLength))
            {
                error = "Persistent-world section exceeds supported bounds.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = new PersistentWorldSectionHeader
            {
                ChunkCount = (uint)chunkTable.Length,
                ItemHashCount = (uint)itemHashTable.Length,
                RecordCount = (uint)safeRecordCount
            };

            int sectionHeaderSize = ResolvePersistentWorldSectionHeaderSize(CurrentVersion);
            WritePersistentWorldSectionHeader(destination, sectionHeaderSize, in sectionHeader);
            int cursor = sectionHeaderSize;

            if (chunkTable.Length > 0)
            {
                void* chunkSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkTable.AsArray());
                int chunkBytes = chunkTable.Length * UnsafeUtility.SizeOf<int3>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, chunkSourcePtr, chunkBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    error = "Persistent-world chunk table write exceeded section bounds.";
                    return false;
                }

                cursor += chunkBytes;
            }

            if (itemHashTable.Length > 0)
            {
                void* itemSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(itemHashTable.AsArray());
                int itemBytes = itemHashTable.Length * UnsafeUtility.SizeOf<ulong>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, itemSourcePtr, itemBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    error = "Persistent-world item hash table write exceeded section bounds.";
                    return false;
                }

                cursor += itemBytes;
            }

            int endIndex = startIndex + safeRecordCount;
            for (int i = startIndex; i < endIndex; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                PersistentWorldSaveRecord16 saveRecord = default;
                bool wroteRecord = false;
                if (PersistentWorldDeltaRecord.IsDeleted(in deltaRecord) &&
                    chunkLookup.TryGetValue(deltaRecord.ChunkId, out ushort deletedChunkIndex))
                {
                    saveRecord = new PersistentWorldSaveRecord16
                    {
                        PackedLocalPosition = deltaRecord.PackedLocalPosition,
                        InstanceUid = deltaRecord.InstanceUid,
                        Quantity = 1,
                        ItemFlags = deltaRecord.ItemFlags,
                        Reserved = 0,
                        ChunkIndex = deletedChunkIndex,
                        ItemHashIndex = PersistentWorldDeletedItemHashIndex
                    };
                    wroteRecord = true;
                }
                else if (PersistentWorldDeltaRecord.IsValid(in deltaRecord) &&
                         chunkLookup.TryGetValue(deltaRecord.ChunkId, out ushort chunkIndex) &&
                         itemHashLookup.TryGetValue(deltaRecord.ItemPersistentIdHash, out ushort itemHashIndex))
                {
                    saveRecord = new PersistentWorldSaveRecord16
                    {
                        PackedLocalPosition = deltaRecord.PackedLocalPosition,
                        InstanceUid = deltaRecord.InstanceUid,
                        Quantity = (ushort)math.clamp(deltaRecord.Quantity, 1, ushort.MaxValue),
                        ItemFlags = deltaRecord.ItemFlags,
                        Reserved = 0,
                        ChunkIndex = chunkIndex,
                        ItemHashIndex = itemHashIndex
                    };
                    wroteRecord = true;
                }

                if (!wroteRecord)
                {
                    error = "Persistent-world section record lookup failed.";
                    return false;
                }

                UnsafeUtility.CopyStructureToPtr(ref saveRecord, AddByteOffset(destination, cursor));
                cursor += UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>();
            }

            return true;
        }

        private static bool TryReadPersistentWorldDeltasV5(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out string error)
        {
            persistentWorldDeltas = null;
            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out _, out error))
                return false;

            int sectionHeaderSize = ResolvePersistentWorldSectionHeaderSize(header.Version);
            PersistentWorldSectionHeader sectionHeader = ReadPersistentWorldSectionHeader(AddByteOffset(rawPtr, entitySectionOffset), sectionHeaderSize);
            if (!TryConvertPersistentWorldSectionCounts(in sectionHeader, out int recordCount, out int chunkCount, out int itemHashCount) ||
                !TryConvertSectionCount(header.EntityCount, out int expectedRecordCount))
            {
                error = "Persistent-world delta section header exceeds supported bounds.";
                return false;
            }

            if (recordCount != expectedRecordCount)
            {
                error = "Persistent-world delta section count mismatch.";
                return false;
            }

            persistentWorldDeltas = new PersistentWorldDeltaRecord[recordCount];
            if (recordCount <= 0)
            {
                error = string.Empty;
                return true;
            }

            int cursor = entitySectionOffset + sectionHeaderSize;
            byte* chunkTablePtr = AddByteOffset(rawPtr, cursor);
            cursor += chunkCount * UnsafeUtility.SizeOf<int3>();
            byte* itemHashTablePtr = AddByteOffset(rawPtr, cursor);
            cursor += itemHashCount * UnsafeUtility.SizeOf<ulong>();
            byte* saveRecordPtr = AddByteOffset(rawPtr, cursor);

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldSaveRecord16 saveRecord = UnsafeUtility.ReadArrayElement<PersistentWorldSaveRecord16>(saveRecordPtr, i);
                bool isDeleted = (((PersistentWorldItemFlags)saveRecord.ItemFlags) & PersistentWorldItemFlags.Deleted) != 0;
                if (saveRecord.ChunkIndex >= chunkCount)
                {
                    error = "Persistent-world delta section lookup index is out of range.";
                    persistentWorldDeltas = null;
                    return false;
                }

                ulong itemHash = 0UL;
                if (!isDeleted)
                {
                    if (saveRecord.ItemHashIndex >= itemHashCount)
                    {
                        error = "Persistent-world delta item-hash lookup index is out of range.";
                        persistentWorldDeltas = null;
                        return false;
                    }

                    itemHash = UnsafeUtility.ReadArrayElement<ulong>(itemHashTablePtr, saveRecord.ItemHashIndex);
                }

                PersistentWorldDeltaRecord deltaRecord = new PersistentWorldDeltaRecord
                {
                    ChunkId = UnsafeUtility.ReadArrayElement<int3>(chunkTablePtr, saveRecord.ChunkIndex),
                    ItemPersistentIdHash = itemHash,
                    InstanceUid = saveRecord.InstanceUid,
                    PackedLocalPosition = saveRecord.PackedLocalPosition,
                    Quantity = saveRecord.Quantity == 0 ? (ushort)1 : saveRecord.Quantity,
                    ItemFlags = saveRecord.ItemFlags,
                    Reserved = saveRecord.Reserved
                };

                if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord))
                {
                    error = "Persistent-world delta section contains an invalid record.";
                    persistentWorldDeltas = null;
                    return false;
                }

                persistentWorldDeltas[i] = deltaRecord;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolvePersistentWorldSectionLength(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out int entitySectionOffset,
            out int entitySectionLength,
            out string error)
        {
            entitySectionLength = 0;
            error = string.Empty;
            if (!TryResolvePayloadOffsets(in header, out _, out _, out entitySectionOffset, out error))
                return false;

            if (entitySectionOffset < 0 || entitySectionOffset > rawPayloadLength)
            {
                error = "Entity payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            if (header.Version < CompactPersistentWorldSectionVersion)
            {
                if (!TryConvertSectionCount(header.EntityCount, out int entityCount))
                {
                    error = "Entity count exceeds supported bounds.";
                    return false;
                }

                long entitySectionLengthLong = entityCount > 0
                    ? (long)entityCount * UnsafeUtility.SizeOf<PersistentWorldDeltaRecordLegacy64>()
                    : 0L;
                if (entitySectionLengthLong > int.MaxValue)
                {
                    error = "Entity payload length exceeds the supported range.";
                    return false;
                }

                entitySectionLength = (int)entitySectionLengthLong;
                if (!IsByteRangeWithin(entitySectionOffset, entitySectionLength, rawPayloadLength))
                {
                    error = "Entity payload exceeds the decompressed payload bounds.";
                    return false;
                }

                return true;
            }

            if (header.EntityCount <= 0)
                return true;

            int sectionHeaderSize = ResolvePersistentWorldSectionHeaderSize(header.Version);
            if (!IsByteRangeWithin(entitySectionOffset, sectionHeaderSize, rawPayloadLength))
            {
                error = "Persistent-world delta section header is truncated.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = ReadPersistentWorldSectionHeader(AddByteOffset(rawPtr, entitySectionOffset), sectionHeaderSize);
            if (!TryConvertPersistentWorldSectionCounts(in sectionHeader, out int recordCount, out int chunkCount, out int itemHashCount) ||
                !TryConvertSectionCount(header.EntityCount, out int expectedRecordCount))
            {
                error = "Persistent-world delta section header exceeds supported bounds.";
                return false;
            }

            if (recordCount != expectedRecordCount)
            {
                error = "Persistent-world delta section header count mismatch.";
                return false;
            }

            if (!TryComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount, header.Version, out entitySectionLength))
            {
                error = "Persistent-world delta section exceeds supported bounds.";
                return false;
            }

            if (!IsByteRangeWithin(entitySectionOffset, entitySectionLength, rawPayloadLength))
            {
                error = "Persistent-world delta section exceeds the decompressed payload bounds.";
                return false;
            }

            return true;
        }

        private static bool WriteEcosystemSection(
            byte* destination,
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorStates,
            out string error)
        {
            error = string.Empty;
            if (destination == null)
            {
                error = "Ecosystem section destination is null.";
                return false;
            }

            int recordCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
            EcosystemSectionHeader sectionHeader = new EcosystemSectionHeader
            {
                RecordCount = (uint)recordCount
            };

            int ecosystemHeaderSize = ResolveEcosystemSectionHeaderSize(CurrentVersion);
            WriteEcosystemSectionHeader(destination, ecosystemHeaderSize, in sectionHeader);
            if (recordCount <= 0)
                return true;

            int recordSize = UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
            if (!TryComputeEcosystemSectionLength(recordCount, CurrentVersion, out int sectionLength))
            {
                error = "Ecosystem section exceeds supported bounds.";
                return false;
            }

            long recordBytes = (long)recordCount * recordSize;
            if (recordBytes > sectionLength - ecosystemHeaderSize)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                error = "Ecosystem section write exceeded section bounds.";
                return false;
            }

            byte* writePtr = AddByteOffset(destination, ecosystemHeaderSize);
            for (int i = 0; i < recordCount; i++)
            {
                EcosystemSectorSaveRecord record = ecosystemSectorStates[i];
                UnsafeUtility.CopyStructureToPtr(ref record, writePtr + (i * recordSize));
            }

            return true;
        }

        private static bool TryReadEcosystemSectorStates(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out string error)
        {
            ecosystemSectorStates = null;
            error = string.Empty;

            if (header.Version < EcosystemSectionVersion)
                return true;

            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out int entitySectionLength, out error))
                return false;

            if (!TryResolveEcosystemSectionLength(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    entitySectionOffset,
                    entitySectionLength,
                    out int ecosystemSectionOffset,
                    out int ecosystemSectionLength,
                    out error))
            {
                return false;
            }

            int ecosystemHeaderSize = ResolveEcosystemSectionHeaderSize(header.Version);
            EcosystemSectionHeader sectionHeader = ReadEcosystemSectionHeader(AddByteOffset(rawPtr, ecosystemSectionOffset), ecosystemHeaderSize);
            if (!TryConvertSectionCount(sectionHeader.RecordCount, out int recordCount))
            {
                error = "Ecosystem section header exceeds supported bounds.";
                return false;
            }

            ecosystemSectorStates = new EcosystemSectorSaveRecord[recordCount];
            if (recordCount <= 0)
                return true;

            fixed (EcosystemSectorSaveRecord* destinationPtr = ecosystemSectorStates)
            {
                int recordBytes = recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(
                        destinationPtr,
                        ecosystemSectorStates.Length * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>(),
                        AddByteOffset(rawPtr, ecosystemSectionOffset + ecosystemHeaderSize),
                        recordBytes))
                {
                    error = "Ecosystem sector-state copy exceeded destination bounds.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveEcosystemSectionLength(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            int entitySectionOffset,
            int entitySectionLength,
            out int ecosystemSectionOffset,
            out int ecosystemSectionLength,
            out string error)
        {
            ecosystemSectionOffset = 0;
            ecosystemSectionLength = 0;
            error = string.Empty;

            if (header.Version < EcosystemSectionVersion)
                return true;

            if (!IsByteRangeWithin(entitySectionOffset, entitySectionLength, rawPayloadLength))
            {
                error = "Persistent-world section bounds are invalid before ecosystem resolution.";
                return false;
            }

            ecosystemSectionOffset = entitySectionOffset + entitySectionLength;

            if (ecosystemSectionOffset < 0 || ecosystemSectionOffset > rawPayloadLength)
            {
                error = "Ecosystem payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            int ecosystemHeaderSize = ResolveEcosystemSectionHeaderSize(header.Version);
            if (!IsByteRangeWithin(ecosystemSectionOffset, ecosystemHeaderSize, rawPayloadLength))
            {
                error = "Ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader sectionHeader = ReadEcosystemSectionHeader(AddByteOffset(rawPtr, ecosystemSectionOffset), ecosystemHeaderSize);
            if (!TryConvertSectionCount(sectionHeader.RecordCount, out int recordCount) ||
                !TryComputeEcosystemSectionLength(recordCount, header.Version, out ecosystemSectionLength))
            {
                error = "Ecosystem section header exceeds supported bounds.";
                return false;
            }

            if (!IsByteRangeWithin(ecosystemSectionOffset, ecosystemSectionLength, rawPayloadLength))
            {
                error = "Ecosystem section exceeds the decompressed payload bounds.";
                return false;
            }

            return true;
        }

        private static bool TryCompressPayload(
            byte* rawPtr,
            int rawPayloadLength,
            byte* compressedPtr,
            int compressedCapacity,
            out int compressedPayloadLength,
            out uint blockFlags,
            out string error)
        {
            blockFlags = 0u;
            compressedPayloadLength = 0;
            error = string.Empty;

            if (rawPtr == null || compressedPtr == null || rawPayloadLength <= 0 || compressedCapacity <= 0)
            {
                error = "Compression input is invalid.";
                return false;
            }

            int compressedLength = Lz4ProtectedBlockCompress(rawPtr, rawPayloadLength, compressedPtr, compressedCapacity);
            if (compressedLength <= 0)
            {
                error = "LZ4 block compression failed.";
                return false;
            }

            compressedPayloadLength = compressedLength;
            blockFlags = FlagProtectedLz4Blocks;
            return true;
        }

        private static bool TryWriteIndexedCompressedBlock(
            byte* rawPtr,
            int rawPayloadLength,
            byte* destinationFilePtr,
            int destinationCapacity,
            ref int fileCursor,
            out int storedBlockLength,
            out uint blockFlags,
            out string error)
        {
            storedBlockLength = 0;
            blockFlags = 0u;
            error = string.Empty;

            if (rawPtr == null || destinationFilePtr == null || rawPayloadLength <= 0)
            {
                error = "Indexed block compression input is invalid.";
                return false;
            }

            int blockHeaderOffset = fileCursor;
            int payloadDestinationOffset = blockHeaderOffset + IndexedSectorBlockHeaderSize;
            if (payloadDestinationOffset >= destinationCapacity)
            {
                error = "Indexed block header exceeded the destination capacity.";
                return false;
            }

            byte* payloadDestinationPtr = destinationFilePtr + payloadDestinationOffset;
            int remainingCapacity = destinationCapacity - payloadDestinationOffset;
            if (!TryCompressPayload(
                    rawPtr,
                    rawPayloadLength,
                    payloadDestinationPtr,
                    remainingCapacity,
                    out int compressedPayloadLength,
                    out blockFlags,
                    out error))
            {
                return false;
            }

            IndexedSectorBlockHeader blockHeader = new IndexedSectorBlockHeader
            {
                Flags = blockFlags,
                Reserved = 0u
            };

            UnsafeUtility.CopyStructureToPtr(ref blockHeader, destinationFilePtr + blockHeaderOffset);
            storedBlockLength = IndexedSectorBlockHeaderSize + compressedPayloadLength;
            fileCursor += storedBlockLength;
            return true;
        }

        private static bool TryExpandTokenizedPayloadInPlace(
            byte* payloadPtr,
            int tokenizedPayloadLength,
            int payloadCapacity,
            out int expandedPayloadLength,
            out string error)
        {
            expandedPayloadLength = 0;
            error = string.Empty;

            if (payloadPtr == null || tokenizedPayloadLength < TokenizedPayloadHeaderSize)
            {
                error = "Tokenized payload header is truncated.";
                return false;
            }

            TokenizedPayloadHeader header = UnsafeUtility.ReadArrayElement<TokenizedPayloadHeader>(payloadPtr, 0);
            int tokenCount = header.TokenCount;
            if (header.ExpandedPayloadLength > int.MaxValue)
            {
                error = "Tokenized payload declared an unsupported expanded length.";
                return false;
            }

            expandedPayloadLength = (int)header.ExpandedPayloadLength;
            if (tokenCount <= 0 || tokenCount > MaxTokenCount)
            {
                error = "Tokenized payload declared an invalid token count.";
                return false;
            }

            if (expandedPayloadLength <= 0 || expandedPayloadLength > payloadCapacity)
            {
                error = "Tokenized payload declared an invalid expanded length.";
                return false;
            }

            int tokenTableBytes = tokenCount * TokenBlockSizeBytes;
            int encodedStreamOffset = TokenizedPayloadHeaderSize + tokenTableBytes;
            if (encodedStreamOffset > tokenizedPayloadLength)
            {
                error = "Tokenized payload token table exceeds the decompressed bounds.";
                return false;
            }

            int readOffset = tokenizedPayloadLength - 1;
            int writeOffset = expandedPayloadLength - 1;
            while (readOffset >= encodedStreamOffset)
            {
                byte tail = payloadPtr[readOffset--];
                if (tail != TokenEscapeMarker)
                {
                    payloadPtr[writeOffset--] = tail;
                    continue;
                }

                if (readOffset < encodedStreamOffset)
                {
                    error = "Tokenized payload ended with an incomplete escape marker.";
                    return false;
                }

                byte prefix = payloadPtr[readOffset--];
                if (prefix == TokenEscapeMarker)
                {
                    payloadPtr[writeOffset--] = TokenEscapeMarker;
                    continue;
                }

                if (prefix >= tokenCount)
                {
                    error = "Tokenized payload referenced an invalid token index.";
                    return false;
                }

                if (writeOffset + 1 < TokenBlockSizeBytes)
                {
                    error = "Tokenized payload expanded beyond the destination bounds.";
                    return false;
                }

                byte* tokenSourcePtr = payloadPtr + TokenizedPayloadHeaderSize + (prefix * TokenBlockSizeBytes);
                for (int i = TokenBlockSizeBytes - 1; i >= 0; i--)
                    payloadPtr[writeOffset--] = tokenSourcePtr[i];
            }

            if (writeOffset != -1)
            {
                error = "Tokenized payload expansion did not fully reconstruct the logical payload.";
                return false;
            }

            return true;
        }

        private static bool TryValidateHeader(SaveFileHeader header, out string error)
        {
            if (!TryValidateCurrentBinaryLayouts(out error))
                return false;

            if (header.Version < MinimumSupportedVersion || header.Version > CurrentVersion)
            {
                error = $"Unsupported save header version {header.Version}.";
                return false;
            }

            if (header.CompatMask != CurrentCompatMask)
            {
                error = $"Unsupported save compatibility mask 0x{header.CompatMask:X2}.";
                return false;
            }

            if ((header.Flags & FlagLz4Blocks) == 0)
            {
                error = "Save payload is not flagged as LZ4 block-compressed.";
                return false;
            }

            int expectedHeaderSize = ResolveExpectedHeaderSize(header.Version);
            if (expectedHeaderSize <= 0)
            {
                error = $"Unsupported save header version {header.Version}.";
                return false;
            }

            if (header.Version < IndexedBlockStorageVersion && header.PlayerOffset != expectedHeaderSize)
            {
                error = $"Player payload offset {header.PlayerOffset} does not match fixed header size {expectedHeaderSize}.";
                return false;
            }

            if (header.PlayerOffset > int.MaxValue ||
                header.DeltaOffset > int.MaxValue ||
                header.EntityOffset > int.MaxValue)
            {
                error = "Save payload offset exceeds the supported decoder range.";
                return false;
            }

            if (header.Version >= IndexedBlockStorageVersion)
            {
                if (header.PlayerOffset < expectedHeaderSize)
                {
                    error = "Indexed metadata block offset is smaller than the fixed header.";
                    return false;
                }

                if (header.DeltaOffset < header.PlayerOffset)
                {
                    error = "Indexed packed quest-state offset precedes the metadata block.";
                    return false;
                }
            }
            else if (header.DeltaOffset < header.PlayerOffset || header.EntityOffset < header.DeltaOffset)
            {
                error = "Save payload offsets are out of order.";
                return false;
            }

            long maxPayloadOffset = (long)header.PlayerOffset + RawPayloadCapacityBytes;
            if (header.DeltaOffset > maxPayloadOffset)
            {
                error = "Save packed quest-state offset exceeds the raw payload decoder budget.";
                return false;
            }

            if (header.EntityOffset > maxPayloadOffset)
            {
                error = "Save entity payload offset exceeds the raw payload decoder budget.";
                return false;
            }

            if (!TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error))
                return false;

            int maxEntityRecordSize = header.Version >= CompactPersistentWorldSectionVersion
                ? UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>()
                : UnsafeUtility.SizeOf<PersistentWorldDeltaRecordLegacy64>();
            if (header.EntityCount > (uint)(RawPayloadCapacityBytes / maxEntityRecordSize))
            {
                error = "Save entity count exceeds the decoder budget.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static uint ComputePackedQuestStateChecksum(NativeArray<uint> packedQuestStateWords)
        {
            if (!packedQuestStateWords.IsCreated || packedQuestStateWords.Length <= 0)
                return 0u;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateWords);
            return Hash32(sourcePtr, (long)packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>());
        }

        private static uint ComputePackedQuestStateChecksum(uint[] packedQuestStateWords)
        {
            if (packedQuestStateWords == null || packedQuestStateWords.Length <= 0)
                return 0u;

            fixed (uint* sourcePtr = packedQuestStateWords)
            {
                return Hash32(sourcePtr, (long)packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>());
            }
        }

        private static SaveFileHeader ConvertLegacyHeader(in LegacySaveFileHeader legacyHeader)
        {
            return new SaveFileHeader
            {
                MagicValue = legacyHeader.MagicValue,
                Version = legacyHeader.Version,
                CompatMask = legacyHeader.CompatMask,
                Flags = legacyHeader.Flags,
                TimestampUnixMs = legacyHeader.TimestampUnixMs,
                DeltaCount = legacyHeader.DeltaCount,
                EntityCount = legacyHeader.EntityCount,
                PlayerOffset = legacyHeader.PlayerOffset,
                DeltaOffset = legacyHeader.DeltaOffset,
                EntityOffset = legacyHeader.EntityOffset,
                HashPayload64 = legacyHeader.HashPayload32,
                HashHeader64 = legacyHeader.HashHeader32
            };
        }

        private static SaveFileHeader ConvertIndexedHeaderV8(in IndexedSaveFileHeaderV8 legacyHeader)
        {
            ulong hashPayload64 = ComposeUInt64(legacyHeader.HashPayload64Lo, legacyHeader.HashPayload64Hi);
            ulong hashHeader64 = ComposeUInt64(legacyHeader.HashHeader64Lo, legacyHeader.HashHeader64Hi);
            return new SaveFileHeader
            {
                MagicValue = legacyHeader.MagicValue,
                Version = legacyHeader.Version,
                CompatMask = legacyHeader.CompatMask,
                Flags = legacyHeader.Flags,
                TimestampUnixMs = legacyHeader.TimestampUnixMs,
                Checksum = unchecked((uint)hashPayload64),
                DeltaCount = legacyHeader.DeltaCount,
                EntityCount = legacyHeader.EntityCount,
                PlayerOffset = legacyHeader.PlayerOffset,
                DeltaOffset = legacyHeader.DeltaOffset,
                EntityOffset = legacyHeader.EntityOffset,
                HashPayload64 = hashPayload64,
                HashHeader64 = hashHeader64
            };
        }

        private static IndexedSaveFileHeaderV8 ConvertToIndexedHeaderV8(in SaveFileHeader header)
        {
            SplitUInt64(header.HashPayload64, out uint hashPayloadLo, out uint hashPayloadHi);
            SplitUInt64(header.HashHeader64, out uint hashHeaderLo, out uint hashHeaderHi);
            return new IndexedSaveFileHeaderV8
            {
                MagicValue = header.MagicValue,
                Version = header.Version,
                CompatMask = header.CompatMask,
                Flags = header.Flags,
                TimestampUnixMs = header.TimestampUnixMs,
                DeltaCount = header.DeltaCount,
                EntityCount = header.EntityCount,
                PlayerOffset = header.PlayerOffset,
                DeltaOffset = header.DeltaOffset,
                EntityOffset = header.EntityOffset,
                HashPayload64Lo = hashPayloadLo,
                HashPayload64Hi = hashPayloadHi,
                HashHeader64Lo = hashHeaderLo,
                HashHeader64Hi = hashHeaderHi
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ComposeUInt64(uint low, uint high)
        {
            return ((ulong)high << 32) | low;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SplitUInt64(ulong value, out uint low, out uint high)
        {
            low = (uint)value;
            high = (uint)(value >> 32);
        }

        private static SaveFileHeader ReadVersionedSaveFileHeader(byte* filePtr, ushort version)
        {
            if (version >= HeaderChecksumVersion)
                return UnsafeUtility.ReadArrayElement<SaveFileHeader>(filePtr, 0);

            if (version >= First64BitHashVersion)
            {
                IndexedSaveFileHeaderV8 legacyHeader = UnsafeUtility.ReadArrayElement<IndexedSaveFileHeaderV8>(filePtr, 0);
                return ConvertIndexedHeaderV8(in legacyHeader);
            }

            LegacySaveFileHeader oldHeader = UnsafeUtility.ReadArrayElement<LegacySaveFileHeader>(filePtr, 0);
            return ConvertLegacyHeader(in oldHeader);
        }

        private static void WriteVersionedSaveFileHeader(byte* filePtr, ref SaveFileHeader header)
        {
            if (header.Version >= HeaderChecksumVersion)
            {
                UnsafeUtility.CopyStructureToPtr(ref header, filePtr);
                return;
            }

            IndexedSaveFileHeaderV8 legacyHeader = ConvertToIndexedHeaderV8(in header);
            UnsafeUtility.CopyStructureToPtr(ref legacyHeader, filePtr);
        }

        private static int ResolveHeaderSize(ushort version)
        {
            switch (version)
            {
                case 0x0003:
                    return LegacyHeaderSize;
                case First64BitHashVersion:
                case CompactPersistentWorldSectionVersion:
                case EcosystemSectionVersion:
                case TokenizedPayloadVersion:
                case IndexedBlockStorageVersion:
                    return IndexedHeaderV8Size;
                case HeaderChecksumVersion:
                case AlignedIndexedSectorDirectoryVersion:
                case AlignedSectionHeaderVersion:
                    return CurrentHeaderSize;
                default:
                    return 0;
            }
        }

        private static int ResolveExpectedHeaderSize(ushort version)
        {
            return ResolveHeaderSize(version);
        }

        private static bool TryResolvePayloadOffsets(
            in SaveFileHeader header,
            out int payloadBaseOffset,
            out int deltaSectionOffset,
            out int entitySectionOffset,
            out string error)
        {
            payloadBaseOffset = 0;
            deltaSectionOffset = 0;
            entitySectionOffset = 0;
            if (header.PlayerOffset > int.MaxValue ||
                header.DeltaOffset > int.MaxValue ||
                header.EntityOffset > int.MaxValue)
            {
                error = "Save payload offset exceeds the supported decoder range.";
                return false;
            }

            payloadBaseOffset = (int)header.PlayerOffset;
            deltaSectionOffset = (int)header.DeltaOffset - payloadBaseOffset;
            entitySectionOffset = (int)header.EntityOffset - payloadBaseOffset;
            error = string.Empty;
            return true;
        }

        private static bool TryDecodeSaveDataByteLength(
            in PayloadPrefixInfo prefix,
            out int saveDataLength,
            out string error)
        {
            saveDataLength = 0;
            if (prefix.SaveDataByteLength > int.MaxValue)
            {
                error = "Serialized save data length exceeds the supported decoder range.";
                return false;
            }

            saveDataLength = (int)prefix.SaveDataByteLength;
            error = string.Empty;
            return true;
        }

        private static bool TryShiftPayloadOffset(uint offset, int byteShift, out uint shiftedOffset, out string error)
        {
            shiftedOffset = 0u;
            long shifted = (long)offset + byteShift;
            if (shifted < 0L || shifted > int.MaxValue)
            {
                error = "Migrated save payload offset exceeds the supported decoder range.";
                return false;
            }

            shiftedOffset = (uint)shifted;
            error = string.Empty;
            return true;
        }

        private static string FormatPayloadChecksum(in SaveFileHeader header)
        {
            if (header.Version >= HeaderChecksumVersion)
                return $"{header.HashPayload64:X16}:{header.Checksum:X8}";

            return header.Version >= First64BitHashVersion
                ? header.HashPayload64.ToString("X16")
                : ((uint)header.HashPayload64).ToString("X8");
        }

        private static ulong ToUnixMilliseconds(long utcTicks)
        {
            long safeTicks = utcTicks > 0L ? utcTicks : DateTime.UtcNow.Ticks;
            return (ulong)((safeTicks - UnixEpochTicks) / TimeSpan.TicksPerMillisecond);
        }

        private static long ToUtcTicks(ulong unixMilliseconds)
        {
            return UnixEpochTicks + ((long)unixMilliseconds * TimeSpan.TicksPerMillisecond);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z))))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private static bool TryToRuntimePosition(AbsoluteUniversePosition position, out Vector3 runtime)
        {
            runtime = default;
            if (!position.IsFinite())
                return false;

            if (!position.TryToRuntimeFloat3(out float3 runtimePosition))
                return false;

            runtime = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return true;
        }

        private static bool TryReadUtf16String(
            byte* source,
            int sourceLength,
            ref int cursor,
            int byteLength,
            out string value,
            out string error)
        {
            value = string.Empty;
            error = string.Empty;

            if (!IsByteRangeWithin(cursor, byteLength, sourceLength))
            {
                error = "UTF-16 metadata block exceeds the payload bounds.";
                return false;
            }

            if (byteLength == 0)
                return true;

            if ((byteLength & 1) != 0)
            {
                error = "UTF-16 metadata block has an odd byte length.";
                return false;
            }

            int charCount = byteLength / sizeof(char);
            value = string.Intern(new string((char*)AddByteOffset(source, cursor), 0, charCount));
            cursor += byteLength;
            return true;
        }

        private static bool TryCopyUtf16StringToUnmanaged(
            string source,
            byte* destination,
            int destinationCapacityBytes,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(source))
                return true;

            fixed (char* sourcePtr = source)
            {
                if (!UnsafeMemoryCopyGuard.SafeCopy(destination, destinationCapacityBytes, sourcePtr, source.Length * sizeof(char)))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    error = "UTF-16 metadata string write exceeded destination bounds.";
                    return false;
                }
            }

            return true;
        }

        private static int Lz4BlockDecompress(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity)
        {
            int failedBlockIndex = -1;
            return Lz4BlockDecompress(
                source,
                compressedLength,
                destination,
                destinationCapacity,
                out failedBlockIndex);
        }

        private static int Lz4BlockDecompress(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity,
            out int failedBlockIndex)
        {
            failedBlockIndex = -1;
            if (source == null ||
                destination == null ||
                compressedLength <= 0 ||
                compressedLength > MaxCompressedPayloadBytes ||
                destinationCapacity <= 0 ||
                destinationCapacity > RawPayloadCapacityBytes)
                return 0;

            return Lz4BlockDecompressStandard(
                source,
                compressedLength,
                destination,
                destinationCapacity,
                out failedBlockIndex);
        }

        private static int Lz4BlockDecompressStandard(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity,
            out int failedBlockIndex)
        {
            failedBlockIndex = -1;
            if (source == null ||
                destination == null ||
                compressedLength <= 0 ||
                compressedLength > MaxCompressedPayloadBytes ||
                destinationCapacity <= 0 ||
                destinationCapacity > RawPayloadCapacityBytes)
                return 0;

            int blockHeaderBytes = StandardCompressedBlockHeaderBytes;
            int minimumCompressedBlockBytes = blockHeaderBytes + 1;
            if (compressedLength < minimumCompressedBlockBytes)
                return 0;

            int sourceOffset = 0;
            int destinationOffset = 0;
            int blockIterations = 0;
            int maxBlocksFromSource = compressedLength / minimumCompressedBlockBytes;
            int maxBlocksFromDestination = (destinationCapacity + BlockSizeBytes - 1) / BlockSizeBytes;
            int maxBlockIterations = math.min(maxBlocksFromSource, maxBlocksFromDestination);
            if (maxBlockIterations <= 0)
                return 0;

            while (sourceOffset < compressedLength && blockIterations < maxBlockIterations)
            {
                blockIterations++;
                if (sourceOffset + blockHeaderBytes > compressedLength)
                    return 0;

                int encodedBlockCompressedLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset, 0);
                int blockCompressedLength = DecodeCompressedBlockLength(encodedBlockCompressedLength);
                bool isManagedDeflateBlock = IsManagedDeflateBlock(encodedBlockCompressedLength);
                int blockRawLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset + 4, 0);
                if (blockCompressedLength <= 0 || blockRawLength <= 0)
                    return 0;

                if (blockRawLength > BlockSizeBytes)
                    return 0;

                sourceOffset += blockHeaderBytes;
                if (sourceOffset + blockCompressedLength > compressedLength)
                    return 0;

                if (destinationOffset + blockRawLength > destinationCapacity)
                    return 0;

                if (!TryDecompressBlock(
                        source + sourceOffset,
                        blockCompressedLength,
                        destination + destinationOffset,
                        blockRawLength,
                        isManagedDeflateBlock,
                        out _))
                    return 0;

                sourceOffset += blockCompressedLength;
                destinationOffset += blockRawLength;
            }

            if (sourceOffset != compressedLength)
                return 0;

            return destinationOffset;
        }

        private static bool TryCompressBlock(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity,
            out int compressedLength)
        {
            compressedLength = 0;
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 0)
                return false;

            if (HectonNativeBridge.IsAvailable(HectonNativeLibrary.Lz4))
            {
                try
                {
                    compressedLength = LZ4Compress(source, destination, sourceLength, destinationCapacity);
                    if (compressedLength > 0)
                        return true;
                }
                catch (Exception exception)
                {
                    if (HectonNativeBridge.IsNativeLoadFailure(exception))
                        HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.Lz4, exception);
                    else
                        throw;
                }
            }

            return false;
        }

        private static bool TryCompressBlockNativeOnly(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity,
            out int compressedLength)
        {
            compressedLength = 0;
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 0)
                return false;

            if (!HectonNativeBridge.IsAvailable(HectonNativeLibrary.Lz4))
                return false;

            compressedLength = LZ4Compress(source, destination, sourceLength, destinationCapacity);
            return compressedLength > 0;
        }

        private static bool TryDecompressBlock(
            byte* source,
            int compressedLength,
            byte* destination,
            int rawLength,
            bool forceManagedDeflate,
            out int actualLength)
        {
            actualLength = 0;
            if (source == null || destination == null || compressedLength <= 0 || rawLength <= 0)
                return false;

            if (!forceManagedDeflate && HectonNativeBridge.IsAvailable(HectonNativeLibrary.Lz4))
            {
                try
                {
                    actualLength = LZ4Decompress(source, destination, compressedLength, rawLength);
                    if (actualLength == rawLength)
                        return true;
                }
                catch (Exception exception)
                {
                    if (HectonNativeBridge.IsNativeLoadFailure(exception))
                        HectonNativeBridge.MarkUnavailableFromException(HectonNativeLibrary.Lz4, exception);
                    else
                        throw;
                }
            }

            if (!forceManagedDeflate)
                return false;

            actualLength = DeflateBlockDecompressManaged(source, compressedLength, destination, rawLength);
            return actualLength == rawLength;
        }

        private static int EncodeCompressedBlockLength(int compressedLength)
        {
            if (compressedLength <= 0 || compressedLength > CompressedBlockLengthMask)
                return 0;

            return compressedLength;
        }

        private static int DecodeCompressedBlockLength(int encodedCompressedLength)
        {
            return encodedCompressedLength & CompressedBlockLengthMask;
        }

        private static bool IsManagedDeflateBlock(int encodedCompressedLength)
        {
            return (encodedCompressedLength & ManagedDeflateBlockLengthFlag) != 0;
        }

        private static int DeflateBlockDecompressManaged(
            byte* source,
            int compressedLength,
            byte* destination,
            int rawLength)
        {
            try
            {
                lock (s_managedCompressionLock)
                {
                    using (UnmanagedMemoryStream input = new UnmanagedMemoryStream(source, compressedLength))
                    using (DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress, true))
                    using (UnmanagedMemoryStream output = new UnmanagedMemoryStream(destination, rawLength, rawLength, FileAccess.Write))
                    {
                        CopyStreamWithManagedCompressionScratch(inflate, output);
                        long written = output.Position;
                        return written == rawLength ? (int)written : 0;
                    }
                }
            }
            catch (InvalidDataException)
            {
                return 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (NotSupportedException)
            {
                return 0;
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        private static void CopyStreamWithManagedCompressionScratch(Stream source, Stream destination)
        {
            Span<byte> scratch = stackalloc byte[ManagedCompressionStackScratchBytes];
            int bytesRead;
            while ((bytesRead = source.Read(scratch)) > 0)
                destination.Write(scratch.Slice(0, bytesRead));
        }

        [DllImport(Lz4DllName, EntryPoint = "LZ4_compress_default")]
        private static extern int LZ4Compress(byte* source, byte* destination, int sourceLength, int destinationCapacity);

        [DllImport(Lz4DllName, EntryPoint = "LZ4_decompress_safe")]
        private static extern int LZ4Decompress(byte* source, byte* destination, int compressedLength, int destinationCapacity);
    }
}
