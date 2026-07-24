#!/usr/bin/env python3
"""Regression tests for Gemini inventory-icon sheet source validation."""

from __future__ import annotations

from argparse import Namespace
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

import ValidateInventoryIconSheetSource as sheet_source


TEST_TMP_ROOT = Path("C:/tmp/Hecton8PythonTests")


def temporary_directory() -> tempfile.TemporaryDirectory:
    TEST_TMP_ROOT.mkdir(parents=True, exist_ok=True)
    return tempfile.TemporaryDirectory(dir=TEST_TMP_ROOT)


def write_spec(path: Path, count: int = 12) -> None:
    payload = {"items": [{"index": index + 1, "persistentId": f"Item_{index + 1:02d}"} for index in range(count)]}
    path.write_text(json.dumps(payload), encoding="utf-8")


def args(source: Path, spec: Path, *, allow_suspicious_name: bool = False, min_cell_px: int = 256) -> Namespace:
    return Namespace(
        source=str(source),
        spec_json=str(spec),
        grid_rows=3,
        grid_columns=4,
        min_cell_px=min_cell_px,
        max_bytes=26_214_400,
        expected_aspect=1.0,
        aspect_tolerance=0.85,
        allow_suspicious_name=allow_suspicious_name,
    )


class InventoryIconSheetSourceTests(unittest.TestCase):
    def test_material_named_source_is_rejected_without_override(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "Carbon composite micro-panel.png"
            Image.new("RGB", (2048, 2048), (32, 38, 40)).save(image)

            errors, warnings, meta = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("material/texture" in error for error in errors))
        self.assertEqual(0, len(warnings))
        self.assertEqual(2048, meta["width"])

    def test_neutral_generated_source_name_is_allowed(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "generated image.png"
            Image.new("RGB", (2048, 2048), (16, 18, 22)).save(image)

            errors, warnings, _ = sheet_source.validate_source(args(image, spec))

        self.assertEqual([], errors)
        self.assertTrue(any("no inventory-icon sheet hint" in warning for warning in warnings), warnings)

    def test_tool_micro_panel_atlas_source_name_is_still_material_like(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "Precision Tool Micro-Panel Atlas.png"
            Image.new("RGB", (2048, 2048), (24, 28, 30)).save(image)

            errors, _, _ = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("material/texture" in error for error in errors), errors)

    def test_grid_cells_below_alpha_bake_floor_are_rejected(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "Tool inventory sheet.png"
            Image.new("RGB", (512, 512), (16, 18, 22)).save(image)

            errors, _, _ = sheet_source.validate_source(args(image, spec, min_cell_px=256))

        self.assertTrue(any("grid cell too small" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
