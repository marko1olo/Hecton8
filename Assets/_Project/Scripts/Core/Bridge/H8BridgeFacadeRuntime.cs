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
        public const int BlackBoxFrameCount = 300;
        private const uint NanVaccinationHash = 0xA5AFE001u;
        private const uint LiveTuningStressHash = 0xA5AFE002u;
        private const uint PointerFenceFaultHash = 0xA5AFE003u;
        private static int _blackBoxCursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Volatile.Write(ref _blackBoxCursor, 0);
        }

        public static bool LiveTuningBlockedByStress()
        {
            float registryStress = SignalBusRegistry.SystemStress01;
            float homeostasisStress = HomeostasisBrain.SystemHealthIndex01;
            float stress01 = math.max(registryStress, homeostasisStress);
            return stress01 > 0.9f;
        }

        public static bool SyncDesignData(H8DesignDataFacade facade, IDataVault vault, ushort extraFlags)
        {
            if (facade == null || vault == null)
                return false;

            int count = facade.BindingCount;
            if (count <= 0)
                return true;

            bool success = true;
            for (int i = 0; i < count; i++)
            {
                H8DesignDataFacade.FloatBinding binding = facade.GetBinding(i);
                if (binding == null || !binding.Enabled)
                    continue;

                H8DesignValueEntry entry = binding.ToValueEntry(facade.DesignerOverride);
                float oldValue;
                if (!WriteDesignValue(vault, facade.FacadeHash, entry, extraFlags, out oldValue))
                    success = false;
            }

            PersistFacadeHeader(facade, vault);
            return success;
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

            float value = SanitizeValue(entry.Value, entry.SafeDefault, entry.MinValue, entry.MaxValue, entry.Flags, entry.FieldHash);
            entry.Value = value;

            int requiredLength = entry.OffsetBytes + sizeof(float);
            NativeArray<byte> bytes = vault.GetBuffer<byte>(
                BufferID.BridgeDesignFacadeValues,
                requiredLength,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (!bytes.IsCreated || bytes.Length < requiredLength)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, entry.FieldHash, requiredLength);
                return false;
            }

            try
            {
                Thread.MemoryBarrier();
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bytes);
                float* valuePtr = (float*)(basePtr + entry.OffsetBytes);
                oldValue = *valuePtr;
                Thread.MemoryBarrier();
                *valuePtr = value;
                Thread.MemoryBarrier();
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, entry.FieldHash, requiredLength);
                return false;
            }

            RecordDelta(vault, facadeHash, entry, oldValue, extraFlags);

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
                Frame = unchecked((uint)Time.frameCount),
                BufferId = (ushort)BufferID.BridgeDesignFacadeValues,
                Flags = (ushort)(entry.Flags | extraFlags)
            };
            SignalBus<DataVaultUpdateSignal>.Push(signal);
            GlobalTelemetryBus.PublishModTelemetry(facadeHash, entry.FieldHash, value);
            return true;
        }

        public static long EstimateTextureBytes(int width, int height, int mipCount, int bytesPerPixel)
        {
            long safeWidth = width > 0 ? width : 1;
            long safeHeight = height > 0 ? height : 1;
            long safeBpp = bytesPerPixel > 0 ? bytesPerPixel : 1;
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

        public static uint ComputeFacadeChecksum(H8DesignDataFacade facade)
        {
            if (facade == null)
                return H8BridgeHashes.FnvOffset;

            uint hash = H8BridgeHashes.Mix(H8BridgeHashes.FnvOffset, facade.FacadeHash);
            int count = facade.BindingCount;
            for (int i = 0; i < count; i++)
            {
                H8DesignDataFacade.FloatBinding binding = facade.GetBinding(i);
                if (binding == null)
                    continue;

                H8DesignValueEntry entry = binding.ToValueEntry(facade.DesignerOverride);
                hash = H8BridgeHashes.Mix(hash, entry.FieldHash);
                hash = H8BridgeHashes.Mix(hash, unchecked((uint)entry.OffsetBytes));
                hash = H8BridgeHashes.Mix(hash, H8BridgeHashes.FloatToUInt32Bits(entry.Value));
                hash = H8BridgeHashes.Mix(hash, entry.LutSwapHash);
            }

            return hash;
        }

        public static void PersistFacadeHeader(H8DesignDataFacade facade, IDataVault vault)
        {
            if (facade == null || vault == null)
                return;

            H8FacadeMacroHeader header = new H8FacadeMacroHeader
            {
                Magic = H8BridgeHashes.MacroHeaderMagic,
                Version = H8BridgeHashes.MacroHeaderVersion,
                FacadeHash = facade.FacadeHash,
                LastChangedFieldHash = facade.LastChangedFieldHash,
                FieldCount = unchecked((uint)facade.BindingCount),
                PrefabCount = 0u,
                InputBindingCount = 0u,
                Checksum = ComputeFacadeChecksum(facade),
                Frame = unchecked((uint)Time.frameCount),
                Flags = facade.DesignerOverride ? (uint)H8DesignValueFlags.DesignerOverride : 0u,
                EstimatedVramBytes = facade.EstimateVramBytes(),
                OneDimensionalLutHash = facade.OneDimensionalLutHash,
                HighTierVisualHash = facade.HighTierVisualHash
            };

            NativeArray<H8FacadeMacroHeader> headerBuffer = vault.GetBuffer<H8FacadeMacroHeader>(
                BufferID.BridgeFacadeMacroHeader,
                1,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);
            if (headerBuffer.IsCreated && headerBuffer.Length > 0)
                headerBuffer[0] = header;

            IMacroDatabaseService macroDatabase = GlobalRegistry.MacroDatabase;
            if (macroDatabase == null || !macroDatabase.IsOpen)
                return;

            H8FacadeMacroHeader* headerPtr = &header;
            macroDatabase.MarkDirty(
                H8BridgeHashes.FacadeMacroHeaderSectorHash,
                (IntPtr)headerPtr,
                UnsafeUtility.SizeOf<H8FacadeMacroHeader>(),
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
            NativeArray<H8FacadeTelemetryEntry> ring = vault.GetBuffer<H8FacadeTelemetryEntry>(
                BufferID.BridgeDesignFacadeTelemetryRing,
                BlackBoxFrameCount,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (!ring.IsCreated || ring.Length < BlackBoxFrameCount)
                return;

            int index = Interlocked.Increment(ref _blackBoxCursor) - 1;
            if (index < 0)
                index = 0;

            H8FacadeTelemetryEntry telemetry = new H8FacadeTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                FacadeHash = facadeHash,
                FieldHash = entry.FieldHash,
                OffsetBytes = entry.OffsetBytes,
                OldValue = oldValue,
                NewValue = entry.Value,
                SafeDefault = entry.SafeDefault,
                LutSwapHash = entry.LutSwapHash,
                Flags = (ushort)(entry.Flags | extraFlags)
            };
            ring[index % BlackBoxFrameCount] = telemetry;
        }

        public static void RequestBlackBoxDump()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!vault.TryGetBuffer(BufferID.BridgeDesignFacadeTelemetryRing, out NativeArray<H8FacadeTelemetryEntry> ring) ||
                !ring.IsCreated ||
                ring.Length == 0)
            {
                return;
            }

            string path = ResolveDumpPath();
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                int bytes = ring.Length * UnsafeUtility.SizeOf<H8FacadeTelemetryEntry>();
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(PointerFenceFaultHash, H8BridgeHashes.DesignFacade, 0f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ResolveDumpPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_ARCHITECT_BRIDGE_FACADE.bin"));
        }
    }
}
