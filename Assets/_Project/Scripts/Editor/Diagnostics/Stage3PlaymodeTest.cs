using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Diagnostics
{
    /// <summary>
    /// Scatter test that runs the sandbox render scene in play mode and waits for 9 stable terrains.
    ///
    /// READ THIS BEFORE TRUSTING ITS OUTPUT. This tool deliberately dismantles the game to isolate
    /// scatter: it destroys every <c>GameBootstrapper</c> in the scene, tries to force the bootstrapper's
    /// private <c>_isUnityTestRunnerProcess</c> to true by reflection, tries to set
    /// <c>HectonHydraulicErosionMapMagicNode.bypassSplatmapGeneration</c> to true, destroys every
    /// <c>H8_PlayModeScreenshotter</c> and claims its session teardown, force-activates the
    /// <c>MapMagicObject</c> if the scene left it disabled, injects a synthetic "Main Camera" so MapMagic
    /// has a pin to generate around, forces Burst compilation on, and runs 020_RENDER_SANDBOX_V2 rather
    /// than the shipping world scene. Its report therefore describes a world with no bootstrap, in a
    /// sandbox scene. It is evidence about scatter only, and is not a statement about the shipping game.
    /// The SCOPE line of the report now states which of those it actually established, per run - see the
    /// reflection note below for why "forced true" was not a safe thing to assert unconditionally.
    ///
    /// It also calls <c>OpenScene</c> with no dirty check, so unsaved edits in currently open scenes are
    /// discarded without a prompt.
    ///
    /// What it used to do wrong. Every one of these reported success or reported nothing at all:
    ///   - the catch in CaptureAndExit called File.WriteAllText BEFORE setting <c>fatal</c>. A missing or
    ///     unwritable outDir - the original failure mode of this whole class of tool - made the catch body
    ///     throw, so the assignment was never reached, <c>fatal</c> stayed false, and the finally computed
    ///     exit 0. A fatal capture failure was indistinguishable from a clean pass. <c>fatal</c> is now the
    ///     FIRST statement in the catch, and the dump write is separately guarded.
    ///   - <c>updateTicks++</c> sat BELOW the "waiting for a MapMagicObject" early-out, so a scene that
    ///     never produced one pinned the counter at 0, the 18000-tick timeout could never fire, no report
    ///     was ever written, and the batchmode editor hung until somebody killed it - while appending a
    ///     line to stage3_debug2.txt on every single editor tick. Ticks now advance before every early-out
    ///     and the diagnostic write is throttled.
    ///   - the <c>!EditorApplication.isPlaying</c> early-out was an unbounded bare <c>return</c>, so a play
    ///     mode that never engaged spun forever with no verdict and no exit code. It is now bounded by
    ///     PlayModeEntryTimeoutSeconds in wall clock and exits 4.
    ///   - <c>H8_PlayModeScreenshotter</c> destruction was scheduled BELOW that same early-out, i.e. after
    ///     the hazard it defends against. The screenshotter calls EditorApplication.Exit(0) on its own wall
    ///     clock at roughly PlayerWaitSeconds + SettleSeconds (H8_PlayModeScreenshotter.cs:161,175,270-271
    ///     - about 200s), and MapMagic generation here routinely outlasts that, so this run could be ended
    ///     with a SUCCESS code and no report at all. Run() now claims ExternalSessionOwner the way
    ///     H8_HeadlessPlayModeProbe.cs:421 does, and the destruction runs beside the bootstrapper's.
    ///   - the SCOPE line asserted "_isUnityTestRunnerProcess forced true, splatmap generation bypassed"
    ///     unconditionally, while both writes were guarded by <c>if (field != null)</c>. Verified against
    ///     live source: <c>bypassSplatmapGeneration</c> does not exist anywhere in this repo, so that
    ///     GetField returns null on every run and the claim was always false. See the note below.
    ///   - "MapMagic Objects Pools not found!" and a missing screenshot were report text, then exit 0. A
    ///     scatter test with no scatter count and no capture is not scatter evidence.
    ///
    /// Reflection is now verified, not assumed. <c>_isUnityTestRunnerProcess</c> is declared
    /// <c>private static readonly bool</c> inside <c>#if UNITY_INCLUDE_TESTS</c>
    /// (GameBootstrapper.cs:236-238), so FieldInfo.SetValue can be absent (the define is off in that
    /// assembly), can throw on an initonly static field, or can succeed while a consumer whose read was
    /// folded against the type's initialised value never observes it. Both writes are wrapped, read back
    /// with GetValue, and a mismatch is loud - and the SCOPE line reports the read-back result rather than
    /// the intent.
    /// </summary>
    public static class Stage3PlaymodeTest
    {
        private static int stableFrames = 0;
        private static int updateTicks = 0;
        private static bool didDestroyInterfering = false;

        // Guards against a second invocation in one editor session. Without it a second RunTest subscribes
        // OnUpdate again - EditorApplication.update is a plain delegate chain - so updateTicks advances
        // twice per editor tick, the 18000-tick budget halves, and the single `-= OnUpdate` in the exit path
        // leaves one live subscription behind. Domain reload is disabled on play mode entry in this project
        // (ProjectSettings/EditorSettings.asset:29-30, m_EnterPlayModeOptionsEnabled 1 /
        // m_EnterPlayModeOptions 1 = DisableDomainReload), so nothing else clears this between runs.
        private static bool isRunning = false;

        // Read back with GetValue after the write, so the SCOPE line states what this run established
        // rather than what it attempted.
        private static bool forcedTestRunnerFlag = false;
        private static bool bypassedSplatmapGeneration = false;
        private static bool injectedSyntheticCamera = false;

        // Bounds the wait for play mode to actually engage, in WALL CLOCK rather than ticks. updateTicks
        // only advances once EditorApplication.isPlaying is true - OnUpdate returns above the increment
        // until then - so the 18000-tick timeout cannot bound this window. DisableDomainReload (cited above)
        // is what keeps these statics and the EditorApplication.update subscription alive across play mode
        // entry, so this deadline really does get evaluated.
        private const double PlayModeEntryTimeoutSeconds = 300d;
        private static double playModeEntryDeadline = 0d;
        private static bool enteredPlayMode = false;

        // Set when the scene cannot possibly produce scatter evidence - a MapMagicObject with graph == null
        // or with an empty generator array. Exit 2, never 0, and the report says why instead of reporting
        // terrain measurements taken from an inert generator.
        private static string refusalReason = null;

        // Was another agent's private brain directory - outside the repo, unversioned, and invisible to
        // anyone auditing this project's evidence. Every progress line, the report, the fatal-error dump
        // and the screenshot all went there. The per-tool subfolder is not cosmetic: two tools that both
        // write "stage3_report.txt" into one directory destroy each other's evidence silently.
        private static readonly string outDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "stage3_playmode");

        // A timeout is a failure. The old code appended "TIMEOUT REACHED" to a debug file and then took the
        // same exit path as a clean pass, so a run that never stabilised reported success.
        private static bool timedOut = false;

        // The deliverables. A scatter test that produced neither a scatter count nor a capture is not
        // evidence, no matter how healthy the terrain counters look, so exit 0 is gated on both.
        private static bool wroteScreenshot = false;
        private static bool haveScatterCount = false;

        // Floor for "the PNG on disk is a real artifact". A 1024x1024 RGB24 capture of any populated scene is
        // tens to hundreds of KB; this only rejects a zero-byte, truncated, or otherwise absent write, which
        // is what "EncodeToPNG did not throw" fails to rule out. It does NOT prove the image is not blank -
        // that is what the no-GPU refusal at the top of RunTest is for, and a human still has to open it.
        private const long MinScreenshotBytes = 4096L;

        [MenuItem("Tools/Diagnostics/Stage3 Scatter Test")]
        public static void RunTest()
        {
            // This is a PLAY MODE test that waits on MapMagic generation.
            // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for MapMagic
            // generation tests because compute shaders and Graphics.Blit return zeros with no GPU context -
            // and Tools/BatchTasks passes exactly that flag to this method. Terrains would appear, contain
            // zeros, and the screenshot would be blank while every counter looked healthy.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[Stage3PlaymodeTest] REFUSED: no GPU context (graphicsDeviceType == Null). A play-mode " +
                    "scatter test cannot produce valid evidence here - compute output is zeros and the " +
                    "capture would be blank. Remove -nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            if (isRunning)
            {
                // No Exit() here: this branch is unreachable in batchmode (a fresh process starts with
                // isRunning false), so killing the editor over a double menu click would be pure harm. It
                // must still say something - a silent `return` is how a second invocation came to measure
                // nothing and report nothing.
                Debug.LogError(
                    "[Stage3PlaymodeTest] REFUSED: a run is already in flight (isRunning). Nothing was " +
                    "measured by this invocation and OnUpdate was NOT subscribed a second time. Wait for " +
                    "the running one to reach its verdict.");
                return;
            }

            isRunning = true;

            // Static state is reset per run. Domain reload is disabled on play mode entry here
            // (ProjectSettings/EditorSettings.asset:29-30), so nothing else clears these within one editor
            // process: without this reset a second run starts with the first run's updateTicks - tripping
            // the timeout on tick one - and its stableFrames, declaring a pass before measuring anything.
            Directory.CreateDirectory(outDir);
            stableFrames = 0;
            updateTicks = 0;
            didDestroyInterfering = false;
            timedOut = false;
            enteredPlayMode = false;
            refusalReason = null;
            wroteScreenshot = false;
            haveScatterCount = false;
            forcedTestRunnerFlag = false;
            bypassedSplatmapGeneration = false;
            injectedSyntheticCamera = false;
            playModeEntryDeadline = EditorApplication.timeSinceStartup + PlayModeEntryTimeoutSeconds;

            Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = true;

            // Claim the play-mode session before anything enters it, the same way
            // H8_HeadlessPlayModeProbe.cs:421 does. Destroying the screenshotters in OnUpdate only reaches
            // instances this tool can see at the moment it looks; this is the guard that actually holds,
            // because H8_PlayModeScreenshotter.cs:270-271 terminates the whole editor process with exit 0
            // on a wall clock (~200s per H8_PlayModeScreenshotter.cs:161,175) that does not wait for
            // MapMagic. The screenshotter still takes its capture - that is real evidence - only the
            // teardown is withheld while an owner is named.
            Hecton8.Tools.H8_PlayModeScreenshotter.ExternalSessionOwner = nameof(Stage3PlaymodeTest);

            bypassedSplatmapGeneration = TryForceStaticBool(
                typeof(MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode),
                "bypassSplatmapGeneration",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (!bypassedSplatmapGeneration)
            {
                // Not fatal - splatmaps are not what this tool measures, they are only skipped for speed and
                // isolation - but the SCOPE line must not claim a state that was never established. As of
                // this writing the field does not exist anywhere in the repo, so this warning fires on every
                // run and the old unconditional SCOPE claim was false in every report ever produced.
                Debug.LogWarning(
                    "[Stage3PlaymodeTest] bypassSplatmapGeneration was NOT set on " +
                    "HectonHydraulicErosionMapMagicNode - the public static field is absent or the write did " +
                    "not stick. Splatmap generation runs normally in this run, which is slower and is NOT " +
                    "the scope this test was written to measure. The report's SCOPE line says so.");
            }

            forcedTestRunnerFlag = TryForceStaticBool(
                typeof(Hecton8.Bootstrap.GameBootstrapper),
                "_isUnityTestRunnerProcess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (!forcedTestRunnerFlag)
            {
                Debug.LogWarning(
                    "[Stage3PlaymodeTest] _isUnityTestRunnerProcess was NOT forced true on GameBootstrapper " +
                    "- the field is compiled out (#if UNITY_INCLUDE_TESTS, GameBootstrapper.cs:236-238), " +
                    "renamed, or the initonly static write did not stick. The run continues with the " +
                    "bootstrapper in its normal mode, which is NOT the scope this test was written to " +
                    "measure. The report's SCOPE line says so.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");

            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Sets a static bool by reflection and PROVES it took, returning false if it did not.
        ///
        /// The read-back is the point. Both targets here are static fields this tool does not own, one of
        /// them <c>readonly</c> and behind a define: GetField can return null, SetValue on an initonly
        /// static field can throw FieldAccessException depending on runtime, and a consumer whose read was
        /// folded against the type's initialised value can ignore a write that technically succeeded. The
        /// old code did `if (field != null) field.SetValue(null, true);` and then asserted success in the
        /// report.
        /// </summary>
        private static bool TryForceStaticBool(System.Type owner, string fieldName, System.Reflection.BindingFlags flags)
        {
            try
            {
                System.Reflection.FieldInfo field = owner.GetField(fieldName, flags);
                if (field == null)
                    return false;

                field.SetValue(null, true);

                object readBack = field.GetValue(null);
                return readBack is bool asBool && asBool;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[Stage3PlaymodeTest] reflection write to {owner.Name}.{fieldName} threw and was not " +
                    $"applied: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                // Bounded. This used to be a bare `return`, and everything that can produce a verdict lives
                // below it, so a play mode that never engaged - a compile error blocking it, a scene that
                // fails to load, another tool leaving play mode - spun here forever and the run reported
                // nothing at all. Once play mode has been seen the deadline is disarmed, so the ordinary
                // transition out of play mode after a capture cannot trip it (OnUpdate is already
                // unsubscribed by then anyway).
                if (!enteredPlayMode && EditorApplication.timeSinceStartup > playModeEntryDeadline)
                {
                    timedOut = true;
                    string entryLine =
                        "TIMEOUT: play mode never engaged within " + PlayModeEntryTimeoutSeconds +
                        "s of RunTest(). Nothing was measured - no terrains, no scatter count, no capture.";
                    File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), entryLine + "\n");
                    Debug.LogError("[Stage3PlaymodeTest] " + entryLine);
                    EditorApplication.update -= OnUpdate;
                    CaptureAndExit(new UnityEngine.Terrain[0]);
                }
                return;
            }

            enteredPlayMode = true;

            // Ticks advance BEFORE any early-out. This increment used to sit below the setup block, and that
            // block returns early while it waits for a MapMagicObject to appear - so a scene that never
            // produced one left updateTicks pinned at 0, the timeout could never fire, no report was ever
            // written, and the process hung until somebody killed it.
            updateTicks++;

            if (updateTicks > 18000)
            {
                // A timeout means the world never reached 9 stable terrains. The capture still runs because a
                // picture of the stuck state is useful, but the exit code must say FAILED. The check has to
                // live here, above the setup early-out, or the very scenes most likely to stall are the ones
                // it cannot bound.
                timedOut = true;
                UnityEngine.Terrain[] atTimeout = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                string timeoutLine =
                    $"TIMEOUT after {updateTicks} ticks: terrains={atTimeout.Length} (needed >= 9), " +
                    $"setupComplete={didDestroyInterfering}, stableFrames={stableFrames} (needed > 200).";
                File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), timeoutLine + "\n");
                Debug.LogError("[Stage3PlaymodeTest] " + timeoutLine);
                EditorApplication.update -= OnUpdate;
                CaptureAndExit(atTimeout);
                return;
            }

            if (!didDestroyInterfering)
            {
                var bootstrappers = Object.FindObjectsByType<Hecton8.Bootstrap.GameBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var b in bootstrappers) Object.DestroyImmediate(b.gameObject);

                // Screenshotters die on every tick too, beside the bootstrappers. This used to sit BELOW the
                // "wait for a MapMagicObject" early-out, so it did not run until generation had already
                // started - while the thing it defends against (EditorApplication.Exit(0) out of
                // H8_PlayModeScreenshotter.cs:270-271) fires on a wall clock that does not wait for MapMagic.
                // The ExternalSessionOwner claim in RunTest() is the real guard; this is the belt-and-braces
                // half, and it is worthless if it is scheduled after the hazard.
                var screenshotters = Object.FindObjectsByType<Hecton8.Tools.H8_PlayModeScreenshotter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var s in screenshotters) Object.DestroyImmediate(s.gameObject);

                var mmObj = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
                if (mmObj == null)
                {
                    // Bounded by the tick timeout above, and throttled: the old code appended a line to
                    // stage3_debug2.txt on EVERY editor tick in this branch, forever, because the timeout it
                    // was waiting for could never arrive.
                    if (updateTicks % 300 == 0)
                    {
                        Debug.Log($"[Stage3PlaymodeTest] tick {updateTicks}: still waiting for a MapMagicObject in 020_RENDER_SANDBOX_V2.");
                        File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), $"[Tick {updateTicks}] no MapMagicObject yet.\n");
                    }
                    return;
                }

                if (!mmObj.gameObject.activeInHierarchy) mmObj.gameObject.SetActive(true);

                // Camera injection happens BEFORE the graph refusal below, so a refused run can still capture
                // a picture of the inert scene - same reasoning as the timeout path, a picture of the broken
                // state is real evidence and the alternative is a second misleading "Camera.main is null"
                // error stacked on top of the real cause.
                if (Camera.main == null)
                {
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    camGo.AddComponent<Camera>();
                    camGo.transform.position = new Vector3(500, 100, 500);
                    injectedSyntheticCamera = true;
                }

                // REFUSE ON AN INERT GENERATOR. 020_RENDER_SANDBOX_V2 is built by
                // Scripts/Editor/CreateSandboxV2.cs, which loads its graph from
                // Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset - a path where that asset no
                // longer exists, it was archived to Data/World/Archive/. Every run of the old CreateSandboxV2
                // therefore overwrote this scene with a MapMagicObject whose graph is null: a terrain
                // generator with no generators. MapMagicObject.Refresh (MapMagicObject.cs:236) returns
                // immediately when graph is null (MapMagicObject.cs:240), so nothing generates, and this
                // tool would burn its full 18000-tick budget and then blame a "stuck world" for a scene that
                // was never asked to produce anything. Detect it and say the real cause.
                var graph = mmObj.graph;
                if (graph == null)
                {
                    refusalReason =
                        "REFUSED: the MapMagicObject in 020_RENDER_SANDBOX_V2 has graph == null - a terrain " +
                        "generator with no generators. Nothing can scatter, so there is no scatter evidence " +
                        "to measure. The scene builder (CreateSandboxV2.cs) loads its graph from " +
                        "Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset, which was " +
                        "archived to Data/World/Archive/; a run of the old builder would have saved this scene " +
                        "empty. Restore or repoint the graph deliberately - that is a content decision.";
                    Debug.LogError("[Stage3PlaymodeTest] " + refusalReason);
                    EditorApplication.update -= OnUpdate;
                    CaptureAndExit(Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
                    return;
                }

                // Graph.generators is [NonSerialized] and rebuilt by OnAfterDeserialize (Graph.cs:22,
                // 1265-1281), which runs synchronously at asset load and nulls the array on a deserialisation
                // failure. So by the first play-mode tick a null or empty array is a real inert-graph state,
                // not a race.
                if (graph.generators == null || graph.generators.Length == 0)
                {
                    refusalReason =
                        $"REFUSED: MapMagic graph '{graph.name}' has " +
                        (graph.generators == null ? "a null generator array" : "0 generators") +
                        " - a terrain generator with no generators. Nothing can scatter, so there is no " +
                        "scatter evidence to measure. Graph.OnAfterDeserialize nulls the array when graph " +
                        "data fails to load (Graph.cs:1274-1279); check the asset, do not read terrain " +
                        "numbers out of an empty scene.";
                    Debug.LogError("[Stage3PlaymodeTest] " + refusalReason);
                    EditorApplication.update -= OnUpdate;
                    CaptureAndExit(Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
                    return;
                }

                didDestroyInterfering = true;
            }

            UnityEngine.Terrain[] terrains = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            bool isGenerating = false;
            var mmObject = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
            if (mmObject != null)
            {
                isGenerating = mmObject.IsGenerating();
            }

            if (updateTicks % 60 == 0)
            {
                File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), $"[Tick {updateTicks}] terrains={terrains.Length}, isGenerating={isGenerating}\n");
            }

            if (terrains.Length >= 9 && !isGenerating)
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
                return;
            }
        }

        private static void CaptureAndExit(UnityEngine.Terrain[] terrains)
        {
            bool fatal = false;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("STAGE 3 PLAYMODE REPORT (Scatter)");
                sb.AppendLine(refusalReason != null
                    ? "VERDICT: REFUSED - " + refusalReason
                    : timedOut
                        ? "VERDICT: FAILED - timed out before 9 terrains were stable. Numbers below describe " +
                          "a stuck world, not a generated one."
                        : "VERDICT: 9 terrains stable for more than 200 frames. See the exit code and the " +
                          "scatter/capture lines below before calling this a pass.");

                // SCOPE states what this run ESTABLISHED, not what it attempted. The old line asserted
                // "_isUnityTestRunnerProcess forced true, splatmap generation bypassed" unconditionally while
                // both writes were guarded by a null check, and omitted the screenshotter destruction, the
                // session-teardown claim, the synthetic camera and the forced Burst compilation entirely.
                // Reports from this tool have been read as describing the shipping build; every dismantling
                // step belongs on this line.
                sb.AppendLine(
                    "SCOPE: every GameBootstrapper destroyed, " +
                    (forcedTestRunnerFlag
                        ? "_isUnityTestRunnerProcess forced true (verified by read-back), "
                        : "_isUnityTestRunnerProcess NOT forced - field absent or write did not stick, ") +
                    (bypassedSplatmapGeneration
                        ? "splatmap generation bypassed (verified by read-back), "
                        : "splatmap generation NOT bypassed - field absent or write did not stick, ") +
                    "every H8_PlayModeScreenshotter destroyed and its session teardown claimed, " +
                    "MapMagicObject force-activated if the scene left it disabled, " +
                    (injectedSyntheticCamera
                        ? "synthetic Main Camera injected at (500,100,500) then moved to (500,300,500), "
                        : "scene's own Main Camera used, moved to (500,300,500), ") +
                    "Burst compilation forced on, scene 020_RENDER_SANDBOX_V2 rather than the shipping world " +
                    "scene. Evidence about scatter only; this is NOT a statement about the shipping game.");
                sb.AppendLine($"[x] Terrains generated: {terrains.Length}");

                var mmObject = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
                if (mmObject == null)
                {
                    // Used to emit no line at all - not even a warning - and still exit 0.
                    Debug.LogError(
                        "[Stage3PlaymodeTest] FAILED: no MapMagicObject in the scene at capture time, so no " +
                        "scatter count was produced. This is not scatter evidence.");
                    sb.AppendLine("[!] No MapMagicObject in the scene at capture time - no scatter count.");
                }
                else
                {
                    var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    int totalObjects = 0;
                    int poolsFound = 0;
                    foreach (var t in allTransforms)
                    {
                        if (t.name == "Objects" && t.parent != null && t.parent.name.StartsWith("Terrain"))
                        {
                            poolsFound++;
                            totalObjects += t.GetComponentsInChildren<Transform>(true).Length - 1;
                        }
                    }

                    if (poolsFound == 0)
                    {
                        // Was report text next to exit 0. The scatter count IS the payload of a scatter test.
                        Debug.LogError(
                            "[Stage3PlaymodeTest] FAILED: no MapMagic \"Objects\" pool found under any " +
                            $"Terrain* (terrains={terrains.Length}), so no scatter count was produced. This " +
                            "is not scatter evidence.");
                        sb.AppendLine("[!] MapMagic Objects Pools not found - no scatter count.");
                    }
                    else if (totalObjects == 0)
                    {
                        // Silent degeneracy, the dominant failure mode here: the pools exist and are empty, so
                        // every counter looks healthy and the measured quantity is zero.
                        Debug.LogError(
                            $"[Stage3PlaymodeTest] FAILED: {poolsFound} MapMagic \"Objects\" pool(s) found but " +
                            "they hold 0 scattered objects. Scatter did not happen; a count of zero is not " +
                            "scatter evidence.");
                        sb.AppendLine($"[!] {poolsFound} Objects pool(s) found holding 0 scattered objects.");
                    }
                    else
                    {
                        haveScatterCount = true;
                        sb.AppendLine($"[x] MapMagic scattered objects total count: {totalObjects} across {poolsFound} pool(s)");
                    }
                }

                string filename = Path.Combine(outDir, "stage3_scatter_screenshot.png");
                if (Camera.main == null)
                {
                    // Used to skip the whole block silently and exit 0 with no capture at all.
                    Debug.LogError(
                        "[Stage3PlaymodeTest] FAILED: Camera.main is null at capture time, so no screenshot " +
                        "was produced. There is no visual evidence for this run.");
                    sb.AppendLine("[!] Camera.main was null at capture time - no screenshot was produced.");
                }
                else
                {
                    Camera.main.transform.position = new Vector3(500, 300, 500);
                    Camera.main.transform.LookAt(new Vector3(500, 0, 500));

                    int resWidth = 1024;
                    int resHeight = 1024;
                    RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
                    byte[] bytes = null;
                    try
                    {
                        Camera.main.targetTexture = rt;
                        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
                        Camera.main.Render();
                        RenderTexture.active = rt;
                        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                        bytes = screenShot.EncodeToPNG();
                    }
                    finally
                    {
                        // Restored even if the readback throws. Leaving targetTexture pointing at an RT that
                        // is about to be destroyed poisons every later render on this camera.
                        if (Camera.main != null) Camera.main.targetTexture = null;
                        RenderTexture.active = null;
                        Object.DestroyImmediate(rt);
                    }

                    if (bytes == null || bytes.Length == 0)
                    {
                        Debug.LogError(
                            "[Stage3PlaymodeTest] FAILED: EncodeToPNG returned no bytes, no screenshot was " +
                            "written. There is no visual evidence for this run.");
                        sb.AppendLine("[!] EncodeToPNG returned no bytes - no screenshot was written.");
                    }
                    else
                    {
                        File.WriteAllBytes(filename, bytes);

                        // "EncodeToPNG did not throw" is not proof. Verify the artifact exists on disk and is
                        // non-trivially sized before any code path is allowed to call this run a pass.
                        var written = new System.IO.FileInfo(filename);
                        if (!written.Exists || written.Length < MinScreenshotBytes)
                        {
                            Debug.LogError(
                                $"[Stage3PlaymodeTest] FAILED: '{filename}' is missing or too small to be a " +
                                $"real 1024x1024 capture (exists={written.Exists}, " +
                                $"bytes={(written.Exists ? written.Length : 0L)}, minimum={MinScreenshotBytes}).");
                            sb.AppendLine($"[!] Screenshot at {filename} is missing or implausibly small.");
                        }
                        else
                        {
                            wroteScreenshot = true;
                            sb.AppendLine($"[x] Screenshot saved to {filename} ({written.Length} bytes)");
                        }
                    }
                }

                File.WriteAllText(Path.Combine(outDir, "stage3_report.txt"), sb.ToString());
            }
            catch (System.Exception mainEx)
            {
                // `fatal` is set FIRST, before anything that can itself throw. It used to be set AFTER the
                // File.WriteAllText below, so a missing or unwritable outDir - the original failure mode of
                // this whole class of tool - made the catch body throw before the assignment, left `fatal`
                // false, and let the finally compute exit 0. A fatal capture failure reported a clean pass.
                fatal = true;

                // Also to the Unity log: the file went to a directory no batchmode log reader ever opened,
                // and the exit code used to be 0 regardless.
                Debug.LogError("[Stage3PlaymodeTest] FATAL during capture, the report is incomplete: " + mainEx);

                try
                {
                    File.WriteAllText(Path.Combine(outDir, "stage3_fatal_error.txt"), mainEx.ToString());
                }
                catch (System.Exception dumpEx)
                {
                    // Guarded separately so a failed dump cannot escape into EditorApplication.update and
                    // cannot be confused with the original failure. The verdict is already recorded in
                    // `fatal` and in the LogError above, which is the channel that actually gets read.
                    Debug.LogError(
                        $"[Stage3PlaymodeTest] the fatal-error dump could not be written to {outDir} either: " +
                        dumpEx);
                }
            }
            finally
            {
                // Exit 0 requires BOTH deliverables. A run that stabilised 9 terrains but produced no scatter
                // count or no capture proved nothing about scatter, and must not report success.
                int exitCode =
                    fatal ? 2
                    : refusalReason != null ? 2
                    : timedOut ? 4
                    : (haveScatterCount && wroteScreenshot) ? 0
                    : 2;

                if (exitCode == 0)
                {
                    Debug.Log(
                        $"[Stage3PlaymodeTest] PASS: {terrains.Length} terrains stable for more than 200 " +
                        $"frames, scatter count and 1024x1024 capture written under {outDir}.");
                }
                else if (!fatal && !timedOut && refusalReason == null)
                {
                    Debug.LogError(
                        "[Stage3PlaymodeTest] FAILED: the run reached its stable state but did not produce " +
                        $"both deliverables (scatterCount={haveScatterCount}, screenshot={wroteScreenshot}), " +
                        $"so it is not scatter evidence. See stage3_report.txt under {outDir}.");
                }

                isRunning = false;

                // Released symmetrically with isRunning. Domain reload is disabled on play mode entry in this
                // project, so a static left set here would keep suppressing the screenshotter's teardown for
                // every later Play in the same editor session.
                if (Hecton8.Tools.H8_PlayModeScreenshotter.ExternalSessionOwner == nameof(Stage3PlaymodeTest))
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

