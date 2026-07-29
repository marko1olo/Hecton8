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
    public static class Stage1VerifyAndRelink
    {
        [MenuItem("Hecton8/Diagnostics/Stage 1 Verify And Relink")]
        public static void Run()
        {
            // Generates MapMagic matrices AND relinks the graph, so a null graphics device means it makes
            // authoring decisions from fabricated zeros. C:\hades\.claude\rules\hecton8-shaders-compute.md
            // :36-37 bans -nographics for this class of test.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[Stage1VerifyAndRelink] REFUSED: no GPU context (graphicsDeviceType == Null). Compute " +
                    "output is all zeros here, and this task RELINKS the graph based on what it measures - " +
                    "so it would rewire the authored world from fabricated data. Remove -nographics from " +
                    "the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                DoRun();
            }
            catch (Exception ex)
            {
                // This tool RELINKS the graph, so a mid-run exception leaves the asset in an unverified
                // state. It used to write the exception to stage1_error.txt under another agent's private
                // brain directory - the same filename Stage1Check wrote - and then exit 0 from a finally
                // block, reporting success for a half-applied rewire.
                Debug.LogError(
                    "[Stage1VerifyAndRelink] FAILED mid-run; the graph may be partially relinked and is " +
                    "unverified: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void DoRun()
        {
            // Was another agent's private brain directory - outside the repo and unversioned. The per-tool
            // subfolder prevents the collision documented in Stage1Check: both tools wrote the same three
            // filenames and the same report header, so each run destroyed the other's evidence.
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "stage1_verify_relink");
            Directory.CreateDirectory(outDir);
            var sb = new StringBuilder();
            sb.AppendLine("STAGE 1 VERIFICATION REPORT");

            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);

            // 1. Relink HeightOutput if needed
            var heightOut = graph.generators.OfType<HeightOutput200>().FirstOrDefault();
            var erosionNodes = graph.generators.Where(g => g.GetType().Name.Contains("HydraulicErosion")).ToList();
            
            if (heightOut != null && erosionNodes.Count > 0)
            {
                Generator erosionNode = erosionNodes[0];
                IOutlet<object> erodedHeightOutlet = null;

                // Try casting to IMultiOutlet
                if (erosionNode is IMultiOutlet multiOutlet)
                {
                    erodedHeightOutlet = multiOutlet.Outlets().FirstOrDefault();
                }
                
                // Try reflection just in case
                var erodedOutField = erosionNode.GetType().GetField("erodedHeightOut");
                if (erodedOutField != null)
                {
                    erodedHeightOutlet = erodedOutField.GetValue(erosionNode) as IOutlet<object>;
                }

                if (erodedHeightOutlet != null)
                {
                    IInlet<object> heightInlet = heightOut as IInlet<object>;
                    
                    // Check if already linked
                    bool alreadyLinked = false;
                    if (graph.links.TryGetValue(heightInlet, out IOutlet<object> currentLink))
                    {
                        if (currentLink == erodedHeightOutlet) alreadyLinked = true;
                    }

                    if (!alreadyLinked)
                    {
                        graph.UnlinkInlet(heightInlet);
                        graph.Link(erodedHeightOutlet, heightInlet);
                        EditorUtility.SetDirty(graph);
                        AssetDatabase.SaveAssets();
                        sb.AppendLine("[x] Relinked HeightOutput to HydraulicErosion");
                    }
                }
            }

            // 2. Connections Check
            bool hasOldImports = graph.generators.Any(g => g.GetType().Name.Contains("Import"));
            sb.AppendLine($"[x] No old Import nodes: {!hasOldImports}");

            var splatNode = graph.generators.FirstOrDefault(g => g.GetType().Name.Contains("SplatmapMapMagicNode"));
            var texturesOut = graph.generators.OfType<TexturesOutput200>().FirstOrDefault();

            string heightOutSource = "NULL";
            if (heightOut != null && graph.links.TryGetValue(heightOut as IInlet<object>, out IOutlet<object> hoLink)) {
                heightOutSource = hoLink.Gen.GetType().Name;
            }
            sb.AppendLine($"[x] HeightOutput reads from: {heightOutSource}");

            string splatSource = "NULL";
            if (splatNode != null) {
                var heightIn = splatNode.GetType().GetField("heightIn")?.GetValue(splatNode) as IInlet<object>;
                if (heightIn != null && graph.links.TryGetValue(heightIn, out IOutlet<object> spLink)) {
                    splatSource = spLink.Gen.GetType().Name;
                }
            }
            sb.AppendLine($"[x] SplatmapNode heightIn reads from: {splatSource}");

            // 3. Generate Graph to get outputs
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
            Action<Generator> GatherAncestors = null;
            GatherAncestors = (node) => {
                if (!requiredNodes.Add(node)) return;
                foreach (var parent in backwardLinks[node]) GatherAncestors(parent);
            };

            if (heightOut != null) GatherAncestors(heightOut);
            if (texturesOut != null) GatherAncestors(texturesOut);
            // also gather SplatmapNode just in case
            if (splatNode != null) GatherAncestors(splatNode);

            List<Generator> topoOrder = new List<Generator>();
            HashSet<Generator> topoVisited = new HashSet<Generator>();
            Action<Generator> TopoSort = null;
            TopoSort = (node) => {
                if (topoVisited.Contains(node)) return;
                topoVisited.Add(node);
                foreach (var parent in backwardLinks[node])
                {
                    if (requiredNodes.Contains(parent)) TopoSort(parent);
                }
                topoOrder.Add(node);
            };
            foreach (var n in requiredNodes) TopoSort(n);

            Vector2D worldSize = new Vector2D(1000, 1000); 
            CoordRect rect = new CoordRect(0, 0, 512, 512);
            Area area = new Area(new Vector2D(0, 0), worldSize, rect, 0);

            TileData data = new TileData();
            data.area = area;
            data.globals = new Globals();
            // Was a bare 12000f. Same value, now named and caveated: this is the LIVE GRAPH's authored span,
            // decoded from the serialised bits of HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset
            // (lowWorldY -10000, highWorldY +2000). Do NOT swap this for DefaultVerticalSpanMeters: that is
            // the C# field initialiser at 7000 m, which matches no graph in the repo, and reading a heightmap
            // against it would make every metre value here wrong by 12000/7000 = 1.714x.
            //
            // REMAINING HAZARD, not fixed by naming the constant: this interprets the dump against the span the
            // GRAPH authored, while the terrain was generated against the span the SCENE's MapMagicObject
            // actually had. HectonMacroGeologyBaseIntegrator.cs:126-130 warns that tiles already baked at the
            // vendor default of 250 m stay 250 m until a regenerate. If that is the case for the tiles being
            // dumped, every height below is out by 12000/250 = 48x and this constant cannot detect it. The real
            // fix is to read globals.height off the MapMagicObject being dumped and refuse when it disagrees.
            data.globals.height = Hecton8.World.WorldVerticalExtentMath.LiveWorldGraphSpanMeters; 
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

            // 4. Save Hillshade + Splat preview
            MatrixWorld heightMatrix = null;
            if (heightOut != null && graph.links.TryGetValue(heightOut as IInlet<object>, out IOutlet<object> heightLink)) {
                heightMatrix = data.ReadProduct(heightLink.Id) as MatrixWorld;
            }

            if (heightMatrix != null)
            {
                Texture2D preview = new Texture2D(rect.size.x, rect.size.z, TextureFormat.RGB24, false);
                Vector3 lightDir = new Vector3(-1f, 0.5f, 1f).normalized;
                float hScale = 12000f / 1000f; 
                
                Color[] layerColors = new Color[] {
                    Color.yellow, // Sand
                    Color.grey,   // Rock
                    new Color(0.4f, 0.2f, 0f), // Sediment
                    Color.white, 
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

                        if (texturesOut != null)
                        {
                            Color splatCol = Color.black;
                            float totalWeight = 0;
                            for (int l = 0; l < texturesOut.layers.Length; l++)
                            {
                                MatrixWorld texMatrix = null;
                                if (graph.links.TryGetValue(texturesOut.layers[l] as IInlet<object>, out IOutlet<object> texLink)) {
                                    texMatrix = data.ReadProduct(texLink.Id) as MatrixWorld;
                                }
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
                                finalColor = splatCol * i;
                            }
                        }

                        preview.SetPixel(x, z, finalColor);
                    }
                }
                
                string imgPath = Path.Combine(outDir, "hillshade_splat_preview.png");
                File.WriteAllBytes(imgPath, preview.EncodeToPNG());
                sb.AppendLine($"[x] Generated hillshade_splat_preview.png");
            }

            File.WriteAllText(Path.Combine(outDir, "stage1_report.txt"), sb.ToString());
        }
    }
}
