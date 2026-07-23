using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

public static class SplatmapDiagnostics2
{
    [MenuItem("Hecton8/Diagnostics/Dump Splatmap All Links")]
    public static void Run()
    {
        var graph = AssetDatabase.LoadAssetAtPath<Graph>("Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset");
        var log = new System.Text.StringBuilder();
        foreach (var kvp in graph.links)
        {
            if (kvp.Key?.Gen?.id == 9077949529453494279UL)
            {
                log.AppendLine($"Splatmap INLET: Id={kvp.Key?.Id} is connected to OUTLET: Id={kvp.Value?.Id}, Gen={kvp.Value?.Gen?.GetType().Name} ({kvp.Value?.Gen?.id})");
            }
            if (kvp.Value?.Gen?.id == 9077949529453494279UL)
            {
                log.AppendLine($"Splatmap OUTLET: Id={kvp.Value?.Id} is connected to INLET: Id={kvp.Key?.Id}, Gen={kvp.Key?.Gen?.GetType().Name} ({kvp.Key?.Gen?.id})");
            }
        }
        File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\splat_all_links.txt", log.ToString());
        EditorApplication.Exit(0);
    }
}
