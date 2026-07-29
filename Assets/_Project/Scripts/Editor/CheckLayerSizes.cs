using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// VERIFIER. Reads every <see cref="UnityEngine.TerrainLayer"/> on the sandbox terrain and reports the
/// dimensions and format of each layer's diffuse texture. Its pass/fail is the null/zero-dimension test
/// below; the resolution comparison against the texture-array builder is a WARNING, for the reason
/// documented on <see cref="BuilderTargetResolution"/>.
///
/// WHAT WAS WRONG - every failure path in this file reported success, and one of them crashed instead of
/// reporting anything:
///
/// * ZERO LAYERS READ WAS A PASS. The <c>for</c> loop ran <c>layers.Length</c> times and
///   <c>EditorApplication.Exit(0)</c> sat after it, so a terrain with an empty <c>terrainLayers</c> array
///   printed nothing at all and exited 0. There is not one standalone <c>.terrainlayer</c> asset anywhere
///   in <c>Assets/</c>, so "no layers" is not a hypothetical for this project - it is the most likely
///   state, and it was the state this tool called success;
/// * THE SUCCESS EXIT SAT OUTSIDE THE GUARD. The whole body was inside <c>if (terrains.Length &gt; 0)</c>
///   while <c>Exit(0)</c> was outside it, so a scene with no terrain exited 0 having examined nothing.
///   Identical shape to the one fixed in <c>CheckAlphamaps.cs:15-18</c>;
/// * <c>diffuseTexture</c> WAS DEREFERENCED WITH NO NULL CHECK. The old line 13 read <c>t.width</c>
///   straight off <c>layers[i].diffuseTexture</c>. A layer with no albedo assigned - which
///   <c>HectonTerrainTextureArrayBuilder.cs:110</c> explicitly logs as an error and then papers over with
///   a flat grey 4x4 fallback (same file, 124-134) - threw a NullReferenceException here. With no
///   try/catch, <c>Execute</c> returned having set NO exit code, and under <c>-quit</c> that ends the
///   process at 0. The crash and the pass were the same exit code;
/// * NULL LAYER SLOTS WERE SILENTLY SKIPPED. <c>if (layers[i] != null)</c> stepped over a null slot and
///   still fell through to <c>Exit(0)</c>. <c>HectonTerrainTextureArrayBuilder.cs:101-106</c> treats a
///   null layer as a build failure; here eight null slots read as a clean run;
/// * THE SIZES WERE LOGGED AND NEVER COMPARED TO ANYTHING. That is the whole of what the tool did: format
///   a number into a string. Same defect class as the average RGB in <c>CheckAlbedoArray.cs:14-19</c>;
/// * it logged under <c>[FAS]</c>, which is FixAndShoot's prefix (<c>Assets/FixAndShoot.cs:42,80</c>) and
///   is shared by nine files in this tree. Two unrelated tools sharing a prefix in one batchmode log is
///   an evidence collision;
/// * it wrote no file, so its verdict lived only in a log the next batch run overwrites.
///
/// NO GPU REFUSAL HERE, on purpose, and the reasoning has to hold or the gate is a defect of its own.
/// Nothing in this file renders, blits, reads pixels back off a RenderTexture, encodes a PNG or dispatches
/// compute. <see cref="Texture2D.width"/>, <see cref="Texture2D.height"/> and
/// <see cref="Texture2D.format"/> are import metadata deserialized from the asset; they are correct with
/// <c>graphicsDeviceType == Null</c>. The failure a gate would supposedly catch - terrain that never
/// generated because the editor came up headless - is caught directly and FAILED by the zero-terrain and
/// zero-layer branches below, which is strictly stronger, because it also catches the with-GPU case where
/// generation simply did not run. Compare the same decision, argued the same way, in
/// <c>CheckAlbedoArray.cs:36-47</c> and <c>CheckSceneMat.cs:70-73</c>. If you ever add a pixel read to
/// this file, that reasoning stops holding.
///
/// SCOPE, stated so the output is not over-read: this reads the layer list of the FIRST terrain found. It
/// proves those layers have albedo textures with real dimensions. It says nothing about the CONTENTS of
/// those textures (that is <c>CheckAlbedoArray</c>), nothing about splat coverage (that is
/// <c>CheckAlphamaps</c>), and nothing about how the terrain looks - visual acceptance is
/// <c>Docs/QUALITY_GATES.md</c>'s job, never this file's.
/// </summary>
public static class CheckLayerSizes
{
    private const string ToolName = "CheckLayerSizes";

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is not
    /// a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_layer_sizes");

    private static readonly string ReportPath = Path.Combine(OutputDir, "layer_sizes_report.txt");

    /// <summary>
    /// The resolution the texture-array builder packs every layer to:
    /// <c>HectonTerrainTextureArrayBuilder.RequiredResolution</c>
    /// (<c>HectonTerrainTextureArrayBuilder.cs:15</c>), locked to 1024 by the terrain bible VRAM budget
    /// recorded at that file's lines 12-14 - "3 arrays x 8 slices at 1024 = 32.0 MiB VRAM resident".
    /// Read from its real source rather than picked as a plausible literal.
    ///
    /// A MISMATCH IS DELIBERATELY NOT A FAILURE, and this is the load-bearing judgement in this file. The
    /// builder does not require its inputs to be 1024: <c>GetReadableTexture</c>
    /// (<c>HectonTerrainTextureArrayBuilder.cs:270-293</c>) blits every source texture into a
    /// <c>targetRes</c> RenderTexture, so a 512 or a 4096 layer is rescaled and packs fine. Hard-failing
    /// on <c>!= 1024</c> would refuse runs that the shipping bake path accepts - a false gate, which is
    /// its own defect. Under-resolution is still worth naming, because upscaling 512 to 1024 pays the full
    /// 1024 VRAM cost for half the detail, so it is reported as a warning with this source cited.
    /// </summary>
    private const int BuilderTargetResolution = 1024;

    public static void Execute()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"{ToolName} report");

        int layersExamined = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            // Was: the return value was discarded, so a scene that failed to load was indistinguishable
            // from one that loaded empty. Under -executeMethod no scene is open unless one is opened
            // explicitly, which is why this tool opens one at all.
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Fail(report,
                    $"OpenScene('{ScenePath}') returned a scene that is not usable " +
                    $"(IsValid={scene.IsValid()}, isLoaded={scene.isLoaded}). No layer was read, so " +
                    "nothing about the terrain layers is verified by this run.");
                return;
            }

            report.AppendLine($"scene: '{scene.name}' ({scene.path})");

            // FindObjectsInactive.Include, matching CheckSceneMat.cs:106-109. The old call took the
            // default (Exclude), so a DISABLED terrain was invisible and the tool would have reported
            // "no terrain" for a terrain that is present. A disabled terrain's TerrainData carries the
            // same layer list, so there is no reason to skip it and every reason not to claim absence
            // when all that was established is "not enabled".
            UnityEngine.Terrain[] terrains =
                UnityEngine.Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Include);
            int terrainCount = terrains == null ? 0 : terrains.Length;
            int activeTerrainCount = UnityEngine.Terrain.activeTerrains == null
                ? 0
                : UnityEngine.Terrain.activeTerrains.Length;

            report.AppendLine($"terrains found: {terrainCount} ({activeTerrainCount} active)");
            Debug.Log(
                $"[{ToolName}] scene '{scene.name}': {terrainCount} Terrain object(s), " +
                $"{activeTerrainCount} active.");

            if (terrainCount == 0)
            {
                // Was: the guard skipped the body and Exit(0) ran anyway.
                Fail(report,
                    $"found 0 Terrain objects in '{ScenePath}' (inactive included), so no layer was " +
                    "read and no statement about layer sizes is supported by this run. This project's " +
                    "terrain chunks are produced by MapMagic, so the usual cause is that generation " +
                    "never ran in this session rather than a missing scene.");
                return;
            }

            UnityEngine.Terrain terrain = null;
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null) { terrain = terrains[i]; break; }
            }

            if (terrain == null)
            {
                Fail(report,
                    $"all {terrainCount} Terrain reference(s) in '{ScenePath}' were null, so no " +
                    "TerrainData could be reached and no layer was read.");
                return;
            }

            report.AppendLine(
                $"inspecting terrain '{terrain.name}' (of {terrainCount}), " +
                $"enabled={terrain.enabled && terrain.gameObject.activeInHierarchy}");

            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                // Was: dereferenced straight through - terrains[0].terrainData.terrainLayers - so this
                // threw and the throw exited 0.
                Fail(report,
                    $"terrain '{terrain.name}' has no TerrainData, so it has no layer list to read.");
                return;
            }

            UnityEngine.TerrainLayer[] layers = terrainData.terrainLayers;
            int layerCount = layers == null ? 0 : layers.Length;
            report.AppendLine($"terrain layers: {layerCount}");
            Debug.Log($"[{ToolName}] terrain '{terrain.name}' has {layerCount} terrain layer(s).");

            if (layerCount == 0)
            {
                // THE HEADLINE DEFECT. Was: the loop body never ran, nothing was logged, Exit(0).
                Fail(report,
                    $"terrain '{terrain.name}' has {layerCount} terrain layer(s), so there is no layer " +
                    "size to read. Zero layers examined is never a pass - a terrain with no layers " +
                    "cannot be textured at all, and reading nothing is not the same as reading " +
                    "something correct.");
                return;
            }

            int underResolution = 0;
            int nonSquare = 0;

            for (int i = 0; i < layerCount; i++)
            {
                UnityEngine.TerrainLayer layer = layers[i];
                if (layer == null)
                {
                    // Was: silently skipped by `if (layers[i] != null)`, then reported as a pass.
                    Fail(report,
                        $"terrain layer slot {i} of {layerCount} on '{terrain.name}' is null. " +
                        "HectonTerrainTextureArrayBuilder.cs:101-106 treats a null layer as a build " +
                        "failure, so it is one here too; the layer set is incomplete and its sizes " +
                        "cannot be verified.");
                    return;
                }

                Texture2D diffuse = layer.diffuseTexture;
                if (diffuse == null)
                {
                    // Was: NullReferenceException on `t.width` with no try/catch, which set no exit code.
                    Fail(report,
                        $"layer {i} ('{layer.name}') has no diffuseTexture, so it has no size to " +
                        "report. HectonTerrainTextureArrayBuilder.cs:110 logs this as an error and then " +
                        "substitutes a flat grey 4x4 fallback (same file, 124-134), so shipping this " +
                        "state produces a uniformly grey layer rather than a visible failure. This is " +
                        "the dereference that used to throw here.");
                    return;
                }

                int width = diffuse.width;
                int height = diffuse.height;

                if (width <= 0 || height <= 0)
                {
                    Fail(report,
                        $"layer {i} ('{layer.name}') diffuseTexture '{diffuse.name}' reports " +
                        $"{width}x{height}. A non-positive dimension is not a texture; there is nothing " +
                        "for the array builder to blit from.");
                    return;
                }

                bool under = width < BuilderTargetResolution || height < BuilderTargetResolution;
                bool square = width == height;
                if (under) underResolution++;
                if (!square) nonSquare++;

                layersExamined++;
                report.AppendLine(
                    $"layer {i}: '{layer.name}' diffuse '{diffuse.name}' {width}x{height} " +
                    $"{diffuse.format}{(under ? " [BELOW builder target]" : string.Empty)}" +
                    $"{(square ? string.Empty : " [non-square]")}");
                Debug.Log(
                    $"[{ToolName}] layer {i}: {width}x{height} {diffuse.format} - '{layer.name}' " +
                    $"(diffuse '{diffuse.name}')");
            }

            if (layersExamined != layerCount)
            {
                // Cannot happen while every failure above returns, and that is the point: if a future
                // edit makes a layer skippable, this refuses to call the run a pass.
                Fail(report,
                    $"examined {layersExamined} of {layerCount} layer(s). Not every layer was measured, " +
                    "so the layer set is not verified.");
                return;
            }

            if (underResolution > 0)
            {
                // A warning, not a failure. See BuilderTargetResolution: the builder rescales, so this
                // does not break the bake - it silently costs VRAM for detail that is not there.
                Debug.LogWarning(
                    $"[{ToolName}] {underResolution} of {layerCount} layer(s) have a diffuse texture " +
                    $"smaller than the {BuilderTargetResolution} the array builder packs to " +
                    "(HectonTerrainTextureArrayBuilder.cs:15). GetReadableTexture (same file, 270-293) " +
                    "blits them up, so the bake succeeds and each slice still costs the full " +
                    $"{BuilderTargetResolution} of VRAM while carrying less detail than that. Not a " +
                    "failure; source-art resolution is an art call, reported here rather than changed.");
                report.AppendLine(
                    $"WARNING: {underResolution} layer(s) below the {BuilderTargetResolution} builder " +
                    "target; they are upscaled at bake time.");
            }

            if (nonSquare > 0)
            {
                Debug.LogWarning(
                    $"[{ToolName}] {nonSquare} of {layerCount} layer(s) have a non-square diffuse " +
                    $"texture. The builder blits to a square {BuilderTargetResolution} target, so these " +
                    "are stretched rather than cropped. Not a failure, but the tiling will not match the " +
                    "source aspect.");
                report.AppendLine($"WARNING: {nonSquare} layer(s) have a non-square diffuse texture.");
            }

            report.AppendLine(
                $"RESULT: PASS - {layersExamined} layer(s) on '{terrain.name}' have a diffuse texture " +
                "with positive dimensions.");
            WriteReport(report);
            Debug.Log(
                $"[{ToolName}] PASS: measured {layersExamined} of {layerCount} layer(s) on " +
                $"'{terrain.name}'. This checks that each layer HAS an albedo texture with real " +
                "dimensions; it does not inspect pixel contents (CheckAlbedoArray) or splat coverage " +
                $"(CheckAlphamaps). Report at {ReportPath}");
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all, so OpenScene throwing on a moved scene, or the diffuseTexture
            // dereference above, left Execute with NO exit code - which under -quit ends at 0.
            Fail(report,
                $"threw after examining {layersExamined} layer(s); the layer sizes are NOT verified. {ex}");
        }
    }

    /// <summary>Writes the error into the report as well as the log, then exits 2.</summary>
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
