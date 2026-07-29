using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hecton8.World;
using MapMagic.Core;
using MapMagic.Nodes.MatrixGenerators;

namespace Hecton8.Diagnostics
{
    /// <summary>
    /// Task 6 of the HeadlessRunAll chain. Compares the amplitude the height FUNCTION produces on the CPU
    /// against the amplitude the live scene's TerrainData actually holds - the whole point being to catch a
    /// scale mismatch between math and shipped terrain.
    ///
    /// It is chained, not standalone: the success path ends in HeadlessRunAll.NextTask(), never in
    /// EditorApplication.Exit(0). A failure path DOES exit, because letting the chain continue would let
    /// HeadlessRunAll print "ALL HEADLESS TASKS COMPLETED SUCCESSFULLY" (HeadlessRunAll.cs:58) over a task
    /// that produced nothing.
    /// </summary>
    public static class MeasureAmplitudeTask
    {
        // Was C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-...\amplitude_report.md - another agent's
        // private scratch directory, outside the repo, unversioned, and invisible to anyone auditing this
        // project's terrain evidence. `static readonly` rather than `const` because Path.Combine is not a
        // compile-time constant.
        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "measure_amplitude");

        public static void Run()
        {
            // Section 2 reads back TerrainData that MapMagic generated (StandaloneMeasureAmplitude.cs:26-32
            // calls mm.StartGenerate() immediately before this). C:\hades\.claude\rules\
            // hecton8-shaders-compute.md:36-37 bans -nographics for MapMagic generation because compute
            // shaders and Graphics.Blit return zeros with no GPU context. A zeroed heightmap makes this
            // audit report "terrain amplitude 0 m vs math amplitude 5000 m", which reads as a genuine
            // scale defect in the terrain - the most expensive false positive this tool can emit.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[MeasureAmplitudeTask] REFUSED: no GPU context (graphicsDeviceType == Null). The " +
                    "live-terrain half of this audit would read back zeros and report a scale mismatch " +
                    "that does not exist. Remove -nographics from the batch invocation and run again.");
                EditorApplication.Exit(3);
                return;
            }

            try
            {
                Directory.CreateDirectory(OutputDir);
                DoRun();
            }
            catch (Exception ex)
            {
                // There was no catch at all before, and the report went to a directory belonging to
                // another agent that may not exist. A throw there produced no report, no visible error,
                // and - because the throw happened before HeadlessRunAll.NextTask() - a chain that simply
                // stopped without ever saying why.
                Debug.LogError($"[MeasureAmplitudeTask] FAILED, no amplitude report was written: {ex}");
                EditorApplication.Exit(2);
                return;
            }

            // Chained continuation, NOT Exit(0). Reaching here means the report exists and its parameters
            // came from the authored graph.
            HeadlessRunAll.NextTask();
        }

        private static void DoRun()
        {
            Debug.Log("=============================================");
            Debug.Log("TASK 6: AMPLITUDE AND SCALE AUDIT");
            Debug.Log("=============================================");

            string artifactPath = Path.Combine(OutputDir, "amplitude_report.md");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Amplitude and Scale Audit");
            sb.AppendLine();

            sb.AppendLine("## 1. Raw Math Function Amplitude (10x10km Grid)");
            sb.AppendLine("Sampling `HectonSandboxAbyssalShelfMath.EvaluateHeightMeters` from -5000 to 5000 on X and Z, step 100m.");

            var p = new HectonSandboxAbyssalShelfParams
            {
                AupCellSizeMeters = 1.0,
                DescentRadiusMeters = 15000.0,
                PlateCellSizeMeters = 4200.0,
                HighWorldY = 1000f,
                LowWorldY = -4000f,
                RidgeHeightMeters = 700f,
                RidgeMultiplier = 0.08f,
                RidgeWidthMeters = 1450f,
                JunctionWidthMeters = 2800f,
                PlateUniformity = 0.78f,
                DomainWarpMeters = 1450f,
                DomainWarpFrequency = 0.00011f,
                SlopeNoiseFrequency = 0.00003125f,
                MacroExponentialFalloff = 3.1f,
                ShelfRunMeters = 15000f,
                ShelfTargetSlopeDegrees = 30f,
                TrenchDepthMeters = 5000f,
                TrenchWidthMeters = 780f,
                TrenchSharpness = 2.4f,
                IslandCenterRadiusMeters = 2600f,
                IslandJunctionThreshold = 0.58f,
                Seed = 111u,
                MacroGeologyArtifactVersion = WorldMacroGeologyFields.ArtifactVersion
            };

            // The parameters above are placeholder defaults. If they are NOT overwritten from the authored
            // graph node, section 1 measures the amplitude of numbers nobody shipped while the document
            // header still says "Amplitude and Scale Audit", and the comparison against the live terrain
            // becomes meaningless. All three of these branches used to fall through in total silence: no
            // log line, no note in the report, and the chain continued to "ALL HEADLESS TASKS COMPLETED
            // SUCCESSFULLY".
            MapMagicObject mm = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>();
            if (mm == null)
            {
                throw new InvalidOperationException(
                    "no MapMagicObject in the loaded scene, so the authored terrain parameters could not " +
                    "be read. Refusing to publish an amplitude audit of placeholder defaults.");
            }
            if (mm.graph == null)
            {
                throw new InvalidOperationException(
                    $"MapMagicObject '{mm.name}' has no graph assigned, so the authored terrain " +
                    "parameters could not be read. Refusing to publish an amplitude audit of placeholder " +
                    "defaults.");
            }

            HectonSandboxAbyssalShelfMapMagicNode macroBaseNode = null;
            foreach (var gen in mm.graph.generators)
            {
                if (gen is HectonSandboxAbyssalShelfMapMagicNode node)
                {
                    macroBaseNode = node;
                    break;
                }
            }

            if (macroBaseNode == null)
            {
                throw new InvalidOperationException(
                    $"graph '{mm.graph.name}' contains no HectonSandboxAbyssalShelfMapMagicNode among its " +
                    $"{mm.graph.generators.Length} generator(s), so the authored terrain parameters could " +
                    "not be read. Refusing to publish an amplitude audit of placeholder defaults.");
            }

            p.HighWorldY = macroBaseNode.highWorldY;
            p.LowWorldY = macroBaseNode.lowWorldY;
            p.RidgeHeightMeters = macroBaseNode.ridgeHeightMeters;
            p.TrenchDepthMeters = macroBaseNode.trenchDepthMeters;
            p.Seed = unchecked((uint)macroBaseNode.seed);
            sb.AppendLine($"Extracted parameters from MapMagic Graph: HighY={p.HighWorldY}, LowY={p.LowWorldY}, Ridge={p.RidgeHeightMeters}, Trench={p.TrenchDepthMeters}, Seed={p.Seed}");

            float minMath = float.MaxValue;
            float maxMath = float.MinValue;
            double sumMath = 0;
            int countMath = 0;
            int nonFiniteMath = 0;

            for (double x = -5000; x <= 5000; x += 100)
            {
                for (double z = -5000; z <= 5000; z += 100)
                {
                    float h = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(x, z, in p);
                    // A NaN loses every comparison, so without this guard a single NaN sample left both
                    // folds sitting on their float.MaxValue / float.MinValue seeds and the report printed
                    // a 3.4e38 "Min Height" as if it were a measurement.
                    if (float.IsNaN(h) || float.IsInfinity(h)) { nonFiniteMath++; continue; }
                    if (h < minMath) minMath = h;
                    if (h > maxMath) maxMath = h;
                    sumMath += h;
                    countMath++;
                }
            }

            if (countMath == 0)
            {
                throw new InvalidOperationException(
                    $"all {nonFiniteMath} samples from HectonSandboxAbyssalShelfMath.EvaluateHeightMeters " +
                    "were NaN or infinite, so there is no amplitude to report.");
            }

            float mathAmplitude = maxMath - minMath;
            sb.AppendLine($"- **Min Height:** {minMath:F2} m");
            sb.AppendLine($"- **Max Height:** {maxMath:F2} m");
            sb.AppendLine($"- **Avg Height:** {(sumMath / countMath):F2} m");
            sb.AppendLine($"- **Amplitude (Max - Min):** {mathAmplitude:F2} m");
            sb.AppendLine($"- **Finite samples:** {countMath}  **Non-finite (excluded):** {nonFiniteMath}");
            sb.AppendLine();

            sb.AppendLine("## 2. TerrainData Parameters (Live Scene)");
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            sb.AppendLine($"Found {terrains.Length} Terrain objects in the scene.");

            int measuredTerrains = 0;
            float widestTerrainAmplitude = 0f;

            foreach (var t in terrains)
            {
                var td = t.terrainData;
                if (td == null)
                {
                    // Was skipped in silence. A Terrain with no TerrainData contributes nothing to the
                    // audit, so the reader has to be told it was skipped rather than inferring it from a
                    // section count that does not match the header.
                    sb.AppendLine($"### Terrain: {t.name}");
                    sb.AppendLine("- SKIPPED: terrainData is null, no heights to measure.");
                    sb.AppendLine();
                    Debug.LogWarning(
                        $"[MeasureAmplitudeTask] Terrain '{t.name}' has no TerrainData and was excluded " +
                        "from the amplitude audit.");
                    continue;
                }

                sb.AppendLine($"### Terrain: {t.name}");
                sb.AppendLine($"- **Transform Position:** {t.transform.position}");
                sb.AppendLine($"- **TerrainData Size:** {td.size} (size.y = {td.size.y})");
                sb.AppendLine($"- **Heightmap Resolution:** {td.heightmapResolution}");

                float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                float minT = float.MaxValue;
                float maxT = float.MinValue;
                for (int y = 0; y < td.heightmapResolution; y++)
                {
                    for (int x = 0; x < td.heightmapResolution; x++)
                    {
                        float h = heights[y, x];
                        if (float.IsNaN(h) || float.IsInfinity(h)) continue;
                        if (h < minT) minT = h;
                        if (h > maxT) maxT = h;
                    }
                }

                if (minT > maxT)
                {
                    sb.AppendLine("- SKIPPED: every heightmap sample was NaN or infinite.");
                    sb.AppendLine();
                    Debug.LogWarning(
                        $"[MeasureAmplitudeTask] Terrain '{t.name}' heightmap is entirely non-finite and " +
                        "was excluded from the amplitude audit.");
                    continue;
                }

                float terrainAmplitude = (maxT - minT) * td.size.y;
                if (terrainAmplitude > widestTerrainAmplitude) widestTerrainAmplitude = terrainAmplitude;
                measuredTerrains++;

                sb.AppendLine($"- **Raw Height 0..1 Min:** {minT:F5}");
                sb.AppendLine($"- **Raw Height 0..1 Max:** {maxT:F5}");
                sb.AppendLine($"- **World Height Min:** {(t.transform.position.y + minT * td.size.y):F2} m");
                sb.AppendLine($"- **World Height Max:** {(t.transform.position.y + maxT * td.size.y):F2} m");
                sb.AppendLine($"- **Terrain Amplitude (Max - Min):** {terrainAmplitude:F2} m");
                sb.AppendLine();
            }

            if (measuredTerrains == 0)
            {
                // The old version wrote this document, logged "report saved", and continued the chain even
                // with zero terrains measured - a one-sided audit that names no scale mismatch because it
                // never had the other side to compare against.
                throw new InvalidOperationException(
                    $"none of the {terrains.Length} Terrain object(s) in the scene yielded a measurable " +
                    "heightmap, so this audit has no live-terrain side to compare the math against. A " +
                    "math-only amplitude figure is not a scale audit.");
            }

            sb.AppendLine("## 3. Verdict");
            sb.AppendLine($"- Math amplitude: {mathAmplitude:F2} m");
            sb.AppendLine($"- Widest measured terrain amplitude: {widestTerrainAmplitude:F2} m over {measuredTerrains} terrain(s)");
            sb.AppendLine($"- Ratio (terrain / math): {(mathAmplitude > 0f ? widestTerrainAmplitude / mathAmplitude : 0f):F3}");

            File.WriteAllText(artifactPath, sb.ToString(), Encoding.UTF8);

            // The headline numbers go into the Unity log too. The report file alone meant every verdict
            // lived in a directory nobody reading the batchmode log would ever open.
            Debug.Log(
                $"[MeasureAmplitudeTask] Amplitude report saved to {artifactPath}. Math amplitude " +
                $"{mathAmplitude:F2} m; widest terrain amplitude {widestTerrainAmplitude:F2} m over " +
                $"{measuredTerrains} terrain(s).");
        }
    }
}
