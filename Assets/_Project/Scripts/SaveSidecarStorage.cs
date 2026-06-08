using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveSidecarStorage
    {
        private const int NullCollectionCount = -1;
        private const SystemID NativeArrayOwnerSystem = SystemID.SavePersistence;
        private const string NativeMemoryAllocationFailureMessage = "H8Memory allocation failed for SaveSidecarStorage temp buffer.";
        private const string NativeMemoryReleaseFailureMessage = "H8Memory release failed for SaveSidecarStorage temp buffer.";
        private const string MetadataWriteBufferLabel = "metadataWriteBuffer";
        private const string MetadataReadBufferLabel = "metadataReadBuffer";
        private const string MaintenanceWriteBufferLabel = "maintenanceWriteBuffer";
        private const string MaintenanceReadBufferLabel = "maintenanceReadBuffer";
        private static string s_persistentDataPathRoot;

        internal static void SetPersistentDataPathRoot(string path)
        {
            if (!string.IsNullOrEmpty(path))
                s_persistentDataPathRoot = path;
        }

        internal static bool Exists(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath) && File.Exists(ToAbsolutePath(relativePath));
        }

        internal static bool Delete(string relativePath)
        {
            if (!Exists(relativePath))
                return false;

            string absolutePath = ToAbsolutePath(relativePath);
            AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            try
            {
                File.Delete(absolutePath);
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            }

            return true;
        }

        internal static bool SaveMetadata(SaveMetadata metadata, string relativePath, out string error)
        {
            error = string.Empty;
            if (metadata == null)
            {
                error = "Save metadata payload is null.";
                return false;
            }

            if (string.IsNullOrEmpty(relativePath))
            {
                error = "Metadata sidecar path is empty.";
                return false;
            }

            string sceneName = Hecton8.SaveSystem.SaveMetadata.NormalizeSceneName(metadata.SceneName);
            if (!TryResolveMetadataByteCount(metadata, out int byteCount, out error))
                return false;

            string absolutePath = ToAbsolutePath(relativePath);

            NativeArray<byte> buffer = AllocateTempNativeArrayBuffer(byteCount, MetadataWriteBufferLabel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[byteCount] — metadata sidecar write staging buffer — owner: SaveSidecarStorage
            try
            {
                byte* bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                SidecarWriter writer = new SidecarWriter(bufferPtr, byteCount);
                if (!writer.WriteString(metadata.SlotName)
                    || !writer.WriteString(metadata.GameVersion)
                    || !writer.WriteLong(metadata.Timestamp)
                    || !writer.WriteFloat(metadata.PlayTimeSeconds)
                    || !writer.WriteString(sceneName)
                    || !writer.WriteFloat(metadata.PlayerPosition.x)
                    || !writer.WriteFloat(metadata.PlayerPosition.y)
                    || !writer.WriteFloat(metadata.PlayerPosition.z)
                    || !writer.WriteString(metadata.Checksum)
                    || !writer.WriteInt(metadata.WorldSeed)
                    || !writer.WriteInt(metadata.WorldGenerationVersionId))
                {
                    error = writer.Error;
                    return false;
                }

                return WriteSidecarAtomically(absolutePath, bufferPtr, byteCount, "Metadata", out error);
            }
            finally
            {
                DisposeTempNativeArrayBuffer(ref buffer, MetadataWriteBufferLabel);
            }
        }

        internal static bool LoadMetadata(string relativePath, out SaveMetadata metadata, out string error)
        {
            metadata = null;
            error = string.Empty;
            if (!Exists(relativePath))
            {
                error = "Metadata sidecar does not exist.";
                return false;
            }

            string absolutePath = ToAbsolutePath(relativePath);
            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength < 0 || fileLength > int.MaxValue)
            {
                error = "Metadata sidecar exceeds the supported range.";
                return false;
            }

            NativeArray<byte> buffer = AllocateTempNativeArrayBuffer((int)fileLength, MetadataReadBufferLabel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[fileLength] — metadata sidecar read staging buffer — owner: SaveSidecarStorage
            try
            {
                byte* bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                if (!AsyncWriteManager.TryReadAll(absolutePath, bufferPtr, (int)fileLength, out error))
                    return false;

                SidecarReader reader = new SidecarReader(bufferPtr, (int)fileLength);
                SaveMetadata loaded = new SaveMetadata();
                if (!reader.ReadString(out loaded.SlotName)
                    || !reader.ReadString(out loaded.GameVersion)
                    || !reader.ReadLong(out loaded.Timestamp)
                    || !reader.ReadFloat(out loaded.PlayTimeSeconds)
                    || !reader.ReadString(out loaded.SceneName)
                    || !reader.ReadFloat(out float playerPosX)
                    || !reader.ReadFloat(out float playerPosY)
                    || !reader.ReadFloat(out float playerPosZ)
                    || !reader.ReadString(out loaded.Checksum)
                    || !FinalizeMetadata(ref loaded, playerPosX, playerPosY, playerPosZ, reader, out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = reader.Error;
                    metadata = null;
                    return false;
                }

                loaded.SceneName = Hecton8.SaveSystem.SaveMetadata.NormalizeSceneName(loaded.SceneName);
                metadata = loaded;
                return true;
            }
            finally
            {
                DisposeTempNativeArrayBuffer(ref buffer, MetadataReadBufferLabel);
            }
        }

        internal static bool SaveMaintenanceRecord(SaveSlotMaintenanceRecord record, out string error)
        {
            error = string.Empty;
            if (record == null || string.IsNullOrEmpty(record.SlotName))
            {
                error = "Maintenance record payload is invalid.";
                return false;
            }

            if (!TryResolveMaintenanceByteCount(record, out int byteCount, out error))
                return false;

            string absolutePath = ToAbsolutePath(SaveSlotMaintenanceRecord.GetPath(record.SlotName));

            NativeArray<byte> buffer = AllocateTempNativeArrayBuffer(byteCount, MaintenanceWriteBufferLabel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[byteCount] — maintenance sidecar write staging buffer — owner: SaveSidecarStorage
            try
            {
                byte* bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                SidecarWriter writer = new SidecarWriter(bufferPtr, byteCount);
                if (!writer.WriteString(record.SlotName)
                    || !writer.WriteLong(record.LastSuccessfulSaveTicksUtc)
                    || !writer.WriteLong(record.LastSuccessfulLoadTicksUtc)
                    || !writer.WriteLong(record.LastAuditTicksUtc)
                    || !writer.WriteLong(record.LastRepairTicksUtc)
                    || !writer.WriteLong(record.LastFailureTicksUtc)
                    || !writer.WriteInt(record.SuccessfulSaveCount)
                    || !writer.WriteInt(record.SuccessfulLoadCount)
                    || !writer.WriteInt(record.AuditCount)
                    || !writer.WriteInt(record.RepairCount)
                    || !writer.WriteInt(record.FailureCount)
                    || !writer.WriteByte(record.PackStateFlags())
                    || !writer.WriteInt(record.LastLoadBackupGeneration)
                    || !writer.WriteInt(record.LastKnownSaveVersion)
                    || !writer.WriteString(record.LastKnownIntegrityState)
                    || !writer.WriteString(record.LastFailureContext)
                    || !writer.WriteString(record.LastFailureMessage)
                    || !writer.WriteString(record.LastAuditMessage)
                    || !writer.WriteString(record.LastRepairMessage))
                {
                    error = writer.Error;
                    return false;
                }

                return WriteSidecarAtomically(absolutePath, bufferPtr, byteCount, "Maintenance", out error);
            }
            finally
            {
                DisposeTempNativeArrayBuffer(ref buffer, MaintenanceWriteBufferLabel);
            }
        }

        private static bool WriteSidecarAtomically(string absolutePath, void* bufferPtr, int byteCount, string sidecarName, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || bufferPtr == null || byteCount <= 0)
            {
                error = $"{sidecarName} sidecar write request is invalid.";
                return false;
            }

            string tempPath = absolutePath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(tempPath))
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                    try
                    {
                        File.Delete(tempPath);
                    }
                    finally
                    {
                        AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                    }
                }

                if (!AsyncWriteManager.WriteAll(tempPath, bufferPtr, byteCount, out error))
                    return false;

                if (!AsyncWriteManager.TryGetFileLength(tempPath, out long tempBytes, out string lengthError))
                {
                    error = string.IsNullOrEmpty(lengthError)
                        ? $"{sidecarName} sidecar temp file length could not be resolved."
                        : lengthError;
                    return false;
                }

                if (tempBytes != byteCount)
                {
                    error = $"{sidecarName} sidecar temp byte count mismatch.";
                    return false;
                }

                if (!AsyncWriteManager.FlushCriticalSavePath(tempPath, tempBytes, out string flushError))
                {
                    error = string.IsNullOrEmpty(flushError)
                        ? $"{sidecarName} sidecar temp critical flush failed."
                        : flushError;
                    return false;
                }

                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
                if (File.Exists(absolutePath))
                    File.Replace(tempPath, absolutePath, null);
                else
                    File.Move(tempPath, absolutePath);
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);

                if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long promotedBytes, out lengthError))
                {
                    error = string.IsNullOrEmpty(lengthError)
                        ? $"{sidecarName} sidecar promoted file length could not be resolved."
                        : lengthError;
                    return false;
                }

                if (promotedBytes != byteCount)
                {
                    error = $"{sidecarName} sidecar promoted byte count mismatch.";
                    return false;
                }

                if (!AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out flushError))
                {
                    error = string.IsNullOrEmpty(flushError)
                        ? $"{sidecarName} sidecar promoted critical flush failed."
                        : flushError;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"{sidecarName} sidecar atomic write failed: {ex.Message}";
                return false;
            }
            finally
            {
                DeleteFileBestEffort(tempPath);
            }
        }

        private static void DeleteFileBestEffort(string absolutePath)
        {
            try
            {
                if (string.IsNullOrEmpty(absolutePath))
                    return;

                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
            }
            catch
            {
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);
            }
        }

        internal static bool LoadMaintenanceRecord(string slotName, out SaveSlotMaintenanceRecord record, out string error)
        {
            record = null;
            error = string.Empty;
            if (string.IsNullOrEmpty(slotName))
            {
                error = "Maintenance slot name is empty.";
                return false;
            }

            string relativePath = SaveSlotMaintenanceRecord.GetPath(slotName);
            if (!Exists(relativePath))
            {
                error = "Maintenance sidecar does not exist.";
                return false;
            }

            string absolutePath = ToAbsolutePath(relativePath);
            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength < 0 || fileLength > int.MaxValue)
            {
                error = "Maintenance sidecar exceeds the supported range.";
                return false;
            }

            NativeArray<byte> buffer = AllocateTempNativeArrayBuffer((int)fileLength, MaintenanceReadBufferLabel, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[fileLength] — maintenance sidecar read staging buffer — owner: SaveSidecarStorage
            try
            {
                byte* bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                if (!AsyncWriteManager.TryReadAll(absolutePath, bufferPtr, (int)fileLength, out error))
                    return false;

                if (TryReadCurrentMaintenanceRecord(bufferPtr, (int)fileLength, out record, out error))
                    return true;

                string currentError = error;
                if (TryReadLegacyMaintenanceRecord(bufferPtr, (int)fileLength, out record, out error))
                    return true;

                error = $"Maintenance sidecar decode failed. Current={currentError}; Legacy={error}";
                return false;
            }
            finally
            {
                DisposeTempNativeArrayBuffer(ref buffer, MaintenanceReadBufferLabel);
            }
        }

        private static bool TryReadCurrentMaintenanceRecord(byte* bufferPtr, int fileLength, out SaveSlotMaintenanceRecord record, out string error)
        {
            SidecarReader reader = new SidecarReader(bufferPtr, fileLength);
            SaveSlotMaintenanceRecord loaded = new SaveSlotMaintenanceRecord();
            record = null;
            error = string.Empty;
            if (!reader.ReadString(out loaded.SlotName)
                || !reader.ReadLong(out loaded.LastSuccessfulSaveTicksUtc)
                || !reader.ReadLong(out loaded.LastSuccessfulLoadTicksUtc)
                || !reader.ReadLong(out loaded.LastAuditTicksUtc)
                || !reader.ReadLong(out loaded.LastRepairTicksUtc)
                || !reader.ReadLong(out loaded.LastFailureTicksUtc)
                || !reader.ReadInt(out loaded.SuccessfulSaveCount)
                || !reader.ReadInt(out loaded.SuccessfulLoadCount)
                || !reader.ReadInt(out loaded.AuditCount)
                || !reader.ReadInt(out loaded.RepairCount)
                || !reader.ReadInt(out loaded.FailureCount)
                || !reader.ReadByte(out byte stateFlags)
                || !reader.ReadInt(out loaded.LastLoadBackupGeneration)
                || !reader.ReadInt(out loaded.LastKnownSaveVersion)
                || !reader.ReadString(out loaded.LastKnownIntegrityState)
                || !reader.ReadString(out loaded.LastFailureContext)
                || !reader.ReadString(out loaded.LastFailureMessage)
                || !reader.ReadString(out loaded.LastAuditMessage)
                || !reader.ReadString(out loaded.LastRepairMessage)
                || !FinalizeSidecar(reader, out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = reader.Error;
                return false;
            }

            loaded.ApplyStateFlags(stateFlags);
            record = loaded;
            return true;
        }

        private static bool TryReadLegacyMaintenanceRecord(byte* bufferPtr, int fileLength, out SaveSlotMaintenanceRecord record, out string error)
        {
            SidecarReader reader = new SidecarReader(bufferPtr, fileLength);
            SaveSlotMaintenanceRecord loaded = new SaveSlotMaintenanceRecord();
            record = null;
            error = string.Empty;
            if (!reader.ReadString(out loaded.SlotName)
                || !reader.ReadLong(out loaded.LastSuccessfulSaveTicksUtc)
                || !reader.ReadLong(out loaded.LastSuccessfulLoadTicksUtc)
                || !reader.ReadLong(out loaded.LastAuditTicksUtc)
                || !reader.ReadLong(out loaded.LastRepairTicksUtc)
                || !reader.ReadLong(out loaded.LastFailureTicksUtc)
                || !reader.ReadInt(out loaded.SuccessfulSaveCount)
                || !reader.ReadInt(out loaded.SuccessfulLoadCount)
                || !reader.ReadInt(out loaded.AuditCount)
                || !reader.ReadInt(out loaded.RepairCount)
                || !reader.ReadInt(out loaded.FailureCount)
                || !reader.ReadBool(out loaded.LastAuditReadable)
                || !reader.ReadBool(out loaded.LastAuditRecommendedRepair)
                || !reader.ReadBool(out loaded.LastLoadUsedBackup)
                || !reader.ReadInt(out loaded.LastLoadBackupGeneration)
                || !reader.ReadBool(out loaded.LastLoadUsedLegacyCompression)
                || !reader.ReadBool(out loaded.LastLoadSelfRepaired)
                || !reader.ReadInt(out loaded.LastKnownSaveVersion)
                || !reader.ReadString(out loaded.LastKnownIntegrityState)
                || !reader.ReadString(out loaded.LastFailureContext)
                || !reader.ReadString(out loaded.LastFailureMessage)
                || !reader.ReadString(out loaded.LastAuditMessage)
                || !reader.ReadString(out loaded.LastRepairMessage)
                || !FinalizeSidecar(reader, out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = reader.Error;
                return false;
            }

            record = loaded;
            return true;
        }

        private static NativeArray<byte> AllocateTempNativeArrayBuffer(int length, string label, NativeArrayOptions options)
        {
            NativeArray<byte> buffer = H8Memory.Allocate<byte>(
                length,
                NativeArrayOwnerSystem,
                Allocator.Temp,
                options);

            if (!buffer.IsCreated || buffer.Length != length)
                throw new InvalidOperationException($"{NativeMemoryAllocationFailureMessage} Label={label}.");

            return buffer;
        }

        private static void DisposeTempNativeArrayBuffer(ref NativeArray<byte> buffer, string label)
        {
            if (!buffer.IsCreated)
                return;

            H8Memory.Release(ref buffer, NativeArrayOwnerSystem);

            if (buffer.IsCreated)
                throw new InvalidOperationException($"{NativeMemoryReleaseFailureMessage} Label={label}.");
        }

        private static bool FinalizeMetadata(ref SaveMetadata metadata, float posX, float posY, float posZ, SidecarReader reader, out string error)
        {
            metadata.PlayerPosition = new Vector3(posX, posY, posZ);

            int remainingBytes = reader.TotalLength - reader.BytesRead;
            if (remainingBytes >= sizeof(int) * 2)
            {
                if (!reader.ReadInt(out metadata.WorldSeed) ||
                    !reader.ReadInt(out metadata.WorldGenerationVersionId))
                {
                    error = reader.Error;
                    return false;
                }
            }
            else if (remainingBytes > 0)
            {
                error = "Metadata sidecar world-seed signature is truncated.";
                return false;
            }

            return FinalizeSidecar(reader, out error);
        }

        private static bool FinalizeSidecar(SidecarReader reader, out string error)
        {
            error = string.Empty;
            if (reader.BytesRead != reader.TotalLength)
            {
                error = "Sidecar payload length mismatch.";
                return false;
            }

            return true;
        }

        private static bool TryResolveMetadataByteCount(SaveMetadata metadata, out int byteCount, out string error)
        {
            byteCount = 0;
            error = string.Empty;
            long total =
                sizeof(long) +
                sizeof(float) +
                (sizeof(float) * 3) +
                (sizeof(int) * 2);

            return TryAddStringByteCount(ref total, metadata.SlotName, out error)
                && TryAddStringByteCount(ref total, metadata.GameVersion, out error)
                && TryAddStringByteCount(ref total, Hecton8.SaveSystem.SaveMetadata.NormalizeSceneName(metadata.SceneName), out error)
                && TryAddStringByteCount(ref total, metadata.Checksum, out error)
                && TryFinalizeSidecarByteCount(
                    total,
                    "Metadata sidecar payload exceeds the supported range.",
                    out byteCount,
                    out error);
        }

        private static bool TryResolveMaintenanceByteCount(SaveSlotMaintenanceRecord record, out int byteCount, out string error)
        {
            byteCount = 0;
            error = string.Empty;
            long total =
                (sizeof(long) * 5L) +
                (sizeof(int) * 7L) +
                sizeof(byte);

            return TryAddStringByteCount(ref total, record.SlotName, out error)
                && TryAddStringByteCount(ref total, record.LastKnownIntegrityState, out error)
                && TryAddStringByteCount(ref total, record.LastFailureContext, out error)
                && TryAddStringByteCount(ref total, record.LastFailureMessage, out error)
                && TryAddStringByteCount(ref total, record.LastAuditMessage, out error)
                && TryAddStringByteCount(ref total, record.LastRepairMessage, out error)
                && TryFinalizeSidecarByteCount(
                    total,
                    "Maintenance sidecar payload exceeds the supported range.",
                    out byteCount,
                    out error);
        }

        private static bool TryAddStringByteCount(ref long total, string value, out string error)
        {
            error = string.Empty;
            int charCount = value != null ? value.Length : 0;
            if (!TryResolveUtf16ByteCount(charCount, out int charBytes))
            {
                error = "Sidecar string byte length exceeds the supported range.";
                return false;
            }

            long entryBytes = sizeof(int) + (value != null ? charBytes : 0);
            return TryAddByteCount(ref total, entryBytes, "Sidecar payload exceeds the supported range.", out error);
        }

        private static bool TryResolveUtf16ByteCount(int charCount, out int byteCount)
        {
            byteCount = 0;
            if (charCount < 0 || charCount > int.MaxValue / sizeof(char))
                return false;

            byteCount = charCount * sizeof(char);
            return true;
        }

        private static bool TryAddByteCount(ref long total, long byteCount, string errorMessage, out string error)
        {
            error = string.Empty;
            if (byteCount < 0 || total < 0 || total > int.MaxValue - byteCount)
            {
                error = errorMessage;
                return false;
            }

            total += byteCount;
            return true;
        }

        private static bool TryFinalizeSidecarByteCount(long total, string errorMessage, out int byteCount, out string error)
        {
            byteCount = 0;
            error = string.Empty;
            if (total < 0 || total > int.MaxValue)
            {
                error = errorMessage;
                return false;
            }

            byteCount = (int)total;
            return true;
        }

        private static string ToAbsolutePath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            string root = s_persistentDataPathRoot;
            if (string.IsNullOrEmpty(root))
            {
                root = HectonPersistentPathPolicy.RootPath;
                s_persistentDataPathRoot = root;
            }

            return Path.Combine(root, NormalizePersistentRelativeSegment(relativePath));
        }

        private static string NormalizePersistentRelativeSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return string.Empty;

            string normalized = segment
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalized.IndexOf("..", StringComparison.Ordinal) >= 0
                ? Path.GetFileName(normalized)
                : normalized;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private ref struct SidecarWriter
        {
            private readonly byte* _buffer;
            private readonly int _capacity;
            private int _cursor;

            public SidecarWriter(byte* buffer, int capacity)
            {
                _buffer = buffer;
                _capacity = capacity;
                _cursor = 0;
                Error = string.Empty;
            }

            public string Error;

            public bool WriteBool(bool value)
            {
                return WriteByte(value ? (byte)1 : (byte)0);
            }

            public bool WriteByte(byte value)
            {
                if (!TryReserve(sizeof(byte)))
                    return false;

                *(_buffer + _cursor) = value;
                _cursor += sizeof(byte);
                return true;
            }

            public bool WriteInt(int value)
            {
                return WriteBlittable(value);
            }

            public bool WriteLong(long value)
            {
                return WriteBlittable(value);
            }

            public bool WriteFloat(float value)
            {
                return WriteBlittable(value);
            }

            public bool WriteString(string value)
            {
                if (value == null)
                    return WriteInt(NullCollectionCount);

                if (!WriteInt(value.Length))
                    return false;

                if (value.Length == 0)
                    return true;

                if (!TryResolveUtf16ByteCount(value.Length, out int byteCount))
                {
                    Error = "Sidecar string byte length exceeds the supported range.";
                    return false;
                }

                if (!TryReserve(byteCount))
                    return false;

                fixed (char* sourcePtr = value)
                {
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(_buffer + _cursor, _capacity - _cursor, sourcePtr, byteCount))
                    {
                        Error = "Sidecar string copy exceeded the allocated byte range.";
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveSidecarStorage));
                        return false;
                    }
                }

                _cursor += byteCount;
                return true;
            }

            private bool WriteBlittable<T>(T value) where T : unmanaged
            {
                int size = UnsafeUtility.SizeOf<T>();
                if (!TryReserve(size))
                    return false;

                UnsafeUtility.CopyStructureToPtr(ref value, _buffer + _cursor);
                _cursor += size;
                return true;
            }

            private bool TryReserve(int byteCount)
            {
                if (byteCount < 0 || _cursor < 0 || _cursor > _capacity - byteCount)
                {
                    Error = "Sidecar payload exceeded the allocated byte range.";
                    return false;
                }

                return true;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private ref struct SidecarReader
        {
            private readonly byte* _buffer;
            private readonly int _length;
            private int _cursor;

            public SidecarReader(byte* buffer, int length)
            {
                _buffer = buffer;
                _length = length;
                _cursor = 0;
                Error = string.Empty;
            }

            public int BytesRead => _cursor;
            public int TotalLength => _length;
            public string Error;

            public bool ReadBool(out bool value)
            {
                value = false;
                if (!ReadByte(out byte rawValue))
                    return false;

                value = rawValue != 0;
                return true;
            }

            public bool ReadByte(out byte value)
            {
                value = 0;
                if (!TryConsume(sizeof(byte)))
                    return false;

                value = *(_buffer + _cursor);
                _cursor += sizeof(byte);
                return true;
            }

            public bool ReadInt(out int value)
            {
                return ReadBlittable(out value);
            }

            public bool ReadLong(out long value)
            {
                return ReadBlittable(out value);
            }

            public bool ReadFloat(out float value)
            {
                return ReadBlittable(out value);
            }

            public bool ReadString(out string value)
            {
                value = string.Empty;
                if (!ReadInt(out int charCount))
                    return false;

                if (charCount == NullCollectionCount)
                {
                    value = null;
                    return true;
                }

                if (charCount < 0)
                {
                    Error = "String length is negative.";
                    return false;
                }

                if (charCount == 0)
                    return true;

                if (!TryResolveUtf16ByteCount(charCount, out int byteCount))
                {
                    Error = "Sidecar string byte length exceeds the supported range.";
                    return false;
                }

                if (!TryConsume(byteCount))
                    return false;

                value = new string((char*)(_buffer + _cursor), 0, charCount);
                _cursor += byteCount;
                return true;
            }

            private bool ReadBlittable<T>(out T value) where T : unmanaged
            {
                value = default;
                int size = UnsafeUtility.SizeOf<T>();
                if (!TryConsume(size))
                    return false;

                value = UnsafeUtility.ReadArrayElement<T>(_buffer + _cursor, 0);
                _cursor += size;
                return true;
            }

            private bool TryConsume(int byteCount)
            {
                if (byteCount < 0 || _cursor < 0 || _cursor > _length - byteCount)
                {
                    Error = "Sidecar payload exceeded the readable byte range.";
                    return false;
                }

                return true;
            }
        }
    }
}
