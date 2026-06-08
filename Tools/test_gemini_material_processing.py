#!/usr/bin/env python3
"""Regression tests for Gemini material cleanup helpers."""

from __future__ import annotations

import json
from pathlib import Path
import sys
import unittest

import numpy as np
from PIL import Image


TOOLS = Path(__file__).resolve().parent
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from SplitGeminiMaterialAtlas import repair_matte_carbon_watermark  # noqa: E402


ROOT = Path(__file__).resolve().parents[1]
GENERATED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated"
GENERATED_MATERIAL_MANIFESTS = (
    GENERATED_ROOT / "GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json",
    GENERATED_ROOT / "GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json",
    GENERATED_ROOT / "GeminiMaterialAtlases/Batch20260607_MicroPanel/GeminiMaterialAtlas_Manifest.json",
)


class GeminiMaterialProcessingTests(unittest.TestCase):
    def test_generated_material_manifests_stay_below_warning_seam(self) -> None:
        for manifest in GENERATED_MATERIAL_MANIFESTS:
            payload = json.loads(manifest.read_text(encoding="utf-8-sig"))
            for asset in payload.get("assets", []) or []:
                seam = asset.get("seamScoreAfter")
                if seam is None:
                    continue
                self.assertLessEqual(
                    float(seam),
                    1.6,
                    f"{asset.get('id')} in {manifest.relative_to(ROOT).as_posix()} has high seamScoreAfter",
                )

    def test_repair_matte_carbon_watermark_removes_bright_stripe(self) -> None:
        size = 256
        pixels = np.zeros((size, size, 3), dtype=np.uint8)
        for y in range(size):
            for x in range(size):
                cell = ((x // 8) + (y // 8)) % 2
                base = 48 if cell == 0 else 76
                pixels[y, x] = (
                    base + ((x + y) % 9),
                    base + ((x * 2) % 7),
                    base + ((y * 3) % 8),
                )
        pixels[132:172, 132:256] = (82, 104, 70)
        pixels[154:159, 144:256] = (232, 226, 204)
        source = Image.fromarray(pixels, "RGB")

        repaired = repair_matte_carbon_watermark(source)
        before = np.asarray(source, dtype=np.float32)
        after = np.asarray(repaired, dtype=np.float32)
        target_before = before[150:164, 144:250]
        target_after = after[150:164, 144:250]

        self.assertLess(float(target_after.max()), float(target_before.max()) - 45.0)
        self.assertLess(float(target_after.mean()), float(target_before.mean()) - 35.0)
        self.assertEqual(repaired.size, source.size)


if __name__ == "__main__":
    unittest.main()
