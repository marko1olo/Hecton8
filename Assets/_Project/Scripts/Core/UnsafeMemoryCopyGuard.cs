using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fatal development-time signal for a byte-copy that would corrupt native memory.
    /// </summary>
    public sealed class FatalMemoryCorruptionException : Exception
    {
        /// <summary>
        /// Creates a fatal native-memory corruption exception.
        /// </summary>
        /// <param name="message">Failure context.</param>
        public FatalMemoryCorruptionException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Central guard for raw native memory copies.
    /// </summary>
    public static unsafe class UnsafeMemoryCopyGuard
    {
        private const string RejectedCopyMessage = "[UnsafeMemoryCopyGuard] Rejected unsafe native copy: source bytes exceed destination capacity or pointer/size is invalid.";

        /// <summary>
        /// Copies native memory only when the source byte range fits the destination byte range.
        /// </summary>
        /// <param name="destination">Destination pointer.</param>
        /// <param name="destinationSizeBytes">Writable destination byte capacity from the current destination offset.</param>
        /// <param name="source">Source pointer.</param>
        /// <param name="sourceSizeBytes">Readable source byte count.</param>
        /// <returns>True when the copy executed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeCopy(
            void* destination,
            long destinationSizeBytes,
            void* source,
            long sourceSizeBytes)
        {
            if (sourceSizeBytes < 0L || destinationSizeBytes < 0L)
                return RejectInvalidCopy();

            if (sourceSizeBytes == 0L)
                return true;

            if (destination == null || source == null)
                return RejectInvalidCopy();

            long copySizeBytes = sourceSizeBytes;
            if (sourceSizeBytes > destinationSizeBytes)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new FatalMemoryCorruptionException(RejectedCopyMessage);
#else
                copySizeBytes = destinationSizeBytes;
#endif
            }

            if (copySizeBytes <= 0L)
                return false;

            UnsafeUtility.MemCpy(destination, source, copySizeBytes);
            GlobalTelemetryBus.RecordNativeCopy(copySizeBytes);
            return copySizeBytes == sourceSizeBytes;
        }

        /// <summary>
        /// Backward-compatible alias for guarded native copies.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMemCpy(
            void* destination,
            long destinationSizeBytes,
            void* source,
            long sourceSizeBytes)
        {
            return SafeCopy(destination, destinationSizeBytes, source, sourceSizeBytes);
        }

        /// <summary>
        /// Returns whether a native copy is inside bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanCopy(
            void* destination,
            long destinationSizeBytes,
            void* source,
            long sourceSizeBytes)
        {
            return destination != null &&
                   source != null &&
                   sourceSizeBytes >= 0L &&
                   destinationSizeBytes >= 0L &&
                   sourceSizeBytes <= destinationSizeBytes;
        }

        /// <summary>
        /// Reports an unsafe native copy attempt from a cold or failure path.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void ReportRejectedCopy(string owner)
        {
            Hecton8.Core.H8Debug.LogError(RejectedCopyMessage);
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
    }
}
