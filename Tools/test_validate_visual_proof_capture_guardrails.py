import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

TEST_TEMP_ROOT = Path("C:/tmp")
TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
tempfile.tempdir = str(TEST_TEMP_ROOT)

import ValidateVisualProofCaptureGuardrails as validator  # noqa: E402


class ValidateVisualProofCaptureGuardrailsTests(unittest.TestCase):
    def test_source_risk_scan_detects_mutation_and_diagnostic_tokens(self) -> None:
        source = "\n".join(
            (
                "serialized.ApplyModifiedPropertiesWithoutUndo();",
                "EditorSceneManager.MarkSceneDirty(scene);",
                "EditorSceneManager.SaveScene(scene);",
                "var material = new Material(shader);",
                "GameObject.CreatePrimitive(PrimitiveType.Quad);",
                "WriteMetadata(camera, path, \"surface_water_recovery_probe_editor_only_unsaved\");",
            )
        )

        risks = validator.find_source_risks(source)

        self.assertEqual(6, len(risks))
        self.assertEqual(
            {
                "serialized_object_mutation",
                "scene_dirty_mark",
                "scene_save",
                "editor_material_clone",
                "editor_probe_geometry",
                "diagnostic_unsaved_capture",
            },
            {risk.category for risk in risks},
        )

    def test_required_terms_reject_missing_guardrail_text(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "guardrail.md"
            path.write_text("H8VisualProofCapture1912\n", encoding="utf-8")

            with self.assertRaises(SystemExit):
                validator.validate_required_terms({path: ("H8VisualProofCapture1912", "editor_only_unsaved")})

    def test_asset_reference_scan_rejects_missing_paths(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = 'private const string ShaderPath = "Assets/_Project/Art/Shaders/Missing.shader";'

            references = validator.find_asset_references(source, root=root)

            self.assertEqual(1, len(references))
            self.assertFalse(references[0].exists)
            with self.assertRaises(SystemExit):
                validator.validate_asset_references(references)

    def test_stale_source_term_rejects_docs_when_absent_from_source(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "guardrail.md"
            path.write_text("SurfaceWaterReadabilityShaderPath\n", encoding="utf-8")

            with self.assertRaises(SystemExit):
                validator.validate_no_stale_source_terms("SurfaceHorizonHazeShaderPath", {path: ()})

    def test_current_guardrail_docs_route_current_source_risks(self) -> None:
        risks = validator.validate_guardrails()

        self.assertEqual([], risks)
        source = validator.load_text(validator.SOURCE_PATH)
        self.assertIn("WriteDisabledDiagnosticRouteAndExit", source)
        self.assertIn("status=REJECTED_DISABLED_DIRECT_EXECUTE_METHOD", source)

    def test_public_execute_route_inventory_rejects_unregistered_entries(self) -> None:
        source = "\n".join(
            f"public static void {route}() {{ }}"
            for route in sorted(validator.EXPECTED_PUBLIC_EXECUTE_ROUTES)
        )
        validator.validate_public_execute_route_inventory(source)

        with self.assertRaises(SystemExit):
            validator.validate_public_execute_route_inventory(source + "\npublic static void CaptureSurfaceNewFalseProofAndExit() { }")

    def test_unsafe_public_routes_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void CaptureSurfaceCrestRecoveryProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_crest_recovery_probe");',
                "}",
                "public static void CaptureSurfaceCrestAprilRouteProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1915_surface_crest_april_route_probe");',
                "}",
                "public static void CaptureSurfaceCrestCleanTerrainProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1916_surface_crest_clean_terrain_probe");',
                "}",
                "public static void CaptureSurfaceCrestDaylightProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1917_surface_crest_daylight_probe");',
                "}",
                "public static void CaptureSurfaceCrestCoastHorizonProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1918_surface_crest_coast_horizon_probe");',
                "}",
                "public static void CaptureSurfaceOwnerLightingNonMutatingAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1921_surface_owner_lighting_nonmutating");',
                "}",
                "public static void CaptureSurfaceCrestOceanExtentProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1920_surface_crest_ocean_extent_probe");',
                "}",
                "public static void QuarantineSurfaceRejectsAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("disabled_legacy_surface_quarantine");',
                "}",
            )
        )
        validator.validate_unsafe_public_routes_disabled(disabled_source)

        enabled_source = disabled_source.replace(
            'WriteDisabledDiagnosticRouteAndExit("h8_1920_surface_crest_ocean_extent_probe");',
            'CaptureSurfaceCrestProbeAndExit("h8_1920_surface_crest_ocean_extent_probe");',
        )
        with self.assertRaises(SystemExit):
            validator.validate_unsafe_public_routes_disabled(enabled_source)

    def test_disabled_route_rejector_has_no_allow_exceptions(self) -> None:
        denied_source = "\n".join(
            (
                "private static bool RejectDisabledMutatingDiagnosticRoute(string captureName)",
                "{",
                "    WriteDisabledDiagnosticRouteAndExit(captureName);",
                "    return true;",
                "}",
            )
        )
        validator.validate_disabled_route_rejector_has_no_allow_exceptions(denied_source)

        allowed_source = denied_source.replace(
            "WriteDisabledDiagnosticRouteAndExit(captureName);",
            'if (string.Equals(captureName, "h8_1921_surface_owner_lighting_nonmutating", StringComparison.Ordinal))\n'
            "        return false;\n"
            "    WriteDisabledDiagnosticRouteAndExit(captureName);",
        )
        with self.assertRaises(SystemExit):
            validator.validate_disabled_route_rejector_has_no_allow_exceptions(allowed_source)

    def test_harness_banned_token_categories_are_rejected(self) -> None:
        source = "\n".join(
            (
                "EditorSceneManager.SaveScene(scene);",
                "EditorSceneManager.MarkSceneDirty(scene);",
                "AssetDatabase.ImportAsset(shaderPath);",
                "serialized.ApplyModifiedPropertiesWithoutUndo();",
                "haze.SetActive(true);",
                "behaviour.enabled = false;",
                "renderer.enabled = false;",
                "hazeRenderer.enabled = true;",
                "camera.transform.position = position;",
                "mainCamera.nearClipPlane = 0.03f;",
                "camera.cullingMask |= 1 << waterLayer;",
                'MethodInfo runUpdate = oceanRenderer.GetType().GetMethod("RunUpdate", BindingFlags.Instance | BindingFlags.NonPublic);',
                "runUpdate.Invoke(oceanRenderer, null);",
                "System.Threading.Thread.Sleep(33);",
                "EditorApplication.QueuePlayerLoopUpdate();",
                "SceneView.RepaintAll();",
                'EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");',
                "var cam = Camera.main;",
                "var player = FindAnyObjectByType<HectonPlayerMovement>();",
                "var allPlayers = Resources.FindObjectsOfTypeAll<HectonPlayerMovement>();",
                'var shell = GameObject.Find("Player");',
                'if (target.CompareTag("Player")) return;',
                "private static Component _surfaceCrestProbeOceanRenderer;",
                "UnityEngine.Object.DestroyImmediate(_surfaceCrestProbeMaterial);",
                "var material = new Material(shader);",
                "material.hideFlags = HideFlags.HideAndDontSave;",
                "renderer.sharedMaterial = material;",
                "terrain.materialTemplate = terrainMaterial;",
                "data.terrainLayers = fallbackLayers;",
                "terrain.drawInstanced = true;",
                'SetSerializedFloat(serialized, "globals.height", 32f);',
                "mapMagicObject.globals.height = 32f;",
                "mapMagicObject.tiles.Pin(coord, asDraft: false, holder: mapMagicObject);",
                "mapMagicObject.tiles.ChangeDists(coords);",
                "mapMagicObject.Refresh(clearAll: true);",
                "mapMagicObject.StartGenerate(main: true, draft: true);",
                "PumpMapMagicGeneration(mapMagicObject, 90.0f);",
                "mapMagicObject.Update();",
                "Den.Tools.Tasks.CoroutineManager.Update();",
                "camera.Render();",
                "readback.ReadPixels(rect, 0, 0);",
                "byte[] png = readback.EncodeToPNG();",
                'private const string CaptureRoot = "C:/hades/Hecton8/Docs/Screenshots/MCP";',
                'private const string OtherCaptureRoot = "Docs/Screenshots/Debug";',
                'CaptureSurfaceAndExit("h8_1914_surface_crest_recovery_probe");',
            )
        )

        result = validator.validate_harness_candidate_source(source)

        self.assertEqual(validator.HARNESS_REJECTED_STATUS, result.status)
        self.assertEqual(
            {
                "scene_save",
                "scene_dirty_mark",
                "asset_import_mutation",
                "serialized_object_mutation",
                "active_state_mutation",
                "behaviour_enabled_mutation",
                "renderer_enabled_mutation",
                "transform_mutation",
                "camera_render_state_mutation",
                "private_reflection_invoke",
                "editor_thread_sleep",
                "editor_loop_pump",
                "scene_open_mutation",
                "scene_search_camera_heuristic",
                "scene_search_heuristic",
                "witness_name_tag_heuristic",
                "static_probe_object_state",
                "object_destroy_mutation",
                "editor_material_clone",
                "temporary_hidden_material",
                "shared_material_mutation",
                "terrain_material_mutation",
                "terrain_layer_mutation",
                "terrain_presentation_mutation",
                "mapmagic_globals_serialized_mutation",
                "mapmagic_global_height_mutation",
                "mapmagic_tile_pin",
                "mapmagic_tile_distance_mutation",
                "mapmagic_refresh",
                "mapmagic_generation",
                "mapmagic_generation_pump",
                "mapmagic_update_pump",
                "mapmagic_coroutine_pump",
                "raw_camera_render",
                "raw_read_pixels",
                "raw_png_encode",
                "raw_mcp_output",
                "noncanonical_screenshot_output",
                "legacy_diagnostic_capture_id",
            },
            {violation.category for violation in result.violations},
        )

    def test_canonical_harness_rejects_scene_search_and_noncanonical_output(self) -> None:
        source = "\n".join(
            (
                'private const string PacketRoot = "Docs/Screenshots/Debug/h8_1475_fake";',
                "var camera = Camera.main;",
                "var player = Object.FindFirstObjectByType<HectonPlayerMovement>();",
                "var tagged = GameObject.FindGameObjectWithTag(\"Player\");",
                "WriteManifest(player, camera, tagged);",
            )
        )

        result = validator.validate_harness_candidate_source(source, strict=True)

        self.assertEqual(validator.HARNESS_REJECTED_STATUS, result.status)
        self.assertEqual(
            {
                "noncanonical_screenshot_output",
                "scene_search_camera_heuristic",
                "scene_search_heuristic",
            },
            {violation.category for violation in result.violations},
        )

    def test_clean_no_mutation_fixture_passes_canonical_harness_gate(self) -> None:
        source = "\n".join(
            (
                'private const string PacketRoot = "Docs/Screenshots/HectonProofPackets/h8_1475_s01";',
                "ReadOnlySerializedState state = ReadRouteState();",
                "WriteManifest(state);",
                "WriteManifestSha256();",
            )
        )

        result = validator.validate_harness_candidate_source(source, strict=True)

        self.assertEqual(validator.HARNESS_PASS_STATUS, result.status)
        self.assertFalse(result.violations)

    def test_autorun_editor_hooks_are_rejected_as_hidden_execution_path(self) -> None:
        source = "\n".join(
            (
                "// diagnostic rejection-only compile follow-up",
                "[InitializeOnLoadMethod]",
                "private static void RunSurfaceCrestRecoveryProbeAutorun()",
                "{",
                "    if (!File.Exists(SurfaceCrestAutoRunFlagPath))",
                "    {",
                "        return;",
                "    }",
                "    EditorApplication.delayCall += RunSurfaceCrestRecoveryProbeAutorun;",
                "    File.Delete(SurfaceCrestAutoRunFlagPath);",
                "    CaptureSurfaceCrestRecoveryProbeAndExit();",
                "}",
                'private const string SurfaceCrestAutoRunFlagPath = CaptureRoot + "/h8_1914_surface_crest_recovery_probe.autorun";',
            )
        )

        result = validator.validate_harness_candidate_source(
            source,
            allow_diagnostic_rejection=True,
            strict=True,
        )

        self.assertEqual(validator.HARNESS_REJECTED_STATUS, result.status)
        self.assertEqual(
            {
                "editor_initialize_on_load_autorun",
                "editor_delay_call_autorun",
                "autorun_marker_file",
                "autorun_marker_lifecycle",
                "legacy_diagnostic_capture_id",
            },
            {violation.category for violation in result.violations},
        )

    def test_diagnostic_rejection_may_mention_h8_1475_as_refused_proof(self) -> None:
        source = "\n".join(
            (
                "// diagnostic rejection-only route",
                'File.WriteAllText("Docs/Screenshots/MCP/h8_1914_rejected.txt",',
                '    "reason=cannot be used as h8_1475 acceptance proof");',
                "EditorSceneManager.MarkSceneDirty(scene);",
            )
        )

        result = validator.validate_harness_candidate_source(
            source,
            allow_diagnostic_rejection=True,
        )

        self.assertEqual(validator.DIAGNOSTIC_PASS_STATUS, result.status)
        self.assertTrue(result.diagnostic_only)

    def test_live_visual_proof_request_markers_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            marker_dir = root / "Docs" / "Screenshots" / "MCP"
            marker_dir.mkdir(parents=True)
            request_marker = marker_dir / "h8_visual_proof_request.txt"
            request_marker.write_text("1919", encoding="utf-8")
            autorun_marker = marker_dir / "h8_1914_surface_crest_recovery_probe.autorun"
            autorun_marker.write_text("1", encoding="utf-8")

            markers = validator.find_live_autorun_markers(root)

            self.assertEqual(
                {
                    request_marker,
                    autorun_marker,
                },
                set(markers),
            )
            with self.assertRaises(SystemExit):
                validator.validate_no_live_autorun_markers(root)

    def test_hidden_visual_proof_autorun_source_terms_are_rejected(self) -> None:
        source = "\n".join(
            (
                "[InitializeOnLoad]",
                "public static class H8VisualProofCapture1912",
                "{",
                '    private const string VisualProofRequestFilePath = "h8_visual_proof_request.txt";',
                "    private static void RunRequestedVisualProofWhenEditorReady()",
                "    {",
                "        EditorApplication.delayCall += RunRequestedVisualProofWhenEditorReady;",
                "    }",
                "}",
            )
        )

        with self.assertRaises(SystemExit):
            validator.validate_no_hidden_autorun_source(source)

    def test_h8_1919_direct_execute_route_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void CaptureSurfaceCrestSkyCardHorizonProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1919_surface_crest_skycard_horizon_probe");',
                "}",
            )
        )
        enabled_source = "\n".join(
            (
                "public static void CaptureSurfaceCrestSkyCardHorizonProbeAndExit()",
                "{",
                "    CaptureSurfaceCrestProbeAndExit(",
                '        "h8_1919_surface_crest_skycard_horizon_probe",',
                "        useAprilCrestFeatureStack: true);",
                "}",
            )
        )

        validator.validate_skycard_direct_execute_route_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_skycard_direct_execute_route_disabled(enabled_source)

    def test_h8_1924_flat_sky_direct_execute_route_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void CaptureSurfaceFlatSkyOnlyProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1924_surface_flat_sky_only_probe");',
                "}",
            )
        )
        enabled_source = "\n".join(
            (
                "public static void CaptureSurfaceFlatSkyOnlyProbeAndExit()",
                "{",
                "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
                "    ConfigureFlatBrightSkyProbe();",
                "    RenderCamera(mainCamera, outputPath);",
                "}",
            )
        )

        validator.validate_flat_sky_direct_execute_route_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_flat_sky_direct_execute_route_disabled(enabled_source)

    def test_pure_ocean_direct_execute_routes_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void CaptureSurfaceCrestFlatSkyHorizonProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1922_surface_crest_flat_sky_horizon_probe");',
                "}",
                "public static void CaptureSurfaceCrestPureOceanFlatSkyProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1923_surface_crest_pure_ocean_flat_sky_probe");',
                "}",
                "public static void CaptureSurfaceCrestPureOceanUniformSkyProbeAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1925_surface_crest_pure_ocean_uniform_sky_probe");',
                "}",
            )
        )
        enabled_source = disabled_source.replace(
            'WriteDisabledDiagnosticRouteAndExit("h8_1925_surface_crest_pure_ocean_uniform_sky_probe");',
            "CaptureSurfaceCrestProbeAndExit(\n"
            '        "h8_1925_surface_crest_pure_ocean_uniform_sky_probe",\n'
            "        usePureOceanFlatSkyProbe: true,\n"
            "        useUniformBrightSkyProbe: true);",
        )

        validator.validate_pure_ocean_direct_execute_routes_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_pure_ocean_direct_execute_routes_disabled(enabled_source)

    def test_persistent_scene_wiring_routes_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void ApplySurfaceSceneCrestTerrainWiringAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1926_surface_scene_crest_terrain_wiring_apply");',
                "}",
                "public static void CaptureSurfaceOwnerLightingAfterSceneWiringAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1927_surface_owner_lighting_after_scene_wiring");',
                "}",
                "public static void CaptureSurfaceOwnerLightingAfterPolishAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1928_surface_owner_lighting_after_polish");',
                "}",
                "public static void ApplySurfaceLightingMaterialPolishAndExit()",
                "{",
                '    WriteDisabledDiagnosticRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
                "}",
            )
        )
        enabled_source = disabled_source.replace(
            'WriteDisabledDiagnosticRouteAndExit("h8_1926_surface_scene_crest_terrain_wiring_apply");',
            "Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);\n"
            "    EditorSceneManager.MarkSceneDirty(scene);\n"
            "    AssetDatabase.SaveAssets();\n"
            "    EditorSceneManager.SaveScene(scene);",
        )

        validator.validate_persistent_scene_wiring_routes_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_persistent_scene_wiring_routes_disabled(enabled_source)

        enabled_after_polish = disabled_source.replace(
            'WriteDisabledDiagnosticRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
            "ApplySurfaceLightingMaterialPolishInternalAndExit();",
        )
        with self.assertRaises(SystemExit):
            validator.validate_persistent_scene_wiring_routes_disabled(enabled_after_polish)

    def test_surface_route_persistent_polish_bypass_must_stay_disabled(self) -> None:
        runner_disabled = "\n".join(
            (
                "public static void DeferredApplyAndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
                "}",
                "public static void DeferredCaptureAndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_owner_lighting_after_polish");',
                "}",
                "public static void ApplyAndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
                "}",
                "public static void CaptureAndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_owner_lighting_after_polish");',
                "}",
                "public static void WriteDisabledPersistentPolishRouteAndExit(string proofName)",
                "{",
                "}",
                "internal static void ApplyAuthoringRoute1930AndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_authoring_apply");',
                "}",
                "internal static void CaptureAuthoringRoute1930AndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_owner_lighting_capture");',
                "}",
                "internal static void ApplyAuthoringRoute1931AndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_authoring_apply");',
                "}",
                "internal static void CaptureAuthoringRoute1931AndExit()",
                "{",
                '    WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_owner_lighting_capture");',
                "}",
            )
        )
        fixer_disabled = "\n".join(
            (
                "public static void AssignAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_crest_material_assign");',
                "}",
                "public static void ForceTextReserializeWorldSceneAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_scene_force_text_reserialize");',
                "}",
                "public static void ApplySurfaceRoutePersistentPolishAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
                "}",
                "public static void InvokeSurfaceRoutePrivatePolishAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_private_polish_invoke");',
                "}",
            )
        )

        validator.validate_surface_route_persistent_polish_bypass_disabled(runner_disabled, fixer_disabled)
        validator.validate_surface_route_persistent_polish_bypass_disabled(runner_disabled, None)

        runner_enabled = runner_disabled.replace(
            'WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
            "ApplyInternalAndExit();",
        )
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_enabled, fixer_disabled)

        runner_hidden_autorun = runner_disabled + "\n[InitializeOnLoadMethod]\nprivate static void RunDeferredModeIfRequested() { EditorApplication.delayCall += ApplyInternalAndExit; }"
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_hidden_autorun, fixer_disabled)

        runner_internal_authoring_enabled = runner_disabled.replace(
            'WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_authoring_apply");',
            "Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);\n    ApplyReadableMaterials();",
        )
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_internal_authoring_enabled, fixer_disabled)

        runner_unexpected_public = runner_disabled + "\npublic static void NewSurfacePolishBypassAndExit() { }"
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_unexpected_public, fixer_disabled)

        fixer_enabled = fixer_disabled.replace(
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_lighting_material_polish_apply");',
            "SurfaceRoutePersistentPolishRunner.ApplyAndExit();",
        )
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_disabled, fixer_enabled)

        fixer_unexpected_public = fixer_disabled + "\npublic static void NewFixerBypassAndExit() { }"
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_disabled, fixer_unexpected_public)

        fixer_private_invoke = fixer_disabled.replace(
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_private_polish_invoke");',
            'System.Reflection.MethodInfo method = typeof(SurfaceRoutePersistentPolishRunner).GetMethod("ApplyInternalAndExit");\n    method.Invoke(null, null);',
        )
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_persistent_polish_bypass_disabled(runner_disabled, fixer_private_invoke)

    def test_h8_editor_bridge_1297_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void RunAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_1928_surface_bridge1297_private_polish_invoke");',
                "}",
            )
        )
        enabled_source = "\n".join(
            (
                "public static void RunAndExit()",
                "{",
                '    MethodInfo method = runnerType.GetMethod("ApplyInternalAndExit", BindingFlags.Static);',
                "    method.Invoke(null, null);",
                "}",
            )
        )
        unexpected_public_source = disabled_source + "\npublic static void NewBridgeAndExit() { }"

        validator.validate_h8_editor_bridge_1297_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_h8_editor_bridge_1297_disabled(enabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_h8_editor_bridge_1297_disabled(unexpected_public_source)

    def test_surface_route_1929_polish_runner_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void ApplyAndExit()",
                "{",
                '    WriteDisabled1929PolishRouteAndExit("h8_1929_surface_lighting_material_polish_apply");',
                "}",
                "public static void CaptureAndExit()",
                "{",
                '    WriteDisabled1929PolishRouteAndExit("h8_1929_surface_owner_lighting_after_polish");',
                "}",
            )
        )
        enabled_apply_source = disabled_source.replace(
            'WriteDisabled1929PolishRouteAndExit("h8_1929_surface_lighting_material_polish_apply");',
            "Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);\n    ApplyReadableMaterials();",
        )
        enabled_capture_source = disabled_source.replace(
            'WriteDisabled1929PolishRouteAndExit("h8_1929_surface_owner_lighting_after_polish");',
            "ResolveMainCamera();\n    RenderCamera(null, capturePath);",
        )
        hidden_autorun_source = (
            disabled_source
            + "\n[InitializeOnLoadMethod]\nprivate static void RunDeferredModeIfRequested() { EditorApplication.delayCall += ApplyAndExit; }"
        )
        unexpected_public_source = disabled_source + "\npublic static void New1929BypassAndExit() { }"

        validator.validate_surface_route_1929_polish_runner_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1929_polish_runner_disabled(enabled_apply_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1929_polish_runner_disabled(enabled_capture_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1929_polish_runner_disabled(hidden_autorun_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1929_polish_runner_disabled(unexpected_public_source)

    def test_surface_route_1930_authoring_bridge_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void ApplyAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_authoring_apply");',
                "}",
                "public static void CaptureAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_owner_lighting_capture");',
                "}",
            )
        )
        enabled_source = disabled_source.replace(
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1930_authoring_apply");',
            "SurfaceRoutePersistentPolishRunner.ApplyAuthoringRoute1930AndExit();",
        )
        unexpected_public_source = disabled_source + "\npublic static void New1930BypassAndExit() { }"

        validator.validate_surface_route_1930_authoring_bridge_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1930_authoring_bridge_disabled(enabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1930_authoring_bridge_disabled(unexpected_public_source)

    def test_surface_scene_1931_authoring_bridge_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void ApplyAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_authoring_apply");',
                "}",
                "public static void CaptureAndExit()",
                "{",
                '    SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_owner_lighting_capture");',
                "}",
            )
        )
        enabled_source = disabled_source.replace(
            'SurfaceRoutePersistentPolishRunner.WriteDisabledPersistentPolishRouteAndExit("h8_surface_route1931_authoring_apply");',
            "SurfaceRoutePersistentPolishRunner.ApplyAuthoringRoute1931AndExit();",
        )
        unexpected_public_source = disabled_source + "\npublic static void New1931BypassAndExit() { }"

        validator.validate_surface_scene_1931_authoring_bridge_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_scene_1931_authoring_bridge_disabled(enabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_scene_1931_authoring_bridge_disabled(unexpected_public_source)

    def test_surface_route_1932_authoring_runner_must_stay_disabled(self) -> None:
        disabled_source = "\n".join(
            (
                "public static void ApplyAndExit()",
                "{",
                '    WriteDisabled1932AuthoringRouteAndExit("h8_surface_route1932_authoring_apply");',
                "}",
                "public static void CaptureAndExit()",
                "{",
                '    WriteDisabled1932AuthoringRouteAndExit("h8_surface_route1932_reference_view");',
                "}",
            )
        )
        enabled_apply_source = disabled_source.replace(
            'WriteDisabled1932AuthoringRouteAndExit("h8_surface_route1932_authoring_apply");',
            "Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);\n"
            "    ConfigureCrestOcean(camera);\n"
            "    EditorSceneManager.SaveScene(scene);",
        )
        enabled_capture_source = disabled_source.replace(
            'WriteDisabled1932AuthoringRouteAndExit("h8_surface_route1932_reference_view");',
            "ConfigureSurfaceCamera(camera);\n"
            "    RenderCamera(camera, pngPath);\n"
            "    WriteMetadata(camera, metadataPath);",
        )
        hidden_autorun_source = (
            disabled_source
            + "\n[InitializeOnLoadMethod]\nprivate static void RunDeferred1932() { EditorApplication.delayCall += ApplyAndExit; }"
        )
        unexpected_public_source = disabled_source + "\npublic static void New1932BypassAndExit() { }"

        validator.validate_surface_route_1932_authoring_runner_disabled(disabled_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1932_authoring_runner_disabled(enabled_apply_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1932_authoring_runner_disabled(enabled_capture_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1932_authoring_runner_disabled(hidden_autorun_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_route_1932_authoring_runner_disabled(unexpected_public_source)

    def test_shared_surface_crest_route_must_reject_before_scene_open(self) -> None:
        guarded_source = "\n".join(
            (
                "private static void CaptureSurfaceCrestProbeAndExit(",
                "    string captureName,",
                "    bool useLargeOceanExtentProbe)",
                "{",
                "    if (RejectDisabledMutatingDiagnosticRoute(captureName))",
                "        return;",
                "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
                "}",
            )
        )
        unguarded_source = "\n".join(
            (
                "private static void CaptureSurfaceCrestProbeAndExit(",
                "    string captureName,",
                "    bool useLargeOceanExtentProbe)",
                "{",
                "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
                "    if (RejectDisabledMutatingDiagnosticRoute(captureName))",
                "        return;",
                "}",
            )
        )

        validator.validate_surface_crest_shared_route_disabled(guarded_source)
        with self.assertRaises(SystemExit):
            validator.validate_surface_crest_shared_route_disabled(unguarded_source)

    def test_all_shared_capture_routes_must_reject_before_scene_open(self) -> None:
        guarded_source = "\n".join(
            (
                "private static void CaptureSurfaceAndExit(string captureName)",
                "{",
                "    if (RejectDisabledMutatingDiagnosticRoute(captureName))",
                "        return;",
                "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
                "}",
                "private static void CaptureSurfaceCrestProbeAndExit(",
                "    string captureName,",
                "    bool useLargeOceanExtentProbe)",
                "{",
                "    if (RejectDisabledMutatingDiagnosticRoute(captureName))",
                "        return;",
                "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
                "}",
                "private static void CaptureWithPoseAndExit(string captureName, Vector3 position, Vector3 target, string captureTruth)",
                "{",
                "    if (RejectDisabledMutatingDiagnosticRoute(captureName))",
                "        return;",
                "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
                "}",
            )
        )
        unguarded_surface_source = guarded_source.replace(
            "private static void CaptureSurfaceAndExit(string captureName)\n{\n"
            "    if (RejectDisabledMutatingDiagnosticRoute(captureName))\n"
            "        return;\n"
            "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);",
            "private static void CaptureSurfaceAndExit(string captureName)\n{\n"
            "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);\n"
            "    if (RejectDisabledMutatingDiagnosticRoute(captureName))\n"
            "        return;",
        )

        validator.validate_shared_capture_routes_disabled(guarded_source)
        without_pose_source = guarded_source.replace(
            "private static void CaptureWithPoseAndExit(string captureName, Vector3 position, Vector3 target, string captureTruth)\n"
            "{\n"
            "    if (RejectDisabledMutatingDiagnosticRoute(captureName))\n"
            "        return;\n"
            "    Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);\n"
            "}",
            "",
        )
        validator.validate_shared_capture_routes_disabled(without_pose_source)
        with self.assertRaises(SystemExit):
            validator.validate_shared_capture_routes_disabled(unguarded_surface_source)

    def test_current_h8_visual_proof_capture_1912_is_rejected_as_canonical_harness(self) -> None:
        source = validator.load_text(validator.SOURCE_PATH)

        result = validator.validate_harness_candidate_source(source, strict=True)

        self.assertEqual(validator.HARNESS_REJECTED_STATUS, result.status)
        categories = {violation.category for violation in result.violations}
        self.assertNotIn("scene_save", categories)
        self.assertNotIn("scene_dirty_mark", categories)
        self.assertNotIn("raw_camera_render", categories)
        self.assertNotIn("raw_read_pixels", categories)
        self.assertNotIn("raw_png_encode", categories)
        self.assertNotIn("mapmagic_generation", categories)
        self.assertNotIn("private_reflection_invoke", categories)
        self.assertNotIn("camera_render_state_mutation", categories)
        self.assertNotIn("terrain_presentation_mutation", categories)
        self.assertNotIn("mapmagic_global_height_mutation", categories)
        self.assertNotIn("static_probe_object_state", categories)
        self.assertNotIn("object_destroy_mutation", categories)
        self.assertEqual(
            {
                "disabled_diagnostic_route",
            },
            categories,
        )

    def test_diagnostic_rejection_source_can_be_classified_only_when_noncanonical(self) -> None:
        source = "\n".join(
            (
                "// diagnostic rejection-only capture; not h8 proof acceptance",
                "camera.Render();",
                "readback.ReadPixels(rect, 0, 0);",
                "byte[] png = readback.EncodeToPNG();",
            )
        )

        result = validator.validate_harness_candidate_source(source, allow_diagnostic_rejection=True)

        self.assertEqual(validator.DIAGNOSTIC_PASS_STATUS, result.status)
        self.assertTrue(result.diagnostic_only)

    def test_strict_mode_rejects_mutation_even_if_labels_look_proof_like(self) -> None:
        source = "\n".join(
            (
                "// diagnostic rejection-only h8_1475 candidate",
                'private const string PacketRoot = "Docs/Screenshots/HectonProofPackets/h8_1475_s01";',
                "EditorSceneManager.MarkSceneDirty(scene);",
                "camera.Render();",
            )
        )

        result = validator.validate_harness_candidate_source(
            source,
            allow_diagnostic_rejection=True,
            strict=True,
        )

        self.assertEqual(validator.HARNESS_REJECTED_STATUS, result.status)
        self.assertFalse(result.diagnostic_only)


if __name__ == "__main__":
    unittest.main()
