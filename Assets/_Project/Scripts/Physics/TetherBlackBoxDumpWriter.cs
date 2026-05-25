using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physics
{
    /// <summary>
    /// Fault-path only writer for tether blackbox rings. Hot telemetry remains vault-backed.
    /// </summary>
    internal static unsafe class TetherBlackBoxDumpWriter
    {
        private const int DumpHeaderBytes = 32;
        private const int DumpVersion = 1;
        private const int DumpSnapshotAlignment = 16;
        private const int DumpWorkerPollMilliseconds = 100;
        private const int DumpStateIdle = 0;
        private const int DumpStateSnapshotting = 1;
        private const int DumpStatePending = 2;
        private const int DumpStateWriting = 3;
        private const string DumpSnapshotOwner = nameof(TetherBlackBoxDumpWriter);
        private const string DumpSnapshotLabel = "DumpSnapshot";

        private static Thread s_dumpWorker;
        private static AutoResetEvent s_dumpSignal;
        private static IntPtr s_snapshot;
        private static int s_snapshotCapacityBytes;
        private static int s_snapshotRegistrationId;
        private static int s_pendingByteCount;
        private static int s_dumpState;
        private static string s_primaryPath;
        private static string s_legacyPath;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForSubsystemReload()
        {
            TryReleaseIdleWorkerState();
        }

        public static void WritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            if (!TryQueuePrimaryAndLegacy(primaryH8DumpPath, legacyBinPath, magic, ring, head, reasonFlags))
                TryWritePrimaryAndLegacy(primaryH8DumpPath, legacyBinPath, magic, ring, head, reasonFlags);
        }

        public static bool TryWritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            int entrySize = UnsafeUtility.SizeOf<T>();
            if (entrySize <= 0)
                return false;

            bool wrotePrimary = TryWritePrimaryH8Dump(primaryH8DumpPath, magic, ring, head, reasonFlags, entrySize);
            bool wroteLegacy = false;

            if (!string.IsNullOrEmpty(legacyBinPath) &&
                !string.Equals(primaryH8DumpPath, legacyBinPath, StringComparison.OrdinalIgnoreCase))
            {
                wroteLegacy = TryWriteLegacyMirror(legacyBinPath, magic, ring, head, reasonFlags, entrySize);
            }

            return wrotePrimary || wroteLegacy;
        }

        public static bool TryQueuePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            int entrySize = UnsafeUtility.SizeOf<T>();
            if (!TryResolveTotalDumpBytes(ring.Length, entrySize, out int totalBytes))
                return false;

            if (string.IsNullOrEmpty(primaryH8DumpPath) &&
                string.IsNullOrEmpty(legacyBinPath))
            {
                return false;
            }

            if (!EnsureDumpWorker())
                return false;

            if (Interlocked.CompareExchange(ref s_dumpState, DumpStateSnapshotting, DumpStateIdle) != DumpStateIdle)
                return false;

            try
            {
                if (!EnsureSnapshotCapacity(totalBytes))
                {
                    Volatile.Write(ref s_dumpState, DumpStateIdle);
                    return false;
                }

                WritePayload((byte*)s_snapshot.ToPointer(), magic, ring, head, reasonFlags, entrySize);
                s_primaryPath = primaryH8DumpPath;
                s_legacyPath = legacyBinPath;
                Volatile.Write(ref s_pendingByteCount, totalBytes);
                Thread.MemoryBarrier();
                Volatile.Write(ref s_dumpState, DumpStatePending);

                AutoResetEvent signal = s_dumpSignal;
                if (signal == null)
                {
                    ClearPendingDumpDescriptor();
                    Volatile.Write(ref s_dumpState, DumpStateIdle);
                    return false;
                }

                signal.Set();
                return true;
            }
            catch (ArgumentException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            ClearPendingDumpDescriptor();
            Volatile.Write(ref s_dumpState, DumpStateIdle);
            return false;
        }

        private static bool TryWritePrimaryH8Dump<T>(
            string path,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags,
            int entrySize) where T : unmanaged
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!TryResolveTotalDumpBytes(ring.Length, entrySize, out int totalBytes))
                return false;

            try
            {
                EnsureDirectory(path);
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough))
                {
                    WriteStreamPayload(stream, magic, ring, head, reasonFlags, entrySize);
                }

                return true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return false;
        }

        private static bool TryWriteLegacyMirror<T>(
            string path,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags,
            int entrySize) where T : unmanaged
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                EnsureDirectory(path);
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough))
                {
                    WriteStreamPayload(stream, magic, ring, head, reasonFlags, entrySize);
                }

                return true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return false;
        }

        private static bool EnsureDumpWorker()
        {
            AutoResetEvent signal = s_dumpSignal;
            Thread worker = s_dumpWorker;
            if (signal != null && worker != null && worker.IsAlive)
                return true;

            try
            {
                if (signal == null)
                {
                    signal = new AutoResetEvent(false);
                    s_dumpSignal = signal;
                }

                worker = s_dumpWorker;
                if (worker == null || !worker.IsAlive)
                {
                    worker = new Thread(DumpWorkerLoop)
                    {
                        IsBackground = true,
                        Name = "H8.Physics.TetherDump1303"
                    };
                    s_dumpWorker = worker;
                    worker.Start();
                }

                return true;
            }
            catch (ThreadStateException)
            {
                return false;
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
        }

        private static bool EnsureSnapshotCapacity(int totalBytes)
        {
            if (totalBytes <= DumpHeaderBytes)
                return false;

            if (s_snapshot != IntPtr.Zero && s_snapshotCapacityBytes >= totalBytes)
                return true;

            if (s_snapshot != IntPtr.Zero)
                ReleaseSnapshotBuffer();

            byte* snapshot = (byte*)UnsafeUtility.Malloc(totalBytes, DumpSnapshotAlignment, Allocator.Persistent);
            if (snapshot == null)
                return false;

            int registrationId = NativeMemorySentinel.RegisterPointer(
                snapshot,
                totalBytes,
                DumpSnapshotOwner,
                DumpSnapshotLabel,
                NativeAllocationLifetime.Session);
            if (registrationId <= 0)
            {
                UnsafeUtility.Free(snapshot, Allocator.Persistent);
                return false;
            }

            s_snapshot = (IntPtr)snapshot;
            s_snapshotCapacityBytes = totalBytes;
            s_snapshotRegistrationId = registrationId;
            return true;
        }

        private static void ReleaseSnapshotBuffer()
        {
            int registrationId = s_snapshotRegistrationId;
            if (registrationId > 0)
            {
                NativeMemorySentinel.Unregister(registrationId);
                s_snapshotRegistrationId = 0;
            }

            if (s_snapshot == IntPtr.Zero)
            {
                s_snapshotCapacityBytes = 0;
                return;
            }

            UnsafeUtility.Free((void*)s_snapshot, Allocator.Persistent);
            s_snapshot = IntPtr.Zero;
            s_snapshotCapacityBytes = 0;
        }

        private static void TryReleaseIdleWorkerState()
        {
            if (Volatile.Read(ref s_dumpState) != DumpStateIdle)
                return;

            AutoResetEvent signal = s_dumpSignal;
            Thread worker = s_dumpWorker;
            bool workerStopped = worker == null || !worker.IsAlive;
            s_dumpSignal = null;
            s_dumpWorker = null;
            s_primaryPath = null;
            s_legacyPath = null;
            Volatile.Write(ref s_pendingByteCount, 0);

            if (signal != null)
            {
                try
                {
                    signal.Set();
                }
                catch (ObjectDisposedException)
                {
                }

                if (worker != null &&
                    worker.IsAlive &&
                    worker.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
                {
                    workerStopped = TryJoinDumpWorker(worker);
                }

                TryDisposeDumpSignal(signal);
                if (!workerStopped &&
                    worker != null &&
                    worker.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
                {
                    workerStopped = TryJoinDumpWorker(worker);
                }
            }
            else if (worker != null &&
                     worker.IsAlive &&
                     worker.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                workerStopped = TryJoinDumpWorker(worker);
            }

            if (workerStopped ||
                worker == null ||
                !worker.IsAlive)
            {
                ReleaseSnapshotBuffer();
            }
        }

        private static void ClearPendingDumpDescriptor()
        {
            s_primaryPath = null;
            s_legacyPath = null;
            Volatile.Write(ref s_pendingByteCount, 0);
        }

        private static bool TryJoinDumpWorker(Thread worker)
        {
            try
            {
                return worker.Join(DumpWorkerPollMilliseconds);
            }
            catch (ThreadStateException)
            {
                return false;
            }
            catch (ThreadInterruptedException)
            {
                return false;
            }
        }

        private static void TryDisposeDumpSignal(AutoResetEvent signal)
        {
            try
            {
                signal.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static void DumpWorkerLoop()
        {
            while (true)
            {
                AutoResetEvent signal = s_dumpSignal;
                if (signal == null)
                    return;

                try
                {
                    signal.WaitOne(DumpWorkerPollMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                DrainPendingDump();
            }
        }

        private static void DrainPendingDump()
        {
            if (Interlocked.CompareExchange(ref s_dumpState, DumpStateWriting, DumpStatePending) != DumpStatePending)
                return;

            string primaryPath = s_primaryPath;
            string legacyPath = s_legacyPath;
            TryWriteQueuedDumpFile(primaryPath, FileMode.Create);
            if (!string.IsNullOrEmpty(legacyPath) &&
                !string.Equals(primaryPath, legacyPath, StringComparison.OrdinalIgnoreCase))
            {
                TryWriteQueuedDumpFile(legacyPath, FileMode.Append);
            }

            ClearPendingDumpDescriptor();
            Volatile.Write(ref s_dumpState, DumpStateIdle);
        }

        private static bool TryWriteQueuedDumpFile(string path, FileMode mode)
        {
            int byteCount = Volatile.Read(ref s_pendingByteCount);
            if (string.IsNullOrEmpty(path) ||
                s_snapshot == IntPtr.Zero ||
                byteCount <= DumpHeaderBytes ||
                byteCount > s_snapshotCapacityBytes)
            {
                return false;
            }

            try
            {
                EnsureDirectory(path);
                using (FileStream stream = new FileStream(
                    path,
                    mode,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough))
                {
                    ref byte first = ref UnsafeUtility.AsRef<byte>((void*)s_snapshot);
                    stream.Write(MemoryMarshal.CreateReadOnlySpan(ref first, byteCount));
                }

                return true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return false;
        }

        private static bool TryResolveTotalDumpBytes(int entryCount, int entrySize, out int totalBytes)
        {
            totalBytes = 0;
            long payloadBytes = (long)entryCount * entrySize;
            long totalBytes64 = DumpHeaderBytes + payloadBytes;
            if (entryCount <= 0 ||
                entrySize <= 0 ||
                payloadBytes <= 0L ||
                totalBytes64 > int.MaxValue)
            {
                return false;
            }

            totalBytes = (int)totalBytes64;
            return true;
        }

        private static void WriteStreamPayload<T>(
            FileStream stream,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags,
            int entrySize) where T : unmanaged
        {
            Span<byte> header = stackalloc byte[DumpHeaderBytes];
            fixed (byte* headerPtr = header)
                WriteHeader(headerPtr, magic, ring.Length, entrySize, head, reasonFlags);

            stream.Write(header);

            byte* source = (byte*)ring.GetUnsafeReadOnlyPtr();
            int normalizedHead = NormalizeHead(head, ring.Length);
            for (int i = 0; i < ring.Length; i++)
            {
                int sourceIndex = normalizedHead + i;
                if (sourceIndex >= ring.Length)
                    sourceIndex -= ring.Length;

                ref byte first = ref UnsafeUtility.AsRef<byte>(source + sourceIndex * entrySize);
                stream.Write(MemoryMarshal.CreateReadOnlySpan(ref first, entrySize));
            }
        }

        private static void WritePayload<T>(
            byte* destination,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags,
            int entrySize) where T : unmanaged
        {
            WriteHeader(destination, magic, ring.Length, entrySize, head, reasonFlags);

            byte* payload = destination + DumpHeaderBytes;
            byte* source = (byte*)ring.GetUnsafeReadOnlyPtr();
            int normalizedHead = NormalizeHead(head, ring.Length);
            for (int i = 0; i < ring.Length; i++)
            {
                int sourceIndex = normalizedHead + i;
                if (sourceIndex >= ring.Length)
                    sourceIndex -= ring.Length;

                UnsafeUtility.MemCpy(
                    payload + i * entrySize,
                    source + sourceIndex * entrySize,
                    entrySize);
            }
        }

        private static void WriteHeader(
            byte* destination,
            ulong magic,
            int entryCount,
            int entrySize,
            int head,
            uint reasonFlags)
        {
            ulong* header64 = (ulong*)destination;
            header64[0] = magic;

            int* header32 = (int*)(destination + sizeof(ulong));
            header32[0] = DumpVersion;
            header32[1] = entryCount;
            header32[2] = entrySize;
            header32[3] = NormalizeHead(head, entryCount);

            uint* tail32 = (uint*)(destination + sizeof(ulong) + sizeof(int) * 4);
            tail32[0] = reasonFlags;
            tail32[1] = DumpHeaderBytes;
        }

        private static int NormalizeHead(int head, int capacity)
        {
            if (capacity <= 0)
                return 0;

            int normalized = head % capacity;
            return normalized < 0 ? normalized + capacity : normalized;
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
