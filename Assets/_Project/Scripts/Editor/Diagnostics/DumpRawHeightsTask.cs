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

            // WHICH GRAPH, and it is now a choice rather than a constant. The sandbox has two: the 500-node
            // biomes graph (the default below, so every existing caller keeps dumping what it dumped) and
            // the 16-node HECTON_PROCEDURAL_GEOLOGY_GRAPH geology bench. This tool executes a graph by hand
            // and reads the product feeding HeightOutput200, which makes it the only way to answer "is the
            // height matrix empty before apply, or is apply losing it" - and that question is currently
            // open for the geology graph, which this tool could not reach.
            //
            // Refuse rather than fall back: a dump whose header says graph A while the caller asked for
            // graph B is worse than no dump, because the numbers get quoted.
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-graphAsset", StringComparison.Ordinal))
                    continue;

                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    throw new Exception(
                        "-graphAsset was passed with no path after it, and falling back to the default " +
                        $"'{path}' would publish a dump of the wrong graph. Nothing was written.");

                path = args[i + 1].Trim();
                break;
            }

            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(path);
            if (graph == null) throw new Exception($"Could not find graph '{path}'");

            Generator endNode = graph.generators.FirstOrDefault(g => g.GetType().Name == "HeightOutput200");
            if (endNode == null) throw new Exception($"Could not find HeightOutput200 in '{path}'");

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

            // The sampled window, stated plainly because it was previously misleading. `centerPos` used to
            // be declared as (5000,5000) right here and NEVER READ - the Area is built from worldPos, so the
            // dump has always covered (0,0)..(10000,10000), not a window centred on 5000. A reader scanning
            // these four lines would conclude the opposite. Removed rather than wired up: which window to
            // sample is a real decision, and silently moving it would invalidate comparison against every
            // dump taken before today.
            float size = 10000f;
            Vector2D worldPos = new Vector2D(0, 0);
            Vector2D worldSize = new Vector2D(size, size);
            CoordRect rect = new CoordRect(0, 0, 1024, 1024);
            Area area = new Area(worldPos, worldSize, rect, 0);

            // READ THE SPAN OFF THE GRAPH BEING DUMPED, do not assume it.
            //
            // Was a hardcoded LiveWorldGraphSpanMeters (12000 m), correct only for the biomes graph. Once
            // -graphAsset can select the geology graph, a fixed constant makes every metre in the dump wrong
            // by the ratio of the two spans - 12000/4000 = 3x for the sandbox V2 authoring - while still
            // producing a plausible-looking heightmap. The comment that used to sit here named that hazard
            // and could not fix it; reading highWorldY/lowWorldY off the shelf node in THIS graph fixes it.
            //
            // Falls back to the named constant only when the graph carries no such node, and says so.
            float spanMeters = Hecton8.World.WorldVerticalExtentMath.LiveWorldGraphSpanMeters;
            string spanSource = $"fallback constant LiveWorldGraphSpanMeters ({spanMeters:F0}m)";
            Generator shelfNode = graph.generators.FirstOrDefault(
                g => g.GetType().Name == "HectonSandboxAbyssalShelfMapMagicNode");
            if (shelfNode != null)
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                var highField = shelfNode.GetType().GetField("highWorldY", flags);
                var lowField = shelfNode.GetType().GetField("lowWorldY", flags);
                if (highField != null && lowField != null)
                {
                    float high = (float)highField.GetValue(shelfNode);
                    float low = (float)lowField.GetValue(shelfNode);
                    spanMeters = Mathf.Max(high, low + 1f) - low;
                    spanSource = $"graph node highWorldY={high:F0} lowWorldY={low:F0} -> span {spanMeters:F0}m";
                }
            }

            Debug.Log($"[DumpRawHeightsTask] graph '{path}', vertical span from {spanSource}.");

            TileData data = new TileData();
            data.area = area;
            data.globals = new Globals();
            data.globals.height = spanMeters;
            data.random = new Den.Tools.Noise(12345);
            data.ClearProducts();

            StopToken stop = new StopToken();

            // WITHOUT THIS THE DUMP MEASURES ITSELF, NOT THE GRAPH.
            //
            // TileData.ReadInletProduct reads by inlet.LinkedOutletId (TileData.cs:349), and that field is
            // populated ONLY by Graph.RefreshInputHashIds (Graph.cs:1120-1137), which Graph.Generate calls as
            // its first act (Graph.cs:907). This tool deliberately does NOT call Graph.Generate - it walks
            // topoOrder and calls gen.Generate directly - so every LinkedOutletId was left at 0, ReadProduct
            // returned null for "not connected" (TileData.cs:354), and every node with an inlet published
            // nothing.
            //
            // Measured 2026-08-11: that produced "HectonBiomeMatrixMapMagicPostProcessNode -> published
            // NOTHING" on a graph whose links a separate reflection audit had just confirmed intact, and it
            // read exactly like a real defect in the geology chain. The graph was fine; the harness was
            // reading through an index it never built. Reflection because RefreshInputHashIds is private and
            // the alternative - calling the full Graph.Generate - is the thing this tool exists to bypass.
            var refreshLut = typeof(Graph).GetMethod(
                "RefreshInputHashIds",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (refreshLut == null)
                throw new Exception(
                    "MapMagic.Nodes.Graph has no private RefreshInputHashIds() - the method this dump needs " +
                    "to populate inlet.LinkedOutletId before reading products was renamed or removed. " +
                    "Refusing to run: without it every inlet reads as unconnected and the dump would report " +
                    "a graph-wide defect that does not exist. No dump was written.");
            refreshLut.Invoke(graph, null);

            // WHICH NODES ACTUALLY RAN, named before the products are read. topoOrder is built from
            // graph.links by walking ancestors backwards from HeightOutput200, so a node the walk never
            // reached is simply never generated - and the only symptom downstream is an absent product,
            // which is indistinguishable from a node that ran and refused to publish. Those are opposite
            // defects with opposite fixes, so the run has to say which one it is.
            Debug.Log(
                $"[DumpRawHeightsTask] executing {topoOrder.Count} of {graph.generators.Length} node(s) " +
                $"reached from HeightOutput200: " +
                string.Join(", ", topoOrder.Select(g => g.GetType().Name)));

            foreach (var gen in topoOrder)
            {
                gen.Generate(data, stop);

                // Per-node publication state. A generator that ran and published nothing is the interesting
                // case; without this line it is invisible until the very last read fails.
                object published = gen is IUnit unit ? data.ReadProduct(unit.Id) : null;
                Debug.Log(
                    $"[DumpRawHeightsTask]   {gen.GetType().Name} -> " +
                    (published == null ? "published NOTHING" : $"published {published.GetType().Name}"));
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
            
            // THREE DISTINCT FAILURES THAT ALL USED TO SURFACE AS THE SAME NullReferenceException at the
            // `mx.rect` line, which named none of them. Measured 2026-08-11 on the geology graph: the dump
            // died with a bare NRE, and the actual finding - that HeightOutput200's inlet carries NO PRODUCT
            // after the graph ran - had to be recovered by reading the line numbers in the stack trace. That
            // finding is the whole point of this tool, so it gets stated.
            if (inlet == null)
                throw new Exception(
                    $"'{endNode.GetType().Name}' in '{path}' exposes no IInlet<object>, so there is no inlet " +
                    "whose product could be read. No dump was written.");

            if (linkedOutlet == null)
                throw new Exception(
                    $"HeightOutput200's inlet in '{path}' is UNCONNECTED - graph.links has no entry for it, " +
                    "so the height output is an orphan and no terrain height can ever be produced from this " +
                    "graph. No dump was written.");

            IUnit unitOut = linkedOutlet as IUnit;
            if (unitOut == null)
                throw new Exception(
                    $"the outlet feeding HeightOutput200 in '{path}' is a " +
                    $"'{linkedOutlet.GetType().Name}', which is not an IUnit, so it has no product Id to " +
                    "read. No dump was written.");

            MatrixWorld mx = data.ReadProduct(unitOut.Id) as MatrixWorld;
            if (mx == null)
            {
                // This is the interesting one: the graph is wired, every node ran, and the inlet still
                // holds nothing. Either the upstream generator returned without publishing (its own
                // RemoveProduct path) or it published a type that is not a MatrixWorld.
                object rawProduct = data.ReadProduct(unitOut.Id);
                throw new Exception(
                    $"the graph in '{path}' ran to completion but the product feeding HeightOutput200 " +
                    $"(outlet '{linkedOutlet.GetType().Name}', id {unitOut.Id}) is " +
                    (rawProduct == null
                        ? "ABSENT - nothing was published to it at all, so this graph produces a flat " +
                          "terrain no matter how it is applied. Check whether the upstream node hit a " +
                          "RemoveProduct/early-return path."
                        : $"a '{rawProduct.GetType().Name}', not a MatrixWorld, so it cannot be read as a " +
                          "heightmap.") +
                    " No dump was written.");
            }

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

            // Normalised extremes alongside the metre ones. The metre figures alone cannot distinguish
            // "field saturated at the top of its range" from "field is a mid-range constant" when the span
            // used to scale them is itself in question - which is exactly the confusion the hardcoded 12000
            // caused. Reporting the raw 0..1 values makes the scale a fact rather than an assumption.
            float minNorm = float.MaxValue;
            float maxNorm = float.MinValue;

            for (int z = 0; z < resZ / 2; z++)
            {
                for (int x = 0; x < resX; x++)
                {
                    float rawFloat = mx.arr[(z) * resX + (x)];

                    // MatrixWorld stores NORMALISED height (0..1); metres = normalised * vertical span.
                    //
                    // WAS `decodedFloat * 12000f` - a hardcoded span, and wrong the moment -graphAsset can
                    // select a graph that is not the 12000 m biomes one. Measured 2026-08-11 on the geology
                    // graph, whose span is 6000 m: the constant made a uniform 0.5 field report as
                    // "Min/Max/Mean 6000.0 m", which reads as the field saturating at the TOP of a 6000 m
                    // world - a completely different defect from the mid-range constant it actually is.
                    //
                    // The BitConverter round-trip on the line below is a no-op (float -> bytes -> same
                    // float) left from an earlier bit-inspection experiment; it is kept only because
                    // removing it is a separate change from fixing the scale, and it cannot alter a value.
                    byte[] bytes = BitConverter.GetBytes(rawFloat);
                    float decodedFloat = BitConverter.ToSingle(bytes, 0);
                    float h = decodedFloat * spanMeters;

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
                        if (decodedFloat < minNorm) minNorm = decodedFloat;
                        if (decodedFloat > maxNorm) maxNorm = decodedFloat;
                    }
                }
            }

            double mean = countNormal > 0 ? sum / countNormal : 0;
            double variance = countNormal > 0 ? (sumSq / countNormal) - (mean * mean) : 0;
            double std = Math.Sqrt(Math.Max(0, variance));

            // Per-graph filename, and the header states which graph and which window. The old fixed name
            // plus the hardcoded title "Lower Half of 10km map" survived a -graphAsset switch unchanged, so
            // a geology dump would overwrite a biomes dump and still claim to describe it.
            string reportPath = Path.Combine(
                outDir, $"raw_heights_{Path.GetFileNameWithoutExtension(path)}.txt");
            using (StreamWriter writer = new StreamWriter(reportPath))
            {
                writer.WriteLine("Raw Heights Diagnostics");
                writer.WriteLine($"Graph: {path}");
                writer.WriteLine($"Vertical span used to convert normalised -> metres: {spanSource}");
                writer.WriteLine(
                    $"Window: world {worldPos.x}..{worldPos.x + worldSize.x} x " +
                    $"{worldPos.z}..{worldPos.z + worldSize.z} m, matrix {resX}x{resZ}, " +
                    $"sampled rows Z=0..{resZ / 2} (lower half)");
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
                    writer.WriteLine($"Normalised min: {minNorm:F6}");
                    writer.WriteLine($"Normalised max: {maxNorm:F6}");

                    // A zero-variance field is the single most misread result this tool can produce, so it
                    // is named in the artifact rather than left for a reader to infer from four equal
                    // numbers. Which CONSTANT it is matters: 0 is an empty matrix, 1 is a clamped/saturated
                    // one, and anything in between is a generator returning a fill value.
                    if (std < 0.0001)
                        writer.WriteLine(
                            $"VERDICT: CONSTANT FIELD - every sampled cell is {minNorm:F6} normalised " +
                            $"({minH:F1} m). Zero variance, so this graph has no relief here at all. " +
                            (minNorm <= 0f ? "0 means nothing was written into the matrix."
                             : minNorm >= 1f ? "1 means the field is clamped at the top of its range."
                             : "A mid-range constant means a generator filled the matrix instead of " +
                               "computing it."));
                    else
                        writer.WriteLine(
                            $"VERDICT: field varies - {maxH - minH:F1} m of relief across the sampled half.");
                }
            }

            Debug.Log($"[DumpRawHeightsTask] wrote {reportPath}");
        }
    }
}
