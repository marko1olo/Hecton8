using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MapMagic.Editor.Diagnostics
{
    public static class Stage2PlaymodeTest
    {
        private static int stableFrames = 0;
        private static int updateTicks = 0;
        private static bool isRunning = false;
        private static bool didDestroyInterfering = false;
        private static string outDir;

        [MenuItem("Hecton8/Diagnostics/Stage 2 Playmode Splat Test")]
        public static void Run()
        {
            if (isRunning) return;
            isRunning = true;
            
            outDir = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0";

            var field = typeof(Hecton8.Bootstrap.GameBootstrapper).GetField("_isUnityTestRunnerProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (field != null) field.SetValue(null, true);

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");
            
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            if (!didDestroyInterfering)
            {
                // Destroy bootstrapper IMMEDIATELY on every tick to avoid timeouts and scene reloading
                var bootstrappers = Object.FindObjectsByType<Hecton8.Bootstrap.GameBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var b in bootstrappers) Object.DestroyImmediate(b.gameObject);

                var mmObj = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
                
                // Wait until the scene is actually loaded and MapMagicObject is present
                if (mmObj == null) return;
                
                didDestroyInterfering = true;
                // Destroy interfering screenshotters
                var screenshotters = Object.FindObjectsByType<Hecton8.Tools.H8_PlayModeScreenshotter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var s in screenshotters) Object.DestroyImmediate(s.gameObject);

                // Ensure MapMagic is active
                if (mmObj != null && !mmObj.gameObject.activeInHierarchy)
                {
                    mmObj.gameObject.SetActive(true);
                }

                // Create a Main Camera so MapMagic knows where to generate chunks
                if (Camera.main == null)
                {
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    var cam = camGo.AddComponent<Camera>();
                    camGo.transform.position = new Vector3(500, 100, 500); // Put it where interesting terrain might be
                }
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            
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

            updateTicks++;
            if (updateTicks % 60 == 0)
            {
                File.AppendAllText(Path.Combine(outDir, "stage2_debug.txt"), $"[Tick {updateTicks}] terrains={terrains.Length}, mmObject!=null={mmObject!=null}, isGenerating={isGenerating}, allAlphamapsLoaded={allAlphamapsLoaded}\n");
                
                // Force exit if stuck for too long (e.g. 6000 frames)
                if (updateTicks > 6000)
                {
                    File.AppendAllText(Path.Combine(outDir, "stage2_debug.txt"), "TIMEOUT REACHED. Exiting.\n");
                    CaptureAndExit(terrains);
                    return; // Prevent further execution
                }
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

        private static void CaptureAndExit(Terrain[] terrains)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("STAGE 2 PLAYMODE REPORT");
                sb.AppendLine($"[x] Terrains generated: {terrains.Length}");

                Terrain centerTerrain = terrains.OrderBy(t => t.transform.position.sqrMagnitude).FirstOrDefault();
                if (centerTerrain != null && centerTerrain.terrainData != null)
                {
                    TerrainData td = centerTerrain.terrainData;
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
                            sb.AppendLine($"[x] Generated stage2_live_splat.png from live terrainData");
                        } catch (System.Exception ex) {
                            sb.AppendLine($"ERROR getting alphamaps: {ex.Message}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"WARNING: No alphamap layers or resolution is 0!");
                    }
                }
                
                File.WriteAllText(Path.Combine(outDir, "stage2_report.txt"), sb.ToString());
            }
            catch (System.Exception mainEx)
            {
                File.WriteAllText(Path.Combine(outDir, "stage2_fatal_error.txt"), mainEx.ToString());
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
