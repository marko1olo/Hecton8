using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.Input
{
    public static unsafe class ControlRemapper
    {
        public const int MaxControlsJsonBytes = 32 * 1024;
        public const int MaxBindingRecords = 128;
        public const int MaxControlPathBytes = 128;
        public const int FileStreamBufferBytes = 4096;

        private const string PlayerActionMapName = "Player";
        private const string UiActionMapName = "UI";
        private const ulong Fnv64Offset = 14695981039346656037UL;
        private const ulong Fnv64Prime = 1099511628211UL;
        private const uint Fnv32Offset = 2166136261u;
        private const uint Fnv32Prime = 16777619u;

        public static bool TryBootstrapTelemetry(
            IDataVault vault,
            out VaultGenerationHandle<InputBindingTelemetryEntry> ringHandle,
            out VaultGenerationHandle<int> cursorHandle)
        {
            ringHandle = default;
            cursorHandle = default;
            if (vault == null)
                return false;

            ringHandle = vault.EnsureGenerationHandle<InputBindingTelemetryEntry>(
                InputBindingContractLayout.InputBindingTelemetryRingBufferId,
                InputBindingContractLayout.InputBindingTelemetryCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            cursorHandle = vault.EnsureGenerationHandle<int>(
                InputBindingContractLayout.InputBindingTelemetryCursorBufferId,
                1,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            return ringHandle.BufferID != 0u && cursorHandle.BufferID != 0u;
        }

        public static void RecordTelemetry(
            IDataVault vault,
            in VaultGenerationHandle<InputBindingTelemetryEntry> ringHandle,
            in VaultGenerationHandle<int> cursorHandle,
            in InputBindingTelemetryEntry entry)
        {
            if (vault == null ||
                ringHandle.BufferID == 0u ||
                cursorHandle.BufferID == 0u)
            {
                return;
            }

            NativeArray<InputBindingTelemetryEntry> ring = default;
            NativeArray<int> cursor = default;
            bool ringLocked = false;
            bool cursorLocked = false;
            try
            {
                ringLocked = vault.TryAcquireWriteLock(in ringHandle, SystemID.UI, out ring);
                cursorLocked = vault.TryAcquireWriteLock(in cursorHandle, SystemID.UI, out cursor);
                if (!ringLocked || !cursorLocked || !ring.IsCreated || !cursor.IsCreated || ring.Length <= 0 || cursor.Length <= 0)
                    return;

                int index = cursor[0];
                if (index < 0 || index >= ring.Length)
                    index = 0;

                ring[index] = entry;
                index++;
                if (index >= ring.Length)
                    index = 0;
                cursor[0] = index;
            }
            finally
            {
                if (cursorLocked)
                    vault.ReleaseWriteLock(in cursorHandle, SystemID.UI);
                if (ringLocked)
                    vault.ReleaseWriteLock(in ringHandle, SystemID.UI);
            }
        }

        public static bool TryDumpTelemetry(
            IDataVault vault,
            in VaultGenerationHandle<InputBindingTelemetryEntry> ringHandle,
            string path)
        {
            if (vault == null || ringHandle.BufferID == 0u || string.IsNullOrEmpty(path))
                return false;

            try
            {
                if (!vault.TryResolveHandle(in ringHandle, out NativeArray<InputBindingTelemetryEntry> ring) ||
                    !ring.IsCreated ||
                    ring.Length <= 0)
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                int byteCount = ring.Length * InputBindingContractLayout.InputBindingTelemetryStrideBytes;
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, FileStreamBufferBytes, FileOptions.WriteThrough))
                {
                    stream.Write(new ReadOnlySpan<byte>(source, byteCount));
                    stream.Flush(true);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public static bool TrySaveOverrides(
            INativeInputManagerRuntime inputManager,
            string path,
            string tempPath,
            out ControlRemapIoResult result)
        {
            result = default;
            long startTicks = Stopwatch.GetTimestamp();
            NativeArray<byte> buffer = default;
            try
            {
                if (inputManager == null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(tempPath))
                {
                    MarkFailure(
                        ref result,
                        InputBindingTelemetryOperation.Save,
                        InputBindingTelemetryResult.InvalidJson,
                        InputBindingFaultFlags.InvalidSchema,
                        0,
                        0,
                        0,
                        startTicks);
                    return false;
                }

                buffer = new NativeArray<byte>(MaxControlsJsonBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
                int index = 0;
                if (!WriteLiteral(ptr, buffer.Length, ref index, "{\"v\":1,\"bindings\":["))
                {
                    MarkFailure(
                        ref result,
                        InputBindingTelemetryOperation.Save,
                        InputBindingTelemetryResult.IoFailure,
                        InputBindingFaultFlags.BufferOverflow,
                        index,
                        0,
                        0,
                        startTicks);
                    return false;
                }

                int recordCount = 0;
                int pathBytes = 0;
                if (!WriteMap(inputManager, inputManager.GetActionMap(PlayerActionMapName), ptr, buffer.Length, ref index, ref recordCount, ref pathBytes) ||
                    !WriteMap(inputManager, inputManager.GetActionMap(UiActionMapName), ptr, buffer.Length, ref index, ref recordCount, ref pathBytes))
                {
                    MarkFailure(
                        ref result,
                        InputBindingTelemetryOperation.Save,
                        InputBindingTelemetryResult.IoFailure,
                        InputBindingFaultFlags.BufferOverflow | InputBindingFaultFlags.PathTooLong,
                        index,
                        recordCount,
                        pathBytes,
                        startTicks);
                    return false;
                }

                if (!WriteLiteral(ptr, buffer.Length, ref index, "]}"))
                {
                    MarkFailure(
                        ref result,
                        InputBindingTelemetryOperation.Save,
                        InputBindingTelemetryResult.IoFailure,
                        InputBindingFaultFlags.BufferOverflow,
                        index,
                        recordCount,
                        pathBytes,
                        startTicks);
                    return false;
                }

                result.RecordCount = recordCount;
                result.ByteCount = index;
                result.PathBytes = pathBytes;
                if (recordCount == 0)
                {
                    result.ResultCode = InputBindingTelemetryResult.NoOverrides;
                    result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Save, result.ResultCode, result.FaultFlags, index, 0, recordCount, pathBytes, startTicks);
                    return true;
                }

                if (!TryWriteAtomic(path, tempPath, ptr, index, ref result))
                    return false;

                result.ResultCode = InputBindingTelemetryResult.Success;
                result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Save, result.ResultCode, result.FaultFlags, index, ComputeHash64(ptr, index), recordCount, pathBytes, startTicks);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Save, startTicks);
                return false;
            }
            catch (IOException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Save, startTicks);
                return false;
            }
            catch (ArgumentException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Save, startTicks);
                return false;
            }
            catch (NotSupportedException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Save, startTicks);
                return false;
            }
            finally
            {
                if (buffer.IsCreated)
                    buffer.Dispose();
            }
        }

        public static bool TryLoadOverrides(
            INativeInputManagerRuntime inputManager,
            string path,
            out ControlRemapIoResult result)
        {
            result = default;
            long startTicks = Stopwatch.GetTimestamp();
            NativeArray<byte> fileBytes = default;
            NativeArray<InputActionStateDTO> records = default;
            try
            {
                if (inputManager == null || string.IsNullOrEmpty(path))
                {
                    MarkFailure(
                        ref result,
                        InputBindingTelemetryOperation.Load,
                        InputBindingTelemetryResult.InvalidJson,
                        InputBindingFaultFlags.InvalidSchema,
                        0,
                        0,
                        0,
                        startTicks);
                    return false;
                }

                if (!File.Exists(path))
                {
                    result.ResultCode = InputBindingTelemetryResult.FileMissing;
                    result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Load, result.ResultCode, 0u, 0, 0, 0, 0, startTicks);
                    return false;
                }

                fileBytes = new NativeArray<byte>(MaxControlsJsonBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* bytesPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(fileBytes);
                int byteCount = TryReadAll(path, bytesPtr, fileBytes.Length, ref result);
                if (byteCount <= 0)
                {
                    result.ResultCode = result.ResultCode == 0u ? InputBindingTelemetryResult.InvalidJson : result.ResultCode;
                    result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Load, result.ResultCode, result.FaultFlags, 0, 0, 0, 0, startTicks);
                    return false;
                }

                records = new NativeArray<InputActionStateDTO>(MaxBindingRecords, Allocator.Temp, NativeArrayOptions.ClearMemory);
                if (!TryParseBindings(bytesPtr, byteCount, records, out int recordCount, ref result))
                {
                    result.ResultCode = InputBindingTelemetryResult.InvalidJson;
                    result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Parse, result.ResultCode, result.FaultFlags, byteCount, ComputeHash64(bytesPtr, byteCount), 0, 0, startTicks);
                    return false;
                }

                int pathBytes = 0;
                for (int i = 0; i < recordCount; i++)
                {
                    InputActionStateDTO record = records[i];
                    ReadOnlySpan<byte> pathBytesSpan = new ReadOnlySpan<byte>(bytesPtr + record.PathByteOffset, record.PathByteLength);
                    pathBytes += record.PathByteLength;
                    if (!CanApplyRecord(inputManager, in record, pathBytesSpan, ref result))
                    {
                        result.RecordCount = 0;
                        result.ByteCount = byteCount;
                        result.PathBytes = pathBytes;
                        result.ResultCode = InputBindingTelemetryResult.UnsupportedPath;
                        result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Load, result.ResultCode, result.FaultFlags, byteCount, ComputeHash64(bytesPtr, byteCount), 0, pathBytes, startTicks);
                        return false;
                    }
                }

                inputManager.ClearBindingOverrides();
                int applied = 0;
                for (int i = 0; i < recordCount; i++)
                {
                    InputActionStateDTO record = records[i];
                    ReadOnlySpan<byte> pathBytesSpan = new ReadOnlySpan<byte>(bytesPtr + record.PathByteOffset, record.PathByteLength);
                    if (TryApplyRecord(inputManager, in record, pathBytesSpan, ref result))
                        applied++;
                }

                result.RecordCount = applied;
                result.ByteCount = byteCount;
                result.PathBytes = pathBytes;
                if (applied != recordCount)
                {
                    inputManager.ClearBindingOverrides();
                    result.RecordCount = 0;
                    result.ResultCode = InputBindingTelemetryResult.UnsupportedPath;
                    result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;
                    result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Load, result.ResultCode, result.FaultFlags, byteCount, ComputeHash64(bytesPtr, byteCount), 0, pathBytes, startTicks);
                    return false;
                }

                result.ResultCode = InputBindingTelemetryResult.Success;
                result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Load, result.ResultCode, result.FaultFlags, byteCount, ComputeHash64(bytesPtr, byteCount), applied, pathBytes, startTicks);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Load, startTicks);
                return false;
            }
            catch (IOException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Load, startTicks);
                return false;
            }
            catch (ArgumentException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Load, startTicks);
                return false;
            }
            catch (NotSupportedException)
            {
                MarkIoFailure(ref result, InputBindingTelemetryOperation.Load, startTicks);
                return false;
            }
            finally
            {
                if (records.IsCreated)
                    records.Dispose();
                if (fileBytes.IsCreated)
                    fileBytes.Dispose();
            }
        }

        private static bool WriteMap(
            INativeInputManagerRuntime inputManager,
            InputActionMap map,
            byte* buffer,
            int capacity,
            ref int index,
            ref int recordCount,
            ref int pathBytes)
        {
            if (inputManager == null || map == null)
                return true;

            int actionCount = map.actions.Count;
            for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                InputAction action = map.actions[actionIndex];
                if (action == null)
                    continue;

                int bindingCount = action.bindings.Count;
                for (int bindingIndex = 0; bindingIndex < bindingCount; bindingIndex++)
                {
                    InputBinding binding = action.bindings[bindingIndex];
                    if (binding.isComposite || binding.isPartOfComposite || binding.overridePath == null)
                        continue;

                    if (recordCount >= MaxBindingRecords)
                        return false;

                    if (recordCount > 0 && !WriteByte(buffer, capacity, ref index, (byte)','))
                        return false;

                    InputActionStateDTO state = BuildStateDto(map.name, action.name, binding, bindingIndex, inputManager.CurrentDisplayStyleCode);
                    pathBytes += binding.overridePath.Length;
                    if (!WriteBindingObject(buffer, capacity, ref index, in state, binding.overridePath))
                        return false;

                    recordCount++;
                }
            }

            return true;
        }

        private static InputActionStateDTO BuildStateDto(string mapName, string actionName, InputBinding binding, int bindingIndex, byte displayStyle)
        {
            InputActionStateDTO state = default;
            state.ActionMapHash = HashString32(mapName);
            state.ActionNameHash = HashString32(actionName);
            state.ActionIdentityHash64 = MixHash64(HashString64(mapName), HashString64(actionName));
            state.OverridePathHash64 = HashString64(binding.overridePath);
            state.EffectivePathHash64 = HashString64(binding.effectivePath);
            byte* guidBytes = stackalloc byte[16];
            Span<byte> guidSpan = new Span<byte>(guidBytes, 16);
            state.BindingGuidHash64 = binding.id.TryWriteBytes(guidSpan)
                ? ComputeHash64(guidBytes, 16)
                : (ulong)binding.id.GetHashCode();
            state.BindingGroupHash = HashString32(binding.groups);
            state.ControlPathHash = HashString32(binding.path);
            state.BindingIndex = bindingIndex;
            state.DisplayStyle = displayStyle;
            state.Flags = (byte)InputActionStateFlags.HasOverridePath;
            return state;
        }

        private static bool WriteBindingObject(byte* buffer, int capacity, ref int index, in InputActionStateDTO state, string overridePath)
        {
            return WriteLiteral(buffer, capacity, ref index, "{\"map\":") &&
                   WriteUInt(buffer, capacity, ref index, state.ActionMapHash) &&
                   WriteLiteral(buffer, capacity, ref index, ",\"action\":") &&
                   WriteUInt(buffer, capacity, ref index, state.ActionNameHash) &&
                   WriteLiteral(buffer, capacity, ref index, ",\"binding\":") &&
                   WriteInt(buffer, capacity, ref index, state.BindingIndex) &&
                   WriteLiteral(buffer, capacity, ref index, ",\"id\":") &&
                   WriteULong(buffer, capacity, ref index, state.BindingGuidHash64) &&
                   WriteLiteral(buffer, capacity, ref index, ",\"path\":\"") &&
                   WriteJsonAsciiStringContent(buffer, capacity, ref index, overridePath) &&
                   WriteLiteral(buffer, capacity, ref index, "\"}");
        }

        private static int TryReadAll(string path, byte* destination, int capacity, ref ControlRemapIoResult result)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes, FileOptions.SequentialScan))
            {
                long length = stream.Length;
                if (length <= 0 || length > capacity)
                {
                    result.ResultCode = InputBindingTelemetryResult.InvalidJson;
                    result.FaultFlags |= length > capacity ? InputBindingFaultFlags.BufferOverflow : InputBindingFaultFlags.InvalidSchema;
                    return -1;
                }

                Span<byte> span = new Span<byte>(destination, (int)length);
                int total = 0;
                while (total < span.Length)
                {
                    int read = stream.Read(span.Slice(total));
                    if (read <= 0)
                        break;
                    total += read;
                }

                if (total != span.Length)
                {
                    result.ResultCode = InputBindingTelemetryResult.IoFailure;
                    result.FaultFlags |= InputBindingFaultFlags.IoException;
                    return -1;
                }

                result.ByteCount = total;
                return total;
            }
        }

        private static bool TryWriteAtomic(string path, string tempPath, byte* source, int byteCount, ref ControlRemapIoResult result)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                long writtenLength;
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, FileStreamBufferBytes, FileOptions.WriteThrough))
                {
                    stream.Write(new ReadOnlySpan<byte>(source, byteCount));
                    stream.Flush(true);
                    writtenLength = stream.Position;
                }

                if (writtenLength != byteCount)
                {
                    result.ResultCode = InputBindingTelemetryResult.IoFailure;
                    result.FaultFlags |= InputBindingFaultFlags.IoException;
                    return false;
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, null, true);
                else
                    File.Move(tempPath, path);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                MarkIoFailureNoTelemetry(ref result);
                TryDeleteTempAfterIoFailure(tempPath, ref result);
                return false;
            }
            catch (IOException)
            {
                MarkIoFailureNoTelemetry(ref result);
                TryDeleteTempAfterIoFailure(tempPath, ref result);
                return false;
            }
            catch (ArgumentException)
            {
                MarkIoFailureNoTelemetry(ref result);
                TryDeleteTempAfterIoFailure(tempPath, ref result);
                return false;
            }
            catch (NotSupportedException)
            {
                MarkIoFailureNoTelemetry(ref result);
                TryDeleteTempAfterIoFailure(tempPath, ref result);
                return false;
            }
        }

        private static void TryDeleteTempAfterIoFailure(string tempPath, ref ControlRemapIoResult result)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (UnauthorizedAccessException)
            {
                result.FaultFlags |= InputBindingFaultFlags.IoException;
            }
            catch (IOException)
            {
                result.FaultFlags |= InputBindingFaultFlags.IoException;
            }
            catch (ArgumentException)
            {
                result.FaultFlags |= InputBindingFaultFlags.IoException;
            }
            catch (NotSupportedException)
            {
                result.FaultFlags |= InputBindingFaultFlags.IoException;
            }
        }

        private static void MarkIoFailure(ref ControlRemapIoResult result, uint operation, long startTicks)
        {
            MarkIoFailureNoTelemetry(ref result);
            result.Telemetry = BuildTelemetry(operation, result.ResultCode, result.FaultFlags, result.ByteCount, 0, result.RecordCount, result.PathBytes, startTicks);
        }

        private static void MarkFailure(
            ref ControlRemapIoResult result,
            uint operation,
            uint resultCode,
            uint faultFlags,
            int byteCount,
            int recordCount,
            int pathBytes,
            long startTicks)
        {
            result.ResultCode = resultCode;
            result.FaultFlags |= faultFlags;
            result.ByteCount = byteCount;
            result.RecordCount = recordCount;
            result.PathBytes = pathBytes;
            result.Telemetry = BuildTelemetry(operation, result.ResultCode, result.FaultFlags, byteCount, 0, recordCount, pathBytes, startTicks);
        }

        private static void MarkIoFailureNoTelemetry(ref ControlRemapIoResult result)
        {
            result.ResultCode = InputBindingTelemetryResult.IoFailure;
            result.FaultFlags |= InputBindingFaultFlags.IoException;
        }

        private static bool TryParseBindings(
            byte* bytes,
            int length,
            NativeArray<InputActionStateDTO> records,
            out int recordCount,
            ref ControlRemapIoResult result)
        {
            recordCount = 0;
            int index = 0;
            if (!TryFindBindingsArray(bytes, length, ref index))
            {
                result.FaultFlags |= InputBindingFaultFlags.InvalidSchema;
                return false;
            }

            SkipWhitespace(bytes, length, ref index);
            if (index < length && bytes[index] == (byte)']')
                return true;

            while (index < length)
            {
                SkipWhitespace(bytes, length, ref index);
                if (index >= length || bytes[index] != (byte)'{')
                {
                    result.FaultFlags |= InputBindingFaultFlags.InvalidSchema;
                    return false;
                }

                if (recordCount >= records.Length)
                {
                    result.FaultFlags |= InputBindingFaultFlags.BufferOverflow;
                    return false;
                }

                index++;
                InputActionStateDTO state = default;
                bool hasMap = false;
                bool hasAction = false;
                bool hasBinding = false;
                bool hasPath = false;
                while (index < length)
                {
                    SkipWhitespace(bytes, length, ref index);
                    if (index < length && bytes[index] == (byte)'}')
                    {
                        index++;
                        break;
                    }

                    if (!TryReadQuotedToken(bytes, length, ref index, out int propertyStart, out int propertyLength))
                    {
                        result.FaultFlags |= InputBindingFaultFlags.InvalidSchema;
                        return false;
                    }

                    SkipWhitespace(bytes, length, ref index);
                    if (index >= length || bytes[index] != (byte)':')
                    {
                        result.FaultFlags |= InputBindingFaultFlags.InvalidSchema;
                        return false;
                    }

                    index++;
                    SkipWhitespace(bytes, length, ref index);
                    if (TokenEquals(bytes + propertyStart, propertyLength, "map"))
                    {
                        if (!TryReadUInt(bytes, length, ref index, out state.ActionMapHash))
                            return false;
                        hasMap = true;
                    }
                    else if (TokenEquals(bytes + propertyStart, propertyLength, "action"))
                    {
                        if (!TryReadUInt(bytes, length, ref index, out state.ActionNameHash))
                            return false;
                        hasAction = true;
                    }
                    else if (TokenEquals(bytes + propertyStart, propertyLength, "binding"))
                    {
                        if (!TryReadInt(bytes, length, ref index, out state.BindingIndex))
                            return false;
                        hasBinding = true;
                    }
                    else if (TokenEquals(bytes + propertyStart, propertyLength, "id"))
                    {
                        if (!TryReadULong(bytes, length, ref index, out state.BindingGuidHash64))
                            return false;
                    }
                    else if (TokenEquals(bytes + propertyStart, propertyLength, "path"))
                    {
                        if (!TryReadQuotedToken(bytes, length, ref index, out int pathStart, out int pathLength))
                        {
                            result.FaultFlags |= InputBindingFaultFlags.InvalidUtf8;
                            return false;
                        }

                        if (pathLength > MaxControlPathBytes || pathStart > ushort.MaxValue)
                        {
                            result.FaultFlags |= InputBindingFaultFlags.PathTooLong;
                            return false;
                        }

                        state.PathByteOffset = (ushort)pathStart;
                        state.PathByteLength = (ushort)pathLength;
                        state.OverridePathHash64 = ComputeHash64(bytes + pathStart, pathLength);
                        state.Flags = (byte)InputActionStateFlags.HasOverridePath;
                        hasPath = true;
                    }
                    else
                    {
                        if (!SkipValue(bytes, length, ref index))
                            return false;
                    }

                    SkipWhitespace(bytes, length, ref index);
                    if (index < length && bytes[index] == (byte)',')
                    {
                        index++;
                        continue;
                    }
                }

                if (!hasMap || !hasAction || !hasBinding || !hasPath)
                {
                    result.FaultFlags |= InputBindingFaultFlags.InvalidSchema;
                    return false;
                }

                records[recordCount++] = state;
                SkipWhitespace(bytes, length, ref index);
                if (index < length && bytes[index] == (byte)',')
                {
                    index++;
                    continue;
                }

                if (index < length && bytes[index] == (byte)']')
                    return true;
            }

            result.FaultFlags |= InputBindingFaultFlags.InvalidSchema;
            return false;
        }

        private static bool TryApplyRecord(
            INativeInputManagerRuntime inputManager,
            in InputActionStateDTO record,
            ReadOnlySpan<byte> pathUtf8,
            ref ControlRemapIoResult result)
        {
            if (!TryFindAction(inputManager, record.ActionMapHash, record.ActionNameHash, out InputAction action))
            {
                result.FaultFlags |= InputBindingFaultFlags.ActionMissing;
                return false;
            }

            if (record.BindingIndex < 0 || record.BindingIndex >= action.bindings.Count)
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }

            InputBinding binding = action.bindings[record.BindingIndex];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }

            if (!BindingIdMatches(binding, record.BindingGuidHash64))
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }

            if (record.PathByteLength == 0)
            {
                return TryApplyOverridePath(action, record.BindingIndex, string.Empty, ref result);
            }

            string path = TryResolvePathString(action, record.BindingIndex, pathUtf8);
            if (string.IsNullOrEmpty(path))
            {
                result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;
                return false;
            }

            return TryApplyOverridePath(action, record.BindingIndex, path, ref result);
        }

        private static bool TryApplyOverridePath(InputAction action, int bindingIndex, string path, ref ControlRemapIoResult result)
        {
            try
            {
                action.ApplyBindingOverride(bindingIndex, path);
                return true;
            }
            catch (InvalidOperationException)
            {
                result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }
            catch (ArgumentException)
            {
                result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;
                return false;
            }
        }

        private static bool CanApplyRecord(
            INativeInputManagerRuntime inputManager,
            in InputActionStateDTO record,
            ReadOnlySpan<byte> pathUtf8,
            ref ControlRemapIoResult result)
        {
            if (!TryFindAction(inputManager, record.ActionMapHash, record.ActionNameHash, out InputAction action))
            {
                result.FaultFlags |= InputBindingFaultFlags.ActionMissing;
                return false;
            }

            if (record.BindingIndex < 0 || record.BindingIndex >= action.bindings.Count)
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }

            InputBinding binding = action.bindings[record.BindingIndex];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }

            if (!BindingIdMatches(binding, record.BindingGuidHash64))
            {
                result.FaultFlags |= InputBindingFaultFlags.BindingMissing;
                return false;
            }

            if (record.PathByteLength == 0 || IsValidControlPathBytes(pathUtf8))
                return true;

            result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;
            return false;
        }

        private static bool TryFindAction(INativeInputManagerRuntime inputManager, uint mapHash, uint actionHash, out InputAction action)
        {
            action = null;
            if (TryFindActionInMap(inputManager.GetActionMap(PlayerActionMapName), mapHash, actionHash, out action))
                return true;
            return TryFindActionInMap(inputManager.GetActionMap(UiActionMapName), mapHash, actionHash, out action);
        }

        private static bool TryFindActionInMap(InputActionMap map, uint mapHash, uint actionHash, out InputAction action)
        {
            action = null;
            if (map == null || HashString32(map.name) != mapHash)
                return false;

            int actionCount = map.actions.Count;
            for (int i = 0; i < actionCount; i++)
            {
                InputAction candidate = map.actions[i];
                if (candidate != null && HashString32(candidate.name) == actionHash)
                {
                    action = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string TryResolveExistingPathString(InputAction action, int bindingIndex, ReadOnlySpan<byte> pathUtf8)
        {
            InputBinding binding = action.bindings[bindingIndex];
            if (StringMatchesAscii(binding.overridePath, pathUtf8))
                return binding.overridePath;
            if (StringMatchesAscii(binding.path, pathUtf8))
                return binding.path;
            if (StringMatchesAscii(binding.effectivePath, pathUtf8))
                return binding.effectivePath;
            return null;
        }

        private static string TryResolvePathString(InputAction action, int bindingIndex, ReadOnlySpan<byte> pathUtf8)
        {
            string existing = TryResolveExistingPathString(action, bindingIndex, pathUtf8);
            if (existing != null)
                return existing;

            return TryDecodeControlPathString(pathUtf8, out string decoded) ? decoded : null;
        }

        private static bool BindingIdMatches(InputBinding binding, ulong expectedHash)
        {
            if (expectedHash == 0UL)
                return true;

            return ComputeBindingGuidHash64(binding) == expectedHash;
        }

        private static ulong ComputeBindingGuidHash64(InputBinding binding)
        {
            byte* guidBytes = stackalloc byte[16];
            Span<byte> guidSpan = new Span<byte>(guidBytes, 16);
            return binding.id.TryWriteBytes(guidSpan)
                ? ComputeHash64(guidBytes, 16)
                : (ulong)binding.id.GetHashCode();
        }

        private static bool IsValidControlPathBytes(ReadOnlySpan<byte> pathUtf8)
        {
            if (pathUtf8.IsEmpty || pathUtf8.Length > MaxControlPathBytes)
                return false;

            if (pathUtf8[0] != (byte)'<')
                return false;

            int layoutEnd = -1;
            for (int i = 1; i < pathUtf8.Length; i++)
            {
                byte c = pathUtf8[i];
                if (c < 33u || c >= 127u || c == (byte)'"' || c == (byte)'\\')
                    return false;

                if (c == (byte)'>')
                {
                    layoutEnd = i;
                    break;
                }
            }

            if (layoutEnd <= 1 || layoutEnd + 1 >= pathUtf8.Length || pathUtf8[layoutEnd + 1] != (byte)'/')
                return false;

            return layoutEnd + 2 < pathUtf8.Length;
        }

        private static bool TryDecodeControlPathString(ReadOnlySpan<byte> pathUtf8, out string path)
        {
            path = null;
            if (!IsValidControlPathBytes(pathUtf8))
                return false;

            char* chars = stackalloc char[MaxControlPathBytes];
            for (int i = 0; i < pathUtf8.Length; i++)
                chars[i] = (char)pathUtf8[i];

            path = new string(chars, 0, pathUtf8.Length);
            return !string.IsNullOrEmpty(path);
        }

        private static bool TryFindBindingsArray(byte* bytes, int length, ref int index)
        {
            while (index < length)
            {
                if (bytes[index] != (byte)'"')
                {
                    index++;
                    continue;
                }

                if (!TryReadQuotedToken(bytes, length, ref index, out int tokenStart, out int tokenLength))
                    return false;

                if (!TokenEquals(bytes + tokenStart, tokenLength, "bindings"))
                    continue;

                SkipWhitespace(bytes, length, ref index);
                if (index >= length || bytes[index] != (byte)':')
                    return false;
                index++;
                SkipWhitespace(bytes, length, ref index);
                if (index >= length || bytes[index] != (byte)'[')
                    return false;
                index++;
                return true;
            }

            return false;
        }

        private static bool TryReadQuotedToken(byte* bytes, int length, ref int index, out int tokenStart, out int tokenLength)
        {
            tokenStart = 0;
            tokenLength = 0;
            if (index >= length || bytes[index] != (byte)'"')
                return false;

            index++;
            tokenStart = index;
            while (index < length)
            {
                byte value = bytes[index];
                if (value == (byte)'\\')
                    return false;
                if (value == (byte)'"')
                {
                    tokenLength = index - tokenStart;
                    index++;
                    return true;
                }

                if (value >= 128u || value == 0u)
                    return false;
                index++;
            }

            return false;
        }

        private static bool TryReadUInt(byte* bytes, int length, ref int index, out uint value)
        {
            value = 0u;
            bool any = false;
            while (index < length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                any = true;
                uint digit = (uint)(c - (byte)'0');
                if (value > (uint.MaxValue - digit) / 10u)
                    return false;
                value = value * 10u + digit;
                index++;
            }

            return any;
        }

        private static bool TryReadInt(byte* bytes, int length, ref int index, out int value)
        {
            value = 0;
            bool negative = false;
            if (index < length && bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            if (!TryReadULong(bytes, length, ref index, out ulong unsigned))
                return false;

            if (negative)
            {
                if (unsigned > 2147483648UL)
                    return false;

                value = unsigned == 2147483648UL ? int.MinValue : -(int)unsigned;
                return true;
            }

            if (unsigned > int.MaxValue)
                return false;

            value = (int)unsigned;
            return true;
        }

        private static bool TryReadULong(byte* bytes, int length, ref int index, out ulong value)
        {
            value = 0UL;
            bool any = false;
            while (index < length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                any = true;
                ulong digit = (ulong)(c - (byte)'0');
                if (value > (ulong.MaxValue - digit) / 10UL)
                    return false;
                value = value * 10UL + digit;
                index++;
            }

            return any;
        }

        private static bool SkipValue(byte* bytes, int length, ref int index)
        {
            SkipWhitespace(bytes, length, ref index);
            if (index >= length)
                return false;

            if (bytes[index] == (byte)'"')
                return TryReadQuotedToken(bytes, length, ref index, out _, out _);

            while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'}' && bytes[index] != (byte)']')
                index++;
            return true;
        }

        private static void SkipWhitespace(byte* bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte c = bytes[index];
                if (c != 32 && c != 9 && c != 10 && c != 13)
                    return;
                index++;
            }
        }

        private static bool WriteLiteral(byte* buffer, int capacity, ref int index, string value)
        {
            if (value == null)
                return true;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 127 || !WriteByte(buffer, capacity, ref index, (byte)c))
                    return false;
            }

            return true;
        }

        private static bool WriteJsonAsciiStringContent(byte* buffer, int capacity, ref int index, string value)
        {
            if (value == null || value.Length > MaxControlPathBytes)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 127)
                    return false;

                if (c == '"' || c == '\\')
                    return false;

                if (!WriteByte(buffer, capacity, ref index, (byte)c))
                    return false;
            }

            return true;
        }

        private static bool WriteUInt(byte* buffer, int capacity, ref int index, uint value)
        {
            byte* digits = stackalloc byte[10];
            int count = 0;
            do
            {
                digits[count++] = (byte)('0' + value % 10u);
                value /= 10u;
            }
            while (value != 0u);

            for (int i = count - 1; i >= 0; i--)
            {
                if (!WriteByte(buffer, capacity, ref index, digits[i]))
                    return false;
            }

            return true;
        }

        private static bool WriteULong(byte* buffer, int capacity, ref int index, ulong value)
        {
            byte* digits = stackalloc byte[20];
            int count = 0;
            do
            {
                digits[count++] = (byte)((ulong)(byte)'0' + value % 10UL);
                value /= 10UL;
            }
            while (value != 0UL);

            for (int i = count - 1; i >= 0; i--)
            {
                if (!WriteByte(buffer, capacity, ref index, digits[i]))
                    return false;
            }

            return true;
        }

        private static bool WriteInt(byte* buffer, int capacity, ref int index, int value)
        {
            if (value < 0)
            {
                if (!WriteByte(buffer, capacity, ref index, (byte)'-'))
                    return false;
                return WriteUInt(buffer, capacity, ref index, (uint)-value);
            }

            return WriteUInt(buffer, capacity, ref index, (uint)value);
        }

        private static bool WriteByte(byte* buffer, int capacity, ref int index, byte value)
        {
            if (index < 0 || index >= capacity)
                return false;
            buffer[index++] = value;
            return true;
        }

        private static bool TokenEquals(byte* token, int length, string value)
        {
            if (value == null || length != value.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (token[i] != (byte)value[i])
                    return false;
            }

            return true;
        }

        private static bool StringMatchesAscii(string value, ReadOnlySpan<byte> ascii)
        {
            if (string.IsNullOrEmpty(value) || value.Length != ascii.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 127 || (byte)c != ascii[i])
                    return false;
            }

            return true;
        }

        private static uint HashString32(string value)
        {
            uint hash = Fnv32Offset;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= Fnv32Prime;
            }

            return hash;
        }

        private static ulong HashString64(string value)
        {
            ulong hash = Fnv64Offset;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= Fnv64Prime;
            }

            return hash;
        }

        private static ulong ComputeHash64(byte* bytes, int length)
        {
            ulong hash = Fnv64Offset;
            for (int i = 0; i < length; i++)
            {
                hash ^= bytes[i];
                hash *= Fnv64Prime;
            }

            return hash;
        }

        private static ulong MixHash64(ulong a, ulong b)
        {
            ulong hash = Fnv64Offset;
            hash ^= a;
            hash *= Fnv64Prime;
            hash ^= b;
            hash *= Fnv64Prime;
            return hash;
        }

        private static InputBindingTelemetryEntry BuildTelemetry(
            uint operation,
            uint result,
            uint faultFlags,
            int bytes,
            ulong payloadHash,
            int recordCount,
            int pathBytes,
            long startTicks)
        {
            InputBindingTelemetryEntry entry = default;
            entry.RealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            entry.PayloadHash64 = payloadHash;
            entry.Frame = (uint)Mathf.Max(0, Time.frameCount);
            entry.Operation = operation;
            entry.Result = result;
            entry.Bytes = (uint)Mathf.Max(0, bytes);
            entry.DurationMicroseconds = ElapsedMicroseconds(startTicks);
            entry.FaultFlags = faultFlags;
            entry.BindingIndex = -1;
            entry.RecordCount = (ushort)Mathf.Clamp(recordCount, 0, ushort.MaxValue);
            entry.PathBytes = (ushort)Mathf.Clamp(pathBytes, 0, ushort.MaxValue);
            entry.IoPhase = (byte)operation;
            return entry;
        }

        private static uint ElapsedMicroseconds(long startTicks)
        {
            long elapsed = Stopwatch.GetTimestamp() - startTicks;
            if (elapsed <= 0)
                return 0u;

            double microseconds = elapsed * (1000000.0 / Stopwatch.Frequency);
            if (microseconds <= 0.0)
                return 0u;
            if (microseconds >= uint.MaxValue)
                return uint.MaxValue;
            return (uint)microseconds;
        }
    }
}
