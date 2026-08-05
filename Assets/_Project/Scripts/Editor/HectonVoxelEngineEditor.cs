using UnityEditor;
using UnityEngine;
using Hecton8.Caves;
using Hecton8.World;
using Hecton8.World.VoxelSurfaceNets;

namespace Hecton8.EditorTools
{
    [CustomEditor(typeof(HectonVoxelEngine))]
    public class HectonVoxelEngineEditor : UnityEditor.Editor
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
                    $"⭐ CAVE VOXEL ENGINE v4.0 ⭐\n" +
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
                $"⭐ CURRENT PRESET: {preset.presetName} ⭐\n" +
                $"Grid: {dim}³ | Voxel: {vox}m | Coverage: {coverage:F0}m\n" +
                $"Rooms: {preset.minRooms}-{preset.maxRooms}\n" +
                $"Density: {densityMB:F1} MB | MC Buffer: {rawMB:F1} MB\n" +
                $"Peak temp: {totalMB:F1} MB (freed after gen)\n" +
                "MC Buffer: two-pass exact extraction (no truncation)",
                totalMB > 100f ? MessageType.Warning : MessageType.None);
    
            EditorGUILayout.Space(5);
        }
    }
}
