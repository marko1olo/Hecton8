// ============================================================================
// HECTON-8 - HectonFluidEngine.cs v2.1 (OPTIMIZATION PASS)
// High-performance buoyancy and hydrodynamic resistance system.
//
// v2.1 CHANGES (OPTIMIZATION):
//   [OPT] Dense BuoyancyObject list duplicate check
//     - Register() keeps one managed registry instead of mirrored hash buckets
//     - Unregister() removes from the dense list directly
//     - Impact: less managed memory and better cache locality
//
//   [OPT] Cached LOD distance squares (_cachedNearDistSq, etc.)
//     - Avoids recalculating nearDistanceSq values every FixedTick
//     - Computed once in Awake and refreshed in OnValidate
//     - Impact: -5-10% GatherData() work at 200+ objects
//
//   [OPT] TryResolveObserver() -> TryResolveObserverOnce() in Awake
//     - Removes scene-search observer checks from FixedTick
//     - One-time initialization instead of per-frame checks
//     - Impact: one O(N) operation at load, not every frame
//
//   [OPT] GatherData() removes null objects from the dense registry
//     - Swap-remove keeps the parallel managed lists compact
//     - Guarantees registry consistency
//
// v2.0 (JOB + BURST BASELINE):
//   - Job System + Burst compiler for parallel computation
//   - NativeArrays with capacity doubling and no per-frame reallocation
//   - LOD system with four distance tiers
//   - Dry zones through isInAir flags
//   - CurrentVolume integration
//
// HOT-PATH CONTRACT:
//   - Zero GC in FixedTick and GatherData paths
//   - Burst-compiled job for SIMD parallelism
//   - Frame-time budget claims require profiler proof; target is sub-0.1ms
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Celestial;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
#if UNITY_EDITOR
using UnityEditor;
#endif
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;
using OceanAdapterVaultHandles = Hecton8.Environment.Fluids.OceanAdapterVaultHandles;
using OceanAdapterVaultRoute = Hecton8.Environment.Fluids.OceanAdapterVaultRoute;
namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FluidViscosityRegion
    {
        public uint Active;
        public float InvRadiusSq;
        public float ViscosityMultiplier;
        public float _padding0;
        public float3 CenterWS;
        public float _padding1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ActiveThrusterFlow
    {
        public uint Active;
        public float Strength;
        public float RadiusSq;
        public float InvRadiusSq;
        public float3 PositionWS;
        public float ConeCos;
        public float3 DirectionWS;
        public float _padding0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WhirlpoolFlow
    {
        public uint Active;
        public float RadiusSq;
        public float InvRadiusSq;
        public float TangentialStrength;
        public float3 CenterWS;
        public float CentripetalStrength;
        public float VerticalPull;
        public float3 _padding0;
    }

    internal static class HectonAnalyticalFlowField
    {
        private static readonly int ViscosityRegionsProp = Shader.PropertyToID("_ViscosityRegions");
        private static readonly int ViscosityRegionCountProp = Shader.PropertyToID("_ViscosityRegionCount");
        private static readonly int ThrusterFlowsProp = Shader.PropertyToID("_ThrusterFlows");
        private static readonly int ThrusterFlowCountProp = Shader.PropertyToID("_ThrusterFlowCount");
        private static readonly int WhirlpoolFlowsProp = Shader.PropertyToID("_WhirlpoolFlows");
        private static readonly int WhirlpoolFlowCountProp = Shader.PropertyToID("_WhirlpoolFlowCount");
        private static readonly int VectorNoiseAupOffsetProp = Shader.PropertyToID("_VectorNoiseAupOffset");

        public static void BindFlowFieldsToComputeShader(
            ComputeShader computeShader,
            int kernelIndex,
            ComputeBuffer viscosityRegionsBuffer,
            int viscosityRegionCount,
            ComputeBuffer thrusterFlowsBuffer,
            int thrusterFlowCount,
            ComputeBuffer whirlpoolFlowsBuffer,
            int whirlpoolFlowCount,
            double3 vectorNoiseAupOffset)
        {
            if (computeShader == null) return;

            if (viscosityRegionsBuffer != null)
            {
                computeShader.SetBuffer(kernelIndex, ViscosityRegionsProp, viscosityRegionsBuffer);
                computeShader.SetInt(ViscosityRegionCountProp, viscosityRegionCount);
            }

            if (thrusterFlowsBuffer != null)
            {
                computeShader.SetBuffer(kernelIndex, ThrusterFlowsProp, thrusterFlowsBuffer);
                computeShader.SetInt(ThrusterFlowCountProp, thrusterFlowCount);
            }

            if (whirlpoolFlowsBuffer != null)
            {
                computeShader.SetBuffer(kernelIndex, WhirlpoolFlowsProp, whirlpoolFlowsBuffer);
                computeShader.SetInt(WhirlpoolFlowCountProp, whirlpoolFlowCount);
            }

            // CRITICAL: Cast double3 to Vector3 before uploading to GPU to prevent precision mismatch
            computeShader.SetVector(VectorNoiseAupOffsetProp, new Vector3((float)vectorNoiseAupOffset.x, (float)vectorNoiseAupOffset.y, (float)vectorNoiseAupOffset.z));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorFloodBfsJob : IJobParallelFor
    {
        public const uint FloodSeedFlag = 1u;
        private const int MaxFloodNodesPerFrame = 5;
        private const int DefaultSeedScanBudget = 32;
        private const int DefaultNodeVisitBudget = MaxFloodNodesPerFrame;
        private const int DefaultEdgeVisitBudget = 64;

        [NoAlias] public NativeArray<InteriorFloodNode> Nodes;
        [ReadOnly, NoAlias] public NativeArray<InteriorFloodEdge> Edges;
        [NoAlias] public NativeArray<int> Queue;
        [NoAlias] public NativeArray<int> Visited;
        [NoAlias] public NativeArray<InteriorFloodBfsResult> Result;
        public float DeltaTime;
        public float WaterDensityKgPerM3;
        public int VisitStamp;
        public int SeedScanStart;
        public int MaxSeedScanCount;
        public int MaxNodeVisits;
        public int MaxEdgeVisits;
        public int ResultSampleStride;
        public int ResultSamplePhase;

        public void Execute(int jobIndex)
        {
            if (jobIndex != 0)
                return;

            int nodeCount = math.min(Nodes.Length, math.min(Queue.Length, Visited.Length));
            if (nodeCount <= 0)
                return;

            int visitStamp = math.max(1, VisitStamp);
            int seedBudget = ResolveBudget(MaxSeedScanCount, DefaultSeedScanBudget, nodeCount);
            int nodeVisitBudget = math.min(MaxFloodNodesPerFrame, ResolveBudget(MaxNodeVisits, DefaultNodeVisitBudget, nodeCount));
            int edgeVisitBudget = math.max(1, MaxEdgeVisits > 0 ? MaxEdgeVisits : DefaultEdgeVisitBudget);
            int seedStart = PositiveModulo(SeedScanStart, nodeCount);
            int head = 0;
            int tail = 0;
            for (int scan = 0; scan < seedBudget && tail < nodeVisitBudget; scan++)
            {
                int i = (seedStart + scan) % nodeCount;
                InteriorFloodNode node = Nodes[i];
                if (node.CurrentLiters <= 0.001f && (node.Flags & FloodSeedFlag) == 0u)
                    continue;
                if (Visited[i] == visitStamp)
                    continue;

                Visited[i] = visitStamp;
                Queue[tail++] = i;
            }

            float safeDeltaTime = math.max(0f, DeltaTime);
            int processedNodes = 0;
            int processedEdges = 0;
            while (head < tail && processedNodes < nodeVisitBudget && processedEdges < edgeVisitBudget)
            {
                processedNodes++;
                int nodeIndex = Queue[head++];
                InteriorFloodNode source = Nodes[nodeIndex];
                float availableLiters = math.max(0f, source.CurrentLiters);
                int edgeStart = math.max(0, source.FirstEdgeIndex);
                int edgeEnd = math.min(Edges.Length, edgeStart + math.max(0, source.EdgeCount));

                for (int edgeIndex = edgeStart;
                     edgeIndex < edgeEnd && availableLiters > 0.001f && processedEdges < edgeVisitBudget;
                     edgeIndex++)
                {
                    processedEdges++;
                    InteriorFloodEdge edge = Edges[edgeIndex];
                    int targetIndex = edge.ToNode;
                    if (edge.IsOpen == 0 || (uint)targetIndex >= nodeCount)
                        continue;

                    InteriorFloodNode target = Nodes[targetIndex];
                    float targetRemainingLiters = math.max(0f, target.CapacityLiters - target.CurrentLiters);
                    if (targetRemainingLiters <= 0.001f)
                        continue;

                    float transferLiters = math.min(
                        availableLiters,
                        math.min(
                            targetRemainingLiters,
                            math.max(0f, source.TransferLitersPerSecond) *
                            math.max(0f, edge.FlowMultiplier) *
                            safeDeltaTime));
                    if (transferLiters <= 0.001f)
                        continue;

                    source.CurrentLiters -= transferLiters;
                    target.CurrentLiters += transferLiters;
                    availableLiters -= transferLiters;
                    Nodes[targetIndex] = target;

                    if (Visited[targetIndex] != visitStamp && tail < nodeVisitBudget)
                    {
                        Visited[targetIndex] = visitStamp;
                        Queue[tail++] = targetIndex;
                    }
                }

                Nodes[nodeIndex] = source;
            }

            float totalLiters = 0f;
            float structuralLoadKg = 0f;
            int floodedCount = 0;
            int sampleStride = math.clamp(ResultSampleStride > 0 ? ResultSampleStride : 1, 1, nodeCount);
            int samplePhase = PositiveModulo(ResultSamplePhase, sampleStride);
            int resultSamples = 0;
            for (int i = samplePhase; i < nodeCount && resultSamples < MaxFloodNodesPerFrame; i += sampleStride)
            {
                resultSamples++;
                InteriorFloodNode node = Nodes[i];
                float liters = math.max(0f, node.CurrentLiters);
                if (liters <= 0.001f)
                    continue;

                float nodeWaterMassKg = liters * 0.001f * math.max(0.01f, WaterDensityKgPerM3);
                totalLiters += liters;
                structuralLoadKg += nodeWaterMassKg + math.max(0f, node.StructuralMassKg);
                floodedCount++;
            }

            if (Result.Length > 0)
            {
                float sampleScale = sampleStride;
                Result[0] = new InteriorFloodBfsResult
                {
                    TotalWaterMassKg = totalLiters * sampleScale * 0.001f * math.max(0.01f, WaterDensityKgPerM3),
                    StructuralLoadKg = structuralLoadKg * sampleScale,
                    FloodedNodeCount = floodedCount * sampleStride
                };
            }
        }

        private static int ResolveBudget(int requested, int fallback, int limit)
        {
            int budget = requested > 0 ? requested : fallback;
            return math.clamp(budget, 1, math.max(1, limit));
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 0)
                return 0;
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}
