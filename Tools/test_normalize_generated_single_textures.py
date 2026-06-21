#!/usr/bin/env python3
"""Regression tests for Tools/NormalizeGeneratedSingleTextures.py."""

from __future__ import annotations

import argparse
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "Tools" / "NormalizeGeneratedSingleTextures.py"

spec = importlib.util.spec_from_file_location("NormalizeGeneratedSingleTextures", MODULE_PATH)
normalize_generated_single_textures = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(normalize_generated_single_textures)


class NormalizeGeneratedSingleTexturesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.temp_path = Path(self.temp_dir.name)

        # Create a dummy image
        self.dummy_image_path = self.temp_path / "dummy.png"
        img = Image.new('RGB', (2048, 2048), color = 'red')
        img.save(self.dummy_image_path)

        # Create a small dummy image
        self.small_image_path = self.temp_path / "small.png"
        img_small = Image.new('RGB', (512, 512), color = 'blue')
        img_small.save(self.small_image_path)

        # Create an alternative report and preview path for tests
        self.report_path = self.temp_path / "report.json"
        self.preview_path = self.temp_path / "preview.png"

    def tearDown(self) -> None:
        self.temp_dir.cleanup()

    def test_display_path(self) -> None:
        abs_path = normalize_generated_single_textures.ROOT / "some_dir" / "file.txt"
        self.assertEqual(normalize_generated_single_textures.display_path(abs_path), "some_dir/file.txt")

        outside_path = Path("/tmp/outside_file.txt")
        self.assertEqual(normalize_generated_single_textures.display_path(outside_path), "/tmp/outside_file.txt")

    def test_normalize_resizes_large_image(self) -> None:
        result = normalize_generated_single_textures.normalize(self.dummy_image_path, 1024)

        self.assertEqual(result["beforeSize"], [2048, 2048])
        self.assertEqual(result["afterSize"], [1024, 1024])
        self.assertGreaterEqual(result["savedBytes"], 0)

        with Image.open(self.dummy_image_path) as img:
            self.assertEqual(img.size, (1024, 1024))

    def test_normalize_keeps_small_image_size(self) -> None:
        result = normalize_generated_single_textures.normalize(self.small_image_path, 1024)

        self.assertEqual(result["beforeSize"], [512, 512])
        self.assertEqual(result["afterSize"], [512, 512])

        with Image.open(self.small_image_path) as img:
            self.assertEqual(img.size, (512, 512))

    def test_run_success(self) -> None:
        args = argparse.Namespace(target=[str(self.dummy_image_path), str(self.small_image_path)], max_size=1024)

        with patch.object(normalize_generated_single_textures, 'REPORT_PATH', self.report_path), \
             patch.object(normalize_generated_single_textures, 'PREVIEW_PATH', self.preview_path), \
             patch('sys.stdout'):

            ret = normalize_generated_single_textures.run(args)

            self.assertEqual(ret, 0)
            self.assertTrue(self.report_path.exists())
            self.assertTrue(self.preview_path.exists())

            with open(self.report_path) as f:
                report = json.load(f)

            self.assertEqual(len(report["records"]), 2)
            self.assertEqual(len(report["missing"]), 0)

    def test_run_missing_file(self) -> None:
        missing_path = self.temp_path / "does_not_exist.png"
        args = argparse.Namespace(target=[str(self.dummy_image_path), str(missing_path)], max_size=1024)

        with patch('sys.stdout'), \
             patch.object(normalize_generated_single_textures, 'REPORT_PATH', self.report_path), \
             patch.object(normalize_generated_single_textures, 'PREVIEW_PATH', self.preview_path):

            ret = normalize_generated_single_textures.run(args)

            self.assertEqual(ret, 2)

            with open(self.report_path) as f:
                report = json.load(f)

            self.assertEqual(len(report["records"]), 1)
            self.assertEqual(len(report["missing"]), 1)

    def test_write_preview_no_records(self) -> None:
        with patch.object(normalize_generated_single_textures, 'PREVIEW_PATH', self.preview_path):
            normalize_generated_single_textures.write_preview([])
            self.assertFalse(self.preview_path.exists())


if __name__ == "__main__":
    unittest.main()
