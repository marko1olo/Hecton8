#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class H8VisualProofCapture1912
    {
        private const string CaptureRoot = "C:/hades/Hecton8/Docs/RejectedDiagnostics/H8VisualProofCapture1912";

        public static void CaptureSurfaceAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_edit_main");
        }

        public static void CaptureSurfaceAfterQuarantineAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_after_quarantine_b");
        }

        public static void CaptureSurfacePatchAAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_patch_a");
        }

        public static void CaptureSurfaceOwnerLightingNonMutatingAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1921_surface_owner_lighting_nonmutating");
        }

        public static void CaptureSurfaceOwnerLightingAfterSceneWiringAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1927_surface_owner_lighting_after_scene_wiring");
        }

        public static void CaptureSurfaceOwnerLightingAfterPolishAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1928_surface_owner_lighting_after_polish");
        }

        public static void ApplySurfaceSceneCrestTerrainWiringAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1926_surface_scene_crest_terrain_wiring_apply");
        }

        public static void ApplySurfaceLightingMaterialPolishAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1928_surface_lighting_material_polish_apply");
        }

        public static void CaptureSurfaceCrestRecoveryProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_crest_recovery_probe");
        }

        public static void CaptureSurfaceCrestAprilRouteProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1915_surface_crest_april_route_probe");
        }

        public static void CaptureSurfaceCrestCleanTerrainProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1916_surface_crest_clean_terrain_probe");
        }

        public static void CaptureSurfaceCrestDaylightProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1917_surface_crest_daylight_probe");
        }

        public static void CaptureSurfaceCrestCoastHorizonProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1918_surface_crest_coast_horizon_probe");
        }

        public static void CaptureSurfaceCrestSkyCardHorizonProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1919_surface_crest_skycard_horizon_probe");
        }

        private static void WriteDisabledDiagnosticRouteAndExit(string captureName)
        {
            Directory.CreateDirectory(CaptureRoot);
            string path = Path.Combine(CaptureRoot, captureName + "_rejected.txt");
            File.WriteAllText(
                path,
                "captureTruth=disabled_diagnostic_route\n" +
                "captureName=" + captureName + "\n" +
                "route=H8VisualProofCapture1912.disabled_mutating_diagnostic_route\n" +
                "status=REJECTED_DISABLED_DIRECT_EXECUTE_METHOD\n" +
                "reason=mutating diagnostic proof route is quarantined and cannot be used as canonical acceptance proof\n",
                Encoding.UTF8);
            Debug.Log("[H8VisualProofCapture1912] Disabled diagnostic route rejected: " + path);
            EditorApplication.Exit(2);
        }

        public static void CaptureSurfaceCrestOceanExtentProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1920_surface_crest_ocean_extent_probe");
        }

        public static void CaptureSurfaceCrestFlatSkyHorizonProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1922_surface_crest_flat_sky_horizon_probe");
        }

        public static void CaptureSurfaceCrestPureOceanFlatSkyProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1923_surface_crest_pure_ocean_flat_sky_probe");
        }

        public static void CaptureSurfaceCrestPureOceanUniformSkyProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1925_surface_crest_pure_ocean_uniform_sky_probe");
        }

        public static void CaptureSurfaceFlatSkyOnlyProbeAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("h8_1924_surface_flat_sky_only_probe");
        }

        public static void CaptureShallowUnderwaterPatchAAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_underwater_0_5m_patch_a");
        }

        public static void CaptureRouteUnderwaterPatchAAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_underwater_20_50m_patch_a");
        }

        public static void QuarantineSurfaceRejectsAndExit()
        {
            WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_quarantine");
        }

    }
}
#endif
