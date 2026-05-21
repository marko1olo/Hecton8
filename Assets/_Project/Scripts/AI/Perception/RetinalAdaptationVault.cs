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

        public readonly bool IsCreated()
        {
            return
            Exposure.IsCreated &&
            BlindnessState.IsCreated &&
            LastPublishedBlindnessState.IsCreated &&
            LightSources.IsCreated &&
            TelemetryRing.IsCreated;
        }
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

            if (!TryOpenBuffer(
                    vault,
                    BufferID.PredatorRetinalExposure,
                    predatorCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float> exposure) ||
                !TryOpenBuffer(
                    vault,
                    BufferID.PredatorRetinalBlindnessState,
                    predatorCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> blindnessState) ||
                !TryOpenBuffer(
                    vault,
                    BufferID.PredatorRetinalLastPublishedBlindnessState,
                    predatorCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> lastPublishedBlindnessState) ||
                !TryOpenBuffer(
                    vault,
                    BufferID.PredatorRetinalLightSources,
                    safeLightCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<LightSourceData> lightSources) ||
                !TryOpenBuffer(
                    vault,
                    BufferID.PredatorRetinalTelemetryRing,
                    safeTelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<RetinalTelemetryEntry> telemetryRing))
            {
                return false;
            }

            buffers.Exposure = exposure;
            buffers.BlindnessState = blindnessState;
            buffers.LastPublishedBlindnessState = lastPublishedBlindnessState;
            buffers.LightSources = lightSources;
            buffers.TelemetryRing = telemetryRing;
            return buffers.IsCreated();
        }

        private static bool TryOpenBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AICognition,
                options);

            uint expectedBufferId = unchecked((uint)(int)bufferId);
            return
                handle.BufferID == expectedBufferId &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }
    }
}
