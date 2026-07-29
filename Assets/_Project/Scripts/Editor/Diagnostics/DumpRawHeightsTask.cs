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
    public static class DumpRawHeightsTask
    {
        [MenuItem("Hecton8/Diagnostics/Dump Raw Heights")]
        public static void Dump()
        {
            // Raw heights are the most load-bearing dump in this project, and until now a failure to
            // produce them was indistinguishable from success: the exception went to another agent's
            // private brain directory and the finally block exited 0 regardless. Compute output is also
            // all zeros without a GPU context, per C:\hades\.claude\rules\hecton8-shaders-compute.md:36-37.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[DumpRawHeightsTask] REFUSED: no GPU context (graphicsDeviceType == Null). Compute " +
                    "shaders and Graphics.Blit return ZEROS here, so a raw-height dump taken now would be " +
                    "fabricated zeros wearing the shape of real data. Remove -nographics and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                DoDump();
            }
            catch (Exception ex)
            {
                Debug.LogError("[DumpRawHeightsTask] FAILED, no dump was written: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void DoDump()
        {
            // Was another agent's private brain directory - outside the repo and unversioned.
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(outDir);
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
            if (graph == null) throw new Exception("Could not find graph");

            Generator endNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HeightOutput200");
            if (endNode == null) throw new Exception("Could not find HeightOutput200");

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
                    foreach (var parent in backwardLinks[node]) 
                        if (parent != null) GatherAncestors(parent);
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

            float size = 10000f;
            Vector2D centerPos = new Vector2D(5000, 5000);
            Vector2D worldPos = new Vector2D(0, 0); 
            Vector2D worldSize = new Vector2D(size, size);
            CoordRect rect = new CoordRect(0, 0, 1024, 1024);
            Area area = new Area(worldPos, worldSize, rect, 0);

            TileData data = new TileData();
            data.area = area;
            data.globals = new Globals();
            data.globals.height = 12000f;
            data.random = new Den.Tools.Noise(12345);
            data.ClearProducts();

            StopToken stop = new StopToken();

            foreach (var gen in topoOrder)
            {
                gen.Generate(data, stop);
            }

            IInlet<object> inlet = null;
            if (endNode is IInlet<object> directInlet) inlet = directInlet;
            else
            {
                var inletField = endNode.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(f => typeof(IInlet<object>).IsAssignableFrom(f.FieldType));
                if (inletField != null) inlet = inletField.GetValue(endNode) as IInlet<object>;
            }

            IOutlet<object> linkedOutlet = null;
            foreach (var kvp in graph.links)
            {
                if (kvp.Key == inlet)
                {
                    linkedOutlet = kvp.Value;
                    break;
                }
            }
            
            IUnit unitOut = linkedOutlet as IUnit;
            MatrixWorld mx = data.ReadProduct(unitOut.Id) as MatrixWorld;
            
            int resX = mx.rect.size.x;
            int resZ = mx.rect.size.z;

            int countNaN = 0;
            int countInf = 0;
            int countZero = 0;
            int countNormal = 0;
            double sum = 0;
            double sumSq = 0;
            float minH = float.MaxValue;
            float maxH = float.MinValue;

            for (int z = 0; z < resZ / 2; z++)
            {
                for (int x = 0; x < resX; x++)
                {
                    float rawFloat = mx.arr[(z) * resX + (x)];
                    
                    byte[] bytes = BitConverter.GetBytes(rawFloat);
                    float decodedFloat = BitConverter.ToSingle(bytes, 0);
                    float h = decodedFloat * 12000f;

                    if (float.IsNaN(h)) countNaN++;
                    else if (float.IsInfinity(h)) countInf++;
                    else if (h == 0f) countZero++;
                    else
                    {
                        countNormal++;
                        sum += h;
                        sumSq += (double)h * h;
                        if (h < minH) minH = h;
                        if (h > maxH) maxH = h;
                    }
                }
            }

            double mean = countNormal > 0 ? sum / countNormal : 0;
            double variance = countNormal > 0 ? (sumSq / countNormal) - (mean * mean) : 0;
            double std = Math.Sqrt(Math.Max(0, variance));

            string reportPath = Path.Combine(outDir, "raw_heights_report.txt");
            using (StreamWriter writer = new StreamWriter(reportPath))
            {
                writer.WriteLine("Raw Heights Diagnostics (Lower Half of 10km map: Z=0..512)");
                writer.WriteLine($"Total Pixels Checked: {(resX * (resZ / 2))}");
                writer.WriteLine($"NaN count: {countNaN}");
                writer.WriteLine($"Inf count: {countInf}");
                writer.WriteLine($"Exactly Zero count: {countZero}");
                writer.WriteLine($"Normal count: {countNormal}");
                if (countNormal > 0)
                {
                    writer.WriteLine($"Min Height: {minH:F4} m");
                    writer.WriteLine($"Max Height: {maxH:F4} m");
                    writer.WriteLine($"Mean Height: {mean:F4} m");
                    writer.WriteLine($"Std Dev: {std:F4} m");
                }
            }
        }
    }
}
