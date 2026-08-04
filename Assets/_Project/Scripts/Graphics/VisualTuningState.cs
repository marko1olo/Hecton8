using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Graphics
{
    /// <summary>
    /// Runtime Data Transfer Object for Data-Driven Visual Tuning.
    /// Strictly ARM64 aligned (size must be multiple of 8, float4 first).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VisualTuningState
    {
        // 16 bytes each
        public float4 OceanScatterBase;
        public float4 OceanScatterShallow;
        public float4 SunColor;

        // 4 bytes each
        public float OceanScatterShallowDepthMax;
        public float PlanetCenterRadius;
        public float SunIntensity;
        public float Exposure;

        // Ensure total size is multiple of 8 for optimal ARM64 copying.
        // 16 * 3 = 48 bytes
        // 4 * 4 = 16 bytes
        // Total = 64 bytes. Modulo 8 == 0. No extra padding needed.
        
        public static VisualTuningState Default()
        {
            return new VisualTuningState
            {
                OceanScatterBase = new float4(0.05f, 0.45f, 0.45f, 1f),
                OceanScatterShallow = new float4(0.15f, 0.75f, 0.7f, 1f),
                SunColor = new float4(1f, 0.95f, 0.9f, 1f),
                OceanScatterShallowDepthMax = 10f,
                PlanetCenterRadius = 15f,
                SunIntensity = 1.2f,
                Exposure = 1.0f
            };
        }
    }
}
