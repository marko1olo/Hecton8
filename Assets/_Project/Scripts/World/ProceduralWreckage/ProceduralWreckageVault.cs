using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralWreckage
{
    public struct ProceduralWreckageVaultHandles
    {
        public VaultGenerationHandle<WreckageRuleDTO> Rules;
        public VaultGenerationHandle<WreckageGridCellDTO> Grid;
        public VaultGenerationHandle<WreckageNodeDTO> Nodes;
        public VaultGenerationHandle<WreckageNodeDTO> DebrisNodes;
        public VaultGenerationHandle<float4x4> RenderMatrices;
        public VaultGenerationHandle<WreckageIndirectArgsDTO> IndirectArgs;
        public VaultGenerationHandle<WreckageSectorTriggerDTO> SectorTriggers;
        public VaultGenerationHandle<LootSpawnRequestDTO> LootRequests;
        public VaultGenerationHandle<WreckageBoxColliderDTO> CollisionProxies;
        public VaultGenerationHandle<WreckageGenerationTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<WreckageTuningDTO> Tuning;
        public VaultGenerationHandle<byte> CsvScratch;
        public VaultGenerationHandle<WreckagePaddedCounterDTO> Counters;
        public VaultGenerationHandle<WreckageDebugCellDTO> DebugCells;
        public VaultGenerationHandle<WreckageGpuScalarDTO> GpuScalars;
        public VaultGenerationHandle<WreckageSelfAuditResultDTO> SelfAudit;
        public VaultGenerationHandle<WreckageHzbTileDTO> HzbTiles;

        public bool IsCreated()
        {
            return IsHandleValid(in Rules) &&
                   IsHandleValid(in Grid) &&
                   IsHandleValid(in Nodes) &&
                   IsHandleValid(in DebrisNodes) &&
                   IsHandleValid(in RenderMatrices) &&
                   IsHandleValid(in IndirectArgs) &&
                   IsHandleValid(in SectorTriggers) &&
                   IsHandleValid(in LootRequests) &&
                   IsHandleValid(in CollisionProxies) &&
                   IsHandleValid(in TelemetryRing) &&
                   IsHandleValid(in TelemetryCursor) &&
                   IsHandleValid(in Tuning) &&
                   IsHandleValid(in CsvScratch) &&
                   IsHandleValid(in Counters) &&
                   IsHandleValid(in DebugCells) &&
                   IsHandleValid(in GpuScalars) &&
                   IsHandleValid(in SelfAudit) &&
                   IsHandleValid(in HzbTiles);
        }

        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }
    }

    public ref struct ProceduralWreckageVaultBuffers
    {
        public NativeArray<WreckageRuleDTO> Rules;
        public NativeArray<WreckageGridCellDTO> Grid;
        public NativeArray<WreckageNodeDTO> Nodes;
        public NativeArray<WreckageNodeDTO> DebrisNodes;
        public NativeArray<float4x4> RenderMatrices;
        public NativeArray<WreckageIndirectArgsDTO> IndirectArgs;
        public NativeArray<WreckageSectorTriggerDTO> SectorTriggers;
        public NativeArray<LootSpawnRequestDTO> LootRequests;
        public NativeArray<WreckageBoxColliderDTO> CollisionProxies;
        public NativeArray<WreckageGenerationTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<WreckageTuningDTO> Tuning;
        public NativeArray<byte> CsvScratch;
        public NativeArray<WreckagePaddedCounterDTO> Counters;
        public NativeArray<WreckageDebugCellDTO> DebugCells;
        public NativeArray<WreckageGpuScalarDTO> GpuScalars;
        public NativeArray<WreckageSelfAuditResultDTO> SelfAudit;
        public NativeArray<WreckageHzbTileDTO> HzbTiles;

        public bool IsCreated()
        {
            return Rules.IsCreated &&
                   Grid.IsCreated &&
                   Nodes.IsCreated &&
                   DebrisNodes.IsCreated &&
                   RenderMatrices.IsCreated &&
                   IndirectArgs.IsCreated &&
                   SectorTriggers.IsCreated &&
                   LootRequests.IsCreated &&
                   CollisionProxies.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   Tuning.IsCreated &&
                   CsvScratch.IsCreated &&
                   Counters.IsCreated &&
                   DebugCells.IsCreated &&
                   GpuScalars.IsCreated &&
                   SelfAudit.IsCreated &&
                   HzbTiles.IsCreated;
        }
    }

    public static unsafe class ProceduralWreckageVault
    {
        private const int DumpVersion = 1;
        private const string BinaryRulesFileName = "wreckage_module_rules.h8bin";
        private const string CsvRulesFileName = "wreckage_adjacency_rules.csv";
        private const string DumpFileName = "Dump_WRECKAGE_ASSEMBLER.bin";
        private const string AgentDumpFileName = "Dump_SHINOBU_121.bin";

        public static bool TryEnsure(IDataVault vault, out ProceduralWreckageVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryResolveExisting(vault, out handles))
                    return false;

                if (TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers lockedBuffers))
                    HydrateDefaultsIfNeeded(lockedBuffers);

                return handles.IsCreated();
            }

            handles.Rules = vault.EnsureGenerationHandle<WreckageRuleDTO>(
                ProceduralWreckageVaultBufferIds.Rules,
                ProceduralWreckageConstants.MaxModuleRules,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Grid = vault.EnsureGenerationHandle<WreckageGridCellDTO>(
                ProceduralWreckageVaultBufferIds.Grid,
                ProceduralWreckageConstants.MaxGridCells,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Nodes = vault.EnsureGenerationHandle<WreckageNodeDTO>(
                ProceduralWreckageVaultBufferIds.Nodes,
                ProceduralWreckageConstants.MaxWreckNodes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.DebrisNodes = vault.EnsureGenerationHandle<WreckageNodeDTO>(
                ProceduralWreckageVaultBufferIds.DebrisNodes,
                ProceduralWreckageConstants.MaxDebrisNodes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.RenderMatrices = vault.EnsureGenerationHandle<float4x4>(
                ProceduralWreckageVaultBufferIds.RenderMatrices,
                ProceduralWreckageConstants.MaxRenderMatrices,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.IndirectArgs = vault.EnsureGenerationHandle<WreckageIndirectArgsDTO>(
                ProceduralWreckageVaultBufferIds.IndirectArgs,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.SectorTriggers = vault.EnsureGenerationHandle<WreckageSectorTriggerDTO>(
                ProceduralWreckageVaultBufferIds.SectorTriggers,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.LootRequests = vault.EnsureGenerationHandle<LootSpawnRequestDTO>(
                ProceduralWreckageVaultBufferIds.LootRequests,
                ProceduralWreckageConstants.MaxLootRequests,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.CollisionProxies = vault.EnsureGenerationHandle<WreckageBoxColliderDTO>(
                ProceduralWreckageVaultBufferIds.CollisionProxies,
                ProceduralWreckageConstants.MaxCollisionProxies,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<WreckageGenerationTelemetryEntry>(
                ProceduralWreckageVaultBufferIds.TelemetryRing,
                ProceduralWreckageConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                ProceduralWreckageVaultBufferIds.TelemetryCursor,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = vault.EnsureGenerationHandle<WreckageTuningDTO>(
                ProceduralWreckageVaultBufferIds.Tuning,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.CsvScratch = vault.EnsureGenerationHandle<byte>(
                ProceduralWreckageVaultBufferIds.CsvScratch,
                ProceduralWreckageConstants.CsvScratchBytes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.Counters = vault.EnsureGenerationHandle<WreckagePaddedCounterDTO>(
                ProceduralWreckageVaultBufferIds.Counters,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.DebugCells = vault.EnsureGenerationHandle<WreckageDebugCellDTO>(
                ProceduralWreckageVaultBufferIds.DebugCells,
                ProceduralWreckageConstants.MaxDebugCells,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            handles.GpuScalars = vault.EnsureGenerationHandle<WreckageGpuScalarDTO>(
                ProceduralWreckageVaultBufferIds.GpuScalars,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.SelfAudit = vault.EnsureGenerationHandle<WreckageSelfAuditResultDTO>(
                ProceduralWreckageVaultBufferIds.SelfAudit,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            handles.HzbTiles = vault.EnsureGenerationHandle<WreckageHzbTileDTO>(
                ProceduralWreckageVaultBufferIds.HzbTiles,
                ProceduralWreckageConstants.MaxHzbTiles,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);

            if (!handles.IsCreated())
                return false;

            if (TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers buffers))
                HydrateDefaultsIfNeeded(buffers);

            return true;
        }

        public static bool TryResolveExisting(IDataVault vault, out ProceduralWreckageVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            return vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.Rules, out handles.Rules) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.Grid, out handles.Grid) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.Nodes, out handles.Nodes) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.DebrisNodes, out handles.DebrisNodes) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.RenderMatrices, out handles.RenderMatrices) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.IndirectArgs, out handles.IndirectArgs) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.SectorTriggers, out handles.SectorTriggers) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.LootRequests, out handles.LootRequests) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.CollisionProxies, out handles.CollisionProxies) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.CsvScratch, out handles.CsvScratch) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.Counters, out handles.Counters) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.DebugCells, out handles.DebugCells) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.GpuScalars, out handles.GpuScalars) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.SelfAudit, out handles.SelfAudit) &&
                   vault.TryGetGenerationHandle(ProceduralWreckageVaultBufferIds.HzbTiles, out handles.HzbTiles);
        }

        public static bool TryResolveViews(IDataVault vault, ref ProceduralWreckageVaultHandles handles, out ProceduralWreckageVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            return TryResolveView(vault, in handles.Rules, out buffers.Rules) &&
                   TryResolveView(vault, in handles.Grid, out buffers.Grid) &&
                   TryResolveView(vault, in handles.Nodes, out buffers.Nodes) &&
                   TryResolveView(vault, in handles.DebrisNodes, out buffers.DebrisNodes) &&
                   TryResolveView(vault, in handles.RenderMatrices, out buffers.RenderMatrices) &&
                   TryResolveView(vault, in handles.IndirectArgs, out buffers.IndirectArgs) &&
                   TryResolveView(vault, in handles.SectorTriggers, out buffers.SectorTriggers) &&
                   TryResolveView(vault, in handles.LootRequests, out buffers.LootRequests) &&
                   TryResolveView(vault, in handles.CollisionProxies, out buffers.CollisionProxies) &&
                   TryResolveView(vault, in handles.TelemetryRing, out buffers.TelemetryRing) &&
                   TryResolveView(vault, in handles.TelemetryCursor, out buffers.TelemetryCursor) &&
                   TryResolveView(vault, in handles.Tuning, out buffers.Tuning) &&
                   TryResolveView(vault, in handles.CsvScratch, out buffers.CsvScratch) &&
                   TryResolveView(vault, in handles.Counters, out buffers.Counters) &&
                   TryResolveView(vault, in handles.DebugCells, out buffers.DebugCells) &&
                   TryResolveView(vault, in handles.GpuScalars, out buffers.GpuScalars) &&
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
            in ProceduralWreckageVaultBuffers buffers,
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
            in ProceduralWreckageVaultBuffers buffers,
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

            WreckageCollapseJob collapse = default;
            collapse.Grid = buffers.Grid;
            collapse.Rules = buffers.Rules;
            collapse.SectorTriggers = buffers.SectorTriggers;
            collapse.Tuning = buffers.Tuning;
            collapse.Nodes = buffers.Nodes;
            collapse.DebugCells = buffers.DebugCells;
            collapse.Counters = buffers.Counters;
            collapse.TelemetryRing = buffers.TelemetryRing;
            collapse.TelemetryCursor = buffers.TelemetryCursor;
            collapse.Frame = frame;
            JobHandle collapseHandle = collapse.Schedule(inputDependency);

            ApplyStructuralShearJob shear = default;
            shear.Nodes = buffers.Nodes;
            shear.Tuning = buffers.Tuning;
            shear.Frame = frame;
            JobHandle shearHandle = shear.Schedule(buffers.Nodes.Length, 32, collapseHandle);

            GenerateDebrisFieldJob debris = default;
            debris.SectorTriggers = buffers.SectorTriggers;
            debris.Tuning = buffers.Tuning;
            debris.DebrisNodes = buffers.DebrisNodes;
            debris.Counters = buffers.Counters;
            debris.Frame = frame;
            JobHandle debrisHandle = debris.Schedule(shearHandle);

            InjectLootRequestsJob loot = default;
            loot.Nodes = buffers.Nodes;
            loot.LootRequests = buffers.LootRequests;
            loot.Counters = buffers.Counters;
            loot.LootTableHash = HashAsciiLiteral("wreckage_loot_table");
            JobHandle lootHandle = loot.Schedule(debrisHandle);

            StageCollisionProxiesJob collision = default;
            collision.Nodes = buffers.Nodes;
            collision.CollisionProxies = buffers.CollisionProxies;
            collision.Counters = buffers.Counters;
            JobHandle collisionHandle = collision.Schedule(lootHandle);
            JobHandle dataReady = JobHandle.CombineDependencies(debrisHandle, collisionHandle);

            ExtractRenderMatricesJob extract = default;
            extract.Nodes = buffers.Nodes;
            extract.DebrisNodes = buffers.DebrisNodes;
            extract.Tuning = buffers.Tuning;
            extract.HzbTiles = buffers.HzbTiles;
            extract.RenderMatrices = buffers.RenderMatrices;
            extract.IndirectArgs = buffers.IndirectArgs;
            extract.GpuScalars = buffers.GpuScalars;
            extract.Counters = buffers.Counters;
            extract.CameraAUP = cameraAup;
            extract.CameraRelativeViewProjection = cameraRelativeViewProjection;
            extract.HzbWidth = hzbWidth;
            extract.HzbHeight = hzbHeight;
            extract.VertexCountPerInstance = vertexCountPerInstance;
            extract.Frame = frame;
            JobHandle extractHandle = extract.Schedule(dataReady);

            WreckageSelfAuditJob audit = default;
            audit.Nodes = buffers.Nodes;
            audit.Counters = buffers.Counters;
            audit.Results = buffers.SelfAudit;
            audit.Frame = frame;
            outputDependency = audit.Schedule(extractHandle);
            return true;
        }

        public static bool TryGetTuning(IDataVault vault, ref ProceduralWreckageVaultHandles handles, out WreckageTuningDTO tuning)
        {
            tuning = default;
            if (!TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            tuning = buffers.Tuning[0];
            return true;
        }

        public static bool TrySetTuning(IDataVault vault, ref ProceduralWreckageVaultHandles handles, in WreckageTuningDTO tuning)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers buffers) ||
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
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            string candidate = Path.Combine(projectRoot, "Assets", "StreamingAssets", BinaryRulesFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            candidate = Path.Combine(projectRoot, "StreamingAssets", BinaryRulesFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            candidate = Path.Combine(projectRoot, "Docs", "Archive", BinaryRulesFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            candidate = Path.Combine(projectRoot, "Assets", "_Project", "Data", "World", BinaryRulesFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            candidate = Path.Combine(projectRoot, "Data", "World", BinaryRulesFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }

            return false;
        }

        public static bool TryLoadAuthoredRules(IDataVault vault, ref ProceduralWreckageVaultHandles handles, string projectRoot)
        {
            if (TryLoadBinaryRules(vault, ref handles, projectRoot))
                return true;

#if UNITY_EDITOR
            return TryLoadCsvRules(vault, ref handles, projectRoot);
#else
            return false;
#endif
        }

        public static bool TryLoadBinaryRules(IDataVault vault, ref ProceduralWreckageVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Rules.IsCreated)
            {
                return false;
            }

            if (!TryFindLegacyRuleBinary(projectRoot, out string path) || !File.Exists(path))
                return false;

            ulong writeTicks = (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            NativeArray<byte> ruleScratch = buffers.CsvScratch;
            int length = ReadFileIntoNativeScratch(path, ruleScratch);
            if (length <= 0)
                return false;

            int activeRuleCount = TryApplyBinaryRules(ruleScratch, length, buffers.Rules, out uint version, out bool swappedEndian);
            if (activeRuleCount <= 1)
                return false;

            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                WreckageTuningDTO tuning = buffers.Tuning[0];
                tuning.LastRulePayloadHash = HashBytes(buffers.CsvScratch, length) ^ version ^ (swappedEndian ? 0xB16B00B5u : 0u);
                tuning.LastRulePayloadWriteTicks = writeTicks;
                tuning.Flags |= swappedEndian ? 1u : 0u;
                tuning.Version++;
                buffers.Tuning[0] = SanitizeTuning(in tuning);
            }

            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = buffers.Counters[0];
                counter.CsvRuleCount = 0u;
                counter.BinaryRuleCount = (uint)(activeRuleCount - 1);
                counter.ActiveRuleCount = (uint)activeRuleCount;
                buffers.Counters[0] = counter;
            }

            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadCsvRules(IDataVault vault, ref ProceduralWreckageVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                !buffers.Rules.IsCreated)
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

            int ruleCount = TryApplyCsvRules(buffers.CsvScratch, length, buffers.Rules);
            if (ruleCount <= 0)
                return false;

            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                WreckageTuningDTO tuning = buffers.Tuning[0];
                tuning.LastRulePayloadHash = HashBytes(buffers.CsvScratch, length);
                tuning.LastRulePayloadWriteTicks = writeTicks;
                tuning.Version++;
                buffers.Tuning[0] = SanitizeTuning(in tuning);
            }

            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = buffers.Counters[0];
                counter.CsvRuleCount = (uint)ruleCount;
                counter.BinaryRuleCount = 0u;
                counter.ActiveRuleCount = (uint)math.max(ruleCount, 1);
                buffers.Counters[0] = counter;
            }

            return true;
        }

        public static bool TryPollCsvRules(IDataVault vault, ref ProceduralWreckageVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ProceduralWreckageVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            return buffers.Tuning[0].LastRulePayloadWriteTicks != writeTicks &&
                   TryLoadCsvRules(vault, ref handles, projectRoot);
        }
#endif

        public static int TryApplyBinaryRules(
            NativeArray<byte> bytes,
            int length,
            NativeArray<WreckageRuleDTO> rules,
            out uint version,
            out bool swappedEndian)
        {
            version = 0u;
            swappedEndian = false;
            if (!bytes.IsCreated ||
                !rules.IsCreated ||
                length < ProceduralWreckageConstants.RuleBinaryHeaderBytes + ProceduralWreckageConstants.RuleBinaryRecordBytes ||
                rules.Length <= 1)
            {
                return 0;
            }

            uint magic = ReadUInt32Little(bytes, 0);
            if (magic != ProceduralWreckageConstants.RuleBinaryMagic)
            {
                uint swappedMagic = ReverseBytes(magic);
                if (swappedMagic != ProceduralWreckageConstants.RuleBinaryMagic)
                    return 0;

                swappedEndian = true;
            }

            uint endianMarker = ReadUInt32(bytes, 4, swappedEndian);
            if (endianMarker != ProceduralWreckageConstants.DumpEndianMarker)
                return 0;

            version = ReadUInt32(bytes, 8, swappedEndian);
            if (version == 0u)
                return 0;

            uint declaredCountRaw = ReadUInt32(bytes, 12, swappedEndian);
            int declaredCount = declaredCountRaw > int.MaxValue ? int.MaxValue : (int)declaredCountRaw;
            int availableCount = (math.min(length, bytes.Length) - ProceduralWreckageConstants.RuleBinaryHeaderBytes) /
                                 ProceduralWreckageConstants.RuleBinaryRecordBytes;
            int readCount = math.clamp(Math.Min(declaredCount, availableCount), 0, rules.Length - 1);
            if (readCount <= 0)
                return 0;

            GenerateEmergencyMockWreckRules(rules);
            int written = 0;
            for (int i = 0; i < readCount && written + 1 < rules.Length; i++)
            {
                int rowOffset = ProceduralWreckageConstants.RuleBinaryHeaderBytes +
                                i * ProceduralWreckageConstants.RuleBinaryRecordBytes;
                if (!TryReadBinaryRule(bytes, rowOffset, swappedEndian, out WreckageRuleDTO rule))
                    continue;

                int slot = written + 1;
                rule.ModuleId = (byte)slot;
                rules[slot] = rule;
                written++;
            }

            return written > 0 ? written + 1 : 0;
        }

#if UNITY_EDITOR
        public static int TryApplyCsvRules(NativeArray<byte> bytes, int length, NativeArray<WreckageRuleDTO> rules)
        {
            if (!bytes.IsCreated || !rules.IsCreated || length <= 0 || rules.Length <= 1)
                return 0;

            GenerateEmergencyMockWreckRules(rules);
            int index = 0;
            int limit = math.min(length, bytes.Length);
            int ruleIndex = 1;
            int written = 0;
            while (index < limit && ruleIndex < rules.Length)
            {
                SkipWhitespace(bytes, limit, ref index);
                if (index >= limit)
                    break;

                if (bytes[index] == (byte)'#')
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                uint moduleHash = ReadKeyHash(bytes, limit, ref index);
                if (index < limit && bytes[index] == (byte)',')
                    index++;

                if (!TryReadUInt(bytes, limit, ref index, out uint north) ||
                    !ConsumeComma(bytes, limit, ref index) ||
                    !TryReadUInt(bytes, limit, ref index, out uint east) ||
                    !ConsumeComma(bytes, limit, ref index) ||
                    !TryReadUInt(bytes, limit, ref index, out uint south) ||
                    !ConsumeComma(bytes, limit, ref index) ||
                    !TryReadUInt(bytes, limit, ref index, out uint west) ||
                    !ConsumeComma(bytes, limit, ref index) ||
                    !TryReadUInt(bytes, limit, ref index, out uint top) ||
                    !ConsumeComma(bytes, limit, ref index) ||
                    !TryReadUInt(bytes, limit, ref index, out uint bottom))
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                float weight = 1f;
                byte priority = 0;
                uint flags = WreckageRuleFlags.Structural;
                if (ConsumeComma(bytes, limit, ref index) && TryReadFloat(bytes, limit, ref index, out float parsedWeight))
                    weight = math.max(parsedWeight, ProceduralWreckageConstants.Epsilon);
                if (ConsumeComma(bytes, limit, ref index) && TryReadUInt(bytes, limit, ref index, out uint parsedPriority))
                    priority = (byte)math.min(parsedPriority, 3u);
                if (ConsumeComma(bytes, limit, ref index) && TryReadUInt(bytes, limit, ref index, out uint parsedFlags))
                    flags = parsedFlags;

                WreckageRuleDTO rule = default;
                rule.ModuleHash = moduleHash;
                rule.SocketNorth = (ushort)north;
                rule.SocketEast = (ushort)east;
                rule.SocketSouth = (ushort)south;
                rule.SocketWest = (ushort)west;
                rule.SocketTop = (ushort)top;
                rule.SocketBottom = (ushort)bottom;
                rule.BoundsExtents = new float3(3.5f, 2.25f, 3.5f);
                rule.Weight = weight;
                rule.PrefabHash = moduleHash;
                rule.Flags = flags;
                rule.ModuleId = (byte)ruleIndex;
                rule.DrawPriority = priority;
                rules[ruleIndex++] = rule;
                written++;
                SkipLine(bytes, limit, ref index);
            }

            return written > 0 ? ruleIndex : 0;
        }
#endif

        public static void GenerateEmergencyMockWreckRules(NativeArray<WreckageRuleDTO> rules)
        {
            if (!rules.IsCreated || rules.Length <= 0)
                return;

            ClearArray(rules);
            SetRule(rules, 0, "void_connector", 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, new float3(0.5f), 1f, WreckageRuleFlags.Empty, 0);
            SetRule(rules, 1, "corridor_a", 0x0001, 0x0001, 0x0001, 0x0001, 0x0020, 0x0020, new float3(3.5f, 2f, 4f), 1f, WreckageRuleFlags.Structural | WreckageRuleFlags.EssentialSilhouette, 0);
            SetRule(rules, 2, "reactor_b", 0x0003, 0x0003, 0x0003, 0x0003, 0x0020, 0x0020, new float3(5.5f, 3.5f, 5.5f), 0.55f, WreckageRuleFlags.Structural | WreckageRuleFlags.DebrisSource | WreckageRuleFlags.EssentialSilhouette, 0);
            SetRule(rules, 3, "breach_room", 0x0005, 0x0005, 0x0005, 0x0005, 0x0020, 0x0020, new float3(4.5f, 2.25f, 4.5f), 0.8f, WreckageRuleFlags.Structural | WreckageRuleFlags.DebrisSource, 1);
            SetRule(rules, 4, "cargo_deadend", 0x0001, 0x0000, 0x0001, 0x0000, 0x0000, 0x0000, new float3(4f, 2f, 5f), 0.7f, WreckageRuleFlags.Structural | WreckageRuleFlags.TerminusEligible, 1);
            SetRule(rules, 5, "airlock_seal", 0x000A, 0x000A, 0x000A, 0x000A, 0x0000, 0x0000, new float3(3f, 2.25f, 3f), 0.45f, WreckageRuleFlags.Structural | WreckageRuleFlags.TerminusEligible, 1);
            SetRule(rules, 6, "bow_shell", 0x0001, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, new float3(5f, 2.5f, 6f), 0.25f, WreckageRuleFlags.Structural | WreckageRuleFlags.EssentialSilhouette, 0);
            SetRule(rules, 7, "stern_engine", 0x0000, 0x0000, 0x0001, 0x0000, 0x0020, 0x0020, new float3(5f, 3f, 5f), 0.35f, WreckageRuleFlags.Structural | WreckageRuleFlags.DebrisSource | WreckageRuleFlags.EssentialSilhouette, 0);
        }

        public static bool TryDumpBlackBox(in ProceduralWreckageVaultBuffers buffers, string projectRoot, uint reason)
        {
            if (!buffers.TelemetryRing.IsCreated || string.IsNullOrEmpty(projectRoot))
                return false;

            bool primary = TryWriteDumpFile("Docs/AgentLogs/" + DumpFileName, in buffers, reason);
            bool agent = TryWriteDumpFile("Docs/AgentLogs/" + AgentDumpFileName, in buffers, reason);
            return primary && agent;
        }

        public static bool TryDumpBlackBoxOnFault(in ProceduralWreckageVaultBuffers buffers, string projectRoot)
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

            WreckageGenerationTelemetryEntry entry = buffers.TelemetryRing[math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1)];
            if (entry.FaultFlags == 0u)
                return false;

            return TryDumpBlackBox(in buffers, projectRoot, entry.FaultFlags);
        }

        public static uint ComputeSectorHash(double3 rootAup)
        {
            return ProceduralWreckageMath.HashDouble3(rootAup);
        }

        private static void HydrateDefaultsIfNeeded(ProceduralWreckageVaultBuffers buffers)
        {
            bool firstHydration = buffers.Tuning.IsCreated && buffers.Tuning.Length > 0 && buffers.Tuning[0].Version == 0u;
            if (!firstHydration)
                return;

            ClearArray(buffers.Grid);
            ClearArray(buffers.Nodes);
            ClearArray(buffers.DebrisNodes);
            ClearArray(buffers.RenderMatrices);
            ClearArray(buffers.IndirectArgs);
            ClearArray(buffers.SectorTriggers);
            ClearArray(buffers.LootRequests);
            ClearArray(buffers.CollisionProxies);
            ClearArray(buffers.TelemetryRing);
            ClearArray(buffers.TelemetryCursor);
            ClearArray(buffers.CsvScratch);
            ClearArray(buffers.Counters);
            ClearArray(buffers.DebugCells);
            ClearArray(buffers.GpuScalars);
            ClearArray(buffers.SelfAudit);
            ClearArray(buffers.HzbTiles);
            GenerateEmergencyMockWreckRules(buffers.Rules);
            buffers.Tuning[0] = BuildDefaultTuning();
            if (buffers.Counters.IsCreated && buffers.Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = default;
                counter.ActiveRuleCount = 8u;
                buffers.Counters[0] = counter;
            }
        }

        private static WreckageTuningDTO BuildDefaultTuning()
        {
            WreckageTuningDTO tuning = default;
            tuning.GlobalQualityWeight = 0.5f;
            tuning.ShearSeverity = 0.45f;
            tuning.DebrisScatterRadius = 96f;
            tuning.VisibilityDistanceMin = 100f;
            tuning.VisibilityDistanceMax = 500f;
            tuning.BacktrackLimit = 256u;
            tuning.MaxNodes = 192;
            tuning.MaxDebris = 512;
            tuning.CellSize = 8f;
            tuning.MaxGenerationMs = 2f;
            tuning.Version = 1u;
            tuning.SeedSalt = 0x121121u;
            return tuning;
        }

        private static WreckageTuningDTO SanitizeTuning(in WreckageTuningDTO tuning)
        {
            WreckageTuningDTO safe = tuning;
            safe.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            safe.ShearSeverity = math.saturate(tuning.ShearSeverity);
            safe.DebrisScatterRadius = math.max(tuning.DebrisScatterRadius, 1f);
            safe.VisibilityDistanceMin = math.max(tuning.VisibilityDistanceMin, 8f);
            safe.VisibilityDistanceMax = math.max(tuning.VisibilityDistanceMax, safe.VisibilityDistanceMin);
            safe.BacktrackLimit = math.max(1u, tuning.BacktrackLimit);
            safe.MaxNodes = math.clamp(tuning.MaxNodes, 1, ProceduralWreckageConstants.MaxWreckNodes);
            safe.MaxDebris = math.clamp(tuning.MaxDebris, 0, ProceduralWreckageConstants.MaxDebrisNodes);
            safe.CellSize = math.max(tuning.CellSize, ProceduralWreckageConstants.Epsilon);
            safe.MaxGenerationMs = math.max(tuning.MaxGenerationMs, 0.25f);
            safe.Version = tuning.Version == 0u ? 1u : tuning.Version;
            safe.SeedSalt = tuning.SeedSalt == 0u ? 0x121121u : tuning.SeedSalt;
            return safe;
        }

        private static void SetRule(
            NativeArray<WreckageRuleDTO> rules,
            int index,
            string name,
            ushort north,
            ushort east,
            ushort south,
            ushort west,
            ushort top,
            ushort bottom,
            float3 extents,
            float weight,
            uint flags,
            byte priority)
        {
            if ((uint)index >= (uint)rules.Length)
                return;

            uint hash = HashAsciiLiteral(name);
            WreckageRuleDTO rule = default;
            rule.ModuleHash = hash;
            rule.SocketNorth = north;
            rule.SocketEast = east;
            rule.SocketSouth = south;
            rule.SocketWest = west;
            rule.SocketTop = top;
            rule.SocketBottom = bottom;
            rule.BoundsExtents = extents;
            rule.Weight = math.max(weight, ProceduralWreckageConstants.Epsilon);
            rule.PrefabHash = hash;
            rule.Flags = flags;
            rule.ModuleId = (byte)index;
            rule.DrawPriority = priority;
            rules[index] = rule;
        }

        private static string ResolveCsvPath(string projectRoot)
        {
            return string.IsNullOrEmpty(projectRoot) ? null : Path.Combine(projectRoot, CsvRulesFileName);
        }

        private static bool TryReadBinaryRule(NativeArray<byte> bytes, int offset, bool swapEndian, out WreckageRuleDTO rule)
        {
            rule = default;
            if (!bytes.IsCreated ||
                offset < 0 ||
                offset + ProceduralWreckageConstants.RuleBinaryRecordBytes > bytes.Length)
            {
                return false;
            }

            uint moduleHash = ReadUInt32(bytes, offset, swapEndian);
            if (moduleHash == 0u)
                return false;

            float3 extents = new float3(
                ReadFloat32(bytes, offset + 16, swapEndian),
                ReadFloat32(bytes, offset + 20, swapEndian),
                ReadFloat32(bytes, offset + 24, swapEndian));
            float weight = ReadFloat32(bytes, offset + 28, swapEndian);
            if (!math.all(math.isfinite(extents)) || !math.isfinite(weight))
                return false;

            rule.ModuleHash = moduleHash;
            rule.SocketNorth = ReadUInt16(bytes, offset + 4, swapEndian);
            rule.SocketEast = ReadUInt16(bytes, offset + 6, swapEndian);
            rule.SocketSouth = ReadUInt16(bytes, offset + 8, swapEndian);
            rule.SocketWest = ReadUInt16(bytes, offset + 10, swapEndian);
            rule.SocketTop = ReadUInt16(bytes, offset + 12, swapEndian);
            rule.SocketBottom = ReadUInt16(bytes, offset + 14, swapEndian);
            rule.BoundsExtents = math.max(math.abs(extents), new float3(0.25f));
            rule.Weight = math.max(weight, ProceduralWreckageConstants.Epsilon);
            uint prefabHash = ReadUInt32(bytes, offset + 32, swapEndian);
            rule.PrefabHash = prefabHash == 0u ? moduleHash : prefabHash;
            uint flags = ReadUInt32(bytes, offset + 36, swapEndian);
            rule.Flags = flags == 0u ? WreckageRuleFlags.Structural : flags;
            byte priority = bytes[offset + 41];
            rule.DrawPriority = priority > 3 ? (byte)3 : priority;
            return true;
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

        private static uint ReadKeyHash(NativeArray<byte> bytes, int limit, ref int index)
        {
            uint hash = 2166136261u;
            while (index < limit)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                hash = ProceduralWreckageMath.HashAsciiLower(c, hash);
                index++;
            }

            return hash;
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

        private static bool TryWriteDumpFile(string path, in ProceduralWreckageVaultBuffers buffers, uint reason)
        {
            if (string.IsNullOrEmpty(path) || !buffers.TelemetryRing.IsCreated)
                return false;

            int entryBytes = UnsafeUtility.SizeOf<WreckageGenerationTelemetryEntry>();
            int telemetryBytes = buffers.TelemetryRing.Length * entryBytes;
            int byteCount = 32 + telemetryBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(ProceduralWreckageVault),
                "ProceduralWreckageTelemetryDumpPayload");
            try
            {
                WriteUInt32(payload, 0, ProceduralWreckageConstants.DumpMagic);
                WriteUInt32(payload, 4, ProceduralWreckageConstants.DumpEndianMarker);
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
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ProceduralWreckageVault),
                    "ProceduralWreckageTelemetryDumpPayload");
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

        private static ushort ReadUInt16(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            return swapEndian
                ? (ushort)(((uint)bytes[offset] << 8) | (uint)bytes[offset + 1])
                : (ushort)((uint)bytes[offset] | ((uint)bytes[offset + 1] << 8));
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
