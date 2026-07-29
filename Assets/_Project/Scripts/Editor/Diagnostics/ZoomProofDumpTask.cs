using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class ZoomProofDumpTask
    {
        [MenuItem("Hecton8/Diagnostics/Zoom Proof Dump")]
        public static void Dump()
        {
            // Tools/BatchTasks invokes this with -nographics, which C:\hades\.claude\rules\
            // hecton8-shaders-compute.md:36-37 bans outright for MapMagic/compute generation tests:
            // "compute shaders and Graphics.Blit return zeros with no GPU context". A zoom proof made of
            // zeros looks exactly like a zoom proof made of real heights, so refuse instead of producing
            // one. The old code additionally exited 0 from a finally block after writing the exception to
            // another agent's private brain directory, so every failure reported success invisibly.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[ZoomProofDumpTask] REFUSED: no GPU context (graphicsDeviceType == Null). Compute " +
                    "shaders and Graphics.Blit return ZEROS here, so this dump would be indistinguishable " +
                    "from a real one while being entirely fabricated. Remove -nographics from the batch " +
                    "invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                DoDump();
            }
            catch (Exception ex)
            {
                Debug.LogError("[ZoomProofDumpTask] FAILED, no dump was written: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static Color GetHeightColor(float h)
        {
            // Make invalid data LOUD, not invisible: NaN = magenta, +Inf = pure red, -Inf = pure blue.
            if (float.IsNaN(h)) return new Color(1f, 0f, 1f, 1f);
            if (float.IsPositiveInfinity(h)) return new Color(1f, 0f, 0f, 1f);
            if (float.IsNegativeInfinity(h)) return new Color(0f, 0f, 1f, 1f);
            if (h < -4000f) return Color.Lerp(new Color(0, 0, 0.2f, 1f), new Color(0, 0, 0.5f, 1f), Mathf.InverseLerp(-6000f, -4000f, h));
            if (h < -2000f) return Color.Lerp(new Color(0, 0, 0.5f, 1f), new Color(0.2f, 0.4f, 0.8f, 1f), Mathf.InverseLerp(-4000f, -2000f, h));
            if (h < -500f) return Color.Lerp(new Color(0.2f, 0.4f, 0.8f, 1f), new Color(0.4f, 0.8f, 0.9f, 1f), Mathf.InverseLerp(-2000f, -500f, h));
            if (h < 0f) return Color.Lerp(new Color(0.4f, 0.8f, 0.9f, 1f), new Color(0.1f, 0.8f, 0.6f, 1f), Mathf.InverseLerp(-500f, 0f, h));
            if (h < 500f) return Color.Lerp(new Color(0.1f, 0.8f, 0.6f, 1f), new Color(0.8f, 0.9f, 0.2f, 1f), Mathf.InverseLerp(0f, 500f, h));
            if (h < 2000f) return Color.Lerp(new Color(0.8f, 0.9f, 0.2f, 1f), new Color(0.8f, 0.4f, 0.1f, 1f), Mathf.InverseLerp(500f, 2000f, h));
            return Color.Lerp(new Color(0.8f, 0.4f, 0.1f, 1f), new Color(1f, 1f, 1f, 1f), Mathf.InverseLerp(2000f, 4000f, h));
        }

        // Compact finite-only statistics over a MatrixWorld, height in meters (arr * 12000).
        private static string ComputeStatsLine(MatrixWorld mx, string label)
        {
            if (mx == null || mx.arr == null) return $"{label}: <null product>";
            float minH = float.MaxValue, maxH = float.MinValue;
            double sum = 0, sumSq = 0;
            long finite = 0, nan = 0, inf = 0;
            for (int i = 0; i < mx.arr.Length; i++)
            {
                float h = mx.arr[i] * 12000f;
                if (float.IsNaN(h)) { nan++; continue; }
                if (float.IsInfinity(h)) { inf++; continue; }
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
                sum += h; sumSq += (double)h * h; finite++;
            }
            double mean = finite > 0 ? sum / finite : 0;
            double var = finite > 0 ? (sumSq / finite) - mean * mean : 0;
            double std = var > 0 ? Math.Sqrt(var) : 0;
            return $"{label}: min={minH:F1} max={maxH:F1} mean={mean:F1} std={std:F1}  NaN={nan} Inf={inf}";
        }

        // Read the product feeding a node's inlet (pre-node) given the graph link table.
        private static MatrixWorld ReadInletProduct(Graph graph, TileData data, Generator node)
        {
            if (node == null) return null;
            IInlet<object> inlet = node as IInlet<object>;
            if (inlet == null)
            {
                var f = node.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(x => typeof(IInlet<object>).IsAssignableFrom(x.FieldType));
                if (f != null) inlet = f.GetValue(node) as IInlet<object>;
            }
            if (inlet == null) return null;
            IOutlet<object> linked = null;
            foreach (var kvp in graph.links) { if (kvp.Key == inlet) { linked = kvp.Value; break; } }
            if (!(linked is IUnit u)) return null;
            return data.ReadProduct(u.Id) as MatrixWorld;
        }

        private static void DoDump()
        {
            // Was another agent's private brain directory: outside the repo, unversioned, invisible to
            // anyone auditing this project's terrain evidence. Logs/ already holds every route artifact.
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(outDir);
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
            if (graph == null) throw new Exception("Could not find graph");

            // We trace from HeightOutput200
            Generator endNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HeightOutput200");
            if (endNode == null) throw new Exception("Could not find HeightOutput200");

            // Isolation probe: hydro-erosion node, so we can measure terrain statistics
            // BEFORE erosion (at its inlet) vs the final AFTER value. Non-destructive.
            Generator erosionNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HectonHydraulicErosionMapMagicNode");

            Dictionary<Generator, List<Generator>> backwardLinks = new Dictionary<Generator, List<Generator>>();
            foreach (var g in graph.generators) backwardLinks[g] = new List<Generator>();

            foreach (var kvp in graph.links)
            {
                if (kvp.Key != null && kvp.Value != null && kvp.Key.Gen != null && kvp.Value.Gen != null)
                {
                    if (backwardLinks.ContainsKey(kvp.Key.Gen))
                        backwardLinks[kvp.Key.Gen].Add(kvp.Value.Gen);
                }
            }

            HashSet<Generator> requiredNodes = new HashSet<Generator>();
            void GatherAncestors(Generator node)
            {
                if (!requiredNodes.Add(node)) return;
                if (backwardLinks.ContainsKey(node))
                {
                    foreach (var parent in backwardLinks[node]) GatherAncestors(parent);
                }
            }
            GatherAncestors(endNode);

            List<Generator> topoOrder = new List<Generator>();
            HashSet<Generator> topoVisited = new HashSet<Generator>();
            void TopoSort(Generator node)
            {
                if (topoVisited.Contains(node)) return;
                topoVisited.Add(node);
                foreach (var parent in backwardLinks[node])
                {
                    if (parent != null && requiredNodes.Contains(parent)) TopoSort(parent);
                }
                topoOrder.Add(node);
            }
            foreach (var n in requiredNodes) TopoSort(n);

            // Generate the graph height matrix for an arbitrary world window and return
            // the MatrixWorld feeding HeightOutput200. Used by both the world census and
            // the per-zoom dump so both see the identical pipeline.
            MatrixWorld GenerateHeight(Vector2D worldPos, Vector2D worldSize)
            {
                CoordRect rect = new CoordRect(0, 0, 1024, 1024);
                Area area = new Area(worldPos, worldSize, rect, 0);
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
                StopToken stop = new StopToken();
                foreach (var gen in topoOrder) gen.Generate(data, stop);

                IInlet<object> hoInlet = endNode as IInlet<object>;
                if (hoInlet == null)
                {
                    var f = endNode.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        .FirstOrDefault(x => typeof(IInlet<object>).IsAssignableFrom(x.FieldType));
                    if (f != null) hoInlet = f.GetValue(endNode) as IInlet<object>;
                }
                if (hoInlet == null) return null;
                IOutlet<object> outl = null;
                foreach (var kvp in graph.links) { if (kvp.Key == hoInlet) { outl = kvp.Value; break; } }
                if (!(outl is IUnit uo)) return null;
                return data.ReadProduct(uo.Id) as MatrixWorld;
            }

            // ---- WORLD CENSUS: sample several far-apart regions at RUNTIME resolution ----
            // Runtime heightmap has one vertex every ~1.953 m (1000m tile / 512). A 2000m
            // window at 1024px = 1.953 m/px = TRUE runtime detail. Judging an infinite
            // procedural world by a single point is invalid; this measures variety.
            {
                string censusPath = Path.Combine(outDir, "world_census.txt");
                Vector2D[] regions = new Vector2D[]
                {
                    new Vector2D(5000, 5000),
                    new Vector2D(50000, 50000),
                    new Vector2D(-40000, 15000),
                    new Vector2D(120000, -80000),
                    new Vector2D(-200000, -200000),
                    new Vector2D(300000, 90000),
                    new Vector2D(-15000, 250000),
                    new Vector2D(777000, -333000)
                };
                Vector2D censusWin = new Vector2D(2000, 2000); // runtime resolution window
                using (StreamWriter cw = new StreamWriter(censusPath))
                {
                    cw.WriteLine("WORLD CENSUS — 2km window @ 1.953 m/px (runtime resolution)");
                    cw.WriteLine("Slope buckets: 0-15 / 15-40 / 40-70 / 70+ degrees");
                    cw.WriteLine("--------------------------------------------------");
                    foreach (var r in regions)
                    {
                        Vector2D wp = new Vector2D(r.x - censusWin.x / 2, r.z - censusWin.z / 2);
                        MatrixWorld cmx = GenerateHeight(wp, censusWin);
                        if (cmx == null || cmx.arr == null)
                        {
                            cw.WriteLine($"Region ({r.x},{r.z}): <null product>");
                            continue;
                        }
                        int cw_res = cmx.rect.size.x;
                        float cpx = (float)censusWin.x / Mathf.Max(1, cw_res);
                        long[] sb = new long[4];
                        for (int z = 0; z < cw_res; z++)
                        for (int x = 0; x < cw_res; x++)
                        {
                            float hc = cmx.arr[z * cw_res + x] * 12000f;
                            if (float.IsNaN(hc) || float.IsInfinity(hc)) continue;
                            float hl = cmx.arr[z * cw_res + Mathf.Max(0, x - 1)] * 12000f;
                            float hr = cmx.arr[z * cw_res + Mathf.Min(cw_res - 1, x + 1)] * 12000f;
                            float hd = cmx.arr[Mathf.Max(0, z - 1) * cw_res + x] * 12000f;
                            float hu = cmx.arr[Mathf.Min(cw_res - 1, z + 1) * cw_res + x] * 12000f;
                            Vector3 nn = new Vector3(hl - hr, 2f * cpx, hd - hu).normalized;
                            float sd = Vector3.Angle(Vector3.up, nn);
                            if (sd < 15f) sb[0]++; else if (sd < 40f) sb[1]++; else if (sd < 70f) sb[2]++; else sb[3]++;
                        }
                        long tot = (long)cw_res * cw_res;
                        double P(int b) => tot > 0 ? 100.0 * sb[b] / tot : 0.0;
                        cw.WriteLine($"Region ({r.x},{r.z}):  {ComputeStatsLine(cmx, "H")}");
                        cw.WriteLine($"   Slope %: 0-15={P(0):F1}  15-40={P(1):F1}  40-70={P(2):F1}  70+={P(3):F1}");
                    }
                }
            }

            float[] sizes = new float[] { 10000f, 2000f, 500f, 100f, 20f };
            string[] names = new string[] { "10km", "2km", "500m", "100m", "20m" };

            // X=5000, Z=5000 is more diverse
            Vector2D centerPos = new Vector2D(5000, 5000);

            string reportPath = Path.Combine(outDir, "zoom_proof_report.txt");
            using (StreamWriter writer = new StreamWriter(reportPath))
            {
                writer.WriteLine("MultiMap Diagnostic Report");
                writer.WriteLine($"Center: X={centerPos.x}, Z={centerPos.z}");
                writer.WriteLine("--------------------------------------------------");

                for (int i = 0; i < sizes.Length; i++)
                {
                    float size = sizes[i];
                    string name = names[i];
                    
                    Vector2D worldPos = new Vector2D(centerPos.x - size / 2, centerPos.z - size / 2);
                    Vector2D worldSize = new Vector2D(size, size);
                    CoordRect rect = new CoordRect(0, 0, 1024, 1024);
                    Area area = new Area(worldPos, worldSize, rect, 0); // MARGINS 0 to prevent 3072 array size

                    TileData data = new TileData();
                    data.area = area;
                    data.globals = new Globals();
                    // Second site, same constant and same caveat as the first in this file.
                    data.globals.height = Hecton8.World.WorldVerticalExtentMath.LiveWorldGraphSpanMeters; // Globals.height
                    data.random = new Den.Tools.Noise(12345);
                    data.ClearProducts();

                    StopToken stop = new StopToken();

                    foreach (var gen in topoOrder)
                    {
                        gen.Generate(data, stop);
                    }

                    // For HeightOutput200, it's an Output node, but we need the MatrixWorld right BEFORE it
                    // The Inlet of HeightOutput200 is connected to the final graph mix.
                    // We can just get the MatrixWorld generated by the node connected to its inlet.
                    
                    IInlet<object> inlet = null;
                    if (endNode is IInlet<object> directInlet) inlet = directInlet;
                    else
                    {
                        var inletField = endNode.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                            .FirstOrDefault(f => typeof(IInlet<object>).IsAssignableFrom(f.FieldType));
                        if (inletField != null) inlet = inletField.GetValue(endNode) as IInlet<object>;
                    }

                    if (inlet == null) throw new Exception("Could not find inlet on HeightOutput200");

                    IOutlet<object> linkedOutlet = null;
                    foreach (var kvp in graph.links)
                    {
                        if (kvp.Key == inlet)
                        {
                            linkedOutlet = kvp.Value;
                            break;
                        }
                    }

                    if (linkedOutlet == null) throw new Exception("HeightOutput200 is not connected to anything");
                    
                    IUnit unitOut = linkedOutlet as IUnit;
                    if (unitOut == null) throw new Exception("Linked outlet is not IUnit");

                    MatrixWorld mx = data.ReadProduct(unitOut.Id) as MatrixWorld;
                    if (mx == null) throw new Exception("Product is not MatrixWorld");

                    int resX = mx.rect.size.x;
                    int resZ = mx.rect.size.z;
                    int expectedLen = resX * resZ;
                    int actualLen = mx.arr != null ? mx.arr.Length : 0;
                    // pixelSize derived from the ACTUAL matrix width, not a hardcoded 1024,
                    // so slope/curvature units stay in meters even if MapMagic returns a
                    // non-1024 matrix.
                    float pixelSize = size / Mathf.Max(1, resX);

                    // RGBA32 with explicit alpha=1 everywhere. Any pixel we fail to write
                    // stays at the cleared default and will be visible as a hole rather
                    // than silently blending; NaN/Inf are painted magenta on purpose.
                    Texture2D texHeight = new Texture2D(resX, resZ, TextureFormat.RGBA32, false);
                    Texture2D texHill = new Texture2D(resX, resZ, TextureFormat.RGBA32, false);
                    Texture2D texSlope = new Texture2D(resX, resZ, TextureFormat.RGBA32, false);
                    Texture2D texNormal = new Texture2D(resX, resZ, TextureFormat.RGBA32, false);
                    Texture2D texCurv = new Texture2D(resX, resZ, TextureFormat.RGBA32, false);

                    Color magenta = new Color(1f, 0f, 1f, 1f);
                    Vector3 lightDir = new Vector3(-1f, 0.5f, 1f).normalized;

                    // ---- Pass 1: statistics over finite values only ----
                    float minH = float.MaxValue;
                    float maxH = float.MinValue;
                    double sum = 0.0;
                    double sumSq = 0.0;
                    long finiteCount = 0;
                    long nanCount = 0;
                    long infCount = 0;

                    if (actualLen < expectedLen)
                        throw new Exception($"Matrix array too small: arr.Length={actualLen} < resX*resZ={expectedLen} (resX={resX}, resZ={resZ}). Indexing would read out of bounds — this is the tool bug, not the terrain.");

                    for (int idx = 0; idx < expectedLen; idx++)
                    {
                        float h = mx.arr[idx] * 12000f;
                        if (float.IsNaN(h)) { nanCount++; continue; }
                        if (float.IsInfinity(h)) { infCount++; continue; }
                        if (h < minH) minH = h;
                        if (h > maxH) maxH = h;
                        sum += h;
                        sumSq += (double)h * h;
                        finiteCount++;
                    }

                    double mean = finiteCount > 0 ? sum / finiteCount : 0.0;
                    double variance = finiteCount > 0 ? (sumSq / finiteCount) - (mean * mean) : 0.0;
                    double std = variance > 0.0 ? Math.Sqrt(variance) : 0.0;

                    // Slope histogram buckets: 0-15, 15-40, 40-70, 70+ degrees.
                    long[] slopeBuckets = new long[4];

                    // ---- Pass 2: paint every pixel of the frame ----
                    float SampleH(int px, int pz)
                    {
                        int cx = Mathf.Clamp(px, 0, resX - 1);
                        int cz = Mathf.Clamp(pz, 0, resZ - 1);
                        return mx.arr[cz * resX + cx] * 12000f;
                    }

                    for (int z = 0; z < resZ; z++)
                    {
                        for (int x = 0; x < resX; x++)
                        {
                            float hC = SampleH(x, z);

                            if (float.IsNaN(hC) || float.IsInfinity(hC))
                            {
                                texHeight.SetPixel(x, z, magenta);
                                texHill.SetPixel(x, z, magenta);
                                texSlope.SetPixel(x, z, magenta);
                                texNormal.SetPixel(x, z, magenta);
                                texCurv.SetPixel(x, z, magenta);
                                continue;
                            }

                            float hL = SampleH(x - 1, z);
                            float hR = SampleH(x + 1, z);
                            float hD = SampleH(x, z - 1);
                            float hU = SampleH(x, z + 1);

                            // Neighbors may be NaN at the frame edge next to a bad cell;
                            // fall back to center so the derivative maps stay finite.
                            if (float.IsNaN(hL) || float.IsInfinity(hL)) hL = hC;
                            if (float.IsNaN(hR) || float.IsInfinity(hR)) hR = hC;
                            if (float.IsNaN(hD) || float.IsInfinity(hD)) hD = hC;
                            if (float.IsNaN(hU) || float.IsInfinity(hU)) hU = hC;

                            texHeight.SetPixel(x, z, GetHeightColor(hC));

                            Vector3 n = new Vector3(hL - hR, 2f * pixelSize, hD - hU).normalized;
                            texNormal.SetPixel(x, z, new Color(n.x * 0.5f + 0.5f, n.y, n.z * 0.5f + 0.5f, 1f));

                            float intensity = Mathf.Max(0f, Vector3.Dot(n, lightDir));
                            texHill.SetPixel(x, z, new Color(intensity, intensity, intensity, 1f));

                            float slopeDeg = Vector3.Angle(Vector3.up, n);
                            if (slopeDeg < 15f) slopeBuckets[0]++;
                            else if (slopeDeg < 40f) slopeBuckets[1]++;
                            else if (slopeDeg < 70f) slopeBuckets[2]++;
                            else slopeBuckets[3]++;

                            float sColor = Mathf.Clamp01(slopeDeg / 90f);
                            texSlope.SetPixel(x, z, new Color(sColor, sColor, sColor, 1f));

                            float laplacian = (hL + hR + hU + hD - 4f * hC) / (pixelSize * pixelSize);
                            float cColor = Mathf.Clamp01(laplacian * 0.05f + 0.5f);
                            texCurv.SetPixel(x, z, new Color(cColor, cColor, cColor, 1f));
                        }
                    }

                    texHeight.Apply();
                    texHill.Apply();
                    texSlope.Apply();
                    texNormal.Apply();
                    texCurv.Apply();

                    File.WriteAllBytes(Path.Combine(outDir, $"map_{name}_1_height.png"), texHeight.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(outDir, $"map_{name}_2_hillshade.png"), texHill.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(outDir, $"map_{name}_3_slope.png"), texSlope.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(outDir, $"map_{name}_4_normal.png"), texNormal.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(outDir, $"map_{name}_5_curvature.png"), texCurv.EncodeToPNG());

                    long total = (long)resX * resZ;
                    double PctSlope(int b) => total > 0 ? 100.0 * slopeBuckets[b] / total : 0.0;

                    writer.WriteLine($"Level: {name}  (worldSize={size}m, pixelSize={pixelSize:F3}m/px)");
                    writer.WriteLine($"Matrix: resX={resX} resZ={resZ}  arr.Length={actualLen}  expected={expectedLen}  {(actualLen == expectedLen ? "OK" : "MISMATCH")}");
                    writer.WriteLine($"Finite: {finiteCount}  NaN: {nanCount}  Inf: {infCount}  (NaN/Inf here = REAL math bug in generation)");
                    writer.WriteLine($"Height m (AFTER full graph): min={minH:F2}  max={maxH:F2}  mean={mean:F2}  std={std:F2}");
                    // Isolation: statistics of the terrain feeding the erosion node's inlet
                    // (i.e. BEFORE hydro-erosion runs). If PRE is already all-walls, the
                    // geology generator is the culprit; if PRE is calm and AFTER is walls,
                    // the erosion node is the culprit.
                    if (erosionNode != null)
                    {
                        MatrixWorld preMx = ReadInletProduct(graph, data, erosionNode);
                        writer.WriteLine(ComputeStatsLine(preMx, "Height m (PRE-erosion inlet)"));
                    }
                    else
                    {
                        writer.WriteLine("Height m (PRE-erosion inlet): <erosion node not found in graph>");
                    }
                    writer.WriteLine($"Slope %:  0-15deg={PctSlope(0):F1}  15-40={PctSlope(1):F1}  40-70={PctSlope(2):F1}  70+={PctSlope(3):F1}");
                    writer.WriteLine("--------------------------------------------------");
                }
            }
        }
    }
}
