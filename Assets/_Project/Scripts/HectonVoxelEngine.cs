// HectonVoxelEngine.cs
// Project HECTON-8 localized voxel volumes.
// Unity 6 URP. Burst + Jobs. Marching Cubes. Multi-primitive SDF.

using System;
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
using Hecton8.Dev;
using Hecton8.World;
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
    static void DomainReloadCleanup()
    {
        Shutdown();
        _edgeTable = default;
        _triTable = default;
        _ready = 0;
        _editorHooksInstalled = false;
    }

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
            NativeMemorySentinel.RegisterNativeArray(_edgeTable, nameof(MCTables), nameof(_edgeTable), NativeAllocationLifetime.Permanent);

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
            NativeMemorySentinel.RegisterNativeArray(_triTable, nameof(MCTables), nameof(_triTable), NativeAllocationLifetime.Permanent);

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
            if (_edgeTable.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_edgeTable);
                _edgeTable.Dispose(default);
            }

            if (_triTable.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_triTable);
                _triTable.Dispose(default);
            }

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
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDensityJob : IJobParallelFor
{
    private const byte DeltaModeAdditive = 1 << 0;
    private const byte DeltaModeReplace = 1 << 1;

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
    [ReadOnly] public NativeArray<VoxelCraterStamp> craterStamps;
    [ReadOnly] public NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells;
    [ReadOnly] public NativeArray<int> nodeBucketOffsets;
    [ReadOnly] public NativeArray<int> nodeBucketIndices;
    [ReadOnly] public NativeArray<int> tunnelBucketOffsets;
    [ReadOnly] public NativeArray<int> tunnelBucketIndices;

    // ── Cave parameters ──
    public CaveGenerationParams caveParams;
    public float3 absoluteNoiseOffset;
    public int partitionDimX;
    public int partitionDimY;
    public int partitionDimZ;
    public float3 partitionOrigin;
    public float3 partitionInvCellSize;

    // ── Edge sealing ──
    public float sealMargin;
    public int lodLevel;
    public float lodTransitionBand;

    // ── Output ──
    [WriteOnly] public NativeArray<float> density;
    [WriteOnly] public NativeArray<float> smoothDensity;

    // ════════════════════════════════════════════════════════════════════════
    //  EXECUTE — Per voxel point
    // ════════════════════════════════════════════════════════════════════════

    public void Execute(int idx)
    {
        int ix = idx % ptsX;
        int iy = (idx / ptsX) % ptsY;
        int iz = idx / (ptsX * ptsY);

        float3 wp = volumeOrigin + new float3(ix, iy, iz) * voxelStep;
        EvaluateDensityAt(wp, out float smoothDensityValue, out float finalDensityValue);
        density[idx] = finalDensityValue;
        smoothDensity[idx] = smoothDensityValue;
    }

    void EvaluateDensityAt(float3 wp, out float smoothDensityValue, out float finalDensityValue)
    {
        bool structureOnlyMode = caveParams.structureOnlyMode != 0;
        float terrainH = SampleTerrainHeight(wp.xz);
        float terrainDensity = structureOnlyMode
            ? -1f
            : math.clamp(terrainH - wp.y, -50f, 50f);

        smoothDensityValue = terrainDensity;
        finalDensityValue = terrainDensity;

        float smoothCaveSdf = 1f;
        float finalCaveSdf = 1f;
        if (!structureOnlyMode)
        {
            EvaluateCaveSDF(wp, out smoothCaveSdf, out finalCaveSdf);

            if (smoothCaveSdf < caveParams.shellThickness)
                smoothDensityValue = SmoothSubtractionExp(-smoothCaveSdf, terrainDensity, caveParams.shellThickness);

            if (finalCaveSdf < caveParams.shellThickness)
                finalDensityValue = SmoothSubtractionExp(-finalCaveSdf, terrainDensity, caveParams.shellThickness);
        }

        if (!structureOnlyMode && caveEntrances.Length > 0)
        {
            float entranceSkirtSDF = EvaluateEntranceSkirtSDF(wp);
            if (entranceSkirtSDF < caveParams.entranceBlendK)
            {
                float skirtBlend = caveParams.entranceBlendK * 0.45f;
                smoothDensityValue = SmoothMaxExp(smoothDensityValue, -entranceSkirtSDF, skirtBlend);
                finalDensityValue = SmoothMaxExp(finalDensityValue, -entranceSkirtSDF, skirtBlend);
            }
        }

        if (caveStructures.Length > 0 && (structureOnlyMode || smoothCaveSdf < 0f || finalCaveSdf < 0f))
        {
            EvaluateStructuresSDF(wp, out float smoothStructureSdf, out float finalStructureSdf);
            if (smoothStructureSdf < caveParams.structureBlendK)
                smoothDensityValue = SmoothMaxExp(smoothDensityValue, -smoothStructureSdf, caveParams.structureBlendK);

            if (finalStructureSdf < caveParams.structureBlendK)
                finalDensityValue = SmoothMaxExp(finalDensityValue, -finalStructureSdf, caveParams.structureBlendK);
        }

        if (craterStamps.IsCreated && craterStamps.Length > 0)
        {
            smoothDensityValue = EvaluateCraterModifiers(wp, smoothDensityValue);
            finalDensityValue = EvaluateCraterModifiers(wp, finalDensityValue);
        }

        if (modifiedCells.IsCreated)
        {
            int3 absoluteCell = ResolveAbsoluteCell(wp + absoluteNoiseOffset);
            if (modifiedCells.TryGetValue(absoluteCell, out VoxelModifiedCell storedCell))
            {
                float deltaDensity = (float)storedCell.Density;
                if ((storedCell.Flags & DeltaModeReplace) != 0)
                {
                    smoothDensityValue = deltaDensity;
                    finalDensityValue = deltaDensity;
                }
                else if ((storedCell.Flags & DeltaModeAdditive) != 0)
                {
                    smoothDensityValue = math.max(smoothDensityValue, deltaDensity);
                    finalDensityValue = math.max(finalDensityValue, deltaDensity);
                }
                else
                {
                    smoothDensityValue = math.min(smoothDensityValue, deltaDensity);
                    finalDensityValue = math.min(finalDensityValue, deltaDensity);
                }
            }
        }

        if (!structureOnlyMode)
        {
            smoothDensityValue = ApplyEdgeSeal(wp, smoothDensityValue);
            finalDensityValue = ApplyEdgeSeal(wp, finalDensityValue);
        }
    }

    int3 ResolveAbsoluteCell(float3 absolutePosition)
    {
        float inverseStep = 1f / math.max(voxelStep, 0.0001f);
        return (int3)math.floor(absolutePosition * inverseStep);
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
            float influenceRadius = math.max(entrance.radius * 2.6f, entrance.innerRadius + entrance.funnelLength * 0.35f);
            float2 horizontalDelta = wp.xz - entrance.surfacePosition.xz;
            float horizontalDistSq = math.lengthsq(horizontalDelta);
            float influenceRadiusSq = influenceRadius * influenceRadius;
            if (horizontalDistSq >= influenceRadiusSq)
                continue;

            float horizontalDist = math.sqrt(horizontalDistSq);
            float exemption = 1f - math.smoothstep(entrance.radius * 0.4f, influenceRadius, horizontalDist);
            topSealStrength = math.min(topSealStrength, 1f - exemption);
            if (entrance.inwardDirection.y > 0.3f)
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
            float3 direction = ResolveEntranceDirection(entrance);
            float3 innerPoint = entrance.surfacePosition + direction * entrance.funnelLength;
            float embedDepth = math.max(5f, math.max(voxelStep * 1.5f, entrance.radius * 0.35f));
            float transitionZone = math.clamp(math.max(2.5f, entrance.radius * 0.18f), 2f, 3.5f);
            float3 skirtStart = entrance.surfacePosition + direction * math.min(entrance.funnelLength * 0.18f, entrance.radius);
            float3 skirtEnd = innerPoint + direction * (entrance.innerRadius + embedDepth * 0.6f);

            float outer = SDCapsuleConic(
                wp,
                skirtStart,
                skirtEnd,
                entrance.radius * 1.35f,
                math.max(entrance.innerRadius * 1.55f, entrance.innerRadius + 1.35f));

            float inner = SDCapsuleConic(
                wp,
                entrance.surfacePosition - direction * embedDepth,
                innerPoint + direction * (entrance.innerRadius * 0.75f),
                entrance.radius * 0.92f,
                math.max(entrance.innerRadius * 0.92f, 0.1f));

            float shell = math.max(outer, -inner);
            float terrainClip = wp.y - (SampleTerrainHeight(wp.xz) - embedDepth);
            float transitionClip = wp.y - (SampleTerrainHeight(wp.xz) - embedDepth - transitionZone);
            shell = SmoothMaxExp(shell, transitionClip, transitionZone);
            shell = math.max(shell, terrainClip);
            skirtDist = SmoothMinExp(skirtDist, shell, caveParams.entranceBlendK * 0.3f);
        }

        return skirtDist;
    }

    float3 ResolveEntranceDirection(CaveEntrance entrance)
    {
        float3 direction = math.normalizesafe(entrance.inwardDirection, new float3(0f, -1f, 0f));
        float normalBlend = math.saturate(entrance.terrainNormalBlend);
        if (normalBlend <= 0f)
            return direction;

        float3 terrainNormal = math.normalizesafe(entrance.terrainNormal, new float3(0f, 1f, 0f));
        float3 terrainInward = math.normalizesafe(-terrainNormal, direction);
        return math.normalizesafe(math.lerp(direction, terrainInward, normalBlend * 0.55f), direction);
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

        if (TryGetPartitionRange(nodeBucketOffsets, wp, out int nodeStart, out int nodeEnd))
        {
            for (int i = nodeStart; i < nodeEnd; i++)
            {
                CaveNode node = caveNodes[nodeBucketIndices[i]];
                EvaluateRoom(warpedPos, absoluteWp, node, out float smoothNodeDist, out float finalNodeDist);
                smoothCaveDist = SmoothMinExp(smoothCaveDist, smoothNodeDist, node.blendRadius);
                finalCaveDist = SmoothMinExp(finalCaveDist, finalNodeDist, node.blendRadius);
            }
        }
        else
        {
            for (int i = 0; i < caveNodes.Length; i++)
            {
                EvaluateRoom(warpedPos, absoluteWp, caveNodes[i], out float smoothNodeDist, out float finalNodeDist);
                smoothCaveDist = SmoothMinExp(smoothCaveDist, smoothNodeDist, caveNodes[i].blendRadius);
                finalCaveDist = SmoothMinExp(finalCaveDist, finalNodeDist, caveNodes[i].blendRadius);
            }
        }

        if (TryGetPartitionRange(tunnelBucketOffsets, wp, out int tunnelStart, out int tunnelEnd))
        {
            for (int i = tunnelStart; i < tunnelEnd; i++)
            {
                CaveTunnel tunnel = caveTunnels[tunnelBucketIndices[i]];
                float tunnelDist = EvaluateTunnel(warpedPos, absoluteWp, wp, tunnel);
                smoothCaveDist = SmoothMinExp(smoothCaveDist, tunnelDist, tunnel.blendRadius);
                finalCaveDist = SmoothMinExp(finalCaveDist, tunnelDist, tunnel.blendRadius);
            }
        }
        else
        {
            for (int i = 0; i < caveTunnels.Length; i++)
            {
                float tunnelDist = EvaluateTunnel(warpedPos, absoluteWp, wp, caveTunnels[i]);
                smoothCaveDist = SmoothMinExp(smoothCaveDist, tunnelDist, caveTunnels[i].blendRadius);
                finalCaveDist = SmoothMinExp(finalCaveDist, tunnelDist, caveTunnels[i].blendRadius);
            }
        }

        for (int i = 0; i < caveEntrances.Length; i++)
        {
            float entranceDist = EvaluateEntrance(warpedPos, caveEntrances[i]);
            smoothCaveDist = SmoothMinExp(smoothCaveDist, entranceDist, caveParams.entranceBlendK);
            finalCaveDist = SmoothMinExp(finalCaveDist, entranceDist, caveParams.entranceBlendK);
        }

        float baseFinalCaveDist = finalCaveDist;
        if (math.abs(baseFinalCaveDist) < caveParams.noiseEvalDistance)
        {
            finalCaveDist += EvaluateWallDetail(absoluteWp, baseFinalCaveDist);
            finalCaveDist -= EvaluateFractalNoiseCarve(warpedAbsolutePos, absoluteWp, baseFinalCaveDist);
        }
    }

    bool TryGetPartitionRange(NativeArray<int> bucketOffsets, float3 wp, out int start, out int end)
    {
        start = 0;
        end = 0;
        if (!bucketOffsets.IsCreated || bucketOffsets.Length < 2)
            return false;

        int bucketIndex = ResolvePartitionBucketIndex(wp);
        if (bucketIndex < 0 || bucketIndex + 1 >= bucketOffsets.Length)
            return false;

        start = bucketOffsets[bucketIndex];
        end = bucketOffsets[bucketIndex + 1];
        return true;
    }

    int ResolvePartitionBucketIndex(float3 wp)
    {
        if (partitionDimX <= 0 || partitionDimY <= 0 || partitionDimZ <= 0)
            return -1;

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
            float3 delta = wp - (float3)crater.position;
            if (math.any(math.abs(delta) > outerRadius))
                continue;

            float distSq = math.lengthsq(delta);
            float outerRadiusSq = outerRadius * outerRadius;
            if (distSq >= outerRadiusSq)
                continue;

            float craterDist = math.sqrt(distSq) - crater.radius;
            if (craterDist >= crater.blendRadius)
                continue;

            densityValue = SmoothSubtractionExp(-craterDist, densityValue, math.max(crater.blendRadius, voxelStep));
        }

        return densityValue;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ROOM SDF — Sphere, Ellipsoid, Shaft, Hall, Crevice
    // ════════════════════════════════════════════════════════════════════════

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
            float localNoise = Fractal3DFast(
                absoluteOriginalPos * node.noiseScale * caveParams.wallNoiseFrequency,
                2, caveParams.seed + 7777u);
            finalDist += localNoise * node.noiseAmplitude;
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
        float axisLength = math.length(axis);
        if (axisLength < 0.01f)
            return SDSphere(evalPos, tunnel.pointA, math.max(tunnel.radiusA, tunnel.radiusB));

        float3 tangent = axis / axisLength;
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

            tunnelDist = SmoothMinExp(tunnelDist, segmentDist, math.max(tunnel.blendRadius * 0.35f, 1.5f));
        }

        return tunnelDist;
    }

    float EvaluateEntrance(float3 warpedPos, CaveEntrance entrance)
    {
        float3 direction = ResolveEntranceDirection(entrance);
        float3 innerPoint = entrance.surfacePosition + direction * entrance.funnelLength;
        float core = SDCapsuleConic(
            warpedPos,
            entrance.surfacePosition,
            innerPoint,
            entrance.radius,
            entrance.innerRadius);

        float3 flareStart = entrance.surfacePosition - direction * math.max(entrance.radius * 0.65f, voxelStep);
        float3 flareEnd = entrance.surfacePosition + direction * math.min(entrance.funnelLength * 0.45f, entrance.radius * 2.2f);
        float flare = SDCapsuleConic(
            warpedPos,
            flareStart,
            flareEnd,
            entrance.radius * 1.3f,
            math.max(entrance.innerRadius, entrance.radius * 0.85f));

        return SmoothMinExp(core, flare, caveParams.entranceBlendK * 0.4f);
    }

    void EvaluateStructuresSDF(float3 wp, out float smoothStructDist, out float finalStructDist)
    {
        float3 absoluteWp = wp + absoluteNoiseOffset;
        smoothStructDist = 99999f;
        finalStructDist = 99999f;

        for (int i = 0; i < caveStructures.Length; i++)
        {
            CaveStructure s = caveStructures[i];
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
                float noise = Fractal3DFast((absoluteWp + s.position * 0.17f) * 0.3f, 2, caveParams.seed + 9999u) * s.noiseAmount;
                if (s.structureType == CaveStructureType.Arch)
                    noise += EvaluateLayeredArchNoise(absoluteWp, s) * s.noiseAmount;

                finalSd += noise;
            }

            smoothStructDist = SmoothMinExp(smoothStructDist, smoothSd, s.blendRadius);
            finalStructDist = SmoothMinExp(finalStructDist, finalSd, s.blendRadius);
        }
    }

    float3 ComputeTunnelCurveOffset(float3 pointA, float3 pointB, float3 tangent, float t, float amplitude, uint seedOffset)
    {
        float3 upHint = math.abs(tangent.y) > 0.8f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
        float3 right = math.normalizesafe(math.cross(upHint, tangent), new float3(1f, 0f, 0f));
        float3 up = math.normalizesafe(math.cross(tangent, right), new float3(0f, 1f, 0f));
        float3 absolutePointA = pointA + absoluteNoiseOffset;
        float3 absolutePointB = pointB + absoluteNoiseOffset;
        float3 noisePoint = (absolutePointA + absolutePointB) * 0.03125f + new float3(t * 3.1f, t * 5.7f, t * 7.9f);
        float lateralNoise = Fractal3DFast(noisePoint + new float3(13.1f, 1.7f, 0.3f), 2, caveParams.seed + seedOffset);
        float verticalNoise = Fractal3DFast(noisePoint + new float3(2.9f, 11.3f, 4.1f), 2, caveParams.seed + seedOffset + 101u);
        float envelope = math.sin(t * math.PI);
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
            archDist = SmoothMinExp(archDist, segmentDist, math.max(s.blendRadius * 0.45f, 1.25f));
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
        return math.clamp(detail * nearSurfaceMask, -maxDisplacement, maxDisplacement);
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

    static float SDEllipsoidAnalytic(float3 p, float3 center, float3 radii)
    {
        float3 q = math.abs(p - center);
        float3 safeRadii = math.max(radii, new float3(0.001f));
        float3 invRadii = 1f / safeRadii;
        float3 invRadiiSq = invRadii / safeRadii;
        float k0 = math.length(q * invRadii);
        float k1 = math.length(q * invRadiiSq);
        return (k0 - 1f) * k0 / math.max(k1, 0.0001f);
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

    static float SmoothMinExp(float a, float b, float k)
    {
        k = math.max(k, 0.0001f);
        float minValue = math.min(a, b);
        float expA = math.exp(-math.clamp(k * (a - minValue), 0f, 60f));
        float expB = math.exp(-math.clamp(k * (b - minValue), 0f, 60f));
        return minValue - math.log(expA + expB) / k;
    }

    /// <summary>Smooth maximum. Inverse of smooth min.</summary>
    static float SmoothMax(float a, float b, float k)
    {
        return -SmoothMin(-a, -b, k);
    }

    static float SmoothMaxExp(float a, float b, float k)
    {
        return -SmoothMinExp(-a, -b, k);
    }

    /// <summary>Smooth subtraction: carve shape B out of shape A.</summary>
    static float SmoothSubtraction(float distCarve, float distBase, float k)
    {
        return SmoothMax(distBase, -distCarve, k);
    }

    static float SmoothSubtractionExp(float distCarve, float distBase, float k)
    {
        return SmoothMaxExp(distBase, -distCarve, k);
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

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
struct VoxelColliderChunkClassifyJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<int> triangleIndices;
    public float3 boundsMin;
    public float3 boundsSize;
    public int chunkCount;
    [WriteOnly] public NativeArray<byte> triangleBuckets;

    public void Execute(int triangleIndex)
    {
        int triBase = triangleIndex * 3;
        int i0 = triangleIndices[triBase];
        int i1 = triangleIndices[triBase + 1];
        int i2 = triangleIndices[triBase + 2];

        float3 centroid = (positions[i0] + positions[i1] + positions[i2]) * (1f / 3f);
        triangleBuckets[triangleIndex] = (byte)ResolveChunkIndex(centroid);
    }

    int ResolveChunkIndex(float3 point)
    {
        float3 safeSize = math.max(boundsSize, new float3(0.01f));
        float3 normalized = math.saturate((point - boundsMin) / safeSize);
        int x = normalized.x >= 0.5f ? 1 : 0;
        int z = normalized.z >= 0.5f ? 1 : 0;

        if (chunkCount <= 4)
            return x | (z << 1);

        int y = normalized.y >= 0.5f ? 1 : 0;
        return x | (z << 1) | (y << 2);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelChunkBoundsContentJob : IJob
{
    public int ptsX, ptsY, ptsZ;
    [ReadOnly] public NativeArray<float> density;
    public NativeArray<int> hasContent;

    public void Execute()
    {
        if (hasContent.Length <= 0 || density.Length <= 0 || ptsX <= 0 || ptsY <= 0 || ptsZ <= 0)
            return;

        int maxX = ptsX - 1;
        int maxY = ptsY - 1;
        int maxZ = ptsZ - 1;
        bool allCornersVoid =
            ReadDensity(0, 0, 0) < 0f &&
            ReadDensity(maxX, 0, 0) < 0f &&
            ReadDensity(0, maxY, 0) < 0f &&
            ReadDensity(maxX, maxY, 0) < 0f &&
            ReadDensity(0, 0, maxZ) < 0f &&
            ReadDensity(maxX, 0, maxZ) < 0f &&
            ReadDensity(0, maxY, maxZ) < 0f &&
            ReadDensity(maxX, maxY, maxZ) < 0f;

        hasContent[0] = allCornersVoid ? 0 : 1;
    }

    float ReadDensity(int x, int y, int z)
    {
        return density[x + y * ptsX + z * ptsX * ptsY];
    }
}

//  JOB 2: Marching Cubes exact count pass
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelMCCountJob : IJobParallelFor
{
    public int cellsX, cellsY, cellsZ;
    public int ptsX, ptsY, ptsZ;

    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<int> edgeTable;
    [ReadOnly] public NativeArray<int> triTable;
    [WriteOnly] public NativeArray<int> cellVertexCounts;

    public void Execute(int cellIdx)
    {
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

        int cubeIndex = 0;
        if (d0 < 0f) cubeIndex |= 1;
        if (d1 < 0f) cubeIndex |= 2;
        if (d2 < 0f) cubeIndex |= 4;
        if (d3 < 0f) cubeIndex |= 8;
        if (d4 < 0f) cubeIndex |= 16;
        if (d5 < 0f) cubeIndex |= 32;
        if (d6 < 0f) cubeIndex |= 64;
        if (d7 < 0f) cubeIndex |= 128;

        if (edgeTable[cubeIndex] == 0)
        {
            cellVertexCounts[cellIdx] = 0;
            return;
        }

        int triBase = cubeIndex * 16;
        int triCount = 0;
        for (int t = 0; t < 15; t += 3)
        {
            if (triTable[triBase + t] == -1)
                break;

            triCount++;
        }

        cellVertexCounts[cellIdx] = triCount * 3;
    }

    int GI(int ix, int iy, int iz) => ix + iy * ptsX + iz * ptsX * ptsY;
    float D(int ix, int iy, int iz) => density[GI(ix, iy, iz)];
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 2.1: Marching Cubes extraction (exact-offset write)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct VoxelMCExtractJob : IJobParallelFor
{
    public int cellsX, cellsY, cellsZ;
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;

    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<int> edgeTable;
    [ReadOnly] public NativeArray<int> triTable;
    [ReadOnly] public NativeArray<int> cellVertexOffsets;
    [ReadOnly] public NativeArray<int> cellVertexCounts;

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
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<MCRawVertex> outVertices;

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

        int vertCount = cellVertexCounts[cellIdx];
        if (vertCount <= 0)
            return;

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
        int writeOffset = cellVertexOffsets[cellIdx];
        if (writeOffset < 0 || writeOffset + vertCount > outVertices.Length)
            return;

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
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct VoxelWeldJob : IJob
{
    private const int InvalidVertexIndex = -1;

    public int rawCount;
    public int ptsX;
    public int ptsY;
    public int ptsZ;
    [ReadOnly] public NativeArray<MCRawVertex> rawVertices;
    public NativeArray<int> edgeVertexX;
    public NativeArray<int> edgeVertexY;
    public NativeArray<int> edgeVertexZ;
    [WriteOnly]
    public NativeArray<float3> weldedPositions;
    [WriteOnly]
    public NativeArray<int> triangleIndices;
    public NativeArray<int> weldedCounter;

    public void Execute()
    {
        int weldedCount = 0;
        for (int i = 0; i < rawCount; i++)
        {
            MCRawVertex rv = rawVertices[i];
            if (TryResolveEdgeRegistrySlot(rv.edgeId, out int axis, out int edgeSlot))
            {
                int existingIdx = ReadEdgeVertex(axis, edgeSlot);
                if (existingIdx != InvalidVertexIndex)
                {
                    triangleIndices[i] = existingIdx;
                    continue;
                }

                int newIdx = weldedCount;
                weldedPositions[newIdx] = rv.position;
                WriteEdgeVertex(axis, edgeSlot, newIdx);
                triangleIndices[i] = newIdx;
                weldedCount++;
                continue;
            }

            int fallbackIdx = weldedCount;
            weldedPositions[fallbackIdx] = rv.position;
            triangleIndices[i] = fallbackIdx;
            weldedCount++;
        }
        weldedCounter[0] = weldedCount;
    }

    bool TryResolveEdgeRegistrySlot(long packedEdge, out int axis, out int slot)
    {
        int lo = (int)(packedEdge & 0xFFFFFFFFL);
        int hi = (int)(packedEdge >> 32);
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
            return true;
        }

        if (diff == strideX && y < cellsY)
        {
            axis = 1;
            slot = x + y * ptsX + z * ptsX * cellsY;
            return true;
        }

        if (diff == strideXY && z < cellsZ)
        {
            axis = 2;
            slot = x + y * ptsX + z * ptsX * ptsY;
            return true;
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
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 3: Normals from SDF central-difference gradient
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelNormalJob : IJobParallelFor
{
    public int ptsX, ptsY, ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    [ReadOnly] public NativeArray<float> densityField;
    [ReadOnly] public NativeArray<float> smoothDensityField;
    [ReadOnly] public NativeArray<float3> positions;
    [WriteOnly] public NativeArray<float3> normals;
    [WriteOnly] public NativeArray<float> curvatureValues;
    [WriteOnly] public NativeArray<float> ambientOcclusionValues;

    public void Execute(int idx)
    {
        float3 wp = positions[idx];
        float epsilon = math.max(voxelStep * 0.5f, 0.05f);
        float3 offsetX = new float3(epsilon, 0f, 0f);
        float3 offsetY = new float3(0f, epsilon, 0f);
        float3 offsetZ = new float3(0f, 0f, epsilon);

        float3 gradient = SampleInterpolatedCentralDifferenceGradient(densityField, wp);
        float3 normal = math.normalizesafe(-gradient, new float3(0f, 1f, 0f));
        normals[idx] = normal;

        float invEpsilonSq = 1f / math.max(epsilon * epsilon, 0.0001f);
        float centerDensity = SampleField(smoothDensityField, wp);
        float smoothPosX = SampleField(smoothDensityField, wp + offsetX);
        float smoothNegX = SampleField(smoothDensityField, wp - offsetX);
        float smoothPosY = SampleField(smoothDensityField, wp + offsetY);
        float smoothNegY = SampleField(smoothDensityField, wp - offsetY);
        float smoothPosZ = SampleField(smoothDensityField, wp + offsetZ);
        float smoothNegZ = SampleField(smoothDensityField, wp - offsetZ);
        float laplacian =
            (smoothPosX + smoothNegX - (2f * centerDensity)) +
            (smoothPosY + smoothNegY - (2f * centerDensity)) +
            (smoothPosZ + smoothNegZ - (2f * centerDensity));

        float signedCurvature = (laplacian * invEpsilonSq) * epsilon;
        float curvature01 = math.saturate(0.5f + signedCurvature * 0.35f);
        curvatureValues[idx] = curvature01;

        ambientOcclusionValues[idx] = 1f;
    }

    float3 SampleInterpolatedCentralDifferenceGradient(NativeArray<float> field, float3 worldPosition)
    {
        float sampleX = math.clamp((worldPosition.x - volumeOrigin.x) / voxelStep, 0f, ptsX - 1.001f);
        float sampleY = math.clamp((worldPosition.y - volumeOrigin.y) / voxelStep, 0f, ptsY - 1.001f);
        float sampleZ = math.clamp((worldPosition.z - volumeOrigin.z) / voxelStep, 0f, ptsZ - 1.001f);

        int x0 = (int)math.floor(sampleX);
        int y0 = (int)math.floor(sampleY);
        int z0 = (int)math.floor(sampleZ);
        int x1 = math.min(x0 + 1, ptsX - 1);
        int y1 = math.min(y0 + 1, ptsY - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);

        float tx = sampleX - x0;
        float ty = sampleY - y0;
        float tz = sampleZ - z0;

        float3 g000 = SampleGridNodeGradient(field, x0, y0, z0);
        float3 g100 = SampleGridNodeGradient(field, x1, y0, z0);
        float3 g010 = SampleGridNodeGradient(field, x0, y1, z0);
        float3 g110 = SampleGridNodeGradient(field, x1, y1, z0);
        float3 g001 = SampleGridNodeGradient(field, x0, y0, z1);
        float3 g101 = SampleGridNodeGradient(field, x1, y0, z1);
        float3 g011 = SampleGridNodeGradient(field, x0, y1, z1);
        float3 g111 = SampleGridNodeGradient(field, x1, y1, z1);

        float3 g00 = math.lerp(g000, g100, tx);
        float3 g10 = math.lerp(g010, g110, tx);
        float3 g01 = math.lerp(g001, g101, tx);
        float3 g11 = math.lerp(g011, g111, tx);
        float3 g0 = math.lerp(g00, g10, ty);
        float3 g1 = math.lerp(g01, g11, ty);
        return math.lerp(g0, g1, tz);
    }

    float3 SampleGridNodeGradient(NativeArray<float> field, int x, int y, int z)
    {
        int xPrev = math.max(0, x - 1);
        int xNext = math.min(ptsX - 1, x + 1);
        int yPrev = math.max(0, y - 1);
        int yNext = math.min(ptsY - 1, y + 1);
        int zPrev = math.max(0, z - 1);
        int zNext = math.min(ptsZ - 1, z + 1);

        return new float3(
            field[GridIndex(xNext, y, z)] - field[GridIndex(xPrev, y, z)],
            field[GridIndex(x, yNext, z)] - field[GridIndex(x, yPrev, z)],
            field[GridIndex(x, y, zNext)] - field[GridIndex(x, y, zPrev)]);
    }

    float SampleField(NativeArray<float> field, float3 worldPosition)
    {
        float sampleX = math.clamp((worldPosition.x - volumeOrigin.x) / voxelStep, 0f, ptsX - 1.001f);
        float sampleY = math.clamp((worldPosition.y - volumeOrigin.y) / voxelStep, 0f, ptsY - 1.001f);
        float sampleZ = math.clamp((worldPosition.z - volumeOrigin.z) / voxelStep, 0f, ptsZ - 1.001f);

        int x0 = (int)math.floor(sampleX);
        int y0 = (int)math.floor(sampleY);
        int z0 = (int)math.floor(sampleZ);
        int x1 = math.min(x0 + 1, ptsX - 1);
        int y1 = math.min(y0 + 1, ptsY - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);

        float tx = sampleX - x0;
        float ty = sampleY - y0;
        float tz = sampleZ - z0;

        float c000 = field[GridIndex(x0, y0, z0)];
        float c100 = field[GridIndex(x1, y0, z0)];
        float c010 = field[GridIndex(x0, y1, z0)];
        float c110 = field[GridIndex(x1, y1, z0)];
        float c001 = field[GridIndex(x0, y0, z1)];
        float c101 = field[GridIndex(x1, y0, z1)];
        float c011 = field[GridIndex(x0, y1, z1)];
        float c111 = field[GridIndex(x1, y1, z1)];

        float c00 = math.lerp(c000, c100, tx);
        float c10 = math.lerp(c010, c110, tx);
        float c01 = math.lerp(c001, c101, tx);
        float c11 = math.lerp(c011, c111, tx);
        float c0 = math.lerp(c00, c10, ty);
        float c1 = math.lerp(c01, c11, ty);
        return math.lerp(c0, c1, tz);
    }

int GridIndex(int x, int y, int z) => x + y * ptsX + z * ptsX * ptsY;
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelTerrainSeamSnapJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float3 absoluteUniverseOffset;
    public float voxelStep;
    public float seamTransitionBand;
    public float seamOverlap;

    [ReadOnly] public NativeArray<float> terrainHeights;
    public NativeArray<float3> positions;

    public void Execute(int idx)
    {
        if (!terrainHeights.IsCreated || ptsX <= 1 || ptsZ <= 1 || seamTransitionBand <= 0f)
            return;

        float3 position = positions[idx];
        float2 absoluteWorldXZ = position.xz + absoluteUniverseOffset.xz;
        float boundaryDistance = VoxelSeamDirector.ComputeBoundaryDistance(
            absoluteWorldXZ,
            volumeOrigin + absoluteUniverseOffset,
            ptsX,
            ptsZ,
            voxelStep);
        if (boundaryDistance > seamTransitionBand)
            return;

        float terrainHeight = SampleTerrainHeight(absoluteWorldXZ);
        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        float targetHeight = VoxelSeamDirector.ComputeTargetSnapHeight(terrainHeight, seamOverlap);
        positions[idx] = new float3(position.x, math.lerp(position.y, targetHeight, blendToTerrain), position.z);
    }

    float SampleTerrainHeight(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - (volumeOrigin.x + absoluteUniverseOffset.x)) / voxelStep;
        float localZ = (absoluteWorldXZ.y - (volumeOrigin.z + absoluteUniverseOffset.z)) / voxelStep;
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
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSeamNormalBlendJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float3 absoluteUniverseOffset;
    public float voxelStep;
    public float seamTransitionBand;

    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float> terrainHeights;
    public NativeArray<float3> normals;

    public void Execute(int idx)
    {
        if (!terrainHeights.IsCreated || ptsX <= 1 || ptsZ <= 1 || seamTransitionBand <= 0f)
            return;

        float3 position = positions[idx];
        float2 absoluteWorldXZ = position.xz + absoluteUniverseOffset.xz;
        float boundaryDistance = VoxelSeamDirector.ComputeBoundaryDistance(
            absoluteWorldXZ,
            volumeOrigin + absoluteUniverseOffset,
            ptsX,
            ptsZ,
            voxelStep);
        if (boundaryDistance > seamTransitionBand)
            return;

        float3 terrainNormal = SampleTerrainNormal(absoluteWorldXZ);
        float3 voxelNormal = math.normalizesafe(normals[idx], new float3(0f, 1f, 0f));
        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        normals[idx] = BlendNormalsSlerp(voxelNormal, terrainNormal, blendToTerrain);
    }

    float SampleTerrainHeight(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - (volumeOrigin.x + absoluteUniverseOffset.x)) / voxelStep;
        float localZ = (absoluteWorldXZ.y - (volumeOrigin.z + absoluteUniverseOffset.z)) / voxelStep;
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

    float3 SampleTerrainNormal(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - (volumeOrigin.x + absoluteUniverseOffset.x)) / voxelStep;
        float localZ = (absoluteWorldXZ.y - (volumeOrigin.z + absoluteUniverseOffset.z)) / voxelStep;
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
        return math.normalizesafe(math.lerp(normalX0, normalX1, fz), new float3(0f, 1f, 0f));
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

        float stepX = math.max((xNext - xPrev) * voxelStep, voxelStep);
        float stepZ = math.max((zNext - zPrev) * voxelStep, voxelStep);
        float3 tangentX = new float3(stepX, heightRight - heightLeft, 0f);
        float3 tangentZ = new float3(0f, heightForward - heightBack, stepZ);
        return math.normalizesafe(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
    }

    static float3 BlendNormalsSlerp(float3 startNormal, float3 endNormal, float t)
    {
        float blend = math.saturate(t);
        float dot = math.clamp(math.dot(startNormal, endNormal), -1f, 1f);
        if (math.abs(dot) > 0.9999f)
            return math.normalizesafe(math.lerp(startNormal, endNormal, blend), startNormal);

        float theta = math.acos(dot);
        float sinTheta = math.sin(theta);
        if (sinTheta <= 0.0001f)
            return math.normalizesafe(math.lerp(startNormal, endNormal, blend), startNormal);

        float startWeight = math.sin((1f - blend) * theta) / sinTheta;
        float endWeight = math.sin(blend * theta) / sinTheta;
        return math.normalizesafe(startNormal * startWeight + endNormal * endWeight, startNormal);
    }
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelShiftAwareProjectionJob : IJobParallelFor
{
    public float3 rebaseDelta;
    public float3 rootRuntimePosition;

    [ReadOnly] public NativeArray<float3> sourcePositions;
    [WriteOnly] public NativeArray<float3> projectedPositions;

    public void Execute(int index)
    {
        projectedPositions[index] = sourcePositions[index] + rebaseDelta - rootRuntimePosition;
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 3.5: Biome Sampling (UNCHANGED from v3.2)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelColorJob : IJobParallelFor
{
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

    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> normals;
    [ReadOnly] public NativeArray<float> terrainHeights;
    [ReadOnly] public NativeArray<float> curvatureValues;
    [ReadOnly] public NativeArray<float> biomeValues;
    [ReadOnly] public NativeArray<CaveEntrance> caveEntrances;

    [WriteOnly] public NativeArray<Color> colors;
    [WriteOnly] public NativeArray<float> skirtAlphaValues;

    public void Execute(int idx)
    {
        float3 p = positions[idx];
        float3 n = normals[idx];

        // R = slope (0 = flat floor/ceiling, 1 = vertical wall)
        float slope = 1f - math.abs(math.dot(n, new float3(0, 1, 0)));

        // G = depth below sea level (normalized 0-1)
        float depth = math.saturate(-p.y / math.max(maxDepth, 1f));

        float distFromCenter = math.length(p - volumeCenter) / math.max(volumeHalfExtent, 1f);
        float interiorFade = math.saturate(distFromCenter);

        float terrainSkirt = 0f;
        if (terrainHeights.IsCreated && ptsX > 1 && ptsZ > 1)
        {
            float terrainHeight = SampleTerrainHeight(p.xz);
            terrainSkirt = 1f - math.smoothstep(0f, math.max(seamTransitionBand, 0.01f), math.abs(terrainHeight - p.y));
        }

        float lodEdgeSkirt = 0f;
        if (lodLevel > 0)
        {
            float volumeSizeX = (ptsX - 1) * voxelStep;
            float volumeSizeZ = (ptsZ - 1) * voxelStep;
            float localX = p.x - volumeOrigin.x;
            float localZ = p.z - volumeOrigin.z;
            float edgeDist = math.min(localX, math.min(volumeSizeX - localX, math.min(localZ, volumeSizeZ - localZ)));
            lodEdgeSkirt = 1f - math.smoothstep(0f, math.max(lodTransitionBand, voxelStep), edgeDist);
        }

        float skirtAlpha = math.saturate(math.max(terrainSkirt, lodEdgeSkirt));
        float4 colorPayload = new float4(slope, depth, 0f, 0f);
        if (TryResolveCaveMouthTerrainColor(p, out float4 terrainSplatColor, out float splatWeight))
        {
            colorPayload.xyz = terrainSplatColor.xyz;
            colorPayload.w = splatWeight;
        }

        if (skirtAlphaValues.IsCreated && idx < skirtAlphaValues.Length)
            skirtAlphaValues[idx] = skirtAlpha;

        colors[idx] = new Color(colorPayload.x, colorPayload.y, colorPayload.z, colorPayload.w);
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
            float blend = math.saturate(math.max(entrance.terrainSplatBlend, entrance.terrainSplatColor.w));
            if (blend <= 0.0001f)
                continue;

            float radius = math.max(entrance.radius, voxelStep);
            float distance = math.length(position - entrance.surfacePosition);
            float localWeight = (1f - math.smoothstep(radius * 0.35f, math.max(radius * 1.85f, voxelStep), distance)) * blend;
            if (localWeight <= weight)
                continue;

            weight = localWeight;
            float caveCutDepth01 = math.saturate(1f - distance / math.max(radius * 1.85f, voxelStep));
            float mouthDarkening = caveCutDepth01 * caveCutDepth01 * blend * 0.58f;
            terrainColor = math.saturate(entrance.terrainSplatColor);
            terrainColor.xyz *= 1f - mouthDarkening;
        }

        return weight > 0.0001f;
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
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 5: Cave Interior Spawn Points (v4.1 — deterministic hash IDs)
//  Extracts floor positions from welded mesh for loot/flora/fauna spawning.
//  Each point carries a deterministic hashId derived from world position,
//  ensuring save system consistency regardless of parallel execution order.
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDirtyBlendJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells;
    public float voxelStep;
    public float3 absoluteUniverseOffset;
    [WriteOnly] public NativeArray<float> dirtyBlendValues;

    public void Execute(int index)
    {
        if (!dirtyBlendValues.IsCreated || index < 0 || index >= dirtyBlendValues.Length)
            return;

        if (!modifiedCells.IsCreated || !positions.IsCreated || index >= positions.Length || voxelStep <= 0.0001f)
        {
            dirtyBlendValues[index] = 0f;
            return;
        }

        float3 absolutePosition = positions[index] + absoluteUniverseOffset;
        int3 cell = (int3)math.floor(absolutePosition / voxelStep);
        float blend = modifiedCells.ContainsKey(cell) ? 1f : 0f;
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
        return modifiedCells.ContainsKey(cell + offset) ? 0.65f : 0f;
    }
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
    private const string DefaultVoxelBakeGhostShaderName = "Hecton8/Environment/Hecton_VoxelBakeGhost";
    private const string RuntimeCaveVolumeName = "CaveVolume";
    private const string RuntimeCaveMeshName = "CaveMesh";
    private const int StreamingScratchLeaseTimeoutFrames = 1200;
    private const int VoxelJobWaitWatchdogFrames = 1200;
    private const int DeferredVoxelPhysicsBakeTeardownDrainBudget = 8;
    private const int DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget = 32;
    private const int DeferredVoxelPhysicsBakeTeardownInspectionBudget = 64;
    private const int DeferredVoxelPhysicsBakeTeardownBackpressureInspectionBudget = 64;
    private const int DeferredVoxelPhysicsBakeBackpressureThreshold = 64;
    private const int DeferredVoxelPhysicsBakeBackpressureReleaseThreshold = 32;
    private const int DeferredVoxelPhysicsBakeTeardownCapacity = 2048;
    private const byte DeferredVoxelBakeDestroyOwner = 1 << 0;
    private const float VoxelLodColliderDisableDistanceMeters = 200f;
    private const int VoxelPhysicsBakeMeshPoolSize = 32;
    private const string VoxelPhysicsBakePoolMeshName = "VoxelPhysicsBakePool";
    private const byte DeltaModeAdditive = 1 << 0;
    private const byte DeltaModeReplace = 1 << 1;
    private const string NativeMemoryOwner = nameof(HectonVoxelEngine);
    private const string ModifiedCellsNativeMemoryLabelPrefix = "VoxelPipelineData.ModifiedCells.";
    private const string SpawnPointListNativeMemoryLabelPrefix = "VoxelPipelineData.SpawnPointList.";
    private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
    private static readonly uint _VoxelTeardownBackpressureWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.TeardownBackpressure"));
    private static readonly uint _VoxelPhysicsBakePoolExhaustedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.MeshPoolExhausted"));
    private static readonly uint _VoxelPhysicsBakeContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.PhysicsBake"));

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
    const int JOB_BATCH = 64;
    const int ActiveVolumeRegistryCapacity = 64;
    const int AirPocketRegistryCapacity = 64;

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
    internal static HectonVoxelEngine ActiveRuntimeInstance { get; private set; }
    private static int _airPocketCount;
    private static readonly Vector3[] _airPocketCenters = new Vector3[AirPocketRegistryCapacity]; // COLD ALLOC: Vector3[64] - fixed voxel air-pocket centers - owner: HectonVoxelEngine
    private static readonly Vector3[] _airPocketHalfExtents = new Vector3[AirPocketRegistryCapacity]; // COLD ALLOC: Vector3[64] - fixed voxel air-pocket AABB extents - owner: HectonVoxelEngine
    private static readonly float[] _airPocketRefillFractions = new float[AirPocketRegistryCapacity]; // COLD ALLOC: float[64] - fixed voxel air-pocket O2 refill scalars - owner: HectonVoxelEngine

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticRuntimeState()
    {
        _liveEngineCount = 0;
        _activeGenerationOperations = 0;
        _shutdownRequested = 0;
        ActiveRuntimeInstance = null;
        ClearAirPocketRegistry();
        _deferredVoxelPhysicsBakeTeardowns.Clear();
        _deferredVoxelPhysicsBakeTeardownRegistered = false;
        _deferredVoxelPhysicsBakeBackpressureActive = false;
        _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
        _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
    }
    // COLD ALLOC: List<DeferredVoxelPhysicsBakeTeardown>[2048] - deferred voxel collider PhysX bake teardown queue - owner: HectonVoxelEngine
    private static readonly List<DeferredVoxelPhysicsBakeTeardown> _deferredVoxelPhysicsBakeTeardowns = new List<DeferredVoxelPhysicsBakeTeardown>(DeferredVoxelPhysicsBakeTeardownCapacity);
    // COLD ALLOC: Mesh[32] - global PhysX voxel bake mesh pool - owner: HectonVoxelEngine
    private static readonly Mesh[] _voxelPhysicsBakeMeshPool = new Mesh[VoxelPhysicsBakeMeshPoolSize];
    // COLD ALLOC: bool[32] - occupancy flags for global PhysX voxel bake mesh pool - owner: HectonVoxelEngine
    private static readonly bool[] _voxelPhysicsBakeMeshPoolInUse = new bool[VoxelPhysicsBakeMeshPoolSize];
    // COLD ALLOC: DeferredVoxelPhysicsBakeTeardownDriver[1] - dispatcher late-frame adapter for voxel bake teardown - owner: HectonVoxelEngine
    private static readonly DeferredVoxelPhysicsBakeTeardownDriver _deferredVoxelPhysicsBakeTeardownDriver = new DeferredVoxelPhysicsBakeTeardownDriver();
    private static bool _deferredVoxelPhysicsBakeTeardownRegistered;
    private static bool _deferredVoxelPhysicsBakeBackpressureActive;
    private static int _deferredVoxelPhysicsBakeTeardownScanCursor;
    private static bool _voxelPhysicsBakeMeshPoolExhaustedWarningArmed;

    private struct DeferredVoxelPhysicsBakeTeardown
    {
        public Mesh Mesh;
        public GameObject Owner;
        public MeshRenderer Renderer;
        public MeshCollider Collider;
        public JobHandle Handle;
        public byte Flags;
    }

    private sealed class DeferredVoxelPhysicsBakeTeardownDriver : ILateFrameTickable
    {
        public void LateFrameTick()
        {
            DrainDeferredVoxelPhysicsBakeTeardowns();
        }
    }

    internal static int RegisterAirPocket(Vector3 centerWS, Vector3 halfExtentsWS, float oxygenRefillFraction = 1f)
    {
        if (_airPocketCount >= AirPocketRegistryCapacity ||
            !IsFiniteVector(centerWS) ||
            !IsFiniteVector(halfExtentsWS))
        {
            return 0;
        }

        Vector3 safeExtents = new Vector3(
            math.max(0.01f, math.abs(halfExtentsWS.x)),
            math.max(0.01f, math.abs(halfExtentsWS.y)),
            math.max(0.01f, math.abs(halfExtentsWS.z)));
        int slot = _airPocketCount++;
        _airPocketCenters[slot] = centerWS;
        _airPocketHalfExtents[slot] = safeExtents;
        _airPocketRefillFractions[slot] = math.saturate(oxygenRefillFraction);
        return slot + 1;
    }

    internal static void UnregisterAirPocket(int handle)
    {
        int slot = handle - 1;
        if ((uint)slot >= (uint)_airPocketCount)
            return;

        int lastSlot = _airPocketCount - 1;
        _airPocketCenters[slot] = _airPocketCenters[lastSlot];
        _airPocketHalfExtents[slot] = _airPocketHalfExtents[lastSlot];
        _airPocketRefillFractions[slot] = _airPocketRefillFractions[lastSlot];
        _airPocketCenters[lastSlot] = Vector3.zero;
        _airPocketHalfExtents[lastSlot] = Vector3.zero;
        _airPocketRefillFractions[lastSlot] = 0f;
        _airPocketCount = lastSlot;
    }

    internal static void ClearAirPocketRegistry()
    {
        for (int i = 0; i < _airPocketCount; i++)
        {
            _airPocketCenters[i] = Vector3.zero;
            _airPocketHalfExtents[i] = Vector3.zero;
            _airPocketRefillFractions[i] = 0f;
        }

        _airPocketCount = 0;
    }

    internal static bool TrySampleAirPocket(Vector3 worldPosition, out float oxygenRefillFraction)
    {
        oxygenRefillFraction = 0f;
        if (!IsFiniteVector(worldPosition))
            return false;

        for (int i = 0; i < _airPocketCount; i++)
        {
            Vector3 center = _airPocketCenters[i];
            Vector3 extents = _airPocketHalfExtents[i];
            if (math.abs(worldPosition.x - center.x) > extents.x ||
                math.abs(worldPosition.y - center.y) > extents.y ||
                math.abs(worldPosition.z - center.z) > extents.z)
            {
                continue;
            }

            oxygenRefillFraction = math.max(0.01f, _airPocketRefillFractions[i]);
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

    // COLD ALLOC: List<GameObject>[64] - active voxel volume object registry - owner: HectonVoxelEngine
    readonly List<GameObject> _activeVolumes = new List<GameObject>(ActiveVolumeRegistryCapacity);
    // COLD ALLOC: List<HectonVoxelVolume>[64] - active voxel volume component registry - owner: HectonVoxelEngine
    readonly List<HectonVoxelVolume> _activeVolumeComponents = new List<HectonVoxelVolume>(ActiveVolumeRegistryCapacity);
    // COLD ALLOC: List<Bounds>[64] - cached local mesh bounds for editor gizmos - owner: HectonVoxelEngine
    readonly List<Bounds> _activeVolumeLocalBounds = new List<Bounds>(ActiveVolumeRegistryCapacity);
    readonly object _streamingScratchGate = new object();
    bool _registeredLiveEngine;
    bool _teardownStreamingScratchRequested;
    VoxelStreamingScratchSlot[] _streamingScratchSlots;
    VoxelDeltaProcessor _deltaProcessor;
    Material _runtimeVoxelBakeGhostMaterial;

    internal VoxelDeltaProcessor DeltaProcessor => _deltaProcessor;
    internal Material ResolvedVoxelBakeGhostMaterial => voxelBakeGhostMaterial != null ? voxelBakeGhostMaterial : _runtimeVoxelBakeGhostMaterial;

    sealed class VoxelStreamingScratchSlot
    {
        public NativeArray<float> TerrainHeights;
        public NativeArray<float> GridBiome;
        public NativeArray<float> DensityField;
        public NativeArray<float> SmoothDensityField;
        public NativeArray<int> ChunkContentFlags;
        public NativeArray<int> CellVertexCounts;
        public NativeArray<int> CellVertexOffsets;
        public bool InUse;

        public void Dispose()
        {
            HectonVoxelEngine.DisposeTrackedNativeArray(ref TerrainHeights);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref GridBiome);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref DensityField);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SmoothDensityField);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ChunkContentFlags);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref CellVertexCounts);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref CellVertexOffsets);
            InUse = false;
        }
    }

    struct VoxelStreamingScratchLease : System.IDisposable
    {
        HectonVoxelEngine _owner;
        int _slotIndex;

        public NativeArray<float> TerrainHeights;
        public NativeArray<float> GridBiome;
        public NativeArray<float> DensityField;
        public NativeArray<float> SmoothDensityField;
        public NativeArray<int> ChunkContentFlags;
        public NativeArray<int> CellVertexCounts;
        public NativeArray<int> CellVertexOffsets;

        public bool IsValid => _owner != null && _slotIndex >= 0;

        public VoxelStreamingScratchLease(
            HectonVoxelEngine owner,
            int slotIndex,
            NativeArray<float> terrainHeights,
            NativeArray<float> gridBiome,
            NativeArray<float> densityField,
            NativeArray<float> smoothDensityField,
            NativeArray<int> chunkContentFlags,
            NativeArray<int> cellVertexCounts,
            NativeArray<int> cellVertexOffsets)
        {
            _owner = owner;
            _slotIndex = slotIndex;
            TerrainHeights = terrainHeights;
            GridBiome = gridBiome;
            DensityField = densityField;
            SmoothDensityField = smoothDensityField;
            ChunkContentFlags = chunkContentFlags;
            CellVertexCounts = cellVertexCounts;
            CellVertexOffsets = cellVertexOffsets;
        }

        public void Dispose()
        {
            if (_owner == null || _slotIndex < 0)
                return;

            _owner.ReleaseStreamingScratchLease(_slotIndex);
            _owner = null;
            _slotIndex = -1;
        }
    }

    sealed class VoxelPipelineData : IDisposable
    {
        public HectonVoxelVolume SourceVolume;
        public int SourceRuntimeStamp;
        public Vector3 WorldCenter;
        public Vector3 AbsoluteUniverseOffsetAtStart;
        public uint ShiftEpochAtStart;
        public float TerrainHeightCenter;
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
        public NativeArray<CaveNode> Nodes;
        public NativeArray<CaveTunnel> Tunnels;
        public NativeArray<CaveEntrance> Entrances;
        public NativeArray<CaveStructure> Structures;
        public NativeArray<VoxelCraterStamp> CraterStamps;
        public NativeParallelHashMap<int3, VoxelModifiedCell> ModifiedCells;
        public string ModifiedCellsNativeMemoryLabel;
        public NativeArray<MCRawVertex> RawVertices;
        public NativeArray<float3> WeldedPositions;
        public NativeArray<int> TriangleIndices;
        public NativeArray<int> EdgeVertexX;
        public NativeArray<int> EdgeVertexY;
        public NativeArray<int> EdgeVertexZ;
        public NativeArray<float3> Normals;
        public NativeArray<float> CurvatureValues;
        public NativeArray<float> AmbientOcclusionValues;
        public NativeArray<float> BiomeValues;
        public NativeArray<float> SkirtAlphaValues;
        public NativeArray<float> DirtyBlendValues;
        public NativeArray<Color> Colors;
        public NativeList<CaveSpawnData> SpawnPointList;
        public string SpawnPointListNativeMemoryLabel;
        public int PartitionDimX;
        public int PartitionDimY;
        public int PartitionDimZ;
        public float3 PartitionOrigin;
        public float3 PartitionCellSize;
        public NativeArray<int> NodeBucketOffsets;
        public NativeArray<int> NodeBucketIndices;
        public NativeArray<int> TunnelBucketOffsets;
        public NativeArray<int> TunnelBucketIndices;
        public int RawCount;
        public int WeldedCount;

        public void Dispose()
        {
            ScratchLease.Dispose();
            if (ModifiedCells.IsCreated)
            {
                if (!string.IsNullOrEmpty(ModifiedCellsNativeMemoryLabel))
                    NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, ModifiedCellsNativeMemoryLabel);

                ModifiedCells.Dispose(default);
                ModifiedCells = default;
                ModifiedCellsNativeMemoryLabel = null;
            }

            HectonVoxelEngine.DisposeTrackedNativeArray(ref RawVertices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref WeldedPositions);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref TriangleIndices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref EdgeVertexX);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref EdgeVertexY);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref EdgeVertexZ);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref Normals);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref CurvatureValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref AmbientOcclusionValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref BiomeValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SkirtAlphaValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref DirtyBlendValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref Colors);
            if (SpawnPointList.IsCreated)
            {
                if (!string.IsNullOrEmpty(SpawnPointListNativeMemoryLabel))
                    NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, SpawnPointListNativeMemoryLabel);

                SpawnPointList.Dispose(default);
                SpawnPointList = default;
                SpawnPointListNativeMemoryLabel = null;
            }
            HectonVoxelEngine.DisposeTrackedNativeArray(ref NodeBucketOffsets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref NodeBucketIndices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref TunnelBucketOffsets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref TunnelBucketIndices);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    struct VoxelMeshBakeJob : IJob
    {
        public EntityId MeshId;
        public bool Convex;

        public void Execute()
        {
            UnityEngine.Physics.BakeMesh(MeshId, Convex);
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
        ActiveRuntimeInstance = this;
        EnsureVoxelBakeGhostMaterial();
        EnsureVoxelPhysicsBakeMeshPool();
        _deltaProcessor = GetComponent<VoxelDeltaProcessor>();
        if (_deltaProcessor == null)
            _deltaProcessor = gameObject.AddComponent<VoxelDeltaProcessor>();

        if (!_registeredLiveEngine)
        {
            Interlocked.Increment(ref _liveEngineCount);
            _registeredLiveEngine = true;
        }

        MCTables.Initialize();
    }

    void OnDisable()
    {
        TeardownRuntimeState();
    }

    void OnDestroy()
    {
        TeardownRuntimeState();
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
        VoxelPipelineData pipelineData = null;

        try
        {
            if (mapMagicBridge == null)
            {
                Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
                return null;
            }

            MCTables.Initialize();

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
            Vector3 absoluteUniverseOffsetAtStart = HectonFloatingOrigin.CurrentTotalOffset;
            uint shiftEpochAtStart = HectonFloatingOrigin.CurrentShiftSequence;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            CaveGenerationParams caveParams = preset.ToGenerationParams(seed);
            CaveGraphGenerator.Generate(
                seed,
                preset,
                worldCenter,
                terrainHeightCenter,
                volumeHalfExtent,
                out caveNodes,
                out caveTunnels,
                out caveEntrances,
                out caveStructures,
                Allocator.Persistent);
            RegisterTrackedNativeArray(caveNodes, nameof(caveNodes));
            RegisterTrackedNativeArray(caveTunnels, nameof(caveTunnels));
            RegisterTrackedNativeArray(caveEntrances, nameof(caveEntrances));
            RegisterTrackedNativeArray(caveStructures, nameof(caveStructures));

#if UNITY_EDITOR
            CaveGraphGenerator.Validate(caveNodes, caveTunnels, caveEntrances, worldCenter, volumeHalfExtent);
#endif

            Debug.Log(CaveGraphGenerator.GetSummary(caveNodes, caveTunnels, caveEntrances));

            pipelineData = new VoxelPipelineData
            {
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStart = absoluteUniverseOffsetAtStart,
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
                Nodes = caveNodes,
                Tunnels = caveTunnels,
                Entrances = caveEntrances,
                Structures = caveStructures,
                CraterStamps = default
            };

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return null;

            GameObject targetGO = SpawnVolume();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            targetGO.name = $"Cave_{preset.presetType}_{seed}_{worldCenter.x:F0}_{worldCenter.z:F0}";
#else
            targetGO.name = RuntimeCaveVolumeName;
#endif

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            targetGO.transform.position = stableShift.RebaseCapturedRuntimePosition(Vector3.zero, absoluteUniverseOffsetAtStart);

            if (!await ApplyVolumeMeshAsync(targetGO, pipelineData, stableShift, ct))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData postMeshShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            ConfigureVolumeRuntimeData(targetGO, seed, worldCenter, absoluteUniverseOffsetAtStart, preset, gridDim, voxelStep, clampedLodLevel, caveParams,
                caveNodes, caveTunnels, caveEntrances, caveStructures,
                pipelineData.ScratchLease.SmoothDensityField,
                pipelineData.PtsX,
                pipelineData.PtsY,
                pipelineData.PtsZ,
                (Vector3)pipelineData.VolumeOrigin,
                pipelineData.VoxelStep,
                pipelineData.BuildCollider);
            RegisterEntranceTerrainHoles(targetGO, caveEntrances, voxelStep, absoluteUniverseOffsetAtStart, postMeshShift.NewTotalOffset);
            RegisterActiveVolume(targetGO);
            RegisterPipelineSpawnPoints(worldCenter, caveParams.spawnContext, pipelineData.SpawnPointList, absoluteUniverseOffsetAtStart, postMeshShift.NewTotalOffset);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int spawnCount = pipelineData.SpawnPointList.IsCreated ? pipelineData.SpawnPointList.Length : 0;
            float reduction = (1f - (float)pipelineData.WeldedCount / pipelineData.RawCount) * 100f;
            float coverageM = gridDim * voxelStep;
            Debug.Log($"[HectonVoxel] Cave '{targetGO.name}': lod={clampedLodLevel} grid={gridDim}^3 voxel={voxelStep}m coverage={coverageM:F0}m | " +
                      $"{pipelineData.RawCount} raw -> {pipelineData.WeldedCount} welded ({reduction:F0}% reduction) | " +
                      $"{pipelineData.RawCount / 3} tris | {spawnCount} spawn points");
#endif
            return targetGO;
        }
        finally
        {
            pipelineData?.Dispose();
            DisposeTrackedNativeArray(ref caveNodes);
            DisposeTrackedNativeArray(ref caveTunnels);
            DisposeTrackedNativeArray(ref caveEntrances);
            DisposeTrackedNativeArray(ref caveStructures);
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
        Vector3 runtimeCenter = (Vector3)worldCenterAup.ToRuntimeFloat3();
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
        BeginGenerationOperation();
        long generationStartTimestamp = Stopwatch.GetTimestamp();
        VoxelPipelineData pipelineData = null;

        try
        {
            if (mapMagicBridge == null)
            {
                Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
                return null;
            }

            MCTables.Initialize();

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
            Vector3 absoluteUniverseOffsetAtStart = HectonFloatingOrigin.CurrentTotalOffset;
            uint shiftEpochAtStart = HectonFloatingOrigin.CurrentShiftSequence;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            pipelineData = new VoxelPipelineData
            {
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStart = absoluteUniverseOffsetAtStart,
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
                Seed = caveParams.seed,
                CaveParams = caveParams,
                BuildCollider = buildCollider && clampedLodLevel == 0,
                ExtractSpawnPoints = true,
                Nodes = nodes,
                Tunnels = tunnels,
                Entrances = entrances,
                Structures = structures,
                CraterStamps = default
            };

            if (RuntimeDiagnosticsTrace.IsActive)
            {
                float setupMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    $"preawait grid={gridDim} voxel={voxelStep:0.00} lod={clampedLodLevel} pts={totalPts} cells={totalCells} setup={setupMs:0.00}ms collider={buildCollider}");
            }

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return null;

            if (RuntimeDiagnosticsTrace.IsActive)
            {
                float surfaceMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    $"surface-data grid={gridDim} voxel={voxelStep:0.00} lod={clampedLodLevel} rawVerts={pipelineData.RawCount} weldedVerts={pipelineData.WeldedCount} spawnPoints={pipelineData.SpawnPointList.Length} elapsed={surfaceMs:0.00}ms");
            }

            GameObject targetGO = SpawnVolume();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            targetGO.name = $"Cave_Data_{caveParams.seed}_{worldCenter.x:F0}_{worldCenter.z:F0}";
#else
            targetGO.name = RuntimeCaveVolumeName;
#endif

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            targetGO.transform.position = stableShift.RebaseCapturedRuntimePosition(Vector3.zero, absoluteUniverseOffsetAtStart);

            if (!await ApplyVolumeMeshAsync(targetGO, pipelineData, stableShift, ct))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData postMeshShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            ConfigureVolumeRuntimeData(targetGO, caveParams.seed, worldCenter, absoluteUniverseOffsetAtStart, null, gridDim, voxelStep, clampedLodLevel, caveParams,
                nodes, tunnels, entrances, structures,
                pipelineData.ScratchLease.SmoothDensityField,
                pipelineData.PtsX,
                pipelineData.PtsY,
                pipelineData.PtsZ,
                (Vector3)pipelineData.VolumeOrigin,
                pipelineData.VoxelStep,
                pipelineData.BuildCollider);
            RegisterEntranceTerrainHoles(targetGO, entrances, voxelStep, absoluteUniverseOffsetAtStart, postMeshShift.NewTotalOffset);
            RegisterActiveVolume(targetGO);
            RegisterPipelineSpawnPoints(worldCenter, caveParams.spawnContext, pipelineData.SpawnPointList, absoluteUniverseOffsetAtStart, postMeshShift.NewTotalOffset);

            if (RuntimeDiagnosticsTrace.IsActive)
            {
                float totalMs = (float)((Stopwatch.GetTimestamp() - generationStartTimestamp) * 1000.0d / Stopwatch.Frequency);
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    $"mesh-build grid={gridDim} voxel={voxelStep:0.00} lod={clampedLodLevel} collider={buildCollider} spawnPoints={pipelineData.SpawnPointList.Length} total={totalMs:0.00}ms");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HectonVoxel] Data volume generated seed={caveParams.seed} grid={gridDim} voxel={voxelStep:F2} lod={clampedLodLevel}.");
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
        VoxelPipelineData pipelineData = null;

        try
        {
            if (volume == null || !volume.HasRuntimeData || !volume.MatchesRuntimeStamp(expectedRuntimeStamp))
                return false;

            if (mapMagicBridge == null)
            {
                Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
                return false;
            }

            MCTables.Initialize();

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            int lodLevel = math.clamp(volume.LODLevel, 0, 2);
            int gridDim = math.clamp(volume.GridDimension, 16, 128);
            float voxelStep = math.max(volume.VoxelSize, 0.25f);
            Vector3 committedTotalOffset = stableShift.NewTotalOffset;
            Vector3 worldCenter = HectonFloatingOrigin.ToRuntimePosition(volume.GenerationAbsoluteUniversePosition, committedTotalOffset);
            CaveGenerationParams caveParams = volume.CaveParams;
            float lodTransitionBand = lodLevel > 0 ? math.max(voxelStep * 1.25f, 0.5f) : 0f;
            float effectiveSealMargin = math.max(sealMargin, TerrainVoxelSeamTransitionBand) + lodTransitionBand;

            CaveNode[] nodeSnapshot = volume.Nodes;
            CaveTunnel[] tunnelSnapshot = volume.Tunnels;
            CaveEntrance[] entranceSnapshot = volume.Entrances;
            CaveStructure[] structureSnapshot = volume.Structures;
            VoxelCraterStamp[] craterSnapshot = volume.CraterStamps;
            int craterCount = volume.CraterStampCount;

            nodes = new NativeArray<CaveNode>(nodeSnapshot.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            tunnels = new NativeArray<CaveTunnel>(tunnelSnapshot.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            entrances = new NativeArray<CaveEntrance>(entranceSnapshot.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            structures = new NativeArray<CaveStructure>(structureSnapshot.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            craterStamps = new NativeArray<VoxelCraterStamp>(craterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(nodes, nameof(nodes));
            RegisterTrackedNativeArray(tunnels, nameof(tunnels));
            RegisterTrackedNativeArray(entrances, nameof(entrances));
            RegisterTrackedNativeArray(structures, nameof(structures));
            RegisterTrackedNativeArray(craterStamps, nameof(craterStamps));

            for (int i = 0; i < nodeSnapshot.Length; i++)
            {
                CaveNode node = nodeSnapshot[i];
                node.position -= (float3)committedTotalOffset;
                nodes[i] = node;
            }
            for (int i = 0; i < tunnelSnapshot.Length; i++)
            {
                CaveTunnel tunnel = tunnelSnapshot[i];
                tunnel.pointA -= (float3)committedTotalOffset;
                tunnel.pointB -= (float3)committedTotalOffset;
                tunnels[i] = tunnel;
            }
            for (int i = 0; i < entranceSnapshot.Length; i++)
            {
                CaveEntrance entrance = entranceSnapshot[i];
                entrance.surfacePosition -= (float3)committedTotalOffset;
                entrances[i] = entrance;
            }
            for (int i = 0; i < structureSnapshot.Length; i++)
            {
                CaveStructure structure = structureSnapshot[i];
                structure.position -= (float3)committedTotalOffset;
                structure.pointB -= (float3)committedTotalOffset;
                structures[i] = structure;
            }
            for (int i = 0; i < craterCount; i++)
            {
                VoxelCraterStamp crater = craterSnapshot[i];
                crater.position -= committedTotalOffset;
                craterStamps[i] = crater;
            }

            int ptsX = gridDim + 1;
            int ptsY = gridDim + 1;
            int ptsZ = gridDim + 1;
            int totalPts = ptsX * ptsY * ptsZ;
            int totalCells = gridDim * gridDim * gridDim;
            int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;
            float volumeHalfExtent = gridDim * voxelStep * 0.5f;
            float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
            float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells = default;
            string modifiedCellsNativeMemoryLabel = null;
            if (_deltaProcessor != null)
                _deltaProcessor.TryBuildDeltaMapForVolume(volume, out modifiedCells);

            if (modifiedCells.IsCreated)
            {
                modifiedCellsNativeMemoryLabel = BuildModifiedCellsNativeMemoryLabel(volume, expectedRuntimeStamp);
                NativeMemorySentinel.RegisterNativeParallelHashMap(
                    modifiedCells,
                    NativeMemoryOwner,
                    modifiedCellsNativeMemoryLabel,
                    NativeMemoryLifetime);
            }

            pipelineData = new VoxelPipelineData
            {
                SourceVolume = volume,
                SourceRuntimeStamp = expectedRuntimeStamp,
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStart = committedTotalOffset,
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
                Nodes = nodes,
                Tunnels = tunnels,
                Entrances = entrances,
                Structures = structures,
                CraterStamps = craterStamps,
                ModifiedCells = modifiedCells,
                ModifiedCellsNativeMemoryLabel = modifiedCellsNativeMemoryLabel
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
            DisposeTrackedNativeArray(ref nodes);
            DisposeTrackedNativeArray(ref tunnels);
            DisposeTrackedNativeArray(ref entrances);
            DisposeTrackedNativeArray(ref structures);
            DisposeTrackedNativeArray(ref craterStamps);
            EndGenerationOperation();
        }
    }

    void RegisterActiveVolume(GameObject volumeObject)
    {
        if (volumeObject == null)
            return;

        HectonVoxelVolume voxelVolume = null;
        volumeObject.TryGetComponent(out voxelVolume);

        Bounds localBounds = default;
        bool hasLocalBounds = false;
        if (volumeObject.TryGetComponent(out MeshFilter meshFilter) &&
            meshFilter.sharedMesh != null)
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

        _activeVolumes.Add(volumeObject);
        _activeVolumeComponents.Add(voxelVolume);
        _activeVolumeLocalBounds.Add(localBounds);
    }

    void UnregisterActiveVolume(GameObject volumeObject)
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] == volumeObject)
            {
                RemoveActiveVolumeAt(i);
                return;
            }
        }
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

        if (_activeVolumeLocalBounds.Count > last)
        {
            _activeVolumeLocalBounds[index] = _activeVolumeLocalBounds[last];
            _activeVolumeLocalBounds.RemoveAt(last);
        }
        else if (index < _activeVolumeLocalBounds.Count)
        {
            _activeVolumeLocalBounds.RemoveAt(index);
        }
    }

    /// <summary>
    /// Despawns active voxel volumes whose runtime center lies inside the supplied XZ bounds.
    /// </summary>
    internal int DespawnVolumesInsideXZ(float minX, float maxX, float minZ, float maxZ)
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

            Vector3 position = activeVolume.transform.position;
            if (position.x < minX || position.x > maxX ||
                position.z < minZ || position.z > maxZ)
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
        UnregisterActiveVolume(volume);
        HectonFloatingOrigin.MarkShiftTargetsDirty();

        var mf = volume.GetComponent<MeshFilter>();
        var mc = volume.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = null;

        ObjectPoolManager pool = GlobalRegistry.ObjectPool;
        if (pool != null && voxelVolumePrefab != null)
        {
            if (mf != null && mf.sharedMesh != null)
                mf.sharedMesh.Clear(false);
            pool.Despawn(volume);
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
                var mf = _activeVolumes[i].GetComponent<MeshFilter>();
                var mc = _activeVolumes[i].GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = null;

                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null && voxelVolumePrefab != null)
                {
                    if (mf != null && mf.sharedMesh != null)
                        mf.sharedMesh.Clear(false);
                    pool.Despawn(_activeVolumes[i]);
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
        _activeVolumeComponents.Clear();
        _activeVolumeLocalBounds.Clear();
    }

    public int ActiveVolumeCount => _activeVolumes.Count;

    internal bool TryGetNearestActiveVolume(Vector3 worldPosition, out Hecton8.Caves.HectonVoxelVolume nearestVolume)
    {
        nearestVolume = null;
        float bestSqrDistance = float.PositiveInfinity;

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

            float sqrDistance = (volume.generationPosition - worldPosition).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            nearestVolume = volume;
        }

        return nearestVolume != null;
    }

    void TeardownRuntimeState()
    {
        bool runtimeStateWasLive =
            _registeredLiveEngine ||
            ReferenceEquals(ActiveRuntimeInstance, this) ||
            _activeVolumes.Count > 0;

        if (!Application.isPlaying && !runtimeStateWasLive)
            return;

        ClearAllVolumes();
        _teardownStreamingScratchRequested = true;
        TryFinalizeStreamingScratchTeardown();

        if (ReferenceEquals(ActiveRuntimeInstance, this))
            ActiveRuntimeInstance = null;

        if (_registeredLiveEngine)
        {
            _registeredLiveEngine = false;
            if (Interlocked.Decrement(ref _liveEngineCount) <= 0)
                RequestSharedTableShutdown();
        }

        if (_runtimeVoxelBakeGhostMaterial != null)
        {
            SafeDestroy(_runtimeVoxelBakeGhostMaterial);
            _runtimeVoxelBakeGhostMaterial = null;
        }
    }

    void EnsureVoxelBakeGhostMaterial()
    {
        if (voxelBakeGhostMaterial != null || _runtimeVoxelBakeGhostMaterial != null)
            return;

        Shader ghostShader = Shader.Find(DefaultVoxelBakeGhostShaderName);
        if (ghostShader == null)
            return;

        // COLD ALLOC: Material[1] - runtime fallback voxel bake ghost material when serialized scene reference is absent - owner: HectonVoxelEngine
        _runtimeVoxelBakeGhostMaterial = new Material(ghostShader)
        {
            name = "Runtime_VoxelBakeGhost"
        };
        _runtimeVoxelBakeGhostMaterial.SetColor("_BaseColor", new Color(0.045f, 0.068f, 0.082f, 1f));
        _runtimeVoxelBakeGhostMaterial.SetColor("_EdgeColor", new Color(0.16f, 0.38f, 0.46f, 1f));
        _runtimeVoxelBakeGhostMaterial.SetColor("_EmissionColor", new Color(0f, 0.16f, 0.22f, 1f));
        _runtimeVoxelBakeGhostMaterial.SetFloat("_Opacity", 0.42f);
        _runtimeVoxelBakeGhostMaterial.SetFloat("_InstabilityScale", 1.4f);
        _runtimeVoxelBakeGhostMaterial.SetFloat("_InstabilitySpeed", 1.25f);
        _runtimeVoxelBakeGhostMaterial.SetFloat("_InstabilityStrength", 0.28f);
        _runtimeVoxelBakeGhostMaterial.SetFloat("_FresnelPower", 2.3f);
    }

    static async Awaitable AwaitForJobCompletionAsync(JobHandle handle, CancellationToken ct, string context)
    {
        int waitFrames = 0;
        try
        {
            while (!handle.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();
                if (waitFrames >= VoxelJobWaitWatchdogFrames)
                {
                    LogVoxelJobWaitWatchdog(context, waitFrames);
                    break;
                }

                waitFrames++;
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }
        }
        finally
        {
            DispatcherJobSwap.TryComplete(ref handle, true);
        }
    }

    static async Awaitable<bool> AwaitForPhysicsBakeCompletionOrDeferAsync(
        JobHandle handle,
        CancellationToken ct,
        string context,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags)
    {
        int waitFrames = 0;
        while (!handle.IsCompleted)
        {
            if (ct.IsCancellationRequested)
            {
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags);
                return false;
            }

            if (waitFrames >= VoxelJobWaitWatchdogFrames)
            {
                LogVoxelJobWaitWatchdog(context, waitFrames);
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags);
                return false;
            }

            waitFrames++;
            try
            {
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags);
                return false;
            }
        }

        return DispatcherJobSwap.TryComplete(ref handle, forceComplete: false);
    }

    private static void EnqueueDeferredVoxelPhysicsBakeTeardown(
        JobHandle handle,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags)
    {
        DisableDeferredVoxelBakePresentation(owner, renderer, collider);
        _deferredVoxelPhysicsBakeTeardowns.Add(new DeferredVoxelPhysicsBakeTeardown
        {
            Mesh = mesh,
            Owner = owner,
            Renderer = renderer,
            Collider = collider,
            Handle = handle,
            Flags = flags
        });

        EnsureDeferredVoxelPhysicsBakeTeardownRegistered();
        UpdateDeferredVoxelPhysicsBakeBackpressure();
    }

    private static void DisableDeferredVoxelBakePresentation(GameObject owner, MeshRenderer renderer, MeshCollider collider)
    {
        if (renderer == null && owner != null)
            owner.TryGetComponent(out renderer);

        if (renderer != null)
            renderer.enabled = false;

        if (collider != null)
        {
            collider.enabled = false;
            collider.sharedMesh = null;
        }
    }

    private static void EnsureDeferredVoxelPhysicsBakeTeardownRegistered()
    {
        if (_deferredVoxelPhysicsBakeTeardownRegistered ||
            !Application.isPlaying ||
            GlobalRegistry.Dispatcher == null)
        {
            return;
        }

        GlobalRegistry.RegisterLateFrameTickable(_deferredVoxelPhysicsBakeTeardownDriver, PriorityLayer.Environment);
        _deferredVoxelPhysicsBakeTeardownRegistered = SystemDispatcher
            .GetLateFrameLane(PriorityLayer.Environment)
            .Contains(_deferredVoxelPhysicsBakeTeardownDriver);
    }

    private static void DrainDeferredVoxelPhysicsBakeTeardowns()
    {
        int pendingCount = _deferredVoxelPhysicsBakeTeardowns.Count;
        if (pendingCount <= 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            UpdateDeferredVoxelPhysicsBakeBackpressure();
            return;
        }

        int drainBudget = _deferredVoxelPhysicsBakeBackpressureActive
            ? DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget
            : DeferredVoxelPhysicsBakeTeardownDrainBudget;
        int inspectionBudget = _deferredVoxelPhysicsBakeBackpressureActive
            ? DeferredVoxelPhysicsBakeTeardownBackpressureInspectionBudget
            : DeferredVoxelPhysicsBakeTeardownInspectionBudget;
        if (inspectionBudget > pendingCount)
            inspectionBudget = pendingCount;

        if (_deferredVoxelPhysicsBakeTeardownScanCursor < 0 ||
            _deferredVoxelPhysicsBakeTeardownScanCursor >= pendingCount)
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
            if (!DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false))
            {
                index--;
                continue;
            }

            if (pending.Collider != null)
            {
                pending.Collider.enabled = false;
                if (pending.Collider.sharedMesh == pending.Mesh)
                    pending.Collider.sharedMesh = null;
            }

            if (pending.Mesh != null)
            {
                pending.Mesh.Clear(false);
                if (!ReleaseVoxelPhysicsBakeMesh(pending.Mesh))
                    DestroyDeferredVoxelObject(pending.Mesh);
            }

            if ((pending.Flags & DeferredVoxelBakeDestroyOwner) != 0 && pending.Owner != null)
                DestroyDeferredVoxelObject(pending.Owner);

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

        _deferredVoxelPhysicsBakeTeardownScanCursor = pendingCount > 0 ? index : 0;
        if (_deferredVoxelPhysicsBakeTeardowns.Count == 0)
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();

        UpdateDeferredVoxelPhysicsBakeBackpressure();
    }

    private static void RemoveDeferredVoxelPhysicsBakeTeardownAt(int index)
    {
        int lastIndex = _deferredVoxelPhysicsBakeTeardowns.Count - 1;
        if (index != lastIndex)
            _deferredVoxelPhysicsBakeTeardowns[index] = _deferredVoxelPhysicsBakeTeardowns[lastIndex];

        _deferredVoxelPhysicsBakeTeardowns.RemoveAt(lastIndex);
    }

    private static void UnregisterDeferredVoxelPhysicsBakeTeardownDriver()
    {
        if (!_deferredVoxelPhysicsBakeTeardownRegistered)
            return;

        GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelPhysicsBakeTeardownDriver, PriorityLayer.Environment);
        _deferredVoxelPhysicsBakeTeardownRegistered = false;
    }

    private static void UpdateDeferredVoxelPhysicsBakeBackpressure()
    {
        int pendingCount = _deferredVoxelPhysicsBakeTeardowns.Count;
        bool nextActive = ResolveDeferredVoxelPhysicsBakeBackpressureState(
            pendingCount,
            _deferredVoxelPhysicsBakeBackpressureActive);

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
        return ResolveDistanceBasedVoxelLodLevel(worldCenter, observerPosition);
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
        if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            return ResolveDistanceBasedVoxelLodLevel(worldCenter, playerTransform.position);

        Transform bootstrapPlayer = BootstrapState.CurrentPlayerTransform;
        return bootstrapPlayer != null
            ? ResolveDistanceBasedVoxelLodLevel(worldCenter, bootstrapPlayer.position)
            : 0;
    }

    private static int ResolveDistanceBasedVoxelLodLevel(Vector3 worldCenter, Vector3 observerPosition)
    {
        return VoxelRuntimeIntegrityUtility.ResolveDistanceBasedLodLevel(
            worldCenter,
            observerPosition,
            VoxelLodColliderDisableDistanceMeters);
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

    private static void EnsureVoxelPhysicsBakeMeshPool()
    {
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (_voxelPhysicsBakeMeshPool[i] != null)
                continue;

            Mesh mesh = new Mesh
            {
                name = VoxelPhysicsBakePoolMeshName
            };
            mesh.MarkDynamic();
            _voxelPhysicsBakeMeshPool[i] = mesh;
        }
    }

    internal static Mesh AcquireVoxelPhysicsBakeMesh(string ownerName, int chunkIndex)
    {
        EnsureVoxelPhysicsBakeMeshPool();
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (_voxelPhysicsBakeMeshPoolInUse[i])
                continue;

            Mesh mesh = _voxelPhysicsBakeMeshPool[i];
            if (mesh == null)
                continue;

            _voxelPhysicsBakeMeshPoolInUse[i] = true;
            mesh.Clear(false);
            return mesh;
        }

        if (!_voxelPhysicsBakeMeshPoolExhaustedWarningArmed)
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

        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (!ReferenceEquals(_voxelPhysicsBakeMeshPool[i], mesh))
                continue;

            mesh.Clear(false);
            _voxelPhysicsBakeMeshPoolInUse[i] = false;
            _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
            return true;
        }

        return false;
    }

    static void LogVoxelJobWaitWatchdog(string context, int waitFrames)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"[HectonVoxel] Job wait watchdog tripped. Context={context}. Frames={waitFrames}. Cleanup barrier required.");
#endif
    }

    async Awaitable<bool> ExecuteVoxelPipelineAsync(VoxelPipelineData data, CancellationToken ct)
    {
        BuildSpatialPartitions(data);
        data.ScratchLease = await AcquireStreamingScratchLeaseAsync(data.PtsX * data.PtsZ, data.TotalPts, data.TotalCells, ct);
        if (!data.ScratchLease.IsValid)
            return false;

        NativeArray<float> terrainHeights = data.ScratchLease.TerrainHeights;
        NativeArray<float> gridBiome = data.ScratchLease.GridBiome;
        NativeArray<float> densityField = data.ScratchLease.DensityField;
        NativeArray<float> smoothDensityField = data.ScratchLease.SmoothDensityField;
        NativeArray<int> chunkContentFlags = data.ScratchLease.ChunkContentFlags;
        NativeArray<int> cellVertexCounts = data.ScratchLease.CellVertexCounts;
        NativeArray<int> cellVertexOffsets = data.ScratchLease.CellVertexOffsets;

        float fallbackHeight = data.TerrainHeightCenter;
        bool sampledHeightGrid = false;
        Vector3 absoluteGridOrigin = new Vector3(data.VolumeOrigin.x, 0f, data.VolumeOrigin.z) + data.AbsoluteUniverseOffsetAtStart;
        HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        if (vegetationBridge != null)
        {
            sampledHeightGrid = vegetationBridge.TryFillTerrainHeightGridFromNativeCacheAUP(
                absoluteGridOrigin,
                data.PtsX,
                data.PtsZ,
                data.VoxelStep,
                terrainHeights,
                fallbackHeight);
        }

        if (!sampledHeightGrid)
        {
            for (int iz = 0; iz < data.PtsZ; iz++)
            for (int ix = 0; ix < data.PtsX; ix++)
            {
                float wx = data.VolumeOrigin.x + ix * data.VoxelStep;
                float wz = data.VolumeOrigin.z + iz * data.VoxelStep;
                int hi = ix + iz * data.PtsX;

                Vector3 absoluteSamplePosition = new Vector3(wx, 0f, wz) + data.AbsoluteUniverseOffsetAtStart;
                if (mapMagicBridge.TryGetHeightAUP(absoluteSamplePosition, out float sampledHeight))
                    terrainHeights[hi] = sampledHeight;
                else
                    terrainHeights[hi] = fallbackHeight;
            }
        }

        for (int i = 0; i < gridBiome.Length; i++)
            gridBiome[i] = 0f;

        ct.ThrowIfCancellationRequested();
        NativeArray<byte> navGridBackBuffer = default;
        NativeArray<byte> navGridBasePassabilityBuffer = default;
        NativeArray<ushort> navGridDistanceMap = default;
        NativeArray<int> navGridPureVoidBlockFlags = default;
        NativeArray<VoxelDynamicNavGridRuntime.NavObstaclePrimitive> navObstacleSnapshot = default;
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
            caveNodes = data.Nodes,
            caveTunnels = data.Tunnels,
            caveEntrances = data.Entrances,
            caveStructures = data.Structures,
            craterStamps = data.CraterStamps,
            modifiedCells = data.ModifiedCells,
            nodeBucketOffsets = data.NodeBucketOffsets,
            nodeBucketIndices = data.NodeBucketIndices,
            tunnelBucketOffsets = data.TunnelBucketOffsets,
            tunnelBucketIndices = data.TunnelBucketIndices,
            caveParams = data.CaveParams,
            absoluteNoiseOffset = (float3)data.AbsoluteUniverseOffsetAtStart,
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
            density = densityField,
            smoothDensity = smoothDensityField
        }.Schedule(data.TotalPts, JOB_BATCH);

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
        ct.ThrowIfCancellationRequested();
        if (chunkContentFlags[0] == 0)
        {
            data.RawCount = 0;
            return false;
        }

        densityHandle = chunkContentHandle;

        if (data.SourceVolume != null &&
            VoxelDynamicNavGridRuntime.TryPrepareBuild(
                data.SourceVolume,
                data.SourceRuntimeStamp,
                new int3(data.PtsX, data.PtsY, data.PtsZ),
                data.VolumeOrigin,
                data.VoxelStep,
                data.TotalPts,
                out navGridBackBuffer,
                out navGridBasePassabilityBuffer,
                out navGridDistanceMap,
                out navGridPureVoidBlockFlags))
        {
            JobHandle navPassabilityHandle = new VoxelDynamicNavGridRuntime.PassabilityBuildJob
            {
                DensityField = densityField,
                Output = navGridBackBuffer,
                SolidThreshold = 0f
            }.Schedule(data.TotalPts, JOB_BATCH, densityHandle);
            JobHandle navBaseCopyHandle = new VoxelDynamicNavGridRuntime.CopyByteBufferJob
            {
                Source = navGridBackBuffer,
                Destination = navGridBasePassabilityBuffer
            }.Schedule(data.TotalPts, JOB_BATCH, navPassabilityHandle);
            navObstacleSnapshot = VoxelDynamicNavGridRuntime.CreateObstacleSnapshot(Allocator.TempJob);
            JobHandle obstacleHandle = navPassabilityHandle;
            if (navObstacleSnapshot.IsCreated)
            {
                obstacleHandle = new VoxelDynamicNavGridRuntime.ObstacleStampJob
                {
                    Passability = navGridBackBuffer,
                    Obstacles = navObstacleSnapshot,
                    Dimensions = new int3(data.PtsX, data.PtsY, data.PtsZ),
                    Origin = data.VolumeOrigin,
                    CellSize = data.VoxelStep
                }.Schedule(data.TotalPts, JOB_BATCH, navPassabilityHandle);
            }

            navGridHandle = new VoxelDynamicNavGridRuntime.ClearanceDilationJob
            {
                Passability = navGridBackBuffer,
                DistanceMap = navGridDistanceMap,
                Dimensions = new int3(data.PtsX, data.PtsY, data.PtsZ),
                AgentRadiusCells = VoxelDynamicNavGridRuntime.ResolveClearanceRadiusCells(data.VoxelStep)
            }.Schedule(obstacleHandle);
            navGridHandle = JobHandle.CombineDependencies(navGridHandle, navBaseCopyHandle);
            navGridHandle = VoxelDynamicNavGridRuntime.SchedulePureVoidScan(
                navGridBackBuffer,
                navGridDistanceMap,
                navGridPureVoidBlockFlags,
                data.TotalPts,
                navGridHandle);
            navGridScheduled = true;
        }

        JobHandle mcCountHandle = new VoxelMCCountJob
        {
            cellsX = data.GridDimension,
            cellsY = data.GridDimension,
            cellsZ = data.GridDimension,
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            density = densityField,
            edgeTable = MCTables.EdgeTable,
            triTable = MCTables.TriTable,
            cellVertexCounts = cellVertexCounts
        }.Schedule(data.TotalCells, JOB_BATCH, densityHandle);

        JobHandle firstPhaseHandle = navGridScheduled
            ? JobHandle.CombineDependencies(mcCountHandle, navGridHandle)
            : mcCountHandle;
        try
        {
            await AwaitForJobCompletionAsync(firstPhaseHandle, ct, "density/count phase");
        }
        catch
        {
            if (navObstacleSnapshot.IsCreated)
            {
                navObstacleSnapshot.Dispose(firstPhaseHandle);
                navObstacleSnapshot = default;
            }

            throw;
        }

        if (navObstacleSnapshot.IsCreated)
        {
            navObstacleSnapshot.Dispose();
            navObstacleSnapshot = default;
        }
        if (navGridScheduled)
            VoxelDynamicNavGridRuntime.CommitBuild(data.SourceVolume, data.SourceRuntimeStamp);

        int exactRawVertexCount = 0;
        for (int cellIndex = 0; cellIndex < data.TotalCells; cellIndex++)
        {
            cellVertexOffsets[cellIndex] = exactRawVertexCount;
            exactRawVertexCount += cellVertexCounts[cellIndex];
        }

        data.RawCount = exactRawVertexCount;
        if (data.RawCount < 3)
            return false;

        // COLD ALLOC: NativeArray<MCRawVertex>[data.RawCount] - exact marching-cubes output buffer after count pass - owner: HectonVoxelEngine
        data.RawVertices = new NativeArray<MCRawVertex>(data.RawCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        // COLD ALLOC: NativeArray<float3>[data.RawCount] - worst-case welded vertex storage sized to exact raw extraction count - owner: HectonVoxelEngine
        data.WeldedPositions = new NativeArray<float3>(data.RawCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        // COLD ALLOC: NativeArray<int>[data.RawCount] - exact triangle index buffer mapped from raw MC vertices - owner: HectonVoxelEngine
        data.TriangleIndices = new NativeArray<int>(data.RawCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        int edgeVertexCountX = data.GridDimension * data.PtsY * data.PtsZ;
        int edgeVertexCountY = data.PtsX * data.GridDimension * data.PtsZ;
        int edgeVertexCountZ = data.PtsX * data.PtsY * data.GridDimension;
        // COLD ALLOC: NativeArray<int>[edgeVertexCountX] - deterministic X-edge weld ownership registry - owner: HectonVoxelEngine
        data.EdgeVertexX = new NativeArray<int>(edgeVertexCountX, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        // COLD ALLOC: NativeArray<int>[edgeVertexCountY] - deterministic Y-edge weld ownership registry - owner: HectonVoxelEngine
        data.EdgeVertexY = new NativeArray<int>(edgeVertexCountY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        // COLD ALLOC: NativeArray<int>[edgeVertexCountZ] - deterministic Z-edge weld ownership registry - owner: HectonVoxelEngine
        data.EdgeVertexZ = new NativeArray<int>(edgeVertexCountZ, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        RegisterTrackedNativeArray(data.RawVertices, nameof(data.RawVertices));
        RegisterTrackedNativeArray(data.WeldedPositions, nameof(data.WeldedPositions));
        RegisterTrackedNativeArray(data.TriangleIndices, nameof(data.TriangleIndices));
        RegisterTrackedNativeArray(data.EdgeVertexX, nameof(data.EdgeVertexX));
        RegisterTrackedNativeArray(data.EdgeVertexY, nameof(data.EdgeVertexY));
        RegisterTrackedNativeArray(data.EdgeVertexZ, nameof(data.EdgeVertexZ));

        for (int edgeIndex = 0; edgeIndex < data.EdgeVertexX.Length; edgeIndex++)
            data.EdgeVertexX[edgeIndex] = -1;

        for (int edgeIndex = 0; edgeIndex < data.EdgeVertexY.Length; edgeIndex++)
            data.EdgeVertexY[edgeIndex] = -1;

        for (int edgeIndex = 0; edgeIndex < data.EdgeVertexZ.Length; edgeIndex++)
            data.EdgeVertexZ[edgeIndex] = -1;

        JobHandle mcHandle = new VoxelMCExtractJob
        {
            cellsX = data.GridDimension,
            cellsY = data.GridDimension,
            cellsZ = data.GridDimension,
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            density = densityField,
            edgeTable = MCTables.EdgeTable,
            triTable = MCTables.TriTable,
            cellVertexOffsets = cellVertexOffsets,
            cellVertexCounts = cellVertexCounts,
            outVertices = data.RawVertices
        }.Schedule(data.TotalCells, JOB_BATCH);

        await AwaitForJobCompletionAsync(mcHandle, ct, "marching-cubes extract");

        ct.ThrowIfCancellationRequested();

        // COLD ALLOC: NativeArray<int>[1] - exact welded vertex counter for current voxel build only - owner: HectonVoxelEngine
        NativeArray<int> weldedCounter = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        RegisterTrackedNativeArray(weldedCounter, nameof(weldedCounter));

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
                weldedCounter = weldedCounter
            }.Schedule();

            await AwaitForJobCompletionAsync(weldHandle, ct, "vertex weld");

            data.WeldedCount = weldedCounter[0];
        }
        finally
        {
            DisposeTrackedNativeArray(ref weldedCounter);
        }

        if (data.WeldedCount < 3)
            return false;

        ct.ThrowIfCancellationRequested();

        data.Normals = new NativeArray<float3>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        data.CurvatureValues = new NativeArray<float>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        data.AmbientOcclusionValues = new NativeArray<float>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        data.BiomeValues = new NativeArray<float>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        data.SkirtAlphaValues = new NativeArray<float>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        data.DirtyBlendValues = new NativeArray<float>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        data.Colors = new NativeArray<Color>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        RegisterTrackedNativeArray(data.Normals, nameof(data.Normals));
        RegisterTrackedNativeArray(data.CurvatureValues, nameof(data.CurvatureValues));
        RegisterTrackedNativeArray(data.AmbientOcclusionValues, nameof(data.AmbientOcclusionValues));
        RegisterTrackedNativeArray(data.BiomeValues, nameof(data.BiomeValues));
        RegisterTrackedNativeArray(data.SkirtAlphaValues, nameof(data.SkirtAlphaValues));
        RegisterTrackedNativeArray(data.DirtyBlendValues, nameof(data.DirtyBlendValues));
        RegisterTrackedNativeArray(data.Colors, nameof(data.Colors));
        if (data.ExtractSpawnPoints)
        {
            int maxSpawnPoints = math.max(data.WeldedCount / 20, 64);
            data.SpawnPointList = new NativeList<CaveSpawnData>(maxSpawnPoints, Allocator.Persistent);
            data.SpawnPointListNativeMemoryLabel = BuildSpawnPointListNativeMemoryLabel(
                data.SourceVolume,
                data.SourceRuntimeStamp);
            NativeMemorySentinel.RegisterNativeList(
                data.SpawnPointList,
                NativeMemoryOwner,
                data.SpawnPointListNativeMemoryLabel,
                NativeMemoryLifetime);
        }

        JobHandle seamSnapHandle = new VoxelTerrainSeamSnapJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            absoluteUniverseOffset = (float3)data.AbsoluteUniverseOffsetAtStart,
            voxelStep = data.VoxelStep,
            seamTransitionBand = TerrainVoxelSeamTransitionBand,
            seamOverlap = VoxelSeamDirector.TerrainOverlapMeters,
            terrainHeights = terrainHeights,
            positions = data.WeldedPositions
        }.Schedule(data.WeldedCount, JOB_BATCH);

        JobHandle normalHandle = new VoxelNormalJob
        {
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            densityField = densityField,
            smoothDensityField = smoothDensityField,
            positions = data.WeldedPositions,
            normals = data.Normals,
            curvatureValues = data.CurvatureValues,
            ambientOcclusionValues = data.AmbientOcclusionValues
        }.Schedule(data.WeldedCount, JOB_BATCH, seamSnapHandle);

        JobHandle seamNormalHandle = new VoxelSeamNormalBlendJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            absoluteUniverseOffset = (float3)data.AbsoluteUniverseOffsetAtStart,
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
        }.Schedule(data.WeldedCount, JOB_BATCH);

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
            biomeValues = data.BiomeValues,
            caveEntrances = data.Entrances,
            colors = data.Colors,
            skirtAlphaValues = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH, colorDeps);

        JobHandle phase5Handle = colorHandle;
        if (data.ModifiedCells.IsCreated)
        {
            JobHandle dirtyBlendHandle = new VoxelDirtyBlendJob
            {
                positions = data.WeldedPositions,
                modifiedCells = data.ModifiedCells,
                voxelStep = data.VoxelStep,
                absoluteUniverseOffset = (float3)data.AbsoluteUniverseOffsetAtStart,
                dirtyBlendValues = data.DirtyBlendValues
            }.Schedule(data.WeldedCount, JOB_BATCH, seamSnapHandle);

            phase5Handle = JobHandle.CombineDependencies(phase5Handle, dirtyBlendHandle);
        }

        if (data.ExtractSpawnPoints)
        {
            JobHandle spawnHandle = new VoxelSpawnPointJob
            {
                positions = data.WeldedPositions,
                normals = data.Normals,
                volumeCenter = data.WorldCenter,
                volumeHalfExtent = data.VolumeHalfExtent,
                floorNormalThreshold = 0.75f,
                minInteriorDepth = 0.15f,
                keepFraction = 0.03f,
                seed = data.Seed,
                spawnPoints = data.SpawnPointList.AsParallelWriter()
            }.Schedule(data.WeldedCount, JOB_BATCH, normalHandle);

            phase5Handle = JobHandle.CombineDependencies(phase5Handle, spawnHandle);
        }

        await AwaitForJobCompletionAsync(phase5Handle, ct, "normal/color/spawn phase");
        ct.ThrowIfCancellationRequested();
        return true;
    }

    async Awaitable<VoxelStreamingScratchLease> AcquireStreamingScratchLeaseAsync(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        CancellationToken ct)
    {
        int waitFrames = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (TryAcquireStreamingScratchLease(heightCount, totalPointCount, totalCellCount, out VoxelStreamingScratchLease lease))
                return lease;

            if (waitFrames >= StreamingScratchLeaseTimeoutFrames)
            {
                LogStreamingScratchLeaseTimeout(heightCount, totalPointCount, totalCellCount, waitFrames);
                return default;
            }

            waitFrames++;
            await Awaitable.NextFrameAsync(ct);
        }
    }

    void LogStreamingScratchLeaseTimeout(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int waitFrames)
    {
        GetStreamingScratchLeaseState(out int slotCount, out int inUseCount, out bool teardownRequested);
        int activeGenerationOperations = Volatile.Read(ref _activeGenerationOperations);
        Debug.LogError($"[HectonVoxel] Streaming scratch lease timed out after {waitFrames} frames. slots={slotCount} inUse={inUseCount} teardown={teardownRequested} activeOps={activeGenerationOperations} height={heightCount} points={totalPointCount} cells={totalCellCount}.");
    }

    void GetStreamingScratchLeaseState(out int slotCount, out int inUseCount, out bool teardownRequested)
    {
        lock (_streamingScratchGate)
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
        out VoxelStreamingScratchLease lease)
    {
        lease = default;
        lock (_streamingScratchGate)
        {
            EnsureStreamingScratchSlots();
            if (_streamingScratchSlots == null || _streamingScratchSlots.Length == 0)
                return false;

            for (int i = 0; i < _streamingScratchSlots.Length; i++)
            {
                VoxelStreamingScratchSlot slot = _streamingScratchSlots[i];
                if (slot == null || slot.InUse)
                    continue;

                slot.InUse = true;
                EnsureStreamingScratchSlotCapacity(slot, heightCount, totalPointCount, totalCellCount);

                lease = new VoxelStreamingScratchLease(
                    this,
                    i,
                    slot.TerrainHeights,
                    slot.GridBiome,
                    slot.DensityField,
                    slot.SmoothDensityField,
                    slot.ChunkContentFlags,
                    slot.CellVertexCounts,
                    slot.CellVertexOffsets);
                return true;
            }
        }

        return false;
    }

    void ReleaseStreamingScratchLease(int slotIndex)
    {
        lock (_streamingScratchGate)
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
        int slotCount = Mathf.Clamp(streamingScratchSlotCount, 1, 8);
        if (_streamingScratchSlots != null && _streamingScratchSlots.Length == slotCount)
            return;

        DisposeStreamingScratchSlots();
        _streamingScratchSlots = new VoxelStreamingScratchSlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            _streamingScratchSlots[i] = new VoxelStreamingScratchSlot();
    }

    void DisposeStreamingScratchSlots()
    {
        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null)
                return;

            for (int i = 0; i < _streamingScratchSlots.Length; i++)
                _streamingScratchSlots[i]?.Dispose();

            _streamingScratchSlots = null;
        }
    }

    void TryFinalizeStreamingScratchTeardown()
    {
        lock (_streamingScratchGate)
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

        for (int i = 0; i < _streamingScratchSlots.Length; i++)
            _streamingScratchSlots[i]?.Dispose();

        _streamingScratchSlots = null;
        _teardownStreamingScratchRequested = false;
    }

    static void EnsureStreamingScratchSlotCapacity(
        VoxelStreamingScratchSlot slot,
        int heightCount,
        int totalPointCount,
        int totalCellCount)
    {
        EnsureNativeArrayCapacity(ref slot.TerrainHeights, heightCount, nameof(VoxelStreamingScratchSlot.TerrainHeights));
        EnsureNativeArrayCapacity(ref slot.GridBiome, heightCount, nameof(VoxelStreamingScratchSlot.GridBiome));
        EnsureNativeArrayCapacity(ref slot.DensityField, totalPointCount, nameof(VoxelStreamingScratchSlot.DensityField));
        EnsureNativeArrayCapacity(ref slot.SmoothDensityField, totalPointCount, nameof(VoxelStreamingScratchSlot.SmoothDensityField));
        EnsureNativeArrayCapacity(ref slot.ChunkContentFlags, 1, nameof(VoxelStreamingScratchSlot.ChunkContentFlags), true);
        EnsureNativeArrayCapacity(ref slot.CellVertexCounts, totalCellCount, nameof(VoxelStreamingScratchSlot.CellVertexCounts));
        EnsureNativeArrayCapacity(ref slot.CellVertexOffsets, totalCellCount, nameof(VoxelStreamingScratchSlot.CellVertexOffsets));
    }

    static void EnsureNativeArrayCapacity<T>(ref NativeArray<T> array, int requiredLength, string label, bool clear = false)
        where T : struct
    {
        if (requiredLength <= 0)
            requiredLength = 1;

        if (array.IsCreated && array.Length >= requiredLength)
        {
            if (clear)
                array[0] = default;

            return;
        }

        if (array.IsCreated)
        {
            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(default);
        }

        NativeArrayOptions options = clear ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory;
        // COLD ALLOC: NativeArray<T>[requiredLength] - reusable voxel streaming scratch slot growth - owner: HectonVoxelEngine
        array = new NativeArray<T>(requiredLength, Allocator.Persistent, options);
        RegisterTrackedNativeArray(array, label);
    }

    static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
    {
        NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
    }

    static string BuildModifiedCellsNativeMemoryLabel(HectonVoxelVolume volume, int runtimeStamp)
    {
        EntityId volumeId = volume != null ? volume.GetEntityId() : default;
        return ModifiedCellsNativeMemoryLabelPrefix + volumeId + ":" + runtimeStamp;
    }

    static string BuildSpawnPointListNativeMemoryLabel(HectonVoxelVolume volume, int runtimeStamp)
    {
        EntityId volumeId = volume != null ? volume.GetEntityId() : default;
        return SpawnPointListNativeMemoryLabelPrefix + volumeId + ":" + runtimeStamp;
    }

    static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
    {
        if (!array.IsCreated)
            return;

        NativeMemorySentinel.UnregisterNativeArray(array);
        array.Dispose(default);
        array = default;
    }

    void BuildSpatialPartitions(VoxelPipelineData data)
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

            BuildNodeSpatialBuckets(data);
            BuildTunnelSpatialBuckets(data);
        }
    }

    void BuildNodeSpatialBuckets(VoxelPipelineData data)
    {
        if (!data.Nodes.IsCreated || data.Nodes.Length == 0)
            return;

        int bucketCount = data.PartitionDimX * data.PartitionDimY * data.PartitionDimZ;
        NativeArray<int> bucketCounts = new NativeArray<int>(bucketCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        NativeArray<int> writeHeads = default;

        try
        {
            for (int nodeIndex = 0; nodeIndex < data.Nodes.Length; nodeIndex++)
            {
                CaveNode node = data.Nodes[nodeIndex];
                float maxRadius = math.cmax(node.radii);
                float inflation = math.max(node.noiseAmplitude, 0f) + data.CaveParams.warpAmplitude + data.CaveParams.noiseEvalDistance + node.blendRadius + (data.VoxelStep * 2f);
                float3 boundsMin = node.position - new float3(maxRadius + inflation);
                float3 boundsMax = node.position + new float3(maxRadius + inflation);

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                    bucketCounts[FlattenPartitionIndex(data, x, y, z)]++;
            }

            data.NodeBucketOffsets = new NativeArray<int>(bucketCount + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(data.NodeBucketOffsets, nameof(data.NodeBucketOffsets));
            int totalReferences = 0;
            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                data.NodeBucketOffsets[bucketIndex] = totalReferences;
                totalReferences += bucketCounts[bucketIndex];
            }

            data.NodeBucketOffsets[bucketCount] = totalReferences;
            if (totalReferences <= 0)
                return;

            data.NodeBucketIndices = new NativeArray<int>(totalReferences, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(data.NodeBucketIndices, nameof(data.NodeBucketIndices));
            writeHeads = new NativeArray<int>(bucketCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
                writeHeads[bucketIndex] = data.NodeBucketOffsets[bucketIndex];

            for (int nodeIndex = 0; nodeIndex < data.Nodes.Length; nodeIndex++)
            {
                CaveNode node = data.Nodes[nodeIndex];
                float maxRadius = math.cmax(node.radii);
                float inflation = math.max(node.noiseAmplitude, 0f) + data.CaveParams.warpAmplitude + data.CaveParams.noiseEvalDistance + node.blendRadius + (data.VoxelStep * 2f);
                float3 boundsMin = node.position - new float3(maxRadius + inflation);
                float3 boundsMax = node.position + new float3(maxRadius + inflation);

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int bucketIndex = FlattenPartitionIndex(data, x, y, z);
                    int writeIndex = writeHeads[bucketIndex];
                    data.NodeBucketIndices[writeIndex] = nodeIndex;
                    writeHeads[bucketIndex] = writeIndex + 1;
                }
            }
        }
        finally
        {
            if (bucketCounts.IsCreated) bucketCounts.Dispose();
            if (writeHeads.IsCreated) writeHeads.Dispose();
        }
    }

    void BuildTunnelSpatialBuckets(VoxelPipelineData data)
    {
        if (!data.Tunnels.IsCreated || data.Tunnels.Length == 0)
            return;

        int bucketCount = data.PartitionDimX * data.PartitionDimY * data.PartitionDimZ;
        NativeArray<int> bucketCounts = new NativeArray<int>(bucketCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        NativeArray<int> writeHeads = default;

        try
        {
            for (int tunnelIndex = 0; tunnelIndex < data.Tunnels.Length; tunnelIndex++)
            {
                CaveTunnel tunnel = data.Tunnels[tunnelIndex];
                float maxRadius = math.max(tunnel.radiusA, tunnel.radiusB);
                float inflation = maxRadius + tunnel.blendRadius + data.CaveParams.warpAmplitude + tunnel.warpAmount + data.CaveParams.noiseEvalDistance + (data.VoxelStep * 2f);
                float3 boundsMin = math.min(tunnel.pointA, tunnel.pointB) - new float3(inflation);
                float3 boundsMax = math.max(tunnel.pointA, tunnel.pointB) + new float3(inflation);

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                    bucketCounts[FlattenPartitionIndex(data, x, y, z)]++;
            }

            data.TunnelBucketOffsets = new NativeArray<int>(bucketCount + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(data.TunnelBucketOffsets, nameof(data.TunnelBucketOffsets));
            int totalReferences = 0;
            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                data.TunnelBucketOffsets[bucketIndex] = totalReferences;
                totalReferences += bucketCounts[bucketIndex];
            }

            data.TunnelBucketOffsets[bucketCount] = totalReferences;
            if (totalReferences <= 0)
                return;

            data.TunnelBucketIndices = new NativeArray<int>(totalReferences, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(data.TunnelBucketIndices, nameof(data.TunnelBucketIndices));
            writeHeads = new NativeArray<int>(bucketCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
                writeHeads[bucketIndex] = data.TunnelBucketOffsets[bucketIndex];

            for (int tunnelIndex = 0; tunnelIndex < data.Tunnels.Length; tunnelIndex++)
            {
                CaveTunnel tunnel = data.Tunnels[tunnelIndex];
                float maxRadius = math.max(tunnel.radiusA, tunnel.radiusB);
                float inflation = maxRadius + tunnel.blendRadius + data.CaveParams.warpAmplitude + tunnel.warpAmount + data.CaveParams.noiseEvalDistance + (data.VoxelStep * 2f);
                float3 boundsMin = math.min(tunnel.pointA, tunnel.pointB) - new float3(inflation);
                float3 boundsMax = math.max(tunnel.pointA, tunnel.pointB) + new float3(inflation);

                ResolvePartitionRange(data, boundsMin, boundsMax, out int3 minCell, out int3 maxCell);
                for (int z = minCell.z; z <= maxCell.z; z++)
                for (int y = minCell.y; y <= maxCell.y; y++)
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int bucketIndex = FlattenPartitionIndex(data, x, y, z);
                    int writeIndex = writeHeads[bucketIndex];
                    data.TunnelBucketIndices[writeIndex] = tunnelIndex;
                    writeHeads[bucketIndex] = writeIndex + 1;
                }
            }
        }
        finally
        {
            if (bucketCounts.IsCreated) bucketCounts.Dispose();
            if (writeHeads.IsCreated) writeHeads.Dispose();
        }
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

        minCell = new int3(
            math.clamp((int)math.floor((clampedMin.x - data.PartitionOrigin.x) * invCellSize.x), 0, data.PartitionDimX - 1),
            math.clamp((int)math.floor((clampedMin.y - data.PartitionOrigin.y) * invCellSize.y), 0, data.PartitionDimY - 1),
            math.clamp((int)math.floor((clampedMin.z - data.PartitionOrigin.z) * invCellSize.z), 0, data.PartitionDimZ - 1));
        maxCell = new int3(
            math.clamp((int)math.floor((clampedMax.x - data.PartitionOrigin.x) * invCellSize.x), 0, data.PartitionDimX - 1),
            math.clamp((int)math.floor((clampedMax.y - data.PartitionOrigin.y) * invCellSize.y), 0, data.PartitionDimY - 1),
            math.clamp((int)math.floor((clampedMax.z - data.PartitionOrigin.z) * invCellSize.z), 0, data.PartitionDimZ - 1));
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

    struct VoxelSurfaceVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color32 Color;
        public Vector4 BakedOcclusionUv1;
        public Vector4 DirtyBlendUv2;
        public Vector3 AbsolutePositionWS;
    }

    struct VoxelColliderVertex
    {
        public Vector3 Position;
    }

    readonly struct VoxelFinalizeProjectionState
    {
        public readonly OriginShiftEventData StableShift;
        public readonly Vector3 RootRuntimePosition;
        public readonly bool ShiftEpochChanged;

        public VoxelFinalizeProjectionState(in OriginShiftEventData stableShift, Vector3 rootRuntimePosition, bool shiftEpochChanged)
        {
            StableShift = stableShift;
            RootRuntimePosition = rootRuntimePosition;
            ShiftEpochChanged = shiftEpochChanged;
        }

        public float3 AbsolutePositionOffset => (float3)(StableShift.NewTotalOffset + RootRuntimePosition);

        public float3 ProjectRuntimePositionToLocal(Vector3 capturedRuntimePosition, Vector3 capturedTotalOffset)
        {
            Vector3 rebasedRuntimePosition = StableShift.RebaseCapturedRuntimePosition(capturedRuntimePosition, capturedTotalOffset);
            return (float3)(rebasedRuntimePosition - RootRuntimePosition);
        }
    }

    static Bounds CalculatePositionBounds(NativeArray<float3> positions, int count)
    {
        if (count <= 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        float3 min = positions[0];
        float3 max = positions[0];
        for (int i = 1; i < count; i++)
        {
            float3 position = positions[i];
            min = math.min(min, position);
            max = math.max(max, position);
        }

        float3 center = (min + max) * 0.5f;
        float3 size = math.max(max - min, new float3(0.01f));
        return new Bounds(center, size);
    }

    static bool OffsetsApproximatelyMatch(Vector3 lhs, Vector3 rhs)
    {
        return (lhs - rhs).sqrMagnitude <= 0.000001f;
    }

    Awaitable<NativeArray<float3>> BuildShiftAwareLocalPositionBufferAsync(
        VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState,
        CancellationToken ct)
    {
        return BuildShiftAwareLocalPositionBufferInternalAsync();

        async Awaitable<NativeArray<float3>> BuildShiftAwareLocalPositionBufferInternalAsync()
        {
            bool needsProjection =
                projectionState.ShiftEpochChanged ||
                !OffsetsApproximatelyMatch(data.AbsoluteUniverseOffsetAtStart, projectionState.StableShift.NewTotalOffset) ||
                projectionState.RootRuntimePosition.sqrMagnitude > 0.000001f;

            if (!needsProjection)
                return default;

            // Red Team #1:
            // local = (capturedRuntime + capturedOffset - committedOffset) - currentRootRuntimePosition
            // This keeps async mesh/collider finalize in sync with the latest committed floating-origin shift.
            // COLD ALLOC: NativeArray<float3>[data.WeldedCount] - shift-aware voxel local-space projection buffer for async finalize - owner: HectonVoxelEngine
            NativeArray<float3> projectedPositions = new NativeArray<float3>(data.WeldedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(projectedPositions, nameof(projectedPositions));
            JobHandle projectionHandle = new VoxelShiftAwareProjectionJob
            {
                rebaseDelta = (float3)(data.AbsoluteUniverseOffsetAtStart - projectionState.StableShift.NewTotalOffset),
                rootRuntimePosition = (float3)projectionState.RootRuntimePosition,
                sourcePositions = data.WeldedPositions,
                projectedPositions = projectedPositions
            }.Schedule(data.WeldedCount, JOB_BATCH);

            await AwaitForJobCompletionAsync(projectionHandle, ct, "origin-shift projection");
            ct.ThrowIfCancellationRequested();
            return projectedPositions;
        }
    }

    static void UploadSurfaceMesh(
        Mesh mesh,
        NativeArray<float3> positions,
        NativeArray<float3> normals,
        NativeArray<Color> colors,
        NativeArray<float> ambientOcclusionValues,
        NativeArray<float> curvatureValues,
        NativeArray<float> skirtAlphaValues,
        NativeArray<float> dirtyBlendValues,
        NativeArray<int> triangleIndices,
        int vertexCount,
        int triangleIndexCount,
        float3 absolutePositionOffset)
    {
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];
        meshData.SetVertexBufferParams(
            vertexCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3));

        meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

        NativeArray<VoxelSurfaceVertex> vertexData = meshData.GetVertexData<VoxelSurfaceVertex>();
        for (int i = 0; i < vertexCount; i++)
        {
            vertexData[i] = new VoxelSurfaceVertex
            {
                Position = positions[i],
                Normal = normals[i],
                Color = (Color32)colors[i],
                BakedOcclusionUv1 = new Vector4(0f, 0f, 0f, ambientOcclusionValues.IsCreated && i < ambientOcclusionValues.Length ? ambientOcclusionValues[i] : 1f),
                DirtyBlendUv2 = new Vector4(
                    dirtyBlendValues.IsCreated && i < dirtyBlendValues.Length ? dirtyBlendValues[i] : 0f,
                    skirtAlphaValues.IsCreated && i < skirtAlphaValues.Length ? skirtAlphaValues[i] : 0f,
                    curvatureValues.IsCreated && i < curvatureValues.Length ? curvatureValues[i] : 0.5f,
                    0f),
                AbsolutePositionWS = positions[i] + absolutePositionOffset
            };
        }

        NativeArray<uint> indexData = meshData.GetIndexData<uint>();
        for (int i = 0; i < triangleIndexCount; i++)
            indexData[i] = (uint)triangleIndices[i];

        Bounds bounds = CalculatePositionBounds(positions, vertexCount);
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount, MeshTopology.Triangles)
        {
            bounds = bounds,
            vertexCount = vertexCount
        }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        mesh.bounds = bounds;
    }

    static void UploadColliderMesh(
        Mesh mesh,
        NativeArray<float3> positions,
        NativeArray<int> triangleIndices,
        int vertexCount,
        int triangleIndexCount)
    {
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];
        meshData.SetVertexBufferParams(
            vertexCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));

        meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

        NativeArray<VoxelColliderVertex> vertexData = meshData.GetVertexData<VoxelColliderVertex>();
        for (int i = 0; i < vertexCount; i++)
            vertexData[i] = new VoxelColliderVertex { Position = positions[i] };

        NativeArray<uint> indexData = meshData.GetIndexData<uint>();
        for (int i = 0; i < triangleIndexCount; i++)
            indexData[i] = (uint)triangleIndices[i];

        Bounds bounds = CalculatePositionBounds(positions, vertexCount);
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount, MeshTopology.Triangles)
        {
            bounds = bounds,
            vertexCount = vertexCount
        }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        mesh.bounds = bounds;
    }

    GameObject SpawnVolume()
    {
        ObjectPoolManager pool = GlobalRegistry.ObjectPool;
        if (pool != null && voxelVolumePrefab != null)
        {
            GameObject pooled = pool.Spawn(voxelVolumePrefab, Vector3.zero, Quaternion.identity);
            if (pooled != null)
            {
                PrepareVolumeForBuild(pooled);
                HectonFloatingOrigin.MarkShiftTargetsDirty();
                return pooled;
            }
        }

        var go = new GameObject(RuntimeCaveVolumeName);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();
        go.AddComponent<HectonVoxelVolume>(); // Add volume component
        PrepareVolumeForBuild(go);
        HectonFloatingOrigin.MarkShiftTargetsDirty();
        return go;
    }

    Mesh BuildWeldedMeshNative(GameObject go,
                               NativeArray<float3> positions,
                               NativeArray<float3> normals,
                               NativeArray<Color> colors,
                               NativeArray<float> ambientOcclusionValues,
                               NativeArray<float> curvatureValues,
                               NativeArray<float> skirtAlphaValues,
                               NativeArray<float> dirtyBlendValues,
                               NativeArray<int> triangleIndices,
                               int triIndexCount,
                               int vertCount,
                               float3 absolutePositionOffset,
                               Material mat)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                name = $"CaveMesh_{go.name}"
#else
                name = RuntimeCaveMeshName
#endif
            };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        UploadSurfaceMesh(mesh, positions, normals, colors, ambientOcclusionValues, curvatureValues, skirtAlphaValues, dirtyBlendValues, triangleIndices, vertCount, triIndexCount, absolutePositionOffset);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.enabled = true;
        return mesh;
    }

    Awaitable<bool> ApplyVolumeMeshAsync(GameObject go, VoxelPipelineData data, OriginShiftEventData stableShift, CancellationToken ct)
    {
        return ApplyVolumeMeshInternalAsync();

        async Awaitable<bool> ApplyVolumeMeshInternalAsync()
        {
            NativeArray<float3> projectedLocalPositions = default;
            try
            {
                VoxelFinalizeProjectionState projectionState = new VoxelFinalizeProjectionState(
                    stableShift,
                    go != null ? go.transform.position : Vector3.zero,
                    data.ShiftEpochAtStart != stableShift.Sequence);

                projectedLocalPositions = await BuildShiftAwareLocalPositionBufferAsync(data, projectionState, ct);
                NativeArray<float3> meshLocalPositions = projectedLocalPositions.IsCreated ? projectedLocalPositions : data.WeldedPositions;
                float3 localVolumeOrigin = projectionState.ProjectRuntimePositionToLocal((Vector3)data.VolumeOrigin, data.AbsoluteUniverseOffsetAtStart);

                Mesh mesh = BuildWeldedMeshNative(
                    go,
                    meshLocalPositions,
                    data.Normals,
                    data.Colors,
                    data.AmbientOcclusionValues,
                    data.CurvatureValues,
                    data.SkirtAlphaValues,
                    data.DirtyBlendValues,
                    data.TriangleIndices,
                    data.RawCount,
                    data.WeldedCount,
                    projectionState.AbsolutePositionOffset,
                    voxelMaterial);

                var mcol = go.GetComponent<MeshCollider>();
                if (mcol == null) mcol = go.AddComponent<MeshCollider>();

                HectonVoxelVolume volume = go.GetComponent<HectonVoxelVolume>();

                if (!data.BuildCollider)
                {
                    if (volume != null)
                        volume.ResetColliderChunks(false);

                    mcol.sharedMesh = null;
                    mcol.enabled = false;
                    return true;
                }

                if (volume == null)
                {
                    JobHandle fallbackBakeHandle = new VoxelMeshBakeJob
                    {
                        MeshId = mesh.GetEntityId(),
                        Convex = false
                    }.Schedule();

                    if (!await AwaitForPhysicsBakeCompletionOrDeferAsync(
                            fallbackBakeHandle,
                            ct,
                            "fallback collider bake",
                            mesh,
                            go,
                            go.GetComponent<MeshRenderer>(),
                            mcol,
                            DeferredVoxelBakeDestroyOwner))
                    {
                        return false;
                    }

                    ct.ThrowIfCancellationRequested();
                    mcol.sharedMesh = mesh;
                    mcol.enabled = true;
                    return true;
                }

                return await ApplyChunkedColliderMeshesAsync(volume, data, meshLocalPositions, localVolumeOrigin, ct);
            }
            finally
            {
                if (projectedLocalPositions.IsCreated)
                    DisposeTrackedNativeArray(ref projectedLocalPositions);
            }
        }
    }

    static int ResolveColliderChunkCount(int triangleCount)
    {
        if (triangleCount >= 40000)
            return 8;

        if (triangleCount >= 10000)
            return 4;

        return 2;
    }

    async Awaitable<bool> ApplyChunkedColliderMeshesAsync(
        HectonVoxelVolume volume,
        VoxelPipelineData data,
        NativeArray<float3> meshLocalPositions,
        float3 localVolumeOrigin,
        CancellationToken ct)
    {
        int triangleIndexCount = data.RawCount;
        int triangleCount = triangleIndexCount / 3;
        if (triangleCount <= 0)
        {
            volume.ResetColliderChunks(false);
            return true;
        }

        int colliderChunkCount = ResolveColliderChunkCount(triangleCount);
        volume.EnsureColliderChunkCapacity(colliderChunkCount);

        NativeArray<byte> triangleBuckets = default;
        NativeArray<int> bucketCounts = default;
        NativeArray<int> bucketOffsets = default;
        NativeArray<int> bucketWriteHeads = default;
        NativeArray<int> chunkTriangleIndices = default;
        bool completed = false;
        bool deferredBakeTeardown = false;

        try
        {
            triangleBuckets = new NativeArray<byte>(triangleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            bucketCounts = new NativeArray<int>(colliderChunkCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            bucketOffsets = new NativeArray<int>(colliderChunkCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            bucketWriteHeads = new NativeArray<int>(colliderChunkCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            chunkTriangleIndices = new NativeArray<int>(triangleIndexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(triangleBuckets, nameof(triangleBuckets));
            RegisterTrackedNativeArray(bucketCounts, nameof(bucketCounts));
            RegisterTrackedNativeArray(bucketOffsets, nameof(bucketOffsets));
            RegisterTrackedNativeArray(bucketWriteHeads, nameof(bucketWriteHeads));
            RegisterTrackedNativeArray(chunkTriangleIndices, nameof(chunkTriangleIndices));

            float3 boundsMin = localVolumeOrigin;
            float3 boundsSize = new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep;

            JobHandle classifyHandle = new VoxelColliderChunkClassifyJob
            {
                positions = meshLocalPositions,
                triangleIndices = data.TriangleIndices,
                boundsMin = boundsMin,
                boundsSize = boundsSize,
                chunkCount = colliderChunkCount,
                triangleBuckets = triangleBuckets
            }.Schedule(triangleCount, 64);

            await AwaitForJobCompletionAsync(classifyHandle, ct, "collider chunk classify");

            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                bucketCounts[triangleBuckets[triangleIndex]] += 3;

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
                int writeHead = bucketWriteHeads[bucket];
                int triBase = triangleIndex * 3;
                chunkTriangleIndices[writeHead] = data.TriangleIndices[triBase];
                chunkTriangleIndices[writeHead + 1] = data.TriangleIndices[triBase + 1];
                chunkTriangleIndices[writeHead + 2] = data.TriangleIndices[triBase + 2];
                bucketWriteHeads[bucket] = writeHead + 3;
            }

            for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
            {
                ct.ThrowIfCancellationRequested();

                int chunkIndexCount = bucketCounts[chunkIndex];
                MeshCollider chunkCollider = volume.GetColliderChunkCollider(chunkIndex);
                if (chunkCollider == null)
                    return false;

                if (chunkIndexCount <= 0)
                {
                    chunkCollider.sharedMesh = null;
                    chunkCollider.enabled = false;
                    chunkCollider.gameObject.SetActive(false);
                    continue;
                }

                Mesh chunkMesh = volume.GetOrCreateColliderChunkBakeMesh(chunkIndex);
                if (chunkMesh == null)
                {
                    chunkCollider.sharedMesh = null;
                    chunkCollider.enabled = false;
                    chunkCollider.gameObject.SetActive(false);
                    return false;
                }

                chunkCollider.gameObject.SetActive(true);
                chunkMesh.Clear();
                NativeParallelHashMap<int, int> localVertexMap = default;
                NativeList<float3> localPositions = default;
                NativeArray<int> localIndices = default;

                try
                {
                    localVertexMap = new NativeParallelHashMap<int, int>(math.max(1, chunkIndexCount), Allocator.Temp);
                    localPositions = new NativeList<float3>(math.max(3, chunkIndexCount), Allocator.Temp);
                    localIndices = new NativeArray<int>(chunkIndexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                    for (int localIndex = 0; localIndex < chunkIndexCount; localIndex++)
                    {
                        int globalIndex = chunkTriangleIndices[bucketOffsets[chunkIndex] + localIndex];
                        if (!localVertexMap.TryGetValue(globalIndex, out int remappedIndex))
                        {
                            remappedIndex = localPositions.Length;
                            localVertexMap.Add(globalIndex, remappedIndex);
                            localPositions.AddNoResize(meshLocalPositions[globalIndex]);
                        }

                        localIndices[localIndex] = remappedIndex;
                    }

                    UploadColliderMesh(chunkMesh, localPositions.AsArray(), localIndices, localPositions.Length, chunkIndexCount);
                }
                finally
                {
                    if (localIndices.IsCreated) localIndices.Dispose();
                    if (localPositions.IsCreated) localPositions.Dispose();
                    if (localVertexMap.IsCreated) localVertexMap.Dispose();
                }

                JobHandle bakeHandle = new VoxelMeshBakeJob
                {
                    MeshId = chunkMesh.GetEntityId(),
                    Convex = false
                }.Schedule();

                if (!await AwaitForPhysicsBakeCompletionOrDeferAsync(
                        bakeHandle,
                        ct,
                        "collider chunk bake",
                        chunkMesh,
                        volume.gameObject,
                        null,
                        chunkCollider,
                        0))
                {
                    volume.DetachColliderChunkBakeMesh(chunkIndex);
                    deferredBakeTeardown = true;
                    return false;
                }

                ct.ThrowIfCancellationRequested();
                volume.PublishColliderChunkMesh(chunkIndex);
                chunkCollider.enabled = true;

                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }

            volume.SetActiveColliderChunkCount(colliderChunkCount);
            completed = true;
            return true;
        }
        finally
        {
            if (!completed && !deferredBakeTeardown)
                volume.ClearColliderChunkBakeMeshes();

            DisposeTrackedNativeArray(ref triangleBuckets);
            DisposeTrackedNativeArray(ref bucketCounts);
            DisposeTrackedNativeArray(ref bucketOffsets);
            DisposeTrackedNativeArray(ref bucketWriteHeads);
            DisposeTrackedNativeArray(ref chunkTriangleIndices);
        }
    }

    void PrepareVolumeForBuild(GameObject go)
    {
        if (go == null)
            return;

        var volume = go.GetComponent<HectonVoxelVolume>();
        if (volume != null)
            volume.PrepareForReuse();

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

    void ConfigureVolumeRuntimeData(
        GameObject go,
        uint seed,
        Vector3 worldCenter,
        Vector3 absoluteUniverseOffset,
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
        bool buildCollider)
    {
        if (go == null)
            return;

        var volume = go.GetComponent<HectonVoxelVolume>();
        if (volume == null)
            return;

        volume.ConfigureRuntimeData(
            this,
            seed,
            worldCenter,
            absoluteUniverseOffset,
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

        volume.PublishSonarSdfSnapshot(
            new Vector3Int(ptsX, ptsY, ptsZ),
            volumeOrigin,
            Vector3.one * voxelStep,
            smoothDensityField);
    }

    void RegisterEntranceTerrainHoles(
        GameObject go,
        NativeArray<CaveEntrance> entrances,
        float voxelSize,
        Vector3 capturedTotalOffset,
        Vector3 committedTotalOffset)
    {
        HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        if (vegetationBridge == null || go == null || !entrances.IsCreated || entrances.Length <= 0)
            return;

        HectonVoxelVolume volume = go.GetComponent<HectonVoxelVolume>();
        if (volume == null)
            return;

        float holePadding = math.max(voxelSize * 1.5f, 1f);
        for (int i = 0; i < entrances.Length; i++)
        {
            CaveEntrance entrance = entrances[i];
            float radius = math.max(entrance.radius, entrance.innerRadius) + holePadding;
            Vector3 runtimeSurfacePosition = (Vector3)entrance.surfacePosition + capturedTotalOffset - committedTotalOffset;
            int holeHandle = vegetationBridge.RegisterTerrainHoleHandle(runtimeSurfacePosition, radius);
            volume.TrackTerrainHoleHandle(holeHandle);
        }
    }

    void RegisterPipelineSpawnPoints(
        Vector3 worldCenter,
        SpawnContext caveContext,
        NativeList<CaveSpawnData> spawnPointList,
        Vector3 capturedTotalOffset,
        Vector3 committedTotalOffset)
    {
        if (!spawnPointList.IsCreated || spawnPointList.Length <= 0 || ScavengePopulator.Instance == null)
            return;

        Vector3 absoluteUniverseCenter = worldCenter + capturedTotalOffset;
        float tileSize = mapMagicTileSize > 0f ? mapMagicTileSize : 999f;
        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt(absoluteUniverseCenter.x / tileSize),
            Mathf.FloorToInt(absoluteUniverseCenter.z / tileSize));

        for (int sp = 0; sp < spawnPointList.Length; sp++)
        {
            CaveSpawnData spawnData = spawnPointList[sp];
            Vector3 runtimeSpawnPosition = (Vector3)spawnData.position + capturedTotalOffset - committedTotalOffset;
            ScavengePopulator.Instance.RegisterSpawnPoint(
                runtimeSpawnPosition,
                Quaternion.identity,
                Vector3.one,
                chunkCoord,
                spawnData.hashId,
                caveContext);
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
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            if (activeVolume == null)
                continue;

            Bounds localBounds = i < _activeVolumeLocalBounds.Count
                ? _activeVolumeLocalBounds[i]
                : new Bounds(Vector3.zero, Vector3.one);

            if (localBounds.size.sqrMagnitude <= 0.0001f)
                localBounds = new Bounds(Vector3.zero, Vector3.one);

            Gizmos.matrix = activeVolume.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }

        Gizmos.matrix = previousMatrix;
    }
#endif
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
