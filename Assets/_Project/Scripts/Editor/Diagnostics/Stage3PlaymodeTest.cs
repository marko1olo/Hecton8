using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Diagnostics
{
    public static class Stage3PlaymodeTest
    {
        private static int stableFrames = 0;
        private static int updateTicks = 0;
        private static bool didDestroyInterfering = false;
        private static string outDir = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0";

        [MenuItem("Tools/Diagnostics/Stage3 Scatter Test")]
        public static void RunTest()
        {
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
                    File.AppendAllText(Path.Combine(outDir, "stage3_debug.txt"), "TIMEOUT REACHED. Exiting.\n");
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
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("STAGE 3 PLAYMODE REPORT (Scatter)");
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
                File.WriteAllText(Path.Combine(outDir, "stage3_fatal_error.txt"), mainEx.ToString());
            }
            finally
            {
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => {
                    EditorApplication.delayCall += () => {
                        EditorApplication.Exit(0);
                    };
                };
            }
        }
    }
}

