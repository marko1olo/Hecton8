using System;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class HectonMacroGeologyBaseIntegrator
    {
        private const string GraphPath = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        // The shipping world scene. 02_HECTON_WORLD.unity is a BINARY scene: it cannot be inspected or
        // mutated as text, which is why the height sync below goes through EditorSceneManager and not
        // through any form of file rewriting.
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        // Same tolerance the runtime validator uses in
        // HectonSandboxAbyssalShelfMapMagicNode.Generate, so the authoring tool and the validator can
        // never disagree about whether the scene is already correct.
        private const float HeightMatchToleranceMeters = 1f;

        [MenuItem("Hecton8/World/MapMagic/Integrate Macro Geology Base")]
        public static void RunIntegration()
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
            if (graph == null)
            {
                Debug.LogError($"[HectonMacroGeologyBaseIntegrator] Graph asset not found at {GraphPath}");
                return;
            }

            // Log all generators in graph
            if (graph.generators == null)
            {
                Debug.LogError("[HectonMacroGeologyBaseIntegrator] graph.generators is NULL!");
            }
            else
            {
                Debug.Log($"[HectonMacroGeologyBaseIntegrator] Found {graph.generators.Length} generators:");
                for (int i = 0; i < graph.generators.Length; i++)
                {
                    var gen = graph.generators[i];
                    Debug.Log($"  Generator [{i}]: {(gen != null ? gen.GetType().FullName : "NULL")}");
                }
            }

            // Find Tectonic Node
            HectonBiomeMatrixMapMagicPostProcessNode tectonicNode = FindFirst<HectonBiomeMatrixMapMagicPostProcessNode>(graph);
            if (tectonicNode == null)
            {
                Debug.LogError("[HectonMacroGeologyBaseIntegrator] HectonBiomeMatrixMapMagicPostProcessNode not found in graph!");
                return;
            }

            // Find or Create Macro Geology Base Node
            HectonSandboxAbyssalShelfMapMagicNode macroBaseNode = EnsureGenerator<HectonSandboxAbyssalShelfMapMagicNode>(graph, -660f, -80f, out bool created);

            // Unlink Tectonic Node if it is linked to something else
            if (graph.IsLinked(tectonicNode))
            {
                graph.UnlinkInlet(tectonicNode);
                Debug.Log("[HectonMacroGeologyBaseIntegrator] Unlinked old connection to Tectonic Node.");
            }

            // Link Macro Geology Base Node to Tectonic Node
            graph.Link(macroBaseNode, tectonicNode);
            Debug.Log($"[HectonMacroGeologyBaseIntegrator] Linked Macro Geology Base Node to Tectonic Node. Created node: {created}");

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════
        //  TERRAIN HEIGHT / GEOLOGY Y-SPAN SYNC
        //
        //  Measured defect (Logs/omega_route18..20.log, ~53 warnings per run):
        //      [HectonMacroGeology] TerrainData.size.y (250,0m) != geology Y-span (12000,0m).
        //
        //  The geology side is authoritative and is authored in the MapMagic graph asset:
        //  the HectonSandboxAbyssalShelfMapMagicNode carries highWorldY = 2000 and
        //  lowWorldY = -10000, i.e. a 12000 m span. The node normalises every height it emits into
        //  0..1 across exactly that span.
        //
        //  The terrain side is NOT authored at all. MapMagicObject.globals.height still holds the
        //  stock vendor default of 250 (Assets/MapMagic/Core/MapMagicObject.cs:512), and
        //  HeightOut.cs applies that value verbatim as TerrainData.size.y. The result is that the
        //  authored 12000 m of relief is rendered into 250 m of terrain - a 48x vertical collapse,
        //  which is also why an authored trenchDepthMeters of 5000 cannot possibly appear.
        //
        //  This tool writes the authored span onto the scene object. It NEVER hardcodes the span:
        //  it reads highWorldY/lowWorldY back out of the graph, so the graph stays the single
        //  source of truth and this tool cannot drift away from it.
        // ══════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Writes the graph-authored geology Y-span onto every MapMagicObject in the currently open
        /// scenes. Operates only on what is already loaded - it deliberately does not open or close
        /// scenes, so it cannot discard unsaved work.
        /// </summary>
        /// <summary>
        /// Reports what the sync WOULD change, and writes nothing.
        ///
        /// The sync's second half moves the terrain in world space - potentially by 10 km, since the
        /// authored LowWorldY is -10000m and the MapMagicObject's current base Y is unknown until
        /// something reads it. 02_HECTON_WORLD is a binary scene: there is no diff to inspect after the
        /// fact and no cheap way to see what changed. So the numbers get read before anything is written,
        /// not after.
        ///
        /// This deliberately duplicates the comparison rather than threading an apply flag through
        /// <see cref="ApplyGeologySpanToMapMagicObject"/>: that method is the tested write path and a
        /// report has no business altering its control flow.
        /// </summary>
        [MenuItem("Hecton8/World/MapMagic/Sync Terrain Height To Geology Span - REPORT ONLY", priority = 180)]
        public static void ReportTerrainHeightVersusGeologySpan()
        {
            if (!TryResolveAuthoredGeologySpan(out float lowWorldY, out float highWorldY, out string spanError))
            {
                Debug.LogError($"[HectonMacroGeologyBaseIntegrator] REPORT ABORTED - {spanError}");
                return;
            }

            float span = highWorldY - lowWorldY;
            Debug.Log(
                $"[HectonMacroGeologyBaseIntegrator] REPORT authored geology: lowWorldY={lowWorldY:F1}m " +
                $"highWorldY={highWorldY:F1}m span={span:F1}m");

            MapMagicObject[] mapMagicObjects = UnityEngine.Object.FindObjectsByType<MapMagicObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (mapMagicObjects == null || mapMagicObjects.Length == 0)
            {
                Debug.LogError(
                    "[HectonMacroGeologyBaseIntegrator] REPORT found no MapMagicObject in any open scene. " +
                    $"Open {WorldScenePath} and run this again.");
                return;
            }

            for (int i = 0; i < mapMagicObjects.Length; i++)
            {
                MapMagicObject mapMagicObject = mapMagicObjects[i];
                if (mapMagicObject == null)
                    continue;

                if (mapMagicObject.globals == null)
                {
                    Debug.LogError(
                        $"[HectonMacroGeologyBaseIntegrator] REPORT '{mapMagicObject.name}' has a null " +
                        "globals - the sync would skip it.",
                        mapMagicObject);
                    continue;
                }

                float currentHeight = mapMagicObject.globals.height;
                float currentBaseY = mapMagicObject.transform.position.y;
                bool heightWouldChange = Mathf.Abs(currentHeight - span) > HeightMatchToleranceMeters;
                bool baseWouldChange = Mathf.Abs(currentBaseY - lowWorldY) > HeightMatchToleranceMeters;

                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] REPORT '{mapMagicObject.name}' " +
                    $"activeInHierarchy={mapMagicObject.gameObject.activeInHierarchy} " +
                    $"scene='{mapMagicObject.gameObject.scene.name}' " +
                    $"globals.height={currentHeight:F1}m -> {span:F1}m ({(heightWouldChange ? "WOULD CHANGE" : "already correct")}) " +
                    $"baseY={currentBaseY:F1}m -> {lowWorldY:F1}m ({(baseWouldChange ? "WOULD MOVE THE TERRAIN" : "already correct")})",
                    mapMagicObject);

                if (heightWouldChange && currentHeight > 0f)
                {
                    Debug.LogWarning(
                        $"[HectonMacroGeologyBaseIntegrator] REPORT '{mapMagicObject.name}' authored relief " +
                        $"is currently compressed {span / currentHeight:F1}x - every slope, cliff, " +
                        "shelf-break and trench in this world is that much flatter than authored.",
                        mapMagicObject);
                }

                if (baseWouldChange)
                {
                    Debug.LogWarning(
                        $"[HectonMacroGeologyBaseIntegrator] REPORT applying the sync would move " +
                        $"'{mapMagicObject.name}' {Mathf.Abs(currentBaseY - lowWorldY):F1}m in world Y. " +
                        "Water level, player spawn height and every authored world-space prop must be " +
                        "re-checked against the new base before this is accepted.",
                        mapMagicObject);
                }
            }

            Debug.Log(
                "[HectonMacroGeologyBaseIntegrator] REPORT complete. Nothing was written. Run " +
                "'Sync Terrain Height To Geology Span' to apply.");
        }

        [MenuItem("Hecton8/World/MapMagic/Sync Terrain Height To Geology Span")]
        public static void SyncTerrainHeightToGeologySpan()
        {
            if (!TryResolveAuthoredGeologySpan(out float lowWorldY, out float highWorldY, out string spanError))
            {
                Debug.LogError($"[HectonMacroGeologyBaseIntegrator] {spanError}");
                return;
            }

            MapMagicObject[] mapMagicObjects = UnityEngine.Object.FindObjectsByType<MapMagicObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (mapMagicObjects == null || mapMagicObjects.Length == 0)
            {
                Debug.LogError(
                    "[HectonMacroGeologyBaseIntegrator] No MapMagicObject found in any open scene. " +
                    $"Open {WorldScenePath} (or the scene that owns the terrain) and run this again.");
                return;
            }

            int changed = 0;
            for (int i = 0; i < mapMagicObjects.Length; i++)
            {
                if (!ApplyGeologySpanToMapMagicObject(mapMagicObjects[i], lowWorldY, highWorldY))
                    continue;

                changed++;

                // Save the scene that actually owns this object rather than iterating a global scene
                // list, so an additively-loaded terrain owner is saved correctly and no unrelated
                // scene is written.
                Scene owningScene = mapMagicObjects[i].gameObject.scene;
                if (owningScene.IsValid())
                    EditorSceneManager.MarkSceneDirty(owningScene);
            }

            if (changed == 0)
            {
                Debug.Log(
                    "[HectonMacroGeologyBaseIntegrator] All MapMagicObjects already match the authored " +
                    $"geology span ({highWorldY - lowWorldY:F1}m base {lowWorldY:F1}m). Nothing written.");
                return;
            }

            for (int i = 0; i < mapMagicObjects.Length; i++)
            {
                Scene owningScene = mapMagicObjects[i].gameObject.scene;
                if (owningScene.IsValid() && owningScene.isDirty)
                    EditorSceneManager.SaveScene(owningScene);
            }

            Debug.Log(
                $"[HectonMacroGeologyBaseIntegrator] Synced {changed} MapMagicObject(s) to the authored " +
                "geology span and saved the owning scene(s). Terrain regenerates on next load; no " +
                "regeneration is forced here.");
        }

        /// <summary>
        /// Batchmode entry point: opens the shipping world scene, syncs it, saves, exits.
        /// Exits non-zero when the sync could not be proven, so a build gate cannot read a
        /// failed sync as success.
        /// </summary>
        public static void SyncTerrainHeightToGeologySpanHeadless()
        {
            int exitCode = 0;
            try
            {
                if (!TryResolveAuthoredGeologySpan(out float lowWorldY, out float highWorldY, out string spanError))
                {
                    Debug.LogError($"[HectonMacroGeologyBaseIntegrator] {spanError}");
                    EditorApplication.Exit(1);
                    return;
                }

                Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

                MapMagicObject[] mapMagicObjects = UnityEngine.Object.FindObjectsByType<MapMagicObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                if (mapMagicObjects == null || mapMagicObjects.Length == 0)
                {
                    Debug.LogError(
                        $"[HectonMacroGeologyBaseIntegrator] No MapMagicObject in {WorldScenePath}. " +
                        "The terrain owner is elsewhere - do not assume this scene owns it.");
                    EditorApplication.Exit(1);
                    return;
                }

                int changed = 0;
                for (int i = 0; i < mapMagicObjects.Length; i++)
                {
                    if (ApplyGeologySpanToMapMagicObject(mapMagicObjects[i], lowWorldY, highWorldY))
                        changed++;
                }

                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] Headless sync complete. Changed {changed} of " +
                    $"{mapMagicObjects.Length} MapMagicObject(s). Authored span " +
                    $"{highWorldY - lowWorldY:F1}m, base {lowWorldY:F1}m.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[HectonMacroGeologyBaseIntegrator] Headless sync threw: {exception}");
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Reads highWorldY/lowWorldY from the graph. Every macro geology node in the graph must
        /// agree; a disagreement is reported instead of silently picking the first one, because
        /// picking a stale duplicate would bake the wrong world height into the scene.
        /// </summary>
        private static bool TryResolveAuthoredGeologySpan(out float lowWorldY, out float highWorldY, out string error)
        {
            lowWorldY = 0f;
            highWorldY = 0f;
            error = null;

            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
            if (graph == null)
            {
                error = $"Graph asset not found at {GraphPath}; cannot resolve the authored geology span.";
                return false;
            }

            Generator[] generators = graph.generators;
            if (generators == null)
            {
                error = $"Graph {GraphPath} has a null generator array; cannot resolve the authored geology span.";
                return false;
            }

            int found = 0;
            for (int i = 0; i < generators.Length; i++)
            {
                if (!(generators[i] is HectonSandboxAbyssalShelfMapMagicNode node))
                    continue;

                found++;
                if (found == 1)
                {
                    lowWorldY = node.lowWorldY;
                    highWorldY = node.highWorldY;
                    continue;
                }

                if (Mathf.Abs(node.lowWorldY - lowWorldY) > HeightMatchToleranceMeters ||
                    Mathf.Abs(node.highWorldY - highWorldY) > HeightMatchToleranceMeters)
                {
                    error =
                        $"Graph {GraphPath} contains {found} macro geology nodes with DISAGREEING Y-spans " +
                        $"(first {lowWorldY:F1}..{highWorldY:F1}, this one {node.lowWorldY:F1}..{node.highWorldY:F1}). " +
                        "Refusing to guess which one is authoritative - resolve the duplicate in the graph first.";
                    return false;
                }
            }

            if (found == 0)
            {
                error =
                    $"No HectonSandboxAbyssalShelfMapMagicNode in {GraphPath}. NOTE: only the root graph is " +
                    "scanned; if the macro geology node lives inside a biome sub-graph the span must be read " +
                    "from there instead.";
                return false;
            }

            // Mirrors the runtime clamp in HectonSandboxAbyssalShelfMapMagicNode.Generate
            // (HighWorldY = math.max(highWorldY, lowWorldY + 1f)) so the value written to the scene is
            // the value the generator actually normalises against, not the raw authored field.
            highWorldY = Mathf.Max(highWorldY, lowWorldY + 1f);

            float span = highWorldY - lowWorldY;
            if (!float.IsFinite(span) || span <= 0f || !float.IsFinite(lowWorldY))
            {
                error = $"Authored geology span is not usable (low {lowWorldY}, high {highWorldY}). Refusing to write it.";
                return false;
            }

            return true;
        }

        private static bool ApplyGeologySpanToMapMagicObject(MapMagicObject mapMagicObject, float lowWorldY, float highWorldY)
        {
            if (mapMagicObject == null || mapMagicObject.globals == null)
                return false;

            float span = highWorldY - lowWorldY;
            bool changed = false;

            float previousHeight = mapMagicObject.globals.height;
            if (Mathf.Abs(previousHeight - span) > HeightMatchToleranceMeters)
            {
                mapMagicObject.globals.height = span;
                changed = true;
                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] {mapMagicObject.name}: globals.height " +
                    $"{previousHeight:F1}m -> {span:F1}m (authored geology span).",
                    mapMagicObject);
            }

            // MapMagic positions each tile with transform.localPosition (TerrainTile.cs:352), so the
            // MapMagicObject's world Y is the terrain base - the world height that normalised 0 maps to.
            // The geology normalises against lowWorldY, therefore the base must equal lowWorldY or every
            // emitted height is offset in world space even once the span is correct.
            Transform transform = mapMagicObject.transform;
            Vector3 position = transform.position;
            if (Mathf.Abs(position.y - lowWorldY) > HeightMatchToleranceMeters)
            {
                float previousBase = position.y;
                position.y = lowWorldY;
                transform.position = position;
                changed = true;
                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] {mapMagicObject.name}: terrain base Y " +
                    $"{previousBase:F1}m -> {lowWorldY:F1}m (authored LowWorldY). This MOVES the terrain in " +
                    "world space; water level, spawn height and any authored world-space props must be " +
                    "re-checked against the new base.",
                    mapMagicObject);
            }

            if (changed)
                EditorUtility.SetDirty(mapMagicObject);

            return changed;
        }

        private static T EnsureGenerator<T>(Graph graph, float x, float y, out bool created) where T : Generator
        {
            T existing = FindFirst<T>(graph);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            T generator = (T)Generator.Create(typeof(T));
            generator.guiPosition = new Vector2(x, y);
            graph.Add(generator);
            created = true;
            return generator;
        }

        private static T FindFirst<T>(Graph graph)
        {
            Generator[] generators = graph != null ? graph.generators : null;
            if (generators == null)
                return default;

            for (int i = 0; i < generators.Length; i++)
            {
                if (generators[i] is T generator)
                    return generator;
            }

            return default;
        }
    }
}
