using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using System.Linq;
using System.IO;

namespace MapMagic.Editor.Diagnostics
{
    public static class FixGraphAmplitudeTask
    {
        public static void Fix()
        {
            try
            {
                string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
                Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
                if (graph == null) throw new System.Exception("Graph not found!");

                Blend200 blend = graph.generators.FirstOrDefault(g => g.id == 17008246414020444181UL) as Blend200;
                if (blend == null) throw new System.Exception("Blend200 node not found!");

                string before = "";
                for (int i=0; i<blend.layers.Length; i++) {
                    before += $"Layer {i}: alg={blend.layers[i].algorithm}, op={blend.layers[i].opacity}\n";
                }

                // Change algorithm to Max for all layers that are currently something else
                // Layer 0 is usually base, Layer 1 is top.
                // If it is Mix, and it's the constant, we should set it to Max.
                if (blend.layers.Length > 1 && blend.layers[1].algorithm != Blend200.BlendAlgorithm.max)
                {
                    blend.layers[1].algorithm = Blend200.BlendAlgorithm.max;
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.SaveAssets();
                }

                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/fix_log.txt", "Before:\n" + before + "\nFixed to Max.");
            }
            catch (System.Exception ex)
            {
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/fix_log.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
