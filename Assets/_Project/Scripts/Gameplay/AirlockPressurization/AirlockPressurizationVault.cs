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

        public static bool TryReadAirlocks(IDataVault vault, out NativeArray<AirlockStateDTO>.ReadOnly airlocks)
        {
            return TryReadOnlyBuffer<AirlockStateDTO>(vault, AirlockPressurizationBufferIds.AirlockStates, out airlocks);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadAirlocks(IDataVault vault, out NativeArray<AirlockStateDTO> airlocks)
        {
            return TryReadLegacyMutableBuffer<AirlockStateDTO>(vault, AirlockPressurizationBufferIds.AirlockStates, out airlocks);
        }

        public static bool TryReadTelemetry(IDataVault vault, out NativeArray<AirlockTelemetryEntry>.ReadOnly telemetry)
        {
            return TryReadOnlyBuffer<AirlockTelemetryEntry>(vault, AirlockPressurizationBufferIds.TelemetryRing, out telemetry);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadTelemetry(IDataVault vault, out NativeArray<AirlockTelemetryEntry> telemetry)
        {
            return TryReadLegacyMutableBuffer<AirlockTelemetryEntry>(vault, AirlockPressurizationBufferIds.TelemetryRing, out telemetry);
        }

        public static bool TryReadTuning(IDataVault vault, out NativeArray<AirlockTuningDTO>.ReadOnly tuning)
        {
            return TryReadOnlyBuffer<AirlockTuningDTO>(vault, AirlockPressurizationBufferIds.Tuning, out tuning);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadTuning(IDataVault vault, out NativeArray<AirlockTuningDTO> tuning)
        {
            return TryReadLegacyMutableBuffer<AirlockTuningDTO>(vault, AirlockPressurizationBufferIds.Tuning, out tuning);
        }

        public static bool TryReadDebugGizmos(IDataVault vault, out NativeArray<AirlockDebugGizmoDTO>.ReadOnly gizmos)
        {
            return TryReadOnlyBuffer<AirlockDebugGizmoDTO>(vault, AirlockPressurizationBufferIds.DebugGizmos, out gizmos);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadDebugGizmos(IDataVault vault, out NativeArray<AirlockDebugGizmoDTO> gizmos)
        {
            return TryReadLegacyMutableBuffer<AirlockDebugGizmoDTO>(vault, AirlockPressurizationBufferIds.DebugGizmos, out gizmos);
        }

        private static bool TryReadOnlyBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryReadLegacyMutableBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }
    }
}
