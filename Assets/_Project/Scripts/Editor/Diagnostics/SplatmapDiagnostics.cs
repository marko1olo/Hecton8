using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

public static class SplatmapDiagnostics
{
    [MenuItem("Hecton8/Diagnostics/Dump Splatmap Inlets")]
    public static void Run()
    {
        var graph = AssetDatabase.LoadAssetAtPath<Graph>("Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset");
        var splatmap = graph.generators.FirstOrDefault(g => g.id == 9077949529453494279UL);
        var multi = splatmap as IMultiInlet;
        var log = new System.Text.StringBuilder();
        log.AppendLine("Splatmap Inlets:");
        foreach (var inlet in multi.Inlets())
        {
            var outlet = graph.links.ContainsKey(inlet) ? graph.links[inlet] : null;
            log.AppendLine($"- Inlet Id={inlet.Id}, Gen={inlet.Gen?.GetType().Name} -> Outlet={outlet?.Gen?.GetType().Name} ({outlet?.Gen?.id})");
        }
        File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\splat_inlets.txt", log.ToString());
        EditorApplication.Exit(0);
    }
}
