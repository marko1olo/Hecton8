using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    public static unsafe class H8BridgeFacadeRuntime
    {
        private static int s_x001H8BridgeFacadeRuntimeSignalPushDropCount;
        public const int BlackBoxFrameCount = 300;
        public const int MaxDesignFacadeValueBytes = 64 * 1024;
        private const uint NanVaccinationHash = 0xA5AFE001u;
        private const uint LiveTuningStressHash = 0xA5AFE002u;
        private const uint PointerFenceFaultHash = 0xA5AFE003u;
        private const ulong DesignSyncMutationGuardMask =
            (1UL << (unchecked((int)(uint)(int)BufferID.BridgeDesignFacadeValues) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BridgeDesignFacadeTelemetryRing) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BridgeFacadeMacroHeader) & 31));
        private static int _blackBoxCursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Volatile.Write(ref _blackBoxCursor, 0);
        }

        public static bool LiveTuningBlockedByStress()
        {
            float registryStress = math.saturate(SignalBusRegistry.SystemStress01);
            float pressureStress = math.saturate(HomeostasisBrain.PressureLevel * (1f / 3f));
            float stress01 = math.max(registryStress, pressureStress);
            return stress01 > 0.9f;
        }

        public static bool SyncDesignData(H8DesignDataFacade facade, IDataVault vault, ushort extraFlags)
        {
            return SyncDesignData(facade, vault, extraFlags, null);
        }

        public static bool SyncDesignData(
            H8DesignDataFacade facade,
            IDataVault vault,
            ushort extraFlags,
            IMacroDatabaseService macroDatabase)
        {
            if (facade == null || vault == null)
                return false;

            int runtimeCount = facade.RefreshRuntimeBindingStateForSync();
            if (facade.ValidationDuplicateFieldHashCount > 0)
                return false;

            int rawCount = facade.BindingCount;
            long estimatedVramBytes = facade.EstimateVramBytes();
            if (runtimeCount <= 0)
            {
                if (!ClearDesignValueBuffer(vault))
                    return false;

                if (!PersistFacadeHeader(facade, vault, macroDatabase))
                    return false;

                RecordHeartbeat(vault, facade.FacadeHash, runtimeCount, estimatedVramBytes, extraFlags);
                PublishDesignClearSignal(facade.FacadeHash, extraFlags);
                return true;
            }

            if (!TryComputeDesignValueBufferLength(facade, rawCount, runtimeCount, out int requiredLength))
                return false;

            return SyncDesignValuesBulk(
                facade,
                vault,
                extraFlags,
                macroDatabase,
                rawCount,
                requiredLength,
                estimatedVramBytes);
        }

        private static bool TryComputeDesignValueBufferLength(
            H8DesignDataFacade facade,
            int rawCount,
            int runtimeCount,
            out int requiredLength)
        {
            requiredLength = 0;
            int validRuntimeCount = 0;
            for (int i = 0; i < rawCount; i++)
            {
                H8DesignDataFacade.FloatBinding binding = facade.GetBinding(i);
                if (binding == null || !binding.Enabled)
                    continue;

                H8DesignValueEntry entry = binding.ToValueEntry(facade.DesignerOverride);
                if (!TryComputeDesignEntryLength(in entry, out int entryRequiredLength))
                    return false;

                requiredLength = math.max(requiredLength, entryRequiredLength);
                validRuntimeCount++;
            }

            return validRuntimeCount == runtimeCount && requiredLength > 0 && requiredLength <= MaxDesignFacadeValueBytes;
        }

        private static bool TryComputeDesignEntryLength(in H8DesignValueEntry entry, out int requiredLength)
        {
            requiredLength = 0;
            if (entry.FieldHash == 0u || entry.OffsetBytes < 0)
                return false;

            if (entry.OffsetBytes > MaxDesignFacadeValueBytes - sizeof(float))
                return false;

            int alignedOffset = AlignFloatOffsetBytes(entry.OffsetBytes);
            requiredLength = alignedOffset + sizeof(float);
            return requiredLength > 0 && requiredLength <= MaxDesignFacadeValueBytes;
        }

        private static bool SyncDesignValuesBulk(
            H8DesignDataFacade facade,
            IDataVault vault,
            ushort extraFlags,
            IMacroDatabaseService macroDatabase,
            int rawCount,
            int requiredLength,
            long estimatedVramBytes)
        {
            if (facade == null ||
                vault == null ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(DesignSyncMutationGuardMask))
            {
                return false;
            }

            H8FacadeMacroHeader header = BuildFacadeHeader(facade, estimatedVramBytes);
            bool bulkWritten = false;
            try
            {
                if (!TryResolveGuardedBuffer(
                        vault,
                        BufferID.BridgeDesignFacadeValues,
                        requiredLength,
                        NativeArrayOptions.ClearMemory,
                        out VaultGenerationHandle<byte> _,
                        out NativeArray<byte> valueBuffer) ||
                    !TryResolveGuardedBuffer(
                        vault,
                        BufferID.BridgeDesignFacadeTelemetryRing,
                        BlackBoxFrameCount,
                        NativeArrayOptions.ClearMemory,
                        out VaultGenerationHandle<H8FacadeTelemetryEntry> _,
                        out NativeArray<H8FacadeTelemetryEntry> telemetryRing) ||
                    !TryResolveGuardedBuffer(
                        vault,
                        BufferID.BridgeFacadeMacroHeader,
                        1,
                        NativeArrayOptions.ClearMemory,
                        out VaultGenerationHandle<H8FacadeMacroHeader> _,
                        out NativeArray<H8FacadeMacroHeader> headerBuffer))
                {
                    return false;
                }

                byte* basePtr = (byte*)valueBuffer.GetUnsafePtr();
                if (basePtr == null)
                    return false;

                Thread.MemoryBarrier();
                for (int i = 0; i < rawCount; i++)
                {
                    H8DesignDataFacade.FloatBinding binding = facade.GetBinding(i);
                    if (binding == null || !binding.Enabled)
                        continue;

                    if (!TryPrepareDesignEntry(binding, facade.DesignerOverride, out H8DesignValueEntry entry, out int entryRequiredLength) ||
                        entryRequiredLength > valueBuffer.Length)
                    {
                        return false;
                    }

                    float* valuePtr = (float*)(basePtr + entry.OffsetBytes);
                    float oldValue = *valuePtr;
                    *valuePtr = entry.Value;
                    RecordDeltaLocked(telemetryRing, facade.FacadeHash, entry, oldValue, extraFlags);
                    PublishDesignValueSignal(facade.FacadeHash, entry, oldValue, extraFlags);
                }

                RecordHeartbeatLocked(telemetryRing, facade.FacadeHash, facade.RuntimeBindingCount, estimatedVramBytes, extraFlags);
                headerBuffer[0] = header;
                Thread.MemoryBarrier();
                bulkWritten = true;
            }
            catch (Exception)
            {
                bulkWritten = false;
            }
            finally
            {
                vault.ReleaseMutationGuard(DesignSyncMutationGuardMask);
            }

            return bulkWritten && MarkFacadeHeaderDirty(macroDatabase, in header);
        }

        private static bool TryResolveGuardedBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.CoreBridge,
                options);

            return handle.BufferID != 0u &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryPrepareDesignEntry(
            H8DesignDataFacade.FloatBinding binding,
            bool designerOverride,
            out H8DesignValueEntry entry,
            out int requiredLength)
        {
            entry = default;
            requiredLength = 0;
            if (binding == null || !binding.Enabled)
                return false;

            entry = binding.ToValueEntry(designerOverride);
            if (!TryComputeDesignEntryLength(in entry, out requiredLength))
                return false;

            entry.OffsetBytes = AlignFloatOffsetBytes(entry.OffsetBytes);
            entry.Value = SanitizeValue(entry.Value, entry.SafeDefault, entry.MinValue, entry.MaxValue, entry.Flags, entry.FieldHash);
            return true;
        }

        private static void PublishDesignValueSignal(uint facadeHash, H8DesignValueEntry entry, float oldValue, ushort extraFlags)
        {
            if (!Application.isPlaying)
            {
                GlobalTelemetryBus.PublishModTelemetry(facadeHash, entry.FieldHash, entry.Value);
                return;
            }

            if ((entry.Flags & (ushort)H8DesignValueFlags.LiveTuning) != 0 &&
                ((entry.Flags | extraFlags) & (ushort)H8DesignValueFlags.DesignerOverride) == 0 &&
                LiveTuningBlockedByStress())
            {
                GlobalTelemetryBus.PublishPerformanceWarning(LiveTuningStressHash, entry.FieldHash, entry.Value);
                return;
            }

            DataVaultUpdateSignal signal = new DataVaultUpdateSignal
            {
                SourceHash = facadeHash,
                FieldHash = entry.FieldHash,
                OffsetBytes = entry.OffsetBytes,
                OldValue = oldValue,
                NewValue = entry.Value,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                BufferId = (ushort)BufferID.BridgeDesignFacadeValues,
                Flags = (ushort)(entry.Flags | extraFlags)
            };
            SignalBus<DataVaultUpdateSignal>.TryPushTracked(in signal, ref s_x001H8BridgeFacadeRuntimeSignalPushDropCount);
            GlobalTelemetryBus.PublishModTelemetry(facadeHash, entry.FieldHash, entry.Value);
        }

        private static bool ClearDesignValueBuffer(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetGenerationHandle<byte>(BufferID.BridgeDesignFacadeValues, out VaultGenerationHandle<byte> bytes) ||
                bytes.BufferID == 0u)
                return true;

            if (!vault.TryAcquireWriteLock(in bytes, SystemID.CoreBridge, out NativeArray<byte> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length <= 0)
                    return true;

                byte* ptr = (byte*)buffer.GetUnsafePtr();
                if (ptr == null)
                    return false;

                Thread.MemoryBarrier();
                UnsafeUtility.MemClear(ptr, buffer.Length);
                Thread.MemoryBarrier();
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in bytes, SystemID.CoreBridge);
            }
        }

        private static void PublishDesignClearSignal(uint facadeHash, ushort extraFlags)
        {
            if (!Application.isPlaying)
                return;

            DataVaultUpdateSignal signal = new DataVaultUpdateSignal
            {
                SourceHash = facadeHash,
                FieldHash = H8BridgeHashes.BridgeHeartbeat,
                OffsetBytes = -1,
                OldValue = 0f,
                NewValue = 0f,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                BufferId = (ushort)BufferID.BridgeDesignFacadeValues,
                Flags = extraFlags
            };
            SignalBus<DataVaultUpdateSignal>.TryPushTracked(in signal, ref s_x001H8BridgeFacadeRuntimeSignalPushDropCount);
            GlobalTelemetryBus.PublishModTelemetry(facadeHash, H8BridgeHashes.BridgeHeartbeat, 0f);
        }

        public static bool WriteDesignValue(
            IDataVault vault,
            uint facadeHash,
            H8DesignValueEntry entry,
            ushort extraFlags,
            out float oldValue)
        {
            oldValue = 0f;
            if (vault == null || entry.FieldHash == 0u || entry.OffsetBytes < 0)
                return false;

            entry.OffsetBytes = AlignFloatOffsetBytes(entry.OffsetBytes);
            if (entry.OffsetBytes > MaxDesignFacadeValueBytes - sizeof(float))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, entry.FieldHash, entry.OffsetBytes);
                return false;
            }

            float value = SanitizeValue(entry.Value, entry.SafeDefault, entry.MinValue, entry.MaxValue, entry.Flags, entry.FieldHash);
            entry.Value = value;

            int requiredLength = entry.OffsetBytes + sizeof(float);
            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<byte> bytes = vault.EnsureGenerationHandle<byte>(
                BufferID.BridgeDesignFacadeValues,
                requiredLength,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (bytes.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in bytes, SystemID.CoreBridge, out NativeArray<byte> buffer))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, entry.FieldHash, requiredLength);
                return false;
            }

            bool valueWriteSucceeded = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    Thread.MemoryBarrier();
                    byte* basePtr = (byte*)buffer.GetUnsafePtr();
                    if (basePtr != null)
                    {
                        float* valuePtr = (float*)(basePtr + entry.OffsetBytes);
                        oldValue = *valuePtr;
                        Thread.MemoryBarrier();
                        *valuePtr = value;
                        Thread.MemoryBarrier();
                        valueWriteSucceeded = true;
                    }
                }
            }
            catch (Exception)
            {
                valueWriteSucceeded = false;
            }
            finally
            {
                vault.ReleaseWriteLock(in bytes, SystemID.CoreBridge);
            }

            if (!valueWriteSucceeded)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, entry.FieldHash, requiredLength);
                return false;
            }

            RecordDelta(vault, facadeHash, entry, oldValue, extraFlags);

            if (!Application.isPlaying)
            {
                GlobalTelemetryBus.PublishModTelemetry(facadeHash, entry.FieldHash, value);
                return true;
            }

            if ((entry.Flags & (ushort)H8DesignValueFlags.LiveTuning) != 0 &&
                ((entry.Flags | extraFlags) & (ushort)H8DesignValueFlags.DesignerOverride) == 0 &&
                LiveTuningBlockedByStress())
            {
                GlobalTelemetryBus.PublishPerformanceWarning(LiveTuningStressHash, entry.FieldHash, value);
                return true;
            }

            DataVaultUpdateSignal signal = new DataVaultUpdateSignal
            {
                SourceHash = facadeHash,
                FieldHash = entry.FieldHash,
                OffsetBytes = entry.OffsetBytes,
                OldValue = oldValue,
                NewValue = value,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                BufferId = (ushort)BufferID.BridgeDesignFacadeValues,
                Flags = (ushort)(entry.Flags | extraFlags)
            };
            SignalBus<DataVaultUpdateSignal>.TryPushTracked(in signal, ref s_x001H8BridgeFacadeRuntimeSignalPushDropCount);
            GlobalTelemetryBus.PublishModTelemetry(facadeHash, entry.FieldHash, value);
            return true;
        }

        public static long EstimateTextureBytes(int width, int height, int mipCount, int bytesPerPixel)
        {
            long safeWidth = math.min(16384, width > 0 ? width : 1);
            long safeHeight = math.min(16384, height > 0 ? height : 1);
            long safeBpp = math.min(16, bytesPerPixel > 0 ? bytesPerPixel : 1);
            long baseBytes = safeWidth * safeHeight * safeBpp;
            if (mipCount <= 1)
                return baseBytes;

            long total = 0L;
            long mipBytes = baseBytes;
            int safeMips = math.min(16, math.max(1, mipCount));
            for (int i = 0; i < safeMips; i++)
            {
                total += mipBytes > 1L ? mipBytes : 1L;
                mipBytes >>= 2;
                if (mipBytes <= 0L)
                    break;
            }

            return total;
        }

        public static int AlignFloatOffsetBytes(int offsetBytes)
        {
            if (offsetBytes <= 0)
                return 0;

            int maxOffset = MaxDesignFacadeValueBytes - sizeof(float);
            if (offsetBytes > maxOffset)
                return maxOffset;

            int aligned = (offsetBytes + 3) & ~3;
            return aligned > maxOffset ? maxOffset : aligned;
        }

        public static uint ComputeFacadeChecksum(H8DesignDataFacade facade)
        {
            if (facade == null)
                return H8BridgeHashes.FnvOffset;

            uint hash = H8BridgeHashes.Mix(H8BridgeHashes.FnvOffset, facade.FacadeHash);
            int count = facade.BindingCount;
            for (int i = 0; i < count; i++)
            {
                H8DesignDataFacade.FloatBinding binding = facade.GetBinding(i);
                if (binding == null || !binding.Enabled)
                    continue;

                H8DesignValueEntry entry = binding.ToValueEntry(facade.DesignerOverride);
                hash = H8BridgeHashes.Mix(hash, entry.FieldHash);
                hash = H8BridgeHashes.Mix(hash, unchecked((uint)entry.OffsetBytes));
                hash = H8BridgeHashes.Mix(hash, H8BridgeHashes.FloatToUInt32Bits(entry.Value));
                hash = H8BridgeHashes.Mix(hash, entry.LutSwapHash);
            }

            return hash;
        }

        public static void RecordHeartbeat(IDataVault vault, uint facadeHash, int bindingCount, long estimatedVramBytes, ushort flags)
        {
            if (vault == null)
                return;

            H8DesignValueEntry heartbeat = BuildHeartbeatEntry(bindingCount, estimatedVramBytes, flags);
            RecordDelta(vault, facadeHash, heartbeat, bindingCount, flags);
        }

        private static void RecordHeartbeatLocked(
            NativeArray<H8FacadeTelemetryEntry> telemetryRing,
            uint facadeHash,
            int bindingCount,
            long estimatedVramBytes,
            ushort flags)
        {
            H8DesignValueEntry heartbeat = BuildHeartbeatEntry(bindingCount, estimatedVramBytes, flags);
            RecordDeltaLocked(telemetryRing, facadeHash, heartbeat, bindingCount, flags);
        }

        private static H8DesignValueEntry BuildHeartbeatEntry(int bindingCount, long estimatedVramBytes, ushort flags)
        {
            return new H8DesignValueEntry
            {
                FieldHash = H8BridgeHashes.BridgeHeartbeat,
                OffsetBytes = -1,
                Value = estimatedVramBytes > 0L ? estimatedVramBytes * (1f / (1024f * 1024f)) : 0f,
                SafeDefault = bindingCount > 0 ? bindingCount : 0,
                MinValue = 0f,
                MaxValue = 65535f,
                Flags = flags
            };
        }

        public static bool PersistFacadeHeader(H8DesignDataFacade facade, IDataVault vault)
        {
            return PersistFacadeHeader(facade, vault, null);
        }

        public static bool PersistFacadeHeader(
            H8DesignDataFacade facade,
            IDataVault vault,
            IMacroDatabaseService macroDatabase)
        {
            if (facade == null || vault == null)
                return false;

            H8FacadeMacroHeader header = BuildFacadeHeader(facade, facade.EstimateVramBytes());

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<H8FacadeMacroHeader> headerBuffer = vault.EnsureGenerationHandle<H8FacadeMacroHeader>(
                BufferID.BridgeFacadeMacroHeader,
                1,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);
            bool headerWritten = false;
            if (headerBuffer.BufferID != 0u &&
                vault.TryAcquireWriteLock(in headerBuffer, SystemID.CoreBridge, out NativeArray<H8FacadeMacroHeader> headerBufferView))
            {
                try
                {
                    if (headerBufferView.IsCreated && headerBufferView.Length > 0)
                    {
                        Thread.MemoryBarrier();
                        headerBufferView[0] = header;
                        Thread.MemoryBarrier();
                        headerWritten = true;
                    }
                }
                finally
                {
                    vault.ReleaseWriteLock(in headerBuffer, SystemID.CoreBridge);
                }
            }

            if (!headerWritten)
                return false;

            return MarkFacadeHeaderDirty(macroDatabase, in header);
        }

        private static H8FacadeMacroHeader BuildFacadeHeader(H8DesignDataFacade facade, long estimatedVramBytes)
        {
            return new H8FacadeMacroHeader
            {
                Magic = H8BridgeHashes.MacroHeaderMagic,
                Version = H8BridgeHashes.MacroHeaderVersion,
                FacadeHash = facade.FacadeHash,
                LastChangedFieldHash = facade.LastChangedFieldHash,
                FieldCount = unchecked((uint)facade.RuntimeBindingCount),
                PrefabCount = 0u,
                InputBindingCount = 0u,
                Checksum = ComputeFacadeChecksum(facade),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Flags = facade.DesignerOverride ? (uint)H8DesignValueFlags.DesignerOverride : 0u,
                EstimatedVramBytes = estimatedVramBytes,
                OneDimensionalLutHash = facade.OneDimensionalLutHash,
                HighTierVisualHash = facade.HighTierVisualHash
            };
        }

        private static bool MarkFacadeHeaderDirty(IMacroDatabaseService macroDatabase, in H8FacadeMacroHeader header)
        {
            if (macroDatabase == null || !macroDatabase.IsOpen)
                return true;

            return macroDatabase.MarkDirty(
                H8BridgeHashes.FacadeMacroHeaderSectorHash,
                in header,
                MacroDatabasePayloadFlags.Dirty);
        }

        private static float SanitizeValue(
            float value,
            float safeDefault,
            float minValue,
            float maxValue,
            ushort flags,
            uint fieldHash)
        {
            bool critical = (flags & (ushort)H8DesignValueFlags.Critical) != 0;
            float fallback = math.isfinite(safeDefault) && safeDefault != 0f ? safeDefault : 1f;
            bool invalid = !math.isfinite(value) || (critical && math.abs(value) <= float.Epsilon);
            if (invalid)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)fieldHash));
                GlobalTelemetryBus.PublishPerformanceWarning(NanVaccinationHash, fieldHash, fallback);
                RequestBlackBoxDump();
                value = fallback;
            }

            if (math.isfinite(minValue) && math.isfinite(maxValue) && maxValue > minValue)
                value = math.clamp(value, minValue, maxValue);

            if (!math.isfinite(value))
                value = fallback;

            return value;
        }

        private static void RecordDelta(
            IDataVault vault,
            uint facadeHash,
            H8DesignValueEntry entry,
            float oldValue,
            ushort extraFlags)
        {
            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            VaultGenerationHandle<H8FacadeTelemetryEntry> ring = vault.EnsureGenerationHandle<H8FacadeTelemetryEntry>(
                BufferID.BridgeDesignFacadeTelemetryRing,
                BlackBoxFrameCount,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (ring.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in ring, SystemID.CoreBridge, out NativeArray<H8FacadeTelemetryEntry> ringBuffer))
            {
                return;
            }

            try
            {
                if (!ringBuffer.IsCreated || ringBuffer.Length < BlackBoxFrameCount)
                    return;

                int index = Interlocked.Increment(ref _blackBoxCursor) - 1;
                if (index < 0)
                    index = 0;

                H8FacadeTelemetryEntry telemetry = default;
                telemetry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                telemetry.FacadeHash = facadeHash;
                telemetry.FieldHash = entry.FieldHash;
                telemetry.OffsetBytes = entry.OffsetBytes;
                telemetry.OldValue = oldValue;
                telemetry.NewValue = entry.Value;
                telemetry.SafeDefault = entry.SafeDefault;
                telemetry.LutSwapHash = entry.LutSwapHash;
                telemetry.Flags = (ushort)(entry.Flags | extraFlags);
                Thread.MemoryBarrier();
                ringBuffer[index % BlackBoxFrameCount] = telemetry;
                Thread.MemoryBarrier();
            }
            finally
            {
                vault.ReleaseWriteLock(in ring, SystemID.CoreBridge);
            }
        }

        private static void RecordDeltaLocked(
            NativeArray<H8FacadeTelemetryEntry> ringBuffer,
            uint facadeHash,
            H8DesignValueEntry entry,
            float oldValue,
            ushort extraFlags)
        {
            if (!ringBuffer.IsCreated || ringBuffer.Length < BlackBoxFrameCount)
                return;

            int index = Interlocked.Increment(ref _blackBoxCursor) - 1;
            if (index < 0)
                index = 0;

            H8FacadeTelemetryEntry telemetry = default;
            telemetry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            telemetry.FacadeHash = facadeHash;
            telemetry.FieldHash = entry.FieldHash;
            telemetry.OffsetBytes = entry.OffsetBytes;
            telemetry.OldValue = oldValue;
            telemetry.NewValue = entry.Value;
            telemetry.SafeDefault = entry.SafeDefault;
            telemetry.LutSwapHash = entry.LutSwapHash;
            telemetry.Flags = (ushort)(entry.Flags | extraFlags);
            ringBuffer[index % BlackBoxFrameCount] = telemetry;
        }

        public static void RequestBlackBoxDump()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle<H8FacadeTelemetryEntry>(BufferID.BridgeDesignFacadeTelemetryRing, out VaultGenerationHandle<H8FacadeTelemetryEntry> ring) ||
                ring.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in ring, out NativeArray<H8FacadeTelemetryEntry>.ReadOnly ringBuffer) ||
                !ringBuffer.IsCreated ||
                ringBuffer.Length == 0)
            {
                return;
            }

            string path = ResolveDumpPath();
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int capacity = math.min(ringBuffer.Length, BlackBoxFrameCount);
                int cursor = Volatile.Read(ref _blackBoxCursor);
                if (cursor < 0)
                    cursor = 0;

                int entryCount = math.min(cursor, capacity);
                int startIndex = cursor >= capacity && capacity > 0 ? cursor % capacity : 0;
                int entrySize = UnsafeUtility.SizeOf<H8FacadeTelemetryEntry>();
                uint payloadHash = ComputeTelemetryDumpHash(ringBuffer, startIndex, entryCount, capacity);
                H8FacadeTelemetryDumpHeader header = new H8FacadeTelemetryDumpHeader
                {
                    Magic = H8BridgeHashes.TelemetryDumpMagic,
                    Version = H8BridgeHashes.TelemetryDumpVersion,
                    EntryCount = unchecked((uint)entryCount),
                    EntrySizeBytes = unchecked((uint)entrySize),
                    Cursor = unchecked((uint)cursor),
                    Capacity = unchecked((uint)capacity),
                    PayloadHash = payloadHash
                };

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(new ReadOnlySpan<byte>(&header, UnsafeUtility.SizeOf<H8FacadeTelemetryDumpHeader>()));
                    for (int i = 0; i < entryCount; i++)
                    {
                        int index = (startIndex + i) % capacity;
                        H8FacadeTelemetryEntry entry = ringBuffer[index];
                        stream.Write(new ReadOnlySpan<byte>(&entry, entrySize));
                    }
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, H8BridgeHashes.DesignFacade, 0f);
            }
        }

        private static uint ComputeTelemetryDumpHash(
            NativeArray<H8FacadeTelemetryEntry>.ReadOnly ringBuffer,
            int startIndex,
            int entryCount,
            int capacity)
        {
            if (!ringBuffer.IsCreated || entryCount <= 0 || capacity <= 0)
                return H8BridgeHashes.FnvOffset;

            uint hash = H8BridgeHashes.Mix(H8BridgeHashes.FnvOffset, H8BridgeHashes.TelemetryDumpMagic);
            for (int i = 0; i < entryCount; i++)
            {
                H8FacadeTelemetryEntry entry = ringBuffer[(startIndex + i) % capacity];
                hash = H8BridgeHashes.Mix(hash, entry.Frame);
                hash = H8BridgeHashes.Mix(hash, entry.FacadeHash);
                hash = H8BridgeHashes.Mix(hash, entry.FieldHash);
                hash = H8BridgeHashes.Mix(hash, unchecked((uint)entry.OffsetBytes));
                hash = H8BridgeHashes.Mix(hash, H8BridgeHashes.FloatToUInt32Bits(entry.OldValue));
                hash = H8BridgeHashes.Mix(hash, H8BridgeHashes.FloatToUInt32Bits(entry.NewValue));
                hash = H8BridgeHashes.Mix(hash, H8BridgeHashes.FloatToUInt32Bits(entry.SafeDefault));
                hash = H8BridgeHashes.Mix(hash, entry.LutSwapHash);
                hash = H8BridgeHashes.Mix(hash, entry.Flags);
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ResolveDumpPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_ARCHITECT_BRIDGE_FACADE.bin"));
        }
    }
}
