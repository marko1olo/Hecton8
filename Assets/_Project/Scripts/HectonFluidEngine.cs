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

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Environment;
using Hecton8.Environment.Fluids;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
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

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ActiveThrusterFlow
    {
        [FieldOffset(0)]
        public float3 PositionWS;
        [FieldOffset(12)]
        public float3 DirectionWS;
        [FieldOffset(24)]
        public float Strength;
        [FieldOffset(28)]
        public float RadiusSq;
        [FieldOffset(32)]
        public float InvRadiusSq;
        [FieldOffset(36)]
        public float ConeCos;
        [FieldOffset(40)]
        public int Active;
        [FieldOffset(44)]
        public float Padding0;
        [FieldOffset(48)]
        private ulong _pad0;
        [FieldOffset(56)]
        private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WhirlpoolFlow
    {
        [FieldOffset(0)]
        public float3 CenterWS;
        [FieldOffset(12)]
        public float RadiusSq;
        [FieldOffset(16)]
        public float InvRadiusSq;
        [FieldOffset(20)]
        public float TangentialStrength;
        [FieldOffset(24)]
        public float CentripetalStrength;
        [FieldOffset(28)]
        public float VerticalPull;
        [FieldOffset(32)]
        public int Active;
        [FieldOffset(36)]
        public float Padding0;
        [FieldOffset(40)]
        public float Padding1;
        [FieldOffset(44)]
        public float Padding2;
        [FieldOffset(48)]
        private ulong _pad0;
        [FieldOffset(56)]
        private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FluidViscosityRegion
    {
        [FieldOffset(0)]
        public float3 CenterWS;
        [FieldOffset(12)]
        public float InvRadiusSq;
        [FieldOffset(16)]
        public float ViscosityMultiplier;
        [FieldOffset(20)]
        public int Active;
        [FieldOffset(24)]
        public float Padding0;
        [FieldOffset(28)]
        public float Padding1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FluidImpactEvent
    {
        [FieldOffset(0)]
        public float3 PositionWS;
        [FieldOffset(12)]
        public float3 VelocityWS;
        [FieldOffset(24)]
        public float MassKg;
        [FieldOffset(28)]
        public float SurfaceY;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanSurfaceTelemetryEntry
    {
        [FieldOffset(0)]
        public uint FrameIndex;
        [FieldOffset(4)]
        public uint OriginShiftSequence;
        [FieldOffset(8)]
        public int ActiveFloaters;
        [FieldOffset(12)]
        public int SleepingFloaters;
        [FieldOffset(16)]
        public int WaveOctaves;
        [FieldOffset(20)]
        public int TerrainRevision;
        [FieldOffset(24)]
        public float WaterLevelY;
        [FieldOffset(28)]
        public float MinSurfaceOffset;
        [FieldOffset(32)]
        public float MaxSurfaceOffset;
        [FieldOffset(36)]
        public float3 ObserverWS;
        [FieldOffset(48)]
        public float3 WindWS;
        [FieldOffset(60)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidAdvectionTelemetryEntry
    {
        [FieldOffset(0)]
        public uint FrameIndex;
        [FieldOffset(4)]
        public uint OriginShiftSequence;
        [FieldOffset(8)]
        public int ActiveAdvectedParticles;
        [FieldOffset(12)]
        public int SiltCount;
        [FieldOffset(16)]
        public int BubbleCount;
        [FieldOffset(20)]
        public int DebrisCount;
        [FieldOffset(24)]
        public int ActiveTurbulenceWakes;
        [FieldOffset(28)]
        public uint Flags;
        [FieldOffset(32)]
        public uint StateHash;
        [FieldOffset(36)]
        private uint _pad0;
        [FieldOffset(40)]
        private ulong _pad1;
        [FieldOffset(48)]
        private ulong _pad2;
        [FieldOffset(56)]
        private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InteriorFloodNode
    {
        [FieldOffset(0)]
        public float CurrentLiters;
        [FieldOffset(4)]
        public float CapacityLiters;
        [FieldOffset(8)]
        public float TransferLitersPerSecond;
        [FieldOffset(12)]
        public float StructuralMassKg;
        [FieldOffset(16)]
        public int FirstEdgeIndex;
        [FieldOffset(20)]
        public int EdgeCount;
        [FieldOffset(24)]
        public uint Flags;
        [FieldOffset(28)]
        public uint Padding;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InteriorFloodEdge
    {
        [FieldOffset(0)]
        public int ToNode;
        [FieldOffset(4)]
        public float FlowMultiplier;
        [FieldOffset(8)]
        public int IsOpen;
        [FieldOffset(12)]
        public int Padding;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InteriorFloodBfsResult
    {
        [FieldOffset(0)]
        public float TotalWaterMassKg;
        [FieldOffset(4)]
        public float StructuralLoadKg;
        [FieldOffset(8)]
        public int FloodedNodeCount;
        [FieldOffset(12)]
        public int Padding;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class HectonFluidEngine : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener
    {
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
        private const float SplashdownBubbleLowTierMaxVelocityMetersPerSecond = 8f;
        private const float SplashdownGoldenAngleRadians = 2.39996323f;
        private const uint SplashdownImpulseLowTierFlag = 1u << 0;
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
        private const int FluidAdvectionTelemetryCapacity = 300;
        private const int FluidAdvectionSignalDrainBudget = 64;
        private const int FluidAdvectionGlobalTelemetryIntervalFrames = 30;
        private const float FluidFallbackClockMaxSeconds = 16777215f;
        private const int DynamicWakeGpuCapacity = 16;
        private const int DynamicWakeLowTierGpuCapacity = 4;
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
        public const int MaxAnalyticalThrusterCount = 4;
        public const int MaxAnalyticalWhirlpoolCount = 2;
        public const int MaxActiveMaelstromCount = MaxAnalyticalWhirlpoolCount;
        public const int MaxDynamicViscosityRegionCount = 4;
        private const int MaelstromTelemetryCapacity = 300;
        private const string MaelstromDumpRelativePath = "Docs/AgentLogs/Dump_MAELSTROM_KINEMATICS.bin";
        private const float MaelstromMinimumRadiusMeters = 0.5f;
        private const float MaelstromEventHorizonRadiusFactor = 0.12f;
        internal const float MaelstromMaxVelocityMetersPerSecond = 18f;
        internal const float MaelstromLowTierMaxVelocityMetersPerSecond = 10f;
        private const HectonQualityTier AuthorityFluidWaveTier = HectonQualityTier.Ultra;
        private const byte AuthorityFluidHighMathTier = 1;
        private const byte AuthorityFluidLowMathTier = 0;
        private const float MaelstromAudioIntervalSeconds = 0.45f;
        private const float MaelstromDamageIntervalSeconds = 0.35f;
        private const float MaelstromDamageMagnitude = 18f;
        private const uint MaelstromSourceHash = 0x4D41454Cu;
        private const byte MaelstromAcousticChannel = 12;
        private const int ViscosityGradientLutSize = 16;
        private const int FluidImpactEventQueueCapacity = 64;
        private const BufferID FluidImpactEventRingBufferId = (BufferID)70887;
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
        private const uint OceanSplashSignalHash = 0x4F435350u;
        private const uint SplashdownFluidImpulseCountHash = 0x53464943u;
        private const uint SplashdownFluidImpulseContextHash = 0x5346504Cu;
        // Keep GPU sampling dormant until it matches the 16-wave/AUP/terrain Burst path.
        private const bool GpuBuoyancySurfaceParityAvailable = false;
        private const string NativeMemoryOwner = nameof(HectonFluidEngine);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

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
        private static readonly int _GlobalWakeParamsId = Shader.PropertyToID("_GlobalWakeParams");
        private static readonly ProfilerMarker _gatherDataProfilerMarker = new ProfilerMarker("H8.Fluid.GatherData");
        private static readonly ProfilerMarker _jobScheduleProfilerMarker = new ProfilerMarker("H8.Fluid.ScheduleBuoyancyJob");
        private static readonly ProfilerMarker _scheduledApplyProfilerMarker = new ProfilerMarker("H8.Fluid.ApplyScheduledForces");
        private static readonly ProfilerMarker _gpuReadbackProfilerMarker = new ProfilerMarker("H8.Fluid.ConsumeGpuReadback");
        private static readonly int _buoyancyForceNanErrorCode = unchecked((int)Hecton.Localization.LocHash.Compute("NAN_ERROR_HASH_BUOYANCY_FORCE"));
        private static readonly int _buoyancyTorqueNanErrorCode = unchecked((int)Hecton.Localization.LocHash.Compute("NAN_ERROR_HASH_BUOYANCY_TORQUE"));
        private static HectonFluidEngine s_runtimeInstance;
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtimeInstance = null;

            for (int i = 0; i < CavitationShockwaveHitCapacity; i++)
            {
                s_CavitationShockwaveColliders[i] = null;
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
        [SerializeField] private float waterLevel = 5000f;
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
        [SerializeField] private bool enableGpuBuoyancySampling = true;
        [SerializeField] private ComputeShader gpuBuoyancyCompute;
        [SerializeField, Range(64, 1024)] private int gpuBuoyancyActivationThreshold = 256;
        [SerializeField] private bool enableGpuAbyssalFlowField = true;
        [SerializeField] private ComputeShader abyssalFlowFieldCompute;
        [SerializeField] private ComputeShader fluidAdvectionCompute;
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

        public struct FluidAdvectionRenderGraphPayload
        {
            public ComputeShader Compute;
            public int Kernel;
            public int DispatchGroups;
            public GraphicsBuffer SiltRead;
            public GraphicsBuffer SiltWrite;
            public GraphicsBuffer BubbleRead;
            public GraphicsBuffer BubbleWrite;
            public GraphicsBuffer DebrisRead;
            public GraphicsBuffer DebrisWrite;
            public GraphicsBuffer EmptySiltBuffer;
            public GraphicsBuffer EmptyBubbleBuffer;
            public GraphicsBuffer EmptyDebrisBuffer;
            public GraphicsBuffer AbyssalFlowBuffer;
            public GraphicsBuffer EmptyAbyssalFlowBuffer;
            public Texture AbyssalFlowTexture;
            public Texture VoxelSdfTexture;
            public Texture EmptyVoxelSdfTexture;
            public RTHandle AbyssalFlowTextureHandle;
            public RTHandle VoxelSdfTextureHandle;
            public RTHandle EmptyVoxelSdfTextureHandle;
            public Vector4 Counts;
            public Vector4 Params;
            public Vector4 Buoyancy;
            public Vector4 AupShiftDelta;
            public GraphicsBuffer DynamicWakeBuffer;
            public GraphicsBuffer DynamicWakeVectorBuffer;
            public Vector4 DynamicWakeParams;
            public Vector4 AbyssalGridResolution;
            public Vector4 AbyssalFlowCenter;
            public Vector4 AbyssalFlowSpacing;
            public Vector4 AbyssalFlowTextureParams;
            public float AbyssalFlowTextureActive;
            public float AbyssalFlowInterpolationAlpha;
            public Matrix4x4 VoxelSdfWorldToLocal;
            public Vector4 VoxelSdfInvDoubleHalfExtents;
            public Vector4 SdfParams;
        }

        /// <summary>Y-koordinata poverhnosti vody.</summary>
        public float WaterLevel
        {
            get => waterLevel;
            set
            {
                waterLevel = value;
                PublishCurrentWaterLevelUniform();
            }
        }

        /// <summary>Cinematic surface water level consumed by shader/UI/physics bridges.</summary>
        public float CurrentWaterLevelY
        {
            get { return ResolveCinematicWaterLevelY(); }
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
        public NativeArray<float3> FloaterPositions => _positions;
        public NativeArray<float> BuoyancyResults => _waveOffsets;

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

            EnsureFluidAdvectionState();
            if (!IsFluidAdvectionReady())
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

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float resolvedWaterLevel = ResolveCinematicWaterLevelY();
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
            byte highScalabilityTier = AuthorityFluidHighMathTier;
            float3 flow = HectonAnalyticalFlowField.SampleBaseFlow(
                position,
                depthBelowSurface,
                baseCurrent,
                math.lengthsq(_resolvedGiantWakeCurrent) > GiantWakeDirectionEpsilonSq
                    ? _resolvedGiantWakeCurrent
                    : ResolveGiantWakeCurrentBase(),
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
                ResolveWaterLevelTimeSeconds(),
                haloclineBoundaryDepthMeters,
                haloclineShearForcePerKg,
                vectorNoiseField,
                vectorNoiseLength,
                aupOffset,
                math.rcp(math.max(0.25f, prebakedVectorNoiseCellSizeMeters)),
                enablePrebakedVectorNoise ? (byte)1 : (byte)0,
                prebakedVectorNoiseTriangleModulation,
                highScalabilityTier);

            for (int i = 0; i < MaxAnalyticalThrusterCount; i++)
                HectonAnalyticalFlowField.ApplyThrusterFlow(ref flow, position, _thrusterFlowBuffer[i]);

            for (int i = 0; i < MaxAnalyticalWhirlpoolCount; i++)
                HectonAnalyticalFlowField.ApplyWhirlpoolFlow(ref flow, position, _whirlpoolFlowBuffer[i], AuthorityFluidLowMathTier);

            return HectonAnalyticalFlowField.ResolveFiniteFloat3OrZero(flow);
        }

        public float GetWaterHeightAtPosition(Vector3 position)
        {
            return GetWaterHeightAtPosition(new float3(position.x, position.y, position.z));
        }

        public float GetWaterHeightAtPosition(float3 position)
        {
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            double3 aupOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double2 absoluteXZ = new double2(
                (double)position.x + aupOffset.x,
                (double)position.z + aupOffset.z);
            float waveOffset = SampleWeatherGerstnerHeight(
                absoluteXZ,
                in weatherSnapshot,
                ResolveAuthorityGerstnerWaveBudget());
            return ResolveCinematicWaterLevelY() + waveOffset;
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
            float3 axisDirection = DominantAxisOrDefault(rawDirection, new float3(0f, 0f, 1f));
            float clampedConeDegrees = math.clamp(coneDegrees, 1f, 89f);
            float cone01 = clampedConeDegrees * 0.011111111f;
            float safeRadius = math.max(0.01f, radius);
            float radiusSq = safeRadius * safeRadius;
            _thrusterFlowBuffer[slot] = new ActiveThrusterFlow
            {
                PositionWS = new float3(position.x, position.y, position.z),
                DirectionWS = axisDirection,
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
            out NativeArray<float4> maelstroms,
            out int activeCount,
            out Vector4 maelstromMeta)
        {
            maelstroms = _activeMaelstroms;
            activeCount = _activeMaelstromCount;
            maelstromMeta = _activeMaelstromMeta;
            return maelstroms.IsCreated && activeCount > 0;
        }

        public bool TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow> whirlpools, out int activeCount)
        {
            whirlpools = _activeWhirlpools;
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
        /// <param name="axis">Signed swirl axis; dominant-axis approximation is used for stability.</param>
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
            float3 normalizedAxis = DominantAxisOrDefault(axis3, new float3(0f, 1f, 0f));
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

        private NativeArray<float3>         _positions;
        private NativeArray<float3>         _previousPositions;
        private NativeArray<byte>           _previousPositionValid;
        private NativeArray<float3>         _velocities;
        private NativeArray<float3>         _angularVelocities;
        private NativeArray<float3>         _upVectors;
        private NativeArray<float3>         _surfaceUpVectors;
        private NativeArray<BuoyancyParams> _params;
        private NativeArray<float>          _waveOffsets;
        private NativeArray<byte>           _sleepMask;
        private NativeArray<GerstnerWaveComponent> _gerstnerWaves;
        private NativeArray<float>          _gpuBuoyancyForcesY;
        private NativeArray<float3>         _resultForces;
        private NativeArray<float3>         _resultTorques;
        private NativeArray<OceanSurfaceTelemetryEntry> _oceanSurfaceTelemetry;
        private NativeArray<FluidImpactEvent> _impactEventScratch;
        private NativeArray<int> _impactEventFlags;
        private NativeArray<GpuBuoyancyObjectData> _gpuBuoyancyObjectDataUpload;
        private NativeArray<float4> _gpuBuoyancyReadback;
        private NativeArray<float> _brineHeights;
        private NativeArray<float> _brineDensityMultipliers;
        private NativeArray<int2> _brineCartographySectors;
        private NativeArray<byte> _brineFlags;
        private NativeArray<GpuHeatSourceData> _gpuAbyssalHeatSourceUpload;
        private NativeArray<ActiveThrusterFlow> _activeThrusterFlows;
        private NativeArray<WhirlpoolFlow> _activeWhirlpools;
        private NativeArray<float4> _activeMaelstroms;
        private NativeArray<MaelstromTelemetryEntry> _maelstromTelemetry;
        private NativeArray<FluidViscosityRegion> _activeViscosityRegions;
        private NativeArray<float> _viscosityGradientLut;
        private NativeArray<float3> _prebakedVectorNoiseField;
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
        private bool _oceanSurfaceWaveGlobalsValid;
        private int _oceanSurfaceTelemetryWriteIndex;
        private int _lastOceanSurfaceDumpFrame = -1;
        private uint _lastOriginShiftSequence;
        private Vector3 _pendingOriginShiftOffset;
        private VaultGenerationHandle<FluidImpactEvent> _fluidImpactEventRingHandle;
        private NativeArray<FluidImpactEvent> _fluidImpactEventRing;
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
        // COLD ALLOC: Collider[64] — static nonalloc cavitation shockwave overlap buffer — owner: HectonFluidEngine
        private static readonly Collider[] s_CavitationShockwaveColliders = new Collider[CavitationShockwaveHitCapacity];
        // COLD ALLOC: Rigidbody[64] — static deduplicated cavitation shockwave rigidbody targets — owner: HectonFluidEngine
        private static readonly Rigidbody[] s_CavitationShockwaveRigidbodies = new Rigidbody[CavitationShockwaveHitCapacity];
        private int _cavitationBurstCount;
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
        private int _lodFrameCounter;
        private float _observerResolveRetryTimer;
        private const float ObserverResolveRetryInterval = 1f;
        private const int MaxNativeCapacityGrowthIterations = 16;
        private GraphicsBuffer _gpuBuoyancyPositionBuffer;
        private GraphicsBuffer _gpuBuoyancyParamBuffer;
        private GraphicsBuffer _gpuBuoyancyResultBuffer;
        private AsyncGPUReadbackRequest[] _gpuReadbackRequests;
        private int[] _gpuReadbackCounts;
        private bool[] _gpuReadbackActive;
        private int _gpuReadbackWriteIndex;
        private bool _hasGpuBuoyancyData;
        private int _gpuBuoyancyKernel = -1;
        private GraphicsBuffer _gpuAbyssalFlowResultBuffer;
        private GraphicsBuffer _gpuAbyssalHeatSourceBuffer;
        private RenderTexture _gpuAbyssalFlowTextureA;
        private RenderTexture _gpuAbyssalFlowTextureB;
        private RenderTexture _gpuAbyssalFlowReadTexture;
        private RenderTexture _gpuAbyssalFlowWriteTexture;
        private RTHandle _gpuAbyssalFlowTextureAHandle;
        private RTHandle _gpuAbyssalFlowTextureBHandle;
        private IDataVault _dataVault;
        private ISimulationBucketer _simulationBucketer;
        private IWeatherService _weatherService;
        private HectonCelestialEngine _celestialEngine;
        private HectonMapMagicVegetationBridge _terrainBridge;
        private WorldProceduralFieldSampler _proceduralFieldSampler;
        private SargassumGlobalDragManager _sargassumDragRuntime;
        private ResourceDistributionDirector _resourceDistributionRuntime;
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
        private GraphicsBuffer _dynamicWakeBuffer;
        private GraphicsBuffer _dynamicWakeVectorBuffer;
        private VaultGenerationHandle<float4> _dynamicWakeBufferHandle;
        private VaultGenerationHandle<float4> _dynamicWakeVectorBufferHandle;
        private GraphicsBuffer _gpuSplashdownImpulseBuffer;
        private Texture3D _emptyFluidAdvectionTexture;
        private RTHandle _emptyFluidAdvectionTextureHandle;
        private Texture _cachedFluidAdvectionFlowHandleSource;
        private RTHandle _cachedFluidAdvectionFlowHandle;
        private Texture _cachedFluidAdvectionSdfHandleSource;
        private RTHandle _cachedFluidAdvectionSdfHandle;
        private NativeArray<AdvectedSilt> _advectedSiltUpload;
        private NativeArray<AdvectedBubble> _advectedBubbleUpload;
        private NativeArray<AdvectedDebris> _advectedDebrisUpload;
        private NativeArray<float4> _emptyAbyssalFlowUpload;
        private NativeArray<FluidAdvectionTelemetryEntry> _fluidAdvectionTelemetry;
        private NativeArray<float4> _splashdownImpulseUpload;
        private NativeArray<int> _splashdownImpulseStats;
        private int _activeAdvectedSiltCount;
        private int _activeAdvectedBubbleCount;
        private int _activeAdvectedDebrisCount;
        private int _advectedBubbleWriteCursor;
        private int _advectedDebrisWriteCursor;
        private int _fluidAdvectionKernel = -1;
        private int _fluidAdvectionBufferParity;
        private int _fluidAdvectionTelemetryCursor;
        private int _lastFluidAdvectionTelemetryFrame = -1;
        private bool _fluidAdvectionTelemetryDumped;
        private bool _fluidAdvectionStateReady;
        private bool _fluidAdvectionRenderGraphQueued;
        private uint _lastProcessedFluidAdvectionAupShiftFrameId;
        private float3 _pendingFluidAdvectionRuntimeShift;
        private Vector4 _lastAbyssalGridResolution;
        private Vector4 _lastAbyssalFlowCenter;
        private Vector4 _lastAbyssalFlowSpacing;
        private Vector4 _lastAbyssalFlowTextureSpacing;
        private bool _hasAbyssalFlowTexture;
        private bool _abyssalFlowPublicationClearIssued;
        private JobHandle _splashdownImpulseJobHandle;
        private bool _splashdownImpulseJobActive;
        private bool _splashdownImpulseUploaded;
        private int _splashdownImpulseScheduleFrame = -1;
        private float3 _splashdownImpulsePositionWS;
        private float _splashdownImpulseRemainingSeconds;
        private float _splashdownImpulseDurationSeconds;
        private int _lastSplashdownFluidImpulseCount;
        private ushort _lastProcessedSplashdownSequence;
        private uint _lastProcessedSplashdownFrame;
        private uint _lastProcessedSplashdownSourceHash;
        private bool _splashdownImpactConsumed;
        private uint _splashdownImpulseFlags;
        private int _gpuAbyssalUpdateKernel = -1;
        private int _gpuAbyssalTextureUpdateKernel = -1;
        private int _gpuAbyssalWakeKernel = -1;
        private int _gpuAbyssalVortexKernel = -1;
        private int _abyssalVortexImpulseCount;
        private int _abyssalVortexImpulseWriteIndex;
        private float _lastAbyssalVortexImpulseAgeFixedTime = -1f;
        private float _lastAbyssalFlowDispatchFixedTime = float.NegativeInfinity;
        private NativeArray<AbyssalFlowTelemetryEntry> _abyssalFlowTelemetry;
        private int _abyssalFlowTelemetryCursor;
        private bool _abyssalFlowTelemetryDumped;
        private int _maelstromTelemetryCursor;
        private bool _maelstromTelemetryDumped;
        private float _nextMaelstromAudioTime;
        private float _nextMaelstromDamageTime;
        private bool _fluidRuntimeRegistered;
        private bool _fixedTickRegistered;
        private bool _postFixedRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _scalabilityListenerRegistered;
        private IPlayerRuntimeContext _playerRuntime;
        private ISubmarineRuntimeContext _submarineRuntime;
        private HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;
        private int _cachedScalabilityTierFrame = int.MinValue;
        private byte _cachedHighScalabilityTier;

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

            MathGuard.Initialize();
            _dataVault = GlobalRegistry.DataVault;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
            CacheFluidRuntimeServicesCold();
            RefreshRuntimeActorContextsIfMissing();

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
            if (gpuBuoyancyCompute != null)
                _gpuBuoyancyKernel = gpuBuoyancyCompute.FindKernel("EvaluateBuoyancy");
            if (abyssalFlowFieldCompute != null)
            {
                _gpuAbyssalUpdateKernel = abyssalFlowFieldCompute.FindKernel("UpdateAbyssalFlowField");
                _gpuAbyssalTextureUpdateKernel = abyssalFlowFieldCompute.FindKernel("UpdateAbyssalFlowTexture");
                _gpuAbyssalWakeKernel = abyssalFlowFieldCompute.FindKernel("InjectAbyssalWakeTexture");
                _gpuAbyssalVortexKernel = abyssalFlowFieldCompute.FindKernel("InjectAbyssalVortexTexture");
            }

            if (fluidAdvectionCompute != null)
                _fluidAdvectionKernel = fluidAdvectionCompute.FindKernel("AdvectFluidParticles");

            _gpuReadbackRequests = new AsyncGPUReadbackRequest[GpuReadbackRingSize]; // COLD ALLOC: AsyncGPUReadbackRequest[3] — fixed GPU buoyancy readback ring state — owner: HectonFluidEngine
            _gpuReadbackCounts = new int[GpuReadbackRingSize]; // COLD ALLOC: int[3] — GPU buoyancy readback element counts — owner: HectonFluidEngine
            _gpuReadbackActive = new bool[GpuReadbackRingSize]; // COLD ALLOC: bool[3] — GPU buoyancy readback slot activity — owner: HectonFluidEngine
            EnsureAbyssalFlowNativeState();
            EnsureFluidAdvectionState();
            EnsurePrebakedVectorNoiseField();
            PublishCurrentWaterLevelUniform();
        }

        private void OnEnable()
        {
            EnsurePrebakedVectorNoiseField();
            _dataVault = GlobalRegistry.DataVault;
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
            CacheFluidRuntimeServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityListener();
            _cachedScalabilityTierFrame = int.MinValue;
            RefreshRuntimeActorContextsIfMissing();

            if (Application.isPlaying && !_fluidRuntimeRegistered)
            {
                HectonFluidEngine registeredFluid = GlobalRegistry.Fluid;
                if (registeredFluid != null && !ReferenceEquals(registeredFluid, this))
                {
                    Destroy(gameObject);
                    return;
                }

                GlobalRegistry.RegisterFluidRuntime(this);
                _fluidRuntimeRegistered = ReferenceEquals(GlobalRegistry.Fluid, this);
                if (_fluidRuntimeRegistered)
                    s_runtimeInstance = this;
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_fixedTickRegistered)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
                _fixedTickRegistered = GlobalRegistry.FixedTickables.Contains(this);
            }

            if (!_postFixedRegistered)
            {
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
                _postFixedRegistered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }

            if (!_originShiftRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftRegistered = true;
            }
        }

        private void OnDisable()
        {
            TryUnregisterScalabilityListener();
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
            DisposeNativeArrays();
            DisposeFluidAdvectionState();
            _simulationBucketer = null;
            _dataVault = null;
            ClearCachedFluidRuntimeServices();
            _playerRuntime = null;
            _submarineRuntime = null;
            _cachedScalabilityTierFrame = int.MinValue;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastOriginShiftSequence = shiftData.Sequence;
            if (shiftData.ShiftOffset.sqrMagnitude <= 0.000001f)
                return;

            if (_scheduledBuoyancyJobActive)
            {
                _pendingOriginShiftOffset += shiftData.ShiftOffset;
                _hasPendingOriginShiftRebase = true;
                return;
            }

            ApplyOriginShiftRebase(shiftData.ShiftOffset);
        }

        private void ApplyOriginShiftRebase(Vector3 shiftOffset)
        {
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

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

        private void OnDestroy()
        {
            TryUnregisterScalabilityListener();
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
            _simulationBucketer = null;
            _dataVault = null;
            ClearCachedFluidRuntimeServices();
            _playerRuntime = null;
            _submarineRuntime = null;
            _cachedScalabilityTierFrame = int.MinValue;
        }

        private void CacheFluidRuntimeServicesCold()
        {
            _weatherService = GlobalRegistry.Weather;
            _celestialEngine = GlobalRegistry.CelestialEngine;
            _terrainBridge = GlobalRegistry.MapMagicVegetation;
            _proceduralFieldSampler = GlobalRegistry.ProceduralFieldSampler;
            _sargassumDragRuntime = GlobalRegistry.SargassumDrag;
            _resourceDistributionRuntime = GlobalRegistry.ResourceDistribution;
        }

        private void ClearCachedFluidRuntimeServices()
        {
            _weatherService = null;
            _celestialEngine = null;
            _terrainBridge = null;
            _proceduralFieldSampler = null;
            _sargassumDragRuntime = null;
            _resourceDistributionRuntime = null;
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

        private void TryRegisterScalabilityListener()
        {
            if (_scalabilityListenerRegistered || !Application.isPlaying)
                return;

            ScalabilityEvents.Register(this);
            _scalabilityListenerRegistered = true;
        }

        private void TryUnregisterScalabilityListener()
        {
            if (!_scalabilityListenerRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityListenerRegistered = false;
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedScalabilityTier = payload.CurrentQualityTier;
            _cachedScalabilityTierFrame = int.MinValue;
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
                    ResetFluidVaultGenerationHandles();
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketer = currentService as ISimulationBucketer;
                    _cachedScalabilityTierFrame = int.MinValue;
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    _weatherService = currentService as IWeatherService;
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    _celestialEngine = currentService as HectonCelestialEngine;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _terrainBridge = currentService as HectonMapMagicVegetationBridge;
                    break;
                case GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime:
                    _proceduralFieldSampler = currentService as WorldProceduralFieldSampler;
                    break;
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    _sargassumDragRuntime = currentService as SargassumGlobalDragManager;
                    break;
                case GlobalRegistryServiceSlot.ResourceDistributionRuntime:
                    _resourceDistributionRuntime = currentService as ResourceDistributionDirector;
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
        public void Register(BuoyancyObject obj)
        {
            if (obj == null || obj.Body == null) return;

            if (ContainsRegisteredObject(obj))
                return;

            _objects.Add(obj);
            _bodies.Add(obj.Body);

            UpdateDiagnostics();
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

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
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
            bool textureCreated = flowFieldTexture is RenderTexture renderTexture && renderTexture.IsCreated();
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
        ///   1. Resize NativeArrays esli count > capacity (Capacity Doubling)
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
            if (vault == null || vault.IsAllocationLocked)
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
            if (vault.IsAllocationLocked)
            {
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
            }
            else
            {
                if (!OpenOrAcquireFluidVaultBuffer(
                        ref _sharedGerstnerWavesHandle,
                        BufferID.OceanGerstnerWaves,
                        MaxGerstnerWaveCount,
                        NativeArrayOptions.ClearMemory,
                        out sharedWaves) ||
                    !OpenOrAcquireFluidVaultBuffer(
                        ref _sharedGerstnerMetaHandle,
                        BufferID.OceanGerstnerWaveMeta,
                        1,
                        NativeArrayOptions.ClearMemory,
                        out sharedMeta))
                {
                    return;
                }
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
                ClearOceanSurfaceWaveUniforms();
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
            SetOceanSurfaceWaveGlobalIfChanged(
                _OceanSurfaceWave0AId,
                _OceanSurfaceWave0BId,
                wave0,
                activeWaveCount > 0,
                ref _lastOceanSurfaceWave0A,
                ref _lastOceanSurfaceWave0B);
            SetOceanSurfaceWaveGlobalIfChanged(
                _OceanSurfaceWave1AId,
                _OceanSurfaceWave1BId,
                wave1,
                activeWaveCount > 1,
                ref _lastOceanSurfaceWave1A,
                ref _lastOceanSurfaceWave1B);
            SetOceanSurfaceWaveGlobalIfChanged(
                _OceanSurfaceWave2AId,
                _OceanSurfaceWave2BId,
                wave2,
                activeWaveCount > 2,
                ref _lastOceanSurfaceWave2A,
                ref _lastOceanSurfaceWave2B);
            _oceanSurfaceWaveGlobalsValid = true;
            Shader.SetGlobalVector(
                _OceanSurfaceWaveMetaId,
                new Vector4(activeWaveCount, math.max(0f, timeSeconds), sleepCount, 0f));
        }

        private void SetOceanSurfaceWaveGlobalIfChanged(
            int waveAId,
            int waveBId,
            in GerstnerWaveComponent wave,
            bool active,
            ref Vector4 lastWaveA,
            ref Vector4 lastWaveB)
        {
            Vector4 waveA = new Vector4(wave.DirectionXZ.x, wave.DirectionXZ.y, wave.Amplitude, wave.Wavelength);
            Vector4 waveB = new Vector4(wave.Steepness, wave.PhaseOffset, wave.SpeedMultiplier, active ? 1f : 0f);
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

        private void ClearOceanSurfaceWaveUniformsIfOwner()
        {
            if (!Application.isPlaying || _fluidRuntimeRegistered || ReferenceEquals(GlobalRegistry.Fluid, this))
                ClearOceanSurfaceWaveUniforms();
        }

        private static int ResolveGerstnerWaveBudget(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                    return 12;
                case HectonQualityTier.Ultra:
                    return MaxGerstnerWaveCount;
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return 4;
                case HectonQualityTier.Mid:
                    return 8;
                case HectonQualityTier.Unknown:
                default:
                    return 1;
            }
        }

        private static int ResolveAuthorityGerstnerWaveBudget()
        {
            return ResolveGerstnerWaveBudget(AuthorityFluidWaveTier);
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
            math.sincos(angle, out float directionSin, out float directionCos);
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

        private bool TryResolveTerrainHeightPayload(out HectonMapMagicVegetationBridge.TerrainHeightSamplePayload payload)
        {
            payload = default;
            HectonMapMagicVegetationBridge bridge = _terrainBridge;
            if (bridge == null)
                return false;

            if (lodObserver != null &&
                bridge.TryGetHeightSamplePayload(lodObserver.position.x, lodObserver.position.z, out payload))
            {
                return true;
            }

            return bridge.TryGetActiveHeightSamplePayload(out payload);
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
            _oceanSurfaceTelemetry[writeIndex] = new OceanSurfaceTelemetryEntry
            {
                FrameIndex = (uint)Time.frameCount,
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

        private void DumpOceanSurfaceTelemetry()
        {
            if (!_oceanSurfaceTelemetry.IsCreated ||
                _oceanSurfaceTelemetry.Length == 0 ||
                _lastOceanSurfaceDumpFrame == Time.frameCount)
            {
                return;
            }

            _lastOceanSurfaceDumpFrame = Time.frameCount;
            string absolutePath = Path.Combine(Application.dataPath, "..", OceanSurfaceDumpPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(_oceanSurfaceTelemetryWriteIndex);
                writer.Write(_oceanSurfaceTelemetry.Length);
                for (int i = 0; i < _oceanSurfaceTelemetry.Length; i++)
                {
                    OceanSurfaceTelemetryEntry entry = _oceanSurfaceTelemetry[i];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.OriginShiftSequence);
                    writer.Write(entry.ActiveFloaters);
                    writer.Write(entry.SleepingFloaters);
                    writer.Write(entry.WaveOctaves);
                    writer.Write(entry.TerrainRevision);
                    writer.Write(entry.WaterLevelY);
                    writer.Write(entry.MinSurfaceOffset);
                    writer.Write(entry.MaxSurfaceOffset);
                    writer.Write(entry.ObserverWS.x);
                    writer.Write(entry.ObserverWS.y);
                    writer.Write(entry.ObserverWS.z);
                    writer.Write(entry.WindWS.x);
                    writer.Write(entry.WindWS.y);
                    writer.Write(entry.WindWS.z);
                }
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            using (ProfilerRegistry.PhysicsTick.Auto())
            {
            float cinematicWaterLevel = PublishCurrentWaterLevelUniform();

            if (!TryDrainScheduledBuoyancyJob())
                return;

            if (lodObserver == null)
            {
                _observerResolveRetryTimer -= fixedDeltaTime;
                if (_observerResolveRetryTimer <= 0f)
                    TryResolveObserver(force: false);
            }

            WeatherRuntimeSnapshot abyssalWeatherSnapshot = ResolveWeatherSnapshot();
            _resolvedGiantWakeCurrent = ResolveGiantWakeCurrentBase();
            _debugGiantWakeCurrent = new Vector3(_resolvedGiantWakeCurrent.x, _resolvedGiantWakeCurrent.y, _resolvedGiantWakeCurrent.z);
            TryCompleteSplashdownImpulseJobForUpload();
            DrainSplashdownFluidSignals(cinematicWaterLevel);
            UpdateSplashdownImpulseState(fixedDeltaTime);
            TryCompleteSplashdownImpulseJobForUpload();
            TryDispatchGpuAbyssalFlowField(abyssalWeatherSnapshot, cinematicWaterLevel, fixedDeltaTime);

            int count = _objects.Count;
            if (count == 0)
            {
                _lastOceanSleepCount = 0;
                PublishOceanSurfaceWaveUniformsFromWeather(in abyssalWeatherSnapshot, 0);
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
            if (count > _nativeCapacity)
            {
                ReallocateNativeArrays(count);
            }

            // ── 2. Gather (mozhet umenshit _objects.Count pri ochistke null) ──
            GatherData(cinematicWaterLevel);

            // Pereschityvaem count posle ochistki destroyed obektov
            count = _objects.Count;
            if (count == 0)
            {
                _lastOceanSleepCount = 0;
                PublishOceanSurfaceWaveUniformsFromWeather(in abyssalWeatherSnapshot, 0);
                ReleaseIdleNativeBuffersIfNeeded();
                return;
            }

            // ── 3. Schedule Job ──
            using (_jobScheduleProfilerMarker.Auto())
            {
            for (int i = 0; i < count; i++)
                _scheduledBodies[i] = _bodies[i];

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            _resolvedGiantWakeCurrent = ResolveGiantWakeCurrentBase();
            _debugGiantWakeCurrent = new Vector3(_resolvedGiantWakeCurrent.x, _resolvedGiantWakeCurrent.y, _resolvedGiantWakeCurrent.z);
            PopulateGerstnerWaveData(in weatherSnapshot, out int activeWaveCount, out float maxWaveEnvelope);
            bool hasTerrainPayload = TryResolveTerrainHeightPayload(out HectonMapMagicVegetationBridge.TerrainHeightSamplePayload terrainPayload);
            CopyAnalyticalFlowInputsToNative();
            bool gpuSurfaceParityEnabled = GpuBuoyancySurfaceParityAvailable && enableGpuBuoyancySampling;
            if (gpuSurfaceParityEnabled)
            {
                ConsumeGpuBuoyancyReadbacks();
                TryDispatchGpuBuoyancySampling(weatherSnapshot, count, cinematicWaterLevel);
            }
            var vectorNoiseField = _prebakedVectorNoiseField.IsCreated
                ? _prebakedVectorNoiseField
                : default;
            int vectorNoiseLength = _prebakedVectorNoiseField.IsCreated ? _prebakedVectorNoiseField.Length : 0;
            double3 vectorNoiseAupOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double2 waveAupOffsetXZ = new double2(vectorNoiseAupOffset.x, vectorNoiseAupOffset.z);
            byte highScalabilityTier = AuthorityFluidHighMathTier;

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
                    TerrainHeightSamples = hasTerrainPayload ? terrainPayload.HeightSamples : default,
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
                    CalculateSurfaceNormals = highScalabilityTier
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
                enableTidalShearZones = (highScalabilityTier != 0 && enableTidalShearZones) ? (byte)1 : (byte)0,
                tidalShearTorqueStrength = tidalShearTorqueStrength,
                tidalShearFrequency = tidalShearFrequency,
                time             = math.isfinite(weatherSnapshot.CurrentMeta.TimeAccumulator) &&
                                   weatherSnapshot.CurrentMeta.TimeAccumulator > 0f
                    ? weatherSnapshot.CurrentMeta.TimeAccumulator
                    : Time.unscaledTime,
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
                highScalabilityTier = highScalabilityTier,
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
            EnsureFluidAdvectionState();
            DrainFluidAdvectionSignals();
            bool fluidAdvectionReady = IsFluidAdvectionReady();
            bool hasAdvectionParticles = _activeAdvectedSiltCount > 0 ||
                                         _activeAdvectedBubbleCount > 0 ||
                                         _activeAdvectedDebrisCount > 0;
            _fluidAdvectionRenderGraphQueued = fluidAdvectionReady &&
                                               hasAdvectionParticles &&
                                               _fluidAdvectionKernel >= 0;

            WriteFluidAdvectionTelemetry();
            TryDrainScheduledBuoyancyJob();
        }

        public bool TryBuildFluidAdvectionRenderGraphPayload(out FluidAdvectionRenderGraphPayload payload)
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
                advectionQualityWeight,
                out GraphicsBuffer dynamicWakeBuffer,
                out GraphicsBuffer dynamicWakeVectorBuffer,
                out Vector4 dynamicWakeParams);

            Texture resolvedFlowTexture = hasFlowTexture ? flowTexture : _emptyFluidAdvectionTexture;
            Texture resolvedSdfTexture = sdfTexture != null ? sdfTexture : _emptyFluidAdvectionTexture;
            RTHandle resolvedFlowTextureHandle = ResolveFluidAdvectionFlowTextureHandle(resolvedFlowTexture);
            RTHandle resolvedSdfTextureHandle = ResolveFluidAdvectionSdfTextureHandle(resolvedSdfTexture);

            payload = new FluidAdvectionRenderGraphPayload
            {
                Compute = fluidAdvectionCompute,
                Kernel = _fluidAdvectionKernel,
                DispatchGroups = (maxCount + FluidAdvectionThreadGroupSize - 1) / FluidAdvectionThreadGroupSize,
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

        private void DrainSplashdownFluidSignals(float cinematicWaterLevel)
        {
            if (_splashdownImpactConsumed)
                return;

            int frame = Time.frameCount;
            if (_lastProcessedSplashdownFrame == (uint)frame)
                return;

            _lastProcessedSplashdownFrame = (uint)frame;
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
            float3 runtimePosition = signal.CapsuleAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimePosition)))
            {
                _splashdownImpulseFlags |= SplashdownImpulseInvalidInputFlag;
                DumpAbyssalFlowTelemetryOnce(_splashdownImpulseFlags);
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SplashdownFluidImpulseContextHash));
                return false;
            }

            runtimePosition.y = math.min(runtimePosition.y, cinematicWaterLevel - 0.25f);
            bool lowTier = ResolveAuthorityLowFluidMathTier();
            int queuedBubbles = QueueSplashdownBubbleRing(runtimePosition, lowTier);
            uint flags = lowTier ? SplashdownImpulseLowTierFlag : 0u;

            if (lowTier)
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

            if (!ScheduleSplashdownImpulseField(runtimePosition, flowCenter))
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

        private static bool ResolveAuthorityLowFluidMathTier()
        {
            return AuthorityFluidLowMathTier != 0;
        }

        private int QueueSplashdownBubbleRing(float3 runtimePosition, bool lowTier)
        {
            EnsureFluidAdvectionState();
            if (!IsFluidAdvectionReady() || !_advectedBubbleUpload.IsCreated)
                return 0;

            ClearPendingFluidAdvectionShiftIfNoActiveParticles();
            int safeCount = math.min(SplashdownBubbleCount, MaxAdvectedBubbleCount);
            float spawnRadius = SplashdownBubbleSpawnRadiusMeters * (lowTier ? 0.85f : 1.15f);

            for (int i = 0; i < safeCount; i++)
            {
                int slot = _advectedBubbleWriteCursor;
                _advectedBubbleWriteCursor = (_advectedBubbleWriteCursor + 1) % MaxAdvectedBubbleCount;
                _activeAdvectedBubbleCount = math.min(MaxAdvectedBubbleCount, _activeAdvectedBubbleCount + 1);

                float phase = (slot + i) * SplashdownGoldenAngleRadians;
                math.sincos(phase, out float sin, out float cos);
                float ringBand = 1f + ((slot & 7) * 0.045f);
                float3 offset = new float3(sin * spawnRadius * ringBand, HashToSignedUnit((uint)slot) * 0.18f, cos * spawnRadius * ringBand);
                float3 velocity = ResolveSplashdownBubbleVelocity(offset, lowTier);
                AdvectedBubble bubble = new AdvectedBubble
                {
                    PositionWS = runtimePosition + offset,
                    Life = 1f,
                    VelocityWS = velocity,
                    Flags = AdvectedBubbleActiveFlag
                };
                _advectedBubbleUpload[slot] = bubble;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_advectedBubbleBufferA, _advectedBubbleUpload, MaxAdvectedBubbleCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_advectedBubbleBufferB, _advectedBubbleUpload, MaxAdvectedBubbleCount);
            return safeCount;
        }

        private static float3 ResolveSplashdownBubbleVelocity(float3 offset, bool lowTier)
        {
            float3 lifted = offset;
            lifted.y += SplashdownBubbleUpwardBiasMeters;
            float lengthSq = math.lengthsq(lifted);
            float3 direction = lengthSq > 0.0001f
                ? lifted * math.rsqrt(math.max(lengthSq, 0.0001f))
                : new float3(0f, 1f, 0f);
            float gain = SplashdownImpulseStrength * math.rcp(math.max(math.lengthsq(offset), 1f));
            float3 velocity = direction * gain;
            float maxVelocity = lowTier ? SplashdownBubbleLowTierMaxVelocityMetersPerSecond : SplashdownImpulseMaxVelocityMetersPerSecond;
            float velocitySq = math.lengthsq(velocity);
            float maxVelocitySq = maxVelocity * maxVelocity;
            if (velocitySq > maxVelocitySq)
                velocity *= maxVelocity * math.rsqrt(math.max(velocitySq, 0.0001f));

            return HectonAnalyticalFlowField.ResolveFiniteFloat3OrZero(velocity);
        }

        private bool ScheduleSplashdownImpulseField(float3 runtimePosition, float3 flowCenter)
        {
            if (_splashdownImpulseJobActive)
            {
                TryCompleteSplashdownImpulseJobForUpload();
                if (_splashdownImpulseJobActive)
                    return false;
            }

            EnsureSplashdownImpulseState();
            EnsureSplashdownImpulseGpuBuffer();
            if (!_splashdownImpulseUpload.IsCreated ||
                !_splashdownImpulseStats.IsCreated ||
                _gpuSplashdownImpulseBuffer == null ||
                !_gpuSplashdownImpulseBuffer.IsValid())
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
                RadiusMeters = SplashdownImpulseRadiusMeters,
                ImpulseStrength = SplashdownImpulseStrength,
                UpwardBiasMeters = SplashdownImpulseUpwardBiasMeters,
                MaxVelocityMetersPerSecond = SplashdownImpulseMaxVelocityMetersPerSecond,
                Resolution = AbyssalFlowTextureResolution
            };

            _splashdownImpulseJobHandle = job.Schedule();
            _splashdownImpulseJobActive = true;
            _splashdownImpulseUploaded = false;
            _splashdownImpulseScheduleFrame = Time.frameCount;
            return true;
        }

        private void TryCompleteSplashdownImpulseJobForUpload()
        {
            if (!_splashdownImpulseJobActive)
                return;

            if (!_splashdownImpulseJobHandle.IsCompleted || _splashdownImpulseScheduleFrame == Time.frameCount)
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
            if (_gpuSplashdownImpulseBuffer == null ||
                !_gpuSplashdownImpulseBuffer.IsValid() ||
                !_splashdownImpulseUpload.IsCreated)
            {
                _splashdownImpulseUploaded = false;
                return false;
            }

            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0 ||
                _gpuSplashdownImpulseBuffer.count < nodeCount ||
                _splashdownImpulseUpload.Length < nodeCount)
            {
                _splashdownImpulseUploaded = false;
                return false;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_gpuSplashdownImpulseBuffer, _splashdownImpulseUpload, nodeCount);
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
            return _gpuSplashdownImpulseBuffer != null && _gpuSplashdownImpulseBuffer.IsValid()
                ? _gpuSplashdownImpulseBuffer
                : _emptyAbyssalFlowBuffer;
        }

        private void PublishSplashdownFluidImpulseTelemetry(int count, uint flags)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                SplashdownFluidImpulseCountHash,
                SplashdownFluidImpulseContextHash ^ flags,
                math.max(0, count));
        }

        private void DrainFluidAdvectionSignals()
        {
            ConsumeFluidAdvectionAupShiftSignals();
            ClearPendingFluidAdvectionShiftIfNoActiveParticles();

            int drained = 0;
            while (drained < FluidAdvectionSignalDrainBudget &&
                   GlobalSignals.TryDequeueDebrisSpawn(out DebrisSpawnSignal signal))
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
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0f;
        }

        private static float SmoothFluidAdvectionQuality(float qualityWeight)
        {
            float quality = math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 0f;
            return quality * quality * (3f - 2f * quality);
        }

        private static Vector4 ResolveGlobalWakeParamsForFluidAdvection(float qualityWeight)
        {
            Vector4 wakeParams = Shader.GetGlobalVector(_GlobalWakeParamsId);
            if (!IsFiniteVector(wakeParams))
                return Vector4.zero;

            float quality = SmoothFluidAdvectionQuality(qualityWeight);
            float maxSlotLimit = math.lerp(DynamicWakeLowTierGpuCapacity, DynamicWakeGpuCapacity, quality);
            float slotLimit = math.clamp(wakeParams.x, 0f, maxSlotLimit);
            float activeCount = math.clamp(wakeParams.z, 0f, slotLimit);
            return new Vector4(
                slotLimit,
                1f - quality,
                activeCount,
                math.saturate(wakeParams.w));
        }

        public bool TryGetDynamicWakeGpuPayload(
            out GraphicsBuffer dynamicWakeBuffer,
            out GraphicsBuffer dynamicWakeVectorBuffer,
            out Vector4 dynamicWakeParams)
        {
            return TryGetDynamicWakeGpuPayload(
                ResolveFluidAdvectionQualityWeight(),
                out dynamicWakeBuffer,
                out dynamicWakeVectorBuffer,
                out dynamicWakeParams);
        }

        private bool TryGetDynamicWakeGpuPayload(
            float qualityWeight,
            out GraphicsBuffer dynamicWakeBuffer,
            out GraphicsBuffer dynamicWakeVectorBuffer,
            out Vector4 dynamicWakeParams)
        {
            dynamicWakeBuffer = _emptyAbyssalFlowBuffer;
            dynamicWakeVectorBuffer = _emptyAbyssalFlowBuffer;

            float quality = SmoothFluidAdvectionQuality(qualityWeight);
            dynamicWakeParams = ResolveGlobalWakeParamsForFluidAdvection(qualityWeight);
            if (dynamicWakeParams.z <= 0.5f)
            {
                dynamicWakeParams = Vector4.zero;
                return false;
            }

            if (!EnsureDynamicWakeGpuBuffers() ||
                !TryResolveDynamicWakeVaultBuffers(
                    out NativeArray<float4> dynamicWakes,
                    out NativeArray<float4> dynamicWakeVectors))
            {
                dynamicWakeParams = Vector4.zero;
                return false;
            }

            int uploadCount = math.min(DynamicWakeGpuCapacity, math.min(dynamicWakes.Length, dynamicWakeVectors.Length));
            if (uploadCount <= 0)
            {
                dynamicWakeParams = Vector4.zero;
                return false;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_dynamicWakeBuffer, dynamicWakes, uploadCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_dynamicWakeVectorBuffer, dynamicWakeVectors, uploadCount);

            float slotLimit = math.clamp(
                dynamicWakeParams.x,
                0f,
                math.min(uploadCount, math.lerp(DynamicWakeLowTierGpuCapacity, DynamicWakeGpuCapacity, quality)));
            float activeCount = math.clamp(dynamicWakeParams.z, 0f, slotLimit);
            dynamicWakeParams = new Vector4(slotLimit, 1f - quality, activeCount, math.saturate(dynamicWakeParams.w));
            dynamicWakeBuffer = _dynamicWakeBuffer;
            dynamicWakeVectorBuffer = _dynamicWakeVectorBuffer;
            return dynamicWakeBuffer != null &&
                   dynamicWakeVectorBuffer != null &&
                   dynamicWakeBuffer.IsValid() &&
                   dynamicWakeVectorBuffer.IsValid();
        }

        private bool EnsureDynamicWakeGpuBuffers()
        {
            if (_emptyAbyssalFlowBuffer == null || !_emptyAbyssalFlowBuffer.IsValid())
                return false;

            if (_dynamicWakeBuffer == null || !_dynamicWakeBuffer.IsValid())
                _dynamicWakeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(DynamicWakeGpuCapacity); // COLD ALLOC: GraphicsBuffer[16] - DataVault wake positions for dynamic VFX advection - owner: HectonFluidEngine
            if (_dynamicWakeVectorBuffer == null || !_dynamicWakeVectorBuffer.IsValid())
                _dynamicWakeVectorBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(DynamicWakeGpuCapacity); // COLD ALLOC: GraphicsBuffer[16] - DataVault wake vectors for dynamic VFX advection - owner: HectonFluidEngine

            return _dynamicWakeBuffer != null &&
                   _dynamicWakeVectorBuffer != null &&
                   _dynamicWakeBuffer.IsValid() &&
                   _dynamicWakeVectorBuffer.IsValid();
        }

        private bool TryResolveDynamicWakeVaultBuffers(
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
                if (vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle(BufferID.WakeGlobalBuffer, out _dynamicWakeBufferHandle))
                        return false;
                }
                else
                {
                    _dynamicWakeBufferHandle = vault.GetGenerationHandle<float4>(
                        BufferID.WakeGlobalBuffer,
                        DynamicWakeGpuCapacity,
                        SystemID.Fluid,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!IsVaultGenerationHandleCreated(in _dynamicWakeVectorBufferHandle))
            {
                if (vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle(BufferID.WakeVectorBuffer, out _dynamicWakeVectorBufferHandle))
                        return false;
                }
                else
                {
                    _dynamicWakeVectorBufferHandle = vault.GetGenerationHandle<float4>(
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
            EnsureFluidAdvectionState();
            if (!IsFluidAdvectionReady())
                return;

            int requestedQuantity = signal.Quantity;
            int quantity = math.clamp(requestedQuantity <= 0 ? 1 : requestedQuantity, 1, MaxAdvectedDebrisCount);
            float intensity = math.saturate(math.isfinite(signal.Intensity01) ? signal.Intensity01 : 0.25f);
            float3 runtimePosition = signal.PositionAup.ToRuntimeFloat3();
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

        private void EnsureFluidAdvectionState()
        {
            if (_fluidAdvectionStateReady && IsFluidAdvectionReady())
            {
                return;
            }

            if (!_advectedSiltUpload.IsCreated)
            {
                _advectedSiltUpload = new NativeArray<AdvectedSilt>(
                    MaxAdvectedSiltCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AdvectedSilt>[4096] - fixed GPU silt advection staging and black-box reset memory - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_advectedSiltUpload, NativeMemoryOwner, nameof(_advectedSiltUpload), NativeMemoryLifetime);
            }

            if (!_advectedBubbleUpload.IsCreated)
            {
                _advectedBubbleUpload = new NativeArray<AdvectedBubble>(
                    MaxAdvectedBubbleCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AdvectedBubble>[2000] - fixed GPU bubble advection staging memory - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_advectedBubbleUpload, NativeMemoryOwner, nameof(_advectedBubbleUpload), NativeMemoryLifetime);
            }

            if (!_advectedDebrisUpload.IsCreated)
            {
                _advectedDebrisUpload = new NativeArray<AdvectedDebris>(
                    MaxAdvectedDebrisCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AdvectedDebris>[1000] - fixed GPU debris advection staging memory - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_advectedDebrisUpload, NativeMemoryOwner, nameof(_advectedDebrisUpload), NativeMemoryLifetime);
            }

            if (!_emptyAbyssalFlowUpload.IsCreated)
            {
                _emptyAbyssalFlowUpload = new NativeArray<float4>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[1] - zero abyssal flow fallback upload - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_emptyAbyssalFlowUpload, NativeMemoryOwner, nameof(_emptyAbyssalFlowUpload), NativeMemoryLifetime);
            }

            if (!_fluidAdvectionTelemetry.IsCreated)
            {
                _fluidAdvectionTelemetry = new NativeArray<FluidAdvectionTelemetryEntry>(
                    FluidAdvectionTelemetryCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FluidAdvectionTelemetryEntry>[300] - fluid advection black-box ring - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_fluidAdvectionTelemetry, NativeMemoryOwner, nameof(_fluidAdvectionTelemetry), NativeMemoryLifetime);
            }

            EnsureEmptyFluidAdvectionTexture();
            EnsureFluidAdvectionBuffers();
            _fluidAdvectionStateReady = IsFluidAdvectionReady();
        }

        private bool HasFluidAdvectionNativeState()
        {
            return _advectedSiltUpload.IsCreated &&
                   _advectedBubbleUpload.IsCreated &&
                   _advectedDebrisUpload.IsCreated &&
                   _emptyAbyssalFlowUpload.IsCreated &&
                   _fluidAdvectionTelemetry.IsCreated;
        }

        private void EnsureFluidAdvectionBuffers()
        {
            if (_advectedSiltBufferA == null || !_advectedSiltBufferA.IsValid())
                _advectedSiltBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedSilt>(MaxAdvectedSiltCount); // COLD ALLOC: GraphicsBuffer[4096] - silt advection front buffer - owner: HectonFluidEngine
            if (_advectedSiltBufferB == null || !_advectedSiltBufferB.IsValid())
                _advectedSiltBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedSilt>(MaxAdvectedSiltCount); // COLD ALLOC: GraphicsBuffer[4096] - silt advection back buffer - owner: HectonFluidEngine
            if (_advectedBubbleBufferA == null || !_advectedBubbleBufferA.IsValid())
                _advectedBubbleBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedBubble>(MaxAdvectedBubbleCount); // COLD ALLOC: GraphicsBuffer[2000] - bubble advection front buffer - owner: HectonFluidEngine
            if (_advectedBubbleBufferB == null || !_advectedBubbleBufferB.IsValid())
                _advectedBubbleBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedBubble>(MaxAdvectedBubbleCount); // COLD ALLOC: GraphicsBuffer[2000] - bubble advection back buffer - owner: HectonFluidEngine
            if (_advectedDebrisBufferA == null || !_advectedDebrisBufferA.IsValid())
                _advectedDebrisBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedDebris>(MaxAdvectedDebrisCount); // COLD ALLOC: GraphicsBuffer[1000] - debris advection front buffer - owner: HectonFluidEngine
            if (_advectedDebrisBufferB == null || !_advectedDebrisBufferB.IsValid())
                _advectedDebrisBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedDebris>(MaxAdvectedDebrisCount); // COLD ALLOC: GraphicsBuffer[1000] - debris advection back buffer - owner: HectonFluidEngine
            if (_emptyAdvectedSiltBuffer == null || !_emptyAdvectedSiltBuffer.IsValid())
                _emptyAdvectedSiltBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedSilt>(1); // COLD ALLOC: GraphicsBuffer[1] - silt unbind fallback - owner: HectonFluidEngine
            if (_emptyAdvectedBubbleBuffer == null || !_emptyAdvectedBubbleBuffer.IsValid())
                _emptyAdvectedBubbleBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedBubble>(1); // COLD ALLOC: GraphicsBuffer[1] - bubble unbind fallback - owner: HectonFluidEngine
            if (_emptyAdvectedDebrisBuffer == null || !_emptyAdvectedDebrisBuffer.IsValid())
                _emptyAdvectedDebrisBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AdvectedDebris>(1); // COLD ALLOC: GraphicsBuffer[1] - debris unbind fallback - owner: HectonFluidEngine
            if (_emptyAbyssalFlowBuffer == null || !_emptyAbyssalFlowBuffer.IsValid())
                _emptyAbyssalFlowBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(1); // COLD ALLOC: GraphicsBuffer[1] - zero abyssal-flow fallback - owner: HectonFluidEngine

            GraphicsBufferUploadUtility.UploadNativeArray(_advectedSiltBufferA, _advectedSiltUpload, MaxAdvectedSiltCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_advectedSiltBufferB, _advectedSiltUpload, MaxAdvectedSiltCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_advectedBubbleBufferA, _advectedBubbleUpload, MaxAdvectedBubbleCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_advectedBubbleBufferB, _advectedBubbleUpload, MaxAdvectedBubbleCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_advectedDebrisBufferA, _advectedDebrisUpload, MaxAdvectedDebrisCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_advectedDebrisBufferB, _advectedDebrisUpload, MaxAdvectedDebrisCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_emptyAbyssalFlowBuffer, _emptyAbyssalFlowUpload, 1);
        }

        private void EnsureEmptyFluidAdvectionTexture()
        {
            if (_emptyFluidAdvectionTexture != null)
            {
                if (_emptyFluidAdvectionTextureHandle == null)
                    _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture);
                return;
            }

            _emptyFluidAdvectionTexture = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false)
            {
                name = "__HectonFluidAdvectionEmptyTex3D",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: Texture3D[1x1x1 RGBA32] - bound fallback for advection compute texture slots - owner: HectonFluidEngine
            _emptyFluidAdvectionTexture.SetPixel(0, 0, 0, Color.black);
            _emptyFluidAdvectionTexture.Apply(false, true);
            _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture);
        }

        private RTHandle ResolveFluidAdvectionFlowTextureHandle(Texture texture)
        {
            if (texture == null || ReferenceEquals(texture, _emptyFluidAdvectionTexture))
                return _emptyFluidAdvectionTextureHandle;

            if (ReferenceEquals(texture, _gpuAbyssalFlowTextureA))
            {
                if (_gpuAbyssalFlowTextureAHandle == null)
                    _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA);
                return _gpuAbyssalFlowTextureAHandle;
            }

            if (ReferenceEquals(texture, _gpuAbyssalFlowTextureB))
            {
                if (_gpuAbyssalFlowTextureBHandle == null)
                    _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB);
                return _gpuAbyssalFlowTextureBHandle;
            }

            if (!ReferenceEquals(texture, _cachedFluidAdvectionFlowHandleSource))
            {
                ReleaseRTHandle(ref _cachedFluidAdvectionFlowHandle);
                _cachedFluidAdvectionFlowHandleSource = texture;
                _cachedFluidAdvectionFlowHandle = RTHandles.Alloc(texture);
            }

            return _cachedFluidAdvectionFlowHandle;
        }

        private RTHandle ResolveFluidAdvectionSdfTextureHandle(Texture texture)
        {
            if (texture == null || ReferenceEquals(texture, _emptyFluidAdvectionTexture))
                return _emptyFluidAdvectionTextureHandle;

            if (!ReferenceEquals(texture, _cachedFluidAdvectionSdfHandleSource))
            {
                ReleaseRTHandle(ref _cachedFluidAdvectionSdfHandle);
                _cachedFluidAdvectionSdfHandleSource = texture;
                _cachedFluidAdvectionSdfHandle = RTHandles.Alloc(texture);
            }

            return _cachedFluidAdvectionSdfHandle;
        }

        private bool IsFluidAdvectionReady()
        {
            return fluidAdvectionCompute != null &&
                   _fluidAdvectionKernel >= 0 &&
                   HasFluidAdvectionNativeState() &&
                   _advectedSiltBufferA != null &&
                   _advectedSiltBufferB != null &&
                   _advectedBubbleBufferA != null &&
                   _advectedBubbleBufferB != null &&
                   _advectedDebrisBufferA != null &&
                   _advectedDebrisBufferB != null &&
                   _emptyAdvectedSiltBuffer != null &&
                   _emptyAdvectedBubbleBuffer != null &&
                   _emptyAdvectedDebrisBuffer != null &&
                   _emptyAbyssalFlowBuffer != null &&
                   _emptyFluidAdvectionTexture != null &&
                   _emptyFluidAdvectionTextureHandle != null &&
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

        private void UploadAdvectedBubble(int slot, in AdvectedBubble bubble)
        {
            if ((uint)slot >= MaxAdvectedBubbleCount || !_advectedBubbleUpload.IsCreated)
                return;

            _advectedBubbleUpload[slot] = bubble;
            UploadSingle(_advectedBubbleBufferA, slot, in bubble);
            UploadSingle(_advectedBubbleBufferB, slot, in bubble);
        }

        private void UploadAdvectedDebris(int slot, in AdvectedDebris debris)
        {
            if ((uint)slot >= MaxAdvectedDebrisCount || !_advectedDebrisUpload.IsCreated)
                return;

            _advectedDebrisUpload[slot] = debris;
            UploadSingle(_advectedDebrisBufferA, slot, in debris);
            UploadSingle(_advectedDebrisBufferB, slot, in debris);
        }

        private static void UploadSingle<T>(GraphicsBuffer buffer, int slot, in T value)
            where T : struct
        {
            if (buffer == null || !buffer.IsValid() || slot < 0 || slot >= buffer.count)
                return;

            var mapped = buffer.LockBufferForWrite<T>(slot, 1);
            mapped[0] = value;
            buffer.UnlockBufferAfterWrite<T>(1);
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

            int frame = Time.frameCount;
            if (_lastFluidAdvectionTelemetryFrame == frame)
                return;

            _lastFluidAdvectionTelemetryFrame = frame;
            int activeCount = _activeAdvectedSiltCount + _activeAdvectedBubbleCount + _activeAdvectedDebrisCount;
            int activeWakeCount = math.max(0, (int)ResolveGlobalWakeParamsForFluidAdvection(ResolveFluidAdvectionQualityWeight()).z);
            int index = _fluidAdvectionTelemetryCursor;
            uint flags = _fluidAdvectionRenderGraphQueued ? 1u : 0u;
            flags |= math.lengthsq(_pendingFluidAdvectionRuntimeShift) > 0.000001f ? 2u : 0u;
            flags |= activeWakeCount > 0 ? 4u : 0u;
            _fluidAdvectionTelemetry[index] = new FluidAdvectionTelemetryEntry
            {
                FrameIndex = (uint)frame,
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
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string dumpPath = Path.Combine(projectRoot, FluidAdvectionDumpRelativePath);
                WriteFluidAdvectionTelemetryDump(dumpPath, reasonFlags);
            }
            catch (System.Exception exception)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[HectonFluidEngine] Fluid advection telemetry dump failed: " + exception.Message, this);
#endif
            }
        }

        private void WriteFluidAdvectionTelemetryDump(string dumpPath, uint reasonFlags)
        {
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x41435654u);
                writer.Write(FluidAdvectionTelemetryCapacity);
                writer.Write(_fluidAdvectionTelemetryCursor);
                writer.Write(reasonFlags);
                for (int i = 0; i < _fluidAdvectionTelemetry.Length; i++)
                {
                    int index = (_fluidAdvectionTelemetryCursor + i) % _fluidAdvectionTelemetry.Length;
                    FluidAdvectionTelemetryEntry entry = _fluidAdvectionTelemetry[index];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.OriginShiftSequence);
                    writer.Write(entry.ActiveAdvectedParticles);
                    writer.Write(entry.SiltCount);
                    writer.Write(entry.BubbleCount);
                    writer.Write(entry.DebrisCount);
                    writer.Write(entry.ActiveTurbulenceWakes);
                    writer.Write(entry.Flags);
                    writer.Write(entry.StateHash);
                }
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
            WorldProceduralFieldSampler biomeFieldSampler = enableBiomeBuoyancyInfluence
                ? _proceduralFieldSampler
                : null;
            SargassumGlobalDragManager sargassumDrag = _sargassumDragRuntime;
            ResourceDistributionDirector brineDirector = _resourceDistributionRuntime;
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
                EnsureFluidImpactEventRing(allowAllocate: false) &&
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
                    if (TryEnqueueFluidImpactEvent(in impactEvent))
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
                    DumpOceanSurfaceTelemetry();

                if (forceFinite && sanitizedForce.sqrMagnitude > 0.0001f)
                {
                    PhysicsForceRouter.QueueAmbientForce(
                        rb,
                        sanitizedForce,
                        ForceMode.Force);
                }

                bool torqueFinite = TrySanitizePhysicsVector(torque, NonFiniteBuoyancyTorqueHash, out Vector3 sanitizedTorque);
                if (!torqueFinite)
                    DumpOceanSurfaceTelemetry();

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
            GlobalSignals.Publish(in signal);

            double3 absolutePosition = impactAup.ToAbsoluteDouble3();
            SplashEvent splashEvent = new SplashEvent
            {
                RuntimePosition = impactEvent.PositionWS,
                AbsoluteUniversePosition = new float3(
                    (float)absolutePosition.x,
                    (float)absolutePosition.y,
                    (float)absolutePosition.z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = impactSpeed,
                KineticEnergyJoules = 0.5f * math.max(0.001f, impactEvent.MassKg) * impactSpeed * impactSpeed,
                SubmersionFactor = math.saturate((impactEvent.SurfaceY - impactEvent.PositionWS.y) * math.rcp(math.max(0.01f, SplashDepthThresholdMeters))),
                SampleIndex = 0
            };
            FluidFeedbackEvents.PublishSplashQueued(in splashEvent);

            DebrisSpawnSignal debrisSignal = new DebrisSpawnSignal
            {
                PositionAup = impactAup,
                SpeciesHash = OceanSplashSignalHash,
                SourceEntityId = bodyId,
                Intensity01 = intensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindWaterSplash,
                Flags = 0
            };
            GlobalSignals.Publish(in debrisSignal);
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

            Vector3 safeDirection = DominantAxisOrDefault(direction, Vector3.back);
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

                EmitCavitationParticles(in burstEvent);
                ApplyCavitationShockwave(in burstEvent);
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
            int colliderCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                burstEvent.Position,
                burstEvent.Radius,
                s_CavitationShockwaveColliders,
                cavitationShockwaveLayers,
                QueryTriggerInteraction.Ignore);
            if (colliderCount <= 0)
                return;

            int rigidbodyCount = 0;
            for (int i = 0; i < colliderCount; i++)
            {
                Collider hitCollider = s_CavitationShockwaveColliders[i];
                s_CavitationShockwaveColliders[i] = null;
                if (hitCollider == null)
                    continue;

                Rigidbody candidateBody = hitCollider.attachedRigidbody;
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

                Vector3 radial = targetBody.worldCenterOfMass - burstEvent.Position;
                float radialDistanceSq = radial.sqrMagnitude;
                Vector3 radialDirection = radialDistanceSq > 0.000001f
                    ? DominantAxisOrDefault(radial, burstEvent.Direction)
                    : burstEvent.Direction;
                radialDirection += burstEvent.Direction * 0.25f;
                radialDirection.y += cavitationShockwaveVerticalLift;
                radialDirection = DominantAxisOrDefault(radialDirection, Vector3.up);

                float distance01 = math.saturate(1f - radialDistanceSq * burstEvent.InvRadiusSq);
                distance01 *= distance01;
                if (distance01 <= 0.0001f)
                    continue;

                float velocityChange = burstEvent.Acceleration * burstEvent.Intensity01 * distance01;
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

        private void ReallocateNativeArrays(int requiredCount)
        {
            requiredCount = math.max(requiredCount, 1);
            int newCapacity = math.max(128, _nativeCapacity * 2);
            int growthIterations = 0;

            while (newCapacity < requiredCount)
            {
                if (growthIterations >= MaxNativeCapacityGrowthIterations || newCapacity > (int.MaxValue / 2))
                {
                    newCapacity = math.max(newCapacity, requiredCount);
                    break;
                }

                newCapacity *= 2;
                growthIterations++;
            }

            DisposeNativeArrays();

            _positions     = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _previousPositions = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _previousPositionValid = new NativeArray<byte>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _velocities    = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _angularVelocities = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _upVectors = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _surfaceUpVectors = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _params        = new NativeArray<BuoyancyParams>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _waveOffsets   = new NativeArray<float>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _sleepMask = new NativeArray<byte>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _gerstnerWaves = new NativeArray<GerstnerWaveComponent>(MaxGerstnerWaveCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _gpuBuoyancyForcesY = new NativeArray<float>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _resultForces  = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _resultTorques = new NativeArray<float3>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _oceanSurfaceTelemetry = new NativeArray<OceanSurfaceTelemetryEntry>(OceanSurfaceTelemetryCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _impactEventScratch = new NativeArray<FluidImpactEvent>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _impactEventFlags = new NativeArray<int>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _gpuBuoyancyObjectDataUpload = new NativeArray<GpuBuoyancyObjectData>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            _gpuBuoyancyReadback = new NativeArray<float4>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _brineHeights = new NativeArray<float>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacity] - absolute brine plane heights per buoyancy lane - owner: HectonFluidEngine
            _brineDensityMultipliers = new NativeArray<float>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacity] - brine density multipliers per buoyancy lane - owner: HectonFluidEngine
            _brineCartographySectors = new NativeArray<int2>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int2>[capacity] - 50m brine sector id per buoyancy lane - owner: HectonFluidEngine
            _brineFlags = new NativeArray<byte>(newCapacity, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[capacity] - brine validity flags per buoyancy lane - owner: HectonFluidEngine
            EnsureAbyssalFlowNativeState();
            _activeThrusterFlows = new NativeArray<ActiveThrusterFlow>(MaxAnalyticalThrusterCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            if (!_activeWhirlpools.IsCreated)
            {
                _activeWhirlpools = new NativeArray<WhirlpoolFlow>(MaxAnalyticalWhirlpoolCount, Allocator.Persistent,
                                     NativeArrayOptions.ClearMemory);
            }
            _activeViscosityRegions = new NativeArray<FluidViscosityRegion>(MaxDynamicViscosityRegionCount, Allocator.Persistent,
                                 NativeArrayOptions.ClearMemory);
            _viscosityGradientLut = new NativeArray<float>(ViscosityGradientLutSize, Allocator.Persistent,
                                 NativeArrayOptions.UninitializedMemory);
            InitializeViscosityGradientLut();
            if (EnsureFluidImpactEventRing(allowAllocate: true))
                ClearFluidImpactEventRing(_fluidImpactEventRing);
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
            RegisterNativeMemorySentinel();
            EnsureSharedGerstnerDataVaultBuffers();
            _scheduledBodies = new Rigidbody[newCapacity];
            EnsureGpuBuoyancyBuffers(newCapacity);
            EnsureGpuAbyssalFlowBuffers();

            _nativeCapacity = newCapacity;
        }

        /// <summary>
        /// Osvobozhdaet NativeArrays. Vyzyvaetsya pri Destroy i Resize.
        /// </summary>
        private void DisposeNativeArrays(bool releaseAbyssalFlow = true)
        {
            JobHandle dependency = _scheduledBuoyancyJobActive ? _scheduledBuoyancyHandle : default;
            DisposeNativeArray(ref _positions, dependency);
            DisposeNativeArray(ref _previousPositions, dependency);
            DisposeNativeArray(ref _previousPositionValid, dependency);
            DisposeNativeArray(ref _velocities, dependency);
            DisposeNativeArray(ref _angularVelocities, dependency);
            DisposeNativeArray(ref _upVectors, dependency);
            DisposeNativeArray(ref _surfaceUpVectors, dependency);
            DisposeNativeArray(ref _params, dependency);
            DisposeNativeArray(ref _waveOffsets, dependency);
            DisposeNativeArray(ref _sleepMask, dependency);
            DisposeNativeArray(ref _gerstnerWaves, dependency);
            DisposeNativeArray(ref _gpuBuoyancyForcesY, dependency);
            DisposeNativeArray(ref _resultForces, dependency);
            DisposeNativeArray(ref _resultTorques, dependency);
            DisposeNativeArray(ref _oceanSurfaceTelemetry, dependency);
            DisposeNativeArray(ref _impactEventScratch, dependency);
            DisposeNativeArray(ref _impactEventFlags, dependency);
            DisposeNativeArray(ref _gpuBuoyancyObjectDataUpload, dependency);
            DisposeNativeArray(ref _gpuBuoyancyReadback, dependency);
            DisposeNativeArray(ref _brineHeights, dependency);
            DisposeNativeArray(ref _brineDensityMultipliers, dependency);
            DisposeNativeArray(ref _brineCartographySectors, dependency);
            DisposeNativeArray(ref _brineFlags, dependency);
            if (releaseAbyssalFlow)
            {
                DisposeNativeArray(ref _gpuAbyssalHeatSourceUpload, dependency);
                DisposeNativeArray(ref _abyssalFlowTelemetry, dependency);
                DisposeNativeArray(ref _maelstromTelemetry, dependency);
                DisposeNativeArray(ref _activeMaelstroms, dependency);
                DisposeSplashdownImpulseState();
            }
            DisposeNativeArray(ref _advectedSiltUpload, dependency);
            DisposeNativeArray(ref _advectedBubbleUpload, dependency);
            DisposeNativeArray(ref _advectedDebrisUpload, dependency);
            DisposeNativeArray(ref _fluidAdvectionTelemetry, dependency);
            _fluidAdvectionStateReady = false;
            _fluidAdvectionRenderGraphQueued = false;
            _fluidAdvectionTelemetryCursor = 0;
            _lastFluidAdvectionTelemetryFrame = -1;
            DisposeNativeArray(ref _activeThrusterFlows, dependency);
            DisposeNativeArray(ref _activeWhirlpools, dependency);
            DisposeNativeArray(ref _activeViscosityRegions, dependency);
            DisposeNativeArray(ref _viscosityGradientLut, dependency);
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
            ReleaseGpuBuoyancyBuffers();
            if (releaseAbyssalFlow)
                ReleaseGpuAbyssalFlowBuffers();
            _hasGpuBuoyancyData = false;

            _nativeCapacity = 0;
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_positions, NativeMemoryOwner, nameof(_positions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_previousPositions, NativeMemoryOwner, nameof(_previousPositions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_previousPositionValid, NativeMemoryOwner, nameof(_previousPositionValid), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_velocities, NativeMemoryOwner, nameof(_velocities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_angularVelocities, NativeMemoryOwner, nameof(_angularVelocities), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_upVectors, NativeMemoryOwner, nameof(_upVectors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_surfaceUpVectors, NativeMemoryOwner, nameof(_surfaceUpVectors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_params, NativeMemoryOwner, nameof(_params), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_waveOffsets, NativeMemoryOwner, nameof(_waveOffsets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_sleepMask, NativeMemoryOwner, nameof(_sleepMask), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gerstnerWaves, NativeMemoryOwner, nameof(_gerstnerWaves), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuBuoyancyForcesY, NativeMemoryOwner, nameof(_gpuBuoyancyForcesY), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_resultForces, NativeMemoryOwner, nameof(_resultForces), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_resultTorques, NativeMemoryOwner, nameof(_resultTorques), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_oceanSurfaceTelemetry, NativeMemoryOwner, nameof(_oceanSurfaceTelemetry), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_impactEventScratch, NativeMemoryOwner, nameof(_impactEventScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_impactEventFlags, NativeMemoryOwner, nameof(_impactEventFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuBuoyancyObjectDataUpload, NativeMemoryOwner, nameof(_gpuBuoyancyObjectDataUpload), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuBuoyancyReadback, NativeMemoryOwner, nameof(_gpuBuoyancyReadback), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_brineHeights, NativeMemoryOwner, nameof(_brineHeights), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_brineDensityMultipliers, NativeMemoryOwner, nameof(_brineDensityMultipliers), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_brineCartographySectors, NativeMemoryOwner, nameof(_brineCartographySectors), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_brineFlags, NativeMemoryOwner, nameof(_brineFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_gpuAbyssalHeatSourceUpload, NativeMemoryOwner, nameof(_gpuAbyssalHeatSourceUpload), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_abyssalFlowTelemetry, NativeMemoryOwner, nameof(_abyssalFlowTelemetry), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeThrusterFlows, NativeMemoryOwner, nameof(_activeThrusterFlows), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeWhirlpools, NativeMemoryOwner, nameof(_activeWhirlpools), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeMaelstroms, NativeMemoryOwner, nameof(_activeMaelstroms), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_maelstromTelemetry, NativeMemoryOwner, nameof(_maelstromTelemetry), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_activeViscosityRegions, NativeMemoryOwner, nameof(_activeViscosityRegions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_viscosityGradientLut, NativeMemoryOwner, nameof(_viscosityGradientLut), NativeMemoryLifetime);
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

            if (vault == null || vault.IsAllocationLocked || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            handle = vault.GetGenerationHandle<T>(
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
            _sharedGerstnerWavesHandle = default;
            _sharedGerstnerMetaHandle = default;
            _dynamicWakeBufferHandle = default;
            _dynamicWakeVectorBufferHandle = default;
            _fluidImpactEventRingHandle = default;
            _fluidImpactEventRing = default;
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
        }

        private bool EnsureFluidImpactEventRing(bool allowAllocate)
        {
            if (_fluidImpactEventRing.IsCreated &&
                _fluidImpactEventRing.Length >= FluidImpactEventQueueCapacity)
            {
                return true;
            }

            IDataVault vault = ResolveFluidDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!IsVaultGenerationHandleCreated(in _fluidImpactEventRingHandle))
            {
                if (!allowAllocate || vault.IsAllocationLocked)
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
                    _fluidImpactEventRingHandle = vault.GetGenerationHandle<FluidImpactEvent>(
                        FluidImpactEventRingBufferId,
                        FluidImpactEventQueueCapacity,
                        SystemID.Fluid,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!vault.TryResolveHandle(in _fluidImpactEventRingHandle, out _fluidImpactEventRing) ||
                !_fluidImpactEventRing.IsCreated ||
                _fluidImpactEventRing.Length < FluidImpactEventQueueCapacity)
            {
                _fluidImpactEventRing = default;
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

            _fluidImpactEventRing = default;
            _fluidImpactEventRingHandle = default;
            _fluidImpactEventReadIndex = 0;
            _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount = 0;
        }

        private bool TryEnqueueFluidImpactEvent(in FluidImpactEvent impactEvent)
        {
            if (!EnsureFluidImpactEventRing(allowAllocate: false) ||
                _fluidImpactQueuedCount >= FluidImpactEventQueueCapacity)
            {
                return false;
            }

            int index = _fluidImpactEventWriteIndex;
            if ((uint)index >= (uint)FluidImpactEventQueueCapacity)
                index = 0;

            _fluidImpactEventRing[index] = impactEvent;
            _fluidImpactEventWriteIndex = index + 1;
            if (_fluidImpactEventWriteIndex >= FluidImpactEventQueueCapacity)
                _fluidImpactEventWriteIndex = 0;
            _fluidImpactQueuedCount++;
            return true;
        }

        private bool TryDequeueFluidImpactEvent(out FluidImpactEvent impactEvent)
        {
            impactEvent = default;
            if (_fluidImpactQueuedCount <= 0 || !EnsureFluidImpactEventRing(allowAllocate: false))
                return false;

            int index = _fluidImpactEventReadIndex;
            if ((uint)index >= (uint)FluidImpactEventQueueCapacity)
                index = 0;

            impactEvent = _fluidImpactEventRing[index];
            _fluidImpactEventRing[index] = default;
            _fluidImpactEventReadIndex = index + 1;
            if (_fluidImpactEventReadIndex >= FluidImpactEventQueueCapacity)
                _fluidImpactEventReadIndex = 0;
            _fluidImpactQueuedCount--;
            return true;
        }

        private void EnsureAbyssalFlowNativeState()
        {
            if (!_gpuAbyssalHeatSourceUpload.IsCreated)
            {
                _gpuAbyssalHeatSourceUpload = new NativeArray<GpuHeatSourceData>(
                    MaxAbyssalHeatSourceCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<GpuHeatSourceData>[8] - bounded abyssal thermal geyser upload staging - owner: HectonFluidEngine
            }

            if (!_abyssalFlowTelemetry.IsCreated)
            {
                _abyssalFlowTelemetry = new NativeArray<AbyssalFlowTelemetryEntry>(
                    AbyssalFlowTelemetryCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AbyssalFlowTelemetryEntry>[300] - abyssal flow black-box telemetry ring - owner: HectonFluidEngine
                _abyssalFlowTelemetryCursor = 0;
                _abyssalFlowTelemetryDumped = false;
            }

            if (!_activeWhirlpools.IsCreated)
            {
                _activeWhirlpools = new NativeArray<WhirlpoolFlow>(
                    MaxAnalyticalWhirlpoolCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WhirlpoolFlow>[2] - active maelstrom physics inputs - owner: HectonFluidEngine
            }

            if (!_activeMaelstroms.IsCreated)
            {
                _activeMaelstroms = new NativeArray<float4>(
                    MaxActiveMaelstromCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[2] - compact maelstrom GPU/AI SOA - owner: HectonFluidEngine
            }

            if (!_maelstromTelemetry.IsCreated)
            {
                _maelstromTelemetry = new NativeArray<MaelstromTelemetryEntry>(
                    MaelstromTelemetryCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<MaelstromTelemetryEntry>[300] - maelstrom black-box telemetry ring - owner: HectonFluidEngine
                _maelstromTelemetryCursor = 0;
                _maelstromTelemetryDumped = false;
            }
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (dependency.IsCompleted)
                array.Dispose();
            else
                array.Dispose(dependency);

            array = default;
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

            _prebakedVectorNoiseField = new NativeArray<float3>(
                HectonAnalyticalFlowField.VectorNoiseVoxelCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float3>[32768] - prebaked 3D vector-noise flow atlas - owner: HectonFluidEngine
            NativeMemorySentinel.RegisterNativeArray(
                _prebakedVectorNoiseField,
                NativeMemoryOwner,
                nameof(_prebakedVectorNoiseField),
                NativeMemoryLifetime);

            Color[] pixels = new Color[HectonAnalyticalFlowField.VectorNoiseVoxelCount]; // COLD ALLOC: Color[32768] - one-shot Texture3D upload staging - owner: HectonFluidEngine
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
                        pixels[index] = new Color(
                            sample.x * 0.5f + 0.5f,
                            sample.y * 0.5f + 0.5f,
                            sample.z * 0.5f + 0.5f,
                            1f);
                        index++;
                    }
                }
            }

            _prebakedVectorNoiseTexture = new Texture3D(
                HectonAnalyticalFlowField.VectorNoiseResolution,
                HectonAnalyticalFlowField.VectorNoiseResolution,
                HectonAnalyticalFlowField.VectorNoiseResolution,
                TextureFormat.RGBAHalf,
                false); // COLD ALLOC: Texture3D[32^3 RGBAHalf] - shared organic current atlas - owner: HectonFluidEngine
            _prebakedVectorNoiseTexture.wrapMode = TextureWrapMode.Repeat;
            _prebakedVectorNoiseTexture.filterMode = FilterMode.Trilinear;
            _prebakedVectorNoiseTexture.SetPixels(pixels);
            _prebakedVectorNoiseTexture.Apply(false, true);
            Shader.SetGlobalTexture(_PrebakedVectorNoise3DId, _prebakedVectorNoiseTexture);
            _prebakedVectorNoiseRuntimeSeed = prebakedVectorNoiseSeed;
        }

        private void DisposePrebakedVectorNoiseField()
        {
            JobHandle dependency = _scheduledBuoyancyJobActive ? _scheduledBuoyancyHandle : default;
            DisposeNativeArray(ref _prebakedVectorNoiseField, dependency);
            _prebakedVectorNoiseRuntimeSeed = int.MinValue;

            if (_prebakedVectorNoiseTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_prebakedVectorNoiseTexture);
#if UNITY_EDITOR
            else
                DestroyImmediate(_prebakedVectorNoiseTexture);
#else
            else
                Destroy(_prebakedVectorNoiseTexture);
#endif
            _prebakedVectorNoiseTexture = null;
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
                    GlobalSignals.Publish(in acoustic);
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

            RefreshRuntimeActorContextsIfMissing();
            IPlayerRuntimeContext player = _playerRuntime;
            Rigidbody playerBody = player != null ? player.PlayerRigidbody : null;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            Vector3 playerPosition = playerBody != null
                ? playerBody.worldCenterOfMass
                : playerTransform != null ? playerTransform.position : Vector3.positiveInfinity;
            if (IsFiniteVector(playerPosition) && (playerPosition - center).sqrMagnitude <= eventHorizonRadiusSq)
                published |= PublishMaelstromDamageSignal(center, playerPosition, playerBody != null ? unchecked((uint)EntityId.ToULong(playerBody.GetEntityId())) : 0u, intensity01);

            ISubmarineRuntimeContext submarine = _submarineRuntime;
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
            damage.Frame = unchecked((uint)Time.frameCount);
            damage.SourceId = (ushort)(MaelstromSourceHash & 0xffffu);
            damage.TargetId = targetHash != 0u ? (ushort)math.min(targetHash, (uint)ushort.MaxValue) : (ushort)0;
            damage.Channel = MaelstromAcousticChannel;
            damage.Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag;
            damage.IntegrityDelta = 1;
            GlobalSignals.Publish(in damage);
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
                Frame = Time.frameCount,
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
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string dumpPath = Path.Combine(projectRoot, MaelstromDumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x4D41454Cu);
                    writer.Write(MaelstromTelemetryCapacity);
                    writer.Write(_maelstromTelemetryCursor);
                    writer.Write(reasonFlags);
                    for (int i = 0; i < _maelstromTelemetry.Length; i++)
                    {
                        int index = (_maelstromTelemetryCursor + i) % _maelstromTelemetry.Length;
                        MaelstromTelemetryEntry entry = _maelstromTelemetry[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.FixedTime);
                        writer.Write(entry.PrimaryCenterWS.x);
                        writer.Write(entry.PrimaryCenterWS.y);
                        writer.Write(entry.PrimaryCenterWS.z);
                        writer.Write(entry.PrimaryRadius);
                        writer.Write(entry.PrimaryCompact.x);
                        writer.Write(entry.PrimaryCompact.y);
                        writer.Write(entry.PrimaryCompact.z);
                        writer.Write(entry.PrimaryCompact.w);
                        writer.Write(entry.Warp01);
                        writer.Write(entry.ActiveCount);
                        writer.Write(entry.Flags);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.EscapeVelocityClamp);
                        writer.Write(entry.EventHorizonRadius);
                    }
                }
            }
            catch (IOException)
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
            originAup = GlobalSignals.CurrentRuntimeOriginAup();
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

            DisposeNativeArrays(releaseAbyssalFlow: false);
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
            float cinematicWaterLevel = ResolveCinematicWaterLevelY();
            Shader.SetGlobalFloat(_CurrentWaterLevelId, cinematicWaterLevel);
            Shader.SetGlobalFloat(_CurrentWaterLevelYId, cinematicWaterLevel);
            if (UIStateStore.IsInitialized)
                UIStateStore.WriteValue(UIValueSlotId.WaterSurfaceY, cinematicWaterLevel, Time.unscaledTime);
            return cinematicWaterLevel;
        }

        private float ResolveCinematicWaterLevelY()
        {
            return GlobalPhysicsStateManager.UpdateFrameCachedCurrentWaterLevelY(
                waterLevel,
                enableCinematicTideShift,
                cinematicTideAmplitudeMeters,
                ResolveWaterLevelTimeSeconds());
        }

        private float ResolveWaterLevelTimeSeconds()
        {
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
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

            if (_gpuBuoyancyPositionBuffer == null || _gpuBuoyancyPositionBuffer.count != capacity)
            {
                ReleaseGpuBuoyancyBuffers();
                _gpuBuoyancyPositionBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float3>(capacity); // COLD ALLOC: GraphicsBuffer[capacity] — GPU buoyancy position upload buffer — owner: HectonFluidEngine
                _gpuBuoyancyParamBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuBuoyancyObjectData>(capacity); // COLD ALLOC: GraphicsBuffer[capacity] — GPU buoyancy object payload buffer — owner: HectonFluidEngine
                _gpuBuoyancyResultBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(capacity); // COLD ALLOC: GraphicsBuffer[capacity] — GPU buoyancy result buffer for async readback — owner: HectonFluidEngine
            }
        }

        private void ReleaseGpuBuoyancyBuffers()
        {
            if (_gpuBuoyancyPositionBuffer != null)
            {
                _gpuBuoyancyPositionBuffer.Release();
                _gpuBuoyancyPositionBuffer = null;
            }

            if (_gpuBuoyancyParamBuffer != null)
            {
                _gpuBuoyancyParamBuffer.Release();
                _gpuBuoyancyParamBuffer = null;
            }

            if (_gpuBuoyancyResultBuffer != null)
            {
                _gpuBuoyancyResultBuffer.Release();
                _gpuBuoyancyResultBuffer = null;
            }
        }

        private void EnsureGpuAbyssalFlowBuffers()
        {
            EnsureAbyssalFlowNativeState();

            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return;

            if (_gpuAbyssalFlowResultBuffer == null || _gpuAbyssalFlowResultBuffer.count != nodeCount)
            {
                ReleaseGpuAbyssalFlowBuffers();
                _gpuAbyssalFlowResultBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(nodeCount); // COLD ALLOC: GraphicsBuffer[nodeCount] — GPU abyssal flow-vector field storage — owner: HectonFluidEngine
                _gpuAbyssalHeatSourceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<GpuHeatSourceData>(MaxAbyssalHeatSourceCount); // COLD ALLOC: GraphicsBuffer[8] — inferred hydrothermal heat-source upload staging — owner: HectonFluidEngine
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

        private void EnsureSplashdownImpulseState()
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return;

            if (_splashdownImpulseUpload.IsCreated && _splashdownImpulseUpload.Length != nodeCount)
                DisposeSplashdownImpulseState();

            if (!_splashdownImpulseUpload.IsCreated)
            {
                _splashdownImpulseUpload = new NativeArray<float4>(
                    nodeCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[32768] - splashdown impulse vector-field staging - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_splashdownImpulseUpload, NativeMemoryOwner, nameof(_splashdownImpulseUpload), NativeMemoryLifetime);
            }

            if (!_splashdownImpulseStats.IsCreated)
            {
                _splashdownImpulseStats = new NativeArray<int>(
                    2,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[2] - splashdown impulse affected-count and guard flags - owner: HectonFluidEngine
                NativeMemorySentinel.RegisterNativeArray(_splashdownImpulseStats, NativeMemoryOwner, nameof(_splashdownImpulseStats), NativeMemoryLifetime);
            }
        }

        private void EnsureAbyssalFlowTextureHandles()
        {
            if (_gpuAbyssalFlowTextureA != null && _gpuAbyssalFlowTextureAHandle == null)
                _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA);

            if (_gpuAbyssalFlowTextureB != null && _gpuAbyssalFlowTextureBHandle == null)
                _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB);
        }

        private void EnsureSplashdownImpulseGpuBuffer()
        {
            int nodeCount = GetAbyssalFlowNodeCount();
            if (nodeCount <= 0)
                return;

            if (_gpuSplashdownImpulseBuffer != null &&
                _gpuSplashdownImpulseBuffer.IsValid() &&
                _gpuSplashdownImpulseBuffer.count == nodeCount)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBuffer);
            _gpuSplashdownImpulseBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(nodeCount); // COLD ALLOC: GraphicsBuffer[nodeCount] - lazy splashdown vector-field override, allocated only for non-low impact - owner: HectonFluidEngine
            _splashdownImpulseUploaded = false;
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

            if (_gpuAbyssalHeatSourceBuffer != null)
            {
                _gpuAbyssalHeatSourceBuffer.Release();
                _gpuAbyssalHeatSourceBuffer = null;
            }

            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBuffer);
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
            ReleaseGraphicsBuffer(ref _dynamicWakeBuffer);
            ReleaseGraphicsBuffer(ref _dynamicWakeVectorBuffer);
            _dynamicWakeBufferHandle = default;
            _dynamicWakeVectorBufferHandle = default;

            JobHandle dependency = _scheduledBuoyancyJobActive ? _scheduledBuoyancyHandle : default;
            DisposeNativeArray(ref _advectedSiltUpload, dependency);
            DisposeNativeArray(ref _advectedBubbleUpload, dependency);
            DisposeNativeArray(ref _advectedDebrisUpload, dependency);
            DisposeNativeArray(ref _emptyAbyssalFlowUpload, dependency);
            DisposeNativeArray(ref _fluidAdvectionTelemetry, dependency);

            ReleaseRTHandle(ref _cachedFluidAdvectionFlowHandle);
            ReleaseRTHandle(ref _cachedFluidAdvectionSdfHandle);
            _cachedFluidAdvectionFlowHandleSource = null;
            _cachedFluidAdvectionSdfHandleSource = null;
            ReleaseRTHandle(ref _emptyFluidAdvectionTextureHandle);

            if (_emptyFluidAdvectionTexture != null)
            {
                UnityEngine.Object.Destroy(_emptyFluidAdvectionTexture);
                _emptyFluidAdvectionTexture = null;
            }

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
            _fluidAdvectionTelemetryDumped = false;
        }

        private void DisposeSplashdownImpulseState()
        {
            JobHandle dependency = _splashdownImpulseJobActive ? _splashdownImpulseJobHandle : default;
            DisposeNativeArray(ref _splashdownImpulseUpload, dependency);
            DisposeNativeArray(ref _splashdownImpulseStats, dependency);
            ReleaseGraphicsBuffer(ref _gpuSplashdownImpulseBuffer);
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
            Shader.SetGlobalFloat(_AbyssalFlowTextureActiveId, 0f);
            Shader.SetGlobalTexture(_AbyssalFlowFieldTextureId, null);
            Shader.SetGlobalVector(_AbyssalGridResolutionId, Vector4.zero);
            Shader.SetGlobalVector(_AbyssalFlowCenterId, Vector4.zero);
            Shader.SetGlobalVector(_AbyssalFlowSpacingId, Vector4.zero);
            Shader.SetGlobalVector(_AbyssalFlowTextureParamsId, Vector4.zero);
            _abyssalFlowPublicationClearIssued = true;
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

            int frameCount = Time.frameCount;
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
            int frame = Time.frameCount;
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

            EnsureGpuAbyssalFlowBuffers();
            if (_gpuAbyssalFlowResultBuffer == null ||
                _gpuAbyssalHeatSourceBuffer == null ||
                _gpuAbyssalFlowReadTexture == null ||
                _gpuAbyssalFlowWriteTexture == null)
            {
                AgeAbyssalVortexImpulsesOnce(fixedDeltaTime);
                DeactivateAbyssalFlowPublication();
                return;
            }

            _lastAbyssalFlowDispatchFixedTime = currentFixedTime;
            long watchdogStart = System.Diagnostics.Stopwatch.GetTimestamp();

            float3 flowCenter = ResolveAbyssalFlowCenter(resolvedWaterLevel);
            bool highTier = ResolveCachedHighScalabilityTier();
            int heatSourceCount = highTier ? CaptureAbyssalHeatSources(flowCenter) : 0;
            _debugAbyssalHeatSourceCount = heatSourceCount;

            if (heatSourceCount > 0)
                GraphicsBufferUploadUtility.UploadNativeArray(_gpuAbyssalHeatSourceBuffer, _gpuAbyssalHeatSourceUpload, heatSourceCount);

            int nodeCount = GetAbyssalFlowNodeCount();
            int groupCount = math.max(1, (nodeCount + GpuThreadGroupSize - 1) >> GpuThreadGroupShift);
            int textureGroupCount = math.max(
                1,
                (AbyssalFlowTextureResolution + AbyssalFlowTextureThreadGroupSize - 1) / AbyssalFlowTextureThreadGroupSize);
            ResolveAbyssalFlowBucketUniforms(out int updateBucket, out int updateBucketMask);
            GraphicsBuffer splashdownImpulseBuffer = ResolveSplashdownImpulseBuffer();
            Vector4 splashdownParams = ResolveSplashdownImpulseParams();

            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalFlowFieldResultId, _gpuAbyssalFlowResultBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalUpdateKernel, _AbyssalHeatSourcesId, _gpuAbyssalHeatSourceBuffer);
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
                highTier && flowTextureInitialized ? math.max(0f, fixedDeltaTime) : 1f,
                highTier ? 1f : 0f);
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
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalTextureUpdateKernel, _AbyssalHeatSourcesId, _gpuAbyssalHeatSourceBuffer);
            abyssalFlowFieldCompute.SetBuffer(_gpuAbyssalTextureUpdateKernel, _AbyssalSplashdownImpulseBufferId, splashdownImpulseBuffer);
            abyssalFlowFieldCompute.SetVector(_AbyssalSplashdownParamsId, splashdownParams);
            abyssalFlowFieldCompute.Dispatch(_gpuAbyssalTextureUpdateKernel, textureGroupCount, textureGroupCount, textureGroupCount);
            SwapAbyssalFlowTextures();

            Vector4 wakeSphere = Vector4.zero;
            Vector4 wakeVelocity = Vector4.zero;
            if (highTier && TryResolveSubmarineWakePayload(out wakeSphere, out wakeVelocity))
            {
                abyssalFlowFieldCompute.SetTexture(_gpuAbyssalWakeKernel, _AbyssalFlowTextureRWId, _gpuAbyssalFlowReadTexture);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowWakeSphereId, wakeSphere);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowWakeVelocityId, wakeVelocity);
                abyssalFlowFieldCompute.Dispatch(_gpuAbyssalWakeKernel, textureGroupCount, textureGroupCount, textureGroupCount);
            }

            int vortexDispatchCount = DispatchAbyssalVortexImpulses(textureGroupCount, fixedDeltaTime, highTier);

            Shader.SetGlobalBuffer(_AbyssalFlowFieldResultId, _gpuAbyssalFlowResultBuffer);
            Shader.SetGlobalTexture(_AbyssalFlowFieldTextureId, _gpuAbyssalFlowReadTexture);
            Shader.SetGlobalVector(_AbyssalGridResolutionId, gridResolution);
            Shader.SetGlobalVector(_AbyssalFlowCenterId, flowCenterVector);
            Shader.SetGlobalVector(_AbyssalFlowSpacingId, flowSpacingVector);
            Shader.SetGlobalVector(_AbyssalFlowTextureParamsId, textureParams);
            Shader.SetGlobalFloat(_AbyssalFlowTextureActiveId, 1f);
            _lastAbyssalGridResolution = gridResolution;
            _lastAbyssalFlowCenter = flowCenterVector;
            _lastAbyssalFlowSpacing = flowSpacingVector;
            _lastAbyssalFlowTextureSpacing = textureSpacingVector;
            _hasAbyssalFlowTexture = true;
            _abyssalFlowPublicationClearIssued = false;
            uint telemetryFlags = highTier ? 1u : 0u;
            if (vortexDispatchCount > 0)
                telemetryFlags |= 2u;
            if (splashdownParams.x > 0.5f)
                telemetryFlags |= 4u;
            WriteAbyssalFlowTelemetry(flowCenter, wakeSphere, wakeVelocity, heatSourceCount, _lastSplashdownFluidImpulseCount, telemetryFlags);
            ReportWatchdogCost(AbyssalFlowBucketedCostHash, watchdogStart);
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

        private int DispatchAbyssalVortexImpulses(int textureGroupCount, float fixedDeltaTime, bool highTier)
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

                if (!highTier)
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
                    impulse.StrengthMetersPerSecond * strengthScale);
                abyssalFlowFieldCompute.SetTexture(_gpuAbyssalVortexKernel, _AbyssalFlowTextureRWId, _gpuAbyssalFlowReadTexture);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexSphereId, sphere);
                abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexAxisStrengthId, axisStrength);
                abyssalFlowFieldCompute.Dispatch(_gpuAbyssalVortexKernel, textureGroupCount, textureGroupCount, textureGroupCount);
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

            RefreshRuntimeActorContextsIfMissing();
            ISubmarineRuntimeContext submarine = _submarineRuntime;
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
                Frame = Time.frameCount,
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
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string dumpPath = Path.Combine(projectRoot, AbyssalFlowDumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x41424646u);
                    writer.Write(AbyssalFlowTelemetryCapacity);
                    writer.Write(_abyssalFlowTelemetryCursor);
                    writer.Write(reasonFlags);
                    for (int i = 0; i < _abyssalFlowTelemetry.Length; i++)
                    {
                        int index = (_abyssalFlowTelemetryCursor + i) % _abyssalFlowTelemetry.Length;
                        AbyssalFlowTelemetryEntry entry = _abyssalFlowTelemetry[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.FixedTime);
                        writer.Write(entry.CenterWS.x);
                        writer.Write(entry.CenterWS.y);
                        writer.Write(entry.CenterWS.z);
                        writer.Write(entry.WakePositionWS.x);
                        writer.Write(entry.WakePositionWS.y);
                        writer.Write(entry.WakePositionWS.z);
                        writer.Write(entry.WakeVelocityWS.x);
                        writer.Write(entry.WakeVelocityWS.y);
                        writer.Write(entry.WakeVelocityWS.z);
                        writer.Write(entry.WakeRadius);
                        writer.Write(entry.HeatSourceCount);
                        writer.Write(entry.FluidImpulseCount);
                        writer.Write(entry.Flags);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (System.Exception)
            {
            }
        }

        private int CaptureAbyssalHeatSources(float3 flowCenter)
        {
            if (!_gpuAbyssalHeatSourceUpload.IsCreated)
                return 0;

            for (int i = 0; i < MaxAbyssalHeatSourceCount; i++)
                _gpuAbyssalHeatSourceUpload[i] = default;

            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            if (thermalManager == null)
                return 0;

            float horizontalProbeOffset = math.max(abyssalHeatProbeRadius, abyssalFlowHorizontalCellSize * 1.5f);
            float verticalProbeOffset = math.max(abyssalHeatProbeRadius * 0.5f, abyssalFlowVerticalCellSize);
            float sampleRadius = math.max(1f, abyssalFlowHorizontalCellSize * 0.5f);
            int sourceCount = 0;

            for (int probeIndex = 0; probeIndex < MaxAbyssalHeatSourceCount; probeIndex++)
            {
                float3 sampleOffset = ResolveHeatProbeOffset(probeIndex, horizontalProbeOffset, verticalProbeOffset);
                Vector3 samplePosition = new Vector3(
                    flowCenter.x + sampleOffset.x,
                    flowCenter.y + sampleOffset.y,
                    flowCenter.z + sampleOffset.z);

                if (!thermalManager.SampleThermalFlow(samplePosition, sampleRadius, out AbyssalThermalManager.ThermalFlowSample sample) ||
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

            HectonCelestialEngine celestialEngine = _celestialEngine;
            if (celestialEngine == null || !celestialEngine.TryGetAegirSkyDirection(out Vector3 directionManaged))
                return float3.zero;

            float3 skyDirection = new float3(directionManaged.x, directionManaged.y, directionManaged.z);
            float3 horizontalDirection = new float3(skyDirection.x, 0f, skyDirection.z);
            float horizontalLengthSq = math.lengthsq(horizontalDirection);
            if (horizontalLengthSq <= GiantWakeDirectionEpsilonSq)
                return float3.zero;

            float3 wakeDirection = DominantAxisOrDefault(horizontalDirection, new float3(1f, 0f, 0f));
            wakeDirection.y = giantWakeVerticalBias;
            wakeDirection = DominantAxisOrDefault(wakeDirection, new float3(1f, 0f, 0f));
            return wakeDirection * math.max(0f, giantWakeCurrentStrength);
        }

        private float3 ResolveGiantWakeCurrentForDepth(float sampleY)
        {
            float3 wakeCurrent = _resolvedGiantWakeCurrent;
            if (math.lengthsq(wakeCurrent) <= GiantWakeDirectionEpsilonSq)
                wakeCurrent = ResolveGiantWakeCurrentBase();

            float depthBelowSurface = math.max(0f, waterLevel - sampleY);
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
            if (_gpuReadbackRequests == null || _gpuReadbackActive == null || !_gpuBuoyancyReadback.IsCreated)
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

                int readCount = math.min(_gpuReadbackCounts[requestIndex], _gpuBuoyancyReadback.Length);
                var readbackData = request.GetData<float4>();
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

            EnsureGpuBuoyancyBuffers(count);
            if (_gpuBuoyancyPositionBuffer == null || _gpuBuoyancyParamBuffer == null || _gpuBuoyancyResultBuffer == null)
                return;

            int slot = _gpuReadbackWriteIndex;
            if (_gpuReadbackActive != null && _gpuReadbackActive[slot])
                return;

            UploadGpuBuoyancyObjectData(count);
            GraphicsBufferUploadUtility.UploadNativeArray(_gpuBuoyancyPositionBuffer, _positions, count);
            GraphicsBufferUploadUtility.UploadNativeArray(_gpuBuoyancyParamBuffer, _gpuBuoyancyObjectDataUpload, count);

            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyPositionsId, _gpuBuoyancyPositionBuffer);
            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyObjectDataId, _gpuBuoyancyParamBuffer);
            gpuBuoyancyCompute.SetBuffer(_gpuBuoyancyKernel, _GpuBuoyancyResultsId, _gpuBuoyancyResultBuffer);
            gpuBuoyancyCompute.SetInt(_GpuBuoyancyObjectCountId, count);
            gpuBuoyancyCompute.SetVector(_GpuBuoyancyWaterParamsId, new Vector4(resolvedWaterLevel, waterDensity, math.abs(UnityEngine.Physics.gravity.y), weatherSnapshot.CurrentMeta.TimeAccumulator));
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave0AId, _GpuBuoyancyWave0BId, weatherSnapshot.Wave0);
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave1AId, _GpuBuoyancyWave1BId, weatherSnapshot.Wave1);
            SetGpuWave(gpuBuoyancyCompute, _GpuBuoyancyWave2AId, _GpuBuoyancyWave2BId, weatherSnapshot.Wave2);

            int groupCount = math.max(1, (count + GpuThreadGroupSize - 1) >> GpuThreadGroupShift);
            gpuBuoyancyCompute.Dispatch(_gpuBuoyancyKernel, groupCount, 1, 1);
            _gpuReadbackRequests[slot] = AsyncGPUReadback.Request(_gpuBuoyancyResultBuffer);
            _gpuReadbackCounts[slot] = count;
            _gpuReadbackActive[slot] = true;
            _gpuReadbackWriteIndex = (_gpuReadbackWriteIndex + 1) % GpuReadbackRingSize;
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

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                lodObserver = playerTransform;
        }

        private HectonQualityTier ResolveCachedScalabilityTier()
        {
            int frame = Time.frameCount;
            if (_cachedScalabilityTierFrame == frame)
                return _cachedScalabilityTier;

            HectonQualityTier tier = _cachedScalabilityTier == HectonQualityTier.Unknown
                ? HectonQualityTier.Low
                : _cachedScalabilityTier;
            _cachedScalabilityTier = tier;
            _cachedScalabilityTierFrame = frame;
            _cachedHighScalabilityTier = DistanceMath.IsHighQualityTier(tier) ? (byte)1 : (byte)0;
            return tier;
        }

        private bool ResolveCachedHighScalabilityTier()
        {
            ResolveCachedScalabilityTier();
            return _cachedHighScalabilityTier != 0;
        }

        private void RefreshRuntimeActorContextsIfMissing()
        {
            if (_playerRuntime == null || IsUnityObjectInvalid(_playerRuntime))
                _playerRuntime = GlobalRegistry.Player;

            if (_submarineRuntime == null || IsUnityObjectInvalid(_submarineRuntime))
                _submarineRuntime = GlobalRegistry.Submarine;
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
            Vector3 center = new Vector3(0f, waterLevel, 0f);
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
                origin.y = waterLevel;
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
    }

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
        public byte   highScalabilityTier;
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

            if (p.simulationMode == 1)
            {
                resultForces[i] = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            if (p.simulationMode == 2)
            {
                resultForces[i] = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            // ══════════════════════════════════════════════
            //  DRY ZONE CHECK — obekt vnutri nezatoplennogo modulya
            // ══════════════════════════════════════════════
            // Mgnovennoe otklyuchenie vsey vodnoy fiziki.
            // Obekt podchinyaetsya tolko Unity gravity.
            if (p.isInAir != 0)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

            float3 pos = positions[i];
            float3 vel = velocities[i];
            float3 angularVel = angularVelocities[i];
            float3 up = ResolveSurfaceNormalLod(upVectors[i], p.alignmentPadding, highScalabilityTier);
            float3 targetUp = ResolveSurfaceNormalLod(
                surfaceUpVectors.IsCreated && i < surfaceUpVectors.Length ? surfaceUpVectors[i] : new float3(0f, 1f, 0f),
                BuoyancyParams.ExactSurfaceNormalFlag,
                highScalabilityTier);

            // ── Glubina pogruzheniya tsentra mass ──
            float waveOffset = waveOffsets[i];
            float surfaceY = waterLevel + waveOffset;
            float depthBelowSurface = surfaceY - pos.y;

            // ── Obekt nad vodoy → nulevye sily ──
            if (depthBelowSurface <= 0f)
            {
                resultForces[i]  = float3.zero;
                resultTorques[i] = float3.zero;
                return;
            }

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

            // ── Koeffitsient pogruzheniya (0..1) ──
            float subRatio = p.simplifiedSubmersion != 0
                ? (depthBelowSurface > 0f ? 1f : 0f)
                : math.saturate(depthBelowSurface * math.rcp(math.max(p.height, 0.0001f)));
            float resolvedWaterDensity = p.useLocalFluidDensityOverride != 0
                ? math.max(0.01f, p.localFluidDensity)
                : waterDensity;
            byte brineSubmerged = 0;
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
            float denseLayer01 = 0f;
            if (enableAnalyticalFlowField != 0)
            {
                float safeHaloclineDepth = math.max(0.01f, haloclineBoundaryDepthMeters);
                denseLayer01 = depthBelowSurface >= safeHaloclineDepth ? 1f : 0f;
                resolvedWaterDensity *= 1f + (math.max(1f, deepLayerDensityMultiplier) - 1f) * denseLayer01;
            }


            // ══════════════════════════════════════════════
            //  1. SILA ARHIMEDA (Buoyancy)
            // ══════════════════════════════════════════════
            float displacedVolume = p.volume * subRatio;
            float buoyancyMagnitude = resolvedWaterDensity * displacedVolume * gravity;
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

            float3 buoyancyForce = new float3(0f, buoyancyMagnitude, 0f);

            // ══════════════════════════════════════════════
            //  2. VYaZKOE SOPROTIVLENIE (Drag)
            // ══════════════════════════════════════════════
            float3 dragForce = float3.zero;

            // ══════════════════════════════════════════════
            //  3. PODVODNOE TEChENIE (Current)
            // ══════════════════════════════════════════════
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
                    highScalabilityTier);
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

                if (highScalabilityTier != 0 && surfaceLayer01 > 0.0001f && p.currentResponse > 0.0001f)
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
                        highScalabilityTier);
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
                        highScalabilityTier == 0 ? (byte)1 : (byte)0);
            }

            float3 analyticalShearForce = float3.zero;
            if (enableAnalyticalFlowField != 0 && denseLayer01 > 0f && haloclineShearForcePerKg != 0f && p.currentResponse > 0.0001f)
            {
                analyticalShearForce = new float3(
                    0f,
                    0f,
                    haloclineShearForcePerKg * p.mass * subRatio * math.max(0f, p.currentResponse));
            }

            float3 currentF = sampledCurrent * (subRatio * p.mass * p.currentResponse);
            float surfaceAdvection01 = 1f - math.saturate(depthBelowSurface * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
            float3 windAdvectionForce = windAdvectionVector *
                                        (math.max(0f, windAdvectionForcePerKg) *
                                         p.mass *
                                         subRatio *
                                         p.currentResponse *
                                         surfaceAdvection01);
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
                dragForce = -relativeVelocity * (math.max(1f, relativeSpeed) * dragScalar);
                dragForce = ClampVectorMagnitude(
                    dragForce,
                    math.max(0f, maxQuadraticDragForcePerKg) * math.max(0.01f, p.mass));
            }

            // ══════════════════════════════════════════════
            //  4. DEMPFIROVANIE POKAChIVANIYa
            // ══════════════════════════════════════════════
            float dampingForce = 0f;
            if (subRatio < 1f)
            {
                dampingForce = -vel.y * resolvedWaterDensity * displacedVolume * 0.5f;
            }

            float3 dampingVec = new float3(0f, dampingForce, 0f);

            // ══════════════════════════════════════════════
            //  ITOG
            // ══════════════════════════════════════════════

            float surfaceBand = math.saturate(
                1f - math.abs(depthBelowSurface - p.height) *
                math.rcp(math.max(0.25f, p.height * 1.5f)));
            float3 tiltAxis = math.cross(up, targetUp);
            float3 stabilityTorque = tiltAxis * (p.surfaceStability * buoyancyMagnitude * surfaceBand * 0.12f);
            float3 angularDragTorque = -angularVel * (angularDragCoeff * math.max(0.1f, p.angularDragMultiplier) * subRatio * math.max(1f, p.mass * 0.35f));
            float3 flowAxis = DominantAxisOrDefault(sampledCurrent, new float3(1f, 0f, 0f));
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
                float standardSpeedSq = math.lengthsq(standardCurrent);
                float wakeSpeedSq = math.lengthsq(resolvedGiantWakeCurrent);
                if (standardSpeedSq > 0.0001f && wakeSpeedSq > 0.0001f)
                {
                    float3 standardAxis = DominantAxisOrDefault(standardCurrent, new float3(1f, 0f, 0f));
                    float3 wakeAxis = DominantAxisOrDefault(resolvedGiantWakeCurrent, new float3(1f, 0f, 0f));
                    float crossMagnitudeSq = math.lengthsq(math.cross(standardAxis, wakeAxis));
                    float opposition = math.saturate(-math.dot(standardAxis, wakeAxis));
                    float minCurrentSpeed = math.min(
                        FastMagnitudeApprox(standardCurrent),
                        FastMagnitudeApprox(resolvedGiantWakeCurrent));
                    float shear01 = math.saturate((crossMagnitudeSq + opposition) * minCurrentSpeed * 0.85f);
                    float phase = math.dot(pos, new float3(0.071f, 0.113f, 0.097f)) + time * math.max(0.01f, tidalShearFrequency);
                    float turbulence = FastTriangleSigned(phase) * FastTriangleSigned(phase * 1.731f + 2.17f);
                    float3 shearAxis = DominantAxisOrDefault(math.cross(standardAxis, wakeAxis), up);
                    shearTorque = shearAxis *
                                  (turbulence * shear01 * math.max(0f, tidalShearTorqueStrength) *
                                   volumeLever * subRatio * math.max(0f, p.currentResponse));
                    shearTorque = ClampVectorMagnitude(shearTorque, maxGyroscopicFlowTorque);
                }
            }

            resultForces[i] = MathGuard.SanitizeFiniteOrZero(
                buoyancyForce + dragForce + currentF + windAdvectionForce + dampingVec + analyticalShearForce,
                forceNanErrorCode,
                mathGuardWriter);
            resultTorques[i] = MathGuard.SanitizeFiniteOrZero(
                angularDragTorque + stabilityTorque + gyroscopicFlowTorque + shearTorque,
                torqueNanErrorCode,
                mathGuardWriter);
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

        private static float3 ResolveSurfaceNormalLod(float3 value, uint flags, byte highScalabilityTier)
        {
            if (highScalabilityTier != 0 && (flags & BuoyancyParams.ExactSurfaceNormalFlag) != 0u)
            {
                float lengthSq = math.lengthsq(value);
                float3 safeValue = math.select(new float3(0f, 1f, 0f), value, lengthSq > 0.000001f);
                return safeValue * math.rsqrt(math.max(math.lengthsq(safeValue), 0.000001f));
            }

            return DominantAxisOrDefault(value, new float3(0f, 1f, 0f));
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
            math.sincos(phase, out _, out float cosPhase);
            float height = wave.Amplitude * cosPhase;
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
            float height = (float)((double)wave.Amplitude * math.cos(phase));
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

    internal static class HectonAnalyticalFlowField
    {
        public const int VectorNoiseResolution = 32;
        public const int VectorNoiseVoxelCount = VectorNoiseResolution * VectorNoiseResolution * VectorNoiseResolution;
        private const int VectorNoiseMask = VectorNoiseResolution - 1;
        private const int VectorNoiseLowTierMask = VectorNoiseMask & ~1;
        private const int VectorNoiseSliceShift = 5;
        private const int VectorNoisePlaneShift = 10;
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
            byte highScalabilityTier)
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
                    highScalabilityTier);
            }

            bool stormActive = (weatherStateMask & (uint)Hecton8.Core.WeatherState.Storm) != 0u;
            if (stormActive)
            {
                float surfaceLayer01 = 1f - math.saturate(depthBelowSurface * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
                float stormBlend = math.max(0f, weatherBlend);
                float stormBiasScale = weatherCurrentScale * math.max(0.35f, stormBlend);
                flow.xz += weatherCurrentDirection.xz * stormBiasScale;

                if (highScalabilityTier != 0 && surfaceLayer01 > 0.0001f)
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
                        highScalabilityTier);
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
            byte highScalabilityTier)
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
            bool highTier = highScalabilityTier != 0;
            int cellMask = math.select(VectorNoiseLowTierMask, VectorNoiseMask, highTier);
            int x = (int)(FastFloorToLong(aupCell.x) & cellMask);
            int y = (int)(FastFloorToLong(aupCell.y) & cellMask);
            int z = (int)(FastFloorToLong(aupCell.z) & cellMask);
            int index = x | (y << VectorNoiseSliceShift) | (z << VectorNoisePlaneShift);
            float3 highSample = vectorNoiseField[index];
            float3 lowSample = DominantAxisOrDefault(highSample, new float3(1f, 0f, 0f));
            float3 vectorSample = math.select(lowSample, highSample, highTier);
            vectorSample.y = math.select(0f, vectorSample.y * math.saturate(verticalFactor), highTier);

            float modulationRange = math.select(math.min(0.2f, math.saturate(triangleModulation)), math.saturate(triangleModulation), highTier);
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

            float3 exhaustDirection = -DominantAxisOrDefault(thruster.DirectionWS, new float3(0f, 0f, 1f));
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

        public static void ApplyWhirlpoolFlow(ref float3 flow, float3 samplePosition, WhirlpoolFlow whirlpool, byte lowMathTier)
        {
            flow += SampleWhirlpoolVelocity(samplePosition, whirlpool, lowMathTier, HectonFluidEngine.MaelstromMaxVelocityMetersPerSecond);
        }

        public static float3 SampleWhirlpoolVelocity(
            float3 samplePosition,
            WhirlpoolFlow whirlpool,
            byte lowMathTier,
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
            float3 tangent = lowMathTier != 0
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
                lowMathTier != 0
                    ? math.min(maxVelocityMetersPerSecond, HectonFluidEngine.MaelstromLowTierMaxVelocityMetersPerSecond)
                    : maxVelocityMetersPerSecond);
        }

        public static float3 SampleWhirlpoolVelocity(
            float3 samplePosition,
            NativeArray<WhirlpoolFlow> whirlpools,
            int whirlpoolCount,
            byte lowMathTier,
            float maxVelocityMetersPerSecond)
        {
            if (!whirlpools.IsCreated || whirlpoolCount <= 0)
                return float3.zero;

            float3 velocity = float3.zero;
            int count = math.min(math.max(0, whirlpoolCount), whirlpools.Length);
            for (int i = 0; i < count; i++)
                velocity += SampleWhirlpoolVelocity(samplePosition, whirlpools[i], lowMathTier, maxVelocityMetersPerSecond);

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
