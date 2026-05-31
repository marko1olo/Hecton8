using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralCoral
{
    public struct ProceduralCoralVaultHandles
    {
        public VaultGenerationHandle<CoralLSystemRuleDTO> Rules;
        public VaultGenerationHandle<uint> InstructionScratchA;
        public VaultGenerationHandle<uint> InstructionScratchB;
        public VaultGenerationHandle<CoralBranchDTO> Branches;
        public VaultGenerationHandle<CoralTurtleStateDTO> TurtleStack;
        public VaultGenerationHandle<CoralSpatialCellDTO> SpatialCells;
        public VaultGenerationHandle<float4x4> RenderMatrices;
        public VaultGenerationHandle<CoralIndirectArgsDTO> IndirectArgs;
        public VaultGenerationHandle<CoralSectorTriggerDTO> SectorTriggers;
        public VaultGenerationHandle<CapsuleColliderDTO> CollisionProxies;
        public VaultGenerationHandle<SyncPulseDTO> SyncPulses;
        public VaultGenerationHandle<CoralGenerationTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<CoralTuningDTO> Tuning;
        public VaultGenerationHandle<byte> CsvScratch;
        public VaultGenerationHandle<CoralPaddedCounterDTO> Counters;
        public VaultGenerationHandle<CoralDebugSegmentDTO> DebugSegments;
        public VaultGenerationHandle<CoralGpuSwayDTO> GpuSway;
        public VaultGenerationHandle<CoralSelfAuditResultDTO> SelfAudit;
        public VaultGenerationHandle<CoralHzbTileDTO> HzbTiles;

        public bool IsCreated()
        {
            return IsHandleValid(in Rules) &&
                   IsHandleValid(in InstructionScratchA) &&
                   IsHandleValid(in InstructionScratchB) &&
                   IsHandleValid(in Branches) &&
                   IsHandleValid(in TurtleStack) &&
                   IsHandleValid(in SpatialCells) &&
                   IsHandleValid(in RenderMatrices) &&
                   IsHandleValid(in IndirectArgs) &&
                   IsHandleValid(in SectorTriggers) &&
                   IsHandleValid(in CollisionProxies) &&
                   IsHandleValid(in SyncPulses) &&
                   IsHandleValid(in TelemetryRing) &&
                   IsHandleValid(in TelemetryCursor) &&
                   IsHandleValid(in Tuning) &&
                   IsHandleValid(in CsvScratch) &&
                   IsHandleValid(in Counters) &&
                   IsHandleValid(in DebugSegments) &&
                   IsHandleValid(in GpuSway) &&
                   IsHandleValid(in SelfAudit) &&
                   IsHandleValid(in HzbTiles);
        }

        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }
    }

    public ref struct ProceduralCoralVaultBuffers
    {
        public NativeArray<CoralLSystemRuleDTO> Rules;
        public NativeArray<uint> InstructionScratchA;
        public NativeArray<uint> InstructionScratchB;
        public NativeArray<CoralBranchDTO> Branches;
        public NativeArray<CoralTurtleStateDTO> TurtleStack;
        public NativeArray<CoralSpatialCellDTO> SpatialCells;
        public NativeArray<float4x4> RenderMatrices;
        public NativeArray<CoralIndirectArgsDTO> IndirectArgs;
        public NativeArray<CoralSectorTriggerDTO> SectorTriggers;
        public NativeArray<CapsuleColliderDTO> CollisionProxies;
        public NativeArray<SyncPulseDTO> SyncPulses;
        public NativeArray<CoralGenerationTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<CoralTuningDTO> Tuning;
        public NativeArray<byte> CsvScratch;
        public NativeArray<CoralPaddedCounterDTO> Counters;
        public NativeArray<CoralDebugSegmentDTO> DebugSegments;
        public NativeArray<CoralGpuSwayDTO> GpuSway;
        public NativeArray<CoralSelfAuditResultDTO> SelfAudit;
        public NativeArray<CoralHzbTileDTO> HzbTiles;

        public bool IsCreated()
        {
            return Rules.IsCreated &&
                   InstructionScratchA.IsCreated &&
                   InstructionScratchB.IsCreated &&
                   Branches.IsCreated &&
                   TurtleStack.IsCreated &&
                   SpatialCells.IsCreated &&
                   RenderMatrices.IsCreated &&
                   IndirectArgs.IsCreated &&
                   SectorTriggers.IsCreated &&
                   CollisionProxies.IsCreated &&
                   SyncPulses.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   Tuning.IsCreated &&
                   CsvScratch.IsCreated &&
                   Counters.IsCreated &&
                   DebugSegments.IsCreated &&
                   GpuSway.IsCreated &&
                   SelfAudit.IsCreated &&
                   HzbTiles.IsCreated;
        }
    }

    public static unsafe class ProceduralCoralVault
    {
        private const int DumpVersion = 1;
        private const string BinaryRulesFileName = "coral_growth_rules.h8bin";
        private const string CsvRulesFileName = "coral_lsystem_rules.csv";
        private const string DumpFileName = "Dump_CORAL_ARCHITECT.bin";
        private const string AgentDumpFileName = "Dump_SHINOBU_139.bin";

        public static bool TryEnsure(IDataVault vault, out ProceduralCoralVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryResolveExisting(vault, out handles))
                    return false;

                if (TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers lockedBuffers))
                    HydrateDefaultsIfNeeded(lockedBuffers);

                return handles.IsCreated();
            }

            handles.Rules = vault.EnsureGenerationHandle<CoralLSystemRuleDTO>(
                ProceduralCoralVaultBufferIds.Rules,
                ProceduralCoralConstants.MaxRules,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.InstructionScratchA = vault.EnsureGenerationHandle<uint>(
                ProceduralCoralVaultBufferIds.InstructionScratchA,
                ProceduralCoralConstants.MaxInstructions,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.InstructionScratchB = vault.EnsureGenerationHandle<uint>(
                ProceduralCoralVaultBufferIds.InstructionScratchB,
                ProceduralCoralConstants.MaxInstructions,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Branches = vault.EnsureGenerationHandle<CoralBranchDTO>(
                ProceduralCoralVaultBufferIds.Branches,
                ProceduralCoralConstants.MaxBranches,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.TurtleStack = vault.EnsureGenerationHandle<CoralTurtleStateDTO>(
                ProceduralCoralVaultBufferIds.TurtleStack,
                ProceduralCoralConstants.MaxTurtleStack,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.SpatialCells = vault.EnsureGenerationHandle<CoralSpatialCellDTO>(
                ProceduralCoralVaultBufferIds.SpatialCells,
                ProceduralCoralConstants.MaxSpatialCells,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.RenderMatrices = vault.EnsureGenerationHandle<float4x4>(
                ProceduralCoralVaultBufferIds.RenderMatrices,
                ProceduralCoralConstants.MaxRenderMatrices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.IndirectArgs = vault.EnsureGenerationHandle<CoralIndirectArgsDTO>(
                ProceduralCoralVaultBufferIds.IndirectArgs,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.SectorTriggers = vault.EnsureGenerationHandle<CoralSectorTriggerDTO>(
                ProceduralCoralVaultBufferIds.SectorTriggers,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CollisionProxies = vault.EnsureGenerationHandle<CapsuleColliderDTO>(
                ProceduralCoralVaultBufferIds.CollisionProxies,
                ProceduralCoralConstants.MaxCollisionProxies,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.SyncPulses = vault.EnsureGenerationHandle<SyncPulseDTO>(
                ProceduralCoralVaultBufferIds.SyncPulses,
                ProceduralCoralConstants.MaxSyncPulses,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<CoralGenerationTelemetryEntry>(
                ProceduralCoralVaultBufferIds.TelemetryRing,
                ProceduralCoralConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                ProceduralCoralVaultBufferIds.TelemetryCursor,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = vault.EnsureGenerationHandle<CoralTuningDTO>(
                ProceduralCoralVaultBufferIds.Tuning,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.EnsureGenerationHandle<byte>(
                ProceduralCoralVaultBufferIds.CsvScratch,
                ProceduralCoralConstants.CsvScratchBytes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Counters = vault.EnsureGenerationHandle<CoralPaddedCounterDTO>(
                ProceduralCoralVaultBufferIds.Counters,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.DebugSegments = vault.EnsureGenerationHandle<CoralDebugSegmentDTO>(
                ProceduralCoralVaultBufferIds.DebugSegments,
                ProceduralCoralConstants.MaxDebugSegments,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.GpuSway = vault.EnsureGenerationHandle<CoralGpuSwayDTO>(
                ProceduralCoralVaultBufferIds.GpuSway,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.SelfAudit = vault.EnsureGenerationHandle<CoralSelfAuditResultDTO>(
                ProceduralCoralVaultBufferIds.SelfAudit,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.HzbTiles = vault.EnsureGenerationHandle<CoralHzbTileDTO>(
                ProceduralCoralVaultBufferIds.HzbTiles,
                ProceduralCoralConstants.MaxHzbTiles,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);

            if (!handles.IsCreated())
                return false;

            if (TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers))
                HydrateDefaultsIfNeeded(buffers);

            return true;
        }

        public static bool TryResolveExisting(IDataVault vault, out ProceduralCoralVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            return vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.Rules, out handles.Rules) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.InstructionScratchA, out handles.InstructionScratchA) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.InstructionScratchB, out handles.InstructionScratchB) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.Branches, out handles.Branches) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.TurtleStack, out handles.TurtleStack) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.SpatialCells, out handles.SpatialCells) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.RenderMatrices, out handles.RenderMatrices) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.IndirectArgs, out handles.IndirectArgs) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.SectorTriggers, out handles.SectorTriggers) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.CollisionProxies, out handles.CollisionProxies) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.SyncPulses, out handles.SyncPulses) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.CsvScratch, out handles.CsvScratch) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.Counters, out handles.Counters) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.DebugSegments, out handles.DebugSegments) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.GpuSway, out handles.GpuSway) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.SelfAudit, out handles.SelfAudit) &&
                   vault.TryGetGenerationHandle(ProceduralCoralVaultBufferIds.HzbTiles, out handles.HzbTiles);
        }

        public static bool TryResolveViews(IDataVault vault, ref ProceduralCoralVaultHandles handles, out ProceduralCoralVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            return TryResolveView(vault, in handles.Rules, out buffers.Rules) &&
                   TryResolveView(vault, in handles.InstructionScratchA, out buffers.InstructionScratchA) &&
                   TryResolveView(vault, in handles.InstructionScratchB, out buffers.InstructionScratchB) &&
                   TryResolveView(vault, in handles.Branches, out buffers.Branches) &&
                   TryResolveView(vault, in handles.TurtleStack, out buffers.TurtleStack) &&
                   TryResolveView(vault, in handles.SpatialCells, out buffers.SpatialCells) &&
                   TryResolveView(vault, in handles.RenderMatrices, out buffers.RenderMatrices) &&
                   TryResolveView(vault, in handles.IndirectArgs, out buffers.IndirectArgs) &&
                   TryResolveView(vault, in handles.SectorTriggers, out buffers.SectorTriggers) &&
                   TryResolveView(vault, in handles.CollisionProxies, out buffers.CollisionProxies) &&
                   TryResolveView(vault, in handles.SyncPulses, out buffers.SyncPulses) &&
                   TryResolveView(vault, in handles.TelemetryRing, out buffers.TelemetryRing) &&
                   TryResolveView(vault, in handles.TelemetryCursor, out buffers.TelemetryCursor) &&
                   TryResolveView(vault, in handles.Tuning, out buffers.Tuning) &&
                   TryResolveView(vault, in handles.CsvScratch, out buffers.CsvScratch) &&
                   TryResolveView(vault, in handles.Counters, out buffers.Counters) &&
                   TryResolveView(vault, in handles.DebugSegments, out buffers.DebugSegments) &&
                   TryResolveView(vault, in handles.GpuSway, out buffers.GpuSway) &&
                   TryResolveView(vault, in handles.SelfAudit, out buffers.SelfAudit) &&
                   TryResolveView(vault, in handles.HzbTiles, out buffers.HzbTiles) &&
                   buffers.IsCreated();
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

        public static bool TryScheduleMockSectorTrigger(
            in ProceduralCoralVaultBuffers buffers,
            double3 rootAup,
            uint worldSeed,
            uint simulationFrame,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!buffers.SectorTriggers.IsCreated || !buffers.Tuning.IsCreated || !buffers.Counters.IsCreated)
                return false;

            MockSectorTriggerJob job = default;
            job.SectorTriggers = buffers.SectorTriggers;
            job.Tuning = buffers.Tuning;
            job.Counters = buffers.Counters;
            job.MockRootAUP = rootAup;
            job.WorldSeed = worldSeed;
            job.SimulationFrameCounter = simulationFrame;
            outputDependency = job.Schedule(inputDependency);
            return true;
        }

        public static bool TryScheduleGenerationPipeline(
            in ProceduralCoralVaultBuffers buffers,
            double3 cameraAup,
            uint frame,
            uint vertexCountPerInstance,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            return TryScheduleGenerationPipeline(
                in buffers,
                cameraAup,
                float4x4.identity,
                0,
                0,
                frame,
                vertexCountPerInstance,
                inputDependency,
                out outputDependency);
        }

        public static bool TryScheduleGenerationPipeline(
            in ProceduralCoralVaultBuffers buffers,
            double3 cameraAup,
            float4x4 cameraRelativeViewProjection,
            int hzbWidth,
            int hzbHeight,
            uint frame,
            uint vertexCountPerInstance,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!buffers.IsCreated())
                return false;

            EvaluateCoralLSystemJob evaluate = default;
            evaluate.Rules = buffers.Rules;
            evaluate.SectorTriggers = buffers.SectorTriggers;
            evaluate.Tuning = buffers.Tuning;
            evaluate.InstructionScratchA = buffers.InstructionScratchA;
            evaluate.InstructionScratchB = buffers.InstructionScratchB;
            evaluate.Branches = buffers.Branches;
            evaluate.TurtleStack = buffers.TurtleStack;
            evaluate.DebugSegments = buffers.DebugSegments;
            evaluate.Counters = buffers.Counters;
            evaluate.TelemetryRing = buffers.TelemetryRing;
            evaluate.TelemetryCursor = buffers.TelemetryCursor;
            evaluate.Frame = frame;
            JobHandle evaluateHandle = evaluate.Schedule(inputDependency);

            ConstrainCoralGrowthJob constrain = default;
            constrain.Branches = buffers.Branches;
            constrain.SpatialCells = buffers.SpatialCells;
            constrain.SectorTriggers = buffers.SectorTriggers;
            constrain.Tuning = buffers.Tuning;
            constrain.Counters = buffers.Counters;
            JobHandle constrainHandle = constrain.Schedule(evaluateHandle);

            InjectBioluminescenceNodesJob pulses = default;
            pulses.Branches = buffers.Branches;
            pulses.Tuning = buffers.Tuning;
            pulses.SyncPulses = buffers.SyncPulses;
            pulses.Counters = buffers.Counters;
            JobHandle pulseHandle = pulses.Schedule(constrainHandle);

            StageCollisionProxiesJob collision = default;
            collision.Branches = buffers.Branches;
            collision.Tuning = buffers.Tuning;
            collision.CollisionProxies = buffers.CollisionProxies;
            collision.Counters = buffers.Counters;
            JobHandle collisionHandle = collision.Schedule(pulseHandle);

            ExtractCoralRenderMatricesJob extract = default;
            extract.Branches = buffers.Branches;
            extract.Tuning = buffers.Tuning;
            extract.HzbTiles = buffers.HzbTiles;
            extract.RenderMatrices = buffers.RenderMatrices;
            extract.IndirectArgs = buffers.IndirectArgs;
            extract.GpuSway = buffers.GpuSway;
            extract.Counters = buffers.Counters;
            extract.TelemetryRing = buffers.TelemetryRing;
            extract.TelemetryCursor = buffers.TelemetryCursor;
            extract.CameraAUP = cameraAup;
            extract.CameraRelativeViewProjection = cameraRelativeViewProjection;
            extract.HzbWidth = hzbWidth;
            extract.HzbHeight = hzbHeight;
            extract.VertexCountPerInstance = vertexCountPerInstance;
            extract.Frame = frame;
            JobHandle extractHandle = extract.Schedule(collisionHandle);

            CoralSelfAuditJob audit = default;
            audit.Branches = buffers.Branches;
            audit.Counters = buffers.Counters;
            audit.Results = buffers.SelfAudit;
            audit.Frame = frame;
            outputDependency = audit.Schedule(extractHandle);
            return true;
        }

        public static bool TryGetTuning(IDataVault vault, ref ProceduralCoralVaultHandles handles, out CoralTuningDTO tuning)
        {
            tuning = default;
            if (!TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            tuning = buffers.Tuning[0];
            return true;
        }

        public static bool TrySetTuning(IDataVault vault, ref ProceduralCoralVaultHandles handles, in CoralTuningDTO tuning)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            buffers.Tuning[0] = SanitizeTuning(in tuning);
            return true;
        }

        public static bool TryFindLegacyRuleBinary(string projectRoot, out string path)
        {
            path = null;
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return false;

            string direct = Path.Combine(projectRoot, "Assets", "StreamingAssets", BinaryRulesFileName);
            if (File.Exists(direct))
            {
                path = direct;
                return true;
            }

            try
            {
                string[] candidates = Directory.GetFiles(projectRoot, BinaryRulesFileName, SearchOption.AllDirectories);
                if (candidates.Length > 0)
                {
                    path = candidates[0];
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return false;
        }

        public static bool TryLoadBinaryRules(IDataVault vault, ref ProceduralCoralVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.Rules.IsCreated ||
                !buffers.CsvScratch.IsCreated ||
                !TryFindLegacyRuleBinary(projectRoot, out string path))
            {
                return false;
            }

            NativeArray<byte> ruleScratch = buffers.CsvScratch;
            int bytesRead = ReadFileIntoNativeScratch(path, ruleScratch);
            int loaded = ParseBinaryRules(ruleScratch, bytesRead, buffers.Rules);
            if (loaded <= 0)
                return false;

            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = buffers.Counters[0];
                counter.BinaryRuleCount = (uint)loaded;
                counter.ActiveRuleCount = (uint)loaded;
                buffers.Counters[0] = counter;
            }

            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadCsvRules(IDataVault vault, ref ProceduralCoralVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.Rules.IsCreated ||
                !buffers.CsvScratch.IsCreated)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            uint payloadHash = HashBytes(buffers.CsvScratch, bytesRead);
            return TryCommitCsvRules(buffers, bytesRead, payloadHash);
        }

        public static bool TryPollCsvRules(IDataVault vault, ref ProceduralCoralVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Rules.IsCreated ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            uint payloadHash = HashBytes(buffers.CsvScratch, bytesRead);
            if (payloadHash == buffers.Tuning[0].LastRulePayloadHash)
                return false;

            return TryCommitCsvRules(buffers, bytesRead, payloadHash);
        }

        private static bool TryCommitCsvRules(ProceduralCoralVaultBuffers buffers, int bytesRead, uint payloadHash)
        {
            if (!buffers.Rules.IsCreated || !buffers.CsvScratch.IsCreated)
                return false;

            int loaded = ParseCsvRules(buffers.CsvScratch, bytesRead, buffers.Rules);
            if (loaded <= 0)
                return false;

            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                CoralTuningDTO tuning = buffers.Tuning[0];
                tuning.LastRulePayloadHash = payloadHash;
                tuning.Version++;
                buffers.Tuning[0] = SanitizeTuning(in tuning);
            }

            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = buffers.Counters[0];
                counter.CsvRuleCount = (uint)loaded;
                counter.ActiveRuleCount = (uint)loaded;
                buffers.Counters[0] = counter;
            }

            return true;
        }
#endif

        public static void GenerateEmergencyMockCoralRules(NativeArray<CoralLSystemRuleDTO> rules)
        {
            if (!rules.IsCreated || rules.Length <= 0)
                return;

            ClearArray(rules);
            SetRule(
                rules,
                0,
                ProceduralCoralConstants.OpGrow,
                ProceduralCoralConstants.OpGrow,
                ProceduralCoralConstants.OpPush,
                ProceduralCoralConstants.OpTurnLeft,
                ProceduralCoralConstants.OpFork,
                ProceduralCoralConstants.OpTip,
                ProceduralCoralConstants.OpPop,
                ProceduralCoralConstants.OpThin,
                ProceduralCoralConstants.OpGrow,
                8,
                0.52f,
                0.96f,
                0.82f,
                0xC0110001u,
                CoralRuleFlags.EmitsBranch | CoralRuleFlags.TrunkRule);
            SetRule(
                rules,
                1,
                ProceduralCoralConstants.OpFork,
                ProceduralCoralConstants.OpPitchUp,
                ProceduralCoralConstants.OpGrow,
                ProceduralCoralConstants.OpPush,
                ProceduralCoralConstants.OpRoll,
                ProceduralCoralConstants.OpGrow,
                ProceduralCoralConstants.OpTip,
                ProceduralCoralConstants.OpPop,
                ProceduralCoralConstants.OpThin,
                8,
                0.44f,
                0.78f,
                0.72f,
                0xC0110002u,
                CoralRuleFlags.EmitsBranch | CoralRuleFlags.FineRule);
            SetRule(
                rules,
                2,
                ProceduralCoralConstants.OpThin,
                ProceduralCoralConstants.OpTurnRight,
                ProceduralCoralConstants.OpGrow,
                ProceduralCoralConstants.OpPush,
                ProceduralCoralConstants.OpPitchDown,
                ProceduralCoralConstants.OpFork,
                ProceduralCoralConstants.OpTip,
                ProceduralCoralConstants.OpPop,
                ProceduralCoralConstants.OpGrow,
                8,
                0.38f,
                0.66f,
                0.68f,
                0xC0110003u,
                CoralRuleFlags.EmitsTip | CoralRuleFlags.FineRule);
        }

        public static bool TryDumpBlackBox(in ProceduralCoralVaultBuffers buffers, string projectRoot, uint reason)
        {
            if (!buffers.TelemetryRing.IsCreated || string.IsNullOrEmpty(projectRoot))
                return false;

            try
            {
                bool primary = TryWriteDumpFile("Docs/AgentLogs/" + DumpFileName, in buffers, reason);
                bool agent = TryWriteDumpFile("Docs/AgentLogs/" + AgentDumpFileName, in buffers, reason);
                return primary && agent;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool TryDumpBlackBoxOnFault(in ProceduralCoralVaultBuffers buffers, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated ||
                !buffers.TelemetryCursor.IsCreated ||
                buffers.TelemetryRing.Length <= 0 ||
                buffers.TelemetryCursor.Length <= 0)
            {
                return false;
            }

            int cursor = buffers.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor = buffers.TelemetryRing.Length - 1;

            CoralGenerationTelemetryEntry entry = buffers.TelemetryRing[math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1)];
            if (entry.FaultFlags == 0u)
                return false;

            return TryDumpBlackBox(in buffers, projectRoot, entry.FaultFlags);
        }

        public static bool TryRecordMeasuredBurstTimeUs(ProceduralCoralVaultBuffers buffers, float burstComputeUs)
        {
            if (!buffers.TelemetryRing.IsCreated ||
                !buffers.TelemetryCursor.IsCreated ||
                buffers.TelemetryRing.Length <= 0 ||
                buffers.TelemetryCursor.Length <= 0)
            {
                return false;
            }

            int cursor = buffers.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor = buffers.TelemetryRing.Length - 1;

            cursor = math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1);
            CoralGenerationTelemetryEntry entry = buffers.TelemetryRing[cursor];
            if (!math.isfinite(burstComputeUs))
            {
                entry.BurstComputeUs = 0f;
                entry.FaultFlags |= ProceduralCoralConstants.FaultNonFinite;
            }
            else
            {
                entry.BurstComputeUs = math.max(burstComputeUs, 0f);
            }

            buffers.TelemetryRing[cursor] = entry;
            return true;
        }

        public static uint ComputeSectorHash(double3 rootAup)
        {
            return ProceduralCoralMath.HashDouble3(rootAup);
        }

        public static CoralSectorSaveDTO BuildSectorSaveRecord(double3 rootAup, uint worldSeed, uint rulePayloadHash)
        {
            uint sectorHash = ComputeSectorHash(rootAup);
            CoralSectorSaveDTO record = default;
            record.SectorHash = sectorHash;
            record.Seed = ProceduralCoralMath.Hash(sectorHash ^ worldSeed ^ 0xC0A17u);
            record.RulePayloadHash = rulePayloadHash;
            record.Flags = 1u;
            return record;
        }

        public static bool TryRunArchitectureAudit(ProceduralCoralVaultBuffers buffers, uint frame, out CoralSelfAuditResultDTO result)
        {
            result = default;
            uint flags = 0u;
            if (!buffers.IsCreated())
                flags |= ProceduralCoralConstants.FaultAuditVault;

            if (UnsafeUtility.SizeOf<CoralBranchDTO>() != 128 ||
                UnsafeUtility.SizeOf<CoralPaddedCounterDTO>() != 64 ||
                UnsafeUtility.SizeOf<CoralGenerationTelemetryEntry>() != 64 ||
                UnsafeUtility.SizeOf<CapsuleColliderDTO>() != 64)
            {
                flags |= ProceduralCoralConstants.FaultAuditLayout;
            }

            CoralPaddedCounterDTO counter = buffers.Counters.IsCreated && buffers.Counters.Length > 0
                ? buffers.Counters[0]
                : default;
            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0 && !math.isfinite(counter.EffectiveQualityWeight))
                flags |= ProceduralCoralConstants.FaultNonFinite;

            int branchCount = buffers.Counters.IsCreated && buffers.Counters.Length > 0
                ? math.max(0, counter.BranchCount)
                : 0;
            if (branchCount > ProceduralCoralConstants.MaxBranches ||
                (buffers.Branches.IsCreated && branchCount > buffers.Branches.Length))
            {
                flags |= ProceduralCoralConstants.FaultCapacity;
            }

            uint stateHash = 2166136261u;
            uint sectorHash = 0u;
            bool sectorHashCaptured = false;
            int live = 0;
            int tips = 0;
            int scanCount = buffers.Branches.IsCreated ? math.min(branchCount, buffers.Branches.Length) : 0;
            for (int i = 0; i < scanCount; i++)
            {
                CoralBranchDTO branch = buffers.Branches[i];
                if ((branch.StateFlags & CoralBranchFlags.Alive) == 0)
                    continue;

                live++;
                if (!sectorHashCaptured)
                {
                    sectorHash = branch.SectorHash;
                    sectorHashCaptured = true;
                }

                if ((branch.StateFlags & CoralBranchFlags.Tip) != 0)
                    tips++;

                stateHash = (stateHash ^ branch.StableId) * 16777619u;
                if (!ProceduralCoralMath.IsFinite(branch.LocalMatrix) || !ProceduralCoralMath.IsFinite(branch.SectorAUP))
                    flags |= ProceduralCoralConstants.FaultNonFinite;
            }

            result.Frame = frame;
            result.SectorHash = sectorHash;
            result.Flags = flags;
            result.LiveBranchCount = (uint)live;
            result.TipCount = (uint)tips;
            result.RenderMatrixCount = buffers.Counters.IsCreated && buffers.Counters.Length > 0 ? (uint)math.max(0, counter.RenderMatrixCount) : 0u;
            result.StateHash = stateHash;
            result.BranchUtilization = branchCount > 0
                ? (float)live / math.max(branchCount, 1)
                : 0f;

            if (buffers.SelfAudit.IsCreated && buffers.SelfAudit.Length > 0)
                buffers.SelfAudit[0] = result;

            return flags == 0u;
        }

        private static void HydrateDefaultsIfNeeded(ProceduralCoralVaultBuffers buffers)
        {
            bool firstHydration = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0 && buffers.Tuning[0].Version == 0u;
            if (!firstHydration)
                return;

            if (buffers.IndirectArgs.IsCreated && buffers.IndirectArgs.Length > 0)
                buffers.IndirectArgs[0] = default;
            if (buffers.SectorTriggers.IsCreated && buffers.SectorTriggers.Length > 0)
                buffers.SectorTriggers[0] = default;
            if (buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0)
                buffers.TelemetryCursor[0] = 0;
            if (buffers.GpuSway.IsCreated && buffers.GpuSway.Length > 0)
                buffers.GpuSway[0] = default;
            if (buffers.SelfAudit.IsCreated && buffers.SelfAudit.Length > 0)
                buffers.SelfAudit[0] = default;

            GenerateEmergencyMockCoralRules(buffers.Rules);
            CoralTuningDTO defaultTuning = BuildDefaultTuning();
            buffers.Tuning[0] = defaultTuning;
            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = default;
                counter.ActiveRuleCount = 3u;
                counter.EffectiveQualityWeight = defaultTuning.GlobalQualityWeight;
                buffers.Counters[0] = counter;
            }
        }

        private static CoralTuningDTO BuildDefaultTuning()
        {
            CoralTuningDTO tuning = default;
            tuning.GlobalQualityWeight = 0.5f;
            tuning.BranchAngleRadians = 0.52f;
            tuning.AngleVarianceRadians = 0.18f;
            tuning.BaseStepMeters = 1.6f;
            tuning.BaseRadiusMeters = 0.32f;
            tuning.RadiusDecay = 0.82f;
            tuning.SdfAvoidanceWeight = 0.55f;
            tuning.MaxDepth = 7;
            tuning.MaxBranches = 768;
            tuning.MaxInstructions = 2048;
            tuning.VisibilityDistanceMin = 48f;
            tuning.VisibilityDistanceMax = 360f;
            tuning.CurrentSwayAmplitude = 0.32f;
            tuning.Version = 1u;
            tuning.SeedSalt = 0xC0A17u;
            return tuning;
        }

        private static CoralTuningDTO SanitizeTuning(in CoralTuningDTO tuning)
        {
            CoralTuningDTO safe = tuning;
            safe.GlobalQualityWeight = ProceduralCoralMath.SafeSaturate(tuning.GlobalQualityWeight, 0.5f);
            safe.BranchAngleRadians = math.clamp(math.isfinite(tuning.BranchAngleRadians) ? tuning.BranchAngleRadians : 0.52f, 0.05f, 1.35f);
            safe.AngleVarianceRadians = ProceduralCoralMath.SafeSaturate(tuning.AngleVarianceRadians, 0.18f);
            safe.BaseStepMeters = ProceduralCoralMath.SafePositive(tuning.BaseStepMeters, 1.6f, ProceduralCoralConstants.Epsilon);
            safe.BaseRadiusMeters = ProceduralCoralMath.SafePositive(tuning.BaseRadiusMeters, 0.32f, ProceduralCoralConstants.Epsilon);
            safe.RadiusDecay = math.clamp(math.isfinite(tuning.RadiusDecay) ? tuning.RadiusDecay : 0.82f, 0.35f, 0.98f);
            safe.SdfAvoidanceWeight = ProceduralCoralMath.SafeSaturate(tuning.SdfAvoidanceWeight, 0.55f);
            safe.MaxDepth = math.clamp(tuning.MaxDepth, 1, 12);
            safe.MaxBranches = math.clamp(tuning.MaxBranches, 1, ProceduralCoralConstants.MaxBranches);
            safe.MaxInstructions = math.clamp(tuning.MaxInstructions, 1, ProceduralCoralConstants.MaxInstructions);
            safe.VisibilityDistanceMin = ProceduralCoralMath.SafePositive(tuning.VisibilityDistanceMin, 48f, 8f);
            safe.VisibilityDistanceMax = ProceduralCoralMath.SafePositive(tuning.VisibilityDistanceMax, 360f, safe.VisibilityDistanceMin);
            safe.CurrentSwayAmplitude = ProceduralCoralMath.SafeSaturate(tuning.CurrentSwayAmplitude, 0.32f);
            safe.Version = tuning.Version == 0u ? 1u : tuning.Version;
            safe.SeedSalt = tuning.SeedSalt == 0u ? 0xC0A17u : tuning.SeedSalt;
            return safe;
        }

        private static void SetRule(
            NativeArray<CoralLSystemRuleDTO> rules,
            int index,
            uint source,
            uint r0,
            uint r1,
            uint r2,
            uint r3,
            uint r4,
            uint r5,
            uint r6,
            uint r7,
            byte replacementCount,
            float angle,
            float lengthScale,
            float radiusScale,
            uint prefabHash,
            uint flags)
        {
            if ((uint)index >= (uint)rules.Length)
                return;

            CoralLSystemRuleDTO rule = default;
            rule.SourceOpcode = source;
            rule.Replacement0 = r0;
            rule.Replacement1 = r1;
            rule.Replacement2 = r2;
            rule.Replacement3 = r3;
            rule.Replacement4 = r4;
            rule.Replacement5 = r5;
            rule.Replacement6 = r6;
            rule.Replacement7 = r7;
            rule.ReplacementCount = replacementCount;
            rule.RuleIndex = (byte)index;
            rule.BranchAngleRadians = math.clamp(math.isfinite(angle) ? angle : 0.52f, ProceduralCoralConstants.Epsilon, 1.75f);
            rule.LengthScale = math.clamp(ProceduralCoralMath.SafePositive(lengthScale, 1f, ProceduralCoralConstants.Epsilon), 0.08f, 1.5f);
            rule.RadiusScale = math.clamp(ProceduralCoralMath.SafePositive(radiusScale, 1f, ProceduralCoralConstants.Epsilon), 0.08f, 1.25f);
            rule.PrefabHash = prefabHash;
            rule.Flags = flags;
            rule.WeightHash = ProceduralCoralMath.Hash(source ^ prefabHash ^ (uint)index);
            rules[index] = rule;
        }

        private static string ResolveCsvPath(string projectRoot)
        {
            return string.IsNullOrEmpty(projectRoot) ? null : Path.Combine(projectRoot, CsvRulesFileName);
        }

        private static int ParseBinaryRules(NativeArray<byte> bytes, int length, NativeArray<CoralLSystemRuleDTO> rules)
        {
            if (!bytes.IsCreated ||
                !rules.IsCreated ||
                length < ProceduralCoralConstants.RuleBinaryHeaderBytes)
            {
                return 0;
            }

            uint magic = ReadUInt32Little(bytes, 0);
            bool swapEndian = false;
            if (magic != ProceduralCoralConstants.RuleBinaryMagic)
            {
                uint swapped = ReverseBytes(magic);
                if (swapped != ProceduralCoralConstants.RuleBinaryMagic)
                    return 0;

                swapEndian = true;
            }

            uint version = ReadUInt32(bytes, 4, swapEndian);
            if (version != ProceduralCoralConstants.RuleBinaryVersion)
                return 0;

            int count = math.min((int)ReadUInt32(bytes, 8, swapEndian), math.min(rules.Length, ProceduralCoralConstants.MaxRules));
            int offset = ProceduralCoralConstants.RuleBinaryHeaderBytes;
            int written = 0;
            CoralLSystemRuleDTO* parsedRules = stackalloc CoralLSystemRuleDTO[ProceduralCoralConstants.MaxRules];
            ClearRuleScratch(parsedRules, ProceduralCoralConstants.MaxRules);
            for (int i = 0; i < count; i++)
            {
                if (offset + ProceduralCoralConstants.RuleBinaryRecordBytes > length)
                    break;

                if (TryReadBinaryRule(bytes, offset, swapEndian, out CoralLSystemRuleDTO rule))
                    parsedRules[written++] = rule;

                offset += ProceduralCoralConstants.RuleBinaryRecordBytes;
            }

            return CommitParsedRules(parsedRules, written, rules);
        }

        private static bool TryReadBinaryRule(NativeArray<byte> bytes, int offset, bool swapEndian, out CoralLSystemRuleDTO rule)
        {
            rule = default;
            uint source = ReadUInt32(bytes, offset, swapEndian);
            if (source == 0u)
                return false;

            rule.SourceOpcode = source;
            rule.Replacement0 = ReadUInt32(bytes, offset + 4, swapEndian);
            rule.Replacement1 = ReadUInt32(bytes, offset + 8, swapEndian);
            rule.Replacement2 = ReadUInt32(bytes, offset + 12, swapEndian);
            rule.Replacement3 = ReadUInt32(bytes, offset + 16, swapEndian);
            rule.Replacement4 = ReadUInt32(bytes, offset + 20, swapEndian);
            rule.Replacement5 = ReadUInt32(bytes, offset + 24, swapEndian);
            rule.Replacement6 = ReadUInt32(bytes, offset + 28, swapEndian);
            rule.Replacement7 = ReadUInt32(bytes, offset + 32, swapEndian);
            rule.ReplacementCount = (byte)math.clamp(bytes[offset + 36], 0, 8);
            rule.RuleIndex = bytes[offset + 37];
            float angle = ReadFloat32(bytes, offset + 40, swapEndian);
            float lengthScale = ReadFloat32(bytes, offset + 44, swapEndian);
            float radiusScale = ReadFloat32(bytes, offset + 48, swapEndian);
            rule.BranchAngleRadians = math.clamp(math.isfinite(angle) ? angle : 0.52f, ProceduralCoralConstants.Epsilon, 1.75f);
            rule.LengthScale = math.clamp(ProceduralCoralMath.SafePositive(lengthScale, 1f, ProceduralCoralConstants.Epsilon), 0.08f, 1.5f);
            rule.RadiusScale = math.clamp(ProceduralCoralMath.SafePositive(radiusScale, 1f, ProceduralCoralConstants.Epsilon), 0.08f, 1.25f);
            rule.PrefabHash = ReadUInt32(bytes, offset + 52, swapEndian);
            rule.Flags = ReadUInt32(bytes, offset + 56, swapEndian);
            rule.WeightHash = ReadUInt32(bytes, offset + 60, swapEndian);
            if (rule.WeightHash == 0u)
                rule.WeightHash = ProceduralCoralMath.Hash(rule.SourceOpcode ^ rule.PrefabHash);

            return math.isfinite(rule.BranchAngleRadians) && math.isfinite(rule.LengthScale) && math.isfinite(rule.RadiusScale);
        }

#if UNITY_EDITOR
        private static int ParseCsvRules(NativeArray<byte> bytes, int length, NativeArray<CoralLSystemRuleDTO> rules)
        {
            if (!bytes.IsCreated || !rules.IsCreated || length <= 0)
                return 0;

            int index = 0;
            int limit = math.min(length, bytes.Length);
            int ruleLimit = math.min(rules.Length, ProceduralCoralConstants.MaxRules);
            int written = 0;
            CoralLSystemRuleDTO* parsedRules = stackalloc CoralLSystemRuleDTO[ProceduralCoralConstants.MaxRules];
            ClearRuleScratch(parsedRules, ProceduralCoralConstants.MaxRules);
            while (index < limit && written < ruleLimit)
            {
                SkipWhitespace(bytes, limit, ref index);
                if (index >= limit)
                    break;

                byte first = bytes[index];
                if (first == (byte)'#')
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                int lineStart = index;
                if (!TryReadOpcode(bytes, limit, ref index, out uint source) || source == 0u)
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                if (source == HashAsciiLiteral("source"))
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                CoralLSystemRuleDTO rule = default;
                rule.SourceOpcode = source;
                byte replacementCount = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (!ConsumeComma(bytes, limit, ref index) || !TryReadOpcode(bytes, limit, ref index, out uint opcode))
                        break;

                    SetReplacement(ref rule, i, opcode);
                    if (opcode != 0u)
                        replacementCount++;
                }

                rule.ReplacementCount = replacementCount;
                rule.RuleIndex = (byte)written;
                rule.BranchAngleRadians = TryConsumeFloat(bytes, limit, ref index, out float angle)
                    ? math.clamp(math.isfinite(angle) ? angle : 0.52f, ProceduralCoralConstants.Epsilon, 1.75f)
                    : 0.52f;
                rule.LengthScale = TryConsumeFloat(bytes, limit, ref index, out float lengthScale)
                    ? math.clamp(ProceduralCoralMath.SafePositive(lengthScale, 0.9f, ProceduralCoralConstants.Epsilon), 0.08f, 1.5f)
                    : 0.9f;
                rule.RadiusScale = TryConsumeFloat(bytes, limit, ref index, out float radiusScale)
                    ? math.clamp(ProceduralCoralMath.SafePositive(radiusScale, 0.82f, ProceduralCoralConstants.Epsilon), 0.08f, 1.25f)
                    : 0.82f;
                rule.PrefabHash = TryConsumeUInt(bytes, limit, ref index, out uint prefabHash) ? prefabHash : ProceduralCoralMath.Hash(source ^ (uint)written);
                rule.Flags = TryConsumeUInt(bytes, limit, ref index, out uint flags) ? flags : CoralRuleFlags.EmitsBranch;
                rule.WeightHash = ProceduralCoralMath.Hash(source ^ rule.PrefabHash ^ (uint)lineStart);
                if (rule.ReplacementCount > 0)
                    parsedRules[written++] = rule;

                SkipLine(bytes, limit, ref index);
            }

            return CommitParsedRules(parsedRules, written, rules);
        }
#endif

        private static void ClearRuleScratch(CoralLSystemRuleDTO* rules, int length)
        {
            for (int i = 0; i < length; i++)
                rules[i] = default;
        }

        private static int CommitParsedRules(CoralLSystemRuleDTO* parsedRules, int parsedCount, NativeArray<CoralLSystemRuleDTO> rules)
        {
            if (parsedCount <= 0 || !rules.IsCreated || rules.Length <= 0)
                return 0;

            int count = math.min(parsedCount, rules.Length);
            for (int i = 0; i < count; i++)
                rules[i] = parsedRules[i];

            if (count < rules.Length)
                rules[count] = default;

            return count;
        }

        private static void SetReplacement(ref CoralLSystemRuleDTO rule, int index, uint opcode)
        {
            switch (index)
            {
                case 0:
                    rule.Replacement0 = opcode;
                    break;
                case 1:
                    rule.Replacement1 = opcode;
                    break;
                case 2:
                    rule.Replacement2 = opcode;
                    break;
                case 3:
                    rule.Replacement3 = opcode;
                    break;
                case 4:
                    rule.Replacement4 = opcode;
                    break;
                case 5:
                    rule.Replacement5 = opcode;
                    break;
                case 6:
                    rule.Replacement6 = opcode;
                    break;
                default:
                    rule.Replacement7 = opcode;
                    break;
            }
        }

        private static bool TryConsumeFloat(NativeArray<byte> bytes, int limit, ref int index, out float value)
        {
            value = 0f;
            if (!ConsumeComma(bytes, limit, ref index))
                return false;

            return TryReadFloat(bytes, limit, ref index, out value);
        }

        private static bool TryConsumeUInt(NativeArray<byte> bytes, int limit, ref int index, out uint value)
        {
            value = 0u;
            if (!ConsumeComma(bytes, limit, ref index))
                return false;

            return TryReadUInt(bytes, limit, ref index, out value);
        }

        private static bool TryReadOpcode(NativeArray<byte> bytes, int limit, ref int index, out uint opcode)
        {
            opcode = 0u;
            SkipValueWhitespace(bytes, limit, ref index);
            if (index >= limit)
                return false;

            if ((bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9') ||
                (index + 1 < limit && bytes[index] == (byte)'0' && (bytes[index + 1] == (byte)'x' || bytes[index + 1] == (byte)'X')))
            {
                return TryReadUInt(bytes, limit, ref index, out opcode);
            }

            uint hash = 2166136261u;
            while (index < limit)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                hash = ProceduralCoralMath.HashAsciiLower(c, hash);
                index++;
            }

            opcode = MapOpcodeHash(hash);
            return true;
        }

        private static uint MapOpcodeHash(uint hash)
        {
            if (hash == HashAsciiLiteral("grow") || hash == HashAsciiLiteral("f"))
                return ProceduralCoralConstants.OpGrow;
            if (hash == HashAsciiLiteral("left") || hash == HashAsciiLiteral("+"))
                return ProceduralCoralConstants.OpTurnLeft;
            if (hash == HashAsciiLiteral("right") || hash == HashAsciiLiteral("-"))
                return ProceduralCoralConstants.OpTurnRight;
            if (hash == HashAsciiLiteral("up") || hash == HashAsciiLiteral("^"))
                return ProceduralCoralConstants.OpPitchUp;
            if (hash == HashAsciiLiteral("down") || hash == HashAsciiLiteral("&"))
                return ProceduralCoralConstants.OpPitchDown;
            if (hash == HashAsciiLiteral("roll") || hash == HashAsciiLiteral("/"))
                return ProceduralCoralConstants.OpRoll;
            if (hash == HashAsciiLiteral("push") || hash == HashAsciiLiteral("["))
                return ProceduralCoralConstants.OpPush;
            if (hash == HashAsciiLiteral("pop") || hash == HashAsciiLiteral("]"))
                return ProceduralCoralConstants.OpPop;
            if (hash == HashAsciiLiteral("tip"))
                return ProceduralCoralConstants.OpTip;
            if (hash == HashAsciiLiteral("thin"))
                return ProceduralCoralConstants.OpThin;
            if (hash == HashAsciiLiteral("fork"))
                return ProceduralCoralConstants.OpFork;

            return hash;
        }

        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || string.IsNullOrEmpty(path))
                return 0;

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    int length = (int)math.min(stream.Length, scratch.Length);
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> span = new Span<byte>(ptr, length);
                    return stream.Read(span);
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static void ClearArray<T>(NativeArray<T> array) where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, array.Length * UnsafeUtility.SizeOf<T>());
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

        private static bool ConsumeComma(NativeArray<byte> bytes, int limit, ref int index)
        {
            SkipValueWhitespace(bytes, limit, ref index);
            if (index >= limit || bytes[index] != (byte)',')
                return false;

            index++;
            return true;
        }

        private static bool TryReadUInt(NativeArray<byte> bytes, int limit, ref int index, out uint value)
        {
            value = 0u;
            SkipValueWhitespace(bytes, limit, ref index);
            if (index >= limit)
                return false;

            bool hex = false;
            if (index + 1 < limit && bytes[index] == (byte)'0' && (bytes[index + 1] == (byte)'x' || bytes[index + 1] == (byte)'X'))
            {
                hex = true;
                index += 2;
            }

            bool readAny = false;
            while (index < limit)
            {
                byte c = bytes[index];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = 10u + (uint)(c - (byte)'a');
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = 10u + (uint)(c - (byte)'A');
                else
                    break;

                value = hex ? (value << 4) | digit : (value * 10u) + digit;
                readAny = true;
                index++;
            }

            return readAny;
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

        private static bool TryWriteDumpFile(string path, in ProceduralCoralVaultBuffers buffers, uint reason)
        {
            if (string.IsNullOrEmpty(path) || !buffers.TelemetryRing.IsCreated)
                return false;

            int entryBytes = UnsafeUtility.SizeOf<CoralGenerationTelemetryEntry>();
            int telemetryBytes = buffers.TelemetryRing.Length * entryBytes;
            int byteCount = 32 + telemetryBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(ProceduralCoralVault),
                "ProceduralCoralTelemetryDumpPayload");
            try
            {
                WriteUInt32(payload, 0, ProceduralCoralConstants.DumpMagic);
                WriteUInt32(payload, 4, ProceduralCoralConstants.DumpEndianMarker);
                WriteUInt32(payload, 8, DumpVersion);
                WriteUInt32(payload, 12, reason);
                WriteUInt32(payload, 16, (uint)buffers.TelemetryRing.Length);
                WriteUInt32(payload, 20, (uint)entryBytes);
                WriteUInt32(payload, 24, buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? (uint)buffers.TelemetryCursor[0] : 0u);
                WriteUInt32(payload, 28, 0u);

                void* payloadPtr = NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffers.TelemetryRing);
                UnsafeUtility.MemCpy((byte*)payloadPtr + 32, source, telemetryBytes);
                return Hecton8.Core.NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ProceduralCoralVault),
                    "ProceduralCoralTelemetryDumpPayload");
            }
        }

        private static void WriteUInt32(NativeArray<byte> target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUInt32Little(NativeArray<byte> bytes, int offset)
        {
            return (uint)bytes[offset] |
                   ((uint)bytes[offset + 1] << 8) |
                   ((uint)bytes[offset + 2] << 16) |
                   ((uint)bytes[offset + 3] << 24);
        }

        private static uint ReadUInt32(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            uint value = ReadUInt32Little(bytes, offset);
            return swapEndian ? ReverseBytes(value) : value;
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static float ReadFloat32(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            return math.asfloat(ReadUInt32(bytes, offset, swapEndian));
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
