using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
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
    public static class GraphHeightsDumpTask
    {
        [MenuItem("Hecton8/Diagnostics/Dump Heights Chain")]
        public static void Dump()
        {
            try
            {
                DoDump();
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\graph_heights_dump_error.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        private static void DoDump()
        {
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
            if (graph == null) throw new Exception("Could not find graph");

            // Find start and end nodes
            Generator startNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HectonSandboxAbyssalShelfMapMagicNode");
            // endNode is defined below

            // Build forward mapping to trace path
            Dictionary<Generator, List<Generator>> forwardLinks = new Dictionary<Generator, List<Generator>>();
            Dictionary<Generator, List<Generator>> backwardLinks = new Dictionary<Generator, List<Generator>>();

            foreach (var g in graph.generators)
            {
                forwardLinks[g] = new List<Generator>();
                backwardLinks[g] = new List<Generator>();
            }

            foreach (var kvp in graph.links)
            {
                IInlet<object> inlet = kvp.Key;
                IOutlet<object> outlet = kvp.Value;
                if (inlet != null && outlet != null)
                {
                    Generator child = inlet.Gen;
                    Generator parent = outlet.Gen;
                    if (child != null && parent != null && forwardLinks.ContainsKey(parent) && backwardLinks.ContainsKey(child))
                    {
                        forwardLinks[parent].Add(child);
                        backwardLinks[child].Add(parent);
                    }
                }
            }

            // We don't trace from Shelf anymore, we just trace ALL ancestors of HeightOutput200
            Generator endNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HeightOutput200");
            if (endNode == null) throw new Exception("Could not find HeightOutput200");

            HashSet<Generator> requiredNodes = new HashSet<Generator>();
            void GatherAncestors(Generator node)
            {
                if (!requiredNodes.Add(node)) return;
                if (backwardLinks.ContainsKey(node))
                {
                    foreach (var parent in backwardLinks[node])
                    {
                        GatherAncestors(parent);
                    }
                }
            }
            GatherAncestors(endNode);

            // The nodes to dump are exactly requiredNodes
            List<Generator> pathNodes = new List<Generator>(requiredNodes);

            // Topological sort
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

            // Create Area and TileData
            // Size 1024x1024 to get enough samples, margins 0
            Vector2D worldPos = new Vector2D(0, 0);
            Vector2D worldSize = new Vector2D(1000, 1000);
            CoordRect rect = new CoordRect(0, 0, 1024, 1024);
            Area area = new Area(worldPos, worldSize, rect, 0);

            Globals globals = new Globals();
            globals.height = 12000f; // from globals
            
            TileData data = new TileData();
            data.area = area;
            data.globals = globals;
            // random dict instance for storing products
            data.ClearProducts();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# MapMagic Height Trace Dump\n");
            sb.AppendLine($"Path length: {pathNodes.Count} nodes\n");
            sb.AppendLine("| Node ID | Type | Out Min | Out Max | Out Std | Drop? | Parameters |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            float prevStd = -1f;

            foreach (var gen in topoOrder)
            {
                // Generate
                try {
                    gen.Generate(data, null);
                } catch (Exception ex) {
                    sb.AppendLine($"| {gen.id} | {gen.GetType().Name} | ERROR | ERROR | ERROR | - | {ex.Message} |");
                    continue;
                }

                // If this is one of our path nodes, inspect its output
                if (pathNodes.Contains(gen))
                {
                    // Find ANY valid outlet if not tracing a single line
                    IOutlet<object> mainOutlet = null;
                    if (gen is IMultiOutlet multiOutlet)
                    {
                        foreach (var outlet in multiOutlet.Outlets())
                        {
                            if (outlet != null) {
                                mainOutlet = outlet;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Fallback using reflection for single outlet
                        var outletField = gen.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                            .FirstOrDefault(f => typeof(IOutlet<object>).IsAssignableFrom(f.FieldType));
                        if (outletField != null)
                        {
                            mainOutlet = outletField.GetValue(gen) as IOutlet<object>;
                        }
                    }

                    if (mainOutlet != null)
                    {
                        MatrixWorld mx = null;
                        try
                        {
                            if (mainOutlet == null) throw new Exception("mainOutlet is null");
                            IUnit unit = mainOutlet as IUnit;
                            if (unit == null) throw new Exception("mainOutlet is not IUnit");
                            var id = unit.Id;
                            if (data == null) throw new Exception("data is null");
                            
                            // Let's use reflection just to be absolutely safe if ReadProduct throws NRE internally
                            object productObj = null;
                            try {
                                productObj = data.ReadProduct(id);
                            } catch (Exception innerE) {
                                throw new Exception($"ReadProduct threw {innerE.GetType().Name}: {innerE.Message}");
                            }

                            mx = productObj as MatrixWorld;
                        }
                        catch (Exception e)
                        {
                            Exception inner = e;
                            while (inner.InnerException != null) inner = inner.InnerException;
                            sb.AppendLine($"| {gen.id} | {gen.GetType().Name} | ERROR | ERROR | ERROR | - | {inner.Message} |");
                            continue;
                        }

                        if (mx != null)
                        {
                            float min = float.MaxValue;
                            float max = float.MinValue;
                            double sum = 0;
                            double sumSq = 0;
                            int count = mx.arr.Length;
                            if (count == 0) continue;

                            for (int i = 0; i < count; i++)
                            {
                                float val = mx.arr[i];
                                if (val < min) min = val;
                                if (val > max) max = val;
                                sum += val;
                                sumSq += (double)val * val;
                            }

                            double mean = sum / count;
                            double variance = (sumSq / count) - (mean * mean);
                            float std = (float)Math.Sqrt(Math.Max(0, variance));

                            string drop = "";
                            if (prevStd >= 0)
                            {
                                if (std < prevStd * 0.5f) drop = "**YES**";
                                else drop = "No";
                            }
                            prevStd = std;

                            // Extract params
                            string paramStr = "";
                            if (gen is Blend200 blend)
                            {
                                paramStr = $"Layers: {blend.layers.Length}. Opacities: " + string.Join(", ", blend.layers.Select(l => l.opacity.ToString("F4")));
                            }
                            else if (gen is Levels200 levels)
                            {
                                paramStr = $"inMin: {levels.inMin:F4}, inMax: {levels.inMax:F4}, gamma: {levels.gamma:F4}, outMin: {levels.outMin:F4}, outMax: {levels.outMax:F4}";
                            }
                            else if (gen.GetType().Name == "HectonSandboxAbyssalShelfMapMagicNode")
                            {
                                // Reflection for custom node
                                Type t = gen.GetType();
                                float ridge = (float)t.GetField("ridgeHeightMeters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gen);
                                float trench = (float)t.GetField("trenchDepthMeters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gen);
                                paramStr = $"ridge: {ridge}, trench: {trench}";
                            }

                            sb.AppendLine($"| {gen.id} | {gen.GetType().Name} | {min:F4} | {max:F4} | {std:F4} | {drop} | {paramStr} |");
                        }
                        else
                        {
                            sb.AppendLine($"| {gen.id} | {gen.GetType().Name} | NO MX | NO MX | NO MX | - | - |");
                        }
                    }
                }
            }

            string outPath = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\graph_heights_dump.md";
            File.WriteAllText(outPath, sb.ToString());
        }
    }
}
