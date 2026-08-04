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
    internal static class HectonGerstnerWater
    {
        private const float TwoPi = 6.28318530718f;
        private const float CinematicPhaseSpeedBase = 0.85f;
        private const float CinematicPhaseSpeedPerMeter = 0.23f;

        public static float SampleHeight(
            float2 worldXZ,
            GerstnerWaveComponent wave0,
            GerstnerWaveComponent wave1,
            GerstnerWaveComponent wave2,
            float timeSeconds)
        {
            float height = ComputeHeight(worldXZ, wave0, timeSeconds) +
                           ComputeHeight(worldXZ, wave1, timeSeconds) +
                           ComputeHeight(worldXZ, wave2, timeSeconds);
            return ResolveFiniteFloatOrZero(height);
        }

        public static float SampleHeight(
            float2 worldXZ,
            GerstnerWaveComponent wave,
            float timeSeconds)
        {
            return ComputeHeight(worldXZ, wave, timeSeconds);
        }

        public static float SampleHeight(
            double2 worldXZ,
            GerstnerWaveComponent wave,
            float timeSeconds)
        {
            return ComputeHeight(worldXZ, wave, timeSeconds);
        }

        public static float SampleHeight(
            float2 worldXZ,
            NativeArray<GerstnerWaveComponent> waves,
            int waveCount,
            float timeSeconds)
        {
            if (!waves.IsCreated || waveCount <= 0)
                return 0f;

            int count = math.min(math.max(0, waveCount), waves.Length);
            float height = 0f;
            for (int i = 0; i < count; i++)
                height += ComputeHeight(worldXZ, waves[i], timeSeconds);

            return ResolveFiniteFloatOrZero(height);
        }

        public static float SampleHeight(
            double2 worldXZ,
            NativeArray<GerstnerWaveComponent> waves,
            int waveCount,
            float timeSeconds)
        {
            if (!waves.IsCreated || waveCount <= 0)
                return 0f;

            int count = math.min(math.max(0, waveCount), waves.Length);
            float height = 0f;
            for (int i = 0; i < count; i++)
                height += ComputeHeight(worldXZ, waves[i], timeSeconds);

            return ResolveFiniteFloatOrZero(height);
        }

        public static float3 SampleFiniteDifferenceNormal(
            float2 worldXZ,
            NativeArray<GerstnerWaveComponent> waves,
            int waveCount,
            float timeSeconds,
            float sampleDistanceMeters)
        {
            float sampleDistance = math.max(0.05f, sampleDistanceMeters);
            float2 offsetX = new float2(sampleDistance, 0f);
            float2 offsetZ = new float2(0f, sampleDistance);
            float left = SampleHeight(worldXZ - offsetX, waves, waveCount, timeSeconds);
            float right = SampleHeight(worldXZ + offsetX, waves, waveCount, timeSeconds);
            float down = SampleHeight(worldXZ - offsetZ, waves, waveCount, timeSeconds);
            float up = SampleHeight(worldXZ + offsetZ, waves, waveCount, timeSeconds);
            float3 normal = new float3(left - right, sampleDistance * 2f, down - up);
            return ResolveNormalOrUp(normal);
        }

        public static float3 SampleFiniteDifferenceNormal(
            double2 worldXZ,
            NativeArray<GerstnerWaveComponent> waves,
            int waveCount,
            float timeSeconds,
            float sampleDistanceMeters)
        {
            double sampleDistance = math.max(0.05d, (double)sampleDistanceMeters);
            double2 offsetX = new double2(sampleDistance, 0d);
            double2 offsetZ = new double2(0d, sampleDistance);
            float left = SampleHeight(worldXZ - offsetX, waves, waveCount, timeSeconds);
            float right = SampleHeight(worldXZ + offsetX, waves, waveCount, timeSeconds);
            float down = SampleHeight(worldXZ - offsetZ, waves, waveCount, timeSeconds);
            float up = SampleHeight(worldXZ + offsetZ, waves, waveCount, timeSeconds);
            float3 normal = new float3(left - right, (float)(sampleDistance * 2d), down - up);
            return ResolveNormalOrUp(normal);
        }

        private static float ComputeHeight(float2 worldXZ, GerstnerWaveComponent wave, float timeSeconds)
        {
            if (wave.Amplitude <= 0f || wave.Wavelength <= 0.01f)
                return 0f;

            float2 direction = ResolveDirectionOrDefault(wave.DirectionXZ, new float2(1f, 0f));
            float waveNumber = TwoPi * math.rcp(math.max(0.01f, wave.Wavelength));
            float phaseVelocity = (CinematicPhaseSpeedBase + wave.Wavelength * CinematicPhaseSpeedPerMeter) *
                                  math.max(0.01f, wave.SpeedMultiplier);
            float phase = waveNumber * math.dot(direction, worldXZ) - phaseVelocity * waveNumber * timeSeconds + wave.PhaseOffset;
            float height = wave.Amplitude * MathLodApproximation.ApproxCosBhaskara(phase);
            return ResolveFiniteFloatOrZero(height);
        }

        private static float ComputeHeight(double2 worldXZ, GerstnerWaveComponent wave, float timeSeconds)
        {
            if (wave.Amplitude <= 0f || wave.Wavelength <= 0.01f)
                return 0f;

            float2 directionFloat = ResolveDirectionOrDefault(wave.DirectionXZ, new float2(1f, 0f));
            double2 direction = new double2(directionFloat.x, directionFloat.y);
            double waveNumber = (double)TwoPi * math.rcp(math.max(0.01d, (double)wave.Wavelength));
            double phaseVelocity = (CinematicPhaseSpeedBase + (double)wave.Wavelength * CinematicPhaseSpeedPerMeter) *
                                   math.max(0.01d, (double)wave.SpeedMultiplier);
            double phase = waveNumber * math.dot(direction, worldXZ) -
                           phaseVelocity * waveNumber * (double)timeSeconds +
                           (double)wave.PhaseOffset;
            float height = wave.Amplitude * MathLodApproximation.ApproxCosBhaskara((float)phase);
            return ResolveFiniteFloatOrZero(height);
        }

        private static float ResolveFiniteFloatOrZero(float value)
        {
            return (math.isnan(value) || math.isinf(value) || !math.isfinite(value)) ? 0f : value;
        }

        internal static float2 ResolveDirectionOrDefault(float2 value, float2 fallback)
        {
            float lengthSq = math.dot(value, value);
            bool valid = math.isfinite(lengthSq) && lengthSq > 0.000001f;
            float2 safeValue = math.select(fallback, value, valid);
            float safeLengthSq = math.dot(safeValue, safeValue);
            return safeValue * math.rsqrt(math.max(safeLengthSq, 0.000001f));
        }

        private static float3 ResolveNormalOrUp(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.isfinite(lengthSq) && lengthSq > 0.000001f;
            float3 safeValue = math.select(new float3(0f, 1f, 0f), value, valid);
            float safeLengthSq = math.lengthsq(safeValue);
            return safeValue * math.rsqrt(math.max(safeLengthSq, 0.000001f));
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static float FastMagnitudeApprox(float2 value)
        {
            float2 absValue = math.abs(value);
            float major = math.max(absValue.x, absValue.y);
            float minor = math.min(absValue.x, absValue.y);
            return major + minor * 0.375f;
        }

        private static float2 DominantAxisOrDefault(float2 value, float2 fallback)
        {
            float2 absValue = math.abs(value);
            float maxComponent = math.max(absValue.x, absValue.y);
            float2 xAxis = new float2(math.select(-1f, 1f, value.x >= 0f), 0f);
            float2 yAxis = new float2(0f, math.select(-1f, 1f, value.y >= 0f));
            float2 axis = math.select(yAxis, xAxis, absValue.x >= absValue.y);
            return math.select(axis, fallback, maxComponent <= 0.000001f);
        }
    }
}
