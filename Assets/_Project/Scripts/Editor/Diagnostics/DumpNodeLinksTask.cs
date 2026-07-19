using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace MapMagic.Editor.Diagnostics
{
    public static class DumpNodeLinksTask
    {
        public static void Dump()
        {
            try
            {
                string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
                Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
                if (graph == null) throw new System.Exception("Graph not found!");

                Blend200 blend = graph.generators.FirstOrDefault(g => g.id == 17008246414020444181UL) as Blend200;
                if (blend == null) throw new System.Exception("Blend200 node not found!");

                string log = "Blend Node 17008246414020444181 Inlets:\n";
                foreach (var inlet in blend.Inlets()) {
                    var link = graph.links.FirstOrDefault(kvp => kvp.Key == inlet);
                    if (link.Key != null && link.Value != null) {
                        var sourceGen = link.Value.Gen;
                        log += $"Inlet {inlet.Id} (Layer ?) is connected to Generator {sourceGen.id} ({sourceGen.GetType().Name})\n";
                    } else {
                        log += $"Inlet {inlet.Id} is UNCONNECTED\n";
                    }
                }
                
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/node_links.txt", log);
            }
            catch (System.Exception ex)
            {
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/node_links.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
