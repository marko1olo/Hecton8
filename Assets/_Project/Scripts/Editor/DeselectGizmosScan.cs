#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Writes the full GameObject/Component hierarchy of the V2 render sandbox to a text file, so a
    /// scene's contents can be audited without opening the editor UI.
    ///
    /// WHAT WAS WRONG:
    ///
    /// * the report went to <c>C:/Users/Admin/.gemini/antigravity/brain/9412af70-.../</c>, another
    ///   agent's private scratch directory. There was no <c>Directory.CreateDirectory</c>, so on any
    ///   machine where that folder is absent the <see cref="StreamWriter"/> constructor threw
    ///   <see cref="DirectoryNotFoundException"/> before a single line was written;
    /// * there was no try/catch and no <c>EditorApplication.Exit</c> of any kind. A throw left the
    ///   batch with no exit code, and the success log line
    ///   ("Scene hierarchy written to scene_hierarchy_log.txt") named a bare filename with no directory,
    ///   so nobody reading the log could tell where to look or whether anything landed;
    /// * <see cref="EditorSceneManager.OpenScene"/> was called unconditionally, discarding unsaved scene
    ///   work without asking - the hazard <c>H8_RouteCaptureStation.cs:459-471</c> guards against, and a
    ///   live one in a working tree shared with other lanes;
    /// * a scene that yielded ZERO GameObjects still logged the success line.
    ///
    /// NO GPU REFUSAL HERE, on purpose, and this is a deliberate judgement rather than an omission.
    /// <c>C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37</c> bans <c>-nographics</c> for tools
    /// whose output becomes zeros without a graphics device - compute dispatches, blits, readbacks,
    /// renders. This tool does none of those: it enumerates scene objects and their component types
    /// through the managed API, which is exactly as truthful headless as it is on a GPU. Refusing here
    /// would block a check that is honest under <c>-nographics</c>, and a refusal that fires on a working
    /// tool teaches the next reader to ignore refusals.
    /// </summary>
    public static class DeselectGizmosScan
    {
        private const string ToolName = "DeselectGizmosScan";

        /// <summary>
        /// Per-tool subfolder inside the repo, not a shared <c>Logs/</c> root: two tools writing the same
        /// filename into one directory is how Stage1Check and Stage1VerifyAndRelink destroyed each
        /// other's evidence. `static readonly` rather than `const` because <see cref="Path.Combine"/> is
        /// not a compile-time constant (CS0133).
        /// </summary>
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "deselect_gizmos_scan");

        private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity";

        /// <summary>A report shorter than this did not describe a scene.</summary>
        private const int MinimumReportBytes = 64;

        [MenuItem("Hecton8/Tests/Scan Scene Hierarchy")]
        public static void Scan()
        {
            string reportPath = null;
            int gameObjectCount = 0;
            int componentCount = 0;

            try
            {
                Directory.CreateDirectory(OutputDir);
                reportPath = Path.Combine(OutputDir, "scene_hierarchy_log.txt");

                if (!TryOpenSceneWithoutDiscardingWork(ScenePath))
                {
                    Finish(2);
                    return;
                }

                // Signature left exactly as it was - this overload is already proven to compile in this
                // assembly, and the lock-free gate in CONTRIBUTING.md emits false errors here so an
                // unverifiable "tidier" Find* call cannot be checked before a real Unity run.
                GameObject[] allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                gameObjectCount = allGo.Length;

                using (var writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine($"SCENE HIERARCHY SCAN - {ScenePath}");
                    writer.WriteLine($"gameObjects={gameObjectCount} (inactive included)");
                    writer.WriteLine("==================================================");

                    foreach (GameObject go in allGo)
                    {
                        string path = GetGameObjectPath(go);
                        writer.WriteLine($"GO: '{path}' (active={go.activeSelf})");
                        Component[] comps = go.GetComponents<Component>();
                        foreach (Component c in comps)
                        {
                            // A null entry here is a MISSING SCRIPT on the GameObject, which is a real
                            // scene defect. It used to be skipped in silence, so a scan of a scene full
                            // of broken references read as clean.
                            if (c == null)
                            {
                                writer.WriteLine("  COMP: <MISSING SCRIPT - broken component reference>");
                                continue;
                            }

                            writer.WriteLine($"  COMP: {c.GetType().FullName}");
                            componentCount++;
                        }
                    }
                }

                if (gameObjectCount == 0)
                {
                    // Was: logged the success line anyway.
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '{ScenePath}' yielded ZERO GameObjects. Either the scene " +
                        "did not open or it is empty; either way this scan measured nothing. Report " +
                        $"stub at {reportPath}");
                    Finish(2);
                    return;
                }

                if (!ArtifactIsPlausible(reportPath, out string detail))
                {
                    Debug.LogError(
                        $"[{ToolName}] FAILED: the writer completed without throwing but the report is " +
                        $"not usable - {reportPath} {detail}.");
                    Finish(2);
                    return;
                }

                Debug.Log(
                    $"[{ToolName}] scanned {gameObjectCount} GameObjects / {componentCount} components " +
                    $"from '{ScenePath}' -> {reportPath} ({detail})");
            }
            catch (System.Exception ex)
            {
                // Was: no catch at all. The exception went to a channel with no exit code attached.
                Debug.LogError(
                    $"[{ToolName}] FAILED: no scene hierarchy report was produced for '{ScenePath}' " +
                    $"(intended path {reportPath ?? "<not resolved>"}). {ex}");
                Finish(2);
                return;
            }

            Finish(0);
        }

        /// <summary>
        /// Opens the scene only when nothing would be lost, mirroring
        /// <c>H8_RouteCaptureStation.cs:459-471</c>.
        /// </summary>
        private static bool TryOpenSceneWithoutDiscardingWork(string scenePath)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isDirty)
                    continue;

                Debug.LogError(
                    $"[{ToolName}] REFUSED to open '{scenePath}': scene '{scene.name}' has unsaved " +
                    "changes and opening would discard them. No scan was performed.");
                return false;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return true;
        }

        /// <summary>
        /// Carries the outcome as a process exit code in batchmode, and does NOT kill a human's editor
        /// when the same method is reached from the menu item above.
        /// </summary>
        private static void Finish(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
                return;
            }

            if (exitCode != 0)
                Debug.LogError($"[{ToolName}] would exit {exitCode} in batchmode.");
        }

        private static bool ArtifactIsPlausible(string path, out string detail)
        {
            if (!File.Exists(path))
            {
                detail = "does not exist on disk after the write";
                return false;
            }

            long length = new FileInfo(path).Length;
            if (length < MinimumReportBytes)
            {
                detail = $"is {length} bytes, too small to describe a scene";
                return false;
            }

            detail = $"{length} bytes";
            return true;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            while (go.transform.parent != null)
            {
                go = go.transform.parent.gameObject;
                path = go.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
