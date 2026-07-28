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

            // Find the Macro Geology Base node - and do NOT create one if it is absent.
            //
            // The previous EnsureGenerator path called Generator.Create, which hands the new node its own
            // field initialisers: HectonSandboxAbyssalShelfMapMagicNode.cs:42-43 declare
            // highWorldY = 2000f and lowWorldY = -5000f, a 7000 m span. The authored graph carries
            // lowWorldY = -10000f, a 12000 m span (decoded from the graph's serialised value table, and
            // confirmed at runtime: the validator in that node reported an expected span of 12000 m on
            // every generated tile). So creating the node silently authored a 7000 m world that disagrees
            // with the world the graph actually generates, and SyncTerrainHeightToGeologySpan below would
            // then faithfully cement that fabricated span onto the scene as if it were authored.
            //
            // A missing macro geology node is an authoring gap. This tool links and syncs; it does not get
            // to invent the vertical extent of the world.
            HectonSandboxAbyssalShelfMapMagicNode macroBaseNode = FindFirst<HectonSandboxAbyssalShelfMapMagicNode>(graph);
            if (macroBaseNode == null)
            {
                Debug.LogError(
                    "[HectonMacroGeologyBaseIntegrator] No Macro Geology Base node in " +
                    $"{GraphPath}, and this tool will not create one: a new node carries the code-default " +
                    "Y-span (High 2000 / Low -5000 = 7000m), not the authored span. Add " +
                    "'Hecton/Macro Geology Base' in the MapMagic graph editor, set High Y m / Low Y m to " +
                    "the authored world span, save the graph, then run this again. Nothing was changed.");
                return;
            }

            // Unlink Tectonic Node if it is linked to something else
            if (graph.IsLinked(tectonicNode))
            {
                graph.UnlinkInlet(tectonicNode);
                Debug.Log("[HectonMacroGeologyBaseIntegrator] Unlinked old connection to Tectonic Node.");
            }

            // Link Macro Geology Base Node to Tectonic Node
            graph.Link(macroBaseNode, tectonicNode);
            Debug.Log(
                "[HectonMacroGeologyBaseIntegrator] Linked the existing Macro Geology Base Node to the " +
                $"Tectonic Node (authored span {macroBaseNode.highWorldY - macroBaseNode.lowWorldY:F1}m, " +
                $"low {macroBaseNode.lowWorldY:F1}m).");

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
        //  This tool writes the authored span onto the scene object. It NEVER hardcodes the span: it
        //  reads highWorldY/lowWorldY back out of the graph EACH OBJECT ACTUALLY GENERATES FROM
        //  (MapMagicObject.graph, MapMagicObject.cs:38), including that graph's biome sub-graphs, so
        //  the graph stays the single source of truth and this tool cannot write one world's vertical
        //  extent onto a different world.
        //
        //  IMPORTANT - what this does NOT do: it changes globals.height, which is the value MapMagic
        //  applies as TerrainData.size.y on the NEXT generate (HeightOut.cs:265/435/455/496/521). It
        //  does not resize TerrainData assets already baked at 250 m. If the tiles in the scene are
        //  pinned/saved rather than generated on load, the terrain stays 250 m until a regenerate, and
        //  a run of this tool alone is not proof that the world got taller.
        //
        //  WRONG-SCENE TRAP - read before concluding this defect is fixed. A sibling tool,
        //  Hecton8/Diagnostics/Fix MapMagic Height (FixMapMagicHeightTask.cs:12), already writes the
        //  correct pair - globals.height = 12000 at :29 and base Y = -10000 at :33 - but it opens
        //  Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity at :20, NOT the shipping world scene.
        //  It then logs "Successfully updated MapMagicObject" (:39). So that tool can be run to
        //  completion, print success, and leave 02_HECTON_WORLD untouched at 250 m - which is the most
        //  likely reason this mismatch survived while looking repeatedly "fixed". Its hardcoded
        //  12000/-10000 is independent corroboration of the authored span, not a second authority.
        //
        //  Because that trap is live, every write below NAMES THE SCENE IT TOUCHED. A sync log that
        //  does not name the scene cannot distinguish "fixed the shipping world" from "fixed a sandbox",
        //  and that ambiguity is exactly what cost this project the repeat.
        // ══════════════════════════════════════════════════════════════════════════════════════════

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

                if (!TryResolveSpanForMapMagicObject(
                        mapMagicObject,
                        out float lowWorldY,
                        out float highWorldY,
                        out string spanError))
                {
                    Debug.LogError(
                        $"[HectonMacroGeologyBaseIntegrator] REPORT '{mapMagicObject.name}' has no resolvable " +
                        $"authored span - the sync would skip it. {spanError}",
                        mapMagicObject);
                    continue;
                }

                float span = highWorldY - lowWorldY;
                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] REPORT '{mapMagicObject.name}' authored geology: " +
                    $"lowWorldY={lowWorldY:F1}m highWorldY={highWorldY:F1}m span={span:F1}m",
                    mapMagicObject);

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

        /// <summary>
        /// Writes each MapMagicObject's own graph-authored geology Y-span onto that object, for every
        /// MapMagicObject in the currently open scenes. Operates only on what is already loaded - it
        /// deliberately does not open or close scenes, so it cannot discard unsaved work, and it saves only
        /// the scenes it dirtied itself.
        /// </summary>
        [MenuItem("Hecton8/World/MapMagic/Sync Terrain Height To Geology Span")]
        public static void SyncTerrainHeightToGeologySpan()
        {
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

            // Tracks the objects THIS tool changed, so the save pass below writes only the scenes this tool
            // dirtied. The previous version saved any owning scene whose isDirty was set, which silently
            // committed the operator's unrelated unsaved edits in an additively-loaded scene and could
            // re-save the same scene once per MapMagicObject in it.
            bool[] changedByThisTool = new bool[mapMagicObjects.Length];
            int changed = 0;
            int failed = 0;
            for (int i = 0; i < mapMagicObjects.Length; i++)
            {
                MapMagicObject mapMagicObject = mapMagicObjects[i];
                if (mapMagicObject == null)
                    continue;

                // Resolved per object: the span belongs to the graph THAT object generates from, and two
                // MapMagicObjects in the open scenes can legitimately run different graphs.
                if (!TryResolveSpanForMapMagicObject(
                        mapMagicObject,
                        out float lowWorldY,
                        out float highWorldY,
                        out string spanError))
                {
                    failed++;
                    Debug.LogError(
                        $"[HectonMacroGeologyBaseIntegrator] '{mapMagicObject.name}' SKIPPED - {spanError}",
                        mapMagicObject);
                    continue;
                }

                if (!ApplyGeologySpanToMapMagicObject(mapMagicObject, lowWorldY, highWorldY))
                    continue;

                changedByThisTool[i] = true;
                changed++;

                // Mark the scene that actually owns this object rather than iterating a global scene
                // list, so an additively-loaded terrain owner is saved correctly and no unrelated
                // scene is written.
                Scene owningScene = mapMagicObject.gameObject.scene;
                if (owningScene.IsValid())
                    EditorSceneManager.MarkSceneDirty(owningScene);
            }

            if (changed == 0)
            {
                if (failed >= mapMagicObjects.Length)
                {
                    Debug.LogError(
                        $"[HectonMacroGeologyBaseIntegrator] NOTHING was synced: all {failed} MapMagicObject(s) " +
                        "failed to resolve an authored geology span (errors above). This is a failure, not a " +
                        "no-op - do not read it as 'already correct'.");
                    return;
                }

                Debug.Log(
                    "[HectonMacroGeologyBaseIntegrator] No MapMagicObject needed a change; every one that " +
                    $"resolved a span already matches it. Skipped {failed} that could not resolve one. " +
                    "Nothing written.");
                return;
            }

            for (int i = 0; i < mapMagicObjects.Length; i++)
            {
                if (!changedByThisTool[i])
                    continue;

                Scene owningScene = mapMagicObjects[i].gameObject.scene;
                if (!owningScene.IsValid() || !owningScene.isDirty)
                    continue;

                bool alreadySaved = false;
                for (int j = 0; j < i; j++)
                {
                    if (changedByThisTool[j] &&
                        mapMagicObjects[j].gameObject.scene.handle == owningScene.handle)
                    {
                        alreadySaved = true;
                        break;
                    }
                }

                if (!alreadySaved)
                {
                    EditorSceneManager.SaveScene(owningScene);

                    // Name the scene PATH that was actually written. "Saved the owning scene(s)" is the
                    // exact wording that let the sibling tool's sandbox write pass for a world fix.
                    Debug.Log(
                        "[HectonMacroGeologyBaseIntegrator] SAVED scene " +
                        $"'{owningScene.path}'. Confirm this is the scene you intended - a sync of any " +
                        $"scene other than {WorldScenePath} does NOT fix the shipping world.");
                }
            }

            Debug.Log(
                $"[HectonMacroGeologyBaseIntegrator] Synced {changed} MapMagicObject(s) to their authored " +
                $"geology span and saved the owning scene(s); {failed} skipped. Terrain regenerates on next " +
                "load; no regeneration is forced here.");
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
                int failed = 0;
                for (int i = 0; i < mapMagicObjects.Length; i++)
                {
                    MapMagicObject mapMagicObject = mapMagicObjects[i];
                    if (mapMagicObject == null)
                        continue;

                    if (!TryResolveSpanForMapMagicObject(
                            mapMagicObject,
                            out float lowWorldY,
                            out float highWorldY,
                            out string spanError))
                    {
                        failed++;
                        Debug.LogError(
                            $"[HectonMacroGeologyBaseIntegrator] '{mapMagicObject.name}' SKIPPED - {spanError}",
                            mapMagicObject);
                        continue;
                    }

                    if (ApplyGeologySpanToMapMagicObject(mapMagicObject, lowWorldY, highWorldY))
                    {
                        changed++;
                        Debug.Log(
                            $"[HectonMacroGeologyBaseIntegrator] '{mapMagicObject.name}' synced to authored " +
                            $"span {highWorldY - lowWorldY:F1}m, base {lowWorldY:F1}m.",
                            mapMagicObject);
                    }
                }

                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] Headless sync complete. Changed {changed} of " +
                    $"{mapMagicObjects.Length} MapMagicObject(s); {failed} could not resolve an authored span.");

                // A build gate reads the exit code, not the log. An object whose span could not be resolved
                // was NOT synced, so the sync is unproven and must not exit 0.
                if (failed > 0)
                    exitCode = 1;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[HectonMacroGeologyBaseIntegrator] Headless sync threw: {exception}");
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Resolves the authored span for ONE MapMagicObject from the graph that object actually
        /// generates from (<see cref="MapMagicObject.graph"/>, MapMagicObject.cs:38).
        ///
        /// The span used to come from the <see cref="GraphPath"/> constant while the value was written to
        /// whatever MapMagicObject happened to be in the open scenes. Those are two independent things: if
        /// the object runs a different graph, the tool writes one world's vertical extent onto another
        /// world and reports success. Hardcoded graph paths in this project do drift - CreateSandboxV2.cs:26
        /// still assigns "Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset", which
        /// no longer exists at that path, so it silently assigns null.
        /// </summary>
        private static bool TryResolveSpanForMapMagicObject(
            MapMagicObject mapMagicObject,
            out float lowWorldY,
            out float highWorldY,
            out string error)
        {
            lowWorldY = 0f;
            highWorldY = 0f;

            Graph objectGraph = mapMagicObject != null ? mapMagicObject.graph : null;
            if (objectGraph != null)
            {
                string label = $"graph '{AssetDatabase.GetAssetPath(objectGraph)}' assigned to '{mapMagicObject.name}'";
                return TryResolveAuthoredGeologySpan(objectGraph, label, out lowWorldY, out highWorldY, out error);
            }

            // A MapMagicObject with no graph generates nothing, so it has no authored span of its own. Read
            // the constant path so the operator still sees numbers, but say plainly that they did not come
            // from the object being written - a silent fallback here is exactly how the wrong world's height
            // gets cemented.
            Debug.LogWarning(
                $"[HectonMacroGeologyBaseIntegrator] '{(mapMagicObject != null ? mapMagicObject.name : "<null>")}' " +
                $"has no graph assigned, so its authored span cannot be read from it. Falling back to {GraphPath}, " +
                "which this object does NOT generate from - treat the result as unverified.",
                mapMagicObject);

            Graph fallbackGraph = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
            return TryResolveAuthoredGeologySpan(
                fallbackGraph,
                $"fallback graph {GraphPath}",
                out lowWorldY,
                out highWorldY,
                out error);
        }

        /// <summary>
        /// Reads highWorldY/lowWorldY from <paramref name="rootGraph"/> and every biome sub-graph under it.
        /// Every macro geology node found must agree; a disagreement is reported instead of silently
        /// picking the first one, because picking a stale duplicate would bake the wrong world height into
        /// the scene.
        ///
        /// Sub-graphs are included because the previous version scanned only the root generator array and
        /// said so in its own error text. That made "no macro geology node" ambiguous between "absent" and
        /// "present one level down", and a node inside a biome sub-graph generates real terrain.
        /// </summary>
        private static bool TryResolveAuthoredGeologySpan(
            Graph rootGraph,
            string graphLabel,
            out float lowWorldY,
            out float highWorldY,
            out string error)
        {
            lowWorldY = 0f;
            highWorldY = 0f;
            error = null;

            if (rootGraph == null)
            {
                error = $"Graph not found ({graphLabel}); cannot resolve the authored geology span.";
                return false;
            }

            if (rootGraph.generators == null)
            {
                error = $"Graph has a null generator array ({graphLabel}); cannot resolve the authored geology span.";
                return false;
            }

            int found = 0;
            if (!AccumulateGeologySpan(rootGraph, graphLabel, ref found, ref lowWorldY, ref highWorldY, out error))
                return false;

            foreach (Graph subGraph in rootGraph.SubGraphs(recursively: true))
            {
                if (!AccumulateGeologySpan(subGraph, graphLabel, ref found, ref lowWorldY, ref highWorldY, out error))
                    return false;
            }

            if (found == 0)
            {
                error =
                    $"No HectonSandboxAbyssalShelfMapMagicNode in {graphLabel}, including its biome " +
                    "sub-graphs. There is no authored geology span to write.";
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

        /// <summary>
        /// Folds one graph's macro geology nodes into the running span. <paramref name="found"/> carries
        /// across graphs so a node in the root and a node in a sub-graph are compared against each other,
        /// not resolved independently.
        /// </summary>
        private static bool AccumulateGeologySpan(
            Graph graph,
            string graphLabel,
            ref int found,
            ref float lowWorldY,
            ref float highWorldY,
            out string error)
        {
            error = null;

            Generator[] generators = graph != null ? graph.generators : null;
            if (generators == null)
                return true;

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
                        $"{graphLabel} contains {found} macro geology nodes with DISAGREEING Y-spans " +
                        $"(first {lowWorldY:F1}..{highWorldY:F1}, this one in '{graph.name}' " +
                        $"{node.lowWorldY:F1}..{node.highWorldY:F1}). Refusing to guess which one is " +
                        "authoritative - resolve the duplicate in the graph first.";
                    return false;
                }
            }

            return true;
        }

        private static bool ApplyGeologySpanToMapMagicObject(MapMagicObject mapMagicObject, float lowWorldY, float highWorldY)
        {
            if (mapMagicObject == null || mapMagicObject.globals == null)
                return false;

            float span = highWorldY - lowWorldY;
            bool changed = false;

            // The scene name is part of every write line below, not a nicety: FixMapMagicHeightTask
            // (see the WRONG-SCENE TRAP note above) wrote the right numbers to the wrong scene and
            // reported success. A log that names only the object cannot catch that.
            Scene owningScene = mapMagicObject.gameObject.scene;
            string sceneLabel = owningScene.IsValid() ? owningScene.name : "<no scene>";

            float previousHeight = mapMagicObject.globals.height;
            if (Mathf.Abs(previousHeight - span) > HeightMatchToleranceMeters)
            {
                mapMagicObject.globals.height = span;
                changed = true;
                Debug.Log(
                    $"[HectonMacroGeologyBaseIntegrator] scene '{sceneLabel}' / {mapMagicObject.name}: " +
                    $"globals.height {previousHeight:F1}m -> {span:F1}m (authored geology span).",
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
                    $"[HectonMacroGeologyBaseIntegrator] scene '{sceneLabel}' / {mapMagicObject.name}: " +
                    $"terrain base Y {previousBase:F1}m -> {lowWorldY:F1}m (authored LowWorldY). This MOVES " +
                    "the terrain in world space; water level, spawn height and any authored world-space " +
                    "props must be re-checked against the new base.",
                    mapMagicObject);
            }

            if (changed)
                EditorUtility.SetDirty(mapMagicObject);

            return changed;
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
