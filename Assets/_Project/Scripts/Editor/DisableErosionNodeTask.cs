using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;

namespace MapMagic.Editor.Diagnostics
{
    /// <summary>
    /// MUTATOR. Disables the single <see cref="HectonHydraulicErosionMapMagicNode"/> in the authored
    /// sandbox biome graph. Running it changes what the terrain looks like, so every outcome now speaks
    /// in the batchmode log AND in the exit code: CHANGED (0), ALREADY DISABLED (5), REFUSED/FAILED (2).
    ///
    /// What it used to do wrong - all of which produced a success report:
    ///   - <c>DisableErosionNodeBatchmode</c> ended in <c>finally { EditorApplication.Exit(0); }</c>. An
    ///     exception was logged and the process still exited 0.
    ///   - the "could not load the Graph asset" branch logged an error and <c>return</c>ed straight into
    ///     that finally, so a stale asset path exited 0 having mutated nothing.
    ///   - the "EROSION NODE NOT FOUND" branch used <c>Debug.LogWarning</c>, not LogError, and also fell
    ///     into Exit(0). This is the same original sin as FixGraphAmplitudeTask: a stale identifier makes
    ///     the tool do nothing and report success, which is indistinguishable from a real mutation of the
    ///     authored world.
    ///   - it located the node by <c>GetType().Name == "HectonHydraulicErosionMapMagicNode"</c>. A class
    ///     rename turned into a silent runtime NOT FOUND instead of a compile error. It now binds the
    ///     real type, so a rename cannot fail quietly.
    ///   - an already-disabled node was re-written anyway: <c>version++</c>, SetDirty, save, and an
    ///     "AFTER: Enabled=False" log identical to a genuine change. Idempotent re-runs dirtied the
    ///     shared asset and looked like work.
    ///   - <c>AssetDatabase.SaveAssets()</c> flushes EVERY dirty asset in the project. This is a shared
    ///     working tree; that commits a concurrent session's unfinished authoring. Now
    ///     <c>SaveAssetIfDirty</c> on the one graph.
    ///   - <c>AssetDatabase.Refresh()</c> kicked a project-wide import for a change that needs none, with
    ///     the same shared-tree hazard. Removed; SaveAssetIfDirty already flushes the graph to disk.
    ///   - it produced no artifact at all. The verdict lived only in a Unity log whose path
    ///     (Tools/BatchTasks/run_disable_erosion.bat) points at another agent's private brain directory.
    ///     A per-tool <c>Logs/disable_erosion_node/</c> file now survives the next batch run.
    ///
    /// NO GPU REFUSAL BLOCK HERE, on purpose. The batch script passes <c>-nographics</c>, and
    /// <c>C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37</c> bans that for "MapMagic/compute
    /// generation tests" because "compute shaders and Graphics.Blit return zeros with no GPU context".
    /// This tool is not a generation test: it loads a ScriptableObject, flips a serialized <c>bool</c>,
    /// and writes the asset. It never calls Generate, never blits, never dispatches compute, never reads
    /// back a texture and never encodes a PNG - there is no number here that degrades to zero headless,
    /// so a refusal would only make the tool permanently unrunnable under its own launcher. If anyone
    /// later makes this task regenerate or capture the terrain to prove the change, it needs the
    /// <c>GraphicsDeviceType.Null</c> refusal + Exit(3) that the render tools carry.
    ///
    /// The graph .asset stores floats as raw IEEE-754 bit patterns: it is mutated only through the
    /// deserialized <see cref="Graph"/> object, never text-parsed and never hand-edited.
    /// </summary>
    public static class DisableErosionNodeTask
    {
        private const string ToolName = "DisableErosionNode";

        private const string GraphAssetPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        /// <summary>
        /// Per-tool subfolder, not a shared <c>Logs/</c> root: two tools in this project already wrote
        /// identical filenames into one directory and each run destroyed the other's evidence.
        /// <c>static readonly</c> rather than <c>const</c> because <see cref="Path.Combine"/> is not a
        /// compile-time constant (CS0133).
        /// </summary>
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "disable_erosion_node");

        /// <summary>Proved it wrote the mutation to disk.</summary>
        private const int ExitChanged = 0;

        /// <summary>Could not do the job, or crashed trying. Nothing is claimed about the graph.</summary>
        private const int ExitFailed = 2;

        /// <summary>
        /// The node was already disabled: nothing written, nothing dirtied. Deliberately NOT 0, because
        /// "I changed the authored world" and "I found it already off" are different facts and a caller
        /// that only reads the exit code must be able to tell them apart. Non-zero here does not mean
        /// something broke - 2 means that. It is outside the reserved 0/2/3/4 taxonomy on purpose.
        /// </summary>
        private const int ExitAlreadyDisabled = 5;

        private enum Outcome
        {
            Changed,
            AlreadyDisabled,
            Refused,
        }

        /// <summary>
        /// Interactive entry point. Never calls <see cref="EditorApplication.Exit"/> - that would kill a
        /// human's editor mid-session. The outcome is in the Console.
        /// </summary>
        [MenuItem("Hecton8/Graph/Disable Erosion Node")]
        public static void DisableErosionNode()
        {
            Outcome outcome = Run();
            Debug.Log(
                $"[{ToolName}] interactive run finished: {outcome}. In batchmode this same outcome would " +
                $"be exit code {ExitCodeFor(outcome)}.");
        }

        /// <summary>
        /// Batch entry point. Called by reflection from Tools/BatchTasks/run_disable_erosion.bat as
        /// <c>MapMagic.Editor.Diagnostics.DisableErosionNodeTask.DisableErosionNodeBatchmode</c> - do not
        /// rename. That script passes no <c>-quit</c>, so this method must always reach exactly one
        /// Exit() call or the editor hangs forever; hence the outer guard, which reports 2 and never 0.
        /// </summary>
        public static void DisableErosionNodeBatchmode()
        {
            int exitCode;
            try
            {
                exitCode = ExitCodeFor(Run());
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED outside the guarded body, so the erosion node's state in " +
                    $"'{GraphAssetPath}' is UNVERIFIED and no verdict file was written under " +
                    $"{OutputDir}. {ex}");
                exitCode = ExitFailed;
            }

            EditorApplication.Exit(exitCode);
        }

        private static int ExitCodeFor(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Changed: return ExitChanged;
                case Outcome.AlreadyDisabled: return ExitAlreadyDisabled;
                default: return ExitFailed;
            }
        }

        private static Outcome Run()
        {
            string phase = "startup";
            bool saveCalled = false;

            try
            {
                phase = "creating the output directory";
                Directory.CreateDirectory(OutputDir);

                phase = "loading the graph asset";
                Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphAssetPath);
                if (graph == null)
                {
                    return Refuse(
                        $"no Graph asset at '{GraphAssetPath}'. The erosion node was NOT disabled. If the " +
                        "graph moved or was renamed, this task's hardcoded path is stale. This branch used " +
                        "to log an error and then exit 0.");
                }

                phase = "scanning the generator array";
                Generator[] generators = graph.generators;
                if (generators == null)
                {
                    return Refuse(
                        $"'{GraphAssetPath}' deserialized with a null generator array, so no node could be " +
                        "inspected. The erosion node was NOT disabled.");
                }

                // Bound to the real type, not GetType().Name: a rename is now a compile error instead of
                // a silent "NOT FOUND". OfType also drops null array slots, which the old null-guarded
                // LINQ predicate had to do by hand.
                HectonHydraulicErosionMapMagicNode[] erosionNodes =
                    generators.OfType<HectonHydraulicErosionMapMagicNode>().ToArray();

                if (erosionNodes.Length == 0)
                {
                    return Refuse(
                        $"no {nameof(HectonHydraulicErosionMapMagicNode)} among the {generators.Length} " +
                        $"generator(s) in '{GraphAssetPath}'. Nothing was changed. Erosion is whatever it " +
                        "was before this run - do not read this as 'erosion is off'. This branch used to " +
                        "be a LogWarning followed by exit 0.");
                }

                if (erosionNodes.Length > 1)
                {
                    string ids = string.Join(", ", erosionNodes.Select(n => n.id.ToString()));
                    return Refuse(
                        $"{erosionNodes.Length} {nameof(HectonHydraulicErosionMapMagicNode)} nodes in " +
                        $"'{GraphAssetPath}' (ids {ids}). This tool's contract is one erosion node and it " +
                        "will not guess which one the caller meant: disabling only the first would leave " +
                        "erosion running and still report success, and disabling all of them would move " +
                        "authored world geometry further than this tool was ever asked to. Nothing was " +
                        "changed - pick the id by hand.");
                }

                HectonHydraulicErosionMapMagicNode erosion = erosionNodes[0];

                if (!erosion.enabled)
                {
                    string already =
                        $"ALREADY DISABLED: erosion node id={erosion.id} version={erosion.version} in " +
                        $"'{GraphAssetPath}' is already enabled=false. Nothing was written, no asset was " +
                        $"marked dirty, no version was bumped. Exit {ExitAlreadyDisabled} means idempotent " +
                        "no-op, not failure.";
                    Debug.Log($"[{ToolName}] {already}");
                    WriteVerdict(already);
                    return Outcome.AlreadyDisabled;
                }

                phase = "writing the graph asset";
                // Resolved from Application.dataPath (always "<project>/Assets" in the editor), NOT from
                // Directory.GetCurrentDirectory(): the launcher .bat sets -projectPath but the process cwd
                // is wherever it was invoked from. A wrong cwd here would make the disk-write proof below
                // fail on a mutation that actually landed.
                string absoluteGraphPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", GraphAssetPath.Replace('/', Path.DirectorySeparatorChar)));
                DateTime mtimeBefore = File.Exists(absoluteGraphPath)
                    ? File.GetLastWriteTimeUtc(absoluteGraphPath)
                    : DateTime.MinValue;

                ulong nodeId = erosion.id;
                ulong versionBefore = erosion.version;

                erosion.enabled = false;

                // Version bump preserved exactly as the original tool had it. MapMagic's own
                // EnableDisableGenerators bumps version only when RE-enabling
                // (Assets/MapMagic/Nodes/Editor/GraphEditorActions.cs:75-76), so this diverges from the
                // editor path - but version drives whether cached generated data is treated as stale, and
                // changing that would change what the world regenerates into. Reported, not "fixed".
                erosion.version++;

                EditorUtility.SetDirty(graph);

                // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the
                // project, including another session's in-flight authoring work in this shared tree.
                AssetDatabase.SaveAssetIfDirty(graph);
                saveCalled = true;

                phase = "verifying the write reached disk";
                bool stillDirty = EditorUtility.IsDirty(graph);
                DateTime mtimeAfter = File.Exists(absoluteGraphPath)
                    ? File.GetLastWriteTimeUtc(absoluteGraphPath)
                    : DateTime.MinValue;
                bool fileRewritten = mtimeAfter != mtimeBefore;

                // Exit 0 has to be earned. Both signals must agree that the asset actually got written;
                // if either disagrees this reports failure rather than guessing, so the error can only
                // ever be a false alarm, never a false success.
                if (stillDirty || !fileRewritten)
                {
                    string notPersisted =
                        $"FAILED: erosion node {nodeId} was set enabled=false IN MEMORY ONLY - the change " +
                        $"was not proven on disk. EditorUtility.IsDirty after SaveAssetIfDirty={stillDirty} " +
                        $"(expected False), '{absoluteGraphPath}' mtime {mtimeBefore:O} -> {mtimeAfter:O} " +
                        $"(expected a change). The asset may be read-only, locked by the concurrent " +
                        "session, or the save was rejected. Treat the graph state as unknown and re-run.";
                    Debug.LogError($"[{ToolName}] {notPersisted}");
                    WriteVerdict(notPersisted);
                    return Outcome.Refused;
                }

                string changed =
                    $"CHANGED: erosion node {nameof(HectonHydraulicErosionMapMagicNode)} id={nodeId} in " +
                    $"'{GraphAssetPath}' enabled True -> False, version {versionBefore} -> " +
                    $"{erosion.version}. Written to disk (mtime {mtimeBefore:O} -> {mtimeAfter:O}). This " +
                    "removes hydraulic erosion from authored terrain - regenerate before judging any " +
                    "height or terrain-shape evidence captured after this run.";
                Debug.Log($"[{ToolName}] {changed}");
                WriteVerdict(changed);
                return Outcome.Changed;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED while {phase}. SaveAssetIfDirty had already been called before " +
                    $"the throw: {saveCalled}. If that is False, the erosion node was NOT disabled and " +
                    "nothing was written. If it is True, the graph was flushed but this run never finished " +
                    $"proving the write, so treat '{GraphAssetPath}' as changed-but-unverified and re-run " +
                    $"for a clean verdict; no complete verdict file exists under {OutputDir}. {ex}");
                return Outcome.Refused;
            }
        }

        private static Outcome Refuse(string why)
        {
            Debug.LogError($"[{ToolName}] REFUSED: {why}");
            WriteVerdict("REFUSED: " + why);
            return Outcome.Refused;
        }

        private static void WriteVerdict(string verdict)
        {
            string reportPath = Path.Combine(OutputDir, "verdict.txt");
            File.WriteAllText(reportPath, verdict + "\n");
            Debug.Log($"[{ToolName}] verdict file: {reportPath}");
        }
    }
}
