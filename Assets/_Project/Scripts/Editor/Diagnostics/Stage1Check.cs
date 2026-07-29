using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Core;
using MapMagic.Terrains;
using MapMagic.Products;
using Den.Tools;
using Den.Tools.Matrices;

namespace MapMagic.Editor.Diagnostics
{
    public static class Stage1Check
    {
        [MenuItem("Hecton8/Diagnostics/Stage 1 Check")]
        public static void Run()
        {
            // Generates MapMagic matrices, so a null graphics device turns every number it verifies into a
            // zero: C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for this class
            // of test precisely because compute shaders and Graphics.Blit return zeros with no GPU context.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[Stage1Check] REFUSED: no GPU context (graphicsDeviceType == Null). This verification " +
                    "would pass or fail on fabricated zeros. Remove -nographics from the batch invocation " +
                    "and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                DoRun();
            }
            catch (Exception ex)
            {
                // The exception used to go to stage1_error.txt under another agent's private brain
                // directory - the SAME filename Stage1VerifyAndRelink wrote, so the two tools clobbered
                // each other's error file - and the finally block exited 0 regardless.
                Debug.LogError("[Stage1Check] FAILED, no verification report was produced: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void DoRun()
        {
            // Was another agent's private brain directory - outside the repo and unversioned. The
            // per-tool subfolder is not cosmetic: Stage1Check and Stage1VerifyAndRelink wrote the SAME
            // three filenames (stage1_error.txt, stage1_report.txt, hillshade_splat_preview.png) into the
            // same directory, and both emit a report whose first line is "STAGE 1 VERIFICATION REPORT".
            // Running either destroyed the other's evidence with no way to tell from the file which tool
            // produced it.
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "stage1_check");
            Directory.CreateDirectory(outDir);
            var sb = new StringBuilder();
            sb.AppendLine("STAGE 1 VERIFICATION REPORT");

            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);

            // 1. Check old Import
            bool hasOldImports = graph.generators.Any(g => g.GetType().Name.Contains("Import"));
            sb.AppendLine($"[ ] No old Import nodes: {!hasOldImports}");

            var heightOut = graph.generators.OfType<HeightOutput200>().FirstOrDefault();
            var splatNode = graph.generators.FirstOrDefault(g => g.GetType().Name.Contains("SplatmapMapMagicNode"));
            var texturesOut = graph.generators.OfType<TexturesOutput200>().FirstOrDefault();

            string heightOutSource = "NULL";
            if (heightOut != null && graph.links.TryGetValue(heightOut as IInlet<object>, out IOutlet<object> hoLink)) {
                heightOutSource = hoLink.Gen.GetType().Name;
            }
            sb.AppendLine($"[ ] HeightOutput reads from: {heightOutSource} (Expected: HydraulicErosion or AbyssalShelf)");

            string splatSource = "NULL";
            if (splatNode != null) {
                var heightIn = splatNode.GetType().GetField("heightIn")?.GetValue(splatNode) as IInlet<object>;
                if (heightIn != null && graph.links.TryGetValue(heightIn, out IOutlet<object> spLink)) {
                    splatSource = spLink.Gen.GetType().Name;
                }
            }
            sb.AppendLine($"[ ] SplatmapNode heightIn reads from: {splatSource} (Expected: HydraulicErosion or AbyssalShelf)");

            // Topological Sort to generate everything
            Dictionary<Generator, List<Generator>> backwardLinks = new Dictionary<Generator, List<Generator>>();
            foreach (var g in graph.generators) backwardLinks[g] = new List<Generator>();

            foreach (var kvp in graph.links)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    Generator child = kvp.Key.Gen;
                    Generator parent = kvp.Value.Gen;
                    if (child != null && parent != null && backwardLinks.ContainsKey(child))
                    {
                        backwardLinks[child].Add(parent);
                    }
                }
            }

            HashSet<Generator> requiredNodes = new HashSet<Generator>();
            void GatherAncestors(Generator node)
            {
                if (!requiredNodes.Add(node)) return;
                foreach (var parent in backwardLinks[node]) GatherAncestors(parent);
            }
            if (heightOut != null) GatherAncestors(heightOut);
            if (texturesOut != null) GatherAncestors(texturesOut);

            List<Generator> topoOrder = new List<Generator>();
            HashSet<Generator> topoVisited = new HashSet<Generator>();
            void TopoSort(Generator node)
            {
                if (topoVisited.Contains(node)) return;
                topoVisited.Add(node);
                foreach (var parent in backwardLinks[node])
                {
                    if (requiredNodes.Contains(parent)) TopoSort(parent);
                }
                topoOrder.Add(node);
            }
            foreach (var n in requiredNodes) TopoSort(n);

            // Generate
            Vector2D worldSize = new Vector2D(1000, 1000); // 1km tile
            CoordRect rect = new CoordRect(0, 0, 512, 512);
            Area area = new Area(new Vector2D(0, 0), worldSize, rect, 0);

            TileData data = new TileData();
            data.area = area;
            data.globals = new Globals();
            data.globals.height = 12000f; 
            data.random = new Den.Tools.Noise(12345);
            data.ClearProducts();

            foreach (var gen in topoOrder)
            {
                try {
                    gen.Generate(data, new StopToken());
                } catch (Exception e) {
                    sb.AppendLine($"ERROR generating {gen.GetType().Name}: {e.Message}");
                }
            }

            // Extract height and save Hillshade + Splat preview
            MatrixWorld heightMatrix = null;
            if (heightOut != null) {
                heightMatrix = data.ReadInletProduct(heightOut);
            }

            if (heightMatrix != null)
            {
                Texture2D preview = new Texture2D(rect.size.x, rect.size.z, TextureFormat.RGB24, false);
                Vector3 lightDir = new Vector3(-1f, 0.5f, 1f).normalized;
                float hScale = 12000f / 1000f; // amplitude / size
                
                Color[] layerColors = new Color[] {
                    Color.yellow, // Sand
                    Color.grey,   // Rock
                    new Color(0.4f, 0.2f, 0f), // Sediment/Mud
                    Color.white,  // Default
                    Color.green
                };

                for (int z = 1; z < rect.size.z - 1; z++)
                {
                    for (int x = 1; x < rect.size.x - 1; x++)
                    {
                        float hL = heightMatrix[x - 1, z] * hScale;
                        float hR = heightMatrix[x + 1, z] * hScale;
                        float hD = heightMatrix[x, z - 1] * hScale;
                        float hU = heightMatrix[x, z + 1] * hScale;
                        Vector3 n = new Vector3(hL - hR, 2f, hD - hU).normalized;
                        float i = Mathf.Max(0f, Vector3.Dot(n, lightDir));
                        
                        Color finalColor = new Color(i, i, i, 1f);

                        // If textures output is valid, overlay
                        if (texturesOut != null)
                        {
                            Color splatCol = Color.black;
                            float totalWeight = 0;
                            for (int l = 0; l < texturesOut.layers.Length; l++)
                            {
                                MatrixWorld texMatrix = data.ReadInletProduct(texturesOut.layers[l]);
                                if (texMatrix != null)
                                {
                                    float w = texMatrix[x, z];
                                    splatCol += layerColors[l % layerColors.Length] * w;
                                    totalWeight += w;
                                }
                            }
                            if (totalWeight > 0)
                            {
                                splatCol /= totalWeight;
                                // Multiply splat color with hillshade
                                finalColor = splatCol * i;
                            }
                        }

                        preview.SetPixel(x, z, finalColor);
                    }
                }
                
                string imgPath = Path.Combine(outDir, "hillshade_splat_preview.png");
                File.WriteAllBytes(imgPath, preview.EncodeToPNG());
                sb.AppendLine($"[ ] Generated hillshade_splat_preview.png (Size: {rect.size.x}x{rect.size.z})");
            }

            File.WriteAllText(Path.Combine(outDir, "stage1_report.txt"), sb.ToString());
        }
    }
}
