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
                Blend200 blend = graph.generators.FirstOrDefault(g => g.id == 17008246414020444181UL) as Blend200;

                if (blend != null)
                {
                    blend.layers[1].algorithm = Blend200.BlendAlgorithm.add; // Changed from max to add!
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.SaveAssets();
                    File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/fix2_log.txt", "Fixed to Add.");
                }
            }
            catch (System.Exception ex)
            {
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/fix2_log.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
