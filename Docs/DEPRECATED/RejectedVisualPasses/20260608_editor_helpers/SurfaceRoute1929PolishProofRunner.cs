#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;

namespace Hecton8.EditorTools
{
    public static class SurfaceRoute1929PolishProofRunner
    {
        private const string CaptureRoot = "C:/hades/Hecton8/Docs/Screenshots/MCP";

        public static void ApplyAndExit()
        {
            WriteDisabled1929PolishRouteAndExit("h8_1929_surface_lighting_material_polish_apply");
        }

        public static void CaptureAndExit()
        {
            WriteDisabled1929PolishRouteAndExit("h8_1929_surface_owner_lighting_after_polish");
        }

        private static void WriteDisabled1929PolishRouteAndExit(string proofName)
        {
            Directory.CreateDirectory(CaptureRoot);
            File.WriteAllText(
                Path.Combine(CaptureRoot, proofName + ".txt"),
                "captureTruth=disabled_diagnostic_route\n" +
                "captureName=" + proofName + "\n" +
                "route=SurfaceRoute1929PolishProofRunner.disabled_mutating_diagnostic_route\n" +
                "status=REJECTED_DISABLED_DIRECT_EXECUTE_METHOD\n" +
                "reason=surface route 1929 polish route is quarantined and cannot be used as canonical acceptance proof\n",
                Encoding.UTF8);
            EditorApplication.Exit(0);
        }
    }
}
#endif
