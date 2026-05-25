using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    public static class UtilityAICognitionConstants
    {
        public const int MaxCreatures = 4096;
        public const int MaxTargets = 4096;
        public const int TargetBucketCount = 1024;
        public const int MaxProfiles = 128;
        public const int TelemetryFrames = 300;
        public const int CsvScratchBytes = 16384;
        public const int DearLieCandidateLimit = 4;
        public const float Epsilon = 0.0001f;
        public const float FaultMicroseconds = 1500f;

        public const uint ActionFleeHash = 0x464C4545u;
        public const uint ActionHuntHash = 0x48554E54u;
        public const uint ActionPatrolHash = 0x5054524Cu;
        public const uint ActionRestHash = 0x52455354u;
        public const uint AgentHash = 0x53333032u;
    }

    public static class UtilityAICognitionStateLayout
    {
        public const int SizeBytes = 32;
        public const int HungerOffset = 0;
        public const int FearOffset = 4;
        public const int AggressionOffset = 8;
        public const int ActiveActionHashOffset = 12;
        public const int TargetEntityHashOffset = 16;
        public const int ActionCooldownOffset = 20;
        public const int Pad0Offset = 24;
    }

    public static class UtilityAICognitionActionFlags
    {
        public const byte Active = 1 << 0;
        public const byte Fault = 1 << 1;
        public const byte NoTarget = 1 << 2;
        public const byte DueTick = 1 << 3;
        public const byte ReducedCandidateBudget = 1 << 4;
        public const byte EmergencyMock = 1 << 5;
        public const byte HighQuality = 1 << 6;
        public const byte Reserved = 1 << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CognitionStateDTO
    {
        [FieldOffset(0)] public float Hunger01;
        [FieldOffset(4)] public float Fear01;
        [FieldOffset(8)] public float Aggression01;
        [FieldOffset(12)] public uint ActiveActionHash;
        [FieldOffset(16)] public uint TargetEntityHash;
        [FieldOffset(20)] public float ActionCooldown;
        [FieldOffset(24)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CognitionAupDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public uint EntityHash;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CognitionTargetCandidateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public uint EntityHash;
        [FieldOffset(28)] public uint SpeciesHash;
        [FieldOffset(32)] public float Threat01;
        [FieldOffset(36)] public float FoodValue01;
        [FieldOffset(40)] public float Weakness01;
        [FieldOffset(44)] public float Noise01;
        [FieldOffset(48)] public byte Flags;
        [FieldOffset(49)] private byte _padByte0;
        [FieldOffset(50)] private ushort _padShort0;
        [FieldOffset(52)] public uint SpatialHash;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CognitionUtilityTuningDTO
    {
        [FieldOffset(0)] public float4 HungerPolynomial;
        [FieldOffset(16)] public float4 FearPolynomial;
        [FieldOffset(32)] public float4 AggressionPolynomial;
        [FieldOffset(48)] public float4 ActionBiases;
        [FieldOffset(64)] public float4 SignalGains;
        [FieldOffset(80)] public float4 DistanceMeters;
        [FieldOffset(96)] public float4 Runtime;
        [FieldOffset(112)] public uint Frame;
        [FieldOffset(116)] public uint LastCsvHash;
        [FieldOffset(120)] public uint CsvReloadVersion;
        [FieldOffset(124)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CognitionActionOutputDTO
    {
        [FieldOffset(0)] public float4 Utilities;
        [FieldOffset(16)] public float3 DesiredLocalDirection;
        [FieldOffset(28)] public float MaxUtility;
        [FieldOffset(32)] public uint ActionHash;
        [FieldOffset(36)] public uint TargetEntityHash;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public float TickIntervalSeconds;
        [FieldOffset(48)] public float CooldownRemaining;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public byte Flags;
        [FieldOffset(57)] public byte CandidateCount;
        [FieldOffset(58)] private ushort _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct CognitionProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float4 HungerPolynomial;
        [FieldOffset(24)] public float4 FearPolynomial;
        [FieldOffset(40)] public float4 AggressionPolynomial;
        [FieldOffset(56)] public float4 Weights;
        [FieldOffset(72)] public float4 DistanceMeters;
        [FieldOffset(88)] public uint LastAppliedHash;
        [FieldOffset(92)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CognitionMovementAcousticSignalDTO
    {
        [FieldOffset(0)] public double3 PositionAup;
        [FieldOffset(24)] public float Volume;
        [FieldOffset(28)] public float VelocitySq;
        [FieldOffset(32)] public uint SourceId;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public byte LocomotionMode;
        [FieldOffset(41)] public byte SurfaceMode;
        [FieldOffset(42)] public byte Flags;
        [FieldOffset(43)] private byte _pad0;
        [FieldOffset(44)] private uint _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CognitionCombatDamageSignalDTO
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint DamageType;
        [FieldOffset(32)] public uint TargetHash;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public byte Flags;
        [FieldOffset(45)] private byte _pad0;
        [FieldOffset(46)] private ushort _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CognitionTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActionHashFold;
        [FieldOffset(8)] public uint HuntingCount;
        [FieldOffset(12)] public uint FaultFlags;
        [FieldOffset(16)] public float AverageFear01;
        [FieldOffset(20)] public float AverageHunger01;
        [FieldOffset(24)] public float AverageAggression01;
        [FieldOffset(28)] public float MaximumUtility;
        [FieldOffset(32)] public float BurstMicroseconds;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint ActiveCount;
        [FieldOffset(44)] public uint NonFiniteCount;
        [FieldOffset(48)] public ulong TargetHashFold;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CognitionDumpHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint EndianMarker;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint EntryCount;
        [FieldOffset(20)] public uint EntrySizeBytes;
        [FieldOffset(24)] public uint Cursor;
        [FieldOffset(28)] public uint AgentHash;
    }

    public static class UtilityAICognitionDefaults
    {
        public static CognitionUtilityTuningDTO BuildTuning()
        {
            CognitionUtilityTuningDTO tuning = default;
            tuning.HungerPolynomial = new float4(0.65f, -0.15f, 0.5f, 0f);
            tuning.FearPolynomial = new float4(0.75f, -0.25f, 0.5f, 0f);
            tuning.AggressionPolynomial = new float4(0.45f, 0.1f, 0.45f, 0f);
            tuning.ActionBiases = new float4(0.02f, 0.01f, 0.08f, 0f);
            tuning.SignalGains = new float4(0.35f, 0.55f, 0.035f, 0.4f);
            tuning.DistanceMeters = new float4(220f, 140f, 48f, 131072f);
            tuning.Runtime = new float4(1f, 1f / 30f, 0f, UtilityAICognitionConstants.FaultMicroseconds);
            tuning.Flags = UtilityAICognitionActionFlags.EmergencyMock;
            return tuning;
        }

        public static CognitionProfileDTO BuildFallbackProfile()
        {
            CognitionProfileDTO profile = default;
            CognitionUtilityTuningDTO tuning = BuildTuning();
            profile.SpeciesHash = UtilityAICognitionConstants.AgentHash;
            profile.Flags = UtilityAICognitionActionFlags.EmergencyMock;
            profile.HungerPolynomial = tuning.HungerPolynomial;
            profile.FearPolynomial = tuning.FearPolynomial;
            profile.AggressionPolynomial = tuning.AggressionPolynomial;
            profile.Weights = new float4(1f, 1f, 1f, 1f);
            profile.DistanceMeters = tuning.DistanceMeters;
            return profile;
        }
    }
}
