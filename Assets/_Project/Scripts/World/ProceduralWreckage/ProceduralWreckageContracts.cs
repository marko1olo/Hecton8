using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralWreckage
{
    public static class ProceduralWreckageConstants
    {
        public const int GridResolutionX = 16;
        public const int GridResolutionY = 4;
        public const int GridResolutionZ = 16;
        public const int MaxGridCells = GridResolutionX * GridResolutionY * GridResolutionZ;
        public const int MaxModuleRules = 16;
        public const int MaxWreckNodes = 1024;
        public const int MaxDebrisNodes = 4096;
        public const int MaxRenderMatrices = MaxWreckNodes + MaxDebrisNodes;
        public const int MaxLootRequests = 512;
        public const int MaxCollisionProxies = 1024;
        public const int MaxDebugCells = MaxGridCells;
        public const int MaxHzbTiles = 4096;
        public const int TelemetryFrames = 300;
        public const int CsvScratchBytes = 32768;
        public const int IndirectArgsUintCount = 4;
        public const float Epsilon = 0.0001f;
        public const uint DumpMagic = 0x57464357u;
        public const uint DumpEndianMarker = 0x01020304u;
        public const uint FaultNoRules = 0x4E4F5255u;
        public const uint FaultContradiction = 0x57464321u;
        public const uint FaultCapacity = 0x43415021u;
        public const uint FaultNonFinite = 0x4E414E21u;
        public const uint FaultOpenHull = 0x48554C4Cu;
    }

    public static class ProceduralWreckageVaultBufferIds
    {
        public const BufferID Rules = (BufferID)70840;
        public const BufferID Grid = (BufferID)70841;
        public const BufferID Nodes = (BufferID)70842;
        public const BufferID DebrisNodes = (BufferID)70843;
        public const BufferID RenderMatrices = (BufferID)70844;
        public const BufferID IndirectArgs = (BufferID)70845;
        public const BufferID SectorTriggers = (BufferID)70846;
        public const BufferID LootRequests = (BufferID)70847;
        public const BufferID CollisionProxies = (BufferID)70848;
        public const BufferID TelemetryRing = (BufferID)70849;
        public const BufferID TelemetryCursor = (BufferID)70850;
        public const BufferID Tuning = (BufferID)70851;
        public const BufferID CsvScratch = (BufferID)70852;
        public const BufferID Counters = (BufferID)70853;
        public const BufferID DebugCells = (BufferID)70854;
        public const BufferID GpuScalars = (BufferID)70855;
        public const BufferID SelfAudit = (BufferID)70856;
        public const BufferID HzbTiles = (BufferID)70857;
    }

    public static class WreckageDirections
    {
        public const int North = 0;
        public const int East = 1;
        public const int South = 2;
        public const int West = 3;
        public const int Top = 4;
        public const int Bottom = 5;
    }

    public static class WreckageNodeFlags
    {
        public const uint Alive = 1u << 0;
        public const uint Structural = 1u << 1;
        public const uint Terminus = 1u << 2;
        public const uint Sheared = 1u << 3;
        public const uint Debris = 1u << 4;
        public const uint Culled = 1u << 5;
        public const uint NonFiniteFallback = 1u << 6;
    }

    public static class WreckageRuleFlags
    {
        public const uint Empty = 1u << 0;
        public const uint Structural = 1u << 1;
        public const uint TerminusEligible = 1u << 2;
        public const uint DebrisSource = 1u << 3;
        public const uint EssentialSilhouette = 1u << 4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageRuleDTO
    {
        [FieldOffset(0)]
        public uint ModuleHash;
        [FieldOffset(4)]
        public ushort SocketNorth;
        [FieldOffset(6)]
        public ushort SocketEast;
        [FieldOffset(8)]
        public ushort SocketSouth;
        [FieldOffset(10)]
        public ushort SocketWest;
        [FieldOffset(12)]
        public ushort SocketTop;
        [FieldOffset(14)]
        public ushort SocketBottom;
        [FieldOffset(16)]
        public float3 BoundsExtents;
        [FieldOffset(28)]
        public float Weight;
        [FieldOffset(32)]
        public uint PrefabHash;
        [FieldOffset(36)]
        public uint Flags;
        [FieldOffset(40)]
        public byte ModuleId;
        [FieldOffset(41)]
        public byte DrawPriority;
        [FieldOffset(42)]
        public ushort _pad0;
        [FieldOffset(44)]
        public uint _pad1;
        [FieldOffset(48)]
        public ulong _pad2;
        [FieldOffset(56)]
        public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct WreckageGridCellDTO
    {
        [FieldOffset(0)]
        public ushort PossibleModuleMask;
        [FieldOffset(2)]
        public byte CollapsedModuleId;
        [FieldOffset(3)]
        public byte SocketConstraints;
        [FieldOffset(4)]
        public float Entropy;
        [FieldOffset(8)]
        public uint ParentIndex;
        [FieldOffset(12)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct WreckageNodeDTO
    {
        [FieldOffset(0)]
        public float4x4 LocalMatrix;
        [FieldOffset(64)]
        public uint PrefabHash;
        [FieldOffset(68)]
        public uint StateFlags;
        [FieldOffset(72)]
        public double3 SectorAUP;
        [FieldOffset(96)]
        public float3 BoundsExtents;
        [FieldOffset(108)]
        public float BoundsRadius;
        [FieldOffset(112)]
        public uint SectorHash;
        [FieldOffset(116)]
        public uint ModuleId;
        [FieldOffset(120)]
        public uint GraphDegree;
        [FieldOffset(124)]
        public uint StableId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageSectorTriggerDTO
    {
        [FieldOffset(0)]
        public double3 RootAUP;
        [FieldOffset(24)]
        public uint SectorHash;
        [FieldOffset(28)]
        public uint Seed;
        [FieldOffset(32)]
        public int3 GridDims;
        [FieldOffset(44)]
        public float CellSize;
        [FieldOffset(48)]
        public float GlobalQualityWeight;
        [FieldOffset(52)]
        public uint SimulationFrame;
        [FieldOffset(56)]
        public uint Flags;
        [FieldOffset(60)]
        public uint BacktrackLimit;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageTuningDTO
    {
        [FieldOffset(0)]
        public float GlobalQualityWeight;
        [FieldOffset(4)]
        public float ShearSeverity;
        [FieldOffset(8)]
        public float DebrisScatterRadius;
        [FieldOffset(12)]
        public float VisibilityDistanceMin;
        [FieldOffset(16)]
        public float VisibilityDistanceMax;
        [FieldOffset(20)]
        public uint BacktrackLimit;
        [FieldOffset(24)]
        public int MaxNodes;
        [FieldOffset(28)]
        public int MaxDebris;
        [FieldOffset(32)]
        public float CellSize;
        [FieldOffset(36)]
        public float MaxGenerationMs;
        [FieldOffset(40)]
        public uint Version;
        [FieldOffset(44)]
        public uint LastCsvHash;
        [FieldOffset(48)]
        public ulong LastCsvWriteTicks;
        [FieldOffset(56)]
        public uint Flags;
        [FieldOffset(60)]
        public uint SeedSalt;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LootSpawnRequestDTO
    {
        [FieldOffset(0)]
        public double3 AUP;
        [FieldOffset(24)]
        public uint SectorHash;
        [FieldOffset(28)]
        public uint LootTableHash;
        [FieldOffset(32)]
        public uint NodeIndex;
        [FieldOffset(36)]
        public uint Quantity;
        [FieldOffset(40)]
        public uint Flags;
        [FieldOffset(44)]
        public uint StableId;
        [FieldOffset(48)]
        public ulong _pad0;
        [FieldOffset(56)]
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageBoxColliderDTO
    {
        [FieldOffset(0)]
        public double3 CenterAUP;
        [FieldOffset(24)]
        public float3 Extents;
        [FieldOffset(36)]
        public uint ModuleIndex;
        [FieldOffset(40)]
        public float4 Rotation;
        [FieldOffset(56)]
        public uint Flags;
        [FieldOffset(60)]
        public uint SectorHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageGenerationTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 RootAUP;
        [FieldOffset(24)]
        public uint Frame;
        [FieldOffset(28)]
        public uint SectorHash;
        [FieldOffset(32)]
        public int CollapsedModules;
        [FieldOffset(36)]
        public int BacktrackIterations;
        [FieldOffset(40)]
        public float EstimatedComputeMs;
        [FieldOffset(44)]
        public float GlobalQualityWeight;
        [FieldOffset(48)]
        public uint StateHash;
        [FieldOffset(52)]
        public uint FaultFlags;
        [FieldOffset(56)]
        public uint RenderedModules;
        [FieldOffset(60)]
        public uint DebrisCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageDebugCellDTO
    {
        [FieldOffset(0)]
        public double3 CenterAUP;
        [FieldOffset(24)]
        public float3 Extents;
        [FieldOffset(36)]
        public uint SectorHash;
        [FieldOffset(40)]
        public uint CellIndex;
        [FieldOffset(44)]
        public byte State;
        [FieldOffset(45)]
        public byte ModuleId;
        [FieldOffset(46)]
        public ushort _pad0;
        [FieldOffset(48)]
        public ulong _pad1;
        [FieldOffset(56)]
        public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct WreckageIndirectArgsDTO
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
    public struct WreckagePaddedCounterDTO
    {
        [FieldOffset(0)]
        public int NodeCount;
        [FieldOffset(4)]
        public int DebrisCount;
        [FieldOffset(8)]
        public int LootCount;
        [FieldOffset(12)]
        public int CollisionProxyCount;
        [FieldOffset(16)]
        public int RenderMatrixCount;
        [FieldOffset(20)]
        public int BacktrackCount;
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
        public uint _pad1;
        [FieldOffset(48)]
        public ulong _pad2;
        [FieldOffset(56)]
        public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WreckageGpuScalarDTO
    {
        [FieldOffset(0)]
        public float4 CausticRustSiltQuality;
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
    public struct WreckageSelfAuditResultDTO
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint SectorHash;
        [FieldOffset(8)]
        public uint Flags;
        [FieldOffset(12)]
        public uint OpenHullNodeCount;
        [FieldOffset(16)]
        public uint OverlapPairCount;
        [FieldOffset(20)]
        public uint LiveNodeCount;
        [FieldOffset(24)]
        public uint RenderMatrixCount;
        [FieldOffset(28)]
        public uint StateHash;
        [FieldOffset(32)]
        public float MaxOverlapDepth;
        [FieldOffset(36)]
        public float ClosedHullRatio;
        [FieldOffset(40)]
        public ulong _pad0;
        [FieldOffset(48)]
        public ulong _pad1;
        [FieldOffset(56)]
        public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct WreckageHzbTileDTO
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
