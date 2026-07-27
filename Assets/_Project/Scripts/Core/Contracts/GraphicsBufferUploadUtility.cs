using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

// Relocated out of SystemDispatcher.cs (Hecton8.Core) into Hecton8.Core.Contracts.
// Hecton8.Core consumes the Surface Nets mesher (VoxelSurfaceNetsVault, VoxelVertexDTO,
// ChunkMeshingStateDTO) while the mesher needed this uploader, which was a hard assembly
// cycle that stopped the project compiling. Namespace is deliberately unchanged so every
// existing call site keeps resolving. Its only non-Unity dependency is UnsafeMemoryCopyGuard,
// which already lives here in CoreLowLevelUtilities.cs.
namespace Hecton8.Core
{
    /// <summary>
    /// LockBufferForWrite upload helpers used by owned runtime graphics buffers.
    /// </summary>
    public static class GraphicsBufferUploadUtility
    {
        // Preserves the exact telemetry owner string previously produced by nameof(SystemDispatcher).
        private const string CopyGuardOwnerLabel = "SystemDispatcher";

        public const int DefaultDirtyPageSize = 256;
        public const byte UploadedDirtyPageSnapshotMarker = 2;

        public struct PageUploadStats
        {
            public int UploadedPages;
            public int DeferredPages;
            public int LockSpans;
            public long UploadedBytes;
            public int FirstDeferredPage;
        }

        public struct UploadBudgetSnapshot
        {
            public uint FrameId;
            public long BudgetBytes;
            public long ClaimedBytes;
            public long UploadedBytes;
            public int ClaimCount;
            public int DeferredPages;
        }

        private const long MinimumFrameUploadBudgetBytes = 256L * 1024L;
        private const long MiddleFrameUploadBudgetBytes = 1024L * 1024L;
        private const long HighFrameUploadBudgetBytes = 2L * 1024L * 1024L;
        private const long UltraFrameUploadBudgetBytes = 4L * 1024L * 1024L;

        private static bool _uploadBudgetActive;
        private static uint _uploadBudgetFrameId;
        private static long _uploadBudgetBytes;
        private static long _claimedUploadBytes;
        private static long _uploadedBytes;
        private static int _uploadClaimCount;
        private static int _deferredUploadPages;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // GraphicsBuffer locking is main-thread only, so a plain latch is enough to stop a
        // device-less run from repeating the same failed-lock warning on every upload attempt.
        private static bool _lockCapacityWarningLogged;
#endif

        public static UploadBudgetSnapshot CurrentUploadBudgetSnapshot => new UploadBudgetSnapshot
        {
            FrameId = _uploadBudgetFrameId,
            BudgetBytes = _uploadBudgetBytes,
            ClaimedBytes = _claimedUploadBytes,
            UploadedBytes = _uploadedBytes,
            ClaimCount = _uploadClaimCount,
            DeferredPages = _deferredUploadPages
        };

        public static void BeginUploadBudgetFrame(float globalQualityWeight01, uint frameId)
        {
            BeginUploadBudgetFrame(globalQualityWeight01, frameId, 0f);
        }

        public static void BeginUploadBudgetFrame(float globalQualityWeight01, uint frameId, float pressureFactor01)
        {
            if (_uploadBudgetActive && _uploadBudgetFrameId == frameId)
                return;

            _uploadBudgetActive = true;
            _uploadBudgetFrameId = frameId;
            _uploadBudgetBytes = ResolveFrameUploadBudgetBytes(globalQualityWeight01, pressureFactor01);
            _claimedUploadBytes = 0L;
            _uploadedBytes = 0L;
            _uploadClaimCount = 0;
            _deferredUploadPages = 0;
        }

        public static long EstimateUploadBytes<T>(int count) where T : struct
        {
            return (long)UnsafeUtility.SizeOf<T>() * math.max(0, count);
        }

        public static bool CanUploadBytesThisFrame(long uploadBytes, bool allowOversizedFirstUpload = true)
        {
            long safeBytes = uploadBytes > 0L ? uploadBytes : 0L;
            if (safeBytes <= 0L || !_uploadBudgetActive)
                return true;

            if (allowOversizedFirstUpload && _claimedUploadBytes <= 0L)
                return true;

            return _claimedUploadBytes + safeBytes <= _uploadBudgetBytes;
        }

        public static bool TryBeginManualUpload(long uploadBytes, bool allowOversizedFirstUpload = true)
        {
            long safeBytes = uploadBytes > 0L ? uploadBytes : 0L;
            if (safeBytes <= 0L || !_uploadBudgetActive)
                return true;

            if (!CanUploadBytesThisFrame(safeBytes, allowOversizedFirstUpload))
            {
                RecordDeferredUploadPages(1);
                return false;
            }

            _claimedUploadBytes += safeBytes;
            _uploadClaimCount++;
            return true;
        }

        public static void CompleteManualUpload(long uploadBytes)
        {
            if (!_uploadBudgetActive || uploadBytes <= 0L)
                return;

            _uploadedBytes += uploadBytes;
        }

        public static void CancelManualUpload(long uploadBytes)
        {
            if (!_uploadBudgetActive || uploadBytes <= 0L)
                return;

            _claimedUploadBytes = Math.Max(0L, _claimedUploadBytes - uploadBytes);
            if (_uploadClaimCount > 0)
                _uploadClaimCount--;
        }

        public static void RecordManualUploadDeferred(int deferredUnits = 1)
        {
            RecordDeferredUploadPages(math.max(1, deferredUnits));
        }

        /// <summary>
        /// Creates a structured buffer configured for standard SetData uploads.
        /// </summary>
        public static GraphicsBuffer CreateStructuredBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Creates a structured buffer that the GPU may write and CPU uploads may initialize via a copy-source staging buffer.
        /// </summary>
        public static GraphicsBuffer CreateStructuredCopyDestinationBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.CopyDestination,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Creates a structured buffer configured for direct CPU writes.
        /// </summary>
        public static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Creates a CPU-write staging buffer that may only be read/copied by the GPU.
        /// </summary>
        public static GraphicsBuffer CreateStructuredUploadStagingBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.CopySource,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        public static GraphicsBuffer CreateRawIndirectCopyDestinationBuffer(int count, int stride)
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.CopyDestination,
                math.max(1, count),
                math.max(4, stride));
        }

        public static GraphicsBuffer CreateRawIndirectUploadStagingBuffer(int count, int stride)
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.CopySource,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                math.max(4, stride));
        }

        /// <summary>
        /// Uploads a blittable native array into a graphics buffer using one memcpy.
        /// </summary>
        public static bool TryUploadSingle<T>(GraphicsBuffer destination, T value) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, 1, 1);
            if (safeCount <= 0)
                return false;

            long uploadedBytes = UnsafeUtility.SizeOf<T>();
            if (!TryBeginManualUpload(uploadedBytes))
                return false;

            bool bufferLocked = false;
            bool uploadAccepted = false;
            bool unlockSucceeded = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(0, 1);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, 1))
                {
                    mapped[0] = value;
                    uploadAccepted = true;
                }
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        destination.UnlockBufferAfterWrite<T>(1);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadAccepted && unlockSucceeded)
                        CompleteManualUpload(uploadedBytes);
                    else
                        CancelManualUpload(uploadedBytes);
                }
            }

            return uploadAccepted && unlockSucceeded;
        }

        public static bool TryClear<T>(GraphicsBuffer destination, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, destination != null ? destination.count : 0, count);
            if (safeCount <= 0)
                return false;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return false;

            bool bufferLocked = false;
            bool uploadAccepted = false;
            bool unlockSucceeded = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(0, safeCount);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, safeCount))
                {
                    for (int i = 0; i < safeCount; i++)
                        mapped[i] = default;
                    uploadAccepted = true;
                }
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        destination.UnlockBufferAfterWrite<T>(safeCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadAccepted && unlockSucceeded)
                        CompleteManualUpload(uploadedBytes);
                    else
                        CancelManualUpload(uploadedBytes);
                }
            }

            return uploadAccepted && unlockSucceeded;
        }

        public static bool TryUploadNativeArrayRange<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            int sourceStartIndex,
            int destinationStartIndex,
            int count)
            where T : struct
        {
            int safeCount = ResolveSafeWriteRangeCount<T>(
                destination,
                source.IsCreated ? source.Length : 0,
                sourceStartIndex,
                destinationStartIndex,
                count);
            if (safeCount <= 0)
                return false;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return false;

            bool uploaded = false;
            try
            {
                uploaded = TryCopyNativeArrayRange(destination, source, sourceStartIndex, destinationStartIndex, safeCount);
            }
            finally
            {
                if (uploaded)
                    CompleteManualUpload(uploadedBytes);
                else
                    CancelManualUpload(uploadedBytes);
            }

            return uploaded;
        }

        public static unsafe bool TryUploadArrayRange<T>(
            GraphicsBuffer destination,
            T[] source,
            int sourceStartIndex,
            int destinationStartIndex,
            int count)
            where T : unmanaged
        {
            int safeCount = ResolveSafeWriteRangeCount<T>(
                destination,
                source != null ? source.Length : 0,
                sourceStartIndex,
                destinationStartIndex,
                count);
            if (safeCount <= 0)
                return false;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return false;

            bool uploaded = false;
            try
            {
                uploaded = TryCopyArrayRange(destination, source, sourceStartIndex, destinationStartIndex, safeCount);
            }
            finally
            {
                if (uploaded)
                    CompleteManualUpload(uploadedBytes);
                else
                    CancelManualUpload(uploadedBytes);
            }

            return uploaded;
        }

        public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            TryUploadNativeArray(destination, source, count);
        }

        private static bool TryUploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return false;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return false;

            bool copyAccepted = false;
            bool bufferLocked = false;
            bool unlockSucceeded = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(0, safeCount);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, safeCount))
                {
                    unsafe
                    {
                        void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        long copyBytes = uploadedBytes;
                        long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                        copyAccepted = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                        if (!copyAccepted)
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(CopyGuardOwnerLabel);
                    }
                }
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        destination.UnlockBufferAfterWrite<T>(safeCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (copyAccepted && unlockSucceeded)
                        CompleteManualUpload(uploadedBytes);
                    else
                        CancelManualUpload(uploadedBytes);
                }
            }

            return copyAccepted;
        }

        public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T>.ReadOnly source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return;

            bool uploadCompleted = false;
            bool bufferLocked = false;
            bool unlockSucceeded = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(0, safeCount);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, safeCount))
                {
                    for (int i = 0; i < safeCount; i++)
                        mapped[i] = source[i];
                    uploadCompleted = true;
                }
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        destination.UnlockBufferAfterWrite<T>(safeCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadCompleted && unlockSucceeded)
                        CompleteManualUpload(uploadedBytes);
                    else
                        CancelManualUpload(uploadedBytes);
                }
            }
        }

        /// <summary>
        /// Uploads a blittable managed array into a graphics buffer using one memcpy.
        /// </summary>
        public static unsafe void UploadArray<T>(GraphicsBuffer destination, T[] source, int count) where T : unmanaged
        {
            TryUploadArray(destination, source, count);
        }

        private static unsafe bool TryUploadArray<T>(GraphicsBuffer destination, T[] source, int count) where T : unmanaged
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source != null ? source.Length : 0, count);
            if (safeCount <= 0)
                return false;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return false;

            bool copyAccepted = false;
            bool bufferLocked = false;
            bool unlockSucceeded = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(0, safeCount);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, safeCount))
                {
                    fixed (T* sourcePtr = source)
                    {
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        long copyBytes = uploadedBytes;
                        long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                        copyAccepted = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                        if (!copyAccepted)
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(CopyGuardOwnerLabel);
                    }
                }
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        destination.UnlockBufferAfterWrite<T>(safeCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (copyAccepted && unlockSucceeded)
                        CompleteManualUpload(uploadedBytes);
                    else
                        CancelManualUpload(uploadedBytes);
                }
            }

            return copyAccepted;
        }

        public static void UploadNativeArrayAndCopyWholeBuffer<T>(
            GraphicsBuffer uploadStaging,
            GraphicsBuffer destination,
            NativeArray<T> source,
            int count)
            where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(uploadStaging, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0 || !CanCopyWholeBuffer(uploadStaging, destination))
                return;

            if (!TryUploadNativeArray(uploadStaging, source, safeCount))
                return;

            UnityEngine.Graphics.CopyBuffer(uploadStaging, destination);
        }

        public static void UploadArrayAndCopyWholeBuffer<T>(
            GraphicsBuffer uploadStaging,
            GraphicsBuffer destination,
            T[] source,
            int count)
            where T : unmanaged
        {
            int safeCount = ResolveSafeWriteCount<T>(uploadStaging, source != null ? source.Length : 0, count);
            if (safeCount <= 0 || !CanCopyWholeBuffer(uploadStaging, destination))
                return;

            if (!TryUploadArray(uploadStaging, source, safeCount))
                return;

            UnityEngine.Graphics.CopyBuffer(uploadStaging, destination);
        }

        /// <summary>
        /// Uploads a blittable native array into a GPU-write-capable buffer.
        /// </summary>
        public static void UploadNativeArraySetData<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return;

            bool uploadCompleted = false;
            try
            {
                destination.SetData(source, 0, 0, safeCount);
                uploadCompleted = true;
            }
            finally
            {
                if (uploadCompleted)
                    CompleteManualUpload(uploadedBytes);
                else
                    CancelManualUpload(uploadedBytes);
            }
        }

        public static void UploadNativeArraySetDataRange<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            int sourceStartIndex,
            int destinationStartIndex,
            int count)
            where T : struct
        {
            int safeCount = ResolveSafeWriteRangeCount<T>(
                destination,
                source.IsCreated ? source.Length : 0,
                sourceStartIndex,
                destinationStartIndex,
                count);
            if (safeCount <= 0)
                return;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return;

            bool uploadCompleted = false;
            try
            {
                destination.SetData(source, sourceStartIndex, destinationStartIndex, safeCount);
                uploadCompleted = true;
            }
            finally
            {
                if (uploadCompleted)
                    CompleteManualUpload(uploadedBytes);
                else
                    CancelManualUpload(uploadedBytes);
            }
        }

        /// <summary>
        /// Uploads a blittable managed staging array into a GPU-write-capable buffer.
        /// </summary>
        public static void UploadArraySetData<T>(GraphicsBuffer destination, T[] source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source != null ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return;

            bool uploadCompleted = false;
            try
            {
                destination.SetData(source, 0, 0, safeCount);
                uploadCompleted = true;
            }
            finally
            {
                if (uploadCompleted)
                    CompleteManualUpload(uploadedBytes);
                else
                    CancelManualUpload(uploadedBytes);
            }
        }

        public static void UploadArraySetDataRange<T>(
            GraphicsBuffer destination,
            T[] source,
            int sourceStartIndex,
            int destinationStartIndex,
            int count)
            where T : struct
        {
            int safeCount = ResolveSafeWriteRangeCount<T>(
                destination,
                source != null ? source.Length : 0,
                sourceStartIndex,
                destinationStartIndex,
                count);
            if (safeCount <= 0)
                return;

            long uploadedBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
            if (!TryBeginManualUpload(uploadedBytes))
                return;

            bool uploadCompleted = false;
            try
            {
                destination.SetData(source, sourceStartIndex, destinationStartIndex, safeCount);
                uploadCompleted = true;
            }
            finally
            {
                if (uploadCompleted)
                    CompleteManualUpload(uploadedBytes);
                else
                    CancelManualUpload(uploadedBytes);
            }
        }

        public static int ResolveDirtyPageCount(int elementCount, int pageSize)
        {
            if (elementCount <= 0)
                return 0;

            int safePageSize = math.max(1, pageSize);
            return (elementCount + safePageSize - 1) / safePageSize;
        }

        public static bool HasAnyDirtyPage(NativeArray<byte> dirtyPages, int elementCount, int pageSize)
        {
            if (!dirtyPages.IsCreated || elementCount <= 0)
                return false;

            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(elementCount, pageSize));
            for (int i = 0; i < pageCount; i++)
            {
                if (dirtyPages[i] != 0)
                    return true;
            }

            return false;
        }

        public static void MarkDirtyPage(NativeArray<byte> dirtyPages, int elementIndex, int elementCount, int pageSize)
        {
            if (!dirtyPages.IsCreated || elementIndex < 0 || elementIndex >= elementCount)
                return;

            int safePageSize = math.max(1, pageSize);
            int pageIndex = elementIndex / safePageSize;
            if ((uint)pageIndex < (uint)dirtyPages.Length)
                dirtyPages[pageIndex] = 1;
        }

        public static void MarkDirtyPageRange(NativeArray<byte> dirtyPages, int startElementIndex, int elementCountToMark, int totalElementCount, int pageSize)
        {
            if (!dirtyPages.IsCreated ||
                startElementIndex < 0 ||
                elementCountToMark <= 0 ||
                totalElementCount <= 0 ||
                startElementIndex >= totalElementCount)
            {
                return;
            }

            int safePageSize = math.max(1, pageSize);
            int endElementExclusive = startElementIndex + elementCountToMark;
            if (endElementExclusive < startElementIndex || endElementExclusive > totalElementCount)
                endElementExclusive = totalElementCount;

            int firstPage = startElementIndex / safePageSize;
            int lastPage = (endElementExclusive - 1) / safePageSize;
            int pageLimit = math.min(dirtyPages.Length, ResolveDirtyPageCount(totalElementCount, safePageSize));
            for (int pageIndex = firstPage; pageIndex <= lastPage && pageIndex < pageLimit; pageIndex++)
                dirtyPages[pageIndex] = 1;
        }

        public static void MarkAllDirtyPages(NativeArray<byte> dirtyPages, int elementCount, int pageSize)
        {
            if (!dirtyPages.IsCreated || elementCount <= 0)
                return;

            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(elementCount, pageSize));
            for (int i = 0; i < pageCount; i++)
                dirtyPages[i] = 1;
        }

        public static void ClearDirtyPages(NativeArray<byte> dirtyPages, int elementCount, int pageSize)
        {
            if (!dirtyPages.IsCreated || elementCount <= 0)
                return;

            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(elementCount, pageSize));
            for (int i = 0; i < pageCount; i++)
                dirtyPages[i] = 0;
        }

        public static int ResolveFirstDirtyPageBytes<T>(NativeArray<byte> dirtyPages, int elementCount, int pageSize)
            where T : struct
        {
            if (!dirtyPages.IsCreated || elementCount <= 0)
                return 0;

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(elementCount, safePageSize));
            int stride = UnsafeUtility.SizeOf<T>();
            for (int i = 0; i < pageCount; i++)
            {
                if (dirtyPages[i] == 0)
                    continue;

                int pageElementStart = i * safePageSize;
                int pageElementCount = math.min(safePageSize, elementCount - pageElementStart);
                if (pageElementCount <= 0)
                    return 0;

                long pageBytes = (long)pageElementCount * stride;
                return pageBytes > int.MaxValue ? int.MaxValue : (int)pageBytes;
            }

            return 0;
        }

        public static PageUploadStats CalculateDirtyPageUploadStats<T>(
            NativeArray<byte> dirtyPages,
            int sourceLength,
            int destinationCount,
            int requestedCount,
            int pageSize,
            int maxBytesThisFrame)
            where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destinationCount, UnsafeUtility.SizeOf<T>(), sourceLength, requestedCount);
            return CalculateDirtyPageUploadStats<T>(dirtyPages, safeCount, pageSize, maxBytesThisFrame);
        }

        public static PageUploadStats UploadNativeArrayDirtyPages<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            NativeArray<byte> dirtyPages,
            int count,
            int pageSize,
            int maxBytesThisFrame,
            bool clearUploadedPages)
            where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0 || !dirtyPages.IsCreated)
                return CreateEmptyPageUploadStats();

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(safeCount, safePageSize));
            if (pageCount <= 0)
                return CreateEmptyPageUploadStats();

            int stride = UnsafeUtility.SizeOf<T>();
            long byteBudget = ResolveAvailableUploadByteBudget(maxBytesThisFrame);
            PageUploadStats stats = CreateEmptyPageUploadStats();
            if (byteBudget <= 0L)
            {
                stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPages, 0, pageCount);
                stats.DeferredPages = CountDirtyPagesInRange(dirtyPages, 0, pageCount);
                RecordDeferredUploadPages(stats.DeferredPages);
                return stats;
            }

            int pageIndex = 0;
            while (pageIndex < pageCount)
            {
                if (dirtyPages[pageIndex] == 0)
                {
                    pageIndex++;
                    continue;
                }

                int runStartPage = pageIndex;
                int runPageCount = 0;
                long runBytes = 0L;
                while (pageIndex < pageCount && dirtyPages[pageIndex] != 0)
                {
                    int pageElementStart = pageIndex * safePageSize;
                    int pageElementCount = math.min(safePageSize, safeCount - pageElementStart);
                    if (pageElementCount <= 0)
                    {
                        pageIndex++;
                        continue;
                    }

                    long pageBytes = (long)pageElementCount * stride;
                    bool overBudget = stats.UploadedBytes + runBytes + pageBytes > byteBudget;
                    if (overBudget && (stats.UploadedBytes > 0L || runPageCount > 0))
                        break;

                    runBytes += pageBytes;
                    runPageCount++;
                    pageIndex++;
                }

                if (runPageCount <= 0)
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPages, runStartPage, pageCount);
                    RecordDeferredUploadPages(stats.DeferredPages);
                    return stats;
                }

                int startElement = runStartPage * safePageSize;
                int elementCount = math.min(runPageCount * safePageSize, safeCount - startElement);
                if (!UploadNativeArrayRange(destination, source, startElement, elementCount))
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPages, runStartPage, pageCount);
                    RecordDeferredUploadPages(stats.DeferredPages);
                    return stats;
                }

                RecordUploadClaim(runBytes);
                if (clearUploadedPages)
                {
                    int runEnd = runStartPage + runPageCount;
                    for (int i = runStartPage; i < runEnd; i++)
                        dirtyPages[i] = 0;
                }

                stats.UploadedPages += runPageCount;
                stats.UploadedBytes += runBytes;
                stats.LockSpans++;

                if (stats.UploadedBytes >= byteBudget && pageIndex < pageCount)
                {
                    int deferred = CountDirtyPagesInRange(dirtyPages, pageIndex, pageCount);
                    if (deferred > 0)
                    {
                        stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPages, pageIndex, pageCount);
                        stats.DeferredPages += deferred;
                        RecordDeferredUploadPages(deferred);
                        return stats;
                    }
                }
            }

            return stats;
        }

        public static PageUploadStats UploadNativeArrayDirtyPagesFromSnapshot<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            byte[] dirtyPageSnapshot,
            int count,
            int pageSize,
            int maxBytesThisFrame,
            bool markUploadedPages)
            where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0 || dirtyPageSnapshot == null || dirtyPageSnapshot.Length == 0)
                return CreateEmptyPageUploadStats();

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPageSnapshot.Length, ResolveDirtyPageCount(safeCount, safePageSize));
            if (pageCount <= 0)
                return CreateEmptyPageUploadStats();

            int stride = UnsafeUtility.SizeOf<T>();
            long byteBudget = ResolveAvailableUploadByteBudget(maxBytesThisFrame);
            PageUploadStats stats = CreateEmptyPageUploadStats();
            if (byteBudget <= 0L)
            {
                stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPageSnapshot, 0, pageCount);
                stats.DeferredPages = CountDirtyPagesInRange(dirtyPageSnapshot, 0, pageCount);
                RecordDeferredUploadPages(stats.DeferredPages);
                return stats;
            }

            int pageIndex = 0;
            while (pageIndex < pageCount)
            {
                if (dirtyPageSnapshot[pageIndex] == 0)
                {
                    pageIndex++;
                    continue;
                }

                int runStartPage = pageIndex;
                int runPageCount = 0;
                long runBytes = 0L;
                while (pageIndex < pageCount && dirtyPageSnapshot[pageIndex] != 0)
                {
                    int pageElementStart = pageIndex * safePageSize;
                    int pageElementCount = math.min(safePageSize, safeCount - pageElementStart);
                    if (pageElementCount <= 0)
                    {
                        pageIndex++;
                        continue;
                    }

                    long pageBytes = (long)pageElementCount * stride;
                    bool overBudget = stats.UploadedBytes + runBytes + pageBytes > byteBudget;
                    if (overBudget && (stats.UploadedBytes > 0L || runPageCount > 0))
                        break;

                    runBytes += pageBytes;
                    runPageCount++;
                    pageIndex++;
                }

                if (runPageCount <= 0)
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPageSnapshot, runStartPage, pageCount);
                    RecordDeferredUploadPages(stats.DeferredPages);
                    return stats;
                }

                int startElement = runStartPage * safePageSize;
                int elementCount = math.min(runPageCount * safePageSize, safeCount - startElement);
                if (!UploadNativeArrayRange(destination, source, startElement, elementCount))
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPageSnapshot, runStartPage, pageCount);
                    RecordDeferredUploadPages(stats.DeferredPages);
                    return stats;
                }

                RecordUploadClaim(runBytes);
                if (markUploadedPages)
                {
                    int runEnd = runStartPage + runPageCount;
                    for (int i = runStartPage; i < runEnd; i++)
                        dirtyPageSnapshot[i] = UploadedDirtyPageSnapshotMarker;
                }

                stats.UploadedPages += runPageCount;
                stats.UploadedBytes += runBytes;
                stats.LockSpans++;

                if (stats.UploadedBytes >= byteBudget && pageIndex < pageCount)
                {
                    int deferred = CountDirtyPagesInRange(dirtyPageSnapshot, pageIndex, pageCount);
                    if (deferred > 0)
                    {
                        stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPageSnapshot, pageIndex, pageCount);
                        stats.DeferredPages += deferred;
                        RecordDeferredUploadPages(deferred);
                        return stats;
                    }
                }
            }

            return stats;
        }

        public static PageUploadStats UploadNativeArrayDirtyPagesSetData<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            NativeArray<byte> dirtyPages,
            int count,
            int pageSize,
            int maxBytesThisFrame,
            bool clearUploadedPages)
            where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0 || !dirtyPages.IsCreated)
                return CreateEmptyPageUploadStats();

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(safeCount, safePageSize));
            if (pageCount <= 0)
                return CreateEmptyPageUploadStats();

            int stride = UnsafeUtility.SizeOf<T>();
            long byteBudget = ResolveAvailableUploadByteBudget(maxBytesThisFrame);
            PageUploadStats stats = CreateEmptyPageUploadStats();
            if (byteBudget <= 0L)
            {
                stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPages, 0, pageCount);
                stats.DeferredPages = CountDirtyPagesInRange(dirtyPages, 0, pageCount);
                RecordDeferredUploadPages(stats.DeferredPages);
                return stats;
            }

            int pageIndex = 0;
            while (pageIndex < pageCount)
            {
                if (dirtyPages[pageIndex] == 0)
                {
                    pageIndex++;
                    continue;
                }

                int runStartPage = pageIndex;
                int runPageCount = 0;
                long runBytes = 0L;
                while (pageIndex < pageCount && dirtyPages[pageIndex] != 0)
                {
                    int pageElementStart = pageIndex * safePageSize;
                    int pageElementCount = math.min(safePageSize, safeCount - pageElementStart);
                    if (pageElementCount <= 0)
                    {
                        pageIndex++;
                        continue;
                    }

                    long pageBytes = (long)pageElementCount * stride;
                    bool overBudget = stats.UploadedBytes + runBytes + pageBytes > byteBudget;
                    if (overBudget && (stats.UploadedBytes > 0L || runPageCount > 0))
                        break;

                    runBytes += pageBytes;
                    runPageCount++;
                    pageIndex++;
                }

                if (runPageCount <= 0)
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPages, runStartPage, pageCount);
                    RecordDeferredUploadPages(stats.DeferredPages);
                    return stats;
                }

                int startElement = runStartPage * safePageSize;
                int elementCount = math.min(runPageCount * safePageSize, safeCount - startElement);
                UploadNativeArraySetDataRange(destination, source, startElement, startElement, elementCount);
                if (clearUploadedPages)
                {
                    int runEnd = runStartPage + runPageCount;
                    for (int i = runStartPage; i < runEnd; i++)
                        dirtyPages[i] = 0;
                }

                stats.UploadedPages += runPageCount;
                stats.UploadedBytes += runBytes;
                stats.LockSpans++;

                if (stats.UploadedBytes >= byteBudget && pageIndex < pageCount)
                {
                    int deferred = CountDirtyPagesInRange(dirtyPages, pageIndex, pageCount);
                    if (deferred > 0)
                    {
                        stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPages, pageIndex, pageCount);
                        stats.DeferredPages += deferred;
                        RecordDeferredUploadPages(deferred);
                        return stats;
                    }
                }
            }

            return stats;
        }

        public static PageUploadStats UploadNativeArrayDirtyPagesSetDataFromSnapshot<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            byte[] dirtyPageSnapshot,
            int count,
            int pageSize,
            int maxBytesThisFrame,
            bool markUploadedPages)
            where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0 || dirtyPageSnapshot == null || dirtyPageSnapshot.Length == 0)
                return CreateEmptyPageUploadStats();

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPageSnapshot.Length, ResolveDirtyPageCount(safeCount, safePageSize));
            if (pageCount <= 0)
                return CreateEmptyPageUploadStats();

            int stride = UnsafeUtility.SizeOf<T>();
            long byteBudget = ResolveAvailableUploadByteBudget(maxBytesThisFrame);
            PageUploadStats stats = CreateEmptyPageUploadStats();
            if (byteBudget <= 0L)
            {
                stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPageSnapshot, 0, pageCount);
                stats.DeferredPages = CountDirtyPagesInRange(dirtyPageSnapshot, 0, pageCount);
                RecordDeferredUploadPages(stats.DeferredPages);
                return stats;
            }

            int pageIndex = 0;
            while (pageIndex < pageCount)
            {
                if (dirtyPageSnapshot[pageIndex] == 0)
                {
                    pageIndex++;
                    continue;
                }

                int runStartPage = pageIndex;
                int runPageCount = 0;
                long runBytes = 0L;
                while (pageIndex < pageCount && dirtyPageSnapshot[pageIndex] != 0)
                {
                    int pageElementStart = pageIndex * safePageSize;
                    int pageElementCount = math.min(safePageSize, safeCount - pageElementStart);
                    if (pageElementCount <= 0)
                    {
                        pageIndex++;
                        continue;
                    }

                    long pageBytes = (long)pageElementCount * stride;
                    bool overBudget = stats.UploadedBytes + runBytes + pageBytes > byteBudget;
                    if (overBudget && (stats.UploadedBytes > 0L || runPageCount > 0))
                        break;

                    runBytes += pageBytes;
                    runPageCount++;
                    pageIndex++;
                }

                if (runPageCount <= 0)
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPageSnapshot, runStartPage, pageCount);
                    RecordDeferredUploadPages(stats.DeferredPages);
                    return stats;
                }

                int startElement = runStartPage * safePageSize;
                int elementCount = math.min(runPageCount * safePageSize, safeCount - startElement);
                UploadNativeArraySetDataRange(destination, source, startElement, startElement, elementCount);
                if (markUploadedPages)
                {
                    int runEnd = runStartPage + runPageCount;
                    for (int i = runStartPage; i < runEnd; i++)
                        dirtyPageSnapshot[i] = UploadedDirtyPageSnapshotMarker;
                }

                stats.UploadedPages += runPageCount;
                stats.UploadedBytes += runBytes;
                stats.LockSpans++;

                if (stats.UploadedBytes >= byteBudget && pageIndex < pageCount)
                {
                    int deferred = CountDirtyPagesInRange(dirtyPageSnapshot, pageIndex, pageCount);
                    if (deferred > 0)
                    {
                        stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPageSnapshot, pageIndex, pageCount);
                        stats.DeferredPages += deferred;
                        RecordDeferredUploadPages(deferred);
                        return stats;
                    }
                }
            }

            return stats;
        }

        private static PageUploadStats CalculateDirtyPageUploadStats<T>(
            NativeArray<byte> dirtyPages,
            int safeCount,
            int pageSize,
            int maxBytesThisFrame)
            where T : struct
        {
            if (safeCount <= 0 || !dirtyPages.IsCreated)
                return CreateEmptyPageUploadStats();

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPages.Length, ResolveDirtyPageCount(safeCount, safePageSize));
            if (pageCount <= 0)
                return CreateEmptyPageUploadStats();

            int stride = UnsafeUtility.SizeOf<T>();
            long byteBudget = maxBytesThisFrame > 0 ? maxBytesThisFrame : long.MaxValue;
            PageUploadStats stats = CreateEmptyPageUploadStats();
            int pageIndex = 0;
            while (pageIndex < pageCount)
            {
                if (dirtyPages[pageIndex] == 0)
                {
                    pageIndex++;
                    continue;
                }

                int runStartPage = pageIndex;
                int runPageCount = 0;
                long runBytes = 0L;
                while (pageIndex < pageCount && dirtyPages[pageIndex] != 0)
                {
                    int pageElementStart = pageIndex * safePageSize;
                    int pageElementCount = math.min(safePageSize, safeCount - pageElementStart);
                    if (pageElementCount <= 0)
                    {
                        pageIndex++;
                        continue;
                    }

                    long pageBytes = (long)pageElementCount * stride;
                    bool overBudget = stats.UploadedBytes + runBytes + pageBytes > byteBudget;
                    if (overBudget && (stats.UploadedBytes > 0L || runPageCount > 0))
                        break;

                    runBytes += pageBytes;
                    runPageCount++;
                    pageIndex++;
                }

                if (runPageCount <= 0)
                {
                    stats.FirstDeferredPage = runStartPage;
                    stats.DeferredPages += CountDirtyPagesInRange(dirtyPages, runStartPage, pageCount);
                    return stats;
                }

                stats.UploadedPages += runPageCount;
                stats.UploadedBytes += runBytes;
                stats.LockSpans++;

                if (stats.UploadedBytes >= byteBudget && pageIndex < pageCount)
                {
                    int deferred = CountDirtyPagesInRange(dirtyPages, pageIndex, pageCount);
                    if (deferred > 0)
                    {
                        stats.FirstDeferredPage = ResolveFirstDirtyPage(dirtyPages, pageIndex, pageCount);
                        stats.DeferredPages += deferred;
                        return stats;
                    }
                }
            }

            return stats;
        }

        private static bool UploadNativeArrayRange<T>(GraphicsBuffer destination, NativeArray<T> source, int startIndex, int count) where T : struct
        {
            if (count <= 0)
                return false;

            return TryCopyNativeArrayRange(destination, source, startIndex, startIndex, count);
        }

        private static bool TryCopyNativeArrayRange<T>(
            GraphicsBuffer destination,
            NativeArray<T> source,
            int sourceStartIndex,
            int destinationStartIndex,
            int count)
            where T : struct
        {
            if (count <= 0)
                return false;

            bool copyAccepted = false;
            bool bufferLocked = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(destinationStartIndex, count);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, count))
                {
                    unsafe
                    {
                        int stride = UnsafeUtility.SizeOf<T>();
                        void* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + ((long)sourceStartIndex * stride);
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        long copyBytes = (long)stride * count;
                        long destinationBytes = (long)stride * mapped.Length;
                        copyAccepted = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                        if (!copyAccepted)
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(CopyGuardOwnerLabel);
                    }
                }
            }
            finally
            {
                if (bufferLocked)
                    destination.UnlockBufferAfterWrite<T>(count);
            }

            return copyAccepted;
        }

        private static unsafe bool TryCopyArrayRange<T>(
            GraphicsBuffer destination,
            T[] source,
            int sourceStartIndex,
            int destinationStartIndex,
            int count)
            where T : unmanaged
        {
            if (count <= 0)
                return false;

            bool copyAccepted = false;
            bool bufferLocked = false;
            NativeArray<T> mapped = default;
            try
            {
                mapped = destination.LockBufferForWrite<T>(destinationStartIndex, count);
                bufferLocked = true;
                if (LockYieldedRequestedCapacity(mapped, count))
                {
                    fixed (T* sourceBase = source)
                    {
                        int stride = UnsafeUtility.SizeOf<T>();
                        void* sourcePtr = (byte*)sourceBase + ((long)sourceStartIndex * stride);
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        long copyBytes = (long)stride * count;
                        long destinationBytes = (long)stride * mapped.Length;
                        copyAccepted = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                        if (!copyAccepted)
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(CopyGuardOwnerLabel);
                    }
                }
            }
            finally
            {
                if (bufferLocked)
                    destination.UnlockBufferAfterWrite<T>(count);
            }

            return copyAccepted;
        }

        // LockBufferForWrite can hand back no mapping at all, or a mapping shorter than the
        // requested element count, when there is no usable GPU device (headless -nographics),
        // when the driver refuses the map, or when the buffer went away underneath us. That is a
        // failed upload, not memory corruption, so callers must skip UnsafeMemoryCopyGuard
        // entirely in that case: a zero-length or short destination span is indistinguishable
        // from a genuine oversize-copy violation once it reaches the guard, and would raise the
        // critical FatalMemoryCorruptionException signal for a benign lock failure.
        private static bool LockYieldedRequestedCapacity<T>(NativeArray<T> mapped, int requestedCount) where T : struct
        {
            if (mapped.IsCreated && mapped.Length >= requestedCount)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_lockCapacityWarningLogged)
            {
                _lockCapacityWarningLogged = true;
                Debug.LogWarning(
                    $"[{CopyGuardOwnerLabel}] GraphicsBuffer.LockBufferForWrite<{typeof(T).Name}> did not yield the requested capacity " +
                    $"(requested {requestedCount}, mapped {mapped.Length}, created {mapped.IsCreated}). The upload was skipped and reported " +
                    "as failed. This is a buffer lock failure, not memory corruption. Further occurrences are not logged.");
            }
#endif

            return false;
        }

        private static int CountDirtyPagesInRange(NativeArray<byte> dirtyPages, int startPage, int pageCount)
        {
            int count = 0;
            for (int i = math.max(0, startPage); i < pageCount; i++)
            {
                if (dirtyPages[i] != 0)
                    count++;
            }

            return count;
        }

        private static int CountDirtyPagesInRange(byte[] dirtyPages, int startPage, int pageCount)
        {
            int count = 0;
            int limit = dirtyPages != null ? math.min(dirtyPages.Length, pageCount) : 0;
            for (int i = math.max(0, startPage); i < limit; i++)
            {
                if (dirtyPages[i] != 0 && dirtyPages[i] != UploadedDirtyPageSnapshotMarker)
                    count++;
            }

            return count;
        }

        private static int ResolveFirstDirtyPage(NativeArray<byte> dirtyPages, int startPage, int pageCount)
        {
            for (int i = math.max(0, startPage); i < pageCount; i++)
            {
                if (dirtyPages[i] != 0)
                    return i;
            }

            return -1;
        }

        private static int ResolveFirstDirtyPage(byte[] dirtyPages, int startPage, int pageCount)
        {
            int limit = dirtyPages != null ? math.min(dirtyPages.Length, pageCount) : 0;
            for (int i = math.max(0, startPage); i < limit; i++)
            {
                if (dirtyPages[i] != 0 && dirtyPages[i] != UploadedDirtyPageSnapshotMarker)
                    return i;
            }

            return -1;
        }

        private static long ResolveFrameUploadBudgetBytes(float globalQualityWeight01)
        {
            return ResolveFrameUploadBudgetBytes(globalQualityWeight01, 0f);
        }

        private static long ResolveFrameUploadBudgetBytes(float globalQualityWeight01, float pressureFactor01)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight01) ? globalQualityWeight01 : 0f);
            long qualityBudgetBytes;
            if (quality < 0.5f)
            {
                float t = quality * 2f;
                qualityBudgetBytes = (long)math.round(math.lerp(MinimumFrameUploadBudgetBytes, MiddleFrameUploadBudgetBytes, t));
            }
            else if (quality < 0.85f)
            {
                float t = (quality - 0.5f) / 0.35f;
                qualityBudgetBytes = (long)math.round(math.lerp(MiddleFrameUploadBudgetBytes, HighFrameUploadBudgetBytes, t));
            }
            else
            {
                float ultraT = (quality - 0.85f) / 0.15f;
                qualityBudgetBytes = (long)math.round(math.lerp(HighFrameUploadBudgetBytes, UltraFrameUploadBudgetBytes, ultraT));
            }

            float pressure = math.saturate(math.isfinite(pressureFactor01) ? pressureFactor01 : 0f);
            float pressureCollapse = math.smoothstep(0.55f, 0.98f, pressure);
            return (long)math.round(math.lerp(qualityBudgetBytes, MinimumFrameUploadBudgetBytes, pressureCollapse));
        }

        private static long ResolveAvailableUploadByteBudget(int localMaxBytesThisFrame)
        {
            long localBudget = localMaxBytesThisFrame > 0 ? localMaxBytesThisFrame : long.MaxValue;
            if (!_uploadBudgetActive)
                return localBudget;

            long remainingGlobalBudget = _uploadBudgetBytes - _claimedUploadBytes;
            if (remainingGlobalBudget <= 0L)
                return 0L;

            return Math.Min(localBudget, remainingGlobalBudget);
        }

        private static void RecordUploadClaim(long uploadedBytes)
        {
            if (!_uploadBudgetActive || uploadedBytes <= 0L)
                return;

            _claimedUploadBytes += uploadedBytes;
            _uploadedBytes += uploadedBytes;
            _uploadClaimCount++;
        }

        private static void RecordDeferredUploadPages(int deferredPages)
        {
            if (!_uploadBudgetActive || deferredPages <= 0)
                return;

            _deferredUploadPages += deferredPages;
        }

        private static PageUploadStats CreateEmptyPageUploadStats()
        {
            return new PageUploadStats
            {
                FirstDeferredPage = -1
            };
        }

        private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : struct
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;

            return ResolveSafeWriteCount<T>(destination.count, destination.stride, sourceLength, requestedCount);
        }

        private static int ResolveSafeWriteRangeCount<T>(
            GraphicsBuffer destination,
            int sourceLength,
            int sourceStartIndex,
            int destinationStartIndex,
            int requestedCount)
            where T : struct
        {
            if (destination == null ||
                requestedCount <= 0 ||
                sourceLength <= 0 ||
                sourceStartIndex < 0 ||
                destinationStartIndex < 0 ||
                destination.count <= 0 ||
                destination.stride != UnsafeUtility.SizeOf<T>() ||
                sourceStartIndex >= sourceLength ||
                destinationStartIndex >= destination.count)
            {
                return 0;
            }

            int sourceRemaining = sourceLength - sourceStartIndex;
            int destinationRemaining = destination.count - destinationStartIndex;
            return math.min(requestedCount, math.min(sourceRemaining, destinationRemaining));
        }

        private static bool CanCopyWholeBuffer(GraphicsBuffer source, GraphicsBuffer destination)
        {
            if (source == null || destination == null)
                return false;

            long sourceBytes = (long)source.count * source.stride;
            long destinationBytes = (long)destination.count * destination.stride;
            return sourceBytes > 0L && sourceBytes == destinationBytes;
        }

        private static int ResolveSafeWriteCount<T>(int destinationCount, int destinationStride, int sourceLength, int requestedCount) where T : struct
        {
            if (requestedCount <= 0 || sourceLength <= 0 || destinationCount <= 0)
                return 0;

            if (destinationStride != UnsafeUtility.SizeOf<T>())
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destinationCount);
        }
    }
}
