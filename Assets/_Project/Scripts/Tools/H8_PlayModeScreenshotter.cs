#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Tools
{
    /// <summary>
    /// Single owner of the on-disk layout every HECTON-8 capture route writes into.
    ///
    /// It lives in Hecton8.Core rather than the editor assembly because both sides of the capture
    /// route need it and only this direction of reference exists: Hecton8.Editor references
    /// Hecton8.Core (Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef:5), never the reverse.
    /// The play-mode component below and Hecton8.EditorTools.H8_RouteCaptureStation therefore share
    /// one convention instead of two drifting copies.
    ///
    /// Every run gets its OWN directory, and the directory is proven unused before it is created.
    /// That is deliberate and it is the reason nothing here deletes anything. AGENTS.md:481
    /// (Atomic File Delete Rule) orders every .png and .log in the output directory removed before
    /// a render run so that nobody grades a stale screenshot. With several agents capturing into one
    /// shared folder that rule destroys another agent's fresh evidence instead of stale evidence.
    /// A fresh, provably empty directory satisfies the intent of the rule - no stale artifact can be
    /// mistaken for this run's - without the concurrent-delete hazard.
    /// </summary>
    public static class H8CaptureRunDirectory
    {
        /// <summary>Project-root-relative capture root. Never an absolute developer path: AGENTS.md:126.</summary>
        public const string CaptureRootFolder = "Logs";

        public const string CaptureRootSubFolder = "RouteCaptures";

        /// <summary>
        /// Upper bound on the collision probe. Reaching it means something is wrong with the
        /// filesystem or the label, and the caller must fail loudly rather than reuse a directory.
        /// </summary>
        public const int MaxRunDirectoryAttempts = 4096;

        private const string FallbackLabel = "route";

        // Allocated once at type initialisation so the probe below never creates a delegate per call.
        private static readonly Func<string, bool> DirectoryExistsProbe = Directory.Exists;

        /// <summary>
        /// Project root resolved from Application.dataPath, per AGENTS.md:126. This is the only
        /// path anchor any capture route may use.
        /// </summary>
        public static string ResolveProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        public static string ResolveCaptureRoot()
        {
            return Path.Combine(ResolveProjectRoot(), CaptureRootFolder, CaptureRootSubFolder);
        }

        public static string UtcStamp()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reduces a caller-supplied label to characters that are legal in a directory name on every
        /// target filesystem. Pure: no Unity types, no IO. Unknown input never produces an empty
        /// name, because an empty name would collapse every run back into one shared directory.
        /// </summary>
        public static string SanitizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return FallbackLabel;

            char[] buffer = new char[label.Length];
            int written = 0;
            for (int i = 0; i < label.Length; i++)
            {
                char c = label[i];
                bool keep = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                            || c == '-' || c == '_';
                buffer[written++] = keep ? c : '_';
            }

            string sanitized = new string(buffer, 0, written).Trim('_');
            return sanitized.Length == 0 ? FallbackLabel : sanitized;
        }

        /// <summary>
        /// Picks a run directory that does not exist yet. Pure apart from the injected existence
        /// predicate, so it is executable and testable outside Unity.
        ///
        /// Returns false when no free name was found inside maxAttempts. The caller must then abort
        /// the capture: silently reusing an existing directory is exactly the overwrite this method
        /// exists to prevent.
        /// </summary>
        public static bool TryResolveUniqueRunDirectory(
            string captureRoot,
            string label,
            string utcStamp,
            Func<string, bool> directoryExists,
            int maxAttempts,
            out string runDirectory)
        {
            runDirectory = null;

            if (string.IsNullOrEmpty(captureRoot) || directoryExists == null || maxAttempts < 1)
                return false;

            string baseName = SanitizeLabel(label) + "_" + SanitizeLabel(utcStamp);
            string candidate = Path.Combine(captureRoot, baseName);
            if (!directoryExists(candidate))
            {
                runDirectory = candidate;
                return true;
            }

            for (int attempt = 2; attempt <= maxAttempts; attempt++)
            {
                candidate = Path.Combine(
                    captureRoot,
                    baseName + "_" + attempt.ToString(CultureInfo.InvariantCulture));

                if (directoryExists(candidate))
                    continue;

                runDirectory = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves and physically creates this run's directory. Creates only; never deletes.
        /// </summary>
        public static bool TryCreateRunDirectory(string label, out string runDirectory)
        {
            string captureRoot = ResolveCaptureRoot();
            Directory.CreateDirectory(captureRoot);

            if (!TryResolveUniqueRunDirectory(
                    captureRoot,
                    label,
                    UtcStamp(),
                    DirectoryExistsProbe,
                    MaxRunDirectoryAttempts,
                    out runDirectory))
            {
                return false;
            }

            Directory.CreateDirectory(runDirectory);
            return true;
        }
    }

    public class H8_PlayModeScreenshotter : MonoBehaviour
    {
        /// <summary>Wall-clock budget for the player to appear. Real seconds, not frames.</summary>
        public const float PlayerWaitSeconds = 180f;

        /// <summary>
        /// Wall-clock settle budget after the player is found (or the wait expires).
        ///
        /// This used to be `_waitFrames > 600` with the comment "(10s)". That conversion assumed 60
        /// game frames per second. It does not hold in this editor: the measurement recorded in
        /// Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:61-65 is "3300
        /// EditorApplication.update callbacks advanced the game by 19 frames in 13.7s wall - about
        /// one game frame per second, with or without -nographics". At that rate 600 frames is about
        /// ten MINUTES, so any external timeout shorter than that killed the run before a single
        /// pixel was written, and the log showed no capture and no error. Wall time is the only
        /// honest unit here.
        /// </summary>
        public const float SettleSeconds = 20f;

        /// <summary>
        /// Name of an external editor harness that owns this play-mode session, or null when this
        /// screenshotter owns it.
        ///
        /// Both this tool and H8_HeadlessPlayModeProbe are live in the same batchmode run, and they
        /// have incompatible budgets: this one captures at roughly PlayerWaitSeconds + SettleSeconds
        /// (~200s of wall time), while the probe's route needs its 300s settle window plus the
        /// gameplay window on top - over 370s. So this tool won the race every single time, and
        /// CaptureAndExit ends with EditorApplication.Exit(0) at the bottom of this file: it
        /// terminated the whole editor process, with a SUCCESS code, while the probe was still
        /// running. In the run that produced Logs/omega_route19.log the last probe line is
        /// "WORLDDRIVER begin ... budget 63s" and the next event is this tool's capture - the probe
        /// never reached a single verdict row, and the launcher read exit code 0 as a pass.
        ///
        /// When an owner is named the capture still happens, because a real play-mode frame is real
        /// evidence and cheap to take. Only the session teardown is withheld.
        /// </summary>
        public static string ExternalSessionOwner;

        private static bool SessionTeardownIsOurs => string.IsNullOrEmpty(ExternalSessionOwner);

        private float _playerWaitStartedAt = -1f;
        private float _settleStartedAt = -1f;
        private float _nextPlayerSearchAt = -1f;
        private GameObject _cachedPlayer;

        void Update()
        {
            // L19 hop2: batchmode/headless must not SubmitRenderRequest. Under hop2 LIVE the capture
            // path reached URP Cull → TerrainManager::CullAllTerrains → SplatMaterials::Update →
            // Material::SetFloat → native heap realloc crash (Crash!!!). Interactive editor capture
            // is unchanged. Headless probes already own session teardown via ExternalSessionOwner.
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            // realtimeSinceStartup, not an accumulation of unscaledDeltaTime: unscaledDeltaTime is
            // clamped by Time.maximumDeltaTime, so on a one-frame-per-second boot the accumulator
            // reports roughly a third of the real elapsed time and every budget here silently
            // stretches by that factor.
            float now = Time.realtimeSinceStartup;
            if (_playerWaitStartedAt < 0f)
            {
                _playerWaitStartedAt = now;
                _nextPlayerSearchAt = now;
            }

            if (_cachedPlayer == null && now >= _nextPlayerSearchAt)
            {
                // Cold, once-a-second diagnostic lookup in an editor-only capture harness, and it
                // must see DontDestroyOnLoad objects, which scene-root traversal cannot -
                // H8_HeadlessPlayModeProbe.cs:768-771 records that blind spot. Not a hot path.
                GameObject found = GameObject.FindWithTag("Player");
                if (found == null)
                {
                    var movement = UnityEngine.Object.FindAnyObjectByType<Hecton8.Gameplay.HectonPlayerMovement>();
                    if (movement != null)
                        found = movement.gameObject;
                }

                if (found != null)
                    _cachedPlayer = found;

                _nextPlayerSearchAt = now + 1f;
            }

            if (_cachedPlayer == null && now - _playerWaitStartedAt < PlayerWaitSeconds)
                return;

            if (_settleStartedAt < 0f)
            {
                _settleStartedAt = now;
                Debug.Log(
                    $"[H8PlayModeScreenshotter] Settling {SettleSeconds:F0}s before capture. " +
                    $"playerFound={_cachedPlayer != null} waitedSeconds={now - _playerWaitStartedAt:F1}");
                return;
            }

            if (now - _settleStartedAt < SettleSeconds)
                return;

            enabled = false;
            CaptureAndExit(_cachedPlayer);
        }

        /// <summary>
        /// Ends the editor session, unless an external harness declared ownership via
        /// <see cref="ExternalSessionOwner"/>. Suppression is logged loudly with the exit code that
        /// was withheld, so a suppressed teardown can never be mistaken for one that never happened.
        /// </summary>
        private static void EndSessionUnlessExternallyOwned(int exitCode, string reason)
        {
            if (!SessionTeardownIsOurs)
            {
                Debug.Log(
                    "[H8PlayModeScreenshotter] TEARDOWN SUPPRESSED - '" + ExternalSessionOwner +
                    "' owns this play-mode session. Withheld EditorApplication.Exit(" +
                    exitCode.ToString(CultureInfo.InvariantCulture) + ") raised because: " + reason);
                return;
            }

            UnityEditor.EditorApplication.isPlaying = false;
            UnityEditor.EditorApplication.Exit(exitCode);
        }

        private void CaptureAndExit(GameObject player)
        {
            Debug.Log($"[H8PlayModeScreenshotter] Capture started. Player spawned: {player != null}");

            // Defense-in-depth for hop2 batchmode: never force a camera render request that walks
            // Terrain splat materials under headless/batchmode (native Crash!!! in SplatMaterials).
            if (Application.isBatchMode)
            {
                Debug.Log(
                    "[H8PlayModeScreenshotter] BATCHMODE skip capture (no SubmitRenderRequest / " +
                    "terrain cull). Session teardown left to ExternalSessionOwner when set.");
                EndSessionUnlessExternallyOwned(0, "batchmode capture soft-disabled");
                return;
            }

            // -nographics has no graphics device, so every render below returns nothing and the PNG
            // would be a plausible-looking blank. Say so and fail instead of writing a file that
            // reads like evidence. AGENTS.md:128 already bans -nographics for MapMagic/compute
            // generation for the same reason.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[H8PlayModeScreenshotter] NO GRAPHICS DEVICE (graphicsDeviceType=Null). This run " +
                    "was launched with -nographics; ScreenCapture and camera rendering produce no " +
                    "pixels. No PNG written. Re-run batchmode WITHOUT -nographics.");
                EndSessionUnlessExternallyOwned(3, "no graphics device, capture is impossible");
                return;
            }

            Camera targetCam = null;
            if (player != null)
            {
                targetCam = player.GetComponentInChildren<Camera>();
            }

            if (targetCam == null)
            {
                // Camera.main is banned (AGENTS.md:336) and it is also wrong here: it resolves only
                // a camera tagged MainCamera, so an untagged player rig silently fell through to the
                // origin fallback below and captured empty water.
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                float bestDepth = float.NegativeInfinity;
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera candidate = cameras[i];
                    if (candidate == null || !candidate.isActiveAndEnabled || candidate.targetTexture != null)
                        continue;

                    if (candidate.depth <= bestDepth)
                        continue;

                    bestDepth = candidate.depth;
                    targetCam = candidate;
                }
            }

            if (targetCam == null)
            {
                var camGO = new GameObject("Fallback Camera");
                targetCam = camGO.AddComponent<Camera>();
                targetCam.transform.position = new Vector3(0, 10, 0);
            }

            // Capture
            int W = 1920, H_RES = 1080;
            var rt = new RenderTexture(W, H_RES, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();

            var urpPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpPipeline != null)
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest();
                request.destination = RTHandles.Alloc(rt);
                if (RenderPipeline.SupportsRenderRequest(targetCam, request))
                {
                    RenderPipeline.SubmitRenderRequest(targetCam, request);
                    // DO NOT Release() the destination. SingleCameraRequest.destination is declared
                    // `public RenderTexture destination` (URP
                    // Runtime/UniversalRenderPipeline.cs:2706) and RTHandles.Alloc(rt) converts
                    // implicitly to it, so this was RenderTexture.Release() on the surface
                    // ReadPixels reads below - Unity recreates a released RT with undefined
                    // contents, producing a blank frame whatever the camera saw. `rt` is released
                    // by this method after ReadPixels, so this was a double release too.
                }
                else
                {
                    targetCam.targetTexture = rt;
                    targetCam.Render();
                    targetCam.targetTexture = null;
                }
            }
            else
            {
                targetCam.targetTexture = rt;
                targetCam.Render();
                targetCam.targetTexture = null;
            }

            var tex = new Texture2D(W, H_RES, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H_RES), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            rt.Release();
            DestroyImmediate(rt);

            // One fixed filename in one shared folder was the old behaviour: Logs/Screenshots/
            // shot_02_PLAYMODE_ACTUAL.png. A second run, or a concurrent lane, overwrote the first
            // run's evidence with no trace, and AGENTS.md:481 additionally orders every .png in that
            // folder deleted before a run. A per-run directory removes both hazards.
            if (!H8CaptureRunDirectory.TryCreateRunDirectory("playmode", out string runDir))
            {
                Debug.LogError(
                    "[H8PlayModeScreenshotter] Could not reserve an unused capture directory under " +
                    H8CaptureRunDirectory.ResolveCaptureRoot() +
                    ". Refusing to overwrite an existing capture. No PNG written.");
                DestroyImmediate(tex);
                EndSessionUnlessExternallyOwned(5, "no unused capture directory could be reserved");
                return;
            }

            string outPath = Path.Combine(runDir, "playmode_frame.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            // camera.md:57 requires every capture to state what it is. This one is a live play-mode
            // frame off a scene camera, not a staged editor render and not gameplay footage.
            File.WriteAllText(
                Path.Combine(runDir, "capture_truth.txt"),
                "captureTruth=playmode_scene_camera\n" +
                "route=Hecton8.Tools.H8_PlayModeScreenshotter\n" +
                "camera=" + targetCam.name + "\n" +
                "playerFound=" + (player != null ? "true" : "false") + "\n" +
                "resolution=" + W.ToString(CultureInfo.InvariantCulture) + "x" +
                H_RES.ToString(CultureInfo.InvariantCulture) + "\n" +
                "graphicsDevice=" + SystemInfo.graphicsDeviceType + "\n" +
                "acceptance=NONE - QUALITY_GATES.md:176: a raw diagnostic capture can reject visual " +
                "quality, never accept it.\n");

            Debug.Log($"[H8PlayModeScreenshotter] Saved -> {outPath}");

            EndSessionUnlessExternallyOwned(0, "capture completed successfully");
        }
    }
}
#endif
