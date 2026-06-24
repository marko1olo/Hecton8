import sys
import unittest
from pathlib import Path

TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateToolSurfaceDetailGeminiRoute as validator  # noqa: E402

class TestUtilityFunctions(unittest.TestCase):
    def test_sanitize_provider_name(self) -> None:
        self.assertEqual("Atlas", validator.sanitize_provider_name(""))
        self.assertEqual("Atlas", validator.sanitize_provider_name("   "))
        self.assertEqual("Provider_123_", validator.sanitize_provider_name("Provider 123!"))
        self.assertEqual("Test-Provider_Name_", validator.sanitize_provider_name("Test-Provider_Name@"))
        self.assertEqual("CleanName", validator.sanitize_provider_name("CleanName"))

    def test_resolve_provider(self) -> None:
        constants = {"ConstProvider": "ResolvedValue"}
        self.assertEqual("Value", validator.resolve_provider('"Value"', constants))
        self.assertEqual("ResolvedValue", validator.resolve_provider("ConstProvider", constants))
        self.assertEqual("Unknown", validator.resolve_provider("Unknown", constants))

    def test_load_constants(self) -> None:
        text = """
        private const string Name1 = "Value1";
        // some comment
        private const string Name2="Value2";
        """
        constants = validator.load_constants(text)
        self.assertEqual({"Name1": "Value1", "Name2": "Value2"}, constants)

import tempfile
from unittest.mock import patch

class TestValidateStatic(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.temp_root = Path(self.temp_dir.name)

        self.integrator = self.temp_root / "Integrator.cs"
        self.unity_applier = self.temp_root / "UnityApplier.cs"
        self.unity_apply_runner = self.temp_root / "UnityApplyRunner.ps1"
        self.static_preflight = self.temp_root / "StaticPreflight.ps1"
        self.material_root = self.temp_root / "Materials"
        self.gemini_atlas_root = self.temp_root / "Atlases"
        self.static_manifests = {}

        # Patching module variables
        self.patches = [
            patch.object(validator, "ROOT", self.temp_root),
            patch.object(validator, "INTEGRATOR", self.integrator),
            patch.object(validator, "UNITY_APPLIER", self.unity_applier),
            patch.object(validator, "UNITY_APPLY_RUNNER", self.unity_apply_runner),
            patch.object(validator, "STATIC_PREFLIGHT", self.static_preflight),
            patch.object(validator, "MATERIAL_ROOT", self.material_root),
            patch.object(validator, "GEMINI_ATLAS_ROOT", self.gemini_atlas_root),
            patch.object(validator, "STATIC_MANIFESTS", self.static_manifests),
        ]
        for p in self.patches:
            p.start()

    def tearDown(self) -> None:
        for p in self.patches:
            p.stop()
        self.temp_dir.cleanup()

    def test_missing_integrator(self) -> None:
        errors: list[str] = []
        warnings: list[str] = []
        details, manifests = validator.validate_static(errors, warnings)
        self.assertEqual([], details)
        self.assertEqual({}, manifests)
        self.assertEqual(1, len(errors))
        self.assertIn("Missing integrator", errors[0])

    def test_integrator_missing_required_tokens(self) -> None:
        # Create empty integrator
        self.integrator.write_text("empty file", encoding="utf-8-sig")
        # Creating valid other files to skip other checks if needed, but error count will be high
        self.unity_applier.write_text("ToolSurfaceDetailGeminiIntegrator.Apply()", encoding="utf-8-sig")
        self.unity_apply_runner.write_text('Invoke-PythonValidator -ValidatorPath $toolSurfaceDetailValidator -Arguments @("--post-apply")', encoding="utf-8-sig")
        self.static_preflight.write_text("ValidateToolSurfaceDetailGeminiRoute.py", encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_static(errors, warnings)

        self.assertTrue(any("integrator missing required token: ToolSurfaceDetailGeminiIntegrator" in e for e in errors))
        self.assertTrue(any("integrator missing required token: ValidateDetails();" in e for e in errors))
        self.assertTrue(any("integrator missing required token: GameObject.CreatePrimitive(PrimitiveType.Cube)" in e for e in errors))

    def _write_valid_runners(self) -> None:
        self.unity_applier.write_text("ToolSurfaceDetailGeminiIntegrator.Apply()", encoding="utf-8-sig")
        self.unity_apply_runner.write_text('Invoke-PythonValidator -ValidatorPath $toolSurfaceDetailValidator -Arguments @("--post-apply")', encoding="utf-8-sig")
        self.static_preflight.write_text("ValidateToolSurfaceDetailGeminiRoute.py", encoding="utf-8-sig")

    def _setup_manifest(self) -> None:
        import json
        manifest_data = {
            "assets": [
                {
                    "id": "valid_mat",
                    "heldToolAllowed": True,
                    "watermarkRisk": False
                },
                {
                    "id": "not_allowed_mat",
                    "heldToolAllowed": False,
                    "watermarkRisk": False
                },
                {
                    "id": "watermark_mat",
                    "heldToolAllowed": True,
                    "watermarkRisk": True
                }
            ]
        }
        self.gemini_atlas_root.mkdir(parents=True, exist_ok=True)
        provider_dir = self.gemini_atlas_root / "TestProvider"
        provider_dir.mkdir(parents=True, exist_ok=True)
        manifest_path = provider_dir / "GeminiMaterialAtlas_Manifest.json"
        manifest_path.write_text(json.dumps(manifest_data), encoding="utf-8-sig")

    def test_out_of_route_prefab(self) -> None:
        self._write_valid_runners()
        text = """
        ToolSurfaceDetailGeminiIntegrator ValidateDetails(); RequirePrefab(spec) RequireMaterial(spec) GameObject.CreatePrimitive(PrimitiveType.Cube) UnityEngine.Object.DestroyImmediate(collider) renderer.shadowCastingMode = ShadowCastingMode.Off renderer.sharedMaterial = material PrefabUtility.LoadPrefabContents PrefabUtility.SaveAsPrefabAsset
        new("Assets/Invalid/Path.prefab", "Child", "TestProvider", "valid_mat")
        """
        self.integrator.write_text(text, encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_static(errors, warnings)

        self.assertTrue(any("prefab outside held/world tool routes: Assets/Invalid/Path.prefab" in e for e in errors))

    def test_unknown_provider(self) -> None:
        self._write_valid_runners()
        self._setup_manifest()

        prefab_path = self.temp_root / "Assets/_Project/Prefabs/Tools/Held/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("", encoding="utf-8-sig")

        text = f"""
        ToolSurfaceDetailGeminiIntegrator ValidateDetails(); RequirePrefab(spec) RequireMaterial(spec) GameObject.CreatePrimitive(PrimitiveType.Cube) UnityEngine.Object.DestroyImmediate(collider) renderer.shadowCastingMode = ShadowCastingMode.Off renderer.sharedMaterial = material PrefabUtility.LoadPrefabContents PrefabUtility.SaveAsPrefabAsset
        new("{validator.display_path(prefab_path)}", "Child", "UnknownProvider", "valid_mat")
        """
        self.integrator.write_text(text, encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_static(errors, warnings)

        self.assertTrue(any("unknown provider: UnknownProvider" in e for e in errors))

    def test_missing_material_in_manifest(self) -> None:
        self._write_valid_runners()
        self._setup_manifest()

        prefab_path = self.temp_root / "Assets/_Project/Prefabs/Tools/Held/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("", encoding="utf-8-sig")

        text = f"""
        ToolSurfaceDetailGeminiIntegrator ValidateDetails(); RequirePrefab(spec) RequireMaterial(spec) GameObject.CreatePrimitive(PrimitiveType.Cube) UnityEngine.Object.DestroyImmediate(collider) renderer.shadowCastingMode = ShadowCastingMode.Off renderer.sharedMaterial = material PrefabUtility.LoadPrefabContents PrefabUtility.SaveAsPrefabAsset
        new("{validator.display_path(prefab_path)}", "Child", "Gemini_TestProvider", "unknown_mat")
        """
        self.integrator.write_text(text, encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_static(errors, warnings)

        self.assertTrue(any("material id not in provider manifest:" in e and "unknown_mat" in e for e in errors))

    def test_material_not_held_tool_allowed(self) -> None:
        self._write_valid_runners()
        self._setup_manifest()

        prefab_path = self.temp_root / "Assets/_Project/Prefabs/Tools/Held/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("", encoding="utf-8-sig")

        text = f"""
        ToolSurfaceDetailGeminiIntegrator ValidateDetails(); RequirePrefab(spec) RequireMaterial(spec) GameObject.CreatePrimitive(PrimitiveType.Cube) UnityEngine.Object.DestroyImmediate(collider) renderer.shadowCastingMode = ShadowCastingMode.Off renderer.sharedMaterial = material PrefabUtility.LoadPrefabContents PrefabUtility.SaveAsPrefabAsset
        new("{validator.display_path(prefab_path)}", "Child", "Gemini_TestProvider", "not_allowed_mat")
        """
        self.integrator.write_text(text, encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_static(errors, warnings)

        self.assertTrue(any("material is not held-tool allowed" in e and "not_allowed_mat" in e for e in errors))

    def test_material_has_watermark_risk(self) -> None:
        self._write_valid_runners()
        self._setup_manifest()

        prefab_path = self.temp_root / "Assets/_Project/Prefabs/Tools/Held/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("", encoding="utf-8-sig")

        text = f"""
        ToolSurfaceDetailGeminiIntegrator ValidateDetails(); RequirePrefab(spec) RequireMaterial(spec) GameObject.CreatePrimitive(PrimitiveType.Cube) UnityEngine.Object.DestroyImmediate(collider) renderer.shadowCastingMode = ShadowCastingMode.Off renderer.sharedMaterial = material PrefabUtility.LoadPrefabContents PrefabUtility.SaveAsPrefabAsset
        new("{validator.display_path(prefab_path)}", "Child", "Gemini_TestProvider", "watermark_mat")
        """
        self.integrator.write_text(text, encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_static(errors, warnings)

        self.assertTrue(any("material has watermark risk" in e and "watermark_mat" in e for e in errors))

class TestValidatePostApply(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.temp_root = Path(self.temp_dir.name)
        self.material_root = self.temp_root / "Materials"
        self.gemini_atlas_root = self.temp_root / "Atlases"

        self.patches = [
            patch.object(validator, "ROOT", self.temp_root),
            patch.object(validator, "MATERIAL_ROOT", self.material_root),
            patch.object(validator, "GEMINI_ATLAS_ROOT", self.gemini_atlas_root),
        ]
        for p in self.patches:
            p.start()

    def tearDown(self) -> None:
        for p in self.patches:
            p.stop()
        self.temp_dir.cleanup()

    def test_post_apply_missing_generated_material_asset(self) -> None:
        details = [{"prefab": "Assets/Valid.prefab", "child": "ChildObj", "provider": "TestProv", "material": "mat_1"}]
        errors: list[str] = []
        warnings: list[str] = []

        validator.validate_post_apply(details, errors, warnings)

        self.assertEqual(1, len(errors))
        self.assertIn("post-apply missing generated material asset", errors[0])
        self.assertIn("mat_1", errors[0])

    def test_post_apply_missing_generated_material_guid(self) -> None:
        details = [{"prefab": "Assets/Valid.prefab", "child": "ChildObj", "provider": "TestProv", "material": "mat_1"}]

        mat_path = self.material_root / "TestProv" / "MAT_EXT_TestProv_mat_1.mat"
        mat_path.parent.mkdir(parents=True, exist_ok=True)
        mat_path.write_text("dummy", encoding="utf-8-sig")
        # missing .meta file

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_post_apply(details, errors, warnings)

        self.assertEqual(1, len(errors))
        self.assertIn("post-apply missing generated material guid", errors[0])

    def test_post_apply_missing_detail_child_in_prefab(self) -> None:
        details = [{"prefab": "Assets/Valid.prefab", "child": "ChildObj", "provider": "TestProv", "material": "mat_1"}]

        mat_path = self.material_root / "TestProv" / "MAT_EXT_TestProv_mat_1.mat"
        mat_path.parent.mkdir(parents=True, exist_ok=True)
        mat_path.write_text("dummy", encoding="utf-8-sig")
        meta_path = mat_path.with_suffix(".mat.meta")
        meta_path.write_text("guid: abcdef123456\n", encoding="utf-8-sig")

        prefab_path = self.temp_root / "Assets/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("dummy content with guid: abcdef123456 but no child", encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_post_apply(details, errors, warnings)

        self.assertEqual(1, len(errors))
        self.assertIn("missing detail child ChildObj", errors[0])

    def test_post_apply_missing_material_guid_in_prefab(self) -> None:
        details = [{"prefab": "Assets/Valid.prefab", "child": "ChildObj", "provider": "TestProv", "material": "mat_1"}]

        mat_path = self.material_root / "TestProv" / "MAT_EXT_TestProv_mat_1.mat"
        mat_path.parent.mkdir(parents=True, exist_ok=True)
        mat_path.write_text("dummy", encoding="utf-8-sig")
        meta_path = mat_path.with_suffix(".mat.meta")
        meta_path.write_text("guid: abcdef123456\n", encoding="utf-8-sig")

        prefab_path = self.temp_root / "Assets/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("dummy content with ChildObj but no guid", encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_post_apply(details, errors, warnings)

        self.assertEqual(1, len(errors))
        self.assertIn("missing detail material guid", errors[0])

    def test_post_apply_success(self) -> None:
        details = [{"prefab": "Assets/Valid.prefab", "child": "ChildObj", "provider": "TestProv", "material": "mat_1"}]

        mat_path = self.material_root / "TestProv" / "MAT_EXT_TestProv_mat_1.mat"
        mat_path.parent.mkdir(parents=True, exist_ok=True)
        mat_path.write_text("dummy", encoding="utf-8-sig")
        meta_path = mat_path.with_suffix(".mat.meta")
        meta_path.write_text("guid: abcdef123456\n", encoding="utf-8-sig")

        prefab_path = self.temp_root / "Assets/Valid.prefab"
        prefab_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.write_text("dummy content with ChildObj and guid: abcdef123456", encoding="utf-8-sig")

        errors: list[str] = []
        warnings: list[str] = []
        validator.validate_post_apply(details, errors, warnings)

        self.assertEqual(0, len(errors))

if __name__ == "__main__":
    unittest.main()
