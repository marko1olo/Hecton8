using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Vehicles.Physics.Contracts
{
    public static class DynamicFloodMassConstants
    {
        public const float SeawaterDensityKgPerM3 = 1025f;
        public const float CriticalFloodMassBaseRatio = 0.4f;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DynamicFloodRoomMassSample
    {
        public float WaterLevel01;
        public float VolumeM3;
        public float3 LocalAup;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DynamicFloodMassSolveResult
    {
        public float3 DynamicCenterOfMassLocal;
        public float3 DynamicCenterOfMassOffsetLocal;
        public float TotalWaterMassKg;
        public float AngularDragMultiplier;
        public uint Flags;
    }
}
