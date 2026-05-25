// ============================================================================
// HECTON-8 - AirlockPressurizationVault.cs
// SHINOBU_338 cold DataVault buffer staging helpers.
// ============================================================================

using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Gameplay.AirlockPressurization
{
    public static partial class AirlockPressurizationVault
    {
        public const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;

        public static bool Bootstrap(IDataVault vault, int requestedCapacity)
        {
            if (vault == null)
                return false;

            int capacity = math.clamp(requestedCapacity, 1, AirlockPressurizationConstants.MaxActiveAirlocks);
            vault.EnsureGenerationHandle<AirlockStateDTO>(
                AirlockPressurizationBufferIds.AirlockStates,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockTuningDTO>(
                AirlockPressurizationBufferIds.Tuning,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockDoorPoseDTO>(
                AirlockPressurizationBufferIds.DoorPoses,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockExchangeIndexDTO>(
                AirlockPressurizationBufferIds.ExchangeIndices,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockEvaluationResultDTO>(
                AirlockPressurizationBufferIds.EvaluationResults,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<BulkheadContainmentIntentDTO>(
                AirlockPressurizationBufferIds.BulkheadIntents,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<Hecton8.Core.Contracts.Signals.BubbleSpawnSignal>(
                AirlockPressurizationBufferIds.VfxSignals,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<Hecton8.Core.Contracts.Signals.MovementAcousticSignal>(
                AirlockPressurizationBufferIds.AcousticSignals,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockTelemetryEntry>(
                AirlockPressurizationBufferIds.TelemetryRing,
                AirlockPressurizationConstants.TelemetryFrameCount,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<int>(
                AirlockPressurizationBufferIds.TelemetryCursor,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<AirlockHardwareProfileDTO>(
                AirlockPressurizationBufferIds.HardwareProfiles,
                AirlockPressurizationConstants.MaxHardwareProfiles,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockDebugGizmoDTO>(
                AirlockPressurizationBufferIds.DebugGizmos,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<int>(
                AirlockPressurizationBufferIds.DumpRequested,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return true;
        }

        public static bool TryReadAirlocks(IDataVault vault, out NativeArray<AirlockStateDTO> airlocks)
        {
            airlocks = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<AirlockStateDTO>(
                       AirlockPressurizationBufferIds.AirlockStates,
                       out VaultGenerationHandle<AirlockStateDTO> handle) &&
                   vault.TryReadHandle(in handle, out airlocks) &&
                   airlocks.IsCreated;
        }

        public static bool TryReadTelemetry(IDataVault vault, out NativeArray<AirlockTelemetryEntry> telemetry)
        {
            telemetry = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<AirlockTelemetryEntry>(
                       AirlockPressurizationBufferIds.TelemetryRing,
                       out VaultGenerationHandle<AirlockTelemetryEntry> handle) &&
                   vault.TryReadHandle(in handle, out telemetry) &&
                   telemetry.IsCreated;
        }

        public static bool TryReadTuning(IDataVault vault, out NativeArray<AirlockTuningDTO> tuning)
        {
            tuning = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<AirlockTuningDTO>(
                       AirlockPressurizationBufferIds.Tuning,
                       out VaultGenerationHandle<AirlockTuningDTO> handle) &&
                   vault.TryReadHandle(in handle, out tuning) &&
                   tuning.IsCreated;
        }

        public static bool TryReadDebugGizmos(IDataVault vault, out NativeArray<AirlockDebugGizmoDTO> gizmos)
        {
            gizmos = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<AirlockDebugGizmoDTO>(
                       AirlockPressurizationBufferIds.DebugGizmos,
                       out VaultGenerationHandle<AirlockDebugGizmoDTO> handle) &&
                   vault.TryReadHandle(in handle, out gizmos) &&
                   gizmos.IsCreated;
        }
    }
}
