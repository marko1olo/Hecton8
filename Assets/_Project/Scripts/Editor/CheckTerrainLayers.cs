using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// VERIFIER. Reads <see cref="TerrainData.terrainLayers"/> on every <see cref="Terrain"/> in the open
/// scenes and fails when the layer list cannot texture the terrain: no terrain, no TerrainData, a null
/// array, a zero-length array, or a null entry inside it.
///
/// WHAT WAS WRONG - the old fifteen-line version could not fail. Every path reached the same
/// <c>EditorApplication.Exit(0)</c>:
///
/// * THE SUCCESS EXIT SAT OUTSIDE THE GUARD. The whole body was inside <c>if (terrains.Length &gt; 0)</c>
///   (old <c>:6</c>) while <c>Exit(0)</c> sat outside it (old <c>:13</c>), so a scene with no terrain
///   printed NOTHING AT ALL and reported success. That is the most likely real invocation of this tool:
///   under <c>-batchmode -executeMethod</c> no scene is loaded unless one is passed, so
///   <see cref="Object.FindObjectsByType{T}()"/> returns an empty array and the <c>foreach</c> body never
///   runs. Identical shape to the one fixed in <c>CheckAlphamaps.cs:15-18</c>;
/// * THE COUNT WAS LOGGED AND NEVER TESTED. <c>"Terrain Layers Count: 0"</c> was a perfectly acceptable
///   line of output (old <c>:8</c>) followed by exit 0. A terrain with zero splat layers cannot be
///   textured at all - <c>HectonTerrainMaterialInjector.cs:121</c> feeds that same count into the
///   shader's <c>_NumLayersCount</c>, so zero layers means zero array slices are addressed;
/// * A NULL LAYER WAS PRINTED AS THE STRING "null" AND PASSED (old <c>:10</c>). The tool formatted the
///   exact evidence of the defect into the log and then exited 0;
/// * <c>terrains[0].terrainData</c> (old <c>:7</c>) was dereferenced with no null check, and
///   <c>layers.Length</c> (old <c>:8</c>) with no null check on the array. There was no try/catch, so
///   either <see cref="System.NullReferenceException"/> left <c>Execute</c> with NO exit code set - which
///   under <c>-quit</c> ends the process at 0;
/// * ONLY <c>terrains[0]</c> WAS EXAMINED while this project generates a 9-tile grid, and the log said
///   "Terrain Layers Count" as though it spoke for the terrain as a whole. All terrains are now iterated
///   to completion so a broken second tile cannot hide behind a healthy first one;
/// * there was no output file, and the log tag was <c>[FAS]</c> - a fossil of <c>FixAndShoot</c>. Filtering
///   this project's batchmode log by <c>[FAS]</c> is itself recorded as having concealed a real magenta
///   fallback-shader failure (<c>Docs/AgentLogs/Current_Dialog_Dump.md:18143</c>), so the tag is now the
///   tool name and errors go through <see cref="Debug.LogError"/> where no tag filter can hide them.
///
/// SCOPE, stated so the output is not over-read. This checks the LAYER LIST - that the entries exist and
/// are not null. It does NOT prove the terrain is correctly textured:
///
/// * <c>TerrainLayer.diffuseTexture</c> is reported but NEVER FAILED ON, and that is deliberate rather
///   than laziness. This project's terrain shader does not sample per-layer <c>_Splat0..3</c>; it samples
///   the texture arrays <c>_AlbedoArray</c>/<c>_NormalArray</c>/<c>_MaskArray</c>
///   (<c>Assets/_Project/Shaders/HectonTerrainSampling.hlsl:9-13</c>, and
///   <c>HectonTerrainLitPasses.hlsl:149</c> records that "the old functions sampled _Splat0..3"). A layer
///   with no diffuse texture therefore does not necessarily break the Hecton render, and failing on it
///   would refuse correct terrain. It is still worth printing, because it is how layer identity is
///   authored and it does break the built-in fallback path;
/// * <c>TerrainLayer.tileSize</c> is likewise informational: the Hecton shader has no per-layer tiling
///   property, only the global <c>_HectonUVScale</c> (<c>HectonTerrain.shader:47</c>);
/// * whether the ARRAY actually has a slice per layer is <c>CheckMat</c>/<c>CheckSceneMat</c>/
///   <c>CheckTexArrays</c> territory - this file never opens a material, so it cannot and does not claim
///   the layer count matches the bound array depth;
/// * visual acceptance is <c>Docs/QUALITY_GATES.md</c>'s job, never this file's.
/// </summary>
public static class CheckTerrainLayers
{
    private const string ToolName = "CheckTerrainLayers";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is
    /// not a compile-time constant (CS0133). The old version wrote no file at all.
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_terrain_layers");

    private static readonly string ReportPath =
        Path.Combine(OutputDir, "terrain_layers_report.txt");

    public static void Execute()
    {
        // PART 4 (GPU refusal) DELIBERATELY OMITTED, decided from the code in this file. Everything read
        // here is a serialized reference or an int: TerrainData.terrainLayers, TerrainLayer.name,
        // .diffuseTexture, .tileSize, TerrainData.alphamapLayers. There is no Blit, no RenderTexture, no
        // GetPixels/ReadPixels, no EncodeToPNG and no compute dispatch, so the values are identical with
        // and without a graphics device and a graphicsDeviceType gate would refuse runs that are
        // perfectly valid. This is NOT the CheckAlphamaps.cs:76-84 case: that tool reads back alphamap
        // WEIGHT DATA, which comes out as zeros with no device. Note also that if a caller runs this
        // straight after a -nographics generation, a zero-layer result here is a true reading of a real
        // broken state and correctly FAILS - it does not fabricate a pass. Do not copy a gate in here.

        StringBuilder report = new StringBuilder();
        report.AppendLine($"{ToolName} report");

        int terrainsExamined = 0;
        int terrainsHealthy = 0;
        int layersExamined = 0;
        int layersUsable = 0;
        int failures = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            // Recorded up front so a zero-terrain run says WHICH scene was searched. "No terrain found"
            // is the same message for a stale path, the wrong scene and an empty batchmode session, and
            // without the scene list the reader cannot tell those apart.
            StringBuilder openScenes = new StringBuilder();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
                if (openScenes.Length > 0)
                    openScenes.Append(", ");

                openScenes.Append(scene.IsValid()
                    ? $"'{scene.name}' ({(string.IsNullOrEmpty(scene.path) ? "unsaved" : scene.path)}, " +
                      $"loaded={scene.isLoaded})"
                    : "<invalid scene>");
            }

            string sceneList = openScenes.Length > 0 ? openScenes.ToString() : "<no scenes open>";
            report.AppendLine($"open scenes: {sceneList}");

            // FindObjectsInactive.Include, matching CheckSceneMat.cs:108-109. The old call used the
            // default overload, which EXCLUDES inactive objects - so "no terrain" could actually have
            // meant "the terrain is there and disabled", and a disabled terrain still carries the layer
            // list that gets shipped.
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            int terrainCount = terrains == null ? 0 : terrains.Length;
            report.AppendLine($"terrains found (inactive included): {terrainCount}");

            if (terrainCount == 0)
            {
                // Was: no log line whatsoever, then Exit(0).
                Fail(report,
                    "found 0 Terrain objects, so no terrainLayers array was read and nothing at all is " +
                    $"verified. Open scenes were: {sceneList}. Under -batchmode -executeMethod no scene " +
                    "is loaded unless one is passed explicitly; this project's terrain may also be " +
                    "streamed in by MapMagic at runtime rather than authored into the scene. Inactive " +
                    "terrains were included in the search, so this is not a disabled-object miss.");
                return;
            }

            foreach (Terrain terrain in terrains)
            {
                if (terrain == null)
                    continue;

                terrainsExamined++;
                bool terrainOk = true;
                report.AppendLine(
                    $"terrain '{terrain.name}' (scene '{terrain.gameObject.scene.name}', " +
                    $"activeInHierarchy={terrain.gameObject.activeInHierarchy}):");

                TerrainData terrainData = terrain.terrainData;
                if (terrainData == null)
                {
                    // Was: dereferenced blind, so this threw out of Execute with no exit code set.
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: terrain '{terrain.name}' has no TerrainData, so it has no " +
                        "terrainLayers to read. No layer verdict is possible for this terrain.");
                    report.AppendLine("  terrainData: NULL - no layers to read");
                    continue;
                }

                TerrainLayer[] layers = terrainData.terrainLayers;
                if (layers == null)
                {
                    // Was: layers.Length dereferenced blind.
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: terrain '{terrain.name}' returned a NULL terrainLayers " +
                        "array. It has no splat layers at all, so nothing textures it.");
                    report.AppendLine("  terrainLayers: NULL ARRAY");
                    continue;
                }

                report.AppendLine($"  terrainLayers count: {layers.Length}");
                Debug.Log(
                    $"[{ToolName}] terrain '{terrain.name}': terrainLayers count {layers.Length}, " +
                    $"alphamapLayers {terrainData.alphamapLayers}");

                if (layers.Length == 0)
                {
                    // THE CHECK THE OLD TOOL NEVER MADE. It printed "Terrain Layers Count: 0" and exited
                    // 0. Zero layers means zero array slices are addressed - see _NumLayersCount at
                    // HectonTerrainMaterialInjector.cs:121 - so the terrain cannot be textured.
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: terrain '{terrain.name}' has 0 terrainLayers. A terrain " +
                        "with no splat layers cannot be textured; the shader's _NumLayersCount comes " +
                        "from this count (HectonTerrainMaterialInjector.cs:121), so no texture array " +
                        "slice is addressed at all. This is the branch that used to print a 0 and pass.");
                    report.AppendLine("  terrainLayers: EMPTY");
                    continue;
                }

                // alphamapLayers and terrainLayers.Length are Unity's own two views of the same list.
                // Reported as a WARNING, not a failure: it is worth naming loudly, but making it fatal
                // risks refusing a terrain caught mid-resize, and this tool is not the owner of that
                // invariant.
                if (terrainData.alphamapLayers != layers.Length)
                {
                    Debug.LogWarning(
                        $"[{ToolName}] terrain '{terrain.name}' reports alphamapLayers=" +
                        $"{terrainData.alphamapLayers} but terrainLayers.Length={layers.Length}. The " +
                        "splat weight tables and the layer list disagree, so layer indices and weight " +
                        "channels may not line up. Not failed on here - CheckAlphamaps owns the weight " +
                        "side.");
                    report.AppendLine(
                        $"  WARNING: alphamapLayers={terrainData.alphamapLayers} != " +
                        $"terrainLayers.Length={layers.Length}");
                }

                for (int i = 0; i < layers.Length; i++)
                {
                    layersExamined++;
                    TerrainLayer layer = layers[i];

                    if (layer == null)
                    {
                        // Was: formatted as the literal string "null" into a Debug.Log and passed.
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: terrain '{terrain.name}' layer {i} is NULL. The layer " +
                            "list claims a splat channel that has no layer behind it, so nothing " +
                            "supplies identity or an array index for it while the count still reports " +
                            "it as present. The old tool printed this exact case as \"null\" and exited " +
                            "0.");
                        report.AppendLine($"  layer {i}: NULL ENTRY");
                        continue;
                    }

                    layersUsable++;

                    // INFORMATIONAL ONLY - see the SCOPE note on the class. The Hecton shader samples
                    // _AlbedoArray, not this layer's diffuseTexture
                    // (HectonTerrainSampling.hlsl:9-13), so a missing diffuse does not necessarily break
                    // the render and must not fail the run. It is still printed because it is how layer
                    // identity is authored and it does break the built-in fallback.
                    string diffuse = layer.diffuseTexture != null
                        ? layer.diffuseTexture.name
                        : "<none>";
                    string normal = layer.normalMapTexture != null
                        ? layer.normalMapTexture.name
                        : "<none>";
                    string mask = layer.maskMapTexture != null
                        ? layer.maskMapTexture.name
                        : "<none>";
                    string assetPath = AssetDatabase.GetAssetPath(layer);

                    report.AppendLine(
                        $"  layer {i}: '{layer.name}' diffuse={diffuse} normal={normal} mask={mask} " +
                        $"tileSize={layer.tileSize} asset={(string.IsNullOrEmpty(assetPath) ? "<runtime instance>" : assetPath)}");
                    Debug.Log($"[{ToolName}] terrain '{terrain.name}' layer {i}: '{layer.name}' diffuse={diffuse}");

                    if (layer.diffuseTexture == null)
                    {
                        Debug.LogWarning(
                            $"[{ToolName}] terrain '{terrain.name}' layer {i} '{layer.name}' has no " +
                            "diffuseTexture. Not failed on: this project's terrain shader samples " +
                            "_AlbedoArray rather than per-layer _Splat0..3 " +
                            "(HectonTerrainSampling.hlsl:9-13), so this does not by itself prove a " +
                            "broken render - but it does break the built-in fallback path and it is how " +
                            "layer identity is authored.");
                    }

                    if (layer.tileSize.x <= 0f || layer.tileSize.y <= 0f)
                    {
                        Debug.LogWarning(
                            $"[{ToolName}] terrain '{terrain.name}' layer {i} '{layer.name}' has " +
                            $"tileSize={layer.tileSize}, which is degenerate. Not failed on: the Hecton " +
                            "shader has no per-layer tiling property, only the global _HectonUVScale " +
                            "(HectonTerrain.shader:47), so this value does not reach the rendered " +
                            "terrain on this project's path.");
                    }
                }

                report.AppendLine($"  VERDICT: {(terrainOk ? "ok" : "FAILED")}");
                if (terrainOk)
                    terrainsHealthy++;
            }

            report.AppendLine(
                $"examined {terrainsExamined} terrain(s) and {layersExamined} layer entr(ies); " +
                $"{layersUsable} non-null, {terrainsHealthy} terrain(s) fully correct, {failures} failure(s)");
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all, so a throw here set no exit code whatsoever and -quit ended the
            // process at 0.
            Fail(report,
                $"threw before a complete terrain layer report was produced; {terrainsExamined} " +
                $"terrain(s) and {layersExamined} layer entr(ies) had been examined, so no layer verdict " +
                $"stands. {ex}");
            return;
        }

        // Exit 0 has to mean the evidence exists on disk, not merely that no branch threw.
        WriteReport(report);
        if (!File.Exists(ReportPath) || new FileInfo(ReportPath).Length == 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: the report at {ReportPath} is missing or empty after the write, " +
                "so this run produced no evidence even though the checks ran.");
            EditorApplication.Exit(2);
            return;
        }

        // Zero examined means a stale path, the wrong scene or a renamed object - every time. Guarded
        // separately from `failures` because zero terrains also yields zero failures.
        if (terrainsExamined == 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: examined 0 terrains. Nothing about the terrain layer list is " +
                $"verified by this run. Report at {ReportPath}.");
            EditorApplication.Exit(2);
            return;
        }

        if (layersExamined == 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: examined {terrainsExamined} terrain(s) but 0 layer entries, so no " +
                $"layer was actually inspected. Report at {ReportPath}.");
            EditorApplication.Exit(2);
            return;
        }

        if (failures > 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: {failures} problem(s) across {terrainsExamined} terrain(s) and " +
                $"{layersExamined} layer entr(ies); only {terrainsHealthy} terrain(s) are fully correct. " +
                $"Details in {ReportPath}.");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log(
            $"[{ToolName}] PASS: all {terrainsExamined} terrain(s) carry a non-empty terrainLayers array " +
            $"with no null entries - {layersExamined} layer entr(ies) examined, all non-null. Report: " +
            $"{ReportPath}. This proves the LAYER LIST exists; it does not prove the layers are textured " +
            "correctly (CheckMat/CheckSceneMat own the arrays, CheckAlphamaps owns the weights).");
        EditorApplication.Exit(0);
    }

    /// <summary>Writes the failure into the report as well as the log, then exits 2.</summary>
    private static void Fail(StringBuilder report, string message)
    {
        report.AppendLine($"RESULT: FAILED - {message}");
        WriteReport(report);
        Debug.LogError($"[{ToolName}] FAILED: {message} Report at {ReportPath}");
        EditorApplication.Exit(2);
    }

    /// <summary>
    /// A report-write failure must not replace the real verdict, so this swallows its own IO error and
    /// says so in the log rather than throwing out of a failure path.
    /// </summary>
    private static void WriteReport(StringBuilder report)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(ReportPath, report.ToString());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[{ToolName}] could not write {ReportPath}: {ex.Message}");
        }
    }
}
