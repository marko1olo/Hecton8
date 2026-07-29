using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a grayscale hillshade PNG from a heightmap PNG so eroded relief can be judged by eye.
///
/// The entry point is <see cref="Gen"/> and the type deliberately stays in the global namespace:
/// Tools/BatchTasks scripts bind editor tools by reflection name (<c>-executeMethod HillshadeGen.Gen</c>),
/// so neither name may change. No .bat in the tree calls it today (checked 2026-07-29), so it was being
/// driven by hand - which is exactly the case where the log is the only channel anyone reads.
///
/// WHAT WAS WRONG:
///
/// * no exception handling and no exit code anywhere. Every failure below produced a Unity log line at
///   best and nothing a caller could branch on;
/// * both the input and the output path were hardcoded into another agent's private
///   <c>.gemini\antigravity\brain\7b5d06d2-...</c> scratch directory - outside the repo, unversioned,
///   invisible to anyone auditing this project's terrain evidence, and shared with several other tools
///   that wrote colliding filenames into it. There was no <c>Directory.CreateDirectory</c> anywhere, so on
///   any machine where that folder is absent the write threw on the first call;
/// * <c>LoadImage</c>'s bool return was discarded. On a failed decode the texture stays 2x2, the render
///   loop (<c>y=1; y&lt;height-1</c>) runs ZERO iterations, and the tool still wrote a PNG and logged
///   "Hillshade eroded done." - a 2x2 image of uninitialized memory reported as a terrain measurement;
/// * the loop skipped row/column 0 and the last row/column, so the one-pixel border of the output was
///   whatever <c>new Texture2D</c> happened to leave in memory. Output was not deterministic;
/// * no <c>Apply()</c> before <c>EncodeToPNG</c>, unlike the landed analog NoiseIsolatorTask.cs:195;
/// * both textures were leaked - no <c>DestroyImmediate</c>;
/// * no GPU-context refusal, no artifact verification, and no check that the input carried any relief at
///   all. A constant input yields a uniform hillshade, which reads as "the terrain is flat" rather than as
///   "this run measured nothing".
///
/// The shading math itself is unchanged on purpose - see <see cref="HeightScale"/>. Its limits are now
/// recorded in the provenance sidecar next to the PNG instead of being silently implied.
/// </summary>
public static class HillshadeGen
{
    private const string ToolName = "HillshadeGen";

    /// <summary>
    /// Per-tool subfolder inside the repo. `static readonly` and not `const` because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hillshade_gen");

    private static readonly string OutputPath = Path.Combine(OutputDir, "hillshade_eroded.png");

    private static readonly string ProvenancePath =
        Path.Combine(OutputDir, "hillshade_eroded_provenance.txt");

    private const string InputFileName = "heightmap_10km.png";

    /// <summary>
    /// Searched in order. The repo locations come first so the tool has an in-tree, versionable input
    /// convention; the third entry is the historical foreign-brain path, kept only because that is the
    /// one place the file currently exists on this machine. Reading from it is harmless - writing there
    /// was the defect. If none of these resolve, the run fails loudly instead of producing an artifact
    /// from nothing.
    /// </summary>
    private static readonly string[] InputCandidates =
    {
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hillshade_gen", InputFileName),
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", InputFileName),
        @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\heightmap_10km.png",
    };

    /// <summary>Floor for "this is a real PNG and not a truncated stub". Matches ExportArraySlices.cs:36.</summary>
    private const int MinimumPngBytes = 512;

    /// <summary>
    /// The render needs a 3x3 neighbourhood to mean anything, and 2x2 is exactly what a failed
    /// <c>LoadImage</c> leaves behind.
    /// </summary>
    private const int MinimumInputDimension = 3;

    /// <summary>
    /// UNCHANGED from the original (<c>12000f / 10000f</c>, commented "roughly"). It is NOT a metric
    /// vertical scale: the sampled channel is 0..1, this multiplies it to 0..1.2, and the normal below
    /// uses a constant <c>2f</c> for the two-pixel horizontal span - so the shading has no relation to the
    /// input's real metre extent and slopes read as exaggerated by an unknown factor. Left alone because
    /// changing it would silently redefine what every previously-captured hillshade meant; recorded in the
    /// provenance file so a reader knows the artifact is qualitative relief, not measured gradient.
    /// </summary>
    private const float HeightScale = 12000f / 10000f;

    private static readonly Vector3 LightDir = new Vector3(-1f, 0.5f, 1f).normalized;

    /// <summary>Below this spread the field is constant for all practical purposes.</summary>
    private const float FlatEpsilon = 1e-6f;

    public static void Gen()
    {
        // PART 4. This tool decodes a PNG into a Texture2D, reads it back pixel by pixel and encodes a PNG.
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 - "compute shaders and Graphics.Blit
        // return zeros with no GPU context" - and texture upload/readback is not guaranteed to carry real
        // pixels with a null device either. The failure mode is silent: an all-zero source produces one
        // uniform grey hillshade, the file is written, the exit code is 0, and the image reads as flat
        // terrain rather than as an editor launched with the wrong flags.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). The hillshade would be " +
                "a uniform or all-zero image indistinguishable from flat terrain, and no PNG was written. " +
                "Remove -nographics from the batch invocation and run again.");
            EditorApplication.Exit(3);
            return;
        }

        // Seeded with the failure code, not 0: if anything below ever gains a path that skips the
        // assignment, it must not fall through to reporting success.
        int exitCode = 2;
        try
        {
            exitCode = Generate();
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: no hillshade was produced at {OutputPath}. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        EditorApplication.Exit(exitCode);
    }

    /// <summary>
    /// Returns 0 only after the PNG has been written AND verified on disk AND shown to carry relief.
    /// Returns 2 for every "could not do the work" branch; each one logs what was not produced first.
    /// 1 is not part of this instrument layer's exit vocabulary.
    /// </summary>
    private static int Generate()
    {
        Directory.CreateDirectory(OutputDir);

        string inputPath = ResolveInputPath();
        if (inputPath == null)
        {
            // Was: File.ReadAllBytes on a hardcoded absolute path, throwing out of an uncaught method.
            Debug.LogError(
                $"[{ToolName}] FAILED: no input heightmap named '{InputFileName}' at any known location, so " +
                $"no hillshade was written to {OutputPath}. Tried: {string.Join(" | ", InputCandidates)}");
            return 2;
        }

        // Stale-artifact hygiene (hecton8-shaders-compute.md:43-44). The existence check at the end can
        // only prove freshness if the previous run's PNG is gone before this one writes.
        if (!DeleteStale(OutputPath)) return 2;
        if (!DeleteStale(ProvenancePath)) return 2;

        byte[] inputBytes = File.ReadAllBytes(inputPath);

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D hill = null;
        try
        {
            // markNonReadable: false - GetPixels below needs the CPU copy. LoadImage replaces the 2x2
            // placeholder dimensions and format on success.
            if (!source.LoadImage(inputBytes, false))
            {
                // Was: return value discarded. This is the branch that produced a 2x2 PNG and logged done.
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{inputPath}' ({inputBytes.Length} bytes) could not be decoded as " +
                    $"an image, so no hillshade was written to {OutputPath}.");
                return 2;
            }

            int width = source.width;
            int height = source.height;
            if (width < MinimumInputDimension || height < MinimumInputDimension)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: '{inputPath}' decoded to {width}x{height}, below the " +
                    $"{MinimumInputDimension}x{MinimumInputDimension} a 3x3 gradient needs. No hillshade " +
                    $"was written to {OutputPath}.");
                return 2;
            }

            long pixelCount = (long)width * height;

            // COLD ALLOC: Color[width*height] x2 - editor-only whole-image buffers, 16 B/px each, so
            // 2 * 16 * width * height bytes (logged below because the input resolution is not known until
            // it is decoded) - owner: HillshadeGen. Not streamed because the gradient needs neighbouring
            // rows and the previous per-pixel GetPixel/SetPixel form cost 4 managed interop calls per
            // pixel. GetPixels returns exactly what GetPixel returned, bottom-left origin, index y*w+x.
            Debug.Log(
                $"[{ToolName}] input {inputPath} -> {width}x{height}, buffering " +
                $"{(2L * 16L * pixelCount) / (1024L * 1024L)} MiB of Color for source and output.");

            Color[] sourcePixels = source.GetPixels();
            // Length taken from the decoded buffer, not recomputed, so SetPixels below cannot be handed a
            // mismatched array if GetPixels ever disagrees with width*height.
            Color[] hillPixels = new Color[sourcePixels.Length];

            // Min-folds seeded with float.MaxValue so an invalid candidate cannot win the fold
            // (hecton8-runtime-source.md, "Silent degeneracy is the dominant failure mode here").
            float sourceMin = float.MaxValue;
            float sourceMax = float.MinValue;
            double sourceSum = 0.0;
            float intensityMin = float.MaxValue;
            float intensityMax = float.MinValue;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    float raw = sourcePixels[index].r;
                    if (raw < sourceMin) sourceMin = raw;
                    if (raw > sourceMax) sourceMax = raw;
                    sourceSum += raw;

                    // Clamped neighbour sampling covers the full image instead of leaving a border of
                    // uninitialized texture memory, and matches the landed analogs
                    // (NoiseIsolatorTask.cs:143-148, GeologyAtlasTask.cs:419-422). Interior values are
                    // identical to the previous loop; only the border changes, from undefined to defined.
                    int xL = Mathf.Clamp(x - 1, 0, width - 1);
                    int xR = Mathf.Clamp(x + 1, 0, width - 1);
                    int yD = Mathf.Clamp(y - 1, 0, height - 1);
                    int yU = Mathf.Clamp(y + 1, 0, height - 1);

                    float hL = sourcePixels[y * width + xL].r * HeightScale;
                    float hR = sourcePixels[y * width + xR].r * HeightScale;
                    float hD = sourcePixels[yD * width + x].r * HeightScale;
                    float hU = sourcePixels[yU * width + x].r * HeightScale;

                    Vector3 normal = new Vector3(hL - hR, 2f, hD - hU).normalized;
                    float intensity = Mathf.Max(0f, Vector3.Dot(normal, LightDir));

                    if (intensity < intensityMin) intensityMin = intensity;
                    if (intensity > intensityMax) intensityMax = intensity;

                    hillPixels[index] = new Color(intensity, intensity, intensity, 1f);
                }
            }

            // Loud probe for the two ways this can succeed at producing nothing. Checked BEFORE the write
            // so a worthless image never lands on disk to be mistaken for evidence.
            if (sourceMax - sourceMin <= FlatEpsilon)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: the red channel of '{inputPath}' is constant at {sourceMin:F6} " +
                    $"across all {pixelCount} pixels, so the hillshade would be uniform and would read as " +
                    $"flat terrain. Nothing was written to {OutputPath}. Either the heightmap is empty or " +
                    "it stores height somewhere other than the red channel.");
                return 2;
            }

            if (intensityMax - intensityMin <= FlatEpsilon)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: the computed hillshade is uniform at {intensityMin:F6} despite a " +
                    $"non-constant input (red channel {sourceMin:F6}..{sourceMax:F6}). Nothing was written " +
                    $"to {OutputPath}.");
                return 2;
            }

            hill = new Texture2D(width, height, TextureFormat.RGB24, false);
            hill.SetPixels(hillPixels);
            // Was missing. Matches NoiseIsolatorTask.cs:195.
            hill.Apply(false, false);

            byte[] png = hill.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: EncodeToPNG returned " +
                    $"{(png == null ? "null" : "0 bytes")} for the {width}x{height} hillshade, so nothing " +
                    $"was written to {OutputPath}.");
                return 2;
            }

            File.WriteAllBytes(OutputPath, png);

            // "I called EncodeToPNG and did not throw" is not proof the artifact exists.
            if (!ArtifactIsPlausible(OutputPath, out string detail))
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: {png.Length} bytes were handed to File.WriteAllBytes without " +
                    $"throwing but the artifact is not usable - {OutputPath} {detail}.");
                return 2;
            }

            WriteProvenance(
                inputPath, inputBytes.Length, width, height,
                sourceMin, sourceMax, (float)(sourceSum / pixelCount),
                intensityMin, intensityMax, detail);

            Debug.Log(
                $"[{ToolName}] wrote a verified {width}x{height} hillshade to {OutputPath} ({detail}); " +
                $"intensity {intensityMin:F4}..{intensityMax:F4}, source red channel " +
                $"{sourceMin:F4}..{sourceMax:F4}. Provenance: {ProvenancePath}");
            return 0;
        }
        finally
        {
            // Both were leaked on every path, including success.
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            if (hill != null) UnityEngine.Object.DestroyImmediate(hill);
        }
    }

    private static string ResolveInputPath()
    {
        for (int i = 0; i < InputCandidates.Length; i++)
        {
            if (File.Exists(InputCandidates[i]))
                return InputCandidates[i];
        }
        return null;
    }

    private static bool DeleteStale(string path)
    {
        if (!File.Exists(path))
            return true;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: could not delete the stale artifact '{path}', so this run could not " +
                $"prove a fresh one was written rather than auditing the previous run's. {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Proves the file exists and is not truncated. It cannot prove the image is correct; the relief
    /// checks in <see cref="Generate"/> cover "not uniform", and visual acceptance stays with
    /// Docs/QUALITY_GATES.md.
    /// </summary>
    private static bool ArtifactIsPlausible(string path, out string detail)
    {
        if (!File.Exists(path))
        {
            detail = "does not exist on disk after the write";
            return false;
        }

        long length = new FileInfo(path).Length;
        if (length < MinimumPngBytes)
        {
            detail = $"is {length} bytes, below the {MinimumPngBytes}-byte floor for a real PNG";
            return false;
        }

        detail = $"{length} bytes";
        return true;
    }

    /// <summary>
    /// Records what the artifact actually is next to the artifact, so its limits travel with it instead of
    /// being rediscovered - or not - by whoever opens the PNG next.
    /// </summary>
    private static void WriteProvenance(
        string inputPath, int inputBytes, int width, int height,
        float sourceMin, float sourceMax, float sourceMean,
        float intensityMin, float intensityMax, string outputDetail)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine($"{ToolName} - hillshade provenance");
        text.AppendLine($"generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine($"graphicsDeviceType: {SystemInfo.graphicsDeviceType}");
        text.AppendLine($"input: {inputPath} ({inputBytes} bytes)");
        text.AppendLine($"decoded: {width}x{height}");
        text.AppendLine($"source red channel: min={sourceMin:F6} max={sourceMax:F6} mean={sourceMean:F6}");
        text.AppendLine($"height scale applied: {HeightScale} (dimensionless)");
        text.AppendLine($"light direction: {LightDir}");
        text.AppendLine($"output intensity: min={intensityMin:F6} max={intensityMax:F6}");
        text.AppendLine($"output: {OutputPath} ({outputDetail})");
        text.AppendLine();
        text.AppendLine("READ THIS BEFORE QUOTING THE IMAGE AS A MEASUREMENT:");
        text.AppendLine(
            "* height comes from the 8-bit red channel of a PNG, so the input is quantised to 256 levels;");
        text.AppendLine(
            "  relief finer than 1/255 of the source range is not present in this render at all.");
        text.AppendLine(
            "* the vertical scale is dimensionless. The channel is scaled to 0..1.2 and the surface normal");
        text.AppendLine(
            "  uses a constant 2.0 for the two-pixel horizontal span, so slope angles here are exaggerated");
        text.AppendLine(
            "  by an unknown factor and depend on the input resolution. Judge relief shape, not gradient.");
        text.AppendLine(
            "* border pixels are clamp-sampled, so the outermost row and column have a one-sided gradient.");

        File.WriteAllText(ProvenancePath, text.ToString());
    }
}
