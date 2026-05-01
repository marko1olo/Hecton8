using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Central guard for raw native memory copies.
    /// </summary>
    public static unsafe class UnsafeMemoryCopyGuard
    {
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
            if (!CanCopy(destination, destinationSizeBytes, source, sourceSizeBytes))
                return false;

            UnsafeUtility.MemCpy(destination, source, sourceSizeBytes);
            GlobalTelemetryBus.RecordNativeCopy(sourceSizeBytes);
            return true;
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
            Debug.LogError("[UnsafeMemoryCopyGuard] Rejected out-of-bounds MemCpy in " + owner + ".");
        }
    }
}
