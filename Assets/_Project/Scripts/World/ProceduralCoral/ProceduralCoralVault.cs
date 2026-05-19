using System;
using System.IO;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralCoral
{
    public struct ProceduralCoralVaultHandles
    {
        public VaultBufferHandle<CoralLSystemRuleDTO> Rules;
        public VaultBufferHandle<uint> InstructionScratchA;
        public VaultBufferHandle<uint> InstructionScratchB;
        public VaultBufferHandle<CoralBranchDTO> Branches;
        public VaultBufferHandle<CoralTurtleStateDTO> TurtleStack;
        public VaultBufferHandle<CoralSpatialCellDTO> SpatialCells;
        public VaultBufferHandle<float4x4> RenderMatrices;
        public VaultBufferHandle<CoralIndirectArgsDTO> IndirectArgs;
        public VaultBufferHandle<CoralSectorTriggerDTO> SectorTriggers;
        public VaultBufferHandle<CapsuleColliderDTO> CollisionProxies;
        public VaultBufferHandle<SyncPulseDTO> SyncPulses;
        public VaultBufferHandle<CoralGenerationTelemetryEntry> TelemetryRing;
        public VaultBufferHandle<int> TelemetryCursor;
        public VaultBufferHandle<CoralTuningDTO> Tuning;
        public VaultBufferHandle<byte> CsvScratch;
        public VaultBufferHandle<CoralPaddedCounterDTO> Counters;
        public VaultBufferHandle<CoralDebugSegmentDTO> DebugSegments;
        public VaultBufferHandle<CoralGpuSwayDTO> GpuSway;
        public VaultBufferHandle<CoralSelfAuditResultDTO> SelfAudit;

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
                   SelfAudit.IsCreated;
        }
    }

    public struct ProceduralCoralVaultBuffers
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
                   SelfAudit.IsCreated;
        }
    }

    public static unsafe class ProceduralCoralVault
    {
        private const int DumpVersion = 1;
        private const string BinaryRulesFileName = "coral_growth_rules.h8bin";
        private const string CsvRulesFileName = "coral_lsystem_rules.csv";
        private const string DumpFileName = "Dump_CORAL_ARCHITECT.bin";
        private const string AgentDumpFileName = "Dump_SHINOBU_139.bin";

        public static bool TryResolve(IDataVault vault, out ProceduralCoralVaultHandles handles)
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

            handles.Rules = vault.GetBufferHandle<CoralLSystemRuleDTO>(
                ProceduralCoralVaultBufferIds.Rules,
                ProceduralCoralConstants.MaxRules,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.InstructionScratchA = vault.GetBufferHandle<uint>(
                ProceduralCoralVaultBufferIds.InstructionScratchA,
                ProceduralCoralConstants.MaxInstructions,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.InstructionScratchB = vault.GetBufferHandle<uint>(
                ProceduralCoralVaultBufferIds.InstructionScratchB,
                ProceduralCoralConstants.MaxInstructions,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Branches = vault.GetBufferHandle<CoralBranchDTO>(
                ProceduralCoralVaultBufferIds.Branches,
                ProceduralCoralConstants.MaxBranches,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.TurtleStack = vault.GetBufferHandle<CoralTurtleStateDTO>(
                ProceduralCoralVaultBufferIds.TurtleStack,
                ProceduralCoralConstants.MaxTurtleStack,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.SpatialCells = vault.GetBufferHandle<CoralSpatialCellDTO>(
                ProceduralCoralVaultBufferIds.SpatialCells,
                ProceduralCoralConstants.MaxSpatialCells,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.RenderMatrices = vault.GetBufferHandle<float4x4>(
                ProceduralCoralVaultBufferIds.RenderMatrices,
                ProceduralCoralConstants.MaxRenderMatrices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.IndirectArgs = vault.GetBufferHandle<CoralIndirectArgsDTO>(
                ProceduralCoralVaultBufferIds.IndirectArgs,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.SectorTriggers = vault.GetBufferHandle<CoralSectorTriggerDTO>(
                ProceduralCoralVaultBufferIds.SectorTriggers,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CollisionProxies = vault.GetBufferHandle<CapsuleColliderDTO>(
                ProceduralCoralVaultBufferIds.CollisionProxies,
                ProceduralCoralConstants.MaxCollisionProxies,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.SyncPulses = vault.GetBufferHandle<SyncPulseDTO>(
                ProceduralCoralVaultBufferIds.SyncPulses,
                ProceduralCoralConstants.MaxSyncPulses,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.GetBufferHandle<CoralGenerationTelemetryEntry>(
                ProceduralCoralVaultBufferIds.TelemetryRing,
                ProceduralCoralConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.GetBufferHandle<int>(
                ProceduralCoralVaultBufferIds.TelemetryCursor,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = vault.GetBufferHandle<CoralTuningDTO>(
                ProceduralCoralVaultBufferIds.Tuning,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.GetBufferHandle<byte>(
                ProceduralCoralVaultBufferIds.CsvScratch,
                ProceduralCoralConstants.CsvScratchBytes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Counters = vault.GetBufferHandle<CoralPaddedCounterDTO>(
                ProceduralCoralVaultBufferIds.Counters,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.DebugSegments = vault.GetBufferHandle<CoralDebugSegmentDTO>(
                ProceduralCoralVaultBufferIds.DebugSegments,
                ProceduralCoralConstants.MaxDebugSegments,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.GpuSway = vault.GetBufferHandle<CoralGpuSwayDTO>(
                ProceduralCoralVaultBufferIds.GpuSway,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.SelfAudit = vault.GetBufferHandle<CoralSelfAuditResultDTO>(
                ProceduralCoralVaultBufferIds.SelfAudit,
                1,
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

            return vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.Rules, out handles.Rules) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.InstructionScratchA, out handles.InstructionScratchA) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.InstructionScratchB, out handles.InstructionScratchB) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.Branches, out handles.Branches) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.TurtleStack, out handles.TurtleStack) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.SpatialCells, out handles.SpatialCells) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.RenderMatrices, out handles.RenderMatrices) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.IndirectArgs, out handles.IndirectArgs) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.SectorTriggers, out handles.SectorTriggers) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.CollisionProxies, out handles.CollisionProxies) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.SyncPulses, out handles.SyncPulses) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.CsvScratch, out handles.CsvScratch) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.Counters, out handles.Counters) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.DebugSegments, out handles.DebugSegments) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.GpuSway, out handles.GpuSway) &&
                   vault.TryGetBufferHandle(ProceduralCoralVaultBufferIds.SelfAudit, out handles.SelfAudit);
        }

        public static bool TryResolveViews(IDataVault vault, ref ProceduralCoralVaultHandles handles, out ProceduralCoralVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            buffers.Rules = handles.Rules.Resolve(vault);
            buffers.InstructionScratchA = handles.InstructionScratchA.Resolve(vault);
            buffers.InstructionScratchB = handles.InstructionScratchB.Resolve(vault);
            buffers.Branches = handles.Branches.Resolve(vault);
            buffers.TurtleStack = handles.TurtleStack.Resolve(vault);
            buffers.SpatialCells = handles.SpatialCells.Resolve(vault);
            buffers.RenderMatrices = handles.RenderMatrices.Resolve(vault);
            buffers.IndirectArgs = handles.IndirectArgs.Resolve(vault);
            buffers.SectorTriggers = handles.SectorTriggers.Resolve(vault);
            buffers.CollisionProxies = handles.CollisionProxies.Resolve(vault);
            buffers.SyncPulses = handles.SyncPulses.Resolve(vault);
            buffers.TelemetryRing = handles.TelemetryRing.Resolve(vault);
            buffers.TelemetryCursor = handles.TelemetryCursor.Resolve(vault);
            buffers.Tuning = handles.Tuning.Resolve(vault);
            buffers.CsvScratch = handles.CsvScratch.Resolve(vault);
            buffers.Counters = handles.Counters.Resolve(vault);
            buffers.DebugSegments = handles.DebugSegments.Resolve(vault);
            buffers.GpuSway = handles.GpuSway.Resolve(vault);
            buffers.SelfAudit = handles.SelfAudit.Resolve(vault);
            return buffers.IsCreated();
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
            extract.RenderMatrices = buffers.RenderMatrices;
            extract.IndirectArgs = buffers.IndirectArgs;
            extract.GpuSway = buffers.GpuSway;
            extract.Counters = buffers.Counters;
            extract.TelemetryRing = buffers.TelemetryRing;
            extract.TelemetryCursor = buffers.TelemetryCursor;
            extract.CameraAUP = cameraAup;
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

            foreach (string candidate in Directory.EnumerateFiles(projectRoot, BinaryRulesFileName, SearchOption.AllDirectories))
            {
                path = candidate;
                return true;
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

            int bytesRead = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            int loaded = ParseBinaryRules(buffers.CsvScratch, bytesRead, buffers.Rules);
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
            int loaded = ParseCsvRules(buffers.CsvScratch, bytesRead, buffers.Rules);
            if (loaded <= 0)
                return false;

            uint payloadHash = HashBytes(buffers.CsvScratch, bytesRead);
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

        public static bool TryPollCsvRules(IDataVault vault, ref ProceduralCoralVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
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

            return TryLoadCsvRules(vault, ref handles, projectRoot);
        }

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

            string dir = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(dir);
            bool primary = TryWriteDumpFile(Path.Combine(dir, DumpFileName), in buffers, reason);
            bool agent = TryWriteDumpFile(Path.Combine(dir, AgentDumpFileName), in buffers, reason);
            return primary && agent;
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

        private static void HydrateDefaultsIfNeeded(ProceduralCoralVaultBuffers buffers)
        {
            bool firstHydration = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0 && buffers.Tuning[0].Version == 0u;
            if (!firstHydration)
                return;

            ClearArray(buffers.InstructionScratchA);
            ClearArray(buffers.InstructionScratchB);
            ClearArray(buffers.Branches);
            ClearArray(buffers.TurtleStack);
            ClearArray(buffers.SpatialCells);
            ClearArray(buffers.RenderMatrices);
            ClearArray(buffers.IndirectArgs);
            ClearArray(buffers.SectorTriggers);
            ClearArray(buffers.CollisionProxies);
            ClearArray(buffers.SyncPulses);
            ClearArray(buffers.TelemetryRing);
            ClearArray(buffers.TelemetryCursor);
            ClearArray(buffers.CsvScratch);
            ClearArray(buffers.Counters);
            ClearArray(buffers.DebugSegments);
            ClearArray(buffers.GpuSway);
            ClearArray(buffers.SelfAudit);
            GenerateEmergencyMockCoralRules(buffers.Rules);
            buffers.Tuning[0] = BuildDefaultTuning();
            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = default;
                counter.ActiveRuleCount = 3u;
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
            safe.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            safe.BranchAngleRadians = math.clamp(tuning.BranchAngleRadians, 0.05f, 1.35f);
            safe.AngleVarianceRadians = math.saturate(tuning.AngleVarianceRadians);
            safe.BaseStepMeters = math.max(tuning.BaseStepMeters, ProceduralCoralConstants.Epsilon);
            safe.BaseRadiusMeters = math.max(tuning.BaseRadiusMeters, ProceduralCoralConstants.Epsilon);
            safe.RadiusDecay = math.clamp(tuning.RadiusDecay, 0.35f, 0.98f);
            safe.SdfAvoidanceWeight = math.saturate(tuning.SdfAvoidanceWeight);
            safe.MaxDepth = math.clamp(tuning.MaxDepth, 1, 12);
            safe.MaxBranches = math.clamp(tuning.MaxBranches, 1, ProceduralCoralConstants.MaxBranches);
            safe.MaxInstructions = math.clamp(tuning.MaxInstructions, 1, ProceduralCoralConstants.MaxInstructions);
            safe.VisibilityDistanceMin = math.max(tuning.VisibilityDistanceMin, 8f);
            safe.VisibilityDistanceMax = math.max(tuning.VisibilityDistanceMax, safe.VisibilityDistanceMin);
            safe.CurrentSwayAmplitude = math.saturate(tuning.CurrentSwayAmplitude);
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
            rule.BranchAngleRadians = math.max(angle, ProceduralCoralConstants.Epsilon);
            rule.LengthScale = math.max(lengthScale, ProceduralCoralConstants.Epsilon);
            rule.RadiusScale = math.max(radiusScale, ProceduralCoralConstants.Epsilon);
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
                uint swapped = math.reversebytes(magic);
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
            ClearArray(rules);
            for (int i = 0; i < count; i++)
            {
                if (offset + ProceduralCoralConstants.RuleBinaryRecordBytes > length)
                    break;

                if (TryReadBinaryRule(bytes, offset, swapEndian, out CoralLSystemRuleDTO rule))
                    rules[written++] = rule;

                offset += ProceduralCoralConstants.RuleBinaryRecordBytes;
            }

            return written;
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
            rule.BranchAngleRadians = math.max(ReadFloat32(bytes, offset + 40, swapEndian), ProceduralCoralConstants.Epsilon);
            rule.LengthScale = math.max(ReadFloat32(bytes, offset + 44, swapEndian), ProceduralCoralConstants.Epsilon);
            rule.RadiusScale = math.max(ReadFloat32(bytes, offset + 48, swapEndian), ProceduralCoralConstants.Epsilon);
            rule.PrefabHash = ReadUInt32(bytes, offset + 52, swapEndian);
            rule.Flags = ReadUInt32(bytes, offset + 56, swapEndian);
            rule.WeightHash = ReadUInt32(bytes, offset + 60, swapEndian);
            if (rule.WeightHash == 0u)
                rule.WeightHash = ProceduralCoralMath.Hash(rule.SourceOpcode ^ rule.PrefabHash);

            return math.isfinite(rule.BranchAngleRadians) && math.isfinite(rule.LengthScale) && math.isfinite(rule.RadiusScale);
        }

        private static int ParseCsvRules(NativeArray<byte> bytes, int length, NativeArray<CoralLSystemRuleDTO> rules)
        {
            if (!bytes.IsCreated || !rules.IsCreated || length <= 0)
                return 0;

            int index = 0;
            int limit = math.min(length, bytes.Length);
            int written = 0;
            ClearArray(rules);
            while (index < limit && written < rules.Length)
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
                rule.BranchAngleRadians = TryConsumeFloat(bytes, limit, ref index, out float angle) ? math.max(angle, ProceduralCoralConstants.Epsilon) : 0.52f;
                rule.LengthScale = TryConsumeFloat(bytes, limit, ref index, out float lengthScale) ? math.max(lengthScale, ProceduralCoralConstants.Epsilon) : 0.9f;
                rule.RadiusScale = TryConsumeFloat(bytes, limit, ref index, out float radiusScale) ? math.max(radiusScale, ProceduralCoralConstants.Epsilon) : 0.82f;
                rule.PrefabHash = TryConsumeUInt(bytes, limit, ref index, out uint prefabHash) ? prefabHash : ProceduralCoralMath.Hash(source ^ (uint)written);
                rule.Flags = TryConsumeUInt(bytes, limit, ref index, out uint flags) ? flags : CoralRuleFlags.EmitsBranch;
                rule.WeightHash = ProceduralCoralMath.Hash(source ^ rule.PrefabHash ^ (uint)lineStart);
                if (rule.ReplacementCount > 0)
                    rules[written++] = rule;

                SkipLine(bytes, limit, ref index);
            }

            return written;
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

            using (FileStream stream = File.OpenRead(path))
            {
                int length = (int)math.min(stream.Length, scratch.Length);
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                Span<byte> span = new Span<byte>(ptr, length);
                return stream.Read(span);
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

            Span<byte> header = stackalloc byte[32];
            WriteUInt32(header, 0, ProceduralCoralConstants.DumpMagic);
            WriteUInt32(header, 4, ProceduralCoralConstants.DumpEndianMarker);
            WriteUInt32(header, 8, DumpVersion);
            WriteUInt32(header, 12, reason);
            WriteUInt32(header, 16, (uint)buffers.TelemetryRing.Length);
            WriteUInt32(header, 20, (uint)UnsafeUtility.SizeOf<CoralGenerationTelemetryEntry>());
            WriteUInt32(header, 24, buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? (uint)buffers.TelemetryCursor[0] : 0u);
            WriteUInt32(header, 28, 0u);

            using (FileStream stream = File.Create(path))
            {
                stream.Write(header);
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffers.TelemetryRing);
                int byteLength = buffers.TelemetryRing.Length * UnsafeUtility.SizeOf<CoralGenerationTelemetryEntry>();
                ReadOnlySpan<byte> telemetry = new ReadOnlySpan<byte>(ptr, byteLength);
                stream.Write(telemetry);
            }

            return true;
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
            return swapEndian ? math.reversebytes(value) : value;
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
