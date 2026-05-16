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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct ContentLoreBlockIndex
    {
        public uint Hash;
        public long Offset;
        public int Length;
    }

    /// <summary>
    /// Memory-mapped Babel_Dictionary.h8bin bridge. UI requests lore bytes by the same uint hash route as textures.
    /// </summary>
    public sealed class ContentLoreBinaryProvider : MonoBehaviour, IDisposable
    {
        public const int MaxSynchronousLoreReadBytes = 64 * 1024;

        [SerializeField] private string dictionaryRelativePath = "Babel_Dictionary.h8bin";
        [SerializeField] private ContentLoreBlockIndex[] blocks = Array.Empty<ContentLoreBlockIndex>();

        private bool _sorted;
#if UNITY_EDITOR || UNITY_STANDALONE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
#endif
        private FileStream _fallbackStream;
        private long _fileLength;

        public int BlockCount => blocks != null ? blocks.Length : 0;

        public ContentLoreBlockIndex GetBlockAt(int index)
        {
            EnsureSorted();
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
            if (!TryGetBlock(hash, out ContentLoreBlockIndex block))
                return false;

            if (!IsBlockReadable(block) || destination.Length < block.Length)
                return false;

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
                return false;

            _fallbackStream.Position = block.Offset;
            bytesWritten = _fallbackStream.Read(destination.Slice(0, block.Length));
            return bytesWritten == block.Length;
        }

        public bool Open()
        {
            Dispose();
            EnsureSorted();

            string path = Path.Combine(Application.streamingAssetsPath, dictionaryRelativePath);
            if (!File.Exists(path))
                path = Path.Combine(Application.dataPath, dictionaryRelativePath);

            if (!File.Exists(path))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[ContentLoreBinaryProvider] Babel dictionary missing: " + dictionaryRelativePath, this);
#endif
                return false;
            }

            _fileLength = new FileInfo(path).Length;
#if UNITY_EDITOR || UNITY_STANDALONE
            _mappedFile = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0L, MemoryMappedFileAccess.Read);
            _accessor = _mappedFile.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read);
#else
            _fallbackStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
#endif
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
            EnsureSorted();

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

        private void EnsureSorted()
        {
            if (_sorted || blocks == null)
                return;

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

            _sorted = true;
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
    }
}
