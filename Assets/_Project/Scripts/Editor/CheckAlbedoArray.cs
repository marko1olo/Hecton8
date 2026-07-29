using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// VERIFIER. Answers one question about <c>Terrain_AlbedoArray.asset</c>: does every slice contain real
/// texture data? Its per-slice average colour is a readout for a human; the pass/fail is the
/// constant-fill test below.
///
/// WHAT WAS WRONG - this file was the exact failure mode this instrument layer exists to stop, because
/// its output is read as permission to believe the terrain textures are fine:
///
/// * the average RGB was COMPUTED, LOGGED, AND NEVER TESTED. Every slice fell through to
///   <c>EditorApplication.Exit(0)</c> whatever the numbers were. A slice of raw uninitialized memory -
///   0xCD fill, which <c>BakeDeepSeaTerrainArrays.cs:230</c> documents as what
///   <c>Graphics.CopyTexture</c> actually shipped into these assets - reads out as
///   "avg RGB 0.804, 0.804, 0.804", a completely plausible light-grey albedo, and the tool called that
///   a pass. An all-black slice reads 0.000 and was also a pass;
/// * <c>depth == 0</c> skipped the loop body entirely and reached <c>Exit(0)</c>. Zero slices inspected,
///   reported as success - a verifier that verified nothing and said so in the one code that gets read;
/// * <see cref="Texture2DArray.GetPixels"/> was never null- or length-checked. When the array has no
///   readable CPU copy - the documented state of a <c>Graphics.CopyTexture</c>-built array,
///   <c>HectonTerrainTextureArrayBuilder.cs:195</c> - <c>pixels.Length</c> is 0, <c>r/c</c> divides by
///   zero, and the tool logged "avg RGB NaN, NaN, NaN" and exited 0;
/// * there was no try/catch at all. <c>GetPixels</c> throws on a non-readable or non-decodable array,
///   and this asset is BC7 (<c>BakeDeepSeaTerrainArrays.cs:25</c>), so the throw left <c>Execute</c>
///   with NO exit code set - which under <c>-quit</c> ends the process at 0;
/// * the "array not found" branch used <c>Debug.Log</c>, not <c>LogError</c>, and exited 1, which is
///   outside this layer's vocabulary (0 proved / 2 could not do the work / 3 no GPU / 4 timeout);
/// * it logged under the tag <c>[FAS]</c>, which belongs to FixAndShoot - the same log-prefix collision
///   <c>DumpSplatmaps.cs:22</c> was fixed for. Two unrelated tools sharing a prefix in one batchmode log
///   is the evidence collision that a shared output folder is;
/// * it wrote no file, so its verdict existed only in a log the next batch run overwrites.
///
/// NO GPU REFUSAL HERE, on purpose, and the reasoning matters because a false gate would block the one
/// cheap honest check available headless. Nothing in this file renders, blits, reads pixels off a
/// RenderTexture, encodes a PNG or dispatches a compute shader. <see cref="Texture2DArray.GetPixelData"/>
/// and <see cref="Texture2DArray.GetPixels"/> both read the serialized CPU-side buffer - the buffer that
/// lives in the .asset on disk, and the buffer the 0xCD incident above was ABOUT.
/// <c>C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37</c> bans <c>-nographics</c> for tools whose
/// numbers become zeros without a graphics device; this tool's numbers come off disk. The case a gate
/// would supposedly catch - a readback that degenerates to nothing - is instead caught directly and
/// FAILED below, which is strictly stronger: it also catches the with-GPU case where the CPU copy is
/// simply absent, which is the bug that actually happened here. If you ever add a Blit, a ReadPixels or
/// an EncodeToPNG to this file, that reasoning stops holding and it needs the refusal block that
/// <c>ExportArraySlices.cs:44-53</c> carries.
/// </summary>
public static class CheckAlbedoArray
{
    private const string ToolName = "CheckAlbedoArray";

    /// <summary>
    /// Per-tool subfolder. `static readonly` rather than `const` because <see cref="Path.Combine"/> is
    /// not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "check_albedo_array");

    private static readonly string ReportPath =
        Path.Combine(OutputDir, "albedo_array_report.txt");

    private const string ArrayPath =
        "Assets/_Project/Art/Textures/TerrainArrays/Terrain_AlbedoArray.asset";

    public static void Execute()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"{ToolName}: {ArrayPath}");

        int verifiedSlices = 0;

        try
        {
            Directory.CreateDirectory(OutputDir);

            Texture2DArray albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPath);
            if (albedoArray == null)
            {
                // Was: Debug.Log (not an error) + Exit(1).
                Fail(report,
                    $"no Texture2DArray at '{ArrayPath}' (missing, or the asset is not a " +
                    "Texture2DArray). No slice was inspected, so nothing about the terrain albedo is " +
                    "verified by this run.");
                return;
            }

            report.AppendLine(
                $"{albedoArray.width}x{albedoArray.height} depth={albedoArray.depth} " +
                $"format={albedoArray.format} mips={albedoArray.mipmapCount}");
            Debug.Log(
                $"[{ToolName}] {ArrayPath} {albedoArray.width}x{albedoArray.height} " +
                $"depth={albedoArray.depth} format={albedoArray.format} mips={albedoArray.mipmapCount}");

            if (albedoArray.depth < 1)
            {
                // Was: the for loop did not execute and the tool exited 0.
                Fail(report,
                    $"the array has depth {albedoArray.depth}, so there is no slice to inspect. An " +
                    "array with no slices cannot texture anything; inspecting nothing is not a pass.");
                return;
            }

            for (int slice = 0; slice < albedoArray.depth; slice++)
            {
                // THE CHECK. Raw mip-0 bytes, so it is format-agnostic and cannot throw on BC7 the way
                // GetPixels can. Same test, same reason, as the guard that stopped the garbage array
                // being written in the first place: BakeDeepSeaTerrainArrays.cs:256-284.
                var raw = albedoArray.GetPixelData<byte>(0, slice);
                if (raw.Length == 0)
                {
                    Fail(report,
                        $"slice {slice} has no readable CPU data (GetPixelData returned 0 bytes). The " +
                        "array's pixel buffer is absent, so its contents cannot be inspected and no " +
                        "statement about this array is supported by this run.");
                    return;
                }

                byte first = raw[0];
                bool varies = false;
                for (int i = 1; i < raw.Length; i++)
                {
                    if (raw[i] != first) { varies = true; break; }
                }

                if (!varies)
                {
                    // This is the branch the old tool reported as a measurement. 0xCD is the MSVC
                    // uninitialized-heap fill byte, so first == 0xCD means the serialized buffer is
                    // raw uninitialized memory; first == 0 means the slice was never populated at all.
                    Fail(report,
                        $"slice {slice} mip 0 is the single repeated byte 0x{first:X2} across all " +
                        $"{raw.Length} bytes. That is a constant-fill slice - uninitialized memory " +
                        "(0xCD) or a never-populated slice (0x00) - not an albedo texture. Its average " +
                        "colour would read as a perfectly plausible measurement, which is exactly why " +
                        "this is a failure and not a log line.");
                    return;
                }

                string readout = TryFormatAverageColor(albedoArray, slice);
                string detail = readout ?? "average colour unavailable for this format (see warning above)";

                verifiedSlices++;
                report.AppendLine($"slice {slice}: VARIES (first byte 0x{first:X2}); {detail}");
                Debug.Log($"[{ToolName}] slice {slice} VARIES; {detail}");
            }

            if (verifiedSlices != albedoArray.depth)
            {
                // Cannot happen while every failure above returns, and that is the point: if a future
                // edit makes a slice skippable, this refuses to call the run a pass.
                Fail(report,
                    $"verified {verifiedSlices} of {albedoArray.depth} slice(s). Not every slice was " +
                    "inspected, so the array is not verified.");
                return;
            }
        }
        catch (System.Exception ex)
        {
            // GetPixelData throws when the array is not readable at all - the state a GPU-only
            // Graphics.CopyTexture build leaves it in. That is a failed check, never a pass.
            Fail(report,
                $"threw after verifying {verifiedSlices} slice(s); the array is NOT verified. {ex}");
            return;
        }

        report.AppendLine($"RESULT: PASS - {verifiedSlices} slice(s) contain non-constant data.");
        WriteReport(report);
        Debug.Log(
            $"[{ToolName}] PASS: all {verifiedSlices} slice(s) of {ArrayPath} contain non-constant " +
            $"pixel data. Report at {ReportPath}");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// The per-slice average colour this tool has always printed. It is a READOUT, not the check: it is
    /// reported for a human reading the log and is deliberately not allowed to decide the exit code,
    /// because any average whatsoever is consistent with garbage data. Returns null when the format
    /// cannot be decoded to <see cref="Color"/>[] - BC7 is this project's albedo default
    /// (<c>hecton8-shaders-compute.md</c> render defaults), so that is an expected outcome and must not
    /// fail the run on its own; the byte-level test above has already passed by then.
    /// </summary>
    private static string TryFormatAverageColor(Texture2DArray array, int slice)
    {
        try
        {
            Color[] pixels = array.GetPixels(slice, 0);
            if (pixels == null || pixels.Length == 0)
            {
                // Logged, not silent: the caller's line says "see warning above", and an unexplained
                // missing readout is how a reader starts guessing.
                Debug.LogWarning(
                    $"[{ToolName}] slice {slice}: GetPixels returned no data, so no average colour is " +
                    "reported. The slice still passed the constant-fill test, which reads the raw " +
                    "buffer directly.");
                return null;
            }

            // double, not float: ~1M samples per 1024x1024 slice accumulate enough rounding error in
            // float32 to move the third decimal this line prints.
            double r = 0.0, g = 0.0, b = 0.0;
            for (int i = 0; i < pixels.Length; i++)
            {
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
            }

            int c = pixels.Length;
            return $"avg RGB {r / c:F3}, {g / c:F3}, {b / c:F3} over {c} px";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning(
                $"[{ToolName}] slice {slice}: GetPixels could not decode this format, so no average " +
                $"colour is reported. The slice still passed the constant-fill test. {ex.Message}");
            return null;
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
