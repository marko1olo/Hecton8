// =====================================================================
// MECHANICAL SPLIT from HectonVoxelEngine.cs — Slice A (no logic change)
// Date: 2026-08-03 — architecture god-object reduction
// Original single-file owner retained behavioral authority in HectonVoxelEngine
// =====================================================================

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
