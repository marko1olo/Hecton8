using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Physics
{
    public static class CableGpuContractLayout
    {
        public const int GpuSplinePointStrideBytes = 16;
        public const int GpuDrawParamsStrideBytes = 80;
    }

    [StructLayout(LayoutKind.Explicit, Size = CableGpuContractLayout.GpuSplinePointStrideBytes)]
    public struct GpuCableSplinePointDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Tension01;
    }

    [StructLayout(LayoutKind.Explicit, Size = CableGpuContractLayout.GpuDrawParamsStrideBytes)]
    public struct GpuCableDrawParamsDTO
    {
        [FieldOffset(0)] public float4 Color;
        [FieldOffset(16)] public float4 StressColor;
        [FieldOffset(32)] public float4 Params0;
        [FieldOffset(48)] public float4 Params1;
        [FieldOffset(64)] public float4 Params2;
    }
}
