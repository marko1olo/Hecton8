using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hecton8.World;
using MapMagic.Core;
using MapMagic.Nodes.MatrixGenerators;

namespace Hecton8.Diagnostics
{
    public static class MeasureAmplitudeTask
    {
        public static void Run()
        {
            Debug.Log("=============================================");
            Debug.Log("TASK 6: AMPLITUDE AND SCALE AUDIT");
            Debug.Log("=============================================");

            string artifactPath = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\amplitude_report.md";
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

            MapMagicObject mm = UnityEngine.Object.FindAnyObjectByType<MapMagicObject>();
            if (mm != null && mm.graph != null)
            {
                HectonSandboxAbyssalShelfMapMagicNode macroBaseNode = null;
                foreach (var gen in mm.graph.generators)
                {
                    if (gen is HectonSandboxAbyssalShelfMapMagicNode node)
                    {
                        macroBaseNode = node;
                        break;
                    }
                }
                
                if (macroBaseNode != null)
                {
                    p.HighWorldY = macroBaseNode.highWorldY;
                    p.LowWorldY = macroBaseNode.lowWorldY;
                    p.RidgeHeightMeters = macroBaseNode.ridgeHeightMeters;
                    p.TrenchDepthMeters = macroBaseNode.trenchDepthMeters;
                    p.Seed = unchecked((uint)macroBaseNode.seed);
                    sb.AppendLine($"Extracted parameters from MapMagic Graph: HighY={p.HighWorldY}, LowY={p.LowWorldY}, Ridge={p.RidgeHeightMeters}, Trench={p.TrenchDepthMeters}, Seed={p.Seed}");
                }
            }

            float minMath = float.MaxValue;
            float maxMath = float.MinValue;
            double sumMath = 0;
            int countMath = 0;

            for (double x = -5000; x <= 5000; x += 100)
            {
                for (double z = -5000; z <= 5000; z += 100)
                {
                    float h = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(x, z, in p);
                    if (h < minMath) minMath = h;
                    if (h > maxMath) maxMath = h;
                    sumMath += h;
                    countMath++;
                }
            }

            sb.AppendLine($"- **Min Height:** {minMath:F2} m");
            sb.AppendLine($"- **Max Height:** {maxMath:F2} m");
            sb.AppendLine($"- **Avg Height:** {(sumMath / countMath):F2} m");
            sb.AppendLine($"- **Amplitude (Max - Min):** {(maxMath - minMath):F2} m");
            sb.AppendLine();

            sb.AppendLine("## 2. TerrainData Parameters (Live Scene)");
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            sb.AppendLine($"Found {terrains.Length} Terrain objects in the scene.");
            
            foreach (var t in terrains)
            {
                var td = t.terrainData;
                if (td != null)
                {
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
                            if (h < minT) minT = h;
                            if (h > maxT) maxT = h;
                        }
                    }
                    
                    sb.AppendLine($"- **Raw Height 0..1 Min:** {minT:F5}");
                    sb.AppendLine($"- **Raw Height 0..1 Max:** {maxT:F5}");
                    sb.AppendLine($"- **World Height Min:** {(t.transform.position.y + minT * td.size.y):F2} m");
                    sb.AppendLine($"- **World Height Max:** {(t.transform.position.y + maxT * td.size.y):F2} m");
                    sb.AppendLine($"- **Terrain Amplitude (Max - Min):** {((maxT - minT) * td.size.y):F2} m");
                    sb.AppendLine();
                }
            }

            File.WriteAllText(artifactPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"Amplitude report saved to {artifactPath}");

            // Continue to next task or exit
            HeadlessRunAll.NextTask();
        }
    }
}
