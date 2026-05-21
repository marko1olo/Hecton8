using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hecton8.World
{
    /// <summary>
    /// Burst quantization helpers for compressing chunk-local float3 offsets into aligned millimeter payloads.
    /// </summary>
    internal static class ChunkLocalOffsetQuantization
    {
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        internal struct Short3
        {
            [FieldOffset(0)]
            public short X;
            [FieldOffset(2)]
            public short Y;
            [FieldOffset(4)]
            public short Z;
            [FieldOffset(6)]
            private ushort _pad0;

            public Short3(short x, short y, short z)
            {
                X = x;
                Y = y;
                Z = z;
                _pad0 = 0;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        internal struct QuantizationParams
        {
            [FieldOffset(0)]
            public float3 ChunkCenterLocal;
            [FieldOffset(16)]
            public float3 EncodeScale;
            [FieldOffset(32)]
            public float3 DecodeStep;
            [FieldOffset(44)]
            private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        internal struct QuantizedLocalOffset
        {
            [FieldOffset(0)]
            public Short3 Packed;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct QuantizeChunkLocalOffsetsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> SourceOffsets;
            [WriteOnly, NoAlias] public NativeArray<QuantizedLocalOffset> QuantizedOffsets;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct DequantizeChunkLocalOffsetsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<QuantizedLocalOffset> QuantizedOffsets;
            [WriteOnly, NoAlias] public NativeArray<float3> DecodedOffsets;
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
