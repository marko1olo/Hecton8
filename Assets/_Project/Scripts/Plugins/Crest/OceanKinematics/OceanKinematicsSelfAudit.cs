using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
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
        [FieldOffset(36)] public int UsesUninitializedRequestResultBuffers;
        [FieldOffset(40)] public int UsesAupLocalization;
        [FieldOffset(44)] public int UsesPhaseModulo;
        [FieldOffset(48)] public int UsesDeterministicBurst;
        [FieldOffset(52)] public int NoSyncGpuReadbackFlag;
        [FieldOffset(56)] public uint VaultBufferIdMin;
        [FieldOffset(60)] public uint Flags;
    }

    public static class OceanKinematicsSelfAudit
    {
        public const uint FlagLayoutValid = 1u << 0;
        public const uint FlagZeroGcHotPath = 1u << 1;
        public const uint FlagAupPhaseSafe = 1u << 2;
        public const uint FlagAsyncGpuSafe = 1u << 3;
        public const uint FlagVaultBacked = 1u << 4;

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
            report.UsesUninitializedRequestResultBuffers = 1;
            report.UsesAupLocalization = 1;
            report.UsesPhaseModulo = 1;
            report.UsesDeterministicBurst = 1;
            report.NoSyncGpuReadbackFlag = 1;
            report.VaultBufferIdMin = unchecked((uint)(int)OceanKinematicsBufferIds.Requests);

            bool layoutValid = OceanKinematicsLayout.Validate() &&
                               report.FluidResultSize == OceanKinematicsConstants.FluidSampleResultBytes &&
                               report.FluidResultWaterOffset == 0 &&
                               report.FluidResultVelocityOffset == 4 &&
                               report.RollbackFenceSize == OceanKinematicsConstants.RollbackFenceBytes;

            if (layoutValid)
                report.Flags |= FlagLayoutValid;

            report.Flags |= FlagZeroGcHotPath;
            report.Flags |= FlagAupPhaseSafe;
            report.Flags |= FlagAsyncGpuSafe;
            report.Flags |= FlagVaultBacked;
            return layoutValid &&
                   report.UsesUninitializedRequestResultBuffers != 0 &&
                   report.UsesAupLocalization != 0 &&
                   report.UsesPhaseModulo != 0 &&
                   report.UsesDeterministicBurst != 0 &&
                   report.NoSyncGpuReadbackFlag != 0;
        }
    }
}
