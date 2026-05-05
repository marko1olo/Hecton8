using System;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;

namespace Hecton8.Editor
{
    public static class PlanetaryCanvasSmokeTestRunner
    {
        private const string ArtifactPath = "CodexArtifacts/planetary-canvas-smoke-2026-05-05.json";

        [MenuItem("HECTON-8/World/Run Planetary Canvas Smoke")]
        public static void RunMenu()
        {
            RunPlanetaryCanvasSmoke();
        }

        public static void RunPlanetaryCanvasSmoke()
        {
            PlanetaryCanvasSmokeTester.Result result = PlanetaryCanvasSmokeTester.RunSlopeCavitySplatmapSmoke();
            WriteReport(result);
            if (!result.Passed)
                throw new InvalidOperationException("Planetary canvas smoke failed.");
        }

        private static void WriteReport(PlanetaryCanvasSmokeTester.Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArtifactPath) ?? ".");
            var builder = new StringBuilder(384);
            builder.AppendLine("{");
            builder.Append("  \"passed\": ").Append(result.Passed ? "true" : "false").AppendLine(",");
            builder.Append("  \"flatSandWeight\": ").Append(result.FlatSandWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"steepRockWeight\": ").Append(result.SteepRockWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"siltWeight\": ").Append(result.SiltWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"cavityWeight\": ").Append(result.CavityWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"checksum\": ").Append(result.Checksum).AppendLine();
            builder.AppendLine("}");
            File.WriteAllText(ArtifactPath, builder.ToString());
        }
    }
}
