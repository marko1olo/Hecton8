// ============================================================================
// GraphCleanupPhantomLinks.cs
// Removes "phantom" entries from the authored sandbox biome graph's link table:
// Graph.links keys that belong to ONE hardcoded generator but whose inlet id is
// no longer among that generator's live Inlets(). Then saves that one asset.
//
// Graph.links is Dictionary<IInlet<object>, IOutlet<object>>
// (Assets/MapMagic/Nodes/Graph.cs:23) and is serialized as two parallel arrays
// of the key/value objects themselves (Assets/MapMagic/Nodes/GraphSerializer.cs:
// 50-51, 71-72), so an inlet that was dropped from a generator's layer array
// without being unlinked survives in that table forever.
//
// The class is in the GLOBAL namespace and the entry point is Run(). Batch
// scripts resolve -executeMethod by reflection name, so "GraphCleanupPhantomLinks
// .Run" is a contract even though no .bat in Tools/BatchTasks currently uses it -
// neither the namespace nor the method name may change.
//
// This is a DESTRUCTIVE MUTATOR of authored world data: every entry it removes is
// a link the graph author cannot get back. It runs against a working tree shared
// with a live authoring session.
//
// WHAT THIS FILE USED TO DO WRONG - every one of these exited 0:
//   - No null checks at all. A missing graph NullReferenced on graph.generators; a
//     stale node id made `splatmap` null so `multi` was null and multi.Inlets()
//     NullReferenced; and there was no try/catch anywhere, so the exception
//     surfaced as a Unity error while the tool's own report file was never
//     written and no exit code distinguished it.
//   - Removed 0 links and exited 0 with the message "Removed 0 phantom links."
//     A zero count on a hardcoded node id means a stale id or the wrong graph
//     essentially every time, and it was reported as success.
//   - The only report went to
//     C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\cleanup_result.txt -
//     another agent's private scratch directory, outside the repo, unversioned,
//     invisible to any batchmode log reader.
//   - AssetDatabase.SaveAssets() flushed EVERY dirty asset in the project,
//     committing a concurrent session's unfinished authoring edits as a side
//     effect of this tool.
//   - It would happily delete EVERY link into the target node if Inlets() came
//     back empty - which is what a failed deserialization of the layers array
//     looks like - and call that a successful cleanup.
//   - `kvp.Key?.Gen?.id` skipped, in silence, every link key whose Gen is null.
//     That is not hypothetical: MapMagic.Nodes.Layer declares
//     `Gen { get; private set; }` with no serialized backing field
//     (Assets/MapMagic/Nodes/Generator.cs:216), and GraphSerializer only
//     reassigns Gen on inlets that are still live (GraphSerializer.cs:95-102) -
//     so a phantom of that shape is invisible to the very predicate meant to
//     find it. Those keys are now counted and reported instead of dropped.
//
// EXIT CODES (batchmode only; from the menu item nothing is killed):
//   0  CHANGED       - at least one phantom removed, recount proved none left,
//                      and the one asset was saved.
//   2  FAILED        - exception, a removal that did not take, or a save that did
//                      not flush. The link table is in an UNVERIFIED state.
//   5  ALREADY CLEAN - the node was found, is a multi-inlet, and had zero phantom
//                      links. Nothing was written. Non-zero on purpose: a zero
//                      count against a hardcoded id is far more often a stale id
//                      than a clean graph, and the counts below say which.
//   6  REFUSED       - graph, node id, or the multi-inlet interface was not
//                      there, or removing would have been unsafe. NOTHING was
//                      changed.
//   3 (no GPU) and 4 (timeout) are unreachable here - see Run().
// ============================================================================

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

public static class GraphCleanupPhantomLinks
{
    private const string ToolName = "CleanupPhantomLinks";

    private const string GraphAssetPath =
        "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

    /// <summary>
    /// Per-tool subfolder inside the repo, replacing the foreign
    /// C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-... path this tool shared with several other
    /// diagnostics. A per-tool folder rather than a shared Logs/ root because two tools writing the same
    /// filename into one directory is how diagnostics here have already destroyed each other's evidence.
    /// `static readonly` rather than `const` because <see cref="Path.Combine"/> is not a compile-time
    /// constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "graph_cleanup_phantom_links");

    /// <summary>
    /// Authored node id, left exactly as it was - the original author called this the splatmap node. It is
    /// deliberately not re-derived or "corrected": changing it would point a destructive mutator at a
    /// different node. The runtime type name of whatever it resolves to is printed on every branch so a
    /// stale id is visible rather than assumed.
    /// </summary>
    private const ulong TargetNodeId = 9077949529453494279UL;

    private const int ExitChanged = 0;
    private const int ExitFailed = 2;
    private const int ExitAlreadyClean = 5;
    private const int ExitRefused = 6;

    [MenuItem("Hecton8/Diagnostics/Cleanup Phantom Links")]
    public static void Run()
    {
        // NO GPU REFUSAL HERE, on purpose, and this is a judgement rather than an omission.
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for tools whose output
        // degenerates to zeros without a graphics device - compute dispatches, Graphics.Blit, readbacks,
        // PNG encodes. This tool does none of those: it edits a managed dictionary on a deserialized
        // ScriptableObject and saves it, which is exactly as truthful headless as it is on a GPU. A
        // refusal that fires on a working tool teaches the next reader to ignore refusals, so exit code 3
        // is deliberately unreachable here.
        int exitCode;
        try
        {
            exitCode = Mutate();
        }
        catch (Exception ex)
        {
            // Deliberately NO file write in this catch. A write into a directory that may not exist throws
            // here and replaces the real cause with the reporter's own exception. Debug.LogError reaches
            // the batchmode log, which is the channel that is actually read.
            Debug.LogError(
                $"[{ToolName}] FAILED mid-cleanup: the link table of '{GraphAssetPath}' is in an " +
                "UNVERIFIED state - entries may have been removed in memory without being saved, or saved " +
                $"without being verified. Do not treat this graph as cleaned. {ex}");
            Finish(ExitFailed);
            return;
        }

        Finish(exitCode);
    }

    /// <summary>
    /// Returns the exit code for the outcome it proved. Never calls EditorApplication.Exit itself, so that
    /// every terminal branch is forced through the single reporting point in <see cref="Run"/>.
    /// </summary>
    private static int Mutate()
    {
        Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
        if (graph == null)
        {
            return Refuse(
                $"no MapMagic Graph asset at '{GraphAssetPath}'. If the graph was moved or renamed, this " +
                "tool's hardcoded path is stale.",
                nodesExamined: 0, linksExamined: 0, candidates: 0, removed: 0);
        }

        // Both are [NonSerialized] and rebuilt by the serialization callback (Graph.cs:22-23). Null means
        // the asset deserialized into an empty shell, which is a load failure and not an empty graph.
        Generator[] generators = graph.generators;
        if (generators == null)
        {
            return Refuse(
                $"'{GraphAssetPath}' loaded but graph.generators is null, so the asset did not " +
                "deserialize. Nothing was examined.",
                nodesExamined: 0, linksExamined: 0, candidates: 0, removed: 0);
        }

        Dictionary<IInlet<object>, IOutlet<object>> links = graph.links;
        if (links == null)
        {
            return Refuse(
                $"'{GraphAssetPath}' loaded but graph.links is null, so there is no link table to clean.",
                nodesExamined: generators.Length, linksExamined: 0, candidates: 0, removed: 0);
        }

        int nodesExamined = 0;
        int matched = 0;
        Generator target = null;

        for (int i = 0; i < generators.Length; i++)
        {
            Generator gen = generators[i];
            if (gen == null) continue;

            nodesExamined++;
            if (gen.id != TargetNodeId) continue;

            matched++;
            if (target == null) target = gen;
        }

        if (matched == 0)
        {
            return Refuse(
                $"examined {nodesExamined} node(s) in '{GraphAssetPath}' and NONE carries id " +
                $"{TargetNodeId}. Zero matches on a hardcoded id means a stale id or the wrong graph, " +
                "never a healthy graph.",
                nodesExamined, links.Count, candidates: 0, removed: 0);
        }

        if (matched > 1)
        {
            return Refuse(
                $"{matched} nodes in '{GraphAssetPath}' share id {TargetNodeId} out of {nodesExamined} " +
                "examined. There is no way to know whose links were meant, and this tool deletes links.",
                nodesExamined, links.Count, candidates: 0, removed: 0);
        }

        string targetTypeName = target.GetType().FullName;

        IMultiInlet multi = target as IMultiInlet;
        if (multi == null)
        {
            return Refuse(
                $"node {TargetNodeId} in '{GraphAssetPath}' is a '{targetTypeName}', which does not " +
                "implement IMultiInlet, so it has no inlet set to compare link keys against. This is " +
                "where the old version NullReferenced.",
                nodesExamined, links.Count, candidates: 0, removed: 0);
        }

        // Live inlet ids. Nulls in a layers array are possible and would otherwise add id 0 to the valid
        // set, so they are counted separately rather than folded in.
        HashSet<ulong> validInletIds = new HashSet<ulong>();
        int nullInlets = 0;
        IEnumerable<IInlet<object>> inlets = multi.Inlets();
        if (inlets == null)
        {
            return Refuse(
                $"node {TargetNodeId} ('{targetTypeName}') returned a null Inlets() enumerable, so its " +
                "live inlet set is unknown. Deleting links against an unknown set would be guessing.",
                nodesExamined, links.Count, candidates: 0, removed: 0);
        }

        foreach (IInlet<object> inlet in inlets)
        {
            if (inlet == null) { nullInlets++; continue; }
            validInletIds.Add(inlet.Id);
        }

        // Classify the whole link table before touching it, and count the blind spot instead of hiding it.
        List<IInlet<object>> phantoms = new List<IInlet<object>>();
        int linksExamined = 0;
        int linksToTarget = 0;
        int keysWithNullGen = 0;
        int nullKeys = 0;

        foreach (KeyValuePair<IInlet<object>, IOutlet<object>> kvp in links)
        {
            linksExamined++;

            IInlet<object> key = kvp.Key;
            if (key == null) { nullKeys++; continue; }

            // MapMagic.Nodes.Layer's Gen is a non-serialized auto-property (Generator.cs:216) and
            // GraphSerializer only reassigns Gen on inlets that are still live (GraphSerializer.cs:95-102),
            // so a phantom of that shape arrives with Gen == null and CANNOT be attributed to any
            // generator. The old code silently skipped these. They are reported now, because "0 phantoms
            // found" plus "N unattributable keys" is a completely different situation from "0 phantoms
            // found" plus "0 unattributable keys".
            Generator keyGen = key.Gen;
            if (keyGen == null) { keysWithNullGen++; continue; }

            if (keyGen.id != TargetNodeId) continue;

            linksToTarget++;
            if (!validInletIds.Contains(key.Id))
                phantoms.Add(key);
        }

        string counts =
            $"nodes examined {nodesExamined}, links examined {linksExamined}, live inlets on target " +
            $"{validInletIds.Count} (null inlet slots {nullInlets}), links keyed to target {linksToTarget}, " +
            $"phantom candidates {phantoms.Count}, link keys with a null Gen that could not be attributed " +
            $"to any node {keysWithNullGen}, null link keys {nullKeys}";

        // A node with no live inlets makes EVERY link into it look phantom. That is also exactly what a
        // layers array that failed to deserialize looks like, and the two are indistinguishable from here.
        // The old version would have deleted the lot. Report instead of guessing.
        if (validInletIds.Count == 0 && linksToTarget > 0)
        {
            return Refuse(
                $"node {TargetNodeId} ('{targetTypeName}') reports ZERO live inlets while {linksToTarget} " +
                "link(s) are keyed to it, so every one of them looks phantom. That is also what a layers " +
                "array which failed to deserialize looks like, and this tool cannot tell the two apart. " +
                "Deleting them all could destroy authored wiring irrecoverably, so nothing was removed. " +
                "Inspect the node in the graph editor.",
                nodesExamined, linksExamined, phantoms.Count, removed: 0);
        }

        if (phantoms.Count == 0)
        {
            string cleanMsg =
                $"ALREADY CLEAN: node {TargetNodeId} ('{targetTypeName}') in '{GraphAssetPath}' has no " +
                $"phantom link entries. Removed 0. Nothing was written and no asset was marked dirty. " +
                $"Counts: {counts}. Read those counts before believing the graph was clean - if 'links " +
                "keyed to target' is 0, or 'link keys with a null Gen' is not 0, this run proved nothing " +
                "about the graph and the likely cause is a stale node id or a phantom shape this " +
                "predicate cannot see.";
            Debug.Log($"[{ToolName}] {cleanMsg}");
            TryWriteReport("ALREADY CLEAN", cleanMsg, nodesExamined, linksExamined, 0, 0);
            return ExitAlreadyClean;
        }

        if (phantoms.Count == linksToTarget)
        {
            Debug.LogWarning(
                $"[{ToolName}] every one of the {linksToTarget} link(s) keyed to node {TargetNodeId} " +
                $"('{targetTypeName}') is classified phantom against {validInletIds.Count} live inlet(s). " +
                "That is legitimate only if all of this node's layers were replaced since those links were " +
                "authored. Proceeding, but verify the node's wiring in the graph editor afterwards.");
        }

        int removed = 0;
        for (int i = 0; i < phantoms.Count; i++)
        {
            if (links.Remove(phantoms[i]))
                removed++;
        }

        if (removed != phantoms.Count)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED mid-mutation: {phantoms.Count} phantom key(s) were identified but " +
                $"only {removed} could be removed from graph.links. Nothing was saved and the asset was " +
                $"NOT marked dirty, so the file on disk is untouched - but the in-memory graph is now " +
                $"partially mutated and no longer matches it. Close this editor without saving, or reimport " +
                $"'{GraphAssetPath}', before doing anything else with this graph. Counts: {counts}.");
            TryWriteReport(
                "FAILED (removal did not take)",
                $"Identified {phantoms.Count} phantom key(s), removed {removed}. Counts: {counts}",
                nodesExamined, linksExamined, phantoms.Count, removed);
            return ExitFailed;
        }

        // Recount against the live table rather than trusting the return values above.
        int remaining = 0;
        foreach (KeyValuePair<IInlet<object>, IOutlet<object>> kvp in links)
        {
            IInlet<object> key = kvp.Key;
            if (key == null) continue;
            Generator keyGen = key.Gen;
            if (keyGen == null || keyGen.id != TargetNodeId) continue;
            if (!validInletIds.Contains(key.Id)) remaining++;
        }

        if (remaining != 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: removed {removed} entry/entries but a recount still finds " +
                $"{remaining} phantom link(s) on node {TargetNodeId}, so the table was not cleaned. " +
                $"Nothing was saved and the asset was NOT marked dirty, so the file on disk is untouched - " +
                $"but the in-memory graph is now partially mutated and no longer matches it. Close this " +
                $"editor without saving, or reimport '{GraphAssetPath}', before doing anything else with " +
                $"this graph. Counts: {counts}.");
            TryWriteReport(
                "FAILED (recount still finds phantoms)",
                $"Removed {removed}, {remaining} still present. Counts: {counts}",
                nodesExamined, linksExamined, phantoms.Count, removed);
            return ExitFailed;
        }

        // Deliberately NO Generator.version bump here, unlike the enable-erosion mutator. Graph.UnlinkInlet
        // bumps it (Graph.cs:399) because it removes a LIVE link and the node's product must be recomputed.
        // A phantom key is by definition not in Inlets(), so nothing reads it and no product changes -
        // bumping version would only force a needless regeneration of this output node.
        EditorUtility.SetDirty(graph);

        // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the project,
        // including a concurrent session's unfinished authoring work in this shared tree. Also deliberately
        // no AssetDatabase.Refresh() - the old one triggered a project-wide reimport for one asset edit
        // that SaveAssetIfDirty already persists.
        AssetDatabase.SaveAssetIfDirty(graph);

        // Proof that the write landed rather than that the call was made. If the save was rejected -
        // read-only file, checkout lock - the object stays dirty and exit 0 would be a lie.
        if (EditorUtility.IsDirty(graph))
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: {removed} phantom link(s) were removed in memory but " +
                $"'{GraphAssetPath}' is STILL DIRTY after AssetDatabase.SaveAssetIfDirty, so nothing " +
                "reached disk. The asset is probably read-only or locked, and the in-memory graph no " +
                "longer matches the file.");
            TryWriteReport(
                "FAILED (save did not flush)",
                $"Removed {removed} in memory; SaveAssetIfDirty left the graph dirty. Counts: {counts}",
                nodesExamined, linksExamined, phantoms.Count, removed);
            return ExitFailed;
        }

        string changedMsg =
            $"CHANGED: removed {removed} phantom link(s) from node {TargetNodeId} ('{targetTypeName}') in " +
            $"'{GraphAssetPath}' and saved that one asset; a recount finds 0 remaining. Counts: {counts}.";
        Debug.Log($"[{ToolName}] {changedMsg}");
        TryWriteReport("CHANGED", changedMsg, nodesExamined, linksExamined, phantoms.Count, removed);
        return ExitChanged;
    }

    /// <summary>
    /// Single refusal path: names what was NOT produced in the batchmode log, records it, and returns a
    /// non-zero code. Every caller of this has already established that no link was removed.
    /// </summary>
    private static int Refuse(string detail, int nodesExamined, int linksExamined, int candidates, int removed)
    {
        string msg =
            $"REFUSED: {detail} No link was removed and no asset was marked dirty. Nodes examined " +
            $"{nodesExamined}, links examined {linksExamined}, phantom candidates {candidates}, removed " +
            $"{removed}.";
        Debug.LogError($"[{ToolName}] {msg}");
        TryWriteReport("REFUSED", msg, nodesExamined, linksExamined, candidates, removed);
        return ExitRefused;
    }

    /// <summary>
    /// Writes the outcome artifact on EVERY terminal branch, not only on success, so the file cannot become
    /// a stale success fossil. A failure to write is a warning, never a change of verdict: the exit code
    /// reports the state of the graph, and Debug.LogError/Log above is the authoritative channel. This
    /// never throws out of itself, so it can never destroy a finding the way the old brain-folder write
    /// did.
    /// </summary>
    private static void TryWriteReport(
        string outcome, string detail, int nodesExamined, int linksExamined, int candidates, int removed)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);

            StringBuilder report = new StringBuilder();
            report.AppendLine("# GraphCleanupPhantomLinks");
            report.AppendLine();
            report.AppendLine($"- outcome: {outcome}");
            report.AppendLine($"- graph: {GraphAssetPath}");
            report.AppendLine($"- target node id: {TargetNodeId}");
            report.AppendLine($"- nodes examined: {nodesExamined}");
            report.AppendLine($"- links examined: {linksExamined}");
            report.AppendLine($"- phantom candidates: {candidates}");
            report.AppendLine($"- links removed: {removed}");
            report.AppendLine($"- utc: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            report.AppendLine();
            report.AppendLine(detail);

            File.WriteAllText(Path.Combine(OutputDir, "cleanup_result.md"), report.ToString());
        }
        catch (Exception writeEx)
        {
            Debug.LogWarning(
                $"[{ToolName}] outcome was '{outcome}' but the report could not be written under " +
                $"'{OutputDir}': {writeEx.Message}. The Unity log above is the record for this run.");
        }
    }

    /// <summary>
    /// Carries the outcome as a process exit code in batchmode, and does NOT kill a human's editor when the
    /// same method is reached from the menu item. This tree is shared with a live authoring session, and a
    /// menu click that quits the editor discards that session's unsaved work.
    /// </summary>
    private static void Finish(int exitCode)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
            return;
        }

        if (exitCode != ExitChanged)
            Debug.LogError($"[{ToolName}] would exit {exitCode} in batchmode.");
    }
}
