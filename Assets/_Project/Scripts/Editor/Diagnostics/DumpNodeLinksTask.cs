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
                
                // Was another agent's private brain directory - outside the repo and unversioned.
                string outDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(outDirectory);
                string outPath = Path.Combine(outDirectory, "node_links.txt");
                File.WriteAllText(outPath, log);
                Debug.Log($"[DumpNodeLinksTask] Wrote node link dump to {outPath}");
            }
            catch (System.Exception ex)
            {
                // The old catch wrote the stack trace INTO node_links.txt - the same file the success
                // path writes - and then exited 0. So a failure silently replaced the dump with a stack
                // trace while reporting success, and anyone opening the file to read link data found
                // an exception instead, with no way to tell when it had been clobbered.
                Debug.LogError("[DumpNodeLinksTask] FAILED, no dump was written: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }
    }
}
