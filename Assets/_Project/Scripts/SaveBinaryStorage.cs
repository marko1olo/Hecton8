using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
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
    internal static unsafe class AsyncWriteManager
    {
        internal struct ReadOnlyMapping
        {
            public NativeArray<byte> Bytes;
            public IntPtr View;
            public long Length;
        }

        private const long DiskFlushThrottleBytesPerSecond = 10L * 1024L * 1024L;
        private const int DiskFlushQueueCapacity = 32;
        private const int ReadWindowCount = 4;
        private const int ReadWindowSizeBytes = 1024 * 1024;
        private const int ReadWindowPrefetchThresholdBytes = 256 * 1024;
        private const int ReadPrefetchQueueCapacity = 16;
        private const int OsAllocationGranularityBytes = 64 * 1024;
        private const int FileWriteScratchBytes = 64 * 1024;
        private const int FileReadScratchBytes = 64 * 1024;

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
        private static readonly object s_readPrefetchLock = new object();
        private static readonly AutoResetEvent s_readPrefetchSignal = new AutoResetEvent(false);
        private static readonly ReadPrefetchRequest[] s_readPrefetchQueue = new ReadPrefetchRequest[ReadPrefetchQueueCapacity];
        private static int s_readPrefetchReadIndex;
        private static int s_readPrefetchWriteIndex;
        private static int s_readPrefetchCount;
        private static int s_readPrefetchWorkerStarted;
        private static readonly object s_fileWriteScratchLock = new object();
        private static readonly byte[] s_fileWriteScratch = new byte[FileWriteScratchBytes];
        private static readonly object s_fileReadScratchLock = new object();
        private static readonly byte[] s_fileReadScratch = new byte[FileReadScratchBytes];

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
        }

        private struct CachedReadWindow
        {
            public string AbsolutePath;
            public NativeArray<byte> Bytes;
            public byte* ViewPointer;
            public long WindowOffset;
            public long WindowLength;
            public long FileLength;
            public int LastUse;

            public bool IsCreated => Bytes.IsCreated;
        }

        internal static bool TryEnableSparseFile(FileStream fileStream)
        {
            return false;
        }

        internal static bool QueueThrottledFlush(string absolutePath, long byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Flush path is empty.";
                return false;
            }

            EnsureFlushThread();
            lock (s_flushLock)
            {
                if (s_flushCount == DiskFlushQueueCapacity)
                {
                    s_flushReadIndex = (s_flushReadIndex + 1) % DiskFlushQueueCapacity;
                    s_flushCount--;
                }

                s_flushQueue[s_flushWriteIndex] = new DiskFlushRequest
                {
                    AbsolutePath = absolutePath,
                    ByteCount = byteCount > 0L ? byteCount : 0L
                };
                s_flushWriteIndex = (s_flushWriteIndex + 1) % DiskFlushQueueCapacity;
                s_flushCount++;
            }

            s_flushSignal.Set();
            return true;
        }

        internal static void InvalidateCachedReadWindows(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return;

            lock (s_readWindowLock)
            {
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

                    if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationBytes + destinationCursor, byteCount - destinationCursor, window.ViewPointer + (int)sourceOffset, chunkBytes))
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

                        if (!UnsafeMemoryCopyGuard.TryMemCpy((byte*)destinationPtr + destinationCursor, destinationBytes - destinationCursor, window.ViewPointer + (int)sourceOffset, chunkBytesLong))
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

        private static void EnsureFlushThread()
        {
            if (Interlocked.CompareExchange(ref s_flushThreadStarted, 1, 0) != 0)
                return;

            Thread thread = new Thread(FlushWorkerLoop)
            {
                IsBackground = true,
                Name = "H8.SaveFlushQueue"
            };
            thread.Start();
        }

        private static void EnsureReadPrefetchWorker()
        {
            if (Interlocked.CompareExchange(ref s_readPrefetchWorkerStarted, 1, 0) != 0)
                return;

            Thread thread = new Thread(ReadPrefetchWorkerLoop)
            {
                IsBackground = true,
                Name = "H8.FileReadPrefetch"
            };
            thread.Start();
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
                FlushPath(request.AbsolutePath);
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

        private static void FlushPath(string absolutePath)
        {
            try
            {
                if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                    return;

                using FileStream stream = new FileStream(
                    absolutePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.WriteThrough);
                stream.Flush(true);
            }
            catch
            {
            }
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
                    if (FindCachedReadWindowLocked(request.AbsolutePath, request.WindowOffset, 1) >= 0)
                        continue;
                }

                if (!TryCreateCachedReadWindow(request.AbsolutePath, request.WindowOffset, 1, request.FileLength, out CachedReadWindow prefetchedWindow, out _))
                    continue;

                lock (s_readWindowLock)
                {
                    if (FindCachedReadWindowLocked(request.AbsolutePath, request.WindowOffset, 1) >= 0)
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

            int existingSlotIndex = FindCachedReadWindowLocked(absolutePath, byteOffset, byteCount);
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

        private static int FindCachedReadWindowLocked(string absolutePath, long byteOffset, int byteCount)
        {
            for (int i = 0; i < s_readWindows.Length; i++)
            {
                CachedReadWindow candidate = s_readWindows[i];
                if (!candidate.IsCreated ||
                    !string.Equals(candidate.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
                {
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

            FileStream fileStream = null;
            NativeArray<byte> windowBytes = default;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);
                // COLD ALLOC: NativeArray<byte>[windowLength] - portable cached save read window - owner: AsyncWriteManager
                windowBytes = new NativeArray<byte>((int)windowLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                byte* windowPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(windowBytes);
                if (!TryReadFileRangeToNativeBuffer(fileStream, windowOffset, windowPtr, (int)windowLength, out error))
                    return false;

                window = new CachedReadWindow
                {
                    AbsolutePath = absolutePath,
                    Bytes = windowBytes,
                    ViewPointer = windowPtr,
                    WindowOffset = windowOffset,
                    WindowLength = windowLength,
                    FileLength = fileLength,
                    LastUse = 0
                };

                windowBytes = default;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Cached file read-window load failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                if (windowBytes.IsCreated)
                    windowBytes.Dispose();

                fileStream?.Dispose();
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
                FindCachedReadWindowLocked(window.AbsolutePath, nextWindowOffset, 1) >= 0)
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
                    FileLength = window.FileLength
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
            if (window.Bytes.IsCreated)
                window.Bytes.Dispose();

            window = default;
        }

        internal static bool WriteAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            return WriteAll(absolutePath, buffer, byteCount, null, 0, out error);
        }

        internal static bool WriteAll(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native write path is empty.";
                return false;
            }

            int totalBytes = math.max(firstByteCount, 0) + math.max(secondByteCount, 0);
            if (totalBytes <= 0)
            {
                error = "Native write requested zero bytes.";
                return false;
            }

            FileStream fileStream = null;
            try
            {
                InvalidateCachedReadWindows(absolutePath);
                fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None, FileWriteScratchBytes, FileOptions.SequentialScan);
                TryEnableSparseFile(fileStream);
                fileStream.SetLength(totalBytes);

                if (!TryWritePointerSegment(fileStream, firstBuffer, firstByteCount, out error))
                    return false;

                if (!TryWritePointerSegment(fileStream, secondBuffer, secondByteCount, out error))
                    return false;

                if (!QueueThrottledFlush(absolutePath, totalBytes, out error))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                error = $"Sequential native write failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                fileStream?.Dispose();
            }
        }

        internal static bool OverwriteAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || buffer == null || byteCount <= 0)
            {
                error = "Native overwrite request is invalid.";
                return false;
            }

            FileStream fileStream = null;
            try
            {
                InvalidateCachedReadWindows(absolutePath);
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, FileWriteScratchBytes, FileOptions.SequentialScan);
                TryEnableSparseFile(fileStream);
                fileStream.SetLength(byteCount);
                fileStream.Position = 0L;

                if (!TryWritePointerSegment(fileStream, buffer, byteCount, out error))
                    return false;

                if (!QueueThrottledFlush(absolutePath, byteCount, out error))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                error = $"Sequential native overwrite failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                fileStream?.Dispose();
            }
        }

        private static bool TryWritePointerSegment(FileStream stream, void* source, int byteCount, out string error)
        {
            error = string.Empty;
            if (stream == null)
            {
                error = "Native write stream is invalid.";
                return false;
            }

            if (byteCount <= 0)
                return true;

            if (source == null)
            {
                stream.Position += byteCount;
                return true;
            }

            lock (s_fileWriteScratchLock)
            {
                byte* sourceBytes = (byte*)source;
                int writtenBytes = 0;
                while (writtenBytes < byteCount)
                {
                    int chunkBytes = byteCount - writtenBytes;
                    if (chunkBytes > s_fileWriteScratch.Length)
                        chunkBytes = s_fileWriteScratch.Length;

                    Marshal.Copy((IntPtr)(sourceBytes + writtenBytes), s_fileWriteScratch, 0, chunkBytes);
                    stream.Write(s_fileWriteScratch, 0, chunkBytes);
                    writtenBytes += chunkBytes;
                }
            }

            return true;
        }

        private static bool TryReadFileRangeToNativeBuffer(
            FileStream stream,
            long byteOffset,
            byte* destination,
            int byteCount,
            out string error)
        {
            error = string.Empty;
            if (stream == null || byteOffset < 0L || byteCount < 0)
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

            try
            {
                stream.Position = byteOffset;
                int totalRead = 0;
                lock (s_fileReadScratchLock)
                {
                    fixed (byte* scratchPtr = s_fileReadScratch)
                    {
                        while (totalRead < byteCount)
                        {
                            int chunkBytes = byteCount - totalRead;
                            if (chunkBytes > s_fileReadScratch.Length)
                                chunkBytes = s_fileReadScratch.Length;

                            int read = stream.Read(s_fileReadScratch, 0, chunkBytes);
                            if (read <= 0)
                            {
                                error = "Native file read ended before the requested byte count.";
                                return false;
                            }

                            if (!UnsafeMemoryCopyGuard.TryMemCpy(destination + totalRead, byteCount - totalRead, scratchPtr, read))
                            {
                                error = "Native file read copy exceeded destination bounds.";
                                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(AsyncWriteManager));
                                return false;
                            }

                            totalRead += read;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Native file read failed: {ex.Message}";
                return false;
            }
        }

        internal static bool TryReadAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || buffer == null || byteCount <= 0)
            {
                error = "Native read request is invalid.";
                return false;
            }

            return TryCopyFromCachedReadWindow(absolutePath, 0L, buffer, byteCount, out error);
        }

        internal static bool TryCopyFileRangeToNativeArray(
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

        internal static bool TryGetFileLength(string absolutePath, out long fileLength, out string error)
        {
            fileLength = 0L;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native read path is empty.";
                return false;
            }

            try
            {
                fileLength = new FileInfo(absolutePath).Length;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to read file length for '{absolutePath}': {ex.Message}";
                return false;
            }
        }

        internal static bool TryOpenReadOnlyMapping(string absolutePath, out ReadOnlyMapping mapping, out string error)
        {
            mapping = default;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native read path is empty.";
                return false;
            }

            FileStream fileStream = null;
            NativeArray<byte> fileBytes = default;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = fileStream.Length;

                if (fileLength <= 0L)
                {
                    error = $"Portable read requested an empty file for '{absolutePath}'.";
                    return false;
                }

                if (fileLength > int.MaxValue)
                {
                    error = $"Portable read exceeds the supported native buffer range for '{absolutePath}'.";
                    return false;
                }

                // COLD ALLOC: NativeArray<byte>[fileLength] - portable read-only save snapshot - owner: AsyncWriteManager
                fileBytes = new NativeArray<byte>((int)fileLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                if (!TryReadFileRangeToNativeBuffer(fileStream, 0L, filePtr, (int)fileLength, out error))
                    return false;

                mapping = new ReadOnlyMapping
                {
                    Bytes = fileBytes,
                    View = (IntPtr)filePtr,
                    Length = fileLength
                };

                fileBytes = default;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Portable read-only open failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                if (fileBytes.IsCreated)
                    fileBytes.Dispose();

                fileStream?.Dispose();
            }
        }

        internal static void CloseReadOnlyMapping(ref ReadOnlyMapping mapping)
        {
            if (mapping.Bytes.IsCreated)
                mapping.Bytes.Dispose();

            mapping = default;
        }
    }
    internal static unsafe class SaveBinaryStorage
    {
        internal const uint Magic = 0x48454354u;
        internal const int ErrorCodeNone = 0;
        internal const int ErrorCodeHeaderReadFailed = unchecked((int)0x48385244);
        internal const int ErrorCodeMagicMismatch = unchecked((int)0x48384D47);
        internal const ushort CurrentVersion = 0x0009;
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
        internal const int BlockSizeBytes = 256 * 1024;
        internal const int RawPayloadCapacityBytes = 64 * 1024 * 1024;
        internal const int MaxCompressedPayloadBytes = 68 * 1024 * 1024;

        private const long UnixEpochTicks = 621355968000000000L;
        private const int PayloadPrefixSizeBytes = SaveDataMigration_AupV8.CurrentPayloadPrefixSizeBytes;
        private const int PackedQuestStateSectionHeaderSize = 64;
        private const int PersistentWorldSectionHeaderSize = 12;
        private const int EcosystemSectionHeaderSize = 4;
        private const int SaveFileHeaderPrefixSize = 8;
        private const int LegacyHeaderHashSizeBytes = 36;
        private const int IndexedHeaderV8Size = 52;
        private const int IndexedHeaderV8HashSizeBytes = 44;
        private const int CurrentHeaderHashSizeBytes = 48;
        private const ushort First64BitHashVersion = 0x0004;
        private const ushort CompactPersistentWorldSectionVersion = 0x0005;
        private const ushort EcosystemSectionVersion = 0x0006;
        private const ushort TokenizedPayloadVersion = 0x0007;
        private const ushort IndexedBlockStorageVersion = 0x0008;
        private const ushort HeaderChecksumVersion = 0x0009;
        private const int TokenizedPayloadHeaderSize = 8;
        private const int TokenBlockSizeBytes = 16;
        private const int MaxTokenCount = 64;
        private const byte TokenEscapeMarker = 0xFF;
        private const int IndexedSectorDirectoryHeaderSize = 16;
        private const int IndexedSectorBlockHeaderSize = 8;
        private const int IndexedSectorDirectorySlotCount = 4096;
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
        private const int ManagedCompressionScratchBytes = 64 * 1024;
        internal const int IndexedSectorQuarantineHashCapacity = 16;
        private const string Lz4DllName = "liblz4";
        private const string NativeMemoryOwner = nameof(SaveBinaryStorage);
        private const string EntityStateWriteSourceStatesLabel = "indexedSectorEntityStateSourceStates";
        private const string EntityStateWriteSortEntriesLabel = "indexedSectorEntityStateSortEntries";
        private const string EntityStateWriteRadixScratchLabel = "indexedSectorEntityStateRadixScratch";
        private const string EntityStateWriteSortedStatesLabel = "indexedSectorEntityStateSortedStates";
        private const string EntityStateWriteFileBytesLabel = "indexedSectorEntityStateFileBytes";
        private const string EntityStateWriteResultLengthLabel = "indexedSectorEntityStateResultLength";
        private const string EntityStateWriteRadixCountsLabel = "indexedSectorEntityStateRadixCounts";
        private const string EntityStateWriteRadixOffsetsLabel = "indexedSectorEntityStateRadixOffsets";
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
        // COLD ALLOC: long[16] - quarantined indexed sector reset queue - owner: SaveBinaryStorage
        private static readonly long[] s_indexedSectorQuarantineHashes = new long[IndexedSectorQuarantineHashCapacity];
        // COLD ALLOC: object[1] - quarantine hash queue lock for background paging workers - owner: SaveBinaryStorage
        private static readonly object s_indexedSectorQuarantineLock = new object();
        // COLD ALLOC: object[1] - serialized managed compression fallback scratch guard - owner: SaveBinaryStorage
        private static readonly object s_managedCompressionLock = new object();
        // COLD ALLOC: byte[65536] - managed Deflate fallback stream copy scratch for non-Windows builds without liblz4 - owner: SaveBinaryStorage
        private static readonly byte[] s_managedCompressionScratch = new byte[ManagedCompressionScratchBytes];
        private static int s_indexedSectorQuarantineHashCount;
        private static readonly uint _indexedSectorQuarantineWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.IndexedSectorQuarantine"));
        private static int s_lastReadErrorCode;

        internal static int LastReadErrorCode => Volatile.Read(ref s_lastReadErrorCode);

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = TokenizedPayloadHeaderSize)]
        private struct TokenizedPayloadHeader
        {
            public uint ExpandedPayloadLength;
            public ushort TokenCount;
            public ushort Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
        private struct IndexedSectorGroup
        {
            public long SectorHash;
            public int DirectorySlot;
            public int StartIndex;
            public int Count;
            public int WriteCount;
            public int Reserved0;
            public int Reserved1;
        }

        private struct IndexedSectorGroupBuffer : IDisposable
        {
            public NativeList<IndexedSectorGroup> Groups;
            public NativeList<PersistentWorldDeltaRecord> Records;

            public int Count => Groups.IsCreated ? Groups.Length : 0;

            public void Dispose()
            {
                if (Groups.IsCreated)
                    Groups.Dispose();
                if (Records.IsCreated)
                    Records.Dispose();
                this = default;
            }
        }

        private readonly struct IndexedSectorCommitTarget
        {
            public readonly bool ReusedExistingSlot;
            public readonly bool InsertedNewSlot;
            public readonly int SlotIndex;
            public readonly long WriteOffset;
            public readonly long NewFileLength;

            public IndexedSectorCommitTarget(bool reusedExistingSlot, bool insertedNewSlot, int slotIndex, long writeOffset, long newFileLength)
            {
                ReusedExistingSlot = reusedExistingSlot;
                InsertedNewSlot = insertedNewSlot;
                SlotIndex = slotIndex;
                WriteOffset = writeOffset;
                NewFileLength = newFileLength;
            }
        }

        internal struct IndexedSectorEntityStateWriteHandle
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

            internal void RegisterNativeMemorySentinel()
            {
                RegisterArray(SourceStates, EntityStateWriteSourceStatesLabel);
                RegisterArray(SortEntries, EntityStateWriteSortEntriesLabel);
                RegisterArray(RadixScratch, EntityStateWriteRadixScratchLabel);
                RegisterArray(SortedEntityStates, EntityStateWriteSortedStatesLabel);
                RegisterArray(CompactStates, EntityStateWriteCompactStatesLabel);
                RegisterArray(FileBytes, EntityStateWriteFileBytesLabel);
                RegisterArray(ResultLength, EntityStateWriteResultLengthLabel);
                RegisterArray(RadixCounts, EntityStateWriteRadixCountsLabel);
                RegisterArray(RadixOffsets, EntityStateWriteRadixOffsetsLabel);
            }

            private static void RegisterArray<T>(NativeArray<T> array, string label) where T : struct
            {
                if (!array.IsCreated)
                    return;

                NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            }

            private void UnregisterNativeMemorySentinel()
            {
                UnregisterArray(SourceStates);
                UnregisterArray(SortEntries);
                UnregisterArray(RadixScratch);
                UnregisterArray(SortedEntityStates);
                UnregisterArray(CompactStates);
                UnregisterArray(FileBytes);
                UnregisterArray(ResultLength);
                UnregisterArray(RadixCounts);
                UnregisterArray(RadixOffsets);
            }

            private static void UnregisterArray<T>(NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeArray(array);
            }

            internal void Dispose()
            {
                UnregisterNativeMemorySentinel();

                if (SourceStates.IsCreated)
                    SourceStates.Dispose();
                if (SortEntries.IsCreated)
                    SortEntries.Dispose();
                if (RadixScratch.IsCreated)
                    RadixScratch.Dispose();
                if (SortedEntityStates.IsCreated)
                    SortedEntityStates.Dispose();
                if (CompactStates.IsCreated)
                    CompactStates.Dispose();
                if (FileBytes.IsCreated)
                    FileBytes.Dispose();
                if (ResultLength.IsCreated)
                    ResultLength.Dispose();
                if (RadixCounts.IsCreated)
                    RadixCounts.Dispose();
                if (RadixOffsets.IsCreated)
                    RadixOffsets.Dispose();

                this = default;
            }

            internal JobHandle DisposeDeferred(JobHandle dependency)
            {
                UnregisterNativeMemorySentinel();

                JobHandle disposeHandle = JobHandle.CombineDependencies(Handle, dependency);
                if (SourceStates.IsCreated)
                    disposeHandle = SourceStates.Dispose(disposeHandle);
                if (SortEntries.IsCreated)
                    disposeHandle = SortEntries.Dispose(disposeHandle);
                if (RadixScratch.IsCreated)
                    disposeHandle = RadixScratch.Dispose(disposeHandle);
                if (SortedEntityStates.IsCreated)
                    disposeHandle = SortedEntityStates.Dispose(disposeHandle);
                if (CompactStates.IsCreated)
                    disposeHandle = CompactStates.Dispose(disposeHandle);
                if (FileBytes.IsCreated)
                    disposeHandle = FileBytes.Dispose(disposeHandle);
                if (ResultLength.IsCreated)
                    disposeHandle = ResultLength.Dispose(disposeHandle);
                if (RadixCounts.IsCreated)
                    disposeHandle = RadixCounts.Dispose(disposeHandle);
                if (RadixOffsets.IsCreated)
                    disposeHandle = RadixOffsets.Dispose(disposeHandle);

                this = default;
                return disposeHandle;
            }
        }

        internal struct SectorEntityStateSortEntry
        {
            public ulong SortKey;
            public EntityDataRecord Record;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        internal struct SectorCompactEntityStateRecord16
        {
            public uint PackedSectorPosition;
            public uint InstanceUid;
            public ushort Quantity;
            public ushort PackedAux;
            public uint PackedState;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 6)]
        internal struct QuantizedAupLocalOffsetShort3
        {
            public short XMillimeters;
            public short YMillimeters;
            public short ZMillimeters;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildSectorEntityStateSortEntriesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityDataRecord> SourceStates;
            public NativeArray<SectorEntityStateSortEntry> Entries;
            public int ChunkSizeMeters;

            public void Execute(int index)
            {
                EntityDataRecord record = SourceStates[index];
                AbsoluteUniversePositionBlit128 alignedPosition = record.Position;
                AbsoluteUniversePosition position = new AbsoluteUniversePosition
                {
                    GridX = alignedPosition.GridX,
                    GridY = alignedPosition.GridY,
                    GridZ = alignedPosition.GridZ,
                    LocalX = alignedPosition.Local.x,
                    LocalY = alignedPosition.Local.y,
                    LocalZ = alignedPosition.Local.z
                };

                Entries[index] = new SectorEntityStateSortEntry
                {
                    SortKey = SaveBinaryPayloadCodec.BuildSectorEntitySpatialSortKey(in position, ChunkSizeMeters),
                    Record = record
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ExtractSortedSectorEntityStatesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorEntityStateSortEntry> Entries;
            [WriteOnly] public NativeArray<EntityDataRecord> SortedStates;

            public void Execute(int index)
            {
                SortedStates[index] = Entries[index].Record;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            double safeChunkSize = math.max(1, chunkSizeMeters);
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
            double safeChunkSize = math.max(1, chunkSizeMeters);
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
            double3 absolute = position.ToAbsoluteDouble3();
            int2 sector = UnpackSectorHash(sectorHash);
            double sectorOriginX = (double)sector.x * PersistentWorldSectorEdgeLengthMeters;
            double sectorOriginZ = (double)sector.y * PersistentWorldSectorEdgeLengthMeters;
            float x01 = math.saturate((float)((absolute.x - sectorOriginX) / PersistentWorldSectorEdgeLengthMeters));
            float z01 = math.saturate((float)((absolute.z - sectorOriginZ) / PersistentWorldSectorEdgeLengthMeters));
            float y01 = math.saturate(((float)absolute.y - SectorCompactDepthMinMeters) / (SectorCompactDepthMaxMeters - SectorCompactDepthMinMeters));

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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = IndexedSectorDirectoryHeaderSize)]
        private struct IndexedSectorDirectoryHeader
        {
            public uint SectorCount;
            public int ChunkSizeMeters;
            public int MetadataCompressedSize;
            public int MetadataDecompressedSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = IndexedSectorBlockHeaderSize)]
        private struct IndexedSectorBlockHeader
        {
            public uint Flags;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = ProtectedCompressedBlockHeaderBytes)]
        private struct ProtectedCompressedBlockHeader
        {
            public int CompressedLength;
            public int RawLength;
            public uint RawChecksumLow32;
            public uint Magic;
            public ulong Reserved0;
            public ulong Reserved1;
            public ulong Reserved2;
            public ulong Reserved3;
            public ulong Reserved4;
            public ulong Reserved5;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]
        internal struct SectorEntry
        {
            public long SectorHash;
            public long ByteOffset;
            public int CompressedSize;
            public int DecompressedSize;
            public uint Checksum;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
        private struct SectorOverrideFileHeader
        {
            public long SectorHash;
            public int CompressedSize;
            public int DecompressedSize;
            public uint Checksum;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
        private struct SectorEntityStateFileHeader
        {
            public long SectorHash;
            public int CompressedSize;
            public int DecompressedSize;
            public uint RecordCount;
            public uint Checksum;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = ModPayloadHeaderSizeBytes)]
        private struct ModPayloadSubSectorHeader
        {
            public uint Magic;
            public ushort Version;
            public ushort HeaderSize;
            public uint ModHash;
            public ushort PayloadLength;
            public ushort Flags;
            public long PagedSectorHash;
            public uint PayloadChecksum;
            public uint Reserved;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SaveFileHeaderPrefixSize)]
        private struct SaveFileHeaderPrefix
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = CurrentHeaderSize)]
        internal struct SaveFileHeader
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
            public ulong TimestampUnixMs;
            public uint Checksum;
            public uint DeltaCount;
            public uint EntityCount;
            public uint PlayerOffset;
            public uint DeltaOffset;
            public uint EntityOffset;
            public ulong HashPayload64;
            public ulong HashHeader64;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = IndexedHeaderV8Size)]
        private struct IndexedSaveFileHeaderV8
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
            public ulong TimestampUnixMs;
            public uint DeltaCount;
            public uint EntityCount;
            public uint PlayerOffset;
            public uint DeltaOffset;
            public uint EntityOffset;
            public ulong HashPayload64;
            public ulong HashHeader64;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
        private struct LegacyIndexedChecksumLeaf
        {
            public long SectorHash;
            public uint Checksum;
            public int CompressedSize;
            public int DecompressedSize;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]
        internal struct SaveCloudMetadataHeader
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
            public uint Checksum;
            public ulong TimestampUnixMs;
            public ulong HashPayload64;
            public ulong HashHeader64;
            public uint PlayerOffset;
            public uint DeltaOffset;
            public uint EntityOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = LegacyHeaderSize)]
        private struct LegacySaveFileHeader
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
            public ulong TimestampUnixMs;
            public uint DeltaCount;
            public uint EntityCount;
            public uint PlayerOffset;
            public uint DeltaOffset;
            public uint EntityOffset;
            public uint HashHeader32;
            public uint HashPayload32;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
        public struct DeltaCell
        {
            /// <summary>
            /// Packed universe-space cell key.
            /// </summary>
            public ulong UniverseKey;

            /// <summary>
            /// Signed-distance delta in meters.
            /// </summary>
            public float SdfValue;

            /// <summary>
            /// Material palette index.
            /// </summary>
            public byte MaterialId;

            /// <summary>
            /// Packed per-cell state flags.
            /// </summary>
            public byte Flags;

            /// <summary>
            /// Material-specific metadata payload.
            /// </summary>
            public ushort Metadata;

            /// <summary>
            /// Reserved expansion bytes required by the current 20-byte mandate.
            /// </summary>
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = PayloadPrefixSizeBytes)]
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = PersistentWorldSectionHeaderSize)]
        private struct PersistentWorldSectionHeader
        {
            public uint ChunkCount;
            public uint ItemHashCount;
            public uint RecordCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = EcosystemSectionHeaderSize)]
        private struct EcosystemSectionHeader
        {
            public uint RecordCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        private struct PersistentWorldSaveRecord16
        {
            public uint PackedLocalPosition;
            public uint InstanceUid;
            public ushort Quantity;
            public byte ItemFlags;
            public byte Reserved;
            public ushort ChunkIndex;
            public ushort ItemHashIndex;
        }

        internal static void WarmRuntime()
        {
            byte value = 0;
            _ = xxHash3.Hash64(&value, 1L);
            Interlocked.Exchange(ref s_indexedSectorQuarantineReported, 0);
            lock (s_indexedSectorQuarantineLock)
            {
                Array.Clear(s_indexedSectorQuarantineHashes, 0, s_indexedSectorQuarantineHashes.Length);
                s_indexedSectorQuarantineHashCount = 0;
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
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
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

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
                Checksum = FormatPayloadChecksum(in header)
            };

            error = string.Empty;
            return true;
        }

        private static bool TryWriteSaveFileIndexedV8(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
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

            string sceneName = string.IsNullOrEmpty(metadata.SceneName) ? "Unknown" : metadata.SceneName;
            string gameVersion = string.IsNullOrEmpty(metadata.GameVersion) ? Application.version : metadata.GameVersion;
            int sceneBytesLength = checked(sceneName.Length * sizeof(char));
            int versionBytesLength = checked(gameVersion.Length * sizeof(char));
            if (sceneBytesLength > ushort.MaxValue || versionBytesLength > ushort.MaxValue)
            {
                error = "Save metadata strings exceed the payload prefix limits.";
                return false;
            }

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);
            UnsafeUtility.MemClear(rawPtr, rawBuffer.Length);
            UnsafeUtility.MemClear(filePtr, compressedBuffer.Length);

            int packedQuestWordCount = packedQuestStateWords.IsCreated ? packedQuestStateWords.Length : 0;
            int ecosystemSectorCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
            int voxelDeltaByteLength = voxelDeltaSnapshot.IsCreated ? voxelDeltaSnapshot.Length : 0;
            int packedQuestSectionLength = packedQuestWordCount > 0
                ? PackedQuestStateSectionHeaderSize + (packedQuestWordCount * UnsafeUtility.SizeOf<uint>())
                : 0;
            int ecosystemSectionLength = ComputeEcosystemSectionLength(ecosystemSectorCount);

            int metadataCursor = PayloadPrefixSizeBytes + sceneBytesLength + versionBytesLength;
            if (!SaveBinaryPayloadCodec.TryWrite(data, AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, out int saveDataByteLength, out error))
                return false;

            ulong timestampUnixMs = ToUnixMilliseconds(metadata.Timestamp);
            PayloadPrefix prefix = new PayloadPrefix
            {
                TimestampUnixMs = timestampUnixMs,
                PlayTimeSeconds = metadata.PlayTimeSeconds,
                PlayerPosition = ToAup(metadata.PlayerPosition),
                SaveDataVersion = math.max(data.version, 0),
                SaveDataByteLength = (uint)saveDataByteLength,
                SceneNameByteLength = (ushort)sceneBytesLength,
                GameVersionByteLength = (ushort)versionBytesLength
            };

            UnsafeUtility.CopyStructureToPtr(ref prefix, rawPtr);
            metadataCursor = PayloadPrefixSizeBytes;
            CopyUtf16StringToUnmanaged(sceneName, AddByteOffset(rawPtr, metadataCursor), sceneBytesLength);
            metadataCursor += sceneBytesLength;
            CopyUtf16StringToUnmanaged(gameVersion, AddByteOffset(rawPtr, metadataCursor), versionBytesLength);
            metadataCursor += versionBytesLength;
            metadataCursor += saveDataByteLength;
            int packedQuestOffsetInMetadataPayload = metadataCursor;

            if (packedQuestWordCount > 0)
            {
                QuestSaveHeader serializedQuestHeader = packedQuestHeader;
                serializedQuestHeader.Magic = QuestSaveHeader.HeaderMagic;
                serializedQuestHeader.FlagCount = (uint)packedQuestWordCount;
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
            WriteEcosystemSection(AddByteOffset(rawPtr, metadataCursor), ecosystemSectorStates);
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
            int sectorCount = sectorGroups.Count;
            if (sectorCount > IndexedSectorDirectorySlotCount)
            {
                long overflowSectorHash = sectorGroups.Groups[IndexedSectorDirectorySlotCount].SectorHash;
                ReportIndexedSectorDirectoryCapacityExceeded(overflowSectorHash, sectorCount);
                sectorCount = IndexedSectorDirectorySlotCount;
            }

            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * UnsafeUtility.SizeOf<SectorEntry>());
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

            using NativeArray<SectorEntry> sectorEntries = new NativeArray<SectorEntry>(
                IndexedSectorDirectorySlotCount,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            int totalEntityCount = CountIndexedSectorRecords(sectorGroups, sectorCount);
            NativeParallelHashMap<int3, ushort> persistentWorldChunkLookup = default;
            NativeList<int3> persistentWorldChunkTable = default;
            NativeParallelHashMap<ulong, ushort> persistentWorldItemHashLookup = default;
            NativeList<ulong> persistentWorldItemHashTable = default;

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
                            out error))
                    {
                        return false;
                    }

                    int sectorRawLength = ComputePersistentWorldSectionLength(recordCount, persistentWorldChunkTable.Length, persistentWorldItemHashTable.Length);
                    UnsafeUtility.MemClear(rawPtr, sectorRawLength);
                    WritePersistentWorldSection(
                        rawPtr,
                        sectorGroups.Records,
                        group.StartIndex,
                        recordCount,
                        persistentWorldChunkLookup,
                        persistentWorldChunkTable,
                        persistentWorldItemHashLookup,
                        persistentWorldItemHashTable);

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

                    if (persistentWorldItemHashTable.IsCreated)
                        persistentWorldItemHashTable.Dispose();
                    if (persistentWorldItemHashLookup.IsCreated)
                        persistentWorldItemHashLookup.Dispose();
                    if (persistentWorldChunkTable.IsCreated)
                        persistentWorldChunkTable.Dispose();
                    if (persistentWorldChunkLookup.IsCreated)
                        persistentWorldChunkLookup.Dispose();

                    persistentWorldItemHashTable = default;
                    persistentWorldItemHashLookup = default;
                    persistentWorldChunkTable = default;
                    persistentWorldChunkLookup = default;
                }
            }
            finally
            {
                if (persistentWorldItemHashTable.IsCreated)
                    persistentWorldItemHashTable.Dispose();
                if (persistentWorldItemHashLookup.IsCreated)
                    persistentWorldItemHashLookup.Dispose();
                if (persistentWorldChunkTable.IsCreated)
                    persistentWorldChunkTable.Dispose();
                if (persistentWorldChunkLookup.IsCreated)
                    persistentWorldChunkLookup.Dispose();
                sectorGroups.Dispose();
            }

            IndexedSectorDirectoryHeader directoryHeader = new IndexedSectorDirectoryHeader
            {
                SectorCount = (uint)sectorCount,
                ChunkSizeMeters = math.max(1, chunkSizeMeters),
                MetadataCompressedSize = metadataCompressedSize,
                MetadataDecompressedSize = metadataRawLength
            };
            UnsafeUtility.CopyStructureToPtr(ref directoryHeader, filePtr + CurrentHeaderSize);

            int directoryCursor = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize;
            for (int i = 0; i < sectorEntries.Length; i++)
            {
                SectorEntry entry = sectorEntries[i];
                UnsafeUtility.CopyStructureToPtr(ref entry, filePtr + directoryCursor);
                directoryCursor += UnsafeUtility.SizeOf<SectorEntry>();
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
                DeltaCount = (uint)packedQuestWordCount,
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

            if (!AsyncWriteManager.WriteAll(absolutePath, filePtr, fileCursor, out error))
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
                EntityOffset = header.EntityOffset
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
            int sectorCount = checked((int)directoryHeader.SectorCount);
            int safeChunkSizeMeters = math.max(1, directoryHeader.ChunkSizeMeters);
            directoryHeader.ChunkSizeMeters = safeChunkSizeMeters;

            if (sectorCount < 0 || sectorCount > IndexedSectorDirectorySlotCount)
            {
                error = $"Indexed sector directory count {sectorCount} exceeded slot capacity {IndexedSectorDirectorySlotCount}.";
                return false;
            }

            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * UnsafeUtility.SizeOf<SectorEntry>());
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
                SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
                if (IsIndexedSectorEntryPopulated(in entry) &&
                    !IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                {
                    error = "Indexed sector entry exceeded the file bounds.";
                    return false;
                }

                sectorEntries[i] = entry;
                if (IsIndexedSectorEntryPopulated(in entry))
                    populatedCount++;
                entryCursor += UnsafeUtility.SizeOf<SectorEntry>();
            }

            if (populatedCount != sectorCount)
            {
                error = "Indexed sector directory count mismatch.";
                return false;
            }

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

        private static int CountIndexedSectorRecords(in IndexedSectorGroupBuffer sectorGroups, int sectorCount)
        {
            if (!sectorGroups.Groups.IsCreated || sectorCount <= 0)
                return 0;

            int safeCount = math.min(sectorCount, sectorGroups.Count);
            int total = 0;
            for (int i = 0; i < safeCount; i++)
            {
                IndexedSectorGroup group = sectorGroups.Groups[i];
                total = checked(total + group.Count);
            }

            return total;
        }

        private static void ReportIndexedSectorDirectoryCapacityExceeded(long sectorHash, int attemptedSectorCount)
        {
            CrashTelemetryBuffer.ReportSaveSystemCriticalFault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
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

            if (sectionPtr == null || sectionLength < PersistentWorldSectionHeaderSize)
            {
                error = "Indexed persistent-world sector section is truncated.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(sectionPtr, 0);
            int recordCount = checked((int)sectionHeader.RecordCount);
            int chunkCount = checked((int)sectionHeader.ChunkCount);
            int itemHashCount = checked((int)sectionHeader.ItemHashCount);
            int expectedLength = ComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount);
            if (expectedLength != sectionLength)
            {
                error = "Indexed persistent-world sector length does not match its compact header.";
                return false;
            }

            persistentWorldDeltas = recordCount > 0 ? new PersistentWorldDeltaRecord[recordCount] : Array.Empty<PersistentWorldDeltaRecord>();
            if (recordCount <= 0)
                return true;

            int cursor = PersistentWorldSectionHeaderSize;
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
            if (!TryComputeIndexedChecksumRootFromMappedDirectory(
                    (byte*)mapping.View,
                    directoryEntryCursor,
                    metadataEndOffset,
                    mapping.Length,
                    checked((int)directoryHeader.SectorCount),
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

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
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
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
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
            voxelDeltaSnapshot = default;
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

            int saveDataLength = checked((int)prefix.SaveDataByteLength);
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
            if (header.DeltaCount > 0)
            {
                if (!IsByteRangeWithin(payloadCursor, PackedQuestStateSectionHeaderSize, metadataRawLength))
                {
                    error = "Indexed packed quest section header is truncated.";
                    return false;
                }

                packedQuestHeader = UnsafeUtility.ReadArrayElement<QuestSaveHeader>(AddByteOffset(rawPtr, payloadCursor), 0);
                if (packedQuestHeader.Magic != QuestSaveHeader.HeaderMagic)
                {
                    error = "Indexed packed quest section magic mismatch.";
                    return false;
                }

                if (packedQuestHeader.FlagCount != header.DeltaCount)
                {
                    error = "Indexed packed quest section count mismatch.";
                    return false;
                }

                packedQuestStateWords = new uint[packedQuestHeader.FlagCount];
                payloadCursor += PackedQuestStateSectionHeaderSize;
                if (packedQuestHeader.FlagCount > 0)
                {
                    fixed (uint* destinationPtr = packedQuestStateWords)
                    {
                        int packedQuestBytes = checked((int)packedQuestHeader.FlagCount) * UnsafeUtility.SizeOf<uint>();
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

                payloadCursor += checked((int)packedQuestHeader.FlagCount) * UnsafeUtility.SizeOf<uint>();
            }
            else
            {
                packedQuestHeader = default;
                packedQuestStateWords = Array.Empty<uint>();
            }

            if (!IsByteRangeWithin(payloadCursor, EcosystemSectionHeaderSize, metadataRawLength))
            {
                error = "Indexed ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader ecosystemHeader = UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(AddByteOffset(rawPtr, payloadCursor), 0);
            int ecosystemRecordCount = checked((int)ecosystemHeader.RecordCount);
            int ecosystemSectionLength = ComputeEcosystemSectionLength(ecosystemRecordCount);
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
                    if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, ecosystemSectorStates.Length * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>(), AddByteOffset(rawPtr, payloadCursor + EcosystemSectionHeaderSize), ecosystemBytes))
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
                voxelDeltaSnapshot = new NativeArray<byte>(voxelByteLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                void* voxelDestinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
                if (!UnsafeMemoryCopyGuard.SafeCopy(voxelDestinationPtr, voxelDeltaSnapshot.Length, AddByteOffset(rawPtr, payloadCursor), voxelByteLength))
                {
                    error = "Indexed voxel delta snapshot copy exceeded destination bounds.";
                    return false;
                }
            }

            NativeList<PersistentWorldDeltaRecord> aggregatedWorldDeltas = new NativeList<PersistentWorldDeltaRecord>(256, Allocator.Temp);
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

                        using NativeArray<byte> sectorRaw = new NativeArray<byte>(entry.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
                if (aggregatedWorldDeltas.IsCreated)
                    aggregatedWorldDeltas.Dispose();
            }

            rawPayloadLength = metadataRawLength;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
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

            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                long fileLength = fileStream.Length;
                if (selectedEntry.ByteOffset < 0L || selectedEntry.ByteOffset > fileLength - IndexedSectorBlockHeaderSize)
                {
                    error = "Smoke corruption block header offset exceeded the file length.";
                    return false;
                }

                byte[] blockHeaderBytes = new byte[IndexedSectorBlockHeaderSize];
                fileStream.Position = selectedEntry.ByteOffset;
                int readBytes = 0;
                while (readBytes < blockHeaderBytes.Length)
                {
                    int justRead = fileStream.Read(blockHeaderBytes, readBytes, blockHeaderBytes.Length - readBytes);
                    if (justRead <= 0)
                    {
                        error = "Smoke corruption block header read ended early.";
                        return false;
                    }

                    readBytes += justRead;
                }

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

                fileStream.Position = payloadOffset;
                int storedByte = fileStream.ReadByte();
                if (storedByte < 0)
                {
                    error = "Smoke corruption payload read ended early.";
                    return false;
                }

                fileStream.Position = payloadOffset;
                fileStream.WriteByte((byte)(storedByte ^ 0x5Au));
                fileStream.Flush();
                AsyncWriteManager.QueueThrottledFlush(absolutePath, fileLength, out _);
                sectorHash = selectedEntry.SectorHash;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Smoke corruption failed: {ex.Message}";
                return false;
            }
            finally
            {
                fileStream?.Dispose();
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
                if (!TryReadIndexedDirectory(in header, ref mapping, out _, out SectorEntry[] sectorEntries, out error))
                    return false;

                for (int requestedIndex = 0; requestedIndex < desiredSectorHashes.Length; requestedIndex++)
                {
                    long desiredSectorHash = desiredSectorHashes[requestedIndex];
                    if (desiredSectorHash == long.MinValue)
                        continue;

                    if (!TryFindIndexedSectorEntryIndex(sectorEntries, desiredSectorHash, out int sectorEntryIndex))
                        continue;

                    SectorEntry entry = sectorEntries[sectorEntryIndex];
                    if (!TryLoadIndexedPersistentWorldSectorRecordsCore(ref mapping, in entry, destination, out string sectorError))
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
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;

            using NativeArray<byte> sectorRaw = new NativeArray<byte>(entry.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
                if (!TryReadIndexedDirectory(in backupHeader, ref backupMapping, out _, out SectorEntry[] backupEntries, out error))
                    return false;

                if (!TryFindIndexedSectorEntryIndex(backupEntries, desiredSectorHash, out int backupSectorEntryIndex))
                {
                    error = $"Indexed sector 0x{desiredSectorHash:X16} is missing from backup save.";
                    return false;
                }

                SectorEntry backupEntry = backupEntries[backupSectorEntryIndex];
                return TryLoadIndexedPersistentWorldSectorRecordsCore(ref backupMapping, in backupEntry, destination, out error);
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

            NativeList<PersistentWorldDeltaRecord> backupRecords = new NativeList<PersistentWorldDeltaRecord>(16, Allocator.Temp);
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
                if (backupRecords.IsCreated)
                    backupRecords.Dispose();
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
            NativeArray<PersistentWorldDeltaRecord> emptySectorRecords =
                new NativeArray<PersistentWorldDeltaRecord>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                NativeMemorySentinel.RegisterNativeArray(
                    emptySectorRecords,
                    NativeMemoryOwner,
                    PristineResetEmptySectorRecordsLabel,
                    NativeAllocationLifetime.TransientArena);

                if (!TryWriteIndexedPersistentWorldSectorOverride(tempOverridePath, sectorHash, emptySectorRecords, chunkSizeMeters, out error))
                    return false;

                if (TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error))
                    return true;
            }
            finally
            {
                if (emptySectorRecords.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(emptySectorRecords);
                    emptySectorRecords.Dispose();
                }
            }

            try
            {
                if (File.Exists(tempOverridePath))
                    File.Delete(tempOverridePath);
            }
            catch
            {
            }

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

            if (!TryBuildPersistentWorldSectionTables(
                    sectorRecords,
                    out NativeParallelHashMap<int3, ushort> chunkLookup,
                    out NativeList<int3> chunkTable,
                    out NativeParallelHashMap<ulong, ushort> itemHashLookup,
                    out NativeList<ulong> itemHashTable,
                    out error))
            {
                return false;
            }

            try
            {
                int rawSectionLength = ComputePersistentWorldSectionLength(sectorRecords.Length, chunkTable.Length, itemHashTable.Length);
                int blockCount = ResolveProtectedLz4BlockCount(rawSectionLength);
                int compressedCapacity = rawSectionLength + (rawSectionLength / 255) + 16 + (blockCount * ProtectedCompressedBlockHeaderBytes) + IndexedSectorBlockHeaderSize + 32;
                int fileCapacity = UnsafeUtility.SizeOf<SectorOverrideFileHeader>() + compressedCapacity;

                using NativeArray<byte> rawSectionBytes = new NativeArray<byte>(rawSectionLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                using NativeArray<byte> fileBytes = new NativeArray<byte>(fileCapacity, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* rawSectionPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawSectionBytes);
                byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                WritePersistentWorldSection(rawSectionPtr, sectorRecords, chunkLookup, chunkTable, itemHashLookup, itemHashTable);
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
                return AsyncWriteManager.WriteAll(absolutePath, filePtr, fileCursor, out error);
            }
            finally
            {
                if (chunkLookup.IsCreated)
                    chunkLookup.Dispose();
                if (chunkTable.IsCreated)
                    chunkTable.Dispose();
                if (itemHashLookup.IsCreated)
                    itemHashLookup.Dispose();
                if (itemHashTable.IsCreated)
                    itemHashTable.Dispose();
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

                using NativeArray<byte> rawBytes = new NativeArray<byte>(overrideHeader.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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

            SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);
            JobHandle.ScheduleBatchedJobs();
            error = "Synchronous sector entity-state override writes are disabled. Use the scheduled dehydration pipeline.";
            return false;
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

            int recordCount = entityStates.Length;
            if (recordCount <= 0)
            {
                error = "Sector entity-state override contains no records.";
                return false;
            }

            int rawByteLength = checked(recordCount * UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>());
            int blockCount = math.max(1, (rawByteLength + BlockSizeBytes - 1) / BlockSizeBytes);
            int compressedCapacity = rawByteLength + (rawByteLength / 255) + 16 + (blockCount * StandardCompressedBlockHeaderBytes);
            int fileCapacity = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>() + compressedCapacity;

            try
            {
                writeHandle.IsCreated = true;
                writeHandle.AbsolutePath = absolutePath;
                writeHandle.SectorHash = sectorHash;
                writeHandle.SourceStates = new NativeArray<EntityDataRecord>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.SortEntries = new NativeArray<SectorEntityStateSortEntry>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.RadixScratch = new NativeArray<SectorEntityStateSortEntry>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.SortedEntityStates = new NativeArray<EntityDataRecord>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.CompactStates = new NativeArray<SectorCompactEntityStateRecord16>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.FileBytes = new NativeArray<byte>(fileCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.ResultLength = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writeHandle.RadixCounts = new NativeArray<int>(1 << 16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writeHandle.RadixOffsets = new NativeArray<int>(1 << 16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writeHandle.RegisterNativeMemorySentinel();
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
                ChunkSizeMeters = math.max(1, chunkSizeMeters)
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

            try
            {
                JobHandle buildHandle = buildJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)));
                JobHandle sortHandle = sortJob.Schedule(buildHandle);
                JobHandle extractHandle = extractJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)), sortHandle);
                JobHandle compactHandle = compactJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)), extractHandle);
                writeHandle.Handle = compressJob.Schedule(compactHandle);
            }
            catch (Exception ex)
            {
                writeHandle.Dispose();
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

            if (!writeHandle.Handle.IsCompleted)
            {
                error = "Sector entity-state write job is still running.";
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

                int recordCount = checked((int)header.RecordCount);
                int recordStride = compact16
                    ? UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>()
                    : UnsafeUtility.SizeOf<EntityDataRecord>();
                int expectedByteLength = checked(recordCount * recordStride);
                if (expectedByteLength != header.DecompressedSize)
                {
                    error = "Sector entity-state override byte count does not match the record count.";
                    return false;
                }

                using NativeArray<byte> rawBytes = new NativeArray<byte>(header.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double absoluteX = (gridX * cellSize) + localX;
            double absoluteZ = (gridZ * cellSize) + localZ;
            int2 sectorCoord = new int2(
                (int)math.floor(absoluteX / PersistentWorldSectorEdgeLengthMeters),
                (int)math.floor(absoluteZ / PersistentWorldSectorEdgeLengthMeters));
            return PackSectorHash(sectorCoord);
        }

        internal static long ComputePersistentWorldPagedSectorHash(in AbsoluteUniversePosition position)
        {
            return PackSectorHash(QuantizePersistentWorldPagedSector(in position));
        }

        private static int2 QuantizePersistentWorldPagedSector(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolute.x / PersistentWorldSectorEdgeLengthMeters),
                (int)math.floor(absolute.z / PersistentWorldSectorEdgeLengthMeters));
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
            Debug.LogWarning($"[SaveBinaryStorage] QUARANTINED indexed sector 0x{sectorHash:X16}. Primary: {primaryError} Backup: {backupError}");
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

            using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.ClearMemory);
            int compressedCapacity = ModPayloadSubBlockSizeBytes + (ModPayloadSubBlockSizeBytes / 255) + 16 + ProtectedCompressedBlockHeaderBytes + IndexedSectorBlockHeaderSize + 32;
            int fileCapacity = UnsafeUtility.SizeOf<SectorOverrideFileHeader>() + compressedCapacity;
            using NativeArray<byte> fileBytes = new NativeArray<byte>(fileCapacity, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

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
                return false;

            return TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error);
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
                int sectorEntrySize = UnsafeUtility.SizeOf<SectorEntry>();
                int populatedCount = 0;
                using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
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

                if (populatedCount != (int)directoryHeader.SectorCount)
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
                int sectorEntrySize = UnsafeUtility.SizeOf<SectorEntry>();
                int populatedCount = 0;
                int skippedModSectorCount = 0;
                using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
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

                if (populatedCount != (int)directoryHeader.SectorCount)
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
                if (!TryReadIndexedDirectory(in header, ref mapping, out _, out SectorEntry[] sectorEntries, out error))
                    return false;

                if (!TryFindIndexedSectorEntryIndex(sectorEntries, sectorHash, out int sectorEntryIndex))
                {
                    error = $"Mod payload sector 0x{sectorHash:X16} is missing.";
                    return false;
                }

                SectorEntry entry = sectorEntries[sectorEntryIndex];
                using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
            if (directoryHeader.SectorCount > IndexedSectorDirectorySlotCount)
            {
                error = $"Indexed sector directory count {directoryHeader.SectorCount} exceeded slot capacity {IndexedSectorDirectorySlotCount}.";
                return false;
            }

            directoryHeader.ChunkSizeMeters = math.max(1, directoryHeader.ChunkSizeMeters);

            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * UnsafeUtility.SizeOf<SectorEntry>());
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
                int sectorEntrySize = UnsafeUtility.SizeOf<SectorEntry>();
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

                if (populatedSectorCount != (int)directoryHeader.SectorCount)
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
                compactBytes = new NativeArray<byte>(compactLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
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
                    UnsafeUtility.CopyStructureToPtr(ref entry, compactPtr + directoryEntryCursor);
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
                    compactBytes.Dispose();
            }

            try
            {
                byte* compactPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compactBytes);
                return AsyncWriteManager.OverwriteAll(absolutePath, compactPtr, compactLength, out error);
            }
            finally
            {
                if (compactBytes.IsCreated)
                    compactBytes.Dispose();
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
            out string error)
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
            NativeArray<byte> overrideBlockBytes = default;
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

                overrideBlockBytes = new NativeArray<byte>(overrideCompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(overrideBlockBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, overrideBlockBytes.Length, (byte*)overrideMapping.View + overrideHeaderSize, overrideCompressedSize))
                {
                    error = "Sector override staging copy exceeded destination bounds.";
                    return false;
                }

                if (!TryVerifySectorOverrideCompressedBlock(overrideBlockBytes, overrideCompressedSize, overrideDecompressedSize, overrideChecksum, out error))
                    return false;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref overrideMapping);
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
                int directoryBytes = saveHeader.PlayerOffset > headerSizeBytes
                    ? checked((int)(saveHeader.PlayerOffset - headerSizeBytes))
                    : 0;
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

            if (!TryPrepareIndexedSectorCommitBackup(absoluteSavePath, out error))
                return false;

            NativeArray<byte> commitBytes = default;
            try
            {
                long newLength = commitTarget.NewFileLength;
                if (newLength <= 0L || newLength > int.MaxValue)
                {
                    error = "Sector override commit exceeds the portable native buffer range.";
                    return false;
                }

                AsyncWriteManager.InvalidateCachedReadWindows(absoluteSavePath);
                // COLD ALLOC: NativeArray<byte>[newLength] - portable indexed-sector commit buffer - owner: SaveBinaryStorage
                commitBytes = new NativeArray<byte>((int)newLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
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

                if (commitTarget.ReusedExistingSlot)
                {
                    int previousCompressedSize = sectorEntries[commitTarget.SlotIndex].CompressedSize;
                    int trailingSlack = previousCompressedSize - overrideCompressedSize;
                    if (trailingSlack > 0)
                        UnsafeUtility.MemClear(mappedFilePtr + commitTarget.WriteOffset + overrideCompressedSize, trailingSlack);
                }

                int headerSizeBytes = ResolveExpectedHeaderSize(saveHeader.Version);
                int directoryEntryOffset = headerSizeBytes + IndexedSectorDirectoryHeaderSize + (commitTarget.SlotIndex * UnsafeUtility.SizeOf<SectorEntry>());
                SectorEntry updatedEntry = new SectorEntry
                {
                    SectorHash = sectorHash,
                    ByteOffset = commitTarget.WriteOffset,
                    CompressedSize = overrideCompressedSize,
                    DecompressedSize = overrideDecompressedSize,
                    Checksum = overrideChecksum
                };
                UnsafeUtility.CopyStructureToPtr(ref updatedEntry, mappedFilePtr + directoryEntryOffset);

                if (commitTarget.InsertedNewSlot && sectorCountDelta != 0)
                {
                    directoryHeader.SectorCount = checked((uint)(directoryHeader.SectorCount + sectorCountDelta));
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
                if (!AsyncWriteManager.OverwriteAll(absoluteSavePath, mappedFilePtr, (int)newLength, out error))
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
                    commitBytes.Dispose();
                if (overrideBlockBytes.IsCreated)
                    overrideBlockBytes.Dispose();
            }

            File.Delete(sectorOverridePath);
            QueueIndexedPersistentWorldDefragmentation(absoluteSavePath);
            return true;
        }

        internal static bool TryLoadSaveData(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
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
            voxelDeltaSnapshot = default;
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
                            out data,
                            out packedQuestHeader,
                            out packedQuestStateWords,
                            out persistentWorldDeltas,
                            out ecosystemSectorStates,
                            out voxelDeltaSnapshot,
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

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            int saveDataLength = checked((int)prefix.SaveDataByteLength);
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
                    out voxelDeltaSnapshot,
                    out error))
            {
                return false;
            }

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
                Checksum = FormatPayloadChecksum(in header)
            };

            error = string.Empty;
            return true;
        }

        internal static uint Hash32(void* ptr, long length)
        {
            return xxHash3.Hash64(ptr, length).x;
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
            uint metadataChecksum,
            uint expectedChecksum)
        {
            if (checksumRoot == expectedChecksum)
                return true;

            return ComputeIndexedChecksumRootLegacyHashFromMappedDirectory(filePtr, entryCursor, metadataChecksum) == expectedChecksum;
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
                        SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
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

                        entryCursor += UnsafeUtility.SizeOf<SectorEntry>();
                    }
                }
                else
                {
                    for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                    {
                        SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
                        if (IsIndexedSectorEntryPopulated(in entry))
                        {
                            if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, fileLength))
                            {
                                error = "Indexed sector entry exceeded the file bounds.";
                                return false;
                            }

                            populatedCount++;
                        }

                        entryCursor += UnsafeUtility.SizeOf<SectorEntry>();
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
            uint metadataChecksum)
        {
            unchecked
            {
                uint root = metadataChecksum == 0u ? 2166136261u : metadataChecksum;
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
                    if (IsIndexedSectorEntryPopulated(in entry))
                    {
                        uint leafHash = FoldIndexedSectorChecksumLegacyHash(in entry);
                        root = (root ^ leafHash) * 16777619u;
                    }

                    entryCursor += UnsafeUtility.SizeOf<SectorEntry>();
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

            using NativeArray<byte> rawBlock = new NativeArray<byte>(expectedDecompressedLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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

            string backupTempPath = backupPath + ".tmp";
            try
            {
                string backupDirectory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDirectory))
                    Directory.CreateDirectory(backupDirectory);

                if (File.Exists(backupTempPath))
                    File.Delete(backupTempPath);

                File.Copy(absolutePath, backupTempPath, true);
                if (File.Exists(backupPath))
                {
                    File.Replace(backupTempPath, backupPath, null);
                }
                else
                {
                    File.Move(backupTempPath, backupPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Indexed sector commit backup failed for '{absolutePath}': {ex.Message}";
                try
                {
                    if (File.Exists(backupTempPath))
                        File.Delete(backupTempPath);
                }
                catch
                {
                }

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
            uint2 hash = xxHash3.Hash64(ptr, length);
            return ((ulong)hash.y << 32) | hash.x;
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
            double3 absolute = position.ToAbsoluteDouble3();
            int2 sectorCoord = new int2(
                (int)math.floor(absolute.x / PersistentWorldSectorEdgeLengthMeters),
                (int)math.floor(absolute.z / PersistentWorldSectorEdgeLengthMeters));
            return PackSectorHash(sectorCoord);
        }

        private static long PackSectorHash(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static IndexedSectorGroupBuffer BuildIndexedSectorGroups(NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas, int chunkSizeMeters)
        {
            int sourceLength = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int capacity = math.max(4, sourceLength);
            IndexedSectorGroupBuffer buffer = new IndexedSectorGroupBuffer
            {
                Groups = new NativeList<IndexedSectorGroup>(math.min(IndexedSectorDirectorySlotCount, capacity), Allocator.Temp),
                Records = new NativeList<PersistentWorldDeltaRecord>(capacity, Allocator.Temp)
            };

            if (sourceLength <= 0)
                return buffer;

            int safeChunkSizeMeters = math.max(1, chunkSizeMeters);
            NativeArray<short> slotToGroupIndex = new NativeArray<short>(
                IndexedSectorDirectorySlotCount,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<short> recordGroupIndex = new NativeArray<short>(
                sourceLength,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            try
            {
                for (int i = 0; i < slotToGroupIndex.Length; i++)
                    slotToGroupIndex[i] = -1;

                for (int i = 0; i < sourceLength; i++)
                {
                    PersistentWorldDeltaRecord record = persistentWorldDeltas[i];
                    if (!record.IsValid)
                    {
                        recordGroupIndex[i] = -1;
                        continue;
                    }

                    long sectorHash = ComputePersistentWorldSectorHash(in record, safeChunkSizeMeters);
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
                    recordOffset = checked(recordOffset + group.Count);
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
                if (slotToGroupIndex.IsCreated)
                    slotToGroupIndex.Dispose();
                if (recordGroupIndex.IsCreated)
                    recordGroupIndex.Dispose();
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

                int encodedBlockCompressedLength = EncodeCompressedBlockLength(blockCompressedLength, usedManagedFallback: false);
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
                        out int blockCompressedLength,
                        out bool usedManagedFallback))
                {
                    return 0;
                }

                int encodedBlockCompressedLength = EncodeCompressedBlockLength(blockCompressedLength, usedManagedFallback);
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
                        out int blockCompressedLength,
                        out bool usedManagedFallback))
                {
                    return 0;
                }

                int encodedBlockCompressedLength = EncodeCompressedBlockLength(blockCompressedLength, usedManagedFallback);
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

            int compressedPayloadLength = checked((int)fileLength - headerSizeBytes);
            if (compressedPayloadLength <= 0)
            {
                error = "Save payload is missing.";
                return false;
            }

            if (compressedPayloadLength > MaxCompressedPayloadBytes || compressedPayloadLength > compressedBuffer.Length)
            {
                error = "Compressed save payload exceeds the supported decoder budget.";
                return false;
            }

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

                header.DeltaOffset = checked((uint)((int)header.DeltaOffset + payloadByteShift));
                header.EntityOffset = checked((uint)((int)header.EntityOffset + payloadByteShift));
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

            int playerPayloadLength = metadataBytes + checked((int)prefix.SaveDataByteLength);
            if (playerPayloadLength > rawPayloadLength)
            {
                error = "Serialized save data exceeds the decompressed payload length.";
                return false;
            }

            int payloadBaseOffset = ResolvePayloadBaseOffset(in header);
            int deltaSectionOffset = checked((int)header.DeltaOffset) - payloadBaseOffset;
            int entitySectionOffset = checked((int)header.EntityOffset) - payloadBaseOffset;
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

            int packedQuestWordCount = checked((int)header.DeltaCount);
            int payloadBaseOffset = ResolvePayloadBaseOffset(in header);
            int packedQuestSectionOffset = checked((int)header.DeltaOffset) - payloadBaseOffset;
            int entitySectionOffset = checked((int)header.EntityOffset) - payloadBaseOffset;
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
            if (packedQuestHeader.Magic != QuestSaveHeader.HeaderMagic)
            {
                error = "Packed quest-state header magic mismatch.";
                return false;
            }

            if (packedQuestHeader.FlagCount != header.DeltaCount)
            {
                error = "Packed quest-state word count header mismatch.";
                return false;
            }

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

            int entityCount = checked((int)header.EntityCount);
            if (entityCount <= 0)
                return true;

            if (header.Version >= CompactPersistentWorldSectionVersion)
                return TryReadPersistentWorldDeltasV5(rawPtr, rawPayloadLength, header, out persistentWorldDeltas, out error);

            int entityRecordSize = UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>();
            int entitySectionOffset = checked((int)header.EntityOffset) - ResolvePayloadBaseOffset(in header);

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

            fixed (PersistentWorldDeltaRecord* destinationPtr = persistentWorldDeltas)
            {
                byte* entitySourcePtr = AddByteOffset(rawPtr, entitySectionOffset);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, persistentWorldDeltas.Length * UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>(), entitySourcePtr, entityBytes))
                {
                    error = "Persistent-world delta copy exceeded destination bounds.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadVoxelDeltaSnapshot(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out NativeArray<byte> voxelDeltaSnapshot,
            out string error)
        {
            voxelDeltaSnapshot = default;
            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out int entitySectionLength, out error))
                return false;

            int voxelSectionOffset = entitySectionOffset + entitySectionLength;
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

            int voxelByteLength = rawPayloadLength - voxelSectionOffset;
            if (voxelByteLength <= 0)
                return true;

            voxelDeltaSnapshot = new NativeArray<byte>(voxelByteLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, voxelDeltaSnapshot.Length, AddByteOffset(rawPtr, voxelSectionOffset), voxelByteLength))
            {
                error = "Voxel delta payload copy exceeded destination bounds.";
                return false;
            }

            return true;
        }

        private static ulong ComputeHeaderHash(ref SaveFileHeader header)
        {
            if (header.Version >= First64BitHashVersion && header.Version < HeaderChecksumVersion)
            {
                IndexedSaveFileHeaderV8 legacyHeader = ConvertToIndexedHeaderV8(in header);
                legacyHeader.HashHeader64 = 0UL;
                return Hash64(UnsafeUtility.AddressOf(ref legacyHeader), IndexedHeaderV8HashSizeBytes);
            }

            SaveFileHeader copy = header;
            copy.HashHeader64 = 0UL;
            return Hash64(UnsafeUtility.AddressOf(ref copy), CurrentHeaderHashSizeBytes);
        }

        private static bool TryBuildPersistentWorldSectionTables(
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            out NativeParallelHashMap<int3, ushort> chunkLookup,
            out NativeList<int3> chunkTable,
            out NativeParallelHashMap<ulong, ushort> itemHashLookup,
            out NativeList<ulong> itemHashTable,
            out string error)
        {
            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int capacity = math.max(recordCount, 1);
            chunkLookup = new NativeParallelHashMap<int3, ushort>(capacity, Allocator.Temp);
            chunkTable = new NativeList<int3>(capacity, Allocator.Temp);
            itemHashLookup = new NativeParallelHashMap<ulong, ushort>(capacity, Allocator.Temp);
            itemHashTable = new NativeList<ulong>(capacity, Allocator.Temp);
            error = string.Empty;

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                if (!deltaRecord.IsValid)
                    continue;

                if (!chunkLookup.ContainsKey(deltaRecord.ChunkId))
                {
                    if (chunkTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta chunk table exceeded 65535 unique chunks.";
                        return false;
                    }

                    ushort chunkIndex = (ushort)chunkTable.Length;
                    chunkLookup.Add(deltaRecord.ChunkId, chunkIndex);
                    chunkTable.Add(deltaRecord.ChunkId);
                }

                if (deltaRecord.IsDeleted)
                    continue;

                if (!itemHashLookup.ContainsKey(deltaRecord.ItemPersistentIdHash))
                {
                    if (itemHashTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta item table exceeded 65535 unique item hashes.";
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
            out string error)
        {
            chunkLookup = default;
            chunkTable = default;
            itemHashLookup = default;
            itemHashTable = default;
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
            chunkLookup = new NativeParallelHashMap<int3, ushort>(capacity, Allocator.Temp);
            chunkTable = new NativeList<int3>(capacity, Allocator.Temp);
            itemHashLookup = new NativeParallelHashMap<ulong, ushort>(capacity, Allocator.Temp);
            itemHashTable = new NativeList<ulong>(capacity, Allocator.Temp);

            int endIndex = startIndex + recordCount;
            for (int i = startIndex; i < endIndex; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                if (!deltaRecord.IsValid)
                    continue;

                if (!chunkLookup.ContainsKey(deltaRecord.ChunkId))
                {
                    if (chunkTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta chunk table exceeded 65535 unique chunks.";
                        return false;
                    }

                    ushort chunkIndex = (ushort)chunkTable.Length;
                    chunkLookup.Add(deltaRecord.ChunkId, chunkIndex);
                    chunkTable.Add(deltaRecord.ChunkId);
                }

                if (deltaRecord.IsDeleted)
                    continue;

                if (!itemHashLookup.ContainsKey(deltaRecord.ItemPersistentIdHash))
                {
                    if (itemHashTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta item table exceeded 65535 unique item hashes.";
                        return false;
                    }

                    ushort itemIndex = (ushort)itemHashTable.Length;
                    itemHashLookup.Add(deltaRecord.ItemPersistentIdHash, itemIndex);
                    itemHashTable.Add(deltaRecord.ItemPersistentIdHash);
                }
            }

            return true;
        }

        private static int ComputePersistentWorldSectionLength(int entityCount, int chunkCount, int itemHashCount)
        {
            return PersistentWorldSectionHeaderSize +
                   checked(chunkCount * UnsafeUtility.SizeOf<int3>()) +
                   checked(itemHashCount * UnsafeUtility.SizeOf<ulong>()) +
                   checked(entityCount * UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>());
        }

        private static int ComputeEcosystemSectionLength(int recordCount)
        {
            return EcosystemSectionHeaderSize +
                   checked(math.max(recordCount, 0) * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>());
        }

        private static void WritePersistentWorldSection(
            byte* destination,
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            NativeParallelHashMap<int3, ushort> chunkLookup,
            NativeList<int3> chunkTable,
            NativeParallelHashMap<ulong, ushort> itemHashLookup,
            NativeList<ulong> itemHashTable)
        {
            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int sectionLength = ComputePersistentWorldSectionLength(recordCount, chunkTable.Length, itemHashTable.Length);
            PersistentWorldSectionHeader sectionHeader = new PersistentWorldSectionHeader
            {
                ChunkCount = (uint)chunkTable.Length,
                ItemHashCount = (uint)itemHashTable.Length,
                RecordCount = (uint)recordCount
            };

            UnsafeUtility.CopyStructureToPtr(ref sectionHeader, destination);
            int cursor = PersistentWorldSectionHeaderSize;

            if (chunkTable.Length > 0)
            {
                void* chunkSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkTable.AsArray());
                int chunkBytes = chunkTable.Length * UnsafeUtility.SizeOf<int3>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, chunkSourcePtr, chunkBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    return;
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
                    return;
                }

                cursor += itemBytes;
            }

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                PersistentWorldSaveRecord16 saveRecord = default;
                if (deltaRecord.IsDeleted &&
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
                }
                else if (deltaRecord.IsValid &&
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
                }

                UnsafeUtility.CopyStructureToPtr(ref saveRecord, AddByteOffset(destination, cursor));
                cursor += UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>();
            }
        }

        private static void WritePersistentWorldSection(
            byte* destination,
            NativeList<PersistentWorldDeltaRecord> persistentWorldDeltas,
            int startIndex,
            int recordCount,
            NativeParallelHashMap<int3, ushort> chunkLookup,
            NativeList<int3> chunkTable,
            NativeParallelHashMap<ulong, ushort> itemHashLookup,
            NativeList<ulong> itemHashTable)
        {
            int safeRecordCount = persistentWorldDeltas.IsCreated &&
                                  startIndex >= 0 &&
                                  recordCount >= 0 &&
                                  startIndex <= persistentWorldDeltas.Length - recordCount
                ? recordCount
                : 0;
            int sectionLength = ComputePersistentWorldSectionLength(safeRecordCount, chunkTable.Length, itemHashTable.Length);
            PersistentWorldSectionHeader sectionHeader = new PersistentWorldSectionHeader
            {
                ChunkCount = (uint)chunkTable.Length,
                ItemHashCount = (uint)itemHashTable.Length,
                RecordCount = (uint)safeRecordCount
            };

            UnsafeUtility.CopyStructureToPtr(ref sectionHeader, destination);
            int cursor = PersistentWorldSectionHeaderSize;

            if (chunkTable.Length > 0)
            {
                void* chunkSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkTable.AsArray());
                int chunkBytes = chunkTable.Length * UnsafeUtility.SizeOf<int3>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, chunkSourcePtr, chunkBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    return;
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
                    return;
                }

                cursor += itemBytes;
            }

            int endIndex = startIndex + safeRecordCount;
            for (int i = startIndex; i < endIndex; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                PersistentWorldSaveRecord16 saveRecord = default;
                if (deltaRecord.IsDeleted &&
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
                }
                else if (deltaRecord.IsValid &&
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
                }

                UnsafeUtility.CopyStructureToPtr(ref saveRecord, AddByteOffset(destination, cursor));
                cursor += UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>();
            }
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

            PersistentWorldSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(AddByteOffset(rawPtr, entitySectionOffset), 0);
            int chunkCount = checked((int)sectionHeader.ChunkCount);
            int itemHashCount = checked((int)sectionHeader.ItemHashCount);
            int recordCount = checked((int)sectionHeader.RecordCount);
            if (recordCount != checked((int)header.EntityCount))
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

            int cursor = entitySectionOffset + PersistentWorldSectionHeaderSize;
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

                persistentWorldDeltas[i] = new PersistentWorldDeltaRecord
                {
                    ChunkId = UnsafeUtility.ReadArrayElement<int3>(chunkTablePtr, saveRecord.ChunkIndex),
                    ItemPersistentIdHash = itemHash,
                    InstanceUid = saveRecord.InstanceUid,
                    PackedLocalPosition = saveRecord.PackedLocalPosition,
                    Quantity = saveRecord.Quantity == 0 ? (ushort)1 : saveRecord.Quantity,
                    ItemFlags = saveRecord.ItemFlags,
                    Reserved = saveRecord.Reserved
                };
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
            entitySectionOffset = checked((int)header.EntityOffset) - ResolvePayloadBaseOffset(in header);
            entitySectionLength = 0;
            error = string.Empty;

            if (entitySectionOffset < 0 || entitySectionOffset > rawPayloadLength)
            {
                error = "Entity payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            if (header.Version < CompactPersistentWorldSectionVersion)
            {
                int entityCount = checked((int)header.EntityCount);
                entitySectionLength = entityCount > 0
                    ? checked(entityCount * UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>())
                    : 0;
                if (entitySectionOffset + entitySectionLength > rawPayloadLength)
                {
                    error = "Entity payload exceeds the decompressed payload bounds.";
                    return false;
                }

                return true;
            }

            if (header.EntityCount <= 0)
                return true;

            if (entitySectionOffset + PersistentWorldSectionHeaderSize > rawPayloadLength)
            {
                error = "Persistent-world delta section header is truncated.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(AddByteOffset(rawPtr, entitySectionOffset), 0);
            if (sectionHeader.RecordCount != header.EntityCount)
            {
                error = "Persistent-world delta section header count mismatch.";
                return false;
            }

            int chunkCount = checked((int)sectionHeader.ChunkCount);
            int itemHashCount = checked((int)sectionHeader.ItemHashCount);
            int recordCount = checked((int)sectionHeader.RecordCount);
            entitySectionLength = ComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount);
            if (entitySectionOffset + entitySectionLength > rawPayloadLength)
            {
                error = "Persistent-world delta section exceeds the decompressed payload bounds.";
                return false;
            }

            return true;
        }

        private static void WriteEcosystemSection(
            byte* destination,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates)
        {
            int recordCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
            EcosystemSectionHeader sectionHeader = new EcosystemSectionHeader
            {
                RecordCount = (uint)recordCount
            };

            UnsafeUtility.CopyStructureToPtr(ref sectionHeader, destination);
            if (recordCount <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ecosystemSectorStates);
            int recordBytes = recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
            int sectionLength = ComputeEcosystemSectionLength(recordCount);
            if (!UnsafeMemoryCopyGuard.SafeCopy(
                    AddByteOffset(destination, EcosystemSectionHeaderSize),
                    sectionLength - EcosystemSectionHeaderSize,
                    sourcePtr,
                    recordBytes))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
            }
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

            EcosystemSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(AddByteOffset(rawPtr, ecosystemSectionOffset), 0);
            int recordCount = checked((int)sectionHeader.RecordCount);
            ecosystemSectorStates = new EcosystemSectorSaveRecord[recordCount];
            if (recordCount <= 0)
                return true;

            fixed (EcosystemSectorSaveRecord* destinationPtr = ecosystemSectorStates)
            {
                int recordBytes = recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(
                        destinationPtr,
                        ecosystemSectorStates.Length * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>(),
                        AddByteOffset(rawPtr, ecosystemSectionOffset + EcosystemSectionHeaderSize),
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
            ecosystemSectionOffset = entitySectionOffset + entitySectionLength;
            ecosystemSectionLength = 0;
            error = string.Empty;

            if (header.Version < EcosystemSectionVersion)
                return true;

            if (ecosystemSectionOffset < 0 || ecosystemSectionOffset > rawPayloadLength)
            {
                error = "Ecosystem payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            if (ecosystemSectionOffset + EcosystemSectionHeaderSize > rawPayloadLength)
            {
                error = "Ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(AddByteOffset(rawPtr, ecosystemSectionOffset), 0);
            int recordCount = checked((int)sectionHeader.RecordCount);
            ecosystemSectionLength = ComputeEcosystemSectionLength(recordCount);
            if (ecosystemSectionOffset + ecosystemSectionLength > rawPayloadLength)
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
            expandedPayloadLength = checked((int)header.ExpandedPayloadLength);
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

            if (header.DeltaOffset > header.PlayerOffset + RawPayloadCapacityBytes)
            {
                error = "Save packed quest-state offset exceeds the raw payload decoder budget.";
                return false;
            }

            if (header.EntityOffset > header.PlayerOffset + RawPayloadCapacityBytes)
            {
                error = "Save entity payload offset exceeds the raw payload decoder budget.";
                return false;
            }

            if (header.DeltaCount > (uint)(RawPayloadCapacityBytes / UnsafeUtility.SizeOf<uint>()))
            {
                error = "Save packed quest-state count exceeds the decoder budget.";
                return false;
            }

            int maxEntityRecordSize = header.Version >= CompactPersistentWorldSectionVersion
                ? UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>()
                : UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>();
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
            return new SaveFileHeader
            {
                MagicValue = legacyHeader.MagicValue,
                Version = legacyHeader.Version,
                CompatMask = legacyHeader.CompatMask,
                Flags = legacyHeader.Flags,
                TimestampUnixMs = legacyHeader.TimestampUnixMs,
                Checksum = unchecked((uint)legacyHeader.HashPayload64),
                DeltaCount = legacyHeader.DeltaCount,
                EntityCount = legacyHeader.EntityCount,
                PlayerOffset = legacyHeader.PlayerOffset,
                DeltaOffset = legacyHeader.DeltaOffset,
                EntityOffset = legacyHeader.EntityOffset,
                HashPayload64 = legacyHeader.HashPayload64,
                HashHeader64 = legacyHeader.HashHeader64
            };
        }

        private static IndexedSaveFileHeaderV8 ConvertToIndexedHeaderV8(in SaveFileHeader header)
        {
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
                HashPayload64 = header.HashPayload64,
                HashHeader64 = header.HashHeader64
            };
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
                    return CurrentHeaderSize;
                default:
                    return 0;
            }
        }

        private static int ResolveExpectedHeaderSize(ushort version)
        {
            return ResolveHeaderSize(version);
        }

        private static int ResolvePayloadBaseOffset(in SaveFileHeader header)
        {
            return checked((int)header.PlayerOffset);
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

        private static AbsoluteUniversePosition ToAup(Vector3 runtimePosition)
        {
            return AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
        }

        private static Vector3 ToRuntimePosition(AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
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

        private static void CopyUtf16StringToUnmanaged(string source, byte* destination, int destinationCapacityBytes)
        {
            if (string.IsNullOrEmpty(source))
                return;

            fixed (char* sourcePtr = source)
            {
                if (!UnsafeMemoryCopyGuard.SafeCopy(destination, destinationCapacityBytes, sourcePtr, source.Length * sizeof(char)))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
            }
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
            out int compressedLength,
            out bool usedManagedFallback)
        {
            compressedLength = 0;
            usedManagedFallback = false;
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

            compressedLength = DeflateBlockCompressManaged(source, sourceLength, destination, destinationCapacity);
            usedManagedFallback = compressedLength > 0;
            return compressedLength > 0;
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

            actualLength = DeflateBlockDecompressManaged(source, compressedLength, destination, rawLength);
            return actualLength == rawLength;
        }

        private static int EncodeCompressedBlockLength(int compressedLength, bool usedManagedFallback)
        {
            if (compressedLength <= 0 || compressedLength > CompressedBlockLengthMask)
                return 0;

            return usedManagedFallback
                ? compressedLength | ManagedDeflateBlockLengthFlag
                : compressedLength;
        }

        private static int DecodeCompressedBlockLength(int encodedCompressedLength)
        {
            return encodedCompressedLength & CompressedBlockLengthMask;
        }

        private static bool IsManagedDeflateBlock(int encodedCompressedLength)
        {
            return (encodedCompressedLength & ManagedDeflateBlockLengthFlag) != 0;
        }

        private static int DeflateBlockCompressManaged(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity)
        {
            try
            {
                lock (s_managedCompressionLock)
                {
                    using (UnmanagedMemoryStream input = new UnmanagedMemoryStream(source, sourceLength))
                    using (UnmanagedMemoryStream output = new UnmanagedMemoryStream(destination, destinationCapacity, destinationCapacity, FileAccess.Write))
                    {
                        using (DeflateStream deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                        {
                            CopyStreamWithManagedCompressionScratch(input, deflate);
                        }

                        long written = output.Position;
                        return written > 0L && written <= destinationCapacity ? (int)written : 0;
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
            byte[] scratch = s_managedCompressionScratch;
            int bytesRead;
            while ((bytesRead = source.Read(scratch, 0, scratch.Length)) > 0)
                destination.Write(scratch, 0, bytesRead);
        }

        [DllImport(Lz4DllName, EntryPoint = "LZ4_compress_default")]
        private static extern int LZ4Compress(byte* source, byte* destination, int sourceLength, int destinationCapacity);

        [DllImport(Lz4DllName, EntryPoint = "LZ4_decompress_safe")]
        private static extern int LZ4Decompress(byte* source, byte* destination, int compressedLength, int destinationCapacity);
    }
}
