#!/usr/bin/env python3
"""Unit tests for the first 20 minute balance simulator."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path


TOOLS_ECONOMY_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ECONOMY_ROOT.parents[1]
if str(TOOLS_ECONOMY_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ECONOMY_ROOT))

import FirstTwentyMinuteBalanceSim as sim  # noqa: E402


class FirstTwentyMinuteBalanceSimTests(unittest.TestCase):
    def test_lcg_known_vectors_match_runtime_shape(self) -> None:
        self.assertEqual(1013904223, sim.next_lcg(0))
        self.assertEqual(1015568748, sim.next_lcg(1))
        self.assertEqual(288599351, sim.next_lcg(0x48454338))

    def test_survival_asset_is_runtime_authority(self) -> None:
        stats = sim.load_survival_stats(REPO_ROOT)
        self.assertGreater(stats.max_oxygen, 0.0)
        self.assertGreater(stats.oxygen_consumption_rate, 0.0)
        self.assertEqual("Standard_Suit_V1.asset", stats.source_path.name)

    def test_pressure_factor_matches_hecton_survival_shape(self) -> None:
        stats = sim.SurvivalStatsAuthority(Path("x"), 100.0, 0.5, 50.0, 2.0, 0.02, 0.1, 0.15, 100.0, 100.0)
        shallow = sim.oxygen_drain_rate(stats, 0.0, 0.0, 0.0, 0)
        ten_meters = sim.oxygen_drain_rate(stats, 10.0, 0.0, 0.0, 0)
        self.assertAlmostEqual(shallow * 2.0, ten_meters, places=5)

    def test_distribution_loads_early_biomes(self) -> None:
        distribution = sim.load_distribution(REPO_ROOT)
        biome_ids = {biome.biome_id for biome in distribution.biomes[:3]}
        self.assertTrue(set(sim.EARLY_BIOME_IDS).issubset(biome_ids))
        self.assertTrue(all(biome.total_weight > 0 for biome in distribution.biomes))

    def test_short_simulation_returns_bounded_results(self) -> None:
        distribution = sim.load_distribution(REPO_ROOT)
        stats = sim.load_survival_stats(REPO_ROOT)
        results = sim.run_simulation(distribution, stats, 64, sim.DEFAULT_WORLD_SEED, 120.0)
        summary = sim.summarize(results, 0.001)
        self.assertEqual(64, summary["runs"])
        self.assertGreaterEqual(summary["prob_suffocated_before_wire"], 0.0)
        self.assertLessEqual(summary["prob_suffocated_before_wire"], 1.0)
        self.assertGreaterEqual(summary["sim_microseconds_per_session"], 0.0)


if __name__ == "__main__":
    unittest.main()
