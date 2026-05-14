#!/usr/bin/env python3
"""Regression tests for the HECTON-8 acoustic reverb LUT baker."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import AcousticValidator


class AcousticValidatorTests(unittest.TestCase):
    def test_bake_output_contract_and_header(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "Reverb_LUT.bin"
            bake_info = AcousticValidator.bake_reverb_lut(output_path)
            file_bytes = output_path.read_bytes()
            header = AcousticValidator.read_header(file_bytes)
            _, report = AcousticValidator.validate_reverb_lut(output_path)

            self.assertEqual(AcousticValidator.EXPECTED_FILE_BYTES, bake_info["bytes"])
            self.assertEqual(AcousticValidator.EXPECTED_FILE_BYTES, len(file_bytes))
            self.assertEqual(AcousticValidator.MAGIC, header["magic"])
            self.assertEqual(AcousticValidator.VERSION, header["version"])
            self.assertEqual(AcousticValidator.HEADER_BYTES, header["headerBytes"])
            self.assertEqual(AcousticValidator.VOLUME_COUNT, header["volumeCount"])
            self.assertEqual(AcousticValidator.ABSORPTION_COUNT, header["absorptionCount"])
            self.assertEqual(AcousticValidator.PAYLOAD_BYTES, header["payloadBytes"])
            self.assertTrue(any("Mega-Cave" in line for line in report))

    def test_bake_is_byte_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as first_dir, tempfile.TemporaryDirectory() as second_dir:
            first_output = Path(first_dir) / "Reverb_LUT.bin"
            second_output = Path(second_dir) / "Reverb_LUT.bin"

            AcousticValidator.bake_reverb_lut(first_output)
            AcousticValidator.bake_reverb_lut(second_output)

            self.assertEqual(first_output.read_bytes(), second_output.read_bytes())

    def test_recursive_edge_cases_match_formula(self) -> None:
        matrix = AcousticValidator.build_reverb_matrix()
        volumes, absorption = AcousticValidator.build_axes()
        report = []

        AcousticValidator.validate_edge_cases_recursive(
            matrix,
            volumes,
            absorption,
            AcousticValidator.EDGE_CASES,
            0,
            report,
        )

        self.assertEqual(len(AcousticValidator.EDGE_CASES), len(report))
        self.assertTrue(report[0].startswith("Small locker:"))
        self.assertTrue(any("Giant Void" in line for line in report))

    def test_corrupt_payload_crc_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "Reverb_LUT.bin"
            AcousticValidator.bake_reverb_lut(output_path)
            payload = bytearray(output_path.read_bytes())
            payload[AcousticValidator.HEADER_BYTES] ^= 0xFF
            output_path.write_bytes(payload)

            with self.assertRaisesRegex(ValueError, "CRC32"):
                AcousticValidator.validate_reverb_lut(output_path)

    def test_truncated_payload_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "Reverb_LUT.bin"
            output_path.write_bytes(b"short")

            with self.assertRaisesRegex(ValueError, "byte size mismatch"):
                AcousticValidator.validate_reverb_lut(output_path)


if __name__ == "__main__":
    unittest.main()
