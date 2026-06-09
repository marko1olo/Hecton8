import json
import shutil
import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
TEST_TEMP_ROOT = TOOLS_ROOT.parent / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/_tmp_regen_target_tests"
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateBatch34RegenTargets as validator  # noqa: E402


class Batch34RegenTargetsValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_root = validator.ROOT
        self.original_manifest = validator.MANIFEST_PATH
        self.original_processor = validator.PROCESSOR_PATH
        self.original_contact = validator.CONTACT_PATH

    def tearDown(self) -> None:
        validator.ROOT = self.original_root
        validator.MANIFEST_PATH = self.original_manifest
        validator.PROCESSOR_PATH = self.original_processor
        validator.CONTACT_PATH = self.original_contact

    def configure_paths(self, root: Path) -> None:
        validator.ROOT = root
        validator.MANIFEST_PATH = root / "QA/Batch34_RegenTargets_IntakeManifest.json"
        validator.PROCESSOR_PATH = root / "Tools/ProcessBatch34RegenTargets.py"
        validator.CONTACT_PATH = root / "QA/PREVIEW_Batch34_RegenTargets_Contact.png"
        validator.MANIFEST_PATH.parent.mkdir(parents=True)
        validator.PROCESSOR_PATH.parent.mkdir(parents=True)
        validator.PROCESSOR_PATH.write_text("# processor\n", encoding="utf-8")
        validator.CONTACT_PATH.write_bytes(b"png")

    def scratch_root(self, name: str) -> Path:
        root = TEST_TEMP_ROOT / name
        if root.exists():
            shutil.rmtree(root, ignore_errors=True)
        root.mkdir(parents=True, exist_ok=True)
        return root

    def touch_project_file(self, root: Path, rel: str) -> None:
        path = root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"asset")

    def entry(self, root: Path, key: tuple[str, str], decision: str, selected: bool, broad: bool) -> dict:
        source_id = key[0].replace("-R1", "") if key[0] != "B34-3439-V2" else "B34-3439"
        original = f"Originals/{key[0]}_{key[1]}.png"
        self.touch_project_file(root, original)
        data = {
            "id": key[0],
            "sourceId": source_id,
            "variant": key[1],
            "sourceType": "SEAMLESS_TILE",
            "priority": "REQUIRED",
            "decision": decision,
            "selected": selected,
            "broadSeamlessAccepted": broad,
            "note": "test",
            "originalPath": original,
            "width": 1024,
            "height": 1024,
            "clipping": {"blackPct": 0.0, "whitePct": 0.0},
        }
        if key[1] == "viewport_glass_jpeg_timestamp":
            data["sourceType"] = "DECAL_ATLAS"
            data["edgeContent"] = {"edgeContentPct": 0.0}
        elif key[1] == "spore_pods_unrequested_variant":
            data["sourceType"] = "UV_ATLAS"
            data["edgeContent"] = {"edgeContentPct": 9.5}
        else:
            preview = f"TilePreviews/{key[0]}_{key[1]}_2x2.png"
            self.touch_project_file(root, preview)
            data["tilePreviewPath"] = preview
            data["seamMetrics"] = {"bandLR": 12.0, "bandTB": 13.0}
        if selected:
            if key[1] == "amber_lens_png_named":
                cleaned = f"SourceCleaned/{key[0]}_{key[1]}_center_crop.png"
                cleaned_preview = f"TilePreviews/{key[0]}_{key[1]}_center_crop_2x2.png"
                self.touch_project_file(root, cleaned)
                self.touch_project_file(root, cleaned_preview)
                data["cleanedCandidatePath"] = cleaned
                data["cleanedTilePreviewPath"] = cleaned_preview
                data["cleanedSeamMetrics"] = {"bandLR": 9.0, "bandTB": 8.0}
                data["finalCandidatePath"] = cleaned
            else:
                data["finalCandidatePath"] = original
        return data

    def write_manifest(self, root: Path, selected_override: dict[tuple[str, str], bool] | None = None) -> None:
        selected_override = selected_override or {}
        entries = []
        for key in sorted(validator.EXPECTED_VARIANTS):
            if key in validator.EXPECTED_SELECTED:
                decision = validator.EXPECTED_SELECTED[key]
                selected = True
            else:
                decision = validator.EXPECTED_REJECTED[key]
                selected = False
            selected = selected_override.get(key, selected)
            broad = key == ("B34-3409-R1", "limestone_ceiling_jpeg_timestamp") and selected
            entries.append(self.entry(root, key, decision, selected, broad))
        selected_entries = [entry for entry in entries if entry["selected"]]
        manifest = {
            "schema": "hecton8.batch34.regen_targets.intake.v2",
            "operatorPrompt": "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3406_TEXTURE_SOURCE_REGEN_TARGETS_20260608.md",
            "contactSheet": validator.display(validator.CONTACT_PATH),
            "entries": entries,
            "selectedFinalCandidates": [
                {
                    "id": entry["id"],
                    "variant": entry["variant"],
                    "sourceId": entry["sourceId"],
                    "decision": entry["decision"],
                    "finalCandidatePath": entry.get("finalCandidatePath", ""),
                    "broadSeamlessAccepted": entry["broadSeamlessAccepted"],
                }
                for entry in selected_entries
            ],
        }
        validator.MANIFEST_PATH.write_text(json.dumps(manifest), encoding="utf-8")

    def run_validation(self) -> list[str]:
        errors: list[str] = []
        validator.validate_manifest(validator.load_manifest(errors), errors)
        return errors

    def test_accepts_expected_regen_target_selection(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        root = self.scratch_root("accepts_expected")
        try:
            self.configure_paths(root)
            self.write_manifest(root)

            self.assertEqual([], self.run_validation())
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_rejects_selecting_limestone_png_hero_repeat(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        root = self.scratch_root("rejects_limestone_png")
        try:
            self.configure_paths(root)
            self.write_manifest(
                root,
                {
                    ("B34-3409-R1", "limestone_ceiling_png_named"): True,
                    ("B34-3409-R1", "limestone_ceiling_jpeg_timestamp"): False,
                },
            )

            errors = self.run_validation()

            self.assertTrue(any("selected regen targets mismatch" in error for error in errors), errors)
            self.assertTrue(any("rejected/hold variant must not be selected" in error for error in errors), errors)
        finally:
            shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
