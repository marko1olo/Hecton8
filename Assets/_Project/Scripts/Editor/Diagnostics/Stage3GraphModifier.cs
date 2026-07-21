using UnityEngine;
using UnityEditor;
using MapMagic.Nodes;
using MapMagic.Nodes.ObjectsGenerators;
using Den.Tools.Matrices;

namespace Hecton8.Editor
{
    public static class Stage3GraphModifier
    {
        [MenuItem("Tools/MapMagic/Stage3 Add Scatter")]
        public static void Run()
        {
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            var graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
            if (graph == null) { Debug.LogError("Graph not found"); return; }

            // Find HectonHydraulicErosionMapMagicNode
            MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode erosionNode = null;
            foreach(var node in graph.generators)
            {
                if (node is MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode eNode)
                {
                    erosionNode = eNode;
                    break;
                }
            }
            if (erosionNode == null) { Debug.LogError("Erosion node not found"); return; }

            // Create Scatter
            var scatter = Generator.Create(typeof(Scatter200)) as Scatter200;
            scatter.density = 2000; 
            scatter.uniformity = 0.1f;
            graph.Add(scatter);

            // Create Mask
            var mask = Generator.Create(typeof(MapMagic.Nodes.ObjectsGenerators.Mask200)) as MapMagic.Nodes.ObjectsGenerators.Mask200;
            graph.Add(mask);

            // Create Output
            var objOut = Generator.Create(typeof(MapMagic.Nodes.ObjectsGenerators.ObjectsOutput)) as MapMagic.Nodes.ObjectsGenerators.ObjectsOutput;
            objOut.prefabs = new GameObject[1];
            objOut.prefabs[0] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_rock_small_floor__low.prefab");
            if (objOut.posSettings == null) objOut.posSettings = new PositioningSettings();
            graph.Add(objOut);

            // Link
            graph.Link(erosionNode.sedimentMaskOut, mask.maskIn);
            graph.Link(scatter, mask.srcIn);
            graph.Link(mask, objOut);

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log("Scatter nodes added to Sandbox graph.");
        }
    }
}
