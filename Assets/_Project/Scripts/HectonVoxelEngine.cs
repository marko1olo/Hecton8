// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelEngine.cs — Project HECTON-8 Localized Voxel Volumes           ║
// ║  Unity 6 (URP) | Burst + Jobs | Marching Cubes | SDF + 3D Noise           ║
// ║  v3.2 — Awaitable API migration (Unity 6 native async)                    ║
// ║                                                                             ║
// ║  CHANGES v3.2:                                                              ║
// ║  ─────────────                                                              ║
// ║  1. Removed System.Threading.Tasks dependency                              ║
// ║  2. GenerateVolumeAsync returns Awaitable<GameObject> (not Task<GO>)        ║
// ║  3. All await Task.Yield() replaced with Awaitable.NextFrameAsync(ct)      ║
// ║  4. Zero GC from async: Awaitable is pooled by Unity, no heap alloc        ║
// ║                                                                             ║
// ║  CHANGES v3.1 (preserved):                                                 ║
// ║  ─────────────                                                              ║
// ║  1. Replaced HectonWorldGenerator → MapMagicBridge for terrain heights     ║
// ║  2. TryGetHeight with safe fallback (center.y - 10m)                       ║
// ║  3. maxDepth hardcoded to 5000f (Abyssal constant)                         ║
// ║  4. gridBiome filled with 0f stub (no MapMagic biome masks yet)            ║
// ║  5. All MapMagicBridge calls on main thread before Burst jobs              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Threading;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Hecton8.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: ENUMS & DATA STRUCTURES
// ════════════════════════════════════════════════════════════════════════════════
#region Enums & Data Structures

public enum VoxelPOIType
{
    Cave,
    DeepRift
}

[System.Serializable]
public class VoxelPOIDefinition
{
    public VoxelPOIType type = VoxelPOIType.Cave;

    [Tooltip("Display name for debugging.")]
    public string label = "Cave";

    [Header("SDF Proportions")]
    [Tooltip("Cave: sphere radius in X. Rift: half-extents (X=width, Y=depth, Z=length).")]
    public Vector3 sdfSize = new Vector3(12f, 12f, 12f);

    [Tooltip("Volume padding beyond SDF bounds (m).")]
    public float volumePadding = 4f;

    [Header("Wall Noise Override")]
    [Tooltip("If true, uses per-POI noise instead of global.")]
    public bool overrideNoise = false;

    public HectonNoiseLayer wallNoise = new HectonNoiseLayer
        { scale = 0.08f, octaves = 3, lacunarity = 2f, persistence = 0.5f, seed = 888 };

    [Range(0f, 8f)]
    public float wallNoiseAmplitude = 3f;
}

/// <summary>Blittable POI data for Burst jobs.</summary>
public struct VoxelPOIData
{
    public VoxelPOIType type;
    public float3 sdfSize;
    public float3 sdfCenter;
    public NoiseData wallNoise;
    public float wallNoiseAmplitude;
}

/// <summary>
/// Raw vertex output from MC job. Position + edge ID for welding.
/// Normals and colors are computed AFTER welding from the density field.
/// </summary>
public struct MCRawVertex
{
    public float3 position;
    public long edgeId;
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: MARCHING CUBES LOOKUP TABLES
// ════════════════════════════════════════════════════════════════════════════════
#region Marching Cubes Tables

public static class MCTables
{
    // ── Public access ────────────────────────────────────────────────────────
    public static NativeArray<int> EdgeTable => _edgeTable;
    public static NativeArray<int> TriTable  => _triTable;
    public static bool IsReady => Volatile.Read(ref _ready) == 1;

    // ── Private fields ───────────────────────────────────────────────────────
    static NativeArray<int> _edgeTable;
    static NativeArray<int> _triTable;
    static int _ready;
    static readonly object _initLock = new object();

    // ════════════════════════════════════════════════════════════════════════
    //  CLEANUP HOOKS — prevent Persistent NativeArray leaks
    // ════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void EditorHooks()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;

        EditorApplication.quitting -= Shutdown;
        EditorApplication.quitting += Shutdown;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReloadCleanup()
    {
        Shutdown();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Thread-safe initialization
    // ════════════════════════════════════════════════════════════════════════

    public static void Initialize()
    {
        if (Volatile.Read(ref _ready) == 1) return;

        lock (_initLock)
        {
            if (Volatile.Read(ref _ready) == 1) return;

            var et = new int[256]
            {
                0x000, 0x109, 0x203, 0x30A, 0x406, 0x50F, 0x605, 0x70C,
                0x80C, 0x905, 0xA0F, 0xB06, 0xC0A, 0xD03, 0xE09, 0xF00,
                0x190, 0x099, 0x393, 0x29A, 0x596, 0x49F, 0x795, 0x69C,
                0x99C, 0x895, 0xB9F, 0xA96, 0xD9A, 0xC93, 0xF99, 0xE90,
                0x230, 0x339, 0x033, 0x13A, 0x636, 0x73F, 0x435, 0x53C,
                0xA3C, 0xB35, 0x83F, 0x936, 0xE3A, 0xF33, 0xC39, 0xD30,
                0x3A0, 0x2A9, 0x1A3, 0x0AA, 0x7A6, 0x6AF, 0x5A5, 0x4AC,
                0xBAC, 0xAA5, 0x9AF, 0x8A6, 0xFAA, 0xEA3, 0xDA9, 0xCA0,
                0x460, 0x569, 0x663, 0x76A, 0x066, 0x16F, 0x265, 0x36C,
                0xC6C, 0xD65, 0xE6F, 0xF66, 0x86A, 0x963, 0xA69, 0xB60,
                0x5F0, 0x4F9, 0x7F3, 0x6FA, 0x1F6, 0x0FF, 0x3F5, 0x2FC,
                0xDFC, 0xCF5, 0xFFF, 0xEF6, 0x9FA, 0x8F3, 0xBF9, 0xAF0,
                0x650, 0x759, 0x453, 0x55A, 0x256, 0x35F, 0x055, 0x15C,
                0xE5C, 0xF55, 0xC5F, 0xD56, 0xA5A, 0xB53, 0x859, 0x950,
                0x7C0, 0x6C9, 0x5C3, 0x4CA, 0x3C6, 0x2CF, 0x1C5, 0x0CC,
                0xFCC, 0xEC5, 0xDCF, 0xCC6, 0xBCA, 0xAC3, 0x9C9, 0x8C0,
                0x8C0, 0x9C9, 0xAC3, 0xBCA, 0xCC6, 0xDCF, 0xEC5, 0xFCC,
                0x0CC, 0x1C5, 0x2CF, 0x3C6, 0x4CA, 0x5C3, 0x6C9, 0x7C0,
                0x950, 0x859, 0xB53, 0xA5A, 0xD56, 0xC5F, 0xF55, 0xE5C,
                0x15C, 0x055, 0x35F, 0x256, 0x55A, 0x453, 0x759, 0x650,
                0xAF0, 0xBF9, 0x8F3, 0x9FA, 0xEF6, 0xFFF, 0xCF5, 0xDFC,
                0x2FC, 0x3F5, 0x0FF, 0x1F6, 0x6FA, 0x7F3, 0x4F9, 0x5F0,
                0xB60, 0xA69, 0x963, 0x86A, 0xF66, 0xE6F, 0xD65, 0xC6C,
                0x36C, 0x265, 0x16F, 0x066, 0x76A, 0x663, 0x569, 0x460,
                0xCA0, 0xDA9, 0xEA3, 0xFAA, 0x8A6, 0x9AF, 0xAA5, 0xBAC,
                0x4AC, 0x5A5, 0x6AF, 0x7A6, 0x0AA, 0x1A3, 0x2A9, 0x3A0,
                0xD30, 0xC39, 0xF33, 0xE3A, 0x936, 0x83F, 0xB35, 0xA3C,
                0x53C, 0x435, 0x73F, 0x636, 0x13A, 0x033, 0x339, 0x230,
                0xE90, 0xF99, 0xC93, 0xD9A, 0xA96, 0xB9F, 0x895, 0x99C,
                0x69C, 0x795, 0x49F, 0x596, 0x29A, 0x393, 0x099, 0x190,
                0xF00, 0xE09, 0xD03, 0xC0A, 0xB06, 0xA0F, 0x905, 0x80C,
                0x70C, 0x605, 0x50F, 0x406, 0x30A, 0x203, 0x109, 0x000
            };
            _edgeTable = new NativeArray<int>(256, Allocator.Persistent);
            _edgeTable.CopyFrom(et);

            var tt = new int[4096]
            {
                -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,8,3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,1,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,8,3,9,8,1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,8,3,1,2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                9,2,10,0,2,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                2,8,3,2,10,8,10,9,8,-1,-1,-1,-1,-1,-1,-1,
                3,11,2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,11,2,8,11,0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,9,0,2,3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,11,2,1,9,11,9,8,11,-1,-1,-1,-1,-1,-1,-1,
                3,10,1,11,10,3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,10,1,0,8,10,8,11,10,-1,-1,-1,-1,-1,-1,-1,
                3,9,0,3,11,9,11,10,9,-1,-1,-1,-1,-1,-1,-1,
                9,8,10,10,8,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,7,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,3,0,7,3,4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,1,9,8,4,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,1,9,4,7,1,7,3,1,-1,-1,-1,-1,-1,-1,-1,
                1,2,10,8,4,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                3,4,7,3,0,4,1,2,10,-1,-1,-1,-1,-1,-1,-1,
                9,2,10,9,0,2,8,4,7,-1,-1,-1,-1,-1,-1,-1,
                2,10,9,2,9,7,2,7,3,7,9,4,-1,-1,-1,-1,
                8,4,7,3,11,2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                11,4,7,11,2,4,2,0,4,-1,-1,-1,-1,-1,-1,-1,
                9,0,1,8,4,7,2,3,11,-1,-1,-1,-1,-1,-1,-1,
                4,7,11,9,4,11,9,11,2,9,2,1,-1,-1,-1,-1,
                3,10,1,3,11,10,7,8,4,-1,-1,-1,-1,-1,-1,-1,
                1,11,10,1,4,11,1,0,4,7,11,4,-1,-1,-1,-1,
                4,7,8,9,0,11,9,11,10,11,0,3,-1,-1,-1,-1,
                4,7,11,4,11,9,9,11,10,-1,-1,-1,-1,-1,-1,-1,
                9,5,4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                9,5,4,0,8,3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,5,4,1,5,0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                8,5,4,8,3,5,3,1,5,-1,-1,-1,-1,-1,-1,-1,
                1,2,10,9,5,4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                3,0,8,1,2,10,4,9,5,-1,-1,-1,-1,-1,-1,-1,
                5,2,10,5,4,2,4,0,2,-1,-1,-1,-1,-1,-1,-1,
                2,10,5,3,2,5,3,5,4,3,4,8,-1,-1,-1,-1,
                9,5,4,2,3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,11,2,0,8,11,4,9,5,-1,-1,-1,-1,-1,-1,-1,
                0,5,4,0,1,5,2,3,11,-1,-1,-1,-1,-1,-1,-1,
                2,1,5,2,5,8,2,8,11,4,8,5,-1,-1,-1,-1,
                10,3,11,10,1,3,9,5,4,-1,-1,-1,-1,-1,-1,-1,
                4,9,5,0,8,1,8,10,1,8,11,10,-1,-1,-1,-1,
                5,4,0,5,0,11,5,11,10,11,0,3,-1,-1,-1,-1,
                5,4,8,5,8,10,10,8,11,-1,-1,-1,-1,-1,-1,-1,
                9,7,8,5,7,9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                9,3,0,9,5,3,5,7,3,-1,-1,-1,-1,-1,-1,-1,
                0,7,8,0,1,7,1,5,7,-1,-1,-1,-1,-1,-1,-1,
                1,5,3,3,5,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                9,7,8,9,5,7,10,1,2,-1,-1,-1,-1,-1,-1,-1,
                10,1,2,9,5,0,5,3,0,5,7,3,-1,-1,-1,-1,
                8,0,2,8,2,5,8,5,7,10,5,2,-1,-1,-1,-1,
                2,10,5,2,5,3,3,5,7,-1,-1,-1,-1,-1,-1,-1,
                7,9,5,7,8,9,3,11,2,-1,-1,-1,-1,-1,-1,-1,
                9,5,7,9,7,2,9,2,0,2,7,11,-1,-1,-1,-1,
                2,3,11,0,1,8,1,7,8,1,5,7,-1,-1,-1,-1,
                11,2,1,11,1,7,7,1,5,-1,-1,-1,-1,-1,-1,-1,
                9,5,8,8,5,7,10,1,3,10,3,11,-1,-1,-1,-1,
                5,7,0,5,0,9,7,11,0,1,0,10,11,10,0,-1,
                11,10,0,11,0,3,10,5,0,8,0,7,5,7,0,-1,
                11,10,5,7,11,5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                10,6,5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,8,3,5,10,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                9,0,1,5,10,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,8,3,1,9,8,5,10,6,-1,-1,-1,-1,-1,-1,-1,
                1,6,5,2,6,1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,6,5,1,2,6,3,0,8,-1,-1,-1,-1,-1,-1,-1,
                9,6,5,9,0,6,0,2,6,-1,-1,-1,-1,-1,-1,-1,
                5,9,8,5,8,2,5,2,6,3,2,8,-1,-1,-1,-1,
                2,3,11,10,6,5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                11,0,8,11,2,0,10,6,5,-1,-1,-1,-1,-1,-1,-1,
                0,1,9,2,3,11,5,10,6,-1,-1,-1,-1,-1,-1,-1,
                5,10,6,1,9,2,9,11,2,9,8,11,-1,-1,-1,-1,
                6,3,11,6,5,3,5,1,3,-1,-1,-1,-1,-1,-1,-1,
                0,8,11,0,11,5,0,5,1,5,11,6,-1,-1,-1,-1,
                3,11,6,0,3,6,0,6,5,0,5,9,-1,-1,-1,-1,
                6,5,9,6,9,11,11,9,8,-1,-1,-1,-1,-1,-1,-1,
                5,10,6,4,7,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,3,0,4,7,3,6,5,10,-1,-1,-1,-1,-1,-1,-1,
                1,9,0,5,10,6,8,4,7,-1,-1,-1,-1,-1,-1,-1,
                10,6,5,1,9,7,1,7,3,7,9,4,-1,-1,-1,-1,
                6,1,2,6,5,1,4,7,8,-1,-1,-1,-1,-1,-1,-1,
                1,2,5,5,2,6,3,0,4,3,4,7,-1,-1,-1,-1,
                8,4,7,9,0,5,0,6,5,0,2,6,-1,-1,-1,-1,
                7,3,9,7,9,4,3,2,9,5,9,6,2,6,9,-1,
                3,11,2,7,8,4,10,6,5,-1,-1,-1,-1,-1,-1,-1,
                5,10,6,4,7,2,4,2,0,2,7,11,-1,-1,-1,-1,
                0,1,9,4,7,8,2,3,11,5,10,6,-1,-1,-1,-1,
                9,2,1,9,11,2,9,4,11,7,11,4,5,10,6,-1,
                8,4,7,3,11,5,3,5,1,5,11,6,-1,-1,-1,-1,
                5,1,11,5,11,6,1,0,11,7,11,4,0,4,11,-1,
                0,5,9,0,6,5,0,3,6,11,6,3,8,4,7,-1,
                6,5,9,6,9,11,4,7,9,7,11,9,-1,-1,-1,-1,
                10,4,9,6,4,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,10,6,4,9,10,0,8,3,-1,-1,-1,-1,-1,-1,-1,
                10,0,1,10,6,0,6,4,0,-1,-1,-1,-1,-1,-1,-1,
                8,3,1,8,1,6,8,6,4,6,1,10,-1,-1,-1,-1,
                1,4,9,1,2,4,2,6,4,-1,-1,-1,-1,-1,-1,-1,
                3,0,8,1,2,9,2,4,9,2,6,4,-1,-1,-1,-1,
                0,2,4,4,2,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                8,3,2,8,2,4,4,2,6,-1,-1,-1,-1,-1,-1,-1,
                10,4,9,10,6,4,11,2,3,-1,-1,-1,-1,-1,-1,-1,
                0,8,2,2,8,11,4,9,10,4,10,6,-1,-1,-1,-1,
                3,11,2,0,1,6,0,6,4,6,1,10,-1,-1,-1,-1,
                6,4,1,6,1,10,4,8,1,2,1,11,8,11,1,-1,
                9,6,4,9,3,6,9,1,3,11,6,3,-1,-1,-1,-1,
                8,11,1,8,1,0,11,6,1,9,1,4,6,4,1,-1,
                3,11,6,3,6,0,0,6,4,-1,-1,-1,-1,-1,-1,-1,
                6,4,8,11,6,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                7,10,6,7,8,10,8,9,10,-1,-1,-1,-1,-1,-1,-1,
                0,7,3,0,10,7,0,9,10,6,7,10,-1,-1,-1,-1,
                10,6,7,1,10,7,1,7,8,1,8,0,-1,-1,-1,-1,
                10,6,7,10,7,1,1,7,3,-1,-1,-1,-1,-1,-1,-1,
                1,2,6,1,6,8,1,8,9,8,6,7,-1,-1,-1,-1,
                2,6,9,2,9,1,6,7,9,0,9,3,7,3,9,-1,
                7,8,0,7,0,6,6,0,2,-1,-1,-1,-1,-1,-1,-1,
                7,3,2,6,7,2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                2,3,11,10,6,8,10,8,9,8,6,7,-1,-1,-1,-1,
                2,0,7,2,7,11,0,9,7,6,7,10,9,10,7,-1,
                1,8,0,1,7,8,1,10,7,6,7,10,2,3,11,-1,
                11,2,1,11,1,7,10,6,1,6,7,1,-1,-1,-1,-1,
                8,9,6,8,6,7,9,1,6,11,6,3,1,3,6,-1,
                0,9,1,11,6,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                7,8,0,7,0,6,3,11,0,11,6,0,-1,-1,-1,-1,
                7,11,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                7,6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                3,0,8,11,7,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,1,9,11,7,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                8,1,9,8,3,1,11,7,6,-1,-1,-1,-1,-1,-1,-1,
                10,1,2,6,11,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,2,10,3,0,8,6,11,7,-1,-1,-1,-1,-1,-1,-1,
                2,9,0,2,10,9,6,11,7,-1,-1,-1,-1,-1,-1,-1,
                6,11,7,2,10,3,10,8,3,10,9,8,-1,-1,-1,-1,
                7,2,3,6,2,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                7,0,8,7,6,0,6,2,0,-1,-1,-1,-1,-1,-1,-1,
                2,7,6,2,3,7,0,1,9,-1,-1,-1,-1,-1,-1,-1,
                1,6,2,1,8,6,1,9,8,8,7,6,-1,-1,-1,-1,
                10,7,6,10,1,7,1,3,7,-1,-1,-1,-1,-1,-1,-1,
                10,7,6,1,7,10,1,8,7,1,0,8,-1,-1,-1,-1,
                0,3,7,0,7,10,0,10,9,6,10,7,-1,-1,-1,-1,
                7,6,10,7,10,8,8,10,9,-1,-1,-1,-1,-1,-1,-1,
                6,8,4,11,8,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                3,6,11,3,0,6,0,4,6,-1,-1,-1,-1,-1,-1,-1,
                8,6,11,8,4,6,9,0,1,-1,-1,-1,-1,-1,-1,-1,
                9,4,6,9,6,3,9,3,1,11,3,6,-1,-1,-1,-1,
                6,8,4,6,11,8,2,10,1,-1,-1,-1,-1,-1,-1,-1,
                1,2,10,3,0,11,0,6,11,0,4,6,-1,-1,-1,-1,
                4,11,8,4,6,11,0,2,9,2,10,9,-1,-1,-1,-1,
                10,9,3,10,3,2,9,4,3,11,3,6,4,6,3,-1,
                8,2,3,8,4,2,4,6,2,-1,-1,-1,-1,-1,-1,-1,
                0,4,2,4,6,2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,9,0,2,3,4,2,4,6,4,3,8,-1,-1,-1,-1,
                1,9,4,1,4,2,2,4,6,-1,-1,-1,-1,-1,-1,-1,
                8,1,3,8,6,1,8,4,6,6,10,1,-1,-1,-1,-1,
                10,1,0,10,0,6,6,0,4,-1,-1,-1,-1,-1,-1,-1,
                4,6,3,4,3,8,6,10,3,0,3,9,10,9,3,-1,
                10,9,4,6,10,4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,9,5,7,6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,8,3,4,9,5,11,7,6,-1,-1,-1,-1,-1,-1,-1,
                5,0,1,5,4,0,7,6,11,-1,-1,-1,-1,-1,-1,-1,
                11,7,6,8,3,4,3,5,4,3,1,5,-1,-1,-1,-1,
                9,5,4,10,1,2,7,6,11,-1,-1,-1,-1,-1,-1,-1,
                6,11,7,1,2,10,0,8,3,4,9,5,-1,-1,-1,-1,
                7,6,11,5,4,10,4,2,10,4,0,2,-1,-1,-1,-1,
                3,4,8,3,5,4,3,2,5,10,5,2,11,7,6,-1,
                7,2,3,7,6,2,5,4,9,-1,-1,-1,-1,-1,-1,-1,
                9,5,4,0,8,6,0,6,2,6,8,7,-1,-1,-1,-1,
                3,6,2,3,7,6,1,5,0,5,4,0,-1,-1,-1,-1,
                6,2,8,6,8,7,2,1,8,4,8,5,1,5,8,-1,
                9,5,4,10,1,6,1,7,6,1,3,7,-1,-1,-1,-1,
                1,6,10,1,7,6,1,0,7,8,7,0,9,5,4,-1,
                4,0,10,4,10,5,0,3,10,6,10,7,3,7,10,-1,
                7,6,10,7,10,8,5,4,10,4,8,10,-1,-1,-1,-1,
                6,9,5,6,11,9,11,8,9,-1,-1,-1,-1,-1,-1,-1,
                3,6,11,0,6,3,0,5,6,0,9,5,-1,-1,-1,-1,
                0,11,8,0,5,11,0,1,5,5,6,11,-1,-1,-1,-1,
                6,11,3,6,3,5,5,3,1,-1,-1,-1,-1,-1,-1,-1,
                1,2,10,9,5,11,9,11,8,11,5,6,-1,-1,-1,-1,
                0,11,3,0,6,11,0,9,6,5,6,9,1,2,10,-1,
                11,8,5,11,5,6,8,0,5,10,5,2,0,2,5,-1,
                6,11,3,6,3,5,2,10,3,10,5,3,-1,-1,-1,-1,
                5,8,9,5,2,8,5,6,2,3,8,2,-1,-1,-1,-1,
                9,5,6,9,6,0,0,6,2,-1,-1,-1,-1,-1,-1,-1,
                1,5,8,1,8,0,5,6,8,3,8,2,6,2,8,-1,
                1,5,6,2,1,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,3,6,1,6,10,3,8,6,5,6,9,8,9,6,-1,
                10,1,0,10,0,6,9,5,0,5,6,0,-1,-1,-1,-1,
                0,3,8,5,6,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                10,5,6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                11,5,10,7,5,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                11,5,10,11,7,5,8,3,0,-1,-1,-1,-1,-1,-1,-1,
                5,11,7,5,10,11,1,9,0,-1,-1,-1,-1,-1,-1,-1,
                10,7,5,10,11,7,9,8,1,8,3,1,-1,-1,-1,-1,
                11,1,2,11,7,1,7,5,1,-1,-1,-1,-1,-1,-1,-1,
                0,8,3,1,2,7,1,7,5,7,2,11,-1,-1,-1,-1,
                9,7,5,9,2,7,9,0,2,2,11,7,-1,-1,-1,-1,
                7,5,2,7,2,11,5,9,2,3,2,8,9,8,2,-1,
                2,5,10,2,3,5,3,7,5,-1,-1,-1,-1,-1,-1,-1,
                8,2,0,8,5,2,8,7,5,10,2,5,-1,-1,-1,-1,
                9,0,1,5,10,3,5,3,7,3,10,2,-1,-1,-1,-1,
                9,8,2,9,2,1,8,7,2,10,2,5,7,5,2,-1,
                1,3,5,3,7,5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,8,7,0,7,1,1,7,5,-1,-1,-1,-1,-1,-1,-1,
                9,0,3,9,3,5,5,3,7,-1,-1,-1,-1,-1,-1,-1,
                9,8,7,5,9,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                5,8,4,5,10,8,10,11,8,-1,-1,-1,-1,-1,-1,-1,
                5,0,4,5,11,0,5,10,11,11,3,0,-1,-1,-1,-1,
                0,1,9,8,4,10,8,10,11,10,4,5,-1,-1,-1,-1,
                10,11,4,10,4,5,11,3,4,9,4,1,3,1,4,-1,
                2,5,1,2,8,5,2,11,8,4,5,8,-1,-1,-1,-1,
                0,4,11,0,11,3,4,5,11,2,11,1,5,1,11,-1,
                0,2,5,0,5,9,2,11,5,4,5,8,11,8,5,-1,
                9,4,5,2,11,3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                2,5,10,3,5,2,3,4,5,3,8,4,-1,-1,-1,-1,
                5,10,2,5,2,4,4,2,0,-1,-1,-1,-1,-1,-1,-1,
                3,10,2,3,5,10,3,8,5,4,5,8,0,1,9,-1,
                5,10,2,5,2,4,1,9,2,9,4,2,-1,-1,-1,-1,
                8,4,5,8,5,3,3,5,1,-1,-1,-1,-1,-1,-1,-1,
                0,4,5,1,0,5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                8,4,5,8,5,3,9,0,5,0,3,5,-1,-1,-1,-1,
                9,4,5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,11,7,4,9,11,9,10,11,-1,-1,-1,-1,-1,-1,-1,
                0,8,3,4,9,7,9,11,7,9,10,11,-1,-1,-1,-1,
                1,10,11,1,11,4,1,4,0,7,4,11,-1,-1,-1,-1,
                3,1,4,3,4,8,1,10,4,7,4,11,10,11,4,-1,
                4,11,7,9,11,4,9,2,11,9,1,2,-1,-1,-1,-1,
                9,7,4,9,11,7,9,1,11,2,11,1,0,8,3,-1,
                11,7,4,11,4,2,2,4,0,-1,-1,-1,-1,-1,-1,-1,
                11,7,4,11,4,2,8,3,4,3,2,4,-1,-1,-1,-1,
                2,9,10,2,7,9,2,3,7,7,4,9,-1,-1,-1,-1,
                9,10,7,9,7,4,10,2,7,8,7,0,2,0,7,-1,
                3,7,10,3,10,2,7,4,10,1,10,0,4,0,10,-1,
                1,10,2,8,7,4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,9,1,4,1,7,7,1,3,-1,-1,-1,-1,-1,-1,-1,
                4,9,1,4,1,7,0,8,1,8,7,1,-1,-1,-1,-1,
                4,0,3,7,4,3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                4,8,7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                9,10,8,10,11,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                3,0,9,3,9,11,11,9,10,-1,-1,-1,-1,-1,-1,-1,
                0,1,10,0,10,8,8,10,11,-1,-1,-1,-1,-1,-1,-1,
                3,1,10,11,3,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,2,11,1,11,9,9,11,8,-1,-1,-1,-1,-1,-1,-1,
                3,0,9,3,9,11,1,2,9,2,11,9,-1,-1,-1,-1,
                0,2,11,8,0,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                3,2,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                2,3,8,2,8,10,10,8,9,-1,-1,-1,-1,-1,-1,-1,
                9,10,2,0,9,2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                2,3,8,2,8,10,0,1,8,1,10,8,-1,-1,-1,-1,
                1,10,2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                1,3,8,9,1,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,9,1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                0,3,8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
                -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1
            };
            _triTable = new NativeArray<int>(4096, Allocator.Persistent);
            _triTable.CopyFrom(tt);

            Volatile.Write(ref _ready, 1);
        }
    }

    public static void Shutdown()
    {
        lock (_initLock)
        {
            if (_edgeTable.IsCreated) _edgeTable.Dispose();
            if (_triTable.IsCreated)  _triTable.Dispose();
            Volatile.Write(ref _ready, 0);
        }
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: BURST JOBS
// ════════════════════════════════════════════════════════════════════════════════
#region Voxel Burst Jobs

// ── JOB 1: Density Field ────────────────────────────────────────────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelDensityJob : IJobParallelFor
{
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    public VoxelPOIType poiType;
    public float3 sdfCenter;
    public float3 sdfSize;

    public NoiseData wallNoise;
    public float wallNoiseAmp;
    public float sealMargin;

    [ReadOnly] public NativeArray<float> terrainHeights;

    [WriteOnly] public NativeArray<float> density;

    public void Execute(int idx)
    {
        int ix = idx % ptsX;
        int iy = (idx / ptsX) % ptsY;
        int iz = idx / (ptsX * ptsY);

        float3 wp = volumeOrigin + new float3(ix, iy, iz) * voxelStep;

        float terrainH = terrainHeights[ix + iz * ptsX];
        float terrainDensity = math.clamp(terrainH - wp.y, -50f, 50f);

        float sdfDist;
        if (poiType == VoxelPOIType.Cave)
        {
            sdfDist = math.length(wp - sdfCenter) - sdfSize.x;
        }
        else
        {
            float3 d = math.abs(wp - sdfCenter) - sdfSize;
            sdfDist = math.length(math.max(d, 0f)) + math.min(math.cmax(d), 0f);
        }

        float noiseVal = 0f;
        if (wallNoiseAmp > 0.001f)
        {
            noiseVal = HectonNoise.Fractal3D(wp.x, wp.y, wp.z, wallNoise, 500f);
            noiseVal = (noiseVal * 2f - 1f) * wallNoiseAmp;
        }

        float perturbedSDF = sdfDist + noiseVal;

        float d_final = math.min(terrainDensity, perturbedSDF);

        float3 localPos = wp - volumeOrigin;
        float3 volumeSize = new float3(ptsX - 1, ptsY - 1, ptsZ - 1) * voxelStep;
        float dMinX = math.min(localPos.x, volumeSize.x - localPos.x);
        float dMinY = math.min(localPos.y, volumeSize.y - localPos.y);
        float dMinZ = math.min(localPos.z, volumeSize.z - localPos.z);
        float dEdge = math.min(dMinX, math.min(dMinY, dMinZ));
        float sealFactor = math.saturate(dEdge / math.max(sealMargin, 0.01f));
        d_final = math.lerp(1f, d_final, sealFactor);

        density[idx] = d_final;
    }
}

// ── JOB 2: Marching Cubes — outputs raw vertices with edge IDs ──────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public unsafe struct VoxelMCExtractJob : IJobParallelFor
{
    public int cellsX, cellsY, cellsZ;
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<int> edgeTable;
    [ReadOnly] public NativeArray<int> triTable;

    [NativeDisableContainerSafetyRestriction]
    public NativeArray<MCRawVertex> outVertices;

    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> vertexCounter;

    public void Execute(int cellIdx)
    {
        int cx = cellIdx % cellsX;
        int cy = (cellIdx / cellsX) % cellsY;
        int cz = cellIdx / (cellsX * cellsY);

        float d0 = D(cx,     cy,     cz);
        float d1 = D(cx + 1, cy,     cz);
        float d2 = D(cx + 1, cy + 1, cz);
        float d3 = D(cx,     cy + 1, cz);
        float d4 = D(cx,     cy,     cz + 1);
        float d5 = D(cx + 1, cy,     cz + 1);
        float d6 = D(cx + 1, cy + 1, cz + 1);
        float d7 = D(cx,     cy + 1, cz + 1);

        int cubeIndex = 0;
        if (d0 < 0f) cubeIndex |= 1;
        if (d1 < 0f) cubeIndex |= 2;
        if (d2 < 0f) cubeIndex |= 4;
        if (d3 < 0f) cubeIndex |= 8;
        if (d4 < 0f) cubeIndex |= 16;
        if (d5 < 0f) cubeIndex |= 32;
        if (d6 < 0f) cubeIndex |= 64;
        if (d7 < 0f) cubeIndex |= 128;

        int edgeBits = edgeTable[cubeIndex];
        if (edgeBits == 0) return;

        float3 p0 = P(cx,     cy,     cz);
        float3 p1 = P(cx + 1, cy,     cz);
        float3 p2 = P(cx + 1, cy + 1, cz);
        float3 p3 = P(cx,     cy + 1, cz);
        float3 p4 = P(cx,     cy,     cz + 1);
        float3 p5 = P(cx + 1, cy,     cz + 1);
        float3 p6 = P(cx + 1, cy + 1, cz + 1);
        float3 p7 = P(cx,     cy + 1, cz + 1);

        int g0 = GI(cx,     cy,     cz);
        int g1 = GI(cx + 1, cy,     cz);
        int g2 = GI(cx + 1, cy + 1, cz);
        int g3 = GI(cx,     cy + 1, cz);
        int g4 = GI(cx,     cy,     cz + 1);
        int g5 = GI(cx + 1, cy,     cz + 1);
        int g6 = GI(cx + 1, cy + 1, cz + 1);
        int g7 = GI(cx,     cy + 1, cz + 1);

        float3 ev0 = float3.zero, ev1 = float3.zero, ev2 = float3.zero, ev3 = float3.zero;
        float3 ev4 = float3.zero, ev5 = float3.zero, ev6 = float3.zero, ev7 = float3.zero;
        float3 ev8 = float3.zero, ev9 = float3.zero, ev10 = float3.zero, ev11 = float3.zero;

        long eid0 = 0, eid1 = 0, eid2 = 0, eid3 = 0;
        long eid4 = 0, eid5 = 0, eid6 = 0, eid7 = 0;
        long eid8 = 0, eid9 = 0, eid10 = 0, eid11 = 0;

        if ((edgeBits &    1) != 0) { ev0  = Lerp(p0, p1, d0, d1); eid0  = PackEdge(g0, g1); }
        if ((edgeBits &    2) != 0) { ev1  = Lerp(p1, p2, d1, d2); eid1  = PackEdge(g1, g2); }
        if ((edgeBits &    4) != 0) { ev2  = Lerp(p2, p3, d2, d3); eid2  = PackEdge(g2, g3); }
        if ((edgeBits &    8) != 0) { ev3  = Lerp(p3, p0, d3, d0); eid3  = PackEdge(g3, g0); }
        if ((edgeBits &   16) != 0) { ev4  = Lerp(p4, p5, d4, d5); eid4  = PackEdge(g4, g5); }
        if ((edgeBits &   32) != 0) { ev5  = Lerp(p5, p6, d5, d6); eid5  = PackEdge(g5, g6); }
        if ((edgeBits &   64) != 0) { ev6  = Lerp(p6, p7, d6, d7); eid6  = PackEdge(g6, g7); }
        if ((edgeBits &  128) != 0) { ev7  = Lerp(p7, p4, d7, d4); eid7  = PackEdge(g7, g4); }
        if ((edgeBits &  256) != 0) { ev8  = Lerp(p0, p4, d0, d4); eid8  = PackEdge(g0, g4); }
        if ((edgeBits &  512) != 0) { ev9  = Lerp(p1, p5, d1, d5); eid9  = PackEdge(g1, g5); }
        if ((edgeBits & 1024) != 0) { ev10 = Lerp(p2, p6, d2, d6); eid10 = PackEdge(g2, g6); }
        if ((edgeBits & 2048) != 0) { ev11 = Lerp(p3, p7, d3, d7); eid11 = PackEdge(g3, g7); }

        int triBase = cubeIndex * 16;
        int triCount = 0;
        for (int t = 0; t < 15; t += 3)
        {
            if (triTable[triBase + t] == -1) break;
            triCount++;
        }
        if (triCount == 0) return;

        int vertCount = triCount * 3;

        int* counterPtr = (int*)vertexCounter.GetUnsafePtr();
        int writeOffset = System.Threading.Interlocked.Add(ref *counterPtr, vertCount) - vertCount;
        if (writeOffset + vertCount > outVertices.Length) return;

        int wi = writeOffset;
        for (int t = 0; t < 15; t += 3)
        {
            int e0 = triTable[triBase + t];
            if (e0 == -1) break;
            int e1 = triTable[triBase + t + 1];
            int e2 = triTable[triBase + t + 2];

            outVertices[wi]     = new MCRawVertex
            {
                position = GetEV(e0, ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId   = GetEID(e0, eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11)
            };
            outVertices[wi + 1] = new MCRawVertex
            {
                position = GetEV(e1, ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId   = GetEID(e1, eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11)
            };
            outVertices[wi + 2] = new MCRawVertex
            {
                position = GetEV(e2, ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId   = GetEID(e2, eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11)
            };
            wi += 3;
        }
    }

    int GI(int ix, int iy, int iz) => ix + iy * ptsX + iz * ptsX * ptsY;
    float D(int ix, int iy, int iz) => density[GI(ix, iy, iz)];
    float3 P(int ix, int iy, int iz) => volumeOrigin + new float3(ix, iy, iz) * voxelStep;

    static float3 Lerp(float3 pA, float3 pB, float dA, float dB)
    {
        float diff = dA - dB;
        if (math.abs(diff) < 1e-6f) return (pA + pB) * 0.5f;
        float t = math.clamp(dA / diff, 0f, 1f);
        return pA + t * (pB - pA);
    }

    static long PackEdge(int gA, int gB)
    {
        int lo = math.min(gA, gB);
        int hi = math.max(gA, gB);
        return ((long)hi << 32) | (uint)lo;
    }

    static float3 GetEV(int e,
        float3 v0, float3 v1, float3 v2, float3 v3,
        float3 v4, float3 v5, float3 v6, float3 v7,
        float3 v8, float3 v9, float3 v10, float3 v11)
    {
        switch (e)
        {
            case 0:  return v0;  case 1:  return v1;  case 2:  return v2;
            case 3:  return v3;  case 4:  return v4;  case 5:  return v5;
            case 6:  return v6;  case 7:  return v7;  case 8:  return v8;
            case 9:  return v9;  case 10: return v10; case 11: return v11;
            default: return float3.zero;
        }
    }

    static long GetEID(int e,
        long id0, long id1, long id2, long id3,
        long id4, long id5, long id6, long id7,
        long id8, long id9, long id10, long id11)
    {
        switch (e)
        {
            case 0:  return id0;  case 1:  return id1;  case 2:  return id2;
            case 3:  return id3;  case 4:  return id4;  case 5:  return id5;
            case 6:  return id6;  case 7:  return id7;  case 8:  return id8;
            case 9:  return id9;  case 10: return id10; case 11: return id11;
            default: return 0;
        }
    }
}

// ── JOB 2.5: Vertex Welding — Zero-GC via NativeParallelHashMap ─────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public unsafe struct VoxelWeldJob : IJob
{
    public int rawCount;

    [ReadOnly]  public NativeArray<MCRawVertex> rawVertices;

    public NativeParallelHashMap<long, int> edgeToVertex;

    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<float3> weldedPositions;

    [WriteOnly, NativeDisableContainerSafetyRestriction]
    public NativeArray<int> triangleIndices;

    public NativeArray<int> weldedCounter;

    public void Execute()
    {
        int weldedCount = 0;

        for (int i = 0; i < rawCount; i++)
        {
            MCRawVertex rv = rawVertices[i];

            if (edgeToVertex.TryGetValue(rv.edgeId, out int existingIdx))
            {
                triangleIndices[i] = existingIdx;
            }
            else
            {
                int newIdx = weldedCount;
                weldedPositions[newIdx] = rv.position;
                edgeToVertex.Add(rv.edgeId, newIdx);
                triangleIndices[i] = newIdx;
                weldedCount++;
            }
        }

        weldedCounter[0] = weldedCount;
    }
}

// ── JOB 3: Normals from density gradient ────────────────────────────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelNormalJob : IJobParallelFor
{
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<float3> positions;

    [WriteOnly] public NativeArray<float3> normals;

    public void Execute(int idx)
    {
        float3 wp = positions[idx];
        float3 lp = (wp - volumeOrigin) / voxelStep;

        const float eps = 0.5f;
        float dxp = Sample(lp + new float3(eps, 0, 0));
        float dxm = Sample(lp - new float3(eps, 0, 0));
        float dyp = Sample(lp + new float3(0, eps, 0));
        float dym = Sample(lp - new float3(0, eps, 0));
        float dzp = Sample(lp + new float3(0, 0, eps));
        float dzm = Sample(lp - new float3(0, 0, eps));

        float3 grad = new float3(dxp - dxm, dyp - dym, dzp - dzm);
        normals[idx] = math.normalizesafe(-grad, new float3(0, 1, 0));
    }

    float Sample(float3 lp)
    {
        lp = math.clamp(lp, float3.zero, new float3(ptsX - 1, ptsY - 1, ptsZ - 1));
        int x0 = (int)lp.x; int x1 = math.min(x0 + 1, ptsX - 1);
        int y0 = (int)lp.y; int y1 = math.min(y0 + 1, ptsY - 1);
        int z0 = (int)lp.z; int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = lp.x - x0, fy = lp.y - y0, fz = lp.z - z0;

        float c000 = density[x0 + y0 * ptsX + z0 * ptsX * ptsY];
        float c100 = density[x1 + y0 * ptsX + z0 * ptsX * ptsY];
        float c010 = density[x0 + y1 * ptsX + z0 * ptsX * ptsY];
        float c110 = density[x1 + y1 * ptsX + z0 * ptsX * ptsY];
        float c001 = density[x0 + y0 * ptsX + z1 * ptsX * ptsY];
        float c101 = density[x1 + y0 * ptsX + z1 * ptsX * ptsY];
        float c011 = density[x0 + y1 * ptsX + z1 * ptsX * ptsY];
        float c111 = density[x1 + y1 * ptsX + z1 * ptsX * ptsY];

        float c00 = math.lerp(c000, c100, fx);
        float c10 = math.lerp(c010, c110, fx);
        float c01 = math.lerp(c001, c101, fx);
        float c11 = math.lerp(c011, c111, fx);
        return math.lerp(math.lerp(c00, c10, fy), math.lerp(c01, c11, fy), fz);
    }
}

// ── JOB 3.5: Biome Sampling — per-vertex biome from grid ────────────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelBiomeSampleJob : IJobParallelFor
{
    public int ptsX, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    [ReadOnly] public NativeArray<float> gridBiome;
    [ReadOnly] public NativeArray<float3> positions;

    [WriteOnly] public NativeArray<float> biomeValues;

    public void Execute(int idx)
    {
        float3 wp = positions[idx];
        float lx = (wp.x - volumeOrigin.x) / voxelStep;
        float lz = (wp.z - volumeOrigin.z) / voxelStep;

        lx = math.clamp(lx, 0f, ptsX - 1f);
        lz = math.clamp(lz, 0f, ptsZ - 1f);

        int x0 = (int)lx, z0 = (int)lz;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = lx - x0, fz = lz - z0;

        float v00 = gridBiome[x0 + z0 * ptsX];
        float v10 = gridBiome[x1 + z0 * ptsX];
        float v01 = gridBiome[x0 + z1 * ptsX];
        float v11 = gridBiome[x1 + z1 * ptsX];

        biomeValues[idx] = math.lerp(math.lerp(v00, v10, fx), math.lerp(v01, v11, fx), fz);
    }
}

// ── JOB 4: Vertex Colors ─────────────────────────────────────────────────────
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelColorJob : IJobParallelFor
{
    public float maxDepth;
    public float3 sdfCenter;
    public float3 sdfSize;
    public VoxelPOIType poiType;
    public float caveEdgeWidth;

    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> normals;
    [ReadOnly] public NativeArray<float>  biomeValues;

    [WriteOnly] public NativeArray<Color> colors;

    public void Execute(int idx)
    {
        float3 p = positions[idx];
        float3 n = normals[idx];

        float slope = 1f - math.abs(math.dot(n, new float3(0, 1, 0)));
        float depth = math.saturate(-p.y / maxDepth);

        float sdfDist;
        if (poiType == VoxelPOIType.Cave)
        {
            sdfDist = math.length(p - sdfCenter) - sdfSize.x;
        }
        else
        {
            float3 d = math.abs(p - sdfCenter) - sdfSize;
            sdfDist = math.length(math.max(d, 0f)) + math.min(math.cmax(d), 0f);
        }
        float ce    = 1f - math.saturate(math.abs(sdfDist) / math.max(caveEdgeWidth, 0.01f));
        float biome = biomeValues[idx];

        colors[idx] = new Color(slope, depth, ce, biome);
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: HECTON VOXEL ENGINE
// ════════════════════════════════════════════════════════════════════════════════
#region HectonVoxelEngine

[ExecuteAlways]
public class HectonVoxelEngine : MonoBehaviour
{
    // ╔═══════════════════════════════════════════════╗
    // ║           INSPECTOR SETTINGS                  ║
    // ╚═══════════════════════════════════════════════╝

    [Header("═══ VOXEL GRID ═══")]
    [Tooltip("Voxel step size (m). Smaller = more detail + more tris.")]
    [Range(0.5f, 4f)]
    public float voxelResolution = 1.0f;

    [Tooltip("Hard cap per axis. Prevents OOM on weak GPUs.")]
    [Range(16, 64)]
    public int maxGridDimension = 48;

    [Header("═══ WALL NOISE (Global Default) ═══")]
    public HectonNoiseLayer wallNoise = new HectonNoiseLayer
        { scale = 0.08f, octaves = 3, lacunarity = 2.0f, persistence = 0.5f, seed = 888 };

    [Range(0f, 10f)]
    public float wallNoiseAmplitude = 3f;

    [Header("═══ EDGE SEAL ═══")]
    [Tooltip("Margin (m) where density fades to solid at volume borders.")]
    [Range(1f, 8f)]
    public float sealMargin = 3f;

    [Header("═══ CAVE EDGE COLOR ═══")]
    [Tooltip("Width (m) of the soft cave-edge transition in vertex color B channel.")]
    [Range(1f, 20f)]
    public float caveEdgeColorWidth = 5f;

    [Header("═══ POI DEFINITIONS ═══")]
    public VoxelPOIDefinition[] poiDefinitions = new VoxelPOIDefinition[]
    {
        new VoxelPOIDefinition
        {
            type = VoxelPOIType.Cave,
            label = "Standard Cave",
            sdfSize = new Vector3(12f, 12f, 12f),
            volumePadding = 4f
        },
        new VoxelPOIDefinition
        {
            type = VoxelPOIType.DeepRift,
            label = "Tectonic Rift",
            sdfSize = new Vector3(8f, 40f, 60f),
            volumePadding = 6f
        }
    };

    [Header("═══ RENDERING ═══")]
    public Material voxelMaterial;

    [Header("═══ REFERENCES ═══")]
    [Tooltip("Bridge to MapMagic terrain for height sampling. Replaces old HectonWorldGenerator.")]
    public MapMagicBridge mapMagicBridge;

    [Header("═══ POOL ═══")]
    [Tooltip("Prefab for pooled voxel volume GameObjects (empty GO with MeshFilter+MeshRenderer+MeshCollider).")]
    public GameObject voxelVolumePrefab;

    // ── Constants ──
    /// <summary>Abyssal max depth constant (m). Used for depth-based vertex coloring.</summary>
    const float ABYSSAL_MAX_DEPTH = 5000f;

    // ── Internal ──
    readonly List<GameObject> _activeVolumes = new List<GameObject>();
    const int JOB_BATCH = 64;

    // ╔═══════════════════════════════════════════════╗
    // ║              LIFECYCLE                        ║
    // ╚═══════════════════════════════════════════════╝

    void OnEnable()  { MCTables.Initialize(); }
    void OnDisable() { ClearAllVolumes(); }

    void OnDestroy()
    {
        ClearAllVolumes();
        MCTables.Shutdown();
    }

    // ╔═══════════════════════════════════════════════╗
    // ║              PUBLIC API                       ║
    // ╚═══════════════════════════════════════════════╝

    /// <summary>
    /// Fully asynchronous volume generation. Heavy computation runs via Job System
    /// with async awaiting (no main-thread blocking). Mesh finalization happens on
    /// main thread after all jobs complete.
    ///
    /// v3.2: Uses Unity 6 Awaitable API instead of System.Threading.Tasks.Task.
    ///   • Awaitable is pooled by Unity runtime — zero GC per await.
    ///   • NextFrameAsync yields to Unity Player Loop (not ThreadPool).
    ///   • CancellationToken properly threaded through all await points.
    ///
    /// Terrain heights are sampled from MapMagicBridge.TryGetHeight() on the main
    /// thread BEFORE any Burst jobs are scheduled (Unity Terrain API is main-thread only).
    /// </summary>
    public async Awaitable<GameObject> GenerateVolumeAsync(Vector3 worldCenter,
                                                            VoxelPOIType poiType,
                                                            Vector3 sdfSizeOverride = default,
                                                            CancellationToken ct = default)
    {
        if (mapMagicBridge == null)
        {
            Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
            return null;
        }

        MCTables.Initialize();

        VoxelPOIDefinition def = FindDefinition(poiType);
        if (def == null) def = new VoxelPOIDefinition { type = poiType };

        Vector3 sdfSize = sdfSizeOverride != Vector3.zero ? sdfSizeOverride : def.sdfSize;
        float padding = def.volumePadding;

        Vector3 volumeExtents = sdfSize + Vector3.one * (padding + sealMargin) * 2f;
        int gridX = Mathf.Min(Mathf.CeilToInt(volumeExtents.x / voxelResolution), maxGridDimension);
        int gridY = Mathf.Min(Mathf.CeilToInt(volumeExtents.y / voxelResolution), maxGridDimension);
        int gridZ = Mathf.Min(Mathf.CeilToInt(volumeExtents.z / voxelResolution), maxGridDimension);

        int ptsX = gridX + 1, ptsY = gridY + 1, ptsZ = gridZ + 1;
        int totalPts   = ptsX * ptsY * ptsZ;
        int totalCells = gridX * gridY * gridZ;
        int maxVerts   = totalCells * 15;

        float3 actualSize    = new float3(gridX, gridY, gridZ) * voxelResolution;
        float3 volumeOrigin  = (float3)worldCenter - actualSize * 0.5f;

        NoiseData wn;
        float wnAmp;
        if (def.overrideNoise)
        { wn = NoiseData.From(def.wallNoise); wnAmp = def.wallNoiseAmplitude; }
        else
        { wn = NoiseData.From(wallNoise); wnAmp = wallNoiseAmplitude; }

        float3 sdfSizeForJob = poiType == VoxelPOIType.Cave
            ? new float3(sdfSize.x, 0, 0) : (float3)sdfSize;

        // ── Fallback height when MapMagic tile is not yet ready ──
        float fallbackHeight = worldCenter.y - 10f;

        // ═══════════════════════════════════════════════════════════════════
        //  ALLOCATE ALL NATIVE CONTAINERS (Persistent — survives async gap)
        // ═══════════════════════════════════════════════════════════════════

        var terrainHeights = new NativeArray<float>(ptsX * ptsZ, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
        var gridBiome = new NativeArray<float>(ptsX * ptsZ, Allocator.Persistent,
                                               NativeArrayOptions.UninitializedMemory);
        var densityField = new NativeArray<float>(totalPts, Allocator.Persistent,
                                                   NativeArrayOptions.UninitializedMemory);
        var rawVerts = new NativeArray<MCRawVertex>(maxVerts, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
        var counter = new NativeArray<int>(1, Allocator.Persistent,
                                            NativeArrayOptions.ClearMemory);

        // Welding containers — sized for worst case, actual count determined by weld job
        var weldedPositions = new NativeArray<float3>(maxVerts, Allocator.Persistent,
                                                       NativeArrayOptions.UninitializedMemory);
        var triangleIndices = new NativeArray<int>(maxVerts, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);
        var weldedCounter = new NativeArray<int>(1, Allocator.Persistent,
                                                  NativeArrayOptions.ClearMemory);
        var edgeToVertex = new NativeParallelHashMap<long, int>(maxVerts / 2, Allocator.Persistent);

        try
        {
            // ═════════════════════════════════════════════════════════════════
            //  PHASE 0: Sample terrain heights & biome (MAIN THREAD — required
            //  because MapMagicBridge internally calls Unity Terrain API which
            //  is not thread-safe). Must complete before any Burst jobs start.
            // ═════════════════════════════════════════════════════════════════
            for (int iz = 0; iz < ptsZ; iz++)
            for (int ix = 0; ix < ptsX; ix++)
            {
                float wx = volumeOrigin.x + ix * voxelResolution;
                float wz = volumeOrigin.z + iz * voxelResolution;
                int hi = ix + iz * ptsX;

                // Height from MapMagicBridge with safe fallback
                if (mapMagicBridge.TryGetHeight(wx, wz, out float height))
                {
                    terrainHeights[hi] = height;
                }
                else
                {
                    // Tile not ready — use fallback to prevent geometry "in the air"
                    terrainHeights[hi] = fallbackHeight;
                }

                // Biome stub: no MapMagic biome masks wired yet
                gridBiome[hi] = 0f;
            }

            ct.ThrowIfCancellationRequested();

            // ═════════════════════════════════════════════════════════════════
            //  PHASE 1: Density field (async)
            // ═════════════════════════════════════════════════════════════════
            var densityHandle = new VoxelDensityJob
            {
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin,
                voxelStep = voxelResolution,
                poiType = poiType,
                sdfCenter = worldCenter,
                sdfSize = sdfSizeForJob,
                wallNoise = wn, wallNoiseAmp = wnAmp,
                sealMargin = sealMargin,
                terrainHeights = terrainHeights,
                density = densityField
            }.Schedule(totalPts, JOB_BATCH);

            // ═════════════════════════════════════════════════════════════════
            //  PHASE 2: MC extraction (depends on density)
            // ═════════════════════════════════════════════════════════════════
            var mcHandle = new VoxelMCExtractJob
            {
                cellsX = gridX, cellsY = gridY, cellsZ = gridZ,
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin,
                voxelStep = voxelResolution,
                density = densityField,
                edgeTable = MCTables.EdgeTable,
                triTable  = MCTables.TriTable,
                outVertices   = rawVerts,
                vertexCounter = counter
            }.Schedule(totalCells, JOB_BATCH, densityHandle);

            // Await MC completion asynchronously (v3.2: Awaitable.NextFrameAsync)
            while (!mcHandle.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(ct);
            }
            mcHandle.Complete();

            int rawCount = counter[0];
            if (rawCount < 3)
            {
                return null;
            }

            ct.ThrowIfCancellationRequested();

            // ═════════════════════════════════════════════════════════════════
            //  PHASE 3: Vertex Welding (zero-GC, IJob, async)
            // ═════════════════════════════════════════════════════════════════
            var weldHandle = new VoxelWeldJob
            {
                rawCount = rawCount,
                rawVertices = rawVerts,
                edgeToVertex = edgeToVertex,
                weldedPositions = weldedPositions,
                triangleIndices = triangleIndices,
                weldedCounter = weldedCounter
            }.Schedule();

            while (!weldHandle.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(ct);
            }
            weldHandle.Complete();

            int weldedCount = weldedCounter[0];
            if (weldedCount < 3)
            {
                return null;
            }

            ct.ThrowIfCancellationRequested();

            // ═════════════════════════════════════════════════════════════════
            //  PHASE 4: Normals + Biome + Colors (chained, async)
            // ═════════════════════════════════════════════════════════════════
            var normals   = new NativeArray<float3>(weldedCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            var biomeVals = new NativeArray<float>(weldedCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);
            var colors    = new NativeArray<Color>(weldedCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);

            try
            {
                // 4a: normals (parallel, no dependency)
                var normalHandle = new VoxelNormalJob
                {
                    ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin,
                    voxelStep = voxelResolution,
                    density   = densityField,
                    positions = weldedPositions,
                    normals   = normals
                }.Schedule(weldedCount, JOB_BATCH);

                // 4b: biome sampling (parallel, no dependency)
                var biomeHandle = new VoxelBiomeSampleJob
                {
                    ptsX = ptsX, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin,
                    voxelStep = voxelResolution,
                    gridBiome = gridBiome,
                    positions = weldedPositions,
                    biomeValues = biomeVals
                }.Schedule(weldedCount, JOB_BATCH);

                // 4c: colors (depends on normals + biome)
                var colorDeps = JobHandle.CombineDependencies(normalHandle, biomeHandle);
                var colorHandle = new VoxelColorJob
                {
                    maxDepth = ABYSSAL_MAX_DEPTH,
                    sdfCenter = worldCenter,
                    sdfSize = sdfSizeForJob,
                    poiType = poiType,
                    caveEdgeWidth = caveEdgeColorWidth,
                    positions   = weldedPositions,
                    normals     = normals,
                    biomeValues = biomeVals,
                    colors      = colors
                }.Schedule(weldedCount, JOB_BATCH, colorDeps);

                // Await full chain (v3.2: Awaitable.NextFrameAsync)
                while (!colorHandle.IsCompleted)
                {
                    ct.ThrowIfCancellationRequested();
                    await Awaitable.NextFrameAsync(ct);
                }
                colorHandle.Complete();

                ct.ThrowIfCancellationRequested();

                // ═════════════════════════════════════════════════════════════
                //  PHASE 5: Build mesh (main thread)
                // ═════════════════════════════════════════════════════════════
                GameObject targetGO = SpawnVolume();
                targetGO.name = $"VoxelVolume_{poiType}_{worldCenter:F0}";
                targetGO.transform.position = Vector3.zero;

                BuildWeldedMeshNative(targetGO, weldedPositions, normals, colors,
                                     triangleIndices, rawCount, weldedCount, voxelMaterial);
                _activeVolumes.Add(targetGO);

                Debug.Log($"[HectonVoxel] Volume '{targetGO.name}': " +
                          $"{rawCount} raw → {weldedCount} welded verts " +
                          $"({(1f - (float)weldedCount / rawCount) * 100f:F0}% reduction), " +
                          $"{rawCount / 3} tris");

                return targetGO;
            }
            finally
            {
                if (normals.IsCreated)   normals.Dispose();
                if (biomeVals.IsCreated) biomeVals.Dispose();
                if (colors.IsCreated)    colors.Dispose();
            }
        }
        finally
        {
            // ═══ STRICT DISPOSE OF ALL PERSISTENT CONTAINERS ═══
            if (terrainHeights.IsCreated)  terrainHeights.Dispose();
            if (gridBiome.IsCreated)       gridBiome.Dispose();
            if (densityField.IsCreated)    densityField.Dispose();
            if (rawVerts.IsCreated)        rawVerts.Dispose();
            if (counter.IsCreated)         counter.Dispose();
            if (weldedPositions.IsCreated) weldedPositions.Dispose();
            if (triangleIndices.IsCreated) triangleIndices.Dispose();
            if (weldedCounter.IsCreated)   weldedCounter.Dispose();
            if (edgeToVertex.IsCreated)    edgeToVertex.Dispose();
        }
    }

    /// <summary>
    /// Synchronous wrapper — keeps backward compatibility.
    /// Calls sync fallback pipeline with blocking .Complete() on all jobs.
    /// Prefer GenerateVolumeAsync for production use.
    /// </summary>
    public void GenerateVolume(GameObject targetGO, Vector3 worldCenter,
                               VoxelPOIType poiType, Vector3 sdfSizeOverride = default)
    {
        GenerateVolumeSyncFallback(targetGO, worldCenter, poiType, sdfSizeOverride);
    }

    /// <summary>
    /// Despawns a volume, cleans its mesh, and returns it to the pool.
    /// </summary>
    public void DespawnVolume(GameObject volume)
    {
        if (volume == null) return;

        _activeVolumes.Remove(volume);

        var mf = volume.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            mf.sharedMesh.Clear();
            SafeDestroy(mf.sharedMesh);
            mf.sharedMesh = null;
        }

        var mc = volume.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = null;

        if (ObjectPoolManager.Instance != null && voxelVolumePrefab != null)
        {
            ObjectPoolManager.Instance.Despawn(volume);
        }
        else
        {
            SafeDestroy(volume);
        }
    }
    // ══════════════════════════════════════════════════════════
    //  PUBLIC API — PURGE NULL VOLUMES (v3.3)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Удаляет все null-ссылки из _activeVolumes.
    /// Reverse swap-remove — O(n), zero GC, zero сдвигов массива.
    ///
    /// КОГДА ВЫЗЫВАТЬ:
    ///   • HectonWorldGenerator.DestroyChunk через DespawnVolume (штатный путь).
    ///   • ClearAllVolumes (safety cleanup).
    ///   • Внешний код, если подозревает утечку ссылок.
    ///
    /// ПОЧЕМУ НУЖЕН:
    ///   Если внешний код уничтожает вокс-объект через Destroy()
    ///   напрямую (минуя DespawnVolume), ссылка в _activeVolumes
    ///   становится "fake null" (Unity destroyed object).
    ///   PurgeNullVolumes чистит такие записи.
    /// </summary>
    public void PurgeNullVolumes()
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] == null)
            {
                // Swap-remove: O(1) per element, no array shift
                int last = _activeVolumes.Count - 1;
                _activeVolumes[i] = _activeVolumes[last];
                _activeVolumes.RemoveAt(last);
            }
        }
    }
    public void ClearAllVolumes()
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] != null)
            {
                var mf = _activeVolumes[i].GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    mf.sharedMesh.Clear();
                    SafeDestroy(mf.sharedMesh);
                    mf.sharedMesh = null;
                }

                var mc = _activeVolumes[i].GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = null;

                if (ObjectPoolManager.Instance != null && voxelVolumePrefab != null)
                {
                    ObjectPoolManager.Instance.Despawn(_activeVolumes[i]);
                }
                else
                {
                    SafeDestroy(_activeVolumes[i]);
                }
            }
        }
        _activeVolumes.Clear();

        // v3.3: Safety — в случае если Clear не поможет
        // (теоретически невозможно после Clear, но belt & suspenders)
        // PurgeNullVolumes(); // не нужен после Clear(), но оставляем комментарий
    }

    public int ActiveVolumeCount => _activeVolumes.Count;

    // ╔═══════════════════════════════════════════════╗
    // ║            INTERNAL HELPERS                   ║
    // ╚═══════════════════════════════════════════════╝

    GameObject SpawnVolume()
    {
        if (ObjectPoolManager.Instance != null && voxelVolumePrefab != null)
        {
            return ObjectPoolManager.Instance.Spawn(voxelVolumePrefab, Vector3.zero,
                                                     Quaternion.identity);
        }

        var go = new GameObject("VoxelVolume");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();
        return go;
    }

    VoxelPOIDefinition FindDefinition(VoxelPOIType type)
    {
        if (poiDefinitions == null) return null;
        for (int i = 0; i < poiDefinitions.Length; i++)
            if (poiDefinitions[i] != null && poiDefinitions[i].type == type)
                return poiDefinitions[i];
        return null;
    }

    /// <summary>
    /// Samples terrain height from MapMagicBridge with safe fallback.
    /// MUST be called from main thread only.
    /// </summary>
    float SampleTerrainHeight(float wx, float wz, float fallbackHeight)
    {
        if (mapMagicBridge != null && mapMagicBridge.TryGetHeight(wx, wz, out float h))
            return h;
        return fallbackHeight;
    }

    void BuildWeldedMeshNative(GameObject go,
                               NativeArray<float3> positions,
                               NativeArray<float3> normals,
                               NativeArray<Color>  colors,
                               NativeArray<int>    triangleIndices,
                               int triIndexCount,
                               int vertCount,
                               Material mat)
    {
        var mesh = new Mesh();
        mesh.name = $"VoxelWelded_{go.name}";
        if (vertCount > 65535) mesh.indexFormat = IndexFormat.UInt32;

        // Use NativeArray slices for SetVertices/SetNormals (zero managed alloc)
        var mPos = new NativeArray<Vector3>(vertCount, Allocator.Temp,
                                             NativeArrayOptions.UninitializedMemory);
        var mNrm = new NativeArray<Vector3>(vertCount, Allocator.Temp,
                                             NativeArrayOptions.UninitializedMemory);
        var mCol = new NativeArray<Color>(vertCount, Allocator.Temp,
                                           NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < vertCount; i++)
        {
            mPos[i] = (Vector3)positions[i];
            mNrm[i] = (Vector3)normals[i];
            mCol[i] = colors[i];
        }

        mesh.SetVertices(mPos);
        mesh.SetNormals(mNrm);
        mesh.SetColors(mCol);

        mPos.Dispose();
        mNrm.Dispose();
        mCol.Dispose();

        // SetIndices from NativeArray slice
        var mIdx = new NativeArray<int>(triIndexCount, Allocator.Temp,
                                         NativeArrayOptions.UninitializedMemory);
        NativeArray<int>.Copy(triangleIndices, mIdx, triIndexCount);
        mesh.SetIndices(mIdx, MeshTopology.Triangles, 0);
        mIdx.Dispose();

        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        if (mf.sharedMesh != null)
        {
            mf.sharedMesh.Clear();
            SafeDestroy(mf.sharedMesh);
        }
        mf.sharedMesh = mesh;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows    = true;

        var mc = go.GetComponent<MeshCollider>();
        if (mc == null) mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    /// <summary>
    /// Synchronous fallback for legacy GenerateVolume() — runs all jobs with .Complete().
    /// All MapMagicBridge calls happen on main thread before jobs are scheduled.
    /// </summary>
    void GenerateVolumeSyncFallback(GameObject targetGO, Vector3 worldCenter,
                                    VoxelPOIType poiType, Vector3 sdfSizeOverride)
    {
        if (mapMagicBridge == null)
        {
            Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
            return;
        }

        MCTables.Initialize();

        VoxelPOIDefinition def = FindDefinition(poiType);
        if (def == null) def = new VoxelPOIDefinition { type = poiType };

        Vector3 sdfSize = sdfSizeOverride != Vector3.zero ? sdfSizeOverride : def.sdfSize;
        float padding = def.volumePadding;

        Vector3 volumeExtents = sdfSize + Vector3.one * (padding + sealMargin) * 2f;
        int gridX = Mathf.Min(Mathf.CeilToInt(volumeExtents.x / voxelResolution), maxGridDimension);
        int gridY = Mathf.Min(Mathf.CeilToInt(volumeExtents.y / voxelResolution), maxGridDimension);
        int gridZ = Mathf.Min(Mathf.CeilToInt(volumeExtents.z / voxelResolution), maxGridDimension);

        int ptsX = gridX + 1, ptsY = gridY + 1, ptsZ = gridZ + 1;
        int totalPts   = ptsX * ptsY * ptsZ;
        int totalCells = gridX * gridY * gridZ;
        int maxVerts   = totalCells * 15;

        float3 actualSize    = new float3(gridX, gridY, gridZ) * voxelResolution;
        float3 volumeOrigin  = (float3)worldCenter - actualSize * 0.5f;

        NoiseData wn;
        float wnAmp;
        if (def.overrideNoise)
        { wn = NoiseData.From(def.wallNoise); wnAmp = def.wallNoiseAmplitude; }
        else
        { wn = NoiseData.From(wallNoise); wnAmp = wallNoiseAmplitude; }

        float3 sdfSizeForJob = poiType == VoxelPOIType.Cave
            ? new float3(sdfSize.x, 0, 0) : (float3)sdfSize;

        // ── Fallback height ──
        float fallbackHeight = worldCenter.y - 10f;

        var terrainHeights  = new NativeArray<float>(ptsX * ptsZ, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var gridBiome       = new NativeArray<float>(ptsX * ptsZ, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var densityField    = new NativeArray<float>(totalPts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var rawVerts        = new NativeArray<MCRawVertex>(maxVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var counter         = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var weldedPositions = new NativeArray<float3>(maxVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var triIndices      = new NativeArray<int>(maxVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var weldedCounter   = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var edgeToVertex    = new NativeParallelHashMap<long, int>(maxVerts / 2, Allocator.Persistent);

        try
        {
            // ── PHASE 0: Main-thread terrain sampling (before any jobs) ──
            for (int iz = 0; iz < ptsZ; iz++)
            for (int ix = 0; ix < ptsX; ix++)
            {
                float wx = volumeOrigin.x + ix * voxelResolution;
                float wz = volumeOrigin.z + iz * voxelResolution;
                int hi = ix + iz * ptsX;

                terrainHeights[hi] = SampleTerrainHeight(wx, wz, fallbackHeight);
                gridBiome[hi]      = 0f; // Biome stub
            }

            // Density → MC → Weld (chained)
            var densityHandle = new VoxelDensityJob
            {
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin, voxelStep = voxelResolution,
                poiType = poiType, sdfCenter = worldCenter, sdfSize = sdfSizeForJob,
                wallNoise = wn, wallNoiseAmp = wnAmp, sealMargin = sealMargin,
                terrainHeights = terrainHeights, density = densityField
            }.Schedule(totalPts, JOB_BATCH);

            var mcHandle = new VoxelMCExtractJob
            {
                cellsX = gridX, cellsY = gridY, cellsZ = gridZ,
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin, voxelStep = voxelResolution,
                density = densityField, edgeTable = MCTables.EdgeTable, triTable = MCTables.TriTable,
                outVertices = rawVerts, vertexCounter = counter
            }.Schedule(totalCells, JOB_BATCH, densityHandle);

            mcHandle.Complete();

            int rawCount = counter[0];
            if (rawCount < 3) { SafeDestroy(targetGO); return; }

            var weldHandle = new VoxelWeldJob
            {
                rawCount = rawCount, rawVertices = rawVerts,
                edgeToVertex = edgeToVertex, weldedPositions = weldedPositions,
                triangleIndices = triIndices, weldedCounter = weldedCounter
            }.Schedule();

            weldHandle.Complete();

            int weldedCount = weldedCounter[0];
            if (weldedCount < 3) { SafeDestroy(targetGO); return; }

            // Normals + Biome + Colors
            var normals   = new NativeArray<float3>(weldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var biomeVals = new NativeArray<float>(weldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var colors    = new NativeArray<Color>(weldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            try
            {
                var normalHandle = new VoxelNormalJob
                {
                    ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin, voxelStep = voxelResolution,
                    density = densityField, positions = weldedPositions, normals = normals
                }.Schedule(weldedCount, JOB_BATCH);

                var biomeHandle = new VoxelBiomeSampleJob
                {
                    ptsX = ptsX, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin, voxelStep = voxelResolution,
                    gridBiome = gridBiome, positions = weldedPositions, biomeValues = biomeVals
                }.Schedule(weldedCount, JOB_BATCH);

                var colorDeps = JobHandle.CombineDependencies(normalHandle, biomeHandle);
                var colorHandle = new VoxelColorJob
                {
                    maxDepth = ABYSSAL_MAX_DEPTH,
                    sdfCenter = worldCenter, sdfSize = sdfSizeForJob,
                    poiType = poiType, caveEdgeWidth = caveEdgeColorWidth,
                    positions = weldedPositions, normals = normals,
                    biomeValues = biomeVals, colors = colors
                }.Schedule(weldedCount, JOB_BATCH, colorDeps);

                colorHandle.Complete();

                BuildWeldedMeshNative(targetGO, weldedPositions, normals, colors,
                                     triIndices, rawCount, weldedCount, voxelMaterial);
                _activeVolumes.Add(targetGO);

                Debug.Log($"[HectonVoxel] Volume '{targetGO.name}': " +
                          $"{rawCount} raw → {weldedCount} welded verts " +
                          $"({(1f - (float)weldedCount / rawCount) * 100f:F0}% reduction), " +
                          $"{rawCount / 3} tris");
            }
            finally
            {
                if (normals.IsCreated)   normals.Dispose();
                if (biomeVals.IsCreated) biomeVals.Dispose();
                if (colors.IsCreated)    colors.Dispose();
            }
        }
        finally
        {
            if (terrainHeights.IsCreated)  terrainHeights.Dispose();
            if (gridBiome.IsCreated)       gridBiome.Dispose();
            if (densityField.IsCreated)    densityField.Dispose();
            if (rawVerts.IsCreated)        rawVerts.Dispose();
            if (counter.IsCreated)         counter.Dispose();
            if (weldedPositions.IsCreated) weldedPositions.Dispose();
            if (triIndices.IsCreated)      triIndices.Dispose();
            if (weldedCounter.IsCreated)   weldedCounter.Dispose();
            if (edgeToVertex.IsCreated)    edgeToVertex.Dispose();
        }
    }

    void SafeDestroy(Object obj)
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            if (_activeVolumes[i] == null) continue;
            var mf = _activeVolumes[i].GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                Gizmos.DrawWireCube(mf.sharedMesh.bounds.center, mf.sharedMesh.bounds.size);
        }
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  CUSTOM EDITOR
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
                $"═══ VOXEL ENGINE ═══\n" +
                $"Active Volumes: {engine.ActiveVolumeCount}\n" +
                $"MC Tables: {(MCTables.IsReady ? "Ready" : "Not Init")}\n" +
                $"Height Source: MapMagicBridge\n" +
                $"Abyssal Depth: 5000m\n" +
                $"Async: Unity 6 Awaitable (Zero GC)",
                MessageType.Info);
        }

        float maxDim    = engine.maxGridDimension;
        float maxPts    = (maxDim + 1) * (maxDim + 1) * (maxDim + 1);
        float maxCells  = maxDim * maxDim * maxDim;
        float densityMB = maxPts * 4f / (1024f * 1024f);
        float rawMB     = maxCells * 15f * 20f / (1024f * 1024f);
        float weldMapMB = maxCells * 15f * 12f / (1024f * 1024f);
        float totalMB   = densityMB + rawMB + weldMapMB;

        EditorGUILayout.HelpBox(
            $"═══ WORST-CASE PER VOLUME ═══\n" +
            $"Grid: {engine.maxGridDimension}³\n" +
            $"Density: {densityMB:F1} MB | Raw MC: {rawMB:F1} MB\n" +
            $"Weld Map: {weldMapMB:F1} MB\n" +
            $"Peak temp: {totalMB:F1} MB (freed after gen)\n" +
            $"Welding reduces final mesh ~60-70% (Zero-GC)",
            totalMB > 100f ? MessageType.Warning : MessageType.None);

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
        if (GUILayout.Button("✕  Clear All Volumes", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(engine.gameObject, "Clear Voxels");
            engine.ClearAllVolumes();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            "HectonVoxelEngine v3.2\n" +
            "Height Source: MapMagicBridge (TryGetHeight)\n" +
            "Async: Unity 6 Awaitable (Zero GC) | Burst + Jobs\n" +
            "NativeParallelHashMap | Zero-GC Welding | Pool Integration\n" +
            "Thread-safe MCTables | Full dependency chain | SDF + 3D Noise",
            MessageType.None);
    }
}

#endif