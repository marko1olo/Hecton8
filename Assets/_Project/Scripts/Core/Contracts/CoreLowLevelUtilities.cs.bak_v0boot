using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public sealed class FatalMemoryCorruptionException : Exception
    {
        public FatalMemoryCorruptionException(string message) : base(message)
        {
        }
    }

    public static unsafe class UnsafeMemoryCopyGuard
    {
        private const string RejectedCopyMessage = "[UnsafeMemoryCopyGuard] Rejected unsafe native copy: source bytes exceed destination capacity or pointer/size is invalid.";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeCopy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            if (sourceSizeBytes < 0L || destinationSizeBytes < 0L)
                return RejectInvalidCopy();

            if (sourceSizeBytes == 0L)
                return true;

            if (destination == null || source == null)
                return RejectInvalidCopy();

            if (sourceSizeBytes > destinationSizeBytes)
                return RejectInvalidCopy();

            if (RangesOverlap(destination, source, sourceSizeBytes))
                UnsafeUtility.MemMove(destination, source, sourceSizeBytes);
            else
                UnsafeUtility.MemCpy(destination, source, sourceSizeBytes);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMemCpy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            return SafeCopy(destination, destinationSizeBytes, source, sourceSizeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanCopy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            return destination != null &&
                   source != null &&
                   sourceSizeBytes >= 0L &&
                   destinationSizeBytes >= 0L &&
                   sourceSizeBytes <= destinationSizeBytes;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void ReportRejectedCopy(string owner)
        {
            _ = owner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RejectInvalidCopy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new FatalMemoryCorruptionException(RejectedCopyMessage);
#else
            return false;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RangesOverlap(void* destination, void* source, long byteCount)
        {
            if (byteCount <= 0L || destination == source)
                return destination == source && byteCount > 0L;

            ulong destinationStart = (ulong)destination;
            ulong sourceStart = (ulong)source;
            ulong size = (ulong)byteCount;
            ulong destinationEnd = destinationStart + size;
            ulong sourceEnd = sourceStart + size;

            if (destinationEnd < destinationStart || sourceEnd < sourceStart)
                return true;

            return destinationStart < sourceEnd && sourceStart < destinationEnd;
        }
    }

    public static unsafe class NativeFaultDumpWriter
    {
        private const int MaxTrackedTransientPayloads = 512;
        private const string TransientPayloadRegistrationFailureMessage = "NativeMemoryTrackingBridge registration failed for NativeFaultDumpWriter transient payload.";
        private const string TransientPayloadUnregistrationFailureMessage = "NativeMemoryTrackingBridge unregistration failed for NativeFaultDumpWriter transient payload.";
        private static readonly object s_transientPayloadRegistrationGate = new object();
        private static readonly IntPtr[] s_transientPayloadPointers = new IntPtr[MaxTrackedTransientPayloads];
        private static readonly int[] s_transientPayloadRegistrationIds = new int[MaxTrackedTransientPayloads];

        public static NativeArray<byte> CreateTransientPayload(
            int byteCount,
            string owner,
            string label,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory,
            Allocator allocator = Allocator.Temp)
        {
            if (byteCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            NativeArray<byte> payload = default;
            bool registered = false;
            IntPtr payloadPointer = IntPtr.Zero;
            try
            {
                payload = new NativeArray<byte>(byteCount, allocator, options);
                payloadPointer = ResolveTransientPayloadPointer(payload);
                registered = TryRegisterTransientNativeArrayPayload(payload, owner, label, allocator);
                if (!registered)
                    throw new InvalidOperationException(TransientPayloadRegistrationFailureMessage);

                return payload;
            }
            catch
            {
                try
                {
                    if (payload.IsCreated)
                        payload.Dispose();
                }
                catch (Exception disposalException)
                {
                    if (registered && !payload.IsCreated && !TryUnregisterTransientNativeArrayPayload(payloadPointer))
                    {
                        throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);
                    }

                    throw;
                }

                if (registered && !TryUnregisterTransientNativeArrayPayload(payloadPointer))
                    throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);

                throw;
            }
        }

        public static void DisposeTransientPayload(
            ref NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator = Allocator.Temp)
        {
            if (!payload.IsCreated)
                return;

            IntPtr payloadPointer = ResolveTransientPayloadPointer(payload);
            int registrationId = TryGetTransientPayloadRegistrationId(payloadPointer);
            if (registrationId <= 0)
            {
                payload.Dispose();
                payload = default;
                throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);
            }

            try
            {
                payload.Dispose();
                payload = default;
            }
            catch (Exception disposalException)
            {
                if (!payload.IsCreated && !TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId))
                {
                    throw new AggregateException(TransientPayloadUnregistrationFailureMessage, disposalException);
                }

                throw;
            }

            bool unregistered = TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId);
            if (!unregistered)
                throw new InvalidOperationException(TransientPayloadUnregistrationFailureMessage);
        }

        private static bool TryRegisterTransientNativeArrayPayload(
            NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator)
        {
            if (!payload.IsCreated || !Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
                return false;

            int registrationId = Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArrayInstance(
                payload,
                owner,
                label,
                ResolveBridgeLifetime(allocator));
            if (registrationId <= 0)
                return false;

            if (TryRememberTransientPayloadRegistration(ResolveTransientPayloadPointer(payload), registrationId))
                return true;

            Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(registrationId);
            return false;
        }

        private static bool TryUnregisterTransientNativeArrayPayload(IntPtr payloadPointer)
        {
            int registrationId = TryGetTransientPayloadRegistrationId(payloadPointer);
            if (registrationId <= 0)
                return false;

            return TryUnregisterTransientNativeArrayPayload(payloadPointer, registrationId);
        }

        private static bool TryUnregisterTransientNativeArrayPayload(IntPtr payloadPointer, int registrationId)
        {
            if (!Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
                return false;

            if (!TryForgetTransientPayloadRegistration(payloadPointer, registrationId))
                return false;

            Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(registrationId);
            return true;
        }

        private static IntPtr ResolveTransientPayloadPointer(NativeArray<byte> payload)
        {
            if (!payload.IsCreated)
                return IntPtr.Zero;

            return (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload);
        }

        private static bool TryRememberTransientPayloadRegistration(IntPtr payloadPointer, int registrationId)
        {
            if (payloadPointer == IntPtr.Zero || registrationId <= 0)
                return false;

            lock (s_transientPayloadRegistrationGate)
            {
                int emptyIndex = -1;
                for (int i = 0; i < MaxTrackedTransientPayloads; i++)
                {
                    IntPtr currentPointer = s_transientPayloadPointers[i];
                    if (currentPointer == payloadPointer)
                        return false;

                    if (emptyIndex < 0 && currentPointer == IntPtr.Zero)
                        emptyIndex = i;
                }

                if (emptyIndex < 0)
                    return false;

                s_transientPayloadPointers[emptyIndex] = payloadPointer;
                s_transientPayloadRegistrationIds[emptyIndex] = registrationId;
                return true;
            }
        }

        private static int TryGetTransientPayloadRegistrationId(IntPtr payloadPointer)
        {
            if (payloadPointer == IntPtr.Zero)
                return 0;

            lock (s_transientPayloadRegistrationGate)
            {
                for (int i = 0; i < MaxTrackedTransientPayloads; i++)
                {
                    if (s_transientPayloadPointers[i] == payloadPointer)
                        return s_transientPayloadRegistrationIds[i];
                }
            }

            return 0;
        }

        private static bool TryForgetTransientPayloadRegistration(IntPtr payloadPointer, int registrationId)
        {
            if (payloadPointer == IntPtr.Zero || registrationId <= 0)
                return false;

            lock (s_transientPayloadRegistrationGate)
            {
                for (int i = 0; i < MaxTrackedTransientPayloads; i++)
                {
                    if (s_transientPayloadPointers[i] != payloadPointer ||
                        s_transientPayloadRegistrationIds[i] != registrationId)
                    {
                        continue;
                    }

                    s_transientPayloadPointers[i] = IntPtr.Zero;
                    s_transientPayloadRegistrationIds[i] = 0;
                    return true;
                }
            }

            return false;
        }

        private static Hecton8.Core.Contracts.NativeMemoryBridgeLifetime ResolveBridgeLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.Temp;
                case Allocator.TempJob:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.TempJob;
                default:
                    throw new ArgumentOutOfRangeException(nameof(allocator), "Fault-dump transient payloads support Temp or TempJob allocators only.");
            }
        }

        public static bool TryWriteAll(string absolutePath, NativeArray<byte> payload, int byteCount)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !payload.IsCreated ||
                byteCount <= 0 ||
                byteCount > payload.Length)
            {
                return false;
            }

            try
            {
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload);
                return TryWriteAll(absolutePath, new ReadOnlySpan<byte>(source, byteCount), byteCount);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryWriteAll(string absolutePath, ReadOnlySpan<byte> payload, int byteCount)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                byteCount <= 0 ||
                byteCount > payload.Length)
            {
                return false;
            }

            if (!TryResolveWritablePath(absolutePath, out string fullPath))
                return false;

            string tempPath = fullPath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                TryDeleteFaultDumpTempFile(tempPath);
                bool tempLengthMatched;
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(payload.Slice(0, byteCount));
                    stream.Flush(true);
                    tempLengthMatched = stream.Length == byteCount;
                }

                if (!tempLengthMatched)
                {
                    TryDeleteFaultDumpTempFile(tempPath);
                    return false;
                }

                if (!TryFlushAndValidateFaultDumpFile(tempPath, byteCount))
                {
                    TryDeleteFaultDumpTempFile(tempPath);
                    return false;
                }

                if (File.Exists(fullPath))
                    File.Replace(tempPath, fullPath, null, true);
                else
                    File.Move(tempPath, fullPath);

                return TryFlushAndValidateFaultDumpFile(fullPath, byteCount);
            }
            catch
            {
                TryDeleteFaultDumpTempFile(tempPath);
                return false;
            }
        }

        private static bool TryFlushAndValidateFaultDumpFile(string path, long expectedBytes)
        {
            if (string.IsNullOrWhiteSpace(path) || expectedBytes <= 0L)
                return false;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    if (stream.Length != expectedBytes)
                        return false;

                    stream.Flush(true);
                    return stream.Length == expectedBytes;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteFaultDumpTempFile(string tempPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
        }

        private static bool TryResolveWritablePath(string requestedPath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(requestedPath))
                return false;

            try
            {
                if (Path.IsPathRooted(requestedPath))
                {
                    fullPath = Path.GetFullPath(requestedPath);
                    return true;
                }

                if (!TryNormalizeRelativePath(requestedPath, out string relativePath))
                    return false;

#if UNITY_EDITOR
                string dataPath = UnityEngine.Application.dataPath;
                string projectRoot = !string.IsNullOrEmpty(dataPath)
                    ? Directory.GetParent(dataPath)?.FullName
                    : null;
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
                    return true;
                }
#endif
                string persistentRoot = Application.persistentDataPath;
                if (string.IsNullOrEmpty(persistentRoot))
                    persistentRoot = System.Environment.CurrentDirectory;

                fullPath = Path.GetFullPath(Path.Combine(persistentRoot, relativePath));
                return true;
            }
            catch
            {
                fullPath = null;
                return false;
            }
        }

        private static bool TryNormalizeRelativePath(string requestedPath, out string relativePath)
        {
            relativePath = null;
            string normalized = requestedPath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (normalized.Length <= 0 || ContainsTraversalSegment(normalized))
            {
                return false;
            }

            relativePath = normalized;
            return true;
        }

        private static bool ContainsTraversalSegment(string path)
        {
            int segmentStart = 0;
            for (int i = 0; i <= path.Length; i++)
            {
                bool atSeparator = i == path.Length || path[i] == Path.DirectorySeparatorChar;
                if (!atSeparator)
                    continue;

                int segmentLength = i - segmentStart;
                if (segmentLength == 2 &&
                    path[segmentStart] == '.' &&
                    path[segmentStart + 1] == '.')
                {
                    return true;
                }

                segmentStart = i + 1;
            }

            return false;
        }
    }

    public static class DispatcherJobFence
    {
        private const string IllegalForcedCompletionWarningMessage = "[DispatcherJobFence] Forced JobHandle.Complete outside dispatcher swap window.";

        private static int _activeSwapWindowDepth;
        private static int _illegalForcedCompletionWarningLogged;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPreSimulationSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPreSimulationSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginLateFrameSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLateFrameSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPostFixedSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPostFixedSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPostSimulationSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPostSimulationSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
            if (forceComplete && !handle.IsCompleted && !IsInsideSwapWindow())
                WarnIllegalForcedCompletion();

            if (!forceComplete && !handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFinalizeCompleted(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInsideSwapWindow()
        {
            return Volatile.Read(ref _activeSwapWindowDepth) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WarnIllegalForcedCompletion()
        {
            if (Interlocked.Exchange(ref _illegalForcedCompletionWarningLogged, 1) != 0)
                return;

            Debug.LogWarning(IllegalForcedCompletionWarningMessage);
        }
    }

    public struct MpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private NativeArray<long> _publishedTickets;
        private NativeArray<MpscSignalRingCursorState> _cursor;
        private string _ownerName;
        private Hecton8.Core.Contracts.NativeMemoryBridgeLifetime _bridgeLifetime;
        private int _bufferRegistrationId;
        private int _publishedTicketsRegistrationId;
        private int _cursorRegistrationId;
        private int _mask;
        private int _capacity;

        public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator)
            : this(requestedCapacity, allocator, null)
        {
        }

        public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator, object owner)
        {
            _buffer = default;
            _publishedTickets = default;
            _cursor = default;
            _ownerName = ResolveOwnerName(owner);
            _bridgeLifetime = ResolveBridgeLifetime(allocator);
            _bufferRegistrationId = 0;
            _publishedTicketsRegistrationId = 0;
            _cursorRegistrationId = 0;
            _mask = 0;
            _capacity = 0;

            try
            {
                int capacity = CeilPowerOfTwo(math.max(2, requestedCapacity));
                _buffer = new NativeArray<T>(capacity, allocator, NativeArrayOptions.UninitializedMemory);
                _publishedTickets = new NativeArray<long>(capacity, allocator, NativeArrayOptions.ClearMemory);
                _cursor = new NativeArray<MpscSignalRingCursorState>(1, allocator, NativeArrayOptions.ClearMemory);
                _mask = capacity - 1;
                _capacity = capacity;

                if (!_buffer.IsCreated || !_publishedTickets.IsCreated || !_cursor.IsCreated)
                {
                    Dispose();
                    return;
                }

                RegisterNativeArrays(allocator);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsCreated => _buffer.IsCreated && _publishedTickets.IsCreated && _cursor.IsCreated;
        public int Capacity => _capacity;

        public unsafe int Count
        {
            get
            {
                if (!TryGetCursor(out MpscSignalRingCursorState* cursor))
                    return 0;

                long head = Volatile.Read(ref cursor->Head);
                long tail = Volatile.Read(ref cursor->Tail);
                long count = ResolveCursorDistance(head, tail, _capacity);
                if (count <= 0L)
                    return 0;

                return count >= _capacity ? _capacity : (int)count;
            }
        }

        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(_buffer, _publishedTickets, _cursor, _mask, _capacity);
        }

        public bool TryEnqueue(in T signal)
        {
            return AsParallelWriter().TryEnqueue(in signal);
        }

        public unsafe bool TryDequeue(out T signal)
        {
            signal = default;
            if (!IsCreated || !TryGetCursor(out MpscSignalRingCursorState* cursor))
                return false;

            long head = Volatile.Read(ref cursor->Head);
            long tail = Volatile.Read(ref cursor->Tail);
            if (tail <= head || head == long.MaxValue)
                return false;

            int slot = (int)head & _mask;
            long expectedTicket = head + 1L;
            long* published = (long*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_publishedTickets);
            if (Volatile.Read(ref published[slot]) != expectedTicket)
                return false;

            signal = _buffer[slot];
            Interlocked.Exchange(ref published[slot], 0L);
            Interlocked.Exchange(ref cursor->Head, head + 1L);
            return true;
        }

        public unsafe void Clear()
        {
            if (!TryGetCursor(out MpscSignalRingCursorState* cursor))
                return;

            long tail = Volatile.Read(ref cursor->Tail);
            Interlocked.Exchange(ref cursor->Head, tail);
        }

        public void Dispose()
        {
            DisposeRegisteredNativeArray(ref _buffer, ref _bufferRegistrationId);
            DisposeRegisteredNativeArray(ref _publishedTickets, ref _publishedTicketsRegistrationId);
            DisposeRegisteredNativeArray(ref _cursor, ref _cursorRegistrationId);

            _buffer = default;
            _publishedTickets = default;
            _cursor = default;
            _ownerName = null;
            _bridgeLifetime = default;
            _bufferRegistrationId = 0;
            _publishedTicketsRegistrationId = 0;
            _cursorRegistrationId = 0;
            _mask = 0;
            _capacity = 0;
        }

        private unsafe bool TryGetCursor(out MpscSignalRingCursorState* cursor)
        {
            if (_cursor.IsCreated && _cursor.Length > 0)
            {
                cursor = (MpscSignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                return cursor != null;
            }

            cursor = null;
            return false;
        }

        private static int CeilPowerOfTwo(int value)
        {
            value = math.clamp(value, 2, 1 << 30);
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        private static string ResolveOwnerName(object owner)
        {
            if (owner == null)
                return DefaultOwnerName;

            string ownerName = owner.ToString();
            return string.IsNullOrWhiteSpace(ownerName)
                ? owner.GetType().Name
                : ownerName;
        }

        private const string DefaultOwnerName = "CoreDataVault";
        private const string BufferLabel = "MpscSignalRingBuffer.buffer";
        private const string PublishedTicketsLabel = "MpscSignalRingBuffer.publishedTickets";
        private const string CursorLabel = "MpscSignalRingBuffer.cursor";
        private const string BridgeUnavailableMessage = "Native memory tracking bridge is not installed for MpscSignalRingBuffer.";

        private void RegisterNativeArrays(Allocator allocator)
        {
            if (!Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
                throw new InvalidOperationException(BridgeUnavailableMessage);

            _bridgeLifetime = ResolveBridgeLifetime(allocator);
            _bufferRegistrationId = RegisterNativeArray(_buffer, BufferLabel, _bridgeLifetime);
            _publishedTicketsRegistrationId = RegisterNativeArray(_publishedTickets, PublishedTicketsLabel, _bridgeLifetime);
            _cursorRegistrationId = RegisterNativeArray(_cursor, CursorLabel, _bridgeLifetime);
        }

        private int RegisterNativeArray<TArray>(
            NativeArray<TArray> array,
            string label,
            Hecton8.Core.Contracts.NativeMemoryBridgeLifetime lifetime)
            where TArray : struct
        {
            if (!array.IsCreated)
                return 0;

            int registrationId = Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArrayInstance(
                array,
                _ownerName,
                ResolveTypedLabel(label),
                lifetime);
            if (registrationId > 0)
                return registrationId;

            throw new InvalidOperationException(
                $"Native memory tracking bridge registration failed for {_ownerName}.{ResolveTypedLabel(label)}.");
        }

        private static void DisposeRegisteredNativeArray<TArray>(ref NativeArray<TArray> array, ref int registrationId)
            where TArray : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
                array = default;
            }

            UnregisterNativeArray(ref registrationId);
        }

        private static void UnregisterNativeArray(ref int registrationId)
        {
            if (registrationId <= 0)
                return;

            Hecton8.Core.Contracts.NativeMemoryTrackingBridge.Unregister(registrationId);
            registrationId = 0;
        }

        private static Hecton8.Core.Contracts.NativeMemoryBridgeLifetime ResolveBridgeLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.Temp;
                case Allocator.TempJob:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.TempJob;
                default:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.Session;
            }
        }

        private static string ResolveTypedLabel(string label)
        {
            string typeName = typeof(T).FullName;
            return string.IsNullOrWhiteSpace(typeName)
                ? label
                : typeName + "." + label;
        }

        private static long ResolveCursorDistance(long head, long tail, int capacity)
        {
            if (capacity <= 0 || tail <= head)
                return 0L;

            long count = tail - head;
            return count < 0L ? capacity : count;
        }

        public struct ParallelWriter
        {
            [NativeDisableParallelForRestriction] private NativeArray<T> _buffer;
            [NativeDisableParallelForRestriction] private NativeArray<long> _publishedTickets;
            [NativeDisableParallelForRestriction] private NativeArray<MpscSignalRingCursorState> _cursor;
            private int _mask;
            private int _capacity;

            internal ParallelWriter(
                NativeArray<T> buffer,
                NativeArray<long> publishedTickets,
                NativeArray<MpscSignalRingCursorState> cursor,
                int mask,
                int capacity)
            {
                _buffer = buffer;
                _publishedTickets = publishedTickets;
                _cursor = cursor;
                _mask = mask;
                _capacity = capacity;
            }

            public bool IsCreated => _buffer.IsCreated && _publishedTickets.IsCreated && _cursor.IsCreated;

            public unsafe bool TryEnqueue(in T signal)
            {
                if (!IsCreated || _capacity <= 0 || _cursor.Length == 0)
                    return false;

                MpscSignalRingCursorState* cursor = (MpscSignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                long* published = (long*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_publishedTickets);
                while (true)
                {
                    long head = Volatile.Read(ref cursor->Head);
                    long tail = Volatile.Read(ref cursor->Tail);
                    if (!HasWritableCapacity(head, tail, _capacity))
                        return false;

                    long nextTail = tail + 1L;
                    if (Interlocked.CompareExchange(ref cursor->Tail, nextTail, tail) != tail)
                        continue;

                    int slot = (int)tail & _mask;
                    _buffer[slot] = signal;
                    Interlocked.Exchange(ref published[slot], tail + 1L);
                    return true;
                }
            }

            private static bool HasWritableCapacity(long head, long tail, int capacity)
            {
                if (capacity <= 0 || tail < head || tail == long.MaxValue)
                    return false;

                long count = tail - head;
                return count >= 0L && count < capacity;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct MpscSignalRingCursorState
    {
        [FieldOffset(0)] public long Head;
        [FieldOffset(8)] private ulong _headPad0;
        [FieldOffset(16)] private ulong _headPad1;
        [FieldOffset(24)] private ulong _headPad2;
        [FieldOffset(32)] private ulong _headPad3;
        [FieldOffset(40)] private ulong _headPad4;
        [FieldOffset(48)] private ulong _headPad5;
        [FieldOffset(56)] private ulong _headPad6;
        [FieldOffset(64)] public long Tail;
        [FieldOffset(72)] private ulong _tailPad0;
        [FieldOffset(80)] private ulong _tailPad1;
        [FieldOffset(88)] private ulong _tailPad2;
        [FieldOffset(96)] private ulong _tailPad3;
        [FieldOffset(104)] private ulong _tailPad4;
        [FieldOffset(112)] private ulong _tailPad5;
        [FieldOffset(120)] private ulong _tailPad6;
    }
}
