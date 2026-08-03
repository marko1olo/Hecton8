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
