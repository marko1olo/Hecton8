// =====================================================================
// Extracted from HectonVoxelEngine.cs — MCTables only (no logic change)
// 2026-08-03 architecture Slice A step-2
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

#region Marching Cubes Tables

public static class MCTables
{
    public static bool IsReady => Volatile.Read(ref _ready) == 1;

    const BufferID EdgeTableBufferId = BufferID.VoxelMarchingCubesEdgeTable;
    const BufferID TriTableBufferId = BufferID.VoxelMarchingCubesTriTable;
    const SystemID TableOwnerSystemId = SystemID.TerrainSeams;
    const int EdgeTableLength = 256;
    const int TriTableLength = 4096;
    static readonly ulong JobTableMutationGuardMask =
        TableMutationGuardBit(EdgeTableBufferId) |
        TableMutationGuardBit(TriTableBufferId);

    static IDataVault _vault;
    static VaultGenerationHandle<int> _edgeTableHandle;
    static VaultGenerationHandle<int> _triTableHandle;
    static int _ready;
    static int _initGate;
    static bool _editorHooksInstalled;

    public readonly struct JobTableLease : IDisposable
    {
        readonly IDataVault _vault;
        readonly VaultGenerationHandle<int> _edgeTableHandle;
        readonly VaultGenerationHandle<int> _triTableHandle;
        readonly ulong _mutationGuardMask;

        public NativeArray<int>.ReadOnly EdgeTable
        {
            get
            {
                if (_vault == null)
                    return default;
                if (_vault.TryReadOnlyHandle(in _edgeTableHandle, out NativeArray<int>.ReadOnly table) && table.Length >= EdgeTableLength)
                    return table;
                return _vault.TryResolveHandle(in _edgeTableHandle, out NativeArray<int> mutableTable) && mutableTable.Length >= EdgeTableLength
                    ? mutableTable.AsReadOnly()
                    : default;
            }
        }

        public NativeArray<int>.ReadOnly TriTable
        {
            get
            {
                if (_vault == null)
                    return default;
                if (_vault.TryReadOnlyHandle(in _triTableHandle, out NativeArray<int>.ReadOnly table) && table.Length >= TriTableLength)
                    return table;
                return _vault.TryResolveHandle(in _triTableHandle, out NativeArray<int> mutableTable) && mutableTable.Length >= TriTableLength
                    ? mutableTable.AsReadOnly()
                    : default;
            }
        }

        public JobTableLease(
            IDataVault vault,
            in VaultGenerationHandle<int> edgeTableHandle,
            in VaultGenerationHandle<int> triTableHandle,
            ulong mutationGuardMask)
        {
            _vault = vault;
            _edgeTableHandle = edgeTableHandle;
            _triTableHandle = triTableHandle;
            _mutationGuardMask = mutationGuardMask;
        }

        public void Dispose()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            vault.ReleaseMutationGuard(_mutationGuardMask);
        }
    }

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
        _edgeTableHandle = default;
        _triTableHandle = default;
        _vault = null;
        _ready = 0;
        _initGate = 0;
        _editorHooksInstalled = false;
    }

    public static void Initialize()
    {
        Initialize(GlobalRegistry.DataVault);
    }

    public static void Initialize(IDataVault vault)
    {
        if (vault == null)
            return;

        if (Volatile.Read(ref _ready) == 1 && ReferenceEquals(_vault, vault))
            return;

        EnterInitGate();
        try
        {
            if (Volatile.Read(ref _ready) == 1 && ReferenceEquals(_vault, vault))
                return;

#if UNITY_EDITOR
            EnsureEditorHooks();
#endif

            if (!ReferenceEquals(_vault, vault))
                ReleaseVaultTables();

            _vault = vault;

            if (!InitializeEdgeTable(vault))
            {
                ReleaseVaultTables();
                return;
            }

            if (!InitializeTriTable(vault))
            {
                ReleaseVaultTables();
                return;
            }

            Volatile.Write(ref _ready, 1);
        }
        finally
        {
            ExitInitGate();
        }
    }

    private static bool InitializeEdgeTable(IDataVault vault)
    {
        ReadOnlySpan<int> et = stackalloc int[256]
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
            if (!TryAcquireWritableVaultTable(vault, EdgeTableBufferId, EdgeTableLength, ref _edgeTableHandle, out NativeArray<int> edgeTable))
            {
                return false;
            }
            bool edgeReleased = false;
            try
            {
                for (int i = 0; i < et.Length; i++)
                    edgeTable[i] = et[i];
            }
            finally
            {
                edgeReleased = vault.ReleaseWriteLock(in _edgeTableHandle, TableOwnerSystemId);
            }
            if (!edgeReleased)
            {
                return false;
            }
        return true;
    }

    private static bool InitializeTriTable(IDataVault vault)
    {
        ReadOnlySpan<int> tt = stackalloc int[4096]
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
            if (!TryAcquireWritableVaultTable(vault, TriTableBufferId, TriTableLength, ref _triTableHandle, out NativeArray<int> triTable))
            {
                return false;
            }
            bool triReleased = false;
            try
            {
                for (int i = 0; i < tt.Length; i++)
                    triTable[i] = tt[i];
            }
            finally
            {
                triReleased = vault.ReleaseWriteLock(in _triTableHandle, TableOwnerSystemId);
            }
            if (!triReleased)
            {
                return false;
            }
        return true;
    }

    static bool TryAcquireWritableVaultTable(
        IDataVault vault,
        BufferID bufferId,
        int requiredLength,
        ref VaultGenerationHandle<int> handle,
        out NativeArray<int> table)
    {
        table = default;
        if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
            return false;

        handle = vault.EnsureGenerationHandle<int>(
            bufferId,
            requiredLength,
            TableOwnerSystemId,
            NativeArrayOptions.UninitializedMemory);
        if (!IsTableHandleCreated(in handle, bufferId) ||
            vault.IsCompactionFenceActive ||
            !vault.TryAcquireWriteLock(in handle, TableOwnerSystemId, out table))
        {
            return false;
        }

        bool keepLock = false;
        try
        {
            if (vault.IsCompactionFenceActive)
                return false;

            if (table.IsCreated && table.Length >= requiredLength)
            {
                keepLock = true;
                return true;
            }

            return false;
        }
        finally
        {
            if (!keepLock)
            {
                vault.ReleaseWriteLock(in handle, TableOwnerSystemId);
                table = default;
            }
        }
    }

    static bool IsTableHandleCreated(in VaultGenerationHandle<int> handle, BufferID expectedBufferId)
    {
        return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
               handle.SystemID == (uint)TableOwnerSystemId &&
               handle.Generation != 0u;
    }

    static void ReleaseVaultTables()
    {
        IDataVault vault = _vault;
        if (vault != null)
        {
            if (IsTableHandleCreated(in _edgeTableHandle, EdgeTableBufferId))
                vault.ReleaseBuffer(in _edgeTableHandle);
            if (IsTableHandleCreated(in _triTableHandle, TriTableBufferId))
                vault.ReleaseBuffer(in _triTableHandle);
        }

        _edgeTableHandle = default;
        _triTableHandle = default;
        _vault = null;
        Volatile.Write(ref _ready, 0);
    }

    public static bool TryAcquireJobTables(out JobTableLease lease)
    {
        IDataVault vault = _vault;
        if (vault == null)
            vault = GlobalRegistry.DataVault;

        return TryAcquireJobTables(vault, out lease);
    }

    public static bool TryAcquireJobTables(IDataVault vault, out JobTableLease lease)
    {
        lease = default;
        if (vault == null)
            return false;

        Initialize(vault);
        if (!ReferenceEquals(_vault, vault) ||
            vault.IsCompactionFenceActive ||
            Volatile.Read(ref _ready) != 1 ||
            !IsTableHandleCreated(in _edgeTableHandle, EdgeTableBufferId) ||
            !IsTableHandleCreated(in _triTableHandle, TriTableBufferId))
        {
            return false;
        }

        bool mutationGuardAcquired = false;
        try
        {
            if (vault.IsCompactionFenceActive || !vault.TryAcquireMutationGuard(JobTableMutationGuardMask))
                return false;
            mutationGuardAcquired = true;

            if (vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _edgeTableHandle, out NativeArray<int>.ReadOnly edgeTable) ||
                edgeTable.Length < EdgeTableLength ||
                !vault.TryReadOnlyHandle(in _triTableHandle, out NativeArray<int>.ReadOnly triTable) ||
                triTable.Length < TriTableLength)
            {
                return false;
            }

            lease = new JobTableLease(vault, in _edgeTableHandle, in _triTableHandle, JobTableMutationGuardMask);
            mutationGuardAcquired = false;
            return true;
        }
        finally
        {
            if (mutationGuardAcquired)
                vault.ReleaseMutationGuard(JobTableMutationGuardMask);
        }
    }

#if UNITY_EDITOR
    public static bool TryAcquireEditorReadOnlyJobTables(IDataVault vault, out JobTableLease lease)
    {
        return TryAcquireEditorReadOnlyJobTables(vault, out lease, out _);
    }

    public static bool TryAcquireEditorReadOnlyJobTables(IDataVault vault, out JobTableLease lease, out string failureReason)
    {
        lease = default;
        failureReason = string.Empty;
        if (vault == null)
        {
            failureReason = "vault-null";
            return false;
        }

        Initialize(vault);
        if (!ReferenceEquals(_vault, vault))
        {
            failureReason = "vault-mismatch";
            return false;
        }

        if (vault.IsCompactionFenceActive)
        {
            failureReason = "compaction-fence";
            return false;
        }

        if (Volatile.Read(ref _ready) != 1)
        {
            failureReason = "not-ready";
            return false;
        }

        if (!IsTableHandleCreated(in _edgeTableHandle, EdgeTableBufferId))
        {
            failureReason = "edge-handle";
            return false;
        }

        if (!IsTableHandleCreated(in _triTableHandle, TriTableBufferId))
        {
            failureReason = "tri-handle";
            return false;
        }

        NativeArray<int>.ReadOnly edgeTable;
        if (!vault.TryReadOnlyHandle(in _edgeTableHandle, out edgeTable))
        {
            if (!vault.TryResolveHandle(in _edgeTableHandle, out NativeArray<int> mutableEdgeTable))
            {
                failureReason = "edge-readonly";
                return false;
            }
            edgeTable = mutableEdgeTable.AsReadOnly();
        }

        if (edgeTable.Length < EdgeTableLength)
        {
            failureReason = $"edge-length-{edgeTable.Length}";
            return false;
        }

        NativeArray<int>.ReadOnly triTable;
        if (!vault.TryReadOnlyHandle(in _triTableHandle, out triTable))
        {
            if (!vault.TryResolveHandle(in _triTableHandle, out NativeArray<int> mutableTriTable))
            {
                failureReason = "tri-readonly";
                return false;
            }
            triTable = mutableTriTable.AsReadOnly();
        }

        if (triTable.Length < TriTableLength)
        {
            failureReason = $"tri-length-{triTable.Length}";
            return false;
        }

        lease = new JobTableLease(vault, in _edgeTableHandle, in _triTableHandle, 0UL);
        return true;
    }
#endif

    static ulong TableMutationGuardBit(BufferID bufferId)
    {
        return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
    }

    static void EnterInitGate()
    {
        SpinWait spinWait = default;
        while (Interlocked.CompareExchange(ref _initGate, 1, 0) != 0)
            spinWait.SpinOnce();
    }

    static void ExitInitGate()
    {
        Volatile.Write(ref _initGate, 0);
    }

    public static void Shutdown()
    {
        EnterInitGate();
        try
        {
#if UNITY_EDITOR
            ReleaseEditorHooks();
#endif
            ReleaseVaultTables();
        }
        finally
        {
            ExitInitGate();
        }
    }
}

#endregion
