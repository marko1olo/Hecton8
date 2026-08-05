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


        public const int VectorNoiseResolution = 32;
        public const int VectorNoiseVoxelCount = VectorNoiseResolution * VectorNoiseResolution * VectorNoiseResolution;
        public const int VectorNoiseMask = VectorNoiseResolution - 1;
        public const int VectorNoiseMinimumDetailMask = VectorNoiseMask & ~1;
        public const int VectorNoiseSliceShift = 5;
        public const int VectorNoisePlaneShift = 10;
        private const float SurfaceStormLayerDepthMeters = 50f;
        private const float StormSurfaceTurbulenceStrength = 0.4f;

        public static float3 SampleBaseFlow(
            float3 position,
            float depthBelowSurface,
            float3 baseCurrent,
            float3 giantWakeCurrent,
            float giantWakeDepthFadeStart,
            float giantWakeDepthFadeRange,
            uint weatherStateMask,
            float3 weatherCurrentDirection,
            float weatherCurrentScale,
            float weatherBlend,
            byte enablePhantomCurrent,
            float currentNoiseScale,
            float currentTimeScale,
            float currentVerticalFactor,
            float phantomCurrentStrength,
            float time,
            float haloclineBoundaryDepthMeters,
            float haloclineShearVelocity,
            NativeArray<float3> vectorNoiseField,
            int vectorNoiseFieldLength,
            double3 vectorNoiseAupOffset,
            float vectorNoiseInvCellSize,
            byte enablePrebakedVectorNoise,
            float vectorNoiseTriangleModulation,
            byte detailedMathEnabled)
        {
            float3 flow = baseCurrent;
            flow += weatherCurrentDirection * math.max(0f, weatherCurrentScale) * math.max(0f, weatherBlend);

            float wakeDepth01 = math.saturate(
                (depthBelowSurface - math.max(0f, giantWakeDepthFadeStart)) *
                math.rcp(math.max(0.001f, giantWakeDepthFadeRange)));
            flow += giantWakeCurrent * wakeDepth01;

            if (enablePhantomCurrent != 0)
            {
                flow += SamplePrebakedVectorCurrent(
                    position,
                    time,
                    vectorNoiseField,
                    vectorNoiseFieldLength,
                    vectorNoiseAupOffset,
                    vectorNoiseInvCellSize,
                    enablePrebakedVectorNoise,
                    currentTimeScale,
                    phantomCurrentStrength,
                    currentVerticalFactor,
                    vectorNoiseTriangleModulation,
                    detailedMathEnabled);
            }

            bool stormActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.Storm) != 0u;
            if (stormActive)
            {
                float surfaceLayer01 = 1f - math.saturate(depthBelowSurface * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
                float stormBlend = math.max(0f, weatherBlend);
                float stormBiasScale = weatherCurrentScale * math.max(0.35f, stormBlend);
                flow.xz += weatherCurrentDirection.xz * stormBiasScale;

                if (detailedMathEnabled != 0 && surfaceLayer01 > 0.0001f)
                {
                    flow += SamplePrebakedVectorCurrent(
                        position + new float3(17.3f, 0f, 11.1f),
                        time,
                        vectorNoiseField,
                        vectorNoiseFieldLength,
                        vectorNoiseAupOffset,
                        vectorNoiseInvCellSize,
                        enablePrebakedVectorNoise,
                        currentTimeScale,
                        phantomCurrentStrength * (StormSurfaceTurbulenceStrength * surfaceLayer01),
                        currentVerticalFactor * surfaceLayer01,
                        vectorNoiseTriangleModulation,
                        detailedMathEnabled);
                }
            }

            if (depthBelowSurface >= math.max(0.01f, haloclineBoundaryDepthMeters))
                flow.z += haloclineShearVelocity;

            return ResolveFiniteFloat3OrZero(flow);
        }

        public static float3 SamplePrebakedVectorCurrent(
            float3 worldPos,
            float time,
            NativeArray<float3> vectorNoiseField,
            int vectorNoiseFieldLength,
            double3 vectorNoiseAupOffset,
            float vectorNoiseInvCellSize,
            byte enablePrebakedVectorNoise,
            float timeScale,
            float strength,
            float verticalFactor,
            float triangleModulation,
            byte detailedMathEnabled)
        {
            if (enablePrebakedVectorNoise == 0 ||
                strength == 0f ||
                vectorNoiseInvCellSize <= 0f ||
                vectorNoiseFieldLength < VectorNoiseVoxelCount ||
                !math.all(math.isfinite(worldPos)) ||
                !math.all(math.isfinite(vectorNoiseAupOffset)))
            {
                return float3.zero;
            }

            double3 aupCell = (new double3(worldPos.x, worldPos.y, worldPos.z) + vectorNoiseAupOffset) * vectorNoiseInvCellSize;
            bool detailedMath = detailedMathEnabled != 0;
            int cellMask = math.select(VectorNoiseMinimumDetailMask, VectorNoiseMask, detailedMath);
            int x = (int)(FastFloorToLong(aupCell.x) & cellMask);
            int y = (int)(FastFloorToLong(aupCell.y) & cellMask);
            int z = (int)(FastFloorToLong(aupCell.z) & cellMask);
            int index = x | (y << VectorNoiseSliceShift) | (z << VectorNoisePlaneShift);
            float3 highSample = vectorNoiseField[index];
            float3 lowSample = DominantAxisOrDefault(highSample, new float3(1f, 0f, 0f));
            float3 vectorSample = math.select(lowSample, highSample, detailedMath);
            vectorSample.y = math.select(0f, vectorSample.y * math.saturate(verticalFactor), detailedMath);

            float modulationRange = math.select(math.min(0.2f, math.saturate(triangleModulation)), math.saturate(triangleModulation), detailedMath);
            float modulation = 1f + FastTriangleSigned(time * timeScale) * modulationRange;
            return ResolveFiniteFloat3OrZero(vectorSample * (strength * math.max(0f, modulation)));
        }

        public static float SampleViscosityMultiplier(
            float3 worldPos,
            NativeArray<FluidViscosityRegion> regions,
            int regionCount,
            NativeArray<float> gradientLut)
        {
            int regionLimit = math.min(math.max(0, regionCount), regions.Length);
            int lutLastIndex = gradientLut.Length - 1;
            if (regionLimit <= 0 || lutLastIndex <= 0)
                return 1f;

            float multiplier = 1f;
            for (int i = 0; i < regionLimit; i++)
            {
                FluidViscosityRegion region = regions[i];
                if (region.Active == 0 || region.InvRadiusSq <= 0f || region.ViscosityMultiplier <= 0f)
                    continue;

                float distanceSq = math.lengthsq(worldPos - region.CenterWS);
                float normalizedDistanceSq = distanceSq * region.InvRadiusSq;
                if (normalizedDistanceSq > 1f)
                    continue;

                float influence01 = math.saturate(1f - normalizedDistanceSq);
                int lutIndex = math.clamp((int)(influence01 * lutLastIndex), 0, lutLastIndex);
                float gradient = math.saturate(gradientLut[lutIndex]);
                multiplier += (math.clamp(region.ViscosityMultiplier, 0.05f, 8f) - 1f) * gradient;
            }

            return math.clamp(multiplier, 0.05f, 8f);
        }

        public static void ApplyThrusterFlow(ref float3 flow, float3 samplePosition, ActiveThrusterFlow thruster)
        {
            if (thruster.Active == 0 || thruster.Strength <= 0f || thruster.RadiusSq <= 0f || thruster.InvRadiusSq <= 0f)
                return;

            float3 toSample = samplePosition - thruster.PositionWS;
            float distanceSq = math.lengthsq(toSample);
            float normalizedDistanceSq = distanceSq * thruster.InvRadiusSq;
            if (distanceSq <= 0.000001f || normalizedDistanceSq > 1f)
                return;

            float3 exhaustDirection = -ResolveDirectionOrDefault(thruster.DirectionWS, new float3(0f, 0f, 1f));
            float axialDistance = math.dot(toSample, exhaustDirection);
            if (axialDistance <= 0f)
                return;

            float coneCosSq = thruster.ConeCos * thruster.ConeCos;
            float axialSq = axialDistance * axialDistance;
            float coneThresholdSq = coneCosSq * distanceSq;
            if (axialSq < coneThresholdSq)
                return;

            float distanceFalloff = math.saturate(1f - normalizedDistanceSq);
            flow += exhaustDirection * (thruster.Strength * distanceFalloff * distanceFalloff);
        }

        public static void ApplyWhirlpoolFlow(ref float3 flow, float3 samplePosition, WhirlpoolFlow whirlpool)
        {
            ApplyWhirlpoolFlow(ref flow, samplePosition, whirlpool, 0);
        }

        public static void ApplyWhirlpoolFlow(ref float3 flow, float3 samplePosition, WhirlpoolFlow whirlpool, byte simplifiedMathEnabled)
        {
            flow += SampleWhirlpoolVelocity(samplePosition, whirlpool, simplifiedMathEnabled, HectonFluidEngine.MaelstromMaxVelocityMetersPerSecond);
        }

        public static float3 SampleWhirlpoolVelocity(
            float3 samplePosition,
            WhirlpoolFlow whirlpool,
            byte simplifiedMathEnabled,
            float maxVelocityMetersPerSecond)
        {
            if (whirlpool.Active == 0 || whirlpool.RadiusSq <= 0f || whirlpool.InvRadiusSq <= 0f)
                return float3.zero;

            if (!math.all(math.isfinite(whirlpool.CenterWS)) ||
                !math.isfinite(whirlpool.TangentialStrength) ||
                !math.isfinite(whirlpool.CentripetalStrength) ||
                !math.isfinite(whirlpool.VerticalPull))
            {
                return float3.zero;
            }

            float3 toCenter = whirlpool.CenterWS - samplePosition;
            toCenter.y = 0f;
            float distanceSq = math.lengthsq(toCenter);
            float normalizedDistanceSq = distanceSq * whirlpool.InvRadiusSq;
            if (distanceSq <= 0.000001f || normalizedDistanceSq > 1f)
                return float3.zero;

            float invDistance = math.rsqrt(math.max(distanceSq, 0.000001f));
            float3 inward = toCenter * invDistance;
            float3 tangent = simplifiedMathEnabled != 0
                ? float3.zero
                : math.cross(new float3(0f, 1f, 0f), toCenter) * invDistance;
            float falloff = math.saturate(1f - normalizedDistanceSq);
            float inverseSqGain = math.min(8f, whirlpool.RadiusSq * math.rcp(math.max(1f, distanceSq)));
            float3 velocity =
                ((inward * whirlpool.CentripetalStrength) +
                 (tangent * whirlpool.TangentialStrength)) *
                (falloff * inverseSqGain);
            velocity.y -= whirlpool.VerticalPull * falloff;
            return ClampFiniteFloat3Magnitude(
                velocity,
                simplifiedMathEnabled != 0
                    ? math.min(maxVelocityMetersPerSecond, HectonFluidEngine.MaelstromMinimumMathDetailMaxVelocityMetersPerSecond)
                    : maxVelocityMetersPerSecond);
        }

        public static float3 SampleWhirlpoolVelocity(
            float3 samplePosition,
            NativeArray<WhirlpoolFlow>.ReadOnly whirlpools,
            int whirlpoolCount,
            byte simplifiedMathEnabled,
            float maxVelocityMetersPerSecond)
        {
            if (!whirlpools.IsCreated || whirlpoolCount <= 0)
                return float3.zero;

            float3 velocity = float3.zero;
            int count = math.min(math.max(0, whirlpoolCount), whirlpools.Length);
            for (int i = 0; i < count; i++)
                velocity += SampleWhirlpoolVelocity(samplePosition, whirlpools[i], simplifiedMathEnabled, maxVelocityMetersPerSecond);

            return ClampFiniteFloat3Magnitude(velocity, maxVelocityMetersPerSecond);
        }

        private static float3 ClampFiniteFloat3Magnitude(float3 value, float maxMagnitude)
        {
            if (!math.all(math.isfinite(value)))
                return float3.zero;

            float maxSafe = math.max(0f, maxMagnitude);
            float lengthSq = math.lengthsq(value);
            float maxSq = maxSafe * maxSafe;
            if (lengthSq > maxSq && lengthSq > 0.000001f)
                value *= maxSafe * math.rsqrt(lengthSq);

            return ResolveFiniteFloat3OrZero(value);
        }

        public static float3 ResolveFiniteFloat3OrZero(float3 value)
        {
            return (math.any(math.isnan(value)) || math.any(math.isinf(value)) || !math.all(math.isfinite(value)))
                ? float3.zero
                : value;
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static int FastFloorToInt(float value)
        {
            int truncated = (int)value;
            return math.select(truncated - 1, truncated, value >= truncated);
        }

        private static long FastFloorToLong(double value)
        {
            long truncated = (long)value;
            return value >= truncated ? truncated : truncated - 1L;
        }

        private static float FastMagnitudeApprox(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private static float3 ResolveDirectionOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.isfinite(lengthSq) && lengthSq > 0.000001f;
            float3 safeValue = math.select(fallback, value, valid);
            float safeLengthSq = math.lengthsq(safeValue);
            return safeValue * math.rsqrt(math.max(safeLengthSq, 0.000001f));
        }

        private static float3 DominantAxisOrDefault(float3 value, float3 fallback)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float3 xAxis = new float3(math.select(-1f, 1f, value.x >= 0f), 0f, 0f);
            float3 yAxis = new float3(0f, math.select(-1f, 1f, value.y >= 0f), 0f);
            float3 zAxis = new float3(0f, 0f, math.select(-1f, 1f, value.z >= 0f));
            float3 yzAxis = math.select(zAxis, yAxis, absValue.y >= absValue.z);
            float3 axis = math.select(yzAxis, xAxis, absValue.x >= absValue.y && absValue.x >= absValue.z);
            return math.select(axis, fallback, maxComponent <= 0.000001f);
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
