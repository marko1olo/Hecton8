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
        private static readonly string OutDir =
            @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\node_dump";

        [MenuItem("Hecton8/Diagnostics/Node By Node Dump")]
        public static void Dump()
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                DoDump();
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(OutDir, "error.txt"), ex.ToString());
                Debug.LogError($"[NodeByNodeDump] {ex}");
            }
            finally
            {
                EditorApplication.Exit(0);
            }
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
            data.globals.height = 12000f;
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
