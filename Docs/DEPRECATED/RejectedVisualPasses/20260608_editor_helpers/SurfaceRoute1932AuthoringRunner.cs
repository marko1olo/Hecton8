#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class SurfaceRoute1932AuthoringRunner
    {
        private const string CaptureRoot = "C:/hades/Hecton8/Docs/Screenshots/MCP";

        public static void ApplyAndExit()
        {
            WriteDisabled1932AuthoringRouteAndExit("h8_surface_route1932_authoring_apply");
        }

        public static void CaptureAndExit()
        {
            WriteDisabled1932AuthoringRouteAndExit("h8_surface_route1932_reference_view");
        }

        private static void WriteDisabled1932AuthoringRouteAndExit(string proofName)
        {
            Directory.CreateDirectory(CaptureRoot);
            string proofPath = Path.Combine(CaptureRoot, proofName + ".txt");
            File.WriteAllText(
                proofPath,
                "captureTruth=disabled_diagnostic_route\n" +
                "captureName=" + proofName + "\n" +
                "route=SurfaceRoute1932AuthoringRunner.disabled_mutating_diagnostic_route\n" +
                "status=REJECTED_DISABLED_DIRECT_EXECUTE_METHOD\n" +
                "reason=surface route 1932 authoring route is quarantined and cannot be used as canonical acceptance proof\n",
                Encoding.UTF8);
            Debug.Log("[SurfaceRoute1932AuthoringRunner] Disabled route wrote " + proofPath);
            EditorApplication.Exit(0);
        }
    }
}
#endif
