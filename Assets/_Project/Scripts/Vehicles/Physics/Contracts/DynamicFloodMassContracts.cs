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

    /// <summary>One compartment flood sample. Size: 64 bytes, one L1 cache line.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct DynamicFloodRoomMassSample
    {
        public float3 LocalAup;
        public float WaterLevel01;
        public float VolumeM3;
        private int _pad0;
        private long _pad1;
        private long _pad2;
        private long _pad3;
        private long _pad4;
        private long _pad5;
    }

    /// <summary>Flood mass solve output. Size: 128 bytes, two L1 cache lines.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct DynamicFloodMassSolveResult
    {
        public double3 GlobalPivotAnchor;
        public float3 DynamicCenterOfMassLocal;
        public float3 DynamicCenterOfMassOffsetLocal;
        public float3 InertiaTensorMultiplier;
        public float TotalWaterMassKg;
        public float AngularDragMultiplier;
        public uint Flags;
        private uint _pad0;
        private long _pad1;
        private long _pad2;
        private long _pad3;
        private long _pad4;
        private long _pad5;
        private long _pad6;
    }
}
