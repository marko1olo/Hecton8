using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// DUMPER. Opens each scene in <see cref="ScenePaths"/> and writes what the terrain in it actually is:
/// TerrainData, material template and shader, size, heightmap resolution, splat layers, alphamap texture
/// count, plus the MapMagic objects and cameras present. It answers "what is in the scene", never "is the
/// scene correct" - the verdicts on material and slot binding belong to <c>CheckSceneMat.cs</c> and
/// <c>CheckMat.cs</c>, and visual acceptance to <c>Docs/QUALITY_GATES.md</c>.
///
/// WHAT WAS WRONG - the single <c>EditorApplication.Exit(0)</c> at old <c>:77</c> sat outside every loop and
/// every guard, so this tool could not report a failure of any kind:
///
/// * EXISTENCE WAS TESTED WITH A RELATIVE PATH. Old <c>:18</c> called
///   <c>System.IO.File.Exists("Assets/_Project/Scenes/...")</c>, which resolves against the process working
///   directory. If that is ever not the project root, all three scenes "do not exist", the tool opens
///   nothing, examines nothing, and exits 0 having dumped an empty diagnosis. The check is now
///   <see cref="AssetDatabase.LoadAssetAtPath{T}"/> against <see cref="SceneAsset"/>, which asks the
///   AssetDatabase and does not depend on the working directory;
/// * A MISSING SCENE WAS <see cref="Debug.Log"/> AND <c>continue</c> (old <c>:20</c>). "Scene not found" is
///   the signature of a renamed or moved scene - the roster going stale is the whole risk of a hardcoded
///   path list - and it was reported at the same severity as ordinary output, on the way to exit 0. All
///   three paths do currently resolve, so this is a live trap rather than a present failure;
/// * ZERO TERRAINS WAS A PASS. Old <c>:28</c> printed <c>"Active terrains: 0"</c> and the <c>foreach</c> at
///   old <c>:30</c> simply never ran. Nothing else in the tool depended on having found a terrain, so a
///   terrain diagnostic that never saw a terrain exited 0. Zero is now fatal, and the counts are logged;
/// * <c>Terrain.activeTerrains</c> (old <c>:27</c>) SKIPS DISABLED TERRAINS. "Active terrains: 0" therefore
///   could not distinguish "this scene has no terrain" from "the terrain is there and switched off". The
///   enumeration now uses <c>FindObjectsInactive.Include</c>, matching <c>CheckSceneMat.cs:106-109</c>, and
///   reports the active count separately so the difference is visible instead of collapsed;
/// * NULL WAS PRINTED AND WALKED PAST. <c>terrainData=NULL</c> (old <c>:33</c>) and
///   <c>materialTemplate=NULL</c> (old <c>:34</c>) were formatted into the log and led to the same exit 0.
///   A terrain with no TerrainData contributes nothing to the dump, so "not one terrain had TerrainData" is
///   now fatal; a missing material template is a defect this tool reports and CheckSceneMat judges;
/// * <c>t.materialTemplate.shader.name</c> (old <c>:34</c>) dereferenced <c>shader</c> with no null check.
///   With no try/catch anywhere in the file, that throw would leave <see cref="EditorApplication.Exit"/>
///   never called and the exit code to Unity's own shutdown path, exactly as fixed in
///   <c>CheckAlphamaps.cs:26-29</c>;
/// * NOTHING WAS WRITTEN TO DISK. Every line went to <see cref="Debug.Log"/> only, so the dump survived
///   solely inside whatever <c>-logFile</c> the launcher happened to point at, interleaved with the
///   asset pipeline. There is now a per-tool report under <c>Logs/diag_terrain/</c>.
///
/// SCOPE, so the output is not over-read: <see cref="EditorSceneManager.OpenScene"/> with
/// <see cref="OpenSceneMode.Single"/> DISCARDS unsaved modifications to the currently open scene. This is a
/// batchmode entry point called by reflection from <c>Tools/BatchTasks</c>; do not run it in an interactive
/// editor that holds unsaved work. It also reports the terrain as it is the moment the scene finishes
/// opening - MapMagic generation is asynchronous, so this is authored state, not generated state.
/// </summary>
public static class DiagTerrain
{
    private const string ToolName = "DiagTerrain";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is not
    /// a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "diag_terrain");

    private static readonly string ReportPath = Path.Combine(OutputDir, "terrain_dump.txt");

    /// <summary>
    /// All three resolve on disk today. Kept in the original order and unchanged: this roster is what the
    /// existing batch callers expect to see dumped. <c>020_RENDER_SANDBOX_V2.unity</c> also exists and is
    /// deliberately NOT added - widening the roster is a caller's decision, not a bug fix.
    /// </summary>
    private static readonly string[] ScenePaths =
    {
        "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity",
        "Assets/_Project/Scenes/010_TEST.unity"
    };

    public static void Execute()
    {
        // PART 4 DELIBERATELY OMITTED. This tool reads component and asset references only - TerrainData
        // fields, materialTemplate, TerrainLayer names, array lengths, transforms. No Blit, no readback, no
        // GetPixels, no EncodeToPNG, no compute dispatch, so every value it prints is identical with and
        // without a GPU context and a graphicsDeviceType gate would refuse valid runs. The one
        // device-sensitive number is the alphamap TEXTURE COUNT: sibling CheckAlphamaps.cs:69-84 refuses
        // without a device because the alphamap DATA reads back as zeros there. This tool never reads that
        // data, only counts the texture objects, so instead of a gate it records graphicsDeviceType in the
        // report and marks that one line device-dependent. Nothing here may be read as evidence about
        // alphamap CONTENT - that is CheckAlphamaps' job, with its gate.
        StringBuilder report = new StringBuilder();
        int scenesMissing = 0;
        int scenesOpened = 0;
        int terrainsExamined = 0;
        int terrainsWithData = 0;
        int defectsReported = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            report.AppendLine($"{ToolName} report");
            report.AppendLine(
                $"graphicsDeviceType: {SystemInfo.graphicsDeviceType} " +
                $"(batchMode={Application.isBatchMode})");
            report.AppendLine($"scenes requested: {ScenePaths.Length}");

            foreach (string scenePath in ScenePaths)
            {
                // Was: File.Exists on a relative path, which silently answers "no" from the wrong working
                // directory. The AssetDatabase answer is also the one OpenScene needs to succeed.
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (sceneAsset == null)
                {
                    scenesMissing++;
                    report.AppendLine($"=== SCENE: {scenePath} === NOT IN ASSETDATABASE");
                    // Was: Debug.Log, then continue, then exit 0.
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '{scenePath}' is not a scene asset in this project, so it " +
                        "was not opened and nothing in it was dumped. The path list in this tool is stale, " +
                        "or the scene was renamed, moved or deleted.");
                    continue;
                }

                report.AppendLine($"=== SCENE: {scenePath} ===");
                Debug.Log($"[{ToolName}] === SCENE: {scenePath} ===");

                // Initialised so the read after the try/catch cannot depend on definite-assignment
                // analysis of the catch block. The catch always continues, so this value is never read.
                UnityEngine.SceneManagement.Scene opened = default;
                try
                {
                    // Mode stated explicitly. Single is what the old parameterless call did by default, and
                    // it is what makes the per-scene counts below mean one scene rather than an accumulated
                    // pile. It also discards unsaved changes - see the class summary.
                    opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                catch (System.Exception ex)
                {
                    scenesMissing++;
                    report.AppendLine($"  OPEN FAILED: {ex.GetType().Name} {ex.Message}");
                    Debug.LogError(
                        $"[{ToolName}] FAILED: OpenScene('{scenePath}') threw " +
                        $"{ex.GetType().Name}: {ex.Message}. Nothing in that scene was dumped.");
                    continue;
                }

                if (!opened.IsValid() || !opened.isLoaded)
                {
                    scenesMissing++;
                    report.AppendLine(
                        $"  OPEN FAILED: valid={opened.IsValid()} loaded={opened.isLoaded}");
                    Debug.LogError(
                        $"[{ToolName}] FAILED: '{scenePath}' did not load (valid={opened.IsValid()}, " +
                        $"loaded={opened.isLoaded}). Any object query against it would return empty, which " +
                        "is indistinguishable from an empty scene.");
                    continue;
                }

                scenesOpened++;
                DumpScene(report, ref terrainsExamined, ref terrainsWithData, ref defectsReported);
            }
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all. A throw left no exit code set by this tool.
            report.AppendLine(
                $"RESULT: FAILED - threw after opening {scenesOpened} scene(s) and examining " +
                $"{terrainsExamined} terrain(s).");
            WriteReport(report);
            Debug.LogError(
                $"[{ToolName}] FAILED: no complete terrain dump was produced (intended {ReportPath}); " +
                $"{scenesOpened} scene(s) opened and {terrainsExamined} terrain(s) examined before the " +
                $"throw. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        // A dumper that opened nothing dumped nothing. Under -executeMethod no scene is open unless it is
        // opened here, so this is the branch that fires when the whole roster fails to resolve.
        if (scenesOpened == 0)
        {
            Fail(report,
                $"opened 0 of {ScenePaths.Length} requested scene(s), so no terrain, material, layer or " +
                "camera was examined. This run says nothing about the terrain.");
            return;
        }

        // Zero terrains across every scene means a stale roster, the wrong scenes, or terrain that only
        // exists after generation - never a healthy result for a terrain dumper.
        if (terrainsExamined == 0)
        {
            Fail(report,
                $"examined 0 Terrain objects across {scenesOpened} opened scene(s) " +
                $"({string.Join(", ", ScenePaths)}). Inactive terrains were included in the search, so " +
                "they are not hiding disabled. Nothing about the terrain was dumped.");
            return;
        }

        // Every terrain having null TerrainData is the same vacuum one level down: names and transforms
        // were printed, and not one height, size, layer or splat number came from anywhere.
        if (terrainsWithData == 0)
        {
            Fail(report,
                $"none of the {terrainsExamined} Terrain object(s) found has TerrainData, so no size, " +
                "heightmap resolution, splat layer or alphamap figure in this dump has any backing data.");
            return;
        }

        if (scenesMissing > 0)
        {
            Fail(report,
                $"{scenesMissing} of {ScenePaths.Length} requested scene(s) could not be opened. The " +
                $"{terrainsExamined} terrain(s) from the {scenesOpened} scene(s) that did open are in the " +
                "report, but this is a partial dump and the missing scenes were not examined at all.");
            return;
        }

        report.AppendLine(
            $"RESULT: PASS - {scenesOpened}/{ScenePaths.Length} scene(s) opened, {terrainsExamined} " +
            $"terrain(s) examined, {terrainsWithData} with TerrainData, {defectsReported} defect(s) " +
            "reported for CheckSceneMat/CheckMat to judge.");
        WriteReport(report);

        if (!File.Exists(ReportPath) || new FileInfo(ReportPath).Length == 0)
        {
            // Exit 0 has to mean the evidence exists on disk, not merely that no branch threw.
            Debug.LogError(
                $"[{ToolName}] FAILED: the dump at {ReportPath} is missing or empty after the write, so " +
                "this run produced no durable evidence.");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log(
            $"[{ToolName}] PASS: dumped {terrainsExamined} terrain(s) ({terrainsWithData} with " +
            $"TerrainData) from {scenesOpened} scene(s); {defectsReported} defect(s) reported. This is a " +
            "DUMP of authored state, not a verdict: material and slot correctness is CheckSceneMat's, " +
            $"alphamap content is CheckAlphamaps', visual acceptance is Docs/QUALITY_GATES.md's. Report: " +
            $"{ReportPath}");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Dumps the currently open scene. Counters are passed by reference so the aggregate gates in
    /// <see cref="Execute"/> see every scene, not just the last one.
    /// </summary>
    private static void DumpScene(
        StringBuilder report, ref int terrainsExamined, ref int terrainsWithData, ref int defectsReported)
    {
        // Was: Terrain.activeTerrains, which omits a disabled terrain entirely. Include is what makes
        // "0 found" mean "absent" instead of "absent or switched off"; CheckSceneMat.cs:106-109 does the
        // same for the same reason.
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        int foundCount = terrains == null ? 0 : terrains.Length;
        int activeCount = Terrain.activeTerrains == null ? 0 : Terrain.activeTerrains.Length;

        report.AppendLine($"  terrains: {foundCount} (Terrain.activeTerrains reports {activeCount})");
        Debug.Log(
            $"[{ToolName}] terrains including inactive: {foundCount}; active only: {activeCount}");

        if (foundCount > activeCount)
        {
            // Named rather than left as two numbers: a disabled terrain renders nothing, and every sibling
            // tool built on Terrain.activeTerrains is blind to it.
            defectsReported++;
            Debug.LogWarning(
                $"[{ToolName}] {foundCount - activeCount} terrain(s) are present but INACTIVE (disabled " +
                "component or inactive GameObject). They render nothing, and tools that enumerate " +
                "Terrain.activeTerrains cannot see them at all.");
            report.AppendLine($"  DEFECT: {foundCount - activeCount} terrain(s) present but inactive");
        }

        for (int i = 0; i < foundCount; i++)
        {
            Terrain t = terrains[i];
            if (t == null)
                continue;

            terrainsExamined++;

            report.AppendLine(
                $"  terrain '{t.name}' pos={t.transform.position} " +
                $"activeInHierarchy={t.gameObject.activeInHierarchy} enabled={t.enabled}");
            Debug.Log(
                $"[{ToolName}] Terrain: {t.name} pos={t.transform.position} " +
                $"activeInHierarchy={t.gameObject.activeInHierarchy} enabled={t.enabled}");

            Material material = t.materialTemplate;
            if (material == null)
            {
                // Was: printed as "materialTemplate=NULL" on the way to exit 0. Reported here and failed on
                // by CheckSceneMat.cs:134-143, which owns that verdict.
                defectsReported++;
                report.AppendLine("    materialTemplate: NONE BOUND (defect - CheckSceneMat owns this verdict)");
                Debug.LogWarning(
                    $"[{ToolName}] terrain '{t.name}' has no materialTemplate, so it renders with Unity's " +
                    "built-in terrain material and none of the authored texture arrays reach the screen. " +
                    "CheckSceneMat is the tool that fails on this.");
            }
            else
            {
                // Was: material.shader dereferenced with no null check, in a file with no try/catch.
                Shader shader = material.shader;
                string shaderName = shader != null ? shader.name : "NULL";
                report.AppendLine($"    materialTemplate: '{material.name}' shader='{shaderName}'");
                Debug.Log($"[{ToolName}]   materialTemplate={material.name} shader={shaderName}");

                if (shader == null || shaderName.Contains("InternalErrorShader"))
                {
                    defectsReported++;
                    Debug.LogWarning(
                        $"[{ToolName}] terrain '{t.name}' material '{material.name}' resolves to " +
                        $"'{shaderName}' - its shader is missing or failed to compile, so the terrain " +
                        "renders as an error surface. CompileShader and CheckSceneMat judge this.");
                    report.AppendLine("    DEFECT: missing or error shader");
                }
            }

            TerrainData td = t.terrainData;
            if (td == null)
            {
                // Was: printed as "terrainData=NULL", then every number below was simply skipped.
                defectsReported++;
                report.AppendLine("    terrainData: NULL - no size, heightmap, layer or alphamap data");
                Debug.LogWarning(
                    $"[{ToolName}] terrain '{t.name}' has NO TerrainData. It has no heights, no layers and " +
                    "no splatmaps; nothing further can be dumped about it.");
                continue;
            }

            terrainsWithData++;
            report.AppendLine($"    terrainData: '{td.name}'");
            report.AppendLine($"    size: {td.size}");
            report.AppendLine($"    heightmapResolution: {td.heightmapResolution}");
            Debug.Log(
                $"[{ToolName}]   terrainData={td.name} size={td.size} " +
                $"heightmapRes={td.heightmapResolution}");

            TerrainLayer[] layers = td.terrainLayers;
            int layerCount = layers == null ? 0 : layers.Length;
            report.AppendLine($"    terrainLayers: {layerCount}");
            Debug.Log($"[{ToolName}]   terrainLayers={layerCount}");

            if (layerCount == 0)
            {
                defectsReported++;
                Debug.LogWarning(
                    $"[{ToolName}] terrain '{t.name}' has 0 TerrainLayers, so it cannot be textured at " +
                    "all regardless of what the material binds.");
                report.AppendLine("    DEFECT: 0 terrain layers");
            }

            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                TerrainLayer layer = layers[layerIndex];
                if (layer == null)
                {
                    // Was: skipped in silence by `if (layers[i] != null)`, so a null slot in the layer
                    // array looked exactly like a shorter array.
                    defectsReported++;
                    report.AppendLine($"      layer[{layerIndex}]: NULL");
                    Debug.LogWarning(
                        $"[{ToolName}] terrain '{t.name}' layer[{layerIndex}] is a null entry in a " +
                        $"{layerCount}-entry layer array.");
                    continue;
                }

                Texture2D diffuse = layer.diffuseTexture;
                report.AppendLine(
                    $"      layer[{layerIndex}]: '{layer.name}' " +
                    $"diffuse={(diffuse != null ? diffuse.name : "NULL")}");
                Debug.Log(
                    $"[{ToolName}]     layer[{layerIndex}]={layer.name} " +
                    $"diffuse={(diffuse != null ? diffuse.name : "NULL")}");
            }

            Texture2D[] alphamaps = td.alphamapTextures;
            int alphamapCount = alphamaps == null ? 0 : alphamaps.Length;
            // DEVICE-DEPENDENT, and the only line here that is. Whether the alphamap textures are resident
            // depends on the graphics device and on generation having run, so this count is informational
            // and is never a verdict. The CONTENT of these textures is not read at all - CheckAlphamaps
            // does that, and refuses without a GPU because the readback would be zeros.
            report.AppendLine(
                $"    alphamapTextures: {alphamapCount} (informational; residency is device- and " +
                $"generation-dependent, graphicsDeviceType={SystemInfo.graphicsDeviceType}. Content NOT " +
                "read here - see CheckAlphamaps)");
            report.AppendLine(
                $"    alphamapResolution: {td.alphamapResolution} alphamapLayers: {td.alphamapLayers}");
            Debug.Log(
                $"[{ToolName}]   alphamapTextures={alphamapCount} " +
                $"alphamapResolution={td.alphamapResolution} alphamapLayers={td.alphamapLayers} " +
                "(count only, content not read)");
        }

        // INFORMATIONAL, and it cannot prove absence: this is a substring match on GameObject names, so a
        // MapMagic root named anything else is invisible to it. The old code printed the same number with
        // no such caveat. Resources.FindObjectsOfTypeAll also returns assets and hidden objects, which is
        // why the scene filter stays.
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int mapMagicCount = 0;
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null || !go.scene.IsValid() || !go.name.Contains("MapMagic"))
                continue;

            mapMagicCount++;
            report.AppendLine($"  MapMagic GO: '{go.name}' activeInHierarchy={go.activeInHierarchy}");
            Debug.Log($"[{ToolName}] MapMagic GO: {go.name} active={go.activeInHierarchy}");
        }

        report.AppendLine(
            $"  MapMagic objects: {mapMagicCount} (informational - substring match on names, so 0 does " +
            "not prove MapMagic is absent)");
        Debug.Log($"[{ToolName}] MapMagic objects (name substring match): {mapMagicCount}");

        // Camera.allCameras returns ENABLED cameras only, so the two counts differ when a camera is
        // switched off - which is a common reason a capture tool renders nothing.
        Camera[] enabledCameras = Camera.allCameras;
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        int enabledCount = enabledCameras == null ? 0 : enabledCameras.Length;
        int totalCount = allCameras == null ? 0 : allCameras.Length;

        report.AppendLine($"  cameras: {totalCount} ({enabledCount} enabled)");
        Debug.Log($"[{ToolName}] Cameras: {totalCount} including disabled, {enabledCount} enabled");

        for (int i = 0; i < totalCount; i++)
        {
            Camera c = allCameras[i];
            if (c == null)
                continue;

            report.AppendLine(
                $"    cam '{c.name}' pos={c.transform.position} bg={c.backgroundColor} " +
                $"enabled={c.enabled} activeInHierarchy={c.gameObject.activeInHierarchy}");
            Debug.Log(
                $"[{ToolName}] Cam: {c.name} pos={c.transform.position} bg={c.backgroundColor} " +
                $"enabled={c.enabled} activeInHierarchy={c.gameObject.activeInHierarchy}");
        }
    }

    /// <summary>
    /// Writes the verdict into the report as well as the log, then exits 2. Same shape as
    /// <c>CheckAlphamaps.cs:234-240</c>: exit 2 is this family's "the tool did not produce its evidence".
    /// </summary>
    private static void Fail(StringBuilder report, string message)
    {
        report.AppendLine($"RESULT: FAILED - {message}");
        WriteReport(report);
        Debug.LogError($"[{ToolName}] FAILED: {message} Report at {ReportPath}");
        EditorApplication.Exit(2);
    }

    /// <summary>
    /// A report-write failure must not replace the real verdict, so this swallows its own IO error and says
    /// so in the log rather than throwing out of a failure path.
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
