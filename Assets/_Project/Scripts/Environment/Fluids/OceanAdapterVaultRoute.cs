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
        public const float DefaultWaterLevel = 14.02f;
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

            if (!TryAcquireExistingLaneWriteLock(
                    vault,
                    GlobalWaterLevelBufferID,
                    1,
                    out VaultGenerationHandle<OceanGlobalWaterLevelDTO> handle,
                    out NativeArray<OceanGlobalWaterLevelDTO> buffer))
            {
                return false;
            }

            try
            {
                OceanGlobalWaterLevelDTO row = default;
                row.WaterLevel = ResolveWaterLevel(waterLevel);
                row.GlobalQualityWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
                row.FrameIndex = frameIndex;
                row.Flags = 1u;
                buffer[0] = row;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private static float ResolveWaterLevel(float candidateWaterLevel)
        {
            return math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) > 0.0001f &&
                math.abs(candidateWaterLevel) <= 1000f
                ? candidateWaterLevel
                : DefaultWaterLevel;
        }

        public static bool TryRecordTelemetry(
            IDataVault vault,
            in OceanAdapterTelemetryEntry entry,
            uint frameIndex)
        {
            if (vault == null)
                return false;

            if (!TryAcquireExistingLaneWriteLock(
                    vault,
                    TelemetryRingBufferID,
                    TelemetryCapacity,
                    out VaultGenerationHandle<OceanAdapterTelemetryEntry> handle,
                    out NativeArray<OceanAdapterTelemetryEntry> telemetry))
            {
                return false;
            }

            try
            {
                int index = (int)(frameIndex % (uint)telemetry.Length);
                telemetry[index] = entry;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private static OceanAdapterVaultLane<T> AcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
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

        private static bool TryAcquireExistingLaneWriteLock<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                handle.BufferID != expectedBufferId ||
                handle.Generation == 0u)
                return false;

            OceanAdapterVaultLane<T> lane = new OceanAdapterVaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = requiredLength
            };

            if (!IsLaneBound(in lane))
                return false;

            if (!vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))
                return false;

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= lane.Length)
                {
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }
    }
}
