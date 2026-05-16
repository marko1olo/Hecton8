using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.Vehicles.Physics.Contracts
{
    public static class DynamicFloodMassConstants
    {
        public const float SeawaterDensityKgPerM3 = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        public const float CriticalFloodMassBaseRatio = 0.4f;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct DynamicFloodRoomMassSample
    {
        [FieldOffset(0)] public float WaterLevel01;
        [FieldOffset(4)] public float VolumeM3;
        [FieldOffset(8)] public float3 LocalAup;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]
    public struct DynamicFloodMassSolveResult
    {
        [FieldOffset(0)] public float3 DynamicCenterOfMassLocal;
        [FieldOffset(12)] public float3 DynamicCenterOfMassOffsetLocal;
        [FieldOffset(24)] public float3 InertiaTensorMultiplier;
        [FieldOffset(40)] public double3 GlobalPivotAnchor;
        [FieldOffset(64)] public float TotalWaterMassKg;
        [FieldOffset(68)] public float AngularDragMultiplier;
        [FieldOffset(72)] public uint Flags;
    }
}
