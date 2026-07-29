using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// MUTATOR THEN CAPTURE. Opens 02_HECTON_WORLD in edit mode, forces the world camera into the
/// configuration this tool exists to test (skybox clear, URP post-processing on, 0.3 - 10000 clip range,
/// 60 degree FOV), waits a settle window while the editor pumps, then captures two 1920x1080 PNGs.
///
/// BOTH HALVES REPORT SEPARATELY, because the tool used to be able to claim a shot it never took and a
/// fix it never applied with the same exit 0:
///   * the fix half says CHANGED, ALREADY CORRECT, or REFUSED. REFUSED means the camera configuration
///     this tool is named after could not be established, so no capture is attempted at all - a shot with
///     post-processing off is not the shot this tool claims to deliver. ALREADY CORRECT still exits 0 when
///     the shots land: finding the camera already right does not invalidate the photograph, unlike a
///     pure mutator such as DisableErosionNodeTask where the write IS the deliverable.
///   * the capture half exits 0 only after BOTH PNGs are proven present on disk above a byte floor.
/// The camera edits are deliberately never saved - this tool does not write the scene - so nothing it does
/// to the shipped world scene survives the run.
///
/// WHAT WAS WRONG:
///
///   * outDir was built at RUNTIME from the user profile plus
///     "/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4" - another agent's private scratch
///     directory outside the repo. Because it was assembled from System.Environment rather than
///     hardcoded, a text search for the literal path did not find it. Nothing created the directory, so
///     File.WriteAllBytes threw on the first shot on any machine where it is absent.
///   * that throw escaped the EditorApplication.update callback AFTER it had already unsubscribed itself
///     and BEFORE it reached EditorApplication.Exit, and nothing caught it. Unity logs the exception and
///     moves on, so no later tick could ever reach an Exit call: the run ended with no artifact, no
///     verdict and no exit code, leaving a batchmode editor to hang until somebody killed it - or, under
///     -quit, to quit with code 0 having produced nothing.
///   * "[FAS] Done." then EditorApplication.Exit(0) was reached unconditionally. Neither PNG was ever
///     checked for existence or size, and EncodeToPNG not throwing is not evidence that a capture
///     happened.
///   * the settle window accumulated Time.unscaledDeltaTime inside an EDIT-mode
///     EditorApplication.update callback. That is an editor frame delta, not wall clock, so "20 seconds"
///     was 20 seconds of whatever rate a windowless batchmode editor happens to tick at - and if it ever
///     reports zero, the threshold is never crossed and the run never ends. Now a wall-clock deadline off
///     EditorApplication.timeSinceStartup, the same fix Stage2PlaymodeTest.cs:66-71 already carries, with
///     an independent tick ceiling so a stalled clock still produces a verdict instead of a hang.
///   * the static `timer` had a field initialiser and was never reset by Execute (unlike `cam` and
///     `outDir`, which were reassigned). A second invocation in one editor session therefore started with
///     timer already past the threshold and captured on the very first tick - before the world had settled
///     at all - while reporting exactly the same success as a clean run.
///   * there was no GPU refusal. Under -nographics Camera.Render and ReadPixels return zeros
///     (C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37), so it wrote two black PNGs and exited 0.
///
/// A NOTE ON THE LAUNCHER. This design is deferred: Execute() returns immediately and the capture happens
/// in later editor ticks. Unity's -quit flag quits as soon as -executeMethod returns, which would end the
/// process with exit 0 before either shot was taken, so this tool refuses to start under -quit rather than
/// producing that silent empty success. No script under Tools/BatchTasks currently invokes it.
///
/// A duplicate global-namespace class of the same name lives at Assets/FixAndShoot.cs, compiled into
/// Assembly-CSharp; it is a different, older tool that still writes to a hardcoded
/// C:/Users/danat/.gemini/... path. This file is the Hecton8.Editor one. Reported, not touched.
/// </summary>
public static class FixAndShoot
{
    private const string ToolName = "FixAndShoot";

    /// <summary>
    /// Per-tool subfolder inside the repo. Was a foreign brain directory shared with other tools writing
    /// identical filenames, where each run destroyed the previous run's evidence. <c>static readonly</c>
    /// rather than <c>const</c> because <see cref="Path.Combine"/> is not a compile-time constant
    /// (CS0133).
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "fix_and_shoot");

    private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

    private const float TargetFarClip = 10000f;
    private const float TargetNearClip = 0.3f;
    private const float TargetFieldOfView = 60f;

    /// <summary>Wall-clock settle window before the first shot. Was 20 accumulated editor frame deltas.</summary>
    private const double SettleSeconds = 20d;

    /// <summary>
    /// After this many editor ticks the clock is checked for movement. The failure being repaired here was
    /// a time source that does not advance, and a deadline built on the same clock cannot catch that - so
    /// this probe is measured in ticks, which advance independently. A healthy editor advances
    /// timeSinceStartup within a handful of ticks, so it cannot fire on a working run; it exists to turn a
    /// silent forever-hang into exit 4 with a diagnosis.
    /// </summary>
    private const int ClockProbeTicks = 240;

    /// <summary>
    /// Backstop ceiling for the case the clock advances but absurdly slowly, so <see cref="ClockProbeTicks"/>
    /// passes and the settle window still never elapses. At any plausible batchmode tick rate a 20 s
    /// window needs far fewer ticks than this.
    /// </summary>
    private const int MaxTicks = 500000;

    private const int ShotWidth = 1920;
    private const int ShotHeight = 1080;

    /// <summary>A 1920x1080 PNG cannot be this small. Below it, nothing usable was encoded.</summary>
    private const int MinimumPngBytes = 1024;

    /// <summary>Proved it applied the camera fix and wrote both verified shots.</summary>
    private const int ExitShotsTaken = 0;

    /// <summary>Could not do the work, or crashed trying. Nothing is claimed about the world.</summary>
    private const int ExitFailed = 2;

    /// <summary>Refused: no GPU context, so the captures would be fabricated zeros.</summary>
    private const int ExitNoGpu = 3;

    /// <summary>The run never reached a verdict within its bounds.</summary>
    private const int ExitTimeout = 4;

    private enum FixOutcome { Changed, AlreadyCorrect, Refused }

    private static Camera cam;
    private static double subscribedAtStartupTime;
    private static double captureAtStartupTime;
    private static int ticks;
    private static FixOutcome fixOutcome;
    private static string fixDetail;

    /// <summary>
    /// Batch entry point. Called by reflection name - do not rename.
    /// </summary>
    public static void Execute()
    {
        // PART 4. Camera.Render, ReadPixels and EncodeToPNG. With no graphics device every pixel this tool
        // reads back is a zero, so it would write two black 1920x1080 PNGs, verify their byte length
        // happily, and exit 0 - and the black frames would then be read as a finding about the world's
        // lighting rather than as an editor launched with the wrong flags. Rule:
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37, "compute shaders and Graphics.Blit
        // return zeros with no GPU context". Fully qualified on purpose so this guard needs no using.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context, would return zeros. Remove -nographics. " +
                "Camera.Render and ReadPixels produce black frames here, and no camera fix could be " +
                "judged from them.");
            EditorApplication.Exit(ExitNoGpu);
            return;
        }

        try
        {
            // The capture is deferred to later editor ticks, so -quit would end the process with a
            // success code before either shot existed. System.Environment fully qualified: a bare
            // Environment inside this project binds to Hecton8.Environment (CS0234).
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-quit", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError(
                        $"[{ToolName}] REFUSED: launched with -quit. This tool captures on later editor " +
                        "ticks, and -quit ends the process as soon as -executeMethod returns - the editor " +
                        "would exit 0 with no camera fix applied and no screenshot taken. Relaunch without " +
                        "-quit; this tool calls EditorApplication.Exit itself on every path.");
                    EditorApplication.Exit(ExitFailed);
                    return;
                }
            }

            Directory.CreateDirectory(OutputDir);

            // Statics are reset per run. Without this a second invocation in one editor session inherits
            // the first run's clock and shoots on tick one, before the world has settled, while reporting
            // the same success as a clean run.
            cam = null;
            ticks = 0;
            fixOutcome = FixOutcome.Refused;
            fixDetail = "not attempted";

            // Removing a delegate that is not subscribed is a no-op, so this is a safe double-subscribe
            // guard: two live subscriptions would capture twice and queue two Exit calls.
            EditorApplication.update -= OnUpdate;

            Debug.Log($"[{ToolName}] Execute started. Artifacts -> {OutputDir}");

            // Single mode: unsaved edits in any currently open scene are discarded without a prompt.
            // Preserved as-is; a batchmode editor holds the only lock on this project.
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Fail($"scene '{ScenePath}' did not open (IsValid={scene.IsValid()}, " +
                     $"isLoaded={scene.isLoaded}). No camera was fixed and no shot was taken.");
                return;
            }

            if (!ApplyCameraFix())
            {
                // ApplyCameraFix already logged the specific refusal.
                Debug.LogError(
                    $"[{ToolName}] REFUSED (fix half): {fixDetail}. No screenshot was attempted, because a " +
                    "capture taken without this camera configuration is not the evidence this tool claims " +
                    "to produce.");
                EditorApplication.Exit(ExitFailed);
                return;
            }

            Debug.Log(
                $"[{ToolName}] fix half: {(fixOutcome == FixOutcome.Changed ? "CHANGED" : "ALREADY CORRECT")} " +
                $"- {fixDetail}. Camera edits are NOT saved to '{ScenePath}'.");

            subscribedAtStartupTime = EditorApplication.timeSinceStartup;
            captureAtStartupTime = subscribedAtStartupTime + SettleSeconds;
            EditorApplication.update += OnUpdate;
            Debug.Log(
                $"[{ToolName}] waiting {SettleSeconds:F0}s of wall clock for the world to settle before " +
                "the first shot.");
        }
        catch (System.Exception ex)
        {
            // Reached before anything was subscribed, so this is the only exit for this run.
            EditorApplication.update -= OnUpdate;
            Debug.LogError(
                $"[{ToolName}] FAILED during setup: no camera fix was verified and no screenshot was " +
                $"produced under {OutputDir}. {ex}");
            EditorApplication.Exit(ExitFailed);
        }
    }

    /// <summary>
    /// Establishes the camera configuration this tool is named after and records whether it had to change
    /// anything. Returns false when the configuration could not be established at all.
    /// </summary>
    private static bool ApplyCameraFix()
    {
        bool createdCamera = false;

        cam = GameObject.Find("Main Camera")?.GetComponent<Camera>();
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            cam = new GameObject("VerifyCam").AddComponent<Camera>();
            createdCamera = cam != null;
        }

        if (cam == null)
        {
            fixOutcome = FixOutcome.Refused;
            fixDetail =
                $"no camera in '{ScenePath}' and AddComponent<Camera> on a new VerifyCam returned null, so " +
                "there is nothing to configure and nothing to render from";
            return false;
        }

        var uacam = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (uacam == null)
        {
            uacam = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        if (uacam == null)
        {
            // Was assumed to succeed. Without this component renderPostProcessing cannot be set at all,
            // and the shot would silently be a no-post-processing frame presented as the fixed one.
            fixOutcome = FixOutcome.Refused;
            fixDetail =
                $"could not get or add UniversalAdditionalCameraData on camera '{cam.name}', so URP " +
                "post-processing cannot be enabled and the capture would not be the configuration this " +
                "tool claims to shoot";
            return false;
        }

        bool changedClearFlags = cam.clearFlags != CameraClearFlags.Skybox;
        bool changedPostFx = !uacam.renderPostProcessing;
        bool changedFarClip = !Mathf.Approximately(cam.farClipPlane, TargetFarClip);
        bool changedNearClip = !Mathf.Approximately(cam.nearClipPlane, TargetNearClip);
        bool changedFov = !Mathf.Approximately(cam.fieldOfView, TargetFieldOfView);

        cam.clearFlags = CameraClearFlags.Skybox;
        uacam.renderPostProcessing = true;
        cam.farClipPlane = TargetFarClip;
        cam.nearClipPlane = TargetNearClip;
        cam.fieldOfView = TargetFieldOfView;

        bool anyChange = createdCamera || changedClearFlags || changedPostFx || changedFarClip
                         || changedNearClip || changedFov;

        fixOutcome = anyChange ? FixOutcome.Changed : FixOutcome.AlreadyCorrect;
        fixDetail =
            $"camera '{cam.name}'" + (createdCamera ? " (CREATED, none existed in the scene)" : "") +
            $": clearFlags->Skybox changed={changedClearFlags}, renderPostProcessing->true " +
            $"changed={changedPostFx}, farClipPlane->{TargetFarClip:F0} changed={changedFarClip}, " +
            $"nearClipPlane->{TargetNearClip} changed={changedNearClip}, " +
            $"fieldOfView->{TargetFieldOfView:F0} changed={changedFov}";
        return true;
    }

    private static void OnUpdate()
    {
        // Ticks advance before any early-out, so no wait can be unbounded. Stage2PlaymodeTest.cs:190-194
        // documents the same ordering: an increment placed below an early-out cannot bound the wait that
        // early-out performs.
        ticks++;

        double now = EditorApplication.timeSinceStartup;

        if (ticks >= ClockProbeTicks && now <= subscribedAtStartupTime)
        {
            EditorApplication.update -= OnUpdate;
            Debug.LogError(
                $"[{ToolName}] TIMEOUT: EditorApplication.timeSinceStartup has not advanced past " +
                $"{subscribedAtStartupTime:F3} in {ticks} editor ticks, so the {SettleSeconds:F0}s settle " +
                "window can never elapse and neither screenshot will ever be taken. Nothing was written " +
                $"under {OutputDir}. The camera fix ({fixOutcome}) was applied in memory only and was " +
                "never saved.");
            EditorApplication.Exit(ExitTimeout);
            return;
        }

        if (ticks > MaxTicks)
        {
            EditorApplication.update -= OnUpdate;
            Debug.LogError(
                $"[{ToolName}] TIMEOUT after {ticks} editor ticks: the {SettleSeconds:F0}s settle window " +
                $"still has not elapsed (timeSinceStartup {now:F3}, needed {captureAtStartupTime:F3}), so " +
                $"neither screenshot was taken and nothing was written under {OutputDir}. The camera fix " +
                $"({fixOutcome}) was applied in memory only and was never saved.");
            EditorApplication.Exit(ExitTimeout);
            return;
        }

        if (now < captureAtStartupTime) return;

        // Unsubscribed BEFORE anything that can throw. The old code unsubscribed first too, but then let
        // exceptions out of the capture; this ordering plus the guard below means exactly one verdict.
        EditorApplication.update -= OnUpdate;

        int exitCode;
        try
        {
            exitCode = CaptureBothShots();
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED during capture after {ticks} ticks: the two screenshots under " +
                $"{OutputDir} are missing or incomplete, so there is no visual evidence for the camera fix " +
                $"({fixOutcome}). {ex}");
            exitCode = ExitFailed;
        }

        EditorApplication.Exit(exitCode);
    }

    private static int CaptureBothShots()
    {
        // The camera can be destroyed between Execute and here by a scene reload or another tool. A null
        // dereference inside an update callback used to mean an endless exception loop.
        if (cam == null)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: the camera was destroyed before the settle window elapsed, so " +
                $"neither shot was taken and no artifact exists under {OutputDir}.");
            return ExitFailed;
        }

        Debug.Log($"[{ToolName}] settle window elapsed after {ticks} editor ticks, capturing.");

        string reason;

        cam.transform.position = new Vector3(0, 150f, -300f);
        cam.transform.LookAt(new Vector3(0, 50f, 0));
        string shot0 = Path.Combine(OutputDir, "Fixed_0.png");
        if (!CaptureFromCamera(cam, shot0, out reason))
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: the first shot (wide, from 0/150/-300) produced no usable " +
                $"artifact - {reason}. The camera fix ({fixOutcome}) has no visual evidence.");
            return ExitFailed;
        }

        cam.transform.position = new Vector3(0, 30f, 0);
        cam.transform.rotation = Quaternion.Euler(0, 45, 0);
        string shot1 = Path.Combine(OutputDir, "Fixed_1.png");
        if (!CaptureFromCamera(cam, shot1, out reason))
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: the second shot (ground level, from 0/30/0) produced no usable " +
                $"artifact - {reason}. Only '{shot0}' exists, so this run is incomplete.");
            return ExitFailed;
        }

        Debug.Log(
            $"[{ToolName}] DONE. Fix half: {(fixOutcome == FixOutcome.Changed ? "CHANGED" : "ALREADY CORRECT")} " +
            $"({fixDetail}), not saved to '{ScenePath}'. Capture half: two verified PNGs under {OutputDir}. " +
            "Both are raw captures, which can only reject, never accept " +
            "(hecton8-shaders-compute.md:24-26) - open them before judging the world.");
        return ExitShotsTaken;
    }

    /// <summary>
    /// Renders one shot and returns false with a reason unless the PNG is on disk and non-trivially
    /// sized. "EncodeToPNG did not throw" is not proof that a capture happened.
    /// </summary>
    private static bool CaptureFromCamera(Camera camera, string path, out string reason)
    {
        var rt = new RenderTexture(ShotWidth, ShotHeight, 24);
        var tex = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
        byte[] bytes;

        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
            tex.Apply();
            bytes = tex.EncodeToPNG();
        }
        finally
        {
            // Restored even on a throw. The old code only reset these on the success path, so a failed
            // capture left the camera rendering into a destroyed RenderTexture for the next attempt.
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        if (bytes == null || bytes.Length == 0)
        {
            reason = "EncodeToPNG returned no bytes";
            return false;
        }

        File.WriteAllBytes(path, bytes);

        if (!File.Exists(path))
        {
            reason = $"'{path}' does not exist on disk after File.WriteAllBytes";
            return false;
        }

        long length = new FileInfo(path).Length;
        if (length < MinimumPngBytes)
        {
            reason = $"'{path}' is {length} bytes, below the {MinimumPngBytes}-byte floor for a real " +
                     $"{ShotWidth}x{ShotHeight} PNG";
            return false;
        }

        reason = null;
        Debug.Log($"[{ToolName}] Screenshot verified on disk: {path} ({length} bytes).");
        return true;
    }

    /// <summary>
    /// Logs a failure naming what was NOT produced and exits non-zero. Used only on setup paths, where
    /// nothing is subscribed yet.
    /// </summary>
    private static void Fail(string what)
    {
        Debug.LogError($"[{ToolName}] FAILED: {what}");
        EditorApplication.Exit(ExitFailed);
    }
}
