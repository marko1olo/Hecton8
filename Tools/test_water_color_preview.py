#!/usr/bin/env python3
"""Validation tests for the HECTON-8 water extinction LUT baker."""

from __future__ import annotations

import json
import gc
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np


TOOLS_DIR = Path(__file__).resolve().parent
SCRIPT_PATH = TOOLS_DIR / "WaterColorPreview.py"


class WaterColorPreviewTests(unittest.TestCase):
    def test_generated_outputs_match_optics_contract(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    "--output-dir",
                    str(output_dir),
                    "--force",
                ],
                capture_output=True,
                check=True,
                text=True,
                timeout=300,
            )

            self.assertIn("OPTICS CALCULATED", result.stdout)

            matrix_path = output_dir / "Water_Extinction_Matrix.bin"
            fog_path = output_dir / "Water_Fog_Density_LUT.bin"
            meta_path = output_dir / "Water_Extinction_Matrix.json"
            preview_path = output_dir / "Water_Extinction_GradientPreview.png"
            snippet_path = output_dir / "Water_Extinction_Hecton_CoreLit_Snippet.hlsl"
            readme_path = output_dir / "Water_Extinction_README.md"

            self.assertEqual(33_554_432, matrix_path.stat().st_size)
            self.assertEqual(3_002, fog_path.stat().st_size)
            self.assertTrue(preview_path.exists())
            self.assertTrue(snippet_path.exists())
            self.assertTrue(readme_path.exists())

            metadata = json.loads(meta_path.read_text(encoding="utf-8"))
            self.assertEqual("OPTICS CALCULATED", metadata["status"])
            self.assertEqual("PASS", metadata["selfAudit"]["status"])
            self.assertEqual(0.0, metadata["selfAudit"]["redTransmittanceAt500m"])
            self.assertEqual(0.0, metadata["selfAudit"]["redMatrixAt500m"])
            self.assertEqual(85, metadata["selfAudit"]["redMatrixAt500mDepthIndex"])
            self.assertEqual([256, 256, 256], metadata["matrixShape"])
            self.assertEqual([1501], metadata["fogDensity"]["shape"])
            self.assertEqual(0.024, metadata["fogDensity"]["abyssalSiltFogDensityPerMeter"])
            self.assertEqual(
                "named silt/sediment RuntimeVisualProfiles",
                metadata["fogDensity"]["representativeSiltSource"],
            )
            self.assertGreater(metadata["siltProfileScan"]["count"], 0)
            self.assertGreaterEqual(
                metadata["allTurbidityProfileScan"]["count"],
                metadata["siltProfileScan"]["count"],
            )
            self.assertTrue(
                metadata["fogDensity"]["abyssalSiltFogDensitySource"].endswith(
                    "Atmos_AbyssalSilt.asset",
                ),
            )
            self.assertEqual(3, len(metadata["inputSources"]["giRelayLogsReadByCli"]))
            self.assertTrue(metadata["giRelayContract"]["allRequiredRulesPresent"])
            self.assertTrue(metadata["giRelayContract"]["appliedRules"]["depthPaletteFake"])
            self.assertTrue(metadata["giRelayContract"]["appliedRules"]["fogGlobals"])
            self.assertTrue(metadata["giRelayContract"]["appliedRules"]["rejectRuntimeVolumetricGI"])
            self.assertTrue(metadata["giRelayContract"]["appliedRules"]["lowTierSnapStates"])
            self.assertTrue(metadata["giRelayContract"]["appliedRules"]["singleCubemapPath"])
            self.assertGreaterEqual(len(metadata["inputSources"]["atmosphereFogProfiles"]), 1)
            self.assertIn("Water_Extinction_README.md", metadata["sha256"])
            self.assertEqual(3, len(metadata["sourceReferences"]))
            self.assertEqual("PASS", metadata["validation"]["status"])
            self.assertEqual(33_554_432, metadata["validation"]["matrixBytes"])
            self.assertEqual(3_002, metadata["validation"]["fogBytes"])
            self.assertEqual(1501, metadata["validation"]["fogCount"])
            self.assertTrue(metadata["validation"]["snippetHasNoRound"])
            self.assertTrue(metadata["validation"]["snippetHasInt2Load"])
            self.assertTrue(metadata["validation"]["giRelayContractAllRequiredRulesPresent"])
            readme_text = readme_path.read_text(encoding="utf-8")
            self.assertIn("Source References", readme_text)
            self.assertIn("GI Relay Contract Read By CLI", readme_text)
            self.assertIn("NOAA Ocean Explorer", readme_text)
            self.assertIn("Representative silt", readme_text)

            matrix = np.memmap(matrix_path, dtype="<f2", mode="r", shape=(256, 256, 256))
            fog = np.fromfile(fog_path, dtype="<f2")
            self.assertEqual(0.0, float(matrix[85, 0, 255]))
            self.assertEqual(1501, int(fog.size))
            self.assertLess(float(fog[0]), float(fog[-1]))
            self.assertEqual("89504E470D0A1A0A", preview_path.read_bytes()[:8].hex().upper())
            forbidden_round_token = "round" + "("
            self.assertNotIn(forbidden_round_token, snippet_path.read_text(encoding="utf-8"))
            del matrix
            gc.collect()


if __name__ == "__main__":
    unittest.main()
