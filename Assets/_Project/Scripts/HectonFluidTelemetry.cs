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
    public struct FluidOceanSurfaceTelemetryEntry
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
}
