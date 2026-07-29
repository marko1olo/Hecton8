// ============================================================================
// GraphArchiveImport.cs
// Analyses everything downstream of ONE hardcoded node of the authored sandbox
// biome graph, and - only when explicitly told to - removes that node.
//
// WHAT IT IMPORTS, AND FROM WHERE: nothing, from nowhere. The file name reads
// like an importer and it is not one. "Import200" is a MapMagic NODE TYPE -
// MapMagic.Nodes.MatrixGenerators.Import200, Assets/MapMagic/Generators/Matrix/
// Runtime/MatrixInitial.cs:325 - a "Map/Initial -> Import" generator that reads
// a MatrixAsset already inside the project. This tool's only input is the graph
// asset at GraphAssetPath, which is present in the repo (329 KB on disk). The
// foreign C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-... path this file
// used to carry was never an input: it was the OUTPUT directory for the report
// and the crash dump. So "the input is unreachable" is NOT the defect here -
// the defect is that every artifact this tool produced landed in another
// agent's private scratch folder, outside the repo and outside Logs/.
//
// It is a MUTATOR of authored world data. Removing a generator changes what the
// graph produces everywhere it generates, so "I removed it", "I found nothing to
// remove" and "I refuse to remove it" cannot share an exit code.
//
// WHAT THIS FILE USED TO DO WRONG:
//   - IT ALREADY DESTROYED AUTHORED GRAPH STATE ONCE, ON A FALSE VERDICT. Its
//     own surviving artifact (archive_import_result.md in that brain folder,
//     written 2026-07-19 20:05) records the run: 28 nodes walked, the last six
//     of them MatrixPortalEnter, "[RESULT] No critical consumers found. The
//     chain is DEAD", "Import200 successfully removed from graph."
//   - THE WALK CANNOT CROSS A PORTAL, which is why that verdict was false. It
//     enumerated graph.links only. A portal's edge is NOT in graph.links:
//     PortalExit<T> stores its source as a ulong `enterId` and resolves it with
//     graph.GetGeneratorById on deserialize (Assets/MapMagic/Nodes/Portals.cs:60,
//     70, 73-84). So the walk terminated ON the six MatrixPortalEnter nodes and
//     called everything past them absent. The hop it needed is
//     ICustomDependence.PriorGens (Assets/MapMagic/Nodes/Generator.cs:139-144),
//     which PortalExit implements for exactly this purpose (Portals.cs:115-119).
//   - THE CRITICAL-CONSUMER TEST RECOGNISED 2 OF 10+ OUTPUT TYPES. It tested for
//     HeightOutput200 and TexturesOutput200 plus a substring match on "Biome" and
//     "Splatmap". Every output node derives from MapMagic.Nodes.OutputGenerator
//     (Generator.cs:100-107), and GrassOutput200, HolesOutput2112, ObjectsOutput,
//     TreesOutput, SplineOutput200, CustomShaderOutput200,
//     DirectTexturesOutput200, DirectMatricesOutput200 and MicroSplatOutput200
//     all matched none of its four patterns.
//   - IT COULD NOT SEE INTO A SUB-GRAPH EITHER. Function210 and Loop210 wire
//     their interiors through GetInternalPortal(subGraph)
//     (Assets/MapMagic/Generators/Biomes/Runtime/Function.cs:77,91, Loop.cs:84,
//     102) and IBiome exposes a whole SubGraph (Generator.cs:131-137). None of
//     that is in the parent graph's links dictionary.
//   - A NULL Gen ON EITHER END OF A LINK WAS SILENTLY SKIPPED (`kvp.Key?.Gen`
//     plus `target != null`), so an edge it could not read counted as an edge
//     that did not exist - in a tool whose whole output is "nothing consumes
//     this".
//   - "Import200 not found in graph. It may already be removed." was written to
//     a file and then `return`ed into EditorApplication.Exit(0). A hardcoded id
//     matching zero nodes means a stale id or the wrong graph just as easily as
//     an already-finished job, and it reported success for all three.
//   - The catch wrote ex.ToString() into that same foreign directory BEFORE
//     Debug.LogError. If the directory is absent the File.WriteAllText throws
//     inside the catch, so the real cause is destroyed and never reaches the
//     batchmode log - the only channel anyone here reads.
//   - AssetDatabase.SaveAssets() flushed EVERY dirty asset in the project. A
//     concurrent authoring session shares this working tree, so its unfinished
//     edits were committed to disk as a side effect. AssetDatabase.Refresh()
//     then triggered a project-wide reimport for a one-node edit.
//   - The removal was hand-rolled (rebuild generators[], delete link keys) and
//     therefore skipped what Graph.Remove does: exposed.RemoveUnused, and the
//     version++ on every neighbour of the removed node
//     (Assets/MapMagic/Nodes/Graph.cs:112-122, 416-445). Without those bumps
//     Graph.IdsVersions (Graph.cs:654) is unchanged, so functions and clusters
//     caching on that value keep serving their pre-removal product: the asset
//     says the node is gone and the generated world disagrees.
//   - Nothing was counted. Not nodes examined, not ids matched, not consumers,
//     not links removed.
//
// WHY REMOVAL IS NOW OPT-IN. Deleting a generator from this graph moves shipped
// world geometry, and a reachability walk over a MapMagic graph cannot prove a
// chain is dead: portals, function sub-graphs and IBiome sub-graphs are three
// separate boundaries, and only the first is crossable from here. A heuristic
// verdict may report; it may not delete unattended. So the analysis always runs
// and the removal requires -hectonRemoveImport200 on the command line. No file
// under Tools/BatchTasks references this type at all today, so nothing that runs
// unattended can trip it.
//
// Reached from the menu item below by a human, or by reflection on
// "Hecton8.Editor.Diagnostics.GraphArchiveImport.Run". The namespace, the type
// name, Run() and the menu path are a caller contract - do not rename any of them.
//
// EXIT CODES (batchmode only; from the menu item nothing is killed):
//   0  CHANGED        - the node was removed, every link to it is gone, and the
//                       one asset was flushed to disk. Requires both a clean
//                       verdict and -hectonRemoveImport200.
//   2  FAILED         - exception, the removal did not take, or the save did not
//                       flush. Graph state is UNVERIFIED.
//   5  ANALYSED ONLY  - the chain report was produced and NOTHING was removed,
//                       because -hectonRemoveImport200 was not passed. Non-zero
//                       on purpose: this run proved no mutation.
//   6  REFUSED        - the graph, the generators array, or the node id was not
//                       there; or the id is ambiguous; or the id points at a
//                       non-Import node; or the chain is live / not provably
//                       dead. NOTHING was changed. This is the branch that used
//                       to exit 0 in silence.
//   3 (no GPU) and 4 (timeout) are unreachable here - see Run().
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

namespace Hecton8.Editor.Diagnostics
{
    public static class GraphArchiveImport
    {
        private const string ToolName = "ArchiveImport200";

        private const string GraphAssetPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        /// <summary>
        /// Per-tool subfolder inside the repo, replacing the foreign
        /// C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-... path. Per-tool rather than a shared Logs/
        /// root because several diagnostics here write generically named files and two of them have already
        /// overwritten each other's evidence inside one directory. <c>static readonly</c> rather than
        /// <c>const</c> because <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
        /// </summary>
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "graph_archive_import");

        /// <summary>
        /// Authored node id, left exactly as it was. Changing it would point this mutator at a different
        /// node and move shipped world geometry; if it is stale the tool now says so and refuses.
        /// </summary>
        private const ulong Import200Id = 12389088114570690561UL;

        /// <summary>
        /// Without this on the command line the tool analyses and writes its report but removes nothing.
        /// </summary>
        private const string RemoveFlag = "-hectonRemoveImport200";

        private const int ExitChanged = 0;
        private const int ExitFailed = 2;
        private const int ExitAnalysedOnly = 5;
        private const int ExitRefused = 6;

        [MenuItem("Hecton8/Diagnostics/Archive Import200")]
        public static void Run()
        {
            // NO GPU REFUSAL HERE, on purpose, and this is a judgement rather than an omission.
            // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for tools whose
            // output degenerates to zeros without a graphics device - compute dispatches, Graphics.Blit,
            // readbacks, PNG encodes. This tool does none of those: it walks managed references on a
            // deserialized ScriptableObject, writes a text file, and at most removes one array element and
            // saves the asset. It never calls StartGenerate, so no matrix is ever produced. It is exactly as
            // truthful headless as it is on a GPU, and a refusal that fires on a working tool teaches the
            // next reader to ignore refusals. Exit code 3 is deliberately unreachable.
            int exitCode;
            try
            {
                exitCode = AnalyseAndMaybeRemove();
            }
            catch (Exception ex)
            {
                // Deliberately NO file write in this catch. The old one wrote into a directory this tool
                // never created, which throws here and replaces the real cause with the reporter's own
                // DirectoryNotFoundException. Debug.LogError reaches the batchmode log, which is the channel
                // that is actually read.
                Debug.LogError(
                    $"[{ToolName}] FAILED: no chain analysis of node {Import200Id} was produced under " +
                    $"'{OutputDir}', and '{GraphAssetPath}' is in an UNVERIFIED state - the node may have " +
                    $"been unlinked or removed in memory without being saved, or saved without the " +
                    $"neighbour version bumps. Reload the asset before trusting it, and do not treat any " +
                    $"terrain capture from this run as evidence. {ex}");
                Finish(ExitFailed);
                return;
            }

            Finish(exitCode);
        }

        /// <summary>
        /// Returns the exit code for the outcome it proved. Never calls EditorApplication.Exit itself, so
        /// every terminal branch is forced through the single reporting point in <see cref="Run"/>.
        /// </summary>
        private static int AnalyseAndMaybeRemove()
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
            if (graph == null)
            {
                return Refuse(
                    $"no MapMagic Graph asset at '{GraphAssetPath}'. If the graph was moved or renamed, " +
                    "this tool's hardcoded path is stale.",
                    examined: 0, matched: 0, chain: null);
            }

            // Graph.generators is [NonSerialized] and repopulated by the serialization callback
            // (Assets/MapMagic/Nodes/Graph.cs:22). A null array means the asset deserialized into an empty
            // shell - a load failure, not an empty graph.
            Generator[] generators = graph.generators;
            if (generators == null)
            {
                return Refuse(
                    $"'{GraphAssetPath}' loaded but graph.generators is null, so the asset did not " +
                    "deserialize. Nothing was examined.",
                    examined: 0, matched: 0, chain: null);
            }

            if (graph.links == null)
            {
                return Refuse(
                    $"'{GraphAssetPath}' loaded but graph.links is null, so not one edge could be read and " +
                    "a walk over it would report every node as unconnected. Nothing was examined.",
                    examined: 0, matched: 0, chain: null);
            }

            // Manual loop rather than LINQ FirstOrDefault so that "how many nodes did you look at" is a
            // reported number instead of an assumption, and so a duplicate id is detected rather than
            // silently resolved to whichever came first.
            int examined = 0;
            int matched = 0;
            Generator importNode = null;

            for (int i = 0; i < generators.Length; i++)
            {
                Generator gen = generators[i];
                if (gen == null) continue;

                examined++;
                if (gen.id != Import200Id) continue;

                matched++;
                if (importNode == null) importNode = gen;
            }

            if (matched == 0)
            {
                return Refuse(
                    $"examined {examined} node(s) in '{GraphAssetPath}' and NONE carries id {Import200Id}. " +
                    "Zero matches on a hardcoded id is indistinguishable from a stale id or the wrong " +
                    "graph, so it is not reported as success. Note that this tool's own artifact from " +
                    "2026-07-19 20:05 claims it already removed this node on a verdict its walk could not " +
                    "support (see the header), so an absent node here may be that damage rather than a " +
                    "finished job. Nothing was changed.",
                    examined, matched, chain: null);
            }

            if (matched > 1)
            {
                return Refuse(
                    $"{matched} nodes in '{GraphAssetPath}' share id {Import200Id} out of {examined} " +
                    "examined. There is no way to know which one was meant, and removing the wrong one " +
                    "moves terrain. Nothing was changed.",
                    examined, matched, chain: null);
            }

            // Identity assertion by runtime type name. A typeof test would need `using
            // MapMagic.Nodes.MatrixGenerators`, and MapMagic ships more than one node type whose name
            // contains "Import"; this tool should accept any of them and reject anything else. IndexOf
            // rather than string.Contains(string, StringComparison) so no netstandard2.1-only overload is
            // required.
            string targetTypeName = importNode.GetType().Name;
            if (targetTypeName.IndexOf("Import", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return Refuse(
                    $"node {Import200Id} exists in '{GraphAssetPath}' but it is a " +
                    $"'{importNode.GetType().FullName}', not an Import node. The hardcoded id now points at " +
                    "a different node, and removing it would change authored terrain in a way nobody asked " +
                    "for. Nothing was changed.",
                    examined, matched, chain: null);
            }

            ChainReport chain = WalkDownstream(graph, generators, importNode);

            // The headline numbers go into the Unity log as well as the report. The old tool's findings all
            // lived in a directory nobody reading the batchmode log would ever open.
            Debug.Log(
                $"[{ToolName}] Walked the chain downstream of {targetTypeName} node {Import200Id} in " +
                $"'{GraphAssetPath}': {examined} node(s) examined, {chain.Reached} reached downstream, " +
                $"{chain.LinkEdges} link edge(s) followed, {chain.PortalHops} portal/PriorGens hop(s) " +
                $"crossed, {chain.OutputConsumers} OutputGenerator(s), {chain.RelevantConsumers} " +
                $"IRelevant node(s), {chain.SubGraphBoundaries} sub-graph boundary node(s), " +
                $"{chain.UnreadableEdges} unreadable edge(s).");

            if (chain.Blockers.Count > 0)
            {
                // The old code's inverse of this branch is what deleted the node. Anything that makes the
                // chain live, OR anything that makes "dead" unprovable, lands here - they are not
                // distinguishable in consequence, only in wording, and both mean do not delete.
                return Refuse(
                    $"the chain downstream of {targetTypeName} node {Import200Id} is NOT provably dead. " +
                    $"{chain.Blockers.Count} blocker(s), first: {chain.Blockers[0]}",
                    examined, matched, chain);
            }

            if (!HasRemoveFlag())
            {
                string analysedMsg =
                    $"ANALYSED ONLY: no live consumer was reached from {targetTypeName} node " +
                    $"{Import200Id} in '{GraphAssetPath}' and the walk hit no boundary it could not cross. " +
                    $"Examined {examined} node(s), matched {matched}, reached {chain.Reached} downstream, " +
                    $"removed 0. NOTHING was changed and no asset was marked dirty. Removing a generator " +
                    $"moves shipped world geometry, and a reachability walk is evidence rather than proof, " +
                    $"so this tool does not delete on its own verdict: re-run with {RemoveFlag} to remove " +
                    "the node. Read the per-node lines in the report first.";
                Debug.Log($"[{ToolName}] {analysedMsg}");
                TryWriteReport("ANALYSED ONLY", analysedMsg, examined, matched, removedNodes: 0, chain: chain);
                return ExitAnalysedOnly;
            }

            // ---- Mutation. Only reached with a clean verdict AND an explicit opt-in. ----

            int linksToImportBefore = CountLinksTouching(graph, importNode);
            int generatorsBefore = generators.Length;

            // Graph.Remove (Assets/MapMagic/Nodes/Graph.cs:112-122) instead of hand-rolled array and
            // dictionary surgery: it calls UnlinkGenerator (Graph.cs:416-445), which also increments
            // version on the removed node and on every neighbour whose link it drops, and it calls
            // exposed.RemoveUnused. Both were missing before, and without the version bumps
            // Graph.IdsVersions (Graph.cs:654) never changes, so anything caching on it keeps serving the
            // pre-removal product.
            graph.Remove(importNode);

            Generator[] after = graph.generators;
            if (after == null)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: graph.generators read as null immediately after Graph.Remove on " +
                    $"node {Import200Id}. '{GraphAssetPath}' is UNVERIFIED and was NOT saved. Reload the " +
                    "asset from disk before touching it again.");
                TryWriteReport("FAILED (generators null after Remove)",
                    "Graph.Remove left graph.generators null; the asset was not saved.",
                    examined, matched, removedNodes: 0, chain: chain);
                return ExitFailed;
            }

            bool stillPresent = false;
            for (int i = 0; i < after.Length; i++)
            {
                if (after[i] == importNode) { stillPresent = true; break; }
            }

            int linksToImportAfter = CountLinksTouching(graph, importNode);

            // Proof that the edit landed in memory, not that the call was made.
            if (stillPresent || after.Length != generatorsBefore - 1 || linksToImportAfter != 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: Graph.Remove did not take on node {Import200Id}. Still in " +
                    $"generators: {stillPresent}. Generator count {generatorsBefore} -> {after.Length} " +
                    $"(expected {generatorsBefore - 1}). Links still touching it: {linksToImportAfter} " +
                    $"(was {linksToImportBefore}). '{GraphAssetPath}' was NOT saved but its in-memory state " +
                    "is UNVERIFIED - reload the asset from disk before touching it again.");
                TryWriteReport("FAILED (removal did not take)",
                    $"Graph.Remove left the node present={stillPresent}, count {generatorsBefore} -> " +
                    $"{after.Length}, dangling links {linksToImportAfter}. Asset not saved.",
                    examined, matched, removedNodes: 0, chain: chain);
                return ExitFailed;
            }

            EditorUtility.SetDirty(graph);

            // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the project,
            // including a concurrent session's unfinished authoring work in this shared tree. Also
            // deliberately no AssetDatabase.Refresh() - the old one triggered a project-wide reimport for a
            // single-asset edit that SaveAssetIfDirty already persists.
            AssetDatabase.SaveAssetIfDirty(graph);

            if (EditorUtility.IsDirty(graph))
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: {targetTypeName} node {Import200Id} was removed in memory but " +
                    $"'{GraphAssetPath}' is STILL DIRTY after AssetDatabase.SaveAssetIfDirty, so nothing " +
                    "reached disk. The asset is probably read-only or locked. The in-memory graph no longer " +
                    "matches the file - reload it rather than saving from anywhere else.");
                TryWriteReport("FAILED (save did not flush)",
                    "SaveAssetIfDirty left the graph dirty; the removal did not reach disk.",
                    examined, matched, removedNodes: 0, chain: chain);
                return ExitFailed;
            }

            string changedMsg =
                $"CHANGED: {targetTypeName} node {Import200Id} REMOVED from '{GraphAssetPath}' together " +
                $"with {linksToImportBefore} link(s) that touched it. Generator count {generatorsBefore} " +
                $"-> {after.Length}. Examined {examined} node(s), matched {matched}, reached " +
                $"{chain.Reached} downstream, removed 1. This alters what the graph generates - regenerate " +
                "before judging any height, splat or biome evidence, and note that the DEAD verdict behind " +
                "this removal is a reachability result, not a proof.";
            Debug.Log($"[{ToolName}] {changedMsg}");
            TryWriteReport("CHANGED", changedMsg, examined, matched, removedNodes: 1, chain: chain);
            return ExitChanged;
        }

        /// <summary>
        /// Everything the walk found, and everything it could not read. Kept as one object so the report and
        /// the verdict cannot disagree about the numbers.
        /// </summary>
        private sealed class ChainReport
        {
            public int Reached;
            public int LinkEdges;
            public int PortalHops;
            public int OutputConsumers;
            public int RelevantConsumers;
            public int SubGraphBoundaries;
            public int UnreadableEdges;
            public int UnresolvedDependence;

            /// <summary>Any one of these means "do not remove this node".</summary>
            public readonly List<string> Blockers = new List<string>();

            /// <summary>Observations that are NOT blockers, kept out of Blockers so the verdict is not
            /// poisoned by facts about unrelated nodes.</summary>
            public readonly List<string> Notes = new List<string>();

            public readonly List<string> Nodes = new List<string>();
        }

        /// <summary>
        /// Breadth-first walk over everything the node feeds. Two edge kinds, because one alone is a lie:
        /// graph.links for ordinary wiring, and the reverse of ICustomDependence.PriorGens
        /// (Assets/MapMagic/Nodes/Generator.cs:139-144) for the portal hop the old walk could not make.
        /// Anything it cannot read, and any boundary it cannot cross, is recorded as a blocker instead of
        /// being treated as the absence of a consumer.
        /// </summary>
        private static ChainReport WalkDownstream(Graph graph, Generator[] generators, Generator importNode)
        {
            ChainReport report = new ChainReport();

            // Reverse PriorGens index: prior -> the nodes that declare it as a prior. For a MatrixPortalExit
            // the single prior is its MatrixPortalEnter (Portals.cs:115-119), which is exactly the edge the
            // old walk was missing when it stopped on six MatrixPortalEnter nodes and called the chain dead.
            Dictionary<Generator, List<Generator>> dependents = new Dictionary<Generator, List<Generator>>();

            for (int i = 0; i < generators.Length; i++)
            {
                Generator gen = generators[i];
                if (gen == null) continue;
                if (!(gen is ICustomDependence dependence)) continue;

                IEnumerable<Generator> priors;
                try
                {
                    priors = dependence.PriorGens();
                }
                catch (Exception ex)
                {
                    // A throw here used to be impossible to notice, and it hides an inbound edge - the
                    // single most misleading thing for a tool whose output is "nothing consumes this".
                    report.UnreadableEdges++;
                    report.Blockers.Add(
                        $"node {gen.id} ('{gen.GetType().FullName}') threw enumerating " +
                        $"ICustomDependence.PriorGens ({ex.GetType().Name}: {ex.Message}), so at least one " +
                        "inbound edge is invisible to this walk");
                    continue;
                }

                if (priors == null)
                {
                    report.UnreadableEdges++;
                    report.Blockers.Add(
                        $"node {gen.id} ('{gen.GetType().FullName}') implements ICustomDependence but " +
                        "PriorGens returned null, so its source is unknown");
                    continue;
                }

                bool anyPrior = false;
                foreach (Generator prior in priors)
                {
                    if (prior == null) continue;
                    anyPrior = true;

                    if (!dependents.TryGetValue(prior, out List<Generator> list))
                    {
                        list = new List<Generator>();
                        dependents[prior] = list;
                    }
                    list.Add(gen);
                }

                if (!anyPrior)
                {
                    // A NOTE, deliberately not a blocker. PortalExit.enter is [NonSerialized] and is
                    // rebuilt from enterId by OnAfterDeserialize -> RefreshEnter (Portals.cs:57, 70, 73-84),
                    // which has therefore already run by the time AssetDatabase handed us this graph. So an
                    // empty PriorGens means enterId was 0 or resolved to nothing - there is no inbound edge
                    // from any node currently in this graph, and it cannot be hiding a consumer of the chain
                    // under test. Treating it as a blocker would refuse forever over unrelated nodes, which
                    // is how a refusal stops being read.
                    report.UnresolvedDependence++;
                    report.Notes.Add(
                        $"node {gen.id} ('{gen.GetType().FullName}') implements ICustomDependence but " +
                        "declares no prior generator - an unwired portal exit, or one whose enterId resolved " +
                        "to nothing. Not treated as a hidden edge: RefreshEnter already ran on deserialize.");
                }
            }

            // Forward wiring index, built in ONE pass: producing node -> consuming nodes. The old code
            // re-scanned the whole links dictionary inside the BFS loop, which is also why a single
            // unreadable edge had to be reported once per visited node to be reported at all.
            Dictionary<Generator, List<Generator>> consumers = new Dictionary<Generator, List<Generator>>();

            foreach (KeyValuePair<IInlet<object>, IOutlet<object>> kvp in graph.links)
            {
                IInlet<object> inlet = kvp.Key;
                IOutlet<object> outlet = kvp.Value;

                if (inlet == null || outlet == null)
                {
                    report.UnreadableEdges++;
                    report.Blockers.Add(
                        "graph.links contains an entry with a null inlet or outlet, so at least one edge " +
                        "cannot be attributed to a node and a consumer of this chain may be invisible");
                    continue;
                }

                Generator source = outlet.Gen;
                if (source == null)
                {
                    report.UnreadableEdges++;
                    report.Blockers.Add(
                        $"a {outlet.GetType().FullName} in graph.links has a null Gen, so the node it " +
                        "produces for cannot be identified and an edge out of this chain may be hidden");
                    continue;
                }

                Generator target = inlet.Gen;
                if (target == null)
                {
                    // The old code did `Generator target = kvp.Key?.Gen; if (target != null && ...)`, which
                    // dropped exactly this case: a consumer that cannot be named counted as no consumer.
                    report.UnreadableEdges++;
                    report.Blockers.Add(
                        $"a {inlet.GetType().FullName} in graph.links has a null Gen, so the node fed by " +
                        $"{source.id} ('{source.GetType().Name}') cannot be identified");
                    continue;
                }

                if (!consumers.TryGetValue(source, out List<Generator> targets))
                {
                    targets = new List<Generator>();
                    consumers[source] = targets;
                }
                targets.Add(target);
            }

            HashSet<Generator> visited = new HashSet<Generator>();
            Queue<Generator> queue = new Queue<Generator>();
            queue.Enqueue(importNode);
            visited.Add(importNode);

            while (queue.Count > 0)
            {
                Generator current = queue.Dequeue();

                report.Nodes.Add($"{current.GetType().Name} id={current.id}");

                if (current != importNode)
                {
                    report.Reached++;
                    ClassifyConsumer(current, report);
                }

                // Ordinary wiring: current is the producing end of the edge.
                if (consumers.TryGetValue(current, out List<Generator> viaLink))
                {
                    for (int i = 0; i < viaLink.Count; i++)
                    {
                        report.LinkEdges++;
                        if (visited.Add(viaLink[i])) queue.Enqueue(viaLink[i]);
                    }
                }

                // The portal hop.
                if (dependents.TryGetValue(current, out List<Generator> viaPrior))
                {
                    for (int i = 0; i < viaPrior.Count; i++)
                    {
                        report.PortalHops++;
                        if (visited.Add(viaPrior[i])) queue.Enqueue(viaPrior[i]);
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Decides what one reached node means for the verdict. Interface tests, not a type-name list: the
        /// old two-type-plus-substring test missed eight shipped OutputGenerator subclasses.
        /// </summary>
        private static void ClassifyConsumer(Generator node, ChainReport report)
        {
            Type t = node.GetType();

            if (node is OutputGenerator)
            {
                report.OutputConsumers++;
                report.Blockers.Add(
                    $"node {node.id} ('{t.FullName}') is a MapMagic.Nodes.OutputGenerator " +
                    "(Generator.cs:100-107) - it applies to the terrain, so this chain is LIVE");
                return;
            }

            if (node is IBiome)
            {
                report.SubGraphBoundaries++;
                report.Blockers.Add(
                    $"node {node.id} ('{t.FullName}') is an IBiome and owns a SubGraph " +
                    "(Generator.cs:131-137). Its interior is wired through GetInternalPortal, not through " +
                    "this graph's links, so nothing past it is visible to this walk");
                return;
            }

            if (node is IFnEnter<object> || node is IFnExit<object>)
            {
                report.SubGraphBoundaries++;
                report.Blockers.Add(
                    $"node {node.id} ('{t.FullName}') is a function portal " +
                    "(Assets/MapMagic/Generators/Biomes/Runtime/FunctionPortals.cs:36,46). The function's " +
                    "interior is resolved against a sub-graph, so its consumers are outside this walk");
                return;
            }

            if (node is IRelevant)
            {
                report.RelevantConsumers++;
                report.Blockers.Add(
                    $"node {node.id} ('{t.FullName}') is IRelevant - it is generated whenever the graph " +
                    "generates (Generator.cs:146-147), so the chain feeding it is not dead");
            }
        }

        /// <summary>
        /// How many links have the node on either end. Reported before and after the removal so "the links
        /// were cleaned up" is a number rather than a claim.
        /// </summary>
        private static int CountLinksTouching(Graph graph, Generator node)
        {
            int count = 0;

            foreach (KeyValuePair<IInlet<object>, IOutlet<object>> kvp in graph.links)
            {
                IInlet<object> inlet = kvp.Key;
                IOutlet<object> outlet = kvp.Value;

                if (inlet != null && inlet.Gen == node) { count++; continue; }
                if (outlet != null && outlet.Gen == node) count++;
            }

            return count;
        }

        /// <summary>
        /// Fully qualified System.Environment: this project has its own Hecton8.Environment namespace, which
        /// wins over System inside a Hecton8.* namespace and resolves to something with no
        /// GetCommandLineArgs. Same trap caught H8_HeadlessPlayModeProbe and H8_ShaderCompileGate.
        /// </summary>
        private static bool HasRemoveFlag()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            if (args == null) return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], RemoveFlag, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Single refusal path: names what was NOT produced in the batchmode log, records it, and returns a
        /// non-zero code. Every caller of this has already established that the graph was not touched.
        /// </summary>
        private static int Refuse(string detail, int examined, int matched, ChainReport chain)
        {
            string msg =
                $"REFUSED: {detail} Node {Import200Id} was NOT removed and no asset was marked dirty. " +
                $"Examined {examined} node(s), matched {matched}, removed 0.";
            Debug.LogError($"[{ToolName}] {msg}");
            TryWriteReport("REFUSED", msg, examined, matched, removedNodes: 0, chain: chain);
            return ExitRefused;
        }

        /// <summary>
        /// Writes the outcome artifact on EVERY terminal branch, not only on success, so the file cannot
        /// become a stale success fossil the way the 2026-07-19 one in the brain folder did. A failure to
        /// write is a warning, never a change of verdict: the exit code reports the state of the graph and
        /// Debug.LogError/Log above is the authoritative channel. This never throws out of itself, so it can
        /// never destroy a finding the way the old foreign-directory write could.
        /// </summary>
        private static void TryWriteReport(string outcome, string detail, int examined, int matched,
                                          int removedNodes, ChainReport chain)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);

                StringBuilder report = new StringBuilder();
                report.AppendLine("# GraphArchiveImport - Import200 chain analysis");
                report.AppendLine();
                report.AppendLine($"- outcome: {outcome}");
                report.AppendLine($"- graph: {GraphAssetPath}");
                report.AppendLine($"- target node id: {Import200Id}");
                report.AppendLine($"- nodes examined: {examined}");
                report.AppendLine($"- nodes matching the target id: {matched}");
                report.AppendLine($"- nodes removed: {removedNodes}");
                report.AppendLine($"- removal opt-in ({RemoveFlag}) present: {HasRemoveFlag()}");
                report.AppendLine($"- utc: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");

                if (chain == null)
                {
                    report.AppendLine("- chain walk: NOT RUN (refused before the walk)");
                }
                else
                {
                    report.AppendLine($"- nodes reached downstream: {chain.Reached}");
                    report.AppendLine($"- link edges followed: {chain.LinkEdges}");
                    report.AppendLine($"- portal / PriorGens hops crossed: {chain.PortalHops}");
                    report.AppendLine($"- OutputGenerator consumers: {chain.OutputConsumers}");
                    report.AppendLine($"- IRelevant consumers: {chain.RelevantConsumers}");
                    report.AppendLine($"- sub-graph boundary nodes: {chain.SubGraphBoundaries}");
                    report.AppendLine($"- unreadable edges: {chain.UnreadableEdges}");
                    report.AppendLine(
                        $"- ICustomDependence nodes with no prior: {chain.UnresolvedDependence}");
                }

                report.AppendLine();
                report.AppendLine(detail);

                if (chain != null)
                {
                    report.AppendLine();
                    if (chain.Blockers.Count == 0)
                    {
                        report.AppendLine(
                            "## Verdict: NO LIVE CONSUMER REACHED, and no boundary blocked the walk.");
                        report.AppendLine();
                        report.AppendLine(
                            "This is a reachability result over graph.links plus ICustomDependence.PriorGens " +
                            "- it is evidence, not proof. It cannot see inside an IBiome or function " +
                            "sub-graph, and it says nothing about references from outside this graph.");
                    }
                    else
                    {
                        report.AppendLine(
                            $"## Verdict: NOT PROVABLY DEAD - {chain.Blockers.Count} blocker(s). Do not " +
                            "remove this node on the strength of this report:");
                        report.AppendLine();
                        for (int i = 0; i < chain.Blockers.Count; i++)
                            report.AppendLine($"- {chain.Blockers[i]}");
                    }

                    if (chain.Notes.Count > 0)
                    {
                        report.AppendLine();
                        report.AppendLine(
                            $"## Notes ({chain.Notes.Count}) - observations, NOT reasons to refuse");
                        report.AppendLine();
                        for (int i = 0; i < chain.Notes.Count; i++)
                            report.AppendLine($"- {chain.Notes[i]}");
                    }

                    report.AppendLine();
                    report.AppendLine("## Chain, in visit order (first entry is the target node itself)");
                    report.AppendLine();
                    for (int i = 0; i < chain.Nodes.Count; i++)
                        report.AppendLine($"- {chain.Nodes[i]}");
                }

                File.WriteAllText(Path.Combine(OutputDir, "archive_import_result.md"),
                    report.ToString(), Encoding.UTF8);
            }
            catch (Exception writeEx)
            {
                Debug.LogWarning(
                    $"[{ToolName}] outcome was '{outcome}' but the report could not be written under " +
                    $"'{OutputDir}': {writeEx.Message}. The Unity log above is the record for this run.");
            }
        }

        /// <summary>
        /// Carries the outcome as a process exit code in batchmode, and does NOT kill a human's editor when
        /// the same method is reached from the menu item. This tree is shared with a live authoring session,
        /// and a menu click that quits the editor discards that session's unsaved work.
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
}
