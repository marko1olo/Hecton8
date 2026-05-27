// ============================================================================
// HECTON-8 - HabitatStressSmokeTester.cs
// Cold-path stress harness for habitat dirty-region BFS and waterline shader upload.
// ============================================================================

using System.Diagnostics;
using System.IO;
using System.Text;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Debug = UnityEngine.Debug;

namespace Hecton8.Debugging
{
    public struct HabitatStressSmokeReport
    {
        public bool Passed;
        public int NodeCount;
        public int RupturedNodeCount;
        public int DirectedEdgeCount;
        public int IslandCount;
        public int VisitedNodeCount;
        public int ExpectedVisitedNodeCount;
        public int ShaderUpdateCount;
        public int QueueOverflow;
        public int CircuitBreakerAllowed;
        public int CircuitBreakerReportedAllowed;
        public int CircuitBreakerDropped;
        public int CircuitBreakerReportedDropped;
        public int CircuitBreakerBudget;
        public int CircuitBreakerSlotCapacity;
        public int CircuitBreakerSlotFillAllowed;
        public int CircuitBreakerOverflowDropped;
        public int CircuitBreakerActiveSlotCount;
        public bool CircuitBreakerPassed;
        public bool CircuitBreakerOverflowPassed;
        public double ElapsedMilliseconds;
        public double AverageElapsedMilliseconds;
        public double WorstElapsedMilliseconds;
        public int SentinelBefore;
        public int SentinelAfter;
        public int SentinelDelta;
        public string ReportPath;
    }

    public static class HabitatStressSmokeTester
    {
        private const string NativeMemoryOwner = nameof(HabitatStressSmokeTester);
        private const string OutputFolder = "CodexArtifacts";
        private const string OutputFileName = "habitat-stress-smoke-report.json";
        private const int NodeCount = 1000;
        private const int RupturedNodeCount = 50;
        private const int DirectedEdgeCount = (NodeCount - 1) * 2;
        private const int ShaderPayloadCapacity = 64;
        private const int TimedSampleCount = 8;
        private const int IslandIdBase = 4096;
        private const double BudgetMilliseconds = 0.1d;
        private const int CircuitBreakerIslandId = 7;
        private const int CircuitBreakerProbeCount = 20;
        private const int CircuitBreakerBudget = 16;
        private const int CircuitBreakerSlotCapacity = 64;
        private static readonly uint s_CircuitBreakerSmokeHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HabitatStressSmokeTester.CircuitBreaker"));
        private static readonly int s_ModuleAmbienceDataId = Shader.PropertyToID("_HectonModuleAmbienceDataBuffer");
        private static readonly int s_ModuleWaterLevelsId = Shader.PropertyToID("_HectonModuleWaterLevelsBuffer");

        // COLD ALLOC: Vector4[64] - editor smoke shader ambience upload staging array - owner: HabitatStressSmokeTester
        private static readonly Vector4[] s_shaderAmbiencePayload = new Vector4[ShaderPayloadCapacity];
        // COLD ALLOC: Vector4[64] - editor smoke shader upload staging array - owner: HabitatStressSmokeTester
        private static readonly Vector4[] s_shaderPayload = new Vector4[ShaderPayloadCapacity];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // COLD ALLOC: StringBuilder[768] - editor smoke JSON report builder - owner: HabitatStressSmokeTester
        private static readonly StringBuilder s_reportBuilder = new StringBuilder(768);
#endif
        private static GraphicsBuffer s_shaderAmbiencePayloadBuffer;
        private static GraphicsBuffer s_shaderPayloadBuffer;

#if UNITY_EDITOR
        [MenuItem("Hecton8/Habitat/Run Habitat Stress Smoke Test")]
        public static void RunMenuItem()
        {
            Run();
        }
#endif

        public static HabitatStressSmokeReport Run()
        {
            HabitatStressSmokeReport report = RunHeadless();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WriteReport(ref report);
            Hecton8.Core.H8Debug.Log("[HabitatStressSmokeTester] " + (report.Passed ? "PASS " : "FAIL ") + report.ReportPath);
#endif
            return report;
        }

        public static HabitatStressSmokeReport RunHeadless()
        {
            NativeArray<int> edgeOffsets = default;
            NativeArray<int> edgeDestinations = default;
            NativeArray<byte> severedEdgeMask = default;
            NativeArray<byte> rupturedNodeMask = default;
            NativeArray<int> dirtyNodeIndices = default;
            NativeArray<int> traversalQueue = default;
            NativeArray<int> visitStamp = default;
            NativeArray<int> islandIds = default;
            NativeArray<HabitatDirtyRegionResult> dirtyResult = default;
            NativeArray<float> floodLevel01 = default;
            NativeArray<float> waterSurfaceY = default;
            NativeArray<float> brownoutFlicker01 = default;
            NativeArray<float> condensationDepth01 = default;
            NativeArray<float4> moduleWaterLevels = default;

            int sentinelBefore = NativeMemorySentinel.ActiveAllocationCount;
            int sentinelAfter = sentinelBefore;
            HabitatDirtyRegionResult finalDirtyResult = default;
            double bestMilliseconds = double.MaxValue;
            double totalMilliseconds = 0d;
            double worstMilliseconds = 0d;

            try
            {
                // COLD ALLOC: NativeArray<int>[1001] - smoke CSR edge offsets - owner: HabitatStressSmokeTester
                edgeOffsets = AllocateSmokeArray<int>(NodeCount + 1, nameof(edgeOffsets));
                // COLD ALLOC: NativeArray<int>[1998] - smoke CSR edge destinations - owner: HabitatStressSmokeTester
                edgeDestinations = AllocateSmokeArray<int>(DirectedEdgeCount, nameof(edgeDestinations));
                // COLD ALLOC: NativeArray<byte>[1998] - smoke severed-edge flags - owner: HabitatStressSmokeTester
                severedEdgeMask = AllocateSmokeArray<byte>(DirectedEdgeCount, nameof(severedEdgeMask));
                // COLD ALLOC: NativeArray<byte>[1000] - smoke ruptured-node flags - owner: HabitatStressSmokeTester
                rupturedNodeMask = AllocateSmokeArray<byte>(NodeCount, nameof(rupturedNodeMask));
                // COLD ALLOC: NativeArray<int>[50] - smoke dirty rupture seed list - owner: HabitatStressSmokeTester
                dirtyNodeIndices = AllocateSmokeArray<int>(RupturedNodeCount, nameof(dirtyNodeIndices));
                // COLD ALLOC: NativeArray<int>[1000] - smoke BFS traversal queue - owner: HabitatStressSmokeTester
                traversalQueue = AllocateSmokeArray<int>(NodeCount, nameof(traversalQueue));
                // COLD ALLOC: NativeArray<int>[1000] - smoke BFS visit stamps - owner: HabitatStressSmokeTester
                visitStamp = AllocateSmokeArray<int>(NodeCount, nameof(visitStamp));
                // COLD ALLOC: NativeArray<int>[1000] - smoke IslandID output buffer - owner: HabitatStressSmokeTester
                islandIds = AllocateSmokeArray<int>(NodeCount, nameof(islandIds));
                // COLD ALLOC: NativeArray<HabitatDirtyRegionResult>[1] - smoke dirty-region result cell - owner: HabitatStressSmokeTester
                dirtyResult = AllocateSmokeArray<HabitatDirtyRegionResult>(1, nameof(dirtyResult));
                // COLD ALLOC: NativeArray<float>[1000] - smoke shader flood-level input - owner: HabitatStressSmokeTester
                floodLevel01 = AllocateSmokeArray<float>(NodeCount, nameof(floodLevel01));
                // COLD ALLOC: NativeArray<float>[1000] - smoke shader waterline-Y input - owner: HabitatStressSmokeTester
                waterSurfaceY = AllocateSmokeArray<float>(NodeCount, nameof(waterSurfaceY));
                // COLD ALLOC: NativeArray<float>[1000] - smoke brownout flicker input - owner: HabitatStressSmokeTester
                brownoutFlicker01 = AllocateSmokeArray<float>(NodeCount, nameof(brownoutFlicker01));
                // COLD ALLOC: NativeArray<float>[1000] - smoke condensation-depth input - owner: HabitatStressSmokeTester
                condensationDepth01 = AllocateSmokeArray<float>(NodeCount, nameof(condensationDepth01));
                // COLD ALLOC: NativeArray<float4>[64] - smoke shader upload output - owner: HabitatStressSmokeTester
                moduleWaterLevels = AllocateSmokeArray<float4>(ShaderPayloadCapacity, nameof(moduleWaterLevels));

                BuildLinearCsr(edgeOffsets, edgeDestinations);
                BuildRuptureSeeds(rupturedNodeMask, dirtyNodeIndices);
                BuildShaderInputs(floodLevel01, waterSurfaceY, brownoutFlicker01, condensationDepth01);
                ResetIslandIds(islandIds);

                JobHandle warmupHandle = ScheduleSmokeJobs(
                    1,
                    edgeOffsets,
                    edgeDestinations,
                    severedEdgeMask,
                    rupturedNodeMask,
                    dirtyNodeIndices,
                    traversalQueue,
                    visitStamp,
                    islandIds,
                    dirtyResult,
                    floodLevel01,
                    waterSurfaceY,
                    brownoutFlicker01,
                    condensationDepth01,
                    moduleWaterLevels);
                // COLD SYNC JOB: editor smoke synchronizes after the scheduled Burst chain; no runtime Tick/Update path calls this method.
                DispatcherJobSwap.TryComplete(ref warmupHandle, forceComplete: true);

                for (int sampleIndex = 0; sampleIndex < TimedSampleCount; sampleIndex++)
                {
                    long startTicks = Stopwatch.GetTimestamp();
                    JobHandle timedHandle = ScheduleSmokeJobs(
                        sampleIndex + 2,
                        edgeOffsets,
                        edgeDestinations,
                        severedEdgeMask,
                        rupturedNodeMask,
                        dirtyNodeIndices,
                        traversalQueue,
                        visitStamp,
                        islandIds,
                        dirtyResult,
                        floodLevel01,
                        waterSurfaceY,
                        brownoutFlicker01,
                        condensationDepth01,
                        moduleWaterLevels);
                    // COLD SYNC JOB: timing probe resolves at smoke-test boundary only.
                    DispatcherJobSwap.TryComplete(ref timedHandle, forceComplete: true);
                    long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                    double elapsedMs = elapsedTicks * 1000.0d / Stopwatch.Frequency;
                    totalMilliseconds += elapsedMs;
                    if (elapsedMs > worstMilliseconds)
                        worstMilliseconds = elapsedMs;

                    if (elapsedMs < bestMilliseconds)
                    {
                        bestMilliseconds = elapsedMs;
                        finalDirtyResult = dirtyResult[0];
                    }

                    UploadShaderPayload(moduleWaterLevels);
                }
            }
            finally
            {
                DisposeSmokeArray(ref edgeOffsets);
                DisposeSmokeArray(ref edgeDestinations);
                DisposeSmokeArray(ref severedEdgeMask);
                DisposeSmokeArray(ref rupturedNodeMask);
                DisposeSmokeArray(ref dirtyNodeIndices);
                DisposeSmokeArray(ref traversalQueue);
                DisposeSmokeArray(ref visitStamp);
                DisposeSmokeArray(ref islandIds);
                DisposeSmokeArray(ref dirtyResult);
                DisposeSmokeArray(ref floodLevel01);
                DisposeSmokeArray(ref waterSurfaceY);
                DisposeSmokeArray(ref brownoutFlicker01);
                DisposeSmokeArray(ref condensationDepth01);
                DisposeSmokeArray(ref moduleWaterLevels);
                ReleaseSmokeShaderBuffers();
                sentinelAfter = NativeMemorySentinel.ActiveAllocationCount;
            }

            int expectedVisited = NodeCount - RupturedNodeCount;
            int sentinelDelta = sentinelAfter - sentinelBefore;
            RunCircuitBreakerProbe(
                out int circuitBreakerAllowed,
                out int circuitBreakerReportedAllowed,
                out int circuitBreakerDropped,
                out int circuitBreakerReportedDropped,
                out bool circuitBreakerPassed);
            RunCircuitBreakerOverflowProbe(
                out int circuitBreakerSlotFillAllowed,
                out int circuitBreakerOverflowDropped,
                out int circuitBreakerActiveSlotCount,
                out bool circuitBreakerOverflowPassed);
            bool passed =
                finalDirtyResult.RupturedSeedCount == RupturedNodeCount &&
                finalDirtyResult.VisitedNodeCount == expectedVisited &&
                finalDirtyResult.ShaderUpdateCount == ShaderPayloadCapacity &&
                finalDirtyResult.QueueOverflow == 0 &&
                sentinelDelta == 0 &&
                circuitBreakerPassed &&
                circuitBreakerOverflowPassed &&
                worstMilliseconds <= BudgetMilliseconds;

            return new HabitatStressSmokeReport
            {
                Passed = passed,
                NodeCount = NodeCount,
                RupturedNodeCount = RupturedNodeCount,
                DirectedEdgeCount = DirectedEdgeCount,
                IslandCount = finalDirtyResult.IslandCount,
                VisitedNodeCount = finalDirtyResult.VisitedNodeCount,
                ExpectedVisitedNodeCount = expectedVisited,
                ShaderUpdateCount = finalDirtyResult.ShaderUpdateCount,
                QueueOverflow = finalDirtyResult.QueueOverflow,
                CircuitBreakerAllowed = circuitBreakerAllowed,
                CircuitBreakerReportedAllowed = circuitBreakerReportedAllowed,
                CircuitBreakerDropped = circuitBreakerDropped,
                CircuitBreakerReportedDropped = circuitBreakerReportedDropped,
                CircuitBreakerBudget = CircuitBreakerBudget,
                CircuitBreakerSlotCapacity = CircuitBreakerSlotCapacity,
                CircuitBreakerSlotFillAllowed = circuitBreakerSlotFillAllowed,
                CircuitBreakerOverflowDropped = circuitBreakerOverflowDropped,
                CircuitBreakerActiveSlotCount = circuitBreakerActiveSlotCount,
                CircuitBreakerPassed = circuitBreakerPassed,
                CircuitBreakerOverflowPassed = circuitBreakerOverflowPassed,
                ElapsedMilliseconds = bestMilliseconds,
                AverageElapsedMilliseconds = TimedSampleCount > 0 ? totalMilliseconds / TimedSampleCount : 0d,
                WorstElapsedMilliseconds = worstMilliseconds,
                SentinelBefore = sentinelBefore,
                SentinelAfter = sentinelAfter,
                SentinelDelta = sentinelDelta
            };
        }

        private static JobHandle ScheduleSmokeJobs(
            int visitStampValue,
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> severedEdgeMask,
            NativeArray<byte> rupturedNodeMask,
            NativeArray<int> dirtyNodeIndices,
            NativeArray<int> traversalQueue,
            NativeArray<int> visitStamp,
            NativeArray<int> islandIds,
            NativeArray<HabitatDirtyRegionResult> dirtyResult,
            NativeArray<float> floodLevel01,
            NativeArray<float> waterSurfaceY,
            NativeArray<float> brownoutFlicker01,
            NativeArray<float> condensationDepth01,
            NativeArray<float4> moduleWaterLevels)
        {
            HabitatDirtyRegionRebuildJob dirtyJob = new HabitatDirtyRegionRebuildJob
            {
                NodeCount = NodeCount,
                DirtyNodeCount = RupturedNodeCount,
                CurrentVisitStamp = visitStampValue,
                IslandIdBase = IslandIdBase + visitStampValue * 128,
                ShaderUpdateCapacity = ShaderPayloadCapacity,
                EdgeOffsets = edgeOffsets,
                EdgeDestinations = edgeDestinations,
                SeveredEdgeMask = severedEdgeMask,
                RupturedNodeMask = rupturedNodeMask,
                DirtyNodeIndices = dirtyNodeIndices,
                TraversalQueue = traversalQueue,
                VisitStamp = visitStamp,
                IslandIds = islandIds,
                Result = dirtyResult
            };

            HabitatWaterlineShaderUpdateJob shaderJob = new HabitatWaterlineShaderUpdateJob
            {
                NodeCount = NodeCount,
                IslandIds = islandIds,
                FloodLevel01 = floodLevel01,
                WaterSurfaceY = waterSurfaceY,
                BrownoutFlicker01 = brownoutFlicker01,
                CondensationDepth01 = condensationDepth01,
                ModuleWaterLevels = moduleWaterLevels
            };

            JobHandle dirtyHandle = dirtyJob.Schedule();
            return shaderJob.Schedule(ShaderPayloadCapacity, 32, dirtyHandle);
        }

        private static void BuildLinearCsr(
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations)
        {
            int edgeWrite = 0;
            for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
            {
                edgeOffsets[nodeIndex] = edgeWrite;
                if (nodeIndex > 0)
                    edgeDestinations[edgeWrite++] = nodeIndex - 1;

                if (nodeIndex < NodeCount - 1)
                    edgeDestinations[edgeWrite++] = nodeIndex + 1;
            }

            edgeOffsets[NodeCount] = edgeWrite;
        }

        private static void BuildRuptureSeeds(
            NativeArray<byte> rupturedNodeMask,
            NativeArray<int> dirtyNodeIndices)
        {
            for (int index = 0; index < RupturedNodeCount; index++)
            {
                int nodeIndex = 10 + index * 19;
                rupturedNodeMask[nodeIndex] = 1;
                dirtyNodeIndices[index] = nodeIndex;
            }
        }

        private static void BuildShaderInputs(
            NativeArray<float> floodLevel01,
            NativeArray<float> waterSurfaceY,
            NativeArray<float> brownoutFlicker01,
            NativeArray<float> condensationDepth01)
        {
            for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
            {
                floodLevel01[nodeIndex] = math.saturate((nodeIndex % 37) * 0.0275f);
                waterSurfaceY[nodeIndex] = -12f + nodeIndex * 0.015f;
                brownoutFlicker01[nodeIndex] = (nodeIndex & 1) == 0 ? 0.42f : 0.91f;
                condensationDepth01[nodeIndex] = math.saturate(nodeIndex * 0.001f);
            }
        }

        private static void ResetIslandIds(NativeArray<int> islandIds)
        {
            for (int nodeIndex = 0; nodeIndex < islandIds.Length; nodeIndex++)
                islandIds[nodeIndex] = -1;
        }

        private static void UploadShaderPayload(NativeArray<float4> moduleWaterLevels)
        {
            int count = math.min(ShaderPayloadCapacity, moduleWaterLevels.Length);
            for (int index = 0; index < count; index++)
            {
                float4 payload = moduleWaterLevels[index];
                s_shaderAmbiencePayload[index] = new Vector4(index, 0f, 0f, 3f);
                s_shaderPayload[index] = new Vector4(payload.x, payload.y, payload.z, payload.w);
            }

            EnsureSmokeShaderBuffers();
            s_shaderAmbiencePayloadBuffer.SetData(s_shaderAmbiencePayload);
            s_shaderPayloadBuffer.SetData(s_shaderPayload);
            Shader.SetGlobalBuffer(s_ModuleAmbienceDataId, s_shaderAmbiencePayloadBuffer);
            Shader.SetGlobalBuffer(s_ModuleWaterLevelsId, s_shaderPayloadBuffer);
        }

        private static void EnsureSmokeShaderBuffers()
        {
            if (s_shaderAmbiencePayloadBuffer != null && s_shaderPayloadBuffer != null)
                return;

            ReleaseSmokeShaderBuffers();
            // COLD ALLOC: GraphicsBuffer[64 float4] - smoke StructuredBuffer ambience binding - owner: HabitatStressSmokeTester
            s_shaderAmbiencePayloadBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ShaderPayloadCapacity, sizeof(float) * 4);
            // COLD ALLOC: GraphicsBuffer[64 float4] - smoke StructuredBuffer water binding - owner: HabitatStressSmokeTester
            s_shaderPayloadBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ShaderPayloadCapacity, sizeof(float) * 4);
        }

        private static void ReleaseSmokeShaderBuffers()
        {
            if (s_shaderAmbiencePayloadBuffer != null)
            {
                s_shaderAmbiencePayloadBuffer.Release();
                s_shaderAmbiencePayloadBuffer = null;
            }

            if (s_shaderPayloadBuffer != null)
            {
                s_shaderPayloadBuffer.Release();
                s_shaderPayloadBuffer = null;
            }
        }

        private static void RunCircuitBreakerProbe(
            out int allowed,
            out int reportedAllowed,
            out int dropped,
            out int reportedDropped,
            out bool passed)
        {
            allowed = 0;
            dropped = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SystemDispatcher.ResetBaseStressCascadeCircuitBreakerForSmokeTest();
#endif
            for (int i = 0; i < CircuitBreakerProbeCount; i++)
            {
                if (SystemDispatcher.TryConsumeBaseStressCascadeEvent(CircuitBreakerIslandId, s_CircuitBreakerSmokeHash))
                    allowed++;
                else
                    dropped++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            reportedAllowed = SystemDispatcher.DebugGetBaseStressCascadeConsumedCount(CircuitBreakerIslandId);
            reportedDropped = SystemDispatcher.DebugGetBaseStressCascadeDroppedCount(CircuitBreakerIslandId);
#else
            reportedAllowed = allowed;
            reportedDropped = dropped;
#endif
            passed = allowed == CircuitBreakerBudget &&
                     reportedAllowed == allowed &&
                     dropped == CircuitBreakerProbeCount - CircuitBreakerBudget &&
                     reportedDropped == dropped;
        }

        private static void RunCircuitBreakerOverflowProbe(
            out int slotFillAllowed,
            out int overflowDropped,
            out int activeSlotCount,
            out bool passed)
        {
            slotFillAllowed = 0;
            overflowDropped = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SystemDispatcher.ResetBaseStressCascadeCircuitBreakerForSmokeTest();
#endif
            for (int islandIndex = 0; islandIndex < CircuitBreakerSlotCapacity; islandIndex++)
            {
                if (SystemDispatcher.TryConsumeBaseStressCascadeEvent(islandIndex, s_CircuitBreakerSmokeHash))
                    slotFillAllowed++;
            }

            if (!SystemDispatcher.TryConsumeBaseStressCascadeEvent(CircuitBreakerSlotCapacity, s_CircuitBreakerSmokeHash))
                overflowDropped++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            activeSlotCount = SystemDispatcher.DebugGetBaseStressCascadeActiveSlotCount();
#else
            activeSlotCount = CircuitBreakerSlotCapacity;
#endif
            passed = slotFillAllowed == CircuitBreakerSlotCapacity &&
                     overflowDropped == 1 &&
                     activeSlotCount == CircuitBreakerSlotCapacity;
        }

        private static NativeArray<T> AllocateSmokeArray<T>(int length, string label)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            return array;
        }

        private static void DisposeSmokeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void WriteReport(ref HabitatStressSmokeReport report)
        {
            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, OutputFileName);
            report.ReportPath = path;
            BuildJson(ref report, s_reportBuilder);
            File.WriteAllText(path, s_reportBuilder.ToString(), new UTF8Encoding(false));
        }

        private static void BuildJson(ref HabitatStressSmokeReport report, StringBuilder builder)
        {
            builder.Length = 0;
            builder.Append("{\n");
            builder.Append("  \"tester\":\"HabitatStressSmokeTester\",\n");
            builder.Append("  \"passed\":").Append(report.Passed ? "true" : "false").Append(",\n");
            builder.Append("  \"nodeCount\":").Append(report.NodeCount).Append(",\n");
            builder.Append("  \"rupturedNodeCount\":").Append(report.RupturedNodeCount).Append(",\n");
            builder.Append("  \"directedEdgeCount\":").Append(report.DirectedEdgeCount).Append(",\n");
            builder.Append("  \"islandCount\":").Append(report.IslandCount).Append(",\n");
            builder.Append("  \"visitedNodeCount\":").Append(report.VisitedNodeCount).Append(",\n");
            builder.Append("  \"expectedVisitedNodeCount\":").Append(report.ExpectedVisitedNodeCount).Append(",\n");
            builder.Append("  \"shaderUpdateCount\":").Append(report.ShaderUpdateCount).Append(",\n");
            builder.Append("  \"queueOverflow\":").Append(report.QueueOverflow).Append(",\n");
            builder.Append("  \"circuitBreakerAllowed\":").Append(report.CircuitBreakerAllowed).Append(",\n");
            builder.Append("  \"circuitBreakerReportedAllowed\":").Append(report.CircuitBreakerReportedAllowed).Append(",\n");
            builder.Append("  \"circuitBreakerDropped\":").Append(report.CircuitBreakerDropped).Append(",\n");
            builder.Append("  \"circuitBreakerReportedDropped\":").Append(report.CircuitBreakerReportedDropped).Append(",\n");
            builder.Append("  \"circuitBreakerBudget\":").Append(report.CircuitBreakerBudget).Append(",\n");
            builder.Append("  \"circuitBreakerSlotCapacity\":").Append(report.CircuitBreakerSlotCapacity).Append(",\n");
            builder.Append("  \"circuitBreakerSlotFillAllowed\":").Append(report.CircuitBreakerSlotFillAllowed).Append(",\n");
            builder.Append("  \"circuitBreakerOverflowDropped\":").Append(report.CircuitBreakerOverflowDropped).Append(",\n");
            builder.Append("  \"circuitBreakerActiveSlotCount\":").Append(report.CircuitBreakerActiveSlotCount).Append(",\n");
            builder.Append("  \"circuitBreakerPassed\":").Append(report.CircuitBreakerPassed ? "true" : "false").Append(",\n");
            builder.Append("  \"circuitBreakerOverflowPassed\":").Append(report.CircuitBreakerOverflowPassed ? "true" : "false").Append(",\n");
            AppendJsonFixed(ref report, builder);
            builder.Append(",\n");
            builder.Append("  \"averageElapsedMilliseconds\":");
            AppendFixed4(builder, report.AverageElapsedMilliseconds);
            builder.Append(",\n");
            builder.Append("  \"worstElapsedMilliseconds\":");
            AppendFixed4(builder, report.WorstElapsedMilliseconds);
            builder.Append(",\n");
            builder.Append("  \"timedSampleCount\":").Append(TimedSampleCount).Append(",\n");
            builder.Append("  \"budgetMilliseconds\":");
            AppendFixed1(builder, BudgetMilliseconds);
            builder.Append(",\n");
            builder.Append("  \"sentinelBefore\":").Append(report.SentinelBefore).Append(",\n");
            builder.Append("  \"sentinelAfter\":").Append(report.SentinelAfter).Append(",\n");
            builder.Append("  \"sentinelDelta\":").Append(report.SentinelDelta).Append("\n");
            builder.Append("}\n");
        }

        private static void AppendJsonFixed(ref HabitatStressSmokeReport report, StringBuilder builder)
        {
            builder.Append("  \"elapsedMilliseconds\":");
            AppendFixed4(builder, report.ElapsedMilliseconds);
        }

        private static void AppendFixed1(StringBuilder builder, double value)
        {
            AppendFixed(builder, value, 1);
        }

        private static void AppendFixed4(StringBuilder builder, double value)
        {
            AppendFixed(builder, value, 4);
        }

        private static void AppendFixed(StringBuilder builder, double value, int fractionalDigits)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                builder.Append('0');
                return;
            }

            if (value < 0d)
            {
                builder.Append('-');
                value = -value;
            }

            long scale = 1;
            for (int i = 0; i < fractionalDigits; i++)
                scale *= 10L;

            double scaledDouble = value * scale;
            long scaled = scaledDouble >= long.MaxValue
                ? long.MaxValue
                : (long)(scaledDouble + 0.5d);
            long whole = scaled / scale;
            long fraction = scaled - whole * scale;
            builder.Append(whole);
            if (fractionalDigits <= 0)
                return;

            builder.Append('.');
            long divisor = scale / 10L;
            for (int i = 0; i < fractionalDigits; i++)
            {
                long digit = divisor > 0L ? fraction / divisor : 0L;
                builder.Append((char)('0' + digit));
                fraction -= digit * divisor;
                divisor /= 10L;
            }
        }
#endif
    }
}
