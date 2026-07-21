using UnityEditor;
using MapMagic.Nodes;
using MapMagic.Core;
using System.Linq;

public static class DumpGraph
{
    [MenuItem("Hecton8/Diagnostics/Test Graph Dump")]
    public static void Dump()
    {
        var graph = AssetDatabase.LoadAssetAtPath<Graph>("Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset");
        if (graph == null) 
        {
            UnityEngine.Debug.Log("Graph not found!");
            EditorApplication.Exit(1);
            return;
        }

        foreach (var n in graph.generators)
        {
            // Generator may not have a "name", so just print the type.
            UnityEngine.Debug.Log("NODE: " + n.GetType().Name);
            if (n is MapMagic.Nodes.MatrixGenerators.Voronoi200 voronoi)
            {
                UnityEngine.Debug.Log("  - Voronoi Cell Size: " + voronoi.cellSize + " Uniformity: " + voronoi.uniformity);
            }
            if (n is MapMagic.Nodes.MatrixGenerators.Erosion200 erosion)
            {
                UnityEngine.Debug.Log("  - Erosion Iterations: " + erosion.iterations);
            }
        }
        
        foreach (var link in graph.links)
        {
            if (link.Key != null && link.Value != null && link.Key.Gen != null && link.Value.Gen != null)
            {
                UnityEngine.Debug.Log("LINK: " + link.Key.Gen.GetType().Name + " -> " + link.Value.Gen.GetType().Name);
            }
        }
        
        EditorApplication.Exit(0);
    }
}
