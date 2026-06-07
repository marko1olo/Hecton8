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

            string absolutePath = ToAbsolutePath(relativePath);
            int byteCount = GetStringByteCount(metadata.SlotName)
                + GetStringByteCount(metadata.GameVersion)
                + sizeof(long)
                + sizeof(float)
                + GetStringByteCount(metadata.SceneName)
                + (sizeof(float) * 3)
                + (sizeof(int) * 2)
                + GetStringByteCount(metadata.Checksum);

            NativeArray<byte> buffer = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[byteCount] — metadata sidecar write staging buffer — owner: SaveSidecarStorage
            RegisterTempNativeArrayBuffer(buffer, "metadataWriteBuffer");
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
                DisposeTempNativeArrayBuffer(ref buffer, "metadataWriteBuffer");
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

            NativeArray<byte> buffer = new NativeArray<byte>((int)fileLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[fileLength] — metadata sidecar read staging buffer — owner: SaveSidecarStorage
            RegisterTempNativeArrayBuffer(buffer, "metadataReadBuffer");
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
                DisposeTempNativeArrayBuffer(ref buffer, "metadataReadBuffer");
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

            string absolutePath = ToAbsolutePath(SaveSlotMaintenanceRecord.GetPath(record.SlotName));
            int byteCount =
                GetStringByteCount(record.SlotName) +
                (sizeof(long) * 5) +
                (sizeof(int) * 7) +
                sizeof(byte) +
                GetStringByteCount(record.LastKnownIntegrityState) +
                GetStringByteCount(record.LastFailureContext) +
                GetStringByteCount(record.LastFailureMessage) +
                GetStringByteCount(record.LastAuditMessage) +
                GetStringByteCount(record.LastRepairMessage);

            NativeArray<byte> buffer = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[byteCount] — maintenance sidecar write staging buffer — owner: SaveSidecarStorage
            RegisterTempNativeArrayBuffer(buffer, "maintenanceWriteBuffer");
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
                DisposeTempNativeArrayBuffer(ref buffer, "maintenanceWriteBuffer");
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

            NativeArray<byte> buffer = new NativeArray<byte>((int)fileLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[fileLength] — maintenance sidecar read staging buffer — owner: SaveSidecarStorage
            RegisterTempNativeArrayBuffer(buffer, "maintenanceReadBuffer");
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
                DisposeTempNativeArrayBuffer(ref buffer, "maintenanceReadBuffer");
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

        private static int GetStringByteCount(string value)
        {
            int charBytes = value != null ? checked(value.Length * sizeof(char)) : 0;
            return sizeof(int) + charBytes;
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

            return HectonPersistentPathPolicy.CombineFile(relativePath);
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

                int byteCount = checked(value.Length * sizeof(char));
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

                int byteCount = checked(charCount * sizeof(char));
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
