using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.Vehicles.Physics.Contracts
{
    internal static class DynamicFloodMassContractLayout
    {
        public const int DynamicFloodRoomMassSampleStrideBytes = 64;
        public const int DynamicFloodMassSolveResultStrideBytes = 128;
    }

    public static class DynamicFloodMassConstants
    {
        public const float SeawaterDensityKgPerM3 = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        public const float CriticalFloodMassBaseRatio = 0.4f;
    }

    /// <summary>One compartment flood sample. Size: 64 bytes, one L1 cache line.</summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicFloodMassContractLayout.DynamicFloodRoomMassSampleStrideBytes)]
    public struct DynamicFloodRoomMassSample
    {
        [FieldOffset(0)] public float3 LocalAup;
        [FieldOffset(12)] public float WaterLevel01;
        [FieldOffset(16)] public float VolumeM3;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
        [FieldOffset(32)] private ulong _pad2;
        [FieldOffset(40)] private ulong _pad3;
        [FieldOffset(48)] private ulong _pad4;
        [FieldOffset(56)] private ulong _pad5;
    }

    /// <summary>Flood mass solve output. Size: 128 bytes, two L1 cache lines.</summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicFloodMassContractLayout.DynamicFloodMassSolveResultStrideBytes)]
    public struct DynamicFloodMassSolveResult
    {
        [FieldOffset(0)] public double3 GlobalPivotAnchor;
        [FieldOffset(24)] public float3 DynamicCenterOfMassLocal;
        [FieldOffset(36)] public float3 DynamicCenterOfMassOffsetLocal;
        [FieldOffset(48)] public float3 InertiaTensorMultiplier;
        [FieldOffset(60)] public float TotalWaterMassKg;
        [FieldOffset(64)] public float AngularDragMultiplier;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] private ulong _pad0;
        [FieldOffset(80)] private ulong _pad1;
        [FieldOffset(88)] private ulong _pad2;
        [FieldOffset(96)] private ulong _pad3;
        [FieldOffset(104)] private ulong _pad4;
        [FieldOffset(112)] private ulong _pad5;
        [FieldOffset(120)] private ulong _pad6;
    }
}
