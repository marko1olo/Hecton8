using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Visor
{
    internal static class DiegeticVisorLensLayout
    {
        public const int VisorStateDTOStrideBytes = 16;
        public const int VisorLensTuningDTOStrideBytes = 128;
        public const int MockPhysiologySignalStrideBytes = 32;
        public const int MockVisorEnvironmentSignalStrideBytes = 48;
        public const int DiegeticVisorLensGpuGlobalsDTOStrideBytes = 64;
        public const int VisorLensTelemetryEntryStrideBytes = 64;
        public const int VisorBreachSignalStrideBytes = 32;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.VisorStateDTOStrideBytes)]
    public struct VisorStateDTO
    {
        [FieldOffset(0)] public float CondensationLevel;
        [FieldOffset(4)] public float WaterDropletIntensity;
        [FieldOffset(8)] public float CrackSeverity;
        [FieldOffset(12)] public float DirtAccumulation;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.VisorLensTuningDTOStrideBytes)]
    public struct VisorLensTuningDTO
    {
        [FieldOffset(0)] public float FogRate;
        [FieldOffset(4)] public float FogBreathGain;
        [FieldOffset(8)] public float FogColdGain;
        [FieldOffset(12)] public float ClearingRate;
        [FieldOffset(16)] public float DropletDrainSeconds;
        [FieldOffset(20)] public float DropletGravityStrength;
        [FieldOffset(24)] public float SurfaceWashDrainRate;
        [FieldOffset(28)] public float CrackPressureThreshold;
        [FieldOffset(32)] public float CrackGrowthRate;
        [FieldOffset(36)] public float MaxCrackSeverity;
        [FieldOffset(40)] public float DirtSiltGain;
        [FieldOffset(44)] public float WipeStrength;
        [FieldOffset(48)] public float ReflectionDarknessGain;
        [FieldOffset(52)] public float AnomalyNoiseGain;
        [FieldOffset(56)] public float LowRefractionQualityCutoff;
        [FieldOffset(60)] public float BiolumReflectionGain;
        [FieldOffset(64)] public float HeartCondensationGain;
        [FieldOffset(68)] public float CoreTempCondensationGain;
        [FieldOffset(72)] public float QualityStaticBlendStart;
        [FieldOffset(76)] public float QualityDynamicBlendEnd;
        [FieldOffset(80)] public uint Flags;
        [FieldOffset(84)] public uint Version;
        [FieldOffset(88)] public float _pad0;
        [FieldOffset(92)] public float _pad1;
        [FieldOffset(96)] private ulong _pad2;
        [FieldOffset(104)] private ulong _pad3;
        [FieldOffset(112)] private ulong _pad4;
        [FieldOffset(120)] private ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.MockPhysiologySignalStrideBytes)]
    public partial struct MockPhysiologySignal
    {
        [FieldOffset(0)] public float RespirationRate;
        [FieldOffset(4)] public float HeartRate;
        [FieldOffset(8)] public float CoreTemperatureC;
        [FieldOffset(12)] public float BreathSpike01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float _pad0;
        [FieldOffset(28)] public float _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.MockVisorEnvironmentSignalStrideBytes)]
    public struct MockVisorEnvironmentSignal
    {
        [FieldOffset(0)] public float ExternalWaterTemperatureC;
        [FieldOffset(4)] public float ExternalPressure01;
        [FieldOffset(8)] public float SiltDensity01;
        [FieldOffset(12)] public float Darkness01;
        [FieldOffset(16)] public float SurfaceEmergence01;
        [FieldOffset(20)] public float WipeCommand01;
        [FieldOffset(24)] public float Corruption01;
        [FieldOffset(28)] public float WaterlineBreach01;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float _pad0;
        [FieldOffset(44)] public float _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.DiegeticVisorLensGpuGlobalsDTOStrideBytes)]
    public struct DiegeticVisorLensGpuGlobalsDTO
    {
        [FieldOffset(0)] public float4 State;
        [FieldOffset(16)] public float4 Params0;
        [FieldOffset(32)] public float4 Params1;
        [FieldOffset(48)] public float4 Params2;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.VisorLensTelemetryEntryStrideBytes)]
    public struct VisorLensTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float Condensation01;
        [FieldOffset(12)] public float Droplets01;
        [FieldOffset(16)] public float Crack01;
        [FieldOffset(20)] public float Dirt01;
        [FieldOffset(24)] public float Quality01;
        [FieldOffset(28)] public float RespirationRate;
        [FieldOffset(32)] public float ExternalPressure01;
        [FieldOffset(36)] public float SiltDensity01;
        [FieldOffset(40)] public float HeadAngularSpeed;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public uint GpuStateHash;
        [FieldOffset(52)] public float RefractionScale01;
        [FieldOffset(56)] public uint ShaderUpdateComputeTimeNs;
        [FieldOffset(60)] public float Anomaly01;
    }

    [StructLayout(LayoutKind.Explicit, Size = DiegeticVisorLensLayout.VisorBreachSignalStrideBytes)]
    public partial struct VisorBreachSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float CrackSeverity01;
        [FieldOffset(12)] public float ExternalPressure01;
        [FieldOffset(16)] public float Condensation01;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ushort Sequence;
        [FieldOffset(26)] public ushort Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }
}
