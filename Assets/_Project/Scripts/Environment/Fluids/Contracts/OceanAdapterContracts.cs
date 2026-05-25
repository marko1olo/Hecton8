using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Environment.Fluids
{
    internal static class OceanAdapterLayout
    {
        internal const int SampleRequestStrideBytes = 32;
        internal const int SampleResultStrideBytes = 64;
        internal const int TelemetryEntryStrideBytes = 64;
        internal const int PerformanceProfileStrideBytes = 32;
        internal const int GlobalWaterLevelStrideBytes = 16;
    }

    [System.Flags]
    public enum OceanSampleStatus : uint
    {
        None = 0u,
        Valid = 1u << 0,
        DelayedOneToThreeFrames = 1u << 1,
        Mocked = 1u << 2,
        SimplifiedByQualityBudget = 1u << 3,
        NonFiniteInput = 1u << 31
    }

    [StructLayout(LayoutKind.Explicit, Size = OceanAdapterLayout.SampleRequestStrideBytes)]
    public struct OceanSampleRequestDTO
    {
        [FieldOffset(0)] public double3 RequestAUP;
        [FieldOffset(24)] public uint CallerHashID;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OceanAdapterLayout.SampleResultStrideBytes)]
    public struct OceanSampleResultDTO
    {
        [FieldOffset(0)] public double3 SourceAUP;
        [FieldOffset(24)] public float WaterHeight;
        [FieldOffset(28)] public float3 SurfaceVelocity;
        [FieldOffset(40)] public float3 WaveNormal;
        [FieldOffset(52)] public float LatencyMilliseconds;
        [FieldOffset(56)] public uint StatusFlags;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OceanAdapterLayout.TelemetryEntryStrideBytes)]
    public struct OceanAdapterTelemetryEntry
    {
        [FieldOffset(0)] public ulong SimulationFrame;
        [FieldOffset(8)] public double3 AnchorAUP;
        [FieldOffset(32)] public uint RequestsSubmitted;
        [FieldOffset(36)] public uint RequestsProcessed;
        [FieldOffset(40)] public uint RequestsDropped;
        [FieldOffset(44)] public uint MaxLatencyMicroseconds;
        [FieldOffset(48)] public uint TranslationMicroseconds;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OceanAdapterLayout.PerformanceProfileStrideBytes)]
    public struct OceanPerformanceProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public uint MaxConcurrentQueries;
        [FieldOffset(8)] public float ReadbackTimeoutMilliseconds;
        [FieldOffset(12)] public float QualityAggression;
        [FieldOffset(16)] public float MockAmplitudeMin;
        [FieldOffset(20)] public float MockAmplitudeMax;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OceanAdapterLayout.GlobalWaterLevelStrideBytes)]
    public struct OceanGlobalWaterLevelDTO
    {
        [FieldOffset(0)] public float WaterLevel;
        [FieldOffset(4)] public float GlobalQualityWeight;
        [FieldOffset(8)] public uint FrameIndex;
        [FieldOffset(12)] public uint Flags;
    }

    public interface IHectonOceanKinematics
    {
        JobHandle ScheduleWaveHeightRequests(
            NativeArray<OceanSampleRequestDTO> requests,
            NativeArray<OceanSampleResultDTO> results,
            int requestCount,
            double3 activeOriginAUP,
            float globalQualityWeight,
            JobHandle inputDeps);

        bool TryReadGlobalWaterLevel(out float waterLevel);
    }
}
