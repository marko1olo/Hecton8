using System;
using System.IO;
#if UNITY_EDITOR || UNITY_STANDALONE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.Core.Content
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ContentLoreBlockIndex
    {
        [FieldOffset(0)] public long Offset;
        [FieldOffset(8)] public uint Hash;
        [FieldOffset(12)] public int Length;
    }

    /// <summary>
    /// Memory-mapped Babel_Dictionary.h8bin bridge. UI requests lore bytes by the same uint hash route as textures.
    /// </summary>
    public sealed class ContentLoreBinaryProvider : MonoBehaviour, IDisposable
    {
        public const int MaxSynchronousLoreReadBytes = 64 * 1024;
        private const byte SortStateUnknown = 0;
        private const byte SortStateSorted = 1;
        private const byte SortStateUnsorted = 2;
        private const int FileStreamBufferBytes = 64 * 1024;

        [SerializeField] private string dictionaryRelativePath = "Babel_Dictionary.h8bin";
        [SerializeField] private ContentLoreBlockIndex[] blocks = Array.Empty<ContentLoreBlockIndex>();

        private byte _sortState;
#if UNITY_EDITOR || UNITY_STANDALONE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
#endif
        private FileStream _fallbackStream;
        private long _fileLength;

        public int BlockCount => blocks != null ? blocks.Length : 0;
        public string DictionaryRelativePath => dictionaryRelativePath;

        public ContentLoreBlockIndex GetBlockAt(int index)
        {
            return blocks[index];
        }

        private void Awake()
        {
            Open();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public bool TryReadBlock(uint hash, Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (hash == 0u)
            {
                LogRejectedZeroHashRead();
                return false;
            }

            if (!TryGetBlock(hash, out ContentLoreBlockIndex block))
            {
                LogMissingLoreBlock(hash);
                return false;
            }

            if (!IsBlockReadable(block))
            {
                LogUnreadableLoreBlock(hash, block.Offset, block.Length, _fileLength);
                return false;
            }

            if (destination.Length < block.Length)
            {
                LogLoreDestinationTooSmall(hash, destination.Length, block.Length);
                return false;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (_accessor != null)
            {
                for (int i = 0; i < block.Length; i++)
                    destination[i] = _accessor.ReadByte(block.Offset + i);

                bytesWritten = block.Length;
                return true;
            }
#endif
            if (_fallbackStream == null)
            {
                LogLoreStreamUnavailable(hash);
                return false;
            }

            _fallbackStream.Position = block.Offset;
            Span<byte> target = destination.Slice(0, block.Length);
            int totalRead = 0;
            while (totalRead < block.Length)
            {
                int read = _fallbackStream.Read(target.Slice(totalRead));
                if (read <= 0)
                    break;

                totalRead += read;
            }

            if (totalRead != block.Length)
            {
                LogPartialLoreRead(hash, totalRead, block.Length);
                return false;
            }

            bytesWritten = block.Length;
            return true;
        }

        public bool Open()
        {
            Dispose();
            RefreshSortStateCold();

            if (!TryResolveDictionaryPath(dictionaryRelativePath, out string path))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Babel dictionary missing.", this);
#endif
                return false;
            }

            try
            {
                _fileLength = new FileInfo(path).Length;
#if UNITY_EDITOR || UNITY_STANDALONE
                _mappedFile = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0L, MemoryMappedFileAccess.Read);
                _accessor = _mappedFile.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read);
#else
                _fallbackStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes);
#endif
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Failed to open Babel dictionary.", this);
#endif
                return false;
            }

            return true;
        }

        public void Dispose()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (_accessor != null)
            {
                _accessor.Dispose();
                _accessor = null;
            }

            if (_mappedFile != null)
            {
                _mappedFile.Dispose();
                _mappedFile = null;
            }
#endif
            if (_fallbackStream != null)
            {
                _fallbackStream.Dispose();
                _fallbackStream = null;
            }

            _fileLength = 0L;
        }

        private bool TryGetBlock(uint hash, out ContentLoreBlockIndex block)
        {
            if (_sortState != SortStateSorted)
                return TryGetBlockLinear(hash, out block);

            int lo = 0;
            int hi = blocks != null ? blocks.Length - 1 : -1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                uint midHash = blocks[mid].Hash;
                if (midHash == hash)
                {
                    block = blocks[mid];
                    return true;
                }

                if (midHash < hash)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            block = default;
            return false;
        }

        private void RefreshSortStateCold()
        {
            _sortState = IsSortedAscending() ? SortStateSorted : SortStateUnsorted;
        }

#if UNITY_EDITOR
        private void SortBlocks()
        {
            if (blocks == null)
            {
                _sortState = SortStateSorted;
                return;
            }

            for (int i = 1; i < blocks.Length; i++)
            {
                ContentLoreBlockIndex current = blocks[i];
                int j = i - 1;
                while (j >= 0 && blocks[j].Hash > current.Hash)
                {
                    blocks[j + 1] = blocks[j];
                    j--;
                }

                blocks[j + 1] = current;
            }

            _sortState = SortStateSorted;
        }
#endif

        private bool IsSortedAscending()
        {
            if (blocks == null || blocks.Length < 2)
                return true;

            uint previous = blocks[0].Hash;
            for (int i = 1; i < blocks.Length; i++)
            {
                uint current = blocks[i].Hash;
                if (current < previous)
                    return false;

                previous = current;
            }

            return true;
        }

        private bool TryGetBlockLinear(uint hash, out ContentLoreBlockIndex block)
        {
            int count = blocks != null ? blocks.Length : 0;
            for (int i = 0; i < count; i++)
            {
                if (blocks[i].Hash != hash)
                    continue;

                block = blocks[i];
                return true;
            }

            block = default;
            return false;
        }

        private bool IsBlockReadable(ContentLoreBlockIndex block)
        {
            if (block.Offset < 0L || block.Length <= 0 || block.Length > MaxSynchronousLoreReadBytes)
                return false;

            long end = block.Offset + block.Length;
            if (end < block.Offset)
                return false;

            return _fileLength <= 0L || end <= _fileLength;
        }

        private static bool TryResolveDictionaryPath(string relativePath, out string path)
        {
            path = null;
            if (!IsPortableDictionaryRelativePath(relativePath))
                return false;

            path = global::Hecton8.Core.HectonPersistentPathPolicy.CombineFile(relativePath);
            if (File.Exists(path))
                return true;

            if (TryResolveFileUnder(Application.streamingAssetsPath, relativePath, out path))
                return true;

            return TryResolveFileUnder(Application.dataPath, relativePath, out path);
        }

        public static bool IsPortableDictionaryRelativePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                IsCompressedPackagePath(relativePath) ||
                relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }

            int length = relativePath.Length;
            int segmentStart = 0;
            bool sawSegment = false;
            for (int i = 0; i <= length; i++)
            {
                if (i < length && relativePath[i] != '/' && relativePath[i] != '\\')
                    continue;

                int segmentLength = i - segmentStart;
                if (segmentLength <= 0)
                    return false;

                if (segmentLength == 1 && relativePath[segmentStart] == '.')
                    return false;

                if (segmentLength == 2 &&
                    relativePath[segmentStart] == '.' &&
                    relativePath[segmentStart + 1] == '.')
                {
                    return false;
                }

                sawSegment = true;
                segmentStart = i + 1;
            }

            return sawSegment;
        }

        private static bool TryResolveFileUnder(string root, string relativePath, out string path)
        {
            path = null;
            if (string.IsNullOrEmpty(root) || IsCompressedPackagePath(root))
                return false;

            string rootPath;
            string candidate;
            try
            {
                rootPath = Path.GetFullPath(root);
                candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath));
                if (!IsUnderRoot(rootPath, candidate))
                    return false;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                return false;
            }

            if (!File.Exists(candidate))
                return false;

            path = candidate;
            return true;
        }

        private static bool IsUnderRoot(string root, string candidate)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(candidate))
                return false;

            char last = root[root.Length - 1];
            string normalizedRoot = last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar
                ? root
                : root + Path.DirectorySeparatorChar;

            return candidate.StartsWith(normalizedRoot, StringComparison.Ordinal);
        }

        private static bool IsCompressedPackagePath(string path)
        {
            return path.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogRejectedZeroHashRead()
        {
            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Rejected zero hash lore read.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogMissingLoreBlock(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Missing lore block.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogUnreadableLoreBlock(uint hash, long offset, int length, long fileLength)
        {
            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Unreadable lore block.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogLoreDestinationTooSmall(uint hash, int destinationLength, int requiredLength)
        {
            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Destination span too small for lore.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogLoreStreamUnavailable(uint hash)
        {
            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] No readable Babel dictionary stream.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPartialLoreRead(uint hash, int readLength, int requiredLength)
        {
            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Partial lore read.", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SortBlocks();
        }
#endif
    }
}
