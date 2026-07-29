using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Captures the editor's active Scene View camera to a PNG.
///
/// The type name <c>CaptureSceneViewOnLoad</c>, the <c>[InitializeOnLoad]</c> attribute and the
/// <c>Capture</c> method name are all preserved: batch scripts bind editor tools by reflection name.
/// <c>Capture</c> is now public so <c>-executeMethod CaptureSceneViewOnLoad.Capture</c> can actually reach
/// it - it was private, so no batch caller could have invoked it at all.
///
/// WHAT WAS WRONG - AND WHY NO ARTIFACT OF THIS TOOL HAS EVER EXISTED:
///
/// * the output path was hardcoded to
///   <c>C:\Users\danat\.gemini\antigravity\brain\389e4a53-b1e6-440c-b190-0f5c509fa8c4\SceneViewScreenshot.png</c>.
///   That is a DIFFERENT USER'S profile. This machine's profile is <c>Admin</c>; <c>C:\Users\danat</c> does
///   not exist here and never has (verified 2026-07-29: <c>C:\Users</c> holds only Admin, All Users,
///   Default, Default User, Public). There was no <c>Directory.CreateDirectory</c> anywhere, and
///   <c>File.WriteAllBytes</c> does not create missing parent directories - it throws
///   <c>DirectoryNotFoundException</c>.
///
///   CONSEQUENCE, STATED SO NOBODY GOES LOOKING AGAIN: every fire of this hook threw at the write, inside
///   an <c>EditorApplication.delayCall</c> with no try/catch, so the exception surfaced as an unattributed
///   editor console error and the run produced nothing. There has never been a run of this tool that
///   produced a readable <c>SceneViewScreenshot.png</c> anywhere on this machine. Do not go hunting for a
///   historical Scene View capture from this tool - no such file was ever written. Any past claim that this
///   tool showed what the scene looked like was made without an artifact.
///
/// * no try/catch, no exit code anywhere, on any path. A batch caller had nothing to branch on;
/// * <c>[InitializeOnLoad]</c> fired this on EVERY domain reload - every script compile, every play-mode
///   enter and exit - in whatever interactive editor happened to be open. Each fire allocated a 1920x1080
///   RenderTexture plus a Texture2D, hijacked the live Scene View camera, threw at the write, and leaked the
///   Texture2D permanently. The auto-run is now gated to batchmode plus an explicit flag file, matching the
///   sanctioned pattern at AnomalySmokeBatchAutoRunner.cs:22, so it can neither fire in a human's editor nor
///   hijack an unrelated batch run;
/// * it called <c>AssetDatabase.DeleteAsset</c> ON ITS OWN SOURCE FILE to stop itself re-firing. On a live
///   shared working tree with a concurrent human session and an orchestrator owning git, a tool that deletes
///   tracked source at runtime is not acceptable regardless of intent. The flag file does that job now;
/// * <c>if (File.Exists(outPath)) { }</c> was an empty block whose comment argued with itself. Dead code;
/// * both "no Scene View" and "no camera" fell out of the method silently - no log, no exit code, nothing.
///   In batchmode there IS no Scene View, so the batch path did nothing at all, forever, in total silence;
/// * <c>cam.targetTexture</c> was forced to <c>null</c> afterwards rather than restored to the value the
///   SceneView owned, and on any throw it was left pointing at a destroyed RenderTexture - which breaks the
///   human's Scene View, not just this tool;
/// * the Texture2D was never destroyed on any path, and no GPU-context refusal or artifact verification
///   existed.
///
/// STANDING LIMITATION: this tool needs an interactive editor with an open Scene View. Batchmode has no
/// Scene View, so the flagged batch path can only report that honestly and exit 2. The menu item is the
/// working route.
/// </summary>
[InitializeOnLoad]
public class CaptureSceneViewOnLoad
{
    private const string ToolName = "CaptureSceneView";

    /// <summary>
    /// Per-tool subfolder, inside the repo, so a human can actually find the output. `static readonly` and
    /// not `const` because <see cref="Path.Combine"/> is not a compile-time constant (CS0133). The subfolder
    /// is per-tool on purpose: <c>Logs/</c> root is already littered with loose PNGs from several tools that
    /// wrote colliding filenames into one directory and destroyed each other's evidence.
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "capture_scene_view");

    private static readonly string OutputPath = Path.Combine(OutputDir, "SceneViewScreenshot.png");

    private static readonly string ProvenancePath =
        Path.Combine(OutputDir, "SceneViewScreenshot_provenance.txt");

    /// <summary>
    /// Opt-in for the automatic run. Replaces "delete my own source file so I stop firing": the flag is
    /// consumed on pickup, so a flagged batch session captures exactly once and an unflagged one - which is
    /// every other build, import and test run in this project - is untouched.
    /// </summary>
    private static readonly string AutoRunFlagPath =
        Path.Combine(OutputDir, "capture-scene-view-autorun.flag");

    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;

    /// <summary>Floor for "this is a real PNG and not a truncated stub". Matches ExportArraySlices.cs:36.</summary>
    private const int MinimumPngBytes = 512;

    static CaptureSceneViewOnLoad()
    {
        // Was: unconditional `EditorApplication.delayCall += Capture` on every domain reload.
        if (!Application.isBatchMode || !File.Exists(AutoRunFlagPath))
            return;

        EditorApplication.delayCall += Capture;
    }

    [MenuItem("Hecton8/Diagnostics/Capture Scene View")]
    public static void Capture()
    {
        // PART 4. This tool drives Camera.Render into a RenderTexture, reads it back with ReadPixels and
        // encodes a PNG - the exact call set that C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37
        // names: "compute shaders and Graphics.Blit return zeros with no GPU context". With -nographics the
        // readback is all zeros and the PNG is solid black, which reads as a broken scene rather than as an
        // editor launched with the wrong flags.
        //
        // Unconditional Exit(3) is safe here: graphicsDeviceType is only Null under -nographics, which only
        // happens in batchmode, so this branch cannot fire in a human's interactive editor.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError("[CaptureSceneView] REFUSED: no GPU context, would return zeros. Remove -nographics.");
            EditorApplication.Exit(3);
            return;
        }

        ConsumeAutoRunFlag();

        // Seeded with the failure code, not 0: if any path below ever skips the assignment it must not fall
        // through to reporting success.
        int exitCode = 2;
        try
        {
            exitCode = Run();
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: no Scene View capture was produced at {OutputPath}. {ex}");
            Exit(2);
            return;
        }

        Exit(exitCode);
    }

    /// <summary>
    /// Returns 0 only after the PNG has been written AND verified on disk AND shown to carry more than one
    /// distinct pixel value. Every "could not do the work" branch returns 2 and logs what was not produced
    /// first.
    /// </summary>
    private static int Run()
    {
        Directory.CreateDirectory(OutputDir);

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            // Was: silent return. This is the branch batchmode always took.
            Debug.LogError(
                $"[{ToolName}] FAILED: SceneView.lastActiveSceneView is null, so there is no viewport to " +
                $"capture and no PNG was written to {OutputPath}. This tool needs an interactive editor with " +
                $"an open Scene View; batchmode has none. batchMode={Application.isBatchMode}.");
            return 2;
        }

        Camera cam = sceneView.camera;
        if (cam == null)
        {
            // Was: silent return.
            Debug.LogError(
                $"[{ToolName}] FAILED: the active SceneView has no camera, so no PNG was written to " +
                $"{OutputPath}.");
            return 2;
        }

        // Stale-artifact hygiene (hecton8-shaders-compute.md:43). The existence check at the end can only
        // prove this run wrote something if the previous run's PNG is gone first.
        if (!DeleteStale(OutputPath)) return 2;
        if (!DeleteStale(ProvenancePath)) return 2;

        // The SceneView owns this camera. Save what it had rather than forcing null afterwards, or the
        // viewport is left broken for the human whose editor this is.
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture rt = null;
        Texture2D tex = null;

        try
        {
            rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            tex.Apply();

            // COLD ALLOC: Color32[1920*1080] - editor-only readback probe, 4 B/px = 8 MiB -
            // owner: CaptureSceneViewOnLoad. Not streamed because the whole frame is needed to prove the
            // capture is not one flat colour, which is what a zeroed or unrendered frame produces.
            Color32[] pixels = tex.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: the readback returned no pixels for a " +
                    $"{CaptureWidth}x{CaptureHeight} capture. No PNG was written to {OutputPath}.");
                return 2;
            }

            if (IsUniform(pixels, out Color32 flatColour))
            {
                // Loud probe for the dominant silent failure: a frame that encodes and writes fine while
                // showing nothing. Checked BEFORE the write so a worthless image never lands on disk to be
                // mistaken for evidence.
                Debug.LogError(
                    $"[{ToolName}] FAILED: all {pixels.Length} captured pixels are the single colour " +
                    $"RGBA({flatColour.r},{flatColour.g},{flatColour.b},{flatColour.a}), so the frame shows " +
                    $"no scene at all. That is what a zeroed readback or an unrendered camera produces, not " +
                    $"a capture worth looking at. Nothing was written to {OutputPath}. " +
                    $"graphicsDeviceType={SystemInfo.graphicsDeviceType}.");
                return 2;
            }

            byte[] png = tex.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: EncodeToPNG returned {(png == null ? "null" : "0 bytes")} for the " +
                    $"{CaptureWidth}x{CaptureHeight} capture, so nothing was written to {OutputPath}.");
                return 2;
            }

            File.WriteAllBytes(OutputPath, png);

            // "EncodeToPNG did not throw" is not proof the artifact exists.
            if (!ArtifactIsPlausible(OutputPath, out string detail))
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: {png.Length} bytes were handed to File.WriteAllBytes without " +
                    $"throwing but the artifact is not usable - {OutputPath} {detail}.");
                return 2;
            }

            WriteProvenance(cam, pixels.Length, detail);

            Debug.Log(
                $"[{ToolName}] wrote a verified {CaptureWidth}x{CaptureHeight} Scene View capture to " +
                $"{OutputPath} ({detail}). Provenance: {ProvenancePath}");
            return 0;
        }
        finally
        {
            // All of this leaked on any failure before, and targetTexture was forced to null rather than
            // restored - the SceneView's own render target.
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    /// <summary>
    /// Deletes the auto-run flag so a flagged batch session captures once. Replaces the old
    /// "AssetDatabase.DeleteAsset on my own source file" self-destruct.
    /// </summary>
    private static void ConsumeAutoRunFlag()
    {
        if (!File.Exists(AutoRunFlagPath))
            return;

        try
        {
            File.Delete(AutoRunFlagPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning(
                $"[{ToolName}] could not delete the auto-run flag '{AutoRunFlagPath}', so a later domain " +
                $"reload in this batch session may capture again. {ex.Message}");
        }
    }

    /// <summary>
    /// True when every pixel is byte-identical, which is what a zeroed readback or an unrendered camera
    /// produces. Cheap early-out on the first difference.
    /// </summary>
    private static bool IsUniform(Color32[] pixels, out Color32 flatColour)
    {
        flatColour = pixels[0];
        for (int i = 1; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (p.r != flatColour.r || p.g != flatColour.g || p.b != flatColour.b || p.a != flatColour.a)
                return false;
        }
        return true;
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
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: could not delete the stale artifact '{path}', so this run could not " +
                $"prove a fresh capture was written rather than auditing the previous run's. {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Proves the file exists and is not truncated. It cannot prove the capture shows anything useful; the
    /// uniformity probe covers "shows nothing", and visual acceptance stays with Docs/QUALITY_GATES.md.
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
    /// Records what the artifact actually is next to the artifact, so a reader does not have to trust a chat
    /// message about it.
    /// </summary>
    private static void WriteProvenance(Camera cam, int pixelCount, string outputDetail)
    {
        string[] lines =
        {
            $"{ToolName} - Scene View capture provenance",
            $"generated (UTC): {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
            $"graphicsDeviceType: {SystemInfo.graphicsDeviceType}",
            $"batchMode: {Application.isBatchMode}",
            $"scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}",
            $"camera position: {cam.transform.position}",
            $"camera rotation: {cam.transform.rotation.eulerAngles}",
            $"camera fieldOfView: {cam.fieldOfView}",
            $"resolution: {CaptureWidth}x{CaptureHeight} ({pixelCount} pixels read back)",
            $"output: {OutputPath} ({outputDetail})",
            "",
            "READ THIS BEFORE QUOTING THE IMAGE AS PROOF:",
            "* this is the EDITOR Scene View camera, re-rendered into an offscreen 1920x1080 target. It is",
            "  not the game camera, not the player's aspect ratio, and it carries editor-only gizmos and",
            "  overlays that the shipped game does not have.",
            "* the tool proves the PNG exists, is non-trivially sized, and is not one flat colour. It does",
            "  NOT prove the scene is correct or shippable - that judgement is Docs/QUALITY_GATES.md.",
        };

        File.WriteAllLines(ProvenancePath, lines);
    }

    /// <summary>
    /// Exit codes are only read by a batch caller, and this tool also has a menu item - killing a human's
    /// editor because no Scene View was focused is not acceptable. In batchmode isBatchMode is always true,
    /// so the caller still gets the real code. Matches H8_TerrainGPUVisualTester.cs:76,201,205. The Exit(3)
    /// refusal above is deliberately NOT guarded: it cannot be reached interactively.
    /// </summary>
    private static void Exit(int code)
    {
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }
}
