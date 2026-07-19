using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using System.Linq;
using System.IO;

namespace MapMagic.Editor.Diagnostics
{
    public static class DumpBlendPropertiesTask
    {
        public static void Dump()
        {
            try
            {
                string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
                Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
                Blend200 blend = graph.generators.FirstOrDefault(g => g.id == 17008246414020444181UL) as Blend200;

                string log = "Blend Node 17008246414020444181 Layers:\n";
                for (int i = 0; i < blend.layers.Length; i++)
                {
                    var layer = blend.layers[i];
                    log += $"Layer {i}: Inlet={layer.inlet.Id}, Alg={layer.algorithm}, Opacity={layer.opacity}\n";
                }
                
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/blend_layers.txt", log);
            }
            catch (System.Exception ex)
            {
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/blend_layers.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
