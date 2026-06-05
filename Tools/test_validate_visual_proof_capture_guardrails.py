import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

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

        self.assertGreaterEqual(len(risks), 6)
        self.assertIn("scene_save", {risk.category for risk in risks})
        self.assertIn("diagnostic_unsaved_capture", {risk.category for risk in risks})


if __name__ == "__main__":
    unittest.main()
