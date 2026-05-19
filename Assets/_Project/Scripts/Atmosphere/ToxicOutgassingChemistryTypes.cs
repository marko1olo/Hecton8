using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ToxicityStateDTO
    {
        public float Density;          // 00..03
        public float PreviousDensity;  // 04..07
        public float FlowBias;         // 08..11
        public float SdfDistance;      // 12..15
        public uint ChemicalHash;      // 16..19
        public uint CellHash;          // 20..23
        public uint Frame;             // 24..27
        public uint _pad0;             // 28..31
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ToxicOutgassingGridHeaderDTO
    {
        public double3 GridOriginAUP;       // 00..23
        public float CellSizeMeters;        // 24..27
        public float GlobalQualityWeight;   // 28..31
        public uint ActiveDensityBufferId;  // 32..35
        public uint BackDensityBufferId;    // 36..39
        public uint StateBufferId;          // 40..43
        public uint DensityVersion;         // 44..47
        public ushort Resolution;           // 48..49
        public ushort ActiveSources;        // 50..51
        public ushort ActiveEntities;       // 52..53
        public byte Flags;                  // 54
        public byte _pad0;                  // 55
        public ulong _pad1;                 // 56..63
    }

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct ToxicitySourceDTO
    {
        public double3 AUP;          // 00..23
        public float EmissionRate;   // 24..27
        public float Density;        // 28..31
        public uint ChemicalHash;    // 32..35
        public uint _pad0;           // 36..39
        public ulong _pad1;          // 40..47
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ToxicOutgassingConstants
    {
        public float BaseDiffusionRate;             // 00..03
        public float CurrentAdvectionMultiplier;    // 04..07
        public float AcidCorrosionDamage;           // 08..11
        public float FloraAbsorptionRate;           // 12..15
        public float DensityDecayPerSecond;         // 16..19
        public float SourceRadiusMeters;            // 20..23
        public float ExposureToxemiaMultiplier;     // 24..27
        public float CausticDensityThreshold;       // 28..31
        public float BiolumDensityThreshold;        // 32..35
        public float MaxDensity;                    // 36..39
        public float RadialFallbackRadiusScale;     // 40..43
        public float SdfWallLeakScale;              // 44..47
        public float GlobalQualityWeight;           // 48..51
        public float SimulationTickDelta;           // 52..55
        public uint ChemistryFlags;                 // 56..59
        public uint _pad0;                          // 60..63
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public partial struct MockFlowField
    {
        public float3 Direction;   // 00..11
        public float Speed;        // 12..15
        public float3 Curl;        // 16..27
        public float Turbulence;   // 28..31
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public partial struct MockWorldSampler
    {
        public float SdfDistance;       // 00..03
        public float FloraAbsorption01; // 04..07
        public float3 SdfGradient;      // 08..19
        public uint Flags;              // 20..23
        public uint PurifierKelpHash;   // 24..27
        public uint _pad0;              // 28..31
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicityExposureSignal : ISignal
    {
        [FieldOffset(0)] public double3 AUP;        // 00..23
        [FieldOffset(24)] public float Exposure01;   // 24..27
        [FieldOffset(28)] public float ToxemiaDelta; // 28..31
        [FieldOffset(32)] public uint EntityId;      // 32..35
        [FieldOffset(36)] public uint ChemicalHash;  // 36..39
        [FieldOffset(40)] public uint Frame;         // 40..43
        [FieldOffset(44)] public byte Flags;         // 44
        [FieldOffset(45)] public byte _pad0;         // 45
        [FieldOffset(46)] public ushort _pad1;       // 46..47
        [FieldOffset(48)] public ulong _pad2;        // 48..55
        [FieldOffset(56)] public ulong _pad3;        // 56..63
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicBioluminescenceSignal : ISignal
    {
        [FieldOffset(0)] public double3 AUP;         // 00..23
        [FieldOffset(24)] public float Intensity01;   // 24..27
        [FieldOffset(28)] public float ToxicDensity;  // 28..31
        [FieldOffset(32)] public float3 LocalNormal;  // 32..43
        [FieldOffset(44)] public uint ChemicalHash;   // 44..47
        [FieldOffset(48)] public uint Frame;          // 48..51
        [FieldOffset(52)] public ushort CellIndex;    // 52..53
        [FieldOffset(54)] public byte Flags;          // 54
        [FieldOffset(55)] public byte _pad0;          // 55
        [FieldOffset(56)] public ulong _pad1;         // 56..63
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ToxicityCombatDamageSignal
    {
        public double3 AUP;       // 00..23
        public float Magnitude;   // 24..27
        public uint TargetHash;   // 28..31
        public uint SourceHash;   // 32..35
        public uint DamageType;   // 36..39
        public uint Frame;        // 40..43
        public ushort SourceId;   // 44..45
        public ushort TargetId;   // 46..47
        public byte Channel;      // 48
        public byte Flags;        // 49
        public ushort _pad0;      // 50..51
        public uint _pad1;        // 52..55
        public ulong _pad2;       // 56..63
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ToxicityGridTelemetryEntry
    {
        public double3 GridOriginAUP;       // 00..23
        public float MaxDensity;            // 24..27
        public float TotalPlumeVolume;      // 28..31
        public float GlobalQualityWeight;   // 32..35
        public float DiffusionCompleteMs;   // 36..39
        public uint StateHash;              // 40..43
        public uint Frame;                  // 44..47
        public ushort ActiveResolution;     // 48..49
        public ushort ActiveSources;        // 50..51
        public ushort ActiveEntities;       // 52..53
        public byte Flags;                  // 54
        public byte NanDetected;            // 55
        public ulong _pad0;                 // 56..63
    }
}
