using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    internal static class ApexBrainContractLayout
    {
        public const int StateStrideBytes = 64;
        public const int MockPlayerAupStrideBytes = 128;
        public const int AcousticEchoTapStrideBytes = 64;
        public const int MockWorldSamplerStrideBytes = 64;
        public const int TuningStrideBytes = 128;
        public const int EmergencyStatsStrideBytes = 64;
        public const int InfluenceNodeStrideBytes = 64;
        public const int OutputStrideBytes = 192;
        public const int TelemetryEntryStrideBytes = 128;
        public const int ProximitySignalStrideBytes = 64;
        public const int MockCombatDamageSignalStrideBytes = 64;
        public const int PanicSignalStrideBytes = 64;
    }

    /// <summary>
    /// Constants for SHINOBU_61 predictive apex aggression.
    /// </summary>
    public static class ApexBrainConstants
    {
        public const int MaxLeviathans = 10;
        public const int MaxAmbushNodes = 16;
        public const int MinAmbushNodes = 2;
        public const int TelemetryFrames = 300;
        public const int TelemetryCapacity = TelemetryFrames * MaxLeviathans;
        public const int InfluenceNodeCapacity = MaxLeviathans * MaxAmbushNodes;
        public const int MaxAcousticTaps = 32;
        public const int CsvScratchBytes = 4096;
        public const float Epsilon = 0.0001f;
        public const float MinimumQualityNodeHold = 0.1f;
        public const float SdfMidsectionStartQuality = 0.25f;
        public const float SdfTailStartQuality = 0.55f;
        public const uint AbyssalTrenchBiomeHash = 0xA8B55110u;
    }

    /// <summary>
    /// Fixed byte offsets for the primary ApexStateDTO cache-line record.
    /// </summary>
    public static class ApexStateLayout
    {
        public const int SizeBytes = 64;
        public const int AupOffset = 0;
        public const int VelocityOffset = 24;
        public const int AggressionLevelOffset = 36;
        public const int TargetHashOffset = 40;
        public const int AcousticMemoryHashOffset = 44;
        public const int StaminaOffset = 48;
        public const int PadAlignOffset = 52;
        public const int Pad0Offset = 56;
    }

    /// <summary>
    /// Phase bytes emitted by the utility matrix. These are data labels, not C# state classes.
    /// </summary>
    public static class ApexBrainPhase
    {
        public const byte Dormant = 0;
        public const byte Stalk = 1;
        public const byte Ambush = 2;
        public const byte Strike = 3;
        public const byte Hide = 4;
    }

    /// <summary>
    /// Output and telemetry bit flags.
    /// </summary>
    public static class ApexBrainFlags
    {
        public const byte Active = 1 << 0;
        public const byte SweetLieOccluded = 1 << 1;
        public const byte AcousticOverride = 1 << 2;
        public const byte StrikeCommitted = 1 << 3;
        public const byte ReducedQualityNodeBudget = 1 << 4;
        public const byte TailSdfSampled = 1 << 5;
        public const byte Fault = 1 << 6;
        public const byte EmergencyMockStats = 1 << 7;
    }

    /// <summary>
    /// Input flags for mock player and target rows.
    /// </summary>
    public static class MockPlayerAupFlags
    {
        public const uint Active = 1u << 0;
        public const uint WfcBaseTarget = 1u << 1;
        public const uint HasForward = 1u << 2;
        public const uint AbyssalTrench = 1u << 3;
    }

    /// <summary>
    /// Pure unmanaged fallback data used by Burst jobs and cold vault hydration when no legacy apex curve payload exists.
    /// </summary>
    public static class ApexBrainDefaults
    {
        public static ApexBrainTuning BuildEmergencyMockTuning()
        {
            ApexBrainTuning tuning = default;
            tuning.AggressionMultiplier = 1.15f;
            tuning.AcousticSensitivity = 1.25f;
            tuning.TurnRate = 0.22f;
            tuning.StalkingDistance = 90f;
            tuning.LeviathanSpeed = 28f;
            tuning.TerrorRadius = 160f;
            tuning.BaseDamageMagnitude = 700f;
            tuning.BiomeAggressionMultiplier = 2f;
            tuning.GlobalQualityWeight = 1f;
            tuning.SimulationTickDelta = 1f / 30f;
            tuning.CurrentTimeSeconds = 0f;
            tuning.StrikeDistance = 28f;
            tuning.HeadOffsetMeters = 28f;
            tuning.MidOffsetMeters = 18f;
            tuning.TailOffsetMeters = 42f;
            tuning.PreferredBiomeHash = ApexBrainConstants.AbyssalTrenchBiomeHash;
            tuning.NoiseAggroGain = 0.35f;
            tuning.StaminaRecoveryPerSecond = 0.12f;
            tuning.StaminaStrikeCost = 0.16f;
            tuning.SweetLieShadowGain = 0.85f;
            tuning.SweetLieViewDotThreshold = 0.58f;
            tuning.AmbushNodeRadiusMeters = 38f;
            tuning.VisualOverkillGain = 1f;
            tuning.BiteHeadLocalOffset = 9f;
            tuning.SourceHash = 0x53484E61u;
            tuning.Flags = ApexBrainFlags.EmergencyMockStats;
            tuning.LastCsvHash = 0u;
            tuning.CsvReloadVersion = 0u;
            tuning.LastCsvWriteTicks = 0UL;
            return tuning;
        }

        public static ApexEmergencyStats BuildEmergencyMockStats()
        {
            ApexEmergencyStats stats = default;
            stats.AggressionBuildSeconds = new float4(12f, 8f, 5f, 3f);
            stats.TurnRadiiMeters = new float4(44f, 35f, 28f, 18f);
            stats.StrikeWindowsSeconds = new float4(1.6f, 1.2f, 0.9f, 0.65f);
            stats.VisualOverkillScalars = new float4(0.35f, 0.65f, 1f, 1.35f);
            return stats;
        }

        public static MockWorldSampler BuildEmergencyMockWorldSampler()
        {
            MockWorldSampler sampler = default;
            sampler.CaveRadiusMeters = 36f;
            sampler.FloorY = -28f;
            sampler.CeilingY = 26f;
            sampler.GradientProbeMeters = 2f;
            sampler.SpatialCellSizeMeters = 16f;
            sampler.CanyonBias01 = 0.25f;
            sampler.WallRepulsionGain = 0.85f;
            sampler.HeadOffsetMeters = 28f;
            sampler.MidOffsetMeters = 18f;
            sampler.TailOffsetMeters = 42f;
            sampler.SdfSoftMarginMeters = 6f;
            sampler.Seed = 0x53484E61u;
            return sampler;
        }
    }

    /// <summary>
    /// Cache-line apex predator truth. Size: 64 bytes without packed runtime layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.StateStrideBytes)]
    public struct ApexStateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float AggressionLevel;
        [FieldOffset(40)] public uint TargetHash;
        [FieldOffset(44)] public uint AcousticMemoryHash;
        [FieldOffset(48)] public float Stamina;
        [FieldOffset(52)] private uint _padAlign0;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    /// <summary>
    /// Blind target kinematics packet used when Player Kinematics is absent. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.MockPlayerAupStrideBytes)]
    public partial struct MockPlayerAUP
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public ulong LastAdvanceFrame;
        [FieldOffset(32)] public float3 Velocity;
        [FieldOffset(44)] public float3 Forward;
        [FieldOffset(56)] public uint TargetHash;
        [FieldOffset(60)] public uint BiomeHash;
        [FieldOffset(64)] public float Noise01;
        [FieldOffset(68)] public float AcousticMagnitude01;
        [FieldOffset(72)] public float SimulationTickDelta;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] private uint _pad0;
        [FieldOffset(84)] private uint _pad1;
        [FieldOffset(88)] private uint _pad2;
        [FieldOffset(92)] private uint _pad3;
        [FieldOffset(96)] private uint _pad4;
        [FieldOffset(100)] private uint _pad5;
        [FieldOffset(104)] private uint _pad6;
        [FieldOffset(108)] private uint _pad7;
        [FieldOffset(112)] private uint _pad8;
        [FieldOffset(116)] private uint _pad9;
        [FieldOffset(120)] private uint _pad10;
        [FieldOffset(124)] private uint _pad11;
    }

    /// <summary>
    /// Decoupled acoustic tap consumed by the apex cortex without referencing the audio runtime. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.AcousticEchoTapStrideBytes)]
    public partial struct ApexBrainAcousticEchoTap : ISignal
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float Magnitude01;
        [FieldOffset(28)] public float AgeSeconds;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint AcousticMemoryHash;
        [FieldOffset(44)] public byte Flags;
        [FieldOffset(45)] private byte _pad0;
        [FieldOffset(46)] private byte _pad1;
        [FieldOffset(47)] private byte _pad2;
        [FieldOffset(48)] private byte _pad3;
        [FieldOffset(49)] private byte _pad4;
        [FieldOffset(50)] private byte _pad5;
        [FieldOffset(51)] private byte _pad6;
        [FieldOffset(52)] private byte _pad7;
        [FieldOffset(53)] private byte _pad8;
        [FieldOffset(54)] private byte _pad9;
        [FieldOffset(55)] private byte _pad10;
        [FieldOffset(56)] private byte _pad11;
        [FieldOffset(57)] private byte _pad12;
        [FieldOffset(58)] private byte _pad13;
        [FieldOffset(59)] private byte _pad14;
        [FieldOffset(60)] private byte _pad15;
        [FieldOffset(61)] private byte _pad16;
        [FieldOffset(62)] private byte _pad17;
        [FieldOffset(63)] private byte _pad18;
    }

    /// <summary>
    /// Analytic SDF lie for caves and canyon cover. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.MockWorldSamplerStrideBytes)]
    public struct MockWorldSampler
    {
        [FieldOffset(0)] public float3 OriginLocal;
        [FieldOffset(12)] public float CaveRadiusMeters;
        [FieldOffset(16)] public float FloorY;
        [FieldOffset(20)] public float CeilingY;
        [FieldOffset(24)] public float GradientProbeMeters;
        [FieldOffset(28)] public float SpatialCellSizeMeters;
        [FieldOffset(32)] public float CanyonBias01;
        [FieldOffset(36)] public float WallRepulsionGain;
        [FieldOffset(40)] public float HeadOffsetMeters;
        [FieldOffset(44)] public float MidOffsetMeters;
        [FieldOffset(48)] public float TailOffsetMeters;
        [FieldOffset(52)] public float SdfSoftMarginMeters;
        [FieldOffset(56)] public uint Seed;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>
    /// Human-authored apex tuning row. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.TuningStrideBytes)]
    public struct ApexBrainTuning
    {
        [FieldOffset(0)] public ulong LastCsvWriteTicks;
        [FieldOffset(8)] public float AggressionMultiplier;
        [FieldOffset(12)] public float AcousticSensitivity;
        [FieldOffset(16)] public float TurnRate;
        [FieldOffset(20)] public float StalkingDistance;
        [FieldOffset(24)] public float LeviathanSpeed;
        [FieldOffset(28)] public float TerrorRadius;
        [FieldOffset(32)] public float BaseDamageMagnitude;
        [FieldOffset(36)] public float BiomeAggressionMultiplier;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float SimulationTickDelta;
        [FieldOffset(48)] public float CurrentTimeSeconds;
        [FieldOffset(52)] public float StrikeDistance;
        [FieldOffset(56)] public float HeadOffsetMeters;
        [FieldOffset(60)] public float MidOffsetMeters;
        [FieldOffset(64)] public float TailOffsetMeters;
        [FieldOffset(68)] public uint PreferredBiomeHash;
        [FieldOffset(72)] public float NoiseAggroGain;
        [FieldOffset(76)] public float StaminaRecoveryPerSecond;
        [FieldOffset(80)] public float StaminaStrikeCost;
        [FieldOffset(84)] public float SweetLieShadowGain;
        [FieldOffset(88)] public float SweetLieViewDotThreshold;
        [FieldOffset(92)] public float AmbushNodeRadiusMeters;
        [FieldOffset(96)] public float VisualOverkillGain;
        [FieldOffset(100)] public float BiteHeadLocalOffset;
        [FieldOffset(104)] public uint SourceHash;
        [FieldOffset(108)] public uint Flags;
        [FieldOffset(112)] public uint LastCsvHash;
        [FieldOffset(116)] public uint CsvReloadVersion;
        [FieldOffset(120)] private uint _pad0;
        [FieldOffset(124)] private uint _pad1;
    }

    /// <summary>
    /// Emergency 16-byte aligned fallback stats when legacy .h8bin curves are absent. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.EmergencyStatsStrideBytes)]
    public struct ApexEmergencyStats
    {
        [FieldOffset(0)] public float4 AggressionBuildSeconds;
        [FieldOffset(16)] public float4 TurnRadiiMeters;
        [FieldOffset(32)] public float4 StrikeWindowsSeconds;
        [FieldOffset(48)] public float4 VisualOverkillScalars;
    }

    /// <summary>
    /// Ambush node scratch record. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.InfluenceNodeStrideBytes)]
    public struct ApexInfluenceNode
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Score;
        [FieldOffset(16)] public float3 Direction;
        [FieldOffset(28)] public uint SpatialHash;
        [FieldOffset(32)] public float SdfSafety01;
        [FieldOffset(36)] public float SweetLieWeight01;
        [FieldOffset(40)] public float FractionalWeight01;
        [FieldOffset(44)] public uint NodeIndex;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private uint _pad1;
        [FieldOffset(60)] private uint _pad2;
    }

    /// <summary>
    /// Apex brain output consumed by movement, animation, and presentation bridges. Size: 192 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.OutputStrideBytes)]
    public struct ApexBrainOutputDTO
    {
        [FieldOffset(0)] public float3 DesiredVelocity;
        [FieldOffset(12)] public float DesiredSpeed;
        [FieldOffset(16)] public float3 IK_BiteTarget;
        [FieldOffset(28)] public float AggressionLevel;
        [FieldOffset(32)] public float3 InterceptLocal;
        [FieldOffset(44)] public float StalkUtility;
        [FieldOffset(48)] public float3 AcousticMemoryLocal;
        [FieldOffset(60)] public float AmbushUtility;
        [FieldOffset(64)] public float3 WallRepulsion;
        [FieldOffset(76)] public float StrikeUtility;
        [FieldOffset(80)] public float3 BestAmbushNodeLocal;
        [FieldOffset(92)] public float SweetLieLos01;
        [FieldOffset(96)] public uint SpatialHash;
        [FieldOffset(100)] public uint StateHash;
        [FieldOffset(104)] public uint EvaluatedNodeCount;
        [FieldOffset(108)] public float FractionalNodeWeight01;
        [FieldOffset(112)] public float3 DesiredDirection;
        [FieldOffset(124)] public float TerrorRadiusMeters;
        [FieldOffset(128)] public float4 VisualOverkillScalars;
        [FieldOffset(144)] public uint TargetHash;
        [FieldOffset(148)] public uint AcousticMemoryHash;
        [FieldOffset(152)] public ushort Slot;
        [FieldOffset(154)] public byte Phase;
        [FieldOffset(155)] public byte Flags;
        [FieldOffset(156)] private byte _pad0;
        [FieldOffset(157)] private byte _pad1;
        [FieldOffset(158)] private byte _pad2;
        [FieldOffset(159)] private byte _pad3;
        [FieldOffset(160)] private byte _pad4;
        [FieldOffset(161)] private byte _pad5;
        [FieldOffset(162)] private byte _pad6;
        [FieldOffset(163)] private byte _pad7;
        [FieldOffset(164)] private byte _pad8;
        [FieldOffset(165)] private byte _pad9;
        [FieldOffset(166)] private byte _pad10;
        [FieldOffset(167)] private byte _pad11;
        [FieldOffset(168)] private byte _pad12;
        [FieldOffset(169)] private byte _pad13;
        [FieldOffset(170)] private byte _pad14;
        [FieldOffset(171)] private byte _pad15;
        [FieldOffset(172)] private byte _pad16;
        [FieldOffset(173)] private byte _pad17;
        [FieldOffset(174)] private byte _pad18;
        [FieldOffset(175)] private byte _pad19;
        [FieldOffset(176)] private byte _pad20;
        [FieldOffset(177)] private byte _pad21;
        [FieldOffset(178)] private byte _pad22;
        [FieldOffset(179)] private byte _pad23;
        [FieldOffset(180)] private byte _pad24;
        [FieldOffset(181)] private byte _pad25;
        [FieldOffset(182)] private byte _pad26;
        [FieldOffset(183)] private byte _pad27;
        [FieldOffset(184)] private byte _pad28;
        [FieldOffset(185)] private byte _pad29;
        [FieldOffset(186)] private byte _pad30;
        [FieldOffset(187)] private byte _pad31;
        [FieldOffset(188)] private byte _pad32;
        [FieldOffset(189)] private byte _pad33;
        [FieldOffset(190)] private byte _pad34;
        [FieldOffset(191)] private byte _pad35;
    }

    /// <summary>
    /// 300-frame black-box row for SHINOBU_61. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.TelemetryEntryStrideBytes)]
    public struct ApexTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint SpatialHash;
        [FieldOffset(12)] public uint AcousticMemoryHash;
        [FieldOffset(16)] public float3 InterceptLocal;
        [FieldOffset(28)] public float AggressionLevel;
        [FieldOffset(32)] public float3 DesiredVelocity;
        [FieldOffset(44)] public float SweetLieLos01;
        [FieldOffset(48)] public float3 WallRepulsion;
        [FieldOffset(60)] public float StrikeUtility;
        [FieldOffset(64)] public float4 UtilityScores;
        [FieldOffset(80)] public uint TargetHash;
        [FieldOffset(84)] public uint BiomeHash;
        [FieldOffset(88)] public uint EvaluatedNodeCount;
        [FieldOffset(92)] public float GlobalQualityWeight;
        [FieldOffset(96)] public float ActiveLeviathans;
        [FieldOffset(100)] public float InterceptComputeTimeMs;
        [FieldOffset(104)] public uint FaultCode;
        [FieldOffset(108)] public ushort Slot;
        [FieldOffset(110)] public byte Phase;
        [FieldOffset(111)] public byte Flags;
        [FieldOffset(112)] private byte _pad0;
        [FieldOffset(113)] private byte _pad1;
        [FieldOffset(114)] private byte _pad2;
        [FieldOffset(115)] private byte _pad3;
        [FieldOffset(116)] private byte _pad4;
        [FieldOffset(117)] private byte _pad5;
        [FieldOffset(118)] private byte _pad6;
        [FieldOffset(119)] private byte _pad7;
        [FieldOffset(120)] private byte _pad8;
        [FieldOffset(121)] private byte _pad9;
        [FieldOffset(122)] private byte _pad10;
        [FieldOffset(123)] private byte _pad11;
        [FieldOffset(124)] private byte _pad12;
        [FieldOffset(125)] private byte _pad13;
        [FieldOffset(126)] private byte _pad14;
        [FieldOffset(127)] private byte _pad15;
    }

    /// <summary>
    /// Proximity pressure signal for audio/HUD consumers. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.ProximitySignalStrideBytes)]
    public partial struct ApexProximitySignal : ISignal
    {
        [FieldOffset(0)] public double3 SourceAup;
        [FieldOffset(24)] public float Aggression01;
        [FieldOffset(28)] public float TerrorRadiusMeters;
        [FieldOffset(32)] public float Rumble01;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public ushort Slot;
        [FieldOffset(46)] public byte Phase;
        [FieldOffset(47)] public byte Flags;
        [FieldOffset(48)] private byte _pad0;
        [FieldOffset(49)] private byte _pad1;
        [FieldOffset(50)] private byte _pad2;
        [FieldOffset(51)] private byte _pad3;
        [FieldOffset(52)] private byte _pad4;
        [FieldOffset(53)] private byte _pad5;
        [FieldOffset(54)] private byte _pad6;
        [FieldOffset(55)] private byte _pad7;
        [FieldOffset(56)] private byte _pad8;
        [FieldOffset(57)] private byte _pad9;
        [FieldOffset(58)] private byte _pad10;
        [FieldOffset(59)] private byte _pad11;
        [FieldOffset(60)] private byte _pad12;
        [FieldOffset(61)] private byte _pad13;
        [FieldOffset(62)] private byte _pad14;
        [FieldOffset(63)] private byte _pad15;
    }

    /// <summary>
    /// Direct mathematical base-hit signal for WFC/base systems. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.MockCombatDamageSignalStrideBytes)]
    public partial struct MockCombatDamageSignal : ISignal
    {
        [FieldOffset(0)] public double3 TargetAup;
        [FieldOffset(24)] public float3 ImpactDirection;
        [FieldOffset(36)] public float Magnitude;
        [FieldOffset(40)] public uint TargetHash;
        [FieldOffset(44)] public uint SourceHash;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public ushort Slot;
        [FieldOffset(54)] public byte Flags;
        [FieldOffset(55)] private byte _pad0;
        [FieldOffset(56)] private byte _pad1;
        [FieldOffset(57)] private byte _pad2;
        [FieldOffset(58)] private byte _pad3;
        [FieldOffset(59)] private byte _pad4;
        [FieldOffset(60)] private byte _pad5;
        [FieldOffset(61)] private byte _pad6;
        [FieldOffset(62)] private byte _pad7;
        [FieldOffset(63)] private byte _pad8;
    }

    /// <summary>
    /// Fauna panic broadcast emitted before apex arrival. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ApexBrainContractLayout.PanicSignalStrideBytes)]
    public partial struct ApexPanicSignal : ISignal
    {
        [FieldOffset(0)] public double3 SourceAup;
        [FieldOffset(24)] public float3 Direction;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public float Intensity01;
        [FieldOffset(44)] public uint SourceHash;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public ushort Slot;
        [FieldOffset(54)] public byte Flags;
        [FieldOffset(55)] private byte _pad0;
        [FieldOffset(56)] private byte _pad1;
        [FieldOffset(57)] private byte _pad2;
        [FieldOffset(58)] private byte _pad3;
        [FieldOffset(59)] private byte _pad4;
        [FieldOffset(60)] private byte _pad5;
        [FieldOffset(61)] private byte _pad6;
        [FieldOffset(62)] private byte _pad7;
        [FieldOffset(63)] private byte _pad8;
    }
}
