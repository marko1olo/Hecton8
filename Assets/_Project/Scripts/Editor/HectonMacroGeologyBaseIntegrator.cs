using System;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class HectonMacroGeologyBaseIntegrator
    {
        private const string GraphPath = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        [MenuItem("Hecton8/World/MapMagic/Integrate Macro Geology Base")]
        public static void RunIntegration()
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
            if (graph == null)
            {
                Debug.LogError($"[HectonMacroGeologyBaseIntegrator] Graph asset not found at {GraphPath}");
                return;
            }

            // Log all generators in graph
            if (graph.generators == null)
            {
                Debug.LogError("[HectonMacroGeologyBaseIntegrator] graph.generators is NULL!");
            }
            else
            {
                Debug.Log($"[HectonMacroGeologyBaseIntegrator] Found {graph.generators.Length} generators:");
                for (int i = 0; i < graph.generators.Length; i++)
                {
                    var gen = graph.generators[i];
                    Debug.Log($"  Generator [{i}]: {(gen != null ? gen.GetType().FullName : "NULL")}");
                }
            }

            // Find Tectonic Node
            HectonBiomeMatrixMapMagicPostProcessNode tectonicNode = FindFirst<HectonBiomeMatrixMapMagicPostProcessNode>(graph);
            if (tectonicNode == null)
            {
                Debug.LogError("[HectonMacroGeologyBaseIntegrator] HectonBiomeMatrixMapMagicPostProcessNode not found in graph!");
                return;
            }

            // Find or Create Macro Geology Base Node
            HectonSandboxAbyssalShelfMapMagicNode macroBaseNode = EnsureGenerator<HectonSandboxAbyssalShelfMapMagicNode>(graph, -660f, -80f, out bool created);

            // Unlink Tectonic Node if it is linked to something else
            if (graph.IsLinked(tectonicNode))
            {
                graph.UnlinkInlet(tectonicNode);
                Debug.Log("[HectonMacroGeologyBaseIntegrator] Unlinked old connection to Tectonic Node.");
            }

            // Link Macro Geology Base Node to Tectonic Node
            graph.Link(macroBaseNode, tectonicNode);
            Debug.Log($"[HectonMacroGeologyBaseIntegrator] Linked Macro Geology Base Node to Tectonic Node. Created node: {created}");

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static T EnsureGenerator<T>(Graph graph, float x, float y, out bool created) where T : Generator
        {
            T existing = FindFirst<T>(graph);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            T generator = (T)Generator.Create(typeof(T));
            generator.guiPosition = new Vector2(x, y);
            graph.Add(generator);
            created = true;
            return generator;
        }

        private static T FindFirst<T>(Graph graph)
        {
            Generator[] generators = graph != null ? graph.generators : null;
            if (generators == null)
                return default;

            for (int i = 0; i < generators.Length; i++)
            {
                if (generators[i] is T generator)
                    return generator;
            }

            return default;
        }
    }
}
