using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;

namespace MapMagic.Editor.Diagnostics
{
    public static class DisableErosionNodeTask
    {
        [MenuItem("Hecton8/Graph/Disable Erosion Node")]
        public static void DisableErosionNode()
        {
            try
            {
                DoDisable();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DisableErosionNode] Exception: {ex}");
            }
        }

        public static void DisableErosionNodeBatchmode()
        {
            try
            {
                DoDisable();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DisableErosionNode] Exception: {ex}");
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        private static void DoDisable()
        {
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
            if (graph == null)
            {
                Debug.LogError($"[DisableErosionNode] Could not load Graph asset at path '{path}'");
                return;
            }

            Generator erosionGen = graph.generators.FirstOrDefault(g => g != null && g.GetType().Name == "HectonHydraulicErosionMapMagicNode");
            if (erosionGen == null)
            {
                Debug.LogWarning("[DisableErosionNode] EROSION NODE NOT FOUND");
                return;
            }

            string nodeTypeName = erosionGen.GetType().Name;
            ulong nodeId = erosionGen.id;
            bool enabledBefore = erosionGen.enabled;
            ulong versionBefore = erosionGen.version;

            Debug.Log($"[DisableErosionNode] BEFORE: Node={nodeTypeName}, Id={nodeId}, Enabled={enabledBefore}, Version={versionBefore}");

            erosionGen.enabled = false;
            erosionGen.version++;

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool enabledAfter = erosionGen.enabled;
            ulong versionAfter = erosionGen.version;

            Debug.Log($"[DisableErosionNode] AFTER: Node={nodeTypeName}, Id={nodeId}, Enabled={enabledAfter}, Version={versionAfter}");
        }
    }
}
