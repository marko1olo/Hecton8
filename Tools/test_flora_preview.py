#!/usr/bin/env python3
"""Regression tests for Tools/FloraPreview.py."""

from __future__ import annotations

import copy
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "Tools" / "FloraPreview.py"
LIBRARY_PATH = ROOT / "Data" / "Flora" / "LSystem_Library.json"

spec = importlib.util.spec_from_file_location("flora_preview", MODULE_PATH)
flora_preview = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(flora_preview)


class FloraPreviewValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.library = json.loads(LIBRARY_PATH.read_text(encoding="utf-8"))
        cls.first_species = cls.library["biomes"][0]["species"][0]

    def test_current_library_structure_is_clean(self) -> None:
        self.assertEqual([], flora_preview.validate_library_structure(self.library))

    def test_current_first_species_validates(self) -> None:
        expanded, metrics, issues = flora_preview.validate_species(self.library, self.first_species, 0)
        self.assertEqual([], issues)
        self.assertGreater(len(expanded), 0)
        self.assertGreater(metrics["line_count"], 0)

    def test_depth_above_eight_fails(self) -> None:
        species = copy.deepcopy(self.first_species)
        species[7] = 9
        _expanded, _metrics, issues = flora_preview.validate_species(self.library, species, 0)
        self.assertIn("depth exceeds 8", issues)

    def test_stack_underflow_fails(self) -> None:
        species = copy.deepcopy(self.first_species)
        species[2] = "F]"
        species[3] = "F=F"
        _expanded, _metrics, issues = flora_preview.validate_species(self.library, species, 0)
        self.assertTrue(any("Stack underflow" in issue for issue in issues))

    def test_unknown_symbol_fails(self) -> None:
        species = copy.deepcopy(self.first_species)
        species[2] = "FZ"
        _expanded, _metrics, issues = flora_preview.validate_species(self.library, species, 0)
        self.assertTrue(any("unknown symbol" in issue for issue in issues))

    def test_unknown_rule_key_fails_even_if_unused(self) -> None:
        species = copy.deepcopy(self.first_species)
        species[3] = f"{species[3]};Z=F"
        _expanded, _metrics, issues = flora_preview.validate_species(self.library, species, 0)
        self.assertTrue(any("invalid rule key" in issue for issue in issues))
        self.assertTrue(any("unknown symbol" in issue for issue in issues))

    def test_invalid_sdf_primitive_fails(self) -> None:
        library = copy.deepcopy(self.library)
        library["branchSDF"]["F"] = "Sphere"
        _expanded, _metrics, issues = flora_preview.validate_species(library, self.first_species, 0)
        self.assertTrue(any("SDF primitive invalid" in issue for issue in issues))

    def test_missing_math_lod_fails(self) -> None:
        library = copy.deepcopy(self.library)
        del library["mathLOD"]
        issues = flora_preview.validate_library_structure(library)
        self.assertIn("missing mathLOD", issues)

    def test_missing_table_version_fails(self) -> None:
        library = copy.deepcopy(self.library)
        del library["tableVersion"]
        issues = flora_preview.validate_library_structure(library)
        self.assertIn("tableVersion mismatch", issues)

    def test_bad_biome_prefix_fails(self) -> None:
        library = copy.deepcopy(self.library)
        library["biomes"][0]["species"][0][0] = "BAD_001"
        issues = flora_preview.validate_library_structure(library)
        self.assertTrue(any("does not match biome prefix" in issue for issue in issues))

    def test_field_order_mismatch_fails(self) -> None:
        library = copy.deepcopy(self.library)
        library["fieldOrder"] = list(reversed(library["fieldOrder"]))
        issues = flora_preview.validate_library_structure(library)
        self.assertIn("fieldOrder mismatch", issues)

    def test_branch_sdf_symbol_mismatch_fails(self) -> None:
        library = copy.deepcopy(self.library)
        del library["branchSDF"]["F"]
        issues = flora_preview.validate_library_structure(library)
        self.assertTrue(any("branchSDF symbol mismatch" in issue for issue in issues))

    def test_metric_record_exposes_sdf_profile(self) -> None:
        expanded, metrics, issues = flora_preview.validate_species(self.library, self.first_species, 0)
        self.assertEqual([], issues)
        record = flora_preview.build_metric_record("safe_shallows", self.first_species, expanded, metrics, self.library)
        self.assertEqual(self.first_species[0], record["id"])
        self.assertRegex(record["speciesFingerprintFNV1A64"], r"^[0-9a-f]{16}$")
        self.assertGreater(record["lineCount"], 0)
        self.assertIn("F", record["sdfProfile"])

    def test_species_fingerprint_changes_when_axiom_changes(self) -> None:
        original = copy.deepcopy(self.first_species)
        changed = copy.deepcopy(self.first_species)
        changed[2] = f"{changed[2]}F"
        first_hash = flora_preview.species_fingerprint(original, self.library["tableVersion"])
        second_hash = flora_preview.species_fingerprint(changed, self.library["tableVersion"])
        self.assertNotEqual(first_hash, second_hash)

    def test_validate_only_skips_preview_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            code = flora_preview.main(
                [
                    "--library",
                    str(LIBRARY_PATH),
                    "--out",
                    str(temp_path / "preview"),
                    "--block-size",
                    "100",
                    "--passes-per-block",
                    "1",
                    "--rationale",
                    "",
                    "--summary-json",
                    str(temp_path / "summary.json"),
                    "--metrics-json",
                    str(temp_path / "metrics.json"),
                    "--validate-only",
                ]
            )
            self.assertEqual(0, code)
            summary = json.loads((temp_path / "summary.json").read_text(encoding="utf-8"))
            metrics = json.loads((temp_path / "metrics.json").read_text(encoding="utf-8"))
            self.assertEqual("H8_LSYSTEM_VALIDATION_V1", summary["schema"])
            self.assertEqual("H8_LSYSTEM_LIBRARY_V1", summary["librarySchema"])
            self.assertEqual(1, summary["tableVersion"])
            self.assertRegex(summary["libraryFingerprintFNV1A64"], r"^[0-9a-f]{16}$")
            self.assertEqual("H8_LSYSTEM_METRICS_V1", metrics["schema"])
            self.assertEqual(summary["libraryFingerprintFNV1A64"], metrics["libraryFingerprintFNV1A64"])
            self.assertTrue(summary["validateOnly"])
            self.assertEqual(1, summary["skippedPreviews"])
            self.assertEqual(0, summary["pngPreviews"])
            self.assertEqual(0, summary["svgPreviews"])
            self.assertEqual(0, summary["turtlePreviews"])
            self.assertFalse(any((temp_path / "preview").glob("*")))


if __name__ == "__main__":
    unittest.main()
