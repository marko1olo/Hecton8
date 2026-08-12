using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using Hecton8.World;

/// <summary>
/// Renders four PNG pairs (hillshade + slope) so the RAW macro-geology field and the MapMagic GRAPH
/// output can be compared by eye at 1024 m and 256 m windows around world (4000, 4000).
///
/// The entry point is <see cref="Execute"/> and the type deliberately stays in the global namespace:
/// Tools/BatchTasks binds editor tools by reflection name (<c>-executeMethod GraphOutputRenderer.Execute</c>),
/// so neither may change. No .bat in the tree calls it today (checked 2026-07-29 against
/// Tools/BatchTasks/*.bat), so it was driven by hand - exactly the case where the Unity log is the only
/// channel anyone reads.
///
/// WHAT WAS WRONG:
///
/// * OUTPUT WENT TO ANOTHER AGENT'S PRIVATE SCRATCH DIRECTORY. <c>OutDir</c> was hardcoded to
///   <c>C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...</c> - outside the repo, unversioned,
///   invisible to anyone auditing this project's terrain evidence, and shared with a dozen other tools in
///   this tree. There was no <c>Directory.CreateDirectory</c> anywhere, so on any machine where that folder
///   is absent the very first <c>File.WriteAllBytes</c> threw.
/// * NO GPU REFUSAL. This tool encodes PNGs and phase B reads back MapMagic-generated terrain.
///   C:\hades\.claude\rules\hecton8-shaders-compute.md:36-39 bans MapMagic/compute generation under
///   <c>-nographics</c> outright - "compute shaders and Graphics.Blit return zeros with no GPU context".
///   A run under <c>-nographics</c> produced black or all-zero images, exited 0, and a human then read the
///   artifact as a measurement of the world.
/// * FAILURE EXITED 0. <see cref="CheckGeneration"/> caught render exceptions, logged them, and then fell
///   straight through to <c>EditorApplication.Exit(0)</c> - success reported for a run that produced
///   nothing. Every other failure path used exit code 1, which is not in this instrument layer's
///   vocabulary (0 proved / 2 exception / 3 refused-no-GPU / 4 timeout), so the 300 s timeout was
///   indistinguishable from a hard error.
/// * NO ARTIFACT VERIFICATION. "EncodeToPNG did not throw" was the whole proof. Nothing checked that the
///   file existed on disk or was larger than a truncated stub.
/// * SILENT FABRICATION IN PHASE B. <c>SampleTerrainHeight</c> returned <c>-4000f</c> for any coordinate no
///   active Terrain covered, so a window MapMagic had not generated came out as a flat abyssal plain, and a
///   partly-covered window came out with a fabricated 4 km cliff. Both rendered as plausible terrain.
/// * NATIVEARRAY LEAKS. Both render methods disposed at the end of the happy path only; any throw from
///   <c>SavePNG</c> leaked 8 MiB of native memory per call.
///
/// The shading and slope math is UNCHANGED on purpose - the jobs live in
/// <see cref="HectonDiagnosticRenderer"/> and their units are documented there. Changing them would
/// silently redefine what every previously-captured image meant.
/// </summary>
public static class GraphOutputRenderer
{
    private const string ToolName = "GraphOutputRenderer";

    /// <summary>
    /// PER-TOOL subfolder inside the repo. `static readonly` and not `const` because
    /// <see cref="Path.Combine"/> is not a compile-time constant (CS0133).
    ///
    /// The subfolder is not optional bookkeeping: this tool and
    /// <see cref="HectonDiagnosticRenderer"/> previously wrote into ONE shared directory, and
    /// Logs/TerrainDiagnostics in this same repo is already shared by three tools
    /// (TerrainDiagnosticsWindow.cs:75, HeadlessTerrainDumper.cs:108, OfflineErosionBakePipeline.cs:181),
    /// which is how two tools here already destroyed each other's evidence.
    /// </summary>
    private static readonly string OutputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "Logs", "graph_output_renderer");

    private static readonly string ProvenancePath =
        Path.Combine(OutputDir, "graph_output_provenance.txt");

    /// <summary>Floor for "this is a real PNG and not a truncated stub". Matches HillshadeGen.cs:71.</summary>
    private const int MinimumPngBytes = 512;

    /// <summary>
    /// Two beauty+slope pairs for the raw field and two for the graph output. Checked at the end so a run
    /// that silently skipped a render cannot report success.
    /// </summary>
    // Three phase-A windows (30 km, 10 km, 1024 m) and two phase-B windows (1024 m, 256 m), each producing a
    // hillshade and a slope map: (3 + 2) * 2 = 10. Was 8 when phase A had two windows. This count is what
    // makes a partial run fail loudly instead of publishing a half set, so it moves whenever a window does.
    private const int ExpectedArtifactCount = 10;

    /// <summary>
    /// Below this metre spread the height field is constant for all practical purposes, which is what an
    /// all-zero readback looks like and what "no terrain was generated" looks like. Any real 16 m window of
    /// this world varies by far more than a micron.
    /// </summary>
    private const float FlatEpsilonMeters = 1e-6f;

    /// <summary>
    /// Wall-clock budget for MapMagic to finish generating.
    ///
    /// TWO WRONG DIAGNOSES ARE KEPT HERE SO THEY ARE NOT REPEATED, because both of them looked
    /// exactly like "the generator is slow" and neither of them was.
    ///
    /// (1) "The budget is too small." A run timed out at 300 s with MapMagicObject present,
    /// activeTerrains=9 and IsGenerating() still true, so the budget was raised to 1500 s. The
    /// second run failed identically at 1500 s. The same failure repeating at 5x the budget is
    /// evidence about the CONDITION, not the clock.
    ///
    /// (2) "tiles.generateInfinite = true has no finished state." True in itself - that is the
    /// streaming mode for a player walking through the world - but it was not what held this run.
    /// Measured with generateInfinite = false: IsGenerating() was still true continuously at 518 s.
    /// The fix refuted itself, which is what pointed at the real cause.
    ///
    /// The actual cause is recorded at the PumpApplyQueue call site: apply never ran, so applyReady
    /// never became true, so IsGenerating() could never fall - in any generation mode, at any budget.
    /// This constant guards a genuinely wedged generator; it has never once been the fix.
    /// </summary>
    private const double TimeoutSeconds = 1500.0;

    /// <summary>
    /// Floor on elapsed time before quiet frames are counted at all, so a cold editor cannot report
    /// "settled" during the gap before MapMagic has queued its first tile. Silence before the work
    /// starts looks exactly like silence after it ends.
    /// </summary>
    private const double SettleSeconds = 15.0;

    /// <summary>
    /// The point both phases capture, in world metres.
    ///
    /// ONE constant deliberately drives phase A (raw macro field) and phase B (MapMagic output): the
    /// entire deliverable is a pixel-for-pixel comparison, and two windows aimed at different places
    /// would produce two plausible images of two different pieces of seabed with nothing to say about
    /// the graph.
    ///
    /// WAS (4000, 4000), WHICH MEASURED NOTHING IN PHASE B. Measured 2026-08-11 once the settle bug was
    /// fixed: the sandbox scene carries 9 tiles of 500 m arranged around the ORIGIN, covering
    /// x[-500..1000] z[-500..1000], centred on (250, 250). All 1048576 samples of a 1024 m window at
    /// (4000, 4000) therefore fell outside every Terrain - it sits ~3.5 km past the far edge. Moving the
    /// Viewer object there does not drag the tiles along, because generation is bounded (see the Refresh
    /// call site): the tile set comes from the scene, not from the camera.
    ///
    /// Phase A does not care - it samples the procedural field analytically and is valid anywhere - which
    /// is exactly why this was invisible for five runs: phase A produced four healthy images of real
    /// relief every single time while phase B was pointed at empty space.
    /// </summary>
    private const float WorldCenterX = 250f;
    private const float WorldCenterZ = 250f;
    private const int Resolution = 1024;

    /// <summary>Every artifact this run verified on disk, with its byte size. Feeds the provenance file.</summary>
    private static readonly List<string> VerifiedArtifacts = new List<string>(ExpectedArtifactCount);

    /// <summary>Human-readable statistics per render, recorded next to the images.</summary>
    private static readonly List<string> RenderNotes = new List<string>(4);

    public static void Execute()
    {
        // PART 4. This tool encodes PNGs, and phase B reads back terrain MapMagic generated on the GPU.
        // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-39 - "compute shaders and Graphics.Blit
        // return zeros with no GPU context". The failure is silent: zeros render as one uniform image that
        // is indistinguishable from a real capture of flat seabed, the exit code was 0, and the artifact
        // then gets quoted as a measurement of the world.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError(
                $"[{ToolName}] REFUSED: no GPU context (graphicsDeviceType == Null). MapMagic generation " +
                "and texture readback return ZEROS here, so the raw-vs-graph comparison would be black or " +
                "uniform while looking exactly like a real capture. No PNG was written. Remove -nographics " +
                "from the batch invocation and run again.");
            EditorApplication.Exit(3);
            return;
        }

        Debug.Log($"[{ToolName}] starting. Artifacts go to {OutputDir}");

        // Static state survives an -executeMethod invocation inside a warm editor; reset it so a second
        // run cannot inherit the first run's clock or artifact list.
        startTime = 0;
        stableFrames = 0;
        lastHeartbeat = 0;
        VerifiedArtifacts.Clear();
        RenderNotes.Clear();

        try
        {
            Directory.CreateDirectory(OutputDir);

            // 1. Raw macro-geology field, no MapMagic involved.
            // WINDOWS SIZED TO THE GEOLOGY, not to a round number.
            //
            // WAS 1024 m and 256 m, and at those sizes this instrument COULD NOT SEE the thing it exists to
            // photograph. The macro field's own authored wavelengths, decoded from the graph node: descent
            // 2600 m, plate cells 2100 m, ridges 1175 m, warp 725 m, and the two noise frequencies 0.0002 and
            // 0.0001 - i.e. 5000 m and 10000 m. Not one of those fits even once inside a 1024 m frame, so
            // every macro form was off-frame and the only thing left in the picture was the finest detail,
            // which reads as uniform noise. The owner said it plainly on seeing the output: kilometre windows
            // show "just ugly noise", 10 km windows had looked good.
            //
            // 30 km is the whole world (WorldExtentMeters = 30000) and 10 km covers the two noise
            // wavelengths plus several plate cells. 1024 m is KEPT as the third window because meso-scale
            // corruption is invisible at 10 km - one artifact class needs the wide frame and the other needs
            // the close one, and dropping either is how a defect hides.
            if (!RenderRaw(WorldCenterX, WorldCenterZ, 30000f, Resolution, "A_raw_30km")) { EditorApplication.Exit(2); return; }
            if (!RenderRaw(WorldCenterX, WorldCenterZ, 10000f, Resolution, "A_raw_10km")) { EditorApplication.Exit(2); return; }
            if (!RenderRaw(WorldCenterX, WorldCenterZ, 1024f, Resolution, "A_raw_1024")) { EditorApplication.Exit(2); return; }

            // 2. Setup scene for the graph render.
            SessionState.SetBool("UpdateSandboxSceneTaskRun", true); // Block the other task from messing with scenes
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");

            var mmObject = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include);
            if (mmObject == null)
            {
                // Was: Exit(1). 1 is not in this instrument layer's exit vocabulary, and the phase A images
                // written above are NOT the artifact this tool exists to produce.
                Debug.LogError(
                    $"[{ToolName}] FAILED: no MapMagicObject in 020_RENDER_SANDBOX_V2.unity, so the B_graph_* " +
                    $"hillshade and slope images were never rendered. Only the {VerifiedArtifacts.Count} raw-field " +
                    $"images in {OutputDir} exist, and they say nothing about the graph output.");
                EditorApplication.Exit(2);
                return;
            }

            // Move MapMagic to generate at (4000, 4000). Scene-only, in-memory mutation - nothing is saved,
            // so no shipped world geometry moves.
            GameObject viewer = new GameObject("Viewer");
            viewer.transform.position = new Vector3(WorldCenterX, 0, WorldCenterZ);
            viewer.tag = "MainCamera";
            viewer.AddComponent<Camera>();

            // BOUNDED generation, not infinite.
            //
            // This used to set tiles.generateInfinite = true with generateRange = 2, which is the
            // mode for a player walking around a streaming world: MapMagic keeps pinning and
            // generating new tiles around the viewer and there is no state in which it is finished.
            // Measured 2026-08-11, twice: IsGenerating() stayed true for the whole budget - 300 s on
            // the first run and 1500 s on the second - with MapMagicObject present and 9 active
            // Terrains the entire time. Nothing was wedged and nothing was slow; the run was simply
            // waiting for a completion condition that infinite generation never reaches.
            //
            // Raising the timeout was the wrong response to the first failure and it is recorded
            // here so it is not tried a third time. A capture needs a FIXED set of tiles: the scene
            // already carries them, so generation is bounded and IsGenerating() can actually fall.
            //
            // NOT SUFFICIENT ON ITS OWN, and that matters: with generateInfinite = false the very
            // next run still showed IsGenerating=True continuously to 518 s. See PumpApplyQueue for
            // what was actually holding it. Bounding stays because a capture of a moving tile set is
            // not a measurement, not because it fixed the hang.
            mmObject.tiles.generateInfinite = false;

            // CPU apply path, chosen because a GPU one is unavailable here BY CONSTRUCTION.
            //
            // globals.heightMainApply defaults to TextureToHeightmap, which applies height via
            // Graphics.Blit into a RenderTexture and CopyActiveRenderTextureToHeightmap. That path is
            // exactly what someone was working around when they added the batchmode early-return in
            // MapMagicObject.Update (commit 3a525ee449: "CopyActiveRenderTextureToHeightmap mono
            // fatal after STARTERGRANT"). Re-enabling the apply pump without moving off that path
            // would walk this run straight back into the crash that guard was hiding.
            //
            // SetHeights is pure CPU - matrix.ExportHeights into a float[,] and TerrainData.SetHeights
            // (HeightOut.cs:148-153). Slower, and it does not stream in splits, but this tool captures
            // a fixed tile set once and then exits; there is no frame budget to protect. Draft already
            // defaults to SetHeights, so only main moves. In-memory on the component, like everything
            // else this tool touches - the asset on disk keeps TextureToHeightmap for real play.
            mmObject.globals.heightMainApply =
                MapMagic.Nodes.MatrixGenerators.HeightOutput200.ApplyType.SetHeights;

            mmObject.Refresh(true);

            Debug.Log(
                $"[{ToolName}] phase A complete ({VerifiedArtifacts.Count} raw-field images). Scene opened, " +
                $"bounded generation requested at ({WorldCenterX}, {WorldCenterZ}). Now waiting for " +
                $"{RequiredStableFrames} consecutive quiet frames, budget {TimeoutSeconds:F0}s. " +
                "Progress is reported every " + HeartbeatSeconds.ToString("F0") + "s below.");

            // -= first: a second -executeMethod invocation inside a warm editor would otherwise register the
            // same delegate twice and run the render (and Exit) twice.
            EditorApplication.update -= CheckGeneration;
            EditorApplication.update += CheckGeneration;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[{ToolName}] FAILED: the raw-vs-graph comparison was not produced in {OutputDir}. " +
                $"{VerifiedArtifacts.Count} of {ExpectedArtifactCount} images had been verified when this threw. {ex}");

            // Exiting from here can also strand live threads: Refresh(true) may already have started
            // generation before whatever threw. FindAnyObjectByType again rather than reusing the local,
            // because the throw may have come from the lines that produce it.
            StopGenerationBeforeExit(UnityEngine.Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include));
            EditorApplication.Exit(2);
        }
    }

    private static double startTime = 0;

    /// <summary>
    /// Consecutive editor frames on which MapMagic has looked finished AND the terrain has looked
    /// complete. Reset to zero the moment either check fails.
    ///
    /// A SINGLE sample of !IsGenerating() is not a completion signal. MapMagic hands work to its own
    /// ThreadManager pool and re-queues between tiles and between draft and main LODs, so there are
    /// windows - sometimes several frames wide - where nothing is in flight and generation is very
    /// much not over. Sampling once inside such a window renders half-built terrain and reports
    /// success. AGENTS.md:130 states the protocol this implements: poll via EditorApplication.update
    /// and require 200+ frames of complete silence before capturing.
    /// </summary>
    private static int stableFrames = 0;

    /// <summary>AGENTS.md:130 - "at least 200+ frames of complete silence".</summary>
    private const int RequiredStableFrames = 220;

    /// <summary>
    /// How often the wait reports what it is looking at.
    ///
    /// The previous revision logged NOTHING between the phase A images and either settling or the
    /// timeout, so a run that was killed externally, a run still generating healthily, and a run
    /// waiting on a condition that can never become true all produced the identical log: phase A
    /// artifacts, then silence. That is how the infinite-generation defect survived two 25-minute
    /// runs before being identified. An unobservable wait is not a measurement.
    /// </summary>
    private const double HeartbeatSeconds = 30.0;

    private static double lastHeartbeat = 0;

    /// <summary>
    /// Drives MapMagic's apply queue, which nothing else drives in this process.
    ///
    /// THIS IS THE FIX FOR THE HANG, and the reason four runs measured nothing. The chain:
    ///
    ///   TerrainTile.cs:765,770 - when a tile finishes generating it does NOT apply inline. It
    ///     enqueues ApplyNow / ApplyRoutine onto Den.Tools.Tasks.CoroutineManager.
    ///   TerrainTile.cs:791,848 - det.applyReady = true is set ONLY inside those two coroutines.
    ///   TerrainTile.cs:899 - IsGenerating is `generateStarted && !applyReady`. It reports on the
    ///     APPLY phase, not on the compute phase.
    ///   MapMagicObject.cs:160 - a local patch in the vendored asset returns from Update() early
    ///     `if (Application.isBatchMode)`, BEFORE its CoroutineManager.Update() call on the next line.
    ///
    /// So in batchmode the queue is never pumped, apply never runs, applyReady stays false forever,
    /// and IsGenerating() cannot fall no matter how the generator is configured or how long the run
    /// waits. That is a precise match for all four measured runs: generateInfinite=true at 300 s and
    /// at 1500 s, and generateInfinite=false at 518 s, every one of them with IsGenerating=True,
    /// activeTerrains=9 and terrainReady=True. The compute side was healthy the whole time.
    ///
    /// The patch is not removed. It was added deliberately (commit 3a525ee449) to dodge a fatal in
    /// CopyActiveRenderTextureToHeightmap, and editing a vendored asset to fix our own tool is how
    /// the next MapMagic update silently reverts this. Instead the queue is pumped from here and the
    /// apply path is moved off the GPU at the Refresh call site, so the crash that guard hides is
    /// never reached.
    ///
    /// Precedent for calling the pump by hand: Diagnostics/HeadlessRunAll.cs:76 already does it, but
    /// once, to flush a teardown - not per frame.
    /// </summary>
    private static void PumpApplyQueue()
    {
        try
        {
            Den.Tools.Tasks.CoroutineManager.Update();
        }
        catch (Exception ex)
        {
            // A throwing pump must not be swallowed into a timeout 1500 s later. CoroutineManager
            // catches per-task exceptions itself, so reaching here means the pump loop broke.
            Debug.LogError(
                $"[{ToolName}] the MapMagic apply queue pump threw, so applyReady can no longer be " +
                $"reached and this run will time out rather than settle. {ex}");
        }
    }

    /// <summary>
    /// Brings MapMagic to a halt before the editor is allowed to exit.
    ///
    /// WHY THIS EXISTS. Exiting with worker threads alive is how the previous revision died: the run
    /// finished its own work, called EditorApplication.Exit, and Unity tore down the scripting
    /// runtime underneath MapMagic's pool. Measured 2026-08-11 - fifteen consecutive
    /// "Thread shouldn't be running anymore" assertions, then "m_TaskStackToDelete.empty()", then
    /// "Setting up scripting invocation from unattached thread", then Crash. A crash during teardown
    /// is especially bad here because it happens AFTER the artifacts are written, so the images look
    /// complete while the exit code says the run died.
    ///
    /// tile.Stop() is what MapMagicObject.StopGenerate does internally; that method is private, but
    /// tiles.All() and TerrainTile.Stop() are both public, so the same shutdown is reproduced here
    /// rather than reflected into.
    /// </summary>
    private static void StopGenerationBeforeExit(MapMagicObject mmObject)
    {
        if (mmObject == null)
            return;

        try
        {
            foreach (MapMagic.Terrains.TerrainTile tile in mmObject.tiles.All())
                tile.Stop();

            // Give the pool a moment to observe the stop tokens. Purely a courtesy window: the exit
            // proceeds regardless, because hanging batchmode forever is worse than a teardown warning.
            //
            // The pump belongs INSIDE this loop. Stop tokens are observed by the apply coroutines, and
            // in batchmode nothing else advances them (see PumpApplyQueue), so without this the loop is
            // a plain five-second sleep that reports "IsGenerating=True" at the end of it and leaves
            // exactly the live threads this method exists to prevent.
            double until = EditorApplication.timeSinceStartup + 5.0;
            while (mmObject.IsGenerating() && EditorApplication.timeSinceStartup < until)
            {
                PumpApplyQueue();
                System.Threading.Thread.Sleep(50);
            }

            Debug.Log(
                $"[{ToolName}] generation stopped before exit. IsGenerating={mmObject.IsGenerating()}.");
        }
        catch (Exception ex)
        {
            // Never let shutdown bookkeeping mask the run's real outcome.
            Debug.LogWarning($"[{ToolName}] could not cleanly stop MapMagic before exit: {ex.Message}");
        }
    }

    private static void CheckGeneration()
    {
        if (startTime == 0) startTime = EditorApplication.timeSinceStartup;

        double elapsed = EditorApplication.timeSinceStartup - startTime;

        // BEFORE the timeout check, and before anything reads IsGenerating(). Nothing else in this
        // process advances MapMagic's apply queue - see PumpApplyQueue. Pumping after the timeout
        // check would still work, but pumping before it means a run that is one apply step from
        // finishing takes that step instead of being declared wedged.
        PumpApplyQueue();

        if (elapsed > TimeoutSeconds)
        {
            EditorApplication.update -= CheckGeneration;
            var mmTimeout = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>();
            // Was: Exit(1), indistinguishable from a hard error. 4 is the timeout code.
            Debug.LogError(
                $"[{ToolName}] TIMEOUT after {elapsed:F1}s waiting for MapMagic: the B_graph_* hillshade and " +
                $"slope images were never rendered to {OutputDir}. " +
                $"MapMagicObject={(mmTimeout == null ? "MISSING" : "present")}, " +
                $"IsGenerating={(mmTimeout == null ? "n/a" : mmTimeout.IsGenerating().ToString())}, " +
                $"activeTerrains={UnityEngine.Terrain.activeTerrains.Length}, " +
                $"stableFrames={stableFrames}/{RequiredStableFrames}.");
            StopGenerationBeforeExit(mmTimeout);
            EditorApplication.Exit(4);
            return;
        }

        var mmObject = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>();
        if (mmObject == null) return;

        // Terrain must be present AND carry real data. A Terrain whose terrainData is null, or whose
        // heightmap has not been applied, is an object in the scene and nothing more - counting it
        // is how a run captures an empty world and calls it a measurement.
        bool terrainReady = UnityEngine.Terrain.activeTerrains.Length > 0;
        foreach (UnityEngine.Terrain t in UnityEngine.Terrain.activeTerrains)
        {
            if (t.terrainData == null || t.terrainData.heightmapResolution <= 0)
            {
                terrainReady = false;
                break;
            }
        }

        bool isGenerating = mmObject.IsGenerating();
        bool quiet = !isGenerating && terrainReady && elapsed > SettleSeconds;
        stableFrames = quiet ? stableFrames + 1 : 0;

        if (elapsed - lastHeartbeat >= HeartbeatSeconds)
        {
            lastHeartbeat = elapsed;

            // The queue counters are the whole diagnosis, not decoration. IsGenerating=True with an
            // EMPTY coroutine queue and no threads working means apply was never enqueued or the pump
            // is not reaching it - the defect that cost four runs. IsGenerating=True with a NON-empty
            // queue means apply is genuinely in flight and the run should be left alone. Those two
            // states produced identical log lines before, which is why the first was read as the second.
            Debug.Log(
                $"[{ToolName}] waiting: {elapsed:F0}s elapsed, IsGenerating={isGenerating}, " +
                $"activeTerrains={UnityEngine.Terrain.activeTerrains.Length}, terrainReady={terrainReady}, " +
                $"coroutinesWorking={Den.Tools.Tasks.CoroutineManager.IsWorking}, " +
                $"threadsWorking={Den.Tools.Tasks.ThreadManager.IsWorking}, " +
                $"quietFrames={stableFrames}/{RequiredStableFrames}.");
        }

        if (stableFrames < RequiredStableFrames)
            return;

        {
            Debug.Log(
                $"[{ToolName}] generation settled after {elapsed:F1}s and {stableFrames} consecutive quiet " +
                $"frames with {UnityEngine.Terrain.activeTerrains.Length} active Terrains. Rendering graph output...");
            EditorApplication.update -= CheckGeneration;

            // Where the tiles actually are, logged BEFORE any sampling. "Settled with 9 active Terrains"
            // says work finished, not that it finished anywhere near the window this tool captures - the
            // first successful settle immediately missed all 1048576 samples, and the log up to that point
            // read like a clean success. One line here turns the next such run into a known coordinate
            // problem instead of a mystery.
            foreach (UnityEngine.Terrain t in UnityEngine.Terrain.activeTerrains)
            {
                if (t.terrainData == null) continue;
                Vector3 origin = t.transform.position;
                Vector3 tSize = t.terrainData.size;
                Debug.Log(
                    $"[{ToolName}] terrain '{t.name}': x[{origin.x:F1}..{origin.x + tSize.x:F1}] " +
                    $"z[{origin.z:F1}..{origin.z + tSize.z:F1}] height {tSize.y:F1} m, " +
                    $"heightmapRes={t.terrainData.heightmapResolution}.");
            }

            bool ok;
            try
            {
                // && short-circuits deliberately: if the 1024 m window failed there is nothing to learn
                // from the 256 m one, and the log already names what was not produced.
                // PHASE B CANNOT MATCH PHASE A's WIDE WINDOWS, and that asymmetry is deliberate rather than an
                // oversight. Phase A samples the field analytically, so it is valid at any size. Phase B reads
                // Terrain.SampleHeight on tiles that actually exist, and the sandbox holds nine 500 m tiles
                // spanning 1500 m total (measured: x[-500..1000] z[-500..1000]). A 10 km request would miss
                // every sample and refuse. So the macro comparison is A-only until the tile set is grown;
                // phase B's job here is the meso scale, where it is the only thing that can show what the
                // heightmap does to the field.
                ok = RenderGraphOutput(WorldCenterX, WorldCenterZ, 1024f, Resolution, "B_graph_1024")
                     && RenderGraphOutput(WorldCenterX, WorldCenterZ, 256f, Resolution, "B_graph_256");
            }
            catch (Exception ex)
            {
                // Note: previously, this caught, logged, and then fell through to Exit(0) below.
                Debug.LogError(
                    $"[{ToolName}] FAILED: the B_graph_* images were not produced in {OutputDir}. " +
                    $"{VerifiedArtifacts.Count} of {ExpectedArtifactCount} images had been verified when this threw. {ex}");
                StopGenerationBeforeExit(mmObject);
                EditorApplication.Exit(2);
                return;
            }

            if (!ok)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: graph-output rendering refused to produce an artifact (reason logged " +
                    $"above). {VerifiedArtifacts.Count} of {ExpectedArtifactCount} images verified in {OutputDir}.");
                StopGenerationBeforeExit(mmObject);
                EditorApplication.Exit(2);
                return;
            }

            if (VerifiedArtifacts.Count != ExpectedArtifactCount)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: only {VerifiedArtifacts.Count} of {ExpectedArtifactCount} images were " +
                    $"written and verified in {OutputDir}, so the raw-vs-graph comparison is incomplete. " +
                    $"Have: {string.Join(" | ", VerifiedArtifacts)}");
                StopGenerationBeforeExit(mmObject);
                EditorApplication.Exit(2);
                return;
            }

            // CheckGeneration runs as an EditorApplication.update delegate and has already unregistered
            // itself, so an exception escaping here would leave batchmode Unity hanging forever with no exit
            // code at all - worse than a wrong one. The provenance sidecar is part of the deliverable: eight
            // PNGs with no record of what they measure are exactly the artifact that gets misread.
            try
            {
                WriteProvenance(elapsed);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{ToolName}] FAILED: all {ExpectedArtifactCount} images were verified in {OutputDir} but the " +
                    $"provenance record at {ProvenancePath} could not be written, so nothing records what those " +
                    $"images measure or that a real GPU produced them. Treat the PNGs as unattributed. {ex}");
                StopGenerationBeforeExit(mmObject);
                EditorApplication.Exit(2);
                return;
            }

            Debug.Log(
                $"[{ToolName}] wrote and verified all {ExpectedArtifactCount} images in {OutputDir}. " +
                $"Provenance: {ProvenancePath}");
            StopGenerationBeforeExit(mmObject);
            EditorApplication.Exit(0);
        }
    }

    /// <summary>
    /// Renders the macro-geology field directly, with no MapMagic and no meso detail. Returns false after
    /// logging what was not produced; never throws past the native-memory cleanup.
    /// </summary>
    private static bool RenderRaw(float cx, float cz, float size, int res, string prefix)
    {
        float cellSize = size / res;
        var p = WorldMacroGeologyParams.CreateDefault(12345);

        // COLD ALLOC: NativeArray<float>[1048576] = 4 MiB + NativeArray<Color32>[1048576] = 4 MiB -
        // editor-only whole-image buffers for a 1024x1024 render - owner: GraphOutputRenderer. Not streamed
        // because the hillshade gradient needs neighbouring rows.
        var heights = new NativeArray<float>(res * res, Allocator.TempJob);
        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        try
        {
            var job = new HectonDiagnosticRenderer.HeightMapJob {
                Heights = heights, Params = p, Width = res, CellSize = cellSize,
                StartX = cx - size * 0.5f, StartZ = cz - size * 0.5f, IncludeMeso = false
            };
            job.Schedule(res * res, 64).Complete();

            // Loud probe before anything lands on disk. A constant field is what an all-zero readback and a
            // dead generator both look like, and it renders as one uniform grey image that reads as
            // "the seabed here is flat" instead of "this run measured nothing".
            if (!HeightFieldIsUsable(heights, $"{prefix} (raw macro field, cell {cellSize:F4} m)", out string stats))
                return false;
            RenderNotes.Add(stats);

            var hsJob = new HectonDiagnosticRenderer.HillshadeJob {
                Heights = heights, Colors = colors, Width = res, CellSize = cellSize,
                SunDir = math.normalize(new float3(-1f, 0.5f, -1f))
            };
            hsJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, $"{prefix}_beauty.png")) return false;

            var slopeJob = new HectonDiagnosticRenderer.SlopeMapJob {
                Heights = heights, Colors = colors, Width = res, CellSize = cellSize
            };
            slopeJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, $"{prefix}_slope.png")) return false;

            return true;
        }
        finally
        {
            // Was disposed on the happy path only: any throw out of SavePNG leaked 8 MiB per call.
            if (heights.IsCreated) heights.Dispose();
            if (colors.IsCreated) colors.Dispose();
        }
    }

    /// <summary>
    /// Samples the generated Terrains - i.e. what the MapMagic graph actually produced - and renders the
    /// same two maps so raw and graph can be compared pixel for pixel.
    /// </summary>
    private static bool RenderGraphOutput(float cx, float cz, float size, int res, string prefix)
    {
        float cellSize = size / res;

        // COLD ALLOC: NativeArray<float>[1048576] = 4 MiB + NativeArray<Color32>[1048576] = 4 MiB -
        // editor-only whole-image buffers - owner: GraphOutputRenderer.
        var heights = new NativeArray<float>(res * res, Allocator.Persistent);
        var colors = new NativeArray<Color32>(res * res, Allocator.TempJob);
        try
        {
            float startX = cx - size * 0.5f;
            float startZ = cz - size * 0.5f;

            // Sample terrains on main thread since Unity API is not thread safe.
            long missCount = 0;
            float firstMissX = 0f;
            float firstMissZ = 0f;
            for (int i = 0; i < res * res; i++)
            {
                int x = i % res;
                int z = i / res;
                float worldX = startX + x * cellSize;
                float worldZ = startZ + z * cellSize;

                if (TrySampleTerrainHeight(worldX, worldZ, out float h))
                {
                    heights[i] = h;
                }
                else
                {
                    if (missCount == 0) { firstMissX = worldX; firstMissZ = worldZ; }
                    missCount++;
                    heights[i] = -4000f; // historical abyssal-depth fallback, kept only so the buffer is defined
                }
            }

            // Was: the -4000 m fallback was returned silently. A window MapMagic had not generated came out
            // as a flat abyssal plain and a partly-covered window came out with a fabricated 4 km cliff -
            // both of which render as entirely plausible terrain. Refuse rather than fabricate.
            if (missCount > 0)
            {
                // Name WHERE the terrain actually is. The first revision of this message said "move the
                // window inside the generated area" without saying where that area was, which is an
                // instruction to go and guess - and the guess is a 25-minute run per attempt. The union of
                // the active Terrain bounds is the answer, so it gets measured and printed.
                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
                foreach (UnityEngine.Terrain t in UnityEngine.Terrain.activeTerrains)
                {
                    if (t.terrainData == null) continue;
                    Vector3 origin = t.transform.position;
                    Vector3 tSize = t.terrainData.size;
                    minX = Mathf.Min(minX, origin.x); maxX = Mathf.Max(maxX, origin.x + tSize.x);
                    minZ = Mathf.Min(minZ, origin.z); maxZ = Mathf.Max(maxZ, origin.z + tSize.z);
                }

                Debug.LogError(
                    $"[{ToolName}] FAILED: {missCount} of {(long)res * res} samples of the {size} m window at " +
                    $"({cx}, {cz}) fell outside every active Terrain (first miss at " +
                    $"({firstMissX:F2}, {firstMissZ:F2})). The old code substituted -4000 m there, which draws a " +
                    $"fabricated abyssal plain or cliff into an image a human reads as a measurement, so nothing " +
                    $"was written for {prefix}. {UnityEngine.Terrain.activeTerrains.Length} Terrains are active, " +
                    $"covering x[{minX:F1}..{maxX:F1}] z[{minZ:F1}..{maxZ:F1}], centred on " +
                    $"({(minX + maxX) * 0.5f:F1}, {(minZ + maxZ) * 0.5f:F1}) - point the window there, or raise " +
                    "MapMagicObject.tiles.generateRange to cover the window that was asked for.");
                return false;
            }

            if (!HeightFieldIsUsable(heights, $"{prefix} (MapMagic graph output, cell {cellSize:F4} m, " +
                    $"{UnityEngine.Terrain.activeTerrains.Length} Terrains)", out string stats))
                return false;
            RenderNotes.Add(stats);

            var hsJob = new HectonDiagnosticRenderer.HillshadeJob {
                Heights = heights, Colors = colors, Width = res, CellSize = cellSize,
                SunDir = math.normalize(new float3(-1f, 0.5f, -1f))
            };
            hsJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, $"{prefix}_beauty.png")) return false;

            var slopeJob = new HectonDiagnosticRenderer.SlopeMapJob {
                Heights = heights, Colors = colors, Width = res, CellSize = cellSize
            };
            slopeJob.Schedule(res * res, 64).Complete();
            if (!SavePNG(colors, res, res, $"{prefix}_slope.png")) return false;

            return true;
        }
        finally
        {
            if (heights.IsCreated) heights.Dispose();
            if (colors.IsCreated) colors.Dispose();
        }
    }

    /// <summary>
    /// Was <c>SampleTerrainHeight</c>, which returned a magic -4000 m for "no Terrain covers this point" and
    /// so was indistinguishable from a real abyssal reading. The miss is now reportable.
    /// </summary>
    private static bool TrySampleTerrainHeight(float worldX, float worldZ, out float height)
    {
        foreach (var t in UnityEngine.Terrain.activeTerrains)
        {
            if (t.terrainData == null) continue;
            Vector3 local = t.transform.InverseTransformPoint(new Vector3(worldX, 0, worldZ));
            if (local.x >= 0 && local.x <= t.terrainData.size.x && local.z >= 0 && local.z <= t.terrainData.size.z)
            {
                height = t.SampleHeight(new Vector3(worldX, 0, worldZ)) + t.transform.position.y;
                return true;
            }
        }
        height = 0f;
        return false;
    }

    /// <summary>
    /// Rejects the two ways this tool can succeed at measuring nothing: a non-finite field (a real math bug
    /// upstream, which would clamp to arbitrary bytes and render as plausible relief) and a constant field
    /// (an all-zero readback or a dead generator, which renders as flat seabed).
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

            VerifiedArtifacts.Add($"{filename} ({length} bytes)");
            Debug.Log($"[{ToolName}] verified {filename} ({length} bytes) at {path}");
            return true;
        }
        finally
        {
            // Was leaked on the exception path.
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    /// <summary>
    /// Records what the images actually are next to the images, so their limits travel with them instead of
    /// being rediscovered - or not - by whoever opens the PNGs next.
    /// </summary>
    private static void WriteProvenance(double elapsedSeconds)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine($"{ToolName} - raw macro field vs MapMagic graph output");
        text.AppendLine($"generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine($"graphicsDeviceType: {SystemInfo.graphicsDeviceType}");
        text.AppendLine($"unity: {Application.unityVersion}");
        text.AppendLine($"scene: Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");
        text.AppendLine($"window center: ({WorldCenterX}, {WorldCenterZ}) world metres, {Resolution}x{Resolution} px");
        text.AppendLine($"macro geology seed: 12345");
        text.AppendLine($"sun direction: normalize(-1, 0.5, -1)");
        text.AppendLine($"MapMagic settle time: {elapsedSeconds:F1}s, activeTerrains={UnityEngine.Terrain.activeTerrains.Length}");
        text.AppendLine();
        text.AppendLine("MEASURED HEIGHT FIELDS:");
        for (int i = 0; i < RenderNotes.Count; i++) text.AppendLine($"* {RenderNotes[i]}");
        text.AppendLine();
        text.AppendLine("ARTIFACTS (all verified to exist and exceed the truncation floor):");
        for (int i = 0; i < VerifiedArtifacts.Count; i++) text.AppendLine($"* {VerifiedArtifacts[i]}");
        text.AppendLine();
        text.AppendLine("READ THIS BEFORE QUOTING AN IMAGE AS A MEASUREMENT:");
        text.AppendLine("* A_raw_* is the macro-geology field sampled directly with meso detail OFF. It is NOT");
        text.AppendLine("  what the player sees; it is the input the MapMagic graph starts from.");
        text.AppendLine("* B_graph_* samples Terrain.SampleHeight on whatever MapMagic generated, which in the");
        text.AppendLine("  editor may still be DRAFT resolution. Compare shape, not fine detail.");
        text.AppendLine("* the *_slope.png mapping is 1 - normal.y scaled by 2 and is NOT degrees. See");
        text.AppendLine("  HectonDiagnosticRenderer.SlopeMapJob. Judge relative steepness only.");
        text.AppendLine("* border pixels use a one-sided gradient (the neighbour is the centre sample), so the");
        text.AppendLine("  outermost row and column read flatter than they are.");

        File.WriteAllText(ProvenancePath, text.ToString());
    }
}
