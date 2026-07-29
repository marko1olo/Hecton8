using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using Hecton8.World;
using Unity.Burst;

/// <summary>
/// Renders the terrain diagnostic set straight from the macro-geology and meso-detail fields, with no
/// MapMagic and no scene: five hillshades at 4096/1024/256/64/16 m windows (phase A) plus five debug maps
/// at 1024 m (phase B).
///
/// The entry point is <see cref="RunDiagnostics"/> and the type deliberately stays in the global namespace:
/// Tools/BatchTasks binds editor tools by reflection name
/// (<c>-executeMethod HectonDiagnosticRenderer.RunDiagnostics</c>), so neither may change. No .bat in the
/// tree calls it today (checked 2026-07-29 against Tools/BatchTasks/*.bat), so it was driven by hand or
/// from the menu item - exactly the case where the Unity log is the only channel anyone reads.
///
/// The nested job structs are PUBLIC on purpose: GraphOutputRenderer.cs reuses HeightMapJob, HillshadeJob
/// and SlopeMapJob so the raw field and the graph output are shaded by identical math. Do not rename them.
///
/// WHAT WAS WRONG:
///
/// * OUTPUT WENT TO ANOTHER AGENT'S PRIVATE SCRATCH DIRECTORY. <c>OutDir</c> was hardcoded to
///   <c>C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...</c> - outside the repo, unversioned,
///   invisible to anyone auditing this project's terrain evidence, and shared with a dozen other tools in
///   this tree including GraphOutputRenderer. There was no <c>Directory.CreateDirectory</c> anywhere, so on
///   any machine where that folder is absent the very first <c>File.WriteAllBytes</c> threw.
/// * NO GPU REFUSAL. The tool encodes ten PNGs. Under <c>-nographics</c> it emitted black or all-zero
///   images and exited 0, and a human then read the artifact as a measurement of the world.
///   C:\hades\.claude\rules\hecton8-shaders-compute.md:36-39.
/// * EXIT CODE 1 FOR EXCEPTIONS. 1 is not in this instrument layer's vocabulary
///   (0 proved / 2 exception / 3 refused-no-GPU / 4 timeout).
/// * NO ARTIFACT VERIFICATION. "EncodeToPNG did not throw" was the whole proof - nothing checked the file
///   existed on disk or exceeded a truncated stub, and <c>Exit(0)</c> fired unconditionally after the loop.
/// * LOCALE-DEPENDENT FILENAMES. <c>$"PhaseA_{size}m_cell{cellSize:F2}.png"</c> formats with the current
///   culture, so this box (Russian locale, see C:\hades\CLAUDE.md on localised build output) wrote
///   <c>PhaseA_256m_cell0,25.png</c> while an invariant machine wrote <c>cell0.25</c> - the same render under
///   two names.
/// * TEXTURE LEAK ON THROW. <c>DestroyImmediate</c> ran only on the happy path.
/// * NATIVEARRAY LEAKS. Both render methods disposed at the end of the happy path only.
///
/// The shading, slope, curvature and diff MATH IS UNCHANGED on purpose - changing it would silently
/// redefine what every previously-captured image meant. Its real limits are recorded in the provenance
/// sidecar next to the PNGs and called out on the jobs themselves; two of the phase B maps are close to
/// worthless as written and now say so in the log instead of implying a measurement.
/// </summary>
public static class HectonDiagnosticRenderer
{
    private const string ToolName = "HectonDiagnosticRenderer";

    /// <summary>
    /// PER-TOOL subfolder inside the repo. `static readonly` and not `const` because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    ///
    /// Distinct from GraphOutputRenderer's Logs/graph_output_renderer: the two tools previously wrote into
    /// ONE shared directory, and Logs/TerrainDiagnostics in this same repo is already shared by three tools
    /// (TerrainDiagnosticsWindow.cs:75, HeadlessTerrainDumper.cs:108, OfflineErosionBakePipeline.cs:181) -
    /// the configuration that already destroyed evidence here.
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hecton_diagnostic_renderer");

    private static readonly string ProvenancePath =
        Path.Combine(OutputDir, "terrain_diagnostics_provenance.txt");

    /// <summary>Floor for "this is a real PNG and not a truncated stub". Matches HillshadeGen.cs:71.</summary>
    private const int MinimumPngBytes = 512;

    /// <summary>Five phase A hillshades plus five phase B debug maps.</summary>
    private const int ExpectedArtifactCount = 10;

    /// <summary>
    /// Below this metre spread the height field is constant for all practical purposes, which is what an
    /// all-zero readback and a dead generator both look like. Any real 16 m window of this world varies by
    /// far more than a micron.
    /// </summary>
    private const float FlatEpsilonMeters = 1e-6f;

    private const uint Seed = 12345;

    private static readonly List<string> VerifiedArtifacts = new List<string>(ExpectedArtifactCount);
    private static readonly List<string> RenderNotes = new List<string>(16);

    /// <summary>
    /// Note the menu item and <see cref="EditorApplication.Exit"/> coexisting: clicking the menu entry quits
    /// the editor. That is the landed convention for this instrument layer (see
    /// Assets/_Project/Scripts/Editor/Diagnostics/ZoomProofDumpTask.cs:19-51) because the batch exit code is
    /// the contract, and it is left as-is rather than quietly forked into two behaviours.
    /// </summary>
    [MenuItem("Tools/Hecton/Run Terrain Diagnostics")]
    public static void RunDiagnostics()
    {
        // PART 4. This tool builds ten Texture2Ds, uploads them with SetPixelData/Apply and encodes ten
        // PNGs. C:\hades\.claude\rules\hecton8-shaders-compute.md:36-39 - "compute shaders and
        // Graphics.Blit return zeros with no GPU context". The failure is silent: zeros render as one
        // uniform image indistinguishable from a real capture of flat seabed, the exit code was 0, and the
        // artifact then gets quoted as a measurement of the world.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). Texture upload and " +
                "readback return ZEROS here, so every hillshade and debug map would be black or uniform while " +
                "looking exactly like a real capture. No PNG was written. Remove -nographics from the batch " +
                "invocation and run again.");
            EditorApplication.Exit(3);
            return;
        }

        Debug.Log($"[{ToolName}] starting Phase A & B. Artifacts go to {OutputDir}");

        // Static state survives an -executeMethod invocation inside a warm editor; reset it so a second run
        // cannot inherit the first run's artifact list and report success on stale counts.
        VerifiedArtifacts.Clear();
        RenderNotes.Clear();

        // Seeded with the failure code, not 0: if anything below ever gains a path that skips the
        // assignment, it must not fall through to reporting success.
        int exitCode = 2;
        try
        {
            exitCode = Generate();
        }
        catch (Exception ex)
        {
            // Was Exit(1).
            Debug.LogError(
                $"[{ToolName}] FAILED: the terrain diagnostic set was not produced in {OutputDir}. " +
                $"{VerifiedArtifacts.Count} of {ExpectedArtifactCount} images had been verified when this threw. {ex}");
            EditorApplication.Exit(2);
            return;
        }

        EditorApplication.Exit(exitCode);
    }

    /// <summary>
    /// Returns 0 only after all ten PNGs have been written AND verified on disk. Returns 2 for every
    /// "could not do the work" branch; each one logs what was not produced first. The old code called
    /// <c>Exit(0)</c> unconditionally after the render loop.
    /// </summary>
    private static int Generate()
    {
        Directory.CreateDirectory(OutputDir);

        float centerX = 4000f; // Edge of the shelf
        float centerZ = 4000f;
        int res = 1024;

        // Phase A: Scales
        float[] scales = { 4096f, 1024f, 256f, 64f, 16f };
        foreach (float scale in scales)
        {
            if (!RenderPhaseA(centerX, centerZ, scale, res)) return 2;
        }

        // Phase B: Debug Maps at 1024m scale
        if (!RenderPhaseB(centerX, centerZ, 1024f, res)) return 2;

        if (VerifiedArtifacts.Count != ExpectedArtifactCount)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: only {VerifiedArtifacts.Count} of {ExpectedArtifactCount} images were " +
                $"written and verified in {OutputDir}, so the diagnostic set is incomplete and must not be read " +
                $"as one. Have: {string.Join(" | ", VerifiedArtifacts)}");
            return 2;
        }

        WriteProvenance(centerX, centerZ, res, scales);

        Debug.Log(
            $"[{ToolName}] wrote and verified all {ExpectedArtifactCount} images in {OutputDir}. " +
            $"Provenance: {ProvenancePath}");
        return 0;
    }

    private static bool RenderPhaseA(float cx, float cz, float size, int res)
    {
        float cellSize = size / res;
        var p = WorldMacroGeologyParams.CreateDefault(Seed);

        // COLD ALLOC: NativeArray<float>[1048576] = 4 MiB + NativeArray<Color32>[1048576] = 4 MiB -
        // editor-only whole-image buffers for a 1024x1024 render - owner: HectonDiagnosticRenderer. Not
        // streamed because the hillshade gradient needs neighbouring rows.
        var heights = new NativeArray<float>(res * res, Allocator.TempJob);
        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        try
        {
            var job = new HeightMapJob {
                Heights = heights,
                Params = p,
                Width = res,
                CellSize = cellSize,
                StartX = cx - size * 0.5f,
                StartZ = cz - size * 0.5f,
                IncludeMeso = true
            };
            job.Schedule(res * res, 64).Complete();

            // Loud probe before anything lands on disk: a constant field renders as one uniform grey image
            // that reads as "the seabed here is flat" rather than "this run measured nothing".
            if (!HeightFieldIsUsable(heights, $"PhaseA {size} m window (cell {Inv(cellSize, "F4")} m, " +
                    $"detailStrength {Inv(CellSizeToDetailStrength(cellSize), "F3")})", out string stats))
                return false;
            RenderNotes.Add(stats);

            // Hillshade
            var hsJob = new HillshadeJob {
                Heights = heights,
                Colors = colors,
                Width = res,
                CellSize = cellSize,
                SunDir = math.normalize(new float3(-1f, 0.5f, -1f))
            };
            hsJob.Schedule(res * res, 64).Complete();

            // Was $"PhaseA_{size}m_cell{cellSize:F2}.png" - current-culture formatting, so this box wrote
            // "cell0,25" and an invariant machine wrote "cell0.25" for the identical render.
            return SavePNG(colors, res, res,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "PhaseA_{0:0.###}m_cell{1:F2}.png", size, cellSize));
        }
        finally
        {
            // Was disposed on the happy path only: any throw out of SavePNG leaked 8 MiB.
            if (heights.IsCreated) heights.Dispose();
            if (colors.IsCreated) colors.Dispose();
        }
    }

    private static bool RenderPhaseB(float cx, float cz, float size, int res)
    {
        float cellSize = size / res;
        var p = WorldMacroGeologyParams.CreateDefault(Seed);

        // COLD ALLOC: NativeArray<float>[1048576] x2 = 8 MiB + NativeArray<Color32>[1048576] = 4 MiB -
        // editor-only whole-image buffers - owner: HectonDiagnosticRenderer.
        var heightsMacro = new NativeArray<float>(res * res, Allocator.TempJob);
        var heightsFull = new NativeArray<float>(res * res, Allocator.TempJob);
        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        try
        {
            var j1 = new HeightMapJob { Heights = heightsMacro, Params = p, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f, IncludeMeso = false };
            var j2 = new HeightMapJob { Heights = heightsFull, Params = p, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f, IncludeMeso = true };

            j1.Schedule(res * res, 64).Complete();
            j2.Schedule(res * res, 64).Complete();

            if (!HeightFieldIsUsable(heightsMacro, $"PhaseB macro-only field ({size} m window, cell {Inv(cellSize, "F4")} m)", out string macroStats))
                return false;
            RenderNotes.Add(macroStats);

            if (!HeightFieldIsUsable(heightsFull, $"PhaseB macro+meso field ({size} m window, cell {Inv(cellSize, "F4")} m)", out string fullStats))
                return false;
            RenderNotes.Add(fullStats);

            // 1. Slope map
            var slopeJob = new SlopeMapJob { Heights = heightsFull, Colors = colors, Width = res, CellSize = cellSize };
            slopeJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, "PhaseB_Slope.png")) return false;

            // 2. Curvature map
            var curvJob = new CurvatureMapJob { Heights = heightsFull, Colors = colors, Width = res, CellSize = cellSize };
            curvJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, "PhaseB_Curvature.png")) return false;

            // 3. Detail Strength map. See DetailStrengthMapJob: this map is CONSTANT by construction and
            // carries no spatial information at all. Kept only so the artifact set does not change shape
            // under this fix; the warning below is what a reader needs.
            var detailJob = new DetailStrengthMapJob { Params = p, Colors = colors, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f };
            detailJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, "PhaseB_DetailStrength.png")) return false;

            // 4. Meso Diff map
            var diffJob = new DiffMapJob { Heights1 = heightsMacro, Heights2 = heightsFull, Colors = colors };
            diffJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, "PhaseB_MesoDiff.png")) return false;

            // 5. Masks map
            var maskJob = new MaskMapJob { Params = p, Colors = colors, Width = res, CellSize = cellSize, StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f };
            maskJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, "PhaseB_FeatureMasks.png")) return false;

            return true;
        }
        finally
        {
            if (heightsMacro.IsCreated) heightsMacro.Dispose();
            if (heightsFull.IsCreated) heightsFull.Dispose();
            if (colors.IsCreated) colors.Dispose();
        }
    }

    /// <summary>
    /// Rejects the two ways this tool can succeed at measuring nothing: a non-finite field (a real math bug
    /// upstream, which clamps to arbitrary bytes and renders as plausible relief) and a constant field (an
    /// all-zero readback or a dead generator, which renders as flat seabed).
    /// Min-fold seeded with <c>float.MaxValue</c> per
    /// C:\hades\.claude\rules\hecton8-runtime-source.md, "Silent degeneracy is the dominant failure mode".
    /// </summary>
    private static bool HeightFieldIsUsable(NativeArray<float> heights, string label, out string stats)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        double sum = 0.0;
        long finite = 0;
        long nan = 0;
        long inf = 0;

        for (int i = 0; i < heights.Length; i++)
        {
            float h = heights[i];
            if (float.IsNaN(h)) { nan++; continue; }
            if (float.IsInfinity(h)) { inf++; continue; }
            if (h < min) min = h;
            if (h > max) max = h;
            sum += h;
            finite++;
        }

        if (nan > 0 || inf > 0)
        {
            stats = null;
            Debug.LogError(
                $"[{ToolName}] FAILED: the height field for {label} contains {nan} NaN and {inf} Inf samples out " +
                $"of {heights.Length}. Those clamp to arbitrary bytes and render as plausible relief, so no PNG " +
                "was written. This is a real math bug in generation, not a rendering problem.");
            return false;
        }

        if (finite == 0 || max - min <= FlatEpsilonMeters)
        {
            stats = null;
            Debug.LogError(
                $"[{ToolName}] FAILED: the height field for {label} is constant at {min:F6} m across all " +
                $"{heights.Length} samples, so the hillshade would be uniform and would read as flat seabed. " +
                "Nothing was written. Either the generator produced nothing or this editor has no real GPU " +
                "readback.");
            return false;
        }

        stats = $"{label}: height m min={min:F3} max={max:F3} mean={(sum / finite):F3} " +
                $"range={(max - min):F3} finite={finite}";
        Debug.Log($"[{ToolName}] {stats}");
        return true;
    }

    /// <summary>
    /// Deletes any stale artifact of the same name first (hecton8-shaders-compute.md:43-44 - otherwise the
    /// existence check below audits the previous run), then writes and VERIFIES. "EncodeToPNG did not throw"
    /// is not proof a usable file exists.
    /// </summary>
    private static bool SavePNG(NativeArray<Color32> colors, int w, int h, string filename)
    {
        string path = Path.Combine(OutputDir, filename);

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: could not delete the stale artifact '{path}', so this run could not " +
                    $"prove a fresh {filename} was written rather than auditing the previous run's. {ex.Message}");
                return false;
            }
        }

        // Reported, not gated: PhaseB_DetailStrength is uniform BY CONSTRUCTION (see DetailStrengthMapJob),
        // so a hard uniformity gate here would make every run fail. A uniform map is still worth shouting
        // about, because uniform is exactly what a no-GPU or dead-generator run produces.
        DescribeColorSpread(colors, out string spread, out bool uniform);
        if (uniform)
        {
            Debug.LogWarning(
                $"[{ToolName}] {filename} is a UNIFORM image ({spread}). It carries no spatial information. " +
                "Do not read it as a map of anything.");
        }

        Texture2D tex = null;
        try
        {
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixelData(colors, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: EncodeToPNG returned {(png == null ? "null" : "0 bytes")} for the " +
                    $"{w}x{h} {filename}, so nothing was written to {path}.");
                return false;
            }

            File.WriteAllBytes(path, png);

            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: {png.Length} bytes were handed to File.WriteAllBytes without throwing " +
                    $"but {path} does not exist on disk afterwards.");
                return false;
            }

            long length = new FileInfo(path).Length;
            if (length < MinimumPngBytes)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: {path} is {length} bytes, below the {MinimumPngBytes}-byte floor for a " +
                    "real PNG. The artifact is truncated and must not be read as evidence.");
                return false;
            }

            VerifiedArtifacts.Add($"{filename} ({length} bytes, {spread})");
            Debug.Log($"[{ToolName}] verified {filename} ({length} bytes, {spread}) at {path}");
            return true;
        }
        finally
        {
            // Was leaked on the exception path.
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    /// <summary>Per-channel byte range of the encoded image, so a reader can tell a real map from a flat fill.</summary>
    private static void DescribeColorSpread(NativeArray<Color32> colors, out string spread, out bool uniform)
    {
        byte rMin = 255, gMin = 255, bMin = 255;
        byte rMax = 0, gMax = 0, bMax = 0;

        for (int i = 0; i < colors.Length; i++)
        {
            Color32 c = colors[i];
            if (c.r < rMin) rMin = c.r; if (c.r > rMax) rMax = c.r;
            if (c.g < gMin) gMin = c.g; if (c.g > gMax) gMax = c.g;
            if (c.b < bMin) bMin = c.b; if (c.b > bMax) bMax = c.b;
        }

        uniform = rMin == rMax && gMin == gMax && bMin == bMax;
        spread = uniform
            ? $"constant RGB({rMin},{gMin},{bMin})"
            : $"R {rMin}-{rMax} G {gMin}-{gMax} B {bMin}-{bMax}";
    }

    /// <summary>Culture-independent number formatting for filenames and log text.</summary>
    private static string Inv(float value, string format)
    {
        return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Records what the images actually are next to the images, so their limits travel with them instead of
    /// being rediscovered - or not - by whoever opens the PNGs next.
    /// </summary>
    private static void WriteProvenance(float cx, float cz, int res, float[] scales)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine($"{ToolName} - terrain diagnostic set (phase A hillshades, phase B debug maps)");
        text.AppendLine($"generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine($"graphicsDeviceType: {SystemInfo.graphicsDeviceType}");
        text.AppendLine($"unity: {Application.unityVersion}");
        text.AppendLine($"window center: ({cx}, {cz}) world metres, {res}x{res} px");
        text.AppendLine($"macro geology seed: {Seed}");
        text.AppendLine($"phase A window sizes (m): {string.Join(", ", Array.ConvertAll(scales, s => Inv(s, "0.###")))}");
        text.AppendLine($"sun direction: normalize(-1, 0.5, -1)");
        text.AppendLine("source: WorldMacroGeologyFields + WorldTerrainMesoDetailFields sampled directly.");
        text.AppendLine("        NO MapMagic graph and NO Terrain involved - this is the INPUT to the graph,");
        text.AppendLine("        not what a player would see.");
        text.AppendLine();
        text.AppendLine("MEASURED HEIGHT FIELDS:");
        for (int i = 0; i < RenderNotes.Count; i++) text.AppendLine($"* {RenderNotes[i]}");
        text.AppendLine();
        text.AppendLine("ARTIFACTS (all verified to exist and exceed the truncation floor):");
        for (int i = 0; i < VerifiedArtifacts.Count; i++) text.AppendLine($"* {VerifiedArtifacts[i]}");
        text.AppendLine();
        text.AppendLine("READ THIS BEFORE QUOTING AN IMAGE AS A MEASUREMENT:");
        text.AppendLine("* PhaseB_DetailStrength.png is CONSTANT BY CONSTRUCTION. DetailStrengthMapJob derives its");
        text.AppendLine("  value from CellSize alone, which is uniform across the window, so the map cannot vary.");
        text.AppendLine("  It records one number as a 1024x1024 image; read the number in this file instead.");
        text.AppendLine("* PhaseB_MesoDiff.png saturates at a 0.1 m difference (diff * 10 * 255, clamped), while the");
        text.AppendLine("  meso budget is 45-70 m. Anything but the calmest terrain therefore reads as solid red.");
        text.AppendLine("  Use the macro-only vs macro+meso ranges above to size the actual delta.");
        text.AppendLine("* PhaseB_Curvature.png divides the laplacian by CellSize, not CellSize squared, so it is");
        text.AppendLine("  NOT 1/m curvature and its scale changes with window size. Compare shape, not values.");
        text.AppendLine("* PhaseB_Slope.png / *_slope maps use (1 - normal.y) * 2, which is NOT degrees.");
        text.AppendLine("* phase A at 4096 m and 1024 m windows has cell sizes of 4.00 m and 1.00 m, and");
        text.AppendLine("  CellSizeToDetailStrength returns 0 at 4 m - so PhaseA_4096m carries NO meso detail even");
        text.AppendLine("  though IncludeMeso was true. That is the intended LOD fade, not a missing feature.");
        text.AppendLine("* border pixels use a one-sided gradient (the neighbour is the centre sample), so the");
        text.AppendLine("  outermost row and column read flatter than they are.");

        File.WriteAllText(ProvenancePath, text.ToString());
    }

    /// <summary>
    /// LOD fade for meso detail. Note the discontinuity at cell == 1 m: the first branch returns 1.0 and the
    /// second starts its lerp at 1.0, so it is continuous there, but it hits exactly 0 at cell == 4 m, which
    /// is why the 4096 m phase A window is macro-only. UNCHANGED - it mirrors the runtime fade.
    /// </summary>
    private static float CellSizeToDetailStrength(float cell)
    {
        if (cell <= 1f) return 1f;
        if (cell <= 2f) return math.lerp(1f, 0.5f, (cell - 1f));
        if (cell <= 4f) return math.lerp(0.5f, 0f, (cell - 2f) * 0.5f);
        return 0f;
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct HeightMapJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> Heights;
        public WorldMacroGeologyParams Params;
        public int Width;
        public float CellSize;
        public float StartX;
        public float StartZ;
        public bool IncludeMeso;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float worldX = StartX + x * CellSize;
            float worldZ = StartZ + z * CellSize;

            WorldMacroGeologySample macro = WorldMacroGeologyFields.EvaluateSinglePass(worldX, worldZ, in Params);
            float h = macro.HeightMeters;

            if (IncludeMeso)
            {
                float detailStrength = CellSizeToDetailStrength(CellSize);
                if (detailStrength > 0.001f)
                {
                    WorldTerrainMesoDetailParams mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(Params.Seed);
                    float baseBudget = math.lerp(45f, 70f, detailStrength);
                    mesoParams.MaxMesoDeltaMeters = math.max(1f, baseBudget);

                    WorldTerrainMesoDetailSample mesoSample = WorldTerrainMesoDetailFields.Evaluate(
                        in macro, worldX, worldZ, in mesoParams);

                    h += mesoSample.HeightDeltaMeters * detailStrength;
                }
            }
            Heights[i] = h;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct HillshadeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;
        public float3 SunDir;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float hC = Heights[i];
            float hL = x > 0 ? Heights[i - 1] : hC;
            float hR = x < Width - 1 ? Heights[i + 1] : hC;
            float hD = z > 0 ? Heights[i - Width] : hC;
            float hU = z < Width - 1 ? Heights[i + Width] : hC;

            float3 normal = math.normalize(new float3(hL - hR, 2f * CellSize, hD - hU));
            float ndotl = math.saturate(math.dot(normal, SunDir));
            float c = 0.1f + ndotl * 0.9f;
            byte b = (byte)math.clamp(c * 255f, 0, 255);
            Colors[i] = new Color32(b, b, b, 255);
        }
    }

    /// <summary>
    /// NOT degrees: <c>1 - normal.y</c> scaled by 2 and clamped, so it saturates white at roughly 62 deg and
    /// is non-linear throughout. UNCHANGED - fixing the mapping would silently redefine every previously
    /// captured slope image. GraphOutputRenderer.cs reuses this job for the same reason.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct SlopeMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float hC = Heights[i];
            float hL = x > 0 ? Heights[i - 1] : hC;
            float hR = x < Width - 1 ? Heights[i + 1] : hC;
            float hD = z > 0 ? Heights[i - Width] : hC;
            float hU = z < Width - 1 ? Heights[i + Width] : hC;

            float3 normal = math.normalize(new float3(hL - hR, 2f * CellSize, hD - hU));
            float slope01 = 1f - normal.y; // 0 = flat, 1 = vertical
            byte r = (byte)math.clamp(slope01 * 2f * 255f, 0, 255);
            Colors[i] = new Color32(r, r, r, 255);
        }
    }

    /// <summary>
    /// The laplacian is divided by <c>CellSize</c>, not <c>CellSize * CellSize</c>, so the output is not 1/m
    /// curvature and its scale changes with the window size (compare ZoomProofDumpTask.cs:424, which uses the
    /// squared term). UNCHANGED and recorded in the provenance file rather than silently corrected, because
    /// changing it would redefine what every previously captured curvature image meant.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct CurvatureMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float hC = Heights[i];
            float hL = x > 0 ? Heights[i - 1] : hC;
            float hR = x < Width - 1 ? Heights[i + 1] : hC;
            float hD = z > 0 ? Heights[i - Width] : hC;
            float hU = z < Width - 1 ? Heights[i + Width] : hC;

            float laplacian = (hL + hR + hD + hU - 4f * hC) / CellSize;
            float c = laplacian * 0.5f + 0.5f;
            byte b = (byte)math.clamp(c * 255f, 0, 255);
            Colors[i] = new Color32(b, b, b, 255);
        }
    }

    /// <summary>
    /// Saturates pure red at a 0.1 m difference (<c>diff * 10 * 255</c>, clamped to 255) while the meso
    /// budget is 45-70 m, so on anything but the calmest terrain this map is solid red and says nothing.
    /// UNCHANGED for the same reason as above; the real delta range is written to the provenance file.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct DiffMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights1;
        [ReadOnly] public NativeArray<float> Heights2;
        [WriteOnly] public NativeArray<Color32> Colors;

        public void Execute(int i)
        {
            float diff = math.abs(Heights1[i] - Heights2[i]);
            byte r = (byte)math.clamp(diff * 10f * 255f, 0, 255);
            Colors[i] = new Color32(r, 0, 0, 255);
        }
    }

    /// <summary>
    /// CONSTANT BY CONSTRUCTION. <see cref="CellSizeToDetailStrength"/> is a function of <c>CellSize</c>
    /// alone, and CellSize is uniform across the window, so every pixel of this map is the same byte. The
    /// original body also computed <c>worldX</c>/<c>worldZ</c> per pixel and discarded them, which made the
    /// map look spatial when it is not; those dead locals are gone. <c>Params</c>, <c>Width</c>, <c>StartX</c>
    /// and <c>StartZ</c> stay on the struct so the call site is unchanged, and so that whoever makes this map
    /// actually vary per pixel has the inputs already wired.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct DetailStrengthMapJob : IJobParallelFor
    {
        public WorldMacroGeologyParams Params;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;
        public float StartX;
        public float StartZ;

        public void Execute(int i)
        {
            float ds = CellSizeToDetailStrength(CellSize);
            byte b = (byte)math.clamp(ds * 255f, 0, 255);
            Colors[i] = new Color32(b, b, b, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct MaskMapJob : IJobParallelFor
    {
        public WorldMacroGeologyParams Params;
        [WriteOnly] public NativeArray<Color32> Colors;
        public int Width;
        public float CellSize;
        public float StartX;
        public float StartZ;

        public void Execute(int i)
        {
            int x = i % Width;
            int z = i / Width;
            float worldX = StartX + x * CellSize;
            float worldZ = StartZ + z * CellSize;

            WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ, in Params, out var masks);

            // R = Terrace, G = Sediment (we will use Slump), B = Canyon
            byte r = (byte)math.clamp(masks.Terrace * 255f, 0, 255);
            byte g = (byte)math.clamp(masks.Slump * 255f, 0, 255);
            byte b = (byte)math.clamp(masks.Canyon * 255f, 0, 255);
            Colors[i] = new Color32(r, g, b, 255);
        }
    }
}
