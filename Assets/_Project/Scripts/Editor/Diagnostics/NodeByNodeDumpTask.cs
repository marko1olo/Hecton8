using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Core;
using MapMagic.Terrains;
using MapMagic.Products;
using Den.Tools;
using Den.Tools.Matrices;

namespace MapMagic.Editor.Diagnostics
{
    /// <summary>
    /// Runs the MapMagic graph node-by-node, saving intermediate heightmap hillshades after each node.
    /// This isolates which node introduces the 32x32 checkerboard artifact.
    /// Run via: Hecton8 > Diagnostics > Node By Node Dump
    /// </summary>
    public static class NodeByNodeDumpTask
    {
        // Was another agent's private brain directory - outside the repo, unversioned, and invisible to
        // anyone auditing this project's terrain evidence. Logs/ already holds the route artifacts.
        private static readonly string OutDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "node_dump");

        [MenuItem("Hecton8/Diagnostics/Node By Node Dump")]
        public static void Dump()
        {
            // This task encodes PNGs per node, so it depends on a graphics device.
            // C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37 bans -nographics for MapMagic
            // generation tests: compute shaders and Graphics.Blit return zeros with no GPU context, and a
            // per-node dump of zeros is shaped exactly like a real one.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[NodeByNodeDump] REFUSED: no GPU context (graphicsDeviceType == Null). Every matrix " +
                    "and PNG this task emits would be zeros wearing the shape of real data. Remove " +
                    "-nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                Directory.CreateDirectory(OutDir);
                DoDump();
            }
            catch (Exception ex)
            {
                // Previously this exited 0 from a finally block, so a batchmode run that produced no dump
                // still reported success to whatever read its exit code.
                Debug.LogError($"[NodeByNodeDump] FAILED, the dump is incomplete or absent: {ex}");
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void DoDump()
        {
            string graphPath = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(graphPath);
            if (graph == null) throw new Exception("Graph not found at: " + graphPath);

            // Build backward links: for each inlet key, store the outlet value
            var backwardLinks = new Dictionary<Generator, List<Generator>>();
            foreach (var g in graph.generators)
                backwardLinks[g] = new List<Generator>();

            foreach (var kvp in graph.links)
            {
                if (kvp.Key?.Gen != null && kvp.Value?.Gen != null)
                {
                    if (backwardLinks.ContainsKey(kvp.Key.Gen))
                        backwardLinks[kvp.Key.Gen].Add(kvp.Value.Gen);
                }
            }

            // Find HeightOutput200
            Generator endNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HeightOutput200");
            if (endNode == null) throw new Exception("HeightOutput200 not found");

            // Collect all ancestors
            var required = new HashSet<Generator>();
            void Gather(Generator n)
            {
                if (!required.Add(n)) return;
                if (backwardLinks.ContainsKey(n))
                    foreach (var p in backwardLinks[n]) Gather(p);
            }
            Gather(endNode);

            // Topological sort
            var topoOrder = new List<Generator>();
            var visited = new HashSet<Generator>();
            void Topo(Generator n)
            {
                if (!visited.Add(n)) return;
                if (backwardLinks.ContainsKey(n))
                    foreach (var p in backwardLinks[n])
                        if (required.Contains(p)) Topo(p);
                topoOrder.Add(n);
            }
            foreach (var n in required) Topo(n);

            // Setup 10km area centered at (5000, 5000)
            float size = 10000f;
            var worldPos = new Vector2D(0f, 0f);
            var worldSize = new Vector2D(size, size);
            var rect = new CoordRect(0, 0, 1024, 1024);
            var area = new Area(worldPos, worldSize, rect, 0);

            var data = new TileData();
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
            var stop = new StopToken();

            using var log = new StreamWriter(Path.Combine(OutDir, "run_log.txt"));
            log.WriteLine($"Graph: {graphPath}");
            log.WriteLine($"Total nodes in pipeline: {topoOrder.Count}");
            log.WriteLine("---");

            int step = 0;
            foreach (var gen in topoOrder)
            {
                gen.Generate(data, stop);

                string typeName = gen.GetType().Name;
                // Try to dump the MatrixWorld product this node produced
                MatrixWorld mx = null;
                try
                {
                    // Each outlet from this generator
                    foreach (var outlet in graph.generators
                        .SelectMany(g => g.GetType()
                            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                            .Where(f => typeof(IUnit).IsAssignableFrom(f.FieldType))
                            .Select(f => f.GetValue(g) as IUnit)
                            .Where(u => u != null && u.Gen == gen)))
                    {
                        object product = data.ReadProduct(outlet.Id);
                        if (product is MatrixWorld mw)
                        {
                            mx = mw;
                            break;
                        }
                    }
                }
                catch { /* ignore reflection errors */ }

                if (mx != null)
                {
                    SaveHillshade(mx, step, typeName, log);
                }
                else
                {
                    log.WriteLine($"[{step:D3}] {typeName} — no MatrixWorld product");
                }

                step++;
            }

            log.WriteLine("Done.");
        }

        private static void SaveHillshade(MatrixWorld mx, int step, string typeName, StreamWriter log)
        {
            int W = mx.rect.size.x;
            int H = mx.rect.size.z;
            if (W <= 2 || H <= 2) { log.WriteLine($"[{step:D3}] {typeName} — too small ({W}x{H})"); return; }

            float pixelSize = (float)(mx.worldSize.x / W);
            float minH = float.MaxValue, maxH = float.MinValue;

            for (int i = 0; i < mx.arr.Length; i++)
            {
                float v = mx.arr[i] * 12000f;
                if (v < minH) minH = v;
                if (v > maxH) maxH = v;
            }

            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var lightDir = new Vector3(-1f, 0.5f, 1f).normalized;

            for (int z = 1; z < H - 1; z++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    float hC = mx.arr[z * W + x] * 12000f;
                    float hL = mx.arr[z * W + (x - 1)] * 12000f;
                    float hR = mx.arr[z * W + (x + 1)] * 12000f;
                    float hD = mx.arr[(z - 1) * W + x] * 12000f;
                    float hU = mx.arr[(z + 1) * W + x] * 12000f;

                    var n = new Vector3(hL - hR, 2f * pixelSize, hD - hU).normalized;
                    float intensity = Mathf.Max(0f, Vector3.Dot(n, lightDir));
                    tex.SetPixel(x, z, new Color(intensity, intensity, intensity, 1f));
                }
            }

            tex.Apply();
            string filename = $"{step:D3}_{typeName}_hill.png";
            File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());

            log.WriteLine($"[{step:D3}] {typeName} — saved {filename} | H range [{minH:F0}m .. {maxH:F0}m]");
            log.Flush();
        }
    }
}
