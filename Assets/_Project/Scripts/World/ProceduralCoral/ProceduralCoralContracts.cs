using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralCoral
{
    public static class ProceduralCoralConstants
    {
        public const int MaxRules = 16;
        public const int MaxInstructions = 8192;
        public const int MaxBranches = 4096;
        public const int MaxTurtleStack = 512;
        public const int MaxSpatialCells = 2048;
        public const int MaxRenderMatrices = MaxBranches;
        public const int MaxCollisionProxies = 1024;
        public const int MaxSyncPulses = 1024;
        public const int MaxDebugSegments = MaxBranches;
        public const int MaxHzbTiles = 4096;
        public const int TelemetryFrames = 300;
        public const int CsvScratchBytes = 32768;
        public const int RuleBinaryHeaderBytes = 16;
        public const int RuleBinaryRecordBytes = 64;
        public const float Epsilon = 0.0001f;
        public const uint RuleBinaryMagic = 0x43473848u;
        public const uint RuleBinaryVersion = 1u;
        public const uint DumpMagic = 0x434F524Cu;
        public const uint DumpEndianMarker = 0x01020304u;
        public const uint FaultNoRules = 0x4E4F5255u;
        public const uint FaultCapacity = 0x43415021u;
        public const uint FaultNonFinite = 0x4E414E21u;
        public const uint FaultCollisionPruned = 0x5052554Eu;
        public const uint FaultStackOverflow = 0x53544B21u;
        public const uint FaultRulePayload = 0x52554C45u;
        public const uint FaultAuditLayout = 0x4C41594Fu;
        public const uint FaultAuditVault = 0x56414C54u;

        public const uint OpGrow = 0x47524F57u;
        public const uint OpTurnLeft = 0x544C4654u;
        public const uint OpTurnRight = 0x54524754u;
        public const uint OpPitchUp = 0x50555021u;
        public const uint OpPitchDown = 0x50444E21u;
        public const uint OpRoll = 0x524F4C4Cu;
        public const uint OpPush = 0x50555348u;
        public const uint OpPop = 0x504F5021u;
        public const uint OpTip = 0x54495021u;
        public const uint OpThin = 0x5448494Eu;
        public const uint OpFork = 0x464F524Bu;
    }

    public static class ProceduralCoralVaultBufferIds
    {
        public const BufferID Rules = (BufferID)71390;
        public const BufferID InstructionScratchA = (BufferID)71391;
        public const BufferID InstructionScratchB = (BufferID)71392;
        public const BufferID Branches = (BufferID)71393;
        public const BufferID TurtleStack = (BufferID)71394;
        public const BufferID SpatialCells = (BufferID)71395;
        public const BufferID RenderMatrices = (BufferID)71396;
        public const BufferID IndirectArgs = (BufferID)71397;
        public const BufferID SectorTriggers = (BufferID)71398;
        public const BufferID CollisionProxies = (BufferID)71399;
        public const BufferID SyncPulses = (BufferID)71400;
        public const BufferID TelemetryRing = (BufferID)71401;
        public const BufferID TelemetryCursor = (BufferID)71402;
        public const BufferID Tuning = (BufferID)71403;
        public const BufferID CsvScratch = (BufferID)71404;
        public const BufferID Counters = (BufferID)71405;
        public const BufferID DebugSegments = (BufferID)71406;
        public const BufferID GpuSway = (BufferID)71407;
        public const BufferID SelfAudit = (BufferID)71408;
        public const BufferID HzbTiles = (BufferID)71409;
    }

    public static class CoralBranchFlags
    {
        public const uint Alive = 1u << 0;
        public const uint Tip = 1u << 1;
        public const uint Root = 1u << 2;
        public const uint CollisionAdjusted = 1u << 3;
        public const uint CollisionPruned = 1u << 4;
        public const uint NonFiniteFallback = 1u << 5;
        public const uint Bioluminescent = 1u << 6;
    }

    public static class CoralRuleFlags
    {
        public const uint Empty = 0u;
        public const uint EmitsBranch = 1u << 0;
        public const uint EmitsTip = 1u << 1;
        public const uint TrunkRule = 1u << 2;
        public const uint FineRule = 1u << 3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralLSystemRuleDTO
    {
        [FieldOffset(0)]
        public uint SourceOpcode;
        [FieldOffset(4)]
        public uint Replacement0;
        [FieldOffset(8)]
        public uint Replacement1;
        [FieldOffset(12)]
        public uint Replacement2;
        [FieldOffset(16)]
        public uint Replacement3;
        [FieldOffset(20)]
        public uint Replacement4;
        [FieldOffset(24)]
        public uint Replacement5;
        [FieldOffset(28)]
        public uint Replacement6;
        [FieldOffset(32)]
        public uint Replacement7;
        [FieldOffset(36)]
        public byte ReplacementCount;
        [FieldOffset(37)]
        public byte RuleIndex;
        [FieldOffset(38)]
        public ushort _pad0;
        [FieldOffset(40)]
        public float BranchAngleRadians;
        [FieldOffset(44)]
        public float LengthScale;
        [FieldOffset(48)]
        public float RadiusScale;
        [FieldOffset(52)]
        public uint PrefabHash;
        [FieldOffset(56)]
        public uint Flags;
        [FieldOffset(60)]
        public uint WeightHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CoralBranchDTO
    {
        [FieldOffset(0)]
        public float4x4 LocalMatrix;
        [FieldOffset(64)]
        public uint PrefabHash;
        [FieldOffset(68)]
        public uint GenerationDepth;
        [FieldOffset(72)]
        public double3 SectorAUP;
        [FieldOffset(96)]
        public float Stiffness;
        [FieldOffset(100)]
        public float Radius;
        [FieldOffset(104)]
        public uint StateFlags;
        [FieldOffset(108)]
        public uint ParentIndex;
        [FieldOffset(112)]
        public uint StableId;
        [FieldOffset(116)]
        public uint SectorHash;
        [FieldOffset(120)]
        public uint _pad0;
        [FieldOffset(124)]
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralSectorTriggerDTO
    {
        [FieldOffset(0)]
        public double3 RootAUP;
        [FieldOffset(24)]
        public uint SectorHash;
        [FieldOffset(28)]
        public uint Seed;
        [FieldOffset(32)]
        public float GlobalQualityWeight;
        [FieldOffset(36)]
        public uint SimulationFrame;
        [FieldOffset(40)]
        public float BaseStepMeters;
        [FieldOffset(44)]
        public float BaseRadiusMeters;
        [FieldOffset(48)]
        public int MaxDepth;
        [FieldOffset(52)]
        public float SectorRadiusMeters;
        [FieldOffset(56)]
        public uint Flags;
        [FieldOffset(60)]
        public uint SeedSalt;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CoralSectorSaveDTO
    {
        [FieldOffset(0)]
        public uint SectorHash;
        [FieldOffset(4)]
        public uint Seed;
        [FieldOffset(8)]
        public uint RulePayloadHash;
        [FieldOffset(12)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralTuningDTO
    {
        [FieldOffset(0)]
        public float GlobalQualityWeight;
        [FieldOffset(4)]
        public float BranchAngleRadians;
        [FieldOffset(8)]
        public float AngleVarianceRadians;
        [FieldOffset(12)]
        public float BaseStepMeters;
        [FieldOffset(16)]
        public float BaseRadiusMeters;
        [FieldOffset(20)]
        public float RadiusDecay;
        [FieldOffset(24)]
        public float SdfAvoidanceWeight;
        [FieldOffset(28)]
        public int MaxDepth;
        [FieldOffset(32)]
        public int MaxBranches;
        [FieldOffset(36)]
        public int MaxInstructions;
        [FieldOffset(40)]
        public float VisibilityDistanceMin;
        [FieldOffset(44)]
        public float VisibilityDistanceMax;
        [FieldOffset(48)]
        public float CurrentSwayAmplitude;
        [FieldOffset(52)]
        public uint Version;
        [FieldOffset(56)]
        public uint SeedSalt;
        [FieldOffset(60)]
        public uint LastRulePayloadHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralTurtleStateDTO
    {
        [FieldOffset(0)]
        public float3 LocalPosition;
        [FieldOffset(12)]
        public float Radius;
        [FieldOffset(16)]
        public quaternion Rotation;
        [FieldOffset(32)]
        public uint ParentIndex;
        [FieldOffset(36)]
        public uint Depth;
        [FieldOffset(40)]
        public uint StableId;
        [FieldOffset(44)]
        public uint RuleHash;
        [FieldOffset(48)]
        public float StepMeters;
        [FieldOffset(52)]
        public float Stiffness;
        [FieldOffset(56)]
        public uint _pad0;
        [FieldOffset(60)]
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CoralSpatialCellDTO
    {
        [FieldOffset(0)]
        public float3 LocalPosition;
        [FieldOffset(12)]
        public float Radius;
        [FieldOffset(16)]
        public uint BranchIndex;
        [FieldOffset(20)]
        public uint SectorHash;
        [FieldOffset(24)]
        public uint OccupancyHash;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CapsuleColliderDTO
    {
        [FieldOffset(0)]
        public double3 CenterAUP;
        [FieldOffset(24)]
        public float3 Axis;
        [FieldOffset(36)]
        public float Radius;
        [FieldOffset(40)]
        public float Height;
        [FieldOffset(44)]
        public uint BranchIndex;
        [FieldOffset(48)]
        public uint Flags;
        [FieldOffset(52)]
        public uint SectorHash;
        [FieldOffset(56)]
        public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SyncPulseDTO
    {
        [FieldOffset(0)]
        public double3 OriginAUP;
        [FieldOffset(24)]
        public float WaveSpeed;
        [FieldOffset(28)]
        public uint ColorOverride;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralGenerationTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 RootAUP;
        [FieldOffset(24)]
        public uint Frame;
        [FieldOffset(28)]
        public uint SectorHash;
        [FieldOffset(32)]
        public int BranchCount;
        [FieldOffset(36)]
        public int DepthReached;
        [FieldOffset(40)]
        public float BurstComputeUs;
        [FieldOffset(44)]
        public float GlobalQualityWeight;
        [FieldOffset(48)]
        public uint StateHash;
        [FieldOffset(52)]
        public uint FaultFlags;
        [FieldOffset(56)]
        public uint TipCount;
        [FieldOffset(60)]
        public uint MatrixCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralDebugSegmentDTO
    {
        [FieldOffset(0)]
        public double3 StartAUP;
        [FieldOffset(24)]
        public double3 EndAUP;
        [FieldOffset(48)]
        public uint BranchIndex;
        [FieldOffset(52)]
        public uint StateFlags;
        [FieldOffset(56)]
        public uint SectorHash;
        [FieldOffset(60)]
        public uint GenerationDepth;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CoralIndirectArgsDTO
    {
        [FieldOffset(0)]
        public uint VertexCountPerInstance;
        [FieldOffset(4)]
        public uint InstanceCount;
        [FieldOffset(8)]
        public uint StartVertex;
        [FieldOffset(12)]
        public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralPaddedCounterDTO
    {
        [FieldOffset(0)]
        public int BranchCount;
        [FieldOffset(4)]
        public int InstructionCount;
        [FieldOffset(8)]
        public int DepthReached;
        [FieldOffset(12)]
        public int CollisionProxyCount;
        [FieldOffset(16)]
        public int RenderMatrixCount;
        [FieldOffset(20)]
        public int SyncPulseCount;
        [FieldOffset(24)]
        public int FaultFlags;
        [FieldOffset(28)]
        public uint StateHash;
        [FieldOffset(32)]
        public uint TelemetryCursor;
        [FieldOffset(36)]
        public uint CsvRuleCount;
        [FieldOffset(40)]
        public uint ActiveRuleCount;
        [FieldOffset(44)]
        public uint BinaryRuleCount;
        [FieldOffset(48)]
        public uint TipCount;
        [FieldOffset(52)]
        public uint PrunedCount;
        [FieldOffset(56)]
        public uint SpatialCellCount;
        [FieldOffset(60)]
        public float EffectiveQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralGpuSwayDTO
    {
        [FieldOffset(0)]
        public float4 FlowAndAmplitude;
        [FieldOffset(16)]
        public float4 BoundsAndDensity;
        [FieldOffset(32)]
        public float4 FaultAndFrame;
        [FieldOffset(48)]
        public uint SectorHash;
        [FieldOffset(52)]
        public uint StateHash;
        [FieldOffset(56)]
        public uint _pad0;
        [FieldOffset(60)]
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CoralSelfAuditResultDTO
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint SectorHash;
        [FieldOffset(8)]
        public uint Flags;
        [FieldOffset(12)]
        public uint LiveBranchCount;
        [FieldOffset(16)]
        public uint TipCount;
        [FieldOffset(20)]
        public uint OverlapPairCount;
        [FieldOffset(24)]
        public uint RenderMatrixCount;
        [FieldOffset(28)]
        public uint StateHash;
        [FieldOffset(32)]
        public float MaxOverlapDepth;
        [FieldOffset(36)]
        public float BranchUtilization;
        [FieldOffset(40)]
        public ulong _pad0;
        [FieldOffset(48)]
        public ulong _pad1;
        [FieldOffset(56)]
        public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CoralHzbTileDTO
    {
        [FieldOffset(0)]
        public float Depth01;
        [FieldOffset(4)]
        public uint TileX;
        [FieldOffset(8)]
        public uint TileY;
        [FieldOffset(12)]
        public uint Flags;
    }
}
