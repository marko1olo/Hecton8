// HectonVoxelEngine.cs
// Project HECTON-8 localized voxel volumes.
// Unity 6 URP. Burst + Jobs. Marching Cubes. Multi-primitive SDF.

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Threading;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.Caves;
using Hecton8.Bootstrap;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.Data;
using Hecton8.Dev;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.World;
using Hecton8.World.VoxelSurfaceNets;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

// -------------------------------------------------------------------------------
//  REGION: MARCHING CUBES LOOKUP TABLES (unchanged from v3.2)
// -------------------------------------------------------------------------------

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: MC RAW VERTEX (unchanged)
// ════════════════════════════════════════════════════════════════════════════════

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: BURST JOBS
// ════════════════════════════════════════════════════════════════════════════════
internal static class VoxelDensityPipelineFaultSlots
{
    public const int SlotCount = 8;
    public const int DensityEvaluation = 0;
    public const int QuantizeInput = 1;
    public const int MarchingCubesCountInput = 2;
    public const int MarchingCubesExtractInput = 3;
    public const int MarchingCubesExtractOutput = 4;
    public const int WeldInput = 5;
    public const int WeldOutput = 6;
    public const int NormalFallback = 7;
}

#region Voxel Burst Jobs

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 1: DENSITY FIELD — Multi-primitive SDF cave system (v4.0 REWRITE)
// ═══════════════════════════════════════════════════════════════════════════════
// R99: FloatMode.Deterministic (was Fast). This job is the LIVE owner of cave/terrain density, and
// voxel save deltas are stored relative to deterministic seed state. Fast-math reassociation makes the
// same seed produce a different field across Burst versions/targets, which silently invalidates every
// stored delta and breaks X-Ray before/after comparison. If this regresses the frame budget, the lever
// is voxel resolution / rebuild cadence (GlobalQualityWeight), NOT reverting determinism.
// PENDING VERIFICATION: no profiler capture for the Fast->Deterministic cost has been taken.
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDensityJob : IJobParallelFor
{
    private const byte DeltaModeAdditive = 1 << 0;
    private const byte DeltaModeReplace = 1 << 1;
    private const float AlienBiomeFullLodNoiseFrequency = 0.19f;
    private const float AlienBiomeMidLodNoiseFrequency = 0.11f;
    private const uint AlienBiomeNoiseSeed = 0xA11E5DFu;
    private const float MinSafeEntranceRadius = 0.1f;
    private const float MaxSafeEntranceRadius = 32f;
    private const float MinSafeEntranceFunnelLength = 0.25f;
    private const float MaxSafeEntranceFunnelLength = 128f;
    private const float MinSafeEntranceInnerRadius = 0.05f;
    private const float MinSafeGraphRadius = 0.1f;
    private const float MaxSafeGraphRadius = 256f;
    private const float MaxSafeGraphBlendRadius = 96f;
    private const float MaxSafeTunnelScale = 8f;
    private const float MaxSafeTunnelWarpAmount = 64f;
    private const float MaxSafeGraphNoiseScale = 8f;
    private const float MaxSafeGraphNoiseAmplitude = 8f;

    // ── Grid dimensions ──
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    // ── Terrain ──
    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;

    // ── Cave SDF primitives ──
    [ReadOnly, NoAlias] public NativeArray<float> gridBiome;
    [ReadOnly, NoAlias] public NativeArray<CaveNode> caveNodes;
    [ReadOnly, NoAlias] public NativeArray<CaveTunnel> caveTunnels;
    [ReadOnly, NoAlias] public NativeArray<CaveEntrance> caveEntrances;
    [ReadOnly, NoAlias] public NativeArray<CaveStructure> caveStructures;
    [ReadOnly, NoAlias] public NativeArray<VoxelCraterStamp> craterStamps;
    [ReadOnly, NoAlias] public NativeArray<VoxelModifiedCellEntry> modifiedCells;
    [ReadOnly, NoAlias] public NativeArray<int> modifiedCellBucketHeads;
    [ReadOnly, NoAlias] public NativeArray<int> modifiedCellNext;
    public int modifiedCellCount;
    public int modifiedCellBucketCount;
    [ReadOnly, NoAlias] public NativeArray<int> nodeBucketOffsets;
    [ReadOnly, NoAlias] public NativeArray<int> nodeBucketIndices;
    [ReadOnly, NoAlias] public NativeArray<int> tunnelBucketOffsets;
    [ReadOnly, NoAlias] public NativeArray<int> tunnelBucketIndices;

    // ── Cave parameters ──
    public CaveGenerationParams caveParams;
    public float3 absoluteNoiseOffset;
    public double3 absoluteCellOffset;
    public int partitionDimX;
    public int partitionDimY;
    public int partitionDimZ;
    public float3 partitionOrigin;
    public float3 partitionInvCellSize;

    // ── Edge sealing ──
    public float sealMargin;
    public int lodLevel;
    public float lodTransitionBand;
    public int enableBiomeSdfModifiers;

    // ── Procedural Cave Parameters ──
    public float PrimaryFrequency;
    public float SecondaryFrequency;
    public float CarveStrengthMeters;
    public float CaveThreshold;
    public float MaxCrustDepthMeters;
    public float SurfaceProtectionMeters;
    public float StrataLayerThicknessMeters;
    public float StrataShelvingStrength;
    public uint WorldSeed;

    // ── Output ──
    [WriteOnly, NoAlias] public NativeArray<float> density;
    [WriteOnly, NoAlias] public NativeArray<float> smoothDensity;
    [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> densityFaultFlags;

    // ════════════════════════════════════════════════════════════════════════
    //  EXECUTE — Per voxel point
    // ════════════════════════════════════════════════════════════════════════

    public void Execute(int idx)
    {
        if (!HasSafeDensityBuffers(idx))
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.DensityEvaluation);
            return;
        }

        int ix = idx % ptsX;
        int iy = (idx / ptsX) % ptsY;
        int iz = idx / (ptsX * ptsY);

        float3 wp = volumeOrigin + new float3(ix, iy, iz) * voxelStep;
        EvaluateDensityAt(wp, out float smoothDensityValue, out float finalDensityValue);
        bool invalidDensity = !math.isfinite(finalDensityValue) || !math.isfinite(smoothDensityValue);
        if (invalidDensity)
            MarkDensityFault(VoxelDensityPipelineFaultSlots.DensityEvaluation);

        finalDensityValue = math.select(0f, finalDensityValue, math.isfinite(finalDensityValue));
        smoothDensityValue = math.select(0f, smoothDensityValue, math.isfinite(smoothDensityValue));
        density[idx] = finalDensityValue;
        smoothDensity[idx] = smoothDensityValue;
    }

    bool HasSafeDensityBuffers(int idx)
    {
        long totalPoints = (long)ptsX * ptsY * ptsZ;
        long terrainGridLength = (long)ptsX * ptsZ;
        return density.IsCreated &&
            smoothDensity.IsCreated &&
            terrainHeights.IsCreated &&
            gridBiome.IsCreated &&
            idx >= 0 &&
            idx < density.Length &&
            idx < smoothDensity.Length &&
            ptsX > 0 && ptsY > 0 && ptsZ > 0 &&
            totalPoints > 0L &&
            terrainGridLength > 0L &&
            idx < totalPoints &&
            totalPoints <= density.Length &&
            totalPoints <= smoothDensity.Length &&
            terrainGridLength <= terrainHeights.Length &&
            terrainGridLength <= gridBiome.Length &&
            math.isfinite(voxelStep) &&
            voxelStep > 0f &&
            math.all(math.isfinite(volumeOrigin));
    }

    void MarkDensityFault(int slot)
    {
        if (densityFaultFlags.IsCreated && (uint)slot < (uint)densityFaultFlags.Length)
            densityFaultFlags[slot] = 1;
    }

    void EvaluateDensityAt(float3 wp, out float smoothDensityValue, out float finalDensityValue)
    {
        float terrainH = SampleTerrainHeight(wp.xz);
        float terrainDensity = VoxelSeamDirector.ComputeTerrainDensity(terrainH, wp.y);

        double3 wpAup = new double3(wp.x, wp.y, wp.z) + absoluteCellOffset;
        const double wrapPeriod = 6627.0;
        float3 p = new float3(
            (float)Fmod(wpAup.x, wrapPeriod),
            (float)Fmod(wpAup.y, wrapPeriod),
            (float)Fmod(wpAup.z, wrapPeriod));

        float3 seedOffset = ResolveSeedOffset(WorldSeed);
        float caveSdf = EvaluateGyroidCellularCaveSdf(p, seedOffset);
        if (HasAuthoredCaveSdfPayload())
        {
            EvaluateCaveSDF(wp, out float graphSmoothCaveSdf, out float graphFinalCaveSdf);
            float authoredCaveSdf = math.select(graphSmoothCaveSdf, graphFinalCaveSdf, math.isfinite(graphFinalCaveSdf));
            if (math.isfinite(authoredCaveSdf))
            {
                float graphBlend = math.max(voxelStep * 3.0f, 1.5f);
                caveSdf = SmoothMinQuadratic(caveSdf, authoredCaveSdf, graphBlend);
            }
        }

        float depthBelowSurface = terrainH - wp.y;
        float protectionDepth = math.max(SurfaceProtectionMeters, voxelStep * 2.0f);
        float protected01 = math.saturate(depthBelowSurface / protectionDepth);
        float smoothProtected = protected01 * protected01 * (3f - protected01 * 2f);
        float exponentialProtected = 1f - math.exp(-protected01 * protected01 * 5.25f);
        float surfaceFade = math.saturate(smoothProtected * exponentialProtected);
        float effectiveCaveSdf = math.lerp(CarveStrengthMeters, caveSdf, surfaceFade);

        float blendK = math.max(voxelStep * 2.5f, 2.0f);
        float carvedDensity = -SmoothSubtractionQuadratic(effectiveCaveSdf, -terrainDensity, blendK);

        smoothDensityValue = carvedDensity;
        finalDensityValue = carvedDensity;
        ApplyAlienBiomeSdfModifier(wp, ref smoothDensityValue, ref finalDensityValue);
        smoothDensityValue = ApplyModifiedCellDensity(wp, smoothDensityValue);
        finalDensityValue = ApplyModifiedCellDensity(wp, finalDensityValue);
        smoothDensityValue = ApplyEdgeSeal(wp, smoothDensityValue);
        finalDensityValue = ApplyEdgeSeal(wp, finalDensityValue);
    }

    bool HasAuthoredCaveSdfPayload()
    {
        return (caveNodes.IsCreated && caveNodes.Length > 0) ||
            (caveTunnels.IsCreated && caveTunnels.Length > 0) ||
            (caveEntrances.IsCreated && caveEntrances.Length > 0) ||
            (caveStructures.IsCreated && caveStructures.Length > 0);
    }

    float ApplyModifiedCellDensity(float3 wp, float densityValue)
    {
        if (!TryResolveModifiedCell(ResolveAbsoluteCell(wp), out VoxelModifiedCell cell))
            return densityValue;

        float deltaDensity = (float)cell.Density;
        if (!math.isfinite(deltaDensity))
            return densityValue;

        if ((cell.Flags & DeltaModeReplace) != 0)
            return deltaDensity;

        return (cell.Flags & DeltaModeAdditive) != 0
            ? math.max(densityValue, deltaDensity)
            : math.min(densityValue, deltaDensity);
    }

    void ApplyAlienBiomeSdfModifier(float3 wp, ref float smoothDensityValue, ref float finalDensityValue)
    {
        if (enableBiomeSdfModifiers == 0)
            return;

        float biomeWeight = SampleBiomeModifier(wp.xz);
        if (biomeWeight <= 0.0001f)
            return;

        float lodWeight = lodLevel <= 0 ? 1f : (lodLevel == 1 ? 0.45f : 0f);
        if (lodWeight <= 0f)
            return;

        float surfaceBand = math.max(voxelStep * 6f, 0.5f);
        float surfaceMask = 1f - math.smoothstep(voxelStep * 0.5f, surfaceBand, math.abs(finalDensityValue));
        float modifierWeight = math.saturate(biomeWeight * surfaceMask * lodWeight);
        if (modifierWeight <= 0.0001f)
            return;

        float3 noisePosition = wp + absoluteNoiseOffset;
        float frequency = lodLevel <= 0 ? AlienBiomeFullLodNoiseFrequency : AlienBiomeMidLodNoiseFrequency;
        float organicNoise = lodLevel <= 0
            ? FractalNoise3D(noisePosition * frequency, 1f, 2, 2.03f, 0.5f, AlienBiomeNoiseSeed)
            : Noise3D(noisePosition * frequency);
        float organicBubbleSdf = (organicNoise - 0.56f) * math.max(voxelStep * 3.5f, 0.35f);
        float blendK = math.max(voxelStep * 1.75f, 0.25f);
        float modifiedSmooth = SmoothMinQuadratic(smoothDensityValue, organicBubbleSdf, blendK);
        float modifiedFinal = SmoothMinQuadratic(finalDensityValue, organicBubbleSdf, blendK);
        smoothDensityValue = math.lerp(smoothDensityValue, modifiedSmooth, modifierWeight);
        finalDensityValue = math.lerp(finalDensityValue, modifiedFinal, modifierWeight);
    }

    float SampleBiomeModifier(float2 worldXZ)
    {
        long biomeGridLength = (long)ptsX * ptsZ;
        if (!gridBiome.IsCreated ||
            ptsX <= 1 ||
            ptsZ <= 1 ||
            biomeGridLength <= 0L ||
            biomeGridLength > gridBiome.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f)
        {
            return 0f;
        }

        float invVoxelStep = math.rcp(voxelStep);
        float localX = (worldXZ.x - volumeOrigin.x) * invVoxelStep;
        float localZ = (worldXZ.y - volumeOrigin.z) * invVoxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float v00 = gridBiome[x0 + z0 * ptsX];
        float v10 = gridBiome[x1 + z0 * ptsX];
        float v01 = gridBiome[x0 + z1 * ptsX];
        float v11 = gridBiome[x1 + z1 * ptsX];
        return math.saturate(math.lerp(math.lerp(v00, v10, fx), math.lerp(v01, v11, fx), fz));
    }

    int3 ResolveAbsoluteCell(float3 localPosition)
    {
        double inverseStep = 1.0d / math.max((double)voxelStep, 0.0001d);
        double3 absolutePosition = new double3(localPosition.x, localPosition.y, localPosition.z) + absoluteCellOffset;
        return (int3)math.floor(absolutePosition * inverseStep);
    }

    bool TryResolveModifiedCell(int3 absoluteCell, out VoxelModifiedCell cell)
    {
        cell = default;
        if (!modifiedCells.IsCreated || modifiedCellCount <= 0)
            return false;

        if (!modifiedCellBucketHeads.IsCreated || !modifiedCellNext.IsCreated || modifiedCellBucketCount <= 0)
            return false;

        int count = math.min(modifiedCellCount, math.min(modifiedCells.Length, modifiedCellNext.Length));
        int bucketCount = math.min(modifiedCellBucketCount, modifiedCellBucketHeads.Length);
        if (count <= 0 || bucketCount <= 0)
            return false;

        int cursor = modifiedCellBucketHeads[ResolveModifiedCellBucket(absoluteCell, bucketCount)];
        int guard = 0;
        while ((uint)cursor < (uint)count && guard < count)
        {
            VoxelModifiedCellEntry entry = modifiedCells[cursor];
            if (math.all(entry.AbsoluteCell == absoluteCell))
            {
                cell = entry.Cell;
                return true;
            }

            cursor = modifiedCellNext[cursor];
            guard++;
        }

        return false;
    }

    static int ResolveModifiedCellBucket(int3 cell, int bucketCount)
    {
        return (int)Hecton8.PureLogic.Systems.VoxelCellDirtystateBitHashingCalculator.Compute(cell.x, cell.y, cell.z, bucketCount);
    }

    float SampleTerrainHeight(float2 worldXZ)
    {
        float localX = (worldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (worldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = terrainHeights[x0 + z0 * ptsX];
        float h10 = terrainHeights[x1 + z0 * ptsX];
        float h01 = terrainHeights[x0 + z1 * ptsX];
        float h11 = terrainHeights[x1 + z1 * ptsX];
        return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
    }

    float ApplyEdgeSeal(float3 wp, float densityValue)
    {
        float3 localPos = wp - volumeOrigin;
        float3 volumeSize = new float3(ptsX - 1, ptsY - 1, ptsZ - 1) * voxelStep;
        float dMinX = math.min(localPos.x, volumeSize.x - localPos.x);
        float dMinZ = math.min(localPos.z, volumeSize.z - localPos.z);
        float dMinYBottom = localPos.y;
        float dMinYTop = volumeSize.y - localPos.y;
        float topSealStrength = 1f;
        float bottomSealStrength = 1f;

        for (int e = 0; e < caveEntrances.Length; e++)
        {
            CaveEntrance entrance = caveEntrances[e];
            if (!TryResolveSafeEntrance(in entrance, out float3 surfacePosition, out float3 direction, out float radius, out float innerRadius, out float funnelLength))
                continue;

            float influenceRadius = math.max(radius * 2.6f, innerRadius + funnelLength * 0.35f);
            float2 horizontalDelta = wp.xz - surfacePosition.xz;
            float horizontalDistSq = math.lengthsq(horizontalDelta);
            float influenceRadiusSq = influenceRadius * influenceRadius;
            if (horizontalDistSq >= influenceRadiusSq)
                continue;

            float horizontalDist = FastMagnitude(horizontalDistSq);
            float exemption = 1f - math.smoothstep(radius * 0.4f, influenceRadius, horizontalDist);
            topSealStrength = math.min(topSealStrength, 1f - exemption);
            if (direction.y > 0.3f)
                bottomSealStrength = math.min(bottomSealStrength, 1f - exemption);
        }

        float effectiveYTop = dMinYTop / math.max(topSealStrength, 0.01f);
        float effectiveYBottom = dMinYBottom / math.max(bottomSealStrength, 0.01f);
        float dMinY = math.min(effectiveYBottom, effectiveYTop);
        float horizontalSealMargin = math.max(sealMargin + (lodLevel > 0 ? lodTransitionBand : 0f), 0.01f);
        float verticalSealMargin = math.max(sealMargin, 0.01f);
        float horizontalEdge = math.min(dMinX, dMinZ);
        float horizontalSeal = math.saturate(horizontalEdge / horizontalSealMargin);
        float verticalSeal = math.saturate(dMinY / verticalSealMargin);
        float sealFactor = math.min(horizontalSeal, verticalSeal);
        return math.lerp(1f, densityValue, sealFactor);
    }

    float EvaluateEntranceSkirtSDF(float3 wp)
    {
        float skirtDist = 99999f;

        for (int i = 0; i < caveEntrances.Length; i++)
        {
            CaveEntrance entrance = caveEntrances[i];
            if (!TryResolveSafeEntrance(in entrance, out float3 surfacePosition, out float3 direction, out float radius, out float innerRadius, out float funnelLength))
                continue;

            float3 innerPoint = surfacePosition + direction * funnelLength;
            float embedDepth = math.max(5f, math.max(voxelStep * 1.5f, radius * 0.35f));
            float transitionZone = math.clamp(math.max(2.5f, radius * 0.18f), 2f, 3.5f);
            float3 skirtStart = surfacePosition + direction * math.min(funnelLength * 0.18f, radius);
            float3 skirtEnd = innerPoint + direction * (innerRadius + embedDepth * 0.6f);

            float outer = SDCapsuleConic(
                wp,
                skirtStart,
                skirtEnd,
                radius * 1.35f,
                math.max(innerRadius * 1.55f, innerRadius + 1.35f));

            float inner = SDCapsuleConic(
                wp,
                surfacePosition - direction * embedDepth,
                innerPoint + direction * (innerRadius * 0.75f),
                radius * 0.92f,
                math.max(innerRadius * 0.92f, 0.1f));

            float shell = math.max(outer, -inner);
            float terrainClip = wp.y - (SampleTerrainHeight(wp.xz) - embedDepth);
            float transitionClip = wp.y - (SampleTerrainHeight(wp.xz) - embedDepth - transitionZone);
            shell = SmoothMaxQuadratic(shell, transitionClip, transitionZone);
            shell = math.max(shell, terrainClip);
            skirtDist = SmoothMinQuadratic(skirtDist, shell, caveParams.entranceBlendK * 0.3f);
        }

        return skirtDist;
    }

    float3 ResolveSafeEntranceDirection(CaveEntrance entrance, float3 baseDirection)
    {
        float3 direction = baseDirection;
        float normalBlend = math.isfinite(entrance.terrainNormalBlend) ? math.saturate(entrance.terrainNormalBlend) : 0f;
        if (normalBlend <= 0f)
            return direction;

        float3 terrainNormal = TryNormalizeFinite(entrance.terrainNormal, out float3 safeTerrainNormal)
            ? safeTerrainNormal
            : new float3(0f, 1f, 0f);
        float3 terrainInward = NormalizeFastOrDefault(-terrainNormal, direction);
        return NormalizeFastOrDefault(math.lerp(direction, terrainInward, normalBlend * 0.55f), direction);
    }

    bool TryResolveSafeEntrance(
        in CaveEntrance entrance,
        out float3 surfacePosition,
        out float3 direction,
        out float radius,
        out float innerRadius,
        out float funnelLength)
    {
        surfacePosition = entrance.surfacePosition;
        direction = default;
        radius = default;
        innerRadius = default;
        funnelLength = default;

        if (!IsFinite(surfacePosition) ||
            !math.isfinite(entrance.radius) ||
            !math.isfinite(entrance.funnelLength) ||
            entrance.radius <= 0f ||
            entrance.funnelLength <= 0f ||
            !TryNormalizeFinite(entrance.inwardDirection, out float3 baseDirection))
        {
            return false;
        }

        radius = math.clamp(entrance.radius, MinSafeEntranceRadius, MaxSafeEntranceRadius);
        funnelLength = math.clamp(entrance.funnelLength, MinSafeEntranceFunnelLength, MaxSafeEntranceFunnelLength);
        float innerRadiusFallback = math.max(radius * 0.6f, MinSafeEntranceInnerRadius);
        innerRadius = math.isfinite(entrance.innerRadius) && entrance.innerRadius > 0f
            ? math.clamp(entrance.innerRadius, MinSafeEntranceInnerRadius, math.max(radius, innerRadiusFallback))
            : innerRadiusFallback;
        direction = ResolveSafeEntranceDirection(entrance, baseDirection);
        return IsFinite(direction);
    }

    bool TryResolveSafeEntranceMouthMask(
        in CaveEntrance entrance,
        out float3 surfacePosition,
        out float radius)
    {
        surfacePosition = entrance.surfacePosition;
        radius = default;
        if (!IsFinite(surfacePosition) ||
            !IsFinite(entrance.inwardDirection) ||
            !math.isfinite(entrance.radius) ||
            entrance.radius <= 0f)
        {
            return false;
        }

        float directionSq = math.lengthsq(entrance.inwardDirection);
        if (!math.isfinite(directionSq) || directionSq <= 0.0001f)
            return false;

        radius = math.clamp(entrance.radius, MinSafeEntranceRadius, MaxSafeEntranceRadius);
        return math.isfinite(radius);
    }

    bool TryResolveSafeNode(in CaveNode source, out CaveNode node)
    {
        node = default;
        if (!IsFinite(source.position) || !IsFinite(source.radii) || math.cmin(source.radii) <= 0f)
            return false;

        node = source;
        node.radii = ClampFinite(source.radii, new float3(MinSafeGraphRadius), MinSafeGraphRadius, MaxSafeGraphRadius);
        node.blendRadius = ClampFinite(source.blendRadius, MinSafeGraphRadius, MinSafeGraphRadius, MaxSafeGraphBlendRadius);
        node.noiseScale = ClampFinite(source.noiseScale, 1f, 0.1f, MaxSafeGraphNoiseScale);
        node.noiseAmplitude = ClampFinite(source.noiseAmplitude, 0f, 0f, MaxSafeGraphNoiseAmplitude);
        return true;
    }

    bool TryResolveSafeTunnel(in CaveTunnel source, out CaveTunnel tunnel)
    {
        tunnel = default;
        if (!IsFinite(source.pointA) ||
            !IsFinite(source.pointB) ||
            !IsFinite(source.radiusA) ||
            !IsFinite(source.radiusB) ||
            source.radiusA <= 0f ||
            source.radiusB <= 0f)
        {
            return false;
        }

        tunnel = source;
        tunnel.radiusA = ClampFinite(source.radiusA, MinSafeGraphRadius, MinSafeGraphRadius, MaxSafeGraphRadius);
        tunnel.radiusB = ClampFinite(source.radiusB, MinSafeGraphRadius, MinSafeGraphRadius, MaxSafeGraphRadius);
        tunnel.blendRadius = ClampFinite(source.blendRadius, MinSafeGraphRadius, MinSafeGraphRadius, MaxSafeGraphBlendRadius);
        tunnel.heightScale = ClampFinite(source.heightScale, 1f, 0.1f, MaxSafeTunnelScale);
        tunnel.widthScale = ClampFinite(source.widthScale, 1f, 0.1f, MaxSafeTunnelScale);
        tunnel.warpAmount = ClampFinite(source.warpAmount, 0f, 0f, MaxSafeTunnelWarpAmount);
        return true;
    }

    bool TryResolveSafeStructure(in CaveStructure source, out CaveStructure structure)
    {
        structure = default;
        if (!IsFinite(source.position) || !IsFinite(source.size) || math.cmax(source.size) <= 0f)
            return false;

        structure = source;
        structure.pointB = IsFinite(source.pointB) ? source.pointB : source.position;
        structure.size = ClampFinite(source.size, new float3(MinSafeGraphRadius), MinSafeGraphRadius, MaxSafeGraphRadius);
        structure.blendRadius = ClampFinite(source.blendRadius, MinSafeGraphRadius, MinSafeGraphRadius, MaxSafeGraphBlendRadius);
        structure.noiseAmount = ClampFinite(source.noiseAmount, 0f, 0f, MaxSafeGraphNoiseAmplitude);
        return true;
    }

    static float ClampFinite(float value, float fallback, float minimum, float maximum)
    {
        return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
    }

    static float3 ClampFinite(float3 value, float3 fallback, float minimum, float maximum)
    {
        return IsFinite(value) ? math.clamp(value, new float3(minimum), new float3(maximum)) : fallback;
    }

    static bool TryNormalizeFinite(float3 value, out float3 normalized)
    {
        normalized = default;
        if (!IsFinite(value))
            return false;

        float lengthSq = math.lengthsq(value);
        if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
            return false;

        normalized = value * math.rsqrt(lengthSq);
        return IsFinite(normalized);
    }

    static bool IsFinite(float value)
    {
        return math.isfinite(value);
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CAVE SDF EVALUATION — Core of the cave generation system
    // ════════════════════════════════════════════════════════════════════════

    void EvaluateCaveSDF(float3 wp, out float smoothCaveDist, out float finalCaveDist)
    {
        float3 absoluteWp = wp + absoluteNoiseOffset;
        float3 warpedPos = ComputeWarpedLocalPosition(wp, absoluteWp, caveParams.warpFrequency, caveParams.warpAmplitude, caveParams.warpOctaves, caveParams.seed);
        float3 warpedAbsolutePos = absoluteWp + (warpedPos - wp);

        smoothCaveDist = 99999f;
        finalCaveDist = 99999f;

        if (caveNodes.IsCreated && caveNodes.Length > 0)
        {
            if (TryGetPartitionRange(nodeBucketOffsets, nodeBucketIndices, wp, out int nodeStart, out int nodeEnd))
            {
                for (int i = nodeStart; i < nodeEnd; i++)
                {
                    int nodeIndex = nodeBucketIndices[i];
                    if ((uint)nodeIndex >= (uint)caveNodes.Length)
                        continue;

                    CaveNode nodeSource = caveNodes[nodeIndex];
                    if (!TryResolveSafeNode(in nodeSource, out CaveNode node))
                        continue;

                    EvaluateRoom(warpedPos, absoluteWp, node, out float smoothNodeDist, out float finalNodeDist);
                    smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, smoothNodeDist, node.blendRadius);
                    finalCaveDist = SmoothMinQuadratic(finalCaveDist, finalNodeDist, node.blendRadius);
                }
            }
            else
            {
                for (int i = 0; i < caveNodes.Length; i++)
                {
                    CaveNode nodeSource = caveNodes[i];
                    if (!TryResolveSafeNode(in nodeSource, out CaveNode node))
                        continue;

                    EvaluateRoom(warpedPos, absoluteWp, node, out float smoothNodeDist, out float finalNodeDist);
                    smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, smoothNodeDist, node.blendRadius);
                    finalCaveDist = SmoothMinQuadratic(finalCaveDist, finalNodeDist, node.blendRadius);
                }
            }
        }

        if (caveTunnels.IsCreated && caveTunnels.Length > 0)
        {
            if (TryGetPartitionRange(tunnelBucketOffsets, tunnelBucketIndices, wp, out int tunnelStart, out int tunnelEnd))
            {
                for (int i = tunnelStart; i < tunnelEnd; i++)
                {
                    int tunnelIndex = tunnelBucketIndices[i];
                    if ((uint)tunnelIndex >= (uint)caveTunnels.Length)
                        continue;

                    CaveTunnel tunnelSource = caveTunnels[tunnelIndex];
                    if (!TryResolveSafeTunnel(in tunnelSource, out CaveTunnel tunnel))
                        continue;

                    float tunnelDist = EvaluateTunnel(warpedPos, absoluteWp, wp, tunnel);
                    smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, tunnelDist, tunnel.blendRadius);
                    finalCaveDist = SmoothMinQuadratic(finalCaveDist, tunnelDist, tunnel.blendRadius);
                }
            }
            else
            {
                for (int i = 0; i < caveTunnels.Length; i++)
                {
                    CaveTunnel tunnelSource = caveTunnels[i];
                    if (!TryResolveSafeTunnel(in tunnelSource, out CaveTunnel tunnel))
                        continue;

                    float tunnelDist = EvaluateTunnel(warpedPos, absoluteWp, wp, tunnel);
                    smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, tunnelDist, tunnel.blendRadius);
                    finalCaveDist = SmoothMinQuadratic(finalCaveDist, tunnelDist, tunnel.blendRadius);
                }
            }
        }

        if (caveEntrances.IsCreated && caveEntrances.Length > 0)
        {
            float entranceBlend = math.max(caveParams.entranceBlendK, voxelStep);
            for (int i = 0; i < caveEntrances.Length; i++)
            {
                float entranceDist = EvaluateEntrance(warpedPos, caveEntrances[i]);
                smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, entranceDist, entranceBlend);
                finalCaveDist = SmoothMinQuadratic(finalCaveDist, entranceDist, entranceBlend);
            }
        }

        if (caveStructures.IsCreated && caveStructures.Length > 0)
        {
            EvaluateStructuresSDF(wp, out float smoothStructDist, out float finalStructDist);
            float structureBlend = math.max(caveParams.structureBlendK, voxelStep);
            smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, smoothStructDist, structureBlend);
            finalCaveDist = SmoothMinQuadratic(finalCaveDist, finalStructDist, structureBlend);
        }

        float baseFinalCaveDist = finalCaveDist;
        float noiseEvalDistance = math.max(caveParams.noiseEvalDistance, voxelStep);
        if (caveEntrances.IsCreated && caveEntrances.Length > 0 && math.abs(baseFinalCaveDist) < noiseEvalDistance)
        {
            float mouthPerturbationMask = EvaluateCaveMouthSdfPerturbationMask(wp);
            if (mouthPerturbationMask > 0.0001f)
            {
                finalCaveDist += EvaluateWallDetail(absoluteWp, baseFinalCaveDist) * mouthPerturbationMask;
                finalCaveDist -= EvaluateFractalNoiseCarve(warpedAbsolutePos, absoluteWp, baseFinalCaveDist) * mouthPerturbationMask;
            }
        }
    }

    bool TryGetPartitionRange(NativeArray<int> bucketOffsets, NativeArray<int> bucketIndices, float3 wp, out int start, out int end)
    {
        start = 0;
        end = 0;
        if (!bucketOffsets.IsCreated || bucketOffsets.Length < 2 || !bucketIndices.IsCreated)
            return false;

        int bucketIndex = ResolvePartitionBucketIndex(wp);
        if (bucketIndex < 0 || bucketIndex + 1 >= bucketOffsets.Length)
            return false;

        int rangeStart = bucketOffsets[bucketIndex];
        int rangeEnd = bucketOffsets[bucketIndex + 1];
        if (rangeStart < 0 || rangeEnd < rangeStart || rangeEnd > bucketIndices.Length)
            return false;

        start = rangeStart;
        end = rangeEnd;
        return true;
    }

    int ResolvePartitionBucketIndex(float3 wp)
    {
        if (partitionDimX <= 0 ||
            partitionDimY <= 0 ||
            partitionDimZ <= 0 ||
            !IsFinite(wp) ||
            !IsFinite(partitionOrigin) ||
            !IsFinite(partitionInvCellSize))
        {
            return -1;
        }

        float fx = math.clamp((wp.x - partitionOrigin.x) * partitionInvCellSize.x, 0f, partitionDimX - 1.0001f);
        float fy = math.clamp((wp.y - partitionOrigin.y) * partitionInvCellSize.y, 0f, partitionDimY - 1.0001f);
        float fz = math.clamp((wp.z - partitionOrigin.z) * partitionInvCellSize.z, 0f, partitionDimZ - 1.0001f);
        int ix = (int)math.floor(fx);
        int iy = (int)math.floor(fy);
        int iz = (int)math.floor(fz);
        return ix + partitionDimX * (iy + partitionDimY * iz);
    }

    float3 ComputeWarpedLocalPosition(float3 localPoint, float3 absolutePoint, float frequency, float amplitude, int octaves, uint seed)
    {
        if (amplitude <= 0.001f)
            return localPoint;

        float3 warpedAbsolute = ApplyDomainWarp(absolutePoint, frequency, amplitude, octaves, seed);
        return localPoint + (warpedAbsolute - absolutePoint);
    }

    float EvaluateFractalNoiseCarve(float3 warpedPos, float3 originalPos, float caveDist)
    {
        float amplitude = caveParams.wallNoiseAmplitude * 0.55f + caveParams.terraceAmplitude * 0.3f;
        if (amplitude <= 0.001f)
            return 0f;

        float surfaceMask = 1f - math.saturate(math.abs(caveDist) / math.max(caveParams.noiseEvalDistance, 0.001f));
        if (surfaceMask <= 0.001f)
            return 0f;

        float coarse = FractalNoise3D(
            warpedPos + new float3(17.1f, 4.3f, 9.7f),
            math.max(caveParams.wallNoiseFrequency * 0.65f, 0.025f),
            math.max(2, caveParams.wallNoiseOctaves - 1),
            caveParams.wallNoiseLacunarity,
            caveParams.wallNoisePersistence,
            caveParams.seed + 401u);

        float medium = FractalNoise3D(
            warpedPos + new float3(3.7f, 13.1f, 5.9f),
            math.max(caveParams.wallNoiseFrequency * 1.25f, 0.05f),
            math.max(2, caveParams.wallNoiseOctaves),
            caveParams.wallNoiseLacunarity,
            caveParams.wallNoisePersistence,
            caveParams.seed + 607u);

        float strata = FractalNoise3D(
            originalPos + new float3(5.3f, 19.7f, 2.1f),
            math.max(caveParams.terraceFrequency * 0.45f, 0.03f),
            3,
            2f,
            0.5f,
            caveParams.seed + 809u);

        float layered = (coarse * 0.45f + medium * 0.4f + strata * 0.15f) * 0.5f + 0.5f;
        float carveMask = math.saturate((layered - 0.22f) * 1.45f);
        float derivativeBudget =
            EstimateFractalDerivative(math.max(caveParams.wallNoiseFrequency * 0.65f, 0.025f), math.max(2, caveParams.wallNoiseOctaves - 1), caveParams.wallNoiseLacunarity, caveParams.wallNoisePersistence) * 0.45f +
            EstimateFractalDerivative(math.max(caveParams.wallNoiseFrequency * 1.25f, 0.05f), math.max(2, caveParams.wallNoiseOctaves), caveParams.wallNoiseLacunarity, caveParams.wallNoisePersistence) * 0.4f +
            EstimateFractalDerivative(math.max(caveParams.terraceFrequency * 0.45f, 0.03f), 3, 2f, 0.5f) * 0.15f;

        float safeAmplitude = ApplyDerivativeSafeAmplitude(amplitude, derivativeBudget);
        return carveMask * safeAmplitude * surfaceMask;
    }

    float EvaluateCraterModifiers(float3 wp, float densityValue)
    {
        for (int i = 0; i < craterStamps.Length; i++)
        {
            VoxelCraterStamp crater = craterStamps[i];
            float outerRadius = crater.radius + math.max(crater.blendRadius, voxelStep);
            float3 craterLocal = AupPrecisionMath.DowncastLocalDelta(crater.position, float3.zero);
            float3 delta = wp - craterLocal;
            if (math.any(math.abs(delta) > outerRadius))
                continue;

            float distSq = math.lengthsq(delta);
            float outerRadiusSq = outerRadius * outerRadius;
            if (distSq >= outerRadiusSq)
                continue;

            float craterDist = FastMagnitude(distSq) - crater.radius;
            if (craterDist >= crater.blendRadius)
                continue;

            densityValue = SmoothSubtractionQuadratic(-craterDist, densityValue, math.max(crater.blendRadius, voxelStep));
        }

        return densityValue;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ROOM SDF — Sphere, Ellipsoid, Shaft, Hall, Crevice
    // ════════════════════════════════════════════════════════════════════════

    static float FastMagnitude(float magnitudeSq)
    {
        float x = math.max(0f, magnitudeSq);
        float safe = math.max(x, 0.000000000001f);
        int estimateBits = (math.asint(safe) >> 1) + 0x1FBD1DF5;
        float estimate = math.asfloat(estimateBits);
        return math.select(0f, 0.5f * (estimate + safe / math.max(estimate, 0.000000000001f)), x > 0f);
    }

    static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
    {
        float lengthSq = math.lengthsq(value);
        return lengthSq > 0.0001f ? value / math.max(LengthApprox(value), 0.0001f) : fallback;
    }

    static float SineEnvelopeCheat01(float t)
    {
        float x = math.saturate(t);
        return x * (1f - x) * 4f;
    }

    static float TriangleWave01(float t)
    {
        float x = math.frac(t);
        return 1f - math.abs(x * 2f - 1f);
    }

    void EvaluateRoom(float3 warpedPos, float3 absoluteOriginalPos, CaveNode node, out float smoothDist, out float finalDist)
    {
        smoothDist = 0f;

        switch (node.roomType)
        {
            case CaveRoomType.Sphere:
                smoothDist = SDSphere(warpedPos, node.position, node.radii.x);
                break;

            case CaveRoomType.Ellipsoid:
                smoothDist = SDEllipsoidAnalytic(warpedPos, node.position, node.radii);
                break;

            case CaveRoomType.VerticalShaft:
                smoothDist = SDVerticalShaft(warpedPos, node.position,
                    node.radii.x, node.radii.y, node.radii.z);
                break;

            case CaveRoomType.FlatHall:
                // Flat hall = ellipsoid with compressed Y
                float3 hallRadii = new float3(
                    node.radii.x * 1.5f,
                    node.radii.y * 0.35f,
                    node.radii.z * 1.5f);
                smoothDist = SDEllipsoidAnalytic(warpedPos, node.position, hallRadii);
                break;

            case CaveRoomType.Crevice:
                // Crevice = ellipsoid with compressed XZ, stretched Y
                float3 creviceRadii = new float3(
                    node.radii.x * 0.25f,
                    node.radii.y * 1.3f,
                    node.radii.z);
                smoothDist = SDEllipsoidAnalytic(warpedPos, node.position, creviceRadii);
                break;

            default:
                smoothDist = SDSphere(warpedPos, node.position, node.radii.x);
                break;
        }

        finalDist = smoothDist;

        if (node.noiseAmplitude > 0.001f)
        {
            float sizeScale = math.max(1f, math.cmax(node.radii) * 0.15f); // 15% of max radius scales the noise
            float scaledNoise = node.noiseAmplitude * sizeScale;
            float scaledFrequency = (node.noiseScale * caveParams.wallNoiseFrequency) / sizeScale;
            float localNoise = Fractal3DFast(
                absoluteOriginalPos * scaledFrequency,
                2, caveParams.seed + 7777u);
            finalDist += localNoise * scaledNoise;
        }

        if (caveParams.floorFlatness > 0.001f && smoothDist < 0f)
        {
            smoothDist = ApplyFloorFlattening(smoothDist, warpedPos, node.position,
                node.radii.y, caveParams.floorFlatness);
        }

        if (caveParams.floorFlatness > 0.001f && finalDist < 0f)
        {
            finalDist = ApplyFloorFlattening(finalDist, warpedPos, node.position,
                node.radii.y, caveParams.floorFlatness);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TUNNEL SDF — Conic capsule with optional cross-section scaling
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateTunnel(float3 warpedPos, float3 absoluteOriginalPos, float3 localOriginalPos, CaveTunnel tunnel)
    {
        float3 evalPos = warpedPos;
        if (tunnel.warpAmount > 0.001f)
        {
            evalPos = ComputeWarpedLocalPosition(
                localOriginalPos,
                absoluteOriginalPos,
                caveParams.warpFrequency * 1.7f,
                tunnel.warpAmount,
                math.min(caveParams.warpOctaves, 2),
                caveParams.seed + 54321u);
        }

        float3 axis = tunnel.pointB - tunnel.pointA;
        float axisLengthSq = math.lengthsq(axis);
        if (axisLengthSq < 0.0001f)
            return SDSphere(evalPos, tunnel.pointA, math.max(tunnel.radiusA, tunnel.radiusB));

        float axisLength = LengthApprox(axis);
        float3 tangent = axis / math.max(axisLength, 0.0001f);
        float lateralAmplitude = math.max(tunnel.warpAmount, math.max(tunnel.heightScale, tunnel.widthScale) * 0.35f);
        float3 controlA = tunnel.pointA + tangent * (axisLength * 0.28f)
            + ComputeTunnelCurveOffset(tunnel.pointA, tunnel.pointB, tangent, 0.25f, lateralAmplitude, 901u);
        float3 controlB = tunnel.pointA + tangent * (axisLength * 0.72f)
            + ComputeTunnelCurveOffset(tunnel.pointA, tunnel.pointB, tangent, 0.75f, lateralAmplitude, 1459u);

        const int segmentCount = 6;
        float tunnelDist = 99999f;
        for (int seg = 0; seg < segmentCount; seg++)
        {
            float t0 = seg / (float)segmentCount;
            float t1 = (seg + 1) / (float)segmentCount;
            float3 p0 = EvaluateCubicBezier(tunnel.pointA, controlA, controlB, tunnel.pointB, t0);
            float3 p1 = EvaluateCubicBezier(tunnel.pointA, controlA, controlB, tunnel.pointB, t1);
            float r0 = math.lerp(tunnel.radiusA, tunnel.radiusB, t0);
            float r1 = math.lerp(tunnel.radiusA, tunnel.radiusB, t1);
            float segmentDist;

            if (tunnel.tunnelType == CaveTunnelType.Round)
            {
                segmentDist = SDCapsuleConic(evalPos, p0, p1, r0, r1);
            }
            else if (tunnel.tunnelType == CaveTunnelType.OpenTrench)
            {
                float baseRadius = math.max((r0 + r1) * 0.5f, 0.1f);
                // Trench: Massive vertical height to cut through the surface terrain completely
                // Width is controlled by standard widthScale (or radius).
                segmentDist = SDCapsuleElliptic(
                    evalPos,
                    p0,
                    p1,
                    baseRadius,
                    200.0f, // Infinite upward/downward extrusion for the open canyon
                    math.max(tunnel.widthScale, 0.5f));
            }
            else
            {
                float baseRadius = math.max((r0 + r1) * 0.5f, 0.1f);
                segmentDist = SDCapsuleElliptic(
                    evalPos,
                    p0,
                    p1,
                    baseRadius,
                    math.max(tunnel.heightScale, 0.2f),
                    math.max(tunnel.widthScale, 0.2f));
            }

            tunnelDist = SmoothMinQuadratic(tunnelDist, segmentDist, math.max(tunnel.blendRadius * 0.35f, 1.5f));
        }

        return tunnelDist;
    }

    float EvaluateEntrance(float3 warpedPos, CaveEntrance entrance)
    {
        if (!TryResolveSafeEntrance(in entrance, out float3 surfacePosition, out float3 direction, out float radius, out float innerRadius, out float funnelLength))
            return 99999f;

        float3 innerPoint = surfacePosition + direction * funnelLength;
        float core = SDCapsuleConic(
            warpedPos,
            surfacePosition,
            innerPoint,
            radius,
            innerRadius);

        float3 flareStart = surfacePosition - direction * math.max(radius * 0.65f, voxelStep);
        float3 flareEnd = surfacePosition + direction * math.min(funnelLength * 0.45f, radius * 2.2f);
        float flare = SDCapsuleConic(
            warpedPos,
            flareStart,
            flareEnd,
            radius * 1.3f,
            math.max(innerRadius, radius * 0.85f));

        return SmoothMinQuadratic(core, flare, caveParams.entranceBlendK * 0.4f);
    }

    float EvaluateCaveMouthSdfPerturbationMask(float3 wp)
    {
        float mask = 1f;
        for (int i = 0; i < caveEntrances.Length; i++)
        {
            CaveEntrance entrance = caveEntrances[i];
            if (!TryResolveSafeEntranceMouthMask(in entrance, out float3 surfacePosition, out float radius))
                continue;

            radius = math.max(radius, voxelStep);
            float distanceSq = math.lengthsq(wp - surfacePosition);
            float inner = radius * 1.35f;
            float outer = radius * 2.75f;
            mask = math.min(mask, math.smoothstep(inner * inner, outer * outer, distanceSq));
        }

        return mask;
    }

    void EvaluateStructuresSDF(float3 wp, out float smoothStructDist, out float finalStructDist)
    {
        float3 absoluteWp = wp + absoluteNoiseOffset;
        smoothStructDist = 99999f;
        finalStructDist = 99999f;

        for (int i = 0; i < caveStructures.Length; i++)
        {
            CaveStructure structureSource = caveStructures[i];
            if (!TryResolveSafeStructure(in structureSource, out CaveStructure s))
                continue;

            float smoothSd;

            switch (s.structureType)
            {
                case CaveStructureType.Column:
                    smoothSd = SDVerticalShaft(wp, s.position, s.size.x, s.size.y, s.size.x * 0.1f);
                    break;

                case CaveStructureType.Bridge:
                    smoothSd = SDCapsuleConic(wp, s.position, s.pointB, s.size.x, s.size.x);
                    break;

                case CaveStructureType.Boulder:
                    smoothSd = SDSphere(wp, s.position, s.size.x);
                    break;

                case CaveStructureType.Stalagmite:
                {
                    float3 tip = s.position + new float3(0f, s.size.y, 0f);
                    smoothSd = SDCapsuleConic(wp, s.position, tip, s.size.x, s.size.z);
                    break;
                }

                case CaveStructureType.Stalactite:
                {
                    float3 hangTip = s.position - new float3(0f, s.size.y, 0f);
                    smoothSd = SDCapsuleConic(wp, s.position, hangTip, s.size.x, s.size.z);
                    break;
                }

                case CaveStructureType.Block:
                case CaveStructureType.Wall:
                    smoothSd = SDBox(wp, s.position, s.size);
                    break;

                case CaveStructureType.Arch:
                    smoothSd = EvaluateArchSDF(wp, s);
                    break;

                default:
                    smoothSd = SDSphere(wp, s.position, s.size.x);
                    break;
            }

            float finalSd = smoothSd;

            if (s.noiseAmount > 0.001f)
            {
                float sizeScale = math.max(1f, math.cmax(s.size) * 0.22f); // 22% of largest axis scales the noise
                float scaledNoiseAmount = s.noiseAmount * sizeScale;
                float scaledFrequency = 0.3f / sizeScale; // Inversely scale frequency to preserve gradient
                float noise = Fractal3DFast((absoluteWp + s.position * 0.17f) * scaledFrequency, 2, caveParams.seed + 9999u) * scaledNoiseAmount;
                if (s.structureType == CaveStructureType.Arch)
                    noise += EvaluateLayeredArchNoise(absoluteWp, s) * scaledNoiseAmount;

                finalSd += noise;
            }

            smoothStructDist = SmoothMinQuadratic(smoothStructDist, smoothSd, s.blendRadius);
            finalStructDist = SmoothMinQuadratic(finalStructDist, finalSd, s.blendRadius);
        }
    }

    float3 ComputeTunnelCurveOffset(float3 pointA, float3 pointB, float3 tangent, float t, float amplitude, uint seedOffset)
    {
        float3 upHint = math.abs(tangent.y) > 0.8f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
        float3 right = NormalizeFastOrDefault(math.cross(upHint, tangent), new float3(1f, 0f, 0f));
        float3 up = NormalizeFastOrDefault(math.cross(tangent, right), new float3(0f, 1f, 0f));
        float3 absolutePointA = pointA + absoluteNoiseOffset;
        float3 absolutePointB = pointB + absoluteNoiseOffset;
        float3 noisePoint = (absolutePointA + absolutePointB) * 0.03125f + new float3(t * 3.1f, t * 5.7f, t * 7.9f);
        float lateralNoise = Fractal3DFast(noisePoint + new float3(13.1f, 1.7f, 0.3f), 2, caveParams.seed + seedOffset);
        float verticalNoise = Fractal3DFast(noisePoint + new float3(2.9f, 11.3f, 4.1f), 2, caveParams.seed + seedOffset + 101u);
        float envelope = SineEnvelopeCheat01(t);
        return (right * lateralNoise + up * verticalNoise * 0.75f) * (amplitude * envelope);
    }

    static float3 EvaluateCubicBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
    {
        float omt = 1f - t;
        return omt * omt * omt * p0
             + 3f * omt * omt * t * p1
             + 3f * omt * t * t * p2
             + t * t * t * p3;
    }

    static float3 EvaluateQuadraticBezier(float3 p0, float3 p1, float3 p2, float t)
    {
        float omt = 1f - t;
        return omt * omt * p0 + 2f * omt * t * p1 + t * t * p2;
    }

    float EvaluateArchSDF(float3 wp, CaveStructure s)
    {
        float3 footA = s.position;
        float3 footB = math.lengthsq(s.pointB - s.position) > 0.01f
            ? s.pointB
            : s.position + new float3(math.max(s.size.x, 2f) * 2f, 0f, 0f);
        float rise = math.max(s.size.y, math.max(s.size.z * 3f, 3f));
        float tubeRadius = math.max(s.size.z, 0.75f);
        float3 crown = (footA + footB) * 0.5f + new float3(0f, rise, 0f);

        const int segmentCount = 6;
        float archDist = 99999f;
        for (int seg = 0; seg < segmentCount; seg++)
        {
            float t0 = seg / (float)segmentCount;
            float t1 = (seg + 1) / (float)segmentCount;
            float3 p0 = EvaluateQuadraticBezier(footA, crown, footB, t0);
            float3 p1 = EvaluateQuadraticBezier(footA, crown, footB, t1);
            float radius0 = math.lerp(tubeRadius * 1.05f, tubeRadius * 0.85f, t0);
            float radius1 = math.lerp(tubeRadius * 1.05f, tubeRadius * 0.85f, t1);
            float segmentDist = SDCapsuleConic(wp, p0, p1, radius0, radius1);
            archDist = SmoothMinQuadratic(archDist, segmentDist, math.max(s.blendRadius * 0.45f, 1.25f));
        }

        return archDist;
    }

    float EvaluateLayeredArchNoise(float3 wp, CaveStructure s)
    {
        float fbm = Fractal3DFast((wp + s.position * 0.13f) * 0.12f, 3, caveParams.seed + 4049u);
        float strata = EvaluateTerrace(
            wp.y + fbm * 2.5f,
            math.max(caveParams.terraceFrequency * 0.55f, 0.08f),
            math.max(caveParams.terraceAmplitude * 0.45f, 0.12f),
            math.max(caveParams.terraceSharpness * 0.8f, 2f));
        return fbm * 0.55f + strata * 0.75f;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WALL DETAIL — Noise + terraces applied near cave surface
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateWallDetail(float3 wp, float currentSDF)
    {
        float detail = 0f;
        float nearSurfaceMask = 1f - math.saturate(math.abs(currentSDF) / math.max(caveParams.noiseEvalDistance, 0.001f));

        // ── Fractal wall noise ──
        if (caveParams.wallNoiseAmplitude > 0.001f)
        {
            float wallNoise = FractalNoise3D(
                wp,
                caveParams.wallNoiseFrequency,
                caveParams.wallNoiseOctaves,
                caveParams.wallNoiseLacunarity,
                caveParams.wallNoisePersistence,
                caveParams.seed);

            detail += wallNoise * caveParams.wallNoiseAmplitude;

            float accretionNoise = FractalNoise3D(
                wp + new float3(9.4f, 17.2f, 3.1f),
                math.max(caveParams.wallNoiseFrequency * 0.7f, 0.04f),
                math.max(2, caveParams.wallNoiseOctaves - 1),
                caveParams.wallNoiseLacunarity,
                caveParams.wallNoisePersistence,
                caveParams.seed + 913u);
            float dripMask = math.saturate((accretionNoise - 0.18f) * 1.4f);
            detail += dripMask * caveParams.wallNoiseAmplitude * 0.45f * nearSurfaceMask;
        }

        // ── Horizontal terraces (rock strata) ──
        if (caveParams.terraceAmplitude > 0.001f)
        {
            float terrace = EvaluateTerrace(
                wp.y,
                caveParams.terraceFrequency,
                caveParams.terraceAmplitude,
                caveParams.terraceSharpness);

            detail += terrace;
        }

        float maxDisplacement = math.max(voxelStep * 0.45f, 0.2f);
        float rawDetail = detail * nearSurfaceMask;
        
        // Soft clamp to prevent jagged plateaus: x / (1 + |x|/max)
        float softClampedDetail = (rawDetail / (1f + math.abs(rawDetail) / maxDisplacement));
        return softClampedDetail;
    }

    float ApplyDerivativeSafeAmplitude(float amplitude, float derivativeBudget)
    {
        float maxAmplitude = math.max(voxelStep * 0.45f, 0.2f);
        if (derivativeBudget <= 0.85f)
            return math.min(amplitude, maxAmplitude);

        return math.min(amplitude * (0.85f / derivativeBudget), maxAmplitude);
    }

    static float EstimateFractalDerivative(float frequency, int octaves, float lacunarity, float persistence)
    {
        float derivative = 0f;
        float octaveFrequency = math.max(frequency, 0.0001f);
        float octaveAmplitude = 1f;
        int octaveCount = math.max(octaves, 1);

        for (int i = 0; i < octaveCount; i++)
        {
            derivative += octaveFrequency * octaveAmplitude;
            octaveFrequency *= math.max(lacunarity, 1f);
            octaveAmplitude *= math.saturate(persistence);
        }

        return derivative;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SDF PRIMITIVES — Inlined for Burst performance
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Signed distance to sphere.</summary>
    static float SDSphere(float3 p, float3 center, float radius)
    {
        return LengthApprox(p - center) - radius;
    }

    /// <summary>Signed distance to axis-aligned ellipsoid (fast approximation).</summary>
    static float SDEllipsoid(float3 p, float3 center, float3 radii)
    {
        // Scale space so ellipsoid becomes unit sphere
        float3 scaled = (p - center) / math.max(radii, 0.001f);
        float lenScaled = LengthApprox(scaled);

        if (lenScaled < 0.0001f)
            return -math.cmin(radii); // Deep inside

        // Approximate: distance in scaled space × minimum radius
        // This is not exact but good enough for MC and much cheaper than analytic
        return (lenScaled - 1f) * math.cmin(radii);
    }

    static float SDEllipsoidAnalytic(float3 p, float3 center, float3 radii)
    {
        float3 q = math.abs(p - center);
        float3 safeRadii = math.max(radii, new float3(0.001f));
        float3 invRadii = 1f / safeRadii;
        float3 invRadiiSq = invRadii / safeRadii;
        float k0 = LengthApprox(q * invRadii);
        float k1 = LengthApprox(q * invRadiiSq);
        return (k0 - 1f) * k0 / math.max(k1, 0.0001f);
    }

    /// <summary>Signed distance to rounded vertical cylinder (shaft/chimney).</summary>
    static float SDVerticalShaft(float3 p, float3 center, float radius,
                                  float halfHeight, float roundness)
    {
        float3 q = p - center;
        float2 d = new float2(
            LengthApprox(q.xz) - radius,
            math.abs(q.y) - halfHeight);

        return math.min(math.max(d.x, d.y), 0f)
             + LengthApprox(math.max(d, 0f))
             - math.max(roundness, 0.01f);
    }

    /// <summary>Signed distance to axis-aligned box.</summary>
    static float SDBox(float3 p, float3 center, float3 halfExtents)
    {
        float3 q = math.abs(p - center) - halfExtents;
        return LengthApprox(math.max(q, 0f)) + math.min(math.cmax(q), 0f);
    }

    /// <summary>Signed distance to conic capsule (different radii at each end).</summary>
    static float SDCapsuleConic(float3 p, float3 a, float3 b,
                                 float radiusA, float radiusB)
    {
        float3 pa = p - a;
        float3 ba = b - a;
        float baba = math.dot(ba, ba);

        if (baba < 0.0001f)
            return LengthApprox(pa) - radiusA; // Degenerate: a ≈ b → sphere

        float h = math.saturate(math.dot(pa, ba) / baba);
        float radius = math.lerp(radiusA, radiusB, h);
        return LengthApprox(pa - ba * h) - radius;
    }

    /// <summary>Signed distance to capsule with elliptic cross-section.
    /// Creates tall narrow or wide flat tunnel profiles.</summary>
    static float SDCapsuleElliptic(float3 p, float3 a, float3 b,
                                    float radius, float heightScale, float widthScale)
    {
        float3 pa = p - a;
        float3 ba = b - a;
        float baba = math.dot(ba, ba);

        if (baba < 0.0001f)
            return LengthApprox(pa) - radius;

        float h = math.saturate(math.dot(pa, ba) / baba);
        float3 closest = pa - ba * h;

        // Build local coordinate frame perpendicular to tunnel direction
        float3 forward = NormalizeApproxOr(ba, new float3(0f, 0f, 1f));
        float3 up = new float3(0, 1, 0);

        // Handle near-vertical tunnels
        if (math.abs(math.dot(forward, up)) > 0.99f)
            up = new float3(1, 0, 0);

        float3 right = NormalizeApproxOr(math.cross(forward, up), new float3(1f, 0f, 0f));
        up = math.cross(right, forward);

        // Project onto local axes and scale
        float projRight = math.dot(closest, right);
        float projUp = math.dot(closest, up);

        // Elliptic scaling
        float safeWidth = math.max(widthScale, 0.01f);
        float safeHeight = math.max(heightScale, 0.01f);
        float2 scaled = new float2(projRight / safeWidth, projUp / safeHeight);

        return LengthApprox(scaled) - radius;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CSG OPERATIONS — Smooth blending
    // ════════════════════════════════════════════════════════════════════════

    static float LengthApprox(float3 value)
    {
        return math.length(value);
    }

    static float LengthApprox(float2 value)
    {
        return math.length(value);
    }

    static float3 NormalizeApproxOr(float3 value, float3 fallback)
    {
        if (math.lengthsq(value) <= 0.0001f)
            return fallback;

        return value / math.max(LengthApprox(value), 0.0001f);
    }

    /// <summary>Polynomial smooth minimum (cubic). Merges shapes organically.</summary>
    static float SmoothMin(float a, float b, float k)
    {
        k = math.max(k, 0.0001f);
        float h = math.max(k - math.abs(a - b), 0f) / k;
        return math.min(a, b) - h * h * h * k * (1f / 6f);
    }

    static float SmoothMinQuadratic(float a, float b, float k)
    {
        float width = math.max(k, 0.0001f);
        float blend = math.max(0f, width - math.abs(a - b));
        float smoothDrop = (blend * blend) * (0.25f / width);
        return math.min(a, b) - smoothDrop;
    }

    /// <summary>Smooth maximum. Inverse of smooth min.</summary>
    static float SmoothMax(float a, float b, float k)
    {
        return -SmoothMin(-a, -b, k);
    }

    static float SmoothMaxQuadratic(float a, float b, float k)
    {
        return -SmoothMinQuadratic(-a, -b, k);
    }

    /// <summary>Smooth subtraction: carve shape B out of shape A.</summary>
    static float SmoothSubtraction(float distCarve, float distBase, float k)
    {
        return SmoothMax(distBase, -distCarve, k);
    }

    static float SmoothSubtractionQuadratic(float distCarve, float distBase, float k)
    {
        return SmoothMaxQuadratic(distBase, -distCarve, k);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  NOISE FUNCTIONS — Burst-safe, no managed allocations
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>3D gradient noise via Unity.Mathematics.noise.snoise.
    /// Returns [-1, 1] range.</summary>
    static float Noise3D(float3 p)
    {
        return noise.snoise(p);
    }

    /// <summary>Fractal Brownian Motion — layered noise.</summary>
    static float FractalNoise3D(float3 p, float frequency, int octaves,
                                 float lacunarity, float persistence, uint seed)
    {
        float seedOff = seed * 0.01317f;
        float3 pp = p * frequency + seedOff;

        float value = 0f;
        float amplitude = 1f;
        float maxAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Noise3D(pp) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            pp *= lacunarity;
        }

        return value / math.max(maxAmplitude, 0.001f);
    }

    /// <summary>Fast 2-octave fractal noise. Used for per-room detail
    /// and domain warping where full FBM is overkill.</summary>
    static float Fractal3DFast(float3 p, int octaves, uint seed)
    {
        float seedOff = seed * 0.00731f;
        float3 pp = p + seedOff;

        float v = Noise3D(pp);
        if (octaves > 1)
            v = v * 0.7f + Noise3D(pp * 2.17f) * 0.3f;

        return v;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DOMAIN WARPING — Distort coordinates with noise
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Warp world coordinates using 3-channel fractal noise.
    /// Each axis is offset by a different noise channel.
    /// This makes straight tunnels curve organically.
    /// </summary>
    float3 ApplyDomainWarp(float3 p, float frequency, float amplitude,
                            int octaves, uint seed)
    {
        float seedOff = seed * 0.00419f;

        // Three independent noise channels for XYZ displacement
        float3 noiseInput = p * frequency;

        float dx = FractalNoise3D(noiseInput + new float3(seedOff, 0f, 0f),
            1f, octaves, 2f, 0.5f, seed);
        float dy = FractalNoise3D(noiseInput + new float3(0f, seedOff, 0f),
            1f, octaves, 2f, 0.5f, seed + 111u);
        float dz = FractalNoise3D(noiseInput + new float3(0f, 0f, seedOff),
            1f, octaves, 2f, 0.5f, seed + 222u);

        return p + new float3(dx, dy, dz) * amplitude;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TERRACE — Horizontal rock strata layers
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Evaluate horizontal terrace effect.
    /// Creates periodic ledges in cave walls based on Y coordinate.</summary>
    static float EvaluateTerrace(float y, float frequency, float amplitude, float sharpness)
    {
        float scaled = y * frequency;
        float fractional = math.frac(scaled);
        float wave = TriangleWave01(fractional);
        float terrace = wave * wave * (3f - 2f * wave);
        float sharper = terrace * terrace * (3f - 2f * terrace);
        terrace = math.lerp(terrace, sharper, math.saturate((sharpness - 1f) * 0.5f));
        return terrace * amplitude;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ORGANIC FLOOR DEPOSITION — Passable rubble and sediment, not flat highways
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Biases only the bottom room band toward an uneven depositional floor.</summary>
    static float ApplyFloorFlattening(float sdfDist, float3 p, float3 roomCenter,
                                       float roomRadiusY, float flatness)
    {
        float floorY = roomCenter.y - roomRadiusY;
        float heightAboveFloor = p.y - floorY;
        float floorZone = roomRadiusY * 0.3f;
        if (heightAboveFloor <= 0f || heightAboveFloor >= floorZone || sdfDist >= 0f)
            return sdfDist;

        float floorMask = 1f - math.smoothstep(0f, floorZone, heightAboveFloor);
        float dunePhase = p.x * 0.23f + p.z * 0.17f + roomCenter.x * 0.031f - roomCenter.z * 0.027f;
        float dune = (math.sin(dunePhase) * 0.5f + math.sin(dunePhase * 1.71f + 1.37f) * 0.25f + 0.75f) * roomRadiusY * 0.035f;
        float rubble = RidgedMultifractal(p * 0.31f + roomCenter * 0.071f, 0xD3A17E5u, 2) * roomRadiusY * 0.025f;
        float organicPlane = heightAboveFloor - floorZone * 0.18f + (dune + rubble) * floorMask;
        float passableBlend = math.saturate(flatness) * floorMask;
        return math.lerp(sdfDist, organicPlane, passableBlend);
    }

    private const float Tau = 6.2831853f;

    /// <summary>
    /// AUP wrap period in meters for the procedural cave field. MUST stay byte-identical in meaning to
    /// <c>ProceduralCaveSdfCarveJob.WrapPeriodMeters</c>, because any divergence between the two carves
    /// different rock wherever the two representations meet.
    ///
    /// CORRECTION (supersedes the earlier R99 note that said the canonical job "currently has no
    /// scheduler"): BOTH carvers are live. This inline copy is the main world cave field, and
    /// <c>HectonAnomalyEngine.ScheduleTerrainSdfSnap</c> schedules <c>ProceduralCaveSdfCarveJob</c> for
    /// anomaly SDF volumes. R99 unified the NOISE FIELD between them; the SURFACE-PROTECTION PROFILE is
    /// still duplicated and still disagrees, and both feed on the same quantity (density == geometric
    /// depth below the heightmap in meters, per <c>VoxelSeamDirector.ComputeTerrainDensity</c> and
    /// <c>SnapSDFToTerrainJob</c>):
    ///   - this path        : smoothstep*exponential ramp over [0, SurfaceProtectionMeters]; full carve AT it.
    ///   - the anomaly job  : hard zero below SurfaceProtectionMeters, smoothstep over [P, P + 15]; full carve after.
    /// Both satisfy the voxels.md L85 safety intent (no carve reaches the terrain surface), so neither is
    /// unsafe, but they are NOT interchangeable: with the anomaly job's P = 50 m the two disagree over a
    /// ~65 m band. Unifying them changes authored anomaly geology, so it needs a visual/level review
    /// rather than a blind edit - do not "fix" one to match the other without that.
    /// </summary>
    private const double CaveWrapPeriodMeters = 6627.0;

    /// <summary>R99: quantizes a frequency (cycles per meter) to a whole number of cycles per wrap period.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int QuantizeCellsPerPeriod(float frequency)
    {
        return math.max(1, (int)math.round(frequency * (float)CaveWrapPeriodMeters));
    }

    /// <summary>R99: converts whole cycles-per-period back to a frequency in cycles per meter.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float CellsToFrequency(int cells)
    {
        return (float)(cells / CaveWrapPeriodMeters);
    }

    /// <summary>
    /// Evaluates the LIVE combined Gyroid + Cellular 3D cave SDF at wrapped AUP point p.
    /// R99 UNIFICATION: this was a silently diverged copy of <c>ProceduralCaveSdfCarveJob</c>. Three
    /// defect classes are fixed here:
    /// (1) NON-PERIODIC FIELD — snoise/cellular/ridged terms were evaluated on a floor-fmod sawtooth
    ///     domain, so the field tore on every X/Y/Z = k*6627 m plane, including the Y = 0 sea-level
    ///     plane. Every term below is now exactly 6627 m-periodic (frequency-quantized waves plus
    ///     wrapped integer lattices), hence continuous across every wrap boundary.
    /// (2) FOLDED STRATA DOMAIN — y' = y + A*sin(w*y) is monotonic only while A*w &lt; 1. The old cap
    ///     A &lt;= 0.45*thickness allowed A*w up to 2.83, producing mirrored duplicate cave bands
    ///     (the banned kaleidoscope artifact class). Now clamped to 0.14 * quantized wavelength.
    /// (3) CONSTANT DRIFT — rarity/gyroidBand/chamberRadius had drifted from the canonical job,
    ///     offsetting cave walls by roughly 1.3 m at PrimaryFrequency ~= 0.01. Now constant-matched.
    /// The depositional-floor, wall-grit and anisotropic-chamber terms are unique to this live path
    /// (they carry the geology the bible requires) and are preserved — rebuilt on periodic primitives.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private float EvaluateGyroidCellularCaveSdf(float3 p, float3 seedOffset)
    {
        float primaryFrequency = math.max(math.abs(PrimaryFrequency), 0.0005f);
        float secondaryFrequency = math.max(math.abs(SecondaryFrequency), 0.0005f);
        float safeCarveStrength = math.max(CarveStrengthMeters, math.max(voxelStep, 1.0f));

        int warpCells = QuantizeCellsPerPeriod(math.max(primaryFrequency * 0.47f, 0.0005f));
        float warpFrequency = CellsToFrequency(warpCells);
        float warpAmplitude = math.clamp(safeCarveStrength * 0.35f, 2.0f, 22.0f);
        float3 warpedPos = ApplyGyroidDomainWarp(p, seedOffset, warpFrequency, warpCells, warpAmplitude, WorldSeed ^ 0x5A17D2E9u);

        float strataThickness = math.max(4.0f, StrataLayerThicknessMeters);
        int strataCycles = math.max(1, (int)math.round(CaveWrapPeriodMeters / strataThickness));
        float strataFrequency = (float)(strataCycles * (Tau / CaveWrapPeriodMeters));
        float strataWavelength = (float)(CaveWrapPeriodMeters / strataCycles);
        float strataAmplitude = math.clamp(StrataShelvingStrength * strataThickness, 0.0f, strataWavelength * 0.14f);
        warpedPos.y += math.sin((warpedPos.y + seedOffset.y) * strataFrequency) * strataAmplitude;

        float rarity = math.saturate(CaveThreshold);

        int gyroidCycles = QuantizeCellsPerPeriod(primaryFrequency);
        float gyroidFrequency = CellsToFrequency(gyroidCycles);
        float3 gyroidPos = (warpedPos + seedOffset * 0.37f) * gyroidFrequency * Tau;
        float sinX = math.sin(gyroidPos.x);
        float sinY = math.sin(gyroidPos.y);
        float sinZ = math.sin(gyroidPos.z);
        float cosX = math.cos(gyroidPos.x);
        float cosY = math.cos(gyroidPos.y);
        float cosZ = math.cos(gyroidPos.z);
        float gyroid = sinX * cosY + sinY * cosZ + sinZ * cosX;
        float gyroidBand = math.lerp(0.62f, 0.26f, rarity);
        float gyroidMetricScale = math.max(1.0f / (gyroidFrequency * Tau), voxelStep);
        float gyroidSdf = (math.abs(gyroid) - gyroidBand) * gyroidMetricScale;

        // Vertical chamber elongation is now an ANISOTROPIC wrapped lattice (fewer cells per period in
        // Y) instead of scaling the Y coordinate by 0.58 — coordinate scaling destroyed Y periodicity
        // and was one of the two sources of the sea-level seam.
        float chamberFrequencyXZ = math.max(secondaryFrequency * 0.55f, 0.0005f);
        int chamberCellsXZ = QuantizeCellsPerPeriod(chamberFrequencyXZ);
        int chamberCellsY = math.max(1, (int)math.round(chamberCellsXZ * 0.58f));
        int3 chamberCells = new int3(chamberCellsXZ, chamberCellsY, chamberCellsXZ);
        float3 chamberFrequency = new float3(
            CellsToFrequency(chamberCellsXZ),
            CellsToFrequency(chamberCellsY),
            CellsToFrequency(chamberCellsXZ));
        float chamberDistance = CellularDistance(warpedPos + seedOffset * 1.91f, chamberFrequency, chamberCells, WorldSeed ^ 0xC0A55123u);
        int chamberNoiseCells = QuantizeCellsPerPeriod(chamberFrequencyXZ * 1.83f);
        float chamberNoise = PeriodicGradientNoise((warpedPos + seedOffset * 2.73f) * CellsToFrequency(chamberNoiseCells), chamberNoiseCells, WorldSeed ^ 0x7B2E44D1u);
        float chamberRadius = math.lerp(0.42f, 0.20f, rarity) + chamberNoise * 0.055f;
        chamberRadius = math.clamp(chamberRadius, 0.14f, 0.48f);
        // Cell-space distance -> meters. Dividing by the LARGEST axis frequency keeps the result a
        // conservative (Lipschitz <= 1) distance estimate under the anisotropic lattice.
        float chamberSdf = (chamberDistance - chamberRadius) / math.cmax(chamberFrequency);

        float gyroidYD = cosY * cosZ - sinX * sinY;
        float strataSlope = math.cos((warpedPos.y + seedOffset.y) * strataFrequency);
        float floorProxy = math.saturate(0.5f + (gyroidYD * 0.34f + strataSlope * 0.18f));
        floorProxy = floorProxy * floorProxy * (3f - floorProxy * 2f);
        float nearSurfaceMask = 1f - math.saturate(math.min(math.abs(gyroidSdf), math.abs(chamberSdf)) / math.max(safeCarveStrength * 0.45f, voxelStep));

        // Sediment ripple field: two plane waves whose spatial frequencies are whole cycles per wrap
        // period. The second wave is an INTEGER harmonic of the first (2x, was 1.73x), so it stays
        // periodic instead of tearing at the wrap plane.
        float duneFrequencyX = CellsToFrequency(QuantizeCellsPerPeriod(0.055f / Tau)) * Tau;
        float duneFrequencyZ = CellsToFrequency(QuantizeCellsPerPeriod(0.041f / Tau)) * Tau;
        float dunePhase = (warpedPos.x + seedOffset.x * 3.1f) * duneFrequencyX +
                          (warpedPos.z - seedOffset.z * 2.7f) * duneFrequencyZ;
        float dune = (math.sin(dunePhase) * 0.62f + math.sin(dunePhase * 2.0f + 1.91f) * 0.38f) * safeCarveStrength * 0.035f;
        float rubbleNoise = PeriodicRidgedMultifractal(warpedPos + seedOffset * 1.733f, QuantizeCellsPerPeriod(0.075f), WorldSeed ^ 0x71C9D3A5u, 3);
        float depositionalNoise = (dune + (rubbleNoise - 0.55f) * safeCarveStrength * 0.045f) * floorProxy * nearSurfaceMask;

        float wallCeilingMask = (1f - floorProxy) * nearSurfaceMask;
        float wallGrit = (PeriodicRidgedMultifractal(warpedPos + seedOffset * 2.158f, QuantizeCellsPerPeriod(0.19f), WorldSeed ^ 0xBADC0DEu, 3) - 0.5f) *
            math.min(safeCarveStrength * 0.065f, math.max(voxelStep * 0.65f, 0.35f)) * wallCeilingMask;
        int reefCells = QuantizeCellsPerPeriod(primaryFrequency * 2.67f);
        float reefNoise = PeriodicGradientNoise((warpedPos + seedOffset * 4.11f) * CellsToFrequency(reefCells), reefCells, WorldSeed ^ 0x19C3A57Fu) *
            safeCarveStrength * 0.045f * wallCeilingMask;

        return math.min(gyroidSdf, chamberSdf) + depositionalNoise + wallGrit + reefNoise;
    }

    /// <summary>R99: exactly wrap-periodic 3D domain warp (replaces non-periodic snoise warp).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float3 ApplyGyroidDomainWarp(float3 p, float3 seedOffset, float frequency, int cellsPerPeriod, float amplitude, uint seed)
    {
        float3 q = (p + seedOffset) * frequency;
        float wx = PeriodicGradientNoise(q, cellsPerPeriod, seed ^ 0x11A3F2C5u);
        float wy = PeriodicGradientNoise(q, cellsPerPeriod, seed ^ 0x8D9B41E7u);
        float wz = PeriodicGradientNoise(q, cellsPerPeriod, seed ^ 0x3F60AB19u);
        return p + new float3(wx, wy, wz) * amplitude;
    }

    /// <summary>
    /// R99: wrap-periodic ridged multifractal. Lacunarity is exactly 2 and the lattice cell count
    /// doubles with it, so every octave keeps the same wrap period as the base field.
    /// </summary>
    private static float PeriodicRidgedMultifractal(float3 p, int cellsPerPeriod, uint seed, int octaves)
    {
        float amplitude = 0.5f;
        float sum = 0f;
        float norm = 0f;
        int cells = math.max(1, cellsPerPeriod);
        for (int i = 0; i < 4; i++)
        {
            if (i >= octaves)
                break;

            float n = PeriodicGradientNoise(p * CellsToFrequency(cells), cells, seed + (uint)i * 0x9E3779B9u);
            float ridge = 1f - math.abs(n);
            sum += ridge * ridge * amplitude;
            norm += amplitude;
            cells *= 2;
            amplitude *= 0.52f;
        }

        return sum / math.max(norm, 0.0001f);
    }

    /// <summary>
    /// R99: C2-smooth (quintic-fade) trilinear gradient-lattice noise whose integer lattice wraps every
    /// cellsPerPeriod cells. Output approximately in [-1, 1]. Deterministic, Burst-safe, exactly periodic.
    /// </summary>
    private static float PeriodicGradientNoise(float3 q, int cellsPerPeriod, uint seed)
    {
        float3 cellF = math.floor(q);
        int3 cell = new int3((int)cellF.x, (int)cellF.y, (int)cellF.z);
        float3 f = q - cellF;
        float3 u = f * f * f * (f * (f * 6.0f - 15.0f) + 10.0f);

        int3 period = new int3(cellsPerPeriod, cellsPerPeriod, cellsPerPeriod);
        float n000 = CornerGradientDot(cell, new int3(0, 0, 0), f, period, seed);
        float n100 = CornerGradientDot(cell, new int3(1, 0, 0), f, period, seed);
        float n010 = CornerGradientDot(cell, new int3(0, 1, 0), f, period, seed);
        float n110 = CornerGradientDot(cell, new int3(1, 1, 0), f, period, seed);
        float n001 = CornerGradientDot(cell, new int3(0, 0, 1), f, period, seed);
        float n101 = CornerGradientDot(cell, new int3(1, 0, 1), f, period, seed);
        float n011 = CornerGradientDot(cell, new int3(0, 1, 1), f, period, seed);
        float n111 = CornerGradientDot(cell, new int3(1, 1, 1), f, period, seed);

        float nx00 = math.lerp(n000, n100, u.x);
        float nx10 = math.lerp(n010, n110, u.x);
        float nx01 = math.lerp(n001, n101, u.x);
        float nx11 = math.lerp(n011, n111, u.x);
        float nxy0 = math.lerp(nx00, nx10, u.y);
        float nxy1 = math.lerp(nx01, nx11, u.y);
        // Edge-direction gradients have |g| = sqrt(2); 1.154 rescales the interpolated result to ~[-1, 1].
        return math.lerp(nxy0, nxy1, u.z) * 1.154f;
    }

    /// <summary>R99: dot of the wrapped-lattice corner gradient with the offset from that corner.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float CornerGradientDot(int3 cell, int3 corner, float3 f, int3 cellsPerPeriod, uint seed)
    {
        int3 wrapped = WrapCell3(cell + corner, cellsPerPeriod);
        uint h = Hash(wrapped.x, wrapped.y, wrapped.z, seed);
        float3 g = new float3(
            (h & 1u) != 0u ? -1.0f : 1.0f,
            (h & 2u) != 0u ? -1.0f : 1.0f,
            (h & 4u) != 0u ? -1.0f : 1.0f);
        uint axis = (h >> 3) % 3u;
        g = math.select(g, new float3(0.0f, g.y, g.z), axis == 0u);
        g = math.select(g, new float3(g.x, 0.0f, g.z), axis == 1u);
        g = math.select(g, new float3(g.x, g.y, 0.0f), axis == 2u);
        float3 d = f - new float3(corner.x, corner.y, corner.z);
        return math.dot(g, d);
    }

    /// <summary>R99: wraps integer lattice coordinates into [0, period) per axis (true modulo, negative-safe).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int3 WrapCell3(int3 cell, int3 cellsPerPeriod)
    {
        int3 period = math.max(new int3(1, 1, 1), cellsPerPeriod);
        int3 m = cell % period;
        return math.select(m, m + period, m < 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float RidgedMultifractal(float3 p, uint seed, int octaves)
    {
        float seedOffset = (seed & 0xFFFFu) * 0.000173f;
        float3 q = p + new float3(seedOffset, seedOffset * 1.37f, -seedOffset * 0.73f);
        float amplitude = 0.5f;
        float sum = 0f;
        float norm = 0f;
        for (int i = 0; i < 4; i++)
        {
            if (i >= octaves)
                break;

            float ridge = 1f - math.abs(noise.snoise(q));
            sum += ridge * ridge * amplitude;
            norm += amplitude;
            q = q * 2.07f + new float3(17.13f, -9.71f, 5.43f);
            amplitude *= 0.52f;
        }

        return sum / math.max(norm, 0.0001f);
    }

    /// <summary>
    /// R99: 3D cellular/Worley distance on a lattice that wraps every cellsPerPeriod cells per axis,
    /// making the chamber field exactly periodic over the AUP wrap period. Per-axis frequency allows
    /// anisotropic (vertically elongated) chambers without breaking periodicity.
    /// Returned distance is in CELL space; callers convert with the largest axis frequency.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float CellularDistance(float3 p, float3 frequency, int3 cellsPerPeriod, uint seed)
    {
        float3 cellPos = p * frequency;
        int3 baseCell = new int3(
            (int)math.floor(cellPos.x),
            (int)math.floor(cellPos.y),
            (int)math.floor(cellPos.z));
        float3 frac = cellPos - new float3(baseCell.x, baseCell.y, baseCell.z);

        float nearestSq = 99999.0f;
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int3 neighbor = WrapCell3(baseCell + new int3(dx, dy, dz), cellsPerPeriod);
                    float3 feature = Hash3ToUnitFloat3(neighbor, seed);
                    float3 diff = new float3(dx, dy, dz) + feature - frac;
                    nearestSq = math.min(nearestSq, math.lengthsq(diff));
                }
            }
        }

        return math.sqrt(nearestSq);
    }

    /// <summary>
    /// R99: full 3D cell hash. The previous chain hashed each output channel from a single input axis
    /// (hx from cell.x only), so every chamber sharing a lattice column got the same feature-point X —
    /// an axis-aligned regularity visible as gridded chamber placement. Each channel now mixes all
    /// three coordinates.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float3 Hash3ToUnitFloat3(int3 cell, uint seed)
    {
        uint hx = Hash(cell.x, cell.y, cell.z, seed ^ 0x9E3779B9u);
        uint hy = Hash(cell.x, cell.y, cell.z, seed ^ 0xBB67AE85u);
        uint hz = Hash(cell.x, cell.y, cell.z, seed ^ 0x3C6EF372u);
        return new float3(
            HashToUnitFloat(hx),
            HashToUnitFloat(hy),
            HashToUnitFloat(hz));
    }

    /// <summary>R99: integer hash over 3D lattice coordinates and seed (matches the canonical carve job).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static uint Hash(int x, int y, int z, uint seed)
    {
        unchecked
        {
            uint h = seed;
            h ^= (uint)x * 0x8DA6B343u;
            h ^= (uint)y * 0xD8163841u;
            h ^= (uint)z * 0xCB1AB31Fu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float HashToUnitFloat(uint hash)
    {
        return (hash & 0x00FFFFFFu) * (1.0f / 16777216.0f);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double Fmod(double value, double period)
    {
        return value - math.floor(value / period) * period;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float3 ResolveSeedOffset(uint seed)
    {
        float seedOffX = ((seed & 0xFFu) - 128f) * 0.5f;
        float seedOffY = (((seed >> 8) & 0xFFu) - 128f) * 0.5f;
        float seedOffZ = (((seed >> 16) & 0xFFu) - 128f) * 0.5f;
        return new float3(seedOffX, seedOffY, seedOffZ);
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
struct VoxelColliderChunkClassifyJob : IJobParallelFor
{
    private const float MinSafeBoundsExtent = 0.01f;
    private const float MaxSafeBoundsExtent = 1048576f;

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<int> triangleIndices;
    public float3 boundsMin;
    public float3 boundsSize;
    public int chunkCount;
    [WriteOnly, NoAlias] public NativeArray<byte> triangleBuckets;

    public void Execute(int triangleIndex)
    {
        if (!triangleBuckets.IsCreated || triangleIndex < 0 || triangleIndex >= triangleBuckets.Length)
            return;

        triangleBuckets[triangleIndex] = 0;
        if (!positions.IsCreated || !triangleIndices.IsCreated || chunkCount <= 0 || !IsFinite(boundsMin) || !IsFinite(boundsSize))
            return;

        if (triangleIndices.Length < 3 || triangleIndex > (triangleIndices.Length - 3) / 3)
            return;

        int triBase = triangleIndex * 3;
        int i0 = triangleIndices[triBase];
        int i1 = triangleIndices[triBase + 1];
        int i2 = triangleIndices[triBase + 2];
        if ((uint)i0 >= (uint)positions.Length || (uint)i1 >= (uint)positions.Length || (uint)i2 >= (uint)positions.Length)
            return;

        float3 p0 = positions[i0];
        float3 p1 = positions[i1];
        float3 p2 = positions[i2];
        if (!IsFinite(p0) || !IsFinite(p1) || !IsFinite(p2))
            return;

        float3 centroid = (p0 + p1 + p2) * (1f / 3f);
        if (!IsFinite(centroid))
            return;

        triangleBuckets[triangleIndex] = (byte)ResolveChunkIndex(centroid);
    }

    int ResolveChunkIndex(float3 point)
    {
        if (!IsFinite(point) || chunkCount <= 0)
            return 0;

        float3 safeBoundsMin = SanitizeBoundsMin(boundsMin);
        float3 safeSize = SanitizeBoundsSize(boundsSize);
        float3 normalized = SaturateFinite((point - safeBoundsMin) / safeSize);
        int x = normalized.x >= 0.5f ? 1 : 0;
        int z = normalized.z >= 0.5f ? 1 : 0;
        int resolvedIndex;

        if (chunkCount <= 4)
        {
            resolvedIndex = x | (z << 1);
            return math.clamp(resolvedIndex, 0, math.max(chunkCount - 1, 0));
        }

        int y = normalized.y >= 0.5f ? 1 : 0;
        resolvedIndex = x | (z << 1) | (y << 2);
        return math.clamp(resolvedIndex, 0, math.max(chunkCount - 1, 0));
    }

    static float3 SanitizeBoundsMin(float3 value)
    {
        return IsFinite(value)
            ? math.clamp(value, new float3(-MaxSafeBoundsExtent), new float3(MaxSafeBoundsExtent))
            : float3.zero;
    }

    static float3 SanitizeBoundsSize(float3 value)
    {
        return IsFinite(value)
            ? math.clamp(math.abs(value), new float3(MinSafeBoundsExtent), new float3(MaxSafeBoundsExtent))
            : new float3(MinSafeBoundsExtent);
    }

    static float3 SaturateFinite(float3 value)
    {
        return IsFinite(value) ? math.saturate(value) : float3.zero;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelFillIntArrayJob : IJobParallelFor
{
    public int Value;
    [NoAlias] public NativeArray<int> Values;

    public void Execute(int index)
    {
        if (!Values.IsCreated || index < 0 || index >= Values.Length)
            return;

        Values[index] = Value;
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelFillFloatArrayJob : IJobParallelFor
{
    public float Value;
    [NoAlias] public NativeArray<float> Values;

    public void Execute(int index)
    {
        if (!Values.IsCreated || index < 0 || index >= Values.Length)
            return;

        Values[index] = math.select(0f, Value, math.isfinite(Value));
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelChunkSkirtExtrusionJob : IJobParallelFor
{
    private const float MaxSafeSkirtMeters = 1048576f;

    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public float skirtDepthMeters;
    public float skirtWidthMeters;
    public int lodLevel;

    [NoAlias] public NativeArray<float3> positions;
    [NoAlias] public NativeArray<float> skirtAlphaValues;

    public void Execute(int idx)
    {
        if (!positions.IsCreated || idx < 0 || idx >= positions.Length || ptsX <= 1 || ptsZ <= 1 ||
            !math.isfinite(voxelStep) || voxelStep <= 0.0001f || !IsFinite(volumeOrigin))
            return;

        float3 position = positions[idx];
        if (!IsFinite(position))
            return;

        float volumeSizeX = ClampFinite((ptsX - 1) * voxelStep, 0f, 0f, MaxSafeSkirtMeters);
        float volumeSizeZ = ClampFinite((ptsZ - 1) * voxelStep, 0f, 0f, MaxSafeSkirtMeters);
        if (!math.isfinite(volumeSizeX) || !math.isfinite(volumeSizeZ) || volumeSizeX <= 0f || volumeSizeZ <= 0f)
            return;

        float localX = position.x - volumeOrigin.x;
        float localZ = position.z - volumeOrigin.z;
        float edgeDist = math.min(localX, math.min(volumeSizeX - localX, math.min(localZ, volumeSizeZ - localZ)));
        if (!math.isfinite(edgeDist))
            return;

        float safeSkirtWidth = ClampFinite(skirtWidthMeters, voxelStep, voxelStep, MaxSafeSkirtMeters);
        float skirtMask = SaturateFinite(1f - math.smoothstep(0f, safeSkirtWidth, math.max(edgeDist, 0f)));
        if (skirtMask <= 0.0001f)
            return;

        float lodScale = lodLevel > 0 ? 1f : 0.65f;
        float safeSkirtDepth = ClampFinite(skirtDepthMeters, 0f, 0f, MaxSafeSkirtMeters);
        float snappedY = position.y - skirtMask * safeSkirtDepth * lodScale;
        if (!math.isfinite(snappedY))
            return;

        position.y = snappedY;
        positions[idx] = position;

        if (skirtAlphaValues.IsCreated && idx < skirtAlphaValues.Length)
            skirtAlphaValues[idx] = math.max(SaturateFinite(skirtAlphaValues[idx]), skirtMask);
    }

    static float ClampFinite(float value, float fallback, float minimum, float maximum)
    {
        float safe = math.select(fallback, value, math.isfinite(value));
        return math.clamp(safe, minimum, maximum);
    }

    static float SaturateFinite(float value)
    {
        return math.select(0f, math.saturate(value), math.isfinite(value));
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelChunkBoundsContentJob : IJob
{
    public int ptsX, ptsY, ptsZ;
    [ReadOnly, NoAlias] public NativeArray<float> density;
    [NoAlias] public NativeArray<int> hasContent;

    public void Execute()
    {
        if (!hasContent.IsCreated || hasContent.Length <= 0)
            return;

        hasContent[0] = 0;
        if (!density.IsCreated || ptsX <= 0 || ptsY <= 0 || ptsZ <= 0 || !HasCompleteDensityField())
            return;

        int total = ptsX * ptsY * ptsZ;
        for (int i = 0; i < total; i++)
        {
            float value = density[i];
            if (math.isfinite(value) && value >= 0f)
            {
                hasContent[0] = 1;
                return;
            }
        }
    }

    float ReadDensity(int x, int y, int z)
    {
        float value = density[x + y * ptsX + z * ptsX * ptsY];
        return math.select(0f, value, math.isfinite(value));
    }

    bool HasCompleteDensityField()
    {
        long expectedLength = (long)ptsX * ptsY * ptsZ;
        return expectedLength > 0L && expectedLength <= density.Length;
    }
}



//  JOB 2: Marching Cubes exact count pass
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelMCCountJob : IJobParallelFor
{
    private const int MarchingCubesCubeCount = 256;
    private const int MarchingCubesTableStride = 16;
    private const float MaxSafeDensityDecodeScale = 1048576f;

    public int cellsX, cellsY, cellsZ;
    public int ptsX, ptsY, ptsZ;
    public float densityDecodeScale;

    [ReadOnly, NoAlias] public NativeArray<sbyte> density;
    [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly edgeTable;
    [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly triTable;
    [WriteOnly, NoAlias] public NativeArray<int> cellVertexCounts;
    [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> densityFaultFlags;

    public void Execute(int cellIdx)
    {
        if (!cellVertexCounts.IsCreated || cellIdx < 0 || cellIdx >= cellVertexCounts.Length)
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.MarchingCubesCountInput);
            return;
        }

        cellVertexCounts[cellIdx] = 0;
        if (!HasSafeMarchingCubesInputs(cellIdx))
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.MarchingCubesCountInput);
            return;
        }

        int cx = cellIdx % cellsX;
        int cy = (cellIdx / cellsX) % cellsY;
        int cz = cellIdx / (cellsX * cellsY);

        float d0 = D(cx, cy, cz);
        float d1 = D(cx + 1, cy, cz);
        float d2 = D(cx + 1, cy + 1, cz);
        float d3 = D(cx, cy + 1, cz);
        float d4 = D(cx, cy, cz + 1);
        float d5 = D(cx + 1, cy, cz + 1);
        float d6 = D(cx + 1, cy + 1, cz + 1);
        float d7 = D(cx, cy + 1, cz + 1);

        CubeDensities densities = new CubeDensities(d0: d0, d1: d1, d2: d2, d3: d3, d4: d4, d5: d5, d6: d6, d7: d7);
        int cubeIndex = ResolveCubeIndex(in densities);

        int triBase = cubeIndex * 16;
        int triCount =
            math.select(0, 1, triTable[triBase] != -1) +
            math.select(0, 1, triTable[triBase + 3] != -1) +
            math.select(0, 1, triTable[triBase + 6] != -1) +
            math.select(0, 1, triTable[triBase + 9] != -1) +
            math.select(0, 1, triTable[triBase + 12] != -1);

        cellVertexCounts[cellIdx] = triCount * 3;
    }

    int GI(int ix, int iy, int iz) => ix + iy * ptsX + iz * ptsX * ptsY;
    float D(int ix, int iy, int iz) => density[GI(ix, iy, iz)] * densityDecodeScale;

    bool HasSafeMarchingCubesInputs(int cellIdx)
    {
        long totalCells = (long)cellsX * cellsY * cellsZ;
        long densityLength = (long)ptsX * ptsY * ptsZ;
        return density.IsCreated &&
            triTable.Length >= MarchingCubesCubeCount * MarchingCubesTableStride &&
            cellsX > 0 && cellsY > 0 && cellsZ > 0 &&
            ptsX > cellsX && ptsY > cellsY && ptsZ > cellsZ &&
            math.isfinite(densityDecodeScale) && densityDecodeScale > 0f && densityDecodeScale <= MaxSafeDensityDecodeScale &&
            totalCells > 0L && cellIdx < totalCells &&
            densityLength > 0L && densityLength <= density.Length;
    }

    void MarkDensityFault(int slot)
    {
        if (densityFaultFlags.IsCreated && (uint)slot < (uint)densityFaultFlags.Length)
            densityFaultFlags[slot] = 1;
    }




    static int ResolveCubeIndex(in CubeDensities densities)
    {
        return
            math.select(0, 1, densities.d0 < 0f) |
            math.select(0, 2, densities.d1 < 0f) |
            math.select(0, 4, densities.d2 < 0f) |
            math.select(0, 8, densities.d3 < 0f) |
            math.select(0, 16, densities.d4 < 0f) |
            math.select(0, 32, densities.d5 < 0f) |
            math.select(0, 64, densities.d6 < 0f) |
            math.select(0, 128, densities.d7 < 0f);



    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDensityQuantizeJob : IJobParallelFor
{
    private const float MaxSafeDensityEncodeInvScale = 1048576f;

    public float densityDecodeInvScale;

    [ReadOnly, NoAlias] public NativeArray<float> density;
    [WriteOnly, NoAlias] public NativeArray<sbyte> quantizedDensity;
    [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> densityFaultFlags;

    public void Execute(int index)
    {
        if (!quantizedDensity.IsCreated || index < 0 || index >= quantizedDensity.Length)
            return;

        quantizedDensity[index] = 0;
        if (!density.IsCreated || index >= density.Length || !math.isfinite(densityDecodeInvScale) ||
            densityDecodeInvScale <= 0f || densityDecodeInvScale > MaxSafeDensityEncodeInvScale)
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.QuantizeInput);
            return;
        }

        float source = density[index];
        if (!math.isfinite(source))
            MarkDensityFault(VoxelDensityPipelineFaultSlots.QuantizeInput);
        source = math.select(0f, source, math.isfinite(source));
        float scaled = math.clamp(source * densityDecodeInvScale, -127f, 127f);
        if (!math.isfinite(scaled))
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.QuantizeInput);
            scaled = 0f;
        }

        int quantized = math.select((int)(scaled - 0.5f), (int)(scaled + 0.5f), scaled >= 0f);
        int minimumSignedStep = math.select(-1, 1, source >= 0f);
        quantized = math.select(quantized, minimumSignedStep, quantized == 0 && math.abs(source) > 0.00001f);

        quantizedDensity[index] = (sbyte)quantized;
    }

    void MarkDensityFault(int slot)
    {
        if (densityFaultFlags.IsCreated && (uint)slot < (uint)densityFaultFlags.Length)
            densityFaultFlags[slot] = 1;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 2.1: Marching Cubes extraction (exact-offset write)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct VoxelMCExtractJob : IJobParallelFor
{
    private const int MarchingCubesCubeCount = 256;
    private const int MarchingCubesTableStride = 16;
    private const int MarchingCubesMaxVertexCount = 15;
    private const float MaxSafeDensityDecodeScale = 1048576f;

    public int cellsX, cellsY, cellsZ;
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public float densityDecodeScale;

    [ReadOnly, NoAlias] public NativeArray<sbyte> density;
    [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly edgeTable;
    [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly triTable;
    [ReadOnly, NoAlias] public NativeArray<int> cellVertexOffsets;
    [ReadOnly, NoAlias] public NativeArray<int> cellVertexCounts;

    // SAFETY_JUSTIFICATION_PARAGRAPH_1:
    // Unity's safety system cannot prove that each parallel cell writes a disjoint slice of outVertices.
    // The slice is derived from cellVertexOffsets[cellIdx] and cellVertexCounts[cellIdx], both produced
    // by the preceding count pass before this job is scheduled.
    // SAFETY_JUSTIFICATION_PARAGRAPH_2:
    // Per-thread NativeStreams and post-merge buffers were rejected because they add allocator pressure and
    // a second compaction stage to the streaming path. A single-thread extractor was rejected because it
    // serializes the dominant marching-cubes emission pass.
    // SAFETY_JUSTIFICATION_PARAGRAPH_3:
    // The invariant is exclusive range ownership: Execute(cellIdx) writes only
    // [cellVertexOffsets[cellIdx], cellVertexOffsets[cellIdx] + cellVertexCounts[cellIdx]).
    // No other job writes outVertices until this job handle completes.
    [NativeDisableContainerSafetyRestriction, NoAlias]
    public NativeArray<MCRawVertex> outVertices;
    [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> densityFaultFlags;

    public void Execute(int cellIdx)
    {
        if (cellIdx < 0 || !HasSafeMarchingCubesInputs(cellIdx))
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.MarchingCubesExtractInput);
            return;
        }

        int cx = cellIdx % cellsX;
        int cy = (cellIdx / cellsX) % cellsY;
        int cz = cellIdx / (cellsX * cellsY);

        CubeDensities densities = new CubeDensities
        {
            d0 = D(cx, cy, cz),
            d1 = D(cx + 1, cy, cz),
            d2 = D(cx + 1, cy + 1, cz),
            d3 = D(cx, cy + 1, cz),
            d4 = D(cx, cy, cz + 1),
            d5 = D(cx + 1, cy, cz + 1),
            d6 = D(cx + 1, cy + 1, cz + 1),
            d7 = D(cx, cy + 1, cz + 1)
        };

        int cubeIndex = ResolveCubeIndex(in densities);


        // Burst-legal lookup: the managed MarchingCubesLookupTable statics are the init source
        // only (line ~244 fills this native copy from them). Managed static arrays are not
        // readable from Burst-compiled code. Length >= 256 is guaranteed by
        // HasSafeMarchingCubesInputs; cubeIndex is 0..255 from ResolveCubeIndex.
        int edgeBits = edgeTable[cubeIndex];
        if (edgeBits == 0) return;

        int vertCount = cellVertexCounts[cellIdx];
        if (vertCount <= 0 || vertCount > MarchingCubesMaxVertexCount || vertCount % 3 != 0)
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.MarchingCubesExtractInput);
            return;
        }

        float3 p0 = P(cx, cy, cz);
        float3 p1 = P(cx+1, cy, cz);
        float3 p2 = P(cx+1, cy+1, cz);
        float3 p3 = P(cx, cy+1, cz);
        float3 p4 = P(cx, cy, cz+1);
        float3 p5 = P(cx+1, cy, cz+1);
        float3 p6 = P(cx+1, cy+1, cz+1);
        float3 p7 = P(cx, cy+1, cz+1);
        if (!IsFinite(p0) || !IsFinite(p1) || !IsFinite(p2) || !IsFinite(p3) ||
            !IsFinite(p4) || !IsFinite(p5) || !IsFinite(p6) || !IsFinite(p7))
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.MarchingCubesExtractInput);
            return;
        }

        int g0=GI(cx,cy,cz); int g1=GI(cx+1,cy,cz);
        int g2=GI(cx+1,cy+1,cz); int g3=GI(cx,cy+1,cz);
        int g4=GI(cx,cy,cz+1); int g5=GI(cx+1,cy,cz+1);
        int g6=GI(cx+1,cy+1,cz+1); int g7=GI(cx,cy+1,cz+1);

        float3 ev0=float3.zero,ev1=float3.zero,ev2=float3.zero,ev3=float3.zero;
        float3 ev4=float3.zero,ev5=float3.zero,ev6=float3.zero,ev7=float3.zero;
        float3 ev8=float3.zero,ev9=float3.zero,ev10=float3.zero,ev11=float3.zero;
        long eid0=0,eid1=0,eid2=0,eid3=0;
        long eid4=0,eid5=0,eid6=0,eid7=0;
        long eid8=0,eid9=0,eid10=0,eid11=0;

        ev0 = Lerp(p0, p1, densities.d0, densities.d1); eid0 = PackEdge(g0, g1);
        ev1 = Lerp(p1, p2, densities.d1, densities.d2); eid1 = PackEdge(g1, g2);
        ev2 = Lerp(p2, p3, densities.d2, densities.d3); eid2 = PackEdge(g2, g3);
        ev3 = Lerp(p3, p0, densities.d3, densities.d0); eid3 = PackEdge(g3, g0);
        ev4 = Lerp(p4, p5, densities.d4, densities.d5); eid4 = PackEdge(g4, g5);
        ev5 = Lerp(p5, p6, densities.d5, densities.d6); eid5 = PackEdge(g5, g6);
        ev6 = Lerp(p6, p7, densities.d6, densities.d7); eid6 = PackEdge(g6, g7);
        ev7 = Lerp(p7, p4, densities.d7, densities.d4); eid7 = PackEdge(g7, g4);
        ev8 = Lerp(p0, p4, densities.d0, densities.d4); eid8 = PackEdge(g0, g4);
        ev9 = Lerp(p1, p5, densities.d1, densities.d5); eid9 = PackEdge(g1, g5);
        ev10 = Lerp(p2, p6, densities.d2, densities.d6); eid10 = PackEdge(g2, g6);
        ev11 = Lerp(p3, p7, densities.d3, densities.d7); eid11 = PackEdge(g3, g7);

        int triBase = cubeIndex * 16;
        int writeOffset = cellVertexOffsets[cellIdx];
        if (writeOffset < 0 || writeOffset > outVertices.Length - vertCount)
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.MarchingCubesExtractOutput);
            return;
        }

        int wi = writeOffset;
        for (int t = 0; t < vertCount; t += 3)
        {
            int e0 = triTable[triBase + t];
            int e1 = triTable[triBase + t + 1];
            int e2 = triTable[triBase + t + 2];

            outVertices[wi] = new MCRawVertex {
                localPosition = GetEV(e0,ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId = GetEID(e0,eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11) };
            outVertices[wi+1] = new MCRawVertex {
                localPosition = GetEV(e1,ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId = GetEID(e1,eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11) };
            outVertices[wi+2] = new MCRawVertex {
                localPosition = GetEV(e2,ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId = GetEID(e2,eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11) };
            wi += 3;
        }
    }

    int GI(int ix,int iy,int iz) => ix+iy*ptsX+iz*ptsX*ptsY;
    float D(int ix,int iy,int iz) => density[GI(ix,iy,iz)] * densityDecodeScale;
    float3 P(int ix,int iy,int iz) => volumeOrigin+new float3(ix,iy,iz)*voxelStep;

    bool HasSafeMarchingCubesInputs(int cellIdx)
    {
        long totalCells = (long)cellsX * cellsY * cellsZ;
        long densityLength = (long)ptsX * ptsY * ptsZ;
        return density.IsCreated &&
            cellVertexOffsets.IsCreated &&
            cellVertexCounts.IsCreated &&
            outVertices.IsCreated &&
            edgeTable.Length >= MarchingCubesCubeCount &&
            triTable.Length >= MarchingCubesCubeCount * MarchingCubesTableStride &&
            cellsX > 0 && cellsY > 0 && cellsZ > 0 &&
            ptsX > cellsX && ptsY > cellsY && ptsZ > cellsZ &&
            IsFinite(volumeOrigin) &&
            math.isfinite(voxelStep) && voxelStep > 0f &&
            math.isfinite(densityDecodeScale) && densityDecodeScale > 0f && densityDecodeScale <= MaxSafeDensityDecodeScale &&
            totalCells > 0L && cellIdx < totalCells &&
            cellIdx < cellVertexOffsets.Length &&
            cellIdx < cellVertexCounts.Length &&
            densityLength > 0L && densityLength <= density.Length;
    }

    static float3 Lerp(float3 pA,float3 pB,float dA,float dB)
    {
        if (!IsFinite(pA) || !IsFinite(pB) || !math.isfinite(dA) || !math.isfinite(dB))
            return float3.zero;

        float diff=dA-dB;
        float absDiff = math.abs(diff);
        float safeSign = math.select(-1f, 1f, diff >= 0f);
        float safeDiff = math.select(safeSign * 1e-6f, diff, absDiff >= 1e-6f);
        // R95: clamp away from exact corners (voxels.md mesh-extraction law). Quantized sbyte
        // density can be exactly 0 at a corner; t == 0/1 then collapses up to three edge vertices
        // onto the identical corner position, and the per-edge welder keeps them as distinct
        // indices -> zero-area triangles. 0.001 keeps vertices strictly inside the edge.
        float t=math.select(0.5f, math.clamp(dA/safeDiff,0.001f,0.999f), absDiff >= 1e-6f);
        t = math.select(0.5f, t, math.isfinite(t));
        float3 result = pA+t*(pB-pA);
        return math.select(float3.zero, result, IsFinite(result));
    }




    static int ResolveCubeIndex(in CubeDensities densities)
    {
        return
            math.select(0, 1, densities.d0 < 0f) |
            math.select(0, 2, densities.d1 < 0f) |
            math.select(0, 4, densities.d2 < 0f) |
            math.select(0, 8, densities.d3 < 0f) |
            math.select(0, 16, densities.d4 < 0f) |
            math.select(0, 32, densities.d5 < 0f) |
            math.select(0, 64, densities.d6 < 0f) |
            math.select(0, 128, densities.d7 < 0f);



    }

    static long PackEdge(int gA,int gB)
    {
        int lo=math.min(gA,gB); int hi=math.max(gA,gB);
        return ((long)hi<<32)|(uint)lo;
    }

    static float3 GetEV(int e,float3 v0,float3 v1,float3 v2,float3 v3,
        float3 v4,float3 v5,float3 v6,float3 v7,
        float3 v8,float3 v9,float3 v10,float3 v11)
    {
        switch(e){
            case 0:return v0;case 1:return v1;case 2:return v2;case 3:return v3;
            case 4:return v4;case 5:return v5;case 6:return v6;case 7:return v7;
            case 8:return v8;case 9:return v9;case 10:return v10;case 11:return v11;
            default:return float3.zero;}
    }

    static long GetEID(int e,long id0,long id1,long id2,long id3,
        long id4,long id5,long id6,long id7,
        long id8,long id9,long id10,long id11)
    {
        switch(e){
            case 0:return id0;case 1:return id1;case 2:return id2;case 3:return id3;
            case 4:return id4;case 5:return id5;case 6:return id6;case 7:return id7;
            case 8:return id8;case 9:return id9;case 10:return id10;case 11:return id11;
            default:return 0;}
    }

    void MarkDensityFault(int slot)
    {
        if (densityFaultFlags.IsCreated && (uint)slot < (uint)densityFaultFlags.Length)
            densityFaultFlags[slot] = 1;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 2.5: Vertex Welding (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct VoxelWeldJob : IJob
{
    private const int InvalidVertexIndex = -1;
    private const int MaxSafeWeldGridPointCount = int.MaxValue;

    public int rawCount;
    public int ptsX;
    public int ptsY;
    public int ptsZ;
    [ReadOnly, NoAlias] public NativeArray<MCRawVertex> rawVertices;
    [NoAlias] public NativeArray<int> edgeVertexX;
    [NoAlias] public NativeArray<int> edgeVertexY;
    [NoAlias] public NativeArray<int> edgeVertexZ;
    [WriteOnly, NoAlias]
    public NativeArray<float3> weldedPositions;
    [WriteOnly, NoAlias]
    public NativeArray<int> triangleIndices;
    [NoAlias] public NativeArray<int> weldedCounter;
    [NoAlias] public NativeArray<int> densityFaultFlags;

    public void Execute()
    {
        if (!weldedCounter.IsCreated || weldedCounter.Length <= 0)
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.WeldInput);
            return;
        }

        weldedCounter[0] = 0;
        if (!HasSafeWeldInputs())
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.WeldInput);
            return;
        }

        int weldedCount = 0;
        for (int i = 0; i < rawCount; i++)
        {
            MCRawVertex rv = rawVertices[i];
            if (!IsFinite(rv.localPosition))
            {
                MarkDensityFault(VoxelDensityPipelineFaultSlots.WeldInput);
                rv.localPosition = float3.zero;
            }

            if (TryResolveEdgeRegistrySlot(rv.edgeId, out int axis, out int edgeSlot))
            {
                int existingIdx = ReadEdgeVertex(axis, edgeSlot);
                if (existingIdx >= 0 && existingIdx < weldedCount)
                {
                    triangleIndices[i] = existingIdx;
                    continue;
                }

                int newIdx = weldedCount;
                if (newIdx >= weldedPositions.Length)
                {
                    MarkDensityFault(VoxelDensityPipelineFaultSlots.WeldOutput);
                    triangleIndices[i] = InvalidVertexIndex;
                    break;
                }

                weldedPositions[newIdx] = rv.localPosition;
                WriteEdgeVertex(axis, edgeSlot, newIdx);
                triangleIndices[i] = newIdx;
                weldedCount++;
                continue;
            }

            int fallbackIdx = weldedCount;
            if (fallbackIdx >= weldedPositions.Length)
            {
                MarkDensityFault(VoxelDensityPipelineFaultSlots.WeldOutput);
                triangleIndices[i] = InvalidVertexIndex;
                break;
            }

            weldedPositions[fallbackIdx] = rv.localPosition;
            triangleIndices[i] = fallbackIdx;
            weldedCount++;
        }
        weldedCounter[0] = weldedCount;
    }

    bool HasSafeWeldInputs()
    {
        long totalGridPoints = (long)ptsX * ptsY * ptsZ;
        long expectedEdgeVertexX = (long)(ptsX - 1) * ptsY * ptsZ;
        long expectedEdgeVertexY = (long)ptsX * (ptsY - 1) * ptsZ;
        long expectedEdgeVertexZ = (long)ptsX * ptsY * (ptsZ - 1);
        return rawCount >= 0 &&
            ptsX > 1 && ptsY > 1 && ptsZ > 1 &&
            totalGridPoints > 0L && totalGridPoints <= MaxSafeWeldGridPointCount &&
            rawVertices.IsCreated && rawCount <= rawVertices.Length &&
            weldedPositions.IsCreated && rawCount <= weldedPositions.Length &&
            triangleIndices.IsCreated && rawCount <= triangleIndices.Length &&
            edgeVertexX.IsCreated && expectedEdgeVertexX > 0L && expectedEdgeVertexX <= edgeVertexX.Length &&
            edgeVertexY.IsCreated && expectedEdgeVertexY > 0L && expectedEdgeVertexY <= edgeVertexY.Length &&
            edgeVertexZ.IsCreated && expectedEdgeVertexZ > 0L && expectedEdgeVertexZ <= edgeVertexZ.Length;
    }

    bool TryResolveEdgeRegistrySlot(long packedEdge, out int axis, out int slot)
    {
        int lo = (int)(packedEdge & 0xFFFFFFFFL);
        int hi = (int)(packedEdge >> 32);
        long totalGridPoints = (long)ptsX * ptsY * ptsZ;
        if (lo < 0 || hi < 0 || hi <= lo || hi >= totalGridPoints)
        {
            axis = -1;
            slot = -1;
            return false;
        }

        int strideX = ptsX;
        int strideXY = ptsX * ptsY;
        int diff = hi - lo;
        int x = lo % ptsX;
        int y = (lo / ptsX) % ptsY;
        int z = lo / strideXY;
        int cellsX = ptsX - 1;
        int cellsY = ptsY - 1;
        int cellsZ = ptsZ - 1;

        if (diff == 1 && x < cellsX)
        {
            axis = 0;
            slot = x + y * cellsX + z * cellsX * ptsY;
            return slot >= 0 && slot < edgeVertexX.Length;
        }

        if (diff == strideX && y < cellsY)
        {
            axis = 1;
            slot = x + y * ptsX + z * ptsX * cellsY;
            return slot >= 0 && slot < edgeVertexY.Length;
        }

        if (diff == strideXY && z < cellsZ)
        {
            axis = 2;
            slot = x + y * ptsX + z * ptsX * ptsY;
            return slot >= 0 && slot < edgeVertexZ.Length;
        }

        axis = -1;
        slot = -1;
        return false;
    }

    int ReadEdgeVertex(int axis, int slot)
    {
        switch (axis)
        {
            case 0:
                return edgeVertexX[slot];
            case 1:
                return edgeVertexY[slot];
            case 2:
                return edgeVertexZ[slot];
            default:
                return InvalidVertexIndex;
        }
    }

    void WriteEdgeVertex(int axis, int slot, int vertexIndex)
    {
        switch (axis)
        {
            case 0:
                edgeVertexX[slot] = vertexIndex;
                break;
            case 1:
                edgeVertexY[slot] = vertexIndex;
                break;
            case 2:
                edgeVertexZ[slot] = vertexIndex;
                break;
        }
    }

    void MarkDensityFault(int slot)
    {
        if (densityFaultFlags.IsCreated && (uint)slot < (uint)densityFaultFlags.Length)
            densityFaultFlags[slot] = 1;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

// -------------------------------------------------------------------------------
//  JOB 3: Cheap SDF normals and cinematic curvature masks
// -------------------------------------------------------------------------------
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelNormalJob : IJobParallelFor
{
    const float SolidNeighborAoScale = 0.111111112f;

    public int ptsX, ptsY, ptsZ;
    public int densityStrideY;
    public int densityStrideZ;
    public float3 volumeOrigin;
    public float invVoxelStep;
    [ReadOnly, NoAlias] public NativeArray<sbyte> densityField;
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [WriteOnly, NoAlias] public NativeArray<float3> normals;
    [WriteOnly, NoAlias] public NativeArray<float> curvatureValues;
    [WriteOnly, NoAlias] public NativeArray<float> ambientOcclusionValues;
    [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> densityFaultFlags;

    public void Execute(int idx)
    {
        if (!normals.IsCreated || !curvatureValues.IsCreated || !ambientOcclusionValues.IsCreated ||
            idx < 0 || idx >= normals.Length || idx >= curvatureValues.Length || idx >= ambientOcclusionValues.Length)
        {
            MarkDensityFault(VoxelDensityPipelineFaultSlots.NormalFallback);
            return;
        }

        if (!positions.IsCreated || !densityField.IsCreated || idx >= positions.Length || ptsX <= 1 || ptsY <= 1 ||
            ptsZ <= 1 || densityStrideY <= 0 || densityStrideZ <= 0 || !IsFinite(volumeOrigin) ||
            !math.isfinite(invVoxelStep) || invVoxelStep <= 0f || !HasCompleteDensityField())
        {
            WriteDefault(idx);
            return;
        }

        float3 wp = positions[idx];
        if (!IsFinite(wp))
        {
            WriteDefault(idx);
            return;
        }

        float3 sample = (wp - volumeOrigin) * invVoxelStep;
        if (!IsFinite(sample))
        {
            WriteDefault(idx);
            return;
        }

        int x = (int)math.clamp(sample.x + 0.5f, 0f, ptsX - 1f);
        int y = (int)math.clamp(sample.y + 0.5f, 0f, ptsY - 1f);
        int z = (int)math.clamp(sample.z + 0.5f, 0f, ptsZ - 1f);
        float4 gradientAndAo = SampleNearestGridGradientAndAo(x, y, z);
        if (!IsFinite(gradientAndAo))
        {
            WriteDefault(idx);
            return;
        }

        float3 normal = ApproxNormalizeOrUp(-gradientAndAo.xyz);
        normals[idx] = normal;

        float horizontalMask = SaturateFinite((math.abs(normal.x) + math.abs(normal.z)) * 0.5f);
        float ceilingMask = SaturateFinite(-normal.y);
        float neighborCavityMask = SaturateFinite(1f - gradientAndAo.w);
        float curvature01 = SaturateFinite(0.45f + horizontalMask * 0.18f - ceilingMask * 0.22f + neighborCavityMask * 0.12f);
        curvatureValues[idx] = curvature01;

        float cavityMask = SaturateFinite((0.5f - curvature01) * 2f);
        float overheadMask = SaturateFinite(0.5f - normal.y * 0.5f);
        float neighborAo = SaturateFinite(gradientAndAo.w - cavityMask * 0.24f - overheadMask * 0.12f);
        float raymarchAo = ResolveTwoStepSdfAo(wp, normal);
        ambientOcclusionValues[idx] = SaturateFinite(math.min(neighborAo, raymarchAo));
    }

    bool HasCompleteDensityField()
    {
        long expectedLength = (long)ptsX * ptsY * ptsZ;
        long maxIndex = (long)(ptsX - 1) + (long)(ptsY - 1) * densityStrideY + (long)(ptsZ - 1) * densityStrideZ;
        return expectedLength > 0L && expectedLength <= densityField.Length && maxIndex >= 0L && maxIndex < densityField.Length;
    }

    void WriteDefault(int idx)
    {
        MarkDensityFault(VoxelDensityPipelineFaultSlots.NormalFallback);
        normals[idx] = new float3(0f, 1f, 0f);
        curvatureValues[idx] = 0.5f;
        ambientOcclusionValues[idx] = 1f;
    }

    void MarkDensityFault(int slot)
    {
        if (densityFaultFlags.IsCreated && (uint)slot < (uint)densityFaultFlags.Length)
            densityFaultFlags[slot] = 1;
    }

    static float3 ApproxNormalizeOrUp(float3 value)
    {
        if (!IsFinite(value))
            return new float3(0f, 1f, 0f);

        float3 axis = math.abs(value);
        float maxAxis = math.cmax(axis);
        float minAxis = math.cmin(axis);
        float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
        float invLen = math.rcp(math.max(maxAxis + midAxis * 0.375f + minAxis * 0.25f, 0.0001f));
        float3 normalized = value * invLen;
        return math.select(new float3(0f, 1f, 0f), normalized, math.isfinite(maxAxis) && maxAxis > 0.0001f && IsFinite(normalized));
    }

    float ResolveTwoStepSdfAo(float3 position, float3 normal)
    {
        float d1 = SampleDensityTrilinear((position + normal * 1.5f - volumeOrigin) * invVoxelStep);
        float d2 = SampleDensityTrilinear((position + normal * 3.5f - volumeOrigin) * invVoxelStep);
        if (!math.isfinite(d1) || !math.isfinite(d2))
            return 1f;

        float nearSolid = math.smoothstep(-2.0f, 6.0f, d1);
        float farSolid = math.smoothstep(-2.0f, 6.0f, d2);
        return SaturateFinite(1f - nearSolid * 0.72f - farSolid * 0.28f);
    }

    float SampleDensityTrilinear(float3 sample)
    {
        if (!IsFinite(sample))
            return float.NaN;

        sample = math.clamp(sample, new float3(0f), new float3(ptsX - 1f, ptsY - 1f, ptsZ - 1f));
        int x0 = (int)math.floor(sample.x);
        int y0 = (int)math.floor(sample.y);
        int z0 = (int)math.floor(sample.z);
        int x1 = math.min(x0 + 1, ptsX - 1);
        int y1 = math.min(y0 + 1, ptsY - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = sample.x - x0;
        float fy = sample.y - y0;
        float fz = sample.z - z0;

        float c000 = DensityAt(x0, y0, z0);
        float c100 = DensityAt(x1, y0, z0);
        float c010 = DensityAt(x0, y1, z0);
        float c110 = DensityAt(x1, y1, z0);
        float c001 = DensityAt(x0, y0, z1);
        float c101 = DensityAt(x1, y0, z1);
        float c011 = DensityAt(x0, y1, z1);
        float c111 = DensityAt(x1, y1, z1);

        float c00 = math.lerp(c000, c100, fx);
        float c10 = math.lerp(c010, c110, fx);
        float c01 = math.lerp(c001, c101, fx);
        float c11 = math.lerp(c011, c111, fx);
        float c0 = math.lerp(c00, c10, fy);
        float c1 = math.lerp(c01, c11, fy);
        return math.lerp(c0, c1, fz);
    }

    float DensityAt(int x, int y, int z)
    {
        return densityField[x + y * densityStrideY + z * densityStrideZ];
    }

    float4 SampleNearestGridGradientAndAo(int x, int y, int z)
    {
        int centerIndex = x + y * densityStrideY + z * densityStrideZ;
        int xmIndex = centerIndex - math.select(0, 1, x > 0);
        int xpIndex = centerIndex + math.select(0, 1, x < ptsX - 1);
        int ymIndex = centerIndex - math.select(0, densityStrideY, y > 0);
        int ypIndex = centerIndex + math.select(0, densityStrideY, y < ptsY - 1);
        int zmIndex = centerIndex - math.select(0, densityStrideZ, z > 0);
        int zpIndex = centerIndex + math.select(0, densityStrideZ, z < ptsZ - 1);

        float center = densityField[centerIndex];
        float xm = densityField[xmIndex];
        float xp = densityField[xpIndex];
        float ym = densityField[ymIndex];
        float yp = densityField[ypIndex];
        float zm = densityField[zmIndex];
        float zp = densityField[zpIndex];
        int solidNeighborCount =
            math.select(0, 1, xm > 0f) +
            math.select(0, 1, xp > 0f) +
            math.select(0, 1, ym > 0f) +
            math.select(0, 1, yp > 0f) +
            math.select(0, 1, zm > 0f) +
            math.select(0, 1, zp > 0f);
        float neighborAo = 1f - solidNeighborCount * SolidNeighborAoScale;

        return new float4(
            math.select(center - xm, xp - center, x < ptsX - 1),
            math.select(center - ym, yp - center, y < ptsY - 1),
            math.select(center - zm, zp - center, z < ptsZ - 1),
            SaturateFinite(neighborAo));
    }

    static float SaturateFinite(float value)
    {
        return math.select(0f, math.saturate(value), math.isfinite(value));
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    static bool IsFinite(float4 value)
    {
        return math.all(math.isfinite(value));
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelTerrainSeamSnapJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public float seamTransitionBand;
    public float seamOverlap;

    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [NoAlias] public NativeArray<float3> positions;

    public void Execute(int idx)
    {
        long terrainGridLength = (long)ptsX * ptsZ;
        if (!terrainHeights.IsCreated ||
            !positions.IsCreated ||
            idx < 0 ||
            idx >= positions.Length ||
            ptsX <= 1 ||
            ptsZ <= 1 ||
            terrainGridLength <= 0L ||
            terrainGridLength > terrainHeights.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f ||
            !math.isfinite(seamTransitionBand) ||
            seamTransitionBand <= 0f ||
            !IsFinite(volumeOrigin))
        {
            return;
        }

        float3 position = positions[idx];
        if (!IsFinite(position))
            return;

        float boundaryDistance = VoxelSeamDirector.ComputeBoundaryDistance(
            position.xz,
            volumeOrigin,
            ptsX,
            ptsZ,
            voxelStep);
        if (!math.isfinite(boundaryDistance) || boundaryDistance > seamTransitionBand)
            return;

        float terrainHeight = SampleTerrainHeight(position.xz);
        if (!math.isfinite(terrainHeight))
            return;

        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        if (!math.isfinite(blendToTerrain))
            return;

        float targetHeight = VoxelSeamDirector.ComputeTargetSnapHeight(terrainHeight, seamOverlap);
        float snappedY = math.lerp(position.y, targetHeight, blendToTerrain);
        if (!math.isfinite(targetHeight) || !math.isfinite(snappedY))
            return;

        positions[idx] = new float3(position.x, snappedY, position.z);
    }

    float SampleTerrainHeight(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (absoluteWorldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = terrainHeights[x0 + z0 * ptsX];
        float h10 = terrainHeights[x1 + z0 * ptsX];
        float h01 = terrainHeights[x0 + z1 * ptsX];
        float h11 = terrainHeights[x1 + z1 * ptsX];
        if (!math.isfinite(h00) || !math.isfinite(h10) || !math.isfinite(h01) || !math.isfinite(h11))
            return float.NaN;

        return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSeamNormalBlendJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public float seamTransitionBand;

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [NoAlias] public NativeArray<float3> normals;

    public void Execute(int idx)
    {
        long terrainGridLength = (long)ptsX * ptsZ;
        if (!terrainHeights.IsCreated ||
            !positions.IsCreated ||
            !normals.IsCreated ||
            idx < 0 ||
            idx >= positions.Length ||
            idx >= normals.Length ||
            ptsX <= 1 ||
            ptsZ <= 1 ||
            terrainGridLength <= 0L ||
            terrainGridLength > terrainHeights.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f ||
            !math.isfinite(seamTransitionBand) ||
            seamTransitionBand <= 0f ||
            !IsFinite(volumeOrigin))
        {
            return;
        }

        float3 position = positions[idx];
        if (!IsFinite(position))
            return;

        float boundaryDistance = VoxelSeamDirector.ComputeBoundaryDistance(
            position.xz,
            volumeOrigin,
            ptsX,
            ptsZ,
            voxelStep);
        if (!math.isfinite(boundaryDistance) || boundaryDistance > seamTransitionBand)
            return;

        float3 terrainNormal = SampleTerrainNormal(position.xz);
        if (!IsFinite(terrainNormal))
            return;

        float3 voxelNormal = NormalizeFastOrDefault(normals[idx], new float3(0f, 1f, 0f));
        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        if (!math.isfinite(blendToTerrain))
            return;

        float3 blendedNormal = BlendNormalsNlerp(voxelNormal, terrainNormal, blendToTerrain);
        if (!IsFinite(blendedNormal))
            return;

        normals[idx] = blendedNormal;
    }

    float SampleTerrainHeight(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (absoluteWorldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = terrainHeights[x0 + z0 * ptsX];
        float h10 = terrainHeights[x1 + z0 * ptsX];
        float h01 = terrainHeights[x0 + z1 * ptsX];
        float h11 = terrainHeights[x1 + z1 * ptsX];
        if (!math.isfinite(h00) || !math.isfinite(h10) || !math.isfinite(h01) || !math.isfinite(h11))
            return float.NaN;

        return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
    }

    float3 SampleTerrainNormal(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (absoluteWorldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float3 normal00 = ResolveTerrainGridNormal(x0, z0);
        float3 normal10 = ResolveTerrainGridNormal(x1, z0);
        float3 normal01 = ResolveTerrainGridNormal(x0, z1);
        float3 normal11 = ResolveTerrainGridNormal(x1, z1);
        float3 normalX0 = math.lerp(normal00, normal10, fx);
        float3 normalX1 = math.lerp(normal01, normal11, fx);
        float3 normal = NormalizeFastOrDefault(math.lerp(normalX0, normalX1, fz), new float3(0f, 1f, 0f));
        return IsFinite(normal) ? normal : new float3(0f, 1f, 0f);
    }

    float3 ResolveTerrainGridNormal(int x, int z)
    {
        int xPrev = math.max(x - 1, 0);
        int xNext = math.min(x + 1, ptsX - 1);
        int zPrev = math.max(z - 1, 0);
        int zNext = math.min(z + 1, ptsZ - 1);

        float heightLeft = terrainHeights[xPrev + z * ptsX];
        float heightRight = terrainHeights[xNext + z * ptsX];
        float heightBack = terrainHeights[x + zPrev * ptsX];
        float heightForward = terrainHeights[x + zNext * ptsX];
        if (!math.isfinite(heightLeft) || !math.isfinite(heightRight) || !math.isfinite(heightBack) || !math.isfinite(heightForward))
            return new float3(0f, 1f, 0f);

        float stepX = math.max((xNext - xPrev) * voxelStep, voxelStep);
        float stepZ = math.max((zNext - zPrev) * voxelStep, voxelStep);
        float3 tangentX = new float3(stepX, heightRight - heightLeft, 0f);
        float3 tangentZ = new float3(0f, heightForward - heightBack, stepZ);
        return NormalizeFastOrDefault(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
    }

    static float3 BlendNormalsNlerp(float3 startNormal, float3 endNormal, float t)
    {
        float blend = math.isfinite(t) ? math.saturate(t) : 0f;
        return NormalizeFastOrDefault(math.lerp(startNormal, endNormal, blend), startNormal);
    }

    static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
    {
        if (!IsFinite(value))
            return fallback;

        float lengthSq = math.lengthsq(value);
        return math.isfinite(lengthSq) && lengthSq > 0.0001f ? value / math.max(LengthApprox(value), 0.0001f) : fallback;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    static float LengthApprox(float3 value)
    {
        return math.length(value);
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelShiftAwareProjectionJob : IJobParallelFor
{
    public float3 rebaseDelta;
    public float3 rootRuntimePosition;

    [ReadOnly, NoAlias] public NativeArray<float3> sourcePositions;
    [WriteOnly, NoAlias] public NativeArray<float3> projectedPositions;

    public void Execute(int index)
    {
        if (!projectedPositions.IsCreated || index < 0 || index >= projectedPositions.Length)
            return;

        projectedPositions[index] = float3.zero;
        if (!sourcePositions.IsCreated || index >= sourcePositions.Length || !IsFinite(rebaseDelta) || !IsFinite(rootRuntimePosition))
            return;

        float3 source = sourcePositions[index];
        if (!IsFinite(source))
            return;

        float3 projected = source + rebaseDelta - rootRuntimePosition;
        if (!IsFinite(projected))
            return;

        projectedPositions[index] = projected;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 3.5: Biome Sampling (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelBiomeSampleJob : IJobParallelFor
{
    public int ptsX, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    [ReadOnly, NoAlias] public NativeArray<float> gridBiome;
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [WriteOnly, NoAlias] public NativeArray<float> biomeValues;

    public void Execute(int idx)
    {
        if (!biomeValues.IsCreated || idx < 0 || idx >= biomeValues.Length)
            return;

        long biomeGridLength = (long)ptsX * ptsZ;
        if (!gridBiome.IsCreated ||
            !positions.IsCreated ||
            idx >= positions.Length ||
            ptsX <= 1 ||
            ptsZ <= 1 ||
            biomeGridLength <= 0L ||
            biomeGridLength > gridBiome.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f ||
            !IsFinite(volumeOrigin))
        {
            biomeValues[idx] = 0f;
            return;
        }

        float3 wp=positions[idx];
        if (!IsFinite(wp))
        {
            biomeValues[idx] = 0f;
            return;
        }

        float lx=(wp.x-volumeOrigin.x)/voxelStep;
        float lz=(wp.z-volumeOrigin.z)/voxelStep;
        lx=math.clamp(lx,0f,ptsX-1f);
        lz=math.clamp(lz,0f,ptsZ-1f);
        int x0=(int)lx,z0=(int)lz;
        int x1=math.min(x0+1,ptsX-1);
        int z1=math.min(z0+1,ptsZ-1);
        float fx=lx-x0,fz=lz-z0;
        float v00=SaturateFinite(gridBiome[x0+z0*ptsX]);
        float v10=SaturateFinite(gridBiome[x1+z0*ptsX]);
        float v01=SaturateFinite(gridBiome[x0+z1*ptsX]);
        float v11=SaturateFinite(gridBiome[x1+z1*ptsX]);
        biomeValues[idx]=SaturateFinite(math.lerp(math.lerp(v00,v10,fx),math.lerp(v01,v11,fx),fz));
    }

    static float SaturateFinite(float value)
    {
        return math.isfinite(value) ? math.saturate(value) : 0f;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 4: Vertex Colors (v4.0 — updated for cave SDF)
// ═══════════════════════════════════════════════════════════════════════════════
// The BurstCompile attribute below belongs to this job. It had drifted onto the
// VoxelSurfaceColorEncoding helper class that used to sit between this banner and the job
// declaration, where it was inert, leaving this IJobParallelFor running as managed IL.
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelColorJob : IJobParallelFor
{
    private const float MinSafeCaveMouthColorRadius = 0.1f;
    private const float MaxSafeCaveMouthColorRadius = 32f;
    private const float MaxSafeColorVolumeExtent = 1048576f;

    public float maxDepth;
    public float caveEdgeWidth;
    public float seamTransitionBand;
    public float3 volumeCenter;
    public float volumeHalfExtent;
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public int lodLevel;
    public float lodTransitionBand;

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float3> normals;
    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [ReadOnly, NoAlias] public NativeArray<float> gridBiome;
    [ReadOnly, NoAlias] public NativeArray<float> curvatureValues;
    [ReadOnly, NoAlias] public NativeArray<float> ambientOcclusionValues;
    [ReadOnly, NoAlias] public NativeArray<float> biomeValues;
    [ReadOnly, NoAlias] public NativeArray<CaveEntrance> caveEntrances;
    [ReadOnly, NoAlias] public NativeArray<VoxelModifiedCellEntry> modifiedCells;
    [ReadOnly, NoAlias] public NativeArray<int> modifiedCellBucketHeads;
    [ReadOnly, NoAlias] public NativeArray<int> modifiedCellNext;
    public int modifiedCellCount;
    public int modifiedCellBucketCount;

    public double3 absoluteCellOffset;

    [WriteOnly, NoAlias] public NativeArray<Color32> colors;
    [NoAlias] public NativeArray<float> skirtAlphaValues;

    public void Execute(int idx)
    {
        if (!colors.IsCreated || idx < 0 || idx >= colors.Length)
            return;

        if (!positions.IsCreated || idx >= positions.Length)
        {
            WriteClearColor(idx);
            return;
        }

        float3 p = positions[idx];
        if (!IsFinite(p))
        {
            WriteClearColor(idx);
            return;
        }

        float localizedAo = ambientOcclusionValues.IsCreated && idx < ambientOcclusionValues.Length
            ? SaturateFinite(ambientOcclusionValues[idx])
            : 1f;

        float terrainSkirt = 0f;
        long terrainGridLength = (long)ptsX * ptsZ;
        if (terrainHeights.IsCreated &&
            ptsX > 1 &&
            ptsZ > 1 &&
            terrainGridLength > 0L &&
            terrainGridLength <= terrainHeights.Length &&
            IsFinite(volumeOrigin) &&
            math.isfinite(voxelStep) &&
            voxelStep > 0f)
        {
            float terrainHeight = SampleTerrainHeight(p.xz);
            if (math.isfinite(terrainHeight))
            {
                float seamBand = ClampFinite(seamTransitionBand, 0.01f, 0.01f, MaxSafeColorVolumeExtent);
                terrainSkirt = 1f - math.smoothstep(0f, seamBand, math.abs(terrainHeight - p.y));
            }
        }

        float lodEdgeSkirt = 0f;
        if (lodLevel > 0 && ptsX > 1 && ptsZ > 1 && IsFinite(volumeOrigin) && math.isfinite(voxelStep) && voxelStep > 0f)
        {
            float volumeSizeX = ClampFinite((ptsX - 1) * voxelStep, 0f, 0f, MaxSafeColorVolumeExtent);
            float volumeSizeZ = ClampFinite((ptsZ - 1) * voxelStep, 0f, 0f, MaxSafeColorVolumeExtent);
            float localX = p.x - volumeOrigin.x;
            float localZ = p.z - volumeOrigin.z;
            float edgeDist = math.min(localX, math.min(volumeSizeX - localX, math.min(localZ, volumeSizeZ - localZ)));
            float lodBand = math.max(ClampFinite(lodTransitionBand, voxelStep, 0.01f, MaxSafeColorVolumeExtent), voxelStep);
            lodEdgeSkirt = 1f - math.smoothstep(0f, lodBand, edgeDist);
        }

        float skirtAlpha = SaturateFinite(math.max(terrainSkirt, lodEdgeSkirt));
        if (skirtAlphaValues.IsCreated && idx < skirtAlphaValues.Length)
            skirtAlphaValues[idx] = math.max(SaturateFinite(skirtAlphaValues[idx]), skirtAlpha);

        float3 normal = normals.IsCreated && idx < normals.Length && IsFinite(normals[idx])
            ? normals[idx]
            : new float3(0f, 1f, 0f);
        byte aoByte = (byte)math.clamp((int)math.round(localizedAo * 255f), 0, 255);
        colors[idx] = VoxelSurfaceColorEncoding.Resolve(normal, aoByte);
    }

    void WriteClearColor(int idx)
    {
        if (skirtAlphaValues.IsCreated && idx < skirtAlphaValues.Length)
            skirtAlphaValues[idx] = 0f;

        // Degenerate vertex: full wall weight, unoccluded. Explicit rather than incidental.
        colors[idx] = new Color32(0, 255, 0, 255);
    }

    bool IsModifiedSdfCell(float3 position)
    {
        if (!modifiedCells.IsCreated ||
            modifiedCellCount <= 0 ||
            !IsFinite(position) ||
            !math.all(math.isfinite(absoluteCellOffset)) ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f)
        {
            return false;
        }

        double invVoxelStep = 1.0d / math.max((double)voxelStep, 0.0001d);
        double3 absolutePosition = new double3(position.x, position.y, position.z) + absoluteCellOffset;
        int3 cell = (int3)math.floor(absolutePosition * invVoxelStep);
        return ContainsModifiedCell(cell);
    }

    bool ContainsModifiedCell(int3 cell)
    {
        if (!modifiedCellBucketHeads.IsCreated || !modifiedCellNext.IsCreated || modifiedCellBucketCount <= 0)
            return false;

        int count = math.min(modifiedCellCount, math.min(modifiedCells.Length, modifiedCellNext.Length));
        int bucketCount = math.min(modifiedCellBucketCount, modifiedCellBucketHeads.Length);
        if (count <= 0 || bucketCount <= 0)
            return false;

        int cursor = modifiedCellBucketHeads[ResolveModifiedCellBucket(cell, bucketCount)];
        int guard = 0;
        while ((uint)cursor < (uint)count && guard < count)
        {
            if (math.all(modifiedCells[cursor].AbsoluteCell == cell))
                return true;

            cursor = modifiedCellNext[cursor];
            guard++;
        }

        return false;
    }

    static int ResolveModifiedCellBucket(int3 cell, int bucketCount)
    {
        return (int)Hecton8.PureLogic.Systems.VoxelCellDirtystateBitHashingCalculator.Compute(cell.x, cell.y, cell.z, bucketCount);
    }

    bool TryResolveCaveMouthTerrainColor(float3 position, out float4 terrainColor, out float weight)
    {
        terrainColor = float4.zero;
        weight = 0f;
        if (!caveEntrances.IsCreated || caveEntrances.Length <= 0)
            return false;

        for (int i = 0; i < caveEntrances.Length; i++)
        {
            CaveEntrance entrance = caveEntrances[i];
            if (!TryResolveCaveMouthTerrainColorPayload(
                    in entrance,
                    out float3 surfacePosition,
                    out float radius,
                    out float4 safeTerrainSplatColor,
                    out float blend))
            {
                continue;
            }

            float safeVoxelStep = ResolveSafeVoxelStep();
            radius = math.max(radius, safeVoxelStep);
            float distanceSq = math.lengthsq(position - surfacePosition);
            float inner = radius * 0.35f;
            float outer = math.max(math.max(radius * 1.85f, safeVoxelStep), 0.0001f);
            float outerSq = outer * outer;
            float localWeight = (1f - math.smoothstep(inner * inner, outerSq, distanceSq)) * blend;
            if (localWeight <= weight)
                continue;

            weight = localWeight;
            float mouthDarkening = math.saturate(1f - distanceSq * math.rcp(outerSq)) * blend * 0.58f;
            terrainColor = safeTerrainSplatColor;
            terrainColor.xyz *= 1f - mouthDarkening;
        }

        return weight > 0.0001f;
    }

    bool TryResolveCaveMouthTerrainColorPayload(
        in CaveEntrance entrance,
        out float3 surfacePosition,
        out float radius,
        out float4 terrainColor,
        out float blend)
    {
        surfacePosition = entrance.surfacePosition;
        radius = default;
        terrainColor = float4.zero;
        blend = 0f;

        if (!IsFinite(surfacePosition) ||
            !math.isfinite(entrance.radius) ||
            entrance.radius <= 0f ||
            !IsFinite(entrance.terrainSplatColor))
        {
            return false;
        }

        blend = math.max(SaturateFinite(entrance.terrainSplatBlend), SaturateFinite(entrance.terrainSplatColor.w));
        if (blend <= 0.0001f)
            return false;

        radius = math.clamp(entrance.radius, MinSafeCaveMouthColorRadius, MaxSafeCaveMouthColorRadius);
        terrainColor = SaturateFinite(entrance.terrainSplatColor);
        terrainColor.w = blend;
        return math.isfinite(radius) && IsFinite(terrainColor);
    }

    float ResolveSafeVoxelStep()
    {
        return math.isfinite(voxelStep) && voxelStep > 0f
            ? math.min(voxelStep, MaxSafeCaveMouthColorRadius)
            : MinSafeCaveMouthColorRadius;
    }

    static float ClampFinite(float value, float fallback, float minimum, float maximum)
    {
        return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
    }

    static float SaturateFinite(float value)
    {
        return math.isfinite(value) ? math.saturate(value) : 0f;
    }

    static float4 SaturateFinite(float4 value)
    {
        return IsFinite(value) ? math.saturate(value) : float4.zero;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    static bool IsFinite(float4 value)
    {
        return math.all(math.isfinite(value));
    }

    float SampleTerrainHeight(float2 worldXZ)
    {
        float invVoxelStep = math.rcp(voxelStep);
        float localX = (worldXZ.x - volumeOrigin.x) * invVoxelStep;
        float localZ = (worldXZ.y - volumeOrigin.z) * invVoxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = terrainHeights[x0 + z0 * ptsX];
        float h10 = terrainHeights[x1 + z0 * ptsX];
        float h01 = terrainHeights[x0 + z1 * ptsX];
        float h11 = terrainHeights[x1 + z1 * ptsX];
        return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 5: Cave Interior Spawn Points (v4.1 — deterministic hash IDs)
//  Extracts floor positions from welded mesh for loot/flora/fauna spawning.
//  Each point carries a deterministic hashId derived from world position,
//  ensuring save system consistency regardless of parallel execution order.
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelPackSurfaceVertexJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float3> normals;
    [ReadOnly, NoAlias] public NativeArray<float> ambientOcclusionValues;
    [ReadOnly, NoAlias] public NativeArray<float> curvatureValues;
    [ReadOnly, NoAlias] public NativeArray<float> skirtAlphaValues;
    [ReadOnly, NoAlias] public NativeArray<float> dirtyBlendValues;
    [WriteOnly, NoAlias] public NativeArray<VoxelSurfaceVertex> surfaceVertices;
    public int vertexCount;
    public float3 boundsMin;
    public float3 boundsMax;
    public float3 runtimePositionOffset;

    public void Execute(int index)
    {
        if (!surfaceVertices.IsCreated || index < 0 || index >= vertexCount || index >= surfaceVertices.Length)
            return;

        float3 localPosition = positions.IsCreated && index < positions.Length && IsFinite(positions[index])
            ? positions[index]
            : float3.zero;
        float3 normal = normals.IsCreated && index < normals.Length
            ? NormalizeFiniteOrUp(normals[index])
            : new float3(0f, 1f, 0f);
        float3 runtimePosition = localPosition + runtimePositionOffset;
        if (!IsFinite(runtimePosition))
            runtimePosition = localPosition;

        float ao = ambientOcclusionValues.IsCreated && index < ambientOcclusionValues.Length
            ? SaturateFinite(ambientOcclusionValues[index], 1f)
            : 1f;
        float dirtyBlend = dirtyBlendValues.IsCreated && index < dirtyBlendValues.Length
            ? SaturateFinite(dirtyBlendValues[index], 0f)
            : 0f;
        float skirtAlpha = skirtAlphaValues.IsCreated && index < skirtAlphaValues.Length
            ? SaturateFinite(skirtAlphaValues[index], 0f)
            : 0f;
        float curvature = curvatureValues.IsCreated && index < curvatureValues.Length
            ? SaturateFinite(curvatureValues[index], 0.5f)
            : 0.5f;
        float chunkBorderStitch = ResolveChunkBorderStitchWeight(localPosition, boundsMin, boundsMax);
        byte aoByte = (byte)math.clamp((int)math.round(ao * 255f), 0, 255);

        surfaceVertices[index] = new VoxelSurfaceVertex
        {
            Position = localPosition,
            Normal = normal,
            Color = VoxelSurfaceColorEncoding.Resolve(normal, aoByte),
            BakedOcclusionUv1 = new float4(0f, 0f, 0f, ao),
            DirtyBlendUv2 = new float4(dirtyBlend, skirtAlpha, curvature, chunkBorderStitch),
            RuntimePositionWS = new float4(runtimePosition.x, runtimePosition.y, runtimePosition.z, runtimePosition.y)
        };
    }

    private static float ResolveChunkBorderStitchWeight(float3 localPosition, float3 min, float3 max)
    {
        float3 size = math.max(max - min, new float3(0.0001f));
        float3 edgeDistance = math.max(new float3(0f), math.min(localPosition - min, max - localPosition));
        float nearestEdgeDistance = math.cmin(edgeDistance);
        float stitchWidth = math.max(math.cmin(size) * 0.0625f, 0.25f);
        return math.saturate(1f - nearestEdgeDistance / stitchWidth);
    }

    private static float3 NormalizeFiniteOrUp(float3 value)
    {
        if (!IsFinite(value))
            return new float3(0f, 1f, 0f);

        float lengthSq = math.lengthsq(value);
        return math.isfinite(lengthSq) && lengthSq > 0.000001f
            ? value * math.rsqrt(lengthSq)
            : new float3(0f, 1f, 0f);
    }

    private static float SaturateFinite(float value, float fallback)
    {
        return math.isfinite(value) ? math.saturate(value) : fallback;
    }

    private static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSanitizeTriangleIndexJob : IJobParallelFor
{
    public int vertexCount;
    public int indexCount;
    [NoAlias] public NativeArray<int> triangleIndices;
    [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> densityFaultFlags;

    public void Execute(int triangleIdx)
    {
        if (!triangleIndices.IsCreated || triangleIdx < 0)
            return;

        int triangleCount = indexCount / 3;
        if (triangleIdx >= triangleCount)
            return;

        int baseIndex = triangleIdx * 3;
        if (baseIndex + 2 >= triangleIndices.Length)
            return;

        int i0 = triangleIndices[baseIndex];
        int i1 = triangleIndices[baseIndex + 1];
        int i2 = triangleIndices[baseIndex + 2];
        bool valid =
            (uint)i0 < (uint)vertexCount &&
            (uint)i1 < (uint)vertexCount &&
            (uint)i2 < (uint)vertexCount;
        if (valid)
            return;

        triangleIndices[baseIndex] = 0;
        triangleIndices[baseIndex + 1] = 0;
        triangleIndices[baseIndex + 2] = 0;
        if (densityFaultFlags.IsCreated && (uint)VoxelDensityPipelineFaultSlots.WeldOutput < (uint)densityFaultFlags.Length)
            densityFaultFlags[VoxelDensityPipelineFaultSlots.WeldOutput] = 1;
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDirtyBlendJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<VoxelModifiedCellEntry> modifiedCells;
    [ReadOnly, NoAlias] public NativeArray<int> modifiedCellBucketHeads;
    [ReadOnly, NoAlias] public NativeArray<int> modifiedCellNext;
    public int modifiedCellCount;
    public int modifiedCellBucketCount;
    public float voxelStep;
    public double3 absoluteCellOffset;
    [WriteOnly, NoAlias] public NativeArray<float> dirtyBlendValues;

    public void Execute(int index)
    {
        if (!dirtyBlendValues.IsCreated || index < 0 || index >= dirtyBlendValues.Length)
            return;

        if (!modifiedCells.IsCreated ||
            modifiedCellCount <= 0 ||
            !positions.IsCreated ||
            index >= positions.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f ||
            !math.all(math.isfinite(absoluteCellOffset)))
        {
            dirtyBlendValues[index] = 0f;
            return;
        }

        double invVoxelStep = 1.0d / math.max((double)voxelStep, 0.0001d);
        float3 position = positions[index];
        if (!IsFinite(position))
        {
            dirtyBlendValues[index] = 0f;
            return;
        }

        double3 absolutePosition = new double3(position.x, position.y, position.z) + absoluteCellOffset;
        if (!math.all(math.isfinite(absolutePosition)))
        {
            dirtyBlendValues[index] = 0f;
            return;
        }

        int3 cell = (int3)math.floor(absolutePosition * invVoxelStep);
        float blend = ContainsModifiedCell(cell) ? 1f : 0f;
        blend = math.max(blend, HasDirtyNeighbor(cell, new int3(1, 0, 0)));
        blend = math.max(blend, HasDirtyNeighbor(cell, new int3(-1, 0, 0)));
        blend = math.max(blend, HasDirtyNeighbor(cell, new int3(0, 1, 0)));
        blend = math.max(blend, HasDirtyNeighbor(cell, new int3(0, -1, 0)));
        blend = math.max(blend, HasDirtyNeighbor(cell, new int3(0, 0, 1)));
        blend = math.max(blend, HasDirtyNeighbor(cell, new int3(0, 0, -1)));
        dirtyBlendValues[index] = blend;
    }

    private float HasDirtyNeighbor(int3 cell, int3 offset)
    {
        return ContainsModifiedCell(cell + offset) ? 0.65f : 0f;
    }

    private bool ContainsModifiedCell(int3 cell)
    {
        if (!modifiedCellBucketHeads.IsCreated || !modifiedCellNext.IsCreated || modifiedCellBucketCount <= 0)
            return false;

        int count = math.min(modifiedCellCount, math.min(modifiedCells.Length, modifiedCellNext.Length));
        int bucketCount = math.min(modifiedCellBucketCount, modifiedCellBucketHeads.Length);
        if (count <= 0 || bucketCount <= 0)
            return false;

        int cursor = modifiedCellBucketHeads[ResolveModifiedCellBucket(cell, bucketCount)];
        int guard = 0;
        while ((uint)cursor < (uint)count && guard < count)
        {
            if (math.all(modifiedCells[cursor].AbsoluteCell == cell))
                return true;

            cursor = modifiedCellNext[cursor];
            guard++;
        }

        return false;
    }

    static int ResolveModifiedCellBucket(int3 cell, int bucketCount)
    {
        return (int)Hecton8.PureLogic.Systems.VoxelCellDirtystateBitHashingCalculator.Compute(cell.x, cell.y, cell.z, bucketCount);
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSpawnPointJob : IJob
{
    private const float MaxSafeSpawnPointCoordinate = 1048576f;

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float3> normals;
    [ReadOnly, NoAlias] public NativeArray<float> ambientOcclusionValues;

    /// <summary>Volume center for interior depth calculation.</summary>
    public float3 volumeCenter;
    public float volumeHalfExtent;

    /// <summary>Minimum upward normal component to qualify as "floor".
    /// 0.75 ≈ 41° from horizontal. Flat surfaces only.</summary>
    public float floorNormalThreshold;

    /// <summary>Minimum normalized interior depth to qualify.
    /// Prevents spawning near entrance mouth. Range 0-1.</summary>
    public float minInteriorDepth;

    /// <summary>Fraction of qualifying vertices to keep (0.03 = 3%).</summary>
    public float keepFraction;

    /// <summary>Seed for spatial hash. Must match cave generation seed.</summary>
    public uint seed;

    /// <summary>Output: floor spawn data with deterministic hash IDs. Owner job clamps writes to capacity.</summary>
    [NoAlias] public NativeArray<CaveSpawnData> spawnPoints;
    [NoAlias] public NativeArray<int> spawnPointCount;
    public int spawnPointCapacity;

    public void Execute()
    {
        if (!positions.IsCreated ||
            !normals.IsCreated ||
            !spawnPoints.IsCreated ||
            !spawnPointCount.IsCreated ||
            spawnPointCount.Length <= 0)
        {
            return;
        }

        int capacity = math.min(spawnPointCapacity, spawnPoints.Length);
        if (capacity <= 0)
            return;

        int count = math.min(positions.Length, normals.Length);
        for (int idx = 0; idx < count; idx++)
            TryAddSpawnPoint(idx);
    }

    void TryAddSpawnPoint(int idx)
    {
        int writeIndex = spawnPointCount[0];
        int capacity = math.min(spawnPointCapacity, spawnPoints.Length);
        if (writeIndex < 0 || writeIndex >= capacity)
            return;

        float3 pos = positions[idx];
        float3 nrm = normals[idx];
        if (!IsFinite(pos) || math.any(math.abs(pos) > new float3(MaxSafeSpawnPointCoordinate)) || !TryNormalizeFinite(nrm, out float3 normal))
            return;

        // ── Filter 1: Floor normal ──
        float safeFloorNormalThreshold = ClampFinite(floorNormalThreshold, 0.75f, -1f, 1f);
        float upDot = math.dot(normal, new float3(0, 1, 0));
        if (!math.isfinite(upDot) || upDot < safeFloorNormalThreshold)
            return;

        float openness = ambientOcclusionValues.IsCreated && idx < ambientOcclusionValues.Length
            ? ClampFinite(ambientOcclusionValues[idx], 0f, 0f, 1f)
            : 1f;
        if (openness < 0.42f)
            return;

        // ── Filter 2: Interior depth ──
        if (!math.isfinite(minInteriorDepth) || minInteriorDepth > 1f)
            return;

        float safeMinInteriorDepth = math.max(minInteriorDepth, 0f);
        if (safeMinInteriorDepth > 0f)
        {
            if (!IsFinite(volumeCenter) || !math.isfinite(volumeHalfExtent) || volumeHalfExtent <= 0f)
                return;

            float maxInteriorRadius = math.max(volumeHalfExtent, 1f) * math.max(0f, 1f - safeMinInteriorDepth);
            float distanceSq = math.lengthsq(pos - volumeCenter);
            if (!math.isfinite(distanceSq) || distanceSq > maxInteriorRadius * maxInteriorRadius)
                return;
        }

        // ── Filter 3: Spatial hash (deterministic thinning) ──
        float safeKeepFraction = ClampFinite(keepFraction, 0f, 0f, 1f);
        if (safeKeepFraction <= 0f)
            return;

        uint hash = SpatialHash(pos, seed);
        float hashNormalized = (hash & 0xFFFF) / 65535f;
        if (hashNormalized > safeKeepFraction)
            return;

        // ── Passed all filters ──
        // hashId is deterministic: same position → same hash → same ID always
        int hashId = (int)(hash & 0x7FFFFFFF);

        spawnPoints[writeIndex] = new CaveSpawnData
        {
            position = pos,
            hashId = hashId
        };
        spawnPointCount[0] = writeIndex + 1;
    }

    /// <summary>
    /// Deterministic spatial hash. Same position + same seed = same result.
    /// Thread execution order has ZERO effect on output.
    /// </summary>
    static uint SpatialHash(float3 p, uint seed)
    {
        // Quantize to 10cm grid — prevents floating-point jitter
        float3 safePoint = math.clamp(
            p,
            new float3(-MaxSafeSpawnPointCoordinate),
            new float3(MaxSafeSpawnPointCoordinate));
        int3 ip = (int3)math.floor(safePoint * 10f);

        uint h = seed;
        h ^= (uint)ip.x * 0x9E3779B9u;
        h ^= (uint)ip.y * 0x517CC1B7u;
        h ^= (uint)ip.z * 0x6C62272Eu;

        // Avalanche mixing (murmur3 finalizer)
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;

        return h;
    }

    static bool TryNormalizeFinite(float3 value, out float3 normalized)
    {
        normalized = default;
        if (!IsFinite(value))
            return false;

        float lengthSq = math.lengthsq(value);
        if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
            return false;

        normalized = value * math.rsqrt(lengthSq);
        return IsFinite(normalized);
    }

    static float ClampFinite(float value, float fallback, float minimum, float maximum)
    {
        return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: HECTON VOXEL ENGINE (v4.0)
// ════════════════════════════════════════════════════════════════════════════════
#region HectonVoxelEngine

public class HectonVoxelEngine : MonoBehaviour, Hecton8.Core.Contracts.IVoxelSonarSdfReadModel, Hecton8.Core.Contracts.IVoxelSonarSdfSurfaceResolver, Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel, IGlobalRegistryHotSwapListener
{
    private const string RuntimeCaveVolumeName = "CaveVolume";
    private const string RuntimeCaveMeshName = "CaveMesh";
    private const string NativeMemoryOwner = nameof(HectonVoxelEngine);
    private const int StreamingScratchLeaseTimeoutFrames = 1200;
    private const int VoxelJobWaitWatchdogFrames = 1200;
    private const int DeferredVoxelPhysicsBakeTeardownDrainBudget = 8;
    private const int DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget = 32;
    private const int DeferredVoxelPhysicsBakeTeardownInspectionBudget = 64;
    private const int DeferredVoxelPhysicsBakeTeardownBackpressureInspectionBudget = 64;
    private const float DeferredVoxelPhysicsBakeTeardownBudgetPerFrame = DeferredVoxelPhysicsBakeTeardownDrainBudget;
    private const float DeferredVoxelPhysicsBakeTeardownBudgetVisualOverkillPerFrame = DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget;
    private const float DeferredVoxelPhysicsBakeTeardownBurstCapBias = 0.5f;
    private const int DeferredVoxelPhysicsBakeBackpressureThreshold = 64;
    private const int DeferredVoxelPhysicsBakeBackpressureReleaseThreshold = 32;
    private const int DeferredVoxelPhysicsBakeTeardownCapacity = 2048;
    private const int DeferredVoxelPhysicsBakeEmergencyTeardownCapacity = 512;
    private const int DeferredVoxelColliderUploadCapacity = 2048;
    private const float DeferredVoxelColliderUploadBudgetPerFrame = 1f;
    private const float DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame = 4f;
    private const float DeferredVoxelColliderUploadBurstCapBias = 0.5f;
    private const int DeferredVoxelColliderUploadBackpressureBudget = 8;
    private const int DeferredVoxelColliderUploadRetryLimit = 4;
    private const int DeferredVoxelColliderUploadDropWarningReleaseThreshold = DeferredVoxelColliderUploadCapacity / 2;
    private const float VoxelMeshUploadBudgetPerFrame = 1f;
    private const float VoxelMeshUploadBudgetVisualOverkillPerFrame = 3f;
    private const float VoxelMeshUploadBurstCapBias = 0.5f;
    private const byte DeferredVoxelColliderUploadVolumeFlag = 1 << 0;
    private static readonly long ChunkGenerationFrameBudgetTicks = Stopwatch.Frequency / 500L;
    private static readonly double _JobAdmissionStopwatchMillisecondsPerTick = 1000.0d / Stopwatch.Frequency;
    private const byte DeferredVoxelBakeDestroyOwner = 1 << 0;
    private const float VoxelLodColliderDisableDistanceMeters = 200f;
    private const float VoxelPressureColliderDisableDistanceMeters = 120f;
    private const float VoxelColliderFakePressureFactor = 0.85f;
    private const float VoxelPhysicsBakeProxyMinHeightMeters = 1f;
    private const float VoxelColliderProxyMinExtentMeters = 0.01f;
    private const float VoxelColliderProxyMaxExtentMeters = 1048576f;
    private const float VoxelTerrainSnapHysteresisMeters = 0.05f;
    private const string VoxelBakeProxyRuntimeName = "VoxelBakeProxy";
    private const float OverhangCameraCullDotThreshold = -0.3f;
    private const float PredictiveVoxelProxyMinSpeedMetersPerSecond = 1f;
    private const float PredictiveVoxelProxyMaxDistanceMeters = 12f;
    private const float PredictiveVoxelProxyLookaheadSeconds = 0.35f;
    private const float PredictiveVoxelProxyDampenerStrength01 = 0.35f;
    private const float PredictiveVoxelProxyCinematicPaddingMeters = 0.75f;
    private const uint PredictiveVoxelProxyKccVelocityMaxAgeFrames = 12u;
    private const float MinRuntimeCaveEntranceHoleRadius = 0.1f;
    private const float MaxRuntimeCaveEntranceHoleRadius = 96f;
    private const float MaxRuntimeCaveEntranceHolePadding = 24f;
    private const float MinRuntimeMapMagicTileSize = 1f;
    private const float MaxRuntimeMapMagicTileSize = 1048576f;
    private const float MinRuntimeCaveGraphBucketRadius = 0.1f;
    private const float MaxRuntimeCaveGraphBucketRadius = 256f;
    private const float MaxRuntimeCaveGraphBucketBlendRadius = 96f;
    private const float MaxRuntimeCaveGraphBucketWarpMeters = 64f;
    private const float MaxRuntimeCaveGraphBucketNoiseMeters = 64f;
    private const float MaxRuntimeCaveGraphBucketVoxelStep = 64f;
    private const int VoxelSurfaceMeshPoolSize = 256;
    private const int VoxelPhysicsBakeMeshPoolSize = 256;
    private const int VoxelMeshPoolAcquireWarmupRetryFrames = 4;
    private const string VoxelSurfacePoolMeshName = "VoxelSurfacePool";
    private const string VoxelPhysicsBakePoolMeshName = "VoxelPhysicsBakePool";
    private const float VoxelAnomalySolveWarningMs = 0.2f;
    private const int VoxelMeshPipelineBlackBoxCapacity = 300;
    private const SystemID VoxelMeshPipelineBlackBoxOwnerSystemId = SystemID.WorldStreaming;
    private const BufferID VoxelMeshPipelineBlackBoxBufferId = BufferID.VoxelMeshPipelineBlackBox;
    private const uint VoxelMeshPipelineInvalidStateFlag = 1u << 0;
    private const uint VoxelMeshPipelineInvalidMeshDataFlag = 1u << 1;
    private const uint VoxelMeshPipelineScratchCapacityOverflowFlag = 1u << 2;
    private const uint VoxelMeshPipelineEmergencyBakeTeardownFlag = 1u << 3;
    private const uint VoxelMeshPipelineVolumeSpawnPoolMissFlag = 1u << 4;
    private const uint VoxelMeshPipelineNullVolumeColliderFallbackFlag = 1u << 5;
    private const uint VoxelMeshPipelineRegistryCorruptionFlag = 1u << 6;
    private const uint VoxelMeshPipelineRebuildFailClosedFlag = 1u << 7;
    private const uint VoxelMeshPipelineDensityEvaluationFaultFlag = 1u << 8;
    private const uint VoxelMeshPipelineQuantizeInputFaultFlag = 1u << 9;
    private const uint VoxelMeshPipelineMarchingCubesCountInputFaultFlag = 1u << 10;
    private const uint VoxelMeshPipelineMarchingCubesExtractInputFaultFlag = 1u << 11;
    private const uint VoxelMeshPipelineMarchingCubesExtractOutputFaultFlag = 1u << 12;
    private const uint VoxelMeshPipelineWeldInputFaultFlag = 1u << 13;
    private const uint VoxelMeshPipelineWeldOutputFaultFlag = 1u << 14;
    private const uint VoxelMeshPipelineNormalFallbackFaultFlag = 1u << 15;
    private const uint VoxelMeshPipelineBlackBoxDumpMagic = 0x564D5042u; // VMPB
    private const string VoxelMeshPipelineBlackBoxPrimaryDumpRelativePath = "Docs/AgentLogs/Dump_VOXEL_MESH_PIPELINE.bin";
    private const string VoxelMeshPipelineBlackBoxAgentDumpRelativePath = "Docs/AgentLogs/Dump_1315_VoxelEngine.bin";
    private const string VoxelMeshPipelineBlackBoxCompactionDumpRelativePath = "Docs/AgentLogs/Dump_1418_VoxelCompaction.bin";
    private const int BiomeHeatmapResolution = 256;
    private const int BiomeHeatmapMaxIndex = BiomeHeatmapResolution - 1;
    private const float VoxelChunkSkirtDepthMeters = 0.5f;
    private const float VoxelChunkSkirtWidthMeters = 1.25f;
    private const byte DeltaModeAdditive = 1 << 0;
    private const byte DeltaModeReplace = 1 << 1;
    private static readonly uint _VoxelTeardownBackpressureWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.TeardownBackpressure"));
    private static readonly uint _VoxelPhysicsBakeForceReleaseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.ForceRelease"));
    private static readonly uint _VoxelColliderUploadDropWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.ColliderUpload.Drop"));
    private static readonly uint _VoxelColliderUploadRetryDropWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.ColliderUpload.RetryDrop"));
    private static readonly uint _VoxelPhysicsBakePoolExhaustedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.MeshPoolExhausted"));
    private static readonly uint _VoxelSurfaceMeshPoolExhaustedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.Surface.MeshPoolExhausted"));
    private static readonly uint _VoxelPhysicsBakeContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.PhysicsBake"));
    private static readonly uint _VoxelAnomalySolveWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.Anomaly.SolveBudgetExceeded"));
    private static readonly uint _VoxelAnomalyContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.AnomalySolve"));
    private static readonly uint _VoxelMeshPipelineContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.MeshPipeline"));
    private static readonly uint _VoxelChunksMeshedPerFrameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.MeshPipeline.ChunksMeshedPerFrame"));
    private static readonly uint _VoxelBakeQueueLengthHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.MeshPipeline.BakeQueueLength"));
    private static readonly uint _VoxelAlienBiomeHash = H8DataHash.ComputeFnv1A32("biome.alien");
    private static readonly uint _VoxelAlienShortBiomeHash = H8DataHash.ComputeFnv1A32("alien");
    private static readonly uint _VoxelAlienSurfaceHash = H8DataHash.ComputeFnv1A32("surface.alien");
    private static readonly uint _VoxelAlienHeatmapHash = H8DataHash.ComputeFnv1A32("heatmap.alien");
    private static readonly uint _VoxelAlienRadiationHash = H8DataHash.ComputeFnv1A32("radiation.alien");
    private static bool _voxelAnomalySolveWarningArmed;
    private static bool _voxelColliderUploadDropWarningArmed;
    private static bool _voxelColliderUploadRetryDropWarningArmed;
    private static int _voxelMeshTelemetryFrame = -1;
    private static int _voxelChunksMeshedThisFrame;
    private static int _voxelMeshPipelineBlackBoxCursor;
    private static bool _voxelMeshPipelineBlackBoxDumped;
    private static bool _voxelMeshPoolWarmupRunning;
    private static int _voxelMeshUploadFrame = -1;
    private static int _voxelMeshUploadsThisFrame;
    private static float _voxelMeshUploadBudgetTokens;
    private static int _deferredVoxelColliderUploadFrame = -1;
    private static float _deferredVoxelColliderUploadBudgetTokens;
    private static int _deferredVoxelPhysicsBakeTeardownFrame = -1;
    private static float _deferredVoxelPhysicsBakeTeardownBudgetTokens;
    private static IPhysicsService s_predictiveVoxelProxyPhysicsService;
    private static VaultGenerationHandle<VoxelMeshPipelineTelemetryEntry> _voxelMeshPipelineBlackBoxHandle;
    private static IDataVault _voxelMeshPipelineBlackBoxVault;

    // ╔═══════════════════════════════════════════════╗
    // ║           INSPECTOR SETTINGS                  ║
    // ╚═══════════════════════════════════════════════╝

    [Header("═══ DEFAULT CAVE PRESET ═══")]
    [Tooltip("Default preset used when GenerateVolumeAsync is called without explicit preset.")]
    public bool UseSurfaceNets = false;
    public CavePreset defaultPreset = new CavePreset();
    [Header("═══ MAPMAGIC INTEGRATION ═══")]
    [Tooltip("MapMagic tile size in meters.\nMust match your MapMagic Tile Size setting.\nUsed to compute chunkCoord for ScavengePopulator spawn points.")]
    [SerializeField]
    private float mapMagicTileSize = 999f;
    [Header("═══ EDGE SEAL ═══")]
    [Tooltip("Margin (m) where density fades to solid at volume borders.")]
    [Range(1f, 10f)]
    public float sealMargin = 3f;

    [Header("═══ CAVE EDGE COLOR ═══")]
    [Tooltip("Width (m) for cave-edge fade in vertex color B channel.")]
    [Range(1f, 20f)]
    public float caveEdgeColorWidth = 5f;

    [Header("═══ RENDERING ═══")]
    public Material voxelMaterial;
    public Material voxelBakeGhostMaterial;

    [Header("═══ REFERENCES ═══")]
    [Tooltip("Bridge to MapMagic terrain for height sampling.")]
    public MapMagicBridge mapMagicBridge;

    [Header("═══ POOL ═══")]
    [Tooltip("Prefab for pooled voxel volume GameObjects.")]
    public GameObject voxelVolumePrefab;
    [Tooltip("Reusable native scratch slots reserved for streaming cave generation. Separate from flora pools.")]
    [SerializeField] private int streamingScratchSlotCount = 2;

    // ── Constants ──
    const float ABYSSAL_MAX_DEPTH = 5000f;
    const float TerrainVoxelSeamTransitionBand = VoxelSeamDirector.SeamTransitionBandMeters;
    const float ChthonicPillarRadiusMeters = 50f;
    const float ChthonicPillarHeightMeters = 1000f;
    const float ChthonicPillarEdgeWarpMeters = 24f;
    const float ChthonicPillarNoiseFrequency = 0.004f;
    const float ChthonicPillarMinimumProminenceMeters = 24f;
    const float ChthonicPillarTectonicBoundaryFrequencyFallback = 0.0065f;
    const uint ChthonicPillarTectonicBoundarySeedFallback = 83117u;
    const float ChthonicPillarMinimumTectonicBoundaryMask = 0.55f;
    const int ChthonicPillarColliderSegments = 24;
    const float CliffOverhangSlopeThreshold = 1.7320508f;
    const float CliffOverhangLateralAmplitudeMeters = 1.25f;
    const float CliffOverhangNoiseFrequency = 0.075f;
    const float CliffOverhangBlendStrength = 0.55f;
    const int JOB_BATCH = 64;
    const int ActiveVolumeRegistryCapacity = 64;
    const int NearestSonarSdfReadLeaseTrackerCapacity = 32;
    const int AirPocketRegistryCapacity = 64;
    const int MinimumStreamingSpawnPointScratchCapacity = 64;
    const int MinimumModifiedCellBucketScratchCapacity = 16;
    const int MaximumModifiedCellBucketScratchCapacity = 1 << 20;
    const int StreamingCaveGraphNodeScratchCapacity = 64;
    const int StreamingCaveGraphTunnelScratchCapacity = 128;
    const int StreamingCaveGraphEntranceScratchCapacity = 8;
    const int StreamingCaveGraphStructureScratchCapacity = 128;
    const int StreamingCraterStampScratchCapacity = 16;
    const int StreamingScratchGridMin = 16;
    const int StreamingScratchGridMax = 128;
    const int StreamingHeightScratchMax = 16641;
    const int StreamingPointScratchMax = 2146689;
    const int StreamingCellScratchMax = 2097152;
    const int StreamingEdgeVertexScratchMax = 2130048;
    const int StreamingSpawnPointScratchMax = StreamingCellScratchMax / 10;
    const int StreamingScratchSlotMax = 8;
    const double VoxelRebuildBudgetMilliseconds = 5.0d;
    const int VoxelRebuildBudgetStrikeFrames = 3;
    const uint VoxelRebuildLaneHash = 0x56584F4Cu;

    /// <summary>
    /// MC raw buffer multiplier. 2× totalCells instead of 15× (worst case).
    /// Atomic counter in MC job truncates gracefully if buffer fills.
    /// Saves ~85% peak memory allocation.
    /// </summary>
    const int MC_BUFFER_MULTIPLIER = 2;
    const int StreamingMeshRawVertexScratchLowTierCapacity = 262144;
    const int StreamingMeshRawVertexScratchMidTierCapacity = 524288;
    const int StreamingMeshRawVertexScratchVisualOverkillCapacity = 786432;
    const int StreamingSpatialBucketScratchCapacity = 512; // 8^3 max partition buckets.
    const int StreamingNodeSpatialReferenceScratchCapacity = StreamingCaveGraphNodeScratchCapacity * StreamingSpatialBucketScratchCapacity;
    const int StreamingTunnelSpatialReferenceScratchCapacity = StreamingCaveGraphTunnelScratchCapacity * StreamingSpatialBucketScratchCapacity;
    const int StreamingColliderChunkScratchCapacity = 8;
    const int StreamingScratchVaultBufferBase = 76500;
    const int StreamingScratchVaultBufferStride = 60;
    const int ScratchLaneTerrainHeights = 0;
    const int ScratchLaneGridBiome = 1;
    const int ScratchLaneDensityField = 2;
    const int ScratchLaneSmoothDensityField = 3;
    const int ScratchLaneOverhangDensityField = 4;
    const int ScratchLaneQuantizedDensityField = 5;
    const int ScratchLaneAnomalyFeatureRecords = 6;
    const int ScratchLaneAnomalyFissureMask = 7;
    const int ScratchLaneSelectedPillarFeature = 8;
    const int ScratchLaneChunkContentFlags = 9;
    const int ScratchLaneCellVertexCounts = 10;
    const int ScratchLaneCellVertexOffsets = 11;
    const int ScratchLaneMeshRawVertices = 12;
    const int ScratchLaneMeshWeldedPositions = 13;
    const int ScratchLaneMeshTriangleIndices = 14;
    const int ScratchLaneMeshEdgeVertexX = 15;
    const int ScratchLaneMeshEdgeVertexY = 16;
    const int ScratchLaneMeshEdgeVertexZ = 17;
    const int ScratchLaneMeshWeldedCounter = 18;
    const int ScratchLaneMeshSurfaceVertices = 55;
    const int ScratchLaneMeshNormals = 19;
    const int ScratchLaneMeshCurvatureValues = 20;
    const int ScratchLaneMeshAmbientOcclusionValues = 21;
    const int ScratchLaneMeshBiomeValues = 22;
    const int ScratchLaneMeshSkirtAlphaValues = 23;
    const int ScratchLaneMeshDirtyBlendValues = 24;
    const int ScratchLaneMeshColors = 25;
    const int ScratchLaneProjectedLocalPositions = 26;
    const int ScratchLaneSpatialBucketCounts = 27;
    const int ScratchLaneSpatialBucketWriteHeads = 28;
    const int ScratchLaneSpatialNodeBucketOffsets = 29;
    const int ScratchLaneSpatialNodeBucketIndices = 30;
    const int ScratchLaneSpatialTunnelBucketOffsets = 31;
    const int ScratchLaneSpatialTunnelBucketIndices = 32;
    const int ScratchLaneRebuildNodes = 33;
    const int ScratchLaneRebuildTunnels = 34;
    const int ScratchLaneRebuildEntrances = 35;
    const int ScratchLaneRebuildStructures = 36;
    const int ScratchLaneRebuildCraterStamps = 37;
    const int ScratchLaneSpawnPointList = 38;
    const int ScratchLaneSpawnPointCount = 39;
    const int ScratchLaneModifiedCells = 40;
    const int ScratchLaneModifiedCellCount = 41;
    const int ScratchLaneModifiedCellBucketHeads = 51;
    const int ScratchLaneModifiedCellNext = 52;
    const int ScratchLaneJobLifetimeFence = 53;
    const int ScratchLaneDensityFaultFlags = 54;
    const int ScratchLaneColliderTriangleBuckets = 42;
    const int ScratchLaneColliderBucketCounts = 43;
    const int ScratchLaneColliderBucketOffsets = 44;
    const int ScratchLaneColliderBucketWriteHeads = 45;
    const int ScratchLaneColliderChunkTriangleIndices = 46;
    const int ScratchLaneColliderLocalRemap = 47;
    const int ScratchLaneColliderTouchedVertexGlobals = 48;
    const int ScratchLaneColliderLocalPositions = 49;
    const int ScratchLaneColliderLocalIndices = 50;

    // ── Internal ──
    static int _liveEngineCount;
    static int _activeGenerationOperations;
    static int _shutdownRequested;
    static int _voxelRebuildOverBudgetConsecutive;
    static HectonVoxelEngine s_activeRuntimeInstance;
    static IPlayerRuntimeContext s_playerRuntimeContext;
    internal static HectonVoxelEngine ActiveRuntimeInstance => s_activeRuntimeInstance;
    private static int _airPocketCount;
    private static FixedList4096Bytes<AirPocketEntry> _airPocketEntries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticRuntimeState()
    {
        FlushDeferredVoxelWorkWithoutDispatcher();
        _liveEngineCount = 0;
        _activeGenerationOperations = 0;
        _shutdownRequested = 0;
        _voxelRebuildOverBudgetConsecutive = 0;
        s_activeRuntimeInstance = null;
        s_playerRuntimeContext = null;
        ClearAirPocketRegistry();
        _deferredVoxelPhysicsBakeTeardowns.Clear();
        ClearDeferredVoxelPhysicsBakeEmergencyTeardowns();
        _deferredVoxelColliderUploads.Clear();
        _deferredVoxelPhysicsBakeTeardownRegistered = false;
        _deferredVoxelColliderUploadRegistered = false;
        _deferredVoxelPhysicsBakeBackpressureActive = false;
        _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
        _deferredVoxelColliderUploadScanCursor = -1;
        _deferredVoxelPhysicsBakeTeardownFrame = -1;
        _deferredVoxelPhysicsBakeTeardownBudgetTokens = 0f;
        _voxelColliderUploadDropWarningArmed = false;
        _voxelColliderUploadRetryDropWarningArmed = false;
        _voxelProxyLayerFilteringConfigured = false;
        _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
        _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
        _voxelMeshTelemetryFrame = -1;
        _voxelChunksMeshedThisFrame = 0;
        _voxelMeshPipelineBlackBoxDumped = false;
        DisposeVoxelMeshPipelineBlackBox();
        ResetPredictiveVoxelProxyCinematicState();
        ResetVoxelProxyLayerFilteringState();
        ResetVoxelMeshPoolState();
    }
    // COLD ALLOC: List<DeferredVoxelPhysicsBakeTeardown>[2048] - deferred voxel collider PhysX bake teardown queue - owner: HectonVoxelEngine
    private static readonly List<DeferredVoxelPhysicsBakeTeardown> _deferredVoxelPhysicsBakeTeardowns = new List<DeferredVoxelPhysicsBakeTeardown>(DeferredVoxelPhysicsBakeTeardownCapacity);
    // COLD ALLOC: DeferredVoxelPhysicsBakeTeardown[512] - fail-closed overflow lane for already-scheduled PhysX bake jobs - owner: HectonVoxelEngine
    private static readonly DeferredVoxelPhysicsBakeTeardown[] _deferredVoxelPhysicsBakeEmergencyTeardowns = new DeferredVoxelPhysicsBakeTeardown[DeferredVoxelPhysicsBakeEmergencyTeardownCapacity];
    private static int _deferredVoxelPhysicsBakeEmergencyCount;
    private static int _deferredVoxelPhysicsBakeEmergencyScanCursor;
    // COLD ALLOC: List<DeferredVoxelColliderUpload>[2048] - late-frame PhysX collider sharedMesh upload queue - owner: HectonVoxelEngine
    private static readonly List<DeferredVoxelColliderUpload> _deferredVoxelColliderUploads = new List<DeferredVoxelColliderUpload>(DeferredVoxelColliderUploadCapacity);
    // COLD ALLOC: Mesh[256] - global voxel surface mesh pool preallocated at engine boot - owner: HectonVoxelEngine
    private static readonly Mesh[] _voxelSurfaceMeshPool = new Mesh[VoxelSurfaceMeshPoolSize];
    private static FixedList4096Bytes<byte> _voxelSurfaceMeshPoolInUse;
    private static int _voxelSurfaceMeshPoolInUseCount;
    // COLD ALLOC: Mesh[256] - global PhysX voxel bake mesh pool - owner: HectonVoxelEngine
    private static readonly Mesh[] _voxelPhysicsBakeMeshPool = new Mesh[VoxelPhysicsBakeMeshPoolSize];
    private static FixedList4096Bytes<byte> _voxelPhysicsBakeMeshPoolInUse;
    private static int _voxelPhysicsBakeMeshPoolInUseCount;
    private static int _voxelMeshPoolOccupancyInitGate;
    private static int _voxelMeshPoolOccupancyInitialized;
    // COLD ALLOC: DeferredVoxelPhysicsBakeTeardownDriver[1] - dispatcher late-frame adapter for voxel bake teardown - owner: HectonVoxelEngine
    private static readonly DeferredVoxelPhysicsBakeTeardownDriver _deferredVoxelPhysicsBakeTeardownDriver = new DeferredVoxelPhysicsBakeTeardownDriver();
    // COLD ALLOC: DeferredVoxelColliderUploadDriver[1] - dispatcher late-frame adapter for collider mesh assignment - owner: HectonVoxelEngine
    private static readonly DeferredVoxelColliderUploadDriver _deferredVoxelColliderUploadDriver = new DeferredVoxelColliderUploadDriver();
    // COLD ALLOC: DeferredVoxelDispatcherHotSwapBridge[1] - rebinds static voxel late-frame drivers after Dispatcher replacement - owner: HectonVoxelEngine
    private static readonly DeferredVoxelDispatcherHotSwapBridge _deferredVoxelDispatcherHotSwapBridge = new DeferredVoxelDispatcherHotSwapBridge();
    private static bool _deferredVoxelPhysicsBakeTeardownRegistered;
    private static bool _deferredVoxelColliderUploadRegistered;
    private static bool _deferredVoxelHotSwapRegistered;
    private static bool _deferredVoxelPhysicsBakeBackpressureActive;
    private static int _deferredVoxelPhysicsBakeTeardownScanCursor;
    private static int _deferredVoxelColliderUploadScanCursor = -1;
    private static int _predictiveVoxelProxyLastFrame = -1;
    private static bool _voxelProxyLayerFilteringConfigured;
    private static bool _voxelSurfaceMeshPoolExhaustedWarningArmed;
    private static bool _voxelPhysicsBakeMeshPoolExhaustedWarningArmed;
    private static int DeferredVoxelPhysicsBakePendingCount =>
        _deferredVoxelPhysicsBakeTeardowns.Count + _deferredVoxelPhysicsBakeEmergencyCount;

    private struct DeferredVoxelPhysicsBakeTeardown
    {
        public Mesh Mesh;
        public GameObject Owner;
        public MeshRenderer Renderer;
        public MeshCollider Collider;
        public BoxCollider ProxyCollider;
        public JobHandle Handle;
        public double3 ProxyMinAup;
        public double3 ProxyMaxAup;
        public uint ProxyShiftSequence;
        public byte Flags;
        public byte HasProxyBounds;
    }

    private struct DeferredVoxelColliderUpload
    {
        public Hecton8.Caves.HectonVoxelVolume Volume;
        public MeshCollider Collider;
        public BoxCollider ProxyCollider;
        public Mesh Mesh;
        public double3 ProxyMinAup;
        public double3 ProxyMaxAup;
        public uint ProxyShiftSequence;
        public int ChunkIndex;
        public byte Flags;
        public byte HasProxyBounds;
        public byte RetryCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct AirPocketEntry
    {
        [FieldOffset(0)] public float3 Center;
        [FieldOffset(12)] public float3 HalfExtents;
        [FieldOffset(24)] public float OxygenRefillFraction;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct ActiveVolumeLocalBoundsEntry
    {
        [FieldOffset(0)] public float3 Center;
        [FieldOffset(12)] public float3 Size;

        public static ActiveVolumeLocalBoundsEntry FromBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            return new ActiveVolumeLocalBoundsEntry
            {
                Center = new float3(center.x, center.y, center.z),
                Size = new float3(size.x, size.y, size.z)
            };
        }

        public Bounds ToBounds()
        {
            return new Bounds(
                new Vector3(Center.x, Center.y, Center.z),
                new Vector3(Size.x, Size.y, Size.z));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct VoxelMeshPipelineTelemetryEntry
    {
        [FieldOffset(0)]
        public long TimestampTicks;

        [FieldOffset(8)]
        public uint Frame;

        [FieldOffset(12)]
        public uint Flags;

        [FieldOffset(16)]
        public uint BufferId;

        [FieldOffset(20)]
        public uint SystemId;

        [FieldOffset(24)]
        public uint Generation;

        [FieldOffset(28)]
        public uint StateHash;

        [FieldOffset(32)]
        public uint VaultGenerationId;

        [FieldOffset(36)]
        public ushort ChunksMeshedThisFrame;

        [FieldOffset(38)]
        public ushort BakeQueueLength;

        [FieldOffset(40)]
        public ushort ColliderUploadQueueLength;

        [FieldOffset(42)]
        public ushort ActiveGenerationOperations;

        [FieldOffset(44)]
        public ushort SurfacePoolInUse;

        [FieldOffset(46)]
        public ushort PhysicsPoolInUse;

        [FieldOffset(48)]
        public uint Padding0;

        [FieldOffset(52)]
        public uint Padding1;

        [FieldOffset(56)]
        public uint Padding2;

        [FieldOffset(60)]
        public uint Padding3;
    }

    private sealed class DeferredVoxelPhysicsBakeTeardownDriver : ILateFrameTickable
    {
        public void LateFrameTick()
        {
            ApplyPredictiveVoxelProxyCinematicGate();
            PublishVoxelMeshPipelineTelemetry();
            DrainDeferredVoxelPhysicsBakeTeardowns();
        }
    }

    private sealed class DeferredVoxelColliderUploadDriver : ILateFrameTickable
    {
        public void LateFrameTick()
        {
            ApplyPredictiveVoxelProxyCinematicGate();
            PublishVoxelMeshPipelineTelemetry();
            DrainDeferredVoxelColliderUploads();
        }
    }

    private sealed class DeferredVoxelDispatcherHotSwapBridge : IGlobalRegistryHotSwapListener
    {
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            RebindDeferredVoxelLateFrameDrivers();
        }
    }

    internal static int RegisterAirPocket(Vector3 centerWS, Vector3 halfExtentsWS, float oxygenRefillFraction = 1f)
    {
        if (_airPocketCount < 0 ||
            _airPocketCount != _airPocketEntries.Length ||
            _airPocketEntries.Length > AirPocketRegistryCapacity)
        {
            RecordVoxelRegistryCorruptionForAgent1304(_airPocketCount, _airPocketEntries.Length, AirPocketRegistryCapacity);
            ClearAirPocketRegistry();
        }

        if (_airPocketEntries.Length >= AirPocketRegistryCapacity ||
            _airPocketEntries.Length >= _airPocketEntries.Capacity ||
            !IsFiniteVector(centerWS) ||
            !IsFiniteVector(halfExtentsWS))
        {
            return 0;
        }

        Vector3 safeExtents = new Vector3(
            math.max(0.01f, math.abs(halfExtentsWS.x)),
            math.max(0.01f, math.abs(halfExtentsWS.y)),
            math.max(0.01f, math.abs(halfExtentsWS.z)));
        int slot = _airPocketEntries.Length;
        _airPocketEntries.AddNoResize(new AirPocketEntry
        {
            Center = new float3(centerWS.x, centerWS.y, centerWS.z),
            HalfExtents = new float3(safeExtents.x, safeExtents.y, safeExtents.z),
            OxygenRefillFraction = math.saturate(oxygenRefillFraction)
        });
        _airPocketCount = _airPocketEntries.Length;
        return slot + 1;
    }

    internal static void UnregisterAirPocket(int handle)
    {
        if (_airPocketCount < 0 ||
            _airPocketCount != _airPocketEntries.Length ||
            _airPocketEntries.Length > AirPocketRegistryCapacity)
        {
            RecordVoxelRegistryCorruptionForAgent1304(_airPocketCount, _airPocketEntries.Length, AirPocketRegistryCapacity);
            ClearAirPocketRegistry();
            return;
        }

        int slot = handle - 1;
        if ((uint)slot >= (uint)_airPocketEntries.Length)
            return;

        _airPocketEntries.RemoveAtSwapBack(slot);
        _airPocketCount = _airPocketEntries.Length;
    }

    internal static void ClearAirPocketRegistry()
    {
        _airPocketEntries.Clear();
        _airPocketCount = 0;
    }

    internal static bool TrySampleAirPocket(Vector3 worldPosition, out float oxygenRefillFraction)
    {
        oxygenRefillFraction = 0f;
        if (!IsFiniteVector(worldPosition))
            return false;

        int airPocketCount = _airPocketEntries.Length < AirPocketRegistryCapacity
            ? _airPocketEntries.Length
            : AirPocketRegistryCapacity;

        for (int i = 0; i < airPocketCount; i++)
        {
            AirPocketEntry entry = _airPocketEntries[i];
            float3 center = entry.Center;
            float3 extents = entry.HalfExtents;
            if (math.abs(worldPosition.x - center.x) > extents.x ||
                math.abs(worldPosition.y - center.y) > extents.y ||
                math.abs(worldPosition.z - center.z) > extents.z)
            {
                continue;
            }

            oxygenRefillFraction = math.max(0.01f, entry.OxygenRefillFraction);
            return true;
        }

        return false;
    }

    internal static bool TryFlagAirPocketFromCeilingConcavity(
        Vector3 centerWS,
        Vector3 halfExtentsWS,
        float ceilingNormalY,
        float sealedVolume01,
        float waterlineClearanceMeters,
        float oxygenRefillFraction,
        out int handle)
    {
        handle = 0;
        if (!IsCeilingConcavityAirPocketCandidate(ceilingNormalY, sealedVolume01, waterlineClearanceMeters))
            return false;

        handle = RegisterAirPocket(centerWS, halfExtentsWS, oxygenRefillFraction);
        return handle != 0;
    }

    internal static bool IsCeilingConcavityAirPocketCandidate(float ceilingNormalY, float sealedVolume01, float waterlineClearanceMeters)
    {
        return math.isfinite(ceilingNormalY) &&
               math.isfinite(sealedVolume01) &&
               math.isfinite(waterlineClearanceMeters) &&
               ceilingNormalY <= -0.55f &&
               sealedVolume01 >= 0.65f &&
               waterlineClearanceMeters >= 0.35f;
    }

    private static bool IsFiniteVector(Vector3 value)
    {
        return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
    }

    private static bool IsFiniteFloat3(float3 value)
    {
        return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
    }

    private static bool IsFiniteDouble3(double3 value)
    {
        return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
    }

    private static bool IsFiniteColor(Color value)
    {
        return math.isfinite(value.r) &&
               math.isfinite(value.g) &&
               math.isfinite(value.b) &&
               math.isfinite(value.a);
    }

    private static float3 NormalizeFiniteOrUp(float3 value, ref bool invalidMeshData)
    {
        if (!IsFiniteFloat3(value))
        {
            invalidMeshData = true;
            return new float3(0f, 1f, 0f);
        }

        float lengthSq = math.lengthsq(value);
        if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
        {
            invalidMeshData = true;
            return new float3(0f, 1f, 0f);
        }

        return value * math.rsqrt(lengthSq);
    }

    private static float SanitizeFinite01(float value, float fallback, ref bool invalidMeshData)
    {
        if (math.isfinite(value))
            return math.saturate(value);

        invalidMeshData = true;
        return fallback;
    }

    private static Color SanitizeFiniteColor(Color value, ref bool invalidMeshData)
    {
        if (IsFiniteColor(value))
            return value;

        invalidMeshData = true;
        return Color.white;
    }

    // COLD ALLOC: List<GameObject>[64] - active voxel volume object registry - owner: HectonVoxelEngine
    readonly List<GameObject> _activeVolumes = new List<GameObject>(ActiveVolumeRegistryCapacity);
    // COLD ALLOC: List<HectonVoxelVolume>[64] - active voxel volume component registry - owner: HectonVoxelEngine
    readonly List<HectonVoxelVolume> _activeVolumeComponents = new List<HectonVoxelVolume>(ActiveVolumeRegistryCapacity);
    // COLD ALLOC: IDataVault[32] - exact public SDF read lease vault route tracker - owner: HectonVoxelEngine
    readonly IDataVault[] _nearestSonarSdfReadLeaseVaults = new IDataVault[NearestSonarSdfReadLeaseTrackerCapacity];
    // COLD ALLOC: uint[32] - public SDF read lease generation tracker - owner: HectonVoxelEngine
    readonly uint[] _nearestSonarSdfReadLeaseGenerations = new uint[NearestSonarSdfReadLeaseTrackerCapacity];
    // COLD ALLOC: int[32] - public SDF read lease version tracker - owner: HectonVoxelEngine
    readonly int[] _nearestSonarSdfReadLeaseVersions = new int[NearestSonarSdfReadLeaseTrackerCapacity];
    // COLD ALLOC: ushort[32] - public SDF read lease ref-count tracker - owner: HectonVoxelEngine
    readonly ushort[] _nearestSonarSdfReadLeaseRefCounts = new ushort[NearestSonarSdfReadLeaseTrackerCapacity];
    FixedList4096Bytes<ActiveVolumeLocalBoundsEntry> _activeVolumeLocalBounds;
    int _streamingScratchGate;
    bool _registeredLiveEngine;
    bool _teardownStreamingScratchRequested;
    bool _registeredRuntimeServiceHotSwapListener;
    IObjectPoolService _objectPoolService;
    IPhysicsService _physicsService;
    IVramPressureReadModel _vramPressureReadModel;
    LODSystemManager _lodSystemManager;
    ScavengePopulator _scavengePopulator;
    HectonMapMagicVegetationBridge _vegetationBridge;
    ResourceDistributionDirector _resourceDistributionDirector;
    IDataVault _streamingScratchVault;
    IDataVault _pendingStreamingScratchVault;
    VoxelStreamingScratchSlot[] _streamingScratchSlots;
    [SerializeField] VoxelDeltaProcessor _deltaProcessor;

    internal VoxelDeltaProcessor DeltaProcessor => _deltaProcessor;
    internal Material ResolvedVoxelBakeGhostMaterial => voxelBakeGhostMaterial;

    StreamingScratchGateScope EnterStreamingScratchGate()
    {
        SpinWait spinWait = default;
        while (Interlocked.CompareExchange(ref _streamingScratchGate, 1, 0) != 0)
            spinWait.SpinOnce();

        return new StreamingScratchGateScope(this);
    }

    void ExitStreamingScratchGate()
    {
        Volatile.Write(ref _streamingScratchGate, 0);
    }

    readonly struct StreamingScratchGateScope : IDisposable
    {
        readonly HectonVoxelEngine _owner;

        public StreamingScratchGateScope(HectonVoxelEngine owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner.ExitStreamingScratchGate();
        }
    }

    sealed class VoxelStreamingScratchSlot
    {
        public VaultGenerationHandle<float> TerrainHeightsHandle; public int TerrainHeightsCapacity;
        public VaultGenerationHandle<float> GridBiomeHandle; public int GridBiomeCapacity;
        public VaultGenerationHandle<float> DensityFieldHandle; public int DensityFieldCapacity;
        public VaultGenerationHandle<float> SmoothDensityFieldHandle; public int SmoothDensityFieldCapacity;
        public VaultGenerationHandle<float> OverhangDensityFieldHandle; public int OverhangDensityFieldCapacity;
        public VaultGenerationHandle<sbyte> QuantizedDensityFieldHandle; public int QuantizedDensityFieldCapacity;
        public VaultGenerationHandle<AnomalyFeatureRecord> AnomalyFeatureRecordsHandle; public int AnomalyFeatureRecordsCapacity;
        public VaultGenerationHandle<byte> AnomalyFissureMaskHandle; public int AnomalyFissureMaskCapacity;
        public VaultGenerationHandle<AnomalyFeatureRecord> SelectedPillarFeatureHandle; public int SelectedPillarFeatureCapacity;
        public VaultGenerationHandle<int> ChunkContentFlagsHandle; public int ChunkContentFlagsCapacity;
        public VaultGenerationHandle<int> DensityFaultFlagsHandle; public int DensityFaultFlagsCapacity;
        public VaultGenerationHandle<int> CellVertexCountsHandle; public int CellVertexCountsCapacity;
        public VaultGenerationHandle<int> CellVertexOffsetsHandle; public int CellVertexOffsetsCapacity;
        public VaultGenerationHandle<MCRawVertex> MeshRawVerticesHandle; public int MeshRawVerticesCapacity;
        public VaultGenerationHandle<float3> MeshWeldedPositionsHandle; public int MeshWeldedPositionsCapacity;
        public VaultGenerationHandle<int> MeshTriangleIndicesHandle; public int MeshTriangleIndicesCapacity;
        public VaultGenerationHandle<int> MeshEdgeVertexXHandle; public int MeshEdgeVertexXCapacity;
        public VaultGenerationHandle<int> MeshEdgeVertexYHandle; public int MeshEdgeVertexYCapacity;
        public VaultGenerationHandle<int> MeshEdgeVertexZHandle; public int MeshEdgeVertexZCapacity;
        public VaultGenerationHandle<int> MeshWeldedCounterHandle; public int MeshWeldedCounterCapacity;
        public VaultGenerationHandle<VoxelSurfaceVertex> MeshSurfaceVerticesHandle; public int MeshSurfaceVerticesCapacity;
        public VaultGenerationHandle<float3> MeshNormalsHandle; public int MeshNormalsCapacity;
        public VaultGenerationHandle<float> MeshCurvatureValuesHandle; public int MeshCurvatureValuesCapacity;
        public VaultGenerationHandle<float> MeshAmbientOcclusionValuesHandle; public int MeshAmbientOcclusionValuesCapacity;
        public VaultGenerationHandle<float> MeshBiomeValuesHandle; public int MeshBiomeValuesCapacity;
        public VaultGenerationHandle<float> MeshSkirtAlphaValuesHandle; public int MeshSkirtAlphaValuesCapacity;
        public VaultGenerationHandle<float> MeshDirtyBlendValuesHandle; public int MeshDirtyBlendValuesCapacity;
        public VaultGenerationHandle<Color32> MeshColorsHandle; public int MeshColorsCapacity;
        public VaultGenerationHandle<float3> ProjectedLocalPositionsHandle; public int ProjectedLocalPositionsCapacity;
        public VaultGenerationHandle<int> SpatialBucketCountsHandle; public int SpatialBucketCountsCapacity;
        public VaultGenerationHandle<int> SpatialBucketWriteHeadsHandle; public int SpatialBucketWriteHeadsCapacity;
        public VaultGenerationHandle<int> SpatialNodeBucketOffsetsHandle; public int SpatialNodeBucketOffsetsCapacity;
        public VaultGenerationHandle<int> SpatialNodeBucketIndicesHandle; public int SpatialNodeBucketIndicesCapacity;
        public VaultGenerationHandle<int> SpatialTunnelBucketOffsetsHandle; public int SpatialTunnelBucketOffsetsCapacity;
        public VaultGenerationHandle<int> SpatialTunnelBucketIndicesHandle; public int SpatialTunnelBucketIndicesCapacity;
        public VaultGenerationHandle<CaveNode> RebuildNodesHandle; public int RebuildNodesCapacity;
        public VaultGenerationHandle<CaveTunnel> RebuildTunnelsHandle; public int RebuildTunnelsCapacity;
        public VaultGenerationHandle<CaveEntrance> RebuildEntrancesHandle; public int RebuildEntrancesCapacity;
        public VaultGenerationHandle<CaveStructure> RebuildStructuresHandle; public int RebuildStructuresCapacity;
        public VaultGenerationHandle<VoxelCraterStamp> RebuildCraterStampsHandle; public int RebuildCraterStampsCapacity;
        public VaultGenerationHandle<CaveSpawnData> SpawnPointListScratchHandle; public int SpawnPointListScratchCapacity;
        public VaultGenerationHandle<int> SpawnPointCountHandle; public int SpawnPointCountCapacity;
        public VaultGenerationHandle<VoxelModifiedCellEntry> ModifiedCellsScratchHandle; public int ModifiedCellsScratchCapacity;
        public VaultGenerationHandle<int> ModifiedCellCountHandle; public int ModifiedCellCountCapacity;
        public VaultGenerationHandle<int> ModifiedCellBucketHeadsHandle; public int ModifiedCellBucketHeadsCapacity;
        public VaultGenerationHandle<int> ModifiedCellNextHandle; public int ModifiedCellNextCapacity;
        public VaultGenerationHandle<byte> JobLifetimeFenceHandle; public int JobLifetimeFenceCapacity;
        public VaultGenerationHandle<byte> ColliderTriangleBucketsHandle; public int ColliderTriangleBucketsCapacity;
        public VaultGenerationHandle<int> ColliderBucketCountsHandle; public int ColliderBucketCountsCapacity;
        public VaultGenerationHandle<int> ColliderBucketOffsetsHandle; public int ColliderBucketOffsetsCapacity;
        public VaultGenerationHandle<int> ColliderBucketWriteHeadsHandle; public int ColliderBucketWriteHeadsCapacity;
        public VaultGenerationHandle<int> ColliderChunkTriangleIndicesHandle; public int ColliderChunkTriangleIndicesCapacity;
        public VaultGenerationHandle<int> ColliderLocalRemapHandle; public int ColliderLocalRemapCapacity;
        public VaultGenerationHandle<int> ColliderTouchedVertexGlobalsHandle; public int ColliderTouchedVertexGlobalsCapacity;
        public VaultGenerationHandle<float3> ColliderLocalPositionsHandle; public int ColliderLocalPositionsCapacity;
        public VaultGenerationHandle<int> ColliderLocalIndicesHandle; public int ColliderLocalIndicesCapacity;
        public IDataVault Vault;
        public bool InUse;

        public void Dispose(IDataVault vault)
        {
            IDataVault releaseVault = Vault != null ? Vault : vault;
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref TerrainHeightsHandle, ref TerrainHeightsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref GridBiomeHandle, ref GridBiomeCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref DensityFieldHandle, ref DensityFieldCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SmoothDensityFieldHandle, ref SmoothDensityFieldCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref OverhangDensityFieldHandle, ref OverhangDensityFieldCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref QuantizedDensityFieldHandle, ref QuantizedDensityFieldCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref AnomalyFeatureRecordsHandle, ref AnomalyFeatureRecordsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref AnomalyFissureMaskHandle, ref AnomalyFissureMaskCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SelectedPillarFeatureHandle, ref SelectedPillarFeatureCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ChunkContentFlagsHandle, ref ChunkContentFlagsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref DensityFaultFlagsHandle, ref DensityFaultFlagsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref CellVertexCountsHandle, ref CellVertexCountsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref CellVertexOffsetsHandle, ref CellVertexOffsetsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshRawVerticesHandle, ref MeshRawVerticesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshWeldedPositionsHandle, ref MeshWeldedPositionsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshTriangleIndicesHandle, ref MeshTriangleIndicesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshEdgeVertexXHandle, ref MeshEdgeVertexXCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshEdgeVertexYHandle, ref MeshEdgeVertexYCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshEdgeVertexZHandle, ref MeshEdgeVertexZCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshWeldedCounterHandle, ref MeshWeldedCounterCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshSurfaceVerticesHandle, ref MeshSurfaceVerticesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshNormalsHandle, ref MeshNormalsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshCurvatureValuesHandle, ref MeshCurvatureValuesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshAmbientOcclusionValuesHandle, ref MeshAmbientOcclusionValuesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshBiomeValuesHandle, ref MeshBiomeValuesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshSkirtAlphaValuesHandle, ref MeshSkirtAlphaValuesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshDirtyBlendValuesHandle, ref MeshDirtyBlendValuesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref MeshColorsHandle, ref MeshColorsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ProjectedLocalPositionsHandle, ref ProjectedLocalPositionsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpatialBucketCountsHandle, ref SpatialBucketCountsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpatialBucketWriteHeadsHandle, ref SpatialBucketWriteHeadsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpatialNodeBucketOffsetsHandle, ref SpatialNodeBucketOffsetsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpatialNodeBucketIndicesHandle, ref SpatialNodeBucketIndicesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpatialTunnelBucketOffsetsHandle, ref SpatialTunnelBucketOffsetsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpatialTunnelBucketIndicesHandle, ref SpatialTunnelBucketIndicesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref RebuildNodesHandle, ref RebuildNodesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref RebuildTunnelsHandle, ref RebuildTunnelsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref RebuildEntrancesHandle, ref RebuildEntrancesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref RebuildStructuresHandle, ref RebuildStructuresCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref RebuildCraterStampsHandle, ref RebuildCraterStampsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpawnPointListScratchHandle, ref SpawnPointListScratchCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref SpawnPointCountHandle, ref SpawnPointCountCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ModifiedCellsScratchHandle, ref ModifiedCellsScratchCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ModifiedCellCountHandle, ref ModifiedCellCountCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ModifiedCellBucketHeadsHandle, ref ModifiedCellBucketHeadsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ModifiedCellNextHandle, ref ModifiedCellNextCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref JobLifetimeFenceHandle, ref JobLifetimeFenceCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderTriangleBucketsHandle, ref ColliderTriangleBucketsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderBucketCountsHandle, ref ColliderBucketCountsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderBucketOffsetsHandle, ref ColliderBucketOffsetsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderBucketWriteHeadsHandle, ref ColliderBucketWriteHeadsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderChunkTriangleIndicesHandle, ref ColliderChunkTriangleIndicesCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderLocalRemapHandle, ref ColliderLocalRemapCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderTouchedVertexGlobalsHandle, ref ColliderTouchedVertexGlobalsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderLocalPositionsHandle, ref ColliderLocalPositionsCapacity);
            HectonVoxelEngine.ReleaseStreamingScratchHandle(releaseVault, ref ColliderLocalIndicesHandle, ref ColliderLocalIndicesCapacity);
            Vault = null;
            InUse = false;
        }
    }

    struct VoxelStreamingScratchLease : System.IDisposable
    {
        internal HectonVoxelEngine _owner;
        internal int _slotIndex;
        internal ulong _lockedScratchMutationGuardMask;
        internal IDataVault _lockedScratchVault;
        internal byte _scratchBuffersLocked;

        public bool IsValid => _owner != null && _slotIndex >= 0;

        public VoxelStreamingScratchLease(HectonVoxelEngine owner, int slotIndex)
        {
            _owner = owner;
            _slotIndex = slotIndex;
            _lockedScratchMutationGuardMask = 0UL;
            _lockedScratchVault = null;
            _scratchBuffersLocked = 0;
        }

        VoxelStreamingScratchSlot Slot => _owner != null &&
                                          _owner._streamingScratchSlots != null &&
                                          _slotIndex >= 0 &&
                                          _slotIndex < _owner._streamingScratchSlots.Length
            ? _owner._streamingScratchSlots[_slotIndex]
            : null;

        NativeArray<T> Resolve<T>(in VaultGenerationHandle<T> handle, int requiredLength) where T : struct
        {
            VoxelStreamingScratchSlot slot = Slot;
            return _owner != null &&
                   slot != null &&
                   _owner.TryResolveStreamingScratchArray(slot.Vault, in handle, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        public NativeArray<float> TerrainHeights => Slot != null ? Resolve(in Slot.TerrainHeightsHandle, Slot.TerrainHeightsCapacity) : default;
        public NativeArray<float> GridBiome => Slot != null ? Resolve(in Slot.GridBiomeHandle, Slot.GridBiomeCapacity) : default;
        public NativeArray<float> DensityField => Slot != null ? Resolve(in Slot.DensityFieldHandle, Slot.DensityFieldCapacity) : default;
        public NativeArray<float> SmoothDensityField => Slot != null ? Resolve(in Slot.SmoothDensityFieldHandle, Slot.SmoothDensityFieldCapacity) : default;
        public NativeArray<float> OverhangDensityField => Slot != null ? Resolve(in Slot.OverhangDensityFieldHandle, Slot.OverhangDensityFieldCapacity) : default;
        public NativeArray<sbyte> QuantizedDensityField => Slot != null ? Resolve(in Slot.QuantizedDensityFieldHandle, Slot.QuantizedDensityFieldCapacity) : default;
        public NativeArray<AnomalyFeatureRecord> AnomalyFeatureRecords => Slot != null ? Resolve(in Slot.AnomalyFeatureRecordsHandle, Slot.AnomalyFeatureRecordsCapacity) : default;
        public NativeArray<byte> AnomalyFissureMask => Slot != null ? Resolve(in Slot.AnomalyFissureMaskHandle, Slot.AnomalyFissureMaskCapacity) : default;
        public NativeArray<AnomalyFeatureRecord> SelectedPillarFeature => Slot != null ? Resolve(in Slot.SelectedPillarFeatureHandle, Slot.SelectedPillarFeatureCapacity) : default;
        public NativeArray<int> ChunkContentFlags => Slot != null ? Resolve(in Slot.ChunkContentFlagsHandle, Slot.ChunkContentFlagsCapacity) : default;
        public NativeArray<int> DensityFaultFlags => Slot != null ? Resolve(in Slot.DensityFaultFlagsHandle, Slot.DensityFaultFlagsCapacity) : default;
        public NativeArray<int> CellVertexCounts => Slot != null ? Resolve(in Slot.CellVertexCountsHandle, Slot.CellVertexCountsCapacity) : default;
        public NativeArray<int> CellVertexOffsets => Slot != null ? Resolve(in Slot.CellVertexOffsetsHandle, Slot.CellVertexOffsetsCapacity) : default;
        public NativeArray<MCRawVertex> MeshRawVertices => Slot != null ? Resolve(in Slot.MeshRawVerticesHandle, Slot.MeshRawVerticesCapacity) : default;
        public NativeArray<float3> MeshWeldedPositions => Slot != null ? Resolve(in Slot.MeshWeldedPositionsHandle, Slot.MeshWeldedPositionsCapacity) : default;
        public NativeArray<int> MeshTriangleIndices => Slot != null ? Resolve(in Slot.MeshTriangleIndicesHandle, Slot.MeshTriangleIndicesCapacity) : default;
        public NativeArray<int> MeshEdgeVertexX => Slot != null ? Resolve(in Slot.MeshEdgeVertexXHandle, Slot.MeshEdgeVertexXCapacity) : default;
        public NativeArray<int> MeshEdgeVertexY => Slot != null ? Resolve(in Slot.MeshEdgeVertexYHandle, Slot.MeshEdgeVertexYCapacity) : default;
        public NativeArray<int> MeshEdgeVertexZ => Slot != null ? Resolve(in Slot.MeshEdgeVertexZHandle, Slot.MeshEdgeVertexZCapacity) : default;
        public NativeArray<int> MeshWeldedCounter => Slot != null ? Resolve(in Slot.MeshWeldedCounterHandle, Slot.MeshWeldedCounterCapacity) : default;
        public NativeArray<VoxelSurfaceVertex> MeshSurfaceVertices => Slot != null ? Resolve(in Slot.MeshSurfaceVerticesHandle, Slot.MeshSurfaceVerticesCapacity) : default;
        public NativeArray<float3> MeshNormals => Slot != null ? Resolve(in Slot.MeshNormalsHandle, Slot.MeshNormalsCapacity) : default;
        public NativeArray<float> MeshCurvatureValues => Slot != null ? Resolve(in Slot.MeshCurvatureValuesHandle, Slot.MeshCurvatureValuesCapacity) : default;
        public NativeArray<float> MeshAmbientOcclusionValues => Slot != null ? Resolve(in Slot.MeshAmbientOcclusionValuesHandle, Slot.MeshAmbientOcclusionValuesCapacity) : default;
        public NativeArray<float> MeshBiomeValues => Slot != null ? Resolve(in Slot.MeshBiomeValuesHandle, Slot.MeshBiomeValuesCapacity) : default;
        public NativeArray<float> MeshSkirtAlphaValues => Slot != null ? Resolve(in Slot.MeshSkirtAlphaValuesHandle, Slot.MeshSkirtAlphaValuesCapacity) : default;
        public NativeArray<float> MeshDirtyBlendValues => Slot != null ? Resolve(in Slot.MeshDirtyBlendValuesHandle, Slot.MeshDirtyBlendValuesCapacity) : default;
        public NativeArray<Color32> MeshColors => Slot != null ? Resolve(in Slot.MeshColorsHandle, Slot.MeshColorsCapacity) : default;
        public NativeArray<float3> ProjectedLocalPositions => Slot != null ? Resolve(in Slot.ProjectedLocalPositionsHandle, Slot.ProjectedLocalPositionsCapacity) : default;
        public NativeArray<int> SpatialBucketCounts => Slot != null ? Resolve(in Slot.SpatialBucketCountsHandle, Slot.SpatialBucketCountsCapacity) : default;
        public NativeArray<int> SpatialBucketWriteHeads => Slot != null ? Resolve(in Slot.SpatialBucketWriteHeadsHandle, Slot.SpatialBucketWriteHeadsCapacity) : default;
        public NativeArray<int> SpatialNodeBucketOffsets => Slot != null ? Resolve(in Slot.SpatialNodeBucketOffsetsHandle, Slot.SpatialNodeBucketOffsetsCapacity) : default;
        public NativeArray<int> SpatialNodeBucketIndices => Slot != null ? Resolve(in Slot.SpatialNodeBucketIndicesHandle, Slot.SpatialNodeBucketIndicesCapacity) : default;
        public NativeArray<int> SpatialTunnelBucketOffsets => Slot != null ? Resolve(in Slot.SpatialTunnelBucketOffsetsHandle, Slot.SpatialTunnelBucketOffsetsCapacity) : default;
        public NativeArray<int> SpatialTunnelBucketIndices => Slot != null ? Resolve(in Slot.SpatialTunnelBucketIndicesHandle, Slot.SpatialTunnelBucketIndicesCapacity) : default;
        public NativeArray<CaveNode> RebuildNodes => Slot != null ? Resolve(in Slot.RebuildNodesHandle, Slot.RebuildNodesCapacity) : default;
        public NativeArray<CaveTunnel> RebuildTunnels => Slot != null ? Resolve(in Slot.RebuildTunnelsHandle, Slot.RebuildTunnelsCapacity) : default;
        public NativeArray<CaveEntrance> RebuildEntrances => Slot != null ? Resolve(in Slot.RebuildEntrancesHandle, Slot.RebuildEntrancesCapacity) : default;
        public NativeArray<CaveStructure> RebuildStructures => Slot != null ? Resolve(in Slot.RebuildStructuresHandle, Slot.RebuildStructuresCapacity) : default;
        public NativeArray<VoxelCraterStamp> RebuildCraterStamps => Slot != null ? Resolve(in Slot.RebuildCraterStampsHandle, Slot.RebuildCraterStampsCapacity) : default;
        public NativeArray<CaveSpawnData> SpawnPointListScratch => Slot != null ? Resolve(in Slot.SpawnPointListScratchHandle, Slot.SpawnPointListScratchCapacity) : default;
        public NativeArray<int> SpawnPointCountScratch => Slot != null ? Resolve(in Slot.SpawnPointCountHandle, Slot.SpawnPointCountCapacity) : default;
        public NativeArray<VoxelModifiedCellEntry> ModifiedCellsScratch => Slot != null ? Resolve(in Slot.ModifiedCellsScratchHandle, Slot.ModifiedCellsScratchCapacity) : default;
        public NativeArray<int> ModifiedCellCountScratch => Slot != null ? Resolve(in Slot.ModifiedCellCountHandle, Slot.ModifiedCellCountCapacity) : default;
        public NativeArray<int> ModifiedCellBucketHeadsScratch => Slot != null ? Resolve(in Slot.ModifiedCellBucketHeadsHandle, Slot.ModifiedCellBucketHeadsCapacity) : default;
        public NativeArray<int> ModifiedCellNextScratch => Slot != null ? Resolve(in Slot.ModifiedCellNextHandle, Slot.ModifiedCellNextCapacity) : default;
        public NativeArray<byte> ColliderTriangleBuckets => Slot != null ? Resolve(in Slot.ColliderTriangleBucketsHandle, Slot.ColliderTriangleBucketsCapacity) : default;
        public NativeArray<int> ColliderBucketCounts => Slot != null ? Resolve(in Slot.ColliderBucketCountsHandle, Slot.ColliderBucketCountsCapacity) : default;
        public NativeArray<int> ColliderBucketOffsets => Slot != null ? Resolve(in Slot.ColliderBucketOffsetsHandle, Slot.ColliderBucketOffsetsCapacity) : default;
        public NativeArray<int> ColliderBucketWriteHeads => Slot != null ? Resolve(in Slot.ColliderBucketWriteHeadsHandle, Slot.ColliderBucketWriteHeadsCapacity) : default;
        public NativeArray<int> ColliderChunkTriangleIndices => Slot != null ? Resolve(in Slot.ColliderChunkTriangleIndicesHandle, Slot.ColliderChunkTriangleIndicesCapacity) : default;
        public NativeArray<int> ColliderLocalRemap => Slot != null ? Resolve(in Slot.ColliderLocalRemapHandle, Slot.ColliderLocalRemapCapacity) : default;
        public NativeArray<int> ColliderTouchedVertexGlobals => Slot != null ? Resolve(in Slot.ColliderTouchedVertexGlobalsHandle, Slot.ColliderTouchedVertexGlobalsCapacity) : default;
        public NativeArray<float3> ColliderLocalPositions => Slot != null ? Resolve(in Slot.ColliderLocalPositionsHandle, Slot.ColliderLocalPositionsCapacity) : default;
        public NativeArray<int> ColliderLocalIndices => Slot != null ? Resolve(in Slot.ColliderLocalIndicesHandle, Slot.ColliderLocalIndicesCapacity) : default;

        public void Dispose()
        {
            if (_owner == null || _slotIndex < 0)
                return;

            if (_scratchBuffersLocked != 0)
            {
                IDataVault vault = _lockedScratchVault != null ? _lockedScratchVault : _owner._streamingScratchVault;
                HectonVoxelEngine.ReleaseStreamingScratchMutationGuard(vault, _lockedScratchMutationGuardMask);
                _lockedScratchMutationGuardMask = 0UL;
                _lockedScratchVault = null;
                _scratchBuffersLocked = 0;
            }

            _owner.ReleaseStreamingScratchLease(_slotIndex);
            _owner = null;
            _slotIndex = -1;
            _lockedScratchMutationGuardMask = 0UL;
            _lockedScratchVault = null;
            _scratchBuffersLocked = 0;
        }
    }

    internal struct VoxelInlineCaveGraphData
    {
        public const int MaxEntrances = 1;
        public const int MaxStructures = 7;

        public CaveGenerationParams CaveParams;
        public int EntranceCount;
        public int StructureCount;
        public CaveEntrance Entrance0;
        public CaveStructure Structure0;
        public CaveStructure Structure1;
        public CaveStructure Structure2;
        public CaveStructure Structure3;
        public CaveStructure Structure4;
        public CaveStructure Structure5;
        public CaveStructure Structure6;

        public int SafeEntranceCount => math.clamp(EntranceCount, 0, MaxEntrances);
        public int SafeStructureCount => math.clamp(StructureCount, 0, MaxStructures);

        public void SetEntrance(CaveEntrance entrance)
        {
            Entrance0 = entrance;
            EntranceCount = 1;
        }

        public void SetStructure(int index, CaveStructure structure)
        {
            if ((uint)index >= MaxStructures)
                return;

            switch (index)
            {
                case 0:
                    Structure0 = structure;
                    break;
                case 1:
                    Structure1 = structure;
                    break;
                case 2:
                    Structure2 = structure;
                    break;
                case 3:
                    Structure3 = structure;
                    break;
                case 4:
                    Structure4 = structure;
                    break;
                case 5:
                    Structure5 = structure;
                    break;
                case 6:
                    Structure6 = structure;
                    break;
            }

            if (StructureCount <= index)
                StructureCount = index + 1;
        }

        public CaveStructure GetStructure(int index)
        {
            return index switch
            {
                0 => Structure0,
                1 => Structure1,
                2 => Structure2,
                3 => Structure3,
                4 => Structure4,
                5 => Structure5,
                6 => Structure6,
                _ => default
            };
        }
    }

    sealed class VoxelPipelineData : IDisposable
    {
        public HectonVoxelVolume SourceVolume;
        public int SourceRuntimeStamp;
        public Vector3 WorldCenter;
        public double3 AbsoluteUniverseOffsetAtStartDouble;
        public uint ShiftEpochAtStart;
        public float TerrainHeightCenter;
        public bool UseConstantTerrainHeight;
        public float ConstantTerrainHeight;
        public int LODLevel;
        public int GridDimension;
        public float VoxelStep;
        public float EffectiveSealMargin;
        public float LodTransitionBand;
        public int PtsX;
        public int PtsY;
        public int PtsZ;
        public int TotalPts;
        public int TotalCells;
        public int MaxVerts;
        public float VolumeHalfExtent;
        public float3 VolumeOrigin;
        public uint Seed;
        public CaveGenerationParams CaveParams;
        public bool BuildCollider;
        public bool ExtractSpawnPoints;
        public VoxelStreamingScratchLease ScratchLease;
        public int NodeCount;
        public int TunnelCount;
        public int EntranceCount;
        public int StructureCount;
        public int CraterStampCount;
        public int ModifiedCellCount;
        public int ModifiedCellBucketCount;
        public bool UsesStreamingScratchMeshBuffers;
        public bool UsesStreamingScratchAttributeBuffers;
        public bool UsesStreamingScratchSpatialBuckets;
        public bool UsesStreamingScratchSpawnPoints;
        public int PartitionDimX;
        public int PartitionDimY;
        public int PartitionDimZ;
        public float3 PartitionOrigin;
        public float3 PartitionCellSize;
        public int RawCount;
        public int WeldedCount;

        public NativeArray<CaveNode> Nodes => Slice(ScratchLease.RebuildNodes, NodeCount);
        public NativeArray<CaveTunnel> Tunnels => Slice(ScratchLease.RebuildTunnels, TunnelCount);
        public NativeArray<CaveEntrance> Entrances => Slice(ScratchLease.RebuildEntrances, EntranceCount);
        public NativeArray<CaveStructure> Structures => Slice(ScratchLease.RebuildStructures, StructureCount);
        public NativeArray<VoxelCraterStamp> CraterStamps => Slice(ScratchLease.RebuildCraterStamps, CraterStampCount);
        public NativeArray<VoxelModifiedCellEntry> ModifiedCells => ScratchLease.ModifiedCellsScratch;
        public NativeArray<int> ModifiedCellCountBuffer => ScratchLease.ModifiedCellCountScratch;
        public NativeArray<int> ModifiedCellBucketHeads => ScratchLease.ModifiedCellBucketHeadsScratch;
        public NativeArray<int> ModifiedCellNext => ScratchLease.ModifiedCellNextScratch;
        public NativeArray<MCRawVertex> RawVertices => ScratchLease.MeshRawVertices;
        public NativeArray<float3> WeldedPositions => ScratchLease.MeshWeldedPositions;
        public NativeArray<int> TriangleIndices => ScratchLease.MeshTriangleIndices;
        public NativeArray<VoxelSurfaceVertex> SurfaceVertices => ScratchLease.MeshSurfaceVertices;
        public NativeArray<int> EdgeVertexX => ScratchLease.MeshEdgeVertexX;
        public NativeArray<int> EdgeVertexY => ScratchLease.MeshEdgeVertexY;
        public NativeArray<int> EdgeVertexZ => ScratchLease.MeshEdgeVertexZ;
        public NativeArray<float3> Normals => ScratchLease.MeshNormals;
        public NativeArray<float> CurvatureValues => ScratchLease.MeshCurvatureValues;
        public NativeArray<float> AmbientOcclusionValues => ScratchLease.MeshAmbientOcclusionValues;
        public NativeArray<float> BiomeValues => ScratchLease.MeshBiomeValues;
        public NativeArray<float> SkirtAlphaValues => ScratchLease.MeshSkirtAlphaValues;
        public NativeArray<float> DirtyBlendValues => ScratchLease.MeshDirtyBlendValues;
        public NativeArray<Color32> Colors => ScratchLease.MeshColors;
        public NativeArray<CaveSpawnData> SpawnPointList => ScratchLease.SpawnPointListScratch;
        public NativeArray<int> SpawnPointCountBuffer => ScratchLease.SpawnPointCountScratch;
        public int SpawnPointCount => SpawnPointCountBuffer.IsCreated && SpawnPointCountBuffer.Length > 0 ? SpawnPointCountBuffer[0] : 0;
        public NativeArray<int> NodeBucketOffsets => ScratchLease.SpatialNodeBucketOffsets;
        public NativeArray<int> NodeBucketIndices => ScratchLease.SpatialNodeBucketIndices;
        public NativeArray<int> TunnelBucketOffsets => ScratchLease.SpatialTunnelBucketOffsets;
        public NativeArray<int> TunnelBucketIndices => ScratchLease.SpatialTunnelBucketIndices;

        static NativeArray<T> Slice<T>(NativeArray<T> source, int count) where T : struct
        {
            if (!source.IsCreated)
                return default;

            int safeCount = math.clamp(count, 0, source.Length);
            return source.GetSubArray(0, safeCount);
        }

        public void Dispose()
        {
            ScratchLease.Dispose();
        }
    }

    // ╔═══════════════════════════════════════════════╗
    // ║              LIFECYCLE                        ║
    // ╚═══════════════════════════════════════════════╝

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        _teardownStreamingScratchRequested = false;
        CacheRuntimeServicesCold();
        TryRegisterRuntimeServiceHotSwapListener();
        GlobalRegistry.RegisterVoxelEngineRuntime(this);
        s_activeRuntimeInstance = this;
        s_predictiveVoxelProxyPhysicsService = _physicsService;
        if (!_registeredLiveEngine)
        {
            Interlocked.Increment(ref _liveEngineCount);
            _registeredLiveEngine = true;
        }

        EnsureVoxelProxyLayerFiltering();
        EnsureVoxelBakeGhostMaterial();
        EnsureVoxelMeshPipelineBlackBox();
        EnsureStreamingScratchSlots();
        HectonVoxelVolume.TryEnsurePublishedSonarVaultPayloadCapacity(_streamingScratchVault);
        _ = WarmVoxelMeshPoolsAsync(destroyCancellationToken);
        CacheVoxelDeltaProcessorCold();

        MCTables.Initialize(_streamingScratchVault);
    }

    void CacheVoxelDeltaProcessorCold()
    {
        if (_deltaProcessor != null)
            return;

        TryGetComponent(out _deltaProcessor);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_deltaProcessor == null)
        {
            Hecton8.Core.H8Debug.LogError(
                "[HectonVoxel] Missing authored VoxelDeltaProcessor. Runtime voxel carving and delta-save replay will fail closed until the prefab is fixed.",
                this);
        }
#endif
    }

    void OnDisable()
    {
        TeardownRuntimeState();
    }

    void OnDestroy()
    {
        TeardownRuntimeState();
    }

    public void OnGlobalRegistryServiceReplaced(
        GlobalRegistryServiceSlot serviceSlot,
        object previousService,
        object currentService)
    {
        if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)
        {
            CacheObjectPoolService(currentService as ObjectPoolManager);
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.Physics)
        {
            _physicsService = currentService as IPhysicsService;
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_predictiveVoxelProxyPhysicsService = _physicsService;
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.VRAMPressureRuntime)
        {
            _vramPressureReadModel = currentService as IVramPressureReadModel;
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.LODSystemRuntime)
        {
            _lodSystemManager = currentService as LODSystemManager;
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.ScavengePopulatorRuntime)
        {
            _scavengePopulator = currentService as ScavengePopulator;
            WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref _scavengePopulator);
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.MapMagicVegetationRuntime)
        {
            _vegetationBridge = currentService as HectonMapMagicVegetationBridge;
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.ResourceDistributionRuntime)
        {
            _resourceDistributionDirector = currentService as ResourceDistributionDirector;
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.Player)
        {
            s_playerRuntimeContext = currentService as IPlayerRuntimeContext;
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
        {
            IDataVault currentVault = currentService as IDataVault;
            RebindStreamingScratchVault(currentVault);
            EnsureVoxelMeshPipelineBlackBox();
            HectonVoxelVolume.TryEnsurePublishedSonarVaultPayloadCapacity(currentVault);
            MCTables.Initialize(_streamingScratchVault);
        }
    }

    void CacheRuntimeServicesCold()
    {
        CacheObjectPoolService(null);
        _physicsService = GlobalRegistry.Physics;
        _vramPressureReadModel = GlobalRegistry.VRAMPressureReadModel;
        _lodSystemManager = GlobalRegistry.LODSystem;
        WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref _scavengePopulator);
        WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
        _resourceDistributionDirector = GlobalRegistry.ResourceDistribution;
        s_playerRuntimeContext = GlobalRegistry.Player;
        _streamingScratchVault = GlobalRegistry.DataVault;
    }

    void CacheObjectPoolService(ObjectPoolManager candidate)
    {
        ObjectPoolManager pool = candidate;
        if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
            ObjectPoolManager.TryResolveActiveRuntime(ref pool))
        {
            _objectPoolService = pool;
            return;
        }

        _objectPoolService = null;
    }

    bool TryResolveCachedObjectPool(out IObjectPoolService pool)
    {
        ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;
        if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
        {
            pool = cached;
            return true;
        }

        ObjectPoolManager resolved = cached;
        if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
        {
            _objectPoolService = resolved;
            pool = resolved;
            return true;
        }

        _objectPoolService = null;
        pool = null;
        return false;
    }

    bool TryResolvePooledRootComponent<T>(GameObject owner, out T component) where T : Component
    {
        component = null;
        if (owner == null)
            return false;

        if (TryResolveCachedObjectPool(out IObjectPoolService pool) &&
            pool.TryGetPooledComponent(owner, out component) &&
            component != null)
            return true;

#if UNITY_EDITOR
        if (!Application.isPlaying && owner.TryGetComponent(out component))
            return component != null;
#endif

        component = null;
        return false;
    }

    HectonVoxelVolume ResolvePooledVoxelVolume(GameObject owner)
    {
        return TryResolvePooledRootComponent(owner, out HectonVoxelVolume volume) ? volume : null;
    }

    internal bool TryGetRegisteredVolumeComponent(GameObject owner, out HectonVoxelVolume volume)
    {
        volume = null;
        int activeIndex = FindActiveVolumeIndex(owner);
        if (activeIndex < 0 || activeIndex >= _activeVolumeComponents.Count)
            return false;

        volume = _activeVolumeComponents[activeIndex];
        return volume != null;
    }

    MeshFilter ResolvePooledMeshFilter(GameObject owner, HectonVoxelVolume volume)
    {
        if (volume != null && volume.CachedMeshFilter != null)
            return volume.CachedMeshFilter;

        return TryResolvePooledRootComponent(owner, out MeshFilter meshFilter) ? meshFilter : null;
    }

    MeshRenderer ResolvePooledMeshRenderer(GameObject owner, HectonVoxelVolume volume)
    {
        if (volume != null && volume.CachedMeshRenderer != null)
            return volume.CachedMeshRenderer;

        return TryResolvePooledRootComponent(owner, out MeshRenderer meshRenderer) ? meshRenderer : null;
    }

    MeshCollider ResolvePooledMeshCollider(GameObject owner, HectonVoxelVolume volume)
    {
        if (volume != null && volume.CachedRootMeshCollider != null)
            return volume.CachedRootMeshCollider;

        return TryResolvePooledRootComponent(owner, out MeshCollider meshCollider) ? meshCollider : null;
    }

    BoxCollider ResolvePooledBoxCollider(GameObject owner)
    {
        return TryResolvePooledRootComponent(owner, out BoxCollider boxCollider) ? boxCollider : null;
    }

    void RebindStreamingScratchVault(IDataVault currentVault)
    {
        using (EnterStreamingScratchGate())
        {
            if (ReferenceEquals(_streamingScratchVault, currentVault))
            {
                _pendingStreamingScratchVault = null;
                _teardownStreamingScratchRequested = false;
                return;
            }

            if (_streamingScratchSlots != null && HasStreamingScratchSlotInUse_NoLock())
            {
                _pendingStreamingScratchVault = currentVault;
                _teardownStreamingScratchRequested = true;
                return;
            }

            DisposeStreamingScratchSlots_NoLock();
            _streamingScratchVault = currentVault;
            _pendingStreamingScratchVault = null;
            _teardownStreamingScratchRequested = false;
        }

        EnsureStreamingScratchSlots();
    }

    void TryRegisterRuntimeServiceHotSwapListener()
    {
        if (_registeredRuntimeServiceHotSwapListener || !Application.isPlaying)
            return;

        _registeredRuntimeServiceHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
    }

    void TryUnregisterRuntimeServiceHotSwapListener()
    {
        if (!_registeredRuntimeServiceHotSwapListener)
            return;

        GlobalRegistry.TryUnregisterHotSwapListener(this);
        _registeredRuntimeServiceHotSwapListener = false;
    }

    // ╔═══════════════════════════════════════════════╗
    // ║       PUBLIC API — CAVE GENERATION            ║
    // ╚═══════════════════════════════════════════════╝

    /// <summary>
    /// Generate a complete cave volume from seed and preset.
    ///
    /// Pipeline:
    /// 1. CaveGraphGenerator builds room/tunnel graph from seed (main thread)
    /// 2. Terrain heights sampled from MapMagicBridge (main thread)
    /// 3. VoxelDensityJob computes SDF field (Burst, async)
    /// 4. VoxelMCExtractJob extracts triangles (Burst, async)
    /// 5. VoxelWeldJob deduplicates vertices (Burst, async)
    /// 6. VoxelNormalJob + VoxelColorJob compute vertex data (Burst, async)
    /// 7. Mesh assembled on main thread
    ///
    /// v4.0: Full SDF cave system with multi-primitive blending.
    /// </summary>
    /// <param name="worldCenter">World-space center of the voxel volume.</param>
    /// <param name="seed">Deterministic seed for cave generation.</param>
    /// <param name="preset">Cave configuration. Null = use defaultPreset.</param>
    /// <param name="ct">Cancellation token for async cancellation.</param>
    /// <returns>Generated GameObject with mesh, or null if generation produced no geometry.</returns>
    public async Awaitable<GameObject> GenerateVolumeAsync(
        Vector3 worldCenter,
        uint seed,
        CavePreset preset = null,
        CancellationToken ct = default)
    {
        return await GenerateVolumeAsync(worldCenter, seed, preset, ResolveDistanceBasedVoxelLodLevel(worldCenter), ct);
    }

    /// <summary>
    /// Generates a single voxel cave volume with an explicit voxel LOD level.
    /// </summary>
    /// <param name="worldCenter">World-space center of the voxel volume.</param>
    /// <param name="seed">Deterministic seed for cave generation.</param>
    /// <param name="preset">Cave configuration. Null = use defaultPreset.</param>
    /// <param name="lodLevel">Voxel LOD level. 0 = full resolution, 1 = doubled voxel step, 2 = quadrupled voxel step.</param>
    /// <param name="ct">Cancellation token for async cancellation.</param>
    /// <returns>Generated GameObject with mesh, or null if generation produced no geometry.</returns>
    public async Awaitable<GameObject> GenerateVolumeAsync(
        Vector3 worldCenter,
        uint seed,
        CavePreset preset,
        int lodLevel,
        CancellationToken ct = default)
    {
        BeginGenerationOperation();
        NativeArray<CaveNode> caveNodes = default;
        NativeArray<CaveTunnel> caveTunnels = default;
        NativeArray<CaveEntrance> caveEntrances = default;
        NativeArray<CaveStructure> caveStructures = default;
        NativeArray<VoxelCraterStamp> generationCraterScratch = default;
        VoxelStreamingScratchLease generationScratchLease = default;
        VoxelPipelineData pipelineData = null;
        bool usesStreamingScratchGraphSnapshots = false;

        try
        {
            if (mapMagicBridge == null)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
#endif
                return null;
            }

            MCTables.Initialize(_streamingScratchVault);

            if (preset == null)
                preset = defaultPreset;
            if (preset == null)
                preset = CavePresetLibrary.Create(CavePresetType.Grotto);

            int clampedLodLevel = math.clamp(lodLevel, 0, 2);
            int baseGridDim = math.clamp(preset.gridDimension, 32, 128);
            float baseVoxelStep = math.max(preset.voxelSize, 0.25f);
            int gridDim = math.max(16, baseGridDim >> clampedLodLevel);
            float voxelStep = baseVoxelStep * (1 << clampedLodLevel);
            int ptsX = gridDim + 1;
            int ptsY = gridDim + 1;
            int ptsZ = gridDim + 1;
            int totalPts = ptsX * ptsY * ptsZ;
            int totalCells = gridDim * gridDim * gridDim;
            int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;
            float volumeHalfExtent = gridDim * voxelStep * 0.5f;
            float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
            float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;
            float lodTransitionBand = clampedLodLevel > 0 ? math.max(baseVoxelStep * 2f, voxelStep * 1.25f) : 0f;
            float effectiveSealMargin = math.max(sealMargin, TerrainVoxelSeamTransitionBand) + lodTransitionBand;
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 absoluteUniverseOffsetAtStartDouble))
                return null;

            uint shiftEpochAtStart = HectonFloatingOrigin.CurrentShiftSequence;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            CaveGenerationParams caveParams = preset.ToGenerationParams(seed);
            int graphNodes, graphTunnels, graphEntrances, graphStructures;
            
            if (preset.presetType == CavePresetType.SurfaceTrench)
            {
                if (!SurfaceTrenchGraphGenerator.TryMeasure(
                    seed,
                    worldCenter,
                    volumeHalfExtent,
                    out SurfaceTrenchGraphGenerator.TrenchGraphCounts trenchCounts))
                {
                    return null;
                }
                graphNodes = trenchCounts.Nodes;
                graphTunnels = trenchCounts.Segments;
                graphEntrances = 0;
                graphStructures = trenchCounts.Structures;
            }
            else
            {
                if (!CaveGraphGenerator.TryMeasure(
                    seed,
                    preset,
                    worldCenter,
                    terrainHeightCenter,
                    volumeHalfExtent,
                    out CaveGraphGenerator.CaveGraphCounts caveGraphCounts))
                {
                    return null;
                }
                graphNodes = caveGraphCounts.Nodes;
                graphTunnels = caveGraphCounts.Tunnels;
                graphEntrances = caveGraphCounts.Entrances;
                graphStructures = caveGraphCounts.Structures;
            }

            generationScratchLease = await AcquireStreamingScratchLeaseAsync(ptsX * ptsZ, totalPts, totalCells, gridDim, ct);
            if (!generationScratchLease.IsValid)
                return null;

            if (!TryPrepareRebuildGraphScratch(
                    ref generationScratchLease,
                    graphNodes,
                    graphTunnels,
                    graphEntrances,
                    graphStructures,
                    0,
                    out caveNodes,
                    out caveTunnels,
                    out caveEntrances,
                    out caveStructures,
                    out generationCraterScratch))
            {
                generationScratchLease.Dispose();
                return null;
            }

            usesStreamingScratchGraphSnapshots = true;

            if (preset.presetType == CavePresetType.SurfaceTrench)
            {
                if (!SurfaceTrenchGraphGenerator.TryFill(
                        seed,
                        worldCenter,
                        volumeHalfExtent,
                        caveNodes,
                        caveTunnels,
                        caveStructures,
                        out SurfaceTrenchGraphGenerator.TrenchGraphCounts filledTrenchCounts) ||
                    filledTrenchCounts.Nodes != graphNodes ||
                    filledTrenchCounts.Segments != graphTunnels ||
                    filledTrenchCounts.Structures != graphStructures)
                {
                    return null;
                }
            }
            else
            {
                if (!CaveGraphGenerator.TryFill(
                        seed,
                        preset,
                        worldCenter,
                        terrainHeightCenter,
                        volumeHalfExtent,
                        caveNodes,
                        caveTunnels,
                        caveEntrances,
                        caveStructures,
                        out CaveGraphGenerator.CaveGraphCounts filledCaveGraphCounts) ||
                    filledCaveGraphCounts.Nodes != graphNodes ||
                    filledCaveGraphCounts.Tunnels != graphTunnels ||
                    filledCaveGraphCounts.Entrances != graphEntrances ||
                    filledCaveGraphCounts.Structures != graphStructures)
                {
                    return null;
                }
            }

#if UNITY_EDITOR
            CaveGraphGenerator.Validate(caveNodes, caveTunnels, caveEntrances, worldCenter, volumeHalfExtent);
#endif

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.Log(CaveGraphGenerator.GetSummary(caveNodes, caveTunnels, caveEntrances));
#endif

            pipelineData = new VoxelPipelineData
            {
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStartDouble = absoluteUniverseOffsetAtStartDouble,
                ShiftEpochAtStart = shiftEpochAtStart,
                TerrainHeightCenter = terrainHeightCenter,
                LODLevel = clampedLodLevel,
                GridDimension = gridDim,
                VoxelStep = voxelStep,
                EffectiveSealMargin = effectiveSealMargin,
                LodTransitionBand = lodTransitionBand,
                PtsX = ptsX,
                PtsY = ptsY,
                PtsZ = ptsZ,
                TotalPts = totalPts,
                TotalCells = totalCells,
                MaxVerts = maxVerts,
                VolumeHalfExtent = volumeHalfExtent,
                VolumeOrigin = volumeOrigin,
                Seed = seed,
                CaveParams = caveParams,
                BuildCollider = clampedLodLevel == 0,
                ExtractSpawnPoints = true,
                ScratchLease = generationScratchLease,
                NodeCount = caveNodes.IsCreated ? caveNodes.Length : 0,
                TunnelCount = caveTunnels.IsCreated ? caveTunnels.Length : 0,
                EntranceCount = caveEntrances.IsCreated ? caveEntrances.Length : 0,
                StructureCount = caveStructures.IsCreated ? caveStructures.Length : 0,
                CraterStampCount = 0
            };

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return null;

            GameObject targetGO = SpawnVolume();
            if (targetGO == null)
                return null;

            targetGO.name = RuntimeCaveVolumeName;
            if (!TryBindGeneratedVolumeForMeshPublication(targetGO, pipelineData))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            if (!TryResolveRuntimeFloat3FromAup(absoluteUniverseOffsetAtStartDouble, stableShift.NewTotalOffsetDouble, out float3 targetRuntimePosition))
            {
                DespawnVolume(targetGO);
                return null;
            }

            targetGO.transform.position = ToVector3(targetRuntimePosition);

            if (!await ApplyVolumeMeshAsync(targetGO, pipelineData, stableShift, ct))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData postMeshShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            if (!await ConfigureVolumeRuntimeDataFromPipelineAsync(targetGO, seed, worldCenter, absoluteUniverseOffsetAtStartDouble, preset, gridDim, voxelStep, clampedLodLevel, caveParams,
                    caveNodes, caveTunnels, caveEntrances, caveStructures,
                    pipelineData,
                    pipelineData.PtsX,
                    pipelineData.PtsY,
                    pipelineData.PtsZ,
                    (Vector3)pipelineData.VolumeOrigin,
                    pipelineData.VoxelStep,
                    pipelineData.BuildCollider,
                    ct))
            {
                DespawnVolume(targetGO);
                return null;
            }
            RegisterEntranceTerrainHoles(targetGO, caveEntrances, voxelStep, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble);
            if (!RegisterActiveVolume(targetGO))
            {
                DespawnVolume(targetGO);
                return null;
            }

            if (!TryRegisterPipelineSpawnPointsFromScratch(pipelineData, worldCenter, caveParams.spawnContext, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble))
            {
                DespawnVolume(targetGO);
                return null;
            }

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.Log("[HectonVoxel] Cave volume generated.");
#endif
            return targetGO;
        }
        finally
        {
            pipelineData?.Dispose();
            if (pipelineData == null)
                generationScratchLease.Dispose();

            if (!usesStreamingScratchGraphSnapshots)
            {
                DisposeTrackedNativeArray(ref caveNodes);
                DisposeTrackedNativeArray(ref caveTunnels);
                DisposeTrackedNativeArray(ref caveEntrances);
                DisposeTrackedNativeArray(ref caveStructures);
            }
            else
            {
                caveNodes = default;
                caveTunnels = default;
                caveEntrances = default;
                caveStructures = default;
                generationCraterScratch = default;
            }

            EndGenerationOperation();
        }
    }
    /// <summary>
    /// Overload accepting pre-built cave data.
    /// Use when you want to generate the graph externally (e.g. custom editor tool)
    /// and pass raw NativeArrays directly.
    ///
    /// Caller is responsible for disposing input NativeArrays AFTER this method completes.
    /// </summary>
    internal async Awaitable<GameObject> GenerateVolumeFromDataAsync(
        AbsoluteUniversePosition worldCenterAup,
        int gridDimension,
        float voxelSize,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        CaveGenerationParams caveParams,
        int lodLevel,
        bool buildCollider = true,
        CancellationToken ct = default)
    {
        if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 originAup) ||
            !TryResolveRuntimeVector3FromAup(worldCenterAup.ToAbsoluteDouble3(), originAup, out Vector3 runtimeCenter))
        {
            return null;
        }

        return await GenerateVolumeFromDataAsync(
            runtimeCenter,
            gridDimension,
            voxelSize,
            nodes,
            tunnels,
            entrances,
            structures,
            caveParams,
            lodLevel,
            buildCollider,
            ct);
    }

    /// <summary>
    /// Overload accepting pre-built cave data.
    /// Use when you want to generate the graph externally (e.g. custom editor tool)
    /// and pass raw NativeArrays directly.
    ///
    /// Caller is responsible for disposing input NativeArrays AFTER this method completes.
    /// </summary>
    public async Awaitable<GameObject> GenerateVolumeFromDataAsync(
        Vector3 worldCenter,
        int gridDimension,
        float voxelSize,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        CaveGenerationParams caveParams,
        bool buildCollider = true,
        CancellationToken ct = default)
    {
        return await GenerateVolumeFromDataAsync(
            worldCenter,
            gridDimension,
            voxelSize,
            nodes,
            tunnels,
            entrances,
            structures,
            caveParams,
            ResolveDistanceBasedVoxelLodLevel(worldCenter),
            buildCollider,
            ct);
    }

    /// <summary>
    /// Overload accepting pre-built cave data with an explicit voxel LOD level.
    /// Use when you want to generate the graph externally (e.g. custom editor tool)
    /// and pass raw NativeArrays directly.
    ///
    /// Caller is responsible for disposing input NativeArrays AFTER this method completes.
    /// </summary>
    public async Awaitable<GameObject> GenerateVolumeFromDataAsync(
        Vector3 worldCenter,
        int gridDimension,
        float voxelSize,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        CaveGenerationParams caveParams,
        int lodLevel,
        bool buildCollider = true,
        CancellationToken ct = default)
    {
        return await GenerateVolumeFromDataInternalAsync(
            worldCenter,
            gridDimension,
            voxelSize,
            nodes,
            tunnels,
            entrances,
            structures,
            caveParams,
            default,
            false,
            lodLevel,
            buildCollider,
            ct);
    }

    internal async Awaitable<GameObject> GenerateVolumeFromInlineCaveGraphDataAsync(
        AbsoluteUniversePosition worldCenterAup,
        int gridDimension,
        float voxelSize,
        VoxelInlineCaveGraphData caveGraphData,
        int lodLevel,
        bool buildCollider = true,
        CancellationToken ct = default)
    {
        if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 originAup) ||
            !TryResolveRuntimeVector3FromAup(worldCenterAup.ToAbsoluteDouble3(), originAup, out Vector3 runtimeCenter))
        {
            return null;
        }

        return await GenerateVolumeFromInlineCaveGraphDataAsync(
            runtimeCenter,
            gridDimension,
            voxelSize,
            caveGraphData,
            lodLevel,
            buildCollider,
            ct);
    }

    internal async Awaitable<GameObject> GenerateVolumeFromInlineCaveGraphDataAsync(
        Vector3 worldCenter,
        int gridDimension,
        float voxelSize,
        VoxelInlineCaveGraphData caveGraphData,
        int lodLevel,
        bool buildCollider = true,
        CancellationToken ct = default)
    {
        return await GenerateVolumeFromDataInternalAsync(
            worldCenter,
            gridDimension,
            voxelSize,
            default,
            default,
            default,
            default,
            caveGraphData.CaveParams,
            caveGraphData,
            true,
            lodLevel,
            buildCollider,
            ct);
    }

    private async Awaitable<GameObject> GenerateVolumeFromDataInternalAsync(
        Vector3 worldCenter,
        int gridDimension,
        float voxelSize,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        CaveGenerationParams caveParams,
        VoxelInlineCaveGraphData caveGraphData,
        bool useInlineCaveGraphData,
        int lodLevel,
        bool buildCollider,
        CancellationToken ct)
    {
        BeginGenerationOperation();
        VoxelPipelineData pipelineData = null;

        try
        {
            if (mapMagicBridge == null)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
#endif
                return null;
            }

            MCTables.Initialize(_streamingScratchVault);

            int clampedLodLevel = math.clamp(lodLevel, 0, 2);
            int baseGridDim = math.clamp(gridDimension, 32, 128);
            float baseVoxelStep = math.max(voxelSize, 0.25f);
            int gridDim = math.max(16, baseGridDim >> clampedLodLevel);
            float voxelStep = baseVoxelStep * (1 << clampedLodLevel);
            int ptsX = gridDim + 1;
            int ptsY = gridDim + 1;
            int ptsZ = gridDim + 1;
            int totalPts = ptsX * ptsY * ptsZ;
            int totalCells = gridDim * gridDim * gridDim;
            int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;
            float volumeHalfExtent = gridDim * voxelStep * 0.5f;
            float lodTransitionBand = clampedLodLevel > 0 ? math.max(baseVoxelStep * 2f, voxelStep * 1.25f) : 0f;
            float effectiveSealMargin = math.max(sealMargin, TerrainVoxelSeamTransitionBand) + lodTransitionBand;
            float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
            float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 absoluteUniverseOffsetAtStartDouble))
                return null;

            uint shiftEpochAtStart = HectonFloatingOrigin.CurrentShiftSequence;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            CaveGenerationParams resolvedCaveParams = useInlineCaveGraphData ? caveGraphData.CaveParams : caveParams;
            int inputNodeCount = useInlineCaveGraphData ? 0 : nodes.IsCreated ? nodes.Length : 0;
            int inputTunnelCount = useInlineCaveGraphData ? 0 : tunnels.IsCreated ? tunnels.Length : 0;
            int inputEntranceCount = useInlineCaveGraphData ? caveGraphData.SafeEntranceCount : entrances.IsCreated ? entrances.Length : 0;
            int inputStructureCount = useInlineCaveGraphData ? caveGraphData.SafeStructureCount : structures.IsCreated ? structures.Length : 0;
            VoxelStreamingScratchLease inputScratchLease = await AcquireStreamingScratchLeaseAsync(ptsX * ptsZ, totalPts, totalCells, gridDim, ct);
            if (!inputScratchLease.IsValid)
                return null;

            if (!TryPrepareRebuildGraphScratch(
                    ref inputScratchLease,
                    inputNodeCount,
                    inputTunnelCount,
                    inputEntranceCount,
                    inputStructureCount,
                    0,
                    out NativeArray<CaveNode> scratchNodes,
                    out NativeArray<CaveTunnel> scratchTunnels,
                    out NativeArray<CaveEntrance> scratchEntrances,
                    out NativeArray<CaveStructure> scratchStructures,
                    out NativeArray<VoxelCraterStamp> _))
            {
                inputScratchLease.Dispose();
                return null;
            }

            if (useInlineCaveGraphData)
            {
                CopyInlineCaveGraphToScratch(caveGraphData, scratchEntrances, scratchStructures);
            }
            else
            {
                CopyNativeArrayToScratch(nodes, scratchNodes, inputNodeCount);
                CopyNativeArrayToScratch(tunnels, scratchTunnels, inputTunnelCount);
                CopyNativeArrayToScratch(entrances, scratchEntrances, inputEntranceCount);
                CopyNativeArrayToScratch(structures, scratchStructures, inputStructureCount);
            }

            pipelineData = new VoxelPipelineData
            {
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStartDouble = absoluteUniverseOffsetAtStartDouble,
                ShiftEpochAtStart = shiftEpochAtStart,
                TerrainHeightCenter = terrainHeightCenter,
                LODLevel = clampedLodLevel,
                GridDimension = gridDim,
                VoxelStep = voxelStep,
                EffectiveSealMargin = effectiveSealMargin,
                LodTransitionBand = lodTransitionBand,
                PtsX = ptsX,
                PtsY = ptsY,
                PtsZ = ptsZ,
                TotalPts = totalPts,
                TotalCells = totalCells,
                MaxVerts = maxVerts,
                VolumeHalfExtent = volumeHalfExtent,
                VolumeOrigin = volumeOrigin,
                Seed = resolvedCaveParams.seed,
                CaveParams = resolvedCaveParams,
                BuildCollider = buildCollider && clampedLodLevel == 0,
                ExtractSpawnPoints = true,
                ScratchLease = inputScratchLease,
                NodeCount = inputNodeCount,
                TunnelCount = inputTunnelCount,
                EntranceCount = inputEntranceCount,
                StructureCount = inputStructureCount,
                CraterStampCount = 0
            };

#if UNITY_EDITOR
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    "preawait");
            }
#endif

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return null;

#if UNITY_EDITOR
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    "surface-data");
            }
#endif

            GameObject targetGO = SpawnVolume();
            if (targetGO == null)
                return null;

            targetGO.name = RuntimeCaveVolumeName;
            if (!TryBindGeneratedVolumeForMeshPublication(targetGO, pipelineData))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            if (!TryResolveRuntimeFloat3FromAup(absoluteUniverseOffsetAtStartDouble, stableShift.NewTotalOffsetDouble, out float3 targetRuntimePosition))
            {
                DespawnVolume(targetGO);
                return null;
            }

            targetGO.transform.position = ToVector3(targetRuntimePosition);

            if (!await ApplyVolumeMeshAsync(targetGO, pipelineData, stableShift, ct))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData postMeshShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            NativeArray<CaveNode> runtimeNodes = pipelineData.Nodes;
            NativeArray<CaveTunnel> runtimeTunnels = pipelineData.Tunnels;
            NativeArray<CaveEntrance> runtimeEntrances = pipelineData.Entrances;
            NativeArray<CaveStructure> runtimeStructures = pipelineData.Structures;
            if (!await ConfigureVolumeRuntimeDataFromPipelineAsync(targetGO, resolvedCaveParams.seed, worldCenter, absoluteUniverseOffsetAtStartDouble, null, gridDim, voxelStep, clampedLodLevel, resolvedCaveParams,
                    runtimeNodes, runtimeTunnels, runtimeEntrances, runtimeStructures,
                    pipelineData,
                    pipelineData.PtsX,
                    pipelineData.PtsY,
                    pipelineData.PtsZ,
                    (Vector3)pipelineData.VolumeOrigin,
                    pipelineData.VoxelStep,
                    pipelineData.BuildCollider,
                    ct))
            {
                DespawnVolume(targetGO);
                return null;
            }
            RegisterEntranceTerrainHoles(targetGO, runtimeEntrances, voxelStep, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble);
            if (!RegisterActiveVolume(targetGO))
            {
                DespawnVolume(targetGO);
                return null;
            }

            if (!TryRegisterPipelineSpawnPointsFromScratch(pipelineData, worldCenter, resolvedCaveParams.spawnContext, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble))
            {
                DespawnVolume(targetGO);
                return null;
            }

#if UNITY_EDITOR
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    "mesh-build");
            }

            Hecton8.Core.H8Debug.Log("[HectonVoxel] Data volume generated.");
#endif
            return targetGO;
        }
        finally
        {
            pipelineData?.Dispose();
            EndGenerationOperation();
        }
    }
    internal async Awaitable<bool> RebuildVolumeAsync(
        HectonVoxelVolume volume,
        int expectedRuntimeStamp,
        CancellationToken ct = default)
    {
        BeginGenerationOperation();
        NativeArray<CaveNode> nodes = default;
        NativeArray<CaveTunnel> tunnels = default;
        NativeArray<CaveEntrance> entrances = default;
        NativeArray<CaveStructure> structures = default;
        NativeArray<VoxelCraterStamp> craterStamps = default;
        VoxelStreamingScratchLease rebuildScratchLease = default;
        VoxelPipelineData pipelineData = null;
        bool usesStreamingScratchGraphSnapshots = false;

        try
        {
            if (volume == null || !volume.HasRuntimeData || !volume.MatchesRuntimeStamp(expectedRuntimeStamp))
                return false;

            if (mapMagicBridge == null)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
#endif
                return false;
            }

            MCTables.Initialize(_streamingScratchVault);

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            int lodLevel = math.clamp(volume.LODLevel, 0, 2);
            int gridDim = math.clamp(volume.GridDimension, 16, 128);
            float voxelStep = math.max(volume.VoxelSize, 0.25f);
            double3 committedTotalOffsetDouble = stableShift.NewTotalOffsetDouble;
            if (!TryResolveRuntimeVector3FromAup(volume.GenerationAbsoluteUniversePositionDouble, committedTotalOffsetDouble, out Vector3 worldCenter))
                return false;
            double3 capturedTotalOffsetDouble = volume.GenerationAbsoluteUniversePositionDouble - global::Hecton8.World.AUPMath.ToDouble3(volume.generationPosition);
            if (!math.all(math.isfinite(capturedTotalOffsetDouble)))
                return false;
            CaveGenerationParams caveParams = volume.CaveParams;
            float lodTransitionBand = lodLevel > 0 ? math.max(voxelStep * 1.25f, 0.5f) : 0f;
            float effectiveSealMargin = math.max(sealMargin, TerrainVoxelSeamTransitionBand) + lodTransitionBand;
            int ptsX = gridDim + 1;
            int ptsY = gridDim + 1;
            int ptsZ = gridDim + 1;
            int totalPts = ptsX * ptsY * ptsZ;
            int totalCells = gridDim * gridDim * gridDim;
            int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;
            float volumeHalfExtent = gridDim * voxelStep * 0.5f;
            float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
            float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;

            CaveNode[] nodeSnapshot = volume.Nodes;
            CaveTunnel[] tunnelSnapshot = volume.Tunnels;
            CaveEntrance[] entranceSnapshot = volume.Entrances;
            CaveStructure[] structureSnapshot = volume.Structures;
            int craterCount = volume.CraterStampCount;
            int nodeCount = nodeSnapshot != null ? nodeSnapshot.Length : 0;
            int tunnelCount = tunnelSnapshot != null ? tunnelSnapshot.Length : 0;
            int entranceCount = entranceSnapshot != null ? entranceSnapshot.Length : 0;
            int structureCount = structureSnapshot != null ? structureSnapshot.Length : 0;
            int safeCraterCount = math.clamp(craterCount, 0, StreamingCraterStampScratchCapacity);

            rebuildScratchLease = await AcquireStreamingScratchLeaseAsync(ptsX * ptsZ, totalPts, totalCells, gridDim, ct);
            if (!rebuildScratchLease.IsValid)
                return false;

            if (!TryPrepareRebuildGraphScratch(
                    ref rebuildScratchLease,
                    nodeCount,
                    tunnelCount,
                    entranceCount,
                    structureCount,
                    safeCraterCount,
                    out nodes,
                    out tunnels,
                    out entrances,
                    out structures,
                    out craterStamps))
            {
                rebuildScratchLease.Dispose();
                return false;
            }

            usesStreamingScratchGraphSnapshots = true;

            for (int i = 0; i < nodeCount; i++)
            {
                CaveNode node = nodeSnapshot[i];
                if (!TryRebaseCapturedRuntimeFloat3(node.position, capturedTotalOffsetDouble, committedTotalOffsetDouble, out node.position))
                {
                    rebuildScratchLease.Dispose();
                    return false;
                }

                nodes[i] = node;
            }
            for (int i = 0; i < tunnelCount; i++)
            {
                CaveTunnel tunnel = tunnelSnapshot[i];
                if (!TryRebaseCapturedRuntimeFloat3(tunnel.pointA, capturedTotalOffsetDouble, committedTotalOffsetDouble, out tunnel.pointA) ||
                    !TryRebaseCapturedRuntimeFloat3(tunnel.pointB, capturedTotalOffsetDouble, committedTotalOffsetDouble, out tunnel.pointB))
                {
                    rebuildScratchLease.Dispose();
                    return false;
                }

                tunnels[i] = tunnel;
            }
            for (int i = 0; i < entranceCount; i++)
            {
                CaveEntrance entrance = entranceSnapshot[i];
                if (!TryRebaseCapturedRuntimeFloat3(entrance.surfacePosition, capturedTotalOffsetDouble, committedTotalOffsetDouble, out entrance.surfacePosition))
                {
                    rebuildScratchLease.Dispose();
                    return false;
                }

                entrances[i] = entrance;
            }
            for (int i = 0; i < structureCount; i++)
            {
                CaveStructure structure = structureSnapshot[i];
                if (!TryRebaseCapturedRuntimeFloat3(structure.position, capturedTotalOffsetDouble, committedTotalOffsetDouble, out structure.position) ||
                    !TryRebaseCapturedRuntimeFloat3(structure.pointB, capturedTotalOffsetDouble, committedTotalOffsetDouble, out structure.pointB))
                {
                    rebuildScratchLease.Dispose();
                    return false;
                }

                structures[i] = structure;
            }
            for (int i = 0; i < safeCraterCount; i++)
            {
                if (!volume.TryGetCraterStamp(i, out VoxelCraterStamp crater))
                {
                    rebuildScratchLease.Dispose();
                    return false;
                }

                crater.position -= committedTotalOffsetDouble;
                craterStamps[i] = crater;
            }

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            pipelineData = new VoxelPipelineData
            {
                SourceVolume = volume,
                SourceRuntimeStamp = expectedRuntimeStamp,
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStartDouble = committedTotalOffsetDouble,
                ShiftEpochAtStart = stableShift.Sequence,
                TerrainHeightCenter = terrainHeightCenter,
                LODLevel = lodLevel,
                GridDimension = gridDim,
                VoxelStep = voxelStep,
                EffectiveSealMargin = effectiveSealMargin,
                LodTransitionBand = lodTransitionBand,
                PtsX = ptsX,
                PtsY = ptsY,
                PtsZ = ptsZ,
                TotalPts = totalPts,
                TotalCells = totalCells,
                MaxVerts = maxVerts,
                VolumeHalfExtent = volumeHalfExtent,
                VolumeOrigin = volumeOrigin,
                Seed = caveParams.seed,
                CaveParams = caveParams,
                BuildCollider = volume.BuildCollider,
                ExtractSpawnPoints = false,
                ScratchLease = rebuildScratchLease,
                NodeCount = nodes.IsCreated ? nodes.Length : 0,
                TunnelCount = tunnels.IsCreated ? tunnels.Length : 0,
                EntranceCount = entrances.IsCreated ? entrances.Length : 0,
                StructureCount = structures.IsCreated ? structures.Length : 0,
                CraterStampCount = craterStamps.IsCreated ? craterStamps.Length : 0
            };

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return false;

            if (volume == null || !volume.MatchesRuntimeStamp(expectedRuntimeStamp))
                return false;

            OriginShiftEventData finalizeShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            return volume != null &&
                   volume.MatchesRuntimeStamp(expectedRuntimeStamp) &&
                   await ApplyVolumeMeshAsync(volume.gameObject, pipelineData, finalizeShift, ct);
        }
        finally
        {
            pipelineData?.Dispose();
            if (pipelineData == null)
                rebuildScratchLease.Dispose();

            if (!usesStreamingScratchGraphSnapshots)
            {
                DisposeTrackedNativeArray(ref nodes);
                DisposeTrackedNativeArray(ref tunnels);
                DisposeTrackedNativeArray(ref entrances);
                DisposeTrackedNativeArray(ref structures);
                DisposeTrackedNativeArray(ref craterStamps);
            }
            else
            {
                nodes = default;
                tunnels = default;
                entrances = default;
                structures = default;
                craterStamps = default;
            }

            EndGenerationOperation();
        }
    }

    bool RegisterActiveVolume(GameObject volumeObject)
    {
        if (volumeObject == null)
            return false;

        if (FindActiveVolumeIndex(volumeObject) >= 0)
            return true;

        HectonVoxelVolume voxelVolume = ResolvePooledVoxelVolume(volumeObject);

        if (_activeVolumes.Count >= ActiveVolumeRegistryCapacity)
        {
            int evictionIndex = SelectActiveVolumeEvictionIndex(voxelVolume);
            if (evictionIndex >= 0 && evictionIndex < _activeVolumes.Count)
            {
                GameObject evictedVolume = _activeVolumes[evictionIndex];
                if (evictedVolume != null && !ReferenceEquals(evictedVolume, volumeObject))
                    DespawnVolume(evictedVolume);
                else
                    RemoveActiveVolumeAt(evictionIndex);
            }

            if (_activeVolumes.Count >= ActiveVolumeRegistryCapacity)
                return false;
        }

        Bounds localBounds = default;
        bool hasLocalBounds = false;
        MeshFilter meshFilter = ResolvePooledMeshFilter(volumeObject, voxelVolume);

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            localBounds = meshFilter.sharedMesh.bounds;
            hasLocalBounds = localBounds.size.sqrMagnitude > 0.0001f;
        }

        if (!hasLocalBounds && voxelVolume != null && voxelVolume.GridDimension > 0 && voxelVolume.VoxelSize > 0f)
        {
            float coverage = voxelVolume.GridDimension * voxelVolume.VoxelSize;
            localBounds = new Bounds(Vector3.zero, new Vector3(coverage, coverage, coverage));
            hasLocalBounds = true;
        }

        if (!hasLocalBounds)
            localBounds = new Bounds(Vector3.zero, Vector3.one);

        if (_activeVolumeLocalBounds.Length >= _activeVolumeLocalBounds.Capacity)
            return false;

        _activeVolumes.Add(volumeObject);
        _activeVolumeComponents.Add(voxelVolume);
        _activeVolumeLocalBounds.AddNoResize(ActiveVolumeLocalBoundsEntry.FromBounds(localBounds));
        return true;
    }

    int SelectActiveVolumeEvictionIndex(HectonVoxelVolume incomingVolume)
    {
        int selectedIndex = _activeVolumes.Count > 0 ? 0 : -1;
        if (incomingVolume == null || _activeVolumes.Count <= 1)
            return selectedIndex;

        double3 incomingPosition = incomingVolume.GenerationAbsoluteUniversePositionDouble;
        if (!math.all(math.isfinite(incomingPosition)))
            return selectedIndex;

        double bestDistanceSq = double.NegativeInfinity;
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            HectonVoxelVolume candidate = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (candidate == null || !candidate.HasRuntimeData)
                return i;

            double3 candidatePosition = candidate.GenerationAbsoluteUniversePositionDouble;
            if (!math.all(math.isfinite(candidatePosition)))
                return i;

            double dx = candidatePosition.x - incomingPosition.x;
            double dz = candidatePosition.z - incomingPosition.z;
            double distanceSq = dx * dx + dz * dz;
            if (distanceSq <= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            selectedIndex = i;
        }

        return selectedIndex;
    }

    int FindActiveVolumeIndex(GameObject volumeObject)
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] == volumeObject)
                return i;
        }

        return -1;
    }

    void UnregisterActiveVolume(GameObject volumeObject)
    {
        int index = FindActiveVolumeIndex(volumeObject);
        if (index >= 0)
            RemoveActiveVolumeAt(index);
    }

    void RemoveActiveVolumeAt(int index)
    {
        if (index < 0 || index >= _activeVolumes.Count)
            return;

        int last = _activeVolumes.Count - 1;
        _activeVolumes[index] = _activeVolumes[last];
        _activeVolumes.RemoveAt(last);

        if (_activeVolumeComponents.Count > last)
        {
            _activeVolumeComponents[index] = _activeVolumeComponents[last];
            _activeVolumeComponents.RemoveAt(last);
        }
        else if (index < _activeVolumeComponents.Count)
        {
            _activeVolumeComponents.RemoveAt(index);
        }

        if (_activeVolumeLocalBounds.Length > last)
        {
            _activeVolumeLocalBounds[index] = _activeVolumeLocalBounds[last];
            _activeVolumeLocalBounds.RemoveAtSwapBack(last);
        }
        else if (index < _activeVolumeLocalBounds.Length)
        {
            _activeVolumeLocalBounds.RemoveAtSwapBack(index);
        }
    }

    /// <summary>
    /// Despawns active voxel volumes whose runtime center lies inside the supplied XZ bounds.
    /// </summary>
    internal int DespawnVolumesInsideAbsoluteXZ(double minX, double maxX, double minZ, double maxZ)
    {
        int despawned = 0;
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            GameObject activeVolume = _activeVolumes[i];
            if (activeVolume == null)
            {
                RemoveActiveVolumeAt(i);
                continue;
            }

            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (volume == null || !volume.HasRuntimeData)
                continue;

            AbsoluteUniversePosition volumeAup = AbsoluteUniversePosition.FromAbsolutePosition(volume.GenerationAbsoluteUniversePositionDouble);
            double3 resolvedPosition = volumeAup.ToAbsoluteDouble3();
            if (resolvedPosition.x < minX || resolvedPosition.x > maxX ||
                resolvedPosition.z < minZ || resolvedPosition.z > maxZ)
                continue;

            DespawnVolume(activeVolume);
            despawned++;
        }

        return despawned;
    }

    /// <summary>Despawns a volume, cleans its mesh, returns to pool.</summary>
    public void DespawnVolume(GameObject volume)
    {
        if (volume == null) return;
        int activeIndex = FindActiveVolumeIndex(volume);
        HectonVoxelVolume voxelVolume = activeIndex >= 0 && activeIndex < _activeVolumeComponents.Count
            ? _activeVolumeComponents[activeIndex]
            : ResolvePooledVoxelVolume(volume);
        MeshFilter mf = ResolvePooledMeshFilter(volume, voxelVolume);
        MeshCollider mc = ResolvePooledMeshCollider(volume, voxelVolume);

        if (activeIndex >= 0)
            RemoveActiveVolumeAt(activeIndex);
        else
            UnregisterActiveVolume(volume);

        HectonFloatingOrigin.MarkShiftTargetsDirty();

        if (mc != null) mc.enabled = false;
        if (voxelVolume != null)
            voxelVolume.PrepareForReuse();

        if (TryResolveCachedObjectPool(out IObjectPoolService pool) && voxelVolumePrefab != null)
        {
            VoxelVolumeLeakSentinel.MarkReleasedToPool(voxelVolume);
            ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: false);
            pool.Despawn(volume);
        }
        else
        {
            VoxelVolumeLeakSentinel.MarkDestroyRequested(voxelVolume);
            ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: true);
            SafeDestroy(volume);
        }
    }

    /// <summary>Removes null references from active volumes list.</summary>
    public void PurgeNullVolumes()
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] == null)
                RemoveActiveVolumeAt(i);
        }
    }

    /// <summary>Despawns and cleans all active volumes.</summary>
    public void ClearAllVolumes()
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] != null)
            {
                HectonVoxelVolume voxelVolume = _activeVolumeComponents.Count > i ? _activeVolumeComponents[i] : null;
                MeshFilter mf = ResolvePooledMeshFilter(_activeVolumes[i], voxelVolume);
                MeshCollider mc = ResolvePooledMeshCollider(_activeVolumes[i], voxelVolume);
                if (mc != null) mc.enabled = false;
                if (voxelVolume != null)
                    voxelVolume.PrepareForReuse();

                if (TryResolveCachedObjectPool(out IObjectPoolService pool) && voxelVolumePrefab != null)
                {
                    VoxelVolumeLeakSentinel.MarkReleasedToPool(voxelVolume);
                    ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: false);
                    pool.Despawn(_activeVolumes[i]);
                }
                else
                {
                    VoxelVolumeLeakSentinel.MarkDestroyRequested(voxelVolume);
                    ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: true);
                    SafeDestroy(_activeVolumes[i]);
                }
            }
        }
        _activeVolumes.Clear();
        _activeVolumeComponents.Clear();
        _activeVolumeLocalBounds.Clear();
    }

    public int ActiveVolumeCount => _activeVolumes.Count;

    public bool TryGetNearestActiveVolume(Vector3 worldPosition, out Hecton8.Caves.HectonVoxelVolume nearestVolume)
    {
        nearestVolume = null;
        if (!TryResolveRuntimeAup(worldPosition, out AbsoluteUniversePosition queryAup))
            return false;

        double bestSqrDistance = double.PositiveInfinity;

        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData ||
                volume.BakeState != VoxelBakeState.Complete)
            {
                continue;
            }

            AbsoluteUniversePosition volumeAup = AbsoluteUniversePosition.FromAbsolutePosition(volume.GenerationAbsoluteUniversePositionDouble);
            double sqrDistance = AbsoluteUniversePosition.DistanceSq(in volumeAup, in queryAup);
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            nearestVolume = volume;
        }

        return nearestVolume != null;
    }

    public bool TryReadNearestSonarSdf(
        float3 runtimeOrigin,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange)
    {
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        cellSize = default;
        sdfRange = 0f;
        // Bulk payload reads cannot carry a lease; consumers must use the lease API or surface resolver.
        return false;
    }

    public bool TryAcquireNearestSonarSdfReadLease(
        float3 runtimeOrigin,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange,
        out Hecton8.Core.Contracts.VoxelSonarSdfReadLease lease)
    {
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        cellSize = default;
        sdfRange = 0f;
        lease = default;
        if (!math.all(math.isfinite(runtimeOrigin)))
            return false;

        Vector3 origin = new Vector3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z);
        if (!TryAcquireNearestActiveSonarSdfPayloadReadLease(
                origin,
                out NativeArray<byte>.ReadOnly payload,
                out Vector3Int dimensions,
                out Vector3 payloadOrigin,
                out Vector3 payloadCellSize,
                out float payloadRange,
                out int version,
                out HectonVoxelVolume.PublishedSonarSdfReadLease volumeLease))
        {
            return false;
        }

        bool accepted = false;
        try
        {
            int3 resolvedDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
            float3 resolvedOrigin = new float3(payloadOrigin.x, payloadOrigin.y, payloadOrigin.z);
            float3 resolvedCellSize = new float3(payloadCellSize.x, payloadCellSize.y, payloadCellSize.z);
            if (!payload.IsCreated ||
                !math.all(resolvedDimensions > 0) ||
                !math.all(math.isfinite(resolvedOrigin)) ||
                !math.all(math.isfinite(resolvedCellSize)) ||
                !math.isfinite(payloadRange) ||
                payloadRange <= 0f)
            {
                return false;
            }

            if (!TryTrackNearestSonarSdfReadLease(
                    volumeLease.Vault,
                    volumeLease.SdfGeneration,
                    version,
                    volumeLease.MutationGuardMask))
                return false;

            encodedSdf = payload;
            gridDimensions = resolvedDimensions;
            volumeOrigin = resolvedOrigin;
            cellSize = resolvedCellSize;
            sdfRange = payloadRange;
            lease = new Hecton8.Core.Contracts.VoxelSonarSdfReadLease
            {
                SdfGeneration = volumeLease.SdfGeneration,
                AudioMaterialGeneration = 0u,
                Version = version,
                Flags = Hecton8.Core.Contracts.VoxelSonarSdfReadLease.FlagValid
            };
            accepted = true;
            return true;
        }
        finally
        {
            if (!accepted && volumeLease.Owner != null)
                volumeLease.Owner.ReleasePublishedSonarSdfPayloadReadLease(in volumeLease);
        }
    }

    public void ReleaseNearestSonarSdfReadLease(in Hecton8.Core.Contracts.VoxelSonarSdfReadLease lease)
    {
        if (!lease.IsValid)
            return;

        TryReleaseTrackedNearestSonarSdfReadLease(in lease);
    }

    bool TryTrackNearestSonarSdfReadLease(IDataVault vault, uint sdfGeneration, int version, ulong guardMask)
    {
        if (vault == null ||
            sdfGeneration == 0u ||
            version <= 0 ||
            guardMask != HectonVoxelVolume.PublishedSonarPayloadReadGuardMask)
            return false;

        for (int i = 0; i < NearestSonarSdfReadLeaseTrackerCapacity; i++)
        {
            if (_nearestSonarSdfReadLeaseRefCounts[i] == 0)
            {
                continue;
            }

            if (_nearestSonarSdfReadLeaseGenerations[i] != sdfGeneration ||
                _nearestSonarSdfReadLeaseVersions[i] != version)
            {
                continue;
            }

            // Public lease DTO has no vault token; reject ambiguous keys instead of guessing on release.
            if (_nearestSonarSdfReadLeaseVaults[i] != vault)
                return false;

            if (_nearestSonarSdfReadLeaseRefCounts[i] == ushort.MaxValue)
                return false;

            _nearestSonarSdfReadLeaseRefCounts[i]++;
            return true;
        }

        for (int i = 0; i < NearestSonarSdfReadLeaseTrackerCapacity; i++)
        {
            if (_nearestSonarSdfReadLeaseRefCounts[i] != 0)
                continue;

            _nearestSonarSdfReadLeaseVaults[i] = vault;
            _nearestSonarSdfReadLeaseGenerations[i] = sdfGeneration;
            _nearestSonarSdfReadLeaseVersions[i] = version;
            _nearestSonarSdfReadLeaseRefCounts[i] = 1;
            return true;
        }

        return false;
    }

    bool TryReleaseTrackedNearestSonarSdfReadLease(in Hecton8.Core.Contracts.VoxelSonarSdfReadLease lease)
    {
        for (int i = 0; i < NearestSonarSdfReadLeaseTrackerCapacity; i++)
        {
            if (_nearestSonarSdfReadLeaseRefCounts[i] == 0 ||
                _nearestSonarSdfReadLeaseGenerations[i] != lease.SdfGeneration ||
                _nearestSonarSdfReadLeaseVersions[i] != lease.Version)
            {
                continue;
            }

            IDataVault vault = _nearestSonarSdfReadLeaseVaults[i];
            if (vault == null)
            {
                ClearNearestSonarSdfReadLeaseTrackerSlot(i);
                return false;
            }

            HectonVoxelVolume.ReleasePublishedSonarPayloadReadGuard(
                vault,
                HectonVoxelVolume.PublishedSonarPayloadReadGuardMask);

            ushort refCount = _nearestSonarSdfReadLeaseRefCounts[i];
            if (refCount <= 1)
                ClearNearestSonarSdfReadLeaseTrackerSlot(i);
            else
                _nearestSonarSdfReadLeaseRefCounts[i] = (ushort)(refCount - 1);

            return true;
        }

        return false;
    }

    void ClearNearestSonarSdfReadLeaseTrackerSlot(int index)
    {
        if ((uint)index >= NearestSonarSdfReadLeaseTrackerCapacity)
            return;

        _nearestSonarSdfReadLeaseVaults[index] = null;
        _nearestSonarSdfReadLeaseGenerations[index] = 0u;
        _nearestSonarSdfReadLeaseVersions[index] = 0;
        _nearestSonarSdfReadLeaseRefCounts[index] = 0;
    }

    void ReleaseTrackedNearestSonarSdfReadLeases()
    {
        for (int i = 0; i < NearestSonarSdfReadLeaseTrackerCapacity; i++)
        {
            ushort refCount = _nearestSonarSdfReadLeaseRefCounts[i];
            IDataVault vault = _nearestSonarSdfReadLeaseVaults[i];
            for (int releaseIndex = 0; releaseIndex < refCount && vault != null; releaseIndex++)
            {
                HectonVoxelVolume.ReleasePublishedSonarPayloadReadGuard(
                    vault,
                    HectonVoxelVolume.PublishedSonarPayloadReadGuardMask);
            }

            ClearNearestSonarSdfReadLeaseTrackerSlot(i);
        }
    }

    public bool TryRaymarchNearestSonarSdf(
        float3 runtimeOrigin,
        float3 runtimeDirection,
        float maxDistance,
        float stepMeters,
        out VoxelSonarSdfRaycastHit hit,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange)
    {
        return TryResolveNearestSonarSdfSurfaceCore(
            runtimeOrigin,
            runtimeDirection,
            maxDistance,
            stepMeters,
            out hit,
            out encodedSdf,
            out gridDimensions,
            out volumeOrigin,
            out cellSize,
            out sdfRange);
    }

    public bool TryResolveNearestSonarSdfSurface(
        float3 runtimeOrigin,
        float3 runtimeDirection,
        float maxDistance,
        float stepMeters,
        out VoxelSonarSdfRaycastHit hit,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange)
    {
        return TryResolveNearestSonarSdfSurfaceCore(
            runtimeOrigin,
            runtimeDirection,
            maxDistance,
            stepMeters,
            out hit,
            out encodedSdf,
            out gridDimensions,
            out volumeOrigin,
            out cellSize,
            out sdfRange);
    }

    private bool TryResolveNearestSonarSdfSurfaceCore(
        float3 runtimeOrigin,
        float3 runtimeDirection,
        float maxDistance,
        float stepMeters,
        out VoxelSonarSdfRaycastHit hit,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange)
    {
        hit = default;
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        cellSize = default;
        sdfRange = 0f;
        if (!math.all(math.isfinite(runtimeOrigin)) ||
            !math.all(math.isfinite(runtimeDirection)) ||
            !math.isfinite(maxDistance) ||
            maxDistance <= 0f)
        {
            return false;
        }

        float safeStepMeters = math.max(0.05f, math.isfinite(stepMeters) ? stepMeters : 0.05f);
        float bestDistance = float.MaxValue;
        bool resolved = false;

        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData)
            {
                continue;
            }

            if (!volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly candidateSdf,
                    out Vector3Int candidateDimensions,
                    out Vector3 candidateOrigin,
                    out Vector3 candidateCellSize,
                    out float candidateRange,
                    out int candidateVersion,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease candidateLease))
            {
                continue;
            }

            try
            {
                int3 candidateGridDimensions = new int3(candidateDimensions.x, candidateDimensions.y, candidateDimensions.z);
                float3 candidateVolumeOrigin = new float3(candidateOrigin.x, candidateOrigin.y, candidateOrigin.z);
                float3 candidateVoxelCellSize = new float3(candidateCellSize.x, candidateCellSize.y, candidateCellSize.z);
                if (!VoxelSonarSdfMath.TryRaymarchEncodedSdf(
                        candidateSdf,
                        candidateGridDimensions,
                        candidateVolumeOrigin,
                        candidateVoxelCellSize,
                        candidateRange,
                        runtimeOrigin,
                        runtimeDirection,
                        maxDistance,
                        safeStepMeters,
                        out VoxelSonarSdfRaycastHit candidateHit) ||
                    (candidateHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                    candidateHit.Distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = candidateHit.Distance;
                hit.Point = candidateHit.Point;
                hit.Normal = candidateHit.Normal;
                hit.Distance = math.max(0f, candidateHit.Distance);
                hit.Density = math.isfinite(candidateHit.Density) ? candidateHit.Density : 0f;
                hit.Density01 = math.saturate(math.max(0f, hit.Density) * math.rcp(math.max(0.0001f, candidateRange)));
                hit.SdfRange = candidateRange;
                hit.Version = candidateVersion;
                hit.Flags = VoxelSonarSdfRaycastHit.FlagHit;
                encodedSdf = default;
                gridDimensions = candidateGridDimensions;
                volumeOrigin = candidateVolumeOrigin;
                cellSize = candidateVoxelCellSize;
                sdfRange = candidateRange;
                resolved = true;
            }
            finally
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in candidateLease);
            }
        }

        return resolved;
    }

    public bool TrySampleNearestSonarSdf(
        float3 runtimePosition,
        out float density,
        out float density01)
    {
        density = 0f;
        density01 = 0f;
        if (!math.all(math.isfinite(runtimePosition)))
            return false;

        Vector3 position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        float bestBoundsDistanceSq = float.MaxValue;
        bool resolved = false;

        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData ||
                !volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly candidateSdf,
                    out Vector3Int dimensions,
                    out Vector3 payloadOrigin,
                    out Vector3 payloadCellSize,
                    out float candidateRange,
                    out int _,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease candidateLease))
            {
                continue;
            }

            try
            {
                float boundsDistanceSq = ResolveSdfPayloadBoundsDistanceSq(position, payloadOrigin, dimensions, payloadCellSize);
                if (boundsDistanceSq >= bestBoundsDistanceSq)
                    continue;

                int3 candidateGridDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
                float3 candidateVolumeOrigin = new float3(payloadOrigin.x, payloadOrigin.y, payloadOrigin.z);
                float3 candidateVoxelCellSize = new float3(payloadCellSize.x, payloadCellSize.y, payloadCellSize.z);
                if (!VoxelSonarSdfMath.TrySampleEncodedSdfTrilinear(
                        candidateSdf,
                        candidateGridDimensions,
                        candidateVolumeOrigin,
                        candidateVoxelCellSize,
                        candidateRange,
                        runtimePosition,
                        out float candidateDensity))
                {
                    continue;
                }

                float candidateDensity01 = math.saturate(math.max(0f, candidateDensity) * math.rcp(math.max(0.0001f, candidateRange)));

                bestBoundsDistanceSq = boundsDistanceSq;
                density = candidateDensity;
                density01 = candidateDensity01;
                resolved = true;
            }
            finally
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in candidateLease);
            }
        }

        return resolved;
    }

    private bool TryAcquireNearestActiveSonarSdfPayloadReadLease(
        Vector3 runtimeOrigin,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out Vector3Int gridDimensions,
        out Vector3 volumeOrigin,
        out Vector3 voxelCellSize,
        out float sdfRange,
        out int version,
        out HectonVoxelVolume.PublishedSonarSdfReadLease lease)
    {
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        voxelCellSize = default;
        sdfRange = 0f;
        version = 0;
        lease = default;

        float bestDistanceSq = float.MaxValue;
        bool resolved = false;
        HectonVoxelVolume bestVolume = null;
        HectonVoxelVolume.PublishedSonarSdfReadLease bestLease = default;
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData ||
                !volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly candidateSdf,
                    out Vector3Int candidateDimensions,
                    out Vector3 candidateOrigin,
                    out Vector3 candidateCellSize,
                    out float candidateSdfRange,
                    out int candidateVersion,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease candidateLease))
            {
                continue;
            }

            bool keepCandidate = false;
            try
            {
                Vector3 center = candidateOrigin + new Vector3(
                    candidateCellSize.x * math.max(0, candidateDimensions.x - 1) * 0.5f,
                    candidateCellSize.y * math.max(0, candidateDimensions.y - 1) * 0.5f,
                    candidateCellSize.z * math.max(0, candidateDimensions.z - 1) * 0.5f);
                float distanceSq = (center - runtimeOrigin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                if (resolved && bestVolume != null)
                    bestVolume.ReleasePublishedSonarSdfPayloadReadLease(in bestLease);

                bestDistanceSq = distanceSq;
                bestVolume = volume;
                bestLease = candidateLease;
                encodedSdf = candidateSdf;
                gridDimensions = candidateDimensions;
                volumeOrigin = candidateOrigin;
                voxelCellSize = candidateCellSize;
                sdfRange = candidateSdfRange;
                version = candidateVersion;
                resolved = true;
                keepCandidate = true;
            }
            finally
            {
                if (!keepCandidate)
                    volume.ReleasePublishedSonarSdfPayloadReadLease(in candidateLease);
            }
        }

        if (!resolved)
            return false;

        lease = bestLease;
        return true;
    }

    private static float ResolveSdfPayloadBoundsDistanceSq(
        Vector3 position,
        Vector3 origin,
        Vector3Int dimensions,
        Vector3 cellSize)
    {
        Vector3 max = origin + new Vector3(
            cellSize.x * math.max(0, dimensions.x - 1),
            cellSize.y * math.max(0, dimensions.y - 1),
            cellSize.z * math.max(0, dimensions.z - 1));
        float dx = position.x < origin.x ? origin.x - position.x : (position.x > max.x ? position.x - max.x : 0f);
        float dy = position.y < origin.y ? origin.y - position.y : (position.y > max.y ? position.y - max.y : 0f);
        float dz = position.z < origin.z ? origin.z - position.z : (position.z > max.z ? position.z - max.z : 0f);
        return dx * dx + dy * dy + dz * dz;
    }

    void TeardownRuntimeState()
    {
        bool runtimeStateWasLive =
            _registeredLiveEngine ||
            ReferenceEquals(s_activeRuntimeInstance, this) ||
            _activeVolumes.Count > 0;

        if (!Application.isPlaying && !runtimeStateWasLive)
            return;

        ClearAllVolumes();
        ReleaseTrackedNearestSonarSdfReadLeases();
        _teardownStreamingScratchRequested = true;
        TryFinalizeStreamingScratchTeardown();

        if (ReferenceEquals(s_activeRuntimeInstance, this))
        {
            s_activeRuntimeInstance = null;
            s_predictiveVoxelProxyPhysicsService = null;
            s_playerRuntimeContext = null;
        }

        if (ReferenceEquals(GlobalRegistry.VoxelEngine, this))
            GlobalRegistry.UnregisterVoxelEngineRuntime(this);

        TryUnregisterRuntimeServiceHotSwapListener();
        _objectPoolService = null;
        _physicsService = null;
        _vramPressureReadModel = null;
        _lodSystemManager = null;
        _scavengePopulator = null;
        _vegetationBridge = null;
        _resourceDistributionDirector = null;

        if (_registeredLiveEngine)
        {
            _registeredLiveEngine = false;
            if (Interlocked.Decrement(ref _liveEngineCount) <= 0)
            {
                RequestSharedTableShutdown();
                ResetPredictiveVoxelProxyCinematicState();
                ResetVoxelProxyLayerFilteringState();
            }
        }

    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _bakeGhostMaterialGapAnnounced;
#endif

    /// <summary>
    /// Announces a missing bake-ghost material once. It no longer throws, and that is the whole point.
    ///
    /// This method used to be nothing but UnityEngine.Assertions.Assert.IsNotNull, which THROWS in this
    /// project - nothing under Assets sets Assert.raiseExceptions false. It is called from OnEnable, and the
    /// SEVEN statements after that call therefore never ran: EnsureVoxelMeshPipelineBlackBox,
    /// EnsureStreamingScratchSlots, HectonVoxelVolume.TryEnsurePublishedSonarVaultPayloadCapacity,
    /// WarmVoxelMeshPoolsAsync, CacheVoxelDeltaProcessorCold, and MCTables.Initialize.
    ///
    /// So runtime voxel carving and delta-save replay were dead because _deltaProcessor was never cached, and
    /// the marching-cubes tables were never initialised - all to guard a cosmetic material.
    ///
    /// Runtime-proven, not inferred: Logs/omega_route22.log:7192 and :7809 show it throwing TWICE per run
    /// with "Assertion failure. Value was Null", entered through WorldRuntimeInstaller's
    /// GameObject.SetActive(true).
    ///
    /// The assert was indefensible because the material is optional by construction. Its sole consumer,
    /// HectonVoxelVolume.cs:4142-4144, already reads
    /// ResolvedVoxelBakeGhostMaterial != null ? ResolvedVoxelBakeGhostMaterial : voxelMaterial - a null
    /// simply falls back to the normal voxel material. A survivable cosmetic gap was costing this engine its
    /// entire cold-init tail.
    ///
    /// The gap itself is real and still wants authoring: voxelBakeGhostMaterial is a serialized field on this
    /// component and is null on the world runtime root today. This says so once, in the log, instead of
    /// unwinding OnEnable.
    /// </summary>
    void EnsureVoxelBakeGhostMaterial()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (voxelBakeGhostMaterial != null || _bakeGhostMaterialGapAnnounced)
            return;

        _bakeGhostMaterialGapAnnounced = true;
        Debug.LogWarning(
            "[HectonVoxelEngine] voxelBakeGhostMaterial is not assigned on '" + name +
            "'. Bake-ghost visuals fall back to voxelMaterial (HectonVoxelVolume.cs:4142-4144), so this is " +
            "cosmetic and NOT fatal - it used to abort OnEnable and take MCTables.Initialize and " +
            "CacheVoxelDeltaProcessorCold with it. Assign the field to restore the bake-ghost look. " +
            "Reported once per engine instance.",
            this);
#endif
    }

    static async Awaitable AwaitForJobCompletionAsync(JobHandle handle, CancellationToken ct, string context)
    {
        int waitFrames = 0;
        bool cancellationRequested = false;
        bool watchdogLogged = false;
        while (!handle.IsCompleted)
        {
            if (!cancellationRequested && ct.IsCancellationRequested)
                cancellationRequested = true;

            if (!watchdogLogged && waitFrames >= VoxelJobWaitWatchdogFrames)
            {
                LogVoxelJobWaitWatchdog(context, waitFrames);
                watchdogLogged = true;
            }

            waitFrames++;
            await AwaitableDebtMonitor.NextFrameAsync();
        }

        DispatcherJobSwap.TryFinalizeCompleted(ref handle);

        if (cancellationRequested)
            return;
    }

    static async Awaitable<long> YieldIfChunkGenerationBudgetExpiredAsync(long frameStartTimestamp, CancellationToken ct)
    {
        if (Stopwatch.GetTimestamp() - frameStartTimestamp < ChunkGenerationFrameBudgetTicks)
            return frameStartTimestamp;

        if (ct.IsCancellationRequested)
            return frameStartTimestamp;

        await AwaitableDebtMonitor.NextFrameAsync();
        if (ct.IsCancellationRequested)
            return frameStartTimestamp;

        return Stopwatch.GetTimestamp();
    }

    static async Awaitable<bool> AwaitForPhysicsBakeCompletionOrDeferAsync(
        JobHandle handle,
        CancellationToken ct,
        string context,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags,
        BoxCollider proxyCollider = null)
    {
        int waitFrames = 0;
        while (!handle.IsCompleted)
        {
            if (ct.IsCancellationRequested)
            {
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags, proxyCollider);
                return false;
            }

            if (waitFrames >= VoxelJobWaitWatchdogFrames)
            {
                LogVoxelJobWaitWatchdog(context, waitFrames);
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags, proxyCollider);
                return false;
            }

            waitFrames++;
            await AwaitableDebtMonitor.NextFrameAsync();
        }

        return DispatcherJobSwap.TryFinalizeCompleted(ref handle);
    }

    static async Awaitable AwaitVoxelMeshUploadBudgetAsync(CancellationToken ct)
    {
        while (true)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_voxelMeshUploadFrame != frame)
            {
                _voxelMeshUploadFrame = frame;
                _voxelMeshUploadsThisFrame = 0;
                float frameBudget = ResolveVoxelMeshUploadBudgetPerFrame();
                float frameCap = Mathf.Clamp(
                    Mathf.Ceil(frameBudget - VoxelMeshUploadBurstCapBias),
                    VoxelMeshUploadBudgetPerFrame,
                    VoxelMeshUploadBudgetVisualOverkillPerFrame);
                _voxelMeshUploadBudgetTokens = Mathf.Min(frameCap, _voxelMeshUploadBudgetTokens + frameBudget);
            }

            if (_voxelMeshUploadBudgetTokens >= 1f)
            {
                _voxelMeshUploadBudgetTokens -= 1f;
                _voxelMeshUploadsThisFrame++;
                return;
            }

            if (ct.IsCancellationRequested)
                return;

            await AwaitableDebtMonitor.NextFrameAsync();
        }
    }

    private static float ResolveVoxelMeshUploadBudgetPerFrame()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        return Mathf.Lerp(VoxelMeshUploadBudgetPerFrame, VoxelMeshUploadBudgetVisualOverkillPerFrame, smooth);
    }

    private static void EnsureVoxelProxyLayerFiltering()
    {
        if (_voxelProxyLayerFilteringConfigured)
            return;

        Physics.IgnoreLayerCollision(HectonLayerMasks.Player, HectonLayerMasks.VoxelProxy, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.Vehicle, HectonLayerMasks.VoxelProxy, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.PlayerVehicle, HectonLayerMasks.VoxelProxy, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.VoxelProxy, HectonLayerMasks.IgnoreRaycast, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.VoxelProxy, HectonLayerMasks.UI, true);

        _voxelProxyLayerFilteringConfigured = true;
    }

    private static void ResetVoxelProxyLayerFilteringState()
    {
        _voxelProxyLayerFilteringConfigured = false;
    }

    private static void ResetPredictiveVoxelProxyCinematicState()
    {
        _predictiveVoxelProxyLastFrame = -1;
    }

    private static void ApplyPredictiveVoxelProxyCinematicGate()
    {
        int frame = SystemDispatcher.CurrentFrameIndex;
        if (_predictiveVoxelProxyLastFrame == frame)
            return;

        _predictiveVoxelProxyLastFrame = frame;

        if (!HasDeferredVoxelProxyCandidate() ||
            !TryResolvePredictiveVoxelProxyTarget(
                out Rigidbody targetBody,
                out HectonPlayerMovement targetMovement,
                out ITransportPredictiveVoxelProxySource targetVehicle,
                out Vector3 origin,
                out Vector3 velocity))
        {
            return;
        }

        float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
        float speedSq = math.lengthsq(velocity3);
        float minSpeedSq = PredictiveVoxelProxyMinSpeedMetersPerSecond * PredictiveVoxelProxyMinSpeedMetersPerSecond;
        if (speedSq <= minSpeedSq)
            return;

        float3 lookaheadOffset = velocity3 * PredictiveVoxelProxyLookaheadSeconds;
        float lookaheadSq = math.lengthsq(lookaheadOffset);
        float maxDistanceSq = PredictiveVoxelProxyMaxDistanceMeters * PredictiveVoxelProxyMaxDistanceMeters;
        if (lookaheadSq > maxDistanceSq)
            lookaheadOffset *= PredictiveVoxelProxyMaxDistanceMeters / math.max(LengthApprox(lookaheadOffset), 0.0001f);

        Vector3 predicted = origin + new Vector3(lookaheadOffset.x, lookaheadOffset.y, lookaheadOffset.z);
        if (!PathIntersectsDeferredVoxelProxyAup(origin, predicted))
            return;

        ApplyPredictiveVoxelProxyDampener(targetBody, targetMovement, targetVehicle, velocity);
    }

    private static bool HasDeferredVoxelProxyCandidate()
    {
        return DeferredVoxelPhysicsBakePendingCount > 0 ||
               _deferredVoxelColliderUploads.Count > 0;
    }

    private static bool PathIntersectsDeferredVoxelProxyAup(Vector3 runtimeStart, Vector3 runtimeEnd)
    {
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup) ||
            !TryResolveRuntimeAbsoluteDouble(runtimeStart, in originAup, out double3 startAup) ||
            !TryResolveRuntimeAbsoluteDouble(runtimeEnd, in originAup, out double3 endAup))
        {
            return false;
        }

        double padding = PredictiveVoxelProxyCinematicPaddingMeters;
        double3 pathMin = math.min(startAup, endAup) - new double3(padding);
        double3 pathMax = math.max(startAup, endAup) + new double3(padding);
        uint currentShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;

        for (int i = 0; i < _deferredVoxelPhysicsBakeTeardowns.Count; i++)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[i];
            if (pending.ProxyShiftSequence != currentShiftSequence)
            {
                RefreshDeferredVoxelTeardownProxyBounds(ref pending, currentShiftSequence);
                _deferredVoxelPhysicsBakeTeardowns[i] = pending;
            }

            if (DeferredVoxelProxyIntersectsAupPath(
                    pending.ProxyMinAup,
                    pending.ProxyMaxAup,
                    pending.HasProxyBounds,
                    pathMin,
                    pathMax))
            {
                return true;
            }
        }

        for (int i = 0; i < _deferredVoxelPhysicsBakeEmergencyCount; i++)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[i];
            if (pending.ProxyShiftSequence != currentShiftSequence)
            {
                RefreshDeferredVoxelTeardownProxyBounds(ref pending, currentShiftSequence);
                _deferredVoxelPhysicsBakeEmergencyTeardowns[i] = pending;
            }

            if (DeferredVoxelProxyIntersectsAupPath(
                    pending.ProxyMinAup,
                    pending.ProxyMaxAup,
                    pending.HasProxyBounds,
                    pathMin,
                    pathMax))
            {
                return true;
            }
        }

        for (int i = 0; i < _deferredVoxelColliderUploads.Count; i++)
        {
            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[i];
            if (pending.ProxyShiftSequence != currentShiftSequence)
            {
                RefreshDeferredVoxelUploadProxyBounds(ref pending, currentShiftSequence);
                _deferredVoxelColliderUploads[i] = pending;
            }

            if (DeferredVoxelProxyIntersectsAupPath(
                    pending.ProxyMinAup,
                    pending.ProxyMaxAup,
                    pending.HasProxyBounds,
                    pathMin,
                    pathMax))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshDeferredVoxelTeardownProxyBounds(
        ref DeferredVoxelPhysicsBakeTeardown pending,
        uint currentShiftSequence)
    {
        pending.ProxyShiftSequence = currentShiftSequence;
        pending.HasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(
            pending.ProxyCollider,
            out pending.ProxyMinAup,
            out pending.ProxyMaxAup)
            ? (byte)1
            : (byte)0;
    }

    private static void RefreshDeferredVoxelUploadProxyBounds(
        ref DeferredVoxelColliderUpload pending,
        uint currentShiftSequence)
    {
        pending.ProxyShiftSequence = currentShiftSequence;
        pending.HasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(
            pending.ProxyCollider,
            out pending.ProxyMinAup,
            out pending.ProxyMaxAup)
            ? (byte)1
            : (byte)0;
    }

    private static bool DeferredVoxelProxyIntersectsAupPath(
        double3 proxyMinAup,
        double3 proxyMaxAup,
        byte hasProxyBounds,
        double3 pathMinAup,
        double3 pathMaxAup)
    {
        if (hasProxyBounds == 0 ||
            !IsFiniteDouble3(proxyMinAup) ||
            !IsFiniteDouble3(proxyMaxAup) ||
            !IsFiniteDouble3(pathMinAup) ||
            !IsFiniteDouble3(pathMaxAup))
        {
            return false;
        }

        return proxyMinAup.x <= pathMaxAup.x && proxyMaxAup.x >= pathMinAup.x &&
               proxyMinAup.y <= pathMaxAup.y && proxyMaxAup.y >= pathMinAup.y &&
               proxyMinAup.z <= pathMaxAup.z && proxyMaxAup.z >= pathMinAup.z;
    }

    private static bool TryCacheDeferredVoxelProxyAupBounds(BoxCollider proxy, out double3 proxyMinAup, out double3 proxyMaxAup)
    {
        proxyMinAup = default;
        proxyMaxAup = default;
        if (proxy == null)
            return false;

        Bounds bounds = proxy.bounds;
        if (!IsFiniteVector(bounds.min) ||
            !IsFiniteVector(bounds.max) ||
            !IsFiniteVector(bounds.size) ||
            !math.isfinite(bounds.size.sqrMagnitude) ||
            bounds.size.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup) ||
            !TryResolveRuntimeAbsoluteDouble(bounds.min, in originAup, out double3 minAup) ||
            !TryResolveRuntimeAbsoluteDouble(bounds.max, in originAup, out double3 maxAup))
        {
            return false;
        }

        double padding = PredictiveVoxelProxyCinematicPaddingMeters;
        if (!math.isfinite(padding) || padding < 0d)
            return false;

        proxyMinAup = minAup - new double3(padding);
        proxyMaxAup = maxAup + new double3(padding);
        return IsFiniteDouble3(proxyMinAup) && IsFiniteDouble3(proxyMaxAup);
    }

    private static void EnableVoxelProxyCollider(BoxCollider proxyCollider)
    {
        if (proxyCollider == null)
            return;

        EnsureVoxelProxyLayerFiltering();
        proxyCollider.gameObject.layer = HectonLayerMasks.VoxelProxy;
        if (!proxyCollider.gameObject.activeSelf)
            proxyCollider.gameObject.SetActive(true);

        proxyCollider.enabled = true;
    }

    private static bool TryResolvePredictiveVoxelProxyTarget(
        out Rigidbody targetBody,
        out HectonPlayerMovement targetMovement,
        out ITransportPredictiveVoxelProxySource targetVehicle,
        out Vector3 origin,
        out Vector3 velocity)
    {
        targetBody = null;
        targetMovement = null;
        targetVehicle = null;
        origin = Vector3.zero;
        velocity = Vector3.zero;

        IPlayerRuntimeContext playerRuntimeContext = s_playerRuntimeContext;
        targetMovement = playerRuntimeContext != null ? playerRuntimeContext.PlayerMovement : null;
        if (targetMovement != null &&
            targetMovement.TryGetActiveTransportPlatform(out ITransportPlatform platform) &&
            platform != null)
        {
            targetVehicle = platform as ITransportPredictiveVoxelProxySource;
            if (targetVehicle != null && targetVehicle.TryResolvePredictiveVoxelProxy(out targetBody, out velocity))
            {
                origin = targetBody != null ? targetBody.worldCenterOfMass : Vector3.zero;
                return true;
            }

            if (platform is ISubmarineRuntimeContext submarineRuntimeContext &&
                submarineRuntimeContext.HullRigidbody != null)
            {
                targetBody = submarineRuntimeContext.HullRigidbody;
                velocity = HectonPlayerMotor.SafeVelocity(targetBody.linearVelocity);
                origin = targetBody.worldCenterOfMass;
                return true;
            }
        }

        Transform playerTransform = playerRuntimeContext != null ? playerRuntimeContext.PlayerTransform : null;
        if (playerTransform == null && targetMovement != null)
            playerTransform = targetMovement.transform;

        if (playerTransform == null)
            return false;

        origin = playerTransform.position;
        velocity = CoreDeterminismSignals.TryGetLatestKccVelocityVector(PredictiveVoxelProxyKccVelocityMaxAgeFrames, out Vector3 kccVelocity)
            ? kccVelocity
            : (targetMovement != null ? targetMovement.CurrentWorldVelocity : Vector3.zero);
        return true;
    }

    private static void ApplyPredictiveVoxelProxyDampener(
        Rigidbody targetBody,
        HectonPlayerMovement targetMovement,
        ITransportPredictiveVoxelProxySource targetVehicle,
        Vector3 sampledVelocity)
    {
        if (sampledVelocity.y >= -0.01f)
            return;

        if (targetVehicle != null)
        {
            targetVehicle.ApplyPredictiveVoxelProxyDampener(PredictiveVoxelProxyDampenerStrength01);
            return;
        }

        Vector3 upwardCorrection = Vector3.up * (-sampledVelocity.y * PredictiveVoxelProxyDampenerStrength01);
        if (targetMovement != null)
        {
            targetMovement.QueueSubsystemExternalVelocityChange(upwardCorrection);
            return;
        }

        if (targetBody == null)
            return;

        Vector3 velocity = HectonPlayerMotor.SafeVelocity(targetBody.linearVelocity);
        if (velocity.y < 0f)
        {
            velocity.y = math.lerp(velocity.y, 0f, PredictiveVoxelProxyDampenerStrength01);
            s_predictiveVoxelProxyPhysicsService?.QueueLinearVelocitySet(targetBody, velocity);
        }
    }

    private static void EnqueueDeferredVoxelPhysicsBakeTeardown(
        JobHandle handle,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags,
        BoxCollider proxyCollider)
    {
        EnsureVoxelProxyLayerFiltering();
        DisableDeferredVoxelBakePresentation(owner, renderer, collider, flags);
        EnableVoxelProxyCollider(proxyCollider);

        uint proxyShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
        bool hasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(proxyCollider, out double3 proxyMinAup, out double3 proxyMaxAup);
        DeferredVoxelPhysicsBakeTeardown pending = new DeferredVoxelPhysicsBakeTeardown
        {
            Mesh = mesh,
            Owner = owner,
            Renderer = renderer,
            Collider = collider,
            ProxyCollider = proxyCollider,
            Handle = handle,
            ProxyMinAup = proxyMinAup,
            ProxyMaxAup = proxyMaxAup,
            ProxyShiftSequence = proxyShiftSequence,
            Flags = flags,
            HasProxyBounds = hasProxyBounds ? (byte)1 : (byte)0
        };

        if (_deferredVoxelPhysicsBakeTeardowns.Count >= DeferredVoxelPhysicsBakeTeardownCapacity)
        {
            DrainCompletedDeferredVoxelPhysicsBakeTeardownsForCapacity();
            if (_deferredVoxelPhysicsBakeTeardowns.Count >= DeferredVoxelPhysicsBakeTeardownCapacity)
            {
                if (!TryEnqueueDeferredVoxelPhysicsBakeEmergencyTeardown(in pending))
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _VoxelPhysicsBakeForceReleaseWarningHash,
                        _VoxelPhysicsBakeContextHash,
                        DeferredVoxelPhysicsBakePendingCount);
                    WriteVoxelMeshPipelineBlackBoxSample(
                        Hecton8.Core.SystemDispatcher.CurrentFrameId,
                        VoxelMeshPipelineInvalidStateFlag | VoxelMeshPipelineEmergencyBakeTeardownFlag,
                        _voxelChunksMeshedThisFrame,
                        DeferredVoxelPhysicsBakePendingCount,
                        _deferredVoxelColliderUploads.Count);
                    UpdateDeferredVoxelPhysicsBakeBackpressure();
                    PublishVoxelMeshPipelineTelemetry();
                    return;
                }

                if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())
                {
                    UpdateDeferredVoxelPhysicsBakeBackpressure();
                    PublishVoxelMeshPipelineTelemetry();
                    return;
                }

                UpdateDeferredVoxelPhysicsBakeBackpressure();
                PublishVoxelMeshPipelineTelemetry();
                return;
            }
        }

        _deferredVoxelPhysicsBakeTeardowns.Add(pending);

        if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())
        {
            UpdateDeferredVoxelPhysicsBakeBackpressure();
            PublishVoxelMeshPipelineTelemetry();
            return;
        }

        UpdateDeferredVoxelPhysicsBakeBackpressure();
        PublishVoxelMeshPipelineTelemetry();
    }

    private static void ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly(
        JobHandle handle,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags,
        BoxCollider proxyCollider,
        bool publishWarning = true)
    {
        if (publishWarning)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelPhysicsBakeForceReleaseWarningHash,
                _VoxelPhysicsBakeContextHash,
                DeferredVoxelPhysicsBakePendingCount);
        }

        DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
        DisableDeferredVoxelBakePresentation(owner, renderer, collider, flags);

        EnableVoxelProxyCollider(proxyCollider);

        if (mesh != null)
        {
            mesh.Clear(false);
            if (!ReleaseVoxelPhysicsBakeMesh(mesh))
                DestroyDeferredVoxelObject(mesh);
        }

        if ((flags & DeferredVoxelBakeDestroyOwner) != 0 && owner != null)
            DestroyDeferredVoxelObject(owner);
    }

    private static void DisableDeferredVoxelBakePresentation(GameObject owner, MeshRenderer renderer, MeshCollider collider, byte flags)
    {
        bool destroyOwner = (flags & DeferredVoxelBakeDestroyOwner) != 0;
        if (destroyOwner)
        {
            if (renderer != null)
                renderer.enabled = false;
        }

        if (collider != null)
        {
            collider.enabled = false;
        }
    }

    private static bool EnsureDeferredVoxelPhysicsBakeTeardownRegistered()
    {
        if (_deferredVoxelPhysicsBakeTeardownRegistered)
            return true;

        if (!CanRegisterDeferredVoxelLateFrameWork())
            return false;

        _deferredVoxelPhysicsBakeTeardownRegistered = GlobalRegistry.TryRegisterLateFrameTickable(
            _deferredVoxelPhysicsBakeTeardownDriver,
            PriorityLayer.Environment);
        if (_deferredVoxelPhysicsBakeTeardownRegistered)
            TryRegisterDeferredVoxelHotSwapBridge();
        return _deferredVoxelPhysicsBakeTeardownRegistered;
    }

    private static void DrainDeferredVoxelPhysicsBakeTeardowns()
    {
        int pendingCount = _deferredVoxelPhysicsBakeTeardowns.Count;
        if (pendingCount <= 0 && _deferredVoxelPhysicsBakeEmergencyCount <= 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            UpdateDeferredVoxelPhysicsBakeBackpressure();
            TryShutdownSharedTables();
            return;
        }

        int drainBudget = ConsumeDeferredVoxelPhysicsBakeTeardownDrainBudgetThisFrame();
        int inspectionBudget = ResolveDeferredVoxelPhysicsBakeTeardownInspectionBudget(drainBudget);
        if (pendingCount > 0 && inspectionBudget > pendingCount)
            inspectionBudget = pendingCount;

        if (pendingCount > 0 &&
            (_deferredVoxelPhysicsBakeTeardownScanCursor < 0 ||
            _deferredVoxelPhysicsBakeTeardownScanCursor >= pendingCount))
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = pendingCount - 1;
        }

        int drained = 0;
        int inspected = 0;
        int index = _deferredVoxelPhysicsBakeTeardownScanCursor;
        while (pendingCount > 0 && inspected < inspectionBudget && drained < drainBudget)
        {
            if (index < 0)
                index = pendingCount - 1;
            else if (index >= pendingCount)
                index = pendingCount - 1;

            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[index];
            inspected++;
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
            {
                index--;
                continue;
            }

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeTeardownAt(index);
            drained++;
            pendingCount = _deferredVoxelPhysicsBakeTeardowns.Count;
            if (pendingCount == 0)
                break;

            if (index >= pendingCount)
                index = pendingCount - 1;
        }

        if (pendingCount > 0)
        {
            if (index < 0)
                index = pendingCount - 1;
            else if (index >= pendingCount)
                index = pendingCount - 1;
        }

        if (_deferredVoxelPhysicsBakeEmergencyCount > 0 && drained < drainBudget)
            drained += DrainDeferredVoxelPhysicsBakeEmergencyTeardowns(drainBudget - drained, inspectionBudget);

        _deferredVoxelPhysicsBakeTeardownScanCursor = pendingCount > 0 ? index : 0;
        if (DeferredVoxelPhysicsBakePendingCount == 0)
        {
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            TryShutdownSharedTables();
        }

        UpdateDeferredVoxelPhysicsBakeBackpressure();
    }

    private static int ConsumeDeferredVoxelPhysicsBakeTeardownDrainBudgetThisFrame()
    {
        int frame = SystemDispatcher.CurrentFrameIndex;
        if (_deferredVoxelPhysicsBakeTeardownFrame != frame)
        {
            _deferredVoxelPhysicsBakeTeardownFrame = frame;
            float frameBudget = ResolveDeferredVoxelPhysicsBakeTeardownBudgetPerFrame();
            float frameCap = Mathf.Clamp(
                Mathf.Ceil(frameBudget - DeferredVoxelPhysicsBakeTeardownBurstCapBias),
                DeferredVoxelPhysicsBakeTeardownBudgetPerFrame,
                DeferredVoxelPhysicsBakeTeardownBudgetVisualOverkillPerFrame);
            AccumulateDeferredVoxelBudgetTokens(
                ref _deferredVoxelPhysicsBakeTeardownBudgetTokens,
                frameCap,
                frameBudget);
        }

        return ConsumeDeferredVoxelBudgetTokens(
            ref _deferredVoxelPhysicsBakeTeardownBudgetTokens,
            (int)DeferredVoxelPhysicsBakeTeardownBudgetVisualOverkillPerFrame);
    }

    private static float ResolveDeferredVoxelPhysicsBakeTeardownBudgetPerFrame()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        float budget = Mathf.Lerp(
            DeferredVoxelPhysicsBakeTeardownBudgetPerFrame,
            DeferredVoxelPhysicsBakeTeardownBudgetVisualOverkillPerFrame,
            smooth);
        return _deferredVoxelPhysicsBakeBackpressureActive
            ? Mathf.Max(budget, DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget)
            : budget;
    }

    private static int ResolveDeferredVoxelPhysicsBakeTeardownInspectionBudget(int drainBudget)
    {
        int baseBudget = _deferredVoxelPhysicsBakeBackpressureActive
            ? DeferredVoxelPhysicsBakeTeardownBackpressureInspectionBudget
            : DeferredVoxelPhysicsBakeTeardownInspectionBudget;
        return math.max(drainBudget, baseBudget);
    }

    private static void DrainCompletedDeferredVoxelPhysicsBakeTeardownsForCapacity()
    {
        int drained = 0;
        for (int i = _deferredVoxelPhysicsBakeTeardowns.Count - 1;
             i >= 0 && drained < DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget;
             i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[i];
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
                continue;

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeTeardownAt(i);
            drained++;
        }

        DrainCompletedDeferredVoxelPhysicsBakeEmergencyTeardownsForCapacity();

        if (DeferredVoxelPhysicsBakePendingCount == 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            TryShutdownSharedTables();
        }
    }

    private static void FinalizeDeferredVoxelPhysicsBakeTeardown(ref DeferredVoxelPhysicsBakeTeardown pending)
    {
        if (pending.Collider != null)
        {
            pending.Collider.enabled = false;
        }

        EnableVoxelProxyCollider(pending.ProxyCollider);

        if (pending.Mesh != null)
        {
            pending.Mesh.Clear(false);
            if (!ReleaseVoxelPhysicsBakeMesh(pending.Mesh))
                DestroyDeferredVoxelObject(pending.Mesh);
        }

        if ((pending.Flags & DeferredVoxelBakeDestroyOwner) != 0 && pending.Owner != null)
            DestroyDeferredVoxelObject(pending.Owner);
    }

    private static void RemoveDeferredVoxelPhysicsBakeTeardownAt(int index)
    {
        if ((uint)index >= (uint)_deferredVoxelPhysicsBakeTeardowns.Count)
            return;

        int lastIndex = _deferredVoxelPhysicsBakeTeardowns.Count - 1;
        if (index != lastIndex)
            _deferredVoxelPhysicsBakeTeardowns[index] = _deferredVoxelPhysicsBakeTeardowns[lastIndex];

        _deferredVoxelPhysicsBakeTeardowns.RemoveAt(lastIndex);
    }

    private static bool TryEnqueueDeferredVoxelPhysicsBakeEmergencyTeardown(in DeferredVoxelPhysicsBakeTeardown pending)
    {
        if (_deferredVoxelPhysicsBakeEmergencyCount >= DeferredVoxelPhysicsBakeEmergencyTeardownCapacity)
            DrainCompletedDeferredVoxelPhysicsBakeEmergencyTeardownsForCapacity();

        if (_deferredVoxelPhysicsBakeEmergencyCount >= DeferredVoxelPhysicsBakeEmergencyTeardownCapacity)
            return false;

        _deferredVoxelPhysicsBakeEmergencyTeardowns[_deferredVoxelPhysicsBakeEmergencyCount] = pending;
        _deferredVoxelPhysicsBakeEmergencyScanCursor = _deferredVoxelPhysicsBakeEmergencyCount;
        _deferredVoxelPhysicsBakeEmergencyCount++;
        return true;
    }

    private static int DrainDeferredVoxelPhysicsBakeEmergencyTeardowns(int drainBudget, int inspectionBudget)
    {
        int pendingCount = _deferredVoxelPhysicsBakeEmergencyCount;
        if (pendingCount <= 0 || drainBudget <= 0 || inspectionBudget <= 0)
            return 0;

        if (inspectionBudget > pendingCount)
            inspectionBudget = pendingCount;

        if (_deferredVoxelPhysicsBakeEmergencyScanCursor < 0 ||
            _deferredVoxelPhysicsBakeEmergencyScanCursor >= pendingCount)
        {
            _deferredVoxelPhysicsBakeEmergencyScanCursor = pendingCount - 1;
        }

        int drained = 0;
        int inspected = 0;
        int index = _deferredVoxelPhysicsBakeEmergencyScanCursor;
        while (pendingCount > 0 && inspected < inspectionBudget && drained < drainBudget)
        {
            if (index < 0)
                index = pendingCount - 1;
            else if (index >= pendingCount)
                index = pendingCount - 1;

            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[index];
            inspected++;
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
            {
                _deferredVoxelPhysicsBakeEmergencyTeardowns[index] = pending;
                index--;
                continue;
            }

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(index);
            drained++;
            pendingCount = _deferredVoxelPhysicsBakeEmergencyCount;
            if (pendingCount == 0)
                break;

            if (index >= pendingCount)
                index = pendingCount - 1;
        }

        if (pendingCount > 0)
        {
            if (index < 0)
                index = pendingCount - 1;
            else if (index >= pendingCount)
                index = pendingCount - 1;
        }

        _deferredVoxelPhysicsBakeEmergencyScanCursor = pendingCount > 0 ? index : 0;
        return drained;
    }

    private static void DrainCompletedDeferredVoxelPhysicsBakeEmergencyTeardownsForCapacity()
    {
        int drained = 0;
        for (int i = _deferredVoxelPhysicsBakeEmergencyCount - 1;
             i >= 0 && drained < DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget;
             i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[i];
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
            {
                _deferredVoxelPhysicsBakeEmergencyTeardowns[i] = pending;
                continue;
            }

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(i);
            drained++;
        }
    }

    private static void RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(int index)
    {
        if ((uint)index >= (uint)_deferredVoxelPhysicsBakeEmergencyCount)
            return;

        int lastIndex = _deferredVoxelPhysicsBakeEmergencyCount - 1;
        if (index != lastIndex)
            _deferredVoxelPhysicsBakeEmergencyTeardowns[index] = _deferredVoxelPhysicsBakeEmergencyTeardowns[lastIndex];

        _deferredVoxelPhysicsBakeEmergencyTeardowns[lastIndex] = default;
        _deferredVoxelPhysicsBakeEmergencyCount = lastIndex;
    }

    private static void ClearDeferredVoxelPhysicsBakeEmergencyTeardowns()
    {
        for (int i = 0; i < _deferredVoxelPhysicsBakeEmergencyCount; i++)
            _deferredVoxelPhysicsBakeEmergencyTeardowns[i] = default;

        _deferredVoxelPhysicsBakeEmergencyCount = 0;
        _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
    }

    private static void UnregisterDeferredVoxelPhysicsBakeTeardownDriver()
    {
        if (!_deferredVoxelPhysicsBakeTeardownRegistered)
            return;

        GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelPhysicsBakeTeardownDriver, PriorityLayer.Environment);
        _deferredVoxelPhysicsBakeTeardownRegistered = false;
        TryUnregisterDeferredVoxelHotSwapBridgeIfIdle();
    }

    internal static bool EnqueueDeferredVoxelColliderUpload(Hecton8.Caves.HectonVoxelVolume volume, int chunkIndex)
    {
        if (volume == null || chunkIndex < 0)
            return false;

        // Fail closed to the box proxy: if the drain cannot take or service the request,
        // the chunk keeps its degraded primitive collision instead of none at all.
        if (!TryReserveDeferredVoxelColliderUploadSlot(null) ||
            !EnsureDeferredVoxelColliderUploadRegistered())
        {
            volume.EnableColliderChunkProxy(chunkIndex);
            return false;
        }

        _deferredVoxelColliderUploads.Add(new DeferredVoxelColliderUpload
        {
            Volume = volume,
            ChunkIndex = chunkIndex,
            Flags = DeferredVoxelColliderUploadVolumeFlag
        });
        return true;
    }

    internal static bool EnqueueDeferredVoxelColliderUpload(MeshCollider collider, Mesh mesh)
    {
        if (collider != null)
            collider.enabled = false;

        return false;
    }

    internal static bool EnqueueDeferredVoxelColliderUpload(MeshCollider collider, Mesh mesh, BoxCollider proxyCollider)
    {
        if (collider != null)
            collider.enabled = false;

        EnableVoxelProxyCollider(proxyCollider);

        return false;
    }

    private static void RefreshDeferredVoxelUploadProxy(ref DeferredVoxelColliderUpload pending, BoxCollider proxyCollider)
    {
        BoxCollider previousProxy = pending.ProxyCollider;
        if (previousProxy != null && !ReferenceEquals(previousProxy, proxyCollider))
            previousProxy.enabled = false;

        pending.ProxyCollider = proxyCollider;
        pending.RetryCount = 0;
        EnableVoxelProxyCollider(proxyCollider);

        RefreshDeferredVoxelUploadProxyBounds(ref pending, HectonFloatingOrigin.CurrentShiftSequence);
    }

    private static void CancelDeferredVoxelColliderUpload(ref DeferredVoxelColliderUpload pending, bool publishRetryDropWarning)
    {
        if ((pending.Flags & DeferredVoxelColliderUploadVolumeFlag) != 0 && pending.Volume != null)
            pending.Volume.EnableColliderChunkProxy(pending.ChunkIndex);

        EnableVoxelProxyCollider(pending.ProxyCollider);

        if (!publishRetryDropWarning || _voxelColliderUploadRetryDropWarningArmed)
            return;

        _voxelColliderUploadRetryDropWarningArmed = true;
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelColliderUploadRetryDropWarningHash,
            _VoxelPhysicsBakeContextHash,
            pending.RetryCount);
    }

    private static bool TryReserveDeferredVoxelColliderUploadSlot(BoxCollider proxyCollider)
    {
        if (_deferredVoxelColliderUploads.Count < DeferredVoxelColliderUploadCapacity)
        {
            if (_deferredVoxelColliderUploads.Count <= DeferredVoxelColliderUploadDropWarningReleaseThreshold)
                _voxelColliderUploadDropWarningArmed = false;
            return true;
        }

        DrainDeferredVoxelColliderUploads(DeferredVoxelColliderUploadBackpressureBudget);
        if (_deferredVoxelColliderUploads.Count < DeferredVoxelColliderUploadCapacity)
        {
            if (_deferredVoxelColliderUploads.Count <= DeferredVoxelColliderUploadDropWarningReleaseThreshold)
                _voxelColliderUploadDropWarningArmed = false;
            return true;
        }

        if (!_voxelColliderUploadDropWarningArmed)
        {
            _voxelColliderUploadDropWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelColliderUploadDropWarningHash,
                _VoxelPhysicsBakeContextHash,
                _deferredVoxelColliderUploads.Count);
        }

        EnableVoxelProxyCollider(proxyCollider);
        return false;
    }

    private static bool EnsureDeferredVoxelColliderUploadRegistered()
    {
        if (_deferredVoxelColliderUploadRegistered)
            return true;

        if (!CanRegisterDeferredVoxelLateFrameWork())
            return false;

        _deferredVoxelColliderUploadRegistered = GlobalRegistry.TryRegisterLateFrameTickable(
            _deferredVoxelColliderUploadDriver,
            PriorityLayer.Environment);
        if (_deferredVoxelColliderUploadRegistered)
            TryRegisterDeferredVoxelHotSwapBridge();
        return _deferredVoxelColliderUploadRegistered;
    }

    private static bool CanRegisterDeferredVoxelLateFrameWork()
    {
        return Application.isPlaying && GlobalRegistry.Dispatcher != null;
    }

    private static void RebindDeferredVoxelLateFrameDrivers()
    {
        bool needsTeardownDriver = _deferredVoxelPhysicsBakeTeardownRegistered ||
                                  DeferredVoxelPhysicsBakePendingCount > 0;
        bool needsUploadDriver = _deferredVoxelColliderUploadRegistered ||
                                _deferredVoxelColliderUploads.Count > 0;

        if (_deferredVoxelPhysicsBakeTeardownRegistered)
        {
            GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelPhysicsBakeTeardownDriver, PriorityLayer.Environment);
            _deferredVoxelPhysicsBakeTeardownRegistered = false;
        }

        if (_deferredVoxelColliderUploadRegistered)
        {
            GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelColliderUploadDriver, PriorityLayer.Environment);
            _deferredVoxelColliderUploadRegistered = false;
        }

        if (needsTeardownDriver)
            EnsureDeferredVoxelPhysicsBakeTeardownRegistered();
        if (needsUploadDriver)
            EnsureDeferredVoxelColliderUploadRegistered();
        TryUnregisterDeferredVoxelHotSwapBridgeIfIdle();
    }

    private static void TryRegisterDeferredVoxelHotSwapBridge()
    {
        if (_deferredVoxelHotSwapRegistered || !Application.isPlaying)
            return;

        _deferredVoxelHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(_deferredVoxelDispatcherHotSwapBridge);
    }

    private static void TryUnregisterDeferredVoxelHotSwapBridgeIfIdle()
    {
        if (!_deferredVoxelHotSwapRegistered ||
            _deferredVoxelPhysicsBakeTeardownRegistered ||
            _deferredVoxelColliderUploadRegistered ||
            HasPendingVoxelDeferredWork())
        {
            return;
        }

        GlobalRegistry.TryUnregisterHotSwapListener(_deferredVoxelDispatcherHotSwapBridge);
        _deferredVoxelHotSwapRegistered = false;
    }

    private static void DrainDeferredVoxelColliderUploads()
    {
        DrainDeferredVoxelColliderUploads(ConsumeDeferredVoxelColliderUploadBudgetThisFrame());
    }

    private static int ConsumeDeferredVoxelColliderUploadBudgetThisFrame()
    {
        int frame = SystemDispatcher.CurrentFrameIndex;
        if (_deferredVoxelColliderUploadFrame != frame)
        {
            _deferredVoxelColliderUploadFrame = frame;
            float frameBudget = ResolveDeferredVoxelColliderUploadBudgetPerFrame();
            float frameCap = Mathf.Clamp(
                Mathf.Ceil(frameBudget - DeferredVoxelColliderUploadBurstCapBias),
                DeferredVoxelColliderUploadBudgetPerFrame,
                DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame);
            AccumulateDeferredVoxelBudgetTokens(
                ref _deferredVoxelColliderUploadBudgetTokens,
                frameCap,
                frameBudget);
        }

        return ConsumeDeferredVoxelBudgetTokens(
            ref _deferredVoxelColliderUploadBudgetTokens,
            (int)DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame);
    }

    private static void AccumulateDeferredVoxelBudgetTokens(ref float tokenBucket, float frameCap, float frameBudget)
    {
        if (!math.isfinite(tokenBucket) || tokenBucket < 0f)
            tokenBucket = 0f;

        float safeFrameCap = math.isfinite(frameCap) && frameCap > 0f ? frameCap : 0f;
        float safeFrameBudget = math.isfinite(frameBudget) && frameBudget > 0f ? frameBudget : 0f;
        tokenBucket = math.min(safeFrameCap, tokenBucket + safeFrameBudget);
    }

    private static int ConsumeDeferredVoxelBudgetTokens(ref float tokenBucket, int maxBudget)
    {
        if (maxBudget <= 0 || !math.isfinite(tokenBucket) || tokenBucket <= 0f)
        {
            tokenBucket = 0f;
            return 0;
        }

        int budget = math.min(maxBudget, (int)math.floor(tokenBucket));
        if (budget <= 0)
        {
            tokenBucket = math.max(0f, tokenBucket);
            return 0;
        }

        tokenBucket = math.max(0f, tokenBucket - budget);
        return budget;
    }

    private static float ResolveDeferredVoxelColliderUploadBudgetPerFrame()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        return Mathf.Lerp(DeferredVoxelColliderUploadBudgetPerFrame, DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame, smooth);
    }

    private static void DrainDeferredVoxelColliderUploads(int uploadBudget)
    {
        int pendingCount = _deferredVoxelColliderUploads.Count;
        if (pendingCount <= 0)
        {
            _deferredVoxelColliderUploadScanCursor = -1;
            _voxelColliderUploadDropWarningArmed = false;
            _voxelColliderUploadRetryDropWarningArmed = false;
            UnregisterDeferredVoxelColliderUploadDriver();
            TryShutdownSharedTables();
            return;
        }

        int uploads = 0;
        int maxUploads = math.max(0, uploadBudget);
        int inspected = 0;
        int maxInspections = maxUploads > 0 ? math.max(maxUploads, maxUploads * 4) : 0;
        int index = _deferredVoxelColliderUploadScanCursor;
        if (index < 0 || index >= pendingCount)
            index = pendingCount - 1;

        while (index >= 0 && pendingCount > 0 && uploads < maxUploads && inspected < maxInspections)
        {
            if (index >= pendingCount)
                index = pendingCount - 1;

            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[index];
            inspected++;
            bool appliedUpload = false;
            bool keepPending = false;
            bool retryDrop = false;
            if ((pending.Flags & DeferredVoxelColliderUploadVolumeFlag) != 0)
            {
                if (pending.Volume != null)
                {
                    if (pending.Volume.IsDeferredColliderChunkUploadReady(pending.ChunkIndex))
                    {
                        appliedUpload = pending.Volume.CommitDeferredColliderChunkUpload(pending.ChunkIndex);
                    }
                    else if (pending.RetryCount < DeferredVoxelColliderUploadRetryLimit)
                    {
                        pending.RetryCount++;
                        RefreshDeferredVoxelUploadProxyBounds(ref pending, HectonFloatingOrigin.CurrentShiftSequence);
                        _deferredVoxelColliderUploads[index] = pending;
                        keepPending = true;
                    }
                    else
                    {
                        retryDrop = true;
                    }
                }
            }
            else if (pending.Collider != null && pending.Mesh != null)
            {
                appliedUpload = CommitDeferredRootVoxelColliderUpload(
                    pending.Collider,
                    pending.Mesh,
                    pending.ProxyCollider);
            }

            if (keepPending)
            {
                index--;
                continue;
            }

            if (!appliedUpload)
                CancelDeferredVoxelColliderUpload(ref pending, retryDrop);

            RemoveDeferredVoxelColliderUploadAt(index);
            if (appliedUpload)
                uploads++;
            pendingCount = _deferredVoxelColliderUploads.Count;
            if (pendingCount == 0)
                break;
            index--;
        }

        if (_deferredVoxelColliderUploads.Count == 0)
        {
            _deferredVoxelColliderUploadScanCursor = -1;
            _voxelColliderUploadDropWarningArmed = false;
            _voxelColliderUploadRetryDropWarningArmed = false;
            UnregisterDeferredVoxelColliderUploadDriver();
            TryShutdownSharedTables();
            return;
        }

        if (index < 0)
            index = _deferredVoxelColliderUploads.Count - 1;
        else if (index >= _deferredVoxelColliderUploads.Count)
            index = _deferredVoxelColliderUploads.Count - 1;

        _deferredVoxelColliderUploadScanCursor = index;
        if (_deferredVoxelColliderUploads.Count <= DeferredVoxelColliderUploadDropWarningReleaseThreshold)
            _voxelColliderUploadDropWarningArmed = false;
    }

    private static void RecordVoxelChunkMeshed()
    {
        int frame = SystemDispatcher.CurrentFrameIndex;
        if (_voxelMeshTelemetryFrame != frame)
        {
            _voxelMeshTelemetryFrame = frame;
            _voxelChunksMeshedThisFrame = 0;
        }

        if (_voxelChunksMeshedThisFrame < ushort.MaxValue)
            _voxelChunksMeshedThisFrame++;

        PublishVoxelMeshPipelineTelemetry();
    }

    private static void PublishVoxelMeshPipelineTelemetry()
    {
        int frame = SystemDispatcher.CurrentFrameIndex;
        if (_voxelMeshTelemetryFrame != frame)
        {
            _voxelMeshTelemetryFrame = frame;
            _voxelChunksMeshedThisFrame = 0;
        }

        int bakeQueueLength = DeferredVoxelPhysicsBakePendingCount;
        int uploadQueueLength = _deferredVoxelColliderUploads.Count;
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelChunksMeshedPerFrameHash,
            _VoxelMeshPipelineContextHash,
            _voxelChunksMeshedThisFrame);
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelBakeQueueLengthHash,
            _VoxelMeshPipelineContextHash,
            bakeQueueLength);

        WriteVoxelMeshPipelineBlackBoxSample(
            unchecked((uint)frame),
            0u,
            _voxelChunksMeshedThisFrame,
            bakeQueueLength,
            uploadQueueLength);
    }

    private static void ReportVoxelMeshScratchCapacityOverflow()
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            Hecton8.Core.SystemDispatcher.CurrentFrameId,
            VoxelMeshPipelineScratchCapacityOverflowFlag,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static void ReportVoxelInvalidMeshUpload()
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            Hecton8.Core.SystemDispatcher.CurrentFrameId,
            VoxelMeshPipelineInvalidMeshDataFlag,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static void ReportVoxelInvalidDensityField(uint densityFaultMask = 0u)
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            Hecton8.Core.SystemDispatcher.CurrentFrameId,
            VoxelMeshPipelineInvalidMeshDataFlag | densityFaultMask,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static uint ResolveVoxelDensityFaultMask(NativeArray<int> densityFaultFlags)
    {
        if (!densityFaultFlags.IsCreated)
            return 0u;

        uint mask = 0u;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.DensityEvaluation))
            mask |= VoxelMeshPipelineDensityEvaluationFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.QuantizeInput))
            mask |= VoxelMeshPipelineQuantizeInputFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.MarchingCubesCountInput))
            mask |= VoxelMeshPipelineMarchingCubesCountInputFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.MarchingCubesExtractInput))
            mask |= VoxelMeshPipelineMarchingCubesExtractInputFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.MarchingCubesExtractOutput))
            mask |= VoxelMeshPipelineMarchingCubesExtractOutputFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.WeldInput))
            mask |= VoxelMeshPipelineWeldInputFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.WeldOutput))
            mask |= VoxelMeshPipelineWeldOutputFaultFlag;
        if (IsVoxelDensityFaultSlotSet(densityFaultFlags, VoxelDensityPipelineFaultSlots.NormalFallback))
            mask |= VoxelMeshPipelineNormalFallbackFaultFlag;

        return mask;
    }

    private static bool IsVoxelDensityFaultSlotSet(NativeArray<int> densityFaultFlags, int slot)
    {
        return (uint)slot < (uint)densityFaultFlags.Length && densityFaultFlags[slot] != 0;
    }

    private static void ClearVoxelDensityFaultFlags(NativeArray<int> densityFaultFlags)
    {
        if (!densityFaultFlags.IsCreated)
            return;

        int count = math.min(densityFaultFlags.Length, VoxelDensityPipelineFaultSlots.SlotCount);
        for (int i = 0; i < count; i++)
            densityFaultFlags[i] = 0;
    }

    private static void ReportAndClearVoxelDensityFaults(NativeArray<int> densityFaultFlags)
    {
        uint faultMask = ResolveVoxelDensityFaultMask(densityFaultFlags);
        if (faultMask != 0u)
            ReportVoxelInvalidDensityField(faultMask);

        ClearVoxelDensityFaultFlags(densityFaultFlags);
    }

    private static void ReportVoxelVolumeSpawnPoolMiss()
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            Hecton8.Core.SystemDispatcher.CurrentFrameId,
            VoxelMeshPipelineVolumeSpawnPoolMissFlag,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static void ReportVoxelNullVolumeColliderFallback()
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            Hecton8.Core.SystemDispatcher.CurrentFrameId,
            VoxelMeshPipelineNullVolumeColliderFallbackFlag,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static bool EnsureVoxelMeshPipelineBlackBox()
    {
        IDataVault vault = CacheVoxelMeshPipelineBlackBoxVaultCold();
        if (vault == null)
            return false;

        if (IsVoxelMeshPipelineBlackBoxHandleCreated() &&
            vault.TryReadOnlyHandle(in _voxelMeshPipelineBlackBoxHandle, out NativeArray<VoxelMeshPipelineTelemetryEntry>.ReadOnly blackBox) &&
            blackBox.Length >= VoxelMeshPipelineBlackBoxCapacity)
        {
            return true;
        }

        _voxelMeshPipelineBlackBoxHandle = vault.EnsureGenerationHandle<VoxelMeshPipelineTelemetryEntry>(
            VoxelMeshPipelineBlackBoxBufferId,
            VoxelMeshPipelineBlackBoxCapacity,
            VoxelMeshPipelineBlackBoxOwnerSystemId,
            NativeArrayOptions.ClearMemory);
        _voxelMeshPipelineBlackBoxCursor = 0;
        return IsVoxelMeshPipelineBlackBoxHandleCreated();
    }

    private static void DisposeVoxelMeshPipelineBlackBox()
    {
        IDataVault vault = _voxelMeshPipelineBlackBoxVault;
        if (vault != null && IsVoxelMeshPipelineBlackBoxHandleCreated())
            vault.ReleaseBuffer(in _voxelMeshPipelineBlackBoxHandle);

        _voxelMeshPipelineBlackBoxHandle = default;
        _voxelMeshPipelineBlackBoxVault = null;
        _voxelMeshPipelineBlackBoxCursor = 0;
    }

    private static IDataVault CacheVoxelMeshPipelineBlackBoxVaultCold()
    {
        IDataVault vault = GlobalRegistry.DataVault;
        if (ReferenceEquals(_voxelMeshPipelineBlackBoxVault, vault))
            return vault;

        if (_voxelMeshPipelineBlackBoxVault != null && IsVoxelMeshPipelineBlackBoxHandleCreated())
            _voxelMeshPipelineBlackBoxVault.ReleaseBuffer(in _voxelMeshPipelineBlackBoxHandle);

        _voxelMeshPipelineBlackBoxVault = vault;
        _voxelMeshPipelineBlackBoxHandle = default;
        _voxelMeshPipelineBlackBoxCursor = 0;
        return vault;
    }

    private static bool IsVoxelMeshPipelineBlackBoxHandleCreated()
    {
        return _voxelMeshPipelineBlackBoxHandle.BufferID == (uint)VoxelMeshPipelineBlackBoxBufferId &&
               _voxelMeshPipelineBlackBoxHandle.SystemID == (uint)VoxelMeshPipelineBlackBoxOwnerSystemId &&
               _voxelMeshPipelineBlackBoxHandle.Generation != 0u;
    }

    private static void WriteVoxelMeshPipelineBlackBoxSample(
        uint frame,
        uint flags,
        int chunksMeshedThisFrame,
        int bakeQueueLength,
        int colliderUploadQueueLength)
    {
        IDataVault vault = _voxelMeshPipelineBlackBoxVault;
        if (vault == null ||
            vault.IsCompactionFenceActive ||
            !IsVoxelMeshPipelineBlackBoxHandleCreated() ||
            !vault.TryReadOnlyHandle(in _voxelMeshPipelineBlackBoxHandle, out NativeArray<VoxelMeshPipelineTelemetryEntry>.ReadOnly existingBlackBox) ||
            existingBlackBox.Length < VoxelMeshPipelineBlackBoxCapacity ||
            vault.IsCompactionFenceActive ||
            !vault.TryAcquireWriteLock(in _voxelMeshPipelineBlackBoxHandle, VoxelMeshPipelineBlackBoxOwnerSystemId, out NativeArray<VoxelMeshPipelineTelemetryEntry> blackBox))
        {
            return;
        }

        uint resolvedFlags = flags;
        int cursor = 0;
        try
        {
            if (vault.IsCompactionFenceActive || blackBox.Length < VoxelMeshPipelineBlackBoxCapacity)
                return;

            int surfacePoolInUse = _voxelSurfaceMeshPoolInUseCount;
            int physicsPoolInUse = _voxelPhysicsBakeMeshPoolInUseCount;
            uint stateHash = 2166136261u;
            stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, chunksMeshedThisFrame));
            stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, bakeQueueLength));
            stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, colliderUploadQueueLength));
            stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, Volatile.Read(ref _activeGenerationOperations)));
            stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, surfacePoolInUse));
            stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, physicsPoolInUse));

            bool invalidState =
                chunksMeshedThisFrame < 0 ||
                bakeQueueLength < 0 ||
                colliderUploadQueueLength < 0 ||
                _voxelMeshPipelineBlackBoxCursor < 0 ||
                _voxelMeshPipelineBlackBoxCursor >= VoxelMeshPipelineBlackBoxCapacity;
            resolvedFlags = invalidState ? flags | VoxelMeshPipelineInvalidStateFlag : flags;
            cursor = math.clamp(_voxelMeshPipelineBlackBoxCursor, 0, VoxelMeshPipelineBlackBoxCapacity - 1);
            blackBox[cursor] = new VoxelMeshPipelineTelemetryEntry
            {
                TimestampTicks = Stopwatch.GetTimestamp(),
                Frame = frame,
                Flags = resolvedFlags,
                BufferId = _voxelMeshPipelineBlackBoxHandle.BufferID,
                SystemId = _voxelMeshPipelineBlackBoxHandle.SystemID,
                Generation = _voxelMeshPipelineBlackBoxHandle.Generation,
                StateHash = stateHash,
                VaultGenerationId = vault.VaultGenerationID,
                ChunksMeshedThisFrame = (ushort)math.min(ushort.MaxValue, math.max(0, chunksMeshedThisFrame)),
                BakeQueueLength = (ushort)math.min(ushort.MaxValue, math.max(0, bakeQueueLength)),
                ColliderUploadQueueLength = (ushort)math.min(ushort.MaxValue, math.max(0, colliderUploadQueueLength)),
                ActiveGenerationOperations = (ushort)math.min(ushort.MaxValue, math.max(0, Volatile.Read(ref _activeGenerationOperations))),
                SurfacePoolInUse = (ushort)math.min(ushort.MaxValue, math.max(0, surfacePoolInUse)),
                PhysicsPoolInUse = (ushort)math.min(ushort.MaxValue, math.max(0, physicsPoolInUse)),
                Padding0 = 0u,
                Padding1 = 0u,
                Padding2 = 0u,
                Padding3 = 0u
            };
        }
        finally
        {
            vault.ReleaseWriteLock(in _voxelMeshPipelineBlackBoxHandle, VoxelMeshPipelineBlackBoxOwnerSystemId);
        }

        cursor++;
        _voxelMeshPipelineBlackBoxCursor = cursor >= VoxelMeshPipelineBlackBoxCapacity ? 0 : cursor;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // R95 FIX: removed a leftover agent capture kill-switch that force-stopped Play mode /
        // quit development builds at frame 310 and dumped the black box unconditionally at frame 300.
        // The black box now dumps only on real fault flags, per the Black Box law.
        if (resolvedFlags != 0u)
            DumpVoxelMeshPipelineBlackBoxOnce(resolvedFlags);
#endif
    }

    private static uint MixVoxelMeshTelemetryHash(uint hash, uint value)
    {
        unchecked
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    internal static void RecordVoxelRegistryCorruptionForAgent1304(int primaryCount, int secondaryCount, int limit)
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            (uint)Time.frameCount,
            VoxelMeshPipelineRegistryCorruptionFlag,
            primaryCount,
            secondaryCount,
            limit);
    }

    internal static void RecordVoxelRebuildFailClosedForAgent1304(int runtimeStamp, int bakeState, int queued)
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            (uint)Time.frameCount,
            VoxelMeshPipelineRebuildFailClosedFlag,
            runtimeStamp,
            bakeState,
            queued);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static void DumpVoxelMeshPipelineBlackBoxOnce(uint reasonFlags)
    {
        if (_voxelMeshPipelineBlackBoxDumped)
            return;

        _voxelMeshPipelineBlackBoxDumped = true;
        DumpVoxelMeshPipelineBlackBox(reasonFlags);
    }

    private static void DumpVoxelMeshPipelineBlackBox(uint reasonFlags)
    {
        IDataVault vault = _voxelMeshPipelineBlackBoxVault;
        if (vault == null ||
            !IsVoxelMeshPipelineBlackBoxHandleCreated() ||
            !vault.TryReadOnlyHandle(in _voxelMeshPipelineBlackBoxHandle, out NativeArray<VoxelMeshPipelineTelemetryEntry>.ReadOnly blackBox) ||
            blackBox.Length < VoxelMeshPipelineBlackBoxCapacity)
        {
            return;
        }

        try
        {
            WriteVoxelMeshPipelineBlackBoxFile(VoxelMeshPipelineBlackBoxPrimaryDumpRelativePath, blackBox, reasonFlags);
            WriteVoxelMeshPipelineBlackBoxFile(VoxelMeshPipelineBlackBoxAgentDumpRelativePath, blackBox, reasonFlags);
            WriteVoxelMeshPipelineBlackBoxFile(VoxelMeshPipelineBlackBoxCompactionDumpRelativePath, blackBox, reasonFlags);
        }
        catch
        {
            // Fault-path export must not add a second runtime failure.
        }
    }

    private static void WriteVoxelMeshPipelineBlackBoxFile(
        string relativePath,
        NativeArray<VoxelMeshPipelineTelemetryEntry>.ReadOnly blackBox,
        uint reasonFlags)
    {
        const int HeaderBytes = 20;
        const int RowBytes = 32;
        int totalBytes = HeaderBytes + VoxelMeshPipelineBlackBoxCapacity * RowBytes;
        NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
            totalBytes,
            nameof(HectonVoxelEngine),
            "VoxelMeshPipelineBlackBoxDumpPayload");
        try
        {
            WriteUInt32LittleEndian(payload, 0, VoxelMeshPipelineBlackBoxDumpMagic);
            WriteUInt32LittleEndian(payload, 4, (uint)VoxelMeshPipelineBlackBoxCapacity);
            WriteUInt32LittleEndian(payload, 8, (uint)UnsafeUtility.SizeOf<VoxelMeshPipelineTelemetryEntry>());
            WriteUInt32LittleEndian(payload, 12, (uint)_voxelMeshPipelineBlackBoxCursor);
            WriteUInt32LittleEndian(payload, 16, reasonFlags);

            for (int i = 0; i < VoxelMeshPipelineBlackBoxCapacity; i++)
            {
                int index = (_voxelMeshPipelineBlackBoxCursor + i) % VoxelMeshPipelineBlackBoxCapacity;
                VoxelMeshPipelineTelemetryEntry entry = blackBox[index];
                int offset = HeaderBytes + i * RowBytes;
                WriteUInt32LittleEndian(payload, offset, entry.Frame);
                WriteUInt32LittleEndian(payload, offset + 4, entry.Flags);
                WriteUInt16LittleEndian(payload, offset + 8, entry.ChunksMeshedThisFrame);
                WriteUInt16LittleEndian(payload, offset + 10, entry.BakeQueueLength);
                WriteUInt16LittleEndian(payload, offset + 12, entry.ColliderUploadQueueLength);
                WriteUInt16LittleEndian(payload, offset + 14, entry.ActiveGenerationOperations);
                WriteUInt16LittleEndian(payload, offset + 16, entry.SurfacePoolInUse);
                WriteUInt16LittleEndian(payload, offset + 18, entry.PhysicsPoolInUse);
                WriteUInt32LittleEndian(payload, offset + 20, entry.StateHash);
                WriteUInt32LittleEndian(payload, offset + 24, entry.Padding0);
                WriteUInt32LittleEndian(payload, offset + 28, entry.Padding1);
            }

            NativeFaultDumpWriter.TryWriteAll(relativePath, payload, totalBytes);
        }
        finally
        {
            NativeFaultDumpWriter.DisposeTransientPayload(
                ref payload,
                nameof(HectonVoxelEngine),
                "VoxelMeshPipelineBlackBoxDumpPayload");
        }
    }

    private static void WriteUInt16LittleEndian(NativeArray<byte> payload, int offset, ushort value)
    {
        payload[offset] = (byte)value;
        payload[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
    {
        payload[offset] = (byte)value;
        payload[offset + 1] = (byte)(value >> 8);
        payload[offset + 2] = (byte)(value >> 16);
        payload[offset + 3] = (byte)(value >> 24);
    }
#endif

    private static void RemoveDeferredVoxelColliderUploadAt(int index)
    {
        if ((uint)index >= (uint)_deferredVoxelColliderUploads.Count)
            return;

        int lastIndex = _deferredVoxelColliderUploads.Count - 1;
        if (index != lastIndex)
            _deferredVoxelColliderUploads[index] = _deferredVoxelColliderUploads[lastIndex];

        _deferredVoxelColliderUploads.RemoveAt(lastIndex);
    }

    private static bool CommitDeferredRootVoxelColliderUpload(
        MeshCollider collider,
        Mesh mesh,
        BoxCollider proxyCollider)
    {
        if (collider == null || mesh == null)
        {
            EnableVoxelProxyCollider(proxyCollider);
            return proxyCollider != null;
        }

        EnableVoxelProxyCollider(proxyCollider);

        collider.enabled = false;
        return proxyCollider != null;
    }

    private static void UnregisterDeferredVoxelColliderUploadDriver()
    {
        if (!_deferredVoxelColliderUploadRegistered)
            return;

        GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelColliderUploadDriver, PriorityLayer.Environment);
        _deferredVoxelColliderUploadRegistered = false;
        TryUnregisterDeferredVoxelHotSwapBridgeIfIdle();
    }

    private static void UpdateDeferredVoxelPhysicsBakeBackpressure()
    {
        int pendingCount = DeferredVoxelPhysicsBakePendingCount;
        bool nextActive = ResolveDeferredVoxelPhysicsBakeBackpressureState(
            pendingCount,
            _deferredVoxelPhysicsBakeBackpressureActive);

        if (GlobalRegistry.Dispatcher == null)
        {
            _deferredVoxelPhysicsBakeBackpressureActive = nextActive;
            return;
        }

        if (nextActive == _deferredVoxelPhysicsBakeBackpressureActive)
        {
            if (nextActive)
                SystemDispatcher.SetVoxelTeardownBackpressure(true, pendingCount);
            return;
        }

        _deferredVoxelPhysicsBakeBackpressureActive = nextActive;
        SystemDispatcher.SetVoxelTeardownBackpressure(nextActive, pendingCount);
        if (nextActive)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelTeardownBackpressureWarningHash,
                _VoxelPhysicsBakeContextHash,
                pendingCount);
        }
    }

    internal static bool DebugResolveDeferredVoxelPhysicsBakeBackpressureState(int pendingCount, bool currentlyActive)
    {
        return ResolveDeferredVoxelPhysicsBakeBackpressureState(pendingCount, currentlyActive);
    }

    internal static int DebugResolveDistanceBasedVoxelLodLevel(Vector3 worldCenter, Vector3 observerPosition)
    {
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup) ||
            !TryResolveRuntimeAup(worldCenter, in originAup, out AbsoluteUniversePosition worldCenterAup) ||
            !TryResolveRuntimeAup(observerPosition, in originAup, out AbsoluteUniversePosition observerAup))
        {
            return 0;
        }

        return ResolveDistanceBasedVoxelLodLevel(in worldCenterAup, in observerAup, VoxelLodColliderDisableDistanceMeters);
    }

    internal static bool DebugResolveVoxelPhysicsBakePoolExhausted(int inUseCount)
    {
        return VoxelRuntimeIntegrityUtility.ResolveFixedPoolExhausted(
            inUseCount,
            VoxelPhysicsBakeMeshPoolSize);
    }

    internal static int DebugVoxelPhysicsBakeMeshPoolSize => VoxelPhysicsBakeMeshPoolSize;

    private static int ResolveDistanceBasedVoxelLodLevel(Vector3 worldCenter)
    {
        if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            return 0;

        if (!TryResolveRuntimeAup(worldCenter, out AbsoluteUniversePosition worldCenterAup))
            return 0;

        return ResolveDistanceBasedVoxelLodLevel(in worldCenterAup, in playerAup, VoxelLodColliderDisableDistanceMeters);
    }

    private static readonly float[] _sharedLodThresholds = new float[1];

    private static int ResolveDistanceBasedVoxelLodLevel(
        in AbsoluteUniversePosition worldCenterAup,
        in AbsoluteUniversePosition observerAup,
        float lodDistanceMeters)
    {
        double distanceSq = AbsoluteUniversePosition.DistanceSq(in worldCenterAup, in observerAup);
        float distance = (float)System.Math.Sqrt(distanceSq);
        _sharedLodThresholds[0] = lodDistanceMeters;
        return Hecton8.PureLogic.Systems.LodChunkSelector.Calculate(distance, _sharedLodThresholds, 1);
    }

    private bool ShouldUseCinematicColliderFake(in VoxelPipelineData data)
    {
        if (!data.BuildCollider || data.LODLevel > 0)
            return false;

        if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            return false;

        AbsoluteUniversePosition volumeAup = BuildCapturedAup(data.WorldCenter, data.AbsoluteUniverseOffsetAtStartDouble);
        double distanceSq = AbsoluteUniversePosition.DistanceSq(in volumeAup, in playerAup);
        float colliderDisableDistance = VoxelLodColliderDisableDistanceMeters;
        IVramPressureReadModel pressureMonitor = _vramPressureReadModel;
        if (pressureMonitor != null &&
            pressureMonitor.HasSample &&
            pressureMonitor.PressureFactor >= VoxelColliderFakePressureFactor)
        {
            colliderDisableDistance = VoxelPressureColliderDisableDistanceMeters;
        }

        double thresholdSq = (double)colliderDisableDistance * colliderDisableDistance;
        return distanceSq > thresholdSq;
    }

    private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
    {
        playerAup = AbsoluteUniversePosition.Invalid();
        IPlayerRuntimeContext playerRuntimeContext = s_playerRuntimeContext;
        if (playerRuntimeContext != null)
        {
            if (playerRuntimeContext.IsInitialized &&
                playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                AbsoluteUniversePosition.IsFinite(in movementState.PredictedAup))
            {
                playerAup = movementState.PredictedAup;
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsFiniteAup(in AbsoluteUniversePosition position)
    {
        return math.isfinite(position.LocalX) &&
               math.isfinite(position.LocalY) &&
               math.isfinite(position.LocalZ);
    }

    private static bool TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute)
    {
        originAbsolute = default;
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            return false;

        originAbsolute = originAup.ToAbsoluteDouble3();
        return math.all(math.isfinite(originAbsolute));
    }

    private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
    {
        originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
        return IsFiniteAup(in originAup);
    }

    private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
    {
        positionAup = default;
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            return false;

        return TryResolveRuntimeAup(runtimePosition, in originAup, out positionAup);
    }

    private static bool TryResolveRuntimeAup(
        Vector3 runtimePosition,
        in AbsoluteUniversePosition originAup,
        out AbsoluteUniversePosition positionAup)
    {
        positionAup = default;
        if (!IsFiniteVector(runtimePosition) || !IsFiniteAup(in originAup))
            return false;

        positionAup = AbsoluteUniversePosition.OffsetMeters(
            in originAup,
            new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        return IsFiniteAup(in positionAup);
    }

    private static bool TryResolveRuntimeAbsoluteDouble(Vector3 runtimePosition, out double3 absolutePosition)
    {
        absolutePosition = default;
        if (!TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition positionAup))
            return false;

        absolutePosition = positionAup.ToAbsoluteDouble3();
        return math.all(math.isfinite(absolutePosition));
    }

    private static bool TryResolveRuntimeAbsoluteDouble(
        Vector3 runtimePosition,
        in AbsoluteUniversePosition originAup,
        out double3 absolutePosition)
    {
        absolutePosition = default;
        if (!TryResolveRuntimeAup(runtimePosition, in originAup, out AbsoluteUniversePosition positionAup))
            return false;

        absolutePosition = positionAup.ToAbsoluteDouble3();
        return math.all(math.isfinite(absolutePosition));
    }

    private static AbsoluteUniversePosition BuildCapturedAup(Vector3 runtimePosition, Vector3 capturedOffset)
    {
        return BuildCapturedAup(runtimePosition, global::Hecton8.World.AUPMath.ToDouble3(capturedOffset));
    }

    private static AbsoluteUniversePosition BuildCapturedAup(Vector3 runtimePosition, double3 capturedOffset)
    {
        return AbsoluteUniversePosition.FromAbsolutePosition(global::Hecton8.World.AUPMath.ToDouble3(runtimePosition) + capturedOffset);
    }

    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    private static bool TryResolveLocalDeltaFloat3(double3 deltaAup, out float3 localDelta)
    {
        const double RuntimeAupLocalClampMeters = 1048576d;
        localDelta = default;
        if (!math.all(math.isfinite(deltaAup)))
            return false;

        deltaAup = math.clamp(
            deltaAup,
            new double3(-RuntimeAupLocalClampMeters),
            new double3(RuntimeAupLocalClampMeters));
        localDelta = (float3)deltaAup;
        return IsFiniteFloat3(localDelta);
    }

    private static bool TryResolveRuntimeFloat3FromAup(double3 targetAup, double3 originAup, out float3 runtimePosition)
    {
        runtimePosition = default;
        if (!math.all(math.isfinite(targetAup)) || !math.all(math.isfinite(originAup)))
            return false;

        double3 deltaAup = targetAup - originAup;
        return TryResolveLocalDeltaFloat3(deltaAup, out runtimePosition);
    }

    private static bool TryResolveSafeEntranceTerrainHole(
        in CaveEntrance entrance,
        float holePadding,
        double3 capturedTotalOffset,
        double3 committedTotalOffset,
        out Vector3 runtimeSurfacePosition,
        out float radius)
    {
        runtimeSurfacePosition = default;
        radius = default;
        if (!IsFiniteFloat3(entrance.surfacePosition) ||
            !IsFiniteFloat3(entrance.inwardDirection) ||
            !math.isfinite(entrance.radius) ||
            entrance.radius <= 0f)
        {
            return false;
        }

        float directionSq = math.lengthsq(entrance.inwardDirection);
        if (!math.isfinite(directionSq) || directionSq <= 0.0001f)
            return false;

        float safePadding = math.isfinite(holePadding) && holePadding > 0f
            ? math.min(holePadding, MaxRuntimeCaveEntranceHolePadding)
            : 1f;
        float safeInnerRadius = math.isfinite(entrance.innerRadius) && entrance.innerRadius > 0f
            ? entrance.innerRadius
            : 0f;
        radius = math.clamp(
            math.max(entrance.radius, safeInnerRadius) + safePadding,
            MinRuntimeCaveEntranceHoleRadius,
            MaxRuntimeCaveEntranceHoleRadius);
        if (!math.isfinite(radius))
            return false;

        double3 runtimeSurfaceDelta = global::Hecton8.World.AUPMath.ToDouble3(entrance.surfacePosition) + capturedTotalOffset - committedTotalOffset;
        if (!TryResolveLocalDeltaFloat3(runtimeSurfaceDelta, out float3 runtimeSurfaceFloat))
            return false;

        runtimeSurfacePosition = ToVector3(runtimeSurfaceFloat);
        return IsFiniteVector(runtimeSurfacePosition);
    }

    private static bool TryRebaseCapturedRuntimeFloat3(
        float3 capturedRuntimePosition,
        double3 capturedOriginAup,
        double3 committedOriginAup,
        out float3 runtimePosition)
    {
        runtimePosition = default;
        if (!IsFiniteFloat3(capturedRuntimePosition) ||
            !math.all(math.isfinite(capturedOriginAup)) ||
            !math.all(math.isfinite(committedOriginAup)))
        {
            return false;
        }

        double3 targetAup = global::Hecton8.World.AUPMath.ToDouble3(capturedRuntimePosition) + capturedOriginAup;
        return TryResolveRuntimeFloat3FromAup(targetAup, committedOriginAup, out runtimePosition);
    }

    private static float3 ResolveWrappedAupNoiseOffset(double3 absoluteAup)
    {
        const double NoiseWrapPeriodMeters = 4096.0d;
        double3 wrapped = absoluteAup - math.floor(absoluteAup / NoiseWrapPeriodMeters) * NoiseWrapPeriodMeters;
        return (float3)wrapped;
    }

    private static bool TryResolveRuntimeVector3FromAup(double3 targetAup, double3 originAup, out Vector3 runtimePosition)
    {
        runtimePosition = default;
        if (!TryResolveRuntimeFloat3FromAup(targetAup, originAup, out float3 runtimeFloat))
            return false;

        runtimePosition = new Vector3(runtimeFloat.x, runtimeFloat.y, runtimeFloat.z);
        return true;
    }

    private static bool ResolveDeferredVoxelPhysicsBakeBackpressureState(int pendingCount, bool currentlyActive)
    {
        return VoxelRuntimeIntegrityUtility.ResolveBackpressureState(
            pendingCount,
            currentlyActive,
            DeferredVoxelPhysicsBakeBackpressureThreshold,
            DeferredVoxelPhysicsBakeBackpressureReleaseThreshold);
    }

    private static void DestroyDeferredVoxelObject(UnityEngine.Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    private static void ResetVoxelMeshPoolState()
    {
        EnsureVoxelMeshPoolOccupancyFlags();
        for (int i = 0; i < _voxelSurfaceMeshPoolInUse.Length; i++)
            _voxelSurfaceMeshPoolInUse[i] = 0;

        for (int i = 0; i < _voxelPhysicsBakeMeshPoolInUse.Length; i++)
            _voxelPhysicsBakeMeshPoolInUse[i] = 0;

        _voxelSurfaceMeshPoolInUseCount = 0;
        _voxelPhysicsBakeMeshPoolInUseCount = 0;
    }

    private static void EnsureVoxelMeshPoolOccupancyFlags()
    {
        if (Volatile.Read(ref _voxelMeshPoolOccupancyInitialized) == 1)
            return;

        SpinWait spinWait = default;
        while (Interlocked.CompareExchange(ref _voxelMeshPoolOccupancyInitGate, 1, 0) != 0)
        {
            if (Volatile.Read(ref _voxelMeshPoolOccupancyInitialized) == 1)
                return;

            spinWait.SpinOnce();
        }

        try
        {
            if (Volatile.Read(ref _voxelMeshPoolOccupancyInitialized) == 1)
                return;

            while (_voxelSurfaceMeshPoolInUse.Length < VoxelSurfaceMeshPoolSize)
                _voxelSurfaceMeshPoolInUse.Add(0);

            while (_voxelPhysicsBakeMeshPoolInUse.Length < VoxelPhysicsBakeMeshPoolSize)
                _voxelPhysicsBakeMeshPoolInUse.Add(0);

            Volatile.Write(ref _voxelMeshPoolOccupancyInitialized, 1);
        }
        finally
        {
            Volatile.Write(ref _voxelMeshPoolOccupancyInitGate, 0);
        }
    }

    private static async Awaitable WarmVoxelMeshPoolsAsync(CancellationToken ct)
    {
        if (_voxelMeshPoolWarmupRunning)
            return;

        _voxelMeshPoolWarmupRunning = true;
        try
        {
            if (ct.IsCancellationRequested || ShouldAbortVoxelMeshPoolWarmup())
                return;

            await WarmVoxelSurfaceMeshPoolAsync(ct);
            if (ct.IsCancellationRequested || ShouldAbortVoxelMeshPoolWarmup())
                return;

            await WarmVoxelPhysicsBakeMeshPoolAsync(ct);
        }
        finally
        {
            _voxelMeshPoolWarmupRunning = false;
            if (Volatile.Read(ref _shutdownRequested) == 1)
                TryShutdownSharedTables();
        }
    }

    private static async Awaitable WarmVoxelSurfaceMeshPoolAsync(CancellationToken ct)
    {
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            if (_voxelSurfaceMeshPool[i] != null)
                continue;

            if (ct.IsCancellationRequested || ShouldAbortVoxelMeshPoolWarmup())
                return;

            _voxelSurfaceMeshPool[i] = CreateVoxelPoolMesh(VoxelSurfacePoolMeshName);
            await AwaitableDebtMonitor.NextFrameAsync();
        }
    }

    private static async Awaitable WarmVoxelPhysicsBakeMeshPoolAsync(CancellationToken ct)
    {
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (_voxelPhysicsBakeMeshPool[i] != null)
                continue;

            if (ct.IsCancellationRequested || ShouldAbortVoxelMeshPoolWarmup())
                return;

            _voxelPhysicsBakeMeshPool[i] = CreateVoxelPoolMesh(VoxelPhysicsBakePoolMeshName);
            await AwaitableDebtMonitor.NextFrameAsync();
        }
    }

    private static bool ShouldAbortVoxelMeshPoolWarmup()
    {
        return Volatile.Read(ref _shutdownRequested) == 1 &&
               Volatile.Read(ref _liveEngineCount) <= 0;
    }

    private static Mesh CreateVoxelPoolMesh(string meshName)
    {
        Mesh mesh = new Mesh // COLD ALLOC: Mesh[1] - staggered pooled voxel mesh slot creation outside the hot path.
        {
            name = meshName
        };
        mesh.MarkDynamic();
        return mesh;
    }

    bool NeedsVoxelSurfaceMeshAcquire(GameObject go, HectonVoxelVolume volume = null)
    {
        if (go == null)
            return true;

        MeshFilter meshFilter = ResolvePooledMeshFilter(go, volume);
        return meshFilter == null || meshFilter.sharedMesh == null;
    }

    private static async Awaitable<Mesh> AcquireVoxelSurfaceMeshAsync(CancellationToken ct)
    {
        Mesh mesh = AcquireVoxelSurfaceMesh();
        if (mesh != null)
            return mesh;

        for (int retry = 0; retry < VoxelMeshPoolAcquireWarmupRetryFrames && _voxelMeshPoolWarmupRunning; retry++)
        {
            if (ct.IsCancellationRequested)
                return null;

            await AwaitableDebtMonitor.NextFrameAsync();
            mesh = AcquireVoxelSurfaceMesh();
            if (mesh != null)
                return mesh;
        }

        return null;
    }

    private static async Awaitable<Mesh> AcquireVoxelPhysicsBakeMeshAsync(CancellationToken ct)
    {
        Mesh mesh = AcquireVoxelPhysicsBakeMesh();
        if (mesh != null)
            return mesh;

        for (int retry = 0; retry < VoxelMeshPoolAcquireWarmupRetryFrames && _voxelMeshPoolWarmupRunning; retry++)
        {
            if (ct.IsCancellationRequested)
                return null;

            await AwaitableDebtMonitor.NextFrameAsync();
            mesh = AcquireVoxelPhysicsBakeMesh();
            if (mesh != null)
                return mesh;
        }

        return null;
    }

    internal static Mesh AcquireVoxelSurfaceMesh()
    {
        EnsureVoxelMeshPoolOccupancyFlags();
        bool hasColdFreeSlot = false;
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            if (_voxelSurfaceMeshPoolInUse[i] != 0)
                continue;

            Mesh mesh = _voxelSurfaceMeshPool[i];
            if (mesh == null)
            {
                hasColdFreeSlot = true;
                continue;
            }

            _voxelSurfaceMeshPoolInUse[i] = 1;
            _voxelSurfaceMeshPoolInUseCount = math.min(VoxelSurfaceMeshPoolSize, _voxelSurfaceMeshPoolInUseCount + 1);
            mesh.Clear(false);
            _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
            return mesh;
        }

        if (!hasColdFreeSlot && !_voxelSurfaceMeshPoolExhaustedWarningArmed)
        {
            _voxelSurfaceMeshPoolExhaustedWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelSurfaceMeshPoolExhaustedWarningHash,
                _VoxelPhysicsBakeContextHash,
                VoxelSurfaceMeshPoolSize);
        }

        return null;
    }

    internal static bool ReleaseVoxelSurfaceMesh(Mesh mesh)
    {
        if (mesh == null)
            return false;

        EnsureVoxelMeshPoolOccupancyFlags();
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            if (!ReferenceEquals(_voxelSurfaceMeshPool[i], mesh))
                continue;

            mesh.Clear(false);
            if (_voxelSurfaceMeshPoolInUse[i] != 0)
            {
                _voxelSurfaceMeshPoolInUse[i] = 0;
                _voxelSurfaceMeshPoolInUseCount = math.max(0, _voxelSurfaceMeshPoolInUseCount - 1);
            }

            _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
            return true;
        }

        return false;
    }

    internal static Mesh AcquireVoxelPhysicsBakeMesh()
    {
        EnsureVoxelMeshPoolOccupancyFlags();
        bool hasColdFreeSlot = false;
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (_voxelPhysicsBakeMeshPoolInUse[i] != 0)
                continue;

            Mesh mesh = _voxelPhysicsBakeMeshPool[i];
            if (mesh == null)
            {
                hasColdFreeSlot = true;
                continue;
            }

            _voxelPhysicsBakeMeshPoolInUse[i] = 1;
            _voxelPhysicsBakeMeshPoolInUseCount = math.min(VoxelPhysicsBakeMeshPoolSize, _voxelPhysicsBakeMeshPoolInUseCount + 1);
            mesh.Clear(false);
            return mesh;
        }

        if (!hasColdFreeSlot && !_voxelPhysicsBakeMeshPoolExhaustedWarningArmed)
        {
            _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelPhysicsBakePoolExhaustedWarningHash,
                _VoxelPhysicsBakeContextHash,
                VoxelPhysicsBakeMeshPoolSize);
        }

        return null;
    }

    internal static bool ReleaseVoxelPhysicsBakeMesh(Mesh mesh)
    {
        if (mesh == null)
            return false;

        EnsureVoxelMeshPoolOccupancyFlags();
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (!ReferenceEquals(_voxelPhysicsBakeMeshPool[i], mesh))
                continue;

            mesh.Clear(false);
            if (_voxelPhysicsBakeMeshPoolInUse[i] != 0)
            {
                _voxelPhysicsBakeMeshPoolInUse[i] = 0;
                _voxelPhysicsBakeMeshPoolInUseCount = math.max(0, _voxelPhysicsBakeMeshPoolInUseCount - 1);
            }

            _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
            return true;
        }

        return false;
    }

    private static void DestroyVoxelMeshPools()
    {
        EnsureVoxelMeshPoolOccupancyFlags();
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            Mesh mesh = _voxelSurfaceMeshPool[i];
            if (mesh != null)
                DestroyDeferredVoxelObject(mesh);

            _voxelSurfaceMeshPool[i] = null;
            _voxelSurfaceMeshPoolInUse[i] = 0;
        }

        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            Mesh mesh = _voxelPhysicsBakeMeshPool[i];
            if (mesh != null)
                DestroyDeferredVoxelObject(mesh);

            _voxelPhysicsBakeMeshPool[i] = null;
            _voxelPhysicsBakeMeshPoolInUse[i] = 0;
        }

        _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
        _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
        _voxelSurfaceMeshPoolInUseCount = 0;
        _voxelPhysicsBakeMeshPoolInUseCount = 0;
    }

    private static void PublishVoxelAnomalySolveWarningIfNeeded(long startTimestamp)
    {
        float elapsedMs = (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d / Stopwatch.Frequency);
        if (elapsedMs <= VoxelAnomalySolveWarningMs)
        {
            _voxelAnomalySolveWarningArmed = false;
        }
        else if (!_voxelAnomalySolveWarningArmed)
        {
            _voxelAnomalySolveWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelAnomalySolveWarningHash,
                _VoxelAnomalyContextHash,
                elapsedMs);
        }
    }

    static void LogVoxelJobWaitWatchdog(string context, int waitFrames)
    {
#if UNITY_EDITOR
        Hecton8.Core.H8Debug.LogError("[HectonVoxel] Job wait watchdog tripped. Cleanup barrier required.");
#endif
    }

    static void TryBindSelectedChthonicPillarResources(
        NativeArray<AnomalyFeatureRecord> selectedPillarFeature,
        ResourceDistributionDirector resourceDistributionDirector)
    {
        if (!selectedPillarFeature.IsCreated || selectedPillarFeature.Length <= 0)
            return;

        AnomalyFeatureRecord record = selectedPillarFeature[0];
        if (record.Valid == 0 || record.Kind != (byte)AnomalyFeatureKind.ChthonicPillar)
            return;

        if (resourceDistributionDirector == null)
            return;

        resourceDistributionDirector.TryBindChthonicPillarResourcesAtAup(
            new double3(record.AupX, record.AupY, record.AupZ),
            ChthonicPillarRadiusMeters,
            ChthonicPillarHeightMeters,
            ComputeChthonicPillarStableId(in record));
    }

    static uint ComputeChthonicPillarStableId(in AnomalyFeatureRecord record)
    {
        uint hash = 2166136261u;
        hash = HashPillarCoordinate(hash, QuantizePillarCoordinate(record.AupX));
        hash = HashPillarCoordinate(hash, QuantizePillarCoordinate(record.AupY));
        hash = HashPillarCoordinate(hash, QuantizePillarCoordinate(record.AupZ));
        return hash == 0u ? 1u : hash;
    }

    static long QuantizePillarCoordinate(double value)
    {
        return value >= 0d
            ? (long)(value + 0.5d)
            : (long)(value - 0.5d);
    }

    static uint HashPillarCoordinate(uint hash, long value)
    {
        unchecked
        {
            ulong folded = (ulong)value;
            hash ^= (uint)folded;
            hash *= 16777619u;
            hash ^= (uint)(folded >> 32);
            hash *= 16777619u;
            return hash;
        }
    }

    // R99: ShouldApplyCameraFacingOverhangNoise was removed. It made pre-extraction SDF geometry depend
    // on camera facing, so the same volume rebuilt from a different viewing angle produced different rock
    // and a different collider. See the call site in the density pipeline for the full rationale.

    static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
    {
        float lengthSq = math.lengthsq(value);
        return lengthSq > 0.0001f ? value / math.max(LengthApprox(value), 0.0001f) : fallback;
    }

    static float LengthApprox(float3 value)
    {
        return math.length(value);
    }

    static int ResolveBiomeSdfModifierEnabled(int lodLevel)
    {
        if (lodLevel >= 2)
            return 0;

        return 1;
    }

    bool TryPrepareModifiedCellsForPipeline(VoxelPipelineData data)
    {
        if (data == null || data.SourceVolume == null || _deltaProcessor == null)
            return true;

        if (!_deltaProcessor.TryMeasureDeltaMapForVolume(data.SourceVolume, out int measuredModifiedCellCapacity) ||
            measuredModifiedCellCapacity <= 0)
        {
            data.ModifiedCellCount = 0;
            data.ModifiedCellBucketCount = 0;
            return true;
        }

        int modifiedCellCapacity = math.min(measuredModifiedCellCapacity, math.max(1, data.TotalCells));
        int modifiedCellBucketCount = ResolveModifiedCellBucketCount(modifiedCellCapacity);
        if (!TryPrepareModifiedCellsScratch(
                ref data.ScratchLease,
                modifiedCellCapacity,
                modifiedCellBucketCount,
                out NativeArray<VoxelModifiedCellEntry> modifiedCells,
                out NativeArray<int> modifiedCellCount,
                out NativeArray<int> modifiedCellBucketHeads,
                out NativeArray<int> modifiedCellNext))
        {
            return false;
        }

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            modifiedCells = data.ModifiedCells;
            modifiedCellCount = data.ModifiedCellCountBuffer;
            modifiedCellBucketHeads = data.ModifiedCellBucketHeads;
            modifiedCellNext = data.ModifiedCellNext;
            if (!modifiedCells.IsCreated ||
                modifiedCells.Length < modifiedCellCapacity ||
                !modifiedCellCount.IsCreated ||
                modifiedCellCount.Length < 1 ||
                !modifiedCellBucketHeads.IsCreated ||
                modifiedCellBucketHeads.Length < modifiedCellBucketCount ||
                !modifiedCellNext.IsCreated ||
                modifiedCellNext.Length < modifiedCellCapacity)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                data.ModifiedCellCount = 0;
                data.ModifiedCellBucketCount = 0;
                return false;
            }

            modifiedCells = modifiedCells.GetSubArray(0, modifiedCellCapacity);
            modifiedCellBucketHeads = modifiedCellBucketHeads.GetSubArray(0, modifiedCellBucketCount);
            modifiedCellNext = modifiedCellNext.GetSubArray(0, modifiedCellCapacity);
            modifiedCellCount[0] = 0;
            if (!_deltaProcessor.TryFillDeltaArrayForVolume(
                    data.SourceVolume,
                    modifiedCells,
                    modifiedCellCount,
                    modifiedCellBucketHeads,
                    modifiedCellNext,
                    modifiedCellBucketCount))
            {
                int resolvedCount = modifiedCellCount[0];
                modifiedCellCount[0] = 0;
                data.ModifiedCellCount = 0;
                data.ModifiedCellBucketCount = 0;
                if (resolvedCount < 0)
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return false;
                }

                return true;
            }

            data.ModifiedCellCount = math.clamp(modifiedCellCount[0], 0, modifiedCells.Length);
            modifiedCellCount[0] = data.ModifiedCellCount;
            data.ModifiedCellBucketCount = data.ModifiedCellCount > 0 ? modifiedCellBucketCount : 0;
            return true;
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }
    }

    async Awaitable<bool> ExecuteVoxelPipelineAsync(VoxelPipelineData data, CancellationToken ct)
    {
        if (!data.ScratchLease.IsValid)
        {
            data.ScratchLease = await AcquireStreamingScratchLeaseAsync(data.PtsX * data.PtsZ, data.TotalPts, data.TotalCells, data.GridDimension, ct);
        }
        else if (data.ScratchLease._owner != this)
        {
            return false;
        }

        if (!data.ScratchLease.IsValid)
            return false;

        long chunkGenerationFrameStart = Stopwatch.GetTimestamp();

        if (!BuildSpatialPartitions(data))
            return false;
        chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
        if (!TryPrepareModifiedCellsForPipeline(data))
            return false;

        float fallbackHeight = data.UseConstantTerrainHeight ? data.ConstantTerrainHeight : data.TerrainHeightCenter;
        HectonMapMagicVegetationBridge vegetationBridge = data.UseConstantTerrainHeight ? null : _vegetationBridge;
        double3 absoluteGridOriginDouble = new double3(data.VolumeOrigin.x, 0d, data.VolumeOrigin.z) + data.AbsoluteUniverseOffsetAtStartDouble;
        chunkGenerationFrameStart = await FillTerrainHeightGridForPipelineAsync(
            data,
            vegetationBridge,
            fallbackHeight,
            absoluteGridOriginDouble,
            chunkGenerationFrameStart,
            ct);
        if (chunkGenerationFrameStart == long.MinValue || ct.IsCancellationRequested)
            return false;

        chunkGenerationFrameStart = await FillBiomeModifierGridAsync(
            data,
            vegetationBridge,
            chunkGenerationFrameStart,
            ct);
        if (chunkGenerationFrameStart == long.MinValue || ct.IsCancellationRequested)
            return false;

        NativeArray<float> terrainHeights = default;
        NativeArray<float> gridBiome = default;
        NativeArray<float> densityField = default;
        NativeArray<float> smoothDensityField = default;
        NativeArray<float> overhangDensityField = default;
        NativeArray<AnomalyFeatureRecord> anomalyFeatureRecords = default;
        NativeArray<byte> anomalyFissureMask = default;
        NativeArray<AnomalyFeatureRecord> selectedPillarFeature = default;
        NativeArray<int> chunkContentFlags = default;
        NativeArray<int> densityFaultFlags = default;
        NativeArray<int> cellVertexCounts = default;
        NativeArray<int> cellVertexOffsets = default;
        NativeArray<sbyte> quantizedDensityField = default;
        float densityDecodeScale = 1f;

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
        terrainHeights = data.ScratchLease.TerrainHeights;
        gridBiome = data.ScratchLease.GridBiome;
        densityField = data.ScratchLease.DensityField;
        smoothDensityField = data.ScratchLease.SmoothDensityField;
        overhangDensityField = data.ScratchLease.OverhangDensityField;
        anomalyFeatureRecords = data.ScratchLease.AnomalyFeatureRecords;
        anomalyFissureMask = data.ScratchLease.AnomalyFissureMask;
        selectedPillarFeature = data.ScratchLease.SelectedPillarFeature;
        chunkContentFlags = data.ScratchLease.ChunkContentFlags;
        densityFaultFlags = data.ScratchLease.DensityFaultFlags;
        cellVertexCounts = data.ScratchLease.CellVertexCounts;
        cellVertexOffsets = data.ScratchLease.CellVertexOffsets;

        int heightGridLength = data.PtsX * data.PtsZ;
        if (!terrainHeights.IsCreated ||
            terrainHeights.Length < heightGridLength ||
            !gridBiome.IsCreated ||
            gridBiome.Length < heightGridLength ||
            !densityField.IsCreated ||
            densityField.Length < data.TotalPts ||
            !smoothDensityField.IsCreated ||
            smoothDensityField.Length < data.TotalPts ||
            !overhangDensityField.IsCreated ||
            overhangDensityField.Length < data.TotalPts ||
            !anomalyFeatureRecords.IsCreated ||
            anomalyFeatureRecords.Length < heightGridLength ||
            !anomalyFissureMask.IsCreated ||
            anomalyFissureMask.Length < heightGridLength ||
            !selectedPillarFeature.IsCreated ||
            selectedPillarFeature.Length < 1 ||
            !chunkContentFlags.IsCreated ||
            chunkContentFlags.Length < 1 ||
            !densityFaultFlags.IsCreated ||
            densityFaultFlags.Length < VoxelDensityPipelineFaultSlots.SlotCount ||
            !cellVertexCounts.IsCreated ||
            cellVertexCounts.Length < data.TotalCells ||
            !cellVertexOffsets.IsCreated ||
            cellVertexOffsets.Length < data.TotalCells)
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }
        ClearVoxelDensityFaultFlags(densityFaultFlags);
        bool navGridScheduled = false;
        JobHandle navGridHandle = default;

        JobHandle densityHandle = new VoxelDensityJob
        {
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            terrainHeights = terrainHeights,
            gridBiome = gridBiome,
            caveNodes = data.Nodes,
            caveTunnels = data.Tunnels,
            caveEntrances = data.Entrances,
            caveStructures = data.Structures,
            craterStamps = data.CraterStamps,
            modifiedCells = data.ModifiedCells,
            modifiedCellBucketHeads = data.ModifiedCellBucketHeads,
            modifiedCellNext = data.ModifiedCellNext,
            modifiedCellCount = data.ModifiedCellCount,
            modifiedCellBucketCount = data.ModifiedCellBucketCount,
            nodeBucketOffsets = data.NodeBucketOffsets,
            nodeBucketIndices = data.NodeBucketIndices,
            tunnelBucketOffsets = data.TunnelBucketOffsets,
            tunnelBucketIndices = data.TunnelBucketIndices,
            caveParams = data.CaveParams,
            absoluteNoiseOffset = ResolveWrappedAupNoiseOffset(data.AbsoluteUniverseOffsetAtStartDouble),
            absoluteCellOffset = data.AbsoluteUniverseOffsetAtStartDouble,
            partitionDimX = data.PartitionDimX,
            partitionDimY = data.PartitionDimY,
            partitionDimZ = data.PartitionDimZ,
            partitionOrigin = data.PartitionOrigin,
            partitionInvCellSize = new float3(
                1f / math.max(data.PartitionCellSize.x, 0.01f),
                1f / math.max(data.PartitionCellSize.y, 0.01f),
                1f / math.max(data.PartitionCellSize.z, 0.01f)),
            sealMargin = data.EffectiveSealMargin,
            lodLevel = data.LODLevel,
            lodTransitionBand = data.LodTransitionBand,
            enableBiomeSdfModifiers = ResolveBiomeSdfModifierEnabled(data.LODLevel),
            density = densityField,
            smoothDensity = smoothDensityField,
            densityFaultFlags = densityFaultFlags,
            PrimaryFrequency = 0.012f,
            SecondaryFrequency = 0.017f,
            CarveStrengthMeters = 28.0f,
            CaveThreshold = 0.65f,
            MaxCrustDepthMeters = 400.0f,
            SurfaceProtectionMeters = 30.0f,
            StrataLayerThicknessMeters = 24.0f,
            StrataShelvingStrength = 0.4f,
            WorldSeed = data.Seed
        }.Schedule(data.TotalPts, JOB_BATCH);
        long anomalySolveStartTimestamp = Stopwatch.GetTimestamp();

        double3 terrainOriginAup = absoluteGridOriginDouble;
        double3 sdfOriginAup = global::Hecton8.World.AUPMath.ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;
        JobHandle pillarDetectionHandle = HectonAnomalyEngine.ScheduleRidgeFeatureDetection(
            terrainHeights,
            anomalyFeatureRecords,
            anomalyFissureMask,
            new AnomalyRidgeDetectionSettings
            {
                Width = data.PtsX,
                Height = data.PtsZ,
                CellSizeMeters = data.VoxelStep,
                OriginAup = terrainOriginAup,
                MinimumPillarProminenceMeters = ChthonicPillarMinimumProminenceMeters,
                MinimumPillarRidgeArms = 3,
                MinimumFissureDepthMeters = 15f,
                EqualHeightEpsilon = 0.001f,
                FissureInfluencePacked = 0u,
                RequireTectonicBoundary = 1,
                TectonicBoundaryFrequency = mapMagicBridge != null
                    ? mapMagicBridge.SandboxTectonicSpineFrequency
                    : ChthonicPillarTectonicBoundaryFrequencyFallback,
                TectonicBoundarySeed = mapMagicBridge != null
                    ? mapMagicBridge.SandboxTectonicSpineSeed
                    : ChthonicPillarTectonicBoundarySeedFallback,
                MinimumTectonicBoundaryMask = ChthonicPillarMinimumTectonicBoundaryMask
            });
        JobHandle selectedPillarHandle = new SelectStrongestPillarFeatureJob
        {
            FeatureRecords = anomalyFeatureRecords,
            SelectedFeature = selectedPillarFeature
        }.Schedule(pillarDetectionHandle);

        // R99 SDF-TRUTH FIX: cliff overhang noise is applied to the density field BEFORE extraction, so
        // it is part of world geometry and collision — not presentation. It used to be gated on
        // ShouldApplyCameraFacingOverhangNoise(data) and scaled by HomeostasisBrain.GlobalQualityWeight,
        // which meant rebuilding the SAME volume while looking away, or after a quality change, produced
        // DIFFERENT rock and a DIFFERENT collider. voxels.md: "GlobalQualityWeight ... must not change SDF
        // truth, carve permission, save delta identity, collision bake requirements". A camera-direction
        // dependency additionally makes voxel carve deltas non-reproducible from seed.
        // The cull is not a legal optimization here — back-facing chunks must be made cheaper by not
        // building them (LOD/residency), never by giving them different geometry.
        {
            densityHandle = HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise(
                densityField,
                overhangDensityField,
                data.PtsX,
                data.PtsY,
                data.PtsZ,
                data.VoxelStep,
                CliffOverhangSlopeThreshold,
                CliffOverhangLateralAmplitudeMeters,
                CliffOverhangNoiseFrequency,
                CliffOverhangBlendStrength,
                sdfOriginAup,
                densityHandle);
            densityField = overhangDensityField;
        }

        var snapTopCellsJob = new SnapDualSDFTopCellsToTerrainJob
        {
            TerrainHeights = terrainHeights,
            TerrainWidth = data.PtsX,
            TerrainDepth = data.PtsZ,
            TerrainCellSizeMeters = data.VoxelStep,
            TerrainOriginAup = terrainOriginAup,
            Sdf = densityField,
            SecondarySdf = default,
            WriteSecondary = 0,
            SdfWidth = data.PtsX,
            SdfHeight = data.PtsY,
            SdfDepth = data.PtsZ,
            VoxelSizeMeters = data.VoxelStep,
            SdfOriginAup = sdfOriginAup,
            SnapHysteresisMeters = VoxelTerrainSnapHysteresisMeters
        };
        densityHandle = Unity.Jobs.IJobParallelForExtensions.Schedule(
            snapTopCellsJob,
            data.PtsX * data.PtsZ,
            JOB_BATCH,
            densityHandle);

        densityHandle = HectonAnomalyEngine.InjectSelectedMegaPillarSDF(
            densityField,
            selectedPillarFeature,
            data.PtsX,
            data.PtsY,
            data.PtsZ,
            data.VoxelStep,
            sdfOriginAup,
            ChthonicPillarRadiusMeters,
            ChthonicPillarHeightMeters,
            ChthonicPillarEdgeWarpMeters,
            ChthonicPillarNoiseFrequency,
            JobHandle.CombineDependencies(densityHandle, selectedPillarHandle));

        densityHandle = HectonAnomalyEngine.InjectFissureNetworkSDF(
            densityField,
            anomalyFissureMask,
            terrainHeights,
            data.PtsX,
            data.PtsY,
            data.PtsZ,
            data.VoxelStep,
            sdfOriginAup,
            500f, // Deep canyon depth
            densityHandle);

        chunkContentFlags[0] = 1;
        JobHandle chunkContentHandle = new VoxelChunkBoundsContentJob
        {
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            density = densityField,
            hasContent = chunkContentFlags
        }.Schedule(densityHandle);

        await AwaitForJobCompletionAsync(chunkContentHandle, ct, "density/content bounds phase");
        PublishVoxelAnomalySolveWarningIfNeeded(anomalySolveStartTimestamp);
        if (ct.IsCancellationRequested)
            return false;
        ReportAndClearVoxelDensityFaults(densityFaultFlags);
        if (chunkContentFlags[0] == 0)
        {
            data.RawCount = 0;
            return false;
        }

        densityHandle = chunkContentHandle;
        if (!MCTables.TryAcquireJobTables(_streamingScratchVault, out MCTables.JobTableLease mcCountTables))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        quantizedDensityField = data.ScratchLease.QuantizedDensityField;
        densityDecodeScale = ResolveDensityDecodeScale(data.VoxelStep);
        float densityDecodeInvScale = 1f / math.max(densityDecodeScale, 0.0001f);
        JobHandle quantizeDensityHandle = new VoxelDensityQuantizeJob
        {
            densityDecodeInvScale = densityDecodeInvScale,
            density = densityField,
            quantizedDensity = quantizedDensityField,
            densityFaultFlags = densityFaultFlags
        }.Schedule(data.TotalPts, JOB_BATCH, densityHandle);

        if (data.SourceVolume != null &&
            VoxelDynamicNavGridRuntime.TryScheduleBuild(
                data.SourceVolume,
                data.SourceRuntimeStamp,
                new int3(data.PtsX, data.PtsY, data.PtsZ),
                data.VolumeOrigin,
                data.VoxelStep,
                data.TotalPts,
                densityField,
                JOB_BATCH,
                densityHandle,
                out navGridHandle))
        {
            navGridScheduled = true;
        }

        JobHandle mcCountHandle;
        try
        {
            mcCountHandle = new VoxelMCCountJob
            {
                cellsX = data.GridDimension,
                cellsY = data.GridDimension,
                cellsZ = data.GridDimension,
                ptsX = data.PtsX,
                ptsY = data.PtsY,
                ptsZ = data.PtsZ,
                densityDecodeScale = densityDecodeScale,
                density = quantizedDensityField,
                edgeTable = mcCountTables.EdgeTable,
                triTable = mcCountTables.TriTable,
                cellVertexCounts = cellVertexCounts,
                densityFaultFlags = densityFaultFlags
            }.Schedule(data.TotalCells, JOB_BATCH, quantizeDensityHandle);

            JobHandle firstPhaseHandle = navGridScheduled
                ? JobHandle.CombineDependencies(mcCountHandle, navGridHandle)
                : mcCountHandle;
            await AwaitForJobCompletionAsync(firstPhaseHandle, ct, "density/count phase");
            ReportAndClearVoxelDensityFaults(densityFaultFlags);
            if (navGridScheduled)
                VoxelDynamicNavGridRuntime.CommitBuild(data.SourceVolume, data.SourceRuntimeStamp);
        }
        finally
        {
            mcCountTables.Dispose();
        }

        TryBindSelectedChthonicPillarResources(selectedPillarFeature, _resourceDistributionDirector);
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }

        int exactRawVertexCount = await BuildRawVertexOffsetsAsync(data, chunkGenerationFrameStart, ct);
        if (exactRawVertexCount < 3 || ct.IsCancellationRequested)
            return false;
        data.RawCount = exactRawVertexCount;

        int edgeVertexCountX = data.GridDimension * data.PtsY * data.PtsZ;
        int edgeVertexCountY = data.PtsX * data.GridDimension * data.PtsZ;
        int edgeVertexCountZ = data.PtsX * data.PtsY * data.GridDimension;
        if (!TryEnsureMeshExtractionScratchCapacity(
                ref data.ScratchLease,
                data.RawCount,
                edgeVertexCountX,
                edgeVertexCountY,
                edgeVertexCountZ))
        {
            return false;
        }

        data.UsesStreamingScratchMeshBuffers = true;

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        // Declared outside the try: the weld-overflow guard below runs after this block
        // closes, and reading it from inside the try left the fail-closed check unreachable.
        bool weldOutputFault = false;
        try
        {
        quantizedDensityField = data.ScratchLease.QuantizedDensityField;
        cellVertexCounts = data.ScratchLease.CellVertexCounts;
        cellVertexOffsets = data.ScratchLease.CellVertexOffsets;
        densityFaultFlags = data.ScratchLease.DensityFaultFlags;
        if (!densityFaultFlags.IsCreated || densityFaultFlags.Length < VoxelDensityPipelineFaultSlots.SlotCount)
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }
        ClearVoxelDensityFaultFlags(densityFaultFlags);

        JobHandle clearEdgeXHandle = new VoxelFillIntArrayJob
        {
            Value = -1,
            Values = data.EdgeVertexX
        }.Schedule(data.EdgeVertexX.Length, JOB_BATCH);
        JobHandle clearEdgeYHandle = new VoxelFillIntArrayJob
        {
            Value = -1,
            Values = data.EdgeVertexY
        }.Schedule(data.EdgeVertexY.Length, JOB_BATCH);
        JobHandle clearEdgeZHandle = new VoxelFillIntArrayJob
        {
            Value = -1,
            Values = data.EdgeVertexZ
        }.Schedule(data.EdgeVertexZ.Length, JOB_BATCH);
        JobHandle clearEdgesHandle = JobHandle.CombineDependencies(clearEdgeXHandle, clearEdgeYHandle, clearEdgeZHandle);
        await AwaitForJobCompletionAsync(clearEdgesHandle, ct, "edge vertex registry clear");

        if (!MCTables.TryAcquireJobTables(_streamingScratchVault, out MCTables.JobTableLease mcExtractTables))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        JobHandle mcHandle;
        try
        {
            mcHandle = new VoxelMCExtractJob
            {
                cellsX = data.GridDimension,
                cellsY = data.GridDimension,
                cellsZ = data.GridDimension,
                ptsX = data.PtsX,
                ptsY = data.PtsY,
                ptsZ = data.PtsZ,
                volumeOrigin = data.VolumeOrigin,
                voxelStep = data.VoxelStep,
                densityDecodeScale = densityDecodeScale,
                density = quantizedDensityField,
                edgeTable = mcExtractTables.EdgeTable,
                triTable = mcExtractTables.TriTable,
                cellVertexOffsets = cellVertexOffsets,
                cellVertexCounts = cellVertexCounts,
                outVertices = data.RawVertices,
                densityFaultFlags = densityFaultFlags
            }.Schedule(data.TotalCells, JOB_BATCH);

            await AwaitForJobCompletionAsync(mcHandle, ct, "marching-cubes extract");
        }
        finally
        {
            mcExtractTables.Dispose();
        }

        if (ct.IsCancellationRequested)
            return false;

        NativeArray<int> weldedCounter = data.ScratchLease.MeshWeldedCounter;
        weldedCounter[0] = 0;

        try
        {
            JobHandle weldHandle = new VoxelWeldJob
            {
                rawCount = data.RawCount,
                ptsX = data.PtsX,
                ptsY = data.PtsY,
                ptsZ = data.PtsZ,
                rawVertices = data.RawVertices,
                edgeVertexX = data.EdgeVertexX,
                edgeVertexY = data.EdgeVertexY,
                edgeVertexZ = data.EdgeVertexZ,
                weldedPositions = data.WeldedPositions,
                triangleIndices = data.TriangleIndices,
                weldedCounter = weldedCounter,
                densityFaultFlags = densityFaultFlags
            }.Schedule();

            await AwaitForJobCompletionAsync(weldHandle, ct, "vertex weld");

            data.WeldedCount = weldedCounter[0];
            weldOutputFault = IsVoxelDensityFaultSlotSet(
                densityFaultFlags,
                VoxelDensityPipelineFaultSlots.WeldOutput);
            ReportAndClearVoxelDensityFaults(densityFaultFlags);
        }
        finally
        {
            weldedCounter[0] = 0;
        }
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }

        // TriangleIndices is pooled scratch. A weld overflow leaves the unwritten tail holding
        // indices from the previous chunk, so fail closed before sanitize, rendering, or collider
        // publication can consume a partially updated buffer.
        if (weldOutputFault)
        {
            data.WeldedCount = 0;
            return false;
        }

        if (data.WeldedCount < 3)
            return false;

        if (ct.IsCancellationRequested)
            return false;

        if (!TryEnsureMeshAttributeScratchCapacity(ref data.ScratchLease, data.WeldedCount))
            return false;

        data.UsesStreamingScratchAttributeBuffers = true;
        if (data.ExtractSpawnPoints)
        {
            int maxSpawnPoints = math.max(data.WeldedCount / 20, 64);
            if (!TryPrepareSpawnPointScratch(ref data.ScratchLease, maxSpawnPoints))
            {
                return false;
            }

            data.UsesStreamingScratchSpawnPoints = true;
        }

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
        terrainHeights = data.ScratchLease.TerrainHeights;
        gridBiome = data.ScratchLease.GridBiome;
        quantizedDensityField = data.ScratchLease.QuantizedDensityField;
        densityFaultFlags = data.ScratchLease.DensityFaultFlags;
        if (!densityFaultFlags.IsCreated || densityFaultFlags.Length < VoxelDensityPipelineFaultSlots.SlotCount)
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }
        ClearVoxelDensityFaultFlags(densityFaultFlags);

        JobHandle clearSkirtAlphaHandle = new VoxelFillFloatArrayJob
        {
            Value = 0f,
            Values = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH);
        JobHandle clearDirtyBlendHandle = new VoxelFillFloatArrayJob
        {
            Value = 0f,
            Values = data.DirtyBlendValues
        }.Schedule(data.WeldedCount, JOB_BATCH);

        JobHandle seamSnapHandle = new VoxelTerrainSeamSnapJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            seamTransitionBand = TerrainVoxelSeamTransitionBand,
            seamOverlap = VoxelSeamDirector.TerrainOverlapMeters,
            terrainHeights = terrainHeights,
            positions = data.WeldedPositions
        }.Schedule(data.WeldedCount, JOB_BATCH);
        JobHandle skirtDependencyHandle = JobHandle.CombineDependencies(seamSnapHandle, clearSkirtAlphaHandle);

        JobHandle skirtHandle = new VoxelChunkSkirtExtrusionJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            skirtDepthMeters = VoxelChunkSkirtDepthMeters,
            skirtWidthMeters = math.max(VoxelChunkSkirtWidthMeters, data.VoxelStep),
            lodLevel = data.LODLevel,
            positions = data.WeldedPositions,
            skirtAlphaValues = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH, skirtDependencyHandle);

        JobHandle normalHandle = new VoxelNormalJob
        {
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            densityStrideY = data.PtsX,
            densityStrideZ = data.PtsX * data.PtsY,
            volumeOrigin = data.VolumeOrigin,
            invVoxelStep = 1f / math.max(data.VoxelStep, 0.0001f),
            densityField = quantizedDensityField,
            positions = data.WeldedPositions,
            normals = data.Normals,
            curvatureValues = data.CurvatureValues,
            ambientOcclusionValues = data.AmbientOcclusionValues,
            densityFaultFlags = densityFaultFlags
        }.Schedule(data.WeldedCount, JOB_BATCH, skirtHandle);

        JobHandle seamNormalHandle = new VoxelSeamNormalBlendJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            seamTransitionBand = TerrainVoxelSeamTransitionBand,
            positions = data.WeldedPositions,
            terrainHeights = terrainHeights,
            normals = data.Normals
        }.Schedule(data.WeldedCount, JOB_BATCH, normalHandle);

        JobHandle biomeHandle = new VoxelBiomeSampleJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            gridBiome = gridBiome,
            positions = data.WeldedPositions,
            biomeValues = data.BiomeValues
        }.Schedule(data.WeldedCount, JOB_BATCH, skirtHandle);

        JobHandle colorDeps = JobHandle.CombineDependencies(seamNormalHandle, biomeHandle);
        JobHandle colorHandle = new VoxelColorJob
        {
            maxDepth = ABYSSAL_MAX_DEPTH,
            caveEdgeWidth = caveEdgeColorWidth,
            seamTransitionBand = TerrainVoxelSeamTransitionBand,
            volumeCenter = data.WorldCenter,
            volumeHalfExtent = data.VolumeHalfExtent,
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            lodLevel = data.LODLevel,
            lodTransitionBand = data.LodTransitionBand,
            positions = data.WeldedPositions,
            normals = data.Normals,
            terrainHeights = terrainHeights,
            curvatureValues = data.CurvatureValues,
            ambientOcclusionValues = data.AmbientOcclusionValues,
            biomeValues = data.BiomeValues,
            caveEntrances = data.Entrances,
            modifiedCells = data.ModifiedCells,
            modifiedCellBucketHeads = data.ModifiedCellBucketHeads,
            modifiedCellNext = data.ModifiedCellNext,
            modifiedCellCount = data.ModifiedCellCount,
            modifiedCellBucketCount = data.ModifiedCellBucketCount,
            absoluteCellOffset = data.AbsoluteUniverseOffsetAtStartDouble,
            colors = data.Colors,
            skirtAlphaValues = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH, colorDeps);

        JobHandle phase5Handle = JobHandle.CombineDependencies(colorHandle, clearDirtyBlendHandle);
        if (data.ModifiedCellCount > 0 && data.ModifiedCells.IsCreated)
        {
            JobHandle dirtyBlendDependencyHandle = JobHandle.CombineDependencies(skirtHandle, clearDirtyBlendHandle);
            JobHandle dirtyBlendHandle = new VoxelDirtyBlendJob
            {
                positions = data.WeldedPositions,
                modifiedCells = data.ModifiedCells,
                modifiedCellBucketHeads = data.ModifiedCellBucketHeads,
                modifiedCellNext = data.ModifiedCellNext,
                modifiedCellCount = data.ModifiedCellCount,
                modifiedCellBucketCount = data.ModifiedCellBucketCount,
                voxelStep = data.VoxelStep,
                absoluteCellOffset = data.AbsoluteUniverseOffsetAtStartDouble,
                dirtyBlendValues = data.DirtyBlendValues
            }.Schedule(data.WeldedCount, JOB_BATCH, dirtyBlendDependencyHandle);

            phase5Handle = JobHandle.CombineDependencies(phase5Handle, dirtyBlendHandle);
        }

        if (data.ExtractSpawnPoints)
        {
            NativeArray<CaveSpawnData> spawnPoints = data.SpawnPointList;
            NativeArray<int> spawnPointCount = data.SpawnPointCountBuffer;
            if (!spawnPoints.IsCreated ||
                !spawnPointCount.IsCreated ||
                spawnPointCount.Length < 1)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            spawnPointCount[0] = 0;
            JobHandle spawnHandle = new VoxelSpawnPointJob
            {
                positions = data.WeldedPositions,
                normals = data.Normals,
                ambientOcclusionValues = data.AmbientOcclusionValues,
                volumeCenter = data.WorldCenter,
                volumeHalfExtent = data.VolumeHalfExtent,
                floorNormalThreshold = 0.75f,
                minInteriorDepth = 0.15f,
                keepFraction = 0.03f,
                seed = data.Seed,
                spawnPoints = spawnPoints,
                spawnPointCount = spawnPointCount,
                spawnPointCapacity = spawnPoints.Length
            }.Schedule(seamNormalHandle);

            phase5Handle = JobHandle.CombineDependencies(phase5Handle, spawnHandle);
        }

        await AwaitForJobCompletionAsync(phase5Handle, ct, "normal/color/spawn phase");
        if (ct.IsCancellationRequested)
            return false;
        ReportAndClearVoxelDensityFaults(densityFaultFlags);
        return true;
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }
    }

    async Awaitable<int> BuildRawVertexOffsetsAsync(
        VoxelPipelineData data,
        long chunkGenerationFrameStart,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return -1;

        int exactRawVertexCount = 0;
        int cellIndex = 0;
        while (cellIndex < data.TotalCells)
        {
            int sliceEnd = math.min(cellIndex + 1024, data.TotalCells);
            if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return -1;
            }

            try
            {
                NativeArray<int> cellVertexCounts = data.ScratchLease.CellVertexCounts;
                NativeArray<int> cellVertexOffsets = data.ScratchLease.CellVertexOffsets;
                if (!cellVertexCounts.IsCreated ||
                    cellVertexCounts.Length < data.TotalCells ||
                    !cellVertexOffsets.IsCreated ||
                    cellVertexOffsets.Length < data.TotalCells)
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return -1;
                }

                for (; cellIndex < sliceEnd; cellIndex++)
                {
                    int cellVertexCount = cellVertexCounts[cellIndex];
                    if (cellVertexCount < 0 ||
                        exactRawVertexCount < 0 ||
                        exactRawVertexCount > data.MaxVerts - cellVertexCount)
                    {
                        ReportVoxelMeshScratchCapacityOverflow();
                        return -1;
                    }

                    cellVertexOffsets[cellIndex] = exactRawVertexCount;
                    exactRawVertexCount += cellVertexCount;
                }
            }
            finally
            {
                UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
            }

            chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            if (ct.IsCancellationRequested)
                return -1;
        }

        return exactRawVertexCount;
    }

    async Awaitable<long> FillTerrainHeightGridForPipelineAsync(
        VoxelPipelineData data,
        HectonMapMagicVegetationBridge vegetationBridge,
        float fallbackHeight,
        double3 absoluteGridOriginDouble,
        long chunkGenerationFrameStart,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return long.MinValue;

        bool hasCurrentOriginAup = TryResolveCurrentRuntimeOriginAbsolute(out double3 currentOriginAup);
        bool sampledHeightGrid = false;
        if (vegetationBridge != null &&
            hasCurrentOriginAup &&
            TryResolveRuntimeVector3FromAup(absoluteGridOriginDouble, currentOriginAup, out Vector3 runtimeGridOrigin))
        {
            if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return long.MinValue;
            }

            try
            {
                NativeArray<float> terrainHeights = data.ScratchLease.TerrainHeights;
                int heightGridLength = data.PtsX * data.PtsZ;
                if (!terrainHeights.IsCreated || terrainHeights.Length < heightGridLength)
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return long.MinValue;
                }

                sampledHeightGrid = vegetationBridge.TryFillTerrainHeightGridFromNativeCache(
                    runtimeGridOrigin.x,
                    runtimeGridOrigin.z,
                    data.PtsX,
                    data.PtsZ,
                    data.VoxelStep,
                    terrainHeights,
                    fallbackHeight);
            }
            finally
            {
                UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
            }
        }

        if (sampledHeightGrid)
            return chunkGenerationFrameStart;

        for (int iz = 0; iz < data.PtsZ; iz++)
        {
            if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return long.MinValue;
            }

            try
            {
                NativeArray<float> terrainHeights = data.ScratchLease.TerrainHeights;
                int heightGridLength = data.PtsX * data.PtsZ;
                if (!terrainHeights.IsCreated || terrainHeights.Length < heightGridLength)
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return long.MinValue;
                }

                for (int ix = 0; ix < data.PtsX; ix++)
                {
                    float wx = data.VolumeOrigin.x + ix * data.VoxelStep;
                    float wz = data.VolumeOrigin.z + iz * data.VoxelStep;
                    int hi = ix + iz * data.PtsX;

                    double3 absoluteSampleAup = new double3(wx, 0d, wz) + data.AbsoluteUniverseOffsetAtStartDouble;
                    if (vegetationBridge != null &&
                        mapMagicBridge != null &&
                        hasCurrentOriginAup &&
                        TryResolveRuntimeVector3FromAup(absoluteSampleAup, currentOriginAup, out Vector3 runtimeSamplePosition) &&
                        mapMagicBridge.TryGetHeight(runtimeSamplePosition.x, runtimeSamplePosition.z, out float sampledHeight))
                    {
                        terrainHeights[hi] = sampledHeight;
                    }
                    else
                    {
                        terrainHeights[hi] = fallbackHeight;
                    }
                }
            }
            finally
            {
                UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
            }

            chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            if (ct.IsCancellationRequested)
                return long.MinValue;
            hasCurrentOriginAup = TryResolveCurrentRuntimeOriginAbsolute(out currentOriginAup);
        }

        return chunkGenerationFrameStart;
    }

    async Awaitable<long> FillBiomeModifierGridAsync(
        VoxelPipelineData data,
        HectonMapMagicVegetationBridge vegetationBridge,
        long chunkGenerationFrameStart,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return long.MinValue;

        bool monolithReady = H8StaticDataArena.IsLoaded;
        HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload = default;
        bool hasHeightPayload = vegetationBridge != null &&
            vegetationBridge.TryGetActiveHeightTexturePayload(out heightPayload);
        Vector3 terrainPosition = hasHeightPayload ? heightPayload.TerrainPosition : Vector3.zero;
        Vector3 terrainSize = hasHeightPayload ? heightPayload.TerrainSize : Vector3.zero;
        float invTerrainSizeX = hasHeightPayload ? math.rcp(math.max(terrainSize.x, 0.001f)) : 0f;
        float invTerrainSizeZ = hasHeightPayload ? math.rcp(math.max(terrainSize.z, 0.001f)) : 0f;
        float fallbackTileSize = math.max(mapMagicTileSize, data.VoxelStep * math.max(data.PtsX - 1, 1));
        float fallbackInvTileSize = math.rcp(math.max(fallbackTileSize, 0.001f));

        // R100 FIX (biome heatmap collapsed to one cell): the height payload's TerrainPosition is
        // runtime/presentation space - HectonMapMagicVegetationBridge rebases it on every origin shift -
        // while the per-sample absoluteX/absoluteZ below are AUP. Subtracting one from the other left the
        // entire floating-origin offset in the result, so at AUP scale u/v were on the order of 10^3,
        // math.saturate pinned them to 1.0, and every voxel in the world sampled the same heatmap corner
        // cell. Lifting TerrainPosition into AUP once here keeps the subtraction in double, narrows only
        // the resulting 0-1 UV, and costs nothing per voxel.
        // Resolved once per chunk here rather than per voxel; this method does not receive the origin.
        double3 biomeRuntimeOriginAup = default;
        bool hasTerrainAupOrigin = hasHeightPayload &&
            TryResolveCurrentRuntimeOriginAbsolute(out biomeRuntimeOriginAup);
        double terrainOriginAupX = hasTerrainAupOrigin ? biomeRuntimeOriginAup.x + terrainPosition.x : 0d;
        double terrainOriginAupZ = hasTerrainAupOrigin ? biomeRuntimeOriginAup.z + terrainPosition.z : 0d;
        bool hasCachedBiomeHash = false;
        uint cachedBiomeHash = 0u;
        float cachedBiomeModifier = 0f;

        for (int iz = 0; iz < data.PtsZ; iz++)
        {
            double absoluteZ = (double)data.VolumeOrigin.z + iz * data.VoxelStep + data.AbsoluteUniverseOffsetAtStartDouble.z;
            if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return long.MinValue;
            }

            try
            {
                NativeArray<float> gridBiome = data.ScratchLease.GridBiome;
                int gridLength = data.PtsX * data.PtsZ;
                if (!gridBiome.IsCreated || gridBiome.Length < gridLength)
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return long.MinValue;
                }

                for (int ix = 0; ix < data.PtsX; ix++)
                {
                    int gridIndex = ix + iz * data.PtsX;
                    float modifier = 0f;
                    if (monolithReady)
                    {
                        double absoluteX = (double)data.VolumeOrigin.x + ix * data.VoxelStep + data.AbsoluteUniverseOffsetAtStartDouble.x;
                        float u = hasTerrainAupOrigin
                            ? math.saturate((float)((absoluteX - terrainOriginAupX) * invTerrainSizeX))
                            : math.frac((float)(absoluteX * fallbackInvTileSize));
                        float v = hasTerrainAupOrigin
                            ? math.saturate((float)((absoluteZ - terrainOriginAupZ) * invTerrainSizeZ))
                            : math.frac((float)(absoluteZ * fallbackInvTileSize));
                        int heatmapX = math.clamp((int)(u * BiomeHeatmapMaxIndex + 0.5f), 0, BiomeHeatmapMaxIndex);
                        int heatmapY = math.clamp((int)(v * BiomeHeatmapMaxIndex + 0.5f), 0, BiomeHeatmapMaxIndex);
                        if (H8StaticDataArena.TryGetBiomeHeatmapCell(heatmapX, heatmapY, out uint biomeHash))
                        {
                            if (hasCachedBiomeHash && cachedBiomeHash == biomeHash)
                            {
                                modifier = cachedBiomeModifier;
                            }
                            else
                            {
                                modifier = ResolveAlienBiomeModifierWeight(biomeHash);
                                cachedBiomeHash = biomeHash;
                                cachedBiomeModifier = modifier;
                                hasCachedBiomeHash = true;
                            }
                        }
                    }

                    gridBiome[gridIndex] = modifier;
                }
            }
            finally
            {
                UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
            }

            chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            if (ct.IsCancellationRequested)
                return long.MinValue;
        }

        return chunkGenerationFrameStart;
    }

    static float ResolveAlienBiomeModifierWeight(uint biomeHash)
    {
        if (biomeHash == 0u)
            return 0f;

        if (biomeHash == _VoxelAlienBiomeHash ||
            biomeHash == _VoxelAlienShortBiomeHash)
        {
            return 1f;
        }

        if (TryResolveVoxelBiomeRecord(biomeHash, out H8BiomeRecord record))
        {
            if (record.SurfaceId == _VoxelAlienSurfaceHash ||
                record.HeatmapId == _VoxelAlienHeatmapHash ||
                record.RadiationFieldHash == _VoxelAlienRadiationHash)
            {
                return 1f;
            }

        }

        return 0f;
    }

    static unsafe bool TryResolveVoxelBiomeRecord(uint biomeHash, out H8BiomeRecord record)
    {
        record = default;
        if (biomeHash == 0u)
            return false;

        ReadOnlySpan<H8BiomeRecord> records = H8StaticDataArena.GetSectionSpan<H8BiomeRecord>(H8DataSectionId.Biomes);
        if (records.Length <= 0)
            return false;

        int low = 0;
        int high = records.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            H8BiomeRecord candidate = records[mid];
            if (candidate.BiomeHash == biomeHash)
            {
                record = candidate;
                return true;
            }

            if (candidate.BiomeHash < biomeHash)
                low = mid + 1;
            else
                high = mid - 1;
        }

        return false;
    }

    async Awaitable<VoxelStreamingScratchLease> AcquireStreamingScratchLeaseAsync(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension,
        CancellationToken ct)
    {
        int waitFrames = 0;
        while (true)
        {
            if (ct.IsCancellationRequested)
                return default;

            if (TryAcquireStreamingScratchLease(heightCount, totalPointCount, totalCellCount, gridDimension, out VoxelStreamingScratchLease lease))
                return lease;

            if (waitFrames >= StreamingScratchLeaseTimeoutFrames)
            {
                LogStreamingScratchLeaseTimeout(heightCount, totalPointCount, totalCellCount, waitFrames);
                return default;
            }

            waitFrames++;
            await AwaitableDebtMonitor.NextFrameAsync();
            if (ct.IsCancellationRequested)
                return default;
        }
    }

    void LogStreamingScratchLeaseTimeout(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int waitFrames)
    {
#if UNITY_EDITOR
        Hecton8.Core.H8Debug.LogError("[HectonVoxel] Streaming scratch lease timed out.");
#endif
    }

    void GetStreamingScratchLeaseState(out int slotCount, out int inUseCount, out bool teardownRequested)
    {
        using (EnterStreamingScratchGate())
        {
            teardownRequested = _teardownStreamingScratchRequested;
            slotCount = _streamingScratchSlots != null ? _streamingScratchSlots.Length : 0;
            inUseCount = 0;
            for (int i = 0; i < slotCount; i++)
            {
                VoxelStreamingScratchSlot slot = _streamingScratchSlots[i];
                if (slot != null && slot.InUse)
                    inUseCount++;
            }
        }
    }

    bool TryAcquireStreamingScratchLease(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension,
        out VoxelStreamingScratchLease lease)
    {
        lease = default;
        if (_streamingScratchSlots == null && !_teardownStreamingScratchRequested)
            EnsureStreamingScratchSlots();

        using (EnterStreamingScratchGate())
        {
            if (_teardownStreamingScratchRequested)
            {
                TryFinalizeStreamingScratchTeardown_NoLock();
                return false;
            }

            if (_streamingScratchSlots == null || _streamingScratchSlots.Length == 0)
                return false;

            for (int i = 0; i < _streamingScratchSlots.Length; i++)
            {
                VoxelStreamingScratchSlot slot = _streamingScratchSlots[i];
                if (slot == null || slot.InUse)
                    continue;

                if (!TryEnsureStreamingScratchSlotCapacity(slot, i, heightCount, totalPointCount, totalCellCount, gridDimension))
                    continue;

                slot.InUse = true;

                lease = new VoxelStreamingScratchLease(this, i);
                return true;
            }
        }

        return false;
    }

    void ReleaseStreamingScratchLease(int slotIndex)
    {
        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null || slotIndex < 0 || slotIndex >= _streamingScratchSlots.Length)
                return;

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[slotIndex];
            if (slot != null)
                slot.InUse = false;

            if (_teardownStreamingScratchRequested)
                TryFinalizeStreamingScratchTeardown_NoLock();
        }
    }

    void EnsureStreamingScratchSlots()
    {
        if (_streamingScratchVault == null)
            return;

        int slotCount = math.clamp(streamingScratchSlotCount, 1, StreamingScratchSlotMax);
        if (_streamingScratchSlots != null && _streamingScratchSlots.Length == slotCount)
            return;

        if (_streamingScratchSlots != null && HasStreamingScratchSlotInUse_NoLock())
            return;

        DisposeStreamingScratchSlots();
        _streamingScratchSlots = new VoxelStreamingScratchSlot[slotCount]; // COLD ALLOC: VoxelStreamingScratchSlot[streamingScratchSlotCount<=8] - cold voxel scratch descriptor slots - owner: HectonVoxelEngine
        for (int i = 0; i < slotCount; i++)
            _streamingScratchSlots[i] = new VoxelStreamingScratchSlot(); // COLD ALLOC: VoxelStreamingScratchSlot[1] - cold voxel scratch descriptor slot - owner: HectonVoxelEngine
    }

    bool HasStreamingScratchSlotInUse_NoLock()
    {
        if (_streamingScratchSlots == null)
            return false;

        for (int i = 0; i < _streamingScratchSlots.Length; i++)
        {
            VoxelStreamingScratchSlot slot = _streamingScratchSlots[i];
            if (slot != null && slot.InUse)
                return true;
        }

        return false;
    }

    void DisposeStreamingScratchSlots()
    {
        using (EnterStreamingScratchGate())
            DisposeStreamingScratchSlots_NoLock();
    }

    void DisposeStreamingScratchSlots_NoLock()
    {
        if (_streamingScratchSlots == null)
            return;

        for (int i = 0; i < _streamingScratchSlots.Length; i++)
            _streamingScratchSlots[i]?.Dispose(_streamingScratchVault);

        _streamingScratchSlots = null;
    }

    void TryFinalizeStreamingScratchTeardown()
    {
        using (EnterStreamingScratchGate())
            TryFinalizeStreamingScratchTeardown_NoLock();
    }

    void TryFinalizeStreamingScratchTeardown_NoLock()
    {
        if (!_teardownStreamingScratchRequested || _streamingScratchSlots == null)
            return;

        for (int i = 0; i < _streamingScratchSlots.Length; i++)
        {
            if (_streamingScratchSlots[i] != null && _streamingScratchSlots[i].InUse)
                return;
        }

        DisposeStreamingScratchSlots_NoLock();

        _streamingScratchVault = _pendingStreamingScratchVault;
        _pendingStreamingScratchVault = null;
        _teardownStreamingScratchRequested = false;
    }

    static BufferID ResolveStreamingScratchBufferId(int slotIndex, int lane)
    {
        return (BufferID)(StreamingScratchVaultBufferBase + slotIndex * StreamingScratchVaultBufferStride + lane);
    }

    static bool IsStreamingScratchBufferAddressSafe(int slotIndex, int lane)
    {
        return (uint)slotIndex < (uint)StreamingScratchSlotMax &&
               (uint)lane < (uint)StreamingScratchVaultBufferStride;
    }

    bool TryEnsureStreamingScratchArray<T>(
        VoxelStreamingScratchSlot slot,
        int slotIndex,
        int lane,
        int requiredLength,
        ref VaultGenerationHandle<T> handle,
        ref int capacity,
        bool clearFirst = false)
        where T : struct
    {
        if (slot == null || !IsStreamingScratchBufferAddressSafe(slotIndex, lane))
            return false;

        IDataVault vault = _streamingScratchVault;
        if (vault == null || vault.IsCompactionFenceActive)
            return false;

        if (slot.Vault != null && !ReferenceEquals(slot.Vault, vault))
        {
            if (slot.InUse)
                return false;

            slot.Dispose(slot.Vault);
        }

        slot.Vault = vault;

        int safeLength = math.max(1, requiredLength);
        BufferID bufferId = ResolveStreamingScratchBufferId(slotIndex, lane);
        if (handle.BufferID != (uint)bufferId ||
            handle.SystemID != (uint)SystemID.WorldStreaming ||
            capacity < safeLength ||
            !vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) ||
            existing.Length < safeLength)
        {
            if (vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                safeLength,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            capacity = safeLength;
        }

        if (vault.IsCompactionFenceActive || handle.BufferID != (uint)bufferId || capacity < safeLength)
            return false;

        if (clearFirst)
            return TryClearStreamingScratchFirstValue(vault, in handle);

        return true;
    }

    bool TryClearStreamingScratchFirstValue<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
        where T : struct
    {
        if (vault == null ||
            vault.IsCompactionFenceActive ||
            !vault.TryAcquireWriteLock(in handle, SystemID.WorldStreaming, out NativeArray<T> values))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            if (vault.IsCompactionFenceActive || !values.IsCreated || values.Length <= 0)
                return false;

            values[0] = default;
            return true;
        }
        finally
        {
            vault.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
        }
    }

    bool TryResolveStreamingScratchArray<T>(
        IDataVault vault,
        in VaultGenerationHandle<T> handle,
        int requiredLength,
        out NativeArray<T> buffer)
        where T : struct
    {
        buffer = default;
        return vault != null &&
               !vault.IsCompactionFenceActive &&
               handle.BufferID != 0u &&
               vault.TryResolveHandle(in handle, out buffer) &&
               !vault.IsCompactionFenceActive &&
               buffer.IsCreated &&
               buffer.Length >= math.max(1, requiredLength);
    }

    static bool TryAppendStreamingScratchBufferLockId(
        uint bufferId,
        ref FixedList512Bytes<BufferID> bufferIds)
    {
        if (bufferId == 0u)
            return true;

        if (bufferIds.Length >= bufferIds.Capacity)
            return false;

        bufferIds.Add((BufferID)bufferId);
        return true;
    }

    static bool TryCollectStreamingScratchBufferLockIds(
        VoxelStreamingScratchSlot slot,
        ref FixedList512Bytes<BufferID> bufferIds)
    {
        if (slot == null)
            return false;

        return TryAppendStreamingScratchBufferLockId(slot.TerrainHeightsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.GridBiomeHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.DensityFieldHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SmoothDensityFieldHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.OverhangDensityFieldHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.QuantizedDensityFieldHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.AnomalyFeatureRecordsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.AnomalyFissureMaskHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SelectedPillarFeatureHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ChunkContentFlagsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.DensityFaultFlagsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.CellVertexCountsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.CellVertexOffsetsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshRawVerticesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshWeldedPositionsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshTriangleIndicesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshEdgeVertexXHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshEdgeVertexYHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshEdgeVertexZHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshWeldedCounterHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshSurfaceVerticesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshNormalsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshCurvatureValuesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshAmbientOcclusionValuesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshBiomeValuesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshSkirtAlphaValuesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshDirtyBlendValuesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.MeshColorsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ProjectedLocalPositionsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpatialBucketCountsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpatialBucketWriteHeadsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpatialNodeBucketOffsetsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpatialNodeBucketIndicesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpatialTunnelBucketOffsetsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpatialTunnelBucketIndicesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.RebuildNodesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.RebuildTunnelsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.RebuildEntrancesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.RebuildStructuresHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.RebuildCraterStampsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpawnPointListScratchHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.SpawnPointCountHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ModifiedCellsScratchHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ModifiedCellCountHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ModifiedCellBucketHeadsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ModifiedCellNextHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.JobLifetimeFenceHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderTriangleBucketsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderBucketCountsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderBucketOffsetsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderBucketWriteHeadsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderChunkTriangleIndicesHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderLocalRemapHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderTouchedVertexGlobalsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderLocalPositionsHandle.BufferID, ref bufferIds) &&
               TryAppendStreamingScratchBufferLockId(slot.ColliderLocalIndicesHandle.BufferID, ref bufferIds);
    }

    bool TryLockStreamingScratchJobLifetime(ref VoxelStreamingScratchLease lease)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0 || lease._scratchBuffersLocked != 0)
            return false;

        IDataVault vault = null;
        FixedList512Bytes<BufferID> bufferIds = default;
        using (EnterStreamingScratchGate())
        {
            VoxelStreamingScratchSlot slot = _streamingScratchSlots != null &&
                                             lease._slotIndex < _streamingScratchSlots.Length
                ? _streamingScratchSlots[lease._slotIndex]
                : null;
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                slot == null ||
                !slot.InUse ||
                !TryCollectStreamingScratchBufferLockIds(slot, ref bufferIds))
            {
                return false;
            }

            vault = slot.Vault;
        }

        if (vault == null || vault.IsCompactionFenceActive)
            return false;

        if (bufferIds.Length <= 0 || vault.IsCompactionFenceActive)
            return false;

        ulong mutationGuardMask = 0UL;
        for (int i = 0; i < bufferIds.Length; i++)
            mutationGuardMask |= StreamingScratchMutationGuardBit(bufferIds[i]);

        if (mutationGuardMask == 0UL || vault.IsCompactionFenceActive)
            return false;

        bool mutationGuardAcquired = false;
        try
        {
            if (!vault.TryAcquireMutationGuard(mutationGuardMask))
                return false;

            mutationGuardAcquired = true;
            if (vault.IsCompactionFenceActive)
                return false;

            lease._lockedScratchMutationGuardMask = mutationGuardMask;
            lease._lockedScratchVault = vault;
            lease._scratchBuffersLocked = 1;
            mutationGuardAcquired = false;
            return true;
        }
        finally
        {
            if (mutationGuardAcquired)
                vault.ReleaseMutationGuard(mutationGuardMask);
        }
    }

    void UnlockStreamingScratchJobLifetime(ref VoxelStreamingScratchLease lease)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0 || lease._scratchBuffersLocked == 0)
            return;

        IDataVault vault = lease._lockedScratchVault != null ? lease._lockedScratchVault : _streamingScratchVault;
        if (vault == null)
        {
            lease._lockedScratchMutationGuardMask = 0UL;
            lease._lockedScratchVault = null;
            lease._scratchBuffersLocked = 0;
            return;
        }

        ReleaseStreamingScratchMutationGuard(vault, lease._lockedScratchMutationGuardMask);
        lease._lockedScratchMutationGuardMask = 0UL;
        lease._lockedScratchVault = null;
        lease._scratchBuffersLocked = 0;
    }

    static void ReleaseStreamingScratchMutationGuard(
        IDataVault vault,
        ulong mutationGuardMask)
    {
        if (vault == null || mutationGuardMask == 0UL)
            return;

        vault.ReleaseMutationGuard(mutationGuardMask);
    }

    static ulong StreamingScratchMutationGuardBit(BufferID bufferId)
    {
        return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
    }

    static void ReleaseStreamingScratchHandle<T>(
        IDataVault vault,
        ref VaultGenerationHandle<T> handle,
        ref int capacity)
        where T : struct
    {
        if (vault != null && handle.BufferID != 0u)
            vault.ReleaseBuffer(in handle);

        handle = default;
        capacity = 0;
    }

    bool EnsureStreamingScratchSlotCapacity(
        VoxelStreamingScratchSlot slot,
        int slotIndex,
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension)
    {
        int meshRawScratchCapacity = ResolveStreamingMeshRawScratchCapacity(totalCellCount);
        ResolveStreamingEdgeVertexScratchCapacity(
            gridDimension,
            out int edgeVertexCountX,
            out int edgeVertexCountY,
            out int edgeVertexCountZ);

        return TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneTerrainHeights, heightCount, ref slot.TerrainHeightsHandle, ref slot.TerrainHeightsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneGridBiome, heightCount, ref slot.GridBiomeHandle, ref slot.GridBiomeCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneDensityField, totalPointCount, ref slot.DensityFieldHandle, ref slot.DensityFieldCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSmoothDensityField, totalPointCount, ref slot.SmoothDensityFieldHandle, ref slot.SmoothDensityFieldCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneOverhangDensityField, totalPointCount, ref slot.OverhangDensityFieldHandle, ref slot.OverhangDensityFieldCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneQuantizedDensityField, totalPointCount, ref slot.QuantizedDensityFieldHandle, ref slot.QuantizedDensityFieldCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneAnomalyFeatureRecords, heightCount, ref slot.AnomalyFeatureRecordsHandle, ref slot.AnomalyFeatureRecordsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneAnomalyFissureMask, heightCount, ref slot.AnomalyFissureMaskHandle, ref slot.AnomalyFissureMaskCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSelectedPillarFeature, 1, ref slot.SelectedPillarFeatureHandle, ref slot.SelectedPillarFeatureCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneChunkContentFlags, 1, ref slot.ChunkContentFlagsHandle, ref slot.ChunkContentFlagsCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneDensityFaultFlags, VoxelDensityPipelineFaultSlots.SlotCount, ref slot.DensityFaultFlagsHandle, ref slot.DensityFaultFlagsCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneCellVertexCounts, totalCellCount, ref slot.CellVertexCountsHandle, ref slot.CellVertexCountsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneCellVertexOffsets, totalCellCount, ref slot.CellVertexOffsetsHandle, ref slot.CellVertexOffsetsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshRawVertices, meshRawScratchCapacity, ref slot.MeshRawVerticesHandle, ref slot.MeshRawVerticesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshWeldedPositions, meshRawScratchCapacity, ref slot.MeshWeldedPositionsHandle, ref slot.MeshWeldedPositionsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshTriangleIndices, meshRawScratchCapacity, ref slot.MeshTriangleIndicesHandle, ref slot.MeshTriangleIndicesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshEdgeVertexX, edgeVertexCountX, ref slot.MeshEdgeVertexXHandle, ref slot.MeshEdgeVertexXCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshEdgeVertexY, edgeVertexCountY, ref slot.MeshEdgeVertexYHandle, ref slot.MeshEdgeVertexYCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshEdgeVertexZ, edgeVertexCountZ, ref slot.MeshEdgeVertexZHandle, ref slot.MeshEdgeVertexZCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshWeldedCounter, 1, ref slot.MeshWeldedCounterHandle, ref slot.MeshWeldedCounterCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshSurfaceVertices, meshRawScratchCapacity, ref slot.MeshSurfaceVerticesHandle, ref slot.MeshSurfaceVerticesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshNormals, meshRawScratchCapacity, ref slot.MeshNormalsHandle, ref slot.MeshNormalsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshCurvatureValues, meshRawScratchCapacity, ref slot.MeshCurvatureValuesHandle, ref slot.MeshCurvatureValuesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshAmbientOcclusionValues, meshRawScratchCapacity, ref slot.MeshAmbientOcclusionValuesHandle, ref slot.MeshAmbientOcclusionValuesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshBiomeValues, meshRawScratchCapacity, ref slot.MeshBiomeValuesHandle, ref slot.MeshBiomeValuesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshSkirtAlphaValues, meshRawScratchCapacity, ref slot.MeshSkirtAlphaValuesHandle, ref slot.MeshSkirtAlphaValuesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshDirtyBlendValues, meshRawScratchCapacity, ref slot.MeshDirtyBlendValuesHandle, ref slot.MeshDirtyBlendValuesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneMeshColors, meshRawScratchCapacity, ref slot.MeshColorsHandle, ref slot.MeshColorsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneProjectedLocalPositions, meshRawScratchCapacity, ref slot.ProjectedLocalPositionsHandle, ref slot.ProjectedLocalPositionsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpatialBucketCounts, StreamingSpatialBucketScratchCapacity, ref slot.SpatialBucketCountsHandle, ref slot.SpatialBucketCountsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpatialBucketWriteHeads, StreamingSpatialBucketScratchCapacity, ref slot.SpatialBucketWriteHeadsHandle, ref slot.SpatialBucketWriteHeadsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpatialNodeBucketOffsets, StreamingSpatialBucketScratchCapacity + 1, ref slot.SpatialNodeBucketOffsetsHandle, ref slot.SpatialNodeBucketOffsetsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpatialNodeBucketIndices, StreamingNodeSpatialReferenceScratchCapacity, ref slot.SpatialNodeBucketIndicesHandle, ref slot.SpatialNodeBucketIndicesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpatialTunnelBucketOffsets, StreamingSpatialBucketScratchCapacity + 1, ref slot.SpatialTunnelBucketOffsetsHandle, ref slot.SpatialTunnelBucketOffsetsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpatialTunnelBucketIndices, StreamingTunnelSpatialReferenceScratchCapacity, ref slot.SpatialTunnelBucketIndicesHandle, ref slot.SpatialTunnelBucketIndicesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneRebuildNodes, StreamingCaveGraphNodeScratchCapacity, ref slot.RebuildNodesHandle, ref slot.RebuildNodesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneRebuildTunnels, StreamingCaveGraphTunnelScratchCapacity, ref slot.RebuildTunnelsHandle, ref slot.RebuildTunnelsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneRebuildEntrances, StreamingCaveGraphEntranceScratchCapacity, ref slot.RebuildEntrancesHandle, ref slot.RebuildEntrancesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneRebuildStructures, StreamingCaveGraphStructureScratchCapacity, ref slot.RebuildStructuresHandle, ref slot.RebuildStructuresCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneRebuildCraterStamps, StreamingCraterStampScratchCapacity, ref slot.RebuildCraterStampsHandle, ref slot.RebuildCraterStampsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpawnPointList, ResolveStreamingSpawnPointScratchCapacity(totalCellCount), ref slot.SpawnPointListScratchHandle, ref slot.SpawnPointListScratchCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneSpawnPointCount, 1, ref slot.SpawnPointCountHandle, ref slot.SpawnPointCountCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneModifiedCells, math.max(1, totalCellCount), ref slot.ModifiedCellsScratchHandle, ref slot.ModifiedCellsScratchCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneModifiedCellCount, 1, ref slot.ModifiedCellCountHandle, ref slot.ModifiedCellCountCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneModifiedCellBucketHeads, ResolveModifiedCellBucketCount(math.max(1, totalCellCount)), ref slot.ModifiedCellBucketHeadsHandle, ref slot.ModifiedCellBucketHeadsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneModifiedCellNext, math.max(1, totalCellCount), ref slot.ModifiedCellNextHandle, ref slot.ModifiedCellNextCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneJobLifetimeFence, 1, ref slot.JobLifetimeFenceHandle, ref slot.JobLifetimeFenceCapacity, true) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderTriangleBuckets, math.max(1, meshRawScratchCapacity / 3), ref slot.ColliderTriangleBucketsHandle, ref slot.ColliderTriangleBucketsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderBucketCounts, StreamingColliderChunkScratchCapacity, ref slot.ColliderBucketCountsHandle, ref slot.ColliderBucketCountsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderBucketOffsets, StreamingColliderChunkScratchCapacity, ref slot.ColliderBucketOffsetsHandle, ref slot.ColliderBucketOffsetsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderBucketWriteHeads, StreamingColliderChunkScratchCapacity, ref slot.ColliderBucketWriteHeadsHandle, ref slot.ColliderBucketWriteHeadsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderChunkTriangleIndices, meshRawScratchCapacity, ref slot.ColliderChunkTriangleIndicesHandle, ref slot.ColliderChunkTriangleIndicesCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderLocalRemap, meshRawScratchCapacity, ref slot.ColliderLocalRemapHandle, ref slot.ColliderLocalRemapCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderTouchedVertexGlobals, meshRawScratchCapacity, ref slot.ColliderTouchedVertexGlobalsHandle, ref slot.ColliderTouchedVertexGlobalsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderLocalPositions, meshRawScratchCapacity, ref slot.ColliderLocalPositionsHandle, ref slot.ColliderLocalPositionsCapacity) &&
               TryEnsureStreamingScratchArray(slot, slotIndex, ScratchLaneColliderLocalIndices, meshRawScratchCapacity, ref slot.ColliderLocalIndicesHandle, ref slot.ColliderLocalIndicesCapacity);
    }

    bool TryEnsureStreamingScratchSlotCapacity(
        VoxelStreamingScratchSlot slot,
        int slotIndex,
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension)
    {
        if (!IsStreamingScratchCapacityRequestSafe(
                heightCount,
                totalPointCount,
                totalCellCount,
                gridDimension))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        return EnsureStreamingScratchSlotCapacity(slot, slotIndex, heightCount, totalPointCount, totalCellCount, gridDimension);
    }

    static bool IsStreamingScratchCapacityRequestSafe(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension)
    {
        if (heightCount <= 0 ||
            totalPointCount <= 0 ||
            totalCellCount <= 0 ||
            gridDimension < StreamingScratchGridMin ||
            gridDimension > StreamingScratchGridMax ||
            heightCount > StreamingHeightScratchMax ||
            totalPointCount > StreamingPointScratchMax ||
            totalCellCount > StreamingCellScratchMax)
        {
            return false;
        }

        int meshRawScratchCapacity = ResolveStreamingMeshRawScratchCapacity(totalCellCount);
        ResolveStreamingEdgeVertexScratchCapacity(
            gridDimension,
            out int edgeVertexCountX,
            out int edgeVertexCountY,
            out int edgeVertexCountZ);

        return meshRawScratchCapacity > 0 &&
               meshRawScratchCapacity <= StreamingMeshRawVertexScratchVisualOverkillCapacity &&
               edgeVertexCountX > 0 &&
               edgeVertexCountX <= StreamingEdgeVertexScratchMax &&
               edgeVertexCountY > 0 &&
               edgeVertexCountY <= StreamingEdgeVertexScratchMax &&
               edgeVertexCountZ > 0 &&
               edgeVertexCountZ <= StreamingEdgeVertexScratchMax &&
               ResolveStreamingSpawnPointScratchCapacity(totalCellCount) <= StreamingSpawnPointScratchMax;
    }

    static int ResolveStreamingMeshRawScratchCapacity(int totalCellCount)
    {
        long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
        int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
        long capacity = math.max(desired, (long)qualityCapacity);
        capacity = math.min(capacity, (long)StreamingMeshRawVertexScratchVisualOverkillCapacity);
        return capacity < 1L ? 1 : (int)capacity;
    }

    static int ResolveStreamingMeshRawScratchQualityCapacity()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        return math.clamp(
            (int)math.round(math.lerp(
                StreamingMeshRawVertexScratchLowTierCapacity,
                StreamingMeshRawVertexScratchVisualOverkillCapacity,
                smooth)),
            StreamingMeshRawVertexScratchLowTierCapacity,
            StreamingMeshRawVertexScratchVisualOverkillCapacity);
    }

    static void ResolveStreamingEdgeVertexScratchCapacity(
        int gridDimension,
        out int edgeVertexCountX,
        out int edgeVertexCountY,
        out int edgeVertexCountZ)
    {
        int grid = math.clamp(gridDimension, 16, 128);
        int points = grid + 1;
        edgeVertexCountX = math.max(1, grid * points * points);
        edgeVertexCountY = math.max(1, points * grid * points);
        edgeVertexCountZ = math.max(1, points * points * grid);
    }

    bool TryEnsureMeshExtractionScratchCapacity(
        ref VoxelStreamingScratchLease lease,
        int rawCount,
        int edgeVertexCountX,
        int edgeVertexCountY,
        int edgeVertexCountZ)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeRawCount = math.max(1, rawCount);
        int safeEdgeVertexCountX = math.max(1, edgeVertexCountX);
        int safeEdgeVertexCountY = math.max(1, edgeVertexCountY);
        int safeEdgeVertexCountZ = math.max(1, edgeVertexCountZ);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<MCRawVertex> rawVertices = lease.MeshRawVertices;
            NativeArray<float3> weldedPositions = lease.MeshWeldedPositions;
            NativeArray<int> triangleIndices = lease.MeshTriangleIndices;
            NativeArray<int> edgeVertexX = lease.MeshEdgeVertexX;
            NativeArray<int> edgeVertexY = lease.MeshEdgeVertexY;
            NativeArray<int> edgeVertexZ = lease.MeshEdgeVertexZ;
            NativeArray<int> weldedCounter = lease.MeshWeldedCounter;
            if (!rawVertices.IsCreated || rawVertices.Length < safeRawCount ||
                !weldedPositions.IsCreated || weldedPositions.Length < safeRawCount ||
                !triangleIndices.IsCreated || triangleIndices.Length < safeRawCount ||
                !edgeVertexX.IsCreated || edgeVertexX.Length < safeEdgeVertexCountX ||
                !edgeVertexY.IsCreated || edgeVertexY.Length < safeEdgeVertexCountY ||
                !edgeVertexZ.IsCreated || edgeVertexZ.Length < safeEdgeVertexCountZ ||
                !weldedCounter.IsCreated || weldedCounter.Length < 1)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

        }

        return true;
    }

    bool TryEnsureMeshAttributeScratchCapacity(ref VoxelStreamingScratchLease lease, int weldedCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeWeldedCount = math.max(1, weldedCount);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<VoxelSurfaceVertex> surfaceVertices = lease.MeshSurfaceVertices;
            NativeArray<float3> normals = lease.MeshNormals;
            NativeArray<float> curvatureValues = lease.MeshCurvatureValues;
            NativeArray<float> ambientOcclusionValues = lease.MeshAmbientOcclusionValues;
            NativeArray<float> biomeValues = lease.MeshBiomeValues;
            NativeArray<float> skirtAlphaValues = lease.MeshSkirtAlphaValues;
            NativeArray<float> dirtyBlendValues = lease.MeshDirtyBlendValues;
            NativeArray<Color32> colors = lease.MeshColors;
            if (!surfaceVertices.IsCreated || surfaceVertices.Length < safeWeldedCount ||
                !normals.IsCreated || normals.Length < safeWeldedCount ||
                !curvatureValues.IsCreated || curvatureValues.Length < safeWeldedCount ||
                !ambientOcclusionValues.IsCreated || ambientOcclusionValues.Length < safeWeldedCount ||
                !biomeValues.IsCreated || biomeValues.Length < safeWeldedCount ||
                !skirtAlphaValues.IsCreated || skirtAlphaValues.Length < safeWeldedCount ||
                !dirtyBlendValues.IsCreated || dirtyBlendValues.Length < safeWeldedCount ||
                !colors.IsCreated || colors.Length < safeWeldedCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }
        }

        return true;
    }

    bool TryEnsureProjectionScratchCapacity(ref VoxelStreamingScratchLease lease, int vertexCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeVertexCount = math.max(1, vertexCount);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<float3> projectedLocalPositions = lease.ProjectedLocalPositions;
            if (!projectedLocalPositions.IsCreated || projectedLocalPositions.Length < safeVertexCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }
        }

        return true;
    }

    bool TryEnsureSpatialBucketCounterScratchCapacity(ref VoxelStreamingScratchLease lease, int bucketCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeBucketCount = math.max(1, bucketCount);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<int> bucketCounts = lease.SpatialBucketCounts;
            NativeArray<int> writeHeads = lease.SpatialBucketWriteHeads;
            if (!bucketCounts.IsCreated || bucketCounts.Length < safeBucketCount ||
                !writeHeads.IsCreated || writeHeads.Length < safeBucketCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }
        }

        return true;
    }

    bool TryEnsureNodeSpatialBucketScratchCapacity(ref VoxelStreamingScratchLease lease, int bucketCount, int totalReferences)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeOffsetCount = math.max(1, bucketCount + 1);
        int safeReferenceCount = math.max(1, totalReferences);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<int> nodeBucketOffsets = lease.SpatialNodeBucketOffsets;
            NativeArray<int> nodeBucketIndices = lease.SpatialNodeBucketIndices;
            if (!nodeBucketOffsets.IsCreated || nodeBucketOffsets.Length < safeOffsetCount ||
                !nodeBucketIndices.IsCreated || nodeBucketIndices.Length < safeReferenceCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }
        }

        return true;
    }

    bool TryEnsureTunnelSpatialBucketScratchCapacity(ref VoxelStreamingScratchLease lease, int bucketCount, int totalReferences)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeOffsetCount = math.max(1, bucketCount + 1);
        int safeReferenceCount = math.max(1, totalReferences);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<int> tunnelBucketOffsets = lease.SpatialTunnelBucketOffsets;
            NativeArray<int> tunnelBucketIndices = lease.SpatialTunnelBucketIndices;
            if (!tunnelBucketOffsets.IsCreated || tunnelBucketOffsets.Length < safeOffsetCount ||
                !tunnelBucketIndices.IsCreated || tunnelBucketIndices.Length < safeReferenceCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }
        }

        return true;
    }

    static void CopyNativeArrayToScratch<T>(NativeArray<T> source, NativeArray<T> destination, int requestedCount)
        where T : struct
    {
        if (!source.IsCreated || !destination.IsCreated || requestedCount <= 0)
            return;

        int count = math.min(requestedCount, math.min(source.Length, destination.Length));
        for (int i = 0; i < count; i++)
            destination[i] = source[i];
    }

    static void CopyInlineCaveGraphToScratch(
        VoxelInlineCaveGraphData caveGraphData,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures)
    {
        int entranceCount = math.min(caveGraphData.SafeEntranceCount, entrances.IsCreated ? entrances.Length : 0);
        if (entranceCount > 0)
            entrances[0] = caveGraphData.Entrance0;

        int structureCount = math.min(caveGraphData.SafeStructureCount, structures.IsCreated ? structures.Length : 0);
        for (int i = 0; i < structureCount; i++)
            structures[i] = caveGraphData.GetStructure(i);
    }

    bool TryPrepareRebuildGraphScratch(
        ref VoxelStreamingScratchLease lease,
        int nodeCount,
        int tunnelCount,
        int entranceCount,
        int structureCount,
        int craterStampCount,
        out NativeArray<CaveNode> nodes,
        out NativeArray<CaveTunnel> tunnels,
        out NativeArray<CaveEntrance> entrances,
        out NativeArray<CaveStructure> structures,
        out NativeArray<VoxelCraterStamp> craterStamps)
    {
        nodes = default;
        tunnels = default;
        entrances = default;
        structures = default;
        craterStamps = default;
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        if (nodeCount > StreamingCaveGraphNodeScratchCapacity ||
            tunnelCount > StreamingCaveGraphTunnelScratchCapacity ||
            entranceCount > StreamingCaveGraphEntranceScratchCapacity ||
            structureCount > StreamingCaveGraphStructureScratchCapacity ||
            craterStampCount > StreamingCraterStampScratchCapacity)
        {
            return false;
        }

        int safeNodeCount = math.max(1, nodeCount);
        int safeTunnelCount = math.max(1, tunnelCount);
        int safeEntranceCount = math.max(1, entranceCount);
        int safeStructureCount = math.max(1, structureCount);
        int safeCraterStampCount = math.max(1, craterStampCount);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<CaveNode> rebuildNodes = lease.RebuildNodes;
            NativeArray<CaveTunnel> rebuildTunnels = lease.RebuildTunnels;
            NativeArray<CaveEntrance> rebuildEntrances = lease.RebuildEntrances;
            NativeArray<CaveStructure> rebuildStructures = lease.RebuildStructures;
            NativeArray<VoxelCraterStamp> rebuildCraterStamps = lease.RebuildCraterStamps;
            if (!rebuildNodes.IsCreated || rebuildNodes.Length < safeNodeCount ||
                !rebuildTunnels.IsCreated || rebuildTunnels.Length < safeTunnelCount ||
                !rebuildEntrances.IsCreated || rebuildEntrances.Length < safeEntranceCount ||
                !rebuildStructures.IsCreated || rebuildStructures.Length < safeStructureCount ||
                !rebuildCraterStamps.IsCreated || rebuildCraterStamps.Length < safeCraterStampCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            nodes = rebuildNodes.GetSubArray(0, nodeCount);
            tunnels = rebuildTunnels.GetSubArray(0, tunnelCount);
            entrances = rebuildEntrances.GetSubArray(0, entranceCount);
            structures = rebuildStructures.GetSubArray(0, structureCount);
            craterStamps = rebuildCraterStamps.GetSubArray(0, craterStampCount);
        }

        return true;
    }

    bool TryPrepareSpawnPointScratch(
        ref VoxelStreamingScratchLease lease,
        int requiredCapacity)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeCapacity = math.max(1, requiredCapacity);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!EnsureSpawnPointScratchCapacity(slot, lease._slotIndex, safeCapacity))
                return false;

            NativeArray<CaveSpawnData> spawnPoints = lease.SpawnPointListScratch;
            NativeArray<int> spawnPointCount = lease.SpawnPointCountScratch;
            if (!spawnPoints.IsCreated || spawnPoints.Length < safeCapacity ||
                !spawnPointCount.IsCreated || spawnPointCount.Length < 1)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

        }

        return true;
    }

    bool TryPrepareModifiedCellsScratch(
        ref VoxelStreamingScratchLease lease,
        int requiredCapacity,
        int requiredBucketCount,
        out NativeArray<VoxelModifiedCellEntry> modifiedCells,
        out NativeArray<int> modifiedCellCount,
        out NativeArray<int> modifiedCellBucketHeads,
        out NativeArray<int> modifiedCellNext)
    {
        modifiedCells = default;
        modifiedCellCount = default;
        modifiedCellBucketHeads = default;
        modifiedCellNext = default;
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeCapacity = math.max(1, requiredCapacity);
        int safeBucketCount = math.max(MinimumModifiedCellBucketScratchCapacity, requiredBucketCount);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!EnsureModifiedCellsScratchCapacity(slot, lease._slotIndex, safeCapacity))
                return false;

            modifiedCells = lease.ModifiedCellsScratch;
            modifiedCellCount = lease.ModifiedCellCountScratch;
            modifiedCellBucketHeads = lease.ModifiedCellBucketHeadsScratch;
            modifiedCellNext = lease.ModifiedCellNextScratch;
            if (!modifiedCells.IsCreated || modifiedCells.Length < safeCapacity ||
                !modifiedCellCount.IsCreated || modifiedCellCount.Length < 1 ||
                !modifiedCellBucketHeads.IsCreated || modifiedCellBucketHeads.Length < safeBucketCount ||
                !modifiedCellNext.IsCreated || modifiedCellNext.Length < safeCapacity)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            modifiedCells = modifiedCells.GetSubArray(0, safeCapacity);
            modifiedCellBucketHeads = modifiedCellBucketHeads.GetSubArray(0, safeBucketCount);
            modifiedCellNext = modifiedCellNext.GetSubArray(0, safeCapacity);
        }

        return true;
    }

    static int ResolveModifiedCellBucketCount(int requiredCapacity)
    {
        int target = math.max(MinimumModifiedCellBucketScratchCapacity, requiredCapacity);
        target = math.min(MaximumModifiedCellBucketScratchCapacity, target > (int.MaxValue >> 1) ? MaximumModifiedCellBucketScratchCapacity : target << 1);
        int bucketCount = MinimumModifiedCellBucketScratchCapacity;
        while (bucketCount < target && bucketCount < MaximumModifiedCellBucketScratchCapacity)
            bucketCount <<= 1;

        return math.clamp(bucketCount, MinimumModifiedCellBucketScratchCapacity, MaximumModifiedCellBucketScratchCapacity);
    }

    static int ResolveStreamingSpawnPointScratchCapacity(int totalCellCount)
    {
        return math.max(MinimumStreamingSpawnPointScratchCapacity, math.max(1, totalCellCount) / 10);
    }

    bool EnsureSpawnPointScratchCapacity(VoxelStreamingScratchSlot slot, int slotIndex, int requiredCapacity)
    {
        int safeCapacity = math.max(MinimumStreamingSpawnPointScratchCapacity, requiredCapacity);
        return TryEnsureStreamingScratchArray(
                   slot,
                   slotIndex,
                   ScratchLaneSpawnPointList,
                   safeCapacity,
                   ref slot.SpawnPointListScratchHandle,
                   ref slot.SpawnPointListScratchCapacity) &&
               TryEnsureStreamingScratchArray(
                   slot,
                   slotIndex,
                   ScratchLaneSpawnPointCount,
                   1,
                   ref slot.SpawnPointCountHandle,
                   ref slot.SpawnPointCountCapacity,
                   true);
    }

    bool EnsureModifiedCellsScratchCapacity(VoxelStreamingScratchSlot slot, int slotIndex, int requiredCapacity)
    {
        int safeCapacity = math.max(1, requiredCapacity);
        int safeBucketCount = ResolveModifiedCellBucketCount(safeCapacity);
        return TryEnsureStreamingScratchArray(
                   slot,
                   slotIndex,
                   ScratchLaneModifiedCells,
                   safeCapacity,
                   ref slot.ModifiedCellsScratchHandle,
                   ref slot.ModifiedCellsScratchCapacity) &&
               TryEnsureStreamingScratchArray(
                   slot,
                   slotIndex,
                   ScratchLaneModifiedCellCount,
                   1,
                   ref slot.ModifiedCellCountHandle,
                   ref slot.ModifiedCellCountCapacity,
                   true) &&
               TryEnsureStreamingScratchArray(
                   slot,
                   slotIndex,
                   ScratchLaneModifiedCellBucketHeads,
                   safeBucketCount,
                   ref slot.ModifiedCellBucketHeadsHandle,
                   ref slot.ModifiedCellBucketHeadsCapacity) &&
               TryEnsureStreamingScratchArray(
                   slot,
                   slotIndex,
                   ScratchLaneModifiedCellNext,
                   safeCapacity,
                   ref slot.ModifiedCellNextHandle,
                   ref slot.ModifiedCellNextCapacity);
    }

    bool TryEnsureColliderChunkScratchCapacity(
        ref VoxelStreamingScratchLease lease,
        int triangleCount,
        int triangleIndexCount,
        int vertexCount,
        int colliderChunkCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeTriangleCount = math.max(1, triangleCount);
        int safeTriangleIndexCount = math.max(1, triangleIndexCount);
        int safeVertexCount = math.max(1, vertexCount);
        int safeColliderChunkCount = math.max(1, colliderChunkCount);

        using (EnterStreamingScratchGate())
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            NativeArray<byte> triangleBuckets = lease.ColliderTriangleBuckets;
            NativeArray<int> bucketCounts = lease.ColliderBucketCounts;
            NativeArray<int> bucketOffsets = lease.ColliderBucketOffsets;
            NativeArray<int> bucketWriteHeads = lease.ColliderBucketWriteHeads;
            NativeArray<int> chunkTriangleIndices = lease.ColliderChunkTriangleIndices;
            NativeArray<int> localRemap = lease.ColliderLocalRemap;
            NativeArray<int> touchedVertexGlobals = lease.ColliderTouchedVertexGlobals;
            NativeArray<float3> localPositions = lease.ColliderLocalPositions;
            NativeArray<int> localIndices = lease.ColliderLocalIndices;
            if (!triangleBuckets.IsCreated || triangleBuckets.Length < safeTriangleCount ||
                !bucketCounts.IsCreated || bucketCounts.Length < safeColliderChunkCount ||
                !bucketOffsets.IsCreated || bucketOffsets.Length < safeColliderChunkCount ||
                !bucketWriteHeads.IsCreated || bucketWriteHeads.Length < safeColliderChunkCount ||
                !chunkTriangleIndices.IsCreated || chunkTriangleIndices.Length < safeTriangleIndexCount ||
                !localRemap.IsCreated || localRemap.Length < safeVertexCount ||
                !touchedVertexGlobals.IsCreated || touchedVertexGlobals.Length < safeVertexCount ||
                !localPositions.IsCreated || localPositions.Length < safeVertexCount ||
                !localIndices.IsCreated || localIndices.Length < safeTriangleIndexCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }
        }

        return true;
    }

    static float ResolveDensityDecodeScale(float voxelStep)
    {
        return math.max(voxelStep * 0.125f, 0.005f);
    }

    static unsafe void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
    {
        if (!array.IsCreated)
            return;

        void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
        System.Exception nativeSentinelCleanupException0 = null;

        try
        {
            NativeMemorySentinel.UnregisterPointer(trackedPointer);
        }
        catch (System.Exception nativeSentinelException0)
        {
            nativeSentinelCleanupException0 = nativeSentinelException0;
        }

        try
        {
            array.Dispose();
        }
        catch (System.Exception nativeSentinelException0)
        {
            if (nativeSentinelCleanupException0 == null)
                nativeSentinelCleanupException0 = nativeSentinelException0;
        }
        finally
        {
            array = default;
        }

        if (nativeSentinelCleanupException0 != null)
            throw nativeSentinelCleanupException0;
    }

    bool BuildSpatialPartitions(VoxelPipelineData data)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            using (ProfilerRegistry.VoxelRebuild.Auto())
            {
                float3 volumeSize = new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep;
                int partitionDim = math.clamp(data.GridDimension / 12, 4, 8);
                data.PartitionDimX = partitionDim;
                data.PartitionDimY = partitionDim;
                data.PartitionDimZ = partitionDim;
                data.PartitionOrigin = data.VolumeOrigin;
                data.PartitionCellSize = volumeSize / new float3(partitionDim, partitionDim, partitionDim);

                if (!BuildNodeSpatialBuckets(data))
                    return false;
                if (!BuildTunnelSpatialBuckets(data))
                    return false;
            }

            return true;
        }
        finally
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d / Stopwatch.Frequency;
            RecordVoxelRebuildBudget(elapsedMilliseconds);
        }
    }

    void RecordVoxelRebuildBudget(double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= VoxelRebuildBudgetMilliseconds)
        {
            _voxelRebuildOverBudgetConsecutive = 0;
            return;
        }

        _voxelRebuildOverBudgetConsecutive++;
        if (_voxelRebuildOverBudgetConsecutive < VoxelRebuildBudgetStrikeFrames)
            return;

        _voxelRebuildOverBudgetConsecutive = 0;
        LODSystemManager lodSystem = _lodSystemManager;
        if (lodSystem != null)
            lodSystem.ApplyEmergencyLODBiasStrike();

        CrashTelemetryBuffer.ReportCriticalPerformanceSpike(
            VoxelRebuildLaneHash,
            elapsedMilliseconds,
            Hecton8.Core.SystemDispatcher.CurrentFrameId);
    }

    bool BuildNodeSpatialBuckets(VoxelPipelineData data)
    {
        int nodeCount = data.NodeCount;
        if (nodeCount <= 0)
            return true;

        int bucketCount = data.PartitionDimX * data.PartitionDimY * data.PartitionDimZ;
        if (!TryEnsureSpatialBucketCounterScratchCapacity(ref data.ScratchLease, bucketCount))
            return false;
        if (!TryEnsureNodeSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, 1))
            return false;

        data.UsesStreamingScratchSpatialBuckets = true;
        int totalReferences = 0;
        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            NativeArray<CaveNode> nodes = data.Nodes;
            NativeArray<int> bucketCounts = data.ScratchLease.SpatialBucketCounts;
            NativeArray<int> writeHeads = data.ScratchLease.SpatialBucketWriteHeads;
            NativeArray<int> nodeBucketOffsets = data.NodeBucketOffsets;
            if (!nodes.IsCreated || nodes.Length < nodeCount ||
                !bucketCounts.IsCreated || bucketCounts.Length < bucketCount ||
                !writeHeads.IsCreated || writeHeads.Length < bucketCount ||
                !nodeBucketOffsets.IsCreated || nodeBucketOffsets.Length < bucketCount + 1)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                bucketCounts[bucketIndex] = 0;
                writeHeads[bucketIndex] = 0;
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                CaveNode node = nodes[nodeIndex];
                if (!TryResolveNodePartitionBounds(in node, data, out float3 boundsMin, out float3 boundsMax))
                    continue;

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                    bucketCounts[FlattenPartitionIndex(data, x, y, z)]++;
            }

            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                nodeBucketOffsets[bucketIndex] = totalReferences;
                totalReferences += bucketCounts[bucketIndex];
            }

            nodeBucketOffsets[bucketCount] = totalReferences;
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }

        if (totalReferences <= 0)
            return true;

        if (!TryEnsureNodeSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, totalReferences))
            return false;

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            NativeArray<CaveNode> nodes = data.Nodes;
            NativeArray<int> writeHeads = data.ScratchLease.SpatialBucketWriteHeads;
            NativeArray<int> nodeBucketOffsets = data.NodeBucketOffsets;
            NativeArray<int> nodeBucketIndices = data.NodeBucketIndices;
            if (!nodes.IsCreated || nodes.Length < nodeCount ||
                !writeHeads.IsCreated || writeHeads.Length < bucketCount ||
                !nodeBucketOffsets.IsCreated || nodeBucketOffsets.Length < bucketCount + 1 ||
                !nodeBucketIndices.IsCreated || nodeBucketIndices.Length < totalReferences)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
                writeHeads[bucketIndex] = nodeBucketOffsets[bucketIndex];

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                CaveNode node = nodes[nodeIndex];
                if (!TryResolveNodePartitionBounds(in node, data, out float3 boundsMin, out float3 boundsMax))
                    continue;

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int bucketIndex = FlattenPartitionIndex(data, x, y, z);
                    int writeIndex = writeHeads[bucketIndex];
                    if (writeIndex < nodeBucketOffsets[bucketIndex] ||
                        writeIndex >= nodeBucketOffsets[bucketIndex + 1] ||
                        writeIndex >= nodeBucketIndices.Length)
                    {
                        ReportVoxelMeshScratchCapacityOverflow();
                        return false;
                    }

                    nodeBucketIndices[writeIndex] = nodeIndex;
                    writeHeads[bucketIndex] = writeIndex + 1;
                }
            }
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }

        return true;
    }

    bool BuildTunnelSpatialBuckets(VoxelPipelineData data)
    {
        int tunnelCount = data.TunnelCount;
        if (tunnelCount <= 0)
            return true;

        int bucketCount = data.PartitionDimX * data.PartitionDimY * data.PartitionDimZ;
        if (!TryEnsureSpatialBucketCounterScratchCapacity(ref data.ScratchLease, bucketCount))
            return false;
        if (!TryEnsureTunnelSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, 1))
            return false;

        data.UsesStreamingScratchSpatialBuckets = true;
        int totalReferences = 0;
        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            NativeArray<CaveTunnel> tunnels = data.Tunnels;
            NativeArray<int> bucketCounts = data.ScratchLease.SpatialBucketCounts;
            NativeArray<int> writeHeads = data.ScratchLease.SpatialBucketWriteHeads;
            NativeArray<int> tunnelBucketOffsets = data.TunnelBucketOffsets;
            if (!tunnels.IsCreated || tunnels.Length < tunnelCount ||
                !bucketCounts.IsCreated || bucketCounts.Length < bucketCount ||
                !writeHeads.IsCreated || writeHeads.Length < bucketCount ||
                !tunnelBucketOffsets.IsCreated || tunnelBucketOffsets.Length < bucketCount + 1)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                bucketCounts[bucketIndex] = 0;
                writeHeads[bucketIndex] = 0;
            }

            for (int tunnelIndex = 0; tunnelIndex < tunnelCount; tunnelIndex++)
            {
                CaveTunnel tunnel = tunnels[tunnelIndex];
                if (!TryResolveTunnelPartitionBounds(in tunnel, data, out float3 boundsMin, out float3 boundsMax))
                    continue;

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                    bucketCounts[FlattenPartitionIndex(data, x, y, z)]++;
            }

            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                tunnelBucketOffsets[bucketIndex] = totalReferences;
                totalReferences += bucketCounts[bucketIndex];
            }

            tunnelBucketOffsets[bucketCount] = totalReferences;
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }

        if (totalReferences <= 0)
            return true;

        if (!TryEnsureTunnelSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, totalReferences))
            return false;

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            NativeArray<CaveTunnel> tunnels = data.Tunnels;
            NativeArray<int> writeHeads = data.ScratchLease.SpatialBucketWriteHeads;
            NativeArray<int> tunnelBucketOffsets = data.TunnelBucketOffsets;
            NativeArray<int> tunnelBucketIndices = data.TunnelBucketIndices;
            if (!tunnels.IsCreated || tunnels.Length < tunnelCount ||
                !writeHeads.IsCreated || writeHeads.Length < bucketCount ||
                !tunnelBucketOffsets.IsCreated || tunnelBucketOffsets.Length < bucketCount + 1 ||
                !tunnelBucketIndices.IsCreated || tunnelBucketIndices.Length < totalReferences)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
                writeHeads[bucketIndex] = tunnelBucketOffsets[bucketIndex];

            for (int tunnelIndex = 0; tunnelIndex < tunnelCount; tunnelIndex++)
            {
                CaveTunnel tunnel = tunnels[tunnelIndex];
                if (!TryResolveTunnelPartitionBounds(in tunnel, data, out float3 boundsMin, out float3 boundsMax))
                    continue;

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int bucketIndex = FlattenPartitionIndex(data, x, y, z);
                    int writeIndex = writeHeads[bucketIndex];
                    if (writeIndex < tunnelBucketOffsets[bucketIndex] ||
                        writeIndex >= tunnelBucketOffsets[bucketIndex + 1] ||
                        writeIndex >= tunnelBucketIndices.Length)
                    {
                        ReportVoxelMeshScratchCapacityOverflow();
                        return false;
                    }

                    tunnelBucketIndices[writeIndex] = tunnelIndex;
                    writeHeads[bucketIndex] = writeIndex + 1;
                }
            }
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }

        return true;
    }

    private static bool TryResolveNodePartitionBounds(
        in CaveNode node,
        VoxelPipelineData data,
        out float3 boundsMin,
        out float3 boundsMax)
    {
        boundsMin = default;
        boundsMax = default;
        if (data == null || !IsFiniteFloat3(node.position) || !IsFiniteFloat3(node.radii) || math.cmin(node.radii) <= 0f)
            return false;

        float3 safeRadii = math.clamp(
            node.radii,
            new float3(MinRuntimeCaveGraphBucketRadius),
            new float3(MaxRuntimeCaveGraphBucketRadius));
        float maxRadius = math.cmax(safeRadii);
        float inflation =
            ClampRuntimeFinite(node.noiseAmplitude, 0f, 0f, MaxRuntimeCaveGraphBucketNoiseMeters) +
            ClampRuntimeFinite(node.blendRadius, MinRuntimeCaveGraphBucketRadius, MinRuntimeCaveGraphBucketRadius, MaxRuntimeCaveGraphBucketBlendRadius) +
            ClampRuntimeFinite(data.CaveParams.warpAmplitude, 0f, 0f, MaxRuntimeCaveGraphBucketWarpMeters) +
            ClampRuntimeFinite(data.CaveParams.noiseEvalDistance, 0f, 0f, MaxRuntimeCaveGraphBucketNoiseMeters) +
            ClampRuntimeFinite(data.VoxelStep, 0.25f, 0.01f, MaxRuntimeCaveGraphBucketVoxelStep) * 2f;

        if (!math.isfinite(maxRadius) || !math.isfinite(inflation))
            return false;

        float extent = maxRadius + inflation;
        boundsMin = node.position - new float3(extent);
        boundsMax = node.position + new float3(extent);
        return IsFiniteFloat3(boundsMin) && IsFiniteFloat3(boundsMax);
    }

    private static bool TryResolveTunnelPartitionBounds(
        in CaveTunnel tunnel,
        VoxelPipelineData data,
        out float3 boundsMin,
        out float3 boundsMax)
    {
        boundsMin = default;
        boundsMax = default;
        if (data == null ||
            !IsFiniteFloat3(tunnel.pointA) ||
            !IsFiniteFloat3(tunnel.pointB) ||
            !math.isfinite(tunnel.radiusA) ||
            !math.isfinite(tunnel.radiusB) ||
            tunnel.radiusA <= 0f ||
            tunnel.radiusB <= 0f)
        {
            return false;
        }

        float maxRadius = math.max(
            ClampRuntimeFinite(tunnel.radiusA, MinRuntimeCaveGraphBucketRadius, MinRuntimeCaveGraphBucketRadius, MaxRuntimeCaveGraphBucketRadius),
            ClampRuntimeFinite(tunnel.radiusB, MinRuntimeCaveGraphBucketRadius, MinRuntimeCaveGraphBucketRadius, MaxRuntimeCaveGraphBucketRadius));
        float inflation =
            maxRadius +
            ClampRuntimeFinite(tunnel.blendRadius, MinRuntimeCaveGraphBucketRadius, MinRuntimeCaveGraphBucketRadius, MaxRuntimeCaveGraphBucketBlendRadius) +
            ClampRuntimeFinite(tunnel.warpAmount, 0f, 0f, MaxRuntimeCaveGraphBucketWarpMeters) +
            ClampRuntimeFinite(data.CaveParams.warpAmplitude, 0f, 0f, MaxRuntimeCaveGraphBucketWarpMeters) +
            ClampRuntimeFinite(data.CaveParams.noiseEvalDistance, 0f, 0f, MaxRuntimeCaveGraphBucketNoiseMeters) +
            ClampRuntimeFinite(data.VoxelStep, 0.25f, 0.01f, MaxRuntimeCaveGraphBucketVoxelStep) * 2f;

        if (!math.isfinite(maxRadius) || !math.isfinite(inflation))
            return false;

        boundsMin = math.min(tunnel.pointA, tunnel.pointB) - new float3(inflation);
        boundsMax = math.max(tunnel.pointA, tunnel.pointB) + new float3(inflation);
        return IsFiniteFloat3(boundsMin) && IsFiniteFloat3(boundsMax);
    }

    private static float ClampRuntimeFinite(float value, float fallback, float minimum, float maximum)
    {
        return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
    }

    static void ResolvePartitionRange(VoxelPipelineData data, float3 boundsMin, float3 boundsMax, out int3 minCell, out int3 maxCell)
    {
        float3 volumeSize = new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep;
        float3 clampedMin = math.clamp(boundsMin, data.VolumeOrigin, data.VolumeOrigin + volumeSize);
        float3 clampedMax = math.clamp(boundsMax, data.VolumeOrigin, data.VolumeOrigin + volumeSize);
        float3 invCellSize = new float3(
            1f / math.max(data.PartitionCellSize.x, 0.01f),
            1f / math.max(data.PartitionCellSize.y, 0.01f),
            1f / math.max(data.PartitionCellSize.z, 0.01f));
        float3 minPartition = (clampedMin - data.PartitionOrigin) * invCellSize;
        float3 maxPartition = (clampedMax - data.PartitionOrigin) * invCellSize;

        minCell = new int3(
            ClampFloorToPartitionCell(minPartition.x, data.PartitionDimX),
            ClampFloorToPartitionCell(minPartition.y, data.PartitionDimY),
            ClampFloorToPartitionCell(minPartition.z, data.PartitionDimZ));
        maxCell = new int3(
            ClampFloorToPartitionCell(maxPartition.x, data.PartitionDimX),
            ClampFloorToPartitionCell(maxPartition.y, data.PartitionDimY),
            ClampFloorToPartitionCell(maxPartition.z, data.PartitionDimZ));
    }

    private static int ClampFloorToPartitionCell(float coordinate, int partitionDim)
    {
        if (!math.isfinite(coordinate) || partitionDim <= 0)
            return 0;

        double floored = math.floor((double)coordinate);
        return (int)math.clamp(floored, 0d, (double)partitionDim - 1d);
    }

    static int FlattenPartitionIndex(VoxelPipelineData data, int x, int y, int z)
    {
        return x + (data.PartitionDimX * (y + (data.PartitionDimY * z)));
    }

    // ╔═══════════════════════════════════════════════╗
    // ║            INTERNAL HELPERS                   ║
    // ╚═══════════════════════════════════════════════╝

    static void BeginGenerationOperation()
    {
        Interlocked.Increment(ref _activeGenerationOperations);
    }

    static void EndGenerationOperation()
    {
        int remaining = Interlocked.Decrement(ref _activeGenerationOperations);
        if (remaining <= 0 && Volatile.Read(ref _shutdownRequested) == 1)
            TryShutdownSharedTables();
    }

    static void RequestSharedTableShutdown()
    {
        Volatile.Write(ref _shutdownRequested, 1);
        if (!CanRegisterDeferredVoxelLateFrameWork())
            FlushDeferredVoxelWorkWithoutDispatcher();

        TryShutdownSharedTables();
    }

    static void TryShutdownSharedTables()
    {
        if (Volatile.Read(ref _liveEngineCount) > 0)
            return;

        if (Volatile.Read(ref _activeGenerationOperations) > 0)
            return;

        if (_voxelMeshPoolWarmupRunning)
            return;

        if (HasPendingVoxelDeferredWork())
            return;

        if (Interlocked.Exchange(ref _shutdownRequested, 0) == 1)
        {
            DestroyVoxelMeshPools();
            DisposeVoxelMeshPipelineBlackBox();
            MCTables.Shutdown();
        }
    }

    private static bool HasPendingVoxelDeferredWork()
    {
        return DeferredVoxelPhysicsBakePendingCount > 0 ||
               _deferredVoxelColliderUploads.Count > 0;
    }

    private static void FlushDeferredVoxelWorkWithoutDispatcher()
    {
        for (int i = _deferredVoxelPhysicsBakeTeardowns.Count - 1; i >= 0; i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[i];
            RemoveDeferredVoxelPhysicsBakeTeardownAt(i);
            ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly(
                pending.Handle,
                pending.Mesh,
                pending.Owner,
                pending.Renderer,
                pending.Collider,
                pending.Flags,
                pending.ProxyCollider,
                publishWarning: false);
        }

        for (int i = _deferredVoxelPhysicsBakeEmergencyCount - 1; i >= 0; i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[i];
            RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(i);
            ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly(
                pending.Handle,
                pending.Mesh,
                pending.Owner,
                pending.Renderer,
                pending.Collider,
                pending.Flags,
                pending.ProxyCollider,
                publishWarning: false);
        }

        if (DeferredVoxelPhysicsBakePendingCount == 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
            if (_deferredVoxelPhysicsBakeTeardownRegistered && GlobalRegistry.Dispatcher != null)
                UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            else
                _deferredVoxelPhysicsBakeTeardownRegistered = false;
            UpdateDeferredVoxelPhysicsBakeBackpressure();
        }

        for (int i = _deferredVoxelColliderUploads.Count - 1; i >= 0; i--)
        {
            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[i];
            bool appliedUpload = false;
            if ((pending.Flags & DeferredVoxelColliderUploadVolumeFlag) != 0)
            {
                if (pending.Volume != null &&
                    pending.Volume.IsDeferredColliderChunkUploadReady(pending.ChunkIndex))
                {
                    appliedUpload = pending.Volume.CommitDeferredColliderChunkUpload(pending.ChunkIndex);
                }
            }
            else if (pending.Collider != null && pending.Mesh != null)
            {
                appliedUpload = CommitDeferredRootVoxelColliderUpload(
                    pending.Collider,
                    pending.Mesh,
                    pending.ProxyCollider);
            }

            if (!appliedUpload)
                CancelDeferredVoxelColliderUpload(ref pending, publishRetryDropWarning: false);

            RemoveDeferredVoxelColliderUploadAt(i);
        }

        if (_deferredVoxelColliderUploads.Count == 0)
        {
            _deferredVoxelColliderUploadScanCursor = -1;
            _voxelColliderUploadDropWarningArmed = false;
            _voxelColliderUploadRetryDropWarningArmed = false;
            if (_deferredVoxelColliderUploadRegistered && GlobalRegistry.Dispatcher != null)
                UnregisterDeferredVoxelColliderUploadDriver();
            else
                _deferredVoxelColliderUploadRegistered = false;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    struct VoxelColliderVertex
    {
        [FieldOffset(0)] public Vector3 Position;
        [FieldOffset(12)] private uint _pad0;
    }

    readonly struct VoxelFinalizeProjectionState
    {
        public readonly OriginShiftEventData StableShift;
        public readonly Vector3 RootRuntimePosition;
        public readonly byte ShiftEpochChanged;

        public VoxelFinalizeProjectionState(in OriginShiftEventData stableShift, Vector3 rootRuntimePosition, bool shiftEpochChanged)
        {
            StableShift = stableShift;
            RootRuntimePosition = rootRuntimePosition;
            ShiftEpochChanged = shiftEpochChanged ? (byte)1 : (byte)0;
        }

        public double3 AbsolutePositionOffsetDouble => StableShift.NewTotalOffsetDouble + global::Hecton8.World.AUPMath.ToDouble3(RootRuntimePosition);

        public float3 RuntimePositionOffset => (float3)RootRuntimePosition;

        public float3 ProjectRuntimePositionToLocal(Vector3 capturedRuntimePosition, double3 capturedTotalOffset)
        {
            float3 capturedRuntimeFloat = new float3(
                capturedRuntimePosition.x,
                capturedRuntimePosition.y,
                capturedRuntimePosition.z);
            if (!TryRebaseCapturedRuntimeFloat3(
                    capturedRuntimeFloat,
                    capturedTotalOffset,
                    StableShift.NewTotalOffsetDouble,
                    out float3 rebasedRuntimePosition))
            {
                return default;
            }

            float3 rootRuntimePosition = new float3(
                RootRuntimePosition.x,
                RootRuntimePosition.y,
                RootRuntimePosition.z);
            return rebasedRuntimePosition - rootRuntimePosition;
        }
    }

    static Bounds CalculatePositionBounds(NativeArray<float3> positions, int count, out bool invalidMeshData)
    {
        invalidMeshData = false;
        if (count <= 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        bool foundFinitePosition = false;
        float3 min = default;
        float3 max = default;
        for (int i = 0; i < count; i++)
        {
            float3 position = positions[i];
            if (!IsFiniteFloat3(position))
            {
                invalidMeshData = true;
                continue;
            }

            if (!foundFinitePosition)
            {
                min = position;
                max = position;
                foundFinitePosition = true;
                continue;
            }

            min = math.min(min, position);
            max = math.max(max, position);
        }

        if (!foundFinitePosition)
            return new Bounds(Vector3.zero, Vector3.one * 0.01f);

        float3 center = (min + max) * 0.5f;
        float3 size = math.max(max - min, new float3(0.01f));
        return new Bounds(center, size);
    }

    static Bounds CalculateVolumeLocalBounds(float3 volumeOrigin, float volumeHalfExtent)
    {
        float safeHalfExtent = math.isfinite(volumeHalfExtent) && volumeHalfExtent > 0f ? volumeHalfExtent : 0.5f;
        float3 min = volumeOrigin;
        float3 size = new float3(safeHalfExtent * 2f);
        if (!IsFiniteFloat3(min) || !IsFiniteFloat3(size))
            return new Bounds(Vector3.zero, Vector3.one * math.max(0.01f, safeHalfExtent * 2f));

        return new Bounds(min + size * 0.5f, math.max(size, new float3(0.01f)));
    }

    static float ResolveChunkBorderStitchWeight(float3 localPosition, Bounds bounds)
    {
        float3 min = (float3)bounds.min;
        float3 max = (float3)bounds.max;
        float3 size = math.max((float3)bounds.size, new float3(0.0001f));
        float3 edgeDistance = math.max(new float3(0f), math.min(localPosition - min, max - localPosition));
        float nearestEdgeDistance = math.cmin(edgeDistance);
        float stitchWidth = math.max(math.cmin(size) * 0.0625f, 0.25f);
        return math.saturate(1f - nearestEdgeDistance / stitchWidth);
    }

    static bool OffsetsApproximatelyMatch(double3 lhs, double3 rhs)
    {
        return math.lengthsq(lhs - rhs) <= 0.000001d;
    }

    Awaitable<int> BuildShiftAwareLocalPositionBufferAsync(
        VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState,
        CancellationToken ct)
    {
        return BuildShiftAwareLocalPositionBufferInternalAsync();

        async Awaitable<int> BuildShiftAwareLocalPositionBufferInternalAsync()
        {
            bool needsProjection =
                projectionState.ShiftEpochChanged != 0 ||
                !OffsetsApproximatelyMatch(data.AbsoluteUniverseOffsetAtStartDouble, projectionState.StableShift.NewTotalOffsetDouble) ||
                projectionState.RootRuntimePosition.sqrMagnitude > 0.000001f;

            if (!needsProjection)
                return 0;

            if (!TryEnsureProjectionScratchCapacity(ref data.ScratchLease, data.WeldedCount))
                return -1;

            if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return -1;
            }

            try
            {
                NativeArray<float3> projectedPositions = data.ScratchLease.ProjectedLocalPositions;
                NativeArray<float3> sourcePositions = data.WeldedPositions;
                double3 rebaseDeltaDouble = data.AbsoluteUniverseOffsetAtStartDouble - projectionState.StableShift.NewTotalOffsetDouble;
                if (!TryResolveLocalDeltaFloat3(rebaseDeltaDouble, out float3 rebaseDelta))
                    return -1;

                JobHandle projectionHandle = new VoxelShiftAwareProjectionJob
                {
                    rebaseDelta = rebaseDelta,
                    rootRuntimePosition = (float3)projectionState.RootRuntimePosition,
                    sourcePositions = sourcePositions,
                    projectedPositions = projectedPositions
                }.Schedule(data.WeldedCount, JOB_BATCH);

                await AwaitForJobCompletionAsync(projectionHandle, ct, "origin-shift projection");
                if (ct.IsCancellationRequested)
                    return -1;

                return 1;
            }
            finally
            {
                UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
            }
        }
    }

    static unsafe bool UploadSurfaceMesh(
        Mesh mesh,
        NativeArray<VoxelSurfaceVertex> surfaceVertices,
        NativeArray<int> triangleIndices,
        int vertexCount,
        int triangleIndexCount,
        Bounds bounds)
    {
        if (!CanUploadSurfaceMeshData(mesh, surfaceVertices, triangleIndices, vertexCount, triangleIndexCount))
        {
            ReportVoxelInvalidMeshUpload();
            return false;
        }

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        bool applied = false;
        try
        {
            Mesh.MeshData meshData = meshDataArray[0];
            meshData.SetVertexBufferParams(
                vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.UNorm8, 4));

            meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

            NativeArray<VoxelSurfaceVertex> vertexData = meshData.GetVertexData<VoxelSurfaceVertex>();
            NativeArray<VoxelSurfaceVertex>.Copy(surfaceVertices, 0, vertexData, 0, vertexCount);

            NativeArray<uint> indexData = meshData.GetIndexData<uint>();
            UnsafeUtility.MemCpy(
                indexData.GetUnsafePtr(),
                triangleIndices.GetUnsafeReadOnlyPtr(),
                (long)triangleIndexCount * UnsafeUtility.SizeOf<int>());

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount, MeshTopology.Triangles)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            applied = true;
            mesh.bounds = bounds;
            return true;
        }
        finally
        {
            if (!applied)
                meshDataArray.Dispose();
        }
    }

    static bool UploadColliderMesh(
        Mesh mesh,
        NativeArray<float3> positions,
        NativeArray<int> triangleIndices,
        int vertexCount,
        int triangleIndexCount)
    {
        if (!CanUploadMeshData(mesh, positions, triangleIndices, vertexCount, triangleIndexCount))
        {
            ReportVoxelInvalidMeshUpload();
            return false;
        }

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        bool applied = false;
        try
        {
            Mesh.MeshData meshData = meshDataArray[0];
            meshData.SetVertexBufferParams(
                vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm8, 4));

            meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

            NativeArray<VoxelColliderVertex> vertexData = meshData.GetVertexData<VoxelColliderVertex>();
            bool invalidMeshData = false;
            Bounds bounds = CalculatePositionBounds(positions, vertexCount, out invalidMeshData);
            float3 fallbackPosition = (float3)bounds.center;
            for (int i = 0; i < vertexCount; i++)
            {
                float3 position = positions[i];
                if (!IsFiniteFloat3(position))
                {
                    invalidMeshData = true;
                    position = fallbackPosition;
                }

                vertexData[i] = new VoxelColliderVertex { Position = position };
            }

            NativeArray<uint> indexData = meshData.GetIndexData<uint>();
            for (int i = 0; i < triangleIndexCount; i++)
            {
                int triangleIndex = triangleIndices[i];
                if ((uint)triangleIndex >= (uint)vertexCount)
                {
                    invalidMeshData = true;
                    triangleIndex = 0;
                }

                indexData[i] = (uint)triangleIndex;
            }

            if (invalidMeshData)
            {
                ReportVoxelInvalidMeshUpload();
                return false;
            }

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount, MeshTopology.Triangles)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            applied = true;
            mesh.bounds = bounds;
            return true;
        }
        finally
        {
            if (!applied)
                meshDataArray.Dispose();
        }
    }

    private static bool CanUploadMeshData(Mesh mesh, NativeArray<float3> positions, NativeArray<int> triangleIndices, int vertexCount, int triangleIndexCount)
    {
        return mesh != null &&
               positions.IsCreated &&
               triangleIndices.IsCreated &&
               vertexCount >= 3 &&
               triangleIndexCount >= 3 &&
               (triangleIndexCount % 3) == 0 &&
               vertexCount <= positions.Length &&
               triangleIndexCount <= triangleIndices.Length;
    }

    private static bool CanUploadSurfaceMeshData(Mesh mesh, NativeArray<VoxelSurfaceVertex> surfaceVertices, NativeArray<int> triangleIndices, int vertexCount, int triangleIndexCount)
    {
        return mesh != null &&
               surfaceVertices.IsCreated &&
               triangleIndices.IsCreated &&
               vertexCount >= 3 &&
               triangleIndexCount >= 3 &&
               (triangleIndexCount % 3) == 0 &&
               vertexCount <= surfaceVertices.Length &&
               triangleIndexCount <= triangleIndices.Length;
    }

    GameObject SpawnVolume()
    {
        if (TryResolveCachedObjectPool(out IObjectPoolService pool) && voxelVolumePrefab != null)
        {
            GameObject pooled = pool.Spawn(voxelVolumePrefab, Vector3.zero, Quaternion.identity);
            if (pooled != null)
            {
                PrepareVolumeForBuild(pooled);
                HectonFloatingOrigin.MarkShiftTargetsDirty();
                return pooled;
            }
        }

        if (Application.isPlaying)
        {
            ReportVoxelVolumeSpawnPoolMiss();
            return null;
        }

        var go = new GameObject(RuntimeCaveVolumeName);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<HectonVoxelVolume>(); // Add volume component
        PrepareVolumeForBuild(go);
        HectonFloatingOrigin.MarkShiftTargetsDirty();
        return go;
    }

    Mesh BuildWeldedMeshNative(GameObject go,
                               NativeArray<VoxelSurfaceVertex> surfaceVertices,
                               NativeArray<int> triangleIndices,
                               int triIndexCount,
                               int vertCount,
                                Bounds bounds,
                                Material mat,
                                Mesh reservedSurfaceMesh = null,
                                HectonVoxelVolume volume = null)
    {
        MeshFilter mf = ResolvePooledMeshFilter(go, volume);
        if (mf == null)
        {
            if (Application.isPlaying || volume != null)
                return null;

            mf = go.AddComponent<MeshFilter>();
        }

        MeshRenderer mr = ResolvePooledMeshRenderer(go, volume);
        if (mr == null)
        {
            if (Application.isPlaying || volume != null)
                return null;

            mr = go.AddComponent<MeshRenderer>();
        }

        Mesh mesh = mf.sharedMesh;
        bool attachAcquiredMesh = false;
        if (mesh == null)
        {
            mesh = reservedSurfaceMesh != null ? reservedSurfaceMesh : AcquireVoxelSurfaceMesh();
            if (mesh == null)
                return null;

            attachAcquiredMesh = true;
        }
        else
        {
            mesh.Clear();
        }

        if (!UploadSurfaceMesh(mesh, surfaceVertices, triangleIndices, vertCount, triIndexCount, bounds))
            return null;

        if (attachAcquiredMesh)
            mf.sharedMesh = mesh;

        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.enabled = true;
        return mesh;
    }

    private static void ReleaseOrDestroySurfaceMesh(MeshFilter meshFilter, bool destroyIfUnpooled)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        Mesh mesh = meshFilter.sharedMesh;
        meshFilter.sharedMesh = null;
        if (ReleaseVoxelSurfaceMesh(mesh))
            return;

        mesh.Clear(false);
        if (destroyIfUnpooled)
            DestroyDeferredVoxelObject(mesh);
    }

    Awaitable<bool> ApplyVolumeMeshAsync(GameObject go, VoxelPipelineData data, OriginShiftEventData stableShift, CancellationToken ct)
    {
        return ApplyVolumeMeshInternalAsync();

        async Awaitable<bool> ApplyVolumeMeshInternalAsync()
        {
            bool useProjectedLocalPositions = false;
            Mesh reservedSurfaceMesh = null;
            try
            {
                if (!TryResolveRuntimeFloat3FromAup(data.AbsoluteUniverseOffsetAtStartDouble, stableShift.NewTotalOffsetDouble, out float3 rootRuntimePositionFloat))
                    return false;

                Vector3 rootRuntimePosition = ToVector3(rootRuntimePositionFloat);
                VoxelFinalizeProjectionState projectionState = new VoxelFinalizeProjectionState(
                    stableShift,
                    rootRuntimePosition,
                    data.ShiftEpochAtStart != stableShift.Sequence);

                int projectionScratchState = await BuildShiftAwareLocalPositionBufferAsync(data, projectionState, ct);
                if (projectionScratchState < 0)
                    return false;

                useProjectedLocalPositions = projectionScratchState > 0;
                float3 localVolumeOrigin = useProjectedLocalPositions
                    ? projectionState.ProjectRuntimePositionToLocal((Vector3)data.VolumeOrigin, data.AbsoluteUniverseOffsetAtStartDouble)
                    : data.VolumeOrigin;
                HectonVoxelVolume volume = data.SourceVolume;

                if (NeedsVoxelSurfaceMeshAcquire(go, volume) &&
                    (reservedSurfaceMesh = await AcquireVoxelSurfaceMeshAsync(ct)) == null)
                {
                    return false;
                }

                await AwaitVoxelMeshUploadBudgetAsync(ct);
                if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return false;
                }

                Mesh mesh;
                try
                {
                    NativeArray<float3> meshLocalPositions = useProjectedLocalPositions
                        ? data.ScratchLease.ProjectedLocalPositions
                        : data.WeldedPositions;
                    Bounds meshBounds = CalculateVolumeLocalBounds(localVolumeOrigin, data.VolumeHalfExtent);
                    float3 meshBoundsMin = (float3)meshBounds.min;
                    float3 meshBoundsMax = (float3)meshBounds.max;
                    JobHandle packSurfaceHandle = new VoxelPackSurfaceVertexJob
                    {
                        positions = meshLocalPositions,
                        normals = data.Normals,
                        ambientOcclusionValues = data.AmbientOcclusionValues,
                        curvatureValues = data.CurvatureValues,
                        skirtAlphaValues = data.SkirtAlphaValues,
                        dirtyBlendValues = data.DirtyBlendValues,
                        surfaceVertices = data.SurfaceVertices,
                        vertexCount = data.WeldedCount,
                        boundsMin = meshBoundsMin,
                        boundsMax = meshBoundsMax,
                        runtimePositionOffset = projectionState.RuntimePositionOffset
                    }.Schedule(data.WeldedCount, JOB_BATCH);
                    JobHandle sanitizeIndicesHandle = new VoxelSanitizeTriangleIndexJob
                    {
                        vertexCount = data.WeldedCount,
                        indexCount = data.RawCount,
                        triangleIndices = data.TriangleIndices,
                        densityFaultFlags = data.ScratchLease.DensityFaultFlags
                    }.Schedule(data.RawCount, JOB_BATCH);
                    JobHandle uploadPrepHandle = JobHandle.CombineDependencies(packSurfaceHandle, sanitizeIndicesHandle);
                    await AwaitForJobCompletionAsync(uploadPrepHandle, ct, "surface vertex pack");
                    if (ct.IsCancellationRequested)
                        return false;

                    mesh = BuildWeldedMeshNative(
                        go,
                        data.SurfaceVertices,
                        data.TriangleIndices,
                        data.RawCount,
                        data.WeldedCount,
                        meshBounds,
                        voxelMaterial,
                        reservedSurfaceMesh,
                        volume);
                    if (ReferenceEquals(mesh, reservedSurfaceMesh))
                        reservedSurfaceMesh = null;
                }
                finally
                {
                    UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
                }

                if (mesh == null)
                    return false;
                RecordVoxelChunkMeshed();
                await AwaitableDebtMonitor.NextFrameAsync();
                if (ct.IsCancellationRequested)
                    return false;

                bool buildCollider = data.BuildCollider && !ShouldUseCinematicColliderFake(in data);
                if (volume == null && Application.isPlaying)
                {
                    ReportVoxelNullVolumeColliderFallback();
                    return true;
                }

                MeshCollider mcol = ResolvePooledMeshCollider(go, volume);

                if (!buildCollider)
                {
                    if (volume != null)
                        volume.DisableColliderChunksForCinematicFake();

                    if (mcol != null)
                    {
                        mcol.enabled = false;
                    }

                    if (!Application.isPlaying)
                    {
                        BoxCollider rootBakeProxy = ResolvePooledBoxCollider(go);
                        DisableVoxelBakeProxy(rootBakeProxy);
                        Transform isolatedProxy = go.transform.Find(VoxelBakeProxyRuntimeName);
                        if (isolatedProxy != null && isolatedProxy.TryGetComponent(out BoxCollider isolatedProxyCollider))
                            DisableVoxelBakeProxy(isolatedProxyCollider);
                    }

                    return true;
                }

                if (mcol == null && volume != null)
                {
                    volume.DisableColliderChunksForCinematicFake();
                    return true;
                }

                bool hasSelectedChthonicPillar = false;
                if (volume != null)
                {
                    if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
                    {
                        ReportVoxelMeshScratchCapacityOverflow();
                        return false;
                    }

                    try
                    {
                        hasSelectedChthonicPillar = TryResolveSelectedChthonicPillarRecord(in data, out _);
                    }
                    finally
                    {
                        UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
                    }
                }

                if (hasSelectedChthonicPillar)
                {
                    if (mcol != null)
                        mcol.enabled = false;
                    return ApplySmoothChthonicPillarColliderMesh(volume, data, projectionState);
                }

                if (volume == null)
                {
                    BoxCollider fallbackBakeProxy = EnsureVoxelBakeProxyCollider(go);
                    MeshRenderer fallbackRenderer = ResolvePooledMeshRenderer(go, null);
                    bool keepFallbackBakeProxy = false;
                    ConfigureVoxelBakeBaseProxy(
                        fallbackBakeProxy,
                        localVolumeOrigin,
                        new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep,
                        data.VoxelStep);

                    try
                    {
                        if (fallbackRenderer != null)
                            fallbackRenderer.enabled = true;
                        EnableVoxelProxyCollider(fallbackBakeProxy);
                        keepFallbackBakeProxy = fallbackBakeProxy != null;
                        return true;
                    }
                    finally
                    {
                        if (!keepFallbackBakeProxy)
                            DisableVoxelBakeProxy(fallbackBakeProxy);
                    }
                }

                if (mcol != null)
                    mcol.enabled = false;
                if (UseSurfaceNets)
                {
                    return await ApplySurfaceNetsColliderMeshesAsync(volume, data, localVolumeOrigin, ct);
                }
                else
                {
                    return await ApplyChunkedColliderMeshesAsync(volume, data, useProjectedLocalPositions, localVolumeOrigin, ct);
                }
            }
            finally
            {
                if (reservedSurfaceMesh != null)
                    ReleaseVoxelSurfaceMesh(reservedSurfaceMesh);
            }
        }
    }

    // Reused across collider chunk bakes. Mesh.SetVertices/SetTriangles need managed arrays, and
    // allocating a fresh pair per chunk per rebuild put real garbage on the streaming path (a single
    // 5k-vertex chunk is ~60 KB of Vector3 alone) against the zero-GC mandate. Grown monotonically to
    // the high-water mark, never shrunk.
    //
    // INVARIANT: these are filled and then consumed by SetVertices/SetTriangles with NO await in
    // between, so a second chunk iteration can never observe a half-filled buffer - continuations on
    // the main thread only interleave at await points. Do not introduce an await between the fill
    // loops and the SetVertices/SetTriangles calls without giving each in-flight bake its own buffer.
    private Vector3[] _colliderBakePositionScratch = System.Array.Empty<Vector3>();
    private int[] _colliderBakeIndexScratch = System.Array.Empty<int>();

    async Awaitable<bool> ApplySurfaceNetsColliderMeshesAsync(
        HectonVoxelVolume volume,
        VoxelPipelineData data,
        float3 localVolumeOrigin,
        CancellationToken ct)
    {
        if (volume == null)
            return false;

        IDataVault vault = GlobalRegistry.DataVault;
        if (vault == null)
            vault = _streamingScratchVault;

        if (vault == null || !VoxelSurfaceNetsVault.TryResolveViews(vault, out VoxelSurfaceNetsVaultBuffers buffers))
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        NativeArray<VoxelVertexDTO> colliderVertices = buffers.ColliderVertices;
        NativeArray<uint> colliderIndices = buffers.ColliderIndices;
        NativeArray<ChunkMeshingStateDTO> states = buffers.States;
        NativeArray<sbyte> vaultDensity = buffers.Density;
        // Canonical collider element counts live here, written by the IsCanonicalCollider extraction
        // pass. ChunkMeshingStateDTO carries the VISUAL counts and must not be used to size this bake.
        NativeArray<VoxelSurfacePhysicsBakeRequestDTO> bakeRequests = buffers.PhysicsBakeRequests;

        if (!colliderVertices.IsCreated || !colliderIndices.IsCreated || !states.IsCreated || !bakeRequests.IsCreated)
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        NativeArray<sbyte> srcDensity = data.ScratchLease.QuantizedDensityField;
        if (srcDensity.IsCreated && vaultDensity.IsCreated && srcDensity.Length > 0)
        {
            int copyLength = math.min(srcDensity.Length, vaultDensity.Length);
            NativeArray<sbyte>.Copy(srcDensity, vaultDensity, copyLength);
        }

        // VoxelPipelineData has no chunk count. Derive it exactly as the chunked collider path
        // does (ResolveColliderChunkCount over the triangle count) and clamp to the state array,
        // so an empty state array cannot drive an out-of-range chunk loop.
        int colliderChunkCount = math.min(ResolveColliderChunkCount(data.RawCount / 3), states.Length);

        for (int chunkIdx = 0; chunkIdx < colliderChunkCount; chunkIdx++)
        {
            if (VoxelSurfaceNetsVault.TryScheduleExtractionPinned(buffers, chunkIdx, (uint)Time.frameCount, default, out JobHandle extractHandle, out var extractLease))
            {
                if (VoxelSurfaceNetsVault.TrySchedulePhysicsBakeRequestsPinned(buffers, 0, extractHandle, out JobHandle bakeHandle, out var bakeLease))
                {
                    await AwaitForJobCompletionAsync(bakeHandle, ct, "surface nets extraction & bake");
                    VoxelSurfaceNetsVault.ReleaseJobBufferLease(ref bakeLease);
                }
                else
                {
                    await AwaitForJobCompletionAsync(extractHandle, ct, "surface nets extraction");
                }
                VoxelSurfaceNetsVault.ReleaseJobBufferLease(ref extractLease);
            }
        }

        if (!volume.TryUsePrewarmedColliderChunkCapacity(colliderChunkCount))
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        float3 boundsMin = localVolumeOrigin;
        float3 boundsSize = new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep;

        for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
        {
            // Size this bake from the CANONICAL collider counts, never from ChunkMeshingStateDTO.
            // ChunkMeshingStateDTO.VertexCount/IndexCount are written by the VISUAL extraction pass,
            // which runs at ResolveSamplingStride(quality) with a decimation bias; the collider pass
            // runs at stride = 1 with no decimation and reports its own counts here. Sizing the bake
            // from the visual counts truncated the canonical buffers below GlobalQualityWeight 1: the
            // surviving collider indices still referenced vertices past the end of the shortened
            // vertex array, so Unity rejected the mesh and the chunk lost collision entirely. That
            // made collision truth a function of a graphics setting, which voxels.md forbids
            // ("GlobalQualityWeight ... must not change collision truth").
            if ((uint)chunkIndex >= (uint)bakeRequests.Length)
            {
                volume.DisableColliderChunkBakeProxy(chunkIndex);
                continue;
            }

            VoxelSurfacePhysicsBakeRequestDTO bakeRequest = bakeRequests[chunkIndex];
            int vertCount = bakeRequest.ColliderVertexCount;
            int indexCount = bakeRequest.ColliderIndexCount;

            // CapacityClamped means the canonical collider pass ran out of index buffer, so the
            // triangles it emitted are an arbitrary PREFIX of the chunk's real surface. Baking that
            // prefix is worse than not baking at all: the mesh replaces the solid box proxy below
            // with a surface full of holes, which is precisely the "player falls through cave walls"
            // failure. Keep the proxy instead - the wrong shape, but closed and solid - and skip the
            // mesh. VoxelSurfacePhysicsBakeRequestJob already refuses clamped chunks for the async
            // bake queue; this makes the direct bake path agree with it rather than contradict it.
            if ((bakeRequest.Flags & VoxelMeshingFlags.CapacityClamped) != 0)
            {
                if (volume.GetColliderChunkBakeProxy(chunkIndex) != null)
                {
                    ResolveVoxelColliderChunkBakeProxyBounds(
                        chunkIndex,
                        colliderChunkCount,
                        boundsMin,
                        boundsSize,
                        data.VoxelStep,
                        out Vector3 clampedProxyCenter,
                        out Vector3 clampedProxySize);
                    volume.ConfigureColliderChunkBakeProxy(chunkIndex, clampedProxyCenter, clampedProxySize);
                }

                volume.ReleaseColliderChunkBakeMesh(chunkIndex);
                continue;
            }

            if (vertCount <= 0 || indexCount <= 0 || vertCount > colliderVertices.Length || indexCount > colliderIndices.Length)
            {
                volume.DisableColliderChunkBakeProxy(chunkIndex);
                continue;
            }

            BoxCollider chunkProxy = volume.GetColliderChunkBakeProxy(chunkIndex);
            if (chunkProxy != null)
            {
                ResolveVoxelColliderChunkBakeProxyBounds(
                    chunkIndex,
                    colliderChunkCount,
                    boundsMin,
                    boundsSize,
                    data.VoxelStep,
                    out Vector3 proxyCenter,
                    out Vector3 proxySize);
                volume.ConfigureColliderChunkBakeProxy(chunkIndex, proxyCenter, proxySize);
            }

            Mesh chunkBakeMesh = volume.GetOrCreateColliderChunkBakeMesh(chunkIndex);
            if (chunkBakeMesh != null)
            {
                // Reused scratch, not per-chunk allocation. See the field declarations for the
                // no-await-between-fill-and-consume invariant this relies on.
                if (_colliderBakePositionScratch.Length < vertCount)
                    _colliderBakePositionScratch = new Vector3[math.ceilpow2(vertCount)];
                if (_colliderBakeIndexScratch.Length < indexCount)
                    _colliderBakeIndexScratch = new int[math.ceilpow2(indexCount)];

                Vector3[] positions = _colliderBakePositionScratch;
                for (int i = 0; i < vertCount; i++)
                {
                    positions[i] = colliderVertices[i].Position;
                }

                int[] indices = _colliderBakeIndexScratch;
                for (int i = 0; i < indexCount; i++)
                {
                    indices[i] = (int)colliderIndices[i];
                }

                // Length-bounded overloads are mandatory here: the scratch arrays are sized to the
                // high-water mark, so the array-only overloads would upload stale trailing elements
                // from a previous, larger chunk.
                chunkBakeMesh.Clear(false);
                chunkBakeMesh.SetVertices(positions, 0, vertCount);
                chunkBakeMesh.SetTriangles(indices, 0, indexCount, 0);

                UnityEngine.EntityId bakeMeshEntityId = chunkBakeMesh.GetEntityId();
                await Awaitable.BackgroundThreadAsync();
                // Explicit options, not the two-argument default overload: the collider that receives
                // this mesh sets the identical value, which is the precondition for PhysX reusing the
                // bake instead of re-cooking on the main thread.
                Physics.BakeMesh(bakeMeshEntityId, false, Hecton8.Caves.HectonVoxelVolume.VoxelColliderCookingOptions);
                await Awaitable.MainThreadAsync();

                if (ct.IsCancellationRequested)
                    return false;

                if (!volume.AssignColliderChunkBakeMesh(chunkIndex, chunkBakeMesh) ||
                    !EnqueueDeferredVoxelColliderUpload(volume, chunkIndex))
                {
                    volume.ReleaseColliderChunkBakeMesh(chunkIndex);
                }
            }
        }

        volume.SetActiveColliderChunkCount(colliderChunkCount);
        return true;
    }

    static bool TryResolveSelectedChthonicPillarRecord(in VoxelPipelineData data, out AnomalyFeatureRecord record)
    {
        record = default;
        NativeArray<AnomalyFeatureRecord> selected = data.ScratchLease.SelectedPillarFeature;
        if (!selected.IsCreated || selected.Length <= 0)
            return false;

        record = selected[0];
        return record.Valid != 0 && record.Kind == (byte)AnomalyFeatureKind.ChthonicPillar;
    }

    bool ApplySmoothChthonicPillarColliderMesh(
        HectonVoxelVolume volume,
        VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState)
    {
        if (volume == null)
            return false;

        if (!volume.TryUsePrewarmedColliderChunkCapacity(1))
            return false;
        if (!TryConfigureChthonicPillarRuntimeProxy(volume, in data, projectionState))
        {
            volume.DisableColliderChunksForCinematicFake();
            return true;
        }

        volume.SetActiveColliderChunkCount(1);
        return true;
    }

    bool TryConfigureChthonicPillarRuntimeProxy(
        HectonVoxelVolume volume,
        in VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState)
    {
        if (volume == null || !TryResolveSelectedChthonicPillarRecord(in data, out AnomalyFeatureRecord record))
            return false;

        double3 baseAup = new double3(record.AupX, record.AupY, record.AupZ);
        double3 chunkMinAup = global::Hecton8.World.AUPMath.ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;
        double3 chunkMaxAup = chunkMinAup + new double3(
            math.max(1, data.PtsX) - 1,
            math.max(1, data.PtsY) - 1,
            math.max(1, data.PtsZ) - 1) * math.max(0.001f, data.VoxelStep);
        double radius = ChthonicPillarRadiusMeters;
        double pillarMinY = baseAup.y;
        double pillarMaxY = baseAup.y + ChthonicPillarHeightMeters;
        if (baseAup.x + radius < chunkMinAup.x ||
            baseAup.x - radius > chunkMaxAup.x ||
            baseAup.z + radius < chunkMinAup.z ||
            baseAup.z - radius > chunkMaxAup.z ||
            pillarMaxY < chunkMinAup.y ||
            pillarMinY > chunkMaxAup.y)
        {
            return false;
        }

        double bottom = math.max(pillarMinY, chunkMinAup.y);
        double top = math.min(pillarMaxY, chunkMaxAup.y);
        if (top - bottom <= 0.01d)
            return false;

        double3 localOffset = projectionState.AbsolutePositionOffsetDouble;
        float height = math.max((float)(top - bottom), VoxelPhysicsBakeProxyMinHeightMeters);
        Vector3 center = new Vector3(
            (float)(baseAup.x - localOffset.x),
            (float)(((bottom + top) * 0.5d) - localOffset.y),
            (float)(baseAup.z - localOffset.z));
        Vector3 size = new Vector3(
            ChthonicPillarRadiusMeters * 2f,
            height,
            ChthonicPillarRadiusMeters * 2f);

        volume.ConfigureColliderChunkBakeProxy(0, center, size);
        return true;
    }

    static float2 GetChthonicPillarColliderUnitCircle(int index)
    {
        switch (index)
        {
            case 0: return new float2(1f, 0f);
            case 1: return new float2(0.9659258f, 0.258819f);
            case 2: return new float2(0.8660254f, 0.5f);
            case 3: return new float2(0.7071068f, 0.7071068f);
            case 4: return new float2(0.5f, 0.8660254f);
            case 5: return new float2(0.258819f, 0.9659258f);
            case 6: return new float2(0f, 1f);
            case 7: return new float2(-0.258819f, 0.9659258f);
            case 8: return new float2(-0.5f, 0.8660254f);
            case 9: return new float2(-0.7071068f, 0.7071068f);
            case 10: return new float2(-0.8660254f, 0.5f);
            case 11: return new float2(-0.9659258f, 0.258819f);
            case 12: return new float2(-1f, 0f);
            case 13: return new float2(-0.9659258f, -0.258819f);
            case 14: return new float2(-0.8660254f, -0.5f);
            case 15: return new float2(-0.7071068f, -0.7071068f);
            case 16: return new float2(-0.5f, -0.8660254f);
            case 17: return new float2(-0.258819f, -0.9659258f);
            case 18: return new float2(0f, -1f);
            case 19: return new float2(0.258819f, -0.9659258f);
            case 20: return new float2(0.5f, -0.8660254f);
            case 21: return new float2(0.7071068f, -0.7071068f);
            case 22: return new float2(0.8660254f, -0.5f);
            case 23: return new float2(0.9659258f, -0.258819f);
            default: return new float2(1f, 0f);
        }
    }

    bool TryBuildSmoothChthonicPillarColliderMesh(
        in VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState,
        ref NativeArray<float3> positions,
        ref NativeArray<int> indices,
        out int vertexCount,
        out int indexCount)
    {
        vertexCount = 0;
        indexCount = 0;
        if (!TryResolveSelectedChthonicPillarRecord(in data, out AnomalyFeatureRecord record))
            return false;

        double3 baseAup = new double3(record.AupX, record.AupY, record.AupZ);
        double3 chunkMinAup = global::Hecton8.World.AUPMath.ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;
        double3 chunkMaxAup = chunkMinAup + new double3(
            math.max(1, data.PtsX) - 1,
            math.max(1, data.PtsY) - 1,
            math.max(1, data.PtsZ) - 1) * math.max(0.001f, data.VoxelStep);
        double radius = ChthonicPillarRadiusMeters;
        double pillarMinY = baseAup.y;
        double pillarMaxY = baseAup.y + ChthonicPillarHeightMeters;
        if (baseAup.x + radius < chunkMinAup.x ||
            baseAup.x - radius > chunkMaxAup.x ||
            baseAup.z + radius < chunkMinAup.z ||
            baseAup.z - radius > chunkMaxAup.z ||
            pillarMaxY < chunkMinAup.y ||
            pillarMinY > chunkMaxAup.y)
        {
            return false;
        }

        double bottom = math.max(pillarMinY, chunkMinAup.y);
        double top = math.min(pillarMaxY, chunkMaxAup.y);
        if (top - bottom <= 0.01d)
            return false;

        int segments = ChthonicPillarColliderSegments;
        vertexCount = segments * 2 + 2;
        indexCount = segments * 12;
        if (!TryEnsureColliderChunkScratchCapacity(
                ref data.ScratchLease,
                1,
                indexCount,
                vertexCount,
                1))
        {
            vertexCount = 0;
            indexCount = 0;
            return false;
        }

        positions = data.ScratchLease.ColliderLocalPositions;
        indices = data.ScratchLease.ColliderLocalIndices;

        double3 localOffset = projectionState.AbsolutePositionOffsetDouble;
        float localBottomY = (float)(bottom - localOffset.y);
        float localTopY = (float)(top - localOffset.y);
        float localCenterX = (float)(baseAup.x - localOffset.x);
        float localCenterZ = (float)(baseAup.z - localOffset.z);
        float safeRadius = ChthonicPillarRadiusMeters;

        for (int segment = 0; segment < segments; segment++)
        {
            float2 unit = GetChthonicPillarColliderUnitCircle(segment);
            float x = localCenterX + unit.x * safeRadius;
            float z = localCenterZ + unit.y * safeRadius;
            int vertexBase = segment * 2;
            positions[vertexBase] = new float3(x, localBottomY, z);
            positions[vertexBase + 1] = new float3(x, localTopY, z);
        }

        int bottomCenter = segments * 2;
        int topCenter = bottomCenter + 1;
        positions[bottomCenter] = new float3(localCenterX, localBottomY, localCenterZ);
        positions[topCenter] = new float3(localCenterX, localTopY, localCenterZ);

        int write = 0;
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            int bottomA = segment * 2;
            int topA = bottomA + 1;
            int bottomB = next * 2;
            int topB = bottomB + 1;

            indices[write++] = bottomA;
            indices[write++] = topA;
            indices[write++] = bottomB;
            indices[write++] = bottomB;
            indices[write++] = topA;
            indices[write++] = topB;

            indices[write++] = bottomCenter;
            indices[write++] = bottomB;
            indices[write++] = bottomA;

            indices[write++] = topCenter;
            indices[write++] = topA;
            indices[write++] = topB;
        }

        return true;
    }

    static int ResolveColliderChunkCount(int triangleCount)
    {
        if (triangleCount >= 40000)
            return 8;

        if (triangleCount >= 10000)
            return 4;

        return 2;
    }

    static BoxCollider EnsureVoxelBakeProxyCollider(GameObject owner)
    {
        if (owner == null)
            return null;

        EnsureVoxelProxyLayerFiltering();
        Transform proxyTransform = owner.transform.Find(VoxelBakeProxyRuntimeName);
        if (proxyTransform == null)
        {
            GameObject proxyObject = new GameObject(VoxelBakeProxyRuntimeName); // COLD ALLOC: GameObject[1] - isolated fallback async bake proxy collider - owner: HectonVoxelEngine
            proxyObject.layer = HectonLayerMasks.VoxelProxy;
            proxyTransform = proxyObject.transform;
            proxyTransform.SetParent(owner.transform, false);
            proxyTransform.localPosition = Vector3.zero;
            proxyTransform.localRotation = Quaternion.identity;
            proxyTransform.localScale = Vector3.one;
        }

        proxyTransform.gameObject.layer = HectonLayerMasks.VoxelProxy;
        if (!proxyTransform.TryGetComponent(out BoxCollider proxy))
            proxy = proxyTransform.gameObject.AddComponent<BoxCollider>();

        proxy.isTrigger = false;
        return proxy;
    }

    static float3 SanitizeVoxelColliderProxyBoundsMin(float3 boundsMin)
    {
        if (!IsFiniteFloat3(boundsMin))
            return float3.zero;

        return math.clamp(
            boundsMin,
            new float3(-VoxelColliderProxyMaxExtentMeters),
            new float3(VoxelColliderProxyMaxExtentMeters));
    }

    static float3 SanitizeVoxelColliderProxyBoundsSize(float3 boundsSize, float fallbackExtent)
    {
        float safeFallback = ClampRuntimeFinite(
            fallbackExtent,
            VoxelPhysicsBakeProxyMinHeightMeters,
            VoxelColliderProxyMinExtentMeters,
            MaxRuntimeCaveGraphBucketVoxelStep);
        if (!IsFiniteFloat3(boundsSize))
            return new float3(safeFallback);

        return math.clamp(
            math.abs(boundsSize),
            new float3(VoxelColliderProxyMinExtentMeters),
            new float3(VoxelColliderProxyMaxExtentMeters));
    }

    static void ConfigureVoxelBakeBaseProxy(
        BoxCollider proxy,
        float3 boundsMin,
        float3 boundsSize,
        float voxelStep)
    {
        if (proxy == null)
            return;

        float safeVoxelStep = ClampRuntimeFinite(voxelStep, VoxelPhysicsBakeProxyMinHeightMeters, VoxelColliderProxyMinExtentMeters, MaxRuntimeCaveGraphBucketVoxelStep);
        float3 safeBoundsMin = SanitizeVoxelColliderProxyBoundsMin(boundsMin);
        float3 safeSize = SanitizeVoxelColliderProxyBoundsSize(boundsSize, safeVoxelStep);
        float proxyHeight = math.max(VoxelPhysicsBakeProxyMinHeightMeters, safeVoxelStep * 2f);
        proxy.center = new Vector3(
            safeBoundsMin.x + safeSize.x * 0.5f,
            safeBoundsMin.y + proxyHeight * 0.5f,
            safeBoundsMin.z + safeSize.z * 0.5f);
        proxy.size = new Vector3(safeSize.x, proxyHeight, safeSize.z);
        EnableVoxelProxyCollider(proxy);
    }

    static void DisableVoxelBakeProxy(BoxCollider proxy)
    {
        if (proxy != null)
            proxy.enabled = false;
    }

    static void ResolveVoxelColliderChunkBakeProxyBounds(
        int chunkIndex,
        int colliderChunkCount,
        float3 boundsMin,
        float3 boundsSize,
        float voxelStep,
        out Vector3 center,
        out Vector3 size)
    {
        float safeVoxelStep = ClampRuntimeFinite(voxelStep, VoxelPhysicsBakeProxyMinHeightMeters, VoxelColliderProxyMinExtentMeters, MaxRuntimeCaveGraphBucketVoxelStep);
        float3 safeBoundsMin = SanitizeVoxelColliderProxyBoundsMin(boundsMin);
        float3 safeBoundsSize = SanitizeVoxelColliderProxyBoundsSize(boundsSize, safeVoxelStep);
        bool splitY = colliderChunkCount > 4;
        int x = chunkIndex & 1;
        int z = (chunkIndex >> 1) & 1;
        int y = splitY ? (chunkIndex >> 2) & 1 : 0;
        float3 chunkSize = new float3(
            safeBoundsSize.x * 0.5f,
            splitY ? safeBoundsSize.y * 0.5f : safeBoundsSize.y,
            safeBoundsSize.z * 0.5f);
        float3 chunkMin = safeBoundsMin + new float3(chunkSize.x * x, chunkSize.y * y, chunkSize.z * z);
        float proxyHeight = math.min(chunkSize.y, math.max(VoxelPhysicsBakeProxyMinHeightMeters, safeVoxelStep * 2f));
        float3 proxySize = new float3(chunkSize.x, proxyHeight, chunkSize.z);
        float3 proxyCenter = new float3(
            chunkMin.x + proxySize.x * 0.5f,
            chunkMin.y + proxySize.y * 0.5f,
            chunkMin.z + proxySize.z * 0.5f);

        center = new Vector3(proxyCenter.x, proxyCenter.y, proxyCenter.z);
        size = new Vector3(proxySize.x, proxySize.y, proxySize.z);
    }

    async Awaitable<bool> ApplyChunkedColliderMeshesAsync(
        HectonVoxelVolume volume,
        VoxelPipelineData data,
        bool useProjectedLocalPositions,
        float3 localVolumeOrigin,
        CancellationToken ct)
    {
        int triangleIndexCount = data.RawCount;
        int triangleCount = triangleIndexCount / 3;
        if (triangleCount <= 0)
        {
            volume.DisableColliderChunksForCinematicFake();
            return true;
        }

        int colliderChunkCount = ResolveColliderChunkCount(triangleCount);
        if (!volume.TryUsePrewarmedColliderChunkCapacity(colliderChunkCount))
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        if (!TryEnsureColliderChunkScratchCapacity(
                ref data.ScratchLease,
                triangleCount,
                triangleIndexCount,
                data.WeldedCount,
                colliderChunkCount))
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        bool completed = false;

        try
        {
            long chunkGenerationFrameStart = Stopwatch.GetTimestamp();

            float3 boundsMin = localVolumeOrigin;
            float3 boundsSize = new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep;

            if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
            {
                ReportVoxelMeshScratchCapacityOverflow();
                volume.DisableColliderChunksForCinematicFake();
                return false;
            }

            try
            {
                NativeArray<float3> meshLocalPositions = useProjectedLocalPositions
                    ? data.ScratchLease.ProjectedLocalPositions
                    : data.WeldedPositions;
                NativeArray<int> triangleIndices = data.TriangleIndices;
                NativeArray<byte> triangleBuckets = data.ScratchLease.ColliderTriangleBuckets;
                NativeArray<int> bucketCounts = data.ScratchLease.ColliderBucketCounts;
                NativeArray<int> bucketOffsets = data.ScratchLease.ColliderBucketOffsets;
                NativeArray<int> bucketWriteHeads = data.ScratchLease.ColliderBucketWriteHeads;
                NativeArray<int> chunkTriangleIndices = data.ScratchLease.ColliderChunkTriangleIndices;
                NativeArray<int> localRemap = data.ScratchLease.ColliderLocalRemap;

                if (!meshLocalPositions.IsCreated ||
                    !triangleIndices.IsCreated ||
                    !triangleBuckets.IsCreated ||
                    !bucketCounts.IsCreated ||
                    !bucketOffsets.IsCreated ||
                    !bucketWriteHeads.IsCreated ||
                    !chunkTriangleIndices.IsCreated ||
                    !localRemap.IsCreated ||
                    triangleIndices.Length < triangleIndexCount ||
                    triangleBuckets.Length < triangleCount ||
                    bucketCounts.Length < colliderChunkCount ||
                    bucketOffsets.Length < colliderChunkCount ||
                    bucketWriteHeads.Length < colliderChunkCount ||
                    chunkTriangleIndices.Length < triangleIndexCount ||
                    localRemap.Length < data.WeldedCount)
                {
                    ReportVoxelInvalidMeshUpload();
                    volume.DisableColliderChunksForCinematicFake();
                    return false;
                }

                for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
                {
                    bucketCounts[chunkIndex] = 0;
                    bucketOffsets[chunkIndex] = 0;
                    bucketWriteHeads[chunkIndex] = 0;
                }

                JobHandle clearRemapHandle = new VoxelFillIntArrayJob
                {
                    Value = -1,
                    Values = localRemap
                }.Schedule(data.WeldedCount, JOB_BATCH);

                JobHandle classifyHandle = new VoxelColliderChunkClassifyJob
                {
                    positions = meshLocalPositions,
                    triangleIndices = triangleIndices,
                    boundsMin = boundsMin,
                    boundsSize = boundsSize,
                    chunkCount = colliderChunkCount,
                    triangleBuckets = triangleBuckets
                }.Schedule(triangleCount, 64, clearRemapHandle);

                await AwaitForJobCompletionAsync(classifyHandle, ct, "collider chunk classify");
                if (ct.IsCancellationRequested)
                    return false;

                for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                {
                    int bucket = triangleBuckets[triangleIndex];
                    if ((uint)bucket >= (uint)colliderChunkCount)
                    {
                        ReportVoxelInvalidMeshUpload();
                        volume.DisableColliderChunksForCinematicFake();
                        return false;
                    }

                    bucketCounts[bucket] += 3;
                }

                int runningOffset = 0;
                for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
                {
                    bucketOffsets[chunkIndex] = runningOffset;
                    bucketWriteHeads[chunkIndex] = runningOffset;
                    runningOffset += bucketCounts[chunkIndex];
                }

                for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                {
                    int bucket = triangleBuckets[triangleIndex];
                    if ((uint)bucket >= (uint)bucketWriteHeads.Length)
                    {
                        ReportVoxelInvalidMeshUpload();
                        volume.DisableColliderChunksForCinematicFake();
                        return false;
                    }

                    int writeHead = bucketWriteHeads[bucket];
                    int triBase = triangleIndex * 3;
                    if (writeHead < 0 ||
                        writeHead > chunkTriangleIndices.Length - 3 ||
                        triBase > triangleIndices.Length - 3)
                    {
                        ReportVoxelInvalidMeshUpload();
                        volume.DisableColliderChunksForCinematicFake();
                        return false;
                    }

                    chunkTriangleIndices[writeHead] = triangleIndices[triBase];
                    chunkTriangleIndices[writeHead + 1] = triangleIndices[triBase + 1];
                    chunkTriangleIndices[writeHead + 2] = triangleIndices[triBase + 2];
                    bucketWriteHeads[bucket] = writeHead + 3;
                }
            }
            finally
            {
                UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
            }
            chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);

            for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                int chunkIndexCount;
                if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
                {
                    ReportVoxelMeshScratchCapacityOverflow();
                    return false;
                }

                try
                {
                    NativeArray<int> bucketCounts = data.ScratchLease.ColliderBucketCounts;
                    chunkIndexCount = bucketCounts.IsCreated && chunkIndex < bucketCounts.Length ? bucketCounts[chunkIndex] : 0;
                }
                finally
                {
                    UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
                }

                BoxCollider chunkProxy = volume.GetColliderChunkBakeProxy(chunkIndex);
                if (chunkProxy == null)
                    return false;

                if (chunkIndexCount <= 0)
                {
                    volume.DisableColliderChunkBakeProxy(chunkIndex);
                    continue;
                }

                ResolveVoxelColliderChunkBakeProxyBounds(
                    chunkIndex,
                    colliderChunkCount,
                    boundsMin,
                    boundsSize,
                    data.VoxelStep,
                    out Vector3 proxyCenter,
                    out Vector3 proxySize);
                volume.ConfigureColliderChunkBakeProxy(chunkIndex, proxyCenter, proxySize);

                // Build the real PhysX mesh for this chunk. The box proxy above stays active
                // until the baked mesh commits through the budgeted deferred upload drain;
                // a null pooled mesh (pool exhausted) leaves the proxy as the degraded route.
                Mesh chunkBakeMesh = volume.GetOrCreateColliderChunkBakeMesh(chunkIndex);
                if (chunkBakeMesh != null)
                {
                    bool meshUploaded = false;
                    if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
                    {
                        ReportVoxelMeshScratchCapacityOverflow();
                        return false;
                    }

                    try
                    {
                        if (TryBuildColliderChunkLocalGeometry(
                                data,
                                useProjectedLocalPositions,
                                chunkIndex,
                                out int localVertexCount,
                                out int localIndexCount))
                        {
                            meshUploaded = UploadColliderMesh(
                                chunkBakeMesh,
                                data.ScratchLease.ColliderLocalPositions,
                                data.ScratchLease.ColliderLocalIndices,
                                localVertexCount,
                                localIndexCount);
                        }
                    }
                    finally
                    {
                        UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
                    }

                    if (meshUploaded)
                    {
                        // PhysX cook off the main thread; the sharedMesh assignment in the
                        // drain then reuses the baked data by mesh entity id.
                        UnityEngine.EntityId bakeMeshEntityId = chunkBakeMesh.GetEntityId();
                        await Awaitable.BackgroundThreadAsync();
                        // Explicit options, not the two-argument default overload: the collider that
                        // receives this mesh sets the identical value, which is the precondition for
                        // PhysX reusing the bake instead of re-cooking on the main thread.
                        Physics.BakeMesh(bakeMeshEntityId, false, Hecton8.Caves.HectonVoxelVolume.VoxelColliderCookingOptions);
                        await Awaitable.MainThreadAsync();
                        if (ct.IsCancellationRequested)
                            return false;

                        if (!volume.AssignColliderChunkBakeMesh(chunkIndex, chunkBakeMesh) ||
                            !EnqueueDeferredVoxelColliderUpload(volume, chunkIndex))
                        {
                            volume.ReleaseColliderChunkBakeMesh(chunkIndex);
                        }
                    }
                    else
                    {
                        volume.ReleaseColliderChunkBakeMesh(chunkIndex);
                    }
                }

                chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            }

            volume.SetActiveColliderChunkCount(colliderChunkCount);
            completed = true;
            return true;
        }
        finally
        {
            if (!completed)
            {
                volume.DisableColliderChunkBakeProxies();
                volume.ClearColliderChunkBakeMeshes();
            }
        }
    }

    /// <summary>
    /// Compacts one collider chunk's triangle range (written by the classify/scatter passes
    /// into ColliderChunkTriangleIndices) into chunk-local vertex/index scratch buffers.
    /// Caller must hold the streaming scratch job lifetime lock. The shared ColliderLocalRemap
    /// scratch is restored to its -1 fill before returning, so chunks can run back to back.
    /// </summary>
    bool TryBuildColliderChunkLocalGeometry(
        VoxelPipelineData data,
        bool useProjectedLocalPositions,
        int chunkIndex,
        out int localVertexCount,
        out int localIndexCount)
    {
        localVertexCount = 0;
        localIndexCount = 0;

        NativeArray<float3> meshLocalPositions = useProjectedLocalPositions
            ? data.ScratchLease.ProjectedLocalPositions
            : data.WeldedPositions;
        NativeArray<int> bucketCounts = data.ScratchLease.ColliderBucketCounts;
        NativeArray<int> bucketOffsets = data.ScratchLease.ColliderBucketOffsets;
        NativeArray<int> chunkTriangleIndices = data.ScratchLease.ColliderChunkTriangleIndices;
        NativeArray<int> localRemap = data.ScratchLease.ColliderLocalRemap;
        NativeArray<int> touchedVertexGlobals = data.ScratchLease.ColliderTouchedVertexGlobals;
        NativeArray<float3> localPositions = data.ScratchLease.ColliderLocalPositions;
        NativeArray<int> localIndices = data.ScratchLease.ColliderLocalIndices;

        if (!meshLocalPositions.IsCreated ||
            !bucketCounts.IsCreated ||
            !bucketOffsets.IsCreated ||
            !chunkTriangleIndices.IsCreated ||
            !localRemap.IsCreated ||
            !touchedVertexGlobals.IsCreated ||
            !localPositions.IsCreated ||
            !localIndices.IsCreated ||
            (uint)chunkIndex >= (uint)bucketCounts.Length ||
            (uint)chunkIndex >= (uint)bucketOffsets.Length)
        {
            ReportVoxelInvalidMeshUpload();
            return false;
        }

        int indexStart = bucketOffsets[chunkIndex];
        int indexCount = bucketCounts[chunkIndex];
        if (indexCount <= 0 ||
            (indexCount % 3) != 0 ||
            indexStart < 0 ||
            indexStart > chunkTriangleIndices.Length - indexCount ||
            indexCount > localIndices.Length)
        {
            ReportVoxelInvalidMeshUpload();
            return false;
        }

        int weldedCount = data.WeldedCount;
        bool valid = true;
        int vertexCursor = 0;
        for (int i = 0; i < indexCount; i++)
        {
            int globalIndex = chunkTriangleIndices[indexStart + i];
            if ((uint)globalIndex >= (uint)weldedCount ||
                (uint)globalIndex >= (uint)localRemap.Length ||
                (uint)globalIndex >= (uint)meshLocalPositions.Length)
            {
                valid = false;
                break;
            }

            int localIndex = localRemap[globalIndex];
            if (localIndex < 0)
            {
                if (vertexCursor >= localPositions.Length || vertexCursor >= touchedVertexGlobals.Length)
                {
                    valid = false;
                    break;
                }

                localIndex = vertexCursor;
                localRemap[globalIndex] = localIndex;
                touchedVertexGlobals[vertexCursor] = globalIndex;
                localPositions[vertexCursor] = meshLocalPositions[globalIndex];
                vertexCursor++;
            }

            localIndices[i] = localIndex;
        }

        // The remap scratch is shared by every chunk of this volume build; restore the -1
        // fill for the next chunk whether or not this one succeeded.
        for (int i = 0; i < vertexCursor; i++)
            localRemap[touchedVertexGlobals[i]] = -1;

        if (!valid || vertexCursor < 3)
        {
            ReportVoxelInvalidMeshUpload();
            return false;
        }

        localVertexCount = vertexCursor;
        localIndexCount = indexCount;
        return true;
    }

    void PrepareVolumeForBuild(GameObject go)
    {
        if (go == null)
            return;

        HectonVoxelVolume volume = ResolvePooledVoxelVolume(go);
        if (volume != null)
            volume.PrepareForReuse();

        MeshRenderer mr = ResolvePooledMeshRenderer(go, volume);
        if (mr != null)
            mr.enabled = false;

        MeshCollider mcol = ResolvePooledMeshCollider(go, volume);
        if (mcol != null)
        {
            mcol.enabled = false;
        }

        BoxCollider bakeProxy = ResolvePooledBoxCollider(go);
        if (bakeProxy != null)
            bakeProxy.enabled = false;

        if (!Application.isPlaying)
        {
            Transform bakeProxyTransform = go.transform.Find(VoxelBakeProxyRuntimeName);
            if (bakeProxyTransform != null && bakeProxyTransform.TryGetComponent(out BoxCollider isolatedProxy))
                isolatedProxy.enabled = false;
        }
    }

    bool TryBindGeneratedVolumeForMeshPublication(GameObject go, VoxelPipelineData data)
    {
        if (go == null || data == null)
            return false;

        HectonVoxelVolume volume = ResolvePooledVoxelVolume(go);
        if (volume == null)
            return false;

        data.SourceVolume = volume;
        data.SourceRuntimeStamp = volume.RuntimeStamp;
        return true;
    }

    async Awaitable<bool> ConfigureVolumeRuntimeDataFromPipelineAsync(
        GameObject go,
        uint seed,
        Vector3 worldCenter,
        double3 absoluteUniverseOffsetDouble,
        CavePreset preset,
        int gridDimension,
        float voxelSize,
        int lodLevel,
        CaveGenerationParams caveParams,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        VoxelPipelineData data,
        int ptsX,
        int ptsY,
        int ptsZ,
        Vector3 volumeOrigin,
        float voxelStep,
        bool buildCollider,
        CancellationToken ct)
    {
        long totalPointCountLong = (long)ptsX * ptsY * ptsZ;
        if (data == null ||
            totalPointCountLong <= 0L ||
            totalPointCountLong > StreamingPointScratchMax)
        {
            return false;
        }

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            NativeArray<float> smoothDensityField = data.ScratchLease.SmoothDensityField;
            if (!smoothDensityField.IsCreated ||
                smoothDensityField.Length < (int)totalPointCountLong)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            return await ConfigureVolumeRuntimeDataAsync(
                go,
                seed,
                worldCenter,
                absoluteUniverseOffsetDouble,
                preset,
                gridDimension,
                voxelSize,
                lodLevel,
                caveParams,
                nodes,
                tunnels,
                entrances,
                structures,
                smoothDensityField,
                ptsX,
                ptsY,
                ptsZ,
                volumeOrigin,
                voxelStep,
                buildCollider,
                ct);
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }
    }

    async Awaitable<bool> ConfigureVolumeRuntimeDataAsync(
        GameObject go,
        uint seed,
        Vector3 worldCenter,
        double3 absoluteUniverseOffsetDouble,
        CavePreset preset,
        int gridDimension,
        float voxelSize,
        int lodLevel,
        CaveGenerationParams caveParams,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        NativeArray<float> smoothDensityField,
        int ptsX,
        int ptsY,
        int ptsZ,
        Vector3 volumeOrigin,
        float voxelStep,
        bool buildCollider,
        CancellationToken ct)
    {
        if (go == null)
            return false;

        HectonVoxelVolume volume = ResolvePooledVoxelVolume(go);
        if (volume == null)
            return false;

        volume.ConfigureRuntimeData(
            this,
            seed,
            worldCenter,
            Vector3.zero,
            absoluteUniverseOffsetDouble,
            preset,
            gridDimension,
            voxelSize,
            lodLevel,
            caveParams,
            nodes,
            tunnels,
            entrances,
            structures,
            buildCollider);

        return await volume.PublishSonarSdfSnapshotAsync(
            new Vector3Int(ptsX, ptsY, ptsZ),
            volumeOrigin,
            Vector3.one * voxelStep,
            smoothDensityField,
            ct);
    }

    void RegisterEntranceTerrainHoles(
        GameObject go,
        NativeArray<CaveEntrance> entrances,
        float voxelSize,
        double3 capturedTotalOffset,
        double3 committedTotalOffset)
    {
        HectonMapMagicVegetationBridge vegetationBridge = _vegetationBridge;
        if (vegetationBridge == null || go == null || !entrances.IsCreated || entrances.Length <= 0)
            return;

        HectonVoxelVolume volume = ResolvePooledVoxelVolume(go);
        if (volume == null)
            return;

        float holePadding = math.max(voxelSize * 1.5f, 1f);
        for (int i = 0; i < entrances.Length; i++)
        {
            CaveEntrance entrance = entrances[i];
            if (!TryResolveSafeEntranceTerrainHole(
                    in entrance,
                    holePadding,
                    capturedTotalOffset,
                    committedTotalOffset,
                    out Vector3 runtimeSurfacePosition,
                    out float radius))
            {
                continue;
            }

            int holeHandle = vegetationBridge.RegisterTerrainHoleHandle(runtimeSurfacePosition, radius);
            volume.TrackTerrainHoleHandle(holeHandle);
        }
    }

    void RegisterPipelineSpawnPoints(
        Vector3 worldCenter,
        SpawnContext caveContext,
        NativeArray<CaveSpawnData> spawnPointList,
        int spawnPointCount,
        double3 capturedTotalOffset,
        double3 committedTotalOffset)
    {
        ScavengePopulator scavengePopulator = _scavengePopulator;
        if (!spawnPointList.IsCreated ||
            spawnPointList.Length <= 0 ||
            spawnPointCount <= 0 ||
            scavengePopulator == null ||
            !IsFiniteVector(worldCenter) ||
            !math.all(math.isfinite(capturedTotalOffset)) ||
            !math.all(math.isfinite(committedTotalOffset)))
        {
            return;
        }

        double3 absoluteUniverseCenter = global::Hecton8.World.AUPMath.ToDouble3(worldCenter) + capturedTotalOffset;
        if (!math.all(math.isfinite(absoluteUniverseCenter)))
            return;

        float tileSize = ClampRuntimeFinite(
            mapMagicTileSize,
            999f,
            MinRuntimeMapMagicTileSize,
            MaxRuntimeMapMagicTileSize);
        if (!TryResolveSafeSpawnChunkCoordinate(absoluteUniverseCenter, tileSize, out Vector2Int chunkCoord))
            return;

        int safeSpawnPointCount = math.min(spawnPointCount, spawnPointList.Length);
        for (int sp = 0; sp < safeSpawnPointCount; sp++)
        {
            CaveSpawnData spawnData = spawnPointList[sp];
            double3 runtimeSpawnDelta = global::Hecton8.World.AUPMath.ToDouble3(spawnData.position) + capturedTotalOffset - committedTotalOffset;
            if (!TryResolveLocalDeltaFloat3(runtimeSpawnDelta, out float3 runtimeSpawnFloat))
                continue;

            Vector3 runtimeSpawnPosition = new Vector3(runtimeSpawnFloat.x, runtimeSpawnFloat.y, runtimeSpawnFloat.z);
            scavengePopulator.RegisterSpawnPoint(
                runtimeSpawnPosition,
                Quaternion.identity,
                Vector3.one,
                chunkCoord,
                spawnData.hashId,
                caveContext);
        }
    }

    private static bool TryResolveSafeSpawnChunkCoordinate(double3 absoluteUniverseCenter, float tileSize, out Vector2Int chunkCoord)
    {
        chunkCoord = default;
        if (!math.all(math.isfinite(absoluteUniverseCenter)) || !math.isfinite(tileSize) || tileSize <= 0f)
            return false;

        double invTileSize = 1.0d / tileSize;
        double chunkX = math.floor(absoluteUniverseCenter.x * invTileSize);
        double chunkZ = math.floor(absoluteUniverseCenter.z * invTileSize);
        if (!math.isfinite(chunkX) || !math.isfinite(chunkZ))
            return false;

        chunkCoord = new Vector2Int(
            (int)math.clamp(chunkX, (double)int.MinValue, (double)int.MaxValue),
            (int)math.clamp(chunkZ, (double)int.MinValue, (double)int.MaxValue));
        return true;
    }

#if UNITY_EDITOR
    internal static bool TryRunCleanRoomDensityPreview(
        int gridDimension,
        float voxelStep,
        Vector3 worldCenter,
        float constantTerrainHeight,
        uint seed,
        NativeArray<float> density,
        NativeArray<float> smoothDensity,
        NativeArray<int> faultFlags)
    {
        int gridDim = math.clamp(gridDimension, 16, 128);
        int pts = gridDim + 1;
        int total = pts * pts * pts;
        if (!density.IsCreated || !smoothDensity.IsCreated || density.Length < total || smoothDensity.Length < total)
            return false;

        NativeArray<float> terrainHeights = new NativeArray<float>(pts * pts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> gridBiome = new NativeArray<float>(pts * pts, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        try
        {
            for (int i = 0; i < terrainHeights.Length; i++)
                terrainHeights[i] = constantTerrainHeight;

            float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
            float3 origin = (float3)worldCenter - actualSize * 0.5f;
            var handle = new VoxelDensityJob
            {
                ptsX = pts,
                ptsY = pts,
                ptsZ = pts,
                volumeOrigin = origin,
                voxelStep = voxelStep,
                terrainHeights = terrainHeights,
                gridBiome = gridBiome,
                caveNodes = default,
                caveTunnels = default,
                caveEntrances = default,
                caveStructures = default,
                craterStamps = default,
                modifiedCells = default,
                modifiedCellBucketHeads = default,
                modifiedCellNext = default,
                nodeBucketOffsets = default,
                nodeBucketIndices = default,
                tunnelBucketOffsets = default,
                tunnelBucketIndices = default,
                density = density,
                smoothDensity = smoothDensity,
                densityFaultFlags = faultFlags,
                caveParams = default,
                absoluteNoiseOffset = float3.zero,
                absoluteCellOffset = double3.zero,
                partitionDimX = 1,
                partitionDimY = 1,
                partitionDimZ = 1,
                partitionOrigin = origin,
                partitionInvCellSize = new float3(1f / math.max(gridDim * voxelStep, 0.01f)),
                sealMargin = 0f,
                lodLevel = 0,
                lodTransitionBand = 0f,
                enableBiomeSdfModifiers = 0,
                PrimaryFrequency = 0.012f,
                SecondaryFrequency = 0.017f,
                CarveStrengthMeters = 28.0f,
                CaveThreshold = 0.65f,
                MaxCrustDepthMeters = 400.0f,
                SurfaceProtectionMeters = 30.0f,
                StrataLayerThicknessMeters = 24.0f,
                StrataShelvingStrength = 0.4f,
                WorldSeed = seed
            }.Schedule(total, JOB_BATCH);
            handle.Complete();
            return true;
        }
        finally
        {
            if (terrainHeights.IsCreated) terrainHeights.Dispose();
            if (gridBiome.IsCreated) gridBiome.Dispose();
        }
    }
#endif

    bool TryRegisterPipelineSpawnPointsFromScratch(
        VoxelPipelineData data,
        Vector3 worldCenter,
        SpawnContext caveContext,
        double3 capturedTotalOffset,
        double3 committedTotalOffset)
    {
        if (data == null || !data.ExtractSpawnPoints)
            return true;

        if (!TryLockStreamingScratchJobLifetime(ref data.ScratchLease))
        {
            ReportVoxelMeshScratchCapacityOverflow();
            return false;
        }

        try
        {
            NativeArray<CaveSpawnData> spawnPointList = data.SpawnPointList;
            NativeArray<int> spawnPointCountBuffer = data.SpawnPointCountBuffer;
            int spawnPointCount = spawnPointCountBuffer.IsCreated && spawnPointCountBuffer.Length > 0
                ? spawnPointCountBuffer[0]
                : 0;
            RegisterPipelineSpawnPoints(
                worldCenter,
                caveContext,
                spawnPointList,
                spawnPointCount,
                capturedTotalOffset,
                committedTotalOffset);
            return true;
        }
        finally
        {
            UnlockStreamingScratchJobLifetime(ref data.ScratchLease);
        }
    }

    void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(obj);
        else Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    // ╔═══════════════════════════════════════════════╗
    // ║                GIZMOS                         ║
    // ╚═══════════════════════════════════════════════╝

#if UNITY_EDITOR
    public static bool ValidateAgent1315EnginePrivateLayouts(ref uint failureFlags)
    {
        bool ok = true;
        ok &= AssertAgent1304ExplicitLayout<MCRawVertex>(24, ref failureFlags);
        ok &= AssertAgent1304Offset<MCRawVertex>(nameof(MCRawVertex.edgeId), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<MCRawVertex>(nameof(MCRawVertex.localPosition), 8, ref failureFlags);
        ok &= AssertAgent1304Offset<MCRawVertex>("_pad0", 20, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<AirPocketEntry>(32, ref failureFlags);
        ok &= AssertAgent1304Offset<AirPocketEntry>(nameof(AirPocketEntry.Center), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<AirPocketEntry>(nameof(AirPocketEntry.HalfExtents), 12, ref failureFlags);
        ok &= AssertAgent1304Offset<AirPocketEntry>(nameof(AirPocketEntry.OxygenRefillFraction), 24, ref failureFlags);
        ok &= AssertAgent1304Offset<AirPocketEntry>("_pad0", 28, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<ActiveVolumeLocalBoundsEntry>(24, ref failureFlags);
        ok &= AssertAgent1304Offset<ActiveVolumeLocalBoundsEntry>(nameof(ActiveVolumeLocalBoundsEntry.Center), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<ActiveVolumeLocalBoundsEntry>(nameof(ActiveVolumeLocalBoundsEntry.Size), 12, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<VoxelModifiedCell>(8, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Density), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Reserved), 2, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Reserved1), 4, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCell>(nameof(VoxelModifiedCell.MaterialId), 6, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCell>(nameof(VoxelModifiedCell.Flags), 7, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<VoxelModifiedCellEntry>(24, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCellEntry>(nameof(VoxelModifiedCellEntry.AbsoluteCell), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCellEntry>(nameof(VoxelModifiedCellEntry.Cell), 12, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelModifiedCellEntry>("_pad0", 20, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<VoxelMeshPipelineTelemetryEntry>(64, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.TimestampTicks), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Frame), 8, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Flags), 12, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.BufferId), 16, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.SystemId), 20, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Generation), 24, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.StateHash), 28, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.VaultGenerationId), 32, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.ChunksMeshedThisFrame), 36, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.BakeQueueLength), 38, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.ColliderUploadQueueLength), 40, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.ActiveGenerationOperations), 42, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.SurfacePoolInUse), 44, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.PhysicsPoolInUse), 46, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Padding0), 48, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Padding1), 52, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Padding2), 56, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelMeshPipelineTelemetryEntry>(nameof(VoxelMeshPipelineTelemetryEntry.Padding3), 60, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<VoxelSurfaceVertex>(80, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>(nameof(VoxelSurfaceVertex.Position), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>(nameof(VoxelSurfaceVertex.Normal), 12, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>(nameof(VoxelSurfaceVertex.Color), 24, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>(nameof(VoxelSurfaceVertex.BakedOcclusionUv1), 28, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>(nameof(VoxelSurfaceVertex.DirtyBlendUv2), 44, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>(nameof(VoxelSurfaceVertex.RuntimePositionWS), 60, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelSurfaceVertex>("_pad0", 76, ref failureFlags);

        ok &= AssertAgent1304ExplicitLayout<VoxelColliderVertex>(16, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelColliderVertex>(nameof(VoxelColliderVertex.Position), 0, ref failureFlags);
        ok &= AssertAgent1304Offset<VoxelColliderVertex>("_pad0", 12, ref failureFlags);
        return ok;
    }

    public static bool ValidateAgent1304EnginePrivateLayouts(ref uint failureFlags)
    {
        return ValidateAgent1315EnginePrivateLayouts(ref failureFlags);
    }

    private static bool AssertAgent1304ExplicitLayout<T>(int expectedSize, ref uint failureFlags)
        where T : struct
    {
        StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
        int observedSize = UnsafeUtility.SizeOf<T>();
        bool ok = layout != null &&
                  layout.Value == LayoutKind.Explicit &&
                  observedSize == expectedSize &&
                  (observedSize & 7) == 0;
        if (!ok)
            failureFlags |= 1u;

        return ok;
    }

    private static bool AssertAgent1304Offset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
        where T : struct
    {
        System.Reflection.FieldInfo field = typeof(T).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        int observedOffset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        bool ok = observedOffset == expectedOffset;
        if (!ok)
            failureFlags |= 1u;

        return ok;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            if (activeVolume == null)
                continue;

            Bounds localBounds = i < _activeVolumeLocalBounds.Length
                ? _activeVolumeLocalBounds[i].ToBounds()
                : new Bounds(Vector3.zero, Vector3.one);

            if (localBounds.size.sqrMagnitude <= 0.0001f)
                localBounds = new Bounds(Vector3.zero, Vector3.one);

            Gizmos.matrix = activeVolume.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }

        Gizmos.matrix = previousMatrix;
    }
#endif

        #region JulesLink_VoxelSdfBooleanSubtraction
        private static void JulesLink_VoxelSdfBooleanSubtraction() { _ = typeof(Hecton8.PureLogic.Systems.VoxelSdfBooleanSubtraction); }
        #endregion

        #region JulesLink_CeilingConcavityAirPocketVolumeCalculator
        private static void JulesLink_CeilingConcavityAirPocketVolumeCalculator() { _ = typeof(Hecton8.PureLogic.Systems.CeilingConcavityAirPocketVolumeCalculator); }
        #endregion
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  CUSTOM EDITOR (v4.0)
// ════════════════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR

[CustomEditor(typeof(HectonVoxelEngine))]
public class HectonVoxelEngineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HectonVoxelEngine engine = (HectonVoxelEngine)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                $"═══ CAVE VOXEL ENGINE v4.0 ═══\n" +
                $"Active Volumes: {engine.ActiveVolumeCount}\n" +
                $"MC Tables: {(MCTables.IsReady ? "Ready" : "Not Init")}\n" +
                $"Height Source: MapMagicBridge\n" +
                $"SDF: Multi-Primitive + Smooth Blend\n" +
                $"Async: Unity 6 Awaitable (Zero GC)",
                MessageType.Info);
        }

        CavePreset preset = engine.defaultPreset ?? new CavePreset();
        int dim = preset.gridDimension;
        float vox = preset.voxelSize;
        float coverage = dim * vox;

        float maxPts = (dim + 1f) * (dim + 1f) * (dim + 1f);
        float maxCells = (float)dim * dim * dim;
        float densityMB = maxPts * 4f / (1024f * 1024f);
        const int MC_BUFFER_MULTIPLIER = 2;
        float rawMB = maxCells * MC_BUFFER_MULTIPLIER * 20f / (1024f * 1024f);
        float weldMapMB = maxCells * MC_BUFFER_MULTIPLIER * 12f / (1024f * 1024f);
        float totalMB = densityMB + rawMB + weldMapMB;

        EditorGUILayout.HelpBox(
            $"═══ CURRENT PRESET: {preset.presetName} ═══\n" +
            $"Grid: {dim}³ | Voxel: {vox}m | Coverage: {coverage:F0}m\n" +
            $"Rooms: {preset.minRooms}-{preset.maxRooms}\n" +
            $"Density: {densityMB:F1} MB | MC Buffer: {rawMB:F1} MB\n" +
            $"Peak temp: {totalMB:F1} MB (freed after gen)\n" +
            "MC Buffer: two-pass exact extraction (no truncation)",
            totalMB > 100f ? MessageType.Warning : MessageType.None);

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
        if (GUILayout.Button("✕  Clear All Volumes", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(engine.gameObject, "Clear Caves");
            engine.ClearAllVolumes();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif
