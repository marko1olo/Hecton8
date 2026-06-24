#!/usr/bin/env python3
"""Regression tests for Gemini inventory-icon sheet source validation."""

from __future__ import annotations

from argparse import Namespace, ArgumentTypeError
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import io

from PIL import Image

import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import ValidateInventoryIconSheetSource as sheet_source


# Use platform-agnostic temporary directory approach
TEST_TMP_ROOT = Path(tempfile.gettempdir()) / "Hecton8PythonTests"


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

    def test_missing_source_image(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "missing_image.png"

            errors, warnings, meta = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("source image missing" in error for error in errors))
        self.assertEqual(meta.get("source"), sheet_source.display_path(image))

    def test_unsupported_extension(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "image.bmp"
            Image.new("RGB", (2048, 2048), (16, 18, 22)).save(image)

            errors, _, _ = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("unsupported source extension" in error for error in errors))

    def test_large_source_image_warning(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "large_image.png"
            Image.new("RGB", (2048, 2048), (16, 18, 22)).save(image)
            # Artificially inflate size to trigger warning
            with open(image, "ab") as f:
                f.write(b'\0' * 30_000_000)

            _, warnings, _ = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("large source image" in warning for warning in warnings))

    def test_invalid_grid_dimensions(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "image.png"
            Image.new("RGB", (2048, 2048), (16, 18, 22)).save(image)

            a = args(image, spec)
            a.grid_rows = 0
            errors, _, _ = sheet_source.validate_source(a)

        self.assertTrue(any("grid rows/columns must be positive" in error for error in errors))

    def test_spec_grid_count_mismatch(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec, count=10) # 10 != 3*4
            image = root / "image.png"
            Image.new("RGB", (2048, 2048), (16, 18, 22)).save(image)

            errors, _, _ = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("spec/grid count mismatch" in error for error in errors))

    def test_aspect_ratio_warning(self) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            spec = root / "spec.json"
            write_spec(spec)
            image = root / "image.png"
            Image.new("RGB", (2048, 512), (16, 18, 22)).save(image)

            _, warnings, _ = sheet_source.validate_source(args(image, spec))

        self.assertTrue(any("source aspect differs" in warning for warning in warnings))

    def test_load_spec_count_none(self) -> None:
        self.assertEqual(0, sheet_source.load_spec_count(None))


class PositiveIntTests(unittest.TestCase):
    def test_positive_int_valid(self) -> None:
        self.assertEqual(5, sheet_source.positive_int("5"))

    def test_positive_int_invalid(self) -> None:
        with self.assertRaises(ArgumentTypeError):
            sheet_source.positive_int("0")
        with self.assertRaises(ArgumentTypeError):
            sheet_source.positive_int("-5")


class MainFunctionTests(unittest.TestCase):
    @patch('sys.stdout', new_callable=io.StringIO)
    def test_main_function_failure(self, mock_stdout: io.StringIO) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            image = root / "missing_image.png"

            test_args = ["validate_source.py", "--source", str(image)]
            with patch('sys.argv', test_args):
                result = sheet_source.main()

            self.assertEqual(1, result)
            self.assertIn("ERROR source image missing", mock_stdout.getvalue())

    @patch('sys.stdout', new_callable=io.StringIO)
    def test_main_function_success_with_warnings(self, mock_stdout: io.StringIO) -> None:
        with temporary_directory() as tmp:
            root = Path(tmp)
            image = root / "generated image.png"
            Image.new("RGB", (2048, 2048), (16, 18, 22)).save(image)

            test_args = ["validate_source.py", "--source", str(image)]
            with patch('sys.argv', test_args):
                result = sheet_source.main()

            self.assertEqual(0, result)
            self.assertIn("WARN source filename has no inventory-icon sheet hint", mock_stdout.getvalue())


if __name__ == "__main__":
    unittest.main()
