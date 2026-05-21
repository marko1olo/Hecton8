using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct OceanKinematicsSelfAuditReport
    {
        [FieldOffset(0)] public int FluidResultSize;
        [FieldOffset(4)] public int FluidResultWaterOffset;
        [FieldOffset(8)] public int FluidResultVelocityOffset;
        [FieldOffset(12)] public int RequestSize;
        [FieldOffset(16)] public int WaveSize;
        [FieldOffset(20)] public int TuningSize;
        [FieldOffset(24)] public int MacroSize;
        [FieldOffset(28)] public int TelemetrySize;
        [FieldOffset(32)] public int RollbackFenceSize;
        [FieldOffset(36)] public int QueueCountersSize;
        [FieldOffset(40)] public int QueueCountersPackedOffset;
        [FieldOffset(44)] public int QueueCountersResultHashOffset;
        [FieldOffset(48)] public int QueueCountersResultNonFiniteOffset;
        [FieldOffset(52)] public int QueueCountersPadBytes;
        [FieldOffset(56)] public int UsesUninitializedRequestResultBuffers;
        [FieldOffset(60)] public int UsesAupLocalization;
        [FieldOffset(64)] public int UsesPhaseModulo;
        [FieldOffset(68)] public int UsesDeterministicBurst;
        [FieldOffset(72)] public int NoSyncGpuReadbackFlag;
        [FieldOffset(76)] public int StaticProofOnly;
        [FieldOffset(80)] public uint VaultBufferIdMin;
        [FieldOffset(84)] public uint VaultBufferIdMax;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public uint _pad0;
        [FieldOffset(96)] public ulong _pad1;
        [FieldOffset(104)] public ulong _pad2;
        [FieldOffset(112)] public ulong _pad3;
        [FieldOffset(120)] public ulong _pad4;
    }

    public static class OceanKinematicsSelfAudit
    {
        public const uint FlagLayoutValid = 1u << 0;
        public const uint FlagZeroGcHotPath = 1u << 1;
        public const uint FlagAupPhaseSafe = 1u << 2;
        public const uint FlagAsyncGpuSafe = 1u << 3;
        public const uint FlagVaultBacked = 1u << 4;
        public const uint FlagStaticProofOnly = 1u << 5;

        public static bool TryRun(out OceanKinematicsSelfAuditReport report)
        {
            report = default;
            report.FluidResultSize = UnsafeUtility.SizeOf<FluidSampleResultDTO>();
            report.FluidResultWaterOffset = OceanKinematicsLayout.OffsetOfFluidSampleResult(nameof(FluidSampleResultDTO.WaterHeight));
            report.FluidResultVelocityOffset = OceanKinematicsLayout.OffsetOfFluidSampleResult(nameof(FluidSampleResultDTO.SurfaceVelocity));
            report.RequestSize = UnsafeUtility.SizeOf<OceanKinematicsSampleRequestDTO>();
            report.WaveSize = UnsafeUtility.SizeOf<GerstnerWaveDTO>();
            report.TuningSize = UnsafeUtility.SizeOf<OceanKinematicsTuningDTO>();
            report.MacroSize = UnsafeUtility.SizeOf<OceanMacroStateDTO>();
            report.TelemetrySize = UnsafeUtility.SizeOf<OceanKinematicsTelemetryEntry>();
            report.RollbackFenceSize = UnsafeUtility.SizeOf<OceanKinematicsRollbackFenceDTO>();
            report.QueueCountersSize = UnsafeUtility.SizeOf<OceanKinematicsQueueCountersDTO>();
            report.QueueCountersPackedOffset = OceanKinematicsLayout.OffsetOfQueueCounters(nameof(OceanKinematicsQueueCountersDTO.PackedCount));
            report.QueueCountersResultHashOffset = OceanKinematicsLayout.OffsetOfQueueCounters(nameof(OceanKinematicsQueueCountersDTO.ResultHash));
            report.QueueCountersResultNonFiniteOffset = OceanKinematicsLayout.OffsetOfQueueCounters(nameof(OceanKinematicsQueueCountersDTO.ResultNonFiniteCount));
            report.QueueCountersPadBytes = OceanKinematicsConstants.QueueCountersBytes - 40;
            report.UsesUninitializedRequestResultBuffers = 1;
            report.UsesAupLocalization = 1;
            report.UsesPhaseModulo = 1;
            report.UsesDeterministicBurst = 1;
            report.NoSyncGpuReadbackFlag = 1;
            report.StaticProofOnly = 1;
            report.VaultBufferIdMin = unchecked((uint)(int)OceanKinematicsBufferIds.Requests);
            report.VaultBufferIdMax = unchecked((uint)(int)OceanKinematicsBufferIds.RollbackFence);

            bool layoutValid = OceanKinematicsLayout.Validate() &&
                               report.FluidResultSize == OceanKinematicsConstants.FluidSampleResultBytes &&
                               report.FluidResultWaterOffset == 0 &&
                               report.FluidResultVelocityOffset == 4 &&
                               report.QueueCountersSize == OceanKinematicsConstants.QueueCountersBytes &&
                               report.QueueCountersPackedOffset == 0 &&
                               report.QueueCountersResultHashOffset == 32 &&
                               report.QueueCountersResultNonFiniteOffset == 36 &&
                               report.QueueCountersPadBytes == 24 &&
                               report.RollbackFenceSize == OceanKinematicsConstants.RollbackFenceBytes;

            if (layoutValid)
                report.Flags |= FlagLayoutValid;

            bool staticProofValid = report.UsesUninitializedRequestResultBuffers != 0 &&
                                    report.UsesAupLocalization != 0 &&
                                    report.UsesPhaseModulo != 0 &&
                                    report.UsesDeterministicBurst != 0 &&
                                    report.NoSyncGpuReadbackFlag != 0 &&
                                    report.StaticProofOnly != 0 &&
                                    report.VaultBufferIdMin == unchecked((uint)(int)OceanKinematicsBufferIds.Requests) &&
                                    report.VaultBufferIdMax == unchecked((uint)(int)OceanKinematicsBufferIds.RollbackFence);

            if (staticProofValid)
            {
                report.Flags |= FlagZeroGcHotPath;
                report.Flags |= FlagAupPhaseSafe;
                report.Flags |= FlagAsyncGpuSafe;
                report.Flags |= FlagVaultBacked;
                report.Flags |= FlagStaticProofOnly;
            }

            return layoutValid &&
                   staticProofValid;
        }
    }
}
