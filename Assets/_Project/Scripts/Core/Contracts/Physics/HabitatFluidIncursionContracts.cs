using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Physics
{
    public static class HabitatFluidIncursionConstants
    {
        public const int MaxCompartments = 5000;
        public const int MaxEdges = 20000;
        public const int TelemetryFrameCount = 300;
        public const int MinSolverIterations = 1;
        public const int MaxSolverIterations = 5;
        public const int MinBfsNodesPerTick = 16;
        public const int MaxBfsNodesPerTick = 128;
        public const float CubicMetersPerMilliliter = 0.000001f;
        public const float MillilitersPerCubicMeter = 1000000f;
        public const float WaterEpsilonM3 = 0.0001f;
        public const float DefaultCompartmentVolumeM3 = 64f;
        public const float DefaultFloorHeightLocal = -1.35f;
        public const float DefaultIngressRateM3PerSecond = 0.65f;
        public const float DefaultTransferRate01PerSecond = 0.42f;
        public const float DefaultMaxTransferPerNodeM3 = 1.25f;
        public const float DefaultMassPublishIntervalSeconds = 0.1f;
        public const float DefaultLowPassCutoffHz = 740f;
        public const float SeawaterDensityKgPerM3 = Hecton8.Core.Contracts.HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        public const float GravityMetersPerSecondSq = Hecton8.Core.Contracts.HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
        public const double AupCellSizeMeters = Hecton8.Core.Contracts.HectonPhysicsContract.AupSectorSizeMetersDouble;
    }

    public static class FluidCompartmentFlags
    {
        public const uint Breached = 1u << 0;
        public const uint Flooded = 1u << 1;
        public const uint Isolated = 1u << 2;
        public const uint OverflowClamped = 1u << 3;
        public const uint MockBreach = 1u << 4;
        public const uint SignalOverflow = 1u << 29;
        public const uint NonFinite = 1u << 31;
    }

    public static class FluidEdgeFlags
    {
        public const byte Sealed = 1 << 0;
        public const byte Ruptured = 1 << 1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidCompartmentDTO
    {
        [FieldOffset(0)] public double3 LocalCenterOfMass;
        [FieldOffset(24)] public uint NodeHashID;
        [FieldOffset(28)] public float CurrentWaterVolume;
        [FieldOffset(32)] public float MaxWaterVolume;
        [FieldOffset(36)] public float WaterLevelHeight01;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private uint _pad1;
        [FieldOffset(52)] private uint _pad2;
        [FieldOffset(56)] private uint _pad3;
        [FieldOffset(60)] private uint _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct IntegrityStateDTO
    {
        public const uint FlagBreached = 1u << 0;
        public const uint FlagMockSource = 1u << 1;
        public const uint FlagSealed = 1u << 2;

        [FieldOffset(0)] public AbsoluteUniversePositionBlit CenterAup;
        [FieldOffset(48)] public uint NodeHash;
        [FieldOffset(52)] public float Integrity01;
        [FieldOffset(56)] public float BreachAreaM2;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidIncursionTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float TransferRate01PerSecond;
        [FieldOffset(8)] public float MaxTransferPerNodeM3;
        [FieldOffset(12)] public float DischargeCoefficient;
        [FieldOffset(16)] public float MaxIngressPerSecondNormalized;
        [FieldOffset(20)] public float MassPublishIntervalSeconds;
        [FieldOffset(24)] public float BaseMassKg;
        [FieldOffset(28)] public float WaterDensityKgPerM3;
        [FieldOffset(32)] public float VisualWobbleScalar;
        [FieldOffset(36)] public float AcousticMuffleGain;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public ushort SolverIterations;
        [FieldOffset(50)] public ushort CompartmentCount;
        [FieldOffset(52)] public ushort EdgeCount;
        [FieldOffset(54)] public byte Flags;
        [FieldOffset(55)] public byte Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct FluidWaterlineShaderDTO
    {
        [FieldOffset(0)] public float Fill01;
        [FieldOffset(4)] public float WaterlineLocalY;
        [FieldOffset(8)] public float Wobble01;
        [FieldOffset(12)] public uint NodeHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidMassStateDTO
    {
        [FieldOffset(0)] public float3 DynamicCenterOfMassLocal;
        [FieldOffset(12)] public float3 DynamicCenterOfMassOffsetLocal;
        [FieldOffset(24)] public float TotalWaterMassKg;
        [FieldOffset(28)] public float BaseMassKg;
        [FieldOffset(32)] public float FillRatio01;
        [FieldOffset(36)] public float AngularDragMultiplier;
        [FieldOffset(40)] public uint SourceBodyId;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public ushort CompartmentCount;
        [FieldOffset(50)] public byte MathLod;
        [FieldOffset(51)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoreFluidIncursionTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public float TotalWaterM3;
        [FieldOffset(12)] public float TotalWaterMassKg;
        [FieldOffset(16)] public float MaxFill01;
        [FieldOffset(20)] public float AverageFill01;
        [FieldOffset(24)] public float PeakIngressRate;
        [FieldOffset(28)] public ushort CompartmentCount;
        [FieldOffset(30)] public ushort FloodedCount;
        [FieldOffset(32)] public ushort BreachedCount;
        [FieldOffset(34)] public ushort EdgeCount;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float3 CenterOfMassLocal;
        [FieldOffset(52)] public uint InvalidCount;
        [FieldOffset(56)] public uint SolverWallMicroseconds;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoreFluidCompartmentTelemetryDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float CurrentWaterM3;
        [FieldOffset(8)] public float MaxVolumeM3;
        [FieldOffset(12)] public float Fill01;
        [FieldOffset(16)] public float IngressRateM3PerSecond;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public ushort CompartmentIndex;
        [FieldOffset(30)] public ushort Reserved0;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidIncursionFrameSummaryDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public float TotalWaterM3;
        [FieldOffset(12)] public float TotalWaterMassKg;
        [FieldOffset(16)] public float MaxFill01;
        [FieldOffset(20)] public float AverageFill01;
        [FieldOffset(24)] public float PeakIngressRate;
        [FieldOffset(28)] public ushort FloodedCount;
        [FieldOffset(30)] public ushort BreachedCount;
        [FieldOffset(32)] public ushort SealedEdgeCount;
        [FieldOffset(34)] public ushort InvalidCount;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float3 CenterOfMassLocal;
        [FieldOffset(52)] public float AcousticFloodIntensity01;
        [FieldOffset(56)] public byte MathLod;
        [FieldOffset(57)] public byte Reserved0;
        [FieldOffset(58)] public ushort Reserved1;
        [FieldOffset(60)] public uint SolverWallMicroseconds;
    }

    public static unsafe class FluidCompartmentPointerUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref FluidCompartmentDTO ElementRef(FluidCompartmentDTO* basePtr, int index)
        {
            return ref UnsafeUtility.AsRef<FluidCompartmentDTO>(basePtr + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCurrentWaterVolume(FluidCompartmentDTO* basePtr, int index, float value)
        {
            ref FluidCompartmentDTO dto = ref ElementRef(basePtr, index);
            dto.CurrentWaterVolume = value;
            dto.WaterLevelHeight01 = dto.MaxWaterVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                ? math.saturate(value * math.rcp(dto.MaxWaterVolume))
                : 0f;
        }
    }

    public static class FluidCompartmentLayoutValidator
    {
        public static bool ValidateFluidCompartmentLayout()
        {
            if (UnsafeUtility.SizeOf<FluidCompartmentDTO>() != 64)
                return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return
                   OffsetOf(nameof(FluidCompartmentDTO.LocalCenterOfMass)) == 0 &&
                   OffsetOf(nameof(FluidCompartmentDTO.NodeHashID)) == 24 &&
                   OffsetOf(nameof(FluidCompartmentDTO.CurrentWaterVolume)) == 28 &&
                   OffsetOf(nameof(FluidCompartmentDTO.MaxWaterVolume)) == 32 &&
                   OffsetOf(nameof(FluidCompartmentDTO.WaterLevelHeight01)) == 36 &&
                   OffsetOf(nameof(FluidCompartmentDTO.Flags)) == 40;
#else
            return true;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int OffsetOf(string fieldName)
        {
            return UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO).GetField(fieldName));
        }
#endif
    }
}
