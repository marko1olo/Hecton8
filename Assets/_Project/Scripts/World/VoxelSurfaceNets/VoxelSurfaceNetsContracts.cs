using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.World.VoxelSurfaceNets
{
    internal static class VoxelSurfaceNetsContractsLayout
    {
        public const int VoxelVertexDTOStrideBytes = 32;
        public const int ChunkMeshingStateDTOStrideBytes = 64;
        public const int VoxelMeshingTuningDTOStrideBytes = 64;
        public const int VoxelMeshingTelemetryEntryStrideBytes = 64;
        public const int VoxelSurfaceAabbDTOStrideBytes = 64;
        public const int VoxelSurfaceModifiedSignalStrideBytes = 64;
        public const int VoxelSurfacePriorityDTOStrideBytes = 16;
        public const int VoxelSurfaceIndirectArgsDTOStrideBytes = 32;
        public const int MockVoxelDensityArrayStrideBytes = 48;
        public const int VoxelSurfacePhysicsBakeRequestDTOStrideBytes = 32;
        public const int VoxelSurfaceHzbTileDTOStrideBytes = 16;
    }

    public static class VoxelSurfaceNetsConstants
    {
        public const int ChunkResolution = 32;
        public const int DensityResolution = ChunkResolution + 2;
        public const int CellCount = ChunkResolution * ChunkResolution * ChunkResolution;
        public const int DensitySampleCount = DensityResolution * DensityResolution * DensityResolution;
        public const int MaxVertices = 65000;
        public const int MaxIndices = 196608;
        public const int MaxColliderVertices = 65000;
        public const int MaxColliderIndices = 196608;
        public const int MaxColliderCells = CellCount;
        public const int MaxRawDebugVertices = 12288;
        public const int MaxTrackedChunks = 256;
        public const int MaxModifiedSignals = 256;
        public const int MaxHzbTiles = 4096;
        public const int TelemetryFrames = 300;
        public const int CsvScratchBytes = 4096;
        public const int LookupCaseCount = 256;
        public const int IndirectArgsUintCount = 5;
        public const float Epsilon = 0.0001f;
        public const uint DumpMagic = 0x4D534847u;
        public const uint DumpEndianMarker = 0x01020304u;
        public const uint FaultSlowExtraction = 0x534C4F57u;
        public const uint FaultCapacity = 0x43415059u;
        public const uint FaultNaN = 0x4E414E21u;
    }

    public static class VoxelSurfaceNetsVaultBufferIds
    {
        public const BufferID Density = BufferID.ShinobuFluidCompartmentFront;
        public const BufferID Vertices = BufferID.ShinobuFluidCompartmentBack;
        public const BufferID Indices = BufferID.ShinobuFluidIntegrityState;
        public const BufferID CellVertexMap = BufferID.ShinobuFluidEdgeOffsets;
        public const BufferID States = BufferID.ShinobuFluidEdgeDestinations;
        public const BufferID Tuning = BufferID.ShinobuFluidEdgeFlags;
        public const BufferID TelemetryRing = BufferID.ShinobuFluidCompartmentCentroids;
        public const BufferID TelemetryCursor = BufferID.ShinobuFluidWaterlineShader;
        public const BufferID SurfaceEdgeMasks = BufferID.ShinobuFluidTuning;
        public const BufferID RawDebugVertices = BufferID.ShinobuFluidTelemetryRing;
        public const BufferID ChunkAabbs = BufferID.ShinobuFluidTelemetryCursor;
        public const BufferID ModifiedSignals = BufferID.ShinobuFluidBfsQueue;
        public const BufferID Priorities = BufferID.ShinobuFluidBfsVisited;
        public const BufferID IndirectArgs = BufferID.ShinobuFluidDeltaVolumes;
        public const BufferID MockDensityConfig = BufferID.ShinobuFluidFrameSummary;
        public const BufferID PhysicsBakeRequests = BufferID.ShinobuFluidCsvScratch;
        public const BufferID HzbTiles = BufferID.ShinobuFluidMockBreach;
        public const BufferID ColliderVertices = BufferID.ShinobuFluidCompartmentTelemetry;
        public const BufferID ColliderIndices = BufferID.ShinobuFluidEdgeConductivity;
        public const BufferID ColliderCellVertexMap = BufferID.ShinobuFluidTransferRemainders;
    }

    public enum VoxelMeshingStage : byte
    {
        Empty = 0,
        Dirty = 1,
        Extracting = 2,
        ReadyForUpload = 3,
        Uploading = 4,
        Uploaded = 5,
        Fault = 255
    }

    public static class VoxelMeshingFlags
    {
        public const byte Dirty = 1 << 0;
        public const byte ModifiedByLaser = 1 << 1;
        public const byte CapacityClamped = 1 << 2;
        public const byte SlowExtraction = 1 << 3;
        public const byte NonFinite = 1 << 4;
        public const byte RawDebugEnabled = 1 << 5;
        public const byte PhysicsBakePending = 1 << 6;
        public const byte GpuResourceInvalid = 1 << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelVertexDTOStrideBytes)]
    public struct VoxelVertexDTO
    {
        [FieldOffset(0)]
        public float3 Position;
        [FieldOffset(12)]
        public uint NormalPacked;
        [FieldOffset(16)]
        public uint TangentPacked;
        [FieldOffset(20)]
        public uint ColorPacked;
        [FieldOffset(24)]
        public float2 UV;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.ChunkMeshingStateDTOStrideBytes)]
    public struct ChunkMeshingStateDTO
    {
        [FieldOffset(0)]
        public double3 ChunkOriginAup;
        [FieldOffset(24)]
        public float3 BoundsCenterLocal;
        [FieldOffset(36)]
        public float VoxelSize;
        [FieldOffset(40)]
        public int VertexCount;
        [FieldOffset(44)]
        public int IndexCount;
        [FieldOffset(48)]
        public int RawDebugVertexCount;
        [FieldOffset(52)]
        public uint ChunkHash;
        [FieldOffset(56)]
        public uint Version;
        [FieldOffset(60)]
        public byte Stage;
        [FieldOffset(61)]
        public byte Flags;
        [FieldOffset(62)]
        public ushort Priority;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelMeshingTuningDTOStrideBytes)]
    public struct VoxelMeshingTuningDTO
    {
        [FieldOffset(0)]
        public ulong LastCsvWriteTicks;
        [FieldOffset(8)]
        public float GlobalQualityWeight;
        [FieldOffset(12)]
        public float IsoSurface;
        [FieldOffset(16)]
        public float DecimationAggression;
        [FieldOffset(20)]
        public float NormalSmoothingAngleDegrees;
        [FieldOffset(24)]
        public float VoxelSize;
        [FieldOffset(28)]
        public float BiomeBlendScale;
        [FieldOffset(32)]
        public float MaxExtractionMs;
        [FieldOffset(36)]
        public float DebugRawCapture01;
        [FieldOffset(40)]
        public int MaxChunksPerFrame;
        [FieldOffset(44)]
        public int ChunkResolution;
        [FieldOffset(48)]
        public uint Version;
        [FieldOffset(52)]
        public uint Flags;
        [FieldOffset(56)]
        public uint ForceRemeshVersion;
        [FieldOffset(60)]
        public uint LastCsvHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelMeshingTelemetryEntryStrideBytes)]
    public struct VoxelMeshingTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint ChunkHash;
        [FieldOffset(8)]
        public int VertexCount;
        [FieldOffset(12)]
        public int IndexCount;
        [FieldOffset(16)]
        public int ChunksMeshedThisFrame;
        [FieldOffset(20)]
        public float ExtractionComputeTimeMs;
        [FieldOffset(24)]
        public float GlobalQualityWeight;
        [FieldOffset(28)]
        public float DecimationRatio;
        [FieldOffset(32)]
        public float SamplingRatio;
        [FieldOffset(36)]
        public uint Flags;
        [FieldOffset(40)]
        public int RawDebugVertexCount;
        [FieldOffset(44)]
        public uint StateHash;
        [FieldOffset(48)]
        public uint DumpReason;
        [FieldOffset(52)]
        public uint _pad1;
        [FieldOffset(56)]
        public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelSurfaceAabbDTOStrideBytes)]
    public struct VoxelSurfaceAabbDTO
    {
        [FieldOffset(0)]
        public double3 CenterAup;
        [FieldOffset(24)]
        public float3 ExtentsLocal;
        [FieldOffset(36)]
        public uint ChunkHash;
        [FieldOffset(40)]
        public uint Version;
        [FieldOffset(44)]
        public byte VisibleFlags;
        [FieldOffset(45)]
        public byte Priority;
        [FieldOffset(46)]
        public ushort _pad0;
        [FieldOffset(48)]
        public ulong _pad1;
        [FieldOffset(56)]
        public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelSurfaceModifiedSignalStrideBytes)]
    public struct VoxelSurfaceModifiedSignal
    {
        [FieldOffset(0)]
        public double3 ChunkOriginAup;
        [FieldOffset(24)]
        public int3 ChunkCoord;
        [FieldOffset(36)]
        public uint ChunkHash;
        [FieldOffset(40)]
        public uint Version;
        [FieldOffset(44)]
        public byte Dirty;
        [FieldOffset(45)]
        public byte ForceHighPriority;
        [FieldOffset(46)]
        public ushort _pad0;
        [FieldOffset(48)]
        public ulong _pad1;
        [FieldOffset(56)]
        public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelSurfacePriorityDTOStrideBytes)]
    public struct VoxelSurfacePriorityDTO
    {
        [FieldOffset(0)]
        public float Score;
        [FieldOffset(4)]
        public int ChunkIndex;
        [FieldOffset(8)]
        public uint ChunkHash;
        [FieldOffset(12)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelSurfaceIndirectArgsDTOStrideBytes)]
    public struct VoxelSurfaceIndirectArgsDTO
    {
        [FieldOffset(0)]
        public uint IndexCountPerInstance;
        [FieldOffset(4)]
        public uint InstanceCount;
        [FieldOffset(8)]
        public uint StartIndex;
        [FieldOffset(12)]
        public uint BaseVertex;
        [FieldOffset(16)]
        public uint StartInstance;
        [FieldOffset(20)]
        public uint _pad0;
        [FieldOffset(24)]
        public uint _pad1;
        [FieldOffset(28)]
        public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.MockVoxelDensityArrayStrideBytes)]
    public partial struct MockVoxelDensityArray
    {
        [FieldOffset(0)]
        public int3 Dimensions;
        [FieldOffset(12)]
        public float VoxelSize;
        [FieldOffset(16)]
        public float3 CenterLocal;
        [FieldOffset(28)]
        public float Radius;
        [FieldOffset(32)]
        public float ShellThickness;
        [FieldOffset(36)]
        public uint Seed;
        [FieldOffset(40)]
        public uint Flags;
        [FieldOffset(44)]
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelSurfacePhysicsBakeRequestDTOStrideBytes)]
    public struct VoxelSurfacePhysicsBakeRequestDTO
    {
        [FieldOffset(0)]
        public int MeshId;
        [FieldOffset(4)]
        public int ChunkIndex;
        [FieldOffset(8)]
        public uint ChunkHash;
        [FieldOffset(12)]
        public uint Version;
        [FieldOffset(16)]
        public byte Pending;
        [FieldOffset(17)]
        public byte Completed;
        [FieldOffset(18)]
        public ushort _pad0;
        [FieldOffset(20)]
        public int ColliderIndexCount;
        [FieldOffset(24)]
        public int ColliderVertexCount;
        [FieldOffset(28)]
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = VoxelSurfaceNetsContractsLayout.VoxelSurfaceHzbTileDTOStrideBytes)]
    public struct VoxelSurfaceHzbTileDTO
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
