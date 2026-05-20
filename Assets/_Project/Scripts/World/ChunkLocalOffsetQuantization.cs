using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hecton8.World
{
    /// <summary>
    /// Burst quantization helpers for compressing chunk-local float3 offsets into 6-byte millimeter payloads.
    /// </summary>
    internal static class ChunkLocalOffsetQuantization
    {
        [StructLayout(LayoutKind.Sequential, Size = 6)]
        internal struct Short3
        {
            public short X;
            public short Y;
            public short Z;

            public Short3(short x, short y, short z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        internal struct QuantizationParams
        {
            public float3 ChunkCenterLocal;
            public float3 EncodeScale;
            public float3 DecodeStep;
        }

        [StructLayout(LayoutKind.Sequential, Size = 6)]
        internal struct QuantizedLocalOffset
        {
            public Short3 Packed;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct QuantizeChunkLocalOffsetsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> SourceOffsets;
            [WriteOnly] public NativeArray<QuantizedLocalOffset> QuantizedOffsets;
            public QuantizationParams Parameters;

            public void Execute(int index)
            {
                float3 relative = SourceOffsets[index] - Parameters.ChunkCenterLocal;
                float3 quantized = math.clamp(math.round(relative * Parameters.EncodeScale), short.MinValue, short.MaxValue);
                QuantizedOffsets[index] = new QuantizedLocalOffset
                {
                    Packed = new Short3((short)quantized.x, (short)quantized.y, (short)quantized.z)
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct DequantizeChunkLocalOffsetsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<QuantizedLocalOffset> QuantizedOffsets;
            [WriteOnly] public NativeArray<float3> DecodedOffsets;
            public QuantizationParams Parameters;

            public void Execute(int index)
            {
                Short3 packed = QuantizedOffsets[index].Packed;
                float3 decoded = new float3(packed.X, packed.Y, packed.Z) * Parameters.DecodeStep;
                DecodedOffsets[index] = Parameters.ChunkCenterLocal + decoded;
            }
        }

        private const float MillimetersPerMeter = 1000f;
        private const float MetersPerMillimeter = 0.001f;

        private static readonly ProfilerMarker _quantizeScheduleProfilerMarker = new ProfilerMarker("H8.World.ChunkOffset.Quantize.Schedule");
        private static readonly ProfilerMarker _dequantizeScheduleProfilerMarker = new ProfilerMarker("H8.World.ChunkOffset.Dequantize.Schedule");

        public static QuantizationParams BuildParams(float3 chunkCenterLocal, float3 maxAbsOffsetFromCenter)
        {
            _ = maxAbsOffsetFromCenter;
            return new QuantizationParams
            {
                ChunkCenterLocal = chunkCenterLocal,
                EncodeScale = new float3(MillimetersPerMeter),
                DecodeStep = new float3(MetersPerMillimeter)
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
