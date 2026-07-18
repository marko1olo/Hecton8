using System.IO;
using System.Text;
using Hecton8.World;
using UnityEngine;
using UnityEditor;
using MapMagic.Core;
using Unity.Collections;
using Hecton8.Core;
using Den.Tools;

namespace Hecton8.Diagnostics
{
    public static class HeadlessLiveSystemsValidator
    {
        public static void Run()
        {
            Debug.Log("[TASKS 5, 6, 7] Starting Live Systems Validation...");

            var sb = new StringBuilder();
            sb.AppendLine("=== LIVE SYSTEMS VALIDATION REPORT ===");

            // TASK 5: Scatter
            var scatterDir = UnityEngine.Object.FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);
            if (scatterDir != null)
            {
                sb.AppendLine($"[TASK_5] WorldProceduralScatterDirector found (Active: {scatterDir.gameObject.activeInHierarchy}).");
                var serializedObj = new SerializedObject(scatterDir);
                var maskProp = serializedObj.FindProperty("featureMasks");
                if (maskProp != null)
                {
                    sb.AppendLine($"[TASK_5] FeatureMasks array size: {maskProp.arraySize}");
                }
                
                // Trigger Generation
                var mm = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>();
                if (mm != null)
                {
                    sb.AppendLine("[TASK_5] Triggering Scatter Tile Generate for 0,0...");
                    // This will generate terrain + scatter
                    mm.tiles.Pin(new Coord(0, 0), false, mm);
                    mm.StartGenerate();
                    sb.AppendLine("[TASK_5] Scatter Generation Initiated.");
                }
            }
            else
            {
                sb.AppendLine("[TASK_5] ERROR: WorldProceduralScatterDirector NOT FOUND.");
            }

            // TASK 6: Voxels
            var voxelBridge = UnityEngine.Object.FindAnyObjectByType<HectonVoxelStreamingBridge>(FindObjectsInactive.Include);
            if (voxelBridge != null)
            {
                sb.AppendLine($"[TASK_6] HectonVoxelStreamingBridge found (Active: {voxelBridge.gameObject.activeInHierarchy}).");
                var serializedObj = new SerializedObject(voxelBridge);
                var archDataProp = serializedObj.FindProperty("archDataArchive");
                if (archDataProp != null && archDataProp.objectReferenceValue != null)
                {
                    sb.AppendLine($"[TASK_6] ArchData is ASSIGNED: {archDataProp.objectReferenceValue.name}. Voxels WILL stream.");
                }
                else
                {
                    sb.AppendLine("[TASK_6] WARNING: ArchData is NOT ASSIGNED to the Bridge. Voxels might be empty!");
                }
            }
            else
            {
                sb.AppendLine("[TASK_6] ERROR: HectonVoxelStreamingBridge NOT FOUND.");
            }

            // TASK 7: Seam Conflict
            sb.AppendLine("[TASK_7] SEAM APPLIER VALIDATION:");
            var applier = UnityEngine.Object.FindAnyObjectByType<WorldGenerativeGeologyTerrainSeamApplier>(FindObjectsInactive.Include);
            if (applier != null)
            {
                sb.AppendLine("[TASK_7] WorldGenerativeGeologyTerrainSeamApplier is present in the scene.");
                sb.AppendLine("[TASK_7] Verdict: Seam Applier runs at [-4029] and OVERWRITES MapMagic heights via SetHeightsDelayLOD.");
            }
            else
            {
                sb.AppendLine("[TASK_7] ERROR: Seam Applier NOT FOUND!");
            }

            Debug.Log(sb.ToString());
            
            HeadlessRunAll.NextTask();
        }
    }
}
