using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using MapMagic.Nodes;

namespace Hecton8.Editor
{
    public static class CreateSandboxV2
    {
        // The graph this scene generates from. It is NOT at this path any more - it was moved to
        // Data/World/Archive/ - and that is the entire reason for the refusal below. The stale path is kept
        // verbatim so the error can name it, and so nobody "fixes" the symptom by quietly repointing at the
        // archive: reviving an archived graph is a content decision, not a code fix.
        private const string GraphPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset";

        private const string ArchivedGraphPath =
            "Assets/_Project/Data/World/Archive/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset";

        private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity";

        [MenuItem("Hecton8/Tests/Create Sandbox V2")]
        public static void Execute()
        {
            // RESOLVE THE GRAPH BEFORE TOUCHING ANYTHING. The ordering IS the fix.
            //
            // What this tool did until now: NewScene(Single), which discards whatever the user had open;
            // then LoadAssetAtPath; then Debug.LogWarning if the graph came back null; then SaveScene over
            // ScenePath ANYWAY; then Debug.Log("Successfully created and saved sterile sandbox"). Since the
            // graph was archived out from under that hardcoded path, every run replaced a 5 MB authored
            // scene with an empty one holding a MapMagicObject whose graph is null - a terrain generator
            // with no generators - and reported success on its last line. A LogWarning does not stop a
            // batch caller and does not stop a human who reads the final line.
            //
            // HectonMacroGeologyBaseIntegrator.cs:454 already recorded that this path was stale. Nothing
            // acted on it, because the tool that had to act on it was the one hiding the failure.
            //
            // No EditorApplication.Exit here on purpose: this is a MenuItem a human clicks, and quitting
            // the editor out from under them would be a worse failure than the one being fixed.
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
            if (graph == null)
            {
                bool archivedCopyExists = AssetDatabase.LoadAssetAtPath<Graph>(ArchivedGraphPath) != null;
                Debug.LogError(
                    $"[CreateSandboxV2] REFUSED: no graph at '{GraphPath}'. NOTHING was changed and " +
                    $"'{ScenePath}' was NOT overwritten. " +
                    (archivedCopyExists
                        ? $"A copy does exist at '{ArchivedGraphPath}' (authored span 6000 m: lowWorldY " +
                          "-5000, highWorldY +1000). Reviving an archived graph is a content decision - " +
                          "restore it deliberately or repoint this tool deliberately, but do not let a run " +
                          "silently produce an empty scene."
                        : "No copy exists under Data/World/Archive/ either, so the asset is gone from the " +
                          "repo and this tool needs a new graph before it can work."));
                return;
            }

            // Create a brand new empty scene. Only reached with a real graph in hand.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create MapMagic GameObject INACTIVE to avoid Awake running before graph is assigned
            GameObject mapMagicGO = new GameObject("MapMagic");
            mapMagicGO.SetActive(false);
            var mm = mapMagicGO.AddComponent<MapMagicObject>();

            // Configure MapMagic properties as requested
            mm.tileSize = new Den.Tools.Vector2D(500, 500);
            mm.tileResolution = MapMagicObject.Resolution._513;
            // TerrainData.size.y for every tile in this scene. 4000 m is preserved EXACTLY - it is not the
            // canonical vertical span and it is not being corrected here.
            //
            // HectonSandboxAbyssalShelfMapMagicNode.Generate warns whenever globals.height != HighWorldY -
            // LowWorldY, because the ratio uniformly compresses every slope, cliff, shelf-break and trench.
            // The compression factor here is measured against THE GRAPH THIS SCENE LOADS, not against the
            // C# field initialisers: a serialised node carries its own values and the deserialiser
            // overwrites the initialiser, so WorldVerticalExtentMath.DefaultVerticalSpanMeters (7000 m) is
            // not this scene's number and 7000/4000 = 1.75x - which an earlier revision of this comment
            // claimed - was never the real figure.
            //
            // Archived graph's authored span, decoded from its serialised bits, is 6000 m
            // (WorldVerticalExtentMath.ArchivedProceduralGeologyGraphSpanMeters), so the real compression is
            // 6000/4000 = 1.5x once that graph is reachable again. For contrast the LIVE world graph is
            // 12000 m (LiveWorldGraphSpanMeters), which is a different world and not what this scene builds.
            //
            // THE PREDICTION IN THE PARAGRAPH ABOVE CAME TRUE AND IS NOW RESOLVED. That graph was restored to
            // Data/World/Sandbox/ on 2026-08-11, so the scene did start rendering 6000 m of authored geology
            // into 4000 m of terrain - a uniform 1.5x vertical compression of every slope, cliff, shelf-break
            // and trench in it. Measured the same day: the node's own mismatch guard fired with
            // "TerrainData.size.y (4000.0m) != geology Y-span (6000.0m)".
            //
            // The scene follows the GRAPH, not the reverse. Vertical extent is authored in the graph node
            // (highWorldY +1000 / lowWorldY -5000, read off the asset), and the sandbox scene is a
            // regenerable render target - the owner confirmed the terrain is seed-generated and disposable.
            // Compressing the author's window to fit a scene constant would silently flatten the geology this
            // scene exists to look at. Deliberately the ArchivedProcedural... constant rather than a fresh
            // 6000f literal: the number already had a documented home and a second copy would drift from it.
            mm.globals.height = Hecton8.World.WorldVerticalExtentMath.ArchivedProceduralGeologyGraphSpanMeters;
            mm.graph = graph;

            // Now activate — Awake/OnEnable will fire with graph already set
            mapMagicGO.SetActive(true);

            // Save the scene. SaveScene to an explicit path, never SaveOpenScenes() - a concurrent authoring
            // session shares this working tree and saving everything open would commit their unfinished work.
            bool success = EditorSceneManager.SaveScene(scene, ScenePath);

            if (success)
            {
                // Name the graph and the height in the success line. The old message said only "Successfully
                // created and saved sterile sandbox", which is exactly as true of an empty scene with a null
                // graph as of a working one, and that is how this defect survived.
                Debug.Log(
                    $"[CreateSandboxV2] Saved '{ScenePath}' with graph '{AssetDatabase.GetAssetPath(graph)}' " +
                    $"and globals.height {mm.globals.height:F1}m.");
            }
            else
            {
                Debug.LogError($"[CreateSandboxV2] Failed to save sterile sandbox at {ScenePath}");
            }
        }
    }
}
