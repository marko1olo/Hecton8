#!/usr/bin/env python3
from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Tools import SabineBaker as baker
from Tools.test_local_temp import project_local_tempdir_factory


TEMP_DIR = project_local_tempdir_factory("sabine_baker")


class SabineBakerTests(unittest.TestCase):
    def test_clamp(self) -> None:
        self.assertEqual(5.0, baker.clamp(5.0, 0.0, 10.0))
        self.assertEqual(0.0, baker.clamp(-5.0, 0.0, 10.0))
        self.assertEqual(10.0, baker.clamp(15.0, 0.0, 10.0))
        self.assertEqual(0.0, baker.clamp(0.0, 0.0, 10.0))
        self.assertEqual(10.0, baker.clamp(10.0, 0.0, 10.0))

    def test_fnv1a_ascii_lower(self) -> None:
        # Same result for uppercase and lowercase characters
        self.assertEqual(baker.fnv1a_ascii_lower("TestString"), baker.fnv1a_ascii_lower("teststring"))
        self.assertEqual(baker.fnv1a_ascii_lower("TEST"), baker.fnv1a_ascii_lower("test"))
        self.assertEqual(0, baker.fnv1a_ascii_lower(""))

        # Basic expected hashes
        test_hash = baker.fnv1a_ascii_lower("test")
        self.assertIsInstance(test_hash, int)
        self.assertTrue(test_hash > 0)

        # Check against manual reference implementation
        expected_test = 2166136261
        for char in "test":
            expected_test ^= ord(char)
            expected_test = (expected_test * 16777619) & 0xFFFFFFFF
        self.assertEqual(expected_test, test_hash)

    def test_material_alpha(self) -> None:
        metal_alpha = baker.material_alpha("Metal")
        self.assertTrue(baker.ABSORPTION_MIN <= metal_alpha <= baker.ABSORPTION_MAX)

        with self.assertRaisesRegex(ValueError, "unknown material preset: UnknownMaterial"):
            baker.material_alpha("UnknownMaterial")

    def test_sabine_rt60_seconds(self) -> None:
        volume = 1000.0
        surface = baker.equal_volume_cube_surface_area(volume)
        alpha = 0.5

        rt60 = baker.sabine_rt60_seconds(volume, surface, alpha)

        equivalent_absorption = max(surface * alpha, baker.EQUIVALENT_ABSORPTION_EPSILON)
        expected_rt60 = min(baker.SABINE_COEFFICIENT * volume / equivalent_absorption, baker.RT60_MAX_SECONDS)

        self.assertAlmostEqual(expected_rt60, float(rt60))

    def test_thorp_absorption_db_per_km(self) -> None:
        freq = 16.0
        absorption = baker.thorp_absorption_db_per_km(freq)
        self.assertTrue(absorption > 0.0)

    def test_hydrostatic_pressure_pa(self) -> None:
        surface_pressure = baker.hydrostatic_pressure_pa(0.0)
        self.assertAlmostEqual(baker.ATMOSPHERIC_PRESSURE_PA, float(surface_pressure))

        depth_pressure = baker.hydrostatic_pressure_pa(10.0)
        expected_pressure = baker.ATMOSPHERIC_PRESSURE_PA + (baker.SEAWATER_DENSITY_KG_M3 * baker.STANDARD_GRAVITY_MPS2 * 10.0)
        self.assertAlmostEqual(expected_pressure, float(depth_pressure))

        # Test that negative depth is clamped to 0
        negative_depth_pressure = baker.hydrostatic_pressure_pa(-10.0)
        self.assertAlmostEqual(baker.ATMOSPHERIC_PRESSURE_PA, float(negative_depth_pressure))

    def test_simulate_metal_room(self) -> None:
        mock_result = baker.simulate_metal_room(5.0)
        self.assertEqual(50.0, mock_result.width_m)
        self.assertEqual(50.0, mock_result.depth_m)
        self.assertEqual(5.0, mock_result.height_m)
        self.assertEqual(50.0 * 50.0 * 5.0, mock_result.volume_m3)
        self.assertTrue(mock_result.alpha > 0.0)
        self.assertTrue(mock_result.rt60_seconds > 0.0)

        with self.assertRaisesRegex(ValueError, "mock room height must be finite and positive"):
            baker.simulate_metal_room(-5.0)

        with self.assertRaisesRegex(ValueError, "mock room height must be finite and positive"):
            baker.simulate_metal_room(0.0)


if __name__ == "__main__":
    unittest.main()
