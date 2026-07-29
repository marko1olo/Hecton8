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
            // This task used to swallow every failure and then exit 0 from a finally block, so a
            // batchmode invocation that generated nothing still reported success. Worse, the exception
            // went to a file under another agent's private brain directory, which no Unity log reader
            // ever sees - and if that directory was absent, File.WriteAllText threw inside the catch and
            // destroyed the original exception. Errors now go to the Unity log, which batchmode captures,
            // and the exit code distinguishes a real dump from a failed one.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[GraphHeightsDumpTask] REFUSED: no GPU context (graphicsDeviceType == Null). This " +
                    "task generates MapMagic matrices, and compute shaders plus Graphics.Blit return " +
                    "ZEROS without a graphics device - the dump would be a table of plausible-looking " +
                    "zeros. Remove -nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                DoDump();
            }
            catch (Exception ex)
            {
                Debug.LogError("[GraphHeightsDumpTask] FAILED, no dump was written: " + ex);
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
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
                StopToken stop = new StopToken();
                // Generate
                try {
                    gen.Generate(data, stop);
                } catch (Exception ex) {
                    string st = ex.ToString().Replace('\n', ' ').Replace('\r', ' ').Replace('|', ':');
                    sb.AppendLine($"| {gen.id} | {gen.GetType().Name} | ERROR | ERROR | ERROR | - | {st} |");
                    continue;
                }

                // If this is one of our path nodes, inspect its output
                if (pathNodes.Contains(gen))
                {
                    // Find ANY valid outlet if not tracing a single line
                    IUnit mainOutlet = null;
                    if (gen is IMultiOutlet multiOutlet)
                    {
                        foreach (var outlet in multiOutlet.Outlets())
                        {
                            if (outlet != null) {
                                mainOutlet = outlet as IUnit;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var outletField = gen.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                            .FirstOrDefault(f => typeof(IUnit).IsAssignableFrom(f.FieldType) && f.FieldType.Name.Contains("Outlet"));
                        if (outletField != null)
                        {
                            mainOutlet = outletField.GetValue(gen) as IUnit;
                        }
                    }

                    if (mainOutlet == null && gen is IUnit genUnit) 
                    {
                        // Some generators ARE the outlet themselves!
                        mainOutlet = genUnit;
                    }

                    if (mainOutlet != null)
                    {
                        MatrixWorld mx = null;
                        try
                        {
                            ulong id = mainOutlet.Id;
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

            // The dump itself used to land in another agent's private brain directory: outside the repo,
            // unversioned, and invisible to anyone reading this project's evidence. Logs/ is where every
            // other route artifact already lives, so height reasoning can be traced to a file that exists.
            string outDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(outDirectory);
            string outPath = Path.Combine(outDirectory, "graph_heights_dump.md");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[GraphHeightsDumpTask] Wrote {sb.Length} chars to {outPath}");
        }
    }
}
