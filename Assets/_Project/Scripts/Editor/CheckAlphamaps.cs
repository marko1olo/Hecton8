using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// VERIFIER. Reads the active terrain's splat layer weights at the centre texel and fails when they do
/// not add up to coverage. Sibling of <c>DumpSplat</c>/<c>DumpSplatmaps</c>, which dump the alphamap
/// textures as PNG; this one reads the numbers straight out of <see cref="TerrainData"/>.
///
/// WHAT WAS WRONG - every single one of its failure paths reported success, which for a verifier is the
/// worst available defect, because "the splatmaps are fine" and "I could not look at the splatmaps" were
/// the same exit code:
///
/// * the entire body sat inside <c>if (terrains.Length &gt; 0)</c> and <c>EditorApplication.Exit(0)</c>
///   sat OUTSIDE it. A scene with no terrain wrote "Terrains found: 0" and exited 0 - the cheapest
///   possible way to make "the terrain never generated" read as "the layer weights check out". This is
///   the identical shape fixed in <c>DumpSplatmaps.cs:13-16</c>;
/// * <c>TerrainData is null</c> was written into the report and then fell through to the same
///   <c>Exit(0)</c>;
/// * <c>alphamapLayers == 0</c> skipped the weight loop and fell through to the same <c>Exit(0)</c>. A
///   terrain with no layers cannot be textured at all;
/// * the layer weights themselves were READ, FORMATTED, AND NEVER TESTED. All-zero weights - which is
///   what an ungenerated splatmap and a <c>-nographics</c> readback both produce - were written out as
///   "Layer 0 weight at center: 0" and the tool exited 0. A number that is logged but never compared is
///   not a check;
/// * there was no try/catch, so the <c>td.alphamapTextures.Length</c> dereference or an out-of-range
///   <see cref="TerrainData.GetAlphamaps"/> left <c>Execute</c> with NO exit code set at all - which
///   under <c>-quit</c> ends the process at 0;
/// * the report went to the RELATIVE path <c>alphamap_check.txt</c>, i.e. into the Unity project root
///   next to Assets/ and Library/, unversioned, with no <c>Directory.CreateDirectory</c> and no per-tool
///   subfolder. Tools in this layer have already destroyed each other's evidence by sharing a directory.
///
/// SCOPE, stated so the output is not over-read: this samples ONE texel at the centre of the FIRST
/// active terrain. Weights summing to 1 there prove that terrain's alphamap is populated at that point.
/// They do not prove coverage across the terrain, and they say nothing about the other chunks. Whole-map
/// evidence is <c>DumpSplat</c>/<c>DumpSplatmaps</c>, and visual acceptance is
/// <c>Docs/QUALITY_GATES.md</c>'s job, never this file's.
/// </summary>
public static class CheckAlphamaps
{
    private const string ToolName = "CheckAlphamaps";

    /// <summary>
    /// Per-tool subfolder inside the repo, replacing a bare relative filename that landed in the project
    /// root. `static readonly` rather than `const` because <see cref="Path.Combine"/> is not a
    /// compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_alphamaps");

    private static readonly string ReportPath = Path.Combine(OutputDir, "alphamap_check.txt");

    /// <summary>
    /// Unity normalizes alphamap weights to sum to 1 per texel. A sum below this is not a lightly-painted
    /// texel, it is an absent weight table: no layer has any coverage there.
    /// </summary>
    private const float MinimumWeightSum = 0.5f;

    /// <summary>
    /// Tolerance on the sum-to-1 invariant. Weights are stored 8-bit per channel, so several layers of
    /// quantization error accumulate; a deviation larger than this means the table is not normalized,
    /// which is worth naming but is still a real reading of real data.
    /// </summary>
    private const float NormalizationTolerance = 0.02f;

    public static void Execute()
    {
        // PART 4. The weights this tool reads are produced by the MapMagic generation path.
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-38 bans -nographics for exactly this and
        // names alphamaps by name: "compute shaders and Graphics.Blit return zeros with no GPU context.
        // Poll EditorApplication.update for stable frames (Terrain length == 9, ALPHAMAPS LOADED, ...)".
        // Without a device this tool reads a table of zeros at the centre texel and reports "no layer
        // coverage" - a specific, plausible, entirely fabricated finding about the terrain, produced by
        // an editor launched wrong.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). The alphamaps are " +
                "not loaded and the weights would read back as zeros, which is indistinguishable from a " +
                "terrain with no layer coverage. Remove -nographics from the batch script.");
            EditorApplication.Exit(3);
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(ToolName);

        try
        {
            Directory.CreateDirectory(OutputDir);

            Terrain[] terrains = Terrain.activeTerrains;
            report.AppendLine($"Terrains found: {terrains.Length}");

            if (terrains.Length == 0)
            {
                // Was: written to the file, then Exit(0).
                Fail(report,
                    "no active Terrain in the loaded scene, so no alphamap was read. Nothing about " +
                    "layer weights was measured. Open the terrain scene (e.g. " +
                    "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity) and let generation settle before " +
                    "running this.");
                return;
            }

            Terrain t = terrains[0];
            report.AppendLine($"Inspecting first active terrain: '{t.name}' (of {terrains.Length})");

            TerrainData td = t.terrainData;
            if (td == null)
            {
                // Was: "TerrainData is null" written to the file, then Exit(0).
                Fail(report,
                    $"terrain '{t.name}' has no TerrainData, so it has no alphamaps to read. The check " +
                    "could not run.");
                return;
            }

            int resolution = td.alphamapResolution;
            int layers = td.alphamapLayers;
            Texture2D[] alphamapTextures = td.alphamapTextures;
            int textureCount = alphamapTextures == null ? 0 : alphamapTextures.Length;

            report.AppendLine($"Alphamap resolution: {resolution}");
            report.AppendLine($"Alphamap layers: {layers}");
            report.AppendLine($"Alphamap textures: {textureCount}");
            Debug.Log(
                $"[{ToolName}] terrain '{t.name}' alphamapResolution={resolution} " +
                $"alphamapLayers={layers} alphamapTextures={textureCount}");

            if (layers < 1)
            {
                // Was: the weight loop was skipped and the tool exited 0.
                Fail(report,
                    $"terrain '{t.name}' reports {layers} alphamap layer(s), so there is no weight to " +
                    "read. A terrain with no splat layers cannot be textured; reading nothing is not a " +
                    "pass.");
                return;
            }

            if (resolution < 1)
            {
                Fail(report,
                    $"terrain '{t.name}' reports alphamapResolution={resolution}, so there is no texel " +
                    "to sample.");
                return;
            }

            if (textureCount == 0)
            {
                // Was: dereferenced with no null check, and a 0 length was simply printed.
                Fail(report,
                    $"terrain '{t.name}' claims {layers} layer(s) at resolution {resolution} but " +
                    "exposes no alphamap textures, so the weights have no backing data.");
                return;
            }

            int centre = resolution / 2;
            float[,,] maps = td.GetAlphamaps(centre, centre, 1, 1);
            if (maps == null)
            {
                Fail(report,
                    $"GetAlphamaps returned null at texel ({centre},{centre}) of '{t.name}'. No weight " +
                    "was read.");
                return;
            }

            int returnedLayers = maps.GetLength(2);
            if (returnedLayers < layers)
            {
                // Do not read a partial table as a full one - and do not index past what was handed back.
                Fail(report,
                    $"GetAlphamaps returned {returnedLayers} layer(s) but the terrain reports {layers}. " +
                    "The weight table does not match the terrain it came from, so it cannot be read as " +
                    "a measurement of it.");
                return;
            }

            // THE CHECK. Weights must sum to coverage. This is the comparison the old tool never made.
            float sum = 0f;
            for (int i = 0; i < layers; i++)
            {
                float weight = maps[0, 0, i];
                sum += weight;
                report.AppendLine($"Layer {i} weight at center: {weight:F4}");
                Debug.Log($"[{ToolName}] layer {i} weight at texel ({centre},{centre}): {weight:F4}");
            }

            report.AppendLine($"Weight sum at center: {sum:F4}");

            if (sum < MinimumWeightSum)
            {
                Fail(report,
                    $"the {layers} layer weights at texel ({centre},{centre}) of '{t.name}' sum to " +
                    $"{sum:F4}, below the {MinimumWeightSum:F2} floor. No layer has coverage at that " +
                    "texel - which is what an ungenerated or never-painted splatmap looks like, and " +
                    "what a readback with no alphamap data looks like. This is the branch that used to " +
                    "print zeros and exit 0.");
                return;
            }

            bool normalized = Mathf.Abs(sum - 1f) <= NormalizationTolerance;
            if (!normalized)
            {
                Debug.LogWarning(
                    $"[{ToolName}] weights at texel ({centre},{centre}) sum to {sum:F4}, not 1.0 within " +
                    $"{NormalizationTolerance:F2}. Unity normalizes alphamaps on write, so this table " +
                    "was not written through the normal path. The weights are real data and the check " +
                    "passed, but do not treat the individual values as calibrated.");
                report.AppendLine(
                    $"WARNING: sum deviates from 1.0 by more than {NormalizationTolerance:F2}; the " +
                    "table is not normalized.");
            }

            report.AppendLine(
                $"RESULT: PASS - {layers} layer weight(s) read at texel ({centre},{centre}) of " +
                $"'{t.name}', summing to {sum:F4}" + (normalized ? " (normalized)." : " (NOT normalized)."));
            WriteReport(report);
            Debug.Log(
                $"[{ToolName}] PASS: '{t.name}' has real layer coverage at texel ({centre},{centre}); " +
                $"{layers} weight(s) sum to {sum:F4}. This is a single-texel spot check on one terrain, " +
                $"not proof of coverage across the map. Report at {ReportPath}");
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            // Was: no try/catch at all, so a throw here set no exit code whatsoever.
            Fail(report, $"threw before the weights could be verified; nothing is measured. {ex}");
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
