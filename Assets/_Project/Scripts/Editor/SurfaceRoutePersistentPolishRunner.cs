#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class SurfaceRoutePersistentPolishRunner
    {
        private const string CaptureRoot = "C:/hades/Hecton8/Docs/Screenshots/MCP";

        public static void DeferredApplyAndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");
        }

        public static void DeferredCaptureAndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_owner_lighting_after_polish");
        }

        public static void ApplyAndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");
        }

        public static void CaptureAndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_owner_lighting_after_polish");
        }

        public static void WriteDisabledPersistentPolishRouteAndExit(string proofName)
        {
            Directory.CreateDirectory(CaptureRoot);
            string proofPath = Path.Combine(CaptureRoot, proofName + ".txt");
            File.WriteAllText(
                proofPath,
                "captureTruth=disabled_diagnostic_route\n" +
                "captureName=" + proofName + "\n" +
                "route=SurfaceRoutePersistentPolishRunner.disabled_mutating_diagnostic_route\n" +
                "status=REJECTED_DISABLED_DIRECT_EXECUTE_METHOD\n" +
                "reason=persistent surface polish route is quarantined and cannot be used as canonical acceptance proof\n",
                Encoding.UTF8);
            Debug.Log("[SurfaceRoutePersistentPolishRunner] Disabled route wrote " + proofPath);
            EditorApplication.Exit(0);
        }

        internal static void ApplyAuthoringRoute1930AndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_authoring_apply");
        }

        internal static void CaptureAuthoringRoute1930AndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_owner_lighting_capture");
        }

        internal static void ApplyAuthoringRoute1931AndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_authoring_apply");
        }

        internal static void CaptureAuthoringRoute1931AndExit()
        {
            WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_owner_lighting_capture");
        }
    }
}
#endif
