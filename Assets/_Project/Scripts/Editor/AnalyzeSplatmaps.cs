using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// VERIFIER. Opens 020_RENDER_SANDBOX and averages every splat layer weight over the FULL alphamap of each
/// terrain sitting near the world origin, then fails when the weights do not add up to coverage. Sibling of
/// <c>CheckAlphamaps</c>, which samples ONE texel of the first active terrain where this one folds the whole
/// map; and of <c>DumpSplatmaps</c>/<c>DumpSplat</c>, which dump the alphamap textures as PNG.
///
/// WHAT EXIT 0 MEANS NOW. At least one terrain within the origin radius was found, its full alphamap was
/// read, the per-layer mean weights are finite, and they sum to real coverage. It does NOT mean the terrain
/// is textured correctly or looks right: mean weights say nothing about WHERE the weight sits, so a
/// perfectly stratified map and a map with every layer smeared uniformly average identically. Spatial
/// evidence is <c>DumpSplatmaps</c>; visual acceptance is <c>Docs/QUALITY_GATES.md</c>'s job, never this
/// file's.
///
/// WHAT WAS WRONG:
///
///   * THE HEADLINE: the per-layer averages were COMPUTED, FORMATTED INTO A STRING, LOGGED, AND NEVER
///     TESTED. An all-zero alphamap - which is what an ungenerated splatmap, a MapMagic run that never
///     wrote weights, and a <c>-nographics</c> editor all produce - printed
///     "Terrain X averages: L0=0.000 L1=0.000 ..." and the tool exited 0. This is the identical defect
///     found in <c>CheckAlbedoArray</c>, which logged an average RGB it never compared and passed 0xCD
///     uninitialised memory off as "avg RGB 0.804". A number that is printed but never compared is not a
///     check;
///   * <c>terrains.Length == 0</c> made the <c>foreach</c> body never run, and
///     <c>EditorApplication.Exit(0)</c> sat after the loop - so a scene with no terrain reported success
///     while measuring nothing. Same for the origin filter: if no terrain sat within the radius, zero
///     terrains were examined and the tool still exited 0. Zero objects examined means a stale path, the
///     wrong scene, or a moved chunk layout - never a pass. The tool did not even log HOW MANY terrains it
///     found or analysed;
///   * <c>t.terrainData</c> was dereferenced with no null check, so a terrain with no TerrainData threw a
///     NullReferenceException out of <c>Execute</c> and NO exit code was ever set - which under
///     <c>-quit</c> ends the process at 0. There was no try/catch anywhere;
///   * <c>float[] totals = new float[8]</c> was a fixed 8 while the loop bound was
///     <c>terrainData.alphamapLayers</c>. HectonTerrain.shader declares _Control.._Control7
///     (HectonTerrain.shader:14-21), i.e. up to 32 layers, so a terrain with more than 8 layers threw
///     IndexOutOfRangeException - again with no exit code set. The loop also indexed the array returned by
///     <see cref="TerrainData.GetAlphamaps"/> using the terrain's declared layer count rather than the
///     count actually handed back;
///   * <c>totals[l] / count</c> divided by <c>alphamapWidth * alphamapHeight</c> with no check that it is
///     non-zero. Float 0/0 does not throw, it yields NaN, so a degenerate alphamap logged "L0=NaN" and
///     exited 0;
///   * there was no output file at all - the only record was a <c>Debug.Log</c> under the tag
///     <c>[FAS]</c>, which is FixAndShoot's tag (see <c>DumpSplatmaps.cs:22-23</c>). Two unrelated tools
///     sharing a log prefix in one batchmode log is the same evidence collision as two tools sharing an
///     output directory, and <c>Logs/</c> here is already a flat dumping ground;
///   * <c>EditorSceneManager.OpenScene</c> was called unconditionally, discarding any unsaved edits in a
///     concurrent authoring session on this shared working tree, and could itself throw with no exit code.
/// </summary>
public static class AnalyzeSplatmaps
{
    private const string ToolName = "AnalyzeSplatmaps";

    /// <summary>
    /// Per-tool subfolder inside the repo. The old tool wrote no file at all. The subfolder is not
    /// cosmetic: tools in this layer have already destroyed each other's evidence by sharing a directory,
    /// and <c>Logs/Splatmap_0.png</c> still sits in the flat root from one of them. <c>static readonly</c>
    /// rather than <c>const</c> because <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "analyze_splatmaps");

    private static readonly string ReportPath = Path.Combine(OutputDir, "splatmap_averages.txt");

    private const string ScenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

    /// <summary>
    /// The original selection rule, preserved: squared distance from the world origin, i.e. a radius of
    /// ~31.6 world units. Its comment claimed "closest to origin", which it is not - it selects EVERY
    /// terrain inside the radius, and analyses all of them. Changing the rule would change which chunk this
    /// tool reports on, so it is preserved and the count of selected terrains is now logged instead.
    /// </summary>
    private const float OriginRadiusSqr = 1000f;

    /// <summary>
    /// Unity normalizes alphamap weights to sum to 1 per texel, so the mean over the whole map must also
    /// land near 1. A mean sum below this is not a lightly-painted terrain, it is an absent weight table:
    /// no layer has coverage anywhere. Same floor and same reasoning as <c>CheckAlphamaps.cs:58</c>.
    /// </summary>
    private const float MinimumMeanWeightSum = 0.5f;

    /// <summary>
    /// Tolerance on the sum-to-1 invariant. Weights are 8-bit per channel and this is a mean over up to a
    /// million texels, so quantization error accumulates; a larger deviation is worth naming but is still a
    /// real reading of real data, so it warns rather than fails.
    /// </summary>
    private const float NormalizationTolerance = 0.05f;

    /// <summary>A layer whose mean weight is below this contributes nothing anywhere on the map.</summary>
    private const float LayerCoverageEpsilon = 0.0005f;

    /// <summary>Proved it read a real alphamap and the weights carry coverage.</summary>
    private const int ExitVerified = 0;

    /// <summary>Could not measure, or measured no coverage. Nothing is claimed about the terrain.</summary>
    private const int ExitFailed = 2;

    /// <summary>Refused: no GPU context, so every weight here would be a fabricated zero.</summary>
    private const int ExitNoGpu = 3;

    /// <summary>
    /// Batch entry point. Called by reflection name from <c>Tools/BatchTasks</c> - do not rename.
    /// </summary>
    public static void Execute()
    {
        // PART 4. This tool does not blit or read back a texture itself, but the weights it now TESTS are
        // produced by the MapMagic generation path in the one scene it hardcodes, and
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-39 bans -nographics for exactly that while
        // naming alphamaps by name: "compute shaders and Graphics.Blit return zeros with no GPU context.
        // Poll EditorApplication.update for stable frames (Terrain length == 9, alphamaps loaded, ...)".
        // Without a device this tool folds a table of zeros and reports "no layer coverage anywhere" - a
        // specific, plausible, entirely fabricated finding about the terrain, produced by an editor launched
        // with the wrong flags. Same gate and same reasoning as its sibling CheckAlphamaps.cs:76-84. Fully
        // qualified on purpose so the guard needs no using directive.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context, would return zeros. Remove -nographics. The " +
                "sandbox splatmaps are generated through MapMagic's compute/Blit path, so with no device " +
                "every layer weight reads back as 0 and this tool would report a terrain with no layer " +
                "coverage anywhere. No averages were computed.");
            EditorApplication.Exit(ExitNoGpu);
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(ToolName);
        report.AppendLine($"Scene: {ScenePath}");
        report.AppendLine($"Origin selection radius: sqrMagnitude < {OriginRadiusSqr} (~{Mathf.Sqrt(OriginRadiusSqr):F1} units)");

        int exitCode;
        try
        {
            exitCode = Run(report);
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all, so a null TerrainData or an out-of-range totals[] left Execute with
            // NO exit code whatsoever. The Unity batchmode log is the only channel anyone reads out of a
            // batch run, so the failure has to name what was not produced.
            report.AppendLine($"RESULT: FAILED - threw before the averages could be verified. {ex}");
            Debug.LogError(
                $"[{ToolName}] FAILED: threw before any splat weight average was verified, so nothing is " +
                $"measured about the terrain in '{ScenePath}'. {ex}");
            exitCode = ExitFailed;
        }

        WriteReport(report);
        EditorApplication.Exit(exitCode);
    }

    private static int Run(StringBuilder report)
    {
        Directory.CreateDirectory(OutputDir);

        if (!TryOpenSceneWithoutDiscardingWork(ScenePath))
        {
            report.AppendLine("RESULT: FAILED - refused to open the scene; no alphamap was read.");
            return ExitFailed;
        }

        // Signature left exactly as it was: this overload is the one already proven to compile in this
        // assembly (DumpSplatmaps.cs:72), and the cheap lock-free compile gate emits phantom
        // CS0433/CS0656 on Hecton8.Editor, so an unverifiable "improvement" to a Find* call is not worth it.
        Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>();

        // Was never logged. Zero examined objects is the single most common way a tool in this layer
        // reports a pass it did not earn, and it is unreadable unless the count is in the log.
        report.AppendLine($"Terrains found: {terrains.Length}");
        Debug.Log($"[{ToolName}] found {terrains.Length} Terrain(s) in '{ScenePath}'.");

        if (terrains.Length == 0)
        {
            // Was: the foreach body never ran and the tool fell through to Exit(0).
            // FindObjectsByType skips inactive objects, so this cannot claim the scene contains no
            // terrain - only that no ACTIVE one was returned.
            return Fail(report,
                $"'{ScenePath}' returned no active Terrain from FindObjectsByType, so no alphamap was read " +
                "and no layer weight was averaged. Nothing was measured. Note that inactive terrains are " +
                "not returned by this call, so the terrains may exist but be disabled; either way this run " +
                "measured nothing. If the scene is right, generation has not run or has not settled yet " +
                "(hecton8-shaders-compute.md:36-39 requires Terrain length == 9 and alphamaps loaded " +
                "before any measurement).");
        }

        int selected = 0;
        int passed = 0;
        StringBuilder skipped = new StringBuilder();

        foreach (Terrain t in terrains)
        {
            Vector3 pos = t.transform.position;
            if (pos.sqrMagnitude >= OriginRadiusSqr)
            {
                skipped.Append($"'{t.name}'@{pos}(sqrMag {pos.sqrMagnitude:F0}) ");
                continue;
            }

            selected++;
            string detail;
            if (AnalyzeTerrain(t, report, out detail))
            {
                passed++;
                Debug.Log($"[{ToolName}] '{t.name}' PASS: {detail}");
            }
            else
            {
                // Every selected terrain must measure, otherwise the aggregate verdict is partial. Log each
                // failure individually so one bad chunk is identifiable in the batch log.
                Debug.LogError($"[{ToolName}] '{t.name}' FAILED: {detail}");
                report.AppendLine($"    FAILED: {detail}");
            }
        }

        report.AppendLine($"Terrains inside the origin radius: {selected} of {terrains.Length}");
        if (skipped.Length > 0)
            report.AppendLine($"Skipped (outside radius): {skipped}");

        if (selected == 0)
        {
            // Was: zero iterations of the loop body, then Exit(0). This is the "zero objects examined
            // treated as a pass" shape - it means the chunk layout moved, the wrong scene is open, or the
            // origin chunk is missing, every time.
            return Fail(report,
                $"none of the {terrains.Length} terrain(s) in '{ScenePath}' sits within sqrMagnitude " +
                $"{OriginRadiusSqr} of the world origin, so ZERO alphamaps were read and no average was " +
                $"computed. Positions checked: {skipped}. This is a stale selection rule, a moved chunk " +
                "layout or the wrong scene - not a statement about the splatmaps.");
        }

        if (passed != selected)
        {
            return Fail(report,
                $"{passed} of {selected} selected terrain(s) produced verified splat coverage. The failures " +
                "are listed above; a partial read is not a pass.");
        }

        report.AppendLine(
            $"RESULT: PASS - {passed} terrain(s) inside the origin radius have real splat layer coverage.");
        Debug.Log(
            $"[{ToolName}] PASS: all {passed} selected terrain(s) - of {terrains.Length} found, " +
            $"{selected} inside the origin radius - carry real splat coverage. Nothing is claimed about the " +
            $"{terrains.Length - selected} terrain(s) outside the radius. Mean weights also say nothing " +
            $"about WHERE the weight sits, so this is not evidence that the terrain is stratified or that " +
            $"it looks right. Report at {ReportPath}");
        return ExitVerified;
    }

    /// <summary>
    /// Folds one terrain's full alphamap into per-layer means and TESTS them. Returns false with a reason
    /// rather than handing back numbers nobody compared.
    /// </summary>
    private static bool AnalyzeTerrain(Terrain t, StringBuilder report, out string detail)
    {
        // Was dereferenced with no null check, one line after the terrain was selected.
        TerrainData td = t.terrainData;
        if (td == null)
        {
            detail = $"terrain '{t.name}' has no TerrainData, so it has no alphamap to average.";
            report.AppendLine($"- '{t.name}' at {t.transform.position}: TerrainData is NULL");
            return false;
        }

        // Hoisted out of the loop conditions, where the old code re-read three TerrainData properties on
        // every one of up to a million iterations.
        int width = td.alphamapWidth;
        int height = td.alphamapHeight;
        int declaredLayers = td.alphamapLayers;

        report.AppendLine(
            $"- '{t.name}' at {t.transform.position}: alphamapWidth={width} alphamapHeight={height} " +
            $"alphamapLayers={declaredLayers}");

        if (width < 1 || height < 1)
        {
            // The old divisor was width*height with no guard: 0 texels gave float 0/0 = NaN, which was
            // printed as "L0=NaN" and exited 0.
            detail =
                $"terrain '{t.name}' reports an alphamap of {width}x{height}, so there is no texel to " +
                "average. The old divisor was width*height and 0/0 in float is NaN, not an exception.";
            return false;
        }

        if (declaredLayers < 1)
        {
            detail =
                $"terrain '{t.name}' reports {declaredLayers} alphamap layer(s), so there is no weight to " +
                "average. A terrain with no splat layers cannot be textured; reading nothing is not a pass.";
            return false;
        }

        float[,,] maps = td.GetAlphamaps(0, 0, width, height);
        if (maps == null)
        {
            detail = $"GetAlphamaps returned null for the full {width}x{height} alphamap of '{t.name}'.";
            return false;
        }

        // GetAlphamaps hands back [y, x, layer]. Bound every loop by what was RETURNED, not by what the
        // terrain declares - the old code indexed this array with terrainData.alphamapLayers.
        int mapHeight = maps.GetLength(0);
        int mapWidth = maps.GetLength(1);
        int layers = maps.GetLength(2);

        if (mapWidth != width || mapHeight != height || layers != declaredLayers)
        {
            detail =
                $"GetAlphamaps returned a [{mapHeight},{mapWidth},{layers}] table for a terrain that " +
                $"declares {height}x{width} and {declaredLayers} layer(s). The table does not match the " +
                "terrain it came from, so it cannot be read as a measurement of it.";
            return false;
        }

        int texels = mapWidth * mapHeight;

        // Was `new float[8]` with the loop bound at alphamapLayers. HectonTerrain.shader:14-21 declares
        // _Control.._Control7, i.e. up to 32 layers, so more than 8 layers threw IndexOutOfRangeException
        // out of Execute with no exit code set.
        //
        // double, not float. A 1025x1025 alphamap is 1,050,625 texels, so a layer total reaches ~1e6 where
        // float's ULP is 0.0625 - adding weights of 0.001 to a total that large rounds them away entirely
        // and the accumulator freezes. That systematically UNDER-counts coverage, which against the floor
        // below would be a false failure on a healthy terrain: exactly the kind of defect this pass exists
        // to remove, so the accumulator must not introduce one.
        double[] totals = new double[layers];

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int l = 0; l < layers; l++)
                {
                    totals[l] += maps[y, x, l];
                }
            }
        }

        double meanSum = 0.0;
        int layersWithCoverage = 0;
        StringBuilder line = new StringBuilder();
        for (int l = 0; l < layers; l++)
        {
            double mean = totals[l] / texels;
            meanSum += mean;
            if (mean > LayerCoverageEpsilon) layersWithCoverage++;
            line.Append($"L{l}={mean:F4} ");
            report.AppendLine($"    layer {l} mean weight: {mean:F4}");
        }

        report.AppendLine(
            $"    mean weight sum: {meanSum:F4} over {texels} texel(s); {layersWithCoverage} of {layers} " +
            "layer(s) carry coverage");
        Debug.Log($"[{ToolName}] '{t.name}' means over {texels} texel(s): {line}(sum {meanSum:F4})");

        // THE TEST THE OLD TOOL NEVER MADE. Everything above this line was already in the old file; below
        // it is the comparison that turns the printed numbers into a check.
        if (double.IsNaN(meanSum) || double.IsInfinity(meanSum))
        {
            detail =
                $"the mean weight sum for '{t.name}' is {meanSum}, not a finite number, so the alphamap " +
                "contains non-finite weights and nothing can be concluded from the averages.";
            return false;
        }

        if (meanSum < MinimumMeanWeightSum)
        {
            detail =
                $"the {layers} layer mean weights of '{t.name}' sum to {meanSum:F4} over {texels} texel(s), " +
                $"below the {MinimumMeanWeightSum:F2} floor - no layer has coverage anywhere on this " +
                $"terrain. Unity normalizes alphamap weights to sum to 1 per texel, so a mean sum near zero " +
                "is an absent weight table: an ungenerated splatmap, a generation pass that wrote nothing, " +
                "or an editor with no GPU context. This is the branch that used to print the numbers and " +
                "exit 0.";
            return false;
        }

        // System.Math.Abs, not Mathf.Abs: Mathf.Abs takes a float and would round the double sum back down
        // before the comparison.
        bool normalized = System.Math.Abs(meanSum - 1.0) <= NormalizationTolerance;
        if (!normalized)
        {
            Debug.LogWarning(
                $"[{ToolName}] '{t.name}' mean weight sum is {meanSum:F4}, not 1.0 within " +
                $"{NormalizationTolerance:F2}. Unity normalizes alphamaps on write, so this table was not " +
                "written through the normal path. The weights are real data and the coverage check passed, " +
                "but do not treat the individual means as calibrated.");
            report.AppendLine(
                $"    WARNING: mean sum deviates from 1.0 by more than {NormalizationTolerance:F2}; the " +
                "table is not normalized.");
        }

        if (layersWithCoverage == 1)
        {
            // Not a hard failure - a single-layer terrain is legal - but for a biome splat pipeline it is
            // the silent-degeneracy shape from hecton8-runtime-source.md: uniform, plausible output from an
            // inert system. Named rather than gated, because gating it would refuse a legitimate run.
            Debug.LogWarning(
                $"[{ToolName}] '{t.name}': exactly ONE of {layers} layer(s) carries any weight, so the " +
                "whole terrain samples a single splat layer. That is legal but it is also what a biome " +
                "splat pass that collapsed to one biome looks like. The coverage check passed; " +
                "stratification was not checked, and means alone cannot tell the two apart.");
            report.AppendLine("    WARNING: only one layer carries weight; the map is single-layer.");
        }

        detail =
            $"{layers} layer mean weight(s) over {texels} texel(s) sum to {meanSum:F4}" +
            (normalized ? " (normalized)" : " (NOT normalized)") +
            $", {layersWithCoverage} layer(s) with coverage.";
        return true;
    }

    /// <summary>
    /// Opens the scene only when nothing would be lost, mirroring <c>DumpSplatmaps.cs:170-186</c> and
    /// <c>H8_RouteCaptureStation.cs:459-471</c>. In a shared working tree an unconditional OpenScene
    /// silently destroys another lane's unsaved edits.
    /// </summary>
    private static bool TryOpenSceneWithoutDiscardingWork(string scenePath)
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isDirty)
                continue;

            Debug.LogError(
                $"[{ToolName}] REFUSED to open '{scenePath}': scene '{scene.name}' has unsaved changes and " +
                "opening would discard them. No splat weight was averaged.");
            return false;
        }

        // Was unguarded and its result discarded. A scene that does not open must not be mistaken for a
        // scene with no terrain.
        UnityEngine.SceneManagement.Scene opened =
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!opened.IsValid() || !opened.isLoaded)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: '{scenePath}' did not open (IsValid={opened.IsValid()}, " +
                $"isLoaded={opened.isLoaded}). No splat weight was averaged.");
            return false;
        }

        return true;
    }

    /// <summary>Writes the failure into the report as well as the log, then returns the failure code.</summary>
    private static int Fail(StringBuilder report, string message)
    {
        report.AppendLine($"RESULT: FAILED - {message}");
        Debug.LogError($"[{ToolName}] FAILED: {message} Report at {ReportPath}");
        return ExitFailed;
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
