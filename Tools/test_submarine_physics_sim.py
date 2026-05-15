#!/usr/bin/env python3
"""Regression tests for the HECTON-8 submarine hydrodynamics baker."""

from __future__ import annotations

import csv
import hashlib
import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import SubmarinePhysicsSim


class SubmarinePhysicsSimTests(unittest.TestCase):
    def test_bake_output_contract_and_hydro_gates(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            summary = SubmarinePhysicsSim.run(output_dir)
            specs = json.loads((output_dir / "Submarine_Specs.json").read_text(encoding="utf-8"))

            self.assertEqual("HYDRODYNAMICS DEFINED", summary["status"])
            self.assertTrue(summary["verification_passed"])
            self.assertEqual("Submarine_SpeedPower.png", summary["power_png"])
            self.assertEqual(
                {"specs", "power_csv", "power_svg", "power_png"},
                set(summary["artifacts"].keys()),
            )
            self.assertEqual("HYDRODYNAMICS DEFINED", specs["status"])
            self.assertTrue(specs["verification"]["passed"])
            self.assertEqual(
                ["SLEEK", "INDUSTRIAL", "BOXY", "ALIEN", "ARMORED_CRAWLER"],
                [hull["shape_id"] for hull in specs["hulls"]],
            )
            self.assertIn("expensive_weight_feel", specs)

            for hull in specs["hulls"]:
                verification = hull["verification"]
                self.assertGreaterEqual(
                    verification["stop_distance_hull_lengths"],
                    SubmarinePhysicsSim.MIN_STOP_DISTANCE_HULL_LENGTHS,
                    hull["shape_id"],
                )
                self.assertIsNone(
                    verification["acceleration_gate"]["time_to_50_mps_seconds"],
                    hull["shape_id"],
                )
                self.assertLess(
                    hull["terminal_speed_at_max_thrust_mps"],
                    SubmarinePhysicsSim.MAX_ACCEPTED_TERMINAL_SPEED_MPS,
                    hull["shape_id"],
                )

    def test_verification_manifest_hashes_match_payloads(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            verification = json.loads((output_dir / "Submarine_Verification.json").read_text(encoding="utf-8"))

            for artifact in verification["artifacts"].values():
                payload = (output_dir / artifact["file"]).read_bytes()
                self.assertEqual(len(payload), artifact["bytes"], artifact["file"])
                self.assertEqual(
                    hashlib.sha256(payload).hexdigest().upper(),
                    artifact["sha256"],
                    artifact["file"],
                )

    def test_validate_existing_output_passes_clean_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)

            self.assertEqual("HYDRODYNAMICS DEFINED", validation["status"])
            self.assertTrue(validation["verification_passed"])
            self.assertEqual([], validation["failures"])
            self.assertEqual("verify_only", validation["mode"])

    def test_validate_existing_output_detects_same_size_corruption(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            target = output_dir / "Submarine_SpeedPower.png"
            payload = bytearray(target.read_bytes())
            payload[-16] ^= 0x11
            target.write_bytes(payload)

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertIn("artifact sha256 mismatch: Submarine_SpeedPower.png", validation["failures"])

    def test_tensors_are_diagonal_positive_and_finite(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            specs = json.loads((output_dir / "Submarine_Specs.json").read_text(encoding="utf-8"))
            tensor_keys = (
                "drag_tensor_cd_area_m2_local_xyz",
                "square_drag_accel_tensor_per_meter_local_xyz",
                "added_mass_tensor_kg_local_xyz",
                "effective_mass_tensor_kg_local_xyz",
                "angular_quadratic_damping_torque_tensor_n_m_per_rad_s_sq_local_xyz",
            )

            for hull in specs["hulls"]:
                for key in tensor_keys:
                    matrix = hull[key]
                    self.assertEqual(3, len(matrix), f"{hull['shape_id']} {key}")
                    for row_index, row in enumerate(matrix):
                        self.assertEqual(3, len(row), f"{hull['shape_id']} {key}")
                        for col_index, value in enumerate(row):
                            if row_index == col_index:
                                self.assertGreater(value, 0.0, f"{hull['shape_id']} {key}")
                            else:
                                self.assertEqual(0.0, value, f"{hull['shape_id']} {key}")

    def test_cavitation_thresholds_increase_with_depth(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            specs = json.loads((output_dir / "Submarine_Specs.json").read_text(encoding="utf-8"))

            for hull in specs["hulls"]:
                previous_depth = -1.0
                previous_onset = -1.0
                for row in hull["cavitation"]["thresholds_by_depth"]:
                    self.assertGreater(row["depth_m"], previous_depth, hull["shape_id"])
                    self.assertGreater(row["hull_speed_onset_mps"], previous_onset, hull["shape_id"])
                    previous_depth = row["depth_m"]
                    previous_onset = row["hull_speed_onset_mps"]

    def test_power_curve_is_monotonic_for_every_hull(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            with (output_dir / "Submarine_SpeedPower.csv").open(encoding="utf-8") as handle:
                rows = list(csv.DictReader(handle))

            self.assertEqual(72, len(rows))
            for shape in [hull.shape_id for hull in SubmarinePhysicsSim.HULL_SHAPES]:
                previous = -1.0
                for row in rows:
                    value = float(row[shape])
                    self.assertGreaterEqual(value, previous, shape)
                    previous = value

    def test_png_plot_contract_is_self_contained(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            png_bytes = (output_dir / "Submarine_SpeedPower.png").read_bytes()

            self.assertEqual(b"\x89PNG\r\n\x1a\n", png_bytes[:8])
            self.assertEqual(b"IHDR", png_bytes[12:16])
            width, height, bit_depth, color_type = struct.unpack(">IIBB", png_bytes[16:26])
            self.assertEqual(1200, width)
            self.assertEqual(720, height)
            self.assertEqual(8, bit_depth)
            self.assertEqual(2, color_type)

    def test_baker_is_byte_deterministic_across_output_directories(self) -> None:
        generated_files = (
            "Submarine_Specs.json",
            "Submarine_SpeedPower.csv",
            "Submarine_SpeedPower.svg",
            "Submarine_SpeedPower.png",
            "Submarine_Verification.json",
        )
        with tempfile.TemporaryDirectory() as first_dir, tempfile.TemporaryDirectory() as second_dir:
            first_output = Path(first_dir)
            second_output = Path(second_dir)
            SubmarinePhysicsSim.run(first_output)
            SubmarinePhysicsSim.run(second_output)

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
