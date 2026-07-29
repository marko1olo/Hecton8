// ============================================================================
// GraphEnableErosion.cs
// Sets Generator.enabled = true on ONE hardcoded node of the authored sandbox
// biome graph, then saves that one asset.
//
// Reached from Tools/BatchTasks/run_enable_erosion.bat, which resolves
// "Hecton8.Editor.Diagnostics.GraphEnableErosion.Run" by reflection. The
// namespace, the type name and Run() are that batch file's contract - do not
// rename any of the three.
//
// This is a MUTATOR of authored world data. Enabling the erosion node changes
// terrain shape everywhere this graph generates, so a stale node id must be
// loud, not silent: "I could not find what I was told to change" and "I changed
// it" cannot share an exit code.
//
// WHAT THIS FILE USED TO DO WRONG - all five of which reported success:
//   - Both failure paths threw a bare Exception ("Graph not found", "Erosion not
//     found"), and the catch reported it by writing into
//     C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\, another agent's
//     private scratch directory. If that directory does not exist the
//     File.WriteAllText throws INSIDE the catch block, so the real cause is
//     destroyed and replaced by the reporter's own DirectoryNotFoundException.
//   - The success artifact went to that same foreign folder, so neither outcome
//     was visible to a Unity log reader or to any audit of this repo. Batchmode
//     logs are the only channel anyone actually reads here.
//   - AssetDatabase.SaveAssets() flushed EVERY dirty asset in the project. A
//     concurrent authoring session shares this working tree, so its unfinished
//     edits got committed to disk as a side effect of this tool.
//   - Nothing was counted or compared. `enabled = true` was written blind, and
//     the log line "enabled is now: True" was byte-identical for a real change
//     and for a node that was already on.
//   - Generator.version was never incremented. That is exactly what the
//     authoring GUI does when a node is enabled
//     (Assets/MapMagic/Nodes/Editor/GraphEditorActions.cs:76,
//     `if (sgen.enabled) sgen.version++;`). Without it Graph.IdsVersions()
//     (Assets/MapMagic/Nodes/Graph.cs:654-664) is unchanged, so functions and
//     clusters that cache on that value keep serving their pre-erosion result -
//     the asset says erosion is on and the generated world disagrees.
//
// EXIT CODES (batchmode only; from the menu item nothing is killed):
//   0  CHANGED          - the flag was flipped off->on and the one asset saved.
//   2  FAILED           - exception, or the save did not flush. Graph state is
//                         unverified.
//   5  ALREADY CORRECT  - the node was found and was already enabled. Nothing
//                         was written and no asset was marked dirty. Non-zero on
//                         purpose: this run did not prove a mutation.
//   6  REFUSED          - the graph, or the node id, or an erosion-shaped node,
//                         was not there. NOTHING was changed. This is the branch
//                         that used to exit 0 in silence.
//   3 (no GPU) and 4 (timeout) are unreachable here - see Run().
// ============================================================================

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;

namespace Hecton8.Editor.Diagnostics
{
    public static class GraphEnableErosion
    {
        private const string ToolName = "EnableErosion";

        private const string GraphAssetPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        /// <summary>
        /// Per-tool subfolder inside the repo, replacing the foreign
        /// C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-... path. A per-tool folder rather than a
        /// shared Logs/ root because two tools writing the same filename into one directory is how
        /// diagnostics here have already destroyed each other's evidence. `static readonly` rather than
        /// `const` because <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
        /// </summary>
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "graph_enable_erosion");

        /// <summary>
        /// Authored node id, left exactly as it was. Changing it would point this mutator at a different
        /// node and move shipped world geometry; if it is stale the tool now says so and refuses.
        /// </summary>
        private const ulong ErosionNodeId = 9077947330430238722UL;

        private const int ExitChanged = 0;
        private const int ExitFailed = 2;
        private const int ExitAlreadyCorrect = 5;
        private const int ExitRefused = 6;

        [MenuItem("Hecton8/Diagnostics/Enable Erosion (Batch)")]
        public static void Run()
        {
            // NO GPU REFUSAL HERE, on purpose, and this is a judgement rather than an omission.
            // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for tools whose
            // output degenerates to zeros without a graphics device - compute dispatches, Graphics.Blit,
            // readbacks, PNG encodes. This tool does none of those: it edits a bool and a ulong on a
            // deserialized ScriptableObject and saves it, which is exactly as truthful headless as it is
            // on a GPU. A refusal that fires on a working tool teaches the next reader to ignore
            // refusals, so exit code 3 is deliberately unreachable here.
            int exitCode;
            try
            {
                exitCode = Mutate();
            }
            catch (Exception ex)
            {
                // Deliberately NO file write in this catch. The old one wrote into a directory that may
                // not exist, which throws here and replaces the real cause with the reporter's own
                // exception. Debug.LogError reaches the batchmode log, which is the channel that is
                // actually read.
                Debug.LogError(
                    $"[{ToolName}] FAILED: the erosion node was NOT proved enabled and " +
                    $"'{GraphAssetPath}' is in an UNVERIFIED state - the flag may have been set in memory " +
                    $"without being saved, or saved without the version bump. Do not treat any terrain " +
                    $"capture from this run as evidence. {ex}");
                Finish(ExitFailed);
                return;
            }

            Finish(exitCode);
        }

        /// <summary>
        /// Returns the exit code for the outcome it proved. Never calls EditorApplication.Exit itself, so
        /// that every terminal branch is forced through the single reporting point in <see cref="Run"/>.
        /// </summary>
        private static int Mutate()
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
            if (graph == null)
            {
                return Refuse(
                    $"no MapMagic Graph asset at '{GraphAssetPath}'. If the graph was moved or renamed, " +
                    "this tool's hardcoded path is stale.",
                    examined: 0, matched: 0);
            }

            // Graph.generators is [NonSerialized] and repopulated by the serialization callback
            // (Assets/MapMagic/Nodes/Graph.cs:22). A null array means the asset deserialized into an
            // empty shell - which is a load failure, not an empty graph.
            Generator[] generators = graph.generators;
            if (generators == null)
            {
                return Refuse(
                    $"'{GraphAssetPath}' loaded but graph.generators is null, so the asset did not " +
                    "deserialize. Nothing was examined.",
                    examined: 0, matched: 0);
            }

            int examined = 0;
            int matched = 0;
            Generator target = null;

            // Manual loop rather than LINQ FirstOrDefault so that "how many nodes did you look at" is a
            // reported number instead of an assumption, and so a duplicate id is detected rather than
            // silently resolved to whichever came first.
            for (int i = 0; i < generators.Length; i++)
            {
                Generator gen = generators[i];
                if (gen == null) continue;

                examined++;
                if (gen.id != ErosionNodeId) continue;

                matched++;
                if (target == null) target = gen;
            }

            if (matched == 0)
            {
                return Refuse(
                    $"examined {examined} node(s) in '{GraphAssetPath}' and NONE carries id " +
                    $"{ErosionNodeId}. Zero matches on a hardcoded id means a stale id or the wrong " +
                    "graph, never a healthy graph. Nothing was changed.",
                    examined, matched);
            }

            if (matched > 1)
            {
                return Refuse(
                    $"{matched} nodes in '{GraphAssetPath}' share id {ErosionNodeId} out of {examined} " +
                    "examined. There is no way to know which one was meant, and enabling the wrong one " +
                    "moves terrain. Nothing was changed.",
                    examined, matched);
            }

            // Identity assertion by runtime type name. Both MapMagic erosion nodes are called Erosion200
            // (MapMagic.Nodes.MatrixGenerators, Assets/MapMagic/Generators/Matrix/Runtime/
            // MatrixModifiers.cs:1081, and MapMagic.Nodes.MatrixSetsGenerators, Assets/MapMagic/
            // Generators/MatrixSets/Runtime/MatrixSetsGenerators.cs:539). A name test rather than a typeof
            // test on purpose: `using` both of those namespaces would make the bare name Erosion200
            // ambiguous (CS0104), and this tool must accept either. IndexOf rather than
            // string.Contains(string, StringComparison) so no netstandard2.1-only overload is required.
            string targetTypeName = target.GetType().Name;
            if (targetTypeName.IndexOf("Erosion", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return Refuse(
                    $"node {ErosionNodeId} exists in '{GraphAssetPath}' but it is a " +
                    $"'{target.GetType().FullName}', not an erosion node. Enabling it would change " +
                    "authored terrain in a way nobody asked for, so nothing was changed. The hardcoded " +
                    "id now points at a different node.",
                    examined, matched);
            }

            if (target.enabled)
            {
                string alreadyMsg =
                    $"ALREADY CORRECT: {targetTypeName} node {ErosionNodeId} in '{GraphAssetPath}' is " +
                    $"already enabled. Examined {examined} node(s), matched {matched}, changed 0. Nothing " +
                    "was written and no asset was marked dirty. If the generated terrain still shows no " +
                    "erosion, the cause is downstream of this flag - link wiring or a graph that has not " +
                    "regenerated - not this node's enabled state.";
                Debug.Log($"[{ToolName}] {alreadyMsg}");
                TryWriteReport("ALREADY CORRECT", alreadyMsg, examined, matched, changed: 0);
                return ExitAlreadyCorrect;
            }

            target.enabled = true;

            // Mirrors what the authoring GUI does for this exact operation
            // (Assets/MapMagic/Nodes/Editor/GraphEditorActions.cs:76). Without it Graph.IdsVersions()
            // (Assets/MapMagic/Nodes/Graph.cs:654-664) does not change, so anything caching on that value
            // keeps its pre-erosion product and the flag is a lie. This bumps a cache counter; it does not
            // itself move geometry.
            ulong versionBefore = target.version;
            target.version++;

            EditorUtility.SetDirty(graph);

            // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the project,
            // including a concurrent session's unfinished authoring work in this shared tree. Also
            // deliberately no AssetDatabase.Refresh() - the old one triggered a project-wide reimport for
            // a one-field edit that SaveAssetIfDirty already persists.
            AssetDatabase.SaveAssetIfDirty(graph);

            // Proof that the write actually landed rather than that the call was made. If the save was
            // rejected - read-only file, checkout lock - the object stays dirty and exit 0 would be a lie.
            if (EditorUtility.IsDirty(graph))
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: {targetTypeName} node {ErosionNodeId} was set enabled in memory " +
                    $"but '{GraphAssetPath}' is STILL DIRTY after AssetDatabase.SaveAssetIfDirty, so " +
                    "nothing reached disk. The asset is probably read-only or locked. Graph state is " +
                    "unverified.");
                TryWriteReport(
                    "FAILED (save did not flush)",
                    "SaveAssetIfDirty left the graph dirty; the enabled flag did not reach disk.",
                    examined, matched, changed: 0);
                return ExitFailed;
            }

            string changedMsg =
                $"CHANGED: {targetTypeName} node {ErosionNodeId} enabled false -> true in " +
                $"'{GraphAssetPath}', version {versionBefore} -> {target.version}. Examined {examined} " +
                $"node(s), matched {matched}, changed 1. This alters authored terrain shape - regenerate " +
                "the graph before judging any height or erosion evidence.";
            Debug.Log($"[{ToolName}] {changedMsg}");
            TryWriteReport("CHANGED", changedMsg, examined, matched, changed: 1);
            return ExitChanged;
        }

        /// <summary>
        /// Single refusal path: names what was NOT produced in the batchmode log, records it, and returns
        /// a non-zero code. Every caller of this has already established that the graph was not touched.
        /// </summary>
        private static int Refuse(string detail, int examined, int matched)
        {
            string msg =
                $"REFUSED: {detail} The erosion node was NOT enabled. Examined {examined} node(s), " +
                $"matched {matched}, changed 0.";
            Debug.LogError($"[{ToolName}] {msg}");
            TryWriteReport("REFUSED", msg, examined, matched, changed: 0);
            return ExitRefused;
        }

        /// <summary>
        /// Writes the outcome artifact on EVERY terminal branch, not only on success, so the file cannot
        /// become a stale success fossil. A failure to write is a warning, never a change of verdict: the
        /// exit code reports the state of the graph, and Debug.LogError/Log above is the authoritative
        /// channel. This never throws out of itself, so it can never destroy a finding the way the old
        /// brain-folder write did.
        /// </summary>
        private static void TryWriteReport(string outcome, string detail, int examined, int matched, int changed)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);

                StringBuilder report = new StringBuilder();
                report.AppendLine("# GraphEnableErosion");
                report.AppendLine();
                report.AppendLine($"- outcome: {outcome}");
                report.AppendLine($"- graph: {GraphAssetPath}");
                report.AppendLine($"- target node id: {ErosionNodeId}");
                report.AppendLine($"- nodes examined: {examined}");
                report.AppendLine($"- nodes matching the target id: {matched}");
                report.AppendLine($"- nodes changed: {changed}");
                report.AppendLine($"- utc: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
                report.AppendLine();
                report.AppendLine(detail);

                File.WriteAllText(Path.Combine(OutputDir, "enable_erosion_result.md"), report.ToString());
            }
            catch (Exception writeEx)
            {
                Debug.LogWarning(
                    $"[{ToolName}] outcome was '{outcome}' but the report could not be written under " +
                    $"'{OutputDir}': {writeEx.Message}. The Unity log above is the record for this run.");
            }
        }

        /// <summary>
        /// Carries the outcome as a process exit code in batchmode, and does NOT kill a human's editor
        /// when the same method is reached from the menu item. This tree is shared with a live authoring
        /// session, and a menu click that quits the editor discards that session's unsaved work.
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
