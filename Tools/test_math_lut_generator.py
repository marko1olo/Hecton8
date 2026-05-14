#!/usr/bin/env python3
"""Byte-size validation for the HECTON-8 math LUT baker."""

from __future__ import annotations

import hashlib
import json
import math
import struct
import tempfile
import unittest
from pathlib import Path

import numpy as np

import MathLUTGenerator


class MathLUTGeneratorTests(unittest.TestCase):
    def test_generated_binary_sizes_match_contract(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            result = MathLUTGenerator.generate_all(output_dir)
            expected_sizes = MathLUTGenerator.expected_bin_sizes()

            self.assertEqual("PASS", result["validation"]["status"])
            for file_name, expected_size in expected_sizes.items():
                actual_size = (output_dir / file_name).stat().st_size
                self.assertEqual(expected_size, actual_size, file_name)

    def test_binary_writer_uses_explicit_little_endian_float32(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "probe.bin"
            table = np.array([1.0, -2.5, 0.25], dtype=np.float32)
            written = MathLUTGenerator.write_float32_bin(output_path, table)

            self.assertEqual(12, written)
            self.assertEqual(
                struct.pack("<f", 1.0) + struct.pack("<f", -2.5) + struct.pack("<f", 0.25),
                output_path.read_bytes(),
            )
            self.assertEqual(4, struct.calcsize("<f"))

    def test_manifest_axes_and_generated_tables_are_sane(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            MathLUTGenerator.generate_all(output_dir)
            manifest = json.loads((output_dir / "math_lut_manifest.json").read_text())
            coefficients = json.loads((output_dir / "ecosystem_coefficients.json").read_text())

            self.assertEqual("little-endian", manifest["byteOrder"])
            self.assertEqual("float32", manifest["scalarFormat"])
            self.assertEqual("<f", manifest["structPackFormat"])
            self.assertEqual(4, manifest["scalarBytes"])
            self.assertEqual(10.0, manifest["files"]["sabine_reverb_rt60.bin"]["axes"]["volumeM3"]["min"])
            self.assertEqual(-100, manifest["files"]["caustics_dispersion_offsets.bin"]["axes"]["depthMeters"]["max"])
            self.assertEqual(1_000_000, coefficients["IntegrationSteps"])
            self.assertTrue(math.isfinite(coefficients["FinalPreyBiomass"]))
            self.assertTrue(math.isfinite(coefficients["FinalPredatorBiomass"]))

            gerstner = MathLUTGenerator.build_gerstner_weather_lut()
            directions = gerstner[:, :, 3:5]
            lengths = np.sqrt(np.sum(directions * directions, axis=2))
            max_error = float(np.max(np.abs(lengths - 1.0)))
            self.assertLess(max_error, 0.000001)

    def test_table_content_ranges_match_design_contract(self) -> None:
        sabine = MathLUTGenerator.build_sabine_reverb_lut()
        dalton = MathLUTGenerator.build_dalton_toxicity_lut()
        gerstner = MathLUTGenerator.build_gerstner_weather_lut()
        caustics = MathLUTGenerator.build_caustics_dispersion_lut()
        ecosystem = MathLUTGenerator.simulate_lotka_volterra()

        self.assertTrue(np.isfinite(sabine).all())
        self.assertGreaterEqual(float(sabine.min()), 0.05)
        self.assertLessEqual(float(sabine.max()), 12.0)

        self.assertEqual(1.0, float(dalton[0, 0]))
        self.assertAlmostEqual(0.2095, float(dalton[0, 1]), places=6)
        self.assertAlmostEqual(0.7808, float(dalton[0, 2]), places=6)
        self.assertEqual(0.0, float(dalton[0, 3]))
        self.assertEqual(1.0, float(dalton[0, 4]))
        self.assertEqual(1.0, float(dalton[-1, 3]))
        self.assertEqual(8.0, float(dalton[-1, 4]))

        self.assertTrue(np.isfinite(gerstner).all())
        self.assertGreater(float(gerstner[:, :, 0].min()), 0.0)
        self.assertGreater(float(gerstner[:, :, 1].min()), 0.0)
        self.assertGreaterEqual(float(gerstner[:, :, 2].min()), 0.019999)
        self.assertLessEqual(float(gerstner[:, :, 2].max()), 0.88)

        self.assertTrue(np.isfinite(caustics).all())
        self.assertEqual(0.0, float(caustics[0, 0]))
        self.assertEqual(0.0, float(caustics[0, 1]))
        self.assertEqual(0.0, float(caustics[0, 2]))

        self.assertAlmostEqual(
            ecosystem["StablePreyBiomass"],
            ecosystem["FinalPreyBiomass"],
            places=6,
        )
        self.assertAlmostEqual(
            ecosystem["StablePredatorBiomass"],
            ecosystem["FinalPredatorBiomass"],
            places=6,
        )

    def test_generate_all_is_byte_deterministic(self) -> None:
        generated_files = (
            "sabine_reverb_rt60.bin",
            "dalton_gas_toxicity.bin",
            "gerstner_wave_weather.bin",
            "caustics_dispersion_offsets.bin",
            "ecosystem_coefficients.json",
            "math_lut_manifest.json",
        )

        with tempfile.TemporaryDirectory() as first_dir, tempfile.TemporaryDirectory() as second_dir:
            first_output = Path(first_dir)
            second_output = Path(second_dir)

            MathLUTGenerator.generate_all(first_output)
            MathLUTGenerator.generate_all(second_output)

            for file_name in generated_files:
                first_bytes = (first_output / file_name).read_bytes()
                second_bytes = (second_output / file_name).read_bytes()
                self.assertEqual(
                    hashlib.sha256(first_bytes).hexdigest(),
                    hashlib.sha256(second_bytes).hexdigest(),
                    file_name,
                )
                self.assertEqual(first_bytes, second_bytes, file_name)


if __name__ == "__main__":
    unittest.main()
