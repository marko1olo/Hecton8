using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToxicityStateDTO
    {
        [FieldOffset(0)]
        public float Density;          // 00..03
        [FieldOffset(4)]
        public float PreviousDensity;  // 04..07
        [FieldOffset(8)]
        public float FlowBias;         // 08..11
        [FieldOffset(12)]
        public float SdfDistance;      // 12..15
        [FieldOffset(16)]
        public uint ChemicalHash;      // 16..19
        [FieldOffset(20)]
        public uint CellHash;          // 20..23
        [FieldOffset(24)]
        public uint Frame;             // 24..27
        [FieldOffset(28)]
        public uint _pad0;             // 28..31
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicOutgassingGridHeaderDTO
    {
        [FieldOffset(0)]
        public double3 GridOriginAUP;       // 00..23
        [FieldOffset(24)]
        public float CellSizeMeters;        // 24..27
        [FieldOffset(28)]
        public float GlobalQualityWeight;   // 28..31
        [FieldOffset(32)]
        public uint ActiveDensityBufferId;  // 32..35
        [FieldOffset(36)]
        public uint BackDensityBufferId;    // 36..39
        [FieldOffset(40)]
        public uint StateBufferId;          // 40..43
        [FieldOffset(44)]
        public uint DensityVersion;         // 44..47
        [FieldOffset(48)]
        public ushort Resolution;           // 48..49
        [FieldOffset(50)]
        public ushort ActiveSources;        // 50..51
        [FieldOffset(52)]
        public ushort ActiveEntities;       // 52..53
        [FieldOffset(54)]
        public byte Flags;                  // 54
        [FieldOffset(55)]
        public byte _pad0;                  // 55
        [FieldOffset(56)]
        public ulong _pad1;                 // 56..63
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ToxicitySourceDTO
    {
        [FieldOffset(0)]
        public double3 AUP;          // 00..23
        [FieldOffset(24)]
        public float EmissionRate;   // 24..27
        [FieldOffset(28)]
        public float Density;        // 28..31
        [FieldOffset(32)]
        public uint ChemicalHash;    // 32..35
        [FieldOffset(36)]
        public uint _pad0;           // 36..39
        [FieldOffset(40)]
        public ulong _pad1;          // 40..47
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicOutgassingConstants
    {
        [FieldOffset(0)]
        public float BaseDiffusionRate;             // 00..03
        [FieldOffset(4)]
        public float CurrentAdvectionMultiplier;    // 04..07
        [FieldOffset(8)]
        public float AcidCorrosionDamage;           // 08..11
        [FieldOffset(12)]
        public float FloraAbsorptionRate;           // 12..15
        [FieldOffset(16)]
        public float DensityDecayPerSecond;         // 16..19
        [FieldOffset(20)]
        public float SourceRadiusMeters;            // 20..23
        [FieldOffset(24)]
        public float ExposureToxemiaMultiplier;     // 24..27
        [FieldOffset(28)]
        public float CausticDensityThreshold;       // 28..31
        [FieldOffset(32)]
        public float BiolumDensityThreshold;        // 32..35
        [FieldOffset(36)]
        public float MaxDensity;                    // 36..39
        [FieldOffset(40)]
        public float RadialFallbackRadiusScale;     // 40..43
        [FieldOffset(44)]
        public float SdfWallLeakScale;              // 44..47
        [FieldOffset(48)]
        public float GlobalQualityWeight;           // 48..51
        [FieldOffset(52)]
        public float SimulationTickDelta;           // 52..55
        [FieldOffset(56)]
        public uint ChemistryFlags;                 // 56..59
        [FieldOffset(60)]
        public uint _pad0;                          // 60..63
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockFlowField
    {
        [FieldOffset(0)]
        public float3 Direction;   // 00..11
        [FieldOffset(12)]
        public float Speed;        // 12..15
        [FieldOffset(16)]
        public float3 Curl;        // 16..27
        [FieldOffset(28)]
        public float Turbulence;   // 28..31
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockWorldSampler
    {
        [FieldOffset(0)]
        public float SdfDistance;       // 00..03
        [FieldOffset(4)]
        public float FloraAbsorption01; // 04..07
        [FieldOffset(8)]
        public float3 SdfGradient;      // 08..19
        [FieldOffset(20)]
        public uint Flags;              // 20..23
        [FieldOffset(24)]
        public uint PurifierKelpHash;   // 24..27
        [FieldOffset(28)]
        public uint _pad0;              // 28..31
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicityExposureSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x54584F58u; // TOX

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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicityStatusSignal
    {
        [FieldOffset(0)]
        public double3 AUP;       // 00..23
        [FieldOffset(24)]
        public float Magnitude;   // 24..27
        [FieldOffset(28)]
        public uint TargetHash;   // 28..31
        [FieldOffset(32)]
        public uint SourceHash;   // 32..35
        [FieldOffset(36)]
        public uint StatusType;   // 36..39
        [FieldOffset(40)]
        public uint Frame;        // 40..43
        [FieldOffset(44)]
        public ushort SourceId;   // 44..45
        [FieldOffset(46)]
        public ushort TargetId;   // 46..47
        [FieldOffset(48)]
        public byte Channel;      // 48
        [FieldOffset(49)]
        public byte Flags;        // 49
        [FieldOffset(50)]
        public ushort _pad0;      // 50..51
        [FieldOffset(52)]
        public uint _pad1;        // 52..55
        [FieldOffset(56)]
        public ulong _pad2;       // 56..63
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToxicityGridTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 GridOriginAUP;       // 00..23
        [FieldOffset(24)]
        public float MaxDensity;            // 24..27
        [FieldOffset(28)]
        public float TotalPlumeVolume;      // 28..31
        [FieldOffset(32)]
        public float GlobalQualityWeight;   // 32..35
        [FieldOffset(36)]
        public float DiffusionCompleteMs;   // 36..39
        [FieldOffset(40)]
        public uint StateHash;              // 40..43
        [FieldOffset(44)]
        public uint Frame;                  // 44..47
        [FieldOffset(48)]
        public ushort ActiveResolution;     // 48..49
        [FieldOffset(50)]
        public ushort ActiveSources;        // 50..51
        [FieldOffset(52)]
        public ushort ActiveEntities;       // 52..53
        [FieldOffset(54)]
        public byte Flags;                  // 54
        [FieldOffset(55)]
        public byte NanDetected;            // 55
        [FieldOffset(56)]
        public ulong _pad0;                 // 56..63
    }
}
