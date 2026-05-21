using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Environment.Fluids
{
    public struct OceanAdapterVaultLane<T> where T : struct
    {
        public VaultGenerationHandle<T> Handle;
        public uint ExpectedBufferID;
        public int Length;
    }

    public struct OceanAdapterVaultHandles
    {
        public OceanAdapterVaultLane<OceanSampleRequestDTO> Requests;
        public OceanAdapterVaultLane<OceanSampleResultDTO> Results;
        public OceanAdapterVaultLane<OceanAdapterTelemetryEntry> TelemetryRing;
        public OceanAdapterVaultLane<OceanPerformanceProfileDTO> Profiles;
        public OceanAdapterVaultLane<OceanGlobalWaterLevelDTO> GlobalWaterLevel;
        public OceanAdapterVaultLane<byte> CsvScratch;
    }

    public static class OceanAdapterVaultRoute
    {
        public const int RequestCapacity = 50000;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 16;
        public const int CsvScratchBytes = 65536;
        public const BufferID RequestBufferID = (BufferID)72960;
        public const BufferID ResultBufferID = (BufferID)72961;
        public const BufferID TelemetryRingBufferID = (BufferID)72962;
        public const BufferID ProfileBufferID = (BufferID)72963;
        public const BufferID GlobalWaterLevelBufferID = (BufferID)72964;
        public const BufferID CsvScratchBufferID = (BufferID)72965;
        private const SystemID OwnerSystem = SystemID.Fluid;

        public static bool TryAcquireBootHandles(IDataVault vault, out OceanAdapterVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            handles.Requests = AcquireLane<OceanSampleRequestDTO>(
                vault,
                RequestBufferID,
                RequestCapacity,
                NativeArrayOptions.UninitializedMemory);
            handles.Results = AcquireLane<OceanSampleResultDTO>(
                vault,
                ResultBufferID,
                RequestCapacity,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = AcquireLane<OceanAdapterTelemetryEntry>(
                vault,
                TelemetryRingBufferID,
                TelemetryCapacity,
                NativeArrayOptions.UninitializedMemory);
            handles.Profiles = AcquireLane<OceanPerformanceProfileDTO>(
                vault,
                ProfileBufferID,
                ProfileCapacity,
                NativeArrayOptions.UninitializedMemory);
            handles.GlobalWaterLevel = AcquireLane<OceanGlobalWaterLevelDTO>(
                vault,
                GlobalWaterLevelBufferID,
                1,
                NativeArrayOptions.UninitializedMemory);
            handles.CsvScratch = AcquireLane<byte>(
                vault,
                CsvScratchBufferID,
                CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory);
            return AreHandlesBound(in handles);
        }

        public static bool TryPublishWaterLevel(
            IDataVault vault,
            float waterLevel,
            float globalQualityWeight,
            uint frameIndex)
        {
            if (vault == null)
                return false;

            if (!TryOpenOrAcquireLane(
                    vault,
                    GlobalWaterLevelBufferID,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<OceanGlobalWaterLevelDTO> buffer))
            {
                return false;
            }

            OceanGlobalWaterLevelDTO row = default;
            row.WaterLevel = math.select(0f, waterLevel, math.isfinite(waterLevel));
            row.GlobalQualityWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            row.FrameIndex = frameIndex;
            row.Flags = 1u;
            buffer[0] = row;
            return true;
        }

        public static bool TryRecordTelemetry(
            IDataVault vault,
            in OceanAdapterTelemetryEntry entry,
            uint frameIndex)
        {
            if (vault == null)
                return false;

            if (!TryOpenOrAcquireLane(
                    vault,
                    TelemetryRingBufferID,
                    TelemetryCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<OceanAdapterTelemetryEntry> telemetry))
            {
                return false;
            }

            int index = (int)(frameIndex % (uint)telemetry.Length);
            telemetry[index] = entry;
            return true;
        }

        private static OceanAdapterVaultLane<T> AcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return default;

            return new OceanAdapterVaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = requiredLength
            };
        }

        private static bool AreHandlesBound(in OceanAdapterVaultHandles handles)
        {
            return IsLaneBound(in handles.Requests) &&
                   IsLaneBound(in handles.Results) &&
                   IsLaneBound(in handles.TelemetryRing) &&
                   IsLaneBound(in handles.Profiles) &&
                   IsLaneBound(in handles.GlobalWaterLevel) &&
                   IsLaneBound(in handles.CsvScratch);
        }

        private static bool IsLaneBound<T>(in OceanAdapterVaultLane<T> lane) where T : struct
        {
            return lane.ExpectedBufferID != 0u &&
                   lane.Handle.BufferID == lane.ExpectedBufferID &&
                   lane.Handle.Generation != 0u &&
                   lane.Length > 0;
        }

        private static bool TryOpenOrAcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            uint expectedBufferId = unchecked((uint)(int)bufferId);
            OceanAdapterVaultLane<T> lane = default;
            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                existing.BufferID == expectedBufferId &&
                existing.Generation != 0u)
            {
                lane.Handle = existing;
                lane.ExpectedBufferID = expectedBufferId;
                lane.Length = requiredLength;
            }
            else
            {
                lane = AcquireLane<T>(vault, bufferId, requiredLength, options);
            }

            return OpenLane(vault, in lane, out buffer);
        }

        private static bool OpenLane<T>(
            IDataVault vault,
            in OceanAdapterVaultLane<T> lane,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || !IsLaneBound(in lane))
                return false;

            if (!vault.TryResolveHandle(in lane.Handle, out buffer))
                return false;

            return buffer.IsCreated && buffer.Length >= lane.Length;
        }
    }
}
