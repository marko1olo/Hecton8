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
        public NativeArray<sbyte> Density;
        public NativeArray<VoxelVertexDTO> Vertices;
        public NativeArray<uint> Indices;
        public NativeArray<int> CellVertexMap;
        public NativeArray<ChunkMeshingStateDTO> States;
        public NativeArray<VoxelMeshingTuningDTO> Tuning;
        public NativeArray<VoxelMeshingTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<byte> CsvScratch;
        public NativeArray<uint> SurfaceEdgeMasks;
        public NativeArray<float3> RawDebugVertices;
        public NativeArray<VoxelSurfaceAabbDTO> ChunkAabbs;
        public NativeArray<VoxelSurfaceModifiedSignal> ModifiedSignals;
        public NativeArray<VoxelSurfacePriorityDTO> Priorities;
        public NativeArray<VoxelSurfaceIndirectArgsDTO> IndirectArgs;
        public NativeArray<MockVoxelDensityArray> MockDensityConfig;
        public NativeArray<VoxelSurfacePhysicsBakeRequestDTO> PhysicsBakeRequests;
        public NativeArray<VoxelSurfaceHzbTileDTO> HzbTiles;

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
    }

    public static unsafe class VoxelSurfaceNetsVault
    {
        private const int DumpVersion = 1;
        private const string DumpFileName = "Dump_MESH_SURGEON.bin";
        private const string AgentDumpFileName = "Dump_SHINOBU_61.bin";
        private const string CsvFileName = "meshing_profiles.csv";
        private static readonly uint _globalQualityHash = HashAsciiLiteral("global_quality_weight");
        private static readonly uint _isoSurfaceHash = HashAsciiLiteral("iso_surface_threshold");
        private static readonly uint _normalAngleHash = HashAsciiLiteral("normal_smoothing_angle");
        private static readonly uint _decimationHash = HashAsciiLiteral("decimation_aggression");
        private static readonly uint _chunksPerFrameHash = HashAsciiLiteral("max_chunks_per_frame");
        private static readonly uint _debugRawHash = HashAsciiLiteral("show_raw_extraction");

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

            handles.Density = vault.GetGenerationHandle<sbyte>(
                VoxelSurfaceNetsVaultBufferIds.Density,
                VoxelSurfaceNetsConstants.DensitySampleCount,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Vertices = vault.GetGenerationHandle<VoxelVertexDTO>(
                VoxelSurfaceNetsVaultBufferIds.Vertices,
                VoxelSurfaceNetsConstants.MaxVertices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Indices = vault.GetGenerationHandle<uint>(
                VoxelSurfaceNetsVaultBufferIds.Indices,
                VoxelSurfaceNetsConstants.MaxIndices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.CellVertexMap = vault.GetGenerationHandle<int>(
                VoxelSurfaceNetsVaultBufferIds.CellVertexMap,
                VoxelSurfaceNetsConstants.CellCount,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.States = vault.GetGenerationHandle<ChunkMeshingStateDTO>(
                VoxelSurfaceNetsVaultBufferIds.States,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Tuning = vault.GetGenerationHandle<VoxelMeshingTuningDTO>(
                VoxelSurfaceNetsVaultBufferIds.Tuning,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.GetGenerationHandle<VoxelMeshingTelemetryEntry>(
                VoxelSurfaceNetsVaultBufferIds.TelemetryRing,
                VoxelSurfaceNetsConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.GetGenerationHandle<int>(
                VoxelSurfaceNetsVaultBufferIds.TelemetryCursor,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.GetGenerationHandle<byte>(
                VoxelSurfaceNetsVaultBufferIds.CsvScratch,
                VoxelSurfaceNetsConstants.CsvScratchBytes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.SurfaceEdgeMasks = vault.GetGenerationHandle<uint>(
                VoxelSurfaceNetsVaultBufferIds.SurfaceEdgeMasks,
                VoxelSurfaceNetsConstants.LookupCaseCount,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.RawDebugVertices = vault.GetGenerationHandle<float3>(
                VoxelSurfaceNetsVaultBufferIds.RawDebugVertices,
                VoxelSurfaceNetsConstants.MaxRawDebugVertices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.ChunkAabbs = vault.GetGenerationHandle<VoxelSurfaceAabbDTO>(
                VoxelSurfaceNetsVaultBufferIds.ChunkAabbs,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.ModifiedSignals = vault.GetGenerationHandle<VoxelSurfaceModifiedSignal>(
                VoxelSurfaceNetsVaultBufferIds.ModifiedSignals,
                VoxelSurfaceNetsConstants.MaxModifiedSignals,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Priorities = vault.GetGenerationHandle<VoxelSurfacePriorityDTO>(
                VoxelSurfaceNetsVaultBufferIds.Priorities,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.IndirectArgs = vault.GetGenerationHandle<VoxelSurfaceIndirectArgsDTO>(
                VoxelSurfaceNetsVaultBufferIds.IndirectArgs,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.MockDensityConfig = vault.GetGenerationHandle<MockVoxelDensityArray>(
                VoxelSurfaceNetsVaultBufferIds.MockDensityConfig,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.PhysicsBakeRequests = vault.GetGenerationHandle<VoxelSurfacePhysicsBakeRequestDTO>(
                VoxelSurfaceNetsVaultBufferIds.PhysicsBakeRequests,
                VoxelSurfaceNetsConstants.MaxTrackedChunks,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.HzbTiles = vault.GetGenerationHandle<VoxelSurfaceHzbTileDTO>(
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

            return TryResolveView(vault, in handles.Density, out buffers.Density) &&
                   TryResolveView(vault, in handles.Vertices, out buffers.Vertices) &&
                   TryResolveView(vault, in handles.Indices, out buffers.Indices) &&
                   TryResolveView(vault, in handles.CellVertexMap, out buffers.CellVertexMap) &&
                   TryResolveView(vault, in handles.States, out buffers.States) &&
                   TryResolveView(vault, in handles.Tuning, out buffers.Tuning) &&
                   TryResolveView(vault, in handles.TelemetryRing, out buffers.TelemetryRing) &&
                   TryResolveView(vault, in handles.TelemetryCursor, out buffers.TelemetryCursor) &&
                   TryResolveView(vault, in handles.CsvScratch, out buffers.CsvScratch) &&
                   TryResolveView(vault, in handles.SurfaceEdgeMasks, out buffers.SurfaceEdgeMasks) &&
                   TryResolveView(vault, in handles.RawDebugVertices, out buffers.RawDebugVertices) &&
                   TryResolveView(vault, in handles.ChunkAabbs, out buffers.ChunkAabbs) &&
                   TryResolveView(vault, in handles.ModifiedSignals, out buffers.ModifiedSignals) &&
                   TryResolveView(vault, in handles.Priorities, out buffers.Priorities) &&
                   TryResolveView(vault, in handles.IndirectArgs, out buffers.IndirectArgs) &&
                   TryResolveView(vault, in handles.MockDensityConfig, out buffers.MockDensityConfig) &&
                   TryResolveView(vault, in handles.PhysicsBakeRequests, out buffers.PhysicsBakeRequests) &&
                   TryResolveView(vault, in handles.HzbTiles, out buffers.HzbTiles) &&
                   buffers.IsCreated();
        }

        public static ref ChunkMeshingStateDTO GetStateAsRef(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, int index)
        {
            if (!TryResolveView(vault, in handles.States, out NativeArray<ChunkMeshingStateDTO> states) ||
                (uint)index >= (uint)states.Length)
            {
                throw new InvalidOperationException("Voxel surface state view unavailable.");
            }

            return ref UnsafeUtility.ArrayElementAsRef<ChunkMeshingStateDTO>(
                NativeArrayUnsafeUtility.GetUnsafePtr(states),
                index);
        }

        public static ref readonly ChunkMeshingStateDTO GetStateAsReadOnlyRef(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, int index)
        {
            if (!TryResolveView(vault, in handles.States, out NativeArray<ChunkMeshingStateDTO> states) ||
                (uint)index >= (uint)states.Length)
            {
                throw new InvalidOperationException("Voxel surface state view unavailable.");
            }

            return ref UnsafeUtility.ArrayElementAsRef<ChunkMeshingStateDTO>(
                (void*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                index);
        }

        private static bool TryResolveView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        public static bool TryCreateMockDensityJob(
            in VoxelSurfaceNetsVaultBuffers buffers,
            out GenerateMockVoxelDensitySphereJob job,
            out int scheduleLength)
        {
            job = default;
            scheduleLength = 0;
            if (!buffers.Density.IsCreated || !buffers.MockDensityConfig.IsCreated || buffers.MockDensityConfig.Length <= 0)
                return false;

            VoxelMeshingTuningDTO tuning = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0
                ? SanitizeTuning(buffers.Tuning[0])
                : VoxelSurfaceNetsDefaults.BuildDefaultTuning();

            job.Densities = buffers.Density;
            job.Config = SanitizeMockDensity(buffers.MockDensityConfig[0]);
            job.GlobalQualityWeight = tuning.GlobalQualityWeight;
            scheduleLength = math.min(buffers.Density.Length, VoxelSurfaceNetsConstants.DensitySampleCount);
            return scheduleLength > 0;
        }

        public static bool TryScheduleMockDensity(
            in VoxelSurfaceNetsVaultBuffers buffers,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!TryCreateMockDensityJob(in buffers, out GenerateMockVoxelDensitySphereJob job, out int scheduleLength))
                return false;

            outputDependency = job.Schedule(scheduleLength, 64, inputDependency);
            return true;
        }

        public static bool TryCreateExtractionJob(
            in VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            uint frame,
            out SurfaceNetExtractionJob job)
        {
            job = default;
            if (!buffers.IsCreated())
                return false;

            job.Densities = buffers.Density;
            job.Vertices = buffers.Vertices;
            job.Indices = buffers.Indices;
            job.CellVertexMap = buffers.CellVertexMap;
            job.States = buffers.States;
            job.Tuning = buffers.Tuning;
            job.SurfaceEdgeMasks = buffers.SurfaceEdgeMasks;
            job.TelemetryRing = buffers.TelemetryRing;
            job.TelemetryCursor = buffers.TelemetryCursor;
            job.RawDebugVertices = buffers.RawDebugVertices;
            job.IndirectArgs = buffers.IndirectArgs;
            job.ChunkIndex = chunkIndex;
            job.Frame = frame;
            return true;
        }

        public static bool TryScheduleExtraction(
            in VoxelSurfaceNetsVaultBuffers buffers,
            int chunkIndex,
            uint frame,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!ShouldEvaluateFrame(in buffers, chunkIndex, frame))
                return false;

            if (!TryCreateExtractionJob(in buffers, chunkIndex, frame, out SurfaceNetExtractionJob job))
                return false;

            outputDependency = job.Schedule(inputDependency);
            return true;
        }

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
            if (!buffers.ChunkAabbs.IsCreated ||
                !buffers.Priorities.IsCreated ||
                !buffers.HzbTiles.IsCreated ||
                hzbWidth <= 0 ||
                hzbHeight <= 0)
            {
                return false;
            }

            VoxelSurfaceHzbCullJob job = default;
            job.Aabbs = buffers.ChunkAabbs;
            job.Priorities = buffers.Priorities;
            job.HzbTiles = buffers.HzbTiles;
            job.CameraRelativeViewProjection = cameraRelativeViewProjection;
            job.CameraAup = cameraAup;
            job.HzbWidth = hzbWidth;
            job.HzbHeight = hzbHeight;
            int length = math.min(buffers.ChunkAabbs.Length, buffers.Priorities.Length);
            outputDependency = job.Schedule(length, 32, inputDependency);
            return true;
        }

        public static bool ShouldEvaluateFrame(in VoxelSurfaceNetsVaultBuffers buffers, int chunkIndex, uint frame)
        {
            if (buffers.States.IsCreated && (uint)chunkIndex < (uint)buffers.States.Length)
            {
                ChunkMeshingStateDTO state = buffers.States[chunkIndex];
                bool urgent = (state.ChunkHash != 0u && state.Priority <= 1) ||
                              (state.Flags & (VoxelMeshingFlags.Dirty | VoxelMeshingFlags.ModifiedByLaser)) != 0;
                if (urgent)
                    return true;
            }

            float quality = 1f;
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
                quality = math.saturate(buffers.Tuning[0].GlobalQualityWeight);

            float qualityCurve = Smooth01(math.saturate((quality - 0.1f) * math.rcp(0.9f)));
            float updateHz = math.lerp(5f, 60f, qualityCurve);
            uint evaluationsPerWindow = (uint)math.clamp((int)math.round(updateHz), 5, 60);
            uint phase = (frame * evaluationsPerWindow) % 60u;
            return phase < evaluationsPerWindow;
        }

        public static bool TryBootstrapLookupTables(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers buffers) ||
                !buffers.SurfaceEdgeMasks.IsCreated ||
                buffers.SurfaceEdgeMasks.Length < VoxelSurfaceNetsConstants.LookupCaseCount)
            {
                return false;
            }

            if (string.IsNullOrEmpty(projectRoot))
            {
                GenerateEmergencyMockTables(buffers.SurfaceEdgeMasks);
                return true;
            }

            if (TryLoadLookupFile(buffers.SurfaceEdgeMasks, Path.Combine(projectRoot, "Docs", "Archive", "surface_nets_lut.h8bin")))
                return true;

            if (TryLoadLookupFile(buffers.SurfaceEdgeMasks, Path.Combine(projectRoot, "Assets", "StreamingAssets", "surface_nets_lut.h8bin")))
                return true;

            if (TryLoadLookupFile(buffers.SurfaceEdgeMasks, Path.Combine(projectRoot, "Assets", "StreamingAssets", "marching_cubes_edge_tables.bin")))
                return true;

            GenerateEmergencyMockTables(buffers.SurfaceEdgeMasks);
            return true;
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
            tuning = default;
            if (!TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            tuning = buffers.Tuning[0];
            return true;
        }

        public static bool TrySetTuning(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, in VoxelMeshingTuningDTO tuning)
        {
            if (!TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            buffers.Tuning[0] = SanitizeTuning(in tuning);
            return true;
        }

        public static bool TryLoadCsvOverrides(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            int length = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            VoxelMeshingTuningDTO tuning = buffers.Tuning[0];
            bool changed = TryApplyCsvOverrides(buffers.CsvScratch, length, ref tuning);
            if (!changed)
                return false;

            tuning.ForceRemeshVersion++;
            tuning.LastCsvHash = HashBytes(buffers.CsvScratch, length);
            tuning.LastCsvWriteTicks = writeTicks;
            buffers.Tuning[0] = SanitizeTuning(in tuning);
            MarkVisibleChunksDirty(buffers.States, tuning.ForceRemeshVersion);
            return true;
        }

        public static bool TryPollCsvOverrides(IDataVault vault, ref VoxelSurfaceNetsVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out VoxelSurfaceNetsVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            if (buffers.Tuning[0].LastCsvWriteTicks == writeTicks)
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

        public static bool TryDumpBlackBoxOnSlowExtraction(in VoxelSurfaceNetsVaultBuffers buffers, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || !buffers.TelemetryCursor.IsCreated || buffers.TelemetryCursor.Length <= 0)
                return false;

            int cursor = math.clamp(buffers.TelemetryCursor[0], 0, buffers.TelemetryRing.Length - 1);
            VoxelMeshingTelemetryEntry entry = buffers.TelemetryRing[cursor];
            if (entry.DumpReason == 0u && entry.ExtractionComputeTimeMs <= 2f)
                return false;

            return TryDumpBlackBox(in buffers, projectRoot, entry.DumpReason == 0u ? VoxelSurfaceNetsConstants.FaultSlowExtraction : entry.DumpReason);
        }

        public static bool TryDumpBlackBox(in VoxelSurfaceNetsVaultBuffers buffers, string projectRoot, uint reason)
        {
            if (!buffers.TelemetryRing.IsCreated || string.IsNullOrEmpty(projectRoot))
                return false;

            string dir = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(dir);
            bool primary = TryWriteDumpFile(Path.Combine(dir, DumpFileName), in buffers, reason);
            bool agent = TryWriteDumpFile(Path.Combine(dir, AgentDumpFileName), in buffers, reason);
            return primary && agent;
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
            bool firstHydration = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0 && buffers.Tuning[0].Version == 0u;
            if (firstHydration)
            {
                ClearArray(buffers.States);
                ClearArray(buffers.Vertices);
                ClearArray(buffers.Indices);
                ClearArray(buffers.CellVertexMap);
                ClearArray(buffers.RawDebugVertices);
                ClearArray(buffers.ChunkAabbs);
                ClearArray(buffers.ModifiedSignals);
                ClearArray(buffers.Priorities);
                ClearArray(buffers.PhysicsBakeRequests);
                ClearArray(buffers.HzbTiles);
                ClearArray(buffers.CsvScratch);
                GenerateEmergencyMockTables(buffers.SurfaceEdgeMasks);
                buffers.Tuning[0] = VoxelSurfaceNetsDefaults.BuildDefaultTuning();
            }

            if (buffers.MockDensityConfig.IsCreated && buffers.MockDensityConfig.Length > 0 && buffers.MockDensityConfig[0].Dimensions.x == 0)
                buffers.MockDensityConfig[0] = VoxelSurfaceNetsDefaults.BuildDefaultMockDensity();
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

        private static void WriteUInt32(Span<byte> target, int offset, uint value)
        {
            target[offset] = (byte)(value & 0xFFu);
            target[offset + 1] = (byte)((value >> 8) & 0xFFu);
            target[offset + 2] = (byte)((value >> 16) & 0xFFu);
            target[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }
    }
}
