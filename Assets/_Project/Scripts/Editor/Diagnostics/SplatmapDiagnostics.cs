using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

/// <summary>
/// Read-only dump of the splatmap node's inlets and what each one is wired to. It renders nothing, blits
/// nothing and encodes no PNG - it only walks graph.links - so there is deliberately NO GPU refusal here.
/// The name suggests otherwise; the code does not.
/// </summary>
public static class SplatmapDiagnostics
{
    private const string GraphAssetPath =
        "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

    private const ulong SplatmapNodeId = 9077949529453494279UL;

    // Was C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\splat_inlets.txt - another agent's private
    // scratch directory, outside the repo and unversioned. Its own subfolder, NOT shared with
    // SplatmapDiagnostics2: the two tools both describe the same node from different angles, and pointing
    // them at one directory is how Stage1Check and Stage1VerifyAndRelink used to destroy each other's
    // evidence. `static readonly` rather than `const` because Path.Combine is not a compile-time constant.
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "splatmap_inlets");

    [MenuItem("Hecton8/Diagnostics/Dump Splatmap Inlets")]
    public static void Run()
    {
        try
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
            if (graph == null)
            {
                Debug.LogError(
                    $"[SplatmapDiagnostics] REFUSED: no Graph asset at '{GraphAssetPath}'. No inlet dump " +
                    "was produced. If the graph moved, this tool's hardcoded path is stale.");
                EditorApplication.Exit(2);
                return;
            }

            Generator splatmap = graph.generators.FirstOrDefault(g => g.id == SplatmapNodeId);
            if (splatmap == null)
            {
                // This used to NullReference two lines later, or - worse, on any code path that tolerated
                // it - dump an empty inlet list and exit 0. A stale hardcoded node id must not be
                // indistinguishable from "the splatmap has no inlets".
                Debug.LogError(
                    $"[SplatmapDiagnostics] REFUSED: no generator with id {SplatmapNodeId} in " +
                    $"'{GraphAssetPath}'. No inlet dump was produced. The graph has " +
                    $"{graph.generators.Length} generator(s); this tool's hardcoded splatmap id is stale.");
                EditorApplication.Exit(2);
                return;
            }

            IMultiInlet multi = splatmap as IMultiInlet;
            if (multi == null)
            {
                Debug.LogError(
                    $"[SplatmapDiagnostics] REFUSED: generator {SplatmapNodeId} is a " +
                    $"{splatmap.GetType().Name}, which does not implement IMultiInlet, so it has no inlet " +
                    "list to dump. No dump was produced.");
                EditorApplication.Exit(2);
                return;
            }

            if (graph.links.Count == 0)
            {
                // MapMagic's deserializer SILENTLY drops every link whose inlet or outlet failed to
                // deserialize - GraphSerializer.CheckNullLinks rebuilds the pair arrays with no error and
                // no warning - and Graph.links is [NonSerialized], rebuilt only by that path. So an empty
                // link table on a production graph means the table did not load, not that nothing is
                // wired. Without this guard the loop below reported every inlet as "-> Outlet=" null and
                // exited 0, so "I could not read the graph's links" and "the splatmap is unwired" were
                // the same report.
                Debug.LogError(
                    $"[SplatmapDiagnostics] REFUSED: '{GraphAssetPath}' deserialized with ZERO links in " +
                    $"the whole graph, while carrying {graph.generators.Length} generator(s). No inlet " +
                    "dump was produced: against an empty link table every inlet reads as unconnected " +
                    "whether or not it actually is.");
                EditorApplication.Exit(2);
                return;
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine($"Splatmap Inlets (node {SplatmapNodeId}, {splatmap.GetType().Name}):");

            int inletCount = 0;
            int connectedCount = 0;
            foreach (var inlet in multi.Inlets())
            {
                inletCount++;
                var outlet = graph.links.ContainsKey(inlet) ? graph.links[inlet] : null;
                if (outlet != null) connectedCount++;
                log.AppendLine($"- Inlet Id={inlet.Id}, Gen={inlet.Gen?.GetType().Name} -> Outlet={outlet?.Gen?.GetType().Name} ({outlet?.Gen?.id})");
            }

            log.AppendLine($"TOTALS: {inletCount} inlet(s), {connectedCount} connected, {inletCount - connectedCount} unconnected.");

            Directory.CreateDirectory(OutputDir);
            string outPath = Path.Combine(OutputDir, "splat_inlets.txt");
            File.WriteAllText(outPath, log.ToString());

            if (inletCount == 0)
            {
                // An empty file is not a finding, it is a non-answer. Say so rather than exiting 0 on it.
                Debug.LogError(
                    $"[SplatmapDiagnostics] FAILED: node {SplatmapNodeId} ({splatmap.GetType().Name}) " +
                    $"enumerated ZERO inlets, so '{outPath}' contains no link data. That is a broken " +
                    "reflection/IMultiInlet assumption, not a wiring answer.");
                EditorApplication.Exit(2);
                return;
            }

            // Echo the answer into the Unity log: batchmode log readers are the only channel anyone
            // actually reads, and the file alone left every previous run's conclusion unrecorded.
            Debug.Log($"[SplatmapDiagnostics] Wrote {outPath}.\n{log}");
        }
        catch (Exception ex)
        {
            // The old version had no catch at all and ended in an unconditional Exit(0). A throw from
            // File.WriteAllText into the foreign brain directory - which may not exist - produced no dump
            // and no visible error.
            Debug.LogError($"[SplatmapDiagnostics] FAILED, no inlet dump was written: {ex}");
            EditorApplication.Exit(2);
            return;
        }

        EditorApplication.Exit(0);
    }
}
