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
    @staticmethod
    def _refresh_manifest_artifact(output_dir: Path, artifact_key: str) -> None:
        verification_path = output_dir / "Submarine_Verification.json"
        verification = json.loads(verification_path.read_text(encoding="utf-8"))
        artifact = verification["artifacts"][artifact_key]
        payload = (output_dir / artifact["file"]).read_bytes()
        artifact["bytes"] = len(payload)
        artifact["sha256"] = hashlib.sha256(payload).hexdigest().upper()
        verification_path.write_text(json.dumps(verification, indent=2, sort_keys=False) + "\n", encoding="utf-8")

    def test_bake_output_contract_and_hydro_gates(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            summary = SubmarinePhysicsSim.run(output_dir)
            specs = json.loads((output_dir / "Submarine_Specs.json").read_text(encoding="utf-8"))

            self.assertEqual("HYDRODYNAMICS DEFINED", summary["status"])
            self.assertTrue(summary["verification_passed"])
            self.assertEqual("Submarine_SpeedPower.png", summary["power_png"])
            self.assertEqual("Submarine_RuntimePack.bin", summary["runtime_pack"])
            self.assertEqual("Submarine_RuntimePackLayout.json", summary["runtime_pack_layout"])
            self.assertEqual(
                {"specs", "power_csv", "power_svg", "power_png", "runtime_pack", "runtime_pack_layout"},
                set(summary["artifacts"].keys()),
            )
            self.assertEqual("HYDRODYNAMICS DEFINED", specs["status"])
            self.assertTrue(specs["verification"]["passed"])
            self.assertEqual(
                {"Low", "Middle", "High", "Ultra"},
                set(specs["quality_tier_runtime_usage"].keys()),
            )
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

    def test_lift_curve_samples_are_symmetric_and_bounded(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            specs = json.loads((output_dir / "Submarine_Specs.json").read_text(encoding="utf-8"))

            expected_angles = [float(angle) for angle in SubmarinePhysicsSim.LIFT_SAMPLE_ANGLES_DEGREES]
            for hull in specs["hulls"]:
                samples = hull["lift_curve_samples"]
                coefficients = hull["hydrodynamic_coefficients"]
                self.assertEqual(expected_angles, [row["angle_degrees"] for row in samples])
                self.assertEqual(0.0, samples[3]["pitch_Cl"])
                self.assertEqual(0.0, samples[3]["yaw_Cl"])
                for left_index in range(3):
                    right_index = len(samples) - 1 - left_index
                    self.assertAlmostEqual(
                        -samples[left_index]["pitch_Cl"],
                        samples[right_index]["pitch_Cl"],
                        places=6,
                        msg=hull["shape_id"],
                    )
                    self.assertAlmostEqual(
                        -samples[left_index]["yaw_Cl"],
                        samples[right_index]["yaw_Cl"],
                        places=6,
                        msg=hull["shape_id"],
                    )
                max_cl = coefficients["Cl_control_surface_max"]
                for sample in samples:
                    self.assertLessEqual(abs(sample["pitch_Cl"]), max_cl, hull["shape_id"])
                    self.assertLessEqual(abs(sample["yaw_Cl"]), max_cl, hull["shape_id"])

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

    def test_write_json_rejects_nonfinite_output(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            target = Path(temp_dir) / "bad.json"

            with self.assertRaises(ValueError):
                SubmarinePhysicsSim.write_json(target, {"bad": float("nan")})
            self.assertFalse(target.exists())

    def test_validate_existing_output_passes_clean_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)

            self.assertEqual("HYDRODYNAMICS DEFINED", validation["status"])
            self.assertTrue(validation["verification_passed"])
            self.assertEqual([], validation["failures"])
            self.assertEqual("verify_only", validation["mode"])

    def test_validate_existing_output_detects_verification_summary_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            verification_path = output_dir / "Submarine_Verification.json"
            verification = json.loads(verification_path.read_text(encoding="utf-8"))
            verification["power_png"] = "Wrong_File.png"
            verification["failures"] = ["stale failure"]
            verification_path.write_text(json.dumps(verification, indent=2, sort_keys=False) + "\n", encoding="utf-8")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertIn("verification summary file mismatch: power_png", validation["failures"])
            self.assertIn("verification failures list mismatch", validation["failures"])

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

    def test_validate_existing_output_detects_png_payload_drift_after_manifest_update(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            target = output_dir / "Submarine_SpeedPower.png"
            payload = bytearray(target.read_bytes())
            payload[-24] ^= 0x08
            target.write_bytes(payload)
            self._refresh_manifest_artifact(output_dir, "power_png")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_SpeedPower.png", validation["failures"])
            self.assertIn("png canonical payload mismatch", validation["failures"])

    def test_validate_existing_output_detects_svg_payload_drift_after_manifest_update(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            target = output_dir / "Submarine_SpeedPower.svg"
            svg_text = target.read_text(encoding="utf-8")
            target.write_text(svg_text.replace("Speed (m/s)", "Speed broken", 1), encoding="utf-8")
            self._refresh_manifest_artifact(output_dir, "power_svg")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_SpeedPower.svg", validation["failures"])
            self.assertIn("svg canonical payload mismatch", validation["failures"])

    def test_validate_existing_output_detects_runtime_pack_payload_corruption(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            target = output_dir / "Submarine_RuntimePack.bin"
            payload = bytearray(target.read_bytes())
            header_size = struct.calcsize(SubmarinePhysicsSim.RUNTIME_PACK_HEADER_FORMAT)
            payload[header_size + 10] ^= 0x7F
            target.write_bytes(payload)
            self._refresh_manifest_artifact(output_dir, "runtime_pack")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_RuntimePack.bin", validation["failures"])
            self.assertTrue(
                any(
                    failure.startswith("runtime pack field mismatch: SLEEK length_m")
                    for failure in validation["failures"]
                ),
                validation["failures"],
            )

    def test_validate_existing_output_detects_power_csv_semantic_drift_after_manifest_update(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            csv_path = output_dir / "Submarine_SpeedPower.csv"
            with csv_path.open(encoding="utf-8", newline="") as handle:
                rows = list(csv.DictReader(handle))
            rows[-1]["SLEEK"] = f"{float(rows[-1]['SLEEK']) + 0.125:.6f}"
            with csv_path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=["speed_mps"] + [shape.shape_id for shape in SubmarinePhysicsSim.HULL_SHAPES])
                writer.writeheader()
                writer.writerows(rows)
            self._refresh_manifest_artifact(output_dir, "power_csv")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_SpeedPower.csv", validation["failures"])
            self.assertIn("power csv value mismatch: row 71 SLEEK", validation["failures"])

    def test_validate_existing_output_rejects_nonfinite_power_csv_value(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            csv_path = output_dir / "Submarine_SpeedPower.csv"
            with csv_path.open(encoding="utf-8", newline="") as handle:
                rows = list(csv.DictReader(handle))
            rows[3]["SLEEK"] = "nan"
            with csv_path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=["speed_mps"] + [shape.shape_id for shape in SubmarinePhysicsSim.HULL_SHAPES])
                writer.writeheader()
                writer.writerows(rows)
            self._refresh_manifest_artifact(output_dir, "power_csv")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_SpeedPower.csv", validation["failures"])
            self.assertIn("power csv non-finite value: row 3 SLEEK", validation["failures"])

    def test_validate_existing_output_rejects_nonfinite_spec_numbers(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            specs_path = output_dir / "Submarine_Specs.json"
            specs = json.loads(specs_path.read_text(encoding="utf-8"))
            specs["hulls"][0]["terminal_speed_at_max_thrust_mps"] = float("nan")
            specs_path.write_text(json.dumps(specs, indent=2, sort_keys=False) + "\n", encoding="utf-8")
            self._refresh_manifest_artifact(output_dir, "specs")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_Specs.json", validation["failures"])
            self.assertIn(
                "non-finite number: specs.hulls[0].terminal_speed_at_max_thrust_mps",
                validation["failures"],
            )

    def test_validate_existing_output_detects_specs_metadata_drift_after_manifest_update(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            specs_path = output_dir / "Submarine_Specs.json"
            specs = json.loads(specs_path.read_text(encoding="utf-8"))
            specs["hulls"][0]["display_name"] = "Sleek Scout Broken"
            specs_path.write_text(json.dumps(specs, indent=2, sort_keys=False, allow_nan=False) + "\n", encoding="utf-8")
            self._refresh_manifest_artifact(output_dir, "specs")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_Specs.json", validation["failures"])
            self.assertIn("specs document mismatch", validation["failures"])

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
            "Submarine_RuntimePack.bin",
            "Submarine_RuntimePackLayout.json",
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

    def test_runtime_pack_contract_and_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            payload = (output_dir / "Submarine_RuntimePack.bin").read_bytes()
            header_size = struct.calcsize(SubmarinePhysicsSim.RUNTIME_PACK_HEADER_FORMAT)
            record_size = struct.calcsize(SubmarinePhysicsSim.RUNTIME_PACK_RECORD_FORMAT)
            magic, version, hull_count, float_count, stride = struct.unpack(
                SubmarinePhysicsSim.RUNTIME_PACK_HEADER_FORMAT,
                payload[:header_size],
            )

            self.assertEqual(SubmarinePhysicsSim.RUNTIME_PACK_MAGIC, magic)
            self.assertEqual(SubmarinePhysicsSim.RUNTIME_PACK_VERSION, version)
            self.assertEqual(len(SubmarinePhysicsSim.HULL_SHAPES), hull_count)
            self.assertEqual(SubmarinePhysicsSim.RUNTIME_PACK_FLOAT_COUNT, float_count)
            self.assertEqual(record_size, stride)
            self.assertEqual(header_size + (record_size * hull_count), len(payload))

            for index, shape in enumerate(SubmarinePhysicsSim.HULL_SHAPES):
                offset = header_size + (record_size * index)
                record = struct.unpack(
                    SubmarinePhysicsSim.RUNTIME_PACK_RECORD_FORMAT,
                    payload[offset:offset + record_size],
                )
                self.assertEqual(SubmarinePhysicsSim.fnv1a_32(shape.shape_id), record[0])
                self.assertEqual(index, record[1])
                self.assertGreater(record[2], 0.0, shape.shape_id)
                self.assertGreater(record[5], 0.0, shape.shape_id)

    def test_runtime_pack_round_trip_matches_json_export(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            specs = json.loads((output_dir / "Submarine_Specs.json").read_text(encoding="utf-8"))
            records = SubmarinePhysicsSim.read_runtime_pack(output_dir / "Submarine_RuntimePack.bin")

            self.assertEqual(len(specs["hulls"]), len(records))
            for index, hull in enumerate(specs["hulls"]):
                shape_id = hull["shape_id"]
                record = records[index]
                self.assertEqual(SubmarinePhysicsSim.fnv1a_32(shape_id), record["shape_hash"])
                self.assertEqual(index, record["shape_index"])
                values = record["values"]
                expected_values = SubmarinePhysicsSim.runtime_record_values(hull)
                for field_index, field_name in enumerate(SubmarinePhysicsSim.RUNTIME_PACK_FLOAT_FIELDS):
                    expected = expected_values[field_index]
                    tolerance = max(0.001, abs(expected) * 0.00001)
                    self.assertAlmostEqual(
                        expected,
                        values[field_name],
                        delta=tolerance,
                        msg=f"{shape_id} {field_name}",
                    )

    def test_runtime_pack_layout_documents_record_offsets(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            layout = json.loads((output_dir / "Submarine_RuntimePackLayout.json").read_text(encoding="utf-8"))
            record = layout["record"]
            fields = record["fields"]

            self.assertEqual("hecton8.submarine_runtime_pack_layout.v1", layout["schema_id"])
            self.assertEqual(SubmarinePhysicsSim.RUNTIME_PACK_VERSION, layout["version"])
            self.assertEqual(struct.calcsize(SubmarinePhysicsSim.RUNTIME_PACK_RECORD_FORMAT), record["bytes"])
            self.assertEqual(SubmarinePhysicsSim.RUNTIME_PACK_FLOAT_COUNT + 2, len(fields))
            self.assertEqual("shape_hash_fnv1a32", fields[0]["name"])
            self.assertEqual("shape_index", fields[1]["name"])
            self.assertEqual("length_m", fields[2]["name"])
            self.assertEqual(8, fields[2]["byte_offset_from_record_start"])
            self.assertEqual(
                SubmarinePhysicsSim.RUNTIME_PACK_FLOAT_FIELDS[-1],
                fields[-1]["name"],
            )

    def test_validate_existing_output_detects_runtime_layout_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            SubmarinePhysicsSim.run(output_dir)
            target = output_dir / "Submarine_RuntimePackLayout.json"
            layout = json.loads(target.read_text(encoding="utf-8"))
            layout["record"]["fields"][2]["name"] = "length_m_broken"
            target.write_text(json.dumps(layout, indent=2, sort_keys=False) + "\n", encoding="utf-8")
            self._refresh_manifest_artifact(output_dir, "runtime_pack_layout")

            validation = SubmarinePhysicsSim.validate_existing_output(output_dir)
            self.assertEqual("PENDING VERIFICATION", validation["status"])
            self.assertFalse(validation["verification_passed"])
            self.assertNotIn("artifact sha256 mismatch: Submarine_RuntimePackLayout.json", validation["failures"])
            self.assertIn("runtime layout document mismatch", validation["failures"])
            self.assertIn("runtime layout field definition mismatch", validation["failures"])


if __name__ == "__main__":
    unittest.main()
