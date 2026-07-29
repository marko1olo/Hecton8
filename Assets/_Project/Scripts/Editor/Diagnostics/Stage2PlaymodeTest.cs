using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MapMagic.Editor.Diagnostics
{
    /// <summary>
    /// Splatmap test: runs the sandbox render scene in play mode, waits for 9 terrains that all carry
    /// alphamaps, then writes a colour-coded preview of the centre terrain's live splatmap.
    ///
    /// READ THIS BEFORE TRUSTING ITS OUTPUT. Like <c>Stage3PlaymodeTest</c> it deliberately dismantles the
    /// game to isolate one system: it destroys every <c>GameBootstrapper</c> in the scene, forces the
    /// bootstrapper's private <c>_isUnityTestRunnerProcess</c> to true by reflection, destroys every
    /// <c>H8_PlayModeScreenshotter</c>, injects a synthetic "Main Camera" at (500, 100, 500) so MapMagic has
    /// a pin to generate around, and runs 020_RENDER_SANDBOX_V2 rather than the shipping world scene. Its
    /// report therefore describes a world with no bootstrap: it is evidence about splatmap generation only,
    /// and is not a statement about the shipping game. Unlike Stage3 it does NOT set
    /// <c>bypassSplatmapGeneration</c> - splatmaps are the thing being measured here.
    ///
    /// It also calls <c>OpenScene</c> with no dirty check, so unsaved edits in currently open scenes are
    /// discarded without a prompt.
    ///
    /// What it used to do wrong. Every one of these reported success:
    ///   - all four artifacts (progress log, report, fatal-error dump, splat PNG) went to another agent's
    ///     private brain directory outside the repo, which no batchmode log reader ever opens.
    ///   - the catch in CaptureAndExit wrote to that same unversioned directory - so if the directory did
    ///     not exist the write threw inside the catch, destroying the original exception - and the finally
    ///     block exited 0 regardless. A total failure was indistinguishable from a pass.
    ///   - the 6000-tick timeout took the identical exit path as a clean pass: exit 0.
    ///   - the timeout path never unsubscribed OnUpdate, so later ticks re-entered CaptureAndExit and
    ///     queued further exits.
    ///   - "ERROR getting alphamaps" and "No alphamap layers" were appended to the report as text and then
    ///     exited 0, so a run that produced no splat evidence at all still read as a pass.
    ///   - static counters were never reset, so a second run in one editor session inherited the first
    ///     run's tick and stable-frame counts; and isRunning was set true and never cleared, so the second
    ///     invocation returned immediately, did nothing, and said nothing.
    ///   - updateTicks was incremented BELOW the "waiting for MapMagicObject" early-out, so a scene that
    ///     never produced a MapMagicObject could never reach the timeout: the run spun forever.
    ///
    /// Three of those repairs were incomplete and were finished afterwards:
    ///   - the tick-ordering fix stopped one level too shallow. updateTicks still only advances once
    ///     EditorApplication.isPlaying is true, and the <c>!isPlaying</c> early-out above it was
    ///     unbounded, so a play mode that never engaged still spun forever. It is now bounded by
    ///     PlayModeEntryTimeoutSeconds in wall clock and exits 4.
    ///   - nothing claimed the play-mode session. H8_PlayModeScreenshotter calls
    ///     EditorApplication.Exit(0) on its own wall clock (H8_PlayModeScreenshotter.cs:270-271, at
    ///     roughly 200s), which ends this run with a SUCCESS code and no report at all. Run() now sets
    ///     ExternalSessionOwner, the mechanism H8_HeadlessPlayModeProbe.cs:421 already uses.
    ///   - the screenshotter destruction was scheduled BELOW the "waiting for MapMagicObject"
    ///     early-out, i.e. after the hazard it defends against. It now runs beside the bootstrapper
    ///     destruction, on every tick until setup completes.
    /// </summary>
    public static class Stage2PlaymodeTest
    {
        private static int stableFrames = 0;
        private static int updateTicks = 0;
        private static bool isRunning = false;
        private static bool didDestroyInterfering = false;
        private static bool forcedTestRunnerFlag = false;

        // Bounds the wait for play mode to actually engage, in WALL CLOCK rather than ticks. updateTicks
        // only advances once EditorApplication.isPlaying is true - OnUpdate returns above the increment
        // until then - so the 6000-tick timeout cannot bound this window. Without a deadline here a play
        // mode that never engaged left OnUpdate returning forever: no report, no verdict, no exit code,
        // and a batchmode editor that hung until somebody killed it. That is the same defect as the
        // tick-ordering bug in OnUpdate, one layer further out.
        // ProjectSettings/EditorSettings.asset:29-30 sets m_EnterPlayModeOptionsEnabled 1 /
        // m_EnterPlayModeOptions 1 (DisableDomainReload), so these statics and the EditorApplication.update
        // subscription survive play mode entry and this deadline really does get evaluated.
        private const double PlayModeEntryTimeoutSeconds = 300d;
        private static double playModeEntryDeadline = 0d;
        private static bool enteredPlayMode = false;

        // Was another agent's private brain directory - outside the repo, unversioned, and invisible to
        // anyone auditing this project's evidence. The per-tool subfolder is not cosmetic: two tools that
        // both write "stage2_report.txt" into one directory destroy each other's evidence silently.
        private static readonly string outDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "stage2_playmode");

        // A timeout is a failure. The old code appended "TIMEOUT REACHED. Exiting." to a debug file and
        // then took the same exit path as a clean pass, so a run that never stabilised reported success.
        private static bool timedOut = false;

        [MenuItem("Hecton8/Diagnostics/Stage 2 Playmode Splat Test")]
        public static void Run()
        {
            if (isRunning)
            {
                // Used to be a bare `return`. isRunning was never cleared, so every invocation after the
                // first in one editor session measured nothing and reported nothing at all. It does not
                // Exit() here: this branch is unreachable in batchmode (a fresh process starts with
                // isRunning false), so killing the editor over a double menu click would be pure harm.
                Debug.LogError(
                    "[Stage2PlaymodeTest] REFUSED: a run is already in flight (isRunning). Nothing was " +
                    "measured by this invocation. Wait for the running one to reach its verdict.");
                return;
            }

            // This is a PLAY MODE test that waits on MapMagic generation and then reads alphamaps back out
            // of live terrainData. C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics
            // for MapMagic generation tests because compute shaders and Graphics.Blit return zeros with no
            // GPU context. Terrains would appear, their splatmaps would be zeros, the preview PNG would be
            // black, and every counter in the report would look healthy.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[Stage2PlaymodeTest] REFUSED: no GPU context (graphicsDeviceType == Null). A play-mode " +
                    "splatmap test cannot produce valid evidence here - generation output is zeros and the " +
                    "splat preview would be blank. Remove -nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            isRunning = true;

            // Static state is reset per run. Without this a second run in the same editor session starts
            // with the first run's updateTicks (so it can trip the timeout on tick one) and its
            // stableFrames (so it can declare a pass before measuring anything).
            Directory.CreateDirectory(outDir);
            stableFrames = 0;
            updateTicks = 0;
            didDestroyInterfering = false;
            timedOut = false;
            enteredPlayMode = false;
            playModeEntryDeadline = EditorApplication.timeSinceStartup + PlayModeEntryTimeoutSeconds;

            // Claim the play-mode session before anything enters it, the same way
            // H8_HeadlessPlayModeProbe.cs:421 does.
            //
            // H8_PlayModeScreenshotter is live in this project. It captures at roughly its
            // PlayerWaitSeconds + SettleSeconds (H8_PlayModeScreenshotter.cs:161,175 - about 200s of wall
            // time) and then calls EditorApplication.Exit(0) at H8_PlayModeScreenshotter.cs:270-271: it
            // terminates the editor process with a SUCCESS code. Destroying the screenshotters below is not
            // enough on its own - that only reaches instances this tool can see, at the moment it looks -
            // and MapMagic generation here routinely outlasts 200s. A screenshotter that survived long
            // enough would end this run with exit 0 and no report, no splat PNG, and nothing in the log to
            // distinguish it from a pass. The screenshotter still takes its capture, which is real
            // evidence; only the teardown is withheld while an owner is named.
            Hecton8.Tools.H8_PlayModeScreenshotter.ExternalSessionOwner = nameof(Stage2PlaymodeTest);

            var field = typeof(Hecton8.Bootstrap.GameBootstrapper).GetField("_isUnityTestRunnerProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            forcedTestRunnerFlag = field != null;
            if (field != null)
            {
                field.SetValue(null, true);
            }
            else
            {
                // The SCOPE line of the report would otherwise claim a bootstrapper state this run never
                // established.
                Debug.LogWarning(
                    "[Stage2PlaymodeTest] _isUnityTestRunnerProcess was not found on GameBootstrapper - it " +
                    "was renamed or removed. The run continues with the bootstrapper in its normal mode, " +
                    "which is NOT the scope this test was written to measure.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");

            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                // Bounded. This early-out used to be a bare `return`, and everything that can produce a
                // verdict lives below it, so a play mode that never engaged - a compile error blocking it,
                // a scene that fails to load, another tool leaving play mode - spun here forever and the
                // run never reported anything at all. Once play mode has been seen the deadline is
                // disarmed, so the ordinary transition out of play mode after a capture cannot trip it
                // (OnUpdate is already unsubscribed by then anyway).
                if (!enteredPlayMode && EditorApplication.timeSinceStartup > playModeEntryDeadline)
                {
                    timedOut = true;
                    string entryLine =
                        "TIMEOUT: play mode never engaged within " + PlayModeEntryTimeoutSeconds +
                        "s of Run(). Nothing was measured - no terrains, no alphamaps, no splat preview.";
                    File.AppendAllText(Path.Combine(outDir, "stage2_debug.txt"), entryLine + "\n");
                    Debug.LogError("[Stage2PlaymodeTest] " + entryLine);
                    EditorApplication.update -= OnUpdate;
                    CaptureAndExit(new UnityEngine.Terrain[0]);
                }
                return;
            }

            enteredPlayMode = true;

            // Ticks advance BEFORE any early-out. The old code incremented this below the setup block, and
            // that block returned early while it waited for a MapMagicObject to appear - so a scene that
            // never produced one left updateTicks pinned at 0, the timeout could never fire, no report was
            // ever written, and the process hung until somebody killed it.
            updateTicks++;

            if (updateTicks > 6000)
            {
                // A timeout means the world never reached 9 stable terrains carrying alphamaps. The capture
                // still runs because a picture of the stuck state is useful, but the exit code must say
                // FAILED. The old code also called CaptureAndExit here WITHOUT unsubscribing, so OnUpdate
                // kept firing and queued a second capture and a second exit.
                timedOut = true;
                UnityEngine.Terrain[] atTimeout = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                string timeoutLine =
                    $"TIMEOUT after {updateTicks} ticks: terrains={atTimeout.Length} (needed >= 9), " +
                    $"setupComplete={didDestroyInterfering}, stableFrames={stableFrames} (needed > 200).";
                File.AppendAllText(Path.Combine(outDir, "stage2_debug.txt"), timeoutLine + "\n");
                Debug.LogError("[Stage2PlaymodeTest] " + timeoutLine);
                EditorApplication.update -= OnUpdate;
                CaptureAndExit(atTimeout);
                return;
            }

            if (!didDestroyInterfering)
            {
                // Destroy bootstrapper IMMEDIATELY on every tick to avoid timeouts and scene reloading
                var bootstrappers = Object.FindObjectsByType<Hecton8.Bootstrap.GameBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var b in bootstrappers) Object.DestroyImmediate(b.gameObject);

                // Screenshotters die on every tick too, for the same reason. This used to sit BELOW the
                // "wait for a MapMagicObject" early-out, so it did not run until generation had already
                // started - while the thing it defends against (EditorApplication.Exit(0) out of
                // H8_PlayModeScreenshotter.cs:270-271) fires on a wall clock that does not wait for
                // MapMagic. The ExternalSessionOwner claim in Run() is the real guard; this is the
                // belt-and-braces half, and it is worthless if it is scheduled after the hazard.
                var screenshotters = Object.FindObjectsByType<Hecton8.Tools.H8_PlayModeScreenshotter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var s in screenshotters) Object.DestroyImmediate(s.gameObject);

                var mmObj = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);

                // Wait until the scene is actually loaded and MapMagicObject is present. This wait is now
                // bounded by the timeout above and it says so out loud, because a scene with no
                // MapMagicObject at all used to look identical to a scene that was still loading.
                if (mmObj == null)
                {
                    if (updateTicks % 300 == 0)
                    {
                        Debug.Log($"[Stage2PlaymodeTest] tick {updateTicks}: still waiting for a MapMagicObject in 020_RENDER_SANDBOX_V2.");
                    }
                    return;
                }

                didDestroyInterfering = true;

                // Ensure MapMagic is active
                if (!mmObj.gameObject.activeInHierarchy)
                {
                    mmObj.gameObject.SetActive(true);
                }

                // Create a Main Camera so MapMagic knows where to generate chunks
                if (Camera.main == null)
                {
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    camGo.AddComponent<Camera>();
                    camGo.transform.position = new Vector3(500, 100, 500); // Put it where interesting terrain might be
                }
            }

            UnityEngine.Terrain[] terrains = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            bool isGenerating = false;
            var mmObject = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
            if (mmObject != null)
            {
                isGenerating = mmObject.IsGenerating();
            }

            bool allAlphamapsLoaded = true;
            if (terrains.Length == 0) allAlphamapsLoaded = false;
            foreach (var t in terrains)
            {
                if (t.terrainData == null || t.terrainData.alphamapTextureCount == 0)
                {
                    allAlphamapsLoaded = false;
                    break;
                }
            }

            if (updateTicks % 60 == 0)
            {
                File.AppendAllText(Path.Combine(outDir, "stage2_debug.txt"), $"[Tick {updateTicks}] terrains={terrains.Length}, mmObject!=null={mmObject!=null}, isGenerating={isGenerating}, allAlphamapsLoaded={allAlphamapsLoaded}\n");
            }

            // Must have 9 chunks and MapMagic must not be generating
            if (terrains.Length >= 9 && !isGenerating && allAlphamapsLoaded)
            {
                stableFrames++;
            }
            else
            {
                stableFrames = 0;
            }

            if (stableFrames > 200)
            {
                EditorApplication.update -= OnUpdate;
                CaptureAndExit(terrains);
                return; // Prevent further execution
            }
        }

        private static void CaptureAndExit(UnityEngine.Terrain[] terrains)
        {
            bool fatal = false;

            // The whole point of this tool is the splat preview. If it was not written there is no
            // splatmap evidence, and the run must not exit 0 no matter how healthy the counters look.
            bool wroteSplat = false;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("STAGE 2 PLAYMODE REPORT (Splatmaps)");
                sb.AppendLine(timedOut
                    ? "VERDICT: FAILED - timed out before 9 terrains were stable with alphamaps. Numbers " +
                      "below describe a stuck world, not a generated one."
                    : "VERDICT: PASS - 9 terrains with alphamaps stable for more than 200 frames.");
                sb.AppendLine(
                    "SCOPE: bootstrap destroyed, " +
                    (forcedTestRunnerFlag
                        ? "_isUnityTestRunnerProcess forced true, "
                        : "_isUnityTestRunnerProcess NOT forced (field missing), ") +
                    "H8_PlayModeScreenshotter destroyed and its session teardown claimed, synthetic Main " +
                    "Camera injected, scene 020_RENDER_SANDBOX_V2 rather than the shipping world scene. " +
                    "Evidence about splatmap generation only.");
                sb.AppendLine($"[x] Terrains generated: {terrains.Length}");

                UnityEngine.Terrain centerTerrain = terrains.OrderBy(t => t.transform.position.sqrMagnitude).FirstOrDefault();
                if (centerTerrain == null || centerTerrain.terrainData == null)
                {
                    // Used to fall straight through to a report with one line in it and exit 0.
                    Debug.LogError(
                        "[Stage2PlaymodeTest] FAILED: no terrain with terrainData to sample, so no splat " +
                        $"preview was produced (terrains={terrains.Length}).");
                    sb.AppendLine("[!] No terrain with terrainData to sample - no splat preview was produced.");
                }
                else
                {
                    UnityEngine.TerrainData td = centerTerrain.terrainData;
                    sb.AppendLine($"[x] Center Terrain: {centerTerrain.name}");
                    sb.AppendLine($"[x] Alphamap Resolution: {td.alphamapResolution}");
                    sb.AppendLine($"[x] Alphamap Layers: {td.alphamapLayers}");

                    if (td.alphamapLayers > 0 && td.alphamapResolution > 0)
                    {
                        try {
                            float[,,] alphas = td.GetAlphamaps(0, 0, td.alphamapResolution, td.alphamapResolution);
                            Texture2D splatPreview = new Texture2D(td.alphamapResolution, td.alphamapResolution, TextureFormat.RGB24, false);

                            Color[] layerColors = new Color[] {
                                Color.yellow, // Sand
                                Color.grey,   // Rock
                                new Color(0.4f, 0.2f, 0f), // Sediment
                                Color.white,
                                Color.green
                            };

                            for (int z = 0; z < td.alphamapResolution; z++)
                            {
                                for (int x = 0; x < td.alphamapResolution; x++)
                                {
                                    Color pixelCol = Color.black;
                                    for (int l = 0; l < td.alphamapLayers; l++)
                                    {
                                        if (l < layerColors.Length)
                                        {
                                            pixelCol += layerColors[l] * alphas[z, x, l];
                                        }
                                    }
                                    splatPreview.SetPixel(x, z, pixelCol);
                                }
                            }
                            splatPreview.Apply();

                            string splatPath = Path.Combine(outDir, "stage2_live_splat.png");
                            File.WriteAllBytes(splatPath, splatPreview.EncodeToPNG());
                            wroteSplat = true;
                            sb.AppendLine($"[x] Generated {splatPath} from live terrainData");
                        } catch (System.Exception ex) {
                            // Was swallowed into a report line while the exit code stayed 0.
                            Debug.LogError(
                                $"[Stage2PlaymodeTest] FAILED: could not read alphamaps from " +
                                $"{centerTerrain.name}, no splat preview was produced. {ex}");
                            sb.AppendLine($"[!] ERROR getting alphamaps: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogError(
                            $"[Stage2PlaymodeTest] FAILED: centre terrain {centerTerrain.name} reports " +
                            $"alphamapLayers={td.alphamapLayers} and alphamapResolution={td.alphamapResolution}, " +
                            "so there is no splatmap to preview.");
                        sb.AppendLine($"[!] WARNING: No alphamap layers or resolution is 0!");
                    }
                }

                File.WriteAllText(Path.Combine(outDir, "stage2_report.txt"), sb.ToString());
            }
            catch (System.Exception mainEx)
            {
                // `fatal` is set FIRST, before anything that can itself throw. Stage3PlaymodeTest.cs:226-227
                // sets it after the File.WriteAllText, so a missing output directory makes the catch body
                // throw, leaves fatal false, and lets the finally choose a success exit code.
                fatal = true;

                // Also to the Unity log: the file used to go to a directory no batchmode log reader ever
                // opened, and the exit code below used to be 0 regardless, so a fatal error read as a pass.
                Debug.LogError("[Stage2PlaymodeTest] FATAL during capture, the splat report is incomplete: " + mainEx);
                File.WriteAllText(Path.Combine(outDir, "stage2_fatal_error.txt"), mainEx.ToString());
            }
            finally
            {
                int exitCode = fatal ? 2 : (timedOut ? 4 : (wroteSplat ? 0 : 2));

                if (exitCode == 0)
                {
                    Debug.Log(
                        $"[Stage2PlaymodeTest] PASS: {terrains.Length} terrains with alphamaps stable for " +
                        $"more than 200 frames, splat preview written under {outDir}.");
                }
                else if (!fatal && !timedOut)
                {
                    Debug.LogError(
                        "[Stage2PlaymodeTest] FAILED: the run reached its stable state but produced no " +
                        $"splat preview, so it is not splatmap evidence. See stage2_report.txt under {outDir}.");
                }

                isRunning = false;

                // Released symmetrically with isRunning. Domain reload is disabled on play mode entry in
                // this project, so a static left set here would keep suppressing the screenshotter's
                // teardown for every later Play in the same editor session.
                if (Hecton8.Tools.H8_PlayModeScreenshotter.ExternalSessionOwner == nameof(Stage2PlaymodeTest))
                {
                    Hecton8.Tools.H8_PlayModeScreenshotter.ExternalSessionOwner = null;
                }

                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => {
                    EditorApplication.delayCall += () => {
                        EditorApplication.Exit(exitCode);
                    };
                };
            }
        }
    }
}
