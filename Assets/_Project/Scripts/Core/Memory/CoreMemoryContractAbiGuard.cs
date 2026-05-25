using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core.Memory
{
    internal static unsafe class CoreMemoryContractAbiGuard
    {
        internal static bool Validate()
        {
            return
                ValidateMemorySentinelSizes() &&
                ValidateMemorySentinelOffsets() &&
                ValidateVaultContractSizes() &&
                ValidateVaultContractOffsets() &&
                ValidateAlignmentTelemetryOffsets()
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                && ValidateEditorTestSignalOffsets()
#endif
                ;
        }

        private static bool ValidateMemorySentinelSizes()
        {
            return
                UnsafeUtility.SizeOf<ValidationStateDTO>() == MemorySentinelConstants.ValidationStateSizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelTargetDTO>() == MemorySentinelConstants.TargetSizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelResultDTO>() == MemorySentinelConstants.ResultSizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelRuntimeStateDTO>() == MemorySentinelConstants.RuntimeStateSizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelTelemetryEntry>() == MemorySentinelConstants.TelemetryEntrySizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelAupSnapshotDTO>() == MemorySentinelConstants.AupSnapshotSizeBytes &&
                UnsafeUtility.SizeOf<MockInventorySpan>() == MemorySentinelConstants.MockInventorySpanSizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelModQuarantineSpan>() == MemorySentinelConstants.ModQuarantineSpanSizeBytes &&
                UnsafeUtility.SizeOf<MemorySentinelTunerSnapshotDTO>() == 64;
        }

        private static bool ValidateMemorySentinelOffsets()
        {
            ValidationStateDTO validation = default;
            MemorySentinelTargetDTO target = default;
            MemorySentinelResultDTO result = default;
            MemorySentinelRuntimeStateDTO runtime = default;
            MemorySentinelTelemetryEntry telemetry = default;
            MemorySentinelAupSnapshotDTO aup = default;
            MockInventorySpan mock = default;
            MemorySentinelModQuarantineSpan quarantine = default;
            MemorySentinelTunerSnapshotDTO tuner = default;

            byte* validationBase = (byte*)&validation;
            byte* targetBase = (byte*)&target;
            byte* resultBase = (byte*)&result;
            byte* runtimeBase = (byte*)&runtime;
            byte* telemetryBase = (byte*)&telemetry;
            byte* aupBase = (byte*)&aup;
            byte* mockBase = (byte*)&mock;
            byte* quarantineBase = (byte*)&quarantine;
            byte* tunerBase = (byte*)&tuner;

            return
                ByteOffset(validationBase, &validation.TargetMemoryPointer) == 0 &&
                ByteOffset(validationBase, &validation.ExpectedHash) == 8 &&
                ByteOffset(validationBase, &validation.StoredHash) == 12 &&
                ByteOffset(validationBase, &validation.CheckInterval) == 16 &&
                ByteOffset(targetBase, &target.TargetMemoryPointer) == 0 &&
                ByteOffset(targetBase, &target.ByteLength) == 8 &&
                ByteOffset(targetBase, &target.RollbackByteOffset) == 12 &&
                ByteOffset(targetBase, &target.TargetHash) == 16 &&
                ByteOffset(targetBase, &target.Flags) == 20 &&
                ByteOffset(targetBase, &target.CheckInterval) == 24 &&
                ByteOffset(targetBase, &target.LastLegalFrame) == 28 &&
                ByteOffset(targetBase, &target.MinQualityWeight) == 32 &&
                ByteOffset(targetBase, &target.Criticality01) == 36 &&
                ByteOffset(targetBase, &target.ModdedGameMask) == 40 &&
                ByteOffset(targetBase, &target.BufferId) == 44 &&
                ByteOffset(targetBase, &target.TargetMemoryFingerprint) == 48 &&
                ByteOffset(resultBase, &result.TargetHash) == 0 &&
                ByteOffset(resultBase, &result.CalculatedHash) == 4 &&
                ByteOffset(resultBase, &result.ExpectedHash) == 8 &&
                ByteOffset(resultBase, &result.StoredHash) == 12 &&
                ByteOffset(resultBase, &result.Flags) == 16 &&
                ByteOffset(resultBase, &result.Frame) == 20 &&
                ByteOffset(resultBase, &result.ByteLength) == 24 &&
                ByteOffset(resultBase, &result.RollbackByteOffset) == 28 &&
                ByteOffset(resultBase, &result.FullHash64) == 32 &&
                ByteOffset(resultBase, &result.GlobalQualityWeight) == 40 &&
                ByteOffset(resultBase, &result.ValidationCostMicrosecondsEstimate) == 44 &&
                ByteOffset(resultBase, &result.CheckInterval) == 48 &&
                ByteOffset(resultBase, &result.LastLegalFrame) == 52 &&
                ByteOffset(runtimeBase, &runtime.ValidationFrequencyHz) == 0 &&
                ByteOffset(runtimeBase, &runtime.AupTeleportToleranceMeters) == 4 &&
                ByteOffset(runtimeBase, &runtime.Strictness01) == 8 &&
                ByteOffset(runtimeBase, &runtime.GlobalQualityWeightOverride) == 12 &&
                ByteOffset(runtimeBase, &runtime.GlobalQualityWeight) == 16 &&
                ByteOffset(runtimeBase, &runtime.LastValidationMs) == 20 &&
                ByteOffset(runtimeBase, &runtime.LastValidationFrame) == 24 &&
                ByteOffset(runtimeBase, &runtime.Flags) == 28 &&
                ByteOffset(runtimeBase, &runtime.TargetCount) == 32 &&
                ByteOffset(runtimeBase, &runtime.LastCorrectedCount) == 36 &&
                ByteOffset(runtimeBase, &runtime.LastFatalCount) == 40 &&
                ByteOffset(runtimeBase, &runtime.ValidationCadenceFrames) == 44 &&
                ByteOffset(runtimeBase, &runtime.ModdedGameMask) == 48 &&
                ByteOffset(telemetryBase, &telemetry.Frame) == 0 &&
                ByteOffset(telemetryBase, &telemetry.BytesHashedPerFrame) == 4 &&
                ByteOffset(telemetryBase, &telemetry.DesyncsCorrected) == 8 &&
                ByteOffset(telemetryBase, &telemetry.DesyncsDetected) == 12 &&
                ByteOffset(telemetryBase, &telemetry.ValidationComputeTimeMs) == 16 &&
                ByteOffset(telemetryBase, &telemetry.GlobalQualityWeight) == 20 &&
                ByteOffset(telemetryBase, &telemetry.Flags) == 24 &&
                ByteOffset(telemetryBase, &telemetry.TargetCount) == 28 &&
                ByteOffset(telemetryBase, &telemetry.FatalCount) == 32 &&
                ByteOffset(telemetryBase, &telemetry.RollbackBytes) == 36 &&
                ByteOffset(telemetryBase, &telemetry.LastTargetHash) == 40 &&
                ByteOffset(telemetryBase, &telemetry.LastExpectedHash) == 44 &&
                ByteOffset(telemetryBase, &telemetry.LastCalculatedHash) == 48 &&
                ByteOffset(telemetryBase, &telemetry.ValidationCadenceFrames) == 52 &&
                ByteOffset(aupBase, &aup.GlobalPosition) == 0 &&
                ByteOffset(aupBase, &aup.Frame) == 24 &&
                ByteOffset(aupBase, &aup.Flags) == 28 &&
                ByteOffset(aupBase, &aup.MaxMetersPerSecond) == 32 &&
                ByteOffset(aupBase, &aup.LastDeltaMeters) == 36 &&
                ByteOffset(aupBase, &aup.LastRequiredSpeedMetersPerSecond) == 40 &&
                ByteOffset(mockBase, &mock.Word0) == 0 &&
                ByteOffset(mockBase, &mock.Word7) == 56 &&
                ByteOffset(quarantineBase, &quarantine.Prefix) == 0 &&
                ByteOffset(quarantineBase, &quarantine.ModHash) == 4 &&
                ByteOffset(quarantineBase, &quarantine.MutationCounter) == 8 &&
                ByteOffset(quarantineBase, &quarantine.Flags) == 12 &&
                ByteOffset(quarantineBase, &quarantine.Payload0) == 16 &&
                ByteOffset(quarantineBase, &quarantine.Payload5) == 56 &&
                ByteOffset(tunerBase, &tuner.ValidationFrequencyHz) == 0 &&
                ByteOffset(tunerBase, &tuner.AupTeleportToleranceMeters) == 4 &&
                ByteOffset(tunerBase, &tuner.Strictness01) == 8 &&
                ByteOffset(tunerBase, &tuner.GlobalQualityWeight) == 12 &&
                ByteOffset(tunerBase, &tuner.LastValidationMs) == 16 &&
                ByteOffset(tunerBase, &tuner.LastValidationFrame) == 20 &&
                ByteOffset(tunerBase, &tuner.TargetCount) == 24 &&
                ByteOffset(tunerBase, &tuner.LastCorrectedCount) == 28 &&
                ByteOffset(tunerBase, &tuner.LastFatalCount) == 32 &&
                ByteOffset(tunerBase, &tuner.LastBytesHashed) == 36 &&
                ByteOffset(tunerBase, &tuner.ModdedGameMask) == 40 &&
                ByteOffset(tunerBase, &tuner.Flags) == 44;
        }

        private static bool ValidateVaultContractSizes()
        {
            return
                UnsafeUtility.SizeOf<VaultMemoryLayoutConfig>() == VaultBufferContract.LayoutConfigSizeBytes &&
                UnsafeUtility.SizeOf<VaultAup64>() == VaultBufferContract.Aup64SizeBytes &&
                UnsafeUtility.SizeOf<VaultAupSectorLocal32>() == VaultBufferContract.AupSectorLocal32SizeBytes &&
                UnsafeUtility.SizeOf<VaultHotEntityData>() == VaultBufferContract.HotEntitySizeBytes &&
                UnsafeUtility.SizeOf<VaultColdEntityData>() == VaultBufferContract.ColdEntitySizeBytes &&
                UnsafeUtility.SizeOf<VaultTransformAlias>() == VaultBufferContract.TransformAliasSizeBytes &&
                UnsafeUtility.SizeOf<VaultSovereigntyTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<VaultMemoryAddressShiftRecord>() == VaultBufferContract.AddressShiftRecordSizeBytes &&
                UnsafeUtility.SizeOf<VaultBufferContract>() == 64 &&
                UnsafeUtility.SizeOf<VaultSovereigntyMaintenanceStats>() == 32;
        }

        private static bool ValidateVaultContractOffsets()
        {
            VaultMemoryLayoutConfig layout = default;
            VaultAup64 aup64 = default;
            VaultAupSectorLocal32 aup32 = default;
            VaultHotEntityData hot = default;
            VaultColdEntityData cold = default;
            VaultTransformAlias alias = default;
            VaultSovereigntyTelemetryEntry telemetry = default;
            VaultMemoryAddressShiftRecord shift = default;
            VaultSovereigntyMaintenanceStats stats = default;

            byte* layoutBase = (byte*)&layout;
            byte* aup64Base = (byte*)&aup64;
            byte* aup32Base = (byte*)&aup32;
            byte* hotBase = (byte*)&hot;
            byte* coldBase = (byte*)&cold;
            byte* aliasBase = (byte*)&alias;
            byte* telemetryBase = (byte*)&telemetry;
            byte* shiftBase = (byte*)&shift;
            byte* statsBase = (byte*)&stats;

            return
                ByteOffset(layoutBase, &layout.ArenaLimitBytes) == VaultBufferContract.LayoutConfigArenaLimitOffset &&
                ByteOffset(layoutBase, &layout.BufferCapacity) == VaultBufferContract.LayoutConfigBufferCapacityOffset &&
                ByteOffset(layoutBase, &layout.HotEntityCapacity) == VaultBufferContract.LayoutConfigHotEntityCapacityOffset &&
                ByteOffset(layoutBase, &layout.ColdEntityCapacity) == VaultBufferContract.LayoutConfigColdEntityCapacityOffset &&
                ByteOffset(layoutBase, &layout.BucketCapacity) == VaultBufferContract.LayoutConfigBucketCapacityOffset &&
                ByteOffset(layoutBase, &layout.SourceHash) == VaultBufferContract.LayoutConfigSourceHashOffset &&
                ByteOffset(layoutBase, &layout.Version) == VaultBufferContract.LayoutConfigVersionOffset &&
                ByteOffset(layoutBase, &layout.ScalabilityProfile) == VaultBufferContract.LayoutConfigScalabilityProfileOffset &&
                ByteOffset(layoutBase, &layout.Flags) == VaultBufferContract.LayoutConfigFlagsOffset &&
                ByteOffset(layoutBase, &layout.StrideAggressiveness) == VaultBufferContract.LayoutConfigStrideAggressivenessOffset &&
                ByteOffset(aup64Base, &aup64.SectorX) == VaultBufferContract.AupSectorXOffset &&
                ByteOffset(aup64Base, &aup64.SectorY) == VaultBufferContract.AupSectorYOffset &&
                ByteOffset(aup64Base, &aup64.SectorZ) == VaultBufferContract.AupSectorZOffset &&
                ByteOffset(aup64Base, &aup64.LocalX) == VaultBufferContract.AupLocalXOffset &&
                ByteOffset(aup64Base, &aup64.LocalY) == VaultBufferContract.AupLocalYOffset &&
                ByteOffset(aup64Base, &aup64.LocalZ) == VaultBufferContract.AupLocalZOffset &&
                ByteOffset(aup32Base, &aup32.SectorX) == VaultBufferContract.Aup32SectorXOffset &&
                ByteOffset(aup32Base, &aup32.SectorY) == VaultBufferContract.Aup32SectorYOffset &&
                ByteOffset(aup32Base, &aup32.SectorZ) == VaultBufferContract.Aup32SectorZOffset &&
                ByteOffset(aup32Base, &aup32.LocalOffset) == VaultBufferContract.Aup32LocalOffset &&
                ByteOffset(aup32Base, &aup32.EntityId) == VaultBufferContract.Aup32EntityIdOffset &&
                ByteOffset(aup32Base, &aup32.Flags) == VaultBufferContract.Aup32FlagsOffset &&
                ByteOffset(aup32Base, &aup32.ShiftFrameId) == VaultBufferContract.Aup32ShiftFrameIdOffset &&
                ByteOffset(hotBase, &hot.Rotation) == VaultBufferContract.HotRotationOffset &&
                ByteOffset(hotBase, &hot.LocalPosition) == VaultBufferContract.HotLocalPositionOffset &&
                ByteOffset(hotBase, &hot.Velocity) == VaultBufferContract.HotVelocityOffset &&
                ByteOffset(hotBase, &hot.EntityId) == VaultBufferContract.HotEntityIdOffset &&
                ByteOffset(hotBase, &hot.Flags) == VaultBufferContract.HotFlagsOffset &&
                ByteOffset(hotBase, &hot.ShiftFrameId) == VaultBufferContract.HotShiftFrameIdOffset &&
                ByteOffset(hotBase, &hot.SimulationBucket) == VaultBufferContract.HotSimulationBucketOffset &&
                ByteOffset(hotBase, &hot.LodTier) == VaultBufferContract.HotLodTierOffset &&
                ByteOffset(coldBase, &cold.DisplayNameHash) == VaultBufferContract.ColdDisplayNameHashOffset &&
                ByteOffset(coldBase, &cold.FactionMask) == VaultBufferContract.ColdFactionMaskOffset &&
                ByteOffset(coldBase, &cold.EntityId) == VaultBufferContract.ColdEntityIdOffset &&
                ByteOffset(coldBase, &cold.ArchetypeHash) == VaultBufferContract.ColdArchetypeHashOffset &&
                ByteOffset(coldBase, &cold.PrefabHash) == VaultBufferContract.ColdPrefabHashOffset &&
                ByteOffset(coldBase, &cold.MaxHealth) == VaultBufferContract.ColdMaxHealthOffset &&
                ByteOffset(coldBase, &cold.MaxEnergy) == VaultBufferContract.ColdMaxEnergyOffset &&
                ByteOffset(coldBase, &cold.Flags) == VaultBufferContract.ColdFlagsOffset &&
                ByteOffset(coldBase, &cold.MaterialSet) == VaultBufferContract.ColdMaterialSetOffset &&
                ByteOffset(aliasBase, &alias.MatrixBufferId) == VaultBufferContract.TransformAliasMatrixBufferIdOffset &&
                ByteOffset(aliasBase, &alias.MatrixOffsetBytes) == VaultBufferContract.TransformAliasMatrixOffsetBytesOffset &&
                ByteOffset(aliasBase, &alias.MatrixGeneration) == VaultBufferContract.TransformAliasMatrixGenerationOffset &&
                ByteOffset(aliasBase, &alias.TransformHash) == VaultBufferContract.TransformAliasTransformHashOffset &&
                ByteOffset(aliasBase, &alias.EntityId) == VaultBufferContract.TransformAliasEntityIdOffset &&
                ByteOffset(aliasBase, &alias.Flags) == VaultBufferContract.TransformAliasFlagsOffset &&
                ByteOffset(telemetryBase, &telemetry.TotalVaultBytes) == 0 &&
                ByteOffset(telemetryBase, &telemetry.ArenaBytes) == 8 &&
                ByteOffset(telemetryBase, &telemetry.ActiveBufferCount) == 16 &&
                ByteOffset(telemetryBase, &telemetry.GenerationMisses) == 20 &&
                ByteOffset(telemetryBase, &telemetry.StrideMultiplier) == 24 &&
                ByteOffset(telemetryBase, &telemetry.MaxMemoryJobUs) == 28 &&
                ByteOffset(telemetryBase, &telemetry.Frame) == 32 &&
                ByteOffset(telemetryBase, &telemetry.VaultGenerationId) == 36 &&
                ByteOffset(telemetryBase, &telemetry.BufferId) == 40 &&
                ByteOffset(telemetryBase, &telemetry.StateHash) == 44 &&
                ByteOffset(telemetryBase, &telemetry.GlobalQualityWeight) == 48 &&
                ByteOffset(telemetryBase, &telemetry.Flags) == 52 &&
                ByteOffset(shiftBase, &shift.OldOffsetBytes) == 0 &&
                ByteOffset(shiftBase, &shift.NewOffsetBytes) == 8 &&
                ByteOffset(shiftBase, &shift.BufferId) == 16 &&
                ByteOffset(shiftBase, &shift.ByteLength) == 20 &&
                ByteOffset(shiftBase, &shift.Version) == 24 &&
                ByteOffset(shiftBase, &shift.Flags) == 28 &&
                ByteOffset(shiftBase, &shift.SystemId) == 29 &&
                ByteOffset(shiftBase, &shift.OldIndex) == 32 &&
                ByteOffset(shiftBase, &shift.NewIndex) == 36 &&
                ByteOffset(shiftBase, &shift.MovedEntityId) == 40 &&
                ByteOffset(shiftBase, &shift.SourceFrame) == 44 &&
                ByteOffset(shiftBase, &shift.SourceHash) == 48 &&
                ByteOffset(shiftBase, &shift.CompactedCount) == 52 &&
                ByteOffset(statsBase, &stats.AupRowsVisited) == 0 &&
                ByteOffset(statsBase, &stats.SweepRowsVisited) == 4 &&
                ByteOffset(statsBase, &stats.ActiveCount) == 8 &&
                ByteOffset(statsBase, &stats.ScanBudget) == 12 &&
                ByteOffset(statsBase, &stats.MaxJobUs) == 16 &&
                ByteOffset(statsBase, &stats.Flags) == 20;
        }

        private static bool ValidateAlignmentTelemetryOffsets()
        {
            AlignmentTelemetryEntry entry = default;
            byte* entryBase = (byte*)&entry;
            return
                UnsafeUtility.SizeOf<AlignmentTelemetryEntry>() == 64 &&
                ByteOffset(entryBase, &entry.StructHash) == 0 &&
                ByteOffset(entryBase, &entry.OffendingAddress) == 8 &&
                ByteOffset(entryBase, &entry.AupOrRuntimePosition) == 16 &&
                ByteOffset(entryBase, &entry.BufferID) == 40 &&
                ByteOffset(entryBase, &entry.ByteOffset) == 44 &&
                ByteOffset(entryBase, &entry.Frame) == 48 &&
                ByteOffset(entryBase, &entry.Flags) == 52 &&
                ByteOffset(entryBase, &entry.Severity01) == 56 &&
                ByteOffset(entryBase, &entry.StateHash) == 60;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        private static bool ValidateEditorTestSignalOffsets()
        {
            VaultMemoryAddressShiftSignal signal = default;
            byte* signalBase = (byte*)&signal;
            return
                UnsafeUtility.SizeOf<VaultMemoryAddressShiftSignal>() == 64 &&
                ByteOffset(signalBase, &signal.OldOffsetBytes) == 0 &&
                ByteOffset(signalBase, &signal.NewOffsetBytes) == 8 &&
                ByteOffset(signalBase, &signal.BufferId) == 16 &&
                ByteOffset(signalBase, &signal.ByteLength) == 20 &&
                ByteOffset(signalBase, &signal.Version) == 24 &&
                ByteOffset(signalBase, &signal.Flags) == 28 &&
                ByteOffset(signalBase, &signal.SystemId) == 29 &&
                ByteOffset(signalBase, &signal.OldIndex) == 32 &&
                ByteOffset(signalBase, &signal.NewIndex) == 36 &&
                ByteOffset(signalBase, &signal.MovedEntityId) == 40 &&
                ByteOffset(signalBase, &signal.SourceFrame) == 44 &&
                ByteOffset(signalBase, &signal.SourceHash) == 48 &&
                ByteOffset(signalBase, &signal.CompactedCount) == 52;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ByteOffset(void* basePtr, void* fieldPtr)
        {
            return (int)((byte*)fieldPtr - (byte*)basePtr);
        }
    }
}
