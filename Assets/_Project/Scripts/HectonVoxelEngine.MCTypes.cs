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

#region MC Types

public struct CubeDensities
{
    public float d0, d1, d2, d3, d4, d5, d6, d7;
    public CubeDensities(float d0, float d1, float d2, float d3, float d4, float d5, float d6, float d7)
    {
        this.d0 = d0; this.d1 = d1; this.d2 = d2; this.d3 = d3;
        this.d4 = d4; this.d5 = d5; this.d6 = d6; this.d7 = d7;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct MCRawVertex
{
    [FieldOffset(0)]
    public long edgeId;

    [FieldOffset(8)]
    public float3 localPosition;

    [FieldOffset(20)]
    private uint _pad0;
}

[StructLayout(LayoutKind.Explicit, Size = 80)]
public struct VoxelSurfaceVertex
{
    [FieldOffset(0)] public float3 Position;
    [FieldOffset(12)] public float3 Normal;
    [FieldOffset(24)] public Color32 Color;
    [FieldOffset(28)] public float4 BakedOcclusionUv1;
    [FieldOffset(44)] public float4 DirtyBlendUv2;
    [FieldOffset(60)] public float4 RuntimePositionWS;
    [FieldOffset(76)] private uint _pad0;
}

[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct VoxelModifiedCellEntry
{
    [FieldOffset(0)]
    public int3 AbsoluteCell;

    [FieldOffset(12)]
    public VoxelModifiedCell Cell;

    [FieldOffset(20)]
    private uint _pad0;
}

#endregion
