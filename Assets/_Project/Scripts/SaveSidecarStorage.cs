using System;
using System.IO;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveSidecarStorage
    {
        private const int NullCollectionCount = -1;
        private const string NativeMemoryOwner = nameof(SaveSidecarStorage);
        private const string NativeMemoryRegistrationFailureMessage = "NativeMemorySentinel registration failed for SaveSidecarStorage temp buffer.";
        private const string NativeMemoryRestoreFailureMessage = "NativeMemorySentinel restore failed after SaveSidecarStorage native disposal fault.";
        private const NativeAllocationLifetime NativeTempMemoryLifetime = NativeAllocationLifetime.Temp;
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

            File.Delete(ToAbsolutePath(relativePath));
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
                    || !writer.WriteString(metadata.SceneName)
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

                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                return AsyncWriteManager.WriteAll(absolutePath, bufferPtr, byteCount, out error);
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
                metadata = new SaveMetadata();
                return reader.ReadString(out metadata.SlotName)
                    && reader.ReadString(out metadata.GameVersion)
                    && reader.ReadLong(out metadata.Timestamp)
                    && reader.ReadFloat(out metadata.PlayTimeSeconds)
                    && reader.ReadString(out metadata.SceneName)
                    && reader.ReadFloat(out float playerPosX)
                    && reader.ReadFloat(out float playerPosY)
                    && reader.ReadFloat(out float playerPosZ)
                    && reader.ReadString(out metadata.Checksum)
                    && FinalizeMetadata(ref metadata, playerPosX, playerPosY, playerPosZ, reader, out error);
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

                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                return AsyncWriteManager.WriteAll(absolutePath, bufferPtr, byteCount, out error);
            }
            finally
            {
                DisposeTempNativeArrayBuffer(ref buffer, MaintenanceWriteBufferLabel);
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
            record = new SaveSlotMaintenanceRecord();
            error = string.Empty;
            if (!reader.ReadString(out record.SlotName)
                || !reader.ReadLong(out record.LastSuccessfulSaveTicksUtc)
                || !reader.ReadLong(out record.LastSuccessfulLoadTicksUtc)
                || !reader.ReadLong(out record.LastAuditTicksUtc)
                || !reader.ReadLong(out record.LastRepairTicksUtc)
                || !reader.ReadLong(out record.LastFailureTicksUtc)
                || !reader.ReadInt(out record.SuccessfulSaveCount)
                || !reader.ReadInt(out record.SuccessfulLoadCount)
                || !reader.ReadInt(out record.AuditCount)
                || !reader.ReadInt(out record.RepairCount)
                || !reader.ReadInt(out record.FailureCount)
                || !reader.ReadByte(out byte stateFlags)
                || !reader.ReadInt(out record.LastLoadBackupGeneration)
                || !reader.ReadInt(out record.LastKnownSaveVersion)
                || !reader.ReadString(out record.LastKnownIntegrityState)
                || !reader.ReadString(out record.LastFailureContext)
                || !reader.ReadString(out record.LastFailureMessage)
                || !reader.ReadString(out record.LastAuditMessage)
                || !reader.ReadString(out record.LastRepairMessage)
                || !FinalizeSidecar(reader, out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = reader.Error;
                return false;
            }

            record.ApplyStateFlags(stateFlags);
            return true;
        }

        private static bool TryReadLegacyMaintenanceRecord(byte* bufferPtr, int fileLength, out SaveSlotMaintenanceRecord record, out string error)
        {
            SidecarReader reader = new SidecarReader(bufferPtr, fileLength);
            record = new SaveSlotMaintenanceRecord();
            error = string.Empty;
            if (!reader.ReadString(out record.SlotName)
                || !reader.ReadLong(out record.LastSuccessfulSaveTicksUtc)
                || !reader.ReadLong(out record.LastSuccessfulLoadTicksUtc)
                || !reader.ReadLong(out record.LastAuditTicksUtc)
                || !reader.ReadLong(out record.LastRepairTicksUtc)
                || !reader.ReadLong(out record.LastFailureTicksUtc)
                || !reader.ReadInt(out record.SuccessfulSaveCount)
                || !reader.ReadInt(out record.SuccessfulLoadCount)
                || !reader.ReadInt(out record.AuditCount)
                || !reader.ReadInt(out record.RepairCount)
                || !reader.ReadInt(out record.FailureCount)
                || !reader.ReadBool(out record.LastAuditReadable)
                || !reader.ReadBool(out record.LastAuditRecommendedRepair)
                || !reader.ReadBool(out record.LastLoadUsedBackup)
                || !reader.ReadInt(out record.LastLoadBackupGeneration)
                || !reader.ReadBool(out record.LastLoadUsedLegacyCompression)
                || !reader.ReadBool(out record.LastLoadSelfRepaired)
                || !reader.ReadInt(out record.LastKnownSaveVersion)
                || !reader.ReadString(out record.LastKnownIntegrityState)
                || !reader.ReadString(out record.LastFailureContext)
                || !reader.ReadString(out record.LastFailureMessage)
                || !reader.ReadString(out record.LastAuditMessage)
                || !reader.ReadString(out record.LastRepairMessage)
                || !FinalizeSidecar(reader, out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = reader.Error;
                return false;
            }

            return true;
        }

        private static NativeArray<byte> AllocateTempNativeArrayBuffer(int length, string label, NativeArrayOptions options)
        {
            NativeArray<byte> buffer = new NativeArray<byte>(length, Allocator.Temp, options);
            try
            {
                RegisterTempNativeArrayBuffer(buffer, label);
                return buffer;
            }
            catch
            {
                if (buffer.IsCreated)
                    buffer.Dispose();
                throw;
            }
        }

        private static void RegisterTempNativeArrayBuffer(NativeArray<byte> buffer, string label)
        {
            if (!buffer.IsCreated)
                return;

            int registrationId = NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, label, NativeTempMemoryLifetime);
            if (registrationId <= 0)
                throw new InvalidOperationException(NativeMemoryRegistrationFailureMessage);
        }

        private static void DisposeTempNativeArrayBuffer(ref NativeArray<byte> buffer, string label)
        {
            if (!buffer.IsCreated)
                return;

            bool sentinelUnregistered = false;
            try
            {
                NativeMemorySentinel.UnregisterNativeArray(buffer);
                sentinelUnregistered = true;
                buffer.Dispose();
                buffer = default;
            }
            catch (Exception disposalException)
            {
                RestoreTempNativeArrayBufferSentinelOrThrow(buffer, label, sentinelUnregistered, disposalException);
                throw;
            }
        }

        private static void RestoreTempNativeArrayBufferSentinelOrThrow(
            NativeArray<byte> buffer,
            string label,
            bool sentinelUnregistered,
            Exception disposalException)
        {
            if (!sentinelUnregistered || !buffer.IsCreated)
                return;

            try
            {
                int registrationId = NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, label, NativeTempMemoryLifetime);
                if (registrationId <= 0)
                    throw new InvalidOperationException(NativeMemoryRestoreFailureMessage, disposalException);
            }
            catch (Exception restoreException)
            {
                throw new AggregateException(NativeMemoryRestoreFailureMessage, disposalException, restoreException);
            }
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
                && TryAddStringByteCount(ref total, metadata.SceneName, out error)
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
            string root = s_persistentDataPathRoot;
            if (string.IsNullOrEmpty(root))
            {
                root = HectonPersistentPathPolicy.RootPath;
                s_persistentDataPathRoot = root;
            }

            return HectonPersistentPathPolicy.CombineFile(root, relativePath);
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
