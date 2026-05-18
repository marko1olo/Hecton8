using System;
using System.IO;
#if UNITY_STANDALONE || UNITY_EDITOR
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
#endif
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

        public static void WritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            if (!ring.IsCreated || ring.Length <= 0)
                return;

            int entrySize = UnsafeUtility.SizeOf<T>();
            if (entrySize <= 0)
                return;

            WritePrimaryH8Dump(primaryH8DumpPath, magic, ring, head, reasonFlags, entrySize);

            if (!string.IsNullOrEmpty(legacyBinPath) &&
                !string.Equals(primaryH8DumpPath, legacyBinPath, StringComparison.OrdinalIgnoreCase))
            {
                WriteLegacyMirror(legacyBinPath, magic, ring, head, reasonFlags, entrySize);
            }
        }

        private static void WritePrimaryH8Dump<T>(
            string path,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags,
            int entrySize) where T : unmanaged
        {
            if (string.IsNullOrEmpty(path))
                return;

            int totalBytes = DumpHeaderBytes + ring.Length * entrySize;
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
#if UNITY_STANDALONE || UNITY_EDITOR
                    stream.SetLength(totalBytes);
                    using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                               stream,
                               null,
                               totalBytes,
                               MemoryMappedFileAccess.ReadWrite,
                               HandleInheritability.None,
                               false))
                    using (MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(
                               0L,
                               totalBytes,
                               MemoryMappedFileAccess.Write))
                    {
                        byte* destination = null;
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);
                        try
                        {
                            WritePayload(destination, magic, ring, head, reasonFlags, entrySize);
                        }
                        finally
                        {
                            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
#else
                    WriteStreamPayload(stream, magic, ring, head, reasonFlags, entrySize);
#endif
                }
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
        }

        private static void WriteLegacyMirror<T>(
            string path,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags,
            int entrySize) where T : unmanaged
        {
            if (string.IsNullOrEmpty(path))
                return;

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

                stream.Write(new ReadOnlySpan<byte>(source + sourceIndex * entrySize, entrySize));
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
