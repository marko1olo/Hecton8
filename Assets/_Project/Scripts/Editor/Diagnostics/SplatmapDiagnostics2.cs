using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

/// <summary>
/// Read-only dump of every link in the graph that touches the splatmap node, in either direction. It walks
/// graph.links only - no matrices, no blits, no PNG - so there is deliberately NO GPU refusal here. The
/// name suggests otherwise; the code does not.
///
/// Companion to SplatmapDiagnostics, which dumps the node's declared inlets instead of the live link table.
/// The two write into SEPARATE Logs subfolders on purpose.
/// </summary>
public static class SplatmapDiagnostics2
{
    private const string GraphAssetPath =
        "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

    private const ulong SplatmapNodeId = 9077949529453494279UL;

    // Was C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\splat_all_links.txt - another agent's
    // private scratch directory, outside the repo and unversioned. `static readonly` rather than `const`
    // because Path.Combine is not a compile-time constant.
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "splatmap_all_links");

    [MenuItem("Hecton8/Diagnostics/Dump Splatmap All Links")]
    public static void Run()
    {
        try
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
            if (graph == null)
            {
                Debug.LogError(
                    $"[SplatmapDiagnostics2] REFUSED: no Graph asset at '{GraphAssetPath}'. No link dump " +
                    "was produced. If the graph moved, this tool's hardcoded path is stale.");
                EditorApplication.Exit(2);
                return;
            }

            // Confirm the node exists BEFORE scanning for its links. Without this, a stale hardcoded id
            // simply matched nothing, wrote a zero-byte file and exited 0 - so "the splatmap is wired to
            // nothing" and "I was looking for a node that no longer exists" were the same report.
            Generator splatmap = graph.generators.FirstOrDefault(g => g.id == SplatmapNodeId);
            if (splatmap == null)
            {
                Debug.LogError(
                    $"[SplatmapDiagnostics2] REFUSED: no generator with id {SplatmapNodeId} in " +
                    $"'{GraphAssetPath}'. No link dump was produced. The graph has " +
                    $"{graph.generators.Length} generator(s); this tool's hardcoded splatmap id is stale.");
                EditorApplication.Exit(2);
                return;
            }

            if (graph.links.Count == 0)
            {
                // MapMagic's deserializer SILENTLY drops every link whose inlet or outlet failed to
                // deserialize - GraphSerializer.CheckNullLinks rebuilds the pair arrays with no error and
                // no warning - and Graph.links is [NonSerialized], rebuilt only by that path. The
                // ISOLATED verdict below only means something against a link table that actually loaded;
                // on an empty table it fired unconditionally and still exited 0, so "I could not read the
                // graph's links" and "the splatmap is wired to nothing" were the same report.
                Debug.LogError(
                    $"[SplatmapDiagnostics2] REFUSED: '{GraphAssetPath}' deserialized with ZERO links in " +
                    $"the whole graph, while carrying {graph.generators.Length} generator(s). No link " +
                    "dump was produced: an empty link table cannot tell an isolated node apart from a " +
                    "graph whose links failed to load.");
                EditorApplication.Exit(2);
                return;
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine($"Splatmap link dump (node {SplatmapNodeId}, {splatmap.GetType().Name}):");

            int inletHits = 0;
            int outletHits = 0;
            foreach (var kvp in graph.links)
            {
                if (kvp.Key?.Gen?.id == SplatmapNodeId)
                {
                    inletHits++;
                    log.AppendLine($"Splatmap INLET: Id={kvp.Key?.Id} is connected to OUTLET: Id={kvp.Value?.Id}, Gen={kvp.Value?.Gen?.GetType().Name} ({kvp.Value?.Gen?.id})");
                }
                if (kvp.Value?.Gen?.id == SplatmapNodeId)
                {
                    outletHits++;
                    log.AppendLine($"Splatmap OUTLET: Id={kvp.Value?.Id} is connected to INLET: Id={kvp.Key?.Id}, Gen={kvp.Key?.Gen?.GetType().Name} ({kvp.Key?.Gen?.id})");
                }
            }

            log.AppendLine(
                $"TOTALS: {inletHits} incoming link(s), {outletHits} outgoing link(s), scanned " +
                $"{graph.links.Count} link(s) in the graph.");

            if (inletHits == 0 && outletHits == 0)
            {
                // The node exists, the link table loaded (guarded above: Count > 0), and nothing in it
                // touches this node. That IS an answer, and a loud one - an unwired splatmap produces
                // uniform terrain textures - so it is reported as a real finding. Exit stays 0 because
                // the tool did prove the work; the defect is in the graph, not in this tool.
                log.AppendLine(
                    "VERDICT: the splatmap node exists but is ISOLATED - no link in the graph reaches or " +
                    "leaves it.");
                Debug.LogWarning(
                    $"[SplatmapDiagnostics2] Node {SplatmapNodeId} ({splatmap.GetType().Name}) is present " +
                    $"in the graph but has ZERO links in either direction across {graph.links.Count} " +
                    "scanned links. That is a real wiring defect, not a missing node.");
            }

            Directory.CreateDirectory(OutputDir);
            string outPath = Path.Combine(OutputDir, "splat_all_links.txt");
            File.WriteAllText(outPath, log.ToString());

            // Echo into the Unity log: the batchmode log is the only channel anyone reads, and the file
            // alone left every previous run's conclusion unrecorded.
            Debug.Log($"[SplatmapDiagnostics2] Wrote {outPath}.\n{log}");
        }
        catch (Exception ex)
        {
            // The old version had no catch at all and ended in an unconditional Exit(0). A throw from
            // File.WriteAllText into the foreign brain directory - which may not exist - produced no dump
            // and no visible error.
            Debug.LogError($"[SplatmapDiagnostics2] FAILED, no link dump was written: {ex}");
            EditorApplication.Exit(2);
            return;
        }

        EditorApplication.Exit(0);
    }
}
