using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

public static class GraphCleanupPhantomLinks
{
    [MenuItem("Hecton8/Diagnostics/Cleanup Phantom Links")]
    public static void Run()
    {
        var graph = AssetDatabase.LoadAssetAtPath<Graph>("Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset");
        var splatmap = graph.generators.FirstOrDefault(g => g.id == 9077949529453494279UL);
        var multi = splatmap as IMultiInlet;
        
        var validInletIds = new HashSet<ulong>(multi.Inlets().Select(i => i.Id));
        
        var keysToRemove = new List<IInlet<object>>();
        foreach (var kvp in graph.links)
        {
            if (kvp.Key?.Gen?.id == 9077949529453494279UL && !validInletIds.Contains(kvp.Key.Id))
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            graph.links.Remove(key, out _);
        }
        
        if (keysToRemove.Count > 0)
        {
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\cleanup_result.txt", $"Removed {keysToRemove.Count} phantom links.");
        EditorApplication.Exit(0);
    }
}
