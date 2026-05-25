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
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

// -------------------------------------------------------------------------------
//  REGION: MARCHING CUBES LOOKUP TABLES (unchanged from v3.2)
// -------------------------------------------------------------------------------
#region Marching Cubes Tables

public static class MCTables
{
    public static NativeArray<int>.ReadOnly EdgeTable => _edgeTable.IsCreated ? _edgeTable.AsReadOnly() : default;
    public static NativeArray<int>.ReadOnly TriTable  => _triTable.IsCreated ? _triTable.AsReadOnly() : default;
    public static bool IsReady => Volatile.Read(ref _ready) == 1;

    static NativeArray<int> _edgeTable;
    static NativeArray<int> _triTable;
    static int _ready;
    static readonly object _initLock = new object();
    static bool _editorHooksInstalled;
    const Allocator DataVaultExemptMarchingCubesTableAllocator = Allocator.Persistent;

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
            _edgeTable = new NativeArray<int>(256, DataVaultExemptMarchingCubesTableAllocator);
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
            _triTable = new NativeArray<int>(4096, DataVaultExemptMarchingCubesTableAllocator);
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

[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct MCRawVertex
{
    [FieldOffset(0)]
    public float3 localPosition;

    [FieldOffset(16)]
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
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDensityJob : IJobParallelFor
{
    private const byte DeltaModeAdditive = 1 << 0;
    private const byte DeltaModeReplace = 1 << 1;
    private const float AlienBiomeFullLodNoiseFrequency = 0.19f;
    private const float AlienBiomeMidLodNoiseFrequency = 0.11f;
    private const uint AlienBiomeNoiseSeed = 0xA11E5DFu;

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
    [ReadOnly] public NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells;
    [ReadOnly, NoAlias] public NativeArray<int> nodeBucketOffsets;
    [ReadOnly, NoAlias] public NativeArray<int> nodeBucketIndices;
    [ReadOnly, NoAlias] public NativeArray<int> tunnelBucketOffsets;
    [ReadOnly, NoAlias] public NativeArray<int> tunnelBucketIndices;

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
    public int enableBiomeSdfModifiers;

    // ── Output ──
    [WriteOnly, NoAlias] public NativeArray<float> density;
    [WriteOnly, NoAlias] public NativeArray<float> smoothDensity;

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
            : VoxelSeamDirector.ComputeTerrainDensity(terrainH, wp.y);

        smoothDensityValue = terrainDensity;
        finalDensityValue = terrainDensity;

        float smoothCaveSdf = 1f;
        float finalCaveSdf = 1f;
        if (!structureOnlyMode)
        {
            EvaluateCaveSDF(wp, out smoothCaveSdf, out finalCaveSdf);

            if (smoothCaveSdf < caveParams.shellThickness)
                smoothDensityValue = SmoothSubtractionQuadratic(-smoothCaveSdf, terrainDensity, caveParams.shellThickness);

            if (finalCaveSdf < caveParams.shellThickness)
                finalDensityValue = SmoothSubtractionQuadratic(-finalCaveSdf, terrainDensity, caveParams.shellThickness);
        }

        if (!structureOnlyMode && caveEntrances.Length > 0)
        {
            float entranceSkirtSDF = EvaluateEntranceSkirtSDF(wp);
            if (entranceSkirtSDF < caveParams.entranceBlendK)
            {
                float skirtBlend = caveParams.entranceBlendK * 0.45f;
                smoothDensityValue = SmoothMaxQuadratic(smoothDensityValue, -entranceSkirtSDF, skirtBlend);
                finalDensityValue = SmoothMaxQuadratic(finalDensityValue, -entranceSkirtSDF, skirtBlend);
            }
        }

        if (caveStructures.Length > 0 && (structureOnlyMode || smoothCaveSdf < 0f || finalCaveSdf < 0f))
        {
            EvaluateStructuresSDF(wp, out float smoothStructureSdf, out float finalStructureSdf);
            if (smoothStructureSdf < caveParams.structureBlendK)
                smoothDensityValue = SmoothMaxQuadratic(smoothDensityValue, -smoothStructureSdf, caveParams.structureBlendK);

            if (finalStructureSdf < caveParams.structureBlendK)
                finalDensityValue = SmoothMaxQuadratic(finalDensityValue, -finalStructureSdf, caveParams.structureBlendK);
        }

        if (craterStamps.IsCreated && craterStamps.Length > 0)
        {
            smoothDensityValue = EvaluateCraterModifiers(wp, smoothDensityValue);
            finalDensityValue = EvaluateCraterModifiers(wp, finalDensityValue);
        }

        ApplyAlienBiomeSdfModifier(wp, ref smoothDensityValue, ref finalDensityValue);

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
        if (!gridBiome.IsCreated || gridBiome.Length < ptsX * ptsZ || ptsX <= 1 || ptsZ <= 1 || voxelStep <= 0.0001f)
            return 0f;

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

            float horizontalDist = FastMagnitude(horizontalDistSq);
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
            shell = SmoothMaxQuadratic(shell, transitionClip, transitionZone);
            shell = math.max(shell, terrainClip);
            skirtDist = SmoothMinQuadratic(skirtDist, shell, caveParams.entranceBlendK * 0.3f);
        }

        return skirtDist;
    }

    float3 ResolveEntranceDirection(CaveEntrance entrance)
    {
        float3 direction = NormalizeFastOrDefault(entrance.inwardDirection, new float3(0f, -1f, 0f));
        float normalBlend = math.saturate(entrance.terrainNormalBlend);
        if (normalBlend <= 0f)
            return direction;

        float3 terrainNormal = NormalizeFastOrDefault(entrance.terrainNormal, new float3(0f, 1f, 0f));
        float3 terrainInward = NormalizeFastOrDefault(-terrainNormal, direction);
        return NormalizeFastOrDefault(math.lerp(direction, terrainInward, normalBlend * 0.55f), direction);
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
                smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, smoothNodeDist, node.blendRadius);
                finalCaveDist = SmoothMinQuadratic(finalCaveDist, finalNodeDist, node.blendRadius);
            }
        }
        else
        {
            for (int i = 0; i < caveNodes.Length; i++)
            {
                EvaluateRoom(warpedPos, absoluteWp, caveNodes[i], out float smoothNodeDist, out float finalNodeDist);
                smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, smoothNodeDist, caveNodes[i].blendRadius);
                finalCaveDist = SmoothMinQuadratic(finalCaveDist, finalNodeDist, caveNodes[i].blendRadius);
            }
        }

        if (TryGetPartitionRange(tunnelBucketOffsets, wp, out int tunnelStart, out int tunnelEnd))
        {
            for (int i = tunnelStart; i < tunnelEnd; i++)
            {
                CaveTunnel tunnel = caveTunnels[tunnelBucketIndices[i]];
                float tunnelDist = EvaluateTunnel(warpedPos, absoluteWp, wp, tunnel);
                smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, tunnelDist, tunnel.blendRadius);
                finalCaveDist = SmoothMinQuadratic(finalCaveDist, tunnelDist, tunnel.blendRadius);
            }
        }
        else
        {
            for (int i = 0; i < caveTunnels.Length; i++)
            {
                float tunnelDist = EvaluateTunnel(warpedPos, absoluteWp, wp, caveTunnels[i]);
                smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, tunnelDist, caveTunnels[i].blendRadius);
                finalCaveDist = SmoothMinQuadratic(finalCaveDist, tunnelDist, caveTunnels[i].blendRadius);
            }
        }

        for (int i = 0; i < caveEntrances.Length; i++)
        {
            float entranceDist = EvaluateEntrance(warpedPos, caveEntrances[i]);
            smoothCaveDist = SmoothMinQuadratic(smoothCaveDist, entranceDist, caveParams.entranceBlendK);
            finalCaveDist = SmoothMinQuadratic(finalCaveDist, entranceDist, caveParams.entranceBlendK);
        }

        float baseFinalCaveDist = finalCaveDist;
        if (math.abs(baseFinalCaveDist) < caveParams.noiseEvalDistance)
        {
            float mouthPerturbationMask = EvaluateCaveMouthSdfPerturbationMask(wp);
            if (mouthPerturbationMask > 0.0001f)
            {
                finalCaveDist += EvaluateWallDetail(absoluteWp, baseFinalCaveDist) * mouthPerturbationMask;
                finalCaveDist -= EvaluateFractalNoiseCarve(warpedAbsolutePos, absoluteWp, baseFinalCaveDist) * mouthPerturbationMask;
            }
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

        return SmoothMinQuadratic(core, flare, caveParams.entranceBlendK * 0.4f);
    }

    float EvaluateCaveMouthSdfPerturbationMask(float3 wp)
    {
        float mask = 1f;
        for (int i = 0; i < caveEntrances.Length; i++)
        {
            CaveEntrance entrance = caveEntrances[i];
            float radius = math.max(entrance.radius, voxelStep);
            float distanceSq = math.lengthsq(wp - entrance.surfacePosition);
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
        float3 axis = math.abs(value);
        float maxAxis = math.cmax(axis);
        float minAxis = math.cmin(axis);
        float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
        return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
    }

    static float LengthApprox(float2 value)
    {
        float2 axis = math.abs(value);
        float maxAxis = math.max(axis.x, axis.y);
        float minAxis = math.min(axis.x, axis.y);
        return maxAxis + minAxis * 0.375f;
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

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
struct VoxelColliderChunkClassifyJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<int> triangleIndices;
    public float3 boundsMin;
    public float3 boundsSize;
    public int chunkCount;
    [WriteOnly, NoAlias] public NativeArray<byte> triangleBuckets;

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
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelFillIntArrayJob : IJobParallelFor
{
    public int Value;
    [NoAlias] public NativeArray<int> Values;

    public void Execute(int index)
    {
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
        Values[index] = Value;
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelChunkSkirtExtrusionJob : IJobParallelFor
{
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
        if (ptsX <= 1 || ptsZ <= 1 || voxelStep <= 0.0001f)
            return;

        float3 position = positions[idx];
        float volumeSizeX = (ptsX - 1) * voxelStep;
        float volumeSizeZ = (ptsZ - 1) * voxelStep;
        float localX = position.x - volumeOrigin.x;
        float localZ = position.z - volumeOrigin.z;
        float edgeDist = math.min(localX, math.min(volumeSizeX - localX, math.min(localZ, volumeSizeZ - localZ)));
        float safeSkirtWidth = math.max(skirtWidthMeters, voxelStep);
        float skirtMask = 1f - math.smoothstep(0f, safeSkirtWidth, math.max(edgeDist, 0f));
        if (skirtMask <= 0.0001f)
            return;

        float lodScale = lodLevel > 0 ? 1f : 0.65f;
        position.y -= skirtMask * math.max(skirtDepthMeters, 0f) * lodScale;
        positions[idx] = position;

        if (skirtAlphaValues.IsCreated && idx < skirtAlphaValues.Length)
            skirtAlphaValues[idx] = math.max(skirtAlphaValues[idx], skirtMask);
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
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelMCCountJob : IJobParallelFor
{
    public int cellsX, cellsY, cellsZ;
    public int ptsX, ptsY, ptsZ;
    public float densityDecodeScale;

    [ReadOnly, NoAlias] public NativeArray<sbyte> density;
    [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly edgeTable;
    [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly triTable;
    [WriteOnly, NoAlias] public NativeArray<int> cellVertexCounts;

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
    float D(int ix, int iy, int iz) => density[GI(ix, iy, iz)] * densityDecodeScale;
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelDensityQuantizeJob : IJobParallelFor
{
    public float densityDecodeInvScale;

    [ReadOnly, NoAlias] public NativeArray<float> density;
    [WriteOnly, NoAlias] public NativeArray<sbyte> quantizedDensity;

    public void Execute(int index)
    {
        float source = density[index];
        float scaled = math.clamp(source * densityDecodeInvScale, -127f, 127f);
        int quantized = scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
        if (quantized == 0 && math.abs(source) > 0.00001f)
            quantized = source < 0f ? -1 : 1;

        quantizedDensity[index] = (sbyte)quantized;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  JOB 2.1: Marching Cubes extraction (exact-offset write)
// ═══════════════════════════════════════════════════════════════════════════════
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct VoxelMCExtractJob : IJobParallelFor
{
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
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public unsafe struct VoxelWeldJob : IJob
{
    private const int InvalidVertexIndex = -1;

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
                weldedPositions[newIdx] = rv.localPosition;
                WriteEdgeVertex(axis, edgeSlot, newIdx);
                triangleIndices[i] = newIdx;
                weldedCount++;
                continue;
            }

            int fallbackIdx = weldedCount;
            weldedPositions[fallbackIdx] = rv.localPosition;
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

    public void Execute(int idx)
    {
        float3 wp = positions[idx];
        float3 sample = (wp - volumeOrigin) * invVoxelStep;
        int x = (int)math.clamp(sample.x + 0.5f, 0f, ptsX - 1f);
        int y = (int)math.clamp(sample.y + 0.5f, 0f, ptsY - 1f);
        int z = (int)math.clamp(sample.z + 0.5f, 0f, ptsZ - 1f);
        float4 gradientAndAo = SampleNearestGridGradientAndAo(x, y, z);
        float3 normal = ApproxNormalizeOrUp(-gradientAndAo.xyz);
        normals[idx] = normal;

        float horizontalMask = math.saturate((math.abs(normal.x) + math.abs(normal.z)) * 0.5f);
        float ceilingMask = math.saturate(-normal.y);
        float neighborCavityMask = 1f - gradientAndAo.w;
        float curvature01 = math.saturate(0.45f + horizontalMask * 0.18f - ceilingMask * 0.22f + neighborCavityMask * 0.12f);
        curvatureValues[idx] = curvature01;

        float cavityMask = math.saturate((0.5f - curvature01) * 2f);
        float overheadMask = math.saturate(0.5f - normal.y * 0.5f);
        ambientOcclusionValues[idx] = math.saturate(gradientAndAo.w - cavityMask * 0.24f - overheadMask * 0.12f);
    }

    static float3 ApproxNormalizeOrUp(float3 value)
    {
        float3 axis = math.abs(value);
        float maxAxis = math.cmax(axis);
        float minAxis = math.cmin(axis);
        float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
        float invLen = math.rcp(math.max(maxAxis + midAxis * 0.375f + minAxis * 0.25f, 0.0001f));
        return math.select(new float3(0f, 1f, 0f), value * invLen, maxAxis > 0.0001f);
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
            neighborAo);
    }
}

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelTerrainSeamSnapJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float3 absoluteUniverseOffset;
    public float voxelStep;
    public float seamTransitionBand;
    public float seamOverlap;

    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [NoAlias] public NativeArray<float3> positions;

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
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSeamNormalBlendJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float3 absoluteUniverseOffset;
    public float voxelStep;
    public float seamTransitionBand;

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [NoAlias] public NativeArray<float3> normals;

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
        float3 voxelNormal = NormalizeFastOrDefault(normals[idx], new float3(0f, 1f, 0f));
        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        normals[idx] = BlendNormalsNlerp(voxelNormal, terrainNormal, blendToTerrain);
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
        return NormalizeFastOrDefault(math.lerp(normalX0, normalX1, fz), new float3(0f, 1f, 0f));
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
        return NormalizeFastOrDefault(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
    }

    static float3 BlendNormalsNlerp(float3 startNormal, float3 endNormal, float t)
    {
        float blend = math.saturate(t);
        return NormalizeFastOrDefault(math.lerp(startNormal, endNormal, blend), startNormal);
    }

    static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
    {
        float lengthSq = math.lengthsq(value);
        return lengthSq > 0.0001f ? value / math.max(LengthApprox(value), 0.0001f) : fallback;
    }

    static float LengthApprox(float3 value)
    {
        float3 axis = math.abs(value);
        float maxAxis = math.cmax(axis);
        float minAxis = math.cmin(axis);
        float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
        return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
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
        projectedPositions[index] = sourcePositions[index] + rebaseDelta - rootRuntimePosition;
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
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float3> normals;
    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [ReadOnly, NoAlias] public NativeArray<float> gridBiome;
    [ReadOnly, NoAlias] public NativeArray<float> curvatureValues;
    [ReadOnly, NoAlias] public NativeArray<float> ambientOcclusionValues;
    [ReadOnly, NoAlias] public NativeArray<float> biomeValues;
    [ReadOnly, NoAlias] public NativeArray<CaveEntrance> caveEntrances;
    [ReadOnly] public NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells;

    public float3 absoluteUniverseOffset;

    [WriteOnly, NoAlias] public NativeArray<Color> colors;
    [NoAlias] public NativeArray<float> skirtAlphaValues;

    public void Execute(int idx)
    {
        float3 p = positions[idx];

        float safeHalfExtent = math.max(volumeHalfExtent, 1f);
        float distFromCenterSq01 = math.saturate(math.lengthsq(p - volumeCenter) / (safeHalfExtent * safeHalfExtent));
        float localizedAo = ambientOcclusionValues.IsCreated && idx < ambientOcclusionValues.Length
            ? math.saturate(ambientOcclusionValues[idx])
            : 1f;
        float caveCenterAo = math.saturate(0.52f + distFromCenterSq01 * 0.48f) * localizedAo;

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
        float4 colorPayload = new float4(caveCenterAo, caveCenterAo, caveCenterAo, 0f);
        if (TryResolveCaveMouthTerrainColor(p, out float4 terrainSplatColor, out float splatWeight))
        {
            float terrainLuma = math.saturate(math.dot(terrainSplatColor.xyz, new float3(0.299f, 0.587f, 0.114f)));
            float mouthAo = math.saturate(math.lerp(caveCenterAo, math.min(caveCenterAo, terrainLuma), splatWeight));
            colorPayload.xyz = new float3(mouthAo);
            colorPayload.w = splatWeight;
        }

        if (IsModifiedSdfCell(p))
            colorPayload.x = 1f;

        if (skirtAlphaValues.IsCreated && idx < skirtAlphaValues.Length)
            skirtAlphaValues[idx] = math.max(skirtAlphaValues[idx], skirtAlpha);

        colors[idx] = new Color(colorPayload.x, colorPayload.y, colorPayload.z, colorPayload.w);
    }

    bool IsModifiedSdfCell(float3 position)
    {
        if (!modifiedCells.IsCreated || voxelStep <= 0.0001f)
            return false;

        float invVoxelStep = math.rcp(voxelStep);
        float3 absolutePosition = position + absoluteUniverseOffset;
        int3 cell = (int3)math.floor(absolutePosition * invVoxelStep);
        return modifiedCells.ContainsKey(cell);
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
            float distanceSq = math.lengthsq(position - entrance.surfacePosition);
            float inner = radius * 0.35f;
            float outer = math.max(math.max(radius * 1.85f, voxelStep), 0.0001f);
            float outerSq = outer * outer;
            float localWeight = (1f - math.smoothstep(inner * inner, outerSq, distanceSq)) * blend;
            if (localWeight <= weight)
                continue;

            weight = localWeight;
            float mouthDarkening = math.saturate(1f - distanceSq * math.rcp(outerSq)) * blend * 0.58f;
            terrainColor = math.saturate(entrance.terrainSplatColor);
            terrainColor.xyz *= 1f - mouthDarkening;
        }

        return weight > 0.0001f;
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
public struct VoxelDirtyBlendJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly] public NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells;
    public float voxelStep;
    public float3 absoluteUniverseOffset;
    [WriteOnly, NoAlias] public NativeArray<float> dirtyBlendValues;

    public void Execute(int index)
    {
        if (!dirtyBlendValues.IsCreated || index < 0 || index >= dirtyBlendValues.Length)
            return;

        if (!modifiedCells.IsCreated || !positions.IsCreated || index >= positions.Length || voxelStep <= 0.0001f)
        {
            dirtyBlendValues[index] = 0f;
            return;
        }

        float invVoxelStep = math.rcp(voxelStep);
        float3 absolutePosition = positions[index] + absoluteUniverseOffset;
        int3 cell = (int3)math.floor(absolutePosition * invVoxelStep);
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

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSpawnPointJob : IJob
{
    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float3> normals;

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

    /// <summary>Output: floor spawn data with deterministic hash IDs. Owner job clamps writes to Capacity.</summary>
    public NativeList<CaveSpawnData> spawnPoints;

    public void Execute()
    {
        int count = math.min(positions.IsCreated ? positions.Length : 0, normals.IsCreated ? normals.Length : 0);
        for (int idx = 0; idx < count; idx++)
            TryAddSpawnPoint(idx);
    }

    void TryAddSpawnPoint(int idx)
    {
        if (!spawnPoints.IsCreated || spawnPoints.Length >= spawnPoints.Capacity)
            return;

        float3 pos = positions[idx];
        float3 nrm = normals[idx];

        // ── Filter 1: Floor normal ──
        float upDot = math.dot(nrm, new float3(0, 1, 0));
        if (upDot < floorNormalThreshold)
            return;

        // ── Filter 2: Interior depth ──
        if (minInteriorDepth > 1f)
            return;

        if (minInteriorDepth > 0f)
        {
            float maxInteriorRadius = math.max(volumeHalfExtent, 1f) * math.max(0f, 1f - minInteriorDepth);
            if (math.lengthsq(pos - volumeCenter) > maxInteriorRadius * maxInteriorRadius)
                return;
        }

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

public class HectonVoxelEngine : MonoBehaviour, Hecton8.Core.Contracts.IVoxelSonarSdfReadModel
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
    private const int DeferredVoxelPhysicsBakeEmergencyTeardownCapacity = 512;
    private const int DeferredVoxelColliderUploadCapacity = 2048;
    private const float DeferredVoxelColliderUploadBudgetPerFrame = 1f;
    private const float DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame = 4f;
    private const float DeferredVoxelColliderUploadBurstCapBias = 0.5f;
    private const int DeferredVoxelColliderUploadBackpressureBudget = 8;
    private const int DeferredVoxelColliderUploadRetryLimit = 4;
    private const int DeferredVoxelColliderUploadDropWarningReleaseThreshold = DeferredVoxelColliderUploadCapacity / 2;
    private const float VoxelMeshUploadBudgetPerFrame = 1f;
    private const float VoxelMeshUploadBudgetVisualOverkillPerFrame = 3f;
    private const float VoxelMeshUploadBurstCapBias = 0.5f;
    private const byte DeferredVoxelColliderUploadVolumeFlag = 1 << 0;
    private static readonly long ChunkGenerationFrameBudgetTicks = Stopwatch.Frequency / 500L;
    private static readonly double _JobAdmissionStopwatchMillisecondsPerTick = 1000.0d / Stopwatch.Frequency;
    private const byte DeferredVoxelBakeDestroyOwner = 1 << 0;
    private const float VoxelLodColliderDisableDistanceMeters = 200f;
    private const float VoxelPressureColliderDisableDistanceMeters = 120f;
    private const float VoxelColliderFakePressureFactor = 0.85f;
    private const float VoxelPhysicsBakeProxyMinHeightMeters = 1f;
    private const float VoxelTerrainSnapHysteresisMeters = 0.05f;
    private const string VoxelBakeProxyRuntimeName = "VoxelBakeProxy";
    private const float OverhangCameraCullDotThreshold = -0.3f;
    private const float PredictiveVoxelProxyMinSpeedMetersPerSecond = 1f;
    private const float PredictiveVoxelProxyMaxDistanceMeters = 12f;
    private const float PredictiveVoxelProxyLookaheadSeconds = 0.35f;
    private const float PredictiveVoxelProxyDampenerStrength01 = 0.35f;
    private const float PredictiveVoxelProxyCinematicPaddingMeters = 0.75f;
    private const int VoxelSurfaceMeshPoolSize = 256;
    private const int VoxelPhysicsBakeMeshPoolSize = 256;
    private const int VoxelMeshPoolAcquireWarmupRetryFrames = 4;
    private const string VoxelSurfacePoolMeshName = "VoxelSurfacePool";
    private const string VoxelPhysicsBakePoolMeshName = "VoxelPhysicsBakePool";
    private const float VoxelAnomalySolveWarningMs = 0.2f;
    private const int VoxelMeshPipelineBlackBoxCapacity = 300;
    private const SystemID VoxelMeshPipelineBlackBoxOwnerSystemId = SystemID.WorldStreaming;
    private const BufferID VoxelMeshPipelineBlackBoxBufferId = BufferID.VoxelMeshPipelineBlackBox;
    private const uint VoxelMeshPipelineInvalidStateFlag = 1u << 0;
    private const uint VoxelMeshPipelineInvalidMeshDataFlag = 1u << 1;
    private const uint VoxelMeshPipelineScratchCapacityOverflowFlag = 1u << 2;
    private const uint VoxelMeshPipelineEmergencyBakeTeardownFlag = 1u << 3;
    private const uint VoxelMeshPipelineVolumeSpawnPoolMissFlag = 1u << 4;
    private const uint VoxelMeshPipelineBlackBoxDumpMagic = 0x564D5042u; // VMPB
    private const string VoxelMeshPipelineBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_VOXEL_MESH_PIPELINE.bin";
    private const int BiomeHeatmapResolution = 256;
    private const int BiomeHeatmapMaxIndex = BiomeHeatmapResolution - 1;
    private const float VoxelChunkSkirtDepthMeters = 0.5f;
    private const float VoxelChunkSkirtWidthMeters = 1.25f;
    private const byte DeltaModeAdditive = 1 << 0;
    private const byte DeltaModeReplace = 1 << 1;
    private const string NativeMemoryOwner = nameof(HectonVoxelEngine);
    private const string ModifiedCellsNativeMemoryLabel = "VoxelPipelineData.ModifiedCells";
    private const string SpawnPointListNativeMemoryLabel = "VoxelPipelineData.SpawnPointList";
    private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
    private const Allocator DataVaultExemptVoxelPipelineScratchAllocator = Allocator.Persistent;
    private const Allocator DataVaultExemptVoxelSpawnPointAllocator = Allocator.Persistent;
    private static readonly uint _VoxelTeardownBackpressureWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.TeardownBackpressure"));
    private static readonly uint _VoxelPhysicsBakeForceReleaseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.ForceRelease"));
    private static readonly uint _VoxelColliderUploadDropWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.ColliderUpload.Drop"));
    private static readonly uint _VoxelColliderUploadRetryDropWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.ColliderUpload.RetryDrop"));
    private static readonly uint _VoxelPhysicsBakePoolExhaustedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.PhysicsBake.MeshPoolExhausted"));
    private static readonly uint _VoxelSurfaceMeshPoolExhaustedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.Surface.MeshPoolExhausted"));
    private static readonly uint _VoxelPhysicsBakeContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.PhysicsBake"));
    private static readonly uint _VoxelAnomalySolveWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.Anomaly.SolveBudgetExceeded"));
    private static readonly uint _VoxelAnomalyContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.AnomalySolve"));
    private static readonly uint _VoxelMeshPipelineContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelEngine.MeshPipeline"));
    private static readonly uint _VoxelChunksMeshedPerFrameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.MeshPipeline.ChunksMeshedPerFrame"));
    private static readonly uint _VoxelBakeQueueLengthHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Voxel.MeshPipeline.BakeQueueLength"));
    private static readonly uint _VoxelAlienBiomeHash = H8DataHash.ComputeFnv1A32("biome.alien");
    private static readonly uint _VoxelAlienShortBiomeHash = H8DataHash.ComputeFnv1A32("alien");
    private static readonly uint _VoxelAlienSurfaceHash = H8DataHash.ComputeFnv1A32("surface.alien");
    private static readonly uint _VoxelAlienHeatmapHash = H8DataHash.ComputeFnv1A32("heatmap.alien");
    private static readonly uint _VoxelAlienRadiationHash = H8DataHash.ComputeFnv1A32("radiation.alien");
    private static bool _voxelAnomalySolveWarningArmed;
    private static bool _voxelColliderUploadDropWarningArmed;
    private static bool _voxelColliderUploadRetryDropWarningArmed;
    private static int _voxelMeshTelemetryFrame = -1;
    private static int _voxelChunksMeshedThisFrame;
    private static int _voxelMeshPipelineBlackBoxCursor;
    private static bool _voxelMeshPipelineBlackBoxDumped;
    private static bool _voxelMeshPoolWarmupRunning;
    private static int _voxelMeshUploadFrame = -1;
    private static int _voxelMeshUploadsThisFrame;
    private static float _voxelMeshUploadBudgetTokens;
    private static int _deferredVoxelColliderUploadFrame = -1;
    private static float _deferredVoxelColliderUploadBudgetTokens;
    private static VaultGenerationHandle<VoxelMeshPipelineTelemetryEntry> _voxelMeshPipelineBlackBoxHandle;
    private static IDataVault _voxelMeshPipelineBlackBoxVault;

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
    const float ChthonicPillarRadiusMeters = 50f;
    const float ChthonicPillarHeightMeters = 1000f;
    const float ChthonicPillarEdgeWarpMeters = 24f;
    const float ChthonicPillarNoiseFrequency = 0.004f;
    const float ChthonicPillarMinimumProminenceMeters = 24f;
    const float ChthonicPillarTectonicBoundaryFrequencyFallback = 0.0065f;
    const uint ChthonicPillarTectonicBoundarySeedFallback = 83117u;
    const float ChthonicPillarMinimumTectonicBoundaryMask = 0.55f;
    const int ChthonicPillarColliderSegments = 24;
    // COLD ALLOC: float2[24] - smooth chthonic pillar collider unit circle LUT - owner: HectonVoxelEngine
    private static readonly float2[] _chthonicPillarColliderUnitCircle =
    {
        new float2(1f, 0f),
        new float2(0.9659258f, 0.258819f),
        new float2(0.8660254f, 0.5f),
        new float2(0.7071068f, 0.7071068f),
        new float2(0.5f, 0.8660254f),
        new float2(0.258819f, 0.9659258f),
        new float2(0f, 1f),
        new float2(-0.258819f, 0.9659258f),
        new float2(-0.5f, 0.8660254f),
        new float2(-0.7071068f, 0.7071068f),
        new float2(-0.8660254f, 0.5f),
        new float2(-0.9659258f, 0.258819f),
        new float2(-1f, 0f),
        new float2(-0.9659258f, -0.258819f),
        new float2(-0.8660254f, -0.5f),
        new float2(-0.7071068f, -0.7071068f),
        new float2(-0.5f, -0.8660254f),
        new float2(-0.258819f, -0.9659258f),
        new float2(0f, -1f),
        new float2(0.258819f, -0.9659258f),
        new float2(0.5f, -0.8660254f),
        new float2(0.7071068f, -0.7071068f),
        new float2(0.8660254f, -0.5f),
        new float2(0.9659258f, -0.258819f)
    };
    const float CliffOverhangSlopeThreshold = 1.7320508f;
    const float CliffOverhangLateralAmplitudeMeters = 1.25f;
    const float CliffOverhangNoiseFrequency = 0.075f;
    const float CliffOverhangBlendStrength = 0.55f;
    const int JOB_BATCH = 64;
    const int ActiveVolumeRegistryCapacity = 64;
    const int AirPocketRegistryCapacity = 64;
    const int MinimumStreamingSpawnPointScratchCapacity = 64;
    const int StreamingCaveGraphNodeScratchCapacity = 64;
    const int StreamingCaveGraphTunnelScratchCapacity = 128;
    const int StreamingCaveGraphEntranceScratchCapacity = 8;
    const int StreamingCaveGraphStructureScratchCapacity = 128;
    const int StreamingCraterStampScratchCapacity = 16;
    const double VoxelRebuildBudgetMilliseconds = 5.0d;
    const int VoxelRebuildBudgetStrikeFrames = 3;
    const uint VoxelRebuildLaneHash = 0x56584F4Cu;

    /// <summary>
    /// MC raw buffer multiplier. 2× totalCells instead of 15× (worst case).
    /// Atomic counter in MC job truncates gracefully if buffer fills.
    /// Saves ~85% peak memory allocation.
    /// </summary>
    const int MC_BUFFER_MULTIPLIER = 2;
    const int StreamingMeshRawVertexScratchLowTierCapacity = 262144;
    const int StreamingMeshRawVertexScratchMidTierCapacity = 524288;
    const int StreamingMeshRawVertexScratchVisualOverkillCapacity = 786432;
    const int StreamingSpatialBucketScratchCapacity = 512; // 8^3 max partition buckets.
    const int StreamingNodeSpatialReferenceScratchCapacity = StreamingCaveGraphNodeScratchCapacity * StreamingSpatialBucketScratchCapacity;
    const int StreamingTunnelSpatialReferenceScratchCapacity = StreamingCaveGraphTunnelScratchCapacity * StreamingSpatialBucketScratchCapacity;
    const int StreamingColliderChunkScratchCapacity = 8;

    // ── Internal ──
    static int _liveEngineCount;
    static int _activeGenerationOperations;
    static int _shutdownRequested;
    static int _voxelRebuildOverBudgetConsecutive;
    internal static HectonVoxelEngine ActiveRuntimeInstance => GlobalRegistry.VoxelEngine;
    private static int _airPocketCount;
    private static readonly Vector3[] _airPocketCenters = new Vector3[AirPocketRegistryCapacity]; // COLD ALLOC: Vector3[64] - fixed voxel air-pocket centers - owner: HectonVoxelEngine
    private static readonly Vector3[] _airPocketHalfExtents = new Vector3[AirPocketRegistryCapacity]; // COLD ALLOC: Vector3[64] - fixed voxel air-pocket AABB extents - owner: HectonVoxelEngine
    private static readonly float[] _airPocketRefillFractions = new float[AirPocketRegistryCapacity]; // COLD ALLOC: float[64] - fixed voxel air-pocket O2 refill scalars - owner: HectonVoxelEngine

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticRuntimeState()
    {
        FlushDeferredVoxelWorkWithoutDispatcher();
        _liveEngineCount = 0;
        _activeGenerationOperations = 0;
        _shutdownRequested = 0;
        _voxelRebuildOverBudgetConsecutive = 0;
        ClearAirPocketRegistry();
        _deferredVoxelPhysicsBakeTeardowns.Clear();
        ClearDeferredVoxelPhysicsBakeEmergencyTeardowns();
        _deferredVoxelColliderUploads.Clear();
        _deferredVoxelPhysicsBakeTeardownRegistered = false;
        _deferredVoxelColliderUploadRegistered = false;
        _deferredVoxelPhysicsBakeBackpressureActive = false;
        _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
        _deferredVoxelColliderUploadScanCursor = -1;
        _voxelColliderUploadDropWarningArmed = false;
        _voxelColliderUploadRetryDropWarningArmed = false;
        _voxelProxyLayerFilteringConfigured = false;
        _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
        _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
        _voxelMeshTelemetryFrame = -1;
        _voxelChunksMeshedThisFrame = 0;
        _voxelMeshPipelineBlackBoxDumped = false;
        DisposeVoxelMeshPipelineBlackBox();
        ResetPredictiveVoxelProxyCinematicState();
        ResetVoxelProxyLayerFilteringState();
        ResetVoxelMeshPoolState();
    }
    // COLD ALLOC: List<DeferredVoxelPhysicsBakeTeardown>[2048] - deferred voxel collider PhysX bake teardown queue - owner: HectonVoxelEngine
    private static readonly List<DeferredVoxelPhysicsBakeTeardown> _deferredVoxelPhysicsBakeTeardowns = new List<DeferredVoxelPhysicsBakeTeardown>(DeferredVoxelPhysicsBakeTeardownCapacity);
    // COLD ALLOC: DeferredVoxelPhysicsBakeTeardown[512] - fail-closed overflow lane for already-scheduled PhysX bake jobs - owner: HectonVoxelEngine
    private static readonly DeferredVoxelPhysicsBakeTeardown[] _deferredVoxelPhysicsBakeEmergencyTeardowns = new DeferredVoxelPhysicsBakeTeardown[DeferredVoxelPhysicsBakeEmergencyTeardownCapacity];
    private static int _deferredVoxelPhysicsBakeEmergencyCount;
    private static int _deferredVoxelPhysicsBakeEmergencyScanCursor;
    // COLD ALLOC: List<DeferredVoxelColliderUpload>[2048] - late-frame PhysX collider sharedMesh upload queue - owner: HectonVoxelEngine
    private static readonly List<DeferredVoxelColliderUpload> _deferredVoxelColliderUploads = new List<DeferredVoxelColliderUpload>(DeferredVoxelColliderUploadCapacity);
    // COLD ALLOC: Mesh[256] - global voxel surface mesh pool preallocated at engine boot - owner: HectonVoxelEngine
    private static readonly Mesh[] _voxelSurfaceMeshPool = new Mesh[VoxelSurfaceMeshPoolSize];
    // COLD ALLOC: bool[256] - occupancy flags for global voxel surface mesh pool - owner: HectonVoxelEngine
    private static readonly bool[] _voxelSurfaceMeshPoolInUse = new bool[VoxelSurfaceMeshPoolSize];
    private static int _voxelSurfaceMeshPoolInUseCount;
    // COLD ALLOC: Mesh[256] - global PhysX voxel bake mesh pool - owner: HectonVoxelEngine
    private static readonly Mesh[] _voxelPhysicsBakeMeshPool = new Mesh[VoxelPhysicsBakeMeshPoolSize];
    // COLD ALLOC: bool[256] - occupancy flags for global PhysX voxel bake mesh pool - owner: HectonVoxelEngine
    private static readonly bool[] _voxelPhysicsBakeMeshPoolInUse = new bool[VoxelPhysicsBakeMeshPoolSize];
    private static int _voxelPhysicsBakeMeshPoolInUseCount;
    // COLD ALLOC: DeferredVoxelPhysicsBakeTeardownDriver[1] - dispatcher late-frame adapter for voxel bake teardown - owner: HectonVoxelEngine
    private static readonly DeferredVoxelPhysicsBakeTeardownDriver _deferredVoxelPhysicsBakeTeardownDriver = new DeferredVoxelPhysicsBakeTeardownDriver();
    // COLD ALLOC: DeferredVoxelColliderUploadDriver[1] - dispatcher late-frame adapter for collider mesh assignment - owner: HectonVoxelEngine
    private static readonly DeferredVoxelColliderUploadDriver _deferredVoxelColliderUploadDriver = new DeferredVoxelColliderUploadDriver();
    // COLD ALLOC: DeferredVoxelDispatcherHotSwapBridge[1] - rebinds static voxel late-frame drivers after Dispatcher replacement - owner: HectonVoxelEngine
    private static readonly DeferredVoxelDispatcherHotSwapBridge _deferredVoxelDispatcherHotSwapBridge = new DeferredVoxelDispatcherHotSwapBridge();
    private static bool _deferredVoxelPhysicsBakeTeardownRegistered;
    private static bool _deferredVoxelColliderUploadRegistered;
    private static bool _deferredVoxelHotSwapRegistered;
    private static bool _deferredVoxelPhysicsBakeBackpressureActive;
    private static int _deferredVoxelPhysicsBakeTeardownScanCursor;
    private static int _deferredVoxelColliderUploadScanCursor = -1;
    private static int _predictiveVoxelProxyLastFrame = -1;
    private static bool _voxelProxyLayerFilteringConfigured;
    private static bool _voxelSurfaceMeshPoolExhaustedWarningArmed;
    private static bool _voxelPhysicsBakeMeshPoolExhaustedWarningArmed;
    private static int DeferredVoxelPhysicsBakePendingCount =>
        _deferredVoxelPhysicsBakeTeardowns.Count + _deferredVoxelPhysicsBakeEmergencyCount;

    private struct DeferredVoxelPhysicsBakeTeardown
    {
        public Mesh Mesh;
        public GameObject Owner;
        public MeshRenderer Renderer;
        public MeshCollider Collider;
        public BoxCollider ProxyCollider;
        public JobHandle Handle;
        public double3 ProxyMinAup;
        public double3 ProxyMaxAup;
        public uint ProxyShiftSequence;
        public byte Flags;
        public byte HasProxyBounds;
    }

    private struct DeferredVoxelColliderUpload
    {
        public Hecton8.Caves.HectonVoxelVolume Volume;
        public MeshCollider Collider;
        public BoxCollider ProxyCollider;
        public Mesh Mesh;
        public double3 ProxyMinAup;
        public double3 ProxyMaxAup;
        public uint ProxyShiftSequence;
        public int ChunkIndex;
        public byte Flags;
        public byte HasProxyBounds;
        public byte RetryCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct VoxelMeshPipelineTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;

        [FieldOffset(4)]
        public uint Flags;

        [FieldOffset(8)]
        public ushort ChunksMeshedThisFrame;

        [FieldOffset(10)]
        public ushort BakeQueueLength;

        [FieldOffset(12)]
        public ushort ColliderUploadQueueLength;

        [FieldOffset(14)]
        public ushort ActiveGenerationOperations;

        [FieldOffset(16)]
        public ushort SurfacePoolInUse;

        [FieldOffset(18)]
        public ushort PhysicsPoolInUse;

        [FieldOffset(20)]
        public uint StateHash;

        [FieldOffset(24)]
        public uint Padding0;

        [FieldOffset(28)]
        public uint Padding1;
    }

    private sealed class DeferredVoxelPhysicsBakeTeardownDriver : ILateFrameTickable
    {
        public void LateFrameTick()
        {
            ApplyPredictiveVoxelProxyCinematicGate();
            PublishVoxelMeshPipelineTelemetry();
            DrainDeferredVoxelPhysicsBakeTeardowns();
        }
    }

    private sealed class DeferredVoxelColliderUploadDriver : ILateFrameTickable
    {
        public void LateFrameTick()
        {
            ApplyPredictiveVoxelProxyCinematicGate();
            PublishVoxelMeshPipelineTelemetry();
            DrainDeferredVoxelColliderUploads();
        }
    }

    private sealed class DeferredVoxelDispatcherHotSwapBridge : IGlobalRegistryHotSwapListener
    {
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null)
                return;

            RebindDeferredVoxelLateFrameDrivers();
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

    private static bool IsFiniteFloat3(float3 value)
    {
        return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
    }

    private static bool IsFiniteColor(Color value)
    {
        return math.isfinite(value.r) &&
               math.isfinite(value.g) &&
               math.isfinite(value.b) &&
               math.isfinite(value.a);
    }

    private static float3 NormalizeFiniteOrUp(float3 value, ref bool invalidMeshData)
    {
        if (!IsFiniteFloat3(value))
        {
            invalidMeshData = true;
            return new float3(0f, 1f, 0f);
        }

        float lengthSq = math.lengthsq(value);
        if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
        {
            invalidMeshData = true;
            return new float3(0f, 1f, 0f);
        }

        return value * math.rsqrt(lengthSq);
    }

    private static float SanitizeFinite01(float value, float fallback, ref bool invalidMeshData)
    {
        if (math.isfinite(value))
            return math.saturate(value);

        invalidMeshData = true;
        return fallback;
    }

    private static Color SanitizeFiniteColor(Color value, ref bool invalidMeshData)
    {
        if (IsFiniteColor(value))
            return value;

        invalidMeshData = true;
        return Color.white;
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
        public NativeArray<float> OverhangDensityField;
        public NativeArray<sbyte> QuantizedDensityField;
        public NativeArray<AnomalyFeatureRecord> AnomalyFeatureRecords;
        public NativeArray<byte> AnomalyFissureMask;
        public NativeArray<AnomalyFeatureRecord> SelectedPillarFeature;
        public NativeArray<int> ChunkContentFlags;
        public NativeArray<int> CellVertexCounts;
        public NativeArray<int> CellVertexOffsets;
        public NativeArray<MCRawVertex> MeshRawVertices;
        public NativeArray<float3> MeshWeldedPositions;
        public NativeArray<int> MeshTriangleIndices;
        public NativeArray<int> MeshEdgeVertexX;
        public NativeArray<int> MeshEdgeVertexY;
        public NativeArray<int> MeshEdgeVertexZ;
        public NativeArray<int> MeshWeldedCounter;
        public NativeArray<float3> MeshNormals;
        public NativeArray<float> MeshCurvatureValues;
        public NativeArray<float> MeshAmbientOcclusionValues;
        public NativeArray<float> MeshBiomeValues;
        public NativeArray<float> MeshSkirtAlphaValues;
        public NativeArray<float> MeshDirtyBlendValues;
        public NativeArray<Color> MeshColors;
        public NativeArray<float3> ProjectedLocalPositions;
        public NativeArray<int> SpatialBucketCounts;
        public NativeArray<int> SpatialBucketWriteHeads;
        public NativeArray<int> SpatialNodeBucketOffsets;
        public NativeArray<int> SpatialNodeBucketIndices;
        public NativeArray<int> SpatialTunnelBucketOffsets;
        public NativeArray<int> SpatialTunnelBucketIndices;
        public NativeArray<CaveNode> RebuildNodes;
        public NativeArray<CaveTunnel> RebuildTunnels;
        public NativeArray<CaveEntrance> RebuildEntrances;
        public NativeArray<CaveStructure> RebuildStructures;
        public NativeArray<VoxelCraterStamp> RebuildCraterStamps;
        public NativeList<CaveSpawnData> SpawnPointListScratch;
        public int SpawnPointListScratchCapacity;
        public int SpawnPointListScratchMemoryId;
        public NativeParallelHashMap<int3, VoxelModifiedCell> ModifiedCellsScratch;
        public int ModifiedCellsScratchCapacity;
        public int ModifiedCellsScratchMemoryId;
        public NativeArray<byte> ColliderTriangleBuckets;
        public NativeArray<int> ColliderBucketCounts;
        public NativeArray<int> ColliderBucketOffsets;
        public NativeArray<int> ColliderBucketWriteHeads;
        public NativeArray<int> ColliderChunkTriangleIndices;
        public NativeArray<int> ColliderLocalRemap;
        public NativeArray<int> ColliderTouchedVertexGlobals;
        public NativeArray<float3> ColliderLocalPositions;
        public NativeArray<int> ColliderLocalIndices;
        public bool InUse;

        public void Dispose()
        {
            HectonVoxelEngine.DisposeTrackedNativeArray(ref TerrainHeights);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref GridBiome);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref DensityField);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SmoothDensityField);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref OverhangDensityField);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref QuantizedDensityField);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref AnomalyFeatureRecords);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref AnomalyFissureMask);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SelectedPillarFeature);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ChunkContentFlags);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref CellVertexCounts);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref CellVertexOffsets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshRawVertices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshWeldedPositions);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshTriangleIndices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshEdgeVertexX);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshEdgeVertexY);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshEdgeVertexZ);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshWeldedCounter);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshNormals);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshCurvatureValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshAmbientOcclusionValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshBiomeValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshSkirtAlphaValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshDirtyBlendValues);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref MeshColors);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ProjectedLocalPositions);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SpatialBucketCounts);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SpatialBucketWriteHeads);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SpatialNodeBucketOffsets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SpatialNodeBucketIndices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SpatialTunnelBucketOffsets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref SpatialTunnelBucketIndices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref RebuildNodes);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref RebuildTunnels);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref RebuildEntrances);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref RebuildStructures);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref RebuildCraterStamps);
            if (SpawnPointListScratch.IsCreated)
            {
                NativeMemorySentinel.Unregister(SpawnPointListScratchMemoryId);
                SpawnPointListScratch.Dispose(default);
                SpawnPointListScratch = default;
                SpawnPointListScratchCapacity = 0;
                SpawnPointListScratchMemoryId = 0;
            }
            if (ModifiedCellsScratch.IsCreated)
            {
                NativeMemorySentinel.Unregister(ModifiedCellsScratchMemoryId);
                ModifiedCellsScratch.Dispose(default);
                ModifiedCellsScratch = default;
                ModifiedCellsScratchCapacity = 0;
                ModifiedCellsScratchMemoryId = 0;
            }
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderTriangleBuckets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderBucketCounts);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderBucketOffsets);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderBucketWriteHeads);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderChunkTriangleIndices);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderLocalRemap);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderTouchedVertexGlobals);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderLocalPositions);
            HectonVoxelEngine.DisposeTrackedNativeArray(ref ColliderLocalIndices);
            InUse = false;
        }
    }

    struct VoxelStreamingScratchLease : System.IDisposable
    {
        internal HectonVoxelEngine _owner;
        internal int _slotIndex;

        public NativeArray<float> TerrainHeights;
        public NativeArray<float> GridBiome;
        public NativeArray<float> DensityField;
        public NativeArray<float> SmoothDensityField;
        public NativeArray<float> OverhangDensityField;
        public NativeArray<sbyte> QuantizedDensityField;
        public NativeArray<AnomalyFeatureRecord> AnomalyFeatureRecords;
        public NativeArray<byte> AnomalyFissureMask;
        public NativeArray<AnomalyFeatureRecord> SelectedPillarFeature;
        public NativeArray<int> ChunkContentFlags;
        public NativeArray<int> CellVertexCounts;
        public NativeArray<int> CellVertexOffsets;
        public NativeArray<MCRawVertex> MeshRawVertices;
        public NativeArray<float3> MeshWeldedPositions;
        public NativeArray<int> MeshTriangleIndices;
        public NativeArray<int> MeshEdgeVertexX;
        public NativeArray<int> MeshEdgeVertexY;
        public NativeArray<int> MeshEdgeVertexZ;
        public NativeArray<int> MeshWeldedCounter;
        public NativeArray<float3> MeshNormals;
        public NativeArray<float> MeshCurvatureValues;
        public NativeArray<float> MeshAmbientOcclusionValues;
        public NativeArray<float> MeshBiomeValues;
        public NativeArray<float> MeshSkirtAlphaValues;
        public NativeArray<float> MeshDirtyBlendValues;
        public NativeArray<Color> MeshColors;
        public NativeArray<float3> ProjectedLocalPositions;
        public NativeArray<int> SpatialBucketCounts;
        public NativeArray<int> SpatialBucketWriteHeads;
        public NativeArray<int> SpatialNodeBucketOffsets;
        public NativeArray<int> SpatialNodeBucketIndices;
        public NativeArray<int> SpatialTunnelBucketOffsets;
        public NativeArray<int> SpatialTunnelBucketIndices;
        public NativeArray<CaveNode> RebuildNodes;
        public NativeArray<CaveTunnel> RebuildTunnels;
        public NativeArray<CaveEntrance> RebuildEntrances;
        public NativeArray<CaveStructure> RebuildStructures;
        public NativeArray<VoxelCraterStamp> RebuildCraterStamps;
        public NativeList<CaveSpawnData> SpawnPointListScratch;
        public NativeParallelHashMap<int3, VoxelModifiedCell> ModifiedCellsScratch;
        public NativeArray<byte> ColliderTriangleBuckets;
        public NativeArray<int> ColliderBucketCounts;
        public NativeArray<int> ColliderBucketOffsets;
        public NativeArray<int> ColliderBucketWriteHeads;
        public NativeArray<int> ColliderChunkTriangleIndices;
        public NativeArray<int> ColliderLocalRemap;
        public NativeArray<int> ColliderTouchedVertexGlobals;
        public NativeArray<float3> ColliderLocalPositions;
        public NativeArray<int> ColliderLocalIndices;

        public bool IsValid => _owner != null && _slotIndex >= 0;

        public VoxelStreamingScratchLease(
            HectonVoxelEngine owner,
            int slotIndex,
            NativeArray<float> terrainHeights,
            NativeArray<float> gridBiome,
            NativeArray<float> densityField,
            NativeArray<float> smoothDensityField,
            NativeArray<float> overhangDensityField,
            NativeArray<sbyte> quantizedDensityField,
            NativeArray<AnomalyFeatureRecord> anomalyFeatureRecords,
            NativeArray<byte> anomalyFissureMask,
            NativeArray<AnomalyFeatureRecord> selectedPillarFeature,
            NativeArray<int> chunkContentFlags,
            NativeArray<int> cellVertexCounts,
            NativeArray<int> cellVertexOffsets,
            NativeArray<MCRawVertex> meshRawVertices,
            NativeArray<float3> meshWeldedPositions,
            NativeArray<int> meshTriangleIndices,
            NativeArray<int> meshEdgeVertexX,
            NativeArray<int> meshEdgeVertexY,
            NativeArray<int> meshEdgeVertexZ,
            NativeArray<int> meshWeldedCounter,
            NativeArray<float3> meshNormals,
            NativeArray<float> meshCurvatureValues,
            NativeArray<float> meshAmbientOcclusionValues,
            NativeArray<float> meshBiomeValues,
            NativeArray<float> meshSkirtAlphaValues,
            NativeArray<float> meshDirtyBlendValues,
            NativeArray<Color> meshColors,
            NativeArray<float3> projectedLocalPositions,
            NativeArray<int> spatialBucketCounts,
            NativeArray<int> spatialBucketWriteHeads,
            NativeArray<int> spatialNodeBucketOffsets,
            NativeArray<int> spatialNodeBucketIndices,
            NativeArray<int> spatialTunnelBucketOffsets,
            NativeArray<int> spatialTunnelBucketIndices,
            NativeArray<CaveNode> rebuildNodes,
            NativeArray<CaveTunnel> rebuildTunnels,
            NativeArray<CaveEntrance> rebuildEntrances,
            NativeArray<CaveStructure> rebuildStructures,
            NativeArray<VoxelCraterStamp> rebuildCraterStamps,
            NativeList<CaveSpawnData> spawnPointListScratch,
            NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCellsScratch,
            NativeArray<byte> colliderTriangleBuckets,
            NativeArray<int> colliderBucketCounts,
            NativeArray<int> colliderBucketOffsets,
            NativeArray<int> colliderBucketWriteHeads,
            NativeArray<int> colliderChunkTriangleIndices,
            NativeArray<int> colliderLocalRemap,
            NativeArray<int> colliderTouchedVertexGlobals,
            NativeArray<float3> colliderLocalPositions,
            NativeArray<int> colliderLocalIndices)
        {
            _owner = owner;
            _slotIndex = slotIndex;
            TerrainHeights = terrainHeights;
            GridBiome = gridBiome;
            DensityField = densityField;
            SmoothDensityField = smoothDensityField;
            OverhangDensityField = overhangDensityField;
            QuantizedDensityField = quantizedDensityField;
            AnomalyFeatureRecords = anomalyFeatureRecords;
            AnomalyFissureMask = anomalyFissureMask;
            SelectedPillarFeature = selectedPillarFeature;
            ChunkContentFlags = chunkContentFlags;
            CellVertexCounts = cellVertexCounts;
            CellVertexOffsets = cellVertexOffsets;
            MeshRawVertices = meshRawVertices;
            MeshWeldedPositions = meshWeldedPositions;
            MeshTriangleIndices = meshTriangleIndices;
            MeshEdgeVertexX = meshEdgeVertexX;
            MeshEdgeVertexY = meshEdgeVertexY;
            MeshEdgeVertexZ = meshEdgeVertexZ;
            MeshWeldedCounter = meshWeldedCounter;
            MeshNormals = meshNormals;
            MeshCurvatureValues = meshCurvatureValues;
            MeshAmbientOcclusionValues = meshAmbientOcclusionValues;
            MeshBiomeValues = meshBiomeValues;
            MeshSkirtAlphaValues = meshSkirtAlphaValues;
            MeshDirtyBlendValues = meshDirtyBlendValues;
            MeshColors = meshColors;
            ProjectedLocalPositions = projectedLocalPositions;
            SpatialBucketCounts = spatialBucketCounts;
            SpatialBucketWriteHeads = spatialBucketWriteHeads;
            SpatialNodeBucketOffsets = spatialNodeBucketOffsets;
            SpatialNodeBucketIndices = spatialNodeBucketIndices;
            SpatialTunnelBucketOffsets = spatialTunnelBucketOffsets;
            SpatialTunnelBucketIndices = spatialTunnelBucketIndices;
            RebuildNodes = rebuildNodes;
            RebuildTunnels = rebuildTunnels;
            RebuildEntrances = rebuildEntrances;
            RebuildStructures = rebuildStructures;
            RebuildCraterStamps = rebuildCraterStamps;
            SpawnPointListScratch = spawnPointListScratch;
            ModifiedCellsScratch = modifiedCellsScratch;
            ColliderTriangleBuckets = colliderTriangleBuckets;
            ColliderBucketCounts = colliderBucketCounts;
            ColliderBucketOffsets = colliderBucketOffsets;
            ColliderBucketWriteHeads = colliderBucketWriteHeads;
            ColliderChunkTriangleIndices = colliderChunkTriangleIndices;
            ColliderLocalRemap = colliderLocalRemap;
            ColliderTouchedVertexGlobals = colliderTouchedVertexGlobals;
            ColliderLocalPositions = colliderLocalPositions;
            ColliderLocalIndices = colliderLocalIndices;
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
        public double3 AbsoluteUniverseOffsetAtStartDouble;
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
        public int ModifiedCellsNativeMemoryId;
        public bool UsesStreamingScratchModifiedCells;
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
        public bool UsesStreamingScratchMeshBuffers;
        public bool UsesStreamingScratchAttributeBuffers;
        public bool UsesStreamingScratchSpatialBuckets;
        public bool UsesStreamingScratchSpawnPoints;
        public NativeList<CaveSpawnData> SpawnPointList;
        public int SpawnPointListNativeMemoryId;
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
                if (!UsesStreamingScratchModifiedCells)
                {
                    NativeMemorySentinel.Unregister(ModifiedCellsNativeMemoryId);
                    ModifiedCells.Dispose(default);
                }

                ModifiedCells = default;
                ModifiedCellsNativeMemoryId = 0;
                UsesStreamingScratchModifiedCells = false;
            }

            if (!UsesStreamingScratchMeshBuffers)
            {
                HectonVoxelEngine.DisposeTrackedNativeArray(ref RawVertices);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref WeldedPositions);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref TriangleIndices);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref EdgeVertexX);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref EdgeVertexY);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref EdgeVertexZ);
            }
            else
            {
                RawVertices = default;
                WeldedPositions = default;
                TriangleIndices = default;
                EdgeVertexX = default;
                EdgeVertexY = default;
                EdgeVertexZ = default;
            }

            if (!UsesStreamingScratchAttributeBuffers)
            {
                HectonVoxelEngine.DisposeTrackedNativeArray(ref Normals);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref CurvatureValues);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref AmbientOcclusionValues);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref BiomeValues);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref SkirtAlphaValues);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref DirtyBlendValues);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref Colors);
            }
            else
            {
                Normals = default;
                CurvatureValues = default;
                AmbientOcclusionValues = default;
                BiomeValues = default;
                SkirtAlphaValues = default;
                DirtyBlendValues = default;
                Colors = default;
            }
            if (SpawnPointList.IsCreated)
            {
                if (!UsesStreamingScratchSpawnPoints)
                {
                    NativeMemorySentinel.Unregister(SpawnPointListNativeMemoryId);
                    SpawnPointList.Dispose(default);
                }

                SpawnPointList = default;
                SpawnPointListNativeMemoryId = 0;
                UsesStreamingScratchSpawnPoints = false;
            }
            if (!UsesStreamingScratchSpatialBuckets)
            {
                HectonVoxelEngine.DisposeTrackedNativeArray(ref NodeBucketOffsets);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref NodeBucketIndices);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref TunnelBucketOffsets);
                HectonVoxelEngine.DisposeTrackedNativeArray(ref TunnelBucketIndices);
            }
            else
            {
                NodeBucketOffsets = default;
                NodeBucketIndices = default;
                TunnelBucketOffsets = default;
                TunnelBucketIndices = default;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    struct VoxelMeshBakeJob : IJob
    {
        public EntityId MeshId;

        public void Execute()
        {
            if (EntityId.ToULong(MeshId) != 0ul)
                UnityEngine.Physics.BakeMesh(MeshId, false);
        }
    }

    // ╔═══════════════════════════════════════════════╗
    // ║              LIFECYCLE                        ║
    // ╚═══════════════════════════════════════════════╝

    static bool TryScheduleVoxelPhysicsBake(in VoxelMeshBakeJob job, out JobHandle handle)
    {
        if (Application.isPlaying && !CanScheduleVoxelPhysicsBake())
        {
            handle = default;
            return false;
        }

        return job.TryScheduleAdmitted(JobAdmissionLane.Lane2_Voxel, default, out handle);
    }

    private static bool CanScheduleVoxelPhysicsBake()
    {
        if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())
            return false;

        if (DeferredVoxelPhysicsBakePendingCount < DeferredVoxelPhysicsBakeBackpressureThreshold)
            return true;

        UpdateDeferredVoxelPhysicsBakeBackpressure();
        return false;
    }

    static void ReportVoxelPhysicsBakeCompletion(long scheduleTimestamp)
    {
        float measuredMs = (float)((Stopwatch.GetTimestamp() - scheduleTimestamp) * _JobAdmissionStopwatchMillisecondsPerTick);
        JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<VoxelMeshBakeJob>(JobAdmissionLane.Lane2_Voxel, measuredMs);
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        _teardownStreamingScratchRequested = false;
        GlobalRegistry.RegisterVoxelEngineRuntime(this);
        if (!_registeredLiveEngine)
        {
            Interlocked.Increment(ref _liveEngineCount);
            _registeredLiveEngine = true;
        }

        EnsureVoxelProxyLayerFiltering();
        EnsureVoxelBakeGhostMaterial();
        EnsureVoxelMeshPipelineBlackBox();
        HectonVoxelVolume.TryEnsurePublishedSonarVaultPayloadCapacity(GlobalRegistry.DataVault);
        _ = WarmVoxelMeshPoolsAsync(destroyCancellationToken);
        _deltaProcessor = GetComponent<VoxelDeltaProcessor>();
        if (_deltaProcessor == null)
            _deltaProcessor = gameObject.AddComponent<VoxelDeltaProcessor>();

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
        NativeArray<VoxelCraterStamp> generationCraterScratch = default;
        VoxelStreamingScratchLease generationScratchLease = default;
        VoxelPipelineData pipelineData = null;
        bool usesStreamingScratchGraphSnapshots = false;

        try
        {
            if (mapMagicBridge == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
#endif
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
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 absoluteUniverseOffsetAtStartDouble))
                return null;

            Vector3 absoluteUniverseOffsetAtStart = ToVector3(absoluteUniverseOffsetAtStartDouble);
            uint shiftEpochAtStart = HectonFloatingOrigin.CurrentShiftSequence;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            CaveGenerationParams caveParams = preset.ToGenerationParams(seed);
            if (!CaveGraphGenerator.TryMeasure(
                seed,
                preset,
                worldCenter,
                terrainHeightCenter,
                volumeHalfExtent,
                out CaveGraphGenerator.CaveGraphCounts caveGraphCounts))
            {
                return null;
            }

            generationScratchLease = await AcquireStreamingScratchLeaseAsync(ptsX * ptsZ, totalPts, totalCells, gridDim, ct);
            if (!generationScratchLease.IsValid)
                return null;

            if (!TryPrepareRebuildGraphScratch(
                    ref generationScratchLease,
                    caveGraphCounts.Nodes,
                    caveGraphCounts.Tunnels,
                    caveGraphCounts.Entrances,
                    caveGraphCounts.Structures,
                    0,
                    out caveNodes,
                    out caveTunnels,
                    out caveEntrances,
                    out caveStructures,
                    out generationCraterScratch))
            {
                generationScratchLease.Dispose();
                return null;
            }

            usesStreamingScratchGraphSnapshots = true;

            if (!CaveGraphGenerator.TryFill(
                    seed,
                    preset,
                    worldCenter,
                    terrainHeightCenter,
                    volumeHalfExtent,
                    caveNodes,
                    caveTunnels,
                    caveEntrances,
                    caveStructures,
                    out CaveGraphGenerator.CaveGraphCounts filledCaveGraphCounts) ||
                filledCaveGraphCounts.Nodes != caveGraphCounts.Nodes ||
                filledCaveGraphCounts.Tunnels != caveGraphCounts.Tunnels ||
                filledCaveGraphCounts.Entrances != caveGraphCounts.Entrances ||
                filledCaveGraphCounts.Structures != caveGraphCounts.Structures)
            {
                return null;
            }

#if UNITY_EDITOR
            CaveGraphGenerator.Validate(caveNodes, caveTunnels, caveEntrances, worldCenter, volumeHalfExtent);
#endif

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.Log(CaveGraphGenerator.GetSummary(caveNodes, caveTunnels, caveEntrances));
#endif

            pipelineData = new VoxelPipelineData
            {
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStart = absoluteUniverseOffsetAtStart,
                AbsoluteUniverseOffsetAtStartDouble = absoluteUniverseOffsetAtStartDouble,
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
                ScratchLease = generationScratchLease,
                Nodes = caveNodes,
                Tunnels = caveTunnels,
                Entrances = caveEntrances,
                Structures = caveStructures,
                CraterStamps = default
            };

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return null;

            GameObject targetGO = SpawnVolume();
            if (targetGO == null)
                return null;

            targetGO.name = RuntimeCaveVolumeName;
            if (!TryBindGeneratedVolumeForMeshPublication(targetGO, pipelineData))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            targetGO.transform.position = stableShift.RebaseCapturedRuntimePosition(Vector3.zero, absoluteUniverseOffsetAtStartDouble);

            if (!await ApplyVolumeMeshAsync(targetGO, pipelineData, stableShift, ct))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData postMeshShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            if (!await ConfigureVolumeRuntimeDataAsync(targetGO, seed, worldCenter, absoluteUniverseOffsetAtStart, absoluteUniverseOffsetAtStartDouble, preset, gridDim, voxelStep, clampedLodLevel, caveParams,
                    caveNodes, caveTunnels, caveEntrances, caveStructures,
                    pipelineData.ScratchLease.SmoothDensityField,
                    pipelineData.PtsX,
                    pipelineData.PtsY,
                    pipelineData.PtsZ,
                    (Vector3)pipelineData.VolumeOrigin,
                    pipelineData.VoxelStep,
                    pipelineData.BuildCollider,
                    ct))
            {
                DespawnVolume(targetGO);
                return null;
            }
            RegisterEntranceTerrainHoles(targetGO, caveEntrances, voxelStep, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble);
            RegisterActiveVolume(targetGO);
            RegisterPipelineSpawnPoints(worldCenter, caveParams.spawnContext, pipelineData.SpawnPointList, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble);

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.Log("[HectonVoxel] Cave volume generated.");
#endif
            return targetGO;
        }
        finally
        {
            pipelineData?.Dispose();
            if (pipelineData == null)
                generationScratchLease.Dispose();

            if (!usesStreamingScratchGraphSnapshots)
            {
                DisposeTrackedNativeArray(ref caveNodes);
                DisposeTrackedNativeArray(ref caveTunnels);
                DisposeTrackedNativeArray(ref caveEntrances);
                DisposeTrackedNativeArray(ref caveStructures);
            }
            else
            {
                caveNodes = default;
                caveTunnels = default;
                caveEntrances = default;
                caveStructures = default;
                generationCraterScratch = default;
            }

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
#if UNITY_EDITOR
                Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
#endif
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
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 absoluteUniverseOffsetAtStartDouble))
                return null;

            Vector3 absoluteUniverseOffsetAtStart = ToVector3(absoluteUniverseOffsetAtStartDouble);
            uint shiftEpochAtStart = HectonFloatingOrigin.CurrentShiftSequence;

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            pipelineData = new VoxelPipelineData
            {
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStart = absoluteUniverseOffsetAtStart,
                AbsoluteUniverseOffsetAtStartDouble = absoluteUniverseOffsetAtStartDouble,
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

#if UNITY_EDITOR
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    "preawait");
            }
#endif

            if (!await ExecuteVoxelPipelineAsync(pipelineData, ct))
                return null;

#if UNITY_EDITOR
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    "surface-data");
            }
#endif

            GameObject targetGO = SpawnVolume();
            if (targetGO == null)
                return null;

            targetGO.name = RuntimeCaveVolumeName;
            if (!TryBindGeneratedVolumeForMeshPublication(targetGO, pipelineData))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            targetGO.transform.position = stableShift.RebaseCapturedRuntimePosition(Vector3.zero, absoluteUniverseOffsetAtStartDouble);

            if (!await ApplyVolumeMeshAsync(targetGO, pipelineData, stableShift, ct))
            {
                DespawnVolume(targetGO);
                return null;
            }

            OriginShiftEventData postMeshShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            if (!await ConfigureVolumeRuntimeDataAsync(targetGO, caveParams.seed, worldCenter, absoluteUniverseOffsetAtStart, absoluteUniverseOffsetAtStartDouble, null, gridDim, voxelStep, clampedLodLevel, caveParams,
                    nodes, tunnels, entrances, structures,
                    pipelineData.ScratchLease.SmoothDensityField,
                    pipelineData.PtsX,
                    pipelineData.PtsY,
                    pipelineData.PtsZ,
                    (Vector3)pipelineData.VolumeOrigin,
                    pipelineData.VoxelStep,
                    pipelineData.BuildCollider,
                    ct))
            {
                DespawnVolume(targetGO);
                return null;
            }
            RegisterEntranceTerrainHoles(targetGO, entrances, voxelStep, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble);
            RegisterActiveVolume(targetGO);
            RegisterPipelineSpawnPoints(worldCenter, caveParams.spawnContext, pipelineData.SpawnPointList, absoluteUniverseOffsetAtStartDouble, postMeshShift.NewTotalOffsetDouble);

#if UNITY_EDITOR
            if (RuntimeDiagnosticsTrace.IsActive)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.pipeline",
                    "mesh-build");
            }

            Hecton8.Core.H8Debug.Log("[HectonVoxel] Data volume generated.");
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
        VoxelStreamingScratchLease rebuildScratchLease = default;
        VoxelPipelineData pipelineData = null;
        bool usesStreamingScratchGraphSnapshots = false;

        try
        {
            if (volume == null || !volume.HasRuntimeData || !volume.MatchesRuntimeStamp(expectedRuntimeStamp))
                return false;

            if (mapMagicBridge == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[HectonVoxel] No MapMagicBridge assigned!");
#endif
                return false;
            }

            MCTables.Initialize();

            OriginShiftEventData stableShift = await HectonFloatingOrigin.WaitForShiftStabilityAsync(ct);
            int lodLevel = math.clamp(volume.LODLevel, 0, 2);
            int gridDim = math.clamp(volume.GridDimension, 16, 128);
            float voxelStep = math.max(volume.VoxelSize, 0.25f);
            double3 committedTotalOffsetDouble = stableShift.NewTotalOffsetDouble;
            Vector3 committedTotalOffset = ToVector3(committedTotalOffsetDouble);
            Vector3 worldCenter = HectonFloatingOrigin.ToRuntimePosition(volume.GenerationAbsoluteUniversePositionDouble, committedTotalOffsetDouble);
            CaveGenerationParams caveParams = volume.CaveParams;
            float lodTransitionBand = lodLevel > 0 ? math.max(voxelStep * 1.25f, 0.5f) : 0f;
            float effectiveSealMargin = math.max(sealMargin, TerrainVoxelSeamTransitionBand) + lodTransitionBand;
            int ptsX = gridDim + 1;
            int ptsY = gridDim + 1;
            int ptsZ = gridDim + 1;
            int totalPts = ptsX * ptsY * ptsZ;
            int totalCells = gridDim * gridDim * gridDim;
            int maxVerts = totalCells * MC_BUFFER_MULTIPLIER;
            float volumeHalfExtent = gridDim * voxelStep * 0.5f;
            float3 actualSize = new float3(gridDim, gridDim, gridDim) * voxelStep;
            float3 volumeOrigin = (float3)worldCenter - actualSize * 0.5f;

            CaveNode[] nodeSnapshot = volume.Nodes;
            CaveTunnel[] tunnelSnapshot = volume.Tunnels;
            CaveEntrance[] entranceSnapshot = volume.Entrances;
            CaveStructure[] structureSnapshot = volume.Structures;
            VoxelCraterStamp[] craterSnapshot = volume.CraterStamps;
            int craterCount = volume.CraterStampCount;
            int nodeCount = nodeSnapshot != null ? nodeSnapshot.Length : 0;
            int tunnelCount = tunnelSnapshot != null ? tunnelSnapshot.Length : 0;
            int entranceCount = entranceSnapshot != null ? entranceSnapshot.Length : 0;
            int structureCount = structureSnapshot != null ? structureSnapshot.Length : 0;
            int safeCraterCount = math.clamp(craterCount, 0, craterSnapshot != null ? craterSnapshot.Length : 0);

            rebuildScratchLease = await AcquireStreamingScratchLeaseAsync(ptsX * ptsZ, totalPts, totalCells, gridDim, ct);
            if (!rebuildScratchLease.IsValid)
                return false;

            if (!TryPrepareRebuildGraphScratch(
                    ref rebuildScratchLease,
                    nodeCount,
                    tunnelCount,
                    entranceCount,
                    structureCount,
                    safeCraterCount,
                    out nodes,
                    out tunnels,
                    out entrances,
                    out structures,
                    out craterStamps))
            {
                rebuildScratchLease.Dispose();
                return false;
            }

            usesStreamingScratchGraphSnapshots = true;

            for (int i = 0; i < nodeCount; i++)
            {
                CaveNode node = nodeSnapshot[i];
                node.position -= (float3)committedTotalOffset;
                nodes[i] = node;
            }
            for (int i = 0; i < tunnelCount; i++)
            {
                CaveTunnel tunnel = tunnelSnapshot[i];
                tunnel.pointA -= (float3)committedTotalOffset;
                tunnel.pointB -= (float3)committedTotalOffset;
                tunnels[i] = tunnel;
            }
            for (int i = 0; i < entranceCount; i++)
            {
                CaveEntrance entrance = entranceSnapshot[i];
                entrance.surfacePosition -= (float3)committedTotalOffset;
                entrances[i] = entrance;
            }
            for (int i = 0; i < structureCount; i++)
            {
                CaveStructure structure = structureSnapshot[i];
                structure.position -= (float3)committedTotalOffset;
                structure.pointB -= (float3)committedTotalOffset;
                structures[i] = structure;
            }
            for (int i = 0; i < safeCraterCount; i++)
            {
                VoxelCraterStamp crater = craterSnapshot[i];
                crater.position -= committedTotalOffset;
                craterStamps[i] = crater;
            }

            float terrainHeightCenter = worldCenter.y - 10f;
            if (mapMagicBridge.TryGetHeight(worldCenter.x, worldCenter.z, out float sampledHeight))
                terrainHeightCenter = sampledHeight;

            pipelineData = new VoxelPipelineData
            {
                SourceVolume = volume,
                SourceRuntimeStamp = expectedRuntimeStamp,
                WorldCenter = worldCenter,
                AbsoluteUniverseOffsetAtStart = committedTotalOffset,
                AbsoluteUniverseOffsetAtStartDouble = committedTotalOffsetDouble,
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
                ScratchLease = rebuildScratchLease,
                Nodes = nodes,
                Tunnels = tunnels,
                Entrances = entrances,
                Structures = structures,
                CraterStamps = craterStamps
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
            if (pipelineData == null)
                rebuildScratchLease.Dispose();

            if (!usesStreamingScratchGraphSnapshots)
            {
                DisposeTrackedNativeArray(ref nodes);
                DisposeTrackedNativeArray(ref tunnels);
                DisposeTrackedNativeArray(ref entrances);
                DisposeTrackedNativeArray(ref structures);
                DisposeTrackedNativeArray(ref craterStamps);
            }
            else
            {
                nodes = default;
                tunnels = default;
                entrances = default;
                structures = default;
                craterStamps = default;
            }

            EndGenerationOperation();
        }
    }

    void RegisterActiveVolume(GameObject volumeObject)
    {
        if (volumeObject == null)
            return;

        if (FindActiveVolumeIndex(volumeObject) >= 0)
            return;

        HectonVoxelVolume voxelVolume = null;
        volumeObject.TryGetComponent(out voxelVolume);

        if (_activeVolumes.Count >= ActiveVolumeRegistryCapacity)
        {
            int evictionIndex = SelectActiveVolumeEvictionIndex(voxelVolume);
            if (evictionIndex >= 0 && evictionIndex < _activeVolumes.Count)
            {
                GameObject evictedVolume = _activeVolumes[evictionIndex];
                if (evictedVolume != null && !ReferenceEquals(evictedVolume, volumeObject))
                    DespawnVolume(evictedVolume);
                else
                    RemoveActiveVolumeAt(evictionIndex);
            }

            if (_activeVolumes.Count >= ActiveVolumeRegistryCapacity)
                return;
        }

        Bounds localBounds = default;
        bool hasLocalBounds = false;
        MeshFilter meshFilter = voxelVolume != null ? voxelVolume.CachedMeshFilter : null;
        if (meshFilter == null)
            volumeObject.TryGetComponent(out meshFilter);

        if (meshFilter != null && meshFilter.sharedMesh != null)
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

    int SelectActiveVolumeEvictionIndex(HectonVoxelVolume incomingVolume)
    {
        int selectedIndex = _activeVolumes.Count > 0 ? 0 : -1;
        if (incomingVolume == null || _activeVolumes.Count <= 1)
            return selectedIndex;

        double3 incomingPosition = incomingVolume.GenerationAbsoluteUniversePositionDouble;
        if (!math.all(math.isfinite(incomingPosition)))
            return selectedIndex;

        double bestDistanceSq = double.NegativeInfinity;
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            HectonVoxelVolume candidate = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (candidate == null || !candidate.HasRuntimeData)
                return i;

            double3 candidatePosition = candidate.GenerationAbsoluteUniversePositionDouble;
            if (!math.all(math.isfinite(candidatePosition)))
                return i;

            double dx = candidatePosition.x - incomingPosition.x;
            double dz = candidatePosition.z - incomingPosition.z;
            double distanceSq = dx * dx + dz * dz;
            if (distanceSq <= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            selectedIndex = i;
        }

        return selectedIndex;
    }

    int FindActiveVolumeIndex(GameObject volumeObject)
    {
        for (int i = _activeVolumes.Count - 1; i >= 0; i--)
        {
            if (_activeVolumes[i] == volumeObject)
                return i;
        }

        return -1;
    }

    void UnregisterActiveVolume(GameObject volumeObject)
    {
        int index = FindActiveVolumeIndex(volumeObject);
        if (index >= 0)
            RemoveActiveVolumeAt(index);
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
    internal int DespawnVolumesInsideAbsoluteXZ(double minX, double maxX, double minZ, double maxZ)
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

            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (volume == null || !volume.HasRuntimeData)
                continue;

            AbsoluteUniversePosition volumeAup = AbsoluteUniversePosition.FromAbsolutePosition(volume.GenerationAbsoluteUniversePositionDouble);
            double3 resolvedPosition = volumeAup.ToAbsoluteDouble3();
            if (resolvedPosition.x < minX || resolvedPosition.x > maxX ||
                resolvedPosition.z < minZ || resolvedPosition.z > maxZ)
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
        int activeIndex = FindActiveVolumeIndex(volume);
        HectonVoxelVolume voxelVolume = activeIndex >= 0 && activeIndex < _activeVolumeComponents.Count
            ? _activeVolumeComponents[activeIndex]
            : null;
        MeshFilter mf = voxelVolume != null ? voxelVolume.CachedMeshFilter : null;
        MeshCollider mc = voxelVolume != null ? voxelVolume.CachedRootMeshCollider : null;
        if (mf == null)
            volume.TryGetComponent(out mf);
        if (mc == null)
            volume.TryGetComponent(out mc);

        if (activeIndex >= 0)
            RemoveActiveVolumeAt(activeIndex);
        else
            UnregisterActiveVolume(volume);

        HectonFloatingOrigin.MarkShiftTargetsDirty();

        if (mc != null) mc.enabled = false;

        IObjectPoolService pool = GlobalRegistry.ObjectPoolService;
        if (pool != null && voxelVolumePrefab != null)
        {
            VoxelVolumeLeakSentinel.MarkReleasedToPool(voxelVolume);
            ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: false);
            pool.Despawn(volume);
        }
        else
        {
            VoxelVolumeLeakSentinel.MarkDestroyRequested(voxelVolume);
            ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: true);
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
                HectonVoxelVolume voxelVolume = _activeVolumeComponents.Count > i ? _activeVolumeComponents[i] : null;
                MeshFilter mf = voxelVolume != null ? voxelVolume.CachedMeshFilter : null;
                MeshCollider mc = voxelVolume != null ? voxelVolume.CachedRootMeshCollider : null;
                if (mf == null)
                    _activeVolumes[i].TryGetComponent(out mf);
                if (mc == null)
                    _activeVolumes[i].TryGetComponent(out mc);
                if (mc != null) mc.enabled = false;

                IObjectPoolService pool = GlobalRegistry.ObjectPoolService;
                if (pool != null && voxelVolumePrefab != null)
                {
                    VoxelVolumeLeakSentinel.MarkReleasedToPool(voxelVolume);
                    ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: false);
                    pool.Despawn(_activeVolumes[i]);
                }
                else
                {
                    VoxelVolumeLeakSentinel.MarkDestroyRequested(voxelVolume);
                    ReleaseOrDestroySurfaceMesh(mf, destroyIfUnpooled: true);
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
        if (!TryResolveRuntimeAup(worldPosition, out AbsoluteUniversePosition queryAup))
            return false;

        double bestSqrDistance = double.PositiveInfinity;

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

            AbsoluteUniversePosition volumeAup = AbsoluteUniversePosition.FromAbsolutePosition(volume.GenerationAbsoluteUniversePositionDouble);
            double sqrDistance = AbsoluteUniversePosition.DistanceSq(in volumeAup, in queryAup);
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            nearestVolume = volume;
        }

        return nearestVolume != null;
    }

    public bool TryReadNearestSonarSdf(
        float3 runtimeOrigin,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange)
    {
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        cellSize = default;
        sdfRange = 0f;
        if (!math.all(math.isfinite(runtimeOrigin)))
            return false;

        Vector3 origin = new Vector3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z);
        if (!TryReadNearestActiveSonarSdfPayload(
                origin,
                out NativeArray<byte>.ReadOnly payload,
                out Vector3Int dimensions,
                out Vector3 payloadOrigin,
                out Vector3 payloadCellSize,
                out float payloadRange,
                out int _))
        {
            return false;
        }

        encodedSdf = payload;
        gridDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
        volumeOrigin = new float3(payloadOrigin.x, payloadOrigin.y, payloadOrigin.z);
        cellSize = new float3(payloadCellSize.x, payloadCellSize.y, payloadCellSize.z);
        sdfRange = payloadRange;
        return encodedSdf.IsCreated &&
               math.all(gridDimensions > 0) &&
               math.all(math.isfinite(volumeOrigin)) &&
               math.all(math.isfinite(cellSize)) &&
               math.isfinite(sdfRange) &&
               sdfRange > 0f;
    }

    public bool TryRaymarchNearestSonarSdf(
        float3 runtimeOrigin,
        float3 runtimeDirection,
        float maxDistance,
        float stepMeters,
        out VoxelSonarSdfRaycastHit hit,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out int3 gridDimensions,
        out float3 volumeOrigin,
        out float3 cellSize,
        out float sdfRange)
    {
        hit = default;
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        cellSize = default;
        sdfRange = 0f;
        if (!math.all(math.isfinite(runtimeOrigin)) ||
            !math.all(math.isfinite(runtimeDirection)) ||
            !math.isfinite(maxDistance) ||
            maxDistance <= 0f)
        {
            return false;
        }

        Vector3 origin = new Vector3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z);
        Vector3 direction = new Vector3(runtimeDirection.x, runtimeDirection.y, runtimeDirection.z);
        float safeStepMeters = math.max(0.05f, math.isfinite(stepMeters) ? stepMeters : 0.05f);
        float bestDistance = float.MaxValue;
        bool resolved = false;

        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData)
            {
                continue;
            }

            if (!volume.TryRaymarchPublishedSdf(
                    origin,
                    direction,
                    maxDistance,
                    safeStepMeters,
                    out VoxelSdfRaycastHit candidateHit) ||
                candidateHit.Hit == 0 ||
                candidateHit.Distance >= bestDistance)
            {
                continue;
            }

            if (!volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte>.ReadOnly candidateSdf,
                    out Vector3Int candidateDimensions,
                    out Vector3 candidateOrigin,
                    out Vector3 candidateCellSize,
                    out float candidateRange,
                    out int candidateVersion))
            {
                continue;
            }

            bestDistance = candidateHit.Distance;
            hit.Point = new float3(candidateHit.Point.x, candidateHit.Point.y, candidateHit.Point.z);
            hit.Normal = new float3(candidateHit.Normal.x, candidateHit.Normal.y, candidateHit.Normal.z);
            hit.Distance = math.max(0f, candidateHit.Distance);
            hit.Density = math.isfinite(candidateHit.Density) ? candidateHit.Density : 0f;
            hit.Density01 = math.saturate(math.max(0f, hit.Density) * math.rcp(math.max(0.0001f, candidateRange)));
            hit.SdfRange = candidateRange;
            hit.Version = candidateVersion;
            hit.Flags = VoxelSonarSdfRaycastHit.FlagHit;
            encodedSdf = candidateSdf;
            gridDimensions = new int3(candidateDimensions.x, candidateDimensions.y, candidateDimensions.z);
            volumeOrigin = new float3(candidateOrigin.x, candidateOrigin.y, candidateOrigin.z);
            cellSize = new float3(candidateCellSize.x, candidateCellSize.y, candidateCellSize.z);
            sdfRange = candidateRange;
            resolved = true;
        }

        return resolved;
    }

    public bool TrySampleNearestSonarSdf(
        float3 runtimePosition,
        out float density,
        out float density01)
    {
        density = 0f;
        density01 = 0f;
        if (!math.all(math.isfinite(runtimePosition)))
            return false;

        Vector3 position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        float bestBoundsDistanceSq = float.MaxValue;
        bool resolved = false;

        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData ||
                !volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte>.ReadOnly _,
                    out Vector3Int dimensions,
                    out Vector3 payloadOrigin,
                    out Vector3 payloadCellSize,
                    out float _,
                    out int _) ||
                !volume.TrySampleSonarSdf(runtimePosition, out float candidateDensity, out float candidateDensity01))
            {
                continue;
            }

            float boundsDistanceSq = ResolveSdfPayloadBoundsDistanceSq(position, payloadOrigin, dimensions, payloadCellSize);
            if (boundsDistanceSq >= bestBoundsDistanceSq)
                continue;

            bestBoundsDistanceSq = boundsDistanceSq;
            density = candidateDensity;
            density01 = candidateDensity01;
            resolved = true;
        }

        return resolved;
    }

    private bool TryReadNearestActiveSonarSdfPayload(
        Vector3 runtimeOrigin,
        out NativeArray<byte>.ReadOnly encodedSdf,
        out Vector3Int gridDimensions,
        out Vector3 volumeOrigin,
        out Vector3 voxelCellSize,
        out float sdfRange,
        out int version)
    {
        encodedSdf = default;
        gridDimensions = default;
        volumeOrigin = default;
        voxelCellSize = default;
        sdfRange = 0f;
        version = 0;

        float bestDistanceSq = float.MaxValue;
        bool resolved = false;
        for (int i = 0; i < _activeVolumes.Count; i++)
        {
            GameObject activeVolume = _activeVolumes[i];
            HectonVoxelVolume volume = i < _activeVolumeComponents.Count ? _activeVolumeComponents[i] : null;
            if (activeVolume == null ||
                volume == null ||
                !volume.HasRuntimeData ||
                !volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte>.ReadOnly candidateSdf,
                    out Vector3Int candidateDimensions,
                    out Vector3 candidateOrigin,
                    out Vector3 candidateCellSize,
                    out float candidateSdfRange,
                    out int candidateVersion))
            {
                continue;
            }

            Vector3 center = candidateOrigin + new Vector3(
                candidateCellSize.x * math.max(0, candidateDimensions.x - 1) * 0.5f,
                candidateCellSize.y * math.max(0, candidateDimensions.y - 1) * 0.5f,
                candidateCellSize.z * math.max(0, candidateDimensions.z - 1) * 0.5f);
            float distanceSq = (center - runtimeOrigin).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            encodedSdf = candidateSdf;
            gridDimensions = candidateDimensions;
            volumeOrigin = candidateOrigin;
            voxelCellSize = candidateCellSize;
            sdfRange = candidateSdfRange;
            version = candidateVersion;
            resolved = true;
        }

        return resolved;
    }

    private static float ResolveSdfPayloadBoundsDistanceSq(
        Vector3 position,
        Vector3 origin,
        Vector3Int dimensions,
        Vector3 cellSize)
    {
        Vector3 max = origin + new Vector3(
            cellSize.x * math.max(0, dimensions.x - 1),
            cellSize.y * math.max(0, dimensions.y - 1),
            cellSize.z * math.max(0, dimensions.z - 1));
        float dx = position.x < origin.x ? origin.x - position.x : (position.x > max.x ? position.x - max.x : 0f);
        float dy = position.y < origin.y ? origin.y - position.y : (position.y > max.y ? position.y - max.y : 0f);
        float dz = position.z < origin.z ? origin.z - position.z : (position.z > max.z ? position.z - max.z : 0f);
        return dx * dx + dy * dy + dz * dz;
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
            GlobalRegistry.UnregisterVoxelEngineRuntime(this);

        if (_registeredLiveEngine)
        {
            _registeredLiveEngine = false;
            if (Interlocked.Decrement(ref _liveEngineCount) <= 0)
            {
                RequestSharedTableShutdown();
                ResetPredictiveVoxelProxyCinematicState();
                ResetVoxelProxyLayerFilteringState();
            }
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
        bool cancellationRequested = false;
        bool watchdogLogged = false;
        while (!handle.IsCompleted)
        {
            if (!cancellationRequested && ct.IsCancellationRequested)
                cancellationRequested = true;

            if (!watchdogLogged && waitFrames >= VoxelJobWaitWatchdogFrames)
            {
                LogVoxelJobWaitWatchdog(context, waitFrames);
                watchdogLogged = true;
            }

            waitFrames++;
            await AwaitableDebtMonitor.NextFrameAsync();
        }

        DispatcherJobSwap.TryFinalizeCompleted(ref handle);

        if (cancellationRequested)
            ct.ThrowIfCancellationRequested();
    }

    static async Awaitable<long> YieldIfChunkGenerationBudgetExpiredAsync(long frameStartTimestamp, CancellationToken ct)
    {
        if (Stopwatch.GetTimestamp() - frameStartTimestamp < ChunkGenerationFrameBudgetTicks)
            return frameStartTimestamp;

        await AwaitableDebtMonitor.NextFrameAsync(ct);
        ct.ThrowIfCancellationRequested();
        return Stopwatch.GetTimestamp();
    }

    static async Awaitable<bool> AwaitForPhysicsBakeCompletionOrDeferAsync(
        JobHandle handle,
        CancellationToken ct,
        string context,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags,
        BoxCollider proxyCollider = null)
    {
        int waitFrames = 0;
        while (!handle.IsCompleted)
        {
            if (ct.IsCancellationRequested)
            {
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags, proxyCollider);
                return false;
            }

            if (waitFrames >= VoxelJobWaitWatchdogFrames)
            {
                LogVoxelJobWaitWatchdog(context, waitFrames);
                EnqueueDeferredVoxelPhysicsBakeTeardown(handle, mesh, owner, renderer, collider, flags, proxyCollider);
                return false;
            }

            waitFrames++;
            await AwaitableDebtMonitor.NextFrameAsync();
        }

        return DispatcherJobSwap.TryFinalizeCompleted(ref handle);
    }

    static async Awaitable AwaitVoxelMeshUploadBudgetAsync(CancellationToken ct)
    {
        while (true)
        {
            int frame = Time.frameCount;
            if (_voxelMeshUploadFrame != frame)
            {
                _voxelMeshUploadFrame = frame;
                _voxelMeshUploadsThisFrame = 0;
                float frameBudget = ResolveVoxelMeshUploadBudgetPerFrame();
                float frameCap = Mathf.Clamp(
                    Mathf.Ceil(frameBudget - VoxelMeshUploadBurstCapBias),
                    VoxelMeshUploadBudgetPerFrame,
                    VoxelMeshUploadBudgetVisualOverkillPerFrame);
                _voxelMeshUploadBudgetTokens = Mathf.Min(frameCap, _voxelMeshUploadBudgetTokens + frameBudget);
            }

            if (_voxelMeshUploadBudgetTokens >= 1f)
            {
                _voxelMeshUploadBudgetTokens -= 1f;
                _voxelMeshUploadsThisFrame++;
                return;
            }

            await AwaitableDebtMonitor.NextFrameAsync(ct);
            ct.ThrowIfCancellationRequested();
        }
    }

    private static float ResolveVoxelMeshUploadBudgetPerFrame()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        return Mathf.Lerp(VoxelMeshUploadBudgetPerFrame, VoxelMeshUploadBudgetVisualOverkillPerFrame, smooth);
    }

    private static void EnsureVoxelProxyLayerFiltering()
    {
        if (_voxelProxyLayerFilteringConfigured)
            return;

        Physics.IgnoreLayerCollision(HectonLayerMasks.Player, HectonLayerMasks.VoxelProxy, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.Vehicle, HectonLayerMasks.VoxelProxy, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.PlayerVehicle, HectonLayerMasks.VoxelProxy, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.VoxelProxy, HectonLayerMasks.IgnoreRaycast, true);
        Physics.IgnoreLayerCollision(HectonLayerMasks.VoxelProxy, HectonLayerMasks.UI, true);

        _voxelProxyLayerFilteringConfigured = true;
    }

    private static void ResetVoxelProxyLayerFilteringState()
    {
        _voxelProxyLayerFilteringConfigured = false;
    }

    private static void ResetPredictiveVoxelProxyCinematicState()
    {
        _predictiveVoxelProxyLastFrame = -1;
    }

    private static void ApplyPredictiveVoxelProxyCinematicGate()
    {
        int frame = Time.frameCount;
        if (_predictiveVoxelProxyLastFrame == frame)
            return;

        _predictiveVoxelProxyLastFrame = frame;

        if (!HasDeferredVoxelProxyCandidate() ||
            !TryResolvePredictiveVoxelProxyTarget(
                out Rigidbody targetBody,
                out HectonPlayerMovement targetMovement,
                out ITransportPredictiveVoxelProxySource targetVehicle,
                out Vector3 velocity))
        {
            return;
        }

        float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
        float speedSq = math.lengthsq(velocity3);
        float minSpeedSq = PredictiveVoxelProxyMinSpeedMetersPerSecond * PredictiveVoxelProxyMinSpeedMetersPerSecond;
        if (speedSq <= minSpeedSq)
            return;

        float3 lookaheadOffset = velocity3 * PredictiveVoxelProxyLookaheadSeconds;
        float lookaheadSq = math.lengthsq(lookaheadOffset);
        float maxDistanceSq = PredictiveVoxelProxyMaxDistanceMeters * PredictiveVoxelProxyMaxDistanceMeters;
        if (lookaheadSq > maxDistanceSq)
            lookaheadOffset *= PredictiveVoxelProxyMaxDistanceMeters / math.max(LengthApprox(lookaheadOffset), 0.0001f);

        Vector3 origin = targetBody.worldCenterOfMass;
        Vector3 predicted = origin + new Vector3(lookaheadOffset.x, lookaheadOffset.y, lookaheadOffset.z);
        if (!PathIntersectsDeferredVoxelProxyAup(origin, predicted))
            return;

        ApplyPredictiveVoxelProxyDampener(targetBody, targetMovement, targetVehicle, velocity);
    }

    private static bool HasDeferredVoxelProxyCandidate()
    {
        return DeferredVoxelPhysicsBakePendingCount > 0 ||
               _deferredVoxelColliderUploads.Count > 0;
    }

    private static bool PathIntersectsDeferredVoxelProxyAup(Vector3 runtimeStart, Vector3 runtimeEnd)
    {
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup) ||
            !TryResolveRuntimeAbsoluteDouble(runtimeStart, in originAup, out double3 startAup) ||
            !TryResolveRuntimeAbsoluteDouble(runtimeEnd, in originAup, out double3 endAup))
        {
            return false;
        }

        double padding = PredictiveVoxelProxyCinematicPaddingMeters;
        double3 pathMin = math.min(startAup, endAup) - new double3(padding);
        double3 pathMax = math.max(startAup, endAup) + new double3(padding);
        uint currentShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;

        for (int i = 0; i < _deferredVoxelPhysicsBakeTeardowns.Count; i++)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[i];
            if (pending.ProxyShiftSequence != currentShiftSequence)
            {
                RefreshDeferredVoxelTeardownProxyBounds(ref pending, currentShiftSequence);
                _deferredVoxelPhysicsBakeTeardowns[i] = pending;
            }

            if (DeferredVoxelProxyIntersectsAupPath(
                    pending.ProxyMinAup,
                    pending.ProxyMaxAup,
                    pending.HasProxyBounds,
                    pathMin,
                    pathMax))
            {
                return true;
            }
        }

        for (int i = 0; i < _deferredVoxelPhysicsBakeEmergencyCount; i++)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[i];
            if (pending.ProxyShiftSequence != currentShiftSequence)
            {
                RefreshDeferredVoxelTeardownProxyBounds(ref pending, currentShiftSequence);
                _deferredVoxelPhysicsBakeEmergencyTeardowns[i] = pending;
            }

            if (DeferredVoxelProxyIntersectsAupPath(
                    pending.ProxyMinAup,
                    pending.ProxyMaxAup,
                    pending.HasProxyBounds,
                    pathMin,
                    pathMax))
            {
                return true;
            }
        }

        for (int i = 0; i < _deferredVoxelColliderUploads.Count; i++)
        {
            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[i];
            if (pending.ProxyShiftSequence != currentShiftSequence)
            {
                RefreshDeferredVoxelUploadProxyBounds(ref pending, currentShiftSequence);
                _deferredVoxelColliderUploads[i] = pending;
            }

            if (DeferredVoxelProxyIntersectsAupPath(
                    pending.ProxyMinAup,
                    pending.ProxyMaxAup,
                    pending.HasProxyBounds,
                    pathMin,
                    pathMax))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshDeferredVoxelTeardownProxyBounds(
        ref DeferredVoxelPhysicsBakeTeardown pending,
        uint currentShiftSequence)
    {
        pending.ProxyShiftSequence = currentShiftSequence;
        pending.HasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(
            pending.ProxyCollider,
            out pending.ProxyMinAup,
            out pending.ProxyMaxAup)
            ? (byte)1
            : (byte)0;
    }

    private static void RefreshDeferredVoxelUploadProxyBounds(
        ref DeferredVoxelColliderUpload pending,
        uint currentShiftSequence)
    {
        pending.ProxyShiftSequence = currentShiftSequence;
        pending.HasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(
            pending.ProxyCollider,
            out pending.ProxyMinAup,
            out pending.ProxyMaxAup)
            ? (byte)1
            : (byte)0;
    }

    private static bool DeferredVoxelProxyIntersectsAupPath(
        double3 proxyMinAup,
        double3 proxyMaxAup,
        byte hasProxyBounds,
        double3 pathMinAup,
        double3 pathMaxAup)
    {
        if (hasProxyBounds == 0)
            return false;

        return proxyMinAup.x <= pathMaxAup.x && proxyMaxAup.x >= pathMinAup.x &&
               proxyMinAup.y <= pathMaxAup.y && proxyMaxAup.y >= pathMinAup.y &&
               proxyMinAup.z <= pathMaxAup.z && proxyMaxAup.z >= pathMinAup.z;
    }

    private static bool TryCacheDeferredVoxelProxyAupBounds(BoxCollider proxy, out double3 proxyMinAup, out double3 proxyMaxAup)
    {
        proxyMinAup = default;
        proxyMaxAup = default;
        if (proxy == null)
            return false;

        Bounds bounds = proxy.bounds;
        if (bounds.size.sqrMagnitude <= 0.0001f)
            return false;

        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup) ||
            !TryResolveRuntimeAbsoluteDouble(bounds.min, in originAup, out double3 minAup) ||
            !TryResolveRuntimeAbsoluteDouble(bounds.max, in originAup, out double3 maxAup))
        {
            return false;
        }

        double padding = PredictiveVoxelProxyCinematicPaddingMeters;
        proxyMinAup = minAup - new double3(padding);
        proxyMaxAup = maxAup + new double3(padding);
        return true;
    }

    private static bool TryResolvePredictiveVoxelProxyTarget(
        out Rigidbody targetBody,
        out HectonPlayerMovement targetMovement,
        out ITransportPredictiveVoxelProxySource targetVehicle,
        out Vector3 velocity)
    {
        targetBody = null;
        targetMovement = null;
        targetVehicle = null;
        velocity = Vector3.zero;

        targetMovement = PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
            ? runtimeContext.PlayerMovement
            : null;
        if (targetMovement != null &&
            targetMovement.TryGetActiveTransportPlatform(out ITransportPlatform platform) &&
            platform != null)
        {
            targetVehicle = platform as ITransportPredictiveVoxelProxySource;
            if (targetVehicle != null && targetVehicle.TryResolvePredictiveVoxelProxy(out targetBody, out velocity))
            {
                return true;
            }

            if (platform is ISubmarineRuntimeContext submarineRuntimeContext &&
                submarineRuntimeContext.HullRigidbody != null)
            {
                targetBody = submarineRuntimeContext.HullRigidbody;
                velocity = HectonPlayerMotor.SafeVelocity(targetBody.linearVelocity);
                return true;
            }
        }

        targetBody = runtimeContext != null ? runtimeContext.PlayerRigidbody : null;
        if (targetBody == null)
            return false;

        velocity = targetMovement != null
            ? targetMovement.CurrentWorldVelocity
            : HectonPlayerMotor.SafeVelocity(targetBody.linearVelocity);
        return true;
    }

    private static void ApplyPredictiveVoxelProxyDampener(
        Rigidbody targetBody,
        HectonPlayerMovement targetMovement,
        ITransportPredictiveVoxelProxySource targetVehicle,
        Vector3 sampledVelocity)
    {
        if (targetBody == null || sampledVelocity.y >= -0.01f)
            return;

        if (targetVehicle != null)
        {
            targetVehicle.ApplyPredictiveVoxelProxyDampener(PredictiveVoxelProxyDampenerStrength01);
            return;
        }

        Vector3 upwardCorrection = Vector3.up * (-sampledVelocity.y * PredictiveVoxelProxyDampenerStrength01);
        if (targetMovement != null)
        {
            targetMovement.QueueSubsystemExternalVelocityChange(upwardCorrection);
            return;
        }

        Vector3 velocity = HectonPlayerMotor.SafeVelocity(targetBody.linearVelocity);
        if (velocity.y < 0f)
        {
            velocity.y = math.lerp(velocity.y, 0f, PredictiveVoxelProxyDampenerStrength01);
            Hecton8.Physics.PhysicsForceRouter.QueueLinearVelocitySet(targetBody, velocity);
        }
    }

    private static void EnqueueDeferredVoxelPhysicsBakeTeardown(
        JobHandle handle,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags,
        BoxCollider proxyCollider)
    {
        EnsureVoxelProxyLayerFiltering();
        DisableDeferredVoxelBakePresentation(owner, renderer, collider);
        if (proxyCollider != null)
        {
            proxyCollider.gameObject.layer = HectonLayerMasks.VoxelProxy;
            proxyCollider.enabled = true;
        }

        uint proxyShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
        bool hasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(proxyCollider, out double3 proxyMinAup, out double3 proxyMaxAup);
        DeferredVoxelPhysicsBakeTeardown pending = new DeferredVoxelPhysicsBakeTeardown
        {
            Mesh = mesh,
            Owner = owner,
            Renderer = renderer,
            Collider = collider,
            ProxyCollider = proxyCollider,
            Handle = handle,
            ProxyMinAup = proxyMinAup,
            ProxyMaxAup = proxyMaxAup,
            ProxyShiftSequence = proxyShiftSequence,
            Flags = flags,
            HasProxyBounds = hasProxyBounds ? (byte)1 : (byte)0
        };

        if (_deferredVoxelPhysicsBakeTeardowns.Count >= DeferredVoxelPhysicsBakeTeardownCapacity)
        {
            DrainCompletedDeferredVoxelPhysicsBakeTeardownsForCapacity();
            if (_deferredVoxelPhysicsBakeTeardowns.Count >= DeferredVoxelPhysicsBakeTeardownCapacity)
            {
                if (!TryEnqueueDeferredVoxelPhysicsBakeEmergencyTeardown(in pending))
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _VoxelPhysicsBakeForceReleaseWarningHash,
                        _VoxelPhysicsBakeContextHash,
                        DeferredVoxelPhysicsBakePendingCount);
                    WriteVoxelMeshPipelineBlackBoxSample(
                        unchecked((uint)Time.frameCount),
                        VoxelMeshPipelineInvalidStateFlag | VoxelMeshPipelineEmergencyBakeTeardownFlag,
                        _voxelChunksMeshedThisFrame,
                        DeferredVoxelPhysicsBakePendingCount,
                        _deferredVoxelColliderUploads.Count);
                    UpdateDeferredVoxelPhysicsBakeBackpressure();
                    PublishVoxelMeshPipelineTelemetry();
                    return;
                }

                if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())
                {
                    UpdateDeferredVoxelPhysicsBakeBackpressure();
                    PublishVoxelMeshPipelineTelemetry();
                    return;
                }

                UpdateDeferredVoxelPhysicsBakeBackpressure();
                PublishVoxelMeshPipelineTelemetry();
                return;
            }
        }

        _deferredVoxelPhysicsBakeTeardowns.Add(pending);

        if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())
        {
            UpdateDeferredVoxelPhysicsBakeBackpressure();
            PublishVoxelMeshPipelineTelemetry();
            return;
        }

        UpdateDeferredVoxelPhysicsBakeBackpressure();
        PublishVoxelMeshPipelineTelemetry();
    }

    private static void ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly(
        JobHandle handle,
        Mesh mesh,
        GameObject owner,
        MeshRenderer renderer,
        MeshCollider collider,
        byte flags,
        BoxCollider proxyCollider,
        bool publishWarning = true)
    {
        if (publishWarning)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelPhysicsBakeForceReleaseWarningHash,
                _VoxelPhysicsBakeContextHash,
                DeferredVoxelPhysicsBakePendingCount);
        }

        DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
        DisableDeferredVoxelBakePresentation(owner, renderer, collider);

        if (proxyCollider != null)
            proxyCollider.enabled = false;

        if (mesh != null)
        {
            mesh.Clear(false);
            if (!ReleaseVoxelPhysicsBakeMesh(mesh))
                DestroyDeferredVoxelObject(mesh);
        }

        if ((flags & DeferredVoxelBakeDestroyOwner) != 0 && owner != null)
            DestroyDeferredVoxelObject(owner);
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
        }
    }

    private static bool EnsureDeferredVoxelPhysicsBakeTeardownRegistered()
    {
        if (_deferredVoxelPhysicsBakeTeardownRegistered)
            return true;

        if (!CanRegisterDeferredVoxelLateFrameWork())
            return false;

        _deferredVoxelPhysicsBakeTeardownRegistered = GlobalRegistry.TryRegisterLateFrameTickable(
            _deferredVoxelPhysicsBakeTeardownDriver,
            PriorityLayer.Environment);
        if (_deferredVoxelPhysicsBakeTeardownRegistered)
            TryRegisterDeferredVoxelHotSwapBridge();
        return _deferredVoxelPhysicsBakeTeardownRegistered;
    }

    private static void DrainDeferredVoxelPhysicsBakeTeardowns()
    {
        int pendingCount = _deferredVoxelPhysicsBakeTeardowns.Count;
        if (pendingCount <= 0 && _deferredVoxelPhysicsBakeEmergencyCount <= 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            UpdateDeferredVoxelPhysicsBakeBackpressure();
            TryShutdownSharedTables();
            return;
        }

        int drainBudget = _deferredVoxelPhysicsBakeBackpressureActive
            ? DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget
            : DeferredVoxelPhysicsBakeTeardownDrainBudget;
        int inspectionBudget = _deferredVoxelPhysicsBakeBackpressureActive
            ? DeferredVoxelPhysicsBakeTeardownBackpressureInspectionBudget
            : DeferredVoxelPhysicsBakeTeardownInspectionBudget;
        if (pendingCount > 0 && inspectionBudget > pendingCount)
            inspectionBudget = pendingCount;

        if (pendingCount > 0 &&
            (_deferredVoxelPhysicsBakeTeardownScanCursor < 0 ||
            _deferredVoxelPhysicsBakeTeardownScanCursor >= pendingCount))
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
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
            {
                index--;
                continue;
            }

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
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

        if (_deferredVoxelPhysicsBakeEmergencyCount > 0 && drained < drainBudget)
            drained += DrainDeferredVoxelPhysicsBakeEmergencyTeardowns(drainBudget - drained, inspectionBudget);

        _deferredVoxelPhysicsBakeTeardownScanCursor = pendingCount > 0 ? index : 0;
        if (DeferredVoxelPhysicsBakePendingCount == 0)
        {
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            TryShutdownSharedTables();
        }

        UpdateDeferredVoxelPhysicsBakeBackpressure();
    }

    private static void DrainCompletedDeferredVoxelPhysicsBakeTeardownsForCapacity()
    {
        int drained = 0;
        for (int i = _deferredVoxelPhysicsBakeTeardowns.Count - 1;
             i >= 0 && drained < DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget;
             i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[i];
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
                continue;

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeTeardownAt(i);
            drained++;
        }

        DrainCompletedDeferredVoxelPhysicsBakeEmergencyTeardownsForCapacity();

        if (DeferredVoxelPhysicsBakePendingCount == 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
            UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            TryShutdownSharedTables();
        }
    }

    private static void FinalizeDeferredVoxelPhysicsBakeTeardown(ref DeferredVoxelPhysicsBakeTeardown pending)
    {
        if (pending.Collider != null)
        {
            pending.Collider.enabled = false;
        }

        if (pending.ProxyCollider != null)
            pending.ProxyCollider.enabled = false;

        if (pending.Mesh != null)
        {
            pending.Mesh.Clear(false);
            if (!ReleaseVoxelPhysicsBakeMesh(pending.Mesh))
                DestroyDeferredVoxelObject(pending.Mesh);
        }

        if ((pending.Flags & DeferredVoxelBakeDestroyOwner) != 0 && pending.Owner != null)
            DestroyDeferredVoxelObject(pending.Owner);
    }

    private static void RemoveDeferredVoxelPhysicsBakeTeardownAt(int index)
    {
        int lastIndex = _deferredVoxelPhysicsBakeTeardowns.Count - 1;
        if (index != lastIndex)
            _deferredVoxelPhysicsBakeTeardowns[index] = _deferredVoxelPhysicsBakeTeardowns[lastIndex];

        _deferredVoxelPhysicsBakeTeardowns.RemoveAt(lastIndex);
    }

    private static bool TryEnqueueDeferredVoxelPhysicsBakeEmergencyTeardown(in DeferredVoxelPhysicsBakeTeardown pending)
    {
        if (_deferredVoxelPhysicsBakeEmergencyCount >= DeferredVoxelPhysicsBakeEmergencyTeardownCapacity)
            DrainCompletedDeferredVoxelPhysicsBakeEmergencyTeardownsForCapacity();

        if (_deferredVoxelPhysicsBakeEmergencyCount >= DeferredVoxelPhysicsBakeEmergencyTeardownCapacity)
            return false;

        _deferredVoxelPhysicsBakeEmergencyTeardowns[_deferredVoxelPhysicsBakeEmergencyCount] = pending;
        _deferredVoxelPhysicsBakeEmergencyScanCursor = _deferredVoxelPhysicsBakeEmergencyCount;
        _deferredVoxelPhysicsBakeEmergencyCount++;
        return true;
    }

    private static int DrainDeferredVoxelPhysicsBakeEmergencyTeardowns(int drainBudget, int inspectionBudget)
    {
        int pendingCount = _deferredVoxelPhysicsBakeEmergencyCount;
        if (pendingCount <= 0 || drainBudget <= 0 || inspectionBudget <= 0)
            return 0;

        if (inspectionBudget > pendingCount)
            inspectionBudget = pendingCount;

        if (_deferredVoxelPhysicsBakeEmergencyScanCursor < 0 ||
            _deferredVoxelPhysicsBakeEmergencyScanCursor >= pendingCount)
        {
            _deferredVoxelPhysicsBakeEmergencyScanCursor = pendingCount - 1;
        }

        int drained = 0;
        int inspected = 0;
        int index = _deferredVoxelPhysicsBakeEmergencyScanCursor;
        while (pendingCount > 0 && inspected < inspectionBudget && drained < drainBudget)
        {
            if (index < 0)
                index = pendingCount - 1;
            else if (index >= pendingCount)
                index = pendingCount - 1;

            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[index];
            inspected++;
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
            {
                _deferredVoxelPhysicsBakeEmergencyTeardowns[index] = pending;
                index--;
                continue;
            }

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(index);
            drained++;
            pendingCount = _deferredVoxelPhysicsBakeEmergencyCount;
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

        _deferredVoxelPhysicsBakeEmergencyScanCursor = pendingCount > 0 ? index : 0;
        return drained;
    }

    private static void DrainCompletedDeferredVoxelPhysicsBakeEmergencyTeardownsForCapacity()
    {
        int drained = 0;
        for (int i = _deferredVoxelPhysicsBakeEmergencyCount - 1;
             i >= 0 && drained < DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget;
             i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[i];
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref pending.Handle))
            {
                _deferredVoxelPhysicsBakeEmergencyTeardowns[i] = pending;
                continue;
            }

            FinalizeDeferredVoxelPhysicsBakeTeardown(ref pending);
            RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(i);
            drained++;
        }
    }

    private static void RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(int index)
    {
        if ((uint)index >= (uint)_deferredVoxelPhysicsBakeEmergencyCount)
            return;

        int lastIndex = _deferredVoxelPhysicsBakeEmergencyCount - 1;
        if (index != lastIndex)
            _deferredVoxelPhysicsBakeEmergencyTeardowns[index] = _deferredVoxelPhysicsBakeEmergencyTeardowns[lastIndex];

        _deferredVoxelPhysicsBakeEmergencyTeardowns[lastIndex] = default;
        _deferredVoxelPhysicsBakeEmergencyCount = lastIndex;
    }

    private static void ClearDeferredVoxelPhysicsBakeEmergencyTeardowns()
    {
        for (int i = 0; i < _deferredVoxelPhysicsBakeEmergencyCount; i++)
            _deferredVoxelPhysicsBakeEmergencyTeardowns[i] = default;

        _deferredVoxelPhysicsBakeEmergencyCount = 0;
        _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
    }

    private static void UnregisterDeferredVoxelPhysicsBakeTeardownDriver()
    {
        if (!_deferredVoxelPhysicsBakeTeardownRegistered)
            return;

        GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelPhysicsBakeTeardownDriver, PriorityLayer.Environment);
        _deferredVoxelPhysicsBakeTeardownRegistered = false;
        TryUnregisterDeferredVoxelHotSwapBridgeIfIdle();
    }

    internal static bool EnqueueDeferredVoxelColliderUpload(Hecton8.Caves.HectonVoxelVolume volume, int chunkIndex)
    {
        if (volume == null || chunkIndex < 0)
            return false;

        for (int i = 0; i < _deferredVoxelColliderUploads.Count; i++)
        {
            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[i];
            if (pending.Volume == volume && pending.ChunkIndex == chunkIndex)
            {
                RefreshDeferredVoxelUploadProxy(ref pending, volume.GetColliderChunkBakeProxy(chunkIndex));
                pending.RetryCount = 0;
                _deferredVoxelColliderUploads[i] = pending;
                _deferredVoxelColliderUploadScanCursor = i;
                return true;
            }
        }

        BoxCollider proxyCollider = volume.GetColliderChunkBakeProxy(chunkIndex);
        if (!TryReserveDeferredVoxelColliderUploadSlot(proxyCollider))
            return false;

        uint proxyShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
        bool hasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(proxyCollider, out double3 proxyMinAup, out double3 proxyMaxAup);
        _deferredVoxelColliderUploads.Add(new DeferredVoxelColliderUpload
        {
            Volume = volume,
            ProxyCollider = proxyCollider,
            ProxyMinAup = proxyMinAup,
            ProxyMaxAup = proxyMaxAup,
            ProxyShiftSequence = proxyShiftSequence,
            ChunkIndex = chunkIndex,
            Flags = DeferredVoxelColliderUploadVolumeFlag,
            HasProxyBounds = hasProxyBounds ? (byte)1 : (byte)0,
            RetryCount = 0
        });
        _deferredVoxelColliderUploadScanCursor = _deferredVoxelColliderUploads.Count - 1;

        if (!EnsureDeferredVoxelColliderUploadRegistered())
        {
            RemoveDeferredVoxelColliderUploadAt(_deferredVoxelColliderUploads.Count - 1);
            volume.DisableColliderChunkBakeProxy(chunkIndex);
            return false;
        }

        PublishVoxelMeshPipelineTelemetry();
        return true;
    }

    internal static bool EnqueueDeferredVoxelColliderUpload(MeshCollider collider, Mesh mesh)
    {
        return EnqueueDeferredVoxelColliderUpload(collider, mesh, null);
    }

    internal static bool EnqueueDeferredVoxelColliderUpload(MeshCollider collider, Mesh mesh, BoxCollider proxyCollider)
    {
        if (collider == null || mesh == null)
            return false;

        for (int i = 0; i < _deferredVoxelColliderUploads.Count; i++)
        {
            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[i];
            if (pending.Collider == collider)
            {
                pending.Mesh = mesh;
                pending.RetryCount = 0;
                RefreshDeferredVoxelUploadProxy(ref pending, proxyCollider);
                _deferredVoxelColliderUploads[i] = pending;
                _deferredVoxelColliderUploadScanCursor = i;
                return true;
            }
        }

        if (!TryReserveDeferredVoxelColliderUploadSlot(proxyCollider))
            return false;

        uint proxyShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
        bool hasProxyBounds = TryCacheDeferredVoxelProxyAupBounds(proxyCollider, out double3 proxyMinAup, out double3 proxyMaxAup);
        _deferredVoxelColliderUploads.Add(new DeferredVoxelColliderUpload
        {
            Collider = collider,
            ProxyCollider = proxyCollider,
            Mesh = mesh,
            ProxyMinAup = proxyMinAup,
            ProxyMaxAup = proxyMaxAup,
            ProxyShiftSequence = proxyShiftSequence,
            Flags = 0,
            HasProxyBounds = hasProxyBounds ? (byte)1 : (byte)0,
            RetryCount = 0
        });
        _deferredVoxelColliderUploadScanCursor = _deferredVoxelColliderUploads.Count - 1;

        if (!EnsureDeferredVoxelColliderUploadRegistered())
        {
            RemoveDeferredVoxelColliderUploadAt(_deferredVoxelColliderUploads.Count - 1);
            collider.enabled = false;
            if (proxyCollider != null)
                proxyCollider.enabled = false;
            return false;
        }

        PublishVoxelMeshPipelineTelemetry();
        return true;
    }

    private static void RefreshDeferredVoxelUploadProxy(ref DeferredVoxelColliderUpload pending, BoxCollider proxyCollider)
    {
        BoxCollider previousProxy = pending.ProxyCollider;
        if (previousProxy != null && !ReferenceEquals(previousProxy, proxyCollider))
            previousProxy.enabled = false;

        pending.ProxyCollider = proxyCollider;
        pending.RetryCount = 0;
        if (proxyCollider != null)
        {
            proxyCollider.gameObject.layer = HectonLayerMasks.VoxelProxy;
            proxyCollider.enabled = true;
        }

        RefreshDeferredVoxelUploadProxyBounds(ref pending, HectonFloatingOrigin.CurrentShiftSequence);
    }

    private static void CancelDeferredVoxelColliderUpload(ref DeferredVoxelColliderUpload pending, bool publishRetryDropWarning)
    {
        if ((pending.Flags & DeferredVoxelColliderUploadVolumeFlag) != 0 && pending.Volume != null)
            pending.Volume.DisableColliderChunkBakeProxy(pending.ChunkIndex);

        if (pending.ProxyCollider != null)
            pending.ProxyCollider.enabled = false;

        if (!publishRetryDropWarning || _voxelColliderUploadRetryDropWarningArmed)
            return;

        _voxelColliderUploadRetryDropWarningArmed = true;
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelColliderUploadRetryDropWarningHash,
            _VoxelPhysicsBakeContextHash,
            pending.RetryCount);
    }

    private static bool TryReserveDeferredVoxelColliderUploadSlot(BoxCollider proxyCollider)
    {
        if (_deferredVoxelColliderUploads.Count < DeferredVoxelColliderUploadCapacity)
        {
            if (_deferredVoxelColliderUploads.Count <= DeferredVoxelColliderUploadDropWarningReleaseThreshold)
                _voxelColliderUploadDropWarningArmed = false;
            return true;
        }

        DrainDeferredVoxelColliderUploads(DeferredVoxelColliderUploadBackpressureBudget);
        if (_deferredVoxelColliderUploads.Count < DeferredVoxelColliderUploadCapacity)
        {
            if (_deferredVoxelColliderUploads.Count <= DeferredVoxelColliderUploadDropWarningReleaseThreshold)
                _voxelColliderUploadDropWarningArmed = false;
            return true;
        }

        if (!_voxelColliderUploadDropWarningArmed)
        {
            _voxelColliderUploadDropWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelColliderUploadDropWarningHash,
                _VoxelPhysicsBakeContextHash,
                _deferredVoxelColliderUploads.Count);
        }

        if (proxyCollider != null)
            proxyCollider.enabled = false;
        return false;
    }

    private static bool EnsureDeferredVoxelColliderUploadRegistered()
    {
        if (_deferredVoxelColliderUploadRegistered)
            return true;

        if (!CanRegisterDeferredVoxelLateFrameWork())
            return false;

        _deferredVoxelColliderUploadRegistered = GlobalRegistry.TryRegisterLateFrameTickable(
            _deferredVoxelColliderUploadDriver,
            PriorityLayer.Environment);
        if (_deferredVoxelColliderUploadRegistered)
            TryRegisterDeferredVoxelHotSwapBridge();
        return _deferredVoxelColliderUploadRegistered;
    }

    private static bool CanRegisterDeferredVoxelLateFrameWork()
    {
        return Application.isPlaying && GlobalRegistry.Dispatcher != null;
    }

    private static void RebindDeferredVoxelLateFrameDrivers()
    {
        bool needsTeardownDriver = _deferredVoxelPhysicsBakeTeardownRegistered ||
                                  DeferredVoxelPhysicsBakePendingCount > 0;
        bool needsUploadDriver = _deferredVoxelColliderUploadRegistered ||
                                _deferredVoxelColliderUploads.Count > 0;

        if (_deferredVoxelPhysicsBakeTeardownRegistered)
        {
            GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelPhysicsBakeTeardownDriver, PriorityLayer.Environment);
            _deferredVoxelPhysicsBakeTeardownRegistered = false;
        }

        if (_deferredVoxelColliderUploadRegistered)
        {
            GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelColliderUploadDriver, PriorityLayer.Environment);
            _deferredVoxelColliderUploadRegistered = false;
        }

        if (needsTeardownDriver)
            EnsureDeferredVoxelPhysicsBakeTeardownRegistered();
        if (needsUploadDriver)
            EnsureDeferredVoxelColliderUploadRegistered();
        TryUnregisterDeferredVoxelHotSwapBridgeIfIdle();
    }

    private static void TryRegisterDeferredVoxelHotSwapBridge()
    {
        if (_deferredVoxelHotSwapRegistered || !Application.isPlaying)
            return;

        _deferredVoxelHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(_deferredVoxelDispatcherHotSwapBridge);
    }

    private static void TryUnregisterDeferredVoxelHotSwapBridgeIfIdle()
    {
        if (!_deferredVoxelHotSwapRegistered ||
            _deferredVoxelPhysicsBakeTeardownRegistered ||
            _deferredVoxelColliderUploadRegistered ||
            HasPendingVoxelDeferredWork())
        {
            return;
        }

        GlobalRegistry.TryUnregisterHotSwapListener(_deferredVoxelDispatcherHotSwapBridge);
        _deferredVoxelHotSwapRegistered = false;
    }

    private static void DrainDeferredVoxelColliderUploads()
    {
        DrainDeferredVoxelColliderUploads(ResolveDeferredVoxelColliderUploadBudgetThisFrame());
    }

    private static int ResolveDeferredVoxelColliderUploadBudgetThisFrame()
    {
        int frame = Time.frameCount;
        if (_deferredVoxelColliderUploadFrame != frame)
        {
            _deferredVoxelColliderUploadFrame = frame;
            float frameBudget = ResolveDeferredVoxelColliderUploadBudgetPerFrame();
            float frameCap = Mathf.Clamp(
                Mathf.Ceil(frameBudget - DeferredVoxelColliderUploadBurstCapBias),
                DeferredVoxelColliderUploadBudgetPerFrame,
                DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame);
            _deferredVoxelColliderUploadBudgetTokens = Mathf.Min(frameCap, _deferredVoxelColliderUploadBudgetTokens + frameBudget);
        }

        int budget = math.min(
            (int)DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame,
            (int)math.floor(_deferredVoxelColliderUploadBudgetTokens));
        if (budget > 0)
            _deferredVoxelColliderUploadBudgetTokens -= budget;

        return budget;
    }

    private static float ResolveDeferredVoxelColliderUploadBudgetPerFrame()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        return Mathf.Lerp(DeferredVoxelColliderUploadBudgetPerFrame, DeferredVoxelColliderUploadBudgetVisualOverkillPerFrame, smooth);
    }

    private static void DrainDeferredVoxelColliderUploads(int uploadBudget)
    {
        int pendingCount = _deferredVoxelColliderUploads.Count;
        if (pendingCount <= 0)
        {
            _deferredVoxelColliderUploadScanCursor = -1;
            _voxelColliderUploadDropWarningArmed = false;
            _voxelColliderUploadRetryDropWarningArmed = false;
            UnregisterDeferredVoxelColliderUploadDriver();
            TryShutdownSharedTables();
            return;
        }

        int uploads = 0;
        int maxUploads = math.max(0, uploadBudget);
        int inspected = 0;
        int maxInspections = maxUploads > 0 ? math.max(maxUploads, maxUploads * 4) : 0;
        int index = _deferredVoxelColliderUploadScanCursor;
        if (index < 0 || index >= pendingCount)
            index = pendingCount - 1;

        while (index >= 0 && pendingCount > 0 && uploads < maxUploads && inspected < maxInspections)
        {
            if (index >= pendingCount)
                index = pendingCount - 1;

            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[index];
            inspected++;
            bool appliedUpload = false;
            bool keepPending = false;
            bool retryDrop = false;
            if ((pending.Flags & DeferredVoxelColliderUploadVolumeFlag) != 0)
            {
                if (pending.Volume != null)
                {
                    if (pending.Volume.IsDeferredColliderChunkUploadReady(pending.ChunkIndex))
                    {
                        appliedUpload = pending.Volume.CommitDeferredColliderChunkUpload(pending.ChunkIndex);
                    }
                    else if (pending.RetryCount < DeferredVoxelColliderUploadRetryLimit)
                    {
                        pending.RetryCount++;
                        RefreshDeferredVoxelUploadProxyBounds(ref pending, HectonFloatingOrigin.CurrentShiftSequence);
                        _deferredVoxelColliderUploads[index] = pending;
                        keepPending = true;
                    }
                    else
                    {
                        retryDrop = true;
                    }
                }
            }
            else if (pending.Collider != null && pending.Mesh != null)
            {
                pending.Collider.enabled = false;
                if (pending.ProxyCollider != null)
                    pending.ProxyCollider.enabled = false;
                appliedUpload = true;
            }

            if (keepPending)
            {
                index--;
                continue;
            }

            if (!appliedUpload)
                CancelDeferredVoxelColliderUpload(ref pending, retryDrop);

            RemoveDeferredVoxelColliderUploadAt(index);
            if (appliedUpload)
                uploads++;
            pendingCount = _deferredVoxelColliderUploads.Count;
            if (pendingCount == 0)
                break;
            index--;
        }

        if (_deferredVoxelColliderUploads.Count == 0)
        {
            _deferredVoxelColliderUploadScanCursor = -1;
            _voxelColliderUploadDropWarningArmed = false;
            _voxelColliderUploadRetryDropWarningArmed = false;
            UnregisterDeferredVoxelColliderUploadDriver();
            TryShutdownSharedTables();
            return;
        }

        if (index < 0)
            index = _deferredVoxelColliderUploads.Count - 1;
        else if (index >= _deferredVoxelColliderUploads.Count)
            index = _deferredVoxelColliderUploads.Count - 1;

        _deferredVoxelColliderUploadScanCursor = index;
        if (_deferredVoxelColliderUploads.Count <= DeferredVoxelColliderUploadDropWarningReleaseThreshold)
            _voxelColliderUploadDropWarningArmed = false;
    }

    private static void RecordVoxelChunkMeshed()
    {
        int frame = Time.frameCount;
        if (_voxelMeshTelemetryFrame != frame)
        {
            _voxelMeshTelemetryFrame = frame;
            _voxelChunksMeshedThisFrame = 0;
        }

        if (_voxelChunksMeshedThisFrame < ushort.MaxValue)
            _voxelChunksMeshedThisFrame++;

        PublishVoxelMeshPipelineTelemetry();
    }

    private static void PublishVoxelMeshPipelineTelemetry()
    {
        int frame = Time.frameCount;
        if (_voxelMeshTelemetryFrame != frame)
        {
            _voxelMeshTelemetryFrame = frame;
            _voxelChunksMeshedThisFrame = 0;
        }

        int bakeQueueLength = DeferredVoxelPhysicsBakePendingCount;
        int uploadQueueLength = _deferredVoxelColliderUploads.Count;
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelChunksMeshedPerFrameHash,
            _VoxelMeshPipelineContextHash,
            _voxelChunksMeshedThisFrame);
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelBakeQueueLengthHash,
            _VoxelMeshPipelineContextHash,
            bakeQueueLength);

        WriteVoxelMeshPipelineBlackBoxSample(
            unchecked((uint)frame),
            0u,
            _voxelChunksMeshedThisFrame,
            bakeQueueLength,
            uploadQueueLength);
    }

    private static void ReportVoxelMeshScratchCapacityOverflow()
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            unchecked((uint)Time.frameCount),
            VoxelMeshPipelineScratchCapacityOverflowFlag,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static void ReportVoxelVolumeSpawnPoolMiss()
    {
        WriteVoxelMeshPipelineBlackBoxSample(
            unchecked((uint)Time.frameCount),
            VoxelMeshPipelineVolumeSpawnPoolMissFlag,
            _voxelChunksMeshedThisFrame,
            DeferredVoxelPhysicsBakePendingCount,
            _deferredVoxelColliderUploads.Count);
    }

    private static bool EnsureVoxelMeshPipelineBlackBox()
    {
        IDataVault vault = CacheVoxelMeshPipelineBlackBoxVaultCold();
        if (vault == null)
            return false;

        if (IsVoxelMeshPipelineBlackBoxHandleCreated() &&
            vault.TryReadOnlyHandle(in _voxelMeshPipelineBlackBoxHandle, out NativeArray<VoxelMeshPipelineTelemetryEntry>.ReadOnly blackBox) &&
            blackBox.Length >= VoxelMeshPipelineBlackBoxCapacity)
        {
            return true;
        }

        _voxelMeshPipelineBlackBoxHandle = vault.EnsureGenerationHandle<VoxelMeshPipelineTelemetryEntry>(
            VoxelMeshPipelineBlackBoxBufferId,
            VoxelMeshPipelineBlackBoxCapacity,
            VoxelMeshPipelineBlackBoxOwnerSystemId,
            NativeArrayOptions.ClearMemory);
        _voxelMeshPipelineBlackBoxCursor = 0;
        return IsVoxelMeshPipelineBlackBoxHandleCreated();
    }

    private static void DisposeVoxelMeshPipelineBlackBox()
    {
        IDataVault vault = _voxelMeshPipelineBlackBoxVault;
        if (vault != null && IsVoxelMeshPipelineBlackBoxHandleCreated())
            vault.ReleaseBuffer(in _voxelMeshPipelineBlackBoxHandle);

        _voxelMeshPipelineBlackBoxHandle = default;
        _voxelMeshPipelineBlackBoxVault = null;
        _voxelMeshPipelineBlackBoxCursor = 0;
    }

    private static IDataVault CacheVoxelMeshPipelineBlackBoxVaultCold()
    {
        IDataVault vault = GlobalRegistry.DataVault;
        if (ReferenceEquals(_voxelMeshPipelineBlackBoxVault, vault))
            return vault;

        if (_voxelMeshPipelineBlackBoxVault != null && IsVoxelMeshPipelineBlackBoxHandleCreated())
            _voxelMeshPipelineBlackBoxVault.ReleaseBuffer(in _voxelMeshPipelineBlackBoxHandle);

        _voxelMeshPipelineBlackBoxVault = vault;
        _voxelMeshPipelineBlackBoxHandle = default;
        _voxelMeshPipelineBlackBoxCursor = 0;
        return vault;
    }

    private static bool IsVoxelMeshPipelineBlackBoxHandleCreated()
    {
        return _voxelMeshPipelineBlackBoxHandle.BufferID == (uint)VoxelMeshPipelineBlackBoxBufferId &&
               _voxelMeshPipelineBlackBoxHandle.SystemID == (uint)VoxelMeshPipelineBlackBoxOwnerSystemId &&
               _voxelMeshPipelineBlackBoxHandle.Generation != 0u;
    }

    private static void WriteVoxelMeshPipelineBlackBoxSample(
        uint frame,
        uint flags,
        int chunksMeshedThisFrame,
        int bakeQueueLength,
        int colliderUploadQueueLength)
    {
        if (!EnsureVoxelMeshPipelineBlackBox())
            return;

        IDataVault vault = _voxelMeshPipelineBlackBoxVault;
        if (vault == null ||
            !vault.TryAcquireWriteLock(in _voxelMeshPipelineBlackBoxHandle, VoxelMeshPipelineBlackBoxOwnerSystemId, out NativeArray<VoxelMeshPipelineTelemetryEntry> blackBox))
        {
            return;
        }

        if (blackBox.Length < VoxelMeshPipelineBlackBoxCapacity)
        {
            vault.ReleaseWriteLock(in _voxelMeshPipelineBlackBoxHandle, VoxelMeshPipelineBlackBoxOwnerSystemId);
            return;
        }

        int surfacePoolInUse = _voxelSurfaceMeshPoolInUseCount;
        int physicsPoolInUse = _voxelPhysicsBakeMeshPoolInUseCount;
        uint stateHash = 2166136261u;
        stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, chunksMeshedThisFrame));
        stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, bakeQueueLength));
        stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, colliderUploadQueueLength));
        stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, Volatile.Read(ref _activeGenerationOperations)));
        stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, surfacePoolInUse));
        stateHash = MixVoxelMeshTelemetryHash(stateHash, (uint)math.max(0, physicsPoolInUse));

        bool invalidState =
            chunksMeshedThisFrame < 0 ||
            bakeQueueLength < 0 ||
            colliderUploadQueueLength < 0 ||
            _voxelMeshPipelineBlackBoxCursor < 0 ||
            _voxelMeshPipelineBlackBoxCursor >= VoxelMeshPipelineBlackBoxCapacity;
        uint resolvedFlags = invalidState ? flags | VoxelMeshPipelineInvalidStateFlag : flags;
        int cursor = math.clamp(_voxelMeshPipelineBlackBoxCursor, 0, VoxelMeshPipelineBlackBoxCapacity - 1);
        try
        {
            blackBox[cursor] = new VoxelMeshPipelineTelemetryEntry
            {
                Frame = frame,
                Flags = resolvedFlags,
                ChunksMeshedThisFrame = (ushort)math.min(ushort.MaxValue, math.max(0, chunksMeshedThisFrame)),
                BakeQueueLength = (ushort)math.min(ushort.MaxValue, math.max(0, bakeQueueLength)),
                ColliderUploadQueueLength = (ushort)math.min(ushort.MaxValue, math.max(0, colliderUploadQueueLength)),
                ActiveGenerationOperations = (ushort)math.min(ushort.MaxValue, math.max(0, Volatile.Read(ref _activeGenerationOperations))),
                SurfacePoolInUse = (ushort)math.min(ushort.MaxValue, math.max(0, surfacePoolInUse)),
                PhysicsPoolInUse = (ushort)math.min(ushort.MaxValue, math.max(0, physicsPoolInUse)),
                StateHash = stateHash,
                Padding0 = 0u,
                Padding1 = 0u
            };
        }
        finally
        {
            vault.ReleaseWriteLock(in _voxelMeshPipelineBlackBoxHandle, VoxelMeshPipelineBlackBoxOwnerSystemId);
        }

        cursor++;
        _voxelMeshPipelineBlackBoxCursor = cursor >= VoxelMeshPipelineBlackBoxCapacity ? 0 : cursor;

#if UNITY_EDITOR
        if (resolvedFlags != 0u)
            DumpVoxelMeshPipelineBlackBoxOnce(resolvedFlags);
#endif
    }

    private static uint MixVoxelMeshTelemetryHash(uint hash, uint value)
    {
        unchecked
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

#if UNITY_EDITOR
    private static void DumpVoxelMeshPipelineBlackBoxOnce(uint reasonFlags)
    {
        if (_voxelMeshPipelineBlackBoxDumped)
            return;

        _voxelMeshPipelineBlackBoxDumped = true;
        DumpVoxelMeshPipelineBlackBox(reasonFlags);
    }

    private static void DumpVoxelMeshPipelineBlackBox(uint reasonFlags)
    {
        IDataVault vault = _voxelMeshPipelineBlackBoxVault;
        if (vault == null ||
            !IsVoxelMeshPipelineBlackBoxHandleCreated() ||
            !vault.TryReadOnlyHandle(in _voxelMeshPipelineBlackBoxHandle, out NativeArray<VoxelMeshPipelineTelemetryEntry>.ReadOnly blackBox) ||
            blackBox.Length < VoxelMeshPipelineBlackBoxCapacity)
        {
            return;
        }

        try
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VoxelMeshPipelineBlackBoxDumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(VoxelMeshPipelineBlackBoxDumpMagic);
                writer.Write((uint)VoxelMeshPipelineBlackBoxCapacity);
                writer.Write((uint)UnsafeUtility.SizeOf<VoxelMeshPipelineTelemetryEntry>());
                writer.Write((uint)_voxelMeshPipelineBlackBoxCursor);
                writer.Write(reasonFlags);

                for (int i = 0; i < VoxelMeshPipelineBlackBoxCapacity; i++)
                {
                    int index = (_voxelMeshPipelineBlackBoxCursor + i) % VoxelMeshPipelineBlackBoxCapacity;
                    VoxelMeshPipelineTelemetryEntry entry = blackBox[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.ChunksMeshedThisFrame);
                    writer.Write(entry.BakeQueueLength);
                    writer.Write(entry.ColliderUploadQueueLength);
                    writer.Write(entry.ActiveGenerationOperations);
                    writer.Write(entry.SurfacePoolInUse);
                    writer.Write(entry.PhysicsPoolInUse);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Padding0);
                    writer.Write(entry.Padding1);
                }
            }
        }
        catch
        {
            // Fault-path export must not add a second runtime failure.
        }
    }
#endif

    private static void RemoveDeferredVoxelColliderUploadAt(int index)
    {
        int lastIndex = _deferredVoxelColliderUploads.Count - 1;
        if (index != lastIndex)
            _deferredVoxelColliderUploads[index] = _deferredVoxelColliderUploads[lastIndex];

        _deferredVoxelColliderUploads.RemoveAt(lastIndex);
    }

    private static void UnregisterDeferredVoxelColliderUploadDriver()
    {
        if (!_deferredVoxelColliderUploadRegistered)
            return;

        GlobalRegistry.UnregisterLateFrameTickable(_deferredVoxelColliderUploadDriver, PriorityLayer.Environment);
        _deferredVoxelColliderUploadRegistered = false;
        TryUnregisterDeferredVoxelHotSwapBridgeIfIdle();
    }

    private static void UpdateDeferredVoxelPhysicsBakeBackpressure()
    {
        int pendingCount = DeferredVoxelPhysicsBakePendingCount;
        bool nextActive = ResolveDeferredVoxelPhysicsBakeBackpressureState(
            pendingCount,
            _deferredVoxelPhysicsBakeBackpressureActive);

        if (GlobalRegistry.Dispatcher == null)
        {
            _deferredVoxelPhysicsBakeBackpressureActive = nextActive;
            return;
        }

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
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup) ||
            !TryResolveRuntimeAup(worldCenter, in originAup, out AbsoluteUniversePosition worldCenterAup) ||
            !TryResolveRuntimeAup(observerPosition, in originAup, out AbsoluteUniversePosition observerAup))
        {
            return 0;
        }

        return ResolveDistanceBasedVoxelLodLevel(in worldCenterAup, in observerAup, VoxelLodColliderDisableDistanceMeters);
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
        if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            return 0;

        if (!TryResolveRuntimeAup(worldCenter, out AbsoluteUniversePosition worldCenterAup))
            return 0;

        return ResolveDistanceBasedVoxelLodLevel(in worldCenterAup, in playerAup, VoxelLodColliderDisableDistanceMeters);
    }

    private static int ResolveDistanceBasedVoxelLodLevel(
        in AbsoluteUniversePosition worldCenterAup,
        in AbsoluteUniversePosition observerAup,
        float lodDistanceMeters)
    {
        double distanceSq = AbsoluteUniversePosition.DistanceSq(in worldCenterAup, in observerAup);
        double thresholdSq = (double)lodDistanceMeters * lodDistanceMeters;
        return distanceSq > thresholdSq ? 1 : 0;
    }

    private static bool ShouldUseCinematicColliderFake(in VoxelPipelineData data)
    {
        if (!data.BuildCollider || data.LODLevel > 0)
            return false;

        if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            return false;

        AbsoluteUniversePosition volumeAup = BuildCapturedAup(data.WorldCenter, data.AbsoluteUniverseOffsetAtStartDouble);
        double distanceSq = AbsoluteUniversePosition.DistanceSq(in volumeAup, in playerAup);
        float colliderDisableDistance = VoxelLodColliderDisableDistanceMeters;
        IVramPressureReadModel pressureMonitor = GlobalRegistry.VRAMPressureReadModel;
        if (pressureMonitor != null &&
            pressureMonitor.HasSample &&
            pressureMonitor.PressureFactor >= VoxelColliderFakePressureFactor)
        {
            colliderDisableDistance = VoxelPressureColliderDisableDistanceMeters;
        }

        double thresholdSq = (double)colliderDisableDistance * colliderDisableDistance;
        return distanceSq > thresholdSq;
    }

    private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
    {
        HectonPlayerMovement playerMovement = PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
            ? runtimeContext.PlayerMovement
            : null;
        if (playerMovement != null)
        {
            playerAup = playerMovement.CurrentAup;
            return true;
        }

        if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
            playerTransform != null &&
            playerTransform.TryGetComponent(out HectonPlayerMovement scenePlayerMovement))
        {
            playerAup = scenePlayerMovement.CurrentAup;
            return true;
        }

        Transform bootstrapPlayer = BootstrapState.CurrentPlayerTransform;
        if (bootstrapPlayer != null &&
            bootstrapPlayer.TryGetComponent(out HectonPlayerMovement bootstrapPlayerMovement))
        {
            playerAup = bootstrapPlayerMovement.CurrentAup;
            return true;
        }

        playerAup = default;
        return false;
    }

    private static bool IsFiniteAup(in AbsoluteUniversePosition position)
    {
        return math.isfinite(position.LocalX) &&
               math.isfinite(position.LocalY) &&
               math.isfinite(position.LocalZ);
    }

    private static bool TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute)
    {
        originAbsolute = default;
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            return false;

        originAbsolute = originAup.ToAbsoluteDouble3();
        return math.all(math.isfinite(originAbsolute));
    }

    private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
    {
        originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
        return IsFiniteAup(in originAup);
    }

    private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
    {
        positionAup = default;
        if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            return false;

        return TryResolveRuntimeAup(runtimePosition, in originAup, out positionAup);
    }

    private static bool TryResolveRuntimeAup(
        Vector3 runtimePosition,
        in AbsoluteUniversePosition originAup,
        out AbsoluteUniversePosition positionAup)
    {
        positionAup = default;
        if (!IsFiniteVector(runtimePosition) || !IsFiniteAup(in originAup))
            return false;

        positionAup = AbsoluteUniversePosition.OffsetMeters(
            in originAup,
            new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        return IsFiniteAup(in positionAup);
    }

    private static bool TryResolveRuntimeAbsoluteDouble(Vector3 runtimePosition, out double3 absolutePosition)
    {
        absolutePosition = default;
        if (!TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition positionAup))
            return false;

        absolutePosition = positionAup.ToAbsoluteDouble3();
        return math.all(math.isfinite(absolutePosition));
    }

    private static bool TryResolveRuntimeAbsoluteDouble(
        Vector3 runtimePosition,
        in AbsoluteUniversePosition originAup,
        out double3 absolutePosition)
    {
        absolutePosition = default;
        if (!TryResolveRuntimeAup(runtimePosition, in originAup, out AbsoluteUniversePosition positionAup))
            return false;

        absolutePosition = positionAup.ToAbsoluteDouble3();
        return math.all(math.isfinite(absolutePosition));
    }

    private static AbsoluteUniversePosition BuildCapturedAup(Vector3 runtimePosition, Vector3 capturedOffset)
    {
        return BuildCapturedAup(runtimePosition, ToDouble3(capturedOffset));
    }

    private static AbsoluteUniversePosition BuildCapturedAup(Vector3 runtimePosition, double3 capturedOffset)
    {
        return AbsoluteUniversePosition.FromAbsolutePosition(ToDouble3(runtimePosition) + capturedOffset);
    }

    private static double3 ToDouble3(Vector3 value)
    {
        return new double3(value.x, value.y, value.z);
    }

    private static double3 ToDouble3(float3 value)
    {
        return new double3(value.x, value.y, value.z);
    }

    private static float3 ToFloat3(double3 value)
    {
        return new float3((float)value.x, (float)value.y, (float)value.z);
    }

    private static Vector3 ToVector3(double3 value)
    {
        return new Vector3((float)value.x, (float)value.y, (float)value.z);
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

    private static void ResetVoxelMeshPoolState()
    {
        for (int i = 0; i < _voxelSurfaceMeshPoolInUse.Length; i++)
            _voxelSurfaceMeshPoolInUse[i] = false;

        for (int i = 0; i < _voxelPhysicsBakeMeshPoolInUse.Length; i++)
            _voxelPhysicsBakeMeshPoolInUse[i] = false;

        _voxelSurfaceMeshPoolInUseCount = 0;
        _voxelPhysicsBakeMeshPoolInUseCount = 0;
    }

    private static async Awaitable WarmVoxelMeshPoolsAsync(CancellationToken ct)
    {
        if (_voxelMeshPoolWarmupRunning)
            return;

        _voxelMeshPoolWarmupRunning = true;
        try
        {
            await WarmVoxelSurfaceMeshPoolAsync(ct);
            await WarmVoxelPhysicsBakeMeshPoolAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Owner teardown cancels cold prewarm; runtime acquire fails closed instead of allocating a mesh.
        }
        catch (Exception exception)
        {
#if UNITY_EDITOR
            Debug.LogException(exception);
#endif
        }
        finally
        {
            _voxelMeshPoolWarmupRunning = false;
            if (Volatile.Read(ref _shutdownRequested) == 1)
                TryShutdownSharedTables();
        }
    }

    private static async Awaitable WarmVoxelSurfaceMeshPoolAsync(CancellationToken ct)
    {
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            if (_voxelSurfaceMeshPool[i] != null)
                continue;

            ct.ThrowIfCancellationRequested();
            if (ShouldAbortVoxelMeshPoolWarmup())
                return;

            _voxelSurfaceMeshPool[i] = CreateVoxelPoolMesh(VoxelSurfacePoolMeshName);
            await AwaitableDebtMonitor.NextFrameAsync(ct);
        }
    }

    private static async Awaitable WarmVoxelPhysicsBakeMeshPoolAsync(CancellationToken ct)
    {
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (_voxelPhysicsBakeMeshPool[i] != null)
                continue;

            ct.ThrowIfCancellationRequested();
            if (ShouldAbortVoxelMeshPoolWarmup())
                return;

            _voxelPhysicsBakeMeshPool[i] = CreateVoxelPoolMesh(VoxelPhysicsBakePoolMeshName);
            await AwaitableDebtMonitor.NextFrameAsync(ct);
        }
    }

    private static bool ShouldAbortVoxelMeshPoolWarmup()
    {
        return Volatile.Read(ref _shutdownRequested) == 1 &&
               Volatile.Read(ref _liveEngineCount) <= 0;
    }

    private static Mesh CreateVoxelPoolMesh(string meshName)
    {
        Mesh mesh = new Mesh // COLD ALLOC: Mesh[1] - staggered pooled voxel mesh slot creation outside the hot path.
        {
            name = meshName
        };
        mesh.MarkDynamic();
        return mesh;
    }

    private static bool NeedsVoxelSurfaceMeshAcquire(GameObject go, HectonVoxelVolume volume = null)
    {
        if (go == null)
            return true;

        MeshFilter meshFilter = volume != null ? volume.CachedMeshFilter : null;
        if (meshFilter == null)
            go.TryGetComponent(out meshFilter);
        return meshFilter == null || meshFilter.sharedMesh == null;
    }

    private static async Awaitable<Mesh> AcquireVoxelSurfaceMeshAsync(CancellationToken ct)
    {
        Mesh mesh = AcquireVoxelSurfaceMesh();
        if (mesh != null)
            return mesh;

        for (int retry = 0; retry < VoxelMeshPoolAcquireWarmupRetryFrames && _voxelMeshPoolWarmupRunning; retry++)
        {
            ct.ThrowIfCancellationRequested();
            await AwaitableDebtMonitor.NextFrameAsync(ct);
            mesh = AcquireVoxelSurfaceMesh();
            if (mesh != null)
                return mesh;
        }

        return null;
    }

    private static async Awaitable<Mesh> AcquireVoxelPhysicsBakeMeshAsync(CancellationToken ct)
    {
        Mesh mesh = AcquireVoxelPhysicsBakeMesh();
        if (mesh != null)
            return mesh;

        for (int retry = 0; retry < VoxelMeshPoolAcquireWarmupRetryFrames && _voxelMeshPoolWarmupRunning; retry++)
        {
            ct.ThrowIfCancellationRequested();
            await AwaitableDebtMonitor.NextFrameAsync(ct);
            mesh = AcquireVoxelPhysicsBakeMesh();
            if (mesh != null)
                return mesh;
        }

        return null;
    }

    internal static Mesh AcquireVoxelSurfaceMesh()
    {
        bool hasColdFreeSlot = false;
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            if (_voxelSurfaceMeshPoolInUse[i])
                continue;

            Mesh mesh = _voxelSurfaceMeshPool[i];
            if (mesh == null)
            {
                hasColdFreeSlot = true;
                continue;
            }

            _voxelSurfaceMeshPoolInUse[i] = true;
            _voxelSurfaceMeshPoolInUseCount = math.min(VoxelSurfaceMeshPoolSize, _voxelSurfaceMeshPoolInUseCount + 1);
            mesh.Clear(false);
            _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
            return mesh;
        }

        if (!hasColdFreeSlot && !_voxelSurfaceMeshPoolExhaustedWarningArmed)
        {
            _voxelSurfaceMeshPoolExhaustedWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelSurfaceMeshPoolExhaustedWarningHash,
                _VoxelPhysicsBakeContextHash,
                VoxelSurfaceMeshPoolSize);
        }

        return null;
    }

    internal static bool ReleaseVoxelSurfaceMesh(Mesh mesh)
    {
        if (mesh == null)
            return false;

        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            if (!ReferenceEquals(_voxelSurfaceMeshPool[i], mesh))
                continue;

            mesh.Clear(false);
            if (_voxelSurfaceMeshPoolInUse[i])
            {
                _voxelSurfaceMeshPoolInUse[i] = false;
                _voxelSurfaceMeshPoolInUseCount = math.max(0, _voxelSurfaceMeshPoolInUseCount - 1);
            }

            _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
            return true;
        }

        return false;
    }

    internal static Mesh AcquireVoxelPhysicsBakeMesh()
    {
        bool hasColdFreeSlot = false;
        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            if (_voxelPhysicsBakeMeshPoolInUse[i])
                continue;

            Mesh mesh = _voxelPhysicsBakeMeshPool[i];
            if (mesh == null)
            {
                hasColdFreeSlot = true;
                continue;
            }

            _voxelPhysicsBakeMeshPoolInUse[i] = true;
            _voxelPhysicsBakeMeshPoolInUseCount = math.min(VoxelPhysicsBakeMeshPoolSize, _voxelPhysicsBakeMeshPoolInUseCount + 1);
            mesh.Clear(false);
            return mesh;
        }

        if (!hasColdFreeSlot && !_voxelPhysicsBakeMeshPoolExhaustedWarningArmed)
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
            if (_voxelPhysicsBakeMeshPoolInUse[i])
            {
                _voxelPhysicsBakeMeshPoolInUse[i] = false;
                _voxelPhysicsBakeMeshPoolInUseCount = math.max(0, _voxelPhysicsBakeMeshPoolInUseCount - 1);
            }

            _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
            return true;
        }

        return false;
    }

    private static void DestroyVoxelMeshPools()
    {
        for (int i = 0; i < _voxelSurfaceMeshPool.Length; i++)
        {
            Mesh mesh = _voxelSurfaceMeshPool[i];
            if (mesh != null)
                DestroyDeferredVoxelObject(mesh);

            _voxelSurfaceMeshPool[i] = null;
            _voxelSurfaceMeshPoolInUse[i] = false;
        }

        for (int i = 0; i < _voxelPhysicsBakeMeshPool.Length; i++)
        {
            Mesh mesh = _voxelPhysicsBakeMeshPool[i];
            if (mesh != null)
                DestroyDeferredVoxelObject(mesh);

            _voxelPhysicsBakeMeshPool[i] = null;
            _voxelPhysicsBakeMeshPoolInUse[i] = false;
        }

        _voxelSurfaceMeshPoolExhaustedWarningArmed = false;
        _voxelPhysicsBakeMeshPoolExhaustedWarningArmed = false;
        _voxelSurfaceMeshPoolInUseCount = 0;
        _voxelPhysicsBakeMeshPoolInUseCount = 0;
    }

    private static void PublishVoxelAnomalySolveWarningIfNeeded(long startTimestamp)
    {
        float elapsedMs = (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d / Stopwatch.Frequency);
        if (elapsedMs <= VoxelAnomalySolveWarningMs)
        {
            _voxelAnomalySolveWarningArmed = false;
            return;
        }

        if (_voxelAnomalySolveWarningArmed)
            return;

        _voxelAnomalySolveWarningArmed = true;
        GlobalTelemetryBus.PublishPerformanceWarning(
            _VoxelAnomalySolveWarningHash,
            _VoxelAnomalyContextHash,
            elapsedMs);
    }

    static void LogVoxelJobWaitWatchdog(string context, int waitFrames)
    {
#if UNITY_EDITOR
        Debug.LogError("[HectonVoxel] Job wait watchdog tripped. Cleanup barrier required.");
#endif
    }

    static void TryBindSelectedChthonicPillarResources(NativeArray<AnomalyFeatureRecord> selectedPillarFeature)
    {
        if (!selectedPillarFeature.IsCreated || selectedPillarFeature.Length <= 0)
            return;

        AnomalyFeatureRecord record = selectedPillarFeature[0];
        if (record.Valid == 0 || record.Kind != (byte)AnomalyFeatureKind.ChthonicPillar)
            return;

        ResourceDistributionDirector director = GlobalRegistry.ResourceDistribution;
        if (director == null)
            return;

        director.TryBindChthonicPillarResourcesAtAup(
            new double3(record.AupX, record.AupY, record.AupZ),
            ChthonicPillarRadiusMeters,
            ChthonicPillarHeightMeters,
            ComputeChthonicPillarStableId(in record));
    }

    static uint ComputeChthonicPillarStableId(in AnomalyFeatureRecord record)
    {
        uint hash = 2166136261u;
        hash = HashPillarCoordinate(hash, QuantizePillarCoordinate(record.AupX));
        hash = HashPillarCoordinate(hash, QuantizePillarCoordinate(record.AupY));
        hash = HashPillarCoordinate(hash, QuantizePillarCoordinate(record.AupZ));
        return hash == 0u ? 1u : hash;
    }

    static long QuantizePillarCoordinate(double value)
    {
        return value >= 0d
            ? (long)(value + 0.5d)
            : (long)(value - 0.5d);
    }

    static uint HashPillarCoordinate(uint hash, long value)
    {
        unchecked
        {
            ulong folded = (ulong)value;
            hash ^= (uint)folded;
            hash *= 16777619u;
            hash ^= (uint)(folded >> 32);
            hash *= 16777619u;
            return hash;
        }
    }

    static bool ShouldApplyCameraFacingOverhangNoise(VoxelPipelineData data)
    {
        if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext playerContext))
            return true;

        float3 cameraForward = NormalizeFastOrDefault(
            playerContext.LookState.AimForward,
            NormalizeFastOrDefault(playerContext.MovementState.CameraForward, playerContext.MovementState.Forward));
        if (math.lengthsq(cameraForward) <= 0.0001f)
            return true;

        AbsoluteUniversePosition playerAup = playerContext.MovementState.PredictedAup;
        AbsoluteUniversePosition chunkCenterAup = BuildCapturedAup(data.WorldCenter, data.AbsoluteUniverseOffsetAtStartDouble);
        float3 cameraToChunk = AbsoluteUniversePosition.ToCameraRelativeFloat3(in chunkCenterAup, in playerAup);
        float cameraToChunkSq = math.lengthsq(cameraToChunk);
        if (cameraToChunkSq <= 0.0001f)
            return true;

        float facingDot = math.dot(cameraToChunk, cameraForward) / math.max(LengthApprox(cameraToChunk), 0.0001f);
        return facingDot > OverhangCameraCullDotThreshold;
    }

    static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
    {
        float lengthSq = math.lengthsq(value);
        return lengthSq > 0.0001f ? value / math.max(LengthApprox(value), 0.0001f) : fallback;
    }

    static float LengthApprox(float3 value)
    {
        float3 axis = math.abs(value);
        float maxAxis = math.cmax(axis);
        float minAxis = math.cmin(axis);
        float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
        return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
    }

    static int ResolveBiomeSdfModifierEnabled(int lodLevel)
    {
        if (lodLevel >= 2)
            return 0;

        return 1;
    }

    async Awaitable<bool> TryPrepareModifiedCellsForPipelineAsync(VoxelPipelineData data, CancellationToken ct)
    {
        if (data == null || data.SourceVolume == null || _deltaProcessor == null)
            return true;

        if (!_deltaProcessor.TryMeasureDeltaMapForVolume(data.SourceVolume, out int measuredModifiedCellCapacity) ||
            measuredModifiedCellCapacity <= 0)
        {
            return true;
        }

        int modifiedCellCapacity = math.min(measuredModifiedCellCapacity, math.max(1, data.TotalCells));
        if (!TryPrepareModifiedCellsScratch(
                ref data.ScratchLease,
                modifiedCellCapacity,
                out NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells))
        {
            return false;
        }

        if (await _deltaProcessor.TryFillDeltaMapForVolumeAsync(
                data.SourceVolume,
                modifiedCells,
                ChunkGenerationFrameBudgetTicks,
                ct) &&
            modifiedCells.Count() > 0)
        {
            data.ModifiedCells = modifiedCells;
            data.UsesStreamingScratchModifiedCells = true;
            return true;
        }

        modifiedCells.Clear();
        data.ModifiedCells = default;
        data.UsesStreamingScratchModifiedCells = false;
        return true;
    }

    async Awaitable<bool> ExecuteVoxelPipelineAsync(VoxelPipelineData data, CancellationToken ct)
    {
        if (!data.ScratchLease.IsValid)
        {
            data.ScratchLease = await AcquireStreamingScratchLeaseAsync(data.PtsX * data.PtsZ, data.TotalPts, data.TotalCells, data.GridDimension, ct);
        }
        else if (data.ScratchLease._owner != this)
        {
            return false;
        }

        if (!data.ScratchLease.IsValid)
            return false;

        long chunkGenerationFrameStart = Stopwatch.GetTimestamp();

        BuildSpatialPartitions(data);
        chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
        if (!await TryPrepareModifiedCellsForPipelineAsync(data, ct))
            return false;

        NativeArray<float> terrainHeights = data.ScratchLease.TerrainHeights;
        NativeArray<float> gridBiome = data.ScratchLease.GridBiome;
        NativeArray<float> densityField = data.ScratchLease.DensityField;
        NativeArray<float> smoothDensityField = data.ScratchLease.SmoothDensityField;
        NativeArray<float> overhangDensityField = data.ScratchLease.OverhangDensityField;
        NativeArray<AnomalyFeatureRecord> anomalyFeatureRecords = data.ScratchLease.AnomalyFeatureRecords;
        NativeArray<byte> anomalyFissureMask = data.ScratchLease.AnomalyFissureMask;
        NativeArray<AnomalyFeatureRecord> selectedPillarFeature = data.ScratchLease.SelectedPillarFeature;
        NativeArray<int> chunkContentFlags = data.ScratchLease.ChunkContentFlags;
        NativeArray<int> cellVertexCounts = data.ScratchLease.CellVertexCounts;
        NativeArray<int> cellVertexOffsets = data.ScratchLease.CellVertexOffsets;

        float fallbackHeight = data.TerrainHeightCenter;
        bool sampledHeightGrid = false;
        double3 absoluteGridOriginDouble = new double3(data.VolumeOrigin.x, 0d, data.VolumeOrigin.z) + data.AbsoluteUniverseOffsetAtStartDouble;
        Vector3 absoluteGridOrigin = ToVector3(absoluteGridOriginDouble);
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
            {
                for (int ix = 0; ix < data.PtsX; ix++)
                {
                    float wx = data.VolumeOrigin.x + ix * data.VoxelStep;
                    float wz = data.VolumeOrigin.z + iz * data.VoxelStep;
                    int hi = ix + iz * data.PtsX;

                    Vector3 absoluteSamplePosition = ToVector3(new double3(wx, 0d, wz) + data.AbsoluteUniverseOffsetAtStartDouble);
                    if (mapMagicBridge.TryGetHeightAUP(absoluteSamplePosition, out float sampledHeight))
                        terrainHeights[hi] = sampledHeight;
                    else
                        terrainHeights[hi] = fallbackHeight;
                }

                chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            }
        }

        chunkGenerationFrameStart = await FillBiomeModifierGridAsync(
            gridBiome,
            data,
            vegetationBridge,
            chunkGenerationFrameStart,
            ct);

        ct.ThrowIfCancellationRequested();
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
            gridBiome = gridBiome,
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
            absoluteNoiseOffset = ToFloat3(data.AbsoluteUniverseOffsetAtStartDouble),
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
            enableBiomeSdfModifiers = ResolveBiomeSdfModifierEnabled(data.LODLevel),
            density = densityField,
            smoothDensity = smoothDensityField
        }.Schedule(data.TotalPts, JOB_BATCH);
        long anomalySolveStartTimestamp = Stopwatch.GetTimestamp();

        double3 terrainOriginAup = absoluteGridOriginDouble;
        double3 sdfOriginAup = ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;
        JobHandle pillarDetectionHandle = HectonAnomalyEngine.ScheduleRidgeFeatureDetection(
            terrainHeights,
            anomalyFeatureRecords,
            anomalyFissureMask,
            new AnomalyRidgeDetectionSettings
            {
                Width = data.PtsX,
                Height = data.PtsZ,
                CellSizeMeters = data.VoxelStep,
                OriginAup = terrainOriginAup,
                MinimumPillarProminenceMeters = ChthonicPillarMinimumProminenceMeters,
                MinimumPillarRidgeArms = 3,
                MinimumFissureDepthMeters = float.MaxValue,
                EqualHeightEpsilon = 0.001f,
                FissureInfluencePacked = 0u,
                RequireTectonicBoundary = 1,
                TectonicBoundaryFrequency = mapMagicBridge != null
                    ? mapMagicBridge.SandboxTectonicSpineFrequency
                    : ChthonicPillarTectonicBoundaryFrequencyFallback,
                TectonicBoundarySeed = mapMagicBridge != null
                    ? mapMagicBridge.SandboxTectonicSpineSeed
                    : ChthonicPillarTectonicBoundarySeedFallback,
                MinimumTectonicBoundaryMask = ChthonicPillarMinimumTectonicBoundaryMask
            });
        JobHandle selectedPillarHandle = new SelectStrongestPillarFeatureJob
        {
            FeatureRecords = anomalyFeatureRecords,
            SelectedFeature = selectedPillarFeature
        }.Schedule(pillarDetectionHandle);

        if (ShouldApplyCameraFacingOverhangNoise(data))
        {
            densityHandle = HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise(
                densityField,
                overhangDensityField,
                data.PtsX,
                data.PtsY,
                data.PtsZ,
                data.VoxelStep,
                CliffOverhangSlopeThreshold,
                CliffOverhangLateralAmplitudeMeters,
                CliffOverhangNoiseFrequency,
                CliffOverhangBlendStrength,
                densityHandle);
            densityField = overhangDensityField;
        }

        var snapTopCellsJob = new SnapDualSDFTopCellsToTerrainJob
        {
            TerrainHeights = terrainHeights,
            TerrainWidth = data.PtsX,
            TerrainDepth = data.PtsZ,
            TerrainCellSizeMeters = data.VoxelStep,
            TerrainOriginAup = terrainOriginAup,
            Sdf = densityField,
            SecondarySdf = default,
            WriteSecondary = 0,
            SdfWidth = data.PtsX,
            SdfHeight = data.PtsY,
            SdfDepth = data.PtsZ,
            VoxelSizeMeters = data.VoxelStep,
            SdfOriginAup = sdfOriginAup,
            SnapHysteresisMeters = VoxelTerrainSnapHysteresisMeters
        };
        densityHandle = Unity.Jobs.IJobParallelForExtensions.Schedule(
            snapTopCellsJob,
            data.PtsX * data.PtsZ,
            JOB_BATCH,
            densityHandle);

        densityHandle = HectonAnomalyEngine.InjectSelectedMegaPillarSDF(
            densityField,
            selectedPillarFeature,
            data.PtsX,
            data.PtsY,
            data.PtsZ,
            data.VoxelStep,
            sdfOriginAup,
            ChthonicPillarRadiusMeters,
            ChthonicPillarHeightMeters,
            ChthonicPillarEdgeWarpMeters,
            ChthonicPillarNoiseFrequency,
            JobHandle.CombineDependencies(densityHandle, selectedPillarHandle));

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
        PublishVoxelAnomalySolveWarningIfNeeded(anomalySolveStartTimestamp);
        ct.ThrowIfCancellationRequested();
        if (chunkContentFlags[0] == 0)
        {
            data.RawCount = 0;
            return false;
        }

        densityHandle = chunkContentHandle;
        NativeArray<sbyte> quantizedDensityField = data.ScratchLease.QuantizedDensityField;
        float densityDecodeScale = ResolveDensityDecodeScale(data.VoxelStep);
        float densityDecodeInvScale = 1f / math.max(densityDecodeScale, 0.0001f);
        JobHandle quantizeDensityHandle = new VoxelDensityQuantizeJob
        {
            densityDecodeInvScale = densityDecodeInvScale,
            density = densityField,
            quantizedDensity = quantizedDensityField
        }.Schedule(data.TotalPts, JOB_BATCH, densityHandle);

        if (data.SourceVolume != null &&
            VoxelDynamicNavGridRuntime.TryScheduleBuild(
                data.SourceVolume,
                data.SourceRuntimeStamp,
                new int3(data.PtsX, data.PtsY, data.PtsZ),
                data.VolumeOrigin,
                data.VoxelStep,
                data.TotalPts,
                densityField,
                JOB_BATCH,
                densityHandle,
                out navGridHandle))
        {
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
            densityDecodeScale = densityDecodeScale,
            density = quantizedDensityField,
            edgeTable = MCTables.EdgeTable,
            triTable = MCTables.TriTable,
            cellVertexCounts = cellVertexCounts
        }.Schedule(data.TotalCells, JOB_BATCH, quantizeDensityHandle);

        JobHandle firstPhaseHandle = navGridScheduled
            ? JobHandle.CombineDependencies(mcCountHandle, navGridHandle)
            : mcCountHandle;
        await AwaitForJobCompletionAsync(firstPhaseHandle, ct, "density/count phase");
        if (navGridScheduled)
            VoxelDynamicNavGridRuntime.CommitBuild(data.SourceVolume, data.SourceRuntimeStamp);

        TryBindSelectedChthonicPillarResources(selectedPillarFeature);

        int exactRawVertexCount = 0;
        for (int cellIndex = 0; cellIndex < data.TotalCells; cellIndex++)
        {
            cellVertexOffsets[cellIndex] = exactRawVertexCount;
            exactRawVertexCount += cellVertexCounts[cellIndex];
            if ((cellIndex & 1023) == 1023)
                chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
        }

        data.RawCount = exactRawVertexCount;
        if (data.RawCount < 3)
            return false;

        int edgeVertexCountX = data.GridDimension * data.PtsY * data.PtsZ;
        int edgeVertexCountY = data.PtsX * data.GridDimension * data.PtsZ;
        int edgeVertexCountZ = data.PtsX * data.PtsY * data.GridDimension;
        if (!TryEnsureMeshExtractionScratchCapacity(
                ref data.ScratchLease,
                data.RawCount,
                edgeVertexCountX,
                edgeVertexCountY,
                edgeVertexCountZ))
        {
            return false;
        }

        data.UsesStreamingScratchMeshBuffers = true;
        data.RawVertices = data.ScratchLease.MeshRawVertices;
        data.WeldedPositions = data.ScratchLease.MeshWeldedPositions;
        data.TriangleIndices = data.ScratchLease.MeshTriangleIndices;
        data.EdgeVertexX = data.ScratchLease.MeshEdgeVertexX;
        data.EdgeVertexY = data.ScratchLease.MeshEdgeVertexY;
        data.EdgeVertexZ = data.ScratchLease.MeshEdgeVertexZ;

        JobHandle clearEdgeXHandle = new VoxelFillIntArrayJob
        {
            Value = -1,
            Values = data.EdgeVertexX
        }.Schedule(data.EdgeVertexX.Length, JOB_BATCH);
        JobHandle clearEdgeYHandle = new VoxelFillIntArrayJob
        {
            Value = -1,
            Values = data.EdgeVertexY
        }.Schedule(data.EdgeVertexY.Length, JOB_BATCH);
        JobHandle clearEdgeZHandle = new VoxelFillIntArrayJob
        {
            Value = -1,
            Values = data.EdgeVertexZ
        }.Schedule(data.EdgeVertexZ.Length, JOB_BATCH);
        JobHandle clearEdgesHandle = JobHandle.CombineDependencies(clearEdgeXHandle, clearEdgeYHandle, clearEdgeZHandle);
        await AwaitForJobCompletionAsync(clearEdgesHandle, ct, "edge vertex registry clear");

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
            densityDecodeScale = densityDecodeScale,
            density = quantizedDensityField,
            edgeTable = MCTables.EdgeTable,
            triTable = MCTables.TriTable,
            cellVertexOffsets = cellVertexOffsets,
            cellVertexCounts = cellVertexCounts,
            outVertices = data.RawVertices
        }.Schedule(data.TotalCells, JOB_BATCH);

        await AwaitForJobCompletionAsync(mcHandle, ct, "marching-cubes extract");

        ct.ThrowIfCancellationRequested();

        NativeArray<int> weldedCounter = data.ScratchLease.MeshWeldedCounter;
        weldedCounter[0] = 0;

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
            weldedCounter[0] = 0;
        }

        if (data.WeldedCount < 3)
            return false;

        ct.ThrowIfCancellationRequested();

        if (!TryEnsureMeshAttributeScratchCapacity(ref data.ScratchLease, data.WeldedCount))
            return false;

        data.UsesStreamingScratchAttributeBuffers = true;
        data.Normals = data.ScratchLease.MeshNormals;
        data.CurvatureValues = data.ScratchLease.MeshCurvatureValues;
        data.AmbientOcclusionValues = data.ScratchLease.MeshAmbientOcclusionValues;
        data.BiomeValues = data.ScratchLease.MeshBiomeValues;
        data.SkirtAlphaValues = data.ScratchLease.MeshSkirtAlphaValues;
        data.DirtyBlendValues = data.ScratchLease.MeshDirtyBlendValues;
        data.Colors = data.ScratchLease.MeshColors;
        if (data.ExtractSpawnPoints)
        {
            int maxSpawnPoints = math.max(data.WeldedCount / 20, 64);
            if (!TryPrepareSpawnPointScratch(
                    ref data.ScratchLease,
                    maxSpawnPoints,
                    out data.SpawnPointList,
                    out data.SpawnPointListNativeMemoryId))
            {
                return false;
            }

            data.UsesStreamingScratchSpawnPoints = true;
        }

        JobHandle clearSkirtAlphaHandle = new VoxelFillFloatArrayJob
        {
            Value = 0f,
            Values = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH);
        JobHandle clearDirtyBlendHandle = new VoxelFillFloatArrayJob
        {
            Value = 0f,
            Values = data.DirtyBlendValues
        }.Schedule(data.WeldedCount, JOB_BATCH);

        JobHandle seamSnapHandle = new VoxelTerrainSeamSnapJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            absoluteUniverseOffset = ToFloat3(data.AbsoluteUniverseOffsetAtStartDouble),
            voxelStep = data.VoxelStep,
            seamTransitionBand = TerrainVoxelSeamTransitionBand,
            seamOverlap = VoxelSeamDirector.TerrainOverlapMeters,
            terrainHeights = terrainHeights,
            positions = data.WeldedPositions
        }.Schedule(data.WeldedCount, JOB_BATCH);
        JobHandle skirtDependencyHandle = JobHandle.CombineDependencies(seamSnapHandle, clearSkirtAlphaHandle);

        JobHandle skirtHandle = new VoxelChunkSkirtExtrusionJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            voxelStep = data.VoxelStep,
            skirtDepthMeters = VoxelChunkSkirtDepthMeters,
            skirtWidthMeters = math.max(VoxelChunkSkirtWidthMeters, data.VoxelStep),
            lodLevel = data.LODLevel,
            positions = data.WeldedPositions,
            skirtAlphaValues = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH, skirtDependencyHandle);

        JobHandle normalHandle = new VoxelNormalJob
        {
            ptsX = data.PtsX,
            ptsY = data.PtsY,
            ptsZ = data.PtsZ,
            densityStrideY = data.PtsX,
            densityStrideZ = data.PtsX * data.PtsY,
            volumeOrigin = data.VolumeOrigin,
            invVoxelStep = 1f / math.max(data.VoxelStep, 0.0001f),
            densityField = quantizedDensityField,
            positions = data.WeldedPositions,
            normals = data.Normals,
            curvatureValues = data.CurvatureValues,
            ambientOcclusionValues = data.AmbientOcclusionValues
        }.Schedule(data.WeldedCount, JOB_BATCH, skirtHandle);

        JobHandle seamNormalHandle = new VoxelSeamNormalBlendJob
        {
            ptsX = data.PtsX,
            ptsZ = data.PtsZ,
            volumeOrigin = data.VolumeOrigin,
            absoluteUniverseOffset = ToFloat3(data.AbsoluteUniverseOffsetAtStartDouble),
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
        }.Schedule(data.WeldedCount, JOB_BATCH, skirtHandle);

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
            ambientOcclusionValues = data.AmbientOcclusionValues,
            biomeValues = data.BiomeValues,
            caveEntrances = data.Entrances,
            modifiedCells = data.ModifiedCells,
            absoluteUniverseOffset = ToFloat3(data.AbsoluteUniverseOffsetAtStartDouble),
            colors = data.Colors,
            skirtAlphaValues = data.SkirtAlphaValues
        }.Schedule(data.WeldedCount, JOB_BATCH, colorDeps);

        JobHandle phase5Handle = JobHandle.CombineDependencies(colorHandle, clearDirtyBlendHandle);
        if (data.ModifiedCells.IsCreated)
        {
            JobHandle dirtyBlendDependencyHandle = JobHandle.CombineDependencies(skirtHandle, clearDirtyBlendHandle);
            JobHandle dirtyBlendHandle = new VoxelDirtyBlendJob
            {
                positions = data.WeldedPositions,
                modifiedCells = data.ModifiedCells,
                voxelStep = data.VoxelStep,
                absoluteUniverseOffset = ToFloat3(data.AbsoluteUniverseOffsetAtStartDouble),
                dirtyBlendValues = data.DirtyBlendValues
            }.Schedule(data.WeldedCount, JOB_BATCH, dirtyBlendDependencyHandle);

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
                spawnPoints = data.SpawnPointList
            }.Schedule(normalHandle);

            phase5Handle = JobHandle.CombineDependencies(phase5Handle, spawnHandle);
        }

        await AwaitForJobCompletionAsync(phase5Handle, ct, "normal/color/spawn phase");
        ct.ThrowIfCancellationRequested();
        return true;
    }

    async Awaitable<long> FillBiomeModifierGridAsync(
        NativeArray<float> gridBiome,
        VoxelPipelineData data,
        HectonMapMagicVegetationBridge vegetationBridge,
        long chunkGenerationFrameStart,
        CancellationToken ct)
    {
        if (!gridBiome.IsCreated)
            return chunkGenerationFrameStart;

        bool monolithReady = H8StaticDataArena.IsLoaded;
        HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload = default;
        bool hasHeightPayload = vegetationBridge != null &&
            vegetationBridge.TryGetActiveHeightTexturePayload(out heightPayload);
        Vector3 terrainPosition = hasHeightPayload ? heightPayload.TerrainPosition : Vector3.zero;
        Vector3 terrainSize = hasHeightPayload ? heightPayload.TerrainSize : Vector3.zero;
        float invTerrainSizeX = hasHeightPayload ? math.rcp(math.max(terrainSize.x, 0.001f)) : 0f;
        float invTerrainSizeZ = hasHeightPayload ? math.rcp(math.max(terrainSize.z, 0.001f)) : 0f;
        float fallbackTileSize = math.max(mapMagicTileSize, data.VoxelStep * math.max(data.PtsX - 1, 1));
        float fallbackInvTileSize = math.rcp(math.max(fallbackTileSize, 0.001f));
        bool hasCachedBiomeHash = false;
        uint cachedBiomeHash = 0u;
        float cachedBiomeModifier = 0f;

        for (int iz = 0; iz < data.PtsZ; iz++)
        {
            double absoluteZ = (double)data.VolumeOrigin.z + iz * data.VoxelStep + data.AbsoluteUniverseOffsetAtStartDouble.z;
            for (int ix = 0; ix < data.PtsX; ix++)
            {
                int gridIndex = ix + iz * data.PtsX;
                float modifier = 0f;
                if (monolithReady)
                {
                    double absoluteX = (double)data.VolumeOrigin.x + ix * data.VoxelStep + data.AbsoluteUniverseOffsetAtStartDouble.x;
                    float u = hasHeightPayload
                        ? math.saturate((float)((absoluteX - terrainPosition.x) * invTerrainSizeX))
                        : math.frac((float)(absoluteX * fallbackInvTileSize));
                    float v = hasHeightPayload
                        ? math.saturate((float)((absoluteZ - terrainPosition.z) * invTerrainSizeZ))
                        : math.frac((float)(absoluteZ * fallbackInvTileSize));
                    int heatmapX = math.clamp((int)(u * BiomeHeatmapMaxIndex + 0.5f), 0, BiomeHeatmapMaxIndex);
                    int heatmapY = math.clamp((int)(v * BiomeHeatmapMaxIndex + 0.5f), 0, BiomeHeatmapMaxIndex);
                    if (H8StaticDataArena.TryGetBiomeHeatmapCell(heatmapX, heatmapY, out uint biomeHash))
                    {
                        if (hasCachedBiomeHash && cachedBiomeHash == biomeHash)
                        {
                            modifier = cachedBiomeModifier;
                        }
                        else
                        {
                            modifier = ResolveAlienBiomeModifierWeight(biomeHash);
                            cachedBiomeHash = biomeHash;
                            cachedBiomeModifier = modifier;
                            hasCachedBiomeHash = true;
                        }
                    }
                }

                gridBiome[gridIndex] = modifier;
            }

            chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
        }

        return chunkGenerationFrameStart;
    }

    static float ResolveAlienBiomeModifierWeight(uint biomeHash)
    {
        if (biomeHash == 0u)
            return 0f;

        if (biomeHash == _VoxelAlienBiomeHash ||
            biomeHash == _VoxelAlienShortBiomeHash)
        {
            return 1f;
        }

        if (TryResolveVoxelBiomeRecord(biomeHash, out H8BiomeRecord record))
        {
            if (record.SurfaceId == _VoxelAlienSurfaceHash ||
                record.HeatmapId == _VoxelAlienHeatmapHash ||
                record.RadiationFieldHash == _VoxelAlienRadiationHash)
            {
                return 1f;
            }

        }

        return 0f;
    }

    static unsafe bool TryResolveVoxelBiomeRecord(uint biomeHash, out H8BiomeRecord record)
    {
        record = default;
        if (biomeHash == 0u)
            return false;

        H8BiomeRecord* records = (H8BiomeRecord*)H8StaticDataArena.GetSectionDataPointer(
            H8DataSectionId.Biomes,
            H8DataLayoutConstants.BiomeRecordSize,
            out int count);
        if (records == null || count <= 0)
            return false;

        int low = 0;
        int high = count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            H8BiomeRecord candidate = records[mid];
            if (candidate.BiomeHash == biomeHash)
            {
                record = candidate;
                return true;
            }

            if (candidate.BiomeHash < biomeHash)
                low = mid + 1;
            else
                high = mid - 1;
        }

        return false;
    }

    async Awaitable<VoxelStreamingScratchLease> AcquireStreamingScratchLeaseAsync(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension,
        CancellationToken ct)
    {
        int waitFrames = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (TryAcquireStreamingScratchLease(heightCount, totalPointCount, totalCellCount, gridDimension, out VoxelStreamingScratchLease lease))
                return lease;

            if (waitFrames >= StreamingScratchLeaseTimeoutFrames)
            {
                LogStreamingScratchLeaseTimeout(heightCount, totalPointCount, totalCellCount, waitFrames);
                return default;
            }

            waitFrames++;
            await AwaitableDebtMonitor.NextFrameAsync(ct);
        }
    }

    void LogStreamingScratchLeaseTimeout(
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int waitFrames)
    {
#if UNITY_EDITOR
        Debug.LogError("[HectonVoxel] Streaming scratch lease timed out.");
#endif
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
        int gridDimension,
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

                if (!TryEnsureStreamingScratchSlotCapacity(slot, heightCount, totalPointCount, totalCellCount, gridDimension))
                    continue;

                slot.InUse = true;

                lease = new VoxelStreamingScratchLease(
                    this,
                    i,
                    slot.TerrainHeights,
                    slot.GridBiome,
                    slot.DensityField,
                    slot.SmoothDensityField,
                    slot.OverhangDensityField,
                    slot.QuantizedDensityField,
                    slot.AnomalyFeatureRecords,
                    slot.AnomalyFissureMask,
                    slot.SelectedPillarFeature,
                    slot.ChunkContentFlags,
                    slot.CellVertexCounts,
                    slot.CellVertexOffsets,
                    slot.MeshRawVertices,
                    slot.MeshWeldedPositions,
                    slot.MeshTriangleIndices,
                    slot.MeshEdgeVertexX,
                    slot.MeshEdgeVertexY,
                    slot.MeshEdgeVertexZ,
                    slot.MeshWeldedCounter,
                    slot.MeshNormals,
                    slot.MeshCurvatureValues,
                    slot.MeshAmbientOcclusionValues,
                    slot.MeshBiomeValues,
                    slot.MeshSkirtAlphaValues,
                    slot.MeshDirtyBlendValues,
                    slot.MeshColors,
                    slot.ProjectedLocalPositions,
                    slot.SpatialBucketCounts,
                    slot.SpatialBucketWriteHeads,
                    slot.SpatialNodeBucketOffsets,
                    slot.SpatialNodeBucketIndices,
                    slot.SpatialTunnelBucketOffsets,
                    slot.SpatialTunnelBucketIndices,
                    slot.RebuildNodes,
                    slot.RebuildTunnels,
                    slot.RebuildEntrances,
                    slot.RebuildStructures,
                    slot.RebuildCraterStamps,
                    slot.SpawnPointListScratch,
                    slot.ModifiedCellsScratch,
                    slot.ColliderTriangleBuckets,
                    slot.ColliderBucketCounts,
                    slot.ColliderBucketOffsets,
                    slot.ColliderBucketWriteHeads,
                    slot.ColliderChunkTriangleIndices,
                    slot.ColliderLocalRemap,
                    slot.ColliderTouchedVertexGlobals,
                    slot.ColliderLocalPositions,
                    slot.ColliderLocalIndices);
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
        int slotCount = math.clamp(streamingScratchSlotCount, 1, 8);
        if (_streamingScratchSlots != null && _streamingScratchSlots.Length == slotCount)
            return;

        if (_streamingScratchSlots != null && HasStreamingScratchSlotInUse_NoLock())
            return;

        DisposeStreamingScratchSlots();
        _streamingScratchSlots = new VoxelStreamingScratchSlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            _streamingScratchSlots[i] = new VoxelStreamingScratchSlot();
    }

    bool HasStreamingScratchSlotInUse_NoLock()
    {
        if (_streamingScratchSlots == null)
            return false;

        for (int i = 0; i < _streamingScratchSlots.Length; i++)
        {
            VoxelStreamingScratchSlot slot = _streamingScratchSlots[i];
            if (slot != null && slot.InUse)
                return true;
        }

        return false;
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
        int totalCellCount,
        int gridDimension)
    {
        int meshRawScratchCapacity = ResolveStreamingMeshRawScratchCapacity(totalCellCount);
        ResolveStreamingEdgeVertexScratchCapacity(
            gridDimension,
            out int edgeVertexCountX,
            out int edgeVertexCountY,
            out int edgeVertexCountZ);

        EnsureNativeArrayCapacity(ref slot.TerrainHeights, heightCount, nameof(VoxelStreamingScratchSlot.TerrainHeights));
        EnsureNativeArrayCapacity(ref slot.GridBiome, heightCount, nameof(VoxelStreamingScratchSlot.GridBiome));
        EnsureNativeArrayCapacity(ref slot.DensityField, totalPointCount, nameof(VoxelStreamingScratchSlot.DensityField));
        EnsureNativeArrayCapacity(ref slot.SmoothDensityField, totalPointCount, nameof(VoxelStreamingScratchSlot.SmoothDensityField));
        EnsureNativeArrayCapacity(ref slot.OverhangDensityField, totalPointCount, nameof(VoxelStreamingScratchSlot.OverhangDensityField));
        EnsureNativeArrayCapacity(ref slot.QuantizedDensityField, totalPointCount, nameof(VoxelStreamingScratchSlot.QuantizedDensityField));
        EnsureNativeArrayCapacity(ref slot.AnomalyFeatureRecords, heightCount, nameof(VoxelStreamingScratchSlot.AnomalyFeatureRecords));
        EnsureNativeArrayCapacity(ref slot.AnomalyFissureMask, heightCount, nameof(VoxelStreamingScratchSlot.AnomalyFissureMask));
        EnsureNativeArrayCapacity(ref slot.SelectedPillarFeature, 1, nameof(VoxelStreamingScratchSlot.SelectedPillarFeature), true);
        EnsureNativeArrayCapacity(ref slot.ChunkContentFlags, 1, nameof(VoxelStreamingScratchSlot.ChunkContentFlags), true);
        EnsureNativeArrayCapacity(ref slot.CellVertexCounts, totalCellCount, nameof(VoxelStreamingScratchSlot.CellVertexCounts));
        EnsureNativeArrayCapacity(ref slot.CellVertexOffsets, totalCellCount, nameof(VoxelStreamingScratchSlot.CellVertexOffsets));
        EnsureNativeArrayCapacity(ref slot.MeshRawVertices, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshRawVertices));
        EnsureNativeArrayCapacity(ref slot.MeshWeldedPositions, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshWeldedPositions));
        EnsureNativeArrayCapacity(ref slot.MeshTriangleIndices, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshTriangleIndices));
        EnsureNativeArrayCapacity(ref slot.MeshEdgeVertexX, edgeVertexCountX, nameof(VoxelStreamingScratchSlot.MeshEdgeVertexX));
        EnsureNativeArrayCapacity(ref slot.MeshEdgeVertexY, edgeVertexCountY, nameof(VoxelStreamingScratchSlot.MeshEdgeVertexY));
        EnsureNativeArrayCapacity(ref slot.MeshEdgeVertexZ, edgeVertexCountZ, nameof(VoxelStreamingScratchSlot.MeshEdgeVertexZ));
        EnsureNativeArrayCapacity(ref slot.MeshWeldedCounter, 1, nameof(VoxelStreamingScratchSlot.MeshWeldedCounter), true);
        EnsureNativeArrayCapacity(ref slot.MeshNormals, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshNormals));
        EnsureNativeArrayCapacity(ref slot.MeshCurvatureValues, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshCurvatureValues));
        EnsureNativeArrayCapacity(ref slot.MeshAmbientOcclusionValues, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshAmbientOcclusionValues));
        EnsureNativeArrayCapacity(ref slot.MeshBiomeValues, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshBiomeValues));
        EnsureNativeArrayCapacity(ref slot.MeshSkirtAlphaValues, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshSkirtAlphaValues));
        EnsureNativeArrayCapacity(ref slot.MeshDirtyBlendValues, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshDirtyBlendValues));
        EnsureNativeArrayCapacity(ref slot.MeshColors, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.MeshColors));
        EnsureNativeArrayCapacity(ref slot.ProjectedLocalPositions, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.ProjectedLocalPositions));
        EnsureNativeArrayCapacity(ref slot.SpatialBucketCounts, StreamingSpatialBucketScratchCapacity, nameof(VoxelStreamingScratchSlot.SpatialBucketCounts));
        EnsureNativeArrayCapacity(ref slot.SpatialBucketWriteHeads, StreamingSpatialBucketScratchCapacity, nameof(VoxelStreamingScratchSlot.SpatialBucketWriteHeads));
        EnsureNativeArrayCapacity(ref slot.SpatialNodeBucketOffsets, StreamingSpatialBucketScratchCapacity + 1, nameof(VoxelStreamingScratchSlot.SpatialNodeBucketOffsets));
        EnsureNativeArrayCapacity(ref slot.SpatialNodeBucketIndices, StreamingNodeSpatialReferenceScratchCapacity, nameof(VoxelStreamingScratchSlot.SpatialNodeBucketIndices));
        EnsureNativeArrayCapacity(ref slot.SpatialTunnelBucketOffsets, StreamingSpatialBucketScratchCapacity + 1, nameof(VoxelStreamingScratchSlot.SpatialTunnelBucketOffsets));
        EnsureNativeArrayCapacity(ref slot.SpatialTunnelBucketIndices, StreamingTunnelSpatialReferenceScratchCapacity, nameof(VoxelStreamingScratchSlot.SpatialTunnelBucketIndices));
        EnsureNativeArrayCapacity(ref slot.RebuildNodes, StreamingCaveGraphNodeScratchCapacity, nameof(VoxelStreamingScratchSlot.RebuildNodes));
        EnsureNativeArrayCapacity(ref slot.RebuildTunnels, StreamingCaveGraphTunnelScratchCapacity, nameof(VoxelStreamingScratchSlot.RebuildTunnels));
        EnsureNativeArrayCapacity(ref slot.RebuildEntrances, StreamingCaveGraphEntranceScratchCapacity, nameof(VoxelStreamingScratchSlot.RebuildEntrances));
        EnsureNativeArrayCapacity(ref slot.RebuildStructures, StreamingCaveGraphStructureScratchCapacity, nameof(VoxelStreamingScratchSlot.RebuildStructures));
        EnsureNativeArrayCapacity(ref slot.RebuildCraterStamps, StreamingCraterStampScratchCapacity, nameof(VoxelStreamingScratchSlot.RebuildCraterStamps));
        EnsureSpawnPointScratchCapacity(slot, ResolveStreamingSpawnPointScratchCapacity(totalCellCount));
        EnsureModifiedCellsScratchCapacity(slot, math.max(1, totalCellCount));
        EnsureNativeArrayCapacity(ref slot.ColliderTriangleBuckets, math.max(1, meshRawScratchCapacity / 3), nameof(VoxelStreamingScratchSlot.ColliderTriangleBuckets));
        EnsureNativeArrayCapacity(ref slot.ColliderBucketCounts, StreamingColliderChunkScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderBucketCounts));
        EnsureNativeArrayCapacity(ref slot.ColliderBucketOffsets, StreamingColliderChunkScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderBucketOffsets));
        EnsureNativeArrayCapacity(ref slot.ColliderBucketWriteHeads, StreamingColliderChunkScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderBucketWriteHeads));
        EnsureNativeArrayCapacity(ref slot.ColliderChunkTriangleIndices, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderChunkTriangleIndices));
        EnsureNativeArrayCapacity(ref slot.ColliderLocalRemap, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderLocalRemap));
        EnsureNativeArrayCapacity(ref slot.ColliderTouchedVertexGlobals, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderTouchedVertexGlobals));
        EnsureNativeArrayCapacity(ref slot.ColliderLocalPositions, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderLocalPositions));
        EnsureNativeArrayCapacity(ref slot.ColliderLocalIndices, meshRawScratchCapacity, nameof(VoxelStreamingScratchSlot.ColliderLocalIndices));
    }

    static bool TryEnsureStreamingScratchSlotCapacity(
        VoxelStreamingScratchSlot slot,
        int heightCount,
        int totalPointCount,
        int totalCellCount,
        int gridDimension)
    {
        try
        {
            EnsureStreamingScratchSlotCapacity(slot, heightCount, totalPointCount, totalCellCount, gridDimension);
            return true;
        }
        catch (Exception ex)
        {
            ReportVoxelMeshScratchCapacityOverflow();
#if UNITY_EDITOR
            Debug.LogException(ex);
#endif
            return false;
        }
    }

    static int ResolveStreamingMeshRawScratchCapacity(int totalCellCount)
    {
        long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
        int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
        if (desired > qualityCapacity)
            return qualityCapacity;

        return desired < 1L ? 1 : (int)desired;
    }

    static int ResolveStreamingMeshRawScratchQualityCapacity()
    {
        float quality = HomeostasisBrain.GlobalQualityWeight;
        float q = math.saturate(math.isfinite(quality) ? quality : 1f);
        float smooth = q * q * (3f - 2f * q);
        return math.clamp(
            (int)math.round(math.lerp(
                StreamingMeshRawVertexScratchLowTierCapacity,
                StreamingMeshRawVertexScratchVisualOverkillCapacity,
                smooth)),
            StreamingMeshRawVertexScratchLowTierCapacity,
            StreamingMeshRawVertexScratchVisualOverkillCapacity);
    }

    static void ResolveStreamingEdgeVertexScratchCapacity(
        int gridDimension,
        out int edgeVertexCountX,
        out int edgeVertexCountY,
        out int edgeVertexCountZ)
    {
        int grid = math.clamp(gridDimension, 16, 128);
        int points = grid + 1;
        edgeVertexCountX = math.max(1, grid * points * points);
        edgeVertexCountY = math.max(1, points * grid * points);
        edgeVertexCountZ = math.max(1, points * points * grid);
    }

    bool TryEnsureMeshExtractionScratchCapacity(
        ref VoxelStreamingScratchLease lease,
        int rawCount,
        int edgeVertexCountX,
        int edgeVertexCountY,
        int edgeVertexCountZ)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeRawCount = math.max(1, rawCount);
        int safeEdgeVertexCountX = math.max(1, edgeVertexCountX);
        int safeEdgeVertexCountY = math.max(1, edgeVertexCountY);
        int safeEdgeVertexCountZ = math.max(1, edgeVertexCountZ);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.MeshRawVertices.IsCreated || slot.MeshRawVertices.Length < safeRawCount ||
                !slot.MeshWeldedPositions.IsCreated || slot.MeshWeldedPositions.Length < safeRawCount ||
                !slot.MeshTriangleIndices.IsCreated || slot.MeshTriangleIndices.Length < safeRawCount ||
                !slot.MeshEdgeVertexX.IsCreated || slot.MeshEdgeVertexX.Length < safeEdgeVertexCountX ||
                !slot.MeshEdgeVertexY.IsCreated || slot.MeshEdgeVertexY.Length < safeEdgeVertexCountY ||
                !slot.MeshEdgeVertexZ.IsCreated || slot.MeshEdgeVertexZ.Length < safeEdgeVertexCountZ ||
                !slot.MeshWeldedCounter.IsCreated || slot.MeshWeldedCounter.Length < 1)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            slot.MeshWeldedCounter[0] = 0;

            lease.MeshRawVertices = slot.MeshRawVertices;
            lease.MeshWeldedPositions = slot.MeshWeldedPositions;
            lease.MeshTriangleIndices = slot.MeshTriangleIndices;
            lease.MeshEdgeVertexX = slot.MeshEdgeVertexX;
            lease.MeshEdgeVertexY = slot.MeshEdgeVertexY;
            lease.MeshEdgeVertexZ = slot.MeshEdgeVertexZ;
            lease.MeshWeldedCounter = slot.MeshWeldedCounter;
        }

        return true;
    }

    bool TryEnsureMeshAttributeScratchCapacity(ref VoxelStreamingScratchLease lease, int weldedCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeWeldedCount = math.max(1, weldedCount);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.MeshNormals.IsCreated || slot.MeshNormals.Length < safeWeldedCount ||
                !slot.MeshCurvatureValues.IsCreated || slot.MeshCurvatureValues.Length < safeWeldedCount ||
                !slot.MeshAmbientOcclusionValues.IsCreated || slot.MeshAmbientOcclusionValues.Length < safeWeldedCount ||
                !slot.MeshBiomeValues.IsCreated || slot.MeshBiomeValues.Length < safeWeldedCount ||
                !slot.MeshSkirtAlphaValues.IsCreated || slot.MeshSkirtAlphaValues.Length < safeWeldedCount ||
                !slot.MeshDirtyBlendValues.IsCreated || slot.MeshDirtyBlendValues.Length < safeWeldedCount ||
                !slot.MeshColors.IsCreated || slot.MeshColors.Length < safeWeldedCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.MeshNormals = slot.MeshNormals;
            lease.MeshCurvatureValues = slot.MeshCurvatureValues;
            lease.MeshAmbientOcclusionValues = slot.MeshAmbientOcclusionValues;
            lease.MeshBiomeValues = slot.MeshBiomeValues;
            lease.MeshSkirtAlphaValues = slot.MeshSkirtAlphaValues;
            lease.MeshDirtyBlendValues = slot.MeshDirtyBlendValues;
            lease.MeshColors = slot.MeshColors;
        }

        return true;
    }

    bool TryEnsureProjectionScratchCapacity(ref VoxelStreamingScratchLease lease, int vertexCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeVertexCount = math.max(1, vertexCount);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.ProjectedLocalPositions.IsCreated || slot.ProjectedLocalPositions.Length < safeVertexCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.ProjectedLocalPositions = slot.ProjectedLocalPositions;
        }

        return true;
    }

    bool TryEnsureSpatialBucketCounterScratchCapacity(ref VoxelStreamingScratchLease lease, int bucketCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeBucketCount = math.max(1, bucketCount);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.SpatialBucketCounts.IsCreated || slot.SpatialBucketCounts.Length < safeBucketCount ||
                !slot.SpatialBucketWriteHeads.IsCreated || slot.SpatialBucketWriteHeads.Length < safeBucketCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.SpatialBucketCounts = slot.SpatialBucketCounts;
            lease.SpatialBucketWriteHeads = slot.SpatialBucketWriteHeads;
        }

        return true;
    }

    bool TryEnsureNodeSpatialBucketScratchCapacity(ref VoxelStreamingScratchLease lease, int bucketCount, int totalReferences)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeOffsetCount = math.max(1, bucketCount + 1);
        int safeReferenceCount = math.max(1, totalReferences);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.SpatialNodeBucketOffsets.IsCreated || slot.SpatialNodeBucketOffsets.Length < safeOffsetCount ||
                !slot.SpatialNodeBucketIndices.IsCreated || slot.SpatialNodeBucketIndices.Length < safeReferenceCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.SpatialNodeBucketOffsets = slot.SpatialNodeBucketOffsets;
            lease.SpatialNodeBucketIndices = slot.SpatialNodeBucketIndices;
        }

        return true;
    }

    bool TryEnsureTunnelSpatialBucketScratchCapacity(ref VoxelStreamingScratchLease lease, int bucketCount, int totalReferences)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeOffsetCount = math.max(1, bucketCount + 1);
        int safeReferenceCount = math.max(1, totalReferences);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.SpatialTunnelBucketOffsets.IsCreated || slot.SpatialTunnelBucketOffsets.Length < safeOffsetCount ||
                !slot.SpatialTunnelBucketIndices.IsCreated || slot.SpatialTunnelBucketIndices.Length < safeReferenceCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.SpatialTunnelBucketOffsets = slot.SpatialTunnelBucketOffsets;
            lease.SpatialTunnelBucketIndices = slot.SpatialTunnelBucketIndices;
        }

        return true;
    }

    bool TryPrepareRebuildGraphScratch(
        ref VoxelStreamingScratchLease lease,
        int nodeCount,
        int tunnelCount,
        int entranceCount,
        int structureCount,
        int craterStampCount,
        out NativeArray<CaveNode> nodes,
        out NativeArray<CaveTunnel> tunnels,
        out NativeArray<CaveEntrance> entrances,
        out NativeArray<CaveStructure> structures,
        out NativeArray<VoxelCraterStamp> craterStamps)
    {
        nodes = default;
        tunnels = default;
        entrances = default;
        structures = default;
        craterStamps = default;
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        if (nodeCount > StreamingCaveGraphNodeScratchCapacity ||
            tunnelCount > StreamingCaveGraphTunnelScratchCapacity ||
            entranceCount > StreamingCaveGraphEntranceScratchCapacity ||
            structureCount > StreamingCaveGraphStructureScratchCapacity ||
            craterStampCount > StreamingCraterStampScratchCapacity)
        {
            return false;
        }

        int safeNodeCount = math.max(1, nodeCount);
        int safeTunnelCount = math.max(1, tunnelCount);
        int safeEntranceCount = math.max(1, entranceCount);
        int safeStructureCount = math.max(1, structureCount);
        int safeCraterStampCount = math.max(1, craterStampCount);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.RebuildNodes.IsCreated || slot.RebuildNodes.Length < safeNodeCount ||
                !slot.RebuildTunnels.IsCreated || slot.RebuildTunnels.Length < safeTunnelCount ||
                !slot.RebuildEntrances.IsCreated || slot.RebuildEntrances.Length < safeEntranceCount ||
                !slot.RebuildStructures.IsCreated || slot.RebuildStructures.Length < safeStructureCount ||
                !slot.RebuildCraterStamps.IsCreated || slot.RebuildCraterStamps.Length < safeCraterStampCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.RebuildNodes = slot.RebuildNodes;
            lease.RebuildTunnels = slot.RebuildTunnels;
            lease.RebuildEntrances = slot.RebuildEntrances;
            lease.RebuildStructures = slot.RebuildStructures;
            lease.RebuildCraterStamps = slot.RebuildCraterStamps;

            nodes = slot.RebuildNodes.GetSubArray(0, nodeCount);
            tunnels = slot.RebuildTunnels.GetSubArray(0, tunnelCount);
            entrances = slot.RebuildEntrances.GetSubArray(0, entranceCount);
            structures = slot.RebuildStructures.GetSubArray(0, structureCount);
            craterStamps = slot.RebuildCraterStamps.GetSubArray(0, craterStampCount);
        }

        return true;
    }

    bool TryPrepareSpawnPointScratch(
        ref VoxelStreamingScratchLease lease,
        int requiredCapacity,
        out NativeList<CaveSpawnData> spawnPointList,
        out int memoryId)
    {
        spawnPointList = default;
        memoryId = 0;
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeCapacity = math.max(1, requiredCapacity);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!EnsureSpawnPointScratchCapacity(slot, safeCapacity))
                return false;

            lease.SpawnPointListScratch = slot.SpawnPointListScratch;
            spawnPointList = slot.SpawnPointListScratch;
            memoryId = slot.SpawnPointListScratchMemoryId;
        }

        return true;
    }

    bool TryPrepareModifiedCellsScratch(
        ref VoxelStreamingScratchLease lease,
        int requiredCapacity,
        out NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells)
    {
        modifiedCells = default;
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeCapacity = math.max(1, requiredCapacity);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!EnsureModifiedCellsScratchCapacity(slot, safeCapacity))
                return false;

            lease.ModifiedCellsScratch = slot.ModifiedCellsScratch;
            modifiedCells = slot.ModifiedCellsScratch;
        }

        return true;
    }

    static int ResolveStreamingSpawnPointScratchCapacity(int totalCellCount)
    {
        return math.max(MinimumStreamingSpawnPointScratchCapacity, math.max(1, totalCellCount) / 10);
    }

    static bool EnsureSpawnPointScratchCapacity(VoxelStreamingScratchSlot slot, int requiredCapacity)
    {
        int safeCapacity = math.max(MinimumStreamingSpawnPointScratchCapacity, requiredCapacity);
        if (!slot.SpawnPointListScratch.IsCreated || slot.SpawnPointListScratchCapacity < safeCapacity)
        {
            if (slot.SpawnPointListScratch.IsCreated)
            {
                NativeMemorySentinel.Unregister(slot.SpawnPointListScratchMemoryId);
                slot.SpawnPointListScratch.Dispose(default);
                slot.SpawnPointListScratch = default;
                slot.SpawnPointListScratchCapacity = 0;
                slot.SpawnPointListScratchMemoryId = 0;
            }

            slot.SpawnPointListScratch = new NativeList<CaveSpawnData>(
                safeCapacity,
                DataVaultExemptVoxelSpawnPointAllocator);
            slot.SpawnPointListScratchCapacity = safeCapacity;
            slot.SpawnPointListScratchMemoryId = NativeMemorySentinel.RegisterNativeListInstance(
                slot.SpawnPointListScratch,
                NativeMemoryOwner,
                SpawnPointListNativeMemoryLabel,
                NativeMemoryLifetime);
        }

        slot.SpawnPointListScratch.Clear();
        return true;
    }

    static bool EnsureModifiedCellsScratchCapacity(VoxelStreamingScratchSlot slot, int requiredCapacity)
    {
        int safeCapacity = math.max(1, requiredCapacity);
        if (!slot.ModifiedCellsScratch.IsCreated || slot.ModifiedCellsScratchCapacity < safeCapacity)
        {
            if (slot.ModifiedCellsScratch.IsCreated)
            {
                NativeMemorySentinel.Unregister(slot.ModifiedCellsScratchMemoryId);
                slot.ModifiedCellsScratch.Dispose(default);
                slot.ModifiedCellsScratch = default;
                slot.ModifiedCellsScratchCapacity = 0;
                slot.ModifiedCellsScratchMemoryId = 0;
            }

            slot.ModifiedCellsScratch = new NativeParallelHashMap<int3, VoxelModifiedCell>(
                safeCapacity,
                DataVaultExemptVoxelPipelineScratchAllocator);
            slot.ModifiedCellsScratchCapacity = safeCapacity;
            slot.ModifiedCellsScratchMemoryId = NativeMemorySentinel.RegisterNativeParallelHashMapInstance(
                slot.ModifiedCellsScratch,
                NativeMemoryOwner,
                ModifiedCellsNativeMemoryLabel,
                NativeMemoryLifetime);
        }

        slot.ModifiedCellsScratch.Clear();
        return true;
    }

    bool TryEnsureColliderChunkScratchCapacity(
        ref VoxelStreamingScratchLease lease,
        int triangleCount,
        int triangleIndexCount,
        int vertexCount,
        int colliderChunkCount)
    {
        if (!lease.IsValid || lease._owner != this || lease._slotIndex < 0)
            return false;

        int safeTriangleCount = math.max(1, triangleCount);
        int safeTriangleIndexCount = math.max(1, triangleIndexCount);
        int safeVertexCount = math.max(1, vertexCount);
        int safeColliderChunkCount = math.max(1, colliderChunkCount);

        lock (_streamingScratchGate)
        {
            if (_streamingScratchSlots == null ||
                lease._slotIndex >= _streamingScratchSlots.Length ||
                _streamingScratchSlots[lease._slotIndex] == null ||
                !_streamingScratchSlots[lease._slotIndex].InUse)
            {
                return false;
            }

            VoxelStreamingScratchSlot slot = _streamingScratchSlots[lease._slotIndex];
            if (!slot.ColliderTriangleBuckets.IsCreated || slot.ColliderTriangleBuckets.Length < safeTriangleCount ||
                !slot.ColliderBucketCounts.IsCreated || slot.ColliderBucketCounts.Length < safeColliderChunkCount ||
                !slot.ColliderBucketOffsets.IsCreated || slot.ColliderBucketOffsets.Length < safeColliderChunkCount ||
                !slot.ColliderBucketWriteHeads.IsCreated || slot.ColliderBucketWriteHeads.Length < safeColliderChunkCount ||
                !slot.ColliderChunkTriangleIndices.IsCreated || slot.ColliderChunkTriangleIndices.Length < safeTriangleIndexCount ||
                !slot.ColliderLocalRemap.IsCreated || slot.ColliderLocalRemap.Length < safeVertexCount ||
                !slot.ColliderTouchedVertexGlobals.IsCreated || slot.ColliderTouchedVertexGlobals.Length < safeVertexCount ||
                !slot.ColliderLocalPositions.IsCreated || slot.ColliderLocalPositions.Length < safeVertexCount ||
                !slot.ColliderLocalIndices.IsCreated || slot.ColliderLocalIndices.Length < safeTriangleIndexCount)
            {
                ReportVoxelMeshScratchCapacityOverflow();
                return false;
            }

            lease.ColliderTriangleBuckets = slot.ColliderTriangleBuckets;
            lease.ColliderBucketCounts = slot.ColliderBucketCounts;
            lease.ColliderBucketOffsets = slot.ColliderBucketOffsets;
            lease.ColliderBucketWriteHeads = slot.ColliderBucketWriteHeads;
            lease.ColliderChunkTriangleIndices = slot.ColliderChunkTriangleIndices;
            lease.ColliderLocalRemap = slot.ColliderLocalRemap;
            lease.ColliderTouchedVertexGlobals = slot.ColliderTouchedVertexGlobals;
            lease.ColliderLocalPositions = slot.ColliderLocalPositions;
            lease.ColliderLocalIndices = slot.ColliderLocalIndices;
        }

        return true;
    }

    static float ResolveDensityDecodeScale(float voxelStep)
    {
        return math.max(voxelStep * 0.125f, 0.005f);
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
        array = new NativeArray<T>(requiredLength, DataVaultExemptVoxelPipelineScratchAllocator, options);
        RegisterTrackedNativeArray(array, label);
    }

    static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
    {
        NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
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
        long startTimestamp = Stopwatch.GetTimestamp();
        try
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
        finally
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d / Stopwatch.Frequency;
            RecordVoxelRebuildBudget(elapsedMilliseconds);
        }
    }

    static void RecordVoxelRebuildBudget(double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= VoxelRebuildBudgetMilliseconds)
        {
            _voxelRebuildOverBudgetConsecutive = 0;
            return;
        }

        _voxelRebuildOverBudgetConsecutive++;
        if (_voxelRebuildOverBudgetConsecutive < VoxelRebuildBudgetStrikeFrames)
            return;

        _voxelRebuildOverBudgetConsecutive = 0;
        LODSystemManager lodSystem = GlobalRegistry.LODSystem;
        if (lodSystem != null)
            lodSystem.ApplyEmergencyLODBiasStrike();

        CrashTelemetryBuffer.ReportCriticalPerformanceSpike(
            VoxelRebuildLaneHash,
            elapsedMilliseconds,
            unchecked((uint)Time.frameCount));
    }

    void BuildNodeSpatialBuckets(VoxelPipelineData data)
    {
        if (!data.Nodes.IsCreated || data.Nodes.Length == 0)
            return;

        int bucketCount = data.PartitionDimX * data.PartitionDimY * data.PartitionDimZ;
        if (!TryEnsureSpatialBucketCounterScratchCapacity(ref data.ScratchLease, bucketCount))
            return;

        data.UsesStreamingScratchSpatialBuckets = true;
        NativeArray<int> bucketCounts = data.ScratchLease.SpatialBucketCounts;
        NativeArray<int> writeHeads = data.ScratchLease.SpatialBucketWriteHeads;
        for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            bucketCounts[bucketIndex] = 0;
            writeHeads[bucketIndex] = 0;
        }

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

        int totalReferences = 0;
        if (!TryEnsureNodeSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, 1))
            return;

        data.NodeBucketOffsets = data.ScratchLease.SpatialNodeBucketOffsets;
        for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            data.NodeBucketOffsets[bucketIndex] = totalReferences;
            totalReferences += bucketCounts[bucketIndex];
        }

        data.NodeBucketOffsets[bucketCount] = totalReferences;
        if (totalReferences <= 0)
            return;

        if (!TryEnsureNodeSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, totalReferences))
        {
            data.NodeBucketOffsets = default;
            return;
        }

        data.NodeBucketOffsets = data.ScratchLease.SpatialNodeBucketOffsets;
        data.NodeBucketIndices = data.ScratchLease.SpatialNodeBucketIndices;
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

    void BuildTunnelSpatialBuckets(VoxelPipelineData data)
    {
        if (!data.Tunnels.IsCreated || data.Tunnels.Length == 0)
            return;

        int bucketCount = data.PartitionDimX * data.PartitionDimY * data.PartitionDimZ;
        if (!TryEnsureSpatialBucketCounterScratchCapacity(ref data.ScratchLease, bucketCount))
            return;

        data.UsesStreamingScratchSpatialBuckets = true;
        NativeArray<int> bucketCounts = data.ScratchLease.SpatialBucketCounts;
        NativeArray<int> writeHeads = data.ScratchLease.SpatialBucketWriteHeads;
        for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            bucketCounts[bucketIndex] = 0;
            writeHeads[bucketIndex] = 0;
        }

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

        int totalReferences = 0;
        if (!TryEnsureTunnelSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, 1))
            return;

        data.TunnelBucketOffsets = data.ScratchLease.SpatialTunnelBucketOffsets;
        for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            data.TunnelBucketOffsets[bucketIndex] = totalReferences;
            totalReferences += bucketCounts[bucketIndex];
        }

        data.TunnelBucketOffsets[bucketCount] = totalReferences;
        if (totalReferences <= 0)
            return;

        if (!TryEnsureTunnelSpatialBucketScratchCapacity(ref data.ScratchLease, bucketCount, totalReferences))
        {
            data.TunnelBucketOffsets = default;
            return;
        }

        data.TunnelBucketOffsets = data.ScratchLease.SpatialTunnelBucketOffsets;
        data.TunnelBucketIndices = data.ScratchLease.SpatialTunnelBucketIndices;
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
        if (!CanRegisterDeferredVoxelLateFrameWork())
            FlushDeferredVoxelWorkWithoutDispatcher();

        TryShutdownSharedTables();
    }

    static void TryShutdownSharedTables()
    {
        if (Volatile.Read(ref _liveEngineCount) > 0)
            return;

        if (Volatile.Read(ref _activeGenerationOperations) > 0)
            return;

        if (_voxelMeshPoolWarmupRunning)
            return;

        if (HasPendingVoxelDeferredWork())
            return;

        if (Interlocked.Exchange(ref _shutdownRequested, 0) == 1)
        {
            DestroyVoxelMeshPools();
            DisposeVoxelMeshPipelineBlackBox();
            MCTables.Shutdown();
        }
    }

    private static bool HasPendingVoxelDeferredWork()
    {
        return DeferredVoxelPhysicsBakePendingCount > 0 ||
               _deferredVoxelColliderUploads.Count > 0;
    }

    private static void FlushDeferredVoxelWorkWithoutDispatcher()
    {
        for (int i = _deferredVoxelPhysicsBakeTeardowns.Count - 1; i >= 0; i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeTeardowns[i];
            RemoveDeferredVoxelPhysicsBakeTeardownAt(i);
            ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly(
                pending.Handle,
                pending.Mesh,
                pending.Owner,
                pending.Renderer,
                pending.Collider,
                pending.Flags,
                pending.ProxyCollider,
                publishWarning: false);
        }

        for (int i = _deferredVoxelPhysicsBakeEmergencyCount - 1; i >= 0; i--)
        {
            DeferredVoxelPhysicsBakeTeardown pending = _deferredVoxelPhysicsBakeEmergencyTeardowns[i];
            RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt(i);
            ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly(
                pending.Handle,
                pending.Mesh,
                pending.Owner,
                pending.Renderer,
                pending.Collider,
                pending.Flags,
                pending.ProxyCollider,
                publishWarning: false);
        }

        if (DeferredVoxelPhysicsBakePendingCount == 0)
        {
            _deferredVoxelPhysicsBakeTeardownScanCursor = 0;
            _deferredVoxelPhysicsBakeEmergencyScanCursor = 0;
            if (_deferredVoxelPhysicsBakeTeardownRegistered && GlobalRegistry.Dispatcher != null)
                UnregisterDeferredVoxelPhysicsBakeTeardownDriver();
            else
                _deferredVoxelPhysicsBakeTeardownRegistered = false;
            UpdateDeferredVoxelPhysicsBakeBackpressure();
        }

        for (int i = _deferredVoxelColliderUploads.Count - 1; i >= 0; i--)
        {
            DeferredVoxelColliderUpload pending = _deferredVoxelColliderUploads[i];
            bool appliedUpload = false;
            if ((pending.Flags & DeferredVoxelColliderUploadVolumeFlag) != 0)
            {
                if (pending.Volume != null &&
                    pending.Volume.IsDeferredColliderChunkUploadReady(pending.ChunkIndex))
                {
                    appliedUpload = pending.Volume.CommitDeferredColliderChunkUpload(pending.ChunkIndex);
                }
            }
            else if (pending.Collider != null && pending.Mesh != null)
            {
                pending.Collider.enabled = false;
                if (pending.ProxyCollider != null)
                    pending.ProxyCollider.enabled = false;
                appliedUpload = true;
            }

            if (!appliedUpload)
                CancelDeferredVoxelColliderUpload(ref pending, publishRetryDropWarning: false);

            RemoveDeferredVoxelColliderUploadAt(i);
        }

        if (_deferredVoxelColliderUploads.Count == 0)
        {
            _deferredVoxelColliderUploadScanCursor = -1;
            _voxelColliderUploadDropWarningArmed = false;
            _voxelColliderUploadRetryDropWarningArmed = false;
            if (_deferredVoxelColliderUploadRegistered && GlobalRegistry.Dispatcher != null)
                UnregisterDeferredVoxelColliderUploadDriver();
            else
                _deferredVoxelColliderUploadRegistered = false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct VoxelSurfaceVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color32 Color;
        public Vector4 BakedOcclusionUv1;
        public Vector4 DirtyBlendUv2;
        public Vector4 AbsolutePositionWS;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct VoxelColliderVertex
    {
        public Vector3 Position;
    }

    readonly struct VoxelFinalizeProjectionState
    {
        public readonly OriginShiftEventData StableShift;
        public readonly Vector3 RootRuntimePosition;
        public readonly byte ShiftEpochChanged;

        public VoxelFinalizeProjectionState(in OriginShiftEventData stableShift, Vector3 rootRuntimePosition, bool shiftEpochChanged)
        {
            StableShift = stableShift;
            RootRuntimePosition = rootRuntimePosition;
            ShiftEpochChanged = shiftEpochChanged ? (byte)1 : (byte)0;
        }

        public double3 AbsolutePositionOffsetDouble => StableShift.NewTotalOffsetDouble + ToDouble3(RootRuntimePosition);

        public float3 AbsolutePositionOffset => ToFloat3(AbsolutePositionOffsetDouble);

        public float3 ProjectRuntimePositionToLocal(Vector3 capturedRuntimePosition, double3 capturedTotalOffset)
        {
            Vector3 rebasedRuntimePosition = StableShift.RebaseCapturedRuntimePosition(capturedRuntimePosition, capturedTotalOffset);
            return (float3)(rebasedRuntimePosition - RootRuntimePosition);
        }
    }

    static Bounds CalculatePositionBounds(NativeArray<float3> positions, int count, out bool invalidMeshData)
    {
        invalidMeshData = false;
        if (count <= 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        bool foundFinitePosition = false;
        float3 min = default;
        float3 max = default;
        for (int i = 0; i < count; i++)
        {
            float3 position = positions[i];
            if (!IsFiniteFloat3(position))
            {
                invalidMeshData = true;
                continue;
            }

            if (!foundFinitePosition)
            {
                min = position;
                max = position;
                foundFinitePosition = true;
                continue;
            }

            min = math.min(min, position);
            max = math.max(max, position);
        }

        if (!foundFinitePosition)
            return new Bounds(Vector3.zero, Vector3.one * 0.01f);

        float3 center = (min + max) * 0.5f;
        float3 size = math.max(max - min, new float3(0.01f));
        return new Bounds(center, size);
    }

    static float ResolveChunkBorderStitchWeight(float3 localPosition, Bounds bounds)
    {
        float3 min = (float3)bounds.min;
        float3 max = (float3)bounds.max;
        float3 size = math.max((float3)bounds.size, new float3(0.0001f));
        float3 edgeDistance = math.max(new float3(0f), math.min(localPosition - min, max - localPosition));
        float nearestEdgeDistance = math.cmin(edgeDistance);
        float stitchWidth = math.max(math.cmin(size) * 0.0625f, 0.25f);
        return math.saturate(1f - nearestEdgeDistance / stitchWidth);
    }

    static bool OffsetsApproximatelyMatch(double3 lhs, double3 rhs)
    {
        return math.lengthsq(lhs - rhs) <= 0.000001d;
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
                projectionState.ShiftEpochChanged != 0 ||
                !OffsetsApproximatelyMatch(data.AbsoluteUniverseOffsetAtStartDouble, projectionState.StableShift.NewTotalOffsetDouble) ||
                projectionState.RootRuntimePosition.sqrMagnitude > 0.000001f;

            if (!needsProjection)
                return default;

            if (!TryEnsureProjectionScratchCapacity(ref data.ScratchLease, data.WeldedCount))
                return default;

            NativeArray<float3> projectedPositions = data.ScratchLease.ProjectedLocalPositions;
            double3 rebaseDeltaDouble = data.AbsoluteUniverseOffsetAtStartDouble - projectionState.StableShift.NewTotalOffsetDouble;
            JobHandle projectionHandle = new VoxelShiftAwareProjectionJob
            {
                rebaseDelta = ToFloat3(rebaseDeltaDouble),
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
        bool invalidMeshData = false;
        Bounds bounds = CalculatePositionBounds(positions, vertexCount, out invalidMeshData);
        float3 fallbackPosition = (float3)bounds.center;
        meshData.SetVertexBufferParams(
            vertexCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4));

        meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

        NativeArray<VoxelSurfaceVertex> vertexData = meshData.GetVertexData<VoxelSurfaceVertex>();
        for (int i = 0; i < vertexCount; i++)
        {
            float3 localPosition = positions[i];
            if (!IsFiniteFloat3(localPosition))
            {
                invalidMeshData = true;
                localPosition = fallbackPosition;
            }

            float3 normal = normals.IsCreated && i < normals.Length
                ? NormalizeFiniteOrUp(normals[i], ref invalidMeshData)
                : new float3(0f, 1f, 0f);
            float3 absolutePosition = localPosition + absolutePositionOffset;
            if (!IsFiniteFloat3(absolutePosition))
            {
                invalidMeshData = true;
                absolutePosition = localPosition;
            }

            float chunkBorderStitch = ResolveChunkBorderStitchWeight(localPosition, bounds);
            vertexData[i] = new VoxelSurfaceVertex
            {
                Position = localPosition,
                Normal = normal,
                Color = (Color32)(colors.IsCreated && i < colors.Length ? SanitizeFiniteColor(colors[i], ref invalidMeshData) : Color.white),
                BakedOcclusionUv1 = new Vector4(
                    0f,
                    0f,
                    0f,
                    ambientOcclusionValues.IsCreated && i < ambientOcclusionValues.Length
                        ? SanitizeFinite01(ambientOcclusionValues[i], 1f, ref invalidMeshData)
                        : 1f),
                // UV2.w gates shader-only seam stitching; UV3.w carries the shared AUP border height.
                DirtyBlendUv2 = new Vector4(
                    dirtyBlendValues.IsCreated && i < dirtyBlendValues.Length ? SanitizeFinite01(dirtyBlendValues[i], 0f, ref invalidMeshData) : 0f,
                    skirtAlphaValues.IsCreated && i < skirtAlphaValues.Length ? SanitizeFinite01(skirtAlphaValues[i], 0f, ref invalidMeshData) : 0f,
                    curvatureValues.IsCreated && i < curvatureValues.Length ? SanitizeFinite01(curvatureValues[i], 0.5f, ref invalidMeshData) : 0.5f,
                    chunkBorderStitch),
                AbsolutePositionWS = new Vector4(absolutePosition.x, absolutePosition.y, absolutePosition.z, absolutePosition.y)
            };
        }

        NativeArray<uint> indexData = meshData.GetIndexData<uint>();
        for (int i = 0; i < triangleIndexCount; i++)
        {
            int triangleIndex = triangleIndices[i];
            if ((uint)triangleIndex >= (uint)vertexCount)
            {
                invalidMeshData = true;
                triangleIndex = 0;
            }

            indexData[i] = (uint)triangleIndex;
        }

        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount, MeshTopology.Triangles)
        {
            bounds = bounds,
            vertexCount = vertexCount
        }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        mesh.bounds = bounds;
        if (invalidMeshData)
            WriteVoxelMeshPipelineBlackBoxSample(
                unchecked((uint)Time.frameCount),
                VoxelMeshPipelineInvalidMeshDataFlag,
                _voxelChunksMeshedThisFrame,
                DeferredVoxelPhysicsBakePendingCount,
                _deferredVoxelColliderUploads.Count);
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
        bool invalidMeshData = false;
        Bounds bounds = CalculatePositionBounds(positions, vertexCount, out invalidMeshData);
        float3 fallbackPosition = (float3)bounds.center;
        for (int i = 0; i < vertexCount; i++)
        {
            float3 position = positions[i];
            if (!IsFiniteFloat3(position))
            {
                invalidMeshData = true;
                position = fallbackPosition;
            }

            vertexData[i] = new VoxelColliderVertex { Position = position };
        }

        NativeArray<uint> indexData = meshData.GetIndexData<uint>();
        for (int i = 0; i < triangleIndexCount; i++)
        {
            int triangleIndex = triangleIndices[i];
            if ((uint)triangleIndex >= (uint)vertexCount)
            {
                invalidMeshData = true;
                triangleIndex = 0;
            }

            indexData[i] = (uint)triangleIndex;
        }
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount, MeshTopology.Triangles)
        {
            bounds = bounds,
            vertexCount = vertexCount
        }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        mesh.bounds = bounds;
        if (invalidMeshData)
            WriteVoxelMeshPipelineBlackBoxSample(
                unchecked((uint)Time.frameCount),
                VoxelMeshPipelineInvalidMeshDataFlag,
                _voxelChunksMeshedThisFrame,
                DeferredVoxelPhysicsBakePendingCount,
                _deferredVoxelColliderUploads.Count);
    }

    GameObject SpawnVolume()
    {
        IObjectPoolService pool = GlobalRegistry.ObjectPoolService;
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

        if (Application.isPlaying)
        {
            ReportVoxelVolumeSpawnPoolMiss();
            return null;
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
                                Material mat,
                                Mesh reservedSurfaceMesh = null,
                                HectonVoxelVolume volume = null)
    {
        MeshFilter mf = volume != null ? volume.CachedMeshFilter : null;
        if (mf == null)
            go.TryGetComponent(out mf);
        if (mf == null)
        {
            if (volume != null)
                return null;

            mf = go.AddComponent<MeshFilter>();
        }

        MeshRenderer mr = volume != null ? volume.CachedMeshRenderer : null;
        if (mr == null)
            go.TryGetComponent(out mr);
        if (mr == null)
        {
            if (volume != null)
                return null;

            mr = go.AddComponent<MeshRenderer>();
        }

        Mesh mesh = mf.sharedMesh;
        bool attachAcquiredMesh = false;
        if (mesh == null)
        {
            mesh = reservedSurfaceMesh != null ? reservedSurfaceMesh : AcquireVoxelSurfaceMesh();
            if (mesh == null)
                return null;

            attachAcquiredMesh = true;
        }
        else
        {
            mesh.Clear();
        }

        UploadSurfaceMesh(mesh, positions, normals, colors, ambientOcclusionValues, curvatureValues, skirtAlphaValues, dirtyBlendValues, triangleIndices, vertCount, triIndexCount, absolutePositionOffset);
        if (attachAcquiredMesh)
            mf.sharedMesh = mesh;

        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.enabled = true;
        return mesh;
    }

    private static void ReleaseOrDestroySurfaceMesh(MeshFilter meshFilter, bool destroyIfUnpooled)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        Mesh mesh = meshFilter.sharedMesh;
        meshFilter.sharedMesh = null;
        if (ReleaseVoxelSurfaceMesh(mesh))
            return;

        mesh.Clear(false);
        if (destroyIfUnpooled)
            DestroyDeferredVoxelObject(mesh);
    }

    Awaitable<bool> ApplyVolumeMeshAsync(GameObject go, VoxelPipelineData data, OriginShiftEventData stableShift, CancellationToken ct)
    {
        return ApplyVolumeMeshInternalAsync();

        async Awaitable<bool> ApplyVolumeMeshInternalAsync()
        {
            NativeArray<float3> projectedLocalPositions = default;
            Mesh reservedSurfaceMesh = null;
            try
            {
                Vector3 rootRuntimePosition = stableShift.RebaseCapturedRuntimePosition(Vector3.zero, data.AbsoluteUniverseOffsetAtStartDouble);
                VoxelFinalizeProjectionState projectionState = new VoxelFinalizeProjectionState(
                    stableShift,
                    rootRuntimePosition,
                    data.ShiftEpochAtStart != stableShift.Sequence);

                projectedLocalPositions = await BuildShiftAwareLocalPositionBufferAsync(data, projectionState, ct);
                NativeArray<float3> meshLocalPositions = projectedLocalPositions.IsCreated ? projectedLocalPositions : data.WeldedPositions;
                float3 localVolumeOrigin = projectionState.ProjectRuntimePositionToLocal((Vector3)data.VolumeOrigin, data.AbsoluteUniverseOffsetAtStartDouble);
                HectonVoxelVolume volume = data.SourceVolume;

                if (NeedsVoxelSurfaceMeshAcquire(go, volume) &&
                    (reservedSurfaceMesh = await AcquireVoxelSurfaceMeshAsync(ct)) == null)
                {
                    return false;
                }

                await AwaitVoxelMeshUploadBudgetAsync(ct);
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
                    voxelMaterial,
                    reservedSurfaceMesh,
                    volume);
                if (ReferenceEquals(mesh, reservedSurfaceMesh))
                    reservedSurfaceMesh = null;

                if (mesh == null)
                    return false;
                RecordVoxelChunkMeshed();
                await AwaitableDebtMonitor.NextFrameAsync(ct);

                bool buildCollider = data.BuildCollider && !ShouldUseCinematicColliderFake(in data);
                MeshCollider mcol = volume != null ? volume.CachedRootMeshCollider : null;
                if (mcol == null)
                    go.TryGetComponent(out mcol);

                if (!buildCollider)
                {
                    if (volume != null)
                        volume.DisableColliderChunksForCinematicFake();

                    if (mcol != null)
                    {
                        mcol.enabled = false;
                    }

                    go.TryGetComponent(out BoxCollider rootBakeProxy);
                    DisableVoxelBakeProxy(rootBakeProxy);
                    Transform isolatedProxy = go.transform.Find(VoxelBakeProxyRuntimeName);
                    if (isolatedProxy != null && isolatedProxy.TryGetComponent(out BoxCollider isolatedProxyCollider))
                        DisableVoxelBakeProxy(isolatedProxyCollider);
                    return true;
                }

                if (mcol == null)
                {
                    if (volume != null)
                    {
                        volume.DisableColliderChunksForCinematicFake();
                        return true;
                    }

                    mcol = go.AddComponent<MeshCollider>();
                }

                if (volume != null && TryResolveSelectedChthonicPillarRecord(in data, out _))
                {
                    mcol.enabled = false;
                    return await ApplySmoothChthonicPillarColliderMeshAsync(volume, data, projectionState, ct);
                }

                if (volume == null)
                {
                    BoxCollider fallbackBakeProxy = EnsureVoxelBakeProxyCollider(go);
                    go.TryGetComponent(out MeshRenderer fallbackRenderer);
                    bool deferredFallbackColliderUpload = false;
                    bool deferredFallbackBakeTeardown = false;
                    ConfigureVoxelBakeBaseProxy(
                        fallbackBakeProxy,
                        localVolumeOrigin,
                        new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep,
                        data.VoxelStep);

                    try
                    {
                        VoxelMeshBakeJob fallbackBakeJob = new VoxelMeshBakeJob
                        {
                            MeshId = mesh.GetEntityId()
                        };

                        long fallbackBakeScheduleTimestamp = Stopwatch.GetTimestamp();
                        if (!TryScheduleVoxelPhysicsBake(in fallbackBakeJob, out JobHandle fallbackBakeHandle))
                        {
                            await AwaitableDebtMonitor.NextFrameAsync(ct);
                            fallbackBakeScheduleTimestamp = Stopwatch.GetTimestamp();
                            if (!TryScheduleVoxelPhysicsBake(in fallbackBakeJob, out fallbackBakeHandle))
                            {
                                deferredFallbackBakeTeardown = true;
                                return false;
                            }
                        }

                        if (!await AwaitForPhysicsBakeCompletionOrDeferAsync(
                                fallbackBakeHandle,
                                ct,
                                "fallback collider bake",
                                mesh,
                                go,
                                fallbackRenderer,
                                mcol,
                                DeferredVoxelBakeDestroyOwner,
                                fallbackBakeProxy))
                        {
                            deferredFallbackBakeTeardown = true;
                            return false;
                        }

                        ReportVoxelPhysicsBakeCompletion(fallbackBakeScheduleTimestamp);
                        ct.ThrowIfCancellationRequested();
                        mcol.enabled = false;
                        deferredFallbackColliderUpload = EnqueueDeferredVoxelColliderUpload(mcol, mesh, fallbackBakeProxy);
                        if (!deferredFallbackColliderUpload)
                            return false;
                        return true;
                    }
                    finally
                    {
                        if (!deferredFallbackColliderUpload && !deferredFallbackBakeTeardown)
                            DisableVoxelBakeProxy(fallbackBakeProxy);
                    }
                }

                mcol.enabled = false;
                return await ApplyChunkedColliderMeshesAsync(volume, data, meshLocalPositions, localVolumeOrigin, ct);
            }
            finally
            {
                if (reservedSurfaceMesh != null)
                    ReleaseVoxelSurfaceMesh(reservedSurfaceMesh);
            }
        }
    }

    static bool TryResolveSelectedChthonicPillarRecord(in VoxelPipelineData data, out AnomalyFeatureRecord record)
    {
        record = default;
        NativeArray<AnomalyFeatureRecord> selected = data.ScratchLease.SelectedPillarFeature;
        if (!selected.IsCreated || selected.Length <= 0)
            return false;

        record = selected[0];
        return record.Valid != 0 && record.Kind == (byte)AnomalyFeatureKind.ChthonicPillar;
    }

    async Awaitable<bool> ApplySmoothChthonicPillarColliderMeshAsync(
        HectonVoxelVolume volume,
        VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState,
        CancellationToken ct)
    {
        if (volume == null)
            return false;

        if (!volume.TryUsePrewarmedColliderChunkCapacity(1))
            return false;

        MeshCollider chunkCollider = volume.GetColliderChunkCollider(0);
        if (chunkCollider == null)
            return false;

        Mesh chunkMesh = volume.GetColliderChunkBakeMesh(0);
        if (chunkMesh == null)
        {
            chunkMesh = await AcquireVoxelPhysicsBakeMeshAsync(ct);
            if (chunkMesh == null)
                return false;

            if (!volume.AssignColliderChunkBakeMesh(0, chunkMesh))
            {
                ReleaseVoxelPhysicsBakeMesh(chunkMesh);
                chunkMesh = volume.GetColliderChunkBakeMesh(0);
                if (chunkMesh == null)
                    return false;
            }
        }

        NativeArray<float3> colliderPositions = default;
        NativeArray<int> colliderIndices = default;
        try
        {
            if (!TryBuildSmoothChthonicPillarColliderMesh(
                    in data,
                    projectionState,
                    ref colliderPositions,
                    ref colliderIndices,
                    out int vertexCount,
                    out int indexCount))
            {
                volume.DisableColliderChunksForCinematicFake();
                return true;
            }

            chunkCollider.gameObject.SetActive(true);
            chunkMesh.Clear();
            await AwaitVoxelMeshUploadBudgetAsync(ct);
            UploadColliderMesh(chunkMesh, colliderPositions, colliderIndices, vertexCount, indexCount);
            await AwaitableDebtMonitor.NextFrameAsync(ct);

            VoxelMeshBakeJob bakeJob = new VoxelMeshBakeJob
            {
                MeshId = chunkMesh.GetEntityId()
            };

            long bakeScheduleTimestamp = Stopwatch.GetTimestamp();
            if (!TryScheduleVoxelPhysicsBake(in bakeJob, out JobHandle bakeHandle))
            {
                await AwaitableDebtMonitor.NextFrameAsync(ct);
                bakeScheduleTimestamp = Stopwatch.GetTimestamp();
                if (!TryScheduleVoxelPhysicsBake(in bakeJob, out bakeHandle))
                {
                    volume.ReleaseColliderChunkBakeMesh(0);
                    return false;
                }
            }

            if (!await AwaitForPhysicsBakeCompletionOrDeferAsync(
                    bakeHandle,
                    ct,
                    "smooth chthonic pillar collider bake",
                    chunkMesh,
                    volume.gameObject,
                    null,
                    chunkCollider,
                    0))
            {
                volume.DetachColliderChunkBakeMesh(0);
                return false;
            }

            ReportVoxelPhysicsBakeCompletion(bakeScheduleTimestamp);
            ct.ThrowIfCancellationRequested();
            if (!volume.PublishColliderChunkMesh(0))
                return false;

            volume.SetActiveColliderChunkCount(1);
            return true;
        }
        finally
        {
            colliderPositions = default;
            colliderIndices = default;
        }
    }

    bool TryBuildSmoothChthonicPillarColliderMesh(
        in VoxelPipelineData data,
        VoxelFinalizeProjectionState projectionState,
        ref NativeArray<float3> positions,
        ref NativeArray<int> indices,
        out int vertexCount,
        out int indexCount)
    {
        vertexCount = 0;
        indexCount = 0;
        if (!TryResolveSelectedChthonicPillarRecord(in data, out AnomalyFeatureRecord record))
            return false;

        double3 baseAup = new double3(record.AupX, record.AupY, record.AupZ);
        double3 chunkMinAup = ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;
        double3 chunkMaxAup = chunkMinAup + new double3(
            math.max(1, data.PtsX) - 1,
            math.max(1, data.PtsY) - 1,
            math.max(1, data.PtsZ) - 1) * math.max(0.001f, data.VoxelStep);
        double radius = ChthonicPillarRadiusMeters;
        double pillarMinY = baseAup.y;
        double pillarMaxY = baseAup.y + ChthonicPillarHeightMeters;
        if (baseAup.x + radius < chunkMinAup.x ||
            baseAup.x - radius > chunkMaxAup.x ||
            baseAup.z + radius < chunkMinAup.z ||
            baseAup.z - radius > chunkMaxAup.z ||
            pillarMaxY < chunkMinAup.y ||
            pillarMinY > chunkMaxAup.y)
        {
            return false;
        }

        double bottom = math.max(pillarMinY, chunkMinAup.y);
        double top = math.min(pillarMaxY, chunkMaxAup.y);
        if (top - bottom <= 0.01d)
            return false;

        int segments = ChthonicPillarColliderSegments;
        vertexCount = segments * 2 + 2;
        indexCount = segments * 12;
        if (!TryEnsureColliderChunkScratchCapacity(
                ref data.ScratchLease,
                1,
                indexCount,
                vertexCount,
                1))
        {
            vertexCount = 0;
            indexCount = 0;
            return false;
        }

        positions = data.ScratchLease.ColliderLocalPositions;
        indices = data.ScratchLease.ColliderLocalIndices;

        double3 localOffset = projectionState.AbsolutePositionOffsetDouble;
        float localBottomY = (float)(bottom - localOffset.y);
        float localTopY = (float)(top - localOffset.y);
        float localCenterX = (float)(baseAup.x - localOffset.x);
        float localCenterZ = (float)(baseAup.z - localOffset.z);
        float safeRadius = ChthonicPillarRadiusMeters;

        for (int segment = 0; segment < segments; segment++)
        {
            float2 unit = _chthonicPillarColliderUnitCircle[segment];
            float x = localCenterX + unit.x * safeRadius;
            float z = localCenterZ + unit.y * safeRadius;
            int vertexBase = segment * 2;
            positions[vertexBase] = new float3(x, localBottomY, z);
            positions[vertexBase + 1] = new float3(x, localTopY, z);
        }

        int bottomCenter = segments * 2;
        int topCenter = bottomCenter + 1;
        positions[bottomCenter] = new float3(localCenterX, localBottomY, localCenterZ);
        positions[topCenter] = new float3(localCenterX, localTopY, localCenterZ);

        int write = 0;
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            int bottomA = segment * 2;
            int topA = bottomA + 1;
            int bottomB = next * 2;
            int topB = bottomB + 1;

            indices[write++] = bottomA;
            indices[write++] = topA;
            indices[write++] = bottomB;
            indices[write++] = bottomB;
            indices[write++] = topA;
            indices[write++] = topB;

            indices[write++] = bottomCenter;
            indices[write++] = bottomB;
            indices[write++] = bottomA;

            indices[write++] = topCenter;
            indices[write++] = topA;
            indices[write++] = topB;
        }

        return true;
    }

    static int ResolveColliderChunkCount(int triangleCount)
    {
        if (triangleCount >= 40000)
            return 8;

        if (triangleCount >= 10000)
            return 4;

        return 2;
    }

    static BoxCollider EnsureVoxelBakeProxyCollider(GameObject owner)
    {
        if (owner == null)
            return null;

        EnsureVoxelProxyLayerFiltering();
        Transform proxyTransform = owner.transform.Find(VoxelBakeProxyRuntimeName);
        if (proxyTransform == null)
        {
            GameObject proxyObject = new GameObject(VoxelBakeProxyRuntimeName); // COLD ALLOC: GameObject[1] - isolated fallback async bake proxy collider - owner: HectonVoxelEngine
            proxyObject.layer = HectonLayerMasks.VoxelProxy;
            proxyTransform = proxyObject.transform;
            proxyTransform.SetParent(owner.transform, false);
            proxyTransform.localPosition = Vector3.zero;
            proxyTransform.localRotation = Quaternion.identity;
            proxyTransform.localScale = Vector3.one;
        }

        proxyTransform.gameObject.layer = HectonLayerMasks.VoxelProxy;
        if (!proxyTransform.TryGetComponent(out BoxCollider proxy))
            proxy = proxyTransform.gameObject.AddComponent<BoxCollider>();

        proxy.isTrigger = false;
        return proxy;
    }

    static void ConfigureVoxelBakeBaseProxy(
        BoxCollider proxy,
        float3 boundsMin,
        float3 boundsSize,
        float voxelStep)
    {
        if (proxy == null)
            return;

        float proxyHeight = math.max(VoxelPhysicsBakeProxyMinHeightMeters, voxelStep * 2f);
        float3 safeSize = math.max(boundsSize, new float3(0.01f));
        proxy.center = new Vector3(
            boundsMin.x + safeSize.x * 0.5f,
            boundsMin.y + proxyHeight * 0.5f,
            boundsMin.z + safeSize.z * 0.5f);
        proxy.size = new Vector3(safeSize.x, proxyHeight, safeSize.z);
        proxy.enabled = true;
    }

    static void DisableVoxelBakeProxy(BoxCollider proxy)
    {
        if (proxy != null)
            proxy.enabled = false;
    }

    static void ResolveVoxelColliderChunkBakeProxyBounds(
        int chunkIndex,
        int colliderChunkCount,
        float3 boundsMin,
        float3 boundsSize,
        float voxelStep,
        out Vector3 center,
        out Vector3 size)
    {
        float3 safeBoundsSize = math.max(boundsSize, new float3(0.01f));
        bool splitY = colliderChunkCount > 4;
        int x = chunkIndex & 1;
        int z = (chunkIndex >> 1) & 1;
        int y = splitY ? (chunkIndex >> 2) & 1 : 0;
        float3 chunkSize = new float3(
            safeBoundsSize.x * 0.5f,
            splitY ? safeBoundsSize.y * 0.5f : safeBoundsSize.y,
            safeBoundsSize.z * 0.5f);
        float3 chunkMin = boundsMin + new float3(chunkSize.x * x, chunkSize.y * y, chunkSize.z * z);
        float proxyHeight = math.min(chunkSize.y, math.max(VoxelPhysicsBakeProxyMinHeightMeters, voxelStep * 2f));
        float3 proxySize = new float3(chunkSize.x, proxyHeight, chunkSize.z);
        float3 proxyCenter = new float3(
            chunkMin.x + proxySize.x * 0.5f,
            chunkMin.y + proxySize.y * 0.5f,
            chunkMin.z + proxySize.z * 0.5f);

        center = new Vector3(proxyCenter.x, proxyCenter.y, proxyCenter.z);
        size = new Vector3(proxySize.x, proxySize.y, proxySize.z);
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
            volume.DisableColliderChunksForCinematicFake();
            return true;
        }

        int colliderChunkCount = ResolveColliderChunkCount(triangleCount);
        if (!volume.TryUsePrewarmedColliderChunkCapacity(colliderChunkCount))
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        if (!TryEnsureColliderChunkScratchCapacity(
                ref data.ScratchLease,
                triangleCount,
                triangleIndexCount,
                data.WeldedCount,
                colliderChunkCount))
        {
            volume.DisableColliderChunksForCinematicFake();
            return false;
        }

        NativeArray<byte> triangleBuckets = data.ScratchLease.ColliderTriangleBuckets;
        NativeArray<int> bucketCounts = data.ScratchLease.ColliderBucketCounts;
        NativeArray<int> bucketOffsets = data.ScratchLease.ColliderBucketOffsets;
        NativeArray<int> bucketWriteHeads = data.ScratchLease.ColliderBucketWriteHeads;
        NativeArray<int> chunkTriangleIndices = data.ScratchLease.ColliderChunkTriangleIndices;
        NativeArray<int> localRemap = data.ScratchLease.ColliderLocalRemap;
        NativeArray<int> touchedVertexGlobals = data.ScratchLease.ColliderTouchedVertexGlobals;
        NativeArray<float3> localPositions = data.ScratchLease.ColliderLocalPositions;
        NativeArray<int> localIndices = data.ScratchLease.ColliderLocalIndices;
        bool completed = false;
        bool deferredBakeTeardown = false;
        bool deferredColliderUploadQueued = false;

        try
        {
            long chunkGenerationFrameStart = Stopwatch.GetTimestamp();

            float3 boundsMin = localVolumeOrigin;
            float3 boundsSize = new float3(data.GridDimension, data.GridDimension, data.GridDimension) * data.VoxelStep;

            for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
            {
                bucketCounts[chunkIndex] = 0;
                bucketOffsets[chunkIndex] = 0;
                bucketWriteHeads[chunkIndex] = 0;
            }

            JobHandle clearRemapHandle = new VoxelFillIntArrayJob
            {
                Value = -1,
                Values = localRemap
            }.Schedule(data.WeldedCount, JOB_BATCH);

            JobHandle classifyHandle = new VoxelColliderChunkClassifyJob
            {
                positions = meshLocalPositions,
                triangleIndices = data.TriangleIndices,
                boundsMin = boundsMin,
                boundsSize = boundsSize,
                chunkCount = colliderChunkCount,
                triangleBuckets = triangleBuckets
            }.Schedule(triangleCount, 64, clearRemapHandle);

            await AwaitForJobCompletionAsync(classifyHandle, ct, "collider chunk classify");

            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                bucketCounts[triangleBuckets[triangleIndex]] += 3;
                if ((triangleIndex & 1023) == 1023)
                    chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            }

            int runningOffset = 0;
            for (int chunkIndex = 0; chunkIndex < colliderChunkCount; chunkIndex++)
            {
                bucketOffsets[chunkIndex] = runningOffset;
                bucketWriteHeads[chunkIndex] = runningOffset;
                runningOffset += bucketCounts[chunkIndex];
            }
            chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);

            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int bucket = triangleBuckets[triangleIndex];
                int writeHead = bucketWriteHeads[bucket];
                int triBase = triangleIndex * 3;
                chunkTriangleIndices[writeHead] = data.TriangleIndices[triBase];
                chunkTriangleIndices[writeHead + 1] = data.TriangleIndices[triBase + 1];
                chunkTriangleIndices[writeHead + 2] = data.TriangleIndices[triBase + 2];
                bucketWriteHeads[bucket] = writeHead + 3;
                if ((triangleIndex & 1023) == 1023)
                    chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
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
                    chunkCollider.enabled = false;
                    volume.DisableColliderChunkBakeProxy(chunkIndex);
                    chunkCollider.gameObject.SetActive(false);
                    continue;
                }

                Mesh chunkMesh = volume.GetColliderChunkBakeMesh(chunkIndex);
                if (chunkMesh == null)
                {
                    chunkMesh = await AcquireVoxelPhysicsBakeMeshAsync(ct);
                    if (chunkMesh != null && !volume.AssignColliderChunkBakeMesh(chunkIndex, chunkMesh))
                    {
                        ReleaseVoxelPhysicsBakeMesh(chunkMesh);
                        chunkMesh = volume.GetColliderChunkBakeMesh(chunkIndex);
                    }
                }

                if (chunkMesh == null)
                {
                    chunkCollider.enabled = false;
                    chunkCollider.gameObject.SetActive(false);
                    return false;
                }

                chunkCollider.gameObject.SetActive(true);
                ResolveVoxelColliderChunkBakeProxyBounds(
                    chunkIndex,
                    colliderChunkCount,
                    boundsMin,
                    boundsSize,
                    data.VoxelStep,
                    out Vector3 proxyCenter,
                    out Vector3 proxySize);
                volume.ConfigureColliderChunkBakeProxy(chunkIndex, proxyCenter, proxySize);
                BoxCollider chunkBakeProxy = volume.GetColliderChunkBakeProxy(chunkIndex);
                await AwaitableDebtMonitor.NextFrameAsync(ct);
                chunkMesh.Clear();
                int localVertexCount = 0;
                int touchedVertexCount = 0;

                try
                {
                    for (int localIndex = 0; localIndex < chunkIndexCount; localIndex++)
                    {
                        int globalIndex = chunkTriangleIndices[bucketOffsets[chunkIndex] + localIndex];
                        int remappedIndex = localRemap[globalIndex];
                        if (remappedIndex < 0)
                        {
                            remappedIndex = localVertexCount;
                            localRemap[globalIndex] = remappedIndex;
                            touchedVertexGlobals[touchedVertexCount++] = globalIndex;
                            localPositions[localVertexCount++] = meshLocalPositions[globalIndex];
                        }

                        localIndices[localIndex] = remappedIndex;
                    }

                    await AwaitVoxelMeshUploadBudgetAsync(ct);
                    UploadColliderMesh(chunkMesh, localPositions, localIndices, localVertexCount, chunkIndexCount);
                }
                finally
                {
                    for (int touchedIndex = 0; touchedIndex < touchedVertexCount; touchedIndex++)
                        localRemap[touchedVertexGlobals[touchedIndex]] = -1;
                }
                chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
                await AwaitableDebtMonitor.NextFrameAsync(ct);

                VoxelMeshBakeJob bakeJob = new VoxelMeshBakeJob
                {
                    MeshId = chunkMesh.GetEntityId()
                };

                long bakeScheduleTimestamp = Stopwatch.GetTimestamp();
                if (!TryScheduleVoxelPhysicsBake(in bakeJob, out JobHandle bakeHandle))
                {
                    await AwaitableDebtMonitor.NextFrameAsync(ct);
                    bakeScheduleTimestamp = Stopwatch.GetTimestamp();
                    if (!TryScheduleVoxelPhysicsBake(in bakeJob, out bakeHandle))
                    {
                        volume.ReleaseColliderChunkBakeMesh(chunkIndex);
                        deferredBakeTeardown = true;
                        return false;
                    }
                }

                if (!await AwaitForPhysicsBakeCompletionOrDeferAsync(
                        bakeHandle,
                        ct,
                        "collider chunk bake",
                        chunkMesh,
                        volume.gameObject,
                        null,
                        chunkCollider,
                        0,
                        chunkBakeProxy))
                {
                    volume.DetachColliderChunkBakeMesh(chunkIndex);
                    deferredBakeTeardown = true;
                    return false;
                }

                ReportVoxelPhysicsBakeCompletion(bakeScheduleTimestamp);
                ct.ThrowIfCancellationRequested();
                if (!volume.PublishColliderChunkMesh(chunkIndex))
                    return false;

                deferredColliderUploadQueued = true;

                chunkGenerationFrameStart = await YieldIfChunkGenerationBudgetExpiredAsync(chunkGenerationFrameStart, ct);
            }

            volume.SetActiveColliderChunkCount(colliderChunkCount);
            completed = true;
            return true;
        }
        finally
        {
            if (!completed && !deferredBakeTeardown)
                volume.DisableColliderChunkBakeProxies();

            if (!completed && !deferredBakeTeardown && !deferredColliderUploadQueued)
                volume.ClearColliderChunkBakeMeshes();
        }
    }

    void PrepareVolumeForBuild(GameObject go)
    {
        if (go == null)
            return;

        go.TryGetComponent(out HectonVoxelVolume volume);
        if (volume != null)
            volume.PrepareForReuse();

        MeshRenderer mr = volume != null ? volume.CachedMeshRenderer : null;
        if (mr == null)
            go.TryGetComponent(out mr);
        if (mr != null)
            mr.enabled = false;

        MeshCollider mcol = volume != null ? volume.CachedRootMeshCollider : null;
        if (mcol == null)
            go.TryGetComponent(out mcol);
        if (mcol != null)
        {
            mcol.enabled = false;
        }

        go.TryGetComponent(out BoxCollider bakeProxy);
        if (bakeProxy != null)
            bakeProxy.enabled = false;

        Transform bakeProxyTransform = go.transform.Find(VoxelBakeProxyRuntimeName);
        if (bakeProxyTransform != null && bakeProxyTransform.TryGetComponent(out BoxCollider isolatedProxy))
            isolatedProxy.enabled = false;
    }

    static bool TryBindGeneratedVolumeForMeshPublication(GameObject go, VoxelPipelineData data)
    {
        if (go == null || data == null)
            return false;

        if (!go.TryGetComponent(out HectonVoxelVolume volume) || volume == null)
            return false;

        data.SourceVolume = volume;
        data.SourceRuntimeStamp = volume.RuntimeStamp;
        return true;
    }

    async Awaitable<bool> ConfigureVolumeRuntimeDataAsync(
        GameObject go,
        uint seed,
        Vector3 worldCenter,
        Vector3 absoluteUniverseOffset,
        double3 absoluteUniverseOffsetDouble,
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
        bool buildCollider,
        CancellationToken ct)
    {
        if (go == null)
            return false;

        if (!go.TryGetComponent(out HectonVoxelVolume volume))
            return false;

        volume.ConfigureRuntimeData(
            this,
            seed,
            worldCenter,
            absoluteUniverseOffset,
            absoluteUniverseOffsetDouble,
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

        return await volume.PublishSonarSdfSnapshotAsync(
            new Vector3Int(ptsX, ptsY, ptsZ),
            volumeOrigin,
            Vector3.one * voxelStep,
            smoothDensityField,
            ct);
    }

    void RegisterEntranceTerrainHoles(
        GameObject go,
        NativeArray<CaveEntrance> entrances,
        float voxelSize,
        double3 capturedTotalOffset,
        double3 committedTotalOffset)
    {
        HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        if (vegetationBridge == null || go == null || !entrances.IsCreated || entrances.Length <= 0)
            return;

        if (!go.TryGetComponent(out HectonVoxelVolume volume))
            return;

        float holePadding = math.max(voxelSize * 1.5f, 1f);
        for (int i = 0; i < entrances.Length; i++)
        {
            CaveEntrance entrance = entrances[i];
            float radius = math.max(entrance.radius, entrance.innerRadius) + holePadding;
            Vector3 runtimeSurfacePosition = ToVector3(ToDouble3(entrance.surfacePosition) + capturedTotalOffset - committedTotalOffset);
            int holeHandle = vegetationBridge.RegisterTerrainHoleHandle(runtimeSurfacePosition, radius);
            volume.TrackTerrainHoleHandle(holeHandle);
        }
    }

    void RegisterPipelineSpawnPoints(
        Vector3 worldCenter,
        SpawnContext caveContext,
        NativeList<CaveSpawnData> spawnPointList,
        double3 capturedTotalOffset,
        double3 committedTotalOffset)
    {
        ScavengePopulator scavengePopulator = null;
        if (!spawnPointList.IsCreated || spawnPointList.Length <= 0 || !WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref scavengePopulator))
            return;

        double3 absoluteUniverseCenter = ToDouble3(worldCenter) + capturedTotalOffset;
        float tileSize = mapMagicTileSize > 0f ? mapMagicTileSize : 999f;
        Vector2Int chunkCoord = new Vector2Int(
            (int)math.floor(absoluteUniverseCenter.x / tileSize),
            (int)math.floor(absoluteUniverseCenter.z / tileSize));

        for (int sp = 0; sp < spawnPointList.Length; sp++)
        {
            CaveSpawnData spawnData = spawnPointList[sp];
            Vector3 runtimeSpawnPosition = ToVector3(ToDouble3(spawnData.position) + capturedTotalOffset - committedTotalOffset);
            scavengePopulator.RegisterSpawnPoint(
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
