// ============================================================================
// GraphEnableErosion.cs
// Enables the Erosion node in the graph.
// ============================================================================

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

namespace Hecton8.Editor.Diagnostics
{
    public static class GraphEnableErosion
    {
        private const string GraphAssetPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        private const string ArtifactDir =
            @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0";

        private const ulong ErosionNodeId = 9077947330430238722UL;

        [MenuItem("Hecton8/Diagnostics/Enable Erosion (Batch)")]
        public static void Run()
        {
            try
            {
                DoRun();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(ArtifactDir, "enable_erosion_error.txt"), ex.ToString());
                Debug.LogError("[EnableErosion] CRASH: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void DoRun()
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
            if (graph == null) throw new Exception("Graph not found");

            dynamic erosion = graph.generators.FirstOrDefault(g => g.id == ErosionNodeId);
            if (erosion == null) throw new Exception("Erosion not found");

            erosion.enabled = true;
            Debug.Log($"[EnableErosion] Erosion node {erosion.id} enabled is now: {erosion.enabled}");

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            File.WriteAllText(Path.Combine(ArtifactDir, "enable_erosion_result.md"), $"# Result\nErosion node {erosion.id} enabled = true.");
            Debug.Log("[EnableErosion] Finished.");
        }
    }
}
