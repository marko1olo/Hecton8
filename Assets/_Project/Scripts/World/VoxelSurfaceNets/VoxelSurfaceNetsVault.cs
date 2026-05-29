using System;
using System.IO;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.VoxelSurfaceNets
{
    public struct VoxelSurfaceNetsVaultHandles
    {
        public VaultGenerationHandle<sbyte> Density;
        public VaultGenerationHandle<VoxelVertexDTO> Vertices;
        public VaultGenerationHandle<uint> Indices;
        public VaultGenerationHandle<int> CellVertexMap;
        public VaultGenerationHandle<ChunkMeshingStateDTO> States;
        public VaultGenerationHandle<VoxelMeshingTuningDTO> Tuning;
        public VaultGenerationHandle<VoxelMeshingTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<byte> CsvScratch;
        public VaultGenerationHandle<uint> SurfaceEdgeMasks;
        public VaultGenerationHandle<float3> RawDebugVertices;
        public VaultGenerationHandle<VoxelSurfaceAabbDTO> ChunkAabbs;
        public VaultGenerationHandle<VoxelSurfaceModifiedSignal> ModifiedSignals;
        public VaultGenerationHandle<VoxelSurfacePriorityDTO> Priorities;
        public VaultGenerationHandle<VoxelSurfaceIndirectArgsDTO> IndirectArgs;
        public VaultGenerationHandle<MockVoxelDensityArray> MockDensityConfig;
        public VaultGenerationHandle<VoxelSurfacePhysicsBakeRequestDTO> PhysicsBakeRequests;
        public VaultGenerationHandle<VoxelSurfaceHzbTileDTO> HzbTiles;

        public bool IsCreated()
        {
            return IsHandleValid(in Density) &&
                   IsHandleValid(in Vertices) &&
                   IsHandleValid(in Indices) &&
                   IsHandleValid(in CellVertexMap) &&
                   IsHandleValid(in States) &&
                   IsHandleValid(in Tuning) &&
                   IsHandleValid(in TelemetryRing) &&
                   IsHandleValid(in TelemetryCursor) &&
                   IsHandleValid(in CsvScratch) &&
                   IsHandleValid(in SurfaceEdgeMasks) &&
                   IsHandleValid(in RawDebugVertices) &&
                   IsHandleValid(in ChunkAabbs) &&
                   IsHandleValid(in ModifiedSignals) &&
                   IsHandleValid(in Priorities) &&
                   IsHandleValid(in IndirectArgs) &&
                   IsHandleValid(in MockDensityConfig) &&
                   IsHandleValid(in PhysicsBakeRequests) &&
                   IsHandleValid(in HzbTiles);
        }

        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }
    }

    public struct VoxelSurfaceNetsVaultBuffers
    {
        public IDataVault Vault;
        public VoxelSurfaceNetsVaultHandles Handles;

        public NativeArray<sbyte> Density => ResolveView(Vault, in Handles.Density);
        public NativeArray<VoxelVertexDTO> Vertices => ResolveView(Vault, in Handles.Vertices);
        public NativeArray<uint> Indices => ResolveView(Vault, in Handles.Indices);
        public NativeArray<int> CellVertexMap => ResolveView(Vault, in Handles.CellVertexMap);
        public NativeArray<ChunkMeshingStateDTO> States => ResolveView(Vault, in Handles.States);
        public NativeArray<VoxelMeshingTuningDTO> Tuning => ResolveView(Vault, in Handles.Tuning);
        public NativeArray<VoxelMeshingTelemetryEntry> TelemetryRing => ResolveView(Vault, in Handles.TelemetryRing);
        public NativeArray<int> TelemetryCursor => ResolveView(Vault, in Handles.TelemetryCursor);
        public NativeArray<byte> CsvScratch => ResolveView(Vault, in Handles.CsvScratch);
        public NativeArray<uint> SurfaceEdgeMasks => ResolveView(Vault, in Handles.SurfaceEdgeMasks);
        public NativeArray<float3> RawDebugVertices => ResolveView(Vault, in Handles.RawDebugVertices);
        public NativeArray<VoxelSurfaceAabbDTO> ChunkAabbs => ResolveView(Vault, in Handles.ChunkAabbs);
        public NativeArray<VoxelSurfaceModifiedSignal> ModifiedSignals => ResolveView(Vault, in Handles.ModifiedSignals);
        public NativeArray<VoxelSurfacePriorityDTO> Priorities => ResolveView(Vault, in Handles.Priorities);
        public NativeArray<VoxelSurfaceIndirectArgsDTO> IndirectArgs => ResolveView(Vault, in Handles.IndirectArgs);
        public NativeArray<MockVoxelDensityArray> MockDensityConfig => ResolveView(Vault, in Handles.MockDensityConfig);
        public NativeArray<VoxelSurfacePhysicsBakeRequestDTO> PhysicsBakeRequests => ResolveView(Vault, in Handles.PhysicsBakeRequests);
        public NativeArray<VoxelSurfaceHzbTileDTO> HzbTiles => ResolveView(Vault, in Handles.HzbTiles);

        public bool IsCreated()
        {
            return Density.IsCreated &&
                   Vertices.IsCreated &&
                   Indices.IsCreated &&
                   CellVertexMap.IsCreated &&
                   States.IsCreated &&
                   Tuning.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   CsvScratch.IsCreated &&
                   SurfaceEdgeMasks.IsCreated &&
                   RawDebugVertices.IsCreated &&
                   ChunkAabbs.IsCreated &&
                   ModifiedSignals.IsCreated &&
                   Priorities.IsCreated &&
                   IndirectArgs.IsCreated &&
                   MockDensityConfig.IsCreated &&
                   PhysicsBakeRequests.IsCreated &&
                   HzbTiles.IsCreated;
        }

        private static NativeArray<T> ResolveView<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                buffer.IsCreated)
            {
                return buffer;
            }

            return default;
        }
    }

    public struct VoxelSurfaceNetsGpuUploadSourceLease
    {
        public IDataVault Vault;
        public byte LockedMask;
        public ulong MutationGuardMask;

        public bool IsCreated()
        {
            return Vault != null && (LockedMask != 0 || MutationGuardMask != 0UL);
        }
    }

    public struct VoxelSurfaceNetsJobBufferLease
    {
        public IDataVault Vault;
        public VoxelSurfaceNetsVaultHandles Handles;
        public uint LockedMask;
        public uint WriteMask;
        public ulong MutationGuardMask;

        public bool IsCreated()
        {
            return Vault != null && (LockedMask != 0u || WriteMask != 0u || MutationGuardMask != 0UL);
        }
    }

    public static unsafe class VoxelSurfaceNetsVault
    {
        private const int DumpVersion = 1;
        private const string DumpFileName = "Dump_MESH_SURGEON.bin";
        private const string AgentDumpFileName = "Dump_1304_Voxel.bin";
        private const string CsvFileName = "meshing_profiles.csv";
        private const byte GpuUploadVerticesLock = 1 << 0;
        private const byte GpuUploadIndicesLock = 1 << 1;
        private const byte GpuUploadIndirectArgsLock = 1 << 2;
        private const uint JobDensityLock = 1u << 0;
        private const uint JobVerticesLock = 1u << 1;
        private const uint JobIndicesLock = 1u << 2;
        private const uint JobCellVertexMapLock = 1u << 3;
        private const uint JobStatesLock = 1u << 4;
        private const uint JobTuningLock = 1u << 5;
        private const uint JobSurfaceEdgeMasksLock = 1u << 6;
        private const uint JobTelemetryRingLock = 1u << 7;
        private const uint JobTelemetryCursorLock = 1u << 8;
        private const uint JobRawDebugVerticesLock = 1u << 9;
        private const uint JobIndirectArgsLock = 1u << 10;
        private const uint JobChunkAabbsLock = 1u << 11;
        private const uint JobPrioritiesLock = 1u << 12;
        private const uint JobHzbTilesLock = 1u << 13;
        private const uint JobMockDensityConfigLock = 1u << 14;
        private static readonly ulong GpuUploadSourceMutationGuardMask =
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Vertices) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Indices) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.IndirectArgs);
        private static readonly ulong MockDensityJobMutationGuardMask =
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Density) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Tuning) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.MockDensityConfig);
        private static readonly ulong ExtractionJobMutationGuardMask =
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Density) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Vertices) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Indices) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.CellVertexMap) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.States) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Tuning) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.SurfaceEdgeMasks) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryRing) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryCursor) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.RawDebugVertices) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.IndirectArgs);
        private static readonly ulong HzbCullJobMutationGuardMask =
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.ChunkAabbs) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Priorities) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.HzbTiles);
        private static readonly ulong TelemetryDumpMutationGuardMask =
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryRing) |
            VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryCursor);
        private static readonly uint _globalQualityHash = HashAsciiLiteral("global_quality_weight");
        private static readonly uint _isoSurfaceHash = HashAsciiLiteral("iso_surface_threshold");
        private static readonly uint _normalAngleHash = HashAsciiLiteral("normal_smoothing_angle");
        private static readonly uint _decimationHash = HashAsciiLiteral("decimation_aggression");
        private static readonly uint _chunksPerFrameHash = HashAsciiLiteral("max_chunks_per_frame");
        private static readonly uint _debugRawHash = HashAsciiLiteral("show_raw_extraction");

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        public static bool TryResolve(IDataVault vault, out VoxelSurfaceNetsVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryResolveExisting(vault, out handles))
                    return false;

                if (TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers lockedBuffers))
                    HydrateDefaultsIfNeeded(lockedBuffers);

                return handles.IsCreated();
            }

            handles.Density = vault.EnsureGenerationHandle<sbyte>(
                VoxelSurfaceNetsVaultBufferIds.Density,
                VoxelSurfaceNetsConstants.DensitySampleCount,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Vertices = vault.EnsureGenerationHandle<VoxelVertexDTO>(
                VoxelSurfaceNetsVaultBufferIds.Vertices,
                VoxelSurfaceNetsConstants.MaxVertices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Indices = vault.EnsureGenerationHandle<uint>(
                VoxelSurfaceNetsVaultBufferIds.Indices,
                VoxelSurfaceNetsConstants.MaxIndices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.CellVertexMap = vault.EnsureGenerationHandle<int>(
                VoxelSurfaceNetsVaultBufferIds.CellVertexMap,
                VoxelSurfaceNetsConstants.CellCount,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.States = vault.EnsureGenerationHandle<ChunkMeshingStateDTO>(
                VoxelSurfaceNetsVaultBufferIds.States,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Tuning = vault.EnsureGenerationHandle<VoxelMeshingTuningDTO>(
                VoxelSurfaceNetsVaultBufferIds.Tuning,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<VoxelMeshingTelemetryEntry>(
                VoxelSurfaceNetsVaultBufferIds.TelemetryRing,
                VoxelSurfaceNetsConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                VoxelSurfaceNetsVaultBufferIds.TelemetryCursor,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.EnsureGenerationHandle<byte>(
                VoxelSurfaceNetsVaultBufferIds.CsvScratch,
                VoxelSurfaceNetsConstants.CsvScratchBytes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.SurfaceEdgeMasks = vault.EnsureGenerationHandle<uint>(
                VoxelSurfaceNetsVaultBufferIds.SurfaceEdgeMasks,
                VoxelSurfaceNetsConstants.LookupCaseCount,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.RawDebugVertices = vault.EnsureGenerationHandle<float3>(
                VoxelSurfaceNetsVaultBufferIds.RawDebugVertices,
                VoxelSurfaceNetsConstants.MaxRawDebugVertices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.ChunkAabbs = vault.EnsureGenerationHandle<VoxelSurfaceAabbDTO>(
                VoxelSurfaceNetsVaultBufferIds.ChunkAabbs,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.ModifiedSignals = vault.EnsureGenerationHandle<VoxelSurfaceModifiedSignal>(
                VoxelSurfaceNetsVaultBufferIds.ModifiedSignals,
                VoxelSurfaceNetsConstants.MaxModifiedSignals,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Priorities = vault.EnsureGenerationHandle<VoxelSurfacePriorityDTO>(
                VoxelSurfaceNetsVaultBufferIds.Priorities,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.IndirectArgs = vault.EnsureGenerationHandle<VoxelSurfaceIndirectArgsDTO>(
                VoxelSurfaceNetsVaultBufferIds.IndirectArgs,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.MockDensityConfig = vault.EnsureGenerationHandle<MockVoxelDensityArray>(
                VoxelSurfaceNetsVaultBufferIds.MockDensityConfig,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.PhysicsBakeRequests = vault.EnsureGenerationHandle<VoxelSurfacePhysicsBakeRequestDTO>(
                VoxelSurfaceNetsVaultBufferIds.PhysicsBakeRequests,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.HzbTiles = vault.EnsureGenerationHandle<VoxelSurfaceHzbTileDTO>(
                VoxelSurfaceNetsVaultBufferIds.HzbTiles,
                VoxelSurfaceNetsConstants.MaxHzbTiles,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);

            if (!handles.IsCreated())
                return false;

            if (TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers buffers))
                HydrateDefaultsIfNeeded(buffers);

            return true;
        }

        public static bool TryResolveExisting(IDataVault vault, out VoxelSurfaceNetsVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            return vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.Density, out handles.Density) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.Vertices, out handles.Vertices) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.Indices, out handles.Indices) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.CellVertexMap, out handles.CellVertexMap) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.States, out handles.States) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.CsvScratch, out handles.CsvScratch) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.SurfaceEdgeMasks, out handles.SurfaceEdgeMasks) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.RawDebugVertices, out handles.RawDebugVertices) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.ChunkAabbs, out handles.ChunkAabbs) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.ModifiedSignals, out handles.ModifiedSignals) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.Priorities, out handles.Priorities) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.IndirectArgs, out handles.IndirectArgs) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.MockDensityConfig, out handles.MockDensityConfig) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.PhysicsBakeRequests, out handles.PhysicsBakeRequests) &&
                   vault.TryGetGenerationHandle(VoxelSurfaceNetsVaultBufferIds.HzbTiles, out handles.HzbTiles);
        }

        public static bool TryResolveViews(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, out VoxelSurfaceNetsVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            buffers.Vault = vault;
            buffers.Handles = handles;
            return buffers.IsCreated();
        }

        public static bool TryAcquireStatesWriteLock(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out NativeArray<ChunkMeshingStateDTO> states)
        {
            states = default;
            if (buffers.Vault == null ||
                buffers.Handles.States.BufferID == 0u ||
                !buffers.Vault.TryAcquireWriteLock(in buffers.Handles.States, SystemID.WorldStreaming, out states))
            {
                return false;
            }

            if (states.IsCreated)
                return true;

            buffers.Vault.ReleaseWriteLock(in buffers.Handles.States, SystemID.WorldStreaming);
            states = default;
            return false;
        }

        public static void ReleaseStatesWriteLock(in VoxelSurfaceNetsVaultBuffers buffers)
        {
            if (buffers.Vault != null && buffers.Handles.States.BufferID != 0u)
                buffers.Vault.ReleaseWriteLock(in buffers.Handles.States, SystemID.WorldStreaming);
        }

        public static bool TryAcquireGpuUploadSourceLease(
            in VoxelSurfaceNetsVaultBuffers buffers,
            int vertexCount,
            int indexCount,
            out VoxelSurfaceNetsGpuUploadSourceLease lease,
            out NativeArray<VoxelVertexDTO> vertices,
            out NativeArray<uint> indices,
            out NativeArray<VoxelSurfaceIndirectArgsDTO> indirectArgs)
        {
            lease = default;
            vertices = default;
            indices = default;
            indirectArgs = default;
            IDataVault vault = buffers.Vault;
            if (vault == null || vertexCount <= 0 || indexCount <= 0)
                return false;

            if (!vault.TryAcquireMutationGuard(GpuUploadSourceMutationGuardMask))
                return false;

            lease.Vault = vault;
            lease.LockedMask = (byte)(GpuUploadVerticesLock | GpuUploadIndicesLock | GpuUploadIndirectArgsLock);
            lease.MutationGuardMask = GpuUploadSourceMutationGuardMask;

            if (!vault.TryResolveHandle(in buffers.Handles.Vertices, out vertices) ||
                !vault.TryResolveHandle(in buffers.Handles.Indices, out indices) ||
                !vault.TryResolveHandle(in buffers.Handles.IndirectArgs, out indirectArgs) ||
                !vertices.IsCreated ||
                !indices.IsCreated ||
                !indirectArgs.IsCreated ||
                vertices.Length < vertexCount ||
                indices.Length < indexCount ||
                indirectArgs.Length <= 0)
            {
                vertices = default;
                indices = default;
                indirectArgs = default;
                ReleaseGpuUploadSourceLease(ref lease);
                return false;
            }

            return true;
        }

        public static void ReleaseGpuUploadSourceLease(ref VoxelSurfaceNetsGpuUploadSourceLease lease)
        {
            IDataVault vault = lease.Vault;
            if (vault != null && lease.MutationGuardMask != 0UL)
                vault.ReleaseMutationGuard(lease.MutationGuardMask);

            lease = default;
        }

        public static bool TryAcquireMockDensityJobLease(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            lease = default;
            IDataVault vault = buffers.Vault;
            if (vault == null)
                return false;

            if (!TryAcquireJobBufferLeaseGuard(
                    vault,
                    in buffers.Handles,
                    MockDensityJobMutationGuardMask,
                    JobTuningLock | JobMockDensityConfigLock,
                    JobDensityLock,
                    out lease))
                return false;

            return true;
        }

        public static bool TryAcquireExtractionJobLease(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            lease = default;
            IDataVault vault = buffers.Vault;
            if (vault == null)
                return false;

            if (!TryAcquireJobBufferLeaseGuard(
                    vault,
                    in buffers.Handles,
                    ExtractionJobMutationGuardMask,
                    JobDensityLock | JobTuningLock | JobSurfaceEdgeMasksLock,
                    JobVerticesLock | JobIndicesLock | JobCellVertexMapLock | JobStatesLock | JobTelemetryRingLock | JobTelemetryCursorLock | JobRawDebugVerticesLock | JobIndirectArgsLock,
                    out lease))
                return false;

            return true;
        }

        public static bool TryAcquireHzbCullJobLease(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            lease = default;
            IDataVault vault = buffers.Vault;
            if (vault == null)
                return false;

            if (!TryAcquireJobBufferLeaseGuard(
                    vault,
                    in buffers.Handles,
                    HzbCullJobMutationGuardMask,
                    JobHzbTilesLock,
                    JobChunkAabbsLock | JobPrioritiesLock,
                    out lease))
                return false;

            return true;
        }

        public static void ReleaseJobBufferLease(ref VoxelSurfaceNetsJobBufferLease lease)
        {
            IDataVault vault = lease.Vault;
            if (vault != null && lease.MutationGuardMask != 0UL)
                vault.ReleaseMutationGuard(lease.MutationGuardMask);

            lease = default;
        }

        private static bool TryAcquireJobBufferLeaseGuard(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles,
            ulong mutationGuardMask,
            uint lockedMask,
            uint writeMask,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            lease = default;
            if (vault == null || mutationGuardMask == 0UL)
                return false;

            if (!vault.TryAcquireMutationGuard(mutationGuardMask))
                return false;

            lease.Vault = vault;
            lease.Handles = handles;
            lease.LockedMask = lockedMask;
            lease.WriteMask = writeMask;
            lease.MutationGuardMask = mutationGuardMask;
            return true;
        }

        private static bool TryCreateMockDensityJob(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out GenerateMockVoxelDensitySphereJob job,
            out int scheduleLength)
        {
            job = default;
            scheduleLength = 0;
            NativeArray<sbyte> density = buffers.Density;
            NativeArray<MockVoxelDensityArray> mockDensity = buffers.MockDensityConfig;
            NativeArray<VoxelMeshingTuningDTO> tuningBuffer = buffers.Tuning;
            if (!density.IsCreated || !mockDensity.IsCreated || mockDensity.Length <= 0)
                return false;

            VoxelMeshingTuningDTO tuning = tuningBuffer.IsCreated && tuningBuffer.Length > 0
                ? SanitizeTuning(tuningBuffer[0])
                : VoxelSurfaceNetsDefaults.BuildDefaultTuning();

            job.Densities = density;
            job.Config = SanitizeMockDensity(mockDensity[0]);
            job.GlobalQualityWeight = tuning.GlobalQualityWeight;
            scheduleLength = math.min(density.Length, VoxelSurfaceNetsConstants.DensitySampleCount);
            return scheduleLength > 0;
        }

        [Obsolete("Use TryScheduleMockDensityPinned and release the returned VoxelSurfaceNetsJobBufferLease after the JobHandle completes.", false)]
        public static bool TryScheduleMockDensity(
            in VoxelSurfaceNetsVaultBuffers buffers,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            return false;
        }

        public static bool TryScheduleMockDensityPinned(
            in VoxelSurfaceNetsVaultBuffers buffers,
            JobHandle inputDependency,
            out JobHandle outputDependency,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            outputDependency = inputDependency;
            lease = default;
            if (!TryAcquireMockDensityJobLease(in buffers, out lease))
                return false;

            if (!TryCreateMockDensityJob(in buffers, out GenerateMockVoxelDensitySphereJob job, out int scheduleLength))
            {
                ReleaseJobBufferLease(ref lease);
                return false;
            }

            outputDependency = job.Schedule(scheduleLength, 64, inputDependency);
            return true;
        }

        private static bool TryCreateExtractionJob(
            in VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            uint frame,
            out SurfaceNetExtractionJob job)
        {
            job = default;
            NativeArray<sbyte> densities = buffers.Density;
            NativeArray<VoxelVertexDTO> vertices = buffers.Vertices;
            NativeArray<uint> indices = buffers.Indices;
            NativeArray<int> cellVertexMap = buffers.CellVertexMap;
            NativeArray<ChunkMeshingStateDTO> states = buffers.States;
            NativeArray<VoxelMeshingTuningDTO> tuning = buffers.Tuning;
            NativeArray<uint> surfaceEdgeMasks = buffers.SurfaceEdgeMasks;
            NativeArray<VoxelMeshingTelemetryEntry> telemetryRing = buffers.TelemetryRing;
            NativeArray<int> telemetryCursor = buffers.TelemetryCursor;
            NativeArray<float3> rawDebugVertices = buffers.RawDebugVertices;
            NativeArray<VoxelSurfaceIndirectArgsDTO> indirectArgs = buffers.IndirectArgs;
            if (!densities.IsCreated ||
                !vertices.IsCreated ||
                !indices.IsCreated ||
                !cellVertexMap.IsCreated ||
                !states.IsCreated ||
                !tuning.IsCreated ||
                !surfaceEdgeMasks.IsCreated ||
                !telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                !rawDebugVertices.IsCreated ||
                !indirectArgs.IsCreated)
            {
                return false;
            }

            job.Densities = densities;
            job.Vertices = vertices;
            job.Indices = indices;
            job.CellVertexMap = cellVertexMap;
            job.States = states;
            job.Tuning = tuning;
            job.SurfaceEdgeMasks = surfaceEdgeMasks;
            job.TelemetryRing = telemetryRing;
            job.TelemetryCursor = telemetryCursor;
            job.RawDebugVertices = rawDebugVertices;
            job.IndirectArgs = indirectArgs;
            job.ChunkIndex = chunkIndex;
            job.Frame = frame;
            return true;
        }

        [Obsolete("Use TryScheduleExtractionPinned and release the returned VoxelSurfaceNetsJobBufferLease after the JobHandle completes.", false)]
        public static bool TryScheduleExtraction(
            in VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            uint frame,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            return false;
        }

        public static bool TryScheduleExtractionPinned(
            in VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            uint frame,
            JobHandle inputDependency,
            out JobHandle outputDependency,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            outputDependency = inputDependency;
            lease = default;
            if (!TryAcquireExtractionJobLease(in buffers, out lease))
                return false;

            if (!ShouldEvaluateFrame(in buffers, chunkIndex, frame))
            {
                ReleaseJobBufferLease(ref lease);
                return false;
            }

            if (!TryCreateExtractionJob(in buffers, chunkIndex, frame, out SurfaceNetExtractionJob job))
            {
                ReleaseJobBufferLease(ref lease);
                return false;
            }

            outputDependency = job.Schedule(inputDependency);
            return true;
        }

        [Obsolete("Use TryScheduleHzbCullPinned and release the returned VoxelSurfaceNetsJobBufferLease after the JobHandle completes.", false)]
        public static bool TryScheduleHzbCull(
            in VoxelSurfaceNetsVaultBuffers buffers,
            in float4x4 cameraRelativeViewProjection,
            double3 cameraAup,
            int hzbWidth,
            int hzbHeight,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            return false;
        }

        public static bool TryScheduleHzbCullPinned(
            in VoxelSurfaceNetsVaultBuffers buffers,
            in float4x4 cameraRelativeViewProjection,
            double3 cameraAup,
            int hzbWidth,
            int hzbHeight,
            JobHandle inputDependency,
            out JobHandle outputDependency,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            outputDependency = inputDependency;
            lease = default;
            if (!TryAcquireHzbCullJobLease(in buffers, out lease))
                return false;

            NativeArray<VoxelSurfaceAabbDTO> chunkAabbs = buffers.ChunkAabbs;
            NativeArray<VoxelSurfacePriorityDTO> priorities = buffers.Priorities;
            NativeArray<VoxelSurfaceHzbTileDTO> hzbTiles = buffers.HzbTiles;
            if (!chunkAabbs.IsCreated ||
                !priorities.IsCreated ||
                !hzbTiles.IsCreated ||
                hzbWidth <= 0 ||
                hzbHeight <= 0)
            {
                ReleaseJobBufferLease(ref lease);
                return false;
            }

            VoxelSurfaceHzbCullJob job = default;
            job.Aabbs = chunkAabbs;
            job.Priorities = priorities;
            job.HzbTiles = hzbTiles;
            job.CameraRelativeViewProjection = cameraRelativeViewProjection;
            job.CameraAup = cameraAup;
            job.HzbWidth = hzbWidth;
            job.HzbHeight = hzbHeight;
            int length = math.min(chunkAabbs.Length, priorities.Length);
            outputDependency = job.Schedule(length, 32, inputDependency);
            return true;
        }

        public static bool ShouldEvaluateFrame(in VoxelSurfaceNetsVaultBuffers buffers, int chunkIndex, uint frame)
        {
            if (buffers.Vault != null &&
                buffers.Vault.TryReadOnlyHandle(in buffers.Handles.States, out NativeArray<ChunkMeshingStateDTO>.ReadOnly states) &&
                (uint)chunkIndex < (uint)states.Length)
            {
                ChunkMeshingStateDTO state = states[chunkIndex];
                bool urgent = (state.ChunkHash != 0u && state.Priority <= 1) ||
                              (state.Flags & (VoxelMeshingFlags.Dirty | VoxelMeshingFlags.ModifiedByLaser)) != 0;
                if (urgent)
                    return true;
            }

            float quality = 1f;
            if (buffers.Vault != null &&
                buffers.Vault.TryReadOnlyHandle(in buffers.Handles.Tuning, out NativeArray<VoxelMeshingTuningDTO>.ReadOnly tuning) &&
                tuning.Length > 0)
            {
                quality = math.saturate(tuning[0].GlobalQualityWeight);
            }

            float qualityCurve = Smooth01(math.saturate((quality - 0.1f) * math.rcp(0.9f)));
            float updateHz = math.lerp(5f, 60f, qualityCurve);
            uint evaluationsPerWindow = (uint)math.clamp((int)math.round(updateHz), 5, 60);
            uint phase = (frame * evaluationsPerWindow) % 60u;
            return phase < evaluationsPerWindow;
        }

        public static bool TryBootstrapLookupTables(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, string projectRoot)
        {
            if (vault == null ||
                handles.SurfaceEdgeMasks.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handles.SurfaceEdgeMasks, SystemID.WorldStreaming, out NativeArray<uint> edgeMasks))
            {
                return false;
            }

            try
            {
                if (!edgeMasks.IsCreated || edgeMasks.Length < VoxelSurfaceNetsConstants.LookupCaseCount)
                    return false;

                if (string.IsNullOrEmpty(projectRoot))
                {
                    GenerateEmergencyMockTables(edgeMasks);
                    return true;
                }

                if (TryLoadLookupFile(edgeMasks, Path.Combine(projectRoot, "Docs", "Archive", "surface_nets_lut.h8bin")))
                    return true;

                if (TryLoadLookupFile(edgeMasks, Path.Combine(projectRoot, "Assets", "StreamingAssets", "surface_nets_lut.h8bin")))
                    return true;

                if (TryLoadLookupFile(edgeMasks, Path.Combine(projectRoot, "Assets", "StreamingAssets", "marching_cubes_edge_tables.bin")))
                    return true;

                GenerateEmergencyMockTables(edgeMasks);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.SurfaceEdgeMasks, SystemID.WorldStreaming);
            }
        }

        public static void GenerateEmergencyMockTables(NativeArray<uint> edgeMasks)
        {
            if (!edgeMasks.IsCreated)
                return;

            int count = math.min(edgeMasks.Length, VoxelSurfaceNetsConstants.LookupCaseCount);
            for (int mask = 0; mask < count; mask++)
            {
                uint edgeMask = 0u;
                AddCaseEdge(mask, 0, 1, 0, ref edgeMask);
                AddCaseEdge(mask, 2, 3, 1, ref edgeMask);
                AddCaseEdge(mask, 4, 5, 2, ref edgeMask);
                AddCaseEdge(mask, 6, 7, 3, ref edgeMask);
                AddCaseEdge(mask, 0, 2, 4, ref edgeMask);
                AddCaseEdge(mask, 1, 3, 5, ref edgeMask);
                AddCaseEdge(mask, 4, 6, 6, ref edgeMask);
                AddCaseEdge(mask, 5, 7, 7, ref edgeMask);
                AddCaseEdge(mask, 0, 4, 8, ref edgeMask);
                AddCaseEdge(mask, 1, 5, 9, ref edgeMask);
                AddCaseEdge(mask, 2, 6, 10, ref edgeMask);
                AddCaseEdge(mask, 3, 7, 11, ref edgeMask);
                edgeMasks[mask] = edgeMask;
            }
        }

        public static bool TryGetTuning(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, out VoxelMeshingTuningDTO tuning)
        {
            return TryReadTuning(vault, in handles, out tuning);
        }

        public static bool TrySetTuning(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, in VoxelMeshingTuningDTO tuning)
        {
            if (vault == null ||
                handles.Tuning.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handles.Tuning, SystemID.WorldStreaming, out NativeArray<VoxelMeshingTuningDTO> tuningBuffer))
            {
                return false;
            }

            try
            {
                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = SanitizeTuning(in tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.Tuning, SystemID.WorldStreaming);
            }
        }

#if UNITY_EDITOR
        public static bool TryLoadCsvOverrides(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, string projectRoot)
        {
            if (vault == null ||
                handles.CsvScratch.BufferID == 0u ||
                handles.Tuning.BufferID == 0u ||
                handles.States.BufferID == 0u)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            if (!vault.TryAcquireWriteLock(in handles.CsvScratch, SystemID.WorldStreaming, out NativeArray<byte> csvScratch))
                return false;

            try
            {
                if (!csvScratch.IsCreated)
                    return false;

                int length = ReadFileIntoNativeScratch(path, csvScratch);
                if (length <= 0)
                    return false;

                if (!TryReadTuning(vault, in handles, out VoxelMeshingTuningDTO tuning))
                    return false;

                bool changed = TryApplyCsvOverrides(csvScratch, length, ref tuning);
                if (!changed)
                    return false;

                tuning.ForceRemeshVersion++;
                tuning.LastCsvHash = HashBytes(csvScratch, length);
                tuning.LastCsvWriteTicks = writeTicks;
                return TryCommitCsvTuning(vault, in handles, in tuning);
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.CsvScratch, SystemID.WorldStreaming);
            }
        }

        public static bool TryPollCsvOverrides(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, string projectRoot)
        {
            if (!TryReadTuning(vault, in handles, out VoxelMeshingTuningDTO tuning))
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            if (tuning.LastCsvWriteTicks == writeTicks)
                return false;

            return TryLoadCsvOverrides(vault, ref handles, projectRoot);
        }

        public static bool TryApplyCsvOverrides(NativeArray<byte> bytes, int length, ref VoxelMeshingTuningDTO tuning)
        {
            if (!bytes.IsCreated || length <= 0)
                return false;

            bool changed = false;
            int limit = math.min(length, bytes.Length);
            int index = 0;
            while (index < limit)
            {
                SkipWhitespace(bytes, limit, ref index);
                if (index >= limit)
                    break;

                if (bytes[index] == (byte)'#')
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                uint keyHash = ReadKeyHash(bytes, limit, ref index);
                if (index < limit && bytes[index] == (byte)',')
                    index++;

                if (!TryReadFloat(bytes, limit, ref index, out float value))
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                if (keyHash == _globalQualityHash)
                {
                    tuning.GlobalQualityWeight = math.saturate(value);
                    changed = true;
                }
                else if (keyHash == _isoSurfaceHash)
                {
                    tuning.IsoSurface = math.clamp(value, -1f, 1f);
                    changed = true;
                }
                else if (keyHash == _normalAngleHash)
                {
                    tuning.NormalSmoothingAngleDegrees = math.clamp(value, 0f, 89f);
                    changed = true;
                }
                else if (keyHash == _decimationHash)
                {
                    tuning.DecimationAggression = math.saturate(value);
                    changed = true;
                }
                else if (keyHash == _chunksPerFrameHash)
                {
                    tuning.MaxChunksPerFrame = math.clamp((int)math.round(value), 1, 2);
                    changed = true;
                }
                else if (keyHash == _debugRawHash)
                {
                    tuning.DebugRawCapture01 = math.saturate(value);
                    changed = true;
                }

                SkipLine(bytes, limit, ref index);
            }

            return changed;
        }

#endif

        public static bool TryDumpBlackBoxOnSlowExtraction(in VoxelSurfaceNetsVaultBuffers buffers, string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot) ||
                !TryAcquireTelemetryDumpLease(in buffers, out VoxelSurfaceNetsJobBufferLease lease))
                return false;

            try
            {
                if (!buffers.TelemetryRing.IsCreated || !buffers.TelemetryCursor.IsCreated || buffers.TelemetryCursor.Length <= 0)
                    return false;

                int cursor = math.clamp(buffers.TelemetryCursor[0], 0, buffers.TelemetryRing.Length - 1);
                VoxelMeshingTelemetryEntry entry = buffers.TelemetryRing[cursor];
                if (entry.DumpReason == 0u && entry.ExtractionComputeTimeMs <= 2f)
                    return false;

                uint reason = entry.DumpReason == 0u ? VoxelSurfaceNetsConstants.FaultSlowExtraction : entry.DumpReason;
                return TryWriteDumpFiles(projectRoot, in buffers, reason);
            }
            finally
            {
                ReleaseJobBufferLease(ref lease);
            }
        }

        public static bool TryDumpBlackBox(in VoxelSurfaceNetsVaultBuffers buffers, string projectRoot, uint reason)
        {
            if (buffers.Vault == null ||
                buffers.Handles.TelemetryRing.BufferID == 0u ||
                string.IsNullOrEmpty(projectRoot))
                return false;

            if (!TryAcquireTelemetryDumpLease(in buffers, out VoxelSurfaceNetsJobBufferLease lease))
                return false;

            try
            {
                return TryWriteDumpFiles(projectRoot, in buffers, reason);
            }
            finally
            {
                ReleaseJobBufferLease(ref lease);
            }
        }

        public static bool TryMarkChunkDirty(NativeArray<ChunkMeshingStateDTO> states, uint chunkHash, uint version)
        {
            if (!states.IsCreated)
                return false;

            for (int i = 0; i < states.Length; i++)
            {
                ChunkMeshingStateDTO state = states[i];
                if (state.ChunkHash != chunkHash && state.ChunkHash != 0u)
                    continue;

                state.ChunkHash = chunkHash;
                state.Version = version;
                state.Stage = (byte)VoxelMeshingStage.Dirty;
                state.Flags = (byte)(state.Flags | VoxelMeshingFlags.Dirty | VoxelMeshingFlags.ModifiedByLaser);
                state.Priority = 1;
                states[i] = state;
                return true;
            }

            return false;
        }

        private static void HydrateDefaultsIfNeeded(VoxelSurfaceNetsVaultBuffers buffers)
        {
            bool firstHydration = TryReadTuning(buffers.Vault, in buffers.Handles, out VoxelMeshingTuningDTO tuning) &&
                                  tuning.Version == 0u;
            if (firstHydration)
            {
                TryClearBuffer(buffers.Vault, in buffers.Handles.States);
                TryClearBuffer(buffers.Vault, in buffers.Handles.Vertices);
                TryClearBuffer(buffers.Vault, in buffers.Handles.Indices);
                TryClearBuffer(buffers.Vault, in buffers.Handles.CellVertexMap);
                TryClearBuffer(buffers.Vault, in buffers.Handles.RawDebugVertices);
                TryClearBuffer(buffers.Vault, in buffers.Handles.ChunkAabbs);
                TryClearBuffer(buffers.Vault, in buffers.Handles.ModifiedSignals);
                TryClearBuffer(buffers.Vault, in buffers.Handles.Priorities);
                TryClearBuffer(buffers.Vault, in buffers.Handles.PhysicsBakeRequests);
                TryClearBuffer(buffers.Vault, in buffers.Handles.HzbTiles);
                TryClearBuffer(buffers.Vault, in buffers.Handles.CsvScratch);
                TryWriteEmergencyMockTables(buffers.Vault, in buffers.Handles);
                TryWriteDefaultTuning(buffers.Vault, in buffers.Handles);
            }

            if (TryReadMockDensity(buffers.Vault, in buffers.Handles, out MockVoxelDensityArray mockDensity) &&
                mockDensity.Dimensions.x == 0)
            {
                TryWriteDefaultMockDensity(buffers.Vault, in buffers.Handles);
            }
        }

        private static bool TryReadTuning(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles,
            out VoxelMeshingTuningDTO tuning)
        {
            tuning = default;
            if (vault == null ||
                handles.Tuning.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in handles.Tuning, out NativeArray<VoxelMeshingTuningDTO>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        private static bool TryReadMockDensity(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles,
            out MockVoxelDensityArray mockDensity)
        {
            mockDensity = default;
            if (vault == null ||
                handles.MockDensityConfig.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in handles.MockDensityConfig, out NativeArray<MockVoxelDensityArray>.ReadOnly mockDensityBuffer) ||
                mockDensityBuffer.Length <= 0)
            {
                return false;
            }

            mockDensity = mockDensityBuffer[0];
            return true;
        }

        private static bool TryCommitCsvTuning(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles,
            in VoxelMeshingTuningDTO tuning)
        {
            if (vault == null)
                return false;

            VoxelMeshingTuningDTO sanitized = SanitizeTuning(in tuning);
            if (!vault.TryAcquireWriteLock(in handles.Tuning, SystemID.WorldStreaming, out NativeArray<VoxelMeshingTuningDTO> tuningBuffer))
                return false;

            try
            {
                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = sanitized;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.Tuning, SystemID.WorldStreaming);
            }

            if (!vault.TryAcquireWriteLock(in handles.States, SystemID.WorldStreaming, out NativeArray<ChunkMeshingStateDTO> states))
                return false;

            try
            {
                if (!states.IsCreated)
                    return false;

                MarkVisibleChunksDirty(states, sanitized.ForceRemeshVersion);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.States, SystemID.WorldStreaming);
            }
        }

        private static bool TryClearBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : unmanaged
        {
            if (vault == null ||
                handle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handle, SystemID.WorldStreaming, out NativeArray<T> buffer))
            {
                return false;
            }

            try
            {
                if (!buffer.IsCreated)
                    return false;

                ClearArray(buffer);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
            }
        }

        private static bool TryWriteEmergencyMockTables(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles)
        {
            if (vault == null ||
                handles.SurfaceEdgeMasks.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handles.SurfaceEdgeMasks, SystemID.WorldStreaming, out NativeArray<uint> edgeMasks))
            {
                return false;
            }

            try
            {
                GenerateEmergencyMockTables(edgeMasks);
                return edgeMasks.IsCreated;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.SurfaceEdgeMasks, SystemID.WorldStreaming);
            }
        }

        private static bool TryWriteDefaultTuning(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles)
        {
            if (vault == null ||
                handles.Tuning.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handles.Tuning, SystemID.WorldStreaming, out NativeArray<VoxelMeshingTuningDTO> tuningBuffer))
            {
                return false;
            }

            try
            {
                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = VoxelSurfaceNetsDefaults.BuildDefaultTuning();
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.Tuning, SystemID.WorldStreaming);
            }
        }

        private static bool TryWriteDefaultMockDensity(
            IDataVault vault,
            in VoxelSurfaceNetsVaultHandles handles)
        {
            if (vault == null ||
                handles.MockDensityConfig.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handles.MockDensityConfig, SystemID.WorldStreaming, out NativeArray<MockVoxelDensityArray> mockDensityBuffer))
            {
                return false;
            }

            try
            {
                if (!mockDensityBuffer.IsCreated || mockDensityBuffer.Length <= 0)
                    return false;

                mockDensityBuffer[0] = VoxelSurfaceNetsDefaults.BuildDefaultMockDensity();
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.MockDensityConfig, SystemID.WorldStreaming);
            }
        }

        private static bool TryAcquireTelemetryDumpLease(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out VoxelSurfaceNetsJobBufferLease lease)
        {
            lease = default;
            IDataVault vault = buffers.Vault;
            if (vault == null)
                return false;

            if (!TryAcquireJobBufferLeaseGuard(
                    vault,
                    in buffers.Handles,
                    TelemetryDumpMutationGuardMask,
                    JobTelemetryRingLock | JobTelemetryCursorLock,
                    0u,
                    out lease))
                return false;

            return true;
        }

        private static bool TryLoadLookupFile(NativeArray<uint> edgeMasks, string path)
        {
            if (!edgeMasks.IsCreated || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            Span<byte> bytes = stackalloc byte[VoxelSurfaceNetsConstants.LookupCaseCount * 4];
            int read;
            using (FileStream stream = File.OpenRead(path))
                read = stream.Read(bytes);

            if (read < bytes.Length)
                return false;

            int count = math.min(edgeMasks.Length, VoxelSurfaceNetsConstants.LookupCaseCount);
            for (int i = 0; i < count; i++)
            {
                int offset = i * 4;
                edgeMasks[i] =
                    bytes[offset] |
                    ((uint)bytes[offset + 1] << 8) |
                    ((uint)bytes[offset + 2] << 16) |
                    ((uint)bytes[offset + 3] << 24);
            }

            return true;
        }

        private static void AddCaseEdge(int mask, int a, int b, int edge, ref uint edgeMask)
        {
            bool sa = ((mask >> a) & 1) != 0;
            bool sb = ((mask >> b) & 1) != 0;
            if (sa != sb)
                edgeMask |= 1u << edge;
        }

        private static VoxelMeshingTuningDTO SanitizeTuning(in VoxelMeshingTuningDTO tuning)
        {
            VoxelMeshingTuningDTO sanitized = tuning;
            sanitized.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            sanitized.IsoSurface = math.clamp(tuning.IsoSurface, -1f, 1f);
            sanitized.DecimationAggression = math.saturate(tuning.DecimationAggression);
            sanitized.NormalSmoothingAngleDegrees = math.clamp(tuning.NormalSmoothingAngleDegrees, 0f, 89f);
            sanitized.VoxelSize = math.max(tuning.VoxelSize, VoxelSurfaceNetsConstants.Epsilon);
            sanitized.BiomeBlendScale = math.max(tuning.BiomeBlendScale, 0f);
            sanitized.MaxExtractionMs = math.max(tuning.MaxExtractionMs, 0.25f);
            sanitized.MaxChunksPerFrame = math.clamp(tuning.MaxChunksPerFrame, 1, 2);
            sanitized.ChunkResolution = VoxelSurfaceNetsConstants.ChunkResolution;
            return sanitized;
        }

        private static MockVoxelDensityArray SanitizeMockDensity(in MockVoxelDensityArray config)
        {
            MockVoxelDensityArray sanitized = config;
            sanitized.Dimensions = new int3(
                VoxelSurfaceNetsConstants.DensityResolution,
                VoxelSurfaceNetsConstants.DensityResolution,
                VoxelSurfaceNetsConstants.DensityResolution);
            sanitized.VoxelSize = math.max(config.VoxelSize, VoxelSurfaceNetsConstants.Epsilon);
            sanitized.Radius = math.max(config.Radius, sanitized.VoxelSize);
            sanitized.ShellThickness = math.max(config.ShellThickness, sanitized.VoxelSize);
            return sanitized;
        }

        private static void MarkVisibleChunksDirty(NativeArray<ChunkMeshingStateDTO> states, uint version)
        {
            if (!states.IsCreated)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                ChunkMeshingStateDTO state = states[i];
                if (state.ChunkHash == 0u)
                    continue;

                state.Version = version;
                state.Stage = (byte)VoxelMeshingStage.Dirty;
                state.Flags = (byte)(state.Flags | VoxelMeshingFlags.Dirty);
                states[i] = state;
            }
        }

        private static void ClearArray<T>(NativeArray<T> array) where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, array.Length * UnsafeUtility.SizeOf<T>());
        }

#if UNITY_EDITOR
        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || string.IsNullOrEmpty(path))
                return 0;

            int length;
            using (FileStream stream = File.OpenRead(path))
            {
                length = (int)math.min(stream.Length, scratch.Length);
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                Span<byte> span = new Span<byte>(ptr, length);
                return stream.Read(span);
            }
        }

        private static string ResolveCsvPath(string projectRoot)
        {
            return string.IsNullOrEmpty(projectRoot) ? null : Path.Combine(projectRoot, CsvFileName);
        }

        private static void SkipWhitespace(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t' && c != (byte)'\r' && c != (byte)'\n')
                    break;

                index++;
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] != (byte)'\n')
                index++;

            if (index < limit)
                index++;
        }

        private static uint ReadKeyHash(NativeArray<byte> bytes, int limit, ref int index)
        {
            uint hash = 2166136261u;
            while (index < limit)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                if (c != (byte)' ' && c != (byte)'\t')
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                index++;
            }

            return hash;
        }

        private static bool TryReadFloat(NativeArray<byte> bytes, int limit, ref int index, out float value)
        {
            value = 0f;
            SkipValueWhitespace(bytes, limit, ref index);
            if (index >= limit)
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            bool readAny = false;
            float integer = 0f;
            while (index < limit)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                integer = (integer * 10f) + (c - (byte)'0');
                index++;
                readAny = true;
            }

            float fraction = 0f;
            if (index < limit && bytes[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < limit)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    fraction += (c - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                    readAny = true;
                }
            }

            value = (integer + fraction) * sign;
            return readAny && math.isfinite(value);
        }

        private static void SkipValueWhitespace(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t')
                    break;

                index++;
            }
        }

        private static uint HashBytes(NativeArray<byte> bytes, int length)
        {
            uint hash = 2166136261u;
            int limit = math.min(length, bytes.Length);
            for (int i = 0; i < limit; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619u;
            }

            return hash;
        }

#endif

        private static uint HashAsciiLiteral(string text)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(text))
                return hash;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                byte c = (byte)(ch >= 'A' && ch <= 'Z' ? ch + 32 : ch);
                if (c != (byte)' ' && c != (byte)'\t')
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
            }

            return hash;
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private static bool TryWriteDumpFile(string path, in VoxelSurfaceNetsVaultBuffers buffers, uint reason)
        {
            if (string.IsNullOrEmpty(path) || !buffers.TelemetryRing.IsCreated)
                return false;

            Span<byte> header = stackalloc byte[32];
            WriteUInt32(header, 0, VoxelSurfaceNetsConstants.DumpMagic);
            WriteUInt32(header, 4, VoxelSurfaceNetsConstants.DumpEndianMarker);
            WriteUInt32(header, 8, DumpVersion);
            WriteUInt32(header, 12, reason);
            WriteUInt32(header, 16, (uint)buffers.TelemetryRing.Length);
            WriteUInt32(header, 20, (uint)UnsafeUtility.SizeOf<VoxelMeshingTelemetryEntry>());
            WriteUInt32(header, 24, buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? (uint)buffers.TelemetryCursor[0] : 0u);
            WriteUInt32(header, 28, 0u);

            using (FileStream stream = File.Create(path))
            {
                stream.Write(header);
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffers.TelemetryRing);
                int byteLength = buffers.TelemetryRing.Length * UnsafeUtility.SizeOf<VoxelMeshingTelemetryEntry>();
                ReadOnlySpan<byte> telemetry = new ReadOnlySpan<byte>(ptr, byteLength);
                stream.Write(telemetry);
            }

            return true;
        }

        private static bool TryWriteDumpFiles(string projectRoot, in VoxelSurfaceNetsVaultBuffers buffers, uint reason)
        {
            string dir = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(dir);
            bool primary = TryWriteDumpFile(Path.Combine(dir, DumpFileName), in buffers, reason);
            bool agent = TryWriteDumpFile(Path.Combine(dir, AgentDumpFileName), in buffers, reason);
            return primary && agent;
        }

        private static void WriteUInt32(Span<byte> target, int offset, uint value)
        {
            target[offset] = (byte)(value & 0xFFu);
            target[offset + 1] = (byte)((value >> 8) & 0xFFu);
            target[offset + 2] = (byte)((value >> 16) & 0xFFu);
            target[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }
    }
}
