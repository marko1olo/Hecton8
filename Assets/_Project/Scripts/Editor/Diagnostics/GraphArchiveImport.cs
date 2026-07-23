// ============================================================================
// GraphArchiveImport.cs
// Analyzes the downstream chain of Import200.
// Removes Import200 if the chain leads to no critical outputs.
// ============================================================================

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

namespace Hecton8.Editor.Diagnostics
{
    public static class GraphArchiveImport
    {
        private const string GraphAssetPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        private const string ArtifactDir =
            @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0";

        private const ulong Import200Id = 12389088114570690561UL;

        [MenuItem("Hecton8/Diagnostics/Archive Import200")]
        public static void Run()
        {
            try
            {
                DoRun();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(ArtifactDir, "archive_import_error.txt"), ex.ToString());
                Debug.LogError("[Archive] CRASH: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void DoRun()
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
            if (graph == null) throw new Exception("Graph not found: " + GraphAssetPath);

            Generator importNode = graph.generators.FirstOrDefault(g => g.id == Import200Id);
            if (importNode == null)
            {
                File.WriteAllText(Path.Combine(ArtifactDir, "archive_import_result.md"), "Import200 not found in graph. It may already be removed.");
                return;
            }

            // Find all downstream nodes
            HashSet<Generator> visited = new HashSet<Generator>();
            Queue<Generator> queue = new Queue<Generator>();
            queue.Enqueue(importNode);
            visited.Add(importNode);

            List<Generator> downstreamNodes = new List<Generator>();

            while (queue.Count > 0)
            {
                Generator current = queue.Dequeue();
                downstreamNodes.Add(current);

                // Find all links where current is the source (outlet)
                foreach (var kvp in graph.links)
                {
                    if (kvp.Value != null && kvp.Value.Gen == current)
                    {
                        Generator target = kvp.Key?.Gen;
                        if (target != null && !visited.Contains(target))
                        {
                            visited.Add(target);
                            queue.Enqueue(target);
                        }
                    }
                }
            }

            // Check if any of the downstream nodes are Outputs or critical nodes
            bool hasCriticalConsumers = false;
            var log = new System.Text.StringBuilder();
            log.AppendLine("# IMPORT200 CHAIN ANALYSIS");
            log.AppendLine($"Found {downstreamNodes.Count} nodes in the downstream chain of Import200.");

            foreach (var node in downstreamNodes)
            {
                log.AppendLine($"- {node.GetType().Name} id={node.id}");
                if (node is MapMagic.Nodes.MatrixGenerators.HeightOutput200 ||
                    node is MapMagic.Nodes.MatrixGenerators.TexturesOutput200 ||
                    node.GetType().Name.Contains("Biome") ||
                    node.GetType().Name.Contains("Splatmap"))
                {
                    hasCriticalConsumers = true;
                    log.AppendLine($"  [!] CRITICAL CONSUMER DETECTED");
                }
            }

            if (hasCriticalConsumers)
            {
                log.AppendLine("\n[RESULT] Cannot archive. Critical consumers found in downstream chain.");
            }
            else
            {
                log.AppendLine("\n[RESULT] No critical consumers found. The chain is DEAD.");
                log.AppendLine("Removing Import200 from graph...");

                // Remove the node
                var genList = graph.generators.ToList(); genList.Remove(importNode); graph.generators = genList.ToArray();

                // Also remove all links connected to it
                var keysToRemove = new List<IInlet<object>>();
                foreach (var kvp in graph.links)
                {
                    if (kvp.Key?.Gen == importNode || kvp.Value?.Gen == importNode)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                foreach (var key in keysToRemove)
                {
                    graph.links.Remove(key, out _);
                }

                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                log.AppendLine("Import200 successfully removed from graph.");
            }

            File.WriteAllText(Path.Combine(ArtifactDir, "archive_import_result.md"), log.ToString());
            Debug.Log("[Archive] Finished.");
        }
    }
}
