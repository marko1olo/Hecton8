using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.AI.Perception
{
    internal struct RetinalAdaptationVaultBuffers
    {
        public NativeArray<float> Exposure;
        public NativeArray<byte> BlindnessState;
        public NativeArray<byte> LastPublishedBlindnessState;
        public NativeArray<LightSourceData> LightSources;
        public NativeArray<RetinalTelemetryEntry> TelemetryRing;

        public readonly bool IsCreated =>
            Exposure.IsCreated &&
            BlindnessState.IsCreated &&
            LastPublishedBlindnessState.IsCreated &&
            LightSources.IsCreated &&
            TelemetryRing.IsCreated;
    }

    internal static class RetinalAdaptationVault
    {
        internal static bool TryResolve(
            IDataVault vault,
            int requiredPredatorSlots,
            int lightCapacity,
            int telemetryCapacity,
            out RetinalAdaptationVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int predatorCapacity = math.max(1, requiredPredatorSlots);
            int safeLightCapacity = math.max(1, lightCapacity);
            int safeTelemetryCapacity = math.max(1, telemetryCapacity);

            buffers.Exposure = vault.GetBuffer<float>(
                BufferID.PredatorRetinalExposure,
                predatorCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.BlindnessState = vault.GetBuffer<byte>(
                BufferID.PredatorRetinalBlindnessState,
                predatorCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.LastPublishedBlindnessState = vault.GetBuffer<byte>(
                BufferID.PredatorRetinalLastPublishedBlindnessState,
                predatorCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.LightSources = vault.GetBuffer<LightSourceData>(
                BufferID.PredatorRetinalLightSources,
                safeLightCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            buffers.TelemetryRing = vault.GetBuffer<RetinalTelemetryEntry>(
                BufferID.PredatorRetinalTelemetryRing,
                safeTelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);

            return buffers.IsCreated;
        }
    }
}
