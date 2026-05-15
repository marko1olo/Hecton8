#!/usr/bin/env python3
"""Regression tests for the HECTON-8 acoustic material echo simulator."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import AudioSim


class AudioSimTests(unittest.TestCase):
    def test_profile_contract_has_twenty_energy_balanced_materials(self) -> None:
        payload = AudioSim.load_profile_payload(AudioSim.DEFAULT_PROFILE_PATH)
        materials = AudioSim.parse_materials(payload)
        AudioSim.validate_recipes(payload)

        self.assertEqual(AudioSim.REQUIRED_MATERIAL_COUNT, len(materials))
        self.assertIn("steel_hull", materials)
        self.assertIn("creature_cartilage", materials)

    def test_ten_by_ten_room_outputs_expected_first_order_taps(self) -> None:
        payload = AudioSim.load_profile_payload(AudioSim.DEFAULT_PROFILE_PATH)
        materials = AudioSim.parse_materials(payload)
        room = AudioSim.Room(10.0, 10.0, 3.0)
        taps = AudioSim.simulate_echo_taps(room, materials["steel_hull"], include_second_order=False)

        self.assertEqual(6, len(taps))
        self.assertIn(taps[0].surface, ("ceiling", "floor"))
        self.assertAlmostEqual((3.0 / AudioSim.SOUND_SPEED_SEAWATER_MPS) * 1000.0, taps[0].delay_ms, places=6)
        wall_taps = [tap for tap in taps if tap.surface.endswith("_wall")]
        self.assertEqual(4, len(wall_taps))
        self.assertAlmostEqual((10.0 / AudioSim.SOUND_SPEED_SEAWATER_MPS) * 1000.0, wall_taps[0].delay_ms, places=6)

    def test_clipping_audit_keeps_sixteen_voices_under_one(self) -> None:
        payload = AudioSim.load_profile_payload(AudioSim.DEFAULT_PROFILE_PATH)
        materials = AudioSim.parse_materials(payload)
        room = AudioSim.Room(10.0, 10.0, 3.0)
        taps = AudioSim.simulate_echo_taps(room, materials["steel_hull"], include_second_order=True)
        clipping = AudioSim.audit_clipping(taps, payload, 16)

        self.assertEqual("PASS", clipping["status"])
        self.assertLessEqual(clipping["peakBound16Voices"], 1.0)

    def test_all_material_clipping_audit_keeps_sixteen_voices_under_one(self) -> None:
        payload = AudioSim.load_profile_payload(AudioSim.DEFAULT_PROFILE_PATH)
        materials = AudioSim.parse_materials(payload)
        room = AudioSim.Room(10.0, 10.0, 3.0)
        audit = AudioSim.audit_all_material_clipping(
            materials,
            payload,
            room,
            16,
            include_second_order=True,
        )

        self.assertEqual("PASS", audit["status"])
        self.assertEqual(AudioSim.REQUIRED_MATERIAL_COUNT, audit["materialCount"])
        self.assertEqual(0, audit["failedCount"])
        self.assertLessEqual(audit["worstPeakBound16Voices"], 1.0)

    def test_run_writes_serializable_report_shape(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = Path(temp_dir) / "AudioSim_LastRun.json"
            args = AudioSim.parse_args(["--json-output", str(output_path)])
            report = AudioSim.run(args)
            output_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            loaded = json.loads(output_path.read_text(encoding="utf-8"))

            self.assertEqual(AudioSim.STATUS_OK, loaded["status"])
            self.assertEqual("steel_hull", loaded["materialId"])
            self.assertEqual("PASS", loaded["clippingAudit"]["status"])
            self.assertEqual("PASS", loaded["allMaterialClippingAudit"]["status"])
            self.assertEqual(AudioSim.REQUIRED_MATERIAL_COUNT, loaded["allMaterialClippingAudit"]["materialCount"])
            self.assertGreaterEqual(len(loaded["virtualTaps"]), 6)


if __name__ == "__main__":
    unittest.main()
