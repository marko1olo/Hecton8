// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelEngine.cs — Project HECTON-8 Localized Voxel Volumes           ║
// ║  Unity 6 (URP) | Burst + Jobs | Marching Cubes | Multi-Primitive SDF      ║
// ║  v4.0 — Complete cave SDF rewrite                                          ║
// ║                                                                             ║
// ║  CHANGES v4.0:                                                              ║
// ║  ─────────────                                                              ║
// ║  1. VoxelDensityJob completely rewritten:                                  ║
// ║     — Multi-primitive SDF: rooms (sphere/ellipsoid/shaft/hall/crevice)     ║
// ║     — Tunnel capsules with conic taper and elliptic cross-section          ║
// ║     — Entrance funnels connecting to terrain surface                       ║
// ║     — CaveStructure support (columns/bridges/boulders — future use)       ║
// ║     — Polynomial smooth-min blending for organic shapes                   ║
// ║     — Domain warping via fractal noise for curved tunnels                 ║
// ║     — Wall noise (FBM) for rocky surface detail                           ║
// ║     — Horizontal terracing for rock strata layers                         ║
// ║     — Floor flattening per-room                                            ║
// ║     — Near-surface-only noise evaluation (performance optimization)        ║
// ║                                                                             ║
// ║  2. GenerateVolumeAsync new signature:                                     ║
// ║     — Accepts seed + CavePreset (or raw NativeArrays)                     ║
// ║     — Calls CaveGraphGenerator internally                                 ║
// ║     — gridDimension and voxelSize per-call (not global)                   ║
// ║     — Raw MC buffer sized at totalCells*2 (not *15) — safe truncation     ║
// ║                                                                             ║
// ║  3. VoxelPOIType, VoxelPOIData, VoxelPOIDefinition — REMOVED              ║
// ║     Replaced by CavePreset / CaveGenerationParams from CaveTypes.cs       ║
// ║                                                                             ║
// ║  4. maxGridDimension raised to 128                                         ║
// ║                                                                             ║
// ║  PRESERVED from v3.2:                                                       ║
// ║  ─────────────────────                                                      ║
// ║  • Awaitable API (Unity 6 native async, zero GC)                           ║
// ║  • MCTables (edge/tri lookup, thread-safe init/shutdown)                   ║
// ║  • VoxelMCExtractJob (marching cubes extraction, unchanged)                ║
// ║  • VoxelWeldJob (edge-based vertex welding, unchanged)                     ║
// ║  • VoxelNormalJob (density gradient normals, unchanged)                    ║
// ║  • VoxelBiomeSampleJob (biome grid sampling, unchanged)                    ║
// ║  • BuildWeldedMeshNative (mesh assembly, unchanged)                        ║
// ║  • Pool integration (ObjectPoolManager)                                    ║
// ║  • MapMagicBridge height sampling on main thread                           ║
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
using Hecton8.Caves;
using Hecton8.Dev;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: MARCHING CUBES LOOKUP TABLES (unchanged from v3.2)
// ════════════════════════════════════════════════════════════════════════════════
#region Marching Cubes Tables

public static class MCTables
{
    public static NativeArray<int> EdgeTable => _edgeTable;
    public static NativeArray<int> TriTable  => _triTable;
    public static bool IsReady => Volatile.Read(ref _ready) == 1;

    static NativeArray<int> _edgeTable;
    static NativeArray<int> _triTable;
    static int _ready;
    static readonly object _initLock = new object();
    static bool _editorHooksInstalled;

#if UNITY_EDITOR
    static void EnsureEditorHooks()
    {
        if (_editorHooksInstalled)
            return;

        AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        EditorApplication.quitting -= Shutdown;
        EditorApplication.quitting += Shutdown;
        _editorHooksInstalled = true;
    }

    static void ReleaseEditorHooks()
    {
        if (!_editorHooksInstalled)
            return;

        AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        EditorApplication.quitting -= Shutdown;
        _editorHooksInstalled = false;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReloadCleanup() { Shutdown(); }

    public static void Initialize()
    {
        if (Volatile.Read(ref _ready) == 1) return;
        lock (_initLock)
        {
            if (Volatile.Read(ref _ready) == 1) return;

#if UNITY_EDITOR
            EnsureEditorHooks();
#endif

            var et = new int[256]
            {
                0x000,0x109,0x203,0x30A,0x406,0x50F,0x605,0x70C,
                0x80C,0x905,0xA0F,0xB06,0xC0A,0xD03,0xE09,0xF00,
                0x190,0x099,0x393,0x29A,0x596,0x49F,0x795,0x69C,
                0x99C,0x895,0xB9F,0xA96,0xD9A,0xC93,0xF99,0xE90,
                0x230,0x339,0x033,0x13A,0x636,0x73F,0x435,0x53C,
                0xA3C,0xB35,0x83F,0x936,0xE3A,0xF33,0xC39,0xD30,
                0x3A0,0x2A9,0x1A3,0x0AA,0x7A6,0x6AF,0x5A5,0x4AC,
                0xBAC,0xAA5,0x9AF,0x8A6,0xFAA,0xEA3,0xDA9,0xCA0,
                0x460,0x569,0x663,0x76A,0x066,0x16F,0x265,0x36C,
                0xC6C,0xD65,0xE6F,0xF66,0x86A,0x963,0xA69,0xB60,
                0x5F0,0x4F9,0x7F3,0x6FA,0x1F6,0x0FF,0x3F5,0x2FC,
                0xDFC,0xCF5,0xFFF,0xEF6,0x9FA,0x8F3,0xBF9,0xAF0,
                0x650,0x759,0x453,0x55A,0x256,0x35F,0x055,0x15C,
                0xE5C,0xF55,0xC5F,0xD56,0xA5A,0xB53,0x859,0x950,
                0x7C0,0x6C9,0x5C3,0x4CA,0x3C6,0x2CF,0x1C5,0x0CC,
                0xFCC,0xEC5,0xDCF,0xCC6,0xBCA,0xAC3,0x9C9,0x8C0,
                0x8C0,0x9C9,0xAC3,0xBCA,0xCC6,0xDCF,0xEC5,0xFCC,
                0x0CC,0x1C5,0x2CF,0x3C6,0x4CA,0x5C3,0x6C9,0x7C0,
                0x950,0x859,0xB53,0xA5A,0xD56,0xC5F,0xF55,0xE5C,
                0x15C,0x055,0x35F,0x256,0x55A,0x453,0x759,0x650,
                0xAF0,0xBF9,0x8F3,0x9FA,0xEF6,0xFFF,0xCF5,0xDFC,
                0x2FC,0x3F5,0x0FF,0x1F6,0x6FA,0x7F3,0x4F9,0x5F0,
                0xB60,0xA69,0x963,0x86A,0xF66,0xE6F,0xD65,0xC6C,
                0x36C,0x265,0x16F,0x066,0x76A,0x663,0x569,0x460,
                0xCA0,0xDA9,0xEA3,0xFAA,0x8A6,0x9AF,0xAA5,0xBAC,
                0x4AC,0x5A5,0x6AF,0x7A6,0x0AA,0x1A3,0x2A9,0x3A0,
                0xD30,0xC39,0xF33,0xE3A,0x936,0x83F,0xB35,0xA3C,
                0x53C,0x435,0x73F,0x636,0x13A,0x033,0x339,0x230,
                0xE90,0xF99,0xC93,0xD9A,0xA96,0xB9F,0x895,0x99C,
                0x69C,0x795,0x49F,0x596,0x29A,0x393,0x099,0x190,
                0xF00,0xE09,0xD03,0xC0A,0xB06,0xA0F,0x905,0x80C,
                0x70C,0x605,0x50F,0x406,0x30A,0x203,0x109,0x000
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
#if UNITY_EDITOR
            ReleaseEditorHooks();
#endif
            if (_edgeTable.IsCreated) _edgeTable.Dispose();
            if (_triTable.IsCreated)  _triTable.Dispose();
            Volatile.Write(ref _ready, 0);
        }
    }
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: MC RAW VERTEX (unchanged)
// ════════════════════════════════════════════════════════════════════════════════
#region MC Types

public struct MCRawVertex
{
    public float3 position;
    public long edgeId;
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: BURST JOBS
// ════════════════════════════════════════════════════════════════════════════════
#region Voxel Burst Jobs

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 1: DENSITY FIELD — Multi-primitive SDF cave system (v4.0 REWRITE)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelDensityJob : IJobParallelFor
{
    // ── Grid dimensions ──
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    // ── Terrain ──
    [ReadOnly] public NativeArray<float> terrainHeights;

    // ── Cave SDF primitives ──
    [ReadOnly] public NativeArray<CaveNode> caveNodes;
    [ReadOnly] public NativeArray<CaveTunnel> caveTunnels;
    [ReadOnly] public NativeArray<CaveEntrance> caveEntrances;
    [ReadOnly] public NativeArray<CaveStructure> caveStructures;

    // ── Cave parameters ──
    public CaveGenerationParams caveParams;

    // ── Edge sealing ──
    public float sealMargin;

    // ── Output ──
    [WriteOnly] public NativeArray<float> density;

    // ════════════════════════════════════════════════════════════════════════
    //  EXECUTE — Per voxel point
    // ════════════════════════════════════════════════════════════════════════

    public void Execute(int idx)
    {
        // ── Unpack 3D index ──
        int ix = idx % ptsX;
        int iy = (idx / ptsX) % ptsY;
        int iz = idx / (ptsX * ptsY);

        float3 wp = volumeOrigin + new float3(ix, iy, iz) * voxelStep;

        // ════════════════════════════════════════════════════════════════
        //  STEP 1: Terrain density
        // ════════════════════════════════════════════════════════════════

        bool structureOnlyMode = caveParams.structureOnlyMode != 0;
        float terrainH = terrainHeights[ix + iz * ptsX];
        float terrainDensity = structureOnlyMode
            ? -1f
            : math.clamp(terrainH - wp.y, -50f, 50f);

        // ════════════════════════════════════════════════════════════════
        //  STEP 2: Cave SDF
        // ════════════════════════════════════════════════════════════════

        float caveSDF = structureOnlyMode ? 1f : EvaluateCaveSDF(wp);

        // ════════════════════════════════════════════════════════════════
        //  STEP 3: Subtract cave from terrain
        // ════════════════════════════════════════════════════════════════

        float d_final;

        if (!structureOnlyMode && caveSDF < caveParams.shellThickness)
        {
            d_final = SmoothSubtraction(-caveSDF, terrainDensity, caveParams.shellThickness);
        }
        else
        {
            d_final = terrainDensity;
        }

        // ════════════════════════════════════════════════════════════════
        //  STEP 4: Add structures back
        // ════════════════════════════════════════════════════════════════

        if (caveStructures.Length > 0 && (structureOnlyMode || caveSDF < 0f))
        {
            float structSDF = EvaluateStructuresSDF(wp);
            if (structSDF < caveParams.structureBlendK)
            {
                d_final = SmoothMax(d_final, -structSDF, caveParams.structureBlendK);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  STEP 5: Edge sealing — WITH ENTRANCE EXEMPTION
        //
        //  The seal pushes density to 1 (solid) near volume borders to
        //  prevent the cave mesh from showing open holes into the void.
        //
        //  BUT: cave entrances intentionally punch through the TOP face
        //  of the volume to connect with the terrain surface above.
        //  If we seal the top face blindly, we brick the entrance.
        //
        //  Fix: For each face, check if the current point is near any
        //  CaveEntrance. If yes, reduce seal influence on that face.
        //  This creates a "hole in the seal" where the entrance is,
        //  while keeping the rest of the border airtight.
        // ════════════════════════════════════════════════════════════════

        if (!structureOnlyMode)
        {
        float3 localPos = wp - volumeOrigin;
        float3 volumeSize = new float3(ptsX - 1, ptsY - 1, ptsZ - 1) * voxelStep;

        // Distance to each face
        float dMinX = math.min(localPos.x, volumeSize.x - localPos.x);
        float dMinZ = math.min(localPos.z, volumeSize.z - localPos.z);
        float dMinYBottom = localPos.y;                    // distance to bottom face
        float dMinYTop    = volumeSize.y - localPos.y;     // distance to top face

        // ── Entrance exemption for top face ──
        // Check if this point is within any entrance's horizontal footprint.
        // If yes, disable sealing on the top face so entrance can punch through.
        float topSealStrength = 1f; // 1 = full seal, 0 = no seal

        for (int e = 0; e < caveEntrances.Length; e++)
        {
            CaveEntrance entrance = caveEntrances[e];

            // Horizontal distance from entrance axis
            float2 horizontalDelta = wp.xz - entrance.surfacePosition.xz;
            float horizontalDist = math.length(horizontalDelta);

            // Entrance influence radius: entrance radius + blend margin
            float influenceRadius = entrance.radius * 2.5f;

            if (horizontalDist < influenceRadius)
            {
                // Smooth falloff: full exemption at center, fading to full seal at edge
                float exemption = 1f - math.smoothstep(
                    entrance.radius * 0.5f,  // full exemption within half radius
                    influenceRadius,          // no exemption beyond influence radius
                    horizontalDist);

                topSealStrength = math.min(topSealStrength, 1f - exemption);
            }
        }

        // Also check bottom face for entrances that might come from below
        // (rare but possible with vertical shaft entrances)
        float bottomSealStrength = 1f;
        for (int e = 0; e < caveEntrances.Length; e++)
        {
            CaveEntrance entrance = caveEntrances[e];

            // Only if entrance direction points upward (entrance from below)
            if (entrance.inwardDirection.y > 0.3f)
            {
                float2 horizontalDelta = wp.xz - entrance.surfacePosition.xz;
                float horizontalDist = math.length(horizontalDelta);
                float influenceRadius = entrance.radius * 2.5f;

                if (horizontalDist < influenceRadius)
                {
                    float exemption = 1f - math.smoothstep(
                        entrance.radius * 0.5f,
                        influenceRadius,
                        horizontalDist);

                    bottomSealStrength = math.min(bottomSealStrength, 1f - exemption);
                }
            }
        }

        // Compute effective distance to nearest sealed edge
        // Top and bottom faces use modulated seal strength
        float effectiveYTop    = dMinYTop / math.max(topSealStrength, 0.01f);
        float effectiveYBottom = dMinYBottom / math.max(bottomSealStrength, 0.01f);
        float dMinY = math.min(effectiveYBottom, effectiveYTop);

        float dEdge = math.min(dMinX, math.min(dMinY, dMinZ));
        float sealFactor = math.saturate(dEdge / math.max(sealMargin, 0.01f));
        d_final = math.lerp(1f, d_final, sealFactor);
        }

        density[idx] = d_final;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CAVE SDF EVALUATION — Core of the cave generation system
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateCaveSDF(float3 wp)
    {
        // ── Domain warping — distort coordinates for organic shapes ──
        float3 warpedPos = wp;
        if (caveParams.warpAmplitude > 0.001f)
        {
            warpedPos = ApplyDomainWarp(wp,
                caveParams.warpFrequency,
                caveParams.warpAmplitude,
                caveParams.warpOctaves,
                caveParams.seed);
        }

        // Start with a very large distance (outside all caves)
        float caveDist = 99999f;

        // ── Evaluate all rooms ──
        for (int i = 0; i < caveNodes.Length; i++)
        {
            float nodeDist = EvaluateRoom(warpedPos, wp, caveNodes[i]);
            caveDist = SmoothMin(caveDist, nodeDist, caveNodes[i].blendRadius);
        }

        // ── Evaluate all tunnels ──
        for (int i = 0; i < caveTunnels.Length; i++)
        {
            float tunnelDist = EvaluateTunnel(warpedPos, wp, caveTunnels[i]);
            caveDist = SmoothMin(caveDist, tunnelDist, caveTunnels[i].blendRadius);
        }

        // ── Evaluate all entrances ──
        for (int i = 0; i < caveEntrances.Length; i++)
        {
            float entranceDist = EvaluateEntrance(warpedPos, caveEntrances[i]);
            caveDist = SmoothMin(caveDist, entranceDist, caveParams.entranceBlendK);
        }

        // ── Wall detail (only near cave surface for performance) ──
        if (math.abs(caveDist) < caveParams.noiseEvalDistance)
        {
            caveDist += EvaluateWallDetail(wp, caveDist);
        }

        return caveDist;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ROOM SDF — Sphere, Ellipsoid, Shaft, Hall, Crevice
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateRoom(float3 warpedPos, float3 originalPos, CaveNode node)
    {
        float dist;

        switch (node.roomType)
        {
            case CaveRoomType.Sphere:
                dist = SDSphere(warpedPos, node.position, node.radii.x);
                break;

            case CaveRoomType.Ellipsoid:
                dist = SDEllipsoid(warpedPos, node.position, node.radii);
                break;

            case CaveRoomType.VerticalShaft:
                dist = SDVerticalShaft(warpedPos, node.position,
                    node.radii.x, node.radii.y, node.radii.z);
                break;

            case CaveRoomType.FlatHall:
                // Flat hall = ellipsoid with compressed Y
                float3 hallRadii = new float3(
                    node.radii.x * 1.5f,
                    node.radii.y * 0.35f,
                    node.radii.z * 1.5f);
                dist = SDEllipsoid(warpedPos, node.position, hallRadii);
                break;

            case CaveRoomType.Crevice:
                // Crevice = ellipsoid with compressed XZ, stretched Y
                float3 creviceRadii = new float3(
                    node.radii.x * 0.25f,
                    node.radii.y * 1.3f,
                    node.radii.z);
                dist = SDEllipsoid(warpedPos, node.position, creviceRadii);
                break;

            default:
                dist = SDSphere(warpedPos, node.position, node.radii.x);
                break;
        }

        // Per-room noise variation
        if (node.noiseAmplitude > 0.001f)
        {
            float localNoise = Fractal3DFast(
                originalPos * node.noiseScale * caveParams.wallNoiseFrequency,
                2, caveParams.seed + 7777u);
            dist += localNoise * node.noiseAmplitude;
        }

        // Floor flattening
        if (caveParams.floorFlatness > 0.001f && dist < 0f)
        {
            dist = ApplyFloorFlattening(dist, warpedPos, node.position,
                node.radii.y, caveParams.floorFlatness);
        }

        return dist;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TUNNEL SDF — Conic capsule with optional cross-section scaling
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateTunnel(float3 warpedPos, float3 originalPos, CaveTunnel tunnel)
    {
        // Additional per-tunnel warp
        float3 evalPos = warpedPos;
        if (tunnel.warpAmount > 0.001f)
        {
            evalPos = ApplyDomainWarp(warpedPos,
                caveParams.warpFrequency * 1.7f,
                tunnel.warpAmount,
                math.min(caveParams.warpOctaves, 2),
                caveParams.seed + 54321u);
        }

        float dist;

        if (tunnel.tunnelType == CaveTunnelType.Round)
        {
            // Simple conic capsule
            dist = SDCapsuleConic(evalPos, tunnel.pointA, tunnel.pointB,
                tunnel.radiusA, tunnel.radiusB);
        }
        else
        {
            // Elliptic cross-section capsule
            dist = SDCapsuleElliptic(evalPos, tunnel.pointA, tunnel.pointB,
                math.lerp(tunnel.radiusA, tunnel.radiusB, 0.5f),
                tunnel.heightScale, tunnel.widthScale);
        }

        return dist;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ENTRANCE SDF — Conic capsule from surface inward
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateEntrance(float3 warpedPos, CaveEntrance entrance)
    {
        float3 innerPoint = entrance.surfacePosition +
                            entrance.inwardDirection * entrance.funnelLength;

        return SDCapsuleConic(warpedPos,
            entrance.surfacePosition, innerPoint,
            entrance.radius, entrance.innerRadius);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  STRUCTURE SDF — Internal solid geometry (future use)
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateStructuresSDF(float3 wp)
    {
        float structDist = 99999f;

        for (int i = 0; i < caveStructures.Length; i++)
        {
            CaveStructure s = caveStructures[i];
            float sd;

            switch (s.structureType)
            {
                case CaveStructureType.Column:
                    sd = SDVerticalShaft(wp, s.position, s.size.x, s.size.y, s.size.x * 0.1f);
                    break;

                case CaveStructureType.Bridge:
                    sd = SDCapsuleConic(wp, s.position, s.pointB, s.size.x, s.size.x);
                    break;

                case CaveStructureType.Boulder:
                    sd = SDSphere(wp, s.position, s.size.x);
                    break;

                case CaveStructureType.Stalagmite:
                    // Approximate as cone: capsule from base to tip with tapering radius
                    float3 tip = s.position + new float3(0, s.size.y, 0);
                    sd = SDCapsuleConic(wp, s.position, tip, s.size.x, s.size.z);
                    break;

                case CaveStructureType.Stalactite:
                    // Inverted cone hanging from ceiling
                    float3 hangTip = s.position - new float3(0, s.size.y, 0);
                    sd = SDCapsuleConic(wp, s.position, hangTip, s.size.x, s.size.z);
                    break;

                case CaveStructureType.Block:
                    sd = SDBox(wp, s.position, s.size);
                    break;

                case CaveStructureType.Wall:
                    sd = SDBox(wp, s.position, s.size);
                    break;

                case CaveStructureType.Arch:
                    // Approximate arch as thick capsule
                    sd = SDCapsuleConic(wp, s.position, s.pointB, s.size.x, s.size.x);
                    break;

                default:
                    sd = SDSphere(wp, s.position, s.size.x);
                    break;
            }

            // Per-structure noise
            if (s.noiseAmount > 0.001f)
            {
                sd += Fractal3DFast(wp * 0.3f, 2, caveParams.seed + 9999u) * s.noiseAmount;
            }

            structDist = SmoothMin(structDist, sd, s.blendRadius);
        }

        return structDist;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WALL DETAIL — Noise + terraces applied near cave surface
    // ════════════════════════════════════════════════════════════════════════

    float EvaluateWallDetail(float3 wp, float currentSDF)
    {
        float detail = 0f;

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

        return detail;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SDF PRIMITIVES — Inlined for Burst performance
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Signed distance to sphere.</summary>
    static float SDSphere(float3 p, float3 center, float radius)
    {
        return math.length(p - center) - radius;
    }

    /// <summary>Signed distance to axis-aligned ellipsoid (fast approximation).</summary>
    static float SDEllipsoid(float3 p, float3 center, float3 radii)
    {
        // Scale space so ellipsoid becomes unit sphere
        float3 scaled = (p - center) / math.max(radii, 0.001f);
        float lenScaled = math.length(scaled);

        if (lenScaled < 0.0001f)
            return -math.cmin(radii); // Deep inside

        // Approximate: distance in scaled space × minimum radius
        // This is not exact but good enough for MC and much cheaper than analytic
        return (lenScaled - 1f) * math.cmin(radii);
    }

    /// <summary>Signed distance to rounded vertical cylinder (shaft/chimney).</summary>
    static float SDVerticalShaft(float3 p, float3 center, float radius,
                                  float halfHeight, float roundness)
    {
        float3 q = p - center;
        float2 d = new float2(
            math.length(q.xz) - radius,
            math.abs(q.y) - halfHeight);

        return math.min(math.max(d.x, d.y), 0f)
             + math.length(math.max(d, 0f))
             - math.max(roundness, 0.01f);
    }

    /// <summary>Signed distance to axis-aligned box.</summary>
    static float SDBox(float3 p, float3 center, float3 halfExtents)
    {
        float3 q = math.abs(p - center) - halfExtents;
        return math.length(math.max(q, 0f)) + math.min(math.cmax(q), 0f);
    }

    /// <summary>Signed distance to conic capsule (different radii at each end).</summary>
    static float SDCapsuleConic(float3 p, float3 a, float3 b,
                                 float radiusA, float radiusB)
    {
        float3 pa = p - a;
        float3 ba = b - a;
        float baba = math.dot(ba, ba);

        if (baba < 0.0001f)
            return math.length(pa) - radiusA; // Degenerate: a ≈ b → sphere

        float h = math.saturate(math.dot(pa, ba) / baba);
        float radius = math.lerp(radiusA, radiusB, h);
        return math.length(pa - ba * h) - radius;
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
            return math.length(pa) - radius;

        float h = math.saturate(math.dot(pa, ba) / baba);
        float3 closest = pa - ba * h;

        // Build local coordinate frame perpendicular to tunnel direction
        float3 forward = ba * math.rsqrt(baba); // Normalized direction
        float3 up = new float3(0, 1, 0);

        // Handle near-vertical tunnels
        if (math.abs(math.dot(forward, up)) > 0.99f)
            up = new float3(1, 0, 0);

        float3 right = math.normalizesafe(math.cross(forward, up));
        up = math.cross(right, forward);

        // Project onto local axes and scale
        float projRight = math.dot(closest, right);
        float projUp = math.dot(closest, up);

        // Elliptic scaling
        float safeWidth = math.max(widthScale, 0.01f);
        float safeHeight = math.max(heightScale, 0.01f);
        float2 scaled = new float2(projRight / safeWidth, projUp / safeHeight);

        return math.length(scaled) - radius;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CSG OPERATIONS — Smooth blending
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Polynomial smooth minimum (cubic). Merges shapes organically.</summary>
    static float SmoothMin(float a, float b, float k)
    {
        k = math.max(k, 0.0001f);
        float h = math.max(k - math.abs(a - b), 0f) / k;
        return math.min(a, b) - h * h * h * k * (1f / 6f);
    }

    /// <summary>Smooth maximum. Inverse of smooth min.</summary>
    static float SmoothMax(float a, float b, float k)
    {
        return -SmoothMin(-a, -b, k);
    }

    /// <summary>Smooth subtraction: carve shape B out of shape A.</summary>
    static float SmoothSubtraction(float distCarve, float distBase, float k)
    {
        return SmoothMax(distBase, -distCarve, k);
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
        // Smoothstep-based terrace with adjustable sharpness
        float terrace = math.pow(
            math.abs(math.sin(fractional * math.PI)),
            math.max(sharpness, 0.1f));
        return terrace * amplitude;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FLOOR FLATTENING — Makes room bottoms more walkable
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Flatten the bottom portion of a room.
    /// Only affects the lower 30% of the room height.
    /// Outside the room (dist > 0), has no effect.</summary>
    static float ApplyFloorFlattening(float sdfDist, float3 p, float3 roomCenter,
                                       float roomRadiusY, float flatness)
    {
        float floorY = roomCenter.y - roomRadiusY;
        float heightAboveFloor = p.y - floorY;

        // Only affect low region inside the room
        float floorZone = roomRadiusY * 0.3f;
        if (heightAboveFloor > 0f && heightAboveFloor < floorZone && sdfDist < 0f)
        {
            // Blend between curved SDF and a flat plane
            float blend = math.smoothstep(0f, floorZone, heightAboveFloor);
            float flatPlane = heightAboveFloor - floorZone * 0.3f;
            return math.lerp(flatPlane, sdfDist, math.lerp(1f - flatness, 1f, blend));
        }

        return sdfDist;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 2: Marching Cubes extraction (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
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

        float d0 = D(cx, cy, cz);
        float d1 = D(cx+1, cy, cz);
        float d2 = D(cx+1, cy+1, cz);
        float d3 = D(cx, cy+1, cz);
        float d4 = D(cx, cy, cz+1);
        float d5 = D(cx+1, cy, cz+1);
        float d6 = D(cx+1, cy+1, cz+1);
        float d7 = D(cx, cy+1, cz+1);

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

        float3 p0 = P(cx, cy, cz);
        float3 p1 = P(cx+1, cy, cz);
        float3 p2 = P(cx+1, cy+1, cz);
        float3 p3 = P(cx, cy+1, cz);
        float3 p4 = P(cx, cy, cz+1);
        float3 p5 = P(cx+1, cy, cz+1);
        float3 p6 = P(cx+1, cy+1, cz+1);
        float3 p7 = P(cx, cy+1, cz+1);

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

        if((edgeBits&1)!=0)    {ev0=Lerp(p0,p1,d0,d1); eid0=PackEdge(g0,g1);}
        if((edgeBits&2)!=0)    {ev1=Lerp(p1,p2,d1,d2); eid1=PackEdge(g1,g2);}
        if((edgeBits&4)!=0)    {ev2=Lerp(p2,p3,d2,d3); eid2=PackEdge(g2,g3);}
        if((edgeBits&8)!=0)    {ev3=Lerp(p3,p0,d3,d0); eid3=PackEdge(g3,g0);}
        if((edgeBits&16)!=0)   {ev4=Lerp(p4,p5,d4,d5); eid4=PackEdge(g4,g5);}
        if((edgeBits&32)!=0)   {ev5=Lerp(p5,p6,d5,d6); eid5=PackEdge(g5,g6);}
        if((edgeBits&64)!=0)   {ev6=Lerp(p6,p7,d6,d7); eid6=PackEdge(g6,g7);}
        if((edgeBits&128)!=0)  {ev7=Lerp(p7,p4,d7,d4); eid7=PackEdge(g7,g4);}
        if((edgeBits&256)!=0)  {ev8=Lerp(p0,p4,d0,d4); eid8=PackEdge(g0,g4);}
        if((edgeBits&512)!=0)  {ev9=Lerp(p1,p5,d1,d5); eid9=PackEdge(g1,g5);}
        if((edgeBits&1024)!=0) {ev10=Lerp(p2,p6,d2,d6); eid10=PackEdge(g2,g6);}
        if((edgeBits&2048)!=0) {ev11=Lerp(p3,p7,d3,d7); eid11=PackEdge(g3,g7);}

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

            outVertices[wi] = new MCRawVertex {
                position = GetEV(e0,ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId = GetEID(e0,eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11) };
            outVertices[wi+1] = new MCRawVertex {
                position = GetEV(e1,ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId = GetEID(e1,eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11) };
            outVertices[wi+2] = new MCRawVertex {
                position = GetEV(e2,ev0,ev1,ev2,ev3,ev4,ev5,ev6,ev7,ev8,ev9,ev10,ev11),
                edgeId = GetEID(e2,eid0,eid1,eid2,eid3,eid4,eid5,eid6,eid7,eid8,eid9,eid10,eid11) };
            wi += 3;
        }
    }

    int GI(int ix,int iy,int iz) => ix+iy*ptsX+iz*ptsX*ptsY;
    float D(int ix,int iy,int iz) => density[GI(ix,iy,iz)];
    float3 P(int ix,int iy,int iz) => volumeOrigin+new float3(ix,iy,iz)*voxelStep;

    static float3 Lerp(float3 pA,float3 pB,float dA,float dB)
    {
        float diff=dA-dB;
        if(math.abs(diff)<1e-6f) return (pA+pB)*0.5f;
        float t=math.clamp(dA/diff,0f,1f);
        return pA+t*(pB-pA);
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
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 2.5: Vertex Welding (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public unsafe struct VoxelWeldJob : IJob
{
    public int rawCount;
    [ReadOnly] public NativeArray<MCRawVertex> rawVertices;
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

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 3: Normals from density gradient (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
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
        float dxp=Sample(lp+new float3(eps,0,0));
        float dxm=Sample(lp-new float3(eps,0,0));
        float dyp=Sample(lp+new float3(0,eps,0));
        float dym=Sample(lp-new float3(0,eps,0));
        float dzp=Sample(lp+new float3(0,0,eps));
        float dzm=Sample(lp-new float3(0,0,eps));
        float3 grad=new float3(dxp-dxm,dyp-dym,dzp-dzm);
        normals[idx]=math.normalizesafe(-grad,new float3(0,1,0));
    }

    float Sample(float3 lp)
    {
        lp=math.clamp(lp,float3.zero,new float3(ptsX-1,ptsY-1,ptsZ-1));
        int x0=(int)lp.x;int x1=math.min(x0+1,ptsX-1);
        int y0=(int)lp.y;int y1=math.min(y0+1,ptsY-1);
        int z0=(int)lp.z;int z1=math.min(z0+1,ptsZ-1);
        float fx=lp.x-x0,fy=lp.y-y0,fz=lp.z-z0;
        float c000=density[x0+y0*ptsX+z0*ptsX*ptsY];
        float c100=density[x1+y0*ptsX+z0*ptsX*ptsY];
        float c010=density[x0+y1*ptsX+z0*ptsX*ptsY];
        float c110=density[x1+y1*ptsX+z0*ptsX*ptsY];
        float c001=density[x0+y0*ptsX+z1*ptsX*ptsY];
        float c101=density[x1+y0*ptsX+z1*ptsX*ptsY];
        float c011=density[x0+y1*ptsX+z1*ptsX*ptsY];
        float c111=density[x1+y1*ptsX+z1*ptsX*ptsY];
        float c00=math.lerp(c000,c100,fx);
        float c10=math.lerp(c010,c110,fx);
        float c01=math.lerp(c001,c101,fx);
        float c11=math.lerp(c011,c111,fx);
        return math.lerp(math.lerp(c00,c10,fy),math.lerp(c01,c11,fy),fz);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 3.5: Biome Sampling (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
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
        float3 wp=positions[idx];
        float lx=(wp.x-volumeOrigin.x)/voxelStep;
        float lz=(wp.z-volumeOrigin.z)/voxelStep;
        lx=math.clamp(lx,0f,ptsX-1f);
        lz=math.clamp(lz,0f,ptsZ-1f);
        int x0=(int)lx,z0=(int)lz;
        int x1=math.min(x0+1,ptsX-1);
        int z1=math.min(z0+1,ptsZ-1);
        float fx=lx-x0,fz=lz-z0;
        float v00=gridBiome[x0+z0*ptsX];
        float v10=gridBiome[x1+z0*ptsX];
        float v01=gridBiome[x0+z1*ptsX];
        float v11=gridBiome[x1+z1*ptsX];
        biomeValues[idx]=math.lerp(math.lerp(v00,v10,fx),math.lerp(v01,v11,fx),fz);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 4: Vertex Colors (v4.0 — updated for cave SDF)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelColorJob : IJobParallelFor
{
    public float maxDepth;
    public float caveEdgeWidth;

    // Simplified: we store volume center and half-extent for depth estimation
    public float3 volumeCenter;
    public float volumeHalfExtent;

    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> normals;
    [ReadOnly] public NativeArray<float> biomeValues;

    [WriteOnly] public NativeArray<Color> colors;

    public void Execute(int idx)
    {
        float3 p = positions[idx];
        float3 n = normals[idx];

        // R = slope (0 = flat floor/ceiling, 1 = vertical wall)
        float slope = 1f - math.abs(math.dot(n, new float3(0, 1, 0)));

        // G = depth below sea level (normalized 0-1)
        float depth = math.saturate(-p.y / math.max(maxDepth, 1f));

        // B = distance from volume center (normalized)
        // Useful for shader effects: darker deeper inside cave
        float distFromCenter = math.length(p - volumeCenter) / math.max(volumeHalfExtent, 1f);
        float interiorFade = math.saturate(distFromCenter);

        // A = biome
        float biome = biomeValues[idx];

        colors[idx] = new Color(slope, depth, interiorFade, biome);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 5: Cave Interior Spawn Points (v4.1 — deterministic hash IDs)
//  Extracts floor positions from welded mesh for loot/flora/fauna spawning.
//  Each point carries a deterministic hashId derived from world position,
//  ensuring save system consistency regardless of parallel execution order.
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct VoxelSpawnPointJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> normals;

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

    /// <summary>Output: floor spawn data with deterministic hash IDs.</summary>
    public NativeList<CaveSpawnData>.ParallelWriter spawnPoints;

    public void Execute(int idx)
    {
        float3 pos = positions[idx];
        float3 nrm = normals[idx];

        // ── Filter 1: Floor normal ──
        float upDot = math.dot(nrm, new float3(0, 1, 0));
        if (upDot < floorNormalThreshold)
            return;

        // ── Filter 2: Interior depth ──
        float distFromCenter = math.length(pos - volumeCenter);
        float normalizedDist = distFromCenter / math.max(volumeHalfExtent, 1f);
        float interiorness = 1f - math.saturate(normalizedDist);
        if (interiorness < minInteriorDepth)
            return;

        // ── Filter 3: Spatial hash (deterministic thinning) ──
        uint hash = SpatialHash(pos, seed);
        float hashNormalized = (hash & 0xFFFF) / 65535f;
        if (hashNormalized > keepFraction)
            return;

        // ── Passed all filters ──
        // hashId is deterministic: same position → same hash → same ID always
        int hashId = (int)(hash & 0x7FFFFFFF); // Positive int, stable across runs

        spawnPoints.AddNoResize(new CaveSpawnData
        {
            position = pos,
            hashId = hashId
        });
    }

    /// <summary>
    /// Deterministic spatial hash. Same position + same seed = same result.
    /// Thread execution order has ZERO effect on output.
    /// </summary>
    static uint SpatialHash(float3 p, uint seed)
    {
        // Quantize to 10cm grid — prevents floating-point jitter
        int3 ip = (int3)math.floor(p * 10f);

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
}

#endregion

// ════════════════════════════════════════════════════════════════════════════════
//  REGION: HECTON VOXEL ENGINE (v4.0)
// ════════════════════════════════════════════════════════════════════════════════
#region HectonVoxelEngine

public class HectonVoxelEngine : MonoBehaviour
{
    // ╔═══════════════════════════════════════════════╗
    // ║           INSPECTOR SETTINGS                  ║
    // ╚═══════════════════════════════════════════════╝

    [Header("═══ DEFAULT CAVE PRESET ═══")]
    [Tooltip("Default preset used when GenerateVolumeAsync is called without explicit preset.")]
    public CavePreset defaultPreset = new CavePreset();
    [Header("═══ MAPMAGIC INTEGRATION ═══")]
    [Tooltip("MapMagic tile size in meters.\n" +
             "Must match your MapMagic Tile Size setting.\n" +
             "Used to compute chunkCoord for ScavengePopulator spawn points.")]
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

    [Header("═══ REFERENCES ═══")]
    [Tooltip("Bridge to MapMagic terrain for height sampling.")]
    public MapMagicBridge mapMagicBridge;

    [Header("═══ POOL ═══")]
    [Tooltip("Prefab for pooled voxel volume GameObjects.")]
    public GameObject voxelVolumePrefab;

    // ── Constants ──
    const float ABYSSAL_MAX_DEPTH = 5000f;
    const int JOB_BATCH = 64;

    /// <summary>
    /// MC raw buffer multiplier. 2× totalCells instead of 15× (worst case).
    /// Atomic counter in MC job truncates gracefully if buffer fills.
    /// Saves ~85% peak memory allocation.
    /// </summary>
    const int MC_BUFFER_MULTIPLIER = 2;

    // ── Internal ──
    static int _liveEngineCount;
    static int _activeGenerationOperations;
    static int _shutdownRequested;
    readonly List<GameObject> _activeVolumes = new List<GameObject>();
    bool _registeredLiveEngine;

    // ╔═══════════════════════════════════════════════╗
    // ║              LIFECYCLE                        ║
    // ╚═══════════════════════════════════════════════╝

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (!_registeredLiveEngine)
        {
            Interlocked.Increment(ref _liveEngineCount);
            _registeredLiveEngine = true;
        }

        MCTables.Initialize();
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        ClearAllVolumes();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        ClearAllVolumes();
        if (_registeredLiveEngine)
        {
            _registeredLiveEngine = false;
            if (Interlocked.Decrement(ref _liveEngineCount) <= 0)
                RequestSharedTableShutdown();
        }
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
        if (mapMagicBridge == null)
        {
            Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
            return null;
        }

        MCTables.Initialize();

        // ── Resolve preset ──
        if (preset == null) preset = defaultPreset;
        if (preset == null) preset = CavePresetLibrary.Create(CavePresetType.Grotto);

        // ── Grid sizing from preset ──
        int gridDim = math.clamp(preset.gridDimension, 32, 128);
        float voxelStep = math.max(preset.voxelSize, 0.25f);

        int ptsX = gridDim + 1, ptsY = gridDim + 1, ptsZ = gridDim + 1;
        int totalPts = ptsX * ptsY * ptsZ;
        int totalCells = gridDim * gridDim * gridDim;
        int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;

        float volumeHalfExtent = gridDim * voxelStep * 0.5f;
        float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
        float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;


        // Record current world shift state to compensate if it changes during async work
        Vector3 shiftAtStart = HectonFloatingOrigin.Instance != null 
            ? HectonFloatingOrigin.Instance.TotalOffset 
            : Vector3.zero;
        // ── Terrain height at center (for cave graph) ──
        float terrainHeightCenter = worldCenter.y - 10f; // fallback
        if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float h))
            terrainHeightCenter = h;

        // ════════════════════════════════════════════════════════════════
        //  PHASE 0: Generate cave graph (main thread)
        // ════════════════════════════════════════════════════════════════

        CaveGenerationParams caveParams = preset.ToGenerationParams(seed);

        CaveGraphGenerator.Generate(
            seed, preset, worldCenter, terrainHeightCenter, volumeHalfExtent,
            out var caveNodes, out var caveTunnels,
            out var caveEntrances, out var caveStructures,
            Allocator.Persistent);

        // Validate in editor
        #if UNITY_EDITOR
        CaveGraphGenerator.Validate(caveNodes, caveTunnels, caveEntrances,
                                     worldCenter, volumeHalfExtent);
        #endif

        Debug.Log(CaveGraphGenerator.GetSummary(caveNodes, caveTunnels, caveEntrances));

        // ════════════════════════════════════════════════════════════════
        //  ALLOCATE ALL NATIVE CONTAINERS
        // ════════════════════════════════════════════════════════════════

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
        var weldedPositions = new NativeArray<float3>(maxVerts, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
        var triangleIndices = new NativeArray<int>(maxVerts, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
        var weldedCounter = new NativeArray<int>(1, Allocator.Persistent,
                                                   NativeArrayOptions.ClearMemory);
        var edgeToVertex = new NativeParallelHashMap<long, int>(maxVerts / 2, Allocator.Persistent);

        try
        {
            // ════════════════════════════════════════════════════════════
            //  PHASE 1: Sample terrain heights (main thread)
            // ════════════════════════════════════════════════════════════

            float fallbackHeight = terrainHeightCenter;
            for (int iz = 0; iz < ptsZ; iz++)
            for (int ix = 0; ix < ptsX; ix++)
            {
                float wx = volumeOrigin.x + ix * voxelStep;
                float wz = volumeOrigin.z + iz * voxelStep;
                int hi = ix + iz * ptsX;

                if (mapMagicBridge.TryGetHeight(wx, wz, out float height))
                    terrainHeights[hi] = height;
                else
                    terrainHeights[hi] = fallbackHeight;

                gridBiome[hi] = 0f; // Biome stub
            }

            ct.ThrowIfCancellationRequested();

            // ════════════════════════════════════════════════════════════
            //  PHASE 2: Density field (async Burst)
            // ════════════════════════════════════════════════════════════

            var densityHandle = new VoxelDensityJob
            {
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin,
                voxelStep = voxelStep,
                terrainHeights = terrainHeights,
                caveNodes = caveNodes,
                caveTunnels = caveTunnels,
                caveEntrances = caveEntrances,
                caveStructures = caveStructures,
                caveParams = caveParams,
                sealMargin = sealMargin,
                density = densityField
            }.Schedule(totalPts, JOB_BATCH);

            // ════════════════════════════════════════════════════════════
            //  PHASE 3: Marching Cubes extraction (async Burst)
            // ════════════════════════════════════════════════════════════

            var mcHandle = new VoxelMCExtractJob
            {
                cellsX = gridDim, cellsY = gridDim, cellsZ = gridDim,
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin,
                voxelStep = voxelStep,
                density = densityField,
                edgeTable = MCTables.EdgeTable,
                triTable = MCTables.TriTable,
                outVertices = rawVerts,
                vertexCounter = counter
            }.Schedule(totalCells, JOB_BATCH, densityHandle);

            await AwaitForJobCompletionAsync(mcHandle, ct);

            int rawCount = counter[0];
            if (rawCount < 3) return null;

            ct.ThrowIfCancellationRequested();

            // ════════════════════════════════════════════════════════════
            //  PHASE 4: Vertex welding (async Burst)
            // ════════════════════════════════════════════════════════════

            var weldHandle = new VoxelWeldJob
            {
                rawCount = rawCount,
                rawVertices = rawVerts,
                edgeToVertex = edgeToVertex,
                weldedPositions = weldedPositions,
                triangleIndices = triangleIndices,
                weldedCounter = weldedCounter
            }.Schedule();

            await AwaitForJobCompletionAsync(weldHandle, ct);

            int weldedCount = weldedCounter[0];
            if (weldedCount < 3) return null;

            ct.ThrowIfCancellationRequested();

            // ════════════════════════════════════════════════════════════
            //  PHASE 5: Normals + Biome + Colors + Spawn Points (async)
            // ════════════════════════════════════════════════════════════

            var normals = new NativeArray<float3>(weldedCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);
            var biomeVals = new NativeArray<float>(weldedCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            var colors = new NativeArray<Color>(weldedCount, Allocator.Persistent,
                                                  NativeArrayOptions.UninitializedMemory);

            // Spawn point extraction: allocate for ~5% of vertices worst case
            int maxSpawnPoints = math.max(weldedCount / 20, 64);
            var spawnPointList = new NativeList<CaveSpawnData>(maxSpawnPoints, Allocator.Persistent);

            try
            {
                // 5a: Normals
                var normalHandle = new VoxelNormalJob
                {
                    ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin, voxelStep = voxelStep,
                    density = densityField, positions = weldedPositions,
                    normals = normals
                }.Schedule(weldedCount, JOB_BATCH);

                // 5b: Biome
                var biomeHandle = new VoxelBiomeSampleJob
                {
                    ptsX = ptsX, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin, voxelStep = voxelStep,
                    gridBiome = gridBiome, positions = weldedPositions,
                    biomeValues = biomeVals
                }.Schedule(weldedCount, JOB_BATCH);

                // 5c: Colors (depends on normals + biome)
                var colorDeps = JobHandle.CombineDependencies(normalHandle, biomeHandle);
                var colorHandle = new VoxelColorJob
                {
                    maxDepth = ABYSSAL_MAX_DEPTH,
                    caveEdgeWidth = caveEdgeColorWidth,
                    volumeCenter = worldCenter,
                    volumeHalfExtent = volumeHalfExtent,
                    positions = weldedPositions, normals = normals,
                    biomeValues = biomeVals, colors = colors
                }.Schedule(weldedCount, JOB_BATCH, colorDeps);

                // 5d: Spawn points with deterministic hash IDs (depends on normals)
                var spawnHandle = new VoxelSpawnPointJob
                {
                    positions = weldedPositions,
                    normals = normals,
                    volumeCenter = worldCenter,
                    volumeHalfExtent = volumeHalfExtent,
                    floorNormalThreshold = 0.75f,
                    minInteriorDepth = 0.15f,
                    keepFraction = 0.03f,
                    seed = seed,
                    spawnPoints = spawnPointList.AsParallelWriter()
                }.Schedule(weldedCount, JOB_BATCH, normalHandle);

                // Await all
                var allPhase5 = JobHandle.CombineDependencies(colorHandle, spawnHandle);

                await AwaitForJobCompletionAsync(allPhase5, ct);

                ct.ThrowIfCancellationRequested();

                // ════════════════════════════════════════════════════════
                //  PHASE 6: Build mesh (main thread)
                // ════════════════════════════════════════════════════════

                GameObject targetGO = SpawnVolume();
                targetGO.name = $"Cave_{preset.presetType}_{seed}_{worldCenter.x:F0}_{worldCenter.z:F0}";
                
                // Compensate for any world shifts that happened during generation
                Vector3 currentShift = HectonFloatingOrigin.Instance != null 
                    ? HectonFloatingOrigin.Instance.TotalOffset 
                    : Vector3.zero;
                Vector3 shiftDelta = currentShift - shiftAtStart;
                targetGO.transform.position = -shiftDelta;


                BuildWeldedMeshNative(targetGO, weldedPositions, normals, colors,
                                     triangleIndices, rawCount, weldedCount, voxelMaterial, true);
                _activeVolumes.Add(targetGO);

                // ════════════════════════════════════════════════════════
                //  PHASE 7: Register spawn points (deterministic IDs)
                //
                //  localIndex = hashId from spatial hash of world position.
                //  This is DETERMINISTIC: same cave seed + same position =
                //  same hashId, regardless of thread execution order in
                //  VoxelSpawnPointJob. Save system integrity preserved.
                // ════════════════════════════════════════════════════════

                // ════════════════════════════════════════════════════════
                //  PHASE 7: Register spawn points (deterministic IDs)
                //
                //  localIndex = hashId from spatial hash of world position.
                //  context = SpawnContext from cave preset (CaveShallow/CaveDeep).
                //  Both are deterministic across save/load cycles.
                // ════════════════════════════════════════════════════════

                int spawnCount = spawnPointList.Length;
                if (spawnCount > 0 && ScavengePopulator.Instance != null)
                {
                    float tileSize = mapMagicTileSize > 0f ? mapMagicTileSize : 999f;
                    Vector2Int chunkCoord = new Vector2Int(
                        Mathf.FloorToInt(worldCenter.x / tileSize),
                        Mathf.FloorToInt(worldCenter.z / tileSize));

                    // Resolve spawn context from cave params
                    SpawnContext caveContext = caveParams.spawnContext;

                    for (int sp = 0; sp < spawnCount; sp++)
                    {
                        CaveSpawnData data = spawnPointList[sp];

                        ScavengePopulator.Instance.RegisterSpawnPoint(
                            (Vector3)data.position,
                            Quaternion.identity,
                            Vector3.one,
                            chunkCoord,
                            data.hashId,
                            caveContext
                        );
                    }

                    Debug.Log($"[HectonVoxel] Registered {spawnCount} spawn points " +
                              $"(context={caveContext}) in chunk {chunkCoord}");
                }

                float reduction = (1f - (float)weldedCount / rawCount) * 100f;
                float coverageM = gridDim * voxelStep;
                Debug.Log($"[HectonVoxel] Cave '{targetGO.name}': " +
                          $"grid={gridDim}³ voxel={voxelStep}m coverage={coverageM:F0}m | " +
                          $"{rawCount} raw → {weldedCount} welded ({reduction:F0}% reduction) | " +
                          $"{rawCount / 3} tris | {spawnCount} spawn points");

                return targetGO;
            }
            finally
            {
                if (normals.IsCreated) normals.Dispose();
                if (biomeVals.IsCreated) biomeVals.Dispose();
                if (colors.IsCreated) colors.Dispose();
                if (spawnPointList.IsCreated) spawnPointList.Dispose();
            }
        }
        finally
        {
            // ═══ DISPOSE ALL ═══
            if (terrainHeights.IsCreated) terrainHeights.Dispose();
            if (gridBiome.IsCreated) gridBiome.Dispose();
            if (densityField.IsCreated) densityField.Dispose();
            if (rawVerts.IsCreated) rawVerts.Dispose();
            if (counter.IsCreated) counter.Dispose();
            if (weldedPositions.IsCreated) weldedPositions.Dispose();
            if (triangleIndices.IsCreated) triangleIndices.Dispose();
            if (weldedCounter.IsCreated) weldedCounter.Dispose();
            if (edgeToVertex.IsCreated) edgeToVertex.Dispose();

            // Cave graph arrays
            if (caveNodes.IsCreated) caveNodes.Dispose();
            if (caveTunnels.IsCreated) caveTunnels.Dispose();
            if (caveEntrances.IsCreated) caveEntrances.Dispose();
            if (caveStructures.IsCreated) caveStructures.Dispose();
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
        BeginGenerationOperation();
        long generationStartTimestamp = Stopwatch.GetTimestamp();

        if (mapMagicBridge == null)
        {
            Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
            EndGenerationOperation();
            return null;
        }

        MCTables.Initialize();

        int gridDim = math.clamp(gridDimension, 32, 128);
        float voxelStep = math.max(voxelSize, 0.25f);

        int ptsX = gridDim + 1, ptsY = gridDim + 1, ptsZ = gridDim + 1;
        int totalPts = ptsX * ptsY * ptsZ;
        int totalCells = gridDim * gridDim * gridDim;
        int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;

        float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
        float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;

        // Record current world shift state to compensate if it changes during async work
        Vector3 shiftAtStart = HectonFloatingOrigin.Instance != null 
            ? HectonFloatingOrigin.Instance.TotalOffset 
            : Vector3.zero;

        float terrainHeightCenter = worldCenter.y - 10f;
        if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float h))
            terrainHeightCenter = h;

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
        var weldedPositions = new NativeArray<float3>(maxVerts, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
        var triangleIndices = new NativeArray<int>(maxVerts, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
        var weldedCounter = new NativeArray<int>(1, Allocator.Persistent,
                                                   NativeArrayOptions.ClearMemory);
        var edgeToVertex = new NativeParallelHashMap<long, int>(maxVerts / 2, Allocator.Persistent);

        try
        {
            long setupStartTimestamp = Stopwatch.GetTimestamp();
            float fallbackHeight = terrainHeightCenter;
            for (int iz = 0; iz < ptsZ; iz++)
            for (int ix = 0; ix < ptsX; ix++)
            {
                float wx = volumeOrigin.x + ix * voxelStep;
                float wz = volumeOrigin.z + iz * voxelStep;
                int hi = ix + iz * ptsX;

                if (mapMagicBridge.TryGetHeight(wx, wz, out float height))
                    terrainHeights[hi] = height;
                else
                    terrainHeights[hi] = fallbackHeight;

                gridBiome[hi] = 0f;
            }

            float terrainSampleMs = (float)((Stopwatch.GetTimestamp() - setupStartTimestamp) * 1000.0d / Stopwatch.Frequency);

            ct.ThrowIfCancellationRequested();

            var densityHandle = new VoxelDensityJob
            {
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin,
                voxelStep = voxelStep,
                terrainHeights = terrainHeights,
                caveNodes = nodes,
                caveTunnels = tunnels,
                caveEntrances = entrances,
                caveStructures = structures,
                caveParams = caveParams,
                sealMargin = sealMargin,
                density = densityField
            }.Schedule(totalPts, JOB_BATCH);

            var mcHandle = new VoxelMCExtractJob
            {
                cellsX = gridDim, cellsY = gridDim, cellsZ = gridDim,
                ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                volumeOrigin = volumeOrigin,
                voxelStep = voxelStep,
                density = densityField,
                edgeTable = MCTables.EdgeTable,
                triTable = MCTables.TriTable,
                outVertices = rawVerts,
                vertexCounter = counter
            }.Schedule(totalCells, JOB_BATCH, densityHandle);

            float preAwaitSetupMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    $"preawait grid={gridDim} voxel={voxelStep:0.00} pts={totalPts} cells={totalCells} " +
                    $"terrainSample={terrainSampleMs:0.00}ms setup={preAwaitSetupMs:0.00}ms collider={buildCollider}");
            }

            await AwaitForJobCompletionAsync(mcHandle, ct);

            int rawCount = counter[0];
            float marchingCubesMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    $"marching-cubes grid={gridDim} voxel={voxelStep:0.00} rawVerts={rawCount} elapsed={marchingCubesMs:0.00}ms");
            }
            if (rawCount < 3)
                return null;

            ct.ThrowIfCancellationRequested();

            var weldHandle = new VoxelWeldJob
            {
                rawCount = rawCount,
                rawVertices = rawVerts,
                edgeToVertex = edgeToVertex,
                weldedPositions = weldedPositions,
                triangleIndices = triangleIndices,
                weldedCounter = weldedCounter
            }.Schedule();

            await AwaitForJobCompletionAsync(weldHandle, ct);

            int weldedCount = weldedCounter[0];
            float weldMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    $"weld grid={gridDim} voxel={voxelStep:0.00} weldedVerts={weldedCount} elapsed={weldMs:0.00}ms");
            }
            if (weldedCount < 3)
                return null;

            ct.ThrowIfCancellationRequested();

            var normals = new NativeArray<float3>(weldedCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);
            var biomeVals = new NativeArray<float>(weldedCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            var colors = new NativeArray<Color>(weldedCount, Allocator.Persistent,
                                                  NativeArrayOptions.UninitializedMemory);
            int maxSpawnPoints = math.max(weldedCount / 20, 64);
            var spawnPointList = new NativeList<CaveSpawnData>(maxSpawnPoints, Allocator.Persistent);

            try
            {
                float volumeHalfExtent = gridDim * voxelStep * 0.5f;

                var normalHandle = new VoxelNormalJob
                {
                    ptsX = ptsX, ptsY = ptsY, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin, voxelStep = voxelStep,
                    density = densityField, positions = weldedPositions,
                    normals = normals
                }.Schedule(weldedCount, JOB_BATCH);

                var biomeHandle = new VoxelBiomeSampleJob
                {
                    ptsX = ptsX, ptsZ = ptsZ,
                    volumeOrigin = volumeOrigin, voxelStep = voxelStep,
                    gridBiome = gridBiome, positions = weldedPositions,
                    biomeValues = biomeVals
                }.Schedule(weldedCount, JOB_BATCH);

                var colorDeps = JobHandle.CombineDependencies(normalHandle, biomeHandle);
                var colorHandle = new VoxelColorJob
                {
                    maxDepth = ABYSSAL_MAX_DEPTH,
                    caveEdgeWidth = caveEdgeColorWidth,
                    volumeCenter = worldCenter,
                    volumeHalfExtent = volumeHalfExtent,
                    positions = weldedPositions,
                    normals = normals,
                    biomeValues = biomeVals,
                    colors = colors
                }.Schedule(weldedCount, JOB_BATCH, colorDeps);

                var spawnHandle = new VoxelSpawnPointJob
                {
                    positions = weldedPositions,
                    normals = normals,
                    volumeCenter = worldCenter,
                    volumeHalfExtent = volumeHalfExtent,
                    floorNormalThreshold = 0.75f,
                    minInteriorDepth = 0.15f,
                    keepFraction = 0.03f,
                    seed = caveParams.seed,
                    spawnPoints = spawnPointList.AsParallelWriter()
                }.Schedule(weldedCount, JOB_BATCH, normalHandle);

                var allPhase5 = JobHandle.CombineDependencies(colorHandle, spawnHandle);
                await AwaitForJobCompletionAsync(allPhase5, ct);

                ct.ThrowIfCancellationRequested();

                float shadingMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
                if (RuntimeDiagnosticsTrace.IsActive)
                {
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "voxel.pipeline",
                        $"surface-data grid={gridDim} voxel={voxelStep:0.00} spawnPoints={spawnPointList.Length} elapsed={shadingMs:0.00}ms");
                }

                GameObject targetGO = SpawnVolume();
                targetGO.name = $"Cave_Data_{caveParams.seed}_{worldCenter.x:F0}_{worldCenter.z:F0}";

                // Compensate for any world shifts that happened during generation
                Vector3 currentShift = HectonFloatingOrigin.Instance != null 
                    ? HectonFloatingOrigin.Instance.TotalOffset 
                    : Vector3.zero;
                Vector3 shiftDelta = currentShift - shiftAtStart;
                targetGO.transform.position = -shiftDelta;

                BuildWeldedMeshNative(targetGO, weldedPositions, normals, colors,
                                     triangleIndices, rawCount, weldedCount, voxelMaterial, buildCollider);
                _activeVolumes.Add(targetGO);

                int spawnCount = spawnPointList.Length;
                if (spawnCount > 0 && ScavengePopulator.Instance != null)
                {
                    float tileSize = mapMagicTileSize > 0f ? mapMagicTileSize : 999f;
                    Vector2Int chunkCoord = new Vector2Int(
                        Mathf.FloorToInt(worldCenter.x / tileSize),
                        Mathf.FloorToInt(worldCenter.z / tileSize));

                    SpawnContext caveContext = caveParams.spawnContext;
                    for (int sp = 0; sp < spawnCount; sp++)
                    {
                        CaveSpawnData data = spawnPointList[sp];
                        ScavengePopulator.Instance.RegisterSpawnPoint(
                            (Vector3)data.position,
                            Quaternion.identity,
                            Vector3.one,
                            chunkCoord,
                            data.hashId,
                            caveContext);
                    }
                }

                float totalMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
                if (RuntimeDiagnosticsTrace.IsActive)
                {
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "voxel.pipeline",
                        $"mesh-build grid={gridDim} voxel={voxelStep:0.00} collider={buildCollider} spawnPoints={spawnCount} total={totalMs:0.00}ms");
                }

                Debug.Log($"[HectonVoxel] Data volume generated seed={caveParams.seed} grid={gridDim} voxel={voxelStep:F2}.");
                return targetGO;
            }
            finally
            {
                if (normals.IsCreated) normals.Dispose();
                if (biomeVals.IsCreated) biomeVals.Dispose();
                if (colors.IsCreated) colors.Dispose();
                if (spawnPointList.IsCreated) spawnPointList.Dispose();
            }
        }
        finally
        {
            if (terrainHeights.IsCreated) terrainHeights.Dispose();
            if (gridBiome.IsCreated) gridBiome.Dispose();
            if (densityField.IsCreated) densityField.Dispose();
            if (rawVerts.IsCreated) rawVerts.Dispose();
            if (counter.IsCreated) counter.Dispose();
            if (weldedPositions.IsCreated) weldedPositions.Dispose();
            if (triangleIndices.IsCreated) triangleIndices.Dispose();
            if (weldedCounter.IsCreated) weldedCounter.Dispose();
            if (edgeToVertex.IsCreated) edgeToVertex.Dispose();
            EndGenerationOperation();
        }
    }

    // ╔═══════════════════════════════════════════════╗
    // ║       PUBLIC API — VOLUME MANAGEMENT          ║
    // ╚═══════════════════════════════════════════════╝

    /// <summary>Despawns a volume, cleans its mesh, returns to pool.</summary>
    public void DespawnVolume(GameObject volume)
    {
        if (volume == null) return;
        _activeVolumes.Remove(volume);

        var mf = volume.GetComponent<MeshFilter>();
        var mc = volume.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = null;

        if (ObjectPoolManager.Instance != null && voxelVolumePrefab != null)
        {
            if (mf != null && mf.sharedMesh != null)
                mf.sharedMesh.Clear(false);
            ObjectPoolManager.Instance.Despawn(volume);
        }
        else
        {
            if (mf != null && mf.sharedMesh != null)
            {
                mf.sharedMesh.Clear();
                SafeDestroy(mf.sharedMesh);
                mf.sharedMesh = null;
            }
            SafeDestroy(volume);
        }
    }

    /// <summary>Removes null references from active volumes list.</summary>
    public void PurgeNullVolumes()
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] == null)
            {
                int last = _activeVolumes.Count - 1;
                _activeVolumes[i] = _activeVolumes[last];
                _activeVolumes.RemoveAt(last);
            }
        }
    }

    /// <summary>Despawns and cleans all active volumes.</summary>
    public void ClearAllVolumes()
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] != null)
            {
                var mf = _activeVolumes[i].GetComponent<MeshFilter>();
                var mc = _activeVolumes[i].GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = null;

                if (ObjectPoolManager.Instance != null && voxelVolumePrefab != null)
                {
                    if (mf != null && mf.sharedMesh != null)
                        mf.sharedMesh.Clear(false);
                    ObjectPoolManager.Instance.Despawn(_activeVolumes[i]);
                }
                else
                {
                    if (mf != null && mf.sharedMesh != null)
                    {
                        mf.sharedMesh.Clear();
                        SafeDestroy(mf.sharedMesh);
                        mf.sharedMesh = null;
                    }
                    SafeDestroy(_activeVolumes[i]);
                }
            }
        }
        _activeVolumes.Clear();
    }

    public int ActiveVolumeCount => _activeVolumes.Count;

    static async Awaitable AwaitForJobCompletionAsync(JobHandle handle, CancellationToken ct)
    {
        try
        {
            while (!handle.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(ct);
            }
        }
        finally
        {
            handle.Complete();
        }
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
        TryShutdownSharedTables();
    }

    static void TryShutdownSharedTables()
    {
        if (Volatile.Read(ref _liveEngineCount) > 0)
            return;

        if (Volatile.Read(ref _activeGenerationOperations) > 0)
            return;

        if (Interlocked.Exchange(ref _shutdownRequested, 0) == 1)
            MCTables.Shutdown();
    }

    GameObject SpawnVolume()
    {
        if (ObjectPoolManager.Instance != null && voxelVolumePrefab != null)
        {
            GameObject pooled = ObjectPoolManager.Instance.Spawn(voxelVolumePrefab, Vector3.zero, Quaternion.identity);
            PrepareVolumeForBuild(pooled);
            return pooled;
        }

        var go = new GameObject("CaveVolume");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();
        PrepareVolumeForBuild(go);
        return go;
    }

    void BuildWeldedMeshNative(GameObject go,
                               NativeArray<float3> positions,
                               NativeArray<float3> normals,
                               NativeArray<Color> colors,
                               NativeArray<int> triangleIndices,
                               int triIndexCount,
                               int vertCount,
                               Material mat,
                               bool buildCollider)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = $"CaveMesh_{go.name}"
            };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear(false);
        }

        mesh.indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

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

        var mIdx = new NativeArray<int>(triIndexCount, Allocator.Temp,
                                          NativeArrayOptions.UninitializedMemory);
        NativeArray<int>.Copy(triangleIndices, mIdx, triIndexCount);
        mesh.SetIndices(mIdx, MeshTopology.Triangles, 0);
        mIdx.Dispose();

        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.enabled = true;

        var mcol = go.GetComponent<MeshCollider>();
        if (mcol == null) mcol = go.AddComponent<MeshCollider>();
        if (buildCollider)
        {
            mcol.sharedMesh = mesh;
            mcol.enabled = true;
        }
        else
        {
            mcol.sharedMesh = null;
            mcol.enabled = false;
        }
    }

    void PrepareVolumeForBuild(GameObject go)
    {
        if (go == null)
            return;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.enabled = false;

        var mcol = go.GetComponent<MeshCollider>();
        if (mcol != null)
        {
            mcol.sharedMesh = null;
            mcol.enabled = false;
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
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
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
            $"MC Buffer: ×{MC_BUFFER_MULTIPLIER} (safe truncation)",
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
