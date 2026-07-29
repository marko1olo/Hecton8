using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Verifies the terrain material the OPEN SCENE ACTUALLY BINDS - every <see cref="UnityEngine.Terrain"/>
/// in the loaded scenes, via <c>Terrain.materialTemplate</c>.
///
/// THIS IS THE SCENE HALF OF A PAIR, AND IT IS NOT A DUPLICATE OF <c>CheckMat.cs</c>. The two examine
/// different objects, and on this project they are almost never the same object:
/// <c>HectonTerrainMaterialInjector.cs:133</c> builds <c>new Material(customTerrainMaterial)</c>, names it
/// <c>&lt;asset&gt;_&lt;gameObject&gt;</c> (<c>:134</c>), enables three keywords on it (<c>:135-137</c>)
/// and binds THAT clone (<c>:145</c>). It then re-binds the arrays (<c>:67-72</c>), substitutes fallback
/// arrays from <c>Assets/_SourceData/</c> when a slot is empty (<c>:75-80</c>, editor only) and pushes the
/// terrain's live alphamaps into <c>_Control</c>/<c>_Control1</c>/<c>_Control2</c> (<c>:115-119</c>). So a
/// perfect asset and a broken render are entirely compatible, and only this file can tell them apart.
///
/// WHAT WAS WRONG:
///
/// * ZERO TERRAINS WAS A PASS. <c>FindObjectsByType</c> over an empty batchmode scene returns an empty
///   array, the <c>foreach</c> body never ran, an empty string was written to disk and the tool exited 0
///   (old <c>:26</c>). That is the single most likely outcome of running this under
///   <c>-executeMethod</c> without opening a scene, and it reported success. It now names the open scenes
///   and exits 2;
/// * THE THREE PROBES NAMED PROPERTIES THAT DO NOT EXIST. <c>_TerrainBaseMapArray</c>,
///   <c>_TerrainNormalMapArray</c>, <c>_TerrainMaskMapArray</c> - the shader declares
///   <c>_AlbedoArray</c>, <c>_NormalArray</c>, <c>_MaskArray</c>
///   (<c>Assets/_Project/Shaders/HectonTerrain.shader:10-12</c>). Those names occur nowhere in this repo
///   outside the verifiers copy-pasted from each other, so all three lines printed "null" forever, on
///   healthy terrain, at exit 0. Probes now go through <see cref="Material.HasProperty"/> so an undeclared
///   name reports as a broken probe, never as a missing texture;
/// * <c>materialTemplate == null</c> was written to the dump as "  Material: null" and then exited 0
///   (old <c>:21-22,26</c>) - a terrain with no material at all was a pass;
/// * every value was logged and nothing was tested;
/// * inactive terrains were skipped, because the default <c>FindObjectsByType</c> overload excludes them;
/// * output went to the hardcoded absolute <c>C:/hades/Hecton8/scene_mats.txt</c> in the repo root.
///
/// Terrains are iterated to completion and failures accumulate, so a broken SECOND terrain cannot hide
/// behind a healthy first one.
/// </summary>
public static class CheckSceneMat
{
    private const string ToolName = "CheckSceneMat";

    /// <summary>
    /// Per-tool subfolder, deliberately distinct from CheckMat's <c>Logs/check_mat</c>, and the report
    /// filename differs too. `static readonly` because <see cref="Path.Combine"/> is not a compile-time
    /// constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_scene_mat");

    /// <summary>From <c>Assets/_Project/Shaders/HectonTerrain.shader:1</c>.</summary>
    private const string ExpectedShaderName = "Hecton8/URP/Terrain_TextureArray";

    /// <summary>
    /// Array slot -> the <c>shader_feature_local</c> keyword that decides whether it is sampled
    /// (<c>HectonTerrain.shader:116-118</c>). An empty keyword means the slot is always sampled.
    /// </summary>
    private static readonly string[] RequiredArrayProperties =
        { "_AlbedoArray", "_NormalArray", "_MaskArray" };

    private static readonly string[] RequiredArrayKeywords =
        { "", "_NORMALMAP", "_MASKMAP" };

    public static void Execute()
    {
        // PART 4 DELIBERATELY OMITTED. This tool reads Terrain.materialTemplate and material property
        // references - no Blit, no readback, no GetPixels, no EncodeToPNG, no compute dispatch. Terrain
        // components and their bound materials exist and report correctly with no GPU context, so a
        // graphicsDeviceType gate here would refuse valid runs. Do not copy CheckTexArrays.cs's gate in.

        string reportPath = Path.Combine(OutputDir, "scene_terrain_material_report.txt");
        int terrainsExamined = 0;
        int terrainsHealthy = 0;
        int failures = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            StringBuilder report = new StringBuilder();
            report.AppendLine($"{ToolName} report");

            // Recorded up front so a zero-terrain run says WHICH scene was searched. "No terrain found"
            // is meaningless without it - it is the same message for a stale path, the wrong scene and an
            // empty batchmode session.
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

            // FindObjectsInactive.Include, matching DeselectGizmosScan.cs:75. A disabled terrain still
            // holds a material binding and was previously invisible to this tool.
            UnityEngine.Terrain[] terrains =
                Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Include);

            if (terrains == null || terrains.Length == 0)
            {
                // Was: empty file written, exit 0.
                Debug.LogError(
                    $"[{ToolName}] FAILED: found 0 Terrain objects, so no scene material binding was " +
                    $"examined and nothing is verified. Open scenes were: {sceneList}. In batchmode no " +
                    "scene is open unless one is passed explicitly, and this project's terrain may also be " +
                    "streamed in at runtime rather than authored into the scene.");
                EditorApplication.Exit(2);
                return;
            }

            foreach (UnityEngine.Terrain terrain in terrains)
            {
                if (terrain == null)
                    continue;

                terrainsExamined++;
                bool terrainOk = true;
                report.AppendLine($"terrain '{terrain.name}' (scene '{terrain.gameObject.scene.name}'):");

                // materialTemplate is a plain property read; unlike Renderer.material it does not clone.
                Material mat = terrain.materialTemplate;
                if (mat == null)
                {
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: terrain '{terrain.name}' has no materialTemplate bound. It " +
                        "renders with Unity's built-in terrain material, not the Hecton terrain shader, " +
                        "so none of the authored texture arrays reach the screen.");
                    report.AppendLine("  material: NONE BOUND");
                    continue;
                }

                // The identity question this whole tool exists to answer: is the scene sampling the asset,
                // a clone of it, or something else? GetAssetPath returns empty for a runtime instance,
                // which is the EXPECTED state while the injector is running, and a real asset path means
                // the injector is not managing this terrain.
                string assetPath = AssetDatabase.GetAssetPath(mat);
                report.AppendLine($"  material: '{mat.name}'");
                report.AppendLine(string.IsNullOrEmpty(assetPath)
                    ? "  origin: runtime instance, not an asset (expected while " +
                      "HectonTerrainMaterialInjector owns the binding)"
                    : $"  origin: asset at {assetPath}");

                Shader shader = mat.shader;
                if (shader == null)
                {
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: the material bound to terrain '{terrain.name}' has no " +
                        "shader. It cannot render.");
                    report.AppendLine("  shader: NONE");
                    continue;
                }

                report.AppendLine($"  shader: {shader.name}");

                if (shader.name.Contains("InternalErrorShader"))
                {
                    failures++;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: terrain '{terrain.name}' is bound to a material resolving " +
                        $"to '{shader.name}' - its shader is missing or failed to compile. The terrain is " +
                        "rendering as an error surface.");
                    report.AppendLine("  VERDICT: error shader");
                    continue;
                }

                if (shader.name != ExpectedShaderName)
                {
                    failures++;
                    terrainOk = false;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: terrain '{terrain.name}' renders with shader " +
                        $"'{shader.name}', expected '{ExpectedShaderName}'. The scene is not using the " +
                        "Hecton terrain shader even if the material asset on disk is correct.");
                }

                for (int i = 0; i < RequiredArrayProperties.Length; i++)
                {
                    string property = RequiredArrayProperties[i];
                    string keyword = RequiredArrayKeywords[i];

                    if (!mat.HasProperty(property))
                    {
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: on terrain '{terrain.name}', shader '{shader.name}' " +
                            $"does not declare '{property}', so this probe proves nothing. Fix the " +
                            "property name here, or the shader lost the slot.");
                        report.AppendLine($"  {property}: PROBE BROKEN - not declared by shader");
                        continue;
                    }

                    Texture bound = mat.GetTexture(property);
                    if (bound == null)
                    {
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: on terrain '{terrain.name}', '{property}' is declared " +
                            "but nothing is bound to it, so that map is absent from the rendered terrain.");
                        report.AppendLine($"  {property}: UNBOUND");
                        continue;
                    }

                    if (!(bound is Texture2DArray array))
                    {
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: on terrain '{terrain.name}', '{property}' is bound to " +
                            $"'{bound.name}' of type {bound.GetType().Name}, but the shader declares a " +
                            "2DArray. It will not sample correctly.");
                        report.AppendLine($"  {property}: WRONG TYPE {bound.GetType().Name} ({bound.name})");
                        continue;
                    }

                    if (array.depth < 1)
                    {
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: on terrain '{terrain.name}', '{property}' is bound to " +
                            $"array '{array.name}' with depth {array.depth}. It textures nothing.");
                        report.AppendLine($"  {property}: EMPTY ARRAY {array.name} depth=0");
                        continue;
                    }

                    // BOUND BUT NOT SAMPLED. _NORMALMAP and _MASKMAP are shader_feature_local
                    // (HectonTerrain.shader:117-118), so with the keyword off the sample is compiled out
                    // and the bound array cannot reach a single pixel. Unlike CheckMat, this check is
                    // earned here: this material IS the one the renderer uses, and the injector enables
                    // these keywords on the clone at HectonTerrainMaterialInjector.cs:135-137 - so a
                    // missing keyword here means that path did not run.
                    if (keyword.Length > 0 && !mat.IsKeywordEnabled(keyword))
                    {
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: on terrain '{terrain.name}', '{property}' is bound to " +
                            $"'{array.name}' but keyword '{keyword}' is DISABLED on the rendering " +
                            "material. That slot is compiled out of the shader, so the texture is bound " +
                            "and never sampled - the terrain renders as though it were missing.");
                        report.AppendLine(
                            $"  {property}: BOUND BUT NOT SAMPLED ({array.name}, {keyword} off)");
                        continue;
                    }

                    report.AppendLine(
                        $"  {property}: OK {array.name} {array.width}x{array.height} " +
                        $"depth={array.depth} format={array.format}" +
                        (keyword.Length > 0 ? $" ({keyword} on)" : string.Empty));
                }

                // Alphamap 0, pushed in by HectonTerrainMaterialInjector.cs:115. Without it the terrain
                // samples layer 0 across the whole surface no matter how good the arrays are.
                if (!mat.HasProperty("_Control"))
                {
                    failures++;
                    terrainOk = false;
                    Debug.LogError(
                        $"[{ToolName}] FAILED: on terrain '{terrain.name}', shader '{shader.name}' does " +
                        "not declare '_Control'; the splat control probe is broken.");
                    report.AppendLine("  _Control: PROBE BROKEN - not declared by shader");
                }
                else
                {
                    Texture control = mat.GetTexture("_Control");
                    if (control == null)
                    {
                        failures++;
                        terrainOk = false;
                        Debug.LogError(
                            $"[{ToolName}] FAILED: on terrain '{terrain.name}', '_Control' (splat alphamap " +
                            "0) is unbound on the rendering material, so every texel resolves to splat " +
                            "layer 0 and the terrain reads as a single flat material.");
                        report.AppendLine("  _Control: UNBOUND");
                    }
                    else
                    {
                        report.AppendLine($"  _Control: OK {control.name}");
                    }
                }

                report.AppendLine(
                    $"  keywords: {(mat.shaderKeywords.Length > 0 ? string.Join(", ", mat.shaderKeywords) : "<none>")}");
                report.AppendLine($"  VERDICT: {(terrainOk ? "ok" : "FAILED")}");

                if (terrainOk)
                    terrainsHealthy++;
            }

            report.AppendLine(
                $"examined {terrainsExamined} terrain(s), {terrainsHealthy} fully correct, " +
                $"{failures} failure(s)");

            File.WriteAllText(reportPath, report.ToString());

            if (!File.Exists(reportPath) || new FileInfo(reportPath).Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: the report at {reportPath} is missing or empty after the " +
                    "write, so this run produced no evidence.");
                EditorApplication.Exit(2);
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: no complete scene terrain material report was produced (intended " +
                $"{reportPath}); {terrainsExamined} terrain(s) had been examined. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        // Zero examined means a stale path, the wrong scene, or a renamed object - every time. Guarded
        // separately from `failures` because zero terrains also yields zero failures.
        if (terrainsExamined == 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: examined 0 terrains. Nothing about the scene's material binding " +
                "is verified by this run.");
            EditorApplication.Exit(2);
            return;
        }

        if (failures > 0)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: {failures} problem(s) across {terrainsExamined} examined " +
                $"terrain(s); only {terrainsHealthy} are fully correct. Details in {reportPath}.");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log(
            $"[{ToolName}] PASS: all {terrainsExamined} terrain(s) bind '{ExpectedShaderName}' with every " +
            $"required array bound, typed, non-empty and keyword-enabled. Report: {reportPath}. This " +
            "covers what the SCENE binds - CheckMat covers the asset on disk.");
        EditorApplication.Exit(0);
    }
}
