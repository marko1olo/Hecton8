#!/usr/bin/env python3
"""Validation tests for the HECTON-8 atmosphere LUT baker."""

from __future__ import annotations

import hashlib
import json
import math
import struct
import tempfile
import unittest
from pathlib import Path

import AtmoPreview


class AtmoPreviewTests(unittest.TestCase):
    def test_scattering_coefficients_and_phase_functions_are_sane(self) -> None:
        self.assertGreater(AtmoPreview.RAYLEIGH_BETA[2], AtmoPreview.RAYLEIGH_BETA[1])
        self.assertGreater(AtmoPreview.RAYLEIGH_BETA[1], AtmoPreview.RAYLEIGH_BETA[0])
        self.assertTrue(all(math.isfinite(value) for value in AtmoPreview.MIE_BETA))

        ray_side = AtmoPreview.rayleigh_phase(0.0)
        ray_forward = AtmoPreview.rayleigh_phase(1.0)
        mie_side = AtmoPreview.mie_phase(0.0)
        mie_forward = AtmoPreview.mie_phase(1.0)

        self.assertGreater(ray_forward, ray_side)
        self.assertGreater(mie_forward, mie_side)
        self.assertTrue(math.isfinite(ray_forward))
        self.assertTrue(math.isfinite(mie_forward))

    def test_generated_binary_sizes_match_half_float_contract(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            result = AtmoPreview.generate_all(output_dir, report_path=None)
            expected_sizes = AtmoPreview.expected_bin_sizes()

            self.assertEqual("PASS", result["validation"]["status"])
            for file_name, expected_size in expected_sizes.items():
                self.assertEqual(expected_size, (output_dir / file_name).stat().st_size, file_name)

    def test_half_writer_uses_explicit_little_endian_half_float(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "probe.bin"
            written = AtmoPreview.write_half_float_bin(output_path, [(1.0, 0.5, 0.25, 0.0)])

            self.assertEqual(8, written)
            self.assertEqual(
                struct.pack("<e", 1.0)
                + struct.pack("<e", 0.5)
                + struct.pack("<e", 0.25)
                + struct.pack("<e", 0.0),
                output_path.read_bytes(),
            )
            self.assertEqual(2, struct.calcsize("<e"))

    def test_density_matrix_has_128_finite_layers_from_zero_to_100km(self) -> None:
        density = AtmoPreview.build_density_matrix()

        self.assertEqual(128, len(density))
        self.assertEqual(0.0, density[0][0])
        self.assertEqual(100.0, density[-1][0])
        self.assertEqual(1.0, density[0][1])
        self.assertEqual(1.0, density[0][2])
        self.assertLess(density[-1][1], density[0][1])
        self.assertLess(density[-1][2], density[0][2])
        for row in density:
            self.assertTrue(all(math.isfinite(value) for value in row))

    def test_sky_gradient_audit_passes_without_surface_line(self) -> None:
        sky_lut, audit = AtmoPreview.build_sky_lut()

        self.assertEqual(AtmoPreview.SKY_LUT_WIDTH * AtmoPreview.SKY_LUT_HEIGHT, len(sky_lut))
        self.assertEqual("PASS", audit["status"], audit)
        self.assertLessEqual(
            audit["maxSurfaceSeamDelta"],
            AtmoPreview.SURFACE_SEAM_MAX_DELTA,
        )
        self.assertLessEqual(audit["voidBlackLuminance"], AtmoPreview.VOID_BLACK_MAX_LUMINANCE)
        self.assertGreaterEqual(
            audit["goldenHourLuminance"],
            AtmoPreview.MIN_GOLDEN_HOUR_LUMINANCE,
        )

    def test_manifest_hashes_match_generated_payloads(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            AtmoPreview.generate_all(output_dir, report_path=None)
            manifest = json.loads((output_dir / AtmoPreview.MANIFEST_FILE).read_text())

            self.assertEqual("float16", manifest["scalarFormat"])
            self.assertEqual("<e", manifest["structPackFormat"])
            self.assertEqual(2, manifest["scalarBytes"])
            for file_name, file_info in manifest["files"].items():
                payload_digest = hashlib.sha256((output_dir / file_name).read_bytes()).hexdigest().upper()
                self.assertEqual(payload_digest, file_info["sha256"], file_name)
                self.assertEqual(64, len(file_info["sha256"]), file_name)

            preview_digest = hashlib.sha256(
                (output_dir / AtmoPreview.PREVIEW_FILE).read_bytes()
            ).hexdigest().upper()
            self.assertEqual(preview_digest, manifest["preview"]["sha256"])

    def test_verify_decodes_quantized_half_payloads(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            result = AtmoPreview.generate_all(output_dir, report_path=None)
            validation = result["validation"]

            self.assertEqual("PASS", validation["status"])
            decoded = validation["decodedPayloads"]
            self.assertEqual("PASS", decoded[AtmoPreview.SKY_LUT_FILE]["status"])
            self.assertEqual("PASS", decoded[AtmoPreview.DENSITY_FILE]["status"])
            self.assertEqual(0, decoded[AtmoPreview.SKY_LUT_FILE]["nonFiniteCount"])
            self.assertEqual(0, decoded[AtmoPreview.DENSITY_FILE]["nonFiniteCount"])

    def test_verify_rejects_hashed_but_nonfinite_half_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            AtmoPreview.generate_all(output_dir, report_path=None)

            sky_path = output_dir / AtmoPreview.SKY_LUT_FILE
            payload = bytearray(sky_path.read_bytes())
            payload[0:2] = struct.pack("<e", math.inf)
            sky_path.write_bytes(payload)

            manifest_path = output_dir / AtmoPreview.MANIFEST_FILE
            manifest = json.loads(manifest_path.read_text())
            manifest["files"][AtmoPreview.SKY_LUT_FILE]["sha256"] = hashlib.sha256(
                sky_path.read_bytes()
            ).hexdigest().upper()
            manifest_path.write_text(
                json.dumps(manifest, indent=2, sort_keys=True) + "\n",
                encoding="utf-8",
            )

            validation = AtmoPreview.validate_existing_output(output_dir)
            decoded_sky = validation["decodedPayloads"][AtmoPreview.SKY_LUT_FILE]

            self.assertEqual("FAIL", validation["status"])
            self.assertTrue(validation["files"][AtmoPreview.SKY_LUT_FILE]["hashMatches"])
            self.assertEqual("FAIL", decoded_sky["status"])
            self.assertIn("non_finite_sample", decoded_sky["failures"])

    def test_curvature_fake_is_monotonic_and_bounded(self) -> None:
        near = AtmoPreview.curvature_depth_remap(0.0)
        middle = AtmoPreview.curvature_depth_remap(2_500.0)
        far = AtmoPreview.curvature_depth_remap(5_000.0)
        beyond = AtmoPreview.curvature_depth_remap(50_000.0)

        self.assertEqual(0.0, near)
        self.assertLess(near, middle)
        self.assertLess(middle, far)
        self.assertEqual(1.0, far)
        self.assertEqual(1.0, beyond)
        self.assertGreater(AtmoPreview.fake_planet_horizon_drop_m(5_000.0), 0.0)


if __name__ == "__main__":
    unittest.main()
