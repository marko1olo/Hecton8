#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// The eight things a HECTON-8 capture is allowed to be about.
    ///
    /// Closed list, taken verbatim from `TASTE.md:403-412` ("A good screenshot contains at least
    /// one:") and restated at `presentation.md:99-108`. It is a declaration by the caller, not a
    /// detection: nothing in this file can look at a PNG and decide whether it shows a danger cue.
    /// What the station DOES enforce is that the declaration exists, that every token in it is on
    /// this list, and that the frame it is attached to is not blank - a caller cannot claim
    /// "machinery cue" over a flat clear-colour image.
    /// </summary>
    [Flags]
    public enum H8ShotCue
    {
        None = 0,
        PlayerVerb = 1 << 0,
        Pressure = 1 << 1,
        Machinery = 1 << 2,
        Route = 1 << 3,
        Scale = 1 << 4,
        Danger = 1 << 5,
        Evidence = 1 << 6,
        InstrumentCorruption = 1 << 7,
    }

    /// <summary>
    /// What a capture attempt produced.
    ///
    /// There is deliberately no `Accepted` member. `Docs/QUALITY_GATES.md:176`: "Raw diagnostic MCP
    /// screenshots, static reports, and near-identical capture galleries can reject bad visuals
    /// only. They cannot accept visual quality." `EvidenceEligible` therefore means exactly one
    /// thing - the PNG is real pixels of real content with a declared shot list, so it is fit to be
    /// carried into the Visual Reference Parity Gate. The gate decides; this enum never does.
    /// </summary>
    public enum H8CaptureVerdict
    {
        EvidenceEligible = 0,
        RejectedNoGraphicsDevice = 1,
        RejectedNoCamera = 2,
        RejectedNoDeclaredCue = 3,
        RejectedBlankFrame = 4,
        RejectedNothingInFrame = 5,

        /// <summary>
        /// No unused output directory could be reserved, so nothing was written. Distinct from every
        /// reject above on purpose: reporting a filesystem refusal as "nothing in frame" would send
        /// the next agent to look at the scene instead of at the disk.
        /// </summary>
        RefusedNoOutputDirectory = 6,
    }

    /// <summary>
    /// Non-mutating capture route for HECTON-8.
    ///
    /// WHY THIS EXISTS. Every other capture path in the project is dead or unusable:
    /// `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:83-97` funnels all 23 of its
    /// entry points into a rejection stub because it was quarantined for mutating production
    /// assets; `ScreenshotGrabber.cs` had both its `[InitializeOnLoad]` and its
    /// `EditorApplication.update` hook commented out; `HectonScreenshotTaker.cs` wrote to a
    /// hardcoded personal path banned by `AGENTS.md:126`. With no route, nobody can produce the
    /// artifacts the Visual Reference Parity Gate (`Docs/QUALITY_GATES.md:163-179`) consumes, so the
    /// gate can be neither passed nor failed.
    ///
    /// WHAT NON-MUTATING MEANS HERE, concretely. `AGENTS.md:124` forbids automated scripts calling
    /// `EditorSceneManager.SaveScene`, `PrefabUtility.SaveAsPrefabAsset`, or
    /// `EditorUtility.SetDirty` on production assets, and requires runtime adjustment to be
    /// in-memory only. This file calls none of the three, writes no asset, and imports nothing.
    /// The one temporary object it can create - a survey camera when the loaded scenes have none -
    /// carries `HideFlags.HideAndDontSave` and is destroyed in a `finally`. Camera state it borrows
    /// (`targetTexture`) is restored in the same `finally`. Scene dirty flags are sampled before and
    /// after and any change is reported in the manifest rather than hidden.
    ///
    /// It will NOT open a scene over unsaved work. `-h8CaptureScene` is honoured only when every
    /// loaded scene is clean; otherwise the station refuses and says which scene blocked it. That is
    /// the same interest `AGENTS.md:124` protects - not wiping a level designer's, or another
    /// agent's, uncommitted state.
    ///
    /// OUTPUT. `Logs/RouteCaptures/&lt;label&gt;_&lt;utc&gt;[_n]/`, resolved from
    /// `Application.dataPath` per `AGENTS.md:126`, one fresh directory per run, reserved through
    /// `Hecton8.Tools.H8CaptureRunDirectory`. Nothing is ever deleted: `AGENTS.md:481`'s
    /// pre-run wipe of `.png`/`.log` in the output directory protects against grading a stale
    /// screenshot, but with concurrent lanes sharing one folder it destroys fresh evidence instead.
    /// A provably unused directory gets the same guarantee without the hazard.
    ///
    /// -nographics. Under `-nographics` there is no graphics device, `SystemInfo.graphicsDeviceType`
    /// is `GraphicsDeviceType.Null`, and no camera render produces pixels - the same reason
    /// `AGENTS.md:128` bans `-nographics` for MapMagic and compute work. The station detects that
    /// case up front, writes a manifest saying so, writes NO PNG, and exits 3. It does not emit a
    /// blank image that would read like a failed art pass.
    ///
    /// USAGE - batchmode, WITH graphics, no `-quit` (the station exits on its own):
    ///   "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode ^
    ///     -projectPath C:\hades\Hecton8 -logFile Logs/routecapture.log ^
    ///     -executeMethod Hecton8.EditorTools.H8_RouteCaptureStation.Run ^
    ///     -h8CaptureScene Assets/_Project/Scenes/02_HECTON_WORLD.unity ^
    ///     -h8CaptureLabel first_exit -h8CaptureCues route,pressure,scale
    ///
    /// Pure-logic self test, no scene and no graphics device required:
    ///   ... -executeMethod Hecton8.EditorTools.H8_RouteCaptureStation.SelfTest
    /// </summary>
    public static class H8_RouteCaptureStation
    {
        private const string Marker = "[H8_CAPTURE]";

        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        /// <summary>Every 4th pixel on each axis. 1/16th of the data, same verdict, no 6 MB copy.</summary>
        private const int StatisticsSampleStride = 4;

        private const int LumaBucketCount = 64;

        /// <summary>
        /// A frame is called blank only when BOTH flatness measures agree. A high-contrast frame can
        /// legitimately occupy very few luma buckets, and a soft gradient can legitimately have a low
        /// deviation while occupying many, so either test alone rejects real captures.
        /// </summary>
        private const int MinOccupiedLumaBuckets = 3;

        private const float MinLumaStdDev = 1.5f;

        // ------------------------------------------------------------------------------------
        // Pure logic. No Unity types cross these signatures, so this block is executable and
        // known-answer testable outside the editor, and SelfTest() below exercises every branch.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Parses a comma-separated shot list into flags. Tokens are matched case-insensitively with
        /// `_`, `-` and spaces ignored, so `player_verb`, `PlayerVerb` and `player verb` are the same
        /// cue. An unrecognised token fails the whole parse and is reported: silently dropping it
        /// would let a caller ship a capture whose declared shot list is not the one it meant.
        ///
        /// Empty input parses successfully to <see cref="H8ShotCue.None"/>. Whether "no cue" is
        /// acceptable is <see cref="EvaluateCapture"/>'s decision, not the parser's.
        /// </summary>
        public static bool TryParseCues(string csv, out H8ShotCue cues, out string unknownToken)
        {
            cues = H8ShotCue.None;
            unknownToken = null;

            if (string.IsNullOrEmpty(csv))
                return true;

            int start = 0;
            while (start <= csv.Length)
            {
                int comma = csv.IndexOf(',', start);
                int end = comma < 0 ? csv.Length : comma;
                string token = csv.Substring(start, end - start);
                string normalized = NormalizeCueToken(token);

                if (normalized.Length != 0)
                {
                    H8ShotCue parsed = MatchCue(normalized);
                    if (parsed == H8ShotCue.None)
                    {
                        unknownToken = token.Trim();
                        cues = H8ShotCue.None;
                        return false;
                    }

                    cues |= parsed;
                }

                if (comma < 0)
                    break;

                start = comma + 1;
            }

            return true;
        }

        /// <summary>Lower-cases and strips separators. Pure; no culture dependency.</summary>
        private static string NormalizeCueToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return string.Empty;

            char[] buffer = new char[token.Length];
            int written = 0;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (c == '_' || c == '-' || c == ' ' || c == '\t')
                    continue;

                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);

                buffer[written++] = c;
            }

            return new string(buffer, 0, written);
        }

        private static H8ShotCue MatchCue(string normalized)
        {
            switch (normalized)
            {
                case "playerverb":
                case "verb":
                    return H8ShotCue.PlayerVerb;
                case "pressure":
                case "pressurecue":
                    return H8ShotCue.Pressure;
                case "machinery":
                case "machine":
                case "machinerycue":
                case "machinecue":
                    return H8ShotCue.Machinery;
                case "route":
                case "routecue":
                    return H8ShotCue.Route;
                case "scale":
                case "scalecue":
                    return H8ShotCue.Scale;
                case "danger":
                case "dangercue":
                    return H8ShotCue.Danger;
                case "evidence":
                case "evidencecue":
                    return H8ShotCue.Evidence;
                case "instrumentcorruption":
                case "seedship":
                case "instrumentcorruptioncue":
                    return H8ShotCue.InstrumentCorruption;
                default:
                    return H8ShotCue.None;
            }
        }

        /// <summary>Renders the flag set back to the canonical token spelling for the manifest.</summary>
        public static string DescribeCues(H8ShotCue cues)
        {
            if (cues == H8ShotCue.None)
                return "none";

            var builder = new StringBuilder(96);
            AppendCue(builder, cues, H8ShotCue.PlayerVerb, "player_verb");
            AppendCue(builder, cues, H8ShotCue.Pressure, "pressure");
            AppendCue(builder, cues, H8ShotCue.Machinery, "machinery");
            AppendCue(builder, cues, H8ShotCue.Route, "route");
            AppendCue(builder, cues, H8ShotCue.Scale, "scale");
            AppendCue(builder, cues, H8ShotCue.Danger, "danger");
            AppendCue(builder, cues, H8ShotCue.Evidence, "evidence");
            AppendCue(builder, cues, H8ShotCue.InstrumentCorruption, "instrument_corruption");
            return builder.ToString();
        }

        private static void AppendCue(StringBuilder builder, H8ShotCue cues, H8ShotCue flag, string name)
        {
            if ((cues & flag) == 0)
                return;

            if (builder.Length > 0)
                builder.Append(',');

            builder.Append(name);
        }

        /// <summary>
        /// Luma histogram, mean and standard deviation over an interleaved RGB byte buffer.
        ///
        /// Luma is the integer BT.601 form `(77R + 150G + 29B) >> 8`; the three weights sum to 256,
        /// so a white pixel yields exactly 255 and a black pixel exactly 0 with no drift. The
        /// histogram is 64 buckets (`luma >> 2`).
        ///
        /// Pure and allocation-free apart from what the caller supplies. Returns false on any
        /// malformed input rather than producing a number that looks measured.
        /// </summary>
        public static bool TryComputeFrameStatistics(
            byte[] rgb,
            int pixelCount,
            int[] histogram,
            out int occupiedBuckets,
            out float meanLuma,
            out float lumaStdDev)
        {
            occupiedBuckets = 0;
            meanLuma = 0f;
            lumaStdDev = 0f;

            if (rgb == null || histogram == null || histogram.Length != LumaBucketCount)
                return false;

            if (pixelCount < 1 || rgb.Length < pixelCount * 3)
                return false;

            for (int i = 0; i < histogram.Length; i++)
                histogram[i] = 0;

            long sum = 0;
            long sumOfSquares = 0;

            for (int p = 0; p < pixelCount; p++)
            {
                int o = p * 3;
                int luma = (77 * rgb[o] + 150 * rgb[o + 1] + 29 * rgb[o + 2]) >> 8;
                histogram[luma >> 2]++;
                sum += luma;
                sumOfSquares += (long)luma * luma;
            }

            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] != 0)
                    occupiedBuckets++;
            }

            double mean = sum / (double)pixelCount;
            double variance = (sumOfSquares / (double)pixelCount) - (mean * mean);
            if (variance < 0.0)
                variance = 0.0;

            meanLuma = (float)mean;
            lumaStdDev = (float)System.Math.Sqrt(variance);
            return true;
        }

        /// <summary>
        /// The whole acceptance policy, in one pure function so it can be argued with and tested.
        ///
        /// Order matters: a missing graphics device explains everything downstream, so it is checked
        /// first and no later reject is allowed to mask it.
        /// </summary>
        public static H8CaptureVerdict EvaluateCapture(
            bool graphicsDevicePresent,
            bool cameraResolved,
            H8ShotCue declaredCues,
            int occupiedLumaBuckets,
            float lumaStdDev,
            int renderersInFrustum)
        {
            if (!graphicsDevicePresent)
                return H8CaptureVerdict.RejectedNoGraphicsDevice;

            if (!cameraResolved)
                return H8CaptureVerdict.RejectedNoCamera;

            if (declaredCues == H8ShotCue.None)
                return H8CaptureVerdict.RejectedNoDeclaredCue;

            if (occupiedLumaBuckets < MinOccupiedLumaBuckets && lumaStdDev < MinLumaStdDev)
                return H8CaptureVerdict.RejectedBlankFrame;

            if (renderersInFrustum < 1)
                return H8CaptureVerdict.RejectedNothingInFrame;

            return H8CaptureVerdict.EvidenceEligible;
        }

        /// <summary>Process exit code for a verdict. 0 only for an eligible capture.</summary>
        public static int ExitCodeFor(H8CaptureVerdict verdict)
        {
            switch (verdict)
            {
                case H8CaptureVerdict.EvidenceEligible:
                    return 0;
                case H8CaptureVerdict.RejectedNoGraphicsDevice:
                    return 3;
                case H8CaptureVerdict.RefusedNoOutputDirectory:
                    return 5;
                default:
                    return 4;
            }
        }

        // ------------------------------------------------------------------------------------
        // Editor route.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Command-line entry point. Never marked `[InitializeOnLoad]`: a capture that fires itself
        /// on every domain reload is a side effect nobody asked for, and it is how
        /// `ScreenshotGrabber` ended up commented out instead of fixed.
        /// </summary>
        public static void Run()
        {
            string label = ReadStringArg("-h8CaptureLabel", "route");
            string cueCsv = ReadStringArg("-h8CaptureCues", null);
            string scenePath = ReadStringArg("-h8CaptureScene", null);
            int width = System.Math.Max(16, ReadIntArg("-h8CaptureWidth", DefaultWidth));
            int height = System.Math.Max(16, ReadIntArg("-h8CaptureHeight", DefaultHeight));

            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError(
                    $"{Marker} ABORT scripts failed to compile. A capture taken now would render the " +
                    "last successfully compiled state and be attributed to the current source.");
                Finish(5);
                return;
            }

            if (!TryParseCues(cueCsv, out H8ShotCue cues, out string unknownToken))
            {
                Debug.LogError(
                    $"{Marker} ABORT -h8CaptureCues contains '{unknownToken}', which is not one of the " +
                    "eight cues in TASTE.md:403-412: player_verb, pressure, machinery, route, scale, " +
                    "danger, evidence, instrument_corruption.");
                Finish(5);
                return;
            }

            if (scenePath != null && !TryOpenSceneWithoutDiscardingWork(scenePath))
            {
                Finish(5);
                return;
            }

            H8CaptureVerdict verdict = Capture(label, cues, width, height, out string runDirectory);
            Debug.Log($"{Marker} DONE verdict={verdict} dir={runDirectory ?? "<none>"}");
            Finish(ExitCodeFor(verdict));
        }

        [MenuItem("Tools/Hecton/Capture/Route Capture (non-mutating)", priority = 240)]
        private static void CaptureFromMenu()
        {
            H8CaptureVerdict verdict = CaptureCurrentEditorState(
                "manual",
                H8ShotCue.Route | H8ShotCue.Scale,
                out string runDirectory);

            Debug.Log($"{Marker} menu capture verdict={verdict} dir={runDirectory ?? "<none>"}");
        }

        /// <summary>
        /// Captures whatever is loaded right now. Opens nothing, saves nothing, imports nothing.
        /// This is the entry point other in-project editor tools should call.
        /// </summary>
        public static H8CaptureVerdict CaptureCurrentEditorState(
            string label,
            H8ShotCue declaredCues,
            out string runDirectory)
        {
            return Capture(label, declaredCues, DefaultWidth, DefaultHeight, out runDirectory);
        }

        /// <summary>
        /// Opens a scene only when nothing would be lost by doing so.
        ///
        /// `EditorSceneManager.OpenScene` on a dirty scene discards the unsaved edits without asking
        /// in batchmode. `AGENTS.md:124` exists to stop automation wiping a level designer's work;
        /// with several agents in one working tree it also stops one lane wiping another's. Refusing
        /// is the correct outcome, not a limitation to route around.
        /// </summary>
        private static bool TryOpenSceneWithoutDiscardingWork(string scenePath)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isDirty)
                    continue;

                Debug.LogError(
                    $"{Marker} REFUSED to open '{scenePath}': scene '{scene.name}' has unsaved changes " +
                    "and opening would discard them. Save or discard it deliberately, then re-run.");
                return false;
            }

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Marker} FAILED to open '{scenePath}': {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static H8CaptureVerdict Capture(
            string label,
            H8ShotCue declaredCues,
            int width,
            int height,
            out string runDirectory)
        {
            runDirectory = null;

            bool graphicsDevicePresent = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

            if (!Hecton8.Tools.H8CaptureRunDirectory.TryCreateRunDirectory(label, out runDirectory))
            {
                Debug.LogError(
                    $"{Marker} could not reserve an unused directory under " +
                    Hecton8.Tools.H8CaptureRunDirectory.ResolveCaptureRoot() +
                    ". Refusing to write into an existing capture.");
                return H8CaptureVerdict.RefusedNoOutputDirectory;
            }

            if (!graphicsDevicePresent)
            {
                // Honest stop. No PNG: a blank image here would be indistinguishable from a real
                // capture of a broken art pass, and somebody would grade it.
                WriteManifest(
                    runDirectory, label, declaredCues, H8CaptureVerdict.RejectedNoGraphicsDevice,
                    "<none>", false, width, height, 0, 0f, 0f, 0, 0f, 0f, string.Empty);

                Debug.LogError(
                    $"{Marker} NO GRAPHICS DEVICE (graphicsDeviceType=Null). This editor was launched " +
                    "with -nographics, under which no camera render and no ScreenCapture call " +
                    "produces pixels. No PNG written. Manifest at " + runDirectory);
                return H8CaptureVerdict.RejectedNoGraphicsDevice;
            }

            string dirtyBefore = DescribeDirtyScenes();

            Camera camera = ResolveCaptureCamera(out bool cameraWasCreated);
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            RenderTexture previousTarget = camera != null ? camera.targetTexture : null;

            try
            {
                if (camera == null)
                {
                    WriteManifest(
                        runDirectory, label, declaredCues, H8CaptureVerdict.RejectedNoCamera,
                        "<none>", false, width, height, 0, 0f, 0f, 0, 0f, 0f, dirtyBefore);

                    Debug.LogError($"{Marker} no camera could be resolved or created. No PNG written.");
                    return H8CaptureVerdict.RejectedNoCamera;
                }

                renderTexture = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                renderTexture.Create();

                RenderThroughPipeline(camera, renderTexture);

                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                int occupiedBuckets = 0;
                float meanLuma = 0f;
                float lumaStdDev = 0f;
                SampleFrameStatistics(texture, width, height, out occupiedBuckets, out meanLuma, out lumaStdDev);

                int renderersInFrustum = CountRenderersInFrustum(camera, out float smallestExtent, out float largestExtent);

                H8CaptureVerdict verdict = EvaluateCapture(
                    true, true, declaredCues, occupiedBuckets, lumaStdDev, renderersInFrustum);

                // The PNG is written for every verdict except the no-device case: a rejected frame is
                // exactly the artifact a VISUAL_ROUTE_INVALID call needs to point at
                // (QUALITY_GATES.md:177-178).
                File.WriteAllBytes(Path.Combine(runDirectory, label + ".png"), texture.EncodeToPNG());

                WriteManifest(
                    runDirectory, label, declaredCues, verdict, camera.name, cameraWasCreated,
                    width, height, occupiedBuckets, meanLuma, lumaStdDev,
                    renderersInFrustum, smallestExtent, largestExtent, dirtyBefore);

                Debug.Log(
                    $"{Marker} {verdict} label={label} camera={camera.name} " +
                    $"lumaBuckets={occupiedBuckets} lumaStdDev={lumaStdDev.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"renderersInFrustum={renderersInFrustum} dir={runDirectory}");

                return verdict;
            }
            finally
            {
                RenderTexture.active = null;

                if (camera != null)
                    camera.targetTexture = previousTarget;

                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (cameraWasCreated && camera != null)
                    UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        /// <summary>
        /// URP does not render from a bare `Camera.Render()` in batchmode; `SubmitRenderRequest` is
        /// the supported path. Same shape as `Assets/_Project/Editor/H8_ScreenshotTaker.cs:64-90`,
        /// which is the only render call in this project already proven to compile against the
        /// installed URP version.
        /// </summary>
        private static void RenderThroughPipeline(Camera camera, RenderTexture target)
        {
            var urpPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpPipeline != null)
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest();
                request.destination = RTHandles.Alloc(target);
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    // DO NOT Release() here - see the note below; the caller owns `target` and
                    // reads it with ReadPixels after this returns.
                    return;
                }

                // Safe on this path only: URP declined the request, so nothing was rendered into
                // the target and the caller falls back to Camera.Render(). Even so it is dropped,
                // because SingleCameraRequest.destination is declared `public RenderTexture`
                // (URP Runtime/UniversalRenderPipeline.cs:2706) and RTHandles.Alloc converts
                // implicitly to it, making this RenderTexture.Release() on a surface this method
                // does not own. On the SUCCESS path above it freed the very frame the caller was
                // about to read, which returned a blank capture regardless of scene contents.
                Debug.LogWarning(
                    $"{Marker} URP declined the render request for '{camera.name}'; falling back to " +
                    "Camera.Render(), which under URP can yield clear colour only. Treat the frame " +
                    "statistics in the manifest as the authority on whether anything was drawn.");
            }

            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
        }

        /// <summary>
        /// Picks the highest-depth enabled camera that is not already rendering to a texture.
        ///
        /// Not `Camera.main` - banned by `AGENTS.md:336`, and wrong besides: it resolves only a
        /// camera tagged `MainCamera`, so an untagged player rig reads as "no camera" and the route
        /// falls back to an empty survey shot. `FindObjectsByType` rather than scene-root traversal
        /// because root traversal cannot see the `DontDestroyOnLoad` scene, the blind spot recorded
        /// at `Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:768-771`. Cold, once per capture.
        /// </summary>
        private static Camera ResolveCaptureCamera(out bool created)
        {
            created = false;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Camera best = null;
            float bestDepth = float.NegativeInfinity;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.targetTexture != null)
                    continue;

                if (candidate.depth <= bestDepth)
                    continue;

                bestDepth = candidate.depth;
                best = candidate;
            }

            if (best != null)
                return best;

            // HideAndDontSave keeps this object out of scene serialization entirely, so it cannot be
            // saved into a production scene by any later save the user performs. It is destroyed in
            // the caller's finally regardless of outcome.
            var surveyGO = new GameObject("H8_CaptureSurveyCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Camera survey = surveyGO.AddComponent<Camera>();
            survey.transform.position = new Vector3(0f, 150f, -200f);
            survey.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            survey.nearClipPlane = 1f;
            survey.farClipPlane = 50000f;
            survey.clearFlags = CameraClearFlags.Skybox;
            created = true;
            return survey;
        }

        /// <summary>
        /// Samples the frame on a stride grid and runs the pure statistics over it. Sub-sampling is
        /// deterministic and covers the whole frame, so a uniform image stays uniform and a detailed
        /// image stays detailed; it only removes the 6 MB full-resolution copy.
        /// </summary>
        private static void SampleFrameStatistics(
            Texture2D texture,
            int width,
            int height,
            out int occupiedBuckets,
            out float meanLuma,
            out float lumaStdDev)
        {
            occupiedBuckets = 0;
            meanLuma = 0f;
            lumaStdDev = 0f;

            Color32[] pixels = texture.GetPixels32();
            int sampledWidth = (width + StatisticsSampleStride - 1) / StatisticsSampleStride;
            int sampledHeight = (height + StatisticsSampleStride - 1) / StatisticsSampleStride;
            int sampleCount = sampledWidth * sampledHeight;
            if (sampleCount < 1)
                return;

            byte[] rgb = new byte[sampleCount * 3];
            int written = 0;
            for (int y = 0; y < height; y += StatisticsSampleStride)
            {
                int row = y * width;
                for (int x = 0; x < width; x += StatisticsSampleStride)
                {
                    Color32 c = pixels[row + x];
                    rgb[written * 3] = c.r;
                    rgb[written * 3 + 1] = c.g;
                    rgb[written * 3 + 2] = c.b;
                    written++;
                }
            }

            var histogram = new int[LumaBucketCount];
            TryComputeFrameStatistics(rgb, written, histogram, out occupiedBuckets, out meanLuma, out lumaStdDev);
        }

        /// <summary>
        /// Counts enabled renderers whose bounds intersect the camera frustum, and records the
        /// smallest and largest visible extent. The extents are the only objective handle this route
        /// has on `TASTE.md`'s scale cue: a frame containing only same-sized objects has no scale
        /// reference in it, whatever the caller declared. Reported as measurement, not verdict.
        /// </summary>
        private static int CountRenderersInFrustum(Camera camera, out float smallestExtent, out float largestExtent)
        {
            smallestExtent = 0f;
            largestExtent = 0f;

            var planes = new UnityEngine.Plane[6];
            GeometryUtility.CalculateFrustumPlanes(camera, planes);

            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            int visible = 0;
            float minExtent = float.MaxValue;
            float maxExtent = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                UnityEngine.Bounds bounds = renderer.bounds;
                if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                    continue;

                visible++;

                UnityEngine.Vector3 size = bounds.size;
                float extent = size.x;
                if (size.y > extent)
                    extent = size.y;
                if (size.z > extent)
                    extent = size.z;

                if (extent < minExtent)
                    minExtent = extent;
                if (extent > maxExtent)
                    maxExtent = extent;
            }

            if (visible > 0)
            {
                smallestExtent = minExtent;
                largestExtent = maxExtent;
            }

            return visible;
        }

        private static string DescribeDirtyScenes()
        {
            var builder = new StringBuilder(64);
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isDirty)
                    continue;

                if (builder.Length > 0)
                    builder.Append(',');

                builder.Append(scene.name);
            }

            return builder.ToString();
        }

        private static void WriteManifest(
            string runDirectory,
            string label,
            H8ShotCue declaredCues,
            H8CaptureVerdict verdict,
            string cameraName,
            bool cameraWasCreated,
            int width,
            int height,
            int occupiedLumaBuckets,
            float meanLuma,
            float lumaStdDev,
            int renderersInFrustum,
            float smallestVisibleExtent,
            float largestVisibleExtent,
            string dirtyScenesBefore)
        {
            string dirtyAfter = DescribeDirtyScenes();

            var builder = new StringBuilder(1024);

            // camera.md:57 - "capture scene must state whether it is gameplay, staged in-engine,
            // editor render, or concept".
            builder.Append("captureTruth=")
                .Append(cameraWasCreated ? "editor_render_survey_camera" : "editor_render_scene_camera")
                .Append('\n');
            builder.Append("route=Hecton8.EditorTools.H8_RouteCaptureStation\n");
            builder.Append("label=").Append(label).Append('\n');
            builder.Append("verdict=").Append(verdict).Append('\n');
            builder.Append("declaredShotCues=").Append(DescribeCues(declaredCues)).Append('\n');
            builder.Append("camera=").Append(cameraName).Append('\n');
            builder.Append("resolution=").Append(width.ToString(CultureInfo.InvariantCulture))
                .Append('x').Append(height.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("graphicsDevice=").Append(SystemInfo.graphicsDeviceType).Append('\n');
            builder.Append("batchmode=").Append(Application.isBatchMode ? "true" : "false").Append('\n');
            builder.Append("occupiedLumaBuckets=").Append(occupiedLumaBuckets.ToString(CultureInfo.InvariantCulture))
                .Append('/').Append(LumaBucketCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("meanLuma=").Append(meanLuma.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("lumaStdDev=").Append(lumaStdDev.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("renderersInFrustum=").Append(renderersInFrustum.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("smallestVisibleExtentMetres=")
                .Append(smallestVisibleExtent.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("largestVisibleExtentMetres=")
                .Append(largestVisibleExtent.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("dirtyScenesBeforeCapture=")
                .Append(dirtyScenesBefore.Length == 0 ? "none" : dirtyScenesBefore).Append('\n');
            builder.Append("dirtyScenesAfterCapture=")
                .Append(dirtyAfter.Length == 0 ? "none" : dirtyAfter).Append('\n');
            builder.Append("mutatedSceneState=")
                .Append(string.Equals(dirtyScenesBefore, dirtyAfter, StringComparison.Ordinal) ? "false" : "TRUE")
                .Append('\n');
            builder.Append("savedAssets=false\n");
            builder.Append("acceptance=NONE - QUALITY_GATES.md:176: a raw diagnostic capture can reject ")
                .Append("visual quality, never accept it. EvidenceEligible means the frame is fit to ")
                .Append("enter the Visual Reference Parity Gate, not that it passed one.\n");

            File.WriteAllText(Path.Combine(runDirectory, "capture_truth.txt"), builder.ToString(), Encoding.UTF8);
        }

        private static void Finish(int exitCode)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        // Fully qualified: Hecton8.Environment shadows System.Environment inside the Hecton8.*
        // namespace root and a bare `Environment` fails CS0234 here.
        private static string ReadStringArg(string flag, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return fallback;
        }

        private static int ReadIntArg(string flag, int fallback)
        {
            string raw = ReadStringArg(flag, null);
            return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        // ------------------------------------------------------------------------------------
        // Known-answer self test for the pure block. Needs no scene, no camera and no graphics
        // device, so it runs under -nographics where the capture itself honestly cannot.
        // ------------------------------------------------------------------------------------

        [MenuItem("Tools/Hecton/Capture/Route Capture Self Test", priority = 241)]
        public static void SelfTest()
        {
            int failures = 0;

            failures += ExpectCueParse("route,pressure , Scale", true, H8ShotCue.Route | H8ShotCue.Pressure | H8ShotCue.Scale, null);
            failures += ExpectCueParse("player_verb", true, H8ShotCue.PlayerVerb, null);
            failures += ExpectCueParse("PlayerVerb", true, H8ShotCue.PlayerVerb, null);
            failures += ExpectCueParse("", true, H8ShotCue.None, null);
            failures += ExpectCueParse("route,sparkle", false, H8ShotCue.None, "sparkle");

            // Uniform mid grey: one occupied bucket, zero deviation. This is the shape a clear-colour
            // frame has, and it is the case the route must never call evidence.
            byte[] flat = new byte[300];
            for (int i = 0; i < flat.Length; i++)
                flat[i] = 128;

            var histogram = new int[LumaBucketCount];
            if (!TryComputeFrameStatistics(flat, 100, histogram, out int flatBuckets, out float flatMean, out float flatStdDev))
                failures += Fail("TryComputeFrameStatistics rejected a valid 100-pixel buffer");

            failures += ExpectInt("flat occupiedBuckets", 1, flatBuckets);

            // Exactly 128: the weights 77+150+29 sum to 256, so (256 * 128) >> 8 == 128 with no
            // rounding drift. Measured outside Unity, not assumed.
            failures += ExpectFloat("flat meanLuma", 128f, flatMean, 0.01f);
            failures += ExpectFloat("flat lumaStdDev", 0f, flatStdDev, 0.001f);

            // Alternating pure black / pure white: only two buckets, but maximal deviation. Proves
            // the blank test needs both measures to agree before it rejects.
            byte[] checker = new byte[300];
            for (int p = 0; p < 100; p++)
            {
                byte v = (p & 1) == 0 ? (byte)0 : (byte)255;
                checker[p * 3] = v;
                checker[p * 3 + 1] = v;
                checker[p * 3 + 2] = v;
            }

            if (!TryComputeFrameStatistics(checker, 100, histogram, out int checkerBuckets, out float checkerMean, out float checkerStdDev))
                failures += Fail("TryComputeFrameStatistics rejected the checker buffer");

            failures += ExpectInt("checker occupiedBuckets", 2, checkerBuckets);
            failures += ExpectFloat("checker meanLuma", 127.5f, checkerMean, 0.01f);
            failures += ExpectFloat("checker lumaStdDev", 127.5f, checkerStdDev, 0.01f);

            // Malformed input must fail rather than return a plausible zero.
            if (TryComputeFrameStatistics(null, 100, histogram, out _, out _, out _))
                failures += Fail("TryComputeFrameStatistics accepted a null buffer");
            if (TryComputeFrameStatistics(flat, 500, histogram, out _, out _, out _))
                failures += Fail("TryComputeFrameStatistics accepted a pixel count larger than the buffer");
            if (TryComputeFrameStatistics(flat, 100, new int[8], out _, out _, out _))
                failures += Fail("TryComputeFrameStatistics accepted a wrong-sized histogram");

            failures += ExpectVerdict("no device", H8CaptureVerdict.RejectedNoGraphicsDevice,
                EvaluateCapture(false, true, H8ShotCue.Route, 40, 30f, 100));
            failures += ExpectVerdict("no camera", H8CaptureVerdict.RejectedNoCamera,
                EvaluateCapture(true, false, H8ShotCue.Route, 40, 30f, 100));
            failures += ExpectVerdict("no declared cue", H8CaptureVerdict.RejectedNoDeclaredCue,
                EvaluateCapture(true, true, H8ShotCue.None, 40, 30f, 100));
            failures += ExpectVerdict("blank frame", H8CaptureVerdict.RejectedBlankFrame,
                EvaluateCapture(true, true, H8ShotCue.Route, flatBuckets, flatStdDev, 100));
            failures += ExpectVerdict("checker is not blank", H8CaptureVerdict.EvidenceEligible,
                EvaluateCapture(true, true, H8ShotCue.Route, checkerBuckets, checkerStdDev, 100));
            failures += ExpectVerdict("nothing in frame", H8CaptureVerdict.RejectedNothingInFrame,
                EvaluateCapture(true, true, H8ShotCue.Route, 40, 30f, 0));
            failures += ExpectVerdict("eligible", H8CaptureVerdict.EvidenceEligible,
                EvaluateCapture(true, true, H8ShotCue.Route | H8ShotCue.Danger, 40, 30f, 12));

            failures += ExpectInt("exit code eligible", 0, ExitCodeFor(H8CaptureVerdict.EvidenceEligible));
            failures += ExpectInt("exit code no device", 3, ExitCodeFor(H8CaptureVerdict.RejectedNoGraphicsDevice));
            failures += ExpectInt("exit code blank", 4, ExitCodeFor(H8CaptureVerdict.RejectedBlankFrame));
            failures += ExpectInt("exit code refused", 5, ExitCodeFor(H8CaptureVerdict.RefusedNoOutputDirectory));

            failures += ExpectString("cue description",
                "player_verb,route", DescribeCues(H8ShotCue.PlayerVerb | H8ShotCue.Route));
            failures += ExpectString("empty cue description", "none", DescribeCues(H8ShotCue.None));

            // Run-directory uniqueness: the first two candidates are occupied, so the third must be
            // chosen. This is the property that stops one lane overwriting another lane's evidence.
            failures += ExpectRunDirectory();

            Debug.Log($"{Marker} SELFTEST failures={failures}");
            if (Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int ExpectRunDirectory()
        {
            string root = Path.Combine("X", "Logs", "RouteCaptures");
            string taken1 = Path.Combine(root, "first_exit_20260727_221500");
            string taken2 = Path.Combine(root, "first_exit_20260727_221500_2");
            string expected = Path.Combine(root, "first_exit_20260727_221500_3");

            Func<string, bool> occupied = candidate =>
                string.Equals(candidate, taken1, StringComparison.Ordinal) ||
                string.Equals(candidate, taken2, StringComparison.Ordinal);

            if (!Hecton8.Tools.H8CaptureRunDirectory.TryResolveUniqueRunDirectory(
                    root, "first exit", "20260727_221500", occupied, 16, out string resolved))
            {
                return Fail("TryResolveUniqueRunDirectory found no free name in 16 attempts");
            }

            int failures = ExpectString("unique run directory", expected, resolved);

            // Exhaustion must be reported, never papered over by reusing a directory.
            if (Hecton8.Tools.H8CaptureRunDirectory.TryResolveUniqueRunDirectory(
                    root, "first exit", "20260727_221500", _ => true, 4, out _))
            {
                failures += Fail("TryResolveUniqueRunDirectory returned a directory when every candidate existed");
            }

            return failures;
        }

        private static int ExpectCueParse(string csv, bool expectedOk, H8ShotCue expectedCues, string expectedUnknown)
        {
            bool ok = TryParseCues(csv, out H8ShotCue cues, out string unknown);
            if (ok != expectedOk || cues != expectedCues || !string.Equals(unknown, expectedUnknown, StringComparison.Ordinal))
            {
                return Fail(
                    $"TryParseCues(\"{csv}\") -> ok={ok} cues={cues} unknown={unknown ?? "<null>"}; " +
                    $"expected ok={expectedOk} cues={expectedCues} unknown={expectedUnknown ?? "<null>"}");
            }

            return 0;
        }

        private static int ExpectVerdict(string what, H8CaptureVerdict expected, H8CaptureVerdict actual)
        {
            return expected == actual ? 0 : Fail($"{what}: expected {expected}, got {actual}");
        }

        private static int ExpectInt(string what, int expected, int actual)
        {
            return expected == actual ? 0 : Fail($"{what}: expected {expected}, got {actual}");
        }

        private static int ExpectFloat(string what, float expected, float actual, float tolerance)
        {
            float delta = actual - expected;
            if (delta < 0f)
                delta = -delta;

            return delta <= tolerance
                ? 0
                : Fail($"{what}: expected {expected.ToString("F4", CultureInfo.InvariantCulture)}, got " +
                       actual.ToString("F4", CultureInfo.InvariantCulture));
        }

        private static int ExpectString(string what, string expected, string actual)
        {
            return string.Equals(expected, actual, StringComparison.Ordinal)
                ? 0
                : Fail($"{what}: expected \"{expected}\", got \"{actual}\"");
        }

        private static int Fail(string message)
        {
            Debug.LogError($"{Marker} SELFTEST FAIL {message}");
            return 1;
        }
    }
}
#endif
