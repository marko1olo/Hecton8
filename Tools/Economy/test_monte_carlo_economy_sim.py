#!/usr/bin/env python3
"""Unit tests for the offline Monte Carlo economy simulator."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path


TOOLS_ECONOMY_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ECONOMY_ROOT.parents[1]
if str(TOOLS_ECONOMY_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ECONOMY_ROOT))

import MonteCarloEconomySim as sim  # noqa: E402


class MonteCarloEconomySimTests(unittest.TestCase):
    def test_lcg_known_vectors_match_uint_overflow(self) -> None:
        self.assertEqual(1013904223, sim.next_lcg(0))
        self.assertEqual(1015568748, sim.next_lcg(1))
        self.assertEqual(288599351, sim.next_lcg(0x48454338))
        self.assertEqual(1012239698, sim.next_lcg(0xFFFFFFFF))

    def test_csharp_lcg_constants_are_mirrored(self) -> None:
        source = (REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "Mining" / "Contracts" / "DeployableSdfDrillContracts.cs").read_text(encoding="utf-8")
        self.assertIn("state * 1664525u", source)
        self.assertIn("+ 1013904223u", source)
        self.assertEqual(1664525, sim.LCG_MULTIPLIER)
        self.assertEqual(1013904223, sim.LCG_INCREMENT)

    def test_mix_known_vectors_match_csharp_shape(self) -> None:
        self.assertEqual(0x79370D7F, sim.mix_u32(0, 0))
        self.assertEqual(0xC7704BBE, sim.mix_u32(0x48454338, 0x2C944C55))
        self.assertEqual(0xB260B65A, sim.mix_u32(0xFFFFFFFF, 0x12345678))

    def test_multiply_high_to_range_avoids_modulo_shape(self) -> None:
        self.assertEqual(0, sim.multiply_high_to_range(0, 100))
        self.assertEqual(99, sim.multiply_high_to_range(0xFFFFFFFF, 100))
        self.assertEqual(50, sim.multiply_high_to_range(0x80000000, 100))

    def test_first_base_dependency_closure_uses_recipe_data(self) -> None:
        recipes = sim.load_recipes(REPO_ROOT)
        demands = sim.resolve_raw_demands(recipes, sim.FIRST_BASE_TARGETS)
        self.assertEqual(18, demands[sim.TITANIUM_ID])
        self.assertEqual(13, demands[sim.COPPER_ID])
        self.assertEqual(3, demands["Data_LithiumCrystal"])
        self.assertEqual(3, demands["Data_NickelOre"])

    def test_distribution_matrix_shape(self) -> None:
        distribution = sim.load_distribution(REPO_ROOT)
        self.assertIn(distribution.source_format, ("csv", "json"))
        self.assertEqual(10, len(distribution.biomes))
        self.assertTrue(all(len(biome.entries) == 15 for biome in distribution.biomes))
        self.assertTrue(all(biome.total_weight > 0 for biome in distribution.biomes))

    def test_summary_persists_million_step_evidence(self) -> None:
        results = [
            sim.PlayerResult(1, True, 600_000, 1.0, 1, 1.0, 1, 1.0, 0),
            sim.PlayerResult(2, True, 400_000, 2.0, 2, 2.0, 2, 2.0, 1),
        ]
        summary = sim.summarize_results(results)
        self.assertEqual(1_000_000, summary["total_nodes_mined"])
        self.assertEqual(1_000_000, summary["monte_carlo_steps"])
        self.assertTrue(summary["million_step_audit_passed"])

    def test_visual_scalability_constants_are_derived(self) -> None:
        self.assertEqual(round(0.015 * 65_535), sim.HIGH_MICA_GLINT_PROBABILITY_Q16)
        self.assertEqual(round(0.050 * 65_535), sim.HIGH_WET_SOOT_OVERLAY_WEIGHT_Q16)
        self.assertEqual(4, sim.HIGH_CLUSTER_NOISE_OCTAVES)
        self.assertEqual(8, sim.HIGH_INSPECTION_GRADIENT_SAMPLES)
        self.assertEqual(6, sim.ULTRA_CLUSTER_NOISE_OCTAVES)
        self.assertEqual(16, sim.ULTRA_INSPECTION_GRADIENT_SAMPLES)
        self.assertEqual(5, sim.ULTRA_HARMONIC_DETAIL_BANDS)
        self.assertEqual(32, sim.ULTRA_RESOURCE_SCAR_HIGHLIGHT_LUT_SAMPLES)

    def test_tuned_scalability_payload_has_derivations_and_no_soft_glint_term(self) -> None:
        distribution = sim.load_distribution(REPO_ROOT)
        summary = {
            "p99_minutes": 59.0,
            "copper_p99_minutes": 55.0,
        }
        payload = sim.distribution_to_json(distribution, summary, summary, [{"pass": 0}], 60.0)
        high = payload["scalability"]["high"]
        ultra = payload["scalability"]["ultra"]
        self.assertEqual("BLACK_RIG_RTX_OVERKILL", high["profile_id"])
        self.assertEqual("ABYSS_GOD_MODE_VISUALS", ultra["profile_id"])
        self.assertIn("mica_glint_probability_q16", high["extra_data"])
        self.assertIn("round(0.015 * 65535)", high["extra_data_derivation"]["mica_glint_probability_q16"])
        self.assertNotIn("spar" + "kle", str(payload).lower())


if __name__ == "__main__":
    unittest.main()
