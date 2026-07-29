using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using System.IO;
using System.Text;

namespace Hecton8.Editor
{
    /// <summary>
    /// Sets the sandbox render scene's MapMagic tile ranges to a 3x3 grid and SAVES THE SCENE. This
    /// is a mutator of authored content, not a probe: it rewrites
    /// <c>020_RENDER_SANDBOX.unity</c> on disk.
    ///
    /// <para>
    /// What it used to do wrong - every one of these produced a clean-looking batchmode run:
    /// </para>
    /// <list type="number">
    /// <item>Every line of its report, including "[ERROR] MapMagicObject NOT FOUND in scene." and the
    /// exception text, went ONLY to
    /// <c>C:/Users/danat/.gemini/antigravity/brain/389e4a53-.../setup_grid_log.txt</c> - a different
    /// agent's private scratch directory, on a user profile ("danat") that is not even this machine's.
    /// It called Debug.Log exactly zero times, so the Unity log - the only channel batchmode captures
    /// and the only channel any verdict in this project is actually read from - saw NOTHING. Not the
    /// success, not the failure, not the exception.</item>
    /// <item>When that directory did not exist, File.WriteAllText threw INSIDE the catch block,
    /// destroying the original exception and replacing it with a DirectoryNotFoundException that also
    /// went nowhere.</item>
    /// <item>Exit code 1 was used for "scene invalid", "MapMagicObject missing", and "exception"
    /// alike, so the three could not be told apart, and 1 is not in this project's instrument exit
    /// vocabulary at all.</item>
    /// <item>It rewrote and re-saved the scene unconditionally. A run that changed nothing was
    /// indistinguishable from a run that changed the grid, and it dirtied a shipped scene asset on
    /// every invocation.</item>
    /// </list>
    ///
    /// <para>
    /// NO GPU REFUSAL HERE, deliberately. <c>C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37</c>
    /// bans <c>-nographics</c> for MapMagic/compute GENERATION, because Graphics.Blit and compute
    /// shaders return zeros with no GPU context. This tool assigns two int fields and saves the scene;
    /// it never calls Generate, never blits, never reads back a texture and never inspects a produced
    /// matrix, so there is no number here that a null graphics device could fabricate. Whatever runs
    /// the generation afterwards is what owns that refusal. Do not add an Exit(3) here - it would only
    /// block a configuration step that is legitimately headless.
    /// </para>
    /// </summary>
    public static class SetupMapMagicGrid
    {
        private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

        /// <summary>
        /// Range 1 => tiles from (-1,-1) to (+1,+1) => 3x3 = 9 chunks. This is the authored intent of
        /// the tool and is NOT a value to retune here; changing it changes how much world the render
        /// sandbox generates.
        /// </summary>
        private const int TargetRange = 1;

        // Per-tool subfolder, inside the repo, under Logs/ with every other route artifact. Not
        // cosmetic: tools in this project have already destroyed each other's evidence by sharing one
        // output directory and one filename. Path.Combine is not a compile-time constant, so this is
        // static readonly, not const.
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "setup_mapmagic_grid");

        private static readonly string ReportPath = Path.Combine(OutputDir, "setup_grid_log.txt");

        private const int ExitOk = 0;
        private const int ExitFailed = 2;

        [MenuItem("Hecton8/Tests/Setup MapMagic 3x3")]
        public static void Execute()
        {
            // COLD ALLOC: StringBuilder[2048] - one editor report, mirrored to the Unity console so the
            // before/after range values survive as readable evidence - owner: SetupMapMagicGrid
            StringBuilder log = new StringBuilder(2048);
            try
            {
                log.AppendLine("[SetupMapMagicGrid] Opening scene: " + ScenePath);
                UnityEngine.SceneManagement.Scene scene =
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Fail(
                        log,
                        "REFUSED: scene '" + ScenePath + "' is invalid or missing, so no MapMagic grid " +
                        "was configured and nothing was saved. If the scene moved, this tool's " +
                        "hardcoded path is stale.");
                    return;
                }

                // Count, do not just pick. Zero means a stale path, a renamed object, or the wrong
                // scene - every time - and used to exit "1" into a file nobody read. More than one
                // means the tool would silently configure an arbitrary member of the set and report
                // success for the whole scene.
                MapMagicObject[] found = UnityEngine.Object.FindObjectsByType<MapMagicObject>(
                    UnityEngine.FindObjectsInactive.Include,
                    UnityEngine.FindObjectsSortMode.None);
                int foundCount = found != null ? found.Length : 0;
                log.AppendLine("[INFO] MapMagicObject instances examined in scene: " + foundCount);

                if (foundCount == 0)
                {
                    Fail(
                        log,
                        "REFUSED: examined the whole of '" + ScenePath + "' and found 0 MapMagicObject " +
                        "instances, so there was no grid to configure. Nothing was changed and the " +
                        "scene was not saved.");
                    return;
                }

                if (foundCount > 1)
                {
                    Fail(
                        log,
                        "REFUSED: found " + foundCount + " MapMagicObject instances in '" + ScenePath +
                        "'. This tool configures exactly one and will not guess which of several owns " +
                        "the render sandbox grid. Nothing was changed and the scene was not saved.");
                    return;
                }

                MapMagicObject mm = found[0];
                if (mm.tiles == null)
                {
                    Fail(
                        log,
                        "REFUSED: MapMagicObject '" + mm.gameObject.name + "' has a null tiles " +
                        "container, so tiles.generateRange cannot be set. Nothing was changed and the " +
                        "scene was not saved.");
                    return;
                }

                log.AppendLine("[INFO] Target MapMagicObject GameObject: '" + mm.gameObject.name + "'");
                log.AppendLine("[INFO] tileSize = " + mm.tileSize);

                int prevMain = mm.mainRange;
                int prevGenerate = mm.tiles.generateRange;
                log.AppendLine("[INFO] before: mainRange=" + prevMain + " tiles.generateRange=" + prevGenerate);
                log.AppendLine("[INFO] target:  mainRange=" + TargetRange + " tiles.generateRange=" + TargetRange
                    + " (range -" + TargetRange + "..+" + TargetRange + " on both axes => 3x3 = 9 chunks)");

                if (prevMain == TargetRange && prevGenerate == TargetRange)
                {
                    log.AppendLine("[SetupMapMagicGrid] ALREADY CORRECT: both ranges are already "
                        + TargetRange + ". Nothing was written, the scene was not marked dirty and the "
                        + "scene file on disk is untouched.");
                    string alreadyCorrect = log.ToString();
                    Debug.Log(alreadyCorrect);
                    TryWriteReport(alreadyCorrect);
                    ExitBatchmode(ExitOk);
                    return;
                }

                mm.mainRange = TargetRange;
                mm.tiles.generateRange = TargetRange;
                log.AppendLine("[CHANGE] mainRange:           " + prevMain + " -> " + mm.mainRange);
                log.AppendLine("[CHANGE] tiles.generateRange: " + prevGenerate + " -> " + mm.tiles.generateRange);

                EditorUtility.SetDirty(mm);
                bool saved = EditorSceneManager.SaveScene(scene);
                if (!saved)
                {
                    Fail(
                        log,
                        "FAILED: EditorSceneManager.SaveScene returned false for '" + ScenePath +
                        "'. The ranges were changed IN MEMORY ONLY and are lost; the scene file on " +
                        "disk still carries mainRange=" + prevMain + " generateRange=" + prevGenerate +
                        ". This branch used to report SUCCESS.");
                    return;
                }

                log.AppendLine("[SetupMapMagicGrid] CHANGED and saved '" + ScenePath + "'. This alters "
                    + "how many chunks the render sandbox generates; regenerate before judging any "
                    + "capture taken from this scene.");
                string changed = log.ToString();
                Debug.Log(changed);
                TryWriteReport(changed);
                ExitBatchmode(ExitOk);
            }
            catch (System.Exception ex)
            {
                // Was: append to the StringBuilder, write it to another agent's brain directory - which
                // threw again when the directory was absent - and exit 1. Debug.LogError is the only
                // channel batchmode captures.
                Debug.LogError(
                    "[SetupMapMagicGrid] FAILED mid-run: no MapMagic 3x3 grid was configured and the "
                    + "state of '" + ScenePath + "' on disk is UNVERIFIED - it may have been opened, "
                    + "mutated in memory and not saved. Partial log follows the exception.\n" + ex
                    + "\n--- partial log ---\n" + log);
                TryWriteReport(log.ToString() + "\n[EXCEPTION] " + ex);
                ExitBatchmode(ExitFailed);
            }
        }

        /// <summary>
        /// Logs a refusal to the Unity console, mirrors it to the repo report, and exits non-zero.
        /// Every branch that reaches this used to exit 1 into a file no log reader ever opened.
        /// </summary>
        private static void Fail(StringBuilder log, string message)
        {
            log.AppendLine("[SetupMapMagicGrid] " + message);
            string text = log.ToString();
            Debug.LogError("[SetupMapMagicGrid] " + message + "\n--- log ---\n" + text);
            TryWriteReport(text);
            ExitBatchmode(ExitFailed);
        }

        /// <summary>
        /// The report is a convenience copy; the Unity console copy above is authoritative and is
        /// written first. A failure to produce the file is reported but deliberately does not change
        /// the outcome, because losing the primary signal to a failed secondary write is the exact bug
        /// this file was fixed for.
        /// </summary>
        private static void TryWriteReport(string text)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);
                File.WriteAllText(ReportPath, text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    "[SetupMapMagicGrid] Could not write the report artifact to '" + ReportPath
                    + "'. The verdict above is unaffected and is the authoritative copy. " + ex);
            }
        }

        /// <summary>
        /// Guarded because this is also a MenuItem: EditorApplication.Exit from a menu click would
        /// close the editor and discard the operator's unsaved work.
        /// </summary>
        private static void ExitBatchmode(int code)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }
    }
}
