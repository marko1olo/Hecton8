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
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class HectonFluidEngine : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IAbyssalFlowGpuReadModel, IFluidAdvectionRenderGraphDispatchSource, IAnalyticalFlowReadModel, IAmbientCurrentReadModel, IFluidSurfaceCurrentReadModel, IFluidBubbleBurstSink, IFluidCurrentWriteSink, IBuoyancyObjectRegistry
    {
        private static int s_x001HectonFluidEngineSignalPushDropCount;
#if UNITY_EDITOR
        private const string GpuBuoyancyComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_GpuBuoyancy.compute";
        private const string AbyssalFlowFieldComputeAssetPath = "Assets/_Project/Art/Shaders/AbyssalFlowField.compute";
        private const string FluidAdvectionComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute";
#endif
        private const float AbyssalFlowThermoclineDepthMeters = 120f;
        private const int AbyssalFlowTextureResolution = 32;
        private const float AbyssalFlowTextureWorldSizeMeters = 100f;
        private const float AbyssalFlowTextureCellSizeMeters = AbyssalFlowTextureWorldSizeMeters / AbyssalFlowTextureResolution;
        private const int AbyssalFlowTextureThreadGroupSize = 4;
        private const int AbyssalFlowUpdateBucketMask = SimulationBucketConstants.FastBucketMask;
        private const float AbyssalFlowUpdateBucketInvCount = 1f / SimulationBucketConstants.FastBucketCount;
        private const uint AbyssalFlowKillSwitchMask = GlobalRegistry.SystemKillSwitchLane4VfxMask;
        private const uint AbyssalFlowBucketedCostHash = 0x41424642u; // ABFB
        private static uint _systemKillSwitchMaskSnapshot;
        private static int _systemKillSwitchSnapshotFrame = -1;
        private const float AbyssalFlowWakeMinimumSpeedMetersPerSecond = 0.5f;
        private const int MaxAbyssalVortexImpulseCount = 4;
        private const float AbyssalVortexImpulseMinimumRadiusMeters = 0.5f;
        private const float AbyssalVortexImpulseMaximumRadiusMeters = 45f;
        private const float AbyssalVortexImpulseMaximumStrengthMetersPerSecond = 14f;
        private const float AbyssalVortexImpulseMaximumDurationSeconds = 4f;
        private const int SplashdownBubbleCount = 500;
        private const float SplashdownImpulseRadiusMeters = 30f;
        private const float SplashdownImpulseDurationSeconds = 10f;
        private const float SplashdownImpulseStrength = 900f;
        private const float SplashdownImpulseUpwardBiasMeters = 4f;
        private const float SplashdownImpulseMaxVelocityMetersPerSecond = 16f;
        private const float SplashdownBubbleSpawnRadiusMeters = 1.15f;
        private const float SplashdownBubbleUpwardBiasMeters = 1.8f;
        private const float SplashdownBubbleMinimumQualityMaxVelocityMetersPerSecond = 8f;
        private const float SplashdownGoldenAngleRadians = 2.39996323f;
        private const uint SplashdownImpulseOutsideFlowVolumeFlag = 1u << 1;
        private const uint SplashdownImpulseJobBusyFlag = 1u << 2;
        private const uint SplashdownImpulseUploadFailedFlag = 1u << 3;
        private const uint SplashdownImpulseNoAffectedCellsFlag = 1u << 4;
        private const uint SplashdownImpulseJobInvalidFlag = 1u << 8;
        private const uint SplashdownImpulseInvalidInputFlag = 1u << 31;
        private const uint PrologueSequenceSourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const int AbyssalFlowTelemetryCapacity = 300;
        private const string AbyssalFlowDumpRelativePath = "Docs/AgentLogs/Dump_SPLASHDOWN_FLUID_DYNAMICS.bin";
        private const int GpuReadbackRingSize = 3;
        private const int MaxAbyssalHeatSourceCount = 8;
        private const int MaxCavitationBurstEvents = 8;
        private const int FluidAdvectionThreadGroupSize = 64;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int FluidAdvectionTelemetryCapacity = 300;
        private const int FluidAdvectionSignalDrainBudget = 64;
        private const int FluidAdvectionGlobalTelemetryIntervalFrames = 30;
        private const float FluidFallbackClockMaxSeconds = 16777215f;
        private const int DynamicWakeGpuCapacity = 16;
        private const int DynamicWakeMinimumQualityGpuCapacity = 4;
        private const int MaxAdvectedSiltCount = 4096;
        public const int MaxAdvectedDebrisCount = 1000;
        public const int MaxAdvectedBubbleCount = 2000;
        private const float BubbleAdvectionBuoyancyMetersPerSecond = 0.42f;
        private const float SiltAdvectionBuoyancyMetersPerSecond = 0f;
        private const float DebrisAdvectionBuoyancyMetersPerSecond = -0.24f;
        private const float FluidAdvectionVelocityBlend = 0.82f;
        private const float BubbleBurstSpawnRadiusMeters = 0.18f;
        private const float DebrisSpawnRadiusMeters = 0.12f;
        private const float FluidAdvectionSdfSolidThreshold = 0.5f;
        private const uint AdvectedBubbleActiveFlag = 1u;
        private const uint AdvectedDebrisActiveFlag = 1u;
        private const uint ActiveAdvectedParticlesTelemetryHash = 0x41445650u;
        private const uint ActiveTurbulenceWakesTelemetryHash = 0x57544B53u;
        private const uint FluidAdvectionTelemetryContextHash = 0x41425953u;
        private const string FluidAdvectionDumpRelativePath = "Docs/AgentLogs/Dump_ABYSSAL_CURRENT_ADVECTION.bin";
        public const int MaxAnalyticalThrusterCount = FluidAnalyticalContractConstants.MaxAnalyticalThrusterCount;
        public const int MaxAnalyticalWhirlpoolCount = FluidAnalyticalContractConstants.MaxAnalyticalWhirlpoolCount;
        public const int MaxActiveMaelstromCount = FluidAnalyticalContractConstants.MaxActiveMaelstromCount;
        public const int MaxDynamicViscosityRegionCount = FluidAnalyticalContractConstants.MaxDynamicViscosityRegionCount;
        private const int MaelstromTelemetryCapacity = 300;
        private const string MaelstromDumpRelativePath = "Docs/AgentLogs/Dump_MAELSTROM_KINEMATICS.bin";
        private const float MaelstromMinimumRadiusMeters = 0.5f;
        private const float MaelstromEventHorizonRadiusFactor = 0.12f;
        internal const float MaelstromMaxVelocityMetersPerSecond = FluidAnalyticalContractConstants.MaelstromMaxVelocityMetersPerSecond;
        internal const float MaelstromMinimumMathDetailMaxVelocityMetersPerSecond = FluidAnalyticalContractConstants.MaelstromMinimumMathDetailMaxVelocityMetersPerSecond;
        private const byte AuthorityFluidDetailedMathEnabled = 1;
        private const byte AuthorityFluidSimplifiedMathEnabled = 0;
        private const float MaelstromAudioIntervalSeconds = 0.45f;
        private const float MaelstromDamageIntervalSeconds = 0.35f;
        private const float MaelstromDamageMagnitude = 18f;
        private const uint MaelstromSourceHash = 0x4D41454Cu;
        private const byte MaelstromAcousticChannel = 12;
        private const int ViscosityGradientLutSize = 16;
        private const int FluidImpactEventQueueCapacity = 64;
        private const BufferID FluidImpactEventRingBufferId = BufferID.HectonFluidEngine_FluidImpactEventRingBufferId;
        private const BufferID FluidPositionsBufferId = BufferID.HectonFluidEngine_FluidPositionsBufferId;
        private const BufferID FluidPreviousPositionsBufferId = BufferID.HectonFluidEngine_FluidPreviousPositionsBufferId;
        private const BufferID FluidPreviousPositionValidBufferId = BufferID.HectonFluidEngine_FluidPreviousPositionValidBufferId;
        private const BufferID FluidVelocitiesBufferId = BufferID.HectonFluidEngine_FluidVelocitiesBufferId;
        private const BufferID FluidAngularVelocitiesBufferId = BufferID.HectonFluidEngine_FluidAngularVelocitiesBufferId;
        private const BufferID FluidUpVectorsBufferId = BufferID.HectonFluidEngine_FluidUpVectorsBufferId;
        private const BufferID FluidSurfaceUpVectorsBufferId = BufferID.HectonFluidEngine_FluidSurfaceUpVectorsBufferId;
        private const BufferID FluidBuoyancyParamsBufferId = BufferID.HectonFluidEngine_FluidBuoyancyParamsBufferId;
        private const BufferID FluidWaveOffsetsBufferId = BufferID.HectonFluidEngine_FluidWaveOffsetsBufferId;
        private const BufferID FluidSleepMaskBufferId = BufferID.HectonFluidEngine_FluidSleepMaskBufferId;
        private const BufferID FluidLocalGerstnerWavesBufferId = BufferID.HectonFluidEngine_FluidLocalGerstnerWavesBufferId;
        private const BufferID FluidGpuBuoyancyForcesYBufferId = BufferID.HectonFluidEngine_FluidGpuBuoyancyForcesYBufferId;
        private const BufferID FluidResultForcesBufferId = BufferID.HectonFluidEngine_FluidResultForcesBufferId;
        private const BufferID FluidResultTorquesBufferId = BufferID.HectonFluidEngine_FluidResultTorquesBufferId;
        private const BufferID FluidOceanSurfaceTelemetryBufferId = BufferID.HectonFluidEngine_FluidOceanSurfaceTelemetryBufferId;
        private const BufferID FluidImpactEventScratchBufferId = BufferID.HectonFluidEngine_FluidImpactEventScratchBufferId;
        private const BufferID FluidImpactEventFlagsBufferId = BufferID.HectonFluidEngine_FluidImpactEventFlagsBufferId;
        private const BufferID FluidGpuBuoyancyObjectUploadBufferId = BufferID.HectonFluidEngine_FluidGpuBuoyancyObjectUploadBufferId;
        private const BufferID FluidGpuBuoyancyReadbackBufferId = BufferID.HectonFluidEngine_FluidGpuBuoyancyReadbackBufferId;
        private const BufferID FluidBrineHeightsBufferId = BufferID.HectonFluidEngine_FluidBrineHeightsBufferId;
        private const BufferID FluidBrineDensityMultipliersBufferId = BufferID.HectonFluidEngine_FluidBrineDensityMultipliersBufferId;
        private const BufferID FluidBrineCartographySectorsBufferId = BufferID.HectonFluidEngine_FluidBrineCartographySectorsBufferId;
        private const BufferID FluidBrineFlagsBufferId = BufferID.HectonFluidEngine_FluidBrineFlagsBufferId;
        private const BufferID FluidGpuAbyssalHeatSourceUploadBufferId = BufferID.HectonFluidEngine_FluidGpuAbyssalHeatSourceUploadBufferId;
        private const BufferID FluidActiveThrusterFlowsBufferId = BufferID.HectonFluidEngine_FluidActiveThrusterFlowsBufferId;
        private const BufferID FluidActiveWhirlpoolsBufferId = BufferID.HectonFluidEngine_FluidActiveWhirlpoolsBufferId;
        private const BufferID FluidActiveMaelstromsBufferId = BufferID.HectonFluidEngine_FluidActiveMaelstromsBufferId;
        private const BufferID FluidMaelstromTelemetryBufferId = BufferID.HectonFluidEngine_FluidMaelstromTelemetryBufferId;
        private const BufferID FluidActiveViscosityRegionsBufferId = BufferID.HectonFluidEngine_FluidActiveViscosityRegionsBufferId;
        private const BufferID FluidViscosityGradientLutBufferId = BufferID.HectonFluidEngine_FluidViscosityGradientLutBufferId;
        private const BufferID FluidPrebakedVectorNoiseFieldBufferId = BufferID.HectonFluidEngine_FluidPrebakedVectorNoiseFieldBufferId;
        private const BufferID FluidAdvectedSiltUploadBufferId = BufferID.HectonFluidEngine_FluidAdvectedSiltUploadBufferId;
        private const BufferID FluidAdvectedBubbleUploadBufferId = BufferID.HectonFluidEngine_FluidAdvectedBubbleUploadBufferId;
        private const BufferID FluidAdvectedDebrisUploadBufferId = BufferID.HectonFluidEngine_FluidAdvectedDebrisUploadBufferId;
        private const BufferID FluidEmptyAbyssalFlowUploadBufferId = BufferID.HectonFluidEngine_FluidEmptyAbyssalFlowUploadBufferId;
        private const BufferID FluidAdvectionTelemetryBufferId = BufferID.HectonFluidEngine_FluidAdvectionTelemetryBufferId;
        private const BufferID FluidSplashdownImpulseUploadBufferId = BufferID.HectonFluidEngine_FluidSplashdownImpulseUploadBufferId;
        private const BufferID FluidSplashdownImpulseStatsBufferId = BufferID.HectonFluidEngine_FluidSplashdownImpulseStatsBufferId;
        private const BufferID FluidAbyssalFlowTelemetryBufferId = BufferID.HectonFluidEngine_FluidAbyssalFlowTelemetryBufferId;
        private const BufferID FluidSovereigntyTelemetryRingBufferId = BufferID.HectonFluidEngine_FluidSovereigntyTelemetryRingBufferId;
        private const BufferID FluidSovereigntyTelemetryCursorBufferId = BufferID.HectonFluidEngine_FluidSovereigntyTelemetryCursorBufferId;
        private const BufferID FluidAdvectedSiltDirtyPagesBufferId = BufferID.HectonFluidEngine_FluidAdvectedSiltDirtyPagesBufferId;
        private const BufferID FluidAdvectedBubbleDirtyPagesBufferId = BufferID.HectonFluidEngine_FluidAdvectedBubbleDirtyPagesBufferId;
        private const BufferID FluidAdvectedDebrisDirtyPagesBufferId = BufferID.HectonFluidEngine_FluidAdvectedDebrisDirtyPagesBufferId;
        private const int FluidAdvectionDirtyPageSize = 64;
        private const int FluidAdvectionMinUploadBudgetBytes = 32 * 1024;
        private const int FluidAdvectionMaxUploadBudgetBytes = 512 * 1024;
        private const int FluidSovereigntyTelemetryCapacity = 300;
        private const int FluidSovereigntyTelemetryEntrySizeBytes = 64;
        private const uint FluidTelemetryFlagResolveOk = 1u << 0;
        private const uint FluidTelemetryFlagResolveFault = 1u << 1;
        private const uint FluidTelemetryFlagWriteLockContention = 1u << 2;
        private const uint FluidTelemetryFlagNonFiniteForce = 1u << 3;
        private const uint FluidTelemetryFlagNonFiniteTorque = 1u << 4;
        private const uint FluidTelemetryFlagDump = 1u << 5;
        private const uint FluidTelemetryFlagCapacityExceeded = 1u << 6;
        private const uint FluidSovereigntyTelemetryMagic = 0x46313332u; // F132
        private const string FluidSovereigntyDumpRelativePath = "Docs/AgentLogs/Dump_1322_FluidEngine.bin";
        private const int MaxGerstnerWaveCount = 16;
        private const int OceanSurfaceTelemetryCapacity = 300;
        private const int CavitationShockwaveHitCapacity = 64;
        private const int GpuThreadGroupSize = 64;
        private const int GpuThreadGroupShift = 6;
        private const float OceanSleepDistanceMeters = 500f;
        private const float OceanWakeDistanceMeters = 495f;
        private const float OceanSleepDistanceSq = OceanSleepDistanceMeters * OceanSleepDistanceMeters;
        private const float OceanWakeDistanceSq = OceanWakeDistanceMeters * OceanWakeDistanceMeters;
        private const float OceanSurfaceNormalSampleMeters = 0.75f;
        private const float ShoreTerrainFallbackBandMeters = 14f;
        private const float SplashDepthThresholdMeters = 1f;
        private const float SplashVelocityThresholdMetersPerSecond = 3.5f;
        private const float SurfaceWindAdvectionForcePerKg = 0.08f;
        private const string OceanSurfaceDumpPath = "Docs/AgentLogs/Dump_OCEAN_SURFACE_KINEMATICS.bin";
        private const float GiantWakeDirectionEpsilonSq = 0.0001f;
        private const uint HectonFluidEngineContextHash = 0x48464645u;
        private const uint NonFiniteBuoyancyForceHash = 0x4E464246u;
        private const uint NonFiniteBuoyancyTorqueHash = 0x4E464254u;
        private const uint BuoyancyCapacityExceededHash = 0x42434150u;
        private const uint OceanSplashSignalHash = 0x4F435350u;
        private const uint SplashdownFluidImpulseCountHash = 0x53464943u;
        private const uint SplashdownFluidImpulseContextHash = 0x5346504Cu;
        // Keep GPU sampling dormant until it matches the 16-wave/AUP/terrain Burst path.
        private const bool GpuBuoyancySurfaceParityAvailable = false;
        private const string NativeMemoryOwner = nameof(HectonFluidEngine);
        private const string GpuReadbackDataLabel = "_gpuReadbackData";
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        private sealed class GpuReadbackNativeRing : System.IDisposable
        {
            private readonly NativeArray<float4>[] _data;

            public GpuReadbackNativeRing(int ringSize)
            {
                _data = new NativeArray<float4>[math.max(1, ringSize)];
            }

            public bool IsReady(int slot, int requiredCount)
            {
                return slot >= 0 &&
                       slot < _data.Length &&
                       _data[slot].IsCreated &&
                       _data[slot].Length >= math.max(1, requiredCount);
            }

            public ref NativeArray<float4> Slot(int slot)
            {
                return ref _data[slot];
            }

            public bool EnsureAllCold(int requiredCount)
            {
                requiredCount = math.max(1, requiredCount);
                try
                {
                    for (int i = 0; i < _data.Length; i++)
                        EnsureSlotCold(i, requiredCount);
                    return true;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void EnsureSlotCold(int slot, int requiredCount)
            {
                if (IsReady(slot, requiredCount))
                    return;

                DisposeSlot(slot);
                NativeArray<float4> array = default;
                int sentinelId = 0;
                try
                {
                    array = new NativeArray<float4>(
                        requiredCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory);
                    sentinelId = NativeMemorySentinel.RegisterNativeArray(
                        array,
                        NativeMemoryOwner,
                        GpuReadbackDataLabel,
                        NativeMemoryLifetime);
                    if (sentinelId <= 0)
                        throw new InvalidOperationException("Native memory sentinel registration failed for GPU readback data.");

                    _data[slot] = array;
                }
                catch
                {
                    if (array.IsCreated)
                    {
                        System.Exception nativeSentinelCleanupException0 = null;

                        if (sentinelId > 0)
                        {
                            try
                            {
                                NativeMemorySentinel.Unregister(sentinelId);
                            }
                            catch (System.Exception nativeSentinelException0)
                            {
                                nativeSentinelCleanupException0 = nativeSentinelException0;
                            }
                            finally
                            {
                                sentinelId = 0;
                            }
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

                        if (nativeSentinelCleanupException0 != null)
                            throw nativeSentinelCleanupException0;
                    }

                    throw;
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < _data.Length; i++)
                    DisposeSlot(i);
            }

            private unsafe void DisposeSlot(int slot)
            {
                if (slot < 0 || slot >= _data.Length || !_data[slot].IsCreated)
                    return;

                void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_data[slot]);
                Exception firstException = null;

                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }

                try
                {
                    _data[slot].Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    _data[slot] = default;
                }

                if (firstException != null)
                    throw firstException;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct GpuBuoyancyObjectData
        {
            [FieldOffset(0)]
            public float Volume;
            [FieldOffset(4)]
            public float Height;
            [FieldOffset(8)]
            public float IsInAir;
            [FieldOffset(12)]
            public float SimplifiedSubmersion;
            [FieldOffset(16)]
            public float3 BoundsCenterWS;
            [FieldOffset(28)]
            public float BoundsPadding0;
            [FieldOffset(32)]
            public float3 BoundsExtentsWS;
            [FieldOffset(44)]
            public float BoundsPadding1;
            [FieldOffset(48)]
            private ulong _pad0;
            [FieldOffset(56)]
            private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct GpuHeatSourceData
        {
            [FieldOffset(0)]
            public float3 PositionWS;
            [FieldOffset(12)]
            public float Intensity;
            [FieldOffset(16)]
            public float Radius;
            [FieldOffset(20)]
            public float3 Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AbyssalFlowTelemetryEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public float FixedTime;
            [FieldOffset(8)]
            public float3 CenterWS;
            [FieldOffset(20)]
            public float3 WakePositionWS;
            [FieldOffset(32)]
            public float3 WakeVelocityWS;
            [FieldOffset(44)]
            public float WakeRadius;
            [FieldOffset(48)]
            public int HeatSourceCount;
            [FieldOffset(52)]
            public int FluidImpulseCount;
            [FieldOffset(56)]
            public uint Flags;
            [FieldOffset(60)]
            public uint StateHash;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct MaelstromTelemetryEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public float FixedTime;
            [FieldOffset(8)]
            public float3 PrimaryCenterWS;
            [FieldOffset(20)]
            public float PrimaryRadius;
            [FieldOffset(24)]
            public float4 PrimaryCompact;
            [FieldOffset(40)]
            public float Warp01;
            [FieldOffset(44)]
            public int ActiveCount;
            [FieldOffset(48)]
            public uint Flags;
            [FieldOffset(52)]
            public uint StateHash;
            [FieldOffset(56)]
            public float EscapeVelocityClamp;
            [FieldOffset(60)]
            public float EventHorizonRadius;
        }

        [StructLayout(LayoutKind.Explicit, Size = FluidSovereigntyTelemetryEntrySizeBytes)]
        private struct FluidTelemetryEntry
        {
            [FieldOffset(0)]
            public long VaultAllocatedBytes;
            [FieldOffset(8)]
            public long VaultArenaBytes;
            [FieldOffset(16)]
            public uint Frame;
            [FieldOffset(20)]
            public uint BufferId;
            [FieldOffset(24)]
            public uint Generation;
            [FieldOffset(28)]
            public uint VaultGenerationId;
            [FieldOffset(32)]
            public uint Flags;
            [FieldOffset(36)]
            public uint StateHash;
            [FieldOffset(40)]
            public int ExpectedLength;
            [FieldOffset(44)]
            public int ActualLength;
            [FieldOffset(48)]
            public float GlobalQualityWeight;
            [FieldOffset(52)]
            public float CpuMicroseconds;
            [FieldOffset(56)]
            public float GpuMicroseconds;
            [FieldOffset(60)]
            public float ActiveFlowRate;
        }

        private struct AbyssalVortexImpulse
        {
            public Vector3 PositionWS;
            public Vector3 AxisWS;
            public float RadiusMeters;
            public float StrengthMetersPerSecond;
            public float DurationSeconds;
            public float RemainingSeconds;
        }

        private struct CavitationBurstEvent
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Intensity01;
            public float Radius;
            public float RadiusSq;
            public float InvRadiusSq;
            public float Acceleration;
            public int SourceBodyInstanceId;
        }

        private static readonly int _GpuBuoyancyPositionsId = Shader.PropertyToID("_GpuBuoyancyPositions");
        private static readonly int _GpuBuoyancyObjectDataId = Shader.PropertyToID("_GpuBuoyancyObjectData");
        private static readonly int _GpuBuoyancyResultsId = Shader.PropertyToID("_GpuBuoyancyResults");
        private static readonly int _GpuBuoyancyObjectCountId = Shader.PropertyToID("_GpuBuoyancyObjectCount");
        private static readonly int _GpuBuoyancyWaterParamsId = Shader.PropertyToID("_GpuBuoyancyWaterParams");
        private static readonly int _GpuBuoyancyWave0AId = Shader.PropertyToID("_GpuBuoyancyWave0A");
        private static readonly int _GpuBuoyancyWave0BId = Shader.PropertyToID("_GpuBuoyancyWave0B");
        private static readonly int _GpuBuoyancyWave1AId = Shader.PropertyToID("_GpuBuoyancyWave1A");
        private static readonly int _GpuBuoyancyWave1BId = Shader.PropertyToID("_GpuBuoyancyWave1B");
        private static readonly int _GpuBuoyancyWave2AId = Shader.PropertyToID("_GpuBuoyancyWave2A");
        private static readonly int _GpuBuoyancyWave2BId = Shader.PropertyToID("_GpuBuoyancyWave2B");
        private static readonly int _OceanSurfaceWave0AId = Shader.PropertyToID("_HectonOceanSurfaceWave0A");
        private static readonly int _OceanSurfaceWave0BId = Shader.PropertyToID("_HectonOceanSurfaceWave0B");
        private static readonly int _OceanSurfaceWave1AId = Shader.PropertyToID("_HectonOceanSurfaceWave1A");
        private static readonly int _OceanSurfaceWave1BId = Shader.PropertyToID("_HectonOceanSurfaceWave1B");
        private static readonly int _OceanSurfaceWave2AId = Shader.PropertyToID("_HectonOceanSurfaceWave2A");
        private static readonly int _OceanSurfaceWave2BId = Shader.PropertyToID("_HectonOceanSurfaceWave2B");
        private static readonly int _OceanSurfaceWaveMetaId = Shader.PropertyToID("_HectonOceanSurfaceWaveMeta");
        private static readonly int _AbyssalFlowFieldResultId = Shader.PropertyToID("_AbyssalFlowFieldResult");
        private static readonly int _AbyssalHeatSourcesId = Shader.PropertyToID("_AbyssalHeatSources");
        private static readonly int _AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
        private static readonly int _AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
        private static readonly int _AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
        private static readonly int _AbyssalFlowWeatherCurrentId = Shader.PropertyToID("_AbyssalFlowWeatherCurrent");
        private static readonly int _AbyssalFlowWeatherWindId = Shader.PropertyToID("_AbyssalFlowWeatherWind");
        private static readonly int _AbyssalFlowWeatherParamsId = Shader.PropertyToID("_AbyssalFlowWeatherParams");
        private static readonly int _AbyssalFlowSurfaceYId = Shader.PropertyToID("_AbyssalFlowSurfaceY");
        private static readonly int _CurrentWaterLevelId = Shader.PropertyToID("_CurrentWaterLevel");
        private static readonly int _CurrentWaterLevelYId = Shader.PropertyToID("_CurrentWaterLevelY");
        private static readonly int _PrebakedVectorNoise3DId = Shader.PropertyToID("_HectonPrebakedVectorNoise3D");
        private static readonly int _AbyssalFlowThermoclineYId = Shader.PropertyToID("_AbyssalFlowThermoclineY");
        private static readonly int _AbyssalFlowHeatSourceCountId = Shader.PropertyToID("_AbyssalFlowHeatSourceCount");
        private static readonly int _AbyssalFlowWeatherStateMaskId = Shader.PropertyToID("_AbyssalFlowWeatherStateMask");
        private static readonly int _AbyssalFlowUpdateBucketId = Shader.PropertyToID("_AbyssalFlowUpdateBucket");
        private static readonly int _AbyssalFlowUpdateBucketMaskId = Shader.PropertyToID("_AbyssalFlowUpdateBucketMask");
        private static readonly int _AbyssalFlowFieldTextureId = Shader.PropertyToID("_AbyssalFlowFieldTexture");
        private static readonly int _AbyssalFlowTextureReadId = Shader.PropertyToID("_AbyssalFlowTextureRead");
        private static readonly int _AbyssalFlowTextureWriteId = Shader.PropertyToID("_AbyssalFlowTextureWrite");
        private static readonly int _AbyssalFlowTextureRWId = Shader.PropertyToID("_AbyssalFlowTextureRW");
        private static readonly int _AbyssalFlowTextureParamsId = Shader.PropertyToID("_AbyssalFlowTextureParams");
        private static readonly int _AbyssalFlowInterpolationAlphaId = Shader.PropertyToID("_AbyssalFlowInterpolationAlpha");
        private static readonly int _AbyssalFlowNoiseOffsetId = Shader.PropertyToID("_AbyssalFlowNoiseOffset");
        private static readonly int _AbyssalFlowWakeSphereId = Shader.PropertyToID("_AbyssalFlowWakeSphere");
        private static readonly int _AbyssalFlowWakeVelocityId = Shader.PropertyToID("_AbyssalFlowWakeVelocity");
        private static readonly int _AbyssalFlowVortexSphereId = Shader.PropertyToID("_AbyssalFlowVortexSphere");
        private static readonly int _AbyssalFlowVortexAxisStrengthId = Shader.PropertyToID("_AbyssalFlowVortexAxisStrength");
        private static readonly int _AbyssalSplashdownImpulseBufferId = Shader.PropertyToID("_AbyssalSplashdownImpulseBuffer");
        private static readonly int _AbyssalSplashdownParamsId = Shader.PropertyToID("_AbyssalSplashdownParams");
        private static readonly int _AbyssalFlowTextureActiveId = Shader.PropertyToID("_AbyssalFlowTextureActive");
        private static readonly int _SiltReadId = Shader.PropertyToID("_SiltRead");
        private static readonly int _SiltWriteId = Shader.PropertyToID("_SiltWrite");
        private static readonly int _BubbleReadId = Shader.PropertyToID("_BubbleRead");
        private static readonly int _BubbleWriteId = Shader.PropertyToID("_BubbleWrite");
        private static readonly int _DebrisReadId = Shader.PropertyToID("_DebrisRead");
        private static readonly int _DebrisWriteId = Shader.PropertyToID("_DebrisWrite");
        private static readonly int _VoxelSdfTexture3DId = Shader.PropertyToID("_VoxelSdfTexture3D");
        private static readonly int _VoxelSdfWorldToLocalId = Shader.PropertyToID("_VoxelSdfWorldToLocal");
        private static readonly int _VoxelSdfInvDoubleHalfExtentsId = Shader.PropertyToID("_VoxelSdfInvDoubleHalfExtents");
        private static readonly int _FluidAdvectionCountsId = Shader.PropertyToID("_FluidAdvectionCounts");
        private static readonly int _FluidAdvectionParamsId = Shader.PropertyToID("_FluidAdvectionParams");
        private static readonly int _FluidAdvectionBuoyancyId = Shader.PropertyToID("_FluidAdvectionBuoyancy");
        private static readonly int _FluidAdvectionAupShiftDeltaId = Shader.PropertyToID("_FluidAdvectionAupShiftDelta");
        private static readonly int _FluidAdvectionSdfParamsId = Shader.PropertyToID("_FluidAdvectionSdfParams");
        private static readonly int _DynamicWakesId = Shader.PropertyToID("_DynamicWakes");
        private static readonly int _DynamicWakeVectorsId = Shader.PropertyToID("_DynamicWakeVectors");
        private static readonly int _DynamicWakeParamsId = Shader.PropertyToID("_DynamicWakeParams");
        private static readonly ProfilerMarker _gatherDataProfilerMarker = new ProfilerMarker("H8.Fluid.GatherData");
        private static readonly ProfilerMarker _jobScheduleProfilerMarker = new ProfilerMarker("H8.Fluid.ScheduleBuoyancyJob");
        private static readonly ProfilerMarker _scheduledApplyProfilerMarker = new ProfilerMarker("H8.Fluid.ApplyScheduledForces");
        private static readonly ProfilerMarker _gpuReadbackProfilerMarker = new ProfilerMarker("H8.Fluid.ConsumeGpuReadback");
        private static readonly int _buoyancyForceNanErrorCode = unchecked((int)Hecton.Localization.LocHash.Compute("NAN_ERROR_HASH_BUOYANCY_FORCE"));
        private static readonly int _buoyancyTorqueNanErrorCode = unchecked((int)Hecton.Localization.LocHash.Compute("NAN_ERROR_HASH_BUOYANCY_TORQUE"));
        private static HectonFluidEngine s_runtimeInstance;
        private static IDataVault s_staticFluidDataVault;
        private struct FluidVaultBuffer<T> where T : struct
        {
            private VaultGenerationHandle<T> _handle;
            private BufferID _bufferId;
            private int _requiredLength;
            private IDataVault _writeLockVault;

            public bool IsCreated
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return TryResolve(out NativeArray<T> buffer) && buffer.IsCreated;
                }
            }

            public int Length
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return TryResolve(out NativeArray<T> buffer) ? buffer.Length : 0;
                }
            }

            public VaultGenerationHandle<T> Handle
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _handle; }
            }

            public BufferID BufferId
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _bufferId; }
            }

            public T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return TryResolve(out NativeArray<T> buffer) && (uint)index < (uint)buffer.Length
                        ? buffer[index]
                        : default;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    IDataVault vault = ResolveStaticFluidDataVault();
                    if (vault == null ||
                        !IsHandleUsable(in _handle, _bufferId) ||
                        !vault.TryAcquireWriteLock(in _handle, SystemID.Fluid, out NativeArray<T> buffer))
                    {
                        HectonFluidEngine instance = s_runtimeInstance;
                        if (instance != null &&
                            _bufferId != FluidSovereigntyTelemetryRingBufferId &&
                            _bufferId != FluidSovereigntyTelemetryCursorBufferId)
                        {
                            instance.RecordFluidSovereigntyTelemetry(
                                _bufferId,
                                FluidTelemetryFlagWriteLockContention,
                                _requiredLength,
                                0,
                                0f,
                                0f,
                                0f);
                        }
                        return;
                    }

                    try
                    {
                        if ((uint)index < (uint)buffer.Length)
                            buffer[index] = value;
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in _handle, SystemID.Fluid);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Ensure(BufferID bufferId, int requiredLength, NativeArrayOptions options)
            {
                IDataVault vault = ResolveStaticFluidDataVault();
                if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || requiredLength <= 0)
                    return false;

                _bufferId = bufferId;
                _requiredLength = requiredLength;
                if (!TryResolve(out NativeArray<T> current) || current.Length < requiredLength)
                {
                    _handle = vault.EnsureGenerationHandle<T>(
                        bufferId,
                        requiredLength,
                        SystemID.Fluid,
                        options);
                }

                return TryResolve(out NativeArray<T> buffer) && buffer.IsCreated && buffer.Length >= requiredLength;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryResolve(out NativeArray<T> buffer)
            {
                buffer = default;
                IDataVault vault = ResolveStaticFluidDataVault();
                if (vault == null)
                    return false;

                if (!IsHandleUsable(in _handle, _bufferId))
                {
                    if (_bufferId == BufferID.Unknown ||
                        !vault.TryGetGenerationHandle(_bufferId, out _handle))
                    {
                        return false;
                    }
                }

                if (vault.TryResolveHandle(in _handle, out buffer) &&
                    buffer.IsCreated &&
                    (_requiredLength <= 0 || buffer.Length >= _requiredLength))
                {
                    return true;
                }

                if (_bufferId == BufferID.Unknown ||
                    !vault.TryGetGenerationHandle(_bufferId, out _handle))
                {
                    buffer = default;
                    return false;
                }

                return vault.TryResolveHandle(in _handle, out buffer) &&
                       buffer.IsCreated &&
                       (_requiredLength <= 0 || buffer.Length >= _requiredLength);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public NativeArray<T>.ReadOnly AsReadOnly()
            {
                IDataVault vault = ResolveStaticFluidDataVault();
                if (vault == null)
                    return default;

                if (!IsHandleUsable(in _handle, _bufferId))
                {
                    if (_bufferId == BufferID.Unknown ||
                        !vault.TryGetGenerationHandle(_bufferId, out _handle))
                    {
                        return default;
                    }
                }

                if (!vault.TryReadOnlyHandle(in _handle, out NativeArray<T>.ReadOnly buffer))
                {
                    if (_bufferId == BufferID.Unknown ||
                        !vault.TryGetGenerationHandle(_bufferId, out _handle) ||
                        !vault.TryReadOnlyHandle(in _handle, out buffer))
                    {
                        return default;
                    }
                }

                if (!buffer.IsCreated || (_requiredLength > 0 && buffer.Length < _requiredLength))
                    return default;

                return buffer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryAcquireWriteLock(out NativeArray<T> buffer)
            {
                buffer = default;
                if (_writeLockVault != null)
                    return false;

                IDataVault vault = ResolveStaticFluidDataVault();
                if (vault == null)
                    return false;

                if (!IsHandleUsable(in _handle, _bufferId))
                {
                    if (_bufferId == BufferID.Unknown ||
                        !vault.TryGetGenerationHandle(_bufferId, out _handle))
                    {
                        return false;
                    }
                }

                if (!vault.TryAcquireWriteLock(in _handle, SystemID.Fluid, out buffer))
                    return false;

                bool ownershipTransferred = false;
                try
                {
                    if (buffer.IsCreated && (_requiredLength <= 0 || buffer.Length >= _requiredLength))
                    {
                        _writeLockVault = vault;
                        ownershipTransferred = true;
                        return true;
                    }

                    buffer = default;
                    return false;
                }
                finally
                {
                    if (!ownershipTransferred)
                        vault.ReleaseWriteLock(in _handle, SystemID.Fluid);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ReleaseWriteLock()
            {
                IDataVault vault = _writeLockVault;
                if (vault == null)
                    return;

                _writeLockVault = null;
                if (vault != null && IsHandleUsable(in _handle, _bufferId))
                    vault.ReleaseWriteLock(in _handle, SystemID.Fluid);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Release()
            {
                IDataVault writeLockVault = _writeLockVault;
                if (writeLockVault != null && IsHandleUsable(in _handle, _bufferId))
                    writeLockVault.ReleaseWriteLock(in _handle, SystemID.Fluid);

                _writeLockVault = null;
                IDataVault vault = ResolveStaticFluidDataVault();
                if (vault != null && IsHandleUsable(in _handle, _bufferId))
                    vault.ReleaseBuffer(in _handle);

                _handle = default;
                _bufferId = BufferID.Unknown;
                _requiredLength = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                _writeLockVault = null;
                _handle = default;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator NativeArray<T>(FluidVaultBuffer<T> buffer)
            {
                return buffer.TryResolve(out NativeArray<T> resolved) ? resolved : default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IDataVault ResolveStaticFluidDataVault()
        {
            HectonFluidEngine instance = s_runtimeInstance;
            return instance != null ? instance._dataVault : s_staticFluidDataVault;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleUsable<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Fluid &&
                   handle.Generation != 0u;
        }
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtimeInstance = null;
            s_staticFluidDataVault = null;

            for (int i = 0; i < CavitationShockwaveHitCapacity; i++)
            {
                s_CavitationShockwaveContacts[i] = default;
                s_CavitationShockwaveRigidbodies[i] = null;
            }
        }

        public static HectonFluidEngine Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return null;
#endif
                HectonFluidEngine instance = s_runtimeInstance;
                if (instance != null)
                    return instance;

                instance = GlobalRegistry.Fluid;
                s_runtimeInstance = instance;
                return instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — WATER
        // ══════════════════════════════════════════════════════════

        [Header("── Water ─────────────────────────────────────")]
        [Tooltip("World-space Y coordinate of the water surface.")]
        [SerializeField] private float waterLevel = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        [SerializeField] private bool enableCinematicTideShift = true;
        [SerializeField, Range(0f, 8f)] private float cinematicTideAmplitudeMeters = 2f;

        [Tooltip("Plotnost vody (kg/m³). Presnaya = 1000, Morskaya = 1025")]
        [SerializeField] private float waterDensity = 1000f;

        [Tooltip("Koeffitsient vyazkogo soprotivleniya. " +
                 "Chem bolshe — tem silnee tormozhenie pod vodoy.")]
        [SerializeField] private float viscousDrag = 3f;
        [SerializeField, Min(0f)] private float maxQuadraticDragForcePerKg = 180f;

        [Tooltip("Angular drag coefficient for submerged object rotation damping.")]
        [SerializeField] private float angularDrag = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CURRENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Currents ──────────────────────────────────")]
        [Tooltip("Global underwater current vector in m/s applied to submerged objects.")]
        [SerializeField] private Vector3 currentVector = Vector3.zero;

        [Tooltip("Current influence multiplier.")]
        [SerializeField] private float currentStrength = 1f;
        [SerializeField] private bool enablePhantomCurrent = true;
        [SerializeField] private float currentNoiseScale = 0.018f;
        [SerializeField] private float currentTimeScale = 0.12f;
        [SerializeField, Range(0f, 1f)] private float currentVerticalFactor = 0.18f;
        [SerializeField] private float phantomCurrentStrength = 0.9f;
        [SerializeField] private bool enablePrebakedVectorNoise = true;
        [SerializeField, Min(0.25f)] private float prebakedVectorNoiseCellSizeMeters = 48f;
        [SerializeField, Range(0f, 1f)] private float prebakedVectorNoiseTriangleModulation = 0.35f;
        [SerializeField] private int prebakedVectorNoiseSeed = 1828914165;
        [SerializeField, Tooltip("Authored 32x32x32 RGBAHalf curl-noise Texture3D for fluid shader globals. Runtime Texture3D synthesis is forbidden.")]
        private Texture3D authoredPrebakedVectorNoiseTexture;

        [Header("-- Analytical Flow Field -----------------------")]
        [SerializeField] private bool enableAnalyticalFlowField = true;
        [SerializeField, Min(0.01f)] private float haloclineBoundaryDepthMeters = 200f;
        [SerializeField, Min(1f)] private float deepLayerDensityMultiplier = 1.5f;
        [SerializeField] private float haloclineShearForcePerKg = 4f;
        [SerializeField] private bool enableDynamicViscosityRegions = true;

        [Header("-- Giant's Wake -----------------------")]
        [Tooltip("Adds a subtle abyssal current bias from the parent gas giant sky direction.")]
        [SerializeField] private bool enableGiantWakeCurrent = true;
        [Tooltip("Meters-per-second current bias applied when deep enough below the water surface.")]
        [SerializeField, Min(0f)] private float giantWakeCurrentStrength = 0.18f;
        [Tooltip("Vertical component mixed into the horizontal planet-facing wake direction.")]
        [SerializeField, Range(-1f, 1f)] private float giantWakeVerticalBias = -0.04f;
        [Tooltip("Depth below water surface where the wake starts contributing.")]
        [SerializeField, Min(0f)] private float giantWakeDepthFadeStart = 120f;
        [Tooltip("Depth span used to fade the wake from zero to full strength.")]
        [SerializeField, Min(1f)] private float giantWakeDepthFadeRange = 480f;
        [Tooltip("Adds chaotic torque where Aegir wake and local abyssal currents shear across each other.")]
        [SerializeField] private bool enableTidalShearZones = true;
        [Tooltip("Torque scalar applied inside wake/current shear zones.")]
        [SerializeField, Min(0f)] private float tidalShearTorqueStrength = 18f;
        [Tooltip("Temporal frequency for deterministic shear-zone tumble.")]
        [SerializeField, Min(0.01f)] private float tidalShearFrequency = 1.7f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PERFORMANCE
        // ══════════════════════════════════════════════════════════

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Minimum job batch size. Lower values increase parallelism; higher values reduce scheduling overhead.")]
        [SerializeField] private int jobBatchSize = 32;
        [Tooltip("Owner-phase native/DataVault buoyancy capacity prewarmed at startup. Runtime FixedTick never grows this.")]
        [SerializeField, Range(128, 2048)] private int prewarmedBuoyancyCapacity = 512;
        [SerializeField] private bool enableDistanceLod = true;
        [SerializeField] private Transform lodObserver;
        [SerializeField] private float nearLodDistance = 20f;
        [SerializeField] private float mediumLodDistance = 45f;
        [SerializeField] private float farLodDistance = 90f;
        [SerializeField] private float cullLodDistance = 160f;
        [SerializeField, Range(1, 8)] private int mediumLodDivisor = 2;
        [SerializeField, Range(1, 16)] private int farLodDivisor = 4;
        [SerializeField, Range(1, 32)] private int cullLodDivisor = 8;
        [SerializeField] private bool enableBiomeBuoyancyInfluence = true;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugObjectCount;
        [SerializeField] private int _debugNearCount;
        [SerializeField] private int _debugMediumCount;
        [SerializeField] private int _debugFarCount;
        [SerializeField] private int _debugCulledCount;
        [SerializeField] private int _debugCurrentVolumeCount;
        [SerializeField] private bool drawLodGizmos = true;
        [SerializeField] private bool drawCurrentVectors = true;
        [SerializeField] private float gizmoCurrentVectorScale = 4f;
        [SerializeField] private int _debugAbyssalHeatSourceCount;
        [SerializeField] private Vector3 _debugGiantWakeCurrent;
        private float3 _resolvedGiantWakeCurrent;

        [Header("â”€â”€ GPU Buoyancy Offload â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private bool enableGpuBuoyancySampling;
        [SerializeField] private ComputeShader gpuBuoyancyCompute;
        [SerializeField, Range(64, 1024)] private int gpuBuoyancyActivationThreshold = 256;
        [SerializeField] private bool enableGpuAbyssalFlowField = true;
        [SerializeField] private ComputeShader abyssalFlowFieldCompute;
        [SerializeField] private ComputeShader fluidAdvectionCompute;
        [SerializeField, Tooltip("Authored 1x1x1 neutral Texture3D bound to inactive fluid advection slots. Runtime fallback texture synthesis is forbidden.")]
        private Texture3D authoredEmptyFluidAdvectionTexture;
        [SerializeField, Range(8, 32)] private int abyssalFlowHorizontalResolution = 16;
        [SerializeField, Range(4, 24)] private int abyssalFlowVerticalResolution = 12;
        [SerializeField, Range(4f, 32f)] private float abyssalFlowHorizontalCellSize = 12f;
        [SerializeField, Range(4f, 24f)] private float abyssalFlowVerticalCellSize = 10f;
        [SerializeField, Range(4f, 40f)] private float abyssalHeatProbeRadius = 16f;
        [SerializeField, Range(0.1f, 64f)] private float abyssalHeatIntensityNormalization = 18f;

        [Header("-- Cavitation -----------------------")]
        [Tooltip("Optional particle system used for thruster cavitation bubble bursts.")]
        [SerializeField] private ParticleSystem cavitationBubbleParticles;
        [Tooltip("Particle count emitted by a full-intensity cavitation burst.")]
        [SerializeField, Range(1, 128)] private int cavitationBubbleEmitCountAtFullIntensity = 42;
        [Tooltip("Layer mask for small fauna or loose bodies affected by cavitation shockwaves.")]
        [SerializeField] private LayerMask cavitationShockwaveLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [Tooltip("Maximum Rigidbody mass affected by cavitation collapse so large props and the submarine are ignored.")]
        [SerializeField, Min(0.1f)] private float cavitationShockwaveMaxAffectedMassKg = 120f;
        [Tooltip("Upward lift mixed into cavitation shockwave direction.")]
        [SerializeField, Range(0f, 1f)] private float cavitationShockwaveVerticalLift = 0.12f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct AdvectedSilt
        {
            [FieldOffset(0)]
            public float3 PositionWS;
            [FieldOffset(12)]
            public float Life;
            [FieldOffset(16)]
            public float3 VelocityWS;
            [FieldOffset(28)]
            public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct AdvectedBubble
        {
            [FieldOffset(0)]
            public float3 PositionWS;
            [FieldOffset(12)]
            public float Life;
            [FieldOffset(16)]
            public float3 VelocityWS;
            [FieldOffset(28)]
            public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct AdvectedDebris
        {
            [FieldOffset(0)]
            public float3 PositionWS;
            [FieldOffset(12)]
            public float Life;
            [FieldOffset(16)]
            public float3 VelocityWS;
            [FieldOffset(28)]
            public uint Flags;
        }

        /// <summary>Y-koordinata poverhnosti vody.</summary>
        public float WaterLevel
        {
            get => ResolveBaseWaterLevelY();
            set
            {
                waterLevel = value;
                PublishCurrentWaterLevelUniform();
            }
        }

        /// <summary>Cinematic surface water level consumed by shader/UI/physics bridges.</summary>
        public float CurrentWaterLevelY
        {
            get { return ReadPublishedCurrentWaterLevelY(); }
        }

        /// <summary>Plotnost vody (kg/m³).</summary>
        public float WaterDensity
        {
            get => waterDensity;
            set => waterDensity = math.max(0.01f, value);
        }

        /// <summary>Vektor techeniya (m/s). Izmenyaetsya v rantayme.</summary>
        public Vector3 CurrentVector
        {
            get => currentVector;
            set
            {
                currentVector = value;
                OnCurrentSettingsChanged();
            }
        }

        public int MaxActiveMaelstromCapacity => MaxActiveMaelstromCount;

        public void ApplyWeatherCurrent(Vector3 vector, float strength)
        {
            currentVector = vector;
            currentStrength = math.max(0f, strength);
            OnCurrentSettingsChanged();
        }

        /// <summary>Sila globalnogo techeniya.</summary>
        public float CurrentStrength
        {
            get => currentStrength;
            set
            {
                currentStrength = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Vklyucheno li phantom techenie.</summary>
        public bool EnablePhantomCurrent
        {
            get => enablePhantomCurrent;
            set
            {
                enablePhantomCurrent = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Masshtab shuma phantom techeniya.</summary>
        public float CurrentNoiseScale
        {
            get => currentNoiseScale;
            set
            {
                currentNoiseScale = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Vremennoy masshtab phantom techeniya.</summary>
        public float CurrentTimeScale
        {
            get => currentTimeScale;
            set
            {
                currentTimeScale = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Vertikalnyy faktor phantom techeniya.</summary>
        public float CurrentVerticalFactor
        {
            get => currentVerticalFactor;
            set
            {
                currentVerticalFactor = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Sila phantom techeniya.</summary>
        public float PhantomCurrentStrength
        {
            get => phantomCurrentStrength;
            set
            {
                phantomCurrentStrength = value;
                OnCurrentSettingsChanged();
            }
        }

        /// <summary>Kolichestvo zaregistrirovannyh obektov.</summary>
        public int ObjectCount => _objects.Count;
        public NativeArray<float3>.ReadOnly FloaterPositions => _positions.AsReadOnly();
        public NativeArray<float>.ReadOnly BuoyancyResults => _waveOffsets.AsReadOnly();

        public Vector3 GiantWakeCurrent => _debugGiantWakeCurrent;

        /// <summary>
        /// Queues one thruster cavitation burst for post-fixed particle emission and shockwave force routing.
        /// </summary>
        /// <param name="position">World-space burst origin.</param>
        /// <param name="direction">Preferred burst direction from the thruster exhaust.</param>
        /// <param name="intensity01">Cavitation intensity in the 0..1 range.</param>
        /// <param name="radius">Shockwave radius in meters.</param>
        /// <param name="acceleration">Shockwave velocity-change magnitude routed through PhysicsApplySystem.</param>
        /// <param name="sourceBodyInstanceId">Rigidbody instance ID to ignore, usually the submarine body.</param>
        /// <returns>True when the fixed-capacity burst queue accepted the event.</returns>
        public static bool QueueCavitationBurst(
            Vector3 position,
            Vector3 direction,
            float intensity01,
            float radius,
            float acceleration,
            int sourceBodyInstanceId)
        {
            HectonFluidEngine instance = Instance;
            return instance != null &&
                   instance.EnqueueCavitationBurst(position, direction, intensity01, radius, acceleration, sourceBodyInstanceId);
        }

        public bool TryQueueAdvectedBubbleBurst(Vector3 runtimePosition, int requestedCount, float intensity01)
        {
            if (!Application.isPlaying ||
                requestedCount <= 0 ||
                !math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z))))
            {
                return false;
            }

            RefreshFluidAdvectionStateReadyCached();
            if (!IsFluidAdvectionStorageReady())
                return false;

            ClearPendingFluidAdvectionShiftIfNoActiveParticles();

            int safeCount = math.min(requestedCount, MaxAdvectedBubbleCount);
            float finiteIntensity = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            for (int i = 0; i < safeCount; i++)
            {
                int slot = _advectedBubbleWriteCursor;
                _advectedBubbleWriteCursor = (_advectedBubbleWriteCursor + 1) % MaxAdvectedBubbleCount;
                _activeAdvectedBubbleCount = math.min(MaxAdvectedBubbleCount, _activeAdvectedBubbleCount + 1);

                float3 offset = ResolveSpawnJitter(slot, BubbleBurstSpawnRadiusMeters * (0.5f + finiteIntensity));
                AdvectedBubble bubble = new AdvectedBubble
                {
                    PositionWS = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + offset,
                    Life = 1f,
                    VelocityWS = new float3(offset.x * 0.3f, BubbleAdvectionBuoyancyMetersPerSecond, offset.z * 0.3f),
                    Flags = AdvectedBubbleActiveFlag
                };
                UploadAdvectedBubble(slot, in bubble);
            }

            return true;
        }

        public Vector3 GetFlowAtPosition(Vector3 position)
        {
            float3 flow = GetFlowAtPosition(new float3(position.x, position.y, position.z));
            return new Vector3(flow.x, flow.y, flow.z);
        }

        public float3 GetFlowAtPosition(float3 position)
        {
            if (!math.all(math.isfinite(position)) || !enableAnalyticalFlowField)
                return float3.zero;

            WeatherRuntimeSnapshot weatherSnapshot = ReadPublishedWeatherSnapshot();
            float resolvedWaterLevel = ReadPublishedCurrentWaterLevelY();
            float3 baseCurrent = new float3(
                currentVector.x * currentStrength,
                currentVector.y * currentStrength,
                currentVector.z * currentStrength);
            float depthBelowSurface = math.max(0f, resolvedWaterLevel - position.y);
            var vectorNoiseField = _prebakedVectorNoiseField.IsCreated
                ? _prebakedVectorNoiseField
                : default;
            int vectorNoiseLength = _prebakedVectorNoiseField.IsCreated ? _prebakedVectorNoiseField.Length : 0;
            double3 aupOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            byte detailedMathEnabled = AuthorityFluidDetailedMathEnabled;
            float3 flow = HectonAnalyticalFlowField.SampleBaseFlow(
                position,
                depthBelowSurface,
                baseCurrent,
                _resolvedGiantWakeCurrent,
                giantWakeDepthFadeStart,
                giantWakeDepthFadeRange,
                (uint)weatherSnapshot.StateMask,
                weatherSnapshot.CurrentMeta.GlobalBaseVector,
                weatherSnapshot.CurrentMeta.GlobalScale,
                weatherSnapshot.WeatherIntensity,
                enablePhantomCurrent ? (byte)1 : (byte)0,
                currentNoiseScale,
                currentTimeScale,
                currentVerticalFactor,
                phantomCurrentStrength,
                ResolveWaterLevelTimeSeconds(in weatherSnapshot),
                haloclineBoundaryDepthMeters,
                haloclineShearForcePerKg,
                vectorNoiseField,
                vectorNoiseLength,
                aupOffset,
                math.rcp(math.max(0.25f, prebakedVectorNoiseCellSizeMeters)),
                enablePrebakedVectorNoise ? (byte)1 : (byte)0,
                prebakedVectorNoiseTriangleModulation,
                detailedMathEnabled);

            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
                HectonAnalyticalFlowField.ApplyThrusterFlow(ref flow, position, _thrusterFlowBuffer[i]);

            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
                HectonAnalyticalFlowField.ApplyWhirlpoolFlow(ref flow, position, _whirlpoolFlowBuffer[i], AuthorityFluidSimplifiedMathEnabled);

            return HectonAnalyticalFlowField.ResolveFiniteFloat3OrZero(flow);
        }

        public float3 SampleAnalyticalFlow(float3 samplePosition) => GetFlowAtPosition(samplePosition);

        public float GetWaterHeightAtPosition(Vector3 position)
        {
            return GetWaterHeightAtPosition(new float3(position.x, position.y, position.z));
        }

        public float GetWaterHeightAtPosition(float3 position)
        {
            WeatherRuntimeSnapshot weatherSnapshot = ReadPublishedWeatherSnapshot();
            double3 aupOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double2 absoluteXZ = new double2(
                (double)position.x + aupOffset.x,
                (double)position.z + aupOffset.z);
            float waveOffset = SampleWeatherGerstnerHeight(
                absoluteXZ,
                in weatherSnapshot,
                ResolveAuthorityGerstnerWaveBudget());
            return ReadPublishedCurrentWaterLevelY() + waveOffset;
        }

        public bool TrySetActiveThruster(
            int slot,
            Vector3 position,
            Vector3 direction,
            float strength,
            float radius,
            float coneDegrees)
        {
            if ((uint)slot >= MaxAnalyticalThrusterCount ||
                !IsFiniteVector(position) ||
                !IsFiniteVector(direction) ||
                direction.sqrMagnitude <= 0.0001f ||
                strength <= 0f ||
                radius <= 0f)
            {
                return false;
            }

            float3 rawDirection = new float3(direction.x, direction.y, direction.z);
            float3 normalizedDirection = NormalizeOrDefault(rawDirection, new float3(0f, 0f, 1f));
            float clampedConeDegrees = math.clamp(coneDegrees, 1f, 89f);
            float cone01 = clampedConeDegrees * 0.011111111f;
            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;
            _thrusterFlowBuffer[slot] = new ActiveThrusterFlow
            {
                PositionWS = new float3(position.x, position.y, position.z),
                DirectionWS = normalizedDirection,
                Strength = math.max(0f, strength),
                RadiusSq = radiusSq,
                InvRadiusSq = math.rcp(radiusSq),
                ConeCos = 1f - cone01 * cone01,
                Active = 1
            };
            OnCurrentSettingsChanged();
            return true;
        }

        public void ClearActiveThruster(int slot)
        {
            if ((uint)slot >= MaxAnalyticalThrusterCount)
                return;

            _thrusterFlowBuffer[slot] = default;
            OnCurrentSettingsChanged();
        }

        public void ClearActiveThrusters()
        {
            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
                _thrusterFlowBuffer[i] = default;
            OnCurrentSettingsChanged();
        }

        public bool TrySetWhirlpool(
            int slot,
            Vector3 center,
            float radius,
            float tangentialStrength,
            float centripetalStrength,
            float verticalPull)
        {
            if ((uint)slot >= MaxAnalyticalWhirlpoolCount ||
                !IsFiniteVector(center) ||
                !math.isfinite(radius) ||
                !math.isfinite(tangentialStrength) ||
                !math.isfinite(centripetalStrength) ||
                !math.isfinite(verticalPull) ||
                radius <= 0f)
            {
                return false;
            }

            float safeRadius = math.max(MaelstromMinimumRadiusMeters, radius);
            float radiusSq = safeRadius * safeRadius;
            float resolvedIntensity = ResolveMaelstromIntensity(tangentialStrength, centripetalStrength, verticalPull);
            float eventHorizonRadius = math.max(0.25f, safeRadius * MaelstromEventHorizonRadiusFactor);
            _whirlpoolFlowBuffer[slot] = new WhirlpoolFlow
            {
                CenterWS = new float3(center.x, center.y, center.z),
                RadiusSq = radiusSq,
                InvRadiusSq = math.rcp(radiusSq),
                TangentialStrength = tangentialStrength,
                CentripetalStrength = centripetalStrength,
                VerticalPull = math.max(0f, verticalPull),
                Active = 1,
                Padding0 = resolvedIntensity * math.rcp(safeRadius),
                Padding1 = safeRadius,
                Padding2 = eventHorizonRadius * eventHorizonRadius
            };
            if (!_scheduledBuoyancyJobActive)
                CopyAnalyticalFlowInputsToNative();
            OnCurrentSettingsChanged();
            return true;
        }

        public bool TrySetMaelstrom(
            int slot,
            Vector3 center,
            float radius,
            float pullStrength,
            float spinStrength,
            float verticalPull)
        {
            return TrySetWhirlpool(slot, center, radius, spinStrength, pullStrength, verticalPull);
        }

        public void ClearWhirlpool(int slot)
        {
            if ((uint)slot >= MaxAnalyticalWhirlpoolCount)
                return;

            _whirlpoolFlowBuffer[slot] = default;
            if (!_scheduledBuoyancyJobActive)
                CopyAnalyticalFlowInputsToNative();
            OnCurrentSettingsChanged();
        }

        public void ClearWhirlpools()
        {
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
                _whirlpoolFlowBuffer[i] = default;
            if (!_scheduledBuoyancyJobActive)
                CopyAnalyticalFlowInputsToNative();
            OnCurrentSettingsChanged();
        }

        public bool TryGetActiveMaelstroms(
            out NativeArray<float4>.ReadOnly maelstroms,
            out int activeCount,
            out Vector4 maelstromMeta)
        {
            maelstroms = _activeMaelstroms.AsReadOnly();
            activeCount = _activeMaelstromCount;
            maelstromMeta = _activeMaelstromMeta;
            return maelstroms.IsCreated && activeCount > 0;
        }

        public bool TryUploadActiveMaelstroms(GraphicsBuffer destination, int requestedCount)
        {
            if (destination == null || !_activeMaelstroms.IsCreated)
                return false;

            int safeCount = math.clamp(
                requestedCount,
                0,
                math.min(MaxActiveMaelstromCount, math.min(_activeMaelstromCount, _activeMaelstroms.Length)));
            if (safeCount <= 0)
                return false;

            GraphicsBufferUploadUtility.UploadNativeArray<float4>(destination, _activeMaelstroms, safeCount);
            return true;
        }

        public bool TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow>.ReadOnly whirlpools, out int activeCount)
        {
            whirlpools = _activeWhirlpools.AsReadOnly();
            activeCount = _activeWhirlpoolFlowCount;
            return whirlpools.IsCreated && activeCount > 0;
        }

        public bool TrySampleMaelstromWarp(Vector3 runtimePosition, out float warp01)
        {
            warp01 = 0f;
            if (!IsFiniteVector(runtimePosition))
                return false;

            float3 sample = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow whirlpool = _whirlpoolFlowBuffer[i];
                float sampleWarp01 = SampleMaelstromWarp01(whirlpool, sample);
                if (sampleWarp01 <= 0f)
                    continue;

                warp01 = math.max(warp01, sampleWarp01);
            }

            warp01 = math.saturate(warp01);
            return warp01 > 0.0001f;
        }

        /// <summary>
        /// Queues a bounded high-tier-only vortex impulse for large body/tail-whip wake events.
        /// </summary>
        /// <param name="position">Impulse center in runtime world space.</param>
        /// <param name="axis">Signed swirl axis; normalized to preserve authored vortex direction.</param>
        /// <param name="radiusMeters">Affected radius in meters.</param>
        /// <param name="strengthMetersPerSecond">Tangential velocity injected into the flow texture.</param>
        /// <param name="durationSeconds">Impulse lifetime before decay/removal.</param>
        /// <returns>True when the impulse was finite, bounded, and queued.</returns>
        /// <remarks>Low tier ages queued impulses without dispatching the vortex compute kernel.</remarks>
        public bool TryQueueAbyssalVortexImpulse(
            Vector3 position,
            Vector3 axis,
            float radiusMeters,
            float strengthMetersPerSecond,
            float durationSeconds)
        {
            if (!IsFiniteVector(position) ||
                !IsFiniteVector(axis) ||
                axis.sqrMagnitude <= 0.0001f ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(strengthMetersPerSecond) ||
                !math.isfinite(durationSeconds) ||
                radiusMeters < AbyssalVortexImpulseMinimumRadiusMeters ||
                math.abs(strengthMetersPerSecond) <= 0.001f ||
                durationSeconds <= 0f)
            {
                return false;
            }

            int slot = _abyssalVortexImpulseWriteIndex;
            _abyssalVortexImpulseWriteIndex = (_abyssalVortexImpulseWriteIndex + 1) % MaxAbyssalVortexImpulseCount;
            if (_abyssalVortexImpulseCount < MaxAbyssalVortexImpulseCount)
                _abyssalVortexImpulseCount++;

            float safeDuration = math.min(durationSeconds, AbyssalVortexImpulseMaximumDurationSeconds);
            float3 axis3 = new float3(axis.x, axis.y, axis.z);
            float3 normalizedAxis = NormalizeOrDefault(axis3, new float3(0f, 1f, 0f));
            _abyssalVortexImpulses[slot] = new AbyssalVortexImpulse
            {
                PositionWS = position,
                AxisWS = new Vector3(normalizedAxis.x, normalizedAxis.y, normalizedAxis.z),
                RadiusMeters = math.clamp(radiusMeters, AbyssalVortexImpulseMinimumRadiusMeters, AbyssalVortexImpulseMaximumRadiusMeters),
                StrengthMetersPerSecond = math.clamp(
                    strengthMetersPerSecond,
                    -AbyssalVortexImpulseMaximumStrengthMetersPerSecond,
                    AbyssalVortexImpulseMaximumStrengthMetersPerSecond),
                DurationSeconds = safeDuration,
                RemainingSeconds = safeDuration
            };
            return true;
        }

        public bool TrySetViscosityRegion(
            int slot,
            Vector3 center,
            float radius,
            float viscosityMultiplier)
        {
            if ((uint)slot >= MaxDynamicViscosityRegionCount ||
                !IsFiniteVector(center) ||
                radius <= 0f ||
                !math.isfinite(viscosityMultiplier) ||
                viscosityMultiplier <= 0f)
            {
                return false;
            }

            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;
            _viscosityRegionBuffer[slot] = new FluidViscosityRegion
            {
                CenterWS = new float3(center.x, center.y, center.z),
                InvRadiusSq = math.rcp(radiusSq),
                ViscosityMultiplier = math.clamp(viscosityMultiplier, 0.05f, 8f),
                Active = 1
            };
            OnCurrentSettingsChanged();
            return true;
        }

        public void ClearViscosityRegion(int slot)
        {
            if ((uint)slot >= MaxDynamicViscosityRegionCount)
                return;

            _viscosityRegionBuffer[slot] = default;
            OnCurrentSettingsChanged();
        }

        public void ClearViscosityRegions()
        {
            for (int i = 0; i < MaxDynamicViscosityRegionCount; i++)
                _viscosityRegionBuffer[i] = default;
            OnCurrentSettingsChanged();
        }

        public bool TryDequeueImpactEvent(out FluidImpactEvent impactEvent)
        {
            impactEvent = default;
            if (!TryDrainScheduledBuoyancyJob())
                return false;

            if (!TryDequeueFluidImpactEvent(out impactEvent))
                return false;

            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>Vyzyvaetsya pri izmenenii nastroek techeniy (dlya vizualizatorov).</summary>
        public event System.Action OnCurrentSettingsChangedEvent;

        event System.Action IFluidSurfaceCurrentReadModel.CurrentSettingsChanged
        {
            add => OnCurrentSettingsChangedEvent += value;
            remove => OnCurrentSettingsChangedEvent -= value;
        }

        /// <summary>Uvedomlyaet podpischikov ob izmenenii nastroek techeniy.</summary>
        private void OnCurrentSettingsChanged()
        {
            OnCurrentSettingsChangedEvent?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  MANAGED REGISTRY (parallel lists)
        // ══════════════════════════════════════════════════════════

        /// <summary>Spisok zaregistrirovannyh BuoyancyObject.</summary>
        // COLD ALLOC: List<BuoyancyObject>[256] — dense buoyancy object registry — owner: HectonFluidEngine
        private readonly List<BuoyancyObject> _objects = new List<BuoyancyObject>(256);

        /// <summary>Parallelnyy spisok Rigidbody (indeksy sovpadayut s _objects).</summary>
        // COLD ALLOC: List<Rigidbody>[256] — dense rigidbody registry parallel to _objects — owner: HectonFluidEngine
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>(256);
        // ══════════════════════════════════════════════════════════
        //  LOD DISTANCE CACHING
        // ══════════════════════════════════════════════════════════

        /// <summary>Keshirovannye kvadraty distantsiy dlya LOD (pereschityvayutsya pri ochischenii).</summary>
        private float _cachedNearDistSq = 400f;      // 20^2
        private float _cachedMediumDistSq = 2025f;   // 45^2
        private float _cachedFarDistSq = 8100f;      // 90^2
        private float _cachedCullDistSq = 25600f;    // 160^2

        // ══════════════════════════════════════════════════════════
        //  NATIVE ARRAYS (Job data)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// A constructed, length-1, permanently-zero stand-in for WaveQueryJob.TerrainHeightSamples when no
        /// terrain payload exists.
        ///
        /// It has to be a real allocation and it has to be PERSISTENT. Real, because the Jobs safety system
        /// rejects an unconstructed NativeArray in a scheduled job even when the job body never reads it -
        /// which is what threw 143 times per run and amputated the dispatcher's whole fixed lane walk.
        /// Persistent, because this is scheduled from FixedTick: a per-frame Allocator.TempJob array here
        /// would be a per-frame allocation on a hot path.
        ///
        /// Length 1 rather than 0 on purpose - a zero-length NativeArray is legal but reads as "empty" in
        /// several Unity code paths, and 1 element of ushort costs 2 bytes for the lifetime of the engine.
        /// The job never indexes it: HasTerrainHeightPayload is 0 whenever this is the array in use.
        /// </summary>
        private NativeArray<ushort> _emptyTerrainHeightSamples;

        private FluidVaultBuffer<float3>         _positions;
        private FluidVaultBuffer<float3>         _previousPositions;
        private FluidVaultBuffer<byte>           _previousPositionValid;
        private FluidVaultBuffer<float3>         _velocities;
        private FluidVaultBuffer<float3>         _angularVelocities;
        private FluidVaultBuffer<float3>         _upVectors;
        private FluidVaultBuffer<float3>         _surfaceUpVectors;
        private FluidVaultBuffer<BuoyancyParams> _params;
        private FluidVaultBuffer<float>          _waveOffsets;
        private FluidVaultBuffer<byte>           _sleepMask;
        private FluidVaultBuffer<GerstnerWaveComponent> _gerstnerWaves;
        private FluidVaultBuffer<float>          _gpuBuoyancyForcesY;
        private FluidVaultBuffer<float3>         _resultForces;
        private FluidVaultBuffer<float3>         _resultTorques;
        private FluidVaultBuffer<FluidOceanSurfaceTelemetryEntry> _oceanSurfaceTelemetry;
        private FluidVaultBuffer<FluidImpactEvent> _impactEventScratch;
        private FluidVaultBuffer<int> _impactEventFlags;
        private FluidVaultBuffer<GpuBuoyancyObjectData> _gpuBuoyancyObjectDataUpload;
        private FluidVaultBuffer<float4> _gpuBuoyancyReadback;
        private FluidVaultBuffer<float> _brineHeights;
        private FluidVaultBuffer<float> _brineDensityMultipliers;
        private FluidVaultBuffer<int2> _brineCartographySectors;
        private FluidVaultBuffer<byte> _brineFlags;
        private FluidVaultBuffer<GpuHeatSourceData> _gpuAbyssalHeatSourceUpload;
        private FluidVaultBuffer<ActiveThrusterFlow> _activeThrusterFlows;
        private FluidVaultBuffer<WhirlpoolFlow> _activeWhirlpools;
        private FluidVaultBuffer<float4> _activeMaelstroms;
        private FluidVaultBuffer<MaelstromTelemetryEntry> _maelstromTelemetry;
        private FluidVaultBuffer<FluidViscosityRegion> _activeViscosityRegions;
        private FluidVaultBuffer<float> _viscosityGradientLut;
        private FluidVaultBuffer<float3> _prebakedVectorNoiseField;
        private Texture3D _prebakedVectorNoiseTexture;
        private int _prebakedVectorNoiseRuntimeSeed = int.MinValue;
        private int _activeThrusterFlowCount;
        private int _activeWhirlpoolFlowCount;
        private int _activeMaelstromCount;
        private int _activeViscosityRegionCount;
        private int _activeGerstnerWaveCount;
        private VaultGenerationHandle<GerstnerWaveComponent> _sharedGerstnerWavesHandle;
        private VaultGenerationHandle<OceanGerstnerWaveBufferMeta> _sharedGerstnerMetaHandle;
        private Vector4 _activeMaelstromMeta;
        private int _lastOceanSleepCount;
        private Vector4 _lastOceanSurfaceWave0A;
        private Vector4 _lastOceanSurfaceWave0B;
        private Vector4 _lastOceanSurfaceWave1A;
        private Vector4 _lastOceanSurfaceWave1B;
        private Vector4 _lastOceanSurfaceWave2A;
        private Vector4 _lastOceanSurfaceWave2B;
        private Vector4 _pendingOceanSurfaceWave0A;
        private Vector4 _pendingOceanSurfaceWave0B;
        private Vector4 _pendingOceanSurfaceWave1A;
        private Vector4 _pendingOceanSurfaceWave1B;
        private Vector4 _pendingOceanSurfaceWave2A;
        private Vector4 _pendingOceanSurfaceWave2B;
        private Vector4 _pendingOceanSurfaceWaveMeta;
        private float _currentWaterLevelYSnapshot;
        private float _currentWaterLevelTimeSecondsSnapshot;
        private float _pendingCurrentWaterLevelY;
        private WeatherRuntimeSnapshot _currentWeatherSnapshot;
        private bool _currentWaterLevelYSnapshotValid;
        private bool _currentWeatherSnapshotValid;
        private bool _oceanSurfaceWaveGlobalsValid;
        private bool _oceanSurfaceWaveUniformsDirty;
        private bool _oceanSurfaceWaveClearDirty;
        private bool _currentWaterLevelUniformDirty;
        private int _oceanSurfaceTelemetryWriteIndex;
        private int _lastOceanSurfaceDumpFrame = -1;
        private uint _lastOriginShiftSequence;
        private Vector3 _pendingOriginShiftOffset;
        private VaultGenerationHandle<FluidImpactEvent> _fluidImpactEventRingHandle;
        private int _fluidImpactEventReadIndex;
        private int _fluidImpactEventWriteIndex;
        private int _fluidImpactQueuedCount;
        // COLD ALLOC: Rigidbody[capacity] — schedule-time rigidbody snapshot for deferred force application — owner: HectonFluidEngine
        private Rigidbody[] _scheduledBodies;
        private JobHandle _scheduledBuoyancyHandle;
        private bool _scheduledBuoyancyJobActive;
        private int _scheduledForceCount;
        private bool _originShiftRegistered;
        private bool _hasPendingOriginShiftRebase;
        // COLD ALLOC: CavitationBurstEvent[8] — fixed post-fixed cavitation burst queue — owner: HectonFluidEngine
        private readonly CavitationBurstEvent[] _cavitationBurstQueue = new CavitationBurstEvent[MaxCavitationBurstEvents];
        private readonly CavitationBurstEvent[] _cavitationVisualBurstQueue = new CavitationBurstEvent[MaxCavitationBurstEvents]; // COLD ALLOC: CavitationBurstEvent[8] - visual-sync cavitation bubble queue - owner: HectonFluidEngine
        // COLD ALLOC: Collider[64] — static nonalloc cavitation shockwave overlap buffer — owner: HectonFluidEngine
        private static readonly SpatialQueryHit[] s_CavitationShockwaveContacts = new SpatialQueryHit[CavitationShockwaveHitCapacity];
        // COLD ALLOC: Rigidbody[64] — static deduplicated cavitation shockwave rigidbody targets — owner: HectonFluidEngine
        private static readonly Rigidbody[] s_CavitationShockwaveRigidbodies = new Rigidbody[CavitationShockwaveHitCapacity];
        private int _cavitationBurstCount;
        private int _cavitationVisualBurstCount;
        // COLD ALLOC: ActiveThrusterFlow[4] — fixed analytical propwash inputs — owner: HectonFluidEngine
        private readonly ActiveThrusterFlow[] _thrusterFlowBuffer = new ActiveThrusterFlow[MaxAnalyticalThrusterCount];
        // COLD ALLOC: WhirlpoolFlow[2] — fixed analytical whirlpool inputs — owner: HectonFluidEngine
        private readonly WhirlpoolFlow[] _whirlpoolFlowBuffer = new WhirlpoolFlow[MaxAnalyticalWhirlpoolCount];
        // COLD ALLOC: AbyssalVortexImpulse[4] - bounded transient tail-whip/large-body vortex inputs - owner: HectonFluidEngine
        private readonly AbyssalVortexImpulse[] _abyssalVortexImpulses = new AbyssalVortexImpulse[MaxAbyssalVortexImpulseCount];
        // COLD ALLOC: FluidViscosityRegion[4] - fixed cinematic viscosity region inputs - owner: HectonFluidEngine
        private readonly FluidViscosityRegion[] _viscosityRegionBuffer = new FluidViscosityRegion[MaxDynamicViscosityRegionCount];

        /// <summary>Tekuschaya emkost NativeArrays (vsegda >= count obektov).</summary>
        private int _nativeCapacity;
        private bool _buoyancyNativeBuffersReady;
        private int _lodFrameCounter;
        private float _observerResolveRetryTimer;
        private const float ObserverResolveRetryInterval = 1f;
        private const int MaxNativeCapacityGrowthIterations = 16;
        private const int FluidGraphicsReleaseGpuBuoyancy = 1 << 0;
        private const int FluidGraphicsReleaseAbyssalFlow = 1 << 1;
        private const int FluidGraphicsReleaseSplashdownImpulse = 1 << 2;
        private GraphicsBuffer _gpuBuoyancyPositionBufferA;
        private GraphicsBuffer _gpuBuoyancyPositionBufferB;
        private GraphicsBuffer _gpuBuoyancyParamBufferA;
        private GraphicsBuffer _gpuBuoyancyParamBufferB;
        private GraphicsBuffer[] _gpuBuoyancyResultBuffers;
        private int _gpuBuoyancyUploadBufferIndex;
        private AsyncGPUReadbackRequest[] _gpuReadbackRequests;
        private GpuReadbackNativeRing _gpuReadbackData;
        private int[] _gpuReadbackCounts;
        private bool[] _gpuReadbackActive;
        private int _gpuReadbackWriteIndex;
        private bool _hasGpuBuoyancyData;
        private int _gpuBuoyancyKernel = -1;
        private int _gpuBuoyancyThreadGroupSizeX;
        private GraphicsBuffer _gpuAbyssalFlowResultBuffer;
        private GraphicsBuffer _gpuAbyssalHeatSourceBufferA;
        private GraphicsBuffer _gpuAbyssalHeatSourceBufferB;
        private GraphicsBuffer _activeGpuAbyssalHeatSourceBuffer;
        private int _gpuAbyssalHeatSourceUploadIndex;
        private RenderTexture _gpuAbyssalFlowTextureA;
        private RenderTexture _gpuAbyssalFlowTextureB;
        private RenderTexture _gpuAbyssalFlowReadTexture;
        private RenderTexture _gpuAbyssalFlowWriteTexture;
        private RTHandle _gpuAbyssalFlowTextureAHandle;
        private RTHandle _gpuAbyssalFlowTextureBHandle;
        private IDataVault _dataVault;
        private ISimulationBucketer _simulationBucketer;
        private IWeatherService _weatherService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private ICelestialSkyDirectionReadModel _celestialEngine;
        private ITerrainHeightSampleReadModel _terrainBridge;
        private IBiomePhysicsInfluenceReadModel _proceduralFieldSampler;
        private ISargassumDragReadModel _sargassumDragRuntime;
        private IBrineFluidDensityReadModel _resourceDistributionRuntime;
        private float _gpuAbyssalFlowInterpolationAlpha = 1f;
        private GraphicsBuffer _advectedSiltBufferA;
        private GraphicsBuffer _advectedSiltBufferB;
        private GraphicsBuffer _advectedBubbleBufferA;
        private GraphicsBuffer _advectedBubbleBufferB;
        private GraphicsBuffer _advectedDebrisBufferA;
        private GraphicsBuffer _advectedDebrisBufferB;
        private GraphicsBuffer _emptyAdvectedSiltBuffer;
        private GraphicsBuffer _emptyAdvectedBubbleBuffer;
        private GraphicsBuffer _emptyAdvectedDebrisBuffer;
        private GraphicsBuffer _emptyAbyssalFlowBuffer;
        private GraphicsBuffer _dynamicWakeBufferA;
        private GraphicsBuffer _dynamicWakeBufferB;
        private GraphicsBuffer _activeDynamicWakeBuffer;
        private GraphicsBuffer _dynamicWakeVectorBufferA;
        private GraphicsBuffer _dynamicWakeVectorBufferB;
        private GraphicsBuffer _activeDynamicWakeVectorBuffer;
        private Vector4 _activeDynamicWakeParams;
        private int _dynamicWakeUploadBufferIndex;
        private VaultGenerationHandle<float4> _dynamicWakeBufferHandle;
        private VaultGenerationHandle<float4> _dynamicWakeVectorBufferHandle;
        private GraphicsBuffer _gpuSplashdownImpulseBufferA;
        private GraphicsBuffer _gpuSplashdownImpulseBufferB;
        private GraphicsBuffer _activeGpuSplashdownImpulseBuffer;
        private int _gpuSplashdownImpulseUploadIndex;
        private Texture3D _emptyFluidAdvectionTexture;
        private RTHandle _emptyFluidAdvectionTextureHandle;
        private Texture _cachedFluidAdvectionFlowHandleSource;
        private RTHandle _cachedFluidAdvectionFlowHandle;
        private Texture _cachedFluidAdvectionSdfHandleSource;
        private RTHandle _cachedFluidAdvectionSdfHandle;
        private FluidVaultBuffer<AdvectedSilt> _advectedSiltUpload;
        private FluidVaultBuffer<AdvectedBubble> _advectedBubbleUpload;
        private FluidVaultBuffer<AdvectedDebris> _advectedDebrisUpload;
        private FluidVaultBuffer<byte> _advectedSiltDirtyPages;
        private FluidVaultBuffer<byte> _advectedBubbleDirtyPages;
        private FluidVaultBuffer<byte> _advectedDebrisDirtyPages;
        private byte[] _advectedSiltDirtyPageUploadSnapshot;
        private byte[] _advectedBubbleDirtyPageUploadSnapshot;
        private byte[] _advectedDebrisDirtyPageUploadSnapshot;
        private FluidVaultBuffer<float4> _emptyAbyssalFlowUpload;
        private FluidVaultBuffer<FluidAdvectionTelemetryEntry> _fluidAdvectionTelemetry;
        private FluidVaultBuffer<float4> _splashdownImpulseUpload;
        private FluidVaultBuffer<int> _splashdownImpulseStats;
        private int _activeAdvectedSiltCount;
        private int _activeAdvectedBubbleCount;
        private int _activeAdvectedDebrisCount;
        private int _advectedBubbleWriteCursor;
        private int _advectedDebrisWriteCursor;
        private int _fluidAdvectionKernel = -1;
        private int _fluidAdvectionThreadGroupSizeX;
        private int _fluidAdvectionBufferParity;
        private int _fluidAdvectionTelemetryCursor;
        private int _lastFluidAdvectionTelemetryFrame = -1;
        private bool _fluidAdvectionTelemetryDumped;
        private bool _fluidAdvectionStateReady;
        private bool _fluidAdvectionRenderGraphQueued;
        private bool _advectedSiltGpuUploadDirty;
        private bool _advectedBubbleGpuUploadDirty;
        private bool _advectedDebrisGpuUploadDirty;
        private uint _lastProcessedFluidAdvectionAupShiftFrameId;
        private float3 _pendingFluidAdvectionRuntimeShift;
        private Vector4 _lastAbyssalGridResolution;
        private Vector4 _lastAbyssalFlowCenter;
        private Vector4 _lastAbyssalFlowSpacing;
        private Vector4 _lastAbyssalFlowTextureSpacing;
        private Vector4 _pendingAbyssalGridResolution;
        private Vector4 _pendingAbyssalFlowCenter;
        private Vector4 _pendingAbyssalFlowSpacing;
        private Vector4 _pendingAbyssalFlowTextureParams;
        private GraphicsBuffer _pendingAbyssalFlowResultBuffer;
        private Texture _pendingAbyssalFlowTexture;
        private WeatherRuntimeSnapshot _pendingAbyssalFlowWeatherSnapshot;
        private float _pendingAbyssalFlowWaterLevel;
        private float _pendingAbyssalFlowDeltaTime;
        private bool _hasAbyssalFlowTexture;
        private bool _abyssalFlowPublicationClearIssued;
        private bool _abyssalFlowVisualDirty;
        private bool _abyssalFlowGlobalsDirty;
        private bool _abyssalFlowGlobalsClearDirty;
        private JobHandle _splashdownImpulseJobHandle;
        private bool _splashdownImpulseJobActive;
        private bool _splashdownImpulseUploaded;
        private int _splashdownImpulseScheduleFrame = -1;
        private float3 _splashdownImpulsePositionWS;
        private float _splashdownImpulseRemainingSeconds;
        private float _splashdownImpulseDurationSeconds;
        private int _lastSplashdownFluidImpulseCount;
        private byte _splashdownImpulseQualityPressureQ8;
        private ushort _lastProcessedSplashdownSequence;
        private uint _lastProcessedSplashdownFrame;
        private uint _lastProcessedSplashdownSourceHash;
        private bool _splashdownImpactConsumed;
        private uint _splashdownImpulseFlags;
        private WeatherRuntimeSnapshot _pendingGpuBuoyancyWeatherSnapshot;
        private float _pendingGpuBuoyancyWaterLevel;
        private int _pendingGpuBuoyancyCount;
        private bool _hasPendingGpuBuoyancyDispatch;
        private bool _hasPendingGpuBuoyancyReadbackConsume;
        private int _pendingFluidGraphicsReleaseMask;
        private int _gpuAbyssalUpdateKernel = -1;
        private int _gpuAbyssalTextureUpdateKernel = -1;
        private int _gpuAbyssalWakeKernel = -1;
        private int _gpuAbyssalVortexKernel = -1;
        private int _gpuAbyssalUpdateThreadGroupSizeX;
        private int _gpuAbyssalTextureThreadGroupSizeX;
        private int _gpuAbyssalTextureThreadGroupSizeY;
        private int _gpuAbyssalTextureThreadGroupSizeZ;
        private int _gpuAbyssalWakeThreadGroupSizeX;
        private int _gpuAbyssalWakeThreadGroupSizeY;
        private int _gpuAbyssalWakeThreadGroupSizeZ;
        private int _gpuAbyssalVortexThreadGroupSizeX;
        private int _gpuAbyssalVortexThreadGroupSizeY;
        private int _gpuAbyssalVortexThreadGroupSizeZ;
        private int _abyssalVortexImpulseCount;
        private int _abyssalVortexImpulseWriteIndex;
        private float _lastAbyssalVortexImpulseAgeFixedTime = -1f;
        private float _lastAbyssalFlowDispatchFixedTime = float.NegativeInfinity;
        private FluidVaultBuffer<AbyssalFlowTelemetryEntry> _abyssalFlowTelemetry;
        private FluidVaultBuffer<FluidTelemetryEntry> _fluidSovereigntyTelemetry;
        private FluidVaultBuffer<int> _fluidSovereigntyTelemetryCursor;
        private OceanAdapterVaultHandles _oceanAdapterVaultHandles;
        private int _abyssalFlowTelemetryCursor;
        private bool _abyssalFlowTelemetryDumped;
        private int _maelstromTelemetryCursor;
        private bool _maelstromTelemetryDumped;
        private int _fluidSovereigntyTelemetryCursorMirror;
        private bool _oceanAdapterVaultHandlesReady;
        private bool _oceanAdapterVaultBootAttempted;
        private int _fluidSovereigntyConsecutiveFaults;
        private int _lastFluidSovereigntyDumpFrame = -1;
        private int _lastBuoyancyCapacityFaultFrame = -1;
        private float _nextMaelstromAudioTime;
        private float _nextMaelstromDamageTime;
        private bool _fluidRuntimeRegistered;
        private bool _fixedTickRegistered;
        private bool _postFixedRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _coldSupportsComputeShaders;
        private IPlayerRuntimeContext _playerRuntime;
        private ISubmarineRuntimeContext _submarineRuntime;
        private IThermodynamicsService _thermalRuntime;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            HectonFluidEngine registeredFluid = GlobalRegistry.Fluid;
            if (Application.isPlaying && registeredFluid != null && !ReferenceEquals(registeredFluid, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtimeInstance = this;
            MathGuard.Initialize();
            _dataVault = GlobalRegistry.DataVault;
            s_staticFluidDataVault = _dataVault;
            EnsureFluidSovereigntyTelemetry();
            EnsureOceanAdapterVaultBootHandles();
            PrewarmBuoyancyNativeCapacity();
            CacheFluidRuntimeServicesCold();
            RefreshRuntimeActorContextsIfMissing();
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;

            // Initial observer resolution. If player/camera appears later,
            // FixedTick retries on a cooldown instead of staying in full-cost mode forever.
            TryResolveObserver(force: true);

            // Cache LOD distances once (update if parameters change via property)
            UpdateCachedLodDistances();

#if UNITY_EDITOR
            if (gpuBuoyancyCompute == null)
                gpuBuoyancyCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(GpuBuoyancyComputeAssetPath);

            if (abyssalFlowFieldCompute == null)
                abyssalFlowFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(AbyssalFlowFieldComputeAssetPath);

            if (fluidAdvectionCompute == null)
                fluidAdvectionCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(FluidAdvectionComputeAssetPath);
#endif
            if (_coldSupportsComputeShaders && gpuBuoyancyCompute != null)
            {
                _gpuBuoyancyKernel = ResolveKernel(gpuBuoyancyCompute, "EvaluateBuoyancy", _coldSupportsComputeShaders);
                _gpuBuoyancyThreadGroupSizeX = ResolveKernelThreadGroupSizeX(
                    gpuBuoyancyCompute,
                    _gpuBuoyancyKernel,
                    _coldSupportsComputeShaders);
            }
            if (_coldSupportsComputeShaders && abyssalFlowFieldCompute != null)
            {
                _gpuAbyssalUpdateKernel = ResolveKernel(abyssalFlowFieldCompute, "UpdateAbyssalFlowField", _coldSupportsComputeShaders);
                _gpuAbyssalTextureUpdateKernel = ResolveKernel(abyssalFlowFieldCompute, "UpdateAbyssalFlowTexture", _coldSupportsComputeShaders);
                _gpuAbyssalWakeKernel = ResolveKernel(abyssalFlowFieldCompute, "InjectAbyssalWakeTexture", _coldSupportsComputeShaders);
                _gpuAbyssalVortexKernel = ResolveKernel(abyssalFlowFieldCompute, "InjectAbyssalVortexTexture", _coldSupportsComputeShaders);
                _gpuAbyssalUpdateThreadGroupSizeX = ResolveKernelThreadGroupSizeX(
                    abyssalFlowFieldCompute,
                    _gpuAbyssalUpdateKernel,
                    _coldSupportsComputeShaders);
                ResolveKernelThreadGroupSizes(
                    abyssalFlowFieldCompute,
                    _gpuAbyssalTextureUpdateKernel,
                    _coldSupportsComputeShaders,
                    out _gpuAbyssalTextureThreadGroupSizeX,
                    out _gpuAbyssalTextureThreadGroupSizeY,
                    out _gpuAbyssalTextureThreadGroupSizeZ);
                ResolveKernelThreadGroupSizes(
                    abyssalFlowFieldCompute,
                    _gpuAbyssalWakeKernel,
                    _coldSupportsComputeShaders,
                    out _gpuAbyssalWakeThreadGroupSizeX,
                    out _gpuAbyssalWakeThreadGroupSizeY,
                    out _gpuAbyssalWakeThreadGroupSizeZ);
                ResolveKernelThreadGroupSizes(
                    abyssalFlowFieldCompute,
                    _gpuAbyssalVortexKernel,
                    _coldSupportsComputeShaders,
                    out _gpuAbyssalVortexThreadGroupSizeX,
                    out _gpuAbyssalVortexThreadGroupSizeY,
                    out _gpuAbyssalVortexThreadGroupSizeZ);
            }

            if (_coldSupportsComputeShaders && fluidAdvectionCompute != null)
            {
                _fluidAdvectionKernel = ResolveKernel(fluidAdvectionCompute, "AdvectFluidParticles", _coldSupportsComputeShaders);
                _fluidAdvectionThreadGroupSizeX = ResolveKernelThreadGroupSizeX(
                    fluidAdvectionCompute,
                    _fluidAdvectionKernel,
                    _coldSupportsComputeShaders);
            }

            _gpuReadbackRequests = new AsyncGPUReadbackRequest[GpuReadbackRingSize]; // COLD ALLOC: AsyncGPUReadbackRequest[3] — fixed GPU buoyancy readback ring state — owner: HectonFluidEngine
            _gpuReadbackData = new GpuReadbackNativeRing(GpuReadbackRingSize); // COLD ALLOC: owner for fixed GPU buoyancy readback native targets - owner: HectonFluidEngine
            _gpuReadbackCounts = new int[GpuReadbackRingSize]; // COLD ALLOC: int[3] — GPU buoyancy readback element counts — owner: HectonFluidEngine
            _gpuReadbackActive = new bool[GpuReadbackRingSize]; // COLD ALLOC: bool[3] — GPU buoyancy readback slot activity — owner: HectonFluidEngine
            EnsureAbyssalFlowNativeState();
            EnsureGpuAbyssalFlowBuffersColdIfEnabled();
            EnsureFluidAdvectionVisualState(allowAllocate: true);
            EnsureSplashdownImpulseState(allowAllocate: true);
            EnsureSplashdownImpulseGpuBuffer(allowAllocate: true);
            EnsureDynamicWakeGpuBuffers();
            TryOpenOrAcquireDynamicWakeVaultBuffers(
                out _,
                out _);
            EnsurePrebakedVectorNoiseField();
            PublishCurrentWaterLevelUniform();
        }

        private void OnEnable()
        {
            _dataVault = GlobalRegistry.DataVault;
            s_staticFluidDataVault = _dataVault;
            EnsureFluidSovereigntyTelemetry();
            PrewarmBuoyancyNativeCapacity();
            EnsurePrebakedVectorNoiseField();
            EnsureGpuAbyssalFlowBuffersColdIfEnabled();
            EnsureFluidAdvectionVisualState(allowAllocate: true);
            EnsureSplashdownImpulseState(allowAllocate: true);
            EnsureSplashdownImpulseGpuBuffer(allowAllocate: true);
            EnsureDynamicWakeGpuBuffers();
            TryOpenOrAcquireDynamicWakeVaultBuffers(
                out _,
                out _);
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
            CacheFluidRuntimeServicesCold();
            TryRegisterHotSwapListener();
            RefreshRuntimeActorContextsIfMissing();

            if (Application.isPlaying && !_fluidRuntimeRegistered)
            {
                HectonFluidEngine registeredFluid = GlobalRegistry.Fluid;
                if (registeredFluid != null && !ReferenceEquals(registeredFluid, this))
                {
                    Destroy(gameObject);
                    return;
                }

                s_runtimeInstance = this;
                GlobalRegistry.RegisterFluidRuntime(this);
                _fluidRuntimeRegistered = ReferenceEquals(GlobalRegistry.Fluid, this);
                if (_fluidRuntimeRegistered)
                    s_runtimeInstance = this;
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_fixedTickRegistered)
            {
                _fixedTickRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            }

            if (!_postFixedRegistered)
            {
                _postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            }

            if (!_lateFrameRegistered)
            {
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            if (!_originShiftRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftRegistered = true;
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            ClearOceanSurfaceWaveUniformsIfOwner();

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            if (_fluidRuntimeRegistered)
            {
                GlobalRegistry.UnregisterFluidRuntime(this);
                _fluidRuntimeRegistered = false;
            }

            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            if (_fixedTickRegistered)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _fixedTickRegistered = false;
            }

            if (_postFixedRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            // Release runtime job buffers before editor domain/play-mode teardown.
            // In-editor play transitions do not always guarantee a clean OnDestroy path
            // for persistent native allocations, so we free them on disable as well.
            ClearAbyssalVortexImpulses();
            DisposePrebakedVectorNoiseField();
            DisposeNativeArrays(releaseGraphicsImmediately: false);
            DisposeFluidAdvectionState();
            _fluidSovereigntyTelemetry.Release();
            _fluidSovereigntyTelemetryCursor.Release();
            ResetOceanAdapterVaultRoute();
            _currentWaterLevelYSnapshotValid = false;
            _currentWeatherSnapshotValid = false;
            _simulationBucketer = null;
            if (ReferenceEquals(s_staticFluidDataVault, _dataVault))
                s_staticFluidDataVault = null;
            _dataVault = null;
            ClearCachedFluidRuntimeServices();
            _playerRuntime = null;
            _submarineRuntime = null;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastOriginShiftSequence = shiftData.Sequence;
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (_scheduledBuoyancyJobActive)
            {
                _pendingOriginShiftOffset += shiftOffset;
                float pendingShiftSqrMagnitude = _pendingOriginShiftOffset.sqrMagnitude;
                if (!MathGuard.IsFinite(_pendingOriginShiftOffset) ||
                    !MathGuard.IsFinite(pendingShiftSqrMagnitude))
                {
                    _pendingOriginShiftOffset = Vector3.zero;
                    _hasPendingOriginShiftRebase = false;
                    CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                    return;
                }

                _hasPendingOriginShiftRebase = true;
                return;
            }

            ApplyOriginShiftRebase(shiftOffset);
        }

        private void ApplyOriginShiftRebase(Vector3 shiftOffset)
        {
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            float3 runtimeOffset = new float3(
                -shiftOffset.x,
                -shiftOffset.y,
                -shiftOffset.z);
            int count = math.min(_objects.Count, _nativeCapacity);

            for (int i = 0; i < count; i++)
            {
                if (_positions.IsCreated && i < _positions.Length)
                    _positions[i] += runtimeOffset;

                if (_previousPositions.IsCreated &&
                    _previousPositionValid.IsCreated &&
                    i < _previousPositions.Length &&
                    i < _previousPositionValid.Length &&
                    _previousPositionValid[i] != 0)
                {
                    _previousPositions[i] += runtimeOffset;
                }
            }

            if (_splashdownImpulseRemainingSeconds > 0f)
            {
                _splashdownImpulsePositionWS += runtimeOffset;
            }

            int vortexCount = _abyssalVortexImpulseCount;
            for (int i = 0; i < vortexCount; i++)
            {
                AbyssalVortexImpulse impulse = _abyssalVortexImpulses[i];
                Vector3 rebasedPosition = impulse.PositionWS;
                rebasedPosition.x += runtimeOffset.x;
                rebasedPosition.y += runtimeOffset.y;
                rebasedPosition.z += runtimeOffset.z;
                impulse.PositionWS = rebasedPosition;
                _abyssalVortexImpulses[i] = impulse;
            }

            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow whirlpool = _whirlpoolFlowBuffer[i];
                if (whirlpool.Active == 0)
                    continue;

                whirlpool.CenterWS += runtimeOffset;
                _whirlpoolFlowBuffer[i] = whirlpool;
            }

            if (_activeWhirlpools.IsCreated)
            {
                int whirlpoolCount = math.min(_activeWhirlpoolFlowCount, _activeWhirlpools.Length);
                for (int i = 0; i < whirlpoolCount; i++)
                {
                    WhirlpoolFlow whirlpool = _activeWhirlpools[i];
                    whirlpool.CenterWS += runtimeOffset;
                    _activeWhirlpools[i] = whirlpool;
                }
            }

            if (_activeMaelstroms.IsCreated)
            {
                int maelstromCount = math.min(_activeMaelstromCount, _activeMaelstroms.Length);
                for (int i = 0; i < maelstromCount; i++)
                {
                    float4 maelstrom = _activeMaelstroms[i];
                    maelstrom.x += runtimeOffset.x;
                    maelstrom.y += runtimeOffset.y;
                    maelstrom.z += runtimeOffset.z;
                    _activeMaelstroms[i] = maelstrom;
                }
            }

        }

        private void ApplyPendingOriginShiftRebase()
        {
            if (!_hasPendingOriginShiftRebase)
                return;

            Vector3 pendingShift = _pendingOriginShiftOffset;
            _pendingOriginShiftOffset = Vector3.zero;
            _hasPendingOriginShiftRebase = false;
            ApplyOriginShiftRebase(pendingShift);
        }

        /// <summary>
        /// Lazily allocates the empty terrain-height stand-in. Allocated once per engine, released in
        /// <see cref="OnDestroy"/>. See the field docs for why a stand-in is required at all.
        /// </summary>
        private NativeArray<ushort> EnsureEmptyTerrainHeightSamples()
        {
            if (!_emptyTerrainHeightSamples.IsCreated)
            {
                // COLD ALLOC: NativeArray<ushort>[1] via H8Memory - Jobs-safety stand-in so a missing terrain payload
                // cannot throw out of FixedTick - owner: HectonFluidEngine / SystemID.Fluid
                _emptyTerrainHeightSamples = H8Memory.Allocate<ushort>(
                    1,
                    SystemID.Fluid,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
            }

            return _emptyTerrainHeightSamples;
        }

        private void OnDestroy()
        {
            if (_emptyTerrainHeightSamples.IsCreated)
                H8Memory.Release(ref _emptyTerrainHeightSamples, SystemID.Fluid);

            TryUnregisterHotSwapListener();
            ClearOceanSurfaceWaveUniformsIfOwner();

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            if (_fluidRuntimeRegistered)
            {
                GlobalRegistry.UnregisterFluidRuntime(this);
                _fluidRuntimeRegistered = false;
            }

            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            if (_fixedTickRegistered)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _fixedTickRegistered = false;
            }

            if (_postFixedRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            ClearAbyssalVortexImpulses();
            DisposePrebakedVectorNoiseField();
            DisposeNativeArrays();
            DisposeFluidAdvectionState();
            _fluidSovereigntyTelemetry.Release();
            _fluidSovereigntyTelemetryCursor.Release();
            ResetOceanAdapterVaultRoute();
            _simulationBucketer = null;
            if (ReferenceEquals(s_staticFluidDataVault, _dataVault))
                s_staticFluidDataVault = null;
            _dataVault = null;
            ClearCachedFluidRuntimeServices();
            _playerRuntime = null;
            _submarineRuntime = null;
        }

        private void CacheFluidRuntimeServicesCold()
        {
            _weatherService = GlobalRegistry.Weather;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _celestialEngine = GlobalRegistry.CelestialSkyDirection;
            _terrainBridge = GlobalRegistry.TerrainHeightSamples;
            _proceduralFieldSampler = GlobalRegistry.BiomePhysicsInfluence;
            WorldRuntimeReferenceUtility.TryResolveSargassumDragReadModel(ref _sargassumDragRuntime);
            _resourceDistributionRuntime = GlobalRegistry.BrineFluidDensity;
            _thermalRuntime = GlobalRegistry.ThermodynamicsService;
        }

        private void ClearCachedFluidRuntimeServices()
        {
            _weatherService = null;
            _oceanKinematicsService = null;
            _celestialEngine = null;
            _terrainBridge = null;
            _proceduralFieldSampler = null;
            _sargassumDragRuntime = null;
            _resourceDistributionRuntime = null;
            _thermalRuntime = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    s_staticFluidDataVault = _dataVault;
                    ResetFluidVaultGenerationHandles();
                    ResetOceanAdapterVaultRoute();
                    EnsureFluidSovereigntyTelemetry();
                    EnsureOceanAdapterVaultBootHandles();
                    PrewarmBuoyancyNativeCapacity();
                    EnsureGpuAbyssalFlowBuffersColdIfEnabled();
                    EnsureFluidAdvectionVisualState(allowAllocate: true);
                    EnsureSplashdownImpulseState(allowAllocate: true);
                    EnsureSplashdownImpulseGpuBuffer(allowAllocate: true);
                    EnsureDynamicWakeGpuBuffers();
                    TryOpenOrAcquireDynamicWakeVaultBuffers(
                        out _,
                        out _);
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketer = currentService as ISimulationBucketer;
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    _weatherService = currentService as IWeatherService;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    PublishCurrentWaterLevelUniform();
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    _celestialEngine = currentService as ICelestialSkyDirectionReadModel;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _terrainBridge = currentService as ITerrainHeightSampleReadModel;
                    break;
                case GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime:
                    _proceduralFieldSampler = currentService as IBiomePhysicsInfluenceReadModel;
                    break;
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    _sargassumDragRuntime = currentService as ISargassumDragReadModel;
                    break;
                case GlobalRegistryServiceSlot.ResourceDistributionRuntime:
                    _resourceDistributionRuntime = currentService as IBrineFluidDensityReadModel;
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsRuntime:
                case GlobalRegistryServiceSlot.ThermodynamicsService:
                    _thermalRuntime = currentService as IThermodynamicsService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntime = currentService as ISubmarineRuntimeContext;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Registriruet BuoyancyObject. Vyzyvaetsya iz OnEnable.
        /// Keshiruet Rigidbody v parallelnom spiske.
        /// </summary>
        public bool Register(BuoyancyObject obj)
        {
            if (obj == null || obj.Body == null) return false;

            if (ContainsRegisteredObject(obj))
                return true;

            int requiredCount = _objects.Count + 1;
            if (!TryOpenOrAcquireBuoyancyNativeCapacity(requiredCount, recordFault: true))
            {
                return false;
            }

            EnsureManagedRegistryCapacity(math.max(requiredCount, _nativeCapacity));

            _objects.Add(obj);
            _bodies.Add(obj.Body);

            UpdateDiagnostics();
            return true;
        }

        /// <summary>
        /// Samples the previous-frame environmental current for sandboxed mod flow queries.
        /// The dispatcher owns call cadence and never exposes fluid buffers to mods.
        /// </summary>
        /// <param name="runtimePosition">Frame-space query position.</param>
        /// <param name="flowVector">Resolved flow vector in meters per second.</param>
        /// <returns>True when a finite flow vector was resolved.</returns>
        public bool TrySampleModAbyssalFlow(Vector3 runtimePosition, out float3 flowVector)
        {
            flowVector = default;
            float3 query = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(query)))
                return false;

            WeatherRuntimeSnapshot weatherSnapshot = ReadPublishedWeatherSnapshot();
            Vector3 authoredCurrent = CurrentVolume.SampleCombinedCurrent(runtimePosition);
            float3 weatherCurrent = weatherSnapshot.CurrentMeta.GlobalBaseVector * math.max(0f, weatherSnapshot.CurrentMeta.GlobalScale);
            float3 configuredCurrent = new float3(currentVector.x, currentVector.y, currentVector.z) * math.max(0f, currentStrength);
            float3 giantWakeCurrent = ResolveGiantWakeCurrentForDepth(query.y);
            flowVector = configuredCurrent + weatherCurrent + giantWakeCurrent + new float3(authoredCurrent.x, authoredCurrent.y, authoredCurrent.z);
            if (!math.all(math.isfinite(flowVector)))
            {
                flowVector = default;
                return false;
            }

            return true;
        }

        public bool TrySampleCombinedCurrent(Vector3 samplePosition, out Vector3 currentVector)
        {
            currentVector = CurrentVolume.SampleCombinedCurrent(samplePosition);
            return math.isfinite(currentVector.x) &&
                   math.isfinite(currentVector.y) &&
                   math.isfinite(currentVector.z);
        }

        public bool TrySampleAuthoredCurrent(Vector3 samplePosition, out Vector3 currentVector)
        {
            currentVector = CurrentVolume.SampleAt(samplePosition);
            return math.isfinite(currentVector.x) &&
                   math.isfinite(currentVector.y) &&
                   math.isfinite(currentVector.z);
        }

        /// <summary>
        /// Resolves the legacy structured-buffer flow payload for consumers that cannot sample the 3D texture.
        /// </summary>
        /// <param name="flowFieldBuffer">Published structured flow buffer.</param>
        /// <param name="gridResolution">Grid resolution and flattened node count.</param>
        /// <param name="flowCenter">Runtime world-space center of the flow volume.</param>
        /// <param name="flowSpacing">Cell spacing and reciprocal world-size metadata.</param>
        /// <returns>True when a finite structured-buffer payload is currently published.</returns>
        /// <remarks>The 3D texture path is the preferred runtime consumer contract.</remarks>
        public bool TryGetGpuAbyssalFlowFieldBuffer(
            out GraphicsBuffer flowFieldBuffer,
            out Vector4 gridResolution,
            out Vector4 flowCenter,
            out Vector4 flowSpacing)
        {
            flowFieldBuffer = _gpuAbyssalFlowResultBuffer;
            gridResolution = _lastAbyssalGridResolution;
            flowCenter = _lastAbyssalFlowCenter;
            flowSpacing = _lastAbyssalFlowSpacing;
            return flowFieldBuffer != null &&
                   flowFieldBuffer.IsValid() &&
                   flowFieldBuffer.count > 0 &&
                   gridResolution.x > 0f &&
                   gridResolution.y > 0f &&
                   gridResolution.z > 0f &&
                   gridResolution.w > 0f &&
                   IsFiniteVector(flowCenter) &&
                   IsFiniteVector(flowSpacing) &&
                   flowSpacing.x > 0f &&
                   flowSpacing.y > 0f &&
                   flowSpacing.z > 0f;
        }

        public float GpuAbyssalFlowInterpolationAlpha => _gpuAbyssalFlowInterpolationAlpha;

        /// <summary>
        /// Resolves the active 3D abyssal flow texture payload for GPU consumers.
        /// </summary>
        /// <param name="flowFieldTexture">Published 3D flow texture.</param>
        /// <param name="gridResolution">Texture resolution and voxel count.</param>
        /// <param name="flowCenter">Runtime world-space center of the 100 m flow volume.</param>
        /// <param name="flowSpacing">Cell spacing, world size, and reciprocal world size.</param>
        /// <returns>True when the texture exists, is created, and contains a current dispatch result.</returns>
        /// <remarks>Consumers must still bind a zero fallback texture when this returns false.</remarks>
        public bool TryGetGpuAbyssalFlowFieldTexture(
            out Texture flowFieldTexture,
            out Vector4 gridResolution,
            out Vector4 flowCenter,
            out Vector4 flowSpacing)
        {
            flowFieldTexture = _gpuAbyssalFlowReadTexture;
            gridResolution = new Vector4(
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureResolution * AbyssalFlowTextureResolution * AbyssalFlowTextureResolution);
            flowCenter = _lastAbyssalFlowCenter;
            flowSpacing = _lastAbyssalFlowTextureSpacing;
            bool textureCreated = _gpuAbyssalFlowReadTexture != null && _gpuAbyssalFlowReadTexture.IsCreated();
            return _hasAbyssalFlowTexture &&
                   textureCreated &&
                   IsFiniteVector(flowCenter) &&
                   IsFiniteVector(flowSpacing) &&
                   flowSpacing.x > 0f &&
                   flowSpacing.y > 0f &&
                   flowSpacing.z > 0f &&
                   flowSpacing.w > 0f;
        }

        /// <summary>
        /// Snimaet BuoyancyObject s registratsii. Vyzyvaetsya iz OnDisable.
        /// Swap-remove dlya O(1).
        /// </summary>
        public void Unregister(BuoyancyObject obj)
        {
            if (obj == null) return;

            if (!ContainsRegisteredObject(obj))
                return;  // Not registered

            int count = _objects.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_objects[i], obj))
                {
                    int last = count - 1;

                    // Swap with last
                    MoveNativeSlotCache(i, last);
                    _objects[i] = _objects[last];
                    _bodies[i]  = _bodies[last];

                    // Remove last
                    _objects.RemoveAt(last);
                    _bodies.RemoveAt(last);

                    break;
                }
            }

            ReleaseIdleNativeBuffersIfNeeded();
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        private void MoveNativeSlotCache(int destination, int source)
        {
            if (destination == source)
                return;

            if (_positions.IsCreated && source < _positions.Length && destination < _positions.Length)
                _positions[destination] = _positions[source];

            if (_previousPositions.IsCreated && source < _previousPositions.Length && destination < _previousPositions.Length)
                _previousPositions[destination] = _previousPositions[source];

            if (_previousPositionValid.IsCreated &&
                source < _previousPositionValid.Length &&
                destination < _previousPositionValid.Length)
            {
                _previousPositionValid[destination] = _previousPositionValid[source];
                _previousPositionValid[source] = 0;
            }

            if (_surfaceUpVectors.IsCreated && source < _surfaceUpVectors.Length && destination < _surfaceUpVectors.Length)
                _surfaceUpVectors[destination] = _surfaceUpVectors[source];

            if (_sleepMask.IsCreated && source < _sleepMask.Length && destination < _sleepMask.Length)
            {
                _sleepMask[destination] = _sleepMask[source];
                _sleepMask[source] = 0;
            }

            if (_brineHeights.IsCreated && source < _brineHeights.Length && destination < _brineHeights.Length)
            {
                _brineHeights[destination] = _brineHeights[source];
                _brineHeights[source] = 0f;
            }

            if (_brineDensityMultipliers.IsCreated &&
                source < _brineDensityMultipliers.Length &&
                destination < _brineDensityMultipliers.Length)
            {
                _brineDensityMultipliers[destination] = _brineDensityMultipliers[source];
                _brineDensityMultipliers[source] = 1f;
            }

            if (_brineCartographySectors.IsCreated &&
                source < _brineCartographySectors.Length &&
                destination < _brineCartographySectors.Length)
            {
                _brineCartographySectors[destination] = _brineCartographySectors[source];
                _brineCartographySectors[source] = default;
            }

            if (_brineFlags.IsCreated && source < _brineFlags.Length && destination < _brineFlags.Length)
            {
                _brineFlags[destination] = _brineFlags[source];
                _brineFlags[source] = 0;
            }
        }

        //  IFixedTickable — MAIN PHYSICS LOOP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya GameTickManager v FixedUpdate.
        ///
        /// Pipeline:
        ///   Runtime guard: a completed previous job is drained before this method writes
        ///   new data into the same NativeArrays. If the job is still running, this fixed
        ///   step is skipped instead of blocking.
        ///   1. Validate prewarmed NativeArrays; FixedTick never grows capacity.
        ///   2. Gather: kopiruem dannye iz Rigidbody → NativeArrays
        ///   3. Schedule: BuoyancyJob (Burst, parallel)
        ///   4. Completion: only after IsCompleted, no blocking wait
        ///   5. Apply: queue force packets cherez PhysicsForceRouter
        ///
        /// Vse shagi krome Job — main thread.
        /// Job — worker threads, Burst compiled, SIMD.
        /// </summary>
        private void PopulateGerstnerWaveData(
            in WeatherRuntimeSnapshot weatherSnapshot,
            out int activeWaveCount,
            out float maxWaveEnvelope)
        {
            activeWaveCount = ResolveAuthorityGerstnerWaveBudget();
            maxWaveEnvelope = 0f;
            _activeGerstnerWaveCount = 0;

            if (!_gerstnerWaves.IsCreated)
                return;

            activeWaveCount = math.min(math.max(1, activeWaveCount), MaxGerstnerWaveCount);
            ResolvePrimaryGerstnerWaves(
                in weatherSnapshot,
                out GerstnerWaveComponent wave0,
                out GerstnerWaveComponent wave1,
                out GerstnerWaveComponent wave2);

            for (int i = 0; i < MaxGerstnerWaveCount; i++)
            {
                GerstnerWaveComponent source = ResolveGerstnerWaveAtIndex(i, wave0, wave1, wave2);
                _gerstnerWaves[i] = source;
                if (i < activeWaveCount)
                    maxWaveEnvelope += math.abs(source.Amplitude);
            }

            _activeGerstnerWaveCount = activeWaveCount;
            PublishGerstnerWaveDataVault(activeWaveCount, weatherSnapshot.CurrentMeta.TimeAccumulator);
            PublishOceanSurfaceWaveUniforms(activeWaveCount, weatherSnapshot.CurrentMeta.TimeAccumulator);
        }

        private void EnsureSharedGerstnerDataVaultBuffers()
        {
            IDataVault vault = ResolveFluidDataVault();
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return;

            OpenOrAcquireFluidVaultBuffer(
                ref _sharedGerstnerWavesHandle,
                BufferID.OceanGerstnerWaves,
                MaxGerstnerWaveCount,
                NativeArrayOptions.ClearMemory,
                out NativeArray<GerstnerWaveComponent> _);
            OpenOrAcquireFluidVaultBuffer(
                ref _sharedGerstnerMetaHandle,
                BufferID.OceanGerstnerWaveMeta,
                1,
                NativeArrayOptions.ClearMemory,
                out NativeArray<OceanGerstnerWaveBufferMeta> _);
        }

        private void PublishGerstnerWaveDataVault(int activeWaveCount, float timeSeconds)
        {
            if (!_gerstnerWaves.IsCreated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<GerstnerWaveComponent> sharedWaves;
            NativeArray<OceanGerstnerWaveBufferMeta> sharedMeta;
            if (!TryOpenExistingFluidVaultBuffer(
                    ref _sharedGerstnerWavesHandle,
                    BufferID.OceanGerstnerWaves,
                    MaxGerstnerWaveCount,
                    out sharedWaves) ||
                !TryOpenExistingFluidVaultBuffer(
                    ref _sharedGerstnerMetaHandle,
                    BufferID.OceanGerstnerWaveMeta,
                    1,
                    out sharedMeta))
            {
                return;
            }

            if (!sharedWaves.IsCreated || sharedWaves.Length < MaxGerstnerWaveCount ||
                !sharedMeta.IsCreated || sharedMeta.Length < 1)
            {
                return;
            }

            NativeArray<GerstnerWaveComponent>.Copy(_gerstnerWaves, sharedWaves, MaxGerstnerWaveCount);
            OceanGerstnerWaveBufferMeta meta = sharedMeta[0];
            meta.ActiveWaveCount = math.clamp(activeWaveCount, 0, MaxGerstnerWaveCount);
            meta.TimeSeconds = math.max(0f, timeSeconds);
            meta.SleepCount = _lastOceanSleepCount;
            meta.Version++;
            sharedMeta[0] = meta;
        }

        private void PublishOceanSurfaceWaveUniforms(int activeWaveCount, float timeSeconds)
        {
            if (!_gerstnerWaves.IsCreated || _gerstnerWaves.Length < 3)
            {
                QueueClearOceanSurfaceWaveUniforms();
                return;
            }

            GerstnerWaveComponent wave0 = _gerstnerWaves[0];
            GerstnerWaveComponent wave1 = _gerstnerWaves[1];
            GerstnerWaveComponent wave2 = _gerstnerWaves[2];
            PublishOceanSurfaceWaveUniforms(activeWaveCount, timeSeconds, wave0, wave1, wave2, _lastOceanSleepCount);
        }

        private void PublishOceanSurfaceWaveUniformsFromWeather(
            in WeatherRuntimeSnapshot weatherSnapshot,
            int sleepCount)
        {
            ResolvePrimaryGerstnerWaves(
                in weatherSnapshot,
                out GerstnerWaveComponent wave0,
                out GerstnerWaveComponent wave1,
                out GerstnerWaveComponent wave2);
            int activeWaveCount = math.min(
                math.max(1, ResolveAuthorityGerstnerWaveBudget()),
                MaxGerstnerWaveCount);
            PublishOceanSurfaceWaveUniforms(
                activeWaveCount,
                weatherSnapshot.CurrentMeta.TimeAccumulator,
                wave0,
                wave1,
                wave2,
                sleepCount);
        }

        private void PublishOceanSurfaceWaveUniforms(
            int activeWaveCount,
            float timeSeconds,
            GerstnerWaveComponent wave0,
            GerstnerWaveComponent wave1,
            GerstnerWaveComponent wave2,
            int sleepCount)
        {
            BuildOceanSurfaceWaveVectors(
                in wave0,
                activeWaveCount > 0,
                out _pendingOceanSurfaceWave0A,
                out _pendingOceanSurfaceWave0B);
            BuildOceanSurfaceWaveVectors(
                in wave1,
                activeWaveCount > 1,
                out _pendingOceanSurfaceWave1A,
                out _pendingOceanSurfaceWave1B);
            BuildOceanSurfaceWaveVectors(
                in wave2,
                activeWaveCount > 2,
                out _pendingOceanSurfaceWave2A,
                out _pendingOceanSurfaceWave2B);
            _pendingOceanSurfaceWaveMeta = new Vector4(activeWaveCount, math.max(0f, timeSeconds), sleepCount, 0f);
            _oceanSurfaceWaveUniformsDirty = true;
            _oceanSurfaceWaveClearDirty = false;
        }

        private static void BuildOceanSurfaceWaveVectors(
            in GerstnerWaveComponent wave,
            bool active,
            out Vector4 waveA,
            out Vector4 waveB)
        {
            waveA = new Vector4(wave.DirectionXZ.x, wave.DirectionXZ.y, wave.Amplitude, wave.Wavelength);
            waveB = new Vector4(wave.Steepness, wave.PhaseOffset, wave.SpeedMultiplier, active ? 1f : 0f);
        }

        private void SetOceanSurfaceWaveGlobalIfChanged(
            int waveAId,
            int waveBId,
            Vector4 waveA,
            Vector4 waveB,
            ref Vector4 lastWaveA,
            ref Vector4 lastWaveB)
        {
            if (!_oceanSurfaceWaveGlobalsValid || HasOceanSurfaceVectorChanged(waveA, lastWaveA))
            {
                Shader.SetGlobalVector(waveAId, waveA);
                lastWaveA = waveA;
            }

            if (!_oceanSurfaceWaveGlobalsValid || HasOceanSurfaceVectorChanged(waveB, lastWaveB))
            {
                Shader.SetGlobalVector(waveBId, waveB);
                lastWaveB = waveB;
            }
        }

        private static bool HasOceanSurfaceVectorChanged(Vector4 current, Vector4 previous)
        {
            return current.x != previous.x ||
                   current.y != previous.y ||
                   current.z != previous.z ||
                   current.w != previous.w;
        }

        private void ClearOceanSurfaceWaveUniforms()
        {
            _lastOceanSurfaceWave0A = Vector4.zero;
            _lastOceanSurfaceWave0B = Vector4.zero;
            _lastOceanSurfaceWave1A = Vector4.zero;
            _lastOceanSurfaceWave1B = Vector4.zero;
            _lastOceanSurfaceWave2A = Vector4.zero;
            _lastOceanSurfaceWave2B = Vector4.zero;
            _oceanSurfaceWaveGlobalsValid = false;
            Shader.SetGlobalVector(_OceanSurfaceWave0AId, Vector4.zero);
            Shader.SetGlobalVector(_OceanSurfaceWave0BId, Vector4.zero);
            Shader.SetGlobalVector(_OceanSurfaceWave1AId, Vector4.zero);
            Shader.SetGlobalVector(_OceanSurfaceWave1BId, Vector4.zero);
            Shader.SetGlobalVector(_OceanSurfaceWave2AId, Vector4.zero);
            Shader.SetGlobalVector(_OceanSurfaceWave2BId, Vector4.zero);
            Shader.SetGlobalVector(_OceanSurfaceWaveMetaId, Vector4.zero);
        }

        private void QueueClearOceanSurfaceWaveUniforms()
        {
            _oceanSurfaceWaveUniformsDirty = false;
            _oceanSurfaceWaveClearDirty = true;
        }

        private void FlushOceanSurfaceWaveUniforms()
        {
            if (_oceanSurfaceWaveClearDirty)
            {
                _oceanSurfaceWaveClearDirty = false;
                ClearOceanSurfaceWaveUniforms();
                return;
            }

            if (!_oceanSurfaceWaveUniformsDirty)
                return;

            _oceanSurfaceWaveUniformsDirty = false;
            SetOceanSurfaceWaveGlobalIfChanged(
                _OceanSurfaceWave0AId,
                _OceanSurfaceWave0BId,
                _pendingOceanSurfaceWave0A,
                _pendingOceanSurfaceWave0B,
                ref _lastOceanSurfaceWave0A,
                ref _lastOceanSurfaceWave0B);
            SetOceanSurfaceWaveGlobalIfChanged(
                _OceanSurfaceWave1AId,
                _OceanSurfaceWave1BId,
                _pendingOceanSurfaceWave1A,
                _pendingOceanSurfaceWave1B,
                ref _lastOceanSurfaceWave1A,
                ref _lastOceanSurfaceWave1B);
            SetOceanSurfaceWaveGlobalIfChanged(
                _OceanSurfaceWave2AId,
                _OceanSurfaceWave2BId,
                _pendingOceanSurfaceWave2A,
                _pendingOceanSurfaceWave2B,
                ref _lastOceanSurfaceWave2A,
                ref _lastOceanSurfaceWave2B);
            _oceanSurfaceWaveGlobalsValid = true;
            Shader.SetGlobalVector(_OceanSurfaceWaveMetaId, _pendingOceanSurfaceWaveMeta);
        }

        private void ClearOceanSurfaceWaveUniformsIfOwner()
        {
            if (!Application.isPlaying || _fluidRuntimeRegistered || ReferenceEquals(GlobalRegistry.Fluid, this))
                ClearOceanSurfaceWaveUniforms();
        }

        private static int ResolveAuthorityGerstnerWaveBudget()
        {
            return MaxGerstnerWaveCount;
        }

        private static GerstnerWaveComponent SanitizeWave(
            GerstnerWaveComponent wave,
            float2 fallbackDirection,
            float fallbackAmplitude)
        {
            wave.DirectionXZ = HectonGerstnerWater.ResolveDirectionOrDefault(wave.DirectionXZ, fallbackDirection);
            wave.Amplitude = math.isfinite(wave.Amplitude) && wave.Amplitude > 0f ? wave.Amplitude : fallbackAmplitude;
            wave.Wavelength = math.isfinite(wave.Wavelength) && wave.Wavelength > 0.01f ? wave.Wavelength : 8f;
            wave.Steepness = math.clamp(math.isfinite(wave.Steepness) ? wave.Steepness : 0.35f, 0f, 1.2f);
            wave.PhaseOffset = math.isfinite(wave.PhaseOffset) ? wave.PhaseOffset : 0f;
            wave.SpeedMultiplier = math.isfinite(wave.SpeedMultiplier) && wave.SpeedMultiplier > 0.01f
                ? wave.SpeedMultiplier
                : 1f;
            return wave;
        }

        private static void ResolvePrimaryGerstnerWaves(
            in WeatherRuntimeSnapshot weatherSnapshot,
            out GerstnerWaveComponent wave0,
            out GerstnerWaveComponent wave1,
            out GerstnerWaveComponent wave2)
        {
            wave0 = SanitizeWave(weatherSnapshot.Wave0, new float2(1f, 0f), 0.35f);
            wave1 = SanitizeWave(weatherSnapshot.Wave1, new float2(0f, 1f), 0.22f);
            wave2 = SanitizeWave(weatherSnapshot.Wave2, new float2(0.70710677f, 0.70710677f), 0.14f);
            float stormMultiplier = ResolveStormWaveAmplitudeMultiplier(in weatherSnapshot);
            wave0.Amplitude *= stormMultiplier;
            wave1.Amplitude *= stormMultiplier;
            wave2.Amplitude *= stormMultiplier;
        }

        private static GerstnerWaveComponent ResolveGerstnerWaveAtIndex(
            int index,
            GerstnerWaveComponent wave0,
            GerstnerWaveComponent wave1,
            GerstnerWaveComponent wave2)
        {
            GerstnerWaveComponent source = index == 0 ? wave0 : (index == 1 ? wave1 : wave2);
            if (index < 3)
                return source;

            int sourceIndex = index % 3;
            source = sourceIndex == 0 ? wave0 : (sourceIndex == 1 ? wave1 : wave2);
            float harmonic = 1f + (index - 2) * 0.173f;
            float amplitudeScale = math.rcp(1f + (index - 2) * 0.42f);
            float angle = source.PhaseOffset + index * 0.61803399f;
            MathLodApproximation.ApproxSinCosBhaskara(angle, out float directionSin, out float directionCos);
            source.DirectionXZ = HectonGerstnerWater.ResolveDirectionOrDefault(
                new float2(
                    source.DirectionXZ.x * directionCos - source.DirectionXZ.y * directionSin,
                    source.DirectionXZ.x * directionSin + source.DirectionXZ.y * directionCos),
                new float2(1f, 0f));
            source.Amplitude *= amplitudeScale;
            source.Wavelength = math.max(0.35f, source.Wavelength / harmonic);
            source.Steepness = math.clamp(source.Steepness * (0.85f + amplitudeScale * 0.35f), 0f, 1.2f);
            source.PhaseOffset += index * 1.731f;
            source.SpeedMultiplier *= 0.88f + (index & 3) * 0.09f;
            return source;
        }

        private static float ResolveStormWaveAmplitudeMultiplier(in WeatherRuntimeSnapshot weatherSnapshot)
        {
            return ((uint)(weatherSnapshot.StateMask & Hecton8.Core.WeatherState.Storm)) != 0u
                ? 1f + math.saturate(weatherSnapshot.WeatherIntensity) * 0.22f
                : 1f;
        }

        private static float SampleWeatherGerstnerHeight(
            double2 absoluteXZ,
            in WeatherRuntimeSnapshot weatherSnapshot,
            int activeWaveCount)
        {
            ResolvePrimaryGerstnerWaves(
                in weatherSnapshot,
                out GerstnerWaveComponent wave0,
                out GerstnerWaveComponent wave1,
                out GerstnerWaveComponent wave2);
            int count = math.min(math.max(1, activeWaveCount), MaxGerstnerWaveCount);
            float height = 0f;
            for (int i = 0; i < count; i++)
            {
                GerstnerWaveComponent wave = ResolveGerstnerWaveAtIndex(i, wave0, wave1, wave2);
                height += HectonGerstnerWater.SampleHeight(absoluteXZ, wave, weatherSnapshot.CurrentMeta.TimeAccumulator);
            }

            return math.isfinite(height) ? height : 0f;
        }

        private bool TryResolveTerrainHeightPayload(out TerrainHeightSamplePayloadDTO payload)
        {
            payload = default;
            ITerrainHeightSampleReadModel bridge = _terrainBridge;
            if (bridge == null)
                return false;

            if (lodObserver != null &&
                bridge.TryGetTerrainHeightSamplePayload(lodObserver.position.x, lodObserver.position.z, out payload))
            {
                return true;
            }

            return bridge.TryGetActiveTerrainHeightSamplePayload(out payload);
        }

        private void WriteOceanSurfaceTelemetry(
            int activeCount,
            float waterLevel,
            float maxWaveEnvelope,
            int waveOctaves,
            int terrainRevision,
            float3 windVector)
        {
            if (!_oceanSurfaceTelemetry.IsCreated || _oceanSurfaceTelemetry.Length == 0)
                return;

            int writeIndex = _oceanSurfaceTelemetryWriteIndex;
            _oceanSurfaceTelemetryWriteIndex = (writeIndex + 1) % _oceanSurfaceTelemetry.Length;
            Vector3 observerPosition = lodObserver != null ? lodObserver.position : Vector3.zero;
            _oceanSurfaceTelemetry[writeIndex] = new FluidOceanSurfaceTelemetryEntry
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                OriginShiftSequence = _lastOriginShiftSequence,
                ActiveFloaters = activeCount,
                SleepingFloaters = _lastOceanSleepCount,
                WaveOctaves = waveOctaves,
                TerrainRevision = terrainRevision,
                WaterLevelY = waterLevel,
                MinSurfaceOffset = -maxWaveEnvelope,
                MaxSurfaceOffset = maxWaveEnvelope,
                ObserverWS = new float3(observerPosition.x, observerPosition.y, observerPosition.z),
                WindWS = windVector
            };
        }

        private static string ResolveFluidDumpPath(string relativePath)
        {
            return relativePath;
        }

        private static unsafe void WriteNativeDump(string dumpPath, NativeArray<byte> payload, int byteCount)
        {
            NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount);
        }

        private static bool BinaryFaultDumpsEnabled => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, ref int cursor, ulong value)
        {
            destination[cursor++] = (byte)value;
            destination[cursor++] = (byte)(value >> 8);
            destination[cursor++] = (byte)(value >> 16);
            destination[cursor++] = (byte)(value >> 24);
            destination[cursor++] = (byte)(value >> 32);
            destination[cursor++] = (byte)(value >> 40);
            destination[cursor++] = (byte)(value >> 48);
            destination[cursor++] = (byte)(value >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt64LittleEndian(NativeArray<byte> destination, ref int cursor, long value)
        {
            WriteUInt64LittleEndian(destination, ref cursor, unchecked((ulong)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, ref int cursor, uint value)
        {
            destination[cursor++] = (byte)value;
            destination[cursor++] = (byte)(value >> 8);
            destination[cursor++] = (byte)(value >> 16);
            destination[cursor++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32LittleEndian(NativeArray<byte> destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteFloatLittleEndian(NativeArray<byte> destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private void DumpOceanSurfaceTelemetry()
        {
            if (!_oceanSurfaceTelemetry.IsCreated ||
                _oceanSurfaceTelemetry.Length == 0 ||
                _lastOceanSurfaceDumpFrame == Hecton8.Core.SystemDispatcher.CurrentFrameIndex)
            {
                return;
            }

            _lastOceanSurfaceDumpFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (!BinaryFaultDumpsEnabled)
                return;
            int entryBytes = 60;
            int byteCount = 8 + _oceanSurfaceTelemetry.Length * entryBytes;
            NativeArray<byte> dump = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(HectonFluidEngine),
                "OceanSurfaceTelemetryDumpPayload");
            try
            {
                int cursor = 0;
                WriteInt32LittleEndian(dump, ref cursor, _oceanSurfaceTelemetryWriteIndex);
                WriteInt32LittleEndian(dump, ref cursor, _oceanSurfaceTelemetry.Length);
                for (int i = 0; i < _oceanSurfaceTelemetry.Length; i++)
                {
                    FluidOceanSurfaceTelemetryEntry entry = _oceanSurfaceTelemetry[i];
                    WriteUInt32LittleEndian(dump, ref cursor, entry.FrameIndex);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.OriginShiftSequence);
                    WriteInt32LittleEndian(dump, ref cursor, entry.ActiveFloaters);
                    WriteInt32LittleEndian(dump, ref cursor, entry.SleepingFloaters);
                    WriteInt32LittleEndian(dump, ref cursor, entry.WaveOctaves);
                    WriteInt32LittleEndian(dump, ref cursor, entry.TerrainRevision);
                    WriteFloatLittleEndian(dump, ref cursor, entry.WaterLevelY);
                    WriteFloatLittleEndian(dump, ref cursor, entry.MinSurfaceOffset);
                    WriteFloatLittleEndian(dump, ref cursor, entry.MaxSurfaceOffset);
                    WriteFloatLittleEndian(dump, ref cursor, entry.ObserverWS.x);
                    WriteFloatLittleEndian(dump, ref cursor, entry.ObserverWS.y);
                    WriteFloatLittleEndian(dump, ref cursor, entry.ObserverWS.z);
                    WriteFloatLittleEndian(dump, ref cursor, entry.WindWS.x);
                    WriteFloatLittleEndian(dump, ref cursor, entry.WindWS.y);
                    WriteFloatLittleEndian(dump, ref cursor, entry.WindWS.z);
                }

                WriteNativeDump(ResolveFluidDumpPath(OceanSurfaceDumpPath), dump, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref dump,
                    nameof(HectonFluidEngine),
                    "OceanSurfaceTelemetryDumpPayload");
            }
        }

        private bool EnsureFluidSovereigntyTelemetry()
        {
            if (_dataVault == null)
                return false;

            s_staticFluidDataVault = _dataVault;
            bool ringReady = _fluidSovereigntyTelemetry.Ensure(
                FluidSovereigntyTelemetryRingBufferId,
                FluidSovereigntyTelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            bool cursorReady = _fluidSovereigntyTelemetryCursor.Ensure(
                FluidSovereigntyTelemetryCursorBufferId,
                1,
                NativeArrayOptions.ClearMemory);

            if (!ringReady || !cursorReady)
                return false;

            if (_fluidSovereigntyTelemetryCursor.TryResolve(out NativeArray<int> cursorBuffer) &&
                cursorBuffer.IsCreated &&
                cursorBuffer.Length > 0)
            {
                int cursor = cursorBuffer[0];
                _fluidSovereigntyTelemetryCursorMirror = (uint)cursor < FluidSovereigntyTelemetryCapacity ? cursor : 0;
            }

            return true;
        }

        private bool EnsureOceanAdapterVaultBootHandles()
        {
            if (_oceanAdapterVaultHandlesReady)
                return true;

            if (_oceanAdapterVaultBootAttempted)
                return false;

            _oceanAdapterVaultBootAttempted = true;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            _oceanAdapterVaultHandlesReady = OceanAdapterVaultRoute.TryAcquireBootHandles(vault, out _oceanAdapterVaultHandles);
            return _oceanAdapterVaultHandlesReady;
        }

        private void ResetOceanAdapterVaultRoute()
        {
            _oceanAdapterVaultHandles = default;
            _oceanAdapterVaultHandlesReady = false;
            _oceanAdapterVaultBootAttempted = false;
        }

        private void PublishOceanAdapterGlobalWaterLevel(float cinematicWaterLevel)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!_oceanAdapterVaultHandlesReady && !EnsureOceanAdapterVaultBootHandles())
                return;

            OceanAdapterVaultRoute.TryPublishWaterLevel(
                vault,
                cinematicWaterLevel,
                ResolveFluidAdvectionQualityWeight(),
                Hecton8.Core.SystemDispatcher.CurrentFrameId);
        }

        private void RecordFluidSovereigntyTelemetry(
            BufferID bufferId,
            uint flags,
            int expectedLength,
            int actualLength,
            float cpuMicroseconds,
            float gpuMicroseconds,
            float activeFlowRate)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _fluidSovereigntyConsecutiveFaults++;
                return;
            }

            bool cursorPersisted = TryAdvanceFluidSovereigntyTelemetryCursor(out int cursor);
            if (!TryWriteFluidSovereigntyTelemetryRing(
                    vault,
                    cursor,
                    cursorPersisted,
                    bufferId,
                    flags,
                    expectedLength,
                    actualLength,
                    cpuMicroseconds,
                    gpuMicroseconds,
                    activeFlowRate))
            {
                _fluidSovereigntyConsecutiveFaults++;
            }
        }

        private bool TryAdvanceFluidSovereigntyTelemetryCursor(out int cursor)
        {
            cursor = _fluidSovereigntyTelemetryCursorMirror;
            if (!_fluidSovereigntyTelemetryCursor.TryAcquireWriteLock(out NativeArray<int> cursorBuffer))
                return false;

            try
            {
                if (!cursorBuffer.IsCreated || cursorBuffer.Length == 0)
                    return false;

                cursor = cursorBuffer[0];
                if ((uint)cursor >= FluidSovereigntyTelemetryCapacity)
                    cursor = 0;

                int nextCursor = cursor + 1;
                if (nextCursor >= FluidSovereigntyTelemetryCapacity)
                    nextCursor = 0;

                cursorBuffer[0] = nextCursor;
                _fluidSovereigntyTelemetryCursorMirror = nextCursor;
                return true;
            }
            finally
            {
                _fluidSovereigntyTelemetryCursor.ReleaseWriteLock();
            }
        }

        private bool TryWriteFluidSovereigntyTelemetryRing(
            IDataVault vault,
            int cursor,
            bool cursorPersisted,
            BufferID bufferId,
            uint flags,
            int expectedLength,
            int actualLength,
            float cpuMicroseconds,
            float gpuMicroseconds,
            float activeFlowRate)
        {
            if (!_fluidSovereigntyTelemetry.TryAcquireWriteLock(out NativeArray<FluidTelemetryEntry> ring))
                return false;

            try
            {
                if (!ring.IsCreated || ring.Length == 0)
                    return true;

                if ((uint)cursor >= (uint)ring.Length)
                    cursor = 0;

                uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                FluidTelemetryEntry entry = default;
                entry.VaultAllocatedBytes = vault.AllocatedBytes;
                entry.VaultArenaBytes = vault.ArenaBytes;
                entry.Frame = frame;
                entry.BufferId = (uint)bufferId;
                entry.Generation = ResolveFluidBufferGeneration(vault, bufferId);
                entry.VaultGenerationId = vault.VaultGenerationID;
                entry.Flags = flags;
                entry.ExpectedLength = math.max(0, expectedLength);
                entry.ActualLength = math.max(0, actualLength);
                entry.GlobalQualityWeight = ResolveFluidAdvectionQualityWeight();
                entry.CpuMicroseconds = math.max(0f, math.isfinite(cpuMicroseconds) ? cpuMicroseconds : 0f);
                entry.GpuMicroseconds = math.max(0f, math.isfinite(gpuMicroseconds) ? gpuMicroseconds : 0f);
                entry.ActiveFlowRate = math.max(0f, math.isfinite(activeFlowRate) ? activeFlowRate : 0f);
                entry.StateHash = HashFluidTelemetry(
                    entry.Frame,
                    entry.BufferId,
                    entry.Generation,
                    entry.Flags,
                    entry.ExpectedLength,
                    entry.ActualLength);
                ring[cursor] = entry;

                if (!cursorPersisted)
                {
                    cursor++;
                    if (cursor >= ring.Length)
                        cursor = 0;
                    _fluidSovereigntyTelemetryCursorMirror = cursor;
                }

                _fluidSovereigntyConsecutiveFaults = 0;
                return true;
            }
            finally
            {
                _fluidSovereigntyTelemetry.ReleaseWriteLock();
            }
        }

        private static uint ResolveFluidBufferGeneration(IDataVault vault, BufferID bufferId)
        {
            return vault != null && vault.TryGetBufferGeneration(bufferId, out uint generation) ? generation : 0u;
        }

        private static uint HashFluidTelemetry(
            uint frame,
            uint bufferId,
            uint generation,
            uint flags,
            int expectedLength,
            int actualLength)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ frame) * 16777619u;
                hash = (hash ^ bufferId) * 16777619u;
                hash = (hash ^ generation) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                hash = (hash ^ (uint)expectedLength) * 16777619u;
                hash = (hash ^ (uint)actualLength) * 16777619u;
                return hash;
            }
        }

#if UNITY_EDITOR
        public static bool ValidateFluidMemorySovereigntyLayout1322(out int failureMask)
        {
            failureMask = 0;
            failureMask |= UnsafeUtility.SizeOf<FluidTelemetryEntry>() == FluidSovereigntyTelemetryEntrySizeBytes ? 0 : 1 << 0;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.VaultAllocatedBytes)) == 0 ? 0 : 1 << 1;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.VaultArenaBytes)) == 8 ? 0 : 1 << 2;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.Frame)) == 16 ? 0 : 1 << 3;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.BufferId)) == 20 ? 0 : 1 << 4;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.Generation)) == 24 ? 0 : 1 << 5;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.VaultGenerationId)) == 28 ? 0 : 1 << 6;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.Flags)) == 32 ? 0 : 1 << 7;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.StateHash)) == 36 ? 0 : 1 << 8;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.ExpectedLength)) == 40 ? 0 : 1 << 9;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.ActualLength)) == 44 ? 0 : 1 << 10;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.GlobalQualityWeight)) == 48 ? 0 : 1 << 11;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.CpuMicroseconds)) == 52 ? 0 : 1 << 12;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.GpuMicroseconds)) == 56 ? 0 : 1 << 13;
            failureMask |= OffsetOf<FluidTelemetryEntry>(nameof(FluidTelemetryEntry.ActiveFlowRate)) == 60 ? 0 : 1 << 14;
            failureMask |= UnsafeUtility.SizeOf<FluidImpactEvent>() == 32 ? 0 : 1 << 15;
            failureMask |= UnsafeUtility.SizeOf<FluidOceanSurfaceTelemetryEntry>() == 64 ? 0 : 1 << 16;
            failureMask |= UnsafeUtility.SizeOf<FluidAdvectionTelemetryEntry>() == 64 ? 0 : 1 << 17;
            failureMask |= UnsafeUtility.SizeOf<BuoyancyParams>() == BuoyancyParams.StrideBytes ? 0 : 1 << 18;
            return failureMask == 0;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
#endif

        private void DumpFluidSovereigntyTelemetryOnce(uint reasonFlags)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastFluidSovereigntyDumpFrame == frame)
                return;

            _lastFluidSovereigntyDumpFrame = frame;
            RecordFluidSovereigntyTelemetry(
                FluidSovereigntyTelemetryRingBufferId,
                reasonFlags | FluidTelemetryFlagDump,
                FluidSovereigntyTelemetryCapacity,
                _fluidSovereigntyTelemetry.Length,
                0f,
                0f,
                _objects.Count);

            if (!BinaryFaultDumpsEnabled)
                return;
            NativeArray<FluidTelemetryEntry>.ReadOnly ring = _fluidSovereigntyTelemetry.AsReadOnly();
            if (!ring.IsCreated || ring.Length == 0)
                return;

            int byteCount = 20 + ring.Length * FluidSovereigntyTelemetryEntrySizeBytes;
            NativeArray<byte> dump = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(HectonFluidEngine),
                "FluidSovereigntyTelemetryDumpPayload");
            try
            {
                int cursor = 0;
                WriteUInt32LittleEndian(dump, ref cursor, FluidSovereigntyTelemetryMagic);
                WriteInt32LittleEndian(dump, ref cursor, FluidSovereigntyTelemetryCapacity);
                WriteInt32LittleEndian(dump, ref cursor, _fluidSovereigntyTelemetryCursorMirror);
                WriteUInt32LittleEndian(dump, ref cursor, reasonFlags);
                WriteInt32LittleEndian(dump, ref cursor, UnsafeUtility.SizeOf<FluidTelemetryEntry>());
                for (int i = 0; i < ring.Length; i++)
                {
                    int index = _fluidSovereigntyTelemetryCursorMirror + i;
                    if (index >= ring.Length)
                        index -= ring.Length;

                    FluidTelemetryEntry entry = ring[index];
                    WriteInt64LittleEndian(dump, ref cursor, entry.VaultAllocatedBytes);
                    WriteInt64LittleEndian(dump, ref cursor, entry.VaultArenaBytes);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.Frame);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.BufferId);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.Generation);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.VaultGenerationId);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.Flags);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.StateHash);
                    WriteInt32LittleEndian(dump, ref cursor, entry.ExpectedLength);
                    WriteInt32LittleEndian(dump, ref cursor, entry.ActualLength);
                    WriteFloatLittleEndian(dump, ref cursor, entry.GlobalQualityWeight);
                    WriteFloatLittleEndian(dump, ref cursor, entry.CpuMicroseconds);
                    WriteFloatLittleEndian(dump, ref cursor, entry.GpuMicroseconds);
                    WriteFloatLittleEndian(dump, ref cursor, entry.ActiveFlowRate);
                }

                WriteNativeDump(ResolveFluidDumpPath(FluidSovereigntyDumpRelativePath), dump, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref dump,
                    nameof(HectonFluidEngine),
                    "FluidSovereigntyTelemetryDumpPayload");
            }
        }

        /// <summary>
        /// Simulated seconds accumulated from the dispatcher-supplied fixed delta and drained by
        /// LateFrameTick for the no-compute CPU fluid fallback. ILateFrameTickable intentionally
        /// passes no delta, and ITickable states plainly that implementations must not read Unity
        /// frame time, so the fallback sources its timestep from here instead of Time.deltaTime.
        /// </summary>
        private float _cpuFluidFallbackAccumulatedSeconds;

        /// <summary>
        /// Reusable CPU-fallback velocity-component scratch for RunCpuFluidAdvectionFallback.
        /// Allocated once at max resolution and reused across frames so the GPU-less tier stays
        /// within the Zero-GC hot-path budget instead of allocating managed float[] per call.
        /// Sized to the resolved noise-field length (VectorNoiseResolution cubed).
        /// </summary>
        private float[] _cpuFallbackAdvectionVelX;
        private float[] _cpuFallbackAdvectionVelY;
        private float[] _cpuFallbackAdvectionVelZ;

        /// <summary>
        /// Ensures the three reusable 1D velocity scratch buffers are large enough for the given
        /// element count, allocating only when they are too small (once at first use / resolution
        /// change). Mirrors the EnsureFluidAdvectionDirtyPageUploadSnapshot pattern used elsewhere
        /// in this type: cold allocation, never per-frame once sized.
        /// </summary>
        private void EnsureCpuFallbackAdvectionVelocityArrays(int length)
        {
            if (_cpuFallbackAdvectionVelX == null || _cpuFallbackAdvectionVelX.Length < length)
            {
                // COLD ALLOC: float[length] - CPU-fallback fluid advection velocity-component scratch reused across frames - owner: HectonFluidEngine
                _cpuFallbackAdvectionVelX = new float[length];
                _cpuFallbackAdvectionVelY = new float[length];
                _cpuFallbackAdvectionVelZ = new float[length];
            }
        }

        /// <summary>Reusable 3D velocity/force/pressure grids for the GPU-less CPU fluid simulation
        /// fallback. Allocated once at grid resolution and reused across frames so the low tier
        /// stays within the Zero-GC hot-path budget.</summary>
        private float[,,] _cpuFallbackGridVelX;
        private float[,,] _cpuFallbackGridVelY;
        private float[,,] _cpuFallbackGridVelZ;
        private float[,,] _cpuFallbackGridFx;
        private float[,,] _cpuFallbackGridFy;
        private float[,,] _cpuFallbackGridFz;
        private Vector3[,,] _cpuFallbackGridVorticity;
        private float[,,] _cpuFallbackGridVorticityMag;
        private float[,,] _cpuFallbackGridDivergence;
        private float[,,] _cpuFallbackGridPressureA;
        private float[,,] _cpuFallbackGridPressureB;

        /// <summary>
        /// Ensures the reusable 3D CPU-fallback grids are allocated to the given grid dimension n,
        /// allocating only when they are missing or too small (once at first use / resolution
        /// change). Mirrors EnsureCpuFallbackAdvectionVelocityArrays: cold allocation, never
        /// per-frame once sized.
        /// </summary>
        private void EnsureCpuFallbackSimulationGrids(int n)
        {
            if (_cpuFallbackGridVelX != null && _cpuFallbackGridVelX.GetLength(0) >= n)
            {
                return;
            }
            // COLD ALLOC: float[n,n,n] x3 velocity grids + force/vorticity/pressure scratch reused across frames - owner: HectonFluidEngine
            _cpuFallbackGridVelX = new float[n, n, n];
            _cpuFallbackGridVelY = new float[n, n, n];
            _cpuFallbackGridVelZ = new float[n, n, n];
            _cpuFallbackGridFx = new float[n, n, n];
            _cpuFallbackGridFy = new float[n, n, n];
            _cpuFallbackGridFz = new float[n, n, n];
            _cpuFallbackGridVorticity = new Vector3[n, n, n];
            _cpuFallbackGridVorticityMag = new float[n, n, n];
            _cpuFallbackGridDivergence = new float[n, n, n];
            _cpuFallbackGridPressureA = new float[n, n, n];
            _cpuFallbackGridPressureB = new float[n, n, n];
        }

        public void FixedTick(float fixedDeltaTime)
        {
            // L19 hop2 LIVE: FixedTick buoyancy drain/ApplyScheduledForces first-touches
            // fluid job + Physics.AddForce paths that have produced mono_jit AV under
            // headless batch probes after WORLDDRIVER/INPUTHOP (FluidBuoyancySystem.Tick).
            // Probe moment census does not need live buoyancy forces - skip under batchmode only.
            if (Application.isBatchMode)
                return;

            // Accumulated before any early return below so the fallback never loses a timestep.
            _cpuFluidFallbackAccumulatedSeconds += fixedDeltaTime;

            using (ProfilerRegistry.PhysicsTick.Auto())
            {
            WeatherRuntimeSnapshot fixedWeatherSnapshot = ResolveWeatherSnapshot();
            float cinematicWaterLevel = PublishCurrentWaterLevelUniform(in fixedWeatherSnapshot);
            PublishOceanAdapterGlobalWaterLevel(cinematicWaterLevel);

            if (!TryDrainScheduledBuoyancyJob())
                return;

            if (lodObserver == null)
            {
                _observerResolveRetryTimer -= fixedDeltaTime;
                if (_observerResolveRetryTimer <= 0f)
                    TryResolveObserver(force: false);
            }

            if (GpuBuoyancySurfaceParityAvailable && enableGpuBuoyancySampling)
                QueueGpuBuoyancyReadbackConsume();

            _resolvedGiantWakeCurrent = ResolveGiantWakeCurrentBase();
            _debugGiantWakeCurrent = new Vector3(_resolvedGiantWakeCurrent.x, _resolvedGiantWakeCurrent.y, _resolvedGiantWakeCurrent.z);
            DrainSplashdownFluidSignals(cinematicWaterLevel);
            UpdateSplashdownImpulseState(fixedDeltaTime);
            QueueAbyssalFlowVisualSync(in fixedWeatherSnapshot, cinematicWaterLevel, fixedDeltaTime);

            int count = _objects.Count;
            if (count == 0)
            {
                _lastOceanSleepCount = 0;
                PublishOceanSurfaceWaveUniformsFromWeather(in fixedWeatherSnapshot, 0);
                ReleaseIdleNativeBuffersIfNeeded();
                return;
            }
            _debugNearCount = 0;
            _debugMediumCount = 0;
            _debugFarCount = 0;
            _debugCulledCount = 0;
            _lodFrameCounter++;

            if (lodObserver == null)
            {
                _observerResolveRetryTimer -= fixedDeltaTime;
                if (_observerResolveRetryTimer <= 0f)
                    TryResolveObserver(force: false);
            }

            // ── 1. Ensure capacity (Capacity Doubling) ──
            if (!TryResolveBuoyancyNativeCapacityHot(count, recordFault: true))
            {
                PublishOceanSurfaceWaveUniformsFromWeather(in fixedWeatherSnapshot, 0);
                return;
            }

            // ── 2. Gather (mozhet umenshit _objects.Count pri ochistke null) ──
            GatherData(cinematicWaterLevel);

            // Pereschityvaem count posle ochistki destroyed obektov
            count = _objects.Count;
            if (count == 0)
            {
                _lastOceanSleepCount = 0;
                PublishOceanSurfaceWaveUniformsFromWeather(in fixedWeatherSnapshot, 0);
                ReleaseIdleNativeBuffersIfNeeded();
                return;
            }

            // ── 3. Schedule Job ──
            using (_jobScheduleProfilerMarker.Auto())
            {
            for (int i = 0; i < count; i++)
                _scheduledBodies[i] = _bodies[i];

            WeatherRuntimeSnapshot weatherSnapshot = fixedWeatherSnapshot;
            _resolvedGiantWakeCurrent = ResolveGiantWakeCurrentBase();
            _debugGiantWakeCurrent = new Vector3(_resolvedGiantWakeCurrent.x, _resolvedGiantWakeCurrent.y, _resolvedGiantWakeCurrent.z);
            PopulateGerstnerWaveData(in weatherSnapshot, out int activeWaveCount, out float maxWaveEnvelope);
            bool hasTerrainPayload = TryResolveTerrainHeightPayload(out TerrainHeightSamplePayloadDTO terrainPayload);
            CopyAnalyticalFlowInputsToNative();
            bool gpuSurfaceParityEnabled = GpuBuoyancySurfaceParityAvailable && enableGpuBuoyancySampling;
            if (gpuSurfaceParityEnabled)
            {
                QueueGpuBuoyancySampling(weatherSnapshot, count, cinematicWaterLevel);
            }
            var vectorNoiseField = _prebakedVectorNoiseField.IsCreated
                ? _prebakedVectorNoiseField
                : default;
            int vectorNoiseLength = _prebakedVectorNoiseField.IsCreated ? _prebakedVectorNoiseField.Length : 0;
            double3 vectorNoiseAupOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double2 waveAupOffsetXZ = new double2(vectorNoiseAupOffset.x, vectorNoiseAupOffset.z);
            byte detailedMathEnabled = AuthorityFluidDetailedMathEnabled;

            JobHandle waveHandle = default;
            bool useGpuBuoyancy = gpuSurfaceParityEnabled &&
                                  gpuBuoyancyCompute != null &&
                                  count >= gpuBuoyancyActivationThreshold &&
                                  _hasGpuBuoyancyData;
            if (!useGpuBuoyancy)
            {
                WaveQueryJob waveJob = new WaveQueryJob
                {
                    PositionsWS = _positions,
                    ObjParams = _params,
                    VerticalOffsets = _waveOffsets,
                    SurfaceUpVectors = _surfaceUpVectors,
                    Waves = _gerstnerWaves,
                    // NEVER `default` here. An unconstructed NativeArray inside a SCHEDULED job throws
                    // InvalidOperationException under the Jobs safety system regardless of whether the job
                    // body reads it - and HasTerrainHeightPayload below already tells the body not to.
                    //
                    // Measured cost of getting this wrong, from Logs/omega_route28.log: 143 identical
                    // "WaveQueryJob.TerrainHeightSamples has not been assigned or constructed" exceptions,
                    // one per frame, thrown out of waveJob.Schedule below. This runs in FixedTick, and the
                    // dispatcher's fixed lane walk has NO try/catch, so the throw unwound out of
                    // RunDispatcherUpdate every frame. This engine is PriorityLayer.Environment = lane 1, the
                    // PLAYER is lane 2, and the walk goes 0->3, so the player's whole fixed lane never ran.
                    //
                    // Every number the Swim route row printed was that one throw: movementIntent01max=0.000
                    // because HectonPlayerMovement.FixedTick is the sole writer of the intent field;
                    // depth=0.000 and pressure=1.000 because RunSlowTick sits after the throw point so
                    // HectonSurvivalSystem.SlowTick never ran; oxygen frozen at its init value; and
                    // immersionMax=1.000 a frozen cold-init reading rather than a measurement. It could not
                    // surface before tonight because the boot never completed - the first throw lands
                    // immediately after "[GameBootstrapper] Complete".
                    TerrainHeightSamples = hasTerrainPayload
                        ? terrainPayload.HeightSamples
                        : EnsureEmptyTerrainHeightSamples(),
                    WaveCount = activeWaveCount,
                    TimeSeconds = weatherSnapshot.CurrentMeta.TimeAccumulator,
                    WaterLevelY = cinematicWaterLevel,
                    MaxWaveEnvelope = maxWaveEnvelope,
                    AupOffsetXZ = waveAupOffsetXZ,
                    TerrainPosition = hasTerrainPayload
                        ? new float3(terrainPayload.TerrainPosition.x, terrainPayload.TerrainPosition.y, terrainPayload.TerrainPosition.z)
                        : float3.zero,
                    TerrainSize = hasTerrainPayload
                        ? new float3(terrainPayload.TerrainSize.x, terrainPayload.TerrainSize.y, terrainPayload.TerrainSize.z)
                        : float3.zero,
                    TerrainHeightmapResolution = hasTerrainPayload ? terrainPayload.HeightmapResolution : 0,
                    HasTerrainHeightPayload = hasTerrainPayload ? (byte)1 : (byte)0,
                    ShoreFallbackBandMeters = ShoreTerrainFallbackBandMeters,
                    NormalSampleDistanceMeters = OceanSurfaceNormalSampleMeters,
                    CalculateSurfaceNormals = detailedMathEnabled
                };

                waveHandle = waveJob.Schedule(count, jobBatchSize);
            }

            BuoyancyJob job = new BuoyancyJob
            {
                positions        = _positions,
                previousPositions = _previousPositions,
                previousPositionValid = _previousPositionValid,
                velocities       = _velocities,
                angularVelocities = _angularVelocities,
                upVectors        = _upVectors,
                surfaceUpVectors = _surfaceUpVectors,
                objParams        = _params,
                waveOffsets      = _waveOffsets,
                gpuBuoyancyForcesY = _gpuBuoyancyForcesY,
                brineHeights = _brineHeights,
                brineDensityMultipliers = _brineDensityMultipliers,
                brineFlags = _brineFlags,
                activeThrusters = _activeThrusterFlows,
                activeWhirlpools = _activeWhirlpools,
                activeViscosityRegions = _activeViscosityRegions,
                viscosityGradientLut = _viscosityGradientLut,
                vectorNoiseField = vectorNoiseField,
                vectorNoiseFieldLength = vectorNoiseLength,
                activeThrusterCount = _activeThrusterFlowCount,
                activeWhirlpoolCount = _activeWhirlpoolFlowCount,
                activeViscosityRegionCount = _activeViscosityRegionCount,
                impactEvents = _impactEventScratch,
                impactEventFlags = _impactEventFlags,
                resultForces     = _resultForces,
                resultTorques    = _resultTorques,
                mathGuardWriter = MathGuard.AsParallelWriter(),
                forceNanErrorCode = _buoyancyForceNanErrorCode,
                torqueNanErrorCode = _buoyancyTorqueNanErrorCode,

                waterLevel       = cinematicWaterLevel,
                waterDensity     = waterDensity,
                viscousDrag      = viscousDrag,
                maxQuadraticDragForcePerKg = maxQuadraticDragForcePerKg,
                angularDragCoeff = angularDrag,
                gravity          = math.abs(UnityEngine.Physics.gravity.y),
                baseCurrentForce = new float3(
                    currentVector.x * currentStrength,
                    currentVector.y * currentStrength,
                    currentVector.z * currentStrength),
                giantWakeCurrent = _resolvedGiantWakeCurrent,
                giantWakeDepthFadeStart = giantWakeDepthFadeStart,
                giantWakeDepthFadeRange = giantWakeDepthFadeRange,
                enableTidalShearZones = (detailedMathEnabled != 0 && enableTidalShearZones) ? (byte)1 : (byte)0,
                tidalShearTorqueStrength = tidalShearTorqueStrength,
                tidalShearFrequency = tidalShearFrequency,
                time             = math.isfinite(weatherSnapshot.CurrentMeta.TimeAccumulator) &&
                                   weatherSnapshot.CurrentMeta.TimeAccumulator > 0f
                    ? weatherSnapshot.CurrentMeta.TimeAccumulator
                    : ResolveFluidFallbackClockSeconds(),
                weatherStateMask = (uint)weatherSnapshot.StateMask,
                weatherCurrentDirection = weatherSnapshot.CurrentMeta.GlobalBaseVector,
                weatherCurrentScale = weatherSnapshot.CurrentMeta.GlobalScale,
                weatherBlend = weatherSnapshot.WeatherIntensity,
                windAdvectionVector = weatherSnapshot.GlobalWindVector,
                windAdvectionForcePerKg = SurfaceWindAdvectionForcePerKg,
                splashDepthThresholdMeters = SplashDepthThresholdMeters,
                splashVelocityThresholdSq = SplashVelocityThresholdMetersPerSecond * SplashVelocityThresholdMetersPerSecond,
                enablePhantomCurrent = enablePhantomCurrent ? (byte)1 : (byte)0,
                currentNoiseScale = currentNoiseScale,
                currentTimeScale = currentTimeScale,
                currentVerticalFactor = currentVerticalFactor,
                phantomCurrentStrength = phantomCurrentStrength,
                vectorNoiseAupOffset = vectorNoiseAupOffset,
                brineShiftOffsetY = math.isfinite(vectorNoiseAupOffset.y) ? (float)vectorNoiseAupOffset.y : 0f,
                vectorNoiseInvCellSize = math.rcp(math.max(0.25f, prebakedVectorNoiseCellSizeMeters)),
                enablePrebakedVectorNoise = enablePrebakedVectorNoise ? (byte)1 : (byte)0,
                vectorNoiseTriangleModulation = prebakedVectorNoiseTriangleModulation,
                detailedMathEnabled = detailedMathEnabled,
                enableAnalyticalFlowField = enableAnalyticalFlowField ? (byte)1 : (byte)0,
                haloclineBoundaryDepthMeters = haloclineBoundaryDepthMeters,
                deepLayerDensityMultiplier = deepLayerDensityMultiplier,
                haloclineShearForcePerKg = haloclineShearForcePerKg,
                enableDynamicViscosityRegions = enableDynamicViscosityRegions ? (byte)1 : (byte)0,
                useGpuBuoyancyForce = useGpuBuoyancy ? (byte)1 : (byte)0
            };

            _scheduledBuoyancyHandle = job.Schedule(count, jobBatchSize, waveHandle);
            WriteOceanSurfaceTelemetry(count, cinematicWaterLevel, maxWaveEnvelope, activeWaveCount, hasTerrainPayload ? terrainPayload.CacheRevision : 0, weatherSnapshot.GlobalWindVector);
            }

            // ── 4. Complete ──

            // ── 5. Apply forces ──
            _scheduledBuoyancyJobActive = true;
            _scheduledForceCount = count;
            }
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            PublishMaelstromRuntimeSignals();
            DrainCavitationBursts();

            TryDrainScheduledBuoyancyJob();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            FlushCurrentWaterLevelUniform();
            FlushOceanSurfaceWaveUniforms();
            FlushQueuedFluidGraphicsReleases();
            TryCompleteSplashdownImpulseJobForUpload();
            FlushAbyssalFlowVisualSync();
            FlushAbyssalFlowGlobalPublication();
            FlushCavitationVisualBursts();
            FlushGpuBuoyancyReadbackConsume();
            FlushGpuBuoyancySampling();
            RefreshFluidAdvectionVisualStateReadyCached();
            DrainFluidAdvectionSignals();
            bool fluidAdvectionGpuUploadReady = FlushFluidAdvectionGpuUploads();
            bool fluidAdvectionReady = fluidAdvectionGpuUploadReady && IsFluidAdvectionReady();
            bool hasAdvectionParticles = _activeAdvectedSiltCount > 0 ||
                                         _activeAdvectedBubbleCount > 0 ||
                                         _activeAdvectedDebrisCount > 0;
            if (fluidAdvectionReady && hasAdvectionParticles)
                RefreshDynamicWakeGpuPayload(ResolveFluidAdvectionQualityWeight());
            else
                ClearDynamicWakeGpuPayload();

            _fluidAdvectionRenderGraphQueued = fluidAdvectionReady &&
                                               hasAdvectionParticles &&
                                               _fluidAdvectionKernel >= 0;

            if (!_coldSupportsComputeShaders)
            {
                float cpuFallbackDeltaSeconds = _cpuFluidFallbackAccumulatedSeconds;
                _cpuFluidFallbackAccumulatedSeconds = 0f;
                if (cpuFallbackDeltaSeconds > 0f)
                {
                    RunCpuFluidSimulationFallback(cpuFallbackDeltaSeconds);
                    if (hasAdvectionParticles)
                    {
                        RunCpuFluidAdvectionFallback(cpuFallbackDeltaSeconds);
                    }
                }
            }

            WriteFluidAdvectionTelemetry();
            TryDrainScheduledBuoyancyJob();
        }

        public bool TryClaimFluidAdvectionRenderGraphPayload(out FluidAdvectionRenderGraphPayload payload)
        {
            payload = default;
            if (!_fluidAdvectionRenderGraphQueued || !IsFluidAdvectionReady())
                return false;

            int maxCount = math.max(_activeAdvectedSiltCount, math.max(_activeAdvectedBubbleCount, _activeAdvectedDebrisCount));
            if (maxCount <= 0)
                return false;

            bool readA = (_fluidAdvectionBufferParity & 1) == 0;
            GraphicsBuffer siltRead = readA ? _advectedSiltBufferA : _advectedSiltBufferB;
            GraphicsBuffer siltWrite = readA ? _advectedSiltBufferB : _advectedSiltBufferA;
            GraphicsBuffer bubbleRead = readA ? _advectedBubbleBufferA : _advectedBubbleBufferB;
            GraphicsBuffer bubbleWrite = readA ? _advectedBubbleBufferB : _advectedBubbleBufferA;
            GraphicsBuffer debrisRead = readA ? _advectedDebrisBufferA : _advectedDebrisBufferB;
            GraphicsBuffer debrisWrite = readA ? _advectedDebrisBufferB : _advectedDebrisBufferA;

            bool hasFlowBuffer = TryGetGpuAbyssalFlowFieldBuffer(
                out GraphicsBuffer flowBuffer,
                out Vector4 gridResolution,
                out Vector4 flowCenter,
                out Vector4 flowSpacing);
            bool hasFlowTexture = TryGetGpuAbyssalFlowFieldTexture(
                out Texture flowTexture,
                out Vector4 textureResolution,
                out Vector4 textureCenter,
                out Vector4 textureSpacing);
            if (hasFlowTexture)
            {
                gridResolution = textureResolution;
                flowCenter = textureCenter;
                flowSpacing = hasFlowBuffer ? flowSpacing : Vector4.zero;
            }

            Texture sdfTexture = _emptyFluidAdvectionTexture;
            Matrix4x4 sdfWorldToLocal = Matrix4x4.identity;
            Vector4 sdfInvDoubleHalfExtents = Vector4.zero;
            float sdfActive = 0f;
            HectonCaveVoxelLightingVolume caveVolume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (caveVolume != null &&
                caveVolume.TryGetPublishedGpuSdfPayload(
                    out Texture3D publishedSdfTexture,
                    out Matrix4x4 publishedWorldToLocal,
                    out _,
                    out Vector4 publishedInvDoubleHalfExtents))
            {
                sdfTexture = publishedSdfTexture;
                sdfWorldToLocal = publishedWorldToLocal;
                sdfInvDoubleHalfExtents = publishedInvDoubleHalfExtents;
                sdfActive = 1f;
            }

            float advectionQualityWeight = ResolveFluidAdvectionQualityWeight();
            TryGetDynamicWakeGpuPayload(
                out GraphicsBuffer dynamicWakeBuffer,
                out GraphicsBuffer dynamicWakeVectorBuffer,
                out Vector4 dynamicWakeParams);

            Texture resolvedFlowTexture = hasFlowTexture ? flowTexture : _emptyFluidAdvectionTexture;
            Texture resolvedSdfTexture = sdfTexture != null ? sdfTexture : _emptyFluidAdvectionTexture;
            RTHandle resolvedFlowTextureHandle = ResolveFluidAdvectionFlowTextureHandle(resolvedFlowTexture, allowAllocate: false);
            if (resolvedFlowTextureHandle == null)
            {
                resolvedFlowTexture = _emptyFluidAdvectionTexture;
                resolvedFlowTextureHandle = _emptyFluidAdvectionTextureHandle;
                hasFlowTexture = false;
                textureSpacing = Vector4.zero;
            }

            RTHandle resolvedSdfTextureHandle = ResolveFluidAdvectionSdfTextureHandle(resolvedSdfTexture, allowAllocate: false);
            if (resolvedSdfTextureHandle == null)
            {
                resolvedSdfTexture = _emptyFluidAdvectionTexture;
                resolvedSdfTextureHandle = _emptyFluidAdvectionTextureHandle;
                sdfWorldToLocal = Matrix4x4.identity;
                sdfInvDoubleHalfExtents = Vector4.zero;
                sdfActive = 0f;
            }

            if (resolvedFlowTextureHandle == null ||
                resolvedSdfTextureHandle == null ||
                _emptyFluidAdvectionTextureHandle == null)
            {
                return false;
            }

            payload = new FluidAdvectionRenderGraphPayload
            {
                Compute = fluidAdvectionCompute,
                Kernel = _fluidAdvectionKernel,
                DispatchGroups = CeilDividePositive(maxCount, _fluidAdvectionThreadGroupSizeX),
                SiltRead = siltRead,
                SiltWrite = siltWrite,
                BubbleRead = bubbleRead,
                BubbleWrite = bubbleWrite,
                DebrisRead = debrisRead,
                DebrisWrite = debrisWrite,
                EmptySiltBuffer = _emptyAdvectedSiltBuffer,
                EmptyBubbleBuffer = _emptyAdvectedBubbleBuffer,
                EmptyDebrisBuffer = _emptyAdvectedDebrisBuffer,
                AbyssalFlowBuffer = hasFlowBuffer ? flowBuffer : _emptyAbyssalFlowBuffer,
                EmptyAbyssalFlowBuffer = _emptyAbyssalFlowBuffer,
                AbyssalFlowTexture = resolvedFlowTexture,
                VoxelSdfTexture = resolvedSdfTexture,
                EmptyVoxelSdfTexture = _emptyFluidAdvectionTexture,
                AbyssalFlowTextureHandle = resolvedFlowTextureHandle,
                VoxelSdfTextureHandle = resolvedSdfTextureHandle,
                EmptyVoxelSdfTextureHandle = _emptyFluidAdvectionTextureHandle,
                Counts = new Vector4(_activeAdvectedSiltCount, _activeAdvectedBubbleCount, _activeAdvectedDebrisCount, maxCount),
                Params = new Vector4(
                    math.max(SystemDispatcher.CurrentFrameDeltaTime, 0.0001f),
                    1f - SmoothFluidAdvectionQuality(advectionQualityWeight),
                    (hasFlowTexture || hasFlowBuffer) ? 1f : 0f,
                    sdfActive),
                Buoyancy = new Vector4(
                    SiltAdvectionBuoyancyMetersPerSecond,
                    BubbleAdvectionBuoyancyMetersPerSecond,
                    DebrisAdvectionBuoyancyMetersPerSecond,
                    FluidAdvectionVelocityBlend),
                AupShiftDelta = new Vector4(
                    _pendingFluidAdvectionRuntimeShift.x,
                    _pendingFluidAdvectionRuntimeShift.y,
                    _pendingFluidAdvectionRuntimeShift.z,
                    0f),
                DynamicWakeBuffer = dynamicWakeBuffer,
                DynamicWakeVectorBuffer = dynamicWakeVectorBuffer,
                DynamicWakeParams = dynamicWakeParams,
                AbyssalGridResolution = gridResolution,
                AbyssalFlowCenter = flowCenter,
                AbyssalFlowSpacing = flowSpacing,
                AbyssalFlowTextureParams = hasFlowTexture ? textureSpacing : Vector4.zero,
                AbyssalFlowTextureActive = hasFlowTexture ? 1f : 0f,
                AbyssalFlowInterpolationAlpha = _gpuAbyssalFlowInterpolationAlpha,
                VoxelSdfWorldToLocal = sdfWorldToLocal,
                VoxelSdfInvDoubleHalfExtents = sdfInvDoubleHalfExtents,
                SdfParams = new Vector4(sdfActive, FluidAdvectionSdfSolidThreshold, 0f, 0f)
            };

            _pendingFluidAdvectionRuntimeShift = default;
            _fluidAdvectionBufferParity ^= 1;
            _fluidAdvectionRenderGraphQueued = false;
            return true;
        }

        internal static void BindFluidAdvectionCompute(CommandBuffer cmd, in FluidAdvectionRenderGraphPayload payload)
        {
            ComputeShader compute = payload.Compute;
            int kernel = payload.Kernel;
            cmd.SetComputeBufferParam(compute, kernel, _SiltReadId, payload.SiltRead);
            cmd.SetComputeBufferParam(compute, kernel, _SiltWriteId, payload.SiltWrite);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleReadId, payload.BubbleRead);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleWriteId, payload.BubbleWrite);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisReadId, payload.DebrisRead);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisWriteId, payload.DebrisWrite);
            cmd.SetComputeBufferParam(compute, kernel, _AbyssalFlowFieldResultId, payload.AbyssalFlowBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakesId, payload.DynamicWakeBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakeVectorsId, payload.DynamicWakeVectorBuffer);
            cmd.SetComputeTextureParam(compute, kernel, _AbyssalFlowFieldTextureId, payload.AbyssalFlowTexture);
            cmd.SetComputeTextureParam(compute, kernel, _VoxelSdfTexture3DId, payload.VoxelSdfTexture);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionCountsId, payload.Counts);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionParamsId, payload.Params);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionBuoyancyId, payload.Buoyancy);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionAupShiftDeltaId, payload.AupShiftDelta);
            cmd.SetComputeVectorParam(compute, _DynamicWakeParamsId, payload.DynamicWakeParams);
            cmd.SetComputeVectorParam(compute, _AbyssalGridResolutionId, payload.AbyssalGridResolution);
            cmd.SetComputeVectorParam(compute, _AbyssalFlowCenterId, payload.AbyssalFlowCenter);
            cmd.SetComputeVectorParam(compute, _AbyssalFlowSpacingId, payload.AbyssalFlowSpacing);
            cmd.SetComputeVectorParam(compute, _AbyssalFlowTextureParamsId, payload.AbyssalFlowTextureParams);
            cmd.SetComputeFloatParam(compute, _AbyssalFlowTextureActiveId, payload.AbyssalFlowTextureActive);
            cmd.SetComputeFloatParam(compute, _AbyssalFlowInterpolationAlphaId, payload.AbyssalFlowInterpolationAlpha);
            cmd.SetComputeMatrixParam(compute, _VoxelSdfWorldToLocalId, payload.VoxelSdfWorldToLocal);
            cmd.SetComputeVectorParam(compute, _VoxelSdfInvDoubleHalfExtentsId, payload.VoxelSdfInvDoubleHalfExtents);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionSdfParamsId, payload.SdfParams);
        }

        internal static void BindFluidAdvectionCompute(
            IComputeCommandBuffer cmd,
            in FluidAdvectionRenderGraphPayload payload,
            TextureHandle abyssalFlowTexture,
            TextureHandle voxelSdfTexture)
        {
            ComputeShader compute = payload.Compute;
            int kernel = payload.Kernel;
            cmd.SetComputeBufferParam(compute, kernel, _SiltReadId, payload.SiltRead);
            cmd.SetComputeBufferParam(compute, kernel, _SiltWriteId, payload.SiltWrite);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleReadId, payload.BubbleRead);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleWriteId, payload.BubbleWrite);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisReadId, payload.DebrisRead);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisWriteId, payload.DebrisWrite);
            cmd.SetComputeBufferParam(compute, kernel, _AbyssalFlowFieldResultId, payload.AbyssalFlowBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakesId, payload.DynamicWakeBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakeVectorsId, payload.DynamicWakeVectorBuffer);
            cmd.SetComputeTextureParam(compute, kernel, _AbyssalFlowFieldTextureId, abyssalFlowTexture);
            cmd.SetComputeTextureParam(compute, kernel, _VoxelSdfTexture3DId, voxelSdfTexture);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionCountsId, payload.Counts);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionParamsId, payload.Params);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionBuoyancyId, payload.Buoyancy);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionAupShiftDeltaId, payload.AupShiftDelta);
            cmd.SetComputeVectorParam(compute, _DynamicWakeParamsId, payload.DynamicWakeParams);
            cmd.SetComputeVectorParam(compute, _AbyssalGridResolutionId, payload.AbyssalGridResolution);
            cmd.SetComputeVectorParam(compute, _AbyssalFlowCenterId, payload.AbyssalFlowCenter);
            cmd.SetComputeVectorParam(compute, _AbyssalFlowSpacingId, payload.AbyssalFlowSpacing);
            cmd.SetComputeVectorParam(compute, _AbyssalFlowTextureParamsId, payload.AbyssalFlowTextureParams);
            cmd.SetComputeFloatParam(compute, _AbyssalFlowTextureActiveId, payload.AbyssalFlowTextureActive);
            cmd.SetComputeFloatParam(compute, _AbyssalFlowInterpolationAlphaId, payload.AbyssalFlowInterpolationAlpha);
            cmd.SetComputeMatrixParam(compute, _VoxelSdfWorldToLocalId, payload.VoxelSdfWorldToLocal);
            cmd.SetComputeVectorParam(compute, _VoxelSdfInvDoubleHalfExtentsId, payload.VoxelSdfInvDoubleHalfExtents);
            cmd.SetComputeVectorParam(compute, _FluidAdvectionSdfParamsId, payload.SdfParams);
        }

        internal static void UnbindFluidAdvectionCompute(CommandBuffer cmd, in FluidAdvectionRenderGraphPayload payload)
        {
            ComputeShader compute = payload.Compute;
            int kernel = payload.Kernel;
            cmd.SetComputeBufferParam(compute, kernel, _SiltReadId, payload.EmptySiltBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _SiltWriteId, payload.EmptySiltBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleReadId, payload.EmptyBubbleBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleWriteId, payload.EmptyBubbleBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisReadId, payload.EmptyDebrisBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisWriteId, payload.EmptyDebrisBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _AbyssalFlowFieldResultId, payload.EmptyAbyssalFlowBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakesId, payload.EmptyAbyssalFlowBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakeVectorsId, payload.EmptyAbyssalFlowBuffer);
            cmd.SetComputeTextureParam(compute, kernel, _AbyssalFlowFieldTextureId, payload.EmptyVoxelSdfTexture);
            cmd.SetComputeTextureParam(compute, kernel, _VoxelSdfTexture3DId, payload.EmptyVoxelSdfTexture);
        }

        internal static void UnbindFluidAdvectionCompute(
            IComputeCommandBuffer cmd,
            in FluidAdvectionRenderGraphPayload payload,
            TextureHandle emptyTexture)
        {
            ComputeShader compute = payload.Compute;
            int kernel = payload.Kernel;
            cmd.SetComputeBufferParam(compute, kernel, _SiltReadId, payload.EmptySiltBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _SiltWriteId, payload.EmptySiltBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleReadId, payload.EmptyBubbleBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _BubbleWriteId, payload.EmptyBubbleBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisReadId, payload.EmptyDebrisBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DebrisWriteId, payload.EmptyDebrisBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _AbyssalFlowFieldResultId, payload.EmptyAbyssalFlowBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakesId, payload.EmptyAbyssalFlowBuffer);
            cmd.SetComputeBufferParam(compute, kernel, _DynamicWakeVectorsId, payload.EmptyAbyssalFlowBuffer);
            cmd.SetComputeTextureParam(compute, kernel, _AbyssalFlowFieldTextureId, emptyTexture);
            cmd.SetComputeTextureParam(compute, kernel, _VoxelSdfTexture3DId, emptyTexture);
        }

        void IFluidAdvectionRenderGraphDispatchSource.BindFluidAdvectionCompute(
            IComputeCommandBuffer cmd,
            in FluidAdvectionRenderGraphPayload payload,
            TextureHandle abyssalFlowTexture,
            TextureHandle voxelSdfTexture)
        {
            BindFluidAdvectionCompute(cmd, in payload, abyssalFlowTexture, voxelSdfTexture);
        }

        void IFluidAdvectionRenderGraphDispatchSource.UnbindFluidAdvectionCompute(
            IComputeCommandBuffer cmd,
            in FluidAdvectionRenderGraphPayload payload,
            TextureHandle emptyTexture)
        {
            UnbindFluidAdvectionCompute(cmd, in payload, emptyTexture);
        }

        private void DrainSplashdownFluidSignals(float cinematicWaterLevel)
        {
            if (_splashdownImpactConsumed)
                return;

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            if (_lastProcessedSplashdownFrame == frame)
                return;

            _lastProcessedSplashdownFrame = frame;
            System.ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PrologueCompleteSignal signal = signals[i];
                if (!IsValidPrologueSplashdownSignal(in signal))
                    continue;

                if (signal.Sequence != 0 &&
                    signal.Sequence == _lastProcessedSplashdownSequence &&
                    signal.SourceHash == _lastProcessedSplashdownSourceHash)
                {
                    continue;
                }

                if (!QueueSplashdownFluidImpulse(in signal, cinematicWaterLevel))
                    continue;

                _lastProcessedSplashdownSequence = signal.Sequence;
                _lastProcessedSplashdownSourceHash = signal.SourceHash;
                _splashdownImpactConsumed = true;
                break;
            }
        }

        private static bool IsValidPrologueSplashdownSignal(in PrologueCompleteSignal signal)
        {
            return signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
                   signal.SourceHash == PrologueSequenceSourceHash &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0 &&
                   math.isfinite(signal.WhiteoutHoldSeconds) &&
                   signal.WhiteoutHoldSeconds >= 0f;
        }

        private bool QueueSplashdownFluidImpulse(in PrologueCompleteSignal signal, float cinematicWaterLevel)
        {
            float3 runtimePosition = AUPMath.ToRuntimeFloat3(in signal.CapsuleAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            if (!math.all(math.isfinite(runtimePosition)))
            {
                _splashdownImpulseFlags |= SplashdownImpulseInvalidInputFlag;
                DumpAbyssalFlowTelemetryOnce(_splashdownImpulseFlags);
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SplashdownFluidImpulseContextHash));
                return false;
            }

            runtimePosition.y = math.min(runtimePosition.y, cinematicWaterLevel - 0.25f);
            float qualityDetail01 = SmoothFluidAdvectionQuality(ResolveFluidAdvectionQualityWeight());
            float qualityPressure01 = 1f - qualityDetail01;
            int queuedBubbles = QueueSplashdownBubbleRing(runtimePosition, qualityPressure01);
            _splashdownImpulseQualityPressureQ8 = EncodeSplashdownImpulseQualityPressureQ8(qualityPressure01);
            uint flags = 0u;

            if (qualityDetail01 <= 0.001f)
            {
                PublishSplashdownFluidImpulseTelemetry(queuedBubbles, flags);
                return true;
            }

            float3 flowCenter = lodObserver != null
                ? ResolveAbyssalFlowCenter(cinematicWaterLevel)
                : runtimePosition;
            if (!IsSplashdownInsideFlowVolume(runtimePosition, flowCenter))
            {
                flags |= SplashdownImpulseOutsideFlowVolumeFlag;
                PublishSplashdownFluidImpulseTelemetry(queuedBubbles, flags);
                return true;
            }

            if (!ScheduleSplashdownImpulseField(runtimePosition, flowCenter, qualityDetail01))
            {
                flags |= _splashdownImpulseJobActive
                    ? SplashdownImpulseJobBusyFlag
                    : SplashdownImpulseUploadFailedFlag;
                PublishSplashdownFluidImpulseTelemetry(queuedBubbles, flags);
                return true;
            }

            _splashdownImpulsePositionWS = runtimePosition;
            _splashdownImpulseRemainingSeconds = SplashdownImpulseDurationSeconds;
            _splashdownImpulseDurationSeconds = SplashdownImpulseDurationSeconds;
            _splashdownImpulseFlags = flags;
            _lastSplashdownFluidImpulseCount = 0;
            PublishSplashdownFluidImpulseTelemetry(queuedBubbles, _splashdownImpulseFlags);
            return true;
        }

        private static bool IsSplashdownInsideFlowVolume(float3 runtimePosition, float3 flowCenter)
        {
            if (!math.all(math.isfinite(runtimePosition)) || !math.all(math.isfinite(flowCenter)))
                return false;

            float activeExtent = AbyssalFlowTextureWorldSizeMeters * 0.5f + SplashdownImpulseRadiusMeters;
            float3 delta = math.abs(runtimePosition - flowCenter);
            return delta.x <= activeExtent &&
                   delta.y <= activeExtent &&
                   delta.z <= activeExtent;
        }

        private int QueueSplashdownBubbleRing(float3 runtimePosition, float qualityPressure01)
        {
            RefreshFluidAdvectionStateReadyCached();
            if (!IsFluidAdvectionStorageReady() || !_advectedBubbleUpload.IsCreated)
                return 0;

            ClearPendingFluidAdvectionShiftIfNoActiveParticles();
            int safeCount = math.min(SplashdownBubbleCount, MaxAdvectedBubbleCount);
            float detail01 = 1f - math.saturate(qualityPressure01);
            float spawnRadius = SplashdownBubbleSpawnRadiusMeters * math.lerp(0.85f, 1.15f, detail01);

            for (int i = 0; i < safeCount; i++)
            {
                int slot = _advectedBubbleWriteCursor;
                _advectedBubbleWriteCursor = (_advectedBubbleWriteCursor + 1) % MaxAdvectedBubbleCount;
                _activeAdvectedBubbleCount = math.min(MaxAdvectedBubbleCount, _activeAdvectedBubbleCount + 1);

                float phase = (slot + i) * SplashdownGoldenAngleRadians;
                MathLodApproximation.ApproxSinCosBhaskara(phase, out float sin, out float cos);
                float ringBand = 1f + ((slot & 7) * 0.045f);
                float3 offset = new float3(sin * spawnRadius * ringBand, HashToSignedUnit((uint)slot) * 0.18f, cos * spawnRadius * ringBand);
                float3 velocity = ResolveSplashdownBubbleVelocity(offset, qualityPressure01);
                AdvectedBubble bubble = new AdvectedBubble
                {
                    PositionWS = runtimePosition + offset,
                    Life = 1f,
                    VelocityWS = velocity,
                    Flags = AdvectedBubbleActiveFlag
                };
                UploadAdvectedBubble(slot, in bubble);
            }

            return safeCount;
        }

        private static float3 ResolveSplashdownBubbleVelocity(float3 offset, float qualityPressure01)
        {
            float3 lifted = offset;
            lifted.y += SplashdownBubbleUpwardBiasMeters;
            float lengthSq = math.lengthsq(lifted);
            float3 direction = lengthSq > 0.0001f
                ? lifted * math.rsqrt(math.max(lengthSq, 0.0001f))
                : new float3(0f, 1f, 0f);
            float gain = SplashdownImpulseStrength * math.rcp(math.max(math.lengthsq(offset), 1f));
            float3 velocity = direction * gain;
            float detail01 = 1f - math.saturate(qualityPressure01);
            float maxVelocity = math.lerp(
                SplashdownBubbleMinimumQualityMaxVelocityMetersPerSecond,
                SplashdownImpulseMaxVelocityMetersPerSecond,
                detail01);
            float velocitySq = math.lengthsq(velocity);
            float maxVelocitySq = maxVelocity * maxVelocity;
            if (velocitySq > maxVelocitySq)
                velocity *= maxVelocity * math.rsqrt(math.max(velocitySq, 0.0001f));

            return HectonAnalyticalFlowField.ResolveFiniteFloat3OrZero(velocity);
        }

        private bool ScheduleSplashdownImpulseField(float3 runtimePosition, float3 flowCenter, float qualityDetail01)
        {
            if (_splashdownImpulseJobActive)
                return false;

            if (!HasSplashdownImpulseState())
            {
                return false;
            }

            _splashdownImpulseStats[0] = 0;
            _splashdownImpulseStats[1] = 0;
            FluidImpulseJob job = new FluidImpulseJob
            {
                ImpulseField = _splashdownImpulseUpload,
                ImpulseStats = _splashdownImpulseStats,
                FieldCenterWS = flowCenter,
                ImpactPositionWS = runtimePosition,
                WorldSizeMeters = AbyssalFlowTextureWorldSizeMeters,
                RadiusMeters = math.lerp(SplashdownImpulseRadiusMeters * 0.6f, SplashdownImpulseRadiusMeters, math.saturate(qualityDetail01)),
                ImpulseStrength = SplashdownImpulseStrength * math.lerp(0.35f, 1f, math.saturate(qualityDetail01)),
                UpwardBiasMeters = SplashdownImpulseUpwardBiasMeters,
                MaxVelocityMetersPerSecond = SplashdownImpulseMaxVelocityMetersPerSecond,
                Resolution = AbyssalFlowTextureResolution
            };

            _splashdownImpulseJobHandle = job.Schedule();
            _splashdownImpulseJobActive = true;
            _splashdownImpulseUploaded = false;
            _splashdownImpulseScheduleFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            return true;
        }

        private void TryCompleteSplashdownImpulseJobForUpload()
        {
            if (!_splashdownImpulseJobActive)
                return;

            if (!_splashdownImpulseJobHandle.IsCompleted || _splashdownImpulseScheduleFrame == Hecton8.Core.SystemDispatcher.CurrentFrameIndex)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _splashdownImpulseJobHandle))
                return;

            _splashdownImpulseJobActive = false;
            _splashdownImpulseScheduleFrame = -1;

            int affectedCount = _splashdownImpulseStats.IsCreated && _splashdownImpulseStats.Length > 0
                ? math.max(0, _splashdownImpulseStats[0])
                : 0;
            uint flags = _splashdownImpulseStats.IsCreated &&
                         _splashdownImpulseStats.Length > 1 &&
                         _splashdownImpulseStats[1] != 0
                ? SplashdownImpulseJobInvalidFlag
                : 0u;

            if (affectedCount <= 0)
            {
                flags |= SplashdownImpulseNoAffectedCellsFlag;
                _lastSplashdownFluidImpulseCount = 0;
                _splashdownImpulseRemainingSeconds = 0f;
                _splashdownImpulseDurationSeconds = 0f;
                _splashdownImpulseUploaded = false;
                _splashdownImpulseFlags |= flags;
                PublishSplashdownFluidImpulseTelemetry(0, _splashdownImpulseFlags);
                if ((flags & SplashdownImpulseJobInvalidFlag) != 0u)
                    DumpAbyssalFlowTelemetryOnce(_splashdownImpulseFlags);
                return;
            }

            bool uploaded = UploadSplashdownImpulseBuffer();
            if (!uploaded)
            {
                flags |= SplashdownImpulseUploadFailedFlag;
                _lastSplashdownFluidImpulseCount = 0;
                _splashdownImpulseRemainingSeconds = 0f;
                _splashdownImpulseDurationSeconds = 0f;
                _splashdownImpulseFlags |= flags;
                PublishSplashdownFluidImpulseTelemetry(0, _splashdownImpulseFlags);
                DumpAbyssalFlowTelemetryOnce(_splashdownImpulseFlags);
                return;
            }

            _lastSplashdownFluidImpulseCount = affectedCount;
            _splashdownImpulseFlags |= flags;
            PublishSplashdownFluidImpulseTelemetry(affectedCount, _splashdownImpulseFlags);
            if ((flags & SplashdownImpulseJobInvalidFlag) != 0u)
                DumpAbyssalFlowTelemetryOnce(_splashdownImpulseFlags);
        }

        private bool UploadSplashdownImpulseBuffer()
        {
            if (!HasSplashdownImpulseGpuBuffer())
            {
                _splashdownImpulseUploaded = false;
                return false;
            }

            if (_gpuSplashdownImpulseBufferA == null ||
                !_gpuSplashdownImpulseBufferA.IsValid() ||
                _gpuSplashdownImpulseBufferB == null ||
                !_gpuSplashdownImpulseBufferB.IsValid() ||
                !_splashdownImpulseUpload.IsCreated)
            {
                _splashdownImpulseUploaded = false;
                return false;
            }

            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0 ||
                _gpuSplashdownImpulseBufferA.count < nodeCount ||
                _gpuSplashdownImpulseBufferB.count < nodeCount ||
                _splashdownImpulseUpload.Length < nodeCount)
            {
                _splashdownImpulseUploaded = false;
                return false;
            }

            GraphicsBuffer writeBuffer = (_gpuSplashdownImpulseUploadIndex & 1) == 0
                ? _gpuSplashdownImpulseBufferA
                : _gpuSplashdownImpulseBufferB;
            if (writeBuffer == null || !writeBuffer.IsValid() || writeBuffer.count < nodeCount)
            {
                _splashdownImpulseUploaded = false;
                return false;
            }

            GraphicsBufferUploadUtility.UploadNativeArray<float4>(writeBuffer, _splashdownImpulseUpload, nodeCount);
            _activeGpuSplashdownImpulseBuffer = writeBuffer;
            _gpuSplashdownImpulseUploadIndex ^= 1;
            _splashdownImpulseUploaded = true;
            return true;
        }

        private void UpdateSplashdownImpulseState(float fixedDeltaTime)
        {
            if (_splashdownImpulseRemainingSeconds <= 0f)
                return;

            _splashdownImpulseRemainingSeconds = math.max(0f, _splashdownImpulseRemainingSeconds - math.max(0f, fixedDeltaTime));
            if (_splashdownImpulseRemainingSeconds <= 0f)
            {
                _lastSplashdownFluidImpulseCount = 0;
                _splashdownImpulseFlags = 0u;
                _splashdownImpulseUploaded = false;
            }
        }

        private Vector4 ResolveSplashdownImpulseParams()
        {
            if (!_splashdownImpulseUploaded ||
                _splashdownImpulseRemainingSeconds <= 0f ||
                _lastSplashdownFluidImpulseCount <= 0)
            {
                return Vector4.zero;
            }

            float duration = math.max(0.001f, _splashdownImpulseDurationSeconds);
            float strengthScale = math.saturate(_splashdownImpulseRemainingSeconds * math.rcp(duration));
            return new Vector4(1f, strengthScale, _lastSplashdownFluidImpulseCount, 0f);
        }

        private GraphicsBuffer ResolveSplashdownImpulseBuffer()
        {
            return _activeGpuSplashdownImpulseBuffer != null && _activeGpuSplashdownImpulseBuffer.IsValid()
                ? _activeGpuSplashdownImpulseBuffer
                : _emptyAbyssalFlowBuffer;
        }

        private static byte EncodeSplashdownImpulseQualityPressureQ8(float qualityPressure01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityPressure01) * 255f), 0, 255);
        }

        private void PublishSplashdownFluidImpulseTelemetry(int count, uint flags)
        {
            uint qualityPressureContext = (uint)_splashdownImpulseQualityPressureQ8 << 16;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SplashdownFluidImpulseCountHash,
                SplashdownFluidImpulseContextHash ^ flags ^ qualityPressureContext,
                math.max(0, count));
        }

        private void DrainFluidAdvectionSignals()
        {
            ConsumeFluidAdvectionAupShiftSignals();
            ClearPendingFluidAdvectionShiftIfNoActiveParticles();

            int drained = 0;
            while (drained < FluidAdvectionSignalDrainBudget &&
                   SignalBus<DebrisSpawnSignal>.TryConsumeFrame(out DebrisSpawnSignal signal))
            {
                QueueAdvectedDebrisFromSignal(in signal);
                drained++;
            }
        }

        private void ConsumeFluidAdvectionAupShiftSignals()
        {
            System.ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                AupShiftSignal signal = shifts[i];
                if (signal.ShiftFrameId == 0u || signal.ShiftFrameId == _lastProcessedFluidAdvectionAupShiftFrameId)
                    continue;

                _lastProcessedFluidAdvectionAupShiftFrameId = signal.ShiftFrameId;
                if (!math.all(math.isfinite(signal.ShiftMeters)))
                {
                    DumpFluidAdvectionTelemetryOnce(1u);
                    continue;
                }

                _pendingFluidAdvectionRuntimeShift += -signal.ShiftMeters;
            }
        }

        private static float ResolveFluidAdvectionQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 0f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0f;
        }

        private static float SmoothFluidAdvectionQuality(float qualityWeight)
        {
            float quality = math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 0f;
            return quality * quality * (3f - 2f * quality);
        }

        private static Vector4 ResolveGlobalWakeParamsForFluidAdvection(
            NativeArray<float4> dynamicWakes,
            int uploadCount,
            float qualityWeight)
        {
            if (!dynamicWakes.IsCreated || uploadCount <= 0)
                return Vector4.zero;

            float quality = SmoothFluidAdvectionQuality(qualityWeight);
            float maxSlotLimit = math.lerp(DynamicWakeMinimumQualityGpuCapacity, DynamicWakeGpuCapacity, quality);
            float slotLimit = math.clamp(uploadCount, 0f, maxSlotLimit);
            int safeLimit = math.min(dynamicWakes.Length, (int)math.floor(slotLimit));
            int activeCount = 0;
            float maxIntensity = 0f;
            for (int i = 0; i < safeLimit; i++)
            {
                float4 wake = dynamicWakes[i];
                float intensity = math.isfinite(wake.w) ? math.max(0f, wake.w) : 0f;
                if (intensity <= 0.0001f || !math.all(math.isfinite(wake.xyz)))
                    continue;

                activeCount++;
                maxIntensity = math.max(maxIntensity, intensity);
            }

            return new Vector4(
                slotLimit,
                1f - quality,
                activeCount,
                math.saturate(maxIntensity));
        }

        public bool TryGetDynamicWakeGpuPayload(
            out GraphicsBuffer dynamicWakeBuffer,
            out GraphicsBuffer dynamicWakeVectorBuffer,
            out Vector4 dynamicWakeParams)
        {
            dynamicWakeBuffer = _emptyAbyssalFlowBuffer;
            dynamicWakeVectorBuffer = _emptyAbyssalFlowBuffer;
            dynamicWakeParams = _activeDynamicWakeParams;
            if (dynamicWakeParams.z <= 0.5f)
            {
                dynamicWakeParams = Vector4.zero;
                return false;
            }

            GraphicsBuffer activeWakeBuffer = _activeDynamicWakeBuffer;
            GraphicsBuffer activeWakeVectorBuffer = _activeDynamicWakeVectorBuffer;
            if (activeWakeBuffer == null ||
                activeWakeVectorBuffer == null ||
                !activeWakeBuffer.IsValid() ||
                !activeWakeVectorBuffer.IsValid())
            {
                dynamicWakeParams = Vector4.zero;
                return false;
            }

            dynamicWakeBuffer = activeWakeBuffer;
            dynamicWakeVectorBuffer = activeWakeVectorBuffer;
            return true;
        }

        private void RefreshDynamicWakeGpuPayload(float qualityWeight)
        {
            ClearDynamicWakeGpuPayload();
            if (_emptyAbyssalFlowBuffer == null || !_emptyAbyssalFlowBuffer.IsValid())
                return;

            if (!AreDynamicWakeGpuBuffersReady() ||
                !TryResolveCachedDynamicWakeVaultBuffers(
                    out NativeArray<float4> dynamicWakes,
                    out NativeArray<float4> dynamicWakeVectors))
            {
                return;
            }

            int uploadCount = math.min(DynamicWakeGpuCapacity, math.min(dynamicWakes.Length, dynamicWakeVectors.Length));
            if (uploadCount <= 0)
                return;

            float quality = SmoothFluidAdvectionQuality(qualityWeight);
            Vector4 dynamicWakeParams = ResolveGlobalWakeParamsForFluidAdvection(dynamicWakes, uploadCount, qualityWeight);
            if (dynamicWakeParams.z <= 0.5f)
                return;

            GraphicsBuffer wakeWriteBuffer = _dynamicWakeUploadBufferIndex == 0 ? _dynamicWakeBufferA : _dynamicWakeBufferB;
            GraphicsBuffer wakeVectorWriteBuffer = _dynamicWakeUploadBufferIndex == 0 ? _dynamicWakeVectorBufferA : _dynamicWakeVectorBufferB;
            if (wakeWriteBuffer == null ||
                wakeVectorWriteBuffer == null ||
                !wakeWriteBuffer.IsValid() ||
                !wakeVectorWriteBuffer.IsValid())
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(wakeWriteBuffer, dynamicWakes, uploadCount);
            GraphicsBufferUploadUtility.UploadNativeArray(wakeVectorWriteBuffer, dynamicWakeVectors, uploadCount);
            _activeDynamicWakeBuffer = wakeWriteBuffer;
            _activeDynamicWakeVectorBuffer = wakeVectorWriteBuffer;
            _dynamicWakeUploadBufferIndex ^= 1;

            float slotLimit = math.clamp(
                dynamicWakeParams.x,
                0f,
                math.min(uploadCount, math.lerp(DynamicWakeMinimumQualityGpuCapacity, DynamicWakeGpuCapacity, quality)));
            float activeCount = math.clamp(dynamicWakeParams.z, 0f, slotLimit);
            _activeDynamicWakeParams = new Vector4(slotLimit, 1f - quality, activeCount, math.saturate(dynamicWakeParams.w));
        }

        private void ClearDynamicWakeGpuPayload()
        {
            _activeDynamicWakeParams = Vector4.zero;
            if (_emptyAbyssalFlowBuffer != null && _emptyAbyssalFlowBuffer.IsValid())
            {
                _activeDynamicWakeBuffer = _emptyAbyssalFlowBuffer;
                _activeDynamicWakeVectorBuffer = _emptyAbyssalFlowBuffer;
                return;
            }

            _activeDynamicWakeBuffer = null;
            _activeDynamicWakeVectorBuffer = null;
        }

        private bool EnsureDynamicWakeGpuBuffers()
        {
            if (_emptyAbyssalFlowBuffer == null || !_emptyAbyssalFlowBuffer.IsValid())
                return false;

            if (_dynamicWakeBufferA == null || !_dynamicWakeBufferA.IsValid())
                _dynamicWakeBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(DynamicWakeGpuCapacity); // COLD ALLOC: GraphicsBuffer[16] A - DataVault wake positions for dynamic VFX advection - owner: HectonFluidEngine
            if (_dynamicWakeBufferB == null || !_dynamicWakeBufferB.IsValid())
                _dynamicWakeBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(DynamicWakeGpuCapacity); // COLD ALLOC: GraphicsBuffer[16] B - DataVault wake positions for dynamic VFX advection - owner: HectonFluidEngine
            if (_dynamicWakeVectorBufferA == null || !_dynamicWakeVectorBufferA.IsValid())
                _dynamicWakeVectorBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(DynamicWakeGpuCapacity); // COLD ALLOC: GraphicsBuffer[16] A - DataVault wake vectors for dynamic VFX advection - owner: HectonFluidEngine
            if (_dynamicWakeVectorBufferB == null || !_dynamicWakeVectorBufferB.IsValid())
                _dynamicWakeVectorBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(DynamicWakeGpuCapacity); // COLD ALLOC: GraphicsBuffer[16] B - DataVault wake vectors for dynamic VFX advection - owner: HectonFluidEngine

            if (_activeDynamicWakeBuffer == null)
                _activeDynamicWakeBuffer = _dynamicWakeBufferA;
            if (_activeDynamicWakeVectorBuffer == null)
                _activeDynamicWakeVectorBuffer = _dynamicWakeVectorBufferA;

            return _dynamicWakeBufferA != null &&
                   _dynamicWakeBufferB != null &&
                   _dynamicWakeVectorBufferA != null &&
                   _dynamicWakeVectorBufferB != null &&
                   _dynamicWakeBufferA.IsValid() &&
                   _dynamicWakeBufferB.IsValid() &&
                   _dynamicWakeVectorBufferA.IsValid() &&
                   _dynamicWakeVectorBufferB.IsValid();
        }

        private bool AreDynamicWakeGpuBuffersReady()
        {
            return _dynamicWakeBufferA != null &&
                   _dynamicWakeBufferA.IsValid() &&
                   _dynamicWakeBufferB != null &&
                   _dynamicWakeBufferB.IsValid() &&
                   _dynamicWakeVectorBufferA != null &&
                   _dynamicWakeVectorBufferA.IsValid() &&
                   _dynamicWakeVectorBufferB != null &&
                   _dynamicWakeVectorBufferB.IsValid();
        }

        private bool TryOpenOrAcquireDynamicWakeVaultBuffers(
            out NativeArray<float4> dynamicWakes,
            out NativeArray<float4> dynamicWakeVectors)
        {
            dynamicWakes = default;
            dynamicWakeVectors = default;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!IsVaultGenerationHandleCreated(in _dynamicWakeBufferHandle))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle(BufferID.WakeGlobalBuffer, out _dynamicWakeBufferHandle))
                        return false;
                }
                else
                {
                    _dynamicWakeBufferHandle = vault.EnsureGenerationHandle<float4>(
                        BufferID.WakeGlobalBuffer,
                        DynamicWakeGpuCapacity,
                        SystemID.Fluid,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!IsVaultGenerationHandleCreated(in _dynamicWakeVectorBufferHandle))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle(BufferID.WakeVectorBuffer, out _dynamicWakeVectorBufferHandle))
                        return false;
                }
                else
                {
                    _dynamicWakeVectorBufferHandle = vault.EnsureGenerationHandle<float4>(
                        BufferID.WakeVectorBuffer,
                        DynamicWakeGpuCapacity,
                        SystemID.Fluid,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!IsVaultGenerationHandleCreated(in _dynamicWakeBufferHandle) ||
                !IsVaultGenerationHandleCreated(in _dynamicWakeVectorBufferHandle))
                return false;

            if (!vault.TryResolveHandle(in _dynamicWakeBufferHandle, out dynamicWakes) ||
                !vault.TryResolveHandle(in _dynamicWakeVectorBufferHandle, out dynamicWakeVectors))
            {
                dynamicWakes = default;
                dynamicWakeVectors = default;
                return false;
            }

            return dynamicWakes.IsCreated && dynamicWakeVectors.IsCreated;
        }

        private bool TryResolveCachedDynamicWakeVaultBuffers(
            out NativeArray<float4> dynamicWakes,
            out NativeArray<float4> dynamicWakeVectors)
        {
            dynamicWakes = default;
            dynamicWakeVectors = default;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultGenerationHandleCreated(in _dynamicWakeBufferHandle) ||
                !IsVaultGenerationHandleCreated(in _dynamicWakeVectorBufferHandle))
            {
                return false;
            }

            if (!vault.TryResolveHandle(in _dynamicWakeBufferHandle, out dynamicWakes) ||
                !vault.TryResolveHandle(in _dynamicWakeVectorBufferHandle, out dynamicWakeVectors))
            {
                dynamicWakes = default;
                dynamicWakeVectors = default;
                return false;
            }

            return dynamicWakes.IsCreated && dynamicWakeVectors.IsCreated;
        }

        private bool HasActiveAdvectedParticles()
        {
            return _activeAdvectedSiltCount > 0 ||
                   _activeAdvectedBubbleCount > 0 ||
                   _activeAdvectedDebrisCount > 0;
        }

        private void ClearPendingFluidAdvectionShiftIfNoActiveParticles()
        {
            if (!HasActiveAdvectedParticles())
                _pendingFluidAdvectionRuntimeShift = default;
        }

        private void QueueAdvectedDebrisFromSignal(in DebrisSpawnSignal signal)
        {
            RefreshFluidAdvectionStateReadyCached();
            if (!IsFluidAdvectionStorageReady())
                return;

            int requestedQuantity = signal.Quantity;
            int quantity = math.clamp(requestedQuantity <= 0 ? 1 : requestedQuantity, 1, MaxAdvectedDebrisCount);
            float intensity = math.saturate(math.isfinite(signal.Intensity01) ? signal.Intensity01 : 0.25f);
            float3 runtimePosition = AUPMath.ToRuntimeFloat3(in signal.PositionAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            if (!math.all(math.isfinite(runtimePosition)))
            {
                DumpFluidAdvectionTelemetryOnce(2u);
                return;
            }

            for (int i = 0; i < quantity; i++)
            {
                int slot = _advectedDebrisWriteCursor;
                _advectedDebrisWriteCursor = (_advectedDebrisWriteCursor + 1) % MaxAdvectedDebrisCount;
                _activeAdvectedDebrisCount = math.min(MaxAdvectedDebrisCount, _activeAdvectedDebrisCount + 1);

                float3 offset = ResolveSpawnJitter(slot + (int)signal.SpeciesHash, DebrisSpawnRadiusMeters * (0.5f + intensity));
                AdvectedDebris debris = new AdvectedDebris
                {
                    PositionWS = runtimePosition + offset,
                    Life = 1f,
                    VelocityWS = new float3(offset.x * 0.2f, DebrisAdvectionBuoyancyMetersPerSecond, offset.z * 0.2f),
                    Flags = AdvectedDebrisActiveFlag | ((uint)signal.DebrisKind << 8)
                };
                UploadAdvectedDebris(slot, in debris);
            }
        }

        private void EnsureFluidAdvectionState(bool allowAllocate = true)
        {
            if (_fluidAdvectionStateReady && IsFluidAdvectionStorageReady())
            {
                return;
            }

            if (!allowAllocate)
            {
                _fluidAdvectionStateReady = IsFluidAdvectionStorageReady();
                return;
            }

            if (!_advectedSiltUpload.IsCreated)
            {
                _advectedSiltUpload.Ensure(
                    FluidAdvectedSiltUploadBufferId,
                    MaxAdvectedSiltCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_advectedBubbleUpload.IsCreated)
            {
                _advectedBubbleUpload.Ensure(
                    FluidAdvectedBubbleUploadBufferId,
                    MaxAdvectedBubbleCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_advectedDebrisUpload.IsCreated)
            {
                _advectedDebrisUpload.Ensure(
                    FluidAdvectedDebrisUploadBufferId,
                    MaxAdvectedDebrisCount,
                    NativeArrayOptions.ClearMemory);
            }

            int siltDirtyPageCount = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                MaxAdvectedSiltCount,
                FluidAdvectionDirtyPageSize);
            if (!_advectedSiltDirtyPages.IsCreated)
            {
                _advectedSiltDirtyPages.Ensure(
                    FluidAdvectedSiltDirtyPagesBufferId,
                    siltDirtyPageCount,
                    NativeArrayOptions.ClearMemory);
            }

            int bubbleDirtyPageCount = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                MaxAdvectedBubbleCount,
                FluidAdvectionDirtyPageSize);
            if (!_advectedBubbleDirtyPages.IsCreated)
            {
                _advectedBubbleDirtyPages.Ensure(
                    FluidAdvectedBubbleDirtyPagesBufferId,
                    bubbleDirtyPageCount,
                    NativeArrayOptions.ClearMemory);
            }

            int debrisDirtyPageCount = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                MaxAdvectedDebrisCount,
                FluidAdvectionDirtyPageSize);
            if (!_advectedDebrisDirtyPages.IsCreated)
            {
                _advectedDebrisDirtyPages.Ensure(
                    FluidAdvectedDebrisDirtyPagesBufferId,
                    debrisDirtyPageCount,
                    NativeArrayOptions.ClearMemory);
            }

            EnsureFluidAdvectionDirtyPageUploadSnapshot(ref _advectedSiltDirtyPageUploadSnapshot, siltDirtyPageCount);
            EnsureFluidAdvectionDirtyPageUploadSnapshot(ref _advectedBubbleDirtyPageUploadSnapshot, bubbleDirtyPageCount);
            EnsureFluidAdvectionDirtyPageUploadSnapshot(ref _advectedDebrisDirtyPageUploadSnapshot, debrisDirtyPageCount);

            if (!_emptyAbyssalFlowUpload.IsCreated)
            {
                _emptyAbyssalFlowUpload.Ensure(
                    FluidEmptyAbyssalFlowUploadBufferId,
                    1,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_fluidAdvectionTelemetry.IsCreated)
            {
                _fluidAdvectionTelemetry.Ensure(
                    FluidAdvectionTelemetryBufferId,
                    FluidAdvectionTelemetryCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            _fluidAdvectionStateReady = IsFluidAdvectionStorageReady();
        }

        private void EnsureFluidAdvectionVisualState(bool allowAllocate = true)
        {
            EnsureFluidAdvectionState(allowAllocate);
            EnsureFluidAdvectionBuffers(allowAllocate);
            EnsureEmptyFluidAdvectionTexture(allowAllocate);
            _fluidAdvectionStateReady = IsFluidAdvectionReady();
        }

        private void RefreshFluidAdvectionStateReadyCached()
        {
            _fluidAdvectionStateReady = IsFluidAdvectionStorageReady();
        }

        private void RefreshFluidAdvectionVisualStateReadyCached()
        {
            RefreshFluidAdvectionStateReadyCached();
            _fluidAdvectionStateReady = IsFluidAdvectionReady();
        }

        private bool HasFluidAdvectionNativeState()
        {
            return _advectedSiltUpload.IsCreated &&
                   _advectedBubbleUpload.IsCreated &&
                   _advectedDebrisUpload.IsCreated &&
                   _advectedSiltDirtyPages.IsCreated &&
                   _advectedBubbleDirtyPages.IsCreated &&
                   _advectedDebrisDirtyPages.IsCreated &&
                   _emptyAbyssalFlowUpload.IsCreated &&
                   _fluidAdvectionTelemetry.IsCreated;
        }

        private bool EnsureFluidAdvectionBuffers(bool allowAllocate = true)
        {
            bool buffersReady = IsFluidAdvectionGpuBufferStateReady();
            if (buffersReady || !allowAllocate)
                return buffersReady;

            bool siltCreated = false;
            bool bubbleCreated = false;
            bool debrisCreated = false;
            bool emptyAbyssalCreated = false;
            if (_advectedSiltBufferA == null || !_advectedSiltBufferA.IsValid())
            {
                _advectedSiltBufferA = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<AdvectedSilt>(MaxAdvectedSiltCount); // COLD ALLOC: GraphicsBuffer[4096] - GPU-write silt advection front buffer, dirty CPU pages use SetData fallback because UAV forbids LockBufferForWrite - owner: HectonFluidEngine
                siltCreated = true;
            }
            if (_advectedSiltBufferB == null || !_advectedSiltBufferB.IsValid())
            {
                _advectedSiltBufferB = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<AdvectedSilt>(MaxAdvectedSiltCount); // COLD ALLOC: GraphicsBuffer[4096] - GPU-write silt advection back buffer, dirty CPU pages use SetData fallback because UAV forbids LockBufferForWrite - owner: HectonFluidEngine
                siltCreated = true;
            }
            if (_advectedBubbleBufferA == null || !_advectedBubbleBufferA.IsValid())
            {
                _advectedBubbleBufferA = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<AdvectedBubble>(MaxAdvectedBubbleCount); // COLD ALLOC: GraphicsBuffer[2000] - GPU-write bubble advection front buffer, dirty CPU pages use SetData fallback because UAV forbids LockBufferForWrite - owner: HectonFluidEngine
                bubbleCreated = true;
            }
            if (_advectedBubbleBufferB == null || !_advectedBubbleBufferB.IsValid())
            {
                _advectedBubbleBufferB = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<AdvectedBubble>(MaxAdvectedBubbleCount); // COLD ALLOC: GraphicsBuffer[2000] - GPU-write bubble advection back buffer, dirty CPU pages use SetData fallback because UAV forbids LockBufferForWrite - owner: HectonFluidEngine
                bubbleCreated = true;
            }
            if (_advectedDebrisBufferA == null || !_advectedDebrisBufferA.IsValid())
            {
                _advectedDebrisBufferA = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<AdvectedDebris>(MaxAdvectedDebrisCount); // COLD ALLOC: GraphicsBuffer[1000] - GPU-write debris advection front buffer, dirty CPU pages use SetData fallback because UAV forbids LockBufferForWrite - owner: HectonFluidEngine
                debrisCreated = true;
            }
            if (_advectedDebrisBufferB == null || !_advectedDebrisBufferB.IsValid())
            {
                _advectedDebrisBufferB = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<AdvectedDebris>(MaxAdvectedDebrisCount); // COLD ALLOC: GraphicsBuffer[1000] - GPU-write debris advection back buffer, dirty CPU pages use SetData fallback because UAV forbids LockBufferForWrite - owner: HectonFluidEngine
                debrisCreated = true;
            }
            if (_emptyAdvectedSiltBuffer == null || !_emptyAdvectedSiltBuffer.IsValid())
                _emptyAdvectedSiltBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<AdvectedSilt>(1); // COLD ALLOC: GraphicsBuffer[1] - GPU-write silt unbind fallback - owner: HectonFluidEngine
            if (_emptyAdvectedBubbleBuffer == null || !_emptyAdvectedBubbleBuffer.IsValid())
                _emptyAdvectedBubbleBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<AdvectedBubble>(1); // COLD ALLOC: GraphicsBuffer[1] - GPU-write bubble unbind fallback - owner: HectonFluidEngine
            if (_emptyAdvectedDebrisBuffer == null || !_emptyAdvectedDebrisBuffer.IsValid())
                _emptyAdvectedDebrisBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<AdvectedDebris>(1); // COLD ALLOC: GraphicsBuffer[1] - GPU-write debris unbind fallback - owner: HectonFluidEngine
            if (_emptyAbyssalFlowBuffer == null || !_emptyAbyssalFlowBuffer.IsValid())
            {
                _emptyAbyssalFlowBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(1); // COLD ALLOC: GraphicsBuffer[1] - GPU-write zero abyssal-flow fallback with mapped CPU upload - owner: HectonFluidEngine
                emptyAbyssalCreated = true;
            }

            if (siltCreated)
                MarkAllFluidAdvectionDirtyPages(ref _advectedSiltDirtyPages, MaxAdvectedSiltCount);
            if (bubbleCreated)
                MarkAllFluidAdvectionDirtyPages(ref _advectedBubbleDirtyPages, MaxAdvectedBubbleCount);
            if (debrisCreated)
                MarkAllFluidAdvectionDirtyPages(ref _advectedDebrisDirtyPages, MaxAdvectedDebrisCount);
            _advectedSiltGpuUploadDirty |= siltCreated;
            _advectedBubbleGpuUploadDirty |= bubbleCreated;
            _advectedDebrisGpuUploadDirty |= debrisCreated;
            if (emptyAbyssalCreated)
                GraphicsBufferUploadUtility.UploadNativeArray<float4>(_emptyAbyssalFlowBuffer, _emptyAbyssalFlowUpload, 1);

            return IsFluidAdvectionGpuBufferStateReady();
        }

        private bool EnsureEmptyFluidAdvectionTexture(bool allowAllocate = true)
        {
            if (_emptyFluidAdvectionTexture != null)
            {
                if (_emptyFluidAdvectionTextureHandle == null)
                {
                    if (!allowAllocate)
                        return false;

                    _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture);
                }

                return _emptyFluidAdvectionTextureHandle != null;
            }

            if (!allowAllocate)
                return false;

            if (authoredEmptyFluidAdvectionTexture == null)
                return false;

            _emptyFluidAdvectionTexture = authoredEmptyFluidAdvectionTexture;
            _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture);
            return _emptyFluidAdvectionTextureHandle != null;
        }

        private bool IsFluidAdvectionGpuBufferStateReady()
        {
            return _advectedSiltBufferA != null &&
                   _advectedSiltBufferB != null &&
                   _advectedBubbleBufferA != null &&
                   _advectedBubbleBufferB != null &&
                   _advectedDebrisBufferA != null &&
                   _advectedDebrisBufferB != null &&
                   _emptyAdvectedSiltBuffer != null &&
                   _emptyAdvectedBubbleBuffer != null &&
                   _emptyAdvectedDebrisBuffer != null &&
                   _emptyAbyssalFlowBuffer != null &&
                   _advectedSiltBufferA.IsValid() &&
                   _advectedSiltBufferB.IsValid() &&
                   _advectedBubbleBufferA.IsValid() &&
                   _advectedBubbleBufferB.IsValid() &&
                   _advectedDebrisBufferA.IsValid() &&
                   _advectedDebrisBufferB.IsValid() &&
                   _emptyAdvectedSiltBuffer.IsValid() &&
                   _emptyAdvectedBubbleBuffer.IsValid() &&
                   _emptyAdvectedDebrisBuffer.IsValid() &&
                   _emptyAbyssalFlowBuffer.IsValid();
        }

        private RTHandle ResolveFluidAdvectionFlowTextureHandle(Texture texture, bool allowAllocate = true)
        {
            if (texture == null || ReferenceEquals(texture, _emptyFluidAdvectionTexture))
                return _emptyFluidAdvectionTextureHandle;

            if (ReferenceEquals(texture, _gpuAbyssalFlowTextureA))
            {
                if (_gpuAbyssalFlowTextureAHandle == null)
                {
                    if (!allowAllocate)
                        return null;

                    _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA);
                }

                return _gpuAbyssalFlowTextureAHandle;
            }

            if (ReferenceEquals(texture, _gpuAbyssalFlowTextureB))
            {
                if (_gpuAbyssalFlowTextureBHandle == null)
                {
                    if (!allowAllocate)
                        return null;

                    _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB);
                }

                return _gpuAbyssalFlowTextureBHandle;
            }

            if (!ReferenceEquals(texture, _cachedFluidAdvectionFlowHandleSource))
            {
                if (!allowAllocate)
                    return null;

                ReleaseRTHandle(ref _cachedFluidAdvectionFlowHandle);
                _cachedFluidAdvectionFlowHandleSource = texture;
                _cachedFluidAdvectionFlowHandle = RTHandles.Alloc(texture);
            }

            return _cachedFluidAdvectionFlowHandle;
        }

        private RTHandle ResolveFluidAdvectionSdfTextureHandle(Texture texture, bool allowAllocate = true)
        {
            if (texture == null || ReferenceEquals(texture, _emptyFluidAdvectionTexture))
                return _emptyFluidAdvectionTextureHandle;

            if (!ReferenceEquals(texture, _cachedFluidAdvectionSdfHandleSource))
            {
                if (!allowAllocate)
                    return null;

                ReleaseRTHandle(ref _cachedFluidAdvectionSdfHandle);
                _cachedFluidAdvectionSdfHandleSource = texture;
                _cachedFluidAdvectionSdfHandle = RTHandles.Alloc(texture);
            }

            return _cachedFluidAdvectionSdfHandle;
        }

        private bool IsFluidAdvectionReady()
        {
            return IsFluidAdvectionStorageReady() &&
                   IsFluidAdvectionGpuBufferStateReady() &&
                   _emptyFluidAdvectionTexture != null &&
                   _emptyFluidAdvectionTextureHandle != null;
        }

        private void RunCpuFluidAdvectionFallback(float deltaTime)
        {
            if (!_prebakedVectorNoiseField.TryResolve(out NativeArray<float3> noiseField) || !noiseField.IsCreated)
                return;

            int length = noiseField.Length;
            if (length <= 0)
                return;

            EnsureCpuFallbackAdvectionVelocityArrays(length);
            float[] velX = _cpuFallbackAdvectionVelX;
            float[] velY = _cpuFallbackAdvectionVelY;
            float[] velZ = _cpuFallbackAdvectionVelZ;
            for (int i = 0; i < length; i++)
            {
                float3 v = noiseField[i];
                velX[i] = v.x;
                velY[i] = v.y;
                velZ[i] = v.z;
            }

            int n = (int)Math.Round(Math.Pow(length, 1.0 / 3.0));
            if (n * n * n != length)
                return;

            float gridSpacing = prebakedVectorNoiseCellSizeMeters / n;

            double3 totalOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            float invCellSize = math.rcp(math.max(0.25f, prebakedVectorNoiseCellSizeMeters));

            if (_advectedSiltUpload.TryResolve(out NativeArray<AdvectedSilt> siltArray) && siltArray.IsCreated)
            {
                for (int i = 0; i < _activeAdvectedSiltCount; i++)
                {
                    AdvectedSilt particle = siltArray[i];
                    if ((particle.Flags & AdvectedBubbleActiveFlag) == 0)
                        continue;

                    double3 localCell = (new double3(particle.PositionWS.x, particle.PositionWS.y, particle.PositionWS.z) + totalOffset) * invCellSize;
                    int x = Math.Clamp((int)Math.Floor(localCell.x) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);
                    int y = Math.Clamp((int)Math.Floor(localCell.y) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);
                    int z = Math.Clamp((int)Math.Floor(localCell.z) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);

                    float advectedX = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velX, velY, velZ, x, y, z, deltaTime, gridSpacing);
                    float advectedY = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velY, velZ, velX, x, y, z, deltaTime, gridSpacing);
                    float advectedZ = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velZ, velX, velY, x, y, z, deltaTime, gridSpacing);

                    particle.PositionWS += particle.VelocityWS * deltaTime;
                    particle.VelocityWS = new float3(advectedX, advectedY, advectedZ);
                    particle.Life -= deltaTime;
                    if (particle.Life <= 0f)
                        particle.Flags &= ~AdvectedBubbleActiveFlag;

                    siltArray[i] = particle;
                }
            }

            if (_advectedBubbleUpload.TryResolve(out NativeArray<AdvectedBubble> bubbleArray) && bubbleArray.IsCreated)
            {
                for (int i = 0; i < _activeAdvectedBubbleCount; i++)
                {
                    AdvectedBubble particle = bubbleArray[i];
                    if ((particle.Flags & AdvectedBubbleActiveFlag) == 0)
                        continue;

                    double3 localCell = (new double3(particle.PositionWS.x, particle.PositionWS.y, particle.PositionWS.z) + totalOffset) * invCellSize;
                    int x = Math.Clamp((int)Math.Floor(localCell.x) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);
                    int y = Math.Clamp((int)Math.Floor(localCell.y) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);
                    int z = Math.Clamp((int)Math.Floor(localCell.z) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);

                    float advectedX = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velX, velY, velZ, x, y, z, deltaTime, gridSpacing);
                    float advectedY = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velY, velZ, velX, x, y, z, deltaTime, gridSpacing);
                    float advectedZ = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velZ, velX, velY, x, y, z, deltaTime, gridSpacing);

                    particle.PositionWS += particle.VelocityWS * deltaTime;
                    particle.VelocityWS = new float3(advectedX, advectedY, advectedZ);
                    particle.Life -= deltaTime;
                    if (particle.Life <= 0f)
                        particle.Flags &= ~AdvectedBubbleActiveFlag;

                    bubbleArray[i] = particle;
                }
            }

            if (_advectedDebrisUpload.TryResolve(out NativeArray<AdvectedDebris> debrisArray) && debrisArray.IsCreated)
            {
                for (int i = 0; i < _activeAdvectedDebrisCount; i++)
                {
                    AdvectedDebris particle = debrisArray[i];
                    if ((particle.Flags & AdvectedDebrisActiveFlag) == 0)
                        continue;

                    double3 localCell = (new double3(particle.PositionWS.x, particle.PositionWS.y, particle.PositionWS.z) + totalOffset) * invCellSize;
                    int x = Math.Clamp((int)Math.Floor(localCell.x) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);
                    int y = Math.Clamp((int)Math.Floor(localCell.y) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);
                    int z = Math.Clamp((int)Math.Floor(localCell.z) & HectonAnalyticalFlowField.VectorNoiseMask, 0, n - 1);

                    float advectedX = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velX, velY, velZ, x, y, z, deltaTime, gridSpacing);
                    float advectedY = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velY, velZ, velX, x, y, z, deltaTime, gridSpacing);
                    float advectedZ = Hecton8.PureLogic.Systems.FluidAdvectionStepCalculator.Compute(
                        velZ, velX, velY, x, y, z, deltaTime, gridSpacing);

                    particle.PositionWS += particle.VelocityWS * deltaTime;
                    particle.VelocityWS = new float3(advectedX, advectedY, advectedZ);
                    particle.Life -= deltaTime;
                    if (particle.Life <= 0f)
                        particle.Flags &= ~AdvectedDebrisActiveFlag;

                    debrisArray[i] = particle;
                }
            }
        }

        private void RunCpuFluidSimulationFallback(float deltaTime)
        {
            if (!_prebakedVectorNoiseField.TryResolve(out NativeArray<float3> noiseField) || !noiseField.IsCreated)
                return;

            int length = noiseField.Length;
            if (length <= 0)
                return;

            int n = HectonAnalyticalFlowField.VectorNoiseResolution;
            if (n * n * n != length)
                return;

            // Reuse pooled 3D grids across frames so the GPU-less CPU fallback stays within the
            // Zero-GC hot-path budget (was: fresh float[,,] per call for every stage below).
            EnsureCpuFallbackSimulationGrids(n);
            float[,,] velX = _cpuFallbackGridVelX;
            float[,,] velY = _cpuFallbackGridVelY;
            float[,,] velZ = _cpuFallbackGridVelZ;

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    for (int z = 0; z < n; z++)
                    {
                        int idx = x | (y << HectonAnalyticalFlowField.VectorNoiseSliceShift) | (z << HectonAnalyticalFlowField.VectorNoisePlaneShift);
                        float3 v = noiseField[idx];
                        velX[x, y, z] = v.x;
                        velY[x, y, z] = v.y;
                        velZ[x, y, z] = v.z;
                    }
                }
            }

            float gridSpacing = prebakedVectorNoiseCellSizeMeters / n;

            // Allocation-free vorticity confinement into pooled force grids.
            float[,,] fx = _cpuFallbackGridFx;
            float[,,] fy = _cpuFallbackGridFy;
            float[,,] fz = _cpuFallbackGridFz;
            Hecton8.PureLogic.Systems.VorticityConfinementForceCalculator.ComputeBuffered(
                velX, velY, velZ, 0.1f, gridSpacing,
                fx, fy, fz,
                _cpuFallbackGridVorticity, _cpuFallbackGridVorticityMag);

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    for (int z = 0; z < n; z++)
                    {
                        velX[x, y, z] += fx[x, y, z] * deltaTime;
                        velY[x, y, z] += fy[x, y, z] * deltaTime;
                        velZ[x, y, z] += fz[x, y, z] * deltaTime;
                    }
                }
            }

            float[,,] divergence = _cpuFallbackGridDivergence;
            float[,,] pressureA = _cpuFallbackGridPressureA;
            float[,,] pressureB = _cpuFallbackGridPressureB;
            float invSpacing2 = 1.0f / (2.0f * gridSpacing);

            for (int x = 1; x < n - 1; x++)
            {
                for (int y = 1; y < n - 1; y++)
                {
                    for (int z = 1; z < n - 1; z++)
                    {
                        float div = ((velX[x + 1, y, z] - velX[x - 1, y, z]) +
                                     (velY[x, y + 1, z] - velY[x, y - 1, z]) +
                                     (velZ[x, y, z + 1] - velZ[x, y, z - 1])) * invSpacing2;
                        divergence[x, y, z] = div;
                    }
                }
            }

            // Zero the full boundary of the pooled divergence grid so boundary cells behave like
            // the original fresh (zero-initialized) array; only interior cells were populated above.
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    divergence[i, j, 0] = 0f;
                    divergence[i, j, n - 1] = 0f;
                    divergence[0, i, j] = 0f;
                    divergence[n - 1, i, j] = 0f;
                    divergence[i, 0, j] = 0f;
                    divergence[i, n - 1, j] = 0f;
                }
            }

            // Allocation-free Jacobi pressure solve: ping-pong between two pooled grids, so no
            // array is allocated per iteration (was: Solve(...) allocating a new float[,,] 10x).
            float[,,] pressure = pressureA;
            for (int iter = 0; iter < 10; iter++)
            {
                float[,,] src = pressure;
                float[,,] dst = (src == pressureA) ? pressureB : pressureA;
                Hecton8.PureLogic.Systems.FluidPressureJacobiSolver.SolveBuffered(
                    src, divergence, gridSpacing, dst);
                pressure = dst;
            }

            for (int x = 1; x < n - 1; x++)
            {
                for (int y = 1; y < n - 1; y++)
                {
                    for (int z = 1; z < n - 1; z++)
                    {
                        velX[x, y, z] -= (pressure[x + 1, y, z] - pressure[x - 1, y, z]) * invSpacing2;
                        velY[x, y, z] -= (pressure[x, y + 1, z] - pressure[x, y - 1, z]) * invSpacing2;
                        velZ[x, y, z] -= (pressure[x, y, z + 1] - pressure[x, y, z - 1]) * invSpacing2;
                    }
                }
            }

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    for (int z = 0; z < n; z++)
                    {
                        int idx = x | (y << HectonAnalyticalFlowField.VectorNoiseSliceShift) | (z << HectonAnalyticalFlowField.VectorNoisePlaneShift);
                        noiseField[idx] = new float3(velX[x, y, z], velY[x, y, z], velZ[x, y, z]);
                    }
                }
            }
        }

        private bool IsFluidAdvectionStorageReady()
        {
            return fluidAdvectionCompute != null &&
                   _fluidAdvectionKernel >= 0 &&
                   HasFluidAdvectionNativeState();
        }

        private bool FlushFluidAdvectionGpuUploads()
        {
            if (!HasFluidAdvectionNativeState())
                return false;

            int remainingBudgetBytes = ResolveFluidAdvectionUploadBudgetBytes();
            bool uploadsReady = true;
            if (_advectedSiltGpuUploadDirty &&
                _advectedSiltBufferA != null &&
                _advectedSiltBufferB != null &&
                _advectedSiltBufferA.IsValid() &&
                _advectedSiltBufferB.IsValid())
            {
                uploadsReady &= FlushFluidAdvectionDirtyLane(
                    _advectedSiltBufferA,
                    _advectedSiltBufferB,
                    _advectedSiltUpload,
                    ref _advectedSiltDirtyPages,
                    _advectedSiltDirtyPageUploadSnapshot,
                    MaxAdvectedSiltCount,
                    ref remainingBudgetBytes,
                    ref _advectedSiltGpuUploadDirty);
            }

            if (_advectedBubbleGpuUploadDirty &&
                _advectedBubbleBufferA != null &&
                _advectedBubbleBufferB != null &&
                _advectedBubbleBufferA.IsValid() &&
                _advectedBubbleBufferB.IsValid())
            {
                uploadsReady &= FlushFluidAdvectionDirtyLane(
                    _advectedBubbleBufferA,
                    _advectedBubbleBufferB,
                    _advectedBubbleUpload,
                    ref _advectedBubbleDirtyPages,
                    _advectedBubbleDirtyPageUploadSnapshot,
                    MaxAdvectedBubbleCount,
                    ref remainingBudgetBytes,
                    ref _advectedBubbleGpuUploadDirty);
            }

            if (_advectedDebrisGpuUploadDirty &&
                _advectedDebrisBufferA != null &&
                _advectedDebrisBufferB != null &&
                _advectedDebrisBufferA.IsValid() &&
                _advectedDebrisBufferB.IsValid())
            {
                uploadsReady &= FlushFluidAdvectionDirtyLane(
                    _advectedDebrisBufferA,
                    _advectedDebrisBufferB,
                    _advectedDebrisUpload,
                    ref _advectedDebrisDirtyPages,
                    _advectedDebrisDirtyPageUploadSnapshot,
                    MaxAdvectedDebrisCount,
                    ref remainingBudgetBytes,
                    ref _advectedDebrisGpuUploadDirty);
            }

            return uploadsReady;
        }

        private static bool FlushFluidAdvectionDirtyLane<T>(
            GraphicsBuffer bufferA,
            GraphicsBuffer bufferB,
            FluidVaultBuffer<T> uploadSource,
            ref FluidVaultBuffer<byte> dirtyPagesHandle,
            byte[] dirtyPageSnapshot,
            int elementCount,
            ref int remainingBudgetBytes,
            ref bool dirtyFlag)
            where T : struct
        {
            if (!uploadSource.TryResolve(out NativeArray<T> source) || !source.IsCreated)
                return false;

            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                elementCount,
                FluidAdvectionDirtyPageSize);
            if (dirtyPageSnapshot == null || dirtyPageSnapshot.Length < requiredPages)
                return false;

            if (!dirtyPagesHandle.TryAcquireWriteLock(out NativeArray<byte> dirtyPages))
                return false;

            int copiedPageCount;
            int firstPageBytes;
            try
            {
                copiedPageCount = CopyFluidAdvectionDirtyPagesToSnapshot(
                    dirtyPages,
                    dirtyPageSnapshot,
                    requiredPages);
                firstPageBytes = GraphicsBufferUploadUtility.ResolveFirstDirtyPageBytes<T>(
                    dirtyPages,
                    elementCount,
                    FluidAdvectionDirtyPageSize);
                if (firstPageBytes <= 0)
                {
                    dirtyFlag = false;
                    return true;
                }
            }
            finally
            {
                dirtyPagesHandle.ReleaseWriteLock();
            }

            int firstMirroredPageBytes = firstPageBytes * 2;
            if (remainingBudgetBytes < firstMirroredPageBytes)
                return false;

            int perBufferBudget = math.max(firstPageBytes, remainingBudgetBytes >> 1);
            GraphicsBufferUploadUtility.PageUploadStats aStats =
                GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesSetDataFromSnapshot(
                    bufferA,
                    source,
                    dirtyPageSnapshot,
                    elementCount,
                    FluidAdvectionDirtyPageSize,
                    perBufferBudget,
                    markUploadedPages: false);
            GraphicsBufferUploadUtility.PageUploadStats bStats =
                GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesSetDataFromSnapshot(
                    bufferB,
                    source,
                    dirtyPageSnapshot,
                    elementCount,
                    FluidAdvectionDirtyPageSize,
                    perBufferBudget,
                    markUploadedPages: true);

            long uploadedBytes = aStats.UploadedBytes + bStats.UploadedBytes;
            remainingBudgetBytes = uploadedBytes >= remainingBudgetBytes
                ? 0
                : remainingBudgetBytes - (int)uploadedBytes;

            bool hasDeferredPages = true;
            if (bStats.UploadedPages > 0 &&
                !TryClearFluidAdvectionUploadedDirtyPages(
                    ref dirtyPagesHandle,
                    dirtyPageSnapshot,
                    copiedPageCount,
                    out hasDeferredPages))
            {
                dirtyFlag = true;
                return false;
            }

            if (bStats.UploadedPages <= 0)
                hasDeferredPages = bStats.DeferredPages > 0 || aStats.DeferredPages > 0;

            dirtyFlag = hasDeferredPages;
            return !hasDeferredPages;
        }

        private static void EnsureFluidAdvectionDirtyPageUploadSnapshot(ref byte[] snapshot, int requiredPageCount)
        {
            if (requiredPageCount <= 0 || (snapshot != null && snapshot.Length >= requiredPageCount))
                return;

            // COLD ALLOC: byte[dirtyPageCount] - fluid advection dirty-page GPU upload snapshot copied under DataVault lock and consumed after release - owner: HectonFluidEngine
            snapshot = new byte[requiredPageCount];
        }

        private static int CopyFluidAdvectionDirtyPagesToSnapshot(
            NativeArray<byte> dirtyPages,
            byte[] dirtyPageSnapshot,
            int requiredPages)
        {
            if (!dirtyPages.IsCreated || dirtyPageSnapshot == null || requiredPages <= 0)
                return 0;

            int pageCount = math.min(requiredPages, math.min(dirtyPages.Length, dirtyPageSnapshot.Length));
            for (int i = 0; i < pageCount; i++)
            {
                bool dirty = dirtyPages[i] != 0;
                dirtyPageSnapshot[i] = dirty ? (byte)1 : (byte)0;
                if (dirty)
                    dirtyPages[i] = GraphicsBufferUploadUtility.UploadedDirtyPageSnapshotMarker;
            }

            return pageCount;
        }

        private static bool TryClearFluidAdvectionUploadedDirtyPages(
            ref FluidVaultBuffer<byte> dirtyPagesHandle,
            byte[] dirtyPageSnapshot,
            int pageCount,
            out bool hasDeferredPages)
        {
            hasDeferredPages = true;
            if (dirtyPageSnapshot == null || pageCount <= 0)
                return true;

            if (!dirtyPagesHandle.TryAcquireWriteLock(out NativeArray<byte> dirtyPages))
                return false;

            try
            {
                hasDeferredPages = false;
                int limit = math.min(pageCount, math.min(dirtyPages.Length, dirtyPageSnapshot.Length));
                for (int i = 0; i < limit; i++)
                {
                    if (dirtyPageSnapshot[i] == GraphicsBufferUploadUtility.UploadedDirtyPageSnapshotMarker &&
                        dirtyPages[i] == GraphicsBufferUploadUtility.UploadedDirtyPageSnapshotMarker)
                    {
                        dirtyPages[i] = 0;
                    }

                    dirtyPageSnapshot[i] = 0;
                    hasDeferredPages |= dirtyPages[i] != 0;
                }

                return true;
            }
            finally
            {
                dirtyPagesHandle.ReleaseWriteLock();
            }
        }

        private static int ResolveFluidAdvectionUploadBudgetBytes()
        {
            float quality = SmoothFluidAdvectionQuality(ResolveFluidAdvectionQualityWeight());
            return (int)math.round(math.lerp(
                FluidAdvectionMinUploadBudgetBytes,
                FluidAdvectionMaxUploadBudgetBytes,
                quality));
        }

        private static void MarkAllFluidAdvectionDirtyPages(ref FluidVaultBuffer<byte> dirtyPagesHandle, int elementCount)
        {
            if (!dirtyPagesHandle.TryAcquireWriteLock(out NativeArray<byte> dirtyPages))
                return;

            try
            {
                GraphicsBufferUploadUtility.MarkAllDirtyPages(
                    dirtyPages,
                    elementCount,
                    FluidAdvectionDirtyPageSize);
            }
            finally
            {
                dirtyPagesHandle.ReleaseWriteLock();
            }
        }

        private static void MarkFluidAdvectionDirtyPage(ref FluidVaultBuffer<byte> dirtyPagesHandle, int elementIndex, int elementCount)
        {
            if (!dirtyPagesHandle.TryAcquireWriteLock(out NativeArray<byte> dirtyPages))
                return;

            try
            {
                GraphicsBufferUploadUtility.MarkDirtyPageRange(
                    dirtyPages,
                    elementIndex,
                    1,
                    elementCount,
                    FluidAdvectionDirtyPageSize);
            }
            finally
            {
                dirtyPagesHandle.ReleaseWriteLock();
            }
        }

        private void UploadAdvectedBubble(int slot, in AdvectedBubble bubble)
        {
            if ((uint)slot >= MaxAdvectedBubbleCount || !_advectedBubbleUpload.IsCreated)
                return;

            _advectedBubbleUpload[slot] = bubble;
            MarkFluidAdvectionDirtyPage(ref _advectedBubbleDirtyPages, slot, MaxAdvectedBubbleCount);
            _advectedBubbleGpuUploadDirty = true;
        }

        private void UploadAdvectedDebris(int slot, in AdvectedDebris debris)
        {
            if ((uint)slot >= MaxAdvectedDebrisCount || !_advectedDebrisUpload.IsCreated)
                return;

            _advectedDebrisUpload[slot] = debris;
            MarkFluidAdvectionDirtyPage(ref _advectedDebrisDirtyPages, slot, MaxAdvectedDebrisCount);
            _advectedDebrisGpuUploadDirty = true;
        }

        private static float3 ResolveSpawnJitter(int seed, float radius)
        {
            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            float x = HashToSignedUnit(hash);
            float y = HashToSignedUnit(hash ^ 0x9E3779B9u) * 0.35f;
            float z = HashToSignedUnit(hash ^ 0x85EBCA6Bu);
            float3 raw = new float3(x, y, z);
            float lengthSq = math.dot(raw, raw);
            float3 direction = lengthSq > 0.0001f ? raw * math.rsqrt(lengthSq) : new float3(0f, 1f, 0f);
            return direction * math.max(0f, radius);
        }

        private static float HashToSignedUnit(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return ((hash & 0x00ffffffu) * 0.00000011920928955078125f) - 1f;
        }

        private void WriteFluidAdvectionTelemetry()
        {
            if (!_fluidAdvectionTelemetry.IsCreated || _fluidAdvectionTelemetry.Length == 0)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastFluidAdvectionTelemetryFrame == frame)
                return;

            _lastFluidAdvectionTelemetryFrame = frame;
            int activeCount = _activeAdvectedSiltCount + _activeAdvectedBubbleCount + _activeAdvectedDebrisCount;
            int activeWakeCount = math.max(0, (int)_activeDynamicWakeParams.z);
            int index = _fluidAdvectionTelemetryCursor;
            uint flags = _fluidAdvectionRenderGraphQueued ? 1u : 0u;
            flags |= math.lengthsq(_pendingFluidAdvectionRuntimeShift) > 0.000001f ? 2u : 0u;
            flags |= activeWakeCount > 0 ? 4u : 0u;
            _fluidAdvectionTelemetry[index] = new FluidAdvectionTelemetryEntry
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                OriginShiftSequence = _lastOriginShiftSequence,
                ActiveAdvectedParticles = activeCount,
                SiltCount = _activeAdvectedSiltCount,
                BubbleCount = _activeAdvectedBubbleCount,
                DebrisCount = _activeAdvectedDebrisCount,
                ActiveTurbulenceWakes = activeWakeCount,
                Flags = flags,
                StateHash = BuildFluidAdvectionTelemetryHash(activeCount, activeWakeCount, flags)
            };
            _fluidAdvectionTelemetryCursor = (index + 1) % _fluidAdvectionTelemetry.Length;

            if (FluidAdvectionGlobalTelemetryIntervalFrames > 0 &&
                frame % FluidAdvectionGlobalTelemetryIntervalFrames == 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ActiveAdvectedParticlesTelemetryHash,
                    FluidAdvectionTelemetryContextHash,
                    activeCount);
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ActiveTurbulenceWakesTelemetryHash,
                    FluidAdvectionTelemetryContextHash,
                    activeWakeCount);
            }
        }

        private uint BuildFluidAdvectionTelemetryHash(int activeCount, int activeWakeCount, uint flags)
        {
            uint hash = 2166136261u;
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, activeCount));
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, _activeAdvectedSiltCount));
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, _activeAdvectedBubbleCount));
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, _activeAdvectedDebrisCount));
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, activeWakeCount));
            hash = HashAbyssalFlowTelemetry(hash, flags);
            return hash;
        }

        private void DumpFluidAdvectionTelemetryOnce(uint reasonFlags)
        {
            if (_fluidAdvectionTelemetryDumped || !_fluidAdvectionTelemetry.IsCreated)
                return;

            _fluidAdvectionTelemetryDumped = true;
            if (!BinaryFaultDumpsEnabled)
                return;
            try
            {
                WriteFluidAdvectionTelemetryDump(ResolveFluidDumpPath(FluidAdvectionDumpRelativePath), reasonFlags);
            }
            catch (System.Exception)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[HectonFluidEngine] Fluid advection telemetry dump failed.", this);
#endif
            }
        }

        private void WriteFluidAdvectionTelemetryDump(string dumpPath, uint reasonFlags)
        {
            int entryBytes = 36;
            int byteCount = 16 + _fluidAdvectionTelemetry.Length * entryBytes;
            NativeArray<byte> dump = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(HectonFluidEngine),
                "FluidAdvectionTelemetryDumpPayload");
            try
            {
                int cursor = 0;
                WriteUInt32LittleEndian(dump, ref cursor, 0x41435654u);
                WriteInt32LittleEndian(dump, ref cursor, FluidAdvectionTelemetryCapacity);
                WriteInt32LittleEndian(dump, ref cursor, _fluidAdvectionTelemetryCursor);
                WriteUInt32LittleEndian(dump, ref cursor, reasonFlags);
                for (int i = 0; i < _fluidAdvectionTelemetry.Length; i++)
                {
                    int index = (_fluidAdvectionTelemetryCursor + i) % _fluidAdvectionTelemetry.Length;
                    FluidAdvectionTelemetryEntry entry = _fluidAdvectionTelemetry[index];
                    WriteUInt32LittleEndian(dump, ref cursor, entry.FrameIndex);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.OriginShiftSequence);
                    WriteInt32LittleEndian(dump, ref cursor, entry.ActiveAdvectedParticles);
                    WriteInt32LittleEndian(dump, ref cursor, entry.SiltCount);
                    WriteInt32LittleEndian(dump, ref cursor, entry.BubbleCount);
                    WriteInt32LittleEndian(dump, ref cursor, entry.DebrisCount);
                    WriteInt32LittleEndian(dump, ref cursor, entry.ActiveTurbulenceWakes);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.Flags);
                    WriteUInt32LittleEndian(dump, ref cursor, entry.StateHash);
                }

                WriteNativeDump(dumpPath, dump, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref dump,
                    nameof(HectonFluidEngine),
                    "FluidAdvectionTelemetryDumpPayload");
            }
        }

        private bool TryDrainScheduledBuoyancyJob()
        {
            if (!_scheduledBuoyancyJobActive)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledBuoyancyHandle, false))
                return false;

            if (_hasPendingOriginShiftRebase)
            {
                ApplyPendingOriginShiftRebase();
                _scheduledBuoyancyJobActive = false;
                _scheduledForceCount = 0;
                return true;
            }

            ApplyScheduledForces();
            _scheduledBuoyancyJobActive = false;
            _scheduledForceCount = 0;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  GATHER — Copy Rigidbody data → NativeArrays
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Kopiruet pozitsii, skorosti i parametry iz managed Rigidbody
        /// v NativeArrays dlya Job. Main thread.
        ///
        /// Udalyaet null/destroyed obekty na letu (swap-remove v obratnom tsikle).
        ///
        /// IZMENENIE (Dry Zones / Ground Contact):
        ///   Kopiruet owner-side fluid suppression truth v BuoyancyParams.isInAir.
        ///   Dry zones always suppress fluid. Grounded contact suppresses fluid
        ///   only when the object is effectively above the waterline.
        ///   BuoyancyJob proveryaet etot flag i obnulyaet sily, esli true.
        /// </summary>
        private void GatherData(float resolvedWaterLevel)
        {
            using (_gatherDataProfilerMarker.Auto())
            {
            IBiomePhysicsInfluenceReadModel biomeFieldSampler = enableBiomeBuoyancyInfluence
                ? _proceduralFieldSampler
                : null;
            ISargassumDragReadModel sargassumDrag = _sargassumDragRuntime;
            IBrineFluidDensityReadModel brineDirector = _resourceDistributionRuntime;
            int sleepCount = 0;

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                BuoyancyObject obj = _objects[i];
                Rigidbody rb = _bodies[i];

                // ── Zaschita ot destroyed obektov (fake null check) ──
                if (obj == null || rb == null)
                {
                    int last = _objects.Count - 1;
                    MoveNativeSlotCache(i, last);
                    _objects[i] = _objects[last];
                    _bodies[i]  = _bodies[last];
                    _objects.RemoveAt(last);
                    _bodies.RemoveAt(last);
                    continue;
                }

                Vector3 com = rb.worldCenterOfMass;
                Vector3 vel = rb.linearVelocity;
                Vector3 angVel = rb.angularVelocity;
                Vector3 up = rb.transform.up;
                Vector3 localCurrent = Vector3.zero;
                obj.GetBuoyancySampleBounds(out Vector3 boundsCenter, out Vector3 boundsExtents);

                byte simulationMode = 0;
                byte simplifiedSubmersion = 0;
                float currentWeight = 1f;
                float stabilityWeight = 1f;
                float biomeBuoyancyMultiplier = 1f;

                if (enableDistanceLod && obj.AllowDistanceLod && lodObserver != null)
                {
                    float bias = math.max(0.1f, obj.LodBias);
                    // Use cached LOD distances
                    float nearDistanceSq = _cachedNearDistSq * bias * bias;
                    float mediumDistanceSq = _cachedMediumDistSq * bias * bias;
                    float farDistanceSq = _cachedFarDistSq * bias * bias;
                    float cullDistanceSq = _cachedCullDistSq * bias * bias;
                    float sleepDistanceSq = OceanSleepDistanceSq * bias * bias;
                    float wakeDistanceSq = OceanWakeDistanceSq * bias * bias;

                    float dx = com.x - lodObserver.position.x;
                    float dy = com.y - lodObserver.position.y;
                    float dz = com.z - lodObserver.position.z;
                    float distanceSq = dx * dx + dy * dy + dz * dz;
                    byte sleepState = _sleepMask.IsCreated && i < _sleepMask.Length ? _sleepMask[i] : (byte)0;
                    if (distanceSq > sleepDistanceSq)
                        sleepState = 1;
                    else if (distanceSq < wakeDistanceSq)
                        sleepState = 0;

                    if (_sleepMask.IsCreated && i < _sleepMask.Length)
                        _sleepMask[i] = sleepState;

                    if (sleepState != 0)
                    {
                        sleepCount++;
                        _debugCulledCount++;
                        simplifiedSubmersion = 1;
                        simulationMode = 2;
                        currentWeight = 0.05f;
                        stabilityWeight = 0f;
                    }
                    else if (distanceSq <= nearDistanceSq)
                    {
                        _debugNearCount++;
                    }
                    else if (distanceSq <= mediumDistanceSq)
                    {
                        _debugMediumCount++;
                        if ((_lodFrameCounter + i) % math.max(1, mediumLodDivisor) != 0)
                            simulationMode = 1;
                        currentWeight = 0.85f;
                        stabilityWeight = 0.9f;
                    }
                    else if (distanceSq <= farDistanceSq)
                    {
                        _debugFarCount++;
                        if ((_lodFrameCounter + i) % math.max(1, farLodDivisor) != 0)
                            simulationMode = 1;
                        simplifiedSubmersion = 1;
                        currentWeight = 0.55f;
                        stabilityWeight = 0.65f;
                    }
                    else if (distanceSq <= cullDistanceSq)
                    {
                        _debugCulledCount++;
                        simplifiedSubmersion = 1;
                        if (rb.IsSleeping())
                            simulationMode = 2;
                        else if ((_lodFrameCounter + i) % math.max(1, cullLodDivisor) != 0)
                            simulationMode = 1;
                        currentWeight = 0.3f;
                        stabilityWeight = 0.45f;
                    }
                    else
                    {
                        _debugCulledCount++;
                        simplifiedSubmersion = 1;
                        simulationMode = rb.IsSleeping() ? (byte)2 : (byte)1;
                        currentWeight = 0.12f;
                        stabilityWeight = 0.25f;
                    }
                }

                if (simulationMode != 2)
                    localCurrent = CurrentVolume.SampleAt(com);

                if (sargassumDrag != null && simulationMode != 2)
                {
                    float sampleRadius = math.max(0.5f, math.max(boundsExtents.x, boundsExtents.z));
                    if (sargassumDrag.SampleInfluence(
                            com,
                            sampleRadius,
                            vel,
                            out float sargassumSpeedMultiplier,
                            out float sargassumDragMultiplier,
                            out float sargassumDensity01))
                    {
                        currentWeight *= math.clamp(sargassumSpeedMultiplier, 0.2f, 1.25f);
                        currentWeight *= math.rcp(math.clamp(sargassumDragMultiplier, 1f, 3f));
                        stabilityWeight *= 1f + math.saturate(sargassumDensity01) * 0.25f;
                        biomeBuoyancyMultiplier *= 1f - math.saturate(sargassumDensity01) * 0.06f;
                    }
                }

                if (biomeFieldSampler != null &&
                    biomeFieldSampler.TrySampleBiomePhysicsInfluence(com, out float sampledBuoyancyMultiplier))
                {
                    biomeBuoyancyMultiplier *= Mathf.Max(0.05f, sampledBuoyancyMultiplier);
                }

                float3 currentPosition = new float3(com.x, com.y, com.z);
                if (_previousPositions.IsCreated &&
                    _previousPositionValid.IsCreated &&
                    i < _previousPositions.Length &&
                    i < _previousPositionValid.Length)
                {
                    _previousPositions[i] = _previousPositionValid[i] != 0 ? _positions[i] : currentPosition;
                    _previousPositionValid[i] = 1;
                }

                _positions[i]  = currentPosition;
                _velocities[i] = new float3(vel.x, vel.y, vel.z);
                _angularVelocities[i] = new float3(angVel.x, angVel.y, angVel.z);
                _upVectors[i] = new float3(up.x, up.y, up.z);
                if (_surfaceUpVectors.IsCreated && i < _surfaceUpVectors.Length)
                    _surfaceUpVectors[i] = new float3(0f, 1f, 0f);
                _params[i]     = new BuoyancyParams
                {
                    boundsCenter = new float3(boundsCenter.x, boundsCenter.y, boundsCenter.z),
                    boundsExtents = new float3(boundsExtents.x, boundsExtents.y, boundsExtents.z),
                    density = obj.Density,
                    volume  = obj.Volume,
                    height  = obj.Height > 0f ? obj.Height : 0.01f,
                    mass    = rb.mass,
                    currentResponse = obj.CurrentResponse * currentWeight,
                    surfaceStability = obj.SurfaceStability * stabilityWeight,
                    localFluidDensity = obj.UseLocalFluidDensityOverride
                        ? obj.LocalFluidDensityOverride
                        : waterDensity,
                    localCurrent = new float3(localCurrent.x, localCurrent.y, localCurrent.z),
                    buoyancyMultiplier = biomeBuoyancyMultiplier,
                    isInAir = obj.ShouldSuppressFluid(resolvedWaterLevel) ? (byte)1 : (byte)0,
                    simulationMode = simulationMode,
                    simplifiedSubmersion = simplifiedSubmersion,
                    useLocalFluidDensityOverride = obj.UseLocalFluidDensityOverride ? (byte)1 : (byte)0,
                    angularDragMultiplier = obj.RuntimeAngularDragMultiplier,
                    alignmentPadding = obj.AllowDistanceLod ? 0u : BuoyancyParams.ExactSurfaceNormalFlag
                };

                if (_brineFlags.IsCreated && i < _brineFlags.Length)
                {
                    _brineFlags[i] = 0;
                    _brineHeights[i] = 0f;
                    _brineDensityMultipliers[i] = 1f;
                    _brineCartographySectors[i] = default;
                    if (brineDirector != null &&
                        brineDirector.TrySampleBrineLayer(com, out BrineLayerSample brineSample) &&
                        math.isfinite(brineSample.AbsoluteHeightY))
                    {
                        _brineHeights[i] = brineSample.AbsoluteHeightY;
                        _brineDensityMultipliers[i] = math.max(1f, brineSample.DensityMultiplier);
                        _brineCartographySectors[i] = brineSample.CartographySector;
                        _brineFlags[i] = brineSample.Flags;
                    }
                }
            }
            _lastOceanSleepCount = sleepCount;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  APPLY — Write forces back to Rigidbody
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Queues computed force packets. Rigidbody mutation is owned by PhysicsApplySystem.
        /// </summary>
        private void ApplyScheduledForces()
        {
            using (_scheduledApplyProfilerMarker.Auto())
            {
            bool canDrainImpactEvents =
                TryResolveCachedFluidImpactEventRing(out NativeArray<FluidImpactEvent> fluidImpactEventRingView) &&
                _impactEventFlags.IsCreated &&
                _impactEventScratch.IsCreated;
            for (int i = 0; i < _scheduledForceCount; i++)
            {
                Rigidbody rb = _scheduledBodies[i];

                if (canDrainImpactEvents &&
                    (uint)i < (uint)_impactEventFlags.Length &&
                    (uint)i < (uint)_impactEventScratch.Length &&
                    _impactEventFlags[i] != 0)
                {
                    _impactEventFlags[i] = 0;
                    FluidImpactEvent impactEvent = _impactEventScratch[i];
                    if (TryEnqueueFluidImpactEvent(in impactEvent, fluidImpactEventRingView))
                    {
                        PublishFluidImpactSignal(in impactEvent, rb);
                    }
                }

                if (rb == null) continue;

                float3 force  = _resultForces[i];
                float3 torque = _resultTorques[i];

                // Propuskaem nulevye sily (obekt nad vodoy ili v suhoy zone)
                bool forceFinite = TrySanitizePhysicsVector(force, NonFiniteBuoyancyForceHash, out Vector3 sanitizedForce);
                if (!forceFinite)
                {
                    RecordFluidSovereigntyTelemetry(
                        FluidResultForcesBufferId,
                        FluidTelemetryFlagNonFiniteForce,
                        _nativeCapacity,
                        _resultForces.Length,
                        0f,
                        0f,
                        _scheduledForceCount);
                    DumpFluidSovereigntyTelemetryOnce(FluidTelemetryFlagNonFiniteForce);
                    DumpOceanSurfaceTelemetry();
                }

                if (forceFinite && sanitizedForce.sqrMagnitude > 0.0001f)
                {
                    PhysicsForceRouter.QueueAmbientForce(
                        rb,
                        sanitizedForce,
                        ForceMode.Force);
                }

                bool torqueFinite = TrySanitizePhysicsVector(torque, NonFiniteBuoyancyTorqueHash, out Vector3 sanitizedTorque);
                if (!torqueFinite)
                {
                    RecordFluidSovereigntyTelemetry(
                        FluidResultTorquesBufferId,
                        FluidTelemetryFlagNonFiniteTorque,
                        _nativeCapacity,
                        _resultTorques.Length,
                        0f,
                        0f,
                        _scheduledForceCount);
                    DumpFluidSovereigntyTelemetryOnce(FluidTelemetryFlagNonFiniteTorque);
                    DumpOceanSurfaceTelemetry();
                }

                if (torqueFinite && sanitizedTorque.sqrMagnitude > 0.0001f)
                {
                    PhysicsForceRouter.QueueAmbientTorque(
                        rb,
                        sanitizedTorque,
                        ForceMode.Force);
                }
            }
            }
        }

        private static void PublishFluidImpactSignal(in FluidImpactEvent impactEvent, Rigidbody body)
        {
            if (!math.all(math.isfinite(impactEvent.PositionWS)) ||
                !math.all(math.isfinite(impactEvent.VelocityWS)) ||
                !math.isfinite(impactEvent.MassKg))
            {
                return;
            }

            float speedSq = math.lengthsq(impactEvent.VelocityWS);
            float impactSpeed = -impactEvent.VelocityWS.y;
            if (impactSpeed <= 0.0001f || speedSq <= 0.000001f)
                return;

            float force = math.max(0f, impactSpeed * math.max(0.001f, impactEvent.MassKg));
            float intensity = math.saturate(force * 0.0025f);
            uint bodyId = body != null ? unchecked((uint)EntityId.ToULong(body.GetEntityId())) : 0u;

            Vector3 runtimePosition = new Vector3(impactEvent.PositionWS.x, impactEvent.PositionWS.y, impactEvent.PositionWS.z);
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition impactAup))
                return;

            ImpactSignal signal = new ImpactSignal
            {
                PointAup = impactAup,
                Force = force,
                Intensity = intensity,
                PrimaryBodyId = bodyId,
                WeightClass = ResolveImpactWeightClass(intensity),
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = 0
            };
            SignalBus<ImpactSignal>.TryPushTracked(in signal, ref s_x001HectonFluidEngineSignalPushDropCount);

            float3 splashRuntimeAup = AUPMath.ToRuntimeFloat3(in impactAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            SplashEvent splashEvent = new SplashEvent
            {
                RuntimePosition = impactEvent.PositionWS,
                AbsoluteUniversePosition = splashRuntimeAup,
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = impactSpeed,
                KineticEnergyJoules = 0.5f * math.max(0.001f, impactEvent.MassKg) * impactSpeed * impactSpeed,
                SubmersionFactor = math.saturate((impactEvent.SurfaceY - impactEvent.PositionWS.y) * math.rcp(math.max(0.01f, SplashDepthThresholdMeters))),
                SampleIndex = 0
            };
            FluidFeedbackEvents.TryPublishSplashQueued(in splashEvent);

            DebrisSpawnSignal debrisSignal = new DebrisSpawnSignal
            {
                PositionAup = impactAup,
                SpeciesHash = OceanSplashSignalHash,
                SourceEntityId = bodyId,
                Intensity01 = intensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindWaterSplash,
                Flags = 0
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in debrisSignal, ref s_x001HectonFluidEngineSignalPushDropCount);
        }

        private static byte ResolveImpactWeightClass(float intensity01)
        {
            if (intensity01 >= 0.75f)
                return 2;
            if (intensity01 >= 0.25f)
                return 1;
            return 0;
        }

        // ══════════════════════════════════════════════════════════
        //  NATIVE ARRAY MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Peresozdaet NativeArrays s uvelichennoy emkostyu (Capacity Doubling).
        /// </summary>
        private bool EnqueueCavitationBurst(
            Vector3 position,
            Vector3 direction,
            float intensity01,
            float radius,
            float acceleration,
            int sourceBodyInstanceId)
        {
            if (_cavitationBurstCount >= MaxCavitationBurstEvents ||
                !IsFiniteVector(position) ||
                !IsFiniteVector(direction) ||
                radius <= 0f ||
                acceleration <= 0f)
            {
                return false;
            }

            Vector3 safeDirection = NormalizeOrDefault(direction, Vector3.back);
            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;

            _cavitationBurstQueue[_cavitationBurstCount++] = new CavitationBurstEvent
            {
                Position = position,
                Direction = safeDirection,
                Intensity01 = math.saturate(intensity01),
                Radius = safeRadius,
                RadiusSq = radiusSq,
                InvRadiusSq = math.rcp(radiusSq),
                Acceleration = math.max(0f, acceleration),
                SourceBodyInstanceId = sourceBodyInstanceId
            };
            return true;
        }

        private void DrainCavitationBursts()
        {
            int burstCount = _cavitationBurstCount;
            if (burstCount <= 0)
                return;

            _cavitationBurstCount = 0;
            for (int i = 0; i < burstCount; i++)
            {
                CavitationBurstEvent burstEvent = _cavitationBurstQueue[i];
                _cavitationBurstQueue[i] = default;
                if (burstEvent.Intensity01 <= 0.0001f)
                    continue;

                QueueCavitationVisualBurst(in burstEvent);
                ApplyCavitationShockwave(in burstEvent);
            }
        }

        private void QueueCavitationVisualBurst(in CavitationBurstEvent burstEvent)
        {
            if (_cavitationVisualBurstCount >= _cavitationVisualBurstQueue.Length)
                return;

            _cavitationVisualBurstQueue[_cavitationVisualBurstCount++] = burstEvent;
        }

        private void FlushCavitationVisualBursts()
        {
            int burstCount = _cavitationVisualBurstCount;
            if (burstCount <= 0)
                return;

            _cavitationVisualBurstCount = 0;
            for (int i = 0; i < burstCount; i++)
            {
                CavitationBurstEvent burstEvent = _cavitationVisualBurstQueue[i];
                _cavitationVisualBurstQueue[i] = default;
                if (burstEvent.Intensity01 <= 0.0001f)
                    continue;

                EmitCavitationParticles(in burstEvent);
            }
        }

        private void EmitCavitationParticles(in CavitationBurstEvent burstEvent)
        {
            if (cavitationBubbleParticles == null)
                return;

            Transform particleTransform = cavitationBubbleParticles.transform;
            particleTransform.position = burstEvent.Position;
            if (burstEvent.Direction.sqrMagnitude > 0.0001f)
                particleTransform.rotation = Quaternion.LookRotation(burstEvent.Direction, Vector3.up);

            int rawEmitCount = (int)(cavitationBubbleEmitCountAtFullIntensity * burstEvent.Intensity01 + 0.999f);
            int emitCount = Mathf.Clamp(rawEmitCount, 1, cavitationBubbleEmitCountAtFullIntensity);
            cavitationBubbleParticles.Emit(emitCount);
        }

        private void ApplyCavitationShockwave(in CavitationBurstEvent burstEvent)
        {
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                burstEvent.Position,
                burstEvent.Radius,
                kindMask,
                s_CavitationShockwaveContacts);
            if (contactCount <= 0)
                return;

            int rigidbodyCount = 0;
            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit hit = s_CavitationShockwaveContacts[i];
                s_CavitationShockwaveContacts[i] = default;
                if (!LayerMatchesMask(hit.Layer, cavitationShockwaveLayers))
                    continue;

                Rigidbody candidateBody = hit.Rigidbody;
                if (candidateBody == null ||
                    candidateBody.isKinematic ||
                    unchecked((int)EntityId.ToULong(candidateBody.GetEntityId())) == burstEvent.SourceBodyInstanceId ||
                    candidateBody.mass > cavitationShockwaveMaxAffectedMassKg)
                {
                    continue;
                }

                TryAppendCavitationShockwaveBody(candidateBody, ref rigidbodyCount);
            }

            for (int i = 0; i < rigidbodyCount; i++)
            {
                Rigidbody targetBody = s_CavitationShockwaveRigidbodies[i];
                s_CavitationShockwaveRigidbodies[i] = null;
                if (targetBody == null || targetBody.isKinematic)
                    continue;

                float burstEnergy = burstEvent.Acceleration * burstEvent.Intensity01 * 1000f; // Derived equivalent energy
                var rawForce = Hecton8.PureLogic.Kinematics.CavitationBurstShockwaveForce.Calculate(
                    new System.Numerics.Vector3(targetBody.worldCenterOfMass.x, targetBody.worldCenterOfMass.y, targetBody.worldCenterOfMass.z),
                    new System.Numerics.Vector3(burstEvent.Position.x, burstEvent.Position.y, burstEvent.Position.z),
                    burstEnergy,
                    waterDensity
                );

                Vector3 impulseForce = new Vector3(rawForce.X, rawForce.Y, rawForce.Z);

                // Re-apply the engine specific direction modifiers and checks
                Vector3 radial = targetBody.worldCenterOfMass - burstEvent.Position;
                float radialDistanceSq = radial.sqrMagnitude;
                if (radialDistanceSq > burstEvent.RadiusSq)
                    continue;

                Vector3 radialDirection = radialDistanceSq > 0.000001f
                    ? NormalizeOrDefault(radial, burstEvent.Direction)
                    : burstEvent.Direction;
                radialDirection += burstEvent.Direction * 0.25f;
                radialDirection.y += cavitationShockwaveVerticalLift;
                radialDirection = NormalizeOrDefault(radialDirection, Vector3.up);

                // The pure logic calculated a force impulse.
                // We'll extract its magnitude to use as velocityChange since it scales inversely.
                float velocityChange = impulseForce.magnitude;

                // If the impulse is zero, continue.
                if (velocityChange <= 0.0001f)
                    continue;
                GlobalPhysicsStateManager.QueueKinematicImpact(
                    targetBody,
                    burstEvent.Position,
                    radialDirection,
                    velocityChange);
                PhysicsForceRouter.QueueForce(
                    targetBody,
                    radialDirection * velocityChange,
                    ForceMode.VelocityChange);
            }
        }

        private static void TryAppendCavitationShockwaveBody(
            Rigidbody candidateBody,
            ref int rigidbodyCount)
        {
            int capacity = math.min(s_CavitationShockwaveRigidbodies.Length, CavitationShockwaveHitCapacity);

            for (int i = 0; i < rigidbodyCount; i++)
            {
                if (s_CavitationShockwaveRigidbodies[i] != candidateBody)
                    continue;

                return;
            }

            if (rigidbodyCount >= capacity)
                return;

            s_CavitationShockwaveRigidbodies[rigidbodyCount] = candidateBody;
            rigidbodyCount++;
        }

        private static bool LayerMatchesMask(int layer, LayerMask mask)
        {
            return layer >= 0 && layer < 32 && (mask.value & (1 << layer)) != 0;
        }

        private void PrewarmBuoyancyNativeCapacity()
        {
            int targetCapacity = ResolvePrewarmedBuoyancyCapacity(1);
            EnsureManagedRegistryCapacity(targetCapacity);
            TryOpenOrAcquireBuoyancyNativeCapacity(targetCapacity, recordFault: false);
        }

        private int ResolvePrewarmedBuoyancyCapacity(int requiredCount)
        {
            int configuredCapacity = math.clamp(prewarmedBuoyancyCapacity, 128, 2048);
            return math.max(math.max(requiredCount, configuredCapacity), 1);
        }

        private void EnsureManagedRegistryCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            if (_objects.Capacity < requiredCapacity)
                _objects.Capacity = requiredCapacity;
            if (_bodies.Capacity < requiredCapacity)
                _bodies.Capacity = requiredCapacity;
        }

        private bool TryResolveBuoyancyNativeCapacityHot(int requiredCount, bool recordFault)
        {
            requiredCount = math.max(requiredCount, 1);
            if (_buoyancyNativeBuffersReady &&
                _nativeCapacity >= requiredCount &&
                _scheduledBodies != null &&
                _scheduledBodies.Length >= requiredCount)
            {
                return true;
            }

            if (AreBuoyancyNativeBuffersReady(requiredCount))
            {
                _buoyancyNativeBuffersReady = true;
                return true;
            }

            if (recordFault)
                RecordBuoyancyCapacityFault(requiredCount);
            return false;
        }

        private bool TryOpenOrAcquireBuoyancyNativeCapacity(int requiredCount, bool recordFault)
        {
            requiredCount = math.max(requiredCount, 1);
            if (TryResolveBuoyancyNativeCapacityHot(requiredCount, recordFault: false))
                return true;

            if (_scheduledBuoyancyJobActive)
            {
                if (recordFault)
                    RecordBuoyancyCapacityFault(requiredCount);
                return false;
            }

            IDataVault vault = ResolveFluidDataVault();
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
            {
                if (recordFault)
                    RecordBuoyancyCapacityFault(requiredCount);
                return false;
            }

            ReallocateNativeArrays(requiredCount);
            bool ready = AreBuoyancyNativeBuffersReady(requiredCount);
            _buoyancyNativeBuffersReady = ready;
            if (!ready && recordFault)
                RecordBuoyancyCapacityFault(requiredCount);
            return ready;
        }

        private bool AreBuoyancyNativeBuffersReady(int requiredCount)
        {
            requiredCount = math.max(requiredCount, 1);
            return _nativeCapacity >= requiredCount &&
                   _scheduledBodies != null &&
                   _scheduledBodies.Length >= requiredCount &&
                   _positions.IsCreated && _positions.Length >= requiredCount &&
                   _previousPositions.IsCreated && _previousPositions.Length >= requiredCount &&
                   _previousPositionValid.IsCreated && _previousPositionValid.Length >= requiredCount &&
                   _velocities.IsCreated && _velocities.Length >= requiredCount &&
                   _angularVelocities.IsCreated && _angularVelocities.Length >= requiredCount &&
                   _upVectors.IsCreated && _upVectors.Length >= requiredCount &&
                   _surfaceUpVectors.IsCreated && _surfaceUpVectors.Length >= requiredCount &&
                   _params.IsCreated && _params.Length >= requiredCount &&
                   _waveOffsets.IsCreated && _waveOffsets.Length >= requiredCount &&
                   _sleepMask.IsCreated && _sleepMask.Length >= requiredCount &&
                   _gerstnerWaves.IsCreated && _gerstnerWaves.Length >= MaxGerstnerWaveCount &&
                   _gpuBuoyancyForcesY.IsCreated && _gpuBuoyancyForcesY.Length >= requiredCount &&
                   _resultForces.IsCreated && _resultForces.Length >= requiredCount &&
                   _resultTorques.IsCreated && _resultTorques.Length >= requiredCount &&
                   _oceanSurfaceTelemetry.IsCreated && _oceanSurfaceTelemetry.Length >= OceanSurfaceTelemetryCapacity &&
                   _impactEventScratch.IsCreated && _impactEventScratch.Length >= requiredCount &&
                   _impactEventFlags.IsCreated && _impactEventFlags.Length >= requiredCount &&
                   _gpuBuoyancyObjectDataUpload.IsCreated && _gpuBuoyancyObjectDataUpload.Length >= requiredCount &&
                   _gpuBuoyancyReadback.IsCreated && _gpuBuoyancyReadback.Length >= requiredCount &&
                   _brineHeights.IsCreated && _brineHeights.Length >= requiredCount &&
                   _brineDensityMultipliers.IsCreated && _brineDensityMultipliers.Length >= requiredCount &&
                   _brineCartographySectors.IsCreated && _brineCartographySectors.Length >= requiredCount &&
                   _brineFlags.IsCreated && _brineFlags.Length >= requiredCount &&
                   _activeThrusterFlows.IsCreated && _activeThrusterFlows.Length >= MaxAnalyticalThrusterCount &&
                   _activeWhirlpools.IsCreated && _activeWhirlpools.Length >= MaxAnalyticalWhirlpoolCount &&
                   _activeViscosityRegions.IsCreated && _activeViscosityRegions.Length >= MaxDynamicViscosityRegionCount &&
                   _viscosityGradientLut.IsCreated && _viscosityGradientLut.Length >= ViscosityGradientLutSize;
        }

        private void RecordBuoyancyCapacityFault(int requiredCount)
        {
            RecordFluidSovereigntyTelemetry(
                FluidPositionsBufferId,
                FluidTelemetryFlagCapacityExceeded,
                _nativeCapacity,
                _positions.Length,
                0f,
                0f,
                requiredCount);
            ReportBuoyancyCapacityFault(requiredCount);
        }

        private void ReportBuoyancyCapacityFault(int requiredCount)
        {
            int frame = Time.frameCount;
            if (_lastBuoyancyCapacityFaultFrame >= 0 && frame - _lastBuoyancyCapacityFaultFrame < 30)
                return;

            _lastBuoyancyCapacityFaultFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BuoyancyCapacityExceededHash,
                HectonFluidEngineContextHash,
                requiredCount);
        }

        private void ReallocateNativeArrays(int requiredCount)
        {
            requiredCount = math.max(requiredCount, 1);
            int targetCapacity = ResolvePrewarmedBuoyancyCapacity(requiredCount);
            int newCapacity = math.max(targetCapacity, math.max(128, _nativeCapacity * 2));
            int growthIterations = 0;

            while (newCapacity < targetCapacity)
            {
                if (growthIterations >= MaxNativeCapacityGrowthIterations || newCapacity > (int.MaxValue / 2))
                {
                    newCapacity = math.max(newCapacity, targetCapacity);
                    break;
                }

                newCapacity *= 2;
                growthIterations++;
            }

            ReleaseBuoyancyNativeBuffersForResize();
            EnsureFluidSovereigntyTelemetry();

            bool primaryVaultReady =
                _positions.Ensure(FluidPositionsBufferId, newCapacity, NativeArrayOptions.UninitializedMemory) &
                _previousPositions.Ensure(FluidPreviousPositionsBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _previousPositionValid.Ensure(FluidPreviousPositionValidBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _velocities.Ensure(FluidVelocitiesBufferId, newCapacity, NativeArrayOptions.UninitializedMemory) &
                _angularVelocities.Ensure(FluidAngularVelocitiesBufferId, newCapacity, NativeArrayOptions.UninitializedMemory) &
                _upVectors.Ensure(FluidUpVectorsBufferId, newCapacity, NativeArrayOptions.UninitializedMemory) &
                _surfaceUpVectors.Ensure(FluidSurfaceUpVectorsBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _params.Ensure(FluidBuoyancyParamsBufferId, newCapacity, NativeArrayOptions.UninitializedMemory) &
                _waveOffsets.Ensure(FluidWaveOffsetsBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _sleepMask.Ensure(FluidSleepMaskBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _gerstnerWaves.Ensure(FluidLocalGerstnerWavesBufferId, MaxGerstnerWaveCount, NativeArrayOptions.ClearMemory) &
                _gpuBuoyancyForcesY.Ensure(FluidGpuBuoyancyForcesYBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _resultForces.Ensure(FluidResultForcesBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _resultTorques.Ensure(FluidResultTorquesBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _oceanSurfaceTelemetry.Ensure(FluidOceanSurfaceTelemetryBufferId, OceanSurfaceTelemetryCapacity, NativeArrayOptions.ClearMemory) &
                _impactEventScratch.Ensure(FluidImpactEventScratchBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _impactEventFlags.Ensure(FluidImpactEventFlagsBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _gpuBuoyancyObjectDataUpload.Ensure(FluidGpuBuoyancyObjectUploadBufferId, newCapacity, NativeArrayOptions.UninitializedMemory) &
                _gpuBuoyancyReadback.Ensure(FluidGpuBuoyancyReadbackBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _brineHeights.Ensure(FluidBrineHeightsBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _brineDensityMultipliers.Ensure(FluidBrineDensityMultipliersBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _brineCartographySectors.Ensure(FluidBrineCartographySectorsBufferId, newCapacity, NativeArrayOptions.ClearMemory) &
                _brineFlags.Ensure(FluidBrineFlagsBufferId, newCapacity, NativeArrayOptions.ClearMemory);

            if (!primaryVaultReady)
            {
                RecordFluidSovereigntyTelemetry(
                    FluidPositionsBufferId,
                    FluidTelemetryFlagResolveFault,
                    newCapacity,
                    _positions.Length,
                    0f,
                    0f,
                    requiredCount);
                _nativeCapacity = 0;
                _buoyancyNativeBuffersReady = false;
                return;
            }
            EnsureAbyssalFlowNativeState();
            _activeThrusterFlows.Ensure(FluidActiveThrusterFlowsBufferId, MaxAnalyticalThrusterCount, NativeArrayOptions.ClearMemory);
            _activeWhirlpools.Ensure(FluidActiveWhirlpoolsBufferId, MaxAnalyticalWhirlpoolCount, NativeArrayOptions.ClearMemory);
            _activeViscosityRegions.Ensure(FluidActiveViscosityRegionsBufferId, MaxDynamicViscosityRegionCount, NativeArrayOptions.ClearMemory);
            _viscosityGradientLut.Ensure(FluidViscosityGradientLutBufferId, ViscosityGradientLutSize, NativeArrayOptions.UninitializedMemory);
            InitializeViscosityGradientLut();
            if (TryOpenOrAcquireFluidImpactEventRing(out NativeArray<FluidImpactEvent> fluidImpactEventRing))
                ClearFluidImpactEventRing(fluidImpactEventRing);
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
            RegisterNativeMemorySentinel();
            EnsureSharedGerstnerDataVaultBuffers();
            _scheduledBodies = new Rigidbody[newCapacity];

            _nativeCapacity = newCapacity;
            _buoyancyNativeBuffersReady = true;
            EnsureGpuBuoyancyBuffersColdIfEnabled(newCapacity);
            RecordFluidSovereigntyTelemetry(
                FluidPositionsBufferId,
                FluidTelemetryFlagResolveOk,
                newCapacity,
                _positions.Length,
                0f,
                0f,
                requiredCount);
        }

        private void ReleaseBuoyancyNativeBuffersForResize()
        {
            _positions.Release();
            _previousPositions.Release();
            _previousPositionValid.Release();
            _velocities.Release();
            _angularVelocities.Release();
            _upVectors.Release();
            _surfaceUpVectors.Release();
            _params.Release();
            _waveOffsets.Release();
            _sleepMask.Release();
            _gerstnerWaves.Release();
            _gpuBuoyancyForcesY.Release();
            _resultForces.Release();
            _resultTorques.Release();
            _oceanSurfaceTelemetry.Release();
            _impactEventScratch.Release();
            _impactEventFlags.Release();
            _gpuBuoyancyObjectDataUpload.Release();
            _gpuBuoyancyReadback.Release();
            _brineHeights.Release();
            _brineDensityMultipliers.Release();
            _brineCartographySectors.Release();
            _brineFlags.Release();
            _activeThrusterFlows.Release();
            _activeWhirlpools.Release();
            _activeViscosityRegions.Release();
            _viscosityGradientLut.Release();
            ReleaseFluidImpactEventRing();
            _activeThrusterFlowCount = 0;
            _activeWhirlpoolFlowCount = 0;
            _activeViscosityRegionCount = 0;
            _activeGerstnerWaveCount = 0;
            _sharedGerstnerWavesHandle = default;
            _sharedGerstnerMetaHandle = default;
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
            _scheduledBodies = null;
            _scheduledBuoyancyHandle = default;
            _scheduledBuoyancyJobActive = false;
            _scheduledForceCount = 0;
            _hasPendingOriginShiftRebase = false;
            _pendingOriginShiftOffset = Vector3.zero;
            ReleaseGpuBuoyancyBuffers();
            _hasGpuBuoyancyData = false;
            _nativeCapacity = 0;
            _buoyancyNativeBuffersReady = false;
        }

        /// <summary>
        /// Osvobozhdaet NativeArrays. Vyzyvaetsya pri Destroy i Resize.
        /// </summary>
        private void DisposeNativeArrays(bool releaseAbyssalFlow = true, bool releaseGraphicsImmediately = true)
        {
            _positions.Release();
            _previousPositions.Release();
            _previousPositionValid.Release();
            _velocities.Release();
            _angularVelocities.Release();
            _upVectors.Release();
            _surfaceUpVectors.Release();
            _params.Release();
            _waveOffsets.Release();
            _sleepMask.Release();
            _gerstnerWaves.Release();
            _gpuBuoyancyForcesY.Release();
            _resultForces.Release();
            _resultTorques.Release();
            _oceanSurfaceTelemetry.Release();
            _impactEventScratch.Release();
            _impactEventFlags.Release();
            _gpuBuoyancyObjectDataUpload.Release();
            _gpuBuoyancyReadback.Release();
            _brineHeights.Release();
            _brineDensityMultipliers.Release();
            _brineCartographySectors.Release();
            _brineFlags.Release();
            if (releaseAbyssalFlow)
            {
                _gpuAbyssalHeatSourceUpload.Release();
                _abyssalFlowTelemetry.Release();
                _maelstromTelemetry.Release();
                _activeMaelstroms.Release();
                DisposeSplashdownImpulseState(releaseGraphicsImmediately);
            }
            _advectedSiltUpload.Release();
            _advectedBubbleUpload.Release();
            _advectedDebrisUpload.Release();
            _advectedSiltDirtyPages.Release();
            _advectedBubbleDirtyPages.Release();
            _advectedDebrisDirtyPages.Release();
            _advectedSiltDirtyPageUploadSnapshot = null;
            _advectedBubbleDirtyPageUploadSnapshot = null;
            _advectedDebrisDirtyPageUploadSnapshot = null;
            _fluidAdvectionTelemetry.Release();
            _fluidAdvectionStateReady = false;
            _fluidAdvectionRenderGraphQueued = false;
            _advectedSiltGpuUploadDirty = false;
            _advectedBubbleGpuUploadDirty = false;
            _advectedDebrisGpuUploadDirty = false;
            _fluidAdvectionTelemetryCursor = 0;
            _lastFluidAdvectionTelemetryFrame = -1;
            _activeThrusterFlows.Release();
            _activeWhirlpools.Release();
            _activeViscosityRegions.Release();
            _viscosityGradientLut.Release();
            ReleaseFluidImpactEventRing();
            _activeThrusterFlowCount = 0;
            _activeWhirlpoolFlowCount = 0;
            _activeMaelstromCount = 0;
            _activeViscosityRegionCount = 0;
            _activeAdvectedSiltCount = 0;
            _activeAdvectedBubbleCount = 0;
            _activeAdvectedDebrisCount = 0;
            _activeGerstnerWaveCount = 0;
            _sharedGerstnerWavesHandle = default;
            _sharedGerstnerMetaHandle = default;
            _dynamicWakeBufferHandle = default;
            _dynamicWakeVectorBufferHandle = default;
            _activeMaelstromMeta = Vector4.zero;
            _lastOceanSleepCount = 0;
            _oceanSurfaceTelemetryWriteIndex = 0;
            _maelstromTelemetryCursor = 0;
            _scheduledBodies = null;
            _scheduledBuoyancyHandle = default;
            _scheduledBuoyancyJobActive = false;
            _scheduledForceCount = 0;
            _pendingOriginShiftOffset = Vector3.zero;
            _hasPendingOriginShiftRebase = false;
            _cavitationBurstCount = 0;
            _nextMaelstromAudioTime = 0f;
            _nextMaelstromDamageTime = 0f;
            if (releaseGraphicsImmediately)
                ReleaseGpuBuoyancyBuffers();
            else
                QueueFluidGraphicsRelease(FluidGraphicsReleaseGpuBuoyancy);

            if (releaseAbyssalFlow)
            {
                if (releaseGraphicsImmediately)
                    ReleaseGpuAbyssalFlowBuffers();
                else
                    QueueFluidGraphicsRelease(FluidGraphicsReleaseAbyssalFlow);
            }
            _hasGpuBuoyancyData = false;

            _nativeCapacity = 0;
            _buoyancyNativeBuffersReady = false;
        }

        private void RegisterNativeMemorySentinel()
        {
            // DataVault owns these native buffers; the owner-level allocation path records native memory.
        }

        private IDataVault ResolveFluidDataVault()
        {
            return _dataVault;
        }

        private bool OpenOrAcquireFluidVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = ResolveFluidDataVault();
            if (TryOpenFluidVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Fluid,
                options);
            return TryOpenFluidVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenExistingFluidVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = ResolveFluidDataVault();
            if (TryOpenFluidVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out handle))
            {
                buffer = default;
                return false;
            }

            return TryOpenFluidVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenFluidVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsMatchingFluidVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMatchingFluidVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Fluid &&
                   handle.Generation != 0u;
        }

        private void ResetFluidVaultGenerationHandles()
        {
            _positions.Reset();
            _previousPositions.Reset();
            _previousPositionValid.Reset();
            _velocities.Reset();
            _angularVelocities.Reset();
            _upVectors.Reset();
            _surfaceUpVectors.Reset();
            _params.Reset();
            _waveOffsets.Reset();
            _sleepMask.Reset();
            _gerstnerWaves.Reset();
            _gpuBuoyancyForcesY.Reset();
            _resultForces.Reset();
            _resultTorques.Reset();
            _oceanSurfaceTelemetry.Reset();
            _impactEventScratch.Reset();
            _impactEventFlags.Reset();
            _gpuBuoyancyObjectDataUpload.Reset();
            _gpuBuoyancyReadback.Reset();
            _brineHeights.Reset();
            _brineDensityMultipliers.Reset();
            _brineCartographySectors.Reset();
            _brineFlags.Reset();
            _gpuAbyssalHeatSourceUpload.Reset();
            _activeThrusterFlows.Reset();
            _activeWhirlpools.Reset();
            _activeMaelstroms.Reset();
            _maelstromTelemetry.Reset();
            _activeViscosityRegions.Reset();
            _viscosityGradientLut.Reset();
            _prebakedVectorNoiseField.Reset();
            _advectedSiltUpload.Reset();
            _advectedBubbleUpload.Reset();
            _advectedDebrisUpload.Reset();
            _advectedSiltDirtyPages.Reset();
            _advectedBubbleDirtyPages.Reset();
            _advectedDebrisDirtyPages.Reset();
            _emptyAbyssalFlowUpload.Reset();
            _fluidAdvectionTelemetry.Reset();
            _splashdownImpulseUpload.Reset();
            _splashdownImpulseStats.Reset();
            _abyssalFlowTelemetry.Reset();
            _fluidSovereigntyTelemetry.Reset();
            _fluidSovereigntyTelemetryCursor.Reset();
            _sharedGerstnerWavesHandle = default;
            _sharedGerstnerMetaHandle = default;
            _dynamicWakeBufferHandle = default;
            _dynamicWakeVectorBufferHandle = default;
            _fluidImpactEventRingHandle = default;
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
            _buoyancyNativeBuffersReady = false;
        }

        private bool TryOpenOrAcquireFluidImpactEventRing(out NativeArray<FluidImpactEvent> ring)
        {
            ring = default;

            IDataVault vault = ResolveFluidDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!IsVaultGenerationHandleCreated(in _fluidImpactEventRingHandle))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle<FluidImpactEvent>(
                            FluidImpactEventRingBufferId,
                            out _fluidImpactEventRingHandle))
                    {
                        return false;
                    }
                }
                else
                {
                    _fluidImpactEventRingHandle = vault.EnsureGenerationHandle<FluidImpactEvent>(
                        FluidImpactEventRingBufferId,
                        FluidImpactEventQueueCapacity,
                        SystemID.Fluid,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!vault.TryResolveHandle(in _fluidImpactEventRingHandle, out ring) ||
                !ring.IsCreated ||
                ring.Length < FluidImpactEventQueueCapacity)
            {
                ring = default;
                return false;
            }

            return true;
        }

        private bool TryResolveCachedFluidImpactEventRing(out NativeArray<FluidImpactEvent> ring)
        {
            ring = default;

            IDataVault vault = ResolveFluidDataVault();
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultGenerationHandleCreated(in _fluidImpactEventRingHandle))
            {
                return false;
            }

            if (!vault.TryResolveHandle(in _fluidImpactEventRingHandle, out ring) ||
                !ring.IsCreated ||
                ring.Length < FluidImpactEventQueueCapacity)
            {
                ring = default;
                return false;
            }

            return true;
        }

        private static bool IsVaultGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u &&
                   handle.Generation != 0u;
        }

        private static void ClearFluidImpactEventRing(NativeArray<FluidImpactEvent> ring)
        {
            if (!ring.IsCreated)
                return;

            int count = math.min(ring.Length, FluidImpactEventQueueCapacity);
            for (int i = 0; i < count; i++)
                ring[i] = default;
        }

        private void ReleaseFluidImpactEventRing()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultGenerationHandleCreated(in _fluidImpactEventRingHandle))
                vault.ReleaseBuffer(in _fluidImpactEventRingHandle);

            _fluidImpactEventRingHandle = default;
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
        }

        private bool TryEnqueueFluidImpactEvent(
            in FluidImpactEvent impactEvent,
            NativeArray<FluidImpactEvent> ring)
        {
            if (!ring.IsCreated ||
                ring.Length < FluidImpactEventQueueCapacity ||
                _fluidImpactQueuedCount >= FluidImpactEventQueueCapacity)
            {
                return false;
            }

            int index = _fluidImpactEventWriteIndex;
            if ((uint)index >= (uint)FluidImpactEventQueueCapacity)
                index = 0;

            ring[index] = impactEvent;
            _fluidImpactEventWriteIndex = index + 1;
            if (_fluidImpactEventWriteIndex >= FluidImpactEventQueueCapacity)
                _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount++;
            return true;
        }

        private bool TryDequeueFluidImpactEvent(out FluidImpactEvent impactEvent)
        {
            impactEvent = default;
            if (_fluidImpactQueuedCount <= 0 ||
                !TryResolveCachedFluidImpactEventRing(out NativeArray<FluidImpactEvent> ring))
            {
                return false;
            }

            int index = _fluidImpactEventReadIndex;
            if ((uint)index >= (uint)FluidImpactEventQueueCapacity)
                index = 0;

            impactEvent = ring[index];
            ring[index] = default;
            _fluidImpactEventReadIndex = index + 1;
            if (_fluidImpactEventReadIndex >= FluidImpactEventQueueCapacity)
                _fluidImpactEventReadIndex = 0;
            _fluidImpactQueuedCount--;
            return true;
        }

        private void EnsureAbyssalFlowNativeState()
        {
            _gpuAbyssalHeatSourceUpload.Ensure(
                FluidGpuAbyssalHeatSourceUploadBufferId,
                MaxAbyssalHeatSourceCount,
                NativeArrayOptions.ClearMemory);

            if (!_abyssalFlowTelemetry.IsCreated)
            {
                _abyssalFlowTelemetry.Ensure(
                    FluidAbyssalFlowTelemetryBufferId,
                    AbyssalFlowTelemetryCapacity,
                    NativeArrayOptions.ClearMemory);
                _abyssalFlowTelemetryCursor = 0;
                _abyssalFlowTelemetryDumped = false;
            }

            _activeWhirlpools.Ensure(
                FluidActiveWhirlpoolsBufferId,
                MaxAnalyticalWhirlpoolCount,
                NativeArrayOptions.ClearMemory);

            _activeMaelstroms.Ensure(
                FluidActiveMaelstromsBufferId,
                MaxActiveMaelstromCount,
                NativeArrayOptions.ClearMemory);

            if (!_maelstromTelemetry.IsCreated)
            {
                _maelstromTelemetry.Ensure(
                    FluidMaelstromTelemetryBufferId,
                    MaelstromTelemetryCapacity,
                    NativeArrayOptions.ClearMemory);
                _maelstromTelemetryCursor = 0;
                _maelstromTelemetryDumped = false;
            }
        }

        private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            Exception firstException = null;

            if (dependency.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref dependency);
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }

                try
                {
                    array.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
            }
            else
            {
                JobHandle disposeHandle = array.Dispose(dependency);
                if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))
                    throw new InvalidOperationException("HectonFluidEngine native array disposal did not complete before sentinel unregister.");

                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            array = default;

            if (firstException != null)
                throw firstException;
        }

        private void InitializeViscosityGradientLut()
        {
            if (!_viscosityGradientLut.IsCreated || _viscosityGradientLut.Length <= 0)
                return;

            int lastIndex = _viscosityGradientLut.Length - 1;
            for (int i = 0; i < _viscosityGradientLut.Length; i++)
            {
                float x = lastIndex > 0 ? i * math.rcp((float)lastIndex) : 1f;
                _viscosityGradientLut[i] = x * x * (3f - 2f * x);
            }
        }

        private void EnsurePrebakedVectorNoiseField()
        {
            if (!enablePrebakedVectorNoise)
            {
                DisposePrebakedVectorNoiseField();
                return;
            }

            if (_prebakedVectorNoiseField.IsCreated &&
                _prebakedVectorNoiseField.Length == HectonAnalyticalFlowField.VectorNoiseVoxelCount &&
                _prebakedVectorNoiseRuntimeSeed == prebakedVectorNoiseSeed)
            {
                return;
            }

            DisposePrebakedVectorNoiseField();

            _prebakedVectorNoiseField.Ensure(
                FluidPrebakedVectorNoiseFieldBufferId,
                HectonAnalyticalFlowField.VectorNoiseVoxelCount,
                NativeArrayOptions.UninitializedMemory);

            uint seed = unchecked((uint)prebakedVectorNoiseSeed);
            int index = 0;
            for (int z = 0; z < HectonAnalyticalFlowField.VectorNoiseResolution; z++)
            {
                for (int y = 0; y < HectonAnalyticalFlowField.VectorNoiseResolution; y++)
                {
                    for (int x = 0; x < HectonAnalyticalFlowField.VectorNoiseResolution; x++)
                    {
                        float3 sample = BuildPrebakedCurlVector(x, y, z, seed);
                        _prebakedVectorNoiseField[index] = sample;
                        index++;
                    }
                }
            }

            _prebakedVectorNoiseTexture = IsValidPrebakedVectorNoiseTexture(authoredPrebakedVectorNoiseTexture)
                ? authoredPrebakedVectorNoiseTexture
                : null;
            Shader.SetGlobalTexture(_PrebakedVectorNoise3DId, _prebakedVectorNoiseTexture);
            _prebakedVectorNoiseRuntimeSeed = prebakedVectorNoiseSeed;
        }

        private void DisposePrebakedVectorNoiseField()
        {
            _prebakedVectorNoiseField.Release();
            _prebakedVectorNoiseRuntimeSeed = int.MinValue;
            Shader.SetGlobalTexture(_PrebakedVectorNoise3DId, null);
            _prebakedVectorNoiseTexture = null;
        }

        private static bool IsValidPrebakedVectorNoiseTexture(Texture3D texture)
        {
            return texture != null &&
                   texture.width == HectonAnalyticalFlowField.VectorNoiseResolution &&
                   texture.height == HectonAnalyticalFlowField.VectorNoiseResolution &&
                   texture.depth == HectonAnalyticalFlowField.VectorNoiseResolution;
        }

        private static float3 BuildPrebakedCurlVector(int x, int y, int z, uint seed)
        {
            float3 curl;
            curl.x = SampleVectorNoiseScalar(x, y + 1, z, seed + 0x8DA6B343u) -
                     SampleVectorNoiseScalar(x, y - 1, z, seed + 0x8DA6B343u) -
                     (SampleVectorNoiseScalar(x, y, z + 1, seed + 0xD8163841u) -
                      SampleVectorNoiseScalar(x, y, z - 1, seed + 0xD8163841u));
            curl.y = SampleVectorNoiseScalar(x, y, z + 1, seed + 0xCB1AB31Fu) -
                     SampleVectorNoiseScalar(x, y, z - 1, seed + 0xCB1AB31Fu) -
                     (SampleVectorNoiseScalar(x + 1, y, z, seed + 0x8DA6B343u) -
                      SampleVectorNoiseScalar(x - 1, y, z, seed + 0x8DA6B343u));
            curl.z = SampleVectorNoiseScalar(x + 1, y, z, seed + 0xD8163841u) -
                     SampleVectorNoiseScalar(x - 1, y, z, seed + 0xD8163841u) -
                     (SampleVectorNoiseScalar(x, y + 1, z, seed + 0xCB1AB31Fu) -
                      SampleVectorNoiseScalar(x, y - 1, z, seed + 0xCB1AB31Fu));

            float magnitudeSq = math.lengthsq(curl);
            if (magnitudeSq <= 0.000001f || !math.isfinite(magnitudeSq))
                return new float3(1f, 0f, 0f);

            return curl * math.rsqrt(magnitudeSq);
        }

        private static float SampleVectorNoiseScalar(int x, int y, int z, uint seed)
        {
            uint hash = seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash ^= (uint)z * 0xC2B2AE35u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return ((hash & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
        }

        private void CopyAnalyticalFlowInputsToNative()
        {
            CopyActiveMaelstromsToNative();

            if (!_activeThrusterFlows.IsCreated || !_activeWhirlpools.IsCreated || !_activeViscosityRegions.IsCreated)
                return;

            int thrusterWriteIndex = 0;
            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
            {
                ActiveThrusterFlow thruster = _thrusterFlowBuffer[i];
                if (thruster.Active == 0 || thruster.Strength <= 0f || thruster.RadiusSq <= 0f || thruster.InvRadiusSq <= 0f)
                    continue;

                _activeThrusterFlows[thrusterWriteIndex++] = thruster;
            }

            for (int i = thrusterWriteIndex; i < MaxAnalyticalThrusterCount; i++)
                _activeThrusterFlows[i] = default;

            _activeThrusterFlowCount = thrusterWriteIndex;

            int whirlpoolWriteIndex = 0;
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow whirlpool = _whirlpoolFlowBuffer[i];
                if (!IsValidWhirlpool(whirlpool))
                    continue;

                _activeWhirlpools[whirlpoolWriteIndex++] = whirlpool;
            }

            for (int i = whirlpoolWriteIndex; i < MaxAnalyticalWhirlpoolCount; i++)
                _activeWhirlpools[i] = default;

            _activeWhirlpoolFlowCount = whirlpoolWriteIndex;

            int viscosityWriteIndex = 0;
            for (int i = 0; i < MaxDynamicViscosityRegionCount; i++)
            {
                FluidViscosityRegion viscosityRegion = _viscosityRegionBuffer[i];
                if (viscosityRegion.Active == 0 || viscosityRegion.InvRadiusSq <= 0f || viscosityRegion.ViscosityMultiplier <= 0f)
                    continue;

                _activeViscosityRegions[viscosityWriteIndex++] = viscosityRegion;
            }

            for (int i = viscosityWriteIndex; i < MaxDynamicViscosityRegionCount; i++)
                _activeViscosityRegions[i] = default;

            _activeViscosityRegionCount = viscosityWriteIndex;
        }

        private void CopyActiveMaelstromsToNative()
        {
            int writeIndex = 0;
            float maxRadius = 0f;
            float maxIntensity = 0f;
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow whirlpool = _whirlpoolFlowBuffer[i];
                if (!IsValidWhirlpool(whirlpool))
                    continue;

                float radius = math.max(MaelstromMinimumRadiusMeters, whirlpool.Padding1);
                float intensityOverRadius = math.max(0f, whirlpool.Padding0);
                if (_activeMaelstroms.IsCreated && writeIndex < _activeMaelstroms.Length)
                {
                    _activeMaelstroms[writeIndex] = new float4(
                        whirlpool.CenterWS.x,
                        whirlpool.CenterWS.y,
                        whirlpool.CenterWS.z,
                        intensityOverRadius);
                }

                maxRadius = math.max(maxRadius, radius);
                maxIntensity = math.max(maxIntensity, intensityOverRadius * radius);
                writeIndex++;
            }

            if (_activeMaelstroms.IsCreated)
            {
                for (int i = writeIndex; i < _activeMaelstroms.Length; i++)
                    _activeMaelstroms[i] = default;
            }

            _activeMaelstromCount = writeIndex;
            _activeMaelstromMeta = new Vector4(
                writeIndex,
                maxRadius,
                maxIntensity,
                0f);
        }

        private static bool IsValidWhirlpool(WhirlpoolFlow whirlpool)
        {
            return whirlpool.Active != 0 &&
                   whirlpool.RadiusSq > 0f &&
                   whirlpool.InvRadiusSq > 0f &&
                   math.all(math.isfinite(whirlpool.CenterWS)) &&
                   math.isfinite(whirlpool.RadiusSq) &&
                   math.isfinite(whirlpool.InvRadiusSq) &&
                   math.isfinite(whirlpool.TangentialStrength) &&
                   math.isfinite(whirlpool.CentripetalStrength) &&
                   math.isfinite(whirlpool.VerticalPull) &&
                   math.isfinite(whirlpool.Padding0) &&
                   math.isfinite(whirlpool.Padding1);
        }

        private bool TryResolveStrongestWhirlpool(out WhirlpoolFlow strongest)
        {
            strongest = default;
            float strongestScore = -1f;
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow candidate = _whirlpoolFlowBuffer[i];
                if (!IsValidWhirlpool(candidate))
                    continue;

                float candidateScore = ResolveMaelstromIntensity(
                    candidate.TangentialStrength,
                    candidate.CentripetalStrength,
                    candidate.VerticalPull);
                if (candidateScore <= strongestScore)
                    continue;

                strongest = candidate;
                strongestScore = candidateScore;
            }

            return strongestScore >= 0f;
        }

        private static float SampleMaelstromWarp01(WhirlpoolFlow whirlpool, float3 sample)
        {
            if (!IsValidWhirlpool(whirlpool))
                return 0f;

            float3 toCenter = whirlpool.CenterWS - sample;
            toCenter.y = 0f;
            float distanceSq = math.lengthsq(toCenter);
            if (!math.isfinite(distanceSq) || distanceSq > whirlpool.RadiusSq)
                return 0f;

            float inside01 = 1f - math.saturate(distanceSq * whirlpool.InvRadiusSq);
            float intensity01 = math.saturate(whirlpool.Padding0 * 0.08f);
            return inside01 * intensity01;
        }

        private static float ResolveMaelstromIntensity(float tangentialStrength, float centripetalStrength, float verticalPull)
        {
            return math.max(
                math.abs(tangentialStrength),
                math.max(math.abs(centripetalStrength), math.max(0f, verticalPull)));
        }

        private void PublishMaelstromRuntimeSignals()
        {
            CopyActiveMaelstromsToNative();
            int activeCount = _activeMaelstromCount;
            if (activeCount <= 0)
            {
                WriteMaelstromTelemetry(default, default, 0f, 0u);
                return;
            }

            WhirlpoolFlow primary = ResolvePrimaryMaelstrom();
            float radius = math.max(MaelstromMinimumRadiusMeters, primary.Padding1);
            float eventHorizonRadius = math.max(0.25f, radius * MaelstromEventHorizonRadiusFactor);
            float intensity01 = math.saturate(ResolveMaelstromIntensity(
                primary.TangentialStrength,
                primary.CentripetalStrength,
                primary.VerticalPull) * 0.04f);
            float now = Time.fixedTime;

            if (now >= _nextMaelstromAudioTime)
            {
                Vector3 acousticRuntimePosition = new Vector3(
                    primary.CenterWS.x,
                    primary.CenterWS.y,
                    primary.CenterWS.z);
                if (TryResolveAupFromRuntimeOrigin(acousticRuntimePosition, out AbsoluteUniversePosition acousticAup))
                {
                    AcousticPingSignal acoustic = default;
                    acoustic.PositionAup = acousticAup;
                    acoustic.RadiusMeters = math.max(radius, radius * 2.5f);
                    acoustic.Intensity01 = math.max(0.2f, intensity01);
                    acoustic.SourceId = MaelstromSourceHash;
                    acoustic.Channel = MaelstromAcousticChannel;
                    acoustic.Flags = 1;
                    SignalBus<AcousticPingSignal>.TryPushTracked(in acoustic, ref s_x001HectonFluidEngineSignalPushDropCount);
                    _nextMaelstromAudioTime = now + MaelstromAudioIntervalSeconds;
                }
            }

            uint telemetryFlags = 0u;
            if (now >= _nextMaelstromDamageTime)
            {
                if (TryPublishMaelstromDamage(primary, eventHorizonRadius, intensity01))
                    telemetryFlags |= 1u;

                _nextMaelstromDamageTime = now + MaelstromDamageIntervalSeconds;
            }

            if (!math.all(math.isfinite(primary.CenterWS)) || !math.isfinite(radius))
                telemetryFlags |= 0x80000000u;

            WriteMaelstromTelemetry(primary, _activeMaelstroms.IsCreated ? _activeMaelstroms[0] : default, intensity01, telemetryFlags);
        }

        private WhirlpoolFlow ResolvePrimaryMaelstrom()
        {
            WhirlpoolFlow best = default;
            float bestIntensity = -1f;
            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
            {
                WhirlpoolFlow candidate = _whirlpoolFlowBuffer[i];
                if (!IsValidWhirlpool(candidate))
                    continue;

                float intensity = ResolveMaelstromIntensity(
                    candidate.TangentialStrength,
                    candidate.CentripetalStrength,
                    candidate.VerticalPull);
                if (intensity > bestIntensity)
                {
                    best = candidate;
                    bestIntensity = intensity;
                }
            }

            return best;
        }

        private bool TryPublishMaelstromDamage(WhirlpoolFlow whirlpool, float eventHorizonRadius, float intensity01)
        {
            if (whirlpool.Active == 0 || eventHorizonRadius <= 0f)
                return false;

            bool published = false;
            float eventHorizonRadiusSq = eventHorizonRadius * eventHorizonRadius;
            Vector3 center = new Vector3(whirlpool.CenterWS.x, whirlpool.CenterWS.y, whirlpool.CenterWS.z);

            IPlayerRuntimeContext player = TryGetCachedPlayerRuntime();
            Rigidbody playerBody = player != null ? player.PlayerRigidbody : null;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            Vector3 playerPosition = Vector3.positiveInfinity;
            if (player != null &&
                player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot playerPose) &&
                math.all(math.isfinite(playerPose.RuntimePosition)))
            {
                playerPosition = new Vector3(
                    playerPose.RuntimePosition.x,
                    playerPose.RuntimePosition.y,
                    playerPose.RuntimePosition.z);
            }
            else if (playerTransform != null)
            {
                playerPosition = playerTransform.position;
            }
            if (IsFiniteVector(playerPosition) && (playerPosition - center).sqrMagnitude <= eventHorizonRadiusSq)
                published |= PublishMaelstromDamageSignal(center, playerPosition, playerBody != null ? unchecked((uint)EntityId.ToULong(playerBody.GetEntityId())) : 0u, intensity01);

            ISubmarineRuntimeContext submarine = TryGetCachedSubmarineRuntime();
            Rigidbody hull = submarine != null ? submarine.HullRigidbody : null;
            if (hull != null)
            {
                Vector3 hullPosition = hull.worldCenterOfMass;
                if (IsFiniteVector(hullPosition) && (hullPosition - center).sqrMagnitude <= eventHorizonRadiusSq)
                    published |= PublishMaelstromDamageSignal(center, hullPosition, unchecked((uint)EntityId.ToULong(hull.GetEntityId())), intensity01);
            }

            return published;
        }

        private static bool PublishMaelstromDamageSignal(Vector3 center, Vector3 targetPosition, uint targetHash, float intensity01)
        {
            if (!IsFiniteVector(center) || !IsFiniteVector(targetPosition))
                return false;

            Vector3 direction = targetPosition - center;
            float directionSq = direction.sqrMagnitude;
            if (directionSq > 0.000001f)
                direction *= math.rsqrt(directionSq);
            else
                direction = Vector3.up;

            Hecton8.Core.Contracts.Signals.CombatDamageSignal damage = default;
            damage.ImpactAup = Hecton8.Core.Contracts.Signals.CombatDamageSignalCodec.FromRuntimePoint(center);
            damage.Direction = new float3(direction.x, direction.y, direction.z);
            damage.Magnitude = MaelstromDamageMagnitude * math.max(0.25f, math.saturate(intensity01));
            damage.DamageType = CombatDamageTypes.Pressure;
            damage.TargetHash = targetHash;
            damage.SourceHash = MaelstromSourceHash;
            damage.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            damage.SourceId = (ushort)(MaelstromSourceHash & 0xffffu);
            damage.TargetId = targetHash != 0u ? (ushort)math.min(targetHash, (uint)ushort.MaxValue) : (ushort)0;
            damage.Channel = MaelstromAcousticChannel;
            damage.Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag;
            damage.IntegrityDelta = 1;
            SignalBus<CombatDamageSignal>.TryPushTracked(in damage, ref s_x001HectonFluidEngineSignalPushDropCount);
            return true;
        }

        private void WriteMaelstromTelemetry(WhirlpoolFlow primary, float4 compactPrimary, float warp01, uint flags)
        {
            if (!_maelstromTelemetry.IsCreated)
                return;

            bool invalid =
                primary.Active != 0 &&
                (!math.all(math.isfinite(primary.CenterWS)) ||
                 !math.isfinite(primary.RadiusSq) ||
                 !math.isfinite(compactPrimary.w));
            if (invalid)
                flags |= 0x80000000u;

            int index = _maelstromTelemetryCursor;
            if ((uint)index >= (uint)_maelstromTelemetry.Length)
                index = 0;

            float radius = primary.Active != 0 ? math.max(0f, primary.Padding1) : 0f;
            float eventHorizonRadius = primary.Active != 0
                ? math.max(0.25f, radius * MaelstromEventHorizonRadiusFactor)
                : 0f;
            _maelstromTelemetry[index] = new MaelstromTelemetryEntry
            {
                Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                FixedTime = Time.fixedTime,
                PrimaryCenterWS = primary.CenterWS,
                PrimaryRadius = radius,
                PrimaryCompact = compactPrimary,
                Warp01 = math.saturate(warp01),
                ActiveCount = _activeMaelstromCount,
                Flags = flags,
                StateHash = BuildMaelstromTelemetryHash(primary, compactPrimary, flags),
                EscapeVelocityClamp = MaelstromMaxVelocityMetersPerSecond,
                EventHorizonRadius = eventHorizonRadius
            };
            _maelstromTelemetryCursor = (index + 1) % _maelstromTelemetry.Length;

            if (invalid)
                DumpMaelstromTelemetryOnce(flags);
        }

        private static uint BuildMaelstromTelemetryHash(WhirlpoolFlow primary, float4 compactPrimary, uint flags)
        {
            uint hash = 2166136261u;
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(primary.CenterWS.x));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(primary.CenterWS.y));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(primary.CenterWS.z));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(primary.Padding1));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(compactPrimary.w));
            hash = HashAbyssalFlowTelemetry(hash, flags);
            return hash;
        }

        private void DumpMaelstromTelemetryOnce(uint reasonFlags)
        {
            if (_maelstromTelemetryDumped || !_maelstromTelemetry.IsCreated)
                return;

            _maelstromTelemetryDumped = true;
            if (!BinaryFaultDumpsEnabled)
                return;
            try
            {
                int entryBytes = 64;
                int byteCount = 16 + _maelstromTelemetry.Length * entryBytes;
                NativeArray<byte> dump = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonFluidEngine),
                    "MaelstromTelemetryDumpPayload");
                try
                {
                    int cursor = 0;
                    WriteUInt32LittleEndian(dump, ref cursor, 0x4D41454Cu);
                    WriteInt32LittleEndian(dump, ref cursor, MaelstromTelemetryCapacity);
                    WriteInt32LittleEndian(dump, ref cursor, _maelstromTelemetryCursor);
                    WriteUInt32LittleEndian(dump, ref cursor, reasonFlags);
                    for (int i = 0; i < _maelstromTelemetry.Length; i++)
                    {
                        int index = (_maelstromTelemetryCursor + i) % _maelstromTelemetry.Length;
                        MaelstromTelemetryEntry entry = _maelstromTelemetry[index];
                        WriteInt32LittleEndian(dump, ref cursor, entry.Frame);
                        WriteFloatLittleEndian(dump, ref cursor, entry.FixedTime);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCenterWS.x);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCenterWS.y);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCenterWS.z);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryRadius);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCompact.x);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCompact.y);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCompact.z);
                        WriteFloatLittleEndian(dump, ref cursor, entry.PrimaryCompact.w);
                        WriteFloatLittleEndian(dump, ref cursor, entry.Warp01);
                        WriteInt32LittleEndian(dump, ref cursor, entry.ActiveCount);
                        WriteUInt32LittleEndian(dump, ref cursor, entry.Flags);
                        WriteUInt32LittleEndian(dump, ref cursor, entry.StateHash);
                        WriteFloatLittleEndian(dump, ref cursor, entry.EscapeVelocityClamp);
                        WriteFloatLittleEndian(dump, ref cursor, entry.EventHorizonRadius);
                    }

                    WriteNativeDump(ResolveFluidDumpPath(MaelstromDumpRelativePath), dump, cursor);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref dump,
                        nameof(HectonFluidEngine),
                        "MaelstromTelemetryDumpPayload");
                }
            }
            catch (System.Exception)
            {
            }
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            float3 numericValue = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(numericValue));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.isfinite(positionAup.LocalX) &&
                   math.isfinite(positionAup.LocalY) &&
                   math.isfinite(positionAup.LocalZ);
        }

        private static bool IsFiniteVector(Vector4 value)
        {
            float4 numericValue = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(numericValue));
        }

        private bool ContainsRegisteredObject(BuoyancyObject target)
        {
            int count = _objects.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_objects[i], target))
                    return true;
            }

            return false;
        }

        private static Vector3 NormalizeOrDefault(Vector3 value, Vector3 fallback)
        {
            float3 normalized = NormalizeOrDefault(
                new float3(value.x, value.y, value.z),
                new float3(fallback.x, fallback.y, fallback.z));
            return new Vector3(normalized.x, normalized.y, normalized.z);
        }

        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.isfinite(lengthSq) && lengthSq > 0.000001f;
            float3 safeValue = math.select(fallback, value, valid);
            float safeLengthSq = math.lengthsq(safeValue);
            return safeValue * math.rsqrt(math.max(safeLengthSq, 0.000001f));
        }

        private static Vector3 DominantAxisOrDefault(Vector3 value, Vector3 fallback)
        {
            float3 axis = DominantAxisOrDefault(new float3(value.x, value.y, value.z), new float3(fallback.x, fallback.y, fallback.z));
            return new Vector3(axis.x, axis.y, axis.z);
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

        private static bool TrySanitizePhysicsVector(float3 value, uint warningHash, out Vector3 sanitized)
        {
            if (math.any(math.isnan(value)) || math.any(math.isinf(value)) || !math.all(math.isfinite(value)))
            {
                ReportNonFinitePhysicsVector(warningHash);
                sanitized = Vector3.zero;
                return false;
            }

            sanitized = new Vector3(value.x, value.y, value.z);
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportNonFinitePhysicsVector(uint warningHash)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, HectonFluidEngineContextHash, 1f);
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private void ReleaseIdleNativeBuffersIfNeeded()
        {
            if (_objects.Count > 0 || _nativeCapacity <= 0)
                return;

            if (Application.isPlaying)
                return;

            DisposeNativeArrays(releaseAbyssalFlow: false, releaseGraphicsImmediately: false);
        }

        private WeatherRuntimeSnapshot ResolveWeatherSnapshot()
        {
            IWeatherService weatherService = _weatherService;
            if (weatherService == null || !weatherService.IsInitialized)
                return default;

            return weatherService.GetRuntimeSnapshot();
        }

        private float PublishCurrentWaterLevelUniform()
        {
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            return PublishCurrentWaterLevelUniform(in weatherSnapshot);
        }

        private float PublishCurrentWaterLevelUniform(in WeatherRuntimeSnapshot weatherSnapshot)
        {
            float waterLevelTimeSeconds = ResolveWaterLevelTimeSeconds(in weatherSnapshot);
            float cinematicWaterLevel = ResolveCinematicWaterLevelY(waterLevelTimeSeconds);
            _currentWeatherSnapshot = weatherSnapshot;
            _currentWaterLevelYSnapshot = cinematicWaterLevel;
            _currentWaterLevelTimeSecondsSnapshot = waterLevelTimeSeconds;
            _pendingCurrentWaterLevelY = cinematicWaterLevel;
            _currentWaterLevelYSnapshotValid = true;
            _currentWeatherSnapshotValid = true;
            _currentWaterLevelUniformDirty = true;
            return cinematicWaterLevel;
        }

        private void FlushCurrentWaterLevelUniform()
        {
            if (!_currentWaterLevelUniformDirty)
                return;

            _currentWaterLevelUniformDirty = false;
            float cinematicWaterLevel = _pendingCurrentWaterLevelY;
            if (UIStateStore.IsInitialized)
                UIStateStore.WriteValue(UIValueSlotId.WaterSurfaceY, cinematicWaterLevel, _currentWaterLevelTimeSecondsSnapshot);
            Shader.SetGlobalFloat(_CurrentWaterLevelId, cinematicWaterLevel);
            Shader.SetGlobalFloat(_CurrentWaterLevelYId, cinematicWaterLevel);
        }

        private float ResolveCinematicWaterLevelY(float waterLevelTimeSeconds)
        {
            return GlobalPhysicsStateManager.UpdateFrameCachedCurrentWaterLevelY(
                ResolveBaseWaterLevelY(),
                enableCinematicTideShift,
                cinematicTideAmplitudeMeters,
                waterLevelTimeSeconds);
        }

        private float ReadPublishedCurrentWaterLevelY()
        {
            return _currentWaterLevelYSnapshotValid ? _currentWaterLevelYSnapshot : ResolveBaseWaterLevelY();
        }

        private float ResolveBaseWaterLevelY()
        {
            return TryResolveOceanWaterLevelY(out float oceanWaterLevelY)
                ? oceanWaterLevelY
                : WorldWaterLevelCalibrationMath.ResolveFallbackWaterLevelY(waterLevel);
        }

        private bool TryResolveOceanWaterLevelY(out float waterLevelY)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveRuntimeWaterLevelY(oceanKinematics.SeaLevel, out waterLevelY))
            {
                return true;
            }

            waterLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            return false;
        }

        private static bool TryResolveRuntimeWaterLevelY(float candidateWaterLevelY, out float waterLevelY)
        {
            if (math.isfinite(candidateWaterLevelY) &&
                math.abs(candidateWaterLevelY) > 0.0001f &&
                math.abs(candidateWaterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevelY = candidateWaterLevelY;
                return true;
            }

            waterLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            return false;
        }

        private WeatherRuntimeSnapshot ReadPublishedWeatherSnapshot()
        {
            return _currentWeatherSnapshotValid ? _currentWeatherSnapshot : default;
        }

        private float ResolveWaterLevelTimeSeconds(in WeatherRuntimeSnapshot weatherSnapshot)
        {
            float syncedTime = weatherSnapshot.CurrentMeta.TimeAccumulator;
            return math.isfinite(syncedTime) && syncedTime > 0f
                ? syncedTime
                : ResolveFluidFallbackClockSeconds();
        }

        private static float ResolveFluidFallbackClockSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(FluidFallbackClockMaxSeconds, timeSeconds);
        }

        private void EnsureGpuBuoyancyBuffers(int capacity)
        {
            if (capacity <= 0)
                return;

            bool resultBuffersValid = _gpuBuoyancyResultBuffers != null &&
                                      _gpuBuoyancyResultBuffers.Length == GpuReadbackRingSize;
            if (resultBuffersValid)
            {
                for (int i = 0; i < _gpuBuoyancyResultBuffers.Length; i++)
                {
                    if (_gpuBuoyancyResultBuffers[i] == null || _gpuBuoyancyResultBuffers[i].count != capacity)
                    {
                        resultBuffersValid = false;
                        break;
                    }
                }
            }

            if (_gpuBuoyancyPositionBufferA == null ||
                _gpuBuoyancyPositionBufferA.count != capacity ||
                _gpuBuoyancyPositionBufferB == null ||
                _gpuBuoyancyPositionBufferB.count != capacity ||
                _gpuBuoyancyParamBufferA == null ||
                _gpuBuoyancyParamBufferA.count != capacity ||
                _gpuBuoyancyParamBufferB == null ||
                _gpuBuoyancyParamBufferB.count != capacity ||
                !resultBuffersValid)
            {
                ReleaseGpuBuoyancyBuffers();
                _gpuBuoyancyPositionBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float3>(capacity);
                _gpuBuoyancyPositionBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float3>(capacity);
                _gpuBuoyancyParamBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuBuoyancyObjectData>(capacity);
                _gpuBuoyancyParamBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuBuoyancyObjectData>(capacity);
                _gpuBuoyancyResultBuffers = new GraphicsBuffer[GpuReadbackRingSize]; // COLD ALLOC: GraphicsBuffer ring slots - async GPU buoyancy readback ownership - owner: HectonFluidEngine
                for (int i = 0; i < _gpuBuoyancyResultBuffers.Length; i++)
                    _gpuBuoyancyResultBuffers[i] = GraphicsBufferUploadUtility.CreateStructuredBuffer<float4>(capacity);
                _gpuBuoyancyUploadBufferIndex = 0;
            }
        }

        private void EnsureGpuBuoyancyBuffersColdIfEnabled(int capacity)
        {
            if (!enableGpuBuoyancySampling ||
                !_coldSupportsComputeShaders ||
                gpuBuoyancyCompute == null ||
                _gpuBuoyancyKernel < 0 ||
                capacity < gpuBuoyancyActivationThreshold)
            {
                return;
            }

            EnsureGpuBuoyancyBuffers(capacity);
            EnsureGpuReadbackDataCold(capacity);
        }

        private bool HasGpuBuoyancyBuffers(int capacity)
        {
            if (capacity <= 0 ||
                _gpuBuoyancyPositionBufferA == null ||
                !_gpuBuoyancyPositionBufferA.IsValid() ||
                _gpuBuoyancyPositionBufferA.count != capacity ||
                _gpuBuoyancyPositionBufferB == null ||
                !_gpuBuoyancyPositionBufferB.IsValid() ||
                _gpuBuoyancyPositionBufferB.count != capacity ||
                _gpuBuoyancyParamBufferA == null ||
                !_gpuBuoyancyParamBufferA.IsValid() ||
                _gpuBuoyancyParamBufferA.count != capacity ||
                _gpuBuoyancyParamBufferB == null ||
                !_gpuBuoyancyParamBufferB.IsValid() ||
                _gpuBuoyancyParamBufferB.count != capacity ||
                _gpuBuoyancyResultBuffers == null ||
                _gpuBuoyancyResultBuffers.Length != GpuReadbackRingSize)
            {
                return false;
            }

            for (int i = 0; i < _gpuBuoyancyResultBuffers.Length; i++)
            {
                GraphicsBuffer buffer = _gpuBuoyancyResultBuffers[i];
                if (buffer == null || !buffer.IsValid() || buffer.count != capacity)
                    return false;
            }

            return true;
        }

        private void EnsureGpuReadbackDataCold(int count)
        {
            if (_gpuReadbackData == null)
                _gpuReadbackData = new GpuReadbackNativeRing(GpuReadbackRingSize);

            int requiredCount = ResolveGpuReadbackElementCount(count);
            _gpuReadbackData.EnsureAllCold(requiredCount);
        }

        private bool HasGpuReadbackData(int slot, int count)
        {
            return _gpuReadbackData != null &&
                   _gpuReadbackData.IsReady(slot, ResolveGpuReadbackElementCount(count));
        }

        private static int ResolveGpuReadbackElementCount(int count)
        {
            return Mathf.NextPowerOfTwo(math.max(1, count));
        }

        private void ReleaseGpuBuoyancyBuffers()
        {
            CompletePendingGpuBuoyancyReadbacksForRelease();
            DisposeGpuReadbackData();
            ReleaseGraphicsBuffer(ref _gpuBuoyancyPositionBufferA);
            ReleaseGraphicsBuffer(ref _gpuBuoyancyPositionBufferB);
            ReleaseGraphicsBuffer(ref _gpuBuoyancyParamBufferA);
            ReleaseGraphicsBuffer(ref _gpuBuoyancyParamBufferB);

            if (_gpuBuoyancyResultBuffers != null)
            {
                for (int i = 0; i < _gpuBuoyancyResultBuffers.Length; i++)
                    ReleaseGraphicsBuffer(ref _gpuBuoyancyResultBuffers[i]);
                _gpuBuoyancyResultBuffers = null;
            }

            _gpuBuoyancyUploadBufferIndex = 0;
        }

        private void EnsureGpuAbyssalFlowBuffers()
        {
            EnsureAbyssalFlowNativeState();

            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return;

            if (_gpuAbyssalFlowResultBuffer == null ||
                !_gpuAbyssalFlowResultBuffer.IsValid() ||
                _gpuAbyssalFlowResultBuffer.count != nodeCount ||
                _gpuAbyssalHeatSourceBufferA == null ||
                !_gpuAbyssalHeatSourceBufferA.IsValid() ||
                _gpuAbyssalHeatSourceBufferB == null ||
                !_gpuAbyssalHeatSourceBufferB.IsValid())
            {
                ReleaseGpuAbyssalFlowBuffers();
                _gpuAbyssalFlowResultBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<float4>(nodeCount); // COLD ALLOC: GraphicsBuffer[nodeCount] - GPU-write abyssal flow-vector field storage - owner: HectonFluidEngine
                _gpuAbyssalHeatSourceBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuHeatSourceData>(MaxAbyssalHeatSourceCount); // COLD ALLOC: GraphicsBuffer[8] - inferred hydrothermal heat-source upload staging A - owner: HectonFluidEngine
                _gpuAbyssalHeatSourceBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuHeatSourceData>(MaxAbyssalHeatSourceCount); // COLD ALLOC: GraphicsBuffer[8] - inferred hydrothermal heat-source upload staging B - owner: HectonFluidEngine
                _activeGpuAbyssalHeatSourceBuffer = _gpuAbyssalHeatSourceBufferA;
                _gpuAbyssalHeatSourceUploadIndex = 1;
            }

            if (_gpuAbyssalFlowTextureA == null || _gpuAbyssalFlowTextureB == null)
            {
                ReleaseAbyssalFlowTextures();
                _gpuAbyssalFlowTextureA = CreateAbyssalFlowTexture("__HectonAbyssalFlowFieldA");
                _gpuAbyssalFlowTextureB = CreateAbyssalFlowTexture("__HectonAbyssalFlowFieldB");
                _gpuAbyssalFlowReadTexture = _gpuAbyssalFlowTextureA;
                _gpuAbyssalFlowWriteTexture = _gpuAbyssalFlowTextureB;
            }
            else
            {
                if (!_gpuAbyssalFlowTextureA.IsCreated())
                {
                    _gpuAbyssalFlowTextureA.Create();
                    _hasAbyssalFlowTexture = false;
                }

                if (!_gpuAbyssalFlowTextureB.IsCreated())
                {
                    _gpuAbyssalFlowTextureB.Create();
                    _hasAbyssalFlowTexture = false;
                }

                if (_gpuAbyssalFlowReadTexture == null || _gpuAbyssalFlowWriteTexture == null)
                {
                    _gpuAbyssalFlowReadTexture = _gpuAbyssalFlowTextureA;
                    _gpuAbyssalFlowWriteTexture = _gpuAbyssalFlowTextureB;
                    _hasAbyssalFlowTexture = false;
                }
            }

            EnsureAbyssalFlowTextureHandles();

            _lastAbyssalFlowTextureSpacing = new Vector4(
                AbyssalFlowTextureCellSizeMeters,
                AbyssalFlowTextureCellSizeMeters,
                AbyssalFlowTextureWorldSizeMeters,
                math.rcp(AbyssalFlowTextureWorldSizeMeters));
        }

        private void EnsureGpuAbyssalFlowBuffersColdIfEnabled()
        {
            if (!enableGpuAbyssalFlowField ||
                !_coldSupportsComputeShaders ||
                abyssalFlowFieldCompute == null ||
                _gpuAbyssalUpdateKernel < 0 ||
                _gpuAbyssalTextureUpdateKernel < 0 ||
                _gpuAbyssalWakeKernel < 0 ||
                _gpuAbyssalVortexKernel < 0)
            {
                return;
            }

            EnsureGpuAbyssalFlowBuffers();
        }

        private bool HasAbyssalFlowNativeState()
        {
            return _gpuAbyssalHeatSourceUpload.IsCreated &&
                   _gpuAbyssalHeatSourceUpload.Length >= MaxAbyssalHeatSourceCount &&
                   _abyssalFlowTelemetry.IsCreated &&
                   _abyssalFlowTelemetry.Length >= AbyssalFlowTelemetryCapacity &&
                   _activeWhirlpools.IsCreated &&
                   _activeWhirlpools.Length >= MaxAnalyticalWhirlpoolCount &&
                   _activeMaelstroms.IsCreated &&
                   _activeMaelstroms.Length >= MaxActiveMaelstromCount &&
                   _maelstromTelemetry.IsCreated &&
                   _maelstromTelemetry.Length >= MaelstromTelemetryCapacity;
        }

        private bool HasGpuAbyssalFlowBuffers()
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            return nodeCount > 0 &&
                   HasAbyssalFlowNativeState() &&
                   _gpuAbyssalFlowResultBuffer != null &&
                   _gpuAbyssalFlowResultBuffer.IsValid() &&
                   _gpuAbyssalFlowResultBuffer.count == nodeCount &&
                   _gpuAbyssalHeatSourceBufferA != null &&
                   _gpuAbyssalHeatSourceBufferA.IsValid() &&
                   _gpuAbyssalHeatSourceBufferB != null &&
                   _gpuAbyssalHeatSourceBufferB.IsValid() &&
                   _activeGpuAbyssalHeatSourceBuffer != null &&
                   _activeGpuAbyssalHeatSourceBuffer.IsValid() &&
                   _gpuAbyssalFlowTextureA != null &&
                   _gpuAbyssalFlowTextureA.IsCreated() &&
                   _gpuAbyssalFlowTextureB != null &&
                   _gpuAbyssalFlowTextureB.IsCreated() &&
                   _gpuAbyssalFlowReadTexture != null &&
                   _gpuAbyssalFlowWriteTexture != null &&
                   _gpuAbyssalFlowTextureAHandle != null &&
                   _gpuAbyssalFlowTextureBHandle != null;
        }

        private bool EnsureSplashdownImpulseState(bool allowAllocate = true)
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return false;

            if (_splashdownImpulseUpload.IsCreated && _splashdownImpulseUpload.Length != nodeCount)
            {
                if (!allowAllocate)
                    return false;

                DisposeSplashdownImpulseState();
            }

            if (!_splashdownImpulseUpload.IsCreated)
            {
                if (!allowAllocate)
                    return false;

                _splashdownImpulseUpload.Ensure(
                    FluidSplashdownImpulseUploadBufferId,
                    nodeCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_splashdownImpulseStats.IsCreated)
            {
                if (!allowAllocate)
                    return false;

                _splashdownImpulseStats.Ensure(
                    FluidSplashdownImpulseStatsBufferId,
                    2,
                    NativeArrayOptions.ClearMemory);
            }

            return _splashdownImpulseUpload.IsCreated &&
                   _splashdownImpulseUpload.Length >= nodeCount &&
                   _splashdownImpulseStats.IsCreated &&
                   _splashdownImpulseStats.Length >= 2;
        }

        private bool HasSplashdownImpulseState()
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            return nodeCount > 0 &&
                   _splashdownImpulseUpload.IsCreated &&
                   _splashdownImpulseUpload.Length >= nodeCount &&
                   _splashdownImpulseStats.IsCreated &&
                   _splashdownImpulseStats.Length >= 2;
        }

        private void EnsureAbyssalFlowTextureHandles()
        {
            if (_gpuAbyssalFlowTextureA != null && _gpuAbyssalFlowTextureAHandle == null)
                _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA);

            if (_gpuAbyssalFlowTextureB != null && _gpuAbyssalFlowTextureBHandle == null)
                _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB);
        }

        private bool EnsureSplashdownImpulseGpuBuffer(bool allowAllocate = true)
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return false;

            if (_gpuSplashdownImpulseBufferA != null &&
                _gpuSplashdownImpulseBufferA.IsValid() &&
                _gpuSplashdownImpulseBufferA.count == nodeCount &&
                _gpuSplashdownImpulseBufferB != null &&
                _gpuSplashdownImpulseBufferB.IsValid() &&
                _gpuSplashdownImpulseBufferB.count == nodeCount)
            {
                if (_activeGpuSplashdownImpulseBuffer == null)
                    _activeGpuSplashdownImpulseBuffer = _gpuSplashdownImpulseBufferA;
                return true;
            }

            if (!allowAllocate)
                return false;

            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferA);
            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferB);
            _gpuSplashdownImpulseBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(nodeCount); // COLD ALLOC: GraphicsBuffer[nodeCount] - splashdown vector-field override A prewarmed before event upload - owner: HectonFluidEngine
            _gpuSplashdownImpulseBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(nodeCount); // COLD ALLOC: GraphicsBuffer[nodeCount] - splashdown vector-field override B prewarmed before event upload - owner: HectonFluidEngine
            _activeGpuSplashdownImpulseBuffer = _gpuSplashdownImpulseBufferA;
            _gpuSplashdownImpulseUploadIndex = 1;
            _splashdownImpulseUploaded = false;
            return _gpuSplashdownImpulseBufferA != null &&
                   _gpuSplashdownImpulseBufferA.IsValid() &&
                   _gpuSplashdownImpulseBufferB != null &&
                   _gpuSplashdownImpulseBufferB.IsValid();
        }

        private bool HasSplashdownImpulseGpuBuffer()
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return false;

            if (_gpuSplashdownImpulseBufferA != null &&
                _gpuSplashdownImpulseBufferA.IsValid() &&
                _gpuSplashdownImpulseBufferA.count == nodeCount &&
                _gpuSplashdownImpulseBufferB != null &&
                _gpuSplashdownImpulseBufferB.IsValid() &&
                _gpuSplashdownImpulseBufferB.count == nodeCount)
            {
                if (_activeGpuSplashdownImpulseBuffer == null)
                    _activeGpuSplashdownImpulseBuffer = _gpuSplashdownImpulseBufferA;
                return true;
            }

            return false;
        }

        private static RenderTexture CreateAbyssalFlowTexture(string textureName)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureResolution)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = AbyssalFlowTextureResolution,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[32x32x32 R16G16B16A16_SFloat] - persistent GPU abyssal flow volume - owner: HectonFluidEngine
            texture.Create();
            return texture;
        }

        private void ReleaseGpuAbyssalFlowBuffers()
        {
            if (_gpuAbyssalFlowResultBuffer != null)
            {
                _gpuAbyssalFlowResultBuffer.Release();
                _gpuAbyssalFlowResultBuffer = null;
            }

            ReleaseGraphicsBuffer(ref _gpuAbyssalHeatSourceBufferA);
            ReleaseGraphicsBuffer(ref _gpuAbyssalHeatSourceBufferB);
            _activeGpuAbyssalHeatSourceBuffer = null;
            _gpuAbyssalHeatSourceUploadIndex = 0;

            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferA);
            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferB);
            _activeGpuSplashdownImpulseBuffer = null;
            _gpuSplashdownImpulseUploadIndex = 0;
            _splashdownImpulseUploaded = false;
            ReleaseAbyssalFlowTextures();
            DeactivateAbyssalFlowPublication();
        }

        private void DisposeFluidAdvectionState()
        {
            ReleaseGraphicsBuffer(ref _advectedSiltBufferA);
            ReleaseGraphicsBuffer(ref _advectedSiltBufferB);
            ReleaseGraphicsBuffer(ref _advectedBubbleBufferA);
            ReleaseGraphicsBuffer(ref _advectedBubbleBufferB);
            ReleaseGraphicsBuffer(ref _advectedDebrisBufferA);
            ReleaseGraphicsBuffer(ref _advectedDebrisBufferB);
            ReleaseGraphicsBuffer(ref _emptyAdvectedSiltBuffer);
            ReleaseGraphicsBuffer(ref _emptyAdvectedBubbleBuffer);
            ReleaseGraphicsBuffer(ref _emptyAdvectedDebrisBuffer);
            ReleaseGraphicsBuffer(ref _emptyAbyssalFlowBuffer);
            ReleaseGraphicsBuffer(ref _dynamicWakeBufferA);
            ReleaseGraphicsBuffer(ref _dynamicWakeBufferB);
            ReleaseGraphicsBuffer(ref _dynamicWakeVectorBufferA);
            ReleaseGraphicsBuffer(ref _dynamicWakeVectorBufferB);
            _activeDynamicWakeBuffer = null;
            _activeDynamicWakeVectorBuffer = null;
            _activeDynamicWakeParams = Vector4.zero;
            _dynamicWakeUploadBufferIndex = 0;
            _dynamicWakeBufferHandle = default;
            _dynamicWakeVectorBufferHandle = default;

            _advectedSiltUpload.Release();
            _advectedBubbleUpload.Release();
            _advectedDebrisUpload.Release();
            _advectedSiltDirtyPages.Release();
            _advectedBubbleDirtyPages.Release();
            _advectedDebrisDirtyPages.Release();
            _advectedSiltDirtyPageUploadSnapshot = null;
            _advectedBubbleDirtyPageUploadSnapshot = null;
            _advectedDebrisDirtyPageUploadSnapshot = null;
            _emptyAbyssalFlowUpload.Release();
            _fluidAdvectionTelemetry.Release();

            ReleaseRTHandle(ref _cachedFluidAdvectionFlowHandle);
            ReleaseRTHandle(ref _cachedFluidAdvectionSdfHandle);
            _cachedFluidAdvectionFlowHandleSource = null;
            _cachedFluidAdvectionSdfHandleSource = null;
            ReleaseRTHandle(ref _emptyFluidAdvectionTextureHandle);

            _emptyFluidAdvectionTexture = null;

            _activeAdvectedSiltCount = 0;
            _activeAdvectedBubbleCount = 0;
            _activeAdvectedDebrisCount = 0;
            _advectedBubbleWriteCursor = 0;
            _advectedDebrisWriteCursor = 0;
            _fluidAdvectionTelemetryCursor = 0;
            _lastFluidAdvectionTelemetryFrame = -1;
            _lastProcessedFluidAdvectionAupShiftFrameId = 0u;
            _pendingFluidAdvectionRuntimeShift = default;
            _fluidAdvectionStateReady = false;
            _fluidAdvectionRenderGraphQueued = false;
            _advectedSiltGpuUploadDirty = false;
            _advectedBubbleGpuUploadDirty = false;
            _advectedDebrisGpuUploadDirty = false;
            _fluidAdvectionTelemetryDumped = false;
        }

        private void DisposeSplashdownImpulseState(bool releaseGraphicsImmediately = true)
        {
            _splashdownImpulseUpload.Release();
            _splashdownImpulseStats.Release();
            if (releaseGraphicsImmediately)
            {
                ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferA);
                ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferB);
                _activeGpuSplashdownImpulseBuffer = null;
                _gpuSplashdownImpulseUploadIndex = 0;
            }
            else
                QueueFluidGraphicsRelease(FluidGraphicsReleaseSplashdownImpulse);
            _splashdownImpulseJobHandle = default;
            _splashdownImpulseJobActive = false;
            _splashdownImpulseUploaded = false;
            _splashdownImpulseScheduleFrame = -1;
            _splashdownImpulsePositionWS = default;
            _splashdownImpulseRemainingSeconds = 0f;
            _splashdownImpulseDurationSeconds = 0f;
            _lastSplashdownFluidImpulseCount = 0;
            _lastProcessedSplashdownSequence = 0;
            _lastProcessedSplashdownFrame = 0;
            _lastProcessedSplashdownSourceHash = 0;
            _splashdownImpactConsumed = false;
            _splashdownImpulseFlags = 0u;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ReleaseAbyssalFlowTextures()
        {
            ReleaseRTHandle(ref _gpuAbyssalFlowTextureAHandle);
            ReleaseRTHandle(ref _gpuAbyssalFlowTextureBHandle);
            ReleaseRenderTexture(ref _gpuAbyssalFlowTextureA);
            ReleaseRenderTexture(ref _gpuAbyssalFlowTextureB);
            _gpuAbyssalFlowReadTexture = null;
            _gpuAbyssalFlowWriteTexture = null;
            _hasAbyssalFlowTexture = false;
        }

        private static void ReleaseRTHandle(ref RTHandle handle)
        {
            if (handle == null)
                return;

            handle.Release();
            handle = null;
        }

        private void DeactivateAbyssalFlowPublication()
        {
            bool wasPublished =
                !_abyssalFlowPublicationClearIssued ||
                _hasAbyssalFlowTexture ||
                _lastAbyssalGridResolution.w > 0f ||
                _lastAbyssalFlowSpacing.w > 0f ||
                _lastAbyssalFlowTextureSpacing.w > 0f;
            if (!wasPublished)
                return;

            _lastAbyssalGridResolution = Vector4.zero;
            _lastAbyssalFlowCenter = Vector4.zero;
            _lastAbyssalFlowSpacing = Vector4.zero;
            _lastAbyssalFlowTextureSpacing = Vector4.zero;
            _hasAbyssalFlowTexture = false;
            QueueAbyssalFlowGlobalClear();
            _abyssalFlowPublicationClearIssued = true;
        }

        private void QueueAbyssalFlowVisualSync(
            in WeatherRuntimeSnapshot weatherSnapshot,
            float resolvedWaterLevel,
            float fixedDeltaTime)
        {
            _pendingAbyssalFlowWeatherSnapshot = weatherSnapshot;
            _pendingAbyssalFlowWaterLevel = resolvedWaterLevel;
            _pendingAbyssalFlowDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            _abyssalFlowVisualDirty = true;
        }

        private void FlushAbyssalFlowVisualSync()
        {
            if (!_abyssalFlowVisualDirty)
                return;

            _abyssalFlowVisualDirty = false;
            TryDispatchGpuAbyssalFlowField(
                in _pendingAbyssalFlowWeatherSnapshot,
                _pendingAbyssalFlowWaterLevel,
                _pendingAbyssalFlowDeltaTime);
        }

        private void QueueAbyssalFlowGlobalPublication(
            GraphicsBuffer resultBuffer,
            Texture flowTexture,
            Vector4 gridResolution,
            Vector4 flowCenter,
            Vector4 flowSpacing,
            Vector4 textureParams)
        {
            _pendingAbyssalFlowResultBuffer = resultBuffer;
            _pendingAbyssalFlowTexture = flowTexture;
            _pendingAbyssalGridResolution = gridResolution;
            _pendingAbyssalFlowCenter = flowCenter;
            _pendingAbyssalFlowSpacing = flowSpacing;
            _pendingAbyssalFlowTextureParams = textureParams;
            _abyssalFlowGlobalsDirty = true;
            _abyssalFlowGlobalsClearDirty = false;
        }

        private void QueueAbyssalFlowGlobalClear()
        {
            _pendingAbyssalFlowResultBuffer = null;
            _pendingAbyssalFlowTexture = null;
            _pendingAbyssalGridResolution = Vector4.zero;
            _pendingAbyssalFlowCenter = Vector4.zero;
            _pendingAbyssalFlowSpacing = Vector4.zero;
            _pendingAbyssalFlowTextureParams = Vector4.zero;
            _abyssalFlowGlobalsDirty = false;
            _abyssalFlowGlobalsClearDirty = true;
        }

        private void FlushAbyssalFlowGlobalPublication()
        {
            if (_abyssalFlowGlobalsClearDirty)
            {
                _abyssalFlowGlobalsClearDirty = false;
                Shader.SetGlobalFloat(_AbyssalFlowTextureActiveId, 0f);
                Shader.SetGlobalTexture(_AbyssalFlowFieldTextureId, null);
                Shader.SetGlobalVector(_AbyssalGridResolutionId, Vector4.zero);
                Shader.SetGlobalVector(_AbyssalFlowCenterId, Vector4.zero);
                Shader.SetGlobalVector(_AbyssalFlowSpacingId, Vector4.zero);
                Shader.SetGlobalVector(_AbyssalFlowTextureParamsId, Vector4.zero);
                return;
            }

            if (!_abyssalFlowGlobalsDirty)
                return;

            _abyssalFlowGlobalsDirty = false;
            Shader.SetGlobalBuffer(_AbyssalFlowFieldResultId, _pendingAbyssalFlowResultBuffer);
            Shader.SetGlobalTexture(_AbyssalFlowFieldTextureId, _pendingAbyssalFlowTexture);
            Shader.SetGlobalVector(_AbyssalGridResolutionId, _pendingAbyssalGridResolution);
            Shader.SetGlobalVector(_AbyssalFlowCenterId, _pendingAbyssalFlowCenter);
            Shader.SetGlobalVector(_AbyssalFlowSpacingId, _pendingAbyssalFlowSpacing);
            Shader.SetGlobalVector(_AbyssalFlowTextureParamsId, _pendingAbyssalFlowTextureParams);
            Shader.SetGlobalFloat(_AbyssalFlowTextureActiveId, 1f);
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        private void SwapAbyssalFlowTextures()
        {
            RenderTexture temp = _gpuAbyssalFlowReadTexture;
            _gpuAbyssalFlowReadTexture = _gpuAbyssalFlowWriteTexture;
            _gpuAbyssalFlowWriteTexture = temp;
        }

        private void ResolveAbyssalFlowBucketUniforms(out int updateBucket, out int updateBucketMask)
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer != null && bucketer.IsInitialized)
            {
                updateBucketMask = bucketer.FastBucketMask;
                updateBucket = bucketer.ActiveFastBucket;
                _gpuAbyssalFlowInterpolationAlpha = math.saturate(bucketer.SimulationBucketInterpolationAlpha);
                return;
            }

            int frameCount = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            updateBucketMask = AbyssalFlowUpdateBucketMask;
            updateBucket = frameCount & updateBucketMask;
            _gpuAbyssalFlowInterpolationAlpha = (updateBucket + 1) * AbyssalFlowUpdateBucketInvCount;
        }

        private static bool IsAbyssalFlowKillSwitchActive()
        {
            RefreshSystemKillSwitchBitsSnapshot();
            return (_systemKillSwitchMaskSnapshot & AbyssalFlowKillSwitchMask) != 0u;
        }

        private static void RefreshSystemKillSwitchBitsSnapshot()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_systemKillSwitchSnapshotFrame == frame)
                return;

            _systemKillSwitchSnapshotFrame = frame;
            System.ReadOnlySpan<SystemKillSwitchBitsSignal> signals = SignalBus<SystemKillSwitchBitsSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                _systemKillSwitchMaskSnapshot = signals[i].CurrentMask;
        }

        private void TryDispatchGpuAbyssalFlowField(
            in WeatherRuntimeSnapshot weatherSnapshot,
            float resolvedWaterLevel,
            float fixedDeltaTime)
        {
            if (!enableGpuAbyssalFlowField ||
                !_coldSupportsComputeShaders ||
                abyssalFlowFieldCompute == null ||
                _gpuAbyssalUpdateKernel < 0 ||
                _gpuAbyssalTextureUpdateKernel < 0 ||
                _gpuAbyssalWakeKernel < 0 ||
                _gpuAbyssalVortexKernel < 0 ||
                lodObserver == null)
            {
                AgeAbyssalVortexImpulsesOnce(fixedDeltaTime);
                DeactivateAbyssalFlowPublication();
                return;
            }

            float currentFixedTime = Time.fixedTime;
            if (math.abs(_lastAbyssalFlowDispatchFixedTime - currentFixedTime) <= 0.000001f)
                return;

            if (IsAbyssalFlowKillSwitchActive())
            {
                _gpuAbyssalFlowInterpolationAlpha = 1f;
                AgeAbyssalVortexImpulsesOnce(fixedDeltaTime);
                return;
            }

            if (!HasGpuAbyssalFlowBuffers())
            {
                AgeAbyssalVortexImpulsesOnce(fixedDeltaTime);
                DeactivateAbyssalFlowPublication();
                return;
            }

            _lastAbyssalFlowDispatchFixedTime = currentFixedTime;
            long watchdogStart = System.Diagnostics.Stopwatch.GetTimestamp();

            float3 flowCenter = ResolveAbyssalFlowCenter(resolvedWaterLevel);
            float flowTextureDetail01 = SmoothFluidAdvectionQuality(ResolveAbyssalVisualQualityWeight());
            int heatSourceCount = CaptureAbyssalHeatSources(flowCenter, flowTextureDetail01);
            _debugAbyssalHeatSourceCount = heatSourceCount;

            if (heatSourceCount > 0)
            {
                GraphicsBuffer heatSourceWriteBuffer = (_gpuAbyssalHeatSourceUploadIndex & 1) == 0
                    ? _gpuAbyssalHeatSourceBufferA
                    : _gpuAbyssalHeatSourceBufferB;
                if (heatSourceWriteBuffer != null && heatSourceWriteBuffer.IsValid())
                {
                    GraphicsBufferUploadUtility.UploadNativeArray<GpuHeatSourceData>(heatSourceWriteBuffer, _gpuAbyssalHeatSourceUpload, heatSourceCount);
                    _activeGpuAbyssalHeatSourceBuffer = heatSourceWriteBuffer;
                    _gpuAbyssalHeatSourceUploadIndex ^= 1;
                }
            }

            int nodeCount = GetAbyssalFlowNodeCount();
            int groupCount = CeilDividePositive(nodeCount, _gpuAbyssalUpdateThreadGroupSizeX);
            int textureGroupCountX = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalTextureThreadGroupSizeX);
            int textureGroupCountY = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalTextureThreadGroupSizeY);
            int textureGroupCountZ = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalTextureThreadGroupSizeZ);
            int wakeGroupCountX = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalWakeThreadGroupSizeX);
            int wakeGroupCountY = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalWakeThreadGroupSizeY);
            int wakeGroupCountZ = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalWakeThreadGroupSizeZ);
            int vortexGroupCountX = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalVortexThreadGroupSizeX);
            int vortexGroupCountY = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalVortexThreadGroupSizeY);
            int vortexGroupCountZ = CeilDividePositive(AbyssalFlowTextureResolution, _gpuAbyssalVortexThreadGroupSizeZ);
            if (groupCount <= 0 ||
                textureGroupCountX <= 0 ||
                textureGroupCountY <= 0 ||
                textureGroupCountZ <= 0 ||
                wakeGroupCountX <= 0 ||
                wakeGroupCountY <= 0 ||
                wakeGroupCountZ <= 0 ||
                vortexGroupCountX <= 0 ||
                vortexGroupCountY <= 0 ||
                vortexGroupCountZ <= 0)
            {
                return;
            }
            ResolveAbyssalFlowBucketUniforms(out int updateBucket, out int updateBucketMask);
            GraphicsBuffer splashdownImpulseBuffer = ResolveSplashdownImpulseBuffer();
            Vector4 splashdownParams = ResolveSplashdownImpulseParams();

            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalFlowFieldResultId, _gpuAbyssalFlowResultBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalHeatSourcesId, _activeGpuAbyssalHeatSourceBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalSplashdownImpulseBufferId, splashdownImpulseBuffer);

            float3 resolvedWeatherCurrent =
                weatherSnapshot.CurrentMeta.GlobalBaseVector * weatherSnapshot.CurrentMeta.GlobalScale +
                ResolveGiantWakeCurrentForDepth(flowCenter.y);
            Vector4 weatherCurrentVector = new Vector4(
                resolvedWeatherCurrent.x,
                resolvedWeatherCurrent.y,
                resolvedWeatherCurrent.z,
                weatherSnapshot.WeatherIntensity);
            Vector4 weatherWindVector = new Vector4(
                weatherSnapshot.GlobalWindVector.x,
                weatherSnapshot.GlobalWindVector.y,
                weatherSnapshot.GlobalWindVector.z,
                0f);
            Vector4 gridResolution = new Vector4(
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureResolution,
                nodeCount);
            Vector4 flowCenterVector = new Vector4(flowCenter.x, flowCenter.y, flowCenter.z, 0f);
            Vector4 flowSpacingVector = new Vector4(
                AbyssalFlowTextureCellSizeMeters,
                AbyssalFlowTextureCellSizeMeters,
                AbyssalFlowTextureWorldSizeMeters,
                math.rcp(AbyssalFlowTextureWorldSizeMeters));
            Vector4 textureSpacingVector = new Vector4(
                AbyssalFlowTextureCellSizeMeters,
                AbyssalFlowTextureCellSizeMeters,
                AbyssalFlowTextureWorldSizeMeters,
                math.rcp(AbyssalFlowTextureWorldSizeMeters));
            float resolvedWaveHeight = math.max(
                0f,
                math.max(0f, weatherSnapshot.Wave0.Amplitude) +
                math.max(0f, weatherSnapshot.Wave1.Amplitude) +
                math.max(0f, weatherSnapshot.Wave2.Amplitude));
            double3 aupOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Vector4 weatherParams = new Vector4(
                weatherSnapshot.CurrentMeta.ThermalIntensity,
                ApproximateMagnitude(weatherSnapshot.GlobalWindVector),
                resolvedWaveHeight,
                weatherSnapshot.CurrentMeta.TimeAccumulator);
            bool flowTextureInitialized = _hasAbyssalFlowTexture;
            Vector4 textureParams = new Vector4(
                AbyssalFlowTextureResolution,
                AbyssalFlowTextureWorldSizeMeters,
                flowTextureInitialized ? math.max(0f, fixedDeltaTime) : 1f,
                flowTextureDetail01);
            Vector4 noiseOffset = new Vector4(
                (float)aupOffset.x,
                (float)aupOffset.y,
                (float)aupOffset.z,
                _lastOriginShiftSequence);

            abyssalFlowFieldCompute.SetVector(_AbyssalGridResolutionId, gridResolution);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowCenterId, flowCenterVector);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowSpacingId, flowSpacingVector);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowWeatherCurrentId, weatherCurrentVector);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowWeatherWindId, weatherWindVector);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowWeatherParamsId, weatherParams);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowTextureParamsId, textureParams);
            abyssalFlowFieldCompute.SetVector(_AbyssalFlowNoiseOffsetId, noiseOffset);
            abyssalFlowFieldCompute.SetVector(_AbyssalSplashdownParamsId, splashdownParams);
            abyssalFlowFieldCompute.SetFloat(_AbyssalFlowSurfaceYId, resolvedWaterLevel);
            abyssalFlowFieldCompute.SetFloat(_AbyssalFlowThermoclineYId, resolvedWaterLevel - AbyssalFlowThermoclineDepthMeters);
            abyssalFlowFieldCompute.SetInt(_AbyssalFlowHeatSourceCountId, heatSourceCount);
            abyssalFlowFieldCompute.SetInt(_AbyssalFlowWeatherStateMaskId, (int)weatherSnapshot.StateMask);
            abyssalFlowFieldCompute.SetInt(_AbyssalFlowUpdateBucketId, updateBucket);
            abyssalFlowFieldCompute.SetInt(_AbyssalFlowUpdateBucketMaskId, updateBucketMask);

            abyssalFlowFieldCompute.Dispatch(_gpuAbyssalUpdateKernel, groupCount, 1, 1);

            abyssalFlowFieldCompute.SetTexture(_gpuAbyssalTextureUpdateKernel, _AbyssalFlowTextureReadId, _gpuAbyssalFlowReadTexture);
            abyssalFlowFieldCompute.SetTexture(_gpuAbyssalTextureUpdateKernel, _AbyssalFlowTextureWriteId, _gpuAbyssalFlowWriteTexture);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalTextureUpdateKernel, _AbyssalHeatSourcesId, _activeGpuAbyssalHeatSourceBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalTextureUpdateKernel, _AbyssalSplashdownImpulseBufferId, splashdownImpulseBuffer);
            abyssalFlowFieldCompute.SetVector(_AbyssalSplashdownParamsId, splashdownParams);
            abyssalFlowFieldCompute.Dispatch(_gpuAbyssalTextureUpdateKernel, textureGroupCountX, textureGroupCountY, textureGroupCountZ);
            SwapAbyssalFlowTextures();

            Vector4 wakeSphere = Vector4.zero;
            Vector4 wakeVelocity = Vector4.zero;
            if (flowTextureDetail01 > 0.001f && TryResolveSubmarineWakePayload(out wakeSphere, out wakeVelocity))
            {
                abyssalFlowFieldCompute.SetTexture(_gpuAbyssalWakeKernel, _AbyssalFlowTextureRWId, _gpuAbyssalFlowReadTexture);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowWakeSphereId, wakeSphere);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowWakeVelocityId, wakeVelocity);
                abyssalFlowFieldCompute.Dispatch(_gpuAbyssalWakeKernel, wakeGroupCountX, wakeGroupCountY, wakeGroupCountZ);
            }

            int vortexDispatchCount = DispatchAbyssalVortexImpulses(
                vortexGroupCountX,
                vortexGroupCountY,
                vortexGroupCountZ,
                fixedDeltaTime,
                flowTextureDetail01);

            QueueAbyssalFlowGlobalPublication(
                _gpuAbyssalFlowResultBuffer,
                _gpuAbyssalFlowReadTexture,
                gridResolution,
                flowCenterVector,
                flowSpacingVector,
                textureParams);
            _lastAbyssalGridResolution = gridResolution;
            _lastAbyssalFlowCenter = flowCenterVector;
            _lastAbyssalFlowSpacing = flowSpacingVector;
            _lastAbyssalFlowTextureSpacing = textureSpacingVector;
            _hasAbyssalFlowTexture = true;
            _abyssalFlowPublicationClearIssued = false;
            uint telemetryFlags = flowTextureDetail01 > 0.001f ? 1u : 0u;
            if (vortexDispatchCount > 0)
                telemetryFlags |= 2u;
            if (splashdownParams.x > 0.5f)
                telemetryFlags |= 4u;
            WriteAbyssalFlowTelemetry(flowCenter, wakeSphere, wakeVelocity, heatSourceCount, _lastSplashdownFluidImpulseCount, telemetryFlags);
            ReportWatchdogCost(AbyssalFlowBucketedCostHash, watchdogStart);
        }

        private static int ResolveKernel(ComputeShader compute, string kernelName, bool supportsComputeShaders)
        {
            if (compute == null || !supportsComputeShaders)
                return -1;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return -1;

                int kernel = compute.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return compute.IsSupported(kernel) ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private static int ResolveKernelThreadGroupSizeX(ComputeShader compute, int kernel, bool supportsComputeShaders)
        {
            if (compute == null || kernel < 0 || !supportsComputeShaders)
                return 0;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return 0;

                compute.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
            catch (MissingReferenceException)
            {
                return 0;
            }
            catch (UnityException)
            {
                return 0;
            }
            ulong totalThreads = (ulong)sizeX * sizeY * sizeZ;
            if (sizeX == 0u ||
                sizeY != 1u ||
                sizeZ != 1u ||
                totalThreads > PortableMaxComputeThreadsPerGroup ||
                sizeX > int.MaxValue)
            {
                return 0;
            }

            return (int)sizeX;
        }

        private static void ResolveKernelThreadGroupSizes(
            ComputeShader compute,
            int kernel,
            bool supportsComputeShaders,
            out int sizeX,
            out int sizeY,
            out int sizeZ)
        {
            sizeX = 0;
            sizeY = 0;
            sizeZ = 0;
            if (compute == null || kernel < 0 || !supportsComputeShaders)
                return;

            uint queryX;
            uint queryY;
            uint queryZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return;

                compute.GetKernelThreadGroupSizes(kernel, out queryX, out queryY, out queryZ);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }
            catch (System.InvalidOperationException)
            {
                return;
            }
            catch (System.ArgumentException)
            {
                return;
            }
            catch (MissingReferenceException)
            {
                return;
            }
            catch (UnityException)
            {
                return;
            }
            ulong totalThreads = (ulong)queryX * queryY * queryZ;
            if (queryX == 0u ||
                queryY == 0u ||
                queryZ == 0u ||
                totalThreads > PortableMaxComputeThreadsPerGroup ||
                queryX > int.MaxValue ||
                queryY > int.MaxValue ||
                queryZ > int.MaxValue)
            {
                sizeX = 0;
                sizeY = 0;
                sizeZ = 0;
                return;
            }

            if (queryX > 0u && queryX <= int.MaxValue)
                sizeX = (int)queryX;
            if (queryY > 0u && queryY <= int.MaxValue)
                sizeY = (int)queryY;
            if (queryZ > 0u && queryZ <= int.MaxValue)
                sizeZ = (int)queryZ;
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private static void ReportWatchdogCost(uint subsystemHash, long startTimestamp)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= 0L)
                return;

            float elapsedMilliseconds = (float)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            RuntimeWatchdog.ReportSubsystemCost(subsystemHash, elapsedMilliseconds);
        }

        private void ClearAbyssalVortexImpulses()
        {
            int count = _abyssalVortexImpulseCount;
            for (int i = 0; i < count; i++)
                _abyssalVortexImpulses[i] = default;

            _abyssalVortexImpulseCount = 0;
            _abyssalVortexImpulseWriteIndex = 0;
            _lastAbyssalVortexImpulseAgeFixedTime = -1f;
        }

        private void AgeAbyssalVortexImpulsesOnce(float fixedDeltaTime)
        {
            float currentFixedTime = Time.fixedTime;
            if (math.abs(_lastAbyssalVortexImpulseAgeFixedTime - currentFixedTime) <= 0.000001f)
                return;

            _lastAbyssalVortexImpulseAgeFixedTime = currentFixedTime;
            AgeAbyssalVortexImpulses(fixedDeltaTime);
        }

        private void AgeAbyssalVortexImpulses(float fixedDeltaTime)
        {
            int count = _abyssalVortexImpulseCount;
            if (count <= 0)
                return;

            float dt = math.max(0f, fixedDeltaTime);
            int writeIndex = 0;
            for (int i = 0; i < count; i++)
            {
                AbyssalVortexImpulse impulse = _abyssalVortexImpulses[i];
                impulse.RemainingSeconds -= dt;
                if (impulse.RemainingSeconds <= 0f)
                    continue;

                if (writeIndex != i)
                    _abyssalVortexImpulses[writeIndex] = impulse;
                writeIndex++;
            }

            for (int i = writeIndex; i < count; i++)
                _abyssalVortexImpulses[i] = default;

            _abyssalVortexImpulseCount = writeIndex;
            _abyssalVortexImpulseWriteIndex = writeIndex % MaxAbyssalVortexImpulseCount;
        }

        private int DispatchAbyssalVortexImpulses(
            int textureGroupCountX,
            int textureGroupCountY,
            int textureGroupCountZ,
            float fixedDeltaTime,
            float flowTextureDetail01)
        {
            int count = _abyssalVortexImpulseCount;
            if (count <= 0)
                return 0;

            _lastAbyssalVortexImpulseAgeFixedTime = Time.fixedTime;
            float dt = math.max(0f, fixedDeltaTime);
            int writeIndex = 0;
            int dispatchCount = 0;

            for (int i = 0; i < count; i++)
            {
                AbyssalVortexImpulse impulse = _abyssalVortexImpulses[i];
                impulse.RemainingSeconds -= dt;
                if (impulse.RemainingSeconds <= 0f)
                    continue;

                if (writeIndex != i)
                    _abyssalVortexImpulses[writeIndex] = impulse;
                writeIndex++;

                float detail01 = math.saturate(flowTextureDetail01);
                if (detail01 <= 0.001f)
                    continue;

                float strengthScale = math.saturate(impulse.RemainingSeconds / math.max(impulse.DurationSeconds, 0.001f));
                Vector4 sphere = new Vector4(
                    impulse.PositionWS.x,
                    impulse.PositionWS.y,
                    impulse.PositionWS.z,
                    impulse.RadiusMeters);
                Vector4 axisStrength = new Vector4(
                    impulse.AxisWS.x,
                    impulse.AxisWS.y,
                    impulse.AxisWS.z,
                    impulse.StrengthMetersPerSecond * strengthScale * detail01);
                abyssalFlowFieldCompute.SetTexture(_gpuAbyssalVortexKernel, _AbyssalFlowTextureRWId, _gpuAbyssalFlowReadTexture);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexSphereId, sphere);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexAxisStrengthId, axisStrength);
                abyssalFlowFieldCompute.Dispatch(_gpuAbyssalVortexKernel, textureGroupCountX, textureGroupCountY, textureGroupCountZ);
                dispatchCount++;
            }

            for (int i = writeIndex; i < count; i++)
                _abyssalVortexImpulses[i] = default;

            _abyssalVortexImpulseCount = writeIndex;
            _abyssalVortexImpulseWriteIndex = writeIndex % MaxAbyssalVortexImpulseCount;
            return dispatchCount;
        }

        private bool TryResolveSubmarineWakePayload(out Vector4 wakeSphere, out Vector4 wakeVelocity)
        {
            wakeSphere = Vector4.zero;
            wakeVelocity = Vector4.zero;

            ISubmarineRuntimeContext submarine = TryGetCachedSubmarineRuntime();
            Rigidbody hull = submarine != null ? submarine.HullRigidbody : null;
            if (hull == null)
                return false;

            Vector3 position = hull.worldCenterOfMass;
            Vector3 velocity = hull.linearVelocity;
            if (!IsFiniteVector(position) || !IsFiniteVector(velocity))
                return false;

            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float speedSq = math.lengthsq(velocity3);
            if (!math.isfinite(speedSq) ||
                speedSq <= AbyssalFlowWakeMinimumSpeedMetersPerSecond * AbyssalFlowWakeMinimumSpeedMetersPerSecond)
            {
                return false;
            }

            float speed = ApproximateMagnitude(velocity3);
            float radius = math.clamp(8f + speed * 0.6f, 8f, 32f);
            wakeSphere = new Vector4(position.x, position.y, position.z, radius);
            wakeVelocity = new Vector4(velocity.x, velocity.y, velocity.z, speed);
            return true;
        }

        private void WriteAbyssalFlowTelemetry(
            float3 center,
            Vector4 wakeSphere,
            Vector4 wakeVelocity,
            int heatSourceCount,
            int fluidImpulseCount,
            uint flags)
        {
            if (!_abyssalFlowTelemetry.IsCreated)
                return;

            bool invalid =
                !math.all(math.isfinite(center)) ||
                !math.isfinite(wakeSphere.x) ||
                !math.isfinite(wakeSphere.y) ||
                !math.isfinite(wakeSphere.z) ||
                !math.isfinite(wakeVelocity.x) ||
                !math.isfinite(wakeVelocity.y) ||
                !math.isfinite(wakeVelocity.z);
            if (invalid)
                flags |= 0x80000000u;

            int index = _abyssalFlowTelemetryCursor;
            if ((uint)index >= (uint)_abyssalFlowTelemetry.Length)
                index = 0;

            float3 wakePosition = new float3(wakeSphere.x, wakeSphere.y, wakeSphere.z);
            float3 wakeVelocity3 = new float3(wakeVelocity.x, wakeVelocity.y, wakeVelocity.z);
            _abyssalFlowTelemetry[index] = new AbyssalFlowTelemetryEntry
            {
                Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                FixedTime = Time.fixedTime,
                CenterWS = center,
                WakePositionWS = wakePosition,
                WakeVelocityWS = wakeVelocity3,
                WakeRadius = math.max(0f, wakeSphere.w),
                HeatSourceCount = math.max(0, heatSourceCount),
                FluidImpulseCount = math.max(0, fluidImpulseCount),
                Flags = flags,
                StateHash = BuildAbyssalFlowTelemetryHash(center, wakePosition, wakeVelocity3, heatSourceCount, fluidImpulseCount, flags)
            };
            _abyssalFlowTelemetryCursor = (index + 1) % _abyssalFlowTelemetry.Length;

            if (invalid)
                DumpAbyssalFlowTelemetryOnce(flags);
        }

        private static uint BuildAbyssalFlowTelemetryHash(
            float3 center,
            float3 wakePosition,
            float3 wakeVelocity,
            int heatSourceCount,
            int fluidImpulseCount,
            uint flags)
        {
            uint hash = 2166136261u;
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(center.x));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(center.y));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(center.z));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(wakePosition.x));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(wakePosition.y));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(wakePosition.z));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(wakeVelocity.x));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(wakeVelocity.y));
            hash = HashAbyssalFlowTelemetry(hash, QuantizeAbyssalFlowTelemetry(wakeVelocity.z));
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, heatSourceCount));
            hash = HashAbyssalFlowTelemetry(hash, (uint)math.max(0, fluidImpulseCount));
            hash = HashAbyssalFlowTelemetry(hash, flags);
            return hash;
        }

        private static uint HashAbyssalFlowTelemetry(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private static uint QuantizeAbyssalFlowTelemetry(float value)
        {
            if (!math.isfinite(value))
                return 0xffffffffu;

            int quantized = (int)math.clamp(math.round(value * 16f), int.MinValue + 1f, int.MaxValue - 1f);
            return unchecked((uint)quantized);
        }

        private void DumpAbyssalFlowTelemetryOnce(uint reasonFlags)
        {
            if (_abyssalFlowTelemetryDumped || !_abyssalFlowTelemetry.IsCreated)
                return;

            _abyssalFlowTelemetryDumped = true;
            if (!BinaryFaultDumpsEnabled)
                return;
            try
            {
                int entryBytes = 64;
                int byteCount = 16 + _abyssalFlowTelemetry.Length * entryBytes;
                NativeArray<byte> dump = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonFluidEngine),
                    "AbyssalFlowTelemetryDumpPayload");
                try
                {
                    int cursor = 0;
                    WriteUInt32LittleEndian(dump, ref cursor, 0x41424646u);
                    WriteInt32LittleEndian(dump, ref cursor, AbyssalFlowTelemetryCapacity);
                    WriteInt32LittleEndian(dump, ref cursor, _abyssalFlowTelemetryCursor);
                    WriteUInt32LittleEndian(dump, ref cursor, reasonFlags);
                    for (int i = 0; i < _abyssalFlowTelemetry.Length; i++)
                    {
                        int index = (_abyssalFlowTelemetryCursor + i) % _abyssalFlowTelemetry.Length;
                        AbyssalFlowTelemetryEntry entry = _abyssalFlowTelemetry[index];
                        WriteInt32LittleEndian(dump, ref cursor, entry.Frame);
                        WriteFloatLittleEndian(dump, ref cursor, entry.FixedTime);
                        WriteFloatLittleEndian(dump, ref cursor, entry.CenterWS.x);
                        WriteFloatLittleEndian(dump, ref cursor, entry.CenterWS.y);
                        WriteFloatLittleEndian(dump, ref cursor, entry.CenterWS.z);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakePositionWS.x);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakePositionWS.y);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakePositionWS.z);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakeVelocityWS.x);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakeVelocityWS.y);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakeVelocityWS.z);
                        WriteFloatLittleEndian(dump, ref cursor, entry.WakeRadius);
                        WriteInt32LittleEndian(dump, ref cursor, entry.HeatSourceCount);
                        WriteInt32LittleEndian(dump, ref cursor, entry.FluidImpulseCount);
                        WriteUInt32LittleEndian(dump, ref cursor, entry.Flags);
                        WriteUInt32LittleEndian(dump, ref cursor, entry.StateHash);
                    }

                    WriteNativeDump(ResolveFluidDumpPath(AbyssalFlowDumpRelativePath), dump, cursor);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref dump,
                        nameof(HectonFluidEngine),
                        "AbyssalFlowTelemetryDumpPayload");
                }
            }
            catch (System.Exception)
            {
            }
        }

        private int CaptureAbyssalHeatSources(float3 flowCenter, float flowTextureDetail01)
        {
            if (!_gpuAbyssalHeatSourceUpload.IsCreated)
                return 0;

            for (int i = 0; i < MaxAbyssalHeatSourceCount; i++)
                _gpuAbyssalHeatSourceUpload[i] = default;

            IThermodynamicsService thermalManager = _thermalRuntime;
            if (thermalManager == null)
                return 0;

            float detail01 = math.saturate(flowTextureDetail01);
            if (detail01 <= 0.001f)
                return 0;

            float horizontalProbeOffset = math.max(abyssalHeatProbeRadius, abyssalFlowHorizontalCellSize * 1.5f);
            float verticalProbeOffset = math.max(abyssalHeatProbeRadius * 0.5f, abyssalFlowVerticalCellSize);
            float sampleRadius = math.max(1f, abyssalFlowHorizontalCellSize * 0.5f);
            int probeBudget = math.clamp((int)math.ceil(math.lerp(1f, MaxAbyssalHeatSourceCount, detail01)), 1, MaxAbyssalHeatSourceCount);
            int sourceCount = 0;

            for (int probeIndex = 0; probeIndex < probeBudget; probeIndex++)
            {
                float3 sampleOffset = ResolveHeatProbeOffset(probeIndex, horizontalProbeOffset, verticalProbeOffset);
                Vector3 samplePosition = new Vector3(
                    flowCenter.x + sampleOffset.x,
                    flowCenter.y + sampleOffset.y,
                    flowCenter.z + sampleOffset.z);

                if (!thermalManager.SampleThermalFlow(samplePosition, sampleRadius, out ThermodynamicFlowSampleDTO sample) ||
                    sample.HasFlow == 0)
                {
                    continue;
                }

                float heatNormalizationRcp = math.rcp(math.max(0.1f, abyssalHeatIntensityNormalization));
                float intensity = math.saturate(math.max(
                    sample.Heat01 * heatNormalizationRcp,
                    sample.FlowVelocityWS.y * 0.125f));
                if (intensity <= 0.0001f)
                    continue;

                _gpuAbyssalHeatSourceUpload[sourceCount] = new GpuHeatSourceData
                {
                    PositionWS = new float3(samplePosition.x, samplePosition.y, samplePosition.z),
                    Intensity = intensity,
                    Radius = abyssalHeatProbeRadius,
                    Padding = float3.zero,
                };

                sourceCount++;
                if (sourceCount >= MaxAbyssalHeatSourceCount)
                    break;
            }

            return sourceCount;
        }

        private float3 ResolveAbyssalFlowCenter(float resolvedWaterLevel)
        {
            Vector3 observerPosition = lodObserver.position;
            return new float3(
                observerPosition.x,
                math.min(observerPosition.y, resolvedWaterLevel - 32f),
                observerPosition.z);
        }

        private float3 ResolveGiantWakeCurrentBase()
        {
            if (!enableGiantWakeCurrent || giantWakeCurrentStrength <= 0f)
                return float3.zero;

            ICelestialSkyDirectionReadModel celestialEngine = _celestialEngine;
            if (celestialEngine == null || !celestialEngine.TryGetAegirSkyDirection(out Vector3 directionManaged))
                return float3.zero;

            float3 skyDirection = new float3(directionManaged.x, directionManaged.y, directionManaged.z);
            float3 horizontalDirection = new float3(skyDirection.x, 0f, skyDirection.z);
            float horizontalLengthSq = math.lengthsq(horizontalDirection);
            if (horizontalLengthSq <= GiantWakeDirectionEpsilonSq)
                return float3.zero;

            float3 wakeDirection = NormalizeOrDefault(horizontalDirection, new float3(1f, 0f, 0f));
            wakeDirection.y = giantWakeVerticalBias;
            wakeDirection = NormalizeOrDefault(wakeDirection, new float3(1f, 0f, 0f));
            return wakeDirection * math.max(0f, giantWakeCurrentStrength);
        }

        private float3 ResolveGiantWakeCurrentForDepth(float sampleY)
        {
            float3 wakeCurrent = _resolvedGiantWakeCurrent;
            float depthBelowSurface = math.max(0f, ReadPublishedCurrentWaterLevelY() - sampleY);
            float fadeStart = math.max(0f, giantWakeDepthFadeStart);
            float fadeRange = math.max(0.001f, giantWakeDepthFadeRange);
            float depthFade = math.saturate((depthBelowSurface - fadeStart) * math.rcp(fadeRange));
            return wakeCurrent * depthFade;
        }

        private int GetAbyssalFlowNodeCount()
        {
            return AbyssalFlowTextureResolution * AbyssalFlowTextureResolution * AbyssalFlowTextureResolution;
        }

        private static float3 ResolveHeatProbeOffset(int probeIndex, float horizontalProbeOffset, float verticalProbeOffset)
        {
            switch (probeIndex)
            {
                case 0: return float3.zero;
                case 1: return new float3(horizontalProbeOffset, 0f, 0f);
                case 2: return new float3(-horizontalProbeOffset, 0f, 0f);
                case 3: return new float3(0f, 0f, horizontalProbeOffset);
                case 4: return new float3(0f, 0f, -horizontalProbeOffset);
                case 5: return new float3(0f, verticalProbeOffset, 0f);
                case 6: return new float3(0f, -verticalProbeOffset, 0f);
                default: return new float3(horizontalProbeOffset * 0.70710677f, 0f, horizontalProbeOffset * 0.70710677f);
            }
        }

        private static float FastMagnitudeApprox(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxComponent = math.cmax(absValue);
            float minComponent = math.cmin(absValue);
            float midComponent = absValue.x + absValue.y + absValue.z - maxComponent - minComponent;
            return maxComponent + midComponent * 0.375f + minComponent * 0.125f;
        }

        private void ConsumeGpuBuoyancyReadbacks()
        {
            using (_gpuReadbackProfilerMarker.Auto())
            {
            if (_gpuReadbackRequests == null || _gpuReadbackActive == null || _gpuReadbackData == null || !_gpuBuoyancyReadback.IsCreated)
                return;

            for (int requestIndex = 0; requestIndex < GpuReadbackRingSize; requestIndex++)
            {
                if (!_gpuReadbackActive[requestIndex])
                    continue;

                AsyncGPUReadbackRequest request = _gpuReadbackRequests[requestIndex];
                if (!request.done)
                    continue;

                _gpuReadbackActive[requestIndex] = false;
                if (request.hasError)
                    continue;

                NativeArray<float4> readbackData = _gpuReadbackData.Slot(requestIndex);
                int readCount = math.min(_gpuReadbackCounts[requestIndex], math.min(_gpuBuoyancyReadback.Length, readbackData.Length));
                if (!readbackData.IsCreated || readCount <= 0)
                    continue;

                for (int i = 0; i < readCount; i++)
                {
                    float4 sample = readbackData[i];
                    _gpuBuoyancyReadback[i] = sample;
                    _waveOffsets[i] = sample.x;
                    _gpuBuoyancyForcesY[i] = sample.y;
                }

                _hasGpuBuoyancyData = readCount > 0;
            }
            }
        }

        private void QueueGpuBuoyancyReadbackConsume()
        {
            _hasPendingGpuBuoyancyReadbackConsume = true;
        }

        private void FlushGpuBuoyancyReadbackConsume()
        {
            if (!_hasPendingGpuBuoyancyReadbackConsume)
                return;

            _hasPendingGpuBuoyancyReadbackConsume = false;
            ConsumeGpuBuoyancyReadbacks();
        }

        private void UploadGpuBuoyancyObjectData(int count)
        {
            if (!_gpuBuoyancyObjectDataUpload.IsCreated)
                return;

            for (int i = 0; i < count; i++)
            {
                BuoyancyParams buoyancyParams = _params[i];
                _gpuBuoyancyObjectDataUpload[i] = new GpuBuoyancyObjectData
                {
                    Volume = buoyancyParams.volume,
                    Height = buoyancyParams.height,
                    IsInAir = buoyancyParams.isInAir != 0 ? 1f : 0f,
                    SimplifiedSubmersion = buoyancyParams.simplifiedSubmersion != 0 ? 1f : 0f,
                    BoundsCenterWS = buoyancyParams.boundsCenter,
                    BoundsExtentsWS = buoyancyParams.boundsExtents
                };
            }
        }

        private void SetGpuWave(ComputeShader shader, int waveAId, int waveBId, in GerstnerWaveComponent wave)
        {
            shader.SetVector(waveAId, new Vector4(wave.DirectionXZ.x, wave.DirectionXZ.y, wave.Amplitude, wave.Wavelength));
            shader.SetVector(waveBId, new Vector4(wave.Steepness, wave.PhaseOffset, wave.SpeedMultiplier, 0f));
        }

        private void TryDispatchGpuBuoyancySampling(in WeatherRuntimeSnapshot weatherSnapshot, int count, float resolvedWaterLevel)
        {
            if (!enableGpuBuoyancySampling ||
                gpuBuoyancyCompute == null ||
                _gpuBuoyancyKernel < 0 ||
                count < gpuBuoyancyActivationThreshold ||
                !_positions.IsCreated ||
                !_gpuBuoyancyObjectDataUpload.IsCreated)
            {
                return;
            }

            int groupCount = CeilDividePositive(count, _gpuBuoyancyThreadGroupSizeX);
            if (groupCount <= 0)
                return;

            if (!HasGpuBuoyancyBuffers(count))
                return;

            int slot = _gpuReadbackWriteIndex;
            if (_gpuReadbackActive != null && _gpuReadbackActive[slot])
                return;

            GraphicsBuffer positionBuffer = _gpuBuoyancyUploadBufferIndex == 0
                ? _gpuBuoyancyPositionBufferA
                : _gpuBuoyancyPositionBufferB;
            GraphicsBuffer paramBuffer = _gpuBuoyancyUploadBufferIndex == 0
                ? _gpuBuoyancyParamBufferA
                : _gpuBuoyancyParamBufferB;
            GraphicsBuffer resultBuffer = _gpuBuoyancyResultBuffers[slot];
            if (positionBuffer == null || paramBuffer == null || resultBuffer == null)
                return;

            if (!HasGpuReadbackData(slot, count))
                return;

            int readbackBytes = ResolveGpuReadbackByteCount(resultBuffer, count);
            if (readbackBytes <= 0)
                return;

            UploadGpuBuoyancyObjectData(count);
            GraphicsBufferUploadUtility.UploadNativeArray<float3>(positionBuffer, _positions, count);
            GraphicsBufferUploadUtility.UploadNativeArray<GpuBuoyancyObjectData>(paramBuffer, _gpuBuoyancyObjectDataUpload, count);
            _gpuBuoyancyUploadBufferIndex ^= 1;

            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyPositionsId, positionBuffer);
            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyObjectDataId, paramBuffer);
            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyResultsId, resultBuffer);
            gpuBuoyancyCompute.SetInt(_GpuBuoyancyObjectCountId, count);
            gpuBuoyancyCompute.SetVector(_GpuBuoyancyWaterParamsId, new Vector4(resolvedWaterLevel, waterDensity, math.abs(UnityEngine.Physics.gravity.y), weatherSnapshot.CurrentMeta.TimeAccumulator));
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave0AId, _GpuBuoyancyWave0BId, weatherSnapshot.Wave0);
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave1AId, _GpuBuoyancyWave1BId, weatherSnapshot.Wave1);
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave2AId, _GpuBuoyancyWave2BId, weatherSnapshot.Wave2);

            gpuBuoyancyCompute.Dispatch(_gpuBuoyancyKernel, groupCount, 1, 1);
            ref NativeArray<float4> readbackData = ref _gpuReadbackData.Slot(slot);
            _gpuReadbackRequests[slot] = AsyncGPUReadback.RequestIntoNativeArray(ref readbackData, resultBuffer, readbackBytes, 0, null);
            if (_gpuReadbackRequests[slot].hasError)
                return;

            _gpuReadbackCounts[slot] = count;
            _gpuReadbackActive[slot] = true;
            _gpuReadbackWriteIndex = (_gpuReadbackWriteIndex + 1) % GpuReadbackRingSize;
        }

        private void CompletePendingGpuBuoyancyReadbacksForRelease()
        {
            if (_gpuReadbackActive == null)
                return;

            bool hasPending = false;
            for (int i = 0; i < math.min(_gpuReadbackActive.Length, GpuReadbackRingSize); i++)
            {
                hasPending |= _gpuReadbackActive[i];
            }

            if (!hasPending)
                return;

            // BLOCKING_SYNC_POINT: teardown/configuration must not release GPU buoyancy result buffers while AsyncGPUReadback owns them.
            AsyncGPUReadback.WaitAllRequests();
            for (int i = 0; i < math.min(_gpuReadbackActive.Length, GpuReadbackRingSize); i++)
            {
                _gpuReadbackActive[i] = false;
            }
        }

        private void DisposeGpuReadbackData()
        {
            if (_gpuReadbackData == null)
                return;

            _gpuReadbackData.Dispose();
        }

        private static int ResolveGpuReadbackByteCount(GraphicsBuffer buffer, int count)
        {
            if (buffer == null || count <= 0)
                return 0;

            int safeCount = math.min(count, math.max(0, buffer.count));
            long byteCount = (long)safeCount * UnsafeUtility.SizeOf<float4>();
            long maxBytes = (long)math.max(0, buffer.count) * math.max(1, buffer.stride);
            return byteCount > 0L && byteCount <= maxBytes ? (int)byteCount : 0;
        }

        private void QueueGpuBuoyancySampling(in WeatherRuntimeSnapshot weatherSnapshot, int count, float resolvedWaterLevel)
        {
            _pendingGpuBuoyancyWeatherSnapshot = weatherSnapshot;
            _pendingGpuBuoyancyCount = count;
            _pendingGpuBuoyancyWaterLevel = resolvedWaterLevel;
            _hasPendingGpuBuoyancyDispatch = true;
        }

        private void FlushGpuBuoyancySampling()
        {
            if (!_hasPendingGpuBuoyancyDispatch)
                return;

            _hasPendingGpuBuoyancyDispatch = false;
            TryDispatchGpuBuoyancySampling(
                in _pendingGpuBuoyancyWeatherSnapshot,
                _pendingGpuBuoyancyCount,
                _pendingGpuBuoyancyWaterLevel);
        }

        private void QueueFluidGraphicsRelease(int releaseMask)
        {
            _pendingFluidGraphicsReleaseMask |= releaseMask;
        }

        private void FlushQueuedFluidGraphicsReleases()
        {
            int releaseMask = _pendingFluidGraphicsReleaseMask;
            if (releaseMask == 0)
                return;

            _pendingFluidGraphicsReleaseMask = 0;
            if ((releaseMask & FluidGraphicsReleaseSplashdownImpulse) != 0)
            {
                ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferA);
                ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBufferB);
                _activeGpuSplashdownImpulseBuffer = null;
                _gpuSplashdownImpulseUploadIndex = 0;
            }
            if ((releaseMask & FluidGraphicsReleaseGpuBuoyancy) != 0)
                ReleaseGpuBuoyancyBuffers();
            if ((releaseMask & FluidGraphicsReleaseAbyssalFlow) != 0)
                ReleaseGpuAbyssalFlowBuffers();
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugObjectCount = _objects.Count;
            _debugNearCount = 0;
            _debugMediumCount = 0;
            _debugFarCount = 0;
            _debugCulledCount = 0;
            _debugCurrentVolumeCount = CurrentVolume.ActiveCount;
        }

        private void TryResolveObserver(bool force)
        {
            if (lodObserver != null)
                return;

            if (!force && _observerResolveRetryTimer > 0f)
                return;

            _observerResolveRetryTimer = ObserverResolveRetryInterval;

            IPlayerRuntimeContext playerRuntime = TryGetCachedPlayerRuntime();
            if (playerRuntime != null && playerRuntime.PlayerTransform != null)
                lodObserver = playerRuntime.PlayerTransform;
        }

        private static float ResolveAbyssalVisualQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 0f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0f;
        }

        private void RefreshRuntimeActorContextsIfMissing()
        {
            if (_playerRuntime == null || IsUnityObjectInvalid(_playerRuntime))
                _playerRuntime = GlobalRegistry.Player;

            if (_submarineRuntime == null || IsUnityObjectInvalid(_submarineRuntime))
                _submarineRuntime = GlobalRegistry.Submarine;
        }

        private IPlayerRuntimeContext TryGetCachedPlayerRuntime()
        {
            if (IsUnityObjectInvalid(_playerRuntime))
                _playerRuntime = null;

            return _playerRuntime;
        }

        private ISubmarineRuntimeContext TryGetCachedSubmarineRuntime()
        {
            if (IsUnityObjectInvalid(_submarineRuntime))
                _submarineRuntime = null;

            return _submarineRuntime;
        }

        private static bool IsUnityObjectInvalid(object context)
        {
            return context is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// Updates cached LOD distance squares (called once at startup,
        /// and whenever LOD parameters change via properties).
        /// </summary>
        private void UpdateCachedLodDistances()
        {
            _cachedNearDistSq = nearLodDistance * nearLodDistance;
            _cachedMediumDistSq = mediumLodDistance * mediumLodDistance;
            _cachedFarDistSq = farLodDistance * farLodDistance;
            _cachedCullDistSq = cullLodDistance * cullLodDistance;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (waterDensity < 0.01f) waterDensity = 0.01f;
            if (cinematicTideAmplitudeMeters < 0f) cinematicTideAmplitudeMeters = 0f;
            if (viscousDrag  < 0f)    viscousDrag  = 0f;
            if (maxQuadraticDragForcePerKg < 0f) maxQuadraticDragForcePerKg = 0f;
            if (angularDrag  < 0f)    angularDrag  = 0f;
            if (jobBatchSize < 1)     jobBatchSize = 1;
            if (currentNoiseScale < 0.0001f) currentNoiseScale = 0.0001f;
            if (currentTimeScale < 0f) currentTimeScale = 0f;
            if (phantomCurrentStrength < 0f) phantomCurrentStrength = 0f;
            if (prebakedVectorNoiseCellSizeMeters < 0.25f) prebakedVectorNoiseCellSizeMeters = 0.25f;
            prebakedVectorNoiseTriangleModulation = Mathf.Clamp01(prebakedVectorNoiseTriangleModulation);
            if (haloclineBoundaryDepthMeters < 0.01f) haloclineBoundaryDepthMeters = 0.01f;
            if (deepLayerDensityMultiplier < 1f) deepLayerDensityMultiplier = 1f;
            if (giantWakeCurrentStrength < 0f) giantWakeCurrentStrength = 0f;
            giantWakeVerticalBias = Mathf.Clamp(giantWakeVerticalBias, -1f, 1f);
            if (giantWakeDepthFadeStart < 0f) giantWakeDepthFadeStart = 0f;
            if (giantWakeDepthFadeRange < 1f) giantWakeDepthFadeRange = 1f;
            if (tidalShearTorqueStrength < 0f) tidalShearTorqueStrength = 0f;
            if (tidalShearFrequency < 0.01f) tidalShearFrequency = 0.01f;
            if (nearLodDistance < 1f) nearLodDistance = 1f;
            if (mediumLodDistance < nearLodDistance) mediumLodDistance = nearLodDistance;
            if (farLodDistance < mediumLodDistance) farLodDistance = mediumLodDistance;
            if (cullLodDistance < farLodDistance) cullLodDistance = farLodDistance;
            if (gizmoCurrentVectorScale < 0f) gizmoCurrentVectorScale = 0f;
            if (abyssalFlowHorizontalResolution < 8) abyssalFlowHorizontalResolution = 8;
            if (abyssalFlowVerticalResolution < 4) abyssalFlowVerticalResolution = 4;
            if (abyssalFlowHorizontalCellSize < 4f) abyssalFlowHorizontalCellSize = 4f;
            if (abyssalFlowVerticalCellSize < 4f) abyssalFlowVerticalCellSize = 4f;
            if (abyssalHeatProbeRadius < 4f) abyssalHeatProbeRadius = 4f;
            if (abyssalHeatIntensityNormalization < 0.1f) abyssalHeatIntensityNormalization = 0.1f;
            cavitationBubbleEmitCountAtFullIntensity = Mathf.Clamp(cavitationBubbleEmitCountAtFullIntensity, 1, 128);
            if (cavitationShockwaveMaxAffectedMassKg < 0.1f) cavitationShockwaveMaxAffectedMassKg = 0.1f;
            cavitationShockwaveVerticalLift = Mathf.Clamp01(cavitationShockwaveVerticalLift);

#if UNITY_EDITOR
            if (gpuBuoyancyCompute == null)
                gpuBuoyancyCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(GpuBuoyancyComputeAssetPath);

            if (abyssalFlowFieldCompute == null)
                abyssalFlowFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(AbyssalFlowFieldComputeAssetPath);
#endif

            // Update LOD cache when parameters change
            UpdateCachedLodDistances();
        }

        private void OnDrawGizmos()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Gizmos.color = new Color(0f, 0.3f, 0.8f, 0.1f);
            float resolvedWaterLevelY = ResolveBaseWaterLevelY();
            Vector3 center = new Vector3(0f, resolvedWaterLevelY, 0f);
            Gizmos.DrawCube(center, new Vector3(200f, 0.02f, 200f));

            if (lodObserver != null && drawLodGizmos)
            {
                DrawLodRing(nearLodDistance, new Color(0.15f, 0.9f, 1f, 0.7f));
                DrawLodRing(mediumLodDistance, new Color(0.25f, 0.8f, 0.55f, 0.65f));
                DrawLodRing(farLodDistance, new Color(0.95f, 0.75f, 0.2f, 0.55f));
                DrawLodRing(cullLodDistance, new Color(1f, 0.35f, 0.2f, 0.45f));
            }

            if (drawCurrentVectors)
            {
                Vector3 origin = lodObserver != null ? lodObserver.position : center;
                origin.y = resolvedWaterLevelY;
                Vector3 current = currentVector * gizmoCurrentVectorScale;
                Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.95f);
                Gizmos.DrawRay(origin, current);
            }
        }

        private void DrawLodRing(float radius, Color color)
        {
            if (lodObserver == null || radius <= 0f)
                return;

            Gizmos.color = color;
#if UNITY_EDITOR
            Handles.color = color;
            Handles.DrawWireDisc(lodObserver.position, Vector3.up, radius);
#else
            Gizmos.DrawWireSphere(lodObserver.position, radius);
#endif
        }
#endif
    
        #region JulesLink_AbyssalVortexAngularTorqueCalculator
        private static void JulesLink_AbyssalVortexAngularTorqueCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.AbyssalVortexAngularTorqueCalculator); }
        #endregion

        #region JulesLink_MaelstromSpatialWarpPullCalculator
        private static void JulesLink_MaelstromSpatialWarpPullCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.MaelstromSpatialWarpPullCalculator); }
        #endregion
}
}
