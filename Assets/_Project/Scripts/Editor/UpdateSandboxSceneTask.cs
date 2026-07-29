using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class UpdateSandboxSceneTask
    {
        // This auto-run editor task was disabled by someone dropping a bare `return;` at the top of
        // Run(), which left the whole body compiled but unreachable (CS0162). The intent is kept -
        // the task stays off - but expressed as an explicit, greppable switch instead of a silent
        // early return. Deliberately `static readonly` rather than `const`: a const would fold and
        // reintroduce the unreachable-code warning, and this is editor-only code where the branch
        // costs nothing. Flip to true to re-enable the sandbox scene updater.
        private static readonly bool TaskEnabled = false;

        [InitializeOnLoadMethod]
        private static void Run()
        {
            if (!TaskEnabled)
                return;

            if (SessionState.GetBool("UpdateSandboxSceneTaskRun", false)) return;
            SessionState.SetBool("UpdateSandboxSceneTaskRun", true);

            EditorApplication.delayCall += () => {
                try
                {
                    const string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

                    // REFUSE BEFORE OPENING. OpenScene defaults to OpenSceneMode.Single, which silently
                    // discards unsaved in-memory work in every currently open scene - and this runs from
                    // [InitializeOnLoadMethod], so it would fire on a launch nobody associated with this
                    // task, destroying whatever a human or a sibling agent had open. Precedent for the
                    // guard: H8_WorldRootGraveyardRepair.cs:116-129 and H8_ScenePPtrIntegrityAudit.cs:809-830.
                    for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    {
                        Scene loaded = EditorSceneManager.GetSceneAt(i);
                        if (!loaded.isDirty)
                            continue;

                        Debug.LogError(
                            $"[UpdateSandboxSceneTask] REFUSED: scene '{loaded.path}' has unsaved changes and " +
                            "this task opens scenes in Single mode, which would discard them. Nothing was " +
                            "changed. Save or discard that scene, then re-run.");
                        return;
                    }

                    Scene activeScene = EditorSceneManager.GetActiveScene();
                    Scene target = activeScene.path == scenePath
                        ? activeScene
                        : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    if (!target.IsValid() || !target.isLoaded)
                    {
                        Debug.LogError(
                            $"[UpdateSandboxSceneTask] REFUSED: could not open '{scenePath}'. Nothing was changed.");
                        return;
                    }

                    // FindAnyObjectByType skips INACTIVE objects, and a sandbox MapMagicObject is routinely
                    // authored inactive so Awake does not run before its graph is assigned - see
                    // CreateSandboxV2.cs, which creates it with SetActive(false) on purpose. So the null
                    // here does not mean "no MapMagicObject in the scene"; it means "none that is active",
                    // and the message must not claim otherwise. Same trap recorded in
                    // Diagnostics/FixMapMagicHeightTask.cs.
                    var mapMagicObject = UnityEngine.Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
                    if (mapMagicObject == null)
                    {
                        Debug.LogError(
                            $"[UpdateSandboxSceneTask] REFUSED: no ACTIVE MapMagicObject in '{scenePath}'. " +
                            "FindAnyObjectByType does not see inactive objects, and sandbox MapMagic holders " +
                            "are often authored inactive, so this may be a live object that is simply " +
                            "disabled. Nothing was changed.");
                        return;
                    }

                    // The graph was MOVED to Data/World/Archive/, so this path resolves to null. Keeping the
                    // stale path verbatim and naming both locations is deliberate: silently repointing at an
                    // archived asset is a content decision, not a code fix. Same resolution as
                    // Scripts/Editor/CreateSandboxV2.cs, and HectonMacroGeologyBaseIntegrator.cs:454 already
                    // recorded the path as stale.
                    const string graphPath = "Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset";
                    const string archivedGraphPath = "Assets/_Project/Data/World/Archive/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset";
                    var newGraph = AssetDatabase.LoadAssetAtPath<MapMagic.Nodes.Graph>(graphPath);

                    if (newGraph == null)
                    {
                        bool archivedCopyExists =
                            AssetDatabase.LoadAssetAtPath<MapMagic.Nodes.Graph>(archivedGraphPath) != null;
                        Debug.LogError(
                            $"[UpdateSandboxSceneTask] REFUSED: no graph at '{graphPath}'. Nothing was changed. " +
                            (archivedCopyExists
                                ? $"A copy exists at '{archivedGraphPath}' - reviving an archived graph is a " +
                                  "content decision, so restore it or repoint this task deliberately."
                                : "No copy exists under Data/World/Archive/ either, so the asset is gone from " +
                                  "the repo."));
                        return;
                    }

                    if (mapMagicObject.graph == newGraph && mapMagicObject.draftsInEditor)
                    {
                        Debug.Log(
                            $"[UpdateSandboxSceneTask] ALREADY CORRECT: '{scenePath}' already binds " +
                            $"'{graphPath}' with draftsInEditor set. Nothing written.");
                        return;
                    }

                    mapMagicObject.graph = newGraph;
                    mapMagicObject.draftsInEditor = true;

                    EditorUtility.SetDirty(mapMagicObject);
                    EditorSceneManager.MarkSceneDirty(target);

                    // SaveScene on the ONE scene, never SaveOpenScenes(). A concurrent authoring session
                    // shares this working tree, and SaveOpenScenes would write out their unfinished work
                    // alongside this change - the scene-level equivalent of AssetDatabase.SaveAssets(),
                    // which this cleanup has already removed from four other tools.
                    if (!EditorSceneManager.SaveScene(target))
                    {
                        Debug.LogError(
                            $"[UpdateSandboxSceneTask] FAILED: graph assigned in memory but SaveScene('{scenePath}') " +
                            "returned false, so the change is NOT on disk.");
                        return;
                    }

                    Debug.Log(
                        $"[UpdateSandboxSceneTask] CHANGED: '{scenePath}' now binds '{graphPath}' with " +
                        "draftsInEditor set, and the scene was saved.");
                }
                catch (System.Exception e)
                {
                    // Name what was not produced. The old handler logged the bare exception, which in a
                    // batchmode log is indistinguishable from any other stack trace.
                    Debug.LogError(
                        $"[UpdateSandboxSceneTask] FAILED: the sandbox scene was NOT updated. {e}");
                }
            };
        }
    }
}
