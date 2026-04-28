using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hecton8.World
{
    /// <summary>
    /// Burst quantization helpers for compressing chunk-local float3 offsets into 3-byte signed integer payloads.
    /// </summary>
    internal static class ChunkLocalOffsetQuantization
    {
        internal struct SByte3
        {
            public sbyte X;
            public sbyte Y;
            public sbyte Z;

            public SByte3(sbyte x, sbyte y, sbyte z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        internal struct QuantizationParams
        {
            public float3 ChunkCenterLocal;
            public float3 DecodeStep;
            public float3 EncodeScale;
        }

        internal struct QuantizedLocalOffset
        {
            public SByte3 Packed;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        internal struct QuantizeChunkLocalOffsetsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> SourceOffsets;
            [WriteOnly] public NativeArray<QuantizedLocalOffset> QuantizedOffsets;
            public QuantizationParams Parameters;

            public void Execute(int index)
            {
                float3 relative = SourceOffsets[index] - Parameters.ChunkCenterLocal;
                float3 quantized = math.clamp(math.round(relative * Parameters.EncodeScale), -127f, 127f);
                QuantizedOffsets[index] = new QuantizedLocalOffset
                {
                    Packed = new SByte3((sbyte)quantized.x, (sbyte)quantized.y, (sbyte)quantized.z)
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        internal struct DequantizeChunkLocalOffsetsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<QuantizedLocalOffset> QuantizedOffsets;
            [WriteOnly] public NativeArray<float3> DecodedOffsets;
            public QuantizationParams Parameters;

            public void Execute(int index)
            {
                SByte3 packed = QuantizedOffsets[index].Packed;
                float3 decoded = new float3(packed.X, packed.Y, packed.Z) * Parameters.DecodeStep;
                DecodedOffsets[index] = Parameters.ChunkCenterLocal + decoded;
            }
        }

        private const float QuantizationMaxMagnitude = 127f;
        private const float MinimumAxisExtent = 0.003937008f;

        private static readonly ProfilerMarker _quantizeScheduleProfilerMarker = new ProfilerMarker("H8.World.ChunkOffset.Quantize.Schedule");
        private static readonly ProfilerMarker _dequantizeScheduleProfilerMarker = new ProfilerMarker("H8.World.ChunkOffset.Dequantize.Schedule");

        public static QuantizationParams BuildParams(float3 chunkCenterLocal, float3 maxAbsOffsetFromCenter)
        {
            float3 safeExtent = math.max(math.abs(maxAbsOffsetFromCenter), MinimumAxisExtent);
            float3 decodeStep = safeExtent / QuantizationMaxMagnitude;
            return new QuantizationParams
            {
                ChunkCenterLocal = chunkCenterLocal,
                DecodeStep = decodeStep,
                EncodeScale = 1f / decodeStep
            };
        }

        public static JobHandle ScheduleQuantize(
            NativeArray<float3> sourceOffsets,
            NativeArray<QuantizedLocalOffset> quantizedOffsets,
            in QuantizationParams parameters,
            JobHandle dependency = default)
        {
            using (_quantizeScheduleProfilerMarker.Auto())
            {
                return new QuantizeChunkLocalOffsetsJob
                {
                    SourceOffsets = sourceOffsets,
                    QuantizedOffsets = quantizedOffsets,
                    Parameters = parameters
                }.Schedule(sourceOffsets.Length, 64, dependency);
            }
        }

        public static JobHandle ScheduleDequantize(
            NativeArray<QuantizedLocalOffset> quantizedOffsets,
            NativeArray<float3> decodedOffsets,
            in QuantizationParams parameters,
            JobHandle dependency = default)
        {
            using (_dequantizeScheduleProfilerMarker.Auto())
            {
                return new DequantizeChunkLocalOffsetsJob
                {
                    QuantizedOffsets = quantizedOffsets,
                    DecodedOffsets = decodedOffsets,
                    Parameters = parameters
                }.Schedule(quantizedOffsets.Length, 64, dependency);
            }
        }
    }
}
