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
    /// scatter: it destroys every <c>GameBootstrapper</c> in the scene, forces the bootstrapper's private
    /// <c>_isUnityTestRunnerProcess</c> to true by reflection, sets
    /// <c>HectonHydraulicErosionMapMagicNode.bypassSplatmapGeneration</c> to true, and destroys every
    /// <c>H8_PlayModeScreenshotter</c>. Its report therefore describes a world with no bootstrap and no
    /// splatmap generation, running in 020_RENDER_SANDBOX_V2 rather than the shipping world scene. It is
    /// evidence about scatter only, and is not a statement about the shipping game.
    ///
    /// It also calls <c>OpenScene</c> with no dirty check, so unsaved edits in currently open scenes are
    /// discarded without a prompt.
    /// </summary>
    public static class Stage3PlaymodeTest
    {
        private static int stableFrames = 0;
        private static int updateTicks = 0;
        private static bool didDestroyInterfering = false;

        // Was another agent's private brain directory - outside the repo, unversioned, and invisible to
        // anyone auditing this project's evidence. Every progress line, the report, the fatal-error dump
        // and the screenshot all went there.
        private static readonly string outDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "stage3_playmode");

        // A timeout is a failure. The old code appended "TIMEOUT REACHED" to a debug file and then took the
        // same exit path as a clean pass, so a run that never stabilised reported success.
        private static bool timedOut = false;

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

            Directory.CreateDirectory(outDir);
            stableFrames = 0;
            updateTicks = 0;
            didDestroyInterfering = false;
            timedOut = false;

            Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = true;

            var field = typeof(MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode)
                .GetField("bypassSplatmapGeneration", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (field != null) field.SetValue(null, true);

            var field_runner = typeof(Hecton8.Bootstrap.GameBootstrapper).GetField("_isUnityTestRunnerProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (field_runner != null) field_runner.SetValue(null, true);

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");
            
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            if (!didDestroyInterfering)
            {
                var bootstrappers = Object.FindObjectsByType<Hecton8.Bootstrap.GameBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var b in bootstrappers) Object.DestroyImmediate(b.gameObject);

                var mmObj = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
                if (mmObj == null) { File.AppendAllText(Path.Combine(outDir, "stage3_debug2.txt"), "mmObj is NULL! Returning.\n"); return; }
                
                didDestroyInterfering = true;
                
                var screenshotters = Object.FindObjectsByType<Hecton8.Tools.H8_PlayModeScreenshotter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var s in screenshotters) Object.DestroyImmediate(s.gameObject);

                if (mmObj != null && !mmObj.gameObject.activeInHierarchy) mmObj.gameObject.SetActive(true);

                if (Camera.main == null)
                {
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    var cam = camGo.AddComponent<Camera>();
                    camGo.transform.position = new Vector3(500, 100, 500); 
                }
            }

            UnityEngine.Terrain[] terrains = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            
            bool isGenerating = false;
            var mmObject = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
            if (mmObject != null)
            {
                isGenerating = mmObject.IsGenerating();
                if (isGenerating && updateTicks % 300 == 0) { File.AppendAllText(Path.Combine(outDir, "stage3_debug2.txt"), $"[Tick {updateTicks}] MapMagic is generating...\n"); }
            }

            updateTicks++;
            if (updateTicks % 60 == 0)
            {
                File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), $"[Tick {updateTicks}] terrains={terrains.Length}, isGenerating={isGenerating}\n");
                
                if (updateTicks > 18000)
                {
                    // A timeout means the world never reached 9 stable terrains. The capture still happens
                    // because a picture of the stuck state is useful, but the exit code must say FAILED.
                    timedOut = true;
                    string timeoutLine =
                        $"TIMEOUT after {updateTicks} ticks: terrains={terrains.Length} (needed >= 9), " +
                        $"isGenerating={isGenerating}, stableFrames={stableFrames} (needed > 200).";
                    File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), timeoutLine + "\n");
                    Debug.LogError("[Stage3PlaymodeTest] " + timeoutLine);
                    EditorApplication.update -= OnUpdate;
                    CaptureAndExit(terrains);
                    return;
                }
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
                sb.AppendLine(timedOut
                    ? "VERDICT: FAILED - timed out before 9 terrains were stable. Numbers below describe a " +
                      "stuck world, not a generated one."
                    : "VERDICT: PASS - 9 terrains stable for more than 200 frames.");
                sb.AppendLine(
                    "SCOPE: bootstrap destroyed, _isUnityTestRunnerProcess forced true, splatmap generation " +
                    "bypassed, scene 020_RENDER_SANDBOX_V2. Evidence about scatter only.");
                sb.AppendLine($"[x] Terrains generated: {terrains.Length}");

                // Find all objects
                int objectCount = 0;
                var mmObject = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
                if (mmObject != null)
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
                    if (poolsFound > 0)
                    {
                        sb.AppendLine($"[x] MapMagic scattered objects total count: {totalObjects}");
                    }
                    else
                    {
                        sb.AppendLine($"[!] MapMagic Objects Pools not found!");
                    }
                }

                // Take Screenshot
                if (Camera.main != null)
                {
                    Camera.main.transform.position = new Vector3(500, 300, 500);
                    Camera.main.transform.LookAt(new Vector3(500, 0, 500));
                    
                    int resWidth = 1024;
                    int resHeight = 1024;
                    RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
                    Camera.main.targetTexture = rt;
                    Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
                    Camera.main.Render();
                    RenderTexture.active = rt;
                    screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                    Camera.main.targetTexture = null;
                    RenderTexture.active = null; 
                    Object.DestroyImmediate(rt);
                    byte[] bytes = screenShot.EncodeToPNG();
                    string filename = Path.Combine(outDir, "stage3_scatter_screenshot.png");
                    File.WriteAllBytes(filename, bytes);
                    sb.AppendLine($"[x] Screenshot saved to {filename}");
                }

                File.WriteAllText(Path.Combine(outDir, "stage3_report.txt"), sb.ToString());
            }
            catch (System.Exception mainEx)
            {
                // Also to the Unity log: the file went to a directory no batchmode log reader ever opened,
                // and the exit code below used to be 0 regardless, so a fatal error read as a clean pass.
                Debug.LogError("[Stage3PlaymodeTest] FATAL during capture, the report is incomplete: " + mainEx);
                File.WriteAllText(Path.Combine(outDir, "stage3_fatal_error.txt"), mainEx.ToString());
                fatal = true;
            }
            finally
            {
                int exitCode = fatal ? 2 : (timedOut ? 4 : 0);
                if (exitCode == 0)
                    Debug.Log("[Stage3PlaymodeTest] PASS: 9 terrains stable for more than 200 frames.");

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

