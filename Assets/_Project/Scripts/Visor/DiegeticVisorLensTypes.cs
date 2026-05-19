using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Visor
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct VisorStateDTO
    {
        public float CondensationLevel;
        public float WaterDropletIntensity;
        public float CrackSeverity;
        public float DirtAccumulation;
    }

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    public struct VisorLensTuningDTO
    {
        public float FogRate;
        public float FogBreathGain;
        public float FogColdGain;
        public float ClearingRate;
        public float DropletDrainSeconds;
        public float DropletGravityStrength;
        public float SurfaceWashDrainRate;
        public float CrackPressureThreshold;
        public float CrackGrowthRate;
        public float MaxCrackSeverity;
        public float DirtSiltGain;
        public float WipeStrength;
        public float ReflectionDarknessGain;
        public float AnomalyNoiseGain;
        public float LowRefractionQualityCutoff;
        public float BiolumReflectionGain;
        public float HeartCondensationGain;
        public float CoreTempCondensationGain;
        public float QualityStaticBlendStart;
        public float QualityDynamicBlendEnd;
        public uint Flags;
        public uint Version;
        public float _pad0;
        public float _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
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

    [StructLayout(LayoutKind.Explicit, Size = 48)]
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

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct DiegeticVisorLensGpuGlobalsDTO
    {
        public float4 State;
        public float4 Params0;
        public float4 Params1;
        public float4 Params2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
