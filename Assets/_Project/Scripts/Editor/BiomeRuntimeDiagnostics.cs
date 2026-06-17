using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Editor
{
    public static class BiomeRuntimeDiagnostics
    {
        [InitializeOnLoadMethod]
        public static void RunAudit()
        {
            if (SessionState.GetBool("BiomeAuditRan", false)) return;
            SessionState.SetBool("BiomeAuditRan", true);

            Debug.Log("[BiomeRuntimeDiagnostics] Starting Runtime Biome Audit...");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Runtime Biome Diagnostics Report");
            sb.AppendLine("Это автоматический срез процедурной генерации, подтверждающий наличие разнообразных биомов в зависимости от высоты (глубины) и масок тектоники.");
            sb.AppendLine("");
            sb.AppendLine("| X (Meters) | Z (Meters) | Depth (m) | Primary Zone | Dominant Material | Scatter Flags |");
            sb.AppendLine("|---|---|---|---|---|---|");

            WorldMacroGeologyParams p = new WorldMacroGeologyParams { Seed = 880031 }; // DefaultAuthoringSeed
            WorldTerrainMesoDetailParams mesoParams = new WorldTerrainMesoDetailParams { Seed = 880031 };

            // Sample a 30km cross-section
            for (float x = -15000f; x <= 15000f; x += 1500f)
            {
                // Take a few Z samples
                for (float z = -2000f; z <= 2000f; z += 1000f)
                {
                    WorldMacroGeologySample macro = WorldMacroGeologyFields.Evaluate(x, z, in p);
                    WorldTerrainSurfaceMaterialWeights weights = WorldTerrainSurfaceMaterialResolver.Resolve(in macro, x, z, p.Seed);
                    WorldTerrainMesoDetailSample meso = WorldTerrainMesoDetailFields.Evaluate(in macro, x, z, in mesoParams);
                    WorldTerrainDetailEligibilityFlags flags = WorldTerrainMesoDetailFields.ResolveEligibilityFlags(in macro, in meso, in weights);

                    WorldTerrainSurfaceMaterialClass dominant = WorldTerrainSurfaceMaterialResolver.ResolveDominant(in weights);

                    // Only show 1 decimal for depth
                    sb.AppendLine($"| {x} | {z} | {macro.DepthMeters:F1} | {macro.PrimaryZone} | {dominant} | {flags} |");
                }
            }

            string path = "C:/Users/danat/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4/scratch/BiomeOutput.md";
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[BiomeRuntimeDiagnostics] Audit complete. Saved to {path}");
        }
    }
}
