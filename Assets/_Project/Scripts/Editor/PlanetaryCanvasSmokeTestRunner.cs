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

        [MenuItem("Hecton8/World/Run Planetary Canvas Smoke")]
        public static void RunMenu()
        {
            RunPlanetaryCanvasSmoke();
        }

        public static void RunPlanetaryCanvasSmoke()
        {
            Hecton8.World.PlanetaryCanvasSmokeTester.Result result =
                Hecton8.World.PlanetaryCanvasSmokeTester.RunSlopeCavitySplatmapSmoke();
            WriteReport(result);
            if (result.Passed == 0)
                throw new InvalidOperationException("Planetary canvas smoke failed.");
        }

        private static void WriteReport(Hecton8.World.PlanetaryCanvasSmokeTester.Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArtifactPath) ?? ".");
            var builder = new StringBuilder(384);
            builder.AppendLine("{");
            builder.Append("  \"passed\": ").Append(result.Passed != 0 ? "true" : "false").AppendLine(",");
            builder.Append("  \"flatSandWeight\": ").Append(result.FlatSandWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"steepRockWeight\": ").Append(result.SteepRockWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"siltWeight\": ").Append(result.SiltWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"cavityWeight\": ").Append(result.CavityWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"macroMaterialDelta\": ").Append(result.MacroMaterialDelta.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"macroSandWeight\": ").Append(result.MacroSandWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"macroRockWeight\": ").Append(result.MacroRockWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"macroSiltWeight\": ").Append(result.MacroSiltWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"checksum\": ").Append(result.Checksum).AppendLine(",");
            builder.Append("  \"macroChecksum\": ").Append(result.MacroChecksum).AppendLine();
            builder.AppendLine("}");
            File.WriteAllText(ArtifactPath, builder.ToString(), new UTF8Encoding(false));
        }
    }
}
