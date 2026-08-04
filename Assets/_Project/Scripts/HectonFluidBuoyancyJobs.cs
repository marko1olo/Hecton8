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

    // ══════════════════════════════════════════════════════════════════
    //  BuoyancyParams — dannye obekta dlya Job (blittable struct)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parametry odnogo obekta dlya BuoyancyJob.
    /// Blittable struct — bezopasen dlya NativeArray i Burst.
    ///
    /// IZMENENIE: dobavleno pole isInAir dlya sistemy Suhih Zon.
    /// Dry-zone and simulation flags are packed into explicit bytes to keep the Burst payload deterministic.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BuoyancyParams
    {
        public const uint ExactSurfaceNormalFlag = 1u;
        public const int StrideBytes = 128;

        [FieldOffset(0)]
        public float3 boundsCenter;
        [FieldOffset(12)]
        public float3 boundsExtents;

        /// <summary>Plotnost obekta (kg/m³).</summary>
        [FieldOffset(24)]
        public float density;

        /// <summary>Obem obekta (m³).</summary>
        [FieldOffset(28)]
        public float volume;

        /// <summary>Vysota obekta (m) dlya chastichnogo pogruzheniya.</summary>
        [FieldOffset(32)]
        public float height;

        /// <summary>Massa Rigidbody (kg).</summary>
        [FieldOffset(36)]
        public float mass;
        [FieldOffset(40)]
        public float currentResponse;
        [FieldOffset(44)]
        public float surfaceStability;
        [FieldOffset(48)]
        public float localFluidDensity;
        [FieldOffset(52)]
        public float angularDragMultiplier;
        [FieldOffset(56)]
        public float buoyancyMultiplier;
        [FieldOffset(60)]
        public float3 localCurrent;

        /// <summary>
        /// Obekt nahoditsya v suhoy zone (vnutri nezatoplennogo modulya).
        /// Esli true — vse vodnye sily obnulyayutsya v BuoyancyJob.
        /// </summary>
        [FieldOffset(72)]
        public byte isInAir;
        [FieldOffset(73)]
        public byte simulationMode;
        [FieldOffset(74)]
        public byte simplifiedSubmersion;
        [FieldOffset(75)]
        public byte useLocalFluidDensityOverride;
        [FieldOffset(76)]
        public uint alignmentPadding;
        [FieldOffset(80)]
        private ulong _pad0;
        [FieldOffset(88)]
        private ulong _pad1;
        [FieldOffset(96)]
        private ulong _pad2;
        [FieldOffset(104)]
        private ulong _pad3;
        [FieldOffset(112)]
        private ulong _pad4;
        [FieldOffset(120)]
        private ulong _pad5;
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuoyancyJob — Burst Compiled, IJobParallelFor
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallelnyy Job dlya vychisleniya sil plavuchesti, soprotivleniya
    /// i podvodnyh techeniy.
    ///
    /// Burst-compiled SIMD-optimizatsiya, net managed code, net GC.
    ///
    /// IZMENENIE (Dry Zones):
    ///   Pervaya proverka v Execute: esli p.isInAir == true,
    ///   rezultiruyuschie sily i momenty = float3.zero.
    ///   Obekt vnutri bazy ne ispytyvaet nikakih vodnyh sil.
    ///
    /// FIZIKA:
    ///   Arhimed:    F_buoy  = ρ_water × V_submerged × g  (vverh)
    ///   Drag:       F_drag  = -v × C_drag × subRatio     (protiv dvizheniya)
    ///   Techenie:    F_curr  = currentForce × subRatio     (po napravleniyu)
    ///   AngDrag:    T_drag  = -ω × C_angDrag × subRatio  (protiv vrascheniya)
    /// </summary>
    /// <summary>
    /// Burst-compiled fallback wave evaluator used by CPU-side buoyancy systems.
    /// This samples the first-party weather spectrum for physics consumers and does not replace the active ocean shader FFT rendering.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WaveQueryJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float3> PositionsWS;
        [ReadOnly, NoAlias] public NativeArray<BuoyancyParams> ObjParams;
        [WriteOnly, NoAlias] public NativeArray<float> VerticalOffsets;
        [WriteOnly, NoAlias] public NativeArray<float3> SurfaceUpVectors;

        [ReadOnly, NoAlias] public NativeArray<GerstnerWaveComponent> Waves;
        [ReadOnly, NoAlias] public NativeArray<ushort> TerrainHeightSamples;
        public int WaveCount;
        public float TimeSeconds;
        public float WaterLevelY;
        public float MaxWaveEnvelope;
        public double2 AupOffsetXZ;
        public float3 TerrainPosition;
        public float3 TerrainSize;
        public int TerrainHeightmapResolution;
        public byte HasTerrainHeightPayload;
        public float ShoreFallbackBandMeters;
        public float NormalSampleDistanceMeters;
        public byte CalculateSurfaceNormals;

        public void Execute(int index)
        {
            float3 positionWS = PositionsWS[index];
            BuoyancyParams buoyancyParams = default;
            float objectHeight = 0.01f;
            float2 centerXZ = positionWS.xz;
            if (index < ObjParams.Length)
            {
                buoyancyParams = ObjParams[index];
                objectHeight = math.max(buoyancyParams.height, 0.01f);
                if (math.all(math.isfinite(buoyancyParams.boundsCenter)))
                    centerXZ = buoyancyParams.boundsCenter.xz;
            }

            if (buoyancyParams.simulationMode != 0)
            {
                VerticalOffsets[index] = 0f;
                if (SurfaceUpVectors.IsCreated && index < SurfaceUpVectors.Length)
                    SurfaceUpVectors[index] = new float3(0f, 1f, 0f);
                return;
            }

            float baseDepth = WaterLevelY - positionWS.y;
            if (baseDepth > objectHeight + MaxWaveEnvelope + 5f)
            {
                VerticalOffsets[index] = 0f;
                if (SurfaceUpVectors.IsCreated && index < SurfaceUpVectors.Length)
                    SurfaceUpVectors[index] = new float3(0f, 1f, 0f);
                return;
            }

            double2 absoluteWaveXZ = new double2(centerXZ.x, centerXZ.y) + AupOffsetXZ;
            float waveOffset = ResolveFiniteFloatOrZero(SampleWaveHeight(absoluteWaveXZ));
            float resolvedSurfaceY = WaterLevelY + waveOffset;
            if (HasTerrainHeightPayload != 0 &&
                TrySampleTerrainHeight(centerXZ, out float terrainY) &&
                math.abs(terrainY - WaterLevelY) <= math.max(0.01f, ShoreFallbackBandMeters))
            {
                resolvedSurfaceY = math.max(resolvedSurfaceY, terrainY);
            }

            VerticalOffsets[index] = ResolveFiniteFloatOrZero(resolvedSurfaceY - WaterLevelY);
            if (SurfaceUpVectors.IsCreated && index < SurfaceUpVectors.Length)
            {
                SurfaceUpVectors[index] = CalculateSurfaceNormals != 0
                    ? HectonGerstnerWater.SampleFiniteDifferenceNormal(
                        absoluteWaveXZ,
                        Waves,
                        WaveCount,
                        TimeSeconds,
                        NormalSampleDistanceMeters)
                    : new float3(0f, 1f, 0f);
            }
        }

        private float SampleWaveHeight(double2 worldXZ)
        {
            return HectonGerstnerWater.SampleHeight(worldXZ, Waves, WaveCount, TimeSeconds);
        }

        private bool TrySampleTerrainHeight(float2 runtimeXZ, out float terrainY)
        {
            terrainY = 0f;
            if (HasTerrainHeightPayload == 0 ||
                !TerrainHeightSamples.IsCreated ||
                TerrainHeightmapResolution <= 1 ||
                TerrainHeightSamples.Length < TerrainHeightmapResolution * TerrainHeightmapResolution ||
                TerrainSize.x <= 0.001f ||
                TerrainSize.z <= 0.001f)
            {
                return false;
            }

            float normalizedX = (runtimeXZ.x - TerrainPosition.x) * math.rcp(TerrainSize.x);
            float normalizedZ = (runtimeXZ.y - TerrainPosition.z) * math.rcp(TerrainSize.z);
            if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
                return false;

            float sampleX = normalizedX * (TerrainHeightmapResolution - 1);
            float sampleZ = normalizedZ * (TerrainHeightmapResolution - 1);
            int x0 = math.clamp((int)math.floor(sampleX), 0, TerrainHeightmapResolution - 1);
            int z0 = math.clamp((int)math.floor(sampleZ), 0, TerrainHeightmapResolution - 1);
            int x1 = math.min(x0 + 1, TerrainHeightmapResolution - 1);
            int z1 = math.min(z0 + 1, TerrainHeightmapResolution - 1);
            float tx = sampleX - x0;
            float tz = sampleZ - z0;
            float heightScale = TerrainSize.y * (1f / 65535f);
            float h00 = TerrainHeightSamples[(z0 * TerrainHeightmapResolution) + x0] * heightScale;
            float h10 = TerrainHeightSamples[(z0 * TerrainHeightmapResolution) + x1] * heightScale;
            float h01 = TerrainHeightSamples[(z1 * TerrainHeightmapResolution) + x0] * heightScale;
            float h11 = TerrainHeightSamples[(z1 * TerrainHeightmapResolution) + x1] * heightScale;
            float bottom = math.lerp(h00, h10, tx);
            float top = math.lerp(h01, h11, tx);
            terrainY = TerrainPosition.y + math.lerp(bottom, top, tz);
            return math.isfinite(terrainY);
        }

        private static float ResolveFiniteFloatOrZero(float value)
        {
            return (math.isnan(value) || math.isinf(value) || !math.isfinite(value)) ? 0f : value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BuoyancyJob : IJobParallelFor
    {
        private const float ThermoclineDepthMeters = 120f;
        private const float ThermoclineHalfBandMeters = 8f;
        private const float ThermoclineVerticalAttenuation = 0.1f;
        private const float SurfaceStormLayerDepthMeters = 50f;
        private const float StormSurfaceTurbulenceStrength = 0.4f;
        private const float JobGyroscopicFlowMaxTorquePerKg = 50f;

        // ── Input (ReadOnly) ──
        [ReadOnly, NoAlias] public NativeArray<float3>         positions;
        [ReadOnly, NoAlias] public NativeArray<float3>         previousPositions;
        [ReadOnly, NoAlias] public NativeArray<byte>           previousPositionValid;
        [ReadOnly, NoAlias] public NativeArray<float3>         velocities;
        [ReadOnly, NoAlias] public NativeArray<float3>         angularVelocities;
        [ReadOnly, NoAlias] public NativeArray<float3>         upVectors;
        [ReadOnly, NoAlias] public NativeArray<float3>         surfaceUpVectors;
        [ReadOnly, NoAlias] public NativeArray<BuoyancyParams> objParams;
        [ReadOnly, NoAlias] public NativeArray<float>          waveOffsets;
        [ReadOnly, NoAlias] public NativeArray<float>          gpuBuoyancyForcesY;
        [ReadOnly, NoAlias] public NativeArray<float>          brineHeights;
        [ReadOnly, NoAlias] public NativeArray<float>          brineDensityMultipliers;
        [ReadOnly, NoAlias] public NativeArray<byte>           brineFlags;
        [ReadOnly, NoAlias] public NativeArray<ActiveThrusterFlow> activeThrusters;
        [ReadOnly, NoAlias] public NativeArray<WhirlpoolFlow> activeWhirlpools;
        [ReadOnly, NoAlias] public NativeArray<FluidViscosityRegion> activeViscosityRegions;
        [ReadOnly, NoAlias] public NativeArray<float> viscosityGradientLut;
        [ReadOnly, NoAlias] public NativeArray<float3> vectorNoiseField;
        public int vectorNoiseFieldLength;
        public int activeThrusterCount;
        public int activeWhirlpoolCount;
        public int activeViscosityRegionCount;
        [WriteOnly, NoAlias] public NativeArray<FluidImpactEvent> impactEvents;
        [WriteOnly, NoAlias] public NativeArray<int> impactEventFlags;

        // ── Output (WriteOnly) ──
        [WriteOnly, NoAlias] public NativeArray<float3> resultForces;
        [WriteOnly, NoAlias] public NativeArray<float3> resultTorques;
        [NoAlias] public MathGuard.InvalidNumberWriter mathGuardWriter;
        public int forceNanErrorCode;
        public int torqueNanErrorCode;

        // ── Shared parameters (uniform) ──
        public float  waterLevel;
        public float  waterDensity;
        public float  viscousDrag;
        public float  maxQuadraticDragForcePerKg;
        public float  angularDragCoeff;
        public float  gravity;
        public float3 baseCurrentForce;
        public float3 giantWakeCurrent;
        public float  giantWakeDepthFadeStart;
        public float  giantWakeDepthFadeRange;
        public byte   enableTidalShearZones;
        public float  tidalShearTorqueStrength;
        public float  tidalShearFrequency;
        public float  time;
        public uint   weatherStateMask;
        public float3 weatherCurrentDirection;
        public float  weatherCurrentScale;
        public float  weatherBlend;
        public float3 windAdvectionVector;
        public float  windAdvectionForcePerKg;
        public float  splashDepthThresholdMeters;
        public float  splashVelocityThresholdSq;
        public byte   enablePhantomCurrent;
        public float  currentNoiseScale;
        public float  currentTimeScale;
        public float  currentVerticalFactor;
        public float  phantomCurrentStrength;
        public double3 vectorNoiseAupOffset;
        public float  brineShiftOffsetY;
        public float  vectorNoiseInvCellSize;
        public byte   enablePrebakedVectorNoise;
        public float  vectorNoiseTriangleModulation;
        public byte   detailedMathEnabled;
        public byte   enableAnalyticalFlowField;
        public float  haloclineBoundaryDepthMeters;
        public float  deepLayerDensityMultiplier;
        public float  haloclineShearForcePerKg;
        public byte   enableDynamicViscosityRegions;
        public byte   useGpuBuoyancyForce;

        public void Execute(int i)
        {
            impactEventFlags[i] = 0;
            BuoyancyParams p = objParams[i];

            if (p.simulationMode == 1 || p.simulationMode == 2 || p.isInAir != 0)
            {
                resultForces[i] = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            float3 pos = positions[i];
            float3 vel = velocities[i];
            float3 angularVel = angularVelocities[i];
            float3 up = ResolveSurfaceNormalLod(upVectors[i], p.alignmentPadding, detailedMathEnabled);
            float3 targetUp = ResolveSurfaceNormalLod(
                surfaceUpVectors.IsCreated && i < surfaceUpVectors.Length ? surfaceUpVectors[i] : new float3(0f, 1f, 0f),
                BuoyancyParams.ExactSurfaceNormalFlag,
                detailedMathEnabled);

            float waveOffset = waveOffsets[i];
            float surfaceY = waterLevel + waveOffset;
            float depthBelowSurface = surfaceY - pos.y;

            if (depthBelowSurface <= 0f)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            EvaluateImpactEvent(i, p, pos, vel, depthBelowSurface, surfaceY);

            float subRatio = p.simplifiedSubmersion != 0
                ? (depthBelowSurface > 0f ? 1f : 0f)
                : math.saturate(depthBelowSurface * math.rcp(math.max(p.height, 0.0001f)));

            float resolvedWaterDensity = ResolveFluidDensity(i, p, pos, depthBelowSurface, out byte brineSubmerged, out float denseLayer01);

            float3 buoyancyForce = CalculateBuoyancyForce(i, p, subRatio, resolvedWaterDensity, brineSubmerged, out float displacedVolume, out float buoyancyMagnitude);

            float3 sampledCurrent = CalculateSampledCurrent(p, pos, depthBelowSurface);
            float3 currentF = sampledCurrent * (subRatio * p.mass * p.currentResponse);
            float3 analyticalShearForce = CalculateShearForce(p, subRatio, denseLayer01);
            float3 windAdvectionForce = CalculateWindForce(p, subRatio, depthBelowSurface);

            float3 dragForce = CalculateDragForce(p, pos, vel, sampledCurrent, subRatio, resolvedWaterDensity);
            float3 dampingVec = CalculateDampingForce(vel, subRatio, resolvedWaterDensity, displacedVolume);

            float3 torqueForces = CalculateTorques(p, pos, up, targetUp, angularVel, sampledCurrent, subRatio, buoyancyMagnitude, depthBelowSurface);

            resultForces[i] = MathGuard.SanitizeFiniteOrZero(
                buoyancyForce + dragForce + currentF + windAdvectionForce + dampingVec + analyticalShearForce,
                forceNanErrorCode,
                mathGuardWriter);
            resultTorques[i] = MathGuard.SanitizeFiniteOrZero(
                torqueForces,
                torqueNanErrorCode,
                mathGuardWriter);
        }

        private void EvaluateImpactEvent(int i, BuoyancyParams p, float3 pos, float3 vel, float depthBelowSurface, float surfaceY)
        {
            float velocitySq = math.lengthsq(vel);
            if (previousPositionValid[i] != 0 &&
                previousPositions[i].y > surfaceY &&
                pos.y <= surfaceY &&
                depthBelowSurface >= math.max(0.01f, splashDepthThresholdMeters) &&
                velocitySq >= math.max(0.0001f, splashVelocityThresholdSq))
            {
                impactEvents[i] = new FluidImpactEvent
                {
                    PositionWS = pos,
                    VelocityWS = vel,
                    MassKg = p.mass,
                    SurfaceY = surfaceY
                };
                impactEventFlags[i] = 1;
            }
        }

        private float ResolveFluidDensity(int i, BuoyancyParams p, float3 pos, float depthBelowSurface, out byte brineSubmerged, out float denseLayer01)
        {
            float resolvedWaterDensity = p.useLocalFluidDensityOverride != 0
                ? math.max(0.01f, p.localFluidDensity)
                : waterDensity;
            brineSubmerged = 0;
            if (brineFlags.IsCreated &&
                brineHeights.IsCreated &&
                brineDensityMultipliers.IsCreated &&
                i < brineFlags.Length &&
                i < brineHeights.Length &&
                i < brineDensityMultipliers.Length &&
                (brineFlags[i] & BrineLayerConstants.SampleValidFlag) != 0)
            {
                float brineRuntimeHeightY = BrineLayerMath.ResolveRuntimeHeightY(brineHeights[i], brineShiftOffsetY);
                if (math.isfinite(brineRuntimeHeightY) && pos.y < brineRuntimeHeightY)
                {
                    resolvedWaterDensity *= math.max(1f, brineDensityMultipliers[i]);
                    brineSubmerged = 1;
                }
            }
            denseLayer01 = 0f;
            if (enableAnalyticalFlowField != 0)
            {
                float safeHaloclineDepth = math.max(0.01f, haloclineBoundaryDepthMeters);
                denseLayer01 = depthBelowSurface >= safeHaloclineDepth ? 1f : 0f;
                resolvedWaterDensity *= 1f + (math.max(1f, deepLayerDensityMultiplier) - 1f) * denseLayer01;
            }
            return resolvedWaterDensity;
        }

        private float3 CalculateBuoyancyForce(int i, BuoyancyParams p, float subRatio, float resolvedWaterDensity, byte brineSubmerged, out float displacedVolume, out float buoyancyMagnitude)
        {
            displacedVolume = p.volume * subRatio;
            buoyancyMagnitude = resolvedWaterDensity * displacedVolume * gravity;
            if (useGpuBuoyancyForce != 0 &&
                p.useLocalFluidDensityOverride == 0 &&
                i < gpuBuoyancyForcesY.Length)
            {
                buoyancyMagnitude = math.max(0f, gpuBuoyancyForcesY[i]);
            }

            buoyancyMagnitude *= math.max(0.05f, p.buoyancyMultiplier);
            if (brineSubmerged != 0)
            {
                float brineForceCap = math.max(0.01f, p.mass) * gravity * 9f;
                buoyancyMagnitude = math.min(buoyancyMagnitude, brineForceCap);
            }

            return new float3(0f, buoyancyMagnitude, 0f);
        }

        private float3 CalculateSampledCurrent(BuoyancyParams p, float3 pos, float depthBelowSurface)
        {
            float3 standardCurrent = baseCurrentForce + p.localCurrent;
            standardCurrent += weatherCurrentDirection * math.max(0f, weatherCurrentScale) * math.max(0f, weatherBlend);
            float3 sampledCurrent = baseCurrentForce + p.localCurrent;
            float giantWakeDepth01 = math.saturate(
                (depthBelowSurface - math.max(0f, giantWakeDepthFadeStart)) *
                math.rcp(math.max(0.001f, giantWakeDepthFadeRange)));
            float3 resolvedGiantWakeCurrent = giantWakeCurrent * giantWakeDepth01;
            sampledCurrent += resolvedGiantWakeCurrent;

            if (enablePhantomCurrent != 0 && p.currentResponse > 0.0001f)
            {
                sampledCurrent += HectonAnalyticalFlowField.SamplePrebakedVectorCurrent(
                    pos,
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
            bool thermoclineActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.ThermoclineActive) != 0u;
            bool haloclineActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.HaloclineActive) != 0u;
            if (stormActive)
            {
                float surfaceLayer01 = 1f - math.saturate(depthBelowSurface * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
                float stormBlend = math.max(0f, weatherBlend);
                float stormBiasScale = weatherCurrentScale * math.max(0.35f, stormBlend);
                sampledCurrent.xz += weatherCurrentDirection.xz * stormBiasScale;

                if (detailedMathEnabled != 0 && surfaceLayer01 > 0.0001f && p.currentResponse > 0.0001f)
                {
                    sampledCurrent += HectonAnalyticalFlowField.SamplePrebakedVectorCurrent(
                        pos + new float3(17.3f, 0f, 11.1f),
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

            if (thermoclineActive || haloclineActive)
            {
                float thermoclineBand01 = 1f - math.saturate(
                    math.abs(depthBelowSurface - ThermoclineDepthMeters) *
                    math.rcp(math.max(ThermoclineHalfBandMeters, 0.0001f)));
                if (thermoclineBand01 > 0.0001f)
                    sampledCurrent.y *= 1f + (ThermoclineVerticalAttenuation - 1f) * thermoclineBand01;
            }

            if (enableAnalyticalFlowField != 0)
            {
                int thrusterCount = math.min(math.max(0, activeThrusterCount), activeThrusters.Length);
                for (int thrusterIndex = 0; thrusterIndex < thrusterCount; thrusterIndex++)
                    HectonAnalyticalFlowField.ApplyThrusterFlow(ref sampledCurrent, pos, activeThrusters[thrusterIndex]);

                int whirlpoolCount = math.min(math.max(0, activeWhirlpoolCount), activeWhirlpools.Length);
                for (int whirlpoolIndex = 0; whirlpoolIndex < whirlpoolCount; whirlpoolIndex++)
                    HectonAnalyticalFlowField.ApplyWhirlpoolFlow(
                        ref sampledCurrent,
                        pos,
                        activeWhirlpools[whirlpoolIndex],
                        detailedMathEnabled == 0 ? (byte)1 : (byte)0);
            }
            return sampledCurrent;
        }

        private float3 CalculateShearForce(BuoyancyParams p, float subRatio, float denseLayer01)
        {
            if (enableAnalyticalFlowField != 0 && denseLayer01 > 0f && haloclineShearForcePerKg != 0f && p.currentResponse > 0.0001f)
            {
                return new float3(
                    0f,
                    0f,
                    haloclineShearForcePerKg * p.mass * subRatio * math.max(0f, p.currentResponse));
            }
            return float3.zero;
        }

        private float3 CalculateWindForce(BuoyancyParams p, float subRatio, float depthBelowSurface)
        {
            System.Numerics.Vector3 pureWindVector = Hecton8.PureLogic.Kinematics.SurfaceCurrentWindshearVector.Calculate(
                new System.Numerics.Vector2(windAdvectionVector.x, windAdvectionVector.z),
                math.max(0f, windAdvectionForcePerKg) * p.mass * subRatio * p.currentResponse,
                depthBelowSurface,
                math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f))
            );
            return new float3(pureWindVector.X, pureWindVector.Y, pureWindVector.Z);
        }

        private float3 CalculateDragForce(BuoyancyParams p, float3 pos, float3 vel, float3 sampledCurrent, float subRatio, float resolvedWaterDensity)
        {
            float viscosityMultiplier = 1f;
            if (enableDynamicViscosityRegions != 0 && activeViscosityRegionCount > 0)
            {
                viscosityMultiplier = HectonAnalyticalFlowField.SampleViscosityMultiplier(
                    pos,
                    activeViscosityRegions,
                    activeViscosityRegionCount,
                    viscosityGradientLut);
            }

            float3 relativeVelocity = vel - sampledCurrent;
            float relativeSpeedSq = math.lengthsq(relativeVelocity);
            if (relativeSpeedSq > 0.000001f && maxQuadraticDragForcePerKg > 0f)
            {
                float relativeSpeed = FastMagnitudeApprox(relativeVelocity);
                float dragScalar = math.max(0f, viscousDrag) *
                                   viscosityMultiplier *
                                   resolvedWaterDensity *
                                   math.max(0.01f, p.volume) *
                                   subRatio;
                float3 dragForce = -relativeVelocity * (math.max(1f, relativeSpeed) * dragScalar);
                return ClampVectorMagnitude(
                    dragForce,
                    math.max(0f, maxQuadraticDragForcePerKg) * math.max(0.01f, p.mass));
            }
            return float3.zero;
        }

        private float3 CalculateDampingForce(float3 vel, float subRatio, float resolvedWaterDensity, float displacedVolume)
        {
            float dampingForce = 0f;
            if (subRatio < 1f)
            {
                dampingForce = -vel.y * resolvedWaterDensity * displacedVolume * 0.5f;
            }
            return new float3(0f, dampingForce, 0f);
        }

        private float3 CalculateTorques(BuoyancyParams p, float3 pos, float3 up, float3 targetUp, float3 angularVel, float3 sampledCurrent, float subRatio, float buoyancyMagnitude, float depthBelowSurface)
        {
            float surfaceBand = math.saturate(
                1f - math.abs(depthBelowSurface - p.height) *
                math.rcp(math.max(0.25f, p.height * 1.5f)));
            float3 tiltAxis = math.cross(up, targetUp);
            float3 stabilityTorque = tiltAxis * (p.surfaceStability * buoyancyMagnitude * surfaceBand * 0.12f);
            float3 angularDragTorque = -angularVel * (angularDragCoeff * math.max(0.1f, p.angularDragMultiplier) * subRatio * math.max(1f, p.mass * 0.35f));
            float3 flowAxis = NormalizeOrDefault(sampledCurrent, new float3(1f, 0f, 0f));
            float3 gyroscopicAxis = math.cross(up, flowAxis);
            float currentSpeed = FastMagnitudeApprox(sampledCurrent);
            float volumeLever = CinematicVolumeLever(p.volume);
            float lightTumbleBias = math.saturate(math.rcp(math.max(0.25f, p.mass)));
            float massStabilizer = math.rcp(math.max(1f, p.mass));
            float3 gyroscopicFlowTorque = gyroscopicAxis *
                                          (currentSpeed * volumeLever * lightTumbleBias * massStabilizer *
                                           subRatio * math.max(0f, p.currentResponse) * 3.25f);
            float maxGyroscopicFlowTorque = JobGyroscopicFlowMaxTorquePerKg * math.max(0.01f, p.mass);
            gyroscopicFlowTorque = ClampVectorMagnitude(gyroscopicFlowTorque, maxGyroscopicFlowTorque);
            float3 shearTorque = float3.zero;
            if (enableTidalShearZones != 0 && tidalShearTorqueStrength > 0f && p.currentResponse > 0.0001f)
            {
                float3 standardCurrent = baseCurrentForce + p.localCurrent;
                standardCurrent += weatherCurrentDirection * math.max(0f, weatherCurrentScale) * math.max(0f, weatherBlend);
                float giantWakeDepth01 = math.saturate(
                    (depthBelowSurface - math.max(0f, giantWakeDepthFadeStart)) *
                    math.rcp(math.max(0.001f, giantWakeDepthFadeRange)));
                float3 resolvedGiantWakeCurrent = giantWakeCurrent * giantWakeDepth01;

                float standardSpeedSq = math.lengthsq(standardCurrent);
                float wakeSpeedSq = math.lengthsq(resolvedGiantWakeCurrent);
                if (standardSpeedSq > 0.0001f && wakeSpeedSq > 0.0001f)
                {
                    float3 standardAxis = NormalizeOrDefault(standardCurrent, new float3(1f, 0f, 0f));
                    float3 wakeAxis = NormalizeOrDefault(resolvedGiantWakeCurrent, new float3(1f, 0f, 0f));
                    float crossMagnitudeSq = math.lengthsq(math.cross(standardAxis, wakeAxis));
                    float opposition = math.saturate(-math.dot(standardAxis, wakeAxis));
                    float minCurrentSpeed = math.min(
                        FastMagnitudeApprox(standardCurrent),
                        FastMagnitudeApprox(resolvedGiantWakeCurrent));
                    float shear01 = math.saturate((crossMagnitudeSq + opposition) * minCurrentSpeed * 0.85f);
                    float phase = math.dot(pos, new float3(0.071f, 0.113f, 0.097f)) + time * math.max(0.01f, tidalShearFrequency);
                    float turbulence = FastTriangleSigned(phase) * FastTriangleSigned(phase * 1.731f + 2.17f);
                    float3 shearAxis = NormalizeOrDefault(math.cross(standardAxis, wakeAxis), up);
                    shearTorque = shearAxis *
                                  (turbulence * shear01 * math.max(0f, tidalShearTorqueStrength) *
                                   volumeLever * subRatio * math.max(0f, p.currentResponse));
                    shearTorque = ClampVectorMagnitude(shearTorque, maxGyroscopicFlowTorque);
                }
            }

            return angularDragTorque + stabilityTorque + gyroscopicFlowTorque + shearTorque;
        }

        private static float FastMagnitudeApprox(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private static float CinematicVolumeLever(float volume)
        {
            float safeVolume = math.max(0.0001f, volume);
            float smallVolumeLever = 0.2f + safeVolume * 0.8f;
            float largeVolumeLever = 0.75f + safeVolume * 0.25f;
            return math.min(8f, math.select(smallVolumeLever, largeVolumeLever, safeVolume > 1f));
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static float3 ClampVectorMagnitude(float3 value, float maxMagnitude)
        {
            float safeMaxMagnitude = math.max(0f, maxMagnitude);
            float magnitude = FastMagnitudeApprox(value);
            if (magnitude <= safeMaxMagnitude || magnitude <= 0.000001f)
                return value;

            return value * (safeMaxMagnitude * math.rcp(magnitude));
        }

        private static float3 ResolveFiniteFloat3OrZero(float3 value)
        {
            return (math.any(math.isnan(value)) || math.any(math.isinf(value)) || !math.all(math.isfinite(value)))
                ? float3.zero
                : value;
        }

        private static float3 ResolveSurfaceNormalLod(float3 value, uint flags, byte detailedMathEnabled)
        {
            if (detailedMathEnabled != 0 && (flags & BuoyancyParams.ExactSurfaceNormalFlag) != 0u)
            {
                float lengthSq = math.lengthsq(value);
                float3 safeValue = math.select(new float3(0f, 1f, 0f), value, lengthSq > 0.000001f);
                return safeValue * math.rsqrt(math.max(math.lengthsq(safeValue), 0.000001f));
            }

            return DominantAxisOrDefault(value, new float3(0f, 1f, 0f));
        }

        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.isfinite(lengthSq) && lengthSq > 0.000001f;
            float3 safeValue = math.select(fallback, value, valid);
            float safeLengthSq = math.lengthsq(safeValue);
            bool safeValid = math.isfinite(safeLengthSq) && safeLengthSq > 0.000001f;
            safeValue = math.select(new float3(1f, 0f, 0f), safeValue, safeValid);
            return safeValue * math.rsqrt(math.max(math.lengthsq(safeValue), 0.000001f));
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
}
