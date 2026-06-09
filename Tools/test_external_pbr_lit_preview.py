import os
import sys
import tempfile
import time
import unittest
from contextlib import redirect_stdout
from argparse import Namespace
from io import StringIO
from pathlib import Path

from PIL import Image


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import BuildExternalPbrLitPreview as builder  # noqa: E402
import ValidateExternalPbrLitPreview as validator  # noqa: E402


class ExternalPbrLitPreviewTests(unittest.TestCase):
    def create_manifest(self, root: Path, asset_count: int = 2) -> Path:
        assets = []
        for index in range(asset_count):
            asset_dir = root / f"asset_{index}"
            asset_dir.mkdir(parents=True, exist_ok=True)
            base = asset_dir / "base.png"
            normal = asset_dir / "normal.png"
            arm = asset_dir / "arm.png"
            Image.new("RGB", (32, 32), (40 + index * 20, 80, 120)).save(base)
            Image.new("RGB", (32, 32), (128, 128, 255)).save(normal)
            Image.new("RGB", (32, 32), (255, 128, 0)).save(arm)
            assets.append(
                {
                    "id": f"test_material_{index}",
                    "maps": {
                        "BaseColor": str(base),
                        "NormalGL": str(normal),
                        "ARM_AO_Rough_Metal": str(arm),
                    },
                }
            )

        manifest = root / "manifest.json"
        manifest.write_text(
            "{\n"
            '  "schema": "hecton8.external_pbr_pack.v1",\n'
            f'  "preview": "{(root / "manifest_preview.png").as_posix()}",\n'
            f'  "assets": {__import__("json").dumps(assets)}\n'
            "}\n",
            encoding="utf-8",
        )
        return manifest

    def build_args(self, manifest: Path, output: Path | None = None) -> Namespace:
        return Namespace(
            manifest=manifest,
            output=output,
            label_prefix="Test",
            tile_size=16,
            columns=2,
        )

    def validate_args(self, manifest: Path, output: Path | None = None) -> Namespace:
        return Namespace(
            manifest=str(manifest),
            output="" if output is None else str(output),
            tile_size=16,
            columns=2,
            label_height=40,
            gap=14,
        )

    def render_quiet(self, args: Namespace) -> int:
        with redirect_stdout(StringIO()):
            return builder.render(args)

    def validate_quiet(self, args: Namespace) -> int:
        with redirect_stdout(StringIO()):
            return validator.validate(args)

    def test_builder_uses_manifest_preview_when_output_omitted(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.create_manifest(root)
            preview = root / "manifest_preview.png"

            code = self.render_quiet(self.build_args(manifest))

            self.assertEqual(0, code)
            self.assertTrue(preview.exists())
            with Image.open(preview) as image:
                self.assertEqual((46, 56), image.size)
                self.assertEqual("RGB", image.mode)
            self.assertEqual(0, self.validate_quiet(self.validate_args(manifest)))

    def test_builder_explicit_output_overrides_manifest_preview(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.create_manifest(root)
            explicit = root / "explicit_preview.png"

            code = self.render_quiet(self.build_args(manifest, output=explicit))

            self.assertEqual(0, code)
            self.assertTrue(explicit.exists())
            self.assertFalse((root / "manifest_preview.png").exists())

    def test_validator_rejects_preview_older_than_source_maps(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.create_manifest(root)
            self.assertEqual(0, self.render_quiet(self.build_args(manifest)))

            base_map = root / "asset_0" / "base.png"
            future = time.time() + 10.0
            os.utime(base_map, (future, future))

            self.assertEqual(1, self.validate_quiet(self.validate_args(manifest)))

    def test_validator_rejects_wrong_preview_size(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.create_manifest(root)
            preview = root / "manifest_preview.png"
            Image.new("RGB", (12, 12), (0, 0, 0)).save(preview)

            self.assertEqual(1, self.validate_quiet(self.validate_args(manifest)))

    def test_validator_rejects_missing_required_map(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.create_manifest(root)
            self.assertEqual(0, self.render_quiet(self.build_args(manifest)))
            missing_map = root / "asset_1" / "arm.png"
            missing_map.unlink()

            self.assertEqual(1, self.validate_quiet(self.validate_args(manifest)))


if __name__ == "__main__":
    unittest.main()
