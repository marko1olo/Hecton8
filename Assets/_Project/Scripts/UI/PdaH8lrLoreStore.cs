#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_PDA_H8LR_MMF_AVAILABLE
#endif

using System;
using System.IO;
#if HECTON8_PDA_H8LR_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    internal static class PdaH8lrLoreStoreLayout
    {
        public const int PdaH8lrHeaderDTOStrideBytes = 16;
        public const int PdaH8lrRecordDTOStrideBytes = 16;
    }

    [StructLayout(LayoutKind.Explicit, Size = PdaH8lrLoreStoreLayout.PdaH8lrHeaderDTOStrideBytes)]
    internal struct PdaH8lrHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint Count;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = PdaH8lrLoreStoreLayout.PdaH8lrRecordDTOStrideBytes)]
    internal struct PdaH8lrRecordDTO
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint ByteOffset;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint Reserved0;
    }

    internal sealed unsafe class PdaH8lrLoreStore : IDisposable
    {
        public const uint MagicH8lr = 0x524C3848u;
        public const uint CurrentVersion = 1u;
        public const int HeaderSizeBytes = 16;
        public const int RecordSizeBytes = 16;

        private const int MaxRecordCount = 4096;
        private const int FileStreamBufferBytes = 64 * 1024;
        private const int VaultMirrorCopyChunkBytes = 8 * 1024;
        private const SystemID VaultOwnerSystemId = SystemID.UI;

#if HECTON8_PDA_H8LR_MMF_AVAILABLE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _accessor;
#endif
        private FileStream _fileStream;
        private int _mappedBytes;
        private int _entryCount;
        private uint _btreeOffset;
        private uint _btreeRootOffset;
        private uint _btreeEndOffset;
        private uint _btreeNodeCount;
        private IDataVault _vault;
        private VaultGenerationHandle<byte> _vaultMirrorHandle;
        private int _vaultMirrorLength;
        private bool _vaultMirrorBacked;
        private bool _btreeAvailable;

        public bool IsOpen => _vaultMirrorBacked && _mappedBytes >= HeaderSizeBytes && _entryCount > 0;
        public int EntryCount => _entryCount;
        public int MappedBytes => _mappedBytes;
        public bool IsVaultMirrorBacked => _vaultMirrorBacked;

        public bool OpenDefault(IDataVault vault, in VaultGenerationHandle<byte> vaultMirrorHandle)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Data", "Lore", "Encyclopedia.h8bin"));
            return Open(path, vault, in vaultMirrorHandle);
        }

        public bool Open(string path, IDataVault vault, in VaultGenerationHandle<byte> vaultMirrorHandle)
        {
            Dispose();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (info.Length < HeaderSizeBytes || info.Length > int.MaxValue)
                return false;

            return TryOpenVaultMirror(path, (int)info.Length, vault, in vaultMirrorHandle);
        }

        public bool TryGetUtf8(uint hash, out ReadOnlySpan<byte> utf8)
        {
            utf8 = ReadOnlySpan<byte>.Empty;
            if (!TryResolveReadableBasePointer(out byte* basePointer, out int mappedBytes) || hash == 0u)
                return false;

            if (!_btreeAvailable ||
                !H8CacheBTree.TryFindValue(
                    basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    hash,
                    ResolveGlobalQualityWeight(),
                    out uint recordIndex,
                    out _,
                    out _,
                    out _) ||
                recordIndex >= _entryCount)
            {
                return false;
            }

            PdaH8lrRecordDTO record = ReadRecord(basePointer, (int)recordIndex);
            if (record.Hash != hash || !IsRecordInBounds(in record, mappedBytes))
                return false;

            utf8 = MemoryMarshal.CreateReadOnlySpan(ref UnsafeUtility.AsRef<byte>(basePointer + record.ByteOffset), (int)record.ByteLength);
            return true;
        }

        public bool TryGetRecord(int index, out PdaH8lrRecordDTO record)
        {
            record = default;
            if (!TryResolveReadableBasePointer(out byte* basePointer, out int mappedBytes) ||
                (uint)index >= (uint)_entryCount)
                return false;

            record = ReadRecord(basePointer, index);
            return IsRecordInBounds(in record, mappedBytes);
        }

        public void Dispose()
        {
#if HECTON8_PDA_H8LR_MMF_AVAILABLE
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
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            _mappedBytes = 0;
            _entryCount = 0;
            _btreeOffset = 0u;
            _btreeRootOffset = 0u;
            _btreeEndOffset = 0u;
            _btreeNodeCount = 0u;
            _vault = null;
            _vaultMirrorHandle = default;
            _vaultMirrorLength = 0;
            _vaultMirrorBacked = false;
            _btreeAvailable = false;
        }

#if HECTON8_PDA_H8LR_MMF_AVAILABLE
        private bool TryOpenMemoryMapped(string path, int fileBytes)
        {
            return false;
        }
#endif

        private bool TryOpenVaultMirror(
            string path,
            int fileBytes,
            IDataVault vault,
            in VaultGenerationHandle<byte> vaultMirrorHandle)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultMirrorHandle(in vaultMirrorHandle) ||
                fileBytes <= 0)
            {
                return false;
            }

            try
            {
                Span<byte> chunk = stackalloc byte[VaultMirrorCopyChunkBytes];
                int totalRead = 0;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))
                {
                    while (totalRead < fileBytes)
                    {
                        int read = stream.Read(chunk.Slice(0, math.min(chunk.Length, fileBytes - totalRead)));
                        if (read <= 0)
                            break;

                        if (!TryCopyVaultMirrorChunk(vault, in vaultMirrorHandle, totalRead, chunk.Slice(0, read), fileBytes))
                            return false;

                        totalRead += read;
                    }
                }

                if (totalRead != fileBytes)
                    return false;

                return TryCommitVaultMirror(vault, in vaultMirrorHandle, fileBytes);
            }
            catch (IOException)
            {
                ClearMappedState();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                ClearMappedState();
                return false;
            }
            catch (NotSupportedException)
            {
                ClearMappedState();
                return false;
            }
            catch (ArgumentException)
            {
                ClearMappedState();
                return false;
            }
            catch (ObjectDisposedException)
            {
                ClearMappedState();
                return false;
            }
        }

        private static bool TryCopyVaultMirrorChunk(
            IDataVault vault,
            in VaultGenerationHandle<byte> vaultMirrorHandle,
            int destinationOffset,
            ReadOnlySpan<byte> source,
            int requiredBytes)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultMirrorHandle(in vaultMirrorHandle) ||
                source.Length <= 0 ||
                destinationOffset < 0 ||
                requiredBytes <= 0 ||
                destinationOffset > requiredBytes - source.Length)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in vaultMirrorHandle, VaultOwnerSystemId, out NativeArray<byte> vaultMirror))
                return false;

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vaultMirror.IsCreated ||
                    vaultMirror.Length < requiredBytes)
                {
                    return false;
                }

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(vaultMirror) + destinationOffset;
                Span<byte> destinationSpan = MemoryMarshal.CreateSpan(ref UnsafeUtility.AsRef<byte>(destination), source.Length);
                source.CopyTo(destinationSpan);
                return !vault.IsCompactionFenceActive;
            }
            finally
            {
                vault.ReleaseWriteLock(in vaultMirrorHandle, VaultOwnerSystemId);
            }
        }

        private bool TryCommitVaultMirror(
            IDataVault vault,
            in VaultGenerationHandle<byte> vaultMirrorHandle,
            int fileBytes)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultMirrorHandle(in vaultMirrorHandle) ||
                fileBytes <= 0)
            {
                ClearMappedState();
                return false;
            }

            if (!vault.TryAcquireWriteLock(in vaultMirrorHandle, VaultOwnerSystemId, out NativeArray<byte> vaultMirror))
            {
                ClearMappedState();
                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vaultMirror.IsCreated ||
                    vaultMirror.Length < fileBytes)
                {
                    ClearMappedState();
                    return false;
                }

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(vaultMirror);
                if (!ValidateMappedBytes(destination, fileBytes) ||
                    vault.IsCompactionFenceActive)
                {
                    ClearMappedState();
                    return false;
                }

                _mappedBytes = fileBytes;
                _vault = vault;
                _vaultMirrorHandle = vaultMirrorHandle;
                _vaultMirrorLength = vaultMirror.Length;
                _vaultMirrorBacked = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in vaultMirrorHandle, VaultOwnerSystemId);
            }
        }

        private void ClearMappedState()
        {
            _mappedBytes = 0;
            _entryCount = 0;
            _btreeOffset = 0u;
            _btreeRootOffset = 0u;
            _btreeEndOffset = 0u;
            _btreeNodeCount = 0u;
            _vault = null;
            _vaultMirrorHandle = default;
            _vaultMirrorLength = 0;
            _vaultMirrorBacked = false;
            _btreeAvailable = false;
        }

        private bool ValidateMappedBytes(byte* basePointer, int mappedBytes)
        {
            if (basePointer == null || mappedBytes < HeaderSizeBytes)
                return false;

            PdaH8lrHeaderDTO header = default;
            header.Magic = ReadUInt32LittleEndian(basePointer, 0);
            header.Version = ReadUInt32LittleEndian(basePointer, 4);
            header.Count = ReadUInt32LittleEndian(basePointer, 8);
            header.Reserved0 = ReadUInt32LittleEndian(basePointer, 12);

            if (header.Magic != MagicH8lr ||
                header.Version != CurrentVersion ||
                header.Count == 0u ||
                header.Count > MaxRecordCount ||
                header.Reserved0 != 0u)
            {
                return false;
            }

            int count = (int)header.Count;
            int recordTableBytes = HeaderSizeBytes + (count * RecordSizeBytes);
            if (recordTableBytes > mappedBytes)
                return false;

            uint previousHash = 0u;
            uint payloadStart = uint.MaxValue;
            for (int i = 0; i < count; i++)
            {
                PdaH8lrRecordDTO record = ReadRecordUnchecked(basePointer, i);
                if (record.Reserved0 != 0u ||
                    record.Hash == 0u ||
                    (i > 0 && record.Hash <= previousHash) ||
                    (record.ByteOffset & 15u) != 0u ||
                    record.ByteLength == 0u ||
                    !IsRecordInBounds(in record, mappedBytes))
                {
                    return false;
                }

                payloadStart = math.min(payloadStart, record.ByteOffset);
                previousHash = record.Hash;
            }

            if (!H8CacheBTree.TryResolveTree(
                    H8StaticDataFormat.CacheBTreeFlag,
                    HeaderSizeBytes,
                    (uint)count,
                    RecordSizeBytes,
                    payloadStart,
                    out _btreeOffset,
                    out _btreeRootOffset,
                    out _btreeNodeCount))
            {
                return false;
            }

            _btreeEndOffset = payloadStart;
            _entryCount = count;
            if (!ValidateBTreeEdge(basePointer))
                return false;

            _btreeAvailable = true;
            return true;
        }

        private bool ValidateBTreeEdge(byte* basePointer)
        {
            PdaH8lrRecordDTO first = ReadRecordUnchecked(basePointer, 0);
            PdaH8lrRecordDTO last = ReadRecordUnchecked(basePointer, _entryCount - 1);
            return H8CacheBTree.TryFindValue(
                    basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    first.Hash,
                    0f,
                    out uint firstIndex,
                    out _,
                    out _,
                    out _) &&
                firstIndex == 0u &&
                H8CacheBTree.TryFindValue(
                    basePointer,
                    _btreeOffset,
                    _btreeRootOffset,
                    _btreeEndOffset,
                    last.Hash,
                    0f,
                    out uint lastIndex,
                    out _,
                    out _,
                    out _) &&
                lastIndex == (uint)(_entryCount - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PdaH8lrRecordDTO ReadRecord(byte* basePointer, int index)
        {
            return (uint)index < (uint)_entryCount ? ReadRecordUnchecked(basePointer, index) : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PdaH8lrRecordDTO ReadRecordUnchecked(byte* basePointer, int index)
        {
            int offset = HeaderSizeBytes + (index * RecordSizeBytes);
            return new PdaH8lrRecordDTO
            {
                Hash = ReadUInt32LittleEndian(basePointer, offset),
                ByteOffset = ReadUInt32LittleEndian(basePointer, offset + 4),
                ByteLength = ReadUInt32LittleEndian(basePointer, offset + 8),
                Reserved0 = ReadUInt32LittleEndian(basePointer, offset + 12)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsRecordInBounds(in PdaH8lrRecordDTO record, int mappedBytes)
        {
            if (record.ByteOffset > int.MaxValue || record.ByteLength > int.MaxValue)
                return false;

            int offset = (int)record.ByteOffset;
            int length = (int)record.ByteLength;
            if (offset < HeaderSizeBytes || length <= 0)
                return false;

            int end = offset + length;
            return end >= offset && end <= mappedBytes;
        }

        private bool TryResolveReadableBasePointer(out byte* basePointer, out int mappedBytes)
        {
            basePointer = null;
            mappedBytes = _mappedBytes;
            if (_vaultMirrorBacked)
            {
                IDataVault vault = _vault;
                if (vault == null ||
                    vault.IsCompactionFenceActive ||
                    !IsVaultMirrorHandle(in _vaultMirrorHandle) ||
                    _vaultMirrorLength < _mappedBytes ||
                    !vault.TryReadOnlyHandle(in _vaultMirrorHandle, out NativeArray<byte>.ReadOnly vaultMirror) ||
                    vault.IsCompactionFenceActive ||
                    !vaultMirror.IsCreated ||
                    vaultMirror.Length < _mappedBytes)
                {
                    basePointer = null;
                    mappedBytes = 0;
                    return false;
                }

                basePointer = (byte*)vaultMirror.GetUnsafeReadOnlyPtr();
                mappedBytes = _mappedBytes;
            }

            return basePointer != null && mappedBytes >= HeaderSizeBytes && _entryCount > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVaultMirrorHandle(in VaultGenerationHandle<byte> handle)
        {
            return handle.BufferID == PDAEncyclopediaStreamer.H8lrMirrorBufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32LittleEndian(byte* bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }
    }
}
