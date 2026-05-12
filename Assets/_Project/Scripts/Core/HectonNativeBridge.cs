using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Central native-library availability gate. Runtime owners must fail closed and use managed fallbacks when a plugin is missing.
    /// </summary>
    [Flags]
    public enum HectonNativeLibrary : uint
    {
        None = 0u,
        Lz4 = 1u << 0,
        AudioKernel = 1u << 1,
        Steamworks = 1u << 2
    }

    /// <summary>
    /// Last observed native plugin failure class.
    /// </summary>
    public enum HectonNativeFailure : byte
    {
        None = 0,
        DllNotFound = 1,
        EntryPointMissing = 2,
        BadImageFormat = 3,
        RuntimeError = 4
    }

    /// <summary>
    /// Zero-allocation safe-load state for first-party and SDK native plugins.
    /// </summary>
    public static class HectonNativeBridge
    {
        private static int _unavailableMask;
        private static int _lastFailureLibrary;
        private static int _lastFailureCode;

        /// <summary>
        /// Returns true until a native library has failed to load or bind during this process.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAvailable(HectonNativeLibrary library)
        {
            uint mask = (uint)library;
            return mask != 0u && ((uint)Volatile.Read(ref _unavailableMask) & mask) == 0u;
        }

        /// <summary>
        /// Marks a native library unavailable for the current process.
        /// </summary>
        public static void MarkUnavailable(HectonNativeLibrary library, HectonNativeFailure failure)
        {
            uint mask = (uint)library;
            if (mask == 0u)
                return;

            int current;
            int next;
            do
            {
                current = Volatile.Read(ref _unavailableMask);
                next = current | unchecked((int)mask);
            }
            while (Interlocked.CompareExchange(ref _unavailableMask, next, current) != current);
            Volatile.Write(ref _lastFailureLibrary, unchecked((int)mask));
            Volatile.Write(ref _lastFailureCode, (int)failure);
        }

        /// <summary>
        /// Marks a native library unavailable from a managed plugin-bind exception.
        /// </summary>
        public static void MarkUnavailableFromException(HectonNativeLibrary library, Exception exception)
        {
            MarkUnavailable(library, ResolveFailure(exception));
        }

        /// <summary>
        /// Returns true when the exception represents a platform plugin load/bind failure.
        /// </summary>
        public static bool IsNativeLoadFailure(Exception exception)
        {
            return exception is DllNotFoundException ||
                   exception is EntryPointNotFoundException ||
                   exception is BadImageFormatException;
        }

        /// <summary>
        /// Last failed library mask for diagnostics.
        /// </summary>
        public static HectonNativeLibrary LastFailureLibrary =>
            (HectonNativeLibrary)Volatile.Read(ref _lastFailureLibrary);

        /// <summary>
        /// Last native failure type for diagnostics.
        /// </summary>
        public static HectonNativeFailure LastFailure =>
            (HectonNativeFailure)Volatile.Read(ref _lastFailureCode);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Volatile.Write(ref _unavailableMask, 0);
            Volatile.Write(ref _lastFailureLibrary, 0);
            Volatile.Write(ref _lastFailureCode, 0);
        }

        private static HectonNativeFailure ResolveFailure(Exception exception)
        {
            if (exception is DllNotFoundException)
                return HectonNativeFailure.DllNotFound;

            if (exception is EntryPointNotFoundException)
                return HectonNativeFailure.EntryPointMissing;

            if (exception is BadImageFormatException)
                return HectonNativeFailure.BadImageFormat;

            return HectonNativeFailure.RuntimeError;
        }
    }
}
