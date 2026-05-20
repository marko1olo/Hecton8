using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Habitat.Deformation.Contracts
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct HabitatModuleDeformationSample
    {
        [FieldOffset(0)] public readonly uint NodeId;
        [FieldOffset(4)] public readonly uint ModuleHash;
        [FieldOffset(8)] public readonly float3 RuntimeCenter;
        [FieldOffset(20)] public readonly float Stress01;
        [FieldOffset(24)] public readonly float PeakStress01;
        [FieldOffset(28)] public readonly byte QualityTier;
        [FieldOffset(29)] private readonly byte _padding0;
        [FieldOffset(30)] private readonly byte _padding1;
        [FieldOffset(31)] private readonly byte _padding2;

        public HabitatModuleDeformationSample(
            uint nodeId,
            uint moduleHash,
            float3 runtimeCenter,
            float stress01,
            float peakStress01,
            byte qualityTier)
        {
            NodeId = nodeId;
            ModuleHash = moduleHash;
            RuntimeCenter = runtimeCenter;
            Stress01 = math.saturate(stress01);
            PeakStress01 = math.saturate(peakStress01);
            QualityTier = qualityTier;
            _padding0 = 0;
            _padding1 = 0;
            _padding2 = 0;
        }
    }

    public interface IHabitatModuleDeformationReadModel
    {
        int ModuleStressCount { get; }
        bool TryGetModuleStress(int stressIndex, out HabitatModuleDeformationSample sample);
    }
}

namespace Hecton8.Habitat.Deformation
{
    public static class StructuralIntegrityConstants
    {
        public const int MaxNodeCapacity = 4096;
        public const int MaxEdgeCapacity = MaxNodeCapacity * 4;
        public const int TelemetryFrameCapacity = 300;
        public const int MaterialStrengthCapacity = 32;
        public const int CsvScratchBytes = 16 * 1024;
        public const uint AgentHash = 0x73323138u; // s218
        public const uint DefaultBaseHash = 0x53323138u; // S218
        public const uint SignalLaneHash = 0x53494331u; // SIC1
        public const uint BaseModuleCompromisedSignalLaneHash = 3041159082u; // FNV32("BaseModuleCompromisedSignal")
        public const uint FluidIncursionSignalLaneHash = 2553418623u; // FNV32("FluidIncursionSignal")
        public const uint DumpMagic = 0x53494344u; // SICD
        public const uint DumpVersion = 1u;

        public const uint StateFlagAnchor = 1u << 0;
        public const uint StateFlagCollapsed = 1u << 1;
        public const uint StateFlagLeakEmitted = 1u << 2;
        public const uint StateFlagWarn80Emitted = 1u << 3;
        public const uint StateFlagWarn90Emitted = 1u << 4;
        public const uint StateFlagNonFinite = 1u << 31;

        public const byte EdgeFlagSevered = 1 << 0;

        public const uint TelemetryFlagNonFinite = 1u << 0;
        public const uint TelemetryFlagMassCollapse = 1u << 1;
        public const uint TelemetryFlagSdfFallback = 1u << 2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public unsafe struct IntegrityStateDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float BaseStrength;
        [FieldOffset(8)] public float CurrentStress;
        [FieldOffset(12)] public float AppliedPressure;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float BucklingScalar;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref IntegrityStateDTO AsRef(NativeArray<IntegrityStateDTO> states, int index)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(states);
            return ref UnsafeUtility.AsRef<IntegrityStateDTO>((byte*)basePtr + (index * UnsafeUtility.SizeOf<IntegrityStateDTO>()));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct StructuralTuningDTO
    {
        [FieldOffset(0)] public double3 SeaLevelAup;
        [FieldOffset(24)] public double3 SdfOriginAup;
        [FieldOffset(48)] public float BasePressureKPa;
        [FieldOffset(52)] public float PressureGradientKPaPerMeter;
        [FieldOffset(56)] public float PressureToStressScale;
        [FieldOffset(60)] public float MaterialStrengthFactor;
        [FieldOffset(64)] public float BucklingStart01;
        [FieldOffset(68)] public float BucklingVisualIntensity;
        [FieldOffset(72)] public float SupportDamping;
        [FieldOffset(76)] public float CollapseStress01;
        [FieldOffset(80)] public float GlobalQualityWeight;
        [FieldOffset(84)] public float SdfMetersPerVoxel;
        [FieldOffset(88)] public float SdfRangeMeters;
        [FieldOffset(92)] public int ActiveNodeCount;
    }
}
